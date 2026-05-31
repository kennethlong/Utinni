---
phase: 11-tjt-subpanel-object-template-editor
plan: 04
subsystem: tjt-editor-mutations-save-reload
tags: [object-template, tjt-subpanel, typed-widgets, hex-fallback, promote-revert, save-targets, reload-badge, cross-repo]

# Dependency graph
requires:
  - phase: 11-tjt-subpanel-object-template-editor
    plan: 01
    provides: ObjectTemplateParamValue (From* factories + Kind/ParamTypeLabel/DeltaType) + ObjectTemplateWriter.Serialize + MutableObjectTemplate.SourceIff
  - phase: 11-tjt-subpanel-object-template-editor
    plan: 02
    provides: ObjectTemplateEditController.Apply + ObjectTemplateEditCommands.EditValue/AddOverride/RemoveOverride
  - phase: 11-tjt-subpanel-object-template-editor
    plan: 03
    provides: FormObjectTemplateEditor host (4-column effective grid, Save/Promote/Revert/Reload controls as stubs, OpenSource provenance, OnEditApplied refresh)
  - phase: 09-tjt-subpanel-datatable-editor-tab
    provides: FormDatatableEditor clone target (BuildSaveMenu / DoFileSaveAsync / OnReloadClicked) + DatatableSaveTargets shim + DatatableNumericUpDownEditingControl
  - phase: 08-tjt-subpanel-iff-editor-read-write
    provides: IffSaveTargets (modes 1/2) + TreRepackSaveTarget (mode 4, V6000 WR-06 refusal + atomic File.Replace + backup) + FormSaveConfirmDialog + ClientReloadDispatcher + IFF hex/text leaf-edit idiom
  - phase: 08-tjt-subpanel-iff-editor-read-write
    provides: ReloadAssetClassifier (object-template .iff -> PendingNextSceneChange; VERIFY-ONLY)
provides:
  - FormObjectTemplateEditor mutations + widgets + save + reload-badge (closes the "edit overrideable fields, save back" half of SC2 + the SC3 reload-candor surface)
  - FormParamHexEditor per-call modal (Consolas hex/text leaf sub-editor) for complex/raw params (D-02 fallback)
  - ObjectTemplateNumericUpDownEditingControl + ObjectTemplateNumericCell (int/float cell editor swap)
  - ObjectTemplateSaveTargets (<100-line shim forwarding to IffSaveTargets / TreRepackSaveTarget)
  - ReloadAssetClassifierTests (OT-root + .iff-fallback -> PendingNextSceneChange tier-(b) verify)
affects: [11-05-live-smoke]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Per-type Effective-value cell editing: bool -> DataGridViewCheckBoxCell swap; int/float -> ObjectTemplateNumericCell whose EditType is the UtinniNumericUpDown editing control (tuned in EditingControlShowing); string -> text cell; complex/raw/unresolved -> read-only inline, edited via the hex sub-editor"
    - "Origin-branching commit seam (D-04): a committed scalar edit routes EditValue on a LocalOverride row, AddOverride (promote) on an Inherited row"
    - "Side-effect-free inherited-fallback PREVIEW (TryResolveInheritedValue): RemoveOverride -> resolve -> restore via AddOverride inside a try/finally, used by both the revert confirm/feedback and the Revert button's enabled-state"
    - "Save shim forwards MutableObjectTemplate.SourceIff (hybrid-DOM, untouched params verbatim) to the Phase 8 dispatchers; repack forwards ObjectTemplateWriter.Serialize() bytes"
    - "Reload badge governed by Game.IsRunning (hidden + disabled with the no-client tooltip when down); reload click STATES the LOCKED CF-05 candor and routes the audit-trail dispatch only (no engine refetch hook invoked)"

key-files:
  created:
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormParamHexEditor.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormParamHexEditor.Designer.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/ObjectTemplateNumericUpDownEditingControl.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/ObjectTemplateSaveTargets.cs"
    - "UtinniCoreDotNet.Tests/Saving/ReloadAssetClassifierTests.cs"
  modified:
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormObjectTemplateEditor.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj"

key-decisions:
  - "FormParamHexEditor is a small self-contained modal hosting a Consolas 9pt multiline hex text box with the SAME pairs-of-0-9/A-F parse/format the Phase 8 IFF Editor's txtHex uses, rather than physically re-hosting FormIffEditor's inline control (which is not a separable widget — its hex/text editing is inline txtHex/txtText fields bound to currentLeaf). The reuse is at the editing-contract level (identical parse, Consolas, controller-routed commit), which is the intent of 'wrap the Phase 8 leaf editor'."
  - "int/float numeric widget swap is made REAL via ObjectTemplateNumericCell (a DataGridViewTextBoxCell whose EditType is the editing control), unlike the Phase 9 DatatableNumericUpDownEditingControl which was declared but never assigned as a cell EditType (so DT_Int/DT_Float there edit as plain text). The OT editor assigns the numeric cell per-row in BindEffectiveView so EditingControlShowing actually tunes DecimalPlaces/range."
  - "The ObjectTemplateSaveTargets shim + the form's Save▾ menu wiring co-landed in the Task 1 commit (UtinniPlugins 758330d) because the form references the shim and would not compile without it; the Task 2 commit (a9b738f) carries only the CF-05 refetch-comment grep-hygiene reword. The classifier verify-test is the Task 2 deliverable on the Utinni side (78ec981)."
  - "ReloadAssetClassifier is UNCHANGED (verify-only). The SHOT/STOT/SBOT allowlist already covers the demo OT roots AND the conservative .iff fallback already routes unknown OT roots to PendingNextSceneChange (RESEARCH Assumption A1 held — no real fixture surfaced a root outside the allowlist that the fallback didn't already cover), so no allowlist extension was needed."

requirements-completed: [PROD-W1-OT]

# Metrics
duration: ~28min
completed: 2026-05-31
---

# Phase 11 Plan 04: Object Template Editor Mutations + Widgets + Save + Reload Badge Summary

**The Object Template Editor becomes editable: per-type scalar value widgets (bool checkbox / int-float numeric / string text) with a Consolas hex/text fallback so no param is ever uneditable (D-02); the three D-04 override/revert/edit mutations routed through the editor-local controller (promote-on-inherited-edit); Save modes 1/2/4 via a <100-line shim to the Phase 8 targets (mode 3 disabled CF-03, V6000 refused); and the honest CF-05 tier-(b) reload badge that STATES the cache reality without triggering a refetch — verified by an OT-root classifier test.**

## Performance

- **Duration:** ~28 min
- **Completed:** 2026-05-31
- **Tasks:** 2 (both auto)
- **Files:** 7 (5 created, 2 modified) across both repos

## Accomplishments

- **Per-type value widgets (D-02).** The Effective-value cell edits by decoded type: `bool` swaps to a `DataGridViewCheckBoxCell` (toggle commits immediately); `int`/`float` swap to `ObjectTemplateNumericCell` whose `EditType` is `ObjectTemplateNumericUpDownEditingControl` (a `UtinniNumericUpDown` adapted to `IDataGridViewEditingControl`), tuned in `OnEditingControlShowing` (int → DecimalPlaces 0; float → DecimalPlaces 6); `string` edits as free text. Complex/ambiguous (`raw bytes (hex)`), `None`, and unresolved-base cells are **read-only inline** (`CellBeginEdit` cancels) — they are never typed-edited.
- **Hex-fallback sub-editor (`FormParamHexEditor`).** A per-call modal (`using (...)`, default dispose-on-close) titled `Edit raw bytes — {field}` hosting a Consolas 9pt multiline hex box with the same pairs-of-0-9/A-F parse the Phase 8 IFF Editor uses. Opened from a complex value cell double-click or the context `Edit raw bytes…`; on OK the parsed bytes replace the param via the controller (`EditValue` for a local override, `AddOverride` to promote an inherited complex param) → dirty + undoable; Cancel makes no change.
- **The three D-04 mutations (undoable).** `CommitCell` branches on origin: a `LocalOverride` row routes `controller.Apply(EditValue(...))`; an `Inherited` row routes `controller.Apply(AddOverride(...))` — **editing an inherited value PROMOTES it to a local override**. `Promote to override` (toolbar + context) copies the inherited value verbatim into a local chunk; `Revert to inherited` (toolbar + context) deletes the local chunk after a lightweight `FormSaveConfirmDialog` confirm and restores the inherited value. Status copy: `Promoted "{field}" to a local override.` / `Reverted "{field}" to the inherited value from {ancestor}.`. Revert on a field with no resolvable inherited value is **DISABLED** with `No inherited value to revert to — this field exists only locally.`
- **Side-effect-free inherited preview.** `TryResolveInheritedValue` removes the local override, re-resolves the chain to find the inherited supplier (+ ancestor name), then restores the override inside a `try/finally` — used both for the revert confirm/feedback and to drive the Revert button + context-item enabled-state.
- **Save modes 1/2/4 (`ObjectTemplateSaveTargets`, 94 lines).** Clones `DatatableSaveTargets`: each method forwards the model's captured `MutableIffDocument` (`SourceIff`) to `IffSaveTargets.SaveLooseOverride`/`SaveToPath`/`SaveInPlace` (modes 1/2) and `ObjectTemplateWriter.Serialize()` bytes to `TreRepackSaveTarget.Apply` (mode 4). The V6000 enumerate-only refusal (WR-06), atomic `File.Replace`, repack lock, and timestamped backup all come free from the Phase 8 layer. The form clones `BuildSaveMenu` (5 items; `Patch live client` **disabled** with the inherited CF-03 tooltip), `RefreshSaveMenuEnabledState` (the `OpenSource` provenance gate, Phase-9 cascade term dropped — no OT cascade), the save click handlers + `DoFileSaveAsync` (`saveInFlight` barrier, `Saving/Saved/<reason>… try another save target` copy, `controller.MarkSaved()` on success, Reload disabled during save). Repack reuses `FormSaveConfirmDialog`.
- **CF-05 reload badge (LOCKED).** `lblReloadBadge` reads `Reloads on next scene change (relog to guarantee).` when a document is loaded and a client is up. `Reload in client` invokes **no engine refetch hook**; on click it surfaces the longer locked OT-cache-reality copy (`Templates re-resolve on the next scene change… keep the cached version until a relog… relog to guarantee.`) + a 1s accent tint pulse, and routes the saved asset through `ClientReloadDispatcher.Dispatch(savedPath, RootType)` for the routing-table audit trail only. When `!Game.IsRunning` the button is disabled with `No live client — start SWG to apply edits in-session.` and the badge is hidden.
- **`ReloadAssetClassifierTests` (verify-only).** 3 theory rows (SHOT/STOT/SBOT) + 2 facts assert the OT root TypeIds AND the conservative `.iff` fallback (`SXXX`) classify as `PendingNextSceneChange` and never as an in-session texture/terrain tier. The classifier is **not modified**.

## Task Commits

1. **Task 1: per-type value widgets + hex-fallback sub-editor + override/revert/edit mutations** — UtinniPlugins `758330d` (feat). _Co-landed the `ObjectTemplateSaveTargets` shim + the form's Save▾ menu wiring for build-coherence — the form references the shim and will not compile without it._
2. **Task 2: Save modes 1/2/4 wiring + CF-05 reload badge candor reword + classifier verify-test** — UtinniPlugins `a9b738f` (feat, the CF-05 refetch-comment grep-hygiene reword) + Utinni `78ec981` (feat, `ReloadAssetClassifierTests`).

_Cross-repo per standing authority — the WinForms host + hex modal + numeric control + save shim live in UtinniPlugins; the framework classifier verify-test lives in Utinni. No human checkpoint (only the live-SWG smoke, Plan 11-05, needs one)._

## Decisions Made

- **Hex modal reuses the editing CONTRACT, not the control instance.** The Phase 8 IFF Editor's hex editing is inline `txtHex`/`txtText` fields bound to `currentLeaf` — not a separable widget. `FormParamHexEditor` re-implements the identical pairs-of-0-9/A-F parse + Consolas 9pt surface + controller-routed commit, which is the substance of the "wrap the Phase 8 leaf editor" guidance.
- **Made the numeric swap real.** Phase 9's `DatatableNumericUpDownEditingControl` is declared but never assigned as a cell `EditType` (DT_Int/DT_Float edit as plain text there). The OT editor assigns `ObjectTemplateNumericCell` per-row so `EditingControlShowing` actually tunes the up-down — a strict improvement that still round-trips through `TryCoerceValue`.
- **Save shim + save-menu co-landed in Task 1.** The form (single file spanning both tasks) references `ObjectTemplateSaveTargets`, so the shim had to be in the first commit for the build to be green. Task 2's UtinniPlugins commit is therefore the small grep-hygiene reword; its substantive deliverable is the Utinni-side classifier test.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] CF-05 refetch-hook comment tripped the literal grep gate**
- **Found during:** Task 2 acceptance verification.
- **Issue:** The acceptance gate `grep "ObjectTemplateList\|::reload\|\.Reload(" <form> returns 0 hook-call matches` is literal. A single source COMMENT ("Utinni does NOT call `ObjectTemplateList::reload`") matched it even though it is not a hook call.
- **Fix:** Reworded the comment to "does NOT invoke the engine's template-list refetch hook" (preserves the documented intent; the gate now reads 0). Per the grep-gate-hygiene memory.
- **Files modified:** `FormObjectTemplateEditor.cs`
- **Commit:** UtinniPlugins `a9b738f`

No architectural escalations (Rule 4). No `./CLAUDE.md` exists in the working tree (confirmed by RESEARCH/PATTERNS).

## Issues Encountered

- **`IffSaveTargets`/`TreRepackSaveTarget` live plugin-side (`TJT.Saving`), not in Utinni.** The shim and the form resolve them via the plugin namespace; `IffSaveTargets.SaveResult` is the nested result carrier the form's `DoFileSaveAsync` consumes (same as the datatable editor). No framework dependency added.
- **Git Bash CRLF-on-checkout warnings** on the new `.cs` files are benign (repo `.gitattributes`/clang-format handle EOLs).
- No `Generated/UtinniCore.cs` regen churn occurred — the TJT build consumes the framework DLL and the test build does not regenerate the CppSharp bindings.

## Threat Surface

The plan's threat-register mitigations for this plan are covered:
- **T-11-11** (typed widget commits an invalid value → corrupt param) — typed widgets are type-constrained (checkbox / numeric up-down with int/float range + DecimalPlaces); complex/ambiguous params are read-only-inline and edited only via the hex sub-editor through the controller; `TryCoerceValue` rejects an invalid numeric (re-binds to discard) rather than writing garbage; the round-trip golden (Plan 02 `roundtrip-ot`) is the byte-exact regression lock.
- **T-11-12** (repack overwrites a source `.tre` without backup / on V6000) — `ObjectTemplateSaveTargets.RepackIntoSourceTre` forwards to `TreRepackSaveTarget.Apply`, which enforces the V6000 (WR-06) refusal, timestamped backup, repack lock, and atomic `File.Replace` — not reimplemented; repack is gated behind `FormSaveConfirmDialog`.
- **T-11-13** (over-promising "reflected on respawn") — the LOCKED CF-05 tier-(b) badge + click copy state the cache reality (respawn unreliable; relog guarantees), verified by the verbatim-string grep gate; the editor STATES, never triggers, a reload (CON-M-05 — the refetch-hook grep gate reads 0).
- **T-11-14** (live-patch enabled prematurely) — mode 3 ships DISABLED with the inherited CF-03 tooltip; no in-memory write path is wired.
- **T-11-SC** (package installs) — N/A; zero new dependencies.

No new security-relevant surface beyond the planned typed/hex-edit → `.iff` bytes (save) and save-target → filesystem/archive boundaries already in the plan's threat model. No threat flags.

## Known Stubs

None. All four host surfaces stubbed in Plan 11-03 are now real:

| Surface (11-03 stub) | Now |
|----------------------|-----|
| `Save ▾` | Modes 1/2/4 wired via `ObjectTemplateSaveTargets`; mode 3 disabled (CF-03); V6000 refused; `MarkSaved()` on success |
| `Promote to override` | `controller.Apply(AddOverride(...))` — copies the inherited value into a local chunk; undoable |
| `Revert to inherited` | `controller.Apply(RemoveOverride(...))` after a confirm; disabled with the locked tooltip on local-only fields; undoable |
| `Reload in client` | LOCKED CF-05 candor + 1s pulse + audit-trail dispatch; no refetch hook; hidden/disabled when no client |
| Inline value editing | per-type widgets + hex fallback; grid no longer globally ReadOnly |

The Find/Replace pane search wiring remains a deferred surface (its controls exist; the plan scoped search to a later polish pass — no false data rendered, the pane stays collapsed by default).

## User Setup Required

None — no external service configuration required.

## Verification Performed

- **Build (the load-bearing gate per Phase 8/9/10 precedent — WinForms-host logic is verified by an MSBuild-green TJT build, not by an x86-test reference):**
  - `TheJawaToolboxDotNet` **Debug|x86 AND Release|x86** — clean (`-> …/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll`), after BOTH tasks.
  - `UtinniCoreDotNet.Tests` Debug|x86 AND Release|x86 — clean (xUnit-analyzer style warnings only, pre-existing).
  - VS2026 MSBuild (Dev18, `D:\Program Files\Microsoft Visual Studio\18\Community`).
- **Framework xUnit:** `dotnet test UtinniCoreDotNet.Tests --no-build` → **624 passed, 0 failed, 0 skipped** in BOTH Debug|x86 and Release|x86 (619 Plan-03 baseline + 5 new `ReloadAssetClassifierTests` facts). `--filter ReloadAssetClassifier` → 8 passed (5 new + the 3 existing Datatable/StringTable reload-routing facts whose names also match the filter substring).
- **Task 1 acceptance grep gates (all pass):**
  - `grep -c "AddOverride|RemoveOverride|EditValue"` (form) → 11 (≥3); commit-path branches on origin (`Inherited` → `AddOverride`).
  - `grep -c "Promoted|Reverted|No inherited value to revert to"` (form) → 7 (≥2).
  - `grep -c "FormParamHexEditor"` (form) → 2 (the hex sub-editor is opened from the form).
  - Complex/ambiguous params are NOT inline-editable — `IsInlineEditable` returns false for `RawBytesHexFallback`/`None`/unresolved; `OnCellBeginEdit` cancels.
- **Task 2 acceptance grep gates (all pass):**
  - `grep -c "IffSaveTargets|TreRepackSaveTarget"` (`ObjectTemplateSaveTargets.cs`) → 11 (≥2); `wc -l` → **94** (<100).
  - `grep -c "Patch live client|Live patch requires opening from client memory"` (form) → 3 (≥2); `MarkSaved()` → 3.
  - `grep -c "Reloads on next scene change (relog to guarantee)."` (form) → 1 AND `grep -c "keep the cached version until a relog"` → 1.
  - `grep -c "ObjectTemplateList|::reload|\.Reload("` (form) → **0** (no refetch-hook call; the comment was reworded to clear the literal gate).
  - `ReloadAssetClassifierTests` has the OT-root → `PendingNextSceneChange` `[Theory]`/`[Fact]`s; `--filter ReloadAssetClassifier` passes.

## Next Phase Readiness

- **Ready for 11-05 (live-SWG smoke checkpoint):** every param is editable (typed widgets + hex fallback); the three D-04 mutations are wired + undoable; Save offers modes 1/2/4 (mode 3 disabled, V6000 refused) with `MarkSaved()` on success; the reload badge states the LOCKED CF-05 candor and triggers nothing; the classifier routing is confirmed. The remaining gate is the human live-SWG smoke (open a template via a hand-off, edit/promote/revert, save loose-override + repack, observe the scene-change-vs-relog reload behavior) — the SC3 residual carried from Phase 10.
- No blockers. CON-M-05 preserved (the editor STATES the reload; the refetch-hook grep gate reads 0; the undo stack is the editor-local controller).

## Self-Check: PASSED

All 5 created files (`FormParamHexEditor.cs` + `.Designer.cs`, `ObjectTemplateNumericUpDownEditingControl.cs`, `ObjectTemplateSaveTargets.cs`, `ReloadAssetClassifierTests.cs`) + the SUMMARY exist on disk; all three task commits (UtinniPlugins `758330d`, `a9b738f`; Utinni `78ec981`) are present in their repos' git history.

---
*Phase: 11-tjt-subpanel-object-template-editor*
*Completed: 2026-05-31*
