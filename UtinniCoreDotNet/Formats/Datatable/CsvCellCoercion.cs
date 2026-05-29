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
// DTII (.tab datatable) CSV/TSV delta-import + export coercion (Phase 9 D-08). Pure-managed BCL-only
// per-cell coercion check, extracted framework-side per checker B-1 (mirrors the Phase 8
// LooseOverridePath / LivePatchValidator framework-side posture) so it is xUnit-testable from CI
// without the TJT WinForms host. The TJT-side DatatableCsvSerializer does the file I/O + RFC-4180
// parse and calls into this helper for the per-cell diff + coercion.

using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.Datatable
{
    /// <summary>
    /// Thrown by <see cref="CsvCellCoercion.PlanImport"/> when a CSV input trips a DoS sanity cap
    /// (T-09-27). The caller surfaces the message in the preview modal's red status before any
    /// allocation explosion.
    /// </summary>
    public sealed class CsvParseException : Exception
    {
        /// <summary>Creates a CSV parse/sanity exception with the given message.</summary>
        public CsvParseException(string message) : base(message)
        {
        }
    }

    /// <summary>A single planned cell change — the imported value differs and coerces cleanly.</summary>
    public sealed class EditCellPatch
    {
        /// <summary>0-based model row index.</summary>
        public int Row { get; }

        /// <summary>0-based model column index.</summary>
        public int Col { get; }

        /// <summary>The typed, mangled value to write into the cell.</summary>
        public DataTableCellValue NewValue { get; }

        /// <summary>Creates a cell patch.</summary>
        public EditCellPatch(int row, int col, DataTableCellValue newValue)
        {
            Row = row;
            Col = col;
            NewValue = newValue;
        }
    }

    /// <summary>A cell whose imported value equals its current value — stays clean (CF-04).</summary>
    public sealed class UnchangedCellMatch
    {
        /// <summary>0-based model row index.</summary>
        public int Row { get; }

        /// <summary>0-based model column index.</summary>
        public int Col { get; }

        /// <summary>Creates an unchanged-cell record.</summary>
        public UnchangedCellMatch(int row, int col)
        {
            Row = row;
            Col = col;
        }
    }

    /// <summary>A CSV cell whose value is type-invalid for its column — skipped, listed in red.</summary>
    public sealed class InvalidCellEntry
    {
        /// <summary>0-based model row index.</summary>
        public int Row { get; }

        /// <summary>0-based model column index.</summary>
        public int Col { get; }

        /// <summary>The raw CSV value that failed coercion.</summary>
        public string CsvValue { get; }

        /// <summary>The column name (for the preview list copy).</summary>
        public string ColumnName { get; }

        /// <summary>A human-readable reason for the failure.</summary>
        public string Reason { get; }

        /// <summary>Creates an invalid-cell record.</summary>
        public InvalidCellEntry(int row, int col, string csvValue, string columnName, string reason)
        {
            Row = row;
            Col = col;
            CsvValue = csvValue;
            ColumnName = columnName;
            Reason = reason;
        }
    }

    /// <summary>
    /// The result of planning a CSV import against a target document: the cells that change, the cells
    /// that stay byte-exact, and the cells that are type-invalid (skipped). Also carries header columns
    /// that did not match any target column (surfaced in the preview modal status copy).
    /// </summary>
    public sealed class CsvImportPlan
    {
        /// <summary>Cells whose imported value differs and coerces cleanly — applied on import.</summary>
        public List<EditCellPatch> Changes { get; } = new List<EditCellPatch>();

        /// <summary>Cells whose imported value equals the current value — stay clean (CF-04).</summary>
        public List<UnchangedCellMatch> Unchanged { get; } = new List<UnchangedCellMatch>();

        /// <summary>CSV cells that are type-invalid for their column — skipped, listed in red.</summary>
        public List<InvalidCellEntry> Invalid { get; } = new List<InvalidCellEntry>();

        /// <summary>Header names from the CSV that matched no column in the target (silently skipped).</summary>
        public List<string> UnmatchedHeaders { get; } = new List<string>();

        /// <summary>The distinct row indices touched by <see cref="Changes"/>.</summary>
        public int RowsTouched
        {
            get
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < Changes.Count; i++) seen.Add(Changes[i].Row);
                return seen.Count;
            }
        }

        /// <summary>The distinct column indices touched by <see cref="Changes"/>.</summary>
        public int ColumnsTouched
        {
            get
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < Changes.Count; i++) seen.Add(Changes[i].Col);
                return seen.Count;
            }
        }
    }

    /// <summary>
    /// Framework-side per-cell CSV coercion + import-plan builder (checker B-1 extraction —
    /// VALIDATION § Wave 0 Gaps lines 938-940). Pure-managed, BCL-only, no WinForms / native /
    /// TJT dependency — xUnit-testable from CI.
    ///
    /// <remarks>
    /// <para><b>Pattern (RESEARCH § Pattern 5, lines 498-519 — CsvImportPlan.Build):</b>
    /// <see cref="PlanImport"/> walks the CSV header to find matching target columns by name, then
    /// for each csvRow × matching column compares the imported string against the current cell's
    /// CSV string form. Equal → <see cref="CsvImportPlan.Unchanged"/> (preserves originalSlice via
    /// CF-04 — the cell stays clean, byte-exact-on-untouched). Different → run
    /// <see cref="DataTableColumnType.TryCoerceCellValue"/>; success queues an
    /// <see cref="EditCellPatch"/>; failure lists an <see cref="InvalidCellEntry"/>.</para>
    ///
    /// <para><b>The byte-exact-on-untouched property (RESEARCH § "What the planner must NOT skip"):</b>
    /// a CSV exported from the current document and re-imported with no value changes produces ZERO
    /// changes (every cell lands in Unchanged), so a subsequent serialize is byte-identical — the
    /// SC4-at-the-CSV-layer guarantee, asserted by the round-trip [Fact].</para>
    ///
    /// <para><b>DoS mitigation (RESEARCH § Security Domain — T-09-27):</b> the row count is capped at
    /// <see cref="MaxRows"/> and per-cell size at <see cref="MaxCellBytes"/> (the same _loadRow buffer
    /// SOE uses per DataTableWriter.cpp:768); over-cap throws <see cref="CsvParseException"/> BEFORE
    /// any per-cell allocation explosion.</para>
    /// </remarks>
    /// </summary>
    public static class CsvCellCoercion
    {
        /// <summary>Row-count sanity cap (T-09-27 DoS mitigation).</summary>
        public const int MaxRows = 100000;

        /// <summary>Per-cell byte-size sanity cap (16 KB; SOE _loadRow buffer, DataTableWriter.cpp:768).</summary>
        public const int MaxCellBytes = 16 * 1024;

        /// <summary>
        /// Plans a CSV import against the target document. For each header entry that matches a target
        /// column by name, each csvRow's value at that column is compared to the current cell's CSV
        /// string form: equal → Unchanged; different + coerces → a change; different + fails → Invalid.
        /// Out-of-schema header columns are recorded in <see cref="CsvImportPlan.UnmatchedHeaders"/>
        /// and otherwise skipped (user-error, not fatal). Throws <see cref="CsvParseException"/> when a
        /// DoS sanity cap is exceeded.
        /// </summary>
        public static CsvImportPlan PlanImport(
            MutableDataTableDocument target,
            IReadOnlyList<string> csvHeader,
            IReadOnlyList<IReadOnlyList<string>> csvRows)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (csvHeader == null) throw new ArgumentNullException("csvHeader");
            if (csvRows == null) throw new ArgumentNullException("csvRows");

            if (csvRows.Count > MaxRows)
            {
                throw new CsvParseException("Row count exceeds sanity cap (100,000)");
            }

            var plan = new CsvImportPlan();

            // Map each CSV header column → target column index (or -1 → unmatched). Done once.
            int[] headerToColumn = new int[csvHeader.Count];
            for (int h = 0; h < csvHeader.Count; h++)
            {
                int colIndex = ResolveColumnByName(target, csvHeader[h]);
                headerToColumn[h] = colIndex;
                if (colIndex < 0)
                {
                    plan.UnmatchedHeaders.Add(csvHeader[h]);
                }
            }

            int rowsToVisit = Math.Min(csvRows.Count, target.Rows.Count);
            for (int r = 0; r < rowsToVisit; r++)
            {
                IReadOnlyList<string> csvRow = csvRows[r];
                MutableDataTableRow modelRow = target.Rows[r];

                for (int h = 0; h < csvHeader.Count; h++)
                {
                    int colIndex = headerToColumn[h];
                    if (colIndex < 0) continue; // unmatched header column
                    if (h >= csvRow.Count) continue; // short CSV row — leave the cell untouched
                    if (colIndex >= modelRow.Cells.Count) continue;

                    string csvValue = csvRow[h] ?? string.Empty;

                    // Per-cell size cap (T-09-27) — measured in chars (~bytes for the ASCII path).
                    if (csvValue.Length > MaxCellBytes)
                    {
                        throw new CsvParseException("Cell size exceeds sanity cap (16 KB)");
                    }

                    MutableDataTableColumn column = target.Columns[colIndex];
                    DataTableColumnType ct = column.ColumnType;
                    MutableDataTableCell cell = modelRow.Cells[colIndex];

                    // Compare the imported string to the current cell's CSV string form. Equal → clean.
                    string currentCsv = cell.Value != null ? cell.Value.ToCsvString(ct) : string.Empty;
                    if (string.Equals(csvValue, currentCsv, StringComparison.Ordinal))
                    {
                        plan.Unchanged.Add(new UnchangedCellMatch(r, colIndex));
                        continue;
                    }

                    // Different value — coerce through the column type (MangleValue + typed build).
                    DataTableCellValue coerced;
                    if (ct.TryCoerceCellValue(csvValue, out coerced))
                    {
                        plan.Changes.Add(new EditCellPatch(r, colIndex, coerced));
                    }
                    else
                    {
                        plan.Invalid.Add(new InvalidCellEntry(
                            r, colIndex, csvValue, column.Name, DescribeCoercionFailure(ct, csvValue)));
                    }
                }
            }

            return plan;
        }

        /// <summary>
        /// Renders a single cell to its CSV string form (symmetric of the coercion check; used by the
        /// TJT-side serializer for export). Delegates to <see cref="DataTableCellValue.ToCsvString"/>.
        /// </summary>
        public static string SerializeCellToCsv(MutableDataTableCell cell, DataTableColumnType ct)
        {
            if (cell == null) throw new ArgumentNullException("cell");
            if (ct == null) throw new ArgumentNullException("ct");
            return cell.Value != null ? cell.Value.ToCsvString(ct) : string.Empty;
        }

        // Resolve a target column by ordinal name match (mirrors MutableDataTableDocument.ResolveColumnIndex
        // for the name case — but we want the name path only here, not the integer-index overload).
        private static int ResolveColumnByName(MutableDataTableDocument target, string name)
        {
            if (name == null) return -1;
            for (int c = 0; c < target.Columns.Count; c++)
            {
                if (string.Equals(target.Columns[c].Name, name, StringComparison.Ordinal))
                {
                    return c;
                }
            }
            return -1;
        }

        // Planner-friendly failure reason. Keyed on the column type so the preview modal shows a
        // human-readable line (e.g. "DT_Int requires a whole number; got 'foo'").
        private static string DescribeCoercionFailure(DataTableColumnType ct, string csvValue)
        {
            string quoted = "'" + csvValue + "'";
            switch (ct.Type)
            {
                case DataType.Int:
                    return "DT_Int requires a whole number; got " + quoted;
                case DataType.Float:
                    return "DT_Float requires a number; got " + quoted;
                case DataType.Bool:
                    return "DT_Bool requires 0 or 1; got " + quoted;
                case DataType.Enum:
                    return "DT_Enum requires a known label; got " + quoted;
                case DataType.BitVector:
                    return "DT_BitVector requires known flag label(s); got " + quoted;
                case DataType.PackedObjVars:
                    return "DT_PackedObjVars requires the name|type|value|…|$| grammar; got " + quoted;
                default:
                    return "Value " + quoted + " is not valid for " + ct.TypeSpec;
            }
        }
    }
}
