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

using System.Linq;
using UtinniCoreDotNet.Formats.StringTable;
using Xunit;

namespace UtinniCoreDotNet.Tests.FormatsTests.StringTable
{
    /// <summary>
    /// Framework-side cover for the pure <see cref="MutableStringTableDocument.ValidateName"/> predicate
    /// (Plan 10-03). This predicate is the SINGLE source of truth the WinForms <c>FormStringTableEditor</c>
    /// Key-column cell-validator DELEGATES to (F3c — the form re-implements no subset of the rules). The
    /// full form behaviour is maintainer-smoke per the 10-03 Rule-3 deviation; these facts lock the rules.
    /// </summary>
    public class StringTableNameValidationTests
    {
        // BuildV1MultiEntry has named entries alpha / beta / gamma (ids 1/2/3).
        private static MutableStringTableDocument MultiEntryDoc()
        {
            return StringTableDocument.FromBytes(StringTableFixtures.BuildV1MultiEntry()).Mutable;
        }

        private static MutableStringTableEntry ByName(MutableStringTableDocument doc, string name)
        {
            return doc.Entries.First(e => e.Name == name);
        }

        [Fact]
        public void ValidName_Accepted()
        {
            StringTableNameValidation r = MultiEntryDoc().ValidateName("delta", null);
            Assert.True(r.Ok);
            Assert.Null(r.Reason);
        }

        [Fact]
        public void ValidCharset_LettersDigitsUnderscorePlus_Accepted()
        {
            // First char a lowercase letter; remaining chars in [a-z0-9_+].
            StringTableNameValidation r = MultiEntryDoc().ValidateName("abc_1+", null);
            Assert.True(r.Ok);
        }

        [Fact]
        public void EmptyName_Rejected()
        {
            StringTableNameValidation r = MultiEntryDoc().ValidateName("", null);
            Assert.False(r.Ok);
            Assert.Equal("String name can't be empty.", r.Reason);
        }

        [Fact]
        public void LeadingDigit_Rejected()
        {
            StringTableNameValidation r = MultiEntryDoc().ValidateName("1greeting", null);
            Assert.False(r.Ok);
            Assert.Equal("String names can't start with a digit. Pick another key.", r.Reason);
        }

        [Fact]
        public void UppercaseFirstChar_Rejected()
        {
            // The engine ruleset requires a LOWERCASE first letter, so an uppercase first char fails.
            StringTableNameValidation r = MultiEntryDoc().ValidateName("Greeting", null);
            Assert.False(r.Ok);
            Assert.Contains("lowercase", r.Reason);
        }

        [Fact]
        public void NonAsciiChar_Rejected()
        {
            // 'é' is outside [a-z0-9_+] — keys are ASCII-only (Q3); full UTF-16 is allowed for TEXT only.
            StringTableNameValidation r = MultiEntryDoc().ValidateName("café", null);
            Assert.False(r.Ok);
        }

        [Fact]
        public void DuplicateName_RejectedAgainstOtherEntry()
        {
            MutableStringTableDocument doc = MultiEntryDoc();
            MutableStringTableEntry alpha = ByName(doc, "alpha");
            // Renaming alpha to "beta" collides with the existing beta entry.
            StringTableNameValidation r = doc.ValidateName("beta", alpha);
            Assert.False(r.Ok);
            Assert.Equal("A string named \"beta\" already exists. Pick another key.", r.Reason);
        }

        [Fact]
        public void Rename_ExcludeSelf_KeepsOwnName()
        {
            MutableStringTableDocument doc = MultiEntryDoc();
            MutableStringTableEntry beta = ByName(doc, "beta");
            // Re-validating beta's own name with itself excluded must NOT flag a duplicate.
            StringTableNameValidation r = doc.ValidateName("beta", beta);
            Assert.True(r.Ok);
        }
    }
}
