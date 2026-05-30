---
phase: 09-tjt-subpanel-datatable-editor-tab
reviewed: 2026-05-29T00:00:00Z
depth: standard
files_reviewed: 27
files_reviewed_list:
  - UtinniCoreDotNet/Formats/Datatable/MutableDataTableCell.cs
  - UtinniCoreDotNet/Formats/Datatable/DataTableWriter.cs
  - UtinniCoreDotNet/Formats/Datatable/DataTableCellValue.cs
  - UtinniCoreDotNet/Formats/Datatable/DataTableColumnType.cs
  - UtinniCoreDotNet/Formats/Datatable/DataTableDocument.cs
  - UtinniCoreDotNet/Formats/Datatable/MutableDataTableDocument.cs
  - UtinniCoreDotNet/Formats/Datatable/MutableDataTableColumn.cs
  - UtinniCoreDotNet/Formats/Datatable/MutableDataTableRow.cs
  - UtinniCoreDotNet/Formats/Datatable/DataTableHashCrc.cs
  - UtinniCoreDotNet/Formats/Datatable/DataTableParseException.cs
  - UtinniCoreDotNet/Formats/Datatable/CsvCellCoercion.cs
  - UtinniCoreDotNet/Editing/IDatatableEditCommand.cs
  - UtinniCoreDotNet/Editing/DatatableEditController.cs
  - UtinniCoreDotNet/Editing/DatatableEditCommands.cs
  - UtinniCoreDotNet/UI/SingletonFormClosePolicy.cs
  - Utinni.Cli/Commands/RoundtripTabCommand.cs
  - Utinni.Cli/Program.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/Saving/DatatableCsvSerializer.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/Saving/DatatableSaveTargets.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/DatatableColumnFactory.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/DatatableHashStringEditor.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/DatatableNumericUpDownEditingControl.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/ThemedDataGridView.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormAddColumnDialog.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTypeChangeCascadeDialog.cs
  - The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormCsvImportPreviewDialog.cs
findings:
  critical: 2
  warning: 7
  info: 5
  total: 14
critical_resolved: 2
status: criticals_resolved
resolution:
  CR-01: "FIXED — UtinniPlugins 555e003 (visual->model row-Tag translation) + Utinni e2ac2ca (edit-after-sort regression fact). Full suite 476/476, both solutions build Debug|x86."
  CR-02: "FALSE POSITIVE — FormSaveConfirmDialog defaults Outcome=Cancelled + private-set, so X/Esc already blocked. Hardened to != Accepted anyway (UtinniPlugins 555e003)."
  warnings: "DEFERRED — 7 Warnings + 5 Info tracked as follow-ups (not phase-blocking; see WR-01..07 below)."
---

# Phase 9: Code Review Report

**Reviewed:** 2026-05-29
**Depth:** standard (per-file + cross-repo seam analysis)
**Files Reviewed:** 27
**Status:** criticals_resolved (2 Critical fixed/cleared; 7 Warning + 5 Info deferred as tracked follow-ups)

> **Resolution (2026-05-29):** Both Criticals addressed before phase completion.
> - **CR-01** (real, silent sort+edit corruption) — FIXED. `ThemedDataGridView.BindMutable` now stamps each grid row's model index into `Tag`; `ToModelRowIndex`/`ToVisualRowIndex` translate, and every model↔visual crossing (CommitCell, RemoveSelectedRow, MoveSelectedRow, Find-jump, FocusCell, ApplyCommentRowFreeze, CellFormatting) routes through them. Regression fact `EditAfterSort_TranslatesVisualToModel_PreservesOtherRows` locks the invariant. Commits: UtinniPlugins `555e003`, Utinni `e2ac2ca`.
> - **CR-02** (safety-net X/Esc bypass) — FALSE POSITIVE on re-inspection: `FormSaveConfirmDialog` has a 2-value enum, defaults `Outcome = Cancelled`, and is private-set, so the existing `== Cancelled` guard already blocked an X/Esc close. Hardened to `!= Accepted` to match the sibling repack guard and future-proof against a third enum value.
> - **7 Warnings + 5 Info** remain open as tracked, non-blocking follow-ups.

## Summary

Phase 9 ships a competent typed-DTII editor. The framework layer (`Formats/Datatable/*`) is largely faithful to the SOE port and the CF-04 byte-preservation invariant is correctly implemented at the cell level: `MutableDataTableCell.WritePayload` re-emits the captured slice for clean cells, the `Value` setter nulls the slice on a real change, and `CaptureState`/`RestoreState` make undo byte-exact. The `roundtrip-tab` CLI harness and its per-cell-slice comparison are sound. I could not fault the CRC port, the IFF composition, or the DoS caps.

The two BLOCKERs are both at the **grid↔model index seam**, not in the framework:

1. The grid enables **view-only column sort** (`SortMode.Automatic`) but EVERY grid event handler (`CommitCell`, `OnCellFormatting`, `OnCellValueNeeded`, `JumpToCurrentMatch`, `OnCellBeginEdit`) treats the grid's visual `RowIndex` as the model row index. After the user sorts, a cell edit writes to the **wrong model row** — a silent-corruption path that defeats SC4 at the UI layer (the framework is byte-exact, but the edit lands on the wrong cell before it ever reaches the framework).

2. The D-02 column reorder/delete **safety-net confirmation can be bypassed** by closing the dialog with the window X — `ConfirmColumnSafetyNet` only treats `== Cancelled` as a cancel, so an `X`/Esc close returns `true` and proceeds with the destructive column op.

The WARNINGs cluster around undo/redo asymmetry (cascade-context loss on undo, non-reference AddRow undo, `IsReordered` flag leak), an out-of-controller mutation in the cascade dialog, a float locale mismatch between the numeric editor and the coercion layer, and an `IndexOutOfRange` exposure in `BuildRowsPayload` for a ragged in-memory doc.

---

## Critical Issues

### CR-01: Cell edits write to the wrong model row after a view-only sort (silent corruption, SC4)

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.cs:1020-1085` (`CommitCell`); enabled at `FormDatatableEditor.cs:297-301` / `393-397`
**Also affects:** `ThemedDataGridView.cs:259-267` (`OnCellValueNeeded`), `ThemedDataGridView.cs:336-395` (`OnCellFormatting`), `FormDatatableEditor.cs:526-536` (`JumpToCurrentMatch`), `FormDatatableEditor.cs:1171-1191` (`OnCellBeginEdit`)

**Issue:** `LoadDocument`/`RebindGrid` set every column to `DataGridViewColumnSortMode.Automatic`. For a non-data-bound `DataGridView`, a header-click sort **physically reorders the `Rows` collection**, so the grid's `e.RowIndex` (visual order) no longer equals the model index. But `CommitCell(int rowIndex, …)` does:

```csharp
MutableDataTableRow row = dtDocument.Mutable.Rows[rowIndex];   // model index
object gridValue = gridSurface.Rows[rowIndex].Cells[columnIndex].Value; // visual index
```

After any sort these two `rowIndex` lookups address different rows, so the edited grid value is committed onto a **different model row** than the one the user edited. `OnCellFormatting`/`OnCellValueNeeded` similarly mispaint and (in VirtualMode) mis-serve values, and Find's `JumpToCurrentMatch` selects the wrong cell. The `Sort_DoesNotMutateModelOrder` fact only proves the model list isn't reordered — it does not exercise an edit-after-sort, which is exactly the corruption path. This is silent: no exception, wrong bytes saved.

**Fix:** Map the grid's visual row back to the model row before touching the model. Either disable sorting (`SortMode = NotSortable`) until a stable view→model map ships, or carry the model index on the row and resolve it:

```csharp
// On bind (non-virtual), stash the model index on each grid row:
gridRow.Tag = r;                       // r = model row index
// In CommitCell / OnCellFormatting / OnCellValueNeeded, resolve it:
int modelRow = (int)gridSurface.Rows[gridRowIndex].Tag;
MutableDataTableRow row = dtDocument.Mutable.Rows[modelRow];
```

VirtualMode (`OnCellValueNeeded`/`CellValuePushed`) cannot sort at all without a custom `SortCompare`, so the index identity must be guaranteed there too. Add a fact that edits a cell after sorting a column and asserts the correct model cell changed.

### CR-02: Column reorder/delete safety-net is bypassed by an X/Esc dialog close

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.cs:845-861` (`ConfirmColumnSafetyNet`)

**Issue:** The destructive-column safety net returns `true` (proceed) unless `dlg.Outcome == ConfirmOutcome.Cancelled`:

```csharp
dlg.ShowDialog(this);
if (dlg.Outcome == FormSaveConfirmDialog.ConfirmOutcome.Cancelled) return false;
if (dlg.BackupRequested) sessionSuppressColumnSafetyNet = true;
return true;
```

If the user dismisses the modal with the window **X button or Esc** (rather than the "Cancel" button), `Outcome` is whatever the dialog's default is — and the sibling `OnRepackTreClick` (line 1393-1394) defends with the opposite, stronger predicate `if (dlg.Outcome != ConfirmOutcome.Accepted) return;`. The asymmetry means a non-Accepted, non-Cancelled close (X/Esc) **proceeds with the irreversible column reorder/delete** — the exact action the safety net exists to guard. Worse, `RemoveSelectedColumn`/`MoveSelectedColumn` then run the destructive op.

**Fix:** Gate on explicit acceptance, mirroring `OnRepackTreClick`:

```csharp
dlg.ShowDialog(this);
if (dlg.Outcome != FormSaveConfirmDialog.ConfirmOutcome.Accepted) return false;
if (dlg.BackupRequested) sessionSuppressColumnSafetyNet = true;
return true;
```

---

## Warnings

### WR-01: Type-change cascade context is permanently lost on undo of a per-cell resolution

**File:** `UtinniCoreDotNet/Editing/DatatableEditController.cs:237-252` (`RecomputeCascadeState`)

**Issue:** `RecomputeCascadeState` only re-reads `ActiveCascade` from `ICascadeProducingCommand`; for a non-cascade command (e.g. an `EditCellValue` that resolves the last NeedsReview cell) it auto-clears `pendingCascade` when `NeedsReviewCount` hits 0. But if the user then **undoes that `EditCellValue`**, `NeedsReviewCount` climbs back above 0, yet `producer` is null (EditCellValue isn't a cascade producer) so `pendingCascade` stays `null`. The Resolve-cascade button and save-block tooltip silently disappear even though cells again need review — and `RefreshSaveMenuEnabledState` still blocks save on `NeedsReviewCount > 0`, leaving the user with a blocked save and no visible cascade to resolve.

**Fix:** When `pendingCascade == null` but `NeedsReviewCount > 0` after an op, reconstruct or retain the cascade record (e.g. keep the last non-null cascade and only drop it when `NeedsReviewCount == 0`), or have `RecomputeCascadeState` rebuild `NeedsReviewCellRefs` from the live model when needs-review cells exist without an active producer.

### WR-02: Cascade "Accept mangled" mutates the model outside the undo stack and does not re-run the save gate

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTypeChangeCascadeDialog.cs:146-166`

**Issue:** "Accept" sets `cell.Value = coerced` directly — not through `controller.Apply(EditCellValue(...))`. Consequences: (a) the resolution is **not on the undo stack** (Ctrl+Z won't revert an Accept, only the original ChangeColumnType), creating an asymmetric undo history; (b) it does not raise `EditApplied`, so `RecomputeCascadeState` never runs for this change and `PendingCascadeContext` is not auto-cleared even when Accept resolves the final cell; (c) `netAppliedCount` is unchanged, so dirty accounting diverges from the actual model state. The dialog closes itself on `affected.Count == 0` and comments that the controller "will have auto-cleared" the cascade, but nothing triggers that recompute.

**Fix:** Route Accept through the controller: `controller.Apply(DatatableEditCommands.EditCellValue(cell, coerced))` (the dialog needs a controller reference), so the resolution is undoable, raises `EditApplied`, and drives the auto-clear path.

### WR-03: `AddRowCommand.UndoOp` removes by stale index instead of by reference

**File:** `UtinniCoreDotNet/Editing/DatatableEditCommands.cs:270-273`

**Issue:** `AddRowCommand` stores `addedRow` but `UndoOp` does `doc.Rows.RemoveAt(atIndex)` — removing by the index captured at Do time. The sibling `RemoveRowCommand`/`RemoveColumnCommand`/`AddColumnCommand` all use reference-based logic (`IndexOf(addedRow)`) precisely to survive index shifts. If any intervening (then-undone) structural op shifts row indices, `RemoveAt(atIndex)` removes the wrong row or throws `ArgumentOutOfRangeException`. Inconsistent with the CR-01-fix posture the phase otherwise follows.

**Fix:**
```csharp
public void UndoOp(MutableDataTableDocument doc)
{
    int idx = doc.Rows.IndexOf(addedRow);
    if (idx < 0) idx = atIndex;
    doc.Rows.RemoveAt(idx);
}
```

### WR-04: `MoveColumnCommand` leaks `IsReordered = true` on undo

**File:** `UtinniCoreDotNet/Editing/DatatableEditCommands.cs:464-488` (`Shift`)

**Issue:** `Shift` sets `col.IsReordered = true` on every move but `UndoOp` (which calls `Shift(-direction)`) never restores the prior `IsReordered` value. After move-then-undo the column is back in its original position but still flagged reordered, which can drive incorrect dirty/serialization hints. `MoveRowCommand` has no equivalent flag and is fine.

**Fix:** Capture `bool wasReordered = col.IsReordered;` before the first Do and restore it in `UndoOp`, or only set the flag in `Do` and clear it when the net index returns to origin.

### WR-05: NumericUpDown editor uses CurrentCulture but coercion parses InvariantCulture — float edits break on comma-decimal locales

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/DatatableNumericUpDownEditingControl.cs:70-83,107-110` vs `UtinniCoreDotNet/Formats/Datatable/DataTableColumnType.cs:313-322`

**Issue:** `EditingControlFormattedValue` and `GetEditingControlFormattedValue` format the decimal with `CultureInfo.CurrentCulture`. The committed grid string then flows into `CommitCell` → `ct.TryCoerceCellValue(raw, …)` → `float.TryParse(mangled, NumberStyles.Float, CultureInfo.InvariantCulture, …)`. On a locale where the decimal separator is `,` (e.g. de-DE), the control produces `1,5` which InvariantCulture parses as `15` (group separator) or rejects — a silent value corruption or a rejected edit for legitimate input.

**Fix:** Format the editing control's value with `CultureInfo.InvariantCulture` (or coerce with `CurrentCulture`), so the two ends agree. Given the on-disk format is culture-independent, standardize the whole numeric edit path on InvariantCulture.

### WR-06: `BuildRowsPayload` throws `IndexOutOfRangeException` for a ragged in-memory document

**File:** `UtinniCoreDotNet/Formats/Datatable/MutableDataTableDocument.cs:242-260`

**Issue:** The serializer iterates `for (c = 0; c < Columns.Count; …) cells[c].WritePayload(...)` without guarding `c < cells.Count`. The class itself acknowledges a row "may have fewer cells than columns only in a malformed in-memory doc" (line 136-137 of the same file) and guards it in `RemoveColumnAt`, but the write path does not. A row/column desync introduced by a buggy structural op (see WR-03/WR-04) would surface here as an uncaught `IndexOutOfRangeException` on save rather than a structured `DataTableParseException`, and could partially write. The reader builds rows in lock-step so this is unreachable from a clean load — but it is reachable from the edit commands the phase adds.

**Fix:** Guard and fail structured:
```csharp
if (c >= cells.Count)
    throw new DataTableParseException("Row " + r + " has " + cells.Count
        + " cells but document has " + Columns.Count + " columns.");
cells[c].WritePayload(bw, Columns[c].ColumnType);
```

### WR-07: `[Serializable]` exceptions omit the serialization constructor

**File:** `UtinniCoreDotNet/Formats/Datatable/DataTableParseException.cs:46-63`; same pattern `UtinniCoreDotNet/Formats/Datatable/CsvCellCoercion.cs:40-46` (`CsvParseException`, not even marked `[Serializable]`)

**Issue:** `DataTableParseException` is `[Serializable]` but lacks the `protected DataTableParseException(SerializationInfo, StreamingContext)` constructor. If it is ever marshaled across an AppDomain/remoting boundary (or a tool serializes it), deserialization throws. `CsvParseException` is thrown across the `Task.Run` boundary in `DatatableCsvSerializer.ImportAsync` — within a process that is fine, but it is inconsistent (one is `[Serializable]`, one is not).

**Fix:** Add the standard protected serialization constructor to `DataTableParseException`, and either mark `CsvParseException` `[Serializable]` with the same ctor or document why it is intentionally not. (Match whatever the Phase 8 `IffParseException` analog does for consistency.)

---

## Info

### IN-01: `FormTypeChangeCascadeDialog.IndexOfRow` is dead code — always returns -1

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTypeChangeCascadeDialog.cs:125-131`

**Issue:** `IndexOfRow` unconditionally `return -1;` (the body explains it "cannot reach the document from the cell"). The caller `cell.Row != null ? IndexOfRow(cell) : -1` therefore always yields -1, and the cascade grid's "Row" column always shows `"?"`, degrading the resolution UX. The cell already has a `Row` back-ref and the row has `ParentDocument` — the index is reachable via `cell.Row.ParentDocument.Rows.IndexOf(cell.Row)` (would require exposing the doc/back-ref). Either wire it up or drop the column.

### IN-02: CSV import preview shows placeholder column names ("col 3") instead of real names

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormCsvImportPreviewDialog.cs:104-123`

**Issue:** The per-column diff grid renders `colName[p.Col] = "col " + p.Col` and `colType = ""` because the dialog receives only the `CsvImportPlan` (which carries no column names/types for `Changes`). The UI-SPEC copy is "{columnName} ({DT_Type}): {touched} rows touched" but the user sees "col 3 () : N rows touched". Pass the target document (or enrich `EditCellPatch`/the plan with column name+type) so the preview is meaningful.

### IN-03: `OnEditCommentRowsToggled` re-adds a setting on every toggle instead of updating it

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.cs:687-693`

**Issue:** The handler calls `ini.AddSetting("DatatableEditor", "editCommentRows", …)` on each toggle to persist the value, relying on AddSetting's update-or-add semantics. Combined with the same call in `CreateSettings`, this is a confused use of the settings API (Add used as Set). Functionally tolerable if `AddSetting` upserts, but it obscures intent; prefer a dedicated setter if one exists.

### IN-04: `FormClosing` persists `Width`/`Height` while possibly maximized

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.cs:1750-1761`

**Issue:** On close the form writes `Width`/`Height` directly; if the window is maximized these are the maximized bounds, so the next open restores an over-large window (and ignores `RestoreBounds`). Persist `RestoreBounds.Size` when `WindowState != Normal`. Low impact (cosmetic).

### IN-05: Cross-editor shared mutable IFF tree in the IFF→Datatable hand-off

**File:** `The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.cs:1569-1587` (`OpenFromMutableIff`) ← `FormIffEditor.cs:1592-1605`

**Issue:** `OnSwitchToDatatableViewClick` passes the IFF Editor's live `this.document` into `OpenFromMutableIff`, which keeps it open "so the user can switch back". `DataTableDocument.FromIff` reads leaf payloads via `GetPayloadCopy()`, so the typed model is decoupled from the IFF leaves at parse time (good — no live aliasing of cell bytes). But both editors now hold the same `MutableIffDocument` instance; a structural edit in the IFF Editor (add/remove chunk) after the hand-off would not be reflected in the already-built typed model, and vice versa, with no staleness signal to the user. Acceptable for V1, but worth a "typed view is a snapshot; re-open to refresh" status note.

---

_Reviewed: 2026-05-29_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
