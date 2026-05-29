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
using System.Text;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Datatable
{
    /// <summary>
    /// Typed DTII reader — the entry point that turns a parsed <see cref="MutableIffDocument"/> (a
    /// <c>FORM DTII</c> tree from Phase 8's <c>IffReader</c> + <c>MutableIffDocument.FromDocument</c>)
    /// into a typed <see cref="MutableDataTableDocument"/> with per-column
    /// <see cref="DataTableColumnType"/> and per-cell <see cref="MutableDataTableCell"/> carrying the
    /// CF-04 captured byte slice. Managed port of the SOE <c>DataTable::load</c> dispatch
    /// (<c>DataTable.cpp:444-603</c>) and <c>_readCell</c> (<c>DataTable.cpp:400-440</c>).
    ///
    /// <para><b>Cell-count sanity cap (T-09-01 DoS):</b> <c>numRows * numCols</c> is checked against
    /// a 16 M-cell cap BEFORE any per-cell allocation; an over-cap or negative count throws
    /// <see cref="DataTableParseException"/>.</para>
    /// </summary>
    public sealed class DataTableDocument
    {
        // 64 MB / 4 bytes-per-int-cell ≈ 16 M cells. Conservative upper bound on total cells before
        // we even look at per-cell sizes (RESEARCH § Security Domain malformed-.tab mitigation).
        private const long MaxCells = 64L * 1024 * 1024 / 4;

        /// <summary>The typed mutable model produced from the IFF tree.</summary>
        public MutableDataTableDocument Mutable { get; }

        private DataTableDocument(MutableDataTableDocument mutable)
        {
            Mutable = mutable;
        }

        /// <summary>
        /// Parses a typed datatable from the given <see cref="MutableIffDocument"/>. Throws
        /// <see cref="DataTableParseException"/> on a non-DTII root, an unsupported version FORM,
        /// a missing COLS/TYPE/ROWS chunk, a cell-count sanity-cap breach, or a malformed payload.
        /// </summary>
        public static DataTableDocument FromIff(MutableIffDocument iffDoc)
        {
            if (iffDoc == null) throw new ArgumentNullException("iffDoc");
            MutableIffNode root = iffDoc.Root;
            if (root == null)
            {
                throw new DataTableParseException("IFF document has no root node.");
            }

            if (root.Kind != MutableIffNodeKind.Container || root.SubTypeId != "DTII")
            {
                throw new DataTableParseException(
                    "Root is not a FORM DTII container (TypeId='" + root.TypeId + "', SubTypeId='"
                    + root.SubTypeId + "').");
            }

            MutableIffNode versionForm = SingleContainerChild(root);
            string version = versionForm.SubTypeId;
            if (version != "0000" && version != "0001")
            {
                throw new DataTableParseException("Unsupported DTII version FORM '" + version + "' (expected 0000 or 0001).");
            }

            byte[] cols = LeafPayload(versionForm, "COLS");
            byte[] type = LeafPayload(versionForm, "TYPE");
            byte[] rows = LeafPayload(versionForm, "ROWS");

            List<string> columnNames = ReadColumns(cols);
            int numCols = columnNames.Count;
            List<DataTableColumnType> columnTypes = ReadTypes(type, version, numCols);

            var columns = new List<MutableDataTableColumn>(numCols);
            for (int i = 0; i < numCols; i++)
            {
                columns.Add(new MutableDataTableColumn(columnNames[i], columnTypes[i]));
            }

            var rowList = ReadRows(rows, columns);

            var mutable = new MutableDataTableDocument(version, columns, rowList);
            mutable.SourceIff = iffDoc;
            return new DataTableDocument(mutable);
        }

        // ── DTII tree navigation ─────────────────────────────────────────────

        private static MutableIffNode SingleContainerChild(MutableIffNode root)
        {
            MutableIffNode found = null;
            foreach (MutableIffNode child in root.Children)
            {
                if (child.Kind == MutableIffNodeKind.Container)
                {
                    if (found != null)
                    {
                        throw new DataTableParseException("FORM DTII has more than one version FORM child.");
                    }

                    found = child;
                }
            }

            if (found == null)
            {
                throw new DataTableParseException("FORM DTII has no version FORM child.");
            }

            return found;
        }

        private static byte[] LeafPayload(MutableIffNode versionForm, string tag)
        {
            foreach (MutableIffNode child in versionForm.Children)
            {
                if (child.Kind == MutableIffNodeKind.Leaf && child.TypeId == tag)
                {
                    return child.GetPayloadCopy();
                }
            }

            throw new DataTableParseException("DTII version FORM is missing the '" + tag + "' chunk.");
        }

        // ── COLS: int32 numCols + numCols NUL strings ────────────────────────

        private static List<string> ReadColumns(byte[] payload)
        {
            int offset = 0;
            int numCols = ReadInt32Le(payload, ref offset);
            if (numCols < 0 || numCols > MaxCells)
            {
                throw new DataTableParseException("COLS column count " + numCols + " is out of range.");
            }

            var names = new List<string>(numCols);
            for (int i = 0; i < numCols; i++)
            {
                names.Add(ReadNulString(payload, ref offset));
            }

            return names;
        }

        // ── TYPE: V0001 NUL type-spec strings; V0000 int32 ordinals ──────────

        private static List<DataTableColumnType> ReadTypes(byte[] payload, string version, int numCols)
        {
            var types = new List<DataTableColumnType>(numCols);
            int offset = 0;
            if (version == "0001")
            {
                for (int i = 0; i < numCols; i++)
                {
                    string spec = ReadNulString(payload, ref offset);
                    types.Add(new DataTableColumnType(spec));
                }
            }
            else
            {
                for (int i = 0; i < numCols; i++)
                {
                    int ordinal = ReadInt32Le(payload, ref offset);
                    types.Add(new DataTableColumnType(OrdinalToSpec(ordinal)));
                }
            }

            return types;
        }

        // V0000 stored a raw DataType ordinal. Only Int/Float/String are valid in V0000
        // (DataTable.cpp:502-534 FATALs on anything else); map them to the canonical single-char spec.
        private static string OrdinalToSpec(int ordinal)
        {
            switch (ordinal)
            {
                case (int)DataType.Int:
                    return "i";
                case (int)DataType.Float:
                    return "f";
                case (int)DataType.String:
                    return "s";
                default:
                    throw new DataTableParseException(
                        "V0000 TYPE ordinal " + ordinal + " is not a valid V0000 column type (expected 0/1/2).");
            }
        }

        // ── ROWS: int32 numRows + per-row, per-column cell payload ───────────

        private static List<MutableDataTableRow> ReadRows(byte[] payload, List<MutableDataTableColumn> columns)
        {
            int offset = 0;
            int numRows = ReadInt32Le(payload, ref offset);
            int numCols = columns.Count;
            if (numRows < 0)
            {
                throw new DataTableParseException("ROWS row count " + numRows + " is negative.");
            }

            // T-09-01 DoS: cap total cell count before allocating per-cell objects.
            long totalCells = (long)numRows * numCols;
            if (totalCells > MaxCells)
            {
                throw new DataTableParseException(
                    "Cell count exceeds sanity cap (" + totalCells + " > " + MaxCells + ").");
            }

            var rows = new List<MutableDataTableRow>(numRows);
            for (int r = 0; r < numRows; r++)
            {
                var row = new MutableDataTableRow();
                for (int c = 0; c < numCols; c++)
                {
                    DataTableColumnType ct = columns[c].ColumnType;
                    int cellStart = offset;
                    DataTableCellValue value = ReadCell(payload, ref offset, ct);
                    int cellLen = offset - cellStart;
                    byte[] slice = new byte[cellLen];
                    if (cellLen > 0)
                    {
                        Array.Copy(payload, cellStart, slice, 0, cellLen);
                    }

                    var cell = new MutableDataTableCell(value, slice);
                    cell.SetParentColumn(columns[c]);
                    row.AddCellInternal(cell);
                }

                rows.Add(row);
            }

            return rows;
        }

        // Port of _readCell (DataTable.cpp:400-440), switching on the column's BASIC type.
        private static DataTableCellValue ReadCell(byte[] payload, ref int offset, DataTableColumnType ct)
        {
            switch (ct.BasicType)
            {
                case DataType.Int:
                    return DataTableCellValue.FromInt(ReadInt32Le(payload, ref offset));
                case DataType.Float:
                    return DataTableCellValue.FromFloat(ReadFloatLe(payload, ref offset));
                case DataType.String:
                    return DataTableCellValue.FromString(ReadNulString(payload, ref offset));
                default:
                    // DT_Comment → zero-byte payload (offset unchanged); DT_Unknown is also stored
                    // with no per-cell bytes in this reader (no V1 source produces it on a real cell).
                    if (ct.Type == DataType.Comment || ct.Type == DataType.Unknown)
                    {
                        return DataTableCellValue.FromString(string.Empty);
                    }

                    throw new DataTableParseException(
                        "Cannot read cell for column basic type '" + ct.BasicType + "'.");
            }
        }

        // ── Little-endian / NUL-string primitives (DTII payload convention) ──

        private static int ReadInt32Le(byte[] buffer, ref int offset)
        {
            if (offset < 0 || offset + 4 > buffer.Length)
            {
                throw new DataTableParseException("Truncated int32 at offset " + offset + " (buffer length " + buffer.Length + ").");
            }

            int v = buffer[offset]
                    | (buffer[offset + 1] << 8)
                    | (buffer[offset + 2] << 16)
                    | (buffer[offset + 3] << 24);
            offset += 4;
            return v;
        }

        private static float ReadFloatLe(byte[] buffer, ref int offset)
        {
            if (offset < 0 || offset + 4 > buffer.Length)
            {
                throw new DataTableParseException("Truncated float32 at offset " + offset + " (buffer length " + buffer.Length + ").");
            }

            byte[] tmp = new byte[4];
            Array.Copy(buffer, offset, tmp, 0, 4);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(tmp);
            }

            float v = BitConverter.ToSingle(tmp, 0);
            offset += 4;
            return v;
        }

        private static string ReadNulString(byte[] buffer, ref int offset)
        {
            int start = offset;
            while (offset < buffer.Length && buffer[offset] != 0)
            {
                offset++;
            }

            if (offset >= buffer.Length)
            {
                throw new DataTableParseException("Unterminated NUL string starting at offset " + start + ".");
            }

            string s = Encoding.ASCII.GetString(buffer, start, offset - start);
            offset++; // consume the NUL terminator
            return s;
        }
    }
}
