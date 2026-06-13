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
using System.Threading;
using UtinniCoreDotNet.Hotkeys;
using UtinniCoreDotNet.PluginFramework;
using UtinniCoreDotNet.UI.Controls;
using UtinniCoreDotNet.UI.Forms;
using UtinniCoreDotNet.UndoRedo;
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    /// <summary>
    /// C-07 regression tests for UndoRedoManager:
    /// - Thread-safety via lock(syncRoot)
    /// - AllowMerge() gate called BEFORE Merge()
    /// - RedoCommands.Clear() ordering after merge check (TD-29 sub-bug)
    /// - Phase-1 D-06 testability seam (Action{Action} registerCleanupCallback)
    /// - CON-M-05 preservation: cleanup-on-scene-cleanup still clears both stacks
    /// </summary>
    public class UndoRedoManagerTests
    {
        // -------------------------------------------------------------------
        // Fixture: configurable IUndoCommand for controlling AllowMerge/Merge
        // -------------------------------------------------------------------
        private class StubCommand : IUndoCommand
        {
            private readonly bool _allowMerge;
            private readonly bool _mergeResult;

            public bool AllowMergeCalled { get; private set; }
            public bool MergeCalled { get; private set; }

            public StubCommand(bool allowMerge = false, bool mergeResult = false)
            {
                _allowMerge = allowMerge;
                _mergeResult = mergeResult;
            }

            public string GetText() => "StubCommand";
            public void Execute() { }
            public void Undo() { }

            public bool AllowMerge()
            {
                AllowMergeCalled = true;
                return _allowMerge;
            }

            public bool Merge(IUndoCommand newCommand)
            {
                MergeCalled = true;
                return _mergeResult;
            }
        }

        // -------------------------------------------------------------------
        // Fixture: IEditorPlugin shim. IEditorPlugin.AddUndoCommand is an
        // EventHandler<AddUndoCommandEventArgs> PROPERTY (not a C# event).
        // The stub exposes a backing field and a Raise helper.
        // -------------------------------------------------------------------
        private class StubEditorPlugin : IEditorPlugin
        {
            public EventHandler<AddUndoCommandEventArgs> AddUndoCommand { get; set; }

            // 15-10 editor-undo seam members (not exercised by these tests; present so the stub
            // satisfies the widened IEditorPlugin interface).
            public Action Undo { get; set; }
            public Action Redo { get; set; }
            public Action ClearUndoStack { get; set; }

            public void RaiseAddUndoCommand(IUndoCommand cmd)
            {
                AddUndoCommand?.Invoke(this, new AddUndoCommandEventArgs(cmd));
            }

            // IEditorPlugin / IPlugin members not used in these tests
            public PluginInformation Information => new PluginInformation("Stub", "", "");
            public UtinniCore.Utinni.UtINI GetConfig() => null;
            public HotkeyManager GetHotkeyManager() => null;
            public List<IEditorForm> GetForms() => null;
            public List<SubPanelContainer> GetStandalonePanels() => null;
            public List<SubPanel> GetSubPanels() => null;
        }

        // -------------------------------------------------------------------
        // Factory: creates a test-isolated UndoRedoManager (no native bridge)
        // -------------------------------------------------------------------
        private static UndoRedoManager MakeManager(Action<Action> registerCleanupCallback = null)
        {
            return new UndoRedoManager(
                onUpdateCommandsCallback: () => { },
                undoCallback: () => { },
                redoCallback: () => { },
                registerCleanupCallback: registerCleanupCallback ?? (_ => { }));
        }

        // ===================================================================
        // Test 1: thread-safety — concurrent pushes from multiple threads
        // ===================================================================
        [Fact]
        public void AddUndoCommand_ConcurrentPushFromMultipleThreads_NoRaceConditions()
        {
            // Arrange
            const int threadCount = 10;
            const int commandsPerThread = 100;
            var mgr = MakeManager();
            var plugin = new StubEditorPlugin();
            mgr.AddUndoCommand(plugin);

            var exceptions = new List<Exception>();
            var threads = new Thread[threadCount];

            for (int t = 0; t < threadCount; t++)
            {
                threads[t] = new Thread(() =>
                {
                    try
                    {
                        for (int i = 0; i < commandsPerThread; i++)
                        {
                            // AllowMerge returns false => each command is pushed
                            plugin.RaiseAddUndoCommand(new StubCommand(allowMerge: false));
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) { exceptions.Add(ex); }
                    }
                });
            }

            // Act
            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join(TimeSpan.FromSeconds(10));

            // Assert
            Assert.Empty(exceptions);
            Assert.Equal(threadCount * commandsPerThread, mgr.UndoCommands.Count);
        }

        // ===================================================================
        // Test 2: AllowMerge returns true + Merge returns true => redo NOT cleared
        // ===================================================================
        [Fact]
        public void AddUndoCommand_MergeReturnsTrue_DoesNotClearRedoStack()
        {
            // Arrange
            var mgr = MakeManager();
            var plugin = new StubEditorPlugin();
            mgr.AddUndoCommand(plugin);

            // Push an initial command that allows and accepts merging
            var mergeableTop = new StubCommand(allowMerge: true, mergeResult: true);
            plugin.RaiseAddUndoCommand(mergeableTop);
            Assert.Equal(1, mgr.UndoCommands.Count);

            // RedoCommands is empty at this point; record count
            int redoCountBefore = mgr.RedoCommands.Count; // 0

            // Act: trigger a second command — top allows AND accepts merge
            plugin.RaiseAddUndoCommand(new StubCommand(allowMerge: false));

            // Assert: merged => undo stack count unchanged (merge happened, no new push)
            Assert.Equal(1, mgr.UndoCommands.Count);
            // Redo stack was NOT cleared (Clear only runs on new-push path; TD-29 fix)
            Assert.Equal(redoCountBefore, mgr.RedoCommands.Count);
        }

        // ===================================================================
        // Test 3: AllowMerge returns false => redo stack IS cleared; new command pushed
        // ===================================================================
        [Fact]
        public void AddUndoCommand_NewCommandActuallyPushed_DoesClearRedoStack()
        {
            // Arrange
            var mgr = MakeManager();
            var plugin = new StubEditorPlugin();
            mgr.AddUndoCommand(plugin);

            // Push one command and undo it so redo stack has 1 item
            plugin.RaiseAddUndoCommand(new StubCommand(allowMerge: false));
            mgr.Undo();
            Assert.Equal(1, mgr.RedoCommands.Count);
            Assert.Equal(0, mgr.UndoCommands.Count);

            // Act: push a new non-mergeable command when undo stack is empty
            plugin.RaiseAddUndoCommand(new StubCommand(allowMerge: false));

            // Assert: redo cleared, undo has 1
            Assert.Equal(0, mgr.RedoCommands.Count);
            Assert.Equal(1, mgr.UndoCommands.Count);
        }

        // ===================================================================
        // Test 4: empty undo stack — first push clears redo and pushes command
        // ===================================================================
        [Fact]
        public void AddUndoCommand_NoExistingTop_PushesAndClearsRedo()
        {
            // Arrange
            var mgr = MakeManager();
            var plugin = new StubEditorPlugin();
            mgr.AddUndoCommand(plugin);

            // Push 1 command then undo to get 1 item on redo stack
            plugin.RaiseAddUndoCommand(new StubCommand());
            mgr.Undo();
            Assert.Equal(1, mgr.RedoCommands.Count);
            Assert.Equal(0, mgr.UndoCommands.Count);

            // Act: push a new command with empty undo stack (AllowMerge won't fire since Count==0)
            plugin.RaiseAddUndoCommand(new StubCommand(allowMerge: false));

            // Assert: redo cleared, undo has 1
            Assert.Equal(0, mgr.RedoCommands.Count);
            Assert.Equal(1, mgr.UndoCommands.Count);
        }

        // ===================================================================
        // Test 5: OnCleanupCallback clears both stacks (CON-M-05 preservation)
        // ===================================================================
        [Fact]
        public void OnCleanupCallback_ClearsBothStacks_PreservedCONM05()
        {
            // Arrange: capture the cleanup action via the testability seam
            Action capturedCleanup = null;
            var mgr = MakeManager(registerCleanupCallback: a => capturedCleanup = a);

            var plugin = new StubEditorPlugin();
            mgr.AddUndoCommand(plugin);

            // Push 3 undo commands
            for (int i = 0; i < 3; i++) plugin.RaiseAddUndoCommand(new StubCommand());
            Assert.Equal(3, mgr.UndoCommands.Count);

            // Undo 2 to populate redo stack
            mgr.Undo(); mgr.Undo();
            Assert.Equal(2, mgr.RedoCommands.Count);

            // Act: simulate scene cleanup
            Assert.NotNull(capturedCleanup);
            capturedCleanup();

            // Assert: both stacks cleared (CON-M-05)
            Assert.Equal(0, mgr.UndoCommands.Count);
            Assert.Equal(0, mgr.RedoCommands.Count);
        }

        // ===================================================================
        // Test 6: 15-10 — public Clear() empties both stacks AND fires the
        // UI update callback (so the titlebar Undo/Redo buttons disable after
        // a programmatic clear on a snapshot boundary).
        // ===================================================================
        [Fact]
        public void Clear_EmptiesBothStacks_AndFiresUpdateCallback()
        {
            // Arrange: count update-callback fires via the onUpdateCommandsCallback seam.
            int updateCallbackFires = 0;
            var mgr = new UndoRedoManager(
                onUpdateCommandsCallback: () => updateCallbackFires++,
                undoCallback: () => { },
                redoCallback: () => { },
                registerCleanupCallback: _ => { });

            var plugin = new StubEditorPlugin();
            mgr.AddUndoCommand(plugin);

            // Push 3 commands, undo 1 so BOTH stacks are non-empty.
            for (int i = 0; i < 3; i++) plugin.RaiseAddUndoCommand(new StubCommand());
            mgr.Undo();
            Assert.Equal(2, mgr.UndoCommands.Count);
            Assert.Equal(1, mgr.RedoCommands.Count);

            int firesBeforeClear = updateCallbackFires;

            // Act
            mgr.Clear();

            // Assert: both stacks empty + the update callback fired exactly once.
            Assert.Equal(0, mgr.UndoCommands.Count);
            Assert.Equal(0, mgr.RedoCommands.Count);
            Assert.Equal(firesBeforeClear + 1, updateCallbackFires);
        }
    }
}
