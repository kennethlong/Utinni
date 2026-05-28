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
    /// Routing-table unit test for the D-06 forced-reload dispatcher's asset classifier
    /// (round-2 MEDIUM 9 / Phase 8 Plan 05 Task 3). The framework-side
    /// <see cref="ReloadAssetClassifier"/> is a pure function over (extension, rootTypeId)
    /// pairs — the plugin-side <c>ClientReloadDispatcher</c> calls it and then dispatches
    /// the resulting <see cref="ReloadTier"/> onto the game thread.
    ///
    /// <para>This test does NOT touch any live-client binding; it exercises the routing
    /// table as a pure function so CI without a live client can still gate the table.</para>
    /// </summary>
    public class ClientReloadDispatcherTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Pinned routing table (round-2 MEDIUM 9) — one [Theory] entry per (ext, expected)
        // pair from the plan's PART A table.
        // ─────────────────────────────────────────────────────────────────────

        [Theory]
        // TextureExtensions → ReloadedTextures
        [InlineData(".dds",  null,   ReloadTier.ReloadedTextures)]
        [InlineData(".tga",  null,   ReloadTier.ReloadedTextures)]
        [InlineData(".png",  null,   ReloadTier.ReloadedTextures)]
        [InlineData(".jpg",  null,   ReloadTier.ReloadedTextures)]
        [InlineData(".jpeg", null,   ReloadTier.ReloadedTextures)]
        // Uppercase / no-leading-dot should normalize and still classify.
        [InlineData("DDS",   null,   ReloadTier.ReloadedTextures)]
        [InlineData(".PNG",  null,   ReloadTier.ReloadedTextures)]
        // TerrainExtensions → ReloadedTerrain
        [InlineData(".trn",  null,   ReloadTier.ReloadedTerrain)]
        [InlineData(".TRN",  null,   ReloadTier.ReloadedTerrain)]
        // StringtableExtensions → PendingNextSceneChange
        [InlineData(".stf",  null,   ReloadTier.PendingNextSceneChange)]
        // DatatableExtensions (.iff with TypeId "DTII") → PendingNextSceneChange
        [InlineData(".iff",  "DTII", ReloadTier.PendingNextSceneChange)]
        // ObjectTemplateExtensions (.iff with TypeId in { SHOT, STOT, SBOT })
        [InlineData(".iff",  "SHOT", ReloadTier.PendingNextSceneChange)]
        [InlineData(".iff",  "STOT", ReloadTier.PendingNextSceneChange)]
        [InlineData(".iff",  "SBOT", ReloadTier.PendingNextSceneChange)]
        // .iff with an unknown / null root TypeId — conservative fallback.
        [InlineData(".iff",  null,    ReloadTier.PendingNextSceneChange)]
        [InlineData(".iff",  "XXXX",  ReloadTier.PendingNextSceneChange)]
        // UnknownExtensions → PendingNextSceneChange (conservative; never silently no-op).
        [InlineData(".bin",  null,    ReloadTier.PendingNextSceneChange)]
        [InlineData(".xml",  null,    ReloadTier.PendingNextSceneChange)]
        // Empty / null extension → conservative.
        [InlineData("",      null,    ReloadTier.PendingNextSceneChange)]
        [InlineData(null,    null,    ReloadTier.PendingNextSceneChange)]
        public void Classify_ProducesExpectedTier(string extension, string rootTypeId, ReloadTier expected)
        {
            ReloadTier actual = ReloadAssetClassifier.Classify(extension, rootTypeId);
            Assert.Equal(expected, actual);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Routing-table set names exist as public fields (grep-gate complement)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void RoutingTableSets_AreExposedAsPublicFields()
        {
            Assert.NotNull(ReloadAssetClassifier.TextureExtensions);
            Assert.NotNull(ReloadAssetClassifier.TerrainExtensions);
            Assert.NotNull(ReloadAssetClassifier.DatatableExtensions);
            Assert.NotNull(ReloadAssetClassifier.StringtableExtensions);
            Assert.NotNull(ReloadAssetClassifier.ObjectTemplateExtensions);
            Assert.NotEmpty(ReloadAssetClassifier.TextureExtensions);
            Assert.NotEmpty(ReloadAssetClassifier.TerrainExtensions);
            Assert.NotEmpty(ReloadAssetClassifier.StringtableExtensions);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Outcome enum surface — confirms all four outcomes are reachable values.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void ReloadTier_HasFourDistinctOutcomes()
        {
            var seen = new System.Collections.Generic.HashSet<ReloadTier>
            {
                ReloadTier.ReloadedTextures,
                ReloadTier.ReloadedTerrain,
                ReloadTier.PendingNextSceneChange,
                ReloadTier.Unavailable,
            };
            Assert.Equal(4, seen.Count);
        }
    }
}
