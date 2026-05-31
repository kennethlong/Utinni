---
phase: 11-tjt-subpanel-object-template-editor
plan: 03
subsystem: tjt-editor-host
tags: [object-template, tjt-subpanel, effective-view, inheritance-breadcrumb, origin-overlays, singleton-form, handoff, cross-repo]

# Dependency graph
requires:
  - phase: 11-tjt-subpanel-object-template-editor
    plan: 01
    provides: MutableObjectTemplate (FromMutableIff) + ObjectTemplateParamValue (ParamTypeLabel/Kind/DeltaType)
  - phase: 11-tjt-subpanel-object-template-editor
    plan: 02
    provides: ObjectTemplateResolver (Resolve/ResolveViaArchive) + EffectiveTemplateView/EffectiveField/BreadcrumbSegment + ObjectTemplateEditController
  - phase: 09-tjt-subpanel-datatable-editor-tab
    provides: FormDatatableEditor clone target + ThemedDataGridView + SingletonFormClosePolicy + DatatableHandoffPolicy analog
  - phase: 08-tjt-subpanel-iff-editor-read-write
    provides: IffReader + MutableIffDocument.FromDocument + OpenSource + TreRecordIndexResolver + FormIffEditor hand-off site
  - phase: 07-tjt-subpanel-tre-browser-read-only
    provides: ObjectTemplateDecoder.LooksLikeObjectTemplate + TrePayloadResolver.TryResolve + FormTreBrowser context-menu site + TreArchiveIndex
provides:
  - FormObjectTemplateEditor host (UtinniForm, IEditorForm) — 4-column effective-inheritance grid (Field/Effective value/Origin/Type), ancestor breadcrumb, origin overlays, editor-local undo/redo, Show-inherited toggle, singleton hide-not-dispose, two OpenFrom* entry points
  - OtHandoffPolicy framework helper (ShouldOfferObjectTemplateEditor path gate + IsObjectTemplatePayload content sniff)
  - 5th SubPanel registration in TJT Plugin.cs (GetForms()) — SPI not widened
  - TRE Browser "Open in Object Template Editor" + IFF Editor "Switch to typed object-template view" hand-offs (HIDDEN when the sniff fails)
affects: [11-04-mutations-widgets, 11-05-save-reload]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Effective-view grid bound from EffectiveField rows directly (NOT ThemedDataGridView.BindMutable, which is datatable-typed) with origin overlays via the form's own CellFormatting"
    - "Background DERV-chain resolve (Task.Run → BeginInvoke marshal) that NEVER blocks the open; degrades to a null-locator resolve when no TRE archive index is buildable"
    - "Lazily-built, once-cached TreArchiveIndex from the resolved client root for production base resolution; null → graceful unresolved-base degradation (D-01 LOCKED)"
    - "OtHandoffPolicy mirrors DatatableHandoffPolicy: cheap .iff path gate (visibility) + click-time LooksLikeObjectTemplate content sniff (never throws)"
    - "Mutable-tree LooksLikeObjectTemplate sniff in FormIffEditor (DERV child OR digit version form with 4-byte count) — the MutableIffNode analog of the framework IffChunk sniff"

key-files:
  created:
    - "UtinniCoreDotNet/UI/OtHandoffPolicy.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormObjectTemplateEditor.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormObjectTemplateEditor.Designer.cs"
  modified:
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs"
    - "../UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs"

key-decisions:
  - "The grid binds OWN DataGridViewRows from EffectiveField (not ThemedDataGridView.BindMutable, which is hard-coded to MutableDataTableDocument). The form keeps the ThemedDataGridView dark token map but supplies its own CellFormatting for the OT origin overlays + a 4-fixed-column config with the Phase 11 deviations (AllowUserToOrderColumns=false, MultiSelect=false/FullRowSelect, Value-only Fill)."
  - "Base-chain resolution wires to a lazily-built TreArchiveIndex from the resolved client root (best-effort, cached once). When no client root / archive set is available (e.g. offline editing of a loose file), resolution uses a null-returning locator so the open still succeeds with inherited bases marked unresolved — the D-01 LOCKED graceful-degradation contract (the open NEVER blocks). Full archive-backed inheritance is exercised live in 11-04/11-05."
  - "OtHandoffPolicy ships framework-side (UtinniCoreDotNet/UI, mirroring DatatableHandoffPolicy/SingletonFormClosePolicy) so the visibility + content gates are unit-testable and reusable; the .iff extension is the cheap visibility gate (object templates have no single source extension), and the click-time IsObjectTemplatePayload runs the Phase-7 LooksLikeObjectTemplate sniff and never throws."
  - "Save▾ / Promote / Revert / Reload click bodies are status-only stubs this plan (the controls EXIST and the surface is fully navigable); the mutation/save/reload-badge bodies land in 11-04 per the plan's explicit staging. The grid is ReadOnly as a surface this plan (value editing is 11-04)."

requirements-completed: []

# Metrics
duration: ~9min
completed: 2026-05-31
---

# Phase 11 Plan 03: Object Template Editor Host + Hand-offs Summary

**The TJT-side Object Template Editor (CF-06) — a resizable `FormObjectTemplateEditor` cloned from `FormDatatableEditor` that renders the D-01 effective-inheritance view (Field · Effective value · Origin · Type) with the ancestor breadcrumb and origin overlays, wires the editor-local `ObjectTemplateEditController` + undo/redo + the Show-inherited toggle, applies the singleton hide-not-dispose policy, registers as the fifth and final V1 SubPanel, and exposes both hand-off entry points (TRE Browser + IFF Editor) — the READ/NAVIGATE surface (SC1 + the "view inherited fields" half of SC2).**

## Performance

- **Duration:** ~9 min
- **Started:** 2026-05-31 02:25Z
- **Completed:** 2026-05-31 02:34Z
- **Tasks:** 2 (both auto)
- **Files:** 7 (3 created, 4 modified) across both repos

## Accomplishments

- **`FormObjectTemplateEditor` (+ Designer)** — a `UtinniForm, IEditorForm` cloned in shape from `FormDatatableEditor`. Title `Object Template Editor` with leading `● ` dirty marker; default 1200×760, minimum 900×560; persists `[ObjectTemplateEditor] width/height/findReplaceVisible/showInheritedRows/looseOverrideDir`. Theme strictly via `Colors.*()` (zero `Color.FromArgb`; `Color.Red` is the only raw literal, used for unresolved-base + failure emphasis).
- **Four-column effective-view grid** — a `ThemedDataGridView` configured per UI-SPEC §Columns with the Phase 11 deviations (`AllowUserToOrderColumns=false`, `MultiSelect=false`/`SelectionMode=FullRowSelect`, only `Effective value` is `AutoSizeMode=Fill`). Rows bind directly from `EffectiveField` (the grid keeps the dark token map but supplies its own `CellFormatting` because `BindMutable` is datatable-typed). Origin overlays: local-override Origin cell at `Colors.Secondary()`; inherited rows (Field+Value+Type+Origin) at `Colors.FontDisabled()` + italic; unresolved-base Origin/value/type at `Color.Red`.
- **Background DERV-chain resolve + breadcrumb** — `LoadDocument` parses → typed `MutableObjectTemplate` → runs `ObjectTemplateResolver` on a `Task` and marshals back to bind. The breadcrumb renders root→…→this with the terminal `this` at `Colors.Secondary()` and any unresolved segment at `Color.Red` with `(unresolved)`. The open NEVER blocks (D-01 LOCKED): when no client-root TRE archive index is buildable, resolution degrades via a null-locator so local params still show + are navigable.
- **Editor-local undo/redo + Show-inherited toggle** — `ObjectTemplateEditController` wired with `EditApplied → OnEditApplied` (re-resolve + re-bind + counters); Undo/Redo toolbar buttons + Ctrl+Z/Ctrl+Y caught at the form (CON-M-05: zero scene undo/redo-manager coupling). `Show inherited` (default ON, persisted) hides inherited rows view-only.
- **Singleton hide-not-dispose** — `FormClosing` delegates to `SingletonFormClosePolicy.ShouldHideInsteadOfDispose` (cancel + `Hide()` on `UserClosing`).
- **`OtHandoffPolicy` (framework)** — `ShouldOfferObjectTemplateEditor` (.iff extension visibility gate) + `IsObjectTemplatePayload` (click-time `LooksLikeObjectTemplate` content sniff; never throws on a malformed payload). Mirrors `DatatableHandoffPolicy`.
- **5th SubPanel registration** — `Plugin.cs` adds `new FormObjectTemplateEditor(this)` in a try/catch with the honest log-on-failure; `GetSubPanels()` stays `return null` (SPI NOT widened — CON-M-01/02, T-11-10).
- **Two hand-offs** — IFF Editor `Switch to typed object-template view` (visible only when the visible mutable root looks like an object template; `OpenFromMutableIff`, no re-parse) + TRE Browser `Open in Object Template Editor` (off-UI-thread `TryResolve` → `IsObjectTemplatePayload` content gate → `OpenFromTreEntry`). Both HIDDEN (not disabled) when the sniff fails.

## Task Commits

1. **Task 1: FormObjectTemplateEditor host + OtHandoffPolicy framework gate** — UtinniPlugins `bc32f12` (feat) + Utinni `484211c` (feat, the framework-side hand-off policy dependency)
2. **Task 2: 5th SubPanel registration + TRE Browser and IFF Editor hand-offs** — UtinniPlugins `0504dfa` (feat)

_Cross-repo: the WinForms host + hand-off wiring live in UtinniPlugins; the unit-testable framework hand-off policy lives in Utinni (`UtinniCoreDotNet/UI`), matching the `DatatableHandoffPolicy`/`SingletonFormClosePolicy` precedent. Standing cross-repo write authority — no human checkpoint (only the live-SWG smoke needs one)._

## Decisions Made

- **Own-row grid bind (not `BindMutable`).** `ThemedDataGridView.BindMutable` is hard-coupled to `MutableDataTableDocument`; the OT editor binds its own `DataGridViewRow`s from `EffectiveField` (stamping the field on the row `Tag`) and supplies its own `CellFormatting` for the local/inherited/unresolved overlays. The grid still inherits the dark token map (it IS a `ThemedDataGridView`). This is the cleanest reuse without widening the shared grid's API.
- **Graceful resolution by default.** Production base resolution wires to a lazily-built, once-cached `TreArchiveIndex` from the resolved client root (`ResolveViaArchive`). When no client root / archive set is buildable (offline loose-file editing, no live client), resolution uses `Resolve(doc, _ => null)` so inherited bases mark unresolved and the open still succeeds — the D-01 LOCKED "never block the open" contract. Full archive-backed inheritance is exercised live in 11-04/11-05.
- **Status-only stubs for Plan-04 surfaces.** Save▾, Promote, Revert, and Reload buttons exist and are present in the toolbar, but their click bodies set a status line ("wired in the next plan") rather than mutating — per the plan's explicit staging. The reload-button copy already carries the LOCKED CF-05 candor wording. The grid is `ReadOnly` as a surface (inline value editing is 11-04).
- **Mutable-tree sniff in FormIffEditor.** The framework `LooksLikeObjectTemplate` takes a read-only `IffChunk`; the IFF Editor holds a `MutableIffDocument`, so a small private `LooksLikeObjectTemplate(MutableIffNode)` mirrors it (DERV child OR a digit-tagged version form with a 4-byte leading count chunk). HIDDEN-when-false, never throws.

## Deviations from Plan

None — plan executed exactly as written. No `./CLAUDE.md` exists in the working tree (confirmed by RESEARCH/PATTERNS). No auto-fixes (Rules 1-3) or architectural escalations (Rule 4) were needed. The own-row grid bind (vs `BindMutable`) is taken inside the plan's interface guidance ("bind `EffectiveField` rows to `ThemedDataGridView`") — `BindMutable` is datatable-typed and was never the intended path for the OT effective view.

## Issues Encountered

- **`ThemedDataGridView.BindMutable` is datatable-only.** It accepts `MutableDataTableDocument` and projects datatable cell types — unusable for the OT effective view. Resolved by binding own `DataGridViewRow`s + a form-local `CellFormatting` (the grid's own overlay formatter is keyed to datatable cell state, so it stays inert for OT rows). No change to the shared grid.
- **`LooksLikeObjectTemplate(IffChunk)` vs `MutableIffNode`.** The framework sniff takes the read-only `IffChunk` (used by the TRE Browser path via `OtHandoffPolicy.IsObjectTemplatePayload`, which parses the resolved payload). The IFF Editor holds a mutable tree, so a parallel mutable-node sniff was added inline there.
- **Generated/UtinniCore.cs regen churn** (CppSharp reorders on every build) was `git checkout --`'d after each build per the locked regen-churn rule; never committed.
- Git Bash CRLF-on-checkout warnings on the new `.cs` files are benign (repo `.gitattributes`/clang-format handle EOLs); no functional impact.

## Threat Surface

The plan's threat-register mitigations for this plan are covered:
- **T-11-08** (hand-off opens a non-OT / enumerate-only entry → parse failure / blank editor) — the TRE Browser hand-off content-gates on `OtHandoffPolicy.IsObjectTemplatePayload` (which runs `LooksLikeObjectTemplate` and returns false rather than throwing) after `TryResolve`; an enumerate-only entry returns `TryResolve==false` → "payload is enumerate-only" status. The IFF Editor switch is gated on the mutable-root sniff. Unresolved-base degradation (Plan 02) keeps the open non-blocking.
- **T-11-09** (a thrown exception during background resolve crashes the editor window) — the resolve runs in `Task.Run` and marshals via `BeginInvoke`; `ResolveEffectiveView` wraps the resolve in try/catch and degrades to a null-locator resolve on any failure (never throws into the UI). `Plugin.cs` registration is in try/catch so a construction failure disables only the editor (SPI preserved).
- **T-11-10** (widening the MEF SPI) — `GetSubPanels()` stays `return null`; the form is found by type via `GetForms()` — verified by the grep gate.
- **T-11-SC** (package installs) — N/A, zero new dependencies.

No new security-relevant surface beyond the planned disk-bytes/IFF-document → editor-open and WinForms-thread ↔ background-resolve boundaries already in the plan's threat model. No threat flags.

## Known Stubs

The following toolbar surfaces are intentionally status-only this plan and are resolved in **Plan 11-04** (mutations + widgets) / **Plan 11-05** (save + reload), per the plan's explicit staging (`<action>`: "Save-handler bodies, promote/revert mutation bodies, and the reload-badge text are wired in Plan 04"):

| Surface | File / handler | Resolved by |
|---------|----------------|-------------|
| `Save ▾` | `FormObjectTemplateEditor.OnSaveButtonClick` (status-only) | 11-04/11-05 |
| `Promote to override` | `OnPromoteClicked` (status-only) | 11-04 |
| `Revert to inherited` | `OnRevertClicked` (status-only) | 11-04 |
| `Reload in client` | `OnReloadClicked` (states the LOCKED CF-05 candor copy; no reload hook) | 11-05 (badge action) |
| Inline value editing | grid `ReadOnly = true` | 11-04 |

These do not prevent the plan's goal (SC1 + the "view inherited fields" half of SC2): the editor loads, renders the effective-inheritance view with origin overlays + breadcrumb, supports undo/redo + the Show-inherited toggle, and both hand-offs open templates into it. The Find/Replace pane controls exist (Designer) but the search wiring is also a 11-04 surface; `btnFind`/`btnReplace` enable on load but their pane logic is deferred (no false data rendered).

## User Setup Required

None — no external service configuration required.

## Verification Performed

- **Build (the load-bearing gate per Phase 8/9/10 precedent — WinForms-host logic is verified by an MSBuild-green TJT build, not by xUnit referencing the form):**
  - `UtinniCoreDotNet` Debug|x86 AND Release|x86 — clean (only the pre-existing CS0108 Generated/UtinniCore.cs warnings).
  - `TheJawaToolboxDotNet` Debug|x86 AND Release|x86 — clean (`-> …/Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll`), after BOTH Task 1 and Task 2.
  - VS2026 MSBuild (Dev18, `D:\Program Files\Microsoft Visual Studio\18\Community`).
- **Framework xUnit (regression):** `dotnet test UtinniCoreDotNet.Tests --no-build -c Debug` → **619 passed, 0 failed, 0 skipped** (same 11-02 baseline; `OtHandoffPolicy` is additive — no existing test changed, and the host/hand-off WinForms logic is not project-referenceable from the x86 framework test project per Phase 8/9/10 precedent).
- **Task 1 acceptance grep gates (all pass):**
  - `grep "FormObjectTemplateEditor : UtinniForm, IEditorForm"` → 1.
  - `grep -E "ThemedDataGridView|ObjectTemplateResolver"` → 6 (≥2).
  - `grep "Color.FromArgb"` (form + designer) → 0.
  - `grep "SingletonFormClosePolicy"` → 2 (≥1).
  - `AllowUserToOrderColumns` set to `false`; `FullRowSelect` present.
- **Task 2 acceptance grep gates (all pass):**
  - `grep "new FormObjectTemplateEditor(this)"` (Plugin.cs) → 1; `GetSubPanels() { return null; }` still present.
  - IFF hand-off `Switch to typed object-template view|OpenFromMutableIff` → 5 (≥2); visibility uses `LooksLikeObjectTemplate`.
  - TRE hand-off `Open in Object Template Editor|OpenFromTreEntry` → 11 (≥2); content gate `IsObjectTemplatePayload` (4) + `TryResolve` (7).
  - Both OT hand-off items toggle `.Visible` (HIDDEN when false), not `.Enabled`.
- **Regen churn:** `git checkout -- Generated/UtinniCore.cs` after every build; never committed.

## Next Phase Readiness

- **Ready for 11-04 (mutations + typed widgets + hex fallback):** the grid commit seam, the `ObjectTemplateEditController` wiring (`Apply(EditValue/AddOverride/RemoveOverride)`), and the Promote/Revert toolbar buttons are in place to be wired; `OnEditApplied` already re-resolves + re-binds; `EffectiveField.Origin` drives the promote-on-edit decision.
- **Ready for 11-05 (Save▾ modes + reload badge):** `Source` provenance is set on all three open paths (loose / TRE / IFF hand-off); the Save▾ button + reload button + the LOCKED CF-05 copy are present; `controller.MarkSaved()` is the save-success rebaseline hook.
- No blockers. CON-M-05 disentanglement verified (zero scene undo/redo-manager coupling in the host); MEF SPI preserved (GetSubPanels stays null — grep-gated).

## Self-Check: PASSED

All 3 created files (`OtHandoffPolicy.cs`, `FormObjectTemplateEditor.cs`, `FormObjectTemplateEditor.Designer.cs`) + the SUMMARY exist on disk; all three task commits (UtinniPlugins `bc32f12`, Utinni `484211c`, UtinniPlugins `0504dfa`) are present in their repos' git history.

---
*Phase: 11-tjt-subpanel-object-template-editor*
*Completed: 2026-05-31*
