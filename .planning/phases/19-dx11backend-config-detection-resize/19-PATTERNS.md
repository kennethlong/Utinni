# Phase 19: Dx11Backend + Config Detection + Resize - Pattern Map

**Mapped:** 2026-06-15
**Files analyzed:** 11 (3 new source + 2 modified source + 1 modified manifest + 4 new tests + 1 extended test; plus 2 build-registration touch-points)
**Analogs found:** 11 / 11 (every file has a strong in-repo analog — this phase is a verified translation job, not invention)

This phase TRANSLATES the live-verified D3D9 hook tier (`directx9.cpp`) and its Phase-18 seam twin (`render_backend_dx9.cpp`) into a DXGI/D3D11 twin behind the already-built `IRenderBackend` 10-vtable seam. Every new file copies a concrete analog. The single net-new piece with no perfect analog is the advertised-contract consumer (`GetHookPoints()` poll), which replaces the D3D9 throwaway-device harvest — but even that mirrors the `getVtbl()` `GetModuleHandleA`/`GetProcAddress` shape.

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| NEW `UtinniCore/swg/graphics/directx11.cpp` | hook-tier (detour install + Present/Resize hooks + contract consumer) | event-driven (per-frame Present/Resize callbacks) | `UtinniCore/swg/graphics/directx9.cpp` | exact (role + data flow) |
| NEW `UtinniCore/swg/graphics/directx11.h` | hook-tier interface (free-function decls) | event-driven | `UtinniCore/swg/graphics/directx9.h` | exact |
| NEW `UtinniCore/swg/graphics/render_backend_dx11.cpp` | seam backend impl (the `Dx11Backend` twin) | request-response (vtable dispatch) + transform (RTV rebind) | `UtinniCore/swg/graphics/render_backend_dx9.cpp` | exact |
| MODIFIED `UtinniCore/swg/graphics/render_backend.h` | seam ABC header (add `Dx11Backend` decl + `dx11Singleton()`) | request-response | the existing `Dx9Backend` decl block in the same file | exact (in-file) |
| MODIFIED `UtinniCore/swg/ui/imgui_impl.cpp` ~`setup()` (263) | install seam (detection branch) | request-response (one-shot install) | the `render_backend::set(dx9Singleton())` call at line 283 + `directx9.cpp::getVtbl()` `GetModuleHandleA` shape | exact |
| MODIFIED `UtinniCore/utinni.cpp` `createDetours()`/init | config/lifecycle (detour registration + before-detours init ordering) | event-driven (install ordering) | `directX::initPresentBlockedEvent`/`initDepthTexture` call sites (371-372) + `directX::detour()` call in `graphics.cpp::hkInstall` (611) | role-match (ordering, not literal new detour call here — see note) |
| MODIFIED `vcpkg.json` (repo root) | config (add imgui `dx11-binding` feature) | n/a | the existing imgui `features` array (lines 19-26) | exact |
| NEW `UtinniCore.Tests/Graphics/Dx11VtblOffsetTests.cpp` | test (D-21 L1: pin Present=8/ResizeBuffers=13) | n/a | the `D3DInformation` enum + `getVtbl()` harvest in `directx9.cpp` (112-234, 460-551) | role-match |
| NEW `UtinniCore.Tests/Graphics/Dx11DummyDeviceHarvestTests.cpp` | test (D-21 L3: WARP harvest + no-leak) | n/a | `directx9.cpp::getVtbl()` throwaway-device harvest (460-551) | exact (this IS the D3D9 harvest, ported to DXGI/WARP) |
| NEW `UtinniCore.Tests/Graphics/Dx11DetectionTests.cpp` | test (D-21 L4: detection/fallback logic) | n/a | `RenderBackendSeamTests.cpp` (mock + REQUIRE structure) | role-match |
| EXTENDED `UtinniCore.Tests/Graphics/ImguiApiNeutralityTests.cpp` | test (extend D-06 gate to ban DX11/DXGI symbols) | n/a | the `dx9Tokens()` set in the same file (60-88) | exact (in-file extension) |
| EXTENDED `UtinniCore.Tests/Graphics/RenderBackendSeamTests.cpp` | test (D-21 L2: Dx11 mock-dispatch) | n/a | the `MockBackend` in the same file (41-103) | exact (in-file / sibling) |

**Build-registration touch-points (not source files but required):**
- `UtinniCore/UtinniCore.vcxproj` — add `directx11.cpp`, `render_backend_dx11.cpp` (`<ClCompile>`), `directx11.h` (`<ClInclude>`).
- `UtinniCore.Tests/UtinniCore.Tests.vcxproj` — add the 4 new test `.cpp` (`<ClCompile>`).
- `UtinniCoreDotNetGen/HeaderDiscovery.cs` — evaluate whether `directx11.h` / `render_backend.h`'s new DX11 surface needs the parse-stage exclusion (see Shared Pattern: CppSharp parse-stage exclusion).

---

## Pattern Assignments

### NEW `UtinniCore/swg/graphics/directx11.cpp` (hook-tier, event-driven)

**Analog:** `UtinniCore/swg/graphics/directx9.cpp` (the entire `directX::` namespace)

This is the DXGI twin of the D3D9 hook tier. It owns: the trampoline typedefs, the `hkSwapChainPresent`/`hkResizeBuffers` hook bodies, the advertised-contract consumer (`GetHookPoints()` poll — REPLACES the throwaway-device harvest in production per D-10), and the DetourXS install. It is one of only two new TUs allowed to include `<d3d11.h>`/`<dxgi1_2.h>` (the other is `render_backend_dx11.cpp`).

**Includes pattern** — mirror `directx9.cpp:25-34`, swapping the DX9 headers for DXGI:
```cpp
#include "directx11.h"
#include <d3d11.h>
#include <dxgi1_2.h>           // IDXGISwapChain1 lives here (the producer returns it)
#include <imgui_impl_dx11.h>   // ONLY if this TU drives ImGui_ImplDX11_*; otherwise keep it in render_backend_dx11.cpp
#include "utinni.h"
#include "swg/ui/imgui_impl.h"
#include "render_backend.h"    // stash live swapchain/device + drive setup(HWND)
```

**Trampoline typedef pattern** (`directx9.cpp:90-97` declares the `using p*` aliases). The DXGI analog (from RESEARCH Pattern 1, grounded in producer `Direct3d11_Device.cpp:1138` `Present(1,0)`):
```cpp
// base IDXGISwapChain::Present (NOT Present1); IDXGISwapChain1 inherits the same slot.
using pSwapChainPresent = HRESULT(__stdcall*)(IDXGISwapChain* pSwapChain, UINT SyncInterval, UINT Flags);
using pResizeBuffers    = HRESULT(__stdcall*)(IDXGISwapChain* pSwapChain, UINT BufferCount, UINT Width, UINT Height, DXGI_FORMAT NewFormat, UINT SwapChainFlags);
pSwapChainPresent origPresent;
pResizeBuffers    origResizeBuffers;
```

**Vtable-index enum pattern** — `directx9.cpp:112-234` enumerates all 119 D3D9 vtable slots as named constants. For DXGI, pin only the two hooked indices (DETOUR target safety + the D-21 L1 offset test reads these):
```cpp
// IDXGISwapChain vtable: 3 IUnknown + 5 IDXGIObject/DeviceSubObject + Present at 8, ResizeBuffers at 13.
enum DxgiSwapChainVtbl { dxgi_Present_Index = 8, dxgi_ResizeBuffers_Index = 13 };
```

**hkPresent hook body** — mirror `directx9.cpp::hkPresent` (266-388). Copy the one-shot first-fire diagnostic (279-322), the `imgui_impl::render()` call BEFORE the original (326), and the `!imgui_impl::isReady()` one-shot install gate (378-386). The DXGI body:
```cpp
HRESULT __stdcall hkSwapChainPresent(IDXGISwapChain* sc, UINT syncInterval, UINT flags)
{
    static bool s_firstPresent = true;
    if (s_firstPresent) { s_firstPresent = false; utinni::log::info("directx11::hkSwapChainPresent: first fire (DXGI detour confirmed)"); }

    imgui_impl::render();                                  // -> seam newFrame()/renderDrawData() (see render_backend_dx11.cpp)
    return origPresent(sc, syncInterval, flags);
}
```
**KEY DIFFERENCE from D3D9:** there is NO `blockPresentCall` branch and NO `depthTexture->createTexture` reach-in (DX11 has no `directX::getDepthTexture()` equivalent this phase — `sceneDepthTexture()` returns `(ImTextureID)0`, RESEARCH Pattern 1). The present-block was a D3D9 WinForms-minimize workaround; do NOT carry it unless live-smoke shows the same crash on DX11.

**hkResizeBuffers hook body** — NO D3D9 analog (D3D9 hkReset at 390-403 is the structural cousin, but the discipline inverts: D3D9 NEVER Resets; DX11 DOES release/recreate). From RESEARCH Pattern 2 (grounded in producer `Direct3d11_Device.cpp:1199-1216`):
```cpp
HRESULT __stdcall hkResizeBuffers(IDXGISwapChain* sc, UINT bc, UINT w, UINT h, DXGI_FORMAT fmt, UINT flags)
{
    render_backend::get()->onPreResize();                 // Dx11Backend: release ONLY Utinni's own RTV (before original)
    HRESULT hr = origResizeBuffers(sc, bc, w, h, fmt, flags);
    render_backend::get()->onPostResize();                // Dx11Backend: GetBuffer(0)+CreateRenderTargetView from resized backbuffer
    return hr;
}
```
Null-guard `render_backend::get()` (the seam pointer is nullable until install). Pitfall 2: release Utinni's RTV BEFORE the original runs or `ResizeBuffers` returns `DXGI_ERROR_INVALID_CALL`.

**Advertised-contract consumer (`tryInstall`)** — REPLACES the D3D9 `getVtbl()` throwaway harvest (`directx9.cpp:460-551`) in production. Reuse the `GetModuleHandleA`/`GetProcAddress` shape from `getVtbl()` (471-485), but read the LIVE swapchain instead of creating a throwaway device. From RESEARCH Pattern 3 + spec §3.1 (struct MUST be byte-identical to the producer — Pitfall 6):
```cpp
struct UtinniDx11HookPoints { IDXGISwapChain1* swapChain; ID3D11Device* device; ID3D11DeviceContext* context; };
using pGetHookPoints = UtinniDx11HookPoints(__cdecl*)();

bool tryInstall()   // polled once/frame until swapChain != null, then latched
{
    HMODULE hGl11 = GetModuleHandleA("gl11_r.dll");
    if (!hGl11) hGl11 = GetModuleHandleA("gl11_d.dll");   // debug client
    if (!hGl11) return false;
    auto getHP = (pGetHookPoints)GetProcAddress(hGl11, "GetHookPoints");
    if (!getHP) { utinni::log::warning("gl11_r.dll lacks GetHookPoints; no D3D11 overlay"); return false; } // spec §7.2 graceful bail
    UtinniDx11HookPoints hp = getHP();
    if (hp.swapChain == nullptr) return false;            // not ready — poll again next frame (spec §3.3)
    // read vtbl idx 8/13 off the LIVE swapchain, Detour::Create both, then ImGui_ImplDX11_Init via the seam.
    return true;
}
```

**Detour install pattern** — copy `directx9.cpp::detour()` (553-641) verbatim in shape: read the address off the vtable, `Detour::CheckPointer`, `Detour::Create(..., DETOUR_TYPE_PUSH_RET)`. Reading the live swapchain vtable:
```cpp
swgptr* vtbl = *(swgptr**)hp.swapChain;                   // mirrors directx9.cpp:542 `*(swgptr**)pDevice`
swgptr presentAddr = Detour::CheckPointer(vtbl[dxgi_Present_Index]);
origPresent = (pSwapChainPresent)Detour::Create((LPVOID)presentAddr, hkSwapChainPresent, DETOUR_TYPE_PUSH_RET);
swgptr resizeAddr = Detour::CheckPointer(vtbl[dxgi_ResizeBuffers_Index]);
origResizeBuffers = (pResizeBuffers)Detour::Create((LPVOID)resizeAddr, hkResizeBuffers, DETOUR_TYPE_PUSH_RET);
```
Prefer `DETOUR_LEN_AUTO` (`[[feedback_detourxs_explicit_len]]`). The `Detour::CheckPointer` guard is the `directx9.cpp:569+` pattern.

**`cleanup()`** — mirror `directx9.cpp::cleanup()` (643-660): the WR-03 lesson (do NOT free borrowed/device-bound state on `DLL_PROCESS_DETACH`; OS reclaims on exit; UAF-on-exit is worse than a process-lifetime leak). Utinni's borrowed swapchain/device/context are NEVER Released (spec §4.1 / Anti-Patterns).

---

### NEW `UtinniCore/swg/graphics/directx11.h` (hook-tier interface)

**Analog:** `UtinniCore/swg/graphics/directx9.h` (entire file, 25-42)

Free-function decls in a `directX11::` namespace. Mirror `directx9.h` exactly: `#pragma once`, `#include "utinni.h"`, the DXGI include, then the namespace. The `UTINNI_API extern` markers on `directx9.h:38-41` are for symbols consumed managed-side / cross-module — only mark the DX11 functions `UTINNI_API` if a managed consumer needs them (the Dx11 hook tier likely needs NONE exported, unlike `directX::getDevice()`). Keep it lean:
```cpp
#pragma once
#include "utinni.h"
#include <dxgi1_2.h>          // IDXGISwapChain1 forward use
namespace directX11
{
    bool tryInstall();        // poll + install (Pattern 3); idempotent/latched
    void cleanup();
    IDXGISwapChain1* getSwapChain();
    ID3D11Device* getDevice();
    ID3D11DeviceContext* getContext();
} // namespace directX11
```

---

### NEW `UtinniCore/swg/graphics/render_backend_dx11.cpp` (seam backend impl)

**Analog:** `UtinniCore/swg/graphics/render_backend_dx9.cpp` (entire file — the structural template)

The `Dx11Backend final : public IRenderBackend` twin. ONLY new TU (alongside `directx11.cpp`) that includes `<imgui_impl_dx11.h>`. Owns the static-storage singleton + `dx11Singleton()`. Copy the file header rationale comment (25-33) adapting "DX9-bearing" → "DX11-bearing" and noting it pulls in `<imgui_impl_dx11.h>` + DetourXS + directx11 (which is why the D-21 L2 mock test must NOT instantiate the concrete type — it uses the device-free `IRenderBackend` ABC instead).

**Includes** (mirror `render_backend_dx9.cpp:34-39`):
```cpp
#include "render_backend.h"
#include <d3d11.h>
#include <dxgi1_2.h>
#include <imgui_impl_dx11.h>
#include "directx11.h"        // directX11:: getSwapChain/getDevice/getContext
#include "graphics.h"         // utinni::Graphics::getCurrentRenderTargetWidth/Height
#include <cassert>
```

**Per-frame hot-path overrides** — `render_backend_dx9.cpp:44-52` forwards to `ImGui_ImplDX9_NewFrame/RenderDrawData`. The DX11 twin DIFFERS: `newFrame()` must rebind the backbuffer RTV (flip-discard unbinds it after every Present — RESEARCH Pattern 4) BEFORE the imgui new-frame:
```cpp
void Dx11Backend::newFrame()
{
    // Flip-discard (producer Direct3d11_Device.cpp:570) unbinds the RTV after each Present.
    // Cache the RTV; recreate only on resize (heap-free hot path, [[project_rh_snapshot_no_heap_alloc]]).
    if (m_rtv == nullptr) createBackbufferRtv();          // GetBuffer(0) + CreateRenderTargetView
    ID3D11RenderTargetView* rtv = m_rtv;
    directX11::getContext()->OMSetRenderTargets(1, &rtv, nullptr);
    ImGui_ImplDX11_NewFrame();
}
void Dx11Backend::renderDrawData(ImDrawData* drawData) { ImGui_ImplDX11_RenderDrawData(drawData); }
```

**Resize overrides** — `render_backend_dx9.cpp:57-58` are honest NO-OPS (`Dx9Backend::onPreResize/onPostResize` empty bodies, D-03). The DX11 twin INVERTS this — they do real work (D-18):
```cpp
void Dx11Backend::onPreResize()  { if (m_rtv) { m_rtv->Release(); m_rtv = nullptr; } }   // release BEFORE original ResizeBuffers
void Dx11Backend::onPostResize() { createBackbufferRtv(); }                              // recreate from resized backbuffer
```

**RT-dims overrides** — copy `render_backend_dx9.cpp:61-69` VERBATIM (both forward to `utinni::Graphics::getCurrentRenderTargetWidth/Height` — API-neutral facade, D-19 reuses the same RT-space block).

**Scene depth/color + stage accessors** — `render_backend_dx9.cpp:73-105` reach into `directX::getDepthTexture()`. The DX11 MVP has no depth SRV wired (RESEARCH "Code Examples"): return `(ImTextureID)0` / `0` / no-op:
```cpp
ImTextureID Dx11Backend::sceneDepthTexture() { return (ImTextureID)0; }
ImTextureID Dx11Backend::sceneColorTexture() { return (ImTextureID)0; }
int  Dx11Backend::sceneDepthStage() { return 0; }
void Dx11Backend::setSceneDepthStage(int) {}
```

**Non-virtual `init()`** — `render_backend_dx9.cpp:122-147` does `GetCreationParameters` + `ImGui_ImplDX9_Init(device)` + returns `hFocusWindow`. The DX11 `init()` signature DIFFERS (device+context, off-vtable per render_backend.h:98 note): call `ImGui_ImplDX11_Init(device, context)` and derive HWND via `swapChain->GetHwnd()`/`GetDesc1()` (spec §3 / Assumption A3). Keep the stash pattern (`render_backend_dx9.cpp:110-118`) for the live swapchain/device/context.

**Static singleton + accessor** — copy `render_backend_dx9.cpp:149-155` verbatim, renaming `s_dx9Backend`→`s_dx11Backend`, `dx9Singleton`→`dx11Singleton`.

**Pitfall 4 (CRITICAL):** this file uses `ComPtr::Reset()` (RTV RAII) or `m_rtv->Release()`. Do NOT add it to `NoDeviceResetTests.cpp`'s guarded list — that gate counts `->Reset(`/`.Reset(` and would false-trip on ComPtr. (The guarded list is intentionally `directx9.cpp`, `direct_input.cpp`, `PanelGame.cs` only — NoDeviceResetTests.cpp:190-206.)

---

### MODIFIED `UtinniCore/swg/graphics/render_backend.h` (seam ABC header)

**Analog:** the existing `Dx9Backend` decl block in the same file (84-126)

Add a `Dx11Backend final : public IRenderBackend` declaration mirroring `Dx9Backend` (84-115) — the SAME 10 overrides, a non-virtual `init(ID3D11Device*, ID3D11DeviceContext*)` (signature differs from Dx9 per the line-98 comment), and a `dx11Singleton()` accessor mirroring `dx9Singleton()` (125).

**CRITICAL (D-05 purge):** render_backend.h MUST stay free of `<d3d11.h>`/`<dxgi1_2.h>`. The existing file forward-declares `struct IDirect3DDevice9;` at line 37 to keep `<d3d9.h>` out. Do the SAME for DX11:
```cpp
// Forward declarations only -- keeps <d3d11.h>/<dxgi1_2.h> out of this header (D-05 gate).
struct ID3D11Device;
struct ID3D11DeviceContext;
```
The header's existing comment (33: `#include <imgui.h> // ... NO <d3d9.h>`) governs — extend the spirit to NO `<d3d11.h>`.

---

### MODIFIED `UtinniCore/swg/ui/imgui_impl.cpp` ~`setup()` (line 263)

**Analog:** the unconditional `render_backend::set(render_backend::dx9Singleton())` at line 283 + the `GetModuleHandleA` shape from `directx9.cpp::getVtbl()` (471)

Replace line 283's unconditional set with the detection branch. From RESEARCH "Code Examples" (grounded in producer `Graphics.cpp:195-228` — gl11=D3D11, gl05/06/07=D3D9):
```cpp
// REPLACES line 283. D-15/D-16/D-17: one GetModuleHandle check, default D9, D11 only on positive detect.
if (GetModuleHandleA("gl11_r.dll") || GetModuleHandleA("gl11_d.dll")) {
    if (directX11::tryInstall()) {
        render_backend::set(render_backend::dx11Singleton());
        utinni::log::info("Render backend: D3D11 (gl11 detected, GetHookPoints advertised)"); // one-shot (RNDR-03)
    } else {
        render_backend::set(render_backend::dx9Singleton());
        utinni::log::warning("gl11 detected but GetHookPoints absent/not-ready; defaulting D3D9");
    }
} else {
    render_backend::set(render_backend::dx9Singleton());
    utinni::log::info("Render backend: D3D9 (default; no gl11 detected)"); // one-shot diagnostic (RNDR-03)
}
```

**CRITICAL (D-05/D-06 grep-gate):** imgui_impl.cpp MUST stay API-neutral. `GetModuleHandleA("gl11_r.dll")` is a plain Win32 string call (NOT a DX11 symbol) so it passes the gate. But `directX11::tryInstall()` — including `directx11.h` here — would introduce `directX11::` / `directx11.h`, which the EXTENDED neutrality gate bans (see Pitfall 5 / the extended `ImguiApiNeutralityTests`). RECONCILE: either (a) the detection+install lives in a thin API-neutral helper that forwards to `directX11::tryInstall()` from a TU that is allowed DX11, or (b) the install poll lives in the per-frame hook (`directx11.cpp`), and `setup()` only does the bare `GetModuleHandleA` select. RESEARCH Open Q2 + D-21 note leave the exact poll/install split to planner discretion — the planner MUST pick a shape that keeps `directx11.h` OUT of imgui_impl.cpp. (The existing `hkPresent` one-shot install at `directx9.cpp:378-386` is the model: the DX9 path does its device-bearing install from the hook tier, not from imgui_impl.)

The `isSetup`/`ImGui::CreateContext()` latch (imgui_impl.cpp:262-279) already guarantees exactly-one context (Pitfall 3 — no doubled input).

---

### MODIFIED `UtinniCore/utinni.cpp` `createDetours()` / init ordering

**Analog:** the before-detours eager-init at lines 371-372 + the `directX::detour()` call site in `graphics.cpp::hkInstall` (611)

NOTE: the D3D9 hook tier does NOT register its detour in `utinni.cpp::createDetours()` (109-166) — `directX::detour()` is invoked from `graphics.cpp::hkInstall()` (611) AFTER `swg::graphics::install()`, and the per-frame device stash+`setup()` happens in `hkPresent`. The DX11 path should follow the SAME deferred shape (the advertised swapchain may not exist at `createDetours()` time): the `directX11::tryInstall()` poll fires per-frame, not as a one-time `createDetours()` registration.

What utinni.cpp DOES need (if anything): preserve the **before-detours init ordering** contract (lines 367-375 comment: eager init runs BEFORE `createDetours()` so the render thread never races). DX11 has no `initPresentBlockedEvent`/`initDepthTexture` equivalent for the MVP. If the DX11 path needs any eager init, place it beside lines 371-372 with the same CON-H-01 ("running in utinni_init, NOT DllMain") / CON-N-01 ("NOT a Detour::Create") rationale comments. **Likeliest outcome: utinni.cpp needs no change** — the detection+install is self-contained in the hook tier + `setup()`. Planner should confirm whether the DX11 poll needs a kick-off hook registered anywhere; if the producer's gl11 path goes through the same `swg::graphics::install` seam, the `graphics.cpp::hkInstall` call site (611) is where a `directX11::` kick-off would mirror `directX::detour()`.

---

### MODIFIED `vcpkg.json` (repo root, D:/Code/Utinni/vcpkg.json)

**Analog:** the imgui `features` array (lines 19-26)

**Wave 0 / first task** (RESEARCH Summary + Pitfall 1). Add `"dx11-binding"` to the imgui features array:
```json
"features": [
  "docking-experimental",
  "dx9-binding",
  "dx11-binding",
  "win32-binding"
],
```
Triggers a manifest-mode reinstall on next MSBuild (x86-windows triplet). AGENTS.md's claim that dx11-binding is already present is OUTDATED — it is NOT in the manifest (verified). The self-hosted runner has flaked vcpkg-install before (`[[project_ci_debug_gitignore_trap]]`); expect a possible re-run.

---

### NEW `UtinniCore.Tests/Graphics/Dx11VtblOffsetTests.cpp` (D-21 L1)

**Analog:** the `D3DInformation` vtable-index enum (`directx9.cpp:112-234`) + `RenderBackendSeamTests.cpp` test skeleton

Pin Present=8, ResizeBuffers=13 so a silent DXGI ABI drift fails the build. Copy the Catch2 file header + `TEST_CASE(... "[dxgi][offsets]")` shape from `RenderBackendSeamTests.cpp` (24-68). Static-assert or REQUIRE against the named constants the hook tier uses (`directX11::dxgi_Present_Index == 8`, `dxgi_ResizeBuffers_Index == 13`). No device needed.

### NEW `UtinniCore.Tests/Graphics/Dx11DummyDeviceHarvestTests.cpp` (D-21 L3)

**Analog:** `directx9.cpp::getVtbl()` throwaway-device harvest (460-551) — this test IS that harvest, ported to DXGI/WARP

Mirror `getVtbl()`'s structure: dynamic-create a device+swapchain (`D3D11CreateDeviceAndSwapChain` with `D3D_DRIVER_TYPE_WARP`), snapshot the swapchain vtable, assert idx 8/13 resolve, then Release and assert no leak (RESEARCH A1: skip-with-log if WARP unavailable — make device-create failure a SKIP, not a FAIL). Use a hidden 1x1 window like `getVtbl():495-496` (Claude's Discretion: message-only vs 1x1 hidden). Tag `[dxgi][harvest]`. This is the offline validation of the offset/leak invariants even though production uses the advertised contract (D-10).

### NEW `UtinniCore.Tests/Graphics/Dx11DetectionTests.cpp` (D-21 L4)

**Analog:** `RenderBackendSeamTests.cpp` (mock + REQUIRE structure, 41-103)

Unit-test the `gl%02d_r.dll` detection + fallback decision (default-D9 / positive-D11 / ambiguous→D9+log) with injected module-presence states. The detection logic should be factored into a testable pure function (taking module-presence booleans) so the test injects states without real DLLs. Tag `[rndr03][detect]`.

### EXTENDED `UtinniCore.Tests/Graphics/ImguiApiNeutralityTests.cpp` (extend D-06 gate)

**Analog:** the `dx9Tokens()` set in the same file (60-88)

Add DX11/DXGI symbol forms to the banned token list (Pitfall 5). Keep gating on CONCRETE symbol forms, NOT the bare strings "D3D11"/"DXGI" (comments mention them — grep-gate hygiene, the same discipline as the existing `directX::` / `IDirect3DDevice9` entries). Add:
```cpp
"#include <d3d11.h>", "#include \"d3d11.h\"",
"#include <dxgi1_2.h>", "#include <dxgi.h>",
"#include <imgui_impl_dx11.h>", "imgui_impl_dx11",
"directx11.h", "directX11::",
"ID3D11Device", "ID3D11DeviceContext", "ID3D11RenderTargetView", "ID3D11ShaderResourceView",
"IDXGISwapChain",
```
The existing stripper-hygiene self-check (93-127) and the two file SECTIONs (129-147) already iterate `dx9Tokens()` — they pick up the new entries automatically. Rename the set or add a second set per planner taste, but keep the comment-stripping self-check covering at least one new token.

### EXTENDED `UtinniCore.Tests/Graphics/RenderBackendSeamTests.cpp` (D-21 L2)

**Analog:** the `MockBackend` + `TEST_CASE` in the same file (41-103)

Add a Dx11-flavored mock-dispatch case. CRITICAL (RESEARCH Wave 0 note + the vcxproj Option-A comment at lines 141-146): do NOT instantiate the concrete `Dx11Backend` — it drags in `<imgui_impl_dx11.h>` + DetourXS + directx11 (LNK2019 cascade, the same reason `render_backend_dx9.cpp` is NOT compiled into the test exe). Reuse the device-free `MockBackend` against the `IRenderBackend` ABC, asserting the 10-vtable contract routes identically. The existing test (68-103) is the exact template — a sibling `TEST_CASE` tagged `[rndr02][graphics]` covering the same 10 members is sufficient (or simply assert the existing dispatch test also covers the Dx11 path since the ABC is shared).

---

## Shared Patterns

### CppSharp parse-stage exclusion (binding-generator hygiene)
**Source:** `UtinniCoreDotNetGen/HeaderDiscovery.cs:86-100`
**Apply to:** `render_backend.h` (already excluded) + evaluate `directx11.h`
The pinned clang-11 CppSharp parser faults (AccessViolation) on `<imgui.h>` and may fault on `<d3d11.h>`/`<dxgi1_2.h>`. `render_backend.h` is ALREADY excluded at discovery (97-100) because it pulls `<imgui.h>` and projects zero managed surface. The new `render_backend_dx11.cpp`/`directx11.cpp` are `.cpp` (not discovered as headers — only `.h` files are scanned). For `directx11.h`: it includes `<dxgi1_2.h>`. The existing `directx9.h` includes `<d3d9.h>` and is NOT excluded (it parses + projects its `UTINNI_API` exports). MIRROR that: if `directx11.h` exports NO `UTINNI_API` symbols (likely — the hook tier needs no managed binding), and if the DXGI headers parse cleanly, leave it discovered. If it faults the parser like `<imgui.h>` did, add a `directx11.h` exclusion beside the `render_backend.h` one (96-100). The planner should treat this as a build-validation checkpoint, not a guess — run `UtinniCoreDotNetGen.exe` and check for the parse fault (`[[project_utinnicore_cs_regen_churn]]`: always `git checkout -- Generated/UtinniCore.cs` after, never commit it).

### Borrowed-pointer discipline (never Release the advertised contract)
**Source:** spec §4.1 + RESEARCH Anti-Patterns
**Apply to:** `directx11.cpp`, `render_backend_dx11.cpp`
The advertised `swapChain`/`device`/`context` are BORROWED — never `AddRef`/`Release` them. Only RTVs Utinni CREATES are released (in `onPreResize`). The D3D9 analog already follows this: `directx9.cpp` never Releases the captured `pDirectXDevice` (`getDevice()` just returns it, 687-690).

### Heap-free per-frame hot path
**Source:** `[[project_rh_snapshot_no_heap_alloc]]` + `render_backend.cpp:38-40` comment
**Apply to:** `Dx11Backend::newFrame()` (RTV rebind), `directx11.cpp::hkSwapChainPresent`
Cache the RTV; recreate only on resize. No per-frame allocation in the dispatch or rebind path. The seam's `s_active` pointer is static storage (no heap) — mirror with a static-storage `Dx11Backend` singleton (`render_backend_dx9.cpp:150`).

### One-shot first-fire diagnostic logging
**Source:** `directx9.cpp:279-322` (hkPresent), `:236-248` (hkBeginScene), imgui_impl.cpp:371-376 (setup)
**Apply to:** `directx11.cpp` hook bodies + the detection branch in `setup()`
Static-bool-guarded one-shot `utinni::log::info`/`debug` on first fire. The RNDR-03 "one-shot diagnostic log" requirement is satisfied by this exact pattern (content/level/destination = Claude's Discretion per D-21).

### Build registration (vcxproj + filters)
**Source:** `UtinniCore/UtinniCore.vcxproj:210-292` (directx9.cpp/render_backend*.cpp `<ClCompile>` + directx9.h/render_backend.h `<ClInclude>`); `UtinniCore.Tests/UtinniCore.Tests.vcxproj:128-146`
**Apply to:** all new source + test files
Register `directx11.cpp`, `render_backend_dx11.cpp` as `<ClCompile>` and `directx11.h` as `<ClInclude>` in UtinniCore.vcxproj (beside lines 210-212/291-292). Register the 4 new test `.cpp` in UtinniCore.Tests.vcxproj beside lines 135-146. The test exe AdditionalIncludeDirectories (UtinniCore.Tests.vcxproj:73) already covers `vcpkg_installed\x86-windows\include` and `$(SolutionDir)UtinniCore` — the new DXGI test files include `<d3d11.h>`/`<dxgi1_2.h>` from the OS SDK (on the default include path; confirm no extra include dir needed — RESEARCH Wave 0 last bullet). The Option-A split (UtinniCore.Tests.vcxproj:141-146) means render_backend_dx11.cpp is NOT compiled into the test exe (it would drag in directx11/DetourXS/imgui_impl_dx11) — same rationale as render_backend_dx9.cpp's exclusion.

---

## No Analog Found

| File / Piece | Role | Data Flow | Reason |
|--------------|------|-----------|--------|
| `directx11.cpp::hkResizeBuffers` body | hook | event-driven | NO DXGI-resize analog in the codebase — D3D9 NEVER Resets (the no-Reset rule, `[[feedback_d3d9_reset_third_party]]`). The DX11 release/recreate discipline is NET-NEW. Reference impl is the PRODUCER's `displayModeChanged` (`swg-client-v2` Direct3d11_Device.cpp:1199-1216), not Utinni. Build it from RESEARCH Pattern 2. |
| `directx11.cpp::tryInstall` (advertised-contract consumer) | hook (acquisition) | request-response | The D3D9 path uses a throwaway-device harvest (`getVtbl()`); the DX11 production path REPLACES it with `GetProcAddress("GetHookPoints")` + poll (D-10). The `GetModuleHandleA`/`GetProcAddress` mechanics mirror `getVtbl():471-485`, but the poll-the-live-swapchain flow is net-new (RESEARCH Pattern 3 + spec §3). |
| `Dx11Backend::newFrame` RTV rebind | seam impl | transform | `Dx9Backend::newFrame` is a bare `ImGui_ImplDX9_NewFrame()` forward — the flip-discard RTV rebind has no D3D9 analog (D3D9 has no flip-discard unbind). Net-new from RESEARCH Pattern 4 + producer Direct3d11_Device.cpp:922-923. |

These pieces have a VERIFIED external reference (the producer's own DX11 device code in `swg-client-v2 @ 056632a`) even though Utinni has no internal analog — the planner should cite the producer anchors (RESEARCH Sources / spec §8) in the plan actions.

---

## Metadata

**Analog search scope:** `UtinniCore/swg/graphics/` (render_backend*, directx9), `UtinniCore/swg/ui/imgui_impl.cpp`, `UtinniCore/utinni.cpp`, `UtinniCore/swg/graphics/graphics.cpp`, `UtinniCore.Tests/Graphics/` (RenderBackendSeamTests, ImguiApiNeutralityTests, NoDeviceResetTests), `UtinniCore/UtinniCore.vcxproj`, `UtinniCore.Tests/UtinniCore.Tests.vcxproj`, `UtinniCoreDotNetGen/HeaderDiscovery.cs`, repo-root `vcpkg.json`.
**Files scanned:** 13
**Pattern extraction date:** 2026-06-15
