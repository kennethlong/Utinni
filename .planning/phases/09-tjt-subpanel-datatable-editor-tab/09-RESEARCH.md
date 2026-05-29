# Phase 9: TJT subpanel — Datatable Editor (`.tab`) - Research

**Researched:** 2026-05-29
**Domain:** Typed datatable model + writer over EA-IFF-85 (`FORM DTII`), per-type WinForms `DataGridView` cell widgets, T4 schema mutation (cells + rows + columns + types), CSV delta export/import, editor-local undo/redo composed on Phase 8's hybrid mutable IFF DOM. Single-format phase — substantially lower reverse-engineering risk than Phase 7 (multi-version TRE) or Phase 8 (in-memory live-patch + `.tre` repack with CRC/TOC rebuild).
**Confidence:** HIGH for the DTII chunk framing, type-spec grammar, mangleValue port, Phase 8 framework primitives Phase 9 composes on, and the Phase 7/8 UI/CLI patterns Phase 9 mirrors. MEDIUM for CSV serialization details (no first-party SOE reference for the round-trip-preserving format; planner discretion per CONTEXT D-08). MEDIUM for `DataGridView` performance on `CombatDataTable`-scale tables (hundreds of rows × dozens of columns) without virtual mode — see Pitfall 7. MEDIUM-LOW for `DT_PackedObjVars` / `DT_BitVector` parse-back fidelity since neither has an in-tree fixture to validate against.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Carried forward from Phase 8 (no re-decision needed):**
- **CF-01 (← 08 D-01):** Format primitives (typed `DataTableDocument`, `DataTableWriter`, schema model) ship **framework-side** in `UtinniCoreDotNet/Formats/Datatable/`, sibling to `Formats/Iff/`. NOT in `TheJawaToolboxDotNet`. TJT consumes via the existing `UtinniCoreDotNet.dll` reference — same model as Phase 8's IFF primitives. No new public-API surface across plugins (honors DEC-C4 intent).
- **CF-02 (← 08 D-02):** Round-trip CLI verb + golden fixtures is the automated correctness gate. Phase 9 adds an analogous datatable round-trip verb (e.g. `utinni-cli roundtrip-tab`) that parses → mutates → serializes → re-parses and asserts byte-exact identity for **untouched cells/columns** (per CF-04). Same harness pattern as Phase 4 `inspect-iff`/`decode-iff` and Phase 8's `roundtrip-iff`. **Automated gate for Success Criterion 4** ("no silent schema corruption").
- **CF-03 (← 08 D-05):** Save modes 1, 2, 4 are V1 (loose override, Save / Save-As, `.tre` repack). **Mode 3 (in-memory live patch) stays DISABLED** behind an honest tooltip inherited from Phase 8 — no `OpenSource.ClientMemory` provenance descriptor is constructed for `.tab` opens in V1. Reduced-mode acceptance (implementation-complete-and-unit-tested-for-bounds-gate, enabled by a future phase) is identical to Phase 8 D-05.3 reduced-mode.
- **CF-04 (← 08 D-07):** **Hybrid mutable DOM.** Each in-memory column/row retains its **original raw bytes** from the read. On save: untouched columns and untouched rows **emit their original bytes verbatim**; edited / added cells and rows emit fresh bytes; the `COLS`/`TYPE`/`ROWS` chunk lengths roll up from contents. **CSV delta-import honors this same property** — cells imported from CSV that match the original value are re-emitted as original bytes, not re-serialized.
- **CF-05 (← 08 D-06 tier (b)):** **Reload UX is locked to the tier-(b) "reloads on next scene change" wording.** Datatables have NO in-session reload hook in SWG (cross-AI-reviewer-locked in Phase 8). The editor candidly tells the user the asset re-resolves on the next TJT-driven scene change. The editor does NOT fabricate speculative scene-setup triggers via `AddSetSceneCallback` (notification hook, not a trigger). The reload-status badge text is locked; planner may NOT loosen it.
- **CF-06 (← 08 D-08):** **Editor-local undo/redo stack**, **independent** of Utinni's scene `UndoRedoManager`. Datatable edits are not scene state — same CON-M-05 disentanglement as Phase 8 IFF.
- **CF-07 (← DEC-C4 LOCKED):** Subpanel-inside-TJT (`IEditorPlugin` SubPanel registered in `TheJawaToolboxDotNet/Plugin.cs` `SubPanelContainer`). Not a separate plugin.
- **CF-08 (← memory `project_swg_iff_no_pad.md`):** IFF reader handles SWG's no-pad quirk. Phase 9's typed model parses through the existing fixed `IffReader` — no special-case work needed.
- **CF-09 (← memory `feedback_winforms_dockfill_zorder.md`):** The Datatable Editor's `DataGridView` (main surface) docks **Fill and stays front-most** (added first / BringToFront, never SendToBack). Toolbar/status sibling docks Top and is added first. Nested `SplitContainer` if any multi-section layout is needed (Size before SplitterDistance).

**Editing scope (Phase 9 ↔ V2 boundary):**
- **D-01:** **V1 ships T4 — full schema mutation (SOE `SwgDataTableTool` parity)** on **existing `.tab` files only**. T4 operations: edit existing cell values; add / remove / reorder rows; add / remove / reorder columns; **change column types**. Wider than PROD-W1-DT's acceptance text ("edit cell values, save back" — literal T1) by explicit founder decision. NOT V1: "new `.tab` from scratch."
- **D-02:** **Column reorder/delete safety net = s2 warn-only modal.** Before any column reorder or delete: *"This may break runtime consumers that read columns by index. Proceed?"* (once-per-session). No engine-consumer scan in V1.

**Validation strictness:**
- **D-03:** **Strict edit-time validation + per-type cell widgets.** `DT_Int` → numeric spinner; `DT_Float` → decimal spinner; `DT_Bool` → checkbox; `DT_Enum` → dropdown from type-spec; `DT_HashString` → text + hash preview; `DT_String`/`DT_Comment` → free text; `DT_PackedObjVars`/`DT_BitVector` → text + format hint. Invalid input **blocked at keystroke**.
- **D-04:** **Type-change cascade** runs `mangleValue()` on every cell in the changed column; failures flagged **"needs review" (red)**; **save blocked while any "needs review" cells exist**.

**Cross-file references (FK / HashString):**
- **D-05:** **One-doc-at-a-time. No table corpus subsystem in V1.**
- **D-06:** **No FK / dangling-ref validation in V1.** `DT_HashString` cells edit as plain text + hash preview; no cross-file resolution. Logical-FK `DT_Int` columns have NO FK awareness.

**Productivity / bulk-edit operations:**
- **D-07:** **Find / Replace across cells (V1).** Standard Ctrl-F / Ctrl-H; honors per-column type validation.
- **D-08:** **CSV / TSV export + delta-import (V1).** Per-cell diff; only cells whose imported value differs from the current value are marked dirty; matching cells preserve original bytes (CF-04). Surfaces a preview modal: *"N cells will change, M will stay original bytes; proceed?"*.
- **D-09:** **Column-click sort (view-only, V1).** Standard `DataGridView` header sort. **View only — does NOT mutate the on-disk row order.**

**Entry points:**
- **D-10:** **Three entry points, V1 — manual hand-off only.** (1) File picker; (2) TRE Browser "Open in Datatable Editor" (mirrors Phase 8's "Open in IFF Editor"); (3) IFF Editor "Switch to typed datatable view" menu item, visible only when root tag is `DTII` — **manual hand-off, NOT auto-route**.

### Claude's Discretion
- **Exact UI layout** of the editor SubPanel → defer to 09-UI-SPEC (already approved 2026-05-28).
- **DT_PackedObjVars / DT_BitVector cell-widget depth** — text input with format syntax hint is the floor; inline validator (parse-back) is upside if cheap.
- **DT_Comment row UX semantics** — UI-SPEC assumption #8 picks the floor (frozen-header row with toggle to edit).
- **CSV serialization details** — separator (`,`/`\t` by extension), encoding (UTF-8 with BOM is SOE convention), Excel-compatible escape, DT_Comment row treatment, header schema.
- **Find/Replace scope toggles** — basic find/replace is required; match-case / regex / include-comments are planner discretion.
- **CLI verb naming/shape** — `roundtrip-tab` is a placeholder; planner picks.
- **Plan decomposition** — CONTEXT advisory: 4–6 plans (typed reader+DOM / typed writer+CLI / DataGridView editor / T4 ops / CSV+find / smoke+UAT). Planner has final say.

### Deferred Ideas (OUT OF SCOPE)
- **"New `.tab` from scratch"** — schema designer UX. V2.
- **Cross-table FK / "table corpus" subsystem** — dropdown pickers, dangling-FK warnings, engine-canonical FK maps. V2 (SOE's tool also lacked this).
- **Engine-consumer scan** — grep `sharedGame/*DataTable.cpp` for `getColumnNumber()` / `getIntValue(...,N)`. V1.5 if reviewers push back on SC4, else V2.
- **Live row filter** — BindingSource filter / virtual mode. V2 (sort ships V1).
- **In-memory live patch for `.tab`** — Phase 8 mode-3 stays disabled inherited (CF-03).
- **Art-asset WRITE / authoring parity** — N/A for datatables (CF-04 / D-01 deliver full datatable schema mutation in V1); the deferred art milestone remains gated behind DEC-A3.
- **ImGui chromeless HUD-overlay presentation** — optional later polish (per memory `project_hud_style_overlay_directive` UPDATED 2026-05-26: WinForms SubPanel ships V1).
- **Shared "abstract editor base class"** across IFF Editor + Datatable Editor + Phase 10/11 — refactor candidate post-Wave-1.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PROD-W1-DT | View and edit `.tab` datatables; replaces SOE-era `SwgDataTableTool`. Plugin loads in editor host; user can open a `.tab` file, view rows/columns, edit cell values, save back; live SWG client picks up the edit on the relevant reload path. | DTII parser + typed `DataTableDocument` (§ Architecture Patterns, Pattern 1); `DataTableWriter` hybrid-DOM byte-preservation (§ Pattern 2); per-type `DataGridView` cell widgets (§ Pattern 3); CSV delta-import (§ Pattern 5); CF-05 tier-(b) reload UX (§ Reload Architecture). PROD-W1-DT's literal acceptance text is T1 ("edit cell values, save back") — Phase 9 ships T4 per CONTEXT D-01. |
| PROD-02 | Wave-1 edit aggregate (contributes; closes at Phase 11). | Phase 9 is the third Wave-1 editor; reuses Phase 8 framework primitives (`MutableIffDocument`, `IffWriter`, `OpenSource`, `IffSaveTargets`, `ClientReloadDispatcher`, `TreRepackSaveTarget`, `LooseOverridePath`, `TreBackupPath`, `TreRepackLock`, `IffEditController` pattern). |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Repository-level `CLAUDE.md` not present at repo root (the user-memory `MEMORY.md` carries Utinni-specific operator guidance and is loaded automatically). Cross-cutting constraints that apply to Phase 9 work, drawn from `MEMORY.md` and `.planning/codebase/CONVENTIONS.md` / `CONCERNS.md`:

- **VS 2026 + v145 PlatformToolset is the default; VS 2022 fallback exists.** Builds via `D:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe`. `dotnet build` MUST NOT be used to build WinForms projects (MSB3823 on image `.resx`); use VS2026 MSBuild instead. Run xUnit via `dotnet test --no-build`. (Mechanism behind `workflow.use_worktrees=false`.)
- **GSD worktrees OFF for this repo** (`workflow.use_worktrees=false`). Run all C++ build waves INLINE on the main tree (fresh worktree lacks `vcpkg_installed`). Phase 9 is pure C# managed work, but the worktree-off discipline still applies because cross-repo paired commits to `D:/Code/UtinniPlugins` need a stable working tree.
- **UtinniPlugins write authority is standing** for the sibling `kennethlong/UtinniPlugins` repo at `D:/Code/UtinniPlugins`. Cross-repo paired commits do NOT need human-action checkpoints (only the live-SWG smoke does).
- **Singleton-form hide-not-dispose pattern is MANDATORY** for ANY plugin-registered `GetForms()` editor from the start (smoke-discovered Phase 8 defect; Phases 9/10/11 must apply from the start per Phase 8 SUMMARY 8-05 / STATE). On `CloseReason.UserClosing`, cancel close + `Hide()` instead of disposing. Editor-host shutdown reasons (`ApplicationExitCall` / `WindowsShutDown`) fall through normally. Without this, the SECOND open of any singleton form throws `ObjectDisposedException` at `Form.CreateHandle`.
- **WinForms Dock.Fill MUST be front-most** (memory `feedback_winforms_dockfill_zorder`). Add the Fill control FIRST in `Controls.Add` order; do NOT `SendToBack` it. CF-09 codifies this for Phase 9.
- **No DXSDK June 2010.** Retired in Phase 6 (Plan 06-03). Use `DirectXMath` from Windows SDK. Phase 9 is pure C# managed and does not touch graphics — N/A here, but the rule stays.
- **Allman braces, 4-space indent, MIT-license file header** for both C++ and C# new files (`.planning/codebase/CONVENTIONS.md`). The 23-line MIT header block (`/** ... **/` doubled-asterisk close) is mandatory verbatim.
- **`// ToDo` (no colon)** is the canonical TODO marker; `// TODO`/`// FIXME`/`// HACK` are NOT used.
- **`[CallerMemberName]` is NOT binary-compat** for existing public methods (memory `feedback_caller_attrs_binary_compat`). Adding NEW types in `UtinniCoreDotNet/Formats/Datatable/` is safe; do NOT change existing public signatures (e.g. `IffReader.Read`, `IffWriter.Write`, `MutableIffDocument.FromDocument`) consumed by pre-built plugins without rebuilding the plugins in the same commit.
- **CON-T-05 `*Impl` separation** — any native↔managed split keeps the public-API class in `UtinniCoreDotNet` and the implementation in an `Impl` partner. For Phase 9 the format primitives are pure managed; no native bridge needed.
- **CON-M-01/02 MEF SPI** — `IEditorPlugin` + `IPlugin` shape MUST NOT widen. Phase 9 registers `FormDatatableEditor` inside the existing `GetForms()` list in `Plugin.cs`, same `try/catch` isolation block as Phase 7/8.
- **STAB-04 preservation guard-rails** — Phase 9 must not break any of CON-N-01..-09, CON-M-01..-09, CON-T-01..-05. The 23-item audit (`UtinniCoreDotNet.Tests/PreservationAudit/`) is a fail-on-violation gate.
- **Old-style .csproj coverage (round-2 HIGH-A)** — `TheJawaToolboxDotNet.csproj` is explicit-compile (no SDK-style glob). EVERY new `.cs` production file MUST have an explicit `<Compile Include>` entry. WinForms `.cs` files use `<SubType>Form</SubType>`; `.Designer.cs` partials use `<DependentUpon>` pointing to the parent. New test files in `UtinniCoreDotNet.Tests/` auto-glob via the SDK-style `**/*.cs` (no test-csproj edit needed). Same for `Utinni.Cli` (SDK-style) and `Utinni.Cli.Tests` (SDK-style).
- **No EmbeddedResource `.resx` for new forms** (memory `feedback_dotnet_build_msbuild_resources`). FormIffEditor / FormFourCcDialog / FormSaveConfirmDialog all ship without `.resx`; FormDatatableEditor and its modals should follow the same hand-written-Designer pattern.
- **GSD grep-gate hygiene** (memory `feedback_gsd_grep_gate_hygiene`) — plan acceptance "grep X returns zero matches" is LITERAL. If a plan grep-gates the literal token `UndoRedoManager` away from the controller file, source XML comments must describe the concept by behavior (e.g. "the scene-level undo plumbing") rather than naming the type.

## Summary

Phase 9 layers a typed datatable view + edit surface on top of Phase 8's hybrid mutable IFF DOM. The core technical insight is that the DTII format is **fully specified by `DataTable.cpp` / `DataTableColumnType.cpp` / `DataTableWriter.cpp` in `D:/Code/swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/`**, and both the read and write semantics port mechanically:

1. **DTII chunk framing** is trivial: `FORM DTII { FORM <ver> { COLS, TYPE, ROWS } }` where `<ver>` is `0000` (DT_Int/DT_Float/DT_String only — short type tag) or `0001` (full type-spec strings, all 10 `DT_*` types). `COLS` is `int32 numCols · NUL-terminated column-name strings`. `TYPE` is `numCols × NUL-terminated type-spec strings` (V0001) OR `numCols × int32 enum-type` (V0000). `ROWS` is `int32 numRows · numRows × numCols × per-cell payload`. Per-cell payload is `int32` for `DT_Int`, `float32` for `DT_Float`, `NUL-terminated ASCII string` for `DT_String`/`DT_Comment`. **DT_Comment cells emit NOTHING** in the ROWS chunk (`DataTableWriter::_saveRows` skips them). `DT_HashString` / `DT_Enum` / `DT_Bool` / `DT_BitVector` all have basicType `DT_Int` and serialize as `int32`. `DT_PackedObjVars` has basicType `DT_String` and serializes as a NUL-terminated string. All scalars are **little-endian** (SOE convention; payload-LE, header-BE).

2. **`DataTableColumnType` type-spec grammar** (per `DataTableColumnType.cpp:84-232`): the type-spec string's first char is the type discriminator (`i`/`f`/`s`/`c`/`h`/`p`/`b`/`e`/`v`/`z`); `[default]` for the default value; `(label=val,label=val,...)` for `e` (enum), `v` (bit-vector), and `z` (enum-from-other-tab — V2 cross-table feature, out of scope per CONTEXT D-05). Phase 9 must port this parser EXACTLY (every column instantiated from the .tab triggers a fresh parse). The CONTEXT line 42 `e[a:0,b:1,c:2]` format is INCORRECT — verified against `DataTableColumnType.cpp:142-153`: the enum syntax uses **parentheses + `=`**: `e(a=0,b=1,c=2)[default]`. **The planner must update the per-type widget contract accordingly.**

3. **`mangleValue()` is the canonical type-coercion routine** for the D-04 cascade. Per `DataTableColumnType.cpp:382-473`:
   - Empty value → default (or fail if default is `"required"`/`"unique"`).
   - `DT_PackedObjVars` → parse-validate the `name|type|value` grammar; trailing `$|` is sentinel.
   - `DT_Bool` → accept only `"0"` or `"1"`.
   - `DT_HashString` → `Crc::normalizeAndCalculate(value)` → int32 (stored as integer; the original string is NOT preserved).
   - `DT_Enum` / `DT_BitVector` → enum-map lookup; fail if label not in spec.
   - All other basicTypes → return true unchanged.

4. **`SwgDataTableTool` parity** is at the user-mental-model level (typed grid of cells, per-column types, schema-mutable on existing files), not at the file-format-output-byte level. SOE's tool also lacked cross-table FK and the engine-consumer scan — Phase 9's V2 deferrals match.

The **CSV delta-import path** is the highest-novelty surface (Phase 8 had no CSV). The byte-exact-on-untouched invariant (CF-04) requires the CSV importer to apply the per-cell diff at the `MutableDataTableCell` level: if the imported value matches the current value, the cell stays clean (preserves its captured original bytes); only divergent cells get their dirty bit set. This makes SC4 survive the CSV round-trip structurally, not by re-comparison.

The **Find / Replace** + **column-click sort** are pure DataGridView affordances — both are first-party `DataGridView` features (`DataGridViewColumn.SortMode`, header-click sort indicator built-in). The `MultiSelect=true` + per-cell BackColor overlay via `CellFormatting` handles search-match highlighting; on `CombatDataTable`-scale tables (~hundreds of rows × dozens of columns) this is a documented performance risk (Pitfall 7) but is acceptable per UI-SPEC § Grid surface (no virtual-mode required for V1).

**Primary recommendation:** Implement the typed reader (`DataTableDocument.FromIff`), the per-cell `MutableDataTableCell` model wrapping the typed value AND the original bytes (mirrors `MutableIffNode`'s slice-or-fresh pattern), the typed `DataTableWriter` that emits a fresh DTII-framed IFF from the in-memory model (with untouched-cell byte preservation), the `roundtrip-tab` CLI verb gating SC4, the `ThemedDataGridView` TJT-side wrapper, per-type `DataGridViewColumn` subclasses + `EditingControlShowing` swap-in for `DT_Int`/`DT_Float`, the `IDatatableEditController` mirroring `IffEditController`'s shape, the column-reorder/delete safety-net modal via `FormSaveConfirmDialog` reuse, the type-change cascade resolution modal (new `FormTypeChangeCascadeDialog`), and the CSV import preview modal (new `FormCsvImportPreviewDialog`). Split per CONTEXT's 4–6 plan advisory.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| DTII chunk parse (typed) | Framework (`UtinniCoreDotNet/Formats/Datatable/`) | Framework `Formats/Iff/IffReader` (raw chunks) | CF-01 framework-side; composes on the immutable `IffDocument` Phase 4/7 ships. |
| DTII chunk serialize (typed) | Framework (`UtinniCoreDotNet/Formats/Datatable/`) | Framework `Formats/Iff/IffWriter` (raw chunks) | CF-01 + CF-04; emits a fresh `MutableIffDocument`'s bytes via `IffWriter.Write` so existing chunk-length roll-up + 64 MB cap apply for free. |
| Typed cell model + original-bytes preservation | Framework (`MutableDataTableCell`) | — | CF-04 — mirrors `MutableIffNode`'s slice-or-fresh hybrid pattern at the per-cell granularity. |
| Type-spec parser (`DataTableColumnType` port) | Framework (`UtinniCoreDotNet/Formats/Datatable/DataTableColumnType.cs`) | — | Pure C# port of the SOE parser; consumed by both reader (column type construction) and writer (default-value resolution). |
| `mangleValue()` port for D-04 cascade | Framework (`DataTableColumnType.MangleValue`) | — | Pure function; consumed by `IDatatableEditController.ChangeColumnTypeCommand` + the CSV importer's per-cell coercion check. |
| `Crc::normalizeAndCalculate()` port | Framework (`DataTableHashCrc` static helper) | — | Used by `DT_HashString` cell widget's live hash preview + `mangleValue()` for the int32 serialized form. **Must port the SOE CRC variant** (per memory `project_swg_client_v2_reference`) — verify against `DataTableColumnType.cpp:434`. |
| Editor-local undo/redo (transactions) | Framework (`UtinniCoreDotNet/Editing/DatatableEditController.cs`) | — | CF-06 / mirrors Phase 8's `IffEditController` shape. Pure-managed (no UI dep) so the controller is unit-testable from CI's Utinni-only checkout. |
| Round-trip CLI verb (`roundtrip-tab`) | CLI (`Utinni.Cli/Commands/RoundtripTabCommand.cs`) → goldens | Framework writer + reader | CF-02 max-harness gate for SC4. Same harness pattern as `roundtrip-iff` (Phase 8). |
| `ThemedDataGridView` themed wrapper | TJT (`The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/ThemedDataGridView.cs`) | — | UI-SPEC assumption #1: TJT-side wrapper alongside `IffChunkTree`. Phases 10/11 inherit. |
| Per-type `DataGridViewColumn` subclasses + cell-editor swap-in | TJT (`The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/Datatable*.cs`) | Framework type-spec | Combo (`DT_Enum`), Checkbox (`DT_Bool`), Numeric (`DT_Int`/`DT_Float` via `EditingControlShowing` → `UtinniNumericUpDown`), Text (`DT_String`/`DT_Comment`/`DT_HashString`/`DT_PackedObjVars`/`DT_BitVector`). |
| Editor host form (`FormDatatableEditor`) | TJT (`The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor.cs`) | — | UI-SPEC § Host Placement: resizable `UtinniForm`, hide-not-dispose, mirrors `FormIffEditor` shape. |
| Modals (cascade resolution, CSV preview, column safety-net) | TJT (`UI/Forms/Form{TypeChangeCascadeDialog,CsvImportPreviewDialog}.cs` + `FormSaveConfirmDialog` reuse) | — | Per-call modals (`using (var dlg = …)`); `FormSaveConfirmDialog` reused for the column safety-net per UI-SPEC assumption #5. |
| Loose-override + Save/Save-As (file writes) | TJT (`UI/Forms/FormDatatableEditor`) → existing `IffSaveTargets` | Framework `LooseOverridePath` | Phase 9 reuses `IffSaveTargets.SaveLooseOverride / SaveToPath / SaveInPlace` verbatim — Phase 8 already abstracted the path. |
| `.tre` repack save target | TJT (`UI/Forms/FormDatatableEditor`) → existing `TreRepackSaveTarget` | Framework `TreBackupPath` + `TreRepackLock` | Phase 9 reuses Phase 8's `TreRepackSaveTarget.Apply(target, rewrittenIffBytes, createBackup)` verbatim — the input is the writer's output bytes, which are equally valid as an IFF for the .tab case. |
| Live-patch (DISABLED inherited from Phase 8) | TJT (UI) → existing `LivePatchSaveTarget` | Framework `LivePatchValidator` | CF-03 — same DISABLED stance; no Phase 9 open path constructs `OpenSource.ClientMemory`. |
| Forced in-session reload | TJT (UI) → existing `ClientReloadDispatcher.Dispatch` | Framework `ReloadAssetClassifier` | CF-05 tier-(b) — `.iff` carrier + root TypeId `DTII` already routes to `PendingNextSceneChange` in `ReloadAssetClassifier.cs` (verified Phase 8 Plan 5; 22-case routing-table test covers DTII). The Reload button shows the locked CF-05 copy. |
| CSV / TSV export + delta-import | TJT (UI form orchestrates) → TJT (`Saving/CsvDelta*.cs`) | Framework `DatatableEditController` (single-transaction Apply) | UI flow is TJT; the per-cell coercion check goes through `DataTableColumnType.MangleValue` (framework). |
| Find / Replace | TJT (Form pane, attaches `CellFormatting`) | — | DataGridView built-in `MultiSelect` + per-cell `BackColor` overlay via `CellFormatting`. No framework code. |
| Column-click sort | TJT (DataGridView built-in) | — | First-party `DataGridView` `SortMode = Automatic`; view-only per D-09. |
| TRE Browser "Open in Datatable Editor" hand-off | TJT (`FormTreBrowser` context-menu entry) → `FormDatatableEditor.OpenFromTreEntry` | Framework `TreRecordIndexResolver` | Mirrors the Phase 8 "Open in IFF Editor" pattern verbatim. Visibility predicate: extension `.tab` OR root tag `DTII` (cheap byte-scan). |
| IFF Editor "Switch to typed datatable view" hand-off | TJT (`FormIffEditor` menu) → `FormDatatableEditor.OpenFromMutableIff` | — | NEW: Phase 9 must add a menu item to FormIffEditor visible only when `document.Root.TypeId == "DTII"`. **Manual hand-off** (D-10.3); does NOT auto-route. Hands the `MutableIffDocument` (or its bytes + Source) directly without re-parsing. |

## Standard Stack

This phase adds **no external packages.** All dependencies are already present in the solution (verified against `UtinniCoreDotNet.csproj` and `TheJawaToolboxDotNet.csproj`, 2026-05-28).

### Core (already in solution)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET Framework | 4.7.2 | Target framework for managed code (CF-01 framework + plugin) | `[VERIFIED: D:/Code/Utinni/UtinniCoreDotNet/UtinniCoreDotNet.csproj` line ~12: `<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>`]. Pinned by CON-T-03 (two-language template parity). |
| `System.Windows.Forms` | net472 BCL | WinForms UI (`UtinniForm`, `DataGridView`, modals) | `[VERIFIED: existing FormIffEditor consumes BCL WinForms]`. |
| `System.IO` | net472 BCL | File save (FileStream + Flush(true)) | `[VERIFIED: IffSaveTargets.WriteAtomic uses System.IO.FileStream]`. |
| xUnit | (pre-existing) | Managed unit tests | `[VERIFIED: UtinniCoreDotNet.Tests.csproj references xunit + xunit.runner.visualstudio]`. |
| Catch2 | 3.x via vcpkg | Native unit tests (N/A for Phase 9 — pure managed) | `[VERIFIED: vcpkg.json manifest, Phase 6]`. Phase 9 is pure managed C#, so native test infra is not consumed. |
| CommandLineParser | 2.x | CLI verb parsing for `roundtrip-tab` | `[VERIFIED: RoundtripIffCommand consumes the `CommandLine` namespace + `[Verb]`/`[Value]`/`[Option]` attributes — D:/Code/Utinni/Utinni.Cli/Commands/RoundtripIffCommand.cs lines 29, 36-50]`. |
| Newtonsoft.Json | 13.x | CLI JSON output envelope (sorted-key stable) | `[VERIFIED: same RoundtripIffCommand.cs line 30 `using Newtonsoft.Json.Linq;`]`. |

### Supporting (already in solution)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `UtinniCoreDotNet.UI.Theme.Colors` | (in-repo) | Themed color accessors (Phase 9 uses `Colors.Primary()` / `Colors.Secondary()` / `Colors.Font()` etc., NO raw ARGB) | All UI painting per UI-SPEC § Color. |
| `UtinniCoreDotNet.UI.Controls.UtinniButton` / `UtinniLabel` / `UtinniTextbox` / `UtinniContextMenuStrip` / `UtinniNumericUpDown` / `UtinniToggleButton` / `UtinniComboBox` | (in-repo) | Themed WinForms control suite | All UI per UI-SPEC § Design System mandatory control reuse. |
| `UtinniCoreDotNet.PluginFramework.IEditorPlugin` / `IEditorForm` | (in-repo) | MEF SPI (CON-M-01/02) | `FormDatatableEditor` implements `IEditorForm`; registered via TJT `Plugin.cs` `GetForms()`. |
| `UtinniCoreDotNet.Utility.UtINI` | (in-repo) | INI settings persistence (`[DatatableEditor] width`, `height`, `splitterDistance`, `findReplaceVisible`, `editCommentRows`, `looseOverrideDir`) | Mirrors Phase 8's `[IffEditor]` pattern in `FormIffEditor.CreateSettings`. |
| `UtinniCoreDotNet.Formats.Iff.{IffReader, IffWriter, IffDocument, IffChunk, IffContainerChunk, IffLeafChunk, MutableIffDocument, MutableIffNode, IffParseException, OpenSource}` | (Phase 4/7/8) | EA-IFF-85 read+write primitives Phase 9 composes on | DTII parse / serialize delegates to `IffReader.Read` + `IffWriter.Write`; Phase 9's typed layer sits on top. |
| `UtinniCoreDotNet.Editing.IffEditController` | (Phase 8) | Pattern reference for `DatatableEditController` | NOT directly consumed; Phase 9 has its own controller (a `DatatableEditCommand` is not an `IIffEditCommand`). |
| `UtinniCoreDotNet.Saving.{LooseOverridePath, ReloadAssetClassifier, TreBackupPath, TreRepackLock}` | (Phase 8) | File save + reload routing | DIRECTLY consumed. `ReloadAssetClassifier.Classify(".iff", "DTII")` already returns `PendingNextSceneChange` (verified `DatatableExtensions = { .iff }` + DTII sub-detect in `ClientReloadDispatcherTests.cs`). |
| `UtinniCoreDotNet.Formats.Tre.{TreFile, TreWriter, TreRecordIndexResolver}` | (Phase 7/8) | `.tre` repack + TRE Browser hand-off resolution | DIRECTLY consumed by the `.tre` repack save mode (CF-03 mode 4) and by `OpenFromTreEntry` (mirrors Phase 8 hand-off). |
| `TheJawaToolboxDotNet.Saving.{IffSaveTargets, ClientReloadDispatcher, TreRepackSaveTarget}` | (Phase 8) | Save mode dispatchers | DIRECTLY reused — Phase 9 calls these verbatim with its own writer's output bytes. |
| `TheJawaToolboxDotNet.UI.Forms.FormSaveConfirmDialog` | (Phase 8) | Per-call risk-proportional confirm modal | DIRECTLY reused per UI-SPEC assumption #5 (column-reorder/delete safety-net) AND for discard-while-dirty / repack confirms. Default WinForms dispose-on-close is CORRECT (per-call lifecycle, not singleton). |
| `TheJawaToolboxDotNet.UI.Controls.IffChunkTree` | (Phase 8) | Pattern reference for `ThemedDataGridView` placement | NOT directly consumed; new `ThemedDataGridView` ships alongside per UI-SPEC assumption #1. |
| `UtinniCore.Utinni.Game.IsRunning` | (existing native binding, defensive try/catch) | Live-client gate | `[VERIFIED: ClientReloadDispatcher.cs line 88 + LivePatchSaveTarget + FormIffEditor lines ~896/1450 all wrap `Game.IsRunning` in try/catch — the binding throws outside an injected client]`. |
| `UtinniCoreDotNet.Callbacks.GameCallbacks.AddMainLoopCall` | (existing) | Game-thread marshal for any live-client binding call | Phase 9 only consumes this transitively via `ClientReloadDispatcher` (CF-03 mode 3 disabled; no direct binding calls). |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `DataGridView` for the main grid surface | Custom-paint over `Panel` | DataGridView is the standard WinForms tabular surface, has built-in column sort + selection + cell editor swap-in; custom paint sacrifices keyboard navigation + ToolTip integration. UI-SPEC § Design System mandates DataGridView. |
| `DataGridView` virtual mode (`VirtualMode = true` + `CellValueNeeded`) | Bound model (`DataSource = BindingList<RowVm>`) | Virtual mode is faster for very large datasets but complicates per-cell `BackColor` overlay (search match) and `Frozen` rows (DT_Comment). V1 ships non-virtual; revisit if `CombatDataTable` (~hundreds of rows × dozens of cols) exhibits jank (Pitfall 7). |
| `DataGridViewComboBoxColumn` for `DT_Enum` | `DataGridViewTextBoxColumn` + lookup-on-commit | Combo provides the natural "dropdown of labels" UX; planner discretion if combo + custom theming gets ugly under dark theme. |
| `EditingControlShowing` swap-in for `DT_Int`/`DT_Float` (themed `UtinniNumericUpDown`) | Plain TextBoxColumn + `Validating` parse | Swap-in mirrors UI-SPEC § Per-type cell widget contract: editor swaps in `UtinniNumericUpDown` for the duration of the edit, then commits the value. Plain TextBox + parse loses the spinner affordance + min/max clamp. |
| Roll our own CSV parser | `System.Text` + custom split | No suitable BCL CSV parser ships in net472 BCL; `Microsoft.VisualBasic.FileIO.TextFieldParser` exists but pulling `Microsoft.VisualBasic.dll` for one helper is over-weight. Hand-roll a small RFC 4180-ish parser per UI-SPEC (UTF-8 BOM, double-quote escape) — see "Don't Hand-Roll" §. |
| Tabbed MDI multi-document FormDatatableEditor | Single-document-per-window (open-replaces with discard prompt) | UI-SPEC assumption #2: V1 ships single-document-per-window per the FormTreBrowser + FormIffEditor precedent. Tabbed/MDI deferred. |
| `MaterialSkin` or third-party DataGridView theme nuget | Hand-themed `ThemedDataGridView` wrapper | Adding a UI package contradicts UI-SPEC § Registry Safety (zero-deps WinForms) AND adds slopcheck surface. The `Colors.*()` token map applied in the wrapper is sufficient per UI-SPEC § ThemedDataGridView token map. |

**Installation:**
```bash
# No new package installs required.
# All dependencies are already in:
#   - D:/Code/Utinni/UtinniCoreDotNet/UtinniCoreDotNet.csproj           (framework primitives consumer)
#   - D:/Code/Utinni/Utinni.Cli/Utinni.Cli.csproj                       (CLI verb host)
#   - D:/Code/Utinni/Utinni.Cli.Tests/Utinni.Cli.Tests.csproj           (golden fixture suite)
#   - D:/Code/Utinni/UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj (xUnit framework tests)
#   - D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj (TJT UI)
```

**Version verification:** N/A — Phase 9 adds zero new packages. The pre-existing dependency chain (xUnit, CommandLineParser, Newtonsoft.Json) has been live since Phase 4 and is already known-green in CI.

## Package Legitimacy Audit

Phase 9 installs **zero new external packages**. The Package Legitimacy Gate is trivially satisfied — there is no slopcheck surface to evaluate.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| *(none — Phase 9 is composed entirely from existing in-solution assemblies + BCL)* | — | — | — | — | N/A | N/A |

**Packages removed due to slopcheck `[SLOP]` verdict:** none (no installs).
**Packages flagged as suspicious `[SUS]`:** none (no installs).

## Architecture Patterns

### System Architecture Diagram

```
                                  ┌───────────────────────────────────────────────┐
                                  │           User triggers an entry point        │
                                  └──────────────────────┬────────────────────────┘
                                                         │
              ┌──────────────────────────────────────────┼──────────────────────────────────────────┐
              │                                          │                                          │
              ▼                                          ▼                                          ▼
   ┌────────────────────┐                  ┌──────────────────────────┐                ┌──────────────────────────┐
   │ FormDatatableEditor │                  │ FormTreBrowser           │                │ FormIffEditor            │
   │  toolbar  Open…     │                  │  context-menu            │                │  menu "Switch to typed   │
   └─────────┬───────────┘                  │  "Open in Datatable      │                │  datatable view"         │
             │ OpenFileDialog               │   Editor" (when ext .tab │                │  (when Root.TypeId=DTII) │
             ▼                              │   OR root tag DTII)      │                └────────────┬─────────────┘
   ┌──────────────────────┐                 └────────────┬─────────────┘                             │
   │ IffReader.Read(...)  │                              │                                            │
   └─────────┬────────────┘                              │ TrePayloadResolver.TryResolve              │
             │ IffDocument                               │ (off-UI-thread, Task.Run)                  │
             ▼                                           ▼                                            │
   ┌──────────────────────┐                 ┌──────────────────────────┐                              │
   │ MutableIffDocument   │◀────────────────│ TreRecordIndexResolver   │                              │
   │ .FromDocument(...)   │                 │ .ResolveOrUnknown(...)   │                              │
   └─────────┬────────────┘                 └────────────┬─────────────┘                              │
             │                                           │ OpenSource.TreArchive OR Unknown           │
             │   ┌───────────────────────────────────────┘                                            │
             ▼   ▼                                                                                    │
   ┌─────────────────────────────────────────────────────────────────────────┐                       │
   │ DataTableDocument.FromIff(mutableIff)                                   │ ◀─────────────────────┘
   │  - parses FORM DTII { FORM 0000|0001 { COLS, TYPE, ROWS } }              │   (manual hand-off,
   │  - constructs DataTableColumnType per column (type-spec parser port)    │    NO auto-route per
   │  - constructs MutableDataTableCell per row×col                          │    D-10.3)
   │    (each holds typed value + original-byte slice — CF-04 hybrid)        │
   │  - retains the underlying MutableIffDocument as the byte-source root    │
   └────────────────────────────────────┬────────────────────────────────────┘
                                        │
                                        ▼
                ┌───────────────────────────────────────────────────────────┐
                │  FormDatatableEditor.LoadDocument(dtDoc, source, name)   │
                │  (mirrors FormIffEditor.LoadDocument shape)              │
                └─────────────┬─────────────────────────────────────────────┘
                              │
                              ▼
   ┌────────────────────────────────────────────────────────────────────────────────────────┐
   │ DatatableEditController + ThemedDataGridView (Fill, front-most per CF-09)             │
   │  per-type DataGridViewColumns: Text|Numeric|Combo|Checkbox per DT_*                   │
   │  CellFormatting overlays: dirty (Secondary fg), needs-review (Color.Red bg),          │
   │                            search-match (Secondary bg @40%), frozen DT_Comment row    │
   │  context menu: row add/remove/reorder · column add/remove/reorder · type change       │
   │  bulk: Find/Replace pane · CSV Import/Export · column-click sort (view-only D-09)    │
   └─────────────┬───────────────────────────────┬──────────────────────────────┬──────────┘
                 │ User commits edit             │ User clicks Save▾            │ User clicks Reload-in-client
                 ▼                               ▼                              ▼
       ┌────────────────────┐    ┌────────────────────────────────┐    ┌───────────────────────────────┐
       │ DatatableEdit-     │    │ DatatableEditController.Build  │    │ ClientReloadDispatcher        │
       │ Controller.Apply   │    │ MutableIffDocument()           │    │  .Dispatch(savedPath, "DTII") │
       │ (or Undo/Redo)     │    │  → IffWriter.Write(mDoc)       │    │  → ReloadAssetClassifier       │
       │  →EditApplied      │    │  → IffSaveTargets.Save(...)    │    │     → PendingNextSceneChange   │
       │  refresh visuals   │    │     OR TreRepackSaveTarget.    │    │  → status: locked CF-05 copy   │
       └────────────────────┘    │     Apply(...)                 │    │  → NO binding call            │
                                  │  with Flush(true) MEDIUM-9     │    └───────────────────────────────┘
                                  │  barrier                       │
                                  └────────────────────────────────┘
```

### Recommended Project Structure

```
D:/Code/Utinni/
├── UtinniCoreDotNet/
│   └── Formats/
│       └── Datatable/                                    # NEW (CF-01)
│           ├── DataTableColumnType.cs                    # type-spec parser port; mangleValue; enum/bitvector map; default cell
│           ├── DataTableCellValue.cs                     # discriminated union: IntValue/FloatValue/StringValue (mirrors DataTableCell.h)
│           ├── DataTableHashCrc.cs                       # SOE CRC variant for DT_HashString (port of Crc::normalizeAndCalculate)
│           ├── DataTableDocument.cs                      # FromIff / per-column DataTableColumnType / rows × cols typed accessors
│           ├── MutableDataTableCell.cs                   # typed value + IsDirty + captured original-bytes slice (CF-04 hybrid)
│           ├── MutableDataTableColumn.cs                 # name + DataTableColumnType + IsDirty
│           ├── MutableDataTableRow.cs                    # cells[] + IsDirty
│           ├── MutableDataTableDocument.cs               # version (0000/0001) + columns + rows + Build() → MutableIffDocument
│           ├── DataTableWriter.cs                        # MutableDataTableDocument → MutableIffDocument (composes IffWriter.Write)
│           └── DataTableParseException.cs                # version mismatch / cell-count mismatch / type-spec parse failure
│   └── Editing/
│       └── DatatableEditController.cs                    # NEW (CF-06) — pure-managed; mirrors IffEditController
│       └── IDatatableEditCommand.cs + DatatableEditCommands factory
├── UtinniCoreDotNet.Tests/
│   └── FormatsTests/
│       └── Datatable/                                    # NEW
│           ├── DataTableColumnTypeTests.cs               # type-spec parser per discriminator; mangleValue per DT_*
│           ├── DataTableHashCrcTests.cs                  # CRC parity vs SOE reference values
│           ├── DataTableDocumentTests.cs                 # FromIff for V0 + V1 fixtures + DT_Comment skip + null-cell defaults
│           ├── DataTableWriterTests.cs                   # round-trip byte-exact (no edits); per-DT_* serialize; chunk-length roll-up
│           ├── DatatableEditControllerTests.cs           # 10+ commands × apply/undo/redo identity; baseline-clean dirty; transaction
│           └── DatatableFixtures.cs                      # in-test-code synthetic .tab byte builders (V0 / V1 minimal; one-per-DT_*; DT_Comment)
├── Utinni.Cli/
│   └── Commands/
│       └── RoundtripTabCommand.cs                        # NEW (CF-02) — `roundtrip-tab` verb (mirrors RoundtripIffCommand)
├── Utinni.Cli.Tests/
│   └── Commands/
│       └── RoundtripTabCommandTests.cs                   # NEW — goldens against in-repo synthetic .tab fixtures
│   └── Infrastructure/
│       └── DataTableFixtureBuilder.cs                    # NEW — builds DTII bytes via the IffBuilder pattern

D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/
├── UI/
│   ├── Controls/
│   │   ├── ThemedDataGridView.cs                         # NEW (UI-SPEC assumption #1; TJT-side wrapper)
│   │   ├── DatatableColumnFactory.cs                     # NEW — switch on DataTableColumnType.Type → DataGridViewColumn subclass
│   │   ├── DatatableHashStringEditor.cs                  # NEW — floating UtinniLabel hash preview anchored on CellBeginEdit
│   │   └── DatatableNumericUpDownEditingControl.cs       # NEW — UtinniNumericUpDown adapted to IDataGridViewEditingControl
│   └── Forms/
│       ├── FormDatatableEditor.cs + .Designer.cs         # NEW — UtinniForm host (mirrors FormIffEditor pattern)
│       ├── FormAddColumnDialog.cs + .Designer.cs         # NEW — Add column modal (name + type combo of 10 DT_*)
│       ├── FormTypeChangeCascadeDialog.cs + .Designer.cs # NEW — type-change cascade resolution modal with embedded grid
│       └── FormCsvImportPreviewDialog.cs + .Designer.cs  # NEW — CSV preview modal with per-column diff + invalid-rows list
├── Saving/
│   ├── DatatableSaveTargets.cs                           # NEW — thin shim composing IffSaveTargets with Build()→bytes pipeline
│   └── DatatableCsvSerializer.cs                         # NEW — CSV export + delta-import (per-cell coercion via DataTableColumnType.MangleValue)
└── Plugin.cs                                             # MODIFIED — register FormDatatableEditor in the existing try/catch block (SPI unchanged)
```

### Pattern 1: Typed DataTable parse over IffReader

**What:** `DataTableDocument.FromIff(MutableIffDocument)` enters `FORM DTII`, branches on `TAG_0000` vs `TAG_0001`, reads `COLS` (int32 numCols + numCols NUL-terminated strings), reads `TYPE` (numCols int32 enum values for V0 OR numCols NUL-terminated type-spec strings for V1), reads `ROWS` (int32 numRows + numRows × numCols × per-cell payload). Per-cell payload decoder switches on `DataTableColumnType.BasicType`: `DT_Int` → `int32` LE; `DT_Float` → `float32` LE; `DT_String` → NUL-terminated ASCII string; `DT_Comment` → nothing in payload (writer skips comment columns in ROWS per `DataTableWriter::_saveRows`).

**When to use:** Both the typed read path (editor open) and the round-trip CLI verb (`roundtrip-tab`).

**Example:**
```csharp
// Source: ported from D:/Code/swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTable.cpp:444-603
// (load + load_0000 + load_0001 + _readCell). [CITED]
public static DataTableDocument FromIff(MutableIffDocument iffDoc)
{
    if (iffDoc == null) throw new ArgumentNullException("iffDoc");
    var root = iffDoc.Root;
    if (root == null || root.Kind != MutableIffNodeKind.Container || root.TypeId != "FORM" || root.SubTypeId != "DTII")
        throw new DataTableParseException("Root is not FORM DTII.");
    if (root.Children.Count != 1)
        throw new DataTableParseException("DTII root must contain exactly one version FORM.");

    var ver = root.Children[0];
    if (ver.Kind != MutableIffNodeKind.Container || ver.TypeId != "FORM")
        throw new DataTableParseException("DTII child must be a version FORM.");

    string version = ver.SubTypeId; // "0000" or "0001"
    if (version != "0000" && version != "0001")
        throw new DataTableParseException("Unsupported DTII version: " + version);

    // COLS, TYPE, ROWS are the three required leaf chunks under FORM <ver>.
    var cols = ver.Children.FirstOrDefault(c => c.TypeId == "COLS");
    var types = ver.Children.FirstOrDefault(c => c.TypeId == "TYPE");
    var rows = ver.Children.FirstOrDefault(c => c.TypeId == "ROWS");
    if (cols == null || types == null || rows == null)
        throw new DataTableParseException("Missing COLS / TYPE / ROWS chunk in FORM DTII/" + version);

    int numCols = ReadInt32Le(cols.GetPayloadCopy(), 0);
    var columnNames = ReadNulStrings(cols.GetPayloadCopy(), 4, numCols);
    var columnTypes = version == "0000"
        ? ReadV0Types(types.GetPayloadCopy(), numCols)
        : ReadV1Types(types.GetPayloadCopy(), numCols);
    var rowData = ReadRows(rows.GetPayloadCopy(), columnTypes); // little-endian per-cell payloads
    return new DataTableDocument(version, columnNames, columnTypes, rowData, iffDoc);
}
```

**Reference:** `D:/Code/swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTable.cpp:444-603` (load / load_0000 / load_0001 / _readCell); chunk strings are NUL-terminated ASCII per `Iff.cpp:893-897` `insertChunkString` (`istrlen(string)+1`). `[VERIFIED: read directly from swg-client-v2]`.

### Pattern 2: Hybrid mutable cell model (CF-04 — per-cell original-byte preservation)

**What:** Each cell in the loaded document holds (a) the typed parsed value, (b) an `IsDirty` flag, and (c) a captured slice of the original payload bytes for that cell. On serialize: clean cells re-emit their captured slice byte-for-byte; dirty cells serialize fresh. Container chunk lengths (`COLS`/`TYPE`/`ROWS`) roll up bottom-up. This makes SC4 ("no silent schema corruption") byte-exact for unmodified columns/rows AND survives CSV round-trip (D-08): a CSV cell whose imported value matches the current value is left clean → re-emits original bytes.

**When to use:** All cells in `MutableDataTableCell`; mirrors `MutableIffNode`'s hybrid-DOM (Phase 8 D-07).

**Example:**
```csharp
// Source: mirrors UtinniCoreDotNet/Formats/Iff/MutableIffNode.cs pattern (Phase 8 D-07). [VERIFIED: D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/MutableIffNode.cs]
public sealed class MutableDataTableCell
{
    private DataTableCellValue _value;
    private byte[] _originalSlice;   // captured at FromIff time
    private bool _isDirty;

    public DataTableCellValue Value
    {
        get => _value;
        set
        {
            if (DataTableCellValue.Equals(_value, value)) return; // no-op; preserve original-slice
            _value = value;
            _isDirty = true;
            _originalSlice = null;     // invalidate slice on any value change
            ParentRow?.MarkDirty();
            ParentColumn?.MarkDirty();
        }
    }
    public bool IsDirty => _isDirty;

    // Serializer entry point: if !IsDirty AND _originalSlice != null, emit slice; else serialize fresh.
    internal void WritePayload(BinaryWriter bw, DataTableColumnType ct)
    {
        if (!_isDirty && _originalSlice != null) { bw.Write(_originalSlice); return; }
        // Fresh serialize — switch on ct.BasicType (DT_Int → int32, DT_Float → float32, DT_String → NUL-terminated)
        WriteFreshPayload(bw, ct, _value);
    }
}
```

### Pattern 3: Per-type cell widget contract (UI-SPEC § Per-type cell widget contract)

**What:** Each `DataTableColumnType.Type` (10 enum values) maps to a `DataGridViewColumn` subclass + optional `EditingControlShowing` swap-in. Invalid input is blocked at keystroke by the widget itself (NumericUpDown won't accept letters; ComboBox is dropdown-only). Free-text columns (`DT_String`, `DT_Comment`, `DT_HashString`, `DT_PackedObjVars`, `DT_BitVector`) run a parse-back check on `CellValidating` commit and revert on failure.

**When to use:** All cells in the editor grid; planner builds `DatatableColumnFactory.Build(columnType) → DataGridViewColumn`.

**Example:**
```csharp
// Source: UI-SPEC § Per-type cell widget contract + DataTableColumnType.cpp:84-232 type-spec parser. [CITED]
public static DataGridViewColumn Build(string columnName, DataTableColumnType ct)
{
    switch (ct.Type)
    {
        case DataTableColumnType.DataType.DT_Bool:
            return new DataGridViewCheckBoxColumn { Name = columnName, HeaderText = columnName };
        case DataTableColumnType.DataType.DT_Enum:
            var combo = new DataGridViewComboBoxColumn { Name = columnName, HeaderText = columnName };
            foreach (var kv in ct.EnumMap) combo.Items.Add(kv.Key);
            return combo;
        case DataTableColumnType.DataType.DT_Int:
        case DataTableColumnType.DataType.DT_Float:
            // EditingControlShowing on the host grid swaps in UtinniNumericUpDown for the duration of the edit.
            return new DataGridViewTextBoxColumn { Name = columnName, HeaderText = columnName };
        case DataTableColumnType.DataType.DT_Comment:
            // Frozen-header row treatment in CellFormatting; per-cell free text otherwise.
            return new DataGridViewTextBoxColumn { Name = columnName, HeaderText = columnName };
        case DataTableColumnType.DataType.DT_HashString:
            // Free text + adjacent floating UtinniLabel hash preview anchored on CellBeginEdit.
            return new DataGridViewTextBoxColumn { Name = columnName, HeaderText = columnName };
        case DataTableColumnType.DataType.DT_String:
        case DataTableColumnType.DataType.DT_PackedObjVars:
        case DataTableColumnType.DataType.DT_BitVector:
            return new DataGridViewTextBoxColumn { Name = columnName, HeaderText = columnName };
        case DataTableColumnType.DataType.DT_Unknown:
            // 09-UI-SPEC R-03 recommendation: read-only hex render, no edit affordance.
            return new DataGridViewTextBoxColumn { Name = columnName, HeaderText = columnName, ReadOnly = true };
        default:
            throw new InvalidOperationException("Unhandled DataType: " + ct.Type);
    }
}
```

### Pattern 4: Editor-local undo/redo controller (CF-06 — mirrors `IffEditController`)

**What:** `DatatableEditController` over a `MutableDataTableDocument`. Public API: `Apply(IDatatableEditCommand) / Undo() / Redo() / CanUndo / CanRedo / IsDirty / EditApplied event / Document property` — verbatim shape from Phase 8 `IffEditController`. `IsDirty = (netAppliedCount > 0)` (baseline-clean dirty per the Phase 8 Codex-unique-concern resolution — see Phase 8 Plan 4 SUMMARY decision #2). Commands: `EditCellValue`, `AddRow`, `RemoveRow`, `MoveRowUp/Down`, `AddColumn`, `RemoveColumn`, `MoveColumnLeft/Right`, `RenameColumn`, `ChangeColumnType`, `ApplyCsvImport` (single transaction wrapping all delta cells).

**When to use:** Every user-visible edit goes through the controller; tree refresh + dirty visuals + button state refresh fire on `EditApplied`. NEVER touch `UtinniCoreDotNet.UndoRedo.UndoRedoManager` (scene-level undo).

**Example:**
```csharp
// Source: mirrors UtinniCoreDotNet/Editing/IffEditController.cs (Phase 8 D-08). [VERIFIED: D:/Code/Utinni/UtinniCoreDotNet/Editing/IffEditController.cs lines 67-105]
public sealed class DatatableEditController
{
    private readonly MutableDataTableDocument document;
    private readonly Stack<IDatatableEditCommand> undoStack = new Stack<IDatatableEditCommand>();
    private readonly Stack<IDatatableEditCommand> redoStack = new Stack<IDatatableEditCommand>();
    private int netAppliedCount;

    public bool IsDirty => netAppliedCount > 0;
    public bool CanUndo => undoStack.Count > 0;
    public bool CanRedo => redoStack.Count > 0;
    public event EventHandler EditApplied;

    public void Apply(IDatatableEditCommand cmd) { /* cmd.Do(); undoStack.Push; redoStack.Clear; netAppliedCount++; EditApplied?.Invoke; */ }
    public void Undo() { /* cmd = undoStack.Pop; cmd.UndoOp(); redoStack.Push; netAppliedCount--; EditApplied; */ }
    public void Redo() { /* cmd = redoStack.Pop; cmd.Do(); undoStack.Push; netAppliedCount++; EditApplied; */ }
}
```

### Pattern 5: CSV delta-import (D-08 — preserves byte-exact-on-untouched)

**What:** Parse the imported CSV → for each cell, compare imported value against current value via `DataTableCellValue.Equals` → if equal, skip (leave clean); if different, validate via `DataTableColumnType.MangleValue` (D-04 cascade contract) → on success, queue an `EditCellValue` command; on failure, list in the preview modal's `Color.Red` rejected-rows section. Apply all queued commands as a SINGLE `ApplyCsvImportCommand` transaction (one undo entry for the whole import). Preview modal surfaces `"{N} cells will change, {M} will stay original bytes, {K} would be type-invalid and will be skipped"` per UI-SPEC § Copywriting.

**When to use:** `Import CSV…` toolbar action triggers this flow.

**Example:**
```csharp
// Source: derived from CONTEXT D-08 + UI-SPEC § CSV import preview modal. Original to Utinni. [DERIVED]
public sealed class CsvImportPlan
{
    public List<EditCellPatch> Changes { get; }
    public List<UnchangedCellMatch> Unchanged { get; }
    public List<InvalidCellEntry> Invalid { get; }

    public static CsvImportPlan Build(MutableDataTableDocument target, string[][] csvRows, IReadOnlyList<string> csvHeader)
    {
        var plan = new CsvImportPlan();
        for (int r = 0; r < csvRows.Length; r++)
        {
            for (int c = 0; c < csvHeader.Count; c++)
            {
                var ct = target.GetColumnType(csvHeader[c]);
                string raw = csvRows[r][c];
                if (CellMatchesCurrent(target, r, c, raw)) { plan.Unchanged.Add(...); continue; }
                if (!ct.MangleValue(ref raw)) { plan.Invalid.Add(new InvalidCellEntry(r, c, raw)); continue; }
                plan.Changes.Add(new EditCellPatch(r, c, raw));
            }
        }
        return plan;
    }
}
```

### Anti-Patterns to Avoid

- **Hand-painting the DataGridView instead of using built-in `Colors.*()` token mapping in `ThemedDataGridView`.** UI-SPEC § ThemedDataGridView token map is exhaustive; use it. Hand-painting breaks `EditingControlShowing` swap-in and high-DPI scaling.
- **Per-character `EditCellValue` commits on `CellValueChanged`.** Mirrors Phase 8 Codex MEDIUM-6: a multi-character edit produces N undoable commands instead of 1. Use commit-on-`CellEndEdit` (`DataGridView.CellEndEdit`) — fires once per cell-edit cycle.
- **Calling `DataGridView.Sort` followed by saving in current view order.** D-09 locks sort as view-only. Save MUST iterate `MutableDataTableRow` collection in its mutable-model order (which `MoveRowUp/Down` commands mutate). Sorting the DataGridView only re-renders rows in display order via the `BindingSource`; the underlying model is untouched.
- **Calling `Game.AddSetSceneCallback` to "trigger" a reload.** That binding is a NOTIFICATION hook, NOT a trigger (Phase 8 cross-AI-reviewer lock; verified `ClientReloadDispatcher.cs:53-56` explicitly does NOT call it). CF-05 tier-(b) is non-negotiable.
- **Constructing `OpenSource.LooseFile(logicalPath)` on TRE Browser hand-off failure.** Phase 8 W-3 contract: on `TreRecordIndexResolver.ResolveOrUnknown` failure, set `Source = OpenSource.Unknown.Instance`. Save-In-Place + Save-Repack + Patch-Live all naturally pattern-match-false and stay disabled.
- **Re-rolling Save modes 1/2/4.** Phase 8 already shipped `IffSaveTargets.SaveLooseOverride / SaveToPath / SaveInPlace` (`D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/IffSaveTargets.cs`) and `TreRepackSaveTarget.Apply` (`D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/TreRepackSaveTarget.cs`). Phase 9 produces serialized bytes via `IffWriter.Write(BuildMutableIff(dtDoc))` and passes them to the existing targets — no new save plumbing.
- **Breaking the Phase 8 singleton-form hide-not-dispose pattern.** FormDatatableEditor MUST intercept `CloseReason.UserClosing` and `Hide()` instead of disposing. Otherwise the second open from TRE Browser / IFF Editor hand-off throws `ObjectDisposedException`. Apply from the start, not in response to a smoke-discovered crash.
- **Hand-rolling a CRC for `DT_HashString`.** Use the SOE CRC variant (port of `Crc::normalizeAndCalculate`). Different CRC = different stored int32 = client cannot resolve the string. Test the port against known SOE-side reference values (`combat_default_actions` etc. if available, otherwise round-trip-only).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| EA-IFF-85 chunk parse | Custom byte walker | `UtinniCoreDotNet.Formats.Iff.IffReader` (Phase 4/7) | Already handles `IffReader` no-pad quirk fix (Phase 7 commit `7012d82`, memory `project_swg_iff_no_pad`); known-green via Phase 8's 103 IFF tests + roundtrip-iff golden. |
| EA-IFF-85 chunk serialize | Custom chunk emitter | `UtinniCoreDotNet.Formats.Iff.IffWriter` + `MutableIffDocument` (Phase 8 D-01/D-07) | Hybrid DOM + 64 MB cap + checked long roll-up already proved out by Phase 8 (`IffWriterTests.cs` 17 tests + `roundtrip-iff` goldens). Phase 9's typed writer builds a `MutableIffDocument` and calls `IffWriter.Write`. |
| File save with stale-bytes barrier | Naked FileStream.Write | `TheJawaToolboxDotNet.Saving.IffSaveTargets.SaveToPath / SaveLooseOverride / SaveInPlace` (Phase 8 D-05.1/2) | Provides Flush(true) MEDIUM-9 barrier, root-containment via `LooseOverridePath.Resolve`, error-message normalization, off-UI-thread Task wrap. |
| `.tre` repack | Manual TOC + name-block rebuild | `TheJawaToolboxDotNet.Saving.TreRepackSaveTarget.Apply(target, bytes, createBackup)` (Phase 8 D-05.4) | Already does full rebuild via `TreWriter.Repack`, atomic File.Replace, locked-archive probe via `TreRepackLock`, timestamped backup via `TreBackupPath`. Phase 9 just calls it with `IffWriter.Write` output. |
| Live-client reload tier routing | Inline switch in the editor | `TheJawaToolboxDotNet.Saving.ClientReloadDispatcher.Dispatch(savedPath, "DTII")` + framework `ReloadAssetClassifier.Classify(".iff", "DTII")` (Phase 8 D-06 tier (b)) | Pre-existing routing-table test (22 cases) already covers DTII → PendingNextSceneChange. Game-thread marshal + Game.IsRunning gate built in. |
| Path traversal defense for loose-override targets | `Path.Combine` + manual checks | `UtinniCoreDotNet.Saving.LooseOverridePath.Resolve` (Phase 8 D-05.1) | 14-Fact test covering null/rooted/`..`/alt-separator/prefix-match-attack. CI-coverable framework-side. |
| TRE record index resolution from offset | Linear scan in plugin | `UtinniCoreDotNet.Formats.Tre.TreRecordIndexResolver.ResolveOrUnknown` (Phase 8 W-3) | Degraded-to-Unknown fallback honors the W-3 contract; 4-Fact coverage. |
| Risk-proportional confirm modal | New WinForms modal | `TheJawaToolboxDotNet.UI.Forms.FormSaveConfirmDialog` (Phase 8) | Per-call modal (`using (var dlg = …)`); `Color.Red` body emphasis + explicit-verb buttons. Reuse for column-reorder/delete safety-net (UI-SPEC assumption #5: relabel `showBackupCheckbox` to "Don't ask again this session"), repack confirm, and discard-while-dirty. |
| Themed control suite | Custom TextBox/Button/etc. | `UtinniCoreDotNet.UI.Controls.{UtinniButton, UtinniTextbox, UtinniLabel, UtinniContextMenuStrip, UtinniNumericUpDown, UtinniToggleButton, UtinniComboBox}` | Existing dark-theme palette via `Colors.*()`. UI-SPEC § Design System mandates reuse. |
| CSV parsing | Naked `String.Split(',')` | Hand-roll a small RFC 4180-ish parser with UTF-8 BOM detection + double-quote escape | `String.Split(',')` breaks on quoted commas + escaped quotes. UI-SPEC locks Excel-compatible double-quote escape + UTF-8 BOM (SOE convention). The parser is < 150 lines; suggested location: `TheJawaToolboxDotNet/Saving/DatatableCsvSerializer.cs`. (BCL options are limited: `Microsoft.VisualBasic.FileIO.TextFieldParser` exists but assembly-pulling `Microsoft.VisualBasic.dll` for one helper is over-weight; planner discretion if they prefer the BCL dependency.) |
| WinForms DataGridView column sort | Manual sort + redraw | Built-in `DataGridViewColumn.SortMode = SortMode.Automatic` + header-click | View-only per D-09; `DataGridView` provides ▲/▼ indicator + sort glyph at `Colors.Font()`. Underlying model order is unchanged (sort is BindingSource view-only). |
| DataGridView selection multi-cell highlight | Custom cell paint | `MultiSelect = true` + `DefaultCellStyle.SelectionBackColor = Colors.Secondary()` | Already in UI-SPEC § ThemedDataGridView token map. |

**Key insight:** Phase 9 is unusually leveraged on Phase 8: 100% of the save plumbing, 100% of the reload routing, 100% of the path-defense + repack-orchestration, and the entire FormSaveConfirmDialog + IffChunkTree TJT-side pattern are reusable verbatim. The only genuinely new managed surface is (a) the typed DTII format primitives in the framework, (b) the `ThemedDataGridView` themed wrapper TJT-side, (c) the per-type cell widgets, (d) the cascade + CSV preview modals, and (e) the CSV serializer. The IFF Editor (Phase 8) does about 2× the new-code work Phase 9 does, and Phase 9 inherits all of it.

## Runtime State Inventory

> Phase 9 is a greenfield additive phase. No rename / refactor / migration is involved. **Section omitted** per researcher instruction (no rename/refactor trigger).

## Common Pitfalls

### Pitfall 1: DT_Comment columns are skipped in COLS/TYPE/ROWS chunks (writer-side asymmetry)
**What goes wrong:** Naive port of the load path that round-trips a V0001 datatable with a DT_Comment column will produce a different `numCols` on save (DataTableWriter skips comment columns in all three chunks per `DataTableWriter::_saveColumns / _saveTypes / _saveRows` — verified `DataTableWriter.cpp:821-840 / 844-855 / 860-911`).
**Why it happens:** SOE's writer treats comment columns as "schema-only / not on disk" — they exist in the spreadsheet input only. But Phase 9 is loading FROM disk (a `.tab` that was already written without comment columns) and writing back — there are NO DT_Comment columns in the on-disk schema to begin with for files that round-trip through `DataTableWriter`. **However**, files authored by a different tool MIGHT carry DT_Comment columns on disk; or files imported via the SOE tool from a spreadsheet might preserve them in the source spreadsheet but not in the .tab. **Practical disposition:** since the .tab file is the ground truth (no spreadsheet path here), Phase 9's loader will simply not encounter DT_Comment columns in real SWG-shipping `.tab` files. If a hand-crafted .tab with DT_Comment is encountered, parse-and-preserve per the CF-04 byte-preservation invariant.
**How to avoid:** Document this asymmetry in `DataTableDocument` XML comment. Add a fixture test that constructs a synthetic DT_Comment-containing .tab and confirms round-trip preserves it byte-for-byte via the hybrid-DOM original-byte slice. Do NOT replicate the SOE writer's "skip comment on output" — that would corrupt the round-trip contract.
**Warning signs:** Hash mismatch on `roundtrip-tab` golden when the fixture contains a DT_Comment column.

### Pitfall 2: Type-spec parsing — CONTEXT documents the WRONG enum syntax
**What goes wrong:** CONTEXT D-03 says `DT_Enum → dropdown sourced from the column's type-spec string ('e[a:0,b:1,c:2]')`. This is **incorrect**. The actual SOE syntax (verified `DataTableColumnType.cpp:142-153`) is `e(a=0,b=1,c=2)[default]`: parentheses for the enum list, `=` for label/value separator, square brackets reserved for the optional default value.
**Why it happens:** CONTEXT was likely paraphrased from memory rather than verified against source.
**How to avoid:** Document the correct grammar in `DataTableColumnType.cs` XML comments AND in the planner's per-type widget contract. Update UI-SPEC § Per-type cell widget contract row `DT_Enum` to say `e(label=val,label=val,…)[default]`. The parser implementation:
- First char (lowercase) = type discriminator (`i`/`f`/`s`/`c`/`h`/`p`/`b`/`e`/`v`/`z`).
- Optional `[default]` at the end (any position; uses `getDelimStr(desc, '[', ']')`).
- For `e` / `v` / `z`: enum list inside `(...)`, comma-separated `label=value` pairs.
- `z` is a cross-table enum (load from referenced .tab file at `DataTableManager::getTable`) — **out of scope per CONTEXT D-05**; if encountered, treat as `DT_Unknown` per UI-SPEC R-03 disposition.
**Warning signs:** A column with enum-typed values fails to parse with a "no enum members" error. Or a default value is mis-parsed.

### Pitfall 3: Strings are NUL-terminated, NOT length-prefixed (in chunk data)
**What goes wrong:** Naive port assumes length-prefixed strings (which IS the format for Unicode strings via `Iff::insertChunkString(Unicode::String &)` — int32 length + UTF-16 chars per `Iff.cpp:906-910`). DTII uses the ASCII variant `Iff::insertChunkString(const char *)` per `Iff.cpp:893-897` which writes `istrlen(s)+1` bytes (NUL inclusive). The reader's `read_string` walks to NUL.
**Why it happens:** Two SOE string formats coexist in the IFF library; only one is used by DTII.
**How to avoid:** `ReadNulStrings(payload, offset, count)` in `DataTableDocument.cs` reads count NUL-terminated ASCII strings starting at offset. Validate the payload's last byte is NUL (defense-in-depth for malformed inputs). Empty strings = single NUL byte.
**Warning signs:** First column name has trailing NUL char in the C# string OR second column name is empty / starts with garbage.

### Pitfall 4: `DT_HashString` cells store the int32 hash on disk, NOT the source string
**What goes wrong:** A naive cell model stores the typed value as `string` for all `DT_HashString` cells and re-emits it on save. The reader gets back an int32 (no string round-trip).
**Why it happens:** Per `DataTableColumnType.cpp:429-441` `mangleValue()` for `DT_HashString`: the value is replaced by `Crc::normalizeAndCalculate(s)` and the integer is what gets stored. The source string is GONE on disk.
**How to avoid:** `MutableDataTableCell` for a `DT_HashString` column stores the int32 (computed once via the CRC port at edit/load time), NOT the string. The UI surfaces a string editor + computed-hash preview per UI-SPEC § Per-type cell widget contract; the editor commit fires `DataTableHashCrc.Compute(text) → int32` and stores the int32 in the cell. The string for display is recomputed only at edit time; the model is integer-typed. **This means CSV export of a DT_HashString column emits the integer, not the original string** — unless the editor maintains a side-table of "last typed string" per cell (planner discretion, but not required for byte parity).
**Warning signs:** Round-trip CSV export → re-import of a DT_HashString column produces integers in the cell values instead of strings.

### Pitfall 5: Singleton-form `ObjectDisposedException` on second open (Phase 8 smoke-discovered)
**What goes wrong:** Plugin.cs registers ONE FormDatatableEditor at MEF load (`GetForms()` list). User closes the form via the X button → default WinForms behavior disposes it. Next `Show()` (from TJT host window menu OR TRE Browser/IFF Editor hand-off) throws `ObjectDisposedException` at `Form.CreateHandle`.
**Why it happens:** Plugin-registered singleton forms have ONE reference held by the editor host; default WinForms dispose-on-close violates that ownership.
**How to avoid:** Apply the hide-not-dispose intercept from the start (verified `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` smoke commit `b899504`):
```csharp
private void FormDatatableEditor_FormClosing(object sender, FormClosingEventArgs e)
{
    // Best-effort INI save (geometry, splitter, find/replace visibility, edit-comment toggle, loose-override dir).
    TrySaveSettings();
    if (e.CloseReason == CloseReason.UserClosing)
    {
        e.Cancel = true;
        Hide();
        return; // Do NOT dispose; keep instance for next .Show().
    }
    // Editor-host shutdown (ApplicationExitCall / TaskManagerClosing / WindowsShutDown) — fall through, dispose normally.
}
```
**Warning signs:** Live-SWG smoke shows a JIT debugger pop on the second TRE Browser → "Open in Datatable Editor" right-click. Phase 8 hit this defect twice (FormIffEditor `b899504` + FormTreBrowser defensive `ce2a0a4`); Phase 9 must NOT re-encounter it.

### Pitfall 6: Per-call modal lifecycle is NOT the same as singleton-form pattern
**What goes wrong:** Confusing the singleton-form hide-not-dispose pattern (Pitfall 5) with per-call modal lifecycle (`FormSaveConfirmDialog`, `FormTypeChangeCascadeDialog`, `FormCsvImportPreviewDialog`). Applying hide-not-dispose to a per-call modal causes a memory leak (the modal stays Hide()-d in memory forever instead of disposing).
**Why it happens:** The two patterns look superficially similar.
**How to avoid:** Per-call modals follow `using (var dlg = new FormX(…)) { dlg.ShowDialog(this); }` — default WinForms dispose-on-close is CORRECT. The `using` block disposes. Document this distinction in the new modal classes' XML comments. Verified in Phase 8 SUMMARY 8-06 decision: "FormSaveConfirmDialog is per-call modal (`using (var dlg = new ...)` → ShowDialog → using-disposes). Default WinForms dispose-on-close is CORRECT for per-call modals."
**Warning signs:** A Phase 9 reviewer finds `Hide()` in a modal Form's FormClosing handler — REJECT.

### Pitfall 7: DataGridView performance with hundreds of rows × dozens of columns (CombatDataTable scale)
**What goes wrong:** Real production tables like `CombatDataTable` have hundreds of rows × dozens of columns. Without virtual mode, the DataGridView pre-renders every cell at bind time. With `CellFormatting` overlays for search-match / dirty / needs-review (the Phase 9 UI-SPEC contract), each cell's paint runs custom code. Worst case: opening the file takes several seconds; search highlighting jitters on every keystroke; sort takes a noticeable pause.
**Why it happens:** Default DataGridView is not designed for tens of thousands of cells.
**How to avoid:** Measure first. If `CombatDataTable` opens in < 1 second and search/sort feel snappy, V1 ships non-virtual per UI-SPEC § Grid surface (no virtual mode required). If profiling shows jank, the V1 fallback is to debounce the `CellFormatting` triggers (Find/Replace pane idle timer of 200 ms before painting search-match BackColors). Virtual mode is V2 — it requires reworking the cell-state overlay strategy (needs-review badge per row would need a `CellValueNeeded` lookup against the in-memory model).
**Warning signs:** Profiler shows > 500 ms in `OnCellFormatting`; user-reported "editor feels sluggish when typing in Find."

### Pitfall 8: Path-CRC for .tre-repacked DTII files (carries forward from Phase 8 Open Q1)
**What goes wrong:** When the user saves via mode 4 (`.tre` repack) for an `.tab` file, the repacked archive's TOC name-CRC must match what the SWG client computes for the same path. Phase 8 deferred the cursor N-H1 live-client ACK to Open Q1 (live-SWG smoke residual). If the CRC is wrong, the client cannot resolve the path on next scene change and the edit appears to silently no-op.
**Why it happens:** `.tre` TOC stores a path-name CRC; if the writer's CRC doesn't match the client's, the entry is invisible.
**How to avoid:** The Phase 8 `TreWriter.Repack` already copies raw name bytes verbatim from the source (`GetRecordNameBytes` per `TreFile.cs` round-2 MEDIUM 6) so the CRC field is preserved bit-for-bit for untouched entries. For the EDITED entry, the stored Checksum field is preserved per `TreRepackLogicalPathTests.cs` (Phase 8 Plan 7). This is structurally identical to Phase 8's repack for IFF files — Phase 9 inherits the structural correctness. The LIVE ACK that the client resolves the new path is still the deferred Tier-4 residual.
**Warning signs:** Live-SWG smoke shows `.tre` repack appears to "succeed" but the client doesn't pick up the change on next scene load. Deferred per CF-03 inheriting Phase 8's Open Q1; Phase 9 documents but does not chase.

### Pitfall 9: Sort vs save-order divergence (D-09 view-only contract)
**What goes wrong:** User column-sorts the grid to make finding a row easier, then clicks Save. If save iterates rows in display order (BindingSource sorted view), the on-disk order changes silently → byte-exact SC4 fails AND every engine consumer that reads rows by index breaks.
**Why it happens:** `DataGridView`'s sort is applied at the `BindingSource` layer; iterating `dataGridView1.Rows` returns rows in display order, NOT model order.
**How to avoid:** Save MUST iterate `MutableDataTableDocument.Rows` directly (the in-memory model collection), NEVER `DataGridView.Rows`. The reorder commands (`MoveRowUp` / `MoveRowDown`) are the ONLY way to change save order, and they mutate the model directly. Add an explicit grep-gate to the writer: `grep -c "dataGridView.Rows" DatatableSaveTargets.cs` MUST be 0.
**Warning signs:** `roundtrip-tab` golden hashes differ for files where the user happened to sort by a column before saving.

### Pitfall 10: ChromaDB-style "cleared cache" reload assumption
**What goes wrong:** Naive reload UX promises "your edit is live" after a file save when in fact `DataTableManager` caches the parsed table and only invalidates on `reload(name)` / `reloadIfOpen(name)` / scene change.
**Why it happens:** Per `DataTableManager.h:25-30`: `getTable` returns the cached `DataTable*`; cache invalidation requires explicit `reload(name)`. The TJT chat-command-parser scene-change path is the user-driven invalidation trigger; CF-05 acknowledges this candidly.
**How to avoid:** **Strictly inherit CF-05** — Reload-in-client button writes the locked CF-05 status copy and a subtle accent pulse (UI-SPEC § States, "Reload triggered (tier-(b) datatable case — CF-05)"). Do NOT call any `reload()` binding even if one exists in the C++ — none is exposed to managed (verified `ClientReloadDispatcher.cs` does NOT call DataTableManager.reload, only Graphics.ReloadTextures / GroundScene.Get().ReloadTerrain).
**Warning signs:** Plan tries to introduce a `DataTable.Reload` binding to managed code — REJECT per CF-05.

## Code Examples

### Read a DTII version-FORM (V0 vs V1 branching)

```csharp
// Source: D:/Code/swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTable.cpp:444-461. [CITED]
var ver = root.Children[0]; // root = FORM DTII; ver = FORM 0000 or FORM 0001
if (ver.TypeId != "FORM" || (ver.SubTypeId != "0000" && ver.SubTypeId != "0001"))
    throw new DataTableParseException("Unknown DataTable file format [" + ver.SubTypeId + "]");

// Both versions have COLS, TYPE, ROWS as the three required leaf chunks. The DIFFERENCE:
//   V0: TYPE is `numCols × int32` (enum value of DataType — only DT_Int/DT_Float/DT_String supported)
//   V1: TYPE is `numCols × NUL-terminated type-spec string` (full type-spec — all 10 DT_* types)
```

### Read the TYPE chunk (V0 → V1 conversion semantics)

```csharp
// V0 → constructs DataTableColumnType from a single char ("i", "f", or "s")
// V1 → constructs DataTableColumnType from a full spec ("i", "f", "s", "c", "h", "p", "b", "e(...)[def]", "v(...)[def]", etc.)
// Source: D:/Code/swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTable.cpp:498-535 (V0); :578-583 (V1). [CITED]
private static IReadOnlyList<DataTableColumnType> ReadV0Types(byte[] payload, int numCols)
{
    var result = new List<DataTableColumnType>(numCols);
    int offset = 0;
    for (int i = 0; i < numCols; i++)
    {
        int dt = ReadInt32Le(payload, offset); offset += 4;
        // Per DataTable.cpp:502-534 — V0 only supports DT_Int/DT_Float/DT_String:
        switch (dt)
        {
            case 0: result.Add(new DataTableColumnType("i")); break; // DT_Int
            case 1: result.Add(new DataTableColumnType("f")); break; // DT_Float
            case 2: result.Add(new DataTableColumnType("s")); break; // DT_String
            default: throw new DataTableParseException("Unknown V0 column type: " + dt);
        }
    }
    return result;
}
```

### `mangleValue` port for the D-04 cascade

```csharp
// Source: D:/Code/swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTableColumnType.cpp:382-473. [CITED]
public bool MangleValue(ref string value)
{
    // Empty value -> default (or fail if default is "required"/"unique").
    if (string.IsNullOrEmpty(value))
    {
        if (_defaultValue == "required" || _defaultValue == "unique") return false;
        value = _defaultValue;
    }
    // DT_PackedObjVars: parse-validate the name|type|value grammar; trailing $| is sentinel.
    if (Type == DataType.DT_PackedObjVars && !ValidatePackedObjVars(value)) return false;
    // Pass-through for DT_String/DT_Comment/DT_Int and DT_PackedObjVars whose basic type is DT_String.
    if (BasicType != DataType.DT_Int || Type == DataType.DT_Int) return true;
    // Complex-type-with-DT_Int-basic mangling:
    switch (Type)
    {
        case DataType.DT_Bool:        return value == "0" || value == "1";
        case DataType.DT_HashString:  value = DataTableHashCrc.Compute(value).ToString(); return true;
        case DataType.DT_Enum:        { if (LookupEnum(value, out int v))    { value = v.ToString(); return true; } return false; }
        case DataType.DT_BitVector:   { if (LookupBitVector(value, out int v)) { value = v.ToString(); return true; } return false; }
        default: return false;
    }
}
```

### Mirror the Phase 8 FormIffEditor.BuildSaveMenu pattern for FormDatatableEditor

```csharp
// Source: D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs:896-928. [VERIFIED]
private void BuildSaveMenu()
{
    saveMenu = new UtinniContextMenuStrip();
    miSaveInPlace      = new ToolStripMenuItem("Save (in place)")            { /* handler */ };
    miSaveLooseOverride = new ToolStripMenuItem("Save as loose override")     { /* handler */ };
    miSaveAs           = new ToolStripMenuItem("Save As…")                    { /* handler — ALWAYS enabled per MEDIUM 5 */ };
    miPatchLive        = new ToolStripMenuItem("Patch live client (in memory)") { Enabled = false,
        ToolTipText = "Live patch requires opening from client memory — not wired in this phase." };
    miRepackTre        = new ToolStripMenuItem("Repack into source .tre…")    { Enabled = false,
        ToolTipText = "Open from a packed .tre to repack the source archive." };
    saveMenu.Items.AddRange(new ToolStripItem[]
    {
        miSaveInPlace, miSaveLooseOverride, miSaveAs,
        new ToolStripSeparator(),
        miPatchLive, miRepackTre,
    });
}

// RefreshSaveMenuEnabledState — pattern-match Source against the 4 OpenSource cases and gate.
// VERBATIM SHAPE from FormIffEditor.cs:933-1010. Phase 9 adds: when controller has any "needs review"
// cells (D-04 cascade), ALL Save items DISABLE with the locked tooltip "Resolve {N} cell(s) that need
// review before saving." per UI-SPEC R-04.
```

### Mirror the Phase 8 OpenFromTreEntry for FormDatatableEditor

```csharp
// Source: D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs:185-235. [VERIFIED]
private void OnOpenInDatatableEditor(object sender, EventArgs e)
{
    PathNode pn = tvTre.SelectedNode != null ? tvTre.SelectedNode.Tag as PathNode : null;
    if (pn == null || !pn.IsLeaf) return;
    TreEntryDescriptor d;
    if (_index == null || !_index.TryGetDescriptor(pn.FullPath, out d)) return;
    Task.Run(() =>
    {
        try
        {
            bool ok = TrePayloadResolver.TryResolve(d, out byte[] payload);
            if (!IsHandleCreated) return;
            BeginInvoke((Action)(() =>
            {
                if (!ok) { lblStatus.Text = "Cannot open " + pn.FullPath + " — payload is enumerate-only."; return; }
                FormDatatableEditor editor = FindOrCreateDatatableEditor();
                if (editor == null) { lblStatus.Text = "Datatable Editor is unavailable in this session."; return; }
                editor.OpenFromTreEntry(payload, d.ResolvedArchivePath, pn.FullPath, d.ArchiveLocalOffset);
                editor.Show(); editor.Activate();
            }));
        }
        catch (Exception ex)
        {
            if (IsHandleCreated) BeginInvoke((Action)(() =>
            {
                lblStatus.Text = "Open-in-Datatable-Editor failed: " + ex.Message;
                lblStatus.ForeColor = Color.Red;
            }));
        }
    });
}

// In FormDatatableEditor — OpenFromTreEntry mirrors FormIffEditor.OpenFromTreEntry:
public void OpenFromTreEntry(byte[] payload, string archivePath, string logicalPath, long offset)
{
    var src = TreRecordIndexResolver.ResolveOrUnknown(archivePath, offset, logicalPath); // W-3
    using (var ms = new MemoryStream(payload))
    {
        var iffDoc = IffReader.Read(ms);
        var mIff = MutableIffDocument.FromDocument(iffDoc, payload);
        var dtDoc = DataTableDocument.FromIff(mIff);
        LoadDocument(dtDoc, src, Path.GetFileName(logicalPath));
    }
}
```

### Build a `MutableIffDocument` from a `MutableDataTableDocument` for serialize

```csharp
// Composes IffWriter.Write on a freshly-constructed MutableIffDocument tree:
//   FORM DTII { FORM <version> { COLS, TYPE, ROWS } }
// Per-cell: if !cell.IsDirty && cell.OriginalSlice != null, write OriginalSlice; else fresh-serialize.
// Per-column (in CHUNK BODY): each chunk is a leaf MutableIffNode whose payload is the
// concatenated bytes; clean columns can shortcut by re-emitting their loaded slice IF unchanged
// (planner discretion — finer-grained per-cell preservation is the V1 invariant).
public byte[] Serialize()
{
    var dtii = MutableIffNode.NewContainer("FORM", "DTII");
    var ver  = MutableIffNode.NewContainer("FORM", Version); // "0000" or "0001"
    dtii.AddChild(ver);
    ver.AddChild(BuildColsChunk());
    ver.AddChild(BuildTypeChunk());
    ver.AddChild(BuildRowsChunk());
    var doc = new MutableIffDocument(dtii);  // wrap as the writer's root
    return IffWriter.Write(doc); // Phase 8's writer handles BE chunk headers, length roll-up, 64 MB cap.
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| SOE `SwgDataTableTool` (read+write `.tab` via spreadsheet UX with separate per-table editor windows) | TJT Datatable Editor SubPanel — same T4 mutation scope, integrated with TRE Browser hand-off + IFF Editor hand-off + CSV import/export | This phase | Replaces a separate SOE-era tool with the Wave-1 integrated editor; user no longer juggles `SwgDataTableTool` alongside Utinni. |
| Spreadsheet (XML/TSV) authoring → DataTableWriter→.iff (V1 ships authoring) | Edit `.tab` directly + CSV delta-import path (V1 ships editing; XML/Excel authoring is V2) | This phase | Modders iterate on existing `.tab` without round-tripping through Excel; CSV import provides a lighter bulk-edit alternative. |
| Hand-rolled cell parsers in every consumer | Centralized `DataTableColumnType.MangleValue` port (CF-04 byte preservation + D-04 cascade) | This phase | One coercion routine matches SOE engine semantics; no drift between editor strict-validation and runtime cell read. |

**Deprecated/outdated:**
- `SwgDataTableTool`: superseded by Phase 9. Source code stays as `D:/Code/swg-client-v2/src/engine/shared/application/DataTableTool/src/shared/DataTableTool.cpp` for spec-reference only.
- `DataTableColumnType::DT_Comment` write-side skip in `_saveRows`: NOT replicated in Phase 9 (would break the byte-preservation invariant for hand-crafted .tab files; see Pitfall 1).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | CONTEXT D-03's enum syntax `e[a:0,b:1,c:2]` is incorrect; actual SOE syntax (verified `DataTableColumnType.cpp:142`) is `e(a=0,b=1,c=2)[default]`. | Pitfall 2 | Per-type widget contract and parser implementation will both be wrong if not corrected; the `e[a:0,b:1,c:2]` form is `[ASSUMED]` from a CONTEXT typo while `e(a=0,b=1,c=2)[default]` is `[VERIFIED: D:/Code/swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTableColumnType.cpp:142-153]`. **Planner action: update UI-SPEC § Per-type cell widget contract + DataTableColumnType.cs XML comment to use the verified form.** |
| A2 | Real-world SWG `.tab` files do NOT carry DT_Comment columns on disk (because SOE's writer skipped them). | Pitfall 1 | If a hand-crafted .tab includes DT_Comment, our byte-preservation contract still works (we just round-trip whatever's on disk), but the UI-SPEC frozen-header behavior assumes the parser builds a comment column from disk data. **Planner: add a synthetic DT_Comment fixture to validate; if a real-world example surfaces, no code change needed.** `[ASSUMED]` — based on reading DataTableWriter.cpp's `_saveRows` skip-comment logic; no in-repo .tab corpus to verify against. |
| A3 | `Crc::normalizeAndCalculate` is the SOE CRC32 variant with a SOE-specific normalize step (lowercase + whitespace handling). | Pattern §, Pitfall 4, Pattern §, Pattern § (mangleValue) | If the port computes a different CRC, DT_HashString cells won't match SWG client-side lookups → silent semantic corruption. **Planner: port `Crc::normalizeAndCalculate` carefully; verify against known reference values from a sample .tab if possible.** `[ASSUMED]` — only `DataTableColumnType.cpp:434` is verified; the actual `Crc` class lives in `sharedFoundation/Crc.{h,cpp}` and was not read for this research. |
| A4 | DTII never uses the Unicode string format (`Iff::insertChunkString(Unicode::String &)` — int32 length + UTF-16 chars). All DTII strings are ASCII NUL-terminated. | Pattern §, Pitfall 3 | If a real-world .tab uses Unicode strings, the parser will misread the int32 length as a chunk byte. `[VERIFIED: DataTableWriter.cpp:837 / :851 / :895 call insertChunkString(const char *) only]` for the writer side; `[ASSUMED]` for the read side (no V0001 .tab fixture in-repo to confirm against). |
| A5 | `DataGridView` performance is acceptable for CombatDataTable-scale tables (~hundreds of rows × dozens of columns) without virtual mode. | Pitfall 7 | If profiling shows jank, V1 ships with debouncing + CellFormatting throttling; V2 introduces virtual mode. `[ASSUMED]` — no Wave-1 measurement against an actual CombatDataTable.tab. Planner should budget a profiling task in plan 3 (DataGridView editor) and tune if needed. |
| A6 | Real `.tab` files are sourced primarily from `D:/Code/swg-client-v2/data/` extractions (SWG client `.tre` archives unpacked) OR direct extraction from SWGEmu / Restoration clients via TRE Browser hand-off. | Validation Architecture § | No `.tab` files were found under `D:/Code/swg-client-v2/data/` in this research (zero matches via `find`). For golden fixtures, planner must EITHER (a) build synthetic .tab files via a `DataTableFixtureBuilder` (mirrors `Utinni.Cli.Tests/Infrastructure/TreFixtureBuilder.cs` and `IffBuilder.cs` patterns — recommended) OR (b) extract one or two small real .tab files from a live SWG client and check in. `[ASSUMED]` — synthetic fixtures via builder is the safer + lower-friction choice. |
| A7 | The TRE Browser visibility predicate for "Open in Datatable Editor" can use `extension == ".tab" OR root IFF tag == "DTII"`. The root-tag check requires a cheap byte-scan (just read the first ~12 bytes to extract the root FORM's sub-type). | Architectural Responsibility Map (entry-point row), UI-SPEC § Copywriting | Wrong predicate would either hide the menu on legitimate .tab files (extension mismatch — e.g., .iff carrying DTII) or surface it on irrelevant entries (some IFFs with .tab extension but non-DTII content). `[ASSUMED]` — the cheap byte-scan is per `Utinni.Cli/Commands/InspectIffCommand.cs` precedent which reads just the IFF root chunk. Planner should reuse a helper from `IffReader` if one exists, else add a small `IffReader.TryPeekRootTypeId(stream)` method. |
| A8 | The CSV format Phase 9 ships is RFC 4180 + UTF-8 BOM (SOE convention) + Excel-compatible double-quote escape + header row format `name` (no `:type` annotation by default). | Pattern 5 §, UI-SPEC § Copywriting (CSV import preview heading) | If users expect a richer header format (`name:type` to surface schema in the CSV), the importer will reject mismatching schemas. `[ASSUMED]` — UI-SPEC marks CSV details as planner discretion; the bare `name` header keeps the format Excel-friendly. Planner may decide to add a comment row above the header (e.g., `# Type: i, s, h, ...`) for human inspection — but the importer needs only the column-name header to map cells. |
| A9 | The Phase 9 typed model can reuse Phase 8's `MutableIffDocument` directly as its byte-source root, with `DataTableDocument` holding a back-reference to it for re-serialization. No separate "raw bytes" storage in the typed model. | Pattern 2 §, Pattern § Build a MutableIffDocument | If the planner decides to detach the typed model from the IFF document (e.g., for V2 "new .tab from scratch"), this becomes a refactor. `[ASSUMED]` — for V1, every typed `DataTableDocument` originates from a `MutableIffDocument` (Pattern 1's FromIff requires it); the back-reference enables re-serialization via `IffWriter.Write` without rebuilding a fresh IFF tree from scratch. |
| A10 | Live SWG smoke for Phase 9 follows Phase 8's "automation-augmented" precedent — on-disk save contracts are automated against golden fixtures; the live-client ACK (next scene change picks up the edit) is the Tier-4 maintainer-driven residual. | Validation Architecture § | If reviewers (codex / cursor) push back on automation-augmented for the Datatable Editor specifically, plan may need to add a hard live-SWG smoke gate. `[ASSUMED]` — Phase 8 Plan 7 (Open Q1, Open Q5) set the precedent; Phase 9 has identical risk shape (no in-memory live patch, only file-based save modes 1/2/4). |

**If this table is empty:** All claims in this research were verified or cited — no user confirmation needed.

**Action items for the planner from the Assumptions Log:**
- A1: Correct the enum-syntax form in UI-SPEC § Per-type cell widget contract + planner artifact references.
- A3: Budget effort to port `Crc::normalizeAndCalculate` faithfully; add reference-value validation if any are accessible.
- A6: Build `DataTableFixtureBuilder` for synthetic fixtures (V0 + V1 minimal + per-DT_* + DT_Comment); planner may also instruct executor to extract one small real `.tab` via TRE Browser → save loose for ground-truth comparison.
- A8: Confirm CSV header schema (bare `name` vs `name:type` annotation) before sealing the CSV importer's parser contract.

## Open Questions

1. **`Crc::normalizeAndCalculate` exact algorithm**
   - What we know: `DataTableColumnType.cpp:434` calls it on `DT_HashString` cells; `Crc::crcNull` is the empty-string sentinel.
   - What's unclear: The exact CRC32 polynomial + normalize semantics (lowercase? trim whitespace? path-separator normalize?).
   - Recommendation: Locate `sharedFoundation/Crc.{h,cpp}` and port the implementation 1:1 in `DataTableHashCrc.cs`. Add a unit test against a known reference value (the planner / executor can hash an SOE-source string in C++ and use that as the test expectation, OR round-trip a real `.tab` containing DT_HashString cells and assert the int32 storage matches).

2. **DT_PackedObjVars / DT_BitVector parse-back depth**
   - What we know: CONTEXT marks these as planner discretion. UI-SPEC § Per-type cell widget contract sets the floor: text input + format syntax hint via `ToolTipText`.
   - What's unclear: Whether the parse-back validator on `CellValidating` is `[ASSUMED]` strict (full grammar) or lenient (accept anything, let `mangleValue` reject at save).
   - Recommendation: Strict at commit (per D-03 strict edit-time validation). The parse functions exist in `DataTableColumnType.cpp:44-69` (`consumePackedObjVarIntField` / `consumePackedObjVarStringField`) — port them for the validator. Cost is < 50 lines.

3. **`.tab` files in-repo for golden fixtures**
   - What we know: No `.tab` files exist under `D:/Code/swg-client-v2/data/` (verified `find` returned no matches). Phase 4/7/8 use synthetic IFF/TRE fixtures built in test code via `IffBuilder` + `TreFixtureBuilder`.
   - What's unclear: Whether any small real-world `.tab` is committable (CONTEXT line 109 references `CombatDataTable.tab` but does not point at an in-repo path).
   - Recommendation: Use the synthetic-fixtures-via-builder pattern. Build a `DataTableFixtureBuilder` in `Utinni.Cli.Tests/Infrastructure/` that emits valid DTII bytes for V0 minimal + V1 minimal + per-DT_* + DT_Comment-row variants. This mirrors `IffBuilder` and `TreFixtureBuilder`. If the maintainer later wants to add a small real fixture from a live client, the harness already accepts a path argument.

4. **CSV header schema** (`name` vs `name:type`)
   - What we know: UI-SPEC marks this as planner discretion; Excel/Sheets users typically expect bare column names; the SOE spreadsheet format used row 1 = names, row 2 = type-spec.
   - What's unclear: Whether modders editing in Excel expect to see types alongside names for sanity-checking.
   - Recommendation: Default to bare `name` header (Excel-friendly). Add an optional second header row (commented with leading `#`) emitting the type-spec string for human inspection, ignored on import. This survives an Excel round-trip (Excel preserves leading-# rows as text).

5. **Whether `DataTableManager` reload semantics differ between SWGEmu 0004/0005 and Restoration 5000+**
   - What we know: Per memory `project_tre_version_support_gap`: Restoration uses v5000/v6000 .tre archives; v6000+ payloads are encrypted. Phase 7 reader enumerates v6000 but cannot decrypt; payload bytes are inaccessible for Restoration v6000+ assets without per-archive decryption keys.
   - What's unclear: Whether a `.tab` extracted from a v6000 archive that the user re-saves via mode 4 (`.tre` repack) preserves the encryption envelope OR drops it.
   - Recommendation: **The `.tre` repack flow refuses v6000 archives** per Phase 8 WR-06 fix (verified via Phase 8 SUMMARY 8-07; `TreWriter` round-trip refuses encrypted payloads). Phase 9 inherits this — datatable repack into a v6000 source archive is REFUSED with the locked tooltip. Loose-override (mode 1) remains available because the override file is plain unencrypted .tab bytes the client searches FIRST on a higher-priority path. Document this in the Save▾ menu's per-archive disabled-tooltip if the file came from a v6000 source.

## Environment Availability

> Phase 9 is purely managed C# work depending on the existing pre-shipped toolchain (already validated by the Phase 8 successful build + green test runs).

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| VS 2026 MSBuild | All managed builds | ✓ (memory `project_vs2026_toolchain`; resolved at `D:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe`) | 18.x (v145 PlatformToolset) | VS 2022 (fallback on disk) |
| .NET Framework SDK | xUnit test compile + execute | ✓ | 4.7.2 | — |
| `dotnet test` runner | xUnit test execution (`--no-build`) | ✓ | 6.x+ | — |
| `dotnet build` for WinForms | (Forbidden — MSB3823 on .resx) | ✗ (intentional) | — | Use VS 2026 MSBuild |
| GitHub Actions Windows runner (self-hosted) | CI gate per push | ✓ (memory `project_self_hosted_ci`; at `C:\actions-runner`; v145 Insiders-only, not on GitHub-hosted images) | runner v2.x | — |
| `gh` CLI | PR/issue ops | ✓ (memory `reference_windows_toolchain_paths` — `C:\Users\kenne\bin\gh.exe`) | latest | — |
| `gsd-sdk` CLI | GSD orchestrator | ✓ (memory `reference_windows_toolchain_paths` — `C:\Users\kenne\bin\gsd-sdk`) | latest | — |
| `cursor-agent` CLI | Cross-AI review (codex + cursor) | ✓ (memory `reference_cursor_agent_cli`; at `C:\Users\kenne\AppData\Local\cursor-agent\`) | latest | — |
| `codex` CLI | Cross-AI review | ✓ (memory `feedback_codex_peer_review`; `codex exec --skip-git-repo-check -`) | latest | Paste-prompt fallback |
| `D:/Code/swg-client-v2/` reference corpus | Format spec lookup (read-only) | ✓ | git revision frozen | — |
| `D:/Code/UtinniPlugins/` sibling repo write access | Cross-repo paired commits | ✓ (memory `feedback_utinniplugins_authority` — standing) | — | — |
| Live SWG client (SWGEmu / Restoration) | Tier-4 manual smoke for CF-05 reload + actual edit ACK | ✓ (operator-driven, residual per Phase 8 precedent) | per session | Automation covers everything except the next-scene-change ACK |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** `dotnet build` for WinForms — fallback is VS 2026 MSBuild (mandatory, not optional).

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Managed framework | xUnit 2.x (`UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` SDK-style — auto-glob `**/*.cs`) |
| CLI golden harness | xUnit 2.x in `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` (SDK-style; `Infrastructure/GoldenTestRunner.cs` + `Infrastructure/InProcessCliRunner.cs` reused verbatim — same pattern as `RoundtripIffCommandTests`) |
| Native framework | Catch2 v3 (not consumed in Phase 9 — pure managed) |
| Config file | None (xUnit auto-discovers) |
| Quick run command | `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~Datatable"` (Datatable subsuite, ~< 1 second steady-state) |
| Full suite command | `dotnet test UtinniCoreDotNet.Tests --no-build` AND `dotnet test Utinni.Cli.Tests --no-build` AND `dotnet test UtinniCoreDotNet.Tests/PreservationAudit --no-build` (STAB-04 grep facts) |
| Phase gate | All three test projects green BEFORE `/gsd:verify-work` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| PROD-W1-DT (1) | Subpanel loads inside TJT against live SWG | Tier 4 manual | Live SWG smoke per Phase 8 precedent | ❌ Wave 6 (smoke plan) |
| PROD-W1-DT (2a) | Open `.tab` (loose / from TRE Browser / from IFF Editor) | xUnit + Tier 4 | `pytest`-equivalent: `dotnet test UtinniCoreDotNet.Tests --filter "FullyQualifiedName~DataTableDocumentTests"` | ❌ Wave 0 |
| PROD-W1-DT (2b) | View rows / columns / types in DataGridView | WinForms manual + xUnit on data binding | `dotnet test UtinniCoreDotNet.Tests --filter "FullyQualifiedName~DataTableColumnTypeTests"` | ❌ Wave 0 |
| PROD-W1-DT (2c) | Edit cell values | xUnit on controller + Tier 4 UI smoke | `dotnet test --filter "FullyQualifiedName~DatatableEditControllerTests.EditCell"` | ❌ Wave 0 |
| PROD-W1-DT (2d) | Save back to disk via mode 1/2/4 | xUnit on save targets (Phase 8 reuse) + golden round-trip | `dotnet test --filter "FullyQualifiedName~DatatableSaveTargets"` (thin) AND `dotnet test Utinni.Cli.Tests --filter "FullyQualifiedName~RoundtripTab"` | ❌ Wave 0 (most) |
| PROD-W1-DT (3) | Live SWG client picks up edit on relevant reload path | Tier 4 manual | Live SWG smoke — `.tab` save → next TJT scene change → in-game observation | ❌ Wave 6 |
| PROD-W1-DT (4 / SC4) | Edits preserve schema; no silent corruption | xUnit byte-exact + CLI golden gate | `dotnet test --filter "FullyQualifiedName~Roundtrip"` (multi-suite); the `roundtrip-tab --mutate-cell` golden is the structural gate | ❌ Wave 0 (CLI verb + fixtures) |
| PROD-W1-DT (T4 schema mutation — D-01) | Add/remove/reorder rows × columns × column types | xUnit per-command | `dotnet test --filter "FullyQualifiedName~DatatableEditController.{AddRow,RemoveRow,...}"` | ❌ Wave 0 |
| Type-change cascade (D-04) | `mangleValue` per DT_* matrix; "needs review" flag; save-blocked semantics | xUnit on `MangleValue` + on `ChangeColumnTypeCommand` | `dotnet test --filter "FullyQualifiedName~MangleValue"` | ❌ Wave 0 |
| CSV delta-import (D-08) | Per-cell diff preserves original bytes; preview modal counts | xUnit on `CsvImportPlan.Build` + on `ApplyCsvImportCommand` | `dotnet test --filter "FullyQualifiedName~CsvImport"` | ❌ Wave 0 |
| Column reorder/delete safety net (D-02) | Modal surfaces; session-suppress flag honored | xUnit + Tier 4 (modal is per-call) | `dotnet test --filter "FullyQualifiedName~ColumnReorderSafety"` + manual modal trigger | ❌ Wave 0 |
| Find / Replace (D-07) | Find returns expected cell coords; Replace honors type validation | xUnit on the find/replace planner; manual on the overlay paint | `dotnet test --filter "FullyQualifiedName~FindReplace"` | ❌ Wave 0 |
| Column-click sort (D-09) | Save serializes physical row order regardless of sort | xUnit on `Serialize` after grid sort | `dotnet test --filter "FullyQualifiedName~SortViewOnly"` | ❌ Wave 0 |
| Reload status (CF-05) | Reload-button click writes locked tier-(b) copy; no binding call | xUnit on `ClientReloadDispatcher` (Phase 8 reused) + manual | `dotnet test UtinniCoreDotNet.Tests --filter "FullyQualifiedName~ClientReloadDispatcher"` (existing) | ✅ (existing Phase 8 test covers DTII case) |

### Sampling Rate
- **Per task commit:** quick run `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~Datatable"` (target < 5 sec).
- **Per wave merge:** full UtinniCoreDotNet.Tests + Utinni.Cli.Tests + PreservationAudit suites; both Debug|x86 and Release|x86 MSBuild clean across both repos.
- **Phase gate:** full suite green AND `roundtrip-tab` golden green AND `/gsd:code-review 09` cross-AI gate AND Tier-4 maintainer-driven live-SWG smoke per Phase 8 precedent (smoke=automation-augmented; live ACK deferred-but-acceptable).

### Wave 0 Gaps

The vast majority of the test infrastructure is NEW. List in plan order:

- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableColumnTypeTests.cs` — per-discriminator parse + MangleValue per DT_*. ~25-30 [Fact]s.
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableHashCrcTests.cs` — CRC parity vs reference. ~4-6 [Fact]s.
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableDocumentTests.cs` — V0 + V1 fixtures; DT_Comment skip; null-cell defaults; cell-count mismatch error; per-DT_* read. ~15-20 [Fact]s.
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableWriterTests.cs` — round-trip byte-exact (no edits); per-DT_* serialize; chunk-length roll-up; over-cap chunk rejection (via Phase 8 IffWriter inheritance). ~15-20 [Fact]s.
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DatatableEditControllerTests.cs` — 10+ commands × apply/undo/redo identity; baseline-clean dirty; single-transaction CSV import; type-change cascade flags needs-review; save-blocked while needs-review > 0. ~30-40 [Fact]s (largest test file in the phase).
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DatatableFixtures.cs` — synthetic .tab builders (V0 minimal, V1 minimal, one-per-DT_*, DT_Comment row, multi-row CombatDataTable-like). Test-helper only.
- [ ] `Utinni.Cli/Commands/RoundtripTabCommand.cs` — `roundtrip-tab` verb (parse → optional `--mutate-cell row col hex` or `--mutate-cell-typed row col value` → serialize → re-parse → byte-exact-untouched-cells assertion). Mirrors `RoundtripIffCommand`.
- [ ] `Utinni.Cli.Tests/Commands/RoundtripTabCommandTests.cs` — golden suite against `DataTableFixtureBuilder`-built `.tab` files. ~10-15 [Fact]s.
- [ ] `Utinni.Cli.Tests/Infrastructure/DataTableFixtureBuilder.cs` — emit valid DTII bytes for V0 / V1 / per-DT_*; mirrors `IffBuilder.cs` + `TreFixtureBuilder.cs`.
- [ ] `TheJawaToolboxDotNet/Saving/DatatableCsvSerializer.cs` — covered by xUnit at TJT side OR (preferred) by extracting the per-cell coercion check into a framework helper and testing there. (Recommend extracting per checker B-1 — same pattern as `LooseOverridePath` / `LivePatchValidator`.)
- [ ] Framework install: N/A — pre-existing.

### Golden Fixtures Needed (Wave 0)

Per Assumption A6 + Open Question 3, all fixtures are synthetic-built via `DataTableFixtureBuilder` (no on-disk binary `.tab` files checked in):

| Fixture name | Purpose | Coverage |
|--------------|---------|----------|
| `BuildV0Minimal()` | V0 round-trip (TAG_0000) | DT_Int / DT_Float / DT_String only (per V0 type set) |
| `BuildV1Minimal()` | V1 round-trip (TAG_0001) | Same 3 types but as V1 type-spec strings |
| `BuildV1AllTypes()` | Per-type cell decoder coverage | One column for each of DT_Int / DT_Float / DT_String / DT_Comment / DT_HashString / DT_Enum / DT_Bool / DT_BitVector / DT_PackedObjVars (skipping DT_Unknown which is "ignore" per R-03) |
| `BuildV1WithDefaultsAndEnums()` | Type-spec parser edge cases | Each DT_* with non-empty default value + Enum/BitVector spec with multiple labels |
| `BuildV1WithComment()` | Comment-row UX | One DT_Comment column with sample rows (note: round-trip byte preservation per Pitfall 1) |
| `BuildV1CombatDataTableLike()` | Performance + bulk-ops coverage | ~200 rows × ~30 columns mixing types — used to validate Find/Replace / sort / CSV import scales (Pitfall 7 measurement) |
| `BuildV1EmptyTable()` | Edge case | 0 rows; columns + types present |

### Coverage Targets
- Per-DT_* cell widget: at least one test per type for parse + serialize + (where applicable) MangleValue → coercion path.
- Per-DT_* `mangleValue` cascade: full matrix of X→Y permutations that are interesting (e.g., `DT_String → DT_Int` for an integer-looking string vs a non-integer string; `DT_Int → DT_Bool` for 0/1 vs other values; `DT_String → DT_HashString` which always succeeds since CRC accepts any string).
- Column reorder: header order shifted but row data preserved (cell at logical (r, c_new) equals cell at logical (r, c_old)).
- Column add / remove: chunk lengths roll up correctly; row cell-count adjusts.
- CSV round-trip property: `export → re-import → assert no cell marked dirty AND save produces byte-identical output`.
- Find/Replace: query matches a known cell → returns expected (row, col); Replace honoring type validation: replacing `"5"` in a DT_Int column succeeds; replacing `"abc"` in a DT_Int column is rejected.

### What the planner must NOT skip

- **SC4 byte-exact-on-untouched is structurally enforced** — CF-04 (hybrid DOM with per-cell original-byte slice) + D-08 (CSV delta-import keeps unchanged cells clean) + CF-02 (`roundtrip-tab` CLI golden assertion). The plan MUST include a test that loads → calls `EditCellValue` on cell (r=0, c=0) → calls `Undo` → asserts the serialized bytes equal the loaded bytes EXACTLY (not just "round-trips parsable"). This catches a regression in original-slice invalidation or net-applied-count counting.
- **D-04 cascade save-block** — every Save▾ menu item (NOT just the top-level button) MUST be disabled when needs-review > 0, with the disabled tooltip exposing on the menu items too (per UI-SPEC R-04). Add an xUnit-friendly seam: `DatatableEditController.NeedsReviewCount` should be exposed publicly so a UI test can drive the cascade and assert `saveMenu.AllItemsDisabled`.
- **Singleton-form hide-not-dispose** — add an xUnit test that constructs `FormDatatableEditor`, calls `OnFormClosing` with `CloseReason.UserClosing`, asserts the form is `!IsDisposed && !Visible` (the canonical Phase 8 smoke-discovered defect class). Mirrors the pattern Phase 9 must replicate from the start.
- **Sort vs save-order divergence** — grep gate: `grep -c "dataGridView.Rows" DatatableSaveTargets.cs` MUST be 0 (D-09 view-only). Plus an xUnit test: sort the grid by column X → save → assert the output bytes equal the pre-sort output bytes.

## Security Domain

> `security_enforcement` is NOT explicitly set to `false` in `.planning/config.json`, so this section is included per default-enabled policy.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A — local desktop editor, no auth surface (the SWG client itself authenticates against the shard; Utinni is local in-process) |
| V3 Session Management | no | N/A — no session state |
| V4 Access Control | no | N/A — local in-process editor on the user's own machine; the only "access" is the SWG client's mapped memory, which the user already owns |
| V5 Input Validation | **yes** | Strict edit-time validation via per-type widgets (D-03); `MangleValue` parse-back on commit for free-text columns; type-change cascade (D-04). All file inputs validated by IffReader's existing chunk-cap (Phase 8: 64 MB / chunk), TreFile's bounded TOC, and `DataTableDocument.FromIff` malformed-input rejection |
| V6 Cryptography | no | N/A — `Crc::normalizeAndCalculate` is a non-cryptographic hash (SOE's CRC variant); used for content-addressing, not security. No password / signing / key-management surface |
| V12 File and Resources | **yes** | Path traversal: `LooseOverridePath.Resolve` (Phase 8 framework helper, 14-Fact-tested) — Phase 9 reuses verbatim; CSV import: read from user-chosen path via OpenFileDialog (OS-mediated); export: same via SaveFileDialog. No arbitrary file load by path-injection |

### Known Threat Patterns for {WinForms-on-net472 + native-bound-IFF stack}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal in loose-override / Save-As | Tampering | `LooseOverridePath.Resolve` enforces rooted/.. rejection + normalized StartsWith (Phase 8 D-05.1; reused verbatim) |
| Malformed `.tab` causes parse panic / DoS | DoS | `IffReader` 64 MB chunk cap (Phase 8 reused); `DataTableDocument.FromIff` cell-count sanity check (`numRows × numCols × max-cell-size <= 256 MB` recommended cap — planner discretion); explicit reject on cell-count > `int.MaxValue` |
| Malicious CSV input causes massive heap allocation | DoS | Cap CSV row count + cell-size at parse time (recommend: 100k rows max, 16 KB per cell — same `_loadRow` buffer SOE uses per `DataTableWriter.cpp:768`); plan-time grep gate that the import path enforces this cap |
| Live-patch off the game thread (CON-N-04 violation) | Tampering | N/A this phase — live-patch DISABLED per CF-03; if a future enabler phase wires .tab ClientMemory, it MUST go through Phase 8's `LivePatchValidator` + `GameCallbacks.AddMainLoopCall` |
| `.tre` repack corrupts source archive on locked-file race | Tampering | Phase 8 `TreRepackLock.Probe` + `IsSharingViolation` + atomic File.Replace + `RefusedClientHoldsArchive_LooseOverrideRecommended` fallback — Phase 9 reuses verbatim |
| Singleton-form ObjectDisposedException on second open | DoS (editor crash) | Hide-not-dispose intercept on `CloseReason.UserClosing` per Pitfall 5 |
| Stale bytes reload race | Tampering | Phase 8 `Flush(true)` MEDIUM-9 barrier in `IffSaveTargets.WriteAtomic` — Phase 9 reuses; Reload button disabled while save Task in flight via `saveInFlight` flag |
| CSV cell injection (e.g., a CSV cell starting with `=` interpreted as Excel formula) | Tampering | Out of scope — exported CSVs are for human review / programmatic re-import, not direct execution in spreadsheets. Document in CSV export tooltip if reviewers push back. |

## Sources

### Primary (HIGH confidence)
- **swg-client-v2 SOE reference** (read-only — memory `project_swg_client_v2_reference`): `D:/Code/swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTable.cpp:444-603` (DTII chunk parsing, V0 / V1 dispatch, per-cell read), `DataTableColumnType.cpp:84-232` (type-spec parser), `DataTableColumnType.cpp:382-473` (`mangleValue`), `DataTableWriter.cpp:619-911` (`_saveTableToIff`, `_saveColumns`, `_saveTypes`, `_saveRows`), `DataTableManager.h:25-30` (no in-session reload hook), `Iff.cpp:893-897` (insertChunkString ASCII NUL-terminated semantics).
- **Phase 8 shipped code (this repo)** — verified via direct file read:
  - `D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/IffWriter.cs` (write-path framework primitive Phase 9 composes on)
  - `D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/MutableIffDocument.cs` + `MutableIffNode.cs` (hybrid mutable DOM pattern to mirror)
  - `D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/OpenSource.cs` (4-case provenance Phase 9 reuses verbatim)
  - `D:/Code/Utinni/UtinniCoreDotNet/Editing/IffEditController.cs:67-105` (controller shape to mirror)
  - `D:/Code/Utinni/UtinniCoreDotNet/Saving/LooseOverridePath.cs` (path-defense helper reused)
  - `D:/Code/Utinni/UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` (DTII routing already covered)
  - `D:/Code/Utinni/UtinniCoreDotNet/Saving/TreBackupPath.cs` + `TreRepackLock.cs` (repack helpers reused)
  - `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/IffSaveTargets.cs:50-283` (save modes 1/2/4 dispatcher reused)
  - `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/ClientReloadDispatcher.cs:67-100` (tiered reload reused)
  - `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/TreRepackSaveTarget.cs:80-230` (repack save target reused)
  - `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` (host pattern to mirror end-to-end: BuildSaveMenu, RefreshSaveMenuEnabledState, ProcessCmdKey, UpdateDirtyVisuals, OpenFromTreEntry, hide-not-dispose)
  - `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs:138-235` ("Open in IFF Editor" hand-off pattern Phase 9 mirrors)
  - `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/IffChunkTree.cs` (TJT.UI.Controls namespace + placement pattern for ThemedDataGridView)
- **Phase 8 PLAN + SUMMARY artifacts** (`.planning/phases/08-tjt-subpanel-iff-editor-read-write/08-{01..07}-SUMMARY.md`) — verified for the exact public API surface Phase 9 will compose on.
- **CONTEXT.md (09)** — locked decisions D-01..D-10 + CF-01..CF-09; CONTEXT D-03 enum syntax `e[a:0,b:1,c:2]` flagged as incorrect in Assumption A1.
- **UI-SPEC.md (09)** — approved 2026-05-28 (6/6 PASS, 4 non-blocking recommendations); ThemedDataGridView token map, per-type cell widget contract, locked copywriting.

### Secondary (MEDIUM confidence)
- **Phase 8 cross-AI review record** (`08-REVIEWS.md`) — HIGH-1/2/3/4, MEDIUM 5..12 dispositions inherited as Phase 9 baseline.
- **`Iff::insertChunkString` semantics** — verified for ASCII variant (`D:/Code/swg-client-v2/src/engine/shared/library/sharedFile/src/shared/Iff.cpp:893-897`); the Unicode variant at `:906-910` is NOT used by DTII (per `DataTableWriter._saveColumns`/`_saveTypes`/`_saveRows` which call `insertChunkString(const char *)` only).
- **`swg-client-v2/tools/swg_blender/swg_iff/writer.py:34-89`** — Python IFF helpers confirming chunk framing format (BE header, payload bytes). Cross-verifies the C++ canon.

### Tertiary (LOW confidence — flagged for validation)
- **`Crc::normalizeAndCalculate` exact algorithm** — only the call site (`DataTableColumnType.cpp:434`) was read in this session; the impl in `sharedFoundation/Crc.{h,cpp}` was NOT read. Listed in Open Question 1; planner action required.
- **Real-world `.tab` file shapes** — none in `D:/Code/swg-client-v2/data/`; reliance is on synthetic fixtures per Assumption A6. Live SWG TRE extraction would provide ground truth but is operator-driven Tier-4 work.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Phase 8 just shipped 7 successful plans against the same stack with passing CI; no new package introductions.
- Architecture: HIGH — Phase 9 mirrors Phase 8's framework + plugin separation, hybrid DOM pattern, controller pattern, modal lifecycle pattern, and save/reload pipeline; the only NEW patterns are the typed format primitives (well-specified by SOE reference) and the DataGridView wrapper (well-specified by UI-SPEC).
- Pitfalls: HIGH — most pitfalls were either learned from Phase 8 smoke (singleton-form, sort-vs-save) or are directly cited from SOE source (DT_Comment writer skip, NUL-terminated strings, enum syntax). Pitfall 7 (DataGridView performance at CombatDataTable scale) is MEDIUM because no actual measurement was done in this research session — listed as Assumption A5 + scheduled for plan 3 profiling.
- Validation: HIGH — Phase 8's test-pyramid + golden-fixture + automation-augmented-smoke pattern transfers 1:1; the new test files are spec'd in the Wave 0 Gaps list.
- Security: HIGH — the threat surface is identical to Phase 8 (file IO, path traversal, malformed input, repack races); all mitigations are reused framework helpers with existing Fact coverage.

**Research date:** 2026-05-29
**Valid until:** 2026-06-29 (30 days — stable since the Phase 8 shipped primitives are stationary and swg-client-v2 is a frozen reference corpus per memory `project_swg_client_v2_reference`).

## RESEARCH COMPLETE
