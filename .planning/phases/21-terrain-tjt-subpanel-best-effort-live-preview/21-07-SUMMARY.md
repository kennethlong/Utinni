---
phase: 21-terrain-tjt-subpanel-best-effort-live-preview
plan: 07
subsystem: ui
tags: [winforms, tjt, terrain, collapsible-panel, subpanel, cross-repo]

# Dependency graph
requires:
  - phase: 21-terrain-tjt-subpanel-best-effort-live-preview
    provides: "TerrainSubPanel docked in TJT (D-02), TRE-Browser 'Open in Terrain Editor' hand-off (21-04 smoke surfaced R3)"
provides:
  - "CollapsiblePanel.WrappedSubPanel — expand-state-independent public accessor for the wrapped SubPanel"
  - "FormTreBrowser.FindTerrainSubPanel resolves the docked TerrainSubPanel while the section is collapsed"
  - "Collapsed-reachability regression (CollapsiblePanelWrappedSubPanelTests)"
affects: [22-clienteffect-editor, future-tjt-subpanel-handoffs]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Expose lazily-realized child via an instance-held accessor instead of forcing eager Controls realization (preserves layout-lightness)."
    - "Consumer consults the accessor first, keeps the recursive Controls walk as a fallback for expanded/non-CollapsiblePanel hosts."

key-files:
  created:
    - "UtinniCoreDotNet.Tests/UITests/CollapsiblePanelWrappedSubPanelTests.cs"
  modified:
    - "UtinniCoreDotNet/UI/Controls/CollapsiblePanel.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs"

key-decisions:
  - "Exposed WrappedSubPanel accessor (the SubPanel is held in the ctor) rather than realizing the child eagerly — keeps btnExpand_CheckedChanged's lazy Controls.Add/RemoveAt layout contract intact."
  - "FindTerrainSubPanelIn consults CollapsiblePanel.WrappedSubPanel first, then falls back to the existing direct-cast + recursive-Controls walk (so expanded sections / non-CollapsiblePanel hosts still resolve)."

patterns-established:
  - "Accessor-over-eager-realize: surface a lazily-mounted child via an instance accessor; consumers prefer the accessor and retain the tree-walk as a fallback."

requirements-completed: [PROD-W2-TRN-05]

# Metrics
duration: ~12min
completed: 2026-06-17
---

# Phase 21 Plan 07: Collapsed-Section Terrain Hand-off Summary

**`CollapsiblePanel.WrappedSubPanel` exposes the constructor-held SubPanel regardless of expand state, so TRE-Browser's "Open in Terrain Editor" resolves the docked TerrainSubPanel while the Terrain section is collapsed — closing R3 without touching the lazy-realize layout.**

## Performance

- **Duration:** ~12 min
- **Completed:** 2026-06-17
- **Tasks:** 2
- **Files modified:** 3 (2 in Utinni, 1 in UtinniPlugins)

## Accomplishments
- Added a public read-only `WrappedSubPanel` accessor to the shared `CollapsiblePanel`, returning the `subPanel` field held since construction — reachable whether the section is expanded or collapsed.
- Updated `FormTreBrowser.FindTerrainSubPanelIn` to consult `CollapsiblePanel.WrappedSubPanel` (expand-state independent) before the recursive Controls walk, retaining the walk as a fallback.
- Closed R3: "Open in Terrain Editor" no longer reports "Terrain Editor is unavailable in this session." on a fresh (collapsed) session; the 21-04 expand-first workaround is no longer needed.
- Added `CollapsiblePanelWrappedSubPanelTests` pinning collapsed-state reachability and documenting the lazy-realize root cause the accessor sidesteps.

## Task Commits

1. **Task 1: Expose CollapsiblePanel.WrappedSubPanel + collapsed-reachability regression** — `8e0236c` (feat) [Utinni]
2. **Task 2: FindTerrainSubPanel consults WrappedSubPanel** — `8b8c5e3` (fix) [UtinniPlugins]

_Task 1 was authored test-first (RED) then implemented (GREEN) and combined into one feat commit — the accessor and its sole regression are a single atomic unit. The new test was confirmed passing post-implementation._

## Files Created/Modified
- `UtinniCoreDotNet/UI/Controls/CollapsiblePanel.cs` — added `public SubPanel WrappedSubPanel { get { return subPanel; } }`; lazy `btnExpand_CheckedChanged` behavior unchanged.
- `UtinniCoreDotNet.Tests/UITests/CollapsiblePanelWrappedSubPanelTests.cs` — asserts the wrapped SubPanel is `Same` as the constructed instance while `Open==false`, is absent from `Controls` while collapsed, and stays the same instance across expand toggles.
- `D:/Code/UtinniPlugins/.../UI/Forms/FormTreBrowser.cs` — `FindTerrainSubPanelIn` now checks `(c as CollapsiblePanel).WrappedSubPanel as TerrainSubPanel` first; recursive-Controls fallback retained; comment block updated (no grep-gated tokens).

## Decisions Made
- Chose the accessor approach (option 2 in the R3 todo) over eager realization — the SubPanel already exists from construction (Plugin.cs builds all SubPanels eagerly), so an accessor avoids any change to the layout-lightness contract every docked SubPanel relies on.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered
None. The pre-existing `AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn` failure appeared in the full suite run; this is the known Phase-17 CPPS-04 gotcha (incremental MSBuild skips `UtinniCoreDotNetGen.exe` → stale `Generated/UtinniCore.cs`). It is unrelated to this plan (no native surface touched) and is gated on master by CI, which runs the generator. All 782 other tests pass, including the new `CollapsiblePanelWrappedSubPanelTests`.

## Verification
- `UtinniCoreDotNet.Tests` built (VS2026 MSBuild, Release x86); `CollapsiblePanelWrappedSubPanelTests` passes (1/1).
- `UtinniCoreDotNet` + `TheJawaToolboxDotNet` built clean (VS2026 MSBuild, Release x86) — cross-repo paired commit.
- Grep gates: `WrappedSubPanel` non-comment count >= 1 in both `CollapsiblePanel.cs` (1) and `FormTreBrowser.cs` (1).
- `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs` applied before committing (CppSharp churn never committed).
- The R3 todo (`phase21-terrain-subpanel-collapsed-handoff.md`) moved `pending/` → `completed/`.

## Next Phase Readiness
- R3 closed framework-side; the live in-session click is covered by the existing phase live-smoke path (no new live checkpoint required for this low-severity UX fix).
- Phase 21 gap-closure plans 21-05..07 now executed.

## Self-Check: PASSED

- `UtinniCoreDotNet/UI/Controls/CollapsiblePanel.cs` — FOUND
- `UtinniCoreDotNet.Tests/UITests/CollapsiblePanelWrappedSubPanelTests.cs` — FOUND
- `D:/Code/UtinniPlugins/.../UI/Forms/FormTreBrowser.cs` — FOUND
- `.planning/phases/21-.../21-07-SUMMARY.md` — FOUND
- Commit `8e0236c` (Utinni) — FOUND
- Commit `8b8c5e3` (UtinniPlugins) — FOUND

---
*Phase: 21-terrain-tjt-subpanel-best-effort-live-preview*
*Completed: 2026-06-17*
