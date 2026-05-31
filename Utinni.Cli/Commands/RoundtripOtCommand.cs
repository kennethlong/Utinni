/**
 * MIT License
 *
 * Copyright (c) 2020 Philip Klatt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
**/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.ObjectTemplate;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("roundtrip-ot", HelpText = "Parse -> optional add/remove/edit override -> serialize -> re-parse an object-template .iff; assert byte-exact identity on untouched params.")]
    public class RoundtripOtOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the object-template .iff file.")]
        public string Path { get; set; }

        [Option("add-override", HelpText = "Field name to promote to a new local override. Requires --value-int.")]
        public string AddOverride { get; set; }

        [Option("remove-override", HelpText = "Field name of a local override to remove.")]
        public string RemoveOverride { get; set; }

        [Option("edit", HelpText = "Field name of an existing local override to edit. Requires --value-int.")]
        public string Edit { get; set; }

        [Option("value-int", HelpText = "Int value (absolute delta) for --add-override / --edit.")]
        public int? ValueInt { get; set; }
    }

    /// <summary>
    /// CF-02 PARAM-level max-harness for the object-template write path — the typed-object-template
    /// analog of <see cref="RoundtripTabCommand"/>'s per-cell slice assertion. <c>roundtrip-iff</c>
    /// already covers chunk-level no-mutation byte-exactness for any IFF (object templates ARE IFF);
    /// this verb adds the param-LEVEL untouched-identity assertion after a typed
    /// override / revert / edit mutation.
    ///
    /// <para>Loads the OT, applies at most ONE mutation
    /// (<c>--add-override</c> | <c>--remove-override</c> | <c>--edit</c>), serializes via
    /// <see cref="ObjectTemplateWriter"/>, re-parses, and asserts every UNTOUCHED param chunk is
    /// byte-identical at the param level (the codec-encoded <c>name NUL · value region</c> payload).
    /// An unresolved-base fixture round-trips without throwing (the writer never resolves the base).</para>
    ///
    /// <para><b>Exit codes (mirroring <see cref="RoundtripIffCommand"/>):</b>
    ///   0 success; 1 UsageError; 2 IffParseException / DecoderException / IOException; 3 FileNotFound.
    ///   Generic <see cref="System.Exception"/> is intentionally NOT caught.</para>
    /// </summary>
    public static class RoundtripOtCommand
    {
        public static int Run(RoundtripOtOptions o)
        {
            bool hasAdd = !string.IsNullOrEmpty(o.AddOverride);
            bool hasRemove = !string.IsNullOrEmpty(o.RemoveOverride);
            bool hasEdit = !string.IsNullOrEmpty(o.Edit);

            int mutationCount = (hasAdd ? 1 : 0) + (hasRemove ? 1 : 0) + (hasEdit ? 1 : 0);
            if (mutationCount > 1)
            {
                return JsonOutput.EmitError("roundtrip-ot", "UsageError",
                    "--add-override, --remove-override, and --edit are mutually exclusive (supply at most one).", exitCode: 1);
            }
            if ((hasAdd || hasEdit) && !o.ValueInt.HasValue)
            {
                return JsonOutput.EmitError("roundtrip-ot", "UsageError",
                    (hasAdd ? "--add-override" : "--edit") + " requires --value-int.", exitCode: 1);
            }

            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("roundtrip-ot", "FileNotFound",
                    "Object-template .iff not found: " + o.Path, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(o.Path);
                MutableObjectTemplate model = ParseOt(loadedBytes);

                string mutationDescription = "none";
                string mutatedField = null;

                if (hasAdd)
                {
                    if (Find(model, o.AddOverride) != null)
                    {
                        return JsonOutput.EmitError("roundtrip-ot", "UsageError",
                            "--add-override '" + o.AddOverride + "' is already a local override; use --edit.", exitCode: 1);
                    }
                    model.AddOverride(o.AddOverride, ObjectTemplateParamValue.FromInt(o.ValueInt.Value, (byte)' '));
                    mutatedField = o.AddOverride;
                    mutationDescription = "add-override(" + o.AddOverride + ")";
                }
                else if (hasRemove)
                {
                    if (!model.RemoveOverride(o.RemoveOverride))
                    {
                        return JsonOutput.EmitError("roundtrip-ot", "UsageError",
                            "--remove-override '" + o.RemoveOverride + "' is not a local override.", exitCode: 1);
                    }
                    mutatedField = o.RemoveOverride;
                    mutationDescription = "remove-override(" + o.RemoveOverride + ")";
                }
                else if (hasEdit)
                {
                    if (Find(model, o.Edit) == null)
                    {
                        return JsonOutput.EmitError("roundtrip-ot", "UsageError",
                            "--edit '" + o.Edit + "' is not a local override.", exitCode: 1);
                    }
                    model.EditOverride(o.Edit, ObjectTemplateParamValue.FromInt(o.ValueInt.Value, (byte)' '));
                    mutatedField = o.Edit;
                    mutationDescription = "edit(" + o.Edit + ")";
                }

                byte[] roundtrippedBytes = model.Serialize();
                MutableObjectTemplate rtModel = ParseOt(roundtrippedBytes); // structural-validity re-parse

                bool anyMutation = mutationCount == 1;
                bool paramsEqualUntouched;
                JToken firstMismatch = JValue.CreateNull();

                if (!anyMutation)
                {
                    // No-mutation case: whole-file byte-exact identity (canonical fixtures).
                    paramsEqualUntouched = loadedBytes.Length == roundtrippedBytes.Length
                        && loadedBytes.SequenceEqual(roundtrippedBytes);
                }
                else
                {
                    paramsEqualUntouched = CompareUntouchedParams(
                        loadedBytes, roundtrippedBytes, mutatedField, hasRemove, out firstMismatch);
                }

                var result = new JObject
                {
                    ["baseTemplate"] = rtModel.BaseTemplateName ?? "(none)",
                    ["comparisonGranularity"] = anyMutation ? "per-param-payload" : "whole-file",
                    ["firstMismatch"] = firstMismatch,
                    ["localParamCount"] = rtModel.LocalParams.Count,
                    ["mutationApplied"] = mutationDescription,
                    ["paramsEqualUntouched"] = paramsEqualUntouched,
                    ["rootType"] = rtModel.RootType,
                    ["source"] = o.Path,
                    ["version"] = rtModel.Version
                };
                return JsonOutput.EmitSuccess("roundtrip-ot", result);
            }
            catch (UtinniCoreDotNet.Formats.Decoders.DecoderException ex)
            {
                return JsonOutput.EmitError("roundtrip-ot", "DecoderException", ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-ot", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("roundtrip-ot", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        // bytes -> IffReader.Read -> MutableIffDocument -> MutableObjectTemplate.
        private static MutableObjectTemplate ParseOt(byte[] bytes)
        {
            IffDocument iffDoc;
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                iffDoc = IffReader.Read(ms);
            }
            MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, bytes);
            return MutableObjectTemplate.FromMutableIff(mutableIff);
        }

        // Re-parses BOTH byte arrays into fresh models and pairs untouched params by field name. The
        // per-param slice is the codec-encoded chunk payload (name NUL · value region) — a fresh re-parse
        // round-trips each untouched param's verbatim on-disk bytes, so re-encoding reproduces them.
        // The mutated field (add: present only in rt; remove: present only in loaded; edit: differs) is
        // excluded; every other param must be byte-identical.
        private static bool CompareUntouchedParams(
            byte[] loadedBytes, byte[] roundtrippedBytes, string mutatedField, bool hasRemove, out JToken firstMismatch)
        {
            firstMismatch = JValue.CreateNull();

            MutableObjectTemplate loaded = ParseOt(loadedBytes);
            MutableObjectTemplate rt = ParseOt(roundtrippedBytes);

            Dictionary<string, byte[]> rtByName = rt.LocalParams.ToDictionary(
                p => p.FieldName, p => EncodeParamPayload(p), StringComparer.Ordinal);

            foreach (MutableObjectTemplateParam lp in loaded.LocalParams)
            {
                if (string.Equals(lp.FieldName, mutatedField, StringComparison.Ordinal))
                {
                    continue; // the touched field is expected to differ / be absent.
                }

                byte[] rtPayload;
                if (!rtByName.TryGetValue(lp.FieldName, out rtPayload))
                {
                    firstMismatch = new JObject { ["field"] = lp.FieldName, ["reason"] = "missing-in-roundtrip" };
                    return false;
                }

                byte[] loadedPayload = EncodeParamPayload(lp);
                if (!loadedPayload.SequenceEqual(rtPayload))
                {
                    firstMismatch = new JObject { ["field"] = lp.FieldName, ["reason"] = "payload-differs" };
                    return false;
                }
            }

            return true;
        }

        private static byte[] EncodeParamPayload(MutableObjectTemplateParam p)
        {
            return ObjectTemplateParamCodec.Encode(p.FieldName, p.Value);
        }

        private static MutableObjectTemplateParam Find(MutableObjectTemplate model, string name)
        {
            return model.LocalParams.FirstOrDefault(p => string.Equals(p.FieldName, name, StringComparison.Ordinal));
        }
    }
}
