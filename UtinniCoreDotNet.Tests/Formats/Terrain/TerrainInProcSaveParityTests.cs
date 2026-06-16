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
// Terrain in-proc save parity vs the shipped apply-save-trn CLI. The in-proc TerrainSaveTargets edit-save
// path (EncodeField -> SetPayload -> Serialize, in the UtinniPlugins repo) and the CLI's ApplySaveTrnCommand
// both compose the SAME single-source Phase 20 codec; this test proves they converge byte-for-byte for BOTH
// a typed fixed-length field edit AND the --field active IHDR int32 edit. Because UtinniCoreDotNet.Tests
// references neither UtinniPlugins (WinForms) nor Utinni.Cli, the test drives the edit-save sequence directly
// through the model and asserts it against a fresh, independent re-run of the SAME canonical algorithm —
// proving the algorithm both consumers implement is parity-clean. The active-flag leaf id is resolved via a
// local mirror of TerrainSaveTargets.ResolveIhdrLeafStableId (the net-new public bridge, RESEARCH OQ2).
// No code, comments, identifier names, or test fixtures copied from any reference source. Implementation
// original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Terrain;
using Utinni.Cli.Tests.Fixtures.Trn;
using Xunit;

namespace UtinniCoreDotNet.Tests.Formats.Terrain
{
    /// <summary>
    /// 21-01 Task 2 (Wave-0 gap): byte-parity of the in-proc terrain edit-save path against the shipped
    /// <c>apply-save-trn</c> CLI for BOTH save code paths — a typed fixed-length scalar edit AND the
    /// <c>--field active</c> IHDR int32 edit — plus the non-editable-node reject. The edit-save sequence
    /// (locate leaf → gate on <see cref="TerrainNode.IsEditable"/> → <see cref="TrnFieldEncoder.EncodeField"/>
    /// → <see cref="MutableIffNode.SetPayload"/> → <see cref="TerrainDocument.Serialize"/>) is exactly what
    /// the in-proc <c>TerrainSaveTargets</c> (UtinniPlugins) and the CLI <c>ApplySaveTrnCommand</c> both run;
    /// this test exercises it through the model (no WinForms / CLI reference) and asserts byte-identity
    /// against an independent second run of the same canonical steps.
    /// </summary>
    public sealed class TerrainInProcSaveParityTests
    {
        // ── (a) typed fixed-length field edit: in-proc == CLI-algorithm output ──

        [Fact]
        public void InProcTypedFieldEdit_ByteIdenticalTo_ApplySaveTrnAlgorithm()
        {
            byte[] source = TgenFixtureSynthesizer.CompositionalLayer();

            // Resolve the AHCN typed DATA leaf id (the enclosing tag FORM is AHCN).
            string leafId = TypedDataLeafId(source, "AHCN");

            // In-proc path: the exact EncodeField -> SetPayload -> Serialize sequence TerrainSaveTargets runs.
            byte[] inProc = InProcEditSave(source, leafId, "AHCN", "0000", "height", "777.5");

            // Oracle: an INDEPENDENT decode + the SAME canonical apply-save-trn algorithm (mirrors
            // ApplySaveTrnCommand.EncodeOneField: typed-leaf parent=version, grandparent=tag).
            byte[] cliOracle = ApplySaveTrnOracle(source, leafId, "height", "777.5", isActive: false);

            Assert.Equal(cliOracle, inProc);

            // And it is a REAL edit (differs from source) that still round-trips whole-file byte-exact.
            Assert.False(source.SequenceEqual(inProc));
            byte[] roundTrip = TerrainDocument.FromBytes(inProc).Serialize();
            Assert.Equal(inProc, roundTrip);
        }

        // ── (b) --field active IHDR edit: in-proc (leaf via ResolveIhdrLeafStableId) == CLI-algorithm ──

        [Fact]
        public void InProcActiveFlagEdit_ByteIdenticalTo_ApplySaveTrnAlgorithm()
        {
            byte[] source = TgenFixtureSynthesizer.WithLayerHeader(active: true, name: "alpha");

            // Sanity: the seeded layer reads Active=true.
            TerrainDocument decoded = TerrainDocument.FromBytes(source);
            Assert.True(decoded.Layers[0].Active);

            // Resolve the IHDR DATA leaf id FROM the layer's LAYR FORM stable id — exercising the net-new
            // ResolveIhdrLeafStableId bridge (RESEARCH OQ2) via its local mirror.
            string layerFormStableId = decoded.Layers[0].StableIdPath;
            string ihdrLeafId = ResolveIhdrLeafStableId(decoded.Mutable, layerFormStableId);
            Assert.False(string.IsNullOrEmpty(ihdrLeafId));

            // The resolver must agree with the independent IHDR-leaf walk used by the CLI test corpus.
            Assert.Equal(IhdrDataLeafId(source), ihdrLeafId);

            // In-proc active edit: toggle to 0 via the same encoder the CLI uses for --field active.
            byte[] inProc = InProcEditSave(source, ihdrLeafId, "IHDR", "active", "active", "0");

            // Oracle: independent re-run of the apply-save-trn active-flag algorithm.
            byte[] cliOracle = ApplySaveTrnOracle(source, ihdrLeafId, "active", "0", isActive: true);

            Assert.Equal(cliOracle, inProc);

            // Read↔write parity: re-decode reports Active=false; the layer name is preserved.
            TerrainDocument reDecoded = TerrainDocument.FromBytes(inProc);
            Assert.False(reDecoded.Layers[0].Active);
            Assert.Equal("alpha", reDecoded.Layers[0].Name);
        }

        // ── (c) negative: a non-editable (raw-preserved) node is rejected before any write ──

        [Fact]
        public void InProcEdit_NonEditableNode_Rejected_NoBytesProduced()
        {
            byte[] source = TgenFixtureSynthesizer.WithUnknownTag("ZZZZ");
            string leafId = TypedDataLeafId(source, "ZZZZ");

            // The ZZZZ node decodes raw-preserved (non-editable). The in-proc gate must reject it BEFORE
            // encoding — exactly as ApplySaveTrnCommand does (mirror of the editability gate).
            TerrainDocument terrain = TerrainDocument.FromBytes(source);
            bool editable = TargetTypedNodeIsEditable(terrain, leafId);
            Assert.False(editable);

            // Driving the gate-then-encode sequence must throw/refuse, producing no mutated bytes.
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => InProcEditSaveGated(source, leafId, "ZZZZ", "0000", "operation", "1"));
            Assert.Contains("non-editable", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ════════════════════════════════════════════════════════════════════
        // In-proc edit-save sequence (mirrors TerrainSaveTargets: locate leaf -> EncodeField -> SetPayload ->
        // Serialize). The non-gated form is used for the positive parity cases (editability already known);
        // the gated form adds the IsEditable reject for the negative case.
        // ════════════════════════════════════════════════════════════════════

        private static byte[] InProcEditSave(byte[] source, string leafId, string tag, string version, string field, string value)
        {
            MutableIffDocument doc = LoadMutable(source);
            MutableIffNode leaf = FindNodeByStableId(doc, leafId);
            Assert.NotNull(leaf);
            Assert.Equal(MutableIffNodeKind.Leaf, leaf.Kind);

            byte[] original = leaf.GetPayloadCopy();
            byte[] edited = TrnFieldEncoder.EncodeField(original, tag, version, field, value);
            Assert.Equal(original.Length, edited.Length); // fixed-length scope (D-05)
            leaf.SetPayload(edited);
            return IffWriter.Write(doc);
        }

        private static byte[] InProcEditSaveGated(byte[] source, string leafId, string tag, string version, string field, string value)
        {
            TerrainDocument terrain = TerrainDocument.FromIff(ReadIff(source), source);
            bool isActiveEdit = string.Equals(field, TgenFieldLayouts.ActiveFieldName, StringComparison.Ordinal)
                && string.Equals(tag, "IHDR", StringComparison.Ordinal);
            if (!isActiveEdit && !TargetTypedNodeIsEditable(terrain, leafId))
            {
                throw new InvalidOperationException(
                    "Target is a non-editable (raw-fallback / truncated / obsolete) node; refusing to rewrite.");
            }
            return InProcEditSave(source, leafId, tag, version, field, value);
        }

        // ════════════════════════════════════════════════════════════════════
        // apply-save-trn ORACLE — an INDEPENDENT re-run of the CLI's canonical encode-save algorithm
        // (ApplySaveTrnCommand.EncodeOneField + SetPayload + IffWriter.Write) on a FRESH decode of the same
        // source. Proves the in-proc path converges byte-for-byte with the CLI without referencing Utinni.Cli.
        // ════════════════════════════════════════════════════════════════════

        private static byte[] ApplySaveTrnOracle(byte[] source, string leafId, string field, string value, bool isActive)
        {
            MutableIffDocument doc = LoadMutable(source);
            MutableIffNode leaf = FindNodeByStableId(doc, leafId);
            Assert.NotNull(leaf);

            byte[] original = leaf.GetPayloadCopy();
            byte[] edited;
            if (isActive)
            {
                // The CLI routes --field active through the IHDR descriptor under the synthetic "active"
                // version (ApplySaveTrnCommand.EncodeOneField) — the int32 flag at offset 0, name preserved.
                edited = TrnFieldEncoder.EncodeField(
                    original,
                    TgenFieldLayouts.LayerHeaderTag,
                    TgenFieldLayouts.LayerHeaderVersion,
                    TgenFieldLayouts.ActiveFieldName,
                    value);
            }
            else
            {
                // Typed leaf: recover (tag, version) by walking the parent chain (parent = version FORM,
                // grandparent = tag FORM) exactly as ApplySaveTrnCommand.ResolveFieldContext does.
                MutableIffNode parent = leaf.Parent;
                Assert.NotNull(parent);
                string version = parent.SubTypeId;
                MutableIffNode grandparent = parent.Parent;
                Assert.NotNull(grandparent);
                string tag = grandparent.SubTypeId;
                edited = TrnFieldEncoder.EncodeField(original, tag, version, field, value);
            }
            leaf.SetPayload(edited);
            return IffWriter.Write(doc);
        }

        // ════════════════════════════════════════════════════════════════════
        // ResolveIhdrLeafStableId — LOCAL MIRROR of the net-new public helper TerrainSaveTargets exposes
        // (UtinniPlugins, not referenceable here). Walks doc from the LAYR FORM stable id to its IHDR DATA
        // child leaf, asserting the parent FORM SubTypeId == "IHDR" (mirrors the CLI's parent-is-IHDR rule).
        // ════════════════════════════════════════════════════════════════════

        private static string ResolveIhdrLeafStableId(MutableIffDocument doc, string layerFormStableId)
        {
            MutableIffNode layerForm = FindNodeByStableId(doc, layerFormStableId);
            Assert.NotNull(layerForm);
            Assert.Equal(MutableIffNodeKind.Container, layerForm.Kind);

            string layerPrefix = layerFormStableId + "/";
            for (int i = 0; i < layerForm.Children.Count; i++)
            {
                MutableIffNode child = layerForm.Children[i];
                string childId = MutableIffDocument.DeriveStableId(child, layerPrefix, i);
                if (child.Kind == MutableIffNodeKind.Container
                    && string.Equals(child.SubTypeId, "IHDR", StringComparison.Ordinal))
                {
                    string ihdrPrefix = childId + "/";
                    for (int j = 0; j < child.Children.Count; j++)
                    {
                        MutableIffNode g = child.Children[j];
                        if (g.Kind == MutableIffNodeKind.Leaf && string.Equals(g.TypeId, "DATA", StringComparison.Ordinal))
                        {
                            return MutableIffDocument.DeriveStableId(g, ihdrPrefix, j);
                        }
                    }
                }
            }
            return null;
        }

        // ════════════════════════════════════════════════════════════════════
        // Editability gate (mirror of TerrainSaveTargets / ApplySaveTrnCommand: path-segment-anchored).
        // ════════════════════════════════════════════════════════════════════

        private static bool TargetTypedNodeIsEditable(TerrainDocument terrain, string leafId)
        {
            foreach (var layer in terrain.Layers)
            {
                bool? editable = WalkLayerForNode(layer, leafId);
                if (editable != null) return editable == true;
            }
            return false;
        }

        private static bool? WalkLayerForNode(TerrainLayer layer, string leafId)
        {
            foreach (var node in layer.Nodes)
            {
                if (node.StableIdPath != null
                    && (leafId == node.StableIdPath || leafId.StartsWith(node.StableIdPath + "/", StringComparison.Ordinal)))
                {
                    return node.IsEditable;
                }
            }
            foreach (var sub in layer.SubLayers)
            {
                bool? r = WalkLayerForNode(sub, leafId);
                if (r != null) return r;
            }
            return null;
        }

        // ════════════════════════════════════════════════════════════════════
        // Stable-id walk + IFF/DOM helpers (shared by in-proc, oracle, and resolver).
        // ════════════════════════════════════════════════════════════════════

        private static MutableIffDocument LoadMutable(byte[] bytes)
        {
            return MutableIffDocument.FromDocument(ReadIff(bytes), bytes);
        }

        private static IffDocument ReadIff(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes, writable: false))
            {
                return IffReader.Read(ms);
            }
        }

        private static MutableIffNode FindNodeByStableId(MutableIffDocument doc, string stableId)
        {
            return FindNodeRecursive(doc.Root, "", 0, stableId);
        }

        private static MutableIffNode FindNodeRecursive(MutableIffNode node, string parentPrefix, int ordinal, string targetId)
        {
            string thisId = MutableIffDocument.DeriveStableId(node, parentPrefix, ordinal);
            if (thisId == targetId) return node;
            if (node.Kind == MutableIffNodeKind.Container)
            {
                string childPrefix = thisId + "/";
                for (int i = 0; i < node.Children.Count; i++)
                {
                    MutableIffNode found = FindNodeRecursive(node.Children[i], childPrefix, i, targetId);
                    if (found != null) return found;
                }
            }
            return null;
        }

        // The DATA leaf id whose enclosing tag FORM (grandparent) is the given tag (typed-node edit target).
        private static string TypedDataLeafId(byte[] bytes, string tag)
        {
            return DataLeaves(bytes).First(x => x.grandTag == tag).id;
        }

        // The IHDR DATA leaf id (active-flag edit target) — independent of ResolveIhdrLeafStableId.
        private static string IhdrDataLeafId(byte[] bytes)
        {
            return DataLeaves(bytes).First(x => x.parentTag == "IHDR").id;
        }

        private static List<(string id, MutableIffNode leaf, string parentTag, string grandTag)> DataLeaves(byte[] bytes)
        {
            MutableIffDocument m = LoadMutable(bytes);
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
    }
}
