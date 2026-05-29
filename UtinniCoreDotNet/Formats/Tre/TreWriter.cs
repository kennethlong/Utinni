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
    /// Phase 8 Plan 07 — full-rebuild <c>.tre</c> archive writer (D-05.4). Produces a fresh
    /// archive whose records preserve the source's TOC invariants byte-for-byte for every
    /// UNTOUCHED entry, and recompresses ONLY the entries the caller marks as edited.
    ///
    /// <para><b>Two-guarantee contract (08-REVIEWS HIGH-1 — supersedes the misleading
    /// "full-file archive bytes identical" claim):</b></para>
    /// <list type="bullet">
    ///   <item><b>(a) Logical payload identity</b> — for every untouched record,
    ///         <see cref="TreFile.GetRecordData"/> on the rebuilt archive equals the same
    ///         method on the original.</item>
    ///   <item><b>(b) Raw compressed slice identity</b> — for every untouched record, the
    ///         byte range at <c>[newRec.Offset, newRec.Offset + newRec.CompressedSize)</c>
    ///         in the rebuilt archive equals
    ///         <c>[oldRec.Offset, oldRec.Offset + oldRec.CompressedSize)</c> in the original.
    ///         Achieved by copying via <see cref="TreFile.GetRecordCompressedBytes"/>
    ///         — NEVER decompressing/recompressing untouched entries.</item>
    /// </list>
    ///
    /// <para><b>Name-block byte-layout preservation (round-2 MEDIUM 6):</b> for every
    /// untouched record, <see cref="TreFile.GetRecordNameBytes"/> returns the same bytes on
    /// the rebuilt archive as on the original. The writer copies each untouched record's
    /// raw name bytes via <see cref="TreFile.GetRecordNameBytes"/> and lays them out in
    /// the new name block in the same record order — so the per-record <c>NameOffset</c>
    /// preserved in the rebuilt TOC points at a byte-identical name slice.</para>
    ///
    /// <para><b>Edited entry semantics:</b> the new payload bytes are recompressed as raw
    /// <c>deflate</c> (compressor = 1) as the V1 default. The edited entry's
    /// <see cref="TreRecord.Checksum"/> is PRESERVED from the original (path CRC, Open
    /// Q1/A1 — the path is unchanged, so the CRC is unchanged). The edited entry's
    /// <see cref="TreRecord.Name"/> is unchanged (path unchanged) and reuses the original
    /// raw name bytes.</para>
    ///
    /// <para><b>TOC + name block compression on output:</b> the rebuilt archive emits both
    /// the TOC and the name block UNCOMPRESSED (block-compression = 0). <see cref="TreFile"/>
    /// handles compressor 0/1/2 uniformly so the rebuilt archive parses correctly regardless
    /// of the original's TOC/name-block compression mode. This is a deliberate simplification
    /// — the per-record invariants (the user-facing acceptance contract) are
    /// compression-mode-independent.</para>
    ///
    /// <para><b>Layout:</b> <c>[36-byte header][payload region][TOC][name block]</c>. All
    /// little-endian (TRE is LE; do not cross with IFF's BE). Header fields and TOC offsets
    /// are written LAST once the block sizes are known.</para>
    ///
    /// <para><b>T-04-DoS:</b> the only inflate path TreWriter goes through is in
    /// <see cref="TreFile.GetRecordData"/> (NOT used here — we copy raw) and in the edited
    /// entry's recompress side, which uses
    /// <see cref="DeflateStream"/> with no upstream inflate. The original archive's
    /// 256 MB <c>MaxBlockSize</c> cap remains in force for any read TreFile performs to
    /// supply the raw slices we copy.</para>
    /// </summary>
    public static class TreWriter
    {
        // V1 default for the edited entry: raw deflate (compressor 1). Documented in the
        // plan's <behavior> block. The TreFile reader handles 0/1/2; the writer-side choice
        // here is the V1 default that BCL DeflateStream produces without RFC1950 framing.
        private const int EditedEntryCompressor = 1;

        // Fixed TRE header size (mirrors TreFile.HeaderSize).
        private const int HeaderSize = 36;

        /// <summary>
        /// Repacks a <c>.tre</c> archive by full rebuild. Untouched entries are copied
        /// byte-for-byte; only entries whose index appears in <paramref name="edits"/> are
        /// recompressed.
        /// </summary>
        /// <param name="original">A <see cref="TreFile"/> opened via
        /// <see cref="TreFile.Open(string)"/> (the path-backed lazy-read instance — required so
        /// we can pull raw compressed slices and raw name bytes via
        /// <see cref="TreFile.GetRecordCompressedBytes"/> /
        /// <see cref="TreFile.GetRecordNameBytes"/>).</param>
        /// <param name="edits">Map of record-index → new payload bytes. Indexes not in the
        /// map are untouched; their TOC + payload + name bytes are preserved exactly.</param>
        /// <returns>The full bytes of the rebuilt archive, ready to be written to disk
        /// atomically (the save target wraps this in a temp-file + <c>File.Replace</c>
        /// bracket).</returns>
        public static byte[] Repack(TreFile original, IDictionary<int, byte[]> edits)
        {
            if (original == null) throw new ArgumentNullException("original");
            if (edits == null) edits = new Dictionary<int, byte[]>();

            int n = original.Records.Count;
            TreVersion version = original.Header.Version;
            int recordStride = TreVersions.RecordStride(version);
            bool crcFirst = TreVersions.IsCrcFirst(version);

            // ── Build payload region + collect per-record stored-byte slices ─────
            // For each record decide whether to copy raw or recompress.
            //   - Untouched: rawCompressed = original.GetRecordCompressedBytes(i); preserve
            //     Compressor + UncompressedSize + CompressedSize + Checksum + Name.
            //   - Edited: newPayload = edits[i]; raw deflate; Compressor = 1; CompressedSize
            //     = compressedLen; UncompressedSize = newPayload.Length; Checksum preserved
            //     from original (path unchanged); Name preserved (path unchanged).
            var storedSlices = new byte[n][];
            var nameSlices = new byte[n][];
            var perRecordCompressor = new int[n];
            var perRecordUncompressedSize = new int[n];
            var perRecordCompressedSize = new int[n];
            var perRecordChecksum = new int[n];

            for (int i = 0; i < n; i++)
            {
                var rec = original.Records[i];
                byte[] newPayload;
                if (edits.TryGetValue(i, out newPayload))
                {
                    if (newPayload == null) newPayload = new byte[0];
                    byte[] recompressed = RawDeflate(newPayload);
                    storedSlices[i] = recompressed;
                    perRecordCompressor[i] = EditedEntryCompressor;
                    perRecordUncompressedSize[i] = newPayload.Length;
                    perRecordCompressedSize[i] = recompressed.Length;
                    // Path unchanged → CRC unchanged (Open Q1/A1).
                    perRecordChecksum[i] = rec.Checksum;
                }
                else
                {
                    // Untouched: copy raw compressed bytes verbatim (08-REVIEWS HIGH-1).
                    byte[] rawCompressed = original.GetRecordCompressedBytes(i);
                    storedSlices[i] = rawCompressed;
                    perRecordCompressor[i] = rec.Compressor;
                    perRecordUncompressedSize[i] = rec.UncompressedSize;
                    perRecordCompressedSize[i] = rec.CompressedSize;
                    perRecordChecksum[i] = rec.Checksum;
                }
                // Name bytes are always copied verbatim from the original (round-2 MEDIUM 6).
                // Edited entries: path is unchanged → name bytes unchanged. Untouched entries:
                // by definition the name bytes are preserved.
                nameSlices[i] = original.GetRecordNameBytes(i);
            }

            // ── Assemble the name block + record per-record NameOffset ───────────
            // Lay out names in record order; record each entry's offset into the new block so
            // the rebuilt TOC can point at it. The bytes themselves are byte-identical to the
            // original at the corresponding source offset (round-2 MEDIUM 6 — the offsets in
            // the rebuilt TOC are recomputed but point at the SAME name slice bytes).
            var newNameOffsets = new int[n];
            int cumName = 0;
            using (var nameMs = new MemoryStream())
            {
                for (int i = 0; i < n; i++)
                {
                    newNameOffsets[i] = cumName;
                    nameMs.Write(nameSlices[i], 0, nameSlices[i].Length);
                    cumName += nameSlices[i].Length;
                }
                byte[] nameBlock = nameMs.ToArray();

                // ── Build payload region; record each entry's payload offset ─────
                // Layout: [header (36)][payload region][TOC (n*stride)][name block]
                int payloadStart = HeaderSize;
                var payloadOffsets = new int[n];
                int payloadCursor = payloadStart;
                using (var payloadMs = new MemoryStream())
                {
                    for (int i = 0; i < n; i++)
                    {
                        payloadOffsets[i] = payloadCursor;
                        payloadMs.Write(storedSlices[i], 0, storedSlices[i].Length);
                        payloadCursor += storedSlices[i].Length;
                    }
                    byte[] payloadRegion = payloadMs.ToArray();

                    // ── Build TOC ───────────────────────────────────────────────
                    int tocSize = checked(n * recordStride);
                    int infoOffset = payloadStart + payloadRegion.Length;
                    byte[] tocBytes;
                    using (var tocMs = new MemoryStream(tocSize))
                    {
                        for (int i = 0; i < n; i++)
                        {
                            if (crcFirst)
                            {
                                // V5000 / V6000 — crc-first 24- or 32-byte stride.
                                WriteI32(tocMs, perRecordChecksum[i]);              // crc
                                WriteI32(tocMs, perRecordUncompressedSize[i]);       // length
                                WriteI32(tocMs, payloadOffsets[i]);                  // offset
                                WriteI32(tocMs, perRecordCompressor[i]);             // compressor
                                WriteI32(tocMs, perRecordCompressedSize[i]);         // compressedLength
                                WriteI32(tocMs, newNameOffsets[i]);                  // fileNameOffset
                                if (recordStride == 32)
                                {
                                    // V6000 8-byte pad.
                                    WriteI32(tocMs, 0);
                                    WriteI32(tocMs, 0);
                                }
                            }
                            else
                            {
                                // V0004 / V0005 / V0006 — size-first 24-byte stride.
                                WriteI32(tocMs, perRecordUncompressedSize[i]);       // uncompressedSize
                                WriteI32(tocMs, payloadOffsets[i]);                  // offset
                                WriteI32(tocMs, perRecordCompressor[i]);             // compression
                                WriteI32(tocMs, perRecordCompressedSize[i]);         // compressedSize
                                WriteI32(tocMs, perRecordChecksum[i]);               // checksum
                                WriteI32(tocMs, newNameOffsets[i]);                  // nameOffset
                            }
                        }
                        tocBytes = tocMs.ToArray();
                    }

                    // ── Assemble the full archive ───────────────────────────────
                    // Header (36) + payloadRegion + tocBytes + nameBlock.
                    int infoCompressedSize = tocBytes.Length;
                    int nameCompressedSize = nameBlock.Length;
                    int nameSize = nameBlock.Length; // uncompressed name-block bytes

                    using (var outMs = new MemoryStream(
                        HeaderSize + payloadRegion.Length + tocBytes.Length + nameBlock.Length))
                    {
                        // Magic + version.
                        outMs.Write(Ascii("EERT"), 0, 4);
                        outMs.Write(Ascii(original.Header.VersionTag), 0, 4);

                        // Header u32 fields.
                        WriteI32(outMs, n);                       // recordCount
                        WriteI32(outMs, infoOffset);              // infoOffset
                        WriteI32(outMs, 0);                       // infoCompression (uncompressed)
                        WriteI32(outMs, infoCompressedSize);      // infoCompressedSize
                        WriteI32(outMs, 0);                       // nameCompression (uncompressed)
                        WriteI32(outMs, nameCompressedSize);      // nameCompressedSize
                        WriteI32(outMs, nameSize);                // nameSize (uncompressed)

                        // Payload region.
                        outMs.Write(payloadRegion, 0, payloadRegion.Length);
                        // TOC.
                        outMs.Write(tocBytes, 0, tocBytes.Length);
                        // Name block (uncompressed).
                        outMs.Write(nameBlock, 0, nameBlock.Length);

                        return outMs.ToArray();
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // raw deflate (compressor 1 V1 default for the edited entry)
        // ─────────────────────────────────────────────────────────────────────

        private static byte[] RawDeflate(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionMode.Compress, leaveOpen: true))
                {
                    if (data.Length > 0)
                    {
                        ds.Write(data, 0, data.Length);
                    }
                }
                return ms.ToArray();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // primitives
        // ─────────────────────────────────────────────────────────────────────

        private static byte[] Ascii(string s)
        {
            return Encoding.ASCII.GetBytes(s ?? string.Empty);
        }

        private static void WriteI32(Stream s, int v)
        {
            s.Write(BitConverter.GetBytes(v), 0, 4);
        }
    }
}
