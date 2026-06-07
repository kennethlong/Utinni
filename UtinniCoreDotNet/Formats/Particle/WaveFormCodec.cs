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
// WaveForm (FORM WVFM) layout understood by reading swg-client-v2
// .../sharedMath/.../WaveForm.cpp (SOE/Bootprint, All Rights Reserved). The WVFM form holds a single
// version-tagged chunk (0000/0001/0002); its little-endian payload is:
//   0000: int32 interpType, int32 count, count × (float percent, value, randomMax, randomMin)
//   0001: int32 interpType, int32 sampleType, int32 count, count × (float percent, value, randomMax, randomMin)
//   0002: int32 interpType, int32 sampleType, float valueMin, float valueMax, int32 count,
//         count × (float percent, value, randomMin, randomMax)
// Only the on-disk layout was studied — no code, comments, identifier names, or test fixtures copied
// from any reference source. Implementation original to Utinni under MIT. The load-time
// scaleAll(0.28f) weight normalization the reference applies on read is a READ-only transform and is
// deliberately NOT applied here so write-back is byte-exact (Pitfall 3).

using System;
using System.IO;
using UtinniCoreDotNet.Formats.Decoders;

namespace UtinniCoreDotNet.Formats.Particle
{
    /// <summary>
    /// Decodes / re-encodes a WaveForm version-chunk payload (the bytes inside the single
    /// <c>0000|0001|0002</c> chunk of a <c>FORM WVFM</c>) to and from a typed
    /// <see cref="ParticleWaveForm"/>.
    ///
    /// <para><b>Byte-exact + degrade-don't-abort (D-05):</b> a recognized version decodes to a typed
    /// waveform that <see cref="Encode"/> re-emits byte-for-byte; an UNRECOGNIZED version tag (or any
    /// payload that does not decode + re-encode to the identical bytes) is carried as a
    /// <see cref="ParticleFieldKind.RawBytesHexFallback"/> via <see cref="TryDecode"/> so it round-trips
    /// verbatim and NEVER throws.</para>
    /// </summary>
    public static class WaveFormCodec
    {
        // Per-control-point record = 4 little-endian floats (16 bytes) on every known version.
        internal const int ControlPointBytes = 16;
        // Absolute cap on a control-point count to reject a forged/oversized count before allocating
        // (T-15-02). A real waveform has a handful of points; 16M is comfortably above any legit asset
        // and far below an int overflow.
        internal const int MaxControlPoints = 16 * 1024 * 1024;

        /// <summary>
        /// Decodes a WVFM version-chunk payload into a typed <see cref="ParticleFieldValue"/>. A
        /// recognized version that decodes AND re-encodes to the identical bytes is returned as a typed
        /// <see cref="ParticleFieldKind.WaveForm"/>; anything else (unknown version, trailing bytes, a
        /// truncated record) is returned as a byte-exact raw-preserve. NEVER throws for an unrecognized
        /// version (D-05).
        /// </summary>
        /// <param name="versionTag">The 4-char chunk tag (e.g. "0002").</param>
        /// <param name="chunkPayload">The chunk's little-endian payload bytes.</param>
        public static ParticleFieldValue TryDecode(string versionTag, byte[] chunkPayload)
        {
            if (versionTag == null) throw new ArgumentNullException("versionTag");
            if (chunkPayload == null) throw new ArgumentNullException("chunkPayload");

            try
            {
                ParticleWaveForm typed = Decode(versionTag, chunkPayload);
                // Consume-exactly-or-hex: only accept the typed decode when it re-encodes to the exact
                // original bytes. This guards against any layout assumption being wrong on a real asset.
                byte[] reencoded = Encode(typed);
                if (BytesEqual(reencoded, chunkPayload))
                {
                    return ParticleFieldValue.FromWaveForm(typed);
                }
            }
            catch (DecoderException)
            {
                // fall through to raw-preserve
            }
            catch (ParticleParseException)
            {
                // fall through to raw-preserve
            }

            return ParticleFieldValue.FromRawBytes(chunkPayload);
        }

        /// <summary>
        /// Decodes a recognized WaveForm version-chunk payload into a typed <see cref="ParticleWaveForm"/>.
        /// Throws <see cref="DecoderException"/> on a truncated payload and
        /// <see cref="ParticleParseException"/> on an unrecognized version or a forged count.
        /// </summary>
        public static ParticleWaveForm Decode(string versionTag, byte[] chunkPayload)
        {
            if (versionTag == null) throw new ArgumentNullException("versionTag");
            if (chunkPayload == null) throw new ArgumentNullException("chunkPayload");

            var cursor = new IffPayloadCursor(chunkPayload);

            int interpolationType;
            int? sampleType = null;
            float? valueMin = null;
            float? valueMax = null;

            switch (versionTag)
            {
                case "0000":
                    interpolationType = cursor.ReadInt32Le();
                    break;
                case "0001":
                    interpolationType = cursor.ReadInt32Le();
                    sampleType = cursor.ReadInt32Le();
                    break;
                case "0002":
                    interpolationType = cursor.ReadInt32Le();
                    sampleType = cursor.ReadInt32Le();
                    valueMin = cursor.ReadFloatLe();
                    valueMax = cursor.ReadFloatLe();
                    break;
                default:
                    // NEVER FATAL — the caller (TryDecode / the emitter codec) raw-preserves this.
                    throw new ParticleParseException(ParticleParseError.UnexpectedForm,
                        "Unrecognized WaveForm version '" + versionTag + "'.");
            }

            int count = cursor.ReadInt32Le();
            GuardCount(count, cursor.Remaining);

            var points = new System.Collections.Generic.List<WaveFormControlPoint>(count);
            for (int i = 0; i < count; i++)
            {
                // 0002 swaps the random-min/random-max order vs 0000/0001 on disk, but the four floats
                // are read in on-disk order and re-emitted in the same order, so the round-trip stays
                // byte-exact regardless of which slot the engine treats as min vs max.
                float a = cursor.ReadFloatLe();
                float b = cursor.ReadFloatLe();
                float c = cursor.ReadFloatLe();
                float d = cursor.ReadFloatLe();
                points.Add(new WaveFormControlPoint(a, b, c, d));
            }

            return new ParticleWaveForm(versionTag, interpolationType, sampleType, valueMin, valueMax, points);
        }

        /// <summary>
        /// Re-encodes a typed waveform to its version-chunk payload bytes — the exact inverse of
        /// <see cref="Decode"/>, little-endian.
        /// </summary>
        public static byte[] Encode(ParticleWaveForm waveForm)
        {
            if (waveForm == null) throw new ArgumentNullException("waveForm");

            using (var ms = new MemoryStream())
            {
                switch (waveForm.Version)
                {
                    case "0000":
                        WriteInt32Le(ms, waveForm.InterpolationType);
                        break;
                    case "0001":
                        WriteInt32Le(ms, waveForm.InterpolationType);
                        WriteInt32Le(ms, RequireValue(waveForm.SampleType, "SampleType", waveForm.Version));
                        break;
                    case "0002":
                        WriteInt32Le(ms, waveForm.InterpolationType);
                        WriteInt32Le(ms, RequireValue(waveForm.SampleType, "SampleType", waveForm.Version));
                        WriteFloatLe(ms, RequireValue(waveForm.ValueMin, "ValueMin", waveForm.Version));
                        WriteFloatLe(ms, RequireValue(waveForm.ValueMax, "ValueMax", waveForm.Version));
                        break;
                    default:
                        throw new ParticleParseException(ParticleParseError.UnexpectedForm,
                            "Cannot encode unrecognized WaveForm version '" + waveForm.Version + "'.");
                }

                WriteInt32Le(ms, waveForm.ControlPoints.Count);
                foreach (WaveFormControlPoint p in waveForm.ControlPoints)
                {
                    WriteFloatLe(ms, p.Percent);
                    WriteFloatLe(ms, p.Value);
                    WriteFloatLe(ms, p.RandomMin);
                    WriteFloatLe(ms, p.RandomMax);
                }

                return ms.ToArray();
            }
        }

        // Division-form count guard (T-15-02): a count that cannot possibly fit in the remaining
        // payload (count × 16 > remaining), is negative, or exceeds the absolute cap is rejected
        // BEFORE the read loop allocates anything.
        private static void GuardCount(int count, int remainingBytes)
        {
            if (count < 0)
            {
                throw new ParticleParseException(ParticleParseError.NegativeCount,
                    "WaveForm control-point count is negative: " + count + ".");
            }
            if (count > MaxControlPoints)
            {
                throw new ParticleParseException(ParticleParseError.CountExceedsCap,
                    "WaveForm control-point count " + count + " exceeds the cap " + MaxControlPoints + ".");
            }
            // Division form avoids count * 16 overflow.
            if (count > remainingBytes / ControlPointBytes)
            {
                throw new ParticleParseException(ParticleParseError.CountExceedsCap,
                    "WaveForm control-point count " + count + " exceeds the "
                    + (remainingBytes / ControlPointBytes) + " record(s) the remaining "
                    + remainingBytes + " payload byte(s) can hold.");
            }
        }

        private static float RequireValue(float? v, string field, string version)
        {
            if (!v.HasValue)
                throw new ParticleParseException(ParticleParseError.UnexpectedForm,
                    "WaveForm version " + version + " requires " + field + ".");
            return v.Value;
        }

        private static int RequireValue(int? v, string field, string version)
        {
            if (!v.HasValue)
                throw new ParticleParseException(ParticleParseError.UnexpectedForm,
                    "WaveForm version " + version + " requires " + field + ".");
            return v.Value;
        }

        private static void WriteInt32Le(Stream s, int v)
        {
            s.WriteByte((byte)(v & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
            s.WriteByte((byte)((v >> 16) & 0xFF));
            s.WriteByte((byte)((v >> 24) & 0xFF));
        }

        private static void WriteFloatLe(Stream s, float v)
        {
            // BinaryWriter emits a float as 4 little-endian IEEE-754 bytes on the supported x86
            // platform — matching IffPayloadCursor.ReadFloatLe — with no byte-buffer index math and
            // without the byte-buffer reinterpret helper the codec deliberately avoids.
            var bw = new BinaryWriter(s);
            bw.Write(v);
            bw.Flush();
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
