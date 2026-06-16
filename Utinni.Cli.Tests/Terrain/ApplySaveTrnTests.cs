// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain (SOE/Bootprint, All
// Rights Reserved) and the EA-IFF-85 public standard. No code, comments, identifier names, or test fixtures
// copied from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Terrain;
using Utinni.Cli.Commands;
using Utinni.Cli.Tests.Fixtures.Trn;
using Utinni.Cli.Tests.Infrastructure;
using Xunit;

namespace Utinni.Cli.Tests.Terrain
{
    /// <summary>
    /// <c>apply-save-trn</c> fixed-length field-edit goldens (PROD-W2-TRN-03): single scalar/enum edit
    /// (exact-span), active-flag toggle on the IHDR int32, untouched-leaf byte-identity, the active-toggle
    /// re-decode parity (read↔write), and the negative battery (non-editable node, --root escape, .tre,
    /// bad-leaf). Every edit is fixed-length (no parent FORM length ripple — D-05/D-06).
    ///
    /// <para>Filter trait: <c>ApplySaveTrn</c>.</para>
    /// </summary>
    public sealed class ApplySaveTrnTests : IDisposable
    {
        private readonly string _work;

        public ApplySaveTrnTests()
        {
            _work = Path.Combine(Path.GetTempPath(), "applysavetrn_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            ApplySaveTrnCommand.TestPerturbSerialized = null;
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

        private static byte[] Sha(byte[] b) { using (var sha = SHA256.Create()) return sha.ComputeHash(b); }

        private static JObject RunApply(out int exit, params string[] args)
        {
            CliResult r = InProcessCliRunner.Run(args);
            exit = r.ExitCode;
            return JObject.Parse(r.Stdout);
        }

        // Enumerates (stableId, mutableLeaf) for every DATA leaf, so a test can address a specific leaf by
        // its enclosing tag without hard-coding the id string.
        private static List<(string id, MutableIffNode leaf, string parentTag, string grandTag)> DataLeaves(byte[] bytes)
        {
            IffDocument doc;
            using (var ms = new MemoryStream(bytes, writable: false)) doc = IffReader.Read(ms);
            MutableIffDocument m = MutableIffDocument.FromDocument(doc, bytes);
            var outList = new List<(string, MutableIffNode, string, string)>();
            Walk(m.Root, "", 0, outList);
            return outList;
        }

        private static void Walk(MutableIffNode node, string prefix, int ordinal,
            List<(string, MutableIffNode, string, string)> acc)
        {
            string id = MutableIffDocument.DeriveStableId(node, prefix, ordinal);
            if (node.Kind == MutableIffNodeKind.Leaf && node.TypeId == "DATA")
            {
                string parentTag = node.Parent != null ? node.Parent.SubTypeId : "";
                string grandTag = (node.Parent != null && node.Parent.Parent != null) ? node.Parent.Parent.SubTypeId : "";
                acc.Add((id, node, parentTag, grandTag));
            }
            if (node.Kind == MutableIffNodeKind.Container)
            {
                string cp = id + "/";
                for (int i = 0; i < node.Children.Count; i++) Walk(node.Children[i], cp, i, acc);
            }
        }

        // The DATA leaf id whose enclosing tag FORM (grandparent) is the given tag (typed-node edit target).
        private static string TypedDataLeafId(byte[] bytes, string tag)
            => DataLeaves(bytes).First(x => x.grandTag == tag).id;

        // The IHDR DATA leaf id (active-flag edit target).
        private static string IhdrDataLeafId(byte[] bytes)
            => DataLeaves(bytes).First(x => x.parentTag == "IHDR").id;

        // ── single scalar field edit: exact-span, untouched leaves identical ────

        [Fact]
        [Trait("Category", "ApplySaveTrn")]
        public void ApplySave_SingleScalarField_EditedValueOnly_UntouchedLeavesByteIdentical()
        {
            const string rel = "terrain/comp.trn";
            byte[] original = TgenFixtureSynthesizer.CompositionalLayer();
            string asset = WriteLooseAsset(rel, original);
            byte[] before = File.ReadAllBytes(asset);
            string leafId = TypedDataLeafId(before, "AHCN");

            JObject env = RunApply(out int exit, "apply-save-trn", rel, "--root", _work, "--leaf", leafId, "--field", "height", "--value", "777.5");

            Assert.Equal(0, exit);
            JObject result = (JObject)env["result"];
            Assert.True((bool)result["written"]);
            Assert.True((bool)result["bytesEqualUntouched"]);
            Assert.True((bool)result["fieldSpanOnly"]);
            Assert.Equal("AHCN", (string)result["tag"]);
            Assert.Equal("0000", (string)result["version"]);

            byte[] after = File.ReadAllBytes(asset);
            Assert.False(Sha(before).SequenceEqual(Sha(after)));
            // The BCIR typed sibling + DEAD/unknown siblings stay byte-exact: the written file still
            // round-trips whole-file byte-identical through the codec (clean nodes re-emit verbatim).
            byte[] rt = TerrainDocument.FromBytes(after).Serialize();
            Assert.True(after.SequenceEqual(rt));
        }

        [Fact]
        [Trait("Category", "ApplySaveTrn")]
        public void ApplySave_EnumField_AhcnOperation_EditedValueOnly()
        {
            const string rel = "terrain/op.trn";
            byte[] original = TgenFixtureSynthesizer.LowVersion("AHCN");
            string asset = WriteLooseAsset(rel, original);
            string leafId = TypedDataLeafId(original, "AHCN");

            JObject env = RunApply(out int exit, "apply-save-trn", rel, "--root", _work, "--leaf", leafId, "--field", "operation", "--value", "7");
            Assert.Equal(0, exit);
            Assert.True((bool)((JObject)env["result"])["fieldSpanOnly"]);
        }

        [Fact]
        [Trait("Category", "ApplySaveTrn")]
        public void ApplySave_ExactByteSpan_OnlyEditedFieldBytesDiffer()
        {
            const string rel = "terrain/span.trn";
            byte[] original = TgenFixtureSynthesizer.LowVersion("AHCN");
            string asset = WriteLooseAsset(rel, original);
            string leafId = TypedDataLeafId(original, "AHCN");

            RunApply(out int exit, "apply-save-trn", rel, "--root", _work, "--leaf", leafId, "--field", "height", "--value", "1234.0");
            Assert.Equal(0, exit);

            // Whole-file diff: AHCN payload [0..4)=operation unchanged; [4..8)=height differs.
            byte[] after = File.ReadAllBytes(asset);
            // Locate the AHCN DATA payload in both (same offset — fixed-length, no ripple).
            byte[] origData = TypedDataPayload(original, "AHCN");
            byte[] newData = TypedDataPayload(after, "AHCN");
            Assert.Equal(origData.Length, newData.Length);
            for (int i = 0; i < 4; i++) Assert.Equal(origData[i], newData[i]); // operation untouched
            Assert.False(origData.Skip(4).Take(4).SequenceEqual(newData.Skip(4).Take(4)));
        }

        // ── active-flag toggle on IHDR + read↔write parity (#10) ────────────────

        [Fact]
        [Trait("Category", "ApplySaveTrn")]
        public void ApplySave_ToggleActive_RoundtripDecodeReflectsEdit()
        {
            const string rel = "terrain/active.trn";
            // Seed active=true; toggle to 0 and assert re-decode reports Active=false.
            byte[] original = TgenFixtureSynthesizer.WithLayerHeader(active: true, name: "alpha");
            string asset = WriteLooseAsset(rel, original);

            // Sanity: the decoder reads Active=true from the seeded IHDR.
            Assert.True(TerrainDocument.FromBytes(original).Layers[0].Active);

            string ihdrLeaf = IhdrDataLeafId(original);
            JObject env = RunApply(out int exit, "apply-save-trn", rel, "--root", _work, "--leaf", ihdrLeaf, "--field", "active", "--value", "0");
            Assert.Equal(0, exit);
            Assert.Equal("IHDR", (string)((JObject)env["result"])["tag"]);

            // Read↔write parity: re-decode the WRITTEN bytes; the layer Active reflects the edit.
            byte[] after = File.ReadAllBytes(asset);
            TerrainDocument reDecoded = TerrainDocument.FromBytes(after);
            Assert.False(reDecoded.Layers[0].Active);
            // The layer name (rest of the IHDR leaf) is preserved.
            Assert.Equal("alpha", reDecoded.Layers[0].Name);
        }

        // ── CR-01: stable-id prefix collision at ordinals ≥10 ───────────────────

        // A layer with 12 same-tag AHCN siblings: the sibling at LAYR-child ordinal 1 is TRUNCATED
        // (raw-fallback / NON-editable); every other sibling is a valid editable AHCN. Node-1's stable id
        // (".../FORM:AHCN/1") is a bare-string prefix of node-10's leaf id (".../FORM:AHCN/10/..."), so an
        // unanchored StartsWith editability gate resolves a leaf in node-10 to node-1's (non-editable) verdict.
        // After the path-segment-anchored fix, each leaf resolves to ITS OWN node.
        [Fact]
        [Trait("Category", "ApplySaveTrn")]
        public void ApplySave_PrefixCollidingSibling_ResolvesEditabilityToOwnNode_NotNodeOne()
        {
            const string rel = "terrain/many.trn";
            byte[] original = TgenFixtureSynthesizer.WithManySameTagSiblings(siblingCount: 12, nonEditableOrdinal: 1);
            string asset = WriteLooseAsset(rel, original);
            byte[] before = File.ReadAllBytes(asset);

            // The EDITABLE node-10 leaf: its stable id contains "/FORM:AHCN/10/". With the bug this would
            // collide with node-1 (".../FORM:AHCN/1") and be WRONGLY rejected as non-editable.
            string node10Leaf = AhcnLeafAtOrdinal(before, 10);
            JObject editEnv = RunApply(out int editExit, "apply-save-trn", rel, "--root", _work,
                "--leaf", node10Leaf, "--field", "height", "--value", "321.5");
            Assert.Equal(0, editExit); // node-10 IS editable; the edit must be ACCEPTED, not bounced to node-1
            Assert.True((bool)((JObject)editEnv["result"])["written"]);
            Assert.Equal("AHCN", (string)((JObject)editEnv["result"])["tag"]);

            // The NON-editable node-1 (truncated, raw-fallback) leaf must STILL be rejected — the anchoring
            // must not accidentally wave it through by matching a longer editable sibling's prefix.
            byte[] afterFirstEdit = File.ReadAllBytes(asset);
            string node1Leaf = AhcnLeafAtOrdinal(afterFirstEdit, 1);
            JObject rejEnv = RunApply(out int rejExit, "apply-save-trn", rel, "--root", _work,
                "--leaf", node1Leaf, "--field", "height", "--value", "9.0");
            Assert.Equal(1, rejExit);
            Assert.Equal("UsageError", (string)((JObject)rejEnv["error"])["kind"]);
            Assert.True(afterFirstEdit.SequenceEqual(File.ReadAllBytes(asset)),
                "a rejected edit on the non-editable node-1 must leave the file byte-unchanged");
        }

        // The DATA leaf id of the AHCN sibling at the given LAYR-child ordinal (matches "/FORM:AHCN/<ord>/"
        // as a path-segment-anchored substring so ordinal 1 never matches ordinal 10..19).
        private static string AhcnLeafAtOrdinal(byte[] bytes, int ordinal)
        {
            string seg = "/FORM:AHCN/" + ordinal + "/";
            return DataLeaves(bytes).First(x => x.grandTag == "AHCN" && x.id.Contains(seg)).id;
        }

        // ── negative battery (#4 / V12 / .tre / bad-leaf) ───────────────────────

        [Fact]
        [Trait("Category", "ApplySaveTrn")]
        public void ApplySave_NonEditableNode_Rejected_NoWrite()
        {
            const string rel = "terrain/unknown.trn";
            byte[] original = TgenFixtureSynthesizer.WithUnknownTag("ZZZZ");
            string asset = WriteLooseAsset(rel, original);
            byte[] before = File.ReadAllBytes(asset);
            // The ZZZZ node is raw-preserved (non-editable); its DATA leaf id.
            string leafId = TypedDataLeafId(before, "ZZZZ");

            JObject env = RunApply(out int exit, "apply-save-trn", rel, "--root", _work, "--leaf", leafId, "--field", "operation", "--value", "1");
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
            Assert.True(before.SequenceEqual(File.ReadAllBytes(asset)), "a rejected edit must leave the file byte-unchanged");
        }

        [Fact]
        [Trait("Category", "ApplySaveTrn")]
        public void ApplySave_PathOutsideRoot_FailClosed_NoWrite()
        {
            JObject env = RunApply(out int exit, "apply-save-trn", "../escape.trn", "--root", _work, "--leaf", "x", "--field", "height", "--value", "1");
            Assert.Equal(2, exit);
            Assert.Equal("PathContainment", (string)((JObject)env["error"])["kind"]);
        }

        [Fact]
        [Trait("Category", "ApplySaveTrn")]
        public void ApplySave_TreMagic_Rejected_NoWrite()
        {
            const string rel = "terrain/fake.trn";
            byte[] tre = new byte[] { (byte)'E', (byte)'E', (byte)'R', (byte)'T', 0, 0, 0, 0 };
            string asset = WriteLooseAsset(rel, tre);
            JObject env = RunApply(out int exit, "apply-save-trn", rel, "--root", _work, "--leaf", "x", "--field", "height", "--value", "1");
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
            Assert.True(tre.SequenceEqual(File.ReadAllBytes(asset)));
        }

        [Fact]
        [Trait("Category", "ApplySaveTrn")]
        public void ApplySave_BadLeafId_Rejected_NoWrite()
        {
            const string rel = "terrain/bad.trn";
            byte[] original = TgenFixtureSynthesizer.LowVersion("AHCN");
            string asset = WriteLooseAsset(rel, original);
            JObject env = RunApply(out int exit, "apply-save-trn", rel, "--root", _work, "--leaf", "FORM:TGEN/0/DATA:DATA/99", "--field", "height", "--value", "1");
            Assert.Equal(1, exit);
            Assert.Equal("UsageError", (string)((JObject)env["error"])["kind"]);
            Assert.True(original.SequenceEqual(File.ReadAllBytes(asset)));
        }

        [Fact]
        [Trait("Category", "ApplySaveTrn")]
        public void ApplySave_FailedVerify_ExitsTwo_FileByteUnchanged()
        {
            const string rel = "terrain/verify.trn";
            // Compositional fixture has multiple DATA leaves (AHCN, ZZZZ, BCIR) — edit AHCN, perturb BCIR.
            byte[] original = TgenFixtureSynthesizer.CompositionalLayer();
            string asset = WriteLooseAsset(rel, original);
            byte[] before = File.ReadAllBytes(asset);
            string leafId = TypedDataLeafId(before, "AHCN");

            // Perturb the UNTOUCHED BCIR DATA leaf in the serialized output -> untouched-verify fails -> no write.
            ApplySaveTrnCommand.TestPerturbSerialized = bytes =>
            {
                using (var ms = new MemoryStream(bytes, writable: false))
                {
                    var doc = IffReader.Read(ms);
                    var mutable = MutableIffDocument.FromDocument(doc, bytes);
                    // Walk to the BCIR tag FORM's DATA leaf and flip its first payload byte.
                    var bcirLeaf = FindFirstDataLeafUnderTag(mutable.Root, "BCIR");
                    Assert.NotNull(bcirLeaf);
                    byte[] p = bcirLeaf.GetPayloadCopy();
                    p[0] ^= 0xFF;
                    bcirLeaf.SetPayload(p);
                    return IffWriter.Write(mutable);
                }
            };

            JObject env = RunApply(out int exit, "apply-save-trn", rel, "--root", _work, "--leaf", leafId, "--field", "height", "--value", "2.0");
            Assert.Equal(2, exit);
            Assert.True(before.SequenceEqual(File.ReadAllBytes(asset)), "a failed verify must leave the file byte-unchanged");
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        private static byte[] TypedDataPayload(byte[] bytes, string tag)
            => DataLeaves(bytes).First(x => x.grandTag == tag).leaf.GetPayloadCopy();

        // Finds the first DATA leaf whose enclosing tag FORM (grandparent) SubTypeId == tag, in a mutable DOM.
        private static MutableIffNode FindFirstDataLeafUnderTag(MutableIffNode node, string tag)
        {
            if (node.Kind == MutableIffNodeKind.Leaf && node.TypeId == "DATA"
                && node.Parent != null && node.Parent.Parent != null
                && node.Parent.Parent.SubTypeId == tag)
            {
                return node;
            }
            if (node.Kind == MutableIffNodeKind.Container)
            {
                foreach (var c in node.Children)
                {
                    var found = FindFirstDataLeafUnderTag(c, tag);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
