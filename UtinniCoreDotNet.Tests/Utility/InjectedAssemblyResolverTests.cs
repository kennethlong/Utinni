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

using System.IO;
using UtinniCoreDotNet.Utility;
using Xunit;

namespace UtinniCoreDotNet.Tests.Utility
{
    /// <summary>
    /// Framework-leg tests for the Plan 15-12 <see cref="InjectedAssemblyResolver"/> probe-path
    /// decision (PROD-W2-PRT / RESID-03 / 15-SMOKE B5). The helper is pure/BCL-only; it maps a
    /// requested assembly display name to a probe path under the inject root and returns it ONLY
    /// when the file exists, never hijacking unrelated resolution. The <c>AssemblyResolve</c> event
    /// hands the FULL display name (<c>name, Version=…, Culture=…, PublicKeyToken=…</c>), so the
    /// decision reduces it to the simple name before the narrow allow-list check. The live
    /// <c>File.Exists</c> probe is stubbed by a <c>fileExists</c> delegate so these facts have no
    /// disk dependency. Class name contains "InjectedAssemblyResolver" so the plan's
    /// <c>--filter InjectedAssemblyResolver</c> selects exactly these facts.
    /// </summary>
    public class InjectedAssemblyResolverTests
    {
        private const string InjectRoot = @"C:\inject\root";

        // Test 1: an allow-listed name (netstandard) with the file present resolves to the
        // inject-root path for that dll.
        [Fact]
        public void ResolveProbePath_Netstandard_FilePresent_ReturnsInjectRootDllPath()
        {
            string result = InjectedAssemblyResolver.ResolveProbePath(InjectRoot, "netstandard", name => true);

            Assert.Equal(Path.Combine(InjectRoot, "netstandard.dll"), result);
        }

        // Test 2: the second allow-listed name (UtinniCoreDotNet.PathContainment) with the file
        // present resolves to the inject-root path for that dll.
        [Fact]
        public void ResolveProbePath_PathContainment_FilePresent_ReturnsInjectRootDllPath()
        {
            string result = InjectedAssemblyResolver.ResolveProbePath(
                InjectRoot, "UtinniCoreDotNet.PathContainment", name => true);

            Assert.Equal(Path.Combine(InjectRoot, "UtinniCoreDotNet.PathContainment.dll"), result);
        }

        // Test 3: an allow-listed name with NO file on disk returns null — the resolver must not
        // claim to resolve a path it cannot actually load.
        [Fact]
        public void ResolveProbePath_Netstandard_FileAbsent_ReturnsNull()
        {
            string result = InjectedAssemblyResolver.ResolveProbePath(InjectRoot, "netstandard", name => false);

            Assert.Null(result);
        }

        // Test 4: a name NOT on the narrow allow-list returns null even if the file would exist —
        // the handler is narrow and never hijacks unrelated resolution.
        [Theory]
        [InlineData("System.Core")]
        [InlineData("Newtonsoft.Json")]
        [InlineData("UtinniCoreDotNet")]
        public void ResolveProbePath_NonAllowListedName_ReturnsNull(string requested)
        {
            string result = InjectedAssemblyResolver.ResolveProbePath(InjectRoot, requested, name => true);

            Assert.Null(result);
        }

        // Test 5: the FULL display name handed by the AssemblyResolve event is reduced to its simple
        // name before the allow-list check, so a versioned/culture/token name still resolves.
        [Fact]
        public void ResolveProbePath_FullDisplayName_ReducedToSimpleName_Resolves()
        {
            const string fullName =
                "UtinniCoreDotNet.PathContainment, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

            string result = InjectedAssemblyResolver.ResolveProbePath(InjectRoot, fullName, name => true);

            Assert.Equal(Path.Combine(InjectRoot, "UtinniCoreDotNet.PathContainment.dll"), result);
        }
    }
}
