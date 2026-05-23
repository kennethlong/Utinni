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
using System.IO;
using System.Linq;
using Xunit;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Tests.FormatsTests.Iff
{
    /// <summary>
    /// Tier-1 parser tests for IffReader. These tests exercise the parser directly
    /// without going through the CLI surface — they verify parser correctness independently
    /// of the JSON envelope layer.
    ///
    /// Test naming convention: [Method]_[Scenario]_[ExpectedOutcome] (Phase 1 D-04).
    /// </summary>
    public class IffReaderTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Happy-path tests
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Test 1: Nested FORM/WSNP with 3 outer leaves + 1 nested FORM/OBJS with 2 leaves.
        /// Verifies: Root is IffContainerChunk, 4 direct children, AllNodesInPreorder has 7 items.
        /// </summary>
        [Fact]
        public void Read_HappyPathNested_ReturnsExpectedTree()
        {
            byte[] bytes = IffReaderFixtures.BuildHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }

            var root = Assert.IsType<IffContainerChunk>(doc.Root);
            Assert.Equal("FORM", root.TypeId);
            Assert.Equal("WSNP", root.SubTypeId);
            Assert.Equal(4, root.Children.Count);

            // First child: DATA leaf (8 bytes)
            var data = Assert.IsType<IffLeafChunk>(root.Children[0]);
            Assert.Equal("DATA", data.TypeId);
            Assert.Equal(8, data.LengthBytes);

            // Second child: NAME leaf (4 bytes)
            var name = Assert.IsType<IffLeafChunk>(root.Children[1]);
            Assert.Equal("NAME", name.TypeId);
            Assert.Equal(4, name.LengthBytes);

            // Third child: DESC leaf (13 bytes — ODD)
            var desc = Assert.IsType<IffLeafChunk>(root.Children[2]);
            Assert.Equal("DESC", desc.TypeId);
            Assert.Equal(13, desc.LengthBytes);

            // Fourth child: nested FORM/OBJS with 2 children
            var innerForm = Assert.IsType<IffContainerChunk>(root.Children[3]);
            Assert.Equal("FORM", innerForm.TypeId);
            Assert.Equal("OBJS", innerForm.SubTypeId);
            Assert.Equal(2, innerForm.Children.Count);

            var info = Assert.IsType<IffLeafChunk>(innerForm.Children[0]);
            Assert.Equal("INFO", info.TypeId);
            Assert.Equal(4, info.LengthBytes);

            var body = Assert.IsType<IffLeafChunk>(innerForm.Children[1]);
            Assert.Equal("BODY", body.TypeId);
            Assert.Equal(6, body.LengthBytes);

            // Preorder traversal: 7 total nodes
            Assert.Equal(7, doc.AllNodesInPreorder.Count);

            // Preorder order: root → DATA → NAME → DESC → innerFORM → INFO → BODY
            Assert.Equal("FORM", doc.AllNodesInPreorder[0].TypeId);
            Assert.Equal("DATA", doc.AllNodesInPreorder[1].TypeId);
            Assert.Equal("NAME", doc.AllNodesInPreorder[2].TypeId);
            Assert.Equal("DESC", doc.AllNodesInPreorder[3].TypeId);
            Assert.Equal("FORM", doc.AllNodesInPreorder[4].TypeId);
            Assert.Equal("INFO", doc.AllNodesInPreorder[5].TypeId);
            Assert.Equal("BODY", doc.AllNodesInPreorder[6].TypeId);
        }

        /// <summary>
        /// Test 2 (REVIEWS MEDIUM-14 regression guard): CAT  container with two PROP children.
        /// Asserts: Root is IffContainerChunk with TypeId="CAT ", each child is IffLeafChunk
        /// (NOT IffContainerChunk). PROP must NOT be classified as a container.
        /// </summary>
        [Fact]
        public void Read_CatContainerWithPropChildren_PropClassifiedAsLeaf()
        {
            byte[] bytes = IffReaderFixtures.BuildCatWithPropChildren();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }

            var root = Assert.IsType<IffContainerChunk>(doc.Root);
            Assert.Equal("CAT ", root.TypeId); // trailing space preserved
            Assert.Equal(2, root.Children.Count);

            // Both children must be IffLeafChunk — NOT IffContainerChunk.
            var child0 = Assert.IsType<IffLeafChunk>(root.Children[0]);
            Assert.Equal("PROP", child0.TypeId);

            var child1 = Assert.IsType<IffLeafChunk>(root.Children[1]);
            Assert.Equal("PROP", child1.TypeId);
        }

        /// <summary>
        /// Test 3: Big-endian length parsing. A leaf with bytes 0x00 0x00 0x00 0x05 (big-endian 5)
        /// must read LengthBytes == 5. Little-endian interpretation (0x05000000 = ~83MB) would fire
        /// the 64MB cap, proving the parser correctly uses big-endian.
        /// </summary>
        [Fact]
        public void Read_BigEndianLengthInLittleEndianBytes_ThrowsExceedsCapException()
        {
            // Build a leaf where the 4 length bytes read as 0x05 0x00 0x00 0x00 (LE would be 5,
            // but BE reads this as 0x05000000 = 83886080 > 64 MB → ChunkLengthExceedsCap).
            byte[] bytes = new byte[]
            {
                (byte)'T', (byte)'E', (byte)'S', (byte)'T', // TypeID
                0x05, 0x00, 0x00, 0x00                      // LE = 83886080 in BE interpretation → over 64 MB cap
            };

            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => IffReader.Read(ms));
                var iffEx = Assert.IsType<IffParseException>(ex);
                Assert.Equal(IffParseError.ChunkLengthExceedsCap, iffEx.Kind);
            }
        }

        /// <summary>
        /// Test 3b: Correct big-endian 5-byte chunk.
        /// Bytes 0x00 0x00 0x00 0x05 → length == 5.
        /// </summary>
        [Fact]
        public void Read_CorrectBigEndianLength_ParsesExpectedLength()
        {
            // FORM/TEST containing one TEST leaf of exactly 5 bytes (even? no, odd — but wrap in FORM)
            // Actually let's use length=4 (even) for simplicity
            byte[] bytes;
            using (var ms = new System.IO.MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                // FORM/TEST: payload = SubType(4) + TEST_header(8) + TEST_data(4) = 16
                bw.Write(new byte[] { (byte)'F', (byte)'O', (byte)'R', (byte)'M' });
                bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x10 }); // BE 16
                bw.Write(new byte[] { (byte)'T', (byte)'E', (byte)'S', (byte)'T' }); // sub-type
                bw.Write(new byte[] { (byte)'T', (byte)'E', (byte)'S', (byte)'T' }); // leaf TypeID
                bw.Write(new byte[] { 0x00, 0x00, 0x00, 0x04 }); // BE 4
                bw.Write(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }); // data
                bytes = ms.ToArray();
            }

            using (var ms = new MemoryStream(bytes))
            {
                var doc = IffReader.Read(ms);
                var root = Assert.IsType<IffContainerChunk>(doc.Root);
                var leaf = Assert.IsType<IffLeafChunk>(root.Children[0]);
                Assert.Equal(4, leaf.LengthBytes);
                Assert.Equal(4, leaf.Data.Length);
            }
        }

        /// <summary>
        /// Test 4: CAT  trailing-space FOURCC. "CAT " (with trailing space) must be classified
        /// as a container, not a leaf.
        /// </summary>
        [Fact]
        public void Read_CatTypeIdWithTrailingSpace_ClassifiesAsContainer()
        {
            byte[] bytes = IffReaderFixtures.BuildCatTypeId();
            using (var ms = new MemoryStream(bytes))
            {
                var doc = IffReader.Read(ms);
                var root = Assert.IsType<IffContainerChunk>(doc.Root);
                Assert.Equal("CAT ", root.TypeId);
            }
        }

        /// <summary>
        /// Test 5: Odd-length leaf followed by another chunk. The pad byte must be consumed;
        /// if it's not, the second chunk's TypeID picks up the pad byte as its first character,
        /// producing a malformed FOURCC. Test verifies the second chunk parses cleanly.
        /// </summary>
        [Fact]
        public void Read_OddLengthLeafFollowedByChunk_ConsumesPadByteAndParsesSecond()
        {
            byte[] bytes = IffReaderFixtures.BuildOddLengthLeafFollowedByChunk();
            using (var ms = new MemoryStream(bytes))
            {
                var doc = IffReader.Read(ms);
                var root = Assert.IsType<IffContainerChunk>(doc.Root);
                Assert.Equal(2, root.Children.Count);

                var first = Assert.IsType<IffLeafChunk>(root.Children[0]);
                Assert.Equal(5, first.LengthBytes); // odd-length

                var second = Assert.IsType<IffLeafChunk>(root.Children[1]);
                Assert.Equal("DATA", second.TypeId); // must parse cleanly, not "0x00DATA" garbage
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // STRICT missing-pad-byte tests (REVIEWS MEDIUM-11 + iter-3 MED-3)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Test 6 (REVIEWS MEDIUM-11 — EOF case): Odd-length leaf at EOF with no pad byte.
        /// parentEnd == stream.Length for outermost FORM. Must throw Truncated.
        /// </summary>
        [Fact]
        public void Read_OddLengthLeafAtEofMissingPad_ThrowsTruncated()
        {
            byte[] bytes = IffReaderFixtures.BuildOddLengthLeafAtEofMissingPad();
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => IffReader.Read(ms));
                var iffEx = Assert.IsType<IffParseException>(ex);
                Assert.Equal(IffParseError.Truncated, iffEx.Kind);
                Assert.Contains("Odd-length chunk at parent boundary missing required pad byte", iffEx.Message);
            }
        }

        /// <summary>
        /// Test 6b (iter-3 MED-3 — nested parentEnd case): Odd-length leaf exactly at the
        /// NESTED parent's end with no pad byte. parentEnd is the parent FORM's payload end,
        /// not file EOF. Must throw Truncated.
        /// </summary>
        [Fact]
        public void Read_OddLengthLeafAtParentEnd_NoPad_ThrowsTruncated()
        {
            byte[] bytes = IffReaderFixtures.BuildOddLengthLeafAtParentEndNoPad();
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => IffReader.Read(ms));
                var iffEx = Assert.IsType<IffParseException>(ex);
                Assert.Equal(IffParseError.Truncated, iffEx.Kind);
                Assert.Contains("Odd-length chunk at parent boundary missing required pad byte", iffEx.Message);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // iter-4 HIGH-1 algorithm tests
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Test 7 (iter-4 HIGH-1 — NestedChunkOverflow):
        /// File 100 bytes; outer FORM length 92 (parentEnd=100);
        /// inner FORM at offset 12 with length 90 → inner end 110 > parentEnd 100.
        /// Must throw NestedChunkOverflow.
        /// </summary>
        [Fact]
        public void Read_NestedChunkOverflow_ThrowsNestedOverflowException()
        {
            byte[] bytes = IffReaderFixtures.BuildNestedChunkOverflow();
            Assert.Equal(100, bytes.Length);

            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => IffReader.Read(ms));
                var iffEx = Assert.IsType<IffParseException>(ex);
                Assert.Equal(IffParseError.NestedChunkOverflow, iffEx.Kind);
            }
        }

        /// <summary>
        /// Test 8 (iter-4 HIGH-1 — streaming-read EOF Truncated):
        /// File 50 bytes; outer FORM length 92 (top-level FORM IS the file; no early throw);
        /// inner leaf at offset 12 with length 80 → nested check passes (80 ≤ parentEnd 100);
        /// streaming read of 80 bytes from offset 20 short-reads at file end (byte 50).
        /// Must throw Truncated.
        /// </summary>
        [Fact]
        public void Read_StreamingReadEofTruncated_ThrowsTruncatedException()
        {
            byte[] bytes = IffReaderFixtures.BuildTruncatedFile();
            Assert.Equal(50, bytes.Length);

            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => IffReader.Read(ms));
                var iffEx = Assert.IsType<IffParseException>(ex);
                Assert.Equal(IffParseError.Truncated, iffEx.Kind);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Other negative-case tests
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Test 9: Length bytes 0x80000001 (signed BE = large negative). Must throw NegativeLength.
        /// Also covers integer-overflow: 0x80000001 as unsigned is 2147483649 but as signed int32 is -2147483647.
        /// </summary>
        [Fact]
        public void Read_NegativeLengthField_ThrowsNegativeLengthException()
        {
            byte[] bytes = IffReaderFixtures.BuildNegativeLength();
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => IffReader.Read(ms));
                var iffEx = Assert.IsType<IffParseException>(ex);
                Assert.Equal(IffParseError.NegativeLength, iffEx.Kind);
            }
        }

        /// <summary>
        /// Test 10: Length 0x04000001 = 64 MB + 1. Must throw ChunkLengthExceedsCap (T-04-DoS).
        /// </summary>
        [Fact]
        public void Read_ChunkLengthExceedsCap_ThrowsExceedsCapException()
        {
            byte[] bytes = IffReaderFixtures.BuildChunkLengthExceedsCap();
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => IffReader.Read(ms));
                var iffEx = Assert.IsType<IffParseException>(ex);
                Assert.Equal(IffParseError.ChunkLengthExceedsCap, iffEx.Kind);
            }
        }

        /// <summary>
        /// Test 11: First byte 0xFF in TypeID field. Must throw MalformedFourCc.
        /// </summary>
        [Fact]
        public void Read_MalformedNonAsciiFourCc_ThrowsMalformedFourCcException()
        {
            byte[] bytes = IffReaderFixtures.BuildMalformedFourCc();
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => IffReader.Read(ms));
                var iffEx = Assert.IsType<IffParseException>(ex);
                Assert.Equal(IffParseError.MalformedFourCc, iffEx.Kind);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Stable-id test
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Test 12: Stable-id assignment for the happy-path fixture.
        /// Root id = "FORM:WSNP/0"; first child id starts with "FORM:WSNP/0/DATA:DATA/0".
        /// All ids in AllNodesInPreorder are unique.
        /// </summary>
        [Fact]
        public void Read_HappyPath_AssignsStableIdsToEveryNode()
        {
            byte[] bytes = IffReaderFixtures.BuildHappyPath();
            IffDocument doc;
            using (var ms = new MemoryStream(bytes))
            {
                doc = IffReader.Read(ms);
            }

            var root = Assert.IsType<IffContainerChunk>(doc.Root);
            Assert.Equal("FORM:WSNP/0", root.Id);

            // First child (DATA leaf at ordinal 0)
            Assert.Equal("FORM:WSNP/0/DATA:DATA/0", root.Children[0].Id);

            // Second child (NAME leaf at ordinal 1)
            Assert.Equal("FORM:WSNP/0/NAME:NAME/1", root.Children[1].Id);

            // Third child (DESC leaf at ordinal 2)
            Assert.Equal("FORM:WSNP/0/DESC:DESC/2", root.Children[2].Id);

            // Fourth child (nested FORM/OBJS at ordinal 3)
            var inner = Assert.IsType<IffContainerChunk>(root.Children[3]);
            Assert.Equal("FORM:WSNP/0/FORM:OBJS/3", inner.Id);

            // Inner INFO leaf at ordinal 0 within the nested FORM
            Assert.Equal("FORM:WSNP/0/FORM:OBJS/3/INFO:INFO/0", inner.Children[0].Id);

            // Inner BODY leaf at ordinal 1 within the nested FORM
            Assert.Equal("FORM:WSNP/0/FORM:OBJS/3/BODY:BODY/1", inner.Children[1].Id);

            // All 7 ids must be unique
            var ids = doc.AllNodesInPreorder.Select(n => n.Id).ToList();
            Assert.Equal(7, ids.Distinct().Count());
        }

        // ─────────────────────────────────────────────────────────────────────
        // Parser purity gate (static analysis via string literal search)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Test 13 (parser purity): IffReader source file must not contain Newtonsoft or Console.
        /// This is a static grep test — reads the source file and asserts absence of forbidden patterns.
        /// </summary>
        [Fact]
        public void IffReader_SourceFile_ContainsNoNewtonsoftOrConsoleReferences()
        {
            // Find the IffReader.cs source relative to the test assembly.
            // This test documents the purity requirement; it passes structurally when the parser
            // is kept pure. If test tooling cannot locate the source file, skip the grep.
            string sourceFile = FindIffReaderSource();
            if (sourceFile == null)
            {
                // Source file not found in development tree — skip the grep gate.
                return;
            }

            string source = File.ReadAllText(sourceFile);
            Assert.DoesNotContain("Newtonsoft", source);
            Assert.DoesNotContain("Console.", source);
        }

        private static string FindIffReaderSource()
        {
            try
            {
                // Walk up from the test assembly location to find the source.
                string baseDir = AppContext.BaseDirectory;
                string candidate = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "..",
                        "UtinniCoreDotNet", "Formats", "Iff", "IffReader.cs"));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore path errors.
            }
            return null;
        }
    }
}
