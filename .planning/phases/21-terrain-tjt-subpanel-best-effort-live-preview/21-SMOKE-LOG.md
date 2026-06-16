---
plan: 21-04
type: smoke-log
date: 2026-06-16
client: SWGEmu (D:\SWGEmu-Client\SWGEmu\SWGEmu.exe) — injected; hooks mapped clean (TRE Browser, TGEN decode, command-parser scene load, ImGui overlay all functional)
planet: naboo (terrain/naboo.trn, FORM PTAT → 0014, "PTAT/5000"-labelled, 1.2 MB, real high-era)
searchpath_prereq: re-enabled `searchPath_00_27=D:\SWGEmu-Client\SWGEmu\loose` in swgemu_live.cfg for the smoke (phantom-walk mitigation temporarily lifted; reverted after)
d07_disposition: pending (PendingNextSceneChange honest default STANDS — LivePreviewObserved=false)
scene_change_crash: NONE observed (0x0051fb0a guard held across save + ReloadTerrain dispatch)
---

# 21-04 Live-SWG Smoke Log — D-07 terrain live-preview

## Headline

A maintainer-driven live smoke (Claude driving TJT via windows-mcp, maintainer watching) on a **real**
injected SWGEmu session against **terrain/naboo.trn** surfaced a **blocking codec defect** that made the
terrain editor unable to save ANY edit on real high-era terrain. The defect was **root-caused, fixed, and
re-validated live in the same session** for the typed-field path. The D-07 live-re-read question itself was
**not** conclusively answered (no unambiguous in-session re-read of edited content), so the honest
`PendingNextSceneChange` default stands.

## What was observed (chronological)

1. **TRE Browser → Open in Terrain Editor** on naboo.trn opened the read-only host fine; the TGEN layer tree
   (~24 layers), six read-only palettes, and typed/raw field panes all rendered. Hooks map → it is the
   SWGEmu client UtinniCore is calibrated for (confirmed via `[TreBrowser] process module dir:
   'D:\SWGEmu-Client\SWGEmu'` in utinni.log).
2. **Both edit paths FAILED on real terrain** (pre-fix):
   - Active-flag toggle → `Active-flag edit failed: LAYR FORM 'FORM:PTAT/0/FORM:0014/0/FORM:TGEN/1/FORM:0000/0/FORM:LYRS/5/FORM:LAYR/0' has no IHDR DATA child leaf (cannot address active flag)`.
   - Typed ASCN scalar edit → `Edit failed: No typed node FORM container found for stable id 'FORM:PTAT/0/FORM:0014/0/FORM:TGEN/1/FORM:0000/0/FORM:LYRS/5/FORM:LAYR/0/FORM:ASCN/2'`.
3. **Root cause (FIXED):** `TgenDecoder.DecodeLayer` descends a real `LAYR` FORM into its version-form body
   to enumerate IHDR + child nodes, but built each child's `StableIdPath` off the **LAYR** path, dropping
   the version-form segment. The save-side `FindNodeByStableId` walks the TRUE DOM and could not relocate
   the node. The Plan 01 fixture used the synthesizer's *collapsed* shape (layer FORM sub-type "0003", not a
   literal "LAYR"), which never exercised the descent — so the byte-parity tests passed while real terrain
   (`LYRS → LAYR → version → IHDR + affectors`) broke.
4. **Fix applied + re-validated LIVE (same session):** after rebuild + reinject, the typed ASCN scalar edit
   **staged** ("Edit staged — Save or Preview to apply." + ● dirty glyph) and **saved** byte-exact with **no
   crash**. The reload candor footer showed the honest "Reloads on next scene change" copy.

## D-07 disposition: PENDING (honest default stands — and now OBSERVED-correct)

**Observed (maintainer, clean controlled Scene→Reload with the edited override on the active searchPath):**
the scene **visibly re-rendered in-session, but the terrain texture did NOT change** — the familyId edit
did not appear.

**Interpretation:** an in-session `GroundScene::ReloadTerrain` (0x0051A4F0) **regenerates** the terrain
live (the re-render is real and crash-free) but does **NOT re-read the edited `.trn` from disk** — it
regenerates from the already-loaded in-memory `TerrainGenerator` data. So an edited loose override is NOT
applied by a bare in-session reload. This is a *positive* observation that the honest
`PendingNextSceneChange` copy is the correct conservative wording — not merely an untested fallback.
**"live"/immediate wording would be wrong** (we disproved in-session re-read of edited content).
→ `TerrainReloadCandor.LivePreviewObserved` stays `false`; the `ReloadedTerrain` tier keeps the
`PendingNextSceneChange` copy. **No code change.** Precedent: phase10 SC3 (observe-then-honestly-label).

**Not tested this session (noted, does NOT block the conservative disposition):** whether a full TJT-driven
**scene change** (which re-reads the `.trn` from disk) actually applies the edit — i.e. whether the precise
tier is "next scene change" vs "relog-only" vs "loose override not picked up at all". The shipped wording
does not over-promise live/immediate, so it is honest regardless; confirming the exact scene-change tier is
a cheap follow-up (one chat-command scene change + observe).

## Residual bugs found (filed as follow-ups — see .planning/todos/pending/)

- **R1 — active-flag IHDR DATA deeper nesting (OPEN):** on real terrain the layer-item-header DATA is nested
  `LAYR → version → IHDR → IHDR-version → DATA`, one level deeper than both `TgenDecoder.ReadLayerItemHeader`
  and `TerrainSaveTargets.ResolveIhdrLeafStableId` handle. The Plan 21.x fix added the `LAYR → version`
  descent (necessary, partial) but the `IHDR → version → DATA` descent is still missing in BOTH the decoder
  read and the resolver — so the active flag (a) reads as the default `true` rather than the real value and
  (b) cannot be addressed for write. Needs a fixture that models `IHDR → version → DATA`.
- **R2 — terrain override save path omits the `loose/` subdir:** `TerrainSaveTargets.SaveLooseOverride`
  resolves `LooseOverridePath.Resolve(resolvedRoot, relAssetPath)` with NO `looseOverrideSubDir`, so terrain
  overrides land at `<root>\terrain\<asset>` instead of `<root>\loose\<asset>` like every other editor
  (IFF/Datatable/STF/OT). The documented loose-`searchPath` convention (`<root>\loose`) therefore does not
  cover terrain overrides; the smoke had to relocate the file to `<root>\loose\terrain\` for pickup.
- **R3 — TerrainSubPanel TRE hand-off fails while the Terrain section is collapsed:** `FindTerrainSubPanel()`
  returns null until the docked Terrain `CollapsiblePanel` is expanded (lazy control realization), so the
  "Open in Terrain Editor" hand-off reports "Terrain Editor is unavailable in this session." Workaround:
  expand the Terrain section first. Should realize the SubPanel eagerly or walk a stable registry.
