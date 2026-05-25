---
phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut
plan: 04
subsystem: infra
tags: [ci-flake, loader-lock-harness, gamecallbacks, access-violation, dllmain, test-exports, d-17, stab-03]

# Dependency graph
requires:
  - phase: 06-03
    provides: CI-green master baseline + the self-hosted v145 runner this plan iterates CI on
provides:
  - Loader-lock-harness hardened against the 50ms-threshold contention flake (best-of-3 minimum; threshold unchanged)
  - GameCallbacks ForceGCCollect test de-flaked — AV-prone native trigger replaced by a deterministic sentinel export; managed-side GC-survival (Probe 2) is the green-CI assertion
  - utinni_testHarnessProbe — new deterministic test-only export (returns 0xDEADBEEF, touches no state), guarded by ExportResolutionTests
  - 06-04-FLAKE-INVESTIGATION.md — root-cause + mitigation-selection record for both flakes
  - D-17 closed (both folded CI-stability todos fixed atomically + regression-fenced)
affects: [06-05, 06-06, 1.0-rc.1]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "best-of-N minimum for a load-time floor-guard: contention only adds time, so min across N cold loads is the robust intrinsic-cost estimate"
    - "deterministic side-effect-free native sentinel export to prove the P/Invoke boundary is callable without exercising AV-prone game state"
    - "regression-probe macro (#ifdef LOADER_LOCK_HARNESS_REGRESSION_PROBE) — compile-time-gated proof the hardened guard still turns red on a simulated regression"

key-files:
  created:
    - .planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-04-FLAKE-INVESTIGATION.md
  modified:
    - Utinni.LoaderLockHarness/main.cpp
    - UtinniCore/test_exports.cpp
    - UtinniCoreDotNet.Tests/GameCallbacksTests.cs
    - UtinniCoreDotNet.Tests/ExportResolutionTests.cs

key-decisions:
  - "Loader-lock: chose OPT-A (best-of-3 min) over the more-precise OPT-D (in-DLL DllMain body timing). OPT-D would measure exactly the regression target and be contention-immune, but it requires modifying production DllMain in the live-SWG INJECTION hot path — a code path CI cannot cover (CI only LoadLibrary's UtinniCore.dll in a benign test process; injection is validated only by manual live-SWG smoke). Putting manual-smoke-only risk into the injection path contradicts this plan's whole purpose (make CI a reliable gate). OPT-A keeps the change in the harness exe, fully CI-covered, zero injection risk."
  - "GameCallbacks: chose OPT-A (sentinel export + File.Exists gate) over OPT-C (split + Xunit.SkippableFact) to avoid a new NuGet dependency + packages.lock.json churn, and over OPT-B (catch AccessViolationException) which is fragile on net472 (needs HandleProcessCorruptedStateExceptions + legacyCorruptedStateExceptionsPolicy and leaves the process in an undefined state after the AV)."
  - "Root cause confirmed for loader-lock: UtinniCore's DllMain body is microseconds (DisableThreadLibraryCalls + return TRUE, utinni.cpp:313). The single cold LoadLibraryA wall-time the old harness measured was dominated by OS loader overhead (image mapping + static-import resolution + CRT init), which is exactly what spikes under shared-runner contention — the flake measured the loader, not DllMain."
  - "Root cause confirmed for GameCallbacks: utinni_triggerInstallCallbacks -> test_internal::triggerInstallCallbacks dispatches over raw void(*)() pointers in the native installCallbacks vector; in a non-injected test process that state is undefined -> ASLR-dependent AccessViolationException (passed in some runs, AV in others)."
  - "Kept the utinni_triggerInstallCallbacks export (still listed/guarded by ExportResolutionTests); only stopped CALLING it from the flaky test. The export is legitimate; the test was the wrong place to exercise it."

patterns-established:
  - "When a 'no-AV at the native boundary' check cannot be made deterministic in a unit-test process (it legitimately needs live game memory), replace it with a deterministic sentinel and demote the real call to a Tier-4 live-smoke concern — same philosophy the loader-lock harness already documents for deadlock proof."
  - "Multi-cycle load harness only re-measures if the DLL fully unloads between cycles. Verified empirically: UtinniCore does not self-pin (DllMain neither starts the CLR nor spawns threads), so 3 cycles each re-run CRT init + DllMain (~11-14ms each locally, not ~0ms) — a real regression inflates every cycle including the min."

requirements-completed: [STAB-03]

# Metrics
duration: ~90min (incl. 6 gated CI runs on the serial self-hosted runner)
completed: 2026-05-25
---

# Phase 06-04: Close the two CI-stability flakes (D-17) Summary

**The loader-lock-harness 50ms-threshold flake and the GameCallbacks `ForceGCCollect` intermittent AccessViolationException are both fixed atomically, each regression-fenced, on CI-green master — clearing the last blocker on 1.0 success criterion #5 (CI green on master) ahead of the rc.1 tag.**

## What shipped (3 atomic commits)

| Commit | Task | What |
|---|---|---|
| `40ac719` | 1 | `docs(06-04):` flake investigation + mitigation selection (no code) |
| `47b86ef` | 2 | `fix(06-04):` loader-lock-harness OPT-A (best-of-3 minimum) |
| `3ed4665` | 3 | `test(06-04):` GameCallbacks OPT-A (deterministic sentinel export) |

## Loader-lock-harness (Task 2 — OPT-A best-of-3 min)

- **Was:** one cold `LoadLibraryA("UtinniCore.dll")` measured end-to-end, `return (elapsedMs < 50.0) ? 0 : 1;`. That wall time is dominated by OS loader overhead, which spikes under shared-runner contention → exit 1 → red (confirmed: run 26190579282).
- **Now:** three full `LoadLibraryA` + `FreeLibrary` cycles; the **minimum** elapsed is compared to the unchanged 50ms threshold. Contention only ever adds time, so the min is the cleanest intrinsic-cost estimate — all three cycles would have to be simultaneously contended to flake. A real "heavy work back in DllMain" regression inflates every cycle (UtinniCore fully unloads/reloads each cycle) including the min → still caught.
- **Regression probe:** `#ifdef LOADER_LOCK_HARNESS_REGRESSION_PROBE` (not built by default) injects an in-window `Sleep(75)` so a maintainer can confirm the hardened guard still turns red (T-06-04-01).
- **Local verification:**
  - normal build → cycles 13.8 / 13.0 / 11.1 ms, min 11.1 ms < 50 ms → **exit 0**.
  - regression-probe build → cycles 88.4 / 84.5 / 86.1 ms, min 84.5 ms > 50 ms → **exit 1** (guard intact).

## GameCallbacks ForceGCCollect AV (Task 3 — OPT-A sentinel)

- **Was:** Probe 1 called native `utinni_triggerInstallCallbacks`, which iterates raw `void(*)()` pointers over undefined native state in a non-injected process → ASLR-dependent `AccessViolationException` (failed runs 26301843923 / 26336492894 / 26350653718 / 26361049514; passed others).
- **Now:** new deterministic export `utinni_testHarnessProbe` (returns `0xDEADBEEF`, touches no callback state, can never AV). Probe 1 is replaced by a `File.Exists(UtinniCore.dll)` gate + a sentinel call asserting the magic value — a deterministic native-boundary liveness check. **Probe 2** (managed-side reflection on `CallInstallCallbacks`) is the green-CI GC-survival assertion and always runs.
- **Export guard:** `utinni_testHarnessProbe` added to `kExpectedExports[]`; `ExportResolutionTests.ExpectedExportCount` bumped 22 → 23, so the sentinel's existence is now itself regression-guarded.
- **Regression fence:** test comment ties the gate to `06-04-FLAKE-INVESTIGATION.md`; removing the gate rejoins the raw trigger to the green-CI path and the AV flake returns.
- **Local verification:** full Release|x86 build clean; `dumpbin /exports` confirms `utinni_testHarnessProbe`; `dotnet test UtinniCoreDotNet.Tests` → **131/131 passed**.

## CI gate evidence (3 consecutive green per fix)

- **Task 2 fix (`47b86ef`):** runs 26412859567 (push) + 26412873806 + 26412876547 (dispatch) → `["success","success","success"]`. ✅
- **Task 3 fix (`3ed4665`):** runs 26414070544 (push) + 26414069950 + 26414072713 (dispatch) → `["success","success","success"]`. ✅

## Why these mitigations (vs alternatives)

Both selections are the **lowest-injection-risk, zero-new-dependency** option of their candidate set — see `06-04-FLAKE-INVESTIGATION.md §(e)` for each flake. The recurring principle: 06-04 exists to make CI a *reliable* gate for the rc.1 tag, so a fix that trades a CI flake for a manual-smoke-only risk (OPT-D) or a new supply-chain surface (OPT-C) is self-defeating.

## Carry-forward

- 06-05 cleanup sweep (clang-format + dead-code purge + STAB-04 audit) is now unblocked — no more re-run lottery on every push.
- 06-06 rc.1 tag depends on CI green on master, which this plan secures.
- The real native `utinni_triggerInstallCallbacks` dispatch AV-safety under a live SWG memory layout remains a Tier-4 live-smoke concern (same disposition as the loader-lock deadlock proof).
