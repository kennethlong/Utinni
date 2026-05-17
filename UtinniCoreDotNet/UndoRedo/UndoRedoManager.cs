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

using System;
using System.Collections.Generic;
using UtinniCoreDotNet.Callbacks;
using UtinniCoreDotNet.PluginFramework;

namespace UtinniCoreDotNet.UndoRedo
{
    public class UndoRedoManager
    {
        // C-07: lock-object for thread-safety. All stack mutations (AddUndoCommand, Undo,
        // Redo, OnCleanupCallback) acquire syncRoot before touching UndoCommands or
        // RedoCommands. The lock is released BEFORE calling external callbacks (cmd.Undo,
        // cmd.Execute, onUpdateCommandsCallback) to avoid re-entrancy deadlock through
        // GroundSceneCallbacks.AddUpdateLoopCall (T-02-10 mitigation).
        private readonly object syncRoot = new object();

        private readonly Action onUpdateCommandsCallback;
        private readonly Action undoCallback;
        private readonly Action redoCallback;

        public readonly Stack<IUndoCommand> UndoCommands;
        public readonly Stack<IUndoCommand> RedoCommands;

        // C-07 + Phase-1 D-06 testability seam: registerCleanupCallback defaults to
        // GameCallbacks.AddCleanupSceneCall (production behavior preserved). Tests pass
        // a no-op lambda (_ => {}) to avoid invoking the native GameCallbacks bridge.
        public UndoRedoManager(Action onUpdateCommandsCallback, Action undoCallback, Action redoCallback,
            Action<Action> registerCleanupCallback = null)
        {
            UndoCommands = new Stack<IUndoCommand>();
            RedoCommands = new Stack<IUndoCommand>();

            this.onUpdateCommandsCallback = onUpdateCommandsCallback;
            this.undoCallback = undoCallback;
            this.redoCallback = redoCallback;

            (registerCleanupCallback ?? GameCallbacks.AddCleanupSceneCall)(OnCleanupCallback);
        }

        // CON-M-05 preservation: cleanup-on-scene-cleanup behavior is preserved verbatim —
        // both stacks are cleared and the UI update callback fires. Only wrapped in lock.
        private void OnCleanupCallback()
        {
            lock (syncRoot)
            {
                UndoCommands.Clear();
                RedoCommands.Clear();
            }
            onUpdateCommandsCallback();
        }

        public void AddUndoCommand(IEditorPlugin editorPlugin)
        {
            editorPlugin.AddUndoCommand += (sender, args) =>
            {
                bool merged;
                lock (syncRoot)
                {
                    // C-07: call AllowMerge() BEFORE Merge() to gate cheap merging.
                    // TD-29 sub-bug: RedoCommands.Clear() now happens AFTER the merge check —
                    // clearing redo is only correct when a new command is actually being pushed.
                    if (UndoCommands.Count > 0
                        && UndoCommands.Peek().AllowMerge()
                        && UndoCommands.Peek().Merge(args.UndoCommand))
                    {
                        merged = true;
                    }
                    else
                    {
                        merged = false;
                        RedoCommands.Clear();
                        UndoCommands.Push(args.UndoCommand);
                    }
                }
                if (!merged)
                {
                    onUpdateCommandsCallback();
                }
            };
        }

        public void Undo()
        {
            IUndoCommand cmd;
            lock (syncRoot)
            {
                if (UndoCommands.Count == 0)
                {
                    return;
                }
                cmd = UndoCommands.Pop();
                RedoCommands.Push(cmd);
            }
            // Release lock before calling cmd.Undo() + undoCallback() to avoid
            // re-entrancy deadlock (T-02-10 mitigation).
            cmd.Undo();
            undoCallback();
        }

        public void Undo(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Undo();
            }
        }

        public void Redo()
        {
            IUndoCommand cmd;
            lock (syncRoot)
            {
                if (RedoCommands.Count == 0)
                {
                    return;
                }
                cmd = RedoCommands.Pop();
                UndoCommands.Push(cmd);
            }
            // Release lock before calling cmd.Execute() + redoCallback().
            cmd.Execute();
            redoCallback();
        }

        public void Redo(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Redo();
            }
        }
    }
}
