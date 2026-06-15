# Phase 18: Render-Backend Seam + Dx9Backend - Context

**Gathered:** 2026-06-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Carve a single `IRenderBackend` seam out of the existing overlay code so the ImGui overlay
renders through one API-neutral path, with the current D3D9 behavior **unchanged** — setting up a
risky D3D11 twin (Phase 19) to be added later without forking the shared overlay logic.

**The carve (grounded in code):**
- `UtinniCore/swg/ui/imgui_impl.cpp` (~1005 lines) holds the API-neutral overlay logic — WndProc
  subclass + Issue #11 chat-context routing, RT-space input mapping, gizmo, renderCallbacks bus —
  **plus** the DX9-specific calls that must move behind the seam:
  - `ImGui_ImplDX9_Init(pDevice)` (setup:274), `ImGui_ImplDX9_NewFrame()` (render:419),
    `ImGui_ImplDX9_RenderDrawData(...)` (562)
  - `directX::getDepthTexture()` reach-ins (349, 376, 489) — RESZ depth-resolve, DX9-specific
  - `IDirect3DDevice9*` + `D3DDEVICE_CREATION_PARAMETERS` (to extract `hFocusWindow`)
- `UtinniCore/swg/graphics/directx9.{cpp,h}` (`directX::` namespace) — device/detour/Present-block/
  depth-texture — carved into a `Dx9Backend` behind the seam.
- New `UtinniCore/swg/graphics/render_backend.{h,cpp}` — does not exist yet; this phase creates it.

**In scope:** the seam interface + Dx9Backend carve + single-sourcing imgui_impl + verification harness.
**Out of scope:** any D3D11 work (Phase 19), any resize *behavior* change, any new editor/UI feature.

</domain>

<decisions>
## Implementation Decisions

### Seam shape & resize hooks
- **D-01:** `IRenderBackend` is a **runtime-polymorphic pure-virtual abstract base** (vtable), with a
  single global instance selected at `setup()`. Phase 19 adds `Dx11Backend` behind the same vtable —
  it does not re-touch `imgui_impl`. (A concrete-now / abstract-in-19 approach was rejected: it would
  force Phase 19 to re-open the seam.)
- **D-02:** The seam exposes the ROADMAP-named members — `newFrame` / `renderDrawData` /
  `onPreResize` / `onPostResize` / `renderTargetWidth` / `renderTargetHeight` — **plus a 7th**:
  an API-neutral scene-depth accessor (see D-05).
- **D-03:** For the no-Reset / Present-stretch D3D9 path, `onPreResize` / `onPostResize` are **honest
  no-ops** in `Dx9Backend`. They exist purely so Phase 19's Dx11 `ResizeBuffers` has a home. D3D9
  needs no resize work — the contract is preserved verbatim, no `Reset` introduced.
- **D-04:** `renderTargetWidth/Height` continue to be sourced from the existing present-stretch math
  already living in `imgui_impl.cpp` (RT-space mapping is unchanged behavior).

### API-neutrality strictness
- **D-05:** **Full purge.** After the carve, `imgui_impl.{cpp,h}` contain ZERO `d3d9.h` include, ZERO
  `IDirect3DDevice9`, ZERO `ImGui_ImplDX9_*` — everything DX9 lives behind the seam. Scene depth
  (formerly `directX::getDepthTexture()`) is reached through the seam's API-neutral accessor, so the
  gizmo/post-processing logic in `imgui_impl` stays fully API-neutral.
- **D-06:** **Enforced by a structural gate** (a grep-style Catch2/xUnit Fact, in the spirit of the
  `06-AUDIT` preservation Facts) that fails the build if any DX9 symbol reappears in `imgui_impl`.
  **Grep-gate hygiene:** choose the gated token(s) so that source *comments* mentioning "DX9"/"D3D9"
  do not false-trip the gate (see `[[feedback_gsd_grep_gate_hygiene]]`) — gate on concrete symbol
  forms (`ImGui_ImplDX9_`, `IDirect3DDevice9`, `#include <d3d9.h>`), not the bare string "D3D9".

### Verification strategy
- **D-07:** **Both harness layers** (per the max-harness preference, `[[feedback_max_harness]]`):
  1. the D-06 structural gate proves API-neutrality;
  2. a **Catch2 seam test** installs a no-op/mock `IRenderBackend` and asserts the dispatch
     contract — `newFrame` / `renderDrawData` / `onPreResize` / `onPostResize` / scene-depth /
     RT-width/height are all routed through the vtable exactly as the live render path would call them.
- **D-08:** The existing **maintainer live-smoke** remains the final RNDR-01 acceptance gate (overlay
  still renders + takes input in a live D3D9 SWG session). CI cannot run it; the two automated layers
  above are the regression protection CI *can* enforce. Live-smoke is a maintainer-only human
  checkpoint, not a subagent-reachable step.

### Claude's Discretion
- Exact split of `directX::` ownership (detour install / Present-block / wireframe / depth-texture
  init) between the new `Dx9Backend` and any residual free functions — left to research/planning,
  provided the no-Reset contract and the D-05 purge hold. The renderCallbacks bus stays API-neutral
  in `imgui_impl` (it is not backend-coupled).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/ROADMAP.md` §"Phase 18: Render-Backend Seam + Dx9Backend" — goal + 4 locked success
  criteria (seam methods, single-source the ~1000 lines, no-Reset/Present-stretch contract verbatim).
- `.planning/REQUIREMENTS.md` — RNDR-01 (this phase) and RNDR-02/03/04 (Phase 19, the consumer of
  this seam — read for forward-compatibility of the interface).

### The code being carved
- `UtinniCore/swg/ui/imgui_impl.cpp` / `imgui_impl.h` — the ~1000-line API-neutral overlay logic +
  the DX9 touch-points enumerated in the Phase Boundary (lines 274/419/562 + depth-tex 349/376/489).
- `UtinniCore/swg/graphics/directx9.cpp` / `directx9.h` — `directX::` namespace carved into Dx9Backend.
- `UtinniCore/swg/graphics/depth_texture.{cpp,h}` — RESZ depth-resolve that moves behind the seam (D-05).

### Locked contracts / engineering lessons that constrain the carve
- `docs/ai/lessons.md` — rendering/injection lessons (RT-space mapping, no-Reset).
- Auto-memory (machine-local, not in repo): `[[feedback_d3d9_reset_third_party]]` (never call
  `Reset` on the third-party device), `[[feedback_imgui_embedded_d3d9_rt_space]]` (RT-space mapping +
  `AddMousePosEvent` in imgui 1.87+), `[[project_swg_context_routing]]` / `[[project_swg_keymap_reality]]`
  (Issue #11 chat-context routing that must survive the carve unchanged), `[[feedback_max_harness]]`,
  `[[feedback_gsd_grep_gate_hygiene]]`, `[[project_d3d11_migration]]` (why the seam exists).
- `UtinniCore.Tests/` (Catch2) + `UtinniCoreDotNet.Tests/PreservationAudit/` (the `06-AUDIT` grep-Fact
  pattern the D-06 structural gate should mirror).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`directX::getDevice()` / `getDepthTexture()` / `blockPresent()` / `toggleWireframe()`** — existing
  DX9 entry points; become the body of `Dx9Backend`.
- **Present-stretch RT-space mapping** (imgui_impl.cpp ~lines 425–465) — stays in the API-neutral path;
  feeds `renderTargetWidth/Height`.
- **`06-AUDIT` preservation Facts** (`UtinniCoreDotNet.Tests/PreservationAudit/`) — the established
  fail-on-violation grep-Fact pattern to clone for the D-06 API-neutrality gate.

### Established Patterns
- **No-Reset / Present-stretch D3D9 contract** — never call `IDirect3DDevice9::Reset`; windowed Present
  stretches the backbuffer; window just resizes. Must be preserved verbatim through the refactor.
- **R-A subscribe/unsubscribe callback bus** (renderCallbacks, gizmo callbacks) in `imgui_impl.h` —
  API-neutral, stays put.
- **Heap-free hot paths** (`[[project_rh_snapshot_no_heap_alloc]]`) — the per-frame seam dispatch must
  not introduce allocations on the render path.

### Integration Points
- `setup(IDirect3DDevice9*)` is the install seam — extracts `hFocusWindow`, currently calls
  `ImGui_ImplDX9_Init`. Becomes: select backend → `backend->init(...)`.
- `render()` per-frame: `newFrame()` → build UI → `renderDrawData()`, all via the vtable.
- The DX9 detour (`directX::detour()`) and `utinni_init` ordering (`initPresentBlockedEvent` /
  `initDepthTexture` before `createDetours()`) must be preserved through the move into Dx9Backend.

</code_context>

<specifics>
## Specific Ideas

- The seam member set is deliberately the ROADMAP's 6 + 1 scene-depth accessor — no broader
  abstraction. Keep the interface minimal and shaped to exactly what Phase 19's Dx11Backend will need.
- Gate on concrete DX9 symbol forms, not the bare string "D3D9" (comment false-trip avoidance).

</specifics>

<deferred>
## Deferred Ideas

- **D3D11 backend, DXGI Present/ResizeBuffers hooks, `gl%02d_r.dll` auto-detect, one-backend-per-session
  diagnostic** — Phase 19 (RNDR-02/03/04). The seam is designed *for* these but implements none here.

### Reviewed Todos (not folded)
- **`swg-window-resize-fullscreen-edge-cases`** (todo, area d3d9-presentation, matched score 0.6) —
  reviewed and **deliberately NOT folded** into Phase 18. Folding it would put real resize-behavior
  logic in `onPreResize/onPostResize` (the rejected "Dx9 resize does work" option) and threaten the
  "behaviorally unchanged" success criterion. The residual fullscreen RT-space mouse-offset +
  cursor-clip deadzone stay deferred (also tracked in STATE.md Deferred Items as RESID-04 residual).
- **`phase09-datatable-editor-review-warnings`**, **`phase10-stringtable-sc3-live-reload-residual`** —
  weak keyword matches only; unrelated to a render-backend carve. Not folded.

</deferred>

---

*Phase: 18-render-backend-seam-dx9backend*
*Context gathered: 2026-06-15*
