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
    /// Locked-archive probe for the Phase 8 Plan 07 <c>.tre</c> repack save target
    /// (08-REVIEWS MEDIUM-10 — locked-archive fallback). Probes whether the target
    /// <c>.tre</c> file can be opened with exclusive (no-share) write access, which is
    /// a proxy for "is the live SWG client holding this archive open?". When the probe
    /// fails with a sharing violation, the repack save target refuses to proceed and
    /// returns the candid loose-override recommendation per
    /// <c>TreRepackResult.RefusedClientHoldsArchive_LooseOverrideRecommended</c>.
    ///
    /// <para><b>Framework-side placement (checker B-1, mirrors LooseOverridePath +
    /// LivePatchValidator + TreBackupPath):</b> this helper is pure-managed and BCL-only
    /// (System + System.IO). It ships in <c>UtinniCoreDotNet</c> rather than the plugin so
    /// the CI's Utinni-only checkout can unit-test it (against a locally-locked temp
    /// file) without a UtinniPlugins checkout.</para>
    ///
    /// <para><b>Contract:</b> <see cref="ProbeResult"/> classifies the outcome:
    /// <list type="bullet">
    ///   <item><c>Available</c> — the archive opened with exclusive access; the repack
    ///         can proceed. The probe stream is closed before returning; the caller's
    ///         subsequent repack-write is responsible for re-opening atomically.</item>
    ///   <item><c>SharingViolation</c> — the archive is held open by another process
    ///         (almost always the live SWG client). The caller should return
    ///         <c>RefusedClientHoldsArchive_LooseOverrideRecommended</c>.</item>
    ///   <item><c>OtherIoError</c> — anything else (file missing, permissions,
    ///         volume offline). The caller maps this to <c>Failed</c>.</item>
    /// </list></para>
    ///
    /// <para><b>Why a probe, not a held lock?</b> Holding the lock for the entire repack
    /// window is brittle: <see cref="File.Replace(string, string, string, bool)"/> requires
    /// the destination NOT be held open by the caller. The probe-then-release pattern
    /// fits the "temp-write + atomic-replace" flow used by <c>TreRepackSaveTarget</c>:
    /// probe at the top → temp-write off-thread → atomic-replace (which will itself
    /// surface a sharing violation if the client grabs the archive between probe and
    /// replace; that race is caught by the save target's <c>IsSharingViolation</c>
    /// catch).</para>
    /// </summary>
    public static class TreRepackLock
    {
        /// <summary>Classification of <see cref="Probe"/>'s outcome.</summary>
        public enum ProbeResult
        {
            /// <summary>The archive opened with exclusive access — repack can proceed.</summary>
            Available,

            /// <summary>Another process holds the archive open (almost always the live
            /// client). The caller should refuse with the loose-override recommendation
            /// per 08-REVIEWS MEDIUM-10.</summary>
            SharingViolation,

            /// <summary>Open failed for a non-sharing-violation reason (missing file,
            /// permissions, volume offline, etc.).</summary>
            OtherIoError,
        }

        /// <summary>
        /// Probes whether <paramref name="treFilePath"/> can be opened for exclusive
        /// write access. Opens with <see cref="FileShare.None"/> then immediately closes
        /// the stream — the probe is non-mutating.
        /// </summary>
        /// <param name="treFilePath">Absolute path to the live <c>.tre</c> archive.</param>
        /// <returns>The classification of the probe outcome.</returns>
        public static ProbeResult Probe(string treFilePath)
        {
            if (treFilePath == null) throw new ArgumentNullException("treFilePath");
            if (treFilePath.Length == 0)
            {
                throw new ArgumentException("treFilePath must not be empty.", "treFilePath");
            }

            try
            {
                // FileMode.Open: do NOT create. FileAccess.Read is sufficient — the live
                // SWG client opens .tre archives with FileShare.Read (read-mapped); a
                // read-only FileShare.None probe will fail with sharing violation if and
                // only if the file is held open in any non-shared mode by another process.
                // Using Read access also means the probe never alters the file metadata
                // (Last Access timestamp is allowed to change but no content changes).
                using (var fs = new FileStream(
                    treFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // Touch a byte to force the OS to actually acquire the share-none
                    // lock semantics (some FS drivers defer the share check until first
                    // I/O). 0-length files return safely from this single-byte read.
                    if (fs.Length > 0)
                    {
                        fs.ReadByte();
                    }
                    return ProbeResult.Available;
                }
            }
            catch (IOException ex) when (IsSharingViolation(ex))
            {
                return ProbeResult.SharingViolation;
            }
            catch (IOException)
            {
                return ProbeResult.OtherIoError;
            }
            catch (UnauthorizedAccessException)
            {
                return ProbeResult.OtherIoError;
            }
        }

        /// <summary>
        /// True iff the <see cref="IOException"/> reflects ERROR_SHARING_VIOLATION
        /// (HResult 0x80070020) or ERROR_LOCK_VIOLATION (0x80070021) — the canonical
        /// "file in use" failures on Windows. Matches <see cref="IOException.HResult"/>
        /// rather than the localized message text to avoid language-dependent
        /// false positives/negatives.
        /// </summary>
        public static bool IsSharingViolation(IOException ex)
        {
            if (ex == null) return false;
            const int errorSharingViolation = unchecked((int)0x80070020);
            const int errorLockViolation = unchecked((int)0x80070021);
            return ex.HResult == errorSharingViolation || ex.HResult == errorLockViolation;
        }
    }
}
