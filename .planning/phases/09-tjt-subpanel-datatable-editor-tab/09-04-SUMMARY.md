---
phase: 09-tjt-subpanel-datatable-editor-tab
plan: 04
subsystem: datatable-editor
tags: [datatable, editor, undo-redo, cascade, controller, t4-schema-mutation]
requires:
  - "09-01 typed DTII primitives (MutableDataTableDocument/Cell/Column/Row, CaptureState/RestoreState, DataTableColumnType.MangleValue/TryCoerceCellValue)"
  - "09-03 FormDatatableEditor host + ThemedDataGridView + per-type widgets"
  - "Phase 8 IffEditController (verbatim port shape) + FormSaveConfirmDialog + FormFourCcDialog"
provides:
  - "DatatableEditController (pure-managed CF-06 controller): Apply/Undo/Redo + NeedsReviewCount + PendingCascadeContext + MarkSaved + EditApplied"
  - "11 IDatatableEditCommand factories (DatatableEditCommands) + ApplyCsvImport stub for 09-06"
  - "FormAddColumnDialog + FormTypeChangeCascadeDialog (TJT per-call modals)"
  - "FormDatatableEditor controller wire: Add row/column, structural-op context menu, D-04 cascade flow, D-02 safety-net, R-04 NeedsReview save-block, btnResolveCascade"
  - "MutableDataTableCell.RebaselineAfterSave + MutableDataTableRow.InsertCellInternal (framework API additions)"
affects:
  - "09-05 composes Save▾ provenance gating on top of the NeedsReview gate + calls MarkSaved on save success"
  - "09-06 fills ApplyCsvImport body (additive — signature already in place)"
tech-stack:
  added: []
  patterns:
    - "IffEditController verbatim port (Apply/Undo/Redo/netAppliedCount) + Phase-9 seams"
    - "Insert-by-reference undo (Phase 8 CR-01) for RemoveRow + RemoveColumn"
    - "CaptureState/RestoreState byte-exact undo primitive for EditCellValue + ChangeColumnType"
    - "Per-call modal lifecycle (using var dlg) — NOT singleton hide-not-dispose"
    - "Cascade state on the controller (PendingCascadeContext), zero form-local cascade field"
key-files:
  created:
    - "UtinniCoreDotNet/Editing/DatatableEditController.cs"
    - "UtinniCoreDotNet/Editing/IDatatableEditCommand.cs"
    - "UtinniCoreDotNet/Editing/DatatableEditCommands.cs"
    - "UtinniCoreDotNet.Tests/FormatsTests/Datatable/DatatableEditControllerTests.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormAddColumnDialog.cs (+ .Designer.cs)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTypeChangeCascadeDialog.cs (+ .Designer.cs)"
  modified:
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj (3 Editing Compile entries)"
    - "UtinniCoreDotNet/Formats/Datatable/MutableDataTableCell.cs (RebaselineAfterSave)"
    - "UtinniCoreDotNet/Formats/Datatable/MutableDataTableRow.cs (InsertCellInternal)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.cs (+ .Designer.cs — controller wire + btnResolveCascade)"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj (4 Compile entries)"
decisions:
  - "MarkSaved rebaseline = PER-CELL (RebaselineAfterSave): re-serialize each cell's current value via SerializeFresh, adopt as originalSlice, clear dirty/needs-review. Lighter than re-parse; satisfies the seam contract (IsDirty false after MarkSaved, true again on next edit)."
  - "PendingCascadeContext hoisted onto the controller via an internal ICascadeProducingCommand interface the controller reads after every Apply/Undo/Redo (commands take only `document`; the controller pulls ActiveCascade off the just-run command). Keeps the cascade state on the controller, not the form."
  - "Added MutableDataTableRow.InsertCellInternal (Rule 3 — missing referenced API; AddColumn/RemoveColumn-undo/MoveColumn need arbitrary-index cell insert; only AddCellInternal/RemoveCellInternal existed)."
  - "Added MutableDataTableCell.RebaselineAfterSave (Rule 3 — MarkSaved needs a per-cell rebaseline primitive)."
metrics:
  duration: ~75 min
  completed: 2026-05-29
  tasks: 2
  files: 7 created + 6 modified across both repos
---

# Phase 9 Plan 04: T4 Schema-Mutation Engine Summary

DatatableEditController + 11 D-01 T4 commands (cell/row/column ops + D-04 type-change cascade) shipped framework-side with byte-exact undo via CaptureState/RestoreState; two TJT per-call modals (FormAddColumnDialog, FormTypeChangeCascadeDialog) plus the FormDatatableEditor controller wire, D-04 cascade flow, D-02 safety-net, and R-04 NeedsReview save-block.

## DatatableEditController public API

| Member | Shape | Test coverage |
|--------|-------|---------------|
| `DatatableEditController(MutableDataTableDocument)` | ctor, null-guard | FreshController fact |
| `Document` | get-only | (consumed by form) |
| `IsDirty` | `netAppliedCount > 0` (baseline-clean) | Apply/Undo/MarkSaved facts |
| `CanUndo` / `CanRedo` | stack-count | state-machine facts |
| `NeedsReviewCount` | walks every cell, counts NeedsReview | cascade matrix + tally facts |
| `PendingCascadeContext` | nullable `PendingTypeChangeCascade` | 3 cascade-state-machine facts |
| `EditApplied` | `event EventHandler` | EditApplied-fires-once fact |
| `Apply(IDatatableEditCommand)` | Do + push undo + clear redo + net++ + RecomputeCascadeState | all command facts |
| `Undo()` / `Redo()` | symmetric | all command facts |
| `MarkSaved()` | per-cell rebaseline + net = 0 | MarkSaved fact |

`PendingTypeChangeCascade { int ColumnIndex; DataTableColumnType OldType; DataTableColumnType NewType; IReadOnlyList<MutableDataTableCell> NeedsReviewCellRefs }`.

11 commands: `EditCellValue, AddRow, RemoveRow, MoveRowUp, MoveRowDown, AddColumn, RemoveColumn, MoveColumnLeft, MoveColumnRight, RenameColumn, ChangeColumnType` + 12th `ApplyCsvImport` (throws NotImplementedException — Plan 09-06).

xUnit: **37 [Fact]s** in DatatableEditControllerTests (≥34 target). Full Datatable subsuite **111/111** green (73 from 09-01 + new). Both repos build Debug|x86 + Release|x86 clean.

## MarkSaved rebaseline mechanism (iter-2 item 8)

PER-CELL. `MarkSaved()` iterates every cell and calls `MutableDataTableCell.RebaselineAfterSave(columnType)` which re-serializes the cell's CURRENT value via `DataTableCellValue.SerializeFresh`, adopts those bytes as the new `originalSlice`, and clears `isDirty` + `needsReview`. Then `netAppliedCount = 0`. Undo/redo stacks are untouched. Verified: after MarkSaved all cells `IsDirty == false`; the next EditCellValue flips dirty true again (baseline moved to the saved state).

## Cascade dialog flow (textual)

```
ChangeColumnType.Do → mangle every cell → failing cells flagged NeedsReview=true
  → command builds PendingTypeChangeCascade (if ≥1 failing)
  → controller.RecomputeCascadeState hoists it onto PendingCascadeContext
  → OnEditApplied: btnResolveCascade.Visible = (PendingCascadeContext != null)
  → form auto-surfaces FormTypeChangeCascadeDialog (or user clicks Resolve cascade…)
      ├─ Accept (per row): re-coerce displayed value; success → cell.Value set (clears NeedsReview),
      │     row drops out; last cell → controller auto-clears PendingCascadeContext
      ├─ Edit cell: close → form focuses offending cell in main grid → user re-opens via toolbar
      ├─ Done: close; remaining red cells stay flagged; save stays blocked
      └─ Revert type change: form calls controller.Undo() → ChangeColumnType.UndoOp restores
            type + per-cell state (clears NeedsReview) + drops cascade
  → per-cell resolution path: each EditCellValue Apply re-runs RecomputeCascadeState;
    when NeedsReviewCount hits 0 the controller nulls PendingCascadeContext automatically
```

## NeedsReview gate flow (R-04)

`RefreshSaveMenuEnabledState` reads `controller.NeedsReviewCount`; when `> 0` it disables all 5 Save▾ menu items (`miSaveInPlace/miSaveLooseOverride/miSaveAs/miPatchLive/miRepackTre`) AND `btnSave`, surfacing the locked tooltip `"Resolve {N} cell(s) that need review before saving."` on EACH item via `ApplyNeedsReviewGate` (not just the button face). Base-enabled stays false in this plan (save targets are 09-05); Plan 09-05 composes the provenance enable ON TOP of this gate.

## ZERO lastCascadeContext (iter-2 item 6)

`grep -c "lastCascadeContext" FormDatatableEditor.cs` → **0**. The Resolve-cascade button visibility + re-open read exclusively from `controller.PendingCascadeContext` (`grep -c "PendingCascadeContext"` → 6). No form field tracks cascade state.

## Insert-by-reference (Phase 8 CR-01 port)

RemoveRow.UndoOp re-inserts the SAME `MutableDataTableRow` instance (`doc.Rows.Insert(originalIndex, row)`); RemoveColumn.UndoOp re-inserts the SAME column + the SAME per-row cells by reference (`InsertCellInternal(originalIndex, captured)`). xUnit asserts row-instance + cell-instance equality after Undo (`RemoveRow_..._RestoresSameInstanceByReference`, `RemoveColumn_..._RestoresByReference`) and byte-exact round-trip after RemoveColumn→Undo.

## Deviations from Plan

### Auto-fixed Issues (Rule 3 — missing referenced framework API)

**1. [Rule 3 - Blocking] Added MutableDataTableRow.InsertCellInternal(int, cell)**
- **Found during:** Task 1 (AddColumn.Do / RemoveColumn.UndoOp / MoveColumn need arbitrary-index cell insert)
- **Issue:** 09-01 shipped only `AddCellInternal` (append) + `RemoveCellInternal(int)`; no insert-at-index.
- **Fix:** Added `internal void InsertCellInternal(int index, MutableDataTableCell cell)` wiring the ParentRow back-ref. Same-assembly internal (Editing namespace consumes it).
- **Files:** UtinniCoreDotNet/Formats/Datatable/MutableDataTableRow.cs · Commit: 997716a

**2. [Rule 3 - Blocking] Added MutableDataTableCell.RebaselineAfterSave(columnType)**
- **Found during:** Task 1 (controller.MarkSaved per-cell rebaseline seam)
- **Issue:** No primitive to re-baseline a cell's slice after save without going through the slice-nulling Value setter.
- **Fix:** Added `internal void RebaselineAfterSave(DataTableColumnType)` — re-serialize current value → originalSlice, clear dirty/needs-review.
- **Files:** UtinniCoreDotNet/Formats/Datatable/MutableDataTableCell.cs · Commit: 997716a

### Port deviation from IffEditController

The controller is a near-verbatim port of IffEditController.cs:67-160 (identical Apply/Undo/Redo/netAppliedCount idiom + variable names). The ONLY additions are the documented Phase-9 seams (`NeedsReviewCount`, `PendingCascadeContext` + `RecomputeCascadeState`, `MarkSaved`). The cascade hoist uses an internal `ICascadeProducingCommand` interface (the controller reads `ActiveCascade` off the just-run command after Do/UndoOp) rather than threading the controller into command Do() — this preserves the `IDatatableEditCommand.Do(document)` signature identical to `IIffEditCommand.Do(document)`.

### Interface-block correction

The plan's `<interfaces>` block cited `ConfirmOutcome.Confirmed`; the real Phase 8 enum is `ConfirmOutcome.Accepted` / `Cancelled`. The D-02 safety-net wire uses the real `Accepted`/`Cancelled` values.

## Cross-repo paired commits

- Utinni (framework + tests): **997716a** `feat(09-04): ship DatatableEditController + 11 T4 commands + cascade + MarkSaved`
- UtinniPlugins (TJT forms): **0868c88** `feat(09-04): ship FormAddColumnDialog + FormTypeChangeCascadeDialog + wire controller`

## Self-Check: PASSED
