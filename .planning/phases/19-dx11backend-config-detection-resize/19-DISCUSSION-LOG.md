# Phase 19: Dx11Backend + Config Detection + Resize - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-15
**Phase:** 19-dx11backend-config-detection-resize
**Areas discussed:** Maturity/cutover, Renderer-DLL detection contract, DXGI resize behavior, Verification & live-smoke, Hook-point advertisement (added mid-discussion by user)

---

## D3D11 client maturity

| Option | Description | Selected |
|--------|-------------|----------|
| Runnable now | A launchable 32-bit D3D11 SWG-Source build exists today | ✓ |
| Foundation ahead of client | No runnable D3D11 client yet; build ahead of maturity | |
| Partial / in-flight | Unstable/incomplete D3D11 build | |

**User's choice:** Runnable now.
**Notes:** Supersedes ROADMAP's "incomplete upstream" framing — renderer DLL observable, live-smoke possible.

## Cutover (research item b)

| Option | Description | Selected |
|--------|-------------|----------|
| Hard one-per-session | One GetModuleHandle check at install; no mid-session swap | ✓ |
| Runtime-switchable | Swap backends mid-session | |

**User's choice:** Hard one-per-session.

---

## Renderer-DLL detection — DLL name (research item a)

| Option | Description | Selected |
|--------|-------------|----------|
| `gl%02d_r.dll` family | Numbered renderer DLL (gl11_r.dll D3D11 vs gl9_r.dll D3D9) | ✓ |
| `Direct3d11.dll` | SWG-Source project output by that name | |
| Researcher confirms first | Confirm exact module against running client | |

**User's choice:** `gl%02d_r.dll` family.
**Notes:** Researcher still confirms exact number→API mapping against the live client.

## Renderer-DLL detection — direction & fallback

| Option | Description | Selected |
|--------|-------------|----------|
| Default D3D9, D3D11 on positive | Install Dx9 unless D3D11 positively detected; ambiguous→D9+log | ✓ |
| Default D3D11, D3D9 fallback | Prefer D3D11 | |
| Hard-require exactly one | Bail if zero/both detected | |

**User's choice:** Default D3D9, D3D11 on positive detect.

---

## DXGI resize — philosophy

| Option | Description | Selected |
|--------|-------------|----------|
| True DXGI ResizeBuffers | Release RTV → ResizeBuffers → recreate RTV; backbuffer follows window | ✓ |
| Keep present-stretch model | Mirror D3D9 stretch | |
| Researcher confirms | Confirm whether client calls ResizeBuffers | |

**User's choice:** True DXGI ResizeBuffers.

## DXGI resize — RT-space input mapping

| Option | Description | Selected |
|--------|-------------|----------|
| Keep RT-space mapping | Reuse existing RT-space block; degrades to 1:1 if backbuffer==window | ✓ |
| Assume 1:1 under D3D11 | Drop stretch math | |
| Researcher determines | Decide after confirming backbuffer-vs-window | |

**User's choice:** Keep RT-space mapping.

---

## Verification — automated harness layers (multi-select)

| Option | Description | Selected |
|--------|-------------|----------|
| DXGI vtbl-offset asserts | Pin Present=8/ResizeBuffers=13 | ✓ |
| Mock Dx11Backend dispatch test | Reuse Phase 18 mock-IRenderBackend pattern | ✓ |
| Offline dummy-device harvest test | D3D11CreateDeviceAndSwapChain/WARP offset + leak check | ✓ |
| Detection-logic unit test | gl%02d_r.dll detection + fallback decision | ✓ |

**User's choice:** All four.

## Verification — live gate

| Option | Description | Selected |
|--------|-------------|----------|
| Maintainer live-smoke is final gate | Inject + eyeball on live D3D11 client | ✓ |
| Automated-only, live-smoke optional | Treat automated as sufficient | |

**User's choice:** Maintainer live-smoke is the final RNDR-02/03/04 gate.

---

## Hook-point advertisement (added by user mid-discussion)

> User note: "we have to instrument the D3D11 client to advertise its hook points."

| Question | Options | User's choice |
|----------|---------|---------------|
| Replace vs augment vtbl-harvest | Replace / Augment / Let me explain | **Replace** — advertised hook points are THE path; blind harvest dropped (supersedes success criterion 1) |
| Mechanism | Function addresses / live swapchain pointer / config sidecar / researcher designs | **Researcher/I design the contract** — principle locked, shape is a research task |
| Client-side scope | Paired dependency / in-scope both sides / already instrumented | **Paired dependency** — "but you write a spec to hand off to claude code to do the instrumentation part, clean separation of responsibilities" |

**Notes:** Phase 19 (Utinni side) = Dx11Backend consumer + a written instrumentation spec; the SWG-Source
client instrumentation is a clean handoff to a separate Claude Code session. Live-smoke gate sequenced
after the handoff instrumentation lands.

---

## Claude's Discretion

- Dummy-swapchain HWND choice for the offline harvest test, one-shot diagnostic log content/level, and the
  exact Dx11Backend internal split (RTV mgmt / device+context ownership / detour install) — left to
  research/planning provided the detection + resize + RT-space decisions hold.

## Deferred Ideas

- Full entry-point advertisement mechanism + x64 — Backlog 999.7 (Phase 19 pulls only a render-seam slice).
- Runtime-switchable backends — rejected this phase.
- `swg-window-resize-fullscreen-edge-cases` (RESID-04) — D3D9 window-management bugs, kept deferred.
