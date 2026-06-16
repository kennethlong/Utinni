---
phase: 19-dx11backend-config-detection-resize
verified: 2026-06-15T00:00:00Z
status: passed
score: 8/8 code-level must-haves verified (live D3D11 behavioral acceptance deferred to Phase 24)
overrides_applied: 0
deferred:
  - truth: "Live D3D11 behavioral acceptance (D-22): overlay renders + RT-space input + single backend + DXGI resize survives, confirmed by maintainer injection into the live 32-bit gl11_r.dll client (success-criteria #1-3 LIVE behavior, RNDR-02/03/04 live legs)"
    addressed_in: "Phase 24 (EPA-01..04, GetEngineHookPoints)"
    evidence: "REQUIREMENTS.md EPA-03 (Phase 24): 'The DX11 overlay kickoff is decoupled from the SWGEmu-addressed graphics::install hook'. Live inject into swg-client-v2/stage/SwgClient_r.exe crashed in createDetours() (VEH 0xC0000005 READ target=0x00401000 = swg::config::detour hooking loadOverrideConfig); this is the PRE-EXISTING hardcoded-RVA / entry-point-advertisement gap, NOT a Phase 19 code defect. The DX11 hook path (directx11.cpp) is correctly binary-agnostic (gl11_r.dll GetHookPoints + swapchain vtbl, no hardcoded addresses). Maintainer-approved scoping decision 2026-06-15."
---

# Phase 19: Dx11Backend + Config Detection + Resize Verification Report

**Phase Goal:** The ImGui overlay renders and maps input correctly when the SWG client runs Direct3D 11, with exactly one backend installed per session and resize handled the DXGI way.
**Verified:** 2026-06-15
**Status:** passed (CODE-LEVEL + CI-ENFORCEABLE achievement; live D3D11 behavioral acceptance deferred to Phase 24)
**Re-verification:** No — initial verification

## Scoping Note (maintainer-approved, 2026-06-15)

This phase is closed **code-complete with the live D3D11 acceptance (D-22, the LIVE behavior of success-criteria #1-3) DEFERRED to Phase 24**, NOT classified as a gap. A live injection into the from-source `SwgClient_r.exe` crashed in `createDetours()` (VEH `0xC0000005` READ `target=0x00401000`) because UtinniCore's ~198 game-logic detours are hardcoded to the SWGEmu.exe binary layout (`swg::config::detour()` hooks `loadOverrideConfig` at `0x00401000` — the exact faulting address). This is the **pre-existing entry-point-advertisement / hardcoded-RVA architectural limitation**, now scoped as Phase 24 (EPA-01..04). The Phase 19 DX11 hook path itself is correctly binary-agnostic. This verification therefore certifies the **code-level and CI-enforceable** achievement, and records the live legs as deferred (see frontmatter `deferred`).

## Goal Achievement

### Observable Truths (code-level / CI-enforceable)

| #   | Truth                                                                                                                      | Status     | Evidence                                                                                                                                                                                                                                                  |
| --- | -------------------------------------------------------------------------------------------------------------------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | A `Dx11Backend final : public IRenderBackend` twin exists, declared behind the Phase-18 seam with zero DX11 includes       | ✓ VERIFIED | `render_backend.h:131-157` — `Dx11Backend` with all 10 vtable overrides + `init(ID3D11Device*, ID3D11DeviceContext*)`; only forward-decls `struct ID3D11Device/DeviceContext/RenderTargetView` (lines 43-45); `dx11Singleton()` at 168. No `<d3d11.h>`.   |
| 2   | The `Dx11Backend` impl realizes per-frame backbuffer-RTV rebind (flip-discard) + true DXGI resize release/recreate (D-18)  | ✓ VERIFIED | `render_backend_dx11.cpp` — `newFrame()` caches RTV + `OMSetRenderTargets` before `ImGui_ImplDX11_NewFrame` (82-97); `onPreResize()` releases m_rtv (107-114), `onPostResize()` recreates (116-119); `createBackbufferRtv()` releases only the GetBuffer temp. |
| 3   | A `directX11::` DXGI hook tier hooks Present(idx 8) + ResizeBuffers(idx 13) via the advertised GetHookPoints contract       | ✓ VERIFIED | `directx11.cpp` — `hkSwapChainPresent` (77-89), `hkResizeBuffers` onPre→orig→onPost (95-111), `tryInstall()` GetModuleHandle gl11_r/_d → GetProcAddress("GetHookPoints") → poll → DetourXS install off live vtbl `vtbl[dxgi_Present_Index/ResizeBuffers_Index]`. |
| 4   | One-backend-per-session: kickoff registered from the single owned site `graphics.cpp::hkInstall`, latched poll              | ✓ VERIFIED | `graphics.cpp:619` `directX11::kickoff();` beside `directX::detour()` (612), one-shot firstFire path; `kickoff()` (directx11.cpp:219-227) subscribes `pollThunk` via `subscribePrePresentCallback`; `s_installed` latch fronts tryInstall (117-120); thunk unsubscribes on latch (195-213). |
| 5   | Detection wired at `imgui_impl::setup()` via pure `selectBackend()`, DX9-default / DX11-on-positive, API-neutral           | ✓ VERIFIED | `imgui_impl.cpp:311-352` — DX9 entry (`get()==nullptr`) computes `gl11Present` (GetModuleHandleA), routes `selectBackend(gl11Present, false)`, sets dx9Singleton; DX11 entry (pre-installed) skips re-set. Step-2 `dx9Singleton()->init(nullptr)` guarded on `get()==dx9Singleton()` (370). |
| 6   | A one-shot diagnostic log names the selected backend (RNDR-03)                                                              | ✓ VERIFIED | `imgui_impl.cpp:327-351` — `sLoggedBackend` static-bool guards three messages: "Render backend: D3D11 (...)" / "D3D9 (default...)" / warning "gl11 detected but GetHookPoints absent...".                                                                  |
| 7   | The D-06 API-neutrality gate bans DX11/DXGI symbol forms; imgui_impl stays neutral                                          | ✓ VERIFIED | `ImguiApiNeutralityTests.cpp` — `dx11Tokens()` bans `ID3D11Device`, `directX11::`, `imgui_impl_dx11`, `IDXGISwapChain`, etc. (98-113); stripper-hygiene self-check (131-143). `[rndr01]` reads imgui_impl.cpp from disk at runtime (line 165). Lane green. |
| 8   | Device-free Catch2 layers exist + pass: vtbl-offset pin, detection unit, mock-dispatch, WARP harvest                       | ✓ VERIFIED | `Dx11VtblOffsetTests` (static_assert 8/13), `Dx11DetectionTests` (4-state truth table), `RenderBackendSeamTests` rndr02 mock-dispatch, `Dx11DummyDeviceHarvestTests` (WARP + no-leak, skip-on-absent). **Ran: 33 assertions / 4 cases PASS**; full `[graphics]` 121/4 PASS. |

**Score:** 8/8 code-level truths verified. Live D3D11 behavioral acceptance (success-criteria #1-3 LIVE legs) deferred to Phase 24.

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | Live D3D11 acceptance (D-22): overlay renders + RT-space input + single-backend + DXGI-resize survives on the live gl11_r.dll client (RNDR-02/03/04 live legs) | Phase 24 (EPA-01..04) | REQUIREMENTS.md EPA-03 explicitly decouples the DX11 kickoff from the SWGEmu-addressed `graphics::install` hook; live inject blocked by the pre-existing hardcoded-RVA crash in `createDetours()` (0x00401000), not a Phase 19 defect. Maintainer-approved deferral. |

### Required Artifacts

| Artifact                                                | Expected                                               | Status     | Details                                                                                          |
| ------------------------------------------------------- | ------------------------------------------------------ | ---------- | ----------------------------------------------------------------------------------------------- |
| `vcpkg.json`                                            | imgui dx11-binding feature                             | ✓ VERIFIED | `grep -c '"dx11-binding"'` == 1                                                                  |
| `UtinniCore/swg/graphics/render_backend.h`              | Dx11Backend decl + dx11Singleton(), forward-decls only | ✓ VERIFIED | `class Dx11Backend final` present; no `<d3d11.h>`                                                |
| `UtinniCore/swg/graphics/backend_select.h`              | neutral pure selectBackend(bool,bool)                  | ✓ VERIFIED | inline pure fn, names zero DX11 types                                                            |
| `UtinniCore/swg/graphics/directx11.h`                   | directX11:: free-fn decls + pinned vtbl constants      | ✓ VERIFIED | `namespace directX11`, `dxgi_Present_Index=8`/`dxgi_ResizeBuffers_Index=13`                      |
| `UtinniCore/swg/graphics/directx11.cpp`                 | DXGI hook tier + GetHookPoints consumer + kickoff      | ✓ VERIFIED | 253 lines; full hook tier, borrowed-ptr discipline, latched poll                                |
| `UtinniCore/swg/graphics/render_backend_dx11.cpp`       | Dx11Backend impl + dx11Singleton()                     | ✓ VERIFIED | 190 lines; 10 overrides + init + RTV rebind/recreate                                            |
| `UtinniCore/swg/graphics/graphics.cpp`                  | kickoff() registration from hkInstall                  | ✓ VERIFIED | `directX11::kickoff();` at line 619 (single owned site)                                          |
| `UtinniCore/swg/ui/imgui_impl.cpp`                      | one-backend detection + selection at setup()           | ✓ VERIFIED | selectBackend wiring; backend-conditional step-2; one-shot log; DX11/DXGI-neutral               |
| `Dx11VtblOffsetTests.cpp` / `Dx11DetectionTests.cpp`    | offset pin + detection unit                            | ✓ VERIFIED | registered in test vcxproj; both pass                                                            |
| `Dx11DummyDeviceHarvestTests.cpp`                       | WARP harvest + no-leak                                 | ✓ VERIFIED | registered; passes (or skips with log if WARP absent)                                            |

### Key Link Verification

| From                          | To                                              | Via                                          | Status  | Details                                                              |
| ----------------------------- | ----------------------------------------------- | -------------------------------------------- | ------- | ------------------------------------------------------------------- |
| graphics.cpp::hkInstall       | directX11::kickoff()                             | call beside directX::detour() (single site)  | ✓ WIRED | graphics.cpp:619; only kick-off site (utinni.cpp untouched)         |
| kickoff()                     | per-frame tryInstall poll                        | subscribePrePresentCallback(&pollThunk)      | ✓ WIRED | directx11.cpp:225; thunk calls tryInstall each frame until latched  |
| directx11.cpp hkResizeBuffers | render_backend::get()->onPreResize/onPostResize | seam dispatch around original ResizeBuffers  | ✓ WIRED | directx11.cpp:100/107, null-guarded, ordered before/after original  |
| render_backend_dx11.cpp newFrame | OMSetRenderTargets + ImGui_ImplDX11_NewFrame | per-frame flip-discard RTV rebind            | ✓ WIRED | render_backend_dx11.cpp:93/96                                       |
| directx11.cpp tryInstall      | gl11_r.dll GetHookPoints export                  | GetProcAddress + poll swapChain != null      | ✓ WIRED | directx11.cpp:132-151; graceful bail on absent export               |
| imgui_impl::setup detection   | render_backend::set(dx9/dx11 Singleton)          | selectBackend(gl11Present, resolved)         | ✓ WIRED | imgui_impl.cpp:319-326; entry-path disambiguation via get()         |

### Probe / Behavioral Spot-Checks (Catch2 — ran in this process)

| Behavior                              | Command                                                                    | Result                          | Status  |
| ------------------------------------- | ------------------------------------------------------------------------- | ------------------------------- | ------- |
| Phase-19 device-free layers           | `UtinniCore.Tests.exe "[dxgi],[rndr02],[rndr03],[detect],[offsets],[harvest]"` | All tests passed (33 assert/4)  | ✓ PASS  |
| Full graphics lane incl. D-06 gate    | `UtinniCore.Tests.exe "[graphics]"`                                        | All tests passed (121 assert/4) | ✓ PASS  |

_Test exe `bin/Release/UtinniCore.Tests.exe`; the `[rndr01]` neutrality gate reads `imgui_impl.cpp` from disk at runtime, so the pass reflects the current (post-Plan-03) file. UtinniCore.dll built 15:23 (same time as final imgui_impl.cpp edit) — the detection wiring compiles into the dll._

### Requirements Coverage

| Requirement | Source Plan       | Description                                                              | Status                                      | Evidence                                                                |
| ----------- | ----------------- | ----------------------------------------------------------------------- | ------------------------------------------- | ---------------------------------------------------------------------- |
| RNDR-02     | 19-01/02/03       | Overlay renders + RT-space input under D3D11 (Present idx 8, RTV rebind) | ✓ SATISFIED (code) / live deferred to Ph 24 | Dx11Backend + directx11.cpp hook tier; live render is the deferred leg  |
| RNDR-03     | 19-01/03          | Exactly one backend per session, auto-detected, one-shot log            | ✓ SATISFIED (code) / live deferred to Ph 24 | selectBackend + latched install + one-shot diagnostic; no dual context  |
| RNDR-04     | 19-02/03          | Overlay survives DXGI resize — RTV release/recreate, no INVALID_CALL     | ✓ SATISFIED (code) / live deferred to Ph 24 | hkResizeBuffers onPre→orig→onPost; never-Reset rule NOT carried over    |

_No orphaned requirements: REQUIREMENTS.md maps RNDR-02/03/04 to Phase 19 (all claimed by plans) and EPA-01..04 to the new Phase 24 (the deferred-live home)._

### Anti-Patterns Found

| File                | Line | Pattern | Severity | Impact                                                                                          |
| ------------------- | ---- | ------- | -------- | ----------------------------------------------------------------------------------------------- |
| imgui_impl.cpp      | 649  | `ToDo`  | ℹ️ Info  | PRE-EXISTING (git blame: 47b1f1d, 06-05 clang-format pass, May 25). Unrelated host-controls note, NOT Phase-19 code. Not a blocker. |

_No `TBD`/`FIXME`/`XXX` debt markers in any Phase-19-modified file. Borrowed-pointer discipline verified: no `Release()` on swapChain/device/context in directx11.cpp. The DX11 scene depth/color `(ImTextureID)0` returns in render_backend_dx11.cpp are a documented, intentional MVP (no DX11 depth-SRV this phase) — the seam contract is satisfied API-neutrally; NOT a stub that breaks the goal. render_backend_dx11.cpp/directx11.cpp correctly excluded from the NoDeviceReset guarded list (RTV `->Release()` not device `->Reset()`)._

### Human Verification Required

None for this code-level verification. The live D3D11 behavioral acceptance (maintainer injection) is **deferred to Phase 24**, not requested here — it is blocked by the pre-existing hardcoded-RVA crash, which Phase 24 (EPA, entry-point advertisement) unblocks. Per the maintainer-approved scoping decision, this is recorded as a deferred item, not a human-verification gate on Phase 19.

### Gaps Summary

No code defects found. Every code-level and CI-enforceable must-have is VERIFIED:
- The Dx11Backend twin, directX11 DXGI hook tier (Present idx 8 / ResizeBuffers idx 13), the GetHookPoints advertised-contract consumer, the single-owned kickoff from graphics.cpp::hkInstall, the latched one-backend-per-session detection at imgui_impl::setup() with a one-shot log, the true DXGI release/recreate resize, the borrowed-pointer discipline, the extended D-06 neutrality gate, and all four device-free Catch2 layers all EXIST, are WIRED, and PASS in the test process (33 + 121 assertions green).
- The DX11 hook path is correctly binary-agnostic (no hardcoded addresses; acquisition via gl11_r.dll GetHookPoints + swapchain vtbl).
- The only deferred item is the LIVE D3D11 behavioral acceptance (D-22), blocked by the pre-existing entry-point-advertisement / hardcoded-RVA limitation (crash at 0x00401000 in unrelated game-logic detours), now scoped as Phase 24 (EPA-01..04). This is explicitly NOT a Phase-19 code defect and is recorded as deferred, not gaps_found, per the maintainer-approved 2026-06-15 decision.

---

_Verified: 2026-06-15_
_Verifier: Claude (gsd-verifier)_
