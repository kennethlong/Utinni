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
// Editor-local undo/redo command contract for the String-table editor (Phase 10 CF-04). The flat-format
// analog of IDatatableEditCommand. Pure-managed (no UI dependency); xUnit-testable from CI.

using UtinniCoreDotNet.Formats.StringTable;

namespace UtinniCoreDotNet.Editing
{
    /// <summary>
    /// One undoable edit operation over a <see cref="MutableStringTableDocument"/>. Implementations
    /// capture inverse state (at construction or at first <see cref="Do"/>) so <see cref="UndoOp"/> can
    /// reverse the change byte-exact.
    /// </summary>
    public interface IStringTableEditCommand
    {
        /// <summary>Apply (or re-apply for Redo) the edit to the document.</summary>
        void Do(MutableStringTableDocument document);

        /// <summary>Reverse the edit, restoring the prior state.</summary>
        void UndoOp(MutableStringTableDocument document);
    }
}
