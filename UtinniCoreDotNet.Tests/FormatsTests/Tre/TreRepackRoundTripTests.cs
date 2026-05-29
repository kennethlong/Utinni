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
    /// Phase 8 Plan 07 — repack round-trip byte-identity automation harness.
    ///
    /// <para>This test class is part of the 5-class automation suite that augments
    /// Plan 07 Task 4's live-SWG smoke. The maintainer requested that the on-disk
    /// repack contract be CI-covered against fixture archives wherever possible, so
    /// the live-client residual shrinks to only what genuinely requires SWG to be
    /// running (cursor N-H1 ACK + UI end-to-end).</para>
    ///
    /// <para><b>What this class proves:</b> on a synthetic 5-record fixture, editing
    /// record 2 and repacking via <see cref="TreWriter.Repack"/> produces an archive
    /// whose:
    /// <list type="bullet">
    ///   <item>Record count is preserved.</item>
    ///   <item>For records 0, 1, 3, 4 — the raw compressed slice
    ///         (<see cref="TreFile.GetRecordCompressedBytes"/>) AND the raw name slice
    ///         (<see cref="TreFile.GetRecordNameBytes"/>) are byte-identical to the
    ///         original (08-REVIEWS HIGH-1 guarantee (b) + round-2 MEDIUM 6 name-block
    ///         byte-layout preservation).</item>
    ///   <item>Record 2 — its <see cref="TreFile.GetRecordData"/> equals the new
    ///         payload bytes; its raw compressed slice will differ from the original
    ///         (it recompresses).</item>
    /// </list></para>
    ///
    /// <para><b>Both same-length AND different-length payload edits are exercised</b>
    /// — same-length proves the writer doesn't accidentally treat the edited entry
    /// as untouched when the byte count happens to match; different-length proves
    /// the payload-region rebuild correctly relocates subsequent entries.</para>
    /// </summary>
    public class TreRepackRoundTripTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Fact 1: edit record 2 with a SAME-LENGTH payload — untouched records
        // (0, 1, 3, 4) are byte-identical (raw compressed slice + name bytes);
        // edited record 2 round-trips to the new payload.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Repack_EditRecord2_SameLengthPayload_UntouchedRecordsByteIdentical()
        {
            byte[] originalBytes = TreFileFixtures.BuildValidV0005FiveRecord();
            string originalPath = WriteTemp(originalBytes);
            string repackedPath = null;
            try
            {
                TreFile original = TreFile.Open(originalPath);
                Assert.Equal(5, original.Records.Count);

                // Same-length swap: original record 2 payload is "RECORD TWO content (target)"
                // (27 bytes). Construct a deliberately same-length replacement so the
                // payload-region byte count doesn't shift.
                byte[] originalRec2 = original.GetRecordData(2);
                byte[] newPayload = new byte[originalRec2.Length];
                for (int i = 0; i < newPayload.Length; i++)
                {
                    // Fill with a stable, non-trivial pattern so an accidental
                    // unmodified write would be detectable.
                    newPayload[i] = (byte)('A' + (i % 26));
                }
                Assert.Equal(originalRec2.Length, newPayload.Length);
                Assert.False(BytesEqual(originalRec2, newPayload),
                    "Same-length sentinel must differ from the original payload bytes.");

                var edits = new Dictionary<int, byte[]> { { 2, newPayload } };
                byte[] repackedBytes = TreWriter.Repack(original, edits);
                repackedPath = WriteTemp(repackedBytes);
                TreFile repacked = TreFile.Open(repackedPath);

                Assert.Equal(5, repacked.Records.Count);

                // Untouched records (0, 1, 3, 4): raw compressed slice + raw name bytes
                // byte-identical (08-REVIEWS HIGH-1 + round-2 MEDIUM 6).
                foreach (int i in new[] { 0, 1, 3, 4 })
                {
                    byte[] oRaw = original.GetRecordCompressedBytes(i);
                    byte[] rRaw = repacked.GetRecordCompressedBytes(i);
                    Assert.True(BytesEqual(oRaw, rRaw),
                        "SameLength: record " + i + " raw compressed bytes differ");

                    byte[] oName = original.GetRecordNameBytes(i);
                    byte[] rName = repacked.GetRecordNameBytes(i);
                    Assert.True(BytesEqual(oName, rName),
                        "SameLength: record " + i + " raw name bytes differ");

                    Assert.Equal(original.Records[i].Name, repacked.Records[i].Name);
                    Assert.Equal(original.Records[i].Checksum, repacked.Records[i].Checksum);
                }

                // Edited record 2: GetRecordData returns the new payload.
                Assert.Equal(newPayload, repacked.GetRecordData(2));
                // Name + Checksum (path CRC, Open Q1/A1) preserved on the edited entry.
                Assert.Equal(original.Records[2].Name, repacked.Records[2].Name);
                Assert.Equal(original.Records[2].Checksum, repacked.Records[2].Checksum);
            }
            finally
            {
                TryDelete(originalPath);
                if (repackedPath != null) TryDelete(repackedPath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact 2: edit record 2 with a DIFFERENT-LENGTH payload — proves the
        // payload-region rebuild correctly handles the size delta; untouched
        // records (0, 1, 3, 4) still byte-identical.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Repack_EditRecord2_DifferentLengthPayload_UntouchedRecordsByteIdentical()
        {
            byte[] originalBytes = TreFileFixtures.BuildValidV0005FiveRecord();
            string originalPath = WriteTemp(originalBytes);
            string repackedPath = null;
            try
            {
                TreFile original = TreFile.Open(originalPath);
                Assert.Equal(5, original.Records.Count);

                // Different-length: replace with a much-larger payload so subsequent
                // entries in the payload region would visibly shift if the writer
                // mishandled offsets.
                byte[] newPayload = Encoding.ASCII.GetBytes(
                    "EDITED record 2 with a deliberately LONGER payload that exceeds the original "
                  + "27-byte size so the payload-region layout has to re-flow records 3 and 4.");

                byte[] originalRec2 = original.GetRecordData(2);
                Assert.NotEqual(originalRec2.Length, newPayload.Length);

                var edits = new Dictionary<int, byte[]> { { 2, newPayload } };
                byte[] repackedBytes = TreWriter.Repack(original, edits);
                repackedPath = WriteTemp(repackedBytes);
                TreFile repacked = TreFile.Open(repackedPath);

                Assert.Equal(5, repacked.Records.Count);

                // Untouched records: raw compressed slice + raw name bytes byte-identical.
                foreach (int i in new[] { 0, 1, 3, 4 })
                {
                    byte[] oRaw = original.GetRecordCompressedBytes(i);
                    byte[] rRaw = repacked.GetRecordCompressedBytes(i);
                    Assert.True(BytesEqual(oRaw, rRaw),
                        "DifferentLength: record " + i + " raw compressed bytes differ");

                    byte[] oName = original.GetRecordNameBytes(i);
                    byte[] rName = repacked.GetRecordNameBytes(i);
                    Assert.True(BytesEqual(oName, rName),
                        "DifferentLength: record " + i + " raw name bytes differ");

                    // Logical payload identity for untouched records.
                    byte[] oData = original.GetRecordData(i);
                    byte[] rData = repacked.GetRecordData(i);
                    Assert.True(BytesEqual(oData, rData),
                        "DifferentLength: record " + i + " GetRecordData differs");
                }

                // Edited record 2: GetRecordData returns the new payload (length differs).
                Assert.Equal(newPayload, repacked.GetRecordData(2));
                Assert.Equal(newPayload.Length, repacked.Records[2].UncompressedSize);
            }
            finally
            {
                TryDelete(originalPath);
                if (repackedPath != null) TryDelete(repackedPath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers (same temp-file pattern as the rest of FormatsTests/Tre)
        // ─────────────────────────────────────────────────────────────────────

        private static string WriteTemp(byte[] bytes)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "tre-repack-roundtrip-" + Guid.NewGuid().ToString("N") + ".tre");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort */ }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
