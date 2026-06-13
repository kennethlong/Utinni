---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 14
subsystem: ui
tags: [worldsnapshot, undo-redo, swg-injection, csharp, winforms, gap-closure]

# Dependency graph
requires:
  - phase: 15-09
    provides: WorldSnapshotCommandGuard bail-on-null helper + null-guarded WS IUndoCommand bodies (crash fix)
  - phase: 15-10
    provides: UndoRedoManager.Clear() seam + FormSnapshotPlacements Ctrl+Z/Y routing
provides:
  - "Finalized A9 WS undo REVERT fix: SetPosition/SetRotation resolve the LIVE node by id from the live snapshot tree, guard node-only (in-world object optional), and revert the node data so the table/state reflect the undo"
  - "Diagnostic-free WorldSnapshotCommands.cs (all 7 [A9-diag] Log.Info lines stripped; unused Utility using removed) ready for the 15-17 reassembled injection build"
  - "Explicit node-only-semantics unit coverage on WorldSnapshotCommandGuard.ShouldApply(node)"
affects: [15-17, 15-18]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Live-node-by-id resolution in WS undo (GetNodeById + GetChildById live-parent fallback) mirrors the working TJT BulkMove path — never trust a copied node's ParentNode linkage"
    - "Undo reverts the snapshot DATA (node-required); the in-world object is OPTIONAL (moved only when instantiated)"

key-files:
  created: []
  modified:
    - "UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs"
    - "UtinniCoreDotNet.Tests/Commands/WorldSnapshotCommandGuardTests.cs"

key-decisions:
  - "node is the only true bail (nothing to revert); a null in-world object never blocks the data revert"
  - "WorldSnapshotCommandGuard already exposed both overloads (15-09) — no guard-source change needed; only test coverage added"

patterns-established:
  - "WS undo finalize: live-node-by-id + node-only ShouldApply(node) + optional-obj branch + node-data revert + DetailLevelChanged"

requirements-completed: [PROD-W2-WS]

# Metrics
duration: ~20min
completed: 2026-06-13
---

# Phase 15 Plan 14: Finalize A9 WS Undo Revert + Strip Diagnostics Summary

**WorldSnapshot bulk Move/Rotate undo now actually REVERTS (was a silent no-op even with the 15-09 crash guard holding): SetPosition/SetRotation resolve the live node by id, guard node-only with the in-world object optional, revert the node data, and ship diagnostic-free with all [A9-diag] logging stripped.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-06-13 (post-handoff)
- **Completed:** 2026-06-13
- **Tasks:** 1
- **Files modified:** 2 (plus 1 deferred-items log)

## Accomplishments
- Finalized the live-verified in-tree A9 revert fix: `SetPosition` + `SetRotation` resolve the LIVE node via `WorldSnapshotReaderWriter.Get().GetNodeById(nodeCopy.Id)` (with the `GetChildById` live-parent fallback for `ParentId > 0`) instead of the copied node's dead `ParentNode` linkage, guard node-only via `WorldSnapshotCommandGuard.ShouldApply(node)`, move the in-world object only inside an `if (obj != null)` branch, revert the node data, and call `WorldSnapshot.DetailLevelChanged()`.
- Stripped all 7 temporary `[A9-diag]` `Log.Info` lines (resolve-trace + BAILED + APPLIED, across both methods) and removed the now-unused `using UtinniCoreDotNet.Utility;` import — the deployed build carries zero diagnostic logging on the undo hot path.
- Added explicit node-only-semantics unit coverage (`ShouldApply_NullNode_ReturnsFalse_BailNothingToRevert` + `ShouldApply_NonNullNode_ReturnsTrue_ProceedToRevert`); the existing single-arg + two-arg facts remain green.

## Task Commits

1. **Task 1: Strip [A9-diag] logging + finalize the revert fix; reconcile the node-only guard + facts** - `c7cbd8a` (fix)

**Plan metadata:** (this docs commit)

## Files Created/Modified
- `UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs` - Finalized A9 revert fix; zero [A9-diag] logging; unused Utility using removed
- `UtinniCoreDotNet.Tests/Commands/WorldSnapshotCommandGuardTests.cs` - Added explicit node-only-semantics facts (8 guard facts total)
- `.planning/.../deferred-items.md` - Logged the out-of-scope D3D9 `GetVtbl` harness failure

## Decisions Made
- The `WorldSnapshotCommandGuard.cs` source was NOT changed: it already exposed both `ShouldApply(object lookup)` (node-only) and `ShouldApply(object, object)` overloads from 15-09. The plan's third listed file only needed confirmation, not modification — only the test got new facts.
- Kept the two-arg `ShouldApply(obj, node)` overload (public API; still used by the Add/Remove command bodies' single-arg guards and asserted by 15-09 tests).

## Deviations from Plan

None — plan executed exactly as written. (The guard source needed no edit because both overloads already existed per 15-09; this matches the plan's interface note.)

## Issues Encountered
- **Out-of-scope test failure (NOT a regression):** the full `UtinniCoreDotNet.Tests` Release suite shows 1 failure — `FindPatternHarnessTests.GetVtbl_WithD3d9Loaded_ReturnsNonZero` — because the dummy-device `CreateDevice(HAL)` path has no graphics adapter in the headless test process (the test's own comment documents this exact caveat and pre-stages a `[Fact(Skip=...)]`). It is entirely unrelated to the WorldSnapshot undo code this plan touched. Excluding that one environment-dependent harness test, **695/695 pass**, including the 8 WorldSnapshotCommandGuard facts. Logged to `deferred-items.md` per the SCOPE BOUNDARY rule; not fixed here.

## Build / Test Verification
- `Utinni.sln` Release|x86 — **Build succeeded, 0 errors** (only pre-existing xUnit2013/xUnit2020 analyzer warnings, out of scope).
- `dotnet test --filter WorldSnapshotCommandGuard` — **8 passed, 0 failed.**
- Full `UtinniCoreDotNet.Tests` Release — 698 passed / 1 failed (the out-of-scope D3D9 `GetVtbl` adapter test above); 695/695 pass with that harness test excluded.
- `grep A9-diag` and `grep Log.Info` in `WorldSnapshotCommands.cs` — no matches.
- `Generated/UtinniCore.cs` — reverted (git clean; never committed).

## Next Phase Readiness
- `WorldSnapshotCommands.cs` is diagnostic-free and ready for the 15-17 reassembled bin/Release injection build.
- Live A9 re-verify (already PASS 2026-06-13 on the pre-strip build) re-confirms against the diagnostic-free 15-17 build in 15-18.

## Self-Check: PASSED

- FOUND: UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs (diagnostic-free)
- FOUND: UtinniCoreDotNet.Tests/Commands/WorldSnapshotCommandGuardTests.cs (node-only facts)
- FOUND: commit c7cbd8a

---
*Phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals*
*Completed: 2026-06-13*
