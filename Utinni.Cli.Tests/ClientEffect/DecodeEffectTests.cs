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
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.ClientEffect
{
    /// <summary>
    /// <c>decode-effect</c> + the <c>decode-iff</c> CLEF auto-dispatch branch golden tests: the JSON envelope
    /// shape (rootType "CLEF", version, ordered commands array) AND a per-command <c>stableId</c> (REVIEWS
    /// HIGH #1). Filter trait: <c>DecodeEffect</c>.
    /// </summary>
    public sealed class DecodeEffectTests : IDisposable
    {
        private readonly string _work;

        public DecodeEffectTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "decodeeffect_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort */ }
        }

        private string WriteFixture(string name, byte[] bytes)
        {
            string p = Path.Combine(_work, name);
            File.WriteAllBytes(p, bytes);
            return p;
        }

        [Fact]
        [Trait("Category", "DecodeEffect")]
        public void DecodeEffect_AllFive_EmitsClefEnvelopeWithPerCommandStableId()
        {
            string path = WriteFixture("all5.iff", ClefTestFixtures.BuildAllFive("0003"));
            CliResult r = InProcessCliRunner.Run("decode-effect", path);

            Assert.Equal(0, r.ExitCode);
            JObject env = JObject.Parse(r.Stdout);
            Assert.Equal("decode-effect", (string)env["command"]);
            Assert.Equal(1, (int)env["schemaVersion"]);

            JObject result = (JObject)env["result"];
            Assert.Equal("clienteffect", (string)result["type"]);
            Assert.Equal("CLEF", (string)result["rootType"]);
            Assert.Equal("0003", (string)result["version"]);

            var commands = (JArray)result["commands"];
            Assert.Equal(5, commands.Count);
            // Every command carries a non-empty stableId (REVIEWS HIGH #1) + a tag.
            foreach (var c in commands)
            {
                Assert.False(string.IsNullOrEmpty((string)c["stableId"]));
                Assert.False(string.IsNullOrEmpty((string)c["tag"]));
            }
            Assert.Contains(commands, c => (string)c["tag"] == "CPAP");
            Assert.Contains(commands, c => (string)c["tag"] == "FFBK");
        }

        [Fact]
        [Trait("Category", "DecodeEffect")]
        public void DecodeIff_OnClefRoot_AutoRoutesToClefEnvelope()
        {
            string path = WriteFixture("clef.iff", ClefTestFixtures.BuildAllFive("0001"));
            CliResult r = InProcessCliRunner.Run("decode-iff", path);

            Assert.Equal(0, r.ExitCode);
            JObject result = (JObject)JObject.Parse(r.Stdout)["result"];
            Assert.Equal("clienteffect", (string)result["type"]);
            Assert.Equal("CLEF", (string)result["rootType"]);
            Assert.Equal("0001", (string)result["version"]);
            Assert.NotEmpty((JArray)result["commands"]);
        }

        [Fact]
        [Trait("Category", "DecodeEffect")]
        public void DecodeEffect_RawCommand_MarkedIsRaw()
        {
            string path = WriteFixture("raw.iff", ClefTestFixtures.BuildWithRawCommand());
            CliResult r = InProcessCliRunner.Run("decode-effect", path);

            Assert.Equal(0, r.ExitCode);
            JObject result = (JObject)JObject.Parse(r.Stdout)["result"];
            var commands = (JArray)result["commands"];
            Assert.Equal(2, commands.Count);
            Assert.Contains(commands, c => (bool)c["isRaw"]);
            Assert.Equal(1, (int)result["rawCommandCount"]);
        }

        [Fact]
        [Trait("Category", "DecodeEffect")]
        public void DecodeEffect_SortedKeys_TopLevelResultKeysAreOrdinalSorted()
        {
            string path = WriteFixture("sorted.iff", ClefTestFixtures.BuildSingleCpap("0003"));
            CliResult r = InProcessCliRunner.Run("decode-effect", path);
            Assert.Equal(0, r.ExitCode);

            JObject result = (JObject)JObject.Parse(r.Stdout)["result"];
            var keys = result.Properties().Select(p => p.Name).ToList();
            var sorted = keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            Assert.Equal(sorted, keys);
        }

        [Fact]
        [Trait("Category", "DecodeEffect")]
        public void DecodeEffect_FileNotFound_Exit3()
        {
            CliResult r = InProcessCliRunner.Run("decode-effect", Path.Combine(_work, "nope.iff"));
            Assert.Equal(3, r.ExitCode);
            Assert.Equal("FileNotFound", (string)((JObject)JObject.Parse(r.Stdout)["error"])["kind"]);
        }
    }
}
