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
// Terrain (.trn / FORM TGEN) single-field re-encoder understood by reading swg-client-v2
// .../sharedTerrain/.../generator/{AffectorHeight,AffectorColor,AffectorShader,Boundary,Filter}.{cpp,h}
// (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. Per Pitfall 6 the DATA payload
// scalars are little-endian on the original Win32 client; this encoder inverts the IffPayloadCursor read
// assembly to WRITE a single field's exact byte span while copying every other byte verbatim. Only the
// on-disk layout was studied — no code, comments, identifier names, or test fixtures copied from any
// reference source. Implementation original to Utinni under MIT.

using System;
using System.Globalization;

namespace UtinniCoreDotNet.Formats.Terrain
{
    /// <summary>
    /// Re-encodes EXACTLY ONE field inside a packed terrain <c>DATA</c> payload, consuming the SAME
    /// single-source <see cref="TgenFieldLayouts"/> descriptor table the <see cref="Decoders.TgenDecoder"/>
    /// reads (review concern #2 — NO offset literals here). It overwrites ONLY the target field's exact
    /// byte span <c>[offset .. offset+width)</c> from the resolved descriptor and copies every other byte
    /// of the input payload verbatim — it NEVER reserializes the payload from a typed model, so untouched
    /// fields (including floats) keep their EXACT input bits (review concern #6).
    ///
    /// <para><b>Edit scope (D-05):</b> fixed-length scalar / int / enum / familyId-ref / active-flag only.
    /// A name / variable-length field is REJECTED (D-06). For float fields the accepted input is an
    /// invariant-culture decimal; <c>NaN</c> / <c>Infinity</c> is REJECTED (concern #6).</para>
    /// </summary>
    public static class TrnFieldEncoder
    {
        /// <summary>
        /// Returns a SAME-LENGTH copy of <paramref name="dataPayload"/> in which ONLY the
        /// <paramref name="fieldName"/> field (located via <c>TgenFieldLayouts.For(tag, version)</c>) has
        /// been overwritten with the little-endian encoding of <paramref name="value"/>. Every byte outside
        /// the target field span is copied verbatim from the input.
        /// </summary>
        /// <param name="dataPayload">The packed DATA payload of a typed terrain leaf (copied, never mutated).</param>
        /// <param name="tag">The enclosing typed FORM FourCC (recovered by the caller's ResolveFieldContext).</param>
        /// <param name="version">The enclosing version FORM (recovered by the caller's ResolveFieldContext).</param>
        /// <param name="fieldName">The descriptor field name to edit (matched on <see cref="TgenFieldDescriptor.Name"/>).</param>
        /// <param name="value">The new value as a string, parsed per the descriptor's <see cref="TgenEditParser"/>.</param>
        /// <returns>A new payload identical to the input except the one edited field's exact byte span.</returns>
        /// <exception cref="ArgumentNullException">A required argument was null.</exception>
        /// <exception cref="ArgumentException">
        /// No layout for (tag, version); the field name is not in the descriptor list; the field is not
        /// editable / is variable-length (D-06); the field span exceeds the payload; or the value is
        /// unparseable / NaN / Infinity for the descriptor's parser.
        /// </exception>
        public static byte[] EncodeField(byte[] dataPayload, string tag, string version, string fieldName, string value)
        {
            if (dataPayload == null) throw new ArgumentNullException(nameof(dataPayload));
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            if (version == null) throw new ArgumentNullException(nameof(version));
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));

            var descriptors = TgenFieldLayouts.For(tag, version);
            if (descriptors == null || descriptors.Count == 0)
            {
                throw new ArgumentException(
                    "No TgenFieldLayouts descriptor for tag '" + tag + "' version '" + version
                    + "'; refusing to guess field offsets (D-02).", nameof(tag));
            }

            TgenFieldDescriptor target = null;
            foreach (var d in descriptors)
            {
                if (string.Equals(d.Name, fieldName, StringComparison.Ordinal)) { target = d; break; }
            }
            if (target == null)
            {
                throw new ArgumentException(
                    "Field '" + fieldName + "' is not a descriptor field of " + tag + "/" + version
                    + " — no silent no-op.", nameof(fieldName));
            }

            if (!target.Editable)
            {
                throw new ArgumentException(
                    "Field '" + fieldName + "' on " + tag + "/" + version + " is not editable.", nameof(fieldName));
            }

            // D-06: variable-length / name edits are out of v1. The fixed-length scope is the closed set of
            // display types below; anything else is rejected (the descriptor table only contains fixed-length
            // scalar/enum/familyId/active fields today, so this is a forward guard).
            switch (target.DisplayType)
            {
                case TgenDisplayType.ScalarFloat:
                case TgenDisplayType.Int32:
                case TgenDisplayType.Enum32:
                case TgenDisplayType.FamilyIdRef:
                case TgenDisplayType.ActiveFlag:
                    break;
                default:
                    throw new ArgumentException(
                        "Field '" + fieldName + "' has unsupported edit type " + target.DisplayType
                        + " (variable-length / name edits are out of v1 — D-06).", nameof(fieldName));
            }

            if (target.Offset < 0 || target.Width <= 0 || target.Offset + target.Width > dataPayload.Length)
            {
                throw new ArgumentException(
                    "Field '" + fieldName + "' span [" + target.Offset + ".." + (target.Offset + target.Width)
                    + ") exceeds payload length " + dataPayload.Length + ".", nameof(fieldName));
            }

            // Encode the new value to its exact-width little-endian byte span per the descriptor's parser.
            byte[] fieldBytes = EncodeValue(target, value, fieldName);

            // Copy the input verbatim, then overwrite ONLY the target field's exact byte span. No other byte
            // is touched — untouched fields (incl. floats) keep their EXACT input bits (#2/#6).
            byte[] result = (byte[])dataPayload.Clone();
            Array.Copy(fieldBytes, 0, result, target.Offset, target.Width);
            return result;
        }

        // Encodes the supplied string value to the descriptor's fixed-width little-endian byte span. Inverts
        // the IffPayloadCursor read assembly: for floats BitConverter.GetBytes + reverse on a big-endian host;
        // for int/enum/familyId/active a width-byte LE integer.
        private static byte[] EncodeValue(TgenFieldDescriptor d, string value, string fieldName)
        {
            switch (d.EditParser)
            {
                case TgenEditParser.Float:
                {
                    if (value == null)
                        throw new ArgumentException("Field '" + fieldName + "' requires a float value.", nameof(value));
                    float f;
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
                    {
                        throw new ArgumentException(
                            "Field '" + fieldName + "' value '" + value
                            + "' is not an invariant-culture decimal float.", nameof(value));
                    }
                    if (float.IsNaN(f) || float.IsInfinity(f))
                    {
                        throw new ArgumentException(
                            "Field '" + fieldName + "' rejects NaN/Infinity ('" + value + "') — #6.", nameof(value));
                    }
                    byte[] four = BitConverter.GetBytes(f);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(four);
                    return WidthAdjust(four, d.Width, fieldName);
                }

                case TgenEditParser.Bool32:
                {
                    // Accept true/false/1/0 (an int32 active flag). Any non-zero int is "active".
                    int iv;
                    if (bool.TryParse(value, out bool b))
                    {
                        iv = b ? 1 : 0;
                    }
                    else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out iv))
                    {
                        // numeric form accepted verbatim (0 / non-0)
                    }
                    else
                    {
                        throw new ArgumentException(
                            "Field '" + fieldName + "' value '" + value
                            + "' is not a boolean (true/false/1/0).", nameof(value));
                    }
                    return Int32LeWidth(iv, d.Width, fieldName);
                }

                case TgenEditParser.Int32:
                {
                    int iv;
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out iv))
                    {
                        throw new ArgumentException(
                            "Field '" + fieldName + "' value '" + value
                            + "' is not an invariant-culture int32.", nameof(value));
                    }
                    return Int32LeWidth(iv, d.Width, fieldName);
                }

                default:
                    // A new TgenEditParser value MUST fail loudly here rather than silently encode as int32
                    // (IN-03) — folding an unknown parser into the int32 path would mis-encode the field.
                    throw new ArgumentException(
                        "Field '" + fieldName + "' has an unhandled EditParser " + d.EditParser
                        + "; refusing to default to int32.", nameof(fieldName));
            }
        }

        // Returns the int32 little-endian bytes truncated/checked to the descriptor width. The terrain
        // scalar set is all 4-byte LE (W=4); a non-4 width is a descriptor error.
        private static byte[] Int32LeWidth(int v, int width, string fieldName)
        {
            if (width != 4)
            {
                throw new ArgumentException(
                    "Field '" + fieldName + "' has non-32-bit integer width " + width
                    + "; the terrain scalar set is 4-byte LE.", nameof(fieldName));
            }
            return new[]
            {
                (byte)(v & 0xFF),
                (byte)((v >> 8) & 0xFF),
                (byte)((v >> 16) & 0xFF),
                (byte)((v >> 24) & 0xFF),
            };
        }

        // Guards that the produced byte span matches the descriptor width (floats are always 4 bytes here).
        private static byte[] WidthAdjust(byte[] bytes, int width, string fieldName)
        {
            if (bytes.Length != width)
            {
                throw new ArgumentException(
                    "Field '" + fieldName + "' encoded to " + bytes.Length + " bytes but the descriptor width is "
                    + width + ".", nameof(fieldName));
            }
            return bytes;
        }
    }
}
