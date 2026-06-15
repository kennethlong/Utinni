---
phase: 19-dx11backend-config-detection-resize
plan: 01
subsystem: infra
tags: [dx11, dxgi, imgui, vcpkg, render-backend, catch2, abi-pin]

# Dependency graph
requires:
  - phase: 18-render-backend-seam
    provides: IRenderBackend 10-vtable ABC + Dx9Backend twin + render_backend.cpp (Option-A split) + D-06 neutrality gate + RenderBackendSeamTests MockBackend
provides:
  - vcpkg imgui dx11-binding feature (imgui_impl_dx11.h + ImGui_ImplDX11_* compiled into imgui.lib)
  - Dx11Backend twin declaration behind the IRenderBackend seam (decl-only; impl is Plan 02)
  - dx11Singleton() accessor declaration
  - D-06 neutrality gate extended to ban DX11/DXGI concrete symbol forms
  - backend_select.h — neutral pure selectBackend(bool,bool) decision function
  - Dx11VtblOffsetTests (Present=8 / ResizeBuffers=13 ABI-drift fence)
  - Dx11DetectionTests (four-state detection/fallback unit test)
  - RenderBackendSeamTests Dx11 mock-dispatch case
affects: [Plan 02 (render_backend_dx11.cpp + directx11.cpp impl against these decls), Plan 03 (detection wiring against selectBackend + imgui_impl.cpp install branch)]

# Tech tracking
tech-stack:
  added: [imgui dx11-binding (vcpkg feature toggle, no new package), DXGI/<dxgi1_2.h> test-only include]
  patterns:
    - "Interface-first ordering: contracts + device-free tests land before any DX11 impl TU"
    - "Neutral pure-decision header (backend_select.h) includable by the API-neutral consumer without tripping the D-06 grep gate"
    - "Device-free Catch2 layers: vtbl-offset static pin + pure-function unit + shared-ABC mock-dispatch (no live device, runs in CI)"

key-files:
  created:
    - UtinniCore/swg/graphics/backend_select.h
    - UtinniCore.Tests/Graphics/Dx11VtblOffsetTests.cpp
    - UtinniCore.Tests/Graphics/Dx11DetectionTests.cpp
  modified:
    - vcpkg.json
    - UtinniCore/swg/graphics/render_backend.h
    - UtinniCore.Tests/Graphics/ImguiApiNeutralityTests.cpp
    - UtinniCore.Tests/Graphics/RenderBackendSeamTests.cpp
    - UtinniCore.Tests/UtinniCore.Tests.vcxproj

key-decisions:
  - "Dx11Backend init() takes BOTH device + immediate context (off-vtable, signature differs from Dx9's single-device init) — ImGui_ImplDX11_Init needs both"
  - "selectBackend picks Dx11 ONLY when (gl11Present && getHookPointsResolved); every ambiguous/partial state defaults to Dx9 (conservative, D-15/D-17)"
  - "vtbl-offset pin uses named local constants + static_assert + compile-time SDK-member ABI cross-check (no device/WARP harvest needed for the L1 fence)"
  - "Dx11 mock-dispatch reuses the device-free MockBackend against the shared IRenderBackend ABC — never instantiates the concrete Dx11Backend (Option-A split avoids the LNK2019 cascade)"

patterns-established:
  - "Pattern 1: Neutral pure-decision header so the API-neutral consumer can include the selection logic without a DX11/DXGI symbol leak"
  - "Pattern 2: ABI-drift fence via named-constant pin + compile-time SDK-member existence check"

requirements-completed: [RNDR-02, RNDR-03]

# Metrics
duration: ~35min
completed: 2026-06-15
---

# Phase 19 Plan 01: Dx11Backend Contracts + Test Scaffolding Summary

**Landed the Phase 19 interface-first scaffolding: vcpkg imgui dx11-binding, the Dx11Backend twin declared behind the existing IRenderBackend seam (zero DX11 includes), the D-06 neutrality gate extended to DX11/DXGI symbol forms, a neutral pure selectBackend() decision, and three device-free Catch2 layers (offset pin, detection unit, Dx11 mock-dispatch).**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-06-15T19:57Z
- **Completed:** 2026-06-15T~22:10Z
- **Tasks:** 3
- **Files modified:** 8 (3 created, 5 modified)

## Accomplishments
- Enabled the vcpkg imgui `dx11-binding` feature — `imgui_impl_dx11.h` now resolves and `ImGui_ImplDX11_*` is compiled into `imgui.lib` (the hard Wave-0 prerequisite for Plans 02/03).
- Declared `Dx11Backend final : public IRenderBackend` (10 vtable overrides mirroring Dx9Backend + a non-virtual `init(ID3D11Device*, ID3D11DeviceContext*)` + `dx11Singleton()`), with the D-05 purge held: forward-decls only, no `<d3d11.h>`/`<dxgi1_2.h>` in the seam header.
- Extended the D-06 API-neutrality gate with a concrete DX11/DXGI banned-token set wired into both file SECTIONs + the stripper-hygiene self-check; `[rndr01][graphics]` stays green (imgui_impl still neutral).
- Added `backend_select.h` with a pure `selectBackend(bool,bool)` decision and three device-free Catch2 layers (`[dxgi][offsets]`, `[rndr03][detect]`, `[rndr02][graphics]`).
- Full native suite green: **209 assertions / 32 test cases**.

## Task Commits

Each task was committed atomically:

1. **Task 1: Enable vcpkg imgui dx11-binding** - `c129e39` (chore)
2. **Task 2: Declare Dx11Backend + extend D-06 neutrality gate** - `14f9762` (feat; TDD test-extension + decl verified green together)
3. **Task 3: DXGI offset pin + detection unit + Dx11 mock-dispatch** - `aaa7a51` (feat)

_Note: Tasks 2 and 3 are `tdd="true"`. The neutrality-gate extension (RED-shaped: it would fail if imgui_impl named a DX11 symbol) and the header decl were verified green in one commit because the decl is the thing that keeps the gate honest. Task 3's three tests are pure device-free layers verified green against the new `selectBackend`/seam._

## Files Created/Modified
- `vcpkg.json` - Added `"dx11-binding"` to the imgui features array.
- `UtinniCore/swg/graphics/render_backend.h` - Added `Dx11Backend` decl + `dx11Singleton()` + `ID3D11Device/Context/RenderTargetView` forward-decls.
- `UtinniCore/swg/graphics/backend_select.h` - NEW neutral pure `selectBackend()` decision (zero DX11/DXGI symbols).
- `UtinniCore.Tests/Graphics/ImguiApiNeutralityTests.cpp` - `dx11Tokens()` banned set + DX11 stripper-hygiene self-check, wired into both file SECTIONs.
- `UtinniCore.Tests/Graphics/Dx11VtblOffsetTests.cpp` - NEW Present=8 / ResizeBuffers=13 ABI-drift fence.
- `UtinniCore.Tests/Graphics/Dx11DetectionTests.cpp` - NEW four-state detection/fallback unit test.
- `UtinniCore.Tests/Graphics/RenderBackendSeamTests.cpp` - Dx11 sibling mock-dispatch case (`[rndr02][graphics]`).
- `UtinniCore.Tests/UtinniCore.Tests.vcxproj` - Registered the two new test .cpp (NOT the DX11 impl TUs — Option-A split).

## Decisions Made
- **Dx11Backend `init()` signature differs from Dx9** — takes device + immediate context (ImGui_ImplDX11_Init needs both), kept off-vtable per the seam-header rationale.
- **Conservative selection** — `selectBackend` returns Dx11 only when both gl11-presence and the GetHookPoints contract are positive; all other states default to Dx9 with a one-shot fallback log (caller-side, Plan 03).
- **L1 fence is compile-time, not a WARP harvest** — the offset pin uses named constants + `static_assert` + a compile-time SDK-member existence check (`&IDXGISwapChain::Present` etc.), which is a sufficient ABI-drift fence without instantiating a device. The optional WARP throwaway-harvest test (Dx11DummyDeviceHarvestTests, D-21 L3) was NOT in this plan's file list and is left to a later plan.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Plan-assumption correction] vcpkg ships the dx11 binding as header + in-lib, not a .cpp under include/**
- **Found during:** Task 1 (Enable vcpkg dx11-binding)
- **Issue:** The plan's acceptance criterion expected `vcpkg_installed/x86-windows/include/imgui_impl_dx11.cpp` to exist after reinstall. The vcpkg imgui port ships bindings as `imgui_impl_dx11.h` only and compiles the implementation directly into `imgui.lib` — exactly as it already does for dx9 (no `imgui_impl_dx9.cpp` exists under include/ either). The `.cpp`-under-include expectation was incorrect.
- **Fix:** Verified the real consumable contract instead: `imgui_impl_dx11.h` resolves AND `dumpbin /LINKERMEMBER imgui.lib` lists `ImGui_ImplDX11_*` symbols. Both confirmed present. No code change needed beyond the feature toggle.
- **Files modified:** vcpkg.json (the intended change)
- **Verification:** `imgui_impl_dx11.h` present; `ImGui_ImplDX11_Data/Init/...` in imgui.lib linker members; UtinniCore.dll + test exe build green.
- **Committed in:** `c129e39`

**2. [Rule 3 - Blocking] MSBuild does not auto-trigger the vcpkg manifest reinstall**
- **Found during:** Task 1 (Enable vcpkg dx11-binding)
- **Issue:** The project consumes prebuilt headers from `vcpkg_installed/x86-windows/` via include paths — it does NOT use the vcpkg MSBuild manifest-integration target. So building UtinniCore after editing vcpkg.json did NOT re-run the install; the dx11 binding stayed absent and the `vcpkg/status` file still listed only the three old imgui features.
- **Fix:** Ran the explicit manifest install exactly as CI does (`vcpkg install --triplet x86-windows --x-manifest-root --x-install-root`) using the full vcpkg checkout at `D:/vcpkg-bootstrap/vcpkg` (the `/tmp/utinni-vcpkg` toolhost lacked the x86-windows triplet). Install completed; imgui now resolves `[...,dx11-binding,...]`.
- **Files modified:** vcpkg_installed/ tree (build artifact, gitignored)
- **Verification:** Install reported `imgui[core,docking-experimental,dx11-binding,dx9-binding,win32-binding]:x86-windows`; header + lib symbols confirmed.
- **Committed in:** N/A (no tracked-file change; vcpkg_installed is gitignored build output)

**3. [Rule 1 - Grep-gate hygiene] backend_select.h comment named a gated token**
- **Found during:** Task 3 (selectBackend header)
- **Issue:** Task 3 acceptance requires `grep "ID3D11" backend_select.h` returns 0. A descriptive comment ("names ZERO ... no ID3D11* / IDXGI* symbols") literally contained `ID3D11`, tripping the literal grep gate (the feedback_gsd_grep_gate_hygiene lesson).
- **Fix:** Reworded the comment to describe the intent without the gated token. Comment-only change, no logic impact.
- **Files modified:** UtinniCore/swg/graphics/backend_select.h
- **Verification:** `grep -c "ID3D11" backend_select.h` == 0; `grep -c "IDXGI|<d3d11.h>|<dxgi" ` == 0; detection test still green.
- **Committed in:** `aaa7a51`

---

**Total deviations:** 3 auto-fixed (2 Rule 1, 1 Rule 3)
**Impact on plan:** All necessary for correctness/completion. Deviation 1 corrects an incorrect plan assumption about vcpkg packaging (the real contract is satisfied). Deviations 2-3 are mechanical (how the reinstall is triggered; grep-gate wording). No scope creep; no architectural change.

## Issues Encountered
- The `Dx11VtblOffsetTests` first build failed: my initial `static_cast` of `&IDXGISwapChain::ResizeBuffers` used the wrong parameter list (the real DXGI signature orders `DXGI_FORMAT` 4th among 6 params). Replaced the casts with plain `&Member` pointer-to-member references, which are the cleaner compile-time ABI existence check anyway. Rebuilt green.

## User Setup Required
None - no external service configuration required. (The vcpkg reinstall is a build-environment step, already run; CI re-runs it from vcpkg.json on a cold cache.)

## Next Phase Readiness
- **Plan 02** can now implement `render_backend_dx11.cpp` + `directx11.cpp` against the `Dx11Backend` decl, `dx11Singleton()`, and the pinned `dxgi_Present_Index=8`/`dxgi_ResizeBuffers_Index=13` constants (keep them in lockstep with `Dx11VtblOffsetTests`).
- **Plan 03** can wire the imgui_impl.cpp install branch against `selectBackend()` from the neutral `backend_select.h` (no gate trip).
- **Open / deferred:** the optional WARP throwaway-device harvest test (D-21 L3, `Dx11DummyDeviceHarvestTests`) was outside this plan's file list — a later plan owns it. The vcpkg `.cpp`-under-include acceptance line should be corrected in the plan template if reused.

## Self-Check: PASSED

- Files: backend_select.h, Dx11VtblOffsetTests.cpp, Dx11DetectionTests.cpp, 19-01-SUMMARY.md, vcpkg.json — all FOUND.
- Commits: c129e39, 14f9762, aaa7a51 — all FOUND in git history.

---
*Phase: 19-dx11backend-config-detection-resize*
*Completed: 2026-06-15*
