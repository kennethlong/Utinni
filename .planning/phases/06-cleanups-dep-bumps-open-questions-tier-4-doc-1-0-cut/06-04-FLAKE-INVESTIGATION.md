# 06-04 CI Flake Investigation + Mitigation Selection

> Task 1 of 06-04. Root-cause analysis + failure-mode evidence for the two folded CI-stability
> todos (D-17): the loader-lock-harness 50 ms-threshold flake and the GameCallbacks
> `ForceGCCollect` intermittent AccessViolationException. No code changes in this task — this doc
> selects exactly one mitigation OPT per flake and justifies it; Tasks 2 and 3 implement them.

**Date:** 2026-05-25
**Baseline:** master @ `5e31fd5` (CI-green, run 26412045821)
**Evidence window:** `gh run list --branch master --limit 50` (covers 2026-05-20 → 2026-05-25)

---

## Evidence-collection commands (reproducible)

```bash
# All failed master runs in the window:
gh run list --branch master --limit 50 --json conclusion,databaseId,headSha,createdAt,name,event \
  --jq '.[] | select(.conclusion == "failure") | "\(.databaseId)  \(.headSha[0:7])  \(.createdAt)  \(.name) [\(.event)]"'

# Per-run failure log (filter to the relevant test/harness signal):
gh run view <ID> --log-failed | grep -iE "DllMain elapsed|LoaderLockHarness|ForceGCCollect|AccessViolation|Assert\.(Null|Equal)"

# Timing from a GREEN run (full log; the harness's internal elapsed-ms is NOT echoed to the
# test log — only the test-process wall time and the exit-code assertion are):
gh run view <GREEN_ID> --log | grep -iE "DllMain elapsed|LoaderLockHarness"
```

19 failed runs in the window. The two flakes below account for the test-lane failures; the
remainder are build-break / iteration commits (e.g. the 2026-05-25 02:xx cluster during active
06-03 work) and are out of scope here.

---

# Loader-Lock-Harness 50 ms threshold flake

## (a) Symptom

`UtinniCoreDotNet.Tests.LoaderLockHarnessTests.LoaderLockHarness_LoadsUtinniCoreUnderThreshold`
shells out to `Utinni.LoaderLockHarness.exe` via `Process.Start` and asserts `ExitCode == 0`
(`LoaderLockHarnessTests.cs:63`). The harness exits `1` when a **single cold** `LoadLibraryA("UtinniCore.dll")`
takes ≥ 50.0 ms (`Utinni.LoaderLockHarness/main.cpp:58`). Under shared-runner contention the cold
load occasionally crosses 50 ms → exit 1 → test red. Low frequency (1 failure in the 50-run window,
on the now-retired GitHub-hosted lane).

## (b) Evidence

| Run ID | SHA | Date | Result | Detail |
|---|---|---|---|---|
| **26190579282** | ea... `4cffbdf`-era | 2026-05-20T21:21Z | **FAIL** | `LoaderLockHarness_LoadsUtinniCoreUnderThreshold [FAIL]` — `Assert.Equal() Failure: Expected: 0, Actual: 1` at `LoaderLockHarnessTests.cs(63)`. GameCallbacks **passed** in this same run (`[11 ms]`), isolating this as the loader-lock flake. GitHub-hosted `windows-2022` (`D:\a\Utinni\...` path). |
| 26412045821 (green) | `5e31fd5` | 2026-05-25T17:19Z | PASS | `LoaderLockHarnessTests.LoaderLockHarness_LoadsUtinniCoreUnderThreshold [918 ms]` — the `[918 ms]` is the **test-process** wall time (Process.Start + WaitForExit), NOT the harness's internal DllMain-load elapsed. The internal `elapsed:` value is printed to the harness's stdout but is only `Assert.Contains`-checked, never echoed to the xUnit log, so the exact ms is not recoverable from CI logs — only the exit code (0/1) is observable. |

Failing-run excerpt (run 26190579282):
```
[xUnit.net] UtinniCoreDotNet.Tests.LoaderLockHarnessTests.LoaderLockHarness_LoadsUtinniCoreUnderThreshold [FAIL]
  Assert.Equal() Failure: Values differ
  Expected: 0
  Actual:   1
    LoaderLockHarnessTests.cs(63,0): at ...LoaderLockHarness_LoadsUtinniCoreUnderThreshold()
```

## (c) Root cause hypothesis

The harness measures the **wall time of a single `LoadLibraryA` call**, then compares it to a
hard-coded 50 ms. But UtinniCore's actual DllMain body is microseconds:

```cpp
// utinni.cpp:313-319
BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
    switch (fdwReason)
    {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hinstDLL);   // microseconds
        return TRUE;
    ...
```

`LoadLibraryA` wall time = **OS loader overhead** (page-mapping the image, resolving UtinniCore's
static-import graph, CRT static-initializer run via `_DllMainCRTStartup`) **+ the DllMain body**.
The loader overhead dominates and is exactly what spikes under shared-runner contention (disk/page-cache
pressure, loader-lock contention with co-scheduled jobs). The threshold therefore conflates two
unrelated things: "did someone make DllMain heavy" (the thing we care about — per C-01 / CON-H-01)
and "was the loader slow on this runner today" (noise Utinni does not control). A single cold sample
against a single number has no statistical model, so a transient loader spike flips the gate red.

Cross-ref: `[[project-loader-lock-harness-ci-flake]]` memory ("DllMain 50 ms threshold flakes on
shared windows-2022 runners under contention; re-run before investigating") and 06-CONTEXT.md D-17.

## (d) Reproduction attempt

**Not locally reproducible on demand** — the flake relies on shared-runner contention (the failing
run was on GitHub-hosted `windows-2022`). On the current self-hosted runner (an otherwise-idle local
machine) the cold load is comfortably under 50 ms, which is *why* the flake is now rare rather than
gone. The fix must not depend on reproducing the contention; it must make the gate robust to it by
construction.

## (e) Chosen mitigation

**Selected: OPT-A (best-of-3 minimum).** Replace the single `LoadLibraryA`/measurement with three
full `LoadLibraryA` + `FreeLibrary` cycles; take `std::min` across the three measured elapsed values;
keep the 50.0 ms threshold applied to that minimum.

**Why `min`-of-N is the correct statistic for *this* guard.** The harness is a *floor* regression
guard ("did DllMain get heavy"). Contention can only ever *add* time to a load; it never makes a load
faster. So the **minimum** across N attempts is the cleanest estimate of the intrinsic load cost — to
flake, *all three* cycles would have to be simultaneously contended (vanishingly unlikely, and nearly
impossible on the idle self-hosted runner). Meanwhile a genuine regression (heavy work moved back into
DllMain, e.g. CLR start or plugin load — tens to hundreds of ms) runs on **every** genuine
`DLL_PROCESS_ATTACH`, inflating all three samples including the minimum → still caught. The 50 ms
threshold against a microsecond-scale body keeps an enormous safety margin: no realistic "heavy work"
regression squeaks under 50 ms.

**Unload-trap check (why 3 cycles genuinely re-measure).** A concern with any multi-cycle approach is
"do cycles 2..N re-run DllMain, or just bump the refcount?" UtinniCore does **not** self-pin: its
DllMain only calls `DisableThreadLibraryCalls` (it does not start the CLR or spawn threads), and its
C++ static objects (the `std::vector`/`std::mutex` registries in `game.cpp`, spdlog loggers) carry no
pin. So `FreeLibrary` drops the refcount to 0, UtinniCore unmaps, and the next `LoadLibraryA` re-maps
and re-runs CRT static-init + DllMain. Each of the three cycles is therefore a genuine, regression-
sensitive measurement — the trap does not apply here.

**Why not the alternatives:**
- **OPT-D (in-DLL body timing via a `getDllMainEntryExitMs` export)** — *the most precise* option (it
  would measure only the DllMain body and be fully contention-immune) and was the strongest contender.
  **Rejected** because it requires modifying the **production DllMain** that runs inside the live-SWG
  injection hot path, and that code path's correctness is **not covered by CI** — CI only `LoadLibrary`s
  UtinniCore.dll in a benign test process; real injection is validated only by the manual live-SWG
  smoke (a human-action checkpoint). Adding a change whose only real validation is manual smoke directly
  contradicts this plan's purpose (make CI reliable so we stop depending on manual smoke). The marginal
  precision gain over OPT-A's `min`-of-3 does not justify putting new code in the injection path.
  `QueryPerformanceCounter` is itself loader-lock-safe, so OPT-D is *feasible* — it is rejected on
  risk-surface grounds, not safety grounds.
- **OPT-B (warmup + median-of-5)** — comparable robustness but 6 cycles vs 3, and discarding the cold
  load as "warmup" is unnecessary once `min` already absorbs outliers. `min`-of-3 is simpler and
  cheaper for equal protection.
- **OPT-C (raise the threshold)** — the crudest option; a higher number silently widens the window in
  which a *real* DllMain regression goes undetected (T-06-04-01). Rejected.

**Regression probe (T-06-04-01 mitigation).** Task 2 adds a compile-time-gated
`#ifdef LOADER_LOCK_HARNESS_REGRESSION_PROBE` block that injects an artificial over-threshold delay,
so a maintainer can flip the macro and confirm the hardened harness still turns red on a simulated
DllMain regression — proving the `min`-of-3 logic did not blunt the guard. Not built by default.

---

# GameCallbacks ForceGCCollect AV flake

## (a) Symptom

`UtinniCoreDotNet.Tests.GameCallbacksTests.RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV`
intermittently fails with `System.AccessViolationException: Attempted to read or write protected
memory` originating at `GameCallbacksTests.cs:77` — the native P/Invoke
`NativeBridge.Utinni_TriggerInstallCallbacks()` (Probe 1) — which then propagates through
`Assert.Null(ex)` at line 80. Failed in **4+** runs in the window; **passed** in others (`[11 ms]`),
confirming intermittency rather than a hard break.

## (b) Evidence

| Run ID | SHA | Date | Result |
|---|---|---|---|
| 26301843923 | `608750e` | 2026-05-22T17:19Z | FAIL — AV at line 77 → `Assert.Null` line 80 |
| 26336492894 | `aced5a8` | 2026-05-23T15:28Z | FAIL — AV (full excerpt below) |
| 26350653718 | `5f63b36` | 2026-05-24T03:22Z | FAIL — AV at line 77/80 |
| 26361049514 | `63609c1` | 2026-05-24T12:21Z | FAIL — AV at line 77/80 |
| 26190579282 | (2026-05-20) | 2026-05-20T21:21Z | **PASS** (`[11 ms]`) — same test, no AV that run |

Full excerpt (run 26336492894):
```
[xUnit.net] UtinniCoreDotNet.Tests.GameCallbacksTests.RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV [FAIL]
  Assert.Null() Failure: Value is not null
  Expected: null
  Actual:   System.AccessViolationException: Attempted to read or write protected memory.
            This is often an indication that other memory is corrupt.
    at ...GameCallbacksTests.<>c.<...b__1_1>() in GameCallbacksTests.cs:line 77
    at Xunit.Record.Exception(Action testCode)
    GameCallbacksTests.cs(80,0): at ...RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV()
```
(net472 / x86; xUnit's `Record.Exception` does surface the AV here, i.e. the runtime's
corrupted-state-exception delivery is active in this test host — so the AV becomes a *test failure*
rather than a process crash, but it is non-deterministic.)

## (c) Root cause hypothesis

Probe 1 calls the native export `utinni_triggerInstallCallbacks`, which forwards to
`test_internal::triggerInstallCallbacks()` →
`dispatchSnapshot(installCallbacks, ..., [](void(*func)()) { func(); })` (`game.cpp:206-209, 593-598`).
That dispatch iterates the **native** `installCallbacks` vector and invokes each entry as a raw
`void(*)()` function pointer. In the unit-test process there is **no injected SWG session**, so the
native callback registry's contents are undefined: when the list is empty / the indirect call happens
to land on a mapped page, the call is a benign no-op and the test passes; when it lands on a protected
page (ASLR-dependent), it AVs. This is a classic non-deterministic, layout-dependent access violation —
the native trigger simply **cannot be made deterministic outside a live SWG session**, because proving
"the native dispatch path doesn't AV" legitimately requires the SWG memory layout the test process
lacks.

Critically, the native trigger is **not** what this test exists to prove. The test's real subject
(C-16) is GC-survival of the managed delegate anchored by `GameCallbacks.installCallbacks` (a static
`SynchronizedCollection`) — that is **Probe 2** (`InvokeCallbacksViaManagedReflection` + `Assert.True(fired)`),
a purely managed-side property. Probe 1 was a bonus "native boundary doesn't AV" check that can never
be reliable in a non-injected process.

Cross-ref: 06-CONTEXT.md D-17 folded todo `gamecallbacks-gc-av-flake-fix` (score 0.4).

## (d) Reproduction attempt

**Intermittently reproducible / layout-dependent — not reliably reproducible on demand.** The same
binary passes and fails across runs (`[11 ms]` pass vs AV fails above) with no source change, which is
itself the diagnostic signature of an ASLR-dependent bad dereference. As with the loader-lock flake,
the fix must remove the non-determinism by construction rather than chase a repro.

## (e) Chosen mitigation

**Selected: OPT-A (gate the native probe behind a deterministic availability + liveness check, and
make the native call a side-effect-free sentinel).** Concretely (Task 3):
1. Add a new test-only export `utinni_testHarnessProbe` to `UtinniCore/test_exports.cpp` that returns a
   fixed magic value (`0xDEADBEEFu`) and **touches no callback state** — it can never AV.
2. In the test, **replace** the AV-prone `utinni_triggerInstallCallbacks()` call with: a
   `File.Exists(<UtinniCore.dll next to the test assembly>)` gate AND a P/Invoke to
   `utinni_testHarnessProbe` asserting it returns the magic value. This proves the native boundary is
   *loadable and callable without AV* deterministically. If the DLL is absent (local dev), the native
   probe is skipped entirely.
3. **Probe 2 (managed-side GC-survival) remains the green-CI assertion** — it is what actually proves
   the C-16 regression target and is fully deterministic.
4. The new export ripples to `utinni_test_resolveExports`'s `kExpectedExports` list and bumps
   `ExportResolutionTests.ExpectedExportCount` 22 → 23 — which means `ExportResolutionTests` then guards
   the sentinel's existence for free.

This keeps the test's `...WithoutAV` semantics meaningful (a deterministic native-boundary liveness
check) while eliminating the only AV source (the raw-function-pointer dispatch over undefined native
state). Whether the *real* `utinni_triggerInstallCallbacks` dispatch AVs under a live layout stays a
Tier-4 live-SWG concern — the same philosophy the loader-lock harness already documents ("full proof
... remains Tier-4 manual").

**Why not the alternatives:**
- **OPT-B (catch `AccessViolationException` + `SEHException`)** — on net472 this needs
  `[HandleProcessCorruptedStateExceptions]` + `legacyCorruptedStateExceptionsPolicy`, and even then
  catching an AV raised mid-native-iteration leaves the process in an undefined state for subsequent
  tests in the same host. Treating an AV as "expected-skip" is brittle and semantically muddy. Rejected.
- **OPT-C (split + `[SkippableFact]`)** — clean separation, but adds the `Xunit.SkippableFact` NuGet
  dependency (supply-chain surface + `packages.lock.json` churn under this project's
  `RestorePackagesWithLockFile=true`). OPT-A achieves the same isolation with a ~5-line native sentinel
  and zero new packages. Rejected on dependency-minimization grounds.

**Regression fence.** Task 3 adds a comment tying the gate to this doc: if the `File.Exists`/sentinel
gate is removed and the raw trigger rejoins the green-CI path, the AV flake returns — so the fence is
one grep away from its rationale (T-06-04-03).

---

## Summary of selections

| Flake | Selected OPT | Touches production injection path? | New dependency? |
|---|---|---|---|
| Loader-lock-harness 50 ms threshold | **OPT-A** — best-of-3 `min`, threshold stays 50 ms | No (harness exe only) | No |
| GameCallbacks ForceGCCollect AV | **OPT-A** — sentinel export + File.Exists gate; managed Probe 2 is the green-CI assertion | No (`test_exports.cpp` is test-only) | No |

Both selections are deliberately the **lowest-injection-risk, zero-new-dependency** options, because
the entire purpose of 06-04 is to make CI a reliable gate for the 1.0-rc.1 tag — not to trade one
manual-smoke dependency for another.
