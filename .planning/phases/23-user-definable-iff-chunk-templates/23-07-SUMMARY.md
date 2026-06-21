---
phase: 23-user-definable-iff-chunk-templates
plan: 07
subsystem: tjt-ui
tags: [iff, template-builder, tier-b, hex-driven, dec-c4, d-02, d-16, pitfall-8, mef-safe, binary-compat, ui-only, cross-repo]

# Dependency graph
requires:
  - phase: 23-user-definable-iff-chunk-templates
    plan: 03
    provides: "KernelCodec.Decode/Encode (byte-exact) + FitChecker.FitCheck -> FitReport + TypePlausibility"
  - phase: 23-user-definable-iff-chunk-templates
    plan: 04
    provides: "TemplateResolver.Resolve (version-FORM match + D-05 altitude + D-07 fit) + TemplatePackStore (D-12 scanned allow-list, DefaultRoots/LoadEffective) + TemplateJson (de)serializer"
  - phase: 08-tjt-subpanel-iff-editor-read-write
    provides: "FormIffEditor host (currentLeaf MutableIffNode, GetPayloadCopy/SetPayload, IffEditController, btnHex/btnText toggle pattern, txtHex/HexDump, Colors.*(), UtinniContextMenuStrip)"
provides:
  - "TemplateBuilderPane: the Tier-B hex-driven builder UserControl (raw-byte grid + decoded FieldRecord list + live byte-exact round-trip indicator) calling the headless engine — owns NO format logic (D-16)"
  - "FormIffEditor Template mode: a 4th leaf-pane MODE (btnTemplateMode) with select->assign, field-list edits, inline value edits through IffEditController, and auto-apply surfacing (silent green / red does-not-fit / multi-match picker / built-in altitude disable)"
  - "FormAssignFieldDialog: the small Assign-field modal (Name, Type, the 3 D-10 repeat kinds, the explicit enum-vs-flags radio per Pitfall 4, encoding) producing a FieldRecord"
affects: [23-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "The pane is UI-ONLY (D-16): every decode/fit-check delegates to KernelCodec/FitChecker; the in-progress template is the SAME D-01 TemplateModel the verbs emit — a legitimate non-exception to DEC-V2-VERBS-FIRST (an interaction, not a batch capability)"
    - "Two undo stacks, clear boundary (UI-SPEC undo decision): document/value edits ride the EXISTING IffEditController (Ctrl+Z/Y free); template-SHAPE edits (assign/remove/reorder) get a builder-local List<FieldRecord> snapshot stack"
    - "Pitfall 8 LOCKED: pnlTemplate Dock.Fill added front-most; nested splitTemplate sets Size BEFORE SplitterDistance with a try/catch; the ctor never throws (MEF silent-reject guard)"
    - "The grid selection -> byte range mapping (CaptureSelection) makes offsets SELECTIONS not arithmetic (D-02); a selection landing in the offset column or ASCII gutter clamps to whole bytes"
    - "Auto-apply rides TemplateResolver.Resolve: SuppressedByBuiltin -> disable toggle + altitude tooltip; Best.Fits -> silent Template mode; key-match-no-fit -> Template mode + red flag; genuine tie -> picker (most-specific pre-selected)"

key-files:
  created:
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TemplateBuilderPane.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormAssignFieldDialog.cs"
  modified:
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.Designer.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj"

key-decisions:
  - "btnTemplateMode is added LAST among the Dock.Right cluster (not literally 'first' as the plan's grep step worded it) because Dock.Right reverse-adds: first-added docks rightmost, last-added docks leftmost. Adding it last achieves the UI-SPEC's actual goal ('renders LEFTMOST of the three') — the same reverse-add semantics the existing toolbar documents. The plan's automated 'added before the other toggles' wording is mechanically inconsistent with 'leftmost'; the rendered outcome (leftmost) is the binding contract."
  - "FormAssignFieldDialog is code-built (no Designer/.resx) — a small modal with no image resources, which avoids the MSB3823 resx-image build path the repo runbook flags and keeps the dialog one grep-able file"
  - "A repeat-spec selection promotes the field to KernelType.Array and records the picked element kind as a single StructFields element shape, so the engine has an element type to repeat (the engine owns the actual decode)"
  - "Template clone-on-apply via TemplateJson round-trip (Serialize->Deserialize) so authoring edits never mutate the shared pack-loaded TemplateModel instance — reuses the D-01 artifact, no hand-rolled field copy"
  - "EnsureTemplatesLoaded + the whole auto-apply path are fail-open (try/catch -> empty list / raw hex): a missing or throwing pack dir, or an unreachable stable-id, never breaks leaf selection — the leaf simply opens in Hex with the Template toggle still available"

requirements-completed: [PROD-IFFT-03]

# Metrics
duration: 10min
completed: 2026-06-21
---

# Phase 23 Plan 07: Tier-B In-Place Hex-Driven Template Builder Summary

**The Tier-B in-place hex-driven template BUILDER (D-02, the heart of the phase) as the fourth leaf-pane MODE of FormIffEditor (DEC-C4 — inside The Jawa Toolbox): `TemplateBuilderPane` renders a raw-byte monospace grid (top) + the decoded FieldRecord list (bottom) + a continuous byte-exact round-trip indicator (status strip), all driven by the headless 23-03/04 engine (KernelCodec/FitChecker/TemplateResolver) — the pane owns NO format logic (D-16). The `btnTemplateMode` toggle wires select->assign (the FormAssignFieldDialog modal with the 3 D-10 repeat kinds + the explicit enum-vs-flags radio per Pitfall 4), field-list add/remove/reorder, inline value edits driving `leaf.SetPayload(KernelCodec.Encode(...))` through the EXISTING IffEditController, and the full auto-apply surfacing: silent green / red does-not-fit / multi-match picker / built-in altitude disable. Pitfall-8 compliant, MEF-safe (ctor never throws), binary-compat (no defaulted [Caller*] params). Cross-repo: code landed in UtinniPlugins under standing write authority; this SUMMARY + planning state in Utinni.**

## Performance
- **Duration:** ~10 min
- **Started:** 2026-06-21T04:37:52Z
- **Completed:** 2026-06-21T04:47:45Z
- **Tasks:** 2
- **Files modified:** 5 (2 created, 3 modified) — all in the sibling UtinniPlugins repo

## Accomplishments
- **Task 1 — TemplateBuilderPane:** a 624-line UserControl. `pnlTemplate` (Dock.Fill, added front-most) contains `pnlTemplateStatus` (Dock.Bottom, 22px, 3px pad — the live indicator) + a nested `splitTemplate` (Orientation=Horizontal, Size set BEFORE SplitterDistance, try/catch'd) with the raw-byte grid (txtBytes, Consolas 9pt, reusing the FormIffEditor HexDump format) on top and the decoded `FieldRecord` list (lvFields: name/type/value/span) on the bottom. `SetPayload(bytes, model)` re-runs `KernelCodec.Decode` + `FitChecker.FitCheck` and repaints; the indicator renders the three UI-SPEC states verbatim (green "Round-trip OK · M of M bytes consumed" via the one sanctioned `Color.FromArgb(78, 201, 76)`, neutral "N of M consumed — R bytes still raw" in FontDisabled, red "Template doesn't fit these bytes — decode stopped at offset 0x{off}"). `CaptureSelection()` maps a grid TextBox selection back to a byte range (offsets are selections, not arithmetic — D-02). `FieldValueCommitRequested` surfaces inline value edits to the host. Enum/flags decoration renders `raw(name)` / `raw(walk|run)` with the Pitfall-4 bit-POSITION semantics.
- **Task 2 — FormIffEditor wiring + FormAssignFieldDialog:** added `btnTemplateMode` (50px, Dock.Right, leftmost of the three toggles) + the `templateBuilderPane` (Dock.Fill, front-most) to the Designer; a `templateModeActive` flag + `SetTemplateMode` (commit on Leave). Interaction 1 (select->assign): the byte grid's UtinniContextMenuStrip "Assign type & name…" opens `FormAssignFieldDialog` (Name, Type drop-down, the 3 D-10 repeat kinds [Fixed count / Count from field… / Until end of chunk], the EXPLICIT "Enum (one value)" vs "Flags (combinable, bit 1..32)" radio, encoding) → appends a `FieldRecord` in file byte order. Interaction 2: Remove / Move up / Move down field rows (mirroring the tree verbs) + inline value edit decoding through the template, coercing the scalar, re-encoding via `KernelCodec.Encode`, and applying through the EXISTING `IffEditController` (with the D-10 length-change feedback). Interaction 3 (auto-apply): `TemplateResolver.Resolve` → built-in disables the toggle with the altitude tooltip; a green FitReport opens Template mode silently; a key-match-no-round-trip opens with the red flag; a genuine tie shows the multi-match picker (most-specific pre-selected). Template-SHAPE edits get a builder-local undo stack (two stacks, clear boundary).
- **Build + MEF:** the TJT solution builds clean at x86 Release (both `TheJawaToolbox.dll` + `TheJawaToolboxDotNet.dll` produced) against a freshly-rebuilt `UtinniCoreDotNet.dll`; the Phase-17 frozen-DLL MEF-compose gate (`FrozenPluginComposeTests`) is GREEN (1 passed). Color grep-gate: `Color.FromArgb(78, 201, 76)` appears exactly once and no other raw ARGB literals exist in the new/changed files (only `Colors.*()` / `Color.Red` / the one green).

## Task Commits
Each task was committed atomically in the UtinniPlugins repo (cross-repo standing authority):
1. **Task 1: TemplateBuilderPane — Tier-B hex-driven builder pane** - `3727274` (feat)
2. **Task 2: wire Template mode into FormIffEditor — select→assign, auto-apply** - `93065dd` (feat)

**Plan metadata:** committed separately in the Utinni repo (this SUMMARY + STATE/ROADMAP/REQUIREMENTS).

## Files Created/Modified
- `UI/Controls/TemplateBuilderPane.cs` (new) — the Tier-B builder pane (raw-byte grid + field list + live round-trip indicator), calls the headless engine, Pitfall-8 + MEF-safe
- `UI/Forms/FormAssignFieldDialog.cs` (new) — the Assign-field modal producing a FieldRecord (3 D-10 repeat kinds + the explicit enum-vs-flags radio + encoding)
- `UI/Forms/FormIffEditor.cs` — Template-mode wiring: SetTemplateMode, the assign/field-row context menu, OnTemplateFieldValueCommit (IffEditController bridge), ConfigureTemplateModeForLeaf auto-apply, the multi-match picker, the two-stack undo, stable-id derivation
- `UI/Forms/FormIffEditor.Designer.cs` — btnTemplateMode + templateBuilderPane controls, Dock.Right cluster + pnlLeafEditor Fill ordering
- `TheJawaToolboxDotNet.csproj` — registered the two new files

## Decisions Made
See the `key-decisions` frontmatter. Load-bearing: (1) btnTemplateMode is added LAST to render leftmost (Dock.Right reverse-add semantics) — the plan's "added before" grep wording is inconsistent with its own "leftmost" goal, so the rendered outcome is the binding contract; (2) the Assign-field dialog is code-built (no .resx) to dodge the MSB3823 image-resx build path; (3) clone-on-apply via TemplateJson round-trip so authoring never mutates the shared pack instance; (4) the whole auto-apply path is fail-open so a bad pack dir never breaks leaf selection.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] btnTemplateMode add-order corrected to achieve UI-SPEC "leftmost"**
- **Found during:** Task 2
- **Issue:** The plan's automated verify says "grep confirms btnTemplateMode added before the other Dock.Right toggles," but a Dock.Right control added FIRST docks RIGHTMOST (WinForms reverse-add). Following the literal wording would render btnTemplateMode rightmost — the opposite of the UI-SPEC requirement "to the LEFT of the existing two … renders leftmost of the three."
- **Fix:** Added btnTemplateMode LAST among the Dock.Right cluster (after btnTextMode + btnHexMode), matching the same reverse-add semantics the existing toolbar block documents, so it renders leftmost as the UI-SPEC requires. Documented inline in the Designer.
- **Files modified:** `UI/Forms/FormIffEditor.Designer.cs`
- **Commit:** `93065dd`

**2. [Rule 3 - Blocking] Rebuilt UtinniCoreDotNet so the TJT reference resolves the 23-03/04/05 engine**
- **Found during:** Task 1 (build setup)
- **Issue:** TJT references `..\..\..\Utinni\bin\$(Configuration)\UtinniCoreDotNet.dll`; the pane consumes KernelCodec/FitChecker/TemplateResolver/TemplateJson which landed in this Utinni repo across 23-03/04/05. The committed bin DLL had to carry those types for the cross-repo build to compile.
- **Fix:** Rebuilt `UtinniCoreDotNet.csproj` (x86 Release) before building TJT (the plan's verify already calls for the "paired UtinniCore build"); `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs` after each build per the CppSharp regen-churn rule.
- **Files modified:** none committed (build artifact only; Generated churn reverted)
- **Commit:** n/a (no source change)

## Authentication Gates
None.

## Issues Encountered
- **Cross-repo build dependency:** the verify requires both the paired UtinniCore build (so the bin DLL exports the template types) and the TJT solution build. Ran inline on the main tree per the worktrees-off posture; restored `Generated/UtinniCore.cs` after each UtinniCoreDotNet build.
- **MEF-compose gate is engine-direction:** `FrozenPluginComposeTests` composes a COMMITTED pre-built TJT DLL against the freshly-built UtinniCoreDotNet to catch a broken binding ABI. This plan touched ZERO UtinniCoreDotNet surface (TJT-side consumer only), so the gate stays green by construction; the freshly-built TJT DLL also compiles + would compose (no throwing ctor, no breaking signature, no defaulted [Caller*] params).

## Known Stubs
None blocking the plan goal. Two intentional Tier-B scope edges, both consistent with the plan/UI-SPEC:
- Inline value editing supports scalar fields (int/float/double/string); byte[]/struct/array element editing via the simple text box returns null (no document mutation) and is a deferred follow-up — array/struct shape editing is authored via the assign dialog's repeat-spec, not inline cell text.
- Save/manage of the authored template (Save to pack / import / export / overwrite / delete confirmations) is explicitly 23-08's scope (next wave); this plan ships the in-progress template held in `currentTemplate` (the D-01 artifact) ready for 23-08 to persist.

## Threat Flags
None. No new network endpoints, auth paths, or trust boundaries. Zero new NuGet packages (T-23-SC accept). The user-authored template is bounded by the engine (T-23-07-ENG mitigated: UI calls the kernel only); the over-read guard surfaces as the red does-not-fit flag (T-23-07-OOB mitigated); the MEF ctor cannot throw and no binary-incompat signature change was made (T-23-07-MEF mitigated, frozen-DLL gate green).

## Self-Check: PASSED
- FOUND: D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TemplateBuilderPane.cs
- FOUND: D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormAssignFieldDialog.cs
- FOUND: D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs
- FOUND commit: 3727274 (Task 1)
- FOUND commit: 93065dd (Task 2)
- FOUND: 23-07-SUMMARY.md

---
*Phase: 23-user-definable-iff-chunk-templates*
*Completed: 2026-06-21*
