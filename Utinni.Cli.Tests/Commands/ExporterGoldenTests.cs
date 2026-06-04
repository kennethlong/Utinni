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
    /// Plan 13-05 Task 3: export-armor / export-weapon (AUTH-06) — the T-13-03 input-path injection
    /// guard and the subprocess error mapping.
    ///
    /// <para><b>Gate-finding (full-chain golden):</b> each exporter reads a datatable .iff, emits a
    /// .tpf, and chains <c>system("TemplateCompiler -compile")</c> — which requires a registered
    /// compiled-in template class + a canonical SOE .tdf (the same Phase-12 gate-finding compile-template
    /// hit) plus a populated <c>tools.cfg</c>. No such canonical assets exist, so the end-to-end
    /// produced-.tpf/.iff golden is deferred. The verbs, the injection guard, and the Perforce-stub
    /// (no "Cannot access Perforce" FATAL — Plan 13-01) are what ship + are tested here.</para>
    /// </summary>
    public sealed class ExporterGoldenTests : IDisposable
    {
        private readonly string _work;

        public ExporterGoldenTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "exporter_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort */ }
        }

        private JObject Run(out int exit, params string[] args)
        {
            var r = InProcessCliRunner.Run(args);
            exit = r.ExitCode;
            return JObject.Parse(r.Stdout);
        }

        [Theory]
        [InlineData("export-armor")]
        [InlineData("export-weapon")]
        public void Export_ShellMetaInputPath_RejectedBeforeInvocation(string verb)
        {
            // T-13-03: a shell-meta input path must be rejected BEFORE the exporter's internal
            // system("TemplateCompiler "+path) shell-out can be reached.
            JObject env = Run(out int exit, verb, "evil.iff & calc.exe", Path.Combine(_work, "out"));
            Assert.Equal(2, exit);
            Assert.Equal("UnsafeInputPath", (string)((JObject)env["error"])["kind"]);
        }

        [Theory]
        [InlineData("export-armor")]
        [InlineData("export-weapon")]
        public void Export_ParentEscapeInputPath_Rejected(string verb)
        {
            JObject env = Run(out int exit, verb, "../../escape.iff", Path.Combine(_work, "out"));
            Assert.Equal(2, exit);
            Assert.Equal("UnsafeInputPath", (string)((JObject)env["error"])["kind"]);
        }

        [Theory]
        [InlineData("export-armor", "ArmorExporterTool")]
        [InlineData("export-weapon", "CoreWeaponExporterTool")]
        public void Export_MissingExe_ExitThree(string verb, string toolName)
        {
            // A clean input path that exists, but a bogus tool-path → the seam's File.Exists(exe) → 3.
            string iff = Path.Combine(_work, "datatable.iff");
            File.WriteAllBytes(iff, new byte[] { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0, 0, 0, 0 });
            JObject env = Run(out int exit, verb, iff, Path.Combine(_work, "out"),
                "--tool-path", Path.Combine(_work, "no_such_" + toolName + ".exe"));
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }
    }
}
