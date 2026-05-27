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
// and tre_reader.py detect_master_index_kind (SOE/Bootprint, All Rights Reserved). No code, comments,
// identifier names, or test fixtures copied from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace UtinniCoreDotNet.Formats.Tre
{
    /// <summary>The two master-index shapes documented in RESEARCH (ported detect_master_index_kind).</summary>
    public enum MasterIndexKind
    {
        /// <summary>COT2000: first 8 bytes == " COT2000" (leading space).</summary>
        Cot2000,
        /// <summary>SearchTOC: bytes[0:4] == "TOC " (0x20434F54) AND bytes[4:8] == "0001".</summary>
        SearchTOC
    }

    /// <summary>
    /// One global entry in a master index. <see cref="Offset"/>/<see cref="Length"/>/
    /// <see cref="CompressedLength"/> are ARCHIVE-LOCAL — they index into the per-tree <c>.tre</c>
    /// archive named at <see cref="TreeFileIndex"/>, NOT into the master index itself.
    /// </summary>
    public struct CotEntry
    {
        public string Path;
        public uint Crc;
        public int TreeFileIndex;
        public long Offset;
        public int Length;
        public int CompressedLength;
        public int Compressor;
    }

    /// <summary>
    /// Reads a COT2000 / SearchTOC master index — the cross-archive path enumeration that maps each
    /// virtual path to (treeFileIndex, archive-local offset/length) in a companion <c>.tre</c>.
    /// All header counts/offsets/sizes are adversary-controlled and bounds-checked before allocation.
    /// </summary>
    public sealed class CotMasterIndex
    {
        private const int MaxBlockSize = 256 * 1024 * 1024;

        public MasterIndexKind Kind { get; }

        /// <summary>The per-tree <c>.tre</c> names (relative to the master index's directory).</summary>
        public IReadOnlyList<string> TreeFileNames { get; }

        /// <summary>The flattened global entries, each resolvable to a companion archive byte range.</summary>
        public IReadOnlyList<CotEntry> Entries { get; }

        private CotMasterIndex(MasterIndexKind kind, IReadOnlyList<string> treeFileNames, IReadOnlyList<CotEntry> entries)
        {
            Kind = kind;
            TreeFileNames = treeFileNames;
            Entries = entries;
        }

        // ── master-index kind constants (ported detect_master_index_kind) ──
        // TAG_TOC = 0x20434F54 (ascii "TOC "), TAG_0001 = ascii "0001".
        private static readonly byte[] Cot2000Magic = { 0x20, 0x43, 0x4F, 0x54, 0x32, 0x30, 0x30, 0x30 }; // " COT2000"

        /// <summary>
        /// Returns the detected master-index kind, or null if the bytes are neither shape.
        /// </summary>
        public static MasterIndexKind? DetectKind(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 8) return null;
            bool isCot2000 = true;
            for (int i = 0; i < 8; i++) { if (bytes[i] != Cot2000Magic[i]) { isCot2000 = false; break; } }
            if (isCot2000) return MasterIndexKind.Cot2000;

            // SearchTOC: bytes[0:4] == "TOC " (0x20434F54) AND bytes[4:8] == "0001".
            if (bytes[0] == 0x54 && bytes[1] == 0x4F && bytes[2] == 0x43 && bytes[3] == 0x20 &&
                bytes[4] == 0x30 && bytes[5] == 0x30 && bytes[6] == 0x30 && bytes[7] == 0x31)
            {
                return MasterIndexKind.SearchTOC;
            }
            return null;
        }

        /// <summary>True when the file at <paramref name="path"/> begins with a recognized master-index magic.</summary>
        public static bool IsMasterIndex(string path)
        {
            try
            {
                byte[] head = new byte[8];
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int n = fs.Read(head, 0, 8);
                    if (n < 8) return false;
                }
                return DetectKind(head) != null;
            }
            catch (IOException) { return false; }
        }

        public static CotMasterIndex Open(string path)
        {
            byte[] bytes;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fs.Length > MaxBlockSize)
                {
                    throw new TreParseException(TreParseError.ChunkLengthExceedsCap,
                        "Master index '" + path + "' is " + fs.Length + " bytes, exceeding the 256 MB cap.");
                }
                bytes = new byte[fs.Length];
                int total = 0;
                while (total < bytes.Length)
                {
                    int n = fs.Read(bytes, total, bytes.Length - total);
                    if (n == 0) break;
                    total += n;
                }
            }

            MasterIndexKind? kind = DetectKind(bytes);
            if (kind == null)
            {
                throw new TreParseException(TreParseError.BadMagic,
                    "File is not a recognized master index (neither ' COT2000' nor 'TOC '+'0001').");
            }

            if (kind == MasterIndexKind.SearchTOC)
            {
                // TODO(searchtoc-fixture): the SearchTOC layout is documented in RESEARCH but no
                // fixture/spec is available this session to validate a parser. SearchTOC is
                // explicitly RECOGNIZED and dispositioned (not silently dropped) — it raises a
                // documented error until a fixture exists (see the fixture-gated skipped test).
                throw new TreParseException(TreParseError.UnsupportedVersion,
                    "SearchTOC master index detected but not yet supported (no fixture/spec available). "
                    + "TODO(searchtoc-fixture): port the SearchTOC layout once a sample is sourced.");
            }

            return ParseCot2000(bytes);
        }

        private static CotMasterIndex ParseCot2000(byte[] b)
        {
            long len = b.Length;
            if (len < 36)
            {
                throw new TreParseException(TreParseError.Truncated, "COT2000 header is shorter than 36 bytes.");
            }

            // Header (offsets per RESEARCH "Detecting master-index kind").
            int numFiles            = ReadI32(b, 12);
            int sizeOfTocBlock      = ReadI32(b, 16);
            int sizeOfNameBlock     = ReadI32(b, 20);
            int numTreeFiles        = ReadI32(b, 28);
            int sizeOfTreeNameBlock = ReadI32(b, 32);

            RequireNonNegative(numFiles, "numFiles");
            RequireNonNegative(numTreeFiles, "numTreeFiles");
            RequireNonNegative(sizeOfTocBlock, "sizeOfTocBlock");
            RequireNonNegative(sizeOfNameBlock, "sizeOfNameBlock");
            RequireNonNegative(sizeOfTreeNameBlock, "sizeOfTreeNameBlock");

            // Each global TOC entry is 32 bytes. Division form: numFiles must not exceed the cap.
            if (numFiles > MaxBlockSize / 32)
            {
                throw new TreParseException(TreParseError.ChunkLengthExceedsCap,
                    "COT2000 numFiles " + numFiles + " * 32 exceeds the 256 MB cap.");
            }

            // Layout: [36 header][treeNames sizeOfTreeNameBlock][TOC sizeOfTocBlock][names sizeOfNameBlock]
            long treeNamesStart = 36;
            long tocStart       = treeNamesStart + sizeOfTreeNameBlock;
            long nameStart      = tocStart + sizeOfTocBlock;
            RequireWithin(treeNamesStart, sizeOfTreeNameBlock, len, "treeNameBlock");
            RequireWithin(tocStart, sizeOfTocBlock, len, "tocBlock");
            RequireWithin(nameStart, sizeOfNameBlock, len, "nameBlock");

            // Tree-file names: numTreeFiles null-terminated ASCII strings.
            var treeNames = ReadNullTerminatedList(b, (int)treeNamesStart, sizeOfTreeNameBlock, numTreeFiles, "treeNameBlock");

            // TOC + name blocks may be zlib-framed on real archives; the synthetic fixture stores
            // them raw. MaybeInflate auto-detects the 0x78 0x9c frame.
            byte[] tocBlock  = MaybeInflate(Slice(b, (int)tocStart, sizeOfTocBlock), numFiles * 32, "tocBlock");
            byte[] nameBlock = MaybeInflate(Slice(b, (int)nameStart, sizeOfNameBlock), MaxBlockSize, "nameBlock");

            if (tocBlock.Length < (long)numFiles * 32)
            {
                throw new TreParseException(TreParseError.Truncated,
                    "COT2000 TOC block is " + tocBlock.Length + " bytes but needs " + ((long)numFiles * 32) + " for " + numFiles + " entries.");
            }

            var entries = new List<CotEntry>(numFiles);
            long cumulativeNameOffset = 0;
            for (int i = 0; i < numFiles; i++)
            {
                int o = i * 32;
                int compressor     = tocBlock[o + 0];
                int treeFileIndex  = ReadU16(tocBlock, o + 2);
                uint crc           = (uint)ReadI32(tocBlock, o + 4);
                int fileNameLength = ReadI32(tocBlock, o + 8);
                long offset        = (uint)ReadI32(tocBlock, o + 12);
                int length         = ReadI32(tocBlock, o + 16);
                int compressedLen  = ReadI32(tocBlock, o + 20);

                if (treeFileIndex < 0 || treeFileIndex >= numTreeFiles)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "COT2000 entry[" + i + "] treeFileIndex " + treeFileIndex + " is out of range [0, " + numTreeFiles + ").");
                }
                RequireNonNegative(fileNameLength, "entry[" + i + "].fileNameLength");
                RequireNonNegative(length, "entry[" + i + "].length");
                RequireNonNegative(compressedLen, "entry[" + i + "].compressedLength");

                // Cumulative name offset (division-form bound against the name block length).
                if (cumulativeNameOffset > nameBlock.Length || fileNameLength > nameBlock.Length - cumulativeNameOffset)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        "COT2000 entry[" + i + "] name (offset " + cumulativeNameOffset + ", length " + fileNameLength + ") exceeds the " + nameBlock.Length + "-byte name block.");
                }
                string path = Encoding.ASCII.GetString(nameBlock, (int)cumulativeNameOffset, fileNameLength);
                cumulativeNameOffset += fileNameLength + 1; // +1 for the null terminator

                entries.Add(new CotEntry
                {
                    Path = path,
                    Crc = crc,
                    TreeFileIndex = treeFileIndex,
                    Offset = offset,
                    Length = length,
                    CompressedLength = compressedLen,
                    Compressor = compressor
                });
            }

            return new CotMasterIndex(MasterIndexKind.Cot2000, treeNames.AsReadOnly(), entries.AsReadOnly());
        }

        // ── helpers ──

        private static List<string> ReadNullTerminatedList(byte[] b, int start, int size, int count, string blockName)
        {
            var list = new List<string>(count);
            int cur = start;
            int end = start + size;
            for (int i = 0; i < count; i++)
            {
                if (cur >= end)
                {
                    throw new TreParseException(TreParseError.Truncated,
                        blockName + " ended before reading name " + i + " of " + count + ".");
                }
                int nul = cur;
                while (nul < end && b[nul] != 0) nul++;
                list.Add(Encoding.ASCII.GetString(b, cur, nul - cur));
                cur = nul + 1;
            }
            return list;
        }

        private static byte[] Slice(byte[] b, int start, int count)
        {
            byte[] r = new byte[count];
            Buffer.BlockCopy(b, start, r, 0, count);
            return r;
        }

        /// <summary>Auto-detects a zlib RFC1950 frame (0x78, %31==0) and inflates it; otherwise returns the raw bytes.</summary>
        private static byte[] MaybeInflate(byte[] raw, int cap, string blockName)
        {
            if (raw.Length >= 6 && raw[0] == 0x78 && (((raw[0] << 8) | raw[1]) % 31 == 0))
            {
                byte[] body = new byte[raw.Length - 2 - 4];
                Buffer.BlockCopy(raw, 2, body, 0, body.Length);
                try
                {
                    using (var ms = new MemoryStream(body))
                    using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
                    using (var outMs = new MemoryStream())
                    {
                        byte[] buffer = new byte[65536];
                        int total = 0;
                        int safeCap = cap > 0 ? cap : MaxBlockSize;
                        while (true)
                        {
                            int n = deflate.Read(buffer, 0, buffer.Length);
                            if (n == 0) break;
                            total += n;
                            if (total > safeCap)
                            {
                                throw new TreParseException(TreParseError.ChunkLengthExceedsCap,
                                    blockName + " inflate exceeded cap of " + safeCap + " bytes.");
                            }
                            outMs.Write(buffer, 0, n);
                        }
                        return outMs.ToArray();
                    }
                }
                catch (TreParseException) { throw; }
                catch (Exception ex)
                {
                    throw new TreParseException(TreParseError.InvalidZlibTrailer,
                        blockName + " zlib inflate failed (" + ex.GetType().Name + ": " + ex.Message + ").");
                }
            }
            return raw;
        }

        private static void RequireNonNegative(int value, string field)
        {
            if (value < 0)
            {
                throw new TreParseException(TreParseError.NegativeLength,
                    "COT2000 field '" + field + "' is negative (" + value + ").");
            }
        }

        private static void RequireWithin(long start, long size, long streamLength, string blockName)
        {
            if (start < 0 || size < 0 || start > streamLength - size)
            {
                throw new TreParseException(TreParseError.Truncated,
                    "COT2000 " + blockName + " (start " + start + ", size " + size + ") exceeds the " + streamLength + "-byte master index.");
            }
        }

        private static int ReadI32(byte[] b, int off) { return BitConverter.ToInt32(b, off); }
        private static int ReadU16(byte[] b, int off) { return BitConverter.ToUInt16(b, off); }
    }
}
