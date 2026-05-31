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
// Object-template per-param self-describing value layout understood by reading swg-client-v2
// .../sharedUtility/.../TemplateParameter.cpp and the generated Shared*ObjectTemplate.cpp loaders
// (SOE/Bootprint, All Rights Reserved): param chunk payload = NUL-terminated field name, then a
// 1-byte data-type tag, then (for numeric int/float) a 1-byte delta-type byte, then the value.
// Only the on-disk layout was studied — no code, comments, identifier names, or test fixtures
// copied from any reference source. Implementation original to Utinni under MIT.

using System;
using System.IO;
using System.Text;
using UtinniCoreDotNet.Formats.Decoders;

namespace UtinniCoreDotNet.Formats.ObjectTemplate
{
    /// <summary>
    /// Decodes and re-encodes a single object-template param chunk payload (a NUL-terminated field
    /// name followed by a self-describing, type-tagged value) into a typed
    /// <see cref="ObjectTemplateParamValue"/>.
    ///
    /// <para><b>Generic, schema-free (D-03):</b> the per-param data-type tag tells the decoder the
    /// value's shape — no <c>.tdf</c> port is needed for scalars.</para>
    ///
    /// <para><b>Consume-exactly-or-hex defensive posture (RESEARCH-locked, T-11-01):</b> a scalar
    /// decode is only accepted when it consumes the chunk payload EXACTLY
    /// (<c>cursor.Remaining == 0</c>). Any inconsistency — WEIGHTED_LIST / RANGE / DIE_ROLL / struct /
    /// dynamic-variable shapes, or a scalar whose decode leaves trailing or insufficient bytes — is
    /// routed to <see cref="ObjectTemplateParamKind.RawBytesHexFallback"/> carrying the verbatim
    /// value-region bytes, so the param is never mis-typed and always round-trips byte-for-byte.</para>
    /// </summary>
    public static class ObjectTemplateParamCodec
    {
        // The numeric delta-type byte only follows int/float scalar tags. Bool/String do not carry it.
        // Tag values: 0 NONE, 1 SINGLE, 2 WEIGHTED_LIST, 3 RANGE, 4 DIE_ROLL.

        /// <summary>
        /// Decodes a param chunk payload into its field name and typed value. The value is a typed
        /// scalar when the self-describing decode consumes the payload exactly; otherwise the verbatim
        /// value-region bytes are carried as a hex-fallback value (never mis-typed).
        /// </summary>
        /// <param name="paramChunkPayload">The raw param-chunk leaf payload bytes.</param>
        public static ObjectTemplateParamEntry Decode(byte[] paramChunkPayload)
        {
            if (paramChunkPayload == null) throw new ArgumentNullException("paramChunkPayload");

            var cursor = new IffPayloadCursor(paramChunkPayload);
            string fieldName = cursor.ReadCString(Encoding.ASCII);

            // The value region is everything AFTER the field-name NUL terminator. Capture it verbatim
            // up front so the hex-fallback path can preserve it byte-for-byte regardless of how far the
            // scalar attempt advanced.
            int valueRegionLength = cursor.Remaining;
            byte[] valueRegion = valueRegionLength > 0 ? cursor.ReadBytes(valueRegionLength) : new byte[0];

            ObjectTemplateParamValue value = DecodeValueRegion(valueRegion);
            return new ObjectTemplateParamEntry(fieldName, value);
        }

        // Attempts a typed scalar decode of the value region. Returns a typed value ONLY when the
        // decode consumes the region exactly; otherwise a RawBytesHexFallback carrying the verbatim
        // region bytes.
        private static ObjectTemplateParamValue DecodeValueRegion(byte[] valueRegion)
        {
            var probe = new IffPayloadCursor(valueRegion);

            // An empty value region carries no data-type tag — treat as hex-fallback (the writer
            // re-emits zero bytes). NONE params, by contrast, carry the 1-byte NONE tag.
            if (probe.Remaining < 1)
            {
                return ObjectTemplateParamValue.FromRawBytes(valueRegion, ObjectTemplateDataTypeTag.None);
            }

            byte tagByte = probe.ReadInt8();

            switch (tagByte)
            {
                case (byte)ObjectTemplateDataTypeTag.None:
                    // NONE: the tag alone consumes the region exactly (no value bytes follow).
                    return probe.Remaining == 0
                        ? ObjectTemplateParamValue.FromNone()
                        : Fallback(valueRegion, ObjectTemplateDataTypeTag.None);

                case (byte)ObjectTemplateDataTypeTag.Single:
                    return TryDecodeSingle(valueRegion, probe);

                case (byte)ObjectTemplateDataTypeTag.WeightedList:
                    return Fallback(valueRegion, ObjectTemplateDataTypeTag.WeightedList);

                case (byte)ObjectTemplateDataTypeTag.Range:
                    return Fallback(valueRegion, ObjectTemplateDataTypeTag.Range);

                case (byte)ObjectTemplateDataTypeTag.DieRoll:
                    return Fallback(valueRegion, ObjectTemplateDataTypeTag.DieRoll);

                default:
                    // Unknown / not-a-tag leading byte — never guess.
                    return Fallback(valueRegion, ObjectTemplateDataTypeTag.None);
            }
        }

        // A SINGLE value can be one of several scalar shapes distinguishable by the consume-exactly
        // guard against the region length:
        //   bool   : [int8 bool]                               → region length 2 (tag + 1)
        //   int    : [int8 delta][int32]                       → region length 6 (tag + 1 + 4)
        //   float  : [int8 delta][float32]                     → region length 6 (tag + 1 + 4)
        //   string : [NUL-terminated bytes]                    → variable, must end exactly at region end
        //
        // bool (region length 2) and string (NUL-terminated, no delta byte) are unambiguous and are
        // surfaced as their typed variants.
        //
        // int and float share a 6-byte region (tag + delta + int32/float32) and are byte-identical in
        // structure — the generic, schema-free V1 decoder (D-03) cannot tell them apart from bytes
        // alone. It decodes the canonical numeric SINGLE as a typed Int (delta byte preserved
        // verbatim). Byte-exactness is preserved either way: encoding an Int writes the same
        // delta + 4 value bytes the region carried, so a numeric param that the engine treats as a
        // float still round-trips byte-for-byte (only the editor's widget label differs — V2's typed
        // schema disambiguates). A Float value carried in from a typed caller likewise encodes its 4
        // value bytes verbatim.
        private static ObjectTemplateParamValue TryDecodeSingle(byte[] valueRegion, IffPayloadCursor probe)
        {
            int afterTag = probe.Remaining;

            // bool: exactly one byte after the tag, and that byte is a valid 0/1.
            if (afterTag == 1)
            {
                byte b = probe.ReadInt8();
                if (probe.Remaining == 0 && (b == 0 || b == 1))
                {
                    return ObjectTemplateParamValue.FromBool(b != 0);
                }
                return Fallback(valueRegion, ObjectTemplateDataTypeTag.Single);
            }

            // string: a NUL-terminated run that ends exactly at the region end (no delta byte).
            // Try this BEFORE the numeric shape so a printable, NUL-terminated SINGLE is surfaced
            // as a typed string. ReadCString throws on an unterminated run; catch and fall through.
            if (LooksLikeNulTerminatedString(valueRegion))
            {
                var strCursor = new IffPayloadCursor(valueRegion);
                strCursor.ReadInt8(); // re-consume the SINGLE tag
                try
                {
                    string s = strCursor.ReadCString(Encoding.ASCII);
                    if (strCursor.Remaining == 0)
                    {
                        return ObjectTemplateParamValue.FromString(s);
                    }
                }
                catch (DecoderException)
                {
                    // Not a clean NUL-terminated string — fall through.
                }
            }

            // numeric int/float: [int8 delta][int32]. Region length must be exactly 6 (tag + 1 + 4).
            if (afterTag == 5)
            {
                byte delta = probe.ReadInt8();
                int intValue = probe.ReadInt32Le();
                if (probe.Remaining == 0)
                {
                    return ObjectTemplateParamValue.FromInt(intValue, delta);
                }
            }

            // Anything else (unexpected length / interior structure) → byte-exact hex fallback.
            return Fallback(valueRegion, ObjectTemplateDataTypeTag.Single);
        }

        // A SINGLE string region is: [tag][value bytes...][NUL], with the NUL as the final byte and no
        // interior NUL before it. (An empty string is just [tag][NUL].) This sniff is conservative —
        // the consume-exactly ReadCString in TryDecodeSingle is the authoritative guard.
        private static bool LooksLikeNulTerminatedString(byte[] valueRegion)
        {
            // valueRegion[0] is the SINGLE tag; the string body starts at index 1.
            if (valueRegion.Length < 2) return false;
            if (valueRegion[valueRegion.Length - 1] != 0) return false;
            for (int i = 1; i < valueRegion.Length - 1; i++)
            {
                if (valueRegion[i] == 0) return false; // interior NUL — not a single C-string
            }
            return true;
        }

        private static ObjectTemplateParamValue Fallback(byte[] valueRegion, ObjectTemplateDataTypeTag tag)
        {
            return ObjectTemplateParamValue.FromRawBytes(valueRegion, tag);
        }

        /// <summary>
        /// Re-encodes a param entry to the verbatim chunk payload bytes — the inverse of
        /// <see cref="Decode"/>. Typed scalars reconstruct <c>name NUL · tag · (delta) · value</c>;
        /// the hex-fallback variant re-emits <c>name NUL · verbatim value bytes</c>.
        /// </summary>
        public static byte[] Encode(string fieldName, ObjectTemplateParamValue value)
        {
            if (fieldName == null) throw new ArgumentNullException("fieldName");
            if (value == null) throw new ArgumentNullException("value");

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                byte[] nameAscii = Encoding.ASCII.GetBytes(fieldName);
                bw.Write(nameAscii, 0, nameAscii.Length);
                bw.Write((byte)0); // field-name NUL terminator

                switch (value.Kind)
                {
                    case ObjectTemplateParamKind.None:
                        bw.Write((byte)ObjectTemplateDataTypeTag.None);
                        break;

                    case ObjectTemplateParamKind.Bool:
                        bw.Write((byte)ObjectTemplateDataTypeTag.Single);
                        bw.Write((byte)(value.BoolValue ? 1 : 0));
                        break;

                    case ObjectTemplateParamKind.Int:
                        bw.Write((byte)ObjectTemplateDataTypeTag.Single);
                        bw.Write(value.DeltaType.Value);
                        WriteInt32Le(bw, value.IntValue);
                        break;

                    case ObjectTemplateParamKind.Float:
                        bw.Write((byte)ObjectTemplateDataTypeTag.Single);
                        bw.Write(value.DeltaType.Value);
                        WriteFloatLe(bw, value.FloatValue);
                        break;

                    case ObjectTemplateParamKind.String:
                        bw.Write((byte)ObjectTemplateDataTypeTag.Single);
                        byte[] strAscii = Encoding.ASCII.GetBytes(value.StringValue);
                        bw.Write(strAscii, 0, strAscii.Length);
                        bw.Write((byte)0); // value NUL terminator
                        break;

                    case ObjectTemplateParamKind.RawBytesHexFallback:
                        byte[] raw = value.GetRawBytesCopy();
                        bw.Write(raw, 0, raw.Length);
                        break;

                    default:
                        throw new InvalidOperationException("Unhandled param kind: " + value.Kind + ".");
                }

                bw.Flush();
                return ms.ToArray();
            }
        }

        private static void WriteInt32Le(BinaryWriter bw, int v)
        {
            // BinaryWriter is little-endian on the supported (x86) platform; write explicitly so the
            // round-trip matches IffPayloadCursor.ReadInt32Le regardless of host endianness.
            bw.Write((byte)(v & 0xFF));
            bw.Write((byte)((v >> 8) & 0xFF));
            bw.Write((byte)((v >> 16) & 0xFF));
            bw.Write((byte)((v >> 24) & 0xFF));
        }

        private static void WriteFloatLe(BinaryWriter bw, float v)
        {
            byte[] four = BitConverter.GetBytes(v);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(four);
            }
            bw.Write(four, 0, 4);
        }
    }

    /// <summary>
    /// A decoded object-template param: its field name and typed value. The simple pair the codec's
    /// <see cref="ObjectTemplateParamCodec.Decode"/> returns and the mutable model holds per local
    /// param chunk.
    /// </summary>
    public sealed class ObjectTemplateParamEntry
    {
        /// <summary>The NUL-terminated field name read from the chunk.</summary>
        public string FieldName { get; }

        /// <summary>The decoded typed value.</summary>
        public ObjectTemplateParamValue Value { get; }

        /// <summary>Constructs a param entry.</summary>
        public ObjectTemplateParamEntry(string fieldName, ObjectTemplateParamValue value)
        {
            if (fieldName == null) throw new ArgumentNullException("fieldName");
            if (value == null) throw new ArgumentNullException("value");
            FieldName = fieldName;
            Value = value;
        }
    }
}
