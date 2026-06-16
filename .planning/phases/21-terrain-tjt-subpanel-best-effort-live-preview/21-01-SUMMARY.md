---
phase: 21-terrain-tjt-subpanel-best-effort-live-preview
plan: 01
subsystem: testing
tags: [terrain, trn, tgen, reload-candor, loose-override, byte-parity, winforms, tjt]

# Dependency graph
requires:
  - phase: 20-terrain-trn-codec-verbs-mcp
    provides: "TerrainDocument/TerrainLayer/TerrainNode model, TrnFieldEncoder, MutableIff DOM, apply-save-trn CLI"
  - phase: 08 (reload framework)
    provides: "ReloadAssetClassifier + ReloadTier (.trn already routes to ReloadedTerrain)"
provides:
  - "TerrainReloadCandor.StatusCopy(ReloadTier) -> (Text, IsError): single tested source of truth for the terrain reload-tier status-footer copy (D-07 honest default)"
  - "In-proc TerrainSaveTargets edit-save path (mirror of IffSaveTargets) proven byte-identical to apply-save-trn for BOTH typed-field and --field active edits"
  - "Net-new public TerrainSaveTargets.ResolveIhdrLeafStableId(doc, layerFormStableId): LAYR FORM -> IHDR DATA leaf bridge (RESEARCH OQ2)"
  - "Explicit .trn -> ReloadedTerrain classifier assertion; non-editable-node reject mirror"
affects: [21-02 (terrain field editor consumes ResolveIhdrLeafStableId + TerrainSaveTargets), 21-03 (save/preview wiring consumes TerrainReloadCandor), 21-04 (D-07 live-smoke flips LivePreviewObserved)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Framework-side presentation-copy helper (TerrainReloadCandor) so a UI-locked string is reachable from UtinniCoreDotNet.Tests (no UtinniPlugins reference) — same resolution as ReloadAssetClassifier"
    - "In-proc edit-save target mirroring IffSaveTargets, performing the field edit (EncodeField->SetPayload->Serialize) inside the save target then writing under fail-closed --root containment"
    - "Byte-parity-vs-CLI test that runs the canonical apply-save-trn algorithm as an independent oracle (no Utinni.Cli reference), via linked TgenFixtureSynthesizer source"

key-files:
  created:
    - "UtinniCoreDotNet/Saving/TerrainReloadCandor.cs"
    - "UtinniCoreDotNet.Tests/Saving/TerrainReloadCandorTests.cs"
    - "UtinniCoreDotNet.Tests/Formats/Terrain/TerrainInProcSaveParityTests.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/TerrainSaveTargets.cs"
  modified:
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj (register TerrainReloadCandor.cs)"
    - "UtinniCoreDotNet.Tests/Saving/ReloadAssetClassifierTests.cs (Classify_Trn assertion)"
    - "UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj (link Tgen fixture sources)"
    - "D:/Code/UtinniPlugins/.../TheJawaToolboxDotNet.csproj (register TerrainSaveTargets.cs)"

key-decisions:
  - "Save mechanism = in-proc TerrainSaveTargets (mirror IffSaveTargets), NOT a CLI shell-out — byte-parity proven by test (LOCKED, binds Plans 02/03)"
  - "Active-flag leaf addressing = net-new public ResolveIhdrLeafStableId helper (NOT the private CLI FindMutableLeafByStableId) — RESEARCH OQ2 resolution (LOCKED)"
  - "Candor copy lives framework-side in TerrainReloadCandor (pure presentation-copy map over ReloadTier) so the locked-string assert is reachable from UtinniCoreDotNet.Tests (LOCKED)"
  - "D-07 honest default: ReloadedTerrain tier renders the PendingNextSceneChange copy while LivePreviewObserved==false; flip only after Plan 04 live-smoke"

patterns-established:
  - "Pattern 1: framework-side presentation-copy helper for a UI-locked string testable without the plugin assembly"
  - "Pattern 2: in-proc terrain edit-save (EncodeField->SetPayload->Serialize) inside the save target, under fail-closed LooseOverridePath.Resolve --root containment"
  - "Pattern 3: independent apply-save-trn oracle inside the test to assert byte-parity without a Utinni.Cli reference"

requirements-completed: [PROD-W2-TRN-05]

# Metrics
duration: 9min
completed: 2026-06-16
---

# Phase 21 Plan 01: Wave-0 Terrain Foundation Summary

**Framework-side TerrainReloadCandor copy-map (D-07 honest default) + an in-proc TerrainSaveTargets edit-save target proven byte-identical to apply-save-trn for BOTH typed-field and IHDR active-flag edits, with the net-new ResolveIhdrLeafStableId bridge and the .trn->ReloadedTerrain assertion.**

## Performance

- **Duration:** ~9 min (first task commit 14:41:34 → last task commit 14:47:06 CDT)
- **Started:** 2026-06-16T19:38:00Z (approx)
- **Completed:** 2026-06-16T19:47:30Z
- **Tasks:** 2
- **Files modified:** 8 (4 created, 4 modified; across 2 repos)

## Accomplishments
- `TerrainReloadCandor.StatusCopy(ReloadTier)` — a pure framework-side presentation-copy map that is the single tested source of truth for the terrain reload-tier status footer, honoring the D-07 honest default (ReloadedTerrain renders the PendingNextSceneChange copy until the Plan 04 live-smoke flips `LivePreviewObserved`).
- In-proc `TerrainSaveTargets` (UtinniPlugins) mirroring `IffSaveTargets`: a `SaveResult` DTO + `SaveLooseOverride` that applies ONE fixed-length field edit and writes under fail-closed `--root` containment, gating on `TerrainNode.IsEditable` and routing the encode through the single-source `TrnFieldEncoder` (zero byte-offset packing of its own).
- Net-new PUBLIC `ResolveIhdrLeafStableId(doc, layerFormStableId)` bridging a layer's LAYR FORM stable id to its IHDR DATA leaf id (RESEARCH OQ2) — the addressing Plan 02's active-flag toggle needs.
- `TerrainInProcSaveParityTests` proving the in-proc edit-save sequence is byte-identical to the `apply-save-trn` algorithm for a typed scalar field edit AND a `--field active` IHDR int32 edit, plus a non-editable-node reject.
- Explicit `Classify_Trn_ReturnsReloadedTerrain` assertion pinning the `.trn -> ReloadedTerrain` routing the candor footer rides.

## Task Commits

Each task was committed atomically (cross-repo paired — no human checkpoint per standing authority):

1. **Task 1: TerrainReloadCandor copy-map + .trn classifier assertion** — `42341de` (feat) [Utinni]
2. **Task 2: in-proc save byte-parity test** — `f6be208` (test) [Utinni]
2. **Task 2: in-proc TerrainSaveTargets save target** — `5e49705` (feat) [UtinniPlugins]

## Files Created/Modified
- `UtinniCoreDotNet/Saving/TerrainReloadCandor.cs` — pure tier→(Text,IsError) candor-copy map; D-07 `LivePreviewObserved` gate
- `UtinniCoreDotNet.Tests/Saving/TerrainReloadCandorTests.cs` — string-asserts each tier maps to the locked copy
- `UtinniCoreDotNet.Tests/Saving/ReloadAssetClassifierTests.cs` — added `Classify_Trn_ReturnsReloadedTerrain`
- `UtinniCoreDotNet.Tests/Formats/Terrain/TerrainInProcSaveParityTests.cs` — byte-parity (typed + active) + non-editable reject; exercises the IHDR-leaf resolver
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj` / `UtinniCoreDotNet.Tests.csproj` — register/link the new sources
- `D:/Code/UtinniPlugins/.../Saving/TerrainSaveTargets.cs` — in-proc save target + `ResolveIhdrLeafStableId`
- `D:/Code/UtinniPlugins/.../TheJawaToolboxDotNet.csproj` — register `TerrainSaveTargets.cs`

## Decisions Made
- All four planner-locked calls (in-proc save, net-new public IHDR resolver, framework-side candor copy, D-07 pending default) implemented as specified — no re-litigation.
- `TerrainSaveTargets.ApplyFieldEdit` exposed public (in addition to `SaveLooseOverride`) so Plan 02's manual Preview can apply the same edit in-memory before dispatch; this is a forward-looking convenience, not a scope change.

## Deviations from Plan

None affecting scope. Two mechanical build-registration steps were required and are part of the task commits:
- Both touched projects are OLD-STYLE csprojs with explicit `<Compile Include>` items (not globbed), so `TerrainReloadCandor.cs` (UtinniCoreDotNet) and `TerrainSaveTargets.cs` (TheJawaToolboxDotNet) were each registered in their csproj — [Rule 3 - Blocking] (the new files would not compile in otherwise). Verified by clean MSBuild + green tests.
- Linked `TgenFixtureSynthesizer.cs` + `TgenEraVersions.cs` from `Utinni.Cli.Tests` into `UtinniCoreDotNet.Tests` via the established linked-source pattern (mirrors `TreFixtureBuilder.cs`), because `UtinniCoreDotNet.Tests` cannot ProjectReference `Utinni.Cli.Tests` — [Rule 3 - Blocking] enabling the parity test to synthesize the same deterministic FORM TGEN fixtures.

**Total deviations:** 2 build-registration / linked-source steps (both blocking). No scope creep; both are the standard mechanism for adding a source to these project types.

## Issues Encountered
- **Pre-existing `AbiSurfaceTests` failure (out of scope).** The full `UtinniCoreDotNet.Tests` run is 779 passed / 1 failed; the lone failure is the Phase-17 CPPS-04 ABI gate (`GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn`). Both the committed `Generated/UtinniCore.cs` and the blessed baseline are byte-unchanged since commit `93ded43` (immediately before this plan), and this plan adds ZERO CppSharp bindings — the drift predates Phase 21 and is the documented incremental-build-skips-gen gotcha (`[[project_phase17_cppsharp_v145_hardening]]`). Logged to `deferred-items.md`; NOT fixed (SCOPE BOUNDARY). The 21-01 acceptance gates (`TerrainReloadCandor` / `Classify_Trn` / `TerrainInProcSaveParity`) are all green.

## User Setup Required
None — no external service configuration required. (The D-07 live-SWG smoke is a Plan 04 maintainer gate, not a Plan 01 setup step.)

## Next Phase Readiness
- Plan 02 can consume `TerrainSaveTargets.ResolveIhdrLeafStableId` + `TerrainSaveTargets.SaveLooseOverride`/`ApplyFieldEdit` and `TerrainReloadCandor.StatusCopy` directly — all shipped and byte-parity-proven.
- The honest-default candor (`LivePreviewObserved == false`) is in place; Plan 04's maintainer live-smoke is the only gate that can flip it to the "Reloaded (terrain)" wording.
- No blockers. The pre-existing ABI-gate failure is independent of this phase and tracked in `deferred-items.md`.

## Self-Check: PASSED

All created files exist on disk (TerrainReloadCandor.cs, TerrainReloadCandorTests.cs,
TerrainInProcSaveParityTests.cs, 21-01-SUMMARY.md, and UtinniPlugins TerrainSaveTargets.cs) and all
three task commits resolve (`42341de`, `f6be208` in Utinni; `5e49705` in UtinniPlugins).

---
*Phase: 21-terrain-tjt-subpanel-best-effort-live-preview*
*Completed: 2026-06-16*
