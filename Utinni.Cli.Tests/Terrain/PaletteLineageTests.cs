// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain (SOE/Bootprint, All
// Rights Reserved) and the EA-IFF-85 public standard. No code, comments, identifier names, or test fixtures
// copied from any reference source. Implementation original to Utinni under MIT.

using System.Linq;
using UtinniCoreDotNet.Formats.Terrain;
using Utinni.Cli.Tests.Fixtures.Trn;
using Utinni.Cli.Tests.Fixtures.Trn.LargeFixtures;
using Xunit;

namespace Utinni.Cli.Tests.Terrain
{
    /// <summary>
    /// Palette-lineage-divergence coverage over the OPT-IN <see cref="TgenLargeFixtureSynthesizer"/> set
    /// (review concern #11). The committed ≤200-byte golden corpus cannot hold name-heavy palettes
    /// (variable-length CString family names), so these palette-bearing fixtures are GENERATED at test
    /// time (not committed — D-12/D-14) and assert: positional palette assignment (fractal-vs-bitmap by
    /// load order, Pitfall 4), family-name resolution, the single-MGRP <c>Ambiguous</c> flag (concern #3),
    /// the absent-earlier-slot state machine, AND whole-file byte identity on roundtrip across BOTH
    /// lineages (low SWGEmu-era + high Infinity-era versions from <see cref="TgenEraVersions"/>).
    ///
    /// <para>Filter trait: <c>PaletteLineage</c>.</para>
    /// </summary>
    public sealed class PaletteLineageTests
    {
        // The committed-golden budget the LARGE fixtures must exceed (proving why they are opt-in, #11).
        private const int CommittedCap = TgenFixtureSynthesizer.MaxFixtureBytes;

        public static System.Collections.Generic.IEnumerable<object[]> BothEras()
        {
            yield return new object[] { "SWGEmu", TgenEraVersions.SwgEmuEra };
            yield return new object[] { "Infinity", TgenEraVersions.InfinityEra };
        }

        // ── these large fixtures EXCEED the committed ≤200-byte cap (#11) ───────

        [Theory]
        [Trait("Category", "PaletteLineage")]
        [MemberData(nameof(BothEras))]
        public void FullPaletteSet_ExceedsCommittedGoldenCap(string _, System.Collections.Generic.IReadOnlyDictionary<string, string> era)
        {
            byte[] bytes = TgenLargeFixtureSynthesizer.FullPaletteSet(era);
            Assert.True(bytes.Length > CommittedCap,
                "palette-bearing large fixtures must exceed the " + CommittedCap +
                "-byte committed-golden cap — that is WHY they live in the opt-in LargeFixtures set (#11).");
        }

        // ── positional palette assignment + family-name resolution (both lineages) ──

        [Theory]
        [Trait("Category", "PaletteLineage")]
        [MemberData(nameof(BothEras))]
        public void FullPaletteSet_PositionalAssignment_And_FamilyNames(string _, System.Collections.Generic.IReadOnlyDictionary<string, string> era)
        {
            TerrainDocument doc = TerrainDocument.FromBytes(TgenLargeFixtureSynthesizer.FullPaletteSet(era));
            TerrainPalettes p = doc.Palettes;

            // Shader (slot 1) + Flora (slot 2) present and name-resolved.
            Assert.True(p.Shader.Present);
            Assert.False(p.Shader.Ambiguous);
            Assert.Contains(p.Shader.Families, f => f.Name == "tatooine_desert_sand_fine");
            Assert.True(p.Flora.Present);
            Assert.Contains(p.Flora.Families, f => f.Name == "flora_swamp_reed_tall_cluster");

            // Two MGRP palettes → fractal (slot 5) THEN bitmap (slot 6) by load-order position, NOT guessed.
            Assert.True(p.Fractal.Present);
            Assert.False(p.Fractal.Ambiguous);
            Assert.Contains(p.Fractal.Families, f => f.Name == "fractal_dune_ridges_primary");
            Assert.True(p.Bitmap.Present);
            Assert.Contains(p.Bitmap.Families, f => f.Name == "bitmap_height_basin_lowlands");

            // familyIds preserved verbatim (never renumbered — D-04).
            Assert.Contains(p.Shader.Families, f => f.FamilyId == 1);
            Assert.Contains(p.Bitmap.Families, f => f.FamilyId == 201);
        }

        // ── single-MGRP → Ambiguous (concern #3) ────────────────────────────────

        [Theory]
        [Trait("Category", "PaletteLineage")]
        [MemberData(nameof(BothEras))]
        public void SingleMgrp_BindsFractalSlot_MarkedAmbiguous(string _, System.Collections.Generic.IReadOnlyDictionary<string, string> era)
        {
            TerrainDocument doc = TerrainDocument.FromBytes(TgenLargeFixtureSynthesizer.SingleMgrpAmbiguous(era));
            TerrainPalettes p = doc.Palettes;

            // A lone MGRP cannot be classified fractal-vs-bitmap by position → bound to Fractal + Ambiguous.
            Assert.True(p.Fractal.Present);
            Assert.True(p.Fractal.Ambiguous);
            Assert.False(p.Bitmap.Present);
            // The uniquely-tagged earlier slot is unaffected.
            Assert.True(p.Shader.Present);
        }

        // ── absent earlier slot does NOT shift later slots (state machine, #3) ──

        [Theory]
        [Trait("Category", "PaletteLineage")]
        [MemberData(nameof(BothEras))]
        public void MissingEarlierSlot_DoesNotShiftLaterSlots(string _, System.Collections.Generic.IReadOnlyDictionary<string, string> era)
        {
            TerrainDocument doc = TerrainDocument.FromBytes(TgenLargeFixtureSynthesizer.MissingEarlierSlot(era));
            TerrainPalettes p = doc.Palettes;

            // SGRP omitted → Shader slot absent; Flora still binds its OWN (slot-2) tag, not shifted up.
            Assert.False(p.Shader.Present);
            Assert.True(p.Flora.Present);
            Assert.Contains(p.Flora.Families, f => f.Name == "flora_forest_fern_understory");
            // Both MGRP still resolve to fractal + bitmap.
            Assert.True(p.Fractal.Present);
            Assert.True(p.Bitmap.Present);
        }

        // ── whole-file byte identity on roundtrip (both lineages) ───────────────

        [Theory]
        [Trait("Category", "PaletteLineage")]
        [MemberData(nameof(BothEras))]
        public void LargeFixtures_RoundtripByteIdentical_BothLineages(string _, System.Collections.Generic.IReadOnlyDictionary<string, string> era)
        {
            AssertRoundtripByteExact(TgenLargeFixtureSynthesizer.FullPaletteSet(era));
            AssertRoundtripByteExact(TgenLargeFixtureSynthesizer.SingleMgrpAmbiguous(era));
            AssertRoundtripByteExact(TgenLargeFixtureSynthesizer.MissingEarlierSlot(era));
        }

        private static void AssertRoundtripByteExact(byte[] input)
        {
            TerrainDocument doc = TerrainDocument.FromBytes(input);
            byte[] roundtripped = doc.Serialize();
            TerrainDocument reparsed = TerrainDocument.FromBytes(roundtripped);
            Assert.NotNull(reparsed);
            Assert.True(input.Length == roundtripped.Length && input.SequenceEqual(roundtripped),
                "whole-file roundtrip must be byte-identical (palette CStrings re-emit verbatim)");
        }
    }
}
