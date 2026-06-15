---
phase: 18-render-backend-seam-dx9backend
verified: 2026-06-15T13:00:00Z
status: passed
score: 4/4 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: none
  previous_score: n/a
---

# Phase 18: Render-Backend Seam + Dx9Backend Verification Report

**Phase Goal:** The ImGui overlay renders through a single `IRenderBackend` seam, with the existing D3D9 path behaviorally unchanged — so a risky D3D11 twin can be added later without forking the shared overlay logic.
**Verified:** 2026-06-15T13:00:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth (ROADMAP success criterion) | Status | Evidence |
|---|-----------------------------------|--------|----------|
| 1 | Overlay still renders + takes input in a live D3D9 SWG session — behavior preserved | ✓ VERIFIED | D-08 maintainer live-smoke PERFORMED + APPROVED 2026-06-15 (overlay renders through the seam, input works, Issue #11 chat routing works, no crash/Reset through scene change). RNDR-01 marked `[x]` in REQUIREMENTS.md:36. Resize edge-cases tracked as pre-existing non-blocking todo (`.planning/todos/pending/swg-window-resize-fullscreen-edge-cases.md`), not a Phase 18 regression. |
| 2 | `render_backend.{h,cpp}` seam exists in `swg/graphics/` (6 ROADMAP members) with `directx9.cpp` carved into `Dx9Backend` | ✓ VERIFIED | `render_backend.h` declares 10-member `IRenderBackend` ABC (6 ROADMAP-named + 4 A2 accessors); `Dx9Backend final` overrides all 10. Option-A split: `render_backend.cpp` (DX9-free get/set/s_active) + `render_backend_dx9.cpp` (Dx9Backend impl, `s_dx9Backend`, `dx9Singleton`, single `ImGui_ImplDX9_Init`). Wraps `directX::` verbatim. |
| 3 | ~1000-line API-neutral overlay logic single-sourced in `imgui_impl.cpp`; only seam dispatch sites call through the seam | ✓ VERIFIED | imgui_impl.cpp raw-grep for DX9 token set (`IDirect3DDevice9`/`ImGui_ImplDX9_`/`directX::`/`<d3d9.h>`/`imgui_impl_dx9`/`D3DDEVICE_CREATION_PARAMETERS`) = ZERO (even in comments). WndProc subclass (`hkWndProcHandler`), Issue #11 routing, RT-space `AddMousePosEvent`, gizmo, renderCallbacks dispatchSnapshot all present and untouched. 12 seam dispatch sites; D-06 gate asserts `render_backend::get()->` >= 5. |
| 4 | No-Reset / Present-stretch D3D9 contract preserved verbatim — no `Reset` introduced | ✓ VERIFIED | Zero `->Reset(`/`.Reset(` in imgui_impl.cpp, render_backend.cpp, render_backend_dx9.cpp. `onPreResize`/`onPostResize` are honest `{}` no-ops. hkReset's `ImGui_ImplDX9_InvalidateDeviceObjects`/`CreateDeviceObjects` stay in directx9.cpp (the only `reset()` call is the original SWG detour trampoline, pre-existing). [resid04] gate passes. |

**Score:** 4/4 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `UtinniCore/swg/graphics/render_backend.h` | 10-member ABC + non-virtual `Dx9Backend::init` + get/set/dx9Singleton; zero DX9 bindings; zero UTINNI_API | ✓ VERIFIED | 10 `= 0;` pure virtuals; `struct IDirect3DDevice9` forward-declared (no `<d3d9.h>`); `UTINNI_API` count = 0; `init(IDirect3DDevice9*)` + `stashDevice`/`stashedDevice` non-virtual off-vtable. |
| `UtinniCore/swg/graphics/render_backend.cpp` | DX9-free seam half (s_active + get/set) | ✓ VERIFIED | Includes only `render_backend.h`; `static IRenderBackend* s_active = nullptr`; get/set only. Compiled directly into test exe. |
| `UtinniCore/swg/graphics/render_backend_dx9.cpp` | Dx9Backend impl wrapping directX:: verbatim; single ImGui_ImplDX9_Init; static-storage singleton; no Reset | ✓ VERIFIED | `ImGui_ImplDX9_Init` count = 1; `static Dx9Backend s_dx9Backend`; zero `make_unique`/`new Dx9Backend`/`std::function`; no-op resize hooks; init() consumes stashed device (primary) then `directX::getDevice()` (fallback). |
| `UtinniCore/swg/ui/imgui_impl.cpp` | Six touch-points routed through seam; setup(HWND) locked order; DX9-neutral | ✓ VERIFIED | setup() order: set(283) → init(nullptr)(292) → setSwgHwnd(302) → ImGui_ImplWin32_Init(307) → WndProc(315) → isSetup=true(351). newFrame/renderDrawData null-guarded. Tests-window stricter `sceneDepthTexture()!=0` guard documented. |
| `UtinniCore/swg/ui/imgui_impl.h` | DX9-free header; setup(HWND); no render_backend.h (CppSharp parse-AV avoidance) | ✓ VERIFIED | `#include <Windows.h>` only for HWND; `extern void setup(HWND hwnd)`; no `<d3d9.h>`, no `IDirect3DDevice9`. |
| `UtinniCore.Tests/Graphics/RenderBackendSeamTests.cpp` | D-07 mock dispatch (10 members + post-restore null) | ✓ VERIFIED | MockBackend overrides all 10; drives each via `get()->`; asserts each counter == 1; `get()==nullptr` after restore. Runs device-free. |
| `UtinniCore.Tests/Graphics/ImguiApiNeutralityTests.cpp` | D-06 source gate (extended tokens + seam-presence) | ✓ VERIFIED | Extended 13-token set asserted == 0 in both files (comment-stripped); stripper self-check SECTION; seam-presence >= 5; gates concrete forms only (not bare "D3D9"). |
| `UtinniCore.Tests/Graphics/SourceGateUtil.h` | Shared comment-strip helpers | ✓ VERIFIED | `namespace source_gate` with stripComments/readFile/repoRootFromThisFile/countSubstr/readStripped. NoDeviceResetTests.cpp keeps own copies (untouched, last commit Phase 15). |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| imgui_impl.cpp | render_backend::get() | null-guarded newFrame/renderDrawData/sceneDepth/Color/Stage dispatch | ✓ WIRED | 12 `render_backend::` sites; 2 `if (auto* b = render_backend::get())` per-frame guards. |
| directx9.cpp hkPresent | imgui_impl::setup(HWND) | extract hFocusWindow + stash live pDevice on Dx9 singleton, then setup(hwnd) | ✓ WIRED | hkPresent:369-377 guards null device + GetCreationParameters + null hFocusWindow; stashDevice(pDevice) before setup(). |
| render_backend_dx9.cpp | directX:: / ImGui_ImplDX9_* | Dx9Backend members + init() forward verbatim | ✓ WIRED | sceneDepth/Color/Stage forward to `directX::getDepthTexture()`; newFrame/renderDrawData to `ImGui_ImplDX9_*`; init() owns single ImGui_ImplDX9_Init. |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| D-07 seam dispatch + D-06 gate + [resid04] no-Reset | `UtinniCore.Tests.exe "[rndr01],[resid04]"` | All tests passed (60 assertions in 3 test cases) | ✓ PASS |
| Full native Catch2 suite (no regression) | `UtinniCore.Tests.exe` | All tests passed (136 assertions in 29 test cases) | ✓ PASS |
| Generated/UtinniCore.cs not committed | `git status --porcelain` | clean | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| RNDR-01 | 18-01, 18-02 | Overlay renders through single IRenderBackend seam, D3D9 behaviorally unchanged | ✓ SATISFIED | Seam exists (10-member ABC, Dx9Backend wrap), imgui_impl DX9-neutral (D-06 gate green), no-Reset preserved ([resid04] green), D-08 live-smoke approved. Marked `[x]` REQUIREMENTS.md:36. RNDR-02/03/04 correctly deferred to Phase 19. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| imgui_impl.cpp | 353-368 | DIAG one-shot debug probe (Phase 06 era, demoted info→debug, one-shot guarded) | ℹ️ Info | Pre-existing diagnostic, dormant in normal runs; not Phase 18 debt. No TBD/FIXME/XXX markers in any Phase-18-modified file. |

No 🛑 blockers. No unreferenced debt markers (TBD/FIXME/XXX) introduced. The `Dx9Backend` no-op resize hooks are intentional D-03 no-ops, not stubs (documented; the live-verified render path remains the directX:: wrap).

### Human Verification Required

None. The only human-gated item (D-08 live-smoke) was already PERFORMED and APPROVED by the maintainer on 2026-06-15 per the verification context. The window-resize edge cases are a pre-existing, deliberately-deferred non-blocking todo (a pure-refactor phase cannot regress them), not a new untested Phase 18 claim.

### Gaps Summary

No gaps. All four ROADMAP success criteria are observably true in the codebase:
1. Live D3D9 render/input preserved — maintainer smoke approved; RNDR-01 closed in REQUIREMENTS.md.
2. `render_backend.{h,cpp}` (+ Option-A `render_backend_dx9.cpp`) seam exists with the 10-member vtable and Dx9Backend wrapping directX:: verbatim.
3. imgui_impl.{cpp,h} are fully DX9-API-neutral (raw-grep AND comment-stripped D-06 gate both clean); the API-neutral overlay logic (Issue #11 routing, RT-space, gizmo, renderCallbacks) is single-sourced and untouched; only seam dispatch sites call through.
4. No-Reset contract verbatim — zero new Reset, honest no-op resize hooks, hkReset Invalidate/Create stay in the DX9 tier, [resid04] gate green.

The documented deviations (Option-A two-TU split, the `stashDevice()`/`stashedDevice()` device hand-off, render_backend.h CppSharp discovery exclusion, render_backend.h kept out of imgui_impl.h) are all structural accommodations that preserve the seam's public shape, the 10-member vtable, the zero-export contract, and the live D3D9 behavior. They were verified against the actual code, not just the SUMMARYs, and each is sound. CPPS-04 ABI gate is unaffected (zero UTINNI_API on the seam header; directx9.h export signatures last touched in Phase 06).

---

_Verified: 2026-06-15T13:00:00Z_
_Verifier: Claude (gsd-verifier)_
