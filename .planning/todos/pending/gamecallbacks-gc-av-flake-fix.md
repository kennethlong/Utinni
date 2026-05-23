---
title: Fix GameCallbacksTests.RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV AV under CI
created: 2026-05-23
priority: medium
area: ci-stability
discovered_in: phase-05
related:
  - UtinniCoreDotNet.Tests/GameCallbacksTests.cs                 # test that AVs
  - UtinniCore/swg/game/game.h                                   # callback subscribe path
  - "[[project_loader_lock_harness_ci_flake]]"                   # same flake bucket
suggested_resolves_phase: 6  # STAB-03 stability hardening is the natural home
---

## Problem

`UtinniCoreDotNet.Tests.GameCallbacksTests.RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV` intermittently fails on shared GitHub Actions `windows-2022` runners with:

```
Assert.Null() Failure: Value is not null
Expected: null
Actual:   System.AccessViolationException: Attempted to read or write protected memory.
   at UtinniCoreDotNet.Tests.GameCallbacksTests.NativeBridge.Utinni_TriggerInstallCallbacks()
   at GameCallbacksTests.cs:line 77
```

The test deliberately forces a `GC.Collect()` between callback registration and trigger, then asserts the delegate's native function pointer is still valid (no AV). The xUnit `Assert.Null(Record.Exception(...))` pattern means the test passes when the call returns cleanly.

**Confirmed reproductions** during Phase 5 wave-2 push (no Phase-5 code touches the callback path):
- Run `26336088047` on SHA `c9627bd` — first attempt failed with AV (managed lane only; native lane never ran)
- Run `26336088047` on SHA `c9627bd` — **re-run on same SHA also failed with same AV**, same test, 273 ms duration

The same-SHA repeat-failure pushes this out of "single-sample flake" territory. Either:
1. The test reliably AVs ~50%+ of the time on the current CI runner image
2. There's a real interop bug (delegate keep-alive missing, GCHandle scope too short, race in the native install path)

## Why this isn't Phase 5

Phase 5 wave 2's only production change is `UtinniCore/utility/string_utility.h:46` — a 4-char header-local initializer for a stack-local `bool` in `toBool`. This cannot plausibly cause an AV in unrelated `Utinni_TriggerInstallCallbacks`. The Phase 5 diff also adds a new vcxproj to the solution, which changes build ordering but not UtinniCore.dll's exported callback dispatch.

The callback subscription path itself was not touched by Phase 5. It was last meaningfully changed in Phase 3 (R-A snapshot iteration). The 24/8 local Catch2 suite passes consistently; the **smoke + seed C++ tests in this phase exit 0 every time**. The failure is exclusively in the managed-lane GC+P/Invoke interop.

## Suggested investigation steps

Same shape as [[project_loader_lock_harness_ci_flake]] but with a different root-cause hypothesis space:

1. **GCHandle audit.** Look at `GameCallbacksTests.cs:77` and the surrounding setup. Is the delegate kept alive via `GCHandle.Alloc(... , GCHandleType.Normal)` or just a local variable? If local, `GC.Collect()` can reclaim it even while xUnit holds a stack frame because the JIT may have optimized away the local.
2. **Native function-pointer storage.** When `Utinni_InstallCallback(IntPtr fp)` runs, does the native side immediately register `fp` into a table that survives the GC, or does it merely read the pointer and let it dangle? The fix is usually a Marshal.GetFunctionPointerForDelegate + GCHandle pair.
3. **Bisect against known-good baseline.** Re-run the test 20× on:
   - `06a7ae4` (pre-Phase-5)
   - `c9627bd` (post-Phase-5)
   - If both runners show ~50% failure rate, it's an environment issue. If only post-Phase-5 fails, something in the new test exe's build ordering is shifting allocator state.
4. **Suspect: dual native-test-exe runtime.** With Phase 5, UtinniCore.Tests.exe now also loads UtinniCore.dll (well, doesn't link against it — but the build produces both). It's possible the build artifacts on disk have shifted in a way that affects which `.dll` Windows' loader picks at test time.
5. **Surface diagnostic info on failure.** Add an `ITestOutputHelper` log of the delegate's `Method.Name`, the GCHandle status, and the native install table's contents (via a new export) on `Assert.Null` failure. Combine with [[project_loader_lock_harness_ci_flake]] item C.

## Recommended composite fix

Mirror the LoaderLockHarness flake-fix recommendation:

| Option | Cost | Risk |
|--------|------|------|
| **A. Pin delegate with `GCHandle`.** If the test currently relies on locals to keep delegates alive, replace with explicit `GCHandle.Alloc(...)` + dispose. | Trivial (~5 LoC). | Lowest-risk and probably the right fix; gives the test a deterministic shape that the JIT can't optimize away. |
| **B. Add retry-on-fail (3-attempt aggregate).** Wrap the install+collect+trigger in a loop and aggregate. | Moderate. | Bandaid; doesn't fix the underlying flake; may mask real regressions. |
| **C. Diagnostic logging on failure.** ITestOutputHelper surface the native install-table state. | Trivial. | Diagnostic-only; combine with A. |
| **D. Disable the test.** | Trivial. | **Do not pick.** C-04 / R-H were specifically about callback dispatch safety; this test is the regression guard for it. |

Recommended composite: **A + C** (deterministic delegate pinning + on-failure diagnostics).

## Files to touch when fixing

1. `UtinniCoreDotNet.Tests/GameCallbacksTests.cs` — GCHandle.Alloc + ITestOutputHelper (option A + C)
2. Possibly `UtinniCore/swg/game/game.h` or wherever `Utinni_InstallCallback` is exported — if the native side has the keep-alive bug instead of the test
3. Commit message scope: `fix(c-04): ...` or `test(c-04): ...` (the underlying concern is C-04 callback dispatch from Phase 2)

## See also

- `[[project_loader_lock_harness_ci_flake]]` — sibling flake; same shared-CI-runner class
- `.planning/phases/02-critical-bug-burn-down-c-01-c-15/02-03-PLAN.md` — C-04 origin
- Run `26336088047` (the SHA-stable repeat-failure case)
- 05-VERIFICATION.md — verifier surfaced this finding 2026-05-23
