---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 16
subsystem: TJT Wave-2 editors (Particle + WorldSnapshot)
tags: [particle-editor, worldsnapshot, candor, ui-polish, gap-closure, cross-repo]
requires:
  - "15-12/15-13/15-14 framework changes (committed; TJT references the fresh framework)"
  - "FormParticleEditor (15-06), FormSnapshotPlacements + WorldSnapshotImpl (15-01/15-10)"
provides:
  - "Particle param-grid re-bind after every model mutation (edit/undo/redo) — B4/B5"
  - "Honest no-hook disabled-Preview tooltip distinct from the no-client case — B6"
  - "Delete-confirm copy disclosing scene-change deferral + BulkDelete DetailLevelChanged() — A7"
affects:
  - "FormParticleEditor.cs (UtinniPlugins)"
  - "FormSnapshotPlacements.cs (UtinniPlugins)"
  - "WorldSnapshotImpl.cs (UtinniPlugins)"
tech-stack:
  added: []
  patterns:
    - "BindParamGrid re-bind after RefreshMutable (model-node refs survive a same-document refresh)"
    - "Disabled-reason tooltip selected by state (no-client vs running-but-no-hook)"
    - "DetailLevelChanged() inside AddUpdateLoopCall for immediate grid refresh (BulkMove/BulkRetemplate parity)"
key-files:
  created: []
  modified:
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormParticleEditor.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSnapshotPlacements.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/SWG/WorldSnapshotImpl.cs"
decisions:
  - "currentParamNode holds the MutableIffNode (model object), not the TreeNode wrapper — model refs survive RefreshMutable (same MutableIffDocument), so a direct re-bind is correct without re-resolving by id/path."
  - "B6 tooltip selected by Game.IsRunning: running-but-no-hook -> PreviewNoHookTooltip (honest, no implied hook, matches LOCKED degraded reload badge); !Game.IsRunning -> existing PreviewUnavailableTooltip."
  - "A7 confirm appends the in-world-persistence sentence (no instant de-spawn over-promise); BulkDelete keeps the GAP-2 gizmo/selection clear and adds DetailLevelChanged() after it."
metrics:
  duration: ~25 min
  completed: 2026-06-13
  tasks: 2
  files: 3
---

# Phase 15 Plan 16: Managed polish — Particle grid re-bind, honest preview tooltip, delete-confirm candor Summary

Three non-blocking polish defects the 2026-06-13 live smoke deferred into the cleanup rebuild, closed managed-side in UtinniPlugins: the Particle param grid re-binds after every model mutation so a raw-hex edit (and Undo/Redo) shows the new hex without reselect; the disabled Preview button now gives an honest no-hook reason distinct from the no-client case; and the WorldSnapshot bulk-delete confirm discloses the scene-change deferral while BulkDelete refreshes the grid immediately like its siblings.

## What Was Built

### Task 1 — B4/B5 param-grid re-bind + B6 honest no-hook preview tooltip (FormParticleEditor.cs)
- **B4/B5:** added a `currentParamNode` field tracking the `MutableIffNode` the param grid is bound to (set in `BindParamGrid`). `AfterModelMutated()` — shared by `ApplyLeafEdit`, `DoUndo`, `DoRedo` — now re-calls `BindParamGrid(currentParamNode)` after `emitterTree.RefreshMutable(...)` when non-null, so the edited leaf's cell re-renders immediately. The model-node reference survives `RefreshMutable` because the control rebuilds only the `TreeNode` wrappers from the same `MutableIffDocument` (verified in `IffChunkTree.LoadMutable`), so no id/path re-resolve is needed.
- **B6:** new constant `PreviewNoHookTooltip = "Live preview isn't wired this build — edits show on the next scene change or relog."`. `RefreshButtonsState` hoists the `Game.IsRunning` probe and selects the disabled-Preview tooltip by reason: running-but-no-hook → `PreviewNoHookTooltip`; `!Game.IsRunning` → the existing `PreviewUnavailableTooltip`. The LOCKED reload-badge constants (`ReloadBadgeLiveCapable` / `ReloadBadgeDegraded`) are untouched. The new copy does not imply a reachable hook and matches the degraded reload-badge wording.

### Task 2 — A7 delete-confirm candor + BulkDelete DetailLevelChanged consistency
- **FormSnapshotPlacements.OnDeleteSelectedClicked:** the `FormSaveConfirmDialog` body appends " The in-world object stays visible until the next scene change." — disclosing the deferred reality (matching the LOCKED WS badge "Placements re-resolve on the next scene change.") without claiming an instant world de-spawn. The heading "Delete {n} placements?", the Delete/Cancel verbs, and the "undoable in the editor until you save" clause are unchanged.
- **WorldSnapshotImpl.BulkDelete:** added `WorldSnapshot.DetailLevelChanged();` inside the existing `GroundSceneCallbacks.AddUpdateLoopCall`, after the GAP-2 `DisableGizmo()` + `UpdateSelectedNodeControls(null)` clear, matching `BulkMove`/`BulkRetemplate` so the grid refreshes immediately rather than relying solely on the 250ms `ScheduleRefresh` timer. The GAP-2 clear is retained.

## Verification
- `TheJawaToolbox.sln` Release|x86 built MSBuild exit 0 after each task (VS2026 / Dev18 MSBuild on D:).
- Read-confirms: `AfterModelMutated` re-binds the grid for the current selection; new `PreviewNoHookTooltip` constant + `Game.IsRunning`-keyed branch present; `ReloadBadgeLiveCapable`/`ReloadBadgeDegraded` unchanged; delete-confirm discloses the scene-change deferral and keeps the undoable clause without an instant-de-spawn claim; `BulkDelete` calls `DetailLevelChanged()` while keeping `DisableGizmo()` + `UpdateSelectedNodeControls(null)`.
- C#-only build did not regenerate `UtinniCoreDotNet/Generated/UtinniCore.cs` (confirmed clean in Utinni).
- LIVE (gated to 15-18 against the 15-17 reassembled build): B4/B5 grid refreshes after hex edit; B6 disabled-Preview tooltip reads honestly for the no-hook state; A7 delete confirm reads honestly + grid refreshes immediately.

## Deviations from Plan

### Build-command substitution (environment, not a code deviation)
- The plan's verify command hardcodes `%ProgramFiles%\Microsoft Visual Studio\18\Community\...` (= C:). The real install is on D:. Substituted `D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`. Also used the `-p:`/`-v:` switch form (not `/p:`) to avoid Git Bash POSIX-path mangling of `/`-prefixed switches.

Otherwise: plan executed exactly as written. No auto-fixes (Rules 1–3) were needed.

## Threat Surface
No new security-relevant surface. Both task changes are honesty/UX polish (display re-bind + copy candor + grid-refresh parity) — no new network endpoints, auth paths, file access, or trust-boundary schema changes. T-15-16-01 (stale param-grid) and T-15-16-02 (over-promising copy) are the two `mitigate` rows in the plan's threat register; both are now mitigated as planned.

## Known Stubs
None introduced. The no-hook Preview state (`IsRetriggerHookReachable() => false`) is the documented 15-03 finding, unchanged by this plan; the live-capable path remains wired behind the single seam for 15-08+.

## Commits
- `02bfc46` (UtinniPlugins) — fix(15-16): re-bind particle param grid after model mutation + honest no-hook preview tooltip
- `9180250` (UtinniPlugins) — fix(15-16): A7 delete-confirm candor + BulkDelete DetailLevelChanged consistency

## Self-Check: PASSED
- FOUND: 15-16-SUMMARY.md
- FOUND: commits 02bfc46, 9180250 (UtinniPlugins)
