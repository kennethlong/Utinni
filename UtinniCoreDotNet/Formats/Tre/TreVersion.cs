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
    /// The TRE archive lineages Utinni reads (D-06). Two client families:
    /// SWGEmu Pre-CU (0004/0005/0006, size-first 24-byte record stride) and
    /// newer/Restoration (5000/6000, crc-first 32-byte record stride).
    /// </summary>
    public enum TreVersion
    {
        /// <summary>SWGEmu Pre-CU 0004. Recognized; read via the size-first path (real engine layout unverified — 07-VALIDATION Wave 0).</summary>
        V0004,
        /// <summary>SWGEmu Pre-CU 0005. Size-first 24-byte stride (fixture-validated).</summary>
        V0005,
        /// <summary>SWGEmu Pre-CU 0006. Size-first 24-byte stride (fixture-validated).</summary>
        V0006,
        /// <summary>SWGEmu Pre-CU 5000. Size-first 24-byte stride, zlib-compressed TOC/name blocks; READABLE — verified against the live SWGEmu client's 'EERT5000' .tre (corrects the planning assumption that 5000 was an unknown/encrypted layout).</summary>
        V5000,
        /// <summary>Restoration 6000. Crc-first 32-byte stride, zlib-framed TOC/name blocks; payloads encrypted (enumerate-only, D-07).</summary>
        V6000
    }

    /// <summary>
    /// Maps the raw 4-char on-disk version tag to a <see cref="TreVersion"/> and exposes the
    /// per-version dispatch facts (record stride, enumerate-only posture) the reader keys on.
    /// </summary>
    public static class TreVersions
    {
        /// <summary>
        /// Parses the 4-char version tag. Throws
        /// <see cref="TreParseException"/>(<see cref="TreParseError.UnsupportedVersion"/>)
        /// for any tag outside the supported set (e.g. "9999").
        /// </summary>
        public static TreVersion Parse(string tag)
        {
            switch (tag)
            {
                case "0004": return TreVersion.V0004;
                case "0005": return TreVersion.V0005;
                case "0006": return TreVersion.V0006;
                case "5000": return TreVersion.V5000;
                case "6000": return TreVersion.V6000;
                default:
                    throw new TreParseException(TreParseError.UnsupportedVersion,
                        "Unsupported TRE version '" + tag + "'. Supported: 0004, 0005, 0006, 5000, 6000.");
            }
        }

        /// <summary>
        /// True when the version's PAYLOADS are not directly decodable: V5000 (no verified
        /// layout, D-06b) and V6000 (encrypted/obfuscated, D-07). V0004/V0005/V0006 are readable.
        /// </summary>
        public static bool IsEnumerateOnly(TreVersion v)
        {
            // Only V6000 (Restoration, encrypted payloads) is enumerate-only. V5000 turned out to be
            // the readable SWGEmu Pre-CU client format (size-first header, zlib-compressed TOC/name
            // blocks) — verified against the live client's 53 'EERT5000' .tre. It is NOT an unknown/
            // encrypted layout (corrects the Phase-7 planning assumption D-06b).
            return v == TreVersion.V6000;
        }

        /// <summary>
        /// On-disk record info stride in bytes: 24 (size-first) for V0004/V0005/V0006/V5000,
        /// 32 (crc-first + 8 pad) for V6000.
        /// </summary>
        public static int RecordStride(TreVersion v)
        {
            return v == TreVersion.V6000 ? 32 : 24;
        }

        /// <summary>
        /// True for the crc-first record field order (crc, length, offset, compressor,
        /// compressedLength, fileNameOffset): V5000 (24-byte stride, no pad) and V6000 (32-byte
        /// stride, +8 pad). False for the size-first family (V0004/V0005/V0006).
        /// </summary>
        public static bool IsCrcFirst(TreVersion v)
        {
            return v == TreVersion.V6000 || v == TreVersion.V5000;
        }
    }
}
