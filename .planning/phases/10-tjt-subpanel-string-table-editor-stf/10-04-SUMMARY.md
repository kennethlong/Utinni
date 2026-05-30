---
phase: 10-tjt-subpanel-string-table-editor-stf
plan: 04
subsystem: tjt-editor + framework-csv
tags: [stf, csv, po, find-replace, filter, view-only-sort, d-03a, d-03b, d-03c, d-03d, f8, sc4]
requires: ["10-01", "10-03"]
provides:
  - "StringTableCsvCoercion.PlanImport — framework per-entry CSV diff planner (Changes/Unchanged/Added/Invalid, F8 invalid-key guard) + SerializeRowToCsv"
  - "StringTablePoExport.ToPo — hand-rolled PO/gettext export (msgid=key / msgstr=text)"
  - "StringTableEditCommands.ApplyCsvImport — one-undo-entry CSV transaction (N EditText + M AddEntry)"
  - "TJT StringTableCsvSerializer (UTF-8 BOM export + RFC-4180 parser) + FormStfCsvImportPreviewDialog"
  - "FormStringTableEditor Find/Replace + live filter (Ctrl+L) + view-only sort + CSV/PO wiring"
affects:
  - "10-05 (Save▾ + reload + TRE hand-off rebase on this committed FormStringTableEditor.cs)"
tech-stack:
  added: []
  patterns:
    - "Phase 9 CsvCellCoercion / ApplyCsvImport / DatatableCsvSerializer / FormCsvImportPreviewDialog analogs ported to the flat key/text format"
    - "by-NAME diff (not by-row-index) keyed on the .stf lookup key; exclude-self ValidateName for in-place updates"
    - "STA plain-DataGridView sort+filter view-only fact (Phase 9 cross-repo UI-test placement)"
key-files:
  created:
    - UtinniCoreDotNet/Formats/StringTable/StringTableCsvCoercion.cs
    - UtinniCoreDotNet/Formats/StringTable/StringTablePoExport.cs
    - UtinniCoreDotNet.Tests/FormatsTests/StringTable/StringTableCsvCoercionTests.cs
    - UtinniCoreDotNet.Tests/FormatsTests/StringTable/StringTablePoExportTests.cs
    - UtinniCoreDotNet.Tests/UITests/StringTableSortViewOnlyTests.cs
    - "The Jawa Toolbox/TheJawaToolboxDotNet/Saving/StringTableCsvSerializer.cs (UtinniPlugins repo)"
    - "The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormStfCsvImportPreviewDialog.cs (+ .Designer.cs) (UtinniPlugins repo)"
  modified:
    - UtinniCoreDotNet/Editing/StringTableEditCommands.cs
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj
    - UtinniCoreDotNet.Tests/FormatsTests/StringTable/StringTableEditControllerTests.cs
    - "The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormStringTableEditor.cs (+ .Designer.cs) (UtinniPlugins repo)"
    - "The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj (UtinniPlugins repo)"
key-decisions:
  - "Filter chord = Ctrl+L (confirmed per 10-UI-SPEC Open-Decision 3; non-colliding with the native grid keys + the inherited Ctrl+F/Ctrl+H)."
  - "PO export SHIPPED (not cut-to-V2). It is ~40 framework lines (StringTablePoExport) with no context/dependency constraint, so the plan's 'ships unless explicitly cut' default applied — the developer did not request cutting it."
  - "The CSV diff is keyed by NAME (the .stf lookup key), not by row index like the Phase 9 datatable. ValidateName is called with exclude=the matched entry so an in-place text UPDATE to an existing key is NOT flagged a duplicate of itself; a genuinely new key validates against all entries (F8). Duplicate-CSV-row is caught with a seen-keys set. There is no engine fixup transform exposed (ValidateName rejects rather than fixes), so 'duplicate-after-fixup' collapses into the duplicate-CSV-row case — covered by the duplicate-CSV-row fact."
  - "Find operates over the VISIBLE grid rows × {Key, Text} keyed by grid-row index; navigation + replace-commit key off the row.Tag entry, so a view-only sort never corrupts a write. The ThemedDataGridView's own search-match overlay is null-guarded on boundDocument (null here, raw bind), so the form paints matches via its own CellFormatting."
  - "Filter is view-only: it sets Row.Visible (clearing CurrentCell first to avoid the WinForms hide-current-cell throw) and never touches the model. The STA framework fact proves a live grid sort + a Row.Visible filter leave StringTableWriter.Serialize byte-identical AND the Entries order + clean state untouched (D-03c)."
  - "ApplyCsvImport is ONE undo entry (N text edits + M adds); its Do() throws InvalidOperationException if handed a plan with HasBlockingErrors (defensive F8 guard — the preview modal is the primary gate that disables Import)."
requirements-completed: [PROD-W1-STF]
deviations:
  - "[Rule 3] The TJT WinForms wiring (serializer file I/O, preview modal, Find/Replace/Filter/CSV/PO Form handlers) is maintainer-smoke (10-06); the unit-testable logic (CSV diff + F8 guard, PO string generation, the single-transaction undo, the view-only on-disk-order guarantee) is all framework-side + xUnit-covered, per the plan's pre-authorized deviation."
  - "PreservationAudit suite named in the verification list does not exist as a project in this repo — nothing to run; the full UtinniCoreDotNet.Tests suite (576) is green."
duration: ~1 session
completed: 2026-05-30
---

# Phase 10 Plan 04: Bulk / Translation Features (Find/Replace + CSV/PO + Sort/Filter) Summary

Ships the Phase 10 translation-productivity surface: Find/Replace (key + text, regex opt-in, D-03a),
CSV/TSV delta-import + export (D-03b, byte-exact-on-untouched + the F8 invalid-key guard), view-only
column sort + the NEW live filter (D-03c), and PO/gettext export (D-03d). The unit-testable logic lives
framework-side; the WinForms wiring is maintainer-smoke (10-06).

## What shipped

- **`StringTableCsvCoercion.PlanImport`** — by-name per-entry diff into Changes / Unchanged / Added /
  Invalid; F8 validates every key via `ValidateName` (exclude-self for updates) + duplicate-CSV-row +
  DoS caps (100k rows / 64 KB cell → `StringTableCsvParseException`). `SerializeRowToCsv` centralizes the
  RFC-4180 escape framework-side.
- **`StringTablePoExport.ToPo`** — `msgid`/`msgstr` per named entry, gettext-escaped, UTF-8 (João survives).
- **`StringTableEditCommands.ApplyCsvImport`** — one-undo-entry transaction (N EditText + M AddEntry);
  Do() refuses a `HasBlockingErrors` plan (F8 defensive guard).
- **TJT `StringTableCsvSerializer`** — UTF-8-BOM `key,text` export + a hand-rolled RFC-4180 parser; PO
  export passthrough.
- **`FormStfCsvImportPreviewDialog`** — locked D-03b body + changed/added grid + red invalid-key list;
  Import DISABLED when `HasBlockingErrors` (F8).
- **`FormStringTableEditor` wiring** — Find/Replace pane (key+text, regex opt-in, key-replace
  re-validates, F3/Shift+F3, Esc), live filter row (Ctrl+L, 250 ms debounce, Row.Visible, `{shown}/{total}`),
  view-only column sort with the LOCKED tooltip `View order only — save serializes strings by id and
  names alphabetically.`, and the Import/Export-CSV + Export-PO buttons. The previously-disabled
  Find/Replace/Filter/Import/Export buttons are enabled on `LoadDocument`.

## Verification

- **Framework facts**: `StringTableCsvCoercionTests` (13) + `StringTablePoExportTests` (5) +
  `StringTableSortViewOnlyTests` (1 STA) + `ApplyCsvImport` controller facts (4) = **28 green** (≥18 required).
- **Full `UtinniCoreDotNet.Tests`**: **576 passed / 0 failed / 0 skipped**. (A first run flagged one
  `NativeCallbacksHandleTests` dispatch-ordering test; it passed in isolation and on the clean re-run —
  a pre-existing non-deterministic flake unrelated to this plan.)
- **TJT MSBuild Debug|x86 + Release|x86**: both green. **Utinni Debug + Release**: green.
- **Grep gates**: serializer→`StringTableCsvCoercion`=7; csproj serializer=1; csproj preview dialog=2;
  form `View order only`=2; `ApplyCsvImport` in `StringTableEditCommands.cs` present.
- **Cross-repo paired commit landed** (see below).

## Filter chord

**Ctrl+L** (confirmed). Find=Ctrl+F, Replace=Ctrl+H are inherited/locked; Ctrl+L does not collide with
the native `DataGridView` keys.

## PO export

**Shipped** (not cut-to-V2). `StringTablePoExport.ToPo` is ~40 framework lines with no
context/dependency constraint, so the plan's "ships unless explicitly cut" default applied.

## CSV parser line count

`StringTableCsvSerializer.ParseCsv` (the RFC-4180 state machine) is **~62 lines** — under the < 100-line
budget; the escape half lives framework-side in `StringTableCsvCoercion.SerializeRowToCsv`.

## Self-Check: PASSED
