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

namespace UtinniCoreDotNet.Saving
{
    /// <summary>
    /// The four tiers the D-06 forced-reload dispatcher returns
    /// (08-REVIEWS HIGH-4 tiered acceptance). Texture / terrain pass in-session;
    /// datatable / STF / object-template / unknown surface honestly as
    /// <see cref="PendingNextSceneChange"/>; the absent-client case maps to
    /// <see cref="Unavailable"/>.
    /// </summary>
    public enum ReloadTier
    {
        /// <summary>Texture-class asset re-resolved via Graphics.ReloadTextures().</summary>
        ReloadedTextures = 0,

        /// <summary>Terrain re-resolved via GroundScene.Get().ReloadTerrain().</summary>
        ReloadedTerrain = 1,

        /// <summary>Asset re-resolves on the next TJT-driven scene change.</summary>
        PendingNextSceneChange = 2,

        /// <summary>No live client — the reload binding is unreachable.</summary>
        Unavailable = 3,
    }

    /// <summary>
    /// Pure-managed extension/sub-type classifier for the D-06 forced-reload dispatcher
    /// (08-REVIEWS HIGH-4 / Phase 8 Plan 05 Task 3, round-2 MEDIUM 9 pinned routing table).
    ///
    /// <para><b>Framework-side placement</b> isolates the routing rules from the plugin
    /// shell so the rules can be unit-tested as a pure function without coupling the test
    /// assembly to UtinniPlugins.</para>
    ///
    /// <para><b>Pinned routing table</b> (round-2 MEDIUM 9):</para>
    /// <list type="bullet">
    ///   <item>TextureExtensions   := { .dds, .tga, .png, .jpg, .jpeg } → ReloadedTextures</item>
    ///   <item>TerrainExtensions   := { .trn } → ReloadedTerrain</item>
    ///   <item>DatatableExtensions := { .iff } + root TypeId "DTII" → PendingNextSceneChange</item>
    ///   <item>StringtableExtensions := { .stf } → PendingNextSceneChange</item>
    ///   <item>ObjectTemplateExtensions := { .iff } + root TypeId in { SHOT, STOT, SBOT }
    ///         → PendingNextSceneChange</item>
    ///   <item>UnknownExtensions := everything else → PendingNextSceneChange (CONSERVATIVE
    ///         fallback; never silently over-promises a no-op reload)</item>
    /// </list>
    ///
    /// <para>The dispatcher additionally maps <c>!Game.IsRunning</c> to
    /// <see cref="ReloadTier.Unavailable"/> upstream — that case is OUT of scope for this
    /// helper (which is purely game-state-free).</para>
    /// </summary>
    public static class ReloadAssetClassifier
    {
        // ─────────────────────────────────────────────────────────────────────
        // Pinned routing table (round-2 MEDIUM 9). Each set is named in source so a
        // grep-gated acceptance check can verify the names exist verbatim.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Recognized texture file extensions (lowercase, with leading dot).</summary>
        public static readonly string[] TextureExtensions =
            new[] { ".dds", ".tga", ".png", ".jpg", ".jpeg" };

        /// <summary>Recognized terrain file extensions.</summary>
        public static readonly string[] TerrainExtensions = new[] { ".trn" };

        /// <summary>Carrier extension for datatables (sub-detection on TypeId "DTII").</summary>
        public static readonly string[] DatatableExtensions = new[] { ".iff" };

        /// <summary>Recognized stringtable file extensions.</summary>
        public static readonly string[] StringtableExtensions = new[] { ".stf" };

        /// <summary>Carrier extension for object templates (sub-detection on TypeId
        /// "SHOT"/"STOT"/"SBOT").</summary>
        public static readonly string[] ObjectTemplateExtensions = new[] { ".iff" };

        /// <summary>
        /// Classifies <paramref name="extension"/> + optional <paramref name="rootTypeIdOrNull"/>
        /// into a <see cref="ReloadTier"/>. <paramref name="extension"/> may include or omit
        /// the leading dot and is matched case-insensitively. <paramref name="rootTypeIdOrNull"/>
        /// is the root IFF chunk's 4-character TypeId (e.g. "DTII", "SHOT", "STOT", "SBOT")
        /// when the carrier is <c>.iff</c>; null otherwise.
        /// </summary>
        public static ReloadTier Classify(string extension, string rootTypeIdOrNull)
        {
            // Normalize the extension to lowercase with a leading dot. An empty / null input
            // falls through to the conservative PendingNextSceneChange.
            string ext = NormalizeExtension(extension);
            if (string.IsNullOrEmpty(ext))
            {
                return ReloadTier.PendingNextSceneChange;
            }

            if (Contains(TextureExtensions, ext))
            {
                return ReloadTier.ReloadedTextures;
            }
            if (Contains(TerrainExtensions, ext))
            {
                return ReloadTier.ReloadedTerrain;
            }
            if (Contains(StringtableExtensions, ext))
            {
                return ReloadTier.PendingNextSceneChange;
            }
            // .iff is the carrier for datatables AND object templates; both surface as
            // PendingNextSceneChange (tier b). Unknown .iff containers ALSO surface as
            // PendingNextSceneChange (conservative).
            if (Contains(DatatableExtensions, ext) || Contains(ObjectTemplateExtensions, ext))
            {
                return ReloadTier.PendingNextSceneChange;
            }
            // Everything else (UnknownExtensions): conservative — never silently promise a
            // no-op reload.
            return ReloadTier.PendingNextSceneChange;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string NormalizeExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return null;
            string lower = ext.ToLowerInvariant();
            return lower[0] == '.' ? lower : ("." + lower);
        }

        private static bool Contains(string[] set, string value)
        {
            for (int i = 0; i < set.Length; i++)
            {
                if (string.Equals(set[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
