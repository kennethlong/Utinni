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

**Open concerns from live UAT 2026-05-18 (not phase-gated; track separately):**

1. **WR-03 exit dialog STILL FIRES** — Plan 02.1-02 successfully eliminated the `delete depthTexture` UAF in `directX::cleanup()` (verified empty body at `directx9.cpp:410-427`), but on injected-session SWG exit the "Direct3D could not be correctly initialized" dialog still appears. Different teardown path than the one we fixed — likely `clr::stop()` (called immediately after cleanup in `detatch()`) or SWG's own D3D9 device-release noticing leftover state. Investigation deferred — not blocking; exit-only nuisance. Earlier "no exit dialog" UAT report on 2026-05-18 was incorrect (user mistook a delayed dialog for a startup dialog); status reset to "partial-fix; exit-side teardown still flagged." Re-investigate in Phase 03 or a Phase 02.2 mini-effort.

2. **D3D9 vtable pattern doesn't match modern d3d9.dll (REFRAMED 2026-05-19)** — initially diagnosed as broad RVA drift; corrected on 2026-05-19 via PE-timestamp + d3d9.dll pattern scan. **Actual root cause**: Utinni's `directx9.cpp::getVtbl()` scans `d3d9.dll` for the 14-byte pattern `C7 06 00 00 00 00 89 86 ?? ?? ?? ?? 89 86`. Modern Windows `d3d9.dll` builds (the user's `C:\Windows\SysWOW64\d3d9.dll`, 1,535,160 bytes) contain 0 hits for this pattern. Result: `getVtbl()` returns null → `directX::detour()` early-returns → zero D3D9 detours install → no `hkPresent` → ImGui can't draw → SWG runs unhooked. The SWG binary itself is fine (PE link timestamp 2005-04-14 confirms SWGEmu doesn't relink, just patches in-place; most RVAs probably correct). **Fix**: replace pattern-scan in `getVtbl()` with the conventional `CreateDevice(D3DDEVTYPE_NULLREF)` + read-first-4-bytes-of-device-ptr approach (works on every Windows version because we use the public D3D9 API). Estimated 2-4 hours. See `.planning/SESSION-HANDOFF-2026-05-19.md` for full diagnostic data + recommended fix code. Open as Phase 02.2 mini-effort.

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
