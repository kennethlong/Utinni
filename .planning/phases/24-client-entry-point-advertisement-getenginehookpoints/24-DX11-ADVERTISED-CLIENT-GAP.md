# DX11 Overlay on the Advertised Client — Live-Smoke Gap (Checkpoint 2)

**Status:** HANDOFF — open finding from the Phase-24 live-smoke (2026-06-22). Addendum to
`2026-06-15-utinni-dx11-hookpoint-advertisement-spec.md` (the graphics contract, IMPLEMENTED/SHIPPED).
**Author:** Utinni Phase 24 close-out.
**Audience:** the follow-on milestone — both `swg-client-v2` (provider) and Utinni (consumer).
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/`
as `2026-06-22-utinni-dx11-advertised-client-gap.md`.

---

## 0. TL;DR

The graphics contract `gl11_r.dll!GetHookPoints` is shipped and was verified in Phase 19 on the
**standalone** D3D11 client. Phase 24 introduced a **new, never-before-tested configuration**: the
**advertised exe** (`SwgClient_r.exe` exporting `GetEngineHookPoints`) **+ DX11** (`gl11_r.dll`). In
that combination the DX11 overlay does **not** come up: the kickoff fires, but the **first per-frame
acquisition poll null-derefs** —

> `VEH FATAL 0xC0000005 WRITE target=0x00000034`

— on the first tick of `directX11::pollThunk → tryInstall()`. The maintainer **waived** DX11 for v2.1
(Checkpoint 2 deferred). This doc captures the exact state + code map so the follow-on can resume
without re-deriving it. **Root cause is NOT yet established** (live DX11 debugging was deferred); what
follows is the confirmed symptom, the precise consumer path, and ranked hypotheses.

## 1. What works vs. what fails

- ✅ **Standalone D3D11 client (Phase 19):** `gl11_r.dll!GetHookPoints` returns live
  swapchain/device/context; overlay renders, takes input, survives resize.
- ✅ **Advertised client on D3D9 (Checkpoint 1):** `SwgClient_r.exe` boots D3D9 by default
  (`Render backend: D3D9 (default; no gl11 detected)`); overlay renders RENDER-only. Proven.
- ✅ **24-03 kickoff chain:** with `graphics::install` resolved from the exe table, the unchanged
  `hkInstall` detour fires `directX11::kickoff()`, which subscribes the per-frame poll. The
  `isD3D11Client()` gate correctly skips the D3D9 throwaway-device harvest
  (`directX::detour: D3D11 client detected; skipping D3D9 throwaway-device harvest`).
- ❌ **Advertised client on DX11 (Checkpoint 2):** first poll tick → `0xC0000005 WRITE target=0x00000034`.

## 2. The exact consumer code path (instrumentation map)

`UtinniCore/swg/graphics/directx11.cpp`. `kickoff()` (`:219`) subscribes `pollThunk` (`:195`) to the
prePresent tick; each frame until latched it calls `tryInstall()` (`:115`):

1. `:127` `hGl11 = GetModuleHandleA("gl11_r.dll" || "gl11_d.dll")` — null → bail (no D3D11 client).
2. `:132` `getHookPoints = GetProcAddress(hGl11, "GetHookPoints")` — null → one-shot warn, bail.
3. `:148` `hp = getHookPoints()` — calls into gl11.
4. `:149` `if (hp.swapChain == nullptr) return false;` — **null swapChain is guarded** (polls again).
5. `:156-158` stash borrowed `swapChain/device/context`.
6. `:161` `swgptr* vtbl = *(swgptr**)hp.swapChain;` — **READ** vtable off the swapchain.
7. `:162-165` `Detour::CheckPointer(vtbl[8/13])` + `Detour::Create` Present/ResizeBuffers.
8. `:168` `render_backend::set(dx11Singleton())`.
9. `:173` `hwnd = dx11Singleton()->init(s_device, s_context);` — DX11 + ImGui-DX11 init; null → undo.
10. `:185` `imgui_impl::setup(hwnd);` `:187` latch `s_installed = true`.

**Key signal — it is a WRITE to `0x00000034`:** an offset-0x34 write off a near-null base. That is
*not* the vtable READ at step 6 (that would fault as a READ), and step 4 already guards a null
swapChain. So the fault is most consistent with **a write through a null/garbage `device`/`context`
or `this`** deeper in step 9 (`init()` / `ImGui_ImplDX11_Init`), **or** with the poll running in a
bad context (step 0). See hypotheses.

## 3. Ranked hypotheses (UNVERIFIED — resume points)

1. **`GetHookPoints()` returns a non-null `swapChain` but a null/not-yet-ready `device`/`context`** on
   the advertised-exe + gl11 combo, so the null-swapChain guard (`:149`) passes but
   `dx11Singleton()->init(null device, …)` writes through it at +0x34. **Check first:** log
   `hp.swapChain/device/context` at `:148` on the advertised client. If device/context are null while
   swapChain is set, the contract guarantee "all three live at end of `install()`" (graphics spec §3.3)
   does not hold in this configuration → provider-side ordering/readiness fix, and/or a consumer guard
   that polls until *all three* are non-null, not just swapChain.
2. **The prePresent poll fires in a wrong/partial context** because the `graphics::*` present/install
   chain on the advertised exe resolved a subset (the RENDER-only set) and the prePresent dispatch
   registry is mid-init — pollThunk runs before the graphics facade is fully live. **Check:** does the
   poll tick fire at all on D3D9-advertised (it must, since the overlay renders there) — compare the
   tick context D3D9 vs DX11.
3. **`gl11_r.dll` loaded by the advertised exe is a build without a populated `GetHookPoints`** (e.g.
   an older staged `gl11`), so `getHookPoints()` returns a zeroed/garbage struct that passes the
   swapChain check by luck. **Check:** `dumpbin /exports stage/gl11_r.dll` for `GetHookPoints`, and
   confirm the staged `gl11` matches the Phase-19 build that shipped the export.

## 4. What each side owns

- **Provider (`swg-client-v2`):** confirm that on `SwgClient_r.exe` + `gl11_r.dll`, by the time the
  client is presenting frames, `GetHookPoints()` returns **all three** pointers live (not just
  swapChain). The graphics spec §3.3 says publication is at end of `Direct3d11::install()`; verify that
  holds when the client is launched as the advertised exe (the exe-side `GetEngineHookPoints` work did
  not touch the gl11 path, but the launch/boot ordering may differ from the standalone client).
- **Consumer (Utinni):** (a) harden `tryInstall()` to poll until `device` **and** `context` are also
  non-null (cheap, removes hypothesis 1 as a crash even if the provider is slow to populate); (b) add a
  one-shot diagnostic log of the three pointers at `:148`; (c) re-run the live-smoke. This is the first
  resume step and is purely consumer-side.

## 5. Acceptance (closes Phase-18 D-08 / Phase-19 D-22)

DX11 overlay renders + takes input (render-target space) on `SwgClient_r.exe` with `rasterMajor=11`,
log shows `directX11::tryInstall: D3D11 overlay installed (advertised swapchain latched)`, no
`0xC0000005`. That closes the deferred Phase-18 (D-08) and Phase-19 (D-22) DX11 live-smokes and
Checkpoint 2 of Phase 24.
