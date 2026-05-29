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
using System.Globalization;
using System.IO;
using System.Text;

namespace UtinniCoreDotNet.Formats.Datatable
{
    /// <summary>
    /// Discriminated-union value held by a datatable cell — the managed analog of the SOE
    /// <c>DataTableCell</c> union (<c>DataTableCell.h:47-59</c>: int / float / string). Shipped as a
    /// sealed abstract base with three concrete subclasses (<see cref="IntCellValue"/> /
    /// <see cref="FloatCellValue"/> / <see cref="StringCellValue"/>) rather than a struct: the
    /// subclass hierarchy lets <see cref="SerializeFresh"/> and the editor cell widgets pattern-match
    /// on the concrete type without an out-of-band discriminator enum, and value-equality is
    /// implemented per subclass via <see cref="Equals(DataTableCellValue, DataTableCellValue)"/>.
    ///
    /// <para>The on-disk basic type is decided by the column's
    /// <see cref="DataTableColumnType.BasicType"/>, NOT by this value's concrete subclass — e.g. a
    /// DT_Bool / DT_Enum / DT_HashString column stores an <see cref="IntCellValue"/>, a
    /// DT_PackedObjVars column stores a <see cref="StringCellValue"/>.</para>
    /// </summary>
    public abstract class DataTableCellValue
    {
        // Closed hierarchy — only the three concrete subclasses below.
        private protected DataTableCellValue()
        {
        }

        /// <summary>Wraps an <see cref="int"/> (DT_Int basic type).</summary>
        public static DataTableCellValue FromInt(int value)
        {
            return new IntCellValue(value);
        }

        /// <summary>Wraps a <see cref="float"/> (DT_Float basic type).</summary>
        public static DataTableCellValue FromFloat(float value)
        {
            return new FloatCellValue(value);
        }

        /// <summary>Wraps a <see cref="string"/> (DT_String basic type). Null is coerced to empty.</summary>
        public static DataTableCellValue FromString(string value)
        {
            return new StringCellValue(value ?? string.Empty);
        }

        /// <summary>
        /// CF-04 value-equality check: handles null-equals-null and per-concrete-type value
        /// equality. Two values of different concrete subclasses are never equal. Used by the
        /// <see cref="MutableDataTableCell.Value"/> setter to detect a no-op assignment (which must
        /// preserve the original byte slice + clean state).
        /// </summary>
        public static bool Equals(DataTableCellValue a, DataTableCellValue b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            return a.ValueEquals(b);
        }

        // Per-subclass equality. Callers reach this only after the null/reference fast-paths above.
        internal abstract bool ValueEquals(DataTableCellValue other);

        /// <summary>
        /// Renders this value as the CSV cell text for the given column type (Plan 09-06 — added for
        /// the CSV serializer + the framework CsvCellCoercion round-trip property). The rendering is
        /// keyed on the column's BASIC type so it round-trips through
        /// <see cref="DataTableColumnType.TryCoerceCellValue"/> for the non-mangled basic types:
        /// <list type="bullet">
        ///   <item>DT_Int basic type → the int32 formatted with the invariant culture.</item>
        ///   <item>DT_Float basic type → the float formatted round-trippable ("R", invariant).</item>
        ///   <item>DT_String basic type → the string verbatim.</item>
        ///   <item>DT_Comment → empty string (comment cells carry no on-disk value).</item>
        /// </list>
        ///
        /// <para><b>DT_HashString lossiness (iter-2 item 9 — both reviewers):</b> a DT_HashString
        /// column's BasicType is DT_Int, so the stored int32 hash is rendered here — NOT the source
        /// string (which is never persisted; only the int32 hash reaches disk per RESEARCH Pitfall 4).
        /// A CSV round-trip therefore CANNOT reconstruct the original source string for an
        /// already-saved DT_HashString cell. The CSV header carries a <c>#</c>-comment documenting
        /// this; 09-07 smoke Step 10 verifies the behavior (it does NOT expect source-string
        /// round-trip).</para>
        /// </summary>
        public string ToCsvString(DataTableColumnType ct)
        {
            if (ct == null) throw new ArgumentNullException("ct");

            if (ct.Type == DataType.Comment)
            {
                return string.Empty;
            }

            switch (ct.BasicType)
            {
                case DataType.Int:
                    return AsInt().ToString(CultureInfo.InvariantCulture);
                case DataType.Float:
                    return AsFloat().ToString("R", CultureInfo.InvariantCulture);
                case DataType.String:
                    return AsString();
                default:
                    // DT_Unknown (cross-table enum 'z' / malformed) — best-effort string form.
                    return ToString();
            }
        }

        /// <summary>
        /// Serializes a fresh per-cell ROWS payload for the given value under the given column type,
        /// matching the SOE <c>_saveRows</c> encoder (<c>DataTableWriter.cpp:860-911</c>) keyed on the
        /// column's BASIC type:
        /// <list type="bullet">
        ///   <item>DT_Int basic type → <c>int32</c> little-endian</item>
        ///   <item>DT_Float basic type → <c>float32</c> little-endian</item>
        ///   <item>DT_String basic type → NUL-terminated ASCII string</item>
        ///   <item>DT_Comment → emits NOTHING (zero-byte ROWS payload). This is the per-cell ROWS
        ///         asymmetry from RESEARCH Pitfall 1: a DT_Comment column STILL appears in COLS+TYPE
        ///         (item 13), only its row payload is empty.</item>
        /// </list>
        /// </summary>
        public static void SerializeFresh(BinaryWriter bw, DataTableColumnType ct, DataTableCellValue value)
        {
            if (bw == null) throw new ArgumentNullException("bw");
            if (ct == null) throw new ArgumentNullException("ct");
            if (value == null) throw new ArgumentNullException("value");

            // DT_Comment contributes zero bytes to ROWS (item 13). Guard on Type, not BasicType,
            // because a comment column's basic type is DT_Comment in this port.
            if (ct.Type == DataType.Comment)
            {
                return;
            }

            switch (ct.BasicType)
            {
                case DataType.Int:
                    bw.Write(value.AsInt());
                    break;
                case DataType.Float:
                    bw.Write(value.AsFloat());
                    break;
                case DataType.String:
                    WriteNulString(bw, value.AsString());
                    break;
                default:
                    throw new DataTableParseException(
                        "SerializeFresh: column basic type '" + ct.BasicType
                        + "' is not a serializable DTII basic type (expected Int/Float/String/Comment).");
            }
        }

        // Coercion accessors. A correctly-typed value returns its payload directly; a mismatched
        // value type is a programming error (the reader builds the right concrete subclass per the
        // column's basic type) and surfaces as a parse exception.
        internal virtual int AsInt()
        {
            throw new DataTableParseException("Cell value " + GetType().Name + " is not an integer value.");
        }

        internal virtual float AsFloat()
        {
            throw new DataTableParseException("Cell value " + GetType().Name + " is not a float value.");
        }

        internal virtual string AsString()
        {
            throw new DataTableParseException("Cell value " + GetType().Name + " is not a string value.");
        }

        // Writes a NUL-terminated ASCII string (matches Iff::insertChunkString — istrlen+1 bytes).
        private static void WriteNulString(BinaryWriter bw, string s)
        {
            s = s ?? string.Empty;
            byte[] ascii = Encoding.ASCII.GetBytes(s);
            bw.Write(ascii, 0, ascii.Length);
            bw.Write((byte)0);
        }

        /// <summary>An <see cref="int"/>-typed cell value (DT_Int basic type).</summary>
        public sealed class IntCellValue : DataTableCellValue
        {
            /// <summary>The wrapped integer.</summary>
            public int Value { get; }

            /// <summary>Creates an integer cell value.</summary>
            public IntCellValue(int value)
            {
                Value = value;
            }

            internal override int AsInt()
            {
                return Value;
            }

            internal override bool ValueEquals(DataTableCellValue other)
            {
                var o = other as IntCellValue;
                return o != null && o.Value == Value;
            }

            /// <summary>Returns the integer formatted with the invariant culture.</summary>
            public override string ToString()
            {
                return Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>A <see cref="float"/>-typed cell value (DT_Float basic type).</summary>
        public sealed class FloatCellValue : DataTableCellValue
        {
            /// <summary>The wrapped float.</summary>
            public float Value { get; }

            /// <summary>Creates a float cell value.</summary>
            public FloatCellValue(float value)
            {
                Value = value;
            }

            internal override float AsFloat()
            {
                return Value;
            }

            internal override bool ValueEquals(DataTableCellValue other)
            {
                var o = other as FloatCellValue;
                // Bit-exact equality (not tolerance) so a no-op Value set is only detected for an
                // identical bit pattern — preserving the original-slice round-trip guarantee.
                return o != null && o.Value.Equals(Value);
            }

            /// <summary>Returns the float formatted round-trippable with the invariant culture.</summary>
            public override string ToString()
            {
                return Value.ToString("R", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>A <see cref="string"/>-typed cell value (DT_String basic type).</summary>
        public sealed class StringCellValue : DataTableCellValue
        {
            /// <summary>The wrapped string (never null).</summary>
            public string Value { get; }

            /// <summary>Creates a string cell value. Null is coerced to empty.</summary>
            public StringCellValue(string value)
            {
                Value = value ?? string.Empty;
            }

            internal override string AsString()
            {
                return Value;
            }

            internal override bool ValueEquals(DataTableCellValue other)
            {
                var o = other as StringCellValue;
                return o != null && string.Equals(o.Value, Value, StringComparison.Ordinal);
            }

            /// <summary>Returns the wrapped string verbatim.</summary>
            public override string ToString()
            {
                return Value;
            }
        }
    }
}
