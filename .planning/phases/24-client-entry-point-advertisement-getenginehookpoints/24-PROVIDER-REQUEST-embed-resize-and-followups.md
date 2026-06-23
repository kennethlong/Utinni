# Provider Request — DX11 Advertised-Client Follow-ups

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider)
**Status:** QUEUED — relay AFTER the current stale-crash root fix is committed and the client is rebuilt + restaged.

---

## 0. First: rebuild + restage, then we re-smoke

The staged `SwgClient_r.exe` we've been injecting is **older than your fixes** (staged 19:25; your `38-06`
`CuiMediator` fix is 19:29, and the in-flight stale-crash fix is newer still). So before anything else:
**rebuild `SwgClient_r.exe` and restage it to `D:\Code\swg-client-v2\stage\`.** Then we re-smoke the
advertised DX11 inject and confirm world-load survives on the fixed exe.

---

## 1. Embed-resize crop — half-done client-side DX11 resize (the headline render bug)

**Symptom:** embedded in The Jawa Toolbox, the scene renders at **correct scale** but **cropped to the
upper-left** — only part of the world is visible. **Standalone (no inject) is correct.**

**What Utinni's DIAG proved** (instrumentation in `directx11.cpp` present/resize hooks):
- The swapchain is **flip-model** (`swapEffect=4` FLIP_DISCARD) + `scaling=0` (STRETCH).
- The **backbuffer correctly tracks the window** — `ResizeBuffers` fires from the SWG window's own
  `WM_SIZE`, and the backbuffer dims match the present-window client rect (e.g. login `1600×900=1600×900`;
  after a panel grow, `ResizeBuffers(1455×1040)` and it converges).
- **Therefore the crop is NOT a swapchain / stretch problem.** The consumer (Utinni) side is correct.

**Root cause (client side):** the DX11 resize path (`WM_SIZE → displayModeChanged → ResizeBuffers`)
resizes the **swapchain backbuffer** but does **not** resize the **3D scene viewport + offscreen render
target(s)/depth + projection/aspect**. The scene is drawn into a stale (smaller, top-left-anchored)
viewport, so only that region lands in the correctly-sized backbuffer → "scale right, cropped to
upper-left." Standalone never triggers a resize (it renders at the cfg `1600×900`), which is why it's
masked there; the embed's `SetWindowPos` is the only thing that drives a resize and exposes the half-done
path.

**Action:** on `displayModeChanged`/resize, also resize — to the new backbuffer dimensions — the scene
**viewport** (`RSSetViewports`), the offscreen **scene render target(s) + depth**, and the
**projection/aspect**, not just call `ResizeBuffers`.

**Confirm via RenderDoc:** capture a post-resize in-game frame and compare the bound viewport + scene RT
dims against the swapchain backbuffer. Expect viewport/RT smaller than (top-left of) the backbuffer
pre-fix; equal post-fix.

---

## 2. Verify the UI-teardown cluster is fully closed (probably your current fix)

The world-load crash we hit was symbolized to **`UIComboBox::~UIComboBox` (`UIComboBox.cpp:141`)** —
`mButton->RemoveCallback(this)` on a **null callback member** during scene teardown. Same family as the
`CuiMediator::deactivate` null-deref you fixed in `38-06` (UI objects half-torn-down during Utinni's embed
focus/activate churn). If your in-flight stale-crash fix addresses that **shared root**, this is likely
already covered.

**On re-smoke:** confirm world-load AND a scene change both survive. If `UIComboBox::~UIComboBox` (or
sibling UI destructors) still assert, they need the same null-guard treatment as `CuiMediator::deactivate`.
Verify before patching — don't pre-emptively whack-a-mole if the root fix covers them.

---

## 3. (Later, not now) `cuiChatWindow::ctor` by REAL ENTRY — for the chat-editor unlock

When we reach lighting up the chat editor on the advertised client: advertise **`cuiChatWindow::ctor` by
its real engine entry, for DETOUR** (like the 4 endpoints you corrected in `38-05`) — **NOT** the
placement-new thunk your current `utinni_advertise.cpp` note proposes. Utinni **detours** the ctor
(`hkCtor` publishes the chat-window instance the method hooks depend on); it does **not** construct one, so
it needs the real code entry, not an injector-supplied instance. **Low priority** — only when we get to the
editor unlock, not part of this batch.

---

## What Utinni (consumer) owns in parallel

- The DIAG instrumentation stays in (log-only, throttled) so we can verify your resize fix on re-smoke.
- The consumer swapchain/window side is correct; **no Utinni change is needed for the crop** — it's the
  client's scene viewport/RT.
- If the UI-teardown guards turn into whack-a-mole, Utinni will damp the embed **focus/activate churn**
  (the RESID-04 owned-popup watchdog + focus bouncing) — the root trigger — as its own focused RNDR-04
  pass.

**Priority order for this batch:** §0 (rebuild/restage) → §2 (verify teardown closed) → §1 (the crop fix).
§3 waits for the editor-unlock milestone.
