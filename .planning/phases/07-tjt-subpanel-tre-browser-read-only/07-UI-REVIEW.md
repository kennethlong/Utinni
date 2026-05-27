# Phase 7 — UI Review

**Audited:** 2026-05-27
**Baseline:** 07-UI-SPEC.md (approved WinForms design contract)
**Screenshots:** not captured — WinForms desktop UI rendered inside an injected SWG client; no dev server / browser applies (code-only audit, ports 3000/5173/8080 all closed as expected). Live-smoke approvals are recorded in 07-02/07-03/07-04b SUMMARYs.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 2/4 | All five state strings match the contract verbatim, but the SPEC-mandated `Filter files…` search hint is entirely absent — the filter box ships empty with zero affordance. |
| 2. Visuals | 3/4 | Clear focal hierarchy (metadata header → accent banner → tree/table/hex) and consistent type cues, but the bottom-left status/legend stack is cramped (carried-over 07-02 defect) and the structured ListView header band is unthemed. |
| 3. Color | 4/4 | Zero `Color.FromArgb` literals in either audited file; every surface pulls from `Colors.*()`; accent (`Colors.Secondary()`) appears on exactly the declared 2px banner rule; `Color.Red` is confined to the parse-failure heading per contract. |
| 4. Typography | 2/4 | Banner + info heading use `10f` Bold — a point size the SPEC's type table never declares (contract says 8.25pt Bold), and the info-panel heading takes Bold that the SPEC reserves for banner + search-match only. |
| 5. Spacing | 3/4 | Metadata rows honour the 3px inset and 16px row pitch; splitter widths are the declared 4px. But the metadata strip uses a magic `Height = 150` with hand-tuned y-offsets (4/20/36/52/68/92/98) that don't map to the declared 4/8/16/24 scale, and the accent rule width is a hard 320px. |
| 6. Experience Design | 3/4 | Strong state coverage: loading, empty, decoding, encrypted, unsupported-raw, parse-failure, and overlay-unavailable are all handled, off-thread, with crash isolation. Gaps: no search placeholder, the metadata `Copy path/CRC` context menu is undiscoverable, and the type-facet combo is a non-functional stub. |

**Overall: 17/24**

---

## Top 3 Priority Fixes

1. **Missing search placeholder (`Filter files…`)** — BLOCKER for Copywriting contract conformance. The SPEC Copywriting Contract row "Search placeholder / hint = `Filter files…`" is unimplemented: `txtFilter` is a bare `UtinniTextbox` (no `PlaceholderText`, no cue-banner) so a first-time user sees an empty box above a 125k-node tree with no hint of its purpose. Fix: send `EM_SETCUEBANNER` to the textbox handle (WinForms .NET 4.7.2 `TextBox` has no managed `PlaceholderText`), or add a dimmed overlay `UtinniLabel` "Filter files…" cleared on focus/text. Lowest-risk: a one-line P/Invoke cue-banner helper on `UtinniTextbox` so Phases 8-11 inherit it.

2. **Banner/info-heading font size diverges from the typography contract** — WARNING. `TreDetailPane.cs:598` and `:749` set `new Font(Font.FontFamily, 10f, FontStyle.Bold)`. The SPEC Typography table declares the banner emphasis as **8.25pt Microsoft Sans Serif Bold** and lists no 10pt size anywhere; it also restricts Bold to "(a) a whole matching node in the filtered tree, (b) the type+version banner text." The info-panel heading ("No file selected", "Could not decode this file", etc.) is a third Bold consumer the contract didn't authorize. Fix: drop the explicit size so both inherit the 8.25pt base font, and differentiate the banner by the accent rule (as the SPEC intends) rather than by an off-contract point size. If a larger heading is genuinely wanted, amend the SPEC type table first.

3. **Cramped, unthemed bottom-left status/legend stack** — WARNING (acknowledged cosmetic from 07-02, still unaddressed in 07-03/07-04b). Two `Dock=Bottom` `UtinniLabel`s (`lblStatus`, `lblLegend`, each 18px) stack at the bottom of Panel1 with no inset/padding and overlap the tree's bottom edge; the SUMMARYs flag them as "cramped/hard to see." The legend carries the load-bearing overlay key ("Dimmed = on disk, not currently loaded") and the live path/match count — primary feedback that is currently hard to read. Fix: wrap both in a single bottom `Panel` with a 3px inset and a 2px top divider (`Colors.PrimaryShadow()`), giving the legend a stable, readable home.

---

## Detailed Findings

### Pillar 1: Copywriting (2/4)
The state-string contract is met almost exactly — this is the pillar's saving grace:
- Empty state: `"No file selected"` / `"Select a file in the tree to inspect its structure and contents."` (`TreDetailPane.cs:116`) — verbatim match.
- Tree loading: `"Loading archive index…"` (`FormTreBrowser.cs:173`) — match.
- Detail loading: `"Decoding…"` (`TreDetailPane.cs:123`) — match (body `"Reading the payload from the archive…"` is a sensible addition).
- Overlay legends: `"Dimmed = on disk, not currently loaded"` / `"Overlay unavailable — no live client"` (`FormTreBrowser.cs:208-209`) — verbatim match.
- Encrypted: `"Encrypted payload (v6000) — enumerate-only"` + the TreeFileExtractor.exe body (`TreDetailPane.cs:209`) — match.
- Parse failure: `"Could not decode this file"` + reason + "Other files are unaffected." (`TreDetailPane.cs:250`) — match.
- Type-facet default: `"All types"` (`FormTreBrowser.Designer.cs:102`) — match.

**BLOCKER — the one missing contract row:** SPEC mandates the search hint `Filter files…`. There is no `PlaceholderText`, cue-banner, or overlay label on `txtFilter` (`FormTreBrowser.Designer.cs:87-94`); `UtinniTextbox` (audited at `UtinniTextbox.cs`) has no placeholder facility. The single most-used control on the panel ships with no label. This is the dominant reason the pillar lands at 2 rather than 4 — every other string is correct, but the primary-interaction affordance copy is absent.

Minor: the 5000-version note from the contract (`"Recognized TRE version 5000 — layout not yet verified; enumerate-only."`) is intentionally and correctly omitted — 07-02 reclassified 5000 as a readable format, so that string would now be a lie (documented in 07-03 SUMMARY). Not a finding; called out so it isn't mistaken for a gap.

### Pillar 2: Visuals (3/4)
- **Focal hierarchy is clear and correct:** metadata header (Dock.Top, 150px) → 2px `Colors.Secondary()` accent rule → Bold type/version banner → three resizable sections (chunk tree / structured table / raw hex). The accent rule + banner is the intended visual anchor and it reads as one.
- **Type cues are consistent:** every leaf carries a bracketed text tag (`[IFF] [TAB] [STF] [TMPL] [MESH] [SKEL] [ANIM] [SHDR] [-]`, `FormTreBrowser.cs:648-670`) — the SPEC's "pick ONE mechanism, text tag is the lower-risk default" is honoured exactly.
- **Loaded/dimmed overlay** gives a second hierarchy axis (full `Colors.Font()` vs `Colors.FontDisabled()` leaves, `FormTreBrowser.cs:502-509`) — good use of value contrast instead of accent.
- **WARNING — cramped status/legend** (see Top Fix 3): the bottom-left label stack has no inset and is documented as hard to read.
- **WARNING — unthemed ListView column-header band:** `lvStructured`/`lvFiltered` bodies are themed dark (`Colors.PrimaryHighlight()`), but WinForms renders the native `View.Details` column-header band in the OS light chrome — a visible light strip in a dark tool. This is a documented WinForms limitation (07-04b SUMMARY) and would require owner-draw to fix; acceptable for V1 but it is a real visual seam, so the pillar does not reach 4.
- No icon-only buttons exist (text tags + text menu items throughout), so the "icon-only needs a label" check is N/A — a point in the implementation's favour.

### Pillar 3: Color (4/4)
- **Zero hardcoded ARGB in scope.** Grep for `Color.FromArgb` across both audited files returns nothing in `FormTreBrowser.cs` and nothing in `TreDetailPane.cs`. (The `Color.FromArgb` hits elsewhere in the repo are pre-existing SubPanels — `FreeCamPanel.Designer.cs` etc. — outside this phase's surface.) The SPEC's hardest color rule — "No raw `Color.FromArgb` literals in the browser code" — is fully satisfied.
- **Accent is reserved correctly.** `Colors.Secondary()` appears exactly once as content styling: the 2px banner accent rule (`TreDetailPane.cs:589-592`). It is NOT applied to ordinary labels, borders, dividers, or buttons — matching the SPEC's explicit "never all interactive elements" list. (`cbTypeFacet` uses `Colors.Primary()`, not accent, `FormTreBrowser.cs:107`.)
- **Surfaces follow the 60/30/10 mapping:** `Colors.Primary()` for backgrounds/panels (dominant), `Colors.PrimaryHighlight()` for the raised data surfaces (tree, lists, hex, search), `Colors.Font()`/`Colors.FontDisabled()` for the text value/dimmed split.
- **Destructive color is contract-confined:** `Color.Red` appears once (`TreDetailPane.cs:462`), on the parse-failure heading only, applied conditionally via the `isError` flag — exactly the SPEC's "ONLY for the parse-failure error heading" rule. There are no destructive actions (read-only phase), so no other red.
- This pillar is the cleanest in the phase; the rule set was specific and the implementation followed it without deviation.

### Pillar 4: Typography (2/4)
- **Monospace exception is correct:** `txtHex.Font = new Font("Consolas", 9f)` (`TreDetailPane.cs:689`) matches the SPEC's "9pt Consolas (monospace)" hex-view exception exactly.
- **Search-match bold is correct:** filtered leaves get `BoldFont()` = `new Font(tvTre.Font, FontStyle.Bold)` (`FormTreBrowser.cs:620-627`), inheriting the tree's 8.25pt base — a contract-clean Bold use, the right one of the two the SPEC authorizes.
- **WARNING — off-contract point size:** the banner (`TreDetailPane.cs:598`) and the info-panel heading (`:749`) both use `10f` Bold. The SPEC Typography table declares the banner as **8.25pt** Bold and lists no 10pt role anywhere. WinForms `AutoScaleMode.Font` means a hard 10f also fights DPI scaling the SPEC's Accessibility note explicitly warns against ("do not hard-code pixel sizes that fight DPI scaling").
- **WARNING — a third Bold consumer:** the SPEC restricts Bold to "(a) matching tree node, (b) type/version banner." The info-panel heading ("No file selected", "Could not decode this file", "Decoding…", "Encrypted payload…") is a third Bold surface (`:749`) the contract did not authorize. It's defensible UX, but it is a divergence from the stated two-use rule and should be reconciled — either amend the SPEC or drop the heading to regular weight differentiated by the `Color.Red`/`Colors.Font()` already in place.
- Net: the two correct exceptions (Consolas, tree-match bold) keep this off the floor, but two of the contract's typography rules (declared sizes only; Bold reserved to two uses) are each broken once, in two places.

### Pillar 5: Spacing (3/4)
- **Inset convention honoured:** metadata keys/values sit at x=3 / x=140 (`TreDetailPane.cs:616-617`), matching the SPEC's 3px-inset canonical gutter and the x=3/x=140 label/value columns called for in the Layout Contract.
- **Row pitch matches:** metadata rows at y = 4/20/36/52/68 are a consistent 16px pitch (`TreDetailPane.cs:581-585`) — the declared `md` token for detail-section row pitch.
- **Splitter widths correct:** both `SplitContainer`s use `SplitterWidth = 4` (`TreDetailPane.cs:706,719`; `FormTreBrowser.Designer.cs:72`) — the declared `xs`/splitter token.
- **WARNING — magic strip height + hand-tuned offsets:** `pnlMeta.Height = 150` (`TreDetailPane.cs:579`) is a magic number, and the accent/banner offsets jump to y=92 then y=98 (`:590,:595`) — an 8px gap that doesn't fall on the 4/8/16/24 scale and leaves a ~30px dead band below the banner inside the 150px strip. The strip height isn't derived from its content.
- **WARNING — hard 320px accent width:** `accent.SetBounds(3, 92, 320, 2)` (`TreDetailPane.cs:590`) hard-codes a 320px rule width (it does anchor L+R, so it stretches, but the design intent of "thin top accent rule" against a resizable pane would be cleaner docked). Minor.
- The structured/hex/note label heights (16/18/24px) are within the documented WinForms control-metric exceptions, so not flagged.

### Pillar 6: Experience Design (3/4)
- **State coverage is the strength:** every SPEC State-table row is implemented — Loading-tree (`FormTreBrowser.cs:173`), No-selection empty (`ShowEmpty`), Loading-detail (`ShowDecoding`), Encrypted/enumerate-only (`ShowEncrypted`), Parse-failure (`ShowParseFailure`), Overlay-unavailable (`FormTreBrowser.cs:207-209`) — plus a fourth distinct non-readable state the SPEC implied but didn't tabulate: unsupported-but-readable-raw (`ShowUnsupportedRaw`), which correctly shows real bytes instead of mislabeling a readable non-FORM payload as encrypted (review item 12). Crash isolation is real: parse/IO exceptions are caught per-file (`TreDetailPane.cs:139-148`, `FormTreBrowser.cs:441-447`) so one bad file never takes the panel down — the SPEC's explicit requirement.
- **Threading is correct:** heavy enumeration off-thread via `Task.Run` with UI applied on the await continuation; per-selection resolve off-thread with `BeginInvoke` marshaling and an `IsHandleCreated` guard (`FormTreBrowser.cs:432-448`). Debounced 250ms filter with a 5000-match cap → flat ListView fallback (`dbFilter_Tick`) matches the SPEC's debounce + broad-filter-guard contract. No modal blocking — matches the SPEC.
- **WARNING — discoverability of `Copy path` / `Copy CRC`:** these live only on a right-click `UtinniContextMenuStrip` on the metadata panel (`TreDetailPane.cs:602-608`) with no visual hint that a context menu exists. The SPEC allows a context-menu affordance, but with no cursor/tooltip cue the read-only copy actions are effectively hidden. Consider a hint or a visible `UtinniButton` (the SPEC's accent-button option #4).
- **WARNING — type-facet combo is a non-functional stub:** `cbTypeFacet` ships with a single "All types" item and no `SelectedIndexChanged` handler (`FormTreBrowser.Designer.cs:96-105`, no wire-up in `FormTreBrowser.cs`). It is V1-optional per the SPEC, so this is not a contract break — but shipping a visible, themed, focus-takable combo that does nothing is a dead control that will read as broken to a user. Either hide it for V1 or wire the asset-extension-group filter the SPEC describes.
- **Minor — missing search placeholder** also lands here (primary-interaction discoverability), already counted as Top Fix 1.
- Keyboard/tab order (search → facet → tree → detail) is satisfied by the Designer TabIndex sequence (0/1/2/3) and native TreeView navigation.

---

## Registry Safety

Not applicable. No `components.json`, no shadcn, no third-party UI registry (SPEC Registry Safety section: "not applicable — WinForms; no shadcn/component registry"). All UI is built from in-repo themed WinForms controls (`UtinniCoreDotNet.UI.Controls`) and BCL `System.Windows.Forms` types. Registry audit skipped per the auditor's shadcn-gated rule.

---

## Files Audited
- `D:/Code/Utinni/.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-UI-SPEC.md` (baseline contract)
- `D:/Code/Utinni/.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-CONTEXT.md` (locked decisions)
- `D:/Code/Utinni/.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-02-SUMMARY.md`, `07-03-SUMMARY.md`, `07-04b-SUMMARY.md`
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs` (detail pane: metadata header, accent banner, chunk tree, structured ListView, hex peek, all states)
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (browser window: tree, filter, overlay, AfterSelect dispatch)
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.Designer.cs` (layout, spacing, control wiring)
- `D:/Code/Utinni/UtinniCoreDotNet/UI/Theme/Colors.cs` (design tokens — grounded Color pillar)
- `D:/Code/Utinni/UtinniCoreDotNet/UI/Controls/UtinniTextbox.cs`, `UtinniLabel.cs` (themed control set — grounded Typography/Color/placeholder findings)
