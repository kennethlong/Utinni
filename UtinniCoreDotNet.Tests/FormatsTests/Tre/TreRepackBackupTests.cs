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
using Xunit;
using UtinniCoreDotNet.Saving;

namespace UtinniCoreDotNet.Tests.FormatsTests.Tre
{
    /// <summary>
    /// Phase 8 Plan 07 — timestamped backup uniqueness contract (08-REVIEWS MEDIUM-10
    /// closure).
    ///
    /// <para><b>What this class proves:</b> the framework-side
    /// <see cref="TreBackupPath.NextAvailable(string, DateTime)"/> helper:
    /// <list type="bullet">
    ///   <item>Two calls with DISTINCT timestamps produce DISTINCT backup paths
    ///         (happy-path two-distinct-stamps case).</item>
    ///   <item>Two calls within the SAME second (collision case — back-to-back
    ///         retry under sub-second clock resolution) produce DISTINCT paths
    ///         via the <c>-N</c> sequence-suffix disambiguator. NEVER overwrites
    ///         a prior backup.</item>
    ///   <item>An existing backup at the proposed timestamp is detected and the
    ///         helper steps to the next available <c>-N</c> name (existing-backup-not-overwritten
    ///         direct verification).</item>
    /// </list></para>
    ///
    /// <para><b>Why framework-side:</b> the timestamped-backup-naming contract is
    /// the 08-REVIEWS MEDIUM-10 disposition's core promise (a failed second attempt
    /// cannot destroy the prior known-good backup). Extracting the naming logic to
    /// <c>UtinniCoreDotNet.Saving.TreBackupPath</c> makes this contract CI-coverable
    /// without a UtinniPlugins checkout. The plugin's <c>TreRepackSaveTarget</c> now
    /// calls the helper directly via <c>DateTime.UtcNow</c>; tests pin
    /// <see cref="DateTime"/> explicitly so the assertions are deterministic.</para>
    /// </summary>
    public class TreRepackBackupTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Fact 1: Two distinct timestamps produce two distinct, non-existing
        // backup paths (happy-path two-distinct-stamps case).
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void NextAvailable_TwoDistinctTimestamps_ProduceTwoDistinctBackupPaths()
        {
            string trePath = NewTempTrePath();
            try
            {
                // Two clearly distinct second-resolution timestamps.
                var t1 = new DateTime(2026, 5, 28, 12, 34, 56, DateTimeKind.Utc);
                var t2 = new DateTime(2026, 5, 28, 12, 34, 57, DateTimeKind.Utc);

                string path1 = TreBackupPath.NextAvailable(trePath, t1);
                string path2 = TreBackupPath.NextAvailable(trePath, t2);

                Assert.NotEqual(path1, path2);
                Assert.EndsWith(".20260528-123456.bak", path1);
                Assert.EndsWith(".20260528-123457.bak", path2);
                Assert.StartsWith(trePath + ".", path1);
                Assert.StartsWith(trePath + ".", path2);

                // Neither should be a name that currently exists on disk.
                Assert.False(File.Exists(path1));
                Assert.False(File.Exists(path2));
            }
            finally
            {
                CleanupAllBackupsFor(trePath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact 2: Two calls within the SAME second (sub-second back-to-back
        // retry — what 08-REVIEWS MEDIUM-10 explicitly warns against) produce
        // DISTINCT paths via the -N disambiguator. The helper must NOT overwrite
        // the prior backup on the same-second collision.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void NextAvailable_SameSecondCollision_DisambiguatesWithSequenceSuffix()
        {
            string trePath = NewTempTrePath();
            try
            {
                var sameInstant = new DateTime(2026, 5, 28, 12, 34, 56, DateTimeKind.Utc);

                // First call: no existing backup; we get the base path.
                string path1 = TreBackupPath.NextAvailable(trePath, sameInstant);
                Assert.EndsWith(".20260528-123456.bak", path1);

                // Simulate the first backup landing on disk.
                File.WriteAllText(path1, "first backup payload (simulated)");

                // Second call at the SAME instant: must NOT return path1 (would
                // overwrite). The helper detects path1 exists and steps to the
                // disambiguated -1 name.
                string path2 = TreBackupPath.NextAvailable(trePath, sameInstant);
                Assert.NotEqual(path1, path2);
                Assert.EndsWith(".20260528-123456-1.bak", path2);
                Assert.False(File.Exists(path2));

                // Third call at the SAME instant: path1 + path2 both exist; helper
                // steps to -2. Prove the bound is bounded-iteration, not single-shot.
                File.WriteAllText(path2, "second backup payload (simulated)");
                string path3 = TreBackupPath.NextAvailable(trePath, sameInstant);
                Assert.NotEqual(path1, path3);
                Assert.NotEqual(path2, path3);
                Assert.EndsWith(".20260528-123456-2.bak", path3);
                Assert.False(File.Exists(path3));
            }
            finally
            {
                CleanupAllBackupsFor(trePath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fact 3: An existing backup at the proposed timestamp is detected and
        // NOT overwritten. Direct verification of the 08-REVIEWS MEDIUM-10
        // "never overwrite a prior backup" contract.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void NextAvailable_ExistingBackupAtTimestamp_StepsToNextNameWithoutOverwriting()
        {
            string trePath = NewTempTrePath();
            try
            {
                var instant = new DateTime(2026, 5, 28, 12, 34, 56, DateTimeKind.Utc);
                string occupiedPath = trePath + ".20260528-123456.bak";
                const string occupiedContent = "PRIOR KNOWN-GOOD BACKUP — must not be overwritten";

                File.WriteAllText(occupiedPath, occupiedContent);
                string priorHash = HashFile(occupiedPath);
                long priorLength = new FileInfo(occupiedPath).Length;

                string nextPath = TreBackupPath.NextAvailable(trePath, instant);

                // The helper must NOT return the occupied path.
                Assert.NotEqual(occupiedPath, nextPath);
                // The returned path must not currently exist (the caller is about
                // to File.Copy into it).
                Assert.False(File.Exists(nextPath));
                // The disambiguated name must follow the -N convention.
                Assert.EndsWith(".20260528-123456-1.bak", nextPath);

                // The prior backup is byte-unchanged (length + SHA-256) — the helper
                // is pure-naming, no filesystem mutation of the prior file.
                Assert.Equal(priorLength, new FileInfo(occupiedPath).Length);
                Assert.Equal(priorHash, HashFile(occupiedPath));
                Assert.Equal(occupiedContent, File.ReadAllText(occupiedPath));
            }
            finally
            {
                CleanupAllBackupsFor(trePath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string NewTempTrePath()
        {
            return Path.Combine(Path.GetTempPath(),
                "tre-repack-backup-" + Guid.NewGuid().ToString("N") + ".tre");
        }

        /// <summary>
        /// Cleans up any files starting with <paramref name="trePath"/>. covers the
        /// trePath itself plus every backup the test created (timestamped + -N
        /// disambiguated). Best-effort; ignores transient I/O errors.
        /// </summary>
        private static void CleanupAllBackupsFor(string trePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(trePath);
                string prefix = Path.GetFileName(trePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                foreach (string f in Directory.GetFiles(dir, prefix + "*"))
                {
                    try { File.Delete(f); } catch { /* best-effort */ }
                }
            }
            catch
            {
                /* best-effort */
            }
        }

        private static string HashFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(fs);
                var sb = new System.Text.StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
