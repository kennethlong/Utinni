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
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using Utinni.Cli.Tests.Infrastructure;
using UtinniCoreDotNet.Formats.Tre;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Plan 13-04 Task 2: build-tre (AUTH-04) — usage/error paths plus the D-06 byte-exact ladder
    /// (synthesize a .rsp from a real .tre, rebuild via TreeFileBuilder, assert determinism).
    /// </summary>
    public sealed class BuildTreGoldenTests : IDisposable
    {
        private readonly string _work;

        public BuildTreGoldenTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "buildtre_" + Guid.NewGuid().ToString("N"));
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

        // Walk up from the test bin dir to the repo root and resolve the Phase-12 native exe
        // (Debug "_d" suffix). Returns null if the tools solution has not been built (a local
        // partial run); CI builds it in the AUTH-01 gate step before the CLI tests.
        private static string FindTreeFileBuilder()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName,
                    "tools", "src", "compile", "win32", "TreeFileBuilder", "Debug", "TreeFileBuilder_d.exe");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        private static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(fs));
            }
        }

        [Fact]
        public void BuildTre_NeitherSource_UsageError()
        {
            JObject env = Run(out int exit, "build-tre", Path.Combine(_work, "out.tre"));
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void BuildTre_MissingFromTre_ExitThree()
        {
            JObject env = Run(out int exit, "build-tre", Path.Combine(_work, "out.tre"),
                "--from-tre", Path.Combine(_work, "nope.tre"));
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void BuildTre_MissingExe_ExitThree()
        {
            // Valid synth source, bogus tool path → the seam's File.Exists(exe) guard → exit 3.
            string src = Path.Combine(_work, "src.tre");
            TreFixtureBuilder.WriteSynthetic5000(src);
            JObject env = Run(out int exit, "build-tre", Path.Combine(_work, "out.tre"),
                "--from-tre", src, "--tool-path", Path.Combine(_work, "no_such_TreeFileBuilder.exe"));
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void BuildTre_UncompressedSynthRsp_ByteExact()
        {
            string exe = FindTreeFileBuilder();
            if (exe == null)
            {
                // Tools solution not built in this checkout (local partial run). The full D-06 ladder
                // runs in CI, where the AUTH-01 gate builds the natives before the CLI tests.
                Console.WriteLine("BuildTre_UncompressedSynthRsp_ByteExact: TreeFileBuilder_d.exe not found — skipped (build tools/Utinni.Tools.sln to enable).");
                return;
            }

            // A real uncompressed-record .tre source (compressor 0 → @u, no zlib variance).
            string src = Path.Combine(_work, "src.tre");
            TreFixtureBuilder.WriteSynthetic5000(src);

            string a = Path.Combine(_work, "a.tre");
            string b = Path.Combine(_work, "b.tre");

            JObject envA = Run(out int exitA, "build-tre", a, "--from-tre", src, "--tool-path", exe);
            Assert.Equal(0, exitA);
            Assert.True((bool)((JObject)envA["result"])["produced"]);

            JObject envB = Run(out int exitB, "build-tre", b, "--from-tre", src, "--tool-path", exe);
            Assert.Equal(0, exitB);

            // D-06: synth-.rsp → TreeFileBuilder is byte-exact deterministic for the uncompressed case.
            Assert.Equal(Sha256(a), Sha256(b));

            // The rebuilt archive is non-empty and re-parses to the original record set (not a silent
            // 36-byte header-only build — the tree-first .rsp format regression guard).
            using (var ms = new MemoryStream(File.ReadAllBytes(a), writable: false))
            {
                TreFile rebuilt = TreFile.Open(ms);
                Assert.Equal(2, rebuilt.Records.Count);
            }
            Assert.True(new FileInfo(a).Length > 36, "rebuilt .tre must contain packed records, not just a header");
        }
    }
}
