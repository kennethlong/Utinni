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
// CLEF command-chunk payload codec. Field tables understood by reading swg-client-v2
// .../clientGame/.../ClientEffectTemplate.cpp (SOE/Bootprint, All Rights Reserved): chunk PAYLOAD
// scalars are LITTLE-endian (Pitfall 6) and strings are NUL-terminated C-strings (strlen+1 on disk,
// NEVER length-prefixed — Pitfall 2). Only the on-disk layout was studied — no code, comments,
// identifier names, or test fixtures copied from any reference source. Implementation original to
// Utinni under MIT.

using System;
using System.IO;
using System.Text;

namespace UtinniCoreDotNet.Formats.ClientEffect
{
    /// <summary>
    /// Variable-length LE-scalar + NUL-C-string encode/decode for the CLEF command-chunk payloads
    /// (the ONE genuinely new asset in the CLEF codec — unlike the fixed-span <c>TrnFieldEncoder</c>,
    /// which rejects length changes; that is the wrong tool for the variable-length string edit that
    /// is the whole point of the ClientEffect editor).
    ///
    /// <para>Encode helpers build a WHOLE command-chunk payload through a <see cref="MemoryStream"/>:
    /// a NUL-terminated C-string (ASCII bytes + 0x00, no length prefix) then LE scalars
    /// (<c>BitConverter.GetBytes</c> + <c>Array.Reverse</c> when <c>!BitConverter.IsLittleEndian</c>).
    /// The per-version CPAP encoder emits ONLY the fields the SOURCE version defines (D-03) and THROWS
    /// <see cref="ClientEffectParseException"/>(<see cref="ClientEffectParseError.VersionFieldMismatch"/>)
    /// when asked to emit a field above that version (REVIEWS HIGH #2). Decode mirrors via the shared
    /// <see cref="Decoders.IffPayloadCursor"/> (read side lives in <see cref="ClientEffectCommand"/>).</para>
    /// </summary>
    public static class ClefFieldCodec
    {
        // The on-disk string encoding for CLEF C-strings (8-bit, NUL-terminated, no length prefix).
        private static readonly Encoding StringEncoding = Encoding.ASCII;

        // ── LE / cstring primitive writers ──────────────────────────────────────

        /// <summary>Writes a NUL-terminated ASCII C-string (no length prefix — Pitfall 2).</summary>
        public static void WriteCString(Stream s, string value)
        {
            if (s == null) throw new ArgumentNullException("s");
            if (value == null) value = string.Empty;
            byte[] bytes = StringEncoding.GetBytes(value);
            s.Write(bytes, 0, bytes.Length);
            s.WriteByte(0x00); // NUL terminator
        }

        /// <summary>Writes a 32-bit IEEE-754 float little-endian (Pitfall 6).</summary>
        public static void WriteFloatLe(Stream s, float value)
        {
            if (s == null) throw new ArgumentNullException("s");
            byte[] four = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(four);
            s.Write(four, 0, four.Length);
        }

        /// <summary>Writes a 32-bit signed integer little-endian (Pitfall 6).</summary>
        public static void WriteInt32Le(Stream s, int value)
        {
            if (s == null) throw new ArgumentNullException("s");
            byte[] four = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(four);
            s.Write(four, 0, four.Length);
        }

        /// <summary>Writes a single byte (uint8 / bool8 — 1 byte each).</summary>
        public static void WriteUInt8(Stream s, byte value)
        {
            if (s == null) throw new ArgumentNullException("s");
            s.WriteByte(value);
        }

        // ── per-command payload encoders ────────────────────────────────────────

        /// <summary>
        /// Encodes a CPAP (CreateAppearance) payload for the given SOURCE version (the ONLY
        /// version-variant command). Emits ONLY the fields that version defines (D-03):
        /// <list type="bullet">
        ///   <item>v0001: appearanceTemplateName (cstring) + timeInSeconds (float)</item>
        ///   <item>v0002: + softParticleTerminate (bool8)</item>
        ///   <item>v0003: + minScale, maxScale, minPlaybackRate, maxPlaybackRate (4 floats)</item>
        /// </list>
        /// THROWS <see cref="ClientEffectParseException"/>(<see cref="ClientEffectParseError.VersionFieldMismatch"/>)
        /// when a caller supplies the v0002 field below v0002 or any v0003 field below v0003 — the
        /// D-03 encode-boundary guard (REVIEWS HIGH #2). A supplied field whose flag is set but the
        /// version permits it is emitted; a permitted-but-omitted higher field defaults to 0.
        /// </summary>
        public static byte[] EncodeCpap(string version, string appearanceTemplateName, float timeInSeconds,
            bool? softParticleTerminate,
            float? minScale, float? maxScale, float? minPlaybackRate, float? maxPlaybackRate)
        {
            int versionNum = ParseVersion(version);

            bool wantsV0002 = softParticleTerminate.HasValue;
            bool wantsV0003 = minScale.HasValue || maxScale.HasValue
                || minPlaybackRate.HasValue || maxPlaybackRate.HasValue;

            if (wantsV0002 && versionNum < 2)
            {
                throw new ClientEffectParseException(ClientEffectParseError.VersionFieldMismatch,
                    "CPAP field 'softParticleTerminate' is v0002+; the source version is " + version
                    + ". Refusing to upgrade the file version on save (D-03).");
            }
            if (wantsV0003 && versionNum < 3)
            {
                throw new ClientEffectParseException(ClientEffectParseError.VersionFieldMismatch,
                    "CPAP fields minScale/maxScale/minPlaybackRate/maxPlaybackRate are v0003-only; the "
                    + "source version is " + version
                    + ". Refusing to upgrade the file version on save (D-03).");
            }

            using (var ms = new MemoryStream())
            {
                WriteCString(ms, appearanceTemplateName);
                WriteFloatLe(ms, timeInSeconds);
                if (versionNum >= 2)
                {
                    WriteUInt8(ms, (byte)((softParticleTerminate ?? false) ? 1 : 0));
                }
                if (versionNum >= 3)
                {
                    WriteFloatLe(ms, minScale ?? 0f);
                    WriteFloatLe(ms, maxScale ?? 0f);
                    WriteFloatLe(ms, minPlaybackRate ?? 0f);
                    WriteFloatLe(ms, maxPlaybackRate ?? 0f);
                }
                return ms.ToArray();
            }
        }

        /// <summary>Encodes a PSND (PlaySound) payload: soundTemplateName (cstring). Stable across versions.</summary>
        public static byte[] EncodePsnd(string soundTemplateName)
        {
            using (var ms = new MemoryStream())
            {
                WriteCString(ms, soundTemplateName);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Encodes a CLGT (CreateLight) payload: 3 uint8 RGB + 5 LE floats = EXACTLY 23 bytes (the odd
        /// length is why the no-pad quirk matters; the writer emits no pad). Stable across versions.
        /// </summary>
        public static byte[] EncodeClgt(byte r, byte g, byte b, float timeInSeconds,
            float constantAttenuation, float linearAttenuation, float quadraticAttenuation, float range)
        {
            using (var ms = new MemoryStream())
            {
                WriteUInt8(ms, r);
                WriteUInt8(ms, g);
                WriteUInt8(ms, b);
                WriteFloatLe(ms, timeInSeconds);
                WriteFloatLe(ms, constantAttenuation);
                WriteFloatLe(ms, linearAttenuation);
                WriteFloatLe(ms, quadraticAttenuation);
                WriteFloatLe(ms, range);
                return ms.ToArray(); // 3 + 5*4 = 23 bytes
            }
        }

        /// <summary>Encodes a CAMS (CameraShake) payload: 4 LE floats = 16 bytes. Stable across versions.</summary>
        public static byte[] EncodeCams(float magnitudeInMeters, float frequencyInHz,
            float timeInSeconds, float falloffRadius)
        {
            using (var ms = new MemoryStream())
            {
                WriteFloatLe(ms, magnitudeInMeters);
                WriteFloatLe(ms, frequencyInHz);
                WriteFloatLe(ms, timeInSeconds);
                WriteFloatLe(ms, falloffRadius);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Encodes an FFBK (ForceFeedback) payload, in the exact SOE read order (string THEN scalars):
        /// forceFeedbackFile (cstring) + iterations (int32 LE) + range (float LE). Stable across versions.
        /// </summary>
        public static byte[] EncodeFfbk(string forceFeedbackFile, int iterations, float range)
        {
            using (var ms = new MemoryStream())
            {
                WriteCString(ms, forceFeedbackFile);
                WriteInt32Le(ms, iterations);
                WriteFloatLe(ms, range);
                return ms.ToArray();
            }
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        // Parses a 4-char numeric CLEF version tag ("0001"/"0002"/"0003") to its integer rank. A
        // non-numeric tag should never reach the encoder (the model raw-preserves unknown versions),
        // so this fails loud rather than silently defaulting.
        private static int ParseVersion(string version)
        {
            int n;
            if (version == null || !int.TryParse(version, out n))
            {
                throw new ClientEffectParseException(ClientEffectParseError.VersionFieldMismatch,
                    "CPAP encode requires a numeric CLEF version tag (e.g. \"0001\"); got '" + version + "'.");
            }
            return n;
        }
    }
}
