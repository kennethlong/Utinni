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
    /// Plan 13-04 Task 1: compile-template (AUTH-03) error-path coverage.
    ///
    /// <para><b>Gate-finding (success golden):</b> a compilable <c>.tpf</c> requires a <b>registered</b>
    /// template class (a compiled-in <c>Shared*ObjectTemplate</c>) whose <c>id</c> tag matches a
    /// canonical SOE <c>.tdf</c>, with every non-pure-virtual param supplied (TemplateCompiler errors
    /// "Unable to create template class. May not be installed." on a synthetic class). No canonical
    /// SOE <c>.tpf</c>/<c>.tdf</c> assets exist in either repo (the documented Phase-12 gate-finding),
    /// so the byte-correct <c>.iff</c> golden is deferred until a real <c>.tpf</c>+<c>.tdf</c> pair is
    /// supplied — at which point the existing golden harness retires it. The verb + the subprocess
    /// seam's error mapping ship here; the seam's success-envelope shape is proven by the 13-04
    /// build-tre golden (which DOES compile a real native).</para>
    /// </summary>
    public sealed class CompileTemplateGoldenTests : IDisposable
    {
        private readonly string _work;

        public CompileTemplateGoldenTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "compiletpl_" + Guid.NewGuid().ToString("N"));
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

        [Fact]
        public void CompileTemplate_MissingInput_ExitThree()
        {
            string missing = Path.Combine(_work, "nope.tpf");
            JObject env = Run(out int exit, "compile-template", missing);
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void CompileTemplate_MissingExe_ExitThree()
        {
            // A real input file, but a bogus --tool-path → the subprocess seam's File.Exists(exe)
            // guard returns exit 3 without spawning anything.
            string tpf = Path.Combine(_work, "dummy.tpf");
            File.WriteAllText(tpf, "@class whatever 0\n");
            string bogusExe = Path.Combine(_work, "no_such_TemplateCompiler.exe");

            JObject env = Run(out int exit, "compile-template", tpf, "--tool-path", bogusExe);
            Assert.Equal(3, exit);
            JObject error = (JObject)env["error"];
            Assert.Equal("FileNotFound", (string)error["kind"]);
            Assert.Contains("TemplateCompiler", (string)error["message"]);
        }
    }
}
