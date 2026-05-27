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
// (SOE/Bootprint, All Rights Reserved): FORM <type> -> optional FORM DERV (a chunk holding the base
// template name string) -> FORM <version> -> a count chunk (int32 paramCount) followed by paramCount
// param chunks (each a NUL-terminated name + type-specific value bytes). Only the on-disk layout was
// studied — no code, comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.

using System.Collections.Generic;
using System.Text;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Decoders
{
    /// <summary>One row in an object-template view: a field name, its value, and its origin.</summary>
    public sealed class ObjectTemplateField
    {
        public string Name { get; }

        /// <summary>
        /// For a local param: a lowercase hex rendering of the param's raw value bytes (the value is
        /// type-specific and is NOT generically typed in Phase 7). For the base reference row: the
        /// declared base-template name string.
        /// </summary>
        public string Value { get; }

        /// <summary>"local" for a field declared in this template; the base-template name for the @base row.</summary>
        public string InheritedFrom { get; }

        public ObjectTemplateField(string name, string value, string inheritedFrom)
        {
            Name = name;
            Value = value;
            InheritedFrom = inheritedFrom;
        }
    }

    /// <summary>
    /// A decoded object template under the Phase-7 BOUNDED read-only posture: the root template type,
    /// the DECLARED base-template reference (the @base name string from the DERV form, if any), the
    /// version, and this template's LOCAL fields. Phase 11 makes this editable and resolves the full
    /// inherited chain with no rework (D-13).
    /// </summary>
    public sealed class ObjectTemplateView
    {
        public string RootType { get; }
        public string BaseTemplate { get; }   // null when this template declares no base
        public string Version { get; }
        public IReadOnlyList<ObjectTemplateField> Fields { get; }

        public ObjectTemplateView(string rootType, string baseTemplate, string version, IReadOnlyList<ObjectTemplateField> fields)
        {
            RootType = rootType;
            BaseTemplate = baseTemplate;
            Version = version;
            Fields = fields;
        }
    }

    /// <summary>
    /// Decodes an object template (FORM &lt;type&gt;) from the shared <see cref="IffDocument"/> output.
    ///
    /// <para><b>Phase-7 bounded posture (review consensus #3 / Codex):</b> this decoder reads ONLY
    /// this one IFF's declared base-template reference (the DERV name string) and this template's
    /// LOCAL fields. It does NOT follow the base reference into another TRE entry / another IFF to
    /// materialize inherited fields, does NOT touch the shared TRE archive-index / payload-resolver
    /// facade, and does NOT walk recursively. It DISPLAYS the declared base (so the user sees "X");
    /// resolving the full inherited chain is deferred to Phase 11.</para>
    ///
    /// <para><b>Parser purity:</b> NO JSON, NO console output, NO file writes.</para>
    /// </summary>
    public static class ObjectTemplateDecoder
    {
        private const string DerivationSubType = "DERV";

        /// <summary>
        /// Cheap structural sniff for the decode-iff dispatcher: a FORM whose children include a DERV
        /// derivation form or a digit-tagged version form with a 4-byte leading (count) chunk.
        /// </summary>
        public static bool LooksLikeObjectTemplate(IffChunk root)
        {
            var container = root as IffContainerChunk;
            if (container == null) return false;
            if (FindContainerChild(container, DerivationSubType) != null) return true;

            var version = FindVersionForm(container);
            if (version == null) return false;
            var first = FirstLeaf(version);
            return first != null && first.Data.Length == 4;
        }

        /// <summary>
        /// Decodes the object template from <paramref name="doc"/>.
        /// Throws <see cref="DecoderException"/> when the IFF is not template-shaped or a count is forged.
        /// </summary>
        public static ObjectTemplateView Decode(IffDocument doc)
        {
            // Phase-7 bounded posture: declared base + local fields only; no recursive cross-IFF
            // inheritance walk (review #3). This decoder never opens another document.
            if (doc == null)
            {
                throw new DecoderException(DecoderError.UnexpectedForm, "Null IFF document.");
            }

            var root = doc.Root as IffContainerChunk;
            if (root == null)
            {
                throw new DecoderException(DecoderError.UnexpectedForm, "Object template root is not a FORM.");
            }

            string rootType = root.SubTypeId;
            var fields = new List<ObjectTemplateField>();

            // ── Declared base reference (the @base row), if a DERV form is present ──
            string baseTemplate = null;
            var derv = FindContainerChild(root, DerivationSubType);
            if (derv != null)
            {
                var nameLeaf = FirstLeaf(derv);
                if (nameLeaf != null)
                {
                    baseTemplate = new IffPayloadCursor(nameLeaf.Data).ReadCString(Encoding.ASCII);
                    // The base reference is DISPLAYED, never resolved (bounded posture). InheritedFrom
                    // is the base name itself so the UI can show "inherits from <base>".
                    fields.Add(new ObjectTemplateField("@base", baseTemplate, baseTemplate));
                }
            }

            // ── Version form + local params ──
            var versionForm = FindVersionForm(root);
            if (versionForm == null)
            {
                throw new DecoderException(DecoderError.UnexpectedForm,
                    "FORM " + rootType.TrimEnd() + " has no version form — not a decodable object template.");
            }
            string version = versionForm.SubTypeId;

            var leaves = LeafChildren(versionForm);
            if (leaves.Count < 1 || leaves[0].Data.Length != 4)
            {
                throw new DecoderException(DecoderError.UnexpectedForm,
                    "Object template version form " + version + " is missing its param-count chunk.");
            }

            int paramCount = new IffPayloadCursor(leaves[0].Data).ReadInt32Le();
            if (paramCount < 0)
            {
                throw new DecoderException(DecoderError.NegativeCount, "Param count is negative: " + paramCount + ".");
            }
            int availableParams = leaves.Count - 1;
            // Forged-count guard: paramCount cannot exceed the param chunks actually present.
            if (paramCount > availableParams)
            {
                throw new DecoderException(DecoderError.CountExceedsCap,
                    "Param count " + paramCount + " exceeds the " + availableParams + " param chunk(s) present.");
            }

            for (int i = 0; i < paramCount; i++)
            {
                var paramLeaf = leaves[i + 1];
                var cursor = new IffPayloadCursor(paramLeaf.Data);
                string name = cursor.ReadCString(Encoding.ASCII);
                byte[] valueBytes = cursor.Remaining > 0 ? cursor.ReadBytes(cursor.Remaining) : new byte[0];
                fields.Add(new ObjectTemplateField(name, ToHex(valueBytes), "local"));
            }

            return new ObjectTemplateView(rootType, baseTemplate, version, fields);
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // The version form is the first child container whose 4-char sub-type tag is all digits
        // (e.g. "0000"); the class-hierarchy parent forms carry letter tags and are NOT expanded here.
        private static IffContainerChunk FindVersionForm(IffContainerChunk parent)
        {
            foreach (var child in parent.Children)
            {
                if (child is IffContainerChunk c && IsAllDigits(c.SubTypeId)) return c;
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

        private static IffContainerChunk FindContainerChild(IffContainerChunk parent, string subTypeId)
        {
            foreach (var child in parent.Children)
            {
                if (child is IffContainerChunk c && c.SubTypeId == subTypeId) return c;
            }
            return null;
        }

        private static IffLeafChunk FirstLeaf(IffContainerChunk parent)
        {
            foreach (var child in parent.Children)
            {
                if (child is IffLeafChunk leaf) return leaf;
            }
            return null;
        }

        private static List<IffLeafChunk> LeafChildren(IffContainerChunk parent)
        {
            var leaves = new List<IffLeafChunk>();
            foreach (var child in parent.Children)
            {
                if (child is IffLeafChunk leaf) leaves.Add(leaf);
            }
            return leaves;
        }
    }
}
