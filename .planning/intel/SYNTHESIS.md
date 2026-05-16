# Synthesis Summary

Single entry point for downstream consumers (notably `gsd-roadmapper`). Summarises what was synthesized from the 2026-05-16 doc ingest.

---

## Doc counts by type

| Type | Count | Sources |
| --- | --- | --- |
| ADR  | 0 | — |
| SPEC | 0 | — |
| PRD  | 0 | — |
| DOC  | 3 | vision.md, assessment.md, test-harness-plan.md |

All three classified DOC. No locked decisions inherited from this ingest.

---

## Decisions extracted

- **9 candidate decisions** (D-01 through D-09), all non-locked. See `decisions.md`.
- Most natural ADR-promotion candidates if maintainer wants to gate downstream: D-01 (Utinni is the integrated modding tool), D-04 (anti-goals), D-08 (testing strategy).
- D-02, D-05, D-07, D-09 may belong in ROADMAP.md as phase ordering rather than as ADRs.

Sources: vision.md (D-01..D-05), assessment.md (D-06, D-07, D-09), test-harness-plan.md (D-07, D-08).

---

## Requirements extracted

15 candidate requirements across three families. See `requirements.md`.

### Product capability (from vision.md) — 9 items
- REQ-one-stop-tool
- REQ-see-everything-the-client-loads
- REQ-edit-major-asset-types
- REQ-live-preview-edits
- REQ-author-new-content
- REQ-one-click-package
- REQ-share-to-hub
- REQ-wave-1-plugins, REQ-wave-2-plugins, REQ-wave-3-plugins (plugin waves)

### Framework-stability remediation (from assessment.md) — 4 items
- REQ-fix-critical-bugs (C-01..C-15)
- REQ-strategic-reworks (R-A..R-H)
- REQ-cleanups (~30 items)
- REQ-preserve-foundations (24 items)
- REQ-resolve-open-questions (8 items)

### Testing infrastructure (from test-harness-plan.md) — 5 items
- REQ-tier-1-csharp-unit-tests
- REQ-tier-1-cpp-unit-tests
- REQ-tier-2-cli-shim
- REQ-tier-3-mock-d3d9-replay
- REQ-explicit-tier-4-boundary

None carry first-class acceptance criteria — they were derived from prose DOCs. Roadmapper may want to expand the most product-critical ones into PRDs with measurable AC before gating phases on them.

---

## Constraints extracted

See `constraints.md`. Four families:

| Family | Count | Notes |
| --- | --- | --- |
| Preservation ("do not refactor") | 23 enumerated | 9 native, 9 managed, 5 process/tooling |
| Scope (anti-goals) | 4 | Not server-side, not launcher, not DCC, not cheat |
| Technical / platform | ~14 | Windows-only, x86, DXSDK June 2010, VS 2019+2022, cross-CRT discipline, DllMain limits, RVA SoT, callback symmetry, etc. |
| Open / unresolved | 11 | 8 from assessment.md + 3 from test-harness-plan.md |

Most important takeaway for the roadmapper: every preservation constraint (CON-N-*, CON-M-*, CON-T-*) acts as a guard-rail. Any phase that touches a preserved item needs explicit justification.

---

## Context topics

See `context.md`. Topics covered:

- Product positioning (competitive landscape, framework leverage)
- Strategic position vs upstream (sovereign MIT fork, dormant upstream)
- Architecture as audited 2026-05-16
- The 1.0 work, by phase (9-week sequencing)
- Plugin pipeline waves (Wave 1–4)
- Test harness strategy (four tiers + phase order)
- Open questions inherited from project history
- Cross-document linkage

---

## Conflicts

- **BLOCKERS:** 0
- **WARNINGS:** 0
- **INFO:** 4

See `D:/Code/Utinni/.planning/INGEST-CONFLICTS.md` for the full report. All three ingested docs are DOC (lowest precedence) so no precedence contest occurred. The vision↔assessment "See also" reciprocity was flagged and dismissed as benign narrative linkage rather than a content-derivation cycle.

---

## Pointers

- `D:/Code/Utinni/.planning/intel/decisions.md` — 9 candidate decisions
- `D:/Code/Utinni/.planning/intel/requirements.md` — 15 candidate requirements
- `D:/Code/Utinni/.planning/intel/constraints.md` — preservation + scope + technical + open
- `D:/Code/Utinni/.planning/intel/context.md` — topical context
- `D:/Code/Utinni/.planning/INGEST-CONFLICTS.md` — conflict report
- `D:/Code/Utinni/.planning/intel/classifications/` — original per-doc classification JSON
- `D:/Code/Utinni/.planning/codebase/` — prior map-codebase intel (reference, not part of this ingest)

---

## STATUS

**READY — safe to route to `gsd-roadmapper` (or `gsd-new-project` for V1 bootstrap).**

No blockers. No competing variants requiring user resolution. All ingested content is DOC-precedence; downstream PROJECT.md/REQUIREMENTS.md/ROADMAP.md drafting should treat the candidate decisions and requirements as inputs to refine, not as locked facts to honour.
