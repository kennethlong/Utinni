---
phase: 23-user-definable-iff-chunk-templates
plan: 08
subsystem: tjt-ui
tags: [iff, template-pack, save-select-import-export, dec-c4, d-12, mef-safe, live-smoke, cross-repo, byte-exact, pitfall-4]

# Dependency graph
requires:
  - phase: 23-user-definable-iff-chunk-templates
    plan: 05
    provides: "TemplatePackStore writable-pack write semantics + scanned allow-list (D-12) + LooseOverridePath root-containment idiom"
  - phase: 23-user-definable-iff-chunk-templates
    plan: 07
    provides: "TemplateBuilderPane (Tier-B hex-driven builder, currentTemplate D-01 artifact) + FormIffEditor Template mode (auto-apply, clone-on-apply)"
  - phase: 23-user-definable-iff-chunk-templates
    plan: 04
    provides: "TemplateJson (de)serializer + TemplateResolver.Resolve"
  - phase: 08-tjt-subpanel-iff-editor-read-write
    provides: "FormSaveConfirmDialog (per-call modal, Color.Red body emphasis, explicit accept verb); FormIffEditor host + IffEditController"
provides:
  - "TemplateBuilderPane pack-management surface: a status-strip Save▾ drop-down — Save template to pack… (per writable pack: User templates / Project pack; Shipped shown disabled, read-only), Select a template… (lists scanned packs, applies one to the current leaf), Import template pack… / Export this template… (OpenFileDialog/SaveFileDialog)"
  - "Save serializes the current TemplateModel via TemplateJson into the chosen pack dir; the write path resolves WITHIN the pack root via LooseOverridePath (T-23-08-PATH); shipped pack rejected at the store"
  - "Overwrite + delete route through FormSaveConfirmDialog with the exact UI-SPEC headings/bodies/accept verbs (T-23-08-DESTRUCT); applying/editing stay non-destructive until explicit Save▾"
  - "FormIffEditor TemplateSelected wiring → clone-on-apply onto the current leaf"
  - "Maintainer live-smoke PASS: the full Tier-B feature (create→assign→green→edit→save→byte-exact re-encode + silent auto-apply on re-open) verified end-to-end in a live injected SWGEmu session — the one manual UAT 23-VALIDATION.md flags (no CI UIA harness for a WinForms SubPanel)"
affects: [24]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pack management is UI-ONLY over the headless store (D-16): Save serializes via TemplateJson into a TemplatePackStore-resolved writable dir; the write path resolves within the pack root via the same LooseOverridePath containment predicate every other editor reuses (T-23-08-PATH) — zero new path defense"
    - "Destructive ops (overwrite an existing same-key template / delete a template) reuse the existing FormSaveConfirmDialog per-call modal with the EXACT UI-SPEC copy + Color.Red body emphasis (T-23-08-DESTRUCT); nothing touches disk until an explicit Save▾ — applying/editing remain non-destructive and undoable"
    - "Import/export use the stock OpenFileDialog/SaveFileDialog (no new file widget); the shipped pack is shown disabled (read-only) in the menu AND rejected at the store — defense in depth"
    - "TemplateSelected → clone-on-apply (TemplateJson round-trip Serialize→Deserialize) so applying a selected pack template never mutates the shared pack-loaded instance — the same clone-on-apply idiom 23-07 established"

key-files:
  created: []
  modified:
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TemplateBuilderPane.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs"

key-decisions:
  - "Save▾ pack-management drop-down lives on the pane's status strip (next to the round-trip indicator) rather than the FormIffEditor toolbar — keeps the whole build→manage interaction self-contained in the Tier-B pane (D-16), so a future host that embeds the pane elsewhere carries its management surface with it"
  - "The shipped pack is defended in two places (disabled menu item + store-side rejection of a shipped-root write) rather than relying on the UI alone — a programmatic save attempt to the read-only pack still fails closed (T-23-08-PATH)"
  - "TemplateSelected applies via clone-on-apply (TemplateJson round-trip), reusing the exact idiom 23-07 uses for currentTemplate, so a selected pack template and a freshly-built one follow the identical apply path"

patterns-established:
  - "Cross-repo close-out: the feature code (Task 1) lands in the sibling UtinniPlugins repo under standing write authority; the SUMMARY + planning state land in the Utinni repo — the same split 23-07 used"

requirements-completed: [PROD-IFFT-03]

# Metrics
duration: ~22h elapsed (Task 1 ~auto, then blocking maintainer live-smoke checkpoint spanning the human-verify gate)
completed: 2026-06-21
---

# Phase 23 Plan 08: Template Management from the IFF Editor + Live-Smoke Sign-Off Summary

**Template PACK management as the management half of PROD-IFFT-03 (DEC-C4 — inside The Jawa Toolbox): a status-strip Save▾ drop-down on `TemplateBuilderPane` — Save template to pack… (per writable pack: "User templates" / "Project pack ({name})"; "Shipped (read-only)" shown disabled), Select a template… (lists scanned packs in load-order and applies one to the current leaf via clone-on-apply), Import template pack… / Export this template… (OpenFileDialog/SaveFileDialog). Save serializes the current `TemplateModel` via `TemplateJson` into the chosen pack dir, resolving WITHIN the pack root via `LooseOverridePath` (T-23-08-PATH); overwrite + delete route through the existing `FormSaveConfirmDialog` with the verbatim UI-SPEC copy + Color.Red emphasis (T-23-08-DESTRUCT); applying/editing stay non-destructive until an explicit Save▾. Then the one manual UAT 23-VALIDATION.md flags — a maintainer live-smoke of the FULL Tier-B feature in a live injected SWGEmu session — which the maintainer APPROVED (PASS): create→assign→green→edit→byte-exact save→silent auto-apply on re-open, byte-exact round-trip, no sibling-chunk corruption.**

## Performance
- **Duration:** Task 1 was a fast autonomous UI extension (~auto); the plan then spanned a blocking maintainer live-verify checkpoint (the human-verify gate) before this close-out.
- **Tasks:** 2 (1 auto + 1 maintainer live-smoke checkpoint, PASS)
- **Files modified:** 2 — both in the sibling UtinniPlugins repo (`TemplateBuilderPane.cs` +440 lines, `FormIffEditor.cs` +15 lines)

## Accomplishments
- **Task 1 — Pack save / select / import / export UI (committed `55515e9` in UtinniPlugins):** extended `TemplateBuilderPane` with a status-strip **Save▾** pack-management drop-down (+440 lines):
  - **Save template to pack…** lists one sub-item per scanned WRITABLE dir (D-12): "User templates", "Project pack ({name})"; the shipped pack shows the disabled "Shipped (read-only)" item. Save serializes the current built `TemplateModel` via `TemplateJson` into the chosen pack dir; the write path resolves WITHIN the pack root via `LooseOverridePath` (the same containment idiom every other editor reuses — **T-23-08-PATH** mitigated; shipped pack also rejected at the store).
  - **Select a template…** lists templates across the scanned packs (load-order) and applies one to the current leaf via clone-on-apply (TemplateJson round-trip, never mutating the shared pack instance).
  - **Import template pack…** = OpenFileDialog → copy into the user/project pack; **Export this template…** = SaveFileDialog. No new file widget.
  - **Overwrite** an existing same-key template and **delete** a template both route through the existing `FormSaveConfirmDialog` with the EXACT UI-SPEC headings/bodies/accept verbs ("Overwrite", "Delete"; body emphasis `Color.Red`) — **T-23-08-DESTRUCT** mitigated. Applying a template + editing values stay non-destructive (undoable; nothing on disk until an explicit Save▾).
  - **FormIffEditor** wires `TemplateSelected` → clone-on-apply onto the current leaf.
  - Colors: only the sanctioned StatusGreen + `Color.Red`; MEF-safe (ctor never throws). The TJT solution built clean at x86 Release; the Phase-17 frozen-DLL MEF-compose gate (`FrozenPluginComposeTests`) is GREEN.
- **Task 2 — Maintainer live-smoke (APPROVED / PASS):** the maintainer built + injected into a live SWGEmu session and drove TJT end-to-end. Using `scene/game_music_manager.iff` (root FORM **GMUS** — not claimed by any built-in, so its leaves are template-eligible), the maintainer:
  1. Carved a NUL-terminated C-string template over the **WATR** leaf (`"sound/amb_seashore_outside.snd\0"`, 31 bytes) → the round-trip indicator went green **"Round-trip OK · 31 of 31 bytes consumed"**.
  2. Edited the value inline → it stayed green with the payload-size feedback.
  3. **Save template to pack → User templates** succeeded; re-opening the chunk **auto-applied silently (green)**.
  4. **Saved the document** and re-opened the saved file → the edited field **round-tripped byte-exact** with **no sibling-chunk corruption**.

  This closes criterion 3 (create / edit / save / select from the IFF Editor UI) and the PROD-IFFT-03 manual gate that the headless test matrix cannot cover (WinForms SubPanel, no CI UIA harness — T-23-08-LIVE accept disposition).

## Task Commits
Task 1 was committed atomically in the **UtinniPlugins** repo (cross-repo standing authority):
1. **Task 1: template pack save/select/import/export from the IFF Editor** — `55515e9` (feat) — `TemplateBuilderPane.cs` (+440), `FormIffEditor.cs` (+15)
2. **Task 2: maintainer live-smoke** — no commit (maintainer-only live verification; result APPROVED / PASS recorded here)

**Plan metadata:** committed separately in the **Utinni** repo (this SUMMARY + STATE/ROADMAP/REQUIREMENTS + the deferred-items residual).

## Files Created/Modified
- `UI/Controls/TemplateBuilderPane.cs` (modified, +440) — the Save▾ pack-management surface: save (TemplateJson into a writable pack, within-root via LooseOverridePath), select-and-apply, import/export, overwrite/delete via FormSaveConfirmDialog
- `UI/Forms/FormIffEditor.cs` (modified, +15) — TemplateSelected → clone-on-apply onto the current leaf

## Decisions Made
See the `key-decisions` frontmatter. Load-bearing: (1) the Save▾ drop-down lives on the pane's status strip, keeping the build→manage interaction self-contained in the Tier-B pane (D-16); (2) the shipped pack is defended in two places (disabled menu item + store-side rejection) so a programmatic write still fails closed; (3) TemplateSelected applies via clone-on-apply (TemplateJson round-trip), reusing the 23-07 idiom so selected and freshly-built templates follow one apply path.

## Deviations from Plan
None — plan executed as written. Task 1 delivered the exact pack-management surface the plan specified (the UI-SPEC copy strings, FormSaveConfirmDialog reuse, OpenFileDialog/SaveFileDialog for import/export, within-root save) and Task 2's maintainer live-smoke returned PASS.

## Authentication Gates
None.

## Issues Encountered
None during planned work. One non-blocking UX residual surfaced during the live-smoke (maintainer-acknowledged) — logged below and to the phase deferred-items surface, NOT a blocker.

## Live-Smoke UX Residual (non-blocking, deferred)
The byte-grid selection is a tolerant TextBox text-selection whose visual highlight can span the offset
column + the ASCII gutter. It is **functionally correct** — `CaptureSelection`/`CaretToByteIndex` clamp
the captured range to whole bytes (the offsets-are-selections D-02 contract), so the assigned span is
always the right bytes. The polish ask is presentational only: add a **"selected: 0xNN–0xNN (N bytes)"**
readout and/or restrict the visual highlight to the hex columns so the user can SEE exactly which bytes
are captured. Logged as a Phase-23 residual in `deferred-items.md` (the "byte-grid selection readout"
item). Maintainer-acknowledged as non-blocking; PROD-IFFT-03 is met as shipped.

## Known Stubs
None. The 23-07 inline-edit scope edge (byte[]/struct/array element editing via the simple text box is
a deferred follow-up) is unchanged by this plan and remains a documented Tier-B scope edge, not a stub
that blocks the plan goal.

## Threat Flags
None. No new network endpoints, auth paths, or trust boundaries beyond those in the plan's threat model.
Zero new NuGet packages (T-23-SC accept). The two write boundaries are both mitigated as planned:
imported-pack copy + template save resolve WITHIN the writable pack root via LooseOverridePath
(T-23-08-PATH); overwrite/delete route through FormSaveConfirmDialog (T-23-08-DESTRUCT). The live
injection smoke is the accepted maintainer-only checkpoint (T-23-08-LIVE) — completed PASS.

## Next Phase Readiness
Phase 23 (user-definable IFF chunk templates) is COMPLETE — all 8 plans shipped and PROD-IFFT-01/02/03
delivered (the manual create→edit→save→byte-exact gate is now maintainer-verified). Next is Phase 24
(client entry-point advertisement, `GetEngineHookPoints`), which remains gated on external swg-client-v2
readiness per the v2.1 roadmap.

## Self-Check: PASSED
- FOUND commit: 55515e9 (Task 1, UtinniPlugins) — `feat(23-08): template pack save/select/import/export from the IFF Editor`
- FOUND: D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TemplateBuilderPane.cs (modified in 55515e9)
- FOUND: D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs (modified in 55515e9)
- Task 2 live-smoke: APPROVED / PASS (maintainer-only, no commit — recorded above)
- FOUND: 23-08-SUMMARY.md (this file)

---
*Phase: 23-user-definable-iff-chunk-templates*
*Completed: 2026-06-21*
