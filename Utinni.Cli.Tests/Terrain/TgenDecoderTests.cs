// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain (SOE/Bootprint, All
// Rights Reserved) and the EA-IFF-85 public standard. No code, comments, identifier names, or test fixtures
// copied from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using CommandLine;
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
        // Wave-1 Skip stubs (PROD-W2-TRN-01/02) — COLLECTED, reported Skipped.
        // ─────────────────────────────────────────────────────────────────────

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenDecode")]
        public void Decode_MinimalTgen_NavigatesTopLevelFormNoPalettesNoLayers()
        {
            // TODO Wave 1: decode MinimalTgen(); assert TGEN root + zero palettes + zero layers.
        }

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenDecode")]
        public void Decode_TgenTree_NavigatesLayersBoundariesFiltersAffectorsAndSubLayers()
        {
            // TODO Wave 1: assert TGEN → Layers → Boundaries/Filters/Affectors/sub-layers navigation (TRN-01).
        }

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenDecode")]
        public void Decode_SixPalettes_ResolvesFamilyIdToNamePositionallyInLoadOrder()
        {
            // TODO Wave 1: decode the six read-only palettes positionally (D-04), incl. the MGRP collision (TRN-01).
        }

        [Theory(Skip = WaveOne)]
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
            // TODO Wave 1: decode the low + high fixture for each Tier-1 tag; assert named-field values (TRN-02).
            _ = tag;
        }

        // ── Raw-fallback / DEAD-skip / negative battery (review concern #17) ──

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_UnknownTag_RawFallbackTagVersionHex_NeverThrows()
        {
            // TODO Wave 1: WithUnknownTag() → {tag, version, hex} raw-fallback, never a hard failure (TRN-02).
        }

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_DeadTag_RecognizedAndSkipped_NotEditable()
        {
            // TODO Wave 1: WithDeadTag() → "obsolete, ignored", recognized-and-skipped, NOT raw-editable (D-03).
        }

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_UnknownFormVersion_RawFallbacksWholeChunk_NoPartialDecode()
        {
            // TODO Wave 1: unrecognized FORM version → raw-fallback the whole chunk (D-02 / Pitfall 5).
        }

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_TruncatedKnownTag_RawFallbackNonEditable_NoOverRead()
        {
            // TODO Wave 1: WithTruncatedKnownTag() → raw-fallback / non-editable, never over-read (concern #4/#17).
        }

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_KnownTagWithTrailingBytes_RawFallbackNonEditable()
        {
            // TODO Wave 1: known tag with trailing bytes past its field list → raw-fallback (concern #17).
        }

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_MissingLyrs_NavigatesPalettesOnly_NoLayers()
        {
            // TODO Wave 1: a TGEN with no LYRS still decodes (LYRS optional — Pitfall 3 / concern #17).
        }

        [Theory(Skip = WaveOne)]
        [Trait("Category", "TgenRawFallback")]
        [InlineData("SGRP")]
        [InlineData("FGRP")]
        [InlineData("RGRP")]
        [InlineData("EGRP")]
        [InlineData("MGRP")]
        public void Decode_MissingPaletteSlot_RemainingPalettesResolvePositionally(string missingPalette)
        {
            // TODO Wave 1: drop each palette slot; the remaining palettes still resolve in load order (concern #17).
            _ = missingPalette;
        }

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_OnlyOneMgrp_DisambiguatesByLoadOrderNotTag()
        {
            // TODO Wave 1: a TGEN with only ONE MGRP — load-order disambiguation, not tag lookup (Pitfall 4 / #17).
        }

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_DeadAdjacentToEditedSibling_SiblingByteExact()
        {
            // TODO Wave 1: CompositionalLayer() — a DEAD/unknown sibling must not corrupt an edited typed neighbour (#16).
        }

        [Fact(Skip = WaveOne)]
        [Trait("Category", "TgenRawFallback")]
        public void Decode_MalformedCString_DegradesGracefully_NoThrow()
        {
            // TODO Wave 1: a malformed (unterminated) CString in a name field degrades gracefully (concern #17).
        }
    }
}
