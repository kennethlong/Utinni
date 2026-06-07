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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.ObjectTemplate;
using Utinni.Cli.Commands;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    /// <summary>
    /// Plan 14-03a Task 1: golden tests for the <c>apply-save-ot</c> verb — the typed object-template
    /// member of the apply-save-* family. Proves READ-BACK persistence (re-parse the written OT and
    /// assert the local override is present / edited / absent; untouched params byte-identical; file
    /// hash changed) and failed-verify-no-write (exit 2, on-disk file byte-unchanged), plus the usage
    /// matrix (duplicate add, edit non-existent, missing --value-int).
    /// </summary>
    public sealed class ApplySaveOtCommandTests : IDisposable
    {
        private readonly string _work;

        public ApplySaveOtCommandTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "applysaveot_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            ApplySaveOtCommand.TestPerturbSerialized = null;
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort */ }
        }

        // ── OT fixture builder (mirrors RoundtripOtCommandTests) ────────────────

        private static byte[] EncodeParam(string name, byte[] valueRegion)
        {
            using (var ms = new MemoryStream())
            {
                byte[] n = Encoding.ASCII.GetBytes(name);
                ms.Write(n, 0, n.Length);
                ms.WriteByte(0);
                if (valueRegion != null && valueRegion.Length > 0) ms.Write(valueRegion, 0, valueRegion.Length);
                return ms.ToArray();
            }
        }

        private static byte[] Int32Le(int v)
        {
            return new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF) };
        }

        private static byte[] IntValueRegion(int v, char delta)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(1);
                ms.WriteByte((byte)delta);
                byte[] le = Int32Le(v);
                ms.Write(le, 0, le.Length);
                return ms.ToArray();
            }
        }

        private static byte[] BuildTemplate(string baseName, IList<KeyValuePair<string, byte[]>> paramChunks)
        {
            MutableIffNode root = MutableIffNode.NewContainer("FORM", "SHOT");
            if (baseName != null)
            {
                MutableIffNode derv = root.AddContainer("FORM", "DERV");
                using (var ms = new MemoryStream())
                {
                    byte[] n = Encoding.ASCII.GetBytes(baseName);
                    ms.Write(n, 0, n.Length);
                    ms.WriteByte(0);
                    derv.AddLeaf("XXXX", ms.ToArray());
                }
            }
            MutableIffNode versionForm = root.AddContainer("FORM", "0000");
            versionForm.AddLeaf("PCNT", Int32Le(paramChunks.Count));
            foreach (KeyValuePair<string, byte[]> p in paramChunks)
            {
                versionForm.AddLeaf("XXXX", EncodeParam(p.Key, p.Value));
            }
            return IffWriter.Write(new MutableIffDocument(root));
        }

        private static KeyValuePair<string, byte[]> P(string name, byte[] valueRegion)
        {
            return new KeyValuePair<string, byte[]>(name, valueRegion);
        }

        private static byte[] MultiParamTemplate(string baseName)
        {
            return BuildTemplate(baseName, new List<KeyValuePair<string, byte[]>>
            {
                P("hitPoints", IntValueRegion(100, ' ')),
                P("armor", IntValueRegion(5, ' ')),
                P("scale", IntValueRegion(1, ' '))
            });
        }

        // ── helpers ─────────────────────────────────────────────────────────────

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

        private static MutableObjectTemplate ParseOt(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                var iff = IffReader.Read(ms);
                return MutableObjectTemplate.FromMutableIff(MutableIffDocument.FromDocument(iff, bytes));
            }
        }

        private static bool HasLocal(MutableObjectTemplate t, string field)
        {
            return t.LocalParams.Any(p => string.Equals(p.FieldName, field, StringComparison.Ordinal));
        }

        // ── happy: add-override persists (READ-BACK + hash changed) ──────────────

        [Fact]
        public void ApplySaveOt_AddOverride_PersistsNewLocal_ReadBackPresent_HashChanged()
        {
            const string rel = "object/creature/base.iff";
            string asset = WriteLooseAsset(rel, MultiParamTemplate("object/base.iff"));
            byte[] before = File.ReadAllBytes(asset);

            JObject env = RunApply(out int exit, "apply-save-ot", rel, "--root", _work, "--add-override", "newField", "--value-int", "7");

            Assert.Equal(0, exit);
            JObject result = (JObject)env["result"];
            Assert.True((bool)result["written"]);
            Assert.True((bool)result["bytesEqualUntouched"]);
            Assert.Equal("add-override(newField)", (string)result["mutationApplied"]);

            byte[] after = File.ReadAllBytes(asset);
            MutableObjectTemplate reparsed = ParseOt(after);
            Assert.True(HasLocal(reparsed, "newField"), "the written OT must contain the new local override");
            Assert.False(Sha(before).SequenceEqual(Sha(after)));
        }

        [Fact]
        public void ApplySaveOt_Edit_PersistsEditedValue_ReadBackReflectsValue()
        {
            const string rel = "object/creature/base.iff";
            string asset = WriteLooseAsset(rel, MultiParamTemplate("object/base.iff"));

            JObject env = RunApply(out int exit, "apply-save-ot", rel, "--root", _work, "--edit", "hitPoints", "--value-int", "250");

            Assert.Equal(0, exit);
            Assert.True((bool)((JObject)env["result"])["bytesEqualUntouched"]);

            MutableObjectTemplate reparsed = ParseOt(File.ReadAllBytes(asset));
            var edited = reparsed.LocalParams.First(p => p.FieldName == "hitPoints");
            Assert.Equal(250, edited.Value.IntValue);
            Assert.True(HasLocal(reparsed, "armor"), "untouched params survive");
        }

        [Fact]
        public void ApplySaveOt_RemoveOverride_PersistsRemoval_ReadBackAbsent()
        {
            const string rel = "object/creature/base.iff";
            string asset = WriteLooseAsset(rel, MultiParamTemplate("object/base.iff"));

            JObject env = RunApply(out int exit, "apply-save-ot", rel, "--root", _work, "--remove-override", "armor");

            Assert.Equal(0, exit);
            Assert.True((bool)((JObject)env["result"])["written"]);

            MutableObjectTemplate reparsed = ParseOt(File.ReadAllBytes(asset));
            Assert.False(HasLocal(reparsed, "armor"), "removed override must be gone from the written OT");
            Assert.True(HasLocal(reparsed, "hitPoints"));
        }

        // ── failed-verify-no-write ──────────────────────────────────────────────

        [Fact]
        public void ApplySaveOt_FailedUntouchedVerify_ExitsTwo_AndFileIsByteUnchanged()
        {
            const string rel = "object/creature/base.iff";
            string asset = WriteLooseAsset(rel, MultiParamTemplate("object/base.iff"));
            byte[] before = File.ReadAllBytes(asset);

            // Perturb an UNTOUCHED param ("armor") on top of the verb's --edit hitPoints edit.
            ApplySaveOtCommand.TestPerturbSerialized = bytes =>
            {
                MutableObjectTemplate t = ParseOt(bytes);
                t.EditOverride("armor", ObjectTemplateParamValue.FromInt(987, (byte)' '));
                return t.Serialize();
            };

            JObject env = RunApply(out int exit, "apply-save-ot", rel, "--root", _work, "--edit", "hitPoints", "--value-int", "250");

            Assert.Equal(2, exit);
            Assert.Equal("VerifyFailed", (string)((JObject)env["error"])["kind"]);

            byte[] after = File.ReadAllBytes(asset);
            Assert.True(before.SequenceEqual(after), "a failed verify must leave the file byte-unchanged");
        }

        // ── usage matrix ────────────────────────────────────────────────────────

        [Fact]
        public void ApplySaveOt_DuplicateAddOverride_ExitsOne()
        {
            const string rel = "object/creature/base.iff";
            WriteLooseAsset(rel, MultiParamTemplate("object/base.iff"));
            JObject env = RunApply(out int exit, "apply-save-ot", rel, "--root", _work, "--add-override", "hitPoints", "--value-int", "1");
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveOt_EditNonExistent_ExitsOne()
        {
            const string rel = "object/creature/base.iff";
            WriteLooseAsset(rel, MultiParamTemplate("object/base.iff"));
            JObject env = RunApply(out int exit, "apply-save-ot", rel, "--root", _work, "--edit", "nope", "--value-int", "1");
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveOt_MissingValueInt_ExitsOne()
        {
            const string rel = "object/creature/base.iff";
            WriteLooseAsset(rel, MultiParamTemplate("object/base.iff"));
            JObject env = RunApply(out int exit, "apply-save-ot", rel, "--root", _work, "--add-override", "newField");
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveOt_NoMutation_ExitsOne()
        {
            const string rel = "object/creature/base.iff";
            WriteLooseAsset(rel, MultiParamTemplate("object/base.iff"));
            JObject env = RunApply(out int exit, "apply-save-ot", rel, "--root", _work);
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveOt_TraversalEscape_ExitsTwo()
        {
            JObject env = RunApply(out int exit, "apply-save-ot", "../escape.iff", "--root", _work, "--edit", "x", "--value-int", "1");
            Assert.Equal(2, exit);
            Assert.Equal("PathContainment", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        public void ApplySaveOt_MissingFile_ExitsThree()
        {
            JObject env = RunApply(out int exit, "apply-save-ot", "object/nope.iff", "--root", _work, "--edit", "x", "--value-int", "1");
            Assert.Equal(3, exit);
            Assert.Equal("FileNotFound", (string)((JObject)env["error"])["kind"]);
        }
    }
}
