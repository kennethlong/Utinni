---
phase: 20-terrain-trn-codec-verbs-mcp
plan: 04
subsystem: terrain-trn-codec
tags: [terrain, trn, tgen, mcp, mcp-oop, palette-lineage, dec-c3, byte-exact]
requires:
  - "utinni-cli decode-iff TGEN branch (Plan 20-03) — the verb summarize_terrain shells"
  - "TgenEraVersions pinned constants (Plan 20-01 Task 3) — drive the both-lineage matrix"
  - "TgenDecoder + TgenFieldEncoder + apply-save-trn (Plans 20-02/03) — the codec under the gate"
provides:
  - "MCP summarize_terrain [McpServerTool(ReadOnly=true)] thin shell over decode-iff (ZERO format logic, MCP-OOP)"
  - "Opt-in LargeFixtures palette-bearing set (ShaderGroup/FloraGroup high version, CString family names) — lineage-divergence coverage without the ≤200-byte cap"
  - "DEC-C3 CLOSED: both-lineage byte-exact gate ratified on the SWGEmu==Infinity pin (BREC the one divergence)"
  - "PROD-W2-TRN-04 complete (MCP half landed here; codec/verbs in 20-02/03)"
affects:
  - "Phase 21 (Terrain TJT SubPanel) consumes the closed codec + the summarize_terrain read surface"
  - "REQUIREMENTS.md PROD-W2-TRN-04 reworded Restoration→Infinity + marked complete"
tech-stack:
  added: []
  patterns:
    - "MCP thin-shell (MCP-OOP): root.Resolve → cli.RunAsync(\"decode-iff\") → CliResultMapper.ToCallToolResult; nonzero exit → tool error"
    - "Opt-in large fixtures synthesized at test time (NOT committed goldens) to cover palette lineage the ≤200-byte cap cannot"
key-files:
  created:
    - "Utinni.Mcp.Tests/Terrain/TerrainReadToolTests.cs"
    - "Utinni.Cli.Tests/Fixtures/trn/LargeFixtures/TgenLargeFixtureSynthesizer.cs"
    - "Utinni.Cli.Tests/Terrain/PaletteLineageTests.cs"
  modified:
    - "Utinni.Mcp/Tools/ReadTools.cs (added SummarizeTerrain)"
    - ".planning/REQUIREMENTS.md (PROD-W2-TRN-04 Restoration→Infinity + complete)"
    - ".planning/phases/20-terrain-trn-codec-verbs-mcp/20-VALIDATION.md (DEC-C3 closure + status green)"
decisions:
  - "DEC-C3 CLOSED on the ratified SWGEmu==Infinity pin; BREC (0002/0003) is the one genuine divergence"
  - "AFCN stays ASSUMED v0000 (absent from every sampled planet, raw-falls-back cleanly) — non-blocking, not fabricated"
  - "Real-asset roundtrip SKIPPED (synthetic matrix + BREC + committed MCP fixtures cover both lineages byte-exactly); no real asset committed"
metrics:
  duration: "~40m (continuation: verify matrix + DEC-C3 closure + finalize)"
  completed: "2026-06-16"
  tasks_completed: 3
  files_modified: 6
---

# Phase 20 Plan 04: MCP `summarize_terrain` + Palette-Lineage LargeFixtures + DEC-C3 Closure Summary

Closed Phase 20 — the terrain `.trn` codec milestone. The thin MCP `summarize_terrain` read tool
(MCP-OOP — shells `decode-iff`, zero format logic) and an opt-in `LargeFixtures` palette-bearing set
(lineage divergence beyond the ≤200-byte committed-golden cap) landed in Tasks 1-2; this continuation
ratified the real-asset version pin and **declared DEC-C3 closed** — the both-lineage byte-exact gate
passes on confirmed, grounded versions, with `AFCN` honestly recorded as still-assumed.

## What Was Built

- **Task 1 (prior commit `2945ad2`, verified present):** `SummarizeTerrain` added to
  `Utinni.Mcp/Tools/ReadTools.cs` as `[McpServerTool(Name="summarize_terrain", ReadOnly=true,
  Idempotent=true)]` — body is `root.Resolve(relativePath)` → `cli.RunAsync("decode-iff", …)` →
  `CliResultMapper.ToCallToolResult(r)`, ZERO format logic (MCP-OOP / DEC-V2-MCP-OOP). The
  `TerrainReadToolTests.cs` proves: navigable `type:terrain`/`rootType:TGEN` envelope pass-through for
  both a low and a high version fixture; a malformed `.trn` propagates the nonzero CLI exit as an MCP
  TOOL ERROR (not a success envelope, concern #5); a path-escape relativePath surfaces a tool error
  (T-20-08); the test resolves the freshly-built x86 Release `utinni-cli.exe` (Plan-03 TGEN branch).
- **Task 2 (prior commit `759994d`, verified present):** `TgenLargeFixtureSynthesizer` emits
  palette-bearing TGEN fixtures (ShaderGroup/FloraGroup high version with CString family names, both
  MGRP palettes, plus single-MGRP-present and missing-earlier-slot cases) that EXCEED 200 bytes and are
  NOT committed goldens (generated at test time — D-12/D-14 preserved, concern #11). `PaletteLineageTests`
  assert positional palette assignment, family-name resolution, the single-MGRP `Ambiguous` flag (#3),
  and whole-file byte identity on roundtrip. The core ≤200-byte `RoundtripTrn` matrix still covers every
  Tier-1 tag low+high (no regression).
- **Task 3 (this continuation — DEC-C3 closure):** Re-verified the matrix green on the pinned versions,
  ratified the pin per the maintainer checkpoint, declared DEC-C3 closed (recorded in `20-VALIDATION.md`),
  reworded PROD-W2-TRN-04 ("Restoration" → "Infinity") and marked it complete, and recorded the
  AFCN-stays-assumed limitation.

## Task 3 — DEC-C3 Closure (maintainer-ratified)

The version pin was front-loaded as a PREREQUISITE in Plan 01 Task 3 (observed by dogfooding `utinni-cli`
`parse-tre` + `inspect-iff` against real SWGEmu `patch_00.tre` + SWG Infinity `mtg_planets.tre` assets) —
NOT a post-hoc sign-off (review concern #1, both reviewers HIGH). This plan ratifies it:

1. **CONFIRM THE PIN (decision 1):** SWGEmu and SWG Infinity ship the IDENTICAL `PTAT/0014` terrain
   format with identical per-tag FORM versions for every observed tag; nothing was v6000+/encrypted
   (concern #15 did not trigger). The one genuine lineage divergence is **`BREC` (low `0002` / high
   `0003`)**, preserved as a real low/high pair so the version-divergence dispatch path stays exercised.
   `TgenEraVersions.cs` already carries these ratified values — no code edit was needed.
2. **Matrix GREEN on the pinned versions:** `dotnet test Utinni.Cli.Tests -c Release --no-build --filter
   "RoundtripTrn|PaletteLineage"` → **55 passed / 0 failed**; `dotnet test Utinni.Mcp.Tests -c Release
   --no-build --filter Terrain` → **10 passed / 0 failed**.
3. **DEC-C3 declared CLOSED** — the both-lineage byte-exact gate is satisfied on grounded, confirmed
   versions. Closure recorded in `20-VALIDATION.md` § "DEC-C3 Closure (2026-06-16)" (the phase's
   gate-verdict location, consistent with how prior phases record decision closures in their VALIDATION /
   SUMMARY artifacts).

## AFCN Limitation (recorded — decision 2)

`AFCN` (FloraDynamicConstant) was **absent from every sampled planet of both clients**. Per the maintainer
decision it **stays annotated ASSUMED v0000** in `TgenEraVersions.cs` — no observed AFCN version was
fabricated. If a real asset's AFCN version ever differs from the assumption, the decoder **raw-falls-back
cleanly** (the tag becomes non-editable rather than a hard decode failure — D-02). This is **non-blocking**:
the both-lineage gate stands on the 18 observed tags + the BREC divergence.

## Real-Asset Roundtrip — SKIPPED (decision 3)

Per the maintainer, `roundtrip-trn` against a real client asset was NOT run and **no real client asset was
added to the repo** (D-14). The synthetic low+high matrix + the `BREC` divergence pair + the committed MCP
fixtures already cover both lineages byte-exactly; the extra real-asset check would add no coverage the
grounded pin does not already provide.

## PROD-W2-TRN-04 Reword (decision 4 — APPROVED)

`.planning/REQUIREMENTS.md` PROD-W2-TRN-04 acceptance text reworded **"Restoration" → "Infinity"**
(reflecting the real validated lineage) and the requirement marked **complete** (`[x]` + traceability row
→ Complete). The MCP half landed in this plan; the codec/verbs landed in 20-02/03. A repo-wide grep for
`Restoration` in `REQUIREMENTS.md` now returns zero matches (grep-gate hygiene).

## Deviations from Plan

**1. [Rule 3 — process] Task 3 was a documentation/decision-closure task, not a code change.**
- The plan's Task 3 anticipated possibly reconciling Wave-0 observations into `TgenEraVersions` and running
  a real-asset roundtrip. Plan 01 Task 3 had ALREADY pinned the constants (and relabeled Restoration→Infinity),
  and the maintainer checkpoint (1) ratified them as-is and (3) skipped the real-asset roundtrip. So Task 3
  required NO source edit — only the DEC-C3 closure record, the REQUIREMENTS reword, and the AFCN limitation
  note. The Release build artifacts from Tasks 1-2 were re-verified current and green.

No architectural deviations. No new packages (threat T-20-SC: accept — pure managed, zero installs).

## Authentication Gates

None.

## Known Stubs

None. The four Plan-01 Skip-marked terrain test classes are all now un-skipped and green
(TgenDecoder/RoundtripTrn/ApplySaveTrn in CLI, TerrainReadTool in MCP). The `AFCN` ASSUMED-v0000 value
is documented (above), not a stub — it raw-falls-back cleanly and is non-blocking.

## Deferred Issues

- **AbiSurfaceTests ABI drift (pre-existing, out-of-scope):** `ADDED(0) / REMOVED(20)` vs the
  Phase-17-frozen blessed baseline — entirely CppSharp regen/reorder churn (ADDED=0 proves Phase 20
  introduced ZERO new native ABI surface; this plan is pure managed + docs). Left deferred per
  `deferred-items.md`; re-bless is a maintainer/Phase-17-scope operation.

## Verification

- **MSBuild** `Utinni.sln /p:Configuration=Release /p:Platform=x86` → built, no errors (only pre-existing
  xUnit analyzer warnings). PowerShell-invoked.
- `Generated/UtinniCore.cs` reverted via `git checkout --` (not committed).
- `dotnet test Utinni.Cli.Tests -c Release --no-build --filter "RoundtripTrn|PaletteLineage"` → **55 passed,
  0 failed**.
- `dotnet test Utinni.Mcp.Tests -c Release --no-build --filter Terrain` → **10 passed, 0 failed**.
- Full lanes: `Utinni.Cli.Tests` → **411 passed, 2 skipped (pre-existing, unrelated), 0 failed**;
  `Utinni.Mcp.Tests` → **112 passed, 0 failed**.
- No real client assets tracked (`git ls-files | grep .trn` → none).

## Self-Check: PASSED

- `20-04-SUMMARY.md` present on disk; `ReadTools.cs`, `TgenLargeFixtureSynthesizer.cs`,
  `TerrainReadToolTests.cs`, `PaletteLineageTests.cs` all present; `summarize_terrain` present in
  `ReadTools.cs`.
- Prior commits `2945ad2` (Task 1) + `759994d` (Task 2) present in git log.
- Matrix re-confirmed green: 55 RoundtripTrn|PaletteLineage, 10 MCP Terrain; full lanes 411 Cli / 112 Mcp.
- No real client assets tracked.
