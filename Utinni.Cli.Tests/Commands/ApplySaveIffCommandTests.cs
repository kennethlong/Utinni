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
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Commands;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Plan 14-03a Task 2: golden tests for the <c>apply-save-iff</c> verb — the generic-IFF member of
    /// the apply-save-* family. Proves READ-BACK persistence (re-parse the written file + assert the
    /// leaf payload equals the hex bytes; untouched leaves byte-identical; file hash changed), removal
    /// persistence, and failed-verify-no-write (exit 2, on-disk byte-unchanged).
    /// </summary>
    public sealed class ApplySaveIffCommandTests : IDisposable
    {
        private readonly string _work;

        public ApplySaveIffCommandTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "applysaveiff_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            ApplySaveIffCommand.TestPerturbSerialized = null;
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort */ }
        }

        // A plain IFF with two leaves under one FORM: DATA(4 bytes) + INFO(2 bytes).
        private static byte[] PlainIff()
        {
            return IffBuilder.Form("TEST",
                IffBuilder.Leaf("DATA", new byte[] { 1, 2, 3, 4 }),
                IffBuilder.Leaf("INFO", new byte[] { 9, 9 }));
        }

        private string WriteLooseAsset(string rel, byte[] bytes)
        {
            string p = Path.Combine(_work, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllBytes(p, bytes);
            return p;
        }

        private static byte[] Sha(byte[] b)
        {
            using (var sha = SHA256.Create()) return sha.ComputeHash(b);
        }

        private static JObject RunApply(out int exit, params string[] args)
        {
            CliResult r = InProcessCliRunner.Run(args);
            exit = r.ExitCode;
            return JObject.Parse(r.Stdout);
        }

        private static IffDocument Parse(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes, writable: false)) return IffReader.Read(ms);
        }

        // Derive the stable id of the first DATA leaf (the layout is deterministic).
        private static string FirstDataLeafId(byte[] bytes)
        {
            IffDocument doc = Parse(bytes);
            return doc.AllNodesInPreorder.OfType<IffLeafChunk>().First(l => l.TypeId == "DATA").Id;
        }

        private static string FirstInfoLeafId(byte[] bytes)
        {
            IffDocument doc = Parse(bytes);
            return doc.AllNodesInPreorder.OfType<IffLeafChunk>().First(l => l.TypeId == "INFO").Id;
        }

        // ── happy: mutate-leaf persists the payload (READ-BACK + hash changed) ───

        [Fact]
        public void ApplySaveIff_MutateLeaf_PersistsPayload_ReadBackReflectsHex_HashChanged()
        {
            const string rel = "iff/test.iff";
            byte[] original = PlainIff();
            string asset = WriteLooseAsset(rel, original);
            byte[] before = File.ReadAllBytes(asset);
            string dataId = FirstDataLeafId(before);

            JObject env = RunApply(out int exit, "apply-save-iff", rel, "--root", _work, "--mutate-leaf", dataId, "--mutate-hex", "DEADBEEF");

            Assert.Equal(0, exit);
            JObject result = (JObject)env["result"];
            Assert.True((bool)result["written"]);
            Assert.True((bool)result["bytesEqualUntouched"]);
            Assert.Equal("mutate-leaf(" + dataId + ")", (string)result["mutationApplied"]);

            byte[] after = File.ReadAllBytes(asset);
            IffDocument reparsed = Parse(after);
            IffLeafChunk dataLeaf = reparsed.AllNodesInPreorder.OfType<IffLeafChunk>().First(l => l.TypeId == "DATA");
            Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, dataLeaf.Data);
            // The untouched INFO leaf is preserved.
            IffLeafChunk infoLeaf = reparsed.AllNodesInPreorder.OfType<IffLeafChunk>().First(l => l.TypeId == "INFO");
            Assert.Equal(new byte[] { 9, 9 }, infoLeaf.Data);
            Assert.False(Sha(before).SequenceEqual(Sha(after)));
        }

        // ── happy: remove-leaf persists the removal ─────────────────────────────

        [Fact]
        public void ApplySaveIff_RemoveLeaf_PersistsRemoval_ReadBackLeafGone()
        {
            const string rel = "iff/test.iff";
            byte[] original = PlainIff();
            string asset = WriteLooseAsset(rel, original);
            string infoId = FirstInfoLeafId(original);

            JObject env = RunApply(out int exit, "apply-save-iff", rel, "--root", _work, "--remove-leaf", infoId);

            Assert.Equal(0, exit);
            Assert.True((bool)((JObject)env["result"])["written"]);

            IffDocument reparsed = Parse(File.ReadAllBytes(asset));
            Assert.DoesNotContain(reparsed.AllNodesInPreorder.OfType<IffLeafChunk>(), l => l.TypeId == "INFO");
            Assert.Contains(reparsed.AllNodesInPreorder.OfType<IffLeafChunk>(), l => l.TypeId == "DATA");
        }

        // ── failed-verify-no-write ──────────────────────────────────────────────

        [Fact]
        public void ApplySaveIff_FailedUntouchedVerify_ExitsTwo_AndFileIsByteUnchanged()
        {
            const string rel = "iff/test.iff";
            byte[] original = PlainIff();
            string asset = WriteLooseAsset(rel, original);
            byte[] before = File.ReadAllBytes(asset);
            string dataId = FirstDataLeafId(before);

            // Perturb the UNTOUCHED INFO leaf on top of the verb's DATA-leaf edit.
            ApplySaveIffCommand.TestPerturbSerialized = bytes =>
            {
                using (var ms = new MemoryStream(bytes, writable: false))
                {
                    var doc = IffReader.Read(ms);
                    var mutable = MutableIffDocument.FromDocument(doc, bytes);
                    string infoId = doc.AllNodesInPreorder.OfType<IffLeafChunk>().First(l => l.TypeId == "INFO").Id;
                    // Find the mutable INFO leaf and perturb it.
                    var leaf = mutable.Root.Children.First(c => c.Kind == MutableIffNodeKind.Leaf && c.TypeId == "INFO");
                    leaf.SetPayload(new byte[] { 7, 7 });
                    return IffWriter.Write(mutable);
                }
            };

            JObject env = RunApply(out int exit, "apply-save-iff", rel, "--root", _work, "--mutate-leaf", dataId, "--mutate-hex", "DEADBEEF");

            Assert.Equal(2, exit);
            Assert.Equal("VerifyFailed", (string)((JObject)env["error"])["kind"]);

            byte[] after = File.ReadAllBytes(asset);
            Assert.True(before.SequenceEqual(after), "a failed verify must leave the file byte-unchanged");
        }

        // ── usage / containment / not-found ─────────────────────────────────────

        [Fact]
        public void ApplySaveIff_NoMutation_ExitsOne()
        {
            const string rel = "iff/x.iff";
            WriteLooseAsset(rel, PlainIff());
            JObject env = RunApply(out int exit, "apply-save-iff", rel, "--root", _work);
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveIff_TraversalEscape_ExitsTwo()
        {
            JObject env = RunApply(out int exit, "apply-save-iff", "../escape.iff", "--root", _work, "--remove-leaf", "FORM:TEST/DATA:DATA/0");
            Assert.Equal(2, exit);
            Assert.Equal("PathContainment", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveIff_MissingFile_ExitsThree()
        {
            JObject env = RunApply(out int exit, "apply-save-iff", "iff/nope.iff", "--root", _work, "--remove-leaf", "x");
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }
    }
}
