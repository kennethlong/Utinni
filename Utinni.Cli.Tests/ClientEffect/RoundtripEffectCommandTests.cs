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
using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.ClientEffect
{
    /// <summary>
    /// <c>roundtrip-effect</c> byte-exact gate goldens: exit 0 + bytesIdentical on a clean fixture, exit 3
    /// on a missing path, exit 2 on a corrupt file. Filter trait: <c>RoundtripEffectCommand</c>.
    /// </summary>
    public sealed class RoundtripEffectCommandTests : IDisposable
    {
        private readonly string _work;

        public RoundtripEffectCommandTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "rteffect_" + Guid.NewGuid().ToString("N"));
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

        [Theory]
        [Trait("Category", "RoundtripEffectCommand")]
        [InlineData("0001")]
        [InlineData("0002")]
        [InlineData("0003")]
        public void RoundtripEffectCommand_CleanFixture_BytesIdenticalTrue_Exit0(string version)
        {
            string path = WriteFixture("rt_" + version + ".iff", ClefTestFixtures.BuildAllFive(version));
            CliResult r = InProcessCliRunner.Run("roundtrip-effect", path);

            Assert.Equal(0, r.ExitCode);
            JObject env = JObject.Parse(r.Stdout);
            Assert.Equal("roundtrip-effect", (string)env["command"]);
            JObject result = (JObject)env["result"];
            Assert.True((bool)result["bytesIdentical"]);
            Assert.Equal("whole-file", (string)result["comparisonGranularity"]);
            Assert.Equal("CLEF", (string)result["rootType"]);
            Assert.Equal(version, (string)result["version"]);
        }

        [Fact]
        [Trait("Category", "RoundtripEffectCommand")]
        public void RoundtripEffectCommand_FileNotFound_Exit3()
        {
            CliResult r = InProcessCliRunner.Run("roundtrip-effect", Path.Combine(_work, "nope.iff"));
            Assert.Equal(3, r.ExitCode);
            Assert.Equal("FileNotFound", (string)((JObject)JObject.Parse(r.Stdout)["error"])["kind"]);
        }

        [Fact]
        [Trait("Category", "RoundtripEffectCommand")]
        public void RoundtripEffectCommand_CorruptFile_Exit2()
        {
            // A non-CLEF, non-IFF blob — the IFF reader / CLEF decoder rejects it with exit 2.
            string path = WriteFixture("bad.iff", new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });
            CliResult r = InProcessCliRunner.Run("roundtrip-effect", path);
            Assert.Equal(2, r.ExitCode);
            Assert.NotNull(JObject.Parse(r.Stdout)["error"]);
        }
    }
}
