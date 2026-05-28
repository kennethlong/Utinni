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
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Tre;
using UtinniCoreDotNet.Tests.FormatsTests.Tre;
using Xunit;

namespace UtinniCoreDotNet.Tests.FormatsTests.Iff
{
    /// <summary>
    /// Degraded-hand-off coverage for <see cref="TreRecordIndexResolver"/>
    /// (Phase 8 Plan 05 Task 4b, checker W-3 / round-2 MEDIUM 12). Validates the
    /// two contractual branches:
    ///
    /// <list type="bullet">
    ///   <item><b>Match branch:</b> the offset matches a record's <c>Offset</c> →
    ///         <see cref="OpenSource.TreArchive"/> with the correct trePath, recordIndex,
    ///         and logicalPath.</item>
    ///   <item><b>Mismatch branch (W-3 degraded fallback):</b> the offset matches no
    ///         record → <see cref="OpenSource.Unknown.Instance"/>. <c>Source is
    ///         OpenSource.LooseFile</c> AND <c>Source is OpenSource.TreArchive</c> both
    ///         evaluate false on the result — so downstream Save-mode gates (Save → In
    ///         Place, Save → Repack) stay disabled on the degraded path.</item>
    /// </list>
    /// </summary>
    public class TreHandoffFallbackTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // [Fact 1] Mismatched offset → Unknown.Instance (degraded fallback)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ResolveOrUnknown_OffsetNotInArchive_ReturnsUnknownInstance()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                // Offset that no record in the fixture has — fixture record offsets are
                // header-plus-aligned values, never a multi-billion sentinel.
                const long bogusOffset = 0x7FFFFFFEL;

                OpenSource src = TreRecordIndexResolver.ResolveOrUnknown(
                    path, bogusOffset, "datatables/foo.iff");

                Assert.Same(OpenSource.Unknown.Instance, src);
                Assert.False(src is OpenSource.LooseFile,
                    "Degraded fallback must NOT be OpenSource.LooseFile (logical path is virtual, not on disk).");
                Assert.False(src is OpenSource.TreArchive,
                    "Degraded fallback must NOT be OpenSource.TreArchive (no record matched).");
                Assert.True(src is OpenSource.Unknown,
                    "Degraded fallback MUST be OpenSource.Unknown (checker W-3).");
            }
            finally { TryDelete(path); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // [Fact 2] Matching offset → TreArchive with the right index + fields
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ResolveOrUnknown_OffsetMatchesARecord_ReturnsTreArchiveWithRightIndex()
        {
            byte[] bytes = TreFileFixtures.BuildValidV0005ThreeRecord();
            string path = WriteTemp(bytes);
            try
            {
                // Read the actual offset of record 1 from the fixture — we don't hard-code it
                // because TreFileFixtures may change layout details over time.
                TreFile tre = TreFile.Open(path);
                Assert.True(tre.Records.Count >= 2, "Fixture must have at least 2 records.");
                int expectedIndex = 1;
                long matchOffset = tre.Records[expectedIndex].Offset;

                OpenSource src = TreRecordIndexResolver.ResolveOrUnknown(
                    path, matchOffset, "quick.txt");

                Assert.True(src is OpenSource.TreArchive, "Match branch must return TreArchive.");
                var ta = (OpenSource.TreArchive)src;
                Assert.Equal(path, ta.TrePath);
                Assert.Equal(expectedIndex, ta.RecordIndex);
                Assert.Equal("quick.txt", ta.LogicalPath);
            }
            finally { TryDelete(path); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // [Fact 3] Missing / unopenable path → Unknown.Instance (graceful degrade)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ResolveOrUnknown_OpenFailure_ReturnsUnknownInstance()
        {
            // A path that does NOT exist must NOT throw — the hand-off is best-effort and
            // surfaces the degraded fallback if the archive can't be opened.
            string nonExistent = Path.Combine(Path.GetTempPath(),
                "tre-handoff-missing-" + Guid.NewGuid().ToString("N") + ".tre");
            OpenSource src = TreRecordIndexResolver.ResolveOrUnknown(
                nonExistent, 0L, "datatables/foo.iff");
            Assert.Same(OpenSource.Unknown.Instance, src);
        }

        // ─────────────────────────────────────────────────────────────────────
        // [Fact 4] Null / empty trePath or null logicalPath → Unknown.Instance
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ResolveOrUnknown_NullOrEmptyInputs_ReturnsUnknownInstance()
        {
            Assert.Same(OpenSource.Unknown.Instance,
                TreRecordIndexResolver.ResolveOrUnknown(null, 0L, "foo"));
            Assert.Same(OpenSource.Unknown.Instance,
                TreRecordIndexResolver.ResolveOrUnknown("", 0L, "foo"));
            Assert.Same(OpenSource.Unknown.Instance,
                TreRecordIndexResolver.ResolveOrUnknown(@"C:\nonexistent.tre", 0L, null));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers (mirror TreFileTests pattern)
        // ─────────────────────────────────────────────────────────────────────

        private static string WriteTemp(byte[] bytes)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "tre-handoff-" + Guid.NewGuid().ToString("N") + ".tre");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { /* best-effort temp cleanup */ }
        }
    }
}
