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
// Schema-driven kernel codec for an IFF leaf-chunk PAYLOAD. The composite write order this codec
// follows was understood by reading swg-client-v2/.../sharedFile/.../Iff.cpp + the per-engine composite
// writers (SOE/Bootprint, All Rights Reserved): payload scalars are LITTLE-endian and strings are
// NUL-terminated C-strings (strlen+1 on disk, never length-prefixed). Only the on-disk layout was
// studied -- no code, comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.
//
// The codec produces / consumes ONLY leaf payload bytes -- it NEVER touches IFF tag/length framing or
// the SWG no-pad quirk (MutableIffNode.SetPayload + IffWriter own the length ripple, already free). The
// ONE genuinely new encode obligation is the D-10 count-from-prior AUTO-RECOMPUTE: when a field is the
// element-count source for a later array, the encoder writes the CURRENT element count, never the stale
// decoded value, so an array grow/shrink round-trips byte-exact (the DEC-C3 golden).
//
// SECURITY (V5): decode never pre-allocates on an attacker-controlled count -- it loop-reads each
// element through the bounds-checked IffPayloadCursor (Need(n) per read), so a count-from-prior field
// that over-claims throws DecoderException(Truncated) before any large allocation. Fixed counts are
// capped at a sane bound; negative/overflowing widths are rejected with a typed TemplateException.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UtinniCoreDotNet.Formats.Decoders;

namespace UtinniCoreDotNet.Formats.Template
{
    /// <summary>
    /// Byte-exact schema-driven decode + encode over the kernel type vocabulary (<see cref="KernelType"/>)
    /// and the D-09 preset structs. <see cref="Decode"/> walks a template's fields in order through a
    /// bounds-checked <see cref="IffPayloadCursor"/> into a <see cref="DecodedTemplate"/> value carrier;
    /// <see cref="Encode"/> mirrors the walk into a single <see cref="MemoryStream"/>, AUTO-RECOMPUTING any
    /// count-from-prior field from the bound array's element count (D-10). Presets are pure kernel sugar:
    /// byte-exactness lives ENTIRELY here (D-08), so a preset can never introduce a parity bug.
    /// </summary>
    public static class KernelCodec
    {
        // A sane upper bound for a FIXED-count array's element count (V5 OOM guard). A count-from-prior
        // count is never pre-trusted (the loop-read + Need(n) cursor guard catches over-claims), but a
        // template-authored FixedCount is bounded here so a hostile template cannot request a huge loop.
        private const int MaxFixedArrayElements = 1 << 20; // ~1M elements

        // ── public entry points ──────────────────────────────────────────────────

        /// <summary>
        /// Decodes a chunk payload against a template into a <see cref="DecodedTemplate"/> value carrier.
        /// Reads every field in declaration order through a bounds-checked cursor. Throws
        /// <see cref="DecoderException"/>(<see cref="DecoderError.Truncated"/>) if the payload is shorter
        /// than the template describes, and <see cref="TemplateException"/> for a structurally invalid
        /// template (e.g. a count field referencing an undefined prior field, or a negative width).
        /// </summary>
        public static DecodedTemplate Decode(TemplateModel template, byte[] payload)
        {
            int consumed;
            int total;
            return DecodeInternal(template, payload, out consumed, out total);
        }

        /// <summary>
        /// Decode variant that also reports how many bytes of the payload the walk consumed and the total
        /// payload length. Used by the pure <see cref="FitChecker.FitCheck"/> (D-17.2) to compute
        /// consumed-exactly without re-walking the template; the public <see cref="Decode"/> hides the
        /// out params. Same throw discipline as <see cref="Decode"/>.
        /// </summary>
        internal static DecodedTemplate DecodeInternal(TemplateModel template, byte[] payload,
            out int bytesConsumed, out int bytesTotal)
        {
            if (template == null) throw new TemplateException("Cannot decode against a null template.");
            if (template.Fields == null) throw new TemplateException("Template has no fields to decode.");

            var cursor = new IffPayloadCursor(payload ?? new byte[0]);
            var values = new Dictionary<string, object>();

            DecodeFields(template.Fields, cursor, values);

            bytesTotal = cursor.Length;
            bytesConsumed = cursor.Length - cursor.Remaining;
            return new DecodedTemplate { Template = template, Values = values };
        }

        /// <summary>
        /// Encodes a decoded value carrier back to a byte-exact chunk payload. Walks the template fields in
        /// declaration order into one <see cref="MemoryStream"/>; for a field that is the element-count
        /// source of a later count-from-prior array, writes the CURRENT element count rather than the
        /// stored value (D-10 auto-recompute -- the core encode-parity mechanism). Throws
        /// <see cref="TemplateException"/> for a structurally invalid template or a value that does not fit
        /// its declared kernel type.
        /// </summary>
        public static byte[] Encode(DecodedTemplate decoded)
        {
            if (decoded == null) throw new TemplateException("Cannot encode a null decoded template.");
            if (decoded.Template == null) throw new TemplateException("Decoded template carries no model.");
            if (decoded.Template.Fields == null) throw new TemplateException("Template has no fields to encode.");
            if (decoded.Values == null) throw new TemplateException("Decoded template carries no values.");

            using (var ms = new MemoryStream())
            {
                EncodeFields(decoded.Template.Fields, decoded.Values, ms);
                return ms.ToArray();
            }
        }

        // ── decode walk ──────────────────────────────────────────────────────────

        private static void DecodeFields(List<FieldRecord> fields, IffPayloadCursor cursor,
            Dictionary<string, object> into)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                FieldRecord f = fields[i];
                if (f == null) throw new TemplateException("Template contains a null field record.");
                if (string.IsNullOrEmpty(f.Name)) throw new TemplateException("Template field is missing a name.");

                // WR-03: an UntilEnd array consumes the WHOLE remaining payload, so it MUST be the last
                // field of its (sub)record. A field after it would either be starved (Truncated) or, worse,
                // the UntilEnd would greedily eat that field's bytes. Reject the malformed layout structurally
                // with a clear message rather than producing a confusing downstream Truncated.
                if (f.Type == KernelType.Array && f.Repeat != null
                    && f.Repeat.Kind == RepeatKind.UntilEnd && i != fields.Count - 1)
                {
                    throw new TemplateException("Array '" + f.Name + "' is an until-end array but is not the "
                        + "last field of its record; an until-end array must be terminal (it consumes all "
                        + "remaining bytes).");
                }

                into[f.Name] = DecodeField(f, cursor, into);
            }
        }

        private static object DecodeField(FieldRecord f, IffPayloadCursor cursor,
            Dictionary<string, object> siblings)
        {
            switch (f.Type)
            {
                case KernelType.I8: return (long)(sbyte)cursor.ReadInt8();
                case KernelType.U8: return (long)cursor.ReadInt8();
                case KernelType.I16: return (long)cursor.ReadInt16Le();
                case KernelType.U16: return (long)cursor.ReadUInt16Le();
                case KernelType.I32: return (long)cursor.ReadInt32Le();
                case KernelType.U32: return cursor.ReadUInt32Le();
                case KernelType.F32: return cursor.ReadFloatLe();
                case KernelType.F64: return cursor.ReadDoubleLe();
                case KernelType.CString: return cursor.ReadCString(EncodingFor(f));
                case KernelType.FixedChar: return cursor.ReadRawBytes(RequireWidth(f, "FixedChar"));
                case KernelType.RawBytes: return cursor.ReadRawBytes(RequireWidth(f, "RawBytes"));
                case KernelType.Pad: return cursor.ReadRawBytes(RequireWidth(f, "Pad"));
                case KernelType.Struct: return DecodeStruct(f, cursor);
                case KernelType.Array: return DecodeArray(f, cursor, siblings);
                default: throw new TemplateException("Unsupported kernel type: " + f.Type);
            }
        }

        private static Dictionary<string, object> DecodeStruct(FieldRecord f, IffPayloadCursor cursor)
        {
            if (f.StructFields == null || f.StructFields.Count == 0)
            {
                throw new TemplateException("Struct field '" + f.Name + "' has no sub-fields.");
            }

            var inner = new Dictionary<string, object>();
            DecodeFields(f.StructFields, cursor, inner);
            return inner;
        }

        private static List<object> DecodeArray(FieldRecord f, IffPayloadCursor cursor,
            Dictionary<string, object> siblings)
        {
            RepeatSpec repeat = f.Repeat;
            if (repeat == null) throw new TemplateException("Array field '" + f.Name + "' has no repeat spec.");

            var elements = new List<object>();

            switch (repeat.Kind)
            {
                case RepeatKind.FixedCount:
                {
                    int count = repeat.FixedCount;
                    if (count < 0) throw new TemplateException("Array '" + f.Name + "' has a negative fixed count.");
                    if (count > MaxFixedArrayElements)
                    {
                        throw new TemplateException("Array '" + f.Name + "' fixed count " + count
                            + " exceeds the safety cap " + MaxFixedArrayElements + ".");
                    }
                    for (int i = 0; i < count; i++) elements.Add(DecodeArrayElement(f, cursor));
                    break;
                }
                case RepeatKind.CountFromField:
                {
                    long count = ReadPriorCount(repeat.CountFieldName, f.Name, siblings);
                    // V5: never pre-allocate to `count`. Loop-read; the cursor's Need(n) throws Truncated
                    // before any large allocation if the count over-claims the remaining payload.
                    for (long i = 0; i < count; i++) elements.Add(DecodeArrayElement(f, cursor));
                    break;
                }
                case RepeatKind.UntilEnd:
                {
                    while (cursor.Remaining > 0)
                    {
                        int before = cursor.Remaining;
                        try
                        {
                            elements.Add(DecodeArrayElement(f, cursor));
                        }
                        catch (DecoderException ex) when (ex.Kind == DecoderError.Truncated)
                        {
                            // WR-03: a non-zero remainder that does not make up one whole element width is a
                            // ragged tail -- the payload is NOT cleanly an array of this element type. Surface
                            // a clear structural error rather than the bare Truncated.
                            throw new TemplateException("Until-end array '" + f.Name + "' has " + before
                                + " trailing byte(s) that do not form a whole element; the payload is not a "
                                + "clean array of this element shape.", ex);
                        }

                        // Guard against a zero-width element (e.g. an empty struct) that would loop forever.
                        if (cursor.Remaining == before)
                        {
                            throw new TemplateException("Until-end array '" + f.Name + "' element consumed no "
                                + "bytes; a zero-width element would loop forever.");
                        }
                    }
                    break;
                }
                default:
                    throw new TemplateException("Unsupported array repeat kind: " + repeat.Kind);
            }

            return elements;
        }

        // An array element is either a struct (when StructFields are declared) or a single scalar of the
        // element kernel type carried in ByteWidth-bearing fields. The repeated element shape is the
        // array field's StructFields; a scalar array declares a single sub-field.
        private static object DecodeArrayElement(FieldRecord arrayField, IffPayloadCursor cursor)
        {
            if (arrayField.StructFields == null || arrayField.StructFields.Count == 0)
            {
                throw new TemplateException("Array '" + arrayField.Name
                    + "' has no element field(s) (declare the repeated element in structFields).");
            }

            // A single sub-field array carries the bare scalar value per element (e.g. fixed[3] of i32 or
            // until-end of f32); a multi sub-field array carries a struct value-map per element.
            if (arrayField.StructFields.Count == 1)
            {
                FieldRecord el = arrayField.StructFields[0];
                if (el == null) throw new TemplateException("Array '" + arrayField.Name + "' has a null element field.");
                return DecodeField(el, cursor, new Dictionary<string, object>());
            }

            var inner = new Dictionary<string, object>();
            DecodeFields(arrayField.StructFields, cursor, inner);
            return inner;
        }

        private static long ReadPriorCount(string countFieldName, string arrayName,
            Dictionary<string, object> siblings)
        {
            if (string.IsNullOrEmpty(countFieldName))
            {
                throw new TemplateException("Count-from-prior array '" + arrayName
                    + "' does not name its count field.");
            }
            object raw;
            if (!siblings.TryGetValue(countFieldName, out raw))
            {
                throw new TemplateException("Count-from-prior array '" + arrayName
                    + "' references undefined prior field '" + countFieldName + "'.");
            }
            long count = AsLong(raw, countFieldName);
            if (count < 0)
            {
                throw new TemplateException("Count field '" + countFieldName + "' decoded a negative count.");
            }
            return count;
        }

        // ── encode walk ──────────────────────────────────────────────────────────

        private static void EncodeFields(List<FieldRecord> fields, Dictionary<string, object> values,
            Stream s)
        {
            foreach (FieldRecord f in fields)
            {
                if (f == null) throw new TemplateException("Template contains a null field record.");
                if (string.IsNullOrEmpty(f.Name)) throw new TemplateException("Template field is missing a name.");

                // D-10: if this field is the element-count source for a later count-from-prior array, write
                // the CURRENT element count of that array -- never the stale stored value. This is the only
                // genuinely new encode-parity obligation in the phase.
                FieldRecord boundArray = FindCountFromPriorArrayFor(f.Name, fields);
                if (boundArray != null)
                {
                    long liveCount = LiveElementCount(boundArray, values);
                    WriteIntegerField(f, liveCount, s);
                    continue;
                }

                EncodeField(f, GetValue(values, f.Name), s);
            }
        }

        private static void EncodeField(FieldRecord f, object value, Stream s)
        {
            switch (f.Type)
            {
                case KernelType.I8:
                case KernelType.U8:
                case KernelType.I16:
                case KernelType.U16:
                case KernelType.I32:
                case KernelType.U32:
                    WriteIntegerField(f, AsLong(value, f.Name), s);
                    break;
                case KernelType.F32:
                    WriteFloatLe(s, AsFloat(value, f.Name));
                    break;
                case KernelType.F64:
                    WriteDoubleLe(s, AsDouble(value, f.Name));
                    break;
                case KernelType.CString:
                    WriteCString(s, AsString(value, f.Name), EncodingFor(f));
                    break;
                case KernelType.FixedChar:
                    WriteFixedSpan(f, value, RequireWidth(f, "FixedChar"), s);
                    break;
                case KernelType.RawBytes:
                    WriteFixedSpan(f, value, RequireWidth(f, "RawBytes"), s);
                    break;
                case KernelType.Pad:
                    WriteFixedSpan(f, value, RequireWidth(f, "Pad"), s);
                    break;
                case KernelType.Struct:
                    EncodeStruct(f, value, s);
                    break;
                case KernelType.Array:
                    EncodeArray(f, value, s);
                    break;
                default:
                    throw new TemplateException("Unsupported kernel type: " + f.Type);
            }
        }

        private static void EncodeStruct(FieldRecord f, object value, Stream s)
        {
            if (f.StructFields == null || f.StructFields.Count == 0)
            {
                throw new TemplateException("Struct field '" + f.Name + "' has no sub-fields.");
            }
            Dictionary<string, object> inner = AsMap(value, f.Name);
            EncodeFields(f.StructFields, inner, s);
        }

        private static void EncodeArray(FieldRecord f, object value, Stream s)
        {
            if (f.StructFields == null || f.StructFields.Count == 0)
            {
                throw new TemplateException("Array '" + f.Name + "' has no element field(s).");
            }
            List<object> elements = AsList(value, f.Name);
            foreach (object el in elements)
            {
                if (f.StructFields.Count == 1)
                {
                    EncodeField(f.StructFields[0], el, s);
                }
                else
                {
                    EncodeFields(f.StructFields, AsMap(el, f.Name + " element"), s);
                }
            }
        }

        // Writes an integer field's value at its on-wire width. The signed/unsigned distinction is purely
        // a display concern (D-11) -- the wire bytes are identical -- so encode emits the same LE bytes for
        // a signed or unsigned field of the same width.
        private static void WriteIntegerField(FieldRecord f, long value, Stream s)
        {
            switch (f.Type)
            {
                // CR-02: range-check BEFORE writing so a too-large value (a count auto-recompute that
                // outgrows its on-wire width, or a user --set that exceeds the field width) fails LOUD
                // with a typed TemplateException rather than silently truncating and corrupting the file.
                case KernelType.I8:
                    RequireRange(f, value, sbyte.MinValue, sbyte.MaxValue);
                    s.WriteByte(unchecked((byte)value));
                    break;
                case KernelType.U8:
                    RequireRange(f, value, byte.MinValue, byte.MaxValue);
                    s.WriteByte(unchecked((byte)value));
                    break;
                case KernelType.I16:
                    RequireRange(f, value, short.MinValue, short.MaxValue);
                    WriteInt16Le(s, value);
                    break;
                case KernelType.U16:
                    RequireRange(f, value, ushort.MinValue, ushort.MaxValue);
                    WriteInt16Le(s, value);
                    break;
                case KernelType.I32:
                    RequireRange(f, value, int.MinValue, int.MaxValue);
                    WriteInt32Le(s, value);
                    break;
                case KernelType.U32:
                    RequireRange(f, value, uint.MinValue, uint.MaxValue);
                    WriteInt32Le(s, value);
                    break;
                default:
                    throw new TemplateException("Field '" + f.Name + "' is not an integer kernel type ("
                        + f.Type + ") but was asked to encode an integer (count field?).");
            }
        }

        // CR-02: throws a typed TemplateException when `value` does not fit the [min, max] range of the
        // field's declared on-wire integer width. The bounds are passed as long (the widest unsigned bound,
        // uint.MaxValue, fits a long) so the comparison is exact for every kernel integer width.
        private static void RequireRange(FieldRecord f, long value, long min, long max)
        {
            if (value < min || value > max)
            {
                throw new TemplateException("count/value " + value + " does not fit field '" + f.Name
                    + "' (" + f.Type + ").");
            }
        }

        // ── LE / cstring primitive writers (host-endian-safe, mirroring ClefFieldCodec) ──

        private static void WriteInt16Le(Stream s, long value)
        {
            byte[] two = new byte[2];
            two[0] = (byte)(value & 0xFF);
            two[1] = (byte)((value >> 8) & 0xFF);
            s.Write(two, 0, 2);
        }

        private static void WriteInt32Le(Stream s, long value)
        {
            byte[] four = new byte[4];
            four[0] = (byte)(value & 0xFF);
            four[1] = (byte)((value >> 8) & 0xFF);
            four[2] = (byte)((value >> 16) & 0xFF);
            four[3] = (byte)((value >> 24) & 0xFF);
            s.Write(four, 0, 4);
        }

        private static void WriteFloatLe(Stream s, float value)
        {
            byte[] four = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(four);
            s.Write(four, 0, four.Length);
        }

        private static void WriteDoubleLe(Stream s, double value)
        {
            byte[] eight = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(eight);
            s.Write(eight, 0, eight.Length);
        }

        private static void WriteCString(Stream s, string value, Encoding encoding)
        {
            if (value == null) value = string.Empty;
            byte[] bytes = encoding.GetBytes(value);
            s.Write(bytes, 0, bytes.Length);
            s.WriteByte(0x00); // NUL terminator, NO length prefix
        }

        // FixedChar / RawBytes / Pad all round-trip via the captured raw n-byte span (WR-04: FixedChar is
        // an OPAQUE byte span, decoded via cursor.ReadRawBytes(n), never a string): re-emit the exact bytes
        // that were read so the on-disk fixed-width field is byte-exact (NUL padding preserved). A short
        // span is right-NUL-padded to the declared width; an over-long span is rejected.
        private static void WriteFixedSpan(FieldRecord f, object value, int width, Stream s)
        {
            byte[] span = value as byte[];
            if (span == null)
            {
                throw new TemplateException("Field '" + f.Name + "' (" + f.Type
                    + ") expects a raw byte span value for byte-exact re-emit.");
            }
            if (span.Length > width)
            {
                throw new TemplateException("Field '" + f.Name + "' span length " + span.Length
                    + " exceeds its declared width " + width + ".");
            }
            s.Write(span, 0, span.Length);
            for (int i = span.Length; i < width; i++) s.WriteByte(0x00);
        }

        // ── count-from-prior plumbing (D-10) ──────────────────────────────────────

        // Returns the count-from-prior ARRAY field (within the same field list) whose count source is the
        // named field, or null if no array binds to it. Field order is authoritative: a count field always
        // precedes the array it sizes.
        private static FieldRecord FindCountFromPriorArrayFor(string countFieldName, List<FieldRecord> fields)
        {
            foreach (FieldRecord f in fields)
            {
                if (f != null && f.Type == KernelType.Array && f.Repeat != null
                    && f.Repeat.Kind == RepeatKind.CountFromField
                    && f.Repeat.CountFieldName == countFieldName)
                {
                    return f;
                }
            }
            return null;
        }

        private static long LiveElementCount(FieldRecord arrayField, Dictionary<string, object> values)
        {
            object raw;
            if (!values.TryGetValue(arrayField.Name, out raw))
            {
                throw new TemplateException("Count-from-prior array '" + arrayField.Name
                    + "' has no value to count on encode.");
            }
            return AsList(raw, arrayField.Name).Count;
        }

        // ── value coercion helpers ─────────────────────────────────────────────────

        private static object GetValue(Dictionary<string, object> values, string name)
        {
            object v;
            if (!values.TryGetValue(name, out v))
            {
                throw new TemplateException("No value provided for field '" + name + "' on encode.");
            }
            return v;
        }

        private static long AsLong(object value, string name)
        {
            if (value is long) return (long)value;
            if (value is int) return (int)value;
            if (value is short) return (short)value;
            if (value is sbyte) return (sbyte)value;
            if (value is byte) return (byte)value;
            if (value is ushort) return (ushort)value;
            if (value is uint) return (uint)value;
            throw new TemplateException("Field '" + name + "' expected an integer value, got "
                + (value == null ? "null" : value.GetType().Name) + ".");
        }

        private static float AsFloat(object value, string name)
        {
            if (value is float) return (float)value;
            if (value is double) return (float)(double)value;
            throw new TemplateException("Field '" + name + "' expected an f32 value, got "
                + (value == null ? "null" : value.GetType().Name) + ".");
        }

        private static double AsDouble(object value, string name)
        {
            if (value is double) return (double)value;
            if (value is float) return (float)value;
            throw new TemplateException("Field '" + name + "' expected an f64 value, got "
                + (value == null ? "null" : value.GetType().Name) + ".");
        }

        private static string AsString(object value, string name)
        {
            if (value == null) return string.Empty;
            string s = value as string;
            if (s != null) return s;
            throw new TemplateException("Field '" + name + "' expected a string value, got "
                + value.GetType().Name + ".");
        }

        private static Dictionary<string, object> AsMap(object value, string name)
        {
            var map = value as Dictionary<string, object>;
            if (map == null)
            {
                throw new TemplateException("Field '" + name + "' expected a struct value map, got "
                    + (value == null ? "null" : value.GetType().Name) + ".");
            }
            return map;
        }

        private static List<object> AsList(object value, string name)
        {
            var list = value as List<object>;
            if (list == null)
            {
                throw new TemplateException("Array '" + name + "' expected an element list, got "
                    + (value == null ? "null" : value.GetType().Name) + ".");
            }
            return list;
        }

        private static int RequireWidth(FieldRecord f, string kind)
        {
            if (f.ByteWidth <= 0)
            {
                throw new TemplateException(kind + " field '" + f.Name
                    + "' requires a positive byteWidth (got " + f.ByteWidth + ").");
            }
            return f.ByteWidth;
        }

        private static Encoding EncodingFor(FieldRecord f)
        {
            string name = string.IsNullOrEmpty(f.Encoding) ? TemplateModelDefaults.DefaultEncoding : f.Encoding;
            try
            {
                return Encoding.GetEncoding(name);
            }
            catch (ArgumentException)
            {
                throw new TemplateException("Field '" + f.Name + "' names an unknown encoding '" + name + "'.");
            }
        }
    }
}
