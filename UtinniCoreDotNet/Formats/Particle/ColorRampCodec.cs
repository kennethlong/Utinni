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
// ColorRamp (FORM CLRR) layout understood by reading swg-client-v2
// .../clientParticle/.../ColorRamp.cpp (SOE/Bootprint, All Rights Reserved). The CLRR form holds a
// single version-tagged chunk (0000/0001); its little-endian payload is:
//   0000: uint32 interpType, uint32 count, count × (float percent, red, green, blue)
//   0001: uint32 interpType, uint32 sampleType, uint32 count, count × (float percent, red, green, blue)
// Only the on-disk layout was studied — no code, comments, identifier names, or test fixtures copied
// from any reference source. Implementation original to Utinni under MIT.

using System;
using System.IO;
using UtinniCoreDotNet.Formats.Decoders;

namespace UtinniCoreDotNet.Formats.Particle
{
    /// <summary>
    /// Decodes / re-encodes a ColorRamp version-chunk payload (the bytes inside the single
    /// <c>0000|0001</c> chunk of a <c>FORM CLRR</c>) to and from a typed <see cref="ParticleColorRamp"/>.
    /// Same byte-exact + degrade-don't-abort posture as <see cref="WaveFormCodec"/> (D-05): an
    /// unrecognized version (or any payload that does not decode + re-encode to the identical bytes) is
    /// carried as a byte-exact raw-preserve and NEVER throws.
    /// </summary>
    public static class ColorRampCodec
    {
        // Per-control-point record = 4 little-endian floats (16 bytes) on every known version.
        internal const int ControlPointBytes = 16;
        internal const int MaxControlPoints = 16 * 1024 * 1024;

        /// <summary>
        /// Decodes a CLRR version-chunk payload into a typed <see cref="ParticleFieldValue"/>. A
        /// recognized version that decodes AND re-encodes to the identical bytes is returned as a typed
        /// <see cref="ParticleFieldKind.ColorRamp"/>; anything else is a byte-exact raw-preserve. NEVER
        /// throws for an unrecognized version (D-05).
        /// </summary>
        public static ParticleFieldValue TryDecode(string versionTag, byte[] chunkPayload)
        {
            if (versionTag == null) throw new ArgumentNullException("versionTag");
            if (chunkPayload == null) throw new ArgumentNullException("chunkPayload");

            try
            {
                ParticleColorRamp typed = Decode(versionTag, chunkPayload);
                byte[] reencoded = Encode(typed);
                if (BytesEqual(reencoded, chunkPayload))
                {
                    return ParticleFieldValue.FromColorRamp(typed);
                }
            }
            catch (DecoderException)
            {
            }
            catch (ParticleParseException)
            {
            }

            return ParticleFieldValue.FromRawBytes(chunkPayload);
        }

        /// <summary>
        /// Decodes a recognized ColorRamp version-chunk payload into a typed <see cref="ParticleColorRamp"/>.
        /// </summary>
        public static ParticleColorRamp Decode(string versionTag, byte[] chunkPayload)
        {
            if (versionTag == null) throw new ArgumentNullException("versionTag");
            if (chunkPayload == null) throw new ArgumentNullException("chunkPayload");

            var cursor = new IffPayloadCursor(chunkPayload);

            long interpolationType;
            long? sampleType = null;

            switch (versionTag)
            {
                case "0000":
                    interpolationType = cursor.ReadUInt32Le();
                    break;
                case "0001":
                    interpolationType = cursor.ReadUInt32Le();
                    sampleType = cursor.ReadUInt32Le();
                    break;
                default:
                    throw new ParticleParseException(ParticleParseError.UnexpectedForm,
                        "Unrecognized ColorRamp version '" + versionTag + "'.");
            }

            long count = cursor.ReadUInt32Le();
            GuardCount(count, cursor.Remaining);

            int intCount = (int)count;
            var points = new System.Collections.Generic.List<ColorRampControlPoint>(intCount);
            for (int i = 0; i < intCount; i++)
            {
                float percent = cursor.ReadFloatLe();
                float red = cursor.ReadFloatLe();
                float green = cursor.ReadFloatLe();
                float blue = cursor.ReadFloatLe();
                points.Add(new ColorRampControlPoint(percent, red, green, blue));
            }

            return new ParticleColorRamp(versionTag, interpolationType, sampleType, points);
        }

        /// <summary>Re-encodes a typed color ramp to its version-chunk payload bytes (little-endian).</summary>
        public static byte[] Encode(ParticleColorRamp colorRamp)
        {
            if (colorRamp == null) throw new ArgumentNullException("colorRamp");

            using (var ms = new MemoryStream())
            {
                switch (colorRamp.Version)
                {
                    case "0000":
                        WriteUInt32Le(ms, colorRamp.InterpolationType);
                        break;
                    case "0001":
                        WriteUInt32Le(ms, colorRamp.InterpolationType);
                        if (!colorRamp.SampleType.HasValue)
                            throw new ParticleParseException(ParticleParseError.UnexpectedForm,
                                "ColorRamp version 0001 requires SampleType.");
                        WriteUInt32Le(ms, colorRamp.SampleType.Value);
                        break;
                    default:
                        throw new ParticleParseException(ParticleParseError.UnexpectedForm,
                            "Cannot encode unrecognized ColorRamp version '" + colorRamp.Version + "'.");
                }

                WriteUInt32Le(ms, colorRamp.ControlPoints.Count);
                foreach (ColorRampControlPoint p in colorRamp.ControlPoints)
                {
                    WriteFloatLe(ms, p.Percent);
                    WriteFloatLe(ms, p.Red);
                    WriteFloatLe(ms, p.Green);
                    WriteFloatLe(ms, p.Blue);
                }

                return ms.ToArray();
            }
        }

        // Division-form count guard (T-15-02) over the unsigned long count.
        private static void GuardCount(long count, int remainingBytes)
        {
            if (count < 0)
            {
                throw new ParticleParseException(ParticleParseError.NegativeCount,
                    "ColorRamp control-point count is negative: " + count + ".");
            }
            if (count > MaxControlPoints)
            {
                throw new ParticleParseException(ParticleParseError.CountExceedsCap,
                    "ColorRamp control-point count " + count + " exceeds the cap " + MaxControlPoints + ".");
            }
            if (count > remainingBytes / ControlPointBytes)
            {
                throw new ParticleParseException(ParticleParseError.CountExceedsCap,
                    "ColorRamp control-point count " + count + " exceeds the "
                    + (remainingBytes / ControlPointBytes) + " record(s) the remaining "
                    + remainingBytes + " payload byte(s) can hold.");
            }
        }

        private static void WriteUInt32Le(Stream s, long v)
        {
            uint u = unchecked((uint)v);
            s.WriteByte((byte)(u & 0xFF));
            s.WriteByte((byte)((u >> 8) & 0xFF));
            s.WriteByte((byte)((u >> 16) & 0xFF));
            s.WriteByte((byte)((u >> 24) & 0xFF));
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
