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
// Datatable layout understood by reading swg-client-v2
// .../sharedUtility/src/shared/DataTable.cpp (load_0000 / load_0001) and DataTableColumnType.cpp
// (SOE/Bootprint, All Rights Reserved). Only the on-disk COLS/TYPE/ROWS layout and the type-code /
// format-string conventions were studied — no code, comments, identifier names, or test fixtures
// copied from any reference source. Implementation original to Utinni under MIT.

using System.Collections.Generic;
using System.Text;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Decoders
{
    /// <summary>The basic storage kind of a datatable cell (the engine's reduced "basic type").</summary>
    public enum DataCellKind
    {
        Int,
        Float,
        String
    }

    /// <summary>One datatable column: its name, the raw type spec, and the basic cell kind.</summary>
    public sealed class DataTableColumnInfo
    {
        /// <summary>Column name (from the COLS chunk).</summary>
        public string Name { get; }

        /// <summary>
        /// The raw type spec: a single-letter code for v0000 (i/f/s) or the full format string for
        /// v0001 (e.g. "i", "f", "s", "e(a=0,b=1)[a]"). Preserved verbatim for display.
        /// </summary>
        public string TypeSpec { get; }

        /// <summary>The reduced basic storage kind used to read each cell of this column.</summary>
        public DataCellKind Kind { get; }

        public DataTableColumnInfo(string name, string typeSpec, DataCellKind kind)
        {
            Name = name;
            TypeSpec = typeSpec;
            Kind = kind;
        }
    }

    /// <summary>
    /// A decoded datatable: its version tag, columns, and rows of typed cells. Each row is an
    /// <c>object[]</c> of length <c>Columns.Count</c> holding a boxed <see cref="int"/>,
    /// <see cref="float"/>, or <see cref="string"/> per the column's <see cref="DataCellKind"/>.
    /// Read-only Phase-7 surface; Phase 9 makes this editable with no rework (D-13).
    /// </summary>
    public sealed class DataTableView
    {
        public string Version { get; }
        public IReadOnlyList<DataTableColumnInfo> Columns { get; }
        public IReadOnlyList<object[]> Rows { get; }

        public DataTableView(string version, IReadOnlyList<DataTableColumnInfo> columns, IReadOnlyList<object[]> rows)
        {
            Version = version;
            Columns = columns;
            Rows = rows;
        }
    }

    /// <summary>
    /// Decodes a datatable (FORM "DTII") from the shared <see cref="IffDocument"/> parse output into
    /// columns + typed rows. Ported from the engine's load_0000 / load_0001 layout.
    ///
    /// <para><b>Parser purity:</b> NO JSON, NO console output, NO file writes — like
    /// <see cref="IffReader"/>. The <c>decode-iff</c> CLI verb owns JSON projection.</para>
    /// </summary>
    public static class DataTableDecoder
    {
        // The decoded root form tag (FORM DTII).
        public const string RootSubType = "DTII";

        /// <summary>
        /// Decodes the datatable from <paramref name="doc"/>.
        /// Throws <see cref="DecoderException"/> on a structural/format problem.
        /// </summary>
        public static DataTableView Decode(IffDocument doc)
        {
            if (doc == null)
            {
                throw new DecoderException(DecoderError.UnexpectedForm, "Null IFF document.");
            }

            var root = doc.Root as IffContainerChunk;
            if (root == null || root.SubTypeId != RootSubType)
            {
                throw new DecoderException(DecoderError.UnexpectedForm,
                    "Expected a FORM " + RootSubType + " root; got "
                    + DescribeRoot(doc.Root) + ".");
            }

            // FORM DTII -> FORM "0000" | "0001".
            var versionForm = FindContainerChild(root, "0000", "0001");
            if (versionForm == null)
            {
                throw new DecoderException(DecoderError.UnsupportedVersion,
                    "FORM DTII does not contain a supported version form (0000 or 0001).");
            }
            string version = versionForm.SubTypeId;
            bool isV0001 = version == "0001";

            var cols = FindLeafChild(versionForm, "COLS");
            var type = FindLeafChild(versionForm, "TYPE");
            var rows = FindLeafChild(versionForm, "ROWS");
            if (cols == null || type == null || rows == null)
            {
                throw new DecoderException(DecoderError.UnexpectedForm,
                    "FORM " + version + " is missing one of the COLS/TYPE/ROWS chunks.");
            }

            // ── COLS: int32 numCols, then numCols NUL-terminated column names ──
            var colsCursor = new IffPayloadCursor(cols.Data);
            int numCols = colsCursor.ReadInt32Le();
            if (numCols < 0)
            {
                throw new DecoderException(DecoderError.NegativeCount, "COLS numCols is negative: " + numCols + ".");
            }
            // Forged-count guard: each column name needs at least 1 byte (its NUL terminator), so the
            // count cannot exceed the remaining COLS bytes. Reject BEFORE the per-column loop (T-07-13).
            if (numCols > colsCursor.Remaining)
            {
                throw new DecoderException(DecoderError.CountExceedsCap,
                    "COLS numCols " + numCols + " exceeds remaining " + colsCursor.Remaining + " byte(s).");
            }

            var names = new List<string>(numCols);
            for (int i = 0; i < numCols; i++)
            {
                names.Add(colsCursor.ReadCString(Encoding.ASCII));
            }

            // ── TYPE: v0000 -> int32 type-code per col; v0001 -> format string per col ──
            var typeCursor = new IffPayloadCursor(type.Data);
            var columns = new List<DataTableColumnInfo>(numCols);
            for (int i = 0; i < numCols; i++)
            {
                string typeSpec;
                DataCellKind kind;
                if (isV0001)
                {
                    typeSpec = typeCursor.ReadCString(Encoding.ASCII);
                    kind = KindFromFormatString(typeSpec);
                }
                else
                {
                    int code = typeCursor.ReadInt32Le();
                    kind = KindFromV0000Code(code);
                    typeSpec = SpecCharFor(kind).ToString();
                }
                columns.Add(new DataTableColumnInfo(names[i], typeSpec, kind));
            }

            // ── ROWS: int32 numRows, then numRows*numCols cells (row-major, LE scalars) ──
            var rowsCursor = new IffPayloadCursor(rows.Data);
            int numRows = rowsCursor.ReadInt32Le();
            if (numRows < 0)
            {
                throw new DecoderException(DecoderError.NegativeCount, "ROWS numRows is negative: " + numRows + ".");
            }
            // Forged-count guard (division form): every row holds numCols cells and every cell is at
            // least 1 byte on disk (a string cell is at minimum a lone NUL). So a row needs >= numCols
            // bytes, and numRows cannot exceed remaining / numCols. Reject BEFORE allocating the grid.
            int minRowStride = numCols < 1 ? 1 : numCols;
            if (numRows > rows.Data.Length / minRowStride)
            {
                throw new DecoderException(DecoderError.CountExceedsCap,
                    "ROWS numRows " + numRows + " exceeds what " + rows.Data.Length
                    + " byte(s) can hold for " + numCols + " column(s).");
            }

            var decodedRows = new List<object[]>(numRows);
            for (int r = 0; r < numRows; r++)
            {
                var cells = new object[numCols];
                for (int c = 0; c < numCols; c++)
                {
                    cells[c] = ReadCell(rowsCursor, columns[c].Kind);
                }
                decodedRows.Add(cells);
            }

            return new DataTableView(version, columns, decodedRows);
        }

        private static object ReadCell(IffPayloadCursor cursor, DataCellKind kind)
        {
            switch (kind)
            {
                case DataCellKind.Int:    return cursor.ReadInt32Le();
                case DataCellKind.Float:  return cursor.ReadFloatLe();
                case DataCellKind.String: return cursor.ReadCString(Encoding.ASCII);
                default:
                    throw new DecoderException(DecoderError.UnsupportedVersion, "Unsupported cell kind.");
            }
        }

        // v0000 type-codes: the engine's DataTableColumnType::DataType enum — Int=0, Float=1, String=2.
        private static DataCellKind KindFromV0000Code(int code)
        {
            switch (code)
            {
                case 0: return DataCellKind.Int;
                case 1: return DataCellKind.Float;
                case 2: return DataCellKind.String;
                default:
                    throw new DecoderException(DecoderError.UnsupportedVersion,
                        "Unsupported v0000 column type code " + code + " (Phase 7 supports Int/Float/String).");
            }
        }

        // v0001 format strings: the leading char selects the reduced basic kind. The Int/String
        // specializations (h/b/e/v/z store an int; p stores a string) collapse to their basic kind,
        // matching the engine's getBasicType(). Comment ('c') and anything else are unsupported here.
        private static DataCellKind KindFromFormatString(string spec)
        {
            if (string.IsNullOrEmpty(spec))
            {
                throw new DecoderException(DecoderError.UnsupportedVersion, "Empty column type spec.");
            }
            char t = char.ToLowerInvariant(spec[0]);
            switch (t)
            {
                case 'i': return DataCellKind.Int;
                case 'f': return DataCellKind.Float;
                case 's': return DataCellKind.String;
                case 'h': // HashString  -> basic Int
                case 'b': // Bool        -> basic Int
                case 'e': // Enum        -> basic Int
                case 'v': // BitVector   -> basic Int
                case 'z': // Enum-from-file -> basic Int
                    return DataCellKind.Int;
                case 'p': // PackedObjVars -> basic String
                    return DataCellKind.String;
                default:
                    throw new DecoderException(DecoderError.UnsupportedVersion,
                        "Unsupported column type spec '" + spec + "' (Phase 7 bounded posture).");
            }
        }

        private static char SpecCharFor(DataCellKind kind)
        {
            switch (kind)
            {
                case DataCellKind.Int:    return 'i';
                case DataCellKind.Float:  return 'f';
                default:                  return 's';
            }
        }

        private static IffContainerChunk FindContainerChild(IffContainerChunk parent, params string[] subTypeIds)
        {
            foreach (var child in parent.Children)
            {
                if (child is IffContainerChunk c)
                {
                    foreach (var want in subTypeIds)
                    {
                        if (c.SubTypeId == want) return c;
                    }
                }
            }
            return null;
        }

        private static IffLeafChunk FindLeafChild(IffContainerChunk parent, string typeId)
        {
            foreach (var child in parent.Children)
            {
                if (child is IffLeafChunk leaf && leaf.TypeId == typeId) return leaf;
            }
            return null;
        }

        private static string DescribeRoot(IffChunk root)
        {
            if (root is IffContainerChunk c) return c.TypeId.TrimEnd() + " " + c.SubTypeId.TrimEnd();
            return root != null ? root.TypeId.TrimEnd() : "(null)";
        }
    }
}
