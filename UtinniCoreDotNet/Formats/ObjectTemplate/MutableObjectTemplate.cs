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
// Object-template layout understood by reading swg-client-v2
// .../sharedObject/.../ObjectTemplate.cpp and the generated Shared*ObjectTemplate.cpp loaders
// (SOE/Bootprint, All Rights Reserved): FORM <type> -> optional FORM DERV (base-template name) ->
// FORM <version> (digit tag) -> int32 paramCount leaf -> paramCount param chunks (NUL name + value).
// Only the on-disk layout was studied — no code, comments, identifier names, or test fixtures copied
// from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.Text;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.ObjectTemplate
{
    /// <summary>
    /// One mutable local param entry: the field name + its typed value, plus the back-reference to
    /// the captured <see cref="MutableIffNode"/> leaf so an in-place edit re-encodes ONLY that leaf
    /// (the Phase 8 hybrid-DOM path), leaving every untouched param byte-identical.
    /// </summary>
    public sealed class MutableObjectTemplateParam
    {
        /// <summary>The NUL-terminated field name.</summary>
        public string FieldName { get; }

        /// <summary>The current typed value.</summary>
        public ObjectTemplateParamValue Value { get; internal set; }

        // The version-form leaf node that carries this param chunk's payload. Null only for a param
        // appended in-memory before its leaf was materialized (the Add path materializes it eagerly,
        // so this is non-null after AddOverride).
        internal MutableIffNode Leaf { get; set; }

        internal MutableObjectTemplateParam(string fieldName, ObjectTemplateParamValue value, MutableIffNode leaf)
        {
            if (fieldName == null) throw new ArgumentNullException("fieldName");
            if (value == null) throw new ArgumentNullException("value");
            FieldName = fieldName;
            Value = value;
            Leaf = leaf;
        }
    }

    /// <summary>
    /// Mutable typed object-template model over a captured <see cref="MutableIffDocument"/> — the
    /// editable counterpart to the Phase 7 read-only <see cref="ObjectTemplateDecoder"/>. Mirrors the
    /// Phase 9 <c>MutableDataTableDocument</c> shape: a version string, the ordered local param-chunk
    /// set, the DERV base name, and the back-reference <see cref="SourceIff"/>.
    ///
    /// <para><b>Byte-exactness posture (PATTERNS.md OT caveat):</b> edits mutate the captured
    /// <see cref="MutableIffDocument"/> LEAF in place (Phase 8 hybrid-DOM) and re-encode ONLY the
    /// touched param via <see cref="ObjectTemplateParamCodec.Encode"/> — never a full rebuild. The
    /// version-form <c>int32 paramCount</c> leaf is machine-managed (D-04): add/remove re-derive it
    /// from the live param count, exactly as <c>MutableDataTableDocument</c> writes <c>Columns.Count</c>.</para>
    /// </summary>
    public sealed class MutableObjectTemplate
    {
        private const string DerivationSubType = "DERV";

        private readonly List<MutableObjectTemplateParam> _params;

        // The version-form container node (digit-tagged child of root) and its count leaf — the two
        // nodes structural mutations touch.
        private readonly MutableIffNode _versionForm;
        private readonly MutableIffNode _countLeaf;

        private MutableObjectTemplate(
            MutableIffDocument sourceIff,
            string rootType,
            string version,
            string baseTemplateName,
            MutableIffNode versionForm,
            MutableIffNode countLeaf,
            List<MutableObjectTemplateParam> paramList)
        {
            SourceIff = sourceIff;
            RootType = rootType;
            Version = version;
            BaseTemplateName = baseTemplateName;
            _versionForm = versionForm;
            _countLeaf = countLeaf;
            _params = paramList;
        }

        /// <summary>The captured raw-IFF document — the byte source for the byte-exact re-emit.</summary>
        public MutableIffDocument SourceIff { get; }

        /// <summary>The root template type tag (e.g. "SHOT").</summary>
        public string RootType { get; }

        /// <summary>The digit-tagged version-form tag (e.g. "0000").</summary>
        public string Version { get; }

        /// <summary>The DERV base-template name string, or null when the template declares no base.</summary>
        public string BaseTemplateName { get; }

        /// <summary>The ordered local param chunks (the local overrides), in on-disk order.</summary>
        public IReadOnlyList<MutableObjectTemplateParam> LocalParams { get { return _params.AsReadOnly(); } }

        /// <summary>True iff any mutation has been applied since parse.</summary>
        public bool IsDirty { get; private set; }

        internal void MarkDirty() { IsDirty = true; }

        /// <summary>
        /// Parses an object template from a captured <see cref="MutableIffDocument"/> (captured by the
        /// caller via <see cref="MutableIffDocument.FromDocument(IffDocument, byte[])"/>, the Phase 8
        /// hybrid-DOM entry point). Reuses the Phase 7 framing (FORM &lt;type&gt; → optional DERV →
        /// digit version form → int32 count leaf → param chunks) and decodes each local param via
        /// <see cref="ObjectTemplateParamCodec"/>. Each param's captured leaf is retained so an
        /// <see cref="EditOverride"/> re-encodes ONLY that leaf in place — untouched params re-emit
        /// their verbatim slice through <see cref="IffWriter"/>.
        /// </summary>
        public static MutableObjectTemplate FromMutableIff(MutableIffDocument mutable)
        {
            if (mutable == null) throw new ArgumentNullException("mutable");
            MutableIffNode root = mutable.Root;
            if (root == null || root.Kind != MutableIffNodeKind.Container)
            {
                throw new DecoderException(DecoderError.UnexpectedForm, "Object template root is not a FORM.");
            }

            string rootType = root.SubTypeId;

            // DERV base name (optional).
            string baseTemplateName = null;
            MutableIffNode derv = FindContainerChild(root, DerivationSubType);
            if (derv != null)
            {
                MutableIffNode nameLeaf = FirstLeaf(derv);
                if (nameLeaf != null)
                {
                    baseTemplateName = new IffPayloadCursor(nameLeaf.GetPayloadCopy()).ReadCString(Encoding.ASCII);
                }
            }

            // Version form + count leaf + param leaves.
            MutableIffNode versionForm = FindVersionForm(root);
            if (versionForm == null)
            {
                throw new DecoderException(DecoderError.UnexpectedForm,
                    "FORM " + rootType.TrimEnd() + " has no version form — not a decodable object template.");
            }
            string version = versionForm.SubTypeId;

            List<MutableIffNode> leaves = LeafChildren(versionForm);
            if (leaves.Count < 1 || leaves[0].PayloadLength != 4)
            {
                throw new DecoderException(DecoderError.UnexpectedForm,
                    "Object template version form " + version + " is missing its param-count chunk.");
            }

            MutableIffNode countLeaf = leaves[0];
            int paramCount = new IffPayloadCursor(countLeaf.GetPayloadCopy()).ReadInt32Le();
            if (paramCount < 0)
            {
                throw new DecoderException(DecoderError.NegativeCount, "Param count is negative: " + paramCount + ".");
            }
            int availableParams = leaves.Count - 1;
            if (paramCount > availableParams)
            {
                throw new DecoderException(DecoderError.CountExceedsCap,
                    "Param count " + paramCount + " exceeds the " + availableParams + " param chunk(s) present.");
            }

            var paramList = new List<MutableObjectTemplateParam>(paramCount);
            for (int i = 0; i < paramCount; i++)
            {
                MutableIffNode leaf = leaves[i + 1];
                byte[] payload = leaf.GetPayloadCopy();
                ObjectTemplateParamEntry entry;
                try
                {
                    entry = ObjectTemplateParamCodec.Decode(payload);
                }
                catch (DecoderException)
                {
                    // Real SWG templates store list/array params (e.g. draft-schematic "slots" /
                    // "attributes", hair tint lists) as a header chunk followed by nameless element
                    // chunks that carry no leading CString name — so the single-chunk <name>\0<value>
                    // codec's ReadCString throws on an element chunk. Rather than abort the whole
                    // template, capture the chunk verbatim as a raw hex-fallback param. The template
                    // still opens, and round-trip stays byte-exact: Serialize re-emits the full
                    // captured IFF tree (ObjectTemplateWriter -> IffWriter.Write(SourceIff)), so the
                    // bytes are preserved regardless of how this chunk is interpreted here. Fully typed
                    // list/struct decode (StructParamOT etc.) is tracked follow-up work.
                    string rawName = "‹raw chunk " + i + "›"; // stable + unique per index.
                    entry = new ObjectTemplateParamEntry(
                        rawName,
                        ObjectTemplateParamValue.FromRawBytes(payload, ObjectTemplateDataTypeTag.None));
                }
                paramList.Add(new MutableObjectTemplateParam(entry.FieldName, entry.Value, leaf));
            }

            return new MutableObjectTemplate(mutable, rootType, version, baseTemplateName, versionForm, countLeaf, paramList);
        }

        /// <summary>
        /// Edits an existing local param's value in place. The captured leaf is re-encoded via
        /// <see cref="ObjectTemplateParamCodec.Encode"/>, dirtying only that leaf + its ancestors;
        /// every untouched param re-serializes byte-identical.
        /// </summary>
        public void EditOverride(string fieldName, ObjectTemplateParamValue newValue)
        {
            if (fieldName == null) throw new ArgumentNullException("fieldName");
            if (newValue == null) throw new ArgumentNullException("newValue");

            MutableObjectTemplateParam param = FindParam(fieldName);
            if (param == null)
            {
                throw new InvalidOperationException(
                    "EditOverride: no local param named '" + fieldName + "'. Use AddOverride to promote an inherited field.");
            }

            param.Value = newValue;
            param.Leaf.SetPayload(ObjectTemplateParamCodec.Encode(fieldName, newValue));
            MarkDirty();
        }

        /// <summary>
        /// Appends a new local param chunk (promotes an inherited field to a local override) and
        /// re-derives the machine-managed <c>int32 paramCount</c> leaf from the live count.
        /// </summary>
        public void AddOverride(string fieldName, ObjectTemplateParamValue value)
        {
            if (fieldName == null) throw new ArgumentNullException("fieldName");
            if (value == null) throw new ArgumentNullException("value");
            if (FindParam(fieldName) != null)
            {
                throw new InvalidOperationException(
                    "AddOverride: a local param named '" + fieldName + "' already exists. Use EditOverride.");
            }

            byte[] payload = ObjectTemplateParamCodec.Encode(fieldName, value);
            MutableIffNode leaf = _versionForm.AddLeaf(PickParamLeafTypeId(), payload);
            _params.Add(new MutableObjectTemplateParam(fieldName, value, leaf));
            RewriteCount();
            MarkDirty();
        }

        /// <summary>
        /// Removes a local param chunk (reverts the field to inherited) and re-derives the
        /// machine-managed <c>int32 paramCount</c> leaf from the live count.
        /// </summary>
        public bool RemoveOverride(string fieldName)
        {
            if (fieldName == null) throw new ArgumentNullException("fieldName");
            MutableObjectTemplateParam param = FindParam(fieldName);
            if (param == null) return false;

            _versionForm.Remove(param.Leaf);
            _params.Remove(param);
            RewriteCount();
            MarkDirty();
            return true;
        }

        /// <summary>Serializes to byte-exact output via <see cref="ObjectTemplateWriter"/>.</summary>
        public byte[] Serialize()
        {
            return ObjectTemplateWriter.Serialize(this);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        // Re-derive the machine-managed paramCount leaf (D-04) from the live local-param count,
        // mirroring MutableDataTableDocument writing Columns.Count. int32 little-endian.
        private void RewriteCount()
        {
            int count = _params.Count;
            byte[] le = new[]
            {
                (byte)(count & 0xFF),
                (byte)((count >> 8) & 0xFF),
                (byte)((count >> 16) & 0xFF),
                (byte)((count >> 24) & 0xFF)
            };
            _countLeaf.SetPayload(le);
        }

        // New param leaves reuse the TypeId of an existing param leaf when present (so the on-disk
        // tag matches the template's convention), falling back to the engine's conventional "PCNT"-
        // style 4-cc only if the template somehow has no existing param leaf. In practice a template
        // with overrides always has at least one param leaf to mirror.
        private string PickParamLeafTypeId()
        {
            for (int i = 0; i < _params.Count; i++)
            {
                MutableIffNode existing = _params[i].Leaf;
                if (existing != null)
                {
                    return existing.TypeId;
                }
            }
            // Conventional param chunk tag used by the engine version forms when no sibling exists.
            return "XXXX";
        }

        private MutableObjectTemplateParam FindParam(string fieldName)
        {
            for (int i = 0; i < _params.Count; i++)
            {
                if (string.Equals(_params[i].FieldName, fieldName, StringComparison.Ordinal))
                {
                    return _params[i];
                }
            }
            return null;
        }

        private static MutableIffNode FindContainerChild(MutableIffNode parent, string subTypeId)
        {
            foreach (MutableIffNode child in parent.Children)
            {
                if (child.Kind == MutableIffNodeKind.Container && child.SubTypeId == subTypeId)
                {
                    return child;
                }
            }
            return null;
        }

        private static MutableIffNode FindVersionForm(MutableIffNode parent)
        {
            foreach (MutableIffNode child in parent.Children)
            {
                if (child.Kind == MutableIffNodeKind.Container && IsAllDigits(child.SubTypeId))
                {
                    return child;
                }
            }
            return null;
        }

        private static bool IsAllDigits(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 4) return false;
            foreach (char ch in s)
            {
                if (ch < '0' || ch > '9') return false;
            }
            return true;
        }

        private static MutableIffNode FirstLeaf(MutableIffNode parent)
        {
            foreach (MutableIffNode child in parent.Children)
            {
                if (child.Kind == MutableIffNodeKind.Leaf) return child;
            }
            return null;
        }

        private static List<MutableIffNode> LeafChildren(MutableIffNode parent)
        {
            var leaves = new List<MutableIffNode>();
            foreach (MutableIffNode child in parent.Children)
            {
                if (child.Kind == MutableIffNodeKind.Leaf) leaves.Add(child);
            }
            return leaves;
        }
    }
}
