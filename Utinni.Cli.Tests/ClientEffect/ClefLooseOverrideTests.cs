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
// Format understood by reading swg-client-v2 .../clientGame/.../ClientEffectTemplate.cpp (SOE/Bootprint,
// All Rights Reserved). No code, comments, identifier names, or test fixtures copied from any reference
// source. Implementation original to Utinni under MIT.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.ClientEffect;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.ClientEffect
{
    /// <summary>
    /// <c>apply-save-effect</c> loose-override containment (REVIEWS HIGH #3): the verb OWNS its
    /// <c>--loose-subdir</c> default "loose" (the convention lands in THIS plan, not borrowed from 22-03).
    /// Asserts the default destination is under <c>&lt;root&gt;\loose\</c>, an empty <c>--loose-subdir</c>
    /// lands at <c>&lt;root&gt;\&lt;rel&gt;</c>, and a traversal escape through EITHER resolve leg is rejected
    /// (mirror <c>TerrainLooseOverridePathTests</c>). Filter trait: <c>ClefLooseOverride</c>.
    /// </summary>
    public sealed class ClefLooseOverrideTests : IDisposable
    {
        private readonly string _work;

        public ClefLooseOverrideTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "clefloose_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort */ }
        }

        private string WriteAt(string subdir, string rel, byte[] bytes)
        {
            string baseDir = string.IsNullOrEmpty(subdir) ? _work : Path.Combine(_work, subdir);
            string p = Path.Combine(baseDir, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllBytes(p, bytes);
            return p;
        }

        private static string StableIdOfTag(string assetPath, string tag)
        {
            CliResult r = InProcessCliRunner.Run("decode-effect", assetPath);
            JObject result = (JObject)JObject.Parse(r.Stdout)["result"];
            return (string)((JArray)result["commands"]).First(c => (string)c["tag"] == tag)["stableId"];
        }

        [Fact]
        [Trait("Category", "ClefLooseOverride")]
        public void ClefLooseOverride_DefaultSubdir_LandsUnderLooseDir()
        {
            const string rel = "effect/cust.iff";
            // Seed under <root>/loose (the default subdir) so the edit resolves to the seeded file.
            string asset = WriteAt("loose", rel, ClefTestFixtures.BuildAllFive("0003"));
            string cpapId = StableIdOfTag(asset, "CPAP");

            CliResult r = InProcessCliRunner.Run("apply-save-effect", rel, "--root", _work,
                "--leaf", cpapId, "--field", "timeInSeconds", "--value", "1.0");
            Assert.Equal(0, r.ExitCode);

            string written = (string)((JObject)JObject.Parse(r.Stdout)["result"])["path"];
            string looseSegment = "loose" + Path.DirectorySeparatorChar;
            Assert.Contains(looseSegment, written, StringComparison.OrdinalIgnoreCase);
            string normalizedLooseBase = Path.GetFullPath(Path.Combine(_work, "loose"));
            Assert.StartsWith(normalizedLooseBase, written, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(asset));
        }

        [Fact]
        [Trait("Category", "ClefLooseOverride")]
        public void ClefLooseOverride_EmptySubdir_LandsAtRootRel()
        {
            const string rel = "effect/legacy.iff";
            // Empty --loose-subdir -> the legacy <root>/<rel> destination.
            string asset = WriteAt("", rel, ClefTestFixtures.BuildAllFive("0003"));
            string cpapId = StableIdOfTag(asset, "CPAP");

            CliResult r = InProcessCliRunner.Run("apply-save-effect", rel, "--root", _work,
                "--loose-subdir", "", "--leaf", cpapId, "--field", "timeInSeconds", "--value", "1.0");
            Assert.Equal(0, r.ExitCode);

            string written = (string)((JObject)JObject.Parse(r.Stdout)["result"])["path"];
            string normalizedRoot = Path.GetFullPath(_work);
            Assert.StartsWith(normalizedRoot, written, StringComparison.OrdinalIgnoreCase);
            // Did NOT land under <root>/loose/.
            string looseBase = Path.GetFullPath(Path.Combine(_work, "loose")) + Path.DirectorySeparatorChar;
            Assert.False(written.StartsWith(looseBase, StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(asset));
        }

        [Fact]
        [Trait("Category", "ClefLooseOverride")]
        public void ClefLooseOverride_TraversalInRelAsset_Rejected_Exit2()
        {
            CliResult r = InProcessCliRunner.Run("apply-save-effect", "../escape.iff", "--root", _work,
                "--leaf", "x", "--field", "timeInSeconds", "--value", "1.0");
            Assert.Equal(2, r.ExitCode);
            Assert.Equal("PathContainment", (string)((JObject)JObject.Parse(r.Stdout)["error"])["kind"]);
        }

        [Fact]
        [Trait("Category", "ClefLooseOverride")]
        public void ClefLooseOverride_TraversalInLooseSubdir_Rejected_Exit2()
        {
            CliResult r = InProcessCliRunner.Run("apply-save-effect", "effect/x.iff", "--root", _work,
                "--loose-subdir", "../escape", "--leaf", "x", "--field", "timeInSeconds", "--value", "1.0");
            Assert.Equal(2, r.ExitCode);
            Assert.Equal("PathContainment", (string)((JObject)JObject.Parse(r.Stdout)["error"])["kind"]);
        }
    }
}
