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
    /// Phase 8 Plan 07 Task 2 — TreWriter full-rebuild repack acceptance harness.
    ///
    /// <para><b>Two-guarantee + name-byte-identity contract (08-REVIEWS HIGH-1 + round-2 MEDIUM 6):</b>
    /// untouched entries are guaranteed under THREE distinct properties — (a) logical payload
    /// identity (<see cref="TreFile.GetRecordData"/> equality), (b) raw compressed slice identity
    /// (<see cref="TreFile.GetRecordCompressedBytes"/> equality), and (c) raw name byte identity
    /// (<see cref="TreFile.GetRecordNameBytes"/> equality). Only the edited entry recompresses.</para>
    ///
    /// <para><b>TOC invariant per-record assertion:</b> for every UNTOUCHED record, EVERY field in the
    /// extended invariant set (Checksum, NameOffset's name bytes, NameString, Compressor,
    /// UncompressedSize, CompressedSize) holds byte-for-byte across the rebuilt archive. Note:
    /// <see cref="TreRecord.Offset"/> is NOT in the invariant set — physical offsets necessarily
    /// change in a full rebuild (the new payload region lives at a different physical location).
    /// The semantic guarantee is that GetRecordCompressedBytes / GetRecordData return the same
    /// bytes; the offset is an opaque pointer into the new file.</para>
    /// </summary>
    public class TreWriterTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Fact 1: Edit-free repack — every record preserved byte-for-byte
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Repack_NoEdits_PreservesEveryTocInvariantAndPayload()
        {
            byte[] originalBytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string originalPath = WriteTemp(originalBytes);
            string repackedPath = null;
            try
            {
                TreFile original = TreFile.Open(originalPath);

                byte[] repackedBytes = TreWriter.Repack(original, new Dictionary<int, byte[]>());
                repackedPath = WriteTemp(repackedBytes);
                TreFile repacked = TreFile.Open(repackedPath);

                // Header invariants — record count + version tag.
                Assert.Equal(original.Records.Count, repacked.Records.Count);
                Assert.Equal(original.Header.VersionTag, repacked.Header.VersionTag);

                // Per-record invariant set — applied to EVERY record (no edits => all untouched).
                for (int i = 0; i < original.Records.Count; i++)
                {
                    AssertTocInvariantHolds(original, repacked, i, "Repack_NoEdits record " + i);
                }
            }
            finally
            {
                TryDelete(originalPath);
                if (repackedPath != null) TryDelete(repackedPath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact 2: Payload-only edit — edited entry mutates, every other entry preserved
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Repack_OneEditedEntry_OthersPreservedAndEditedEntryReturnsNewPayload()
        {
            byte[] originalBytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string originalPath = WriteTemp(originalBytes);
            string repackedPath = null;
            try
            {
                TreFile original = TreFile.Open(originalPath);

                // Edit record 1 (the deflate-compressed payload — exercises the
                // recompress branch).
                byte[] newPayload = Encoding.ASCII.GetBytes("REPACKED PAYLOAD content for record 1 - longer than original");
                var edits = new Dictionary<int, byte[]> { { 1, newPayload } };

                byte[] repackedBytes = TreWriter.Repack(original, edits);
                repackedPath = WriteTemp(repackedBytes);
                TreFile repacked = TreFile.Open(repackedPath);

                // Edited record: GetRecordData returns the new payload bytes.
                byte[] roundtripPayload = repacked.GetRecordData(1);
                Assert.Equal(newPayload, roundtripPayload);

                // Edited record: name unchanged (path is unchanged — see plan behavior block).
                Assert.Equal(original.Records[1].Name, repacked.Records[1].Name);
                Assert.Equal(original.Records[1].NameOffset, repacked.Records[1].NameOffset);

                // Edited record: Checksum preserved (path CRC, Open Q1/A1).
                Assert.Equal(original.Records[1].Checksum, repacked.Records[1].Checksum);

                // Edited record: UncompressedSize matches the new payload length.
                Assert.Equal(newPayload.Length, repacked.Records[1].UncompressedSize);

                // Untouched records (indexes 0 and 2): full TOC invariant set holds.
                AssertTocInvariantHolds(original, repacked, 0, "Repack_OneEdited record 0");
                AssertTocInvariantHolds(original, repacked, 2, "Repack_OneEdited record 2");
            }
            finally
            {
                TryDelete(originalPath);
                if (repackedPath != null) TryDelete(repackedPath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact 3: Explicit full TOC invariant per-record table — the 08-REVIEWS HIGH-1 +
        // round-2 MEDIUM 6 acceptance contract. Folds the helper used by Facts 1 & 2.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Repack_TocInvariantPerRecord_HoldsAcrossEditedAndUntouched()
        {
            byte[] originalBytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string originalPath = WriteTemp(originalBytes);
            string repackedPath = null;
            try
            {
                TreFile original = TreFile.Open(originalPath);
                Assert.Equal(3, original.Records.Count);

                byte[] newPayload = Encoding.ASCII.GetBytes("explicit-tocinvariant-edit");
                var edits = new Dictionary<int, byte[]> { { 0, newPayload } };

                byte[] repackedBytes = TreWriter.Repack(original, edits);
                repackedPath = WriteTemp(repackedBytes);
                TreFile repacked = TreFile.Open(repackedPath);

                // Untouched records (indexes 1 and 2): nine-invariant per-record table holds.
                AssertTocInvariantHolds(original, repacked, 1, "Fact3 untouched record 1");
                AssertTocInvariantHolds(original, repacked, 2, "Fact3 untouched record 2");

                // Edited record (index 0): partial invariants (name + checksum) still hold;
                // GetRecordData yields the new payload bytes.
                Assert.Equal(original.Records[0].Name, repacked.Records[0].Name);
                Assert.Equal(original.Records[0].Checksum, repacked.Records[0].Checksum);
                Assert.Equal(newPayload, repacked.GetRecordData(0));
                Assert.Equal(newPayload.Length, repacked.Records[0].UncompressedSize);
            }
            finally
            {
                TryDelete(originalPath);
                if (repackedPath != null) TryDelete(repackedPath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Per-record TOC invariant helper — the canonical 9-property check from the plan
        // <behavior> block:
        //   1) new.Checksum == old.Checksum
        //   2) new.NameOffset == old.NameOffset OR points at byte-identical name bytes
        //   3) new.NameString == old.NameString
        //   4) new.Compressor == old.Compressor
        //   5) new.UncompressedSize == old.UncompressedSize
        //   6) new.CompressedSize == old.CompressedSize
        //   7) GetRecordData(new, i).SequenceEqual(GetRecordData(old, i))
        //   8) GetRecordCompressedBytes(new, i).SequenceEqual(GetRecordCompressedBytes(old, i))
        //   9) GetRecordNameBytes(new, i).SequenceEqual(GetRecordNameBytes(old, i))   ← round-2 MEDIUM 6
        // ─────────────────────────────────────────────────────────────────────

        private static void AssertTocInvariantHolds(TreFile original, TreFile repacked, int index, string label)
        {
            var o = original.Records[index];
            var r = repacked.Records[index];

            // (1) Checksum (path CRC) preserved.
            Assert.True(o.Checksum == r.Checksum,
                label + ": Checksum mismatch (original=" + o.Checksum + ", repacked=" + r.Checksum + ")");

            // (3) NameString preserved.
            Assert.True(o.Name == r.Name,
                label + ": Name mismatch (original=" + o.Name + ", repacked=" + r.Name + ")");

            // (4) Compressor flag preserved.
            // CompressionKind is the public derived string; assert both forms.
            Assert.True(o.CompressionKind == r.CompressionKind,
                label + ": CompressionKind mismatch (original=" + o.CompressionKind + ", repacked=" + r.CompressionKind + ")");

            // (5) UncompressedSize preserved.
            Assert.True(o.UncompressedSize == r.UncompressedSize,
                label + ": UncompressedSize mismatch (original=" + o.UncompressedSize + ", repacked=" + r.UncompressedSize + ")");

            // (6) CompressedSize preserved.
            Assert.True(o.CompressedSize == r.CompressedSize,
                label + ": CompressedSize mismatch (original=" + o.CompressedSize + ", repacked=" + r.CompressedSize + ")");

            // (7) GetRecordData equality — guarantee (a) from the plan.
            byte[] oData = original.GetRecordData(index);
            byte[] rData = repacked.GetRecordData(index);
            Assert.True(BytesEqual(oData, rData),
                label + ": GetRecordData mismatch (originalLen=" + oData.Length + ", repackedLen=" + rData.Length + ")");

            // (8) GetRecordCompressedBytes equality — guarantee (b) from the plan
            //     (08-REVIEWS HIGH-1 raw-compressed-slice preservation).
            byte[] oRaw = original.GetRecordCompressedBytes(index);
            byte[] rRaw = repacked.GetRecordCompressedBytes(index);
            Assert.True(BytesEqual(oRaw, rRaw),
                label + ": GetRecordCompressedBytes mismatch (originalLen=" + oRaw.Length + ", repackedLen=" + rRaw.Length + ")");

            // (9) GetRecordNameBytes equality — round-2 MEDIUM 6 name-block byte-layout preservation.
            byte[] oName = original.GetRecordNameBytes(index);
            byte[] rName = repacked.GetRecordNameBytes(index);
            Assert.True(BytesEqual(oName, rName),
                label + ": GetRecordNameBytes mismatch (originalLen=" + oName.Length + ", repackedLen=" + rName.Length + ")");

            // (2) NameOffset round-trip — the offset numerically may match (when the writer lays
            // out the new name block in the same order as the original, which our writer does);
            // but the canonical guarantee per the plan is that "the new offset, computed correctly,
            // points at byte-identical name bytes" — (9) already verifies that.
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers (same temp-file pattern as the rest of FormatsTests/Tre)
        // ─────────────────────────────────────────────────────────────────────

        private static string WriteTemp(byte[] bytes)
        {
            string path = Path.Combine(Path.GetTempPath(), "tre-writer-" + Guid.NewGuid().ToString("N") + ".tre");
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
