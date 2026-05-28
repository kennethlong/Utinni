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
// Format understood by reading swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}
// (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. No code,
// comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.

using System;
using System.Globalization;

namespace UtinniCoreDotNet.Formats.Iff
{
    /// <summary>
    /// Mutable hybrid-DOM document — the editable counterpart to <see cref="IffDocument"/> per D-07.
    ///
    /// <para><b>Construction:</b> <see cref="FromDocument(IffDocument, byte[])"/> builds a mutable
    /// tree mirroring the read result and captures each node's verbatim source-byte slice
    /// (<c>sourceBytes[OffsetBytes .. OffsetBytes + 8 + LengthBytes]</c>). Untouched nodes re-emit
    /// that slice byte-for-byte via <see cref="IffWriter"/>; any edit dirties the touched node AND
    /// every ancestor (ancestor invalidation) so a clean container with a dirty descendant cannot
    /// re-emit a stale slice.</para>
    ///
    /// <para><b>Padded-input policy (Codex unique concern):</b> if <see cref="IffReader"/> consumed
    /// a single 0x00 pad byte for an odd-length leaf (EA-IFF-85 compliant input), the captured slice
    /// covers <c>[OffsetBytes .. OffsetBytes + 8 + LengthBytes]</c> ONLY — the pad byte is NOT part of
    /// the captured slice and the writer never emits a pad (SWG no-pad quirk). Round-trip for SWG IFF
    /// (which omits the pad) stays byte-exact. Round-trip for NON-SWG padded inputs is NOT byte-exact:
    /// the pad bytes are LOST. Pad bytes from non-SWG-style padded inputs are NOT preserved on write.
    /// This is intentional and matches the locked D-07 / SWG no-pad quirk policy.</para>
    /// </summary>
    public sealed class MutableIffDocument
    {
        /// <summary>The mutable root node (always a container — FORM/LIST/CAT ).</summary>
        public MutableIffNode Root { get; private set; }

        /// <summary>Constructs a MutableIffDocument with the given mutable root.</summary>
        public MutableIffDocument(MutableIffNode root)
        {
            if (root == null) throw new ArgumentNullException("root");
            Root = root;
        }

        /// <summary>
        /// Builds a <see cref="MutableIffDocument"/> from an immutable <see cref="IffDocument"/> and
        /// the full source-byte buffer used to parse it. Each node's verbatim source-byte slice
        /// (<c>sourceBytes[chunk.OffsetBytes .. chunk.OffsetBytes + 8 + chunk.LengthBytes]</c>) is
        /// captured for byte-exact verbatim re-emit on a no-mutation round trip (D-07 hybrid DOM).
        /// </summary>
        /// <param name="doc">The parsed read result.</param>
        /// <param name="sourceBytes">The full byte buffer that was passed to <see cref="IffReader"/>.</param>
        /// <remarks>
        /// <b>Padded-input policy:</b> Pad bytes from non-SWG-style padded inputs are NOT preserved
        /// on write. The IFF read path detects-but-does-not-include a pad byte from the chunk's slice
        /// (see <c>IffReader.ReadLeafChunk</c>), so the captured slice never includes a pad. SWG IFF
        /// omits the pad in writing, so SWG round-trips byte-exact. EA-IFF-85-compliant inputs that
        /// include a pad will round-trip to a 1-byte-shorter output per odd-length leaf — intentional
        /// per the locked SWG no-pad quirk.
        /// </remarks>
        public static MutableIffDocument FromDocument(IffDocument doc, byte[] sourceBytes)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (doc.Root == null) throw new ArgumentException("IffDocument has no root.", "doc");
            if (sourceBytes == null) throw new ArgumentNullException("sourceBytes");

            MutableIffNode rootMut = BuildSubtree(doc.Root, sourceBytes);
            return new MutableIffDocument(rootMut);
        }

        // Recursively builds a mutable subtree mirroring the immutable read result, capturing each
        // node's verbatim source-byte slice for D-07 re-emit.
        private static MutableIffNode BuildSubtree(IffChunk chunk, byte[] sourceBytes)
        {
            long sliceStart = chunk.OffsetBytes;
            long sliceLen = 8L + chunk.LengthBytes; // tag(4) + length(4) + payload(LengthBytes)
            if (sliceStart < 0 || sliceStart > sourceBytes.Length)
                throw new ArgumentException(
                    "OffsetBytes is outside the source buffer (offset=" + sliceStart
                    + ", buffer length=" + sourceBytes.Length + ").", "sourceBytes");
            if (sliceStart + sliceLen > sourceBytes.Length)
                throw new ArgumentException(
                    "Captured-slice end exceeds the source buffer (start=" + sliceStart
                    + ", len=" + sliceLen + ", buffer length=" + sourceBytes.Length + ").", "sourceBytes");

            byte[] captured = new byte[sliceLen];
            Array.Copy(sourceBytes, sliceStart, captured, 0, sliceLen);

            var container = chunk as IffContainerChunk;
            if (container != null)
            {
                var node = MutableIffNode.FromContainer(container.TypeId, container.SubTypeId, captured);
                foreach (var child in container.Children)
                {
                    var childMut = BuildSubtree(child, sourceBytes);
                    node.AddChildInternal(childMut);
                }
                return node;
            }

            var leaf = chunk as IffLeafChunk;
            if (leaf != null)
            {
                return MutableIffNode.FromLeaf(leaf.TypeId, leaf.Data, captured);
            }

            throw new InvalidOperationException(
                "Unknown IffChunk concrete type: " + chunk.GetType().FullName);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Document-level convenience over the root
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Removes the descendant with the given stable id (as derived by
        /// <see cref="DeriveStableId"/>). Returns true iff a node was removed.
        /// Used by the CLI structural-op verbs (08-02 <c>--remove-leaf</c>) so the CLI and the editor
        /// agree on how to address a node without sharing a live reference.
        /// </summary>
        public bool RemoveByStableId(string stableId)
        {
            if (stableId == null) throw new ArgumentNullException("stableId");
            if (Root == null) return false;
            // The root cannot be removed via this entry point.
            if (DeriveStableId(Root, "", 0) == stableId) return false;
            return Root.RemoveByStableIdRecursive(stableId, DeriveStableId(Root, "", 0) + "/");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Stable-id derivation — mirrors IffChunk's path-id format
        // (e.g. "FORM:WSNP/0", nested "FORM:WSNP/0/FORM:OBJS/3", leaf "FORM:WSNP/0/DATA:DATA/0").
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Derives the stable path-id for a mutable node given its parent's id prefix and its
        /// ordinal within its parent (root nodes pass <c>parentIdPrefix=""</c> and <c>ordinal=0</c>).
        /// Mirrors the format used by <see cref="IffChunk.Id"/>:
        /// <c>FORM:WSNP/0</c> for root, <c>FORM:WSNP/0/FORM:OBJS/3</c> for nested,
        /// <c>FORM:WSNP/0/DATA:DATA/0</c> for leaves.
        /// </summary>
        public static string DeriveStableId(MutableIffNode node, string parentIdPrefix, int ordinal)
        {
            if (node == null) throw new ArgumentNullException("node");
            if (parentIdPrefix == null) throw new ArgumentNullException("parentIdPrefix");
            string typeTrimmed = node.TypeId.TrimEnd();
            string suffix;
            if (node.Kind == MutableIffNodeKind.Container)
            {
                string subTrimmed = node.SubTypeId.TrimEnd();
                suffix = typeTrimmed + ":" + subTrimmed + "/" + ordinal.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                suffix = typeTrimmed + ":" + typeTrimmed + "/" + ordinal.ToString(CultureInfo.InvariantCulture);
            }
            return parentIdPrefix + suffix;
        }
    }
}
