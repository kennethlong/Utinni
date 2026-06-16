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
    /// PRESENTATION-COPY ONLY. Maps a <see cref="ReloadTier"/> to the LOCKED terrain status-footer text +
    /// its error/neutral colour intent — the single testable source of truth for the Phase 21 terrain
    /// editor's reload candor (21-UI-SPEC Copywriting Contract). This is NOT reload logic and NOT codec
    /// logic: it composes the existing <see cref="ReloadTier"/> enum (from <see cref="ReloadAssetClassifier"/>)
    /// and returns copy. It dispatches no reload, touches no <c>GroundScene</c>/<c>AddMainLoopCall</c>, and
    /// encodes no bytes.
    ///
    /// <para><b>Why framework-side (not in the TJT plugin UI):</b> the status copy lives in the UtinniPlugins
    /// editor, which <c>UtinniCoreDotNet.Tests</c> cannot reference (the plugin P/Invokes <c>Game.IsRunning</c>).
    /// Extracting the tier→copy map into this pure helper makes the locked-string assertion reachable from the
    /// framework-layer test suite — the exact resolution PATTERNS.md flags for the candor-copy Wave-0 gap, and
    /// the same "framework-layer, not plugin-layer" rationale carried by <see cref="ReloadAssetClassifier"/>'s
    /// own tests. The TJT terrain editor's status footer consumes <see cref="StatusCopy"/> verbatim so the UI
    /// and the test assert ONE source of truth.</para>
    ///
    /// <para><b>D-07 honest default (21-CONTEXT D-07 / 21-RESEARCH Open Questions §D-07):</b> the classifier
    /// returns <see cref="ReloadTier.ReloadedTerrain"/> for <c>.trn</c>, but static analysis cannot prove the
    /// native <c>GroundScene::ReloadTerrain</c> visibly re-reads a procedurally-edited <c>.trn</c> in-session
    /// (the body is the SWG client's, not ours). Until a maintainer live-smoke observes an in-session re-read
    /// (Plan 04; precedent: the Phase 10 stringtable SC3 live-reload residual todo), this build ships the
    /// HONEST <see cref="ReloadTier.PendingNextSceneChange"/> copy for the <see cref="ReloadTier.ReloadedTerrain"/>
    /// tier — it never over-promises "live." The <see cref="LivePreviewObserved"/> const gates that wording:
    /// flip it to <c>true</c> ONLY when the Plan 04 smoke confirms in-session re-read, at which point the
    /// <see cref="ReloadTier.ReloadedTerrain"/> tier returns the "Reloaded (terrain)" copy.</para>
    /// </summary>
    public static class TerrainReloadCandor
    {
        /// <summary>
        /// Gate for the D-07 honest default. While <c>false</c> (this build), the
        /// <see cref="ReloadTier.ReloadedTerrain"/> tier renders the <see cref="ReloadTier.PendingNextSceneChange"/>
        /// copy ("Reloads on next scene change") — never the over-promising "Reloaded (terrain)." Flip to
        /// <c>true</c> ONLY after the Plan 04 maintainer live-smoke observes the native
        /// <c>GroundScene::ReloadTerrain</c> visibly re-reading a procedurally-edited <c>.trn</c> in-session
        /// (precedent: <c>phase10-stringtable-sc3-live-reload-residual</c>, the Phase 10 SC3
        /// observe-then-honestly-label disposition). Do NOT flip it on static reasoning alone.
        /// </summary>
        public const bool LivePreviewObserved = false;

        // Locked copy (21-UI-SPEC Copywriting Contract — verbatim from the shipped FormIffEditor /
        // FormParticleEditor reload-candor switch + ClientReloadDispatcher tier vocabulary). Do NOT soften
        // or invent new wording (D-05/D-07).
        private const string LiveTerrainText = "Reloaded (terrain)";
        private const string PendingText = "Reloads on next scene change";
        private const string UnavailableText = "No live client — start SWG to preview terrain edits in-session.";

        /// <summary>
        /// Terrain status-footer copy for one reload tier: the locked <see cref="Text"/> and whether it is an
        /// error state (<see cref="IsError"/> drives <c>Color.Red</c> vs the neutral <c>Colors.Font()</c>;
        /// there is NO green "success" hue per the UI-SPEC Color contract / D-07).
        /// </summary>
        public struct StatusCopyResult
        {
            /// <summary>The locked status-footer text for the tier.</summary>
            public string Text { get; }

            /// <summary>True iff this tier renders as an error (red); false for the neutral tiers.</summary>
            public bool IsError { get; }

            /// <summary>Constructs a status-copy result.</summary>
            public StatusCopyResult(string text, bool isError)
            {
                Text = text;
                IsError = isError;
            }
        }

        /// <summary>
        /// Returns the locked terrain status-footer copy + error-intent for <paramref name="tier"/>.
        ///
        /// <para><see cref="ReloadTier.ReloadedTerrain"/> honours the D-07 default: it renders the
        /// <see cref="ReloadTier.PendingNextSceneChange"/> copy while <see cref="LivePreviewObserved"/> is
        /// <c>false</c>, and only returns "Reloaded (terrain)" once the Plan 04 smoke flips that const.
        /// <see cref="ReloadTier.ReloadedTextures"/> is not a terrain-editor tier (the terrain reload path
        /// classifies <c>.trn</c> as <see cref="ReloadTier.ReloadedTerrain"/>); it is mapped to the
        /// conservative pending copy so the helper is total over the enum and never over-promises.</para>
        /// </summary>
        public static StatusCopyResult StatusCopy(ReloadTier tier)
        {
            switch (tier)
            {
                case ReloadTier.ReloadedTerrain:
                    // D-07 honest default: ship the PendingNextSceneChange copy until the Plan 04 live-smoke
                    // confirms in-session procedural re-read.
                    return LivePreviewObserved
                        ? new StatusCopyResult(LiveTerrainText, false)
                        : new StatusCopyResult(PendingText, false);

                case ReloadTier.PendingNextSceneChange:
                    return new StatusCopyResult(PendingText, false);

                case ReloadTier.Unavailable:
                    return new StatusCopyResult(UnavailableText, true);

                case ReloadTier.ReloadedTextures:
                default:
                    // Not a terrain-editor tier; degrade to the conservative pending copy (never over-promise).
                    return new StatusCopyResult(PendingText, false);
            }
        }
    }
}
