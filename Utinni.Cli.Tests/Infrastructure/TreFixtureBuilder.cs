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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Utinni.Cli.Tests.Infrastructure
{
    /// <summary>
    /// Deterministic, in-repo byte builder for the Phase-7 synthetic TRE / COT2000 test
    /// fixtures. This is TEST infrastructure only — it lives in Utinni.Cli.Tests, NOT in the
    /// shipping Formats/ assembly, and adds no write/authoring/export surface to the product
    /// (DEC-A3 stays locked, D-01).
    ///
    /// The builder is the GENERATOR OF RECORD for the committed Fixtures/tre/*.tre and
    /// Fixtures/tre/cot2000/*.tre files. TreFixtureBuilderTests regenerates each fixture into a
    /// temp path and asserts the committed bytes are byte-identical — so a hand-edited fixture
    /// fails CI (threat T-07-00-01). To (re)emit the committed fixtures into the source tree,
    /// set GSD_EMIT_FIXTURES=1 and run the EmitCommittedFixtures test (see TreFixtureBuilderTests).
    ///
    /// Determinism note: the zlib-framed blocks wrap System.IO.Compression.DeflateStream output.
    /// That output is stable for a given net472 BCL, so generate-and-compare matches on the same
    /// toolchain (the CI runner is the same machine that emits the committed bytes). This mirrors
    /// the existing synthesized-*-v000X.tre fixtures, whose deflate payloads are likewise committed.
    ///
    /// Verified byte layouts (07-RESEARCH "Per-TRE v6000 header" + "Detecting master-index kind",
    /// and the existing size-first synthesized-2record-v0006.tre decoded byte-for-byte):
    ///
    ///   v6000 per-TRE header (36): 0:"EERT" 4:"6000" 8:numFiles 12:tocOffset 16:tocCompressor(2=zlib)
    ///     20:sizeOfTOC 24:blockCompressor(2=zlib) 28:sizeOfNameBlock 32:uncompSizeOfNameBlock
    ///   v6000 TOC entry (32, crc-first): 0:u32 crc 4:i32 length 8:i32 offset 12:i32 compressor
    ///     16:i32 compressedLength 20:i32 fileNameOffset 24:8 pad
    ///
    ///   COT2000 master index: 0:" COT2000"(8) 8:reserved 12:numFiles 16:sizeOfTocBlock
    ///     20:sizeOfNameBlock 24:dupNameBlock 28:numTreeFiles 32:sizeOfTreeNameBlock
    ///     36:null-terminated .tre names (numTreeFiles) -> TOC block -> name block
    ///   COT2000 global TOC entry (32): 0:u8 compressor 1:u8 unused 2:u16 treeFileIndex 4:u32 crc
    ///     8:u32 fileNameLength 12:u32 offset 16:u32 length 20:u32 compressedLength 24:8 pad
    ///     offset/length/compressedLength are ARCHIVE-LOCAL — they index into the per-tree .tre
    ///     named at treeFileIndex (under cot2000/), NOT into the .toc. compressor enum (synthetic):
    ///     0=none, 1=raw-deflate, 2=zlib-framed. The synthetic fixture exercises 0 and 1; the real
    ///     env-gated COT2000 set exercises 2 / enumerate-only.
    ///   Fixture convention (07-01 reader honors this): the COT2000 TOC block and name block are
    ///     stored UNCOMPRESSED here. 07-01 detects zlib framing (0x78 0x9c) on those blocks so the
    ///     real archives (which zlib them) and this fixture both read through one code path.
    ///
    ///   size-first 0004/0005/0006 header (36): 0:"EERT" 4:tag 8:recordCount 12:infoOffset(=36)
    ///     16:infoCompression 20:infoCompressedSize 24:nameCompression 28:nameCompressedSize 32:nameSize
    ///   size-first record (24): 0:i32 uncompressedSize 4:i32 offset 8:i32 compression 12:i32 compressedSize
    ///     16:i32 checksum 20:i32 nameOffset      (compression: 0=none, 1=raw-deflate)
    /// </summary>
    public static class TreFixtureBuilder
    {
        // ── Adversarial constants (exposed so TreFixtureBuilderTests can assert the malformed shapes) ──

        /// <summary>numFiles declared by the count*stride-overflow fixture: numFiles*32 overflows int32.</summary>
        public const int MalformedCountStrideNumFiles = 100000000; // *32 = 3.2e9 > int.MaxValue

        /// <summary>offset declared by the offset+length-overflow fixture's TOC entry.</summary>
        public const int MalformedOverflowOffset = int.MaxValue - 0x0F; // 2147483632

        /// <summary>length declared by the offset+length-overflow fixture's TOC entry (offset+length wraps negative).</summary>
        public const int MalformedOverflowLength = 0x20; // 32

        /// <summary>compressor byte injected by the unknown-compressor fixture (not 0/1/2).</summary>
        public const int MalformedUnknownCompressor = 7;

        // ──────────────────────────────────────────────────────────────────────────────
        // Valid fixtures (Task 1)
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// v6000 per-TRE archive with a 2-record crc-first zlib TOC + zlib name block.
        /// </summary>
        public static void WriteV6000TwoRecord(string path)
        {
            byte[] payload0 = Ascii("v6000-payload-alpha");
            byte[] payload1 = Ascii("v6000-payload-beta!!");
            string[] names = { "alpha.iff", "beta.iff" };

            byte[] nameRaw = NameBlock(names, out int[] nameOffsets);

            const int headerSize = 36;
            int offset0 = headerSize;
            int offset1 = offset0 + payload0.Length;

            byte[] toc = V6000Toc(
                Entry(0x11111111, payload0.Length, offset0, 0, payload0.Length, nameOffsets[0]),
                Entry(0x22222222, payload1.Length, offset1, 0, payload1.Length, nameOffsets[1]));

            byte[] payloadRegion = Concat(payload0, payload1);
            WriteBytes(path, AssembleV6000(toc, nameRaw, payloadRegion, numFilesHeader: 2, corruptTocZlib: false));
        }

        /// <summary>
        /// Single-record v6000 archive whose zlib-framed TOC/name blocks inflate back to the
        /// expected uncompressed bytes — mirrors the strip-header+inflate path 07-01's reader uses.
        /// Named v6000 throughout (review item 5: filename, emitter, and any dispatch-test name).
        /// </summary>
        public static void WriteZlibFramedV6000OneRecord(string path)
        {
            byte[] payload0 = Ascii("zlib-roundtrip-payload");
            string[] names = { "solo.iff" };
            byte[] nameRaw = NameBlock(names, out int[] nameOffsets);

            byte[] toc = V6000Toc(
                Entry(0x33333333, payload0.Length, 36, 0, payload0.Length, nameOffsets[0]));

            WriteBytes(path, AssembleV6000(toc, nameRaw, payload0, numFilesHeader: 1, corruptTocZlib: false));
        }

        /// <summary>
        /// Self-contained COT2000 master index (>= 2 tree files) PLUS the two companion .tre
        /// archives its global TOC entries resolve into. The companion archives carry the readable
        /// payloads at the byte positions the global TOC declares, so a resolver opening
        /// cot2000/tree{idx}.tre at the declared offset reads exactly the entry's bytes (review
        /// consensus #2). entry0 -> tree0 (raw-deflate), entry1 -> tree1 (stored/none).
        /// </summary>
        public static void WriteCot2000TwoTree(string tocPath, string companionDir)
        {
            Directory.CreateDirectory(companionDir);

            // Companion 0: one raw-deflate payload (COT2000 entry0 compressor=1).
            byte[] payloadA = Ascii("COT2000 companion payload A -> object/tangible/foo.iff");
            V0006Result tree0 = BuildSizeFirstArchive("0006",
                new[] { "alpha_payload.iff" }, new[] { payloadA }, new[] { 1 });

            // Companion 1: one stored/none payload (COT2000 entry1 compressor=0).
            byte[] payloadB = Ascii("COT2000 companion payload B -> string/en/bar.stf");
            V0006Result tree1 = BuildSizeFirstArchive("0006",
                new[] { "beta_payload.stf" }, new[] { payloadB }, new[] { 0 });

            WriteBytes(Path.Combine(companionDir, "tree0.tre"), tree0.Bytes);
            WriteBytes(Path.Combine(companionDir, "tree1.tre"), tree1.Bytes);

            string[] vpaths = { "object/tangible/foo.iff", "string/en/bar.stf" };

            // Global TOC (uncompressed in this fixture; 07-01 detects framing).
            var toc = new MemoryStream();
            WriteCot2000Entry(toc, compressor: 1, treeFileIndex: 0, crc: 0xAAAA0001,
                fileNameLength: vpaths[0].Length, offset: tree0.Locs[0].Offset,
                length: tree0.Locs[0].UncompressedLength, compressedLength: tree0.Locs[0].StoredLength);
            WriteCot2000Entry(toc, compressor: 0, treeFileIndex: 1, crc: 0xBBBB0002,
                fileNameLength: vpaths[1].Length, offset: tree1.Locs[0].Offset,
                length: tree1.Locs[0].UncompressedLength, compressedLength: tree1.Locs[0].StoredLength);
            byte[] tocBlock = toc.ToArray();

            // Tree-file names are stored as paths RELATIVE TO THE .toc's directory so a resolver
            // can open them via Path.Combine(masterIndexBaseDir, treeName). The companions live in
            // a cot2000/ subdir here; real COT2000 sets keep them beside the .toc (bare names also
            // resolve). The resolver containment-checks the result (rejects ../rooted).
            byte[] treeNames = NameBlock(new[] { "cot2000/tree0.tre", "cot2000/tree1.tre" }, out int[] _);
            byte[] nameBlock = NameBlock(vpaths, out int[] __);

            var ms = new MemoryStream();
            ms.Write(Ascii(" COT2000"), 0, 8);
            WriteU32(ms, 0);                          // 8  reserved
            WriteU32(ms, 2);                          // 12 numFiles (global entries)
            WriteU32(ms, (uint)tocBlock.Length);      // 16 sizeOfTocBlock
            WriteU32(ms, (uint)nameBlock.Length);     // 20 sizeOfNameBlock
            WriteU32(ms, 0);                          // 24 dupNameBlock (reserved/unknown in fixture)
            WriteU32(ms, 2);                          // 28 numTreeFiles
            WriteU32(ms, (uint)treeNames.Length);     // 32 sizeOfTreeNameBlock
            ms.Write(treeNames, 0, treeNames.Length); // 36 tree names
            ms.Write(tocBlock, 0, tocBlock.Length);   //    TOC block
            ms.Write(nameBlock, 0, nameBlock.Length); //    name block
            WriteBytes(tocPath, ms.ToArray());
        }

        /// <summary>
        /// Synthetic 5000 header whose record region is DELIBERATELY not a coherent 32-byte
        /// crc-first 6000 TOC (a short garbage region) — so 07-01's "5000 recognized,
        /// enumerate-empty, no throw" contract is provable against a real, non-6000 artifact.
        /// </summary>
        public static void WriteSynthetic5000(string path)
        {
            var ms = new MemoryStream();
            ms.Write(Ascii("EERT"), 0, 4);
            ms.Write(Ascii("5000"), 0, 4);
            WriteU32(ms, 2);   // numFiles claims 2 ...
            WriteU32(ms, 36);  // tocOffset
            WriteU32(ms, 2);   // tocCompressor
            WriteU32(ms, 10);  // sizeOfTOC (too small to be 2*32 — deliberately incoherent)
            WriteU32(ms, 2);   // blockCompressor
            WriteU32(ms, 0);   // sizeOfNameBlock
            WriteU32(ms, 0);   // uncompSizeOfNameBlock
            // ... but only 10 bytes of garbage follow — NOT a valid 64-byte crc-first TOC.
            for (int i = 0; i < 10; i++) ms.WriteByte(0xCC);
            WriteBytes(path, ms.ToArray());
        }

        /// <summary>
        /// Synthetic 0004 header reusing the size-first 24-byte stride family (same as the existing
        /// 0005/0006 synthesized fixtures). NOTE: the real SWGEmu 0004 record order is UNVERIFIED —
        /// no real 0004 fixture exists on this machine (07-VALIDATION Wave 0). This fixture exists
        /// only so 07-01 can prove 0004 is recognized via the size-first path; do not treat its
        /// layout as the canonical engine 0004 layout.
        /// </summary>
        public static void WriteSynthetic0004(string path)
        {
            V0006Result a = BuildSizeFirstArchive("0004",
                new[] { "marker0004.txt" }, new[] { Ascii("v0004 marker") }, new[] { 0 });
            WriteBytes(path, a.Bytes);
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Malformed / adversarial fixtures (Task 2)
        // Each starts from the valid v6000 base, then injects EXACTLY ONE adversarial field so the
        // failure mode is unambiguous. Each maps to a 07-01 threat ID:
        //   count*stride overflow      -> T-07-01 (recordCount > (len-header)/stride guard)
        //   offset+length overflow     -> T-07-03 (offset <= streamLength - length guard)
        //   truncated/invalid zlib frame-> T-07-04 (inflate-side short read / DeflateStream error)
        //   unknown compressor         -> documented unknown-compressor error kind
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// numFiles declared so numFiles*32 overflows int32 and exceeds the file length — 07-01's
        /// division-form guard `recordCount > (streamLength - headerSize) / stride` must reject it
        /// before any allocation (T-07-01).
        /// </summary>
        public static void WriteMalformedCountStrideOverflow(string path)
        {
            byte[] payload0 = Ascii("x");
            byte[] nameRaw = NameBlock(new[] { "a.iff" }, out int[] nameOffsets);
            byte[] toc = V6000Toc(Entry(0x1, payload0.Length, 36, 0, payload0.Length, nameOffsets[0]));
            // Header lies: numFiles = 100,000,000 (×32 overflows int and dwarfs the ~tiny file).
            WriteBytes(path, AssembleV6000(toc, nameRaw, payload0,
                numFilesHeader: MalformedCountStrideNumFiles, corruptTocZlib: false));
        }

        /// <summary>
        /// A TOC entry whose offset+length exceeds the stream length, with an offset chosen so
        /// naive `offset + length` int math wraps negative — 07-01's `offset &lt;= streamLength - length`
        /// guard must reject it (T-07-03).
        /// </summary>
        public static void WriteMalformedOffsetLengthOverflow(string path)
        {
            byte[] payload0 = Ascii("x");
            byte[] nameRaw = NameBlock(new[] { "a.iff" }, out int[] nameOffsets);
            // Inject the wrap-negative offset/length into the single TOC entry.
            byte[] toc = V6000Toc(Entry(0x1, MalformedOverflowLength, MalformedOverflowOffset, 0,
                MalformedOverflowLength, nameOffsets[0]));
            WriteBytes(path, AssembleV6000(toc, nameRaw, payload0, numFilesHeader: 1, corruptTocZlib: false));
        }

        /// <summary>
        /// A zlib block with a valid 0x78 0x9c header but a truncated/garbled deflate body AND a
        /// mangled Adler trailer — so the failure is caught on the INFLATE side (short read /
        /// DeflateStream error), NOT by Adler validation the BCL skips (review Codex item 11).
        /// On-disk name kept as malformed-zlib-bad-adler.tre for continuity, but the DETECTABLE
        /// failure is the invalid frame, not the Adler bytes alone.
        /// </summary>
        public static void WriteMalformedZlibBadFrame(string path)
        {
            byte[] payload0 = Ascii("x");
            byte[] nameRaw = NameBlock(new[] { "a.iff" }, out int[] nameOffsets);
            byte[] toc = V6000Toc(Entry(0x1, payload0.Length, 36, 0, payload0.Length, nameOffsets[0]));
            WriteBytes(path, AssembleV6000(toc, nameRaw, payload0, numFilesHeader: 1, corruptTocZlib: true));
        }

        /// <summary>
        /// A TOC entry whose compressor byte is an unrecognized value (7 — neither 0/none, 1/deflate,
        /// nor 2/zlib) so 07-01 raises a documented unknown-compressor error kind.
        /// </summary>
        public static void WriteMalformedUnknownCompressorFixture(string path)
        {
            byte[] payload0 = Ascii("x");
            byte[] nameRaw = NameBlock(new[] { "a.iff" }, out int[] nameOffsets);
            byte[] toc = V6000Toc(Entry(0x1, payload0.Length, 36, MalformedUnknownCompressor,
                payload0.Length, nameOffsets[0]));
            WriteBytes(path, AssembleV6000(toc, nameRaw, payload0, numFilesHeader: 1, corruptTocZlib: false));
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // Batch emit (used by the env-gated generator in TreFixtureBuilderTests)
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Emits every committed fixture into <paramref name="treDir"/> (and treDir/cot2000).
        /// Filenames match the committed fixtures the 07-01 golden tests load.
        /// </summary>
        public static void EmitAll(string treDir)
        {
            Directory.CreateDirectory(treDir);
            WriteV6000TwoRecord(Path.Combine(treDir, "synthetic-v6000-2record.tre"));
            WriteZlibFramedV6000OneRecord(Path.Combine(treDir, "zlib-framed-1record-v6000.tre"));
            WriteCot2000TwoTree(Path.Combine(treDir, "synthetic-cot2000-2tree.toc"), Path.Combine(treDir, "cot2000"));
            WriteSynthetic5000(Path.Combine(treDir, "synthetic-5000-header.tre"));
            WriteSynthetic0004(Path.Combine(treDir, "synthetic-0004-header.tre"));
            WriteMalformedCountStrideOverflow(Path.Combine(treDir, "malformed-count-stride-overflow.tre"));
            WriteMalformedOffsetLengthOverflow(Path.Combine(treDir, "malformed-offset-length-overflow.tre"));
            WriteMalformedZlibBadFrame(Path.Combine(treDir, "malformed-zlib-bad-adler.tre"));
            WriteMalformedUnknownCompressorFixture(Path.Combine(treDir, "malformed-unknown-compressor.tre"));
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // v6000 assembly helpers
        // ──────────────────────────────────────────────────────────────────────────────

        private static int[] Entry(uint crc, int length, int offset, int compressor, int compressedLength, int fileNameOffset)
        {
            return new[] { unchecked((int)crc), length, offset, compressor, compressedLength, fileNameOffset };
        }

        private static byte[] V6000Toc(params int[][] entries)
        {
            var ms = new MemoryStream();
            foreach (int[] e in entries)
            {
                WriteI32(ms, e[0]); // crc
                WriteI32(ms, e[1]); // length
                WriteI32(ms, e[2]); // offset
                WriteI32(ms, e[3]); // compressor
                WriteI32(ms, e[4]); // compressedLength
                WriteI32(ms, e[5]); // fileNameOffset
                ms.Write(new byte[8], 0, 8); // pad
            }
            return ms.ToArray();
        }

        private static byte[] AssembleV6000(byte[] tocRaw, byte[] nameRaw, byte[] payloadRegion, int numFilesHeader, bool corruptTocZlib)
        {
            byte[] tocZlib = corruptTocZlib ? InvalidZlibFrame() : ZlibFrame(tocRaw);
            byte[] nameZlib = ZlibFrame(nameRaw);

            const int headerSize = 36;
            int tocOffset = headerSize + payloadRegion.Length;

            var ms = new MemoryStream();
            ms.Write(Ascii("EERT"), 0, 4);
            ms.Write(Ascii("6000"), 0, 4);
            WriteU32(ms, (uint)numFilesHeader);     // 8
            WriteU32(ms, (uint)tocOffset);          // 12
            WriteU32(ms, 2);                        // 16 tocCompressor = zlib
            WriteU32(ms, (uint)tocZlib.Length);     // 20 sizeOfTOC
            WriteU32(ms, 2);                        // 24 blockCompressor = zlib
            WriteU32(ms, (uint)nameZlib.Length);    // 28 sizeOfNameBlock
            WriteU32(ms, (uint)nameRaw.Length);     // 32 uncompSizeOfNameBlock
            ms.Write(payloadRegion, 0, payloadRegion.Length);
            ms.Write(tocZlib, 0, tocZlib.Length);
            ms.Write(nameZlib, 0, nameZlib.Length);
            return ms.ToArray();
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // size-first (0004/0005/0006) archive builder
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>One payload's archive-local position inside a built size-first archive.</summary>
        public struct PayloadLoc
        {
            public int Offset;
            public int StoredLength;
            public int UncompressedLength;
            public int Compressor;
        }

        private sealed class V0006Result
        {
            public byte[] Bytes;
            public List<PayloadLoc> Locs;
        }

        private static V0006Result BuildSizeFirstArchive(string tag, string[] names, byte[][] payloads, int[] compression)
        {
            int n = names.Length;
            byte[] nameBlock = NameBlock(names, out int[] nameOffsets);

            byte[][] stored = new byte[n][];
            for (int i = 0; i < n; i++)
                stored[i] = compression[i] == 1 ? RawDeflate(payloads[i]) : payloads[i];

            int tocSize = n * 24;
            int payloadStart = 36 + tocSize + nameBlock.Length;

            var locs = new List<PayloadLoc>();
            var tocMs = new MemoryStream();
            int pcur = payloadStart;
            for (int i = 0; i < n; i++)
            {
                int off = pcur;
                int storedLen = stored[i].Length;
                int uncomp = payloads[i].Length;
                WriteI32(tocMs, uncomp);          // uncompressedSize
                WriteI32(tocMs, off);             // offset
                WriteI32(tocMs, compression[i]);  // compression
                WriteI32(tocMs, storedLen);       // compressedSize
                WriteI32(tocMs, 0);               // checksum
                WriteI32(tocMs, nameOffsets[i]);  // nameOffset
                locs.Add(new PayloadLoc { Offset = off, StoredLength = storedLen, UncompressedLength = uncomp, Compressor = compression[i] });
                pcur += storedLen;
            }
            byte[] toc = tocMs.ToArray();

            var ms = new MemoryStream();
            ms.Write(Ascii("EERT"), 0, 4);
            ms.Write(Ascii(tag), 0, 4);
            WriteI32(ms, n);                  // recordCount
            WriteI32(ms, 36);                 // infoOffset
            WriteI32(ms, 0);                  // infoCompression
            WriteI32(ms, tocSize);            // infoCompressedSize
            WriteI32(ms, 0);                  // nameCompression
            WriteI32(ms, nameBlock.Length);   // nameCompressedSize
            WriteI32(ms, nameBlock.Length);   // nameSize
            ms.Write(toc, 0, toc.Length);
            ms.Write(nameBlock, 0, nameBlock.Length);
            for (int i = 0; i < n; i++) ms.Write(stored[i], 0, stored[i].Length);
            return new V0006Result { Bytes = ms.ToArray(), Locs = locs };
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // COT2000 entry writer
        // ──────────────────────────────────────────────────────────────────────────────

        private static void WriteCot2000Entry(Stream s, int compressor, int treeFileIndex, uint crc,
            int fileNameLength, int offset, int length, int compressedLength)
        {
            s.WriteByte((byte)compressor);   // 0 u8 compressor
            s.WriteByte(0);                  // 1 u8 unused
            WriteU16(s, (ushort)treeFileIndex); // 2 u16 treeFileIndex
            WriteU32(s, crc);                // 4 u32 crc
            WriteU32(s, (uint)fileNameLength); // 8 u32 fileNameLength
            WriteU32(s, (uint)offset);       // 12 u32 offset (archive-local)
            WriteU32(s, (uint)length);       // 16 u32 length (uncompressed)
            WriteU32(s, (uint)compressedLength); // 20 u32 compressedLength
            s.Write(new byte[8], 0, 8);      // 24 8 pad
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // zlib / deflate / adler32
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>RFC1950 zlib frame: 0x78 0x9c + raw deflate body + 4-byte big-endian Adler32.</summary>
        private static byte[] ZlibFrame(byte[] uncompressed)
        {
            byte[] raw = RawDeflate(uncompressed);
            var ms = new MemoryStream();
            ms.WriteByte(0x78);
            ms.WriteByte(0x9C);
            ms.Write(raw, 0, raw.Length);
            uint adler = Adler32(uncompressed);
            ms.WriteByte((byte)(adler >> 24));
            ms.WriteByte((byte)(adler >> 16));
            ms.WriteByte((byte)(adler >> 8));
            ms.WriteByte((byte)adler);
            return ms.ToArray();
        }

        /// <summary>
        /// A zlib frame with a valid 0x78 0x9c header but a deliberately truncated/garbled body and
        /// corrupt trailer. The failure is detectable on the inflate side (DeflateStream error /
        /// short read), independent of Adler validation.
        /// </summary>
        private static byte[] InvalidZlibFrame()
        {
            return new byte[] { 0x78, 0x9C, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x01 };
        }

        private static byte[] RawDeflate(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionMode.Compress, leaveOpen: true))
                {
                    ds.Write(data, 0, data.Length);
                }
                return ms.ToArray();
            }
        }

        private static uint Adler32(byte[] data)
        {
            const uint mod = 65521;
            uint a = 1, b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + data[i]) % mod;
                b = (b + a) % mod;
            }
            return (b << 16) | a;
        }

        // ──────────────────────────────────────────────────────────────────────────────
        // primitives
        // ──────────────────────────────────────────────────────────────────────────────

        private static byte[] NameBlock(string[] names, out int[] offsets)
        {
            offsets = new int[names.Length];
            var ms = new MemoryStream();
            int cum = 0;
            for (int i = 0; i < names.Length; i++)
            {
                offsets[i] = cum;
                byte[] nb = Ascii(names[i]);
                ms.Write(nb, 0, nb.Length);
                ms.WriteByte(0);
                cum += nb.Length + 1;
            }
            return ms.ToArray();
        }

        private static byte[] Ascii(string s)
        {
            return Encoding.ASCII.GetBytes(s);
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            byte[] r = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, r, 0, a.Length);
            Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
            return r;
        }

        private static void WriteU16(Stream s, ushort v) { s.Write(BitConverter.GetBytes(v), 0, 2); }
        private static void WriteU32(Stream s, uint v) { s.Write(BitConverter.GetBytes(v), 0, 4); }
        private static void WriteI32(Stream s, int v) { s.Write(BitConverter.GetBytes(v), 0, 4); }

        private static void WriteBytes(string path, byte[] bytes)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, bytes);
        }
    }
}
