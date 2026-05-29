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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Datatable;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("roundtrip-tab", HelpText = "Parse -> optional mutate -> serialize -> re-parse a .tab; assert byte-exact identity on untouched cells.")]
    public class RoundtripTabOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .tab file.")]
        public string Path { get; set; }

        [Option("mutate-cell", HelpText = "row,col index pair (0-based), e.g. 3,5. Requires --mutate-value.")]
        public string MutateCell { get; set; }

        [Option("mutate-value", HelpText = "Replacement value for --mutate-cell (string; coerced per column type via DataTableColumnType.MangleValue).")]
        public string MutateValue { get; set; }

        [Option("remove-row", HelpText = "Row index (0-based) to remove.")]
        public int? RemoveRow { get; set; }

        [Option("remove-column", HelpText = "Column index (0-based) or column name to remove.")]
        public string RemoveColumn { get; set; }
    }

    /// <summary>
    /// CF-02 max-harness for the DTII (.tab) write path — the typed-datatable analog of
    /// <see cref="RoundtripIffCommand"/>. Loads a .tab, optionally applies at most ONE structural
    /// mutation (<c>--mutate-cell</c> | <c>--remove-row</c> | <c>--remove-column</c>), serializes via
    /// Plan 09-01's <see cref="DataTableWriter"/>, re-parses the serialized bytes, and reports
    /// byte-exact identity on the UNTOUCHED cells in a sorted-key JSON envelope. This is the automated
    /// gate for Phase 9 Success Criterion 4 ("no silent schema corruption").
    ///
    /// <para><b>Byte-exact scope (iter-2 item 4):</b> the no-mutation path asserts FULL-FILE
    /// byte-equality (<c>comparisonGranularity = "whole-file"</c>) — valid for the canonical/supported
    /// DTII fixtures the harness round-trips. Every mutation path narrows the guarantee to the per-cell
    /// ROWS-payload slice on the untouched cells (<c>comparisonGranularity = "per-cell-rows-slice"</c>),
    /// NOT whole-file identity.</para>
    ///
    /// <para><b>Per-cell ROWS-slice comparison algorithm (iter-2 item 3):</b> after a mutation, the
    /// command re-parses BOTH the loaded bytes and the serialized bytes into FRESH
    /// <see cref="MutableDataTableDocument"/>s. Each cell's <c>originalSlice</c> in a fresh no-op load
    /// reflects exactly the bytes on disk for that cell's ROWS payload. Untouched cells are paired by
    /// stable (row,col) index — with an explicit index-shift map for remove-row / remove-column — and
    /// their slices compared byte-for-byte. The comparison NEVER reads the mutated in-memory doc (whose
    /// edited cell has a nulled slice); it always re-parses the two byte arrays.</para>
    ///
    /// <para><b>Exit codes (mirroring <see cref="RoundtripIffCommand"/>):</b>
    ///   0 success; 1 UsageError; 2 IffParseException / DataTableParseException / IOException;
    ///   3 FileNotFound. Generic <see cref="System.Exception"/> is intentionally NOT caught.</para>
    /// </summary>
    public static class RoundtripTabCommand
    {
        public static int Run(RoundtripTabOptions o)
        {
            // Usage validation: at most ONE mutation flag may be supplied.
            bool hasMutateCell = !string.IsNullOrEmpty(o.MutateCell);
            bool hasRemoveRow = o.RemoveRow.HasValue;
            bool hasRemoveColumn = !string.IsNullOrEmpty(o.RemoveColumn);

            int mutationCount = (hasMutateCell ? 1 : 0) + (hasRemoveRow ? 1 : 0) + (hasRemoveColumn ? 1 : 0);
            if (mutationCount > 1)
            {
                return JsonOutput.EmitError("roundtrip-tab", "UsageError",
                    "--mutate-cell, --remove-row, and --remove-column are mutually exclusive (supply at most one).", exitCode: 1);
            }

            if (hasMutateCell && o.MutateValue == null)
            {
                return JsonOutput.EmitError("roundtrip-tab", "UsageError",
                    "--mutate-cell requires --mutate-value.", exitCode: 1);
            }

            // FileNotFound check: exit 3.
            if (!File.Exists(o.Path))
            {
                return JsonOutput.EmitError("roundtrip-tab", "FileNotFound",
                    ".tab file not found: " + o.Path, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(o.Path);
                DataTableDocument dtDoc = ParseTab(loadedBytes);
                MutableDataTableDocument mutDoc = dtDoc.Mutable;

                string mutationDescription = null;

                if (hasMutateCell)
                {
                    int usage = ApplyMutateCell(o, mutDoc, out mutationDescription);
                    if (usage != 0) return usage;
                }
                else if (hasRemoveRow)
                {
                    int usage = ApplyRemoveRow(o, mutDoc, out mutationDescription);
                    if (usage != 0) return usage;
                }
                else if (hasRemoveColumn)
                {
                    int usage = ApplyRemoveColumn(o, mutDoc, out mutationDescription);
                    if (usage != 0) return usage;
                }

                // Serialize the (possibly mutated) document and confirm it re-parses.
                byte[] roundtrippedBytes = new DataTableWriter(mutDoc).Serialize();
                DataTableDocument rtDoc = ParseTab(roundtrippedBytes); // structural-validity re-parse

                bool anyMutation = mutationCount == 1;
                bool bytesEqualUntouched;
                JToken firstMismatch = JValue.CreateNull();

                if (!anyMutation)
                {
                    // No-mutation case: the ONLY full-file byte-exact assertion (canonical fixtures).
                    bytesEqualUntouched = loadedBytes.Length == roundtrippedBytes.Length
                        && loadedBytes.SequenceEqual(roundtrippedBytes);
                }
                else
                {
                    bytesEqualUntouched = CompareUntouchedCellSlices(
                        loadedBytes, roundtrippedBytes, o, hasMutateCell, hasRemoveRow, hasRemoveColumn,
                        out firstMismatch);
                }

                var result = new JObject
                {
                    ["bytesEqualUntouched"] = bytesEqualUntouched,
                    ["comparisonGranularity"] = anyMutation ? "per-cell-rows-slice" : "whole-file",
                    ["cols"] = rtDoc.Mutable.Columns.Count,
                    ["firstMismatch"] = firstMismatch,
                    ["mutationApplied"] = mutationDescription ?? "none",
                    ["rows"] = rtDoc.Mutable.Rows.Count,
                    ["source"] = o.Path,
                    ["version"] = rtDoc.Mutable.Version
                };
                return JsonOutput.EmitSuccess("roundtrip-tab", result);
            }
            catch (DataTableParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-tab", "DataTableParseException", ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("roundtrip-tab", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("roundtrip-tab", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        // ─────────────────────────────────────────────────────────────────────
        // Parse helper: bytes -> IffReader.Read -> MutableIffDocument -> DataTableDocument.
        // ─────────────────────────────────────────────────────────────────────

        private static DataTableDocument ParseTab(byte[] bytes)
        {
            IffDocument iffDoc;
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                iffDoc = IffReader.Read(ms);
            }

            MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, bytes);
            return DataTableDocument.FromIff(mutableIff);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Mutation application (returns 0 on success, or a non-zero exit code on usage error).
        // ─────────────────────────────────────────────────────────────────────

        private static int ApplyMutateCell(RoundtripTabOptions o, MutableDataTableDocument mutDoc, out string mutationDescription)
        {
            mutationDescription = null;

            int row, col;
            if (!TryParseRowCol(o.MutateCell, out row, out col))
            {
                return JsonOutput.EmitError("roundtrip-tab", "UsageError",
                    "--mutate-cell must be a 'row,col' integer pair (0-based), e.g. 3,5; got '" + o.MutateCell + "'.", exitCode: 1);
            }

            if (row < 0 || row >= mutDoc.Rows.Count || col < 0 || col >= mutDoc.Columns.Count)
            {
                return JsonOutput.EmitError("roundtrip-tab", "UsageError",
                    "--mutate-cell (" + row + "," + col + ") is out of range (rows=" + mutDoc.Rows.Count
                    + ", cols=" + mutDoc.Columns.Count + ").", exitCode: 1);
            }

            DataTableColumnType ct = mutDoc.Columns[col].ColumnType;
            DataTableCellValue coerced;
            if (!ct.TryCoerceCellValue(o.MutateValue, out coerced))
            {
                // A coercion failure on a malformed value is a parse-class error per the behavior spec.
                throw new DataTableParseException(
                    "--mutate-value '" + o.MutateValue + "' could not be coerced to column type '"
                    + ct.TypeSpec + "' at cell (" + row + "," + col + ").");
            }

            // The public Value setter clears the cell's originalSlice (hybrid-DOM idiom), forcing a
            // fresh re-serialize for the edited cell only — every other cell re-emits its slice verbatim.
            mutDoc.Rows[row].Cells[col].Value = coerced;
            mutationDescription = "mutate-cell(" + row + "," + col + ")";
            return 0;
        }

        private static int ApplyRemoveRow(RoundtripTabOptions o, MutableDataTableDocument mutDoc, out string mutationDescription)
        {
            mutationDescription = null;
            int n = o.RemoveRow.Value;
            if (n < 0 || n >= mutDoc.Rows.Count)
            {
                return JsonOutput.EmitError("roundtrip-tab", "UsageError",
                    "--remove-row " + n + " is out of range (rows=" + mutDoc.Rows.Count + ").", exitCode: 1);
            }

            mutDoc.RemoveRowAt(n);
            mutationDescription = "remove-row(" + n + ")";
            return 0;
        }

        private static int ApplyRemoveColumn(RoundtripTabOptions o, MutableDataTableDocument mutDoc, out string mutationDescription)
        {
            mutationDescription = null;
            int idx = mutDoc.ResolveColumnIndex(o.RemoveColumn);
            if (idx < 0)
            {
                return JsonOutput.EmitError("roundtrip-tab", "UsageError",
                    "--remove-column '" + o.RemoveColumn + "' did not resolve to a column index or name.", exitCode: 1);
            }

            mutDoc.RemoveColumnAt(idx);
            mutationDescription = "remove-column(" + idx + ")";
            return 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Per-cell ROWS-slice comparison algorithm (iter-2 item 3).
        //
        // Re-parses BOTH byte arrays into fresh no-op-loaded documents (so every cell's originalSlice
        // reflects exactly the bytes on disk for that cell's ROWS payload), then pairs the UNTOUCHED
        // cells by stable (row,col) index with an explicit index-shift map for the structural removes:
        //
        //   --mutate-cell R,C : identity map; cell (R,C) is EXCLUDED, all others compared 1:1.
        //   --remove-row    N : loaded row r maps to rt row r          for r <  N
        //                       loaded row r maps to rt row r-1        for r >  N  (loaded row N removed)
        //   --remove-column N : loaded col c maps to rt col c          for c <  N
        //                       loaded col c maps to rt col c-1        for c >  N  (loaded col N removed)
        //
        // bytesEqualUntouched is true iff every paired untouched-cell slice is byte-identical.
        // ─────────────────────────────────────────────────────────────────────

        private static bool CompareUntouchedCellSlices(
            byte[] loadedBytes, byte[] roundtrippedBytes, RoundtripTabOptions o,
            bool hasMutateCell, bool hasRemoveRow, bool hasRemoveColumn,
            out JToken firstMismatch)
        {
            firstMismatch = JValue.CreateNull();

            MutableDataTableDocument loadedFresh = ParseTab(loadedBytes).Mutable;
            MutableDataTableDocument rtFresh = ParseTab(roundtrippedBytes).Mutable;

            int excludedRow = -1, excludedCol = -1;
            int removedRow = -1, removedCol = -1;
            if (hasMutateCell)
            {
                TryParseRowCol(o.MutateCell, out excludedRow, out excludedCol);
            }
            else if (hasRemoveRow)
            {
                removedRow = o.RemoveRow.Value;
            }
            else if (hasRemoveColumn)
            {
                // ResolveColumnIndex is re-run against the freshly loaded doc (pre-removal column set).
                removedCol = loadedFresh.ResolveColumnIndex(o.RemoveColumn);
            }

            for (int r = 0; r < loadedFresh.Rows.Count; r++)
            {
                if (hasRemoveRow && r == removedRow)
                {
                    continue; // the removed row has no counterpart in rtFresh
                }

                int rtRow = (hasRemoveRow && r > removedRow) ? r - 1 : r;
                IReadOnlyList<MutableDataTableCell> loadedCells = loadedFresh.Rows[r].Cells;
                IReadOnlyList<MutableDataTableCell> rtCells = rtFresh.Rows[rtRow].Cells;

                for (int c = 0; c < loadedCells.Count; c++)
                {
                    if (hasMutateCell && r == excludedRow && c == excludedCol)
                    {
                        continue; // the single edited cell is expected to differ
                    }

                    if (hasRemoveColumn && c == removedCol)
                    {
                        continue; // the removed column has no counterpart in rtFresh
                    }

                    int rtCol = (hasRemoveColumn && c > removedCol) ? c - 1 : c;
                    byte[] loadedSlice = loadedCells[c].GetOriginalSliceForCompare();
                    byte[] rtSlice = rtCells[rtCol].GetOriginalSliceForCompare();

                    if (!SlicesEqual(loadedSlice, rtSlice))
                    {
                        firstMismatch = new JObject
                        {
                            ["rowIndex"] = r,
                            ["colIndex"] = c
                        };
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool SlicesEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null) return b == null || b.Length == 0;
            if (b == null) return a.Length == 0;
            return a.Length == b.Length && a.SequenceEqual(b);
        }

        private static bool TryParseRowCol(string spec, out int row, out int col)
        {
            row = -1;
            col = -1;
            if (string.IsNullOrEmpty(spec)) return false;

            string[] parts = spec.Split(',');
            if (parts.Length != 2) return false;

            return int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out row)
                && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out col);
        }
    }
}
