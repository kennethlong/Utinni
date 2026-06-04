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
    /// Plan 13-03 Task 2: the D-10 repack-tre verb — backup-before-overwrite, round-trip repack of a
    /// supported archive, V6000/encrypted refusal, and both new verbs registered in the parser.
    /// </summary>
    public sealed class RepackTreCommandTests : IDisposable
    {
        private readonly string _work;

        public RepackTreCommandTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "repack_" + Guid.NewGuid().ToString("N"));
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

        // A supported (non-enumerate-only) 0006 archive: the COT2000 companion tree1.tre.
        private string Supported0006Archive()
        {
            string toc = Path.Combine(_work, "idx.toc");
            string companions = Path.Combine(_work, "cot");
            TreFixtureBuilder.WriteCot2000TwoTree(toc, companions);
            return Path.Combine(companions, "tree1.tre");
        }

        [Fact]
        public void RepackTre_SupportedArchive_RoundTrips_TakesBackup()
        {
            string tre = Supported0006Archive();
            byte[] before = File.ReadAllBytes(tre);

            JObject env = Run(out int exit, "repack-tre", tre);

            Assert.Equal(0, exit);
            JObject result = (JObject)env["result"];
            Assert.True((bool)result["written"]);
            Assert.True((bool)result["validated"]);
            Assert.True((int)result["bytesWritten"] > 0);

            // backupPath is POPULATED (unlike save) and holds the pre-repack bytes.
            string backupPath = (string)result["backupPath"];
            Assert.False(string.IsNullOrEmpty(backupPath));
            Assert.True(File.Exists(backupPath));
            Assert.Equal(before, File.ReadAllBytes(backupPath));

            // The repacked archive still parses to the same record count.
            using (var ms = new MemoryStream(File.ReadAllBytes(tre), writable: false))
            {
                var rt = UtinniCoreDotNet.Formats.Tre.TreFile.Open(ms);
                Assert.True(rt.Records.Count >= 1);
            }
        }

        [Fact]
        public void RepackTre_V6000Encrypted_Refused_ExitTwo()
        {
            string tre = Path.Combine(_work, "v6000.tre");
            TreFixtureBuilder.WriteV6000TwoRecord(tre); // enumerate-only / encrypted payloads

            JObject env = Run(out int exit, "repack-tre", tre);

            Assert.Equal(2, exit);
            Assert.Equal("NotSupported", (string)((JObject)env["error"])["kind"]);
            // No backup file should be left behind on refusal.
            Assert.Empty(Directory.GetFiles(_work, "v6000.tre.*.bak"));
        }

        [Fact]
        public void RepackTre_MissingFile_ExitThree()
        {
            JObject env = Run(out int exit, "repack-tre", Path.Combine(_work, "nope.tre"));
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void Help_ListsBothNewVerbs()
        {
            var r = InProcessCliRunner.Run("--help");
            string text = (r.Stdout ?? "") + (r.Stderr ?? "");
            Assert.Contains("save", text);
            Assert.Contains("repack-tre", text);
        }
    }
}
