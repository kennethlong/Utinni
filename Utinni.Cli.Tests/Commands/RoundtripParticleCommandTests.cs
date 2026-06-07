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
using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Tier-2 goldens for the <c>roundtrip-particle</c> CLI command — the byte-exact max-harness for the
    /// particle-effect (<c>.prt</c> / FORM PEFT) write path (the typed-<c>.prt</c> analog of
    /// <c>roundtrip-ot</c> / <c>roundtrip-tab</c>). Proves a clean fixture AND a degraded
    /// (unknown-EMTR-version, raw-preserved) fixture both re-serialize byte-identical, and the
    /// 0/1/2/3 exit-code taxonomy holds. Fixtures are synthesized THROUGH the framework IFF primitives
    /// (no real <c>.prt</c> fixtures exist today; extract-from-<c>.tre</c> is the documented alternate).
    ///
    /// <para>Naming convention: [Method]_[Scenario]_[ExpectedOutcome].</para>
    /// </summary>
    public class RoundtripParticleCommandTests
    {
        private sealed class TempPrt : IDisposable
        {
            public string Path { get; }

            public TempPrt(byte[] bytes)
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "utinni-roundtrip-particle-" + Guid.NewGuid().ToString("N") + ".prt");
                File.WriteAllBytes(Path, bytes);
            }

            public void Dispose()
            {
                try { if (File.Exists(Path)) File.Delete(Path); }
                catch (IOException) { /* best-effort */ }
            }
        }

        private static JToken RunAndParse(out int exitCode, params string[] args)
        {
            CliResult result = InProcessCliRunner.Run(args);
            exitCode = result.ExitCode;
            return JToken.Parse(result.Stdout);
        }

        private static JToken ResultOf(JToken envelope) => envelope["result"];

        // ── byte-exact: a clean, recognized-version effect round-trips identical ──

        [Fact]
        public void RoundtripParticle_CleanFixture_ExitsZero_ByteIdentical()
        {
            using (var f = new TempPrt(ParticleCliFixtures.BuildMinimalPeft("0011")))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-particle", f.Path);
                Assert.Equal(0, exit);
                JToken r = ResultOf(env);
                Assert.True((bool)r["bytesIdentical"]);
                Assert.False((bool)r["rawPreserved"]);
                Assert.Equal("PEFT", (string)r["rootType"]);
                Assert.Equal("0002", (string)r["version"]);
                Assert.Equal(1, (int)r["emitterGroupCount"]);
                Assert.Equal(1, (int)r["emitterCount"]);
            }
        }

        // ── byte-exact: a degraded (unknown-root-version, raw-preserved) effect still round-trips ──

        [Fact]
        public void RoundtripParticle_DegradedRootVersionFixture_ExitsZero_ByteIdentical()
        {
            using (var f = new TempPrt(ParticleCliFixtures.BuildPeftWithRootVersion("9999")))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-particle", f.Path);
                Assert.Equal(0, exit);
                JToken r = ResultOf(env);
                Assert.True((bool)r["bytesIdentical"]);  // raw-preserve held through the verb
                Assert.True((bool)r["rawPreserved"]);
                Assert.Equal("9999", (string)r["version"]);
            }
        }

        // ── byte-exact: a degraded (truncated WVFM leaf inside a recognized emitter) round-trips ──

        [Fact]
        public void RoundtripParticle_TruncatedWaveFormFixture_ExitsZero_ByteIdentical()
        {
            using (var f = new TempPrt(ParticleCliFixtures.BuildPeftWithTruncatedWaveForm()))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-particle", f.Path);
                Assert.Equal(0, exit);
                Assert.True((bool)ResultOf(env)["bytesIdentical"]);
            }
        }

        // ── exit-code taxonomy ────────────────────────────────────────────────

        [Fact]
        public void RoundtripParticle_FileNotFound_ExitsThree()
        {
            JToken env = RunAndParse(out int exit, "roundtrip-particle",
                System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    "does-not-exist-" + Guid.NewGuid().ToString("N") + ".prt"));
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)env["error"]["kind"]);
        }

        [Fact]
        public void RoundtripParticle_MalformedNonIff_ExitsTwo()
        {
            // Not an IFF container at all — the parser rejects it; exit 2 (parse/decoder), never OOB.
            using (var f = new TempPrt(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02, 0x03 }))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-particle", f.Path);
                Assert.Equal(2, exit);
                Assert.NotNull(env["error"]);
            }
        }

        [Fact]
        public void RoundtripParticle_NonPeftRoot_ExitsTwo()
        {
            // A well-formed IFF whose root is NOT FORM PEFT → ParticleParseException (UnexpectedForm) → exit 2.
            using (var f = new TempPrt(ParticleCliFixtures.BuildNonPeftIff()))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-particle", f.Path);
                Assert.Equal(2, exit);
                Assert.Equal("UnexpectedForm", (string)env["error"]["kind"]);
            }
        }

        // ── verb is registered (appears in --help) ───────────────────────────

        [Fact]
        public void RoundtripParticle_VerbIsRegistered_AppearsInHelp()
        {
            CliResult result = InProcessCliRunner.Run("--help");
            Assert.Contains("roundtrip-particle", result.Stdout + result.Stderr);
        }
    }
}
