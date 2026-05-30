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

using System.Collections.Generic;
using System.Linq;
using UtinniCoreDotNet.Formats.StringTable;
using Xunit;

namespace UtinniCoreDotNet.Tests.FormatsTests.StringTable
{
    /// <summary>
    /// Coverage for <see cref="StringTableCsvCoercion"/> — the framework-side per-entry CSV diff planner
    /// (Plan 10-04 D-03b). The flat-format analog of the Phase 9 <c>CsvCellCoercion</c> tests: the diff is
    /// keyed by NAME (equal text → Unchanged / preserve original bytes; differ → Changes; new key →
    /// Added), the F8 invalid-key guard flags bad/empty/duplicate keys into <c>Invalid</c>, and the DoS
    /// caps throw before allocation.
    /// </summary>
    public class StringTableCsvCoercionTests
    {
        // BuildV1MultiEntry: ids 1/2/3 named alpha/beta/gamma, texts first/second/third.
        private static MutableStringTableDocument MultiEntryDoc()
        {
            return StringTableDocument.FromBytes(StringTableFixtures.BuildV1MultiEntry()).Mutable;
        }

        private static List<KeyValuePair<string, string>> Rows(params (string key, string text)[] rows)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (var r in rows) list.Add(new KeyValuePair<string, string>(r.key, r.text));
            return list;
        }

        [Fact]
        public void AllMatch_ZeroChanges_AllUnchanged()
        {
            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                MultiEntryDoc(), Rows(("alpha", "first"), ("beta", "second"), ("gamma", "third")));

            Assert.Empty(plan.Changes);
            Assert.Empty(plan.Added);
            Assert.Equal(3, plan.Unchanged.Count);
            Assert.False(plan.HasBlockingErrors);
        }

        [Fact]
        public void OneDiffers_OneChange_OthersUnchanged()
        {
            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                MultiEntryDoc(), Rows(("alpha", "CHANGED"), ("beta", "second"), ("gamma", "third")));

            Assert.Single(plan.Changes);
            Assert.Equal("alpha", plan.Changes[0].Entry.Name);
            Assert.Equal("CHANGED", plan.Changes[0].NewText);
            Assert.Equal(2, plan.Unchanged.Count);
        }

        [Fact]
        public void NewKey_GoesToAdded()
        {
            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                MultiEntryDoc(), Rows(("delta", "fourth")));

            Assert.Single(plan.Added);
            Assert.Equal("delta", plan.Added[0].Key);
            Assert.Equal("fourth", plan.Added[0].Text);
            Assert.Empty(plan.Changes);
        }

        [Fact]
        public void MissingKey_LeftUntouched()
        {
            // Only alpha is in the CSV — beta + gamma are simply absent from the plan (untouched).
            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                MultiEntryDoc(), Rows(("alpha", "first")));

            Assert.Single(plan.Unchanged);
            Assert.Empty(plan.Changes);
            Assert.Empty(plan.Added);
        }

        [Fact]
        public void Sc4_ReimportCurrentValues_ProducesNoChanges()
        {
            MutableStringTableDocument doc = MultiEntryDoc();
            var rows = doc.Entries
                .Select(e => new KeyValuePair<string, string>(e.Name, e.Text))
                .ToList();

            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(doc, rows);

            Assert.Empty(plan.Changes);
            Assert.Empty(plan.Added);
            Assert.Equal(3, plan.Unchanged.Count);
        }

        [Fact]
        public void SerializeRowToCsv_EscapesCommaQuoteAndNewline()
        {
            Assert.Equal("key,value", StringTableCsvCoercion.SerializeRowToCsv("key", "value"));
            Assert.Equal("key,\"a,b\"", StringTableCsvCoercion.SerializeRowToCsv("key", "a,b"));
            Assert.Equal("key,\"a\"\"b\"", StringTableCsvCoercion.SerializeRowToCsv("key", "a\"b"));
            Assert.Equal("key,\"line1\nline2\"", StringTableCsvCoercion.SerializeRowToCsv("key", "line1\nline2"));
        }

        [Fact]
        public void OverRowCap_Throws()
        {
            var rows = new List<KeyValuePair<string, string>>(StringTableCsvCoercion.MaxRows + 1);
            for (int i = 0; i < StringTableCsvCoercion.MaxRows + 1; i++)
            {
                rows.Add(new KeyValuePair<string, string>("k", "v"));
            }

            Assert.Throws<StringTableCsvParseException>(() => StringTableCsvCoercion.PlanImport(MultiEntryDoc(), rows));
        }

        [Fact]
        public void OverCellCap_Throws()
        {
            string huge = new string('x', StringTableCsvCoercion.MaxCellChars + 1);
            Assert.Throws<StringTableCsvParseException>(
                () => StringTableCsvCoercion.PlanImport(MultiEntryDoc(), Rows(("akey", huge))));
        }

        // ── F8 invalid-key guard ──

        [Fact]
        public void F8_BadCharsetKey_Flagged()
        {
            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                MultiEntryDoc(), Rows(("Bad Key", "x")));   // uppercase + space

            Assert.True(plan.HasBlockingErrors);
            Assert.Single(plan.Invalid);
            Assert.Equal("Bad Key", plan.Invalid[0].Key);
            Assert.Empty(plan.Added);
        }

        [Fact]
        public void F8_EmptyKey_Flagged()
        {
            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                MultiEntryDoc(), Rows(("", "x")));

            Assert.True(plan.HasBlockingErrors);
            Assert.Equal("String name can't be empty.", plan.Invalid[0].Reason);
        }

        [Fact]
        public void F8_LeadingDigitKey_Flagged()
        {
            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                MultiEntryDoc(), Rows(("9bad", "x")));

            Assert.True(plan.HasBlockingErrors);
            Assert.Contains("digit", plan.Invalid[0].Reason);
        }

        [Fact]
        public void F8_DuplicateCsvRow_Flagged()
        {
            // The first "newkey" is a valid add; the second is flagged duplicate-in-CSV.
            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                MultiEntryDoc(), Rows(("newkey", "a"), ("newkey", "b")));

            Assert.True(plan.HasBlockingErrors);
            Assert.Single(plan.Added);
            Assert.Single(plan.Invalid);
            Assert.Contains("more than once", plan.Invalid[0].Reason);
        }

        [Fact]
        public void F8_UpdateExistingKey_IsNotFlaggedAsDuplicate()
        {
            // Re-using an EXISTING key to UPDATE its text must NOT be flagged a duplicate (exclude-self).
            StringTableCsvImportPlan plan = StringTableCsvCoercion.PlanImport(
                MultiEntryDoc(), Rows(("beta", "updated")));

            Assert.False(plan.HasBlockingErrors);
            Assert.Single(plan.Changes);
        }
    }
}
