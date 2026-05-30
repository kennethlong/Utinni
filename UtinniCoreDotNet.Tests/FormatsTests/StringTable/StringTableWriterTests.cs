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
    /// <summary>Coverage for the byte-exact writer <see cref="StringTableWriter"/> (Task 1 of 10-01).</summary>
    public class StringTableWriterTests
    {
        private static StringTableDocument Load(byte[] bytes)
        {
            return StringTableDocument.FromBytes(bytes);
        }

        private static byte[] Serialize(StringTableDocument doc)
        {
            return StringTableWriter.Serialize(doc.Mutable);
        }

        private static MutableStringTableEntry ById(StringTableDocument doc, uint id)
        {
            return doc.Mutable.Entries.First(e => e.Id == id);
        }

        private static bool Contains(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                }

                if (match) return true;
            }

            return false;
        }

        // ── Untouched byte-exact round-trip (F2a zero-dirty short-circuit) ──

        [Fact]
        public void RoundTrip_V1Minimal_ByteExact()
        {
            byte[] original = StringTableFixtures.BuildV1Minimal();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_V1MultiEntry_ByteExact()
        {
            byte[] original = StringTableFixtures.BuildV1MultiEntry();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_Joao_ByteExact()
        {
            byte[] original = StringTableFixtures.BuildV1Joao();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_Empty_ByteExact()
        {
            byte[] original = StringTableFixtures.BuildEmpty();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_V0Legacy_ByteExact()
        {
            byte[] original = StringTableFixtures.BuildV0Legacy();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_NonBmp_ByteExact()
        {
            byte[] original = StringTableFixtures.BuildV1NonBmp();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_MalformedUtf16_ByteExact()
        {
            byte[] original = StringTableFixtures.BuildV1MalformedUtf16();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_NoNameBlock_ByteExact()
        {
            byte[] original = StringTableFixtures.BuildV1NoNameBlock();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void RoundTrip_PartialNameBlock_ByteExact()
        {
            byte[] original = StringTableFixtures.BuildV1PartialNameBlock();
            Assert.Equal(original, Serialize(Load(original)));
        }

        [Fact]
        public void ZeroDirty_ShortCircuit_ReturnsOriginalEvenForNonCanonical()
        {
            // A non-canonically-ordered file, untouched, re-emits its ORIGINAL bytes verbatim (F2a)
            // — the short-circuit neutralizes the canonical-ordering assumption for the no-edit case.
            byte[] original = StringTableFixtures.BuildV1NonCanonicalOrder();
            Assert.Equal(original, Serialize(Load(original)));
        }

        // ── F2b: untouched malformed entry survives byte-identically when another entry is dirty ──

        [Fact]
        public void DirtyOneEntry_UntouchedMalformedEntryReEmittedVerbatim()
        {
            byte[] original = StringTableFixtures.BuildV1MalformedPlusNormal();
            StringTableDocument doc = Load(original);

            // Edit the NORMAL entry → forces full re-serialize (no short-circuit).
            ById(doc, 1).Text = "edited";
            byte[] output = Serialize(doc);

            // The untouched malformed entry's lone-surrogate bytes (00 D8) survive verbatim — a fresh
            // re-encode of U+FFFD would have emitted FD FF instead.
            Assert.True(Contains(output, new byte[] { 0x00, 0xD8 }));
            Assert.False(Contains(output, new byte[] { 0xFD, 0xFF }));
        }

        // ── Canonical ordering imposed on a dirtied non-canonical file (D-02 normalize-on-save) ──

        [Fact]
        public void DirtyNonCanonical_NormalizedToCanonicalOrderOnSave()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1NonCanonicalOrder());
            ById(doc, 1).Text = "touch"; // force re-serialize
            byte[] output = Serialize(doc);

            Assert.Equal(new uint[] { 1, 2, 3 }, StringTableFixtures.ReadOnDiskStringIds(output).ToArray());
            Assert.Equal(new[] { "apple", "mango", "zebra" }, StringTableFixtures.ReadOnDiskNames(output).ToArray());
        }

        // ── sourceCrc preserved on a text-only edit (D-02b) ──

        [Fact]
        public void TextEdit_PreservesSourceCrc()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal());
            ById(doc, 1).Text = "changed";
            byte[] output = Serialize(doc);

            StringTableDocument reloaded = Load(output);
            Assert.Equal("changed", ById(reloaded, 1).Text);
            Assert.Equal(100u, ById(reloaded, 1).SourceCrc); // preserved, not recomputed
        }

        // ── Added entry: nextUniqueId increment + sourceCrc 0 ──

        [Fact]
        public void AddEntry_AdvancesNextUniqueId_AndSourceCrcZero_RoundTrips()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal()); // nextId 2
            MutableStringTableEntry added = doc.Mutable.AddEntry();
            added.Text = "brand new";

            Assert.Equal(2u, added.Id);
            Assert.Equal(0u, added.SourceCrc);
            Assert.Equal(3u, doc.Mutable.NextUniqueId);

            byte[] output = Serialize(doc);
            StringTableDocument reloaded = Load(output);
            Assert.Equal(3u, reloaded.NextUniqueId);
            Assert.Equal("002_default", ById(reloaded, 2).Name);
            Assert.Equal("brand new", ById(reloaded, 2).Text);
        }

        // ── Re-encoded non-BMP edit keeps the surrogate pair (charCount code-unit correctness) ──

        [Fact]
        public void EditedNonBmpText_ReEncodesCodeUnitsCorrectly()
        {
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal());
            ById(doc, 1).Text = "𐐷"; // U+10437, 2 code units
            byte[] output = Serialize(doc);

            StringTableDocument reloaded = Load(output);
            Assert.Equal("𐐷", ById(reloaded, 1).Text);
            Assert.Equal(2, ById(reloaded, 1).Text.Length);
        }

        // ── No smart-quote / unfubar rewrite is performed by the writer ──

        [Fact]
        public void Writer_DoesNotRewriteSmartQuotes()
        {
            const string smart = "“hi”…"; // “hi”…
            StringTableDocument doc = Load(StringTableFixtures.BuildV1Minimal());
            ById(doc, 1).Text = smart;
            byte[] output = Serialize(doc);

            StringTableDocument reloaded = Load(output);
            Assert.Equal(smart, ById(reloaded, 1).Text); // preserved verbatim, NOT downgraded to "hi"...
        }
    }
}
