# Phase 19: Dx11Backend + Config Detection + Resize - Context

**Gathered:** 2026-06-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Add the `Dx11Backend` twin behind the **existing Phase 18 `IRenderBackend` 10-vtable seam** so the
ImGui overlay renders and maps input correctly when the SWG client runs Direct3D 11, with exactly
**one backend installed per session** (auto-detected, hard cutover) and resize handled **the DXGI
way** (`ResizeBuffers` RTV recreate, NOT the D3D9 never-Reset/stretch rule carried verbatim).

**The work (grounded in Phase 18's seam):**
- `UtinniCore/swg/graphics/render_backend.{h,cpp}` already defines `IRenderBackend` (10 pure virtuals)
  + `Dx9Backend`. Phase 19 adds a `Dx11Backend final : public IRenderBackend` plugging into the SAME
  vtable — it does NOT re-touch `imgui_impl.cpp` (API-neutral per the D-05 purge).
- Backend selection at install: a single `GetModuleHandle` check on the loaded `gl%02d_r.dll` family
  picks Dx9 vs Dx11 (`render_backend::set(...)` in `imgui_impl::setup()` is the install seam).
- DXGI hooks: `IDXGISwapChain::Present` (vtbl idx 8) + `ResizeBuffers` (vtbl idx 13), with a per-frame
  backbuffer-RTV rebind and RTV release/recreate on resize.

**Scope: 32-bit only.** x64 is explicitly OUT of v2.1 (user-locked 2026-06-14; the `swg-client-v2`
`x64bit-Upgrade` branch is a deliberate later milestone — Backlog 999.7).

**In scope:** `Dx11Backend` (consumer side) + renderer-DLL detection/cutover + DXGI resize +
verification harness + **a written hook-point advertisement contract spec** (handoff artifact, see D-12).
**Out of scope:** the actual SWG-Source client instrumentation (separate Claude Code handoff, D-13);
D3D9 windowed↔fullscreen window-management bugs (deferred todo); any new editor/UI feature; x64.

</domain>

<decisions>
## Implementation Decisions

### Hook-point acquisition (KEY DEVIATION from ROADMAP success criterion 1)
- **D-09:** **Client advertises its render hook points; Utinni consumes them.** The D3D11 client is a
  SWG-Source build the **user controls**, so instead of the ROADMAP's *blind* acquisition (throwaway
  `D3D11CreateDeviceAndSwapChain` → harvest `Present` vtbl idx 8 / `ResizeBuffers` idx 13), the
  instrumented client cooperatively **advertises** where its hook points live. This is the
  entry-point-advertisement philosophy (`[[project_entrypoint_advertisement_mechanism]]` / Backlog
  999.7) pulled in here for the render seam specifically.
- **D-10:** **REPLACE, not augment.** Advertised hook points are THE path for D3D11 — the blind
  throwaway-device vtbl-harvest is **dropped** for this phase. ⚠ This **supersedes ROADMAP Phase 19
  success criterion 1's stated acquisition method** ("acquired from a throwaway
  `D3D11CreateDeviceAndSwapChain`"). Flag as a deliberate, user-approved deviation; the *outcome*
  (hook Present idx 8 + ResizeBuffers idx 13, per-frame RTV rebind) is unchanged — only how the
  addresses/objects are obtained changes. Planner should reconcile this with the ROADMAP wording.
- **D-11:** **Contract shape RESOLVED by research (2026-06-15) → see `19-INSTRUMENTATION-SPEC.md`.**
  Mechanism = **Candidate A**: `gl11_r.dll` gains one new `extern "C"` export `GetHookPoints()`
  returning the live `{IDXGISwapChain1*, ID3D11Device*, ID3D11DeviceContext*}` (~9 client-side lines,
  no logic change, client stays Utinni-agnostic). Utinni `GetProcAddress`es it, polls
  `swapChain != null` once/frame, then reads vtbl idx 8/13 off the **live** swapchain and detours
  `Present`/`ResizeBuffers`. Push-model (client calls a Utinni export, Candidate D) is the documented
  fallback. The blind throwaway-`D3D11CreateDeviceAndSwapChain` harvest is NOT used in production
  (D-10) — only as the offline CI test (D-21 layer 3). Grounded: client uses
  `D3D11CreateDevice` + `CreateSwapChainForHwnd` (NOT `...AndSwapChain`); swapchain is a
  process-lifetime `ms_swapChain` ComPtr that persists through resize.

### Separation of responsibilities (Utinni vs SWG-Source)
- **D-12:** Phase 19 (Utinni side) delivers: the `Dx11Backend` **consumer** of the advertised contract
  + **a written instrumentation spec** documenting the contract the client must satisfy.
- **D-13:** The actual **SWG-Source client instrumentation is a clean handoff** — the spec (D-12) is
  handed to a **separate Claude Code session** working in the SWG-Source repo. It is NOT Utinni-phase
  code; it does not live in Utinni's tree. Clean separation of responsibilities (analogous to the
  cross-repo UtinniPlugins pattern, but executed by a different agent against a different repo).
- **D-14 (sequencing):** Because advertisement REPLACES harvest, the Utinni consumer needs the contract
  defined first, and the **maintainer live-smoke gate is sequenced AFTER the handoff instrumentation
  lands** in the running client. Planner must account for this dependency ordering (contract design →
  instrumentation spec → handoff/instrument → Utinni consumes → live-smoke).

### Backend detection & cutover
- **D-15:** **Hard one-backend-per-session.** One `GetModuleHandle` check at install selects exactly
  one backend for the whole session; **no mid-session switch**. Matches success criterion 2 verbatim
  and the seam's single-global-instance design. Runtime-switchable was rejected (dual-context / doubled
  input / teardown-reinit risk; contradicts "exactly one per session").
- **D-16:** **Detection keys on the `gl%02d_r.dll` family** (e.g. gl11_r.dll = D3D11 vs the D3D9
  numbered DLL). Researcher must **confirm the exact number→API mapping against the running D3D11
  client** (ground-truth from the live process, not assumed) before the detection code is written.
- **D-17:** **Default D3D9, install Dx11Backend only on positive D3D11 detection.** Ambiguous/neither
  (zero or both renderer DLLs) → fall back to **D3D9 + one-shot diagnostic log**. Safest: the mature
  D3D9 path is the default; D3D11 only when explicitly seen.

### DXGI resize
- **D-18:** **True DXGI `ResizeBuffers`** (vtbl idx 13): release the backbuffer RTV → let
  `ResizeBuffers` run → recreate RTV. Backbuffer tracks the window; no `DXGI_ERROR_INVALID_CALL`. The
  D3D9 never-Reset/stretch rule is **NOT** carried verbatim (D3D11 has no third-party-Reset hazard —
  `[[feedback_d3d9_reset_third_party]]` is D3D9-specific). Confirms success criterion 3.
- **D-18b (resize-trigger gap, research finding 2026-06-15):** the D3D11 client today calls
  `ResizeBuffers` **only** from `displayModeChanged()` on `WM_DISPLAYCHANGE` (monitor mode change) —
  **NOT** on `WM_SIZE` (window-drag / embed-panel resize). Utinni's `ResizeBuffers` hook covers the
  mode-change path; the embedded-panel drag-resize is not backbuffer-tracked unless the client also
  fires `ResizeBuffers` on `WM_SIZE`. Disposition: hook `ResizeBuffers` regardless (covers what fires);
  the optional client-side `WM_SIZE→ResizeBuffers` improvement is raised in the instrumentation spec
  §6 (default = defer to the RESID-04 window-management bucket). Does NOT block the contract.
- **D-19:** **Keep RT-space input mapping under D3D11.** Reuse the existing RT-space block
  (`imgui_impl.cpp` ~425–465) feeding `renderTargetWidth/Height` through the seam. Success criterion 1
  explicitly requires render-target-space input under D3D11; if true `ResizeBuffers` makes
  backbuffer==window it degrades to 1:1 naturally — defensive against any residual stretch (fullscreen/DPI).

### Maturity baseline
- **D-20:** A **runnable 32-bit D3D11 SWG-Source build exists today.** This supersedes the ROADMAP's
  "incomplete upstream" framing for planning purposes — the renderer DLL is observable, vtbl offsets
  confirmable, and live-smoke is possible (once instrumented per D-13/D-14).

### Verification strategy
- **D-21:** **All four automated layers** CI can enforce (per `[[feedback_max_harness]]`; CI cannot run
  live D3D11):
  1. **DXGI vtbl-offset asserts** — Catch2 test pinning the hooked indices (Present=8, ResizeBuffers=13)
     so a silent DXGI ABI drift fails the build.
  2. **Mock `Dx11Backend` dispatch test** — reuse Phase 18's mock-`IRenderBackend` pattern; assert the
     10-vtable dispatch contract routes identically for the D3D11 backend.
  3. **Offline dummy-device harvest test** — process-isolated harness running
     `D3D11CreateDeviceAndSwapChain` (or WARP) to validate offset harvest + assert **no dummy-device
     leak**, without SWG. (NOTE: even though advertisement replaces blind harvest in production per D-10,
     this offline test still validates the offset/leak invariants and the WARP path — confirm WARP/D3D11
     availability on the self-hosted CI runner.)
  4. **Detection-logic unit test** — unit-test the `gl%02d_r.dll` detection + fallback decision
     (default-D9 / positive-D11 / ambiguous→D9+log) with injected module-presence states.
- **D-22:** **Maintainer live-smoke is the final RNDR-02/03/04 acceptance gate** (mirrors Phase 18 D-08
  for RNDR-01): inject + eyeball the overlay (renders, input maps in RT-space, survives resize) on the
  live D3D11 client. Maintainer-only human checkpoint; not subagent-reachable. Sequenced after D-14.

### Claude's Discretion
- Dummy-swapchain HWND choice for the offline harvest test (message-only window vs 1×1 hidden window),
  one-shot diagnostic log content/level/destination, and the exact `Dx11Backend` internal split
  (RTV management / device+context ownership / detour install) — left to research/planning, provided
  D-15..D-19 hold.

### Reviewed Todos (not folded)
- **`swg-window-resize-fullscreen-edge-cases`** (todo, area d3d9-presentation, RESID-04 residual,
  matched score 0.6) — reviewed and **deliberately NOT folded**. Those are D3D9 windowed↔fullscreen
  presentation bugs (z-order/parent coupling, exclusive-fullscreen mode switch, cursor/input-routing
  failure) — broader than Phase 19's DXGI `ResizeBuffers` RTV-recreate scope, and folding would mix
  D3D9 window-management with the D3D11 backend work. Stays deferred (cross-referenced below).
- **`phase09-datatable-editor-review-warnings`**, **`phase10-stringtable-sc3-live-reload-residual`** —
  weak keyword matches only ("phase"/"2026"); unrelated to D3D11 backend work. Not folded.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/phases/19-dx11backend-config-detection-resize/19-INSTRUMENTATION-SPEC.md` — **the resolved
  hook-point advertisement contract** (Candidate A `GetHookPoints()` export). Source of truth for the
  cross-repo handoff; a copy lives at `D:/Code/swg-client-v2/.planning/handoff/2026-06-15-utinni-dx11-hookpoint-advertisement-spec.md`.
  The Utinni-side `Dx11Backend` consumer MUST match this contract.
- `.planning/ROADMAP.md` §"Phase 19: Dx11Backend + Config Detection + Resize" — goal + 3 locked success
  criteria + the research-phase confirm-FIRST notes (renderer-DLL contract; hard-cutover vs
  runtime-switch). ⚠ Note D-10 deviation: criterion-1's "throwaway `D3D11CreateDeviceAndSwapChain`"
  acquisition is superseded by client-advertised hook points.
- `.planning/REQUIREMENTS.md` — RNDR-02 (render+input under D3D11), RNDR-03 (one backend/session,
  auto-detect, one-shot log), RNDR-04 (resize survives via `ResizeBuffers`, no `DXGI_ERROR_INVALID_CALL`).

### The seam this phase plugs into (Phase 18 output)
- `UtinniCore/swg/graphics/render_backend.h` / `render_backend.cpp` — the `IRenderBackend` 10-vtable ABC
  + `Dx9Backend` + `render_backend::get()/set()/dx9Singleton()`. `Dx11Backend` plugs in here.
- `UtinniCore/swg/graphics/render_backend_dx9.cpp` — the Dx9Backend impl (template for the Dx11 twin).
- `UtinniCore/swg/ui/imgui_impl.cpp` `setup(HWND)` (~263) — the install seam where backend selection
  happens; `render_backend::set(dx9Singleton())` is the current install call. Stays API-neutral (D-05).
- `.planning/phases/18-render-backend-seam-dx9backend/18-CONTEXT.md` — the seam design rationale
  (D-01..D-08): vtable shape, no-Reset contract, RT-space mapping, the D-06 API-neutrality grep-gate.

### The D3D9 reference path (to mirror under DXGI)
- `UtinniCore/swg/graphics/directx9.cpp` — `directX::` Present detour, `hkPresent`, dynamic-load harvest
  (Direct3DCreate9, ~467-481), present-stretch/RT-space first-fire diagnostics. The DXGI analog patterns.
- `UtinniCore/utinni.cpp` — `createDetours()` (~109) + the `initPresentBlockedEvent`/`initDepthTexture`
  before-detours ordering (~371-375) the D3D11 install must preserve.

### Entry-point advertisement (the D-09 philosophy)
- Auto-memory (machine-local): `[[project_entrypoint_advertisement_mechanism]]` (client advertises
  entry points via well-known API / build-time config sidecar) and Backlog **999.7** in
  `.planning/ROADMAP.md` (the future advertisement+x64 item this phase pulls a render-seam slice from).
- `docs/ai/rva-realignment.md` + `CON-H-03` — the RVA-discovery model this advertisement supersedes for
  the D3D11 render hooks.

### Locked contracts / engineering lessons
- `docs/ai/lessons.md` — rendering/injection lessons (RT-space mapping, no-Reset is D3D9-specific).
- Auto-memory: `[[feedback_d3d9_reset_third_party]]` (D3D9-only Reset hazard — DO NOT carry to DXGI),
  `[[feedback_imgui_embedded_d3d9_rt_space]]` (RT-space mapping + `AddMousePosEvent` in imgui 1.87+),
  `[[feedback_max_harness]]` (invent harnesses), `[[project_d3d11_migration]]` (why this phase exists),
  `[[feedback_detourxs_explicit_len]]` (prefer DETOUR_LEN_AUTO if detouring advertised addresses),
  `[[project_rh_snapshot_no_heap_alloc]]` (per-frame seam dispatch stays heap-free).
- `UtinniCore.Tests/` (Catch2) + `UtinniCoreDotNet.Tests/PreservationAudit/` — the harness homes for the
  D-21 automated layers (offset asserts, mock dispatch, dummy-device harvest, detection-logic unit).
- SWG-Source / `swg-client-v2` `Direct3d11` project — the running D3D11 client to confirm the renderer
  DLL name (D-16), the live swapchain/device, and the target of the D-13 instrumentation handoff
  (read-only reference corpus at `D:/Code/swg-client-v2`).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`IRenderBackend` 10-vtable ABC + `render_backend::set/get/dx9Singleton`** (`render_backend.h`) — the
  `Dx11Backend` is a second `final` subclass; install via `render_backend::set(dx11Singleton())`.
- **`Dx9Backend` impl** (`render_backend_dx9.cpp`) — the structural template for the Dx11 twin
  (vtable overrides forwarding to the API-specific calls; non-virtual `init()` device-bearing entry).
- **`Dx9Backend::stashDevice()/stashedDevice()`** pattern — hkPresent stashes the live device before the
  API-neutral `setup(HWND)`; the Dx11 path needs the analogous live-swapchain/device stash.
- **`directX::hkPresent` + first-fire diagnostics** (`directx9.cpp` ~266) — the model for the DXGI
  `Present` hook body (RT-space first-fire log, present-block, depth-texture rebind).
- **Phase 18 mock-`IRenderBackend` Catch2 test** — the dispatch-contract test pattern to clone for D-21 layer 2.

### Established Patterns
- **API-neutral `imgui_impl` (D-05 purge + D-06 grep-gate)** — `Dx11Backend` must keep all D3D11 types
  behind the seam; `imgui_impl.cpp` stays free of d3d11/DXGI includes. The grep-gate must not regress.
- **Before-detours init ordering** (`utinni.cpp` ~371-375) — `initPresentBlockedEvent`/`initDepthTexture`
  run before `createDetours()`; the D3D11 install path must preserve equivalent ordering.
- **Heap-free hot path** (`[[project_rh_snapshot_no_heap_alloc]]`) — per-frame RTV rebind + seam dispatch
  must not allocate.

### Integration Points
- `imgui_impl::setup(HWND)` (~263) — add the `gl%02d_r.dll` detection + backend selection here
  (default Dx9, Dx11 on positive detect; D-15/D-17).
- DXGI Present (idx 8) + ResizeBuffers (idx 13) hook install — the addresses/objects come from the
  **advertised contract** (D-09/D-10), not a throwaway-device harvest, in production.
- `createDetours()` (`utinni.cpp` ~109) — where the D3D11 detours register once the backend is selected.

</code_context>

<specifics>
## Specific Ideas

- **Instrumentation spec is a first-class deliverable** (D-12): a standalone handoff doc defining the
  hook-point advertisement contract the SWG-Source client must satisfy, clean enough that a separate
  Claude Code session can implement the client side without further Utinni context.
- The blind throwaway-device harvest survives ONLY as an offline CI test (D-21 layer 3) for the
  offset/leak invariants — it is NOT the production acquisition path (D-10).
- Renderer-DLL number→API mapping must be ground-truthed against the live running client, not assumed (D-16).

</specifics>

<deferred>
## Deferred Ideas

- **Full entry-point advertisement mechanism + x64** — Backlog 999.7. Phase 19 pulls in only a
  render-seam slice of the advertisement philosophy (D3D11 hook points); the general entry-point
  advertisement and x64 support stay deferred to a post-v2.1 milestone (x64 user-locked OUT of v2.1).
- **Runtime-switchable backends** — rejected for this phase (D-15); revisit only if a future client
  toggles renderers mid-session.

### Reviewed Todos (not folded)
- **`swg-window-resize-fullscreen-edge-cases`** (RESID-04 residual, area d3d9-presentation) — D3D9
  windowed↔fullscreen z-order/parent-coupling + cursor/input-routing bugs. Reviewed, **kept deferred**:
  broader than Phase 19's DXGI `ResizeBuffers` scope; folding would mix D3D9 window-management into the
  D3D11 backend work. Stays in the future window-management / D3D9-presentation pass.
- **`phase09-datatable-editor-review-warnings`**, **`phase10-stringtable-sc3-live-reload-residual`** —
  weak keyword matches; unrelated. Not folded.

</deferred>

---

*Phase: 19-dx11backend-config-detection-resize*
*Context gathered: 2026-06-15*
