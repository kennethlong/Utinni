---
title: Resolve Phase 9 code-review Warnings (7) + Info (5) — datatable editor edges
created: 2026-05-29
priority: medium
area: code-quality
discovered_in: phase-09
related:
  - "[[project_utinnicore_cs_regen_churn]]"
  - .planning/phases/09-tjt-subpanel-datatable-editor-tab/09-REVIEW.md   # full findings
suggested_resolves_phase: 10  # natural home alongside the next TJT editor, or a dedicated cleanup pass
---

## Problem

The Phase 9 code review (09-REVIEW.md) found 2 Critical + 7 Warning + 5 Info. The 2 Criticals
were fixed/cleared before phase completion (CR-01 row-index translation + regression fact;
CR-02 was a false positive, hardened anyway). The 7 Warnings + 5 Info were deferred as
non-blocking follow-ups and are tracked here.

## Warnings (from 09-REVIEW.md)

- **WR-01** — Cascade context permanently lost when undoing a per-cell resolution: save stays blocked (`NeedsReviewCount > 0`) with no visible cascade to resolve. `DatatableEditController.cs:237-252`.
- **WR-02** — Cascade "Accept mangled" mutates `cell.Value` outside the controller: not undoable, no `EditApplied`, doesn't auto-clear the cascade. `FormTypeChangeCascadeDialog.cs:146-166`.
- **WR-03** — `AddRowCommand.UndoOp` removes by stale `atIndex` instead of by reference (unlike sibling commands). `DatatableEditCommands.cs:270-273`.
- **WR-04** — `MoveColumnCommand` leaks `IsReordered = true` on undo. `DatatableEditCommands.cs:464-488`.
- **WR-05** — NumericUpDown editor formats with `CurrentCulture` but coercion parses `InvariantCulture` → float edits corrupt/reject on comma-decimal locales. `DatatableNumericUpDownEditingControl.cs` vs `DataTableColumnType.cs:313-322`.
- **WR-06** — `BuildRowsPayload` throws `IndexOutOfRangeException` (not structured) for a ragged in-memory doc on save. `MutableDataTableDocument.cs:242-260`.
- **WR-07** — `[Serializable] DataTableParseException` omits the serialization constructor.

## Info

Dead `IndexOfRow`, placeholder column names in the CSV preview, `AddSetting`-as-Set,
maximized-bounds persistence, shared-mutable-IFF-tree hand-off snapshot note. See 09-REVIEW.md.

## Notes

WR-01/WR-02 (cascade-undo integrity) and WR-05 (locale float parse) are the highest-value
of the set — the others are robustness/cleanup. None corrupt byte-exact save on the happy path.
