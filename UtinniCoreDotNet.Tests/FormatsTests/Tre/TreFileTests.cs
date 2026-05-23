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
using System.Text;
using Xunit;
using UtinniCoreDotNet.Formats.Tre;

namespace UtinniCoreDotNet.Tests.FormatsTests.Tre
{
    /// <summary>
    /// Tier-1 parser tests for TreFile.Open(Stream). These tests exercise the parser directly
    /// without going through the CLI surface — they verify parser correctness independently
    /// of the JSON envelope layer.
    /// </summary>
    public class TreFileTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Happy-path tests
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Test 1: v0005 header + 3 records parse correctly.
        /// Also verifies the REVIEWS HIGH-4 eager-read contract: GetRecordData(0) returns
        /// expected bytes AFTER the MemoryStream is disposed.
        /// </summary>
        [Fact]
        public void Open_ValidV0005ThreeRecord_ReturnsHeaderAndRecords()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            TreFile tre;

            // ── Eager-read contract: stream disposed before GetRecordData ──────
            using (var ms = new MemoryStream(bytes))
            {
                tre = TreFile.Open(ms);
            }
            // MemoryStream is now disposed. GetRecordData must still work.

            Assert.Equal("0005", tre.Header.Version);
            Assert.Equal(3, tre.Header.RecordCount);
            Assert.Equal(3, tre.Records.Count);

            Assert.Equal("hello.txt", tre.Records[0].Name);
            Assert.Equal("quick.txt", tre.Records[1].Name);
            Assert.Equal("empty.bin", tre.Records[2].Name);

            Assert.Equal("none",    tre.Records[0].CompressionKind);
            Assert.Equal("deflate", tre.Records[1].CompressionKind);
            Assert.Equal("none",    tre.Records[2].CompressionKind);

            // Payload data accessible after stream dispose.
            byte[] record0 = tre.GetRecordData(0);
            Assert.Equal("Hello, World!", Encoding.ASCII.GetString(record0));

            byte[] record1 = tre.GetRecordData(1);
            Assert.Equal("The quick brown fox jumps over the lazy dog.", Encoding.ASCII.GetString(record1));

            byte[] record2 = tre.GetRecordData(2);
            Assert.Empty(record2);
        }

        /// <summary>
        /// Test 2: v0006 header parses identically to v0005 (REVIEWS LOW-20).
        /// </summary>
        [Fact]
        public void Open_ValidV0006TwoRecord_ReturnsHeaderAndRecords()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0006TwoRecord();

            TreFile tre;
            using (var ms = new MemoryStream(bytes))
            {
                tre = TreFile.Open(ms);
            }

            Assert.Equal("0006", tre.Header.Version);
            Assert.Equal(2, tre.Header.RecordCount);
            Assert.Equal(2, tre.Records.Count);

            Assert.Equal("marker.txt", tre.Records[0].Name);
            Assert.Equal("v6.txt",     tre.Records[1].Name);

            byte[] record0 = tre.GetRecordData(0);
            Assert.Equal("v0006 marker", Encoding.ASCII.GetString(record0));

            byte[] record1 = tre.GetRecordData(1);
            Assert.Equal("Phase 4 v0006 coverage", Encoding.ASCII.GetString(record1));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Negative-case tests
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Test 3: Wrong magic bytes → TreParseException(BadMagic).</summary>
        [Fact]
        public void Open_BadMagic_ThrowsBadMagicException()
        {
            byte[] bytes = TreFileFixtures.BuildBadMagic();
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => TreFile.Open(ms));
                var treEx = Assert.IsType<TreParseException>(ex);
                Assert.Equal(TreParseError.BadMagic, treEx.Kind);
            }
        }

        /// <summary>Test 4: Version "0009" → TreParseException(UnsupportedVersion) with "0009" in message.</summary>
        [Fact]
        public void Open_UnsupportedVersionField_ThrowsUnsupportedVersionException()
        {
            byte[] bytes = TreFileFixtures.BuildUnsupportedVersion("0009");
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => TreFile.Open(ms));
                var treEx = Assert.IsType<TreParseException>(ex);
                Assert.Equal(TreParseError.UnsupportedVersion, treEx.Kind);
                Assert.Contains("0009", treEx.Message);
            }
        }

        /// <summary>Test 5: Header claims 3 records but info block is only 1 record long → Truncated.</summary>
        [Fact]
        public void Open_TruncatedRecordTable_ThrowsTruncatedException()
        {
            byte[] bytes = TreFileFixtures.BuildTruncatedRecordTable();
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => TreFile.Open(ms));
                var treEx = Assert.IsType<TreParseException>(ex);
                Assert.Equal(TreParseError.Truncated, treEx.Kind);
            }
        }

        /// <summary>
        /// Test 6 + 10: Deflated record + REVIEWS HIGH-4 regression guard.
        /// Opens a TRE with a deflated record INSIDE a using block, lets the stream dispose,
        /// then calls GetRecordData(0) — must return the correct inflated bytes.
        /// This test FAILS if TreFile lazy-seeks the original stream.
        /// </summary>
        [Fact]
        public void GetRecordData_DeflatedRecordAfterDispose_ReturnsInflatedBytes()
        {
            byte[] payload = Encoding.ASCII.GetBytes("deflated payload test string for unit tests");
            byte[] bytes = TreFileFixtures.BuildDeflatedRecord(payload);

            TreFile tre;
            using (var ms = new MemoryStream(bytes))
            {
                tre = TreFile.Open(ms);
            }
            // Stream is now disposed. GetRecordData must work from in-memory cache.

            byte[] result = tre.GetRecordData(0);
            Assert.Equal("deflated payload test string for unit tests", Encoding.ASCII.GetString(result));
        }

        /// <summary>
        /// Test 7 (T-04-DoS): Record claims 1 GB uncompressed but deflates to a tiny stream.
        /// GetRecordData must throw TreParseException(DeflateExpansionExceedsCap) before
        /// allocating an attacker-controlled 1 GB buffer.
        /// </summary>
        [Fact]
        public void GetRecordData_DeflateClaimedGigabyte_ThrowsExpansionCapException()
        {
            byte[] bytes = TreFileFixtures.BuildClaimedGigabyteDeflate();
            TreFile tre;
            using (var ms = new MemoryStream(bytes))
            {
                tre = TreFile.Open(ms);
            }

            var ex = Record.Exception(() => tre.GetRecordData(0));
            var treEx = Assert.IsType<TreParseException>(ex);
            Assert.Equal(TreParseError.DeflateExpansionExceedsCap, treEx.Kind);
        }

        /// <summary>
        /// Test 8 (T-04-V5): RecordCount = -1 → TreParseException(NegativeLength).
        /// </summary>
        [Fact]
        public void Open_NegativeResourceCount_ThrowsNegativeLengthException()
        {
            byte[] bytes = TreFileFixtures.BuildNegativeResourceCount();
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => TreFile.Open(ms));
                var treEx = Assert.IsType<TreParseException>(ex);
                Assert.Equal(TreParseError.NegativeLength, treEx.Kind);
            }
        }

        /// <summary>
        /// Test 9 (T-04-V5): InfoOffset = 999_999 in a ~100-byte file → Truncated or ChunkLengthExceedsCap.
        /// Either kind is acceptable (both indicate the claimed offset exceeds the file).
        /// </summary>
        [Fact]
        public void Open_InfoOffsetExceedsFile_ThrowsTruncatedOrCapException()
        {
            byte[] bytes = TreFileFixtures.BuildInfoOffsetExceedsFile();
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => TreFile.Open(ms));
                var treEx = Assert.IsType<TreParseException>(ex);
                Assert.True(
                    treEx.Kind == TreParseError.Truncated || treEx.Kind == TreParseError.ChunkLengthExceedsCap,
                    "Expected Truncated or ChunkLengthExceedsCap, got: " + treEx.Kind);
            }
        }

        /// <summary>
        /// Test 10: REVIEWS HIGH-4 explicit regression guard.
        /// Wraps MemoryStream in using, calls TreFile.Open(stream), exits using (disposes stream),
        /// then calls GetRecordData(0) — must return non-null expected bytes.
        /// This test is the definitive proof of the eager-read contract.
        /// </summary>
        [Fact]
        public void GetRecordData_AfterOpenStreamDisposed_StillReturnsBytes()
        {
            byte[] payload = Encoding.ASCII.GetBytes("eager read contract verification payload");
            byte[] bytes = TreFileFixtures.BuildDeflatedRecord(payload);

            TreFile capturedTre;

            // Step 1+2: Open inside using block → stream gets disposed when using exits.
            using (var ms = new MemoryStream(bytes))
            {
                capturedTre = TreFile.Open(ms);
            }

            // Step 3: Stream is now disposed. This call MUST NOT seek the original stream.
            byte[] result = capturedTre.GetRecordData(0);

            // Step 4: Assert result is non-null and matches the original payload.
            Assert.NotNull(result);
            Assert.Equal("eager read contract verification payload", Encoding.ASCII.GetString(result));
        }
    }
}
