---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 10
subsystem: worldsnapshot-editor / undo-redo seam
tags: [PROD-W2-WS, gap-closure, undo-redo, cross-repo, binary-compat, A9-crash]
requires:
  - "UndoRedoManager (UtinniCoreDotNet/UndoRedo) — existing lock/stack idiom"
  - "IEditorPlugin MEF SPI — existing AddUndoCommand settable-from-host shape"
  - "WorldSnapshotImpl (UtinniPlugins) — existing DisableGizmo + GroundSceneCallbacks.AddUpdateLoopCall FIFO marshalling"
  - "15-09 WS undo-crash null-guards (belt-and-suspenders pairing)"
provides:
  - "UndoRedoManager.Clear() — public programmatic clear of both stacks + UI update callback"
  - "IEditorPlugin undo seam (Undo / Redo / ClearUndoStack settable delegates) wired by FormMain"
  - "WorldSnapshotImpl clears the undo stack + gizmo on Load/Unload/Reload + BulkDelete/RemoveNode"
  - "FormSnapshotPlacements Ctrl+Z / Ctrl+Y routing to the editor undo manager (FIFO refresh-after-undo)"
affects:
  - "Any IEditorPlugin implementer (interface widened — TJT + 2 SDK artifacts updated in this commit)"
tech-stack:
  added: []
  patterns:
    - "Host-wired plugin seam: settable Action delegates on IEditorPlugin bound by FormMain (mirrors AddUndoCommand)"
    - "FIFO update-loop ordering: enqueue undo body THEN re-read so the grid reflects post-undo state"
    - "Snapshot-boundary stack clear: the undo stack must not outlive the node list it was built against"
key-files:
  created: []
  modified:
    - "UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs (Utinni)"
    - "UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs (Utinni)"
    - "UtinniCoreDotNet/UI/Forms/FormMain.cs (Utinni)"
    - "UtinniCoreDotNet.Tests/UndoRedoManagerTests.cs (Utinni)"
    - "sdk/examples/ExampleEditorPlugin/ExampleEditorPlugin.cs (Utinni)"
    - "sdk/UtinniPluginTemplates/DotNetEditorPluginTemplate/Plugin.cs (Utinni)"
    - "The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs (UtinniPlugins)"
    - "The Jawa Toolbox/TheJawaToolboxDotNet/SWG/WorldSnapshotImpl.cs (UtinniPlugins)"
    - "The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSnapshotPlacements.cs (UtinniPlugins)"
decisions:
  - "OnCleanupCallback now DELEGATES to the new public Clear() (single clear-both-stacks idiom, no duplicate logic)"
  - "Lock-release-before-callback ordering preserved in Clear() (C-07 / T-02-10 — never fire onUpdateCommandsCallback while holding syncRoot)"
  - "ClearUndoStack invoked OUTSIDE the AddUpdateLoopCall (managed UI-manager call, null-safe); native (re)read stays inside the loop for allocator-safety"
  - "RefreshTable self-enqueues on the update loop, so Undo-then-RefreshTable gives FIFO post-undo re-read without extra wrapping"
metrics:
  duration: ~20 min
  completed: 2026-06-12
---

# Phase 15 Plan 10: WorldSnapshot Undo-Stack Clear Seam + Ctrl+Z Routing + Stale-Gizmo Fix Summary

Closes the secondary half of the A9 WS undo-crash cluster and the GAP 2 stale-gizmo polish: a public `UndoRedoManager.Clear()`, a host-wired `IEditorPlugin` undo seam (`Undo`/`Redo`/`ClearUndoStack`), `WorldSnapshotImpl` clearing the editor undo stack + selection gizmo on every snapshot boundary, and `FormSnapshotPlacements` honestly routing Ctrl+Z/Y with a FIFO refresh-after-undo — all built Release|x86 against the paired-rebuilt widened interface across both repos.

## What Shipped

### Task 1 — UndoRedoManager.Clear() + IEditorPlugin undo seam + FormMain wiring (Utinni 8a888b7 + UtinniPlugins 0b7e1a1)
- **`UndoRedoManager.Clear()`** (public): clears both stacks under `lock(syncRoot)`, then fires `onUpdateCommandsCallback()` AFTER releasing the lock (preserves the C-07 / T-02-10 re-entrancy ordering). The private `OnCleanupCallback` now delegates to `Clear()` so the clear idiom lives in exactly one place; scene-cleanup behavior (CON-M-05) is unchanged.
- **`IEditorPlugin` widened** with three settable delegates — `Action Undo`, `Action Redo`, `Action ClearUndoStack` — mirroring the existing settable-from-host `AddUndoCommand` shape. Documented as null-until-wired (callers null-check).
- **`FormMain`** binds each plugin's seam (`editorPlugin.Undo/Redo/ClearUndoStack = undoRedoManager.Undo/Redo/Clear`) in the same per-plugin `CreatePluginControls` loop that already calls `AddUndoCommand`.
- **SDK consistency:** the two `IEditorPlugin` implementers outside `Utinni.sln` (`ExampleEditorPlugin`, `DotNetEditorPluginTemplate`) gain the three auto-properties so the shipped plugin-author SDK stays compilable against the widened interface.
- **TJT `Plugin.cs`** implements the three auto-properties (the build-gate implementer).
- **Test:** `Clear_EmptiesBothStacks_AndFiresUpdateCallback` — pushes 3, undoes 1 (both stacks non-empty), calls `Clear()`, asserts both empty + the update callback fired exactly once. `dotnet test --filter UndoRedoManager` → **6/6 green** (5 prior + 1).

### Task 2 — Clear stack + gizmo on snapshot boundaries; route Ctrl+Z (UtinniPlugins d61b922)
- **`WorldSnapshotImpl`** — `Load`/`Unload`/`Reload` each invoke `editorPlugin.ClearUndoStack?.Invoke()` (3 call sites) so no command built against the old node list survives the native re-read. The native call stays inside `AddUpdateLoopCall`; the clear is a managed null-safe seam call.
- **GAP 2 stale gizmo** — `Unload`/`Reload` + `BulkDelete` (after the delete loop) + `RemoveNode` (on the targeted-node removal) call `DisableGizmo()`; `BulkDelete`/`RemoveNode` also clear the per-node panel controls (`UpdateSelectedNodeControls(null)`, mirroring `OnTarget`'s target-gone path).
- **`FormSnapshotPlacements.ProcessCmdKey`** — `Ctrl+Z` → `editorPlugin.Undo?.Invoke()`, `Ctrl+Y` → `editorPlugin.Redo?.Invoke()` (both null-checked, return true). The undo/redo is enqueued FIRST (its body marshals via `AddUpdateLoopCall`); `RefreshTable()` is called SECOND and self-enqueues its node-list re-read on the same update loop, so FIFO ordering guarantees the grid reflects the post-undo state, never a stale pre-undo snapshot.

## Verification

- `dotnet test UtinniCoreDotNet.Tests --no-build -c Release --filter UndoRedoManager` → **Passed! Failed: 0, Passed: 6** (Clear fact + 5 prior).
- `Utinni.sln` Release|x86 MSBuild **exit 0** (only pre-existing xUnit2013/2020 style warnings).
- `TheJawaToolbox.sln` Release|x86 MSBuild **exit 0** against the widened `IEditorPlugin` — binary-compat verified (TJT is the only build-gate implementer; rebuilt in the paired commit window, so no `MissingMethodException`-at-MEF hazard per `feedback_caller_attrs_binary_compat`).
- Grep gates: `ClearUndoStack` = 3 call sites in WorldSnapshotImpl; `DisableGizmo` present in Unload/Reload/BulkDelete/RemoveNode; `ProcessCmdKey` routes `Ctrl+Z`→Undo and `Ctrl+Y`→Redo with `RefreshTable()` after.
- `Generated/UtinniCore.cs` regen churn reverted (never committed).

## Threat Mitigations (from plan threat register)

- **T-15-10-01 (DoS — stale command replay):** mitigated — undo stack cleared on every snapshot Load/Unload/Reload; combined with the 15-09 null-guards a replayed command cannot reach a dangling deref.
- **T-15-10-02 (Tampering — widened public interface):** mitigated — TJT + both SDK artifacts rebuilt/updated in the same paired commit; no stale plugin DLL ships.
- **T-15-10-SC (installs):** N/A — zero packages installed.

## Deviations from Plan

None of Rules 1–4 triggered. Two structural notes (within plan latitude, not deviations):
- The `StubEditorPlugin` test fixture gained the three new interface members so it satisfies the widened `IEditorPlugin` (required for the Tests project to compile — anticipated by the plan's binary-compat framing).
- `FormSnapshotPlacements` had no stored `editorPlugin` field (the ctor only captured `worldSnapshot` + `ini`); added a `private readonly IEditorPlugin editorPlugin` field + ctor assignment so `ProcessCmdKey` can reach the seam. The plan's `<read_first>` noted "ctor L101 captures editorPlugin" — it captured it as a parameter but did not retain it; retaining it is the minimal change to route the keys.

## Known Stubs

None. No hardcoded empty/placeholder values introduced; all wiring is live.

## Follow-ups

- Live re-verification (Ctrl+Z from the placements window applies + grid reflects revert; gizmo clears on delete/reload; no A9 crash) happens in the reassembled-build smoke (15-11 → 15-08). This plan is the automatable half; the live loop is maintainer-gated.

## Commits

- Utinni `8a888b7` — feat(15-10): UndoRedoManager.Clear() + IEditorPlugin undo seam + FormMain wiring
- UtinniPlugins `0b7e1a1` — feat(15-10): TJT Plugin implements widened IEditorPlugin undo seam
- UtinniPlugins `d61b922` — feat(15-10): clear undo stack + gizmo on snapshot boundaries; route Ctrl+Z from placements

## Self-Check: PASSED

- Utinni `8a888b7` — FOUND; UtinniPlugins `0b7e1a1` — FOUND; UtinniPlugins `d61b922` — FOUND
- `15-10-SUMMARY.md` — FOUND
- All modified source files committed to their own repos; no untracked artifacts left behind
