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
    /// Plan 13-05 Task 2: compile-datatable (AUTH-06) — usage/error paths plus the D-03 cross-check:
    /// the native DataTableTool compiles a tab-delimited spreadsheet to a .iff that the managed reader
    /// (decode-iff) decodes typed-correct (native is SOE-authoritative, D-04).
    /// </summary>
    public sealed class CompileDatatableGoldenTests : IDisposable
    {
        private readonly string _work;

        // Minimal tab-delimited spreadsheet: name row / type row (i/s/f) / 2 data rows.
        private const string MiniTab = "id\tname\tvalue\r\ni\ts\tf\r\n1\talpha\t1.5\r\n2\tbeta\t2.5\r\n";

        public CompileDatatableGoldenTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "compiledt_" + Guid.NewGuid().ToString("N"));
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
        public void CompileDatatable_MissingInput_ExitThree()
        {
            JObject env = Run(out int exit, "compile-datatable", Path.Combine(_work, "nope.tab"), Path.Combine(_work, "o.iff"));
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void CompileDatatable_MissingExe_ExitThree()
        {
            string tab = Path.Combine(_work, "mini.tab");
            File.WriteAllText(tab, MiniTab);
            JObject env = Run(out int exit, "compile-datatable", tab, Path.Combine(_work, "o.iff"),
                "--tool-path", Path.Combine(_work, "no_such_DataTableTool.exe"));
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void CompileDatatable_Native_ProducesIff_ManagedReaderDecodesTypedCorrect()
        {
            string exe = NativeToolLocator.Find("DataTableTool");
            if (exe == null)
            {
                Console.WriteLine("CompileDatatable native cross-check skipped — DataTableTool_d.exe not found (build tools/Utinni.Tools.sln).");
                return;
            }

            string tab = Path.Combine(_work, "mini.tab");
            File.WriteAllText(tab, MiniTab);
            string iff = Path.Combine(_work, "mini.iff");

            JObject env = Run(out int exit, "compile-datatable", tab, iff, "--tool-path", exe);
            Assert.Equal(0, exit);
            Assert.True((bool)((JObject)env["result"])["produced"]);
            Assert.True(File.Exists(iff));

            // D-03 cross-check: the native .iff decodes typed-correct through the managed reader.
            JObject decoded = Run(out int decExit, "decode-iff", iff);
            Assert.Equal(0, decExit);
            JObject result = (JObject)decoded["result"];
            var columns = (JArray)result["columns"];
            Assert.Equal(3, columns.Count);
            Assert.Equal("id", (string)columns[0]["name"]);
            Assert.Equal("Int", (string)columns[0]["kind"]);
            Assert.Equal("String", (string)columns[1]["kind"]);
            Assert.Equal("Float", (string)columns[2]["kind"]);
            Assert.Equal(2, (int)result["rowCount"]);
        }
    }
}
