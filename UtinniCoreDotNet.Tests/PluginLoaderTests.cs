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
    // C-06 regression: PluginLoader.Load previously composed all plugins via a single
    // AggregateCatalog + ComposeParts, so one plugin's load failure (throwing ctor,
    // ReflectionTypeLoadException, etc.) tore down EVERY plugin's discovery. The fix
    // is per-plugin try/catch isolation around each DirectoryCatalog composition.
    //
    // These tests exercise the testability seam Load(string pluginDir): a temp dir is
    // populated with copies of the BrokenPlugin and GoodPlugin fixture DLLs (built by
    // Phase-2 Wave-0 Task 1 fixture csprojs), and the loader is pointed at it. The
    // assertions check that GoodPlugin still composes when BrokenPlugin's ctor throws,
    // and that the broken plugin's name + the InvalidOperationException message are
    // captured in PluginLoader.LoadErrors (test-friendly surface that mirrors Log.Error).
    public class PluginLoaderTests
    {
        // The fixture DLLs land at: <repo>/UtinniCoreDotNet.Tests/Fixtures/{Broken,Good}Plugin/bin/Release/net472/*.dll
        // The test runs from:        <repo>/UtinniCoreDotNet.Tests/bin/Release/net472/
        // So the relative walk from AppContext.BaseDirectory is: ../../Fixtures/<Name>/bin/Release/net472/<Name>.dll
        private static string FindFixtureDll(string fixtureName)
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "Fixtures", fixtureName, "bin", "Release", "net472", fixtureName + ".dll"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
            // Fallback: net472 may be re-routed under x86 if AppendPlatformToOutputPath is changed.
            candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "Fixtures", fixtureName, "bin", "Release", "net472", fixtureName + ".dll"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
            throw new FileNotFoundException(
                "Could not find " + fixtureName + ".dll near " + baseDir
                + " (looked in ../../Fixtures and ../../../Fixtures)");
        }

        private static string MakeTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "Utinni-PluginLoaderTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void Load_WithOneBrokenAndOneGoodPlugin_LoadsGoodAndLogsBroken()
        {
            var broken = FindFixtureDll("BrokenPlugin");
            var good = FindFixtureDll("GoodPlugin");
            var tempDir = MakeTempDir();
            try
            {
                File.Copy(broken, Path.Combine(tempDir, "BrokenPlugin.dll"));
                File.Copy(good, Path.Combine(tempDir, "GoodPlugin.dll"));

                var loader = new PluginLoader(autoLoad: false);
                loader.Load(tempDir);

                Assert.Equal(1, loader.Plugins.Count());
                Assert.Equal("GoodPlugin", loader.Plugins.First().GetType().Name);

                // The broken plugin name + an indication of the exception MUST surface
                // in LoadErrors (the C-06 invariant: silent failure was the bug).
                var combined = string.Join("\n", loader.LoadErrors);
                Assert.Contains("BrokenPlugin", combined);
                Assert.True(
                    combined.Contains("InvalidOperationException")
                    || combined.Contains("deliberately throws"),
                    "LoadErrors should mention the exception type or message. Actual:\n" + combined);
            }
            finally
            {
                TryDeleteDir(tempDir);
            }
        }

        [Fact]
        public void Load_AllPluginsBroken_ResultsInZeroLoaded_AndAllLogged()
        {
            var broken = FindFixtureDll("BrokenPlugin");
            var tempDir = MakeTempDir();
            try
            {
                File.Copy(broken, Path.Combine(tempDir, "BrokenPlugin.dll"));

                var loader = new PluginLoader(autoLoad: false);
                loader.Load(tempDir);

                Assert.Equal(0, loader.Plugins.Count());
                Assert.Contains("BrokenPlugin", string.Join("\n", loader.LoadErrors));
            }
            finally
            {
                TryDeleteDir(tempDir);
            }
        }

        [Fact]
        public void Load_EmptyDirectory_LoadsZeroPlugins_NoExceptions()
        {
            var tempDir = MakeTempDir();
            try
            {
                // autoLoad: false skips LoadFromPluginManager which P/Invokes into
                // UtinniCore.dll (not deployed alongside the test runner in CI).
                var loader = new PluginLoader(autoLoad: false);
                var ex = Record.Exception(() => loader.Load(tempDir));
                Assert.Null(ex);
                Assert.Equal(0, loader.Plugins.Count());
            }
            finally
            {
                TryDeleteDir(tempDir);
            }
        }

        // DirectoryCatalog loads each fixture DLL into the test AppDomain; the file
        // stays locked until the process exits. Cleanup is best-effort.
        private static void TryDeleteDir(string dir)
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
