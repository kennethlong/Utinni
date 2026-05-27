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
using System.IO.Compression;
using System.Text;
using Xunit;

namespace Utinni.Cli.Tests.Infrastructure
{
    /// <summary>
    /// Self-tests for TreFixtureBuilder. Every committed fixture is regenerated into a temp path
    /// and compared byte-for-byte against the committed copy (T-07-00-01 anti-drift), plus
    /// structural asserts that prove each fixture is the exact shape 07-01's reader tests assert
    /// against. To (re)emit the committed fixtures into the source tree, run with GSD_EMIT_FIXTURES=1:
    ///   dotnet test Utinni.Cli.Tests --filter "TreFixtureBuilder" (env GSD_EMIT_FIXTURES=1)
    /// </summary>
    public class TreFixtureBuilderTests
    {
        // ── Known synthetic payload contents (kept in sync with TreFixtureBuilder) ──
        private const string Cot2000PayloadA = "COT2000 companion payload A -> object/tangible/foo.iff";

        // ──────────────────────────────────────────────────────────────────────────────
        // 0. Env-gated generator (no-op unless GSD_EMIT_FIXTURES=1). Emits the committed
        //    fixtures into the SOURCE Fixtures/tre tree so they can be committed.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void EmitCommittedFixtures_WhenEnvSet()
        {
            if (Environment.GetEnvironmentVariable("GSD_EMIT_FIXTURES") != "1")
                return; // no-op on normal CI runs

            TreFixtureBuilder.EmitAll(CommittedTreDir());
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 1. Valid fixtures: regenerate-and-compare + structural asserts
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void V6000TwoRecord_MagicIsEert6000_AndMatchesCommitted()
        {
            byte[] emitted = AssertCommittedMatches("synthetic-v6000-2record.tre", TreFixtureBuilder.WriteV6000TwoRecord);

            // ASCII "EERT6000" = 45 45 52 54 36 30 30 30
            Assert.Equal(new byte[] { 0x45, 0x45, 0x52, 0x54, 0x36, 0x30, 0x30, 0x30 }, Take(emitted, 0, 8));
            Assert.Equal(2u, ReadU32(emitted, 8)); // numFiles
        }

        [Fact]
        public void ZlibFramedV6000OneRecord_RoundTrips_AndMatchesCommitted()
        {
            byte[] emitted = AssertCommittedMatches("zlib-framed-1record-v6000.tre", TreFixtureBuilder.WriteZlibFramedV6000OneRecord);

            // The TOC and name blocks are zlib-framed (0x78 0x9c) and must inflate back cleanly.
            byte[] toc = InflateV6000Block(emitted, tocOffsetField: 12, sizeField: 20);
            Assert.Equal(32, toc.Length);                 // exactly one 32-byte crc-first entry
            Assert.Equal(0x33333333u, ReadU32(toc, 0));   // crc of the single record

            int tocOffset = (int)ReadU32(emitted, 12);
            int sizeOfToc = (int)ReadU32(emitted, 20);
            int nameSize = (int)ReadU32(emitted, 28);
            byte[] nameRaw = InflateZlib(Take(emitted, tocOffset + sizeOfToc, nameSize));
            Assert.Equal("solo.iff\0", Encoding.ASCII.GetString(nameRaw));
        }

        [Fact]
        public void Cot2000_MagicAndSelfContainedResolution_AndMatchesCommitted()
        {
            // Emit the whole self-contained set into a temp dir and compare all three files.
            string tempDir = NewTempDir();
            string tocTemp = Path.Combine(tempDir, "synthetic-cot2000-2tree.toc");
            TreFixtureBuilder.WriteCot2000TwoTree(tocTemp, Path.Combine(tempDir, "cot2000"));

            AssertFileBytesEqual("synthetic-cot2000-2tree.toc", tocTemp);
            AssertFileBytesEqual("cot2000/tree0.tre", Path.Combine(tempDir, "cot2000", "tree0.tre"));
            AssertFileBytesEqual("cot2000/tree1.tre", Path.Combine(tempDir, "cot2000", "tree1.tre"));

            byte[] toc = File.ReadAllBytes(tocTemp);

            // ASCII " COT2000" = 20 43 4F 54 32 30 30 30
            Assert.Equal(new byte[] { 0x20, 0x43, 0x4F, 0x54, 0x32, 0x30, 0x30, 0x30 }, Take(toc, 0, 8));
            Assert.Equal(2u, ReadU32(toc, 12)); // numFiles
            Assert.Equal(2u, ReadU32(toc, 28)); // numTreeFiles

            // ── Self-contained resolver contract (review consensus #2) ──
            // Parse global TOC entry 0, open the companion it names, read the declared bytes, inflate.
            int sizeOfTreeNameBlock = (int)ReadU32(toc, 32);
            int tocStart = 36 + sizeOfTreeNameBlock;

            int compressor = toc[tocStart];
            int treeFileIndex = ReadU16(toc, tocStart + 2);
            int offset = (int)ReadU32(toc, tocStart + 12);
            int compressedLength = (int)ReadU32(toc, tocStart + 20);

            Assert.Equal(1, compressor);     // entry 0 is raw-deflate
            Assert.Equal(0, treeFileIndex);  // -> cot2000/tree0.tre

            byte[] companion = File.ReadAllBytes(Path.Combine(tempDir, "cot2000", "tree" + treeFileIndex + ".tre"));
            Assert.True(offset + compressedLength <= companion.Length,
                "COT2000 entry points past the end of its companion archive");

            byte[] stored = Take(companion, offset, compressedLength);
            byte[] payload = InflateRawDeflate(stored);
            Assert.Equal(Cot2000PayloadA, Encoding.ASCII.GetString(payload));
        }

        [Fact]
        public void Synthetic5000_IsReadableCrcFirst24Zlib_AndMatchesCommitted()
        {
            // 5000 is the READABLE SWGEmu Pre-CU format (verified against the live client):
            // size-first-style header + crc-first 24-byte records + zlib-compressed blocks.
            byte[] emitted = AssertCommittedMatches("synthetic-5000-header.tre", TreFixtureBuilder.WriteSynthetic5000);

            Assert.Equal("EERT5000", Encoding.ASCII.GetString(Take(emitted, 0, 8)));
            Assert.Equal(2u, ReadU32(emitted, 8));   // recordCount
            Assert.Equal(2u, ReadU32(emitted, 16));  // infoCompression = zlib
            Assert.Equal(2u, ReadU32(emitted, 24));  // nameCompression = zlib
        }

        [Fact]
        public void Synthetic0004_SizeFirstFamily_AndMatchesCommitted()
        {
            byte[] emitted = AssertCommittedMatches("synthetic-0004-header.tre", TreFixtureBuilder.WriteSynthetic0004);
            Assert.Equal("EERT0004", Encoding.ASCII.GetString(Take(emitted, 0, 8)));
            Assert.Equal(1u, ReadU32(emitted, 8));  // recordCount
            Assert.Equal(36u, ReadU32(emitted, 12)); // infoOffset (size-first family)
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 2. FixturePath env resolver
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void SampleTreDir_IsNull_WhenEnvUnset()
        {
            string original = Environment.GetEnvironmentVariable("SWG_SAMPLE_TRE_DIR");
            try
            {
                Environment.SetEnvironmentVariable("SWG_SAMPLE_TRE_DIR", null);
                Assert.Null(FixturePath.SampleTreDir());
                Assert.False(FixturePath.HasSampleTreDir());
            }
            finally
            {
                Environment.SetEnvironmentVariable("SWG_SAMPLE_TRE_DIR", original);
            }
        }

        [Fact]
        public void SampleTreDir_ReturnsValue_WhenEnvSet()
        {
            string original = Environment.GetEnvironmentVariable("SWG_SAMPLE_TRE_DIR");
            try
            {
                Environment.SetEnvironmentVariable("SWG_SAMPLE_TRE_DIR", @"Z:\some\sample\path");
                Assert.Equal(@"Z:\some\sample\path", FixturePath.SampleTreDir());
            }
            finally
            {
                Environment.SetEnvironmentVariable("SWG_SAMPLE_TRE_DIR", original);
            }
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // 3. Malformed / adversarial fixtures (Task 2)
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void MalformedCountStride_DeclaresOverflowingNumFiles_AndMatchesCommitted()
        {
            byte[] emitted = AssertCommittedMatches("malformed-count-stride-overflow.tre", TreFixtureBuilder.WriteMalformedCountStrideOverflow);

            int numFiles = (int)ReadU32(emitted, 8);
            Assert.Equal(TreFixtureBuilder.MalformedCountStrideNumFiles, numFiles);
            // Declared count dwarfs what the file can hold: numFiles > (len - header) / stride.
            Assert.True(numFiles > (emitted.Length - 36) / 32);
            // numFiles * 32 overflows int32 (the reason the guard must use the division form).
            Assert.True((long)numFiles * 32 > int.MaxValue);
        }

        [Fact]
        public void MalformedOffsetLength_WrapsNegative_AndMatchesCommitted()
        {
            byte[] emitted = AssertCommittedMatches("malformed-offset-length-overflow.tre", TreFixtureBuilder.WriteMalformedOffsetLengthOverflow);

            byte[] toc = InflateV6000Block(emitted, tocOffsetField: 12, sizeField: 20);
            int length = ReadI32(toc, 4);
            int offset = ReadI32(toc, 8);
            Assert.Equal(TreFixtureBuilder.MalformedOverflowOffset, offset);
            Assert.Equal(TreFixtureBuilder.MalformedOverflowLength, length);
            // offset + length overflows int32 and wraps negative under naive int arithmetic.
            Assert.True((long)offset + length > int.MaxValue);
            Assert.True(unchecked(offset + length) < 0);
        }

        [Fact]
        public void MalformedZlibBadFrame_HasValidHeaderButInflateFails_AndMatchesCommitted()
        {
            byte[] emitted = AssertCommittedMatches("malformed-zlib-bad-adler.tre", TreFixtureBuilder.WriteMalformedZlibBadFrame);

            int tocOffset = (int)ReadU32(emitted, 12);
            int sizeOfToc = (int)ReadU32(emitted, 20);
            byte[] block = Take(emitted, tocOffset, sizeOfToc);

            // Valid 0x78 0x9c zlib header ...
            Assert.Equal(0x78, block[0]);
            Assert.Equal(0x9C, block[1]);
            // ... but the body does NOT inflate (detectable on the inflate side, not via Adler).
            byte[] body = Take(block, 2, block.Length - 2 - 4); // strip header + 4-byte trailer
            Assert.ThrowsAny<Exception>(() => InflateRawDeflate(body));
        }

        [Fact]
        public void MalformedUnknownCompressor_HasUnrecognizedCompressor_AndMatchesCommitted()
        {
            byte[] emitted = AssertCommittedMatches("malformed-unknown-compressor.tre", TreFixtureBuilder.WriteMalformedUnknownCompressorFixture);

            byte[] toc = InflateV6000Block(emitted, tocOffsetField: 12, sizeField: 20);
            int compressor = ReadI32(toc, 12); // v6000 TOC entry: compressor at +12
            Assert.Equal(TreFixtureBuilder.MalformedUnknownCompressor, compressor);
            Assert.True(compressor != 0 && compressor != 1 && compressor != 2);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // helpers
        // ──────────────────────────────────────────────────────────────────────────────

        private static byte[] AssertCommittedMatches(string fileName, Action<string> emit)
        {
            string temp = Path.Combine(NewTempDir(), fileName);
            emit(temp);
            byte[] emitted = File.ReadAllBytes(temp);
            AssertFileBytesEqual(fileName, temp);
            return emitted;
        }

        private static void AssertFileBytesEqual(string committedRelativeName, string emittedPath)
        {
            string committed = Path.Combine(CommittedTreDir(), committedRelativeName.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(committed),
                "Committed fixture missing: " + committed + " — run TreFixtureBuilderTests with GSD_EMIT_FIXTURES=1 to emit it.");
            Assert.Equal(File.ReadAllBytes(committed), File.ReadAllBytes(emittedPath));
        }

        private static string CommittedTreDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Utinni.Cli.Tests.csproj")))
                dir = dir.Parent;
            if (dir == null)
                throw new DirectoryNotFoundException("Could not locate Utinni.Cli.Tests.csproj from " + AppContext.BaseDirectory);
            return Path.Combine(dir.FullName, "Fixtures", "tre");
        }

        private static string NewTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "tre-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static byte[] InflateV6000Block(byte[] fixture, int tocOffsetField, int sizeField)
        {
            int offset = (int)ReadU32(fixture, tocOffsetField);
            int size = (int)ReadU32(fixture, sizeField);
            return InflateZlib(Take(fixture, offset, size));
        }

        private static byte[] InflateZlib(byte[] zlibFramed)
        {
            // Strip the 2-byte zlib header and 4-byte Adler trailer, inflate the raw deflate body.
            byte[] body = Take(zlibFramed, 2, zlibFramed.Length - 2 - 4);
            return InflateRawDeflate(body);
        }

        private static byte[] InflateRawDeflate(byte[] raw)
        {
            using (var ms = new MemoryStream(raw))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                ds.CopyTo(outMs);
                return outMs.ToArray();
            }
        }

        private static byte[] Take(byte[] src, int offset, int count)
        {
            byte[] r = new byte[count];
            Buffer.BlockCopy(src, offset, r, 0, count);
            return r;
        }

        private static uint ReadU32(byte[] b, int off) { return BitConverter.ToUInt32(b, off); }
        private static int ReadI32(byte[] b, int off) { return BitConverter.ToInt32(b, off); }
        private static ushort ReadU16(byte[] b, int off) { return BitConverter.ToUInt16(b, off); }
    }
}
