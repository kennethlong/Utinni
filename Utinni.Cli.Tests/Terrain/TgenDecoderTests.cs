// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain (SOE/Bootprint, All
// Rights Reserved) and the EA-IFF-85 public standard. No code, comments, identifier names, or test fixtures
// copied from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine;
using UtinniCoreDotNet.Formats.Terrain;
using Utinni.Cli.Tests.Fixtures.Trn;
using Xunit;

namespace Utinni.Cli.Tests.Terrain
{
    /// <summary>
    /// Terrain (<c>.trn</c> / FORM TGEN) decoder navigation + typed-field + raw-fallback + DEAD-skip +
    /// negative-battery tests (PROD-W2-TRN-01/02).
    ///
    /// <para>Most methods are <c>Skip</c>-marked Wave-1 stubs — the <see cref="TgenDecoder"/> does not exist
    /// yet (it is built in Plan 02). They are COLLECTED by the runner (visible Skipped, not absent) so no
    /// downstream wave inherits a silent test gap. Two facts are NON-Skipped and run GREEN in Wave 0:
    /// the synthesizer self-test (Task 1, mitigation T-20-01) and the verb-registration ceiling smoke
    /// (Task 2, D-11).</para>
    ///
    /// <para>Filter traits: <c>TgenDecode</c> (navigation + typed) and <c>TgenRawFallback</c> (raw / DEAD /
    /// negative battery) so the VALIDATION § Per-Task Verification Map filters resolve.</para>
    /// </summary>
    public class TgenDecoderTests
    {
        private const string WaveOne = "Wave 1 — pending TgenDecoder (Plan 02)";

        // ─────────────────────────────────────────────────────────────────────
        // Task 1 (Wave 0, NON-Skipped): synthesizer self-test — mitigation T-20-01.
        // Every emitted fixture must re-parse cleanly AND stay within the ≤200-byte budget,
        // and the both-lineage matrix must cover every Tier-1 tag at low+high.
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        [Trait("Category", "TgenDecode")]
        public void Synthesizer_EveryFixture_ReParsesAndWithinByteBudget()
        {
            IReadOnlyDictionary<string, int> lengths = TgenFixtureSynthesizer.AssertAllFixturesWellFramed();

            // Minimal + every Tier-1 low/high arm + the four edge fixtures are present.
            Assert.True(lengths.ContainsKey("MinimalTgen"));
            foreach (string tag in TgenEraVersions.Tier1Tags)
            {
                Assert.True(lengths.ContainsKey(tag + ":low"), "Missing low-version fixture for " + tag);
                Assert.True(lengths.ContainsKey(tag + ":high"), "Missing high-version fixture for " + tag);
            }
            Assert.True(lengths.ContainsKey("UnknownTag:ZZZZ"));
            Assert.True(lengths.ContainsKey("DeadTag:BALL"));
            Assert.True(lengths.ContainsKey("TruncatedKnownTag:AHCN"));
            Assert.True(lengths.ContainsKey("CompositionalLayer"));

            // Every fixture is within the committed-golden budget (the self-test already asserts this,
            // re-assert here so the budget is an explicit, visible test signal).
            foreach (var kvp in lengths)
                Assert.True(kvp.Value <= TgenFixtureSynthesizer.MaxFixtureBytes,
                    "Fixture '" + kvp.Key + "' exceeded the byte budget.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Task 2 (Wave 0, NON-Skipped): D-11 verb-ceiling smoke — proves the existing Type[] dispatch
        // admits an additional trn verb with no BadVerbSelectedError, WITHOUT shipping a no-op verb in
        // Program.cs (review concern #7). Uses a test-local sentinel options class only.
        // ─────────────────────────────────────────────────────────────────────

        [Verb("trn-smoke", Hidden = true, HelpText = "Test-only sentinel verb — NOT shipped in Program.cs.")]
        private sealed class TrnSmokeSentinelOptions
        {
            [Option("flag", Required = false, HelpText = "Optional sentinel flag.")]
            public bool Flag { get; set; }
        }

        [Fact]
        [Trait("Category", "TgenDecode")]
        public void VerbDispatch_AdmitsAdditionalTrnVerb_NoCeilingError()
        {
            // The production verb-options set (mirrors Utinni.Cli/Program.cs ParseArguments Type[]),
            // PLUS one test-local sentinel verb — proving an additional trn verb registers cleanly.
            var types = new[]
            {
                typeof(global::Utinni.Cli.Commands.ParseTreOptions),
                typeof(global::Utinni.Cli.Commands.ListObjectsOptions),
                typeof(global::Utinni.Cli.Commands.InspectIffOptions),
                typeof(global::Utinni.Cli.Commands.DecodeIffOptions),
                typeof(global::Utinni.Cli.Commands.RoundtripIffOptions),
                typeof(global::Utinni.Cli.Commands.RoundtripTabOptions),
                typeof(global::Utinni.Cli.Commands.RoundtripStfOptions),
                typeof(global::Utinni.Cli.Commands.RoundtripOtOptions),
                typeof(global::Utinni.Cli.Commands.ValidatePluginOptions),
                typeof(global::Utinni.Cli.Commands.SaveOptions),
                typeof(global::Utinni.Cli.Commands.RepackTreOptions),
                typeof(global::Utinni.Cli.Commands.CompileTemplateOptions),
                typeof(global::Utinni.Cli.Commands.BuildTreOptions),
                typeof(global::Utinni.Cli.Commands.CompileDefinitionOptions),
                typeof(global::Utinni.Cli.Commands.CompileDatatableOptions),
                typeof(global::Utinni.Cli.Commands.ExportArmorOptions),
                typeof(global::Utinni.Cli.Commands.ExportWeaponOptions),
                typeof(global::Utinni.Cli.Commands.ApplySaveTabOptions),
                typeof(global::Utinni.Cli.Commands.ApplySaveOtOptions),
                typeof(global::Utinni.Cli.Commands.ApplySaveIffOptions),
                typeof(global::Utinni.Cli.Commands.ApplySaveStfOptions),
                typeof(global::Utinni.Cli.Commands.RoundtripParticleOptions),
                typeof(global::Utinni.Cli.Commands.ValidateBundleOptions),
                typeof(TrnSmokeSentinelOptions), // the additional trn verb under test
            };

            bool sawBadVerb = false;
            using (var parser = new Parser(s => s.CaseSensitive = false))
            {
                var result = parser.ParseArguments(new[] { "trn-smoke" }, types);
                result.WithNotParsed(errs =>
                {
                    foreach (var e in errs)
                        if (e is BadVerbSelectedError) sawBadVerb = true;
                });
            }

            Assert.False(sawBadVerb,
                "The dispatch path rejected an additional 'trn-smoke' verb with a BadVerbSelectedError — "
                + "the CommandLineParser verb ceiling would block the real trn verbs.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Wave-1 (Plan 02 Task 2): TgenDecoder navigation + typed-field + raw-fallback + DEAD-skip +
        // negative battery (PROD-W2-TRN-01/02). Implemented against the TgenFixtureSynthesizer matrix.
        // ─────────────────────────────────────────────────────────────────────

        // Helper: the single decoded boundary/filter/affector node of a single-tag fixture (the first
        // node of the first layer). Throws via xUnit assertions if the navigation shape is unexpected.
        private static TerrainNode SingleNode(TerrainDocument doc)
        {
            Assert.Single(doc.Layers);
            TerrainLayer layer = doc.Layers[0];
            Assert.Single(layer.Nodes);
            return layer.Nodes[0];
        }

        [Fact]
        [Trait("Category", "TgenDecode")]
        public void Decode_MinimalTgen_NavigatesTopLevelFormNoPalettesNoLayers()
        {
            TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.MinimalTgen());

            Assert.Equal("TGEN", doc.RootForm);
            Assert.Empty(doc.Layers);
            Assert.All(doc.Palettes.InLoadOrder, p => Assert.False(p.Present));
        }

        [Fact]
        [Trait("Category", "TgenDecode")]
        public void Decode_TgenTree_NavigatesLayersBoundariesFiltersAffectorsAndSubLayers()
        {
            // CompositionalLayer = one layer with AHCN (affector), BALL (dead), ZZZZ (unknown), BCIR (boundary).
            TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.CompositionalLayer());

            Assert.Single(doc.Layers);
            TerrainLayer layer = doc.Layers[0];
            Assert.Equal(4, layer.Nodes.Count); // four sibling children all navigable.
            Assert.Contains(layer.Nodes, n => n.Tag == "AHCN" && n.IsEditable);
            Assert.Contains(layer.Nodes, n => n.Tag == "BCIR" && n.IsEditable);
            Assert.Contains(layer.Nodes, n => n.Tag == "BALL" && n.IsDeadSkipped);
            Assert.Contains(layer.Nodes, n => n.Tag == "ZZZZ" && n.IsRawPreserved);
        }

        [Fact]
        [Trait("Category", "TgenDecode")]
        public void Decode_Layer_ExposesNameAndActive()
        {
            // TRN-01: each layer exposes Name + Active. The synthesizer's minimal layer has no IHDR, so
            // Active defaults to true (the C++ LayerItem default) and Name is empty — the navigable shape
            // still surfaces both properties.
            TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.LowVersion("AHCN"));

            Assert.Single(doc.Layers);
            Assert.True(doc.Layers[0].Active);
            Assert.NotNull(doc.Layers[0].Name);
        }

        [Fact]
        [Trait("Category", "TgenDecode")]
        public void Decode_SixPalettes_AreExposedPositionallyInLoadOrder()
        {
            // MinimalTgen has no palettes — assert the six positional slots exist in fixed load order with
            // their roles, all absent (the positional state machine yields all-absent, not a throw).
            TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.MinimalTgen());

            string[] expectedRoles = { "Shader", "Flora", "Radial", "Environment", "Fractal", "Bitmap" };
            Assert.Equal(expectedRoles, doc.Palettes.InLoadOrder.Select(p => p.Role).ToArray());
            Assert.Equal("Fractal", doc.Palettes.Fractal.Role);
            Assert.Equal("Bitmap", doc.Palettes.Bitmap.Role);
        }

        [Theory]
        [Trait("Category", "TgenDecode")]
        [InlineData("AHCN")]
        [InlineData("AHTR")]
        [InlineData("ACCN")]
        [InlineData("ACRH")]
        [InlineData("ASCN")]
        [InlineData("ASRP")]
        [InlineData("AFCN")]
        [InlineData("AFSC")]
        [InlineData("AFSN")]
        [InlineData("BCIR")]
        [InlineData("BREC")]
        [InlineData("FHGT")]
        [InlineData("FSLP")]
        public void Decode_Tier1Tag_DecodesTypedNamedFields_LowAndHighVersion(string tag)
        {
            foreach (string version in new[] { TgenEraVersions.Low(tag), TgenEraVersions.High(tag) })
            {
                TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.WithTypedTag(tag, version));
                TerrainNode node = SingleNode(doc);

                Assert.Equal(tag, node.Tag);
                Assert.Equal(version, node.Version);
                Assert.True(node.IsEditable, tag + "/" + version + " should be a typed editable node.");
                Assert.False(node.IsRawPreserved);
                Assert.False(node.IsDeadSkipped);
                Assert.NotEmpty(node.TypedFields);
                // The field names match the single-source descriptor table (no offset literals in decode).
                IReadOnlyList<TgenFieldDescriptor> descriptors = TgenFieldLayouts.For(tag, version);
                Assert.Equal(descriptors.Select(d => d.Name).ToArray(), node.TypedFields.Select(f => f.Name).ToArray());
            }
        }

        [Fact]
        [Trait("Category", "TgenDecode")]
        public void Decode_AHCN_BothVersions_ExposesOperationEnumAndHeightFloat()
        {
            foreach (var era in new[] { TgenEraVersions.SwgEmuEra, TgenEraVersions.InfinityEra })
            {
                TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.WithAffector("AHCN", era));
                TerrainNode node = SingleNode(doc);

                TerrainField op = node.TypedFields.Single(f => f.Name == "operation");
                TerrainField height = node.TypedFields.Single(f => f.Name == "height");
                Assert.Equal(TgenDisplayType.Enum32, op.DisplayType);
                Assert.Equal(TgenDisplayType.ScalarFloat, height.DisplayType);
                Assert.Equal("1", op.Value); // the synthesizer packs operation = 1.
            }
        }

        [Fact]
        [Trait("Category", "TgenDecode")]
        public void Decode_BCIR_LowVersionHasFeather_HighVersionHasFeather()
        {
            // Both observed BCIR arms are v0002 (feather-bearing). Assert the feather fields decode in both.
            foreach (var era in new[] { TgenEraVersions.SwgEmuEra, TgenEraVersions.InfinityEra })
            {
                TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.WithBoundary("BCIR", era));
                TerrainNode node = SingleNode(doc);
                Assert.Contains(node.TypedFields, f => f.Name == "radius");
                Assert.Contains(node.TypedFields, f => f.Name == "featherFunction");
                Assert.Contains(node.TypedFields, f => f.Name == "featherDistance");
            }
        }

        [Fact]
        [Trait("Category", "TgenDecode")]
        public void Decode_StableIds_DoNotDriftAcrossDecodeModes()
        {
            // Every node (typed/raw/dead/unknown) gets a physical-path stable id; ordinals include all
            // siblings so they do not drift (concern #14). Assert the four compositional siblings carry
            // distinct, non-empty stable ids reflecting their physical ordinal positions.
            TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.CompositionalLayer());
            TerrainLayer layer = doc.Layers[0];

            var ids = layer.Nodes.Select(n => n.StableIdPath).ToList();
            Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
            Assert.Equal(ids.Count, ids.Distinct().Count()); // all distinct — no ordinal collision/drift.
        }

        // ── Raw-fallback / DEAD-skip / negative battery (review concern #17) ──

        [Fact]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_UnknownTag_RawFallbackTagVersionHex_NeverThrows()
        {
            TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.WithUnknownTag());
            TerrainNode node = SingleNode(doc);

            Assert.Equal("ZZZZ", node.Tag);
            Assert.True(node.IsRawPreserved);
            Assert.False(node.IsEditable);
            Assert.Empty(node.TypedFields); // raw-preserved, NOT a parsed field list (concern #8).
            Assert.False(string.IsNullOrEmpty(node.RawHex));
        }

        [Fact]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_DeadTag_RecognizedAndSkipped_NotEditable()
        {
            TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.WithDeadTag());
            TerrainNode node = SingleNode(doc);

            Assert.Equal("BALL", node.Tag);
            Assert.True(node.IsDeadSkipped);
            Assert.False(node.IsRawPreserved); // a DEAD tag is NOT raw-fallback (D-03).
            Assert.False(node.IsEditable);
        }

        [Fact]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_UnknownFormVersion_RawFallbacksWholeChunk_NoPartialDecode()
        {
            // A known tag at an unrecognized version → raw-fallback the WHOLE chunk (D-02 / Pitfall 5).
            byte[] bytes = TgenFixtureSynthesizer.WithTypedTag("AHCN", "9999", new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
            TerrainDocument doc = TerrainDocument.FromBytes(bytes);
            TerrainNode node = SingleNode(doc);

            Assert.Equal("AHCN", node.Tag);
            Assert.Equal("9999", node.Version);
            Assert.True(node.IsRawPreserved);
            Assert.False(node.IsEditable);
        }

        [Fact]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_TruncatedKnownTag_RawFallbackNonEditable_NoOverRead()
        {
            TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.WithTruncatedKnownTag());
            TerrainNode node = SingleNode(doc);

            Assert.Equal("AHCN", node.Tag);
            Assert.True(node.IsRawPreserved); // consumed-length != payload-length → raw (concern #4).
            Assert.False(node.IsEditable);
        }

        [Fact]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_KnownTagWithTrailingBytes_RawFallbackNonEditable()
        {
            // AHCN v0000 layout is 8 bytes; supply 12 (trailing bytes) → consumed != payload → raw-fallback.
            byte[] payload = new byte[12];
            payload[0] = 1; // operation
            byte[] bytes = TgenFixtureSynthesizer.WithTypedTag("AHCN", TgenEraVersions.Low("AHCN"), payload);
            TerrainDocument doc = TerrainDocument.FromBytes(bytes);
            TerrainNode node = SingleNode(doc);

            Assert.True(node.IsRawPreserved);
            Assert.False(node.IsEditable);
        }

        [Fact]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_MissingLyrs_NavigatesPalettesOnly_NoLayers()
        {
            // MinimalTgen has no LYRS — decodes with zero layers, no throw (LYRS optional, Pitfall 3).
            TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.MinimalTgen());
            Assert.Empty(doc.Layers);
        }

        [Fact]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_NonTgenRoot_ThrowsTerrainParseException()
        {
            // A non-TGEN root is the one hard malformation that does NOT degrade to raw-preserve.
            byte[] notTerrain;
            using (var ms = new System.IO.MemoryStream())
            {
                var node = UtinniCoreDotNet.Formats.Iff.MutableIffNode.NewContainer("FORM", "PEFT");
                node.AddContainer("FORM", "0000");
                notTerrain = UtinniCoreDotNet.Formats.Iff.IffWriter.Write(
                    new UtinniCoreDotNet.Formats.Iff.MutableIffDocument(node));
            }
            var ex = Assert.Throws<TerrainParseException>(() => TerrainDocument.FromBytes(notTerrain));
            Assert.Equal(TerrainParseError.UnexpectedForm, ex.Kind);
        }

        [Fact]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_OnlyOneMgrp_DisambiguatesByLoadOrderNotTag_MarksAmbiguous()
        {
            // A TGEN with exactly ONE MGRP palette — bound to the expected next slot (Fractal) and marked
            // Ambiguous rather than guessing fractal-vs-bitmap (concern #3 / Pitfall 4).
            byte[] bytes = BuildTgenWithSingleMgrp();
            TerrainDocument doc = TerrainDocument.FromBytes(bytes);

            Assert.True(doc.Palettes.Fractal.Present);
            Assert.True(doc.Palettes.Fractal.Ambiguous);
            Assert.False(doc.Palettes.Bitmap.Present);
        }

        [Fact]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_TwoMgrp_BindsFractalThenBitmapByPosition()
        {
            byte[] bytes = BuildTgenWithTwoMgrp();
            TerrainDocument doc = TerrainDocument.FromBytes(bytes);

            Assert.True(doc.Palettes.Fractal.Present);
            Assert.False(doc.Palettes.Fractal.Ambiguous); // two present → no ambiguity.
            Assert.True(doc.Palettes.Bitmap.Present);
        }

        [Fact]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_DeadAdjacentToTypedSibling_BothDecode_NonDriftingIds()
        {
            // CompositionalLayer: AHCN (typed) adjacent to BALL (dead), ZZZZ (unknown), BCIR (typed).
            // The DEAD/unknown siblings must not corrupt the typed neighbours' decode or stable ids (#16).
            TerrainDocument doc = TerrainDocument.FromBytes(TgenFixtureSynthesizer.CompositionalLayer());
            TerrainLayer layer = doc.Layers[0];

            TerrainNode ahcn = layer.Nodes.Single(n => n.Tag == "AHCN");
            TerrainNode bcir = layer.Nodes.Single(n => n.Tag == "BCIR");
            Assert.True(ahcn.IsEditable);
            Assert.True(bcir.IsEditable);
            Assert.NotEqual(ahcn.StableIdPath, bcir.StableIdPath);
        }

        [Fact]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_RoundTripsByteExact_WhenUnedited()
        {
            // The held mutable DOM re-emits byte-identical on an unedited decode (Serialize is the Plan-03
            // roundtrip foundation, concern #12).
            byte[] input = TgenFixtureSynthesizer.CompositionalLayer();
            TerrainDocument doc = TerrainDocument.FromBytes(input);
            byte[] output = doc.Serialize();
            Assert.Equal(input, output);
        }

        // ── Local TGEN builders for the palette state-machine tests ──

        private static byte[] BuildTgenWithSingleMgrp()
        {
            var tgen = UtinniCoreDotNet.Formats.Iff.MutableIffNode.NewContainer("FORM", "TGEN");
            var body = tgen.AddContainer("FORM", "0000");
            var mgrp = body.AddContainer("FORM", "MGRP");
            AddFamilyLeaf(mgrp, 0, "fractal0");
            return UtinniCoreDotNet.Formats.Iff.IffWriter.Write(
                new UtinniCoreDotNet.Formats.Iff.MutableIffDocument(tgen));
        }

        private static byte[] BuildTgenWithTwoMgrp()
        {
            var tgen = UtinniCoreDotNet.Formats.Iff.MutableIffNode.NewContainer("FORM", "TGEN");
            var body = tgen.AddContainer("FORM", "0000");
            var first = body.AddContainer("FORM", "MGRP");
            AddFamilyLeaf(first, 0, "fractal0");
            var second = body.AddContainer("FORM", "MGRP");
            AddFamilyLeaf(second, 1, "bitmap0");
            return UtinniCoreDotNet.Formats.Iff.IffWriter.Write(
                new UtinniCoreDotNet.Formats.Iff.MutableIffDocument(tgen));
        }

        private static void AddFamilyLeaf(UtinniCoreDotNet.Formats.Iff.MutableIffNode paletteForm, int familyId, string name)
        {
            var bytes = new List<byte>();
            bytes.Add((byte)(familyId & 0xFF));
            bytes.Add((byte)((familyId >> 8) & 0xFF));
            bytes.Add((byte)((familyId >> 16) & 0xFF));
            bytes.Add((byte)((familyId >> 24) & 0xFF));
            bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(name));
            bytes.Add(0x00);
            paletteForm.AddLeaf("DATA", bytes.ToArray());
        }
    }
}
