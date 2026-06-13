---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 09
subsystem: worldsnapshot-undo
tags: [PROD-W2-WS, gap-closure, crash-fix, null-guard, A9]
requires:
  - "WorldSnapshotCommands (shipped four WS IUndoCommands)"
  - "GroundSceneCallbacks.AddUpdateLoopCall (game-thread marshalling)"
provides:
  - "WorldSnapshotCommandGuard (pure BCL-only bail-on-null decision helper)"
  - "Null-safe Execute/Undo bodies for all four WS IUndoCommands"
affects:
  - "TJT WorldSnapshot bulk-op Undo (A9 live re-verify in 15-11 -> 15-08)"
tech-stack:
  added: []
  patterns:
    - "Framework-leg discipline (checker B-1): pure BCL-only decision helper, native bridge untouched, decision unit-tested in xUnit host"
    - "Resolve-then-guard-then-deref ordering for nullable native lookups"
key-files:
  created:
    - "UtinniCoreDotNet/Commands/WorldSnapshotCommandGuard.cs"
    - "UtinniCoreDotNet.Tests/Commands/WorldSnapshotCommandGuardTests.cs"
  modified:
    - "UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs"
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj"
decisions:
  - "Two-arg ShouldApply(obj, node) gates position/rotation (both dereferenced); single-arg ShouldApply(node) gates the removal path"
  - "ParentNode guarded for null BEFORE reading .LastChild / .GetChildById (ParentNode itself can be null)"
  - "AddNode(nodeCopy) paths left unguarded: nodeCopy is a ctor-captured copy, never null"
metrics:
  duration: ~5 min
  completed: 2026-06-13
---

# Phase 15 Plan 09: A9 WorldSnapshot Undo-Crash Null-Guards Summary

Closed the BLOCKING 15-SMOKE Checklist A9 crash (GAP 1.1): editor Undo of any WorldSnapshot bulk op (Move / Rotation / Add / Remove) null-deref'd the SWG client (`0xC0000005` READ target=0x0) because the four WS `IUndoCommand` bodies dereferenced `Network.GetObjectById` / `WorldSnapshotReaderWriter` node lookups with no null guard — and, at the confirmed crash site, dereferenced `obj` BEFORE the `node` lookup was even resolved. Extracted the bail-on-null decision into a pure `WorldSnapshotCommandGuard` helper (unit-covered, native-bridge-free) and restructured every command body to resolve object+node first, guard, then dereference.

## What Was Built

### Task 1 — Pure `WorldSnapshotCommandGuard` helper + unit coverage (commit `43b9dc9`)
- `UtinniCoreDotNet/Commands/WorldSnapshotCommandGuard.cs`: BCL-only static helper with `ShouldApply(object)` (returns `lookup != null`) and `ShouldApply(object, object)` (returns `objLookup != null && nodeLookup != null`). `object`-typed params keep it free of the native `UtinniCore.Utinni.Object`/`Node` CppSharp types so it loads in the xUnit host. Same framework-leg discipline (checker B-1) as `WorldSnapshotBulkComposer` / `LooseOverridePath` / `CsvCellCoercion` — no native bridge, no WinForms, no update-loop callbacks.
- `UtinniCoreDotNet.Tests/Commands/WorldSnapshotCommandGuardTests.cs`: 4 facts (6 cases via a `[Theory]`) — null single → false, non-null single → true, any-null pair → false, both-non-null → true. Class name contains `WorldSnapshotCommandGuard` for the `--filter` gate.
- Registered the new source in the old-style `UtinniCoreDotNet.csproj` (`<Compile Include>` — the project does not glob).

### Task 2 — Null-guard all four WS command Execute/Undo bodies (commit `08eeb51`)
- **`SetPosition` (the confirmed A9 crash site):** hoisted the `node` resolution (`ParentNode.GetChildById` / `Get().GetNodeById`) ABOVE the first `obj` deref; single `ShouldApply(obj, node)` gate with early `return;` now precedes ALL of `obj.Transform.Position`, `obj.PositionAndRotationChanged`, and `node.Transform.Position`. No `obj.`/`node.` deref precedes the guard.
- **`SetRotation`:** identical restructure — node hoisted, single `ShouldApply(obj, node)` gate before `obj.Transform.CopyRotation`, `obj.PositionAndRotationChanged`, `node.Transform.CopyRotation`.
- **`AddWorldSnapshotNodeCommand.Undo()` + `RemoveWorldSnapshotNodeCommand.Execute()` (shared removal shape):** guard `nodeCopy.ParentNode` for null BEFORE reading `.LastChild`; capture the resolved node (`LastNode` / `ParentNode.LastChild`) to a local and skip `WorldSnapshot.RemoveNode` via `ShouldApply(node)` when null instead of passing a null into the native remove.
- `AddNode(nodeCopy)` paths left as-is (ctor-captured copy, never null). All bodies stay inside their `GroundSceneCallbacks.AddUpdateLoopCall` game-thread wrappers — marshalling unchanged. `Generated/UtinniCore.cs` regen churn reverted (never committed).

## Verification

- `dotnet test ... --filter WorldSnapshotCommandGuard` → 6 passed, 0 failed.
- Full `UtinniCoreDotNet.Tests` Release suite → **696 passed, 0 failed** (690 baseline + 6 new guard cases; no regression).
- `Utinni.sln` Release|x86 MSBuild → exit 0.
- Read-back confirms: node resolution hoisted above first obj deref in both `SetPosition`/`SetRotation`; single two-arg `ShouldApply` gate before any obj/node deref; `ParentNode` guarded before `.LastChild`; `RemoveNode` skipped on null.
- `Generated/UtinniCore.cs` reverted (git status clean for that file).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] MSBuild path + multi-target invocation**
- **Found during:** Task 1 verify.
- **Issue:** The plan's verify string uses `%ProgramFiles%` which Git-Bash's `cmd /c` mangled on the embedded space (`'ode' is not recognized`); and the `-t:UtinniCoreDotNet;UtinniCoreDotNet_Tests` target list applies to every solution project (UtinniCore-Symbols / UtinniCoreDotNetGen lack that target → MSB4057).
- **Fix:** Invoked MSBuild via its resolved absolute path (`D:\Program Files\Microsoft Visual Studio\18\Community\...` — `%ProgramFiles%` resolves to `D:` on this box per the VS-2026 toolchain note) and built the SDK-style `UtinniCoreDotNet.Tests.csproj` directly (it project-references `UtinniCoreDotNet`, so both compile). Functionally identical coverage; no source impact.

**2. [Rule 3 - Blocking] New source not picked up by old-style csproj**
- **Found during:** Task 1 build (CS0103 `WorldSnapshotCommandGuard` does not exist).
- **Issue:** `UtinniCoreDotNet.csproj` is an old-style (non-SDK) project that enumerates files via explicit `<Compile Include>`; a newly-created `.cs` is invisible until listed.
- **Fix:** Added `<Compile Include="Commands\WorldSnapshotCommandGuard.cs" />`. (The test project IS SDK-style and globs, so no test-csproj edit was needed.) Committed with Task 1.

**3. [Doc] Comment reword for grep-gate hygiene**
- The Task-1 acceptance greps for absence of `System.Windows.Forms` / `UtinniCore.Utinni` / `GroundSceneCallbacks` in the guard source. The original header comment named those tokens descriptively; reworded to describe them without the literal tokens (per the standing grep-gate hygiene practice) so the gate reads clean. No behavior change.

## Known Stubs

None — both the helper and the command-body guards are fully wired and exercised.

## Threat Flags

None — this plan closes the existing availability defect (T-15-09-01 mitigate) and introduces no new trust-boundary surface.

## Notes / Follow-ups

- The helper test covers the bail-on-null DECISION; it is NOT a substitute for the integrated native crash path. Live re-verification of Checklist A9 (Undo after a bulk op against a now-absent in-world object) happens in the reassembled-build smoke (15-11 → 15-08), per the plan's success criteria.

## Self-Check: PASSED
- FOUND: UtinniCoreDotNet/Commands/WorldSnapshotCommandGuard.cs
- FOUND: UtinniCoreDotNet.Tests/Commands/WorldSnapshotCommandGuardTests.cs
- FOUND (modified): UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs
- FOUND: commit 43b9dc9 (Task 1)
- FOUND: commit 08eeb51 (Task 2)
