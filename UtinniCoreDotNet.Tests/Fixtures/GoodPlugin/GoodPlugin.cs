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

using UtinniCore.Utinni;
using UtinniCoreDotNet.PluginFramework;

namespace UtinniCoreDotNet.Tests.Fixtures.GoodPlugin
{
    // Phase 2 (02-01) C-06 fixture: a working plugin that loads next to BrokenPlugin.
    // Used by PluginLoaderTests to assert that surviving plugins still compose when a
    // sibling plugin throws.
    public class GoodPlugin : IPlugin
    {
        public GoodPlugin()
        {
            Information = new PluginInformation("GoodPlugin", "C-06 fixture", "Phase 2");
        }

        public PluginInformation Information { get; }

        public UtINI GetConfig()
        {
            return null;
        }
    }
}
