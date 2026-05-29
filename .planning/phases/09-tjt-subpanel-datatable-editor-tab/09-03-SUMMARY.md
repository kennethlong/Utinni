---
phase: 09-tjt-subpanel-datatable-editor-tab
plan: 03
subsystem: tjt-datatable-editor
tags: [winforms, datagridview, datatable, mef, singleton-form, perf-probe]
status: awaiting-live-host-verification
requires:
  - "09-01 typed DTII primitives (DataTableDocument.FromIff, MutableDataTableDocument, DataTableColumnType, DataTableHashCrc, DataTableCellValue, TryCoerceCellValue)"
  - "Phase 8 IFF primitives (IffReader, MutableIffDocument, OpenSource) + UtinniForm/UtinniButton/UtinniLabel/UtinniNumericUpDown/UtinniContextMenuStrip + Colors"
provides:
  - "FormDatatableEditor singleton UtinniForm host (TJT.UI.Forms) — LoadDocument(DataTableDocument, OpenSource, string); grid-binding commit-back seam for 09-04 controller swap"
  - "ThemedDataGridView TJT-side themed wrapper (TJT.UI.Controls) — BindMutable non-virtual; CellFormatting overlays; Phases 10/11 inherit"
  - "DatatableColumnFactory.Build (DataTableColumnType.Type -> DataGridViewColumn subclass)"
  - "DatatableHashStringEditor floating hash preview (int-vs-source UX); DatatableNumericUpDownEditingControl (IDataGridViewEditingControl)"
  - "SingletonFormClosePolicy framework helper (UtinniCoreDotNet.UI) + xUnit regression guard"
  - "DataGridView bind-latency measurement (Plan 09-06 VirtualMode decision input)"
affects:
  - "Plan 09-04 (controller + structural ops) consumes the form-host API + the commit-back seam"
  - "Plan 09-05 (entry points + save targets) enables the Save menu + hand-offs"
  - "Plan 09-06 (CSV + Find/Replace + sort) inherits the bind-latency decision -> VirtualMode fallback recommended"
tech-stack:
  added: []
  patterns:
    - "Singleton-form hide-not-dispose decision extracted to a CI-coverable framework helper (no cross-repo TJT.dll reference)"
    - "DataGridView non-virtual BindMutable + CellFormatting overlay (production path)"
    - "STA-thread WinForms probe without adding the Xunit.StaFact package"
key-files:
  created:
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/ThemedDataGridView.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/DatatableColumnFactory.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/DatatableHashStringEditor.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/DatatableNumericUpDownEditingControl.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.Designer.cs"
    - "UtinniCoreDotNet/UI/SingletonFormClosePolicy.cs"
    - "UtinniCoreDotNet.Tests/UITests/SingletonFormClosePolicyTests.cs"
    - "UtinniCoreDotNet.Tests/PerfProbes/DataGridViewBindLatencyProbeTests.cs"
  modified:
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj"
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj"
decisions:
  - "Bind-latency straddles the 100 ms threshold (cold ~265 ms, warm ~90-122 ms) -> Plan 09-06 SHOULD include a VirtualMode fallback subtask (conservative on the cold first-open path)."
  - "STA requirement met with a created+joined STA thread instead of adding the Xunit.StaFact package (Rule 3 blocking-fix, no new dependency)."
metrics:
  tasks_completed: 3
  tasks_total: 4
  files_created: 9
  files_modified: 3
  duration: "~95 min"
  completed: "2026-05-29"
---

# Phase 9 Plan 03: FormDatatableEditor Host + Grid + Per-type Widgets Summary

The TJT-side host + grid + per-type cell widgets for the Datatable Editor: a singleton
`FormDatatableEditor` (`UtinniForm`, 1200x760, min 900x560) that loads a `.tab`/`.iff` from disk,
parses it through the Plan 09-01 typed layer, and binds it non-virtual to a new `ThemedDataGridView`
with per-type cell widgets — plus the singleton hide-not-dispose pattern extracted to a CI-coverable
framework helper and the DataGridView bind-latency probe that gates Plan 09-06's VirtualMode decision.

## DataGridView bind-latency measurement (MANDATORY — gates Plan 09-06)

Measured against `BuildV1CombatDataTableLike()` (200 rows × 30 cols) on the **production path** (a
plain `System.Windows.Forms.DataGridView` WITH a representative `CellFormatting` overlay handler
attached — iter-2 item 2), via `DataGridViewBindLatencyProbeTests`:

- DataGridViewBindLatency_CombatDataTableLike: 265.63 ms (200 rows × 30 cols, CellFormatting overlay attached)  — cold (first run, JIT-cold)
- DataGridViewBindLatency_CombatDataTableLike: 121.93 ms (200 rows × 30 cols, CellFormatting overlay attached)  — typical
- DataGridViewBindLatency_CombatDataTableLike: 95.06 ms (200 rows × 30 cols, CellFormatting overlay attached)   — warm
- DataGridViewBindLatency_CombatDataTableLike: 89.59 ms (200 rows × 30 cols, CellFormatting overlay attached)   — warm

**Decision branch (RESEARCH Pitfall 7 + Assumption A5): the measurement STRADDLES the 100 ms
threshold.** The cold first-open bind (the realistic user-facing cost on the first datatable opened
in a session) measured ~265 ms — well above 100 ms; warm/JIT-hot binds hover 89-122 ms, straddling
the line. **Recommendation for Plan 09-06: INCLUDE the VirtualMode-fallback subtask.** The cold-bind
path exceeds the threshold and the warm path is marginal, so the conservative reading is that V1
benefits from a VirtualMode fallback for large combat-scale tables. This is NOT the iter-1
"deferral-signal" case — the measurement is present and parseable in this SUMMARY (the deferral
signal fires only when the measurement is ABSENT).

## Tasks completed

| Task | Name | Repo | Commit | Key files |
|------|------|------|--------|-----------|
| 1 | ThemedDataGridView + DatatableColumnFactory + DatatableHashStringEditor + DatatableNumericUpDownEditingControl | UtinniPlugins | `697a30c` | 4 controls + csproj |
| 2 | SingletonFormClosePolicy framework helper + xUnit guard | Utinni | `6a40e05` | helper + csproj + test |
| 2 | FormDatatableEditor + Designer + Plugin.cs (paired) | UtinniPlugins | `ef0a0c8` | form + Designer + Plugin.cs + csproj |
| 3 | DataGridView bind-latency probe | Utinni | `84a24c6` | perf probe |
| 4 | Live-host maintainer visual sanity check | — | AWAITING | (checkpoint) |

## Automated self-checks (all green)

- TJT `TheJawaToolboxDotNet.csproj` Debug|x86 via VS2026 MSBuild: **build succeeded, 0 warnings** in the new files.
- `UtinniCoreDotNet.csproj` Debug|x86: build succeeded (only pre-existing CS0108 warnings in `Generated/UtinniCore.cs`).
- `SingletonFormClosePolicyTests`: **6 passing** (1 Fact UserClosing->hide + 5 Theory shutdown reasons->dispose); zero TJT reference in the test csproj.
- `DataGridViewBindLatencyProbeTests`: **1 passing**; emits the `DataGridViewBindLatency_CombatDataTableLike:` measurement prefix.
- Full `UtinniCoreDotNet.Tests`: **411/411 pass** (404 baseline + 6 close-policy + 1 perf probe — no regression of 09-01/02).
- Literal gates: `grep -c Color.FromArgb ThemedDataGridView.cs` = 0; `grep -c NotImplementedException FormDatatableEditor.cs` = 0; csproj `<Compile Include>` entries present (ripgrep count 1 each); CF-09 Controls.Add order = gridSurface, pnlStatus, pnlFindReplace, toolbar.

## LOC added per file (rough)

| File | LOC |
|------|-----|
| ThemedDataGridView.cs | 248 |
| DatatableColumnFactory.cs | 99 |
| DatatableHashStringEditor.cs | 142 |
| DatatableNumericUpDownEditingControl.cs | 149 |
| FormDatatableEditor.cs | 596 |
| FormDatatableEditor.Designer.cs | 371 |
| SingletonFormClosePolicy.cs | 63 |
| SingletonFormClosePolicyTests.cs | 59 |
| DataGridViewBindLatencyProbeTests.cs | 209 |

## Grid-binding commit-back seam (for Plan 09-04)

The commit-back path lives in `FormDatatableEditor.CommitCell(rowIndex, columnIndex)`, invoked by
`OnCellEndEdit` (text + NumericUpDown columns) and `OnCellValueChanged` (CheckBox + ComboBox
commit-immediately columns). In Plan 09-03 (no controller yet) it sets `cell.Value` DIRECTLY via the
Plan 09-01 setter:

- DT_Bool -> `DataTableCellValue.FromInt(isChecked ? 1 : 0)`
- DT_HashString -> `DataTableCellValue.FromInt(unchecked((int)DataTableHashCrc.Compute(sourceText)))`, then the stored int32 is written back into the grid display (item 5: display the int32, source not persisted)
- everything else -> `DataTableColumnType.TryCoerceCellValue(raw, out coerced)` (Enum label->int, numeric coerce, string passthrough); a coercion failure surfaces the red status copy and does NOT commit

**Plan 09-04 swap point:** replace the `cell.Value = ...` assignments in `CommitCell` with
`controller.Apply(DatatableEditCommands.EditCellValue(cell, newValue))` (CaptureState/RestoreState
API from 09-01/09-04). The seam is isolated to `CommitCell` so the swap touches one method.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] STA requirement met without adding the Xunit.StaFact package**
- **Found during:** Task 3
- **Issue:** The plan specified a `[StaFact]`, but no StaFact infrastructure exists in the test project and the repo has no `Xunit.StaFact` package. Adding a new external package is excluded from auto-fix (package-install exclusion) and would also be unnecessary for a single probe.
- **Fix:** Implemented the probe as a plain `[Fact]` that runs the WinForms `DataGridView` work on a dedicated `Thread` with `SetApartmentState(ApartmentState.STA)`, started and joined inline. Same observable behavior (WinForms on an STA thread), zero new packages. Surfaced the measurement via both `Trace.WriteLine` (the plan's stdout-prefix contract) AND `ITestOutputHelper.WriteLine` (the console logger swallows Trace).
- **Files:** `UtinniCoreDotNet.Tests/PerfProbes/DataGridViewBindLatencyProbeTests.cs`
- **Commit:** `84a24c6`

**2. [Rule 3 - Blocking] Reworded a doc-comment ARGB-literal mention to satisfy the literal gate**
- **Found during:** Task 1
- **Issue:** A `<remarks>` line in `ThemedDataGridView.cs` literally contained `Color.FromArgb` (saying "NO raw `Color.FromArgb` literals"), tripping the naive `grep -c "Color.FromArgb" == 0` acceptance gate as a false positive.
- **Fix:** Reworded the comment to "NO raw ARGB color literals" (grep-gate hygiene memory). No behavior change; the actual constructor uses only `Colors.*()` accessors.
- **Files:** `ThemedDataGridView.cs`
- **Commit:** `697a30c`

### Plan-shape notes (NOT deviations)

- `FormDatatableEditor` ports the FormIffEditor member shape but its grid surface is a single
  `ThemedDataGridView` (Dock.Fill) — NO `SplitContainer` (UI-SPEC default V1 layout uses no splitter;
  the hash preview floats inline). The `splitterDistance` ini key is still created (UI-SPEC § Host
  Placement settings row) for forward compatibility but is unused in 09-03.
- The empty-state `lblEmptyState` (UI-SPEC § States "Empty (no document)") docks Fill on top of the
  grid and is hidden on the first successful `LoadDocument`.
- `lblCounters` renders `{rows} rows · {cols} cols` clean / adds `· {dirty} dirty` when dirty
  (needs-review count is hard-zero in 09-03 — the type-change cascade ships in 09-04).

## SingletonFormClosePolicy test count + cross-repo confirmation

- 6 passing facts (1 `[Fact]` + 5 `[Theory]` cases) — references ONLY `SingletonFormClosePolicy` +
  `System.Windows.Forms.CloseReason`; NO TJT type; NO WinForms form instantiated; NO STA requirement.
- **Confirmed: zero TJT reference added to `UtinniCoreDotNet.Tests.csproj`** (no `TheJawaToolboxDotNet.dll`
  HintPath; the test compiles against the framework helper only — iter-2 item 3 cross-repo placement holds).

## Plugin.cs registration block diff (3-7 lines, paired commit landed cleanly)

A 7-line `try { forms.Add(new FormDatatableEditor(this)); } catch (Exception ex) { Log.Info(...); }`
block inserted after the FormIffEditor registration — mirrors the Phase 8 pattern verbatim. SPI NOT
widened (`GetSubPanels()` untouched, CON-M-01/02). Cross-repo paired commit confirmed:
Utinni `6a40e05` + UtinniPlugins `ef0a0c8`.

## Designer.cs Controls.Add order (CF-09 compliance)

CF-09 add-order verified by ripgrep — `gridSurface` (Fill, front-most) FIRST, then `pnlStatus`
(Bottom), then `pnlFindReplace` (Top, hidden), then `toolbar` (Top) LAST. No reorder issues surfaced;
the `winforms-dockfill-zorder` memory was applied verbatim (Fill added first so it is not starved).

## Checkpoint — AWAITING live-host maintainer verification (Task 4)

Plan 09-03 ends on a deliberately-scoped live-host (editor host, NO live SWG required) maintainer
visual sanity check. Do NOT mark the plan/ROADMAP complete until that check passes. See the
structured checkpoint report returned to the orchestrator.

## Self-Check: PASSED

- All 9 created files + the SUMMARY verified present on disk.
- All 4 task commits verified in history: Utinni `6a40e05` (close-policy + guard), `84a24c6`
  (perf probe); UtinniPlugins `697a30c` (controls), `ef0a0c8` (form host).
- iter-1 fix #6 acceptance gate satisfied: the SUMMARY contains a parseable
  `DataGridViewBindLatency ... NNN.NN ms` measurement line.
