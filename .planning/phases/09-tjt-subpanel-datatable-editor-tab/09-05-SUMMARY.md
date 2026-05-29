---
phase: 09-tjt-subpanel-datatable-editor-tab
plan: 05
subsystem: tjt-datatable-editor
tags: [save-targets, entry-points, provenance-gate, reload-routing, cross-repo]
requires:
  - "09-01 DataTableWriter (BuildMutableIff + Serialize)"
  - "09-03 FormDatatableEditor host + Save▾ menu shape"
  - "09-04 DatatableEditController (MarkSaved / NeedsReviewCount / PendingCascadeContext seams)"
  - "Phase 8 IffSaveTargets + TreRepackSaveTarget + ClientReloadDispatcher + LooseOverridePath + ReloadAssetClassifier + TreRecordIndexResolver + OpenSource"
provides:
  - "TJT.Saving.DatatableSaveTargets — < 100-line composition shim (modes 1/2/3 via IffSaveTargets, mode 4 via TreRepackSaveTarget)"
  - "FormDatatableEditor Save▾ wired (provenance + NeedsReview composed gate) + MarkSaved-on-success + OpenFromTreEntry + OpenFromMutableIff + reload-dispatch + saveInFlight barrier"
  - "FormTreBrowser D-10.2 Open-in-Datatable-Editor hand-off"
  - "FormIffEditor D-10.3 Switch-to-typed-datatable-view hand-off"
affects:
  - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet (Saving + 3 Forms + csproj)"
  - "UtinniCoreDotNet.Tests (2 new SavingTests files)"
tech-stack:
  added: []
  patterns: ["Phase 8 verbatim-reuse composition shim", "provenance-gate composed on top of NeedsReview gate", "framework-layer testing of plugin-shim composition legs (Phase 8 precedent)"]
key-files:
  created:
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/DatatableSaveTargets.cs"
    - "UtinniCoreDotNet.Tests/SavingTests/DatatableSaveTargetsTests.cs"
    - "UtinniCoreDotNet.Tests/SavingTests/DatatableReloadRoutingTests.cs"
  modified:
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj"
decisions:
  - "DatatableSaveTargets lives in namespace TJT.Saving (matching the SHIPPED Phase 8 IffSaveTargets/TreRepackSaveTarget convention), NOT the plan's stated TheJawaToolboxDotNet.Saving"
  - "DatatableSaveTargetsTests exercise the FRAMEWORK primitives the shim composes (the TJT WinForms/native assembly is not project-referenceable from the test project — Phase 8 precedent)"
  - "V6000 repack refusal surfaces as TreRepackResult.Failed (TreWriter.Repack throws NotSupportedException; there is no dedicated RefusedV6000Encrypted enum value — the plan's assumption was inaccurate)"
metrics:
  duration: "~70 min"
  completed: 2026-05-29
  tasks: 2
  files: 7
---

# Phase 9 Plan 05: Save Targets + Entry Points Summary

DatatableSaveTargets composition shim (87-line body) routes all four V1 save modes through Phase 8's verbatim save plumbing; FormDatatableEditor's Save▾ now dispatches with a composed provenance+NeedsReview gate and calls `controller.MarkSaved()` on every save success; the TRE-Browser (D-10.2) and IFF-Editor (D-10.3) hand-offs land additively.

## One-liner

Typed datatable Save▾ (in-place / loose-override / Save-As / .tre-repack) + the two D-10 entry-point hand-offs, all composed verbatim on Phase 8's hardened save + reload infrastructure.

## What shipped

### Task 1 — DatatableSaveTargets shim + reload-routing facts
- **`DatatableSaveTargets.cs`** (`TJT.Saving`, 110 lines / 87-line body excl. 23-line MIT header — under the < 100-line body target): four `Task<...>` methods. `SaveLooseOverride` / `SaveToPath` / `SaveInPlace` build the `MutableIffDocument` via `DataTableWriter.BuildMutableIff()` and forward to `IffSaveTargets`; `RepackIntoSourceTre` forwards `DataTableWriter.Serialize()` bytes to `TreRepackSaveTarget.Apply`. No `dataGridView` reference (D-09 writer-layer defense — case-sensitive grep gate 0).
- **csproj:** `<Compile Include="Saving\DatatableSaveTargets.cs" />` added alphabetically within the `Saving\` block.
- **DatatableSaveTargetsTests.cs (7 facts):** DTII build byte-exact (no edits); untouched-cell preservation after one edit (SC4 at the shim layer); `LooseOverridePath.Resolve` containment + traversal-reject; V6000 repack refusal on a DTII payload (WR-06 inheritance); valid-archive DTII repack round-trip + reopen; BuildMutableIff/Serialize byte-identity.
- **DatatableReloadRoutingTests.cs (3 facts):** `(".iff","DTII") → PendingNextSceneChange`; DTII never an in-session reload tier; extension-casing stability.

### Task 2 — Form wiring (cross-repo, UtinniPlugins-only)
- **FormDatatableEditor:** 5 Save▾ click handlers; `controller.MarkSaved()` on each save-success path; `RefreshSaveMenuEnabledState` rewritten to compose the provenance gate on top of 09-04's NeedsReview gate; public `OpenFromTreEntry` + `OpenFromMutableIff`; `saveInFlight` MEDIUM-9 barrier (disables Save▾ + Reload during a save); `DispatchReload` routing-table audit trail; `OnReloadClicked` keeps the CF-05 locked copy.
- **FormTreBrowser (D-10.2):** `Open in Datatable Editor` context item, HIDDEN unless the entry is a `.tab` (extension-only V1 visibility) + non-enumerate-only; `OnOpenInDatatableEditor` + `FindOrCreateDatatableEditor` mirror the IFF hand-off verbatim.
- **FormIffEditor (D-10.3):** `Switch to typed datatable view` tree-context item, visible only when `document.Root.TypeId == "DTII"`; `OnSwitchToDatatableViewClick` hands `this.document` + `Source` directly to `OpenFromMutableIff` (no re-parse); `FindOrCreateDatatableEditor` helper. Manual hand-off — the IFF Editor stays open.

## Save▾ dispatch table (handler × provenance gate × NeedsReview gate × MarkSaved)

| Menu item | Enabled when | Dispatches to | MarkSaved on success? |
|---|---|---|---|
| Save (in place) | `LooseFile` && !cascade && !inFlight | `DatatableSaveTargets.SaveInPlace` | Yes |
| Save as loose override | (`LooseFile`\|`TreArchive`) && !cascade && !inFlight | `DatatableSaveTargets.SaveLooseOverride` | Yes |
| Save As… | hasDoc && !cascade && !inFlight (escape hatch) | `DatatableSaveTargets.SaveToPath` | Yes |
| Patch live client | DISABLED (CF-03) | — (defensive no-op) | No |
| Repack into source .tre… | `TreArchive` && !cascade && !inFlight | `DatatableSaveTargets.RepackIntoSourceTre` | Yes (Replaced/BackedUpThenReplaced only) |

The composed gate matches the plan's behavior-block inline code: each item is `provenanceAllowsThisMode && !blockedByCascade && !saveInFlight`; Save-As stays enabled on Unknown provenance (round-2 MEDIUM 5 escape hatch); the top-level `btnSave` enables on LooseFile/TreArchive/Unknown so the Save-As escape hatch is always reachable.

## Decisions / corrections vs the plan

- **Namespace `TJT.Saving`** (not the plan's `TheJawaToolboxDotNet.Saving`) — matches the SHIPPED Phase 8 `IffSaveTargets`/`TreRepackSaveTarget`. The grep gates (`DataTableWriter` in file; `Saving\DatatableSaveTargets.cs` in csproj) are namespace-agnostic and pass.
- **`SaveResult` members are `Ok`/`Path`/`Message`** (not the plan's `Succeeded`/`SavedPath`/`ErrorMessage`) — the DoFileSaveAsync orchestration uses the actual Phase 8 contract.
- **V6000 refusal = `TreRepackResult.Failed`** — there is no `RefusedV6000Encrypted` enum value. `TreWriter.Repack` throws `NotSupportedException` for enumerate-only archives; `TreRepackSaveTarget.ApplyCore` catches it and returns `Failed`. The Failed status copy steers the user to the loose-override escape hatch. The WR-06 inheritance fact asserts the framework `NotSupportedException` (with the "loose override" message) the whole chain depends on.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] DatatableSaveTargetsTests retargeted to the framework composition legs**
- **Found during:** Task 1 (TDD setup).
- **Issue:** The plan's `<interfaces>` assumed the tests instantiate the plugin-side `DatatableSaveTargets` shim directly. `UtinniCoreDotNet.Tests` has NO project reference to the UtinniPlugins TJT assembly, and TJT (WinForms + native UtinniCore bindings) cannot be cleanly ProjectReferenced into an x86 test assembly. This is the exact constraint under which Phase 8 unit-tested its save plumbing at the framework layer (`LooseOverridePathTests`, `TreWriterTests`) and smoke-tested the plugin wrappers.
- **Fix:** The 7 facts exercise the EXACT framework primitives the shim chains — `DataTableWriter.BuildMutableIff/Serialize` → `IffWriter.Write` byte-exactness, `LooseOverridePath.Resolve` containment, `TreWriter.Repack` V6000 refusal + valid-archive round-trip. A regression in any composed leg surfaces against a 09-05-named test. The plugin shim itself is a verbatim forwarder with no testable logic of its own; it compiles green against the real Phase 8 API and is exercised end-to-end by the 09-07 Tier-4 smoke.
- **Files modified:** `DatatableSaveTargetsTests.cs`
- **Commit:** Utinni `3b02999`

**2. [Rule 1 - Doc-accuracy] V6000 refusal enum corrected**
- Documented above under Decisions — the plan's `RefusedV6000Encrypted` enum value does not exist; the refusal manifests as `Failed`. No code workaround needed; the handler's Failed branch surfaces the loose-override recommendation, and the test asserts the underlying `NotSupportedException`.

## TRE-Browser visibility-predicate decision

Ships **extension-only** (`.tab`) for V1, HIDDEN (not just disabled) when the entry isn't a `.tab`. The DTII-in-non-`.tab` corner case is reachable via **Open in IFF Editor → Switch to typed datatable view** (D-10.3). This UX gap is flagged for the **09-07 Tier-4 smoke**. Adding `IffReader.TryPeekRootTypeId` is a new framework surface deferred to V2 (iter-2 LOW).

## IFF-Editor menu placement

`Switch to typed datatable view` is added to FormIffEditor's **tree context menu** (`treeContextMenu`), after a separator following the structural ops. Visibility is driven by `RefreshSwitchMenuVisibility()` (called from `LoadDocument` and the context-menu `Opening` event) gating on `document.Root.TypeId == "DTII"`. (V2 reviewer note: a dedicated `View` menu was the alternative; the tree-context placement keeps it discoverable next to the chunk it acts on.)

## Verification

- TJT MSBuild Debug|x86: GREEN.
- `dotnet test --filter "FullyQualifiedName~DatatableSaveTargetsTests|FullyQualifiedName~DatatableReloadRoutingTests"`: 10/10 pass (≥8 required; V6000 refusal + DTII routing facts explicit).
- `dotnet test --filter "FullyQualifiedName~Datatable"`: 121/121 (no regression of 09-01..04 baseline).
- Full `UtinniCoreDotNet.Tests`: 458/458.
- All Task-1 + Task-2 grep gates pass (DatatableSaveTargets ≥4, MarkSaved ≥3, Dispatch ≥1, OpenFromMutableIff/OpenFromTreEntry ≥1, FindOrCreateDatatableEditor ≥1/file, locked menu texts ≥1).

## Cross-repo commits

| Task | Utinni | UtinniPlugins |
|---|---|---|
| 1 | `3b02999` test(09-05): save-target + reload-routing facts | `149904c` feat(09-05): DatatableSaveTargets shim + csproj |
| 2 | — (no Utinni production change) | `b3dd75d` feat(09-05): wire Save▾ + D-10.2/D-10.3 hand-offs |

## Self-Check: PASSED

- FOUND: `DatatableSaveTargets.cs`, `DatatableSaveTargetsTests.cs`, `DatatableReloadRoutingTests.cs`, `09-05-SUMMARY.md`
- FOUND commits: Utinni `3b02999`; UtinniPlugins `149904c`, `b3dd75d`
