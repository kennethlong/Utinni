---
phase: 09-tjt-subpanel-datatable-editor-tab
verified: 2026-05-29T00:00:00Z
status: human_needed
score: 4/4 roadmap success criteria structurally verified (code + automation complete; SC1/SC3 live-ACK pending)
overrides_applied: 0
human_verification:
  - test: "Plan 09-03 editor-host open-from-disk smoke (Part A, A1–A7) — NO live SWG required"
    expected: "Datatable Editor opens 1200x760 dark-themed; a synthesized .tab (DatatableFixtures.BuildV1AllTypes) loads; per-type widgets render (int=numeric, bool=checkbox, enum=dropdown, DT_HashString=int32 + floating Consolas-9pt hash preview); cell edit commits on CellEndEdit; close-then-reopen throws NO ObjectDisposedException (hide-not-dispose); reload badge shows locked CF-05 copy; no-document state honest-disables Save/Add/Import/Find with tooltips"
    why_human: "WinForms rendering, singleton MEF lifecycle, and per-type cell-widget interaction cannot be exercised without a running editor host process; grep confirms the code paths exist but not their runtime behavior"
  - test: "Plan 09-07 Tier-4 live-SWG smoke (Part B, B1–B13) against an injected SWGEmu/Restoration client — choose Option A/B/C disposition in 09-07-SMOKE-LOG.md"
    expected: "SC1: Datatable Editor subpanel loads inside TJT under live MEF (B1). All three entry points open a .tab (file picker B2, TRE Browser hand-off B3, IFF Editor Switch-to-typed-view B4). Edit cell + add row + change column type with cascade resolution + Save▾ R-04 gating (B5/B6). Save modes write correct bytes (B8). SC3: TJT chat-command scene change propagates the edit to the live client (B9). CSV import byte-exact-on-untouched (B10), Find/Replace (B11), view-only sort (B12), singleton re-open (B13)"
    why_human: "Requires an injected live SWG client + scene-change repro path (memory project_scene_change_via_tjt); SC3 (client picks up edit on reload path) is unobservable without the live session. Deferred-but-acceptable for V1 per Phase 8 P05/P06/P07 precedent — code + ~170 automated facts are complete and green"
deferred: []
---

# Phase 9: TJT subpanel — Datatable Editor (`.tab`) Verification Report

**Phase Goal:** View and edit `.tab` datatables (tabular client data). Replaces SOE-era `SwgDataTableTool`. Layers on Phase 8's IFF read/write where `.tab` is IFF-backed. Per DEC-C4, ships as a TJT subpanel (`IEditorPlugin`).
**Verified:** 2026-05-29
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria + merged PLAN must-haves)

| # | Truth (roadmap SC) | Status | Evidence |
|---|---|---|---|
| SC1 | Datatable Editor subpanel loads inside TJT in the editor host against a live SWG client | ⚠ STRUCTURALLY VERIFIED — live ACK pending | `FormDatatableEditor` registered in `Plugin.cs GetForms()` (try/catch isolation, 2 refs); both TJT solutions build Debug+Release x86 (smoke-log P0.3/P0.4); runtime MEF-load is the human Part-A/B1 check |
| SC2 | User can open a `.tab`, view rows/columns, edit cell values, and save back | ✓ VERIFIED | `FormDatatableEditor` (1773 LoC) wires `LoadDocument`→`ThemedDataGridView.BindMutable`, per-type cell widgets via `DatatableColumnFactory`, Save▾ → `DatatableSaveTargets` (4 modes; mode-3 disabled per CF-03). 138 framework + 16 CLI + 17 saving/sort/singleton facts pass |
| SC3 | Live SWG client picks up the edit on the relevant reload path | ⚠ STRUCTURALLY VERIFIED — live ACK pending | `ClientReloadDispatcher.Dispatch(savedPath,"DTII")` wired on save success → `PendingNextSceneChange` (tier-(b), CF-05); `DatatableReloadRoutingTests` green. In-game propagation is human Part-B9 |
| SC4 | Edits preserve schema without silent corruption | ✓ VERIFIED | CF-04 hybrid DOM: per-cell `originalSlice` + `CaptureState/RestoreState` undo; `DataTableWriter` composes `IffWriter.Write` (no new framing); `roundtrip-tab` CLI golden asserts byte-exact-on-untouched-cells (16 facts pass); DT_Comment column preserved in COLS/TYPE with zero-byte ROWS |

**Score:** SC2 + SC4 fully VERIFIED in code & automation; SC1 + SC3 structurally complete, awaiting maintainer live session (the deferred-but-acceptable residual).

### Required Artifacts (all 3 levels: exists / substantive / wired)

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `UtinniCoreDotNet/Formats/Datatable/*.cs` (11 files) | Typed DTII model + writer + CRC + CSV coercion | ✓ VERIFIED | All 11 present, 2600+ LoC; `DataTableWriter` → `IffWriter.Write` (3 refs); `FromIff` → `MutableIffDocument` (4 refs); no debt markers |
| `UtinniCoreDotNet/Editing/{IDatatableEditCommand,DatatableEditController,DatatableEditCommands}.cs` | Controller + 11 T4 commands + cascade | ✓ VERIFIED | 965 LoC; `MangleValue`/`PendingCascadeContext`/`MarkSaved`/`NeedsReviewCount` wired; `CaptureState/RestoreState` undo (10 refs) |
| `UtinniCoreDotNet/UI/SingletonFormClosePolicy.cs` | Hide-not-dispose framework helper | ✓ VERIFIED | 63 LoC; `ShouldHideInsteadOfDispose`; `SingletonFormClosePolicyTests` green |
| `Utinni.Cli/Commands/RoundtripTabCommand.cs` | `roundtrip-tab` verb (SC4 CLI gate) | ✓ VERIFIED | 375 LoC; registered in `Program.cs`; appears in `--help`; 16 golden facts pass |
| `TJT/UI/Controls/{ThemedDataGridView,DatatableColumnFactory,DatatableHashStringEditor,DatatableNumericUpDownEditingControl}.cs` | Themed grid + per-type widgets | ✓ VERIFIED | All 4 present (787 LoC); `BindMutable` non-virtual bind; hash preview via `DataTableHashCrc.Compute` |
| `TJT/UI/Forms/FormDatatableEditor(.Designer).cs` | Singleton editor host | ✓ VERIFIED | 2308 LoC; controller + save targets + CSV + reload + both hand-offs + Find/Replace + sort + comment-row toggle all wired (37 + 22 matched refs) |
| `TJT/UI/Forms/{FormAddColumnDialog,FormTypeChangeCascadeDialog,FormCsvImportPreviewDialog}(.Designer).cs` | Modals | ✓ VERIFIED | All 6 present + Designers |
| `TJT/Saving/{DatatableSaveTargets,DatatableCsvSerializer}.cs` | Save composition + CSV I/O | ✓ VERIFIED | 398 LoC; `DatatableSaveTargets` composes Phase 8 `IffSaveTargets`/`TreRepackSaveTarget` (18 refs) |
| Test suites (framework + CLI + saving + UI probes) | ~170 facts | ✓ VERIFIED | 34+19+6+14+39+12 framework, 16 CLI, 17 saving/sort/singleton — all green |

### Key Link Verification

| From | To | Status | Details |
|---|---|---|---|
| `DataTableWriter.Serialize` | `IffWriter.Write` (Phase 8) | ✓ WIRED | Composes Phase 8 primitive; no new chunk framing |
| `DataTableDocument.FromIff` | `MutableIffDocument` (Phase 8) | ✓ WIRED | Typed-on-raw composition, back-ref retained |
| `MutableDataTableCell.RestoreState` | byte-exact undo invariant | ✓ WIRED | EditCellValue.UndoOp restores slice (not Value re-set) |
| `ChangeColumnType.Do` | `DataTableColumnType.MangleValue` + NeedsReview | ✓ WIRED | D-04 cascade; save blocked while NeedsReviewCount > 0 |
| `Plugin.cs GetForms()` | `new FormDatatableEditor(this)` | ✓ WIRED | MEF singleton in try/catch |
| `FormTreBrowser._miOpenInDatatableEditor` | `FindOrCreateDatatableEditor` / `OpenFromTreEntry` | ✓ WIRED | D-10.2 hand-off, ext==".tab" predicate |
| `FormIffEditor._miSwitchToDatatableView` | `OpenFromMutableIff` | ✓ WIRED | D-10.3, predicate `Root.TypeId=="DTII"` |
| Save-success | `controller.MarkSaved()` + `ClientReloadDispatcher.Dispatch` | ✓ WIRED | Dirty baseline reset + tier-(b) routing |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|---|---|---|---|
| Datatable framework tests | `dotnet test UtinniCoreDotNet.Tests --filter ~Datatable` | 138 passed / 0 failed | ✓ PASS |
| CLI roundtrip-tab goldens (SC4 gate) | `dotnet test Utinni.Cli.Tests --filter ~RoundtripTab` | 16 passed | ✓ PASS |
| Save / Singleton / Sort / Reload-routing | `dotnet test ... --filter SaveTargets|Singleton|SortViewOnly|ReloadRouting` | 17 passed | ✓ PASS |
| `roundtrip-tab` verb registered | `utinni-cli --help` | verb listed | ✓ PASS |
| In-game edit propagation (SC3) | live SWG scene-change | — | ? SKIP → human (Part-B9) |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|---|---|---|---|---|
| PROD-W1-DT | 09-01..09-07 (all 7 declare it) | View/edit `.tab`; replaces SwgDataTableTool; open/view/edit/save; live client picks up edit on reload path | ✓ SATISFIED (live-ACK residual) | Full T4 editor (SC2/SC4 verified in code+automation); SC1/SC3 live-ACK is the human residual. Implemented WIDER than the requirement's T1 acceptance text by founder decision D-01 |
| PROD-02 (aggregate) | — | Wave-1 edit aggregate (Phases 8–11) | ⏳ NOT CLOSEABLE BY PHASE 9 | Aggregate spans Phases 8–11; Phase 9 contributes the datatable leg only — closes when STF + Object Template land |

No ORPHANED requirements: REQUIREMENTS.md maps only PROD-W1-DT (+ PROD-02 aggregate) to Phase 9; both accounted for.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|---|---|---|---|---|
| — | — | No debt markers (TBD/FIXME/XXX/HACK), no NotImplementedException, no stub returns | — | Clean across all 22 phase-9 source files in both repos |

NOTE: `gsd-sdk query verify.artifacts` reported 5 "Missing pattern" failures on `<Compile Include>` csproj entries (e.g. `Formats\Datatable\DataTableColumnType.cs`). These are FALSE NEGATIVES from backslash over-escaping in the SDK's pattern matcher — all five entries were confirmed present via direct Grep (UtinniCoreDotNet.csproj lines 73/75/159/229; TJT csproj line 182). NOT a real gap.

### Human Verification Required

Two batched maintainer sessions, consolidated in `09-07-SMOKE-LOG.md` (Parts A + B). Both are `☐ pending`. Per Phase 8 P05/P06/P07 precedent and the orchestrator letter, the live ACK is **deferred-but-acceptable for V1** — code and all automation gates are complete and green. See `human_verification` frontmatter for the two items.

### Gaps Summary

No code or automation gaps. All four ROADMAP success criteria are structurally satisfied in the codebase: SC2 (open/view/edit/save) and SC4 (no silent schema corruption) are fully verified by ~170 passing automated facts plus the CLI byte-exact-on-untouched golden gate; SC1 (subpanel loads in TJT) and SC3 (live client picks up edit on reload) are structurally complete and wired but their runtime/in-game observation requires the maintainer live session. The orchestrator backstop already confirmed both solutions build Debug+Release x86 and the full test suites pass (475/475 UtinniCoreDotNet.Tests, 139+1-skip Utinni.Cli.Tests, 23/23 PreservationAudit). The only outstanding work is the deferred-but-acceptable live maintainer smoke — classified `human_needed`, NOT `gaps_found`.

---

_Verified: 2026-05-29_
_Verifier: Claude (gsd-verifier)_
