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
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.Iff
{
    /// <summary>
    /// Discriminates a <see cref="MutableIffNode"/> as either a container (FORM/LIST/CAT )
    /// or a leaf chunk. The set is closed — there are no other kinds in EA-IFF-85.
    /// </summary>
    public enum MutableIffNodeKind
    {
        /// <summary>Container chunk (FORM/LIST/CAT ).</summary>
        Container,
        /// <summary>Leaf chunk (anything that is NOT FORM/LIST/CAT , including PROP per EA-IFF-85).</summary>
        Leaf
    }

    /// <summary>
    /// Mutable hybrid-DOM node for IFF editing (D-07).
    ///
    /// <para><b>Hybrid-DOM contract:</b> each node retains its original captured source-byte slice
    /// (the on-disk <c>tag(4) · length(4) · payload</c> framing) AND can be edited in-place.
    /// Untouched nodes round-trip byte-for-byte verbatim via their captured slice; dirty nodes
    /// reserialize fresh (containers re-roll lengths bottom-up; leaves emit fresh
    /// <c>tag · length · payload</c> with NO pad byte per the SWG no-pad quirk).</para>
    ///
    /// <para><b>Ancestor invalidation (HIGH MEDIUM-Cursor-Unique):</b> any structural op or payload
    /// edit walks up the parent chain, setting every ancestor's <see cref="IsDirty"/> AND clearing
    /// every ancestor's captured-source-slice (so a clean container with a dirty descendant cannot
    /// re-emit a stale verbatim slice over the dirty subtree).</para>
    ///
    /// <para><b>Defensive payload copy (MEDIUM-7):</b> the <see cref="GetPayloadCopy"/> getter returns
    /// a defensive copy of the underlying leaf bytes (so callers cannot mutate the array in place and
    /// bypass the dirty bit); <see cref="SetPayload"/> copies the supplied array (defensive on the
    /// way in too).</para>
    ///
    /// <para><b>Container TypeID set (mirrors <see cref="IffReader"/>):</b> { "FORM", "LIST", "CAT " }
    /// (the trailing space on "CAT " is load-bearing per EA-IFF-85 §3.1).</para>
    ///
    /// <para><b>Read model is untouched:</b> this is a sibling mutable type hierarchy; it never mutates
    /// the immutable <see cref="IffDocument"/> / <see cref="IffChunk"/> / <see cref="IffContainerChunk"/>
    /// / <see cref="IffLeafChunk"/> read result.</para>
    /// </summary>
    public sealed class MutableIffNode
    {
        // ─────────────────────────────────────────────────────────────────────
        // Container TypeID set — mirrors IffReader.ContainerTypeIds.
        // Kept in sync via the same literal values so any divergence shows up here.
        // ─────────────────────────────────────────────────────────────────────
        internal static readonly HashSet<string> ContainerTypeIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "FORM",
            "LIST",
            "CAT "
        };

        // ─────────────────────────────────────────────────────────────────────
        // Discriminator + structural state
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Discriminator: container vs leaf.</summary>
        public MutableIffNodeKind Kind { get; private set; }

        /// <summary>
        /// 4-character ASCII TypeID (e.g. "FORM", "DATA", "CAT "). Settable for the rename/retag
        /// structural op. Setting it dirties this node and propagates ancestor invalidation.
        /// </summary>
        public string TypeId
        {
            get { return typeId; }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                if (value.Length != 4) throw new ArgumentException("TypeId must be exactly 4 characters.", "value");
                ValidatePrintableAscii(value, "TypeId");
                // Switching between container and leaf TypeIds via rename is not supported — would
                // require the children/payload state to flip, which is structurally inconsistent.
                bool wouldBecomeContainer = ContainerTypeIds.Contains(value);
                bool isContainer = Kind == MutableIffNodeKind.Container;
                if (wouldBecomeContainer != isContainer)
                {
                    throw new InvalidOperationException(
                        "Rename/retag cannot cross the container/leaf boundary. Current kind="
                        + Kind + ", proposed TypeId='" + value + "'. Use Add/Remove to restructure.");
                }
                if (typeId != value)
                {
                    typeId = value;
                    MarkDirtyAndInvalidateAncestors();
                }
            }
        }
        private string typeId;

        /// <summary>
        /// 4-character ASCII sub-type tag for containers (e.g. "WSNP" on a FORM). Null on leaves.
        /// Settable for the edit-FORM-sub-type structural op.
        /// </summary>
        public string SubTypeId
        {
            get { return subTypeId; }
            set
            {
                if (Kind != MutableIffNodeKind.Container)
                    throw new InvalidOperationException("SubTypeId is only valid on container nodes.");
                if (value == null) throw new ArgumentNullException("value");
                if (value.Length != 4) throw new ArgumentException("SubTypeId must be exactly 4 characters.", "value");
                ValidatePrintableAscii(value, "SubTypeId");
                if (subTypeId != value)
                {
                    subTypeId = value;
                    MarkDirtyAndInvalidateAncestors();
                }
            }
        }
        private string subTypeId;

        /// <summary>The parent node, or null for the root.</summary>
        public MutableIffNode Parent { get; private set; }

        // Children backing list (containers only); empty list on leaves.
        private readonly List<MutableIffNode> children = new List<MutableIffNode>();

        /// <summary>Ordered child list for containers (read-only view; empty on leaves).</summary>
        public IReadOnlyList<MutableIffNode> Children { get { return children.AsReadOnly(); } }

        // Leaf payload backing field; null on containers.
        private byte[] payload;

        /// <summary>
        /// True when this node (or any descendant for containers) has been modified since
        /// construction or the last clean checkpoint. A dirty node MUST be reserialized fresh by
        /// <see cref="IffWriter"/> rather than re-emitting any captured verbatim slice.
        /// </summary>
        public bool IsDirty { get; private set; }

        // Verbatim captured source slice: bytes [OffsetBytes .. OffsetBytes + 8 + LengthBytes]
        // from the original document buffer. CLEARED to null on any structural edit or descendant
        // edit (ancestor invalidation). Null on freshly-constructed nodes (no captured source).
        private byte[] capturedSlice;

        /// <summary>
        /// True iff this node has a captured verbatim source-byte slice that is still valid for
        /// re-emit (i.e. neither this node nor any descendant has been edited since capture).
        /// </summary>
        public bool HasCapturedSlice { get { return capturedSlice != null; } }

        // ─────────────────────────────────────────────────────────────────────
        // Construction (internal — go through MutableIffDocument.FromDocument or the
        // structural Add* APIs on a parent container).
        // ─────────────────────────────────────────────────────────────────────

        private MutableIffNode(MutableIffNodeKind kind, string typeId, string subTypeId, byte[] payload, byte[] capturedSlice)
        {
            Kind = kind;
            this.typeId = typeId;
            this.subTypeId = subTypeId;
            this.payload = payload;
            this.capturedSlice = capturedSlice;
            IsDirty = false;
        }

        /// <summary>Creates a fresh (dirty, no captured slice) leaf node.</summary>
        public static MutableIffNode NewLeaf(string typeId, byte[] payload)
        {
            if (typeId == null) throw new ArgumentNullException("typeId");
            if (typeId.Length != 4) throw new ArgumentException("TypeId must be exactly 4 characters.", "typeId");
            ValidatePrintableAscii(typeId, "typeId");
            if (ContainerTypeIds.Contains(typeId))
                throw new ArgumentException("'" + typeId + "' is a container TypeID; use NewContainer.", "typeId");
            byte[] copy = (payload == null) ? new byte[0] : (byte[])payload.Clone();
            var node = new MutableIffNode(MutableIffNodeKind.Leaf, typeId, null, copy, null);
            node.IsDirty = true;
            return node;
        }

        /// <summary>Creates a fresh (dirty, no captured slice) container node.</summary>
        public static MutableIffNode NewContainer(string typeId, string subTypeId)
        {
            if (typeId == null) throw new ArgumentNullException("typeId");
            if (typeId.Length != 4) throw new ArgumentException("TypeId must be exactly 4 characters.", "typeId");
            if (!ContainerTypeIds.Contains(typeId))
                throw new ArgumentException("'" + typeId + "' is not a container TypeID (FORM/LIST/CAT ).", "typeId");
            if (subTypeId == null) throw new ArgumentNullException("subTypeId");
            if (subTypeId.Length != 4) throw new ArgumentException("SubTypeId must be exactly 4 characters.", "subTypeId");
            ValidatePrintableAscii(subTypeId, "subTypeId");
            var node = new MutableIffNode(MutableIffNodeKind.Container, typeId, subTypeId, null, null);
            node.IsDirty = true;
            return node;
        }

        // Internal factories used by FromDocument so the build can set the captured slice
        // and start each node clean (IsDirty = false).
        internal static MutableIffNode FromLeaf(string typeId, byte[] payload, byte[] capturedSlice)
        {
            byte[] payloadCopy = (payload == null) ? new byte[0] : (byte[])payload.Clone();
            var node = new MutableIffNode(MutableIffNodeKind.Leaf, typeId, null, payloadCopy, capturedSlice);
            return node;
        }

        internal static MutableIffNode FromContainer(string typeId, string subTypeId, byte[] capturedSlice)
        {
            var node = new MutableIffNode(MutableIffNodeKind.Container, typeId, subTypeId, null, capturedSlice);
            return node;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Captured-slice access (internal — IffWriter reads this directly for verbatim re-emit)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the captured verbatim source-byte slice for this node (the on-disk
        /// <c>tag · length · payload</c> framing as it appeared in the source buffer), or null if
        /// no slice is captured (freshly constructed node, or ancestor-invalidated by a descendant
        /// edit). INTERNAL: <see cref="IffWriter"/> consumes this to byte-perfect re-emit clean nodes.
        /// </summary>
        internal byte[] GetCapturedSliceInternal() { return capturedSlice; }

        // ─────────────────────────────────────────────────────────────────────
        // Leaf payload (defensive copy on get/set per MEDIUM-7)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a DEFENSIVE COPY of the leaf payload bytes (callers cannot mutate the array
        /// in place and bypass the dirty bit). Throws on a container node.
        /// </summary>
        public byte[] GetPayloadCopy()
        {
            if (Kind != MutableIffNodeKind.Leaf)
                throw new InvalidOperationException("Payload is only valid on leaf nodes.");
            return (byte[])payload.Clone();
        }

        /// <summary>
        /// Returns the length of the leaf payload without allocating a copy. Throws on a container.
        /// </summary>
        public int PayloadLength
        {
            get
            {
                if (Kind != MutableIffNodeKind.Leaf)
                    throw new InvalidOperationException("PayloadLength is only valid on leaf nodes.");
                return payload.Length;
            }
        }

        /// <summary>
        /// Replaces the leaf payload with a defensive copy of the supplied bytes; marks this node
        /// dirty and propagates ancestor invalidation. Throws on a container node.
        /// </summary>
        public void SetPayload(byte[] newPayload)
        {
            if (Kind != MutableIffNodeKind.Leaf)
                throw new InvalidOperationException("Payload is only valid on leaf nodes.");
            if (newPayload == null) throw new ArgumentNullException("newPayload");
            payload = (byte[])newPayload.Clone();
            MarkDirtyAndInvalidateAncestors();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal read-only access used by IffWriter for fresh serialization
        // (no defensive copy needed — the writer reads, doesn't mutate).
        // ─────────────────────────────────────────────────────────────────────

        internal byte[] GetPayloadInternal()
        {
            return payload;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Structural operations on containers — each one dirties this node + every ancestor
        // and clears every ancestor's captured slice (ancestor invalidation).
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Appends a new leaf child to this container.
        /// </summary>
        public MutableIffNode AddLeaf(string newTypeId, byte[] newPayload)
        {
            RequireContainer("AddLeaf");
            var child = NewLeaf(newTypeId, newPayload);
            child.Parent = this;
            children.Add(child);
            MarkDirtyAndInvalidateAncestors();
            return child;
        }

        /// <summary>
        /// Appends a new container child (FORM/LIST/CAT ) with the supplied 4-char sub-type to this container.
        /// </summary>
        public MutableIffNode AddContainer(string newTypeId, string newSubTypeId)
        {
            RequireContainer("AddContainer");
            var child = NewContainer(newTypeId, newSubTypeId);
            child.Parent = this;
            children.Add(child);
            MarkDirtyAndInvalidateAncestors();
            return child;
        }

        /// <summary>
        /// Removes the specified child from this container. Returns true iff a child was removed.
        /// </summary>
        public bool Remove(MutableIffNode child)
        {
            RequireContainer("Remove");
            if (child == null) throw new ArgumentNullException("child");
            int idx = children.IndexOf(child);
            if (idx < 0) return false;
            child.Parent = null;
            children.RemoveAt(idx);
            MarkDirtyAndInvalidateAncestors();
            return true;
        }

        /// <summary>
        /// Removes the descendant with the given stable id (as derived by
        /// <see cref="MutableIffDocument.DeriveStableId"/>). Returns true iff a node was removed.
        /// Used by the CLI structural-op verbs (08-02 <c>--remove-leaf</c>) and editor surfaces that
        /// only have a stable id to address by (Rule-2 auto-add: 08-REVIEWS R3-M3).
        /// </summary>
        public bool RemoveByStableId(string stableId)
        {
            if (stableId == null) throw new ArgumentNullException("stableId");
            // Walk children depth-first; the document's RemoveByStableId entry point uses this
            // method on the root container.
            for (int i = 0; i < children.Count; i++)
            {
                var c = children[i];
                string cid = MutableIffDocument.DeriveStableId(c, this == null ? "" : this.GetStableIdRecursive(), i);
                if (cid == stableId)
                {
                    Remove(c);
                    return true;
                }
                if (c.Kind == MutableIffNodeKind.Container)
                {
                    if (c.RemoveByStableIdRecursive(stableId, cid + "/"))
                        return true;
                }
            }
            return false;
        }

        // Helper used by RemoveByStableId — walks a subtree using the precomputed prefix.
        internal bool RemoveByStableIdRecursive(string targetStableId, string idPrefix)
        {
            for (int i = 0; i < children.Count; i++)
            {
                var c = children[i];
                string cid = MutableIffDocument.DeriveStableId(c, idPrefix, i);
                if (cid == targetStableId)
                {
                    Remove(c);
                    return true;
                }
                if (c.Kind == MutableIffNodeKind.Container)
                {
                    if (c.RemoveByStableIdRecursive(targetStableId, cid + "/"))
                        return true;
                }
            }
            return false;
        }

        // Recursively derives the stable id of THIS node by walking the parent chain. Used by
        // RemoveByStableId on the root entry point. Internal — production callers should use
        // MutableIffDocument.DeriveStableId directly.
        internal string GetStableIdRecursive()
        {
            if (Parent == null)
            {
                return MutableIffDocument.DeriveStableId(this, "", 0);
            }
            // Find this node's ordinal in its parent.
            int ord = 0;
            var siblings = Parent.children;
            for (int i = 0; i < siblings.Count; i++)
            {
                if (object.ReferenceEquals(siblings[i], this))
                {
                    ord = i;
                    break;
                }
            }
            return MutableIffDocument.DeriveStableId(this, Parent.GetStableIdRecursive() + "/", ord);
        }

        /// <summary>
        /// Moves the specified child one position toward the start (Reorder up).
        /// </summary>
        public void ReorderUp(MutableIffNode child)
        {
            RequireContainer("ReorderUp");
            if (child == null) throw new ArgumentNullException("child");
            int idx = children.IndexOf(child);
            if (idx <= 0) return;
            children.RemoveAt(idx);
            children.Insert(idx - 1, child);
            MarkDirtyAndInvalidateAncestors();
        }

        /// <summary>
        /// Moves the specified child one position toward the end (Reorder down).
        /// </summary>
        public void ReorderDown(MutableIffNode child)
        {
            RequireContainer("ReorderDown");
            if (child == null) throw new ArgumentNullException("child");
            int idx = children.IndexOf(child);
            if (idx < 0 || idx >= children.Count - 1) return;
            children.RemoveAt(idx);
            children.Insert(idx + 1, child);
            MarkDirtyAndInvalidateAncestors();
        }

        /// <summary>
        /// Deep-copies the specified child and appends the copy as the last child of this container.
        /// </summary>
        public MutableIffNode Duplicate(MutableIffNode child)
        {
            RequireContainer("Duplicate");
            if (child == null) throw new ArgumentNullException("child");
            if (children.IndexOf(child) < 0)
                throw new InvalidOperationException("Duplicate: child is not a member of this container.");
            var copy = DeepCopyForDuplication(child);
            copy.Parent = this;
            children.Add(copy);
            MarkDirtyAndInvalidateAncestors();
            return copy;
        }

        // Deep-copy helper for Duplicate. The copy is dirty (no captured slice) so it reserializes
        // fresh on write; this matches the semantics that the duplicate is a new node, not a verbatim
        // re-emit of the original on-disk slice.
        private static MutableIffNode DeepCopyForDuplication(MutableIffNode src)
        {
            if (src.Kind == MutableIffNodeKind.Leaf)
            {
                return NewLeaf(src.TypeId, src.payload);
            }
            else
            {
                var dst = NewContainer(src.TypeId, src.SubTypeId);
                foreach (var c in src.children)
                {
                    var copy = DeepCopyForDuplication(c);
                    copy.Parent = dst;
                    dst.children.Add(copy);
                }
                return dst;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal helpers
        // ─────────────────────────────────────────────────────────────────────

        internal void SetParent(MutableIffNode parent)
        {
            Parent = parent;
        }

        internal void AddChildInternal(MutableIffNode child)
        {
            children.Add(child);
            child.SetParent(this);
        }

        // Dirty-propagation contract: this node's IsDirty is set; every ancestor's IsDirty is set
        // AND every ancestor's captured slice is cleared (so a clean ancestor cannot re-emit a stale
        // verbatim slice over the now-dirty subtree). This node's captured slice is also cleared so
        // it reserializes fresh, not from the captured slice.
        private void MarkDirtyAndInvalidateAncestors()
        {
            IsDirty = true;
            capturedSlice = null;
            var p = Parent;
            while (p != null)
            {
                p.IsDirty = true;
                p.capturedSlice = null;
                p = p.Parent;
            }
        }

        private void RequireContainer(string op)
        {
            if (Kind != MutableIffNodeKind.Container)
                throw new InvalidOperationException(op + " is only valid on container nodes.");
        }

        private static void ValidatePrintableAscii(string s, string paramName)
        {
            foreach (char c in s)
            {
                if (c < 0x20 || c > 0x7E)
                    throw new ArgumentException(paramName + " must contain only printable ASCII (0x20..0x7E).", paramName);
            }
        }

    }
}
