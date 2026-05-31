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
// (SOE/Bootprint, All Rights Reserved): each param chunk is a NUL-terminated field name followed by
// a 1-byte data-type tag (NONE/SINGLE/WEIGHTED_LIST/RANGE/DIE_ROLL); numeric (int/float) params then
// carry a 1-byte delta-type byte (' '/'+'/'-') before the value. Only the on-disk layout was studied
// — no code, comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.

using System;

namespace UtinniCoreDotNet.Formats.ObjectTemplate
{
    /// <summary>
    /// The 1-byte self-describing data-type tag that leads every object-template param value
    /// (after the NUL-terminated field name). The tag tells the generic decoder the value's shape
    /// with NO external <c>.tdf</c> schema (D-03).
    /// </summary>
    public enum ObjectTemplateDataTypeTag
    {
        /// <summary>Param explicitly cleared — no value bytes follow.</summary>
        None = 0,
        /// <summary>One value follows.</summary>
        Single = 1,
        /// <summary>int32 count, then count × (int32 weight + nested value). Hex-fallback in V1.</summary>
        WeightedList = 2,
        /// <summary>Two values follow (min, max) — numeric types only. Hex-fallback in V1.</summary>
        Range = 3,
        /// <summary>Three int32 follow (num_dice, die_sides, base) — integer only. Hex-fallback in V1.</summary>
        DieRoll = 4
    }

    /// <summary>
    /// Discriminates an <see cref="ObjectTemplateParamValue"/> concrete variant without an
    /// out-of-band reflection check (the editor's per-type value widget switches on this).
    /// </summary>
    public enum ObjectTemplateParamKind
    {
        /// <summary>A typed <see cref="bool"/> SINGLE value.</summary>
        Bool,
        /// <summary>A typed <see cref="int"/> SINGLE value (delta byte preserved).</summary>
        Int,
        /// <summary>A typed <see cref="float"/> SINGLE value (delta byte preserved).</summary>
        Float,
        /// <summary>A typed NUL-terminated string SINGLE value.</summary>
        String,
        /// <summary>An explicitly-cleared value (data-type tag NONE; no payload).</summary>
        None,
        /// <summary>
        /// Verbatim value bytes the generic decoder could not safely type (WEIGHTED_LIST / RANGE /
        /// DIE_ROLL / struct / dynamic-variable, or any scalar decode that did not consume the chunk
        /// payload exactly). Edited via the Phase 8 hex/text leaf editor; never mis-typed.
        /// </summary>
        RawBytesHexFallback
    }

    /// <summary>
    /// Discriminated value carrier for a single object-template param — the managed analog of the
    /// per-param self-describing value the engine loaders read. The closed variant set is:
    /// <see cref="ObjectTemplateParamKind.Bool"/> / <see cref="ObjectTemplateParamKind.Int"/> /
    /// <see cref="ObjectTemplateParamKind.Float"/> / <see cref="ObjectTemplateParamKind.String"/> /
    /// <see cref="ObjectTemplateParamKind.None"/> / <see cref="ObjectTemplateParamKind.RawBytesHexFallback"/>.
    ///
    /// <para><b>Defensive posture (RESEARCH-locked, D-02):</b> only SINGLE int/float/bool/string are
    /// surfaced as typed scalars in V1. WEIGHTED_LIST / RANGE / DIE_ROLL / struct / dynamic-variable
    /// shapes — and any scalar decode that does not consume the chunk payload exactly — are carried
    /// as <see cref="ObjectTemplateParamKind.RawBytesHexFallback"/> so they round-trip byte-for-byte
    /// and are never mis-typed.</para>
    ///
    /// <para><b>Delta byte (numeric only):</b> Int and Float values carry the verbatim 1-byte
    /// <see cref="DeltaType"/> (<c>' '</c> absolute, <c>'+'</c>/<c>'-'</c> delta-on-base) which the
    /// codec preserves on encode. Bool/String/None carry no delta byte.</para>
    /// </summary>
    public sealed class ObjectTemplateParamValue
    {
        private readonly bool _boolValue;
        private readonly int _intValue;
        private readonly float _floatValue;
        private readonly string _stringValue;
        private readonly byte[] _rawBytes;

        private ObjectTemplateParamValue(
            ObjectTemplateParamKind kind,
            ObjectTemplateDataTypeTag tag,
            byte? deltaType,
            bool boolValue,
            int intValue,
            float floatValue,
            string stringValue,
            byte[] rawBytes)
        {
            Kind = kind;
            DataTypeTag = tag;
            DeltaType = deltaType;
            _boolValue = boolValue;
            _intValue = intValue;
            _floatValue = floatValue;
            _stringValue = stringValue;
            _rawBytes = rawBytes;
        }

        /// <summary>The concrete variant discriminator.</summary>
        public ObjectTemplateParamKind Kind { get; }

        /// <summary>The self-describing data-type tag this value decoded from.</summary>
        public ObjectTemplateDataTypeTag DataTypeTag { get; }

        /// <summary>
        /// The verbatim 1-byte delta-type (<c>' '</c>/<c>'+'</c>/<c>'-'</c>) for numeric params, or
        /// null for Bool/String/None/RawBytesHexFallback values. Preserved byte-for-byte on encode.
        /// </summary>
        public byte? DeltaType { get; }

        /// <summary>Creates a typed bool SINGLE value (no delta byte).</summary>
        public static ObjectTemplateParamValue FromBool(bool value)
        {
            return new ObjectTemplateParamValue(
                ObjectTemplateParamKind.Bool, ObjectTemplateDataTypeTag.Single, null,
                value, 0, 0f, null, null);
        }

        /// <summary>Creates a typed int SINGLE value carrying the verbatim delta byte.</summary>
        public static ObjectTemplateParamValue FromInt(int value, byte deltaType)
        {
            return new ObjectTemplateParamValue(
                ObjectTemplateParamKind.Int, ObjectTemplateDataTypeTag.Single, deltaType,
                false, value, 0f, null, null);
        }

        /// <summary>Creates a typed float SINGLE value carrying the verbatim delta byte.</summary>
        public static ObjectTemplateParamValue FromFloat(float value, byte deltaType)
        {
            return new ObjectTemplateParamValue(
                ObjectTemplateParamKind.Float, ObjectTemplateDataTypeTag.Single, deltaType,
                false, 0, value, null, null);
        }

        /// <summary>Creates a typed string SINGLE value (no delta byte). Null is coerced to empty.</summary>
        public static ObjectTemplateParamValue FromString(string value)
        {
            return new ObjectTemplateParamValue(
                ObjectTemplateParamKind.String, ObjectTemplateDataTypeTag.Single, null,
                false, 0, 0f, value ?? string.Empty, null);
        }

        /// <summary>Creates an explicitly-cleared value (data-type tag NONE; no payload bytes).</summary>
        public static ObjectTemplateParamValue FromNone()
        {
            return new ObjectTemplateParamValue(
                ObjectTemplateParamKind.None, ObjectTemplateDataTypeTag.None, null,
                false, 0, 0f, null, null);
        }

        /// <summary>
        /// Creates a raw-bytes hex-fallback value carrying the verbatim value-region bytes (everything
        /// after the field-name NUL terminator). The originating data-type tag is recorded for the UI
        /// label; the bytes round-trip byte-for-byte on encode.
        /// </summary>
        public static ObjectTemplateParamValue FromRawBytes(byte[] valueBytes, ObjectTemplateDataTypeTag tag)
        {
            byte[] copy = (valueBytes == null) ? new byte[0] : (byte[])valueBytes.Clone();
            return new ObjectTemplateParamValue(
                ObjectTemplateParamKind.RawBytesHexFallback, tag, null,
                false, 0, 0f, null, copy);
        }

        /// <summary>The wrapped bool (Bool variant only).</summary>
        public bool BoolValue
        {
            get
            {
                RequireKind(ObjectTemplateParamKind.Bool);
                return _boolValue;
            }
        }

        /// <summary>The wrapped int (Int variant only).</summary>
        public int IntValue
        {
            get
            {
                RequireKind(ObjectTemplateParamKind.Int);
                return _intValue;
            }
        }

        /// <summary>The wrapped float (Float variant only).</summary>
        public float FloatValue
        {
            get
            {
                RequireKind(ObjectTemplateParamKind.Float);
                return _floatValue;
            }
        }

        /// <summary>The wrapped string (String variant only; never null).</summary>
        public string StringValue
        {
            get
            {
                RequireKind(ObjectTemplateParamKind.String);
                return _stringValue;
            }
        }

        /// <summary>Returns a defensive copy of the verbatim value bytes (RawBytesHexFallback only).</summary>
        public byte[] GetRawBytesCopy()
        {
            RequireKind(ObjectTemplateParamKind.RawBytesHexFallback);
            return (byte[])_rawBytes.Clone();
        }

        /// <summary>
        /// The UI Type-column label per the 11-UI-SPEC per-type value widget contract:
        /// <c>bool</c>/<c>int</c>/<c>float</c>/<c>string</c>/<c>(none)</c>/<c>raw bytes (hex)</c>.
        /// </summary>
        public string ParamTypeLabel
        {
            get
            {
                switch (Kind)
                {
                    case ObjectTemplateParamKind.Bool: return "bool";
                    case ObjectTemplateParamKind.Int: return "int";
                    case ObjectTemplateParamKind.Float: return "float";
                    case ObjectTemplateParamKind.String: return "string";
                    case ObjectTemplateParamKind.None: return "(none)";
                    case ObjectTemplateParamKind.RawBytesHexFallback: return "raw bytes (hex)";
                    default: return "(unknown)";
                }
            }
        }

        private void RequireKind(ObjectTemplateParamKind expected)
        {
            if (Kind != expected)
            {
                throw new InvalidOperationException(
                    "Param value is a " + Kind + " variant, not " + expected + ".");
            }
        }
    }
}
