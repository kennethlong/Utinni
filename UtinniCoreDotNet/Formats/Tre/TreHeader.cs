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
    /// Immutable header parsed from a TRE file. All integer fields are the raw little-endian
    /// values from the on-disk header block (bytes 8–35).
    /// Field names match the JSON contract locked in Plan 04-02 (REVIEWS HIGH-7).
    /// </summary>
    public sealed class TreHeader
    {
        /// <summary>
        /// Raw 4-char version tag exactly as it appears on disk, e.g., "0004", "0005",
        /// "0006", "5000", "6000". This is the value emitted as the locked JSON
        /// <c>version</c> field (Plan 04-02 contract — unchanged in Phase 7).
        /// </summary>
        public string VersionTag { get; internal set; }

        /// <summary>
        /// Phase 7 (07-01): the dispatched version enum (D-06). Distinct from
        /// <see cref="VersionTag"/> (the raw on-disk string) so callers can switch on the
        /// lineage without string compares.
        /// </summary>
        public TreVersion Version { get; internal set; }

        /// <summary>
        /// Phase 7 (07-01): true when this archive's PAYLOADS are not directly decodable —
        /// V5000 (no verified layout, D-06b) and V6000 (encrypted/obfuscated payloads, D-07).
        /// Enumeration (TOC/names/CRC) still works; only content reads degrade to enumerate-only.
        /// </summary>
        public bool EnumerateOnly { get; internal set; }

        /// <summary>
        /// Number of resource records in this archive.
        /// Named <c>recordCount</c> in the JSON contract (REVIEWS HIGH-7 — NOT resourceCount).
        /// </summary>
        public int RecordCount { get; internal set; }

        /// <summary>File offset to the info (record metadata) block.</summary>
        public int InfoOffset { get; internal set; }

        /// <summary>Compression flag for the info block (0=none, 1=deflate).</summary>
        public int InfoCompression { get; internal set; }

        /// <summary>Compressed byte size of the info block on disk.</summary>
        public int InfoCompressedSize { get; internal set; }

        /// <summary>Compression flag for the names block (0=none, 1=deflate).</summary>
        public int NameCompression { get; internal set; }

        /// <summary>Compressed byte size of the names block on disk.</summary>
        public int NameCompressedSize { get; internal set; }

        /// <summary>Uncompressed byte size of the names block.</summary>
        public int NameSize { get; internal set; }
    }
}
