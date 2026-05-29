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
// Editor-local datatable edit-command contract for the FormDatatableEditor (Phase 9 D-01 T4).
// The cell-/row-/column-granularity analog of UtinniCoreDotNet.Editing.IIffEditCommand
// (IffEditController.cs:167-173). Pure-managed (no UI dependency); xUnit-testable from CI.

using UtinniCoreDotNet.Formats.Datatable;

namespace UtinniCoreDotNet.Editing
{
    /// <summary>
    /// One undoable edit operation over a <see cref="MutableDataTableDocument"/>. The datatable analog
    /// of <c>IIffEditCommand</c> (<c>IffEditController.cs:167-173</c>). Implementations capture inverse
    /// state at construction (or at first <see cref="Do"/>) so <see cref="UndoOp"/> can reverse the
    /// change. The factory class <see cref="DatatableEditCommands"/> exposes one factory per supported
    /// D-01 T4 op (cell edit, row add/remove/reorder, column add/remove/reorder, rename, type change).
    /// </summary>
    public interface IDatatableEditCommand
    {
        /// <summary>Apply (or re-apply for Redo) the edit to the document.</summary>
        void Do(MutableDataTableDocument document);

        /// <summary>Reverse the edit, restoring the prior state.</summary>
        void UndoOp(MutableDataTableDocument document);
    }
}
