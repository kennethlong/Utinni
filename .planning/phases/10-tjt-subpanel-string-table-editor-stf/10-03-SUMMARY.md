---
phase: 10-tjt-subpanel-string-table-editor-stf
plan: 03
subsystem: tjt-editor
tags: [stf, winforms, editor-shell, two-column-grid, t4, name-validation, sc4, cf-04, cf-09]
requires: ["10-01"]
provides:
  - "FormStringTableEditor (UtinniForm singleton) — TJT String-table Editor shell + two-column grid + T4 mutation"
  - "Plugin.cs GetForms() registration (MEF try/catch isolation block)"
  - "UtinniCoreDotNet.Tests StringTableNameValidationTests (8 facts on the ValidateName predicate)"
affects:
  - "10-04 (Find/Replace + Filter + CSV/PO fill the disabled toolbar buttons + hidden panes shipped here)"
  - "10-05 (Save▾ targets + reload dispatch + TRE Browser hand-off wire the disabled stubs shipped here)"
tech-stack:
  added: []
  patterns:
    - "FormDatatableEditor host shape ported VERBATIM with a strictly-simpler two-column surface"
    - "ThemedDataGridView bound RAW (own columns + rows, row.Tag = entry reference) — NOT via datatable-specific BindMutable"
    - "Key cell-validator DELEGATES to MutableStringTableDocument.ValidateName (F3c — single source of truth)"
    - "Full RebindGrid on every structural T4 + undo/redo (F12 no model/grid desync), done OUTSIDE CellEndEdit"
key-files:
  created:
    - "The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormStringTableEditor.cs (UtinniPlugins repo)"
    - "The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormStringTableEditor.Designer.cs (UtinniPlugins repo)"
    - UtinniCoreDotNet.Tests/FormatsTests/StringTable/StringTableNameValidationTests.cs
  modified:
    - "The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs (UtinniPlugins repo)"
    - "The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj (UtinniPlugins repo)"
key-decisions:
  - "AutoSizeRowsMode = DisplayedCells (NOT AllCells). With WrapMode=True on the Text column this gives multi-line legibility for the visible rows while only measuring on-screen rows — AllCells would re-measure every row of a large .stf on each change (a perf cliff with no VirtualMode fallback in this plan). DisplayedCells is the performant wrap mode."
  - "OS/IME smart-substitution disabled in EditingControlShowing by setting the editing TextBox AutoCompleteMode=None + AutoCompleteSource=None + CharacterCasing=Normal, and DELIBERATELY leaving ImeMode at its default (NOT ImeMode.Off) so legitimate CJK/accented composition still works. A plain DataGridViewTextBoxEditingControl performs no smart-quote/ellipsis/dash rewriting by default; the verbatim-bytes guarantee itself is owned by the 10-01 model setter + writer (no unfubar) and proven by the 10-01/10-02 byte-exact gates — the UI's job is only to not re-introduce a substitution."
  - "Grid bound RAW (own 3 columns + one row per entry, row.Tag = the MutableStringTableEntry reference) rather than via the datatable-specific ThemedDataGridView.BindMutable. The grid's own CellFormatting/CellValueNeeded handlers all null-guard boundDocument (which stays null), so they no-op; dirty/added cell overlays are painted by the form's own CellFormatting keyed off the entry reference."
  - "Name validation runs in CellValidating with e.Cancel=true on reject (blocks the commit so the controller never sees a bad name); the red cell BackColor + red status copy come straight from the ValidateName Reason. Esc reverts to the current key. The Text column has NO validation seam."
  - "Undo/redo + Add/Remove call a full RebindGrid AFTER the controller op (F12), executed outside any CellEndEdit (RebindGrid CancelEdits first), so the visible grid always exactly mirrors the model — an undone Add removes its row, an undone Remove re-inserts it, an undone rename/edit restores the cell text."
  - "Deferred-feature surfaces shipped DISABLED-with-tooltip (NOT throwing): Save▾ (5 stub items), Import/Export CSV, Export PO, Find, Replace, Filter, Reload in client. The hidden pnlFindReplace (32px) + pnlFilter (28px) panels are present in the Designer in the locked CF-09 add-order so 10-04 only has to fill + toggle them."
requirements-completed: [PROD-W1-STF]
deviations:
  - "[Rule 3] The WinForms form is not framework-test-coverable (Phase 8/9 precedent: no TJT-side test project). The only framework-side test is StringTableNameValidationTests over the pure ValidateName predicate the cell-validator delegates to; full form behaviour is the 10-06 maintainer smoke. (Pre-authorized by the plan's DEVIATION assumption.)"
  - "PreservationAudit suite named in the plan's verification list does not exist as a project in this repo — nothing to run; the applicable UtinniCoreDotNet.Tests suite (553) + the new 8 facts are green."
duration: ~1 session
completed: 2026-05-30
---

# Phase 10 Plan 03: TJT String-table Editor Shell + Two-Column Grid + T4 Summary

Ships the TJT-side editor shell for Phase 10: `FormStringTableEditor`, a resizable `UtinniForm`
singleton (default 1000×720, min 760×520, title `String-table Editor` with a leading ● dirty marker),
a sibling of `FormDatatableEditor` with a STRICTLY SIMPLER two-column (Key + Text) `ThemedDataGridView`
surface. Delivers SC1 (subpanel loads in TJT via `GetForms()`) and the editable half of SC2 (open a
`.stf`, view entries with keys, edit text, T4 add/remove/rename through the undoable controller), plus
the SC4 Unicode-fidelity non-behavior on the Text column.

## What shipped

- **`FormStringTableEditor` + hand-written Designer** — `Open…` file picker (`*.stf`) →
  `StringTableDocument.FromBytes` → `LoadDocument(mutable, OpenSource.LooseFile, name)`; two editable
  `DataGridViewTextBoxColumn`s (Key FillWeight 35, Text FillWeight 65 `WrapMode=True`) + one optional
  read-only hidden `id` column (recessed `PrimaryShadow`/`FontDisabled`) toggled by `Show id`
  (persisted to `[StringTableEditor] showIdColumn`).
- **Four T4 verbs through `StringTableEditController`** (CF-04, editor-local — never the scene
  `UndoRedoManager`): edit text (commit-on-`CellEndEdit`), rename key (validated), `Add entry`
  (auto-`{NNN}_default`, new Key cell drops straight into edit mode), `Remove entry` (toolbar + `Delete`
  on a non-editing row). `Ctrl+Z`/`Ctrl+Y` in `ProcessCmdKey` caught at the form before the grid.
- **Key name-validation** delegated to `MutableStringTableDocument.ValidateName` (F3c) — invalid →
  red cell + red status (the predicate's `Reason`) + reverted edit; the Text column commits UTF-16LE
  verbatim with no validation/transformation (SC4).
- **Dirty visuals** (Phase 8/9 idiom): edited/added cell `ForeColor = Colors.Secondary()`, row-header
  `●`/`＋` glyph, title `●`, `lblDirty` `Unsaved changes`, counters `{entries} entries · {dirty} dirty`.
  Reload badge shows the LOCKED CF-05 `Reloads on next scene change.` from the moment a file loads.
- **`Plugin.cs`** GetForms() registration in a new try/catch isolation block (mirrors the Phase 7/8/9
  entries); **csproj** Compile entries for the Form (`<SubType>Form</SubType>`) + Designer
  (`<DependentUpon>`).
- **`StringTableNameValidationTests`** — 8 framework facts: valid accept, valid charset accept, empty
  reject, leading-digit reject, uppercase-first reject, non-ASCII reject, duplicate reject,
  exclude-self-on-rename accept.

## Verification

- **TJT MSBuild Debug|x86 + Release|x86**: both green.
- **`StringTableNameValidationTests`**: 8 passed / 0 failed (Release, --no-build).
- **Full `UtinniCoreDotNet.Tests`**: 553 passed / 0 failed / 0 skipped — no regression.
- **Plan grep gates**: csproj `FormStringTableEditor.cs` Compile = 1; `Plugin.cs` `FormStringTableEditor`
  = 2 (≥1); form `ValidateName` = 4 (≥1); the Text column has no validation/transformation hook.
- **Cross-repo paired commit landed** (Utinni: the framework test; UtinniPlugins: the form + Designer +
  Plugin.cs + csproj) — see the commit confirmation below.

## Unicode-fidelity mechanism (SC4 non-behavior)

`OnEditingControlShowing` sets the editing `TextBox` `AutoCompleteMode = None`,
`AutoCompleteSource = None`, `CharacterCasing = Normal`, and leaves `ImeMode` at default so CJK/accented
composition still works. No smart-quote / ellipsis / dash / typo "fix" is applied; the byte-exact
guarantee is the 10-01 model + writer (proven by the 10-01/10-02 gates), and the UI simply does not
re-introduce a substitution.

## AutoSizeRowsMode

`DisplayedCells` (paired with `WrapMode=True` on the Text column) — multi-line legibility for the
visible rows without the all-rows re-measure cost of `AllCells` (this plan ships no VirtualMode
fallback, so a large `.stf` would feel a per-change cliff under `AllCells`).

## Self-Check: PASSED
