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
// Editor-local undo/redo controller for the FormStringTableEditor (Phase 10 CF-04). The flat-format
// analog of UtinniCoreDotNet.Editing.DatatableEditController — a port of its Apply/Undo/Redo/
// netAppliedCount/IsDirty/MarkSaved idiom, MINUS the Phase-9 type-change cascade (.stf has no column
// types → no per-cell review seam). Independent of the scene-level undo plumbing (CON-M-05); pure-managed
// (no UI dependency); xUnit-testable from CI.

using System;
using System.Collections.Generic;
using UtinniCoreDotNet.Formats.StringTable;

namespace UtinniCoreDotNet.Editing
{
    /// <summary>
    /// Editor-local undo/redo controller over a <see cref="MutableStringTableDocument"/>.
    ///
    /// <para><b>Baseline-clean dirty semantics:</b> <see cref="IsDirty"/> is <c>netAppliedCount &gt; 0</c>
    /// — Apply increments, Undo decrements, Redo re-increments. Undo back to baseline reads clean even
    /// though touched entries still carry their dirty bit (so per-cell UI overlays still work).</para>
    ///
    /// <para><b>MarkSaved rebaseline (F10):</b> after a successful save (1) the document's current
    /// serialized bytes are captured as the new whole-file baseline (so the writer's zero-dirty
    /// short-circuit emits the post-save bytes); (2) each entry re-captures its current string-block
    /// bytes as its new per-entry baseline and clears its dirty/added bit; (3) the controller resets
    /// <c>netAppliedCount</c>. The undo/redo stacks are NOT cleared — the user can still undo across a
    /// save boundary; only the dirty indicator resets.</para>
    /// </summary>
    public sealed class StringTableEditController
    {
        private readonly MutableStringTableDocument document;
        private readonly Stack<IStringTableEditCommand> undoStack = new Stack<IStringTableEditCommand>();
        private readonly Stack<IStringTableEditCommand> redoStack = new Stack<IStringTableEditCommand>();

        // net edits applied relative to the baseline (Apply++; Undo--; Redo++). net == 0 → clean.
        private int netAppliedCount;

        /// <summary>
        /// Constructs a controller over the given mutable document. The current document state is the
        /// baseline for clean-dirty semantics.
        /// </summary>
        public StringTableEditController(MutableStringTableDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            this.document = document;
            this.netAppliedCount = 0;
        }

        /// <summary>The mutable document under edit.</summary>
        public MutableStringTableDocument Document { get { return document; } }

        /// <summary>True iff the document has at least one net-applied edit relative to the baseline.</summary>
        public bool IsDirty { get { return netAppliedCount > 0; } }

        /// <summary>True iff there is at least one applied command to undo.</summary>
        public bool CanUndo { get { return undoStack.Count > 0; } }

        /// <summary>True iff there is at least one undone command to redo.</summary>
        public bool CanRedo { get { return redoStack.Count > 0; } }

        /// <summary>Raised after every successful Apply / Undo / Redo / MarkSaved so the host refreshes.</summary>
        public event EventHandler EditApplied;

        /// <summary>
        /// Applies the given edit command, pushes it on the undo stack, and truncates the redo tail
        /// (Apply-after-Undo invalidates the redo history per standard undo semantics).
        /// </summary>
        public void Apply(IStringTableEditCommand command)
        {
            if (command == null) throw new ArgumentNullException("command");
            command.Do(document);
            undoStack.Push(command);
            redoStack.Clear();
            netAppliedCount++;
            RaiseEditApplied();
        }

        /// <summary>Undoes the top command on the undo stack and pushes it on the redo stack. No-op when <see cref="CanUndo"/> is false.</summary>
        public void Undo()
        {
            if (!CanUndo) return;
            IStringTableEditCommand command = undoStack.Pop();
            command.UndoOp(document);
            redoStack.Push(command);
            netAppliedCount--;
            RaiseEditApplied();
        }

        /// <summary>Redoes the top command on the redo stack and pushes it back on the undo stack. No-op when <see cref="CanRedo"/> is false.</summary>
        public void Redo()
        {
            if (!CanRedo) return;
            IStringTableEditCommand command = redoStack.Pop();
            command.Do(document);
            undoStack.Push(command);
            netAppliedCount++;
            RaiseEditApplied();
        }

        /// <summary>
        /// Resets the dirty baseline after a successful save (F10). Captures the document's current
        /// serialized bytes as the new whole-file baseline, rebaselines every entry, and zeroes the
        /// net-applied count. Undo/redo stacks are NOT touched.
        /// </summary>
        public void MarkSaved()
        {
            // Capture the CURRENT serialized bytes BEFORE rebaselining entries — once entries are clean
            // the writer's zero-dirty short-circuit would otherwise return the pre-save baseline.
            byte[] saved = StringTableWriter.Serialize(document);

            IList<MutableStringTableEntry> entries = document.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].RebaselineAfterSave();
            }

            document.SetOriginalFileBytes(saved);
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
