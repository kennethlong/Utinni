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

namespace UtinniCoreDotNet.UndoRedo
{
   public interface IUndoCommand
    {
        // Gets the text displayed in the Undo/Redo lists
        string GetText();
        // Executes/redose the command
        void Execute();
        // Undos the command
        void Undo();
        // Returns true if this command may absorb the next command into itself via Merge().
        // Per docs/ai/undo-redo.html §AllowMerge, called by UndoRedoManager.AddUndoCommand
        // BEFORE Merge() to gate cheap merging of e.g. time-of-day slider drags.
        // C-07 disposition: KEEP this method — documented contract in undo-redo.html:54-55,184-185.
        bool AllowMerge();
        // Returns true if the merge was successful
        bool Merge(IUndoCommand newCommand);
    }
}
