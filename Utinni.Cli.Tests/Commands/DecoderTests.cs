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

        // ── helper ──────────────────────────────────────────────────────────

        private static void WithTempIff(byte[] bytes, Action<string> body)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "utinni-decode-" + Guid.NewGuid().ToString("N") + ".iff");
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
