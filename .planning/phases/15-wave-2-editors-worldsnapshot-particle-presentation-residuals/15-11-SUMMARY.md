---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 11
subsystem: build-handoff
tags: [gap-closure, release-gate, injection-build, content-verification, worldsnapshot, undo]
requires: ["15-09", "15-10"]
provides:
  - "Reassembled, content-verified injection build at D:/Code/Utinni/bin/Release/ carrying the A9 undo-crash fix"
  - "Green full automated gate (managed + native) with zero regression post gap-fix"
  - "A9 re-verify pointer recorded in 15-SMOKE.md back to plan 15-08"
affects:
  - "15-08 Task 2 maintainer live-SWG smoke (Checklist A9 re-verify resumes against the fixed binaries)"
tech-stack:
  added: []
  patterns:
    - "Content-of-fix gate via reflection-only type/method enumeration of the DEPLOYED PEs (anti-stale; mtime is only a weak secondary signal)"
key-files:
  created:
    - ".planning/phases/15-wave-2-editors-worldsnapshot-particle-presentation-residuals/15-11-SUMMARY.md"
  modified:
    - ".planning/phases/15-wave-2-editors-worldsnapshot-particle-presentation-residuals/15-SMOKE.md"
  artifacts:
    - "D:/Code/Utinni/bin/Release/UtinniCoreDotNet.dll (rebuilt — defines WorldSnapshotCommandGuard + UndoRedoManager.Clear)"
    - "D:/Code/Utinni/bin/Release/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll (rebuilt — references ClearUndoStack)"
decisions:
  - "Task 1 produces only gitignored build output (bin/Release/); the deliverable is the on-disk content-verified layout, so there is no source commit for Task 1 — only Task 2's 15-SMOKE.md annotation is committed (c2d5d31)."
  - "Content-of-fix gate implemented via .NET Framework reflection-only assembly load (ReflectionOnlyLoadFrom + type/method enumeration) instead of System.Reflection.Metadata.MetadataReader, because Windows PowerShell 5.x does not ship System.Reflection.Metadata; reflection-only enumeration is a precise (stronger-than-strings) equivalent and the plan explicitly permits MetadataReader OR ildasm/strings."
metrics:
  duration: ~39 min
  completed: 2026-06-13
---

# Phase 15 Plan 11: Gap-Closure Release Gate + Reassembled Injection Build Summary

Reassembled the deployable Release|x86 injection build at `D:/Code/Utinni/bin/Release/` after the 15-09/15-10 A9 undo-crash gap-fixes, re-ran the full automated gate green with zero regression, content-verified the DEPLOYED DLLs actually carry the fix (not just a fresh mtime), and recorded the A9 live re-verify pointer back into `15-SMOKE.md` under plan 15-08.

## What Shipped

**Task 1 — Full Release gate + reassembled, content-verified injection build:**
- `Utinni.sln` Release|x86 (VS2026 MSBuild v145) -> exit 0. `UtinniCoreDotNet` builds directly into `bin/Release/` (per its csproj `OutputPath`), so the managed core DLL is reassembled by the build itself.
- `TheJawaToolbox.sln` Release|x86 (UtinniPlugins, paired rebuild against the widened `IEditorPlugin`) -> exit 0. Both TJT DLLs build directly into `bin/Release/Plugins/TheJawaToolbox/`.
- Full automated gate, zero regression vs the 15-08 Task 1 baseline (the new 15-09/15-10 facts are additive):
  - `UtinniCoreDotNet.Tests` (Release): **697 passed / 0 failed** (>= 690 baseline + the new guard + Clear facts).
  - `Utinni.Cli.Tests` (Release): **249 passed / 2 skipped / 0 failed**.
  - `Utinni.Mcp.Tests` (net10, Release): **77 passed / 0 failed**.
  - Native `UtinniCore.Tests.exe` (full): **84 assertions / 27 cases**.
  - Native `UtinniCore.Tests.exe [resid04]`: **8 assertions / 1 case**.
- `Generated/UtinniCore.cs` CppSharp churn reverted (`git checkout --`; never committed). Working tree clean.
- `bin/Release/` layout present and complete: `Launcher.exe`, `UtinniCore.dll`, `UtinniCoreDotNet.dll`, `Plugins/TheJawaToolbox/{TheJawaToolbox.dll, TheJawaToolboxDotNet.dll, Resources/, input.ini, settings.ini}`.
- **Content-of-fix gate PASSED (binding anti-stale gate, T-15-11-01 mitigation):** via reflection-only type/method enumeration of the DEPLOYED PEs —
  - deployed `UtinniCoreDotNet.dll` defines type `WorldSnapshotCommandGuard` (proof the 15-09 guard shipped), AND
  - deployed `UtinniCoreDotNet.dll`'s `UndoRedoManager` exposes a public `Clear` method (proof the 15-10 seam shipped), AND
  - deployed `Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` references `ClearUndoStack` (proof the paired TJT rebuild against the widened seam shipped).
  - The freshly-rebuilt DLL mtimes (UtinniCoreDotNet.dll 21:44, TheJawaToolboxDotNet.dll 21:46 — both after the 15-09/15-10 fix commits) are recorded only as a weak secondary signal; the content markers are the binding proof.

**Task 2 — Gap-closure note + A9 re-verify pointer in 15-SMOKE.md (commit `c2d5d31`):**
- Annotated the Checklist A9 row: `FIX SHIPPED (15-09/15-10); AWAITING LIVE RE-VERIFY against the reassembled build (under 15-08)` — prepended, original crash record preserved.
- Appended a dated `GAP-CLOSURE 2026-06-13` note referencing 15-09 (null-guards via `WorldSnapshotCommandGuard`), 15-10 (`UndoRedoManager.Clear()` seam + Load/Unload/Reload stack-clear + `Ctrl+Z`/`Ctrl+Y` routing + stale-gizmo clear), and 15-11 (reassembled + content-verified build), with the A9-re-verify-under-15-08 pointer.
- Original A9 crash evidence (the `0xC0000005` records + both DEFECT blocks) preserved verbatim (4 `0xC0000005` occurrences intact). Maintainer Sign-Off block untouched (still unsigned). This plan does NOT sign off the phase.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Content-of-fix gate engine swapped from MetadataReader to reflection-only load**
- **Found during:** Task 1, step 5 (content verification).
- **Issue:** Windows PowerShell 5.x (the default `powershell` on this machine) does not ship `System.Reflection.Metadata` in the GAC, so `System.Reflection.PortableExecutable.PEReader` could not be instantiated; the plan's verify-command also uses a `[Text.Encoding]::Latin1` strings grep that conflates a member named `Clear` with the unrelated common token.
- **Fix:** Implemented the content-of-fix gate via .NET Framework reflection-only assembly load (`Assembly.ReflectionOnlyLoadFrom` + `GetTypes()` + per-type `GetMethods(DeclaredOnly)`), which is a precise, stronger-than-strings enumeration that the plan explicitly permits ("MetadataReader OR ildasm/strings grep"). The `WorldSnapshotCommandGuard` type and `UndoRedoManager.Clear` method are matched by exact name; `ClearUndoStack` is matched by string presence in the TJT PE (it appears as a member reference, which the strings path covers).
- **Files modified:** none committed (verification-only temp script, removed after use).
- **Commit:** n/a (gate logic, not a source change).

### Scope/commit note (not a deviation)

- Task 1's only output is the gitignored `bin/Release/` build layout (the csproj `OutputPath` + the TJT vcxproj/csproj output target write there directly). The deliverable is the on-disk content-verified layout, so there is no source commit for Task 1 — consistent with the plan's `files_modified: [D:/Code/Utinni/bin/Release/]`. The only committable artifact is Task 2's `15-SMOKE.md` annotation (`c2d5d31`).
- Native `UtinniCore.dll` was not recompiled (no native source change since 15-05; MSBuild correctly skipped it). The A9 fix lives entirely in the managed `UtinniCoreDotNet.dll`, which was rebuilt — confirmed by the content-of-fix gate.

## Known Stubs

None. This plan is a build/handoff seam; it adds no new feature code or data wiring.

## Authentication Gates

None.

## Verification Summary

| Check | Result |
|-------|--------|
| `Utinni.sln` Release\|x86 (MSBuild v145) | exit 0 |
| `TheJawaToolbox.sln` Release\|x86 | exit 0 |
| `UtinniCoreDotNet.Tests` (Release) | 697 passed / 0 failed |
| `Utinni.Cli.Tests` (Release) | 249 passed / 2 skipped / 0 failed |
| `Utinni.Mcp.Tests` (net10, Release) | 77 passed / 0 failed |
| Native `UtinniCore.Tests.exe` full | 84 assertions / 27 cases |
| Native `UtinniCore.Tests.exe [resid04]` | 8 assertions / 1 case |
| `Generated/UtinniCore.cs` churn | reverted (clean) |
| bin/Release layout | all required files present |
| Content-of-fix: core `WorldSnapshotCommandGuard` type | PRESENT |
| Content-of-fix: core `UndoRedoManager.Clear` method | PRESENT |
| Content-of-fix: TJT `ClearUndoStack` reference | PRESENT |
| 15-SMOKE.md gap-closure note (15-09/15-10/15-11) | present (4/5/3 token occurrences) |
| 15-SMOKE.md original crash evidence (`0xC0000005`) | intact (4 occurrences) |
| Maintainer Sign-Off block | unchanged (unsigned) |

## Handoff

The maintainer can now resume the **15-08 Task 2** live-SWG smoke against the fixed, content-verified `D:/Code/Utinni/bin/Release/` build: inject it, re-run the A9 undo path (it should now reverse atomically without crashing the client), and record the A9 re-verify result back into `15-SMOKE.md`. Checklists B/C/D and any Checklist A re-run remain the maintainer's continuation under 15-08. The phase gate is the still-unsigned Maintainer Sign-Off block.

## Self-Check: PASSED

- FOUND: `.planning/phases/15-wave-2-.../15-11-SUMMARY.md`
- FOUND: commit `c2d5d31` (Task 2 smoke annotation)
- FOUND: deployed `bin/Release/UtinniCoreDotNet.dll` + `bin/Release/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` (content-verified for the fix markers)
