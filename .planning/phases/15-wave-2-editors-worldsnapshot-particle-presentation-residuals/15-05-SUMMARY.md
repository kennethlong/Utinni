---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 05
subsystem: infra
tags: [directinput, d3d9, fullscreen, directx9, panelgame, catch2, regression-gate, resid04]

# Dependency graph
requires:
  - phase: 02-foundation (Phase B owned-popup window ownership)
    provides: PanelGame owned-popup reparent (WS_POPUP + GWLP_HWNDPARENT) + SetWindowPos reposition path
  - phase: 02 (DirectInput SetCooperativeLevel vtable shim, commit 18e79c3)
    provides: hkSetCooperativeLevel shim at vtbl[13] + DISCL flag/caller-PC logging extended here
provides:
  - Native DirectInput-level suppression of SWG's exclusive-fullscreen mode switch (DISCL_EXCLUSIVE -> DISCL_NONEXCLUSIVE) behind a default-ON runtime toggle (D-12)
  - Exported DirectInput::setSuppressExclusiveFullscreen/getSuppressExclusiveFullscreen for live A/B without a rebuild
  - PanelGame.cs resize-path documentation tying the (now-suppressed) mode change to window-side SetWindowPos-only + RT-space mapping + no device Reset
  - Catch2 [resid04] no-device-Reset regression gate (D-13) enforcing the third-party-device no-Reset constraint
affects: [15-08, RESID-04, window-management, d3d9-presentation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Toggle-guarded native suppression: a std::atomic<bool> runtime flag (default ON) lets the maintainer A/B a behavioral fix live without a rebuild, keeping a deferred fallback reachable"
    - "Comment-stripping source grep-gate: read source, strip // and /* */ comments before counting an invocation-shape needle, with a self-check section proving the stripper is non-trivial (no bare == 0)"

key-files:
  created:
    - "UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp"
  modified:
    - "UtinniCore/swg/misc/direct_input.cpp"
    - "UtinniCore/swg/misc/direct_input.h"
    - "UtinniCoreDotNet/UI/Controls/PanelGame.cs"
    - "UtinniCore.Tests/UtinniCore.Tests.vcxproj"

key-decisions:
  - "D-12: suppress the exclusive-fullscreen switch at the DirectInput cooperative-level layer (rewrite DISCL_EXCLUSIVE -> DISCL_NONEXCLUSIVE, preserving FOREGROUND/BACKGROUND/NOWINKEY) rather than at the D3D9 device layer"
  - "D-13: the no-Reset constraint is enforced by a comment-stripped source grep-gate counting ->Reset(/.Reset( invocations; hkReset's free-function reset(pDevice,...) pass-through of SWG's OWN Reset is naturally excluded by the invocation-shape pattern"
  - "Suppression is a default-ON std::atomic toggle exposed via the exported DirectInput API so 15-08 can A/B it live and the deferred detached-fullscreen fallback stays reachable (Open Q3)"

patterns-established:
  - "Toggle-guarded behavioral native fixes (default-ON atomic + exported get/set) for live maintainer A/B"
  - "Comment-stripped source grep-gate with an anti-trivial self-check section (grep-gate hygiene)"

requirements-completed: [RESID-04]

# Metrics
duration: ~25min
completed: 2026-06-07
---

# Phase 15 Plan 05: RESID-04 Window-Resize / Fullscreen Edge Cases (automatable half) Summary

**SWG's exclusive-fullscreen mode switch is suppressed at the DirectInput cooperative-level layer (DISCL_EXCLUSIVE -> DISCL_NONEXCLUSIVE) behind a default-ON runtime toggle to keep the embed windowed, with a Catch2 [resid04] grep-gate enforcing the D-13 no-device-Reset constraint.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-06-07T15:16Z
- **Completed:** 2026-06-07T15:41Z
- **Tasks:** 2
- **Files modified:** 4 (1 created, 3 modified) + 1 vcxproj wire

## Accomplishments
- Extended (did NOT re-hook) the existing `hkSetCooperativeLevel` shim to redirect `DISCL_EXCLUSIVE` to `DISCL_NONEXCLUSIVE` while preserving `DISCL_FOREGROUND`/`DISCL_BACKGROUND`/`DISCL_NOWINKEY`, keeping SWG windowed-embedded (D-12). The existing DISCL-flag + caller-PC logging is retained verbatim as the A4 live-confirmation instrument, and the redirect itself logs the old->new flag rewrite.
- Gated the suppression behind a default-ON `std::atomic<bool>` runtime toggle, exposed via new exported `DirectInput::setSuppressExclusiveFullscreen` / `getSuppressExclusiveFullscreen` so the maintainer can A/B it live in 15-08 without a rebuild and the deferred detached-fullscreen fallback (Open Q3) stays reachable.
- Documented the `PanelGame.cs` resize path: with the mode switch suppressed, any resize response stays purely window-side `SetWindowPos` (the existing L252 path); NO Utinni-initiated device `Reset` (D-13), owned-popup model unchanged (no `WS_CHILD`), and the imgui RT-space mouse + DisplaySize mapping holds across resize via the windowed COPY Present self-stretch.
- Added a Catch2 `[resid04]` no-device-Reset regression gate (`NoDeviceResetTests.cpp`) — a comment-stripped source grep over `directx9.cpp` + `direct_input.cpp` + `PanelGame.cs` asserting ZERO `->Reset(`/`.Reset(` device invocations, with a self-check section proving the stripper detects an in-comment `.Reset(` and zeroes it (no bare `== 0`).

## Task Commits

1. **Task 1: Suppress/redirect the exclusive-fullscreen mode switch (toggle-guarded)** — `bf5843d` (fix)
2. **Task 2: No-device-Reset regression gate (D-13)** — `6ae1dd7` (test)

## Files Created/Modified
- `UtinniCore/swg/misc/direct_input.cpp` - `hkSetCooperativeLevel` redirects DISCL_EXCLUSIVE -> DISCL_NONEXCLUSIVE behind a default-ON `std::atomic` toggle; new exported toggle accessors; `<atomic>` include
- `UtinniCore/swg/misc/direct_input.h` - declared `setSuppressExclusiveFullscreen` / `getSuppressExclusiveFullscreen`
- `UtinniCoreDotNet/UI/Controls/PanelGame.cs` - RESID-04 comment block tying the resize path to window-side SetWindowPos-only + RT-space mapping + no device Reset (D-13)
- `UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp` - new Catch2 `[resid04]` no-Reset source grep-gate (comment-stripping + anti-trivial self-check)
- `UtinniCore.Tests/UtinniCore.Tests.vcxproj` - wired the new test into the ItemGroup

## Decisions Made
- Suppress at the DirectInput cooperative-level layer (not the D3D9 device layer) because RESEARCH A4 + the chat-open-d3d9-fullscreen debug session point to `SetCooperativeLevel(DISCL_EXCLUSIVE|DISCL_FOREGROUND)` as the prime trigger, and the shim is already installed there — extend, don't re-hook.
- Implement the no-Reset gate as a comment-stripped source grep counting the `->Reset(`/`.Reset(` invocation shape so the legitimate `hkReset` free-function `reset(pDevice,...)` pass-through and the no-Reset RATIONALE comments are both excluded without special-casing.

## Deviations from Plan

None - plan executed exactly as written. Both tasks built green and the gate passes (8 assertions, 1 test case). `Generated/UtinniCore.cs` regenerated by both builds and was `git checkout --`'d each time (never committed), per `project_utinnicore_cs_regen_churn`.

## Issues Encountered
- The Bash-tool shell mangled MSBuild `/switch` arguments and `%ProgramFiles%` expansion; resolved by invoking `MSBuild.exe` with the literal install path and `-switch` (dash) form. No code impact.

## Live-dependent follow-up (15-08 Tier-4 maintainer smoke)
This plan ships the automatable + CI-gateable half of RESID-04. The live edge-case-matrix enumeration + suppress confirmation is folded into 15-08. On the first live injected run, 15-08 must confirm:
1. Read the `DI::SetCooperativeLevel` log on login->world and chat-open to confirm A4 — that SWG requests `DISCL_EXCLUSIVE` (the trigger), and that the `D-12 suppress -> redirected EXCLUSIVE to NONEXCLUSIVE` line fires.
2. With suppression ON (default), verify SWG stays windowed-embedded (no detach/overlay, embed survives login->world + chat-open + alt-tab + minimize/restore) and that chat/input still route (FOREGROUND preserved).
3. Verify no device `Reset` occurs and no crash (the gate enforces source-level; live confirms behavioral).
4. Optionally flip the toggle OFF live to observe the un-suppressed mode switch (A/B), confirming the toggle is the lever.

The known right-edge cursor-clip dead-zone (`project_swg_cursor_clip_deadzone`) is a SEPARATE deferred item and was intentionally NOT touched here.

## Next Phase Readiness
- RESID-04 automatable code + regression gate shipped and build-green (UtinniCore + UtinniCore.Tests Release|x86).
- 15-08 owns the live confirmation; the DISCL log + runtime toggle are the instruments it needs.

---
*Phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals*
*Completed: 2026-06-07*
