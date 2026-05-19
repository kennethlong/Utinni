---
status: resolved
phase: 02-critical-bug-burn-down-c-01-c-15
source: [02-VERIFICATION.md]
started: 2026-05-17T02:50:00Z
updated: 2026-05-18T23:05:00Z
---

## Current Test

Both Tier-4 manual UATs PASSED on 2026-05-18 against the user's local SWGEmu client (`D:\SWGEmu-Client\SWGEmu\SWGEmu.exe`, ProductName=Star Wars Galaxies, VersionInfo 0.0.119.798) injected via the `scripts\local-test-setup.ps1 -WithPlugins -Launch` flow.

## Tests

### 1. Live SWG injection — C-01 full proof

expected: SWG launches cleanly, editor comes up with plugins loaded, no loader-lock hang.
result: **passed** — 2026-05-18. Editor host + The Jawa Toolbox plugin came up alongside the SWG client window. Control flowed past `LoadLibrary` → `utinni_init` → CLR bring-up → MEF plugin discovery → FormMain → TJT visible and selectable. Implicit proof of no loader-lock hang (deadlock would have manifested before any of those steps completed).

**Bugs surfaced en route to passing (NOT in scope for the C-01 fix but worth recording):**

- **`directX::swgptr` namespace-qualification typo** in `UtinniCore/test_exports.cpp:71` — Plan 02-02 wave-0 P/Invoke scaffolding referenced `directX::swgptr*` but `swgptr` is a global `using` in `utinni.h`, not a member of the directX namespace. Caught by the orchestrator's local MSBuild gate before push; fixed in commit `cb547bb`.
- **Missing `using System;`** in `UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs` — caught by code review CR-01, fixed in commit `92758ff` before the live UAT run.
- **`UtinniCoreDotNet.Tests` SDK-default Compile glob pulled in Fixtures plugins' auto-generated AssemblyInfo** producing CS0579 duplicate-attribute errors at build time. Fixed in commit `9a108d1` (DefaultItemExcludes for Fixtures\\**).
- **`TheJawaToolboxDotNet.csproj` `<Reference Include="UtinniCoreDotNet">` HintPath hard-coded `RelWithDbgInfo`** instead of `$(Configuration)`. Cross-repo fix in `kennethlong/UtinniPlugins` commit `3254059`.
- **`utinni_init` export decoration mismatch** — UtinniCore.dll exported `_utinni_init@4` (decorated stdcall name) but the Launcher's `GetProcAddress` asked for undecorated `utinni_init`. THIS WAS THE BUG THE TIER-4 UAT WAS DESIGNED TO CATCH: Plan 02-03's LoaderLockHarness only timed `LoadLibraryA` and never called `GetProcAddress`, so the mismatch was invisible to automated tests. Fixed in commit `389fc83` via `#pragma comment(linker, "/EXPORT:utinni_init=_utinni_init@4")`. **This finding feeds Phase 02.1 — should drive an additional harness that exercises GetProcAddress lookups for every documented C-export.**

**Resume signal received:** *implicit* — user reported "both windows came up and I can select Jawa Toolbox" 2026-05-18T18:03:09Z.

---

### 2. Live SWG minimize/restore — C-09 full proof

expected: No UI hang (window animates smoothly), no CPU spike on UI thread, editor remains responsive. Confirms `EventWaitHandle.WaitOne(100ms)` replaced the `Thread.Sleep(1)` spin correctly end-to-end.
result: **passed** — 2026-05-18. Minimize/restore stress test against the live SWG client window; editor host stayed responsive throughout, no UI thread CPU spike.

**Resume signal received:** *"C-09 complete"* — user 2026-05-18T18:05:10Z.

---

## Side-quest finding (NOT a UAT failure — surfaces Phase 02.1 work)

**WR-03 live-confirmed (partial fix in 02.1; exit dialog STILL fires).** On exit from the injected session, SWGEmu showed a "Direct3D could not be correctly initialized" error dialog. Standalone SWGEmu.exe (no Utinni injection) does NOT show this dialog on exit — comparison confirmed by user. This matches the WR-03 prediction from `02-REVIEW.md`.

**Update 2026-05-18 post Phase 02.1:** Plan 02.1-02 successfully eliminated the `delete depthTexture` UAF in `directX::cleanup()` — verified empty body at `directx9.cpp:410-427` after Phase 02.1's eager-init refactor. However, **the exit dialog STILL fires** on the user's machine after Phase 02.1's fix landed. The earlier "no exit dialog" UAT report from 2026-05-18 was incorrect — the user later noticed the dialog had been appearing all along, just delayed/missed. The cleanup-side UAF is fixed; the remaining failure mode is a DIFFERENT teardown path, likely `clr::stop()` (called immediately after `cleanup()` in `detatch()` at `utinni.cpp:153-157`) or SWG's own D3D9 device-release validator. Investigation deferred to Phase 03 (or a Phase 02.2 mini-effort). Tracked in `STATE.md` Blockers/Concerns. Non-blocking for Phase 02 closure — both C-01 and C-09 manual UATs remain PASSED on their own criteria.

## Summary

total: 2
passed: 2
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

None blocking Phase 02 closure. One non-blocking side-quest finding (WR-03 live repro) recorded above; resolution tracked under Phase 02.1 Plan 02.1-02.
