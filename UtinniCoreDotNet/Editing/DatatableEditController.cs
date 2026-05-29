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
// Editor-local undo/redo controller for the FormDatatableEditor typed datatable editor (Phase 9
// D-01 T4). The datatable analog of UtinniCoreDotNet.Editing.IffEditController (Phase 8 D-08) —
// a verbatim port of its Apply/Undo/Redo/netAppliedCount idiom (IffEditController.cs:67-160) PLUS
// the Phase-9 NeedsReviewCount + PendingCascadeContext + MarkSaved seams. Pure-managed (no UI
// dependency); xUnit-testable from CI. Phases 9-11 reuse via the existing UtinniCoreDotNet.dll ref.

using System;
using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Datatable;

namespace UtinniCoreDotNet.Editing
{
    /// <summary>
    /// Active column-type-change cascade record (D-04). Held on the CONTROLLER
    /// (<see cref="DatatableEditController.PendingCascadeContext"/>), NOT on the form, so the cascade
    /// state machine is xUnit-testable (iter-1 fix #8 + iter-2 item 6 — cursor HIGH). The form READS
    /// this property; it never owns the state and there is no form-local <c>lastCascadeContext</c> field.
    /// </summary>
    public sealed class PendingTypeChangeCascade
    {
        /// <summary>The 0-based index of the column whose type changed.</summary>
        public int ColumnIndex { get; internal set; }

        /// <summary>The column's type before the change (restored on Undo).</summary>
        public DataTableColumnType OldType { get; internal set; }

        /// <summary>The column's type after the change.</summary>
        public DataTableColumnType NewType { get; internal set; }

        /// <summary>
        /// The cells flagged <c>NeedsReview = true</c> by the cascade — the subset whose
        /// <c>MangleValue</c> failed under the new type. Live references into the model so the
        /// resolution UI (FormTypeChangeCascadeDialog) can edit them in place.
        /// </summary>
        public IReadOnlyList<MutableDataTableCell> NeedsReviewCellRefs { get; internal set; }
    }

    /// <summary>
    /// A command that owns an active cascade context. The controller reads this after every
    /// Apply / Undo / Redo to hoist the cascade state onto <see cref="DatatableEditController.PendingCascadeContext"/>
    /// (iter-2 item 6 — the state ends up on the controller, never the form). A null
    /// <see cref="ActiveCascade"/> means the command currently contributes no cascade (e.g. after its
    /// own UndoOp, or when its cascade flagged zero cells).
    /// </summary>
    internal interface ICascadeProducingCommand
    {
        PendingTypeChangeCascade ActiveCascade { get; }
    }

    /// <summary>
    /// Editor-local undo/redo controller over a <see cref="MutableDataTableDocument"/>.
    ///
    /// <para><b>Verbatim port of IffEditController (Phase 8 D-08):</b> the Apply / Undo / Redo /
    /// <c>netAppliedCount</c> idiom mirrors <c>IffEditController.cs:67-160</c> with the type-name
    /// swaps (<c>MutableIffDocument</c> → <c>MutableDataTableDocument</c>; <c>IIffEditCommand</c> →
    /// <see cref="IDatatableEditCommand"/>). The Phase-9 additions are <see cref="NeedsReviewCount"/>,
    /// <see cref="PendingCascadeContext"/>, and <see cref="MarkSaved"/>.</para>
    ///
    /// <para><b>Baseline-clean dirty semantics (08-REVIEWS Codex unique):</b> <see cref="IsDirty"/> is
    /// <c>netAppliedCount &gt; 0</c> — Apply increments, Undo decrements, Redo re-increments. Undo back
    /// to baseline reads clean even though touched cells still carry their dirty bit (so per-cell UI
    /// overlays still work). <see cref="MarkSaved"/> resets the baseline after a save (iter-2 item 8).</para>
    ///
    /// <para><b>Cascade state machine (D-04 / iter-2 item 6):</b> <see cref="ChangeColumnTypeCommand"/>
    /// populates its cascade; the controller hoists it onto <see cref="PendingCascadeContext"/> after
    /// every operation via <see cref="RecomputeCascadeState"/>, and auto-clears it the moment
    /// <see cref="NeedsReviewCount"/> reaches 0 (the last failing cell resolved via per-cell edits).</para>
    /// </summary>
    public sealed class DatatableEditController
    {
        private readonly MutableDataTableDocument document;
        private readonly Stack<IDatatableEditCommand> undoStack = new Stack<IDatatableEditCommand>();
        private readonly Stack<IDatatableEditCommand> redoStack = new Stack<IDatatableEditCommand>();

        // Baseline-clean dirty semantics: net edits applied relative to the baseline (Apply++; Undo--;
        // Redo++). net == 0 → clean. Identical idiom to IffEditController.netAppliedCount.
        private int netAppliedCount;

        private PendingTypeChangeCascade pendingCascade;

        /// <summary>
        /// Constructs a controller over the given mutable document. The current document state is the
        /// baseline for clean-dirty semantics.
        /// </summary>
        public DatatableEditController(MutableDataTableDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            this.document = document;
            this.netAppliedCount = 0;
        }

        /// <summary>The mutable document under edit.</summary>
        public MutableDataTableDocument Document { get { return document; } }

        /// <summary>True iff the document has at least one net-applied edit relative to the baseline.</summary>
        public bool IsDirty { get { return netAppliedCount > 0; } }

        /// <summary>True iff there is at least one applied command to undo.</summary>
        public bool CanUndo { get { return undoStack.Count > 0; } }

        /// <summary>True iff there is at least one undone command to redo.</summary>
        public bool CanRedo { get { return redoStack.Count > 0; } }

        /// <summary>
        /// The number of cells currently flagged <c>NeedsReview = true</c> (D-04 cascade). Walks every
        /// cell on read — cheap for V1 datatable sizes; if profiling surfaces it, cache via an
        /// incremental delta in a follow-up. Save is BLOCKED while this is &gt; 0 (UI-SPEC R-04).
        /// </summary>
        public int NeedsReviewCount
        {
            get
            {
                int count = 0;
                IList<MutableDataTableRow> rows = document.Rows;
                for (int r = 0; r < rows.Count; r++)
                {
                    IReadOnlyList<MutableDataTableCell> cells = rows[r].Cells;
                    for (int c = 0; c < cells.Count; c++)
                    {
                        if (cells[c].NeedsReview) count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// The active column-type-change cascade (D-04), or null when none is pending. Lives on the
        /// controller (NOT the form) so the cascade state machine is xUnit-testable (iter-2 item 6).
        /// </summary>
        public PendingTypeChangeCascade PendingCascadeContext { get { return pendingCascade; } }

        /// <summary>
        /// Raised after every successful Apply / Undo / Redo / MarkSaved so the host refreshes the grid
        /// + dirty visuals + undo/redo button states + the Resolve-cascade button visibility.
        /// </summary>
        public event EventHandler EditApplied;

        /// <summary>
        /// Applies the given edit command, pushes it on the undo stack, and truncates the redo tail
        /// (Apply-after-Undo invalidates the redo history per standard undo semantics).
        /// </summary>
        public void Apply(IDatatableEditCommand command)
        {
            if (command == null) throw new ArgumentNullException("command");
            command.Do(document);
            undoStack.Push(command);
            redoStack.Clear();
            netAppliedCount++;
            RecomputeCascadeState(command);
            RaiseEditApplied();
        }

        /// <summary>
        /// Undoes the top command on the undo stack and pushes it on the redo stack. No-op when
        /// <see cref="CanUndo"/> is false.
        /// </summary>
        public void Undo()
        {
            if (!CanUndo) return;
            IDatatableEditCommand command = undoStack.Pop();
            command.UndoOp(document);
            redoStack.Push(command);
            netAppliedCount--;
            RecomputeCascadeState(command);
            RaiseEditApplied();
        }

        /// <summary>
        /// Redoes the top command on the redo stack and pushes it back on the undo stack. No-op when
        /// <see cref="CanRedo"/> is false.
        /// </summary>
        public void Redo()
        {
            if (!CanRedo) return;
            IDatatableEditCommand command = redoStack.Pop();
            command.Do(document);
            undoStack.Push(command);
            netAppliedCount++;
            RecomputeCascadeState(command);
            RaiseEditApplied();
        }

        /// <summary>
        /// Resets the dirty baseline after a successful save (iter-2 item 8). Sets
        /// <c>netAppliedCount = 0</c> so <see cref="IsDirty"/> reads false, and rebaselines every cell
        /// so the NEXT edit re-computes dirtiness against the just-saved bytes — a saved-then-edited
        /// document is dirty again. Per-cell rebaseline mechanism: re-capture each cell's current
        /// serialized payload as its new <c>originalSlice</c> and clear its dirty bit
        /// (<see cref="MutableDataTableCell.RebaselineAfterSave"/>). Undo/redo stacks are NOT touched —
        /// the user can still undo across a save boundary; only the dirty indicator resets. Plan 09-05's
        /// save handlers call this on save success.
        /// </summary>
        public void MarkSaved()
        {
            IList<MutableDataTableColumn> columns = document.Columns;
            IList<MutableDataTableRow> rows = document.Rows;
            for (int r = 0; r < rows.Count; r++)
            {
                IReadOnlyList<MutableDataTableCell> cells = rows[r].Cells;
                for (int c = 0; c < cells.Count && c < columns.Count; c++)
                {
                    cells[c].RebaselineAfterSave(columns[c].ColumnType);
                }
            }

            netAppliedCount = 0;
            RaiseEditApplied();
        }

        // Hoist any cascade the just-run command owns onto the controller, and auto-clear the pending
        // cascade the moment every flagged cell has been resolved (NeedsReviewCount transitions to 0).
        private void RecomputeCascadeState(IDatatableEditCommand command)
        {
            var producer = command as ICascadeProducingCommand;
            if (producer != null)
            {
                // ChangeColumnTypeCommand.Do sets ActiveCascade; its UndoOp nulls it.
                pendingCascade = producer.ActiveCascade;
            }

            // Auto-clear on full per-cell resolution: a non-cascade command (e.g. EditCellValue on the
            // last failing cell) drives NeedsReviewCount to 0 → drop the now-stale cascade record.
            if (pendingCascade != null && NeedsReviewCount == 0)
            {
                pendingCascade = null;
            }
        }

        private void RaiseEditApplied()
        {
            EventHandler h = EditApplied;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
