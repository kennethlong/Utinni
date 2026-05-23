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
    /// <para><b>Eager-read contract (REVIEWS HIGH-4 fix path A):</b>
    /// <see cref="Open(Stream)"/> reads ALL record payload bytes into memory before returning,
    /// so the source stream may be disposed immediately after. Memory cost: O(total compressed
    /// size). For files &gt;100 MB consider a future IDisposable refactor
    /// (see Plan 04-02 §pitfalls Pitfall B). REVIEWS HIGH-4 path A.</para>
    ///
    /// <para><b>Security caps (T-04-V5 + T-04-DoS):</b>
    /// Every header field is validated to be non-negative and within
    /// <see cref="MaxBlockSize"/> (256 MB). Deflate output is capped at the same limit.</para>
    ///
    /// <para><b>Supported versions:</b> 0005, 0006. Others throw
    /// <see cref="TreParseException"/>(<see cref="TreParseError.UnsupportedVersion"/>).</para>
    /// </summary>
    public sealed class TreFile
    {
        // T-04-V5 + T-04-DoS: absolute cap per block/record (256 MB).
        private const int MaxBlockSize = 256 * 1024 * 1024;

        // Size of one TreInfo struct on disk.
        private const int RecordInfoSize = 24;

        // REVIEWS HIGH-4 fix path A: compressed bytes cached at Open time, indexed by ordinal.
        // Callers MUST NOT mutate the returned arrays.
        private readonly byte[][] RecordCompressedBytes;

        /// <summary>Parsed TRE header.</summary>
        public TreHeader Header { get; }

        /// <summary>Ordered list of record metadata (index == ordinal in info table).</summary>
        public IReadOnlyList<TreRecord> Records { get; }

        private TreFile(TreHeader header, IReadOnlyList<TreRecord> records, byte[][] recordCompressedBytes)
        {
            Header = header;
            Records = records;
            RecordCompressedBytes = recordCompressedBytes;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public factory methods
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens a TRE file from <paramref name="path"/> and eagerly reads all payloads.
        /// Throws <see cref="FileNotFoundException"/> if the path does not exist (the CLI
        /// surfaces this as exit code 3 with <c>error.kind = "FileNotFound"</c>).
        /// </summary>
        public static TreFile Open(string path)
        {
            // The FileStream's using scope extends across Open(Stream) — which completes
            // all eager reads before returning — so the stream is alive for the full read.
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Open(fs);
            }
        }

        /// <summary>
        /// Opens a TRE container from <paramref name="stream"/> and eagerly reads all record
        /// payload bytes. After this method returns, the caller may dispose the stream freely —
        /// no further reads are performed on it.
        /// </summary>
        public static TreFile Open(Stream stream)
        {
            // Phase 4 runs on Windows x86; document the endianness assumption.
            if (!BitConverter.IsLittleEndian)
            {
                throw new NotSupportedException("TreFile requires a little-endian host (Windows x86 only in Phase 4).");
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

                string version = Encoding.ASCII.GetString(versionBytes);
                if (version != "0005" && version != "0006")
                {
                    throw new TreParseException(TreParseError.UnsupportedVersion,
                        "Unsupported TRE version '" + version + "'. Phase 4 supports 0005 and 0006 only.");
                }

                // ── Header fields (28 bytes at offset 8) ────────────────────
                if (streamLength < 36)
                {
                    throw new TreParseException(TreParseError.Truncated, "Stream too short to contain full TRE header.");
                }

                int recordCount    = br.ReadInt32();
                int infoOffset     = br.ReadInt32();
                int infoCompression     = br.ReadInt32();
                int infoCompressedSize  = br.ReadInt32();
                int nameCompression     = br.ReadInt32();
                int nameCompressedSize  = br.ReadInt32();
                int nameSize            = br.ReadInt32();

                // Validate all length fields (T-04-V5 bounds check).
                ValidateLength(recordCount,        streamLength, TreParseError.NegativeLength,         TreParseError.ChunkLengthExceedsCap, "recordCount");
                ValidateLength(infoOffset,         streamLength, TreParseError.NegativeLength,         TreParseError.ChunkLengthExceedsCap, "infoOffset");
                ValidateLength(infoCompressedSize, streamLength, TreParseError.NegativeLength,         TreParseError.ChunkLengthExceedsCap, "infoCompressedSize");
                ValidateLength(nameCompressedSize, streamLength, TreParseError.NegativeLength,         TreParseError.ChunkLengthExceedsCap, "nameCompressedSize");
                ValidateLength(nameSize,           MaxBlockSize, TreParseError.NegativeLength,         TreParseError.ChunkLengthExceedsCap, "nameSize");

                // CR-02: infoOffset + infoCompressedSize must fit within the stream.
                // The previous form was `infoOffset > 0 && infoEnd > streamLength`, which
                // silently bypassed the bounds check whenever infoOffset == 0 — letting a
                // malformed TRE with a non-zero infoCompressedSize claim that the info
                // block starts at offset 0 (overlapping the magic/version header). Drop
                // the leading guard and let the end-of-block check stand on its own,
                // mirroring the analogous namesEnd > streamLength check below.
                long infoEnd = (long)infoOffset + infoCompressedSize;
                if (infoEnd > streamLength)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Info block at offset " + infoOffset + " with size " + infoCompressedSize + " exceeds stream length " + streamLength + ".");
                }

                // Build TreHeader.
                var header = new TreHeader
                {
                    Version            = version,
                    RecordCount        = recordCount,
                    InfoOffset         = infoOffset,
                    InfoCompression    = infoCompression,
                    InfoCompressedSize = infoCompressedSize,
                    NameCompression    = nameCompression,
                    NameCompressedSize = nameCompressedSize,
                    NameSize           = nameSize
                };

                // ── Info block (record metadata) ────────────────────────────
                // Located at InfoOffset; size = InfoCompressedSize (may be deflate-compressed).
                // The uncompressed info block is recordCount * 24 bytes.
                int expectedInfoUncompressedSize = recordCount * RecordInfoSize;
                ValidateLength(expectedInfoUncompressedSize, MaxBlockSize, TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "infoTableTotalSize");

                byte[] infoBytes = ReadBlock(br, stream, infoOffset, infoCompressedSize, infoCompression,
                    expectedInfoUncompressedSize, streamLength, "info block");

                if (infoBytes.Length < expectedInfoUncompressedSize)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Info block is " + infoBytes.Length + " bytes but need " + expectedInfoUncompressedSize + " for " + recordCount + " records.");
                }

                // ── Names block ─────────────────────────────────────────────
                // Located immediately after the info block end in the file
                // (InfoOffset + InfoCompressedSize).
                int namesOffset = infoOffset + infoCompressedSize;
                ValidateLength(namesOffset, streamLength, TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "namesBlockOffset");

                long namesEnd = (long)namesOffset + nameCompressedSize;
                if (namesEnd > streamLength)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Names block at offset " + namesOffset + " with compressed size " + nameCompressedSize + " exceeds stream length " + streamLength + ".");
                }

                byte[] namesBytes = ReadBlock(br, stream, namesOffset, nameCompressedSize, nameCompression,
                    nameSize, streamLength, "names block");

                // ── Parse per-record info structs ────────────────────────────
                var records = new List<TreRecord>(recordCount);
                using (var infoBr = new BinaryReader(new MemoryStream(infoBytes), Encoding.ASCII, leaveOpen: false))
                {
                    for (int i = 0; i < recordCount; i++)
                    {
                        int dataSize            = infoBr.ReadInt32();   // uncompressed
                        int dataOffset          = infoBr.ReadInt32();
                        int dataCompression     = infoBr.ReadInt32();
                        int dataCompressedSize  = infoBr.ReadInt32();
                        int checksum            = infoBr.ReadInt32();
                        int nameOffset          = infoBr.ReadInt32();

                        // dataSize is the declared uncompressed size — only check it's non-negative.
                        // The 256MB cap is enforced during inflation in GetRecordData (T-04-DoS).
                        if (dataSize < 0)
                        {
                            throw new TreParseException(TreParseError.NegativeLength,
                                "Field 'record[" + i + "].dataSize' is negative (" + dataSize + "); TRE file may be corrupt.");
                        }
                        ValidateLength(dataOffset,         streamLength,  TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "record[" + i + "].dataOffset");
                        ValidateLength(dataCompressedSize, streamLength,  TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "record[" + i + "].dataCompressedSize");
                        ValidateLength(nameOffset,         namesBytes.Length + 1, TreParseError.NegativeLength, TreParseError.ChunkLengthExceedsCap, "record[" + i + "].nameOffset");

                        string compressionKind = (dataCompression == 0) ? "none" : "deflate";

                        // Resolve name from names block (null-terminated ASCII).
                        string name = ReadNullTerminatedAscii(namesBytes, nameOffset);

                        records.Add(new TreRecord
                        {
                            UncompressedSize = dataSize,
                            Offset           = dataOffset,
                            CompressionKind  = compressionKind,
                            CompressedSize   = dataCompressedSize,
                            Checksum         = checksum,
                            NameOffset       = nameOffset,
                            Name             = name
                        });
                    }
                }

                // ── REVIEWS HIGH-4: Eager-read all compressed payloads ───────
                // Cache the on-disk compressed bytes for each record. After this loop,
                // the caller may dispose the source stream — GetRecordData reads only
                // from RecordCompressedBytes[].
                var compressedCache = new byte[recordCount][];
                for (int i = 0; i < recordCount; i++)
                {
                    var rec = records[i];
                    if (rec.CompressedSize == 0)
                    {
                        compressedCache[i] = new byte[0];
                        continue;
                    }

                    long payloadEnd = (long)rec.Offset + rec.CompressedSize;
                    if (payloadEnd > streamLength)
                    {
                        throw new TreParseException(TreParseError.Truncated,
                            "Record " + i + " payload at offset " + rec.Offset + " with compressed size " + rec.CompressedSize + " exceeds stream length " + streamLength + ".");
                    }

                    stream.Seek(rec.Offset, SeekOrigin.Begin);
                    var payload = new byte[rec.CompressedSize];
                    int read = stream.Read(payload, 0, rec.CompressedSize);
                    if (read != rec.CompressedSize)
                    {
                        throw new TreParseException(TreParseError.Truncated,
                            "Record " + i + " payload read returned " + read + " bytes; expected " + rec.CompressedSize + ".");
                    }
                    compressedCache[i] = payload;
                }

                return new TreFile(header, records.AsReadOnly(), compressedCache);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public data accessor
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the decompressed payload bytes for record at <paramref name="index"/>.
        /// <para><b>REVIEWS HIGH-4:</b> This method does NOT access the original stream —
        /// all reads come from the cached compressed bytes populated during
        /// <see cref="Open(Stream)"/>. The original stream may be disposed before this call.</para>
        /// <para><b>T-04-DoS:</b> Deflate output is capped at min(256 MB, record.UncompressedSize).
        /// If deflate produces more bytes than declared, <see cref="TreParseException"/>
        /// (<see cref="TreParseError.DeflateExpansionExceedsCap"/>) is thrown.</para>
        /// <para><b>Mutation warning:</b> Callers MUST NOT mutate the returned byte[].
        /// For uncompressed records, the cached byte[] is returned directly.</para>
        /// </summary>
        public byte[] GetRecordData(int index)
        {
            if (index < 0 || index >= Records.Count)
            {
                throw new ArgumentOutOfRangeException("index", "Record index " + index + " is out of range [0, " + Records.Count + ").");
            }

            var rec = Records[index];
            byte[] compressed = RecordCompressedBytes[index];

            if (rec.CompressionKind == "none")
            {
                // Return the cached bytes directly (no copy per the xmldoc contract).
                return compressed;
            }

            // T-04-DoS: reject records claiming more bytes than the 256 MB cap.
            if (rec.UncompressedSize > MaxBlockSize)
            {
                throw new TreParseException(TreParseError.DeflateExpansionExceedsCap,
                    "Record " + index + " declares uncompressed size " + rec.UncompressedSize + " which exceeds the 256 MB cap (" + MaxBlockSize + " bytes). T-04-DoS.");
            }

            // Deflate path — cap at min(MaxBlockSize, rec.UncompressedSize).
            int cap = Math.Min(MaxBlockSize, rec.UncompressedSize);

            using (var compressedStream = new MemoryStream(compressed))
            using (var deflate = new DeflateStream(compressedStream, CompressionMode.Decompress))
            {
                var output = new MemoryStream(cap);
                int totalRead = 0;
                var buffer = new byte[65536];

                while (true)
                {
                    int remaining = cap - totalRead;
                    if (remaining <= 0)
                    {
                        // Try reading one more byte to detect over-expansion.
                        int extra = deflate.ReadByte();
                        if (extra >= 0)
                        {
                            throw new TreParseException(TreParseError.DeflateExpansionExceedsCap,
                                "Record " + index + " deflate expansion exceeded cap of " + cap + " bytes.");
                        }
                        break;
                    }

                    int toRead = Math.Min(buffer.Length, remaining);
                    int n = deflate.Read(buffer, 0, toRead);
                    if (n == 0)
                    {
                        break;
                    }
                    output.Write(buffer, 0, n);
                    totalRead += n;
                }

                byte[] result = output.ToArray();

                // Tamper check: declared vs actual inflate length.
                if (result.Length != rec.UncompressedSize)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "Record " + index + " deflated to " + result.Length + " bytes but header declared " + rec.UncompressedSize + ".");
                }

                return result;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads a (possibly deflate-compressed) block from the stream at the given offset.
        /// Returns the uncompressed bytes (or raw bytes if compression == 0).
        /// </summary>
        private static byte[] ReadBlock(BinaryReader br, Stream stream, int offset, int compressedSize, int compression,
            int uncompressedSize, long streamLength, string blockName)
        {
            if (compressedSize == 0)
            {
                return new byte[0];
            }

            stream.Seek(offset, SeekOrigin.Begin);
            byte[] rawBytes = new byte[compressedSize];
            int bytesRead = stream.Read(rawBytes, 0, compressedSize);
            if (bytesRead != compressedSize)
            {
                throw new TreParseException(TreParseError.Truncated,
                    "Read of " + blockName + " at offset " + offset + " returned " + bytesRead + " bytes; expected " + compressedSize + ".");
            }

            if (compression == 0)
            {
                return rawBytes;
            }

            // Deflate-compressed block.
            int cap = Math.Min(MaxBlockSize, uncompressedSize > 0 ? uncompressedSize : MaxBlockSize);
            using (var compressedStream = new MemoryStream(rawBytes))
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
                            throw new TreParseException(TreParseError.ChunkLengthExceedsCap,
                                blockName + " deflate expansion exceeded cap of " + cap + " bytes.");
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

        /// <summary>
        /// Reads a null-terminated ASCII string from <paramref name="namesBlock"/> at the given offset.
        /// </summary>
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
        /// <paramref name="maxBound"/>. Throws <see cref="TreParseException"/> on violation.
        /// Single source of truth for T-04-V5 bounds checking.
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
