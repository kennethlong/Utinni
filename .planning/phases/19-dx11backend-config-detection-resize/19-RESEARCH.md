# Phase 19: Dx11Backend + Config Detection + Resize - Research

**Researched:** 2026-06-15
**Domain:** D3D11/DXGI vtable hooking · ImGui DX11 backend · renderer-DLL detection · flip-model RTV management · injected-overlay seam
**Confidence:** HIGH (consumer + producer code read directly; ImGui DX11 backend API is stable & verified against vcpkg port; one MEDIUM item — the `WM_SIZE` resize-trigger reality, honestly bounded below)

<user_constraints>
## User Constraints (from 19-CONTEXT.md)

### Locked Decisions
- **D-09/D-10:** Client *advertises* its render hook points; Utinni consumes them. The blind throwaway-`D3D11CreateDeviceAndSwapChain` harvest is **dropped for production** (REPLACE, not augment) and survives ONLY as the offline CI test (D-21 layer 3). This is a deliberate, user-approved deviation from ROADMAP success-criterion-1's stated acquisition method. The *outcome* (hook Present idx 8 + ResizeBuffers idx 13, per-frame RTV rebind) is unchanged.
- **D-11:** Contract = **Candidate A** `GetHookPoints()` `extern "C" __cdecl` export on `gl11_r.dll`, returning `{IDXGISwapChain1*, ID3D11Device*, ID3D11DeviceContext*}`. Utinni `GetProcAddress`es it, polls `swapChain != null` once/frame, then reads vtbl idx 8/13 off the live swapchain and detours. Push-model (Candidate D) is the documented fallback.
- **D-12/D-13/D-14:** Phase 19 (Utinni) delivers the **consumer** + the written instrumentation spec (already complete — `19-INSTRUMENTATION-SPEC.md`). The SWG-Source client instrumentation is a clean handoff to a separate session. The maintainer live-smoke gate is sequenced AFTER the handoff instrumentation lands.
- **D-15:** Hard **one-backend-per-session**. One `GetModuleHandle` check at install selects exactly one backend; no mid-session switch.
- **D-16:** Detection keys on the `gl%02d_r.dll` family. Confirm the number→API mapping against the running client (done — see Architectural Responsibility Map + Detection section).
- **D-17:** **Default D3D9**, install Dx11Backend only on positive D3D11 detect. Ambiguous/neither → D3D9 + one-shot diagnostic log.
- **D-18:** True DXGI `ResizeBuffers` (release RTV → ResizeBuffers → recreate RTV). The D3D9 never-Reset/stretch rule is NOT carried verbatim.
- **D-18b:** The D3D11 client today calls `ResizeBuffers` only from `displayModeChanged()` on `WM_DISPLAYCHANGE`, NOT on `WM_SIZE`. Hook `ResizeBuffers` regardless (covers what fires); the `WM_SIZE` improvement defers to RESID-04. Does NOT block the contract.
- **D-19:** Keep RT-space input mapping under D3D11 (reuse `imgui_impl.cpp` ~497–512 RT-space block).
- **D-20:** A runnable 32-bit D3D11 SWG-Source build exists today; renderer DLL observable, vtbl offsets confirmable, live-smoke possible once instrumented.
- **D-21:** Four automated harness layers (DXGI vtbl-offset asserts; mock Dx11Backend dispatch test; offline dummy-device WARP harvest + leak test; detection-logic unit test).
- **D-22:** Maintainer live-smoke is the final RNDR-02/03/04 acceptance gate.

### Claude's Discretion
- Dummy-swapchain HWND choice for the offline harvest test (message-only window vs 1×1 hidden window).
- One-shot diagnostic log content/level/destination.
- The exact `Dx11Backend` internal split (RTV management / device+context ownership / detour install), provided D-15..D-19 hold.

### Deferred Ideas (OUT OF SCOPE)
- Full entry-point advertisement mechanism + x64 (Backlog 999.7). x64 is user-locked OUT of v2.1.
- Runtime-switchable backends (rejected — D-15).
- `swg-window-resize-fullscreen-edge-cases` todo (RESID-04 residual, D3D9 window-management) — deliberately NOT folded.
- The optional client-side `WM_SIZE→ResizeBuffers` improvement (instrumentation spec §6 Option 1; default = defer).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| RNDR-02 | Overlay renders + maps input (RT-space) under D3D11; `Dx11Backend` hooks the DXGI swapchain `Present`. | Standard Stack (ImGui DX11 backend) + Pattern 1 (Present hook body) + Pattern 4 (flip-model RTV rebind) + the existing RT-space block (`imgui_impl.cpp` 497–512) routes through unchanged. |
| RNDR-03 | Exactly one backend installs per session, auto-detected from `gl%02d_r.dll`, one-shot diagnostic log; no doubled input / dual contexts. | Detection section (ground-truthed mapping: gl11=D3D11, gl05/06/07=D3D9) + Pattern 3 (consumer flow) + D-21 layer 4 unit test. |
| RNDR-04 | Overlay survives a window resize under D3D11 — RTV released/recreated inside the `ResizeBuffers` hook (vtbl idx 13); no `DXGI_ERROR_INVALID_CALL`. | Pattern 2 (ResizeBuffers hook body: release RTV → call original → recreate) + Pitfall 2 (release-before-resize) + the honest `WM_SIZE` bound in Open Questions. |
</phase_requirements>

## Summary

Phase 19 adds a `Dx11Backend final : public IRenderBackend` behind the Phase 18 seam, plus a `directx11.cpp` DXGI hook tier mirroring `directx9.cpp`, plus renderer-DLL detection in `imgui_impl::setup()`. Almost all of the hard design ambiguity is already locked by D-09..D-22 and the resolved `19-INSTRUMENTATION-SPEC.md`; the producer side (`gl11_r.dll`) has **already implemented** the `GetHookPoints()` export (verified at `Direct3d11.cpp:879-888` in the pinned `swg-client-v2` @ `056632a`). So this phase is mostly a careful translation job with four well-bounded technical areas: (1) the ImGui DX11 init/per-frame/resize/shutdown call sequence and how the 10 vtable overrides map onto it, (2) detouring vtbl idx 8/13 of the *advertised live* `IDXGISwapChain1` via DetourXS, (3) the consumer poll-then-install flow slotting into the existing `hkPresent`/`setup()`/`createDetours()` ordering, and (4) the four-layer Catch2 harness.

The single biggest **planner-actionable gap**: the vcpkg manifest (`vcpkg.json`) lists imgui features `docking-experimental`, `dx9-binding`, `win32-binding` — **`dx11-binding` is NOT present** (contradicting AGENTS.md's claim). It must be added, or `imgui_impl_dx11.h`/`.cpp` won't be available. `dx11-binding` is a confirmed valid feature of the vcpkg imgui port. This is a one-line manifest change that triggers a vcpkg re-install — sequence it as the very first task.

The flip-model reality is the subtle correctness item: the producer creates a `DXGI_SWAP_EFFECT_FLIP_DISCARD` swapchain (`Direct3d11_Device.cpp:570`), which **unbinds the backbuffer RTV after every Present**. The producer rebinds its own RTV each frame (`applyPreDrawState`, line 922-923). Utinni's overlay therefore must bind a backbuffer RTV itself inside `hkPresent` *before* `ImGui_ImplDX11_RenderDrawData` — it cannot assume any RTV is bound at Present-hook time. This is the DX11 analog of the D3D9 "operate in render-target space" discipline and is verified by both the producer source and Microsoft DXGI docs.

**Primary recommendation:** First task = add `dx11-binding` to vcpkg.json + reinstall. Then build `directx11.{cpp,h}` (DXGI tier, mirrors directx9.cpp) + `render_backend_dx11.cpp` (the seam twin, the ONLY new TU allowed to include `<d3d11.h>`/`<dxgi1_2.h>`/`<imgui_impl_dx11.h>`), wire detection into `setup()`, and clone the four Phase-18 harness patterns. Keep `imgui_impl.cpp` byte-for-byte API-neutral (the D-06 grep-gate is non-negotiable). Treat success-criterion-3 as "survives `WM_DISPLAYCHANGE`" for the live-smoke; `WM_SIZE` drag-resize is honestly out of reach for the embed case unless the client takes spec §6 Option 1.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| DXGI Present/ResizeBuffers detour install | `directx11.cpp` (new DXGI hook tier) | DetourXS | Mirrors `directx9.cpp`'s `directX::` namespace; the only place that names DXGI/D3D11 *hook* types. |
| ImGui DX11 renderer binding (Init/NewFrame/RenderDrawData/Shutdown) | `render_backend_dx11.cpp` (seam twin) | imgui `dx11-binding` | The seam twin is the ONLY new TU that includes `<imgui_impl_dx11.h>` (D-05 purge holds). |
| Per-frame backbuffer-RTV bind + resize RTV recreate | `Dx11Backend` (newFrame / onPreResize / onPostResize) | `directx11.cpp` (holds live swapchain/device/context) | Flip-discard unbinds RTV each Present; the seam owns the rebind so `imgui_impl` stays neutral. |
| Renderer-DLL detection + backend selection | `imgui_impl::setup(HWND)` (~283 install seam) | `GetModuleHandleA` | Selection happens once at install, exactly where Dx9 is selected today (`render_backend::set(...)`). |
| Hook-point acquisition (advertised contract consumer) | `directx11.cpp` (GetProcAddress + poll) | `gl11_r.dll::GetHookPoints` (producer, already shipped) | Utinni reads the live swapchain off the advertised struct; no throwaway device in production. |
| RT-space input mapping | `imgui_impl.cpp` (~497–512, unchanged) | seam `renderTargetWidth/Height` | Stays API-neutral; behaviorally identical to D3D9 path (D-19). |
| API-neutral overlay logic (WndProc, gizmo, renderCallbacks, depth/color windows) | `imgui_impl.cpp` (unchanged) | seam | D-05 purge: zero D3D11/DXGI symbols may appear here. |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dear ImGui | 1.92.6+ (vcpkg, x86-windows) | Overlay UI + the `imgui_impl_dx11.{h,cpp}` backend binding | Already the project's overlay lib; the DX11 backend ships in the same port. `[VERIFIED: vcpkg.json + vcpkg imgui port]` |
| DetourXS (vendored) | 1.0 (`external/DetourXS`) | Detour vtbl idx 8/13 of the live swapchain | Already used for all 7 D3D9 vtable detours; `DETOUR_TYPE_PUSH_RET`, prefer `DETOUR_LEN_AUTO`. `[VERIFIED: external/DetourXS/detourxs.h]` |
| Windows SDK `<d3d11.h>` / `<dxgi1_2.h>` | OS-provided | `ID3D11Device/Context`, `IDXGISwapChain1`, `ResizeBuffers` signatures | `IDXGISwapChain1` lives in `<dxgi1_2.h>`; the producer returns it. No new dependency, no .lib needed for the hook (we patch function bodies, not link). `[VERIFIED: producer source uses these]` |
| `<wrl/client.h>` `ComPtr` | OS-provided | Optional RAII for Utinni-created RTV (NOT for borrowed device/swapchain) | The producer uses ComPtr throughout. Utinni's *borrowed* pointers are raw (never AddRef/Release per spec §4.1); only a Utinni-*created* RTV would be ComPtr-managed. `[CITED: 19-INSTRUMENTATION-SPEC.md §4]` |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Catch2 | 3.15.0+ (vcpkg) | The four D-21 harness layers | Existing native test framework (`UtinniCore.Tests`). `[VERIFIED: vcpkg.json]` |
| D3D11 WARP (`D3D_DRIVER_TYPE_WARP`) | OS-provided | Offline dummy-device harvest test (D-21 layer 3) on the self-hosted CI runner with no GPU/SWG | WARP is a software rasterizer present on all modern Windows; vtbl offsets are identical to HAL. `[ASSUMED — see A1]` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Advertised `GetHookPoints()` (D-09/D-11, locked) | Blind throwaway `D3D11CreateDeviceAndSwapChain` harvest | Locked OUT of production (D-10); kept only as the offline test. Throwaway yields a *different* swapchain object (wrapped vtable risk) and costs a device create. |
| DetourXS on the live vtbl | MinHook / Microsoft Detours | DetourXS is already vendored and used for all D3D9 hooks; introducing a second hook lib is needless churn. |
| ComPtr for borrowed pointers | raw pointers (chosen) | Spec §4.1 forbids ownership ops on borrowed device/swapchain/context — raw avoids accidental AddRef/Release. |

**Installation:**
```bash
# vcpkg.json change (NOT yet present — see Summary): add "dx11-binding" to the imgui features array.
# Triggers a manifest-mode reinstall on next MSBuild (x86-windows triplet).
```

**Version verification:** imgui `>=1.92.6` is pinned in `vcpkg.json` `[VERIFIED]`. The `dx11-binding` feature is confirmed available in the vcpkg imgui port (alongside `dx9-binding`/`win32-binding`) `[VERIFIED: vcpkg imgui port docs]`. The ImGui DX11 backend public API (`ImGui_ImplDX11_Init/NewFrame/RenderDrawData/Shutdown/InvalidateDeviceObjects/CreateDeviceObjects`) has been stable since imgui 1.80 and is unchanged through 1.92.x `[ASSUMED — A2; stable across the range, but Context7/header confirm recommended at plan time]`.

## Package Legitimacy Audit

> No new external/registry packages are installed. The only dependency change is enabling an existing-port *feature* (`dx11-binding`) of the already-vendored, already-pinned vcpkg `imgui` package. All other libraries (DetourXS, Windows SDK D3D11/DXGI) are vendored or OS-provided.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| imgui (`dx11-binding` feature) | vcpkg | mature | n/a | github.com/ocornut/imgui | N/A (already pinned) | Approved — feature toggle only |
| DetourXS | vendored | n/a | n/a | external/DetourXS (in-repo) | N/A | Approved — already in use |

**Packages removed due to slopcheck [SLOP] verdict:** none.
**Packages flagged as suspicious [SUS]:** none.

## Architecture Patterns

### System Architecture Diagram

```
                    utinni_init (launcher remote thread, NOT DllMain)
                            |
        initPresentBlockedEvent() ──┐  (CR-04, eager)
        initDepthTexture()        ──┤  (WR-03, eager)   [D3D9 path keeps these]
                            |       │
                       createDetours()
                            |
        ┌───────────────────┴────────────────────────────────────┐
        │  D3D9 path (existing, default)   │  D3D11 path (NEW, Phase 19) │
        │  directX::detour()               │  directx11::poll/install    │
        │  throwaway-device vtbl harvest   │  (no throwaway in prod)     │
        └──────────────────────────────────┴─────────────────────────────┘

  SELECTION (one-shot, at install — imgui_impl::setup ~283):
        GetModuleHandleA("gl11_r.dll") non-null?
            yes ──► resolve GetProcAddress(hGl11,"GetHookPoints")
                       absent ──► log + bail (no overlay)  [spec §7.2]
                       present ──► render_backend::set(dx11Singleton())
            no  ──► default render_backend::set(dx9Singleton())   [D-17]
        (ambiguous/neither ──► D3D9 + one-shot diagnostic log)

  D3D11 PER-FRAME (once swapChain advertised non-null):
        gl11_r.dll Present(1,0)  ──detour idx 8──►  hkSwapChainPresent
                                                       │
                       imgui_impl::render()  ◄─────────┤ (newFrame via seam)
                       Dx11Backend::newFrame():
                           GetBuffer(0)+CreateRTV+OMSetRenderTargets  ← flip-discard rebind
                           ImGui_ImplDX11_NewFrame()
                       ... build UI (API-neutral) ...
                       Dx11Backend::renderDrawData():
                           ImGui_ImplDX11_RenderDrawData(drawData)
                                                       │
                       call original Present  ─────────┘

  D3D11 RESIZE (WM_DISPLAYCHANGE today):
        gl11_r.dll displayModeChanged() ─► ResizeBuffers(...) ──detour idx 13──► hkResizeBuffers
            Dx11Backend::onPreResize():  release Utinni RTV  (BEFORE original runs)
            call original ResizeBuffers
            Dx11Backend::onPostResize(): recreate Utinni RTV from new backbuffer
```

### Recommended Project Structure
```
UtinniCore/swg/graphics/
├── render_backend.h          # EXISTING — add Dx11Backend decl + dx11Singleton() (NO d3d11 types; forward-declare only)
├── render_backend.cpp        # EXISTING — unchanged (DX-free get/set; compiled into the test exe directly)
├── render_backend_dx9.cpp    # EXISTING — unchanged (DX9-bearing half)
├── render_backend_dx11.cpp   # NEW — the ONLY new TU including <d3d11.h>/<imgui_impl_dx11.h>; Dx11Backend impls + dx11Singleton()
├── directx11.cpp             # NEW — DXGI hook tier (mirrors directx9.cpp): hkSwapChainPresent, hkResizeBuffers, GetHookPoints consumer, detour install
├── directx11.h               # NEW — directX11:: free-function decls (init/detour/getSwapChain/getDevice/getContext)
└── directx9.{cpp,h}          # EXISTING — unchanged
UtinniCore.Tests/Graphics/
├── RenderBackendSeamTests.cpp     # EXISTING — clone the MockBackend dispatch test for Dx11 (D-21 layer 2)
├── Dx11VtblOffsetTests.cpp        # NEW — pin Present=8 / ResizeBuffers=13 (D-21 layer 1)
├── Dx11DummyDeviceHarvestTests.cpp# NEW — WARP harvest + no-leak (D-21 layer 3)
├── Dx11DetectionTests.cpp         # NEW — gl%02d_r.dll detection + fallback logic (D-21 layer 4)
└── ImguiApiNeutralityTests.cpp    # EXISTING — extend token set to ALSO ban d3d11/dxgi symbols in imgui_impl
```

### Pattern 1: DXGI Present hook (mirrors `directx9.cpp::hkPresent`)
**What:** Detour `IDXGISwapChain::Present` (vtbl idx 8). Body drives `imgui_impl::render()` (which calls the seam's `newFrame`/`renderDrawData`), then calls the original Present.
**When to use:** Once the advertised swapchain is non-null and detours are installed.
**Trampoline typedef:**
```cpp
// Source: producer Present is ms_swapChain->Present(1,0) — base IDXGISwapChain::Present(UINT,UINT)
// (Direct3d11_Device.cpp:1138; spec §4.4 — base interface, NOT Present1).
using pSwapChainPresent = HRESULT(__stdcall*)(IDXGISwapChain* pSwapChain, UINT SyncInterval, UINT Flags);
using pResizeBuffers    = HRESULT(__stdcall*)(IDXGISwapChain* pSwapChain, UINT BufferCount, UINT Width, UINT Height, DXGI_FORMAT NewFormat, UINT SwapChainFlags);
```
**Key:** the first-fire diagnostic + present-block pattern from `directx9.cpp:266-388` translates directly; the depth-texture reach-in does NOT (DX11 has no `directX::getDepthTexture()` equivalent — `Dx11Backend::sceneDepthTexture()` returns `(ImTextureID)0` for the MVP unless a DX11 depth SRV is wired, which is OUT of phase scope; the depth/color windows are dev-only behind `!enableUi`).

### Pattern 2: DXGI ResizeBuffers hook (the DX11 resize discipline — D-18/RNDR-04)
**What:** Detour `ResizeBuffers` (vtbl idx 13). Release Utinni's RTV **before** the original runs (DXGI requires all backbuffer references released or `ResizeBuffers` returns `DXGI_ERROR_INVALID_CALL`), call original, then recreate Utinni's RTV.
**Example:**
```cpp
// Source: producer displayModeChanged (Direct3d11_Device.cpp:1199-1216) does exactly this
// for ITS OWN RTV: Reset RTV/DSV -> ResizeBuffers(0,w,h,DXGI_FORMAT_UNKNOWN,0) -> recreate.
HRESULT __stdcall hkResizeBuffers(IDXGISwapChain* sc, UINT bc, UINT w, UINT h, DXGI_FORMAT fmt, UINT flags) {
    render_backend::get()->onPreResize();   // Dx11Backend: release ONLY Utinni's own RTV
    HRESULT hr = origResizeBuffers(sc, bc, w, h, fmt, flags);
    render_backend::get()->onPostResize();   // Dx11Backend: GetBuffer(0)+CreateRenderTargetView from resized backbuffer
    return hr;
}
```
**Critical:** Utinni must release only RTVs **it created**. The producer releases its own RTV in `displayModeChanged` before calling `ResizeBuffers`. Both the producer's RTV and Utinni's RTV must be released for `ResizeBuffers` to succeed. Since the producer's `displayModeChanged` *calls* `ResizeBuffers` (which Utinni has detoured), the ordering is: producer releases its RTV → producer calls `ResizeBuffers` → **Utinni's hook fires** → `onPreResize` releases Utinni's RTV → original `ResizeBuffers` → `onPostResize` recreates Utinni's RTV. Verify the producer's RTV is already released by the time the detour fires (it is — lines 1200-1202 precede the `ResizeBuffers` call at 1205).

### Pattern 3: Advertised-contract consumer flow (D-09/D-11)
**What:** Resolve and poll the producer's `GetHookPoints()`, install once.
**Example:**
```cpp
// At install (imgui_impl::setup or a directx11::tryInstall called from hk... poll):
HMODULE hGl11 = GetModuleHandleA("gl11_r.dll");   // also try "gl11_d.dll" for debug client (Graphics.cpp:199)
if (!hGl11) { /* not the D3D11 client → leave Dx9 default (D-17) */ }
struct UtinniDx11HookPoints { IDXGISwapChain1* swapChain; ID3D11Device* device; ID3D11DeviceContext* context; };
using pGetHookPoints = UtinniDx11HookPoints(__cdecl*)();
auto getHP = (pGetHookPoints)GetProcAddress(hGl11, "GetHookPoints");
if (!getHP) { utinni::log::warning("gl11_r.dll lacks GetHookPoints; no D3D11 overlay"); return; } // spec §7.2 graceful bail
UtinniDx11HookPoints hp = getHP();
if (hp.swapChain == nullptr) return;   // not ready yet — poll again next frame (spec §3.3)
// swapChain live → read vtbl idx 8/13 off the LIVE object, detour, ImGui_ImplDX11_Init(hp.device, hp.context),
// derive HWND via hp.swapChain->GetDesc1()/GetHwnd() (spec table row "Focus HWND").
```
**Note:** the producer's struct is `IDXGISwapChain1*` (`Direct3d11.cpp:872-877`). Utinni's local mirror must match field order/type exactly (return-by-value POD ABI).

### Pattern 4: Flip-model per-frame RTV rebind (RNDR-02 correctness)
**What:** The producer's swapchain is `DXGI_SWAP_EFFECT_FLIP_DISCARD` (`Direct3d11_Device.cpp:570`). After every Present, DXGI **unbinds the backbuffer RTV** from the output-merger. Utinni's overlay draws inside `hkSwapChainPresent` *before* the original Present — so at that point the producer has already bound its own RTV for the frame (line 922-923) and the overlay can draw onto it. To be robust, `Dx11Backend::newFrame()` should `GetBuffer(0)` + `CreateRenderTargetView` (cache it; recreate only on resize) + `OMSetRenderTargets` so the overlay is never drawn against an unbound or stale RT.
**Source:** Microsoft DXGI docs — "DXGI unbinds the back buffer from all pipeline state locations ... call `OMSetRenderTargets` immediately before you render to the back buffer" `[CITED: learn.microsoft.com IDXGISwapChain::Present]`. Producer confirms by rebinding every frame.

### Anti-Patterns to Avoid
- **Calling `swapChain->Release()` / `device->Release()` on the borrowed pointers** — spec §4.1 forbids it; they are borrowed. Only release RTVs Utinni created.
- **Recreating the swapchain** — spec §4.2: the swapchain object is stable for the session; Utinni's detours assume identity stability. Never tear it down.
- **Carrying the D3D9 never-Reset rule verbatim into DX11** — `[VERIFIED: lessons.md:13-14]` the D3D11 analog is release/recreate the RTV inside `ResizeBuffers`, never tear down the swapchain. `feedback_d3d9_reset_third_party` is D3D9-specific.
- **Detouring `Present1`** — the producer uses base `Present(1,0)` (spec §4.4). idx 8 is correct; `Present1` is a different slot.
- **Heap allocation on the per-frame RTV rebind / seam dispatch** — `project_rh_snapshot_no_heap_alloc`. Cache the RTV; recreate only on resize.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| D3D11 imgui rendering | A custom vertex/index/shader submit path | `ImGui_ImplDX11_*` (vcpkg `dx11-binding`) | Handles font atlas, dynamic VB/IB growth, state backup/restore — fiddly and error-prone. |
| Hook-point acquisition | Throwaway-device vtbl harvest in production | Advertised `GetHookPoints()` (D-10) | Locked; the live swapchain is the real hook target, no wrapped-vtable risk. |
| Function detouring | Hand-rolled JMP patch | DetourXS `Detour::Create(..., DETOUR_TYPE_PUSH_RET)` | Already vendored, handles instruction-length decode (ADE32); `DETOUR_LEN_AUTO`. |
| RTV release/recreate on resize | A bespoke swapchain teardown | Release RTV → original `ResizeBuffers` → recreate RTV | The producer's `displayModeChanged` is the exact reference pattern. |

**Key insight:** Every hard piece here already has a verified reference implementation — the producer's own DX11 device code (RTV lifecycle, swapchain desc, present) and Utinni's own D3D9 hook tier (detour install, first-fire diag, present-block, stash-device hand-off). This phase is translation, not invention.

## Common Pitfalls

### Pitfall 1: `dx11-binding` missing from vcpkg.json
**What goes wrong:** `#include <imgui_impl_dx11.h>` fails; `ImGui_ImplDX11_*` unresolved.
**Why it happens:** AGENTS.md claims the dx11-binding feature is "in the manifest" but `vcpkg.json` currently lists only `docking-experimental`, `dx9-binding`, `win32-binding`.
**How to avoid:** First task — add `"dx11-binding"` to the imgui features array; reinstall (manifest mode). The self-hosted runner has flaked vcpkg-install before (`project_ci_debug_gitignore_trap` note) — expect a possible re-run.
**Warning signs:** missing `imgui_impl_dx11.h` under `vcpkg_installed/x86-windows/include`.

### Pitfall 2: `DXGI_ERROR_INVALID_CALL` from ResizeBuffers (RNDR-04 explicit anti-target)
**What goes wrong:** `ResizeBuffers` returns `DXGI_ERROR_INVALID_CALL` because an outstanding backbuffer reference (Utinni's RTV) was not released.
**Why it happens:** DXGI requires ALL references to the swapchain's backbuffers released before `ResizeBuffers`.
**How to avoid:** Release Utinni's RTV in `onPreResize` *before* the original `ResizeBuffers` runs (Pattern 2). The producer already releases its own RTV (line 1200-1202).
**Warning signs:** the requirement names this exact error — it is the RNDR-04 gate.

### Pitfall 3: Doubled input / dual ImGui contexts (RNDR-03 anti-target)
**What goes wrong:** Both Dx9Backend and Dx11Backend install, or two ImGui contexts get created → doubled input, double-drawn UI.
**Why it happens:** Detection picks both, or the install seam runs twice.
**How to avoid:** Hard one-backend-per-session (D-15): a single `GetModuleHandle` check selects exactly one; `imgui_impl::setup` already latches `isSetup` so `ImGui::CreateContext()` runs once. The detection-logic unit test (D-21 layer 4) covers default-D9 / positive-D11 / ambiguous→D9+log.
**Warning signs:** two log lines for backend install; UI drawn twice.

### Pitfall 4: The `.Reset(` grep-gate false-trip (project hygiene)
**What goes wrong:** `render_backend_dx11.cpp` uses `ComPtr::Reset()` (legit DX11 RAII) which textually matches the `NoDeviceResetTests.cpp` `.Reset(` device-Reset gate.
**Why it happens:** That gate counts `->Reset(`/`.Reset(` in a *fixed file list* (directx9.cpp, direct_input.cpp, PanelGame.cs). It does NOT currently scan dx11 files — so a naive *extension* of the guarded-file list to include the new DX11 files would false-trip on ComPtr.
**How to avoid:** Do NOT add `render_backend_dx11.cpp`/`directx11.cpp` to the `NoDeviceResetTests` guarded list. ComPtr `.Reset()` is RTV release, not a D3D9 device Reset. If a DX11 source must be gated for some other reason, gate on a DX11-specific symbol, not `.Reset(`. (`feedback_gsd_grep_gate_hygiene`.)
**Warning signs:** NoDeviceResetTests fails on a file that only does RTV ComPtr management.

### Pitfall 5: D-06 API-neutrality gate must extend to DX11 symbols
**What goes wrong:** A DX11 type leaks into `imgui_impl.cpp` (e.g. someone reaches for `ID3D11ShaderResourceView` to wire a depth window) and the gate doesn't catch it because the token set only bans DX9 forms.
**Why it happens:** `ImguiApiNeutralityTests.cpp` `dx9Tokens()` lists only D3D9 symbol forms.
**How to avoid:** Extend the token set to also ban `#include <d3d11.h>`, `#include <imgui_impl_dx11.h>`, `imgui_impl_dx11`, `ID3D11Device`, `ID3D11DeviceContext`, `IDXGISwapChain`, `ID3D11RenderTargetView`, `ID3D11ShaderResourceView`, `directx11.h`, `directX11::`. Keep gating on concrete symbol forms, NOT the bare strings "D3D11"/"DXGI" (comments mention them).
**Warning signs:** new DX11 include in imgui_impl that the gate passed.

### Pitfall 6: `GetHookPoints` POD-return ABI mismatch
**What goes wrong:** Utinni's local `UtinniDx11HookPoints` struct field order/types don't match the producer's → garbage pointers.
**Why it happens:** Two independent definitions of the same POD.
**How to avoid:** Copy the struct definition verbatim from `19-INSTRUMENTATION-SPEC.md` §3.1 / producer `Direct3d11.cpp:872-877`: `{IDXGISwapChain1*, ID3D11Device*, ID3D11DeviceContext*}`, `__cdecl`, returned by value.
**Warning signs:** swapChain non-null but Present detour faults on the address.

## Runtime State Inventory

> Phase 19 is additive code (new files + a vcpkg feature toggle + detection branch). It does NOT rename, migrate, or rewrite stored data. The categories below are answered for completeness.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — no datastore keys/IDs touched. Verified: phase adds render-path code only. | None |
| Live service config | The SWG-Source client must ship `gl11_r.dll` with the `GetHookPoints` export. ALREADY DONE in the pinned `swg-client-v2` @ `056632a` (`Direct3d11.cpp:879-888`). A *deployed* `gl11_r.dll` build must carry it before live-smoke (D-14 sequencing). | Verify deployed gl11_r.dll exports GetHookPoints before D-22 smoke |
| OS-registered state | None — no Task Scheduler / pm2 / service registration. Verified: injection model unchanged. | None |
| Secrets/env vars | None. The `ut.ini` keys (`enableInternalUi`, `enableEditorMode`) are read unchanged. | None |
| Build artifacts | `vcpkg_installed/x86-windows/` will gain `imgui_impl_dx11.{h,cpp}` after the `dx11-binding` feature is added (one reinstall). The new `directx11.obj` / `render_backend_dx11.obj` are fresh build outputs. | Reinstall vcpkg deps; add new TUs to UtinniCore.vcxproj + .filters |

**Nothing found in categories Stored data / OS-registered state / Secrets — verified by reading the phase scope (additive render-path code, no rename/migration).**

## Code Examples

### Backend detection + selection (in `imgui_impl::setup`, ~line 283)
```cpp
// REPLACES the unconditional `render_backend::set(render_backend::dx9Singleton());` (line 283).
// D-15/D-16/D-17: one GetModuleHandle check, default D9, D11 only on positive detect.
// Ground truth (Graphics.cpp:195-228): gl11_r.dll == rasterMajor 11 == D3D11;
// gl05/gl06/gl07_r.dll == rasterMajor 5-7 == D3D9.
if (GetModuleHandleA("gl11_r.dll") || GetModuleHandleA("gl11_d.dll")) {
    if (directX11::tryInstall()) {                  // resolves GetHookPoints, polls, installs (Pattern 3)
        render_backend::set(render_backend::dx11Singleton());
        utinni::log::info("Render backend: D3D11 (gl11 detected, GetHookPoints advertised)"); // one-shot
    } else {
        render_backend::set(render_backend::dx9Singleton());  // gl11 present but no export → graceful D9 fallback + bail
        utinni::log::warning("gl11 detected but GetHookPoints absent/not-ready; defaulting D3D9 (no overlay if no D9 device)");
    }
} else {
    render_backend::set(render_backend::dx9Singleton());
    utinni::log::info("Render backend: D3D9 (default; no gl11 detected)"); // one-shot diagnostic (RNDR-03)
}
```
*(Note: the exact poll-vs-install split between `setup()` and the per-frame hook is Claude's Discretion per D-21 note; the D3D11 swapchain may not be ready at first `setup()`, so a per-frame `tryInstall` poll like the D3D9 `hkPresent` one-shot is the natural shape.)*

### ImGui DX11 backend call sequence (in `render_backend_dx11.cpp`)
```cpp
// Source: imgui backends/imgui_impl_dx11.h public API (stable 1.80-1.92.x).
// Maps onto the IRenderBackend vtable:
//   init(device,context)            -> ImGui_ImplDX11_Init(device, context)        // non-virtual, off-vtable (like Dx9 init)
//   newFrame()                      -> [rebind backbuffer RTV] ImGui_ImplDX11_NewFrame()
//   renderDrawData(dd)              -> ImGui_ImplDX11_RenderDrawData(dd)
//   onPreResize()  / onPostResize() -> NOT no-ops (unlike Dx9): release / recreate Utinni RTV
//   sceneDepthTexture/Color()       -> (ImTextureID)0 for MVP (no DX11 depth SRV wired this phase)
//   sceneDepthStage get/set         -> 0 / no-op (DX11 has no directX::depth stage)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Blind throwaway-device vtbl harvest (D3D9, still in directx9.cpp) | Cooperative advertisement `GetHookPoints()` for D3D11 | Phase 19 (D-09/D-10) | Hooks land on the real live swapchain; no wrapped-vtable risk. D3D9 keeps the harvest (SWGEmu client is not Utinni-controlled). |
| `D3D11CreateDeviceAndSwapChain` (single call) | `D3D11CreateDevice` + `CreateSwapChainForHwnd` (flip model) | producer v2.2 (`Direct3d11_Device.cpp:574`) | Producer uses `IDXGISwapChain1` + flip-discard; the offline harvest test (D-21 layer 3) may still use `...AndSwapChain` (simpler) since it only needs vtbl offsets, which are interface-stable. |
| BitBlt swap effect | `DXGI_SWAP_EFFECT_FLIP_DISCARD` | producer v2.2 (`Direct3d11_Device.cpp:570`) | RTV unbinds after Present → overlay must rebind RTV (Pattern 4). |

**Deprecated/outdated:**
- AGENTS.md "dx11-binding feature is in the manifest" — **outdated/incorrect**; it is NOT in `vcpkg.json`. Treat as a task, not a given.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | D3D11 WARP (`D3D_DRIVER_TYPE_WARP`) is available on the self-hosted CI runner for the offline harvest test, and its swapchain vtbl offsets match HAL (idx 8 Present, 13 ResizeBuffers). | Standard Stack / D-21 layer 3 | If WARP unavailable, the offline harvest test can't run in CI; mitigate by making it skip-with-log when device creation fails (the test still asserts no-leak on the paths it can run). vtbl offsets are interface-defined (IDXGISwapChain), so HAL/WARP parity is near-certain. |
| A2 | The `ImGui_ImplDX11_*` public API is unchanged across imgui 1.80–1.92.x. | Standard Stack | Very low — these signatures are stable; confirm against the actual installed header at plan/build time. |
| A3 | Utinni can derive the focus HWND from `IDXGISwapChain1::GetHwnd()`/`GetDesc1()`. `GetHwnd` exists on `IDXGISwapChain1`. | Pattern 3 | Low — `IDXGISwapChain1::GetHwnd` is a documented method; if it returns the child HWND vs top-level, the WndProc subclass target may differ. Verify the HWND identity against `utinni::Client::getSwgHwnd()` at smoke time. |
| A4 | The producer's RTV is fully released before Utinni's `hkResizeBuffers` detour fires (ordering at `Direct3d11_Device.cpp:1200-1205`). | Pattern 2 | Low — confirmed by reading the source; the `.Reset()` calls precede the `ResizeBuffers` call lexically and execute first. |

## Open Questions

1. **`WM_SIZE` drag-resize for the embedded panel (RNDR-04 honest bound)**
   - What we know: the client calls `ResizeBuffers` ONLY from `displayModeChanged()` on `WM_DISPLAYCHANGE` (`Direct3d11_Device.cpp:1181`; trigger chain spec §8). It does NOT call `ResizeBuffers` on `WM_SIZE`.
   - What's unclear: whether the WinForms reparent/embed generates any path that fires `ResizeBuffers`. It almost certainly does NOT for free window-drag/panel-resize.
   - Recommendation: **Success-criterion-3 / RNDR-04 is demonstrable for `WM_DISPLAYCHANGE` (monitor mode change)** via the live-smoke. Free `WM_SIZE` drag-resize of the embed panel is **out of reach** unless the client takes spec §6 Option 1 (call `displayModeChanged()` on `WM_SIZE`). Plan the RNDR-04 acceptance as "no `DXGI_ERROR_INVALID_CALL`, RTV recreated, overlay survives a mode change"; track the `WM_SIZE` quality gap under RESID-04 (deliberately deferred, D-18b). State this candor to the planner so RNDR-04 is not over-scoped.

2. **Poll location: `setup()` one-shot vs per-frame `hkSwapChainPresent` poll**
   - What we know: the D3D11 swapchain may not exist at first `setup()`; the D3D9 path polls inside `hkPresent` until ready (`directx9.cpp:378`).
   - What's unclear: whether to detect/select in `setup()` (which needs a device/swapchain) or to install detours from a lightweight poll before `setup()`.
   - Recommendation: Claude's Discretion (D-21 note). Natural shape: a `directx11::tryInstall()` polled once/frame from a minimal Present-equivalent OR from `hkPresent` analog, latched. The detection branch in `setup()` selects the singleton; the actual DXGI detour install waits for `swapChain != null`. Let the planner choose; both honor D-15.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| VS2026 MSBuild (v145, x86) | UtinniCore + tests build | ✓ | Dev18 | — (worktrees OFF; inline build) |
| vcpkg imgui `dx11-binding` | `imgui_impl_dx11.{h,cpp}` | ✗ (feature not enabled) | imgui 1.92.6+ pinned | Add feature to vcpkg.json + reinstall (first task) |
| Windows SDK `<d3d11.h>`/`<dxgi1_2.h>` | DXGI hook tier | ✓ | OS SDK | — |
| DetourXS | vtbl detour | ✓ | vendored 1.0 | — |
| D3D11 WARP | offline harvest test (D-21 L3) | ? | OS-provided | Skip-with-log if device create fails (A1) |
| Live 32-bit D3D11 SWG-Source client w/ GetHookPoints | D-22 live-smoke | ✓ build exists (D-20); export shipped @ `056632a` | swg-client-v2 | — (maintainer-only checkpoint) |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** `dx11-binding` (add to manifest — trivial); WARP (skip-with-log).

## Validation Architecture

> nyquist_validation is not disabled in config.json → enabled.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Catch2 3.15.0+ (vcpkg, x86-windows) — native suite `UtinniCore.Tests` |
| Config file | `UtinniCore.Tests/UtinniCore.Tests.vcxproj` (Catch2Main supplies main()) |
| Quick run command | Build `UtinniCore.Tests` via MSBuild (x86), then run the test exe filtered: `UtinniCore.Tests.exe "[rndr02],[rndr03],[rndr04]"` |
| Full suite command | `UtinniCore.Tests.exe` (all tags) + `dotnet test --no-build` for the managed lanes |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| RNDR-02 | 10-vtable dispatch routes through Dx11Backend (mock) | unit | `UtinniCore.Tests.exe "[rndr02][graphics]"` | ❌ Wave 0 (clone RenderBackendSeamTests MockBackend) |
| RNDR-02/04 | Present=8, ResizeBuffers=13 vtbl offsets pinned (ABI-drift fence) | unit | `UtinniCore.Tests.exe "[dxgi][offsets]"` | ❌ Wave 0 (Dx11VtblOffsetTests.cpp) |
| RNDR-02/04 | Offline WARP/dummy-device harvest yields idx 8/13 + no device leak | integration (CI) | `UtinniCore.Tests.exe "[dxgi][harvest]"` | ❌ Wave 0 (Dx11DummyDeviceHarvestTests.cpp; skip-with-log if no WARP) |
| RNDR-03 | gl%02d_r.dll detection: default-D9 / positive-D11 / ambiguous→D9+log | unit | `UtinniCore.Tests.exe "[rndr03][detect]"` | ❌ Wave 0 (Dx11DetectionTests.cpp; inject module-presence states) |
| RNDR-02 | imgui_impl stays free of D3D11/DXGI symbols (extend D-06 gate) | source-gate | `UtinniCore.Tests.exe "[rndr01][graphics]"` (extended) | ✅ extend ImguiApiNeutralityTests.cpp token set |
| RNDR-04 | No `DXGI_ERROR_INVALID_CALL`; RTV recreated; overlay survives WM_DISPLAYCHANGE | manual (live-smoke) | maintainer inject + eyeball (D-22) | manual-only — CI cannot run live D3D11 |
| RNDR-02/03/04 | Overlay renders + RT-space input on live D3D11 | manual (live-smoke) | maintainer inject (D-22, after D-14 handoff lands) | manual-only |

### Sampling Rate
- **Per task commit:** `UtinniCore.Tests.exe "[graphics]"` (the seam + offset + detection + neutrality tags).
- **Per wave merge:** full `UtinniCore.Tests.exe` + `dotnet test --no-build` (managed lanes) + `tools/` build lane (CI gates master).
- **Phase gate:** full Catch2 suite green (CI) → THEN maintainer live-smoke (D-22) is the final RNDR-02/03/04 acceptance.

### Wave 0 Gaps
- [ ] `UtinniCore.Tests/Graphics/Dx11VtblOffsetTests.cpp` — pins Present=8, ResizeBuffers=13 (RNDR-02/04, D-21 L1)
- [ ] `UtinniCore.Tests/Graphics/Dx11DummyDeviceHarvestTests.cpp` — WARP harvest + no-leak (D-21 L3); process-isolated or skip-with-log
- [ ] `UtinniCore.Tests/Graphics/Dx11DetectionTests.cpp` — detection/fallback logic with injected module-presence (RNDR-03, D-21 L4)
- [ ] Extend `RenderBackendSeamTests.cpp` (or a sibling) — Dx11Backend mock-dispatch contract (D-21 L2). NOTE: the Dx11Backend impl pulls in `<imgui_impl_dx11.h>` + DetourXS + directx11, so the dispatch test should use the SAME MockBackend pattern against the IRenderBackend ABC (device-free), NOT instantiate the concrete Dx11Backend — mirrors the Phase-18 Option-A split rationale (vcxproj comment lines 141-146).
- [ ] Extend `ImguiApiNeutralityTests.cpp` `dx9Tokens()` → add the D3D11/DXGI symbol forms (Pitfall 5).
- [ ] Register all new `.cpp` in `UtinniCore.Tests.vcxproj` + `UtinniCore.vcxproj` (+ `.filters`).
- [ ] First task: add `dx11-binding` to `vcpkg.json` imgui features + reinstall.
- [ ] Decide whether the new DXGI test files need the DX11 SDK include dirs in the test vcxproj (they include `<d3d11.h>`/`<dxgi1_2.h>` — OS SDK, on the default include path).

## Security Domain

> `security_enforcement` is not set in config.json → treat as enabled. This phase is an in-process injected render hook with no network/auth/crypto/input-from-untrusted-source surface.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | n/a — local injected overlay |
| V3 Session Management | no | n/a |
| V4 Access Control | no | n/a — all editing local/offline (DEC-A1/A4) |
| V5 Input Validation | partial | The advertised `GetHookPoints()` pointers are trusted (same-process, Utinni-controlled client). Null-check every field before use (spec §3.3); `GetProcAddress`-absent → graceful bail (spec §7.2). No external/untrusted input. |
| V6 Cryptography | no | n/a |

### Known Threat Patterns for {injected D3D11 hook}
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Null/stale borrowed pointer deref (swapChain not ready, post-destroy) | Denial of Service (crash) | Poll `swapChain != null` before install (spec §3.3); treat all three as borrowed, never Release (spec §4.1). |
| vtbl offset drift (DXGI ABI change) silently hooking the wrong method | Tampering | D-21 L1 offset-assert Catch2 test fails the build on drift. |
| Detour faulting on a bad/unmapped address | DoS | DetourXS `Detour::CheckPointer` guard (as the D3D9 path does); read the address off the LIVE swapchain (advertised), not a hardcoded RVA. |
| `GetHookPoints` absent on a non-instrumented gl11_r.dll | DoS (crash on missing export) | Graceful log-and-bail to no-overlay (spec §7.2) — must NOT crash. |

## Sources

### Primary (HIGH confidence)
- Consumer code (read directly): `UtinniCore/swg/graphics/render_backend.h` / `render_backend.cpp` / `render_backend_dx9.cpp`; `directx9.{cpp,h}`; `swg/ui/imgui_impl.cpp` (setup ~263-377, render RT-space block ~497-512); `utinni.cpp` (createDetours ~109, init ordering ~371-375); `utility/memory.cpp` (findPattern); `UtinniCore.Tests/Graphics/{RenderBackendSeamTests,ImguiApiNeutralityTests,NoDeviceResetTests}.cpp`; `UtinniCore.Tests/UtinniCore.Tests.vcxproj`; `external/DetourXS/detourxs.h`; `vcpkg.json`.
- Producer code (read directly, `swg-client-v2` @ `056632a`): `Direct3d11.cpp:846-888` (GetApi + GetHookPoints export — ALREADY SHIPPED), `:1177-1179` install readiness; `Direct3d11_Device.cpp:540-592` (CreateSwapChainForHwnd, FLIP_DISCARD, BGRA8), `:702-730` accessors, `:922-923` per-frame RTV bind, `:1122-1173` present (Present(1,0)), `:1181-1217` displayModeChanged/ResizeBuffers; `clientGraphics/.../Graphics.cpp:185-228` (gl%02d_r.dll load, rasterMajor 11=D3D11, 5-7=D3D9).
- `19-CONTEXT.md` (D-09..D-22) + `19-INSTRUMENTATION-SPEC.md` (Candidate A contract) + `18-CONTEXT.md` (seam D-01..D-08) + `REQUIREMENTS.md` (RNDR-02/03/04).
- `docs/ai/lessons.md:13-14` (D3D11 resize analog = RTV release/recreate inside ResizeBuffers, never tear down swapchain).

### Secondary (MEDIUM confidence)
- vcpkg imgui port features (dx9/dx10/dx11/dx12/win32 bindings) — confirms `dx11-binding` is a valid feature: https://github.com/Microsoft/vcpkg/blob/master/ports/imgui/portfile.cmake , https://vcpkg.io/en/package/imgui
- DXGI flip-model RTV unbind-after-Present: https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgiswapchain-present , https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/d3d10-graphics-programming-guide-dxgi

### Tertiary (LOW confidence)
- WARP availability + vtbl parity on the CI runner (A1) — inferred from interface stability, not measured this session.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libs verified in-repo / vcpkg port; only `dx11-binding` enablement pending (a task, not a risk).
- Architecture / hook patterns: HIGH — both the producer's DX11 device code and Utinni's D3D9 hook tier are read and provide verified reference implementations.
- Detection mapping: HIGH — ground-truthed against `Graphics.cpp:204-221` (rasterMajor→API) and `:195` (gl%02d_r.dll format).
- Resize story (`WM_SIZE` bound): MEDIUM — the `WM_DISPLAYCHANGE`-only trigger is verified (Open Q1); the embed `WM_SIZE` reality is honestly bounded, not measured live.
- ImGui DX11 API exactness: HIGH (stable API; confirm header at build).

**Research date:** 2026-06-15
**Valid until:** 2026-07-15 (stable; swg-client-v2 D3D11 contract is pinned at `056632a` and the export is already shipped — re-check only if the producer recreates the swapchain object or moves to Present1).
