---
status: partial
phase: 02-critical-bug-burn-down-c-01-c-15
source: [02-VERIFICATION.md]
started: 2026-05-17T02:50:00Z
updated: 2026-05-17T02:50:00Z
---

## Current Test

[awaiting human testing — both items pre-accepted as Tier-4 manual residuals; user already chose "Defer manual test — code-complete" for both during phase execution]

## Tests

### 1. Live SWG injection — C-01 full proof

expected: SWG launches cleanly, editor comes up with plugins loaded, no loader-lock hang.
result: [pending]

**How to verify:**
1. Build: `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86` (requires DXSDK June 2010 — installed in CI; user's local Windows machine may need installation per `.github/workflows/ci.yml` lines 38-69).
2. Confirm `dumpbin /exports bin\Release\UtinniCore.dll | findstr utinni_init` shows one unmangled `utinni_init` entry.
3. Run `bin\Release\Utinni.LoaderLockHarness.exe` — should print `"UtinniCore DllMain elapsed: X.XXX ms"` with X well under 50, exit code 0.
4. Run `bin\Release\Launcher.exe` (or via your normal injection path):
   - SWG client window appears within ~2 seconds (no hang at "Loading…")
   - Utinni editor UI is visible alongside SWG
   - At least one plugin loads (visible in editor UI or `utinni.log`)
   - Task Manager → Resource Monitor → CPU → Wait Chain Analysis for the SWG process: no thread blocked on `ntdll!LdrpDrainWorkQueue`.

**Resume signal:** `approved — editor came up with plugins loaded; no loader-lock hang`.

**Why human:** The LoaderLockHarness regression guard times DllMain at <50ms but does not prove "no deadlock under loader-lock contention". Full proof requires injection into a live SWG client process. Per CONTEXT.md D-06 (Tier-4 manual residual).

---

### 2. Live SWG minimize/restore — C-09 full proof

expected: No UI hang (window animates smoothly), no CPU spike on UI thread, editor remains responsive. Confirms `EventWaitHandle.WaitOne(100ms)` replaced the `Thread.Sleep(1)` spin correctly end-to-end.
result: [pending]

**How to verify:**
1. With UtinniCore.dll injected into a live SWG client (item 1 above):
2. Minimize the SWG client window via its taskbar button (or Win+Down).
3. Restore the SWG client window via its taskbar entry.
4. Repeat steps 2-3 five times in quick succession.
5. Confirm: no UI hang, no CPU spike on UI thread (open Task Manager → Performance → CPU), editor UI remains responsive during minimize/restore cycles.
6. Optional stress: 20 rapid minimize/restore cycles, no degradation.

**Resume signal:** `approved — minimize/restore no hang, no CPU spike`.

**Why human:** Mock-signaller tests (`FormMainSignallerTests.cs`) prove timeout and signal semantics via a fake EventWaitHandle, but the actual SetEvent signal path requires the real D3D9 `hkPresent` detour running inside a live SWG client. Per CONTEXT.md D-06 (Tier-4 manual residual).

## Summary

total: 2
passed: 0
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps
