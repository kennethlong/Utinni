---
status: partial
phase: 09-tjt-subpanel-datatable-editor-tab
source: [09-VERIFICATION.md, 09-07-SMOKE-LOG.md]
started: 2026-05-29
updated: 2026-05-29
---

## Current Test

[awaiting maintainer live session — batched 09-03 editor-host + 09-07 live-SWG]

## Tests

The full step-by-step checklist lives in `09-07-SMOKE-LOG.md` (Part A: 09-03 editor-host, no
live SWG; Part B: 09-07 Tier-4 live-SWG). Summary of what needs human eyes:

### 1. 09-03 editor-host smoke (no live SWG)
expected: Datatable Editor opens 1200x760 dark-themed; per-type widgets (bool checkbox, enum dropdown, DT_HashString int + Consolas-9pt `{source} -> 0x{hash:X8}` preview); edit commits on tab-away; close→reopen shows NO ObjectDisposedException (hide-not-dispose); reload badge `Reloads on next scene change.`; disabled-toolbar tooltips name their wiring plan (no throwing dialogs).
result: [pending]

### 2. 09-07 Tier-4 live-SWG smoke
expected: subpanel loads under live MEF; 3 entry points (file picker / TRE Browser hand-off / IFF Editor "Switch to typed datatable view"); edit + structural ops; D-04 type-change cascade with R-04 save-block on every Save▾ item; D-02 reorder safety-net; 4 save modes; CF-05 TJT scene-change reload picks up the edit in-client; CSV byte-exact import; Find/Replace; view-only sort then EDIT a cell — confirm the edit lands on the row you clicked (CR-01 fix); singleton re-open.
result: [pending]

## Summary

total: 2
passed: 0
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps

(none — deferred-but-acceptable for V1 per Phase 8 P05/P06/P07 precedent; code + automation complete, criticals fixed)
