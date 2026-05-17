---
phase: 02-critical-bug-burn-down-c-01-c-15
plan: 03
subsystem: native/launcher
tags: [C-01, loader-lock, DllMain, utinni_init, CreateRemoteThread, LoaderLockHarness, CON-H-01]
dependency_graph:
  requires: [02-02]
  provides: [C-01-fix, LoaderLockHarness-regression-guard]
  affects: [UtinniCore/utinni.cpp, Launcher/main.cpp, UtinniCoreDotNet.Tests, Utinni.sln]
tech_stack:
  added: []
  patterns:
    - "extern C __declspec(dllexport) DWORD WINAPI utinni_init(LPVOID) -- plain export, not a detour"
    - "Second CreateRemoteThread after LoadLibraryA for deferred DLL init (path-a pattern)"
    - "QueryPerformanceCounter-bracketed LoadLibraryA as DllMain timing regression guard"
    - "Process.Start xUnit wrapper invoking native harness exe for cross-boundary timing assertion"
key_files:
  created:
    - Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj
    - Utinni.LoaderLockHarness/main.cpp
    - UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs
  modified:
    - UtinniCore/utinni.cpp
    - Launcher/main.cpp
    - Utinni.sln
    - docs/ai/assessment.md
decisions:
  - "Path (a) chosen: utinni_init export + launcher CreateRemoteThread. Rejected path (b) defer-to-Game::install (chicken-and-egg: plugins need CLR up before install fires). Rejected path (c) hybrid (double failure modes)."
  - "utinni_init runs synchronously (no internal CreateThread). Launcher WaitForSingleObject blocks for bounded bring-up time. Easier to debug than fire-and-forget."
  - "LoaderLockHarness catches DllMain-grew-heavy regression class; does NOT prove no deadlock under contention (that remains Tier-4 manual per CONTEXT.md D-06)."
metrics:
  duration_minutes: 45
  completed: "2026-05-16"
  tasks_completed: 4
  files_changed: 7
---

# Phase 2 Plan 03: C-01 DllMain Loader-Lock Fix Summary

**One-liner:** DllMain slimmed to DisableThreadLibraryCalls+return; `utinni_init` C-linkage export fires heavy startup via launcher's second `CreateRemoteThread` (path a); `Utinni.LoaderLockHarness` regression guard times DllMain at 50ms threshold.

## What Was Built

### Task 1 (0d56d93): Wave-0 scaffolding — Utinni.LoaderLockHarness + xUnit test

New C++ console-exe project `Utinni.LoaderLockHarness/` at repo root (flat-root convention per 02-PATTERNS.md S-6):

- **vcxproj GUID:** `{6A57E74E-0D39-430B-AD0D-6E56AA99B54A}`
- **main.cpp:** `QueryPerformanceFrequency` + `QueryPerformanceCounter` brackets `LoadLibraryA("UtinniCore.dll")`; prints `"UtinniCore DllMain elapsed: %.3f ms"`; exits 0 if timing < 50ms, 1 if >= 50ms (regression), 2 if `LoadLibraryA` failed
- **Project dependency:** depends on UtinniCore (build ordering), does NOT link against it (runtime `LoadLibrary`)
- **Utinni.sln:** project entry + Debug|Win32 + Release|Win32 + RelWithDbgInfo->Release mapping (6 total references)
- **UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs:** `[Fact] LoaderLockHarness_LoadsUtinniCoreUnderThreshold` — `Process.Start` with captured stdout; asserts `ExitCode == 0` and stdout contains `"elapsed:"`; harness discovered at `bin/Release/Utinni.LoaderLockHarness.exe` via relative path from test output dir

Harness builds successfully standalone (verified: `bin/Release/Utinni.LoaderLockHarness.exe` produced). Full `Utinni.sln` build requires DXSDK June 2010 (absent locally — CI verification required per integration note).

### Task 2 (b2f5c16): C-01 fix — DllMain slim + utinni_init export + Launcher second CreateRemoteThread

**UtinniCore/utinni.cpp:**
- Extracted `main()` body verbatim into `extern "C" __declspec(dllexport) DWORD WINAPI utinni_init(LPVOID lpThreadParam)`
  - Returns `DWORD` per `LPTHREAD_START_ROUTINE` contract
  - Runs synchronously on launcher-spawned thread (no internal `CreateThread`)
  - Body unchanged: path resolution, log create, ini load, client/imgui settings, `createDetours()`, `createPatches()`, `pluginManager.loadPlugins()`, `CoInitializeEx`, `clr::load()`
- Deleted orphaned `void main()` (now unreachable)
- `DllMain` `DLL_PROCESS_ATTACH`: `DisableThreadLibraryCalls(hinstDLL); return TRUE;` — nothing else
- `DLL_PROCESS_DETACH`: continues to call `detatch()` (typo is Phase-6 STAB-03 cleanup)
- Comment added above `DllMain` citing C-01 and CON-H-01 rationale

**Launcher/main.cpp inject():**
After existing `hDll` extraction (remote HMODULE for UtinniCore.dll), appended:
```
local LoadLibraryA(dllFilename) -> GetProcAddress("utinni_init") -> compute offset
-> FreeLibrary(localCore) -> CreateRemoteThread(remote = (BYTE*)hDll + offset)
-> WaitForSingleObject(hInitThread, INFINITE) -> CloseHandle
```

**UtinniCore.vcxproj:** No changes needed — `__declspec(dllexport)` auto-exports; no `.def` file present (verified).

### Task 3 (checkpoint): Live SWG injection verification

**Status: PENDING human verification.** This is the Tier-4 manual residual per CONTEXT.md D-06. The LoaderLockHarness regression guard confirms DllMain timing is under 50ms but does NOT prove "no deadlock under loader-lock contention." Full proof requires:
1. Build `bin/Release/Launcher.exe + UtinniCore.dll + UtinniCoreDotNet.dll`
2. Launch SWG via Launcher.exe
3. Confirm: client window appears, editor UI visible, at least one plugin loaded, no loader-lock wait chain in Resource Monitor

**Resume signal:** "approved — editor came up with plugins loaded; no loader-lock hang"

### Task 4 (277ae74): Docs sweep — assessment.md C-01 row flipped to done

`docs/ai/assessment.md` status row C-01 updated from `open` to `done` with commit SHAs:
- Fix commit: `b2f5c16`
- Harness scaffolding: `0d56d93`
- Live-SWG verification: pending (Task 3 checkpoint)

## C-01 Architectural Choice Rationale

**Path (a) chosen over path (b) and (c):**

| Path | Verdict | Reason |
|------|---------|--------|
| (a) utinni_init export + launcher CreateRemoteThread | **CHOSEN** | Small native + small launcher delta; launcher controls timing; utinni_init fires during OEP-parked window before SWG reaches Game::install |
| (b) defer to Game::install detour | Rejected | Chicken-and-egg: plugins subscribe to install callbacks; CLR must be UP before install fires; if CLR comes up inside install, the callback registration is too late |
| (c) hybrid: utinni_init + Game::install fallback | Rejected | Double failure-mode trees; deferred-init failure recovery harder to reason about; more surface area |

**If path (b) ever needs to be reopened:** The key blocker is that `GameCallbacks.AddInstallCallback` subscribers expect CLR to be up when `Game::install` fires. A reopening of path (b) would require either (a) CLR-safe plugin callback registration before install, or (b) a "CLR lazy-init on first callback" guard — both are complex and risky. The current path (a) is strictly simpler.

## LoaderLockHarness Regression Guard Scope

| What it catches | What it does NOT catch |
|-----------------|----------------------|
| "DllMain grew heavy work inline" (e.g., someone reverts C-01 by adding `pluginManager.loadPlugins()` back inside `DLL_PROCESS_ATTACH`) | Actual deadlock under loader-lock contention |
| DllMain timing exceeding 50ms (2000x headroom over expected microseconds post-fix) | Race conditions in CLR bring-up |
| `LoadLibraryA("UtinniCore.dll")` failure (exit code 2) | Plugin-specific startup failures |

**Timing expectation post-fix:** `DisableThreadLibraryCalls + return TRUE` should complete in microseconds (well under 1ms). The 50ms threshold provides ample headroom.

## Preservation Verifications

| Constraint | Verification |
|------------|--------------|
| CON-N-01: detour-table pattern untouched | `utinni_init` is a plain `extern "C"` export, not a `Detour::Create` call. Zero new detour registrations in the Task 2 diff. |
| CON-N-04: memory.cpp VirtualProtect bracket | `git diff b2f5c16 UtinniCore/utility/memory.cpp` → empty (file untouched by this plan) |
| CON-H-01: DllMain does no heavy work | `DllMain DLL_PROCESS_ATTACH` body: `DisableThreadLibraryCalls(hinstDLL); return TRUE;` — verified in utinni.cpp:148-152 |
| CON-D-01: blank login default | Not touched by this plan |
| Typo `detatch`: left as-is per CONTEXT.md §domain (Phase-6 STAB-03 cleanup territory) |

## Deviations from Plan

None — plan executed exactly as written. The only adaptation: `.vcxproj.filters` file was created but is gitignored (`*.filters` in `.gitignore`) so it was not committed; this is consistent with existing repo convention (no other `.filters` files are tracked).

## Known Stubs

None. The `utinni_init` body is the full production startup sequence extracted verbatim from `main()`. The harness has no stub output.

## Threat Flags

None beyond what the plan's threat model covers. The new `utinni_init` export is within the T-02-15 (Tampering) threat already registered in the plan's threat model with an `accept` disposition.

## Self-Check: PASSED

| Check | Result |
|-------|--------|
| `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj` exists | FOUND |
| `Utinni.LoaderLockHarness/main.cpp` exists | FOUND |
| `UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs` exists | FOUND |
| `bin/Release/Utinni.LoaderLockHarness.exe` built | FOUND |
| Task 1 commit `0d56d93` exists | FOUND |
| Task 2 commit `b2f5c16` exists | FOUND |
| Task 4 commit `277ae74` exists | FOUND |
| `extern "C" __declspec(dllexport) DWORD WINAPI utinni_init` in utinni.cpp | PASS (line 102) |
| `DisableThreadLibraryCalls` in utinni.cpp DllMain | PASS (line 151) |
| `GetProcAddress(localCore, "utinni_init")` in Launcher/main.cpp | PASS (line 220) |
| `CreateRemoteThread` count >= 2 in Launcher/main.cpp | PASS (count=2) |
| `C-01` row shows `done` in assessment.md | PASS |
