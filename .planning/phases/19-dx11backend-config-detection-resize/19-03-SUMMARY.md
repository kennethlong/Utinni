---
phase: 19-dx11backend-config-detection-resize
plan: 03
subsystem: graphics
tags: [dx11, dxgi, imgui, render-backend, detection, backend-select, api-neutral, checkpoint]

# Dependency graph
requires:
  - phase: 19-dx11backend-config-detection-resize
    plan: 01
    provides: backend_select.h pure selectBackend(bool,bool) + dx11Singleton() decl + D-06 gate extended to DX11/DXGI tokens + Dx11DetectionTests
  - phase: 19-dx11backend-config-detection-resize
    plan: 02
    provides: directX11:: DXGI hook tier + tryInstall() (installs dx11Singleton() + init(device,context) BEFORE setup) + directX11::kickoff() from graphics.cpp::hkInstall
  - phase: 18-render-backend-seam
    provides: IRenderBackend 10-vtable seam + setup(HWND) seam + render_backend::get()/set()/dx9Singleton()
provides:
  - one-backend-per-session detection wired at imgui_impl::setup() (DX9 entry runs selectBackend; DX11 entry arrives pre-installed)
  - one-shot RNDR-03 diagnostic naming the chosen backend (info D3D11 / info default-D3D9 / warning gl11-without-contract)
  - backend-conditional step-2 device init (dx9Singleton()->init(nullptr) GUARDED on installed backend == dx9Singleton(); DX11 path skips it)
affects: [Phase 20+ (overlay live on whichever backend the client advertises); the maintainer live-smoke gate D-22 (RNDR-02/03/04)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Entry-path disambiguation via render_backend::get() (nullptr=DX9 entry / non-null-non-dx9=DX11 entry) keeps imgui_impl DX11/DXGI-symbol-free -- no hook-point flag read that would force a tier-header include"
    - "Pure-decision routing: the DX9 entry path still routes through selectBackend() (testable contract, Dx11DetectionTests) even though its contract bit is necessarily false in this TU"

key-files:
  created: []
  modified:
    - UtinniCore/swg/ui/imgui_impl.cpp

key-decisions:
  - "imgui_impl::setup() distinguishes the two entry paths by render_backend::get() -- it does NOT read any DXGI hook-point flag (that would force the DX11 tier header include and break D-06 neutrality)"
  - "On the DX9 entry path the GetHookPoints contract bit passed to selectBackend() is hard-false: a ready DXGI swapchain would have entered via the hook-tier tryInstall() path with the dx11 backend already installed, never via the DX9 hkPresent path"
  - "Step-2 dx9Singleton()->init(nullptr) + the WR-01 bail-on-null teardown is guarded on the installed backend being the dx9 singleton; the DX11 path skips it (device init already ran in tryInstall) and runs the HWND-bearing tail with the swapchain-derived hwnd"

patterns-established:
  - "One-backend-per-session selection single-sourced at setup() behind the isSetup/CreateContext latch (one context); the DX11 install poll + its kick-off stay owned by the hook tier + graphics.cpp::hkInstall (Plan 02)"

requirements-completed: [RNDR-03]
requirements-pending-live-smoke: [RNDR-02, RNDR-04]

# Metrics
duration: ~20min
completed: 2026-06-15
status: checkpoint-blocking-human
---

# Phase 19 Plan 03: One-Backend-Per-Session Detection + Live-Smoke Gate Summary

**Wired the one-backend-per-session detection at `imgui_impl::setup()`: the DX9 entry path routes through the pure `selectBackend()` and installs `dx9Singleton()` with a one-shot RNDR-03 diagnostic; the DX11 entry path (the hook tier's `tryInstall()` already installed `dx11Singleton()` + ran `init(device,context)`) is detected via `render_backend::get()`, skips the DX9 step-2 device init, and runs the HWND-bearing tail with the swapchain-derived hwnd — all while keeping `imgui_impl.cpp` DX11/DXGI-symbol-free (D-06 gate green). Task 2 is the maintainer-only live-smoke (D-22) — STOPPED at the blocking-human checkpoint.**

## Status: CHECKPOINT (blocking-human)

This plan is `autonomous: false`. Task 1 (the autonomous detection wiring) is **complete and committed**. Task 2 is a `checkpoint:human-verify gate="blocking-human"` — the maintainer-only live-SWG smoke on the live 32-bit D3D11 `gl11_r.dll` client. There is no headless/subagent path for live injection (AGENTS.md build-wave constraint), so execution **STOPS here** awaiting the maintainer sign-off.

## Performance

- **Duration:** ~20 min (autonomous portion)
- **Completed (Task 1):** 2026-06-15
- **Tasks:** 2 (1 auto complete + 1 blocking-human checkpoint pending)
- **Files modified:** 1

## Accomplishments (Task 1)
- **Detection at `setup()`:** replaced the unconditional `render_backend::set(dx9Singleton())` (former line 283) with a two-path selection keyed on `render_backend::get()`:
  - **DX9 entry** (`get() == nullptr`): computes `gl11Present` via plain `GetModuleHandleA("gl11_r.dll")`/`("gl11_d.dll")` (Win32 strings, D-06-safe), routes the decision through the pure `selectBackend(gl11Present, /*getHookPointsResolved=*/false)`, and installs `dx9Singleton()`. The contract bit is hard-false on this entry path (a ready DXGI swapchain enters via the hook-tier `tryInstall()` path instead).
  - **DX11 entry** (`get() != nullptr` and `!= dx9Singleton()`): the hook tier's `tryInstall()` already did `set(dx11Singleton())` + `init(device, context)` before calling `setup(hwnd)`. `setup()` does NOT re-set the backend.
- **One-shot RNDR-03 diagnostic** (static-bool guarded, single source): `info "Render backend: D3D11 (gl11 detected, GetHookPoints advertised)"` on the DX11 entry; `info "Render backend: D3D9 (default; no gl11 detected)"` / `warning "gl11 detected but GetHookPoints absent/not-ready; defaulting D3D9"` on the DX9 entry.
- **Backend-conditional step-2 device init:** the `dx9Singleton()->init(nullptr)` + the WR-01 bail-on-null teardown (`set(nullptr)` + `DestroyContext()`) is now **guarded** on `render_backend::get() == render_backend::dx9Singleton()`. On the DX11 path the block is skipped — the DX11 device init already happened in `tryInstall`, and the HWND is the swapchain-derived one passed into `setup()`. This is the Plan-02 hand-off (`setup()` must NOT call `dx9Singleton()->init(nullptr)` in a D3D11 client — there is no D3D9 device; it would bail and leave a dead overlay).
- **HWND-bearing tail unchanged + single-sourced** for both backends: `setSwgHwnd` + `ImGui_ImplWin32_Init` + the docking/WantCapture flags + the WndProc subclass + font/style + `isSetup = true` all run with the live hwnd, not gated to DX9.
- **API-neutrality preserved:** `imgui_impl.cpp` includes only the pure `backend_select.h` (names zero DX11/DXGI symbol); it never includes the DX11 tier header nor names a DX11/DXGI type. The extended D-06 gate (`[rndr01]`) stays green.

## Task Commits

1. **Task 1: One-backend-per-session detection at setup()** — `59cf1a6` (feat)
2. **Task 2: Maintainer live-smoke (RNDR-02/03/04, D-22)** — PENDING (blocking-human checkpoint; no headless path)

## Files Created/Modified
- `UtinniCore/swg/ui/imgui_impl.cpp` — added `#include "swg/graphics/backend_select.h"`; refactored `setup()` step-1 (entry-path detection + selection + one-shot log) and step-2 (backend-conditional DX9 device init); the HWND-bearing tail is untouched in body.

## Verification (CI-enforceable layers — all green)
- `grep -c "directx11.h" imgui_impl.cpp` == **0**; `grep -c "directX11::"` == **0**; `grep -c "ID3D11"` == **0**; `grep -c "IDXGISwapChain"` == **0**; `grep -c "imgui_impl_dx11"` == **0**.
- `grep -c "selectBackend" imgui_impl.cpp` == **3** (decl include comment + call + comment); `grep -c "render_backend::get()->"` == **7** (≥5 seam-dispatch gate green).
- `git status` shows **only** `imgui_impl.cpp` changed — no `utinni.cpp`, no `graphics.cpp` (the kick-off is Plan 02's; this plan owns no kick-off edit).
- **UtinniCore.dll builds green** (x86 Release, inline via `Utinni.sln -t:UtinniCore`).
- **`[graphics]` lane green:** 121 assertions / 4 cases.
- **Extended render/offset/neutrality lane green:** `[rndr01],[rndr02],[rndr03],[dxgi],[detect],[offsets],[resid04]` — 138 assertions / 7 cases (D-06 neutrality gate `[rndr01]` included).
- `Generated/UtinniCore.cs` checked out after each CppSharp gen (never committed).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Grep-gate hygiene] setup() rationale comments named gated tokens `directx11.h` / `directX11::`**
- **Found during:** Task 1 (post-edit acceptance grep)
- **Issue:** The plan's acceptance criteria require `grep -c "directx11.h" imgui_impl.cpp == 0` and `grep -c "directX11:: imgui_impl.cpp == 0` (literal greps, per `feedback_gsd_grep_gate_hygiene`). My first-draft rationale comments legitimately referenced the hook-tier header (`directx11.h`) and `directX11::tryInstall` to explain WHY the include stays out — tripping the literal grep gates even though the comment-stripping D-06 unit test (`ImguiApiNeutralityTests`) passed (it strips comments before counting).
- **Fix:** Reworded three comment lines to describe the hook tier without the gated tokens ("the directX11 tier .h" / "the DX11 tier header" / "the hook tier's install poll"). Comment-only; no logic/build change.
- **Files modified:** UtinniCore/swg/ui/imgui_impl.cpp
- **Verification:** both literal greps now return 0; rebuilt UtinniCore + tests green; `[graphics]` re-run green (121 assertions / 4 cases).
- **Committed in:** `59cf1a6`

---

**Total deviations:** 1 auto-fixed (Rule 1 grep-gate hygiene). Mechanical comment wording; no scope creep; no architectural change. Plan executed exactly as written otherwise.

## Authentication Gates
None.

## Known Stubs
None introduced by this plan. (The DX11 scene depth/color `(ImTextureID)0` MVP stub lives in Plan 02's `render_backend_dx11.cpp` and is documented there — not a Plan-03 surface.)

## Next Phase Readiness
- **Task 2 (maintainer live-smoke, D-22)** is the only remaining item in this plan and the phase's final acceptance gate. It cannot be automated (no headless live-injection path). It requires a built `gl11_r.dll` exporting `GetHookPoints` (pinned swg-client-v2 @ 056632a, WM_SIZE path landed @ 2d01b0cb5) staged in the live 32-bit D3D11 SWG-Source client, then maintainer injection to confirm RNDR-02 (render + RT-space input), RNDR-03 (single backend + one-shot log + no doubled context/input), and RNDR-04 (WM_DISPLAYCHANGE + embed/window resize survive, RTV release/recreate, no `DXGI_ERROR_INVALID_CALL`).
- All CI-enforceable layers (the `[graphics]` Catch2 lane + the extended D-06 neutrality gate + the offset/detection/mock-dispatch fences) are green.

## Self-Check: PASSED

- File: `UtinniCore/swg/ui/imgui_impl.cpp` — FOUND (modified).
- File: `.planning/phases/19-dx11backend-config-detection-resize/19-03-SUMMARY.md` — FOUND.
- Commit: `59cf1a6` — verified present in git history.
- Grep gates (directx11.h / directX11:: / ID3D11 / IDXGISwapChain / imgui_impl_dx11) all return 0; selectBackend present; seam dispatch ≥5; `[graphics]` + extended lanes green.

---
*Phase: 19-dx11backend-config-detection-resize*
*Status: Task 1 complete + committed; Task 2 = blocking-human live-smoke checkpoint (PENDING)*
*Completed (autonomous portion): 2026-06-15*
