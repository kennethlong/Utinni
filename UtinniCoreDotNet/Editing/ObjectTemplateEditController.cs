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
// Editor-local undo/redo controller for the FormObjectTemplateEditor typed object-template editor
// (Phase 11 D-04 / CF-04). The object-template analog of UtinniCoreDotNet.Editing.DatatableEditController
// — the SIMPLER core Apply/Undo/Redo/netAppliedCount/MarkSaved skeleton, WITHOUT the Phase-9 column-type
// cascade machinery (OT has no column-type cascade). CON-M-05 (extra-load-bearing): object-template edits
// touch live-scene objects, so this stack is COMPLETELY disentangled from the scene undo/redo manager —
// no scene-manager reference. Pure-managed (no UI dependency); xUnit-testable from CI.

using System;
using System.Collections.Generic;
using UtinniCoreDotNet.Formats.ObjectTemplate;

namespace UtinniCoreDotNet.Editing
{
    /// <summary>
    /// Editor-local undo/redo controller over a <see cref="MutableObjectTemplate"/>.
    ///
    /// <para><b>Clone of DatatableEditController's core (Phase 9):</b> the Apply / Undo / Redo /
    /// <c>netAppliedCount</c> idiom with the type swaps (<c>MutableDataTableDocument</c> →
    /// <see cref="MutableObjectTemplate"/>; <c>IDatatableEditCommand</c> →
    /// <see cref="IObjectTemplateEditCommand"/>). The Phase-9 column-type-cascade state machine
    /// (the pending-cascade record, the needs-review counter, and its recompute step) is DROPPED —
    /// object templates have no column-type cascade.</para>
    ///
    /// <para><b>Baseline-clean dirty semantics:</b> <see cref="IsDirty"/> is <c>netAppliedCount &gt; 0</c>
    /// — Apply increments, Undo decrements, Redo re-increments. Undo back to baseline reads clean.
    /// <see cref="MarkSaved"/> resets the baseline after a save.</para>
    ///
    /// <para><b>CON-M-05 guard-rail (extra-load-bearing):</b> this controller MUST NOT reference the
    /// scene undo/redo manager. It is pure-managed exactly like DatatableEditController.</para>
    /// </summary>
    public sealed class ObjectTemplateEditController
    {
        private readonly MutableObjectTemplate document;
        private readonly Stack<IObjectTemplateEditCommand> undoStack = new Stack<IObjectTemplateEditCommand>();
        private readonly Stack<IObjectTemplateEditCommand> redoStack = new Stack<IObjectTemplateEditCommand>();

        // Baseline-clean dirty semantics: net edits applied relative to the baseline (Apply++; Undo--;
        // Redo++). net == 0 → clean. Identical idiom to DatatableEditController.netAppliedCount.
        private int netAppliedCount;

        /// <summary>
        /// Constructs a controller over the given mutable object template. The current document state is
        /// the baseline for clean-dirty semantics.
        /// </summary>
        public ObjectTemplateEditController(MutableObjectTemplate document)
        {
            if (document == null) throw new ArgumentNullException("document");
            this.document = document;
            this.netAppliedCount = 0;
        }

        /// <summary>The mutable object template under edit.</summary>
        public MutableObjectTemplate Document { get { return document; } }

        /// <summary>True iff the document has at least one net-applied edit relative to the baseline.</summary>
        public bool IsDirty { get { return netAppliedCount > 0; } }

        /// <summary>True iff there is at least one applied command to undo.</summary>
        public bool CanUndo { get { return undoStack.Count > 0; } }

        /// <summary>True iff there is at least one undone command to redo.</summary>
        public bool CanRedo { get { return redoStack.Count > 0; } }

        /// <summary>
        /// Raised after every successful Apply / Undo / Redo / MarkSaved so the host refreshes the grid +
        /// dirty visuals + undo/redo button states.
        /// </summary>
        public event EventHandler EditApplied;

        /// <summary>
        /// Applies the given edit command, pushes it on the undo stack, and truncates the redo tail
        /// (Apply-after-Undo invalidates the redo history per standard undo semantics).
        /// </summary>
        public void Apply(IObjectTemplateEditCommand command)
        {
            if (command == null) throw new ArgumentNullException("command");
            command.Do(document);
            undoStack.Push(command);
            redoStack.Clear();
            netAppliedCount++;
            RaiseEditApplied();
        }

        /// <summary>
        /// Undoes the top command on the undo stack and pushes it on the redo stack. No-op when
        /// <see cref="CanUndo"/> is false.
        /// </summary>
        public void Undo()
        {
            if (!CanUndo) return;
            IObjectTemplateEditCommand command = undoStack.Pop();
            command.UndoOp(document);
            redoStack.Push(command);
            netAppliedCount--;
            RaiseEditApplied();
        }

        /// <summary>
        /// Redoes the top command on the redo stack and pushes it back on the undo stack. No-op when
        /// <see cref="CanRedo"/> is false.
        /// </summary>
        public void Redo()
        {
            if (!CanRedo) return;
            IObjectTemplateEditCommand command = redoStack.Pop();
            command.Do(document);
            undoStack.Push(command);
            netAppliedCount++;
            RaiseEditApplied();
        }

        /// <summary>
        /// Resets the dirty baseline after a successful save. Sets <c>netAppliedCount = 0</c> so
        /// <see cref="IsDirty"/> reads false immediately after a save. The undo/redo stacks are NOT
        /// touched — the user can still undo across a save boundary; only the dirty indicator resets.
        /// The form's save handlers call this on save success.
        /// </summary>
        public void MarkSaved()
        {
            netAppliedCount = 0;
            RaiseEditApplied();
        }

        private void RaiseEditApplied()
        {
            EventHandler h = EditApplied;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
