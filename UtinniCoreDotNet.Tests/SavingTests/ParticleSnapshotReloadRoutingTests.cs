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

using UtinniCoreDotNet.Saving;
using Xunit;

namespace UtinniCoreDotNet.Tests.SavingTests
{
    /// <summary>
    /// Plan 15-07 (RESID-03 / D-14) defense-in-depth: the two NEW Wave-2 editor reload paths —
    /// WorldSnapshot (<c>.ws</c>) and Particle (<c>.prt</c>) — classify with HONEST tier-(b)
    /// <see cref="ReloadTier.PendingNextSceneChange"/> candor, backing the LOCKED Reload Candor
    /// Contract badge copy (15-UI-SPEC §Reload Candor Contract):
    /// <list type="bullet">
    ///   <item>WorldSnapshot badge: <c>Placements re-resolve on the next scene change.</c></item>
    ///   <item>Particle DEGRADED badge: <c>Reloads on next scene change or relog.</c></item>
    /// </list>
    ///
    /// <para>The Particle LIVE-capable badge (<c>Re-triggers live instances on Preview.</c>) is NOT a
    /// classifier tier — it is a runtime affordance gated on the 15-03 retrigger hook + <c>Game.IsRunning</c>,
    /// set in the form, not here. The classifier's job is the HONEST FLOOR: neither new extension may
    /// classify as an instant/live (texture/terrain) tier that would over-promise a reload that did not happen.</para>
    ///
    /// <para><b>Why framework-layer, not plugin-layer:</b> the plugin-side reload dispatcher gates on
    /// <c>Game.IsRunning</c> (a native P/Invoke into the injected client) and lives in UtinniPlugins,
    /// which this test project does not reference. The routing DECISION is the framework-side pure
    /// function <see cref="ReloadAssetClassifier.Classify"/> — the testable contract the dispatcher
    /// composes (Phase 8/9/10/11 precedent; the plugin dispatcher + live SC3 render-on-reload
    /// observation is the 15-08 maintainer smoke).</para>
    /// </summary>
    public class ParticleSnapshotReloadRoutingTests
    {
        // ── Test 1: WorldSnapshot .ws classifies tier-(b) ───────────────────────────────────

        [Fact]
        public void Ws_ClassifiesAsPendingNextSceneChange()
        {
            // The WorldSnapshot reload path saves a .ws carrier; the reload dispatcher feeds (".ws", null)
            // to the classifier, which must route to PendingNextSceneChange (tier-(b)) so the badge
            // "Placements re-resolve on the next scene change." is honest.
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify(".ws", null));
        }

        [Fact]
        public void Ws_CaseInsensitive_RoutesTheSame()
        {
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify(".WS", null));
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify("ws", null));
        }

        [Fact]
        public void Ws_IsAmongTheRecognizedWorldSnapshotExtensions()
        {
            // Named extension set must exist verbatim so a grep-gated acceptance check + a regression in
            // the routing table surfaces against a .ws-named fact (not just the conservative fallback).
            Assert.Contains(".ws", ReloadAssetClassifier.WorldSnapshotExtensions);
        }

        // ── Test 2: Particle .prt classifies tier-(b) in the degraded case ──────────────────

        [Fact]
        public void Prt_ClassifiesAsPendingNextSceneChange()
        {
            // The Particle reload path (DEGRADED — no live retrigger hook this phase per 15-03) saves a
            // .prt carrier; (".prt", null) must route to PendingNextSceneChange (tier-(b)) so the degraded
            // badge "Reloads on next scene change or relog." is honest.
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify(".prt", null));
        }

        [Fact]
        public void Prt_CaseInsensitive_RoutesTheSame()
        {
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify(".PRT", null));
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify("prt", null));
        }

        [Fact]
        public void Prt_IsAmongTheRecognizedParticleExtensions()
        {
            Assert.Contains(".prt", ReloadAssetClassifier.ParticleExtensions);
        }

        // ── Test 3: the conservative unknown-extension fallback is UNCHANGED ────────────────

        [Fact]
        public void UnknownExtension_StillFallsBackToConservativePendingNextSceneChange()
        {
            // An unrecognized extension must NOT be loosened: it still routes to the most-conservative
            // tier (PendingNextSceneChange), never silently promising a no-op in-session reload. This
            // pins that adding .ws/.prt did NOT touch the fallback branch.
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify(".xyz", null));
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify(".totallyunknown", null));
            // Null / empty input also falls through conservatively (unchanged).
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify(null, null));
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify("", null));
        }

        // ── Test 4 (honesty guard): neither new extension is an instant/live tier ───────────

        [Theory]
        [InlineData(".ws")]
        [InlineData(".prt")]
        public void NewExtensions_AreNeverAnInstantOrLiveReloadTier(string extension)
        {
            // The LOCKED Reload Candor Contract forbids over-promising. Neither new editor's reload path
            // may classify as a texture/terrain in-session hot-swap — the badge copy is honest only if
            // the classifier routes to the tier-(b) "pending next scene change" floor, never an instant
            // refresh. (The Particle LIVE-capable copy is a form-side runtime affordance, not a tier.)
            ReloadTier tier = ReloadAssetClassifier.Classify(extension, null);
            Assert.NotEqual(ReloadTier.ReloadedTextures, tier);
            Assert.NotEqual(ReloadTier.ReloadedTerrain, tier);
            Assert.Equal(ReloadTier.PendingNextSceneChange, tier);
        }
    }
}
