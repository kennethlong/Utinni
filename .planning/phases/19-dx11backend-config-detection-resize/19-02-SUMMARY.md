---
phase: 19-dx11backend-config-detection-resize
plan: 02
subsystem: graphics
tags: [dx11, dxgi, imgui, render-backend, detour, hook-tier, warp, resize, abi-pin]

# Dependency graph
requires:
  - phase: 19-dx11backend-config-detection-resize
    plan: 01
    provides: Dx11Backend decl behind IRenderBackend + dx11Singleton() + pinned dxgi_Present_Index=8/dxgi_ResizeBuffers_Index=13 + imgui dx11-binding + D-06 gate (DX11/DXGI tokens)
  - phase: 18-render-backend-seam
    provides: IRenderBackend 10-vtable ABC + Dx9Backend twin + render_backend.cpp (Option-A split) + setup(HWND) seam
provides:
  - directX11:: DXGI hook tier (hkSwapChainPresent idx 8 + hkResizeBuffers idx 13 + GetHookPoints consumer + DetourXS install)
  - directX11::kickoff() registered from graphics.cpp::hkInstall (the single owned kick-off site; per-frame prePresent tryInstall poll)
  - Dx11Backend impl (render_backend_dx11.cpp) — per-frame backbuffer RTV rebind (flip-discard) + onPreResize/onPostResize RTV release/recreate + init(device, context)
  - Dx11DummyDeviceHarvestTests [dxgi][harvest] — WARP vtbl idx 8/13 + no-leak (skip-with-log if WARP absent)
  - HeaderDiscovery decision: directx11.h parses clean, left DISCOVERED (no exclusion)
affects: [Plan 03 (detection wiring at imgui_impl.cpp setup() against directX11::tryInstall + selectBackend; setup() must SKIP its dx9Singleton()->init(nullptr) when the installed backend is not the dx9 singleton)]

# Tech tracking
tech-stack:
  added: [d3d11.lib/dxgi.lib on the test link line (WARP harvest CALLS the API), <imgui_impl_dx11.h> consumed in render_backend_dx11.cpp]
  patterns:
    - "Advertised-contract consumer (GetHookPoints poll) REPLACES the D3D9 throwaway-device harvest in production — the live swapchain, not a wrapped throwaway"
    - "Bootstrap-correct kick-off: a per-frame prePresent callback installed at graphics-install time polls until the DXGI swapchain latches (a DXGI Present detour cannot exist before the swapchain exists)"
    - "Flip-discard per-frame RTV rebind (cached RTV; recreated only on resize — heap-free hot path)"
    - "True DXGI ResizeBuffers (release-before / recreate-after the original) — the inverse of the D3D9 never-Reset rule"

key-files:
  created:
    - UtinniCore/swg/graphics/directx11.h
    - UtinniCore/swg/graphics/directx11.cpp
    - UtinniCore/swg/graphics/render_backend_dx11.cpp
    - UtinniCore.Tests/Graphics/Dx11DummyDeviceHarvestTests.cpp
  modified:
    - UtinniCore/swg/graphics/graphics.cpp
    - UtinniCore/UtinniCore.vcxproj
    - UtinniCore.Tests/UtinniCore.Tests.vcxproj

key-decisions:
  - "directx11.h forward-declares ID3D11Device/ID3D11DeviceContext (return-type names) and includes only <dxgi1_2.h> — keeps <d3d11.h> out of the header (D-05); the concrete D3D11 types live only in directx11.cpp + render_backend_dx11.cpp"
  - "GetHookPoints-absent and DXGI-init-null are both one-shot logged graceful bails (no overlay rather than crash/half-install) — T-19-04 / checker Blocker 1"
  - "directx11.h parses CLEAN through the clang-11 CppSharp parser (alongside directx9.h) — left DISCOVERED; NO HeaderDiscovery.cs exclusion (unlike render_backend.h which pulls <imgui.h>)"
  - "render_backend_dx11.cpp NOT added to the NoDeviceResetTests guarded list (Pitfall 4) — it uses m_rtv->Release(), not ->Reset(/.Reset("

patterns-established:
  - "DXGI hook-tier twin of directx9.cpp: trampoline typedefs + vtbl-index enum + hook bodies + advertised-contract consumer + DetourXS install + WR-03 cleanup"
  - "Single owned kick-off site discipline: the recurring DX11 acquisition poll runs from graphics.cpp (a plan-declared file), not utinni.cpp, not a bootstrap-broken self-detour"

requirements-completed: [RNDR-02, RNDR-04]

# Metrics
duration: ~25min
completed: 2026-06-15
---

# Phase 19 Plan 02: Dx11Backend + directx11 Hook Tier Summary

**Built the DX11 backend twin behind the Phase 18 seam: a `directx11.{cpp,h}` DXGI hook tier (Present idx 8 + ResizeBuffers idx 13 + the `GetHookPoints` advertised-contract consumer + DetourXS install), the `Dx11Backend` impl (flip-discard per-frame RTV rebind + true DXGI resize release/recreate), the single-owned `directX11::kickoff()` per-frame poll registered from `graphics.cpp::hkInstall`, and the WARP harvest/no-leak test — with `directx11.h` confirmed to parse clean through CppSharp.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-06-15
- **Tasks:** 3
- **Files modified:** 7 (4 created, 3 modified)

## Accomplishments
- **DXGI hook tier (`directx11.{cpp,h}`):** `hkSwapChainPresent` (one-shot first-fire log, `imgui_impl::render()` before the original, no `blockPresentCall`/depth reach-in), `hkResizeBuffers` (`onPreResize` -> original -> `onPostResize`, returns the original's HRESULT — no `DXGI_ERROR_INVALID_CALL` path), the `GetHookPoints` consumer (`GetModuleHandleA` gl11_r/_d -> `GetProcAddress` -> poll `swapChain != null` -> LOCKED install), DetourXS install off the live swapchain vtbl, and a WR-03 `cleanup()` that releases nothing (borrowed pointers never Released).
- **LOCKED install sequence** in `tryInstall`: stash borrowed `{swapChain, device, context}` -> `Detour::Create` both (CheckPointer-guarded, `DETOUR_TYPE_PUSH_RET`/`DETOUR_LEN_AUTO`) -> `render_backend::set(dx11Singleton())` -> `dx11Singleton()->init(device, context)` (bail + `set(nullptr)` if HWND null) -> `imgui_impl::setup(hwnd)` -> latch. Idempotent: a second `tryInstall` after the latch is a no-op (Pitfall 3).
- **`directX11::kickoff()`** defined in `directx11.cpp` and CALLED from `graphics.cpp::hkInstall` beside `directX::detour()` (the single owned kick-off site). It subscribes a file-local poll thunk via `Graphics::subscribePrePresentCallback`; the thunk calls `tryInstall()` each frame until latched, then unsubscribes itself (T-19-09 symmetric subscribe/unsubscribe).
- **`Dx11Backend` twin (`render_backend_dx11.cpp`):** all 10 vtable overrides + non-virtual `init(device, context)` + `dx11Singleton()`. `newFrame()` rebinds the cached backbuffer RTV (`OMSetRenderTargets`) before `ImGui_ImplDX11_NewFrame` (flip-discard); `onPreResize/onPostResize` release/recreate the RTV (D-18); `createBackbufferRtv()` releases the `GetBuffer` temp; scene depth/color return `(ImTextureID)0` (MVP).
- **WARP harvest test** `[dxgi][harvest]` green (5 assertions): vtbl idx 8/13 resolve to distinct non-null code addresses + `device->Release()` returns refcount 0 (no leak); WARP-absent is a SKIP.
- **CppSharp:** `directx11.h` parses CLEAN (left DISCOVERED, no exclusion). Full graphics/seam test set green: **138 assertions / 7 cases** across `[dxgi][rndr01][rndr02][rndr03][resid04]`.

## Task Commits

Each task was committed atomically:

1. **Task 1: directx11 DXGI hook tier + kickoff poll from hkInstall** — `2dbdb03` (feat)
2. **Task 2: Dx11Backend seam twin — per-frame RTV rebind + resize recreate** — `71a29fc` (feat)
3. **Task 3: WARP dummy-device harvest + no-leak; directx11.h parses clean** — `79370d4` (test)

_Note: Tasks 1 and 2 are `tdd="true"`. These are native hook-tier TUs whose behavior (DXGI detours against a live SWG client) is not unit-testable out of the box; the "test" layer is the pre-existing device-free Catch2 fences from Plan 01 (`[dxgi][offsets]` ABI pin, `[rndr02][graphics]` mock-dispatch, the D-06 neutrality gate) plus Task 3's WARP harvest — all of which stayed/went green. Acceptance is the grep-gate set + green inline x86 Release build + the harvest assertions._

## Files Created/Modified
- `UtinniCore/swg/graphics/directx11.h` — NEW `directX11::` free-function decls (`kickoff/tryInstall/cleanup/getSwapChain/getDevice/getContext`) + `dxgi_Present_Index=8`/`dxgi_ResizeBuffers_Index=13` + `<dxgi1_2.h>` + `ID3D11Device/Context` forward-decls. Nothing UTINNI_API.
- `UtinniCore/swg/graphics/directx11.cpp` — NEW DXGI hook tier (hooks + `GetHookPoints` consumer + DetourXS install + `kickoff()` poll registration + WR-03 cleanup).
- `UtinniCore/swg/graphics/render_backend_dx11.cpp` — NEW `Dx11Backend` impl + `dx11Singleton()` + `createBackbufferRtv()` helper.
- `UtinniCore/swg/graphics/graphics.cpp` — `#include "directx11.h"` beside `directx9.h`; `directX11::kickoff()` called in `hkInstall` beside `directX::detour()`.
- `UtinniCore/UtinniCore.vcxproj` — registered `directx11.cpp` + `render_backend_dx11.cpp` (`<ClCompile>`) + `directx11.h` (`<ClInclude>`).
- `UtinniCore.Tests/Graphics/Dx11DummyDeviceHarvestTests.cpp` — NEW WARP harvest + no-leak test.
- `UtinniCore.Tests/UtinniCore.Tests.vcxproj` — registered the harvest test + added `d3d11.lib;dxgi.lib` to the three link configs.

## Decisions Made
- **`directx11.h` forward-declares the D3D11 device types** (`struct ID3D11Device;`/`struct ID3D11DeviceContext;`) and includes only `<dxgi1_2.h>` — the accessor return types name them by pointer without pulling `<d3d11.h>` into the header (D-05 gate). The first build failed exactly here (`<dxgi1_2.h>` declares `IDXGISwapChain1` but not the device types); the forward-decls are the same discipline `render_backend.h` already uses.
- **HeaderDiscovery: left `directx11.h` DISCOVERED.** Ran `UtinniCoreDotNetGen.exe` to completion; it parsed `swg\graphics\directx11.h` clean alongside `directx9.h`/`backend_select.h`, generated `UtinniCore.cs`+`Std.cs`, exited 0 — no clang-11 AccessViolation. `<dxgi1_2.h>` + the two forward-decl structs do not fault the parser (unlike `<imgui.h>`, which forces `render_backend.h`'s exclusion). No `HeaderDiscovery.cs` change.
- **`d3d11.lib`/`dxgi.lib` added to the TEST link line only** — the harvest test CALLS `D3D11CreateDeviceAndSwapChain` (it must link the import libs), whereas `directx11.cpp` only patches function bodies it reads off the live vtable (no link dep). UtinniCore.dll itself needs no new lib.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] directx11.h needed ID3D11Device/Context forward-decls**
- **Found during:** Task 1 (first build)
- **Issue:** directx11.h's `getDevice()`/`getContext()` accessor return types name `ID3D11Device*`/`ID3D11DeviceContext*`, but the header includes only `<dxgi1_2.h>` (which declares `IDXGISwapChain1`, not the D3D11 device types) — C2143/C4430 syntax-error cascade. The plan's `<action>` listed only `<dxgi1_2.h>` for the header.
- **Fix:** Added `struct ID3D11Device;` / `struct ID3D11DeviceContext;` forward-decls (the same D-05 discipline `render_backend.h:43-45` uses), keeping `<d3d11.h>` out of the header.
- **Files modified:** UtinniCore/swg/graphics/directx11.h
- **Commit:** `2dbdb03`

**2. [Rule 1 - Grep-gate hygiene] comments named gated tokens `Present1` / `ImGui_ImplDX11`**
- **Found during:** Task 1 (acceptance grep)
- **Issue:** Two acceptance criteria require `grep -c "Present1" directx11.cpp == 0` and `grep -c "ImGui_ImplDX11" directx11.cpp == 0`. Descriptive comments literally contained "NOT Present1" and "ImGui_ImplDX11_Init inside" / "does NOT call ImGui_ImplDX11_*" — tripping the literal grep gates (the `feedback_gsd_grep_gate_hygiene` lesson).
- **Fix:** Reworded the three comments to describe intent without the gated tokens ("the IDXGISwapChain1 variant", "the imgui DX11-backend binding"). Comment-only; no logic/build change.
- **Files modified:** UtinniCore/swg/graphics/directx11.cpp
- **Verification:** both greps now return 0; rebuilt green.
- **Commit:** `2dbdb03`

**3. [Rule 3 - Blocking] DetourXS include path convention**
- **Found during:** Task 1 (first build)
- **Issue:** I initially wrote `#include "external/DetourXS/detourxs.h"`; the repo convention (per `utinni.h:32`) is `#include "DetourXS/detourxs.h"`, resolved via the `$(SolutionDir)external` additional-include dir. (The misleading first error was the whole TU set failing on `DetourXS/detourxs.h` because building the bare `.vcxproj` left `$(SolutionDir)` wrong — building via `Utinni.sln -t:UtinniCore` fixed the macro.)
- **Fix:** Use `#include "DetourXS/detourxs.h"`; build via the solution (`-t:UtinniCore`) so `$(SolutionDir)external` resolves.
- **Files modified:** UtinniCore/swg/graphics/directx11.cpp
- **Commit:** `2dbdb03`

---

**Total deviations:** 3 auto-fixed (1 Rule 1, 2 Rule 3). All mechanical (missing forward-decls, grep-gate wording, include-path/build-invocation convention). No scope creep; no architectural change.

## Issues Encountered
- Building the bare `UtinniCore.vcxproj` (not via `Utinni.sln`) sets `$(SolutionDir)` to the project dir, dropping the `external` include dir and failing every TU on `DetourXS/detourxs.h`. Build native waves via `Utinni.sln -t:<Project>` so the solution-relative include/lib macros resolve. (Also: Git Bash mangles `/p:` MSBuild switches — use `-p:`.)

## Known Stubs
- **DX11 scene depth/color accessors return `(ImTextureID)0`** (render_backend_dx11.cpp `sceneDepthTexture/sceneColorTexture`/`sceneDepthStage` -> 0, `setSceneDepthStage` -> no-op). Intentional MVP: no DX11 depth-SRV is wired this phase (mirrors the RESEARCH "Architectural Responsibility Map"). The seam contract is satisfied API-neutrally; a DX11 depth SRV is a later (post-v2.1) pass, not a Plan-03 blocker.

## Next Phase Readiness
- **Plan 03** wires detection at `imgui_impl.cpp::setup()` against `selectBackend()` (Plan 01's neutral `backend_select.h`) and `directX11::tryInstall()`. CRITICAL hand-off: Plan 03 Task 1 must make `setup()` SKIP its own `dx9Singleton()->init(nullptr)` when the installed backend is NOT the dx9 singleton — the DX11 device init already happens inside `directX11::tryInstall` step 4 (`dx11Singleton()->init(device, context)`). Keep `directx11.h` OUT of imgui_impl.cpp (D-06 gate) — the install poll already lives in the hook tier via `kickoff()`.
- **Open / deferred:** `WM_SIZE` (window-drag) resize for the embedded panel is the spec §6 open question — the `ResizeBuffers` hook covers `WM_DISPLAYCHANGE` only; the client-side `WM_SIZE->ResizeBuffers` (spec §6 Option 1) is outside this consumer plan. Tracked under RESID-04 / `swg-window-resize-fullscreen-edge-cases`.

## Self-Check: PASSED

- Files: directx11.h, directx11.cpp, render_backend_dx11.cpp, Dx11DummyDeviceHarvestTests.cpp, 19-02-SUMMARY.md — all FOUND.
- Commits: 2dbdb03, 71a29fc, 79370d4 — all FOUND in git history.
- UtinniCore.dll builds green (x86 Release, inline); `[dxgi][harvest]` green (5 assertions); graphics/seam suite green (138 assertions / 7 cases); Generated/UtinniCore.cs NOT staged.

---
*Phase: 19-dx11backend-config-detection-resize*
*Completed: 2026-06-15*
