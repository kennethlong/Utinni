---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 13
subsystem: ui
tags: [resid-04, d3d9-presentation, owned-popup, swg-window-embed, watchdog, winforms, panelgame]

# Dependency graph
requires:
  - phase: 15 (15-05)
    provides: "the D-12 DirectInput exclusive-fullscreen suppress + the [resid04] no-Reset Catch2 grep gate (NoDeviceResetTests.cpp) this plan stays green against"
  - phase: "Issue #10 Phase B (owned-popup reparenting)"
    provides: "ReparentSwgWindow / RepositionSwgWindow frame-strip + owner-set + HWND_TOP/SWP_NOACTIVATE reposition machinery this plan re-asserts"
provides:
  - "A window-style watchdog in PanelGame.cs that detects SWG's WINDOW-LEVEL fullscreen restyle (WS_POPUP cleared / frame-mask re-added / owner changed) and re-asserts the owned-popup embed window-side"
  - "Shared AssertEmbedStyles helper — single source of the frame-strip + owner-set, used by both the initial reparent and the re-assert"
  - "ReassertEmbed() — re-strip + re-own + reposition + host re-activate, purely SetWindowLong/SetWindowPos + Activate, NO device Reset"
affects: [15-17 reassembled-injection-build, 15-18 live-re-smoke (Checklist C3 windowed->fullscreen re-verify)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Low-frequency window-style watchdog Timer (distinct from the one-shot reparentPollTimer) that re-asserts an owned-popup embed when the third-party window restyles itself"
    - "Shared style-assertion helper so the initial reparent and the re-assert cannot drift their frame-mask / WS_POPUP / owner logic"

key-files:
  created: []
  modified:
    - "UtinniCoreDotNet/UI/Controls/PanelGame.cs — embedWatchdogTimer + EmbedWatchdogTimer_Tick + ReassertEmbed + AssertEmbedStyles refactor"

key-decisions:
  - "Window-level fullscreen detection is style-based (WS_POPUP cleared OR any frame-mask bit re-added OR GWLP_HWNDPARENT owner != FormMain) — matches the 2026-06-13 smoke finding that SWG restyles its own HWND with ZERO new SetCooperativeLevel/EXCLUSIVE request, so D-12 never fires for C3"
  - "Distinct 250ms embedWatchdogTimer, NOT a reuse of the self-stopping reparentPollTimer (reuse would defeat its one-shot contract)"
  - "Re-assert is window-side ONLY (SetWindowLong/SetWindowPos + ownerFormCached.Activate()); NEVER IDirect3DDevice9::Reset (D-13) — the [resid04] grep gate stays at count 0"
  - "Owned-popup model preserved: SWG stays WS_POPUP (never WS_CHILD, which breaks DirectInput's top-level-HWND requirement); Z-order via HWND_TOP + SWP_NOACTIVATE through RepositionSwgWindow"

patterns-established:
  - "Pattern 1: re-assertable embed — extract the embed-style assertion into one helper so a watchdog can re-run it on demand without recreating timers"

requirements-completed: [RESID-04]

# Metrics
duration: ~20min
completed: 2026-06-13
---

# Phase 15 Plan 13: RESID-04 window-level-fullscreen embed re-assert watchdog Summary

**A 250ms window-style watchdog in PanelGame.cs detects SWG's window-level fullscreen restyle (WS_POPUP cleared / frame re-added / owner changed) and re-asserts the owned-popup embed — re-strip + re-own + reposition + host re-activate — purely via SetWindowLong/SetWindowPos + Activate, never a device Reset.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-06-13
- **Completed:** 2026-06-13
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Closed the BLOCKING RESID-04 live residual (15-SMOKE Checklist C3): SWG's windowed→fullscreen here is a WINDOW-LEVEL fullscreen (the 2026-06-13 smoke captured ZERO new SetCooperativeLevel/EXCLUSIVE request), so the D-12 DirectInput suppress correctly never engages and nothing re-asserted Utinni's owned-popup reparent — the embed detached (black right gutter, chrome behind), focus dropped to 0x0, input died. This watchdog counters that window-side.
- Added `embedWatchdogTimer` (250ms, distinct from the self-stopping `reparentPollTimer`), started right after the first successful reparent in `ReparentSwgWindow` and stopped/disposed in `PanelGame_Disposed`.
- Watchdog tick reads SWG's live `GWL_STYLE` + `GWLP_HWNDPARENT` and detects the detached/restyled condition: `WS_POPUP` cleared OR any frame-mask bit re-added OR owner ≠ `ownerFormCached.Handle`.
- `ReassertEmbed()` re-runs the frame-strip (`| WS_POPUP`) + owner-set, calls `RepositionSwgWindow()` (HWND_TOP + SWP_FRAMECHANGED + SWP_NOACTIVATE), and re-activates the host group via `ownerFormCached.Activate()` to pull focus back off 0x0 — restoring input/focus window-side.
- Extracted the shared frame-strip + owner-set into a single `AssertEmbedStyles(...)` helper consumed by BOTH `ReparentSwgWindow` and `ReassertEmbed`, so the frame-mask / WS_POPUP / owner logic cannot drift between the two paths.
- Rate-limited (cap 8, mirroring `s_repositionLogCount`) `Log.Info` diagnostic on each re-assert recording the detected vs. re-asserted style, so the 15-18 live re-smoke can confirm the watchdog engaged across the fullscreen transition.

## Task Commits

Each task was committed atomically:

1. **Task 1: Watchdog that re-asserts the owned-popup embed on SWG's window-level fullscreen restyle** - `fc6e3fe` (feat)

**Plan metadata:** this commit (docs: complete plan — SUMMARY + STATE + ROADMAP)

## Files Created/Modified
- `UtinniCoreDotNet/UI/Controls/PanelGame.cs` - Added `embedWatchdogTimer` + `EmbedWatchdogTimer_Tick` (style/owner detach detection) + `ReassertEmbed` (window-side re-assert + host re-activate) + `AssertEmbedStyles` (shared frame-strip/owner-set helper) + `EmbedFrameMask` static; refactored `ReparentSwgWindow` to use the shared helper and start the watchdog; stop+dispose the watchdog in `PanelGame_Disposed`.

## Decisions Made
- **Detection is style-based, not D3D9-event-based.** The 2026-06-13 smoke proved the C3 transition is a window-level fullscreen with no exclusive-mode request, so the only reliable signal is SWG mutating its own GWL_STYLE/GWLP_HWNDPARENT. The watchdog polls those at 250ms.
- **Distinct watchdog timer.** `reparentPollTimer` self-stops by contract after the first reparent; reusing it would either break that contract or never re-fire. A separate `embedWatchdogTimer` is the clean fit.
- **Window-side only, no Reset (D-13 LOCKED).** Re-assert is `SetWindowLong`/`SetWindowPos` + managed `Activate()`. The native `[resid04]` NoDeviceResetTests grep gate (comment-stripped count of `->Reset(`/`.Reset(` over directx9.cpp + direct_input.cpp + PanelGame.cs) stays at 0 — verified green (8 assertions / 1 case).
- **Owned-popup preserved.** SWG stays WS_POPUP (never WS_CHILD — DirectInput needs a top-level HWND); Z-order stays HWND_TOP + SWP_NOACTIVATE via the existing `RepositionSwgWindow`.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- **Build invocation:** the plan's verify command hardcodes `%ProgramFiles%\Microsoft Visual Studio\18\Community\...` (C: drive, absent on this machine). Per the build-environment override, substituted the real MSBuild at `/d/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe`. The Git Bash `/m` → `M:/` path-conversion mangling was fixed by quoting switches and setting `MSYS_NO_PATHCONV=1`.
- **Native test exe location:** `UtinniCore.Tests.exe` builds to `D:/Code/Utinni/bin/Release/` (CMake-style shared output), not `UtinniCore.Tests/bin/Release/` as the plan's verify path implied. The `[resid04]` gate is a live source-grep (reads PanelGame.cs from disk), so the binary build date is immaterial to its correctness; ran the existing exe and it passed.

## Verification
- `Utinni.sln` Release|x86 MSBuild exit 0 (warnings only — pre-existing xUnit analyzer noise; zero errors).
- `UtinniCore.Tests.exe [resid04]` → **All tests passed (8 assertions in 1 test case)** — no-Reset gate green, Reset-invocation count stays 0 across PanelGame.cs.
- `dotnet test UtinniCoreDotNet.Tests --no-build -c Release` → **Passed! Failed: 0, Passed: 706** — no new managed regressions (the known pre-existing `FindPatternHarnessTests.GetVtbl` D3D9-harness failure is in a separate harness and did not surface in this suite).
- `grep WS_CHILD` / `grep .Reset(` in PanelGame.cs: the only occurrences are in COMMENTS (the "NEVER WS_CHILD" / "no `.Reset(` token appears" rationale lines, consistent with the file's pre-existing Issue-#10 comment style at L78/L80). The authoritative comment-stripped `[resid04]` gate confirms zero code-level Reset invocations.
- `Generated/UtinniCore.cs` reverted (git status clean for that file — no regen churn this build).

## Next Phase Readiness
- Window-side fix is complete and gate-green. The C3 windowed→fullscreen live re-verify is gated to the **15-18 re-smoke against the 15-17 reassembled injection build** (a cached failed bind / live restyle needs an actual injected SWG session to confirm the watchdog re-attaches the embed and recovers focus/input).
- No blockers introduced. The diagnostic `Log.Info` lines are intentionally retained (rate-limited) so the maintainer can confirm watchdog engagement in utinni.log during the live run.

## Self-Check: PASSED

- FOUND: UtinniCoreDotNet/UI/Controls/PanelGame.cs (modified, committed)
- FOUND: commit fc6e3fe (Task 1)
- FOUND: .planning/phases/15-wave-2-editors-worldsnapshot-particle-presentation-residuals/15-13-SUMMARY.md

---
*Phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals*
*Completed: 2026-06-13*
