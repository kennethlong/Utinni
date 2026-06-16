// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain/src/shared/generator/
// {TerrainGenerator,TerrainGeneratorType,TerrainGeneratorLoader,AffectorHeight,AffectorShader,Boundary,Filter,
//  ShaderGroup,FractalGroup,BitmapGroup}.{cpp,h,def} (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85
// public standard. Per 20-RESEARCH.md § "SWGEmu vs Restoration version dispatch": the lineage difference is
// expressed entirely as per-FORM version numbers dispatched in a switch at every level. No code, comments,
// identifier names, or test fixtures copied from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;

namespace Utinni.Cli.Tests.Fixtures.Trn
{
    /// <summary>
    /// The SINGLE source of the per-tag FORM version numbers the <see cref="TgenFixtureSynthesizer"/>
    /// reads when it hand-emits a low-version ("SWGEmu-era") and a high-version ("Restoration-era")
    /// fixture for every Tier-1 tag.
    ///
    /// <para><b>ASSUMED — pending Plan 04 Task 3 real-asset pin (D-13 / D-14).</b> The numbers below are
    /// the CURRENTLY-ASSUMED low/high versions seeded from 20-RESEARCH.md § "SWGEmu vs Restoration
    /// version dispatch" + § "The Six Shared Palettes" + the Tag Taxonomy. They are NOT confirmed facts.
    /// Plan 01 Task 3 (the Wave-0 maintainer checkpoint) observes the REAL shipped FORM versions of one
    /// small SWGEmu and one small Restoration <c>.trn</c>; the executor then either pins these constants
    /// to the observed numbers or annotates them as still-assumed (review concern #1). The FINAL
    /// DEC-C3 pin/confirm is Plan 04 Task 3 — which edits ONLY this file, so every downstream fixture
    /// stays grounded without touching fixture-build code.</para>
    ///
    /// <para><b>Low = SWGEmu-era</b> (older clients, lower FORM versions); <b>High = Restoration-era</b>
    /// (newer clients, higher FORM versions). Where the research records the same version for both
    /// lineages (most affectors/filters/boundaries top out at <c>0000</c>/<c>0001</c>) the low/high pair
    /// still differs by at least one version step for the tags whose payload diverges across the range
    /// (e.g. <c>BCIR</c> v0000 no-feather vs v0001+ feather; <c>ASCN</c> v0000 vs v0001 feather-override),
    /// so the both-lineage divergence path is exercised. For tags that genuinely only ship one version,
    /// the low and high entries are equal — the fixture matrix still emits both arms (proving the
    /// version-argument plumbing) and the equality documents "no observed lineage drift for this tag,
    /// pending the real-asset pin."</para>
    /// </summary>
    public static class TgenEraVersions
    {
        // Version FORM names are the 4-char zero-padded decimal strings SWG uses as the per-form
        // version sub-type tag (e.g. "0000".."0008"). The synthesizer composes these as the
        // version-FORM sub-type under each typed tag's outer FORM.

        /// <summary>
        /// Low ("SWGEmu-era") per-tag FORM versions. ASSUMED — pending Plan 04 Task 3 pin.
        /// Covers every Tier-1 tag, every palette tag, and the structural <c>LAYR</c> / <c>TGEN</c> tags.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> SwgEmuEra =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Structural
                { "TGEN", "0000" }, // RESEARCH: top-level FORM has a single version 0000
                { "LAYR", "0000" }, // RESEARCH: LAYR versions 0000..0004; low arm = 0000

                // Tier-1 affectors (RESEARCH Tag Taxonomy + version dispatch :247-255)
                { "AHCN", "0000" }, // AffectorHeightConstant
                { "AHTR", "0000" }, // AffectorHeightTerrace
                { "ACCN", "0000" }, // AffectorColorConstant
                { "ACRH", "0000" }, // AffectorColorRampHeight
                { "ASCN", "0000" }, // AffectorShaderConstant (v0000 = no feather-override)
                { "ASRP", "0000" }, // AffectorShaderReplace
                { "AFCN", "0000" }, // FloraStaticCollidableConstant
                { "AFSC", "0000" }, // FloraStaticCollidableConstant (alt FourCC)
                { "AFSN", "0000" }, // FloraStaticNonCollidableConstant

                // Tier-1 boundaries
                { "BCIR", "0000" }, // BoundaryCircle (v0000 = NO feather)
                { "BREC", "0000" }, // BoundaryRectangle

                // Tier-1 filters
                { "FHGT", "0000" }, // FilterHeight
                { "FSLP", "0000" }, // FilterSlope

                // Six shared read-only palettes (RESEARCH § palettes; low arm = lowest shipped version)
                { "SGRP", "0000" }, // Shader  (0000..0006)
                { "FGRP", "0001" }, // Flora   (0001..0008 — widest range; low arm = 0001)
                { "RGRP", "0000" }, // Radial  (0000..0004)
                { "EGRP", "0000" }, // Environment (0000..0002)
                { "MGRP", "0000" }, // Fractal + Bitmap both share MGRP at 0000 (disambiguated by load order, not version)
            };

        /// <summary>
        /// High ("Restoration-era") per-tag FORM versions. ASSUMED — pending Plan 04 Task 3 pin.
        /// Covers every Tier-1 tag, every palette tag, and the structural <c>LAYR</c> / <c>TGEN</c> tags.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> RestorationEra =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Structural
                { "TGEN", "0000" }, // single version — no lineage drift on the top-level FORM
                { "LAYR", "0004" }, // RESEARCH: LAYR tops out at 0004 (invertFilters/expanded/notes added progressively)

                // Tier-1 affectors — most top out at 0001 (v0001 typically adds feather-override/feather-function)
                { "AHCN", "0001" },
                { "AHTR", "0001" },
                { "ACCN", "0001" },
                { "ACRH", "0001" },
                { "ASCN", "0001" }, // v0001 = feather-override present (divergence path)
                { "ASRP", "0001" },
                { "AFCN", "0001" },
                { "AFSC", "0001" },
                { "AFSN", "0001" },

                // Tier-1 boundaries
                { "BCIR", "0001" }, // v0001 = feather present (divergence path)
                { "BREC", "0001" },

                // Tier-1 filters
                { "FHGT", "0001" },
                { "FSLP", "0001" },

                // Six shared read-only palettes — high arm = highest shipped version in the research range
                { "SGRP", "0006" }, // Shader  tops out at 0006
                { "FGRP", "0008" }, // Flora   tops out at 0008
                { "RGRP", "0004" }, // Radial  tops out at 0004
                { "EGRP", "0002" }, // Environment tops out at 0002
                { "MGRP", "0000" }, // Fractal + Bitmap only ship 0000 — equal arms document "no observed drift, pending pin"
            };

        /// <summary>
        /// The Tier-1 tag set (D-01) the synthesizer enumerates for the both-lineage matrix:
        /// affectors AHCN/AHTR (height), ACCN/ACRH (color), ASCN/ASRP (shader),
        /// AFCN/AFSC/AFSN (flora); boundaries BCIR/BREC; filters FHGT/FSLP.
        /// </summary>
        public static readonly IReadOnlyList<string> Tier1Tags = new[]
        {
            "AHCN", "AHTR", "ACCN", "ACRH", "ASCN", "ASRP", "AFCN", "AFSC", "AFSN",
            "BCIR", "BREC", "FHGT", "FSLP",
        };

        /// <summary>The six shared palette FORM tags in their fixed positional load order (D-04).</summary>
        public static readonly IReadOnlyList<string> PaletteTagsInLoadOrder = new[]
        {
            "SGRP", // 1 Shader
            "FGRP", // 2 Flora
            "RGRP", // 3 Radial
            "EGRP", // 4 Environment
            "MGRP", // 5 Fractal  (shares MGRP tag with Bitmap)
            "MGRP", // 6 Bitmap   (second MGRP — disambiguated by load order, not tag)
        };

        /// <summary>Returns the low ("SWGEmu-era") version for a tag, or throws if it is unmapped.</summary>
        public static string Low(string tag)
        {
            if (SwgEmuEra.TryGetValue(tag, out var v)) return v;
            throw new KeyNotFoundException("No SwgEmuEra version mapped for tag '" + tag + "'.");
        }

        /// <summary>Returns the high ("Restoration-era") version for a tag, or throws if it is unmapped.</summary>
        public static string High(string tag)
        {
            if (RestorationEra.TryGetValue(tag, out var v)) return v;
            throw new KeyNotFoundException("No RestorationEra version mapped for tag '" + tag + "'.");
        }
    }
}
