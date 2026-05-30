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
// Factory + concrete commands for the four D-01 T4 string-table edit ops (Phase 10). The flat-format
// analog of UtinniCoreDotNet.Editing.DatatableEditCommands — public static factories returning private
// nested IStringTableEditCommand implementations, each capturing inverse state for Undo. RemoveEntry
// re-inserts the SAME entry instance by reference (Phase 8 CR-01 insert-by-reference). Pure-managed
// (no UI dependency); xUnit-testable from CI.

using System;
using System.Collections.Generic;
using UtinniCoreDotNet.Formats.StringTable;

namespace UtinniCoreDotNet.Editing
{
    /// <summary>Factory methods for the four T4 edit commands: EditText, AddEntry, RemoveEntry, RenameKey.</summary>
    public static class StringTableEditCommands
    {
        /// <summary>
        /// Edit one entry's text (D-01 T4 text edit). Do captures the entry's full state via
        /// <c>CaptureState()</c> then sets the new text; UndoOp restores via <c>RestoreState</c> so the
        /// original byte slice + SourceCrc + dirty bit come back byte-exact (SC4). SourceCrc is
        /// preserved across the edit (D-02b — both the setter and RestoreState keep it).
        /// </summary>
        public static IStringTableEditCommand EditText(MutableStringTableEntry entry, string newText)
        {
            if (entry == null) throw new ArgumentNullException("entry");
            return new EditTextCommand(entry, newText);
        }

        /// <summary>
        /// Add a new engine-style entry (D-01 T4 add). Do allocates via <c>doc.AddEntry()</c> on first
        /// run (auto-id / auto-name <c>{NNN}_default</c> / sourceCrc=0) and re-inserts the SAME instance
        /// on Redo; UndoOp removes it and restores the prior <c>NextUniqueId</c>.
        /// </summary>
        public static IStringTableEditCommand AddEntry(MutableStringTableDocument doc)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            return new AddEntryCommand();
        }

        /// <summary>
        /// Remove the given entry (D-01 T4 remove). UndoOp re-inserts the SAME instance by reference at
        /// the original index (Phase 8 CR-01 insert-by-reference — preserves the entry's captured slice).
        /// </summary>
        public static IStringTableEditCommand RemoveEntry(MutableStringTableEntry entry)
        {
            if (entry == null) throw new ArgumentNullException("entry");
            return new RemoveEntryCommand(entry);
        }

        /// <summary>
        /// Rename an entry's key (D-01 T4 rename). The caller MUST pre-validate <paramref name="newName"/>
        /// via <see cref="MutableStringTableDocument.ValidateName"/> BEFORE Apply. Do captures state then
        /// sets the name; UndoOp restores.
        /// </summary>
        public static IStringTableEditCommand RenameKey(MutableStringTableEntry entry, string newName)
        {
            if (entry == null) throw new ArgumentNullException("entry");
            return new RenameKeyCommand(entry, newName);
        }

        /// <summary>
        /// Apply a whole CSV import as ONE undoable transaction (Phase 10 D-03b): every
        /// <see cref="StringTableCsvImportPlan.Changes"/> patch is an EditText and every
        /// <see cref="StringTableCsvImportPlan.Added"/> row is an AddEntry, all collapsed into a single
        /// undo entry (UndoOp reverses every change + removes every added entry).
        ///
        /// <para><b>Precondition (F8):</b> the plan MUST have <see cref="StringTableCsvImportPlan.HasBlockingErrors"/>
        /// false — the preview modal blocks Import upstream, so a plan with invalid rows never reaches
        /// here; <see cref="ApplyCsvImportCommand.Do"/> throws defensively if one does (entries are never
        /// created-then-found-invalid inside the transaction).</para>
        /// </summary>
        public static IStringTableEditCommand ApplyCsvImport(MutableStringTableDocument doc, StringTableCsvImportPlan plan)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (plan == null) throw new ArgumentNullException("plan");
            return new ApplyCsvImportCommand(plan);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Concrete commands — each captures inverse state for Undo
        // ─────────────────────────────────────────────────────────────────────

        private sealed class EditTextCommand : IStringTableEditCommand
        {
            private readonly MutableStringTableEntry entry;
            private readonly string newText;
            private MutableStringTableEntry.EntryState captured;
            private bool capturedFlag;

            public EditTextCommand(MutableStringTableEntry entry, string newText)
            {
                this.entry = entry;
                this.newText = newText;
            }

            public void Do(MutableStringTableDocument doc)
            {
                // Re-capture each Do (Apply + Redo) so UndoOp restores the state that immediately
                // preceded THIS application.
                captured = entry.CaptureState();
                capturedFlag = true;
                entry.Text = newText;
            }

            public void UndoOp(MutableStringTableDocument doc)
            {
                if (!capturedFlag) return;
                entry.RestoreState(captured);
            }
        }

        private sealed class RenameKeyCommand : IStringTableEditCommand
        {
            private readonly MutableStringTableEntry entry;
            private readonly string newName;
            private MutableStringTableEntry.EntryState captured;
            private bool capturedFlag;

            public RenameKeyCommand(MutableStringTableEntry entry, string newName)
            {
                this.entry = entry;
                this.newName = newName;
            }

            public void Do(MutableStringTableDocument doc)
            {
                captured = entry.CaptureState();
                capturedFlag = true;
                entry.Name = newName;
            }

            public void UndoOp(MutableStringTableDocument doc)
            {
                if (!capturedFlag) return;
                entry.RestoreState(captured);
            }
        }

        private sealed class AddEntryCommand : IStringTableEditCommand
        {
            private MutableStringTableEntry created;
            private int insertIndex;
            private uint priorNextUniqueId;
            private uint postNextUniqueId;

            public void Do(MutableStringTableDocument doc)
            {
                if (created == null)
                {
                    priorNextUniqueId = doc.NextUniqueId;
                    created = doc.AddEntry();
                    postNextUniqueId = doc.NextUniqueId;
                    insertIndex = doc.Entries.IndexOf(created);
                }
                else
                {
                    // Redo: re-insert the SAME instance by reference (keeps id stable) and restore the
                    // post-add high-water mark.
                    doc.Entries.Insert(insertIndex, created);
                    doc.SetNextUniqueId(postNextUniqueId);
                }
            }

            public void UndoOp(MutableStringTableDocument doc)
            {
                doc.Entries.Remove(created);
                doc.SetNextUniqueId(priorNextUniqueId);
            }
        }

        private sealed class RemoveEntryCommand : IStringTableEditCommand
        {
            private readonly MutableStringTableEntry entry;
            private int originalIndex;

            public RemoveEntryCommand(MutableStringTableEntry entry)
            {
                this.entry = entry;
            }

            public void Do(MutableStringTableDocument doc)
            {
                originalIndex = doc.Entries.IndexOf(entry);
                if (originalIndex >= 0)
                {
                    doc.Entries.RemoveAt(originalIndex);
                }
            }

            public void UndoOp(MutableStringTableDocument doc)
            {
                // Re-insert the SAME entry instance by reference at its original slot (CR-01) so a
                // subsequent Do() finds it and removes it cleanly, and the captured original slice +
                // dirty bits survive verbatim.
                if (originalIndex >= 0)
                {
                    doc.Entries.Insert(originalIndex, entry);
                }
            }
        }

        private sealed class ApplyCsvImportCommand : IStringTableEditCommand
        {
            private readonly StringTableCsvImportPlan plan;

            // Captured pre-change state for each applied EditText patch (re-captured each Do so a Redo
            // undoes back to the state that immediately preceded THIS application).
            private List<MutableStringTableEntry.EntryState> changedCaptured;

            // The entries created for the Added rows — held by reference so Redo re-inserts the SAME
            // instances (stable ids) and Undo removes them and restores the NextUniqueId high-water mark.
            private List<MutableStringTableEntry> createdEntries;
            private uint priorNextUniqueId;
            private uint postNextUniqueId;
            private bool created;

            public ApplyCsvImportCommand(StringTableCsvImportPlan plan)
            {
                this.plan = plan;
            }

            public void Do(MutableStringTableDocument doc)
            {
                // F8 defensive guard — a plan with invalid rows must be blocked by the preview modal and
                // never reach the transaction. Refuse rather than create-then-discover-invalid.
                if (plan.HasBlockingErrors)
                {
                    throw new InvalidOperationException(
                        "Refusing to apply a CSV import plan with " + plan.Invalid.Count + " invalid row(s).");
                }

                // Re-capture each Do (Apply + Redo) so UndoOp restores the immediately-preceding state.
                changedCaptured = new List<MutableStringTableEntry.EntryState>(plan.Changes.Count);
                for (int i = 0; i < plan.Changes.Count; i++)
                {
                    StringTableEditPatch p = plan.Changes[i];
                    changedCaptured.Add(p.Entry.CaptureState());
                    p.Entry.Text = p.NewText;
                }

                if (!created)
                {
                    priorNextUniqueId = doc.NextUniqueId;
                    createdEntries = new List<MutableStringTableEntry>(plan.Added.Count);
                    for (int i = 0; i < plan.Added.Count; i++)
                    {
                        MutableStringTableEntry e = doc.AddEntry();
                        e.Name = plan.Added[i].Key; // already ValidateName-passed in PlanImport.
                        e.Text = plan.Added[i].Text;
                        createdEntries.Add(e);
                    }
                    postNextUniqueId = doc.NextUniqueId;
                    created = true;
                }
                else
                {
                    // Redo: re-insert the SAME instances by reference + restore the post-add high-water.
                    for (int i = 0; i < createdEntries.Count; i++)
                    {
                        doc.Entries.Add(createdEntries[i]);
                    }
                    doc.SetNextUniqueId(postNextUniqueId);
                }
            }

            public void UndoOp(MutableStringTableDocument doc)
            {
                // Remove the added entries first, then restore each changed entry's captured state.
                if (createdEntries != null)
                {
                    for (int i = 0; i < createdEntries.Count; i++)
                    {
                        doc.Entries.Remove(createdEntries[i]);
                    }
                    doc.SetNextUniqueId(priorNextUniqueId);
                }

                if (changedCaptured != null)
                {
                    for (int i = plan.Changes.Count - 1; i >= 0; i--)
                    {
                        plan.Changes[i].Entry.RestoreState(changedCaptured[i]);
                    }
                }
            }
        }
    }
}
