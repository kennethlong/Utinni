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
    /// Open-side logical-path derivation (15-20, 15-SMOKE Checklist D-ii). The complement of
    /// <see cref="LooseOverridePath"/>: where <c>LooseOverridePath.Resolve</c> guards the SAVE side
    /// (root-containment of a relative asset path), this derives the relative/logical asset path on the
    /// OPEN side from an absolute on-disk path.
    ///
    /// <para><b>Why:</b> a raw <c>Open…</c> file dialog yields only an absolute filesystem path. The prior
    /// loose-override save derived its <c>relAssetPath</c> as <c>Path.GetFileName(path)</c> — the filename
    /// ONLY — which flattened the override (<c>loose\ui_auc.stf</c>) so the SWG client, which resolves by
    /// the logical subpath (<c>string\en\ui_auc.stf</c>), silently never picked it up. This helper recovers
    /// the logical subpath by making the opened absolute path relative to the resolved client root.</para>
    ///
    /// <para><b>Loose round-trip parity:</b> a file opened from <c>&lt;root&gt;\loose\appearance\x.prt</c>
    /// derives <c>appearance/x.prt</c> (the leading <c>loose/</c> segment is stripped) so it re-saves to
    /// the SAME loose location — matching the shipped <c>.prt</c> behavior the maintainer verified. A file
    /// opened from <c>&lt;root&gt;\string\en\ui_auc.stf</c> derives <c>string/en/ui_auc.stf</c>.</para>
    ///
    /// <para><b>Placement (mirrors <see cref="LooseOverridePath"/>):</b> pure-managed, BCL-only
    /// (System + System.IO), shipped in the netstandard2.0 <c>UtinniCoreDotNet.PathContainment</c> lib so
    /// CI's Utinni-only checkout can unit-test it without a UtinniPlugins checkout. The namespace stays
    /// <c>UtinniCoreDotNet.Saving</c> so net472 callers are source-unchanged.</para>
    ///
    /// <para><b>Contract (Try-pattern, NEVER throws):</b>
    /// <see cref="TryFromAbsolute(string, string, out string)"/> returns <c>false</c> (with
    /// <c>logicalRelPath = null</c>) on every degraded input — null/empty args, malformed paths, a file
    /// OUTSIDE the client root, or an empty remainder. The caller keeps its existing
    /// <c>Path.GetFileName</c> fallback. Never an exception on the open path.</para>
    ///
    /// <para><b>Defenses (defense-in-depth with the downstream <see cref="LooseOverridePath"/>):</b>
    /// canonicalizes both inputs via <see cref="Path.GetFullPath(string)"/>; the OrdinalIgnoreCase
    /// StartsWith gate uses a trailing separator so a sibling root sharing a string prefix
    /// (<c>…\SWGEmu-X</c> vs root <c>…\SWGEmu</c>) cannot pass; the result is forward-slash normalized,
    /// has no leading separator, and (because it is the canonicalized remainder under the root) can never
    /// contain a <c>..</c> segment.</para>
    /// </summary>
    public static class LogicalAssetPath
    {
        /// <summary>
        /// Derives the client-root-relative logical asset path of an absolute on-disk path. Returns
        /// <c>true</c> with the forward-slash-normalized, loose-prefix-stripped logical path on success;
        /// <c>false</c> (with <paramref name="logicalRelPath"/> = <c>null</c>) on any degraded input.
        /// Never throws.
        /// </summary>
        /// <param name="absolutePath">The absolute on-disk path the asset was opened from (e.g. a raw
        /// <c>Open…</c> dialog result).</param>
        /// <param name="clientRoot">The resolved client root directory (the SAME root the loose-override
        /// save composes under). May be supplied with or without a trailing separator.</param>
        /// <param name="logicalRelPath">On success, the forward-slash logical subpath (no leading
        /// separator, leading <c>loose/</c> segment stripped); otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a logical path was derived; <c>false</c> if the input was degraded or
        /// the file lies outside <paramref name="clientRoot"/>.</returns>
        public static bool TryFromAbsolute(string absolutePath, string clientRoot, out string logicalRelPath)
        {
            logicalRelPath = null;

            if (string.IsNullOrEmpty(absolutePath)) return false;
            if (string.IsNullOrEmpty(clientRoot)) return false;

            string fullAbsolute;
            string fullRoot;
            try
            {
                fullAbsolute = Path.GetFullPath(absolutePath);
                fullRoot = Path.GetFullPath(clientRoot);
            }
            catch
            {
                // Malformed input (illegal chars, too long, etc.) — degrade, never throw.
                return false;
            }

            if (fullRoot.Length == 0) return false;

            // Normalize the root to end with a single OS separator so the StartsWith gate cannot be
            // defeated by a prefix-match attack (sibling 'C:\SWGEmu-X' vs root 'C:\SWGEmu').
            string rootWithSep = fullRoot;
            char last = rootWithSep[rootWithSep.Length - 1];
            if (last != Path.DirectorySeparatorChar && last != Path.AltDirectorySeparatorChar)
            {
                rootWithSep = rootWithSep + Path.DirectorySeparatorChar;
            }

            // Containment gate (case-insensitive on Windows). If the file is not under the root there is
            // no derivable logical path — the caller keeps its Path.GetFileName fallback.
            if (!fullAbsolute.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Take the remainder under the root.
            string remainder = fullAbsolute.Substring(rootWithSep.Length);

            // Normalize separators to '/', then trim any leading separators.
            remainder = remainder.Replace(Path.DirectorySeparatorChar, '/')
                                  .Replace(Path.AltDirectorySeparatorChar, '/');
            remainder = remainder.TrimStart('/');

            if (remainder.Length == 0) return false;

            // The path is exactly <root>\loose (the loose directory itself, no file remainder) — there
            // is no asset to derive.
            if (string.Equals(remainder, "loose", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Loose round-trip parity: strip a single leading 'loose/' segment (case-insensitive) so a
            // file opened from inside the loose tree re-saves to the SAME loose location.
            if (remainder.StartsWith("loose/", StringComparison.OrdinalIgnoreCase))
            {
                remainder = remainder.Substring("loose/".Length);
                remainder = remainder.TrimStart('/');
                if (remainder.Length == 0) return false; // <root>\loose\ with no file remainder.
            }

            logicalRelPath = remainder;
            return true;
        }
    }
}
