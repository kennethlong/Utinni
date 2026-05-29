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
// DTII (.tab datatable) format understood by reading
// swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/{DataTable,DataTableColumnType,
// DataTableCell,DataTableWriter}.{h,cpp} (SOE/Bootprint, All Rights Reserved). No code, comments,
// identifier names, or test fixtures copied from any reference source. Implementation original to
// Utinni under MIT.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace UtinniCoreDotNet.Formats.Datatable
{
    /// <summary>
    /// Datatable column data types. Ordinals match the SOE <c>DataTableColumnType::DataType</c> enum
    /// verbatim (<c>DataTableColumnType.h:24-38</c>) so the V0000 TYPE chunk's <c>int32</c> ordinals
    /// map straight across.
    /// </summary>
    public enum DataType
    {
        /// <summary>Signed 32-bit integer (DT_Int = 0).</summary>
        Int = 0,
        /// <summary>32-bit float (DT_Float = 1).</summary>
        Float = 1,
        /// <summary>NUL-terminated ASCII string (DT_String = 2).</summary>
        String = 2,
        /// <summary>Unknown / unsupported (DT_Unknown = 3); read-only hex fallback in the editor.</summary>
        Unknown = 3,
        /// <summary>Comment column (DT_Comment = 4); zero-byte ROWS payload per cell.</summary>
        Comment = 4,
        /// <summary>CRC-hashed string stored as int32 (DT_HashString = 5); basic type DT_Int.</summary>
        HashString = 5,
        /// <summary>Label-to-int enumeration stored as int32 (DT_Enum = 6); basic type DT_Int.</summary>
        Enum = 6,
        /// <summary>Boolean stored as int32 0/1 (DT_Bool = 7); basic type DT_Int.</summary>
        Bool = 7,
        /// <summary>Packed objvar string (DT_PackedObjVars = 8); basic type DT_String.</summary>
        PackedObjVars = 8,
        /// <summary>Bit-flag vector stored as int32 (DT_BitVector = 9); basic type DT_Int.</summary>
        BitVector = 9
    }

    /// <summary>
    /// Typed parse of a DTII column's type-spec string and the value-coercion (MangleValue) the
    /// editor applies before serialization. Managed port of SOE
    /// <c>DataTableColumnType</c> (<c>DataTableColumnType.{h,cpp}</c>).
    ///
    /// <remarks>
    /// <para><b>Type-spec grammar</b> (port of <c>DataTableColumnType.cpp:84-232</c>, the
    /// <c>_parseDescription</c> constructor body): the first character is the type discriminator —
    /// <c>i</c> Int, <c>f</c> Float, <c>s</c> String, <c>c</c> Comment, <c>h</c> HashString,
    /// <c>p</c> PackedObjVars, <c>b</c> Bool, <c>e</c> Enum, <c>v</c> BitVector,
    /// <c>z</c> cross-table-Enum (V1-out-of-scope, treated as Unknown per CONTEXT D-05 /
    /// UI-SPEC R-03). The optional <c>[default]</c> group is extracted with the SOE
    /// <c>getDelimStr(desc, '[', ']')</c> semantic; the <c>(label=val,…)</c> group (required for
    /// <c>e</c>/<c>v</c>) is extracted with <c>getDelimStr(desc, '(', ')')</c>. Enum/bitvector
    /// grammar is <c>e(a=0,b=1,c=2)[default]</c> (UI-SPEC § Per-type cell widget contract, corrected
    /// per Assumption A1 in 09-01 Task 0). Any other first character maps to Unknown basic type.</para>
    ///
    /// <para><b>MangleValue</b> port: <c>DataTableColumnType.cpp:382-473</c>.</para>
    /// </remarks>
    /// </summary>
    public sealed class DataTableColumnType
    {
        private readonly string typeSpec;
        private readonly Dictionary<string, int> enumMap;

        /// <summary>The discriminated data type for this column.</summary>
        public DataType Type { get; }

        /// <summary>
        /// The basic on-disk type used for ROWS serialization: DT_Int for Bool/HashString/Enum/
        /// BitVector; DT_String for PackedObjVars; DT_Unknown for a malformed/unsupported spec;
        /// otherwise the same as <see cref="Type"/>.
        /// </summary>
        public DataType BasicType { get; private set; }

        /// <summary>The string default value (empty if none); sentinels "required"/"unique" preserved verbatim.</summary>
        public string DefaultValue { get; }

        /// <summary>The original type-spec string (re-emitted verbatim into the V0001 TYPE chunk).</summary>
        public string TypeSpec
        {
            get { return typeSpec; }
        }

        /// <summary>
        /// Label-to-value map for DT_Enum / DT_BitVector (bit values are pre-shifted to
        /// <c>1 &lt;&lt; (bit-1)</c> for bitvectors per SOE). Empty for all other types.
        /// </summary>
        public IReadOnlyDictionary<string, int> EnumMap
        {
            get { return enumMap; }
        }

        /// <summary>True iff the column requires unique cell values (default value sentinel "unique").</summary>
        public bool AreUniqueCellsRequired
        {
            get { return DefaultValue == "unique"; }
        }

        /// <summary>
        /// Parses the given type-spec string. Throws <see cref="DataTableParseException"/> on a null
        /// or empty spec. An unknown discriminator does NOT throw — it yields a column with
        /// <see cref="BasicType"/> == <see cref="DataType.Unknown"/> (mirroring SOE, which sets
        /// m_basicType = DT_Unknown for an unrecognized first char).
        /// </summary>
        public DataTableColumnType(string typeSpecString)
        {
            if (string.IsNullOrEmpty(typeSpecString))
            {
                throw new DataTableParseException("Datatable column type-spec is null or empty.");
            }

            typeSpec = typeSpecString;
            enumMap = new Dictionary<string, int>(StringComparer.Ordinal);

            // Default-value group lives between the first '[' and the last ']'.
            DefaultValue = GetDelimStr(typeSpecString, '[', ']');

            char disc = ToLowerAscii(typeSpecString[0]);
            switch (disc)
            {
                case 'i':
                    Type = BasicType = DataType.Int;
                    if (DefaultValue.Length == 0) DefaultValue = "0";
                    break;
                case 'f':
                    Type = BasicType = DataType.Float;
                    if (DefaultValue.Length == 0) DefaultValue = "0";
                    break;
                case 's':
                    Type = BasicType = DataType.String;
                    break;
                case 'c':
                    Type = BasicType = DataType.Comment;
                    break;
                case 'h':
                    Type = DataType.HashString;
                    BasicType = DataType.Int;
                    break;
                case 'p':
                    Type = DataType.PackedObjVars;
                    BasicType = DataType.String;
                    break;
                case 'b':
                    Type = DataType.Bool;
                    BasicType = DataType.Int;
                    if (DefaultValue != "1") DefaultValue = "0";
                    break;
                case 'e':
                    Type = DataType.Enum;
                    BasicType = DataType.Int;
                    BuildEnumMap(typeSpecString, isBitVector: false);
                    break;
                case 'v':
                    Type = DataType.BitVector;
                    BasicType = DataType.Int;
                    BuildEnumMap(typeSpecString, isBitVector: true);
                    break;
                case 'z':
                    // Cross-table enum — V1 out of scope (CONTEXT D-05). Parse the discriminator but
                    // expose Type = Unknown so the cell widget falls back to read-only hex render
                    // (UI-SPEC R-03). The actual enum table is not resolvable in the editor.
                    Type = DataType.Unknown;
                    BasicType = DataType.Unknown;
                    break;
                default:
                    Type = DataType.Unknown;
                    BasicType = DataType.Unknown;
                    break;
            }
        }

        /// <summary>
        /// Coerces a raw cell value string toward its on-disk form, returning <c>false</c> if the
        /// value is invalid for this column type. Port of <c>DataTableColumnType.cpp:382-473</c>:
        /// <list type="bullet">
        ///   <item>empty value → set to default and return true, UNLESS the default is the
        ///         "required"/"unique" sentinel (then return false)</item>
        ///   <item>DT_PackedObjVars → validate the <c>name|type|value|…|$|</c> grammar</item>
        ///   <item>DT_Bool → accept only "0" or "1"</item>
        ///   <item>DT_HashString → replace with the int32 CRC of the value</item>
        ///   <item>DT_Enum / DT_BitVector → replace with the looked-up int (miss → false)</item>
        ///   <item>any other basic type → leave unchanged and return true</item>
        /// </list>
        /// </summary>
        public bool MangleValue(ref string value)
        {
            if (value == null) value = string.Empty;

            if (value.Length == 0)
            {
                if (DefaultValue == "required" || DefaultValue == "unique")
                {
                    return false;
                }

                value = DefaultValue;
            }

            if (Type == DataType.PackedObjVars)
            {
                if (!ValidatePackedObjVars(value))
                {
                    return false;
                }
            }

            // Only DT_Int basic-typed complex types need mangling beyond the default substitution.
            // (DT_Int itself, and all DT_String/DT_Float/DT_Comment, pass through unchanged.)
            if (BasicType != DataType.Int || Type == DataType.Int)
            {
                return true;
            }

            switch (Type)
            {
                case DataType.Bool:
                    return value == "0" || value == "1";

                case DataType.HashString:
                {
                    uint crc = value.Length != 0 ? DataTableHashCrc.Compute(value) : 0u;
                    // SOE stores the CRC as a signed int via sprintf("%d") — mirror the round-trip
                    // exactly so the on-disk int32 matches the reference client byte-for-byte.
                    value = unchecked((int)crc).ToString(CultureInfo.InvariantCulture);
                    return true;
                }

                case DataType.Enum:
                {
                    int mapped;
                    if (LookupEnum(value, out mapped))
                    {
                        value = mapped.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }

                    return false;
                }

                case DataType.BitVector:
                {
                    int mapped;
                    if (LookupBitVector(value, out mapped))
                    {
                        value = mapped.ToString(CultureInfo.InvariantCulture);
                        return true;
                    }

                    return false;
                }

                default:
                    return false;
            }
        }

        /// <summary>
        /// Coerces a raw editor string toward this column's typed on-disk value: runs
        /// <see cref="MangleValue"/> (which substitutes defaults and maps Bool/Hash/Enum/BitVector to
        /// their int32 form), then constructs the <see cref="DataTableCellValue"/> for this column's
        /// <see cref="BasicType"/>. Returns <c>false</c> (and leaves <paramref name="result"/> null) on
        /// a mangle failure or an unparseable numeric. Reusable by Plan 09-02's <c>roundtrip-tab
        /// --mutate-cell</c>, Plan 09-04's EditCellValue, and Plan 09-06's ApplyCsvImport.
        /// </summary>
        public bool TryCoerceCellValue(string raw, out DataTableCellValue result)
        {
            result = null;
            string mangled = raw ?? string.Empty;
            if (!MangleValue(ref mangled))
            {
                return false;
            }

            switch (BasicType)
            {
                case DataType.Int:
                {
                    int v;
                    if (!int.TryParse(mangled, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                    {
                        return false;
                    }

                    result = DataTableCellValue.FromInt(v);
                    return true;
                }

                case DataType.Float:
                {
                    float v;
                    if (!float.TryParse(mangled, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    {
                        return false;
                    }

                    result = DataTableCellValue.FromFloat(v);
                    return true;
                }

                case DataType.String:
                    result = DataTableCellValue.FromString(mangled);
                    return true;

                case DataType.Comment:
                    // Comment cells carry no on-disk value; model as empty string.
                    result = DataTableCellValue.FromString(string.Empty);
                    return true;

                default:
                    // DT_Unknown (e.g. cross-table enum 'z' or a malformed spec) is not editable.
                    return false;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Enum / bitvector map construction (DataTableColumnType.cpp:136-194)
        // ──────────────────────────────────────────────────────────────────────

        private void BuildEnumMap(string desc, bool isBitVector)
        {
            string enumList = GetDelimStr(desc, '(', ')');
            // SOE appends a trailing ',' then walks "label=val," tuples.
            enumList += ",";
            int searchFrom = 0;
            while (true)
            {
                int eqPos = enumList.IndexOf('=', searchFrom);
                if (eqPos < 0) break;
                int commaPos = enumList.IndexOf(',', eqPos);
                if (commaPos < 0) break;

                string label = enumList.Substring(searchFrom, eqPos - searchFrom);
                string valStr = enumList.Substring(eqPos + 1, commaPos - eqPos - 1);
                int parsed = ParseCInt(valStr);

                if (isBitVector)
                {
                    if (parsed < 1 || parsed > 32)
                    {
                        // SOE WARNINGs and marks the basic type Unknown for an out-of-range bit.
                        BasicType = DataType.Unknown;
                    }
                    else
                    {
                        enumMap[label] = 1 << (parsed - 1);
                    }
                }
                else
                {
                    enumMap[label] = parsed;
                }

                searchFrom = commaPos + 1;
            }

            // Assure the default is a member of the enumeration (SOE marks Unknown otherwise).
            // BitVector allows the "NONE" sentinel default per DataTableColumnType.cpp:186.
            if (isBitVector && DefaultValue == "NONE")
            {
                return;
            }

            if (DefaultValue.Length > 0 && !enumMap.ContainsKey(DefaultValue))
            {
                BasicType = DataType.Unknown;
            }
        }

        // Lookup a single enum label (DataTableColumnType.cpp:331-344).
        private bool LookupEnum(string label, out int result)
        {
            result = 0;
            string trimmed = Chomp(label);
            int val;
            if (enumMap.TryGetValue(trimmed, out val))
            {
                result = val;
                return true;
            }

            return false;
        }

        // Lookup a comma-separated set of bitvector labels, OR-ing their bits
        // (DataTableColumnType.cpp:348-378). "NONE" → 0. Returns true iff at least one matched.
        private bool LookupBitVector(string label, out int result)
        {
            result = 0;
            if (label == "NONE")
            {
                return true;
            }

            bool foundAny = false;
            string list = label + ",";
            int searchFrom = 0;
            while (true)
            {
                int commaPos = list.IndexOf(',', searchFrom);
                if (commaPos < 0) break;

                string sub = Chomp(list.Substring(searchFrom, commaPos - searchFrom));
                int subResult;
                if (sub.Length > 0 && LookupEnum(sub, out subResult))
                {
                    foundAny = true;
                    result |= subResult;
                }

                searchFrom = commaPos + 1;
            }

            return foundAny;
        }

        // ──────────────────────────────────────────────────────────────────────
        // PackedObjVars validator (DataTableColumnType.cpp:44-69, :394-414)
        // ──────────────────────────────────────────────────────────────────────

        // Packed objvars: name|type|value|name|type|value|...|$| where '|' separates fields.
        private static bool ValidatePackedObjVars(string value)
        {
            int pos = 0;
            int len = value.Length;
            while (pos < len)
            {
                // Sentinel "$|" terminating the list (SOE: s[0]=='$' && s[1]=='|' && s[2]=='\0').
                if (value[pos] == '$' && pos + 1 < len && value[pos + 1] == '|' && pos + 2 == len)
                {
                    break;
                }

                if (!ConsumeStringField(value, ref pos)) return false; // name
                if (!ConsumeIntField(value, ref pos)) return false;    // type
                if (!ConsumeStringField(value, ref pos)) return false; // value
            }

            return true;
        }

        // Port of consumePackedObjVarStringField: advance past characters up to and including a '|'.
        private static bool ConsumeStringField(string s, ref int pos)
        {
            while (pos < s.Length)
            {
                char c = s[pos++];
                if (c == '|')
                {
                    return true;
                }
            }

            return false;
        }

        // Port of consumePackedObjVarIntField: digits (and a leading '-') up to and including a '|'.
        private static bool ConsumeIntField(string s, ref int pos)
        {
            while (pos < s.Length)
            {
                char c = s[pos];
                pos++;
                if (c == '|')
                {
                    return true;
                }

                if (c != '-' && !(c >= '0' && c <= '9'))
                {
                    return false;
                }
            }

            return false;
        }

        // ──────────────────────────────────────────────────────────────────────
        // String helpers mirroring the SOE statics (getDelimStr, chomp, strtol semantics)
        // ──────────────────────────────────────────────────────────────────────

        // Port of getDelimStr (DataTableColumnType.cpp:31-40): substring between the FIRST left
        // delimiter and the LAST right delimiter, or "" if either is absent.
        private static string GetDelimStr(string str, char left, char right)
        {
            int lPos = str.IndexOf(left);
            int rPos = str.LastIndexOf(right);
            if (lPos < 0 || rPos < 0 || rPos <= lPos)
            {
                return string.Empty;
            }

            return str.Substring(lPos + 1, rPos - lPos - 1);
        }

        // Port of chomp (DataTableColumnType.cpp:21-29): trim leading/trailing spaces only.
        private static string Chomp(string str)
        {
            return str.Trim(' ');
        }

        // strtol(str, NULL, 0) semantics: base auto-detect (0x → hex, leading 0 → octal, else
        // decimal). Mirrors how SOE parses enum/bitvector values. Returns 0 on a non-numeric string.
        private static int ParseCInt(string s)
        {
            s = (s ?? string.Empty).Trim();
            if (s.Length == 0) return 0;

            bool negative = false;
            int idx = 0;
            if (s[0] == '+' || s[0] == '-')
            {
                negative = s[0] == '-';
                idx = 1;
            }

            string body = s.Substring(idx);
            long magnitude;
            try
            {
                if (body.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && body.Length > 2)
                {
                    magnitude = Convert.ToInt64(body.Substring(2), 16);
                }
                else if (body.Length > 1 && body[0] == '0')
                {
                    magnitude = Convert.ToInt64(body, 8);
                }
                else
                {
                    magnitude = long.Parse(body, CultureInfo.InvariantCulture);
                }
            }
            catch (FormatException)
            {
                return 0;
            }
            catch (OverflowException)
            {
                return 0;
            }

            long result = negative ? -magnitude : magnitude;
            return unchecked((int)result);
        }

        private static char ToLowerAscii(char c)
        {
            if (c >= 'A' && c <= 'Z')
            {
                return (char)(c + 32);
            }

            return c;
        }
    }
}
