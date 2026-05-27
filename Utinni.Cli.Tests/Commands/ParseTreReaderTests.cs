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
using UtinniCoreDotNet.Formats.Tre;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// 07-01 reader-level tests for the version-dispatching, zlib-aware, lazy TRE reader,
    /// asserted against the 07-00 in-repo synthetic fixtures (+ env-gated real archives).
    /// (Class name contains "ParseTre" so `dotnet test --filter ParseTre` runs them.)
    /// </summary>
    public class ParseTreReaderTests
    {
        private static string Fx(string name) => FixturePath.Resolve("tre/" + name);

        // ── Version dispatch ────────────────────────────────────────────────

        [Fact]
        public void ParseTre_VersionDispatch_ParsesKnownTags_RejectsUnknown()
        {
            Assert.Equal(TreVersion.V0004, TreVersions.Parse("0004"));
            Assert.Equal(TreVersion.V0005, TreVersions.Parse("0005"));
            Assert.Equal(TreVersion.V0006, TreVersions.Parse("0006"));
            Assert.Equal(TreVersion.V5000, TreVersions.Parse("5000"));
            Assert.Equal(TreVersion.V6000, TreVersions.Parse("6000"));

            var ex = Record.Exception(() => TreVersions.Parse("9999"));
            var treEx = Assert.IsType<TreParseException>(ex);
            Assert.Equal(TreParseError.UnsupportedVersion, treEx.Kind);

            Assert.False(TreVersions.IsEnumerateOnly(TreVersion.V5000)); // readable SWGEmu Pre-CU
            Assert.True(TreVersions.IsEnumerateOnly(TreVersion.V6000));
            Assert.False(TreVersions.IsEnumerateOnly(TreVersion.V0005));
        }

        // ── 0004 dedicated golden (review item 8) ───────────────────────────

        [Fact]
        public void ParseTre_0004Header_EnumeratesRecords()
        {
            // The synthetic 0004 fixture (07-00) uses the size-first family; assert its OWN shape
            // enumerates without throwing (the recognized 0004 tag is a TESTED path).
            TreFile tre = TreFile.Open(Fx("synthetic-0004-header.tre"));

            Assert.Equal("0004", tre.Header.VersionTag);
            Assert.Equal(TreVersion.V0004, tre.Header.Version);
            Assert.False(tre.Header.EnumerateOnly);
            Assert.Equal(1, tre.Records.Count);
            Assert.Equal("marker0004.txt", tre.Records[0].Name);
        }

        // ── v6000 crc-first enumeration + enumerate-vs-payload distinction ──

        [Fact]
        public void ParseTre_V6000_EnumeratesCrcFirstToc_AndResolvesNames()
        {
            TreFile tre = TreFile.Open(Fx("synthetic-v6000-2record.tre"));

            Assert.Equal("6000", tre.Header.VersionTag);
            Assert.Equal(TreVersion.V6000, tre.Header.Version);
            Assert.Equal(2, tre.Records.Count);
            // Names resolve from the zlib-framed name block (the reader DID inflate v6000 metadata)...
            Assert.Equal("alpha.iff", tre.Records[0].Name);
            Assert.Equal("beta.iff",  tre.Records[1].Name);
            // ...WHILE payloads are flagged enumerate-only (encrypted/obfuscated in real archives, D-07).
            Assert.True(tre.Header.EnumerateOnly);
        }

        [Fact]
        public void ParseTre_ZlibFramedV6000OneRecord_InflatesTocAndNameBlocks()
        {
            // Proves the zlib (0x78 0x9c) TOC + name blocks inflate to the expected metadata.
            TreFile tre = TreFile.Open(Fx("zlib-framed-1record-v6000.tre"));
            Assert.Equal(TreVersion.V6000, tre.Header.Version);
            Assert.Equal(1, tre.Records.Count);
            Assert.Equal("solo.iff", tre.Records[0].Name);
        }

        // ── 5000 enumerate-empty / no throw (review consensus #1) ───────────

        [Fact]
        public void ParseTre_V5000_EnumeratesRecords_CrcFirst24()
        {
            // 5000 is the readable SWGEmu Pre-CU format (crc-first 24-byte stride, zlib blocks) —
            // verified against the live client. It enumerates records; NOT enumerate-only.
            TreFile tre = TreFile.Open(Fx("synthetic-5000-header.tre"));
            Assert.Equal("5000", tre.Header.VersionTag);
            Assert.Equal(TreVersion.V5000, tre.Header.Version);
            Assert.False(tre.Header.EnumerateOnly);
            Assert.Equal(2, tre.Records.Count);
            Assert.Equal("texture/alpha.dds", tre.Records[0].Name);
            Assert.Equal("appearance/beta.msh", tre.Records[1].Name);
        }

        // ── Malformed inputs raise documented kinds (not OOM) ───────────────

        [Fact]
        public void ParseTre_MalformedZlibBadFrame_ThrowsInvalidZlibTrailer()
        {
            var ex = Record.Exception(() => TreFile.Open(Fx("malformed-zlib-bad-adler.tre")));
            var treEx = Assert.IsType<TreParseException>(ex);
            Assert.Equal(TreParseError.InvalidZlibTrailer, treEx.Kind);
        }

        [Fact]
        public void ParseTre_MalformedUnknownCompressor_ThrowsUnknownCompressor()
        {
            var ex = Record.Exception(() => TreFile.Open(Fx("malformed-unknown-compressor.tre")));
            var treEx = Assert.IsType<TreParseException>(ex);
            Assert.Equal(TreParseError.UnknownCompressor, treEx.Kind);
        }

        [Fact]
        public void ParseTre_MalformedCountStride_ThrowsParseException_NotOom()
        {
            var ex = Record.Exception(() => TreFile.Open(Fx("malformed-count-stride-overflow.tre")));
            var treEx = Assert.IsType<TreParseException>(ex);
            // The division-form guard rejects the count before any allocation.
            Assert.Equal(TreParseError.ChunkLengthExceedsCap, treEx.Kind);
        }

        [Fact]
        public void ParseTre_MalformedOffsetLength_ThrowsParseException()
        {
            var ex = Record.Exception(() => TreFile.Open(Fx("malformed-offset-length-overflow.tre")));
            var treEx = Assert.IsType<TreParseException>(ex);
            // The subtraction-form guard (offset <= streamLength - length) rejects it.
            Assert.Equal(TreParseError.Truncated, treEx.Kind);
        }

        // ── Lazy proof: parse-level enumeration reads zero payloads ─────────

        [Fact]
        public void ParseTre_Enumeration_PerformsZeroPayloadReads()
        {
            TreFile tre = TreFile.Open(Fx("synthetic-v6000-2record.tre"));
            Assert.Equal(0, tre.PayloadReadCount); // InternalsVisibleTo("Utinni.Cli.Tests")
        }

        // ── Env-gated real archive (Skips cleanly when SWG_SAMPLE_TRE_DIR unset) ──

        [Fact]
        public void ParseTre_EnvGatedRealV6000_ResolvesKnownRecord()
        {
            if (!FixturePath.HasSampleTreDir())
            {
                return; // SUPPLEMENTARY real-archive golden — skip cleanly when the sample set is absent.
            }

            string real = Path.Combine(FixturePath.SampleTreDir(), "SwgRestoration_06.tre");
            if (!File.Exists(real))
            {
                return; // sample dir present but this archive isn't — skip cleanly.
            }

            TreFile tre = TreFile.Open(real);
            Assert.Equal(TreVersion.V6000, tre.Header.Version);
            var rec0 = tre.Records.FirstOrDefault(r => r.Name == "playback/fire_projectiles_arc.pst");
            Assert.NotNull(rec0);
            Assert.Equal(344, rec0.UncompressedSize);
        }
    }
}
