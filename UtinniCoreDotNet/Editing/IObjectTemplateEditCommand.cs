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
// Editor-local object-template edit-command contract for the FormObjectTemplateEditor (Phase 11 D-04).
// The param-granularity analog of UtinniCoreDotNet.Editing.IDatatableEditCommand. Pure-managed (no UI
// dependency, NO scene UndoRedoManager reference per CON-M-05); xUnit-testable from CI.

using UtinniCoreDotNet.Formats.ObjectTemplate;

namespace UtinniCoreDotNet.Editing
{
    /// <summary>
    /// One undoable edit operation over a <see cref="MutableObjectTemplate"/>. The object-template
    /// analog of <see cref="IDatatableEditCommand"/>. Implementations capture the inverse state needed
    /// for a byte-exact <see cref="UndoOp"/> at construction (or first <see cref="Do"/>). The factory
    /// class <see cref="ObjectTemplateEditCommands"/> exposes one factory per D-04 mutation (edit a
    /// local override, add-override to promote an inherited field, remove-override to revert).
    /// </summary>
    public interface IObjectTemplateEditCommand
    {
        /// <summary>Apply (or re-apply for Redo) the edit to the document.</summary>
        void Do(MutableObjectTemplate document);

        /// <summary>Reverse the edit, restoring the prior state byte-exactly.</summary>
        void UndoOp(MutableObjectTemplate document);
    }
}
