---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: completed
stopped_at: Phase 2 context gathered
last_updated: "2026-05-19T00:12:48.430Z"
last_activity: 2026-05-19 -- Phase 02.1 marked complete
progress:
  total_phases: 12
  completed_phases: 3
  total_plans: 9
  completed_plans: 9
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-16)

**Core value:** A modder downloads Utinni, installs once, and from a single application can see, edit, and live-preview every asset the SWG client loads — replacing the fragmented 15-year-old editor zoo with one stable, plugin-driven tool.
**Current focus:** Phase 02.1 — phase-02-gap-closure-critical-correctness-harness-quality

## Current Position

Phase: 02.1 — COMPLETE
Plan: 1 of 3
Status: Phase 02.1 complete
Last activity: 2026-05-19 -- Phase 02.1 marked complete
Next action: `/gsd:discuss-phase 02.1` or `/gsd:plan-phase 02.1`

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**

- Total plans completed: 2
- Average duration: —
- Total execution time: —

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| — | — | — | — |
| 01 | 2 | - | - |

**Recent Trend:**

- Last 5 plans: (none yet)
- Trend: (no data)

*Updated after each plan completion*

## Accumulated Context

### Roadmap Evolution

- Phase 02.1 inserted after Phase 2: Phase 02 gap closure — critical correctness + harness quality from 02-REVIEW.md (URGENT)

### Decisions

Full decision log lives in PROJECT.md Key Decisions table. V1 starts with four locked anti-goal decisions (DEC-A1..A4 — not a server-side manager, not a launcher, not a DCC, not a cheat enabler) and three non-locked candidate decisions (DEC-C1 product target, DEC-C2 anti-goals as scope filter, DEC-C3 tiered testing strategy).

### Pending Todos

None yet.

### Blockers/Concerns

**Open concerns from live UAT 2026-05-18/19 (not phase-gated; track separately):**

1. **WR-03 exit dialog STILL FIRES** — Plan 02.1-02 successfully eliminated the `delete depthTexture` UAF in `directX::cleanup()` (verified empty body at `directx9.cpp:410-427`), but on injected-session SWG exit the "Direct3D could not be correctly initialized" dialog still appears. Different teardown path than the one we fixed — likely `clr::stop()` (called immediately after cleanup in `detatch()`) or SWG's own D3D9 device-release noticing leftover state. Investigation deferred — not blocking; exit-only nuisance. Earlier "no exit dialog" UAT report on 2026-05-18 was incorrect (user mistook a delayed dialog for a startup dialog); status reset to "partial-fix; exit-side teardown still flagged." Re-investigate in Phase 03 or a Phase 02.2 mini-effort. **2026-05-19 update:** disappears in passthrough-everything builds; reappears with any detour active — confirms exit dialog is downstream of a Utinni hook interaction with D3D9 lifecycle, not the cleanup() UAF we already fixed.

2. **~~D3D9 vtable pattern doesn't match modern d3d9.dll~~ RESOLVED 2026-05-19 (commit 2c57d38)** — Replaced the broken `d3d9.dll` byte-pattern scan in `directx9.cpp::getVtbl()` with the conventional dummy-device approach (`Direct3DCreate9` + hidden 1x1 window + `CreateDevice(HAL)` + read vtable pointer + snapshot 119 entries + release). Proved via probe of buildable SWG Source client that modern `d3d9.dll` (Win11 24H2 6.2.26100.8328) allocates IDirect3DDevice9 vtables per-instance on the heap — no static `.rdata` table exists for pattern scanning. Probe data archived in `.planning/SESSION-HANDOFF-2026-05-19.md`. After this commit, injection log shows no DirectX9 critical errors; D3D9 detours install cleanly.

3. **Editor-mode HWND-override hooks were wedging SWG init — RESOLVED 2026-05-19 (commits 18c5e22 + 74f64fc)** — Bisection (13 rounds) traced post-d3d9-fix audio-init stall to two editor-mode code paths that override SWG's HWND with the editor's:
   - `hkSetupStartInstall` set `pStartupData->createOwnWindow=false` + `windowHandle=Client::getHwnd()` → SWG silently hung after audio init.
   - `hkSetupInstall` (DirectInput) replaced SWG's HWND with editor's top-level HWND → `SetCooperativeLevel` returned `DIERR_INVALIDPARAM` because the editor HWND is on the CLR thread, not SWG's main thread.
   Both hooks now pass through. New integration model: SWG creates its own window normally; managed side will reparent that HWND into the editor's PanelGame via `SetParent` + `WS_CHILD` style change. **Managed-side reparenting is not yet implemented** — for now SWG creates a separate window (still hidden by another open issue, see #4).

4. **NEW: Managed-side CLR exception 0xE0434352 during character template load** — After the three native fixes above, SWG runs cleanly through audio → dPVS → preloading, then hits an unhandled .NET exception that escapes into native and trips SWG's ExceptionHandler. Crash dump at `D:\SWGEmu-Client\SWGEmu\logs\SWGEmu.exe-stage.119798-20260519170719.{txt,mdmp}` (12:07 run). `0xE0434352` ("CCR" backwards) is the standard CLR escaped-exception code. Context fields in the dump: `ObjectTemplate_Constructor: object/creature/player/shared_zabrak_male.iff` / `AppearanceTemplate: appearance/zab_m.sat` / `SkeletalMeshGeneratorTemplate: appearance/mesh/zab_m_head_l3.mgn` — exception fires while SWG is loading the default Zabrak avatar template. Almost certainly a TJT plugin or UtinniCoreDotNet callback wired to skeletal/template-load events throwing because some prerequisite isn't ready. Pre-existing bug; previously masked because the d3d9 failure stopped SWG before it reached template load. Next session: open VS 2026 → Debug → Exception Settings → check "Common Language Runtime Exceptions" → run Launcher.exe under VS → exception triggers, VS pauses on the throwing managed line. Or analyze the `.mdmp` with SOS/Psscor. Open as Phase 02.2 or new investigation phase.

5. **NEW: SWG window invisible during runtime; visible briefly on editor-host close** — With all detours active (post-fixes), SWG progresses to preloading but its top-level window never becomes visible during normal runtime; it pops up briefly when the editor host is closed. May share root cause with #4 (CLR exception may be killing the render loop before first frame) or may be an independent z-order / `ShowWindow` interception issue. Defer triage until #4 is resolved — fixing the CLR exception may make this symptom disappear.

Eleven open questions (CON-O-01..CON-O-11) are tracked as phase-gated unresolved constraints — see ROADMAP.md "Open-Question → Phase Mapping" section.

## Deferred Items

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| *(none)* | | | |

## Resolved Deferred Items

| Category | Item | Resolved | Notes |
|----------|------|----------|-------|
| Tier-4 manual UAT | Plan 02-03 Task 3 — C-01 live SWG injection | 2026-05-18 | PASSED. Editor host + TJT plugin came up alongside live SWGEmu client; no loader-lock hang. See `02-HUMAN-UAT.md` §1. En-route fixes (commits `cb547bb`, `92758ff`, `9a108d1`, `3254059`, `389fc83`) landed during the UAT run. |
| Tier-4 manual UAT | Plan 02-04 Task 2 — C-09 live SWG minimize/restore | 2026-05-18 | PASSED. Editor stays responsive across rapid minimize/restore cycles; no UI-thread CPU spike. See `02-HUMAN-UAT.md` §2. |

## Session Continuity

Last session: 2026-05-16T23:04:55.398Z
Stopped at: Phase 2 context gathered
Resume file: .planning/phases/02-critical-bug-burn-down-c-01-c-15/02-CONTEXT.md

## Ingest Provenance

Bootstrapped 2026-05-16 via `/gsd:ingest-docs` from `docs/ai/vision.md`, `docs/ai/assessment.md`, and `docs/ai/test-harness-plan.md`. Zero blockers, zero warnings, four INFO items auto-resolved (all three sources are DOC-precedence; reciprocal vision↔assessment cross-reference is benign narrative linkage). Codebase intel at `.planning/codebase/` (from prior `/gsd:map-codebase`) treated as read-only reference. Synthesis artefacts at `.planning/intel/` (SYNTHESIS.md, decisions.md, requirements.md, constraints.md, context.md) and conflict report at `.planning/INGEST-CONFLICTS.md`.
