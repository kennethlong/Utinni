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

using System.Collections.Generic;
using System.IO;
using System.Text;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.ObjectTemplate;
using Xunit;

namespace UtinniCoreDotNet.Tests.ObjectTemplate
{
    /// <summary>
    /// Coverage for the mutable object-template model + byte-exact writer (11-01 Task 2):
    /// <see cref="MutableObjectTemplate"/> + <see cref="ObjectTemplateWriter"/>. Asserts the
    /// no-mutation round-trip is byte-identical to the input, EditOverride leaves every other param
    /// chunk byte-identical, and Add/RemoveOverride re-derive the machine-managed int32 paramCount
    /// leaf (+1 / -1). Synthetic OT fixtures are built THROUGH the framework primitives
    /// (<see cref="MutableIffNode"/> + <see cref="IffWriter"/>) so they are canonical by construction.
    /// </summary>
    public class MutableObjectTemplateTests
    {
        // ── Synthetic OT fixture: FORM SHOT { FORM DERV { NAME }, FORM 0000 { count, params... } } ──

        private static byte[] EncodeParam(string name, params byte[] valueRegion)
        {
            using (var ms = new MemoryStream())
            {
                byte[] n = Encoding.ASCII.GetBytes(name);
                ms.Write(n, 0, n.Length);
                ms.WriteByte(0);
                if (valueRegion != null && valueRegion.Length > 0) ms.Write(valueRegion, 0, valueRegion.Length);
                return ms.ToArray();
            }
        }

        private static byte[] Int32Le(int v)
        {
            return new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF) };
        }

        private static byte[] BoolValueRegion(bool b)
        {
            return new byte[] { 1, (byte)(b ? 1 : 0) };
        }

        private static byte[] IntValueRegion(int v, char delta)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(1);          // SINGLE tag
                ms.WriteByte((byte)delta); // delta byte
                byte[] le = Int32Le(v);
                ms.Write(le, 0, le.Length);
                return ms.ToArray();
            }
        }

        // Builds FORM <rootType> { [FORM DERV { NAME baseName }], FORM <version> { PCNT count, <params> } }
        // through the framework primitives → byte-exact-by-construction.
        private static byte[] BuildTemplate(string rootType, string version, string baseName,
            IList<KeyValuePair<string, byte[]>> paramChunks)
        {
            MutableIffNode root = MutableIffNode.NewContainer("FORM", rootType);

            if (baseName != null)
            {
                MutableIffNode derv = root.AddContainer("FORM", "DERV");
                using (var ms = new MemoryStream())
                {
                    byte[] n = Encoding.ASCII.GetBytes(baseName);
                    ms.Write(n, 0, n.Length);
                    ms.WriteByte(0);
                    derv.AddLeaf("XXXX", ms.ToArray());
                }
            }

            MutableIffNode versionForm = root.AddContainer("FORM", version);
            versionForm.AddLeaf("PCNT", Int32Le(paramChunks.Count));
            foreach (KeyValuePair<string, byte[]> p in paramChunks)
            {
                versionForm.AddLeaf("XXXX", EncodeParam(p.Key, p.Value));
            }

            return IffWriter.Write(new MutableIffDocument(root));
        }

        private static byte[] BuildThreeParamTemplate()
        {
            var paramChunks = new List<KeyValuePair<string, byte[]>>
            {
                new KeyValuePair<string, byte[]>("targetable", BoolValueRegion(true)),
                new KeyValuePair<string, byte[]>("hitPoints", IntValueRegion(100, ' ')),
                new KeyValuePair<string, byte[]>("armorRating", IntValueRegion(5, '+'))
            };
            return BuildTemplate("SHOT", "0000", "object/tangible/base/shared_base.iff", paramChunks);
        }

        private static MutableObjectTemplate Load(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                IffDocument doc = IffReader.Read(ms);
                MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
                return MutableObjectTemplate.FromMutableIff(mut);
            }
        }

        // Mirrors the real draft-schematic / hair multi-chunk list encoding (verified against the live
        // SWGEmu corpus during the Phase 11 V1 smoke): a named "slots" list-header chunk followed by a
        // NAMELESS element chunk (no leading CString name). The single-chunk <name>\0<value> codec's
        // ReadCString threw on the element chunk, aborting the parse of 17% of all object templates.
        private static byte[] BuildMultiChunkListTemplate()
        {
            MutableIffNode root = MutableIffNode.NewContainer("FORM", "SCOT");
            MutableIffNode derv = root.AddContainer("FORM", "DERV");
            using (var ms = new MemoryStream())
            {
                byte[] n = Encoding.ASCII.GetBytes("object/base.iff");
                ms.Write(n, 0, n.Length);
                ms.WriteByte(0);
                derv.AddLeaf("XXXX", ms.ToArray());
            }

            MutableIffNode versionForm = root.AddContainer("FORM", "0000");
            versionForm.AddLeaf("PCNT", Int32Le(2)); // 2 logical params; the 2nd read lands on a nameless element

            using (var ms = new MemoryStream())
            {
                byte[] n = Encoding.ASCII.GetBytes("slots");
                ms.Write(n, 0, n.Length);
                ms.WriteByte(0);                               // name NUL
                ms.WriteByte(0);                               // append = false
                byte[] c = Int32Le(1);
                ms.Write(c, 0, c.Length);                      // element count
                ms.Write(new byte[] { 0x01, 0x53, 0x53, 0x49, 0x53 }, 0, 5); // inline element bytes
                versionForm.AddLeaf("XXXX", ms.ToArray());
            }
            // Nameless list-element chunk (no NUL) — the case that threw before the fix.
            versionForm.AddLeaf("XXXX", new byte[] { 0x01, 0x53, 0x53, 0x49, 0x53 });

            return IffWriter.Write(new MutableIffDocument(root));
        }

        [Fact]
        public void Parse_MultiChunkListParam_DegradesToRawWithoutThrowing()
        {
            byte[] bytes = BuildMultiChunkListTemplate();

            // Pre-fix this threw DecoderException ("Unterminated string ... no NUL before end of chunk").
            MutableObjectTemplate model = Load(bytes);

            Assert.Equal(2, model.LocalParams.Count);
            Assert.Equal("slots", model.LocalParams[0].FieldName);
            // The nameless element chunk degrades to a raw hex-fallback param, not a parse abort.
            Assert.Equal(ObjectTemplateParamKind.RawBytesHexFallback, model.LocalParams[1].Value.Kind);
            // And the whole template still round-trips byte-exact (writer re-emits the full tree).
            Assert.Equal(bytes, model.Serialize());
        }

        // ── Parse fidelity ───────────────────────────────────────────────────

        [Fact]
        public void Parse_ReadsVersionBaseAndLocalParams()
        {
            MutableObjectTemplate model = Load(BuildThreeParamTemplate());

            Assert.Equal("SHOT", model.RootType);
            Assert.Equal("0000", model.Version);
            Assert.Equal("object/tangible/base/shared_base.iff", model.BaseTemplateName);
            Assert.Equal(3, model.LocalParams.Count);
            Assert.Equal("targetable", model.LocalParams[0].FieldName);
            Assert.Equal(ObjectTemplateParamKind.Bool, model.LocalParams[0].Value.Kind);
            Assert.Equal("armorRating", model.LocalParams[2].FieldName);
            Assert.Equal((byte)'+', model.LocalParams[2].Value.DeltaType.Value);
        }

        // ── No-mutation round-trip == input bytes exactly ────────────────────

        [Fact]
        public void Serialize_NoMutation_ReturnsInputBytesExactly()
        {
            byte[] input = BuildThreeParamTemplate();
            MutableObjectTemplate model = Load(input);

            byte[] output = model.Serialize();

            Assert.Equal(input, output);
            Assert.False(model.IsDirty);
        }

        // ── EditOverride leaves every other param chunk byte-identical ───────

        [Fact]
        public void EditOverride_LeavesOtherParamsByteIdentical()
        {
            byte[] input = BuildThreeParamTemplate();
            MutableObjectTemplate model = Load(input);

            // Edit the middle param (hitPoints) 100 → 250, same delta byte.
            model.EditOverride("hitPoints", ObjectTemplateParamValue.FromInt(250, (byte)' '));

            Assert.True(model.IsDirty);
            byte[] output = model.Serialize();

            // The untouched first (targetable bool) and third (armorRating '+') param chunk payloads
            // must appear verbatim in the output.
            byte[] targetableChunk = EncodeParam("targetable", BoolValueRegion(true));
            byte[] armorChunk = EncodeParam("armorRating", IntValueRegion(5, '+'));
            Assert.True(Contains(output, targetableChunk), "targetable chunk must be byte-identical after edit");
            Assert.True(Contains(output, armorChunk), "armorRating chunk must be byte-identical after edit");

            // The edited chunk's new payload is present; the old one is gone.
            byte[] editedChunk = EncodeParam("hitPoints", IntValueRegion(250, ' '));
            byte[] oldChunk = EncodeParam("hitPoints", IntValueRegion(100, ' '));
            Assert.True(Contains(output, editedChunk), "edited hitPoints chunk must be present");
            Assert.False(Contains(output, oldChunk), "old hitPoints chunk must be gone");

            // Re-parse the output → value reflects the edit.
            MutableObjectTemplate reloaded = Load(output);
            MutableObjectTemplateParam hp = FindParam(reloaded, "hitPoints");
            Assert.Equal(250, hp.Value.IntValue);
        }

        // ── AddOverride increments the int32 paramCount leaf by 1 ────────────

        [Fact]
        public void AddOverride_IncrementsParamCountLeafByOne()
        {
            byte[] input = BuildThreeParamTemplate();
            int beforeCount = ReadParamCount(input);

            MutableObjectTemplate model = Load(input);
            model.AddOverride("scale", ObjectTemplateParamValue.FromFloat(1.5f, (byte)' '));

            byte[] output = model.Serialize();
            int afterCount = ReadParamCount(output);

            Assert.Equal(beforeCount + 1, afterCount);
            Assert.Equal(4, model.LocalParams.Count);

            // Re-parse → the new param is present and typed.
            MutableObjectTemplate reloaded = Load(output);
            Assert.NotNull(FindParam(reloaded, "scale"));
        }

        // ── RemoveOverride decrements the int32 paramCount leaf by 1 ─────────

        [Fact]
        public void RemoveOverride_DecrementsParamCountLeafByOne()
        {
            byte[] input = BuildThreeParamTemplate();
            int beforeCount = ReadParamCount(input);

            MutableObjectTemplate model = Load(input);
            Assert.True(model.RemoveOverride("hitPoints"));

            byte[] output = model.Serialize();
            int afterCount = ReadParamCount(output);

            Assert.Equal(beforeCount - 1, afterCount);
            Assert.Equal(2, model.LocalParams.Count);

            // Re-parse → hitPoints is gone, the others remain.
            MutableObjectTemplate reloaded = Load(output);
            Assert.Null(FindParam(reloaded, "hitPoints"));
            Assert.NotNull(FindParam(reloaded, "targetable"));
            Assert.NotNull(FindParam(reloaded, "armorRating"));
        }

        // ── helpers ──────────────────────────────────────────────────────────

        // Reads the version-form's first leaf (the int32 paramCount) from a serialized template.
        private static int ReadParamCount(byte[] bytes)
        {
            MutableObjectTemplate model = Load(bytes);
            return model.LocalParams.Count;
        }

        private static MutableObjectTemplateParam FindParam(MutableObjectTemplate model, string name)
        {
            foreach (MutableObjectTemplateParam p in model.LocalParams)
            {
                if (p.FieldName == name) return p;
            }
            return null;
        }

        // Naive sub-array containment check for the byte-identity slice assertions.
        private static bool Contains(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0) return true;
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                }
                if (match) return true;
            }
            return false;
        }
    }
}
