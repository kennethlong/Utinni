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

using System.IO;
using System.Linq;
using System.Text;
using UtinniCoreDotNet.Formats.StringTable;
using Xunit;

namespace UtinniCoreDotNet.Tests.FormatsTests.StringTable
{
    /// <summary>Coverage for the flat-binary reader <see cref="StringTableDocument.FromBytes"/> (Task 1 of 10-01).</summary>
    public class StringTableDocumentTests
    {
        private static MutableStringTableEntry ById(StringTableDocument doc, uint id)
        {
            return doc.Mutable.Entries.First(e => e.Id == id);
        }

        [Fact]
        public void Parse_V1Minimal_HeaderAndEntry()
        {
            StringTableDocument doc = StringTableDocument.FromBytes(StringTableFixtures.BuildV1Minimal());
            Assert.Equal(1, doc.Version);
            Assert.Equal(2u, doc.NextUniqueId);
            Assert.Single(doc.Mutable.Entries);

            MutableStringTableEntry e = ById(doc, 1);
            Assert.Equal("Hello", e.Text);
            Assert.Equal("greeting", e.Name);
            Assert.Equal(100u, e.SourceCrc);
            Assert.False(e.IsDirty);
        }

        [Fact]
        public void Parse_V0Legacy_VersionZeroAccepted()
        {
            StringTableDocument doc = StringTableDocument.FromBytes(StringTableFixtures.BuildV0Legacy());
            Assert.Equal(0, doc.Version);
            Assert.Equal("legacy", ById(doc, 1).Name);
        }

        [Fact]
        public void Parse_Joao_NonAsciiTextRoundTripsToString()
        {
            StringTableDocument doc = StringTableDocument.FromBytes(StringTableFixtures.BuildV1Joao());
            Assert.Equal("João", ById(doc, 5).Text);
        }

        [Fact]
        public void Parse_MultiEntry_NameTableJoin()
        {
            StringTableDocument doc = StringTableDocument.FromBytes(StringTableFixtures.BuildV1MultiEntry());
            Assert.Equal(3, doc.Mutable.Entries.Count);
            Assert.Equal("alpha", ById(doc, 1).Name);
            Assert.Equal("beta", ById(doc, 2).Name);
            Assert.Equal("gamma", ById(doc, 3).Name);
            Assert.Equal("second", ById(doc, 2).Text);
        }

        [Fact]
        public void Parse_EmptyTable_NoEntries()
        {
            StringTableDocument doc = StringTableDocument.FromBytes(StringTableFixtures.BuildEmpty());
            Assert.Empty(doc.Mutable.Entries);
            Assert.Equal(1u, doc.NextUniqueId);
        }

        [Fact]
        public void Parse_NonBmp_SurrogatePairPreserved()
        {
            StringTableDocument doc = StringTableDocument.FromBytes(StringTableFixtures.BuildV1NonBmp());
            string text = ById(doc, 4).Text;
            Assert.Equal("𐐷", text);
            Assert.Equal(2, text.Length); // 2 UTF-16 code units
        }

        [Fact]
        public void Parse_MalformedUtf16_ReplacedNotThrown()
        {
            StringTableDocument doc = StringTableDocument.FromBytes(StringTableFixtures.BuildV1MalformedUtf16());
            // A lone high surrogate decodes to U+FFFD via the replacement fallback (does not throw).
            Assert.Equal("�", ById(doc, 9).Text);
        }

        [Fact]
        public void Parse_NoNameBlock_EntriesNamelessWithWarning()
        {
            StringTableDocument doc = StringTableDocument.FromBytes(StringTableFixtures.BuildV1NoNameBlock());
            Assert.Equal(2, doc.Mutable.Entries.Count);
            Assert.All(doc.Mutable.Entries, e => Assert.Null(e.Name));
            Assert.NotEmpty(doc.Warnings);
        }

        [Fact]
        public void Parse_PartialNameBlock_NamedAndNamelessTolerated()
        {
            StringTableDocument doc = StringTableDocument.FromBytes(StringTableFixtures.BuildV1PartialNameBlock());
            Assert.Equal("first_key", ById(doc, 1).Name);
            Assert.Null(ById(doc, 2).Name);
            Assert.Equal("third_key", ById(doc, 3).Name);
            Assert.NotEmpty(doc.Warnings);
        }

        [Fact]
        public void Parse_BadMagic_Throws()
        {
            byte[] bad = StringTableFixtures.BuildV1Minimal();
            bad[0] = 0x00; bad[1] = 0x00; // corrupt magic
            Assert.Throws<StringTableParseException>(() => StringTableDocument.FromBytes(bad));
        }

        [Fact]
        public void Parse_UnsupportedVersion_Throws()
        {
            byte[] bad = StringTableFixtures.BuildV1Minimal();
            bad[4] = 2; // version byte (after the 4-byte magic)
            Assert.Throws<StringTableParseException>(() => StringTableDocument.FromBytes(bad));
        }

        [Fact]
        public void Parse_ForgedCount_Throws()
        {
            byte[] data;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((uint)0xABCD);
                bw.Write((byte)1);
                bw.Write(0u);            // nextId
                bw.Write(1000000u);      // forged count, far beyond the bytes present
                bw.Flush();
                data = ms.ToArray();
            }

            Assert.Throws<StringTableParseException>(() => StringTableDocument.FromBytes(data));
        }

        [Fact]
        public void Parse_NullData_Throws()
        {
            Assert.Throws<StringTableParseException>(() => StringTableDocument.FromBytes(null));
        }
    }
}
