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
using UtinniCore.Utinni;
using UtinniCoreDotNet.PluginFramework;

namespace UtinniCoreDotNet.Tests.Fixtures.BrokenPlugin
{
    // Phase 2 (02-01) C-06 fixture: a plugin that deliberately throws during construction.
    // MEF's DirectoryCatalog discovers this via the [InheritedExport(typeof(IPlugin))]
    // attribute on IPlugin (UtinniCoreDotNet/PluginFramework/IPlugin.cs:44); the throwing
    // ctor exercises the per-plugin try/catch isolation introduced by C-06.
    public class BrokenPlugin : IPlugin
    {
        public BrokenPlugin()
        {
            throw new InvalidOperationException(
                "BrokenPlugin deliberately throws during construction - exercises C-06 isolation.");
        }

        public PluginInformation Information { get; }

        public UtINI GetConfig()
        {
            return null;
        }
    }
}
