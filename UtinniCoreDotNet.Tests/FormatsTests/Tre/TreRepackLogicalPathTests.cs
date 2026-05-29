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
using System.Text;
using Xunit;
using UtinniCoreDotNet.Formats.Tre;

namespace UtinniCoreDotNet.Tests.FormatsTests.Tre
{
    /// <summary>
    /// Phase 8 Plan 07 — edit→repack→reopen logical-path resolution test (the
    /// TreFile-side path-CRC self-consistency check).
    ///
    /// <para><b>What this test proves AND does NOT prove:</b>
    /// <list type="bullet">
    ///   <item><b>PROVES (CI-coverable, automation-side):</b> after editing record 2's
    ///         payload and repacking, re-opening the new archive via
    ///         <see cref="TreFile.Open(string)"/> yields a record at index 2 whose
    ///         <c>Name</c> is still <c>datatable/foo/bar.iff</c> AND whose <c>Checksum</c>
    ///         (the stored path CRC) is unchanged AND whose
    ///         <see cref="TreFile.GetRecordData"/> returns the new payload bytes.
    ///         This is the TreFile-side self-consistency: the writer preserves the
    ///         path and the path CRC across the edit (Open Q1/A1).</item>
    ///   <item><b>DOES NOT PROVE (live-client residual, cursor N-H1 ACK):</b> that
    ///         the actual SWG client resolves the edited record under its original
    ///         logical path. That requires a live client lookup and stays in the
    ///         Tier-4 smoke (Open Q1). Self-consistency in our own reader is a
    ///         necessary condition for the client ACK but not a sufficient one —
    ///         if SWG's path-CRC algorithm doesn't match ours, the client would
    ///         still fail to resolve. The CRC preservation here proves we ROUND-TRIP
    ///         the CRC byte; the CRC's correctness against SWG's algorithm is
    ///         covered by Phase 7's TreFile golden-fixture tests (which decode the
    ///         real SWGEmu .tre files and successfully resolve real assets).</item>
    /// </list></para>
    /// </summary>
    public class TreRepackLogicalPathTests
    {
        [Fact]
        public void Repack_EditRecord2_LogicalPathAndChecksumPreservedAfterReopen()
        {
            const string expectedPath = "datatable/foo/bar.iff";

            byte[] originalBytes = TreFileFixtures.BuildValidV0005FiveRecord();
            string originalPath = WriteTemp(originalBytes);
            string repackedPath = null;
            try
            {
                TreFile original = TreFile.Open(originalPath);
                Assert.Equal(5, original.Records.Count);
                // Confirm the fixture's record-2 logical path matches our expectation
                // — guards against drift if the fixture builder is later renamed.
                Assert.Equal(expectedPath, original.Records[2].Name);
                int originalRec2Checksum = original.Records[2].Checksum;

                // Edit record 2 with a payload whose content is clearly distinct from
                // the original so the round-trip can prove the new bytes round-tripped.
                byte[] newPayload = Encoding.ASCII.GetBytes(
                    "EDITED: this is the new payload for datatable/foo/bar.iff — "
                  + "edit→repack→reopen logical-path resolution test.");
                var edits = new Dictionary<int, byte[]> { { 2, newPayload } };

                byte[] repackedBytes = TreWriter.Repack(original, edits);
                repackedPath = WriteTemp(repackedBytes);

                // Re-open via TreFile (NOT carrying over the in-memory original
                // instance — we explicitly want to prove the bytes on disk parse
                // back correctly).
                TreFile repacked = TreFile.Open(repackedPath);
                Assert.Equal(5, repacked.Records.Count);

                // (1) Logical path preserved — TreFile.Records[2].Name still matches.
                Assert.Equal(expectedPath, repacked.Records[2].Name);

                // (2) Stored path CRC (Checksum) preserved — Open Q1/A1: path unchanged
                //     → CRC unchanged. This is what the live-SWG smoke would check
                //     against the client's lookup (cursor N-H1 ACK), but here we only
                //     prove the self-consistency: the writer didn't drop or recompute
                //     the CRC.
                Assert.Equal(originalRec2Checksum, repacked.Records[2].Checksum);

                // (3) Edited payload round-tripped.
                Assert.Equal(newPayload, repacked.GetRecordData(2));

                // (4) Defensive: the path-CRC for the edited record matches the
                //     path-CRC for the SAME record in the original (sanity that our
                //     "same path = same CRC" invariant holds across the writer).
                Assert.Equal(original.Records[2].Checksum, repacked.Records[2].Checksum);
            }
            finally
            {
                TryDelete(originalPath);
                if (repackedPath != null) TryDelete(repackedPath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string WriteTemp(byte[] bytes)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "tre-repack-logicalpath-" + Guid.NewGuid().ToString("N") + ".tre");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort */ }
        }
    }
}
