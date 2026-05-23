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
using System.Reflection;
using System.Text.RegularExpressions;
using Utinni.Cli.Commands;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Tier-1 tests for PluginInspection + NativeExportProbe.
    /// Task 4.1 ships Tests 1-6, 8, 9, 10 (9 tests — iter-4 LOW-5).
    /// Test 7 lives in Task 4.3 (iter-3 MED-5 — depends on wrong-iplugin-shape fixture).
    /// </summary>
    public class PluginInspectionTests
    {
        // ---------------------------------------------------------------------------
        // CrtMatchPlugin path resolver
        // ---------------------------------------------------------------------------

        private static string ResolveCrtMatchPluginPath()
        {
            var baseDir = AppContext.BaseDirectory;

            // Walk up to find the solution root and then bin\Release\
            var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Release", "Utinni.CrtMatchPlugin.dll"));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Debug", "Utinni.CrtMatchPlugin.dll"));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            throw new FileNotFoundException(
                "Phase 3 R-B prerequisite missing: Utinni.CrtMatchPlugin.dll not found at " +
                "bin\\Release\\ or bin\\Debug\\. Build the solution first.",
                "Utinni.CrtMatchPlugin.dll");
        }

        // ---------------------------------------------------------------------------
        // Test 1: NativeExportProbe.HasExport — positive case via CrtMatchPlugin
        // (PRE-FLIGHT smoke promoted to permanent Tier-1 test — REVIEWS HIGH-1)
        // ---------------------------------------------------------------------------

        [Fact]
        public void HasExport_CrtMatchPlugin_CreatePlugin_ReturnsTrue()
        {
            string crtPath = ResolveCrtMatchPluginPath();
            Assert.True(NativeExportProbe.HasExport(crtPath, "createPlugin"),
                "Expected createPlugin export in Utinni.CrtMatchPlugin.dll");
        }

        [Fact]
        public void HasExport_CrtMatchPlugin_DestroyPlugin_ReturnsTrue()
        {
            string crtPath = ResolveCrtMatchPluginPath();
            Assert.True(NativeExportProbe.HasExport(crtPath, "destroyPlugin"),
                "Expected destroyPlugin export in Utinni.CrtMatchPlugin.dll");
        }

        // ---------------------------------------------------------------------------
        // Test 2: NativeExportProbe.HasExport — negative case
        // ---------------------------------------------------------------------------

        [Fact]
        public void HasExport_CrtMatchPlugin_NonExistentSymbol_ReturnsFalse()
        {
            string crtPath = ResolveCrtMatchPluginPath();
            Assert.False(NativeExportProbe.HasExport(crtPath, "totallyNotExportedSymbol"),
                "Expected false for a non-existent export");
        }

        // ---------------------------------------------------------------------------
        // Test 3: NativeExportProbe.HasExport — missing file does not throw
        // ---------------------------------------------------------------------------

        [Fact]
        public void HasExport_MissingFile_DoesNotThrow_ReturnsFalse()
        {
            bool result = NativeExportProbe.HasExport("Z:/nonexistent/file.dll", "createPlugin");
            Assert.False(result, "Expected false for a non-existent file");
        }

        // ---------------------------------------------------------------------------
        // Test 4: NativeExportProbe.HasExport — managed DLL has no native exports
        // ---------------------------------------------------------------------------

        [Fact]
        public void HasExport_ManagedDll_ReturnsFlaseForNativeExport()
        {
            // Use UtinniCoreDotNet.dll (managed, no native createPlugin export).
            string baseDir = AppContext.BaseDirectory;
            string managedDll = Path.Combine(baseDir, "UtinniCoreDotNet.dll");

            // CR-04: a silent `return` here would let xunit report this test as PASSED
            // even though no assertion ever ran — a vacuous pass on any build that
            // doesn't place UtinniCoreDotNet.dll in the test output directory. Fail
            // loudly with an explicit prerequisite assertion instead.
            Assert.True(File.Exists(managedDll),
                "Prerequisite UtinniCoreDotNet.dll not found at " + managedDll +
                ". Ensure the solution is built before running tests.");

            Assert.False(NativeExportProbe.HasExport(managedDll, "createPlugin"),
                "Managed DLL should not have a native createPlugin export");
        }

        // ---------------------------------------------------------------------------
        // Test 5: PluginInspection.InspectDirectory — empty directory
        // ---------------------------------------------------------------------------

        [Fact]
        public void InspectDirectory_EmptyDirectory_ReturnsEmptyReport()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "utinni-test-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var dirReport = PluginInspection.InspectDirectory(tempDir);
                Assert.NotNull(dirReport);
                Assert.Equal(0, dirReport.Plugins.Count);
                Assert.NotNull(dirReport.LoaderObserved);
                Assert.Equal(0, dirReport.LoaderObserved.PluginsCount);
                Assert.NotNull(dirReport.LoaderObserved.LoadErrors);
                Assert.Empty(dirReport.LoaderObserved.LoadErrors);
                Assert.NotNull(dirReport.DirectoryChecks);
                Assert.Equal(1, dirReport.DirectoryChecks.Count);
                Assert.Equal("loader-vs-reflection-agreement", dirReport.DirectoryChecks[0].Id);
                Assert.Equal("pass", dirReport.DirectoryChecks[0].Status);
                Assert.NotNull(dirReport.Summary);
                Assert.Equal(0, dirReport.Summary.TotalPlugins);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, recursive: true);
            }
        }

        // ---------------------------------------------------------------------------
        // Test 6: PluginInspection.InspectDirectory — CrtMatchPlugin (native, kind=native)
        // REVIEWS HIGH-5 + iter-3 HIGH-1 + plan-checker iter-2 WARNING-5
        // ---------------------------------------------------------------------------

        [Fact]
        public void InspectDirectory_CrtMatchPlugin_ClassifiesAsNativeAndFiltersLoadErrors()
        {
            string crtPath = ResolveCrtMatchPluginPath();
            string tempDir = Path.Combine(Path.GetTempPath(), "utinni-test-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);
            string destPath = Path.Combine(tempDir, "Utinni.CrtMatchPlugin.dll");

            try
            {
                File.Copy(crtPath, destPath);
                var dirReport = PluginInspection.InspectDirectory(tempDir);

                Assert.Equal(1, dirReport.Plugins.Count);
                var entry = dirReport.Plugins[0];
                Assert.Equal("Utinni.CrtMatchPlugin", entry.Id);
                Assert.Equal("native", entry.Kind);
                Assert.True(entry.Exports.CreatePlugin);
                Assert.True(entry.Exports.DestroyPlugin);
                Assert.False(entry.Exports.ManagedIPlugin);
                Assert.False(entry.Exports.ManagedIEditorPlugin);
                Assert.Equal("pass", entry.OverallStatus);

                // REVIEWS HIGH-5 + plan-checker iter-2 WARNING-5: LoaderObserved is at directory level.
                Assert.NotNull(dirReport.LoaderObserved);
                Assert.Equal(0, dirReport.LoaderObserved.PluginsCount);

                // Iter-3 HIGH-1: FilterNativeLoadErrors drops native noise.
                Assert.Empty(dirReport.LoaderObserved.LoadErrors);

                // Iter-3 MED-8: directory-level cross-check present.
                Assert.NotNull(dirReport.DirectoryChecks);
                Assert.Equal(1, dirReport.DirectoryChecks.Count);
                Assert.Equal("loader-vs-reflection-agreement", dirReport.DirectoryChecks[0].Id);
                Assert.Equal("pass", dirReport.DirectoryChecks[0].Status);
            }
            finally
            {
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    System.IO.Directory.Delete(tempDir, recursive: true);
                }
                catch (UnauthorizedAccessException)
                {
                    // File still locked by CLR — leave for OS cleanup.
                }
                catch (IOException)
                {
                    // Similar lock scenario — leave for OS cleanup.
                }
            }
        }

        // ---------------------------------------------------------------------------
        // Test 7 (iter-3 MED-5 moved here from Task 4.1; iter-4 MED-2 — kind=managed not unknown):
        // InspectDirectory against wrong-iplugin-shape fixture → kind=managed + iplugin-export-shape=fail
        // ---------------------------------------------------------------------------

        [Fact]
        public void InspectDirectory_WrongIPluginShapeFixture_ManagedWithShapeFail()
        {
            // Resolve the wrong-iplugin-shape fixture DLL.
            string wrongDll = FixturePath.Resolve("plugins/wrong-iplugin-shape/WrongIPluginShape.dll");
            string tempDir = Path.Combine(Path.GetTempPath(), "utinni-test-wis-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string destPath = Path.Combine(tempDir, "WrongIPluginShape.dll");

            try
            {
                File.Copy(wrongDll, destPath);
                var dirReport = PluginInspection.InspectDirectory(tempDir);

                Assert.Equal(1, dirReport.Plugins.Count);
                var entry = dirReport.Plugins[0];

                // Iter-4 MED-2: kind=managed (attribute present → managedIPlugin=true → managed).
                Assert.Equal("managed", entry.Kind);

                // Dual-signal: attribute present but interface NOT implemented.
                Assert.True(entry.Exports.ManagedIPlugin, "ManagedIPlugin should be true (attribute present)");
                Assert.False(entry.Exports.CreatePlugin, "No native createPlugin export");
                Assert.False(entry.Exports.DestroyPlugin, "No native destroyPlugin export");

                // Checks: createPlugin/destroyPlugin are n/a (managed plugin, no native exports expected).
                var createCheck = System.Linq.Enumerable.FirstOrDefault(entry.Checks, c => c.Id == "createplugin-export");
                var destroyCheck = System.Linq.Enumerable.FirstOrDefault(entry.Checks, c => c.Id == "destroyplugin-export");
                Assert.NotNull(createCheck);
                Assert.Equal("n/a", createCheck.Status);
                Assert.NotNull(destroyCheck);
                Assert.Equal("n/a", destroyCheck.Status);

                // iplugin-export-shape must fail (attribute present but interface not implemented).
                var shapeCheck = System.Linq.Enumerable.FirstOrDefault(entry.Checks, c => c.Id == "iplugin-export-shape");
                Assert.NotNull(shapeCheck);
                Assert.Equal("fail", shapeCheck.Status);

                // Overall must fail (shape check fails).
                Assert.Equal("fail", entry.OverallStatus);
            }
            finally
            {
                // ReflectionOnlyLoadFrom holds a file lock on the DLL until the AppDomain
                // is unloaded (or GC runs). Try GC + retry; if still locked, leave the temp dir
                // for OS cleanup rather than failing the test assertion.
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    System.IO.Directory.Delete(tempDir, recursive: true);
                }
                catch (UnauthorizedAccessException)
                {
                    // Lock still held by CLR reflection machinery — leave temp dir for OS cleanup.
                }
                catch (IOException)
                {
                    // Similar lock scenario — leave for OS cleanup.
                }
            }
        }

        // ---------------------------------------------------------------------------
        // Test 8: REVIEWS MEDIUM-15 — AssemblyMetadata marker read via reflection
        // (NOT a source-file walk from BaseDirectory — working-directory-independent)
        // ---------------------------------------------------------------------------

        [Fact]
        public void AssemblyMetadata_ValidatePluginVersion_IsOne()
        {
            var attrs = typeof(Utinni.Cli.Commands.PluginInspection).Assembly
                .GetCustomAttributes(typeof(AssemblyMetadataAttribute), inherit: false);

            var versionAttr = attrs.Cast<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "validate-plugin-version");

            Assert.NotNull(versionAttr);
            Assert.Equal("1", versionAttr.Value);
        }

        // ---------------------------------------------------------------------------
        // Test 9: Source-purity check via comment-stripped grep
        // (secondary guard complementing Test 8 — REVIEWS MEDIUM-15 backstop)
        // ---------------------------------------------------------------------------

        [Fact]
        public void PluginInspection_SourceFile_ContainsNoConsoleOrNewtonsoftReferences()
        {
            // Walk up from AppContext.BaseDirectory (test output dir) to find the source file.
            // Uses AppContext.BaseDirectory rather than Assembly.Location because .NET Framework
            // shadow-copies assemblies to a temp path, making Assembly.Location useless for
            // source file discovery.
            string testDir = AppContext.BaseDirectory;

            // Walk up until we find Utinni.Cli/Commands/PluginInspection.cs
            string sourceFile = null;
            string dir = testDir;
            for (int i = 0; i < 10 && dir != null; i++, dir = Path.GetDirectoryName(dir))
            {
                string candidate = Path.Combine(dir, "Utinni.Cli", "Commands", "PluginInspection.cs");
                if (File.Exists(candidate))
                {
                    sourceFile = candidate;
                    break;
                }
            }

            string diagMsg = "sourceFile not found. testDir=" + testDir;
            Assert.True(sourceFile != null, diagMsg);
            string content = File.ReadAllText(sourceFile);

            // Strip single-line comments.
            string stripped = Regex.Replace(content, @"//.*", string.Empty);

            Assert.DoesNotMatch(@"\bConsole\.", stripped);
            Assert.DoesNotMatch(@"\bNewtonsoft\.", stripped);
        }

        // ---------------------------------------------------------------------------
        // Test 10: Iter-3 HIGH-1 — FilterNativeLoadErrors unit assertion
        // Iter-4 LOW-6: class name is PluginInspectionFilters (not PluginInspection)
        // ---------------------------------------------------------------------------

        [Fact]
        public void FilterNativeLoadErrors_DropsNativeNoise_KeepsOtherErrors()
        {
            var raw = new[]
            {
                "PluginLoader: failed to scan 'native.dll': BadImageFormatException: ...",
                "PluginLoader: failed to scan 'managed.dll': ReflectionTypeLoadException: Type could not be loaded."
            };

            var filtered = PluginInspectionFilters.FilterNativeLoadErrors(raw);

            Assert.Equal(1, filtered.Count);
            Assert.Contains("ReflectionTypeLoadException", filtered[0]);
        }
    }
}
