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
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Saving;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("apply-save-iff", HelpText = "Apply ONE generic IFF leaf mutation to a loose-override .iff, verify untouched leaves byte-identical, then atomically commit the mutated bytes.")]
    public class ApplySaveIffOptions
    {
        [Value(0, MetaName = "relAsset", Required = true, HelpText = "Relative asset path under --root (loose-override destination).")]
        public string RelAsset { get; set; }

        [Option("root", Required = true, HelpText = "Client root for the loose-override destination; relAsset is resolved + contained under it.")]
        public string Root { get; set; }

        [Option("mutate-leaf", HelpText = "Stable id of a leaf chunk to mutate. Requires --mutate-hex. Mutually exclusive with --remove-leaf.")]
        public string MutateLeafId { get; set; }

        [Option("mutate-hex", HelpText = "Hex string (no separators) of replacement payload bytes for --mutate-leaf.")]
        public string MutateHex { get; set; }

        [Option("remove-leaf", HelpText = "Stable id of a leaf chunk to remove. Mutually exclusive with --mutate-leaf.")]
        public string RemoveLeafId { get; set; }
    }

    /// <summary>
    /// Plan 14-03a Task 2 (MCP-02 / AUTH-05): the generic-IFF member of the <c>apply-save-*</c> family.
    /// Applies EXACTLY ONE leaf mutation (<c>--mutate-leaf</c>+<c>--mutate-hex</c> | <c>--remove-leaf</c>)
    /// to a loose-override IFF, serializes via <see cref="IffWriter"/>, re-parses for validity, verifies
    /// byte-identity on the UNTOUCHED leaves, and — ONLY on a clean verify — atomically commits the SAME
    /// mutated bytes. Fails closed (exit 2, no write) on a failed verify.
    ///
    /// <para><b>Naming (Cursor divergent concern, 14-REVIEWS):</b> this verb handles plain / generic IFF
    /// leaf edits. The TYPED DTII datatable semantics live in <see cref="ApplySaveTabCommand"/>
    /// (apply-save-tab). The 14-03 MCP tool names map accordingly — generic IFF edits route here, typed
    /// datatable edits route to apply-save-tab.</para>
    ///
    /// <para>Exit codes: 0 ok; 1 usage; 2 verify-failed | parse | path-containment; 3 file-not-found.</para>
    /// </summary>
    public static class ApplySaveIffCommand
    {
        // Test-only seam (see ApplySaveTabCommand.TestPerturbSerialized).
        internal static Func<byte[], byte[]> TestPerturbSerialized;

        public static int Run(ApplySaveIffOptions o)
        {
            bool hasMutate = !string.IsNullOrEmpty(o.MutateLeafId);
            bool hasRemove = !string.IsNullOrEmpty(o.RemoveLeafId);

            if (hasMutate && hasRemove)
            {
                return JsonOutput.EmitError("apply-save-iff", "UsageError",
                    "--mutate-leaf and --remove-leaf are mutually exclusive.", exitCode: 1);
            }
            if (!hasMutate && !hasRemove)
            {
                return JsonOutput.EmitError("apply-save-iff", "UsageError",
                    "apply-save-iff requires exactly one mutation: --mutate-leaf+--mutate-hex or --remove-leaf.", exitCode: 1);
            }
            if (hasMutate && string.IsNullOrEmpty(o.MutateHex))
            {
                return JsonOutput.EmitError("apply-save-iff", "UsageError",
                    "--mutate-leaf requires --mutate-hex.", exitCode: 1);
            }

            string destPath;
            try
            {
                destPath = LooseOverridePath.Resolve(o.Root, o.RelAsset);
            }
            catch (ArgumentException ex)
            {
                return JsonOutput.EmitError("apply-save-iff", "PathContainment", ex.Message, exitCode: 2);
            }

            if (!File.Exists(destPath))
            {
                return JsonOutput.EmitError("apply-save-iff", "FileNotFound",
                    "loose-override asset not found: " + destPath, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(destPath);

                if (IsTreMagic(loadedBytes))
                {
                    return JsonOutput.EmitError("apply-save-iff", "UsageError",
                        ".tre archives are not an apply-save target; use the repack-tre verb.", exitCode: 1);
                }

                IffDocument originalDoc;
                using (var ms = new MemoryStream(loadedBytes, writable: false))
                {
                    originalDoc = IffReader.Read(ms);
                }
                MutableIffDocument mutable = MutableIffDocument.FromDocument(originalDoc, loadedBytes);

                string mutationDescription;
                if (hasMutate)
                {
                    IffLeafChunk targetLeaf = FindLeafByStableId(originalDoc, o.MutateLeafId);
                    if (targetLeaf == null)
                    {
                        return JsonOutput.EmitError("apply-save-iff", "UsageError",
                            "--mutate-leaf id not found in document: " + o.MutateLeafId, exitCode: 1);
                    }
                    byte[] newPayload;
                    try { newPayload = ParseHex(o.MutateHex); }
                    catch (ArgumentException ex)
                    {
                        return JsonOutput.EmitError("apply-save-iff", "UsageError",
                            "--mutate-hex is not a valid hex string: " + ex.Message, exitCode: 1);
                    }
                    MutableIffNode mutableLeaf = FindMutableLeafByStableId(mutable, o.MutateLeafId);
                    if (mutableLeaf == null)
                    {
                        return JsonOutput.EmitError("apply-save-iff", "UsageError",
                            "--mutate-leaf id not addressable in mutable DOM: " + o.MutateLeafId, exitCode: 1);
                    }
                    mutableLeaf.SetPayload(newPayload);
                    mutationDescription = "mutate-leaf(" + o.MutateLeafId + ")";
                }
                else
                {
                    IffLeafChunk targetLeaf = FindLeafByStableId(originalDoc, o.RemoveLeafId);
                    if (targetLeaf == null)
                    {
                        return JsonOutput.EmitError("apply-save-iff", "UsageError",
                            "--remove-leaf id not found in document: " + o.RemoveLeafId, exitCode: 1);
                    }
                    if (!mutable.RemoveByStableId(o.RemoveLeafId))
                    {
                        return JsonOutput.EmitError("apply-save-iff", "UsageError",
                            "--remove-leaf id was not removable in mutable DOM: " + o.RemoveLeafId, exitCode: 1);
                    }
                    mutationDescription = "remove-leaf(" + o.RemoveLeafId + ")";
                }

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
                    return JsonOutput.EmitError("apply-save-iff", "VerifyFailed",
                        "serialized bytes failed to re-parse; nothing written: " + ex.Message, exitCode: 2);
                }

                bool bytesEqualUntouched = CompareUntouchedLeaves(originalDoc, rewrittenDoc, hasMutate, hasRemove, o);

                if (!bytesEqualUntouched)
                {
                    return JsonOutput.EmitError("apply-save-iff", "VerifyFailed",
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
                return JsonOutput.EmitSuccess("apply-save-iff", result);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("apply-save-iff", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("apply-save-iff", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        private static bool IsTreMagic(byte[] b)
        {
            return b.Length >= 4 && b[0] == (byte)'E' && b[1] == (byte)'E' && b[2] == (byte)'R' && b[3] == (byte)'T';
        }

        // Compares every UNTOUCHED leaf (located by stable id) between the original and rewritten docs.
        // For --mutate-leaf the ids are stable (pure payload edit); for --remove-leaf the post-removal
        // siblings shift ordinal, so we conservatively verify by matching every original untouched leaf's
        // (TypeId, Data) into the rewritten leaf SET (multiset by content) rather than by id.
        private static bool CompareUntouchedLeaves(
            IffDocument originalDoc, IffDocument rewrittenDoc, bool hasMutate, bool hasRemove, ApplySaveIffOptions o)
        {
            if (hasMutate)
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
                    if (origLeaf.Id == o.MutateLeafId) continue;
                    untouched++;
                    if (!rewrittenById.TryGetValue(origLeaf.Id, out IffLeafChunk rw)) return false;
                    if (rw.TypeId != origLeaf.TypeId || !rw.Data.SequenceEqual(origLeaf.Data)) return false;
                    matched++;
                }
                return matched == untouched;
            }

            // --remove-leaf: content-multiset comparison of all untouched leaves (id ordinals shift).
            var remaining = new List<IffLeafChunk>();
            foreach (var node in rewrittenDoc.AllNodesInPreorder)
            {
                if (node is IffLeafChunk leaf) remaining.Add(leaf);
            }
            foreach (var node in originalDoc.AllNodesInPreorder)
            {
                if (!(node is IffLeafChunk origLeaf)) continue;
                if (origLeaf.Id == o.RemoveLeafId) continue; // the removed leaf has no counterpart
                int idx = remaining.FindIndex(
                    rw => rw.TypeId == origLeaf.TypeId && rw.Data.SequenceEqual(origLeaf.Data));
                if (idx < 0) return false;
                remaining.RemoveAt(idx); // consume one match (multiset)
            }
            return true;
        }

        // ─── lookup + hex helpers (REUSE the RoundtripIffCommand algorithm) ───

        private static IffLeafChunk FindLeafByStableId(IffDocument doc, string stableId)
        {
            foreach (var node in doc.AllNodesInPreorder)
            {
                if (node is IffLeafChunk leaf && leaf.Id == stableId) return leaf;
            }
            return null;
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

        private static byte[] ParseHex(string hex)
        {
            if (hex == null) throw new ArgumentException("null", nameof(hex));
            if (hex.Length % 2 != 0)
                throw new ArgumentException("hex string must have an even number of characters; got length " + hex.Length);
            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                int hi = HexNibble(hex[i * 2]);
                int lo = HexNibble(hex[i * 2 + 1]);
                if (hi < 0 || lo < 0)
                    throw new ArgumentException("non-hex character at position " + (i * 2));
                result[i] = (byte)((hi << 4) | lo);
            }
            return result;
        }

        private static int HexNibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return 10 + (c - 'a');
            if (c >= 'A' && c <= 'F') return 10 + (c - 'A');
            return -1;
        }
    }
}
