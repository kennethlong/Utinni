# Utinni.Mcp.Tests/Fixtures/trn — Terrain (.trn) Fixture Provenance

These are the MINIMAL synthesized `FORM TGEN` (`.trn`) assets the net10
`TerrainReadToolCliIntegrationTests` copy into a temp resolved-root and drive the
freshly-built x86 Release `utinni-cli.exe` against, proving `summarize_terrain` shells
`decode-iff` end-to-end across BOTH lineages (Plan 20-04, PROD-W2-TRN-04 / D-10).

**Why they are committed (not generated at test time):** the net10 test project CANNOT
reference the net472 `TgenFixtureSynthesizer` in `Utinni.Cli.Tests` (cross-TFM; the
synthesizer pulls in the x86 `UtinniCoreDotNet`). Mirroring the Plan-14 `Fixtures/README.md`
convention, the bytes are GENERATED ONCE and committed. The generator-of-record is
`.planning/phases/20-terrain-trn-codec-verbs-mcp/gen-trn-fixtures.ps1` (run under 32-bit
PowerShell so the x86 `UtinniCoreDotNet.dll` loads). Re-run it only if the synthesizer's TGEN
shape changes; then re-commit the bytes.

Each fixture was validated post-generation through the built `utinni-cli decode-iff` — the
low/high fixtures emit `type:terrain`/`rootType:TGEN` (exit 0); the malformed blob is rejected
(exit 2), which `CliResultMapper` surfaces as an MCP tool error (review concern #5).

| Fixture | Shape | FORM versions | decode-iff outcome |
|---------|-------|---------------|--------------------|
| `terrain_low.trn` | `TGEN → 0000 → LYRS → LAYR(0003) → BREC → DATA` | **BREC 0002** (SWGEmu-era low arm) | `type:terrain`, BREC raw-falls-back (no typed v0002 descriptor) — exit 0 |
| `terrain_high.trn` | same, with feather fields | **BREC 0003** (Infinity-era high arm — the ONE observed lineage divergence) | `type:terrain`, BREC typed-decodes — exit 0 |
| `terrain_malformed.trn` | 8 non-IFF bytes | n/a | `MalformedFourCc` error envelope — exit 2 → MCP tool error |

**Lineage note:** BREC (SWGEmu 0002 / SWG Infinity 0003) is the single per-tag version
divergence observed in Wave 0 (Plan 20-01); every other Tier-1 tag ships identical versions
across both clients. These two fixtures therefore genuinely exercise the low+high lineage
arms through the SAME `decode-iff` TGEN branch. Real client assets stay OUT of the committed
corpus (D-14).
