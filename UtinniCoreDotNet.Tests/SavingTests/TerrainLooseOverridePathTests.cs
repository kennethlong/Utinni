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

using System;
using System.IO;
using UtinniCoreDotNet.Saving;
using Xunit;

namespace UtinniCoreDotNet.Tests.SavingTests
{
    /// <summary>
    /// Plan 21-06 (R2) framework-side contract: a terrain loose override must land under
    /// <c>&lt;root&gt;/loose/&lt;logical&gt;</c> — the same documented loose searchPath every other
    /// editor (IFF/Datatable/STF/OT) targets — NOT directly at <c>&lt;root&gt;/&lt;logical&gt;</c>.
    ///
    /// <para>The plugin-side <c>TJT.Saving.TerrainSaveTargets.SaveLooseOverride</c> (in the sibling
    /// UtinniPlugins repo, which this framework test project cannot reference) composes the destination
    /// with the SAME two-step <see cref="LooseOverridePath.Resolve"/> shape <c>IffSaveTargets</c> uses:
    /// <c>overrideBase = Resolve(root, "loose")</c> then <c>Resolve(overrideBase, relAsset)</c>. This
    /// test pins that composition at the framework layer (mirrors
    /// <c>DatatableSaveTargetsTests.SaveLooseOverride_UsesLooseOverridePathResolve_StaysUnderRoot</c>),
    /// proving the destination is under <c>loose/</c> and that traversal escapes are still rejected.</para>
    /// </summary>
    public sealed class TerrainLooseOverridePathTests
    {
        // The literal loose subdir FormTerrainEditor passes (matching IffSaveTargets' default).
        private const string LooseSubDir = "loose";

        [Fact]
        public void SaveLooseOverride_ComposesUnderLooseSubDir_StaysUnderRoot()
        {
            // Two-step composition the plugin-side SaveLooseOverride performs: first place the override
            // base under <root>/loose, then place the logical asset under that base. The resolved
            // destination must therefore sit under <root>/loose/ — the documented searchPath — not
            // directly at <root>/terrain/naboo.trn (the R2 bug).
            string root = Path.Combine(Path.GetTempPath(), "utinni-21-06-loose-" + Guid.NewGuid().ToString("N"));

            string overrideBase = LooseOverridePath.Resolve(root, LooseSubDir);
            string resolved = LooseOverridePath.Resolve(overrideBase, "terrain/naboo.trn");

            // (a) StartsWith <root>/loose (canonicalized, case-insensitive — Windows fs).
            string normalizedLooseBase = Path.GetFullPath(Path.Combine(root, LooseSubDir));
            Assert.StartsWith(normalizedLooseBase, resolved, StringComparison.OrdinalIgnoreCase);

            // (b) Contains the OS-native "loose" + separator segment, proving the destination lands
            //     UNDER <root>/loose/ rather than directly under <root>.
            string looseSegment = LooseSubDir + Path.DirectorySeparatorChar;
            Assert.Contains(looseSegment, resolved, StringComparison.OrdinalIgnoreCase);

            // And the asset filename is preserved at the leaf.
            Assert.EndsWith("naboo.trn", resolved, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SaveLooseOverride_RejectsTraversalEscape_ThroughComposition()
        {
            // Containment is preserved through the two-step composition: a relAsset that climbs out of
            // the override base must still throw (the editor surfaces it as a save failure, never writes).
            string root = Path.Combine(Path.GetTempPath(), "utinni-21-06-loose-" + Guid.NewGuid().ToString("N"));
            string overrideBase = LooseOverridePath.Resolve(root, LooseSubDir);

            Assert.Throws<ArgumentException>(() =>
                LooseOverridePath.Resolve(overrideBase, "../../escape.trn"));
        }
    }
}
