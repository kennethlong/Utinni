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
    /// Root-containment helper for loose-override file save paths (08-REVIEWS MEDIUM-8,
    /// Phase 8 Plan 05).
    ///
    /// <para><b>Cross-TFM single-source-of-truth (14-01, review Consensus #5):</b> this helper now
    /// lives in the <c>netstandard2.0</c> <c>UtinniCoreDotNet.PathContainment</c> project so it can
    /// be consumed by BOTH the net472 editor host (via <c>[TypeForwardedTo]</c> in
    /// <c>UtinniCoreDotNet</c>, binary type identity preserved) AND the net10 <c>Utinni.Mcp</c>
    /// server (the access-control boundary's <c>ResolvedRoot.Resolve</c> delegates here). The
    /// namespace stays <c>UtinniCoreDotNet.Saving</c> so net472 callers are source-unchanged.</para>
    ///
    /// <para><b>Framework-side placement (checker B-1):</b> this helper is pure-managed and BCL-only
    /// (System + System.IO). It ships in the netstandard2.0 lib rather than the plugin so the
    /// CI's Utinni-only checkout can unit-test it without a UtinniPlugins checkout — eliminating
    /// the cross-repo linked-source path that the previous draft would have required. Phases 9-11
    /// consume the same helper via the existing <c>UtinniCoreDotNet.dll</c> reference (now a
    /// type-forward to this lib).</para>
    ///
    /// <para><b>Contract:</b> <see cref="Resolve(string, string)"/> takes an absolute
    /// <paramref name="resolvedRoot"/> (already canonical-normalized by the caller) and a
    /// candidate relative asset path. It returns the resolved absolute path under the root if
    /// the input passes every defense; otherwise it throws <see cref="ArgumentException"/> /
    /// <see cref="ArgumentNullException"/>.</para>
    ///
    /// <para><b>Defenses (defense-in-depth):</b>
    /// <list type="bullet">
    ///   <item>Reject null/empty <paramref name="resolvedRoot"/> or <paramref name="relAssetPath"/>.</item>
    ///   <item>Reject rooted <paramref name="relAssetPath"/> (<see cref="Path.IsPathRooted(string)"/>
    ///         catches <c>C:\evil.iff</c>, <c>D:foo</c>, <c>\unc</c>, <c>/abs</c>).</item>
    ///   <item>Reject any <c>..</c> path segment after splitting on both <c>/</c> and <c>\</c>
    ///         (explicit segment-level check; does NOT rely solely on
    ///         <see cref="Path.GetFullPath(string)"/>'s canonicalization).</item>
    ///   <item>Final <c>StartsWith</c> check against <paramref name="resolvedRoot"/> + trailing
    ///         <see cref="Path.DirectorySeparatorChar"/> — prevents prefix-match attacks
    ///         (<c>C:\swg-clientx\...</c> against root <c>C:\swg-client</c>). Uses
    ///         <see cref="StringComparison.OrdinalIgnoreCase"/> on Windows.</item>
    /// </list></para>
    /// </summary>
    public static class LooseOverridePath
    {
        /// <summary>
        /// Resolves a loose-override path under <paramref name="resolvedRoot"/>. Throws
        /// <see cref="ArgumentException"/> on every documented rejection class (null/empty,
        /// rooted relAssetPath, <c>..</c> traversal, escape-via-canonicalization, prefix-match
        /// attack).
        /// </summary>
        /// <param name="resolvedRoot">Absolute, canonical client root directory.</param>
        /// <param name="relAssetPath">Relative virtual/logical path of the asset (no leading
        /// separator, no <c>..</c> segments).</param>
        /// <returns>The absolute file path under <paramref name="resolvedRoot"/>.</returns>
        public static string Resolve(string resolvedRoot, string relAssetPath)
        {
            if (resolvedRoot == null) throw new ArgumentNullException("resolvedRoot");
            if (resolvedRoot.Length == 0) throw new ArgumentException("resolvedRoot must not be empty.", "resolvedRoot");
            if (relAssetPath == null) throw new ArgumentNullException("relAssetPath");
            if (relAssetPath.Length == 0) throw new ArgumentException("relAssetPath must not be empty.", "relAssetPath");

            // 08-REVIEW WR-07: Re-canonicalize resolvedRoot defensively. The docstring says the
            // caller is responsible, but every observed caller in TJT (FormIffEditor.ResolveClientRoot
            // around lines 1477-1511) feeds in raw paths from Process.MainModule.FileName /
            // GetWorkingDirectory / Utinni.ini — none of which guarantee canonical form. A root like
            //   "C:\swg-client\..\swg-client"
            // would Path.GetFullPath(combined) into "C:\swg-client\foo.iff" but the StartsWith gate
            // (which compared against the un-canonicalized rootWithSep "C:\swg-client\..\swg-client\")
            // would return false — falsely rejecting a legitimate path. Canonicalize the root once
            // at entry; downstream StartsWith / trailing-separator logic then operates on a stable
            // value. Same exception class as the candidate-side canonicalization failure path.
            try
            {
                resolvedRoot = Path.GetFullPath(resolvedRoot);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "resolvedRoot produced an invalid path: " + ex.Message,
                    "resolvedRoot", ex);
            }

            // (b) Rooted candidates (C:\evil.iff, D:foo, \unc, /abs) are immediate rejects —
            // they would Path.Combine away from resolvedRoot.
            if (Path.IsPathRooted(relAssetPath))
            {
                throw new ArgumentException(
                    "relAssetPath must not be rooted (was '" + relAssetPath + "').",
                    "relAssetPath");
            }

            // (c) Explicit '..' segment check (defense in depth — do NOT rely solely on
            // GetFullPath canonicalization, which would otherwise let a malicious '..' escape
            // resolvedRoot before the StartsWith gate runs).
            string[] segments = relAssetPath.Split('/', '\\');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == "..")
                {
                    throw new ArgumentException(
                        "relAssetPath must not contain '..' segments (was '" + relAssetPath + "').",
                        "relAssetPath");
                }
            }

            // Normalize the root: append the OS separator if the caller didn't, so the
            // StartsWith gate cannot be defeated by a prefix-match attack (e.g.
            // 'C:\swg-clientx\loot' StartsWith 'C:\swg-client' would otherwise return true).
            // (WR-07: operates on the now-canonicalized resolvedRoot.)
            string rootWithSep = resolvedRoot;
            if (rootWithSep[rootWithSep.Length - 1] != Path.DirectorySeparatorChar
                && rootWithSep[rootWithSep.Length - 1] != Path.AltDirectorySeparatorChar)
            {
                rootWithSep = rootWithSep + Path.DirectorySeparatorChar;
            }

            // Normalize input separators to the OS-native form before combining.
            string normalizedRel = relAssetPath.Replace(
                Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            // Combine + canonicalize. Path.GetFullPath collapses any residual '.'/duplicate
            // separators — the explicit segment scan above already rejected '..' so any
            // canonicalization here is benign.
            string combined = Path.Combine(rootWithSep, normalizedRel);
            string full;
            try
            {
                full = Path.GetFullPath(combined);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "relAssetPath produced an invalid path: " + ex.Message,
                    "relAssetPath", ex);
            }

            // (d) StartsWith gate — on Windows we use OrdinalIgnoreCase because the filesystem
            // is case-insensitive. The trailing-separator guard on rootWithSep prevents a sibling
            // directory like 'C:\swg-clientx' from passing the prefix check.
            if (!full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "relAssetPath escapes resolvedRoot after normalization (got '" + full
                    + "', expected to start with '" + rootWithSep + "').",
                    "relAssetPath");
            }

            return full;
        }
    }
}
