---
phase: 18-render-backend-seam-dx9backend
plan: 02
subsystem: graphics
tags: [render-backend, d3d9, imgui, irenderbackend, seam, carve, catch2, source-gate, cppsharp]

# Dependency graph
requires:
  - phase: 18-render-backend-seam-dx9backend
    plan: 01
    provides: IRenderBackend 10-member vtable + non-virtual Dx9Backend::init + nullable get()/set()/dx9Singleton() seam (the carve target)
  - phase: 15-resid
    provides: NoDeviceResetTests.cpp [resid04] no-Reset gate (the no-Reset contract this carve preserves) + the source-grep-gate helper precedent
  - phase: 17-cppsharp-v145-hardening
    provides: CPPS-04 ABI gate (AbiSurfaceTests) the carve must not drift; the clang-11 <imgui.h> parse-AV constraint that shaped the header layout
provides:
  - imgui_impl.{cpp,h} fully DX9-API-neutral (D-05 purge, extended token set) — the ~1000-line overlay logic single-sourced behind the IRenderBackend vtable
  - The six former DX9 touch-points routed through render_backend::get() (null-guarded); setup(HWND) running the locked set->init->setSwgHwnd->Win32->WndProc->isSetup order
  - hkPresent device-stash + HWND-extract path feeding the seam (amendment 6 primary-source device)
  - D-06 structural source gate (ImguiApiNeutralityTests, [rndr01]) + shared SourceGateUtil.h — fails the build if any DX9 symbol reappears AND proves the seam was wired in (>=5 dispatch sites)
affects: [Phase 19 Dx11Backend (slots behind the same vtable without re-touching imgui_impl), RNDR-01 final acceptance]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Carve-onto-seam: route former concrete touch-points through a nullable runtime-polymorphic vtable, every per-frame dispatch null-guarded (`if (auto* b = get())`), accessors fold the null guard"
    - "Device-stash hand-off: hkPresent stashes the live device on the backend singleton so a DX9-API-neutral setup(HWND) can drive a device-bearing init() without naming a Direct3D type"
    - "imgui-dependency containment: keep <imgui.h> OUT of a UTINNI_API-bearing header so it never enters the CppSharp clang-11 parse graph (parse-AV avoidance)"
    - "Shared comment-stripped source grep-gate via a header-only helper namespace, with a planted-in-comment stripper self-check"

key-files:
  created:
    - UtinniCore.Tests/Graphics/SourceGateUtil.h
    - UtinniCore.Tests/Graphics/ImguiApiNeutralityTests.cpp
  modified:
    - UtinniCore/swg/ui/imgui_impl.cpp
    - UtinniCore/swg/ui/imgui_impl.h
    - UtinniCore/swg/graphics/directx9.cpp
    - UtinniCore/swg/graphics/render_backend.h
    - UtinniCore/swg/graphics/render_backend_dx9.cpp
    - UtinniCore.Tests/UtinniCore.Tests.vcxproj

key-decisions:
  - "Device routing for amendment 6 implemented via a stashDevice()/stashedDevice() pair on Dx9Backend (added beyond the plan's render_backend files_modified) — hkPresent stashes its live pDevice, init(nullptr) consumes it as primary; keeps imgui_impl.cpp free of any Direct3D device type"
  - "render_backend.h is included ONLY in imgui_impl.cpp, NOT in imgui_impl.h — imgui_impl.h takes <Windows.h> for HWND only, so the seam's <imgui.h> dependency never enters the CppSharp parse graph (the AV that forced render_backend.h's discovery exclusion in 18-01)"
  - "Tests-window block gated on the stricter sceneDepthTexture()!=0 (intentional dev-only behavior change, amendment 4) — Release (enableInternalUi=true) unaffected"

requirements-completed: []   # RNDR-01 stays OPEN pending the D-08 maintainer live-smoke (Task 3)

# Metrics
duration: ~1h
completed: 2026-06-15
---

# Phase 18 Plan 02: imgui_impl Carve onto the IRenderBackend Seam Summary

**The ~1000-line API-neutral ImGui overlay is now single-sourced behind the IRenderBackend vtable: imgui_impl.{cpp,h} are fully DX9-API-neutral (extended D-05 purge), the six former DX9 touch-points route through the null-guarded seam, setup() takes an HWND and runs the locked Win32-before-DX9 / set-before-isSetup order, and a D-06 structural gate fails the build if any DX9 symbol reappears or the seam is not wired in. Built Release/x86 with the full native Catch2 suite green. The final RNDR-01 acceptance — the D-08 maintainer live-smoke — is PENDING (no headless path).**

## Performance
- **Duration:** ~1h
- **Completed (code tasks):** 2026-06-15
- **Tasks:** 2 / 3 code-complete; Task 3 is the maintainer-only D-08 live-smoke (PENDING)
- **Files modified:** 7 (2 created, 5 modified)

## Accomplishments
- **D-05 full purge (extended token set):** imgui_impl.{cpp,h} contain ZERO `<d3d9.h>`, `<imgui_impl_dx9.h>`, `"swg/graphics/directx9.h"`, `IDirect3DDevice9`, `LPDIRECT3DDEVICE9`/`LPDIRECT3D`, `D3DDEVICE_CREATION_PARAMETERS`, `ImGui_ImplDX9_`, or `directX::` tokens (verified raw-grep clean AND via the comment-stripped D-06 gate).
- **Six touch-points carved:** `newFrame()` (was `ImGui_ImplDX9_NewFrame`) and `renderDrawData()` (was `ImGui_ImplDX9_RenderDrawData`) each behind an `if (auto* b = render_backend::get())` guard; the three former `directX::getDepthTexture()` reach-ins (DrawDepthWindow / DrawColorWindow / Tests-window stage slider) fold into `sceneDepthTexture()` / `sceneColorTexture()` / `sceneDepthStage()`+`setSceneDepthStage()`; the former `ImGui_ImplDX9_Init` moved into the Plan-01 `Dx9Backend::init()` driven from `setup()`.
- **Locked setup() order:** `setup(HWND)` runs (1) `render_backend::set(dx9Singleton())` -> (2) `dx9Singleton()->init(nullptr)` [stashed live device primary, amendment 6] -> (3) `Client::setSwgHwnd(hwnd)` [Issue #10 reparent preserved] -> (4) `ImGui_ImplWin32_Init(hwnd)` [Win32 before DX9] -> (6) WndProc subclass install -> (7) `isSetup = true` LAST. `set()` precedes `isSetup=true`, so no seam call site can fire through the isSetup gate with a null backend. Guards bail (no overlay) on a null HWND or a null backend window.
- **hkPresent hand-off:** extracts `hFocusWindow` from its live `pDevice` (DX9 tier is the right place to touch a device type), stashes that same live device on the Dx9 singleton so `init()` consumes it as the PRIMARY source, then calls `setup(hwnd)`. Guards against a null device / failed `GetCreationParameters` / null focus window. The render()-before-setup() ordering in hkPresent is PRESERVED.
- **D-06 structural gate:** `ImguiApiNeutralityTests.cpp` ([rndr01][graphics]) asserts the extended token set counts to 0 in both files after comment-strip, PLUS the amendment-8 seam-dispatch-presence (`render_backend::get()->` >= 5 in imgui_impl.cpp). Includes the planted-in-comment stripper self-check; gates on concrete symbol forms only (never bare "D3D9"/"DX9", Pitfall 5). Shared helpers live in the new `SourceGateUtil.h` (`namespace source_gate`); `NoDeviceResetTests.cpp` [resid04] left untouched with its own file-local copies (amendment 10).
- **Build + tests green:** MSBuild `Utinni.sln /p:Configuration=Release /p:Platform=x86` exits 0. Native Catch2 suite: 29 cases / 136 assertions all pass; `[rndr01]` = 2 cases / 52 assertions (D-07 seam dispatch + new D-06 gate); `[resid04]` = 8 assertions (no-Reset contract, untouched). CPPS-04 ABI gate passes against a freshly-regenerated surface (see Issues).

## Task Commits
1. **Task 1: Carve imgui_impl onto the seam + full D-05 purge + locked setup order** — `6625636` (feat)
2. **Task 2: D-06 API-neutrality source gate (extended + seam-presence) + SourceGateUtil.h** — `672bac0` (test)
3. **Task 3: D-08 maintainer live-smoke (final RNDR-01 acceptance)** — PENDING (maintainer-only checkpoint; no commit)

**Plan metadata:** (this commit) `docs(18-02): code-complete imgui_impl carve; D-08 live-smoke pending`

## Files Created/Modified
- `UtinniCore/swg/ui/imgui_impl.cpp` — six touch-points routed through `render_backend::get()` (7 literal dispatch sites + 2 null-guarded per-frame dispatches); `setup(HWND)` locked-order body; DX9-binding-free; Issue #11 WndProc subclass + RT-space AddMousePosEvent + DirectInput arbitration + renderCallbacks dispatchSnapshot + gizmo namespace all preserved verbatim.
- `UtinniCore/swg/ui/imgui_impl.h` — dropped `<d3d9.h>`; `setup(HWND)` signature; takes `<Windows.h>` for HWND only (NOT render_backend.h) so `<imgui.h>` stays out of the CppSharp parse graph.
- `UtinniCore/swg/graphics/directx9.cpp` — hkPresent extracts HWND from the live device + stashes the device on the Dx9 singleton + calls `setup(hwnd)` with null/param guards; added `#include "render_backend.h"`. hkReset's Invalidate/Create device-objects calls UNCHANGED (stay in the DX9 tier).
- `UtinniCore/swg/graphics/render_backend.h` — added the non-virtual `stashDevice()`/`stashedDevice()` pair + private `m_stashedDevice` to `Dx9Backend` (amendment-6 device hand-off; off the vtable, zero managed surface).
- `UtinniCore/swg/graphics/render_backend_dx9.cpp` — `init()` now consumes the stashed device as PRIMARY (then `directX::getDevice()` fallback); added the stash accessors.
- `UtinniCore.Tests/Graphics/SourceGateUtil.h` — NEW; `namespace source_gate` with `repoRootFromThisFile`/`readFile`/`stripComments`/`countSubstr`/`readStripped`.
- `UtinniCore.Tests/Graphics/ImguiApiNeutralityTests.cpp` — NEW; the D-06 gate.
- `UtinniCore.Tests/UtinniCore.Tests.vcxproj` — phase-tagged `<ClCompile>` for the gate + `<ClInclude>` for the helper header.

## Decisions Made
- **stashDevice() hand-off for amendment 6.** The plan said hkPresent "may stash the live pDevice for init() to consume." Implemented exactly that: a non-virtual `stashDevice()`/`stashedDevice()` pair on `Dx9Backend` (off the vtable, zero managed surface), so `setup(HWND)` can call `init(nullptr)` and `init()` consumes the stashed live device as its primary source — keeping imgui_impl.cpp free of any Direct3D device type while honoring "pDevice from hkPresent is primary, getDevice() is fallback only."
- **render_backend.h included in imgui_impl.cpp, not imgui_impl.h.** imgui_impl.h is a UTINNI_API-bearing header that CppSharp discovers and parses. render_backend.h pulls `<imgui.h>`, which clang-11 AccessViolation-faults on (the exact reason 18-01 excluded render_backend.h from discovery). Including it in imgui_impl.h would have dragged `<imgui.h>` into imgui_impl.h's parse and re-triggered the AV (observed live during this plan — see Issues). imgui_impl.h needs only `HWND`, so it takes `<Windows.h>`; the seam types are needed only in the .cpp.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] render_backend.h kept out of imgui_impl.h to avoid the CppSharp clang-11 parse AccessViolation**
- **Found during:** Task 1 first build.
- **Issue:** The plan's action said to `ADD #include "swg/graphics/render_backend.h"` to imgui_impl.h. Doing so dragged `<imgui.h>` (pulled by render_backend.h) into imgui_impl.h's CppSharp parse graph; the post-build `UtinniCoreDotNetGen.exe` gen step crashed with `-1073741819` (AccessViolation) inside the clang-11 parser — the same `<imgui.h>` parse-AV that forced render_backend.h's discovery exclusion in 18-01. The C++ compile itself succeeded; only the binding-gen post-build step crashed.
- **Fix:** imgui_impl.h takes `<Windows.h>` for `HWND` only; `#include "swg/graphics/render_backend.h"` lives in imgui_impl.cpp (and directx9.cpp) where the seam types are actually used. This keeps `<imgui.h>` out of any CppSharp-parsed header.
- **Files modified:** UtinniCore/swg/ui/imgui_impl.h, UtinniCore/swg/ui/imgui_impl.cpp, UtinniCore/swg/graphics/directx9.cpp
- **Verification:** Rebuild Release/x86 exits 0; gen completes without AV; `[rndr01]`/`[resid04]`/full native suite green.
- **Committed in:** `6625636` (Task 1 commit)

**2. [Rule 3 - Blocking] Added stashDevice()/stashedDevice() to Dx9Backend (beyond the plan's render_backend files_modified)**
- **Found during:** Task 1 (setup carve).
- **Issue:** Amendment 6 requires the live pDevice from hkPresent to be init()'s PRIMARY source, but `setup(HWND)` cannot name a Direct3D device type (D-05 gate) and cannot call `directX::getDevice()` (also gated). Without a hand-off, init() could only fall back to `directX::getDevice()`, demoting hkPresent's pDevice from primary to fallback.
- **Fix:** A non-virtual `stashDevice()`/`stashedDevice()` pair on `Dx9Backend` (off the IRenderBackend vtable, no managed surface). hkPresent stashes its live device; `init(nullptr)` consumes the stash first, `directX::getDevice()` only if the stash is null. The seam header (render_backend.h) was modified beyond the plan's `files_modified` list to add these.
- **Files modified:** UtinniCore/swg/graphics/render_backend.h, UtinniCore/swg/graphics/render_backend_dx9.cpp
- **Impact:** No vtable change (10 pure virtuals unchanged), no managed/export surface, no behavior change for Phase 19 (Dx11 init signature still differs and is off the vtable). Purely the device hand-off plumbing the plan explicitly sanctioned ("hkPresent may stash the live pDevice").
- **Committed in:** `6625636` (Task 1 commit)

**Total deviations:** 2 auto-fixed (both Rule 3 - blocking). One added symbol pair on the Plan-01 seam header, sanctioned by the plan's own amendment-6 hand-off language.

## Known Stubs
None. The seam dispatches to the live-verified Dx9Backend (which wraps directX:: verbatim). No empty/mock data paths introduced.

## Issues Encountered
- **CPPS-04 ABI gate failure was a STALE committed Generated/UtinniCore.cs, NOT a regression from this carve.** Running the managed suite with the committed Generated file showed `AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline` REMOVED 20 blocks. This is the Phase-17 documented gotcha: the incremental build skips the post-build gen, leaving the committed Generated file stale relative to the blessed baseline. To verify my carve is ABI-safe I ran `UtinniCoreDotNetGen.exe` explicitly to regenerate the surface fresh, then re-ran the ABI test against that fresh file — it **PASSED** (0 added / 0 removed). My changes are ABI-neutral: `setup()` is non-UTINNI_API (signature change is ABI-safe), render_backend.h is excluded from discovery, and the stash accessors add no managed surface. Per AGENTS.md, Generated/UtinniCore.cs was reverted (never committed); CI runs a clean gen so the ABI lane passes there.
- Build/test ran INLINE on the main tree (worktrees OFF). No fix-attempt-limit reached.

## TDD Gate Compliance
Plan type is `execute` (not `tdd`). Task 1 is a `feat` carve; Task 2 is the structural `test` gate (added after the carve it guards — it is a source-presence/absence gate, not a behavior test, so a RED-before-implementation split does not apply). No gate-compliance warning required.

## D-08 Maintainer Live-Smoke — PENDING (final RNDR-01 acceptance)
RNDR-01 is **NOT YET COMPLETE.** The carve is CI-green but the final acceptance is the maintainer-only live-smoke (no headless path exists — CI cannot inject into a live SWG.exe). RNDR-01 stays OPEN in REQUIREMENTS.md until the maintainer signs off. See the checkpoint returned to the orchestrator for the exact live-smoke steps, PASS criteria, and the documented baseline (landing naked after a TJT-driven scene change is BASELINE, not a regression). This mirrors how Phase 15 gated its live re-verify.

## Self-Check: PASSED
- `UtinniCore/swg/ui/imgui_impl.cpp` — FOUND
- `UtinniCore/swg/ui/imgui_impl.h` — FOUND
- `UtinniCore/swg/graphics/directx9.cpp` — FOUND
- `UtinniCore.Tests/Graphics/SourceGateUtil.h` — FOUND
- `UtinniCore.Tests/Graphics/ImguiApiNeutralityTests.cpp` — FOUND
- Commit `6625636` (Task 1 carve) — FOUND
- Commit `672bac0` (Task 2 D-06 gate) — FOUND
- `Generated/UtinniCore.cs` — clean (never committed)
- `[rndr01]` + `[resid04]` + full native suite — green; CPPS-04 ABI gate green against fresh regen

---
*Phase: 18-render-backend-seam-dx9backend*
*Code-complete: 2026-06-15 — RNDR-01 pending the D-08 maintainer live-smoke*
