---
phase: 21-terrain-tjt-subpanel-best-effort-live-preview
plan: 04
type: execute
status: complete
autonomous: false
requirements: [PROD-W2-TRN-05]
date: 2026-06-16
---

# 21-04 SUMMARY — D-07 maintainer live-smoke + best-effort live-preview disposition

## Self-Check: PASSED

Task 1 (maintainer live smoke) and Task 2 (candor disposition) both complete. The smoke was driven live by
Claude via windows-mcp with the maintainer watching, on a real injected **SWGEmu** session against
**terrain/naboo.trn** (real high-era PTAT/0014).

## What happened (and why this plan did more than "observe")

The live smoke surfaced a **blocking codec defect**: the terrain editor could not save ANY edit on real
high-era terrain — both the active-flag and typed-field paths threw stable-id errors. Root cause:
`TgenDecoder.DecodeLayer` descended a real `LAYR` FORM into its version-form body to enumerate children but
built their `StableIdPath` off the LAYR path, **dropping the version-form segment**, so the save-side
`FindNodeByStableId` could not relocate the node. The Plan 01 fixture used the synthesizer's *collapsed*
layer shape (FORM sub-type "0003", not a literal "LAYR"), which never exercised the descent — so byte-parity
tests passed while real terrain (`LYRS → LAYR → version → IHDR + affectors`) broke.

This was **fixed and re-validated LIVE in the same session** for the typed-field path:
- `TgenDecoder.DecodeLayer` now folds the version-form segment into child stable-ids.
- `TerrainSaveTargets.ResolveIhdrLeafStableId` descends the LAYR version form (partial — see R1).
- New `TgenFixtureSynthesizer.WithRealLayrWrapper` fixture + `RealLayrWrapperShape_…` regression test pin
  the real nesting (typed node StableIdPath round-trips the DOM; typed edit saves byte-exact). All terrain
  tests green (UtinniCoreDotNet.Tests 8/8 terrain; Utinni.Cli.Tests 181/181).
- After rebuild + reinject: typed ASCN scalar edit **staged** ("Edit staged" + ● glyph) and **saved**
  byte-exact, **no scene-change crash** (D-06 0x0051fb0a guard held).

## D-07 disposition: PendingNextSceneChange (honest default STANDS, now observed-correct)

Maintainer observed (clean Scene→Reload, edited override on the active searchPath): **the scene re-rendered
in-session, but the texture did NOT change.** → in-session `ReloadTerrain` regenerates terrain from the
already-loaded `TerrainGenerator` data and does **not** re-read the edited `.trn` from disk. So "live"/
immediate wording would be wrong. `TerrainReloadCandor.LivePreviewObserved` stays `false`; the
`ReloadedTerrain` tier keeps the `PendingNextSceneChange` copy. **No candor code change.** Full detail in
`21-SMOKE-LOG.md`.

## Residuals filed (todos/pending/)

- **R1** `phase21-terrain-active-flag-ihdr-deeper-nesting` — active flag still fails: real IHDR DATA is one
  more version-form deeper (`IHDR → version → DATA`); needs the descent in both `ReadLayerItemHeader` and
  `ResolveIhdrLeafStableId` + a real-shape IHDR fixture.
- **R2** `phase21-terrain-override-loose-subdir` — terrain overrides save to `<root>\terrain\` instead of
  `<root>\loose\` (no `looseOverrideSubDir`), so they fall off the documented loose searchPath.
- **R3** `phase21-terrain-subpanel-collapsed-handoff` — TRE-Browser hand-off returns "unavailable" until the
  docked Terrain section is expanded (lazy SubPanel realization).

## Key files

- `UtinniCoreDotNet/Formats/Decoders/TgenDecoder.cs` (FIX 1 — version-form stable-id segment)
- `D:/Code/UtinniPlugins/.../Saving/TerrainSaveTargets.cs` (FIX 2 — LAYR→version descent, partial)
- `Utinni.Cli.Tests/Fixtures/trn/TgenFixtureSynthesizer.cs` (`WithRealLayrWrapper` real-shape fixture)
- `UtinniCoreDotNet.Tests/Formats/Terrain/TerrainInProcSaveParityTests.cs` (regression test + fixed mirrors)
- `.planning/phases/21-.../21-SMOKE-LOG.md` (the observation record)
