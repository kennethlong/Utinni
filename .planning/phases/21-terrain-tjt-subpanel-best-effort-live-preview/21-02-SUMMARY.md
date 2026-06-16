---
phase: 21-terrain-tjt-subpanel-best-effort-live-preview
plan: 02
subsystem: ui
tags: [terrain, trn, tgen, winforms, tjt, reload-candor, loose-override, live-preview]

# Dependency graph
requires:
  - phase: 20-terrain-trn-codec-verbs-mcp
    provides: "TerrainDocument/TerrainLayer/TerrainNode/TerrainPalettes model + TgenFieldLayouts (single offset source)"
  - phase: 21 plan 01
    provides: "TerrainSaveTargets.SaveLooseOverride/ApplyFieldEdit/ResolveIhdrLeafStableId + TerrainReloadCandor.StatusCopy (D-07 honest default)"
  - phase: 08 (reload framework)
    provides: "ClientReloadDispatcher.Dispatch (.trn → ReloadedTerrain, game-thread, Game.IsRunning-gated)"
provides:
  - "FormTerrainEditor: the roomy tree+field-editor host Form the thin docked Terrain SubPanel (Plan 03) launches — open/tree/typed-raw field pane/Save/Save-As-Override/Preview/candor footer"
  - "Net-new public TerrainSaveTargets.ResolveTypedDataLeafStableId(doc, nodeFormStableId): typed node FORM<tag> → its editable DATA leaf stable id (the typed-edit analog of Plan 01's IHDR bridge)"
affects: [21-03 (the docked SubPanel launches FormTerrainEditor + wires the TRE-Browser open hand-off), 21-04 (D-07 maintainer live-smoke flips LivePreviewObserved → upgrades the candor footer)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Thin-SubPanel-launches-roomy-Form host (D-02 escape hatch) — the FormTerrainEditor consumes the Phase 20 model + Plan 01 save/candor helpers with ZERO new format/reload logic"
    - "Pitfall-8 imperative WinForms layout: Dock.Fill content added FIRST (front-most), nested SplitContainer Size BEFORE SplitterDistance, whole build inside an MEF-safe try/catch (D-09)"
    - "Net-new public typed-DATA-leaf resolver mirroring the Plan 01 IHDR bridge so the UI addresses an editable leaf without re-implementing the DOM walk"

key-files:
  created:
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTerrainEditor.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTerrainEditor.Designer.cs"
  modified:
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/TerrainSaveTargets.cs (net-new public ResolveTypedDataLeafStableId)"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj (register the two new sources)"

key-decisions:
  - "Field pane = TreDetailPane-style custom pane (D-03), NOT a stock PropertyGrid — cleanly handles the typed-vs-raw-fallback split + per-field editability gating + read-only palette presentation"
  - "Typed-leaf addressing = NET-NEW public TerrainSaveTargets.ResolveTypedDataLeafStableId (the model's TerrainNode.StableIdPath is the FORM<tag> path, not the DATA leaf) — the typed analog of the Plan 01 IHDR bridge; UI never hand-rolls the walk"
  - "Manual Preview = Discretion Option A: apply the edit in-memory (ApplyFieldEdit) → write a temp loose override INSIDE the containment root → Dispatch; never commits to the real override (heap-free, one AddMainLoopCall, D-06)"
  - "Candor footer copy ONLY via TerrainReloadCandor.StatusCopy (D-07 honest default holds in the helper) — zero inline tier strings"

patterns-established:
  - "Pattern 1: roomy host Form for a heavy tree+grid editor, launched from a width-pinned docked SubPanel (the SnapshotPanel → FormSnapshotPlacements precedent applied to terrain)"
  - "Pattern 2: degrade-not-fail field pane — typed editable rows for IsEditable nodes, read-only generic list + verbatim hint for raw/dead/palette/name; selecting any node never throws"
  - "Pattern 3: net-new public leaf-id resolver in the save-targets module so consuming UI addresses an editable leaf without re-walking the DOM"

requirements-completed: [PROD-W2-TRN-05]

# Metrics
duration: 25min
completed: 2026-06-16
---

# Phase 21 Plan 02: FormTerrainEditor (terrain tree + field editor host) Summary

**The roomy `FormTerrainEditor` host Form — opens a `.trn`, renders the navigable TGEN layer tree + six read-only palettes, shows typed editable fields for Tier-1 tags and a read-only generic list for everything else (never a hard fail), edits fixed-length leaves + active-flag toggles saved byte-exact through the Plan 01 in-proc loose-override path, and previews on Save AND manual Preview through the EXISTING reload dispatcher with honest tiered candor — built MEF-safe with the locked Pitfall-8 layout idiom and a null-checked undo seam.**

## Performance

- **Duration:** ~25 min (research/reads → first task commit 14:57 → last task commit 15:02 CDT)
- **Tasks:** 2
- **Files modified:** 4 (2 created, 2 modified; all in the UtinniPlugins sibling repo)

## Accomplishments
- `FormTerrainEditor` (`TJT.UI.Forms`, `: Form`, ctor `FormTerrainEditor(IEditorPlugin)`): the heavy tree+field-editor surface the thin docked SubPanel (Plan 03) launches (D-02 — the SubPanel is hard-pinned to 417px, too narrow for a tree+grid split).
- MEF-safe ctor (D-09 / Pitfall 8): the whole control build runs inside a try/catch; a partial build surfaces a read-only state panel, never throws (a throwing registered child silently cascades the entire `IEditorPlugin` out of MEF compose). Nested `SplitContainer` sets `Size` BEFORE `SplitterDistance`; the `Dock.Fill` content region is added FIRST (front-most) so the docked Top banner/action-bar and Bottom status/candor strips claim their edges; `Panel1MinSize`/`Panel2MinSize` 40/80.
- Tree population from `doc.Layers` (TGEN root → each layer name + active flag → its boundary/filter/affector `Nodes` → recurse `SubLayers`) + a top-level "Shared palettes (read-only)" branch from `doc.Palettes` (six palette nodes, `familyId → name` children, all non-editable).
- Field pane (D-03 custom pane): typed (`IsEditable`) nodes render editable rows matched to `DisplayType` (`ScalarFloat`/`Int32` → text box invariant-culture parse; `Enum32`/`FamilyIdRef` → decoded-value text box; `ActiveFlag` → checkbox); raw-preserved → read-only generic list + verbatim `RawFieldHint`; dead-skipped → `ObsoleteTagHint`; palette/family/name → read-only with the locked copy. **Selecting any node never throws** (degrade-not-fail throughout).
- Active-flag toggle resolves the IHDR DATA leaf id via the Plan 01 `TerrainSaveTargets.ResolveIhdrLeafStableId(doc.Mutable, layer.StableIdPath)` bridge (RESEARCH OQ2) — never a hand-rolled tree walk; detach/reattach idiom so the programmatic set doesn't re-fire `CheckedChanged`.
- Save / Save-As-Override routes through Plan 01 `TerrainSaveTargets.SaveLooseOverride` (in-proc, byte-parity-proven); encoder `ArgumentException` surfaces as red status, never bubbles (Pitfall 5); Save-As over an existing override shows the locked "Override exists" confirm.
- Save AND manual Preview both fire EXACTLY ONE `AddMainLoopCall` via `ClientReloadDispatcher.Dispatch(path, null)` (D-05/D-06); Preview applies the edit in-memory (`ApplyFieldEdit`) then writes a temp loose override INSIDE the containment root and Dispatches (Discretion Option A) — never commits to the real override. The footer copy comes ONLY from `TerrainReloadCandor.StatusCopy` (D-07 honest default holds in the helper). Dirty leaves get the `●` glyph + `Colors.Secondary()` tint; the undo seam is null-safe (`editorPlugin.ClearUndoStack?.Invoke()`, D-09).

## Task Commits

Each task committed atomically in the UtinniPlugins sibling repo (cross-repo paired — no human checkpoint per standing authority; the live smoke is Plan 04):

1. **Task 1: FormTerrainEditor shell + layout + tree + read-only palettes** — `039b404` (feat) [UtinniPlugins]
2. **Task 2: field pane (typed/raw), active toggle, in-proc Save/Save-As-Override, Preview, candor footer + undo-seam null-checks** — `c718256` (feat) [UtinniPlugins]

## Files Created/Modified
- `…/UI/Forms/FormTerrainEditor.cs` — the host Form (open / tree / typed-raw field pane / Save / Save-As-Override / Preview / status+candor footer); consumes `TerrainDocument.FromBytes`, `TerrainSaveTargets.{SaveLooseOverride,ApplyFieldEdit,ResolveIhdrLeafStableId,ResolveTypedDataLeafStableId}`, `ClientReloadDispatcher.Dispatch`, `TerrainReloadCandor.StatusCopy`.
- `…/UI/Forms/FormTerrainEditor.Designer.cs` — the WinForms partial-class plumbing (IContainer/Dispose only; the layout is built imperatively in the .cs so the Pitfall-8 order is explicit).
- `…/Saving/TerrainSaveTargets.cs` — added the net-new public `ResolveTypedDataLeafStableId(doc, nodeFormStableId)` (the typed-node analog of the Plan 01 IHDR bridge).
- `…/TheJawaToolboxDotNet.csproj` — registered both new sources (old-style explicit `<Compile Include>` project).

## Decisions Made
- All planner-locked calls honored: D-02 (roomy host Form launched from the thin SubPanel), D-03 (custom tree+field pane, not `PropertyGrid`), the Plan 01 `ResolveIhdrLeafStableId` for the active flag, the D-07 honest candor default via `TerrainReloadCandor`.
- Manual Preview implemented as Discretion Option A (in-memory edit → temp loose override inside containment → Dispatch), keeping it heap-free with exactly one `AddMainLoopCall` and never committing to the real override.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added net-new public `TerrainSaveTargets.ResolveTypedDataLeafStableId`**
- **Found during:** Task 2 (typed-field edit wiring).
- **Issue:** The plan's interface table says the active-flag toggle uses the Plan 01 `ResolveIhdrLeafStableId`, but a **typed-field** edit also needs a leaf id, and the consumed model surfaces only `TerrainNode.StableIdPath` — which is the node's `FORM<tag>` path, NOT the editable `DATA` leaf id `apply-save-trn`/`SaveLooseOverride` mutate (confirmed in `TgenDecoder.DecodeNode` + the Plan 01 parity test's `TypedDataLeafId` walk). Without a resolver the UI could not address a typed leaf, and the plan forbids hand-rolling the DOM walk in the UI.
- **Fix:** Added a net-new public `ResolveTypedDataLeafStableId(MutableIffDocument doc, string nodeFormStableId)` to `TerrainSaveTargets` (the save-targets module that owns leaf addressing) — it walks `FORM<tag> → FORM<version> → DATA` and returns the DATA leaf stable id, exactly mirroring the structure of the existing public `ResolveIhdrLeafStableId`. The UI calls it; no hand-rolled walk.
- **Files modified:** `…/Saving/TerrainSaveTargets.cs`
- **Commit:** `c718256`

**Total deviations:** 1 (blocking). No scope creep — the addition is the typed-node analog of the Plan 01 IHDR bridge, in the same module, with the same shape, so the UI honors the "no hand-rolled tree walk" lock.

## Issues Encountered
- **Pre-existing `AbiSurfaceTests` failure (out of scope).** The full `UtinniCoreDotNet.Tests` run is 779 passed / 1 failed; the lone failure is the Phase-17 CPPS-04 ABI gate (`GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn`) — the documented incremental-build-skips-gen gotcha (`[[project_phase17_cppsharp_v145_hardening]]`). This plan adds ZERO CppSharp bindings (it is a pure UtinniPlugins UI change in a separate repo), so the drift is independent of Phase 21 and already tracked in `deferred-items.md`. NOT fixed (SCOPE BOUNDARY). The 36 terrain/candor/classifier tests are all green (`dotnet test --filter ~Terrain|~Classify` → 36/36 pass).

## Known Stubs
None that prevent the plan's goal. The terrain-render disposition (whether `GroundScene::ReloadTerrain` visibly re-reads a procedurally-edited `.trn` in-session) is the D-07 maintainer live-smoke deferred to Plan 04 by design — until then the candor footer honestly reports "Reloads on next scene change" via `TerrainReloadCandor` (`LivePreviewObserved == false`), which is correct behavior, not a stub.

## User Setup Required
None for the build/automation gate. The D-07 live-SWG smoke is a Plan 04 maintainer gate (it also requires re-enabling the loose-override `searchPath` the maintainer disabled for the phantom-walk mitigation — noted in RESEARCH).

## Next Phase Readiness
- **Plan 03** can launch `FormTerrainEditor` from the thin docked Terrain SubPanel (singleton hide-not-dispose, SnapshotPanel precedent) and wire the TRE-Browser "Open in Terrain Editor" hand-off to `FormTerrainEditor.OpenFromTreEntry(payload, archivePath, logicalPath)`; the loose-override open path is `OpenLooseOverride(loosePath)`.
- **Plan 04** flips `TerrainReloadCandor.LivePreviewObserved` only after the maintainer live-smoke confirms in-session procedural re-read — the editor's footer upgrades automatically (it reads the helper).
- No blockers. The pre-existing ABI-gate failure is independent of this phase.

## Self-Check: PASSED

All created/modified files exist on disk and both task commits resolve in the UtinniPlugins repo
(`039b404`, `c718256`). The solution builds clean (VS2026 MSBuild x86/Release); the dispatcher-only /
candor-helper-only / null-safe-undo / no-hardcoded-color grep gates all pass; the 36 terrain tests
are green.

---
*Phase: 21-terrain-tjt-subpanel-best-effort-live-preview*
*Completed: 2026-06-16*
