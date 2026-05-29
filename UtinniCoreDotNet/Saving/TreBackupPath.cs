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
using System.IO;

namespace UtinniCoreDotNet.Saving
{
    /// <summary>
    /// Timestamped backup-path resolver for the Phase 8 Plan 07 <c>.tre</c> repack save
    /// target (08-REVIEWS MEDIUM-10 — never overwrite a prior backup).
    ///
    /// <para><b>Framework-side placement (checker B-1, mirrors LooseOverridePath +
    /// LivePatchValidator):</b> this helper is pure-managed and BCL-only
    /// (System + System.IO). It ships in <c>UtinniCoreDotNet</c> rather than the plugin so
    /// the CI's Utinni-only checkout can unit-test it without a UtinniPlugins checkout —
    /// eliminating the cross-repo linked-source path that would otherwise be required.
    /// The plugin's <c>TreRepackSaveTarget</c> consumes this via the existing
    /// <c>UtinniCoreDotNet.dll</c> reference.</para>
    ///
    /// <para><b>Contract:</b> <see cref="NextAvailable(string, DateTime)"/> returns a
    /// timestamped backup path that does NOT currently exist on disk:
    /// <c>&lt;treFilePath&gt;.&lt;yyyyMMdd-HHmmss&gt;.bak</c>. If a backup at that exact
    /// timestamp already exists (sub-second back-to-back retry), the helper disambiguates
    /// by appending a <c>-N</c> sequence suffix (<c>.bak.1.bak</c>-style naming is avoided
    /// in favour of inserting the disambiguator BEFORE the <c>.bak</c> extension so the
    /// file extension stays recognizable). NEVER overwrites a prior backup
    /// (08-REVIEWS MEDIUM-10 disposition).</para>
    ///
    /// <para><b>Timestamp source:</b> the caller passes a <see cref="DateTime"/>
    /// explicitly so the helper is deterministic and testable. Production code passes
    /// <see cref="DateTime.UtcNow"/>; tests pin a known instant and assert the produced
    /// path string.</para>
    ///
    /// <para><b>Disambiguation strategy:</b> the same-second collision bound is 1000
    /// attempts (1, 2, …, 999) — pathologically high; real sub-second retries
    /// resolve at -1 or -2. If the bound is exceeded (essentially impossible),
    /// a GUID-suffixed name is returned as the final fallback so the helper never
    /// throws and never returns an already-existing path.</para>
    /// </summary>
    public static class TreBackupPath
    {
        /// <summary>The maximum sequence suffix attempted before the GUID fallback.</summary>
        public const int MaxDisambiguationAttempts = 1000;

        /// <summary>
        /// Returns the next non-existing timestamped backup path for
        /// <paramref name="treFilePath"/>. The returned path is guaranteed to NOT exist
        /// on disk at the moment of call (the caller is responsible for any TOCTOU
        /// window — for the repack save-target's use case the window is acceptable
        /// because <see cref="File.Copy(string, string, bool)"/> with <c>overwrite:false</c>
        /// throws on collision anyway).
        /// </summary>
        /// <param name="treFilePath">Absolute path to the live <c>.tre</c> archive being
        /// backed up. Used as the prefix for the backup name.</param>
        /// <param name="now">Timestamp source — typically <see cref="DateTime.UtcNow"/>
        /// in production. Deterministic in tests.</param>
        /// <returns>An absolute path of the form
        /// <c>&lt;treFilePath&gt;.&lt;yyyyMMdd-HHmmss&gt;.bak</c> on the first attempt,
        /// or <c>&lt;treFilePath&gt;.&lt;yyyyMMdd-HHmmss&gt;-N.bak</c> on collision.</returns>
        public static string NextAvailable(string treFilePath, DateTime now)
        {
            if (treFilePath == null) throw new ArgumentNullException("treFilePath");
            if (treFilePath.Length == 0)
            {
                throw new ArgumentException("treFilePath must not be empty.", "treFilePath");
            }

            string stamp = now.ToString("yyyyMMdd-HHmmss");
            string candidate = treFilePath + "." + stamp + ".bak";
            if (!File.Exists(candidate)) return candidate;

            // Disambiguate. Bounded loop — File.Exists at -1, -2, -3, ... should resolve
            // in a few iterations even under pathological sub-second retries.
            for (int n = 1; n < MaxDisambiguationAttempts; n++)
            {
                string disambiguated = treFilePath + "." + stamp + "-" + n + ".bak";
                if (!File.Exists(disambiguated)) return disambiguated;
            }

            // Pathological fallback: append a GUID so we never overwrite. File.Copy with
            // overwrite:false will still throw if it somehow exists, but the GUID makes
            // collision astronomically unlikely.
            return treFilePath + "." + stamp + "-" + Guid.NewGuid().ToString("N") + ".bak";
        }
    }
}
