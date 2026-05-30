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
    /// Coverage for <see cref="StringTablePoExport"/> — the hand-rolled PO/gettext EXPORT (Plan 10-04
    /// D-03d, export-only). Asserts the msgid/msgstr framing, gettext escaping, and non-ASCII survival.
    /// </summary>
    public class StringTablePoExportTests
    {
        private static MutableStringTableDocument Doc(byte[] bytes)
        {
            return StringTableDocument.FromBytes(bytes).Mutable;
        }

        [Fact]
        public void ToPo_EmitsMsgidMsgstrFraming()
        {
            string po = StringTablePoExport.ToPo(Doc(StringTableFixtures.BuildV1Minimal())); // greeting → "Hello"
            Assert.Contains("msgid \"greeting\"", po);
            Assert.Contains("msgstr \"Hello\"", po);
        }

        [Fact]
        public void ToPo_OneEntryPerNamedString()
        {
            string po = StringTablePoExport.ToPo(Doc(StringTableFixtures.BuildV1MultiEntry())); // alpha/beta/gamma
            int msgidCount = po.Split('\n').Count(l => l.StartsWith("msgid "));
            Assert.Equal(3, msgidCount);
        }

        [Fact]
        public void ToPo_EscapesQuotesAndNewlines()
        {
            MutableStringTableDocument doc = Doc(StringTableFixtures.BuildV1Minimal());
            doc.Entries.First().Text = "line1\nwith \"quotes\"";

            string po = StringTablePoExport.ToPo(doc);

            Assert.Contains("\\n", po);       // newline escaped
            Assert.Contains("\\\"quotes\\\"", po); // double-quotes escaped
        }

        [Fact]
        public void ToPo_NonAsciiTextSurvives()
        {
            string po = StringTablePoExport.ToPo(Doc(StringTableFixtures.BuildV1Joao())); // "João"
            Assert.Contains("msgstr \"João\"", po);
        }

        [Fact]
        public void ToPo_SkipsNamelessEntries()
        {
            // BuildV1NoNameBlock has two string entries and ZERO name rows → no msgid keys to emit.
            string po = StringTablePoExport.ToPo(Doc(StringTableFixtures.BuildV1NoNameBlock()));
            Assert.DoesNotContain("msgid", po);
        }
    }
}
