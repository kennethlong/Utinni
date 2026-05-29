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

namespace UtinniCoreDotNet.Formats.Tre
{
    /// <summary>
    /// Immutable metadata for one record in a TRE file. Each record's on-disk struct is 24 bytes.
    /// Field names match the JSON contract locked in Plan 04-02 (REVIEWS HIGH-7).
    /// </summary>
    public sealed class TreRecord
    {
        /// <summary>
        /// Uncompressed payload size in bytes.
        /// Named <c>uncompressedSize</c> in the JSON contract (REVIEWS HIGH-7 — NOT dataSize).
        /// </summary>
        public int UncompressedSize { get; internal set; }

        /// <summary>
        /// File offset where the (possibly compressed) record payload begins.
        /// Named <c>offset</c> in the JSON contract (REVIEWS HIGH-7 — NOT dataOffset).
        /// </summary>
        public int Offset { get; internal set; }

        /// <summary>
        /// Compression kind as a human-readable string: "none" (DataCompression==0) or
        /// "deflate" (DataCompression==1).
        /// Named <c>compressionKind</c> in the JSON contract (REVIEWS HIGH-7 — NOT an int).
        /// </summary>
        public string CompressionKind { get; internal set; }

        /// <summary>
        /// Compressed payload size on disk.
        /// Named <c>compressedSize</c> in the JSON contract (REVIEWS HIGH-7 — NOT dataCompressedSize).
        /// </summary>
        public int CompressedSize { get; internal set; }

        /// <summary>Raw CRC/checksum field from the record info struct.</summary>
        public int Checksum { get; internal set; }

        /// <summary>
        /// Phase 7 (07-01): raw compressor value from the info struct (0=none, 1=raw-deflate,
        /// 2=zlib). Internal — drives the on-demand inflate path in GetRecordData; not exposed in
        /// the JSON contract (which carries the derived <see cref="CompressionKind"/> string).
        /// </summary>
        internal int Compressor { get; set; }

        /// <summary>
        /// Byte offset into the names block where this record's null-terminated
        /// filename begins. Used internally during parsing; not exposed in JSON.
        /// </summary>
        public int NameOffset { get; internal set; }

        /// <summary>Null-terminated filename resolved from the names block.</summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Phase 8 (08-07): byte length of this record's name AS STORED IN THE NAME BLOCK,
        /// including the null terminator if the format wrote one. Equals the number of bytes
        /// <see cref="TreFile.Parse"/> consumed from the uncompressed names block starting at
        /// <see cref="NameOffset"/>. <see cref="TreFile.GetRecordNameBytes"/> uses this to
        /// return a verbatim slice of the name-block bytes so <see cref="TreFile"/>'s
        /// TreWriter sibling can preserve the name-block byte layout for untouched entries
        /// (round-2 MEDIUM 6). Internal — derived during parse, not exposed in JSON.
        /// </summary>
        internal int NameByteLength { get; set; }
    }
}
