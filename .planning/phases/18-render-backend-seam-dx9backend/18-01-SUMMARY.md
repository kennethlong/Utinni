---
phase: 18-render-backend-seam-dx9backend
plan: 01
subsystem: graphics
tags: [render-backend, d3d9, imgui, irenderbackend, vtable, seam, catch2, cppsharp]

# Dependency graph
requires:
  - phase: 15-resid
    provides: NoDeviceResetTests.cpp [resid04] no-Reset regression gate (the tree-wide Reset fence this seam preserves)
  - phase: 17-cppsharp-v145-hardening
    provides: CPPS-04 ABI gate + zero-UTINNI_API binding-surface discipline (the seam declares zero export to stay off the gate)
provides:
  - IRenderBackend pure-virtual ABC (10 members, D-01/D-02 + A2) — the single runtime-polymorphic render seam Phase 19 Dx11Backend slots behind
  - Non-virtual Dx9Backend::init(IDirect3DDevice9*) — the device-bearing GetCreationParameters + ImGui_ImplDX9_Init contract Plan 02 setup() calls
  - Dx9Backend wrapping directX:: verbatim with honest no-op resize hooks (D-03)
  - Two-TU seam split (Option A) — DX9-free render_backend.cpp (get/set/s_active) + DX9-bearing render_backend_dx9.cpp (Dx9Backend/s_dx9Backend/dx9Singleton)
  - D-07 Catch2 mock-dispatch test proving all 10 members route through the installed backend device-free
affects: [Plan 02 imgui_impl carve, Phase 19 Dx11Backend, 18-render-backend-seam consumers]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Two-TU seam split: zero-export interface accessors compiled DIRECTLY into the test exe (device-free), device-bound concretes isolated in a sibling DX9 TU"
    - "Runtime-polymorphic render seam: single IRenderBackend vtable, static-storage singleton, plain virtual dispatch (heap-free hot path)"
    - "Non-virtual device-init OFF the vtable (Phase 19 Dx11 init signature differs)"

key-files:
  created:
    - UtinniCore/swg/graphics/render_backend.h
    - UtinniCore/swg/graphics/render_backend.cpp
    - UtinniCore/swg/graphics/render_backend_dx9.cpp
    - UtinniCore.Tests/Graphics/RenderBackendSeamTests.cpp
  modified:
    - UtinniCore/UtinniCore.vcxproj
    - UtinniCore.Tests/UtinniCore.Tests.vcxproj
    - UtinniCoreDotNetGen/HeaderDiscovery.cs

key-decisions:
  - "Option A two-TU split chosen to satisfy CPPS-04 zero-export AND the D-07 device-free test simultaneously"
  - "render_backend.h excluded from CppSharp PARSE-stage discovery (clang-11 AccessViolation on <imgui.h>)"

patterns-established:
  - "Two-TU seam split: a zero-export accessor TU compiles into both the DLL and the test exe; the device-bound concrete TU compiles only into the DLL"
  - "Forward-compat vtable hooks (renderTargetWidth/Height, resize no-ops) defined now, consumed in a later phase"

requirements-completed: [RNDR-01]

# Metrics
duration: ~2h (across two executor sessions)
completed: 2026-06-15
---

# Phase 18 Plan 01: IRenderBackend Seam + Dx9Backend Summary

**A runtime-polymorphic IRenderBackend vtable (10 members) with the Dx9Backend wrapping directX:: verbatim, split across two translation units so the zero-export seam (CPPS-04) and the device-free D-07 mock-dispatch test coexist — built Release/x86 with the full native Catch2 suite green.**

## Performance

- **Duration:** ~2h total (Task 1 in a prior session; Task 2 + the Option-A split + build/test in this continuation session)
- **Completed:** 2026-06-15
- **Tasks:** 2 / 2
- **Files modified:** 7 (4 created, 3 modified)

## Accomplishments
- `IRenderBackend` pure-virtual ABC: 10 members (newFrame, renderDrawData, onPreResize, onPostResize, renderTargetWidth, renderTargetHeight, sceneDepthTexture, sceneColorTexture, sceneDepthStage, setSceneDepthStage) — the single seam Phase 19's Dx11Backend slots behind without re-touching imgui_impl.
- `Dx9Backend` wraps the live-verified `directX::` free functions verbatim with honest `{}` no-op resize hooks (D-03 — no Reset introduced); the non-virtual `init(IDirect3DDevice9*)` owns the device-bearing `GetCreationParameters` + the single `ImGui_ImplDX9_Init` in the tree (pDevice primary, `directX::getDevice()` assert/fallback).
- **Option-A two-TU split** resolves the Task-2 DECISION checkpoint: the zero-export `render_backend::get/set` + `s_active` live in the DX9-free `render_backend.cpp`; the device-bound `Dx9Backend` bodies + `s_dx9Backend` storage + `dx9Singleton()` live in the new `render_backend_dx9.cpp`.
- D-07 Catch2 mock-dispatch test (`[rndr01]`) drives all 10 vtable members through the installed mock and asserts `get()==nullptr` after restore — runs device-free in CI. Test exe compiles `render_backend.cpp` directly, so dispatch links with no LNK2019 cascade.
- Build Release/x86 exits 0; `[rndr01]` (16 assertions), `[resid04]` (8 assertions, untouched), and the full native suite (28 cases / 100 assertions) all green.

## Task Commits

1. **Task 1: Define IRenderBackend seam (10 members) + non-virtual Dx9Backend::init + Dx9Backend wrapper** — `fe32792` (feat) — *prior session*
2. **Task 2: D-07 Catch2 mock-dispatch test + Option-A two-TU seam split + build wiring** — `48b2684` (feat)

**Plan metadata:** (this commit) `docs(18-01): complete IRenderBackend seam plan`

## Files Created/Modified
- `UtinniCore/swg/graphics/render_backend.h` — IRenderBackend 10-member ABC + non-virtual Dx9Backend::init decl + get/set/dx9Singleton trio; DX9-binding-free (struct IDirect3DDevice9 forward-declared), zero UTINNI_API.
- `UtinniCore/swg/graphics/render_backend.cpp` — **DX9-FREE half** (Option A): active-backend pointer `s_active` + `get()`/`set()` only; zero Direct3D/imgui-backend/device-facade deps. Compiled directly into the test exe.
- `UtinniCore/swg/graphics/render_backend_dx9.cpp` — **NEW, DX9-bearing half** (Option A): Dx9Backend method bodies (forwarding to directX:: + ImGui_ImplDX9_*), `static Dx9Backend s_dx9Backend`, `dx9Singleton()`; the only file pulling `<d3d9.h>`/`<imgui_impl_dx9.h>`.
- `UtinniCore.Tests/Graphics/RenderBackendSeamTests.cpp` — D-07 MockBackend (10 counters) + the route-all-10-members + post-restore-null test, tag `[rndr01][graphics]`.
- `UtinniCore/UtinniCore.vcxproj` — added `render_backend.cpp` (Task 1) + `render_backend_dx9.cpp` (Task 2).
- `UtinniCore.Tests/UtinniCore.Tests.vcxproj` — added `RenderBackendSeamTests.cpp` + (Option A) `..\UtinniCore\swg\graphics\render_backend.cpp` compiled directly into the test exe.
- `UtinniCoreDotNetGen/HeaderDiscovery.cs` — excluded `render_backend.h` from CppSharp PARSE-stage discovery (see deviation 2).

## Decisions Made
- **Option A (two-TU split)** over compiling the whole DX9-bearing TU into the test: the seam's `get/set` are deliberately zero-export (CPPS-04 locked, Codex+Cursor affirmed) so they are NOT in `UtinniCore.lib` (the test references `UtinniCore.vcxproj` with `LinkLibraryDependencies=true`, which links only the import lib). Compiling the full `render_backend.cpp` into the test cascaded into directX::/DetourXS/DepthTexture/CuiManager (LNK2019). Splitting the zero-export accessors into a DX9-free TU lets the test compile that TU directly and link the mock-dispatch contract device-free — true to D-07 intent — while CPPS-04 zero-export is preserved (no `get/set/dx9Singleton` exported).
- **Acceptance-gate wording shift (per user decision, documented here):** the plan's DX9-specific gates — "single ImGui_ImplDX9_Init" (==1), "static Dx9Backend storage", "dx9Singleton" — now target `render_backend_dx9.cpp` instead of `render_backend.cpp`. The zero-UTINNI_API gate and the 10-pure-virtual / 10-member vtable gate still apply to `render_backend.h` unchanged. All gates re-verified green against the new layout (ImGui_ImplDX9_Init count == 1 in the DX9 TU; zero Reset; zero heap allocators; header zero UTINNI_API; 10 `= 0;` pure virtuals; test >=10 overrides + get()==nullptr assert).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added render_backend_dx9.cpp (two-TU split) — added file beyond plan files_modified**
- **Found during:** Task 2 (build wiring) — surfaced as the Task-2 DECISION checkpoint resolved by the user as Option A.
- **Issue:** The plan's single `render_backend.cpp` could not simultaneously satisfy two locked constraints: CPPS-04 zero-export (so `get/set` are absent from `UtinniCore.lib`) AND the D-07 device-free test (which must link those accessors). Compiling the whole DX9-bearing TU into the test exe cascaded into directX::/DetourXS/DepthTexture (LNK2019).
- **Fix:** Split the seam — DX9-free `render_backend.cpp` (get/set/s_active) + new DX9-bearing `render_backend_dx9.cpp` (Dx9Backend/s_dx9Backend/dx9Singleton). Test compiles the DX9-free TU directly.
- **Files modified:** UtinniCore/swg/graphics/render_backend.cpp (slimmed), UtinniCore/swg/graphics/render_backend_dx9.cpp (new), UtinniCore.vcxproj, UtinniCore.Tests.vcxproj
- **Verification:** Build Release/x86 exits 0; `[rndr01]` passes device-free (16 assertions); full suite green.
- **Committed in:** `48b2684` (Task 2 commit)

**2. [Rule 3 - Blocking] Excluded render_backend.h from CppSharp PARSE-stage discovery**
- **Found during:** Task 2 build (CppSharp binding-gen step in UtinniCore.vcxproj's pre-build).
- **Issue:** `render_backend.h` is the first UtinniCore header to pull `<imgui.h>` into the CppSharp parse graph. The pinned clang-11 parser (redirected to the MSVC 14.29 STL, NoStandardIncludes) AccessViolation-faults inside `ClangParser.ParseHeader` during `ParseCode()` — BEFORE the `Preprocess` `IgnoreHeadersWithName` AST pass can take effect. The header must be excluded at DISCOVERY (pre-parse), not via the AST ignore list.
- **Fix:** Added a phase-tagged early-`continue` in `HeaderDiscovery.Discover()` skipping `render_backend.h` (case-insensitive). The seam projects ZERO managed surface (zero UTINNI_API), so exclusion loses nothing.
- **Files modified:** UtinniCoreDotNetGen/HeaderDiscovery.cs
- **Verification:** Binding-gen completes; `Generated/UtinniCore.cs` reverted via `git checkout --` (never committed; status clean post-commit).
- **Committed in:** `48b2684` (Task 2 commit)

**3. [Rule 3 - Blocking] Reworded an XML comment to avoid `--` (MSB4025) and a source comment to clear the DX9-free grep gate**
- **Found during:** Task 2 build wiring.
- **Issue:** (a) The new `UtinniCore.Tests.vcxproj` comment contained a `--` sequence — illegal in XML comments (MSB4025, project failed to load). (b) The `render_backend.cpp` header comment literally contained the gated tokens `<d3d9.h>` / `directX::` in prose, tripping the DX9-free grep gate (grep-gate-hygiene lesson — gates are literal).
- **Fix:** Reworded the XML comment to drop `--`; reworded the source comment to "Direct3D / imgui-backend / device-facade" prose.
- **Files modified:** UtinniCore.Tests/UtinniCore.Tests.vcxproj, UtinniCore/swg/graphics/render_backend.cpp
- **Verification:** Build exits 0; DX9-free grep gate returns clean.
- **Committed in:** `48b2684` (Task 2 commit)

---

**Total deviations:** 3 auto-fixed (all Rule 3 - blocking). One added file (`render_backend_dx9.cpp`) beyond the plan's `files_modified`, justified by two simultaneously-locked constraints (CPPS-04 zero-export + D-07 device-free test).
**Impact on plan:** No scope creep — the split is purely structural (same symbols, two TUs); the seam's public shape, the 10-member vtable, the zero-export contract, and the live D3D9 behavior are all unchanged. The CppSharp exclusion is a build-graph accommodation losing no managed surface.

## Issues Encountered
- MSBuild switch mangling under Git Bash (`/m` → `M:/`): resolved by using the dash-prefix switch form (`-m -p:... -nologo`).
- All resolved within the task; no fix-attempt-limit reached.

## TDD Gate Compliance
Task 1 (`tdd="true"`) landed as a single `feat` commit in the prior session (`fe32792`) — the seam definition + the Dx9Backend wrapper together. The D-07 test is Task 2 (`type="auto"`, not TDD-gated). No RED/GREEN split warning required: Task 2's test (`48b2684`) is the CI-enforceable proof and passed on first run against the Option-A layout.

## User Setup Required
None — no external service configuration required. The final RNDR-01 acceptance (overlay renders + takes input live) is the Plan-02 D-08 maintainer live-smoke; this plan only adds the not-yet-consumed seam + a CI test.

## Next Phase Readiness
- **Plan 02 ready:** the seam shape is settled — `imgui_impl.cpp` carves onto `IRenderBackend` and calls `render_backend::dx9Singleton()->init(pDevice)` from `setup()`. The non-virtual init contract and the nullable `get()` (guard every call site) are defined.
- **Phase 19 ready:** Dx11Backend slots behind the same 10-member vtable; the resize no-ops and renderTargetWidth/Height forward-compat hooks have a home.
- No blockers. D-06 source gate (imgui_impl free of DX9 symbols) is NOT expected to pass yet — that is Plan 02's purge.

## Self-Check: PASSED
- `render_backend.h` — FOUND
- `render_backend.cpp` — FOUND (DX9-free)
- `render_backend_dx9.cpp` — FOUND (DX9-bearing)
- `RenderBackendSeamTests.cpp` — FOUND
- Commit `fe32792` — FOUND
- Commit `48b2684` — FOUND
- `Generated/UtinniCore.cs` — clean (not committed)
- `[rndr01]` + `[resid04]` + full native suite — green

---
*Phase: 18-render-backend-seam-dx9backend*
*Completed: 2026-06-15*
