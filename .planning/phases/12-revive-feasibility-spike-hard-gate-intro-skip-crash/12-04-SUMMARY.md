---
phase: 12-revive-feasibility-spike-hard-gate-intro-skip-crash
plan: 04
subsystem: RESID-02 (intro-skip scene-transition crash — diagnose/dispose)
tags: [resid-02, veh-logger, scene-transition, no-repro, resolved-by-prior-fix, windows-mcp, resid-04-deferred]
requires: []
provides:
  - ".planning/phases/.../12-RESID-02-RCA.md (A5 no-repro disposition + resolved-by-prior-fix attribution)"
  - ".mcp.json (windows-mcp live-session driver infra, for future Phase-15 live work)"
  - "swg-window-resize-fullscreen-edge-cases.md live-observation block (RESID-04 data points)"
tech-stack:
  added: [windows-mcp (uvx, .mcp.json — UIA/screenshot Windows-desktop driver)]
  patterns: [VEH-logger-as-harness, A5-no-repro disposition, resolved-by-prior-fix attribution]
key-files:
  created:
    - .planning/phases/12-revive-feasibility-spike-hard-gate-intro-skip-crash/12-RESID-02-RCA.md
    - .mcp.json
  modified:
    - .planning/todos/pending/swg-window-resize-fullscreen-edge-cases.md
key-decisions:
  - "RESID-02 disposed via A5 (no-repro): the intro-skip scene-transition crash no longer reproduces on the current build; attributed to the Phase-3 R-A/R-H heap-free dispatchSnapshot migration (7201700+5e81410), same fault class as the prior 0x0051fb0a crash."
  - "Disposition rests on absence-of-fault, so the VEH logger STAYS deployed (utinni.cpp:291) as standing observation per D-11 — not removed."
  - "The window-overlay / stuck-cursor / minimize-both-disappear symptoms surfaced during the re-run are RESID-04 (windowed<->fullscreen edge cases), already roadmapped to Phase 15 — captured, NOT fixed here (no scope-creep)."
  - "windows-mcp wired into .mcp.json (mirrors swg-client-v2) as the live-session driver; not exercised this plan (RESID-02 didn't repro), staged for Phase-15 RESID-04 live enumeration."
requirements-completed: [RESID-02]
duration: ~1h (live session)
completed: 2026-06-03
---

# Phase 12 Plan 04: RESID-02 intro-skip crash — Summary

Closes the **RESID-02** track (the only non-AUTH-01 work in Phase 12). The dump-less intro-skip scene-transition crash **no longer reproduces** on the current build — the plan's anticipated **A5 (no-repro)** branch. Root cause is attributed to a prior fix, the deliverable is the documented RCA, and the VEH logger stays deployed. **Tasks:** 4 (Task 1 = live repro checkpoint; Task 4 = maintainer RCA acceptance). **Files:** 2 created + 1 modified.

## What happened

- **Task 1 (live repro):** Maintainer launched `bin\Release\Launcher.exe` (one-shot: spawns `SWGEmu.exe` suspended → EB FE entry patch → inject `UtinniCore.dll` → `utinni_init` → TJT) and exercised the RESID-02 trigger two ways: (a) TJT Scene subpanel → select `terrain/naboo.trn` → **Load** (clean load, naked-but-in-world baseline), and (b) the original **login → load-into-world** intro→login transition. **Neither crashed.** No `VEH FATAL` line was emitted to `bin\Release\utinni.log` (watched live, rotation-aware).
- **Task 2/3 (RCA + disposition):** Authored `12-RESID-02-RCA.md`. With no fault to resolve, the disposition is **A5 no-repro → resolved by prior Utinni-side fix** (retroactive D-11 branch-1): the Phase-3 **R-A/R-H heap-free `dispatchSnapshot` migration** (`7201700` + `5e81410`) eliminated the per-frame-heap-alloc fault class that produced the scene-change crash. Corroborated independently by the 2026-05-23 R-A migration smoke note (`chat-open-d3d9-fullscreen.md`: *"no scene-change AV at `0x0051fb0a`"*) and again live today. VEH logger remains deployed (`utinni.cpp:291`, D-11).
- **Task 4 (acceptance):** Maintainer accepted the documented RCA (RESID-02 no longer crashes → **success criterion #5 met**).

## Live-session driver infra (windows-mcp)

Per the heads-up flagged in `reference_winapp_mcp_testing`, wired the live-session driver into `.mcp.json` (`uvx windows-mcp serve`, telemetry off) — mirroring the working `swg-client-v2` config. uvx confirmed + dep set cached. **Not exercised this plan** (RESID-02 didn't repro, and the maintainer drove the UI manually), but staged for Phase-15 RESID-04 live enumeration. Note: the entire TJT scene-change trigger is WinForms (UIA-addressable), so the driver can act by element — only the DirectX surface needs coordinate/keystroke input.

## Separate finding — RESID-04, deferred (NOT this plan)

During the re-run the maintainer observed the embedded SWG window, on **login → fullscreen**, detach and **overlay the WinForms editor**; clicking it does not establish the SWG cursor and the **character cannot move** (non-recoverable in-session); and **minimizing takes both windows down together**. These are all the already-roadmapped **RESID-04** windowed↔fullscreen / D3D9-presentation cluster (Phase 15), not the RESID-02 fault class. Captured as a dated live-observation block in `swg-window-resize-fullscreen-edge-cases.md`, including a **new data point**: the fullscreen switch fires on the **login→load-into-world** path, not only the previously-known chat-open Enter path — widening the prime-suspect trigger surface for the Phase-15 fix. **Maintainer triage: low priority, likely not a hard find.**

## Deviations from Plan

**[Plan-anticipated] A5 no-repro instead of a captured-and-fixed `VEH FATAL`.** The plan's Task 1 `<resume-signal>` explicitly provided for this ("If no crash reproduces after several attempts, report that (A5) so disposition can adjust"). No live fault → no module/rva to resolve → disposition is documented no-repro with prior-fix attribution. The RCA still satisfies the Task 2 gate (contains the `VEH FATAL` line format, the module/rva framing, and an explicit D-11 disposition).

**Total deviations:** 0 unplanned. A5 is the plan's designed no-repro branch.

## Self-Check: PASSED
- `12-RESID-02-RCA.md` passes the Task 2 automated gate (`VEH FATAL` + `rva`/`module` + D-11 disposition all present).
- VEH logger still deployed: `AddVectoredExceptionHandler(1, utinniBreakpointVEH)` at `utinni.cpp:291` (D-11 honored).
- RESID-04 finding routed to its existing Phase-15 todo, not conflated with RESID-02.

## Phase 12 — COMPLETE
All four plans done: **12-01/02/03** (AUTH-01 build hard gate — 3 SOE CLIs green at v145/Win32, CI-enforced, byte-exact = documented gate-findings) + **12-04** (RESID-02 intro-skip crash resolved-by-prior-fix, documented). AUTH-01 unblocks Phase 13; RESID-02 closed; RESID-04 deferred to Phase 15 as roadmapped.
