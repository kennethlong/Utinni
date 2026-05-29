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

namespace UtinniCoreDotNet.Tests.FormatsTests.Tre
{
    /// <summary>
    /// In-memory TRE byte builders for Tier-1 parser tests and synthesized fixture generation.
    /// Each method returns a byte[] that represents a valid or intentionally malformed TRE file.
    /// No external files are required.
    /// </summary>
    public static class TreFileFixtures
    {
        // ─────────────────────────────────────────────────────────────────────
        // Happy-path builders
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a 3-record TRE (version 0005):
        ///   Record 0: uncompressed "Hello, World!" → name "hello.txt"
        ///   Record 1: deflate-compressed "The quick brown fox jumps over the lazy dog." → name "quick.txt"
        ///   Record 2: uncompressed empty payload → name "empty.bin"
        /// </summary>
        public static byte[] BuildValidV0005ThreeRecord()
        {
            return BuildMultiRecordTre("0005", new[]
            {
                new RecordSpec { Name = "hello.txt",  Payload = Encoding.ASCII.GetBytes("Hello, World!"),                                       Compress = false },
                new RecordSpec { Name = "quick.txt",  Payload = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog."),         Compress = true  },
                new RecordSpec { Name = "empty.bin",  Payload = new byte[0],                                                                     Compress = false }
            });
        }

        /// <summary>
        /// Builds a 5-record TRE (version 0005) used by the 08-07 repack contract tests
        /// (round-trip byte-identity, edit→repack→reopen logical-path resolution, locked
        /// archive refusal, backup uniqueness, byte-diff per untouched record):
        ///   Record 0: uncompressed "RECORD ZERO content"             → name "datatable/foo/zero.iff"
        ///   Record 1: deflate-compressed "RECORD ONE content"         → name "datatable/foo/one.iff"
        ///   Record 2: uncompressed "RECORD TWO content (target)"      → name "datatable/foo/bar.iff" (the EDITED entry)
        ///   Record 3: deflate-compressed "RECORD THREE content"       → name "datatable/foo/three.iff"
        ///   Record 4: uncompressed "RECORD FOUR content"              → name "datatable/foo/four.iff"
        /// Five records lets the new repack tests edit index 2 and assert byte-identity
        /// for all four untouched records (0, 1, 3, 4) — explicitly mixing compressed
        /// and uncompressed neighbours to exercise the raw-slice copy path for both.
        /// The "datatable/foo/" logical-path prefix gives Test 2 a stable
        /// edit→repack→reopen path-CRC self-consistency target.
        /// </summary>
        public static byte[] BuildValidV0005FiveRecord()
        {
            return BuildMultiRecordTre("0005", new[]
            {
                new RecordSpec { Name = "datatable/foo/zero.iff",  Payload = Encoding.ASCII.GetBytes("RECORD ZERO content"),         Compress = false },
                new RecordSpec { Name = "datatable/foo/one.iff",   Payload = Encoding.ASCII.GetBytes("RECORD ONE content"),          Compress = true  },
                new RecordSpec { Name = "datatable/foo/bar.iff",   Payload = Encoding.ASCII.GetBytes("RECORD TWO content (target)"),  Compress = false },
                new RecordSpec { Name = "datatable/foo/three.iff", Payload = Encoding.ASCII.GetBytes("RECORD THREE content"),        Compress = true  },
                new RecordSpec { Name = "datatable/foo/four.iff",  Payload = Encoding.ASCII.GetBytes("RECORD FOUR content"),         Compress = false }
            });
        }

        /// <summary>
        /// Builds a 2-record TRE (version 0006):
        ///   Record 0: uncompressed "v0006 marker" → name "marker.txt"
        ///   Record 1: deflate-compressed "Phase 4 v0006 coverage" → name "v6.txt"
        /// Demonstrates v0006 header parses identically to v0005 (REVIEWS LOW-20).
        /// </summary>
        public static byte[] BuildValidV0006TwoRecord()
        {
            return BuildMultiRecordTre("0006", new[]
            {
                new RecordSpec { Name = "marker.txt", Payload = Encoding.ASCII.GetBytes("v0006 marker"),          Compress = false },
                new RecordSpec { Name = "v6.txt",     Payload = Encoding.ASCII.GetBytes("Phase 4 v0006 coverage"), Compress = true  }
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Negative case builders
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Builds bytes with an invalid magic ("XXXX" instead of "EERT").</summary>
        public static byte[] BuildBadMagic()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                WriteAscii4(bw, "XXXX"); // bad magic
                WriteAscii4(bw, "0005"); // version
                bw.Write(0);  // RecordCount
                bw.Write(36); // InfoOffset (right after header)
                bw.Write(0);  // InfoCompression
                bw.Write(0);  // InfoCompressedSize
                bw.Write(0);  // NameCompression
                bw.Write(0);  // NameCompressedSize
                bw.Write(0);  // NameSize
                return ms.ToArray();
            }
        }

        /// <summary>Builds bytes with magic "EERT" but unsupported version "0009".</summary>
        public static byte[] BuildUnsupportedVersion(string version = "0009")
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                WriteAscii4(bw, "EERT");
                WriteAscii4(bw, version);
                bw.Write(0);  // RecordCount
                bw.Write(36); // InfoOffset
                bw.Write(0);  // InfoCompression
                bw.Write(0);  // InfoCompressedSize
                bw.Write(0);  // NameCompression
                bw.Write(0);  // NameCompressedSize
                bw.Write(0);  // NameSize
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds a TRE claiming 3 records but only containing enough bytes for 1 record
        /// info entry — triggers TreParseException(Truncated).
        /// </summary>
        public static byte[] BuildTruncatedRecordTable()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                // Header: 8 bytes magic+version + 28 bytes header fields = 36 bytes
                WriteAscii4(bw, "EERT");
                WriteAscii4(bw, "0005");

                int recordCount = 3;
                int infoOffset  = 36; // immediately after header
                // Only write enough for 1 record (24 bytes), not 3 (72 bytes)
                int infoCompressedSize = 24; // too small for 3 records

                bw.Write(recordCount);
                bw.Write(infoOffset);
                bw.Write(0);  // InfoCompression
                bw.Write(infoCompressedSize);
                bw.Write(0);  // NameCompression
                bw.Write(0);  // NameCompressedSize
                bw.Write(0);  // NameSize

                // Write only 1 record info struct (24 bytes) instead of 3
                WriteRecordInfo(bw, dataSize: 4, dataOffset: 36 + 24, dataCompression: 0, dataCompressedSize: 4, checksum: 0, nameOffset: 0);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds a TRE with a deflate-compressed record containing the given payload.
        /// Used to test the eager-read + post-dispose GetRecordData behaviour (REVIEWS HIGH-4).
        /// </summary>
        public static byte[] BuildDeflatedRecord(byte[] payload, string version = "0005")
        {
            return BuildMultiRecordTre(version, new[]
            {
                new RecordSpec { Name = "deflated.bin", Payload = payload, Compress = true }
            });
        }

        /// <summary>
        /// Builds a TRE whose record 0 claims DataSize = 1_000_000_000 (1 GB, well above the 256 MB cap)
        /// but has a tiny (10-byte) compressed payload. Used to test the DeflateExpansionExceedsCap
        /// check (T-04-DoS).
        /// </summary>
        public static byte[] BuildClaimedGigabyteDeflate()
        {
            // Build the TRE manually: 1 record claiming 1 GB uncompressed but compressed to a trivial payload.
            byte[] fakePayload = DeflateBytes(Encoding.ASCII.GetBytes("hi")); // real but tiny deflate data

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                WriteAscii4(bw, "EERT");
                WriteAscii4(bw, "0005");

                const int recordCount = 1;
                string name = "giant.bin";
                byte[] nameBlock = Encoding.ASCII.GetBytes(name + "\0");

                // Info block: 1 record * 24 bytes = 24 bytes
                byte[] infoBlock;
                using (var ims = new MemoryStream())
                using (var ibw = new BinaryWriter(ims, Encoding.ASCII, leaveOpen: true))
                {
                    const int dataSize            = 1_000_000_000; // 1 GB claimed (T-04-DoS)
                    int       infoBlockSize       = 24;
                    int       namesBlockSize       = nameBlock.Length;
                    int       dataPayloadSize      = fakePayload.Length;
                    int       headerEnd            = 36;
                    int       infoOffset           = headerEnd;
                    int       namesBlockOffset     = infoOffset + infoBlockSize;
                    int       dataOffset           = namesBlockOffset + namesBlockSize;

                    ibw.Write(dataSize);            // DataSize (1 GB)
                    ibw.Write(dataOffset);          // DataOffset
                    ibw.Write(1);                   // DataCompression = deflate
                    ibw.Write(dataPayloadSize);     // DataCompressedSize
                    ibw.Write(0);                   // Checksum
                    ibw.Write(0);                   // NameOffset
                    infoBlock = ims.ToArray();

                    // Now write header
                    bw.Write(recordCount);
                    bw.Write(infoOffset);
                    bw.Write(0);                    // InfoCompression = none
                    bw.Write(infoBlockSize);
                    bw.Write(0);                    // NameCompression = none
                    bw.Write(namesBlockSize);
                    bw.Write(namesBlockSize);       // NameSize = compressed size (no compression)
                }
                ms.Write(infoBlock, 0, infoBlock.Length);
                ms.Write(nameBlock, 0, nameBlock.Length);
                ms.Write(fakePayload, 0, fakePayload.Length);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds a TRE whose RecordCount field is -1 (negative). Triggers NegativeLength.
        /// </summary>
        public static byte[] BuildNegativeResourceCount()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                WriteAscii4(bw, "EERT");
                WriteAscii4(bw, "0005");
                bw.Write(-1);  // RecordCount = -1 (negative)
                bw.Write(36);  // InfoOffset
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds a TRE with InfoOffset = 999_999 in a 100-byte file. Triggers Truncated or
        /// ChunkLengthExceedsCap because the declared info block offset exceeds the file size.
        /// </summary>
        public static byte[] BuildInfoOffsetExceedsFile()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                WriteAscii4(bw, "EERT");
                WriteAscii4(bw, "0005");
                bw.Write(1);          // RecordCount
                bw.Write(999_999);    // InfoOffset — way beyond end of file
                bw.Write(0);
                bw.Write(24);         // InfoCompressedSize (1 record * 24 bytes)
                bw.Write(0);
                bw.Write(0);
                bw.Write(0);
                // Pad to ~100 bytes total
                for (int i = 0; i < 64; i++) bw.Write((byte)0);
                return ms.ToArray();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private sealed class RecordSpec
        {
            public string Name    { get; set; }
            public byte[] Payload { get; set; }
            public bool   Compress { get; set; }
        }

        /// <summary>
        /// Generic multi-record TRE builder used by the happy-path fixture methods.
        /// Computes all offsets dynamically to produce a structurally valid TRE.
        /// </summary>
        private static byte[] BuildMultiRecordTre(string version, RecordSpec[] specs)
        {
            int recordCount = specs.Length;
            const int headerSize = 36; // 8 (magic+version) + 28 (header fields)
            int infoBlockSize = recordCount * 24;

            // Prepare payloads and names block.
            byte[][] payloads = new byte[recordCount][];
            bool[] compressed = new bool[recordCount];
            for (int i = 0; i < recordCount; i++)
            {
                if (specs[i].Compress && specs[i].Payload.Length > 0)
                {
                    payloads[i] = DeflateBytes(specs[i].Payload);
                    compressed[i] = true;
                }
                else
                {
                    payloads[i] = specs[i].Payload;
                    compressed[i] = false;
                }
            }

            // Build names block (concatenated null-terminated ASCII strings).
            var namesMs = new MemoryStream();
            int[] nameOffsets = new int[recordCount];
            for (int i = 0; i < recordCount; i++)
            {
                nameOffsets[i] = (int)namesMs.Position;
                byte[] nameBytes = Encoding.ASCII.GetBytes(specs[i].Name + "\0");
                namesMs.Write(nameBytes, 0, nameBytes.Length);
            }
            byte[] namesBlock = namesMs.ToArray();
            int namesSize = namesBlock.Length;

            // Layout: header(36) + info(infoBlockSize) + names(namesSize) + payloads...
            int infoOffset   = headerSize;
            int namesOffset  = infoOffset + infoBlockSize;
            int dataBase     = namesOffset + namesSize;

            int[] dataOffsets = new int[recordCount];
            int currentOffset = dataBase;
            for (int i = 0; i < recordCount; i++)
            {
                dataOffsets[i] = currentOffset;
                currentOffset += payloads[i].Length;
            }

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
            {
                // Magic + version
                WriteAscii4(bw, "EERT");
                WriteAscii4(bw, version);

                // Header fields
                bw.Write(recordCount);
                bw.Write(infoOffset);
                bw.Write(0);          // InfoCompression = none
                bw.Write(infoBlockSize);
                bw.Write(0);          // NameCompression = none
                bw.Write(namesSize);
                bw.Write(namesSize);  // NameSize = namesSize (uncompressed)

                // Info block
                for (int i = 0; i < recordCount; i++)
                {
                    int uncompressedSize = specs[i].Payload.Length;
                    int compressedSize   = payloads[i].Length;
                    int dataCompression  = compressed[i] ? 1 : 0;
                    WriteRecordInfo(bw,
                        dataSize:            uncompressedSize,
                        dataOffset:          dataOffsets[i],
                        dataCompression:     dataCompression,
                        dataCompressedSize:  compressedSize,
                        checksum:            0,
                        nameOffset:          nameOffsets[i]);
                }

                // Names block
                ms.Write(namesBlock, 0, namesBlock.Length);

                // Payloads
                for (int i = 0; i < recordCount; i++)
                {
                    ms.Write(payloads[i], 0, payloads[i].Length);
                }

                return ms.ToArray();
            }
        }

        private static void WriteAscii4(BinaryWriter bw, string s)
        {
            byte[] b = Encoding.ASCII.GetBytes(s);
            for (int i = 0; i < 4; i++)
            {
                bw.Write(i < b.Length ? b[i] : (byte)0);
            }
        }

        private static void WriteRecordInfo(BinaryWriter bw, int dataSize, int dataOffset,
            int dataCompression, int dataCompressedSize, int checksum, int nameOffset)
        {
            bw.Write(dataSize);
            bw.Write(dataOffset);
            bw.Write(dataCompression);
            bw.Write(dataCompressedSize);
            bw.Write(checksum);
            bw.Write(nameOffset);
        }

        /// <summary>
        /// Deflate-compresses <paramref name="input"/> using raw deflate
        /// (no zlib 2-byte header — SWG convention).
        /// </summary>
        public static byte[] DeflateBytes(byte[] input)
        {
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionMode.Compress, leaveOpen: true))
                {
                    deflate.Write(input, 0, input.Length);
                }
                return output.ToArray();
            }
        }
    }
}
