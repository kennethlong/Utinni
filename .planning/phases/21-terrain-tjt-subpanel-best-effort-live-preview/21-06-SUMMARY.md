---
phase: 21-terrain-tjt-subpanel-best-effort-live-preview
plan: 06
subsystem: ui
tags: [terrain, trn, loose-override, save-target, tjt, path-containment]

# Dependency graph
requires:
  - phase: 21-05
    provides: "TerrainSaveTargets.cs current state (R1 IHDR-version-form descent); the SaveLooseOverride site R2 closes"
  - phase: 20
    provides: "apply-save-trn CLI + LooseOverridePath.Resolve fail-closed containment + TerrainDocument codec"
provides:
  - "TerrainSaveTargets.SaveLooseOverride threads a looseOverrideSubDir (default 'loose') via the same two-step LooseOverridePath composition every other editor uses"
  - "FormTerrainEditor passes the loose subdir at the save call site (ini-resolved, defaults to 'loose') and PredictOverridePath composes the matching <root>/loose/<logical> destination"
  - "Framework-side TerrainLooseOverridePathTests pinning the destination under <root>/loose/ + traversal-escape rejection"
affects: [22-clienteffect-editor, terrain-live-smoke]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Two-step LooseOverridePath composition (overrideBase = Resolve(root, subDir) then Resolve(base, relAsset)) — verbatim shape from IffSaveTargets, now shared by the terrain save target"
    - "Per-editor [<Editor>] looseOverrideDir ini key with a 'loose' literal default, resolved at the form layer"

key-files:
  created:
    - "UtinniCoreDotNet.Tests/SavingTests/TerrainLooseOverridePathTests.cs"
  modified:
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/TerrainSaveTargets.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTerrainEditor.cs"

key-decisions:
  - "looseOverrideSubDir appended LAST in the SaveLooseOverride signature (after value) — keeps the existing positional call site readable; documented in the XML-doc."
  - "FormTerrainEditor mirrors FormIffEditor's ini resolution from its own [TerrainEditor] looseOverrideDir section (not a hardcoded literal), falling back to 'loose' so the two editors agree on the destination convention."

patterns-established:
  - "Terrain overrides land on the documented loose searchPath (<root>/loose/<logical>) like IFF/Datatable/STF/OT — no per-editor destination divergence."

requirements-completed: [PROD-W2-TRN-05]

# Metrics
duration: ~22min
completed: 2026-06-17
---

# Phase 21 Plan 06: Terrain loose-override destination under <root>/loose/ (R2) Summary

**TerrainSaveTargets.SaveLooseOverride now composes `<root>/loose/<logical>` via the same two-step LooseOverridePath shape every other editor uses, with PredictOverridePath aligned and a framework test pinning the loose/ destination + traversal rejection.**

## Performance

- **Duration:** ~22 min
- **Completed:** 2026-06-17
- **Tasks:** 2 (Task 1 TDD)
- **Files modified:** 3 (2 UtinniPlugins + 1 Utinni test)

## Accomplishments

- **R2 closed:** terrain loose overrides for a TRE asset `terrain/naboo.trn` now resolve to `<root>/loose/terrain/naboo.trn` — the documented loose searchPath the client toggles — instead of the bare `<root>/terrain/naboo.trn` the 21-04 smoke had to relocate by hand.
- Threaded a `string looseOverrideSubDir` parameter through `TerrainSaveTargets.SaveLooseOverride`, replacing the single `LooseOverridePath.Resolve(resolvedRoot, relAssetPath)` with the EXACT two-step composition from `IffSaveTargets.SaveLooseOverride` (overrideBase via `Resolve(root, subDir)` in its own try/catch → `Resolve(overrideBase, relAsset)`), each leg returning a distinct `SaveResult.Failure`. Empty/null subdir preserves the legacy `<root>/<logical>` destination.
- Aligned `FormTerrainEditor.PredictOverridePath` to the SAME two-step destination so the overwrite-confirm `File.Exists` checks the real loose/ override; added `ResolveLooseOverrideSubDir()` mirroring FormIffEditor's `[IffEditor] looseOverrideDir` resolution from a `[TerrainEditor]` section (default `"loose"`).
- Added `TerrainLooseOverridePathTests` (framework-layer, no UtinniPlugins reference): one fact asserts the resolved destination StartsWith `<root>/loose` and contains the `loose<sep>` segment; one asserts `../../escape.trn` still throws through the composition.

## Task Commits

1. **Task 1: Thread looseOverrideSubDir through TerrainSaveTargets.SaveLooseOverride** — UtinniPlugins `110d065` (feat)
2. **Task 2 (test): assert terrain loose override lands under <root>/loose/** — Utinni `a8ecf36` (test)
2. **Task 2 (plugin): pass loose subdir at call site + align PredictOverridePath** — UtinniPlugins `10cc1c7` (feat)

_Cross-repo paired commits (Utinni + UtinniPlugins) — no human checkpoint required per standing authority._

## Files Created/Modified

- `D:/Code/UtinniPlugins/.../Saving/TerrainSaveTargets.cs` — SaveLooseOverride gains a `looseOverrideSubDir` param + the two-step LooseOverridePath composition; XML-doc updated to state the `<root>/<subDir>/<logical>` destination.
- `D:/Code/UtinniPlugins/.../UI/Forms/FormTerrainEditor.cs` — save call passes `ResolveLooseOverrideSubDir()`; PredictOverridePath composes the matching two-step destination; new `ResolveLooseOverrideSubDir()` helper (`[TerrainEditor] looseOverrideDir`, default `"loose"`).
- `UtinniCoreDotNet.Tests/SavingTests/TerrainLooseOverridePathTests.cs` — framework-layer destination + escape-rejection facts.

## Decisions Made

- **Param position:** `looseOverrideSubDir` appended last (after `value`) to keep the single existing call site readable, per the plan's "choose the position that keeps the call site readable" guidance.
- **ini resolution over hardcoded literal:** FormTerrainEditor reads `[TerrainEditor] looseOverrideDir` (default `"loose"`), mirroring FormIffEditor, rather than hardcoding `"loose"` — so a maintainer can override the terrain subdir the same way the IFF editor allows, and the two editors stay in agreement.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

- `dotnet test --no-build` defaults to the Debug build dir; the wave built Release via MSBuild. Re-ran with `-c Release` to exercise the freshly-built assembly. Not a code issue.
- The full `UtinniCoreDotNet.Tests` suite shows the known pre-existing `AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline` failure (Phase-17 CPPS-04 incremental-build gotcha — incremental MSBuild skips `UtinniCoreDotNetGen.exe` → stale `Generated/UtinniCore.cs`; CI runs the gen and gates master). Not caused by this plan. All 784 other tests pass, including the 2 new terrain tests.

## Verification

- Utinni.sln + TheJawaToolbox.sln both built clean (VS2026 MSBuild, x86 Release) — TJT plugin DLL emitted with the new required parameter.
- `TerrainLooseOverridePathTests`: 2/2 passed.
- Grep gates: `looseOverrideSubDir` non-comment count in TerrainSaveTargets.cs = 3 (≥2); `SaveLooseOverride` non-comment count in FormTerrainEditor.cs = 2 (≥1, the matched call passes the loose-subdir arg).
- `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs` performed before committing (CppSharp churn).
- Byte-content parity with `apply-save-trn` unchanged — same single-source codec; R2 only relocates the destination under `loose/`.

## Next Phase Readiness

- R2 closed; all three Phase-21 gap-closure residuals (R1/21-05, R3/21-07, R2/21-06) are now resolved. Phase 21 gap-closure complete.
- A future live smoke can confirm the client picks up `<root>/loose/terrain/<asset>.trn` on the next scene change without manual relocation.

## Self-Check: PASSED

- FOUND: UtinniCoreDotNet.Tests/SavingTests/TerrainLooseOverridePathTests.cs
- FOUND: .planning/phases/21-.../21-06-SUMMARY.md
- FOUND: commit a8ecf36 (Utinni test)
- FOUND: commit 110d065 (UtinniPlugins Task 1)
- FOUND: commit 10cc1c7 (UtinniPlugins Task 2)

---
*Phase: 21-terrain-tjt-subpanel-best-effort-live-preview*
*Completed: 2026-06-17*
