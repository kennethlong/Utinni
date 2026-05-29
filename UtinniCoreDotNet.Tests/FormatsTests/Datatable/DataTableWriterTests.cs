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
    /// Coverage for the DTII writer (Task 2 of 09-01): DataTableWriter.Serialize via IffWriter.Write.
    /// Validates byte-exact round-trip on canonical fixtures, per-DT_* serialize fidelity, and the
    /// CF-04 / SC4 structural property (EditCellValue → RestoreState-Undo → bytes EXACTLY equal
    /// loaded) via the new CaptureState/RestoreState revert path (iter-2 item 3).
    /// </summary>
    public class DataTableWriterTests
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

        private static byte[] Serialize(DataTableDocument d)
        {
            return new DataTableWriter(d.Mutable).Serialize();
        }

        // ── Round-trip byte-exact on canonical fixtures ──────────────────────

        [Fact]
        public void RoundTrip_V0Minimal_ByteExact()
        {
            byte[] original = DatatableFixtures.BuildV0Minimal();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_V1Minimal_ByteExact()
        {
            byte[] original = DatatableFixtures.BuildV1Minimal();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_V1AllTypes_ByteExact()
        {
            byte[] original = DatatableFixtures.BuildV1AllTypes();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_V1WithDefaultsAndEnums_ByteExact()
        {
            byte[] original = DatatableFixtures.BuildV1WithDefaultsAndEnums();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_V1WithComment_ByteExact()
        {
            byte[] original = DatatableFixtures.BuildV1WithComment();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_V1CombatDataTableLike_ByteExact()
        {
            byte[] original = DatatableFixtures.BuildV1CombatDataTableLike();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_V1EmptyTable_ByteExact()
        {
            byte[] original = DatatableFixtures.BuildV1EmptyTable();
            Assert.Equal(original, Serialize(Load(original)));
        }

        // ── Per-DT_* serialize fidelity (edit then re-read matches) ──────────

        [Fact]
        public void Serialize_EditedIntCell_ReReadsNewValue()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1Minimal());
            d.Mutable.Rows[0].Cells[0].Value = DataTableCellValue.FromInt(999);
            byte[] bytes = Serialize(d);

            DataTableDocument reloaded = Load(bytes);
            var v = Assert.IsType<DataTableCellValue.IntCellValue>(reloaded.Mutable.Rows[0].Cells[0].Value);
            Assert.Equal(999, v.Value);
        }

        [Fact]
        public void Serialize_EditedStringCell_ReReadsNewValue()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1Minimal());
            d.Mutable.Rows[0].Cells[1].Value = DataTableCellValue.FromString("edited");
            byte[] bytes = Serialize(d);

            DataTableDocument reloaded = Load(bytes);
            var v = Assert.IsType<DataTableCellValue.StringCellValue>(reloaded.Mutable.Rows[0].Cells[1].Value);
            Assert.Equal("edited", v.Value);
        }

        // ── CF-04 / SC4 structural property (item 3) ─────────────────────────

        [Fact]
        public void EditThenRestoreState_BytesExactlyEqualLoaded()
        {
            byte[] original = DatatableFixtures.BuildV1AllTypes();
            DataTableDocument d = Load(original);

            MutableDataTableCell cell = d.Mutable.Rows[0].Cells[0];
            MutableDataTableCell.CellState snapshot = cell.CaptureState();

            // Edit the cell (dirties it + nulls the original slice).
            cell.Value = DataTableCellValue.FromInt(123456);
            Assert.True(cell.IsDirty);

            // Undo via RestoreState — re-attaches the EXACT original byte slice + clean state.
            cell.RestoreState(snapshot);
            Assert.False(cell.IsDirty);

            // The serialized bytes must equal the loaded bytes byte-for-byte.
            Assert.Equal(original, Serialize(d));
        }

        [Fact]
        public void EditThenValueSetBack_MayDifferFromLoad_DocumentsWhyRestoreStateExists()
        {
            // The NEGATIVE case (item 3): reverting via a SECOND Value-set (not RestoreState) leaves
            // the cell dirty with a nulled original slice, so it reserializes fresh. For a float cell
            // a fresh re-emit is bit-identical here, but the cell is NOT clean — proving the Value
            // setter alone cannot restore the verbatim-slice contract that RestoreState provides.
            byte[] original = DatatableFixtures.BuildV1AllTypes();
            DataTableDocument d = Load(original);

            MutableDataTableCell cell = d.Mutable.Rows[0].Cells[0];
            int loadedValue = ((DataTableCellValue.IntCellValue)cell.Value).Value;

            cell.Value = DataTableCellValue.FromInt(loadedValue + 1);
            cell.Value = DataTableCellValue.FromInt(loadedValue); // "set back" revert

            // Value matches, but the cell is dirty (slice was nulled) — RestoreState is required to
            // restore clean verbatim-slice state.
            Assert.True(cell.IsDirty);
            Assert.Equal(loadedValue, ((DataTableCellValue.IntCellValue)cell.Value).Value);
        }

        [Fact]
        public void NoOpValueSet_PreservesCleanStateAndRoundTrip()
        {
            byte[] original = DatatableFixtures.BuildV1Minimal();
            DataTableDocument d = Load(original);

            MutableDataTableCell cell = d.Mutable.Rows[0].Cells[0];
            int loaded = ((DataTableCellValue.IntCellValue)cell.Value).Value;

            // Setting the SAME value is a no-op: preserves clean state + original slice.
            cell.Value = DataTableCellValue.FromInt(loaded);
            Assert.False(cell.IsDirty);
            Assert.Equal(original, Serialize(d));
        }

        // ── Composition assertion (writer delegates to IffWriter) ────────────

        [Fact]
        public void BuildMutableIff_ProducesFormDtiiRoot()
        {
            DataTableDocument d = Load(DatatableFixtures.BuildV1Minimal());
            MutableIffDocument mIff = new DataTableWriter(d.Mutable).BuildMutableIff();
            Assert.Equal("FORM", mIff.Root.TypeId);
            Assert.Equal("DTII", mIff.Root.SubTypeId);
        }

        [Fact]
        public void Serialize_OutputReparsesThroughIffReader()
        {
            byte[] bytes = Serialize(Load(DatatableFixtures.BuildV1AllTypes()));
            using (var ms = new MemoryStream(bytes))
            {
                IffDocument doc = IffReader.Read(ms); // must not throw
                Assert.NotNull(doc.Root);
            }
        }
    }
}
