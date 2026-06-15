# Phase 18: Render-Backend Seam + Dx9Backend - Research

**Researched:** 2026-06-15 (force-refresh; every line number re-verified against live source)
**Domain:** Native C++ (x86) render-architecture refactor inside an injected ImGui-on-D3D9 overlay
**Confidence:** HIGH (all claims grounded in read-of-source in this working tree, not training data)

<user_constraints>
## User Constraints (from 18-CONTEXT.md)

### Locked Decisions

- **D-01:** `IRenderBackend` is a **runtime-polymorphic pure-virtual abstract base** (vtable), with a
  single global instance selected at `setup()`. Phase 19 adds `Dx11Backend` behind the same vtable —
  it does not re-touch `imgui_impl`. (Concrete-now / abstract-in-19 was rejected.)
- **D-02:** The seam exposes the ROADMAP-named members — `newFrame` / `renderDrawData` /
  `onPreResize` / `onPostResize` / `renderTargetWidth` / `renderTargetHeight` — **plus a 7th**:
  an API-neutral scene-depth accessor (see D-05).
- **D-03:** `onPreResize` / `onPostResize` are **honest no-ops** in `Dx9Backend`. They exist purely
  so Phase 19's Dx11 `ResizeBuffers` has a home. D3D9 needs no resize work — no `Reset` introduced.
- **D-04:** `renderTargetWidth/Height` continue to be sourced from the existing present-stretch math
  already living in `imgui_impl.cpp` (RT-space mapping is unchanged behavior).
- **D-05:** **Full purge.** After the carve, `imgui_impl.{cpp,h}` contain ZERO `d3d9.h` include, ZERO
  `IDirect3DDevice9`, ZERO `ImGui_ImplDX9_*`. Scene depth (formerly `directX::getDepthTexture()`) is
  reached through the seam's API-neutral accessor.
- **D-06:** Enforced by a structural gate (a grep-style Catch2/xUnit Fact in the spirit of the
  `06-AUDIT` preservation Facts) that fails the build if any DX9 symbol reappears in `imgui_impl`.
  Gate on concrete symbol forms (`ImGui_ImplDX9_`, `IDirect3DDevice9`, `#include <d3d9.h>`), NOT the
  bare string "D3D9" (comment false-trip avoidance).
- **D-07:** **Both harness layers** — (1) the D-06 structural gate; (2) a Catch2 seam test that
  installs a no-op/mock `IRenderBackend` and asserts all 7 members route through the vtable.
- **D-08:** The existing **maintainer live-smoke** remains the final RNDR-01 acceptance gate (overlay
  still renders + takes input in a live D3D9 SWG session). CI cannot run it; the two automated layers
  are the regression protection CI *can* enforce.

### Claude's Discretion

- Exact split of `directX::` ownership (detour install / Present-block / wireframe / depth-texture
  init) between the new `Dx9Backend` and any residual free functions — provided the no-Reset contract
  and the D-05 purge hold. The renderCallbacks bus stays API-neutral in `imgui_impl` (not
  backend-coupled).

### Deferred Ideas (OUT OF SCOPE)

- **D3D11 backend, DXGI Present/ResizeBuffers hooks, `gl%02d_r.dll` auto-detect, one-backend-per-session
  diagnostic** — Phase 19 (RNDR-02/03/04). The seam is designed *for* these but implements none here.
- **`swg-window-resize-fullscreen-edge-cases`** todo — deliberately NOT folded (would put real resize
  logic in `onPreResize/onPostResize`, threatening "behaviorally unchanged"). Stays deferred (RESID-04
  residual).
- **`phase09-datatable-editor-review-warnings`**, **`phase10-stringtable-sc3-live-reload-residual`** —
  unrelated; not folded.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| RNDR-01 | The ImGui overlay renders through a single `IRenderBackend` seam with the existing D3D9 path behaviorally unchanged — overlay still renders + takes input in a live D3D9 session (verified by live-smoke). | This entire research: the 6-touch-point carve (Pattern 2), the 7-member seam shape (Pattern 1), the D-05 purge (Pitfall 5), the no-Reset preservation (Pitfall 3), the D-06/D-07 harness (Code Examples), the live-smoke gate (D-08). |

**Forward-compatibility (Phase 19 consumers — read but NOT implemented here):**
- **RNDR-02** (`Dx11Backend` hooks DXGI swapchain `Present`) — confirms `newFrame`/`renderDrawData`
  are the right per-frame seam crossings (the Dx11 backend will route `ImGui_ImplDX11_NewFrame/RenderDrawData`
  through the same two members). [VERIFIED: REQUIREMENTS.md:39-40]
- **RNDR-03** (exactly one backend per session, auto-detected from `gl%02d_r.dll`, one-shot diag log) —
  confirms D-01's "single global instance selected at `setup()`" is the right shape; Phase 19 swaps the
  selection, not the interface. [VERIFIED: REQUIREMENTS.md:41-42]
- **RNDR-04** (overlay survives resize under D3D11 — RTV released/recreated inside `ResizeBuffers`, the
  DXGI analog of the forbidden D3D9 `Reset`) — confirms `onPreResize`/`onPostResize` (D-03 no-ops here)
  are the correct home for Dx11 resize work, and that the scene-depth accessor must be an API-neutral
  handle (Dx11's depth SRV ≠ D3D9 texture). [VERIFIED: REQUIREMENTS.md:43-44]
</phase_requirements>

## Summary

Phase 18 carves a single runtime-polymorphic `IRenderBackend` vtable out of the existing
ImGui-on-D3D9 overlay so the ~1005-line API-neutral logic in `imgui_impl.cpp` is single-sourced and a
`Dx11Backend` twin can be added in Phase 19 without re-touching `imgui_impl`. This is a **pure
refactor** — the live D3D9 behavior must be preserved verbatim and is the locked success criterion.
Every fact below was verified by reading the actual source in this working tree (file + line). No
external library research was required; the seam is internal architecture.

The carve is small and well-bounded. `imgui_impl.cpp` has exactly **six DX9 touch-points** that must
move behind the seam: three `ImGui_ImplDX9_*` calls (`_Init` line 274, `_NewFrame` line 419,
`_RenderDrawData` line 562) plus three `directX::getDepthTexture()` reach-ins (lines 349, 376, 489),
plus the `IDirect3DDevice9* pDevice` parameter of `setup()` (line 257) and its
`D3DDEVICE_CREATION_PARAMETERS` / `hFocusWindow` extract (lines 262-269). The `imgui_impl.h` header
also carries `#include <d3d9.h>` (line 27) and `IDirect3DDevice9*` in the `setup()` decl (line 38) —
both must be purged. The `directX::` namespace in `directx9.cpp` (device, 7 vtable detours + 1 guarded
compileShader detour, Present-block, depth-texture, wireframe) becomes the body of `Dx9Backend`.

The verification harness has a strong existing precedent:
`UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp` (Phase 15 / RESID-04 / D-13) is a comment-stripping
source grep-gate that is the exact shape D-06 needs, and it already runs in the native Catch2 CI lane.
Its `repoRootFromThisFile()` / `readFile()` / `stripComments()` / per-shape `countDeviceResetInvocations()`
helpers (lines 69-170) clone directly for the D-06 API-neutrality gate.

**Primary recommendation:** Define `IRenderBackend` in `render_backend.h` with the 7 members; implement
`Dx9Backend` in `render_backend.cpp` that forwards to the existing `directX::` free functions verbatim
(do NOT rewrite the detour/device internals — wrap them); expose a file-scope `IRenderBackend*` selected
via a `set()`/`get()` pair in `setup()`; route `imgui_impl`'s six touch-points through it; purge
`<d3d9.h>` / `IDirect3DDevice9` / `ImGui_ImplDX9_*` from `imgui_impl.{cpp,h}`; add both verification
layers (D-06 source gate + D-07 Catch2 mock-dispatch test) to the native suite.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| API-neutral overlay (WndProc subclass, Issue #11 routing, RT-space input, gizmo, renderCallbacks bus) | `imgui_impl.cpp` | — | Behaviorally identical across any graphics API; stays single-sourced (D-05 full purge) |
| Per-frame backend dispatch (`newFrame`/`renderDrawData`) | `IRenderBackend` vtable | `imgui_impl::render()` calls through it | The only per-frame seam crossings; must be heap-free (plain virtual call) |
| D3D9 device + 7 vtable detours + compileShader detour + Present-block + wireframe + depth-resolve | `Dx9Backend` (wraps `directX::`) | `directx9.cpp` free fns | All DX9-specific; lives behind the seam (D-05) |
| Render-target dimensions (present-stretch math) | `imgui_impl.cpp` (reads `Graphics::getCurrentRenderTargetWidth/Height`) | seam exposes as `renderTargetWidth/Height` | D-04: RT-space math stays in the API-neutral path; the seam just surfaces it |
| Scene-depth texture (gizmo / depth+color preview windows) | `IRenderBackend::sceneDepthTexture()` (API-neutral handle) | `Dx9Backend` returns the D3D9 texture as `ImTextureID` | D-05: gizmo logic must not see `directX::DepthTexture` directly |
| Resize (no-op under D3D9, `ResizeBuffers` under D3D11) | `IRenderBackend::onPreResize`/`onPostResize` | `Dx9Backend` = honest no-ops | D-03: exist only so Phase 19 Dx11 has a home; D3D9 never Resets |

## Standard Stack

No new external libraries. This phase uses only what is already vendored and building:

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dear ImGui | 1.92.x (vcpkg, `dx9-binding`) | overlay UI + `imgui_impl_dx9` / `imgui_impl_win32` backends | already the overlay engine; the DX9 backend bindings are what move behind the seam [VERIFIED: vcpkg_installed + AGENTS.md] |
| DetourXS | vendored (`external/`) | the 7 D3D9 vtable hooks + compileShader (`Detour::Create`, `DETOUR_TYPE_PUSH_RET`) | already installs every Utinni hook; unchanged by the carve [VERIFIED: directx9.cpp:545-597] |
| Catch2 | vcpkg (`Catch2.lib`/`Catch2Main.lib` Release, `Catch2d`/`Catch2Maind` Debug) | D-06 source-gate + D-07 mock-dispatch test | the native test suite (`UtinniCore.Tests`) already links it and runs in CI [VERIFIED: UtinniCore.Tests.vcxproj:80,99,120] |
| xUnit | net472 (`UtinniCoreDotNet.Tests`) | alternative home for the D-06 source gate (PreservationAudit precedent) | the `PreservationAuditTests.cs` grep-Fact pattern is the closest managed analog to D-06 [VERIFIED: file exists] |

**Installation:** none — no package changes. `render_backend.{h,cpp}` are new source files added to the
existing `UtinniCore.vcxproj`. [VERIFIED: no vcpkg manifest change required]

**Version verification:** N/A — no package added. ImGui/DetourXS/Catch2 versions are already locked by
the existing build (AGENTS.md: imgui 1.92.x via vcpkg manifest mode). [VERIFIED: AGENTS.md toolchain section]

## Package Legitimacy Audit

Not applicable — this phase installs **zero** external packages. It is an internal C++ refactor using
already-vendored dependencies (ImGui, DetourXS, Catch2). No `npm`/`pip`/`cargo`/`vcpkg` manifest change.
slopcheck and registry verification are inapplicable (no new package names introduced).

## Architecture Patterns

### System Architecture Diagram (post-carve data flow)

```
SWG render thread
      │
      ▼
directX::hkPresent(pDevice)                    [directx9.cpp:265 — UNCHANGED detour body]
      │  ├─ (first fire) captures pDirectXDevice (line 251, via hkBeginScene/hkPresent)
      │  ├─ imgui_impl::render()  ───────────────┐  (line 325)
      │  ├─ present(...) unless blockPresentCall  │  (line 332 — no Reset, windowed stretch)
      │  ├─ depthTexture->createTexture(...)      │  (line 357)
      │  └─ imgui_impl::setup(...)  ──────────────┼──┐ (line 361, one-shot under isSetup)
      │                                            │  │
      ▼                                            ▼  ▼
imgui_impl::render() (line 401)              imgui_impl::setup(...) (line 257)
      │  newFrame  ─────────────┐  (line 419)      │ select g_backend = the Dx9 singleton
      │  ImGui_ImplWin32_NewFrame│  (line 420)      │ g_backend->init(window)  [self-sources device]
      │  RT-space input map      │  (lines 447-463) │ (no ImGui_ImplDX9_Init visible here post-carve)
      │  build UI (gizmo, cbs)   │                  │
      │  sceneDepth preview ─────┼──┐ (349/376/489) │
      │  renderDrawData  ────────┘  │  (line 562)    │
      ▼                              ▼                ▼
  g_backend (IRenderBackend*) ──────────────────► Dx9Backend  [render_backend.cpp]
      newFrame()                                  → ImGui_ImplDX9_NewFrame()
      renderDrawData(drawData)                    → ImGui_ImplDX9_RenderDrawData(drawData)
      sceneDepthTexture() : ImTextureID           → (ImTextureID)directX::getDepthTexture()->getTextureDepth()
      renderTargetWidth()/Height()                → Graphics::getCurrentRenderTargetWidth/Height()
      onPreResize()/onPostResize()                → {} no-op (D-03)
                                                  (device/detours/present-block stay in directX::)
```

The diagram's load-bearing point: **only `imgui_impl::render()` and `imgui_impl::setup()` cross the
seam.** Everything else in `imgui_impl.cpp` (WndProc subclass lines 144-254, Issue #11 routing, RT-space
mouse map lines 447-463, gizmo namespace lines 631-1005, renderCallbacks bus lines 121-123 + 573-606) is
already API-neutral and does not move.

### Recommended file layout

```
UtinniCore/swg/graphics/
├── render_backend.h     # NEW — IRenderBackend pure-virtual ABC (the seam) + get()/set()
├── render_backend.cpp   # NEW — Dx9Backend impl (forwards to directX::) + the static singleton
├── directx9.{cpp,h}     # directX:: free fns: device/detour/present-block/depth/wireframe (Dx9Backend's body)
└── depth_texture.{cpp,h}# RESZ/INTZ/NVAPI depth-resolve — stays DX9-only, reached only via the seam accessor
UtinniCore/swg/ui/
├── imgui_impl.cpp       # API-neutral only after purge; includes render_backend.h, NOT d3d9.h/imgui_impl_dx9.h
└── imgui_impl.h         # drop `#include <d3d9.h>` (line 27); setup() loses its IDirect3DDevice9* param (line 38)
UtinniCore.Tests/Graphics/
├── RenderBackendSeamTests.cpp  # NEW — D-07 mock IRenderBackend dispatch test (Catch2)
├── ImguiApiNeutralityTests.cpp # NEW — D-06 source gate (Catch2)
└── (optional) SourceGateUtil.h # NEW — shared stripComments/readFile/repoRootFromThisFile helpers
```

### Pattern 1: Pure-virtual ABC with file-scope global instance (D-01)

**What:** `IRenderBackend` is an abstract base with 7 pure-virtual members; one global `IRenderBackend*`
is set once in `setup()`. Phase 19 adds `Dx11Backend : IRenderBackend` and selects it instead.

**When to use:** the seam must be runtime-polymorphic NOW (D-01 rejected concrete-now/abstract-in-19),
so a vtable from day one is the locked shape.

**Example (seam shape — grounded in the six touch-points + D-05 accessor):**
```cpp
// render_backend.h  — NO <d3d9.h>; this header is what imgui_impl.cpp includes instead.
#pragma once
#include <imgui.h>   // for ImTextureID + ImDrawData (already in imgui_impl's include graph)

namespace render_backend
{
class IRenderBackend
{
public:
    virtual ~IRenderBackend() = default;

    // Per-frame (hot path — plain virtual call, zero heap; see Pitfall 1)
    virtual void newFrame() = 0;                            // → ImGui_ImplDX9_NewFrame() (imgui_impl.cpp:419)
    virtual void renderDrawData(ImDrawData* drawData) = 0;  // → ImGui_ImplDX9_RenderDrawData(...) (562)

    // Resize hooks (D-03: honest no-ops in Dx9Backend; Dx11 ResizeBuffers home in Ph19/RNDR-04)
    virtual void onPreResize() = 0;
    virtual void onPostResize() = 0;

    // Render-target dimensions (D-04: sourced from present-stretch math in imgui_impl)
    virtual int renderTargetWidth() = 0;
    virtual int renderTargetHeight() = 0;

    // D-05 7th member: API-neutral scene-depth accessor for the depth/color preview windows.
    // ImTextureID is imgui's opaque handle (void*-sized) — the lowest-common-denominator type
    // that Dx11Backend ALSO satisfies in Ph19 (an ID3D11ShaderResourceView*). RNDR-04 confirms
    // the depth handle differs by API, so a neutral handle is the right shape. Returns 0/nullptr
    // when no depth texture is live (matches today's null guards at imgui_impl.cpp:350/377/490).
    virtual ImTextureID sceneDepthTexture() = 0;
};

// D-01: single global instance selected at setup(). A free setter keeps it
// test-seam-friendly (D-07) without exposing the concrete type.
IRenderBackend* get();
void set(IRenderBackend* backend);   // setup() calls this with the Dx9 singleton
} // namespace render_backend
```

```cpp
// render_backend.cpp  — the ONLY new file that includes the DX9 bindings.
#include "render_backend.h"
#include <imgui_impl_dx9.h>
#include "directx9.h"          // directX:: free fns (device/depth/etc.)
#include "graphics.h"

namespace render_backend
{
class Dx9Backend final : public IRenderBackend
{
public:
    void newFrame() override { ImGui_ImplDX9_NewFrame(); }
    void renderDrawData(ImDrawData* d) override { ImGui_ImplDX9_RenderDrawData(d); }
    void onPreResize() override {}   // D-03: D3D9 never Resets — honest no-op
    void onPostResize() override {}  // D-03
    int renderTargetWidth() override  { return utinni::Graphics::getCurrentRenderTargetWidth(); }
    int renderTargetHeight() override { return utinni::Graphics::getCurrentRenderTargetHeight(); }
    ImTextureID sceneDepthTexture() override
    {
        auto* t = directX::getDepthTexture();
        if (t == nullptr || t->getTextureColor() == nullptr) return (ImTextureID)0;
        return (ImTextureID)t->getTextureDepth();   // matches imgui_impl.cpp:364 cast today
    }
};

static Dx9Backend s_dx9Backend;     // static-storage singleton — no heap
static IRenderBackend* s_active = nullptr;
IRenderBackend* get() { return s_active; }
void set(IRenderBackend* b) { s_active = b; }
Dx9Backend* dx9Singleton() { return &s_dx9Backend; }  // setup() uses this for selection
} // namespace render_backend
```
[ASSUMED] — the exact member signatures (`ImTextureID` vs `void*`, whether `init()` is a member, the
`namespace render_backend` name) are a planning decision; the shape above is the minimal
forward-compatible form derived from RNDR-02/03/04 + the six verified touch-points. The user/planner
should confirm before locking.

### Pattern 2: The seam-crossing edits in imgui_impl.cpp (exactly six lines today — all VERIFIED)

| Current line | Today (verified) | Post-carve |
|--------------|------------------|-----------|
| `imgui_impl.cpp:274` | `ImGui_ImplDX9_Init(pDevice);` (in `setup`) | move into `Dx9Backend` init; `setup()` selects backend |
| `imgui_impl.cpp:419` | `ImGui_ImplDX9_NewFrame();` (in `render`) | `render_backend::get()->newFrame();` |
| `imgui_impl.cpp:562` | `ImGui_ImplDX9_RenderDrawData(ImGui::GetDrawData());` | `render_backend::get()->renderDrawData(ImGui::GetDrawData());` |
| `imgui_impl.cpp:349` | `auto depthTex = directX::getDepthTexture();` (`DrawDepthWindow`) | use `render_backend::get()->sceneDepthTexture()` |
| `imgui_impl.cpp:376` | `auto colorTex = directX::getDepthTexture();` (`DrawColorWindow`) | same accessor |
| `imgui_impl.cpp:489` | `auto depthTex = directX::getDepthTexture();` (`Tests` window) | same accessor (or drop — see below) |

[VERIFIED: all six lines read in source this session.]

**Important nuance on the three depth reach-ins (349/376/489):** they also call
`depthTex->getTextureDepth()` / `->getTextureColor()` / `->getStage()` / `->setStage()`
(lines 350, 364, 377, 390, 490, 499-502). The D-05 purge means `imgui_impl` must not see
`directX::DepthTexture` at all. Specifics:
- **`DrawDepthWindow` (347-372)** uses `getTextureColor()` (null guard, line 350) + `getTextureDepth()`
  (the `AddImage` cast, line 364). Both reachable through the seam if `sceneDepthTexture()` returns the
  depth handle and the null guard is folded into the accessor (return 0 when color tex is null).
- **`DrawColorWindow` (374-397)** uses `getTextureColor()` for both the guard (377) and the image (390).
  This needs a **second** API-neutral accessor (`sceneColorTexture()`) OR the planner folds color into
  the existing seam. **Research note:** the ROADMAP locked 6+1 = 7 members. If `DrawColorWindow` must
  survive, either (a) add an 8th `sceneColorTexture()` member, or (b) drop the color window (it is
  dev-only diagnostic UI, see below). This is a **planning decision** the discuss-phase did not resolve.
  [ASSUMED — flag for planner.]
- **`Tests` window (480-515, behind `if (!enableUi)`)** reaches `getDepthTexture()` (489) and the
  depth-stage slider `getStage`/`setStage` (499-503). This block runs ONLY when `enableUi == false`
  (a dev/diagnostic config; Release `ut.ini` sets `enableInternalUi=true`, so the block is skipped —
  verified at imgui_impl.cpp:480 + 557). **Research recommendation:** the planner should decide whether
  to (a) drop the depth/color/stage diagnostic UI entirely (smallest, behavior-preserving for Release),
  or (b) preserve it via API-neutral seam accessors (`sceneDepthTexture` + `sceneColorTexture` + a
  neutral stage getter/setter). Option (a) is cleaner and matches "behaviorally unchanged" for the
  shipped config; option (b) preserves the dev sliders. This is a **Claude's-discretion detail** under
  D-05. [ASSUMED — flag for planner.]

### Pattern 3: setup() device acquisition without imgui_impl seeing IDirect3DDevice9 (research Q2/Q3)

Today `setup(IDirect3DDevice9* pDevice)` (imgui_impl.cpp:257) does three DX9 things:
`pDevice->GetCreationParameters(&cParam)` to extract `hFocusWindow` (line 264), publishes it via
`Client::setSwgHwnd(cParam.hFocusWindow)` (line 269), and `ImGui_ImplDX9_Init(pDevice)` (line 274).
After the D-05 purge, `imgui_impl` cannot take an `IDirect3DDevice9*` or call `GetCreationParameters`.

**Recommended approach (backend self-sources the device):** `Dx9Backend` already has access to the
device via `directX::getDevice()` (returns the captured `pDirectXDevice`, verified directx9.cpp:662).
The window handle is the only thing `imgui_impl` still needs for `ImGui_ImplWin32_Init` (line 273) +
the WndProc subclass install (line 281). Two viable shapes:

1. **`setup()` takes no DX9 type.** The backend's `init()` does `GetCreationParameters` +
   `ImGui_ImplDX9_Init` internally (it has the device), and returns/publishes `hFocusWindow`.
   `imgui_impl::setup()` then calls `ImGui_ImplWin32_Init(hwnd)` (line 273) + installs the WndProc
   subclass (line 281) with that window. This keeps `setup()`'s signature API-neutral. `HWND` is a
   Win32 type, not a DX9 type — it does NOT trip the D-05 gate (gate is on `IDirect3DDevice9` /
   `<d3d9.h>` / `ImGui_ImplDX9_`).
2. **Caller passes HWND.** `directX::hkPresent` (line 361) currently calls `imgui_impl::setup(pDevice)`.
   It could instead extract the window and call `imgui_impl::setup(hwnd)` after `g_backend->init()`.

Approach 1 is cleaner: the device-bearing logic (`GetCreationParameters`, `ImGui_ImplDX9_Init`) lives
entirely inside the backend; `imgui_impl` deals only in `HWND`. [ASSUMED] — confirm the exact `init()`
signature in planning. **Critical:** `Client::setSwgHwnd(hFocusWindow)` MUST still happen (it is
consumed managed-side by PanelGame reparenting — verified imgui_impl.cpp:269 comment "Issue #10 Phase A").

### Anti-Patterns to Avoid
- **`std::function` in the seam.** Do NOT model the seam as `std::function` callbacks — that adds a
  type-erasure allocation/indirection bus on the per-frame path. Use a plain pure-virtual vtable
  (see Pitfall 1).
- **Rewriting `directx9.cpp` internals.** The 7 vtable detours (directx9.cpp:545-563), the guarded
  compileShader detour (576-613), the dummy-device vtable harvest (`getVtbl`, 435-526), the Present-block
  (329-343), and the `hkReset` pass-through (365-378) are live-verified and fragile. **Wrap, don't
  rewrite.** `Dx9Backend` should forward to existing `directX::` free functions verbatim.
- **Introducing a `Reset` call.** Success criterion #4. The only `Reset` is SWG's own, flowing through
  `hkReset`'s free-function pass-through `reset(pDevice, ...)` (directx9.cpp:374, lowercase `reset`).
  Never add `->Reset(` / `.Reset(`. (`NoDeviceResetTests.cpp` gates this.)
- **Gating the D-06 grep on the bare string "D3D9".** Source comments mention D3D9/DX9 constantly
  (imgui_impl.cpp:52-55, 422-446 reference D3D9/backbuffer/Reset). Gate on concrete symbol forms only
  (see D-06 / Pitfall 5). [VERIFIED: feedback_gsd_grep_gate_hygiene]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Comment-stripping source grep-gate (D-06) | a fresh regex stripper | clone `NoDeviceResetTests.cpp`'s `stripComments`(106-145) + `repoRootFromThisFile`(69-87) + `readFile`(89-96) | already live, CI-running, handles `//` + `/* */`, has a self-check SECTION (174-188) proving the stripper works |
| Repo-root discovery in a managed gate | hard-coded paths | `PreservationAudit/RepoRoot.cs` helpers | walks up to `Utinni.sln`; works locally + on the self-hosted CI runner; excludes build/vendored trees |
| D3D9 vtable harvest / detour install | re-implement in Dx9Backend | keep `directX::detour()`(528-616) / `getVtbl()`(435-526) verbatim | live-verified dummy-device approach (2026-05-19, directx9.cpp:425 comment); modern-Windows-safe; touching it risks the overlay |
| Depth resolve (RESZ/INTZ/NVAPI) | re-implement behind seam | keep `DepthTexture` as-is, expose only `sceneDepthTexture()` | RESZ depth-resolve is hardware-path-specific and verified; the seam only surfaces the handle |

**Key insight:** Phase 18 is a *move*, not a *rewrite*. The highest-value discipline is wrapping the
existing live-verified DX9 code unchanged behind the vtable, so the only new behavior is one virtual
indirection per frame.

## Runtime State Inventory

This is a code-only refactor (no rename of stored data, no service config, no OS-registered state).
Walking the five categories explicitly:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — verified: no datastore keys/IDs reference `imgui_impl` or `directX` symbol names | none |
| Live service config | None — verified: the overlay is in-process; no external service holds these symbol names | none |
| OS-registered state | None — verified: no Task Scheduler / pm2 / service registration references these symbols | none |
| Secrets/env vars | None — verified: no env var or SOPS key names reference render symbols | none |
| Build artifacts | `UtinniCore.vcxproj` lists `depth_texture.cpp`(167), `imgui_impl.cpp`(208), `directx9.cpp`(210) + includes `depth_texture.h`(243), `imgui_impl.h`(287), `directx9.h`(289). Adding `render_backend.{h,cpp}` requires one `<ClCompile>` + one `<ClInclude>` entry. **No `.vcxproj.filters` file exists** (verified by glob — zero matches) — no filters edit needed. CppSharp regen (`Generated/UtinniCore.cs`) reorders on build — `git checkout --` it, never commit. | add 2 vcxproj entries; do not commit Generated churn |

**Key carve concern — symbol references that move (the analog of "runtime state" for a code carve):**
- `imgui_impl::isRendering()` is called from `directx9.cpp:382,386` (`hkDrawIndexedPrimitive` wireframe
  gate, verified). This is a cross-direction call (directX→imgui_impl) and is API-neutral — it stays and
  is unaffected by the carve.
- `directX::getDevice()`(662), `getDepthTexture()`(637), `blockPresent()`(647), `toggleWireframe()`(642),
  `isPresentBlocked()`(657) are `UTINNI_API`-exported (directx9.h:38-41) and consumed by managed code /
  plugins. The carve must NOT change these exported signatures (they are the public façade). `Dx9Backend`
  *wraps* them; it does not replace them. Verify no exported symbol is removed (would break pre-built
  plugins — `[[feedback_caller_attrs_binary_compat]]`, CPPS-04 ABI gate). [VERIFIED: directx9.h]
- `imgui_impl::setup()` / `render()` / `isRendering()` are NOT `UTINNI_API`-exported (imgui_impl.h:38-40,
  plain `extern`) — they are internal to UtinniCore, called from `directx9.cpp` only. Changing `setup()`'s
  signature is therefore ABI-safe (no managed/plugin consumer). [VERIFIED: imgui_impl.h:38-40]

## Common Pitfalls

### Pitfall 1: Assuming the vtable call needs allocation hardening
**What goes wrong:** Over-engineering the per-frame dispatch (snapshot vectors, `std::function`,
`shared_ptr`) out of fear of the heap-free hot-path rule (`[[project_rh_snapshot_no_heap_alloc]]`).
**Why it happens:** The R-H lesson burned in: a per-frame `std::vector::reserve()` in callback dispatch
fragmented SWG's allocator and crashed scene change at `0x0051fb0a`. (That dispatch is now the
stack-snapshot `dispatchSnapshot` template, imgui_impl.cpp:66-113.)
**How to avoid:** A plain virtual call through a static-storage global (`g_backend->newFrame()`) is a
single pointer-indirection + call. **Zero heap allocation.** It fully satisfies the heap-free rule. The
R-H concern was about per-frame *container allocation*, not virtual dispatch. Keep the backend as a
static-storage singleton (not `new`'d per frame, not a `shared_ptr`).
**Warning signs:** any `std::function`, `make_unique`, or container in the seam's per-frame path.

### Pitfall 2: Breaking the utinni_init / detour install ordering (research Q4)
**What goes wrong:** Moving `initPresentBlockedEvent()` / `initDepthTexture()` into `Dx9Backend`
construction and accidentally running them after detours install, reintroducing the CR-04/WR-03 race.
**Why it happens:** The ordering is subtle and split across three files.
**The verified current ordering (do NOT disturb):**
1. `utinni.cpp` (`utinni_init`, lines 367-375): `directX::initPresentBlockedEvent()`(371) →
   `directX::initDepthTexture()`(372) → **then** `createDetours()`(375). The comment (utinni.cpp:368)
   is explicit: "These calls are BEFORE createDetours() so hkPresent cannot fire before they complete."
   [VERIFIED]
2. `directX::detour()` is **NOT** called from `createDetours()`. It is called from `graphics.cpp:611`
   inside `hkInstall` (the hook on SWG's `graphics::install`), which fires later on the render thread
   when SWG brings up its graphics subsystem. `createDetours()` installs the `Graphics::detour()` hook
   (`Detour::Create(swg::graphics::install, hkInstall, ...)`, graphics.cpp:735) that *enables*
   `hkInstall` to fire. [VERIFIED: graphics.cpp:588-616, 733-735]
3. `imgui_impl::setup(pDevice)` is called from `hkPresent` (directx9.cpp:361), one-shot under `isSetup`
   (imgui_impl.cpp:259). [VERIFIED]
**How to avoid:** Keep `initPresentBlockedEvent` / `initDepthTexture` as `directX::` free functions
called from `utinni_init` in the SAME order. Do NOT fold them into `Dx9Backend`'s constructor (which,
if the backend were constructed lazily in `setup()`, would run them on the render thread AFTER detours —
the exact race CR-04/WR-03 eliminated). The `Dx9Backend` singleton is static-storage and its constructor
must stay trivial; the eager init stays where it is.
**Warning signs:** `initPresentBlockedEvent`/`initDepthTexture` moving out of `utinni_init`'s pre-detour
block (utinni.cpp:371-375).

### Pitfall 3: The hkReset detour vs the no-Reset contract (research Q5/Q6)
**What goes wrong:** Misreading `hkReset` (directx9.cpp:365) + the `reset` detour install
(directx9.cpp:553-554) as "Utinni calls Reset," and either removing it or treating the no-op resize
hooks as needing to wire into it.
**Why it happens:** There IS a `Reset` *detour* — but it intercepts **SWG's own** Reset; it does not
*initiate* one. The pass-through `reset(pDevice, pPresentationParameters)` (line 374) is a free-function
call through the captured original vtable pointer (lowercase `reset`), invalidating/recreating imgui
device objects around SWG's Reset (`ImGui_ImplDX9_InvalidateDeviceObjects`(373) / `_CreateDeviceObjects`(375)).
This is correct and live-verified. [VERIFIED: directx9.cpp:365-378]
**How to avoid (D-03):** `Dx9Backend::onPreResize`/`onPostResize` are **honest no-ops** — they do NOT
call `hkReset`, do NOT touch the device, do NOT wire to the `reset` detour. The no-Reset contract
(success criterion #4) is preserved because: (a) no `->Reset(`/`.Reset(` invocation exists today
(verified by `NoDeviceResetTests.cpp` SECTION at line 190-196, which passes), and (b) the windowed
stretch path in `hkPresent` (line 332, `present(...)`) plus the RT-space mapping in
imgui_impl.cpp:447-463 are untouched by the carve. The existing `NoDeviceResetTests.cpp` continues to
gate this. **Note:** the `ImGui_ImplDX9_InvalidateDeviceObjects/CreateDeviceObjects` calls in `hkReset`
live in `directx9.cpp` (not `imgui_impl.cpp`), so the D-05 purge does NOT need to touch them — they stay
in the DX9 tier where they belong. The planner may optionally route them through the backend, but it is
not required by D-05 (D-05 purges `imgui_impl`, not `directx9.cpp`).
**Warning signs:** any new `->Reset(` in `render_backend.cpp`; `onPreResize`/`onPostResize` containing
anything but `{}`.

### Pitfall 4: Issue #11 chat-context routing / RT-space input accidentally moving (research Q9)
**What goes wrong:** Sweeping "DX9-looking" code out of `imgui_impl` and catching the WndProc subclass
or RT-space mouse map, breaking the live-verified Issue #11 fix.
**Why it happens:** The RT-space block (imgui_impl.cpp:447-463) reads `Graphics::getCurrentRenderTargetWidth/Height`
which *sounds* graphics-API-ish — but `Graphics::` is the API-neutral SWG façade, not DX9.
**How to avoid:** The following are ALL API-neutral and STAY in `imgui_impl.cpp` untouched (verified):
- `hkWndProcHandler` (lines 144-254) — the WndProc subclass, `ImGui_ImplWin32_WndProcHandler` forward-decl
  (line 55), Issue #11 VK_RETURN/VK_ESCAPE/F11/F12 diagnostics, `forceOpenChatInputFromCpp` (line 187).
- RT-space input map (lines 447-463) — `io.AddMousePosEvent` (line 459, imgui 1.87+ event-queue, per
  `[[feedback_imgui_embedded_d3d9_rt_space]]`), `GetCursorPos`/`ScreenToClient` (line 457), `io.DisplaySize`(461).
- `originalWndProcHandler = SetWindowLongPtr(...)` (line 281) — but note this needs the HWND, which
  `setup()` still receives (as HWND, not as a device — see Pattern 3).
- The `DirectInput::suspend/resume` capture-arbitration (lines 537-550).
- `ImGui_ImplWin32_Init` (line 273) + `ImGui_ImplWin32_NewFrame` (line 420) — **Win32, not DX9**; they
  STAY in `imgui_impl`. The D-06 gate is on `ImGui_ImplDX9_`, which does not match `ImGui_ImplWin32_`.
None of these reference `IDirect3DDevice9`, `<d3d9.h>`, or `ImGui_ImplDX9_*`, so the D-06 gate naturally
leaves them in place. [VERIFIED]
**Warning signs:** WndProc subclass or `AddMousePosEvent` block appearing in `render_backend.cpp`.

### Pitfall 5: D-06 grep false-trips on comments (research Q7)
**What goes wrong:** Gating on `"D3D9"` or `"DX9"` fires on the dozens of rationale comments in
`imgui_impl.cpp` (lines 52-55, 422-446 reference D3D9/backbuffer/Reset). [VERIFIED — those comments exist]
**How to avoid:** Gate ONLY on concrete symbol forms that cannot appear in prose:
- `#include <d3d9.h>` (and `#include "d3d9.h"`)
- `IDirect3DDevice9`
- `ImGui_ImplDX9_` (prefix — catches `_Init`/`_NewFrame`/`_RenderDrawData`/`_InvalidateDeviceObjects`/`_CreateDeviceObjects`)
- `directX::` (the namespace reach-in — catches `getDepthTexture`/`getDevice` calls)
Strip comments first (clone `NoDeviceResetTests.cpp`'s `stripComments`, lines 106-145), so even a comment
that happens to contain `IDirect3DDevice9` does not false-trip. Include a self-check SECTION (as
`NoDeviceResetTests.cpp` does at lines 174-188) proving the stripper removes a planted-in-comment token.
**Warning signs:** the gate pattern containing the bare substring `D3D9` or `DX9`.

## Code Examples

### D-06 API-neutrality source gate (Catch2, clone of NoDeviceResetTests pattern)
```cpp
// UtinniCore.Tests/Graphics/ImguiApiNeutralityTests.cpp  (NEW — D-06)
// Reuses the stripComments / readFile / repoRootFromThisFile helpers proven in
// NoDeviceResetTests.cpp (lines 69-170). Gate on concrete DX9 symbol forms ONLY (grep-gate hygiene).
#include <catch2/catch_all.hpp>
// ... (stripComments, readFile, repoRootFromThisFile — copied or shared via SourceGateUtil.h) ...

namespace
{
int countSubstr(const std::string& hay, const std::string& needle)
{
    int n = 0; size_t pos = 0;
    while ((pos = hay.find(needle, pos)) != std::string::npos) { ++n; pos += needle.size(); }
    return n;
}
int gatedCount(const std::string& rel, const std::string& needle)
{
    const std::string code = stripComments(readFile(repoRootFromThisFile() + "/" + rel));
    return countSubstr(code, needle);
}
} // namespace

TEST_CASE("D-06 imgui_impl is fully DX9-API-neutral after the carve", "[rndr01][graphics]")
{
    SECTION("stripper hygiene self-check")  // mirror NoDeviceResetTests:174
    {
        const std::string s = "int x; // IDirect3DDevice9 in a comment\n#include <d3d9.h> // also comment\n";
        REQUIRE(countSubstr(stripComments(s), "IDirect3DDevice9") == 0);   // comment stripped
        REQUIRE(countSubstr(s, "IDirect3DDevice9") == 1);                  // proves stripper works
    }
    SECTION("imgui_impl.cpp")
    {
        REQUIRE(gatedCount("UtinniCore/swg/ui/imgui_impl.cpp", "#include <d3d9.h>") == 0);
        REQUIRE(gatedCount("UtinniCore/swg/ui/imgui_impl.cpp", "IDirect3DDevice9")  == 0);
        REQUIRE(gatedCount("UtinniCore/swg/ui/imgui_impl.cpp", "ImGui_ImplDX9_")    == 0);
        REQUIRE(gatedCount("UtinniCore/swg/ui/imgui_impl.cpp", "directX::")         == 0);
    }
    SECTION("imgui_impl.h")
    {
        REQUIRE(gatedCount("UtinniCore/swg/ui/imgui_impl.h", "#include <d3d9.h>") == 0);
        REQUIRE(gatedCount("UtinniCore/swg/ui/imgui_impl.h", "IDirect3DDevice9")  == 0);
    }
}
```

### D-07 mock IRenderBackend dispatch test (Catch2, no live device — research Q8)
```cpp
// UtinniCore.Tests/Graphics/RenderBackendSeamTests.cpp  (NEW — D-07)
#include <catch2/catch_all.hpp>
#include "swg/graphics/render_backend.h"

namespace
{
struct MockBackend final : render_backend::IRenderBackend
{
    int newFrameCalls = 0, renderDrawCalls = 0, preResize = 0, postResize = 0;
    int rtwCalls = 0, rthCalls = 0, depthCalls = 0;
    void newFrame() override { ++newFrameCalls; }
    void renderDrawData(ImDrawData*) override { ++renderDrawCalls; }
    void onPreResize() override { ++preResize; }
    void onPostResize() override { ++postResize; }
    int renderTargetWidth() override  { ++rtwCalls; return 1920; }
    int renderTargetHeight() override { ++rthCalls; return 1080; }
    ImTextureID sceneDepthTexture() override { ++depthCalls; return (ImTextureID)0; }
};
} // namespace

TEST_CASE("D-07 seam routes all 7 members through the installed backend", "[rndr01][graphics]")
{
    MockBackend mock;
    render_backend::set(&mock);                        // D-01 setter keeps it test-friendly
    REQUIRE(render_backend::get() == &mock);

    render_backend::get()->newFrame();
    render_backend::get()->renderDrawData(nullptr);
    render_backend::get()->onPreResize();
    render_backend::get()->onPostResize();
    REQUIRE(render_backend::get()->renderTargetWidth()  == 1920);
    REQUIRE(render_backend::get()->renderTargetHeight() == 1080);
    render_backend::get()->sceneDepthTexture();

    REQUIRE(mock.newFrameCalls   == 1);
    REQUIRE(mock.renderDrawCalls == 1);
    REQUIRE(mock.preResize       == 1);
    REQUIRE(mock.postResize      == 1);
    REQUIRE(mock.rtwCalls        == 1);
    REQUIRE(mock.rthCalls        == 1);
    REQUIRE(mock.depthCalls      == 1);

    render_backend::set(nullptr);                      // restore for other tests
}
```
**Test-seam note (research Q8):** D-01's "single global instance selected at `setup()`" IS test-friendly
**if** a free `set()` setter exists alongside `get()`. Without a setter the mock can't be installed
without a live device. Recommend exposing `render_backend::set()` (used by `setup()` in production to
install the Dx9 singleton; used by the test to install the mock). This adds no production risk —
`setup()` calls `set(dx9Singleton())` exactly once under its `isSetup` guard.

## Build wiring (research Q10)

- Add to `UtinniCore/UtinniCore.vcxproj`: `<ClCompile Include="swg\graphics\render_backend.cpp" />`
  near line 167/210, and `<ClInclude Include="swg\graphics\render_backend.h" />` near line 243/289.
  [VERIFIED: those neighbor lines exist]
- **No `.vcxproj.filters` edit** — verified by glob that no `UtinniCore.vcxproj.filters` file exists.
- Add to `UtinniCore.Tests/UtinniCore.Tests.vcxproj`: `<ClCompile Include="Graphics\RenderBackendSeamTests.cpp" />`
  and `<ClCompile Include="Graphics\ImguiApiNeutralityTests.cpp" />` near line 135 (where
  `Graphics\NoDeviceResetTests.cpp` is registered). [VERIFIED: line 135]
- `UtinniCore.Tests` already `ProjectReference`s `UtinniCore.vcxproj` with `LinkLibraryDependencies=true`
  (lines 138-146), so the test exe links `render_backend.cpp`'s symbols. `render_backend::set/get` are
  plain (non-`UTINNI_API`) functions — they link via the project reference, no DLL export needed for the
  test. [VERIFIED: vcxproj:138-146]
- Build INLINE on the main tree (worktrees OFF, `[[project_gsd_worktrees_off]]`): VS2026 MSBuild,
  `/p:Configuration=Release /p:Platform=x86`, PlatformToolset v145. Do NOT use `dotnet build`
  (`[[feedback_dotnet_build_msbuild_resources]]`). Run native tests via `bin\Release\UtinniCore.Tests.exe`.
- `UtinniCore.Tests` uses `LanguageStandard=stdcpp17` (verified vcxproj lines 74/89/108) — `render_backend.h`
  must compile under C++17 (no C++20/23 features; also satisfies the CPPS-03 C++23-header CI tripwire).
- **No CppSharp binding regen impact:** `render_backend.{h,cpp}` are NOT consumed managed-side (the seam
  is internal; only the existing `UTINNI_API directX::*` / `imgui_impl::*` exports cross to managed, and
  those signatures are unchanged). The CppSharp `Generated/UtinniCore.cs` reorder-on-build still happens
  (always `git checkout --` it), but no NEW binding surface is added. [VERIFIED: render_backend has no
  UTINNI_API in the proposed shape]

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| d3d9.dll code-pattern scan for vtable | throwaway-device vtable harvest (`getVtbl`) | 2026-05-19 (directx9.cpp:425 comment) | modern-Windows-safe; KEEP verbatim inside Dx9Backend |
| pre-1.87 `io.MouseDown[]`/`io.KeysDown[]` poking | `io.Add*Event()` queue (imgui 1.87+) | imgui 1.92 vendored | RT-space `AddMousePosEvent` (line 459) stays in imgui_impl |
| direct `directX::getDepthTexture()` reach-in | `IRenderBackend::sceneDepthTexture()` accessor | THIS phase | the D-05 purge; gizmo/preview logic becomes API-neutral |

**Deprecated/outdated:** nothing being removed; the carve preserves all current behavior.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `ImTextureID` is the right LCD return type for `sceneDepthTexture()` (Dx11 can satisfy it with `ID3D11ShaderResourceView*` in Ph19) | Pattern 1 | Low — `ImTextureID` IS imgui's documented opaque texture handle for `AddImage`, exactly how it's used at imgui_impl.cpp:364/390. If wrong, Ph19 re-opens the seam. |
| A2 | `DrawColorWindow` (color texture) needs either an 8th `sceneColorTexture()` member or to be dropped; the Tests-window depth-stage slider likewise | Pattern 2 | MEDIUM — the ROADMAP locked 6+1=7 members but the color-preview window needs a color handle too. Planner must resolve: add an 8th member, or drop the dev-only diagnostic windows. Both preserve Release behavior (`enableUi=true` skips the block). |
| A3 | `setup()` should take HWND (not IDirect3DDevice9) post-carve; backend self-sources the device via `directX::getDevice()` | Pattern 3 | Low — `hkPresent` captures `pDirectXDevice` (via hkBeginScene:251) before calling `setup` (361), so the device is live when the backend's init runs. |
| A4 | A free `render_backend::set()` setter is acceptable for D-07 testability | Pattern 1 / Code Examples | Low — setter is called once in production under the `isSetup` guard; no per-frame use. |
| A5 | D-06 + D-07 both belong in the native Catch2 suite (vs managed xUnit) | Validation Architecture | Low — both run in CI; native is the better home because D-07 must `#include render_backend.h` (a C++ header) and link the C++ symbols. D-06 could go either place. |

## Open Questions

1. **D-06 home: native Catch2 vs managed xUnit PreservationAudit?**
   - What we know: both run in CI. D-07 *must* be native (it includes the C++ seam header).
     `NoDeviceResetTests.cpp` (the closest precedent) is native and gates SC#4.
   - What's unclear: whether the maintainer wants D-06 co-located with D-07 (native) or with the other
     `CON-*` structural Facts (managed `PreservationAuditTests.cs`).
   - Recommendation: put BOTH native (co-locate the gate with the test that proves the seam works, reuse
     the `stripComments` helpers already in that folder). The planner/discuss-phase should confirm.

2. **Color-preview + stage-slider disposition (A2).** See Pattern 2. The ROADMAP's 7-member set covers
   depth but not the color texture nor the stage slider. Recommend dropping the dev-only diagnostic
   windows (depth/color/stage are behind `!enableUi`); if the maintainer uses them, add `sceneColorTexture()`
   (8th member) + a neutral stage getter/setter. **Flag for discuss-phase / planner.**

3. **Exact `init()` signature on the backend (A3).** Whether `setup()` becomes `setup()` (no args,
   backend self-sources HWND too) or `setup(HWND)`. Recommend the backend owns `GetCreationParameters`
   + `ImGui_ImplDX9_Init` and publishes HWND via `Client::setSwgHwnd`; planner locks the signature.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| VS2026 MSBuild (v145) | building the carve | ✓ (per AGENTS.md / Phase 17 green) | Dev18 | — |
| Catch2 (vcpkg, x86) | D-06/D-07 native tests | ✓ (linked in UtinniCore.Tests.vcxproj:80/99) | vcpkg x86-windows | — |
| Self-hosted CI runner | gating master on the suites | ✓ (push-only) | C:\actions-runner | — |
| Live SWG client (32-bit SWGEmu/Restoration) | D-08 live-smoke | maintainer-only | — | none (human checkpoint; CI cannot run) |

**Missing dependencies with no fallback:** the D-08 live-smoke is maintainer-only and not
subagent-reachable — by design, not a blocker (the two automated layers are CI's regression net).

## Validation Architecture

Nyquist validation is enabled (no `workflow.nyquist_validation: false` in config.json — verified).
Mapping each success criterion + RNDR-01 to its proof:

### Test Framework
| Property | Value |
|----------|-------|
| Framework (native) | Catch2 (vcpkg `Catch2.lib`/`Catch2Main.lib` Release; `Catch2d`/`Catch2Maind` Debug), C++17 |
| Framework (managed gate option) | xUnit (net472, `UtinniCoreDotNet.Tests`) |
| Config file | `UtinniCore.Tests/UtinniCore.Tests.vcxproj` (no separate Catch2 config; Catch2Main supplies `main()`) |
| Quick run command | `bin\Release\UtinniCore.Tests.exe "[rndr01]"` (tag-filtered) |
| Full suite command | `msbuild Utinni.sln /m /p:Configuration=Release /p:Platform=x86` then `bin\Release\UtinniCore.Tests.exe` |

### Requirements / Success Criteria → Test Map
| Req / Criterion | Behavior | Test Type | Automated Command | File Exists? |
|-----------------|----------|-----------|-------------------|-------------|
| RNDR-01 / SC#2 | seam exists (7 members) + Dx9Backend behind it | unit (mock dispatch) | `bin\Release\UtinniCore.Tests.exe "[rndr01]"` | ❌ Wave 0 (`RenderBackendSeamTests.cpp`) |
| RNDR-01 / SC#3 | imgui_impl single-sourced, no DX9 symbols | structural source-gate | `bin\Release\UtinniCore.Tests.exe "[rndr01]"` | ❌ Wave 0 (`ImguiApiNeutralityTests.cpp`) |
| RNDR-01 / SC#4 | no Utinni-initiated device Reset | structural source-gate | `bin\Release\UtinniCore.Tests.exe "[resid04]"` | ✅ `NoDeviceResetTests.cpp` (passing; continues to gate) |
| RNDR-01 / SC#1 | overlay renders + takes input in live D3D9 | manual live-smoke (D-08) | maintainer-only — CI cannot run | N/A (human checkpoint) |
| (regression) | CON-* preservation Facts unbroken | structural Fact | `dotnet test ... PreservationAudit` | ✅ `PreservationAuditTests.cs` |

### Sampling Rate
- **Per task commit:** `bin\Release\UtinniCore.Tests.exe "[rndr01],[resid04],[graphics]"` (fast, no device)
- **Per wave merge:** full `UtinniCore.Tests.exe` + `dotnet test UtinniCoreDotNet.Tests` (PreservationAudit)
- **Phase gate:** full native + managed suites green in CI, THEN maintainer D-08 live-smoke (the final
  RNDR-01 acceptance — overlay renders + takes input + chat-context routing works in a live D3D9 session).

### Wave 0 Gaps
- [ ] `UtinniCore.Tests/Graphics/RenderBackendSeamTests.cpp` — D-07 mock dispatch (covers SC#2)
- [ ] `UtinniCore.Tests/Graphics/ImguiApiNeutralityTests.cpp` — D-06 source gate (covers SC#3)
- [ ] Shared helper extraction: `stripComments`/`readFile`/`repoRootFromThisFile` currently live in an
      anonymous namespace inside `NoDeviceResetTests.cpp` (lines 63-170). To reuse without ODR clash,
      either (a) lift them into a small `Graphics/SourceGateUtil.h` header, or (b) keep per-file copies
      in distinct anonymous namespaces (they are file-local, so duplication is legal). Recommend (a).
- [ ] vcxproj: add the 2 new test `<ClCompile>` entries + the 2 production-file entries.
- [ ] Framework install: none — Catch2 already linked.

## Security Domain

`security_enforcement` is not configured `false`, so per the default this section is included — but the
applicable surface is minimal. Phase 18 is an in-process refactor of an injected overlay; it adds no
network surface, no input parsing, no auth, no crypto, no file I/O beyond the test source-gate reading
repo files at test time.

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | partial | The WndProc subclass + DirectInput arbitration are UNCHANGED by this carve; no new untrusted input path is introduced |
| V6 Cryptography | no | none |
| V2/V3/V4 Auth/Session/Access | no | not an authenticated surface |

| Threat Pattern | STRIDE | Mitigation |
|----------------|--------|------------|
| Use-after-free on device/render-thread race | Denial of Service | preserve the CR-04/WR-03 eager-init ordering (Pitfall 2) and the `cleanup()` no-delete-on-exit contract (directx9.cpp:618-635) verbatim |
| Destabilizing SWG's third-party device | Tampering / DoS | never introduce `->Reset(` (Pitfall 3); `NoDeviceResetTests.cpp` gates it |
| Plugin-ABI break from changing exported `directX::`/`imgui_impl` symbols | (binary compat) | wrap, don't replace, the `UTINNI_API` exports; rebuild TJT/plugins in lockstep if any exported signature changes (`[[feedback_caller_attrs_binary_compat]]`, CPPS-04 ABI gate) — note `imgui_impl::setup` is NOT exported, so its signature change is ABI-safe |

## Project Constraints (from CLAUDE.md / AGENTS.md)

- **x86 / 32-bit only** throughout; v2.1 is 32-bit (x64 OUT, user-locked 2026-06-14).
- **VS2026 MSBuild, PlatformToolset v145** — NOT `dotnet build` (MSB3823 on WinForms `.resx`).
- **Worktrees OFF** — run build waves INLINE on the main tree.
- **Grep-gate hygiene** — D-06 must gate on concrete symbol forms, strip comments first.
- **CI is self-hosted, push-only** — native Catch2 + managed xUnit + tools lanes gate master.
- **Never commit `Generated/UtinniCore.cs`** — `git checkout --` the CppSharp churn.
- **CON-H code-safety:** no heavy DllMain startup (init stays in `utinni_init`); pattern-scan results
  null-checked; callback lists snapshotted under lock (the `dispatchSnapshot` template — unchanged here).
- **`[[feedback_max_harness]]`** — both verification layers (D-06 gate + D-07 mock) per D-07.
- **Commit trailer:** Co-Authored-By line (per CLAUDE.md standing authority).

## Sources

### Primary (HIGH confidence — read-of-source this session, line numbers verified)
- `UtinniCore/swg/ui/imgui_impl.cpp` (1006 lines) — the carve target; six DX9 touch-points + API-neutral logic
- `UtinniCore/swg/ui/imgui_impl.h` — `#include <d3d9.h>`(27), `setup(IDirect3DDevice9*)`(38), non-exported internals
- `UtinniCore/swg/graphics/directx9.cpp` (667 lines) — `directX::` namespace (Dx9Backend's body); hkReset(365), detour(528), getVtbl(435)
- `UtinniCore/swg/graphics/directx9.h` — exported `UTINNI_API` façade (38-41); init decls (32-33)
- `UtinniCore/swg/graphics/depth_texture.h` — `DepthTexture` class (getTextureDepth/Color, getStage/setStage)
- `UtinniCore/swg/graphics/graphics.cpp:588-616,733-735` — `hkInstall` → `directX::detour()`(611); `Graphics::detour()`(733)
- `UtinniCore/utinni.cpp:367-378` — utinni_init eager-init(371-372)-before-createDetours(375) ordering
- `UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp` — the comment-stripping source grep-gate precedent (D-06/SC#4)
- `UtinniCore.Tests/UtinniCore.Tests.vcxproj` — native test build wiring(80/99/135) + ProjectReference linkage(138-146)
- `UtinniCore/UtinniCore.vcxproj:167,208,210,243,287,289` — production source/include entries (neighbor lines for the 2 new)
- `.planning/phases/18-render-backend-seam-dx9backend/18-CONTEXT.md` — D-01..D-08 locked decisions
- `.planning/REQUIREMENTS.md:36-44,122-125` — RNDR-01 (this phase) + RNDR-02/03/04 forward-compat consumers

### Secondary (auto-memory — machine-local, verified-against-source where possible)
- `[[feedback_d3d9_reset_third_party]]`, `[[feedback_imgui_embedded_d3d9_rt_space]]`,
  `[[project_swg_context_routing]]`, `[[project_swg_keymap_reality]]`, `[[project_rh_snapshot_no_heap_alloc]]`,
  `[[feedback_gsd_grep_gate_hygiene]]`, `[[feedback_max_harness]]`, `[[feedback_caller_attrs_binary_compat]]`,
  `[[project_gsd_worktrees_off]]`, `[[feedback_dotnet_build_msbuild_resources]]`, `[[project_d3d11_migration]]`

## Metadata

**Confidence breakdown:**
- Seam shape & touch-points: HIGH — every line cited from source read this session (lines re-verified in force-refresh)
- init/detour ordering: HIGH — verified across utinni.cpp:367-378 + graphics.cpp:588-735 + directx9.cpp:251-361
- No-Reset contract: HIGH — `NoDeviceResetTests.cpp` already gates it and passes; `hkReset` body read
- Verification harness: HIGH — exact precedent file exists, helpers read line-by-line, runs in CI
- Exact member signatures / setup() shape / color-window disposition: MEDIUM — planning decisions flagged [ASSUMED] (A1-A5)

**Research date:** 2026-06-15
**Valid until:** 2026-07-15 (stable internal architecture; only churns if imgui_impl/directx9 are edited)
