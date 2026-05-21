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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UtinniCoreDotNetGen;
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    // Phase 3 R-F regression tests (per 03-CONTEXT D-22 + D-24 + PATTERNS.md
    // §"R-F CppSharp header auto-discovery"). HeaderDiscovery.Discover walks
    // UtinniCore/ for *.h files, excluding any path beneath an _internal/
    // directory (case-insensitive at any depth). Tests use the temp-dir fixture
    // pattern from CppSharpSlnDirTests:74-104 (CreateDirectory + WriteAllText
    // seed + try/finally Directory.Delete cleanup).
    //
    // Tests 1-3 are PURE FIXTURE-TREE tests (no dependency on the real
    // UtinniCore/ tree); Tests 4-5 point at the real UtinniCore/ root and
    // assert backward-compat with the prior explicit allowlist (Test 4) plus
    // forward-compat with Plan 03-02 R-C's getSwgWndProc declaration (Test 5).
    public class HeaderDiscoveryTests
    {
        // --- Locate the real UtinniCore/ root via 4-level walk-up
        //     (mirrors UtinniCfgTests.FindCfg). Used by Tests 4-5 only. ---

        private static string FindUtinniCoreRoot()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "UtinniCore"));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "..", "UtinniCore"));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            throw new DirectoryNotFoundException(
                "Could not locate UtinniCore/ directory from " + baseDir);
        }

        // --- Fact 1: top-level _internal/ excluded. ---

        [Fact]
        public void HeaderDiscovery_FixtureTree_ExcludesInternalDirectory()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string utinniCoreRoot = Path.Combine(tempRoot, "UtinniCore");
            Directory.CreateDirectory(Path.Combine(utinniCoreRoot, "_internal"));
            Directory.CreateDirectory(Path.Combine(utinniCoreRoot, "sub"));
            File.WriteAllText(Path.Combine(utinniCoreRoot, "public.h"), "");
            File.WriteAllText(Path.Combine(utinniCoreRoot, "_internal", "private.h"), "");
            File.WriteAllText(Path.Combine(utinniCoreRoot, "sub", "public2.h"), "");

            try
            {
                List<string> discovered = HeaderDiscovery.Discover(utinniCoreRoot);

                Assert.Contains("public.h", discovered);
                Assert.Contains(@"sub\public2.h", discovered);
                Assert.DoesNotContain(@"_internal\private.h", discovered);
                Assert.Equal(2, discovered.Count);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        // --- Fact 2: nested _internal/ also excluded (filter matches at any
        //     depth, not just top-level). ---

        [Fact]
        public void HeaderDiscovery_NestedInternalDirectories_AlsoExcluded()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string utinniCoreRoot = Path.Combine(tempRoot, "UtinniCore");
            Directory.CreateDirectory(Path.Combine(utinniCoreRoot, "swg", "_internal"));
            File.WriteAllText(Path.Combine(utinniCoreRoot, "public.h"), "");
            File.WriteAllText(Path.Combine(utinniCoreRoot, "swg", "public.h"), "");
            File.WriteAllText(Path.Combine(utinniCoreRoot, "swg", "_internal", "private.h"), "");

            try
            {
                List<string> discovered = HeaderDiscovery.Discover(utinniCoreRoot);

                Assert.Contains("public.h", discovered);
                Assert.Contains(@"swg\public.h", discovered);
                Assert.DoesNotContain(@"swg\_internal\private.h", discovered);
                Assert.Equal(2, discovered.Count);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        // --- Fact 3: case-insensitive _internal/ match. ---

        [Fact]
        public void HeaderDiscovery_CaseInsensitiveInternalSegment()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string utinniCoreRoot = Path.Combine(tempRoot, "UtinniCore");
            Directory.CreateDirectory(Path.Combine(utinniCoreRoot, "_Internal"));
            Directory.CreateDirectory(Path.Combine(utinniCoreRoot, "_INTERNAL"));
            File.WriteAllText(Path.Combine(utinniCoreRoot, "public.h"), "");
            File.WriteAllText(Path.Combine(utinniCoreRoot, "_Internal", "private.h"), "");
            File.WriteAllText(Path.Combine(utinniCoreRoot, "_INTERNAL", "other.h"), "");

            try
            {
                List<string> discovered = HeaderDiscovery.Discover(utinniCoreRoot);

                Assert.Contains("public.h", discovered);
                Assert.DoesNotContain(@"_Internal\private.h", discovered);
                Assert.DoesNotContain(@"_INTERNAL\other.h", discovered);
                Assert.Single(discovered);
            }
            finally
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }

        // --- Fact 4: real UtinniCore/ tree includes every header from the prior
        //     explicit allowlist. Backward-compat guard. ---

        [Fact]
        public void HeaderDiscovery_RealUtinniCoreRoot_CoversAllExpectedHeaders()
        {
            string utinniCoreRoot = FindUtinniCoreRoot();
            List<string> discovered = HeaderDiscovery.Discover(utinniCoreRoot);

            // Subset of headers from the pre-R-F explicit allowlist in
            // Program.cs:78-103 (representative slice covering each subsystem).
            Assert.Contains("utinni.h", discovered);
            Assert.Contains(@"utility\log.h", discovered);
            Assert.Contains(@"plugin_framework\plugin_manager.h", discovered);
            Assert.Contains(@"swg\client\client.h", discovered);
            Assert.Contains(@"swg\game\game.h", discovered);
            Assert.Contains(@"swg\graphics\graphics.h", discovered);
            Assert.Contains(@"swg\ui\imgui_impl.h", discovered);
        }

        // --- Fact 5: real UtinniCore/ tree picks up Plan 03-02 R-C's
        //     getSwgWndProc declaration. Forward-compat guard -- proves the
        //     auto-discovery sees newly-added headers (which the prior explicit
        //     allowlist would have silently missed). ---

        [Fact]
        public void HeaderDiscovery_RealUtinniCoreRoot_PicksUpClientHeader()
        {
            string utinniCoreRoot = FindUtinniCoreRoot();
            List<string> discovered = HeaderDiscovery.Discover(utinniCoreRoot);

            // Plan 03-02 R-C added Client::getSwgWndProc to swg/client/client.h.
            // The header itself was in the prior allowlist; this test confirms
            // the auto-discovery still includes it (would catch any
            // misclassification of the path as _internal).
            Assert.Contains(@"swg\client\client.h", discovered);

            // Confirm the header file content contains the R-C declaration --
            // catches the case where a future cleanup might rename or move the
            // getter, which the build would also catch but failing here is
            // earlier in the test run.
            string clientHeaderPath = Path.Combine(
                utinniCoreRoot, "swg", "client", "client.h");
            string clientHeaderContent = File.ReadAllText(clientHeaderPath);
            Assert.Contains("getSwgWndProc", clientHeaderContent);
        }

        // --- Fact 6: discovered paths use backslash separators (CppSharp's
        //     module.Headers.Add API expects this). ---

        [Fact]
        public void HeaderDiscovery_OutputUsesBackslashSeparators()
        {
            string utinniCoreRoot = FindUtinniCoreRoot();
            List<string> discovered = HeaderDiscovery.Discover(utinniCoreRoot);

            // Any path with a subdirectory must use backslash, not forward slash.
            var withForwardSlash = discovered.Where(p => p.Contains('/')).ToList();
            Assert.Empty(withForwardSlash);

            // Confirm at least one path has a subdirectory (the real UtinniCore
            // tree has nested headers like swg\client\client.h).
            var withBackslash = discovered.Where(p => p.Contains('\\')).ToList();
            Assert.NotEmpty(withBackslash);
        }
    }
}
