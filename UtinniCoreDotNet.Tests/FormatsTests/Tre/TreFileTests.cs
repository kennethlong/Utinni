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
using System.Text;
using Xunit;
using UtinniCoreDotNet.Formats.Tre;

namespace UtinniCoreDotNet.Tests.FormatsTests.Tre
{
    /// <summary>
    /// Tier-1 parser tests for TreFile. These exercise the parser directly without going through
    /// the CLI surface.
    ///
    /// Phase 7 (07-01) lazy contract: enumeration (Open(string)/Open(Stream)) reads only TOC +
    /// names — no payloads. Payload reads are on-demand via GetRecordData, which re-opens the
    /// source file by path. A stream-backed instance (Open(Stream)) cannot lazily read payloads
    /// and throws InvalidOperationException — so payload-access tests open via a temp file path.
    /// </summary>
    public class TreFileTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Happy-path tests (payload access via Open(string) — lazy contract)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Open_ValidV0005ThreeRecord_ReturnsHeaderAndRecords()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);

                Assert.Equal("0005", tre.Header.VersionTag);
                Assert.Equal(TreVersion.V0005, tre.Header.Version);
                Assert.False(tre.Header.EnumerateOnly);
                Assert.Equal(3, tre.Header.RecordCount);
                Assert.Equal(3, tre.Records.Count);

                Assert.Equal("hello.txt", tre.Records[0].Name);
                Assert.Equal("quick.txt", tre.Records[1].Name);
                Assert.Equal("empty.bin", tre.Records[2].Name);

                Assert.Equal("none",    tre.Records[0].CompressionKind);
                Assert.Equal("deflate", tre.Records[1].CompressionKind);
                Assert.Equal("none",    tre.Records[2].CompressionKind);

                Assert.Equal("Hello, World!", Encoding.ASCII.GetString(tre.GetRecordData(0)));
                Assert.Equal("The quick brown fox jumps over the lazy dog.", Encoding.ASCII.GetString(tre.GetRecordData(1)));
                Assert.Empty(tre.GetRecordData(2));
            }
            finally { TryDelete(path); }
        }

        [Fact]
        public void Open_ValidV0006TwoRecord_ReturnsHeaderAndRecords()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0006TwoRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);

                Assert.Equal("0006", tre.Header.VersionTag);
                Assert.Equal(TreVersion.V0006, tre.Header.Version);
                Assert.Equal(2, tre.Header.RecordCount);
                Assert.Equal(2, tre.Records.Count);

                Assert.Equal("marker.txt", tre.Records[0].Name);
                Assert.Equal("v6.txt",     tre.Records[1].Name);

                Assert.Equal("v0006 marker", Encoding.ASCII.GetString(tre.GetRecordData(0)));
                Assert.Equal("Phase 4 v0006 coverage", Encoding.ASCII.GetString(tre.GetRecordData(1)));
            }
            finally { TryDelete(path); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Negative-case tests (throw at Open — stream-backed is fine, no payload read)
        // ─────────────────────────────────────────────────────────────────────

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

        [Fact]
        public void Open_TruncatedRecordTable_ThrowsTruncatedOrCapException()
        {
            // 07-01: a 60-byte file claiming 3 records (3*24 = 72-byte uncompressed table) is now
            // rejected by the division-form count guard (ChunkLengthExceedsCap) before the old
            // post-read truncation check. Either kind correctly indicates the table cannot fit.
            byte[] bytes = TreFileFixtures.BuildTruncatedRecordTable();
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => TreFile.Open(ms));
                var treEx = Assert.IsType<TreParseException>(ex);
                Assert.True(
                    treEx.Kind == TreParseError.Truncated || treEx.Kind == TreParseError.ChunkLengthExceedsCap,
                    "Expected Truncated or ChunkLengthExceedsCap, got: " + treEx.Kind);
            }
        }

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

        // ─────────────────────────────────────────────────────────────────────
        // Lazy payload-read contract (07-01)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 07-01: GetRecordData on an Open(string) instance reads the deflated record lazily
        /// (re-opening the file by path) and returns the correct inflated bytes.
        /// </summary>
        [Fact]
        public void GetRecordData_DeflatedRecordViaOpenString_ReturnsInflatedBytes()
        {
            byte[] payload = Encoding.ASCII.GetBytes("deflated payload test string for unit tests");
            byte[] bytes = TreFileFixtures.BuildDeflatedRecord(payload);
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                byte[] result = tre.GetRecordData(0);
                Assert.Equal("deflated payload test string for unit tests", Encoding.ASCII.GetString(result));
            }
            finally { TryDelete(path); }
        }

        /// <summary>
        /// 07-01 lazy contract (review consensus #2): a stream-backed instance (Open(Stream)) has
        /// no stored path and throws InvalidOperationException on GetRecordData — it does NOT
        /// silently eager-read or keep the stream.
        /// </summary>
        [Fact]
        public void GetRecordData_OnStreamBackedInstance_ThrowsInvalidOperationException()
        {
            byte[] payload = Encoding.ASCII.GetBytes("stream-backed has no lazy payload read");
            byte[] bytes = TreFileFixtures.BuildDeflatedRecord(payload);

            TreFile tre;
            using (var ms = new MemoryStream(bytes))
            {
                tre = TreFile.Open(ms);
                // Metadata enumeration works from a stream.
                Assert.Equal(1, tre.Records.Count);
                Assert.Equal("deflated.bin", tre.Records[0].Name);
            }

            Assert.Throws<InvalidOperationException>(() => tre.GetRecordData(0));
        }

        /// <summary>
        /// 07-01 (T-04-DoS): a record claiming 1 GB uncompressed but a tiny payload throws
        /// DeflateExpansionExceedsCap before allocating an attacker-controlled buffer.
        /// </summary>
        [Fact]
        public void GetRecordData_DeflateClaimedGigabyte_ThrowsExpansionCapException()
        {
            byte[] bytes = TreFileFixtures.BuildClaimedGigabyteDeflate();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                var ex = Record.Exception(() => tre.GetRecordData(0));
                var treEx = Assert.IsType<TreParseException>(ex);
                Assert.Equal(TreParseError.DeflateExpansionExceedsCap, treEx.Kind);
            }
            finally { TryDelete(path); }
        }

        /// <summary>
        /// 07-01: PayloadReadCount proves lazy enumeration — Open(string) reads zero payloads,
        /// and each GetRecordData increments the counter by exactly one.
        /// </summary>
        [Fact]
        public void PayloadReadCount_IsZeroAfterOpen_AndIncrementsPerGetRecordData()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                TreFile tre = TreFile.Open(path);
                Assert.Equal(0, tre.PayloadReadCount);

                tre.GetRecordData(0);
                Assert.Equal(1, tre.PayloadReadCount);

                tre.GetRecordData(1);
                Assert.Equal(2, tre.PayloadReadCount);
            }
            finally { TryDelete(path); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string WriteTemp(byte[] bytes)
        {
            string path = Path.Combine(Path.GetTempPath(), "tre-t1-" + Guid.NewGuid().ToString("N") + ".tre");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* best-effort temp cleanup */ }
        }
    }
}
