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
    /// Goldens for the <c>decode-iff</c> PEFT auto-dispatch branch (15-04 Task 1) and the
    /// <c>Program.Dispatch</c> wiring for the new <c>roundtrip-particle</c> verb. Proves a FORM PEFT
    /// root is routed into the 15-02 particle codec (emitting PEFT + emitter-group / emitter counts)
    /// and that the new option type is reachable through dispatch (the case fires).
    /// </summary>
    public class DecodeParticleDispatchTests
    {
        private sealed class TempPrt : IDisposable
        {
            public string Path { get; }

            public TempPrt(byte[] bytes)
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "utinni-decode-particle-" + Guid.NewGuid().ToString("N") + ".prt");
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

        // ── decode-iff auto-dispatches FORM PEFT into the particle codec ──────

        [Fact]
        public void DecodeIff_PeftRoot_AutoDispatchesToParticleCodec_ReportsCounts()
        {
            using (var f = new TempPrt(ParticleCliFixtures.BuildMinimalPeft("0011")))
            {
                JToken env = RunAndParse(out int exit, "decode-iff", f.Path);
                Assert.Equal(0, exit);
                JToken r = env["result"];
                Assert.Equal("particle", (string)r["type"]);
                Assert.Equal("PEFT", (string)r["rootType"]);
                Assert.Equal("0002", (string)r["version"]);
                Assert.Equal(1, (int)r["emitterGroupCount"]);
                Assert.Equal(1, (int)r["emitterCount"]);
                Assert.False((bool)r["rawPreserved"]);
            }
        }

        [Fact]
        public void DecodeIff_DegradedPeftRoot_AutoDispatches_RawPreserved()
        {
            using (var f = new TempPrt(ParticleCliFixtures.BuildPeftWithRootVersion("9999")))
            {
                JToken env = RunAndParse(out int exit, "decode-iff", f.Path);
                Assert.Equal(0, exit);
                JToken r = env["result"];
                Assert.Equal("particle", (string)r["type"]);
                Assert.True((bool)r["rawPreserved"]);
                Assert.Equal("9999", (string)r["version"]);
            }
        }

        // ── Program.Dispatch wiring: the new verb case fires (not the default → exit 1) ──

        [Fact]
        public void Dispatch_RoundtripParticleOptions_RoutesToCommand_NotDefault()
        {
            // If the Dispatch switch lacked a RoundtripParticleOptions case, the verb would parse but the
            // default arm would return 1 with NO JSON envelope. A clean fixture reaching the command
            // emits the success envelope (exit 0), proving the case fires through Program.Dispatch.
            using (var f = new TempPrt(ParticleCliFixtures.BuildMinimalPeft("0011")))
            {
                JToken env = RunAndParse(out int exit, "roundtrip-particle", f.Path);
                Assert.Equal(0, exit);
                Assert.Equal("roundtrip-particle", (string)env["command"]);
                Assert.NotNull(env["result"]);
            }
        }
    }
}
