# Phase 12: Revive-feasibility spike (HARD GATE) + intro-skip crash - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-02
**Phase:** 12-revive-feasibility-spike-hard-gate-intro-skip-crash
**Areas discussed:** Tools tree & build seam, Per-tool 'pass' bar, v143-fallback & fail policy, RESID-02 crash scope

---

## Tools tree & build seam

| Option | Description | Selected |
|--------|-------------|----------|
| Local-only, standalone sln | Standalone `tools/Utinni.Tools.sln`, verified locally, CI deferred to Phase 13 | |
| Wire into self-hosted CI now | Add `tools/Utinni.Tools.sln` build step to the self-hosted v145 runner this phase | ✓ |
| Fold into main Utinni.sln | Single solution carrying game-tools + managed/native projects | |

**User's choice:** Wire into self-hosted CI now (separate sln + CI lane this phase).
**Notes:** Continuously enforced hard gate from day one; no silent rot before Phase 13. Self-hosted because v145/VS2026 is Insiders-only.

---

## Per-tool 'pass' bar

| Option | Description | Selected |
|--------|-------------|----------|
| Non-empty artifact from synth input | Tier-2-style synth input; non-empty output suffices; correctness deferred | |
| Round-trip through Utinni readers | Output must parse cleanly through existing TRE/IFF readers | |
| Real asset, byte-compare | Real swg-client-v2 asset, diff against known-good reference | ✓ |

**Follow-up — Byte-compare reference + fallback:**

| Option | Description | Selected |
|--------|-------------|----------|
| Byte-exact where reachable; structural fallback | Byte-exact where deterministic; documented structural equivalence where not | |
| Byte-exact mandatory, no fallback | Any tool that can't reproduce byte-exact output is a gate failure to surface | ✓ |
| I'll supply reference pairs | User provides known source→output pairs | |

**User's choice:** Real-asset byte-exact, **mandatory, no structural fallback**.
**Notes:** Cross-toolset non-determinism (ordering/padding/build-stamps) surfaces as a finding to resolve, not a pass. Research must validate per-tool whether byte-exact is achievable and whether matching source→output pairs exist.

---

## v143-fallback & fail policy

| Option | Description | Selected |
|--------|-------------|----------|
| Auto-fallback; refuses-both = captured failure | Timeboxed v145 fight, auto-drop to v143, refuses-both still passes as documented failure | |
| Auto-fallback; refuses-both HARD-BLOCKS | Auto v143 fallback, but builds-at-neither blocks the milestone | |
| Stop-and-ask before each fallback | Surface every v145 failure to maintainer before dropping toolset | ✓ |

**User's choice:** Stop-and-ask before each v143 fallback.
**Notes:** Toolset decision made per tool with maintainer in the loop; tightens the ROADMAP's "documented fallback" into a per-failure maintainer decision.

---

## RESID-02 crash scope

| Option | Description | Selected |
|--------|-------------|----------|
| Diagnose + targeted fix | Narrowest correct change; guard/detour if root cause is deep | |
| Full root-cause fix only | No targeted guard; fix the underlying defect even if invasive | ✓ |
| Diagnose-only, defer fix | Capture + document, defer fix (violates ROADMAP success criterion) | |

**Follow-up — Full root-cause contingency (game-side fault):**

| Option | Description | Selected |
|--------|-------------|----------|
| Fix in Utinni's surface; document if game-side | Fix if in Utinni's surface; documented root-cause analysis if SWG.exe game code | ✓ |
| Must produce a fix regardless | Ship a fix even if game-side (may mean a detour after all) | |
| Pause + re-scope if repro fails | Stop and re-decide if the crash can't be reliably reproduced | |

**User's choice:** Full root-cause fix; fix in Utinni's surface, documented analysis if the fault is game-side code Utinni can't own.
**Notes:** No masking guard/detour either way. Reproduce via TJT-driven scene-change path; VEH logger stays deployed. Naked-after-scene-change is expected baseline, not a crash signal.

---

## Claude's Discretion

- `tools/Utinni.Tools.sln` internal project layout; manifest/SHA file naming + format.
- Self-hosted-CI step wiring shape (verify-only build lane).
- Which real asset(s) used per tool for the byte-exact smoke (subject to availability).
- Build-pass order across the three tools.

## Deferred Ideas

- Phase 13: wrap tools as `utinni-cli` verbs, SAVE verb, OT Tier-2 typed display.
- Per-dep vcpkg migration (plan 06-02b), post-V1.
- Reviewed-not-folded todos: window-resize (RESID-04/Phase 15), datatable/stringtable residuals, CI-stability flakes.
