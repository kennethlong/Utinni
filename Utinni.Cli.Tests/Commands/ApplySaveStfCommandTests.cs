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
using UtinniCoreDotNet.Formats.StringTable;
using Utinni.Cli.Commands;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Plan 14-03a Task 2: golden tests for the <c>apply-save-stf</c> verb — the string-table member of
    /// the apply-save-* family. Proves READ-BACK persistence (re-parse the written file + assert the
    /// entry text == new value, incl. a non-ASCII round-trip; untouched entries byte-identical; file
    /// hash changed), the D-02b sourceCrc-preserved policy, and failed-verify-no-write.
    /// </summary>
    public sealed class ApplySaveStfCommandTests : IDisposable
    {
        private readonly string _work;

        public ApplySaveStfCommandTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "applysavestf_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            ApplySaveStfCommand.TestPerturbSerialized = null;
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort */ }
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

        private static MutableStringTableDocument Parse(byte[] bytes)
        {
            return StringTableDocument.FromBytes(bytes).Mutable;
        }

        private static string TextOf(MutableStringTableDocument doc, string key)
        {
            return doc.Entries.First(e => string.Equals(e.Name, key, StringComparison.Ordinal)).Text;
        }

        // ── happy: edit-text persists; untouched entries byte-identical; crc preserved ──

        [Fact]
        public void ApplySaveStf_EditText_PersistsValue_ReadBack_UntouchedPreserved_HashChanged()
        {
            const string rel = "string/en/ui.stf";
            string asset = WriteLooseAsset(rel, StringTableFixtureBuilder.BuildV1MultiEntry());
            byte[] before = File.ReadAllBytes(asset);

            JObject env = RunApply(out int exit, "apply-save-stf", rel, "--root", _work, "--edit-text", "beta=CHANGED");

            Assert.Equal(0, exit);
            JObject result = (JObject)env["result"];
            Assert.True((bool)result["written"]);
            Assert.True((bool)result["bytesEqualUntouched"]);
            Assert.True((bool)result["sourceCrcPreserved"]);
            Assert.Equal("edit-text(beta)", (string)result["mutationApplied"]);

            byte[] after = File.ReadAllBytes(asset);
            MutableStringTableDocument reparsed = Parse(after);
            Assert.Equal("CHANGED", TextOf(reparsed, "beta"));
            // Untouched entries survive.
            Assert.Equal("first", TextOf(reparsed, "alpha"));
            Assert.Equal("third", TextOf(reparsed, "gamma"));
            Assert.False(Sha(before).SequenceEqual(Sha(after)));
        }

        // ── non-ASCII round-trip (named-João entry edited to a new non-ASCII value) ──

        [Fact]
        public void ApplySaveStf_EditText_NonAscii_RoundTripsByteClean()
        {
            const string rel = "string/en/city.stf";
            string asset = WriteLooseAsset(rel, StringTableFixtureBuilder.BuildV1Joao());

            // Edit the João entry to another accented string; the written file must re-parse to it.
            JObject env = RunApply(out int exit, "apply-save-stf", rel, "--root", _work, "--edit-text", "player_city=Théed");

            Assert.Equal(0, exit);
            Assert.True((bool)((JObject)env["result"])["sourceCrcPreserved"]);

            MutableStringTableDocument reparsed = Parse(File.ReadAllBytes(asset));
            Assert.Equal("Théed", TextOf(reparsed, "player_city"));
        }

        // ── failed-verify-no-write ──────────────────────────────────────────────

        [Fact]
        public void ApplySaveStf_FailedUntouchedVerify_ExitsTwo_AndFileIsByteUnchanged()
        {
            const string rel = "string/en/ui.stf";
            string asset = WriteLooseAsset(rel, StringTableFixtureBuilder.BuildV1MultiEntry());
            byte[] before = File.ReadAllBytes(asset);

            // Perturb an UNTOUCHED entry ("alpha") on top of the verb's "beta" edit.
            ApplySaveStfCommand.TestPerturbSerialized = bytes =>
            {
                MutableStringTableDocument mut = Parse(bytes);
                mut.Entries.First(e => e.Name == "alpha").Text = "TAMPERED";
                return StringTableWriter.Serialize(mut);
            };

            JObject env = RunApply(out int exit, "apply-save-stf", rel, "--root", _work, "--edit-text", "beta=CHANGED");

            Assert.Equal(2, exit);
            Assert.Equal("VerifyFailed", (string)((JObject)env["error"])["kind"]);

            byte[] after = File.ReadAllBytes(asset);
            Assert.True(before.SequenceEqual(after), "a failed verify must leave the file byte-unchanged");
        }

        // ── usage / missing-key / containment / not-found ───────────────────────

        [Fact]
        public void ApplySaveStf_EditTextMissingKey_ExitsTwo()
        {
            const string rel = "string/en/ui.stf";
            WriteLooseAsset(rel, StringTableFixtureBuilder.BuildV1MultiEntry());
            JObject env = RunApply(out int exit, "apply-save-stf", rel, "--root", _work, "--edit-text", "nonexistent=x");
            Assert.Equal(2, exit);
            Assert.Equal("StringTableParseException", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveStf_MalformedEditFlag_ExitsOne()
        {
            const string rel = "string/en/ui.stf";
            WriteLooseAsset(rel, StringTableFixtureBuilder.BuildV1MultiEntry());
            JObject env = RunApply(out int exit, "apply-save-stf", rel, "--root", _work, "--edit-text", "noequalssign");
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveStf_NoMutation_ExitsOne()
        {
            const string rel = "string/en/ui.stf";
            WriteLooseAsset(rel, StringTableFixtureBuilder.BuildV1MultiEntry());
            JObject env = RunApply(out int exit, "apply-save-stf", rel, "--root", _work);
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveStf_TraversalEscape_ExitsTwo()
        {
            JObject env = RunApply(out int exit, "apply-save-stf", "../escape.stf", "--root", _work, "--edit-text", "k=v");
            Assert.Equal(2, exit);
            Assert.Equal("PathContainment", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveStf_MissingFile_ExitsThree()
        {
            JObject env = RunApply(out int exit, "apply-save-stf", "string/nope.stf", "--root", _work, "--edit-text", "k=v");
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }
    }
}
