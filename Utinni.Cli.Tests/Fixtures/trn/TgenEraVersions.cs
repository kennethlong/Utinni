// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain/src/shared/generator/
// {TerrainGenerator,TerrainGeneratorType,TerrainGeneratorLoader,AffectorHeight,AffectorShader,Boundary,Filter,
//  ShaderGroup,FractalGroup,BitmapGroup}.{cpp,h,def} (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85
// public standard. Per 20-RESEARCH.md the lineage difference is expressed as per-FORM version numbers
// dispatched in a switch at every level. No code, comments, identifier names, or test fixtures copied from any
// reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;

namespace Utinni.Cli.Tests.Fixtures.Trn
{
    /// <summary>
    /// The SINGLE source of the per-tag FORM version numbers the <see cref="TgenFixtureSynthesizer"/>
    /// reads when it hand-emits a low-version ("SWGEmu-era") and a high-version ("Infinity-era")
    /// fixture for every Tier-1 tag.
    ///
    /// <para><b>OBSERVED (Plan 01 Task 3, 2026-06-16) — grounded against real client assets.</b> Per the
    /// maintainer Wave-0 directive ("use SWG Infinity and SWGEmu, no Restoration") the two real lineages
    /// observed are <b>SWGEmu</b> (classic clients) and <b>SWG Infinity</b> (newer clients). Both ship the
    /// SAME <c>PTAT/0014</c> terrain format; the per-tag FORM versions below were read by dogfooding
    /// <c>utinni-cli parse-tre</c> + <c>inspect-iff</c> over real <c>.trn</c> records extracted from
    /// SWGEmu <c>patch_00.tre</c> (tutorial / tatooine / naboo / corellia / dathooine) and SWG Infinity
    /// <c>mtg_planets.tre</c> (taanab / mustafar). Neither client's <c>.trn</c> payload was v6000+/encrypted
    /// — both decoded cleanly (concern #15 did not trigger). Real assets were NOT committed to the corpus
    /// (D-14); the observation note lives in the Plan 20-01 SUMMARY.</para>
    ///
    /// <para><b>Key finding:</b> SWGEmu and SWG Infinity ship IDENTICAL per-tag FORM versions for every tag
    /// observed in both — there is NO version-word lineage divergence between these two clients. The only
    /// intra-corpus drift observed is <c>BREC</c> (v0002 low .. v0003 high), kept as a genuine low/high pair
    /// so the version-divergence path stays exercised. For every other Tier-1 tag the low and high entries
    /// are equal, documenting "no observed lineage drift." The authoritative DEC-C3 pin/confirm still lands
    /// at Plan 04 Task 3 (which edits ONLY this file) — this is the Wave-0 grounding.</para>
    ///
    /// <para><b>Still ASSUMED (not observed):</b> <c>AFCN</c> did not appear in any sampled planet of either
    /// client; its value below is the pre-observation assumption, annotated inline. Plan 04 Task 3 should
    /// confirm it or fall back to raw-only coverage. Note this file pins only which real client the era
    /// constants are GROUNDED in; the broader format-capability support range (SWGEmu 0004/0005/0006 + newer
    /// 5000/6000/COT2000 per AGENTS.md) is unchanged.</para>
    /// </summary>
    public static class TgenEraVersions
    {
        // Version FORM names are the 4-char zero-padded decimal strings SWG uses as the per-form
        // version sub-type tag (e.g. "0000".."0008"). The synthesizer composes these as the
        // version-FORM sub-type under each typed tag's outer FORM.

        /// <summary>
        /// Low ("SWGEmu-era") per-tag FORM versions. OBSERVED against real SWGEmu <c>.trn</c> assets
        /// (Plan 01 Task 3) except where annotated ASSUMED. Covers every Tier-1 tag, every palette tag,
        /// and the structural <c>LAYR</c> / <c>TGEN</c> tags.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> SwgEmuEra =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Structural (OBSERVED)
                { "TGEN", "0000" }, // OBSERVED: top-level generator FORM ships version 0000
                { "LAYR", "0003" }, // OBSERVED: LAYR ships 0003 in real SWGEmu planets

                // Tier-1 affectors (OBSERVED, except AFCN)
                { "AHCN", "0000" }, // OBSERVED AffectorHeightConstant
                { "AHTR", "0004" }, // OBSERVED AffectorHeightTerrace
                { "ACCN", "0000" }, // OBSERVED AffectorColorConstant
                { "ACRH", "0000" }, // OBSERVED AffectorColorRampHeight
                { "ASCN", "0001" }, // OBSERVED AffectorShaderConstant
                { "ASRP", "0001" }, // OBSERVED AffectorShaderReplace
                { "AFCN", "0000" }, // ASSUMED — not observed in any sampled planet (Plan 04 Task 3 confirm)
                { "AFSC", "0004" }, // OBSERVED FloraStaticCollidableConstant
                { "AFSN", "0004" }, // OBSERVED FloraStaticNonCollidableConstant

                // Tier-1 boundaries (OBSERVED)
                { "BCIR", "0002" }, // OBSERVED BoundaryCircle
                { "BREC", "0002" }, // OBSERVED BoundaryRectangle low arm (Infinity taanab ships 0002)

                // Tier-1 filters (OBSERVED)
                { "FHGT", "0002" }, // OBSERVED FilterHeight
                { "FSLP", "0002" }, // OBSERVED FilterSlope

                // Six shared read-only palettes (OBSERVED)
                { "SGRP", "0006" }, // OBSERVED Shader palette
                { "FGRP", "0008" }, // OBSERVED Flora palette
                { "RGRP", "0003" }, // OBSERVED Radial palette
                { "EGRP", "0002" }, // OBSERVED Environment palette
                { "MGRP", "0000" }, // OBSERVED Fractal + Bitmap both share MGRP at 0000 (disambiguated by load order)
            };

        /// <summary>
        /// High ("Infinity-era") per-tag FORM versions. OBSERVED against real SWG Infinity <c>.trn</c>
        /// assets (Plan 01 Task 3) except where annotated ASSUMED. Identical to <see cref="SwgEmuEra"/>
        /// for every observed tag EXCEPT <c>BREC</c> (Infinity ships a higher 0003 arm) — the one genuine
        /// version-divergence pair. Covers every Tier-1 tag, every palette tag, and <c>LAYR</c>/<c>TGEN</c>.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> InfinityEra =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Structural (OBSERVED — identical to SWGEmu)
                { "TGEN", "0000" },
                { "LAYR", "0003" },

                // Tier-1 affectors (OBSERVED — identical to SWGEmu; no version drift observed)
                { "AHCN", "0000" },
                { "AHTR", "0004" },
                { "ACCN", "0000" },
                { "ACRH", "0000" },
                { "ASCN", "0001" },
                { "ASRP", "0001" },
                { "AFCN", "0000" }, // ASSUMED — not observed (mirrors SwgEmuEra)
                { "AFSC", "0004" },
                { "AFSN", "0004" },

                // Tier-1 boundaries — BCIR identical; BREC is the one OBSERVED divergence (Infinity 0003 high)
                { "BCIR", "0002" },
                { "BREC", "0003" }, // OBSERVED high arm — divergence pair vs SwgEmuEra 0002

                // Tier-1 filters (OBSERVED — identical to SWGEmu)
                { "FHGT", "0002" },
                { "FSLP", "0002" },

                // Six shared read-only palettes (OBSERVED — identical to SWGEmu)
                { "SGRP", "0006" },
                { "FGRP", "0008" },
                { "RGRP", "0003" },
                { "EGRP", "0002" },
                { "MGRP", "0000" },
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

        /// <summary>Returns the high ("Infinity-era") version for a tag, or throws if it is unmapped.</summary>
        public static string High(string tag)
        {
            if (InfinityEra.TryGetValue(tag, out var v)) return v;
            throw new KeyNotFoundException("No InfinityEra version mapped for tag '" + tag + "'.");
        }
    }
}
