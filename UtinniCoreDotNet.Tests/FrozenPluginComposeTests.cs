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
using System.Linq;
using Xunit;
using UtinniCoreDotNet.PluginFramework;

namespace UtinniCoreDotNet.Tests
{
    // Phase 17 CPPS-04b — the ABI gate's second layer of defense-in-depth.
    //
    // A committed, PRE-BUILT (never-rebuilt-in-CI) The Jawa Toolbox host plugin DLL is
    // MEF-composed against the FRESHLY-BUILT UtinniCoreDotNet. TJT is the broadest binding
    // consumer (778 references across 72 files — CONTEXT D-02), so one fixture gives full
    // ABI coverage. If a regen removed / re-signatured a public member the frozen plugin
    // calls (or shifted a binary signature — e.g. a defaulted [CallerMemberName] param,
    // RESEARCH Pitfall 3), composition lands a MissingMethod / Composition /
    // ReflectionTypeLoad failure in PluginLoader.LoadErrors and this test goes red.
    //
    // DELIBERATE inversion vs Fixtures/{BrokenPlugin,GoodPlugin}: those are REBUILT each
    // run (their own csprojs), so they always match the current surface. THIS fixture is a
    // committed binary referenced as <Content> with NO rebuild csproj — a rebuilt "frozen"
    // plugin always matches → dead gate (RESEARCH Pitfall 6). When an intentional surface
    // change lands, the maintainer re-freezes the fixture via the D-01 --rebless lockstep.
    public class FrozenPluginComposeTests
    {
        private const string FrozenDllName = "TheJawaToolboxDotNet.dll";

        // The frozen DLL is committed DIRECTLY under Fixtures/FrozenPlugin/ (no
        // bin/Release/net472 build-output subpath) and content-copied beside the test
        // assembly. Probe the copied location first, then walk up to the committed source.
        private static string FindFrozenDll()
        {
            var baseDir = AppContext.BaseDirectory;

            // 1. Content-copied beside the test runner (csproj <Content> with Link).
            var candidate = Path.Combine(baseDir, "Fixtures", "FrozenPlugin", FrozenDllName);
            if (File.Exists(candidate)) return candidate;

            // 2. Committed source location: ../../Fixtures/FrozenPlugin/ (mirrors the
            //    PluginLoaderTests fixture walk).
            candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "Fixtures", "FrozenPlugin", FrozenDllName));
            if (File.Exists(candidate)) return candidate;

            // 3. +1-level fallback for x86-rerouted output paths.
            candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "Fixtures", "FrozenPlugin", FrozenDllName));
            if (File.Exists(candidate)) return candidate;

            throw new FileNotFoundException(
                "frozen plugin fixture " + FrozenDllName + " missing near " + baseDir
                + " (looked in Fixtures/FrozenPlugin and ../../Fixtures/FrozenPlugin)");
        }

        private static string MakeTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "Utinni-FrozenPluginCompose-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void FrozenPlugin_ComposesAgainstCurrentBindings_NoLoadErrors()
        {
            var frozen = FindFrozenDll();
            var tempDir = MakeTempDir();
            try
            {
                File.Copy(frozen, Path.Combine(tempDir, Path.GetFileName(frozen)));

                // autoLoad:false skips LoadFromPluginManager's native P/Invoke into
                // UtinniCore.dll (not deployed beside the test runner) — mirrors
                // PluginLoaderTests' rationale.
                var loader = new PluginLoader(autoLoad: false);
                loader.Load(tempDir);

                // A regen that broke the binding ABI surfaces here as a compose failure.
                Assert.True(loader.LoadErrors.Count == 0,
                    "frozen TJT plugin failed to compose — a regen likely broke the binding ABI "
                    + "(re-freeze the fixture via the --rebless lockstep if the surface change was "
                    + "intentional):\n" + string.Join("\n", loader.LoadErrors));

                // The frozen plugin actually composed (not a silent empty pass).
                Assert.NotEmpty(loader.Plugins);
            }
            finally
            {
                TryDeleteDir(tempDir);
            }
        }

        // DirectoryCatalog loads the fixture DLL into the test AppDomain; the file stays
        // locked until the process exits. Cleanup is best-effort.
        private static void TryDeleteDir(string dir)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
