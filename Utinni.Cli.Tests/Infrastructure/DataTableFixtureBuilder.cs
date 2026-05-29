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

namespace Utinni.Cli.Tests.Infrastructure
{
    /// <summary>
    /// CLI-side synthetic DTII fixture builders — a DUPLICATE of
    /// <c>UtinniCoreDotNet.Tests.FormatsTests.Datatable.DatatableFixtures</c> (Plan 09-01).
    ///
    /// <para><b>Why a duplicate, not a reference:</b> xUnit test projects do NOT ProjectReference each
    /// other (that would create a test-of-tests anti-pattern), so the CLI golden suite cannot reach
    /// into the framework test project's builder. The duplication is acceptable because (a) both
    /// builders consume ONLY framework primitives (<see cref="MutableDataTableDocument"/> +
    /// <see cref="DataTableWriter"/> → <c>IffWriter.Write</c>) so they cannot drift in behavior, (b) the
    /// file is small, and (c) Phase 8's <c>IffBuilder</c> followed the same posture. The non-optional
    /// drift-detector [Fact] in RoundtripTabCommandTests asserts this builder still emits a CANONICAL
    /// round-trippable fixture — if it ever drifts to a non-canonical layout that fact fails.</para>
    ///
    /// <para>The seven <c>Build*</c> helpers MUST stay in lock-step with the 09-01 framework builder.
    /// Each returns <c>byte[]</c> of a complete <c>FORM DTII</c>-framed IFF.</para>
    /// </summary>
    public static class DataTableFixtureBuilder
    {
        // ── Low-level row/cell/doc builders (mirror DatatableFixtures) ────────

        private static MutableDataTableRow Row(params DataTableCellValue[] cells)
        {
            var row = new MutableDataTableRow();
            foreach (var c in cells)
            {
                // Fresh cell: no original slice; WritePayload will SerializeFresh. InternalsVisibleTo
                // grants this assembly access to AddCellInternal + the internal cell ctor.
                row.AddCellInternal(new MutableDataTableCell(c, null));
            }

            return row;
        }

        private static byte[] BuildDoc(string version, IList<MutableDataTableColumn> columns, IList<MutableDataTableRow> rows)
        {
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

        // ── Public fixtures (lock-step with 09-01 DatatableFixtures) ─────────

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
        /// Columns: i / f / s / h / b / e / v / p. One row.
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
                    DataTableCellValue.FromString(string.Empty),
                    DataTableCellValue.FromString("first")),
                Row(
                    DataTableCellValue.FromInt(2),
                    DataTableCellValue.FromString(string.Empty),
                    DataTableCellValue.FromString("second"))
            };
            return BuildDoc("0001", cols, rows);
        }

        /// <summary>
        /// V0001 CombatDataTable-like fixture: 200 rows × 30 mixed-type columns. Backs the
        /// remove-row index-shift golden in this plan's test suite.
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

        // ── iter-2 LOW fold: one-cell-differs helper (backs the isolation golden) ──

        /// <summary>
        /// Rebuilds the <see cref="BuildV1AllTypes"/> fixture with EXACTLY ONE cell changed to
        /// <paramref name="newValue"/> at (<paramref name="row"/>, <paramref name="col"/>). Backs the
        /// <c>Roundtrip_OneCellDiffers_ComparisonIsolatesExactlyOneCell</c> golden — the produced file
        /// is byte-different from BuildV1AllTypes in exactly that one cell's ROWS slice.
        /// </summary>
        public static byte[] BuildV1OneCellDiffersFrom(int row, int col, string newValue)
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

            var baseValues = new DataTableCellValue[]
            {
                DataTableCellValue.FromInt(7),
                DataTableCellValue.FromFloat(3.5f),
                DataTableCellValue.FromString("hello"),
                DataTableCellValue.FromInt(unchecked((int)DataTableHashCrc.Compute("creature/foo"))),
                DataTableCellValue.FromInt(1),
                DataTableCellValue.FromInt(2),
                DataTableCellValue.FromInt(5),
                DataTableCellValue.FromString("name|0|val|$|")
            };

            // Coerce the override value through the target column's type so the differing cell is a
            // valid, canonically-serialized value (not a raw string into an int column).
            if (row == 0 && col >= 0 && col < baseValues.Length)
            {
                DataTableCellValue coerced;
                if (cols[col].ColumnType.TryCoerceCellValue(newValue, out coerced))
                {
                    baseValues[col] = coerced;
                }
            }

            var rows = new List<MutableDataTableRow> { Row(baseValues) };
            return BuildDoc("0001", cols, rows);
        }
    }
}
