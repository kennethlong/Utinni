---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 01
subsystem: worldsnapshot-editor
tags: [PROD-W2-WS, worldsnapshot, bulk-ops, winforms, themeddatagridview, cross-repo]
requires:
  - WorldSnapshotReaderWriter (native, CppSharp-exposed)
  - WorldSnapshotCommands (Add/Remove/PositionChanged IUndoCommands)
  - WorldSnapshotImpl (game-thread *Impl wrapper)
  - ThemedDataGridView (Phase-9 TJT control)
  - SingletonFormClosePolicy (Phase-8 framework helper)
  - FormSaveConfirmDialog (Phase-8 destructive-confirm modal)
provides:
  - WorldSnapshotBulkComposer (pure/BCL-only bulk-op command-composition helper)
  - WorldSnapshotImpl.BulkMove / BulkDelete / BulkRetemplate (atomic, undoable)
  - WorldSnapshotImpl.SelectNodeById (table-row -> gizmo selection sync)
  - FormSnapshotPlacements (companion placements-table editor window)
  - FormSnapshotBulkMoveDialog / FormSnapshotBulkRetemplateDialog (bulk-input modals)
  - SnapshotPanel "Placements…" launch button
affects:
  - TheJawaToolboxDotNet.csproj (added explicit UtinniCoreDotNet.PathContainment ref)
tech-stack:
  added: []
  patterns: [game-thread-marshalling, event-handler-detach-reattach, singleton-hide-not-dispose, winforms-dockfill-zorder, framework-leg-discipline]
key-files:
  created:
    - "D:/Code/Utinni/UtinniCoreDotNet/Commands/WorldSnapshotBulkComposer.cs"
    - "D:/Code/Utinni/UtinniCoreDotNet.Tests/Commands/WorldSnapshotBulkComposerTests.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSnapshotPlacements.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSnapshotPlacements.Designer.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSnapshotBulkMoveDialog.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSnapshotBulkRetemplateDialog.cs"
  modified:
    - "D:/Code/Utinni/UtinniCoreDotNet/UtinniCoreDotNet.csproj"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/SWG/WorldSnapshotImpl.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/SubPanels/SnapshotPanel.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/SubPanels/SnapshotPanel.Designer.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj"
decisions:
  - "Bulk-op undo wiring = N ordered descriptors (NOT a single composite); retemplate = remove+add pair per node, in input order."
  - "Duplicate ids in a selection are NOT deduped (undo-count integrity with the user's selection)."
  - "FormSnapshotPlacements is launched directly by the SnapshotPanel button (not registered in GetForms()); singleton held by the panel."
  - "Cell column shows 'cell {parentId}' for child nodes / empty for world-cell (0); a full parent-id->cell-name table is a polish follow-up."
metrics:
  duration: ~75 min
  completed: 2026-06-07
---

# Phase 15 Plan 01: WorldSnapshot Placements Editor (bulk ops) Summary

Grew the shipped TJT `SnapshotPanel` into a real WorldSnapshot editor: a flat, multi-select placements table in a companion resizable `FormSnapshotPlacements` window, with atomic, undoable bulk move / delete / retemplate composing the shipped `WorldSnapshotCommands` over the native `WorldSnapshotReaderWriter` node list — zero new format code (D-02).

## What shipped

**Task 1 — framework helper + bulk ops (cross-repo):**
- `WorldSnapshotBulkComposer` (Utinni, pure/BCL-only, checker B-1): `ComposeMove`/`ComposeDelete`/`ComposeRetemplate` produce ordered `WorldSnapshotBulkOpDescriptor` lists naming the shipped command to compose per node. No WinForms / native-marshalling / `UtinniCore.Utinni` tokens (grep-clean).
- `WorldSnapshotImpl.BulkMove/BulkDelete/BulkRetemplate` (UtinniPlugins): each derives a plan via the composer and enqueues exactly ONE `GroundSceneCallbacks.AddUpdateLoopCall` composing N shipped `IUndoCommand`s (PositionChanged / Remove / Add) atomically on one game-frame, pushed through `editorPlugin.AddUndoCommand`. Never mutates the snapshot on the WinForms thread.
- 5 passing facts (`WorldSnapshotBulkComposerTests`, `--filter SnapshotBulk`).

**Task 2 — companion window + launch button (UtinniPlugins):**
- `FormSnapshotPlacements` (+ Designer): resizable `UtinniForm`, 900×600 / min 720×420, title `Snapshot Placements — {name}`, persists `[Snapshot] placementsWidth/placementsHeight`. Read-only multi-select `ThemedDataGridView` (Id / Object template / Cell / Position) populated from the native node list read on the game thread + marshalled back. Bulk toolbar, filter row (substring across id/template/cell, Esc clears), locked reload-candor badge, DEC-A3 D-10 footer, status strip. Single-row select drives the gizmo via `SelectNodeById` (Pattern 2 detach/reattach); multi-select drives only the count. Bulk delete routes through `FormSaveConfirmDialog`; move/retemplate use per-call input modals. Ctrl+A select-all, Delete -> delete selected. Singleton hide-not-dispose. Fill grid added FIRST (CF-09).
- `SnapshotPanel`: one `Placements…` `UtinniButton` launching the singleton window; Load/Unload re-baseline/clear the table; `GetSubPanels()` NOT widened.

## Verification

- `dotnet test UtinniCoreDotNet.Tests --no-build --filter SnapshotBulk` -> 5/5 passed.
- TJT solution builds Debug|x86 green via VS2026 MSBuild (exit 0; both `TheJawaToolbox.dll` + `TheJawaToolboxDotNet.dll` produced).
- Grep gates: composer has zero forbidden tokens; `AddUpdateLoopCall` count in `WorldSnapshotImpl` rose (3 bulk methods + `SelectNodeById`); locked WS reload-badge copy + DEC-A3 D-10 sentence appear verbatim; exactly one `Placements…` button; FormClosing delegates to `SingletonFormClosePolicy.ShouldHideInsteadOfDispose`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking build config] Explicit UtinniCoreDotNet.PathContainment reference in TJT csproj**
- **Found during:** Task 2 (TJT build gate).
- **Issue:** Pre-existing breakage introduced by 14-01: `UtinniCoreDotNet.Saving.LooseOverridePath` was physically moved to the netstandard2.0 `UtinniCoreDotNet.PathContainment` assembly with a `[TypeForwardedTo]` shim in `UtinniCoreDotNet.dll`. TJT references only `UtinniCoreDotNet.dll` with `<Private>False</Private>`, so RAR did not pull the forwarded-to implementation assembly into the C# compiler's reference set — the forwarded `LooseOverridePath` failed to resolve (CS0103) in the pre-existing `IffSaveTargets.cs` / `StringTableSaveTargets.cs`. Phase 14 verified the Utinni + MCP side but not the TJT solution, so this latent break only surfaced at this plan's TJT build gate.
- **Fix:** Added an explicit `<Reference Include="UtinniCoreDotNet.PathContainment">` (HintPath into `Utinni\bin\$(Configuration)\`, CopyLocal=false) so the compiler can follow the type-forward.
- **Files modified:** `TheJawaToolboxDotNet.csproj`.
- **Commit:** e0dacd7 (UtinniPlugins).

### Discretionary choices (CONTEXT-allowed, documented)

- Undo wiring = N ordered descriptors, not a composite (documented in the composer header).
- Placements window launched directly from the panel button (not `GetForms()`-registered) — UI-SPEC allowed planner discretion.
- Cell column shows `cell {parentId}` / empty-for-world-cell; full parent-id->cell-name resolution deferred as polish (column copy is honest about what it shows).

## Known Stubs

- **Cell-name resolution** (`FormSnapshotPlacements.ResolveCellName`): child-node cells render as `cell {parentId}` rather than a resolved cell name. Intentional V1 floor — the column is honest, the data source (node list) is fully wired, and a parent-id->cell-name table is a polish follow-up. Does not block the plan goal (single/multi-select + bulk ops over the placements table).

## Commits

| Repo | Hash | Message |
| ---- | ---- | ------- |
| Utinni | f1192e5 | feat(15-01): WorldSnapshotBulkComposer framework helper + 5 facts |
| UtinniPlugins | ed7f156 | feat(15-01): WorldSnapshotImpl bulk move/delete/retemplate ops |
| UtinniPlugins | e0dacd7 | feat(15-01): FormSnapshotPlacements companion window + SnapshotPanel Placements button |

## Notes for downstream plans

- The 14-01 type-forward gap (CS0103 on `LooseOverridePath` when a consumer references `UtinniCoreDotNet.dll` with `<Private>False</Private>`) is now fixed for TJT via the explicit PathContainment reference. The headier Particle editor (15-02+) is a NEW `FormParticleEditor` in the same csproj and inherits the fix.
- The live-in-client demo of the placements editor is deferred to the Wave-4 Tier-4 smoke (15-08), per the plan's success criteria.

## Self-Check: PASSED
