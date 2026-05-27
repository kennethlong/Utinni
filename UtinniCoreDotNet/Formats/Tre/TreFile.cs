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
// Format understood by reading swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}
// (SOE/Bootprint, All Rights Reserved). No code, comments, identifier names, or test fixtures copied
// from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace UtinniCoreDotNet.Formats.Tre
{
    /// <summary>
    /// Pure-C# read-only TRE container reader.
    ///
    /// <para><b>Lazy TOC-only enumeration (Phase 7 / 07-01):</b>
    /// <see cref="Open(string)"/> and <see cref="Open(Stream)"/> read only the TOC (record
    /// metadata) + names blocks — they do NOT eager-read record payloads (archives carry
    /// 100k+ entries). A single payload is read on demand by <see cref="GetRecordData(int)"/>,
    /// which re-opens the source file by stored path. A <see cref="Open(Stream)"/> instance has
    /// no stored path and therefore cannot lazily read payloads — <see cref="GetRecordData(int)"/>
    /// throws <see cref="InvalidOperationException"/> on it (open via <see cref="Open(string)"/>
    /// for payload access). The test-only <see cref="PayloadReadCount"/> counter proves metadata
    /// enumeration performs zero payload reads.</para>
    ///
    /// <para><b>Version dispatch (D-06):</b> SWGEmu Pre-CU 0004/0005/0006 (size-first 24-byte
    /// record stride) and Restoration 6000 (crc-first 32-byte stride, zlib-framed TOC/name
    /// blocks). 5000 is recognized but has no verified layout (D-06b): it is enumerate-empty and
    /// is never routed through the 6000 stride. 6000 payloads are encrypted (enumerate-only, D-07)
    /// — the TOC/name blocks still inflate so names enumerate; only payload content degrades.</para>
    ///
    /// <para><b>Security caps (T-04-V5 + T-04-DoS):</b> every count/offset/size is bounds-checked
    /// with division/subtraction-form guards (which cannot overflow) before any allocation;
    /// inflate output is capped at <see cref="MaxBlockSize"/> (256 MB).</para>
    /// </summary>
    public sealed class TreFile
    {
        // T-04-V5 + T-04-DoS: absolute cap per block/record (256 MB).
        private const int MaxBlockSize = 256 * 1024 * 1024;

        // Fixed TRE header size (magic + version + 7 u32 fields).
        private const int HeaderSize = 36;

        // Set by Open(string); null for Open(Stream). Drives lazy payload reads.
        private readonly string _sourcePath;

        /// <summary>Parsed TRE header.</summary>
        public TreHeader Header { get; }

        /// <summary>Ordered list of record metadata (index == ordinal in info table).</summary>
        public IReadOnlyList<TreRecord> Records { get; }

        /// <summary>
        /// Phase 7 (07-01) TEST SEAM (internal, NOT public shipping surface — exposed via
        /// InternalsVisibleTo("Utinni.Cli.Tests")). Counts the number of on-demand payload reads
        /// performed by <see cref="GetRecordData(int)"/>. Proves lazy TOC-only enumeration does
        /// zero payload reads (parse-tre leaves this at 0).
        /// </summary>
        internal int PayloadReadCount { get; private set; }

        private TreFile(TreHeader header, IReadOnlyList<TreRecord> records, string sourcePath)
        {
            Header = header;
            Records = records;
            _sourcePath = sourcePath;
            PayloadReadCount = 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public factory methods
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens a TRE file from <paramref name="path"/> and enumerates its TOC + names lazily
        /// (no payload reads). The path is stored so <see cref="GetRecordData(int)"/> can read a
        /// single payload on demand by re-opening the file. Throws
        /// <see cref="FileNotFoundException"/> if the path does not exist (the CLI surfaces this
        /// as exit code 3 with <c>error.kind = "FileNotFound"</c>).
        /// </summary>
        public static TreFile Open(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Parse(fs, path);
            }
        }

        /// <summary>
        /// Opens a TRE container from <paramref name="stream"/> and enumerates its TOC + names.
        /// <para><b>Lazy contract:</b> a stream-backed instance has no stored source path, so it
        /// cannot perform on-demand payload reads — <see cref="GetRecordData(int)"/> throws
        /// <see cref="InvalidOperationException"/>. Use <see cref="Open(string)"/> for payload
        /// access. (Stream-backed enumeration is still fully supported for metadata.)</para>
        /// </summary>
        public static TreFile Open(Stream stream)
        {
            return Parse(stream, null);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Core parse (TOC + names only — never eager-reads payloads)
        // ─────────────────────────────────────────────────────────────────────

        private static TreFile Parse(Stream stream, string sourcePath)
        {
            // Phase 4 runs on Windows x86; the LE header reads (BinaryReader.ReadInt32) require it.
            if (!BitConverter.IsLittleEndian)
            {
                throw new NotSupportedException("TreFile requires a little-endian host (Windows x86).");
            }

            long streamLength = stream.Length;

            using (var br = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true))
            {
                // ── Magic (4 bytes at offset 0) ─────────────────────────────
                byte[] magicBytes = br.ReadBytes(4);
                if (magicBytes.Length < 4 ||
                    magicBytes[0] != 'E' || magicBytes[1] != 'E' ||
                    magicBytes[2] != 'R' || magicBytes[3] != 'T')
                {
                    throw new TreParseException(TreParseError.BadMagic,
                        "TRE magic bytes not found. Expected 'EERT' at offset 0.");
                }

                // ── Version (4 bytes at offset 4) ────────────────────────────
                byte[] versionBytes = br.ReadBytes(4);
                if (versionBytes.Length < 4)
                {
                    throw new TreParseException(TreParseError.Truncated, "Stream too short to contain version field.");
                }

                string versionTag = Encoding.ASCII.GetString(versionBytes);
                // Version-tag dispatch replaces the old 0005/0006 hard-fail gate (TreVersions.Parse
                // throws UnsupportedVersion for any tag outside the supported lineage set).
                TreVersion version = TreVersions.Parse(versionTag);

                // ── Header fields (28 bytes at offset 8) ────────────────────
                if (streamLength < HeaderSize)
                {
                    throw new TreParseException(TreParseError.Truncated, "Stream too short to contain full TRE header.");
                }

                int recordCount        = br.ReadInt32();
                int infoOffset         = br.ReadInt32();
                int infoCompression    = br.ReadInt32();
                int infoCompressedSize = br.ReadInt32();
                int nameCompression    = br.ReadInt32();
                int nameCompressedSize = br.ReadInt32();
                int nameSize           = br.ReadInt32();

                var header = new TreHeader
                {
                    VersionTag         = versionTag,
                    Version            = version,
                    EnumerateOnly      = TreVersions.IsEnumerateOnly(version),
                    RecordCount        = recordCount,
                    InfoOffset         = infoOffset,
                    InfoCompression    = infoCompression,
                    InfoCompressedSize = infoCompressedSize,
                    NameCompression    = nameCompression,
                    NameCompressedSize = nameCompressedSize,
                    NameSize           = nameSize
                };

                // ── 5000: recognized tag, NO verified layout (D-06b, review consensus #1) ──
                // Enumerate-empty: never parse the record region, never assert a layout, never
                // route through the V6000 crc-first stride. A non-6000-layout 5000 file MUST
                // enumerate empty and never throw.
                // TODO(5000-fixture): no record layout — enumerate-empty until a real 5000
                // fixture/spec exists, then implement its true record decode here.
                if (version == TreVersion.V5000)
                {
                    return new TreFile(header, new List<TreRecord>(0).AsReadOnly(), sourcePath);
                }

                int recordStride = TreVersions.RecordStride(version);

                // ── Checked arithmetic guards (review consensus #4 — division/subtraction form) ──
                if (recordCount < 0)
                {
                    throw new TreParseException(TreParseError.NegativeLength,
                        "Field 'recordCount' is negative (" + recordCount + "); TRE file may be corrupt.");
                }
                // Division form (cannot overflow, never computes recordCount * recordStride): the
                // UNCOMPRESSED record table must fit within the 256 MB inflate cap. This rejects an
                // absurd recordCount (e.g. the count*stride-overflow fixture, 100M) before any
                // allocation. We bound against MaxBlockSize rather than (streamLength - header)
                // because a zlib-compressed TOC (V6000) is far smaller on disk than its
                // recordCount * stride uncompressed size — a streamLength-relative bound would
                // falsely reject every real V6000 archive. The "does the inflated table actually
                // contain enough bytes" check remains the post-read infoBytes.Length guard below.
                if (recordCount > 0 && recordCount > MaxBlockSize / recordStride)
                {
                    throw new TreParseException(TreParseError.ChunkLengthExceedsCap,
                        "Field 'recordCount' value " + recordCount + " * stride " + recordStride
                        + " exceeds the 256 MB uncompressed-record-table cap.");
                }

                ValidateLength(infoOffset,         streamLength, TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "infoOffset");
                ValidateLength(infoCompressedSize, streamLength, TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "infoCompressedSize");
                ValidateLength(nameCompressedSize, streamLength, TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "nameCompressedSize");
                ValidateLength(nameSize,           MaxBlockSize, TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "nameSize");

                // CR-02 (Phase 4): info block end must fit within the stream (subtraction form).
                long infoEnd = (long)infoOffset + infoCompressedSize;
                if (infoEnd > streamLength)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Info block at offset " + infoOffset + " with size " + infoCompressedSize + " exceeds stream length " + streamLength + ".");
                }

                // ── Info/TOC block (record metadata) ────────────────────────
                int expectedInfoUncompressedSize = recordCount * recordStride;
                ValidateLength(expectedInfoUncompressedSize, MaxBlockSize, TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "infoTableTotalSize");

                byte[] infoBytes = ReadBlock(stream, infoOffset, infoCompressedSize, infoCompression,
                    expectedInfoUncompressedSize, "info block");

                if (infoBytes.Length < expectedInfoUncompressedSize)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Info block is " + infoBytes.Length + " bytes but need " + expectedInfoUncompressedSize + " for " + recordCount + " records.");
                }

                // ── Names block (immediately after the info block) ──────────
                int namesOffset = infoOffset + infoCompressedSize;
                ValidateLength(namesOffset, streamLength, TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "namesBlockOffset");

                long namesEnd = (long)namesOffset + nameCompressedSize;
                if (namesEnd > streamLength)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Names block at offset " + namesOffset + " with compressed size " + nameCompressedSize + " exceeds stream length " + streamLength + ".");
                }

                byte[] namesBytes = ReadBlock(stream, namesOffset, nameCompressedSize, nameCompression,
                    nameSize, "names block");

                // ── Parse per-record info structs (version-dispatched field order) ──
                var records = new List<TreRecord>(recordCount);
                bool crcFirst = TreVersions.IsCrcFirst(version);
                using (var infoBr = new BinaryReader(new MemoryStream(infoBytes), Encoding.ASCII, leaveOpen: false))
                {
                    for (int i = 0; i < recordCount; i++)
                    {
                        int uncompressedSize;
                        int offset;
                        int compressor;
                        int compressedSize;
                        int checksum;
                        int nameOffset;

                        if (crcFirst)
                        {
                            // V6000 crc-first 32-byte stride: crc, length, offset, compressor,
                            // compressedLength, fileNameOffset, + 8 pad.
                            checksum         = infoBr.ReadInt32();   // crc
                            uncompressedSize = infoBr.ReadInt32();   // length
                            offset           = infoBr.ReadInt32();
                            compressor       = infoBr.ReadInt32();
                            compressedSize   = infoBr.ReadInt32();   // compressedLength
                            nameOffset       = infoBr.ReadInt32();   // fileNameOffset
                            infoBr.ReadInt32(); infoBr.ReadInt32();  // 8 bytes pad
                        }
                        else
                        {
                            // V0004/V0005/V0006 size-first 24-byte stride (UNCHANGED from Phase 4 —
                            // the existing CLI goldens exercise this exact field order; Open Q1).
                            uncompressedSize = infoBr.ReadInt32();   // dataSize
                            offset           = infoBr.ReadInt32();   // dataOffset
                            compressor       = infoBr.ReadInt32();   // dataCompression
                            compressedSize   = infoBr.ReadInt32();   // dataCompressedSize
                            checksum         = infoBr.ReadInt32();
                            nameOffset       = infoBr.ReadInt32();
                        }

                        if (uncompressedSize < 0)
                        {
                            throw new TreParseException(TreParseError.NegativeLength,
                                "Field 'record[" + i + "].uncompressedSize' is negative (" + uncompressedSize + "); TRE file may be corrupt.");
                        }
                        if (offset < 0)
                        {
                            throw new TreParseException(TreParseError.NegativeLength,
                                "Field 'record[" + i + "].offset' is negative (" + offset + "); TRE file may be corrupt.");
                        }
                        if (compressedSize < 0)
                        {
                            throw new TreParseException(TreParseError.NegativeLength,
                                "Field 'record[" + i + "].compressedSize' is negative (" + compressedSize + "); TRE file may be corrupt.");
                        }
                        // Subtraction form (never offset + length): offset must leave room for compressedSize.
                        if ((long)offset > streamLength - compressedSize)
                        {
                            throw new TreParseException(TreParseError.Truncated,
                                "Record[" + i + "] payload at offset " + offset + " with compressed size " + compressedSize
                                + " exceeds stream length " + streamLength + ".");
                        }
                        ValidateLength(nameOffset, namesBytes.Length + 1, TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "record[" + i + "].nameOffset");

                        // Strict compressor mapping (0=none, 1=deflate, 2=zlib) — an unrecognized
                        // value raises UnknownCompressor. The legacy fixtures only use 0/1, so
                        // their "none"/"deflate" kinds are unchanged.
                        string compressionKind = CompressorKind(compressor, i);

                        string name = ReadNullTerminatedAscii(namesBytes, nameOffset);

                        records.Add(new TreRecord
                        {
                            UncompressedSize = uncompressedSize,
                            Offset           = offset,
                            CompressionKind  = compressionKind,
                            Compressor       = compressor,
                            CompressedSize   = compressedSize,
                            Checksum         = checksum,
                            NameOffset       = nameOffset,
                            Name             = name
                        });
                    }
                }

                return new TreFile(header, records.AsReadOnly(), sourcePath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public data accessor (lazy, on-demand)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the decompressed payload bytes for the record at <paramref name="index"/>,
        /// reading only that record's bytes on demand by re-opening the source file.
        /// <para><b>Lazy contract (07-01):</b> a stream-backed instance (opened via
        /// <see cref="Open(Stream)"/>) has no stored path and throws
        /// <see cref="InvalidOperationException"/> — open via <see cref="Open(string)"/> for
        /// payload access.</para>
        /// <para><b>T-04-DoS:</b> inflate output is capped at min(256 MB, record.UncompressedSize);
        /// over-expansion throws <see cref="TreParseError.DeflateExpansionExceedsCap"/>.</para>
        /// <para><b>WR-01:</b> every call returns a fresh byte[] callers may mutate freely.</para>
        /// </summary>
        public byte[] GetRecordData(int index)
        {
            if (index < 0 || index >= Records.Count)
            {
                throw new ArgumentOutOfRangeException("index", "Record index " + index + " is out of range [0, " + Records.Count + ").");
            }

            if (_sourcePath == null)
            {
                throw new InvalidOperationException(
                    "This TreFile was opened from a Stream and has no stored source path, so it cannot "
                    + "perform lazy on-demand payload reads. Open the archive via TreFile.Open(string path) "
                    + "for payload access (enumeration via Open(Stream) is metadata-only).");
            }

            var rec = Records[index];

            if (rec.CompressedSize == 0)
            {
                PayloadReadCount++;
                return new byte[0];
            }

            byte[] compressed = new byte[rec.CompressedSize];
            using (var fs = new FileStream(_sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Seek(rec.Offset, SeekOrigin.Begin);
                int read = ReadFully(fs, compressed, rec.CompressedSize);
                if (read != rec.CompressedSize)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Record " + index + " payload read returned " + read + " bytes; expected " + rec.CompressedSize + ".");
                }
            }
            PayloadReadCount++;

            if (rec.Compressor == 0)
            {
                // WR-01: fresh copy so the caller can mutate freely.
                var copy = new byte[compressed.Length];
                Buffer.BlockCopy(compressed, 0, copy, 0, compressed.Length);
                return copy;
            }

            // T-04-DoS: reject records claiming more bytes than the 256 MB cap.
            if (rec.UncompressedSize > MaxBlockSize)
            {
                throw new TreParseException(TreParseError.DeflateExpansionExceedsCap,
                    "Record " + index + " declares uncompressed size " + rec.UncompressedSize + " which exceeds the 256 MB cap (" + MaxBlockSize + " bytes). T-04-DoS.");
            }

            int cap = Math.Min(MaxBlockSize, rec.UncompressedSize);
            byte[] result = Inflate(compressed, rec.Compressor, cap, TreParseError.DeflateExpansionExceedsCap, "record " + index);

            // Tamper check: declared vs actual inflate length.
            if (result.Length != rec.UncompressedSize)
            {
                throw new TreParseException(TreParseError.Truncated,
                    "Record " + index + " deflated to " + result.Length + " bytes but header declared " + rec.UncompressedSize + ".");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>0=none, 1=raw-deflate, 2=zlib. Any other value raises UnknownCompressor.</summary>
        private static string CompressorKind(int compressor, int recordIndex)
        {
            switch (compressor)
            {
                case 0: return "none";
                case 1: return "deflate";
                case 2: return "zlib";
                default:
                    throw new TreParseException(TreParseError.UnknownCompressor,
                        "Record[" + recordIndex + "] declares unknown compressor " + compressor
                        + " (expected 0=none, 1=deflate, 2=zlib).");
            }
        }

        /// <summary>
        /// Reads a (possibly compressed) block from the stream at the given offset and returns its
        /// uncompressed bytes. Zlib-aware (Pitfall 2): compression 0=none (raw), 1=raw deflate,
        /// 2=zlib RFC1950 (0x78 0x9c header validated via %31, body fed minus the 4-byte Adler
        /// trailer). Any other compression value raises UnknownCompressor.
        /// </summary>
        private static byte[] ReadBlock(Stream stream, int offset, int compressedSize, int compression,
            int uncompressedSize, string blockName)
        {
            if (compressedSize == 0)
            {
                return new byte[0];
            }

            stream.Seek(offset, SeekOrigin.Begin);
            byte[] rawBytes = new byte[compressedSize];
            int bytesRead = ReadFully(stream, rawBytes, compressedSize);
            if (bytesRead != compressedSize)
            {
                throw new TreParseException(TreParseError.Truncated,
                    "Read of " + blockName + " at offset " + offset + " returned " + bytesRead + " bytes; expected " + compressedSize + ".");
            }

            if (compression == 0)
            {
                return rawBytes;
            }

            int cap = Math.Min(MaxBlockSize, uncompressedSize > 0 ? uncompressedSize : MaxBlockSize);
            return Inflate(rawBytes, compression, cap, TreParseError.ChunkLengthExceedsCap, blockName);
        }

        /// <summary>
        /// Inflates <paramref name="raw"/> according to <paramref name="compression"/>
        /// (1=raw deflate, 2=zlib RFC1950), capping output at <paramref name="cap"/>. For zlib,
        /// validates the 2-byte header (%31==0) and strips header + 4-byte Adler trailer before
        /// feeding the body to DeflateStream. A truncated/garbled frame is detected on the inflate
        /// side (short read / DeflateStream error) and raised as InvalidZlibTrailer/Truncated —
        /// NOT via an Adler32 comparison (the BCL DeflateStream does not validate Adler32).
        /// </summary>
        private static byte[] Inflate(byte[] raw, int compression, int cap, TreParseError overExpansionKind, string blockName)
        {
            byte[] body;
            if (compression == 2)
            {
                // zlib RFC1950 framing.
                if (raw.Length < 6)
                {
                    throw new TreParseException(TreParseError.InvalidZlibTrailer,
                        blockName + " declared zlib (compressor 2) but is only " + raw.Length + " bytes — too short for a 2-byte header + 4-byte Adler trailer.");
                }
                int b0 = raw[0];
                int b1 = raw[1];
                if (b0 != 0x78 || (((b0 << 8) | b1) % 31 != 0))
                {
                    throw new TreParseException(TreParseError.InvalidZlibTrailer,
                        blockName + " declared zlib (compressor 2) but the 2-byte header 0x"
                        + b0.ToString("X2") + b1.ToString("X2") + " is not a valid RFC1950 frame (%31 != 0).");
                }
                body = new byte[raw.Length - 2 - 4]; // strip 2-byte header, drop 4-byte Adler trailer
                Buffer.BlockCopy(raw, 2, body, 0, body.Length);
            }
            else if (compression == 1)
            {
                body = raw; // raw deflate (existing SWGEmu/synthesized fixtures)
            }
            else
            {
                throw new TreParseException(TreParseError.UnknownCompressor,
                    blockName + " declares unknown block compressor " + compression + " (expected 0=none, 1=deflate, 2=zlib).");
            }

            try
            {
                using (var compressedStream = new MemoryStream(body))
                using (var deflate = new DeflateStream(compressedStream, CompressionMode.Decompress))
                {
                    var output = new MemoryStream(cap);
                    int totalRead = 0;
                    byte[] buffer = new byte[65536];

                    while (true)
                    {
                        int remaining = cap - totalRead;
                        if (remaining <= 0)
                        {
                            int extra = deflate.ReadByte();
                            if (extra >= 0)
                            {
                                throw new TreParseException(overExpansionKind,
                                    blockName + " inflate expansion exceeded cap of " + cap + " bytes.");
                            }
                            break;
                        }
                        int toRead = Math.Min(buffer.Length, remaining);
                        int n = deflate.Read(buffer, 0, toRead);
                        if (n == 0) break;
                        output.Write(buffer, 0, n);
                        totalRead += n;
                    }

                    return output.ToArray();
                }
            }
            catch (TreParseException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Inflate-side failure (truncated/garbled body). For a zlib-declared block this is
                // the invalid-frame detection (review Codex item 11); for raw deflate it is Truncated.
                TreParseError kind = compression == 2 ? TreParseError.InvalidZlibTrailer : TreParseError.Truncated;
                throw new TreParseException(kind,
                    blockName + " inflate failed (" + ex.GetType().Name + ": " + ex.Message + ").");
            }
        }

        /// <summary>Reads exactly <paramref name="count"/> bytes (or fewer at EOF). Returns the count read.</summary>
        private static int ReadFully(Stream stream, byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = stream.Read(buffer, total, count - total);
                if (n == 0) break;
                total += n;
            }
            return total;
        }

        /// <summary>Reads a null-terminated ASCII string from <paramref name="namesBlock"/> at the given offset.</summary>
        private static string ReadNullTerminatedAscii(byte[] namesBlock, int offset)
        {
            if (offset < 0 || offset >= namesBlock.Length)
            {
                return string.Empty;
            }

            int end = offset;
            while (end < namesBlock.Length && namesBlock[end] != 0)
            {
                end++;
            }

            return Encoding.ASCII.GetString(namesBlock, offset, end - offset);
        }

        /// <summary>
        /// Validates that <paramref name="claimed"/> is non-negative and does not exceed
        /// <paramref name="maxBound"/>. Single source of truth for T-04-V5 bounds checking.
        /// </summary>
        private static void ValidateLength(long claimed, long maxBound, TreParseError kindOnNegative, TreParseError kindOnOverflow, string fieldName)
        {
            if (claimed < 0)
            {
                throw new TreParseException(kindOnNegative,
                    "Field '" + fieldName + "' is negative (" + claimed + "); TRE file may be corrupt.");
            }
            if (claimed > maxBound)
            {
                throw new TreParseException(kindOnOverflow,
                    "Field '" + fieldName + "' value " + claimed + " exceeds maximum allowed " + maxBound + ".");
            }
        }
    }
}
