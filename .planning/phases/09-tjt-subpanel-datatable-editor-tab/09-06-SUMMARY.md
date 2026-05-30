---
phase: 09-tjt-subpanel-datatable-editor-tab
plan: 06
subsystem: tjt-datatable-editor
tags: [winforms, datagridview, csv, find-replace, sort, virtualmode, datatable]
status: complete
requires:
  - "09-01 typed DTII primitives (DataTableColumnType.MangleValue/TryCoerceCellValue, MutableDataTableCell CaptureState/RestoreState, DataTableCellValue, DataTableWriter)"
  - "09-04 DatatableEditController + ApplyCsvImport stub (replaced here) + EditCellValue command"
  - "09-05 FormDatatableEditor Save▾ + entry points (CSV/Find/sort wire ON TOP, no save-target touch)"
  - "09-03 ThemedDataGridView non-virtual BindMutable + CellFormatting overlay hooks; DataGridView bind-latency measurement (gates Task 4)"
provides:
  - "CsvCellCoercion framework helper (PlanImport + SerializeCellToCsv) — CI-coverable per-cell CSV coercion"
  - "DataTableCellValue.ToCsvString(ct) extension"
  - "ApplyCsvImportCommand single-transaction CSV-import undo entry (replaces the 09-04 stub)"
  - "DatatableCsvSerializer (TJT-side CSV file I/O + RFC-4180 parser) + FormCsvImportPreviewDialog"
  - "FormDatatableEditor: Find/Replace pane + CSV import/export + column-click view-only sort + DT_Comment frozen-row toggle"
  - "ThemedDataGridView VirtualMode fallback (large-table) + search-match + frozen-comment CellFormatting overlays"
affects:
  - "Plan 09-07 (Tier-4 live-SWG smoke) can exercise the full feature surface end-to-end"
tech-stack:
  added: []
  patterns:
    - "Framework-side CSV coercion extraction (checker B-1; mirrors Phase 8 LooseOverridePath / LivePatchValidator)"
    - "Single-transaction bulk-edit command wrapping N per-cell EditCellValues (reverse-order RestoreState undo)"
    - "DataGridView VirtualMode fallback engaged by row-count threshold (CellValueNeeded/CellValuePushed)"
    - "STA-thread WinForms [Fact] without the Xunit.StaFact package"
key-files:
  created:
    - "UtinniCoreDotNet/Formats/Datatable/CsvCellCoercion.cs"
    - "UtinniCoreDotNet.Tests/FormatsTests/Datatable/CsvCellCoercionTests.cs"
    - "UtinniCoreDotNet.Tests/UITests/DatatableSortViewOnlyTests.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/DatatableCsvSerializer.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormCsvImportPreviewDialog.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormCsvImportPreviewDialog.Designer.cs"
  modified:
    - "UtinniCoreDotNet/Formats/Datatable/DataTableCellValue.cs"
    - "UtinniCoreDotNet/Editing/DatatableEditCommands.cs"
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj"
    - "UtinniCoreDotNet.Tests/FormatsTests/Datatable/DatatableEditControllerTests.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.Designer.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/ThemedDataGridView.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj"
decisions:
  - "Task 4 (VirtualMode) EXECUTED — the 09-03 measurement (265.63 ms cold / 121.93 ms typical, > 100 ms) requires it. Engaged by a row-count threshold (150) so small tables keep the non-virtual per-type-widget path."
  - "ApplyCsvImport factory signature specialized from object to CsvImportPlan (safe — only the 09-04 stub existed; no production caller)."
  - "VirtualMode commit-back routes through a host-installed callback (controller.Apply(EditCellValue)) so VirtualMode edits keep undo/redo parity."
metrics:
  tasks_completed: 4
  tasks_total: 4
  files_created: 6
  files_modified: 8
  duration: "~75 min"
  completed: "2026-05-29"
---

# Phase 9 Plan 06: CSV Import/Export + Find/Replace + Sort + DT_Comment Frozen Toggle Summary

The Phase-9 bulk-edit + productivity layer: CSV/TSV delta-import + export (D-08, byte-exact-on-
untouched), Find/Replace pane (D-07), column-click view-only sort (D-09 with its dual defense), the
DT_Comment frozen-header treatment, and the VirtualMode large-table fallback (Task 4, mandated by the
09-03 measurement). One new framework file (`CsvCellCoercion.cs`, checker B-1 extraction), the
`ApplyCsvImport` stub replaced with the real transaction wrapper, one new TJT serializer + one new
preview modal, and the FormDatatableEditor wiring. Both repos build Debug+Release|x86 green;
475/475 UtinniCoreDotNet.Tests pass.

## Tasks completed

| Task | Name | Repo | Commit | Key files |
|------|------|------|--------|-----------|
| 1 | Framework CsvCellCoercion + ApplyCsvImportCommand + xUnit | Utinni | `a090726` | CsvCellCoercion.cs + DataTableCellValue + DatatableEditCommands + 2 test files + csproj |
| 2 | TJT DatatableCsvSerializer + FormCsvImportPreviewDialog | UtinniPlugins | `3ff92e3` | serializer + dialog + Designer + csproj |
| 3 | FormDatatableEditor wire (Find/Replace + CSV + sort + frozen) + Sort fact | Utinni `c84c655` / UtinniPlugins `2c60048` | sort STA fact (Utinni) + form/Designer/ThemedDataGridView (UtinniPlugins) |
| 4 | VirtualMode fallback (REQUIRED — 09-03 > 100 ms) | UtinniPlugins | `f1bb651` | ThemedDataGridView + FormDatatableEditor |

## Task 4 status: EXECUTED (VirtualMode shipped)

**PRE-FLIGHT grep** against `09-03-SUMMARY.md` matched the parseable measurement line
(`DataGridViewBindLatency_CombatDataTableLike: 265.63 ms … — cold`), so the measurement is PRESENT
(not the ABSENT/deferred case). The recorded value is **265.63 ms cold / 121.93 ms typical** for the
200×30 combat-scale table on the production CellFormatting path — **ABOVE the 100 ms threshold**.
Per the gated decision input and the plan's Task 4 GATE, VirtualMode is REQUIRED — implemented, NOT
skipped.

**Implementation:** `ThemedDataGridView.BindMutable` engages `VirtualMode = true` + `RowCount` (no
per-row materialization — the cost the probe measured) when `doc.Rows.Count >= VirtualRowThreshold`
(150). Cell values serve on demand via `CellValueNeeded`; edits write back via `CellValuePushed` →
a host-installed commit callback (`FormDatatableEditor.CommitVirtualCell` → `controller.Apply(
EditCellValue(...))`) so VirtualMode edits keep undo/redo parity. Small tables keep the non-virtual
path so the per-type editor widgets (CheckBox/ComboBox/NumericUpDown) work unchanged. The search-match
and frozen-comment CellFormatting overlays work in both modes (CellFormatting fires per displayed cell
in VirtualMode). No re-measurement harness was added (RESEARCH Pitfall 7 notes VirtualMode avoids the
per-row materialization the 09-03 probe measured — the cost is structurally removed for large tables).

## Implementation detail

- **CsvCellCoercion.cs (~330 lines):** `PlanImport(target, header, rows)` resolves each CSV header to
  a target column by name; per cell, compares the imported string to the current cell's CSV string
  form (`ToCsvString`) — equal → `Unchanged` (CF-04 byte-exact-on-untouched), different → coerce via
  `TryCoerceCellValue` (success → `EditCellPatch`, failure → `InvalidCellEntry` with a planner-friendly
  reason). DoS caps T-09-27: 100k rows + 16 KB/cell → `CsvParseException`. `SerializeCellToCsv` for
  export symmetry. 13 `[Fact]`s.
- **ApplyCsvImportCommand:** `captured = List<(cell, CellState)>` in apply order; `Do` captures each
  cell's state then sets the patch value; `UndoOp` walks captured state in REVERSE so one Ctrl+Z reverts
  the whole import byte-exact (RestoreState re-attaches original slices). 5 controller `[Fact]`s incl.
  the SC4-via-controller byte-exact-on-unchanged fact.
- **DatatableCsvSerializer (~280 lines; parser ~70 lines):** UTF-8-BOM export (bare-name header +
  optional `#`-type-spec row + a leading `#`-comment documenting DT_HashString int32 lossiness),
  hand-rolled RFC-4180 parser (in-quotes flag, doubled-quote escape, CR/LF row terminators), `LoadAndPlan`
  delegates to `CsvCellCoercion.PlanImport`, `ImportAsync` runs the preview modal + single-transaction apply.
- **FormCsvImportPreviewDialog:** locked D-08 copy (heading `Import {csv} into {tab}?` + body
  `{N} cells will change. {M} cells will stay as original bytes. {K} cells … skipped …`), per-column
  diff grid + red invalid-rows ListView, Import/Cancel; per-call modal lifecycle.

## D-09 view-only-sort defense (dual)

- **Writer-side grep gate (0:0:0):** `grep -c "dataGridView"` on `DatatableSaveTargets.cs`,
  `DataTableWriter.cs`, `MutableDataTableDocument.cs` all return 0 — no UI concept in the save path.
- **`Sort_DoesNotMutateModelOrder` STA `[Fact]` (GREEN):** binds a plain `System.Windows.Forms.DataGridView`
  with a shuffled int column, calls `grid.Sort(...)` for real (asserts the VIEW reordered via
  `Assert.NotEqual`), then asserts `DataTableWriter.Serialize()` bytes are byte-identical
  (`Assert.Equal`). No `TheJawaToolboxDotNet` reference.

## Automated self-checks (all green)

- `dotnet test --filter CsvCellCoercion|ApplyCsvImport|Sort_DoesNotMutateModelOrder`: **18 pass** (≥14).
- Full `UtinniCoreDotNet.Tests`: **475/475 pass** (no regression; one removed NotImplemented stub fact
  replaced by 5 real ApplyCsvImport facts).
- Both repos: **Debug|x86 + Release|x86 green** (only pre-existing CS0108 in Generated/UtinniCore.cs).
- Task 1 gates: csproj CsvCellCoercion=1, NotImplementedException=0, ApplyCsvImportCommand=3.
- Task 2 gates: DatatableCsvSerializer=1, CsvCellCoercion consumed=6, csproj serializer=1, dialog .cs+.Designer=2.
- Task 3 gates: D-09 grep 0:0:0, SortMode.Automatic≥1 (2), DatatableCsvSerializer≥2 (2), "Edit comment rows"≥1, "View order only"≥1 (2).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `m`-lambda name collision in RecomputeMatches**
- **Found during:** Task 3 build (CS0136).
- **Issue:** A `currentMatches.Select(m => …)` lambda collided with the `MutableDataTableDocument m`
  local declared later in the same method scope.
- **Fix:** Renamed the lambda parameter to `mk`. No behavior change.
- **Files:** `FormDatatableEditor.cs`
- **Commit:** `2c60048`

### Plan-shape notes (NOT deviations)

- **ImportAsync signature:** the plan sketched `ImportAsync(..., IWin32Window owner)` calling
  `owner.GetDisplayName()` (no such method on IWin32Window). Shipped as
  `ImportAsync(target, csvPath, controller, owner, csvFileName, tabFileName)` — the host passes the
  display names explicitly (cleaner than reflecting a name off the window). Same observable behavior.
- **VirtualMode strategy:** the plan described an unconditional `VirtualMode = true` swap. Shipped as a
  row-count-threshold engagement (≥150) so small tables retain the per-type editor widgets the
  non-virtual path provides — VirtualMode is the large-table fallback the measurement actually
  motivates (the 200×30 combat fixture is the slow case; a 5-row table binds instantly either way).
- **Find pane close button** named `btnFindClose` (not `btnClose`) to avoid colliding with the
  UtinniForm titlebar close.

## DT_HashString CSV lossiness (item 9)

`ToCsvString`/`SerializeCellToCsv` render the stored int32 hash for DT_HashString columns (the source
string is never persisted — only the int32 reaches disk). The export writes a leading `#`-comment
documenting this; a CSV round-trip CANNOT reconstruct the source string for an already-saved hash cell.
09-07 smoke Step 10 verifies this (it does NOT expect source-string round-trip).

## Self-Check: PASSED

- All 6 created files + the SUMMARY verified present on disk (3 Utinni + 3 UtinniPlugins).
- All task commits verified in history: Utinni `a090726`, `c84c655`; UtinniPlugins `3ff92e3`,
  `2c60048`, `f1bb651`.
- Plan-level acceptance gates #1–#8 all satisfied (18 facts ≥14; both repos Debug+Release green;
  D-09 grep 0:0:0; NotImplementedException 0; csproj entries present; full suites green; Task 4 EXECUTED).
