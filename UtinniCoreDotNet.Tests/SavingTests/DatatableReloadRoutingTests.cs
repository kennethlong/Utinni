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
    /// Plan 09-05 defense-in-depth: make the DTII reload-routing case EXPLICIT for Phase 9
    /// traceability. Phase 8's <see cref="ClientReloadDispatcherTests"/> already covers the full
    /// routing-table <see cref="Xunit.TheoryAttribute"/>; these facts pin the (".iff", "DTII")
    /// pair the datatable editor's reload-button delegates to so a regression in the routing
    /// table surfaces against a DTII-named test (not just a buried row in the Phase-8 theory).
    ///
    /// <para><b>Why framework-layer, not plugin-layer:</b> the plugin-side
    /// <c>ClientReloadDispatcher.Dispatch</c> (TJT) gates on <c>Game.IsRunning</c> (a native P/Invoke
    /// into the injected client) and lives in the UtinniPlugins assembly, which this test project
    /// does not (and cannot cleanly) reference. The routing DECISION is the framework-side pure
    /// function <see cref="ReloadAssetClassifier.Classify"/>; that is the testable contract the
    /// dispatcher composes (Phase 8 Plan 05 Task 3 precedent — the plugin dispatcher is smoke-only).</para>
    /// </summary>
    public class DatatableReloadRoutingTests
    {
        [Fact]
        public void ReloadAssetClassifier_IffWithDtiiRootTypeId_ReturnsPendingNextSceneChange()
        {
            // The datatable editor saves a .iff carrier whose root TypeId is "DTII"; the reload
            // dispatcher feeds (".iff", "DTII") to the classifier, which must route to
            // PendingNextSceneChange (tier-(b) per CF-05 — datatables re-resolve on scene change).
            ReloadTier tier = ReloadAssetClassifier.Classify(".iff", "DTII");
            Assert.Equal(ReloadTier.PendingNextSceneChange, tier);
        }

        [Fact]
        public void ReloadAssetClassifier_IffWithDtii_IsNotAnInSessionReloadTier()
        {
            // Confirms DTII never classifies as a texture/terrain in-session reload — the editor's
            // reload-button copy (the CF-05 "reloads on next scene change" wording) is honest only
            // if the classifier never promises a hot-swap for the DTII case.
            ReloadTier tier = ReloadAssetClassifier.Classify(".iff", "DTII");
            Assert.NotEqual(ReloadTier.ReloadedTextures, tier);
            Assert.NotEqual(ReloadTier.ReloadedTerrain, tier);
        }

        [Fact]
        public void ReloadAssetClassifier_DtiiRoutingIsCaseInsensitiveOnExtension()
        {
            // The saved path may carry an uppercase ".IFF" extension on some filesystems; the
            // DTII routing must survive extension casing so the reload-button dispatch is stable.
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify(".IFF", "DTII"));
            Assert.Equal(ReloadTier.PendingNextSceneChange, ReloadAssetClassifier.Classify("iff", "DTII"));
        }
    }
}
