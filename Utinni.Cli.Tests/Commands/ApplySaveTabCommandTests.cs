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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Datatable;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Commands;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Plan 14-03a Task 1: golden tests for the <c>apply-save-tab</c> verb — the typed-datatable member
    /// of the apply-save-* family. The MANDATORY reviewer gates: (a) READ-BACK persistence (re-parse the
    /// WRITTEN file + assert the edited value + assert the file hash changed), and (b)
    /// failed-verify-no-write (a perturbed untouched cell -> exit 2 + on-disk file BYTE-UNCHANGED, no
    /// partial write). Plus the exit-code matrix (usage/containment/.tre/not-found).
    /// </summary>
    public sealed class ApplySaveTabCommandTests : IDisposable
    {
        private readonly string _work;

        public ApplySaveTabCommandTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "applysavetab_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            // Always clear the test seam so a failing test can't leak into the next.
            ApplySaveTabCommand.TestPerturbSerialized = null;
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort */ }
        }

        private string WriteLooseAsset(string rel, byte[] bytes)
        {
            string p = Path.Combine(_work, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllBytes(p, bytes);
            return p;
        }

        private static byte[] Sha(byte[] b)
        {
            using (var sha = SHA256.Create()) return sha.ComputeHash(b);
        }

        private static JObject RunApply(out int exit, params string[] args)
        {
            CliResult r = InProcessCliRunner.Run(args);
            exit = r.ExitCode;
            return JObject.Parse(r.Stdout);
        }

        private static DataTableDocument Parse(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                var iff = IffReader.Read(ms);
                return DataTableDocument.FromIff(MutableIffDocument.FromDocument(iff, bytes));
            }
        }

        // ── happy: mutate-cell persists the edit (READ-BACK + hash changed) ─────

        [Fact]
        public void ApplySaveTab_MutateCell_PersistsEdit_ReadBackReflectsValue_HashChanged()
        {
            const string rel = "datatables/combat.iff";
            string asset = WriteLooseAsset(rel, DataTableFixtureBuilder.BuildV1AllTypes());
            byte[] before = File.ReadAllBytes(asset);

            JObject env = RunApply(out int exit, "apply-save-tab", rel, "--root", _work, "--mutate-cell", "0,0", "--mutate-value", "999");

            Assert.Equal(0, exit);
            JObject result = (JObject)env["result"];
            Assert.True((bool)result["written"]);
            Assert.True((bool)result["bytesEqualUntouched"]);
            Assert.True((bool)result["validated"]);
            Assert.Equal("mutate-cell(0,0)", (string)result["mutationApplied"]);
            Assert.Equal(Path.GetFullPath(asset), (string)result["path"]);
            Assert.Equal(JTokenType.Null, result["backupPath"].Type);

            // READ-BACK: the WRITTEN file re-parses to reflect the edited value.
            byte[] after = File.ReadAllBytes(asset);
            DataTableDocument reparsed = Parse(after);
            var reparsedCol = reparsed.Mutable.Columns[0].ColumnType;
            string cellText = reparsed.Mutable.Rows[0].Cells[0].Value.ToCsvString(reparsedCol);
            Assert.Equal("999", cellText);

            // The persisted bytes are the POST-mutation bytes (hash differs from pre-edit).
            Assert.False(Sha(before).SequenceEqual(Sha(after)), "the written file must differ from the pre-edit file");
            Assert.Equal((int)result["bytesWritten"], after.Length);
        }

        // ── happy: remove-row / remove-column persist + untouched cells preserved ──

        [Fact]
        public void ApplySaveTab_RemoveRow_PersistsRemoval_ReadBackHasOneFewerRow()
        {
            const string rel = "datatables/big.iff";
            string asset = WriteLooseAsset(rel, DataTableFixtureBuilder.BuildV1CombatDataTableLike());
            int rowsBefore = Parse(File.ReadAllBytes(asset)).Mutable.Rows.Count;

            JObject env = RunApply(out int exit, "apply-save-tab", rel, "--root", _work, "--remove-row", "50");

            Assert.Equal(0, exit);
            Assert.True((bool)((JObject)env["result"])["written"]);

            int rowsAfter = Parse(File.ReadAllBytes(asset)).Mutable.Rows.Count;
            Assert.Equal(rowsBefore - 1, rowsAfter);
        }

        [Fact]
        public void ApplySaveTab_RemoveColumn_PersistsRemoval_ReadBackHasOneFewerColumn()
        {
            const string rel = "datatables/types.iff";
            string asset = WriteLooseAsset(rel, DataTableFixtureBuilder.BuildV1AllTypes());
            int colsBefore = Parse(File.ReadAllBytes(asset)).Mutable.Columns.Count;

            JObject env = RunApply(out int exit, "apply-save-tab", rel, "--root", _work, "--remove-column", "2");

            Assert.Equal(0, exit);
            Assert.True((bool)((JObject)env["result"])["written"]);

            int colsAfter = Parse(File.ReadAllBytes(asset)).Mutable.Columns.Count;
            Assert.Equal(colsBefore - 1, colsAfter);
        }

        // ── failed-verify-no-write: perturb an untouched cell -> exit 2 + file unchanged ──

        [Fact]
        public void ApplySaveTab_FailedUntouchedVerify_ExitsTwo_AndFileIsByteUnchanged()
        {
            const string rel = "datatables/combat.iff";
            string asset = WriteLooseAsset(rel, DataTableFixtureBuilder.BuildV1AllTypes());
            byte[] before = File.ReadAllBytes(asset);

            // Inject a writer that mutates an UNTOUCHED region of the serialized bytes: re-serialize
            // a SECOND mutation on top so a cell other than (0,0) also differs -> the untouched-region
            // verify must trip and the verb must write NOTHING.
            ApplySaveTabCommand.TestPerturbSerialized = bytes =>
            {
                using (var ms = new MemoryStream(bytes, writable: false))
                {
                    var iff = IffReader.Read(ms);
                    var dt = DataTableDocument.FromIff(MutableIffDocument.FromDocument(iff, bytes));
                    var mut = dt.Mutable;
                    // Perturb a DIFFERENT cell (0,1 = the float column) than the verb's
                    // --mutate-cell 0,0 target -> the untouched-region verify must trip.
                    var col = mut.Columns[1].ColumnType;
                    col.TryCoerceCellValue("123.5", out DataTableCellValue v);
                    mut.Rows[0].Cells[1].Value = v;
                    return new DataTableWriter(mut).Serialize();
                }
            };

            JObject env = RunApply(out int exit, "apply-save-tab", rel, "--root", _work, "--mutate-cell", "0,0", "--mutate-value", "999");

            Assert.Equal(2, exit);
            Assert.Equal("VerifyFailed", (string)((JObject)env["error"])["kind"]);

            // The on-disk file is BYTE-UNCHANGED — no partial/corrupt write.
            byte[] after = File.ReadAllBytes(asset);
            Assert.True(before.SequenceEqual(after), "a failed verify must leave the file byte-unchanged");
        }

        // ── usage / containment / .tre / not-found ─────────────────────────────

        [Fact]
        public void ApplySaveTab_NoMutation_ExitsOne_UsageError()
        {
            const string rel = "datatables/x.iff";
            WriteLooseAsset(rel, DataTableFixtureBuilder.BuildV1Minimal());
            JObject env = RunApply(out int exit, "apply-save-tab", rel, "--root", _work);
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveTab_TraversalEscape_ExitsTwo_PathContainment()
        {
            JObject env = RunApply(out int exit, "apply-save-tab", "../escape.iff", "--root", _work, "--mutate-cell", "0,0", "--mutate-value", "1");
            Assert.Equal(2, exit);
            Assert.Equal("PathContainment", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveTab_TreInput_ExitsOne_PointsAtRepackTre()
        {
            const string rel = "archive.tre";
            string asset = Path.Combine(_work, rel);
            TreFixtureBuilder.WriteSynthetic5000(asset);

            JObject env = RunApply(out int exit, "apply-save-tab", rel, "--root", _work, "--mutate-cell", "0,0", "--mutate-value", "1");
            Assert.Equal(1, exit);
            JObject error = (JObject)env["error"];
            Assert.Equal("UsageError", (string)error["kind"]);
            Assert.Contains("repack-tre", (string)error["message"]);
        }

        [Fact]
        public void ApplySaveTab_MissingFile_ExitsThree()
        {
            JObject env = RunApply(out int exit, "apply-save-tab", "datatables/nope.iff", "--root", _work, "--mutate-cell", "0,0", "--mutate-value", "1");
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }

        // ── envelope-shape regression guard ────────────────────────────────────

        [Fact]
        public void ApplySaveTab_HappyPath_EnvelopeHasTopLevelSchemaVersionAndCommand()
        {
            const string rel = "datatables/x.iff";
            WriteLooseAsset(rel, DataTableFixtureBuilder.BuildV1AllTypes());
            JObject env = RunApply(out int exit, "apply-save-tab", rel, "--root", _work, "--mutate-cell", "0,0", "--mutate-value", "5");
            Assert.Equal(0, exit);
            Assert.Equal(1, (int)env["schemaVersion"]);
            Assert.Equal("apply-save-tab", (string)env["command"]);
            Assert.Null(env["result"]["schemaVersion"]);
            Assert.Null(env["error"]);
        }
    }
}
