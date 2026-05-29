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

using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Datatable;

namespace UtinniCoreDotNet.Tests.FormatsTests.Datatable
{
    /// <summary>
    /// Synthetic DTII fixture builders. Each returns a complete <c>FORM DTII</c>-framed IFF byte
    /// array built THROUGH the framework primitives (<see cref="MutableDataTableDocument"/> +
    /// <see cref="DataTableWriter"/> → <c>IffWriter.Write</c>) so every fixture is CANONICAL by
    /// construction — exactly the bytes the writer emits. No on-disk binary is checked in
    /// (Assumption A6 + Open Q3). Mirrors the Phase 8 <c>IffBuilder</c> composable-helper shape.
    /// </summary>
    public static class DatatableFixtures
    {
        // ── Low-level row/cell builders ──────────────────────────────────────

        private static MutableDataTableRow Row(params DataTableCellValue[] cells)
        {
            var row = new MutableDataTableRow();
            foreach (var c in cells)
            {
                // Fresh cell: no original slice; WritePayload will SerializeFresh. InternalsVisibleTo
                // grants the test assembly access to AddCellInternal.
                row.AddCellInternal(new MutableDataTableCell(c, null));
            }

            return row;
        }

        private static byte[] BuildDoc(string version, IList<MutableDataTableColumn> columns, IList<MutableDataTableRow> rows)
        {
            // Wire each cell's parent column so SerializeFresh sees the right type.
            for (int r = 0; r < rows.Count; r++)
            {
                IReadOnlyList<MutableDataTableCell> cells = rows[r].Cells;
                for (int c = 0; c < columns.Count && c < cells.Count; c++)
                {
                    cells[c].SetParentColumn(columns[c]);
                }
            }

            var doc = new MutableDataTableDocument(version, columns, rows);
            return new DataTableWriter(doc).Serialize();
        }

        private static MutableDataTableColumn Col(string name, string spec)
        {
            return new MutableDataTableColumn(name, new DataTableColumnType(spec));
        }

        // ── Public fixtures (VALIDATION § Golden Fixtures Needed) ────────────

        /// <summary>V0000 minimal: one int + one string column, 2 rows.</summary>
        public static byte[] BuildV0Minimal()
        {
            var cols = new List<MutableDataTableColumn>
            {
                Col("id", "i"),
                Col("name", "s")
            };
            var rows = new List<MutableDataTableRow>
            {
                Row(DataTableCellValue.FromInt(1), DataTableCellValue.FromString("alpha")),
                Row(DataTableCellValue.FromInt(2), DataTableCellValue.FromString("beta"))
            };
            return BuildDoc("0000", cols, rows);
        }

        /// <summary>V0001 minimal: one int + one string column, 2 rows.</summary>
        public static byte[] BuildV1Minimal()
        {
            var cols = new List<MutableDataTableColumn>
            {
                Col("id", "i"),
                Col("name", "s")
            };
            var rows = new List<MutableDataTableRow>
            {
                Row(DataTableCellValue.FromInt(10), DataTableCellValue.FromString("one")),
                Row(DataTableCellValue.FromInt(20), DataTableCellValue.FromString("two"))
            };
            return BuildDoc("0001", cols, rows);
        }

        /// <summary>
        /// V0001 with one column per DT_* type whose basic type is serializable (Int/Float/String).
        /// Columns: i / f / s / h(hash, stored int) / b(bool, stored int) / e(enum, stored int) /
        /// v(bitvector, stored int) / p(packed objvars, stored string). One row.
        /// </summary>
        public static byte[] BuildV1AllTypes()
        {
            var cols = new List<MutableDataTableColumn>
            {
                Col("anInt", "i"),
                Col("aFloat", "f"),
                Col("aString", "s"),
                Col("aHash", "h"),
                Col("aBool", "b"),
                Col("anEnum", "e(red=1,green=2,blue=3)[red]"),
                Col("aFlags", "v(a=1,b=2,c=3)[NONE]"),
                Col("aPacked", "p")
            };
            var rows = new List<MutableDataTableRow>
            {
                Row(
                    DataTableCellValue.FromInt(7),
                    DataTableCellValue.FromFloat(3.5f),
                    DataTableCellValue.FromString("hello"),
                    DataTableCellValue.FromInt(unchecked((int)DataTableHashCrc.Compute("creature/foo"))),
                    DataTableCellValue.FromInt(1),
                    DataTableCellValue.FromInt(2),
                    DataTableCellValue.FromInt(5),
                    DataTableCellValue.FromString("name|0|val|$|"))
            };
            return BuildDoc("0001", cols, rows);
        }

        /// <summary>V0001 with default + enum columns, 2 rows exercising defaults.</summary>
        public static byte[] BuildV1WithDefaultsAndEnums()
        {
            var cols = new List<MutableDataTableColumn>
            {
                Col("level", "i[1]"),
                Col("faction", "e(rebel=1,imperial=2,neutral=3)[neutral]")
            };
            var rows = new List<MutableDataTableRow>
            {
                Row(DataTableCellValue.FromInt(1), DataTableCellValue.FromInt(1)),
                Row(DataTableCellValue.FromInt(5), DataTableCellValue.FromInt(3))
            };
            return BuildDoc("0001", cols, rows);
        }

        /// <summary>
        /// V0001 with a DT_Comment column between two data columns. The comment column stays in
        /// COLS+TYPE (item 13); its per-cell ROWS payload is zero bytes.
        /// </summary>
        public static byte[] BuildV1WithComment()
        {
            var cols = new List<MutableDataTableColumn>
            {
                Col("id", "i"),
                Col("note", "c"),
                Col("label", "s")
            };
            var rows = new List<MutableDataTableRow>
            {
                Row(
                    DataTableCellValue.FromInt(1),
                    DataTableCellValue.FromString(string.Empty), // comment cell — zero bytes on write
                    DataTableCellValue.FromString("first")),
                Row(
                    DataTableCellValue.FromInt(2),
                    DataTableCellValue.FromString(string.Empty),
                    DataTableCellValue.FromString("second"))
            };
            return BuildDoc("0001", cols, rows);
        }

        /// <summary>
        /// V0001 CombatDataTable-like fixture: ~200 rows × ~30 mixed-type columns. Shipped here so
        /// Plan 09-03 can measure DataGridView performance against it (Assumption A5).
        /// </summary>
        public static byte[] BuildV1CombatDataTableLike()
        {
            const int colCount = 30;
            const int rowCount = 200;
            var cols = new List<MutableDataTableColumn>(colCount);
            for (int c = 0; c < colCount; c++)
            {
                switch (c % 3)
                {
                    case 0:
                        cols.Add(Col("int_" + c, "i"));
                        break;
                    case 1:
                        cols.Add(Col("float_" + c, "f"));
                        break;
                    default:
                        cols.Add(Col("str_" + c, "s"));
                        break;
                }
            }

            var rows = new List<MutableDataTableRow>(rowCount);
            for (int r = 0; r < rowCount; r++)
            {
                var cells = new DataTableCellValue[colCount];
                for (int c = 0; c < colCount; c++)
                {
                    switch (c % 3)
                    {
                        case 0:
                            cells[c] = DataTableCellValue.FromInt(r * 100 + c);
                            break;
                        case 1:
                            cells[c] = DataTableCellValue.FromFloat(r + c * 0.25f);
                            break;
                        default:
                            cells[c] = DataTableCellValue.FromString("r" + r + "c" + c);
                            break;
                    }
                }

                rows.Add(Row(cells));
            }

            return BuildDoc("0001", cols, rows);
        }

        /// <summary>V0001 with columns but zero rows.</summary>
        public static byte[] BuildV1EmptyTable()
        {
            var cols = new List<MutableDataTableColumn>
            {
                Col("id", "i"),
                Col("name", "s")
            };
            var rows = new List<MutableDataTableRow>();
            return BuildDoc("0001", cols, rows);
        }
    }
}
