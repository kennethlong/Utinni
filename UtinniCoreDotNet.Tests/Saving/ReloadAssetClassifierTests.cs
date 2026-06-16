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

namespace UtinniCoreDotNet.Tests.Saving
{
    /// <summary>
    /// Plan 11-04 CF-05 verify-only: pins the object-template reload-routing case for the Object
    /// Template Editor's reload badge. The editor saves a <c>.iff</c> carrier whose root TypeId is an
    /// object-template tag (SHOT/STOT/SBOT); the reload dispatcher feeds (".iff", &lt;rootTypeId&gt;) to
    /// <see cref="ReloadAssetClassifier.Classify"/>, which must route to
    /// <see cref="ReloadTier.PendingNextSceneChange"/> (tier-(b) — object templates re-resolve on the
    /// next scene change; the badge wording is honest only if the classifier never promises a hot-swap).
    ///
    /// <para><b>Why framework-layer, not plugin-layer:</b> the plugin-side reload dispatcher gates on
    /// <c>Game.IsRunning</c> (a native P/Invoke into the injected client) and lives in UtinniPlugins,
    /// which this test project does not reference. The routing DECISION is the framework-side pure
    /// function <see cref="ReloadAssetClassifier.Classify"/> — the testable contract the dispatcher
    /// composes (Phase 8 Plan 05 / Phase 9 / Phase 10 precedent; the plugin dispatcher is smoke-only).</para>
    ///
    /// <para><b>VERIFY-ONLY:</b> the classifier already routes object-template <c>.iff</c> via the
    /// SHOT/STOT/SBOT allowlist AND the conservative <c>.iff</c> fallback to the same tier — these
    /// facts assert that contract; the classifier is NOT modified by this plan (RESEARCH Assumption A1).</para>
    /// </summary>
    public class ReloadAssetClassifierTests
    {
        [Theory]
        [InlineData("SHOT")] // shared tangible object template
        [InlineData("STOT")] // shared (intangible/creature etc.) object template
        [InlineData("SBOT")] // shared building object template
        public void Classify_ObjectTemplateRootTypeId_ReturnsPendingNextSceneChange(string rootTypeId)
        {
            // The demo template root (and every object-template root on the SHOT/STOT/SBOT allowlist)
            // must classify as the tier-(b) "reloads on next scene change" routing the badge promises.
            ReloadTier tier = ReloadAssetClassifier.Classify(".iff", rootTypeId);
            Assert.Equal(ReloadTier.PendingNextSceneChange, tier);
        }

        [Fact]
        public void Classify_IffWithUnknownObjectTemplateRoot_FallsBackToPendingNextSceneChange()
        {
            // Conservative .iff fallback: a root TypeId outside the explicit allowlist still routes to
            // PendingNextSceneChange (never silently promises a no-op in-session reload). This covers a
            // template whose root tag the demo corpus surfaces outside SHOT/STOT/SBOT.
            ReloadTier tier = ReloadAssetClassifier.Classify(".iff", "SXXX");
            Assert.Equal(ReloadTier.PendingNextSceneChange, tier);
        }

        [Fact]
        public void Classify_ObjectTemplateRoot_IsNeverAnInSessionReloadTier()
        {
            // The badge's CF-05 candor ("reloads on next scene change; relog to guarantee") is honest
            // only if the object-template case never classifies as a texture/terrain hot-swap.
            ReloadTier tier = ReloadAssetClassifier.Classify(".iff", "SHOT");
            Assert.NotEqual(ReloadTier.ReloadedTextures, tier);
            Assert.NotEqual(ReloadTier.ReloadedTerrain, tier);
        }

        [Fact]
        public void Classify_Trn_ReturnsReloadedTerrain()
        {
            // Phase 21 Plan 01 Wave-0: pin the explicit .trn routing the Terrain editor's reload candor rides
            // (PROD-W2-TRN-05). The Terrain reload path feeds (".trn", null) to the classifier; it must route
            // to ReloadTier.ReloadedTerrain (the tier the candor footer maps to its honest-default copy via
            // TerrainReloadCandor — D-07).
            Assert.Equal(ReloadTier.ReloadedTerrain, ReloadAssetClassifier.Classify(".trn", null));
        }
    }
}
