---
phase: 10-tjt-subpanel-string-table-editor-stf
plan: 05
subsystem: tjt-editor + framework-handoff
tags: [stf, save-targets, reload, tre-handoff, d-04, cf-03, cf-05, sc2, sc3, wr-06]
requires: ["10-01", "10-03", "10-04"]
provides:
  - "StringTableHandoffPolicy.ShouldOfferStringTableEditor — framework D-04 hand-off gate (.stf ext OR 0xABCD sniff, !enumerateOnly)"
  - "TJT StringTableSaveTargets — bytes-layer save shim (in-place / loose-override / save-as / repack)"
  - "FormStringTableEditor Save▾ (modes 1/2/4) + reload dispatch + OpenFromTreEntry + dirty-discard"
  - "FormTreBrowser 'Open in String-table Editor' D-04 hand-off"
affects:
  - "10-06 (maintainer live-SWG smoke — the save-back + hand-off + reload flow is the smoke's core)"
tech-stack:
  added: []
  patterns:
    - "Phase 9 DatatableHandoffPolicy / DatatableSaveTargets / FormDatatableEditor save machinery / FormTreBrowser hand-off ported, simplified for flat .stf"
    - "bytes-layer save composition (StringTableWriter.Serialize → atomic write) — NOT IffSaveTargets' MutableIffDocument signature"
key-files:
  created:
    - UtinniCoreDotNet/UI/StringTableHandoffPolicy.cs
    - UtinniCoreDotNet.Tests/UITests/StringTableHandoffPolicyTests.cs
    - UtinniCoreDotNet.Tests/SavingTests/StringTableReloadRoutingTests.cs
    - "The Jawa Toolbox/TheJawaToolboxDotNet/Saving/StringTableSaveTargets.cs (UtinniPlugins repo)"
  modified:
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj
    - "The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormStringTableEditor.cs (UtinniPlugins repo)"
    - "The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs (UtinniPlugins repo)"
    - "The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj (UtinniPlugins repo)"
key-decisions:
  - "Atomic write: INLINED at the bytes layer (StringTableSaveTargets.WriteAtomic — Directory.CreateDirectory + FileStream(Create) + Flush(true)), NOT the reused Phase 8 IffSaveTargets.WriteAtomic. Reason: IffSaveTargets.WriteAtomic is hard-coded to IffWriter.Write(MutableIffDocument); .stf is a flat binary with no IFF document, so forcing it through that signature was explicitly rejected by the plan. The Phase 8 SaveResult type, LooseOverridePath.Resolve root-containment, and TreRepackSaveTarget.Apply (byte-payload) ARE reused verbatim — only the leaf write is re-expressed (~10 lines)."
  - "F11 — the FormTreBrowser context-menu Opening event does NOT have the file payload (it gates on pn.FullPath + d.EnumerateOnly only; TrePayloadResolver resolves lazily on click, mirroring the Datatable hand-off precedent). So the live `Open in String-table Editor` gate is EXTENSION-ONLY: ShouldOfferStringTableEditor(pn.FullPath, null, d.EnumerateOnly). The 0xABCD magic-sniff branch is a secondary affordance for extension-less .stf entries, exercised in isolation by the framework policy unit test (MagicSniff_ExtensionlessPayload_Offered) — never the sole live gate. The .stf magic is re-verified on click by StringTableDocument.FromBytes (clean throw → status message for a non-.stf payload)."
  - "Repack V6000 handling mirrors FormDatatableEditor: TreRepackResult has no dedicated V6000 value — the WR-06 enumerate-only refusal surfaces as Failed, handled in the default switch arm with the 'use Save as loose override' hint. RefusedClientHoldsArchive_LooseOverrideRecommended gets its own arm (no MarkSaved on a refusal)."
  - "Dirty-discard on close reuses the inherited 2-way FormSaveConfirmDialog (Discard / Cancel), not a 3-way Save/Discard/Cancel — the inherited modal is 2-outcome (Accepted/Cancelled). Since the form is hide-not-dispose, edits survive a close regardless; the prompt gives the user a Cancel to abort the close. (A true 3-way Save-then-close is a deferred polish; the 2-way reuse satisfies 'routes through FormSaveConfirmDialog'.)"
  - "ClientReloadDispatcher.Dispatch(savedPath, null) — rootTypeId is null because .stf is flat (no IFF root TypeId); Classify('.stf', null) → PendingNextSceneChange (tier-b). The user-facing badge stays the locked CF-05 copy."
requirements-completed: [PROD-W1-STF]
deviations:
  - "[Rule 3] The TJT WinForms wiring (Save▾ handlers, reload dispatch, OpenFromTreEntry, FormTreBrowser hand-off) is maintainer-smoke (10-06); the unit-testable logic (hand-off policy gate, reload-tier routing) is framework-side + xUnit-covered (12 facts), per the plan's pre-authorized deviation. MarkSaved-on-success is covered by 10-01's controller facts."
  - "PreservationAudit suite named in the verification list does not exist as a project in this repo — nothing to run; the full UtinniCoreDotNet.Tests suite (588) is green."
duration: ~1 session
completed: 2026-05-30
---

# Phase 10 Plan 05: Save Targets + Reload + TRE Browser Hand-off Summary

Completes the editor's connection to disk + live-client reload: Save▾ modes 1/2/4 (mode 3 disabled per
CF-03; repack refuses V6000 per WR-06), the framework `StringTableHandoffPolicy`, the TRE Browser
`Open in String-table Editor` hand-off (D-04 — file picker + TRE Browser are the only entry points; NO
IFF-Editor hand-off), and the tier-(b) reload dispatch. Delivers SC2 (save back) + SC3 (reload).

## What shipped

- **`StringTableHandoffPolicy`** (framework, pure) — `ShouldOfferStringTableEditor(logicalPath,
  payloadOrNull, enumerateOnly)`: `.stf` extension OR `StringTableDecoder.LooksLikeStf` magic, AND not
  enumerate-only. No `datatables/` path rule; no IFF hand-off.
- **`StringTableSaveTargets`** (TJT, ~155 lines, bytes-layer) — `SaveInPlace` / `SaveToPath` /
  `SaveLooseOverride` (via `LooseOverridePath.Resolve`) / `RepackIntoSourceTre` (via
  `TreRepackSaveTarget.Apply`). Reuses `IffSaveTargets.SaveResult`; inlines the `Flush(true)` atomic write.
- **`FormStringTableEditor`** — Save▾ click handlers + `RefreshSaveMenuEnabledState` provenance gate
  (LooseFile/TreArchive/Unknown; Save-As escape hatch; PatchLive disabled CF-03); `controller.MarkSaved()`
  on success; `saveInFlight` stale-bytes barrier; `OnReloadClicked` (locked CF-05 copy + dispatch);
  public `OpenFromTreEntry`; dirty-discard prompt on close.
- **`FormTreBrowser`** — `_miOpenInStringTableEditor` item gated on `StringTableHandoffPolicy` (Opening
  event, extension-only per F11), `OnOpenInStringTableEditor` handler + `FindOrCreateStringTableEditor`.

## Verification

- **Framework facts**: `StringTableHandoffPolicyTests` (9) + `StringTableReloadRoutingTests` (3) =
  **12 green** (≥10 required).
- **Full `UtinniCoreDotNet.Tests`**: **588 passed / 0 failed / 0 skipped**.
- **TJT MSBuild Debug|x86 + Release|x86**: both green. **Utinni Debug + Release**: green.
- **Grep gates**: csproj handoff policy=1; saveTargets→`StringTableWriter`=3, →`dataGridView`=0;
  form→`StringTableSaveTargets`=4, →`MarkSaved`=4; treBrowser→`FindOrCreateStringTableEditor`=2,
  →`ShouldOfferStringTableEditor`=1, →`Open in String-table Editor`=3.
- **Cross-repo paired commit landed** (see below).

## Atomic-write mechanism

**Inlined** at the bytes layer (`StringTableSaveTargets.WriteAtomic`: `Directory.CreateDirectory` +
`FileStream(Create)` + `Flush(true)` MEDIUM-9 barrier) — `IffSaveTargets.WriteAtomic` is hard-bound to
`IffWriter.Write(MutableIffDocument)` and `.stf` is flat, so the leaf write is re-expressed (~10 lines)
while the `SaveResult` type, `LooseOverridePath.Resolve`, and `TreRepackSaveTarget.Apply` are reused
verbatim.

## TRE Browser hand-off gate (F11)

**Extension-only.** The Opening event has no payload (lazy resolve on click), so the gate is
`ShouldOfferStringTableEditor(pn.FullPath, null, d.EnumerateOnly)`. The magic-sniff branch is a secondary
affordance unit-tested in isolation; the `.stf` magic is re-verified on click by `FromBytes`.

## StringTableSaveTargets line count

**157 lines total** (23-line MIT header + XML docs included). The executable shim — the four save methods
+ `SaveBytesToPath` + `WriteAtomic` — is ~75 lines, comfortably under the < 120-line budget; the balance
is the header + doc comments.

## Self-Check: PASSED
