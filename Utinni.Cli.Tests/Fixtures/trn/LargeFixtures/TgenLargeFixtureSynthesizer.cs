// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain/src/shared/generator/
// {TerrainGenerator,ShaderGroup,FloraGroup,RadialGroup,EnvironmentGroup,FractalGroup,BitmapGroup}.{cpp,h}
// (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. Per 20-RESEARCH.md "The Six
// Shared Palettes": the palettes load in a fixed order, each carries variable-length CString family names
// (NUL-terminated, IffPayloadCursor.ReadCString), and FractalGroup + BitmapGroup share the MGRP tag
// disambiguated by load-order position. No code, comments, identifier names, or test fixtures copied from
// any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UtinniCoreDotNet.Formats.Iff;

namespace Utinni.Cli.Tests.Fixtures.Trn.LargeFixtures
{
    /// <summary>
    /// Opt-in PALETTE-BEARING <c>FORM TGEN</c> fixtures for the palette-lineage-divergence coverage the
    /// committed ≤200-byte golden corpus cannot accommodate (review concern #11). Because palette family
    /// names are variable-length NUL-terminated <c>CString</c>s (see
    /// <see cref="UtinniCoreDotNet.Formats.Decoders.IffPayloadCursor.ReadCString"/>), a realistic
    /// ShaderGroup/FloraGroup/Fractal/Bitmap palette set EXCEEDS 200 bytes — so these fixtures are
    /// DELIBERATELY NOT subject to <see cref="TgenFixtureSynthesizer.MaxFixtureBytes"/> and are NEVER
    /// committed as golden blobs: <see cref="PaletteLineageTests"/> GENERATE them at test time (D-12/D-14
    /// preserved — the committed corpus stays tiny + real assets stay out).
    ///
    /// <para>The version words come from <see cref="TgenEraVersions"/> so these fixtures pin WITH the core
    /// matrix. The on-disk palette shape mirrors what <see cref="UtinniCoreDotNet.Formats.Decoders.TgenDecoder"/>
    /// reads: each palette is a <c>FORM &lt;tag&gt;</c> child of the TGEN body whose <c>DATA</c> leaves are
    /// <c>[int32 familyId][cstring name]</c> entries (one leaf per family). The two MGRP palettes (fractal
    /// then bitmap) are emitted in load-order position; the single-MGRP fixture emits exactly one so the
    /// decoder marks the bound slot <see cref="UtinniCoreDotNet.Formats.Terrain.TerrainPalette.Ambiguous"/>
    /// (concern #3).</para>
    /// </summary>
    public static class TgenLargeFixtureSynthesizer
    {
        // ── little-endian DATA scalar + CString primitives (Pitfall 6) ─────────

        private static byte[] Int32Le(int v) => new[]
        {
            (byte)(v & 0xFF),
            (byte)((v >> 8) & 0xFF),
            (byte)((v >> 16) & 0xFF),
            (byte)((v >> 24) & 0xFF),
        };

        private static byte[] CString(string s)
        {
            byte[] text = Encoding.ASCII.GetBytes(s ?? string.Empty);
            var result = new byte[text.Length + 1];
            Array.Copy(text, result, text.Length);
            result[text.Length] = 0x00; // NUL terminator
            return result;
        }

        private static byte[] Family(int familyId, string name)
        {
            using (var ms = new MemoryStream())
            {
                byte[] id = Int32Le(familyId);
                ms.Write(id, 0, id.Length);
                byte[] n = CString(name);
                ms.Write(n, 0, n.Length);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Adds a palette <c>FORM &lt;tag&gt;</c> (e.g. SGRP/FGRP/MGRP) under the TGEN body with one
        /// <c>DATA</c> leaf per (familyId,name) entry — the exact shape <see cref="TgenDecoder.ReadFamilies"/>
        /// reads (DATA leaves are DIRECT children of the palette form; the decoder's family reader does not
        /// descend through a version sub-form). <paramref name="version"/> is consumed only to assert the
        /// caller is pinning against a real era version (it is not a separate on-disk form for palettes).
        /// </summary>
        private static MutableIffNode AddPalette(
            MutableIffNode body, string tag, string version, IReadOnlyList<(int Id, string Name)> families)
        {
            if (string.IsNullOrEmpty(version)) throw new ArgumentException("palette version required", nameof(version));
            MutableIffNode palette = body.AddContainer("FORM", tag);
            foreach (var f in families)
                palette.AddLeaf("DATA", Family(f.Id, f.Name));
            return palette;
        }

        // A realistic, name-heavy family set (long CString names push the fixture well past 200 bytes).
        private static IReadOnlyList<(int, string)> ShaderFamilies() => new[]
        {
            (1, "tatooine_desert_sand_fine"),
            (2, "tatooine_desert_rock_cracked"),
            (3, "tatooine_canyon_shadow_blend"),
            (4, "naboo_swamp_mud_wet_overlay"),
            (5, "corellia_grassland_temperate"),
        };

        private static IReadOnlyList<(int, string)> FloraFamilies() => new[]
        {
            (10, "flora_desert_shrub_dry_small"),
            (11, "flora_swamp_reed_tall_cluster"),
            (12, "flora_forest_fern_understory"),
            (13, "flora_grassland_wildflower_mix"),
        };

        private static IReadOnlyList<(int, string)> FractalFamilies() => new[]
        {
            (100, "fractal_dune_ridges_primary"),
            (101, "fractal_canyon_erosion_detail"),
        };

        private static IReadOnlyList<(int, string)> BitmapFamilies() => new[]
        {
            (200, "bitmap_height_basin_lowlands"),
            (201, "bitmap_height_plateau_uplands"),
        };

        /// <summary>
        /// A palette-rich <c>FORM TGEN</c>: ShaderGroup (high SGRP version) + FloraGroup (high FGRP version)
        /// + BOTH MGRP palettes (fractal then bitmap by load-order position), each with multiple
        /// CString-named families. EXCEEDS the ≤200-byte committed-golden cap (#11). <paramref name="era"/>
        /// selects the per-tag versions (low = <see cref="TgenEraVersions.SwgEmuEra"/>, high =
        /// <see cref="TgenEraVersions.InfinityEra"/>).
        /// </summary>
        public static byte[] FullPaletteSet(IReadOnlyDictionary<string, string> era)
        {
            if (era == null) throw new ArgumentNullException(nameof(era));
            MutableIffNode tgen = MutableIffNode.NewContainer("FORM", "TGEN");
            MutableIffNode body = tgen.AddContainer("FORM", "0000");

            AddPalette(body, "SGRP", era["SGRP"], ShaderFamilies());
            AddPalette(body, "FGRP", era["FGRP"], FloraFamilies());
            // Both MGRP palettes: fractal first, bitmap second (positional disambiguation, Pitfall 4).
            AddPalette(body, "MGRP", era["MGRP"], FractalFamilies());
            AddPalette(body, "MGRP", era["MGRP"], BitmapFamilies());

            // A trailing LYRS so the fixture also carries a layer alongside the palettes.
            MutableIffNode lyrs = body.AddContainer("FORM", "LYRS");
            lyrs.AddContainer("FORM", era["LAYR"]);
            return Emit(tgen);
        }

        /// <summary>
        /// A <c>FORM TGEN</c> with EXACTLY ONE MGRP palette present (no second MGRP) — the decoder cannot
        /// classify it fractal-vs-bitmap by position, so it binds the Fractal slot and marks it
        /// <see cref="UtinniCoreDotNet.Formats.Terrain.TerrainPalette.Ambiguous"/> (concern #3). Still
        /// name-heavy (palette CStrings) so it exceeds the committed cap.
        /// </summary>
        public static byte[] SingleMgrpAmbiguous(IReadOnlyDictionary<string, string> era)
        {
            if (era == null) throw new ArgumentNullException(nameof(era));
            MutableIffNode tgen = MutableIffNode.NewContainer("FORM", "TGEN");
            MutableIffNode body = tgen.AddContainer("FORM", "0000");

            AddPalette(body, "SGRP", era["SGRP"], ShaderFamilies());
            AddPalette(body, "MGRP", era["MGRP"], FractalFamilies()); // ONE MGRP only → Ambiguous
            return Emit(tgen);
        }

        /// <summary>
        /// A <c>FORM TGEN</c> that SKIPS an earlier optional palette slot (no SGRP) but DOES carry a later
        /// one (FGRP + both MGRP) — proving the absent earlier slot does NOT shift the later slots (the
        /// optional-slot state machine, concern #3). Name-heavy → exceeds the committed cap.
        /// </summary>
        public static byte[] MissingEarlierSlot(IReadOnlyDictionary<string, string> era)
        {
            if (era == null) throw new ArgumentNullException(nameof(era));
            MutableIffNode tgen = MutableIffNode.NewContainer("FORM", "TGEN");
            MutableIffNode body = tgen.AddContainer("FORM", "0000");

            // NO SGRP slot. FGRP present; both MGRP present.
            AddPalette(body, "FGRP", era["FGRP"], FloraFamilies());
            AddPalette(body, "MGRP", era["MGRP"], FractalFamilies());
            AddPalette(body, "MGRP", era["MGRP"], BitmapFamilies());
            return Emit(tgen);
        }

        private static byte[] Emit(MutableIffNode root)
        {
            return IffWriter.Write(new MutableIffDocument(root));
        }
    }
}
