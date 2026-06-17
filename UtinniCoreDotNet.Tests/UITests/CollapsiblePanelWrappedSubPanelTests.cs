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

using System.Linq;
using System.Windows.Forms;
using UtinniCoreDotNet.UI.Controls;
using Xunit;

namespace UtinniCoreDotNet.Tests.UITests
{
    /// <summary>
    /// Phase-21 R3 regression: <see cref="CollapsiblePanel"/> realizes its wrapped
    /// <see cref="SubPanel"/> into <c>Controls</c> lazily (only on expand). On a fresh session the
    /// Terrain section is collapsed by default, so the recursive <c>Controls</c> walk in
    /// <c>FormTreBrowser.FindTerrainSubPanel</c> returned null → "Terrain Editor is unavailable in
    /// this session." The fix exposes a public read-only <see cref="CollapsiblePanel.WrappedSubPanel"/>
    /// accessor returning the field held since construction — reachable regardless of expand state.
    /// These Facts pin that collapsed-state reachability while documenting the lazy-realize root cause
    /// the accessor sidesteps. No TJT type / MEF compose here — the live hand-off click is covered by
    /// the phase live-smoke path.
    /// </summary>
    public class CollapsiblePanelWrappedSubPanelTests
    {
        [Fact]
        public void WrappedSubPanel_IsReachable_WhileCollapsed()
        {
            SubPanel sub = new SubPanel("terrain", isOpenByDefault: false);
            CollapsiblePanel panel = new CollapsiblePanel(sub);

            // (1) Collapsed by default since IsOpenByDefault == false.
            Assert.False(panel.Open);

            // (2) The accessor resolves the SAME instance while collapsed (the R3 regression).
            Assert.NotNull(panel.WrappedSubPanel);
            Assert.Same(sub, panel.WrappedSubPanel);

            // (3) Document the lazy-realize root cause: the SubPanel is NOT in Controls while collapsed.
            Assert.DoesNotContain(sub, panel.Controls.Cast<Control>());

            // (4) Idempotent: after expanding, the accessor still returns the same instance.
            panel.Open = true;
            Assert.Same(sub, panel.WrappedSubPanel);

            // ...and after collapsing again it remains reachable (never null).
            panel.Open = false;
            Assert.Same(sub, panel.WrappedSubPanel);
        }
    }
}
