# SWG-Source D3D11 Hook-Point Advertisement — Instrumentation Spec

**Status:** Handoff spec — ready for the `swg-client-v2` implementer
**Author:** Utinni Phase 19 (Dx11Backend + Config Detection + Resize)
**Date:** 2026-06-15
**Target repo:** `D:/Code/swg-client-v2` (the SWG-Source D3D11 client; `gl11_r.dll`)
**Consumer:** Utinni `UtinniCore.dll` (injected 32-bit modding overlay), Phase 19 `Dx11Backend`
**Scope:** 32-bit only (x64 is out of v2.1).

---

## 0. TL;DR for the implementer

Add **one new exported C function** to `gl11_r.dll` that hands out the live DXGI swapchain +
device + context as opaque pointers. That's the whole contract. ~7 lines of code, no logic change,
no dependency on Utinni. Everything else (hooking Present/ResizeBuffers, the ImGui overlay) lives on
Utinni's side and is NOT your concern.

```cpp
// New, in Direct3d11.cpp — immediately after the existing GetApi() export (~line 848):
struct UtinniDx11HookPoints
{
    IDXGISwapChain1*     swapChain;   // null until Direct3d11_Device::create() completes
    ID3D11Device*        device;
    ID3D11DeviceContext* context;
};

extern "C" __declspec(dllexport) UtinniDx11HookPoints __cdecl GetHookPoints()
{
    UtinniDx11HookPoints hp = {};
    hp.swapChain = Direct3d11_Device::getSwapChain();   // NEW one-line accessor (see §3.2)
    hp.device    = Direct3d11_Device::getDevice();      // exists
    hp.context   = Direct3d11_Device::getContext();     // exists
    return hp;
}
```

Plus a one-line accessor `getSwapChain()` in `Direct3d11_Device.{h,cpp}` (§3.2). That is the entire
deliverable. The rest of this doc is the *why*, the exact anchors, the guarantees Utinni relies on,
and one open question about window resize (§6) you should weigh in on.

---

## 1. Why this exists (context)

Utinni is an in-process modding overlay (ImGui) injected into the live SWG client. Under Direct3D 9
it acquires its render hook by harvesting the device vtable from a throwaway device created via the
public D3D9 API, then detouring `Present`. For the D3D11 client we are **replacing the blind
throwaway-harvest with cooperative advertisement**: because this client is a SWG-Source build we
control, it can simply *expose its live hook points* and Utinni reads them directly.

Benefits over the blind harvest (why we want advertisement):
- Utinni hooks the **actual live swapchain** SWG uses — zero risk of a throwaway object yielding a
  different/wrapped vtable.
- Utinni gets `device` + `context` directly for `ImGui_ImplDX11_Init` — no `GetDevice()` dance, no
  dependency on first-Present timing for setup.
- No throwaway `D3D11CreateDevice`/`CreateSwapChainForHwnd` cost or GPU-driver edge cases.

The client stays **completely Utinni-agnostic**: `GetHookPoints()` is a pure read-only getter. If
Utinni is not injected, nothing calls it; the export is inert.

---

## 2. What Utinni needs (the requirements behind the contract)

| Need | Why | How satisfied by this spec |
|------|-----|----------------------------|
| Live `IDXGISwapChain1*` | Detour `Present` (vtbl idx 8) + `ResizeBuffers` (vtbl idx 13) off the real object | `GetHookPoints().swapChain` |
| `ID3D11Device*` | `ImGui_ImplDX11_Init(device, context)` | `GetHookPoints().device` |
| `ID3D11DeviceContext*` | `ImGui_ImplDX11_Init` + per-frame draw submit | `GetHookPoints().context` |
| Focus `HWND` | WndProc subclass for input + RT-space mapping | Utinni derives it via `swapChain->GetDesc1()`/`GetHwnd()` — **not** part of the contract |
| Readiness signal | Must not hook before the swapchain exists | Null `swapChain` field = not ready; non-null = live for the session |
| Renderer-DLL identity | One-backend-per-session detection | `GetModuleHandleA("gl11_r.dll")` — already true today, no client change |

Utinni does **all** of the hooking, overlay rendering, RTV management, and resize-survival itself.
The contract is purely: *expose these three live pointers when they exist.*

---

## 3. The contract (Candidate A — recommended)

### 3.1 The export

Add to `Direct3d11.cpp`, immediately after the existing `GetApi()` export (anchor: `Direct3d11.cpp:846-853`):

```cpp
struct UtinniDx11HookPoints
{
    IDXGISwapChain1*     swapChain;   // null until create() completes; non-null + stable for session
    ID3D11Device*        device;
    ID3D11DeviceContext* context;
};

extern "C" __declspec(dllexport) UtinniDx11HookPoints __cdecl GetHookPoints()
{
    UtinniDx11HookPoints hp = {};
    hp.swapChain = Direct3d11_Device::getSwapChain();
    hp.device    = Direct3d11_Device::getDevice();
    hp.context   = Direct3d11_Device::getContext();
    return hp;
}
```

**Calling convention:** `__cdecl`, `extern "C"` (no name mangling). 32-bit. Utinni resolves it with
`GetProcAddress(GetModuleHandleA("gl11_r.dll"), "GetHookPoints")`. If your toolchain decorates `__cdecl`
exports with a leading underscore in the export table, prefer a `.def` file entry or `#pragma comment(linker, "/EXPORT:GetHookPoints")` so the undecorated name `GetHookPoints` appears in the export table (matching how `GetApi` is exported today).

**Return by value** is deliberate — three pointers in a small POD struct, returned in the standard
manner; no shared-memory lifetime concerns, no out-params.

### 3.2 The one new accessor

`Direct3d11_Device` already exposes `getDevice()`, `getContext()`, `getBackBufferRTV()`,
`getDepthStencilView()` (anchor: `Direct3d11_Device.cpp:702-730`) but **not** the swapchain. Add the
parallel one-liner:

```cpp
// Direct3d11_Device.h — beside the existing getters (~line 31-156 public section)
static IDXGISwapChain1* getSwapChain();

// Direct3d11_Device.cpp — beside getBackBufferRTV() (~line 703)
IDXGISwapChain1* Direct3d11_Device::getSwapChain()
{
    return ms_swapChain.Get();   // null before create() / after destroy()
}
```

That is the complete client-side change: **2 lines in the device accessor + ~7 lines for the export.**
No behavioral change to device creation, present, or resize.

### 3.3 Where readiness comes from (no extra signal needed)

The swapchain becomes valid at the end of `Direct3d11_Device::create()` after
`CreateSwapChainForHwnd` succeeds (anchor: `Direct3d11_Device.cpp:574-577`; `ms_installed = true` at
line 592). Utinni polls `GetHookPoints().swapChain != nullptr` (cheap) until non-null, then installs
once. No new callback or event is required. Publication is implicitly at end of
`Direct3d11::install()` (anchor: `Direct3d11.cpp:1177-1179`) — by which point device, context,
swapchain, and the window are all live.

---

## 4. Guarantees Utinni relies on (please preserve)

1. **Ownership stays with the client.** Utinni treats all three pointers as **borrowed** — it will
   never `Release()`/`AddRef()` them for ownership. `ms_swapChain`/`ms_device`/`ms_context` remain
   client-owned `ComPtr`s.
2. **Swapchain pointer is stable for the session.** `ms_swapChain` must **persist through resize**
   (today it does — `displayModeChanged()` resets only the RTV/DSV, not the swapchain; anchor
   `Direct3d11_Device.cpp:1198-1215`). Utinni's installed detours assume the swapchain object identity
   does not change mid-session. If a future change recreates the swapchain object, the contract must
   gain a re-advertise signal — flag it.
3. **Null = not ready, non-null = live.** The fields must be null before `create()` and after
   `destroy()`, never dangling. (The `ComPtr::Get()` getters already satisfy this.)
4. **`Present` stays `IDXGISwapChain::Present(1, 0)`** on the base interface (not `Present1`) — anchor
   `Direct3d11_Device.cpp:1121-1172`. Utinni detours vtbl idx 8; `IDXGISwapChain1` inherits the same
   slot. If the present path moves to `Present1`, tell Utinni (different vtbl slot).
5. **No `__declspec(dllexport)` ABI churn on `Gl_api`.** Utinni does not read `Gl_api`; keep
   `GetHookPoints` independent of that table.

---

## 5. Alternatives considered (and why Candidate A wins)

| Option | Mechanism | Verdict |
|--------|-----------|---------|
| **A — `GetHookPoints()` export (RECOMMENDED)** | Client exports a getter returning live swapchain/device/context; Utinni `GetProcAddress`es it | ✅ Least invasive (~9 lines), client stays Utinni-agnostic, null-safe readiness, no ABI coupling, gives the live swapchain so hooks land on the real object |
| B — `Gl_api` slot-swap | Utinni reads `GetApi()`'s `present`/`displayModeChanged` fn-pointer slots and overwrites them | ❌ Zero client change but couples Utinni to the `Gl_api` ABI layout and intercepts at the engine-wrapper level, not the DXGI level — can't get the raw swapchain/device for ImGui |
| C — Shared-memory advertisement struct | Renderer publishes a named shared segment at install | ❌ Overkill for same-process injection; adds a named-object lifetime/polling concern A avoids |
| D — Push model (`gl11_r.dll` calls a `utinni_advertise_*` export in UtinniCore) | Client calls into Utinni at swapchain creation | ⚠️ Viable; eliminates Utinni's poll, but inverts the dependency (client must know Utinni's export) and means `gl11_r.dll` carries Utinni-specific code. A keeps the client clean. Reconsider only if the per-frame null-poll is undesirable. |

**Recommendation: Candidate A.** If you (the client-side implementer) find A awkward against the
codebase, Candidate D (push) is the acceptable fallback — tell Utinni's side which you chose so the
consumer matches (poll-A vs callback-D).

---

## 6. OPEN QUESTION — window resize (`WM_SIZE`) vs mode change (`WM_DISPLAYCHANGE`)

**Finding:** the client today calls `ResizeBuffers` **only** from `displayModeChanged()`, triggered by
`WM_DISPLAYCHANGE` (monitor resolution/refresh change). It does **not** call `ResizeBuffers` on
`WM_SIZE` (the user dragging the window / the embedded panel resizing). Anchors:
`Direct3d11_Device.cpp:1180-1216`, trigger chain `Os.cpp:1251-1253` → `Graphics.cpp:375-378`.

**Why it matters to Utinni:** Phase 19 success criterion 3 is "the overlay survives a window resize."
Utinni hooks `ResizeBuffers` (vtbl idx 13) and rebuilds its RTV inside that hook — which works for the
mode-change path. But Utinni runs **embedded** (its WinForms host reparents the SWG window), so the
practically common resize is a `WM_SIZE` on the SWG child window, which currently does NOT call
`ResizeBuffers`. So the backbuffer would not track the embed-panel size.

**Decision needed (please advise):**
- **Option 1 (client-side, preferred if cheap):** since you're already touching the client, also call
  the existing `displayModeChanged()`/`ResizeBuffers` path on `WM_SIZE` (clamped/debounced). This makes
  the D3D11 client resize-correct generally, and Utinni's `ResizeBuffers` hook then "just works." This
  is OUTSIDE the minimal advertisement contract — a separate, optional client improvement.
- **Option 2 (defer):** leave `WM_SIZE` unhandled; Utinni accepts that free window-drag resize is part
  of the broader windowed↔fullscreen edge-case bucket (Utinni RESID-04, deliberately deferred). The
  overlay still survives mode changes; embed-panel drag-resize quality is a later pass.

This question does NOT block the §3 contract — implement `GetHookPoints()` regardless. It only decides
how complete the resize story is for v2.1. Default disposition if unsure: **Option 2 (defer)**, and
Utinni tracks the gap.

---

## 7. Acceptance — how Utinni will consume this (so you know "done")

1. Utinni detects the D3D11 client: `GetModuleHandleA("gl11_r.dll")` non-null at install
   (default-D3D9, D3D11 only on positive detect).
2. Utinni resolves `GetProcAddress(hGl11, "GetHookPoints")`. **If absent, Utinni logs and bails to a
   safe no-overlay state** — so a `gl11_r.dll` without this export must not crash; it simply means no
   Utinni overlay.
3. Utinni polls `GetHookPoints().swapChain` once/frame until non-null, then:
   reads vtbl idx 8/13 off the live swapchain, detours `Present`/`ResizeBuffers`, calls
   `ImGui_ImplDX11_Init(device, context)`, subclasses the focus window.
4. **Smoke (maintainer):** inject Utinni into the live D3D11 client; the ImGui overlay renders,
   takes input in render-target space, and survives a `WM_DISPLAYCHANGE` resize without
   `DXGI_ERROR_INVALID_CALL`.

**Your deliverable is complete when:** `gl11_r.dll` exports `GetHookPoints` (undecorated, `__cdecl`),
returning the three live pointers (null before `create()`), with no other behavioral change — and a
build of `gl11_r.dll` is staged. (Optionally, §6 Option 1 if you elect to do it.)

---

## 8. Anchors quick-reference (`D:/Code/swg-client-v2`)

- `src/engine/client/application/Direct3d11/Direct3d11.cpp:846-853` — `GetApi()` export (add `GetHookPoints` after).
- `src/engine/client/application/Direct3d11/Direct3d11.cpp:1177-1179` — end of `install()` (readiness point).
- `src/engine/client/application/Direct3d11/Direct3d11_Device.cpp:51-107` — the `ms_*` ComPtr statics.
- `Direct3d11_Device.cpp:560-592` — `CreateSwapChainForHwnd` + `ms_installed=true`.
- `Direct3d11_Device.cpp:702-730` — existing accessors (`getDevice/getContext/getBackBufferRTV/getDepthStencilView`); add `getSwapChain()` here.
- `Direct3d11_Device.cpp:1121-1172` — `present()` → `ms_swapChain->Present(1,0)`.
- `Direct3d11_Device.cpp:1180-1216` — `displayModeChanged()` → `ResizeBuffers` (WM_DISPLAYCHANGE-only; see §6).
- `src/engine/client/render/Graphics.cpp:193-228` — `.\gl%02d_r.dll` load (`rasterMajor=11`).

---

*Generated for Utinni Phase 19. Source of truth: this file in the Utinni repo
(`.planning/phases/19-dx11backend-config-detection-resize/19-INSTRUMENTATION-SPEC.md`). A copy is
placed in `swg-client-v2/.planning/handoff/`. If the two diverge, the Utinni copy governs the
consumer contract.*
