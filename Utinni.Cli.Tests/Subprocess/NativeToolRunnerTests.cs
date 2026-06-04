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
using Utinni.Cli.Commands.Subprocess;
using Xunit;

namespace Utinni.Cli.Tests.Subprocess
{
    /// <summary>
    /// Plan 13-02 Task 1: exercises the subprocess seam's exit-code mapping (0→0, non-zero→2,
    /// missing-exe→3), the locked envelope shape, and NormalizeBanner. Drives <c>cmd.exe</c>
    /// (always present via %ComSpec%) for deterministic exit codes / output-file creation.
    /// </summary>
    public sealed class NativeToolRunnerTests
    {
        private static readonly object ConsoleLock = new object();

        // The native ProcessStartInfo.Arguments string is what cmd re-parses; using cmd's own
        // /c switch lets us script exact exit codes and stderr without an extra fixture exe.
        private static string Cmd
        {
            get { return Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe"; }
        }

        private static JObject RunCapturing(out int exitCode, string toolExe, string[] args, string workingDir, string verb, string toolName, string expectedOutputPath)
        {
            lock (ConsoleLock)
            {
                var original = Console.Out;
                var sw = new StringWriter();
                Console.SetOut(sw);
                try
                {
                    exitCode = NativeToolRunner.Run(toolExe, args, workingDir, verb, toolName, expectedOutputPath);
                }
                finally
                {
                    Console.SetOut(original);
                }
                return JObject.Parse(sw.ToString());
            }
        }

        [Fact]
        public void Run_NativeExitZero_MapsToExitZero_WithEnvelopeKeys()
        {
            string outFile = Path.Combine(Path.GetTempPath(), "ntr_ok_" + Guid.NewGuid().ToString("N") + ".out");
            try
            {
                int exit;
                // `cmd /c break > file` creates an empty file and exits 0.
                JObject env = RunCapturing(out exit, Cmd,
                    new[] { "/c", "break", ">", outFile },
                    null, "compile-template", "TemplateCompiler", outFile);

                Assert.Equal(0, exit);
                JObject result = (JObject)env["result"];
                Assert.NotNull(result);
                // Locked envelope keys.
                Assert.True(result.ContainsKey("tool"));
                Assert.True(result.ContainsKey("exitCode"));
                Assert.True(result.ContainsKey("outputPath"));
                Assert.True(result.ContainsKey("produced"));
                Assert.True(result.ContainsKey("stderr"));
                Assert.Equal("TemplateCompiler", (string)result["tool"]);
                Assert.Equal(0, (int)result["exitCode"]);
                Assert.True((bool)result["produced"]); // the output file exists
            }
            finally
            {
                if (File.Exists(outFile)) File.Delete(outFile);
            }
        }

        [Fact]
        public void Run_NativeNonZeroExit_MapsToExitTwo_ToolError()
        {
            int exit;
            JObject env = RunCapturing(out exit, Cmd,
                new[] { "/c", "exit", "7" },
                null, "compile-template", "TemplateCompiler", null);

            Assert.Equal(2, exit);
            JObject error = (JObject)env["error"];
            Assert.NotNull(error);
            Assert.Equal("ToolError", (string)error["kind"]);
        }

        [Fact]
        public void Run_MissingExe_MapsToExitThree_WithoutThrowing()
        {
            string missing = Path.Combine(Path.GetTempPath(), "does_not_exist_" + Guid.NewGuid().ToString("N") + ".exe");

            int exit;
            JObject env = RunCapturing(out exit, missing,
                new[] { "-compile", "whatever.tpf" },
                null, "compile-template", "TemplateCompiler", null);

            Assert.Equal(3, exit);
            JObject error = (JObject)env["error"];
            Assert.NotNull(error);
            Assert.Equal("FileNotFound", (string)error["kind"]);
        }

        [Fact]
        public void Run_ProducedFalse_WhenExpectedOutputMissing()
        {
            string outFile = Path.Combine(Path.GetTempPath(), "ntr_missing_" + Guid.NewGuid().ToString("N") + ".out");
            // cmd exits 0 but never creates the file → produced=false.
            int exit;
            JObject env = RunCapturing(out exit, Cmd,
                new[] { "/c", "break" },
                null, "build-tre", "TreeFileBuilder", outFile);

            Assert.Equal(0, exit);
            JObject result = (JObject)env["result"];
            Assert.False((bool)result["produced"]);
        }

        [Fact]
        public void NormalizeBanner_StripsDateTimeAndAbsPath()
        {
            string raw = "TemplateCompiler  Built Jun  4 2026 12:34:56\nOutput: C:\\Users\\maintainer\\proj\\out.iff done";
            string normalized = NativeToolRunner.NormalizeBanner(raw);

            Assert.DoesNotContain("2026", normalized);
            Assert.DoesNotContain("12:34:56", normalized);
            Assert.DoesNotContain("C:\\Users", normalized);
            Assert.Contains("<DATE>", normalized);
            Assert.Contains("<TIME>", normalized);
            Assert.Contains("<PATH>", normalized);
            // The trailing non-path word survives (the abs-path regex stops at whitespace).
            Assert.Contains("done", normalized);
        }

        [Fact]
        public void NormalizeBanner_IsIdempotent()
        {
            string raw = "Built Dec 25 2025 09:08:07 at D:\\Code\\Utinni\\tools\\x.exe";
            string once = NativeToolRunner.NormalizeBanner(raw);
            string twice = NativeToolRunner.NormalizeBanner(once);
            Assert.Equal(once, twice);
        }

        [Fact]
        public void NormalizeBanner_NullOrEmpty_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, NativeToolRunner.NormalizeBanner(null));
            Assert.Equal(string.Empty, NativeToolRunner.NormalizeBanner(string.Empty));
        }
    }
}
