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
using Xunit;
using UtinniCoreDotNet.Formats.Datatable;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Tests.FormatsTests.Datatable
{
    /// <summary>
    /// Framework-layer tests for the Plan 09-06 <see cref="CsvCellCoercion"/> per-cell CSV coercion +
    /// import-plan builder (checker B-1 extraction). Covers per-DT_* coercion success + failure, the
    /// DoS sanity caps (T-09-27), and the SC4-at-the-CSV-layer round-trip property (a CSV exported from
    /// the current values re-imports as all-unchanged → byte-exact-on-untouched).
    /// </summary>
    public class CsvCellCoercionTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static MutableDataTableDocument LoadMutable(byte[] bytes)
        {
            IffDocument iff;
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                iff = IffReader.Read(ms);
            }
            MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iff, bytes);
            return DataTableDocument.FromIff(mutableIff).Mutable;
        }

        // Build a CSV header (column names) + rows (each cell's current CSV string form) FROM the
        // document — i.e. the exact bytes Export would write. Re-importing this should yield zero
        // changes (the SC4-at-the-CSV-layer property).
        private static (List<string> header, List<IReadOnlyList<string>> rows) ExportToCsvModel(MutableDataTableDocument doc)
        {
            var header = new List<string>();
            for (int c = 0; c < doc.Columns.Count; c++)
            {
                header.Add(doc.Columns[c].Name);
            }

            var rows = new List<IReadOnlyList<string>>();
            for (int r = 0; r < doc.Rows.Count; r++)
            {
                var cells = new List<string>();
                for (int c = 0; c < doc.Columns.Count; c++)
                {
                    cells.Add(CsvCellCoercion.SerializeCellToCsv(doc.Rows[r].Cells[c], doc.Columns[c].ColumnType));
                }
                rows.Add(cells);
            }

            return (header, rows);
        }

        private static IReadOnlyList<IReadOnlyList<string>> Rows(params string[][] rows)
        {
            var list = new List<IReadOnlyList<string>>();
            foreach (var r in rows) list.Add(new List<string>(r));
            return list;
        }

        // ── Round-trip / unchanged ───────────────────────────────────────────

        [Fact]
        public void PlanImport_AllCellsMatchCurrent_ReturnsZeroChanges()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1AllTypes());
            var (header, rows) = ExportToCsvModel(doc);

            CsvImportPlan plan = CsvCellCoercion.PlanImport(doc, header, rows);

            Assert.Empty(plan.Changes);
            Assert.Empty(plan.Invalid);
            Assert.Equal(doc.Rows.Count * doc.Columns.Count, plan.Unchanged.Count);
        }

        [Fact]
        public void PlanImport_OneCellDiffers_ReturnsOneChange_OthersUnchanged()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1AllTypes());
            var (header, rows) = ExportToCsvModel(doc);

            // Flip the anInt column (index 0) of row 0 to a new value.
            ((List<string>)rows[0])[0] = "987654";

            CsvImportPlan plan = CsvCellCoercion.PlanImport(doc, header, rows);

            Assert.Single(plan.Changes);
            Assert.Equal(0, plan.Changes[0].Row);
            Assert.Equal(0, plan.Changes[0].Col);
            Assert.Equal(doc.Rows.Count * doc.Columns.Count - 1, plan.Unchanged.Count);
        }

        // ── Invalid coercions per DT_* ───────────────────────────────────────

        [Fact]
        public void PlanImport_InvalidValueForDtInt_ReturnsInvalid_NotInChanges()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1AllTypes());
            var (header, rows) = ExportToCsvModel(doc);
            ((List<string>)rows[0])[0] = "foo"; // anInt is DT_Int

            CsvImportPlan plan = CsvCellCoercion.PlanImport(doc, header, rows);

            Assert.Single(plan.Invalid);
            Assert.Equal(0, plan.Invalid[0].Col);
            Assert.DoesNotContain(plan.Changes, p => p.Row == 0 && p.Col == 0);
        }

        [Fact]
        public void PlanImport_DtBoolCellWithValueTwo_FlaggedInvalid()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1AllTypes());
            var (header, rows) = ExportToCsvModel(doc);
            // aBool is column index 4 (i,f,s,h,b,...).
            ((List<string>)rows[0])[4] = "2";

            CsvImportPlan plan = CsvCellCoercion.PlanImport(doc, header, rows);

            Assert.Contains(plan.Invalid, e => e.Col == 4);
        }

        [Fact]
        public void PlanImport_DtEnumCellWithUnknownLabel_FlaggedInvalid()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1AllTypes());
            var (header, rows) = ExportToCsvModel(doc);
            // anEnum is column index 5: e(red=1,green=2,blue=3)[red]. "purple" is unknown.
            ((List<string>)rows[0])[5] = "purple";

            CsvImportPlan plan = CsvCellCoercion.PlanImport(doc, header, rows);

            Assert.Contains(plan.Invalid, e => e.Col == 5);
        }

        [Fact]
        public void PlanImport_DtHashStringAlwaysCoerces_NoInvalid()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1AllTypes());
            var (header, rows) = ExportToCsvModel(doc);
            // aHash is column index 3 (DT_HashString). The CSV holds the int32 hash; flip to another
            // valid int32 — DT_HashString basic type is DT_Int, so any whole number coerces.
            ((List<string>)rows[0])[3] = "123";

            CsvImportPlan plan = CsvCellCoercion.PlanImport(doc, header, rows);

            Assert.DoesNotContain(plan.Invalid, e => e.Col == 3);
            Assert.Contains(plan.Changes, p => p.Col == 3);
        }

        // ── DoS sanity caps (T-09-27) ────────────────────────────────────────

        [Fact]
        public void PlanImport_OverRowCap_ThrowsCsvParseException()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var header = new List<string> { "id", "name" };
            var rows = new List<IReadOnlyList<string>>(CsvCellCoercion.MaxRows + 1);
            for (int i = 0; i < CsvCellCoercion.MaxRows + 1; i++)
            {
                rows.Add(new List<string> { "0", "x" });
            }

            Assert.Throws<CsvParseException>(() => CsvCellCoercion.PlanImport(doc, header, rows));
        }

        [Fact]
        public void PlanImport_OverCellSizeCap_ThrowsCsvParseException()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var header = new List<string> { "id", "name" };
            string huge = new string('x', CsvCellCoercion.MaxCellBytes + 1);
            var rows = Rows(new[] { "0", huge });

            Assert.Throws<CsvParseException>(() => CsvCellCoercion.PlanImport(doc, header, (List<IReadOnlyList<string>>)rows));
        }

        // ── Out-of-schema header column ──────────────────────────────────────

        [Fact]
        public void PlanImport_UnknownHeaderColumn_RecordedAsUnmatched_NotFatal()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var header = new List<string> { "id", "name", "ghost" };
            var rows = Rows(new[] { "10", "one", "boo" });

            CsvImportPlan plan = CsvCellCoercion.PlanImport(doc, header, (List<IReadOnlyList<string>>)rows);

            Assert.Contains("ghost", plan.UnmatchedHeaders);
        }

        // ── SerializeCellToCsv round-trips per DT_* ──────────────────────────

        [Fact]
        public void SerializeCellToCsv_AllDtTypes_RoundTripsThroughPlanImport()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1AllTypes());
            var (header, rows) = ExportToCsvModel(doc);

            // Each cell, exported to CSV and re-imported with no edit, must be Unchanged (no Changes,
            // no Invalid) — the SC4-at-the-CSV-layer property for every DT_* in the all-types fixture.
            CsvImportPlan plan = CsvCellCoercion.PlanImport(doc, header, rows);

            Assert.Empty(plan.Changes);
            Assert.Empty(plan.Invalid);
            Assert.Equal(doc.Rows.Count * doc.Columns.Count, plan.Unchanged.Count);
        }

        [Fact]
        public void SerializeCellToCsv_FloatCell_RoundTrippableForm()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1AllTypes());
            // aFloat is column index 1.
            string csv = CsvCellCoercion.SerializeCellToCsv(doc.Rows[0].Cells[1], doc.Columns[1].ColumnType);

            // The exported float string re-coerces to a bit-identical FloatCellValue (no Changes).
            var (header, rows) = ExportToCsvModel(doc);
            ((List<string>)rows[0])[1] = csv;
            CsvImportPlan plan = CsvCellCoercion.PlanImport(doc, header, rows);
            Assert.DoesNotContain(plan.Changes, p => p.Col == 1);
        }

        [Fact]
        public void PlanImport_ShortCsvRow_LeavesMissingCellsUntouched()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var header = new List<string> { "id", "name" };
            // Row provides only the first column; the second is absent → untouched (not Invalid).
            var rows = Rows(new[] { "10" });

            CsvImportPlan plan = CsvCellCoercion.PlanImport(doc, header, (List<IReadOnlyList<string>>)rows);

            Assert.Empty(plan.Invalid);
            // id "10" matches the current value → Unchanged; name absent → no record at all.
            Assert.DoesNotContain(plan.Changes, p => p.Col == 1);
        }
    }
}
