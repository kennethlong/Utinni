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
// Format understood by reading swg-client-v2 .../clientGame/.../ClientEffectTemplate.cpp (SOE/Bootprint,
// All Rights Reserved). No code, comments, identifier names, or test fixtures copied from any reference
// source. Implementation original to Utinni under MIT.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.ClientEffect;
using UtinniCoreDotNet.Formats.Iff;
using Utinni.Cli.Commands;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.ClientEffect
{
    /// <summary>
    /// <c>apply-save-effect</c> goldens — the variable-length save verb with the MUTATION-MODE structural
    /// verify (REVIEWS HIGH #2), canonical-payload + version-preservation (REVIEWS HIGH #2/#7), LOCKED
    /// add-defaults (REVIEWS MEDIUM #5), per-version D-03 (REVIEWS MEDIUM-Codex), and the negative battery
    /// (raw-leaf reject / perturb-verify-fail). Filter trait: <c>ApplySaveEffect</c>.
    /// </summary>
    public sealed class ApplySaveEffectCommandTests : IDisposable
    {
        private readonly string _work;

        public ApplySaveEffectCommandTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "applysaveeffect_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            ApplySaveEffectCommand.TestPerturbSerialized = null;
            try { if (Directory.Exists(_work)) Directory.Delete(_work, recursive: true); }
            catch { /* best-effort */ }
        }

        // Seeds an asset under <root>/loose/<rel> — the default --loose-subdir destination.
        private string WriteLooseAsset(string rel, byte[] bytes)
        {
            string p = Path.Combine(_work, "loose", rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllBytes(p, bytes);
            return p;
        }

        private static JObject RunApply(out int exit, params string[] args)
        {
            CliResult r = InProcessCliRunner.Run(args);
            exit = r.ExitCode;
            return JObject.Parse(r.Stdout);
        }

        // The stableId of the first command with the given tag, from the live decode-effect envelope.
        private static string StableIdOfTag(string assetPath, string tag)
        {
            CliResult r = InProcessCliRunner.Run("decode-effect", assetPath);
            JObject result = (JObject)JObject.Parse(r.Stdout)["result"];
            var cmd = ((JArray)result["commands"]).First(c => (string)c["tag"] == tag);
            return (string)cmd["stableId"];
        }

        // Decodes the on-disk file and returns the per-command payloads (by content), in order.
        private static System.Collections.Generic.List<byte[]> Payloads(byte[] bytes)
        {
            return ClientEffectDocument.FromBytes(bytes).Commands.Select(c => c.Leaf.GetPayloadCopy()).ToList();
        }

        // ── (a) field edit: length-changing string edit, untouched-by-stableId + canonical-payload ──

        [Fact]
        [Trait("Category", "ApplySaveEffect")]
        public void ApplySaveEffect_LongerAppearanceStringEdit_CommitsAndUntouchedByteIdentical_VersionUnchanged()
        {
            const string rel = "effect/edit.iff";
            byte[] original = ClefTestFixtures.BuildAllFive("0003");
            string asset = WriteLooseAsset(rel, original);
            string cpapId = StableIdOfTag(asset, "CPAP");

            // A LONGER appearance name than the seeded "appearance/test.prt" -> a length change (D-01).
            const string longerName = "appearance/some/much/longer/path/effect.prt";
            JObject env = RunApply(out int exit, "apply-save-effect", rel, "--root", _work,
                "--leaf", cpapId, "--field", "appearanceTemplateName", "--value", longerName);

            Assert.Equal(0, exit);
            JObject result = (JObject)env["result"];
            Assert.True((bool)result["written"]);
            Assert.Equal("FieldEdit", (string)result["mutationMode"]);
            Assert.Equal("0003", (string)result["version"]); // version unchanged (canonical-payload)

            byte[] after = File.ReadAllBytes(asset);
            Assert.NotEqual(original.Length, after.Length); // length changed (the point of CFX, D-01)

            // The edited CPAP decodes to the requested value; the other four commands are byte-identical.
            var afterCmds = ClientEffectDocument.FromBytes(after).Commands.ToList();
            var origCmds = ClientEffectDocument.FromBytes(original).Commands.ToList();
            var editedCpap = afterCmds.First(c => c.Tag == "CPAP");
            Assert.Equal(longerName, editedCpap.StringValue);
            foreach (string tag in new[] { "PSND", "CLGT", "CAMS", "FFBK" })
            {
                byte[] a = afterCmds.First(c => c.Tag == tag).Leaf.GetPayloadCopy();
                byte[] o = origCmds.First(c => c.Tag == tag).Leaf.GetPayloadCopy();
                Assert.True(a.SequenceEqual(o), tag + " (untouched) must be byte-identical");
            }
            // Whole-file still round-trips byte-exact (clean leaves re-emit verbatim).
            Assert.True(after.SequenceEqual(ClientEffectDocument.FromBytes(after).Serialize()));
        }

        // ── (e) per-version D-03: a field edit preserves the source version through the CLI ──

        [Theory]
        [Trait("Category", "ApplySaveEffect")]
        [InlineData("0001")]
        [InlineData("0002")]
        [InlineData("0003")]
        public void ApplySaveEffect_FieldEdit_PreservesSourceVersion_D03(string version)
        {
            string rel = "effect/v" + version + ".iff";
            byte[] original = ClefTestFixtures.BuildAllFive(version);
            string asset = WriteLooseAsset(rel, original);
            string cpapId = StableIdOfTag(asset, "CPAP");

            JObject env = RunApply(out int exit, "apply-save-effect", rel, "--root", _work,
                "--leaf", cpapId, "--field", "timeInSeconds", "--value", "9.5");
            Assert.Equal(0, exit);
            Assert.Equal(version, (string)((JObject)env["result"])["version"]);

            byte[] after = File.ReadAllBytes(asset);
            Assert.Equal(version, ClientEffectDocument.FromBytes(after).Version);
        }

        // ── (c) add: LOCKED defaults, count+1, originals present ──

        [Fact]
        [Trait("Category", "ApplySaveEffect")]
        public void ApplySaveEffect_AddCommandCpap_LockedDefaults_CountPlusOne_OriginalsPresent()
        {
            const string rel = "effect/add.iff";
            byte[] original = ClefTestFixtures.BuildAllFive("0003");
            string asset = WriteLooseAsset(rel, original);
            var origPayloads = Payloads(original);

            JObject env = RunApply(out int exit, "apply-save-effect", rel, "--root", _work, "--add-command", "CPAP");
            Assert.Equal(0, exit);
            Assert.Equal("Add", (string)((JObject)env["result"])["mutationMode"]);

            byte[] after = File.ReadAllBytes(asset);
            var afterCmds = ClientEffectDocument.FromBytes(after).Commands.ToList();
            Assert.Equal(origPayloads.Count + 1, afterCmds.Count);

            // The added (last) command equals the LOCKED defaults for CPAP @ 0003.
            byte[] expected = ClefCommandDefaults.BuildDefaultPayload("CPAP", "0003");
            Assert.True(afterCmds.Last().Leaf.GetPayloadCopy().SequenceEqual(expected));
            Assert.Equal("CPAP", afterCmds.Last().Tag);

            // All originals still present (by content).
            var afterPayloads = afterCmds.Select(c => c.Leaf.GetPayloadCopy()).ToList();
            foreach (byte[] op in origPayloads)
                Assert.Contains(afterPayloads, ap => ap.SequenceEqual(op));
        }

        // ── (d) remove: count-1, removed absent, survivors byte-identical by content ──

        [Fact]
        [Trait("Category", "ApplySaveEffect")]
        public void ApplySaveEffect_RemoveLeaf_CountMinusOne_SurvivorsByteIdentical()
        {
            const string rel = "effect/remove.iff";
            byte[] original = ClefTestFixtures.BuildAllFive("0003");
            string asset = WriteLooseAsset(rel, original);
            string clgtId = StableIdOfTag(asset, "CLGT");
            byte[] removedPayload = ClientEffectDocument.FromBytes(original).Commands.First(c => c.Tag == "CLGT").Leaf.GetPayloadCopy();

            JObject env = RunApply(out int exit, "apply-save-effect", rel, "--root", _work, "--remove-leaf", clgtId);
            Assert.Equal(0, exit);
            Assert.Equal("Remove", (string)((JObject)env["result"])["mutationMode"]);

            byte[] after = File.ReadAllBytes(asset);
            var afterCmds = ClientEffectDocument.FromBytes(after).Commands.ToList();
            Assert.Equal(4, afterCmds.Count);
            Assert.DoesNotContain(afterCmds, c => c.Tag == "CLGT");
            // The removed payload is absent; survivors are byte-identical to their originals (by content).
            var afterPayloads = afterCmds.Select(c => c.Leaf.GetPayloadCopy()).ToList();
            Assert.DoesNotContain(afterPayloads, p => p.SequenceEqual(removedPayload));
            var origCmds = ClientEffectDocument.FromBytes(original).Commands.ToList();
            foreach (string tag in new[] { "CPAP", "PSND", "CAMS", "FFBK" })
            {
                byte[] a = afterCmds.First(c => c.Tag == tag).Leaf.GetPayloadCopy();
                byte[] o = origCmds.First(c => c.Tag == tag).Leaf.GetPayloadCopy();
                Assert.True(a.SequenceEqual(o));
            }
        }

        // ── (b) reorder: count unchanged, (tag,payload) multiset preserved (NOT stableId equality) ──

        [Fact]
        [Trait("Category", "ApplySaveEffect")]
        public void ApplySaveEffect_Reorder_MultisetOfPayloadsPreserved_NotStableIdEquality()
        {
            const string rel = "effect/reorder.iff";
            byte[] original = ClefTestFixtures.BuildAllFive("0003");
            string asset = WriteLooseAsset(rel, original);
            string psndId = StableIdOfTag(asset, "PSND"); // index 1; move it up to index 0

            var origPayloads = Payloads(original);

            JObject env = RunApply(out int exit, "apply-save-effect", rel, "--root", _work,
                "--reorder", psndId, "--direction", "up");
            Assert.Equal(0, exit);
            Assert.Equal("Reorder", (string)((JObject)env["result"])["mutationMode"]);

            byte[] after = File.ReadAllBytes(asset);
            var afterPayloads = Payloads(after);
            Assert.Equal(origPayloads.Count, afterPayloads.Count);
            // Order CHANGED (PSND now first), but the multiset of payloads is identical.
            Assert.Equal("PSND", ClientEffectDocument.FromBytes(after).Commands[0].Tag);
            foreach (byte[] op in origPayloads)
            {
                int origN = origPayloads.Count(p => p.SequenceEqual(op));
                int afterN = afterPayloads.Count(p => p.SequenceEqual(op));
                Assert.Equal(origN, afterN);
            }
        }

        // ── (f) raw/non-editable target rejected (exit 1), no write ──

        [Fact]
        [Trait("Category", "ApplySaveEffect")]
        public void ApplySaveEffect_FieldEditOnRawCommand_Rejected_Exit1_NoWrite()
        {
            const string rel = "effect/raw.iff";
            byte[] original = ClefTestFixtures.BuildWithRawCommand();
            string asset = WriteLooseAsset(rel, original);
            byte[] before = File.ReadAllBytes(asset);
            // The XXXX command raw-falls-back; address it by stableId.
            string xxxxId = StableIdOfTag(asset, "XXXX");

            JObject env = RunApply(out int exit, "apply-save-effect", rel, "--root", _work,
                "--leaf", xxxxId, "--field", "appearanceTemplateName", "--value", "x");
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
            Assert.True(before.SequenceEqual(File.ReadAllBytes(asset)), "a rejected edit must leave the file byte-unchanged");
        }

        // ── combination of mutations rejected (exit 1) ──

        [Fact]
        [Trait("Category", "ApplySaveEffect")]
        public void ApplySaveEffect_FieldEditPlusAdd_Combination_Rejected_Exit1()
        {
            const string rel = "effect/combo.iff";
            byte[] original = ClefTestFixtures.BuildAllFive("0003");
            string asset = WriteLooseAsset(rel, original);
            string cpapId = StableIdOfTag(asset, "CPAP");

            JObject env = RunApply(out int exit, "apply-save-effect", rel, "--root", _work,
                "--leaf", cpapId, "--field", "timeInSeconds", "--value", "1.0", "--add-command", "PSND");
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
        }

        // ── (h) perturbed serialize forces VerifyFailed (exit 2), no write ──

        [Fact]
        [Trait("Category", "ApplySaveEffect")]
        public void ApplySaveEffect_PerturbedSerialize_VerifyFailed_Exit2_NoWrite()
        {
            const string rel = "effect/verify.iff";
            byte[] original = ClefTestFixtures.BuildAllFive("0003");
            string asset = WriteLooseAsset(rel, original);
            byte[] before = File.ReadAllBytes(asset);
            string cpapId = StableIdOfTag(asset, "CPAP");

            // Perturb an UNTOUCHED command (PSND) in the serialized output so the untouched-by-stableId
            // verify fails -> no write.
            ApplySaveEffectCommand.TestPerturbSerialized = bytes =>
            {
                using (var ms = new MemoryStream(bytes, writable: false))
                {
                    var doc = IffReader.Read(ms);
                    var mutable = MutableIffDocument.FromDocument(doc, bytes);
                    var psnd = FindFirstLeafByTag(mutable.Root, "PSND");
                    Assert.NotNull(psnd);
                    byte[] p = psnd.GetPayloadCopy();
                    p[0] ^= 0xFF;
                    psnd.SetPayload(p);
                    return IffWriter.Write(mutable);
                }
            };

            JObject env = RunApply(out int exit, "apply-save-effect", rel, "--root", _work,
                "--leaf", cpapId, "--field", "timeInSeconds", "--value", "3.0");
            Assert.Equal(2, exit);
            Assert.Equal("VerifyFailed", (string)((JObject)env["error"])["kind"]);
            Assert.True(before.SequenceEqual(File.ReadAllBytes(asset)), "a failed verify must leave the file byte-unchanged");
        }

        private static MutableIffNode FindFirstLeafByTag(MutableIffNode node, string tag)
        {
            if (node.Kind == MutableIffNodeKind.Leaf && node.TypeId == tag) return node;
            if (node.Kind == MutableIffNodeKind.Container)
            {
                foreach (var c in node.Children)
                {
                    var found = FindFirstLeafByTag(c, tag);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
