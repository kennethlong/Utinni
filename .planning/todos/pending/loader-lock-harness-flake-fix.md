---
title: Fix flaky LoaderLockHarness 50 ms DllMain threshold on shared CI runners
created: 2026-05-23
priority: medium
area: ci-stability
discovered_in: phase-05-wave-1
related:
  - .planning/phases/02-critical-bug-burn-down-c-01-c-15/02-03-PLAN.md  # C-01 origin
  - Utinni.LoaderLockHarness/main.cpp                                    # threshold lives here
  - UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs                     # test that asserts on exit code
suggested_resolves_phase: 6  # STAB-03 stability hardening is the natural home; user may route elsewhere
---

## Problem

`UtinniCoreDotNet.Tests.LoaderLockHarnessTests.LoaderLockHarness_LoadsUtinniCoreUnderThreshold` — the C-01 regression guard that measures `LoadLibraryA("UtinniCore.dll")` wall time — has a hard-coded **50 ms** threshold in `Utinni.LoaderLockHarness/main.cpp:58`. On shared GitHub Actions `windows-2022` runners under cold-disk-cache or parallel-build contention, DllMain occasionally exceeds 50 ms with no code change.

**Confirmed flake event:** Phase 5 wave-1 push (SHA `727250b`, run id `26335337027`) failed on first run; `gh run rerun` on the **same SHA** passed clean. Zero code changes between the two runs. All native lane steps were green on the re-run.

**Prior documentation:** Commit `06a7ae4` already noted this test as "environment-dependent" — *"131/131 UtinniCoreDotNet.Tests count is environment-dependent: LoaderLockHarness fails on plain `dotnet test --no-build` without the native VS toolchain outputs. Unrelated to the Phase 4 fixes."*

## Why it matters

- Spurious red runs erode trust in the master-gating contract (Phase 4 D-11 + Phase 5 D-04 both rely on CI being a reliable signal).
- Every flake costs a re-run cycle (~10 min wall, minor compute) and risks masking real regressions during pile-on diagnosis.
- The new Phase 5 native lane is BLOCKED from running when the managed lane fails earlier, so flakes here also delay native-test signal.

## Why this is not a Phase 5 fix

The flake is in the **existing managed lane** introduced in Phase 2 (C-01). Phase 5's scope is the native Catch2 lane (TEST-02). Fixing the flake here would be scope creep and would couple two unrelated phase concerns. Track here, fix in a dedicated follow-up.

## Suggested fix options (pick one in the resolving phase)

| Option | Cost | Risk |
|--------|------|------|
| **A. Raise threshold to 100 ms.** One-line change in `main.cpp:58`. | Trivial. | Masks slow-DllMain regressions in the 50–100 ms band. Probably fine — the goal is "no heavy work in DllMain," not "DllMain in exactly 50 ms." |
| **B. Add retry-on-fail (median of 3 runs).** Modify `main.cpp` to run LoadLibrary 3× and assert on the median. | Moderate. | Eliminates single-sample variance; preserves regression-catch fidelity. Slightly slower test (still <1 s). |
| **C. Surface elapsed-ms on failure.** Modify `LoaderLockHarnessTests.cs` to capture `ITestOutputHelper` and log harness stdout (`UtinniCore DllMain elapsed: X.XXX ms`) when the assertion fails. | Trivial. | Doesn't fix the flake — just makes diagnosis faster next time. Combine with A or B. |
| **D. Tag the test as `[Flaky]` / opt out of the gate.** | Trivial. | Removes the regression guard entirely. **Do not pick this** unless C-01 is being retired. |

Recommended composite: **C + A** (surface the value AND raise the threshold to 100 ms). C makes the next flake diagnosable in one log line; A reduces the flake rate to ~zero.

## Don't pick D

The whole point of C-01 is to catch "someone moved heavy work back into DllMain." Removing the gate gives that regression an unimpeded path back in.

## Files to touch when fixing

1. `Utinni.LoaderLockHarness/main.cpp` — threshold constant (option A) or retry loop (option B)
2. `UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs` — `ITestOutputHelper.WriteLine(stdout)` on failure (option C)
3. Commit message scope: `fix(c-01): ...` (this is C-01's regression guard, not phase-N work)

## See also

- `[[project_loader_lock_harness_ci_flake]]` (the auto-memory) — short hook + "re-run first" rule for the **next** time CI fires this
- `.planning/phases/02-critical-bug-burn-down-c-01-c-15/02-03-PLAN.md` — original C-01 plan that introduced the harness
- Commit `06a7ae4` — original "environment-dependent" caveat
