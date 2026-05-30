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
using System.Linq;
using UtinniCoreDotNet.Editing;
using UtinniCoreDotNet.Formats.StringTable;
using Xunit;

namespace UtinniCoreDotNet.Tests.FormatsTests.StringTable
{
    /// <summary>Coverage for <see cref="StringTableEditController"/> + the four T4 commands (Task 2 of 10-01).</summary>
    public class StringTableEditControllerTests
    {
        private static StringTableDocument Load(byte[] bytes)
        {
            return StringTableDocument.FromBytes(bytes);
        }

        private static MutableStringTableEntry ById(StringTableDocument doc, uint id)
        {
            return doc.Mutable.Entries.First(e => e.Id == id);
        }

        // ── EditText: Apply / Undo / Redo identity ──

        [Fact]
        public void EditText_ApplyUndoRedo_Identity()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal());
            var controller = new StringTableEditController(doc.Mutable);
            MutableStringTableEntry e = ById(doc, 1);

            controller.Apply(StringTableEditCommands.EditText(e, "changed"));
            Assert.Equal("changed", e.Text);
            Assert.True(controller.IsDirty);

            controller.Undo();
            Assert.Equal("Hello", e.Text);
            Assert.False(controller.IsDirty);

            controller.Redo();
            Assert.Equal("changed", e.Text);
            Assert.True(controller.IsDirty);
        }

        // ── EditText → Undo → bytes EXACTLY equal loaded (SC4 via RestoreState) ──

        [Fact]
        public void EditText_Undo_BytesExactlyEqualLoaded()
        {
            byte[] original = StringTableFixtures.BuildV1Joao();
            StringTableDocument doc = Load(original);
            var controller = new StringTableEditController(doc.Mutable);

            controller.Apply(StringTableEditCommands.EditText(ById(doc, 5), "Lisboa"));
            controller.Undo();

            Assert.Equal(original, StringTableWriter.Serialize(doc.Mutable));
        }

        // ── AddEntry: auto-name + nextUniqueId + sourceCrc 0 ──

        [Fact]
        public void AddEntry_AutoNameAndNextUniqueId()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal()); // nextId 2
            var controller = new StringTableEditController(doc.Mutable);

            controller.Apply(StringTableEditCommands.AddEntry(doc.Mutable));
            MutableStringTableEntry added = ById(doc, 2);

            Assert.Equal("002_default", added.Name);
            Assert.Equal(0u, added.SourceCrc);
            Assert.Equal(3u, doc.Mutable.NextUniqueId);
        }

        [Fact]
        public void AddEntry_Undo_RestoresNextUniqueIdAndRemovesEntry()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal());
            var controller = new StringTableEditController(doc.Mutable);

            controller.Apply(StringTableEditCommands.AddEntry(doc.Mutable));
            Assert.Equal(2, doc.Mutable.Entries.Count);

            controller.Undo();
            Assert.Single(doc.Mutable.Entries);
            Assert.Equal(2u, doc.Mutable.NextUniqueId);

            // Redo re-adds the SAME id (stable).
            controller.Redo();
            Assert.Equal(3u, doc.Mutable.NextUniqueId);
            Assert.Equal(2u, ById(doc, 2).Id);
        }

        // ── RemoveEntry: insert-by-reference undo (CR-01) ──

        [Fact]
        public void RemoveEntry_Undo_ReInsertsSameInstanceAtOriginalIndex()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1MultiEntry());
            var controller = new StringTableEditController(doc.Mutable);

            MutableStringTableEntry middle = ById(doc, 2);
            int originalIndex = doc.Mutable.Entries.IndexOf(middle);

            controller.Apply(StringTableEditCommands.RemoveEntry(middle));
            Assert.Equal(2, doc.Mutable.Entries.Count);
            Assert.DoesNotContain(middle, doc.Mutable.Entries);

            controller.Undo();
            Assert.Equal(3, doc.Mutable.Entries.Count);
            Assert.Same(middle, doc.Mutable.Entries[originalIndex]); // SAME instance, original slot
        }

        // ── RenameKey: Apply / Undo + name validation rejections ──

        [Fact]
        public void RenameKey_ApplyUndo_Identity()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal());
            var controller = new StringTableEditController(doc.Mutable);
            MutableStringTableEntry e = ById(doc, 1);

            controller.Apply(StringTableEditCommands.RenameKey(e, "salutation"));
            Assert.Equal("salutation", e.Name);

            controller.Undo();
            Assert.Equal("greeting", e.Name);
        }

        [Fact]
        public void ValidateName_AcceptsValidKeys()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal());
            Assert.True(doc.Mutable.ValidateName("creature_name", null).Ok);
            Assert.True(doc.Mutable.ValidateName("foo+bar", null).Ok);
        }

        [Fact]
        public void ValidateName_RejectsLeadingDigit()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal());
            StringTableNameValidation r = doc.Mutable.ValidateName("3foo", null);
            Assert.False(r.Ok);
            Assert.Equal("String names can't start with a digit. Pick another key.", r.Reason);
        }

        [Fact]
        public void ValidateName_RejectsUppercaseAndSpace()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal());
            Assert.False(doc.Mutable.ValidateName("Foo", null).Ok);       // uppercase first char
            Assert.False(doc.Mutable.ValidateName("foo bar", null).Ok);   // illegal space char
        }

        [Fact]
        public void ValidateName_RejectsEmpty()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal());
            StringTableNameValidation r = doc.Mutable.ValidateName("", null);
            Assert.False(r.Ok);
            Assert.Equal("String name can't be empty.", r.Reason);
        }

        [Fact]
        public void ValidateName_RejectsDuplicate_ButExcludesSelf()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1MultiEntry());
            MutableStringTableEntry alpha = ById(doc, 1);

            // "beta" already belongs to id 2 → duplicate when proposed for id 1.
            StringTableNameValidation dup = doc.Mutable.ValidateName("beta", alpha);
            Assert.False(dup.Ok);
            Assert.Equal("A string named \"beta\" already exists. Pick another key.", dup.Reason);

            // The entry keeps its OWN name on rename (exclude self).
            Assert.True(doc.Mutable.ValidateName("alpha", alpha).Ok);
        }

        // ── MarkSaved: dirty baseline reset + rebaseline (F10) ──

        [Fact]
        public void MarkSaved_ResetsDirty_NextEditFlipsItTrue()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal());
            var controller = new StringTableEditController(doc.Mutable);

            controller.Apply(StringTableEditCommands.EditText(ById(doc, 1), "v1"));
            Assert.True(controller.IsDirty);

            controller.MarkSaved();
            Assert.False(controller.IsDirty);

            controller.Apply(StringTableEditCommands.EditText(ById(doc, 1), "v2"));
            Assert.True(controller.IsDirty);
        }

        [Fact]
        public void MarkSaved_Rebaseline_UndoReturnsPostSaveBytes()
        {
            byte[] original = StringTableFixtures.BuildV1Minimal();
            StringTableDocument doc = Load(original);
            var controller = new StringTableEditController(doc.Mutable);

            controller.Apply(StringTableEditCommands.EditText(ById(doc, 1), "v1"));
            controller.MarkSaved();
            byte[] postSave = StringTableWriter.Serialize(doc.Mutable);

            controller.Apply(StringTableEditCommands.EditText(ById(doc, 1), "v2"));
            controller.Undo();

            // Undo after a save returns the POST-save bytes (rebaselined), NOT the pre-first-edit bytes.
            Assert.Equal(postSave, StringTableWriter.Serialize(doc.Mutable));
            Assert.NotEqual(original, postSave);
        }

        // ── ApplyCsvImport: single-transaction undo (Plan 10-04 D-03b) ──

        private static List<KeyValuePair<string, string>> Rows(params (string key, string text)[] rows)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (var r in rows) list.Add(new KeyValuePair<string, string>(r.key, r.text));
            return list;
        }

        [Fact]
        public void ApplyCsvImport_MixedChangeAndAdd_OneUndoReversesAll()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1MultiEntry()); // alpha/beta/gamma, nextId 4
            var controller = new StringTableEditController(doc.Mutable);

            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                doc.Mutable, Rows(("alpha", "CHANGED"), ("delta", "fourth")));
            Assert.Single(plan.Changes);
            Assert.Single(plan.Added);
            Assert.False(plan.HasBlockingErrors);

            controller.Apply(StringTableEditCommands.ApplyCsvImport(doc.Mutable, plan));
            Assert.Equal("CHANGED", ById(doc, 1).Text);
            Assert.Equal(4, doc.Mutable.Entries.Count);                 // delta added
            Assert.Equal("delta", ById(doc, 4).Name);
            Assert.True(controller.IsDirty);

            // ONE undo entry reverses BOTH the text change and the add.
            controller.Undo();
            Assert.Equal("first", ById(doc, 1).Text);
            Assert.Equal(3, doc.Mutable.Entries.Count);
            Assert.Equal(4u, doc.Mutable.NextUniqueId);
            Assert.False(controller.IsDirty);
        }

        [Fact]
        public void ApplyCsvImport_Redo_ReappliesWholeTransaction()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1MultiEntry());
            var controller = new StringTableEditController(doc.Mutable);

            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                doc.Mutable, Rows(("beta", "B2"), ("delta", "fourth")));

            controller.Apply(StringTableEditCommands.ApplyCsvImport(doc.Mutable, plan));
            controller.Undo();
            controller.Redo();

            Assert.Equal("B2", ById(doc, 2).Text);
            Assert.Equal(4, doc.Mutable.Entries.Count);
            Assert.Equal("delta", ById(doc, 4).Name);
        }

        [Fact]
        public void ApplyCsvImport_ImportedEqualsCurrent_IsByteExact()
        {
            byte[] original = StringTableFixtures.BuildV1MultiEntry();
            StringTableDocument doc = Load(original);
            var controller = new StringTableEditController(doc.Mutable);

            // Re-import the CURRENT (name, text) of every entry → zero changes, zero adds (SC4).
            var rows = new List<KeyValuePair<string, string>>();
            foreach (MutableStringTableEntry e in doc.Mutable.Entries)
            {
                rows.Add(new KeyValuePair<string, string>(e.Name, e.Text));
            }

            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(doc.Mutable, rows);
            Assert.Empty(plan.Changes);
            Assert.Empty(plan.Added);
            Assert.Equal(3, plan.Unchanged.Count);

            controller.Apply(StringTableEditCommands.ApplyCsvImport(doc.Mutable, plan));

            // The no-op transaction leaves the table byte-identical (F2a zero-dirty short-circuit holds).
            Assert.Equal(original, StringTableWriter.Serialize(doc.Mutable));
        }

        [Fact]
        public void ApplyCsvImport_RefusesPlanWithBlockingErrors()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1MultiEntry());
            var controller = new StringTableEditController(doc.Mutable);

            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                doc.Mutable, Rows(("9bad", "x")));   // leading digit → invalid
            Assert.True(plan.HasBlockingErrors);

            Assert.Throws<InvalidOperationException>(
                () => controller.Apply(StringTableEditCommands.ApplyCsvImport(doc.Mutable, plan)));
            // Nothing was applied — the refusal happens inside Do before any mutation.
            Assert.Equal(3, doc.Mutable.Entries.Count);
            Assert.False(controller.IsDirty);
        }
    }
}
