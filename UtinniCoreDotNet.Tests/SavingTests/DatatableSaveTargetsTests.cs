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
using System.IO;
using UtinniCoreDotNet.Formats.Datatable;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Tre;
using UtinniCoreDotNet.Saving;
using UtinniCoreDotNet.Tests.FormatsTests.Datatable;
using UtinniCoreDotNet.Tests.FormatsTests.Tre;
using Xunit;

namespace UtinniCoreDotNet.Tests.SavingTests
{
    /// <summary>
    /// Plan 09-05 save-target composition contract — exercised at the FRAMEWORK layer the
    /// plugin-side <c>TJT.Saving.DatatableSaveTargets</c> shim composes on.
    ///
    /// <para><b>Why framework-layer, not the plugin shim directly:</b> <c>DatatableSaveTargets</c>
    /// is a &lt; 100-line composition shim in the UtinniPlugins (TJT) assembly. It builds the
    /// intermediate <see cref="MutableIffDocument"/> via <see cref="DataTableWriter.BuildMutableIff"/>
    /// then forwards verbatim to Phase 8's <c>IffSaveTargets</c> (modes 1/2/3) and
    /// <c>TreRepackSaveTarget</c> (mode 4). This test project does NOT (and cannot cleanly)
    /// reference the WinForms/native-binding TJT assembly — the same constraint under which Phase 8
    /// unit-tested its save plumbing at the framework layer (<see cref="LooseOverridePathTests"/>,
    /// <c>TreWriterTests</c>) and smoke-tested the plugin wrappers (Phase 8 Plan 05 Task 5).
    /// These facts therefore pin the EXACT primitives the shim chains so a regression in any
    /// composed leg surfaces against a 09-05-named test (Rule 3 deviation — documented in SUMMARY).</para>
    ///
    /// <para>Covered legs: (a) DTII build → IffWriter byte-exactness (SaveToPath / SaveInPlace
    /// serialize path); (b) per-cell untouched preservation after one edit (SC4 at the save-shim
    /// layer); (c) <see cref="LooseOverridePath.Resolve"/> root-containment (SaveLooseOverride path);
    /// (d) <c>TreWriter.Repack</c> V6000 refusal on a DTII payload (Phase 8 WR-06 inheritance);
    /// (e) valid-archive repack-with-DTII-payload success + reopen (RepackIntoSourceTre happy path).</para>
    /// </summary>
    public class DatatableSaveTargetsTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static DataTableDocument Load(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                IffDocument doc = IffReader.Read(ms);
                MutableIffDocument mut = MutableIffDocument.FromDocument(doc, bytes);
                return DataTableDocument.FromIff(mut);
            }
        }

        // Mirrors the shim's SaveToPath/SaveInPlace serialize leg: build the intermediate
        // MutableIffDocument the shim hands to IffSaveTargets, then serialize via the SAME
        // IffWriter.Write the Phase 8 save target's WriteAtomic uses.
        private static byte[] BuildShimBytes(DataTableDocument dt)
        {
            MutableIffDocument mIff = new DataTableWriter(dt.Mutable).BuildMutableIff();
            return IffWriter.Write(mIff);
        }

        // ── (a) DTII build → IffWriter byte-exact (no edits) ─────────────────

        [Fact]
        public void SaveToPath_RoundTripsByteExact_NoEdits()
        {
            // The SaveToPath leg builds the MutableIffDocument via DataTableWriter then writes it
            // through IffWriter.Write. With no edits, the bytes the shim would write must equal the
            // canonical fixture bytes (full-file byte-exact — the byte-exact scope).
            byte[] original = DatatableFixtures.BuildV1AllTypes();
            DataTableDocument dt = Load(original);

            byte[] shimBytes = BuildShimBytes(dt);

            Assert.Equal(original, shimBytes);
        }

        // ── (b) Per-cell untouched preservation after one edit (SC4) ─────────

        [Fact]
        public void SaveToPath_PreservesByteExactOnUntouchedCellsAfterOneEdit()
        {
            // Edit cell (0,0); the shim-built bytes must re-emit every UNTOUCHED cell's ROWS slice
            // identically to the original (SC4 at the save-shim layer). We compare the re-parsed
            // documents cell-by-cell, asserting equality on every cell except the edited one and
            // a change on the edited one.
            byte[] original = DatatableFixtures.BuildV1AllTypes();
            DataTableDocument dt = Load(original);

            MutableDataTableCell edited = dt.Mutable.Rows[0].Cells[0]; // "anInt" = 7
            edited.Value = DataTableCellValue.FromInt(999);

            byte[] shimBytes = BuildShimBytes(dt);
            DataTableDocument reparsed = Load(shimBytes);

            // The edited cell changed.
            var editedCell = reparsed.Mutable.Rows[0].Cells[0].Value as DataTableCellValue.IntCellValue;
            Assert.NotNull(editedCell);
            Assert.Equal(999, editedCell.Value);

            // Every other cell in row 0 round-trips value-equal to the original parse (untouched-cell
            // preservation — the shim's DataTableWriter must not perturb neighbours).
            DataTableDocument originalParsed = Load(original);
            IReadOnlyList<MutableDataTableCell> origCells = originalParsed.Mutable.Rows[0].Cells;
            IReadOnlyList<MutableDataTableCell> newCells = reparsed.Mutable.Rows[0].Cells;
            for (int c = 1; c < origCells.Count; c++)
            {
                Assert.True(DataTableCellValue.Equals(origCells[c].Value, newCells[c].Value),
                    "Untouched cell " + c + " changed across the shim serialize.");
            }
        }

        // ── (c) LooseOverridePath.Resolve root-containment (SaveLooseOverride) ──

        [Fact]
        public void SaveLooseOverride_UsesLooseOverridePathResolve_StaysUnderRoot()
        {
            // The SaveLooseOverride leg composes the destination via LooseOverridePath.Resolve
            // (the Phase 8 root-containment helper) before handing the MutableIffDocument to
            // IffSaveTargets. A TreArchive source's LogicalPath ("datatables/foo.tab") must resolve
            // UNDER the override base — never escaping the client root (path-traversal defense).
            string root = Path.Combine(Path.GetTempPath(), "utinni-09-05-loose-" + Guid.NewGuid().ToString("N"));
            string resolved = LooseOverridePath.Resolve(root, "datatables/foo.tab");

            string normalizedRoot = Path.GetFullPath(root);
            Assert.StartsWith(normalizedRoot, resolved, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("foo.tab", resolved, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SaveLooseOverride_RejectsTraversalEscape()
        {
            // The loose-override leg must reject a logical path that tries to climb out of the root
            // (the shim inherits this defense verbatim — no new path handling in Phase 9).
            string root = Path.Combine(Path.GetTempPath(), "utinni-09-05-loose-" + Guid.NewGuid().ToString("N"));
            Assert.Throws<ArgumentException>(() =>
                LooseOverridePath.Resolve(root, "../../escape.tab"));
        }

        // ── (d) TreWriter.Repack V6000 refusal on a DTII payload (WR-06) ─────

        [Fact]
        public void RepackIntoSourceTre_V6000Archive_RefusedWithLooseOverrideRecommendation()
        {
            // WR-06 inheritance fact: the RepackIntoSourceTre leg hands DataTableWriter.Serialize()
            // bytes (a DTII payload) to TreRepackSaveTarget.Apply, which calls TreWriter.Repack.
            // For a V6000 (enumerate-only / encrypted) archive, TreWriter.Repack throws
            // NotSupportedException — the plugin save target catches it and returns Failed, while
            // the FormDatatableEditor surfaces the "use loose override" copy. This fact pins the
            // framework refusal the whole chain depends on, using a real DTII payload.
            byte[] dtiiPayload = new DataTableWriter(Load(DatatableFixtures.BuildV1Minimal()).Mutable).Serialize();

            // A 0-record V6000 header is sufficient — the EnumerateOnly guard fires before the
            // record loop (Phase 8 BuildEmptyV6000 precedent).
            byte[] v6000Bytes = TreFileFixtures.BuildEmptyV6000();
            string trePath = Path.Combine(Path.GetTempPath(), "utinni-09-05-v6000-" + Guid.NewGuid().ToString("N") + ".tre");
            File.WriteAllBytes(trePath, v6000Bytes);
            try
            {
                TreFile original = TreFile.Open(trePath);
                var edits = new Dictionary<int, byte[]> { { 0, dtiiPayload } };

                NotSupportedException refusal = Assert.Throws<NotSupportedException>(() =>
                    TreWriter.Repack(original, edits));
                // The refusal message must steer the user to the loose-override escape hatch.
                Assert.Contains("loose override", refusal.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                SafeDelete(trePath);
            }
        }

        // ── (e) Valid-archive repack with a DTII payload succeeds + reopens ──

        [Fact]
        public void RepackIntoSourceTre_ValidArchive_DtiiPayloadRoundTrips()
        {
            // The RepackIntoSourceTre happy path: a non-V6000 archive + a DTII payload repacks via
            // TreWriter.Repack, and the rebuilt archive reopens with the DTII bytes resolvable at
            // the edited record. Untouched records stay byte-identical (the raw-slice copy path).
            byte[] dtiiPayload = new DataTableWriter(Load(DatatableFixtures.BuildV1AllTypes()).Mutable).Serialize();

            byte[] archiveBytes = TreFileFixtures.BuildValidV0005FiveRecord();
            string trePath = Path.Combine(Path.GetTempPath(), "utinni-09-05-v0005-" + Guid.NewGuid().ToString("N") + ".tre");
            File.WriteAllBytes(trePath, archiveBytes);
            try
            {
                TreFile original = TreFile.Open(trePath);
                const int editIndex = 2; // "datatable/foo/bar.iff" — the documented EDITED entry
                byte[] origUntouched = original.GetRecordData(0);

                var edits = new Dictionary<int, byte[]> { { editIndex, dtiiPayload } };
                byte[] repacked = TreWriter.Repack(original, edits);

                // Reopen the rebuilt archive FROM A PATH (payload reads require a stored source path;
                // Open(Stream) is metadata-only) and confirm the DTII payload is at the edited record
                // + an untouched record is preserved byte-for-byte.
                string repackedPath = Path.Combine(Path.GetTempPath(), "utinni-09-05-repacked-" + Guid.NewGuid().ToString("N") + ".tre");
                File.WriteAllBytes(repackedPath, repacked);
                try
                {
                    TreFile rebuilt = TreFile.Open(repackedPath);
                    Assert.Equal(dtiiPayload, rebuilt.GetRecordData(editIndex));
                    Assert.Equal(origUntouched, rebuilt.GetRecordData(0));

                    // The rebuilt DTII record re-parses as a valid datatable (the bytes are a real
                    // DTII carrier, not just opaque blob bytes).
                    DataTableDocument reparsed = Load(rebuilt.GetRecordData(editIndex));
                    Assert.True(reparsed.Mutable.Columns.Count > 0);
                }
                finally
                {
                    SafeDelete(repackedPath);
                }
            }
            finally
            {
                SafeDelete(trePath);
            }
        }

        // ── (f) Build-mutable identity: the two serialize legs agree ─────────

        [Fact]
        public void BuildMutableIff_AndSerialize_ProduceIdenticalBytes()
        {
            // The shim uses BuildMutableIff() for the IffSaveTargets legs (save-to-file) and
            // Serialize() for the RepackIntoSourceTre leg (raw bytes). Both must produce identical
            // bytes so the save mode chosen does not change what lands on disk.
            DataTableDocument dt = Load(DatatableFixtures.BuildV1WithComment());

            byte[] viaBuildMutable = IffWriter.Write(new DataTableWriter(dt.Mutable).BuildMutableIff());
            byte[] viaSerialize = new DataTableWriter(dt.Mutable).Serialize();

            Assert.Equal(viaSerialize, viaBuildMutable);
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // best-effort temp cleanup
            }
        }
    }
}
