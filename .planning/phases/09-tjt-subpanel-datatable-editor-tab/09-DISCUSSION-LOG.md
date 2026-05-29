# Phase 9: TJT subpanel — Datatable Editor (`.tab`) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in `09-CONTEXT.md` — this log preserves the alternatives considered.

**Date:** 2026-05-28
**Phase:** 09-tjt-subpanel-datatable-editor-tab
**Areas discussed:** V1 editing scope (rows vs schema), Schema & type validation strictness, Cross-table FK / reference UX, Bulk editing & productivity ops

---

## Area 1 — V1 editing scope (rows vs schema)

### Turn 1.1 — Editing-scope ceiling

| Option | Description | Selected |
|---|---|---|
| T1 — cell values only | Edit existing cell values; rows + columns frozen. Matches PROD-W1-DT acceptance text exactly. Smallest surface. | |
| T2 — cells + row add/remove (Recommended) | Cell edits + add/remove/reorder rows. Schema (columns + types) frozen. Phase 8 "low ceiling" analog. | |
| T3 — cells + rows + columns | T2 + add/remove/reorder columns. Engine code reading column N by index breaks silently. | |
| T4 — full schema mutation (SOE parity) | T3 + edit column types. SOE `SwgDataTableTool` parity. Type changes force FK targets to re-resolve and engine semantics to re-validate. | ✓ |

**User's choice:** T4 — full schema mutation. SOE parity.
**Notes:** Founder override of the "Recommended" T2 — the one-stop modding-tool vision (memory `project_vision.md`) wants SOE-parity for the Wave-1 datatable surface, not the smallest defensible upgrade.

### Turn 1.2 — T4 boundary clarifications (new-from-scratch + reorder/delete safety)

| Option | Description | Selected |
|---|---|---|
| Existing only + s2 warn-only (Recommended) | Edit existing `.tab` files only; column reorder/delete shows a once-per-session "may break runtime consumers, proceed?" modal. | ✓ |
| Existing only + s3 engine-consumer scan | Edit existing only + real grep-the-engine scan for column-by-index reads with concrete file:line warnings. ½ a plan of its own. | |
| Existing + new-from-scratch + s2 warn | Full SOE parity including new-`.tab`-from-scratch (file → new → schema designer). Largest scope. | |
| Existing only + s1 trust user | No safety net on reorder/delete. Will almost certainly fail SC4 review. | |

**User's choice:** Existing only + s2 warn-only.
**Notes:** "New from scratch" deferred to V2 (`09-CONTEXT.md` Deferred Ideas). s2 modal is honest about risk without claiming the editor verified consumer safety. s3 engine-scan parked as a possible V1.5 phase if cross-AI reviewers push back on SC4 acceptance for T4 schema mutation.

---

## Area 2 — Schema & type validation strictness

### Turn 2.1 — Edit-time strictness

| Option | Description | Selected |
|---|---|---|
| Strict edit-time, typed widgets (Recommended) | Per-type cell editors (Int spinner, Bool checkbox, Enum dropdown, etc.); invalid input blocked at keystroke. Highest correctness; most editor code. | ✓ |
| Loose edit-time + strict save-time | Plain-text cell edits; validate-and-block on save. User learns of errors only on save. | |
| Warn-only (mark and allow) | Plain-text edits; type mismatches highlight red but save succeeds. SOE behavior. | |
| Pass-through (no validation) | Cells as strings; whatever the user types gets serialized. Smallest code, SC4 effectively unenforced. | |

**User's choice:** Strict edit-time + typed widgets.
**Notes:** **Implied A2.2 (recorded in CONTEXT D-04):** type-change cascade (T4 column-type-edit) uses per-cell `DataTableColumnType::mangleValue()` retry; failed cells flagged "needs review" (red); **save is blocked while any flagged cells exist** — consistent strict UX with edit-time. User must resolve or revert the type change before saving.

---

## Area 3 — Cross-table FK / reference UX

### Turn 3.1 — Cross-file reference handling

| Option | Description | Selected |
|---|---|---|
| One-doc-at-a-time + warn dangling (Recommended) | Single `.tab` at a time; DT_HashString cells = plain text + hash preview; **NO** dangling-ref validation on save. Lowest scope; SOE-tool parity. | ✓ |
| Corpus + dropdown picker for hash refs | "Table corpus" subsystem (load N referenced files); DT_HashString dropdowns from resolved values; opt-in FK designation for Int columns. UX win, big architecture lift. | |
| On-save validation only (warn dangling) | No corpus; on-save walk verifying referenced hashtable files exist. Catches typos. | |
| Full FK with engine-canonical mapping | Curated FK conventions (`CombatDataTable.columnN → weapons.tab`) sourced from `sharedGame/*DataTable.cpp`. Brittle / maintenance burden. | |

**User's choice:** One-doc-at-a-time + warn dangling.
**Notes:** The option's label said "warn dangling" but the description clarified **NO** dangling-FK validation in V1. Confirmed in conversation — V1 has zero FK awareness. Dangling-FK detection, dropdown pickers, table corpus, and engine-canonical FK maps all deferred to V2 (`09-CONTEXT.md` Deferred Ideas). SOE `SwgDataTableTool` also lacked these.

---

## Area 4 — Bulk editing & productivity ops

### Turn 4.1 — CSV/TSV round-trip

| Option | Description | Selected |
|---|---|---|
| V1 with delta-import (Recommended) | CSV export + import; import path diffs cell-by-cell and only marks actually-changed cells as dirty. Preserves byte-exact-on-untouched (SC4) across round-trip. Import preview modal: "N cells will change, M will stay original bytes". | ✓ |
| V1 export-only | Export only; no import-back. Trivially preserves SC4; loses iterate-loop value. | |
| Defer to V2 | No CSV; Find/Replace + sort only. Cleanest V1; modders working on 1000-row tables will hate it. | |
| V1 full (no delta) | CSV import without delta logic; every imported row's cells re-emit fresh bytes. Quietly breaks SC4. | |

**User's choice:** V1 with delta-import.
**Notes:** Delta-import is the structural piece that makes CSV round-trip honor CF-04 (Phase 8 D-07 hybrid mutable DOM byte-exact-on-untouched). Without it, CSV roundtrip quietly invalidates SC4 for every "untouched" cell.

### Turn 4.2 — Find/Replace + Sort/Filter + Entry points

| Option | Description | Selected |
|---|---|---|
| Find/Replace + sort + 3 entry points (Recommended) | Find/Replace (Ctrl-F / Ctrl-H, type-aware on replace). Column-click sort (view-only). Three entry points: file picker, TRE Browser "Open in Datatable Editor", IFF Editor "Switch to typed datatable view" (manual hand-off, NOT auto-route). | ✓ |
| Find only + 2 entry points | Find (no Replace). Sort. File picker + TRE Browser only. Loses the "I'm already in IFF Editor and realized this is a datatable" flow. | |
| Find/Replace + sort + filter + 3 entry points | Adds live row filter. DataGridView doesn't auto-filter; needs BindingSource expression or virtual mode. Mid implementation. | |
| Find/Replace + sort + 3 entry points + auto-route on DTII | IFF Editor AUTO-routes to Datatable Editor when opening DTII-root IFF (no menu hand-off). Cleaner one-click flow; surprises users who specifically opened via IFF Editor. | |

**User's choice:** Find/Replace + sort + 3 entry points; manual hand-off (no auto-route).
**Notes:** Row filter deferred to V2 (`09-CONTEXT.md` Deferred Ideas). Auto-route on DTII rejected — preserves the IFF Editor's chunk-tree view as the user's choice when they specifically opened through that route.

---

## Claude's Discretion

Areas left to planner / `/gsd-ui-phase 9`:

- Exact UI layout of the editor SubPanel (toolbar layout, dirty-state indicator placement, CSV-import preview modal, type-change-cascade resolution UX).
- `DT_PackedObjVars` / `DT_BitVector` cell-widget depth (text-input-with-syntax-hint is the floor).
- `DT_Comment` row UX semantics (frozen header vs hidden vs freely editable).
- CSV serialization details (separator, encoding, escape rules, DT_Comment treatment, header row schema).
- Find/Replace scope toggles (column-scoped vs all-cells, case-sensitive, regex).
- CLI verb naming/shape (`roundtrip-tab` is a placeholder).
- Plan decomposition (4–6 plans expected; planner has final say).

---

## Deferred Ideas

Captured in `09-CONTEXT.md` `<deferred>` section. Summary:

- "New `.tab` from scratch" — empty-state schema designer → V2 / dedicated phase
- Cross-table FK / "table corpus" subsystem → V2
- Engine-consumer scan (s3 from Area 1.2) → V1.5 if reviewers push back; else V2
- Live row filter → V2
- In-memory live patch for `.tab` (Phase 8 mode-3 enablement) → follow-up phase
- Art-asset WRITE parity — N/A for datatables; LOCKED DEC-A3 still gates mesh / skeleton / animation / shader
- ImGui chromeless HUD-overlay presentation → optional later polish
- Shared "abstract editor base class" across IFF + Datatable + Phase 10/11 editors → post-Wave-1 refactor candidate

### Reviewed Todos (not folded)

- `gamecallbacks-gc-av-flake-fix.md` — CI-stability, resolved 06-04; keyword false-positive (third time — Phase 7 + Phase 8 also reviewed-not-folded).
- `loader-lock-harness-flake-fix.md` — CI-stability, resolved 06-04; keyword false-positive (third time).
