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
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("roundtrip-iff", HelpText = "Parse -> [optional mutate-leaf | remove-leaf] -> serialize -> re-parse an IFF; assert byte-exact untouched chunks.")]
    public class RoundtripIffOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .iff file.")]
        public string Path { get; set; }

        [Option("mutate-leaf", HelpText = "Stable id of a leaf chunk to mutate. Requires --mutate-hex. Mutually exclusive with --remove-leaf.")]
        public string MutateLeafId { get; set; }

        [Option("mutate-hex", HelpText = "Hex string (no separators) of replacement payload bytes for --mutate-leaf.")]
        public string MutateHex { get; set; }

        [Option("remove-leaf", HelpText = "Stable id of a leaf chunk to remove. Mutually exclusive with --mutate-leaf.")]
        public string RemoveLeafId { get; set; }
    }

    /// <summary>
    /// D-02 max-harness for the IFF write path: parses an IFF, builds the mutable hybrid DOM,
    /// optionally mutates or removes one leaf, serializes back via the shared framework write
    /// path, and reports per-byte-range identity in a JSON envelope.
    ///
    /// <para><b>Three modes:</b></para>
    /// <list type="bullet">
    ///   <item><b>No mutation</b> — straight parse -> FromDocument -> Write -> SequenceEqual.
    ///         Reports <c>byteExact</c>.</item>
    ///   <item><b><c>--mutate-leaf &lt;id&gt; --mutate-hex &lt;hex&gt;</c></b> — finds the leaf by
    ///         stable id, replaces its payload, serializes, re-parses, and asserts every untouched
    ///         leaf in the re-parsed tree has identical bytes to the corresponding original leaf.
    ///         Reports <c>mutatedLeafId, untouchedLeafRangesIdentical, untouchedLeafCount</c>.</item>
    ///   <item><b><c>--remove-leaf &lt;id&gt;</c></b> (round-2 MEDIUM 8 / cursor N-M4) — removes
    ///         the leaf via <see cref="MutableIffDocument.RemoveByStableId"/>, serializes, re-parses,
    ///         and asserts every UNTOUCHED leaf in the re-parsed tree (located via stable id
    ///         lookup) has identical TypeID + Data to the original. Reports <c>removedLeafId,
    ///         removedLeafTypeId, byteExactExceptRemovedLeaf,
    ///         parentContainerChildCountBefore, parentContainerChildCountAfter, untouchedLeafCount</c>.</item>
    /// </list>
    ///
    /// <para><b>Exit codes (mirroring <see cref="InspectIffCommand"/>):</b>
    ///   FileNotFound -> 3; IffParseException/IOException -> 2; usage error -> 1.
    ///   Generic <see cref="System.Exception"/> is intentionally NOT caught.</para>
    /// </summary>
    public static class RoundtripIffCommand
    {
        public static int Run(RoundtripIffOptions o)
        {
            // Usage validation: --mutate-leaf and --remove-leaf are mutually exclusive.
            bool hasMutate = !string.IsNullOrEmpty(o.MutateLeafId);
            bool hasRemove = !string.IsNullOrEmpty(o.RemoveLeafId);
            if (hasMutate && hasRemove)
            {
                return JsonOutput.EmitError("roundtrip-iff", "UsageError",
                    "--mutate-leaf and --remove-leaf are mutually exclusive.", exitCode: 1);
            }
            if (hasMutate && string.IsNullOrEmpty(o.MutateHex))
            {
                return JsonOutput.EmitError("roundtrip-iff", "UsageError",
                    "--mutate-leaf requires --mutate-hex.", exitCode: 1);
            }

            // FileNotFound check: exit 3.
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("roundtrip-iff", "FileNotFound",
                    "IFF file not found: " + o.Path, exitCode: 3);
            }

            try
            {
                byte[] original = File.ReadAllBytes(o.Path);
                IffDocument originalDoc;
                using (var ms = new MemoryStream(original, writable: false))
                {
                    originalDoc = IffReader.Read(ms);
                }

                MutableIffDocument mutable = MutableIffDocument.FromDocument(originalDoc, original);

                if (hasMutate)
                {
                    return RunMutate(o, original, originalDoc, mutable);
                }
                if (hasRemove)
                {
                    return RunRemove(o, original, originalDoc, mutable);
                }

                // No-mutation case: straight round-trip.
                byte[] rewritten = IffWriter.Write(mutable);
                var result = BuildNoMutationResult(original, rewritten, o.Path);
                return JsonOutput.EmitSuccess("roundtrip-iff", result);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-iff", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("roundtrip-iff", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        // ─────────────────────────────────────────────────────────────────────
        // Mode (a): no mutation — byteExact identity over the whole file.
        // ─────────────────────────────────────────────────────────────────────

        private static JObject BuildNoMutationResult(byte[] original, byte[] rewritten, string sourcePath)
        {
            return new JObject
            {
                ["byteExact"] = original.Length == rewritten.Length && original.SequenceEqual(rewritten),
                ["originalLength"] = original.Length,
                ["rewrittenLength"] = rewritten.Length,
                ["source"] = sourcePath
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Mode (b): mutate one leaf's payload; verify untouched-leaf identity.
        // ─────────────────────────────────────────────────────────────────────

        private static int RunMutate(RoundtripIffOptions o, byte[] original, IffDocument originalDoc, MutableIffDocument mutable)
        {
            // Locate the target leaf in the ORIGINAL document.
            IffLeafChunk targetLeaf = FindLeafByStableId(originalDoc, o.MutateLeafId);
            if (targetLeaf == null)
            {
                return JsonOutput.EmitError("roundtrip-iff", "UsageError",
                    "--mutate-leaf id not found in document: " + o.MutateLeafId, exitCode: 1);
            }

            // Parse the hex replacement payload.
            byte[] newPayload;
            try
            {
                newPayload = ParseHex(o.MutateHex);
            }
            catch (ArgumentException ex)
            {
                return JsonOutput.EmitError("roundtrip-iff", "UsageError",
                    "--mutate-hex is not a valid hex string: " + ex.Message, exitCode: 1);
            }

            // Locate the same leaf in the MUTABLE document and replace its payload.
            MutableIffNode mutableLeaf = FindMutableLeafByStableId(mutable, o.MutateLeafId);
            if (mutableLeaf == null)
            {
                // Should not happen if FindLeafByStableId succeeded — defensive.
                return JsonOutput.EmitError("roundtrip-iff", "UsageError",
                    "--mutate-leaf id not addressable in mutable DOM: " + o.MutateLeafId, exitCode: 1);
            }
            mutableLeaf.SetPayload(newPayload);

            byte[] rewritten = IffWriter.Write(mutable);

            // Re-parse rewritten bytes to a fresh IffDocument; we identify untouched leaves by
            // stable id (sibling ordinals are unchanged on a pure payload edit, so ids are stable).
            IffDocument rewrittenDoc;
            using (var ms = new MemoryStream(rewritten, writable: false))
            {
                rewrittenDoc = IffReader.Read(ms);
            }

            // Build a map of stable id -> Data bytes for the original's leaves (minus the mutated one).
            var originalLeavesById = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            int untouchedLeafCount = 0;
            foreach (var node in originalDoc.AllNodesInPreorder)
            {
                if (node is IffLeafChunk leaf && leaf.Id != o.MutateLeafId)
                {
                    originalLeavesById[leaf.Id] = leaf.Data;
                    untouchedLeafCount++;
                }
            }

            // Compare each untouched leaf's Data in the rewritten doc to the original.
            bool untouchedLeafRangesIdentical = true;
            foreach (var node in rewrittenDoc.AllNodesInPreorder)
            {
                if (node is IffLeafChunk leaf && originalLeavesById.TryGetValue(leaf.Id, out byte[] origData))
                {
                    if (!leaf.Data.SequenceEqual(origData))
                    {
                        untouchedLeafRangesIdentical = false;
                        break;
                    }
                }
            }
            // Also verify count of untouched leaves matches in the rewritten doc.
            int rewrittenMatchedCount = 0;
            foreach (var node in rewrittenDoc.AllNodesInPreorder)
            {
                if (node is IffLeafChunk leaf && originalLeavesById.ContainsKey(leaf.Id))
                {
                    rewrittenMatchedCount++;
                }
            }
            if (rewrittenMatchedCount != untouchedLeafCount)
            {
                untouchedLeafRangesIdentical = false;
            }

            bool byteExact = original.Length == rewritten.Length && original.SequenceEqual(rewritten);

            var result = new JObject
            {
                ["byteExact"] = byteExact,
                ["mutatedLeafId"] = o.MutateLeafId,
                ["originalLength"] = original.Length,
                ["rewrittenLength"] = rewritten.Length,
                ["source"] = o.Path,
                ["untouchedLeafCount"] = untouchedLeafCount,
                ["untouchedLeafRangesIdentical"] = untouchedLeafRangesIdentical
            };
            return JsonOutput.EmitSuccess("roundtrip-iff", result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Mode (c): remove one leaf; verify untouched-leaf identity + structural reporting.
        // ─────────────────────────────────────────────────────────────────────

        private static int RunRemove(RoundtripIffOptions o, byte[] original, IffDocument originalDoc, MutableIffDocument mutable)
        {
            // Locate the target leaf + its parent container in the ORIGINAL document.
            IffLeafChunk targetLeaf = FindLeafByStableId(originalDoc, o.RemoveLeafId);
            if (targetLeaf == null)
            {
                return JsonOutput.EmitError("roundtrip-iff", "UsageError",
                    "--remove-leaf id not found in document: " + o.RemoveLeafId, exitCode: 1);
            }
            IffContainerChunk parent = FindParentOf(originalDoc, targetLeaf.Id);
            if (parent == null)
            {
                return JsonOutput.EmitError("roundtrip-iff", "UsageError",
                    "--remove-leaf cannot remove the root.", exitCode: 1);
            }
            string removedLeafTypeId = targetLeaf.TypeId;
            int parentBefore = parent.Children.Count;

            // Capture the original siblings AFTER the removed leaf — these will have shifted
            // stable IDs in the rewritten document (ordinals decrease by 1).
            int removedOrdinal = -1;
            for (int i = 0; i < parent.Children.Count; i++)
            {
                if (parent.Children[i].Id == targetLeaf.Id) { removedOrdinal = i; break; }
            }

            // Perform the removal on the mutable document.
            bool removed = mutable.RemoveByStableId(o.RemoveLeafId);
            if (!removed)
            {
                return JsonOutput.EmitError("roundtrip-iff", "UsageError",
                    "--remove-leaf id was not removable in mutable DOM: " + o.RemoveLeafId, exitCode: 1);
            }

            byte[] rewritten = IffWriter.Write(mutable);

            // Re-parse rewritten bytes.
            IffDocument rewrittenDoc;
            using (var ms = new MemoryStream(rewritten, writable: false))
            {
                rewrittenDoc = IffReader.Read(ms);
            }

            // Locate the parent container in the rewritten doc by its stable id (its id is unchanged).
            IffContainerChunk rewrittenParent = FindContainerByStableId(rewrittenDoc, parent.Id);
            int parentAfter = rewrittenParent != null ? rewrittenParent.Children.Count : -1;

            // For each original untouched leaf, find its corresponding leaf in the rewritten doc.
            // - Leaves NOT under the affected parent: stable id unchanged.
            // - Leaves under the affected parent BEFORE the removed ordinal: stable id unchanged.
            // - Leaves under the affected parent AFTER the removed ordinal: stable id ordinal -1.
            string parentIdPrefix = parent.Id + "/";
            var rewrittenLeavesById = BuildLeafIndex(rewrittenDoc);

            int untouchedLeafCount = 0;
            bool byteExactExceptRemovedLeaf = true;
            foreach (var node in originalDoc.AllNodesInPreorder)
            {
                if (!(node is IffLeafChunk origLeaf)) continue;
                if (origLeaf.Id == o.RemoveLeafId) continue;
                untouchedLeafCount++;

                string expectedRewrittenId = origLeaf.Id;
                if (origLeaf.Id.StartsWith(parentIdPrefix, StringComparison.Ordinal))
                {
                    // This leaf is a descendant of the affected parent. Check if it is a direct
                    // child whose ordinal is greater than the removed ordinal — those have shifted.
                    string relative = origLeaf.Id.Substring(parentIdPrefix.Length);
                    int slashIdx = relative.IndexOf('/');
                    string firstSegment = slashIdx < 0 ? relative : relative.Substring(0, slashIdx);
                    // firstSegment looks like "INFO:INFO/0" — extract the trailing ordinal.
                    int ordSepIdx = firstSegment.LastIndexOf('/');
                    if (ordSepIdx >= 0 && int.TryParse(firstSegment.Substring(ordSepIdx + 1),
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int firstSegOrd))
                    {
                        if (firstSegOrd > removedOrdinal)
                        {
                            string shiftedFirst = firstSegment.Substring(0, ordSepIdx + 1) +
                                (firstSegOrd - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            expectedRewrittenId = parentIdPrefix + shiftedFirst +
                                (slashIdx < 0 ? "" : relative.Substring(slashIdx));
                        }
                    }
                }

                if (!rewrittenLeavesById.TryGetValue(expectedRewrittenId, out IffLeafChunk rewrittenLeaf))
                {
                    byteExactExceptRemovedLeaf = false;
                    continue;
                }
                if (rewrittenLeaf.TypeId != origLeaf.TypeId || !rewrittenLeaf.Data.SequenceEqual(origLeaf.Data))
                {
                    byteExactExceptRemovedLeaf = false;
                }
            }

            bool byteExact = original.Length == rewritten.Length && original.SequenceEqual(rewritten);

            var result = new JObject
            {
                ["byteExact"] = byteExact,
                ["byteExactExceptRemovedLeaf"] = byteExactExceptRemovedLeaf,
                ["originalLength"] = original.Length,
                ["parentContainerChildCountAfter"] = parentAfter,
                ["parentContainerChildCountBefore"] = parentBefore,
                ["removedLeafId"] = o.RemoveLeafId,
                ["removedLeafTypeId"] = removedLeafTypeId,
                ["rewrittenLength"] = rewritten.Length,
                ["source"] = o.Path,
                ["untouchedLeafCount"] = untouchedLeafCount
            };
            return JsonOutput.EmitSuccess("roundtrip-iff", result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Lookup helpers
        // ─────────────────────────────────────────────────────────────────────

        private static IffLeafChunk FindLeafByStableId(IffDocument doc, string stableId)
        {
            foreach (var node in doc.AllNodesInPreorder)
            {
                if (node is IffLeafChunk leaf && leaf.Id == stableId) return leaf;
            }
            return null;
        }

        private static IffContainerChunk FindContainerByStableId(IffDocument doc, string stableId)
        {
            foreach (var node in doc.AllNodesInPreorder)
            {
                if (node is IffContainerChunk c && c.Id == stableId) return c;
            }
            return null;
        }

        private static IffContainerChunk FindParentOf(IffDocument doc, string childStableId)
        {
            foreach (var node in doc.AllNodesInPreorder)
            {
                if (node is IffContainerChunk c)
                {
                    foreach (var child in c.Children)
                    {
                        if (child.Id == childStableId) return c;
                    }
                }
            }
            return null;
        }

        private static Dictionary<string, IffLeafChunk> BuildLeafIndex(IffDocument doc)
        {
            var map = new Dictionary<string, IffLeafChunk>(StringComparer.Ordinal);
            foreach (var node in doc.AllNodesInPreorder)
            {
                if (node is IffLeafChunk leaf) map[leaf.Id] = leaf;
            }
            return map;
        }

        private static MutableIffNode FindMutableLeafByStableId(MutableIffDocument doc, string stableId)
        {
            // Walk the mutable tree, deriving each node's stable id alongside.
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
                    var child = node.Children[i];
                    var found = FindMutableLeafRecursive(child, childPrefix, i, targetId);
                    if (found != null) return found;
                }
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Hex parsing
        // ─────────────────────────────────────────────────────────────────────

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
