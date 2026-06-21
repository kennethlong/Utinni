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
using System.Globalization;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Template;
using UtinniCoreDotNet.Saving;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("apply-save-template", HelpText = "Apply ONE template-field edit to a template-eligible IFF leaf in a loose-override .iff, verify untouched leaves byte-identical, then atomically commit the re-encoded bytes.")]
    public class ApplySaveTemplateOptions
    {
        [Value(0, MetaName = "relAsset", Required = true, HelpText = "Relative asset path under --root (loose-override destination).")]
        public string RelAsset { get; set; }

        [Option("root", Required = true, HelpText = "Client root for the loose-override destination; relAsset is resolved + contained under it.")]
        public string Root { get; set; }

        [Option("mutate-leaf", Required = true, HelpText = "Stable id of the template-eligible leaf chunk to edit.")]
        public string MutateLeafId { get; set; }

        [Option("template", HelpText = "Path to a single template .json to apply (skips pack auto-resolution).")]
        public string TemplatePath { get; set; }

        [Option("templates-dir", HelpText = "Optional project-local pack directory added to the default scanned allow-list for auto-resolution.")]
        public string TemplatesDir { get; set; }

        [Option("set", HelpText = "Zero or more scalar field edits as name=value (e.g. --set weight=2.5). Unset fields keep their decoded value.")]
        public IEnumerable<string> Sets { get; set; }
    }

    /// <summary>
    /// The verbs-first template-edit save (D-15 / MCP write parity, PROD-IFFT-02): the
    /// <c>apply-save-template</c> member of the <c>apply-save-*</c> family. Clones
    /// <see cref="ApplySaveIffCommand"/>'s contained/atomic/fail-closed backbone EXACTLY — the ONLY
    /// difference is HOW the new leaf payload is produced: the leaf is decoded through its resolved
    /// template, the requested scalar field edits are applied, and the payload is re-encoded via
    /// <see cref="KernelCodec.Encode"/> (replacing apply-save-iff's <c>--mutate-hex</c> literal).
    ///
    /// <para>Reuses the proven security backbone verbatim: <c>--root</c> containment via
    /// <see cref="LooseOverridePath.Resolve"/> (exit 2 on a PathContainment escape), File.Exists -&gt; exit 3,
    /// <see cref="MutableIffDocument.FromDocument"/>, the mutable-leaf SetPayload, <see cref="IffWriter.Write"/>
    /// (leaf-len + parent-FORM ripple FREE), re-parse-for-validity, untouched-leaf byte-identity verify
    /// (fail-closed exit 2, NO write), and <see cref="SaveCommandIo.WriteAtomic"/> on a clean verify only.
    /// The internal <see cref="TestPerturbSerialized"/> seam (the SAME shape ApplySaveIffCommand exposes)
    /// lets the fail-closed golden corrupt an untouched leaf in the serialized output.</para>
    ///
    /// <para>Exit codes: 0 ok; 1 usage; 2 verify-failed | parse | path-containment | template/decode/encode;
    /// 3 file-not-found.</para>
    /// </summary>
    public static class ApplySaveTemplateCommand
    {
        // Test-only seam (mirrors ApplySaveIffCommand.TestPerturbSerialized): corrupts the serialized bytes
        // after the mutation so the untouched-leaf verify must fail-closed (exit 2, NOTHING written).
        internal static Func<byte[], byte[]> TestPerturbSerialized;

        public static int Run(ApplySaveTemplateOptions o)
        {
            if (string.IsNullOrEmpty(o.MutateLeafId))
            {
                return JsonOutput.EmitError("apply-save-template", "UsageError",
                    "apply-save-template requires --mutate-leaf <stableId>.", exitCode: 1);
            }
            if (!string.IsNullOrEmpty(o.TemplatePath) && !File.Exists(o.TemplatePath))
            {
                return JsonOutput.EmitError("apply-save-template", "FileNotFound",
                    "template .json not found: " + o.TemplatePath, exitCode: 3);
            }

            string destPath;
            try
            {
                destPath = LooseOverridePath.Resolve(o.Root, o.RelAsset);
            }
            catch (ArgumentException ex)
            {
                return JsonOutput.EmitError("apply-save-template", "PathContainment", ex.Message, exitCode: 2);
            }

            if (!File.Exists(destPath))
            {
                return JsonOutput.EmitError("apply-save-template", "FileNotFound",
                    "loose-override asset not found: " + destPath, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(destPath);

                IffDocument originalDoc;
                using (var ms = new MemoryStream(loadedBytes, writable: false))
                {
                    originalDoc = IffReader.Read(ms);
                }
                MutableIffDocument mutable = MutableIffDocument.FromDocument(originalDoc, loadedBytes);

                // ── the ONE difference from apply-save-iff: newPayload comes from KernelCodec.Encode ──
                List<TemplateModel> pool = TemplateDecodeShared.LoadTemplatePool(o.TemplatePath, o.TemplatesDir);
                var resolver = new TemplateResolver(pool);
                TemplateResolution resolution = resolver.Resolve(mutable, o.MutateLeafId);
                if (resolution.SuppressedByBuiltin)
                {
                    return JsonOutput.EmitError("apply-save-template", "NoMatch",
                        "the root FORM has a built-in decoder (D-05 altitude); a template never engages here", exitCode: 2);
                }
                if (resolution.Best == null)
                {
                    return JsonOutput.EmitError("apply-save-template", "NoMatch",
                        "no template in the pool matched the leaf " + o.MutateLeafId, exitCode: 2);
                }

                TemplateModel template = resolution.Best.Template;
                byte[] originalPayload = TemplateDecodeShared.FindLeafPayload(mutable, o.MutateLeafId);
                DecodedTemplate decoded = KernelCodec.Decode(template, originalPayload);
                ApplyScalarEdits(decoded, o.Sets);
                byte[] newPayload = KernelCodec.Encode(decoded);

                MutableIffNode mutableLeaf = FindMutableLeafByStableId(mutable, o.MutateLeafId);
                if (mutableLeaf == null)
                {
                    return JsonOutput.EmitError("apply-save-template", "UsageError",
                        "--mutate-leaf id not addressable in mutable DOM: " + o.MutateLeafId, exitCode: 1);
                }
                mutableLeaf.SetPayload(newPayload);
                string mutationDescription = "template-edit(" + o.MutateLeafId + ")";

                byte[] mutatedBytes = IffWriter.Write(mutable);

                if (TestPerturbSerialized != null)
                {
                    mutatedBytes = TestPerturbSerialized(mutatedBytes);
                }

                IffDocument rewrittenDoc;
                try
                {
                    using (var ms = new MemoryStream(mutatedBytes, writable: false))
                    {
                        rewrittenDoc = IffReader.Read(ms);
                    }
                }
                catch (IffParseException ex)
                {
                    return JsonOutput.EmitError("apply-save-template", "VerifyFailed",
                        "serialized bytes failed to re-parse; nothing written: " + ex.Message, exitCode: 2);
                }

                bool bytesEqualUntouched = CompareUntouchedLeaves(originalDoc, rewrittenDoc, o.MutateLeafId);

                if (!bytesEqualUntouched)
                {
                    return JsonOutput.EmitError("apply-save-template", "VerifyFailed",
                        "untouched-region byte-identity check failed; nothing written.", exitCode: 2);
                }

                SaveCommandIo.WriteAtomic(destPath, mutatedBytes);

                var result = new JObject
                {
                    ["backupPath"] = JValue.CreateNull(),
                    ["bytesEqualUntouched"] = true,
                    ["bytesWritten"] = mutatedBytes.Length,
                    ["comparisonGranularity"] = "per-leaf",
                    ["firstMismatch"] = JValue.CreateNull(),
                    ["mutationApplied"] = mutationDescription,
                    ["path"] = destPath,
                    ["validated"] = true,
                    ["written"] = true
                };
                return JsonOutput.EmitSuccess("apply-save-template", result);
            }
            catch (TemplateException ex)
            {
                return JsonOutput.EmitError("apply-save-template", "TemplateError", ex.Message, exitCode: 2);
            }
            catch (UtinniCoreDotNet.Formats.Decoders.DecoderException ex)
            {
                return JsonOutput.EmitError("apply-save-template", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("apply-save-template", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("apply-save-template", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        // Applies name=value scalar edits onto the decoded value carrier. An edit naming a non-scalar field
        // (struct/array) or a field absent from the template is a usage error surfaced as a TemplateException
        // (exit 2). The decoded value's CLR type drives the parse (long/float/double/string).
        private static void ApplyScalarEdits(DecodedTemplate decoded, IEnumerable<string> sets)
        {
            if (sets == null) return;
            foreach (string set in sets)
            {
                if (string.IsNullOrEmpty(set)) continue;
                int eq = set.IndexOf('=');
                if (eq <= 0)
                {
                    throw new TemplateException("--set must be name=value, got '" + set + "'.");
                }
                string name = set.Substring(0, eq);
                string raw = set.Substring(eq + 1);

                if (!decoded.Values.TryGetValue(name, out object current))
                {
                    throw new TemplateException("--set names a field not decoded by the template: '" + name + "'.");
                }
                decoded.Values[name] = CoerceTo(current, raw, name);
            }
        }

        private static object CoerceTo(object current, string raw, string name)
        {
            if (current is long) return long.Parse(raw, CultureInfo.InvariantCulture);
            if (current is uint) return uint.Parse(raw, CultureInfo.InvariantCulture);
            if (current is int) return (long)int.Parse(raw, CultureInfo.InvariantCulture);
            if (current is float) return float.Parse(raw, CultureInfo.InvariantCulture);
            if (current is double) return double.Parse(raw, CultureInfo.InvariantCulture);
            if (current is string) return raw;
            throw new TemplateException("--set cannot edit non-scalar field '" + name + "' ("
                + (current == null ? "null" : current.GetType().Name) + ").");
        }

        // Compares every UNTOUCHED leaf (by stable id) between the original and rewritten docs (the edit is a
        // pure payload edit, so ids are stable). Identical algorithm to ApplySaveIffCommand's mutate branch.
        private static bool CompareUntouchedLeaves(IffDocument originalDoc, IffDocument rewrittenDoc, string editedLeafId)
        {
            var rewrittenById = new Dictionary<string, IffLeafChunk>(StringComparer.Ordinal);
            foreach (var node in rewrittenDoc.AllNodesInPreorder)
            {
                if (node is IffLeafChunk leaf) rewrittenById[leaf.Id] = leaf;
            }
            int matched = 0, untouched = 0;
            foreach (var node in originalDoc.AllNodesInPreorder)
            {
                if (!(node is IffLeafChunk origLeaf)) continue;
                if (origLeaf.Id == editedLeafId) continue;
                untouched++;
                if (!rewrittenById.TryGetValue(origLeaf.Id, out IffLeafChunk rw)) return false;
                if (rw.TypeId != origLeaf.TypeId || !rw.Data.SequenceEqual(origLeaf.Data)) return false;
                matched++;
            }
            return matched == untouched;
        }

        private static MutableIffNode FindMutableLeafByStableId(MutableIffDocument doc, string stableId)
        {
            return FindMutableLeafRecursive(doc.Root, "", 0, stableId);
        }

        private static MutableIffNode FindMutableLeafRecursive(MutableIffNode node, string parentPrefix, int ordinal, string targetId)
        {
            string thisId = MutableIffDocument.DeriveStableId(node, parentPrefix, ordinal);
            if (node.Kind == MutableIffNodeKind.Leaf && thisId == targetId) return node;
            if (node.Kind == MutableIffNodeKind.Container)
            {
                string childPrefix = thisId + "/";
                for (int i = 0; i < node.Children.Count; i++)
                {
                    var found = FindMutableLeafRecursive(node.Children[i], childPrefix, i, targetId);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
