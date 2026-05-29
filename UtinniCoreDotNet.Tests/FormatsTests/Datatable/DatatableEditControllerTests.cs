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
using UtinniCoreDotNet.Editing;
using UtinniCoreDotNet.Formats.Datatable;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Tests.FormatsTests.Datatable
{
    /// <summary>
    /// Controller-layer tests for the Plan 09-04 <see cref="DatatableEditController"/> + the 11 D-01 T4
    /// <see cref="DatatableEditCommands"/> factories.
    ///
    /// <para>Contract under test: each command × Apply / Undo / Redo identity; the baseline-clean dirty
    /// invariant (Codex unique concern); the D-04 column-type-change cascade matrix + the three cascade
    /// state-machine facts (PendingCascadeContext on the CONTROLLER); the <c>MarkSaved()</c> rebaseline
    /// fact (iter-2 item 8); the insert-by-reference fix for RemoveRow + RemoveColumn (CR-01); and the
    /// SC4 structural property at the controller layer (load → EditCellValue → Undo → bytes EXACTLY
    /// equal loaded, via RestoreState — iter-2 item 3).</para>
    /// </summary>
    public class DatatableEditControllerTests
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

        private static byte[] Serialize(MutableDataTableDocument doc)
        {
            return new DataTableWriter(doc).Serialize();
        }

        private static MutableDataTableCell CellAt(MutableDataTableDocument doc, int row, int col)
        {
            return doc.Rows[row].Cells[col];
        }

        // A small string-column fixture for the cascade tests: a single "label" DT_String column whose
        // cell values are the supplied strings (one row per string).
        private static MutableDataTableDocument BuildStringColumn(params string[] values)
        {
            var cols = new List<MutableDataTableColumn> { new MutableDataTableColumn("label", new DataTableColumnType("s")) };
            var rows = new List<MutableDataTableRow>();
            foreach (string v in values)
            {
                var row = new MutableDataTableRow();
                var cell = new MutableDataTableCell(DataTableCellValue.FromString(v), null);
                cell.SetParentColumn(cols[0]);
                row.AddCellInternal(cell);
                rows.Add(row);
            }

            return new MutableDataTableDocument("0001", cols, rows);
        }

        // ── Controller invariants ────────────────────────────────────────────

        [Fact]
        public void FreshController_IsNotDirty_CanUndoRedoFalse()
        {
            var c = new DatatableEditController(LoadMutable(DatatableFixtures.BuildV1Minimal()));
            Assert.False(c.IsDirty);
            Assert.False(c.CanUndo);
            Assert.False(c.CanRedo);
            Assert.Equal(0, c.NeedsReviewCount);
            Assert.Null(c.PendingCascadeContext);
        }

        [Fact]
        public void Apply_SetsDirty_AndCanUndo()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(999)));
            Assert.True(c.IsDirty);
            Assert.True(c.CanUndo);
            Assert.False(c.CanRedo);
        }

        [Fact]
        public void ApplyThenUndo_BackToBaseline_IsNotDirty()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(999)));
            c.Undo();
            Assert.False(c.IsDirty);
            Assert.False(c.CanUndo);
            Assert.True(c.CanRedo);
        }

        [Fact]
        public void ApplyAfterUndo_TruncatesRedoTail()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(1)));
            c.Undo();
            Assert.True(c.CanRedo);
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(2)));
            Assert.False(c.CanRedo);
        }

        [Fact]
        public void EditApplied_FiresOncePerOperation()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var c = new DatatableEditController(doc);
            int fires = 0;
            c.EditApplied += (s, e) => fires++;
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(5)));
            c.Undo();
            c.Redo();
            Assert.Equal(3, fires);
        }

        [Fact]
        public void Undo_WhenNothingToUndo_IsNoOp()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var c = new DatatableEditController(doc);
            c.Undo();
            Assert.False(c.IsDirty);
        }

        // ── EditCellValue Apply/Undo/Redo ────────────────────────────────────

        [Fact]
        public void EditCellValue_Do_ChangesValue()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(42)));
            Assert.Equal("42", CellAt(doc, 0, 0).Value.ToString());
        }

        [Fact]
        public void EditCellValue_Undo_RestoresValueAndCleanState()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            string original = CellAt(doc, 0, 0).Value.ToString();
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(42)));
            Assert.True(CellAt(doc, 0, 0).IsDirty);
            c.Undo();
            Assert.Equal(original, CellAt(doc, 0, 0).Value.ToString());
            Assert.False(CellAt(doc, 0, 0).IsDirty);
        }

        [Fact]
        public void EditCellValue_Redo_ReAppliesValue()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(42)));
            c.Undo();
            c.Redo();
            Assert.Equal("42", CellAt(doc, 0, 0).Value.ToString());
            Assert.True(c.IsDirty);
        }

        [Fact(DisplayName = "SC4 (controller): EditCellValue → RestoreState-Undo → bytes EXACTLY equal loaded")]
        public void SC4_Controller_EditCellValue_Undo_BytesExactlyEqualLoaded()
        {
            byte[] loaded = DatatableFixtures.BuildV1AllTypes();
            MutableDataTableDocument doc = LoadMutable(loaded);
            byte[] beforeEdit = Serialize(doc);
            Assert.Equal(loaded, beforeEdit); // sanity: clean round-trip is byte-exact

            var c = new DatatableEditController(doc);
            // Edit cell [0,0] (anInt) to a different value, then Undo via RestoreState.
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(123456)));
            c.Undo();

            byte[] afterUndo = Serialize(doc);
            Assert.Equal(loaded, afterUndo);
        }

        // ── AddRow / RemoveRow ───────────────────────────────────────────────

        [Fact]
        public void AddRow_Do_IncrementsRowCount_Undo_Restores()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            int before = doc.Rows.Count;
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.AddRow(doc, doc.Rows.Count));
            Assert.Equal(before + 1, doc.Rows.Count);
            c.Undo();
            Assert.Equal(before, doc.Rows.Count);
        }

        [Fact]
        public void AddRow_Redo_ReAddsRow()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            int before = doc.Rows.Count;
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.AddRow(doc, doc.Rows.Count));
            c.Undo();
            c.Redo();
            Assert.Equal(before + 1, doc.Rows.Count);
        }

        [Fact]
        public void RemoveRow_Do_DecrementsRowCount_Undo_RestoresSameInstanceByReference()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            MutableDataTableRow target = doc.Rows[1];
            int before = doc.Rows.Count;
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.RemoveRow(target));
            Assert.Equal(before - 1, doc.Rows.Count);
            c.Undo();
            Assert.Equal(before, doc.Rows.Count);
            // CR-01 insert-by-reference: the restored row is the SAME instance.
            Assert.Same(target, doc.Rows[1]);
        }

        [Fact]
        public void RemoveRow_Undo_PreservesCellOriginalSlices()
        {
            byte[] loaded = DatatableFixtures.BuildV1AllTypes();
            var doc = LoadMutable(loaded);
            // Append a clone-less row removal/undo and assert byte-exact round-trip survives.
            MutableDataTableRow target = doc.Rows[0];
            var c = new DatatableEditController(doc);
            // Add a second row first so removing row 0 leaves a non-empty table to restore against.
            c.Apply(DatatableEditCommands.AddRow(doc, doc.Rows.Count));
            c.Apply(DatatableEditCommands.RemoveRow(target));
            c.Undo(); // undo remove → target row back by reference
            c.Undo(); // undo add → back to the loaded shape
            Assert.Equal(loaded, Serialize(doc));
        }

        [Fact]
        public void RemoveRow_RedoAfterUndo_RemovesCleanly()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            MutableDataTableRow target = doc.Rows[0];
            int before = doc.Rows.Count;
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.RemoveRow(target));
            c.Undo();
            c.Redo();
            Assert.Equal(before - 1, doc.Rows.Count);
            Assert.DoesNotContain(target, doc.Rows);
        }

        // ── MoveRowUp / MoveRowDown ──────────────────────────────────────────

        [Fact]
        public void MoveRowDown_Do_SwapsInstances_Undo_Restores()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            MutableDataTableRow r0 = doc.Rows[0];
            MutableDataTableRow r1 = doc.Rows[1];
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.MoveRowDown(r0));
            Assert.Same(r1, doc.Rows[0]);
            Assert.Same(r0, doc.Rows[1]);
            c.Undo();
            Assert.Same(r0, doc.Rows[0]);
            Assert.Same(r1, doc.Rows[1]);
        }

        [Fact]
        public void MoveRowUp_Do_SwapsInstances_Redo_ReApplies()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            MutableDataTableRow r0 = doc.Rows[0];
            MutableDataTableRow r1 = doc.Rows[1];
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.MoveRowUp(r1));
            Assert.Same(r1, doc.Rows[0]);
            c.Undo();
            Assert.Same(r0, doc.Rows[0]);
            c.Redo();
            Assert.Same(r1, doc.Rows[0]);
        }

        // ── AddColumn / RemoveColumn ─────────────────────────────────────────

        [Fact]
        public void AddColumn_Do_AddsColumnAndCellPerRow_Undo_Restores()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            int beforeCols = doc.Columns.Count;
            int beforeCells = doc.Rows[0].Cells.Count;
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.AddColumn(doc, "extra", new DataTableColumnType("i"), doc.Columns.Count));
            Assert.Equal(beforeCols + 1, doc.Columns.Count);
            Assert.Equal(beforeCells + 1, doc.Rows[0].Cells.Count);
            c.Undo();
            Assert.Equal(beforeCols, doc.Columns.Count);
            Assert.Equal(beforeCells, doc.Rows[0].Cells.Count);
        }

        [Fact]
        public void AddColumn_Redo_ReAdds()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            int beforeCols = doc.Columns.Count;
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.AddColumn(doc, "extra", new DataTableColumnType("s"), doc.Columns.Count));
            c.Undo();
            c.Redo();
            Assert.Equal(beforeCols + 1, doc.Columns.Count);
        }

        [Fact]
        public void RemoveColumn_Do_RemovesColumnAndCells_Undo_RestoresByReference()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            MutableDataTableColumn target = doc.Columns[1];
            MutableDataTableCell row0Cell = doc.Rows[0].Cells[1];
            int beforeCols = doc.Columns.Count;
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.RemoveColumn(target));
            Assert.Equal(beforeCols - 1, doc.Columns.Count);
            c.Undo();
            Assert.Equal(beforeCols, doc.Columns.Count);
            // CR-01 insert-by-reference: the same column AND the same cell instance come back.
            Assert.Same(target, doc.Columns[1]);
            Assert.Same(row0Cell, doc.Rows[0].Cells[1]);
        }

        [Fact]
        public void RemoveColumn_Undo_PreservesByteExactRoundTrip()
        {
            byte[] loaded = DatatableFixtures.BuildV1AllTypes();
            var doc = LoadMutable(loaded);
            MutableDataTableColumn target = doc.Columns[2]; // aString
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.RemoveColumn(target));
            c.Undo();
            Assert.Equal(loaded, Serialize(doc));
        }

        // ── MoveColumnLeft / MoveColumnRight ─────────────────────────────────

        [Fact]
        public void MoveColumnRight_Do_MovesColumnAndCells_Undo_Restores()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            MutableDataTableColumn c0 = doc.Columns[0];
            MutableDataTableColumn c1 = doc.Columns[1];
            MutableDataTableCell row0Col0 = doc.Rows[0].Cells[0];
            var ctrl = new DatatableEditController(doc);
            ctrl.Apply(DatatableEditCommands.MoveColumnRight(c0));
            Assert.Same(c1, doc.Columns[0]);
            Assert.Same(c0, doc.Columns[1]);
            // The cell that belonged to c0 moved alongside it.
            Assert.Same(row0Col0, doc.Rows[0].Cells[1]);
            ctrl.Undo();
            Assert.Same(c0, doc.Columns[0]);
            Assert.Same(row0Col0, doc.Rows[0].Cells[0]);
        }

        [Fact]
        public void MoveColumnLeft_Do_MovesColumn_Redo_ReApplies()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            MutableDataTableColumn c1 = doc.Columns[1];
            var ctrl = new DatatableEditController(doc);
            ctrl.Apply(DatatableEditCommands.MoveColumnLeft(c1));
            Assert.Same(c1, doc.Columns[0]);
            ctrl.Undo();
            Assert.Same(c1, doc.Columns[1]);
            ctrl.Redo();
            Assert.Same(c1, doc.Columns[0]);
        }

        [Fact]
        public void MoveColumn_RoundTrip_PreservesByteExact()
        {
            byte[] loaded = DatatableFixtures.BuildV1AllTypes();
            var doc = LoadMutable(loaded);
            var ctrl = new DatatableEditController(doc);
            ctrl.Apply(DatatableEditCommands.MoveColumnRight(doc.Columns[0]));
            ctrl.Undo();
            Assert.Equal(loaded, Serialize(doc));
        }

        // ── RenameColumn ─────────────────────────────────────────────────────

        [Fact]
        public void RenameColumn_Do_ChangesName_Undo_Restores()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            string original = doc.Columns[0].Name;
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.RenameColumn(doc.Columns[0], "renamed"));
            Assert.Equal("renamed", doc.Columns[0].Name);
            c.Undo();
            Assert.Equal(original, doc.Columns[0].Name);
        }

        [Fact]
        public void RenameColumn_Redo_ReApplies()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.RenameColumn(doc.Columns[0], "renamed"));
            c.Undo();
            c.Redo();
            Assert.Equal("renamed", doc.Columns[0].Name);
        }

        // ── ChangeColumnType cascade (D-04) ──────────────────────────────────

        [Fact]
        public void ChangeColumnType_StringToInt_AllInteger_NoReviewNeeded()
        {
            var doc = BuildStringColumn("1", "2", "3");
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.ChangeColumnType(doc.Columns[0], new DataTableColumnType("i")));
            Assert.Equal(0, c.NeedsReviewCount);
            Assert.Null(c.PendingCascadeContext);
            Assert.Equal("1", CellAt(doc, 0, 0).Value.ToString());
        }

        [Fact]
        public void ChangeColumnType_StringToInt_OneNonInteger_FlagsOneCell()
        {
            var doc = BuildStringColumn("1", "notnum", "3");
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.ChangeColumnType(doc.Columns[0], new DataTableColumnType("i")));
            Assert.Equal(1, c.NeedsReviewCount);
            Assert.NotNull(c.PendingCascadeContext);
            Assert.Single(c.PendingCascadeContext.NeedsReviewCellRefs);
            // The bad cell kept its value; the others coerced.
            Assert.True(CellAt(doc, 1, 0).NeedsReview);
            Assert.False(CellAt(doc, 0, 0).NeedsReview);
        }

        [Fact]
        public void ChangeColumnType_IntToBool_FlagsOutOfRangeCell()
        {
            // Build an int column with values 0, 1, 5; changing to bool flags "5".
            var cols = new List<MutableDataTableColumn> { new MutableDataTableColumn("flag", new DataTableColumnType("i")) };
            var rows = new List<MutableDataTableRow>();
            foreach (int v in new[] { 0, 1, 5 })
            {
                var row = new MutableDataTableRow();
                var cell = new MutableDataTableCell(DataTableCellValue.FromInt(v), null);
                cell.SetParentColumn(cols[0]);
                row.AddCellInternal(cell);
                rows.Add(row);
            }
            var doc = new MutableDataTableDocument("0001", cols, rows);

            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.ChangeColumnType(doc.Columns[0], new DataTableColumnType("b")));
            Assert.Equal(1, c.NeedsReviewCount);
            Assert.True(CellAt(doc, 2, 0).NeedsReview);
            Assert.False(CellAt(doc, 0, 0).NeedsReview);
            Assert.False(CellAt(doc, 1, 0).NeedsReview);
        }

        [Fact]
        public void ChangeColumnType_StringToHash_AllMangleSuccessfully()
        {
            var doc = BuildStringColumn("creature/foo", "creature/bar", "");
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.ChangeColumnType(doc.Columns[0], new DataTableColumnType("h")));
            // HashString CRC accepts any string (empty → 0); zero cells need review.
            Assert.Equal(0, c.NeedsReviewCount);
            Assert.Null(c.PendingCascadeContext);
        }

        [Fact]
        public void ChangeColumnType_Undo_RestoresTypeValuesAndClearsAllNeedsReview()
        {
            var doc = BuildStringColumn("1", "notnum", "3");
            DataTableColumnType oldType = doc.Columns[0].ColumnType;
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.ChangeColumnType(doc.Columns[0], new DataTableColumnType("i")));
            Assert.Equal(1, c.NeedsReviewCount);
            c.Undo();
            Assert.Same(oldType, doc.Columns[0].ColumnType);
            Assert.Equal(0, c.NeedsReviewCount);
            Assert.Null(c.PendingCascadeContext);
            Assert.Equal("notnum", CellAt(doc, 1, 0).Value.ToString());
            Assert.False(CellAt(doc, 1, 0).NeedsReview);
        }

        [Fact]
        public void ChangeColumnType_FlagsThreeCells_NeedsReviewCountIsThree_Undo_Zero()
        {
            var doc = BuildStringColumn("a", "b", "c");
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.ChangeColumnType(doc.Columns[0], new DataTableColumnType("i")));
            Assert.Equal(3, c.NeedsReviewCount);
            c.Undo();
            Assert.Equal(0, c.NeedsReviewCount);
        }

        // ── Cascade state-machine facts (iter-2 item 6) ──────────────────────

        [Fact]
        public void ChangeColumnType_Then_Undo_ClearsPendingCascade()
        {
            var doc = BuildStringColumn("1", "notnum", "3");
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.ChangeColumnType(doc.Columns[0], new DataTableColumnType("i")));
            Assert.NotNull(c.PendingCascadeContext);
            c.Undo();
            Assert.Null(c.PendingCascadeContext);
            Assert.Equal(0, c.NeedsReviewCount);
        }

        [Fact]
        public void ChangeColumnType_Then_ResolveAllCells_ClearsPendingCascade()
        {
            var doc = BuildStringColumn("x", "y", "z");
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.ChangeColumnType(doc.Columns[0], new DataTableColumnType("i")));
            Assert.Equal(3, c.NeedsReviewCount);
            Assert.NotNull(c.PendingCascadeContext);

            // Resolve each failing cell with a valid integer value.
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(10)));
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 1, 0), DataTableCellValue.FromInt(20)));
            Assert.NotNull(c.PendingCascadeContext); // still one unresolved
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 2, 0), DataTableCellValue.FromInt(30)));

            Assert.Equal(0, c.NeedsReviewCount);
            Assert.Null(c.PendingCascadeContext);
        }

        [Fact]
        public void ChangeColumnType_Then_PartialResolve_PendingCascadeRetained()
        {
            var doc = BuildStringColumn("x", "y", "z");
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.ChangeColumnType(doc.Columns[0], new DataTableColumnType("i")));

            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(10)));
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 1, 0), DataTableCellValue.FromInt(20)));

            Assert.NotNull(c.PendingCascadeContext);
            Assert.Equal(1, c.NeedsReviewCount);
            // Setting a valid value clears NeedsReview on that cell; the remaining failing cell stays.
            Assert.True(CellAt(doc, 2, 0).NeedsReview);
        }

        // ── MarkSaved (iter-2 item 8) ────────────────────────────────────────

        [Fact]
        public void MarkSaved_ResetsDirtyBaseline_AndNextEditFlipsDirty()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            var c = new DatatableEditController(doc);
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(101)));
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 1, 0), DataTableCellValue.FromInt(202)));
            Assert.True(c.IsDirty);

            c.MarkSaved();
            Assert.False(c.IsDirty);
            for (int r = 0; r < doc.Rows.Count; r++)
            {
                Assert.False(doc.Rows[r].IsDirty);
            }

            // The next edit flips dirty true again (baseline moved to the saved state).
            c.Apply(DatatableEditCommands.EditCellValue(CellAt(doc, 0, 0), DataTableCellValue.FromInt(303)));
            Assert.True(c.IsDirty);
        }

        // ── ApplyCsvImport stub ──────────────────────────────────────────────

        [Fact]
        public void ApplyCsvImport_Throws_NotImplemented_WiredByPlan0906()
        {
            var doc = LoadMutable(DatatableFixtures.BuildV1Minimal());
            Assert.Throws<System.NotImplementedException>(
                () => DatatableEditCommands.ApplyCsvImport(doc, null));
        }
    }
}
