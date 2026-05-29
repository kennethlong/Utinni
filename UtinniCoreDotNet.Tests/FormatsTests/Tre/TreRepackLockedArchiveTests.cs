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
using System.Security.Cryptography;
using Xunit;
using UtinniCoreDotNet.Saving;

namespace UtinniCoreDotNet.Tests.FormatsTests.Tre
{
    /// <summary>
    /// Phase 8 Plan 07 — locked-archive refusal automation (08-REVIEWS MEDIUM-10).
    ///
    /// <para><b>What this class proves:</b> the framework-side
    /// <see cref="TreRepackLock.Probe"/> correctly returns
    /// <c>SharingViolation</c> when the target <c>.tre</c> is held open with
    /// <see cref="FileShare.None"/> by another stream (simulating the live SWG
    /// client holding the archive). Additionally, the original file is byte-unchanged
    /// after the probe — proving the probe is non-mutating.</para>
    ///
    /// <para><b>Why framework-side:</b> the plugin's <c>TreRepackSaveTarget</c> would
    /// also reject the operation, but its actual save-target code lives in the
    /// UtinniPlugins repo which is NOT in the Utinni CI checkout. By proving the
    /// helper at the framework boundary, we cover the locked-archive contract in
    /// CI without needing a UtinniPlugins build step. The plugin's save target is
    /// a thin caller that translates the probe outcome to
    /// <c>RefusedClientHoldsArchive_LooseOverrideRecommended</c> via the
    /// <see cref="TreRepackLock.IsSharingViolation(IOException)"/> shared
    /// HResult detector.</para>
    ///
    /// <para><b>What this does NOT cover:</b> the actual SWG client's lock-acquire
    /// semantics under live injection — but the test simulates the canonical
    /// Windows ERROR_SHARING_VIOLATION (HResult 0x80070020) the OS surfaces in
    /// either case.</para>
    /// </summary>
    public class TreRepackLockedArchiveTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Fact 1: Probe against a locked-with-FileShare.None archive returns
        //         SharingViolation (the canonical refusal classification).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Probe_LockedArchive_ReturnsSharingViolation()
        {
            byte[] originalBytes = TreFileFixtures.BuildValidV0005FiveRecord();
            string archivePath = WriteTemp(originalBytes);
            try
            {
                // Hold the archive with FileShare.None — simulates the live SWG client
                // holding it open exclusively. The probe should detect this and refuse.
                using (var holder = new FileStream(
                    archivePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    TreRepackLock.ProbeResult result = TreRepackLock.Probe(archivePath);
                    Assert.Equal(TreRepackLock.ProbeResult.SharingViolation, result);
                }
            }
            finally
            {
                TryDelete(archivePath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact 2: After the failed probe attempt, the original file is byte-unchanged.
        //         Proves the probe is non-mutating (no partial-write, no truncate).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Probe_LockedArchive_OriginalBytesUnchanged()
        {
            byte[] originalBytes = TreFileFixtures.BuildValidV0005FiveRecord();
            string archivePath = WriteTemp(originalBytes);
            try
            {
                // Snapshot the pre-attempt hash + length.
                string preHash;
                long preLength;
                using (var fs = new FileStream(
                    archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    preLength = fs.Length;
                    using (var sha = SHA256.Create())
                    {
                        preHash = ToHex(sha.ComputeHash(fs));
                    }
                }

                // Lock the file and probe; the probe must report SharingViolation.
                using (var holder = new FileStream(
                    archivePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    TreRepackLock.ProbeResult result = TreRepackLock.Probe(archivePath);
                    Assert.Equal(TreRepackLock.ProbeResult.SharingViolation, result);
                }

                // Re-hash + re-length after the (failed) probe. Must be byte-identical.
                string postHash;
                long postLength;
                using (var fs = new FileStream(
                    archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    postLength = fs.Length;
                    using (var sha = SHA256.Create())
                    {
                        postHash = ToHex(sha.ComputeHash(fs));
                    }
                }

                Assert.Equal(preLength, postLength);
                Assert.Equal(preHash, postHash);
            }
            finally
            {
                TryDelete(archivePath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact 3 (defensive): With the file unlocked, the probe succeeds.
        //         Guards against an over-eager probe that returns SharingViolation
        //         even on an unlocked target.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void Probe_UnlockedArchive_ReturnsAvailable()
        {
            byte[] originalBytes = TreFileFixtures.BuildValidV0005FiveRecord();
            string archivePath = WriteTemp(originalBytes);
            try
            {
                TreRepackLock.ProbeResult result = TreRepackLock.Probe(archivePath);
                Assert.Equal(TreRepackLock.ProbeResult.Available, result);
            }
            finally
            {
                TryDelete(archivePath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact 4: IsSharingViolation(IOException) correctly classifies the
        //         canonical Windows ERROR_SHARING_VIOLATION HResult. Mirrors the
        //         classification used by the plugin's catch-when filters.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void IsSharingViolation_RecognizesCanonicalHResults()
        {
            // Use the IOException(message, hresult) ctor — the HResult property setter
            // is not public on net472. The ctor accepts the hresult directly and is the
            // canonical way to construct an IOException with a specific HResult value.
            var sharingViolation = new IOException("simulated sharing violation",
                unchecked((int)0x80070020));
            var lockViolation = new IOException("simulated lock violation",
                unchecked((int)0x80070021));
            var unrelatedIoError = new IOException("disk full",
                unchecked((int)0x80070070));

            Assert.True(TreRepackLock.IsSharingViolation(sharingViolation));
            Assert.True(TreRepackLock.IsSharingViolation(lockViolation));
            Assert.False(TreRepackLock.IsSharingViolation(unrelatedIoError));
            Assert.False(TreRepackLock.IsSharingViolation(null));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string WriteTemp(byte[] bytes)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "tre-repack-locked-" + Guid.NewGuid().ToString("N") + ".tre");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort */ }
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
