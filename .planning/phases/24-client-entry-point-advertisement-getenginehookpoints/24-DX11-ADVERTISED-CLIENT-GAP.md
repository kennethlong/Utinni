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

---

## LIVE-SMOKE RESULTS (2026-06-22) — overlay install FIXED; two follow-on findings

First real live inject of the advertised client (`SwgClient_r.exe`) + DX11 (`rasterMajor=11`,
`gl11_r.dll`). The added 3-pointer diagnostic resolved the hypotheses immediately.

### Overlay install — FIXED (Utinni commit 7ed9403)

- The diagnostic logged **all three pointers non-null**: `GetHookPoints acquired -- swapChain=0x1328CEE8
  device=0x02E4207C context=0x12FCAF18`. So **hypothesis #1 is REFUTED** and EPA-08 is confirmed: the
  provider populates device+context together; the crash was **consumer-side**.
- Root cause (symbolized): `ImGui_ImplDX11_Init` (imgui_impl_dx11.cpp:641) ran via the DX11 hook tier
  (`dx11Singleton()->init()`) BEFORE `imgui_impl::setup()` called `ImGui::CreateContext()`, so
  `ImGui::GetIO()` wrote through a null global context (+0x34). Fixed by creating the context (idempotent)
  in `Dx11Backend::init()` before `ImGui_ImplDX11_Init`, and guarding `setup()`'s CreateContext.
- Result: `directX11::tryInstall: D3D11 overlay installed (advertised swapchain latched)` — overlay
  installs, session ran ~5 min. **Checkpoint 2's overlay-install milestone is reached.**

### Finding 1 (PROVIDER ACTION) — `CuiMediator::deactivate()` null-deref

After ~5 min the **client** crashed (not Utinni): `module=SwgClient_r.exe rva=0x00BCF045 READ
target=0x00000014`, symbolized to **`CuiMediator::deactivate()` (CuiMediator.cpp:494**, the
`getSettingsEnabled()`/`saveSettings()` deactivate path). It fires on a `WM_ACTIVATE INACTIVE` /
focus change immediately after Utinni's RESID-04 embed-reassert watchdog re-styled the window to an
owned popup (focus bouncing between the TJT host HWND `0x00F90A88` and `0x004C0EF0`). It is a client
null-deref (a mediator member at +0x14) **exposed by the embed focus/activate churn**. Provider:
add a null-guard in `deactivate()` and/or investigate why a mediator is in a half-torn-down state when
deactivated during the embedded focus transition. Utinni may also be able to damp the focus churn — joint.

### Finding 2 (RESOLVED BY THE POST-v3 BINDING) — embed resize doesn't rescale the backbuffer

Observed: the embedded SWG window didn't size to the host panel until an input event, and when it did
resize the **render didn't rescale** (no `ResizeBuffers` ever logged). Root cause: on the advertised
client `PanelGame.WndProc` forwards to SWG's WndProc only when `swgWndProcValid` (PanelGame.cs:60-61),
which is **false** because `client::wndProc` is unbound in the current 78-name set — so `swgWndProcAddr`
keeps the unmapped SWGEmu RVA and **no window message (incl. `WM_SIZE`) reaches the client's WndProc**.
The client's `WM_SIZE → ms_displayModeChangedHookFunction → ResizeBuffers` path EXISTS (Os.cpp, added
for exactly this embed case, DX11 spec §6) but never receives the message. **Fix: bind `client::wndProc`
in the post-v3 consumer pass.** The provider already advertised it as `utinni_osWindowProc` (38-02);
`getSwgWndProcExport()` (client.cpp:326) returns the resolver-managed `swg::client::wndProc`, so once
bound, `swgWndProcValid` flips true → forwarding restored → `WM_SIZE` reaches the client → `ResizeBuffers`
fires → the DX11 backbuffer tracks the panel. `client::wndProc` is a CALLED endpoint (`CallWindowProc`),
so the call-through shim is the correct mechanism (not subject to the detour-vs-call finding). No separate
fix — this rides the gated post-v3 binding wiring.

---

## LIVE-SMOKE RESULTS round 2 (2026-06-22 PM) — overlay-install crash class closed; embed-resize remains

The post-v3 consumer wiring + a string of fixes took the advertised DX11 client from "crashes on
overlay install" to "boots + renders correct content, mis-sized embed". Sequence (all committed):

- **Overlay install** — FIXED (ImGui context before ImGui_ImplDX11_Init, 7ed9403).
- **setPreloadSnapshot** hardcoded data-global write on the advertised client — gated (fe97a52).
- **client::wndProc binding** ("DX11 resize fix") — REVERTED to a carve-out (4072eb1): it forwarded
  the TJT panel's WM_SIZE to the embedded client -> ResizeBuffers to the wrong size -> corrupted render
  (login narrow strip / in-game oversized). It did not fix resize; it broke the render.
- **getSwgWndProcExport ASLR crash** — FIXED (c842b84): returns null on the advertised client so
  PanelGame never forwards to the stale SWGEmu wndProc RVA (which, at a low ASLR base, lands in the
  relocated client's mapped+executable range and IsExecutableAddress() wrongly passed -> jump-to-garbage
  startup crash). Pre-existing ASLR-roulette latent bug, now deterministic-safe.

**Current state:** advertised DX11 client BOOTS reliably + the DX11 overlay installs + content renders
CORRECTLY, but the embed is **mis-sized** — black bar at the bottom of the login screen (doesn't fill
vertical space) + in-game redraw tearing. Backbuffer is 1600x900, the gl11 swapchain already uses
DXGI_SCALING_STRETCH, client::wndProc is carved out (no WM_SIZE->ResizeBuffers), so the backbuffer
stays at creation size and the embedded window / RTV / viewport don't match the TJT panel rect on the
DX11 path (D3D9's RT-space stretch masked this; DXGI doesn't).

**This is the remaining RNDR-04 work — an instrumented, maintainer-live, RenderDoc debug session, NOT a
headless fix.** See `24-DX11-EMBED-RESIZE-DEBUG-PLAN.md` (symptom + established facts + what to
instrument + ranked candidate fixes + guardrails). Checkpoint 2's overlay-install milestone is reached;
embed-resize correctness is the follow-on.

---

## EMBED-RESIZE — instrumented finding (2026-06-23): half-done client-side resize (viewport/RT, NOT swapchain)

DIAG (directx11.cpp present/resize hooks, commit 405fc8d) localized it:
- Swapchain is **flip-model** (`swapEffect=4` FLIP_DISCARD) + `scaling=0` (STRETCH). The **backbuffer
  TRACKS the window** — `ResizeBuffers` fires from the SWG window's OWN `WM_SIZE` (the carve-out never
  broke resize). Sample: login `clientRect=1600x900 backbuffer=1600x900`; after a panel grow,
  `hkResizeBuffers(w=1455 h=1040)` and the backbuffer converges (one-frame lag while the window leads).
- **Symptom (maintainer): "scaling is right but the render is cropped — only the upper-left of the scene
  shows."** Since the backbuffer == window (DIAG), the crop is NOT a swapchain/stretch issue. The client
  is rendering the **3D scene into a viewport / scene render-target that does not match the resized
  backbuffer**, so only the top-left region lands in the (correctly-sized) backbuffer.

**Root cause (PROVIDER side):** the client's DX11 resize path (`WM_SIZE → displayModeChanged →
ResizeBuffers`) resizes the **swapchain backbuffer** but does NOT fully resize the **scene viewport +
render target(s) + projection**. Standalone never resizes (renders at the cfg 1600x900), so it is
correct; the embed's `SetWindowPos` is the only thing that triggers a resize, exposing the half-done path.

**Provider action:** on `displayModeChanged`/resize, also resize the scene viewport (`RSSetViewports`) +
the off-screen render target(s) + the projection/aspect to the new backbuffer size — not just
`ResizeBuffers`. Confirm via RenderDoc: capture a post-resize in-game frame and compare the bound
viewport + scene RT dims against the swapchain backbuffer (they will be smaller / top-left-anchored).

**Consumer (Utinni) side is correct here** — the swapchain backbuffer already tracks the window; Utinni
does not own the client's scene viewport/RT. No Utinni change indicated for the crop.

(Separately, the WORLD-LOAD crash is a client UI-teardown assert: `UIComboBox::~UIComboBox`
[UIComboBox.cpp:141] on a null callback member during the scene transition -- same family as the
`CuiMediator::deactivate` crash the provider fixed in 38-06. Note the staged `SwgClient_r.exe` is
19:25, the 38-06 fix is 19:29 -> the staged exe PREDATES the fix; rebuild + restage, and guard
`UIComboBox::~UIComboBox` the same way.)
