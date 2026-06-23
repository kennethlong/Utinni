# DX11 Embedded-Window Resize — Instrumented Debug Plan (RNDR-04 follow-on)

**Status:** PLAN for a focused, RenderDoc-instrumented, maintainer-live debug session. NOT yet started.
**Author:** Utinni Phase-24 close-out (2026-06-22), after the advertised DX11 client reached
stable-but-mis-sized embed rendering.
**Scope:** the advertised client (`SwgClient_r.exe` + `gl11_r.dll`, `rasterMajor=11`) embedded in the
TJT WinForms host. SWGEmu (D3D9) is unaffected and out of scope.

---

## 0. Symptom (live-confirmed 2026-06-22)

The advertised DX11 client now **boots + renders correct content** in the TJT embed, but is **mis-sized**:
- **Login:** the scene fills the panel width but leaves a **black bar across the bottom** (does not fill
  the vertical space). [Screenshot 2026-06-22 211025]
- **In-game:** a **redraw "mess"** — torn/overlapping geometry, content shifted, black at the bottom.
  [screenShot0399]
- **Standalone (no injection): correct.** So it is embed/reparent-specific, and DX11-specific (the D3D9
  advertised + SWGEmu embeds render fine via the RT-space stretch).

## 1. Established facts (do not re-derive)

- Backbuffer is created **1600×900** (`stage/client.cfg`: `screenWidth/Height`, `windowed=true`).
- The gl11 swapchain already uses **`DXGI_SCALING_STRETCH`** (`Direct3d11_Device.cpp:569`), so the
  backbuffer *should* stretch to the present window — this is **NOT** a scaling-mode bug.
- `client::wndProc` is a **carve-out** (NOT bound) — so Utinni does **not** forward `WM_SIZE` to the
  client, and the client does **not** drive `ResizeBuffers` from the embed. The backbuffer therefore
  stays at its 1600×900 creation size. (Binding `client::wndProc` to drive `WM_SIZE`→`ResizeBuffers`
  was tried and REVERTED — it forwarded the *panel's* `WM_SIZE` at the wrong size and corrupted the
  render / crashed; see the gap doc. Do NOT reinstate that approach.)
- On D3D9 the overlay runs in **render-target space** (DisplaySize + mouse scaled onto an un-Reset
  backbuffer) — that trick masks a backbuffer/window mismatch. **DXGI does not** give that for free:
  the backbuffer + RTV + viewport must match the present window, and the reparent changes the window
  rect without any corresponding `ResizeBuffers`/RTV/viewport update.

## 2. Leading hypothesis

The embedded SWG window is reparented into the TJT panel and (partially) resized to the panel **width**,
but the **DX11 backbuffer (1600×900) + the RTV + the viewport are never resized to the panel's client
rect**, so:
- the stretched present maps 1600×900 onto a window whose height ≠ 900-scaled → letterbox/black bar; and
- the RTV/viewport (sized to 1600×900 or to a stale size) ≠ the live window → the in-game tearing.

## 3. What to INSTRUMENT first (before any fix) — turn the guess into data

Add one-shot / throttled diagnostics (gate to `isAdvertisedClient()` + DX11) and capture a frame:

1. **In `directx11.cpp`** (present/resize path), log per-acquire + on each `hkResizeBuffers`:
   - the swapchain `GetDesc1()` width/height (backbuffer),
   - the swapchain `GetHwnd()` + that HWND's `GetClientRect` (the present window),
   - the Utinni RTV dimensions (after `onPostResize`).
2. **In `PanelGame`** (UtinniCoreDotNet): log the panel's `ClientRectangle` and the reparented SWG
   window's `GetWindowRect`/`GetClientRect` at embed time + on panel resize.
3. **RenderDoc** (the `swg-client-v2/evidence` workflow): capture one login frame + one in-game frame.
   Inspect: backbuffer dimensions, the bound RTV size, the viewport (`RSSetViewports`), and the final
   present source/dest rects. The black bar's origin is one of {backbuffer < window, RTV/viewport <
   backbuffer, window < panel}.

The instrumentation alone should localize the mismatch (which of the three rects disagrees).

## 4. Candidate fixes (test ONE at a time, each maintainer-smoked)

Likely in priority order once §3 localizes it:
- **(A) Drive a correct `ResizeBuffers` from the embed.** When PanelGame sizes the embedded window,
  have Utinni (consumer-side, in `directx11.cpp`) call `ResizeBuffers` to the **queried client rect of
  the embedded window** (NOT a forwarded `WM_SIZE` message param — that was the broken approach), then
  rebuild the RTV (`onPostResize`) + set the viewport to match. Trigger once at install (to the initial
  panel size) and on panel resize.
- **(B) Size the embedded window to the panel.** Ensure PanelGame `SetWindowPos`'s the reparented SWG
  window to the panel's full client rect (so window == panel), then (A) resizes the backbuffer to it.
- **(C) Viewport/RTV-only.** If the backbuffer already stretches correctly but the RTV/viewport are
  stale (the tearing), rebuild them to the backbuffer size each resize without changing the backbuffer.

## 5. Acceptance

- Login fills the panel — no black bar.
- In-game renders cleanly — no tearing/overlap.
- Resizing the TJT panel (and maximize) rescales correctly.
- SWGEmu (D3D9) unchanged. No startup/resize crash at any ASLR base.

## 6. Hard-won guardrails (from this session)

- **Maintainer-live + RenderDoc only.** `installable()` / headless builds cannot see render correctness;
  every attempt needs a smoke. Do NOT commit speculative render changes — instrument, capture, then fix.
- **Never re-bind `client::wndProc`** to drive resize — it forwards the panel's message at the wrong size
  and (ASLR-dependent) can jump to a stale RVA. Drive `ResizeBuffers` directly with a *queried* rect.
- One change per smoke; revert immediately if a boot regresses.
