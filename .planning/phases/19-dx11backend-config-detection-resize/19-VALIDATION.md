---
phase: 19
slug: dx11backend-config-detection-resize
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-06-15
validated: 2026-06-30
---

# Phase 19 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from 19-RESEARCH.md "Validation Architecture". The four automated layers
> below realize CONTEXT.md D-21; the live-smoke (D-22) is the manual-only final gate.

> **Retroactive reconciliation (2026-06-30, v2.1 milestone audit):** left at `draft` after the phase
> closed. The four automated layers shipped CI-green; the D-22 DX11 live-smoke was CLOSED 2026-06-23
> (advertised DX11 client boots → renders → embed-scales). Flipped to `complete` / `nyquist_compliant: true`.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Catch2 (native, `UtinniCore.Tests`) + xUnit (managed gates, `UtinniCoreDotNet.Tests`) |
| **Config file** | Built via VS2026 MSBuild on `Utinni.sln` (`/p:Configuration=Release /p:Platform=x86`); no `dotnet build` (MSB3823 on WinForms .resx) |
| **Quick run command** | Native: run the `UtinniCore.Tests` Catch2 exe; Managed: `dotnet test --no-build` (targeted) |
| **Full suite command** | MSBuild build → `dotnet test --no-build` (managed) + run Catch2 `UtinniCore.Tests` exe |
| **Estimated runtime** | ~build-bound (native link dominates); test exec seconds |

---

## Sampling Rate

- **After every task commit:** Run the affected Catch2/xUnit lane (build the touched project, run its tests)
- **After every plan wave:** Full native Catch2 suite + managed `dotnet test --no-build`
- **Before `/gsd:verify-work`:** Full suite green; D-06 API-neutrality gate + NoDeviceReset gate green
- **Max feedback latency:** build-bound (inline build waves; worktrees OFF)

---

## Per-Task Verification Map

> The four D-21 automated layers (planner assigns task IDs/waves). The live D3D11 client is NOT
> reachable in CI — these are the regression layers CI *can* enforce; D-22 live-smoke is manual.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| {tbd} | {tbd} | {tbd} | RNDR-02 | — | Overlay dispatch routes via the 10-vtable seam for the Dx11 backend (mock IRenderBackend) | unit (Catch2) | run `UtinniCore.Tests` | ❌ W0 | ⬜ pending |
| {tbd} | {tbd} | {tbd} | RNDR-02 | — | DXGI vtbl offsets pinned: Present=8, ResizeBuffers=13 (offset-assert) | unit (Catch2) | run `UtinniCore.Tests` | ❌ W0 | ⬜ pending |
| {tbd} | {tbd} | {tbd} | RNDR-03 | — | Detection: default-D9 / positive-D11 / ambiguous→D9+log (injected module-presence) | unit (Catch2) | run `UtinniCore.Tests` | ❌ W0 | ⬜ pending |
| {tbd} | {tbd} | {tbd} | RNDR-02/04 | — | Offline WARP `D3D11CreateDeviceAndSwapChain` harvest of vtbl 8/13 + no dummy-device leak | integration (Catch2, process-isolated) | run `UtinniCore.Tests` | ❌ W0 | ⬜ pending |
| {tbd} | {tbd} | {tbd} | RNDR-02 | — | D-06 API-neutrality gate EXTENDED to ban D3D11/DXGI symbol forms in imgui_impl | gate (xUnit) | `dotnet test --no-build` | ✅ extend existing | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Enable vcpkg imgui `dx11-binding` feature (vcpkg.json) + reinstall — **FIRST task**; `imgui_impl_dx11.{h,cpp}` does not exist until then (RESEARCH finding; AGENTS.md is wrong that it's already in the manifest).
- [ ] New Catch2 test files in `UtinniCore.Tests` for the four D-21 layers (clone the Phase-18 mock-`IRenderBackend` dispatch test + the D3D9 harvest harness as analogs).
- [ ] Confirm WARP (`D3D_DRIVER_TYPE_WARP`) / d3d11 availability for a 32-bit test process on the self-hosted CI runner.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Overlay renders + maps input in RT-space on the live D3D11 client | RNDR-02 | CI cannot run a live SWG D3D11 session (D-22) | Inject Utinni into the running 32-bit `gl11_r.dll` SWG-Source client; confirm overlay draws + mouse/keyboard map in render-target space |
| Overlay survives resize without `DXGI_ERROR_INVALID_CALL` | RNDR-04 | Live-only; client fires `ResizeBuffers` on both `WM_DISPLAYCHANGE` and `WM_SIZE` (D-18b CLOSED @ 2d01b0cb5) | Trigger a display-mode change AND an embed-panel/window resize (drag, maximize/restore) in the live D3D11 client; confirm overlay re-renders, RTV recreated, backbuffer tracks client rect, no INVALID_CALL. (WM_SIZE now in scope — client landed spec §6 Option 1.) |
| Exactly one backend, one-shot diagnostic, no doubled input | RNDR-03 | Live-only confirmation of single-context install | Inject into the D3D11 client; confirm the one-shot detection log names gl11_r.dll + Dx11Backend, single ImGui context, no doubled input |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (dx11-binding enable; new Catch2 files; WARP availability)
- [ ] No watch-mode flags
- [ ] Feedback latency acceptable (build-bound, inline)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
