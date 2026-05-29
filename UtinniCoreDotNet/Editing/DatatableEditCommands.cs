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
// Factory + concrete commands for every D-01 T4 datatable edit op (Phase 9). The datatable analog
// of UtinniCoreDotNet.Editing.IffEditCommands (IffEditController.cs:175-262). Mirrors that layout —
// public static factory methods returning private nested IDatatableEditCommand implementations, each
// capturing inverse state for Undo. Pure-managed (no UI dependency); xUnit-testable from CI.

using System;
using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Datatable;

namespace UtinniCoreDotNet.Editing
{
    /// <summary>
    /// Factory methods for every D-01 T4 edit command supported by FormDatatableEditor: cell-value
    /// edit, row add/remove/reorder, column add/remove/reorder, column rename, and the D-04 column
    /// type-change cascade. Mirrors the <c>IffEditCommands</c> factory layout
    /// (<c>IffEditController.cs:175-262</c>).
    /// </summary>
    public static class DatatableEditCommands
    {
        /// <summary>
        /// Edit a single cell's value (D-01 T4 cell edit). Do captures the cell's full state via
        /// <c>CaptureState()</c> then sets the new value; UndoOp restores via <c>RestoreState(CellState)</c>
        /// so the original byte slice + dirty/needs-review bits come back byte-exact (iter-2 item 3 — a
        /// plain Value re-set would null the slice and reserialize fresh, risking byte drift).
        /// </summary>
        public static IDatatableEditCommand EditCellValue(MutableDataTableCell cell, DataTableCellValue newValue)
        {
            if (cell == null) throw new ArgumentNullException("cell");
            if (newValue == null) throw new ArgumentNullException("newValue");
            return new EditCellValueCommand(cell, newValue);
        }

        /// <summary>Insert a new default-valued row at the given index (append when atIndex == Rows.Count).</summary>
        public static IDatatableEditCommand AddRow(MutableDataTableDocument doc, int atIndex)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (atIndex < 0 || atIndex > doc.Rows.Count)
                throw new ArgumentOutOfRangeException("atIndex", atIndex, "Row insert index is out of range.");
            return new AddRowCommand(atIndex);
        }

        /// <summary>Remove the given row; UndoOp re-inserts the SAME instance by reference (CR-01 fix).</summary>
        public static IDatatableEditCommand RemoveRow(MutableDataTableRow row)
        {
            if (row == null) throw new ArgumentNullException("row");
            return new RemoveRowCommand(row);
        }

        /// <summary>Move the given row one position earlier (index − 1). No-op at the top.</summary>
        public static IDatatableEditCommand MoveRowUp(MutableDataTableRow row)
        {
            if (row == null) throw new ArgumentNullException("row");
            return new MoveRowCommand(row, -1);
        }

        /// <summary>Move the given row one position later (index + 1). No-op at the bottom.</summary>
        public static IDatatableEditCommand MoveRowDown(MutableDataTableRow row)
        {
            if (row == null) throw new ArgumentNullException("row");
            return new MoveRowCommand(row, +1);
        }

        /// <summary>
        /// Insert a new column of the given name + type at the given index, adding a fresh default cell
        /// to every row at the same index. UndoOp removes the column AND each row's cell at that index.
        /// </summary>
        public static IDatatableEditCommand AddColumn(MutableDataTableDocument doc, string name, DataTableColumnType ct, int atIndex)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (name == null) throw new ArgumentNullException("name");
            if (ct == null) throw new ArgumentNullException("ct");
            if (atIndex < 0 || atIndex > doc.Columns.Count)
                throw new ArgumentOutOfRangeException("atIndex", atIndex, "Column insert index is out of range.");
            return new AddColumnCommand(name, ct, atIndex);
        }

        /// <summary>
        /// Remove the given column; UndoOp re-inserts the column AND every row's captured cell BY
        /// REFERENCE (same insert-by-reference posture as RemoveRow — CR-01 fix).
        /// </summary>
        public static IDatatableEditCommand RemoveColumn(MutableDataTableColumn col)
        {
            if (col == null) throw new ArgumentNullException("col");
            return new RemoveColumnCommand(col);
        }

        /// <summary>Move the given column one position earlier; every row's cell at that index moves too.</summary>
        public static IDatatableEditCommand MoveColumnLeft(MutableDataTableColumn col)
        {
            if (col == null) throw new ArgumentNullException("col");
            return new MoveColumnCommand(col, -1);
        }

        /// <summary>Move the given column one position later; every row's cell at that index moves too.</summary>
        public static IDatatableEditCommand MoveColumnRight(MutableDataTableColumn col)
        {
            if (col == null) throw new ArgumentNullException("col");
            return new MoveColumnCommand(col, +1);
        }

        /// <summary>Rename the given column; UndoOp restores the old name.</summary>
        public static IDatatableEditCommand RenameColumn(MutableDataTableColumn col, string newName)
        {
            if (col == null) throw new ArgumentNullException("col");
            if (newName == null) throw new ArgumentNullException("newName");
            return new RenameColumnCommand(col, newName);
        }

        /// <summary>
        /// Change the given column's type, running the D-04 mangle cascade over every cell in the
        /// column. Cells that fail <c>MangleValue</c> are flagged <c>NeedsReview = true</c>; the command
        /// produces a <see cref="PendingTypeChangeCascade"/> (hoisted onto the controller) when at least
        /// one cell fails. UndoOp restores the old type + every cell's captured state (clearing the
        /// needs-review flags) and drops the cascade.
        /// </summary>
        public static IDatatableEditCommand ChangeColumnType(MutableDataTableColumn col, DataTableColumnType newCt)
        {
            if (col == null) throw new ArgumentNullException("col");
            if (newCt == null) throw new ArgumentNullException("newCt");
            return new ChangeColumnTypeCommand(col, newCt);
        }

        /// <summary>
        /// Bulk CSV-import transaction (Plan 09-06 — D-08). Wraps the N per-cell diffs in the plan's
        /// <see cref="CsvImportPlan.Changes"/> as a SINGLE undo entry: Do applies every patch (capturing
        /// each cell's full state first); UndoOp restores every cell in REVERSE order so one Ctrl+Z
        /// reverts the entire import byte-exact (RestoreState re-attaches the original slices — item 3).
        /// Cells in <see cref="CsvImportPlan.Unchanged"/> are NOT touched (they stay clean, preserving
        /// originalSlice per CF-04); cells in <see cref="CsvImportPlan.Invalid"/> are skipped entirely.
        /// </summary>
        public static IDatatableEditCommand ApplyCsvImport(MutableDataTableDocument doc, CsvImportPlan plan)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (plan == null) throw new ArgumentNullException("plan");
            return new ApplyCsvImportCommand(plan);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Concrete commands — each captures inverse state for Undo
        // ─────────────────────────────────────────────────────────────────────

        private sealed class EditCellValueCommand : IDatatableEditCommand
        {
            private readonly MutableDataTableCell cell;
            private readonly DataTableCellValue newValue;
            private MutableDataTableCell.CellState capturedState;
            private bool captured;

            public EditCellValueCommand(MutableDataTableCell cell, DataTableCellValue newValue)
            {
                this.cell = cell;
                this.newValue = newValue;
            }

            public void Do(MutableDataTableDocument doc)
            {
                // Re-capture the cell's current state each time Do runs (Apply + Redo) so UndoOp always
                // restores the state that immediately preceded THIS application (iter-2 item 3).
                capturedState = cell.CaptureState();
                captured = true;
                cell.Value = newValue;
            }

            public void UndoOp(MutableDataTableDocument doc)
            {
                if (!captured) return;
                cell.RestoreState(capturedState);
            }
        }

        private sealed class ApplyCsvImportCommand : IDatatableEditCommand
        {
            private readonly CsvImportPlan plan;

            // Captured pre-change state for each applied patch, in apply order. UndoOp walks this in
            // reverse so the document returns to its exact pre-import bytes (RestoreState re-attaches
            // each cell's original slice — item 3 byte-exact restore).
            private List<CapturedPatch> captured;

            public ApplyCsvImportCommand(CsvImportPlan plan)
            {
                this.plan = plan;
            }

            public void Do(MutableDataTableDocument doc)
            {
                // Re-capture each time Do runs (Apply + Redo) so UndoOp always restores the state that
                // immediately preceded THIS application.
                captured = new List<CapturedPatch>(plan.Changes.Count);
                for (int i = 0; i < plan.Changes.Count; i++)
                {
                    EditCellPatch p = plan.Changes[i];
                    if (p.Row < 0 || p.Row >= doc.Rows.Count) continue;
                    MutableDataTableRow row = doc.Rows[p.Row];
                    if (p.Col < 0 || p.Col >= row.Cells.Count) continue;

                    MutableDataTableCell cell = row.Cells[p.Col];
                    captured.Add(new CapturedPatch(cell, cell.CaptureState()));
                    cell.Value = p.NewValue;
                }
            }

            public void UndoOp(MutableDataTableDocument doc)
            {
                if (captured == null) return;
                for (int i = captured.Count - 1; i >= 0; i--)
                {
                    captured[i].Cell.RestoreState(captured[i].State);
                }
            }

            private struct CapturedPatch
            {
                public readonly MutableDataTableCell Cell;
                public readonly MutableDataTableCell.CellState State;

                public CapturedPatch(MutableDataTableCell cell, MutableDataTableCell.CellState state)
                {
                    Cell = cell;
                    State = state;
                }
            }
        }

        private sealed class AddRowCommand : IDatatableEditCommand
        {
            private readonly int atIndex;
            private MutableDataTableRow addedRow;

            public AddRowCommand(int atIndex)
            {
                this.atIndex = atIndex;
            }

            public void Do(MutableDataTableDocument doc)
            {
                // Build a fresh default-valued row, one cell per column seeded from the column's mangle
                // default (MangleValue("", ...) substitutes the column's [default]).
                MutableDataTableRow row = BuildDefaultRow(doc);
                row.IsAdded = true;
                doc.Rows.Insert(atIndex, row);
                row.ParentDocument = doc;
                row.MarkDirty();
                addedRow = row;
            }

            public void UndoOp(MutableDataTableDocument doc)
            {
                doc.Rows.RemoveAt(atIndex);
            }
        }

        private sealed class RemoveRowCommand : IDatatableEditCommand
        {
            private readonly MutableDataTableRow row;
            private int originalIndex;

            public RemoveRowCommand(MutableDataTableRow row)
            {
                this.row = row;
            }

            public void Do(MutableDataTableDocument doc)
            {
                // Re-find the index from the SAME row reference (post-undo it is once again a member at
                // the same slot). Removing detaches the row but preserves its cell subtree + per-cell
                // slices + dirty bits in place on the row object, so UndoOp re-attaches the same instance
                // verbatim — Phase 8 IffEditController CR-01 insert-by-reference posture
                // (IffEditController.cs:333-361). Avoids the snapshot+Materialize trap that would break
                // the cell→row back-refs.
                originalIndex = doc.Rows.IndexOf(row);
                doc.Rows.RemoveAt(originalIndex);
                doc.MarkDirty();
            }

            public void UndoOp(MutableDataTableDocument doc)
            {
                // InsertAt the original row instance BY REFERENCE so a subsequent Do() finds it in
                // doc.Rows and removes it cleanly (CR-01).
                doc.Rows.Insert(originalIndex, row);
                row.ParentDocument = doc;
                doc.MarkDirty();
            }
        }

        private sealed class MoveRowCommand : IDatatableEditCommand
        {
            private readonly MutableDataTableRow row;
            private readonly int direction; // -1 = up, +1 = down

            public MoveRowCommand(MutableDataTableRow row, int direction)
            {
                this.row = row;
                this.direction = direction;
            }

            public void Do(MutableDataTableDocument doc)
            {
                Shift(doc, direction);
            }

            public void UndoOp(MutableDataTableDocument doc)
            {
                // Inverse of move-up is move-down and vice versa.
                Shift(doc, -direction);
            }

            private void Shift(MutableDataTableDocument doc, int dir)
            {
                int idx = doc.Rows.IndexOf(row);
                int target = idx + dir;
                if (idx < 0 || target < 0 || target >= doc.Rows.Count) return;
                doc.Rows.RemoveAt(idx);
                doc.Rows.Insert(target, row);
                doc.MarkDirty();
            }
        }

        private sealed class AddColumnCommand : IDatatableEditCommand
        {
            private readonly string name;
            private readonly DataTableColumnType ct;
            private readonly int atIndex;
            private MutableDataTableColumn addedColumn;

            public AddColumnCommand(string name, DataTableColumnType ct, int atIndex)
            {
                this.name = name;
                this.ct = ct;
                this.atIndex = atIndex;
            }

            public void Do(MutableDataTableDocument doc)
            {
                var col = new MutableDataTableColumn(name, ct) { IsAdded = true };
                doc.Columns.Insert(atIndex, col);
                addedColumn = col;

                for (int r = 0; r < doc.Rows.Count; r++)
                {
                    MutableDataTableCell cell = BuildDefaultCell(ct);
                    cell.SetParentColumn(col);
                    doc.Rows[r].InsertCellInternal(atIndex, cell);
                }

                doc.MarkDirty();
            }

            public void UndoOp(MutableDataTableDocument doc)
            {
                int idx = doc.Columns.IndexOf(addedColumn);
                if (idx < 0) idx = atIndex;
                doc.Columns.RemoveAt(idx);
                for (int r = 0; r < doc.Rows.Count; r++)
                {
                    if (idx < doc.Rows[r].Cells.Count)
                    {
                        doc.Rows[r].RemoveCellInternal(idx);
                    }
                }

                doc.MarkDirty();
            }
        }

        private sealed class RemoveColumnCommand : IDatatableEditCommand
        {
            private readonly MutableDataTableColumn col;
            private int originalIndex;
            private List<MutableDataTableCell> removedCells;

            public RemoveColumnCommand(MutableDataTableColumn col)
            {
                this.col = col;
            }

            public void Do(MutableDataTableDocument doc)
            {
                originalIndex = doc.Columns.IndexOf(col);
                doc.Columns.RemoveAt(originalIndex);

                // Capture each row's cell at this column BY REFERENCE, then remove it. The captured
                // cells re-attach verbatim on Undo (CR-01 insert-by-reference) — a snapshot+rebuild
                // would re-allocate cells and break their captured original slices.
                removedCells = new List<MutableDataTableCell>(doc.Rows.Count);
                for (int r = 0; r < doc.Rows.Count; r++)
                {
                    MutableDataTableRow row = doc.Rows[r];
                    if (originalIndex < row.Cells.Count)
                    {
                        removedCells.Add(row.Cells[originalIndex]);
                        row.RemoveCellInternal(originalIndex);
                    }
                    else
                    {
                        removedCells.Add(null);
                    }
                }

                doc.MarkDirty();
            }

            public void UndoOp(MutableDataTableDocument doc)
            {
                doc.Columns.Insert(originalIndex, col);
                for (int r = 0; r < doc.Rows.Count; r++)
                {
                    MutableDataTableCell captured = r < removedCells.Count ? removedCells[r] : null;
                    if (captured != null)
                    {
                        // Re-insert the SAME cell instance by reference (CR-01).
                        doc.Rows[r].InsertCellInternal(originalIndex, captured);
                    }
                }

                doc.MarkDirty();
            }
        }

        private sealed class MoveColumnCommand : IDatatableEditCommand
        {
            private readonly MutableDataTableColumn col;
            private readonly int direction; // -1 = left, +1 = right

            public MoveColumnCommand(MutableDataTableColumn col, int direction)
            {
                this.col = col;
                this.direction = direction;
            }

            public void Do(MutableDataTableDocument doc)
            {
                Shift(doc, direction);
            }

            public void UndoOp(MutableDataTableDocument doc)
            {
                Shift(doc, -direction);
            }

            private void Shift(MutableDataTableDocument doc, int dir)
            {
                int idx = doc.Columns.IndexOf(col);
                int target = idx + dir;
                if (idx < 0 || target < 0 || target >= doc.Columns.Count) return;

                // Move the column.
                doc.Columns.RemoveAt(idx);
                doc.Columns.Insert(target, col);
                col.IsReordered = true;

                // Move every row's cell at the same index so cell↔column alignment is preserved.
                for (int r = 0; r < doc.Rows.Count; r++)
                {
                    MutableDataTableRow row = doc.Rows[r];
                    if (idx < row.Cells.Count)
                    {
                        MutableDataTableCell cell = row.Cells[idx];
                        row.RemoveCellInternal(idx);
                        row.InsertCellInternal(target, cell);
                    }
                }

                doc.MarkDirty();
            }
        }

        private sealed class RenameColumnCommand : IDatatableEditCommand
        {
            private readonly MutableDataTableColumn col;
            private readonly string newName;
            private string oldName;

            public RenameColumnCommand(MutableDataTableColumn col, string newName)
            {
                this.col = col;
                this.newName = newName;
            }

            public void Do(MutableDataTableDocument doc)
            {
                if (oldName == null) oldName = col.Name;
                col.Name = newName;
            }

            public void UndoOp(MutableDataTableDocument doc)
            {
                col.Name = oldName;
            }
        }

        private sealed class ChangeColumnTypeCommand : IDatatableEditCommand, ICascadeProducingCommand
        {
            private readonly MutableDataTableColumn col;
            private readonly DataTableColumnType newCt;
            private DataTableColumnType oldCt;
            private MutableDataTableCell.CellState[] capturedStates;
            private PendingTypeChangeCascade cascade;

            public ChangeColumnTypeCommand(MutableDataTableColumn col, DataTableColumnType newCt)
            {
                this.col = col;
                this.newCt = newCt;
            }

            public PendingTypeChangeCascade ActiveCascade { get { return cascade; } }

            public void Do(MutableDataTableDocument doc)
            {
                oldCt = col.ColumnType;
                int colIndex = doc.Columns.IndexOf(col);

                // Capture each cell's pre-change state so UndoOp restores values + slices + the
                // needs-review bits we are about to flip, byte-exact (item 3).
                var states = new List<MutableDataTableCell.CellState>(doc.Rows.Count);
                var needsReviewCells = new List<MutableDataTableCell>();

                col.ColumnType = newCt;

                for (int r = 0; r < doc.Rows.Count; r++)
                {
                    MutableDataTableRow row = doc.Rows[r];
                    if (colIndex < 0 || colIndex >= row.Cells.Count)
                    {
                        states.Add(default(MutableDataTableCell.CellState));
                        continue;
                    }

                    MutableDataTableCell cell = row.Cells[colIndex];
                    states.Add(cell.CaptureState());

                    // Run the new type's mangle over the cell's current string form.
                    string current = cell.Value != null ? cell.Value.ToString() : string.Empty;
                    DataTableCellValue coerced;
                    if (newCt.TryCoerceCellValue(current, out coerced))
                    {
                        // Mangle succeeded → adopt the coerced typed value (no review needed).
                        cell.Value = coerced;
                    }
                    else
                    {
                        // Mangle failed → keep the current value, flag for review (D-04). The user
                        // resolves via the cascade modal (Accept-mangled / Edit-cell / Revert).
                        cell.NeedsReview = true;
                        needsReviewCells.Add(cell);
                    }
                }

                capturedStates = states.ToArray();

                cascade = needsReviewCells.Count > 0
                    ? new PendingTypeChangeCascade
                    {
                        ColumnIndex = colIndex,
                        OldType = oldCt,
                        NewType = newCt,
                        NeedsReviewCellRefs = needsReviewCells,
                    }
                    : null;
            }

            public void UndoOp(MutableDataTableDocument doc)
            {
                int colIndex = doc.Columns.IndexOf(col);
                col.ColumnType = oldCt;

                if (capturedStates != null && colIndex >= 0)
                {
                    for (int r = 0; r < doc.Rows.Count && r < capturedStates.Length; r++)
                    {
                        MutableDataTableRow row = doc.Rows[r];
                        if (colIndex < row.Cells.Count)
                        {
                            // RestoreState re-attaches the original value + slice AND clears the
                            // NeedsReview = true this command set (item 3 byte-exact restore).
                            row.Cells[colIndex].RestoreState(capturedStates[r]);
                        }
                    }
                }

                // Drop the cascade — the controller's RecomputeCascadeState nulls PendingCascadeContext.
                cascade = null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Default-cell / default-row builders (seed via the column's MangleValue default)
        // ─────────────────────────────────────────────────────────────────────

        private static MutableDataTableRow BuildDefaultRow(MutableDataTableDocument doc)
        {
            var row = new MutableDataTableRow();
            for (int c = 0; c < doc.Columns.Count; c++)
            {
                MutableDataTableColumn column = doc.Columns[c];
                MutableDataTableCell cell = BuildDefaultCell(column.ColumnType);
                cell.SetParentColumn(column);
                row.AddCellInternal(cell);
            }

            return row;
        }

        private static MutableDataTableCell BuildDefaultCell(DataTableColumnType ct)
        {
            // Seed from the column's mangle default ("" → the column's [default] substitution). A
            // freshly-built cell carries no original slice (WritePayload will SerializeFresh).
            DataTableCellValue value;
            if (!ct.TryCoerceCellValue(string.Empty, out value))
            {
                // A "required"/"unique" default or DT_Unknown column has no coercible empty default;
                // seed a basic-type-appropriate zero/empty so the row is serializable.
                value = DefaultZeroValue(ct);
            }

            return new MutableDataTableCell(value, null);
        }

        private static DataTableCellValue DefaultZeroValue(DataTableColumnType ct)
        {
            switch (ct.BasicType)
            {
                case DataType.Float:
                    return DataTableCellValue.FromFloat(0f);
                case DataType.String:
                    return DataTableCellValue.FromString(string.Empty);
                case DataType.Int:
                    return DataTableCellValue.FromInt(0);
                default:
                    // DT_Comment / DT_Unknown carry no on-disk value; model as empty string.
                    return DataTableCellValue.FromString(string.Empty);
            }
        }
    }
}
