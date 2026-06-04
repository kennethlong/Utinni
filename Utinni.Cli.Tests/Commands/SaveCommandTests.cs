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
    /// Plan 13-03 Task 1: the AUTH-05 save verb — 4-format re-serialize, the locked envelope, the
    /// loose-override path-containment gate, and the .tre→repack-tre redirect (D-10).
    /// </summary>
    public sealed class SaveCommandTests : IDisposable
    {
        private readonly string _work;

        public SaveCommandTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "save_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort */ }
        }

        // ── fixtures ──────────────────────────────────────────────────────────

        // Minimal object-template .iff: FORM SHOT > FORM 0000 > PCNT(int32 0). Decodes as OT.
        private static byte[] OtBytes()
        {
            return IffBuilder.Form("SHOT",
                IffBuilder.Form("0000",
                    IffBuilder.Leaf("PCNT", IffBuilder.Int32Le(0))));
        }

        // Plain non-OT, non-DTII IFF.
        private static byte[] PlainIffBytes()
        {
            return IffBuilder.Form("TEST",
                IffBuilder.Leaf("DATA", new byte[] { 1, 2, 3, 4 }));
        }

        private string WriteFixture(string name, byte[] bytes)
        {
            string p = Path.Combine(_work, name);
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllBytes(p, bytes);
            return p;
        }

        private static JObject Result(JObject env) => (JObject)env["result"];

        private JObject RunSave(out int exit, params string[] args)
        {
            var r = InProcessCliRunner.Run(args);
            exit = r.ExitCode;
            return JObject.Parse(r.Stdout);
        }

        private void AssertLockedEnvelope(JObject result)
        {
            Assert.True(result.ContainsKey("written"));
            Assert.True(result.ContainsKey("path"));
            Assert.True(result.ContainsKey("bytesWritten"));
            Assert.True(result.ContainsKey("backupPath"));
            Assert.True(result.ContainsKey("validated"));
        }

        // ── 4-format save (explicit --path) ─────────────────────────────────────

        [Theory]
        [InlineData("datatable")]
        [InlineData("stf")]
        [InlineData("ot")]
        [InlineData("iff")]
        public void Save_EachFormat_WritesValidated_LockedEnvelope(string fmt)
        {
            byte[] bytes;
            string srcName;
            switch (fmt)
            {
                case "datatable": bytes = DataTableFixtureBuilder.BuildV1Minimal(); srcName = "src.iff"; break;
                case "stf": bytes = StringTableFixtureBuilder.BuildV1Minimal(); srcName = "src.stf"; break;
                case "ot": bytes = OtBytes(); srcName = "src_ot.iff"; break;
                default: bytes = PlainIffBytes(); srcName = "src_plain.iff"; break;
            }
            string src = WriteFixture(srcName, bytes);
            string dest = Path.Combine(_work, "out_" + fmt + ".bin");

            JObject env = RunSave(out int exit, "save", src, "--path", dest);

            Assert.Equal(0, exit);
            JObject result = Result(env);
            AssertLockedEnvelope(result);
            Assert.True((bool)result["written"]);
            Assert.True((bool)result["validated"], fmt + " must re-parse cleanly");
            Assert.True((int)result["bytesWritten"] > 0);
            Assert.True(File.Exists(dest));
            Assert.Equal((int)result["bytesWritten"], File.ReadAllBytes(dest).Length);
            Assert.Equal(JTokenType.Null, result["backupPath"].Type); // save never backs up (D-10)
        }

        // ── loose-override (default, --root) ────────────────────────────────────

        [Fact]
        public void Save_LooseOverride_ReserializesInPlace_UnderRoot()
        {
            string root = Path.Combine(_work, "client");
            const string rel = "datatables/foo.iff";
            string asset = Path.Combine(root, "datatables", "foo.iff");
            Directory.CreateDirectory(Path.GetDirectoryName(asset));
            File.WriteAllBytes(asset, DataTableFixtureBuilder.BuildV1Minimal());

            JObject env = RunSave(out int exit, "save", rel, "--root", root);

            Assert.Equal(0, exit);
            JObject result = Result(env);
            Assert.True((bool)result["validated"]);
            // Destination is the resolved loose path under the root.
            Assert.Equal(Path.GetFullPath(asset), (string)result["path"]);
            Assert.True(File.Exists(asset));
        }

        [Fact]
        public void Save_LooseOverride_TraversalEscape_RejectedExitTwo()
        {
            string root = Path.Combine(_work, "client2");
            Directory.CreateDirectory(root);

            JObject env = RunSave(out int exit, "save", "../escape.iff", "--root", root);

            Assert.Equal(2, exit);
            Assert.Equal("PathContainment", (string)((JObject)env["error"])["kind"]);
        }

        // ── mutual exclusion / usage ────────────────────────────────────────────

        [Fact]
        public void Save_NeitherPathNorRoot_UsageError()
        {
            string src = WriteFixture("lonely.stf", StringTableFixtureBuilder.BuildV1Minimal());
            JObject env = RunSave(out int exit, "save", src);
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
        }

        // ── .tre redirect (D-10) ────────────────────────────────────────────────

        [Fact]
        public void Save_TreInput_RoutesToRepackTre_UsageError()
        {
            string tre = Path.Combine(_work, "archive.tre");
            TreFixtureBuilder.WriteSynthetic5000(tre);
            string dest = Path.Combine(_work, "out.tre");

            JObject env = RunSave(out int exit, "save", tre, "--path", dest);

            Assert.Equal(1, exit);
            JObject error = (JObject)env["error"];
            Assert.Equal("UsageError", (string)error["kind"]);
            Assert.Contains("repack-tre", (string)error["message"]);
        }

        [Fact]
        public void Save_MissingInput_ExitThree()
        {
            string missing = Path.Combine(_work, "nope.iff");
            JObject env = RunSave(out int exit, "save", missing, "--path", Path.Combine(_work, "x.iff"));
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }
    }
}
