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
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Tier-2 tests for the per-type structured decoders + the decode-iff CLI verb.
    ///
    /// <para>The IFF inputs are MINIMAL CONTRACT (smoke) fixtures synthesized via
    /// <see cref="IffBuilder"/> — they prove each decoder's chunk-layout contract and the
    /// little-endian / forged-count guards. They are NOT real-loader-layout confidence; the
    /// SWG_SAMPLE_TRE_DIR-gated supplemental tests (added when a real loose-IFF corpus is present)
    /// exercise real layouts.</para>
    /// </summary>
    public class DecoderTests
    {
        // ── DataTable fixture synthesis (DTII -> FORM 0001 -> COLS/TYPE/ROWS) ──

        /// <summary>
        /// A 3-column (id:i, weight:f, name:s) v0001 datatable with two rows:
        /// (1, 1.5, "alpha") and (2, 2.5, "beta"). MINIMAL CONTRACT (smoke) fixture.
        /// </summary>
        private static byte[] BuildDataTable0001()
        {
            byte[] cols = IffBuilder.Concat(
                IffBuilder.Int32Le(3),
                IffBuilder.CString("id"),
                IffBuilder.CString("weight"),
                IffBuilder.CString("name"));

            byte[] type = IffBuilder.Concat(
                IffBuilder.CString("i"),
                IffBuilder.CString("f"),
                IffBuilder.CString("s"));

            byte[] rows = IffBuilder.Concat(
                IffBuilder.Int32Le(2),
                // row 0
                IffBuilder.Int32Le(1), IffBuilder.FloatLe(1.5f), IffBuilder.CString("alpha"),
                // row 1
                IffBuilder.Int32Le(2), IffBuilder.FloatLe(2.5f), IffBuilder.CString("beta"));

            return IffBuilder.Form("DTII",
                IffBuilder.Form("0001",
                    IffBuilder.Leaf("COLS", cols),
                    IffBuilder.Leaf("TYPE", type),
                    IffBuilder.Leaf("ROWS", rows)));
        }

        private static IffDocument Parse(byte[] iff) => IffReader.Read(new MemoryStream(iff));

        // ── StringTable (.stf) fixture synthesis — raw magic+version binary, NOT IFF ──

        /// <summary>
        /// A 2-entry v1 .stf: id 1 = "café" (name "welcome") and id 2 = "hello" (name "greeting").
        /// The accented 'é' exercises the UTF-16LE round-trip. MINIMAL CONTRACT (smoke) fixture.
        /// </summary>
        private static byte[] BuildStf()
        {
            using (var ms = new MemoryStream())
            {
                void U32(int v) { var b = IffBuilder.Int32Le(v); ms.Write(b, 0, b.Length); }
                void Utf16(string s) { var b = Encoding.Unicode.GetBytes(s); ms.Write(b, 0, b.Length); }
                void Ascii(string s) { var b = Encoding.ASCII.GetBytes(s); ms.Write(b, 0, b.Length); }

                U32(0xABCD);          // magic (little-endian: CD AB 00 00)
                ms.WriteByte(1);      // version
                U32(99);              // nextUniqueId (ignored)
                U32(2);               // entry count

                // string table: id, crc, charCount, UTF-16LE text
                U32(1); U32(0); U32("café".Length); Utf16("café");
                U32(2); U32(0); U32("hello".Length); Utf16("hello");

                // name table: id, nameLen, ASCII name
                U32(1); U32("welcome".Length); Ascii("welcome");
                U32(2); U32("greeting".Length); Ascii("greeting");

                return ms.ToArray();
            }
        }

        // ── ObjectTemplate fixture synthesis (FORM type -> DERV -> FORM version -> count + params) ──

        /// <summary>
        /// A FORM SBMK object template deriving from "object/tangible/base.iff" with two local params:
        /// radius (float 1.0) and numberOfPoles (int 4). MINIMAL CONTRACT (smoke) fixture.
        /// </summary>
        private static byte[] BuildObjectTemplate(bool withBase)
        {
            byte[] versionForm = IffBuilder.Form("0000",
                IffBuilder.Leaf("PCNT", IffBuilder.Int32Le(2)),
                IffBuilder.Leaf("PARM", IffBuilder.Concat(IffBuilder.CString("radius"), IffBuilder.FloatLe(1.0f))),
                IffBuilder.Leaf("PARM", IffBuilder.Concat(IffBuilder.CString("numberOfPoles"), IffBuilder.Int32Le(4))));

            if (withBase)
            {
                byte[] derv = IffBuilder.Form("DERV",
                    IffBuilder.Leaf("NAME", IffBuilder.CString("object/tangible/base.iff")));
                return IffBuilder.Form("SBMK", derv, versionForm);
            }
            return IffBuilder.Form("SBMK", versionForm);
        }

        // ── Decoder unit tests ─────────────────────────────────────────────────

        [Fact]
        public void Decode_DataTable0001_ReadsColumnsTypesAndTypedRows()
        {
            DataTableView dt = DataTableDecoder.Decode(Parse(BuildDataTable0001()));

            Assert.Equal("0001", dt.Version);
            Assert.Equal(3, dt.Columns.Count);

            Assert.Equal("id", dt.Columns[0].Name);
            Assert.Equal(DataCellKind.Int, dt.Columns[0].Kind);
            Assert.Equal("weight", dt.Columns[1].Name);
            Assert.Equal(DataCellKind.Float, dt.Columns[1].Kind);
            Assert.Equal("name", dt.Columns[2].Name);
            Assert.Equal(DataCellKind.String, dt.Columns[2].Kind);

            Assert.Equal(2, dt.Rows.Count);
            Assert.Equal(1, (int)dt.Rows[0][0]);
            Assert.Equal(1.5f, (float)dt.Rows[0][1]);
            Assert.Equal("alpha", (string)dt.Rows[0][2]);
            Assert.Equal(2, (int)dt.Rows[1][0]);
            Assert.Equal(2.5f, (float)dt.Rows[1][1]);
            Assert.Equal("beta", (string)dt.Rows[1][2]);
        }

        [Fact]
        public void Decode_DataTable_ReadsNumColsLittleEndian()
        {
            // A single-column table: numCols on disk is the LE bytes 01 00 00 00. Read big-endian
            // those same bytes are 16777216 — which would trip the CountExceedsCap guard, not yield
            // one column. Decoding to exactly ONE column proves the scalar is read little-endian.
            byte[] cols = IffBuilder.Concat(IffBuilder.Int32Le(1), IffBuilder.CString("only"));
            byte[] type = IffBuilder.CString("i");
            byte[] rows = IffBuilder.Concat(IffBuilder.Int32Le(1), IffBuilder.Int32Le(42));
            byte[] iff = IffBuilder.Form("DTII",
                IffBuilder.Form("0000",
                    IffBuilder.Leaf("COLS", cols),
                    // v0000 TYPE is an int32 type-code per column: 0 == DT_Int.
                    IffBuilder.Leaf("TYPE", IffBuilder.Int32Le(0)),
                    IffBuilder.Leaf("ROWS", rows)));

            DataTableView dt = DataTableDecoder.Decode(Parse(iff));

            Assert.Equal(1, dt.Columns.Count); // NOT 16777216
            Assert.Equal("0000", dt.Version);
            Assert.Equal(42, (int)dt.Rows[0][0]);
        }

        [Fact]
        public void Decode_ForgedNumCols_ThrowsDecoderExceptionNotOom()
        {
            // COLS declares int.MaxValue columns but carries no name bytes -> CountExceedsCap.
            byte[] cols = IffBuilder.Int32Le(int.MaxValue);
            byte[] iff = IffBuilder.Form("DTII",
                IffBuilder.Form("0001",
                    IffBuilder.Leaf("COLS", cols),
                    IffBuilder.Leaf("TYPE", new byte[0]),
                    IffBuilder.Leaf("ROWS", IffBuilder.Int32Le(0))));

            var ex = Assert.Throws<DecoderException>(() => DataTableDecoder.Decode(Parse(iff)));
            Assert.Equal(DecoderError.CountExceedsCap, ex.Kind);
        }

        [Fact]
        public void Decode_ForgedNumRows_ThrowsDecoderExceptionNotOom()
        {
            // Valid single int column, but ROWS declares int.MaxValue rows with almost no data ->
            // the division-form guard (numRows > Data.Length / numCols) rejects before allocating.
            byte[] cols = IffBuilder.Concat(IffBuilder.Int32Le(1), IffBuilder.CString("id"));
            byte[] type = IffBuilder.CString("i");
            byte[] rows = IffBuilder.Int32Le(int.MaxValue);
            byte[] iff = IffBuilder.Form("DTII",
                IffBuilder.Form("0001",
                    IffBuilder.Leaf("COLS", cols),
                    IffBuilder.Leaf("TYPE", type),
                    IffBuilder.Leaf("ROWS", rows)));

            var ex = Assert.Throws<DecoderException>(() => DataTableDecoder.Decode(Parse(iff)));
            Assert.Equal(DecoderError.CountExceedsCap, ex.Kind);
        }

        [Fact]
        public void Decode_NonDtiiRoot_ThrowsUnexpectedForm()
        {
            byte[] iff = IffBuilder.Form("WSNP", IffBuilder.Leaf("DATA", new byte[] { 1, 2 }));
            var ex = Assert.Throws<DecoderException>(() => DataTableDecoder.Decode(Parse(iff)));
            Assert.Equal(DecoderError.UnexpectedForm, ex.Kind);
        }

        // ── decode-iff CLI verb tests (full path: File.Exists + IffReader + dispatch + JSON) ──

        [Fact]
        public void DecodeIff_DataTable_EmitsSchemaV1EnvelopeAndExitsZero()
        {
            WithTempIff(BuildDataTable0001(), path =>
            {
                var result = InProcessCliRunner.Run("decode-iff", path);
                Assert.Equal(0, result.ExitCode);

                var root = JToken.Parse(result.Stdout);
                Assert.Equal(1, root["schemaVersion"].Value<int>());
                Assert.Equal("decode-iff", root["command"].Value<string>());
                Assert.Null(root["error"]);

                JToken r = root["result"];
                Assert.Equal("datatable", r["type"].Value<string>());
                Assert.Equal("0001", r["version"].Value<string>());
                Assert.Equal(2, r["rowCount"].Value<int>());
                Assert.Equal(3, ((JArray)r["columns"]).Count);
                Assert.Equal("id", r["columns"][0]["name"].Value<string>());
                // rows is an array of arrays; first cell of first row is the int 1.
                Assert.Equal(1, r["rows"][0][0].Value<int>());
                Assert.Equal("alpha", r["rows"][0][2].Value<string>());
            });
        }

        [Fact]
        public void DecodeIff_MissingFile_ExitsThreeWithFileNotFound()
        {
            var result = InProcessCliRunner.Run("decode-iff", @"Z:\nonexistent\nope.iff");
            Assert.Equal(3, result.ExitCode);

            var root = JToken.Parse(result.Stdout);
            Assert.Equal("decode-iff", root["command"].Value<string>());
            Assert.Equal("FileNotFound", root["error"]["kind"].Value<string>());
        }

        [Fact]
        public void DecodeIff_TruncatedRows_ExitsTwoWithErrorKind()
        {
            // ROWS declares 2 rows of one int column but supplies only one int -> Truncated on read.
            byte[] cols = IffBuilder.Concat(IffBuilder.Int32Le(1), IffBuilder.CString("id"));
            byte[] type = IffBuilder.CString("i");
            byte[] rows = IffBuilder.Concat(IffBuilder.Int32Le(2), IffBuilder.Int32Le(7)); // missing 2nd row
            byte[] iff = IffBuilder.Form("DTII",
                IffBuilder.Form("0001",
                    IffBuilder.Leaf("COLS", cols),
                    IffBuilder.Leaf("TYPE", type),
                    IffBuilder.Leaf("ROWS", rows)));

            WithTempIff(iff, path =>
            {
                var result = InProcessCliRunner.Run("decode-iff", path);
                Assert.Equal(2, result.ExitCode);

                var root = JToken.Parse(result.Stdout);
                Assert.Equal("decode-iff", root["command"].Value<string>());
                Assert.Equal("Truncated", root["error"]["kind"].Value<string>());
            });
        }

        [Fact]
        public void DecodeIff_UnsupportedRoot_ExitsTwoWithUnsupportedForm()
        {
            byte[] iff = IffBuilder.Form("WSNP", IffBuilder.Leaf("DATA", new byte[] { 9, 9 }));
            WithTempIff(iff, path =>
            {
                var result = InProcessCliRunner.Run("decode-iff", path);
                Assert.Equal(2, result.ExitCode);
                var root = JToken.Parse(result.Stdout);
                Assert.Equal("UnsupportedForm", root["error"]["kind"].Value<string>());
            });
        }

        // ── StringTable decoder tests ──────────────────────────────────────────

        [Fact]
        public void Decode_Stf_ReadsIdNameTextEntries_AndNonAsciiRoundTrips()
        {
            StfTable stf = StringTableDecoder.Decode(BuildStf());

            Assert.Equal(1, stf.Version);
            Assert.Equal(2, stf.Entries.Count);

            StfEntry first = stf.Entries[0];
            Assert.Equal(1u, first.Id);
            Assert.Equal("welcome", first.Name);
            Assert.Equal("café", first.Text); // non-ASCII round-trips via UTF-16LE, not mangled to ASCII

            StfEntry second = stf.Entries[1];
            Assert.Equal(2u, second.Id);
            Assert.Equal("greeting", second.Name);
            Assert.Equal("hello", second.Text);
        }

        [Fact]
        public void Decode_Stf_ForgedCount_ThrowsDecoderExceptionNotOom()
        {
            using (var ms = new MemoryStream())
            {
                void U32(int v) { var b = IffBuilder.Int32Le(v); ms.Write(b, 0, b.Length); }
                U32(0xABCD);              // magic
                ms.WriteByte(1);          // version
                U32(0);                   // nextUniqueId
                U32(int.MaxValue);        // forged entry count, no entry data follows
                var ex = Assert.Throws<DecoderException>(() => StringTableDecoder.Decode(ms.ToArray()));
                Assert.Equal(DecoderError.CountExceedsCap, ex.Kind);
            }
        }

        [Fact]
        public void Decode_Stf_BadMagic_ThrowsUnexpectedForm()
        {
            byte[] notStf = { 0x00, 0x11, 0x22, 0x33, 0x01, 0x00, 0x00, 0x00, 0x00 };
            var ex = Assert.Throws<DecoderException>(() => StringTableDecoder.Decode(notStf));
            Assert.Equal(DecoderError.UnexpectedForm, ex.Kind);
        }

        // ── ObjectTemplate decoder tests (bounded posture) ─────────────────────

        [Fact]
        public void Decode_ObjectTemplate_ReadsDeclaredBaseAndLocalFields()
        {
            ObjectTemplateView ot = ObjectTemplateDecoder.Decode(Parse(BuildObjectTemplate(withBase: true)));

            Assert.Equal("SBMK", ot.RootType);
            Assert.Equal("object/tangible/base.iff", ot.BaseTemplate);
            Assert.Equal("0000", ot.Version);

            // Fields = the @base reference row + the two LOCAL params. No inherited fields are
            // materialized from another document (bounded posture): exactly 3 rows, not the base's.
            Assert.Equal(3, ot.Fields.Count);

            ObjectTemplateField baseRow = ot.Fields[0];
            Assert.Equal("@base", baseRow.Name);
            Assert.Equal("object/tangible/base.iff", baseRow.Value);
            Assert.Equal("object/tangible/base.iff", baseRow.InheritedFrom); // base-name, NOT "local"

            ObjectTemplateField radius = ot.Fields[1];
            Assert.Equal("radius", radius.Name);
            Assert.Equal("local", radius.InheritedFrom);
            Assert.Equal("0000803f", radius.Value); // float 1.0f raw bytes, little-endian

            ObjectTemplateField poles = ot.Fields[2];
            Assert.Equal("numberOfPoles", poles.Name);
            Assert.Equal("local", poles.InheritedFrom);
            Assert.Equal("04000000", poles.Value);
        }

        [Fact]
        public void Decode_ObjectTemplate_NoBase_DecodesLocalFieldsOnlyWithoutThrow()
        {
            ObjectTemplateView ot = ObjectTemplateDecoder.Decode(Parse(BuildObjectTemplate(withBase: false)));

            Assert.Null(ot.BaseTemplate);
            Assert.Equal(2, ot.Fields.Count); // only the two local params, no @base row
            Assert.All(ot.Fields, f => Assert.Equal("local", f.InheritedFrom));
        }

        [Fact]
        public void Decode_ObjectTemplate_ForgedParamCount_ThrowsDecoderExceptionNotOom()
        {
            // version form claims int.MaxValue params but supplies only one -> CountExceedsCap.
            byte[] versionForm = IffBuilder.Form("0000",
                IffBuilder.Leaf("PCNT", IffBuilder.Int32Le(int.MaxValue)),
                IffBuilder.Leaf("PARM", IffBuilder.Concat(IffBuilder.CString("x"), IffBuilder.Int32Le(1))));
            byte[] iff = IffBuilder.Form("SBMK", versionForm);

            var ex = Assert.Throws<DecoderException>(() => ObjectTemplateDecoder.Decode(Parse(iff)));
            Assert.Equal(DecoderError.CountExceedsCap, ex.Kind);
        }

        // ── decode-iff dispatch tests for STF + object template ────────────────

        [Fact]
        public void DecodeIff_Stf_EmitsStringtableEnvelope()
        {
            WithTempIff(BuildStf(), path =>
            {
                var result = InProcessCliRunner.Run("decode-iff", path);
                Assert.Equal(0, result.ExitCode);

                var root = JToken.Parse(result.Stdout);
                Assert.Equal("decode-iff", root["command"].Value<string>());
                JToken r = root["result"];
                Assert.Equal("stringtable", r["type"].Value<string>());
                Assert.Equal(2, r["entryCount"].Value<int>());
                Assert.Equal("café", r["entries"][0]["text"].Value<string>());
                Assert.Equal("welcome", r["entries"][0]["name"].Value<string>());
            }, ".stf");
        }

        [Fact]
        public void DecodeIff_ObjectTemplate_EmitsObjecttemplateEnvelope()
        {
            WithTempIff(BuildObjectTemplate(withBase: true), path =>
            {
                var result = InProcessCliRunner.Run("decode-iff", path);
                Assert.Equal(0, result.ExitCode);

                var root = JToken.Parse(result.Stdout);
                JToken r = root["result"];
                Assert.Equal("objecttemplate", r["type"].Value<string>());
                Assert.Equal("SBMK", r["rootType"].Value<string>());
                Assert.Equal("object/tangible/base.iff", r["baseTemplate"].Value<string>());
                Assert.Equal("radius", r["fields"][1]["name"].Value<string>());
            });
        }

        // ── AppearanceSummary (mesh/skeleton/anim) fixtures + tests ────────────

        // MESH: shader count in SPS/CNT; vertex count in VTXA/0003/INFO (2nd int32). Smoke fixture.
        private static byte[] BuildStaticMesh() =>
            IffBuilder.Form("MESH",
                IffBuilder.Form("0005",
                    IffBuilder.Form("SPS",
                        IffBuilder.Form("0001",
                            IffBuilder.Leaf("CNT", IffBuilder.Int32Le(2)), // shader count
                            IffBuilder.Form("VTXA",
                                IffBuilder.Form("0003",
                                    IffBuilder.Leaf("INFO", IffBuilder.Concat(
                                        IffBuilder.Int32Le(0),   // flags
                                        IffBuilder.Int32Le(10)))))))));  // vertex count

        // SKMG INFO: position(vertex) count at offset 16, per-shader(shader) count at offset 28.
        private static byte[] BuildSkeletalMesh()
        {
            byte[] info = IffBuilder.Concat(
                IffBuilder.Int32Le(0), IffBuilder.Int32Le(0), IffBuilder.Int32Le(0), IffBuilder.Int32Le(0), // 0..15
                IffBuilder.Int32Le(8),                                   // 16: position(vertex) count
                IffBuilder.Int32Le(0), IffBuilder.Int32Le(0),            // 20,24
                IffBuilder.Int32Le(3));                                  // 28: per-shader(shader) count
            return IffBuilder.Form("SKMG", IffBuilder.Form("0004", IffBuilder.Leaf("INFO", info)));
        }

        // SKTM: INFO = joint count; NAME = jointCount NUL-terminated names.
        private static byte[] BuildSkeleton() =>
            IffBuilder.Form("SKTM",
                IffBuilder.Form("0002",
                    IffBuilder.Leaf("INFO", IffBuilder.Int32Le(3)),
                    IffBuilder.Leaf("NAME", IffBuilder.Concat(
                        IffBuilder.CString("root"), IffBuilder.CString("spine"), IffBuilder.CString("head")))));

        // KFAT INFO: fps (float) then frame count (int32) at offset 4.
        private static byte[] BuildAnimation() =>
            IffBuilder.Form("KFAT",
                IffBuilder.Form("0003",
                    IffBuilder.Leaf("INFO", IffBuilder.Concat(IffBuilder.FloatLe(30f), IffBuilder.Int32Le(24)))));

        [Fact]
        public void Summarize_StaticMesh_ReadsVertexAndShaderCounts()
        {
            AppearanceInfo a = AppearanceSummary.Summarize(Parse(BuildStaticMesh()));
            Assert.Equal("mesh", a.Kind);
            Assert.Equal(2, a.ShaderCount);
            Assert.Equal(10, a.VertexCount);
        }

        [Fact]
        public void Summarize_SkeletalMesh_ReadsCountsFromInfoOffsets()
        {
            AppearanceInfo a = AppearanceSummary.Summarize(Parse(BuildSkeletalMesh()));
            Assert.Equal("skeletal-mesh", a.Kind);
            Assert.Equal(8, a.VertexCount);
            Assert.Equal(3, a.ShaderCount);
        }

        [Fact]
        public void Summarize_Skeleton_ReadsJointCountAndNames()
        {
            AppearanceInfo a = AppearanceSummary.Summarize(Parse(BuildSkeleton()));
            Assert.Equal("skeleton", a.Kind);
            Assert.Equal(3, a.JointCount);
            Assert.Equal(new[] { "root", "spine", "head" }, a.JointNames);
        }

        [Fact]
        public void Summarize_Animation_ReadsFrameCount()
        {
            AppearanceInfo a = AppearanceSummary.Summarize(Parse(BuildAnimation()));
            Assert.Equal("animation", a.Kind);
            Assert.Equal(24, a.FrameCount);
        }

        [Fact]
        public void Summarize_UnrecognizedRoot_ReturnsNull()
        {
            Assert.Null(AppearanceSummary.Summarize(Parse(IffBuilder.Form("WSNP", IffBuilder.Leaf("DATA", new byte[] { 1, 2 })))));
        }

        [Fact]
        public void Summarize_Skeleton_ForgedJointCount_ThrowsDecoderExceptionNotOom()
        {
            byte[] iff = IffBuilder.Form("SKTM",
                IffBuilder.Form("0002",
                    IffBuilder.Leaf("INFO", IffBuilder.Int32Le(int.MaxValue)),
                    IffBuilder.Leaf("NAME", IffBuilder.CString("a"))));
            var ex = Assert.Throws<DecoderException>(() => AppearanceSummary.Summarize(Parse(iff)));
            Assert.Equal(DecoderError.CountExceedsCap, ex.Kind);
        }

        // ── IffStructureSummary (shader + UI-page) tests ───────────────────────

        // SSHT is the LOCKED shader root FORM tag (verified: StaticShaderTemplate.cpp + real 2d_bloom.sht).
        private static byte[] BuildShaderSsht() =>
            IffBuilder.Form("SSHT", IffBuilder.Form("0000", IffBuilder.Leaf("MATL", new byte[] { 1, 2, 3, 4 })));

        [Fact]
        public void Summarize_Shader_SSHT_RecognizedByLockedRootTag()
        {
            // Shader is recognized by its locked root FORM tag SSHT (NOT a path hint, NOT any-FORM).
            StructureInfo s = IffStructureSummary.Summarize(Parse(BuildShaderSsht()), "shader/2d_bloom.sht");
            Assert.Equal("shader", s.RecognizedAs);
            Assert.Equal("SSHT", s.RootTag);
            Assert.Equal(1, s.ChildCount);
            Assert.Equal(new[] { "0000" }, s.ChildTags);
        }

        // UI page: SWG .gui files are TEXT (not IFF, no FORM tag) — recognized by the .gui extension /
        // ui/ path hint per the plan's path/ext clause. This test LOCKS the recognition convention.
        [Fact]
        public void Summarize_UiPage_DotGuiOrUiPath_RecognizedByPathHint()
        {
            Assert.True(IffStructureSummary.IsUiPagePath("ui/window_login.gui"));
            Assert.True(IffStructureSummary.IsUiPagePath("foo.GUI"));
            Assert.True(IffStructureSummary.IsUiPagePath("ui/somepage"));
            Assert.False(IffStructureSummary.IsUiPagePath("datatables/appearance/x.iff"));

            // The summarizer classifies via the hint (any FORM doc + a .gui hint -> ui-page).
            StructureInfo s = IffStructureSummary.Summarize(
                Parse(IffBuilder.Form("WSNP", IffBuilder.Leaf("DATA", new byte[] { 1, 2 }))), "ui/window_login.gui");
            Assert.Equal("ui-page", s.RecognizedAs);
        }

        [Fact]
        public void Summarize_UnrecognizedFormNoHint_ReturnsEmptyRecognizedAs_NoThrow()
        {
            // The any-FORM fallback: a non-shader FORM with no UI-page hint returns RootTag + childcount
            // but RecognizedAs == "" (UI hides the structured view) and does NOT throw.
            StructureInfo s = IffStructureSummary.Summarize(
                Parse(IffBuilder.Form("WSNP", IffBuilder.Leaf("DATA", new byte[] { 1, 2 }))), "snapshot/world.ws");
            Assert.Equal("", s.RecognizedAs);
            Assert.Equal("WSNP", s.RootTag);
            Assert.Equal(1, s.ChildCount);
        }

        // ── decode-iff dispatch for mesh family + shader + UI-page text ────────

        [Fact]
        public void DecodeIff_SkeletalMesh_EmitsAppearanceEnvelope()
        {
            WithTempIff(BuildSkeletalMesh(), path =>
            {
                var result = InProcessCliRunner.Run("decode-iff", path);
                Assert.Equal(0, result.ExitCode);
                JToken r = JToken.Parse(result.Stdout)["result"];
                Assert.Equal("appearance", r["type"].Value<string>());
                Assert.Equal("skeletal-mesh", r["kind"].Value<string>());
                Assert.Equal(8, r["vertexCount"].Value<int>());
                Assert.Equal(3, r["shaderCount"].Value<int>());
            });
        }

        [Fact]
        public void DecodeIff_Shader_EmitsStructureEnvelopeRecognizedAsShader()
        {
            WithTempIff(BuildShaderSsht(), path =>
            {
                var result = InProcessCliRunner.Run("decode-iff", path);
                Assert.Equal(0, result.ExitCode);
                JToken r = JToken.Parse(result.Stdout)["result"];
                Assert.Equal("structure", r["type"].Value<string>());
                Assert.Equal("shader", r["recognizedAs"].Value<string>());
                Assert.Equal("SSHT", r["rootTag"].Value<string>());
            }, ".sht");
        }

        [Fact]
        public void DecodeIff_UiPageTextGui_EmitsUiPageEnvelope()
        {
            byte[] guiText = Encoding.ASCII.GetBytes("<Page name='login'>...</Page>\n"); // text, not IFF
            WithTempIff(guiText, path =>
            {
                var result = InProcessCliRunner.Run("decode-iff", path);
                Assert.Equal(0, result.ExitCode);
                JToken r = JToken.Parse(result.Stdout)["result"];
                Assert.Equal("ui-page", r["type"].Value<string>());
                Assert.Equal("ui-page", r["recognizedAs"].Value<string>());
                Assert.True(r["isText"].Value<bool>());
            }, ".gui");
        }

        // ── SUPPLEMENTAL env-gated real-asset test (review LOW / Codex fixture-confidence) ──

        [Fact]
        public void Decode_RealStf_FromLooseIffDir_DecodesEntriesWithText()
        {
            // SUPPLEMENT to the synthesized minimal-contract fixtures: when SWG_LOOSE_IFF_DIR points
            // at a real serverdata-style tree, decode an actual .stf so the StringTableDecoder is
            // exercised against the real on-disk layout. Skips cleanly (no-op) when the dir is unset,
            // so CI passes on the synthesized fixtures alone.
            if (!FixturePath.HasLooseIffDir()) return;

            string stf = Directory
                .EnumerateFiles(FixturePath.LooseIffDir(), "*.stf", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (stf == null) return; // dir present but no .stf found — skip cleanly.

            StfTable table = StringTableDecoder.Decode(File.ReadAllBytes(stf));
            Assert.NotEmpty(table.Entries);
            Assert.All(table.Entries, e => Assert.NotNull(e.Text));
        }

        [Fact]
        public void Decode_RealDatatable_FromLooseIffDir_DecodesColumnsAndRows()
        {
            // Real-layout confidence for DataTableDecoder over the IffReader's pad-tolerant parse
            // (07-04a finding: real SWG datatables are not word-padded). Skips cleanly when unset.
            if (!FixturePath.HasLooseIffDir()) return;

            string datatable = Directory
                .EnumerateFiles(FixturePath.LooseIffDir(), "*.iff", SearchOption.AllDirectories)
                .FirstOrDefault(IsDtiiDatatable);
            if (datatable == null) return; // no datatable .iff found — skip cleanly.

            DataTableView dt = DataTableDecoder.Decode(IffReader.Read(datatable));
            Assert.NotEmpty(dt.Columns);
            Assert.All(dt.Columns, c => Assert.False(string.IsNullOrEmpty(c.Name)));
            Assert.All(dt.Rows, r => Assert.Equal(dt.Columns.Count, r.Length));
        }

        // Cheap header check: a datatable is FORM <len> "DTII" — the sub-type tag at offset 8.
        private static bool IsDtiiDatatable(string path)
        {
            try
            {
                byte[] head = new byte[12];
                using (var fs = File.OpenRead(path))
                {
                    if (fs.Read(head, 0, 12) < 12) return false;
                }
                return head[0] == (byte)'F' && head[1] == (byte)'O' && head[2] == (byte)'R' && head[3] == (byte)'M'
                    && head[8] == (byte)'D' && head[9] == (byte)'T' && head[10] == (byte)'I' && head[11] == (byte)'I';
            }
            catch
            {
                return false;
            }
        }

        // ── helper ──────────────────────────────────────────────────────────

        private static void WithTempIff(byte[] bytes, Action<string> body, string extension = ".iff")
        {
            string path = Path.Combine(Path.GetTempPath(),
                "utinni-decode-" + Guid.NewGuid().ToString("N") + extension);
            File.WriteAllBytes(path, bytes);
            try
            {
                body(path);
            }
            finally
            {
                try { File.Delete(path); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
