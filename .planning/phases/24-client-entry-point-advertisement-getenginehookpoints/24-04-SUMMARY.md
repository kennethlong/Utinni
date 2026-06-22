---
phase: 24-client-entry-point-advertisement-getenginehookpoints
plan: 04
subsystem: infra
tags: [native-cpp, x86, injection, getenginehookpoints, live-smoke, automation-gate, dumpbin, cppsharp, abi-gate, maintainer-checkpoint]

# Dependency graph
requires:
  - phase: 24-01
    provides: the swg::endpoints resolver TU + Wave-0 Catch2 harness (the headless precondition for the live-smoke)
  - phase: 24-02
    provides: the full-catalog s_bindings[] (77 of 78) + D-04 read->call adaptation
  - phase: 24-03
    provides: EPA-03 Approach A (graphics::install resolved -> hkInstall fires -> directX11::kickoff) + the D3D9-harvest D3D11 gate
  - phase: swg-client-v2 (provider, external)
    provides: the SwgClient_r.exe / SwgClient_d.exe builds carrying the GetEngineHookPoints export
provides:
  - the automation-gate result (full solution build + native Catch2 suite + dumpbin export precheck) gating the 3 maintainer live-smokes
  - a Rule-3 blocking fix (HeaderDiscovery endpoints.h exclusion) that unbreaks the regenerated managed bridge
  - the recorded live-smoke acceptance results for the 3 maintainer checkpoints (PENDING maintainer injection)
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Automation-gate-first: the full MSBuild + dotnet test lane (run for the FIRST time in this phase here) surfaces regen-driven managed breaks that a native-only Catch2 run cannot -- plans 24-01..03 never compiled UtinniCoreDotNet, so the CppSharp CS0266 stayed latent until this gate."
    - "CppSharp discovery exclusion for zero-UTINNI_API seams: a public internal-resolver header (endpoints.h) with no managed surface is excluded at HeaderDiscovery pre-parse, exactly as Phase 18 excludes render_backend.h -- never via the AST ignore list."

key-files:
  created:
    - .planning/phases/24-client-entry-point-advertisement-getenginehookpoints/24-04-SUMMARY.md
  modified:
    - UtinniCoreDotNetGen/HeaderDiscovery.cs

key-decisions:
  - "endpoints.h is excluded from CppSharp header discovery (Rule 3 blocking fix). It declares zero UTINNI_API symbols; the generated Binding/Slot (void** slot) property tripped CS0266 in the regenerated UtinniCore.cs and broke the managed build. Mirrors the Phase-18 render_backend.h exclusion; no managed surface lost."
  - "The ABI baseline re-bless (AbiSurfaceTests: 28 ADDED, 0 REMOVED) is DEFERRED to the maintainer as a Rule-4 ABI-contract decision -- it requires the documented lockstep TJT rebuild + MEF-compose verification (Phase-17 CPPS-04), which is part of the maintainer live-smoke flow. NOT auto-re-blessed by the executor."

requirements-completed: []  # EPA-01/02/03 are live-proven only by the 3 maintainer checkpoints (still pending)

# Metrics
duration: ~5min
completed: 2026-06-22
---

# Phase 24 Plan 04: Maintainer Live-Smoke Acceptance Summary

**The automation gate (full solution build + native Catch2 suite + dumpbin export precheck) for the
Phase-24 maintainer live-smoke. The dumpbin precheck PASSES (`GetEngineHookPoints` confirmed on
`SwgClient_r.exe`); the full build PASSES after a Rule-3 blocking fix (excluding the zero-UTINNI_API
`endpoints.h` from CppSharp discovery to unbreak the regenerated managed bridge); the native suite is
green. One managed-lane failure remains — the Phase-17 ABI gate flags 28 new Phase-24 bindings (0
removed) and needs a maintainer re-bless. The 3 live-smoke checkpoints are PENDING maintainer
injection.**

## Task 1 — Automation Gate

**Status: PASS (build + native suite green; dumpbin export confirmed) with one deferred managed-lane re-bless (see below).**

### (a) Full solution build — VS2026 MSBuild, Release/x86

`MSBuild Utinni.sln -p:Configuration=Release -p:Platform=x86 -m` → **exit 0** (build succeeded)
after the Rule-3 fix.

- First attempt FAILED with `UtinniCore.cs(27343,32): error CS0266: Cannot implicitly convert type
  'System.IntPtr' to 'void**'` in the **CppSharp-regenerated** `Generated/UtinniCore.cs`.
- Root cause: 24-01 added `swg/endpoints.h`, whose public `Binding { void** slot; }` struct declares
  ZERO `UTINNI_API` symbols (an internal injection-side resolver seam). CppSharp nonetheless emitted a
  managed binding whose `Slot` property getter returns `IntPtr` into a `void**`-typed property → CS0266.
  Plans 24-01..03 ran only the native Catch2 suite, so the regen-driven break stayed latent until this
  full MSBuild + managed lane.
- Fix (Rule 3 blocking): excluded `endpoints.h` at `HeaderDiscovery` pre-parse, exactly as Phase 18
  excludes `render_backend.h`. The seam projects no managed surface; nothing is lost. Committed `4af6c12`.
- After the fix: full solution builds clean — `UtinniCore.dll`, `UtinniCoreDotNet.dll`, both managed
  test DLLs, `Launcher.exe`, `utinni-cli.exe`, `UtinniCore.Tests.exe` all produced. `Generated/UtinniCore.cs`
  churn was `git checkout --`'d (never committed, per repo discipline).

### (b) Native Catch2 suite

- `UtinniCore.Tests.exe "[endpoints]"` → **exit 0**, **186 assertions in 8 test cases** (all passed).
- Full native suite (`UtinniCore.Tests.exe`) → **exit 0**, **400 assertions in 41 test cases** (all passed).

### (c) Managed lanes — `dotnet test --no-build` (per-project; solution-scope trips MSB4278 on the C++ vcxproj)

- `UtinniCoreDotNet.Tests` → **855 passed, 1 FAILED, 0 skipped (856 total)**.
  - The single failure is `AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn`
    (the Phase-17 CPPS-04 ABI gate). It reports **28 ADDED, 0 REMOVED** blocks.
  - **0 REMOVED** confirms the `endpoints.h` discovery exclusion lost no baseline surface, and no prior
    binding regressed.
  - **28 ADDED** are legitimate NEW `UTINNI_API` bindings introduced across 24-01..03 (e.g.
    `getObjectTemplateName@WorldSnapshotReaderWriter`, the `object.cpp` / `world_snapshot.cpp` full-catalog
    slots from 24-02). The blessed ABI baseline was never re-blessed during 24-01..03 because the managed
    lane was never run — this gate is precisely what catches that.
  - **Disposition (Rule 4 — ABI contract):** re-blessing the baseline is a deliberate managed-ABI
    contract change with a `MissingMethodException`-at-MEF-compose risk class; the documented Phase-17
    process requires a lockstep TJT rebuild + frozen-DLL MEF-compose verification, which is part of the
    maintainer live-smoke flow (the maintainer rebuilds TJT and injects). The executor does NOT
    auto-re-bless. See "Deferred / Maintainer Action" below.
- `Utinni.Cli.Tests` → **511 passed, 0 failed, 2 skipped (513 total)** — green.

### (d) dumpbin export precheck (T-24-10 mitigation) — PASS

`dumpbin /exports D:/Code/swg-client-v2/stage/SwgClient_r.exe` (run with `MSYS_NO_PATHCONV=1` to
defeat Git-Bash path-mangling of the `/exports` switch) confirms the advertised contract is present in
the build under test. **Exact matching export line:**

```
         82   51 00700280 GetEngineHookPoints = _GetEngineHookPoints
```

(ordinal 82, hint 51, RVA 00700280, undecorated `GetEngineHookPoints`; the import-library alias is the
decorated `_GetEngineHookPoints`.) `SwgClient_r.exe` is freshly staged (28 MB, 2026-06-21 17:32). The
sibling `SwgClient_d.exe` carries the same export at ordinal 83. `SwgClient_o.exe` was NOT tested
(pre-existing LNK1281 SAFESEH; never exports — per 24-RESEARCH Environment Availability). The provider
binary is present and advertised → the live-smoke is NOT gated on a missing provider build.

### Automation-gate verdict

| Gate | Result |
|------|--------|
| Full solution build (Release/x86) | PASS (after Rule-3 fix `4af6c12`) |
| Native `[endpoints]` filter | PASS — 186 assertions / 8 cases, exit 0 |
| Full native Catch2 suite | PASS — 400 assertions / 41 cases, exit 0 |
| `Utinni.Cli.Tests` managed lane | PASS — 511 passed / 2 skipped |
| `UtinniCoreDotNet.Tests` managed lane | 855 pass / **1 fail** (ABI re-bless needed — maintainer action) |
| `dumpbin /exports SwgClient_r.exe` → `GetEngineHookPoints` | PASS — `82 51 00700280 GetEngineHookPoints` |

The build is green, the export is confirmed, and the native suite is fully green. The single managed
failure is a known ABI re-bless that is a maintainer step in the live-smoke flow (Rule 4), not a code
defect. The live-smoke precondition is met for the maintainer to proceed.

## Deferred / Maintainer Action (ABI re-bless)

`AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn` will stay red until the
blessed baseline (`UtinniCoreDotNet.Tests/Fixtures/abi-baseline-blockhashes.txt`) is re-blessed to
include the 28 new Phase-24 bindings (0 removed). Per the Phase-17 CPPS-04 design this is intentionally
a maintainer step: run `AbiBlockHash.Rebless`, rebuild TJT, confirm the frozen-DLL MEF-compose still
composes (no `MissingMethodException`), and commit the regenerated fixture together. This dovetails with
the maintainer live-smoke (the maintainer is already rebuilding/injecting TJT). It is recorded here as a
gate finding, NOT auto-applied by the executor.

## Checkpoint 1 — No createDetours() crash on SwgClient_r.exe (criterion 1 / EPA-01/EPA-02)

[pending maintainer live-smoke]

**How to verify (verbatim from the plan):**
1. Confirm `dumpbin /exports stage/SwgClient_r.exe` shows `GetEngineHookPoints` (from Task 1 — **DONE, PASS**: `82 51 00700280 GetEngineHookPoints`).
2. Launch `swg-client-v2/stage/SwgClient_r.exe` and inject UtinniCore (the usual Launcher path).
3. Watch the Utinni log: confirm the resolver's resolved/missing/coverage summary line appears and that `config::loadOverrideConfig` resolved to a non-null table address.
4. Confirm NO `VEH FATAL 0xC0000005 ... READ target=0x00401000` (the exact 2026-06-15 crash) and no crash in `createDetours()`. Reaching a stable post-detour state = pass (criterion 1 / EPA-01/EPA-02).

**Resume signal:** Type "approved" if no 0xC0000005 crash and the resolver summary shows config::loadOverrideConfig resolved; otherwise describe the crash target/log lines.

## Checkpoint 2 — DX11 overlay renders on SwgClient_r.exe (criterion 4 / EPA-03 — closes D-08/D-22)

[pending maintainer live-smoke]

**How to verify (verbatim from the plan):**
1. With UtinniCore injected into `SwgClient_r.exe` (rasterMajor=11 / D3D11 client), confirm the ImGui overlay is VISIBLE and renders.
2. Confirm overlay input maps correctly (render-target space) — mouse/keyboard reach the overlay.
3. Confirm the log shows `directX11::kickoff` + `directX11::tryInstall: D3D11 overlay installed` (the advertised swapchain latched). This closes the Phase-18 D-08 and Phase-19 D-22 live-smokes (criterion 4 / EPA-03).

**Resume signal:** Type "approved" if the DX11 overlay renders + takes input on SwgClient_r.exe; otherwise describe what failed (no overlay / no input / kickoff log absent).

## Checkpoint 3 — SWGEmu D3D9 live-smoke unchanged — no regression (criterion 3 / EPA-02 / D-00)

[pending maintainer live-smoke]

**How to verify (verbatim from the plan):**
1. Inject UtinniCore into the existing SWGEmu Pre-CU (D3D9) client as you do today.
2. Confirm the log shows the resolver's "no GetEngineHookPoints export — RVA path (SWGEmu)" info line (the strict no-op branch).
3. Confirm the ImGui overlay renders and mouse + keyboard work exactly as before — no regression (criterion 3 / EPA-02). A TJT-driven scene change landing "naked but in world" is the documented baseline, NOT a regression.

**Resume signal:** Type "approved" if the SWGEmu D3D9 overlay + input are unchanged from today; otherwise describe the regression.

## Task Commits
1. **Task 1 fix: exclude endpoints.h from CppSharp discovery (unbreak managed build)** - `4af6c12` (fix)

(The draft SUMMARY commit follows; the 3 checkpoint result sections are filled in after the maintainer live-smoke.)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] CppSharp regen of endpoints.h broke the managed build (CS0266)**
- **Found during:** Task 1 (full solution build — the automation gate).
- **Issue:** `swg/endpoints.h` (added in 24-01) has a public `Binding { void** slot; }` struct with zero `UTINNI_API` symbols. CppSharp generated a managed binding whose `Slot` getter returns `IntPtr` into a `void**` property → `error CS0266` in the regenerated `Generated/UtinniCore.cs`, failing `UtinniCoreDotNet`. Latent because 24-01..03 ran only the native Catch2 suite (never the regen-driven managed compile).
- **Fix:** Excluded `endpoints.h` at `HeaderDiscovery` pre-parse, mirroring the Phase-18 `render_backend.h` exclusion. The seam projects no managed surface, so nothing is lost. Reverted the generated-file churn (`git checkout --`).
- **Files modified:** `UtinniCoreDotNetGen/HeaderDiscovery.cs`
- **Verification:** Full solution builds clean (exit 0); `endpoints` bindings absent from the regenerated file (grep count 0); native suite green (400/41; `[endpoints]` 186/8); ABI gate REMOVED=0 (no baseline surface lost).
- **Committed in:** `4af6c12`

**Total deviations:** 1 auto-fixed (Rule 3 blocking). The ABI re-bless is deferred to the maintainer (Rule 4), not auto-applied.

## Known Stubs
None introduced by this plan. (The 24-02 full-catalog inert slots remain as documented in 24-02-SUMMARY.)

## Issues Encountered
- Git-Bash MSYS path-mangling turned `dumpbin /exports` into a Windows path (`LNK1181: cannot open input file 'C:\Program Files\Git\exports'`); fixed by prefixing `MSYS_NO_PATHCONV=1`. Inherited gotcha (24-01..03 used the `-t:`/`-p:` dash form for MSBuild).
- `dotnet test` at solution scope errors `MSB4278` on the C++ `.vcxproj` projects (the dotnet CLI cannot import `Microsoft.Cpp.Default.props`); ran the two managed xUnit projects directly instead.
- `Generated/UtinniCore.cs` churns on every UtinniCore build (CppSharp) — `git checkout --`'d each time, never committed (repo discipline).
- Pre-existing CS0108 "hides inherited member" warnings in the generated file (Camera/NetworkScene/GroundScene/ClientObject Unk* + Detour/Name) — out of scope, not caused by this plan.

## User Setup Required
The 3 maintainer live-smokes (inject into `SwgClient_r.exe` and the SWGEmu Pre-CU client) are
irreducibly manual (maintainer-only injection per AGENTS.md — no headless path). Additionally, the ABI
re-bless (rebuild TJT + re-freeze the fixture + MEF-compose check + commit) is a maintainer step that
dovetails with the live-smoke.

## Next Phase Readiness
- Automation gate met: build green, export confirmed, native suite green. The maintainer can proceed
  with the 3 live-smoke checkpoints. This plan is NOT complete until the 3 checkpoint results are
  recorded and the ABI baseline is re-blessed.
- The live-smoke should NOT exercise the two signature-concern rows (`worldSnapshot::addObject`,
  `treeFile::open`) flagged in 24-02.
