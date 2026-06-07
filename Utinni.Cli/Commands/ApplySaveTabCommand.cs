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
// Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Datatable;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Saving;
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("apply-save-tab", HelpText = "Apply ONE typed datatable mutation to a loose-override .tab/.iff, verify untouched cells byte-identical, then atomically commit the mutated bytes.")]
    public class ApplySaveTabOptions
    {
        [Value(0, MetaName = "relAsset", Required = true, HelpText = "Relative asset path under --root (loose-override destination).")]
        public string RelAsset { get; set; }

        [Option("root", Required = true, HelpText = "Client root for the loose-override destination; relAsset is resolved + contained under it.")]
        public string Root { get; set; }

        [Option("mutate-cell", HelpText = "row,col index pair (0-based), e.g. 3,5. Requires --mutate-value.")]
        public string MutateCell { get; set; }

        [Option("mutate-value", HelpText = "Replacement value for --mutate-cell (string; coerced per column type).")]
        public string MutateValue { get; set; }

        [Option("remove-row", HelpText = "Row index (0-based) to remove.")]
        public int? RemoveRow { get; set; }

        [Option("remove-column", HelpText = "Column index (0-based) or column name to remove.")]
        public string RemoveColumn { get; set; }
    }

    /// <summary>
    /// Plan 14-03a (MCP-02 / AUTH-05): the typed-datatable member of the <c>apply-save-*</c> family.
    /// In ONE atomic operation it (1) resolves the loose-override destination under <c>--root</c>,
    /// (2) applies EXACTLY ONE typed mutation to the mutable DTII model (REUSING the same mutation
    /// semantics as <see cref="RoundtripTabCommand"/>), (3) serializes via
    /// <see cref="DataTableWriter"/>, (4) re-parses for structural validity, (5) verifies byte-identity
    /// on the UNTOUCHED per-cell ROWS slices, and — ONLY on a clean verify — (6)
    /// <c>WriteAtomic</c>-commits the SAME mutated bytes it just verified.
    ///
    /// <para>This is <b>NOT a typed DTII-only verb in disguise of the plain-IFF path</b>: it handles
    /// the typed DTII datatable semantics specifically (the plain/generic IFF leaf-edit path lives in
    /// <see cref="ApplySaveIffCommand"/>). The MCP host (14-03) wraps this 1:1 and decides
    /// persist-vs-fail on the EXIT CODE alone (0 ok / 2 verify-failed), never by parsing
    /// <c>bytesEqualUntouched</c>.</para>
    ///
    /// <para><b>Why this verb exists (the reviewer-confirmed gap):</b> <c>save</c> re-serializes the
    /// UNCHANGED on-disk file (persists the PRE-mutation bytes); <c>roundtrip-tab</c> verifies an edit
    /// in memory but DISCARDS the mutated bytes (never writes). Neither can "apply one typed edit AND
    /// persist it." This verb fuses apply + verify + commit so the persisted bytes are PROVABLY the
    /// post-mutation bytes (read-back tested), closing the TOCTOU window (verify and commit in ONE
    /// process, no re-load between them).</para>
    ///
    /// <para><b>Exit codes:</b> 0 ok; 1 usage (mutation missing/ambiguous, malformed args, or .tre
    /// input); 2 verify-failed | parse | path-containment; 3 file-not-found. Generic
    /// <see cref="System.Exception"/> is intentionally NOT caught.</para>
    /// </summary>
    public static class ApplySaveTabCommand
    {
        // Test-only seam (InternalsVisibleTo Utinni.Cli.Tests): perturbs the serialized bytes BEFORE
        // the untouched-region verify so a test can deterministically drive the failed-verify-no-write
        // path through the real CLI. Null in production. The threat-model gate T-14-08 (verify-before-
        // commit) is exercised by this hook flipping an untouched cell's byte and asserting NO write.
        internal static Func<byte[], byte[]> TestPerturbSerialized;

        public static int Run(ApplySaveTabOptions o)
        {
            bool hasMutateCell = !string.IsNullOrEmpty(o.MutateCell);
            bool hasRemoveRow = o.RemoveRow.HasValue;
            bool hasRemoveColumn = !string.IsNullOrEmpty(o.RemoveColumn);

            int mutationCount = (hasMutateCell ? 1 : 0) + (hasRemoveRow ? 1 : 0) + (hasRemoveColumn ? 1 : 0);
            if (mutationCount > 1)
            {
                return JsonOutput.EmitError("apply-save-tab", "UsageError",
                    "--mutate-cell, --remove-row, and --remove-column are mutually exclusive (supply exactly one).", exitCode: 1);
            }
            if (mutationCount == 0)
            {
                // apply-save REQUIRES a mutation (unlike roundtrip's no-op path) — it is a write verb.
                return JsonOutput.EmitError("apply-save-tab", "UsageError",
                    "apply-save-tab requires exactly one mutation: --mutate-cell+--mutate-value, --remove-row, or --remove-column.", exitCode: 1);
            }
            if (hasMutateCell && o.MutateValue == null)
            {
                return JsonOutput.EmitError("apply-save-tab", "UsageError",
                    "--mutate-cell requires --mutate-value.", exitCode: 1);
            }

            // Resolve loose-override destination under the root (rejects ../rooted -> exit 2).
            string destPath;
            try
            {
                destPath = LooseOverridePath.Resolve(o.Root, o.RelAsset);
            }
            catch (ArgumentException ex)
            {
                return JsonOutput.EmitError("apply-save-tab", "PathContainment", ex.Message, exitCode: 2);
            }

            if (!File.Exists(destPath))
            {
                return JsonOutput.EmitError("apply-save-tab", "FileNotFound",
                    "loose-override asset not found: " + destPath, exitCode: 3);
            }

            try
            {
                byte[] loadedBytes = File.ReadAllBytes(destPath);

                // .tre archives are never an apply-save destination (T-14-14): route to repack-tre.
                if (IsTreMagic(loadedBytes))
                {
                    return JsonOutput.EmitError("apply-save-tab", "UsageError",
                        ".tre archives are not an apply-save target; use the repack-tre verb (a backup-routed destructive rebuild).", exitCode: 1);
                }

                DataTableDocument dtDoc = ParseTab(loadedBytes);
                MutableDataTableDocument mutDoc = dtDoc.Mutable;

                string mutationDescription;
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
                else
                {
                    int usage = ApplyRemoveColumn(o, mutDoc, out mutationDescription);
                    if (usage != 0) return usage;
                }

                byte[] mutatedBytes = new DataTableWriter(mutDoc).Serialize();

                // Test-only perturbation seam (production: no-op).
                if (TestPerturbSerialized != null)
                {
                    mutatedBytes = TestPerturbSerialized(mutatedBytes);
                }

                // Structural-validity re-parse of the bytes we are about to commit (exit 2 on failure).
                DataTableDocument rtDoc;
                try
                {
                    rtDoc = ParseTab(mutatedBytes);
                }
                catch (Exception ex) when (ex is DataTableParseException || ex is IffParseException)
                {
                    return JsonOutput.EmitError("apply-save-tab", "VerifyFailed",
                        "serialized bytes failed to re-parse; nothing written: " + ex.Message, exitCode: 2);
                }

                JToken firstMismatch;
                bool bytesEqualUntouched = CompareUntouchedCellSlices(
                    loadedBytes, mutatedBytes, o, hasMutateCell, hasRemoveRow, hasRemoveColumn, out firstMismatch);

                if (!bytesEqualUntouched)
                {
                    // VERIFY-BEFORE-COMMIT (T-14-08): fail closed, write NOTHING.
                    return JsonOutput.EmitError("apply-save-tab", "VerifyFailed",
                        "untouched-region byte-identity check failed; nothing written.", exitCode: 2);
                }

                // Commit the SAME bytes we verified — no re-load, no re-serialize (T-14-15).
                SaveCommandIo.WriteAtomic(destPath, mutatedBytes);
                bool validated = rtDoc != null; // re-parse already succeeded above

                var result = new JObject
                {
                    ["backupPath"] = JValue.CreateNull(),
                    ["bytesEqualUntouched"] = true,
                    ["bytesWritten"] = mutatedBytes.Length,
                    ["comparisonGranularity"] = "per-cell-rows-slice",
                    ["firstMismatch"] = JValue.CreateNull(),
                    ["mutationApplied"] = mutationDescription,
                    ["path"] = destPath,
                    ["validated"] = validated,
                    ["written"] = true
                };
                return JsonOutput.EmitSuccess("apply-save-tab", result);
            }
            catch (DataTableParseException ex)
            {
                return JsonOutput.EmitError("apply-save-tab", "DataTableParseException", ex.Message, exitCode: 2);
            }
            catch (IffParseException ex)
            {
                return JsonOutput.EmitError("apply-save-tab", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
            catch (IOException ex)
            {
                return JsonOutput.EmitError("apply-save-tab", "IoError", ex.Message, exitCode: 2);
            }
            // NOTE: Generic Exception is intentionally NOT caught.
        }

        private static bool IsTreMagic(byte[] b)
        {
            return b.Length >= 4 && b[0] == (byte)'E' && b[1] == (byte)'E' && b[2] == (byte)'R' && b[3] == (byte)'T';
        }

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

        // ─── mutation application (REUSES the exact semantics of RoundtripTabCommand) ───

        private static int ApplyMutateCell(ApplySaveTabOptions o, MutableDataTableDocument mutDoc, out string mutationDescription)
        {
            mutationDescription = null;
            int row, col;
            if (!TryParseRowCol(o.MutateCell, out row, out col))
            {
                return JsonOutput.EmitError("apply-save-tab", "UsageError",
                    "--mutate-cell must be a 'row,col' integer pair (0-based), e.g. 3,5; got '" + o.MutateCell + "'.", exitCode: 1);
            }
            if (row < 0 || row >= mutDoc.Rows.Count || col < 0 || col >= mutDoc.Columns.Count)
            {
                return JsonOutput.EmitError("apply-save-tab", "UsageError",
                    "--mutate-cell (" + row + "," + col + ") is out of range (rows=" + mutDoc.Rows.Count
                    + ", cols=" + mutDoc.Columns.Count + ").", exitCode: 1);
            }
            DataTableColumnType ct = mutDoc.Columns[col].ColumnType;
            DataTableCellValue coerced;
            if (!ct.TryCoerceCellValue(o.MutateValue, out coerced))
            {
                throw new DataTableParseException(
                    "--mutate-value '" + o.MutateValue + "' could not be coerced to column type '"
                    + ct.TypeSpec + "' at cell (" + row + "," + col + ").");
            }
            mutDoc.Rows[row].Cells[col].Value = coerced;
            mutationDescription = "mutate-cell(" + row + "," + col + ")";
            return 0;
        }

        private static int ApplyRemoveRow(ApplySaveTabOptions o, MutableDataTableDocument mutDoc, out string mutationDescription)
        {
            mutationDescription = null;
            int n = o.RemoveRow.Value;
            if (n < 0 || n >= mutDoc.Rows.Count)
            {
                return JsonOutput.EmitError("apply-save-tab", "UsageError",
                    "--remove-row " + n + " is out of range (rows=" + mutDoc.Rows.Count + ").", exitCode: 1);
            }
            mutDoc.RemoveRowAt(n);
            mutationDescription = "remove-row(" + n + ")";
            return 0;
        }

        private static int ApplyRemoveColumn(ApplySaveTabOptions o, MutableDataTableDocument mutDoc, out string mutationDescription)
        {
            mutationDescription = null;
            int idx = mutDoc.ResolveColumnIndex(o.RemoveColumn);
            if (idx < 0)
            {
                return JsonOutput.EmitError("apply-save-tab", "UsageError",
                    "--remove-column '" + o.RemoveColumn + "' did not resolve to a column index or name.", exitCode: 1);
            }
            mutDoc.RemoveColumnAt(idx);
            mutationDescription = "remove-column(" + idx + ")";
            return 0;
        }

        // ─── per-cell ROWS-slice comparison (REUSES the RoundtripTabCommand algorithm) ───

        private static bool CompareUntouchedCellSlices(
            byte[] loadedBytes, byte[] mutatedBytes, ApplySaveTabOptions o,
            bool hasMutateCell, bool hasRemoveRow, bool hasRemoveColumn, out JToken firstMismatch)
        {
            firstMismatch = JValue.CreateNull();

            MutableDataTableDocument loadedFresh = ParseTab(loadedBytes).Mutable;
            MutableDataTableDocument rtFresh = ParseTab(mutatedBytes).Mutable;

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
                removedCol = loadedFresh.ResolveColumnIndex(o.RemoveColumn);
            }

            for (int r = 0; r < loadedFresh.Rows.Count; r++)
            {
                if (hasRemoveRow && r == removedRow) continue;

                int rtRow = (hasRemoveRow && r > removedRow) ? r - 1 : r;
                if (rtRow < 0 || rtRow >= rtFresh.Rows.Count)
                {
                    firstMismatch = new JObject { ["rowIndex"] = r, ["reason"] = "missing-row-after-mutate" };
                    return false;
                }
                IReadOnlyList<MutableDataTableCell> loadedCells = loadedFresh.Rows[r].Cells;
                IReadOnlyList<MutableDataTableCell> rtCells = rtFresh.Rows[rtRow].Cells;

                for (int c = 0; c < loadedCells.Count; c++)
                {
                    if (hasMutateCell && r == excludedRow && c == excludedCol) continue;
                    if (hasRemoveColumn && c == removedCol) continue;

                    int rtCol = (hasRemoveColumn && c > removedCol) ? c - 1 : c;
                    if (rtCol < 0 || rtCol >= rtCells.Count)
                    {
                        firstMismatch = new JObject { ["rowIndex"] = r, ["colIndex"] = c, ["reason"] = "missing-col-after-mutate" };
                        return false;
                    }
                    byte[] loadedSlice = loadedCells[c].GetOriginalSliceForCompare();
                    byte[] rtSlice = rtCells[rtCol].GetOriginalSliceForCompare();
                    if (!SlicesEqual(loadedSlice, rtSlice))
                    {
                        firstMismatch = new JObject { ["rowIndex"] = r, ["colIndex"] = c };
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
            row = -1; col = -1;
            if (string.IsNullOrEmpty(spec)) return false;
            string[] parts = spec.Split(',');
            if (parts.Length != 2) return false;
            return int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out row)
                && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out col);
        }
    }
}
