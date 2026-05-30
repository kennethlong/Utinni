# Phase 10: TJT subpanel — String-table Editor (`.stf`) - Context

**Gathered:** 2026-05-29
**Status:** Ready for planning

<domain>
## Phase Boundary

A **typed view + edit surface for `.stf` localized string tables** (SWG `LocalizedStringTable`), shipped as an `IEditorPlugin` WinForms SubPanel **inside The Jawa Toolbox** (per DEC-C4). Replaces the SOE-era `SwgStringEditor`. Ships at **SOE-parity scope (T4)** — full key/entry management on **existing** `.stf` files: edit text, add entries, remove entries, rename keys.

**⚠ Format correction vs ROADMAP:** The roadmap's Phase 10 line says this "layers on Phase 8's IFF read/write." **It does not.** `.stf` is a **flat, little-endian custom binary — NOT an IFF container** (confirmed by the Phase 7 `StringTableDecoder.cs` header comment and the SOE `LocalizedStringTable*` source). On-disk layout: `magic(0xABCD, long)` + `version(byte)` + `nextUniqueId(u32)` + `count(u32)`, then `count` string entries (`id u32` + `sourceCrc u32` + `charCount u32` + UTF-16LE text), then `count` name entries (`id u32` + `length u32` + ASCII name). Consequences that propagate through the whole phase:
- There is **no IFF chunk layer**, so the Phase 8/9 "IFF hybrid-DOM gives byte-exact-on-untouched for free" mechanism **does not apply** — byte-exactness must be engineered at the string-table level (see D-02).
- The IFF Editor "Switch to typed view" hand-off (Phase 9 D-10.3) is **N/A** — a `.stf` never opens in the IFF Editor at all. Entry points are file picker + TRE Browser hand-off only (see D-04).

**In scope:**
- Typed mutable model + writer in `UtinniCoreDotNet/Formats/StringTable/` (sibling to `Formats/Iff/` and `Formats/Datatable/`), composing on the existing **read-only** `Formats/Decoders/StringTableDecoder.cs`.
- WinForms editor SubPanel: grid of (key, text) entries with **T4 mutation** — edit text, add entry (engine-style auto-id + `NNN_default` name), remove entry by key, rename key.
- Translation/bulk-edit features (all V1, see D-03): **Find/Replace**, **CSV/TSV export + delta-import**, **sort + filter** by key/text, **PO/gettext export**.
- Round-trip CLI verb + golden fixtures (`roundtrip-stf`, Phase 8 D-02 / Phase 9 CF-02 pattern) — automated SC4 correctness gate.
- Save modes 1/2/4 from Phase 8 D-05 (loose override, Save/Save-As, `.tre` repack); mode 3 (in-memory live patch) stays DISABLED inherited.
- Reload UX locked to tier-(b) "reloads on next scene change" — `.stf` is already classified `PendingNextSceneChange` in `ReloadAssetClassifier`.
- Editor-local undo/redo (Phase 8 D-08 pattern).
- Faithful Unicode round-trip (SC4: `João` survives) — UTF-16LE, NO smart-quote "unfubar", NO STAB-03 typo-fix regression.

**Explicitly OUT of scope (deferred):**
- **New `.stf` from scratch** (empty-state "create a string table" designer) — V2, same boundary as Phase 9 D-01.
- **Raw `id` editing** — ids stay machine-managed (never user-editable); they are internal lookup machinery (runtime looks up by NAME). T4 means full key/entry parity, NOT manual id surgery.
- **Cross-table / @stringfile:key reference resolution** — no validation that an edited key is referenced anywhere, no "find usages across other `.stf`/datatables" — V2.
- **In-memory live patch for `.stf`** (Phase 8 mode 3) — stays disabled inherited.
- **Localization-manager-aware multi-language workflows** (e.g. editing `en/`, `fr/`, `de/` variants of the same table side-by-side) — V2.

</domain>

<decisions>
## Implementation Decisions

### Carried forward (locked — no re-decision; mirror Phase 8/9 CF-* lineage)
- **CF-01 (← 08 D-01 / 09 CF-01):** Format primitives (typed `StringTableDocument`, `StringTableWriter`, mutable entry model) ship **framework-side** in `UtinniCoreDotNet/Formats/StringTable/`, sibling to `Formats/Iff/` and `Formats/Datatable/`. **NOT** in `TheJawaToolboxDotNet`. TJT consumes via the existing `UtinniCoreDotNet.dll` reference. No new public-API surface across plugins (honors DEC-C4 intent). The existing read-only `Formats/Decoders/StringTableDecoder.cs` (Phase 7) is the parse foundation — Phase 10 adds the mutable model + writer; planner decides whether the mutable model wraps/reuses the decoder or supersedes it (recommend reuse the proven parse path, add a parallel mutable type).
- **CF-02 (← 08 D-02 / 09 CF-02):** Round-trip CLI verb + golden fixtures is the automated correctness gate. Add `utinni-cli roundtrip-stf` (placeholder name; planner picks per Phase 4/8/9 conventions) that parses → mutates → serializes → re-parses and asserts **byte-exact identity for untouched entries**. Automated gate for Success Criterion 4 (clean non-ASCII round-trip) and the no-corruption guarantee.
- **CF-03 (← 08 D-05 / 09 CF-03):** Save modes 1, 2, 4 are V1 (loose override, Save / Save-As, `.tre` repack — refuses V6000 archives per Phase 8 WR-06). **Mode 3 (in-memory live patch) stays DISABLED** behind the honest inherited tooltip; no `OpenSource.ClientMemory` provenance descriptor for `.stf` opens in V1.
- **CF-04 (← 08 D-08 / 09 CF-06):** **Editor-local undo/redo stack**, independent of Utinni's scene `UndoRedoManager`. String-table edits are not scene state (CON-M-05 disentanglement).
- **CF-05 (← 08 D-06 tier (b) / 09 CF-05):** **Reload UX locked to tier-(b) "reloads on next scene change."** `.stf` is already classified `ReloadTier.PendingNextSceneChange` in `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` (`StringtableExtensions = { ".stf" }`). The editor candidly tells the user the asset re-resolves on the next TJT-driven scene change; it does NOT fabricate a trigger. Locked wording — planner may NOT loosen it. **Researcher confirm:** whether string tables actually re-resolve on scene change or require relog (LocalizationManager cache semantics) — if relog-only, the badge wording must say so honestly.
- **CF-06 (← DEC-C4 LOCKED):** Subpanel-inside-TJT (`IEditorPlugin` SubPanel registered in `TheJawaToolboxDotNet/Plugin.cs` `SubPanelContainer`, alongside Phase 7 TRE Browser + Phase 8 IFF Editor + Phase 9 Datatable Editor). Not a separate plugin.

### Editing scope (Phase 10 ↔ V2 boundary)
- **D-01:** **V1 ships T4 — full SOE `SwgStringEditor` parity** on **existing `.stf` files only**. Founder decision, consistent with Phase 9 D-01's T4 call.
  - **T4 operations:** edit existing entry text; **add** entry (engine-style: auto-assign `m_nextUniqueId`, auto-name `NNN_default` per SOE `addString`, user renames after); **remove** entry by key; **rename** key.
  - **`id` is machine-managed, NOT user-editable.** Effectively "T4 minus id-edit." Manual id surgery is the one footgun (id collisions); ids are internal and never the runtime lookup key, so there is no user reason to edit them. The model assigns/maintains ids; the writer maintains `nextUniqueId` correctly on add.
  - **Name validation:** key renames honor the engine's rules — names that start with a digit are rejected (`LocalizedStringTableRW::rename`), and `validateStringName`/`fixupStringName` semantics apply (researcher: confirm exact rules from `LocalizedStringTable.cpp`). Duplicate keys rejected.
  - **NOT V1:** "new `.stf` from scratch" (empty-state designer) — V2.

### Write strategy & byte-exactness
- **D-02:** **Lean canonical re-serialize + faithful text; escalate only if fixtures disprove the assumption.** User chose "you decide (recommend canonical)." Planner/researcher must validate against real SWG `.stf` fixtures, then commit to one of:
  - **Default (recommended): canonical re-serialize.** Emit strings in **id-ascending** order and names in **name-ascending** (lexicographic) order — exactly what the SOE engine writes (`std::map<id,…>` and `std::map<name,id>` iteration in `LocalizedStringTableRW::write`). For a canonically-ordered input (which real engine/SOE-tool output **is**), full re-serialize is **byte-exact** — the flat format makes this simpler than Phase 9's IFF chunks. A non-canonically-ordered hand-authored input gets **normalized** to canonical order on save (same as the engine would do on its next write); this is acceptable and honest.
  - **Escalation (only if fixtures prove real `.stf` files exist with non-canonical ordering that must be preserved): per-entry original-byte + original-order preservation** (Phase 9 CF-04 style re-engineered for the flat format). More machinery; adopt only with evidence.
  - **Text faithfulness (non-negotiable, both paths):** UTF-16LE verbatim. **Do NOT replicate the SOE `unfubarMicrosoftInvalidTextCharacters` smart-quote rewrite** — the engine itself mutates smart-quotes/ellipsis/CR on load+write, but Utinni must **preserve text byte-faithfully** to honor SC4 (`João`) and the STAB-03 typo-fix-don't-regress policy. The `roundtrip-stf` golden asserts **our-reader ↔ our-writer** byte-exactness (self-consistency), NOT engine-output parity.
- **D-02b (crc-on-edit — DEFERRED to planner/research, decision-bearing):** Each entry carries a `sourceCrc u32` that is **NOT a content hash** — SOE `addString` sets it to `int(time(0))` (a timestamp). Policy for **edited/added** entries is deferred; candidates:
  - **preserve original crc** (max byte-stability for round-trips),
  - **set fresh `int(time(0))`** (engine-faithful), or
  - **set 0** (deterministic, test-friendly — easiest for golden fixtures).
  - **Researcher MUST confirm** whether the live client even reads `sourceCrc` at string-lookup time (bears on SC3 "live client renders edited strings"). If the client ignores it, prefer the deterministic/test-friendly choice. Untouched entries always preserve their original crc.

### Productivity / translation features (all V1)
- **D-03:** All four bulk features ship V1 (user selected all):
  - **D-03a — Find/Replace** across key + text columns (Ctrl-F / Ctrl-H, case toggle). The single most valuable feature for localization passes. Phase 9 D-07 precedent.
  - **D-03b — CSV/TSV export + delta-import.** Export `(key, text)` (UTF-8 **with BOM** for non-ASCII); import computes a **per-entry diff** — only entries whose imported text differs are marked dirty; matching entries keep original bytes (honors D-02). Preview modal "N entries change, M unchanged; proceed?" (Phase 9 D-08 pattern). CSV escape rules + encoding = planner discretion.
  - **D-03c — Sort + filter** by key/text. Column-click sort is **view-only** (does NOT mutate the canonical on-disk order — save always serializes per D-02). A live **filter box** over key+text also ships (the one thing Phase 9 deferred — justified here by large translation tables). 
  - **D-03d — PO/gettext export** (`msgid = key`, `msgstr = text`) for standard translation toolchains (Poedit/Weblate). **Lowest priority of the four** — if scope pressure hits during planning, this is the first cut-to-V2 candidate (export-only is the floor; PO *import* is explicitly not required in V1).

### Entry points & reload
- **D-04:** **Two entry points, V1 — manual hand-off only (no auto-route):**
  1. **File picker** (loose `.stf` from disk) — baseline.
  2. **TRE Browser "Open in String-table Editor"** on a selected entry (recognized as `.stf` by extension or by the `0xABCD` magic sniff — `StringTableDecoder.LooksLikeStf`). Mirrors the Phase 8/9 "Open in … Editor" hand-off from `FormTreBrowser`.
  - **The IFF Editor "Switch to typed view" hand-off is N/A** — `.stf` is not IFF and never loads in the IFF Editor (correction vs the Phase 9 three-entry-point pattern).
  - On open, the reload-status badge shows the CF-05 tier-(b) wording from the moment the file loads.

### Claude's Discretion
- **Editor surface / columns** (deferred to `/gsd-ui-phase 10` + planner): exact column layout, whether `id`/`crc` are shown read-only/diagnostic or fully hidden, dirty-state indicator placement, add/remove/rename UX, CSV-import preview modal layout. **Locked floor:** text + key are editable; `id` is machine-managed and never user-edited.
- **D-02b crc-on-edit policy** — see above; planner picks with researcher's confirmation of whether the client reads `sourceCrc`.
- **`StringTableDocument` ↔ existing `StringTableDecoder` relationship** — wrap/reuse vs supersede; recommend reuse the proven parse path.
- **CLI verb naming/shape** — `roundtrip-stf` is a placeholder; planner picks per Phase 4/8/9 conventions.
- **Plan decomposition** — Phase 10 is simpler than Phase 9 (one flat format, no per-type cell widgets, no type-change cascade, deterministic canonical write). Expect **3–5 plans**: (1) typed mutable model + writer (framework) + canonical-order validation against fixtures, (2) `roundtrip-stf` CLI verb + golden fixtures, (3) editor SubPanel + T4 mutation + entry points + undo/redo, (4) bulk features (Find/Replace + CSV + sort/filter + PO export), (5) live-SWG smoke + UAT. Planner has final say.

### Reviewed Todos (not folded)
- `gamecallbacks-gc-av-flake-fix.md` — CI-stability flake-fix in `GameCallbacksTests`; resolved in 06-04. Keyword match (`fix`, `phase`, `swg`, `game`) is a false positive — same as Phases 7/8/9. Unrelated to String-table Editor scope.
- `phase09-datatable-editor-review-warnings.md` — Phase 9 code-review Warnings/Info follow-ups. **Phase 9, not Phase 10** — belongs to Phase 9's outstanding-review backlog, not this phase's scope. Noted so planner does not absorb it.
- `loader-lock-harness-flake-fix.md` — CI-stability flake-fix; resolved in 06-04 (memory `project_loader_lock_harness_ci_flake.md`). Keyword false positive. Unrelated.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 10 roadmap / project decisions
- `.planning/ROADMAP.md` § Phase 10 — goal, success criteria (1: subpanel loads in TJT; 2: open / view entries with keys / edit text / save back; 3: live client renders edited strings on reload; 4: non-ASCII round-trips cleanly, e.g. `João`), preservation guard-rails (CON-M-01/02 SPI, CON-T-05 `*Impl` separation, STAB-03 Unicode typo-fix-don't-regress policy). **Note the format correction:** the roadmap's "layers on Phase 8's IFF read/write" is inaccurate — `.stf` is NOT IFF (see `<domain>`).
- `.planning/PROJECT.md` § Key Decisions — **DEC-C4** (subpanel-inside-TJT, LOCKED), **DEC-A3** (not-a-DCC-replacement, LOCKED — not engaged here; string tables are text data, no art-asset write overlap).
- `.planning/REQUIREMENTS.md` — **PROD-W1-STF** ("View and edit `.stf` string tables; replaces SOE-era `SwgStringEditor`. Plugin loads in editor host; user can open a `.stf`, view string entries, edit text, save back; live SWG client renders edited strings on reload.") and **PROD-02** (Wave-1 edit aggregate). Note: PROD-W1-STF acceptance is T1; Phase 10 ships **T4** (D-01) by explicit founder decision, parallel to Phase 9.

### Phase 8/9 carried-forward decisions (CF-* above)
- `.planning/phases/09-tjt-subpanel-datatable-editor-tab/09-CONTEXT.md` — **READ FIRST.** The direct sibling phase; CF-01..CF-09 + D-01..D-10 are the template Phase 10 specializes. Phase 10 reuses the SubPanel host pattern, dirty-state, Save▾ wiring, TRE-Browser hand-off, round-trip golden harness, and tier-(b) reload candor — adapting them to a flat (non-IFF) format and a much simpler 2-column (key/text) surface.
- `.planning/phases/08-tjt-subpanel-iff-editor-read-write/08-CONTEXT.md` — D-05 (4 save modes; mode-3 disabled), D-06 (TIERED reload; `.stf` is tier (b)), D-08 (editor-local undo/redo). The save-target + reload infrastructure Phase 10 consumes.
- `.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-CONTEXT.md` — D-02 (swg-client-v2 reference policy); origin of the read-only `StringTableDecoder` (Phase 7 04a).

### Existing Utinni assets to extend / reuse (this repo)
- `UtinniCoreDotNet/Formats/Decoders/StringTableDecoder.cs` — **READ FIRST.** Existing read-only `.stf` decoder (Phase 7). Documents the exact on-disk layout, the `0xABCD` magic, version 0/1 support, the string-then-name two-block structure, UTF-16LE text with U+FFFD replacement, and the `LooksLikeStf` magic sniff (reused for D-04 hand-off detection). Phase 10's mutable model + writer build on this parse path.
- `UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs`, `DecoderException.cs` — the cursor + exception types the decoder uses (the cursor is a generic LE byte reader despite the `Iff` name — fine to reuse for the flat `.stf`).
- `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` — already classifies `.stf` → `ReloadTier.PendingNextSceneChange` (`StringtableExtensions`). CF-05 reload badge consumes this directly. **Do not re-implement classification.**
- `UtinniCoreDotNet/Formats/Datatable/*` (Phase 9 deliverable: `DataTableDocument`, `DataTableWriter`, mutable DOM) — the **structural template** for `StringTableDocument`/`StringTableWriter` (mutable typed model + writer in a `Formats/<Type>/` sibling folder). NOT shared code — a parallel, simpler analog.
- `Utinni.Cli/Commands/DecodeIffCommand.cs` (dispatches the `.stf` decoder today), Phase 8 `roundtrip-iff` + Phase 9 `roundtrip-tab` verbs, `Utinni.Cli.Tests` golden fixtures — the harness pattern `roundtrip-stf` (CF-02) follows. Same fixture layout, same `Assert.Equal(original, roundtripped)` byte-exact gate.
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor*` (Phase 9) — closest SubPanel precedent (toolbar + themed grid + dirty-state + Save▾ + entry-point hand-off). Phase 10's editor mirrors its host shape with a simpler grid.
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (+ `.Designer.cs`) — the TRE Browser the D-04 "Open in String-table Editor" hand-off originates from. Mirror the Phase 8/9 hand-off wiring (extension `.stf` and/or `LooksLikeStf` magic detection).
- `UtinniCoreDotNet/UI/Controls/SubPanel.cs`, `SubPanelContainer.cs`, themed control patterns (ThemedDataGridView from Phase 9) — host surface + dark theme.
- `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs` — MEF SPI (CON-M-01/02).
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs` — `SubPanelContainer` registration site.
- `TheJawaToolboxDotNet.csproj` — references `UtinniCoreDotNet.dll` via `HintPath` (the CF-01 consumption path).

### swg-client-v2 reference (read-only spec/impl, NOT a runtime dep — per memory `project_swg_client_v2_reference.md`)
- `../swg-client-v2/src/external/ours/library/localization/src/shared/LocalizedStringTable.{h,cpp}` — **AUTHORITATIVE FORMAT.** `Map_t = std::map<id, LocalizedString*>` (→ strings written **id-ascending**) and `NameMap_t = std::map<name, id>` (→ names written **name-ascending**) — the canonical on-disk ordering for D-02. `ms_MAGIC` (`long` 0xABCD), `getCurrentVersion()`, `validateStringName`/`fixupStringName` (D-01 rename rules), `load_0000`/`load_0001` (version 0 vs 1 read).
- `../swg-client-v2/src/external/ours/library/localization/src/shared/LocalizedStringTableReaderWriter.{h,cpp}` — **PORT REFERENCE for the writer (CF-01).** `write()` (header + string block + name block framing), `str_write()` (id + sourceCrc + buflen + UTF-16 text), `addString()` (auto-id + `NNN_default` naming + `nextUniqueId` maintenance — D-01 add semantics), `removeStringByName()`, `rename()` (D-01 validation: no-leading-digit, no-duplicate). **`unfubarMicrosoftInvalidTextCharacters` — DO NOT PORT** (D-02 text-faithfulness; the engine's smart-quote rewrite is exactly what Utinni must avoid).
- `../swg-client-v2/src/external/ours/library/localization/src/shared/LocalizedString.{h,cpp}` — `LocalizedString` storage: `m_id`, `m_sourceCrc` (set to `int(time(0))` on add — the D-02b timestamp-not-hash finding), `m_str` (UTF-16). Reverse for the C# mutable entry model.
- `../swg-client-v2/src/engine/shared/application/StringFileTool/src/win32/StringTable.cpp` and `.../QuestEditor/src/win32/StringTable.cpp` — SOE-era authoring-tool views over the same format; design reference for the editor UX and for cross-checking serialization output (if invocable offline; otherwise design reference only).
- **LocalizationManager** (search `../swg-client-v2/.../localization/` for `LocalizationManager.cpp`) — runtime string-table cache + reload semantics. **Researcher: confirm CF-05** — whether an edited `.stf` re-resolves on scene change or needs relog (drives the honest reload-badge wording).

### Codebase intel (this repo)
- `.planning/codebase/STACK.md`, `STRUCTURE.md`, `ARCHITECTURE.md`, `CONVENTIONS.md`, `CONCERNS.md`, `INTEGRATIONS.md`, `TESTING.md` — repo-wide maps. CONCERNS/CONVENTIONS flag CON-M-01/02 (MEF SPI), CON-M-05 (UndoRedoManager scene cleanup — Phase 10 steers clear per CF-04), CON-T-05 (`*Impl` separation).

### Relevant memory (operator background; informs planner choices, not direct agent input)
- `feedback_caller_attrs_binary_compat.md` — adding NEW types to `UtinniCoreDotNet/Formats/StringTable/` is safe; do NOT change existing public signatures consumed by pre-built plugins without rebuilding in the same commit.
- `feedback_winforms_dockfill_zorder.md` — the grid docks Fill and stays front-most (Phase 9 CF-09 carries over).
- `project_swg_client_v2_reference.md` — locked reference-corpus policy (read-only, no runtime dep, no code/identifier copying).
- `project_scene_change_via_tjt.md` + `project_tjt_scene_change_naked_baseline.md` — TJT chat-command-parser scene change is the reload trigger users run after a `.stf` save; "naked after scene change" is a pre-existing baseline, NOT a Phase 10 regression signal.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`StringTableDecoder.cs` (Phase 7, read-only):** the proven `.stf` parse path. Phase 10's mutable model layers on it; the `LooksLikeStf(byte[])` magic sniff is reused for the D-04 TRE-Browser hand-off detection.
- **Phase 9 `Formats/Datatable/` model+writer:** structural template for `Formats/StringTable/` — mutable typed document + writer composing low-level emit, with a round-trip golden gate. Simpler here (no IFF chunk framing, no per-type cells, no type cascade).
- **Phase 8/9 round-trip CLI verb + `Utinni.Cli.Tests` goldens:** drop-in harness pattern for `roundtrip-stf` (CF-02).
- **Phase 9 `FormDatatableEditor` SubPanel + themed grid + dirty-state + Save▾ + TRE-Browser hand-off:** host-pattern precedent reused (separately, not as a shared base class — a shared abstract editor base is a post-Wave-1 refactor, see Deferred).
- **`ReloadAssetClassifier` (`.stf` → tier b):** CF-05 reload badge wiring already exists; consume it.
- **`SubPanel`/`SubPanelContainer` + Utinni themed controls:** WinForms host + dark theme.

### Established Patterns
- `IEditorPlugin` MEF export → aggregation in TJT `Plugin.cs` `SubPanelContainer` (CON-M-01/02); `*Impl` separation (CON-T-05).
- **Round-trip golden test:** parse → mutate → serialize → re-parse → byte-exact compare on untouched entries (Phase 4/8/9 precedent). For the flat `.stf`, canonical re-serialize (D-02) makes the byte-exact gate achievable without per-entry original-byte machinery (pending fixture confirmation).
- **Deterministic canonical serialization order** (D-02): strings id-ascending, names name-ascending — matches the SOE engine's `std::map` iteration. This is the byte-exactness mechanism for the flat format (the analog of Phase 9's IFF hybrid-DOM, but achieved by ordering rather than original-byte capture).
- **TIERED reload-status badge UX** (CF-05): `.stf` always shows tier-(b) "reloads on next scene change."
- **Binary-compat caution:** add new types only; don't change existing public `Formats/*` signatures without rebuilding plugins in the same commit.

### Integration Points
- New String-table Editor SubPanel registers via TJT `Plugin.cs` `SubPanelContainer` (alongside Phase 7 TRE Browser + Phase 8 IFF Editor + Phase 9 Datatable Editor).
- TRE Browser "Open in String-table Editor" hand-off wires from `FormTreBrowser` selection (extension `.stf` and/or `LooksLikeStf` magic detection). **No IFF-Editor hand-off** (format is not IFF).
- Loose-override save target reuses the same client-derived override directory the Phase 8/9 editors used (CF-03 mode 1).
- `.tre` repack save target reuses the Phase 8 `TreWriter.Repack` path (CF-03 mode 4; refuses V6000 archives).
- Reload badge (CF-05) interacts with the TJT-driven scene-change path; the editor tells the user the asset reloads on the next scene change, does NOT trigger one.
- CLI `roundtrip-stf` (CF-02) adds to `Utinni.Cli` alongside `inspect-iff` / `decode-iff` / `roundtrip-iff` / `roundtrip-tab`; goldens live in `Utinni.Cli.Tests` fixtures.

</code_context>

<specifics>
## Specific Ideas

- Replaces the **SOE-era `SwgStringEditor`** at **feature parity for editing existing files** (T4 — D-01). The "spreadsheet of key → localized text" mental model is the UX anchor; this is the simplest Wave-1 grid (two columns: key + text).
- String tables are the **localization surface** — the killer use case is **translation work**, which is why all four bulk features (Find/Replace, CSV round-trip, sort+filter, PO export) are first-class V1 (D-03), not deferred polish. Real tables (e.g. `creature_names`, `quest/*`) have hundreds of entries.
- **SC4 "clean non-ASCII round-trip" (`João`) is structurally enforced** (D-02): UTF-16LE verbatim, NO smart-quote unfubar, the `roundtrip-stf` golden as the automated gate. This directly honors the STAB-03 typo-fix-don't-regress preservation guard-rail.
- **The flat format is a feature, not a limitation:** unlike Phase 9's IFF chunks, deterministic canonical ordering (strings id↑, names name↑) means a full re-serialize is byte-exact for canonical inputs — simpler and lower-risk than the IFF hybrid-DOM. The SOE writer (`LocalizedStringTableReaderWriter.cpp`) is a small, direct port reference (header + two blocks).
- **Two surprising format facts the planner/researcher must not miss:** (1) `sourceCrc` is a **timestamp** (`int(time(0))`), not a content hash — drives the D-02b crc-on-edit decision; (2) the engine itself **mutates smart-quotes on load+write** (`unfubar…`), so engine output is NOT text-faithful — Utinni deliberately diverges to preserve text exactly.

</specifics>

<deferred>
## Deferred Ideas

- **"New `.stf` from scratch"** — empty-state designer to create a brand-new string table. Adds a creation UX that's its own small project. Deferred to V2 / dedicated phase (same boundary as Phase 9 D-01).
- **Raw `id` editing** — manual id assignment/surgery. Ids stay machine-managed in V1 (D-01); manual editing is a collision footgun with no user benefit (lookup is by name). V2 only if a real need surfaces.
- **Cross-reference resolution / "find usages"** — surfacing where an edited key (`@stringfile:key`) is referenced across other `.stf`, datatables, or engine code; dangling-key warnings on rename/remove. Deferred to V2 (matches SOE `SwgStringEditor`, which had no cross-reference awareness).
- **PO *import*** — V1 ships PO *export* only (D-03d); round-trip import via gettext is V2. (And PO export itself is the first cut-to-V2 candidate under scope pressure.)
- **Multi-language side-by-side editing** — editing `en/`, `fr/`, `de/` variants of the same table together with a LocalizationManager-aware diff view. V2.
- **In-memory live patch for `.stf`** — Phase 8 mode-3 stays disabled inherited (CF-03). Wiring an `OpenSource.ClientMemory` provenance descriptor is a follow-up enabler phase.
- **ImGui chromeless HUD-overlay presentation** — per memory `project_hud_style_overlay_directive.md` (HUD = optional later polish for data editors, NOT binding). WinForms SubPanel ships V1 per CF-06.
- **Shared "abstract editor base class"** across IFF / Datatable / String-table / Object-template editors — tempting now that this is the fourth SubPanel with shared toolbar/dirty-state/save-flow patterns. Refactor candidate post-Wave-1 (same note as Phase 9).

### Reviewed Todos (not folded)
- `gamecallbacks-gc-av-flake-fix.md` — CI-stability flake-fix, resolved in 06-04. Keyword false-positive (same as Phases 7/8/9). Unrelated.
- `phase09-datatable-editor-review-warnings.md` — Phase 9 review backlog (Warnings 7 + Info 5), belongs to Phase 9, not Phase 10. Noted so planner does not absorb it into Phase 10 scope.
- `loader-lock-harness-flake-fix.md` — CI-stability flake-fix, resolved in 06-04. Keyword false-positive. Unrelated.

</deferred>

---

*Phase: 10-tjt-subpanel-string-table-editor-stf*
*Context gathered: 2026-05-29*
