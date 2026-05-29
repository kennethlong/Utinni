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

using System.IO;
using Xunit;
using UtinniCoreDotNet.Formats.Datatable;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Tests.FormatsTests.Datatable
{
    /// <summary>
    /// Coverage for the typed DTII reader (Task 2 of 09-01): DataTableDocument.FromIff.
    /// Validates V0000 + V0001 parse, per-DT_* cell read, DT_Comment preservation + zero-byte ROWS,
    /// DT_HashString int storage (Pitfall 4), cell-count sanity cap, missing-chunk + bad-version
    /// errors.
    /// </summary>
    public class DataTableDocumentTests
    {
        private static DataTableDocument Load(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                IffDocument doc = IffReader.Read(ms);
                MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
                return DataTableDocument.FromIff(mut);
            }
        }

        [Fact]
        public void FromIff_V0Minimal_ParsesColumnsAndRows()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV0Minimal());
            Assert.Equal("0000", d.Mutable.Version);
            Assert.Equal(2, d.Mutable.Columns.Count);
            Assert.Equal(2, d.Mutable.Rows.Count);
        }

        [Fact]
        public void FromIff_V0Minimal_IntAndStringValues()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV0Minimal());
            var id = Assert.IsType<DataTableCellValue.IntCellValue>(d.Mutable.Rows[0].Cells[0].Value);
            var name = Assert.IsType<DataTableCellValue.StringCellValue>(d.Mutable.Rows[0].Cells[1].Value);
            Assert.Equal(1, id.Value);
            Assert.Equal("alpha", name.Value);
        }

        [Fact]
        public void FromIff_V1Minimal_ParsesColumnsAndRows()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1Minimal());
            Assert.Equal("0001", d.Mutable.Version);
            Assert.Equal(2, d.Mutable.Columns.Count);
            Assert.Equal(2, d.Mutable.Rows.Count);
        }

        [Fact]
        public void FromIff_V1Minimal_ColumnNamesAndTypes()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1Minimal());
            Assert.Equal("id", d.Mutable.Columns[0].Name);
            Assert.Equal(DataType.Int, d.Mutable.Columns[0].ColumnType.Type);
            Assert.Equal("name", d.Mutable.Columns[1].Name);
            Assert.Equal(DataType.String, d.Mutable.Columns[1].ColumnType.Type);
        }

        [Fact]
        public void FromIff_V1AllTypes_IntColumnReadsInt()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1AllTypes());
            var v = Assert.IsType<DataTableCellValue.IntCellValue>(d.Mutable.Rows[0].Cells[0].Value);
            Assert.Equal(7, v.Value);
        }

        [Fact]
        public void FromIff_V1AllTypes_FloatColumnReadsFloat()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1AllTypes());
            var v = Assert.IsType<DataTableCellValue.FloatCellValue>(d.Mutable.Rows[0].Cells[1].Value);
            Assert.Equal(3.5f, v.Value);
        }

        [Fact]
        public void FromIff_V1AllTypes_StringColumnReadsString()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1AllTypes());
            var v = Assert.IsType<DataTableCellValue.StringCellValue>(d.Mutable.Rows[0].Cells[2].Value);
            Assert.Equal("hello", v.Value);
        }

        [Fact]
        public void FromIff_V1AllTypes_HashStringStoredAsInt()
        {
            // Pitfall 4: a DT_HashString column stores the int32 CRC on disk, not the source string.
            DataTableDocument d = Load(DatatableFixtures.BuildV1AllTypes());
            DataTableColumnType ct = d.Mutable.Columns[3].ColumnType;
            Assert.Equal(DataType.HashString, ct.Type);
            Assert.Equal(DataType.Int, ct.BasicType);
            var v = Assert.IsType<DataTableCellValue.IntCellValue>(d.Mutable.Rows[0].Cells[3].Value);
            Assert.Equal(unchecked((int)DataTableHashCrc.Compute("creature/foo")), v.Value);
        }

        [Fact]
        public void FromIff_V1AllTypes_BoolEnumBitVectorStoredAsInt()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1AllTypes());
            Assert.IsType<DataTableCellValue.IntCellValue>(d.Mutable.Rows[0].Cells[4].Value); // bool
            Assert.IsType<DataTableCellValue.IntCellValue>(d.Mutable.Rows[0].Cells[5].Value); // enum
            Assert.IsType<DataTableCellValue.IntCellValue>(d.Mutable.Rows[0].Cells[6].Value); // bitvector
        }

        [Fact]
        public void FromIff_V1AllTypes_PackedObjVarsStoredAsString()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1AllTypes());
            DataTableColumnType ct = d.Mutable.Columns[7].ColumnType;
            Assert.Equal(DataType.PackedObjVars, ct.Type);
            Assert.Equal(DataType.String, ct.BasicType);
            var v = Assert.IsType<DataTableCellValue.StringCellValue>(d.Mutable.Rows[0].Cells[7].Value);
            Assert.Equal("name|0|val|$|", v.Value);
        }

        [Fact]
        public void FromIff_V1WithComment_CommentColumnPreservedInColsAndType()
        {
            // Item 13: the comment column STAYS in COLS+TYPE; only its ROWS payload is zero bytes.
            DataTableDocument d = Load(DatatableFixtures.BuildV1WithComment());
            Assert.Equal(3, d.Mutable.Columns.Count);
            Assert.Equal("note", d.Mutable.Columns[1].Name);
            Assert.Equal(DataType.Comment, d.Mutable.Columns[1].ColumnType.Type);
        }

        [Fact]
        public void FromIff_V1WithComment_CommentCellHasZeroByteSlice()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1WithComment());
            // The data columns around the comment must still read correctly.
            var id = Assert.IsType<DataTableCellValue.IntCellValue>(d.Mutable.Rows[0].Cells[0].Value);
            var label = Assert.IsType<DataTableCellValue.StringCellValue>(d.Mutable.Rows[0].Cells[2].Value);
            Assert.Equal(1, id.Value);
            Assert.Equal("first", label.Value);
        }

        [Fact]
        public void FromIff_V1EmptyTable_ZeroRows()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1EmptyTable());
            Assert.Empty(d.Mutable.Rows);
            Assert.Equal(2, d.Mutable.Columns.Count);
        }

        [Fact]
        public void FromIff_V1WithDefaultsAndEnums_Parses()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1WithDefaultsAndEnums());
            Assert.Equal(2, d.Mutable.Columns.Count);
            Assert.Equal(DataType.Enum, d.Mutable.Columns[1].ColumnType.Type);
        }

        [Fact]
        public void FromIff_NullDocument_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => DataTableDocument.FromIff(null));
        }

        [Fact]
        public void FromIff_NonDtiiRoot_Throws()
        {
            var root = MutableIffNode.NewContainer("FORM", "WSNP");
            var doc = new MutableIffDocument(root);
            Assert.Throws<DataTableParseException>(() => DataTableDocument.FromIff(doc));
        }

        [Fact]
        public void FromIff_BadVersionForm_Throws()
        {
            var root = MutableIffNode.NewContainer("FORM", "DTII");
            root.AddContainer("FORM", "9999");
            var doc = new MutableIffDocument(root);
            Assert.Throws<DataTableParseException>(() => DataTableDocument.FromIff(doc));
        }

        [Fact]
        public void FromIff_MissingColsChunk_Throws()
        {
            var root = MutableIffNode.NewContainer("FORM", "DTII");
            MutableIffNode ver = root.AddContainer("FORM", "0001");
            ver.AddLeaf("TYPE", new byte[0]);
            ver.AddLeaf("ROWS", new byte[0]);
            var doc = new MutableIffDocument(root);
            Assert.Throws<DataTableParseException>(() => DataTableDocument.FromIff(doc));
        }

        [Fact]
        public void FromIff_OverCapCellCount_Throws()
        {
            // Build a malformed ROWS chunk declaring a colossal row count with a single int column.
            byte[] cols = BuildColsChunkPayload("id");
            byte[] type = BuildTypeChunkV1("i");
            // numRows = int.MaxValue → numRows * 1 col exceeds the 16 M cell cap.
            byte[] rows = Int32Le(int.MaxValue);

            var root = MutableIffNode.NewContainer("FORM", "DTII");
            MutableIffNode ver = root.AddContainer("FORM", "0001");
            ver.AddLeaf("COLS", cols);
            ver.AddLeaf("TYPE", type);
            ver.AddLeaf("ROWS", rows);
            var doc = new MutableIffDocument(root);

            DataTableParseException ex = Assert.Throws<DataTableParseException>(() => DataTableDocument.FromIff(doc));
            Assert.Contains("sanity cap", ex.Message);
        }

        // ── local raw-chunk builders for the malformed-input tests ───────────

        private static byte[] Int32Le(int v)
        {
            return new[]
            {
                (byte)(v & 0xFF),
                (byte)((v >> 8) & 0xFF),
                (byte)((v >> 16) & 0xFF),
                (byte)((v >> 24) & 0xFF)
            };
        }

        private static byte[] BuildColsChunkPayload(params string[] names)
        {
            using (var ms = new MemoryStream())
            {
                byte[] count = Int32Le(names.Length);
                ms.Write(count, 0, 4);
                foreach (string n in names)
                {
                    byte[] ascii = System.Text.Encoding.ASCII.GetBytes(n);
                    ms.Write(ascii, 0, ascii.Length);
                    ms.WriteByte(0);
                }

                return ms.ToArray();
            }
        }

        private static byte[] BuildTypeChunkV1(params string[] specs)
        {
            using (var ms = new MemoryStream())
            {
                foreach (string s in specs)
                {
                    byte[] ascii = System.Text.Encoding.ASCII.GetBytes(s);
                    ms.Write(ascii, 0, ascii.Length);
                    ms.WriteByte(0);
                }

                return ms.ToArray();
            }
        }
    }
}
