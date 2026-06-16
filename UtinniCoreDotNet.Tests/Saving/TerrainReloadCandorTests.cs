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
    /// Phase 21 Plan 01 Wave-0 gap: pins the terrain reload-tier → status-footer copy as a single tested
    /// source of truth (21-UI-SPEC Copywriting Contract; 21-VALIDATION Wave-0). The copy lives in
    /// <see cref="TerrainReloadCandor"/> (framework-side) rather than the UtinniPlugins UI so this assertion
    /// is reachable from <c>UtinniCoreDotNet.Tests</c> — the plugin status footer P/Invokes
    /// <c>Game.IsRunning</c> and lives in a repo this test project does not reference (same "framework-layer,
    /// not plugin-layer" rationale as <see cref="ReloadAssetClassifierTests"/>).
    ///
    /// <para>The <see cref="ReloadTier.ReloadedTerrain"/> case asserts the D-07 HONEST DEFAULT: while
    /// <see cref="TerrainReloadCandor.LivePreviewObserved"/> is <c>false</c> (this build), the
    /// <c>ReloadedTerrain</c> tier renders the <see cref="ReloadTier.PendingNextSceneChange"/> copy — it never
    /// over-promises "live" until the Plan 04 maintainer live-smoke observes an in-session re-read.</para>
    /// </summary>
    public class TerrainReloadCandorTests
    {
        [Fact]
        public void StatusCopy_PendingNextSceneChange_IsLockedNeutralCopy()
        {
            TerrainReloadCandor.StatusCopyResult copy =
                TerrainReloadCandor.StatusCopy(ReloadTier.PendingNextSceneChange);
            Assert.Equal("Reloads on next scene change", copy.Text);
            Assert.False(copy.IsError);
        }

        [Fact]
        public void StatusCopy_ReloadedTerrain_HonorsD07Default_RendersPendingCopy()
        {
            // D-07: until the Plan 04 live-smoke flips LivePreviewObserved, the ReloadedTerrain tier MUST
            // render the PendingNextSceneChange copy (the honest default) — never "Reloaded (terrain)."
            Assert.False(TerrainReloadCandor.LivePreviewObserved,
                "This build ships the D-07 honest default; flipping LivePreviewObserved is a Plan 04 smoke gate.");

            TerrainReloadCandor.StatusCopyResult copy =
                TerrainReloadCandor.StatusCopy(ReloadTier.ReloadedTerrain);
            Assert.Equal("Reloads on next scene change", copy.Text);
            Assert.False(copy.IsError);
        }

        [Fact]
        public void StatusCopy_Unavailable_IsLockedErrorCopy()
        {
            TerrainReloadCandor.StatusCopyResult copy =
                TerrainReloadCandor.StatusCopy(ReloadTier.Unavailable);
            Assert.Equal("No live client — start SWG to preview terrain edits in-session.", copy.Text);
            Assert.True(copy.IsError);
        }
    }
}
