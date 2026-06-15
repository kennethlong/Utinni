# Phase 18: Render-Backend Seam + Dx9Backend - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-15
**Phase:** 18-render-backend-seam-dx9backend
**Areas discussed:** Seam shape & resize hooks, API-neutrality strictness, Depth-texture / RESZ ownership, Verification strategy

---

## Seam shape & resize hooks

| Option | Description | Selected |
|--------|-------------|----------|
| Abstract base + Dx9 resize no-ops | `IRenderBackend` pure-virtual abstract base (vtable), one instance at setup; Phase 19 just adds Dx11Backend behind it. Dx9 onPreResize/onPostResize are honest no-ops (no-Reset contract); RT width/height from existing present-stretch math. | ✓ |
| Abstract base + Dx9 resize does work | Same vtable, but resize hooks carry real D3D9 logic now (folds the swg-window-resize todo). Expands scope, risks "behaviorally unchanged". | |
| Concrete seam, abstract in Ph19 | Dx9Backend concrete now, introduce interface in Phase 19. Defers seam design; Phase 19 re-touches imgui_impl. | |

**User's choice:** Abstract base + Dx9 resize no-ops
**Notes:** Keeps Phase 18 a pure carve; resize behavior change stays out. swg-window-resize todo deferred.

---

## API-neutrality strictness

| Option | Description | Selected |
|--------|-------------|----------|
| Full purge + enforced grep gate | imgui_impl ends with zero d3d9.h / IDirect3DDevice9 / ImGui_ImplDX9_*; a structural test fails the build if any DX9 symbol reappears. | ✓ |
| Full purge, no enforcement test | Carve fully but rely on review, not an automated gate. Regressions can creep back silently. | |
| Pragmatic boundary | Carve hot-path calls, tolerate a few residual DX9 touch-points. Weaker seam for Phase 19. | |

**User's choice:** Full purge + enforced grep gate
**Notes:** Matches grep-gate hygiene + max-harness habits. Gate on concrete symbol forms so "DX9" in comments doesn't false-trip.

---

## Depth-texture / RESZ ownership

| Option | Description | Selected |
|--------|-------------|----------|
| Generic accessor on the seam | Scene depth exposed through IRenderBackend as an API-neutral accessor; imgui_impl calls the seam, not directX::getDepthTexture(). Dx9Backend implements via RESZ. | ✓ |
| Stays DX9-only behind Dx9Backend | depth_texture moves into Dx9Backend, stays DX9-only; some gizmo/post-processing logic stays DX9-coupled. Tension with full-purge. | |
| Defer depth-tex abstraction to Ph19 | Carve everything except depth reach-ins; leave one gated exception, abstract when Dx11 needs depth. Smaller diff, one carried seam gap. | |

**User's choice:** Generic accessor on the seam
**Notes:** Adds a 7th seam member beyond the ROADMAP's named 6 — required to make the Area-2 full purge actually achievable.

---

## Verification strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Both: structural gate + seam unit test | Area-2 grep gate proves API-neutrality + a Catch2 test installs a no-op/mock backend and asserts the vtable dispatch contract. Live-smoke = final maintainer gate. | ✓ |
| Structural gate only | Grep gate + live-smoke; skip the mock-backend dispatch test. Seam call contract not independently exercised. | |
| Live-smoke only | Treat existing maintainer live-smoke as sufficient. No automated regression protection. | |

**User's choice:** Both: structural gate + seam unit test
**Notes:** Max-harness default. CI can enforce the two automated layers; live-smoke remains the maintainer-only RNDR-01 acceptance.

---

## Claude's Discretion

- Exact split of `directX::` ownership (detour install / Present-block / wireframe / depth-texture init)
  between the new `Dx9Backend` and any residual free functions — left to research/planning, provided
  the no-Reset contract and the full purge hold. renderCallbacks bus stays API-neutral.

## Deferred Ideas

- D3D11 backend / DXGI Present + ResizeBuffers hooks / `gl%02d_r.dll` auto-detect / one-backend-per-session
  diagnostic — Phase 19 (RNDR-02/03/04).
- `swg-window-resize-fullscreen-edge-cases` todo — reviewed, deliberately not folded (protects
  "behaviorally unchanged"). Residual fullscreen RT-space mouse-offset + cursor-clip deadzone stay deferred.
