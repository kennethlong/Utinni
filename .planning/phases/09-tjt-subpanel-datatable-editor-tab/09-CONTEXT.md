# Phase 9: TJT subpanel — Datatable Editor (`.tab`) - Context

**Gathered:** 2026-05-28
**Status:** Ready for planning

<domain>
## Phase Boundary

A **typed view + edit surface for `.tab` datatables** (FORM `DTII` IFF — version FORM `0000`/`0001` with `COLS`/`TYPE`/`ROWS` chunks), shipped as an `IEditorPlugin` WinForms SubPanel **inside The Jawa Toolbox** (per DEC-C4). The first Wave-1 phase to layer a **typed grid UX** on the IFF read/write primitives that Phase 8 shipped in `UtinniCoreDotNet/Formats/Iff/`. Replaces the SOE-era `SwgDataTableTool` — and ships at **SOE-parity scope** (T4 — full schema mutation: cells, rows, columns, types).

**In scope:** typed datatable model + writer (`UtinniCoreDotNet/Formats/Datatable/`, sibling to `Formats/Iff/`); typed DataGridView editor SubPanel; full T4 schema mutation on **existing** `.tab` files (cell edits + row add/remove/reorder + column add/remove/reorder + column-type changes); strict edit-time validation with per-type cell widgets; type-change cascade via `DataTableColumnType::mangleValue()` with "needs review" flagging; column reorder/delete safety-net modal; CSV/TSV export + delta-import (per-cell diff preserves byte-exact-on-untouched); Find/Replace + column-click sort; three entry points (file picker, TRE Browser "Open in Datatable Editor", IFF Editor "Switch to typed datatable view" — manual hand-off, NOT auto-route); round-trip CLI verb + golden fixtures (Phase 8 D-02 pattern); save modes 1/2/4 from Phase 8 D-05 (loose override, Save/Save-As, `.tre` repack); mode 3 (in-memory live patch) stays DISABLED inherited from Phase 8; reload UX locked to Phase 8 D-06 tier-(b) "reloads on next scene change" — candid, no in-session datatable reload hook fabricated; editor-local undo/redo (Phase 8 D-08 pattern).

**Explicitly OUT of scope (deferred):** **new `.tab` from scratch** (empty-state schema designer — V2); **cross-table FK / "table corpus" subsystem** (dropdown pickers for `DT_HashString` resolution from referenced hashtables, dangling-FK warnings on save, engine-canonical FK mapping — V2; SOE's tool also lacked this); **engine-consumer scan** (grep swg-client-v2 / Utinni source for `getColumnNumber("...")` / `getIntValue(...,N)` to warn before column reorder/delete — possibly a V1.5 phase); **row filter** (column-click sort ships V1; live row-filter expression deferred); **DT_Comment row UX semantics** (whether the first comment row is hidden, locked, or freely editable — planner discretion).

</domain>

<decisions>
## Implementation Decisions

### Carried forward from Phase 8 (no re-decision needed)
- **CF-01 (← 08 D-01):** Format primitives (typed `DataTableDocument`, `DataTableWriter`, schema model) ship **framework-side** in `UtinniCoreDotNet/Formats/Datatable/`, sibling to `Formats/Iff/`. NOT in `TheJawaToolboxDotNet`. TJT consumes via the existing `UtinniCoreDotNet.dll` reference — same model as Phase 8's IFF primitives. No new public-API surface across plugins (honors DEC-C4 intent).
- **CF-02 (← 08 D-02):** Round-trip CLI verb + golden fixtures is the automated correctness gate. Phase 9 adds an analogous datatable round-trip verb (e.g. `utinni-cli roundtrip-tab`) that parses → mutates → serializes → re-parses and asserts byte-exact identity for **untouched cells/columns** (per CF-04). Same harness pattern as Phase 4 `inspect-iff`/`decode-iff` and Phase 8's IFF round-trip. **Automated gate for Success Criterion 4** ("no silent schema corruption").
- **CF-03 (← 08 D-05):** Save modes 1, 2, 4 are V1 (loose override, Save / Save-As, `.tre` repack). **Mode 3 (in-memory live patch) stays DISABLED** behind an honest tooltip inherited from Phase 8 — no `OpenSource.ClientMemory` provenance descriptor is constructed for `.tab` opens in V1. Reduced-mode acceptance (implementation-complete-and-unit-tested-for-bounds-gate, enabled by a future phase) is identical to Phase 8 D-05.3 reduced-mode.
- **CF-04 (← 08 D-07):** **Hybrid mutable DOM.** Each in-memory column/row retains its **original raw bytes** from the read. On save: untouched columns and untouched rows **emit their original bytes verbatim**; edited / added cells and rows emit fresh bytes; the `COLS`/`TYPE`/`ROWS` chunk lengths roll up from contents. Untouched-byte preservation is what makes SC4 (no schema corruption) **byte-exact** for unmodified columns. **CSV delta-import (D-08 below) honors this same property** — cells imported from CSV that match the original value are re-emitted as original bytes, not re-serialized.
- **CF-05 (← 08 D-06 tier (b)):** **Reload UX is locked to the tier-(b) "reloads on next scene change" wording.** Datatables have NO in-session reload hook in SWG (cross-AI-reviewer-locked in Phase 8 — codex + cursor both confirmed). The editor candidly tells the user the asset re-resolves on the next TJT-driven scene change (the existing chat-command parser callback path). The editor does NOT fabricate speculative scene-setup triggers via `AddSetSceneCallback` (notification hook, not a trigger). The reload-status badge text is locked; planner may NOT loosen it.
- **CF-06 (← 08 D-08):** **Editor-local undo/redo stack**, **independent** of Utinni's scene `UndoRedoManager`. Datatable edits are not scene state — same CON-M-05 disentanglement as Phase 8 IFF.
- **CF-07 (← DEC-C4 LOCKED):** Subpanel-inside-TJT (`IEditorPlugin` SubPanel registered in `TheJawaToolboxDotNet/Plugin.cs` `SubPanelContainer`). Not a separate plugin.
- **CF-08 (← memory `project_swg_iff_no_pad.md`):** IFF reader handles SWG's no-pad quirk (fixed in Phase 7 commit `7012d82`). Phase 9's typed model parses through the existing fixed `IffReader` — no special-case work needed.
- **CF-09 (← memory `feedback_winforms_dockfill_zorder.md`):** The Datatable Editor's `DataGridView` (main surface) docks **Fill and stays front-most** (added first / BringToFront, never SendToBack). Toolbar/status sibling docks Top and is added first. Nested `SplitContainer` if any multi-section layout is needed (Size before SplitterDistance). Phase 8's 08-04b precedent.

### Editing scope (Phase 9 ↔ V2 boundary)
- **D-01:** **V1 ships T4 — full schema mutation (SOE `SwgDataTableTool` parity)** on **existing `.tab` files only**.
  - **T4 operations:** edit existing cell values; add / remove / reorder rows; add / remove / reorder columns; **change column types**. Wider than PROD-W1-DT's acceptance text ("edit cell values, save back" — literal T1) by explicit founder decision: a one-stop modding tool needs SOE parity for the Wave-1 datatable surface.
  - **NOT V1: "new `.tab` from scratch"** (file → new → schema designer with empty rows). Deferred to V2 — adds a "schema designer" UX (define columns + types before any rows) that's its own UX project. Open-existing covers ~all real modder iterate loops.
- **D-02:** **Column reorder/delete safety net = s2 warn-only modal.** Before any column reorder or delete, the editor surfaces a once-per-session modal: *"This may break runtime consumers that read columns by index. Proceed?"* No engine-consumer scan in V1 (the heavier s3 option — grepping `sharedGame/*DataTable.cpp` for `getColumnNumber("colname")` / `getIntValue(...,N)` references — is deferred to V1.5 if reviewers push back on SC4). Honest UX over silent risk.

### Validation strictness
- **D-03:** **Strict edit-time validation + per-type cell widgets.**
  - `DT_Int` → numeric spinner (whole numbers)
  - `DT_Float` → numeric spinner with decimals
  - `DT_Bool` → checkbox
  - `DT_Enum` → dropdown sourced from the column's type-spec string (`e(a=0,b=1,c=2)[default]` — Assumption A1 corrected 2026-05-29 per RESEARCH Pitfall 2)
  - `DT_HashString` → text input + computed hash preview adjacent
  - `DT_String`, `DT_Comment` → free text
  - `DT_PackedObjVars`, `DT_BitVector` → text input with format syntax hint (planner discretion on validator depth)
  - Invalid input is **blocked at keystroke** (no `"abc"` in an Int cell).
- **D-04:** **Type-change cascade** (T4 column-type-edit only): when the user changes column N's type from X to Y, every existing cell in column N is run through `DataTableColumnType::mangleValue()` from the new type Y. Cells that fail the mangle (e.g. `"hello"` → `DT_Int`) get flagged **"needs review" (red)**. **Save is blocked while any "needs review" cells exist** — consistent strict UX with edit-time. User must resolve or revert the type change before saving.

### Cross-file references (FK / HashString)
- **D-05:** **One-doc-at-a-time. No table corpus subsystem in V1.** The editor opens a single `.tab` at a time; it does NOT maintain a corpus of related `.tab` / hashtable files for cross-reference resolution.
- **D-06:** **No FK / dangling-ref validation in V1.** `DT_HashString` cells with spec `h[hashtable/path]` edit as plain text + hash preview (per D-03); the editor does NOT validate that the referenced hashtable file exists, nor that the hashed value resolves inside it. Logical-FK `DT_Int` columns (row-ID-into-another-table by convention) have NO FK awareness at all. Dangling-FK detection, dropdown pickers for known FK targets, and engine-canonical FK maps are deferred to V2 (matches SOE `SwgDataTableTool` which also lacked them).

### Productivity / bulk-edit operations
- **D-07:** **Find / Replace across cells (V1).** Standard Ctrl-F / Ctrl-H. Find scope = visible cell values (all columns, all rows, ignores DT_Comment unless user opts in — planner discretion). Replace honors per-column type validation (D-03) — type-invalid replacements are blocked the same as direct edits.
- **D-08:** **CSV / TSV export + delta-import (V1).** Export is straightforward (write columns + rows + types as a header). **Import path computes a per-cell diff against the in-memory table; only cells whose imported value differs from the current value are marked dirty**. Cells that match are left as-is — their **original bytes (CF-04) are preserved**. This is what makes SC4 (byte-exact-on-untouched) survive a CSV round-trip. The CSV import surfaces a preview modal: *"N cells will change, M will stay original bytes; proceed?"*. CSV escape rules, encoding (UTF-8 with BOM), and DT_Comment row handling are planner discretion.
- **D-09:** **Column-click sort (view-only, V1).** Standard `DataGridView` sort-by-column-header. **View only — does NOT mutate the on-disk row order.** Save always serializes rows in their original (or user-edited) physical order. Row-filter (live filter expression over rows) is deferred — not V1.

### Entry points
- **D-10:** **Three entry points, V1 — manual hand-off only (no auto-route).**
  1. **File picker** (loose `.tab` from disk) — baseline.
  2. **TRE Browser "Open in Datatable Editor"** on a selected entry (when the entry is recognized as `.tab` by extension or its IFF root tag is `DTII`). Mirrors the Phase 8 "Open in IFF Editor" hand-off from `FormTreBrowser`.
  3. **IFF Editor "Switch to typed datatable view"** menu item, available only when the currently-loaded IFF's root tag is `DTII`. **Manual hand-off — not auto-route.** Preserves the IFF Editor's chunk-tree view as the user's choice (a user who specifically opens via IFF Editor wanted to see the raw chunks; we don't surprise-route them).
  - When the editor opens a `.tab` whose tier-(b) reload behavior applies (which is all of them), the reload-status badge displays the candid CF-05 wording from the moment the file loads.

### Claude's Discretion
- **Exact UI layout** of the editor SubPanel (toolbar layout, dirty-state indicator placement, CSV-import preview modal layout, type-change-cascade resolution UX) → defer to `/gsd-ui-phase 9` (UI-SPEC) and planner.
- **DT_PackedObjVars / DT_BitVector cell-widget depth** — text input with format syntax hint is the floor; a small inline validator (parse-back-check) is upside if cheap.
- **DT_Comment row UX semantics** — whether the first comment row is rendered as a frozen header (locked + visually distinct), hidden by default behind a toggle, or freely editable like any row. Planner picks; recommend frozen-header with toggle to edit, but not locked.
- **CSV serialization details** — separator (`,` for `.csv` / `\t` for `.tsv` by file extension), encoding (UTF-8 with BOM is the SOE convention), escape rules (Excel-compatible double-quote), DT_Comment row treatment on export, header row schema (column name only vs `name:type` annotation).
- **Find/Replace scope toggles** — column-scoped vs all-cells, case-sensitive, regex. V1 must have basic find/replace; toggles are planner discretion.
- **CLI verb naming/shape** — `utinni-cli roundtrip-tab` is a placeholder; planner picks consistent with Phase 4 / Phase 8 verb conventions.
- **Plan decomposition** — Phase 9 is large but more cohesive than Phase 8 (single typed format, no parallel save-mode risks like CON-N-04 live-patch). Expect 4–6 plans: (1) typed reader + DOM, (2) typed writer + round-trip CLI golden gate, (3) DataGridView editor SubPanel with typed cell widgets + entry points, (4) T4 schema mutation operations + safety modals, (5) CSV delta export/import + Find/Replace + sort, (6) live-SWG smoke + UAT polish. Planner has final say.

### Reviewed Todos (not folded)
- `gamecallbacks-gc-av-flake-fix.md` — CI-stability flake-fix in `GameCallbacksTests`; resolved in 06-04. Keyword match (`phase`, `swg`) was a false positive — same as Phase 7 and Phase 8 reviews. Unrelated to Datatable Editor scope.
- `loader-lock-harness-flake-fix.md` — CI-stability flake-fix for LoaderLockHarness 50ms threshold; resolved in 06-04 (memory `project_loader_lock_harness_ci_flake.md`). Keyword false positive. Unrelated.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 9 roadmap / project decisions
- `.planning/ROADMAP.md` § Phase 9 — goal, success criteria (1: subpanel loads in TJT, 2: open / view / edit cells / save, 3: live client picks up edit on relevant reload path, 4: schema preserved without silent corruption), preservation guard-rails (CON-M-01/02 SPI, CON-T-05 `*Impl` separation).
- `.planning/PROJECT.md` § Key Decisions — **DEC-C4** (subpanel-inside-TJT, LOCKED), **DEC-A3** (not-a-DCC-replacement, LOCKED — gates the deferred write-authoring milestone if datatable WRITE parity ever overlaps art-asset write parity, which it does NOT for datatables).
- `.planning/REQUIREMENTS.md` — **PROD-W1-DT** ("View and edit `.tab` datatables; replaces SOE-era `SwgDataTableTool`. Plugin loads in editor host; user can open a `.tab` file, view rows/columns, edit cell values, save back; live SWG client picks up the edit on the relevant reload path.") and **PROD-02** (Wave-1 edit aggregate). Note: PROD-W1-DT's acceptance text is T1; Phase 9 ships T4 (D-01) by explicit founder decision.

### Phase 8 carried-forward decisions (CF-* above)
- `.planning/phases/08-tjt-subpanel-iff-editor-read-write/08-CONTEXT.md` — **READ THIS FIRST.** D-01 (framework-side primitives), D-02 (round-trip CLI golden gate), D-05 (4 save modes; mode-3 disabled), D-06 (TIERED reload, datatable in tier (b)), D-07 (hybrid mutable DOM, byte-exact-on-untouched), D-08 (editor-local undo/redo), D-09 (shared chunk-tree control — N/A here; Phase 9 uses DataGridView).
- `.planning/phases/08-tjt-subpanel-iff-editor-read-write/08-REVIEWS.md` (if present) — cross-AI review record that locked D-05 V1-same-length-only and D-06 TIERED.
- `.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-CONTEXT.md` — D-02 (swg-client-v2 reference policy), D-08/D-13 (read-only IFF reader placement + extractable chunk-tree control).

### Existing Utinni assets to extend / reuse (this repo)
- `UtinniCoreDotNet/Formats/Iff/IffReader.cs`, `IffDocument.cs`, `IffChunk.cs`, `IffContainerChunk.cs`, `IffLeafChunk.cs`, `IffParseException.cs` — the read path Phase 9's typed model layers on. The reader handles SWG's no-pad quirk (CF-08); Phase 9 inherits this transparently.
- `UtinniCoreDotNet/Formats/Iff/IffWriter.cs` (+ mutable edit model from Phase 8 D-01/D-07) — Phase 9's typed writer composes on top of this for the actual chunk emit step. **Resolve the precise public API exposed by Phase 8's IffWriter before planning.**
- `Utinni.Cli/Commands/InspectIffCommand.cs`, `Utinni.Cli/Commands/DecodeIffCommand.cs`, Phase 8's round-trip-iff verb (`Utinni.Cli.Tests` golden fixtures) — the harness pattern Phase 9's roundtrip-tab verb (CF-02) follows.
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (+ `.Designer.cs`) — the Phase-7 browser the TRE Browser "Open in Datatable Editor" hand-off (D-10.2) originates from. Mirror the Phase 8 "Open in IFF Editor" wiring.
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs` — Phase 7 chunk-tree control. NOT directly reused (Phase 9 uses DataGridView, not chunk tree) but the dark-themed control patterns (themed ListView/TreeView/hex) transfer.
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/*IffEditor*` (Phase 8 deliverable; final filename per Phase 8 plans) — the IFF Editor SubPanel that hosts the "Switch to typed datatable view" menu (D-10.3).
- `UtinniCoreDotNet/UI/Controls/SubPanel.cs`, `SubPanelContainer.cs` — SubPanel base + container the Datatable Editor registers into.
- `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs` — the MEF SPI (CON-M-01/02).
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs` — `SubPanelContainer` registration site (where the Datatable Editor panel plugs in).
- `TheJawaToolboxDotNet.csproj` — references `UtinniCoreDotNet.dll` via `HintPath ..\..\..\Utinni\bin\$(Configuration)\UtinniCoreDotNet.dll` (the consumption path for CF-01 primitives).

### swg-client-v2 reference (read-only spec/impl, NOT a runtime dep — per memory `project_swg_client_v2_reference.md`)
- `../swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTable.h` — typed DataTable interface (rows, columns, get/set by name and by index). Reverse for the C# typed model.
- `../swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTable.cpp` — **AUTHORITATIVE IFF FRAMING**. Lines ~446–602: `enterForm(DTII)` → version FORM (`TAG_0000` / `TAG_0001`) → `enterChunk(COLS)` → `enterChunk(TYPE)` → `enterChunk(ROWS)`. Both V0 and V1 layouts are present.
- `../swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTableColumnType.h` (and `.cpp`) — the `DataType` enum (`DT_Int`, `DT_Float`, `DT_String`, `DT_Unknown`, `DT_Comment`, `DT_HashString`, `DT_Enum`, `DT_Bool`, `DT_PackedObjVars`, `DT_BitVector`), `getBasicType()`, `mangleValue()`, `getDefaultCell()`, `getTypeSpecString()`. **`mangleValue()` is the canonical type-coercion routine for D-04's type-change cascade — port its semantics faithfully.**
- `../swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTableCell.h` / `.cpp` — cell storage + per-type accessors. The C# typed model's cell representation reverses from this.
- `../swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTableWriter.h` / `.cpp` — **PORT REFERENCE for the typed writer (CF-01)**. The C++ engine has a real DataTableWriter — read it for chunk framing, type-spec serialization, and value-mangling on emit.
- `../swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTableManager.h` / `.cpp` — runtime cache + scene-change reload semantics. Confirms CF-05 tier-(b) "reloads on next scene change" — the manager has no in-session invalidation hook.
- `../swg-client-v2/src/engine/shared/application/DataTableTool/src/shared/DataTableTool.cpp`, `DataTableTool.h`, `FirstDataTableTool.cpp` — **The SOE-era CLI authoring tool**. Sanity-check Phase 9's serialization output against its golden fixtures (if the SOE tool's command-line shape can be invoked offline; otherwise treat as design reference).
- `../swg-client-v2/tools/swg_blender/swg_iff/reader.py`, `tags.py`, `writer.py` — Python-side IFF navigation. The DTII handling is the cross-check against the C++ canon above.
- `../swg-client-v2/docs/research/iff-tre-codebase-map.md` — index of `.tab` / IFF / TRE loaders/readers/writers. **Cross-reference for the framing primitives** (CF-01 / CF-02).

### Codebase intel (this repo)
- `.planning/codebase/STACK.md`, `STRUCTURE.md`, `ARCHITECTURE.md`, `CONVENTIONS.md`, `CONCERNS.md`, `INTEGRATIONS.md`, `TESTING.md` — repo-wide maps. CONCERNS.md and CONVENTIONS.md flag CON-M-01/02 (MEF SPI), CON-M-05 (UndoRedoManager scene cleanup — Phase 9 SteersClear per CF-06), CON-T-05 (`*Impl` separation), CON-N-04 (VirtualProtect bracket — N/A here since live-patch stays disabled per CF-03).

### Relevant memory (operator background; not for downstream agent inputs, but informs planner choices)
- `feedback_winforms_dockfill_zorder.md` — DataGridView docks Fill, stays front-most (CF-09).
- `feedback_caller_attrs_binary_compat.md` — adding NEW types to `UtinniCoreDotNet/Formats/Datatable/` is safe; do NOT change existing public signatures consumed by pre-built plugins without rebuilding them in the same commit.
- `project_swg_iff_no_pad.md` — IFF reader fixed; CF-08 inherits the fix.
- `project_swg_client_v2_reference.md` — locked reference corpus policy.
- `project_scene_change_via_tjt.md` + `project_tjt_scene_change_naked_baseline.md` — TJT chat-command-parser scene change is the reload trigger users will run after a `.tab` save. The "naked after scene change" is a pre-existing baseline, NOT a Phase 9 regression signal.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **Phase 8 `IffWriter` + mutable edit model (UtinniCoreDotNet):** Phase 9's typed writer composes on top of this. The hybrid mutable DOM pattern (CF-04 / Phase 8 D-07) gives Phase 9 byte-exact-on-untouched for free at the chunk level; Phase 9 extends it to **cell-level** byte preservation (untouched cells re-emit original bytes).
- **Phase 8 round-trip CLI verb pattern (Utinni.Cli + Tests goldens):** the harness pattern Phase 9 follows for the `.tab` round-trip golden (CF-02). Same fixture layout, same `Assert.Equal(original, roundtripped)` byte-exact gate.
- **Phase 8 "Open in IFF Editor" hand-off from `FormTreBrowser`:** Phase 9's TRE-Browser entry point (D-10.2) mirrors this exactly. Same selection-detection, same dispatch.
- **Phase 8 IFF Editor SubPanel + dirty-state indicator + Save/Save-As toolbar (TJT):** Phase 9's editor reuses the SubPanel host pattern, dirty-state convention, and toolbar layout — separately, not as a shared base class (a shared abstract editor base is its own refactor, not Phase 9 scope).
- **`Utinni.Cli.Tests` golden fixture infrastructure:** drop-in reuse for the Phase 9 `roundtrip-tab` goldens.
- **`SubPanel` / `SubPanelContainer` + Utinni themed controls:** the WinForms host surface + dark theme. Themed `DataGridView` is the new piece (Phase 7/8 used TreeView/ListView; planner picks the themed grid wrapper).

### Established Patterns
- `IEditorPlugin` MEF export → aggregation in `FormMain` (CON-M-01/02); `*Impl` separation (CON-T-05).
- **Hybrid mutable DOM with original-bytes preservation** (CF-04) is the **structural property that makes byte-exact-on-untouched survive even through CSV round-trip** (D-08 delta-import). Phase 9's cells need the same `originalBytes` field pattern.
- **`[CallerMemberName]` / binary-compat caution:** adding NEW types to `UtinniCoreDotNet/Formats/Datatable/` is safe; do NOT change existing public signatures consumed by pre-built plugins without rebuilding them in the same commit.
- **Round-trip golden test pattern:** parse → mutate → serialize → re-parse → byte-exact compare on untouched chunks. Phase 4 IFF goldens + Phase 8 IFF round-trip are the precedent.
- **TIERED reload-status badge UX** (CF-05) — Phase 9 datatables ALWAYS show tier-(b) "reloads on next scene change" wording. There is no datatable case that gets tier-(a) in-session reload.

### Integration Points
- New Datatable Editor SubPanel registers via TJT `Plugin.cs` `SubPanelContainer` (alongside Phase 7 TRE Browser + Phase 8 IFF Editor).
- TRE Browser "Open in Datatable Editor" hand-off wires from `FormTreBrowser` selection (extension `.tab` and/or IFF root tag `DTII` detection).
- IFF Editor "Switch to typed datatable view" menu item adds to the Phase 8 IFF Editor SubPanel (visible only when current IFF root is `DTII`).
- Loose-override save target reuses the same client-derived override directory the Phase 8 IFF Editor used (CF-03 mode 1).
- `.tre` repack save target reuses the same Phase 8 `TreWriter.Repack` path (CF-03 mode 4). Refuses V6000 archives (Phase 8 WR-06 fix).
- Forced in-session reload (CF-05) interacts with the **TJT-driven scene-change** path — see `project_scene_change_via_tjt.md`. Editor badge tells user explicitly that the asset reloads on the next scene change; does NOT trigger one.
- CLI round-trip verb (CF-02) adds to `Utinni.Cli` alongside `inspect-iff` / `decode-iff` / `roundtrip-iff` (Phase 8); goldens live alongside `Utinni.Cli.Tests` fixtures.

</code_context>

<specifics>
## Specific Ideas

- Replaces the **SOE-era `SwgDataTableTool`** at **feature parity for editing existing files** (T4 — D-01). The SOE tool's "spreadsheet of typed cells" mental model is the UX anchor.
- Datatables are the **most data-intensive** Wave-1 surface — real production tables like `CombatDataTable` have hundreds of rows and dozens of columns. Bulk operations (CSV delta-import, Find/Replace, sort) are first-class V1 productivity features (D-07 / D-08 / D-09), not deferred polish.
- **SC4 "no silent schema corruption" is structurally enforced**, not just tested:
  - CF-04 hybrid DOM preserves original cell bytes for untouched cells.
  - D-08 CSV delta-import preserves original bytes for cells whose imported value matches the current value.
  - CF-02 round-trip CLI verb is the automated gate (Phase 4 / Phase 8 precedent).
  - D-03 / D-04 strict edit-time + cascade prevent type-incoherent saves.
- **Tiered reload candor (CF-05) is non-negotiable** — locked by Phase 8's cross-AI review. Phase 9 inherits the locked wording.
- The **C++ engine ships a real `DataTableWriter`** (`sharedUtility/src/shared/DataTableWriter.cpp/h`) — Phase 9's typed writer ports its semantics directly. This significantly lowers the reverse-engineering risk vs Phase 8 (which had to derive `.tre` repack framing from `TreFile` family across multiple read-only loaders).

</specifics>

<deferred>
## Deferred Ideas

- **"New `.tab` from scratch"** — empty-state schema designer (define columns + types before any rows). Adds a schema-designer UX surface that's its own UX project. Deferred to V2 / dedicated phase.
- **Cross-table FK / "table corpus" subsystem** — dropdown pickers for `DT_HashString` cells resolving from referenced hashtables; dangling-reference warnings on save; engine-canonical FK conventions (e.g., `CombatDataTable.columnN → weapons.tab`) sourced from `sharedGame/*DataTable.cpp`. Deferred to V2; matches SOE `SwgDataTableTool` parity (their tool also had no cross-table awareness).
- **Engine-consumer scan** — grep `sharedGame/*DataTable.cpp` and `UtinniCore`/`UtinniPlugins` for `getColumnNumber("colname")` / `getIntValue(...,N)` references to surface a "1 engine consumer reads this column by index in `CombatDataTable.cpp:142`" warning before column reorder/delete (the s3 option from Area 1). Possibly V1.5 phase if reviewers push back on SC4 acceptance for T4 schema mutation; otherwise V2.
- **Live row filter** — text-box-filter-over-rows (BindingSource filter expression or virtual mode). Column-click sort ships V1 (D-09); the filter is the next step. Deferred to V2.
- **In-memory live patch for `.tab`** — Phase 8 mode-3 stays disabled inherited (CF-03). Wiring up an `OpenSource.ClientMemory` provenance descriptor for opened `.tab` files (so the live-patch menu item enables) is a follow-up enabler phase — same status as Phase 8 D-05.3 reduced-mode.
- **Art-asset WRITE / authoring parity** — N/A for datatables (CF-04 / D-01 deliver full datatable schema mutation in V1); the deferred milestone remains gated behind LOCKED DEC-A3 for mesh / skeleton / animation / shader.
- **ImGui chromeless HUD-overlay presentation** of the Datatable Editor — per memory `project_hud_style_overlay_directive.md` (UPDATED 2026-05-26: HUD = optional later polish for data browsers, NOT binding). WinForms SubPanel ships V1 per CF-07.
- **Shared "abstract editor base class"** across IFF Editor + Datatable Editor + future Phase 10/11 editors — tempting after Phase 9 makes the third editor SubPanel with shared toolbar/dirty-state/save-flow patterns. Refactor candidate post-Wave-1.

### Reviewed Todos (not folded)
- `gamecallbacks-gc-av-flake-fix.md` — CI-stability flake-fix, resolved in 06-04. Keyword false-positive match (same as Phase 7 + Phase 8). Unrelated to Datatable Editor.
- `loader-lock-harness-flake-fix.md` — CI-stability flake-fix, resolved in 06-04 (memory `project_loader_lock_harness_ci_flake.md`). Keyword false-positive match. Unrelated.

</deferred>

---

*Phase: 09-tjt-subpanel-datatable-editor-tab*
*Context gathered: 2026-05-28*
