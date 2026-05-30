# Phase 10: TJT subpanel — String-table Editor (`.stf`) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-29
**Phase:** 10-tjt-subpanel-string-table-editor-stf
**Areas discussed:** Editing scope, Write strategy & byte-exactness, Translation/bulk features, Editor surface

---

## Editing scope (parity tier)

| Option | Description | Selected |
|--------|-------------|----------|
| Full SOE parity (T4) | Edit text + add/remove entries + rename keys (+ id?). Matches SOE SwgStringEditor and Phase 9's full-parity precedent. | ✓ |
| Text-edit only (T1) | Just edit existing text + save. Literal PROD-W1-STF acceptance. | |
| T4 minus id-edit | Full parity except raw id editing (ids machine-managed). | |

**User's choice:** Full SOE parity (T4)
**Notes:** Combined with the "Editor surface = you decide (UI-phase)" answer, which locks "id machine-managed, never user-edited" — so the effective scope is T4 with ids never surfaced for manual editing (the "T4 minus id-edit" UX, reached via the parity choice + surface deferral). Mirrors Phase 9 D-01 founder call. No "new .stf from scratch" (→ V2).

---

## Write strategy & byte-exactness

| Option | Description | Selected |
|--------|-------------|----------|
| Canonical re-serialize, faithful text | Engine-canonical order (strings id↑, names name↑); verbatim UTF-16LE, no smart-quote unfubar; self-consistent roundtrip-stf golden. | |
| Original-byte/order preservation | Preserve each untouched entry's original bytes + original order (Phase 9 CF-04 style for the flat format). | |
| You decide (recommend canonical) | Defer to research against real .stf fixtures; lean canonical, escalate to original-byte only if non-canonical ordering exists in the wild. | ✓ |

**User's choice:** You decide (recommend canonical)
**Notes:** Captured as D-02 — lean canonical re-serialize + faithful text; planner/researcher validates against real fixtures and escalates to original-byte preservation only with evidence. Text faithfulness (no `unfubar`, João survives) is non-negotiable in both paths. crc-on-edit policy (D-02b) deferred to planner with a researcher pre-req: confirm whether the live client reads `sourceCrc`.

---

## Translation / bulk-edit features (multi-select)

| Option | Description | Selected |
|--------|-------------|----------|
| Find/Replace across text | Ctrl-F / Ctrl-H over key + text. | ✓ |
| CSV/TSV export + delta-import | Export (key,text) UTF-8+BOM; per-entry diff import. | ✓ |
| Sort + filter by key/text | View-only column sort + live filter box. | ✓ |
| PO / gettext export | Export to .po for Poedit/Weblate. | ✓ |

**User's choice:** All four selected
**Notes:** All translation/bulk features are first-class V1 (D-03). PO export (D-03d) noted as lowest priority / first cut-to-V2 candidate under scope pressure; PO *import* is explicitly not V1.

---

## Editor surface (columns / what's editable)

| Option | Description | Selected |
|--------|-------------|----------|
| Key + Text editable; id/crc read-only | Two editable columns; id/crc shown read-only/diagnostic; crc auto-managed. | |
| Key + Text only; id/crc fully hidden | Pure localization view; id/crc invisible machinery. | |
| You decide (UI-phase) | Defer columns + crc-on-edit to /gsd-ui-phase 10 + planner; lock only text+key editable, id machine-managed. | ✓ |

**User's choice:** You decide (UI-phase)
**Notes:** Locked floor — text + key editable, id machine-managed (never user-edited). Column layout, id/crc visibility, and crc-on-edit default deferred to UI-SPEC + planner.

---

## Ready-check

Asked whether to lock in or revisit PO export / discuss crc-on-edit before writing CONTEXT.
**User's choice:** Lock it in — write CONTEXT.

## Claude's Discretion

- Editor surface / columns, id/crc visibility, dirty-state placement, add/remove/rename UX, CSV-import preview modal → `/gsd-ui-phase 10` + planner.
- D-02b crc-on-edit policy (preserve / fresh int(time(0)) / 0) → planner, gated on researcher confirming whether the client reads sourceCrc.
- `StringTableDocument` ↔ existing `StringTableDecoder` relationship (wrap/reuse vs supersede) → planner (recommend reuse).
- CLI verb naming (`roundtrip-stf` placeholder) → planner per Phase 4/8/9 conventions.
- Plan decomposition (expect 3–5 plans) → planner.

## Deferred Ideas

- New `.stf` from scratch (empty-state designer) → V2.
- Raw id editing → V2 (ids machine-managed in V1).
- Cross-reference resolution / "find usages" / dangling-key warnings → V2.
- PO *import* (gettext round-trip) → V2; PO export itself is the first cut-to-V2 candidate under scope pressure.
- Multi-language side-by-side editing → V2.
- In-memory live patch for `.stf` (Phase 8 mode 3) → follow-up enabler phase.
- ImGui chromeless HUD-overlay presentation → optional later polish (non-binding).
- Shared abstract editor base class across the four Wave-1 editors → post-Wave-1 refactor.
