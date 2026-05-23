---
phase: 02-critical-bug-burn-down-c-01-c-15
verified: 2026-05-17T00:00:00Z
status: passed
human_uat_resolved: 2026-05-18T18:05:10Z
human_uat_resolution_artifact: .planning/phases/02-critical-bug-burn-down-c-01-c-15/02-HUMAN-UAT.md
score: 4/4 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Inject UtinniCore.dll into live SWG client via Launcher.exe. Confirm: SWG client window appears within ~2s, editor UI is visible, at least one plugin loads (shown in editor UI or utinni.log), and no thread is blocked on LdrpDrainWorkQueue in Resource Monitor Wait Chain Analysis."
    expected: "SWG launches cleanly, editor comes up with plugins loaded, no loader-lock hang."
    why_human: "LoaderLockHarness proves DllMain timing is under 50ms but cannot prove no deadlock under actual loader-lock contention. Full proof requires injection into a live SWG client process. Deferred to Tier-4 per CONTEXT.md D-06 and confirmed in STATE.md."
    result: passed
    resolved: 2026-05-18T18:03:09Z
    evidence: "Editor host + The Jawa Toolbox plugin came up alongside the SWG client window; control flowed past LoadLibrary -> utinni_init -> CLR -> MEF plugin discovery -> FormMain -> TJT visible. User resume signal: 'both windows came up and I can select Jawa Toolbox'. Full record in 02-HUMAN-UAT.md."
  - test: "With UtinniCore.dll injected into a live SWG client, minimize and restore the SWG client window 5+ times in quick succession. Optionally repeat 20 times without pause."
    expected: "No UI hang (window animates smoothly), no CPU spike on UI thread, editor remains responsive. Confirms EventWaitHandle.WaitOne(100ms) replaced the Thread.Sleep(1) spin correctly end-to-end."
    why_human: "Mock-signaller tests (FormMainSignallerTests.cs) prove timeout and signal semantics via a fake EventWaitHandle, but the actual SetEvent signal path requires the real D3D9 hkPresent detour running inside a live SWG client. Deferred to Tier-4 per CONTEXT.md D-06 and confirmed in STATE.md."
    result: passed
    resolved: 2026-05-18T18:05:10Z
    evidence: "Minimize/restore stress test against the live SWG client window; editor host stayed responsive throughout, no UI thread CPU spike. User resume signal: 'C-09 complete'. Full record in 02-HUMAN-UAT.md."
---

# Phase 02: Critical Bug Burn-Down (C-01..C-15) Verification Report

**Phase Goal:** All 15 critical bugs enumerated in assessment.md are closed. Framework no longer exhibits the listed silent failures, crashes, or data losses; class-of-bug constraints CON-H-01..-05, CON-L-01..-04, CON-B-04, CON-D-01 are honoured going forward.
**Verified:** 2026-05-17
**Status:** passed (Tier-4 UAT resolved 2026-05-18; see `human_uat_resolved` in frontmatter)
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Every C-01 through C-15 item is closed in code and verified — no observable regression of the listed symptoms | VERIFIED | All 15 rows in `docs/ai/assessment.md` status table show `done` with verified commit SHAs (see artifact table below). Code-level spot checks confirm implementations are substantive, not stubs. |
| 2 | CI (from Phase 1) stays green across the burn-down; each fix lands behind a green build | VERIFIED (circumstantial — see note) | CI workflow exists at `.github/workflows/ci.yml`. Commit `02911ba fix(02-01): repair 3 PluginLoaderTests failures on CI` demonstrates active CI monitoring. CR-01 (missing `using System;` in `LoaderLockHarnessTests.cs`) was caught and fixed in commit `92758ff` before merge. Cannot programmatically confirm GitHub Actions run results without API access, but every phase merge commit was preceded by an explicit CI-green signal in the plan docs. |
| 3 | `data/utinni.cfg` ships with blank server host/port per CON-D-01 (C-14 fix) | VERIFIED | `data/utinni.cfg` lines 6-7: `loginServerPort0=` and `loginServerAddress0=` are blank. Comment above them cites CON-D-01. Commit `e7c6699`. |
| 4 | DllMain no longer does heavy startup per CON-H-01 (C-01 fix); CLR bring-up is deferred to a safe initialization point | VERIFIED | `UtinniCore/utinni.cpp` DllMain (lines 146-159): `DLL_PROCESS_ATTACH` body is `DisableThreadLibraryCalls(hinstDLL); return TRUE;` — nothing else. `extern "C" __declspec(dllexport) DWORD WINAPI utinni_init(LPVOID lpThreadParam)` exported at line 102. `Launcher/main.cpp` line 220: `GetProcAddress(localCore, "utinni_init")` + 2 `CreateRemoteThread` calls (line 186 + 230). |

**Score:** 4/4 ROADMAP success criteria verified

**Note on SC #2:** The automated checks that constitute CI green evidence include: (a) commit `02911ba` named "repair 3 PluginLoaderTests failures on CI" proving CI was running and failures were fixed; (b) CR-01 compile-break was caught and fixed in commit `92758ff`; (c) no debt markers (TBD/FIXME/XXX) in phase-modified files. Direct GitHub Actions green badge confirmation is a human-observable item.

---

### Deferred Items

No items are addressed in later milestone phases. All 15 C-NN bugs are closed in this phase. The two residual Tier-4 manual UATs are expected verification residuals, not deferred work.

---

### Required Artifacts

#### Plan 02-03 (C-01 DllMain loader-lock)

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj` | New C++ console-exe project, depends on UtinniCore | VERIFIED | File exists. Project GUID `{6A57E74E-0D39-430B-AD0D-6E56AA99B54A}`. Registered in `Utinni.sln` with C++ project type GUID `{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}`. |
| `Utinni.LoaderLockHarness/main.cpp` | QPC-bracketed LoadLibraryA with 50ms threshold; exit codes 0/1/2 | VERIFIED | 59 lines. `QueryPerformanceCounter` brackets `LoadLibraryA("UtinniCore.dll")`. Prints `"UtinniCore DllMain elapsed: %.3f ms\n"`. Returns `(elapsedMs < 50.0) ? 0 : 1`; returns 2 on LoadLibraryA failure. |
| `UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs` | xUnit Process.Start wrapper; asserts ExitCode == 0 | VERIFIED | 67 lines. `[Fact] LoaderLockHarness_LoadsUtinniCoreUnderThreshold`. Uses `Process.Start` with stdout capture. Asserts `ExitCode == 0` and stdout contains `"elapsed:"`. Has correct `using System;` (CR-01 fixed in `92758ff`). |
| `UtinniCore/utinni.cpp` | Slimmed DllMain + utinni_init export | VERIFIED | `extern "C" __declspec(dllexport) DWORD WINAPI utinni_init(LPVOID lpThreadParam)` at line 102. DllMain DLL_PROCESS_ATTACH: `DisableThreadLibraryCalls + return TRUE` only. |
| `Launcher/main.cpp` | Second CreateRemoteThread against utinni_init | VERIFIED | `GetProcAddress(localCore, "utinni_init")` at line 220. Two `CreateRemoteThread` calls: original LoadLibraryA (line 186) + new utinni_init (line 230). |

#### Plan 02-04 (C-09 UI/game-thread busy-wait)

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `UtinniCoreDotNet.Tests/FormMainSignallerTests.cs` | 3 mock-signaller tests | VERIFIED | 145 lines. Three `[Fact]` methods: `WaitForPresentBlock_SignalNeverFires_ReturnsWithinTimeout`, `WaitForPresentBlock_SignalFires_ReturnsImmediately`, `WaitForPresentBlock_AlreadySignalled_ReturnsImmediately`. All use `FormMain.TestSignaller` injection seam — no native P/Invoke in tests. |
| `UtinniCore/swg/graphics/directx9.cpp` | getPresentBlockedEvent export + SetEvent + ResetEvent | VERIFIED | `extern "C" __declspec(dllexport) HANDLE __cdecl getPresentBlockedEvent()` at line 45. `SetEvent(hPresentBlockedEvent)` at line 250. `ResetEvent(hPresentBlockedEvent)` at line 404. Static `HANDLE hPresentBlockedEvent = nullptr` at file scope. |
| `UtinniCoreDotNet/UI/Forms/FormMain.cs` | WaitForPresentBlock + EventWaitHandle + SafeWaitHandle | VERIFIED | `Thread.Sleep(1)` count: 0 (grep returns no matches). `internal static bool WaitForPresentBlock(TimeSpan timeout)` method at line 91. `Lazy<EventWaitHandle> presentBlockedSignal` with `SafeWaitHandle(h, ownsHandle: false)`. `internal static EventWaitHandle TestSignaller = null` injection seam. WndProc calls `WaitForPresentBlock(TimeSpan.FromMilliseconds(100))` at line 112. |

#### Plan 02-01 (Trivial criticals C-04/C-06/C-08/C-12/C-13/C-14)

| Artifact | Status | Commit | Evidence |
|----------|--------|--------|---------|
| `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs` (C-04 Drain) | VERIFIED | `9aa0eb9` | `DequeuePostDrawLoopCalls` drains `postDrawLoopCallQueue`. `internal static void Drain(ConcurrentQueue<Action> queue)` helper present. |
| `UtinniCoreDotNet/PluginFramework/PluginLoader.cs` (C-06 isolation) | VERIFIED | `efdb80b` | Per-plugin `DirectoryCatalog` isolation. `public IList<string> LoadErrors` surface. `PluginLoader(autoLoad: false)` ctor seam. |
| `UtinniCoreDotNet/Hotkeys/Hotkey.cs` (C-08 TryParse) | VERIFIED | `c6879b5` | `Enum.TryParse<Keys>` used in `ProcessString`. No `Enum.Parse` throwing on unknown tokens. |
| `sdk/UtinniPluginTemplates/Vsix/source.extension.vsixmanifest` (C-12) | VERIFIED | `88b5b6b` | InstallationTarget widened to `[16.0,18.0)`. |
| `data/utinni.cfg` (C-14) | VERIFIED | `e7c6699` | `loginServerPort0=` and `loginServerAddress0=` blank. |

#### Plan 02-02 (Single-file criticals C-02/C-03/C-05/C-07/C-10/C-11/C-15/C-16/KB-05)

| Artifact | Status | Commit | Evidence |
|----------|--------|--------|---------|
| `UtinniCore/swg/misc/config.cpp` (C-02) | VERIFIED | `8e88879` | `delete[]` removed from `hkLoadOverrideConfig`. Comment cites CON-B-04. |
| `UtinniCore/swg/misc/network.cpp` (C-03) | VERIFIED | `70038a9` | `swgptr networkId = 0;` initialised to 0. OUT param pattern used. Double-semicolon removed. |
| `UtinniCoreDotNet/UI/GameDragDropEventHandlers.cs` (C-05) | VERIFIED (per summary) | `5fd0dac` | Static event + forwarder lambda pattern. `Initialize` accepts `Panel` base type. |
| `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs` (C-07) | VERIFIED | `1a8ff42` | `lock (syncRoot)` at 4 sites. `AllowMerge()` gate before `Merge()`. `RedoCommands.Clear()` ordering fixed (TD-29). |
| `UtinniCore/clr.cpp` (C-10) | VERIFIED | `eabc0d2` | Null-checked `Release` calls at lines 98-100. Pointers set to `nullptr` after release. |
| `UtinniCore/swg/graphics/directx9.cpp` (C-11) | VERIFIED (per summary + getVtbl null check) | `ba1402a` | `getVtbl` null-checks `GetModuleHandle` + `findPattern`. |
| `UtinniCoreDotNetGen/Program.cs` (C-15) | VERIFIED (per summary) | `8a4d7f9` | `ResolveSlnDir` three-mode function (args/walk-up/env-var). |
| `UtinniCore/swg/game/game.cpp` (KB-05) | VERIFIED (per summary) | `94cd3e9` | `isSafeToUse` uses `&&` per internals.md. |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `Launcher/main.cpp::inject()` | `UtinniCore.dll!utinni_init` in remote SWG process | `GetProcAddress("utinni_init")` + offset + `CreateRemoteThread` | VERIFIED | `GetProcAddress(localCore, "utinni_init")` at line 220. `CreateRemoteThread` at line 230 with computed remote address. |
| `Utinni.LoaderLockHarness/main.cpp` | `UtinniCore.dll` DllMain timing | `QueryPerformanceCounter`-bracketed `LoadLibraryA` | VERIFIED | QPC start/stop around `LoadLibraryA("UtinniCore.dll")`. Exit code 0 when < 50ms. |
| `UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs` | `Utinni.LoaderLockHarness.exe` exit code | `Process.Start` + `WaitForExit` + `ExitCode == 0` assertion | VERIFIED | `Process.Start` with `RedirectStandardOutput`. `Assert.Equal(0, proc.ExitCode)`. `Assert.Contains("elapsed:", ...)`. |
| `UtinniCore/swg/graphics/directx9.cpp::hkPresent` | Win32 manual-reset event | `SetEvent(hPresentBlockedEvent)` after `isPresenting = false` | VERIFIED | `SetEvent(hPresentBlockedEvent)` at line 250 inside `hkPresent` block. |
| `UtinniCoreDotNet/UI/Forms/FormMain.cs::WaitForPresentBlock` | `UtinniCore.dll!getPresentBlockedEvent` via `SafeWaitHandle` | P/Invoke + `EventWaitHandle.WaitOne(timeout)` | VERIFIED | `[DllImport("UtinniCore"...)] GetPresentBlockedEvent()`. `Lazy<EventWaitHandle>` wraps handle with `SafeWaitHandle(h, ownsHandle: false)`. `WaitOne(timeout)` call. |
| `UtinniCoreDotNet/UI/Forms/FormMain.cs::WndProc` | `WaitForPresentBlock(100ms)` replaces `Thread.Sleep(1)` spin | `WM_SYSCOMMAND` handler calls `WaitForPresentBlock(TimeSpan.FromMilliseconds(100))` | VERIFIED | `Thread.Sleep(1)` count is 0. `WaitForPresentBlock(TimeSpan.FromMilliseconds(100))` at line 112. |

---

### Data-Flow Trace (Level 4)

Level 4 data-flow trace is not applicable to this phase. All deliverables are bug fixes, regression guard executables, and test harnesses — not UI components rendering dynamic data from a database. The "data" being verified (DllMain timing, event signal, WndProc wait) flows through the tested execution paths confirmed in Levels 1-3.

---

### Behavioral Spot-Checks

Step 7b is partially applicable. The native build requires DXSDK June 2010 which is absent in the local dev environment (confirmed pre-existing constraint in context_note). CI is the canonical build gate. Runnable checks without a full native build:

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| `utinni_init` export present in utinni.cpp | `grep -n "extern \"C\" __declspec(dllexport) DWORD WINAPI utinni_init"` | 1 match at line 102 | PASS |
| DllMain has only `DisableThreadLibraryCalls` + return | Read `UtinniCore/utinni.cpp` lines 146-159 | Only `DisableThreadLibraryCalls(hinstDLL); return TRUE;` in DLL_PROCESS_ATTACH | PASS |
| `Thread.Sleep(1)` removed from FormMain.cs | `grep "Thread\.Sleep(1)"` | 0 matches | PASS |
| `SetEvent(hPresentBlockedEvent)` in directx9.cpp | `grep "SetEvent(hPresentBlockedEvent)"` | 1 match at line 250 | PASS |
| `loginServerPort0=` blank in utinni.cfg | Read `data/utinni.cfg` lines 6-7 | Both fields blank | PASS |
| All 15 C-NN rows `done` in assessment.md | Read assessment.md status table | All 15 show `done` with commit SHAs | PASS |

---

### Probe Execution

Step 7c not applicable. No probe scripts (`scripts/*/tests/probe-*.sh`) declared in any plan or present in the phase directory.

---

### Requirements Coverage

| Requirement | Source Plans | Description | Status | Evidence |
|-------------|-------------|-------------|--------|---------|
| STAB-01 | 02-01, 02-02, 02-03, 02-04 | Fix all 15 critical bugs C-01..C-15 | SATISFIED | All 15 C-NN rows in `docs/ai/assessment.md` status table show `done` with verified commit SHAs. Code-level verification confirms substantive implementations for C-01, C-02, C-03, C-04, C-06, C-07, C-08, C-09, C-10, C-11, C-12, C-14 directly; C-05, C-13, C-15 confirmed by cross-checked commit SHAs and summary documentation. |

No orphaned requirements: REQUIREMENTS.md maps STAB-01 to Phase 2 only, and all four plans claim STAB-01.

---

### Anti-Patterns Found

Scan of phase-modified files for unreferenced debt markers (TBD/FIXME/XXX):

| File | Pattern | Result |
|------|---------|--------|
| `UtinniCore/utinni.cpp` | TBD/FIXME/XXX | None |
| `Launcher/main.cpp` | TBD/FIXME/XXX | None |
| `UtinniCore/swg/graphics/directx9.cpp` | TBD/FIXME/XXX | None |
| `UtinniCoreDotNet/UI/Forms/FormMain.cs` | TBD/FIXME/XXX | None |
| `UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs` | TBD/FIXME/XXX | None |
| `UtinniCoreDotNet.Tests/FormMainSignallerTests.cs` | TBD/FIXME/XXX | None |

No debt-marker blockers found.

**Code review findings (from 02-REVIEW.md):** The orchestrator confirmed the following triage before this verification ran:
- CR-01 (`LoaderLockHarnessTests.cs` missing `using System;`): FIXED in commit `92758ff`. Not a gap.
- CR-02 (`Network::cast` 32-bit/64-bit mismatch), CR-03 (`id >> 32` UB on 32-bit int), CR-04 (`getPresentBlockedEvent` double-create race): Accepted as Phase 2.1 gap-closure work per user direction. Not Phase 02 blockers.
- WR-01..WR-09: Accepted as Phase 2.1 gap-closure work per user direction. Not Phase 02 blockers.

CR-02, CR-03, CR-04 are genuine code-quality concerns in phase-modified files but are classified as WARNING (not BLOCKER) per user direction that they are intentional Phase 2.1 deferred work.

---

### Human Verification Required

#### 1. Live SWG Injection — C-01 Full Proof (Tier-4 Manual UAT)

**Test:** Build `bin/Release/Launcher.exe + UtinniCore.dll + UtinniCoreDotNet.dll` via `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86`. Start SWG via `bin/Release/Launcher.exe`. Confirm: (a) SWG client window appears within ~2 seconds; (b) Utinni editor UI is visible; (c) at least one plugin loads (visible in editor UI or `bin/Release/utinni.log`); (d) no thread blocked on `ntdll!LdrpDrainWorkQueue` in Resource Monitor CPU tab Wait Chain Analysis.

**Expected:** SWG launches cleanly, editor comes up with plugins loaded, no loader-lock hang visible in Wait Chain.

**Why human:** The `Utinni.LoaderLockHarness` regression guard proves DllMain timing is under 50ms (microseconds post-fix), but it runs DllMain in-process and cannot simulate loader-lock contention from a real `LoadLibraryA` injection into an externally-controlled SWG process. Deadlock under contention requires a live SWG client per CONTEXT.md D-06.

**Deferred at:** 2026-05-17 (Wave 3) — recorded in STATE.md.

---

#### 2. Live SWG Minimize/Restore — C-09 Full Proof (Tier-4 Manual UAT)

**Test:** With UtinniCore.dll injected into a live SWG client, minimize the SWG client window from the taskbar and restore it. Repeat 5 times in rapid succession. Optionally repeat 20 times without pause.

**Expected:** (a) No UI hang during any minimize or restore — window animates smoothly; (b) Task Manager CPU does NOT spike to 100% on a single thread during minimize/restore cycles (the pre-fix `Thread.Sleep(1)` busy-wait would peg one core); (c) the editor UI remains responsive throughout.

**Why human:** The `FormMainSignallerTests.cs` mock-signaller tests prove the `EventWaitHandle.WaitOne(timeout)` semantics are correct (no infinite spin, signal observed, already-signalled fast path), but the actual `SetEvent` path requires the real D3D9 `hkPresent` detour running inside a live SWG client, triggered by a real minimize event routed through `WM_SYSCOMMAND`. The end-to-end signal chain (native hkPresent SetEvent → managed WaitOne) cannot be exercised without live SWG per CONTEXT.md D-06.

**Deferred at:** 2026-05-17 (Wave 3) — recorded in STATE.md.

---

### Gaps Summary

No gaps blocking goal achievement. All 4 ROADMAP success criteria are verified at the code level:

1. All 15 C-NN bugs show `done` in assessment.md with verifiable commit SHAs, and code-level spot checks confirm substantive implementations.
2. CI workflow is real and active; the CR-01 compile-break was caught and fixed before merging; no unreferenced debt markers in phase-modified files.
3. `data/utinni.cfg` blank login fields confirmed.
4. `DllMain` slimmed to `DisableThreadLibraryCalls + return TRUE`; `utinni_init` exported; Launcher fires second `CreateRemoteThread`.

The two human verification items are expected Tier-4 residuals, pre-accepted by the user and recorded in STATE.md. They are not gaps — they are the documented boundary of automated verification for a native-DLL-injection project.

**Code review findings CR-02/CR-03/CR-04 + WR-01..WR-09** are code-quality issues in phase-modified files that the user has explicitly accepted as Phase 2.1 gap-closure work. They are not Phase 02 blockers per user direction.

---

_Verified: 2026-05-17_
_Verifier: Claude (gsd-verifier)_
