---
phase: 24-client-entry-point-advertisement-getenginehookpoints
plan: 03
subsystem: graphics
tags: [native-cpp, x86, injection, getenginehookpoints, epa03, dx11-kickoff, d3d9-gate, dual-path]

# Dependency graph
requires:
  - phase: 24-01
    provides: the swg::endpoints resolver (pure resolve + lookupByName) + Wave-0 Catch2 harness
  - phase: 24-02
    provides: the full-catalog s_bindings[] including the graphics::install row that EPA-03 depends on
provides:
  - the EPA-03 D-05 Approach A coupling confirmed end-to-end - graphics::install resolves from the table (Plan 02) so the unchanged hkInstall detour fires on the real Graphics::install on the advertised client and directX11::kickoff() runs naturally (no new trigger site, Pitfall 6 honored)
  - a D3D11-safety gate on directX::detour() - the D3D9 throwaway-device harvest no-ops on the advertised D3D11 client (gl11_{r,d}.dll loaded); SWGEmu (no gl11) runs the harvest byte-for-byte unchanged (D-00)
  - a headless [endpoints][epa03] regression unit locking the graphics::install binding (drop-it-and-the-DX11-overlay-silently-regresses guard)
affects: [24-04 maintainer live-smoke]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "D3D11-client detection mirror: isD3D11Client() = GetModuleHandleA(gl11_r.dll || gl11_d.dll) != nullptr -- the SAME signal directX11::tryInstall() uses to detect the from-source D3D11 render module. SWGEmu Pre-CU never loads gl11, so the gate is a strict false there and the D3D9 path is untouched (D-00)."
    - "EPA-03 Approach A is a NO-new-code coupling: the kickoff site in hkInstall is unchanged; resolving graphics::install (Plan 02) is what redirects hkInstall to the real function on the advertised client. The hardcoded 0x007548A0 was the wrong address on SwgClient_r.exe -- the exact reason the DX11 overlay never started before."

key-files:
  created: []
  modified:
    - UtinniCore/swg/graphics/directx9.cpp
    - UtinniCore/swg/graphics/graphics.cpp
    - UtinniCore.Tests/endpoints_tests.cpp

key-decisions:
  - "directX::detour() lives in directx9.cpp, not the plan's referenced directx.cpp (no such file). The directX:: namespace is split: directx9.cpp owns detour()/getVtbl(), directx9.h declares it. Edited directx9.cpp; the [endpoints][epa03] unit is the headless gate the plan's directx.cpp <verify> path stood in for."
  - "Open Q3 resolution: directX::detour() is ALREADY crash-safe -- getVtbl() creates its OWN throwaway D3D9 device via Direct3DCreate9 and null-checks every step (Direct3DCreate9, CreateWindow, CreateDevice); it NEVER harvests SWG's device, so there is no non-existent D3D9 device to deref. The added gate is an OPTIMIZATION+CORRECTNESS guard (avoid creating a pointless HAL device + 7 dead detours on a D3D11 client), not a crash fix. Chose to gate rather than rely on the no-op-safety because creating a real device the client never uses is wasteful and could perturb a D3D11 driver."
  - "Gate signal is gl11 presence (the D3D11 render module), NOT render_backend::get() == dx11 -- at hkInstall time the DX11 backend is not yet installed (tryInstall runs later, on the prePresent poll), so render_backend would still be the dx9 default. gl11-loaded is the correct early-boot tell."
  - "The kickoff site itself is byte-for-byte unchanged (Approach A, not B) -- only documentation comments added at the hkInstall kickoff site explaining the resolved-install chain."

patterns-established:
  - "Gate a SWGEmu-only D3D9 mechanism off on the advertised client by the gl11-loaded signal at the top of the function, returning early with a log; the SWGEmu fall-through path stays untouched (mirrors the dual-path discipline of the resolver itself)."

requirements-completed: [EPA-03]

# Metrics
duration: ~25min
completed: 2026-06-21
---

# Phase 24 Plan 03: EPA-03 DX11 Kickoff Decouple + D3D9-Harvest D3D11 Gate Summary

**Closed EPA-03 via D-05 Approach A: confirmed that resolving `graphics::install` from the advertised table (Plan 02) is sufficient to make the existing `hkInstall` detour fire on the real `Graphics::install` on the D3D11 client so `directX11::kickoff()` runs naturally with no new trigger site -- then gated `directX::detour()` (the D3D9 throwaway-device harvest) to no-op on the advertised D3D11 client (gl11 loaded) while leaving the SWGEmu D3D9 path byte-for-byte unchanged, and locked the EPA-03 dependency with a headless `[endpoints][epa03]` unit.**

## Performance
- **Duration:** ~25 min
- **Completed:** 2026-06-21
- **Tasks:** 2
- **Files modified:** 3 (0 created, 3 modified)

## Accomplishments
- **Confirmed EPA-03 Approach A end-to-end (no new code at the trigger site):** `graphics::install` is bound in `s_bindings[]` (`endpoints_bindings.cpp:374`, Plan 02). On the advertised client the resolver overwrites that literal, so the *unchanged* `hkInstall` detour installs on the REAL `Graphics::install` and `directX11::kickoff()` fires as part of the existing chain. Documented this at the kickoff site; added NO new kickoff call (verified `utinni.cpp` has zero `directX11::kickoff` -- Pitfall 6 honored).
- **Resolved Open Q3 and added the D3D11-safety gate:** traced `directX::detour()` -> `getVtbl()` and confirmed it creates its OWN throwaway D3D9 device (via `Direct3DCreate9`, every step null-checked) and never touches SWG's device -- it was already crash-safe. Added `isD3D11Client()` (gl11_{r,d}.dll loaded) as an early-return gate at the top of `directX::detour()` so the pointless harvest + 7 dead detours are skipped on the advertised D3D11 client. The DXGI overlay (`directX11::kickoff` -> `tryInstall`) owns rendering there.
- **SWGEmu D3D9 path byte-for-byte unchanged (D-00):** on SWGEmu, `gl11` is never loaded, so `isD3D11Client()` is false and `directX::detour()` falls through to the harvest exactly as today. The gate only short-circuits the gl11-loaded case.
- **Locked the dependency headlessly:** added `[endpoints][epa03]` -- a synthetic table advertising `graphics::install` resolves the binding (slot = fixture address), and an absent-`graphics::install` fixture leaves the install literal untouched (SWGEmu no-op). If a future change drops `graphics::install` from `s_bindings[]`, the DX11 overlay would silently regress on the advertised client; this unit catches it in CI so the maintainer live-smoke (Plan 04) only proves render.

## Task Commits
1. **Task 1: directX::detour D3D11 gate + EPA-03 Approach A confirmation** - `a4e5e72` (feat)
2. **Task 2: [endpoints][epa03] regression unit** - `7a709f3` (test)

## Files Created/Modified
- `UtinniCore/swg/graphics/directx9.cpp` - added `isD3D11Client()` + the early-return gate at the top of `directX::detour()`; the D3D9 throwaway-device harvest now no-ops on the advertised D3D11 client.
- `UtinniCore/swg/graphics/graphics.cpp` - documentation-only at `hkInstall`: the `directX::detour()` D3D11 no-op note + the EPA-03 Approach A explanation at the unchanged `directX11::kickoff()` site.
- `UtinniCore.Tests/endpoints_tests.cpp` - the `[endpoints][epa03]` unit (2 sections: advertised graphics::install overwrites its slot; absent graphics::install leaves the RVA literal).

## Decisions Made
- **`directX::detour()` is in `directx9.cpp`, not `directx.cpp`** (the plan's referenced path and `<verify>` grep target). No `directx.cpp` exists; the `directX::` namespace is split `directx9.cpp` (body) / `directx9.h` (decl). See Deviations.
- **The gate is correctness+optimization, not a crash fix** -- `directX::detour()` was already crash-safe (own device, null-checks). Gating avoids a needless real HAL device + 7 dead detours on the D3D11 client.
- **Gate signal = gl11-loaded, not `render_backend::get()`** -- at `hkInstall` time the DX11 backend is not yet installed (`tryInstall` runs later on the prePresent poll), so `render_backend` is still the dx9 default. `gl11`-loaded is the correct early-boot D3D11 tell (same signal `tryInstall` uses).
- **Kickoff site byte-for-byte unchanged (Approach A, not B)** -- only comments added; the resolved `graphics::install` is what redirects the hook.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Plan path `directx.cpp` does not exist -- the target is `directx9.cpp`**
- **Found during:** Task 1 (read_first -- `UtinniCore/swg/graphics/directx.cpp` not found).
- **Issue:** The plan's `files_modified`, `read_first`, and the Task 1 `<verify>` grep all name `UtinniCore/swg/graphics/directx.cpp`. There is no such file. The `directX::detour()` body (the D3D9 throwaway-device harvest / `getVtbl`) lives in `UtinniCore/swg/graphics/directx9.cpp`; `directx9.h` declares the `directX::` namespace.
- **Fix:** Edited `directx9.cpp` (the actual home of `directX::detour()`). The Task 1 `<verify>`'s `directx.cpp` reference is moot -- its two real gates (`grep directX11::kickoff` in `graphics.cpp` = present; in `utinni.cpp` = absent) both pass, and the `directX::detour()` change is proven by the clean build + the `[endpoints][epa03]` unit.
- **Files modified:** UtinniCore/swg/graphics/directx9.cpp
- **Verification:** UtinniCore.dll builds clean (Release/x86); the gate is the only behavioral change and short-circuits only the gl11-loaded case.
- **Committed in:** a4e5e72

**Total deviations:** 1 auto-fixed (blocking path correction). None architectural; none required user input.

## Open Q3 Finding (recorded per the plan)
`directX::detour()` (`directx9.cpp:553`) was **already crash-safe on a D3D11-only client BEFORE this plan**: it calls `getVtbl()` (`directx9.cpp:460`), which creates a **throwaway D3D9 device via the public `Direct3DCreate9` API** (NOT a harvest of SWG's device) and null-checks every step -- `GetModuleHandleA("d3d9.dll")` (line 471-476), `Direct3DCreate9` (478-485, 487-492), `CreateWindowExA` (495-506), and `CreateDevice(HAL)` (522-540) -- bailing gracefully (critical log, no deref) at each. There is therefore no "non-existent D3D9 device" to deref. The added gate (`isD3D11Client()`, lines added at the top of `detour()`) is an optimization+correctness guard, not a crash fix: on the advertised D3D11 client it skips creating a real but useless HAL device and installing 7 detours on `d3d9.dll` functions SWG never calls in D3D11 mode. The SWGEmu D3D9 branch (no gl11 -> gate false -> fall through to `getVtbl`) is unchanged.

## Known Stubs
None introduced. (The Plan 02 full-catalog inert slots remain as documented in 24-02-SUMMARY; this plan touched none of them.)

## Issues Encountered
- `Generated/UtinniCore.cs` churns on every UtinniCore build (CppSharp) -- `git checkout --`'d it after both builds, never committed (repo discipline).
- Pre-existing `utini.h` C4091/C4251 + `object.h` C4099 warnings surfaced compiling `graphics.cpp` -- out of scope (not caused by this task); not fixed.
- Git MSYS path-mangling on MSBuild switches -- used the dash form (`-t:`/`-p:`/`-m`), inherited 24-01/24-02 gotcha.
- LF->CRLF warning on the test file commit -- cosmetic (Git autocrlf), no content impact.

## User Setup Required
None. The maintainer live-smoke (inject into `SwgClient_r.exe`, confirm DX11 overlay renders + SWGEmu D3D9 still works) is Plan 04, not this plan -- deferred per the plan and the locked maintainer-only live-verification constraint.

## Next Phase Readiness
- EPA-03 is closed headlessly: the kickoff chain is confirmed (Approach A), the D3D9 harvest is D3D11-safe, and the `graphics::install` binding is unit-locked.
- **Plan 04** (maintainer live-smoke) should verify on `SwgClient_r.exe`: (1) no `0xC0000005` at the first detour (criterion 1, the config crash), (4) the DX11 overlay renders (criterion 4, EPA-03 -- the chain proven here fires at runtime), and (3) the SWGEmu Pre-CU D3D9 live-smoke is unchanged (criterion 3, D-00). It should NOT exercise the two signature-concern rows (`worldSnapshot::addObject`, `treeFile::open`) flagged in 24-02.
- No blockers.

## Self-Check: PASSED
- Files: directx9.cpp / graphics.cpp / endpoints_tests.cpp all modified + present.
- Commits: a4e5e72 FOUND, 7a709f3 FOUND.
- Build: UtinniCore.dll + UtinniCore.Tests.exe clean (Release/x86). `[endpoints][epa03]` 4 assertions / 1 case PASS (exit 0); full `[endpoints]` 186 assertions / 8 cases PASS; full native suite 400 assertions / 41 cases PASS.
- Grep gates: `directX11::kickoff` present in graphics.cpp, absent in utinni.cpp.

---
*Phase: 24-client-entry-point-advertisement-getenginehookpoints*
*Completed: 2026-06-21*
