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
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.ObjectTemplate;
using UtinniCoreDotNet.Saving;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("apply-save-ot", HelpText = "Apply ONE typed object-template override mutation to a loose-override .iff, verify untouched params byte-identical, then atomically commit the mutated bytes.")]
    public class ApplySaveOtOptions
    {
        [Value(0, MetaName = "relAsset", Required = true, HelpText = "Relative asset path under --root (loose-override destination).")]
        public string RelAsset { get; set; }

        [Option("root", Required = true, HelpText = "Client root for the loose-override destination; relAsset is resolved + contained under it.")]
        public string Root { get; set; }

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
    /// Plan 14-03a (MCP-02 / AUTH-05): the typed object-template member of the <c>apply-save-*</c>
    /// family — the OT analog of <see cref="ApplySaveTabCommand"/>. Resolves the loose-override
    /// destination under <c>--root</c>, applies EXACTLY ONE typed override mutation (REUSING the
    /// add / remove / edit semantics of <see cref="RoundtripOtCommand"/>), serializes via
    /// <see cref="MutableObjectTemplate.Serialize"/>, re-parses for validity, verifies byte-identity
    /// on the UNTOUCHED params, and — ONLY on a clean verify — atomically commits the SAME mutated
    /// bytes it verified. Fails closed (exit 2, no write) on a failed verify.
    ///
    /// <para>Exit codes: 0 ok; 1 usage; 2 verify-failed | parse | path-containment; 3 file-not-found.</para>
    /// </summary>
    public static class ApplySaveOtCommand
    {
        // Test-only seam (see ApplySaveTabCommand.TestPerturbSerialized).
        internal static Func<byte[], byte[]> TestPerturbSerialized;

        public static int Run(ApplySaveOtOptions o)
        {
            bool hasAdd = !string.IsNullOrEmpty(o.AddOverride);
            bool hasRemove = !string.IsNullOrEmpty(o.RemoveOverride);
            bool hasEdit = !string.IsNullOrEmpty(o.Edit);

            int mutationCount = (hasAdd ? 1 : 0) + (hasRemove ? 1 : 0) + (hasEdit ? 1 : 0);
            if (mutationCount > 1)
            {
                return JsonOutput.EmitError("apply-save-ot", "UsageError",
                    "--add-override, --remove-override, and --edit are mutually exclusive (supply exactly one).", exitCode: 1);
            }
            if (mutationCount == 0)
            {
                return JsonOutput.EmitError("apply-save-ot", "UsageError",
                    "apply-save-ot requires exactly one mutation: --add-override+--value-int, --edit+--value-int, or --remove-override.", exitCode: 1);
            }
            if ((hasAdd || hasEdit) && !o.ValueInt.HasValue)
            {
                return JsonOutput.EmitError("apply-save-ot", "UsageError",
                    (hasAdd ? "--add-override" : "--edit") + " requires --value-int.", exitCode: 1);
            }

            string destPath;
            try
            {
                destPath = LooseOverridePath.Resolve(o.Root, o.RelAsset);
            }
            catch (ArgumentException ex)
            {
                return JsonOutput.EmitError("apply-save-ot", "PathContainment", ex.Message, exitCode: 2);
            }

            if (!File.Exists(destPath))
            {
                return JsonOutput.EmitError("apply-save-ot", "FileNotFound",
                    "loose-override asset not found: " + destPath, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(destPath);

                if (IsTreMagic(loadedBytes))
                {
                    return JsonOutput.EmitError("apply-save-ot", "UsageError",
                        ".tre archives are not an apply-save target; use the repack-tre verb.", exitCode: 1);
                }

                MutableObjectTemplate model = ParseOt(loadedBytes);

                string mutationDescription;
                string mutatedField;
                if (hasAdd)
                {
                    if (Find(model, o.AddOverride) != null)
                    {
                        return JsonOutput.EmitError("apply-save-ot", "UsageError",
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
                        return JsonOutput.EmitError("apply-save-ot", "UsageError",
                            "--remove-override '" + o.RemoveOverride + "' is not a local override.", exitCode: 1);
                    }
                    mutatedField = o.RemoveOverride;
                    mutationDescription = "remove-override(" + o.RemoveOverride + ")";
                }
                else
                {
                    if (Find(model, o.Edit) == null)
                    {
                        return JsonOutput.EmitError("apply-save-ot", "UsageError",
                            "--edit '" + o.Edit + "' is not a local override.", exitCode: 1);
                    }
                    model.EditOverride(o.Edit, ObjectTemplateParamValue.FromInt(o.ValueInt.Value, (byte)' '));
                    mutatedField = o.Edit;
                    mutationDescription = "edit(" + o.Edit + ")";
                }

                byte[] mutatedBytes = model.Serialize();

                if (TestPerturbSerialized != null)
                {
                    mutatedBytes = TestPerturbSerialized(mutatedBytes);
                }

                MutableObjectTemplate rtModel;
                try
                {
                    rtModel = ParseOt(mutatedBytes);
                }
                catch (Exception ex) when (ex is DecoderException || ex is IffParseException)
                {
                    return JsonOutput.EmitError("apply-save-ot", "VerifyFailed",
                        "serialized bytes failed to re-parse; nothing written: " + ex.Message, exitCode: 2);
                }

                JToken firstMismatch;
                bool bytesEqualUntouched = CompareUntouchedParams(
                    loadedBytes, mutatedBytes, mutatedField, hasRemove, out firstMismatch);

                if (!bytesEqualUntouched)
                {
                    return JsonOutput.EmitError("apply-save-ot", "VerifyFailed",
                        "untouched-region byte-identity check failed; nothing written.", exitCode: 2);
                }

                SaveCommandIo.WriteAtomic(destPath, mutatedBytes);

                var result = new JObject
                {
                    ["backupPath"] = JValue.CreateNull(),
                    ["bytesEqualUntouched"] = true,
                    ["bytesWritten"] = mutatedBytes.Length,
                    ["comparisonGranularity"] = "per-param-payload",
                    ["firstMismatch"] = JValue.CreateNull(),
                    ["localParamCount"] = rtModel.LocalParams.Count,
                    ["mutationApplied"] = mutationDescription,
                    ["path"] = destPath,
                    ["validated"] = true,
                    ["written"] = true
                };
                return JsonOutput.EmitSuccess("apply-save-ot", result);
            }
            catch (DecoderException ex)
            {
                return JsonOutput.EmitError("apply-save-ot", "DecoderException", ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("apply-save-ot", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("apply-save-ot", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        private static bool IsTreMagic(byte[] b)
        {
            return b.Length >= 4 && b[0] == (byte)'E' && b[1] == (byte)'E' && b[2] == (byte)'R' && b[3] == (byte)'T';
        }

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

        // Re-parses BOTH byte arrays into fresh models and pairs untouched params by field name
        // (REUSES the RoundtripOtCommand per-param-payload algorithm).
        private static bool CompareUntouchedParams(
            byte[] loadedBytes, byte[] mutatedBytes, string mutatedField, bool hasRemove, out JToken firstMismatch)
        {
            firstMismatch = JValue.CreateNull();

            MutableObjectTemplate loaded = ParseOt(loadedBytes);
            MutableObjectTemplate rt = ParseOt(mutatedBytes);

            Dictionary<string, byte[]> rtByName = rt.LocalParams.ToDictionary(
                p => p.FieldName, p => EncodeParamPayload(p), StringComparer.Ordinal);

            foreach (MutableObjectTemplateParam lp in loaded.LocalParams)
            {
                if (string.Equals(lp.FieldName, mutatedField, StringComparison.Ordinal)) continue;

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
