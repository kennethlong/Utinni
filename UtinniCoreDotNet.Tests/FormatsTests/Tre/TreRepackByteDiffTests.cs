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
    /// Phase 8 Plan 07 — overall-bytes-differ + per-record untouched-slice
    /// byte-identity automation harness.
    ///
    /// <para><b>What this class proves:</b> after editing record 2 and repacking
    /// the synthetic 5-record fixture:
    /// <list type="bullet">
    ///   <item><see cref="Repack_OverallBytesDiffer_PostEdit"/> — the full byte
    ///         array of the repacked archive differs from the original. (Sanity
    ///         that an edit actually changes the file on disk; guards against a
    ///         degenerate writer that silently no-ops.)</item>
    ///   <item><see cref="Repack_UntouchedRecord_GetRecordCompressedBytesIdentical_AcrossOriginalAndRepacked"/>
    ///         — parameterized over the four untouched record indices (0, 1, 3, 4):
    ///         <see cref="TreFile.GetRecordCompressedBytes"/> on the repacked archive
    ///         is byte-identical to the same call on the original. (08-REVIEWS HIGH-1
    ///         guarantee (b) — raw compressed slice identity.)</item>
    /// </list></para>
    ///
    /// <para><b>Relationship to TreWriterTests:</b> TreWriterTests already exercises
    /// the full TOC invariant + raw-name-byte identity contract via the 9-property
    /// helper. This class is the focused "overall-bytes-differ + per-untouched-record
    /// slice-identity" cross-section called out by the round-2 review process. The
    /// Theory parameterization makes the per-index assertion appear as 4 distinct
    /// test outcomes in CI, so a regression on a single index produces an immediately
    /// actionable failure name (e.g. "...Index=3").</para>
    /// </summary>
    public class TreRepackByteDiffTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Fact: post-edit, the repacked archive's bytes differ from the original
        // archive's bytes. Sanity guard for the writer.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Repack_OverallBytesDiffer_PostEdit()
        {
            byte[] originalBytes = TreFileFixtures.BuildValidV0005FiveRecord();
            string originalPath = WriteTemp(originalBytes);
            try
            {
                TreFile original = TreFile.Open(originalPath);
                Assert.Equal(5, original.Records.Count);

                byte[] newPayload = Encoding.ASCII.GetBytes(
                    "EDITED record 2: distinct sentinel content the original cannot match.");

                var edits = new Dictionary<int, byte[]> { { 2, newPayload } };
                byte[] repackedBytes = TreWriter.Repack(original, edits);

                // The two byte arrays MUST differ — both length and content can vary,
                // but if they happen to match exactly the writer is broken (it would
                // mean the edit didn't reach the on-disk representation).
                bool identical = BytesEqual(originalBytes, repackedBytes);
                Assert.False(identical, "Repacked bytes are identical to original — edit did not propagate.");
            }
            finally
            {
                TryDelete(originalPath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Theory: GetRecordCompressedBytes byte-identity for each untouched record.
        // Parameterized over the 4 untouched indices (0, 1, 3, 4) so a per-index
        // regression surfaces as a distinct CI failure name.
        // ─────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(4)]
        public void Repack_UntouchedRecord_GetRecordCompressedBytesIdentical_AcrossOriginalAndRepacked(int index)
        {
            byte[] originalBytes = TreFileFixtures.BuildValidV0005FiveRecord();
            string originalPath = WriteTemp(originalBytes);
            string repackedPath = null;
            try
            {
                TreFile original = TreFile.Open(originalPath);
                Assert.Equal(5, original.Records.Count);

                // Edit record 2 (NOT the index under test, by construction — the
                // Theory only covers untouched indices).
                Assert.NotEqual(2, index);

                byte[] newPayload = Encoding.ASCII.GetBytes(
                    "EDIT for the byte-diff theory — index under test is " + index + ".");
                var edits = new Dictionary<int, byte[]> { { 2, newPayload } };

                byte[] repackedBytes = TreWriter.Repack(original, edits);
                repackedPath = WriteTemp(repackedBytes);
                TreFile repacked = TreFile.Open(repackedPath);

                Assert.Equal(5, repacked.Records.Count);

                byte[] originalSlice = original.GetRecordCompressedBytes(index);
                byte[] repackedSlice = repacked.GetRecordCompressedBytes(index);
                Assert.True(BytesEqual(originalSlice, repackedSlice),
                    "Index=" + index + ": untouched record's compressed slice differs "
                  + "(originalLen=" + originalSlice.Length + ", repackedLen=" + repackedSlice.Length + ")");
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
                "tre-repack-bytediff-" + Guid.NewGuid().ToString("N") + ".tre");
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
