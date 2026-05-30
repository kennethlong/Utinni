# Phase 10: TJT subpanel — String-table Editor (`.stf`) - Research

**Researched:** 2026-05-30
**Domain:** SWG `.stf` localized string-table format (flat little-endian binary) — typed mutable model + writer (C# / .NET Framework `UtinniCoreDotNet`), CLI round-trip golden gate, WinForms editor SubPanel inside The Jawa Toolbox (`UtinniPlugins`), translation/bulk features.
**Confidence:** HIGH (decisions + format are locked in CONTEXT/UI-SPEC and cross-checked against the SOE `LocalizedStringTable*` source paths); MEDIUM on three points the planner must confirm against live source (see Open Questions: `sourceCrc` client-read behavior, exact `validateStringName`/`fixupStringName` rules, real-`.stf` canonical-ordering assumption).

> **Tooling note (honest disclosure):** During this research session the Bash/Glob/Grep tools intermittently dropped outputs (a malformed PowerShell-in-bash call wedged a parallel batch) and the `UtinniCoreDotNet` source tree is gitignored, so content-search tools could not reliably enumerate it. The four authoritative *planning* documents — `10-CONTEXT.md`, `10-UI-SPEC.md`, `ROADMAP.md` §Phase 10, `config.json` — were read **in full** and are the basis for this research. Claims about *source code* (Phase 7 `StringTableDecoder.cs`, Phase 9 `Formats/Datatable/*`, Phase 8 save infra, swg-client-v2 SOE C++) are tagged `[CITED: …]` from the documented paths/annotations in CONTEXT.md, **not** re-verified line-by-line this session. Every such claim that bears on byte-exactness is flagged for the planner to confirm by direct read during the first plan's Wave-0. This is the safest posture: the planner re-reads the proven parse path and the SOE writer before committing the serializer.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Carried forward (locked — mirror Phase 8/9 CF-* lineage):**
- **CF-01 (← 08 D-01 / 09 CF-01):** Format primitives (typed `StringTableDocument`, `StringTableWriter`, mutable entry model) ship **framework-side** in `UtinniCoreDotNet/Formats/StringTable/`, sibling to `Formats/Iff/` and `Formats/Datatable/`. **NOT** in `TheJawaToolboxDotNet`. TJT consumes via the existing `UtinniCoreDotNet.dll` reference. No new public-API surface across plugins (honors DEC-C4). The read-only `Formats/Decoders/StringTableDecoder.cs` (Phase 7) is the parse foundation — Phase 10 adds the mutable model + writer; **recommended: reuse the proven parse path, add a parallel mutable type.**
- **CF-02 (← 08 D-02 / 09 CF-02):** Round-trip CLI verb + golden fixtures is the automated correctness gate. Add `utinni-cli roundtrip-stf` (placeholder name) that parses → mutates → serializes → re-parses and asserts **byte-exact identity for untouched entries**. Automated gate for Success Criterion 4 (clean non-ASCII round-trip) and the no-corruption guarantee.
- **CF-03 (← 08 D-05 / 09 CF-03):** Save modes 1, 2, 4 are V1 (loose override, Save / Save-As, `.tre` repack — refuses V6000 archives per Phase 8 WR-06). **Mode 3 (in-memory live patch) stays DISABLED** behind the inherited tooltip; no `OpenSource.ClientMemory` provenance for `.stf` opens in V1.
- **CF-04 (← 08 D-08 / 09 CF-06):** **Editor-local undo/redo stack**, independent of Utinni's scene `UndoRedoManager` (CON-M-05 disentanglement).
- **CF-05 (← 08 D-06 tier (b) / 09 CF-05):** **Reload UX locked to tier-(b) "reloads on next scene change."** `.stf` is already classified `ReloadTier.PendingNextSceneChange` in `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs`. The editor candidly tells the user the asset re-resolves on next TJT-driven scene change; does NOT fabricate a trigger. **Locked wording — planner may NOT loosen it.** Researcher confirm (see Open Questions): whether `.stf` actually re-resolves on scene change or requires relog.
- **CF-06 (← DEC-C4 LOCKED):** Subpanel-inside-TJT (`IEditorPlugin` registered in TJT `Plugin.cs`, alongside Phase 7 TRE Browser + Phase 8 IFF Editor + Phase 9 Datatable Editor). Not a separate plugin.

**Editing scope:**
- **D-01:** V1 ships **T4 — full SOE `SwgStringEditor` parity on existing `.stf` files only.** T4 operations: edit existing entry text; **add** entry (engine-style: auto-assign `m_nextUniqueId`, auto-name `NNN_default` per SOE `addString`, user renames after); **remove** entry by key; **rename** key. **`id` is machine-managed, NOT user-editable** ("T4 minus id-edit"). Name validation honors engine rules (no leading digit, no duplicate, non-empty). NOT V1: "new `.stf` from scratch."

**Write strategy & byte-exactness:**
- **D-02:** **Lean canonical re-serialize + faithful text; escalate only if fixtures disprove the assumption.** Default (recommended): canonical re-serialize — strings **id-ascending**, names **name-ascending (lexicographic)** — exactly what the SOE engine writes (`std::map` iteration). For canonically-ordered input (real engine/SOE-tool output), full re-serialize is byte-exact. Non-canonical hand-authored input is **normalized** on save (acceptable + honest). Escalation (only with fixture evidence of non-canonical real files): per-entry original-byte + original-order preservation (Phase 9 CF-04 style). **Text faithfulness (non-negotiable):** UTF-16LE verbatim. **Do NOT replicate the SOE `unfubarMicrosoftInvalidTextCharacters` smart-quote rewrite.** The `roundtrip-stf` golden asserts **our-reader ↔ our-writer** byte-exactness (self-consistency), NOT engine-output parity.
- **D-02b (crc-on-edit — DEFERRED to planner/research):** Each entry carries a `sourceCrc u32` that is **NOT a content hash** — SOE `addString` sets it to `int(time(0))` (a timestamp). Policy for edited/added entries deferred; candidates: preserve original crc (max byte-stability) / set fresh `int(time(0))` (engine-faithful) / set 0 (deterministic, test-friendly). **Researcher MUST confirm** whether the live client reads `sourceCrc` at lookup time. Untouched entries always preserve their original crc.

**Productivity/translation features (all V1):**
- **D-03a — Find/Replace** across key + text columns (Ctrl-F / Ctrl-H, case toggle, regex opt-in).
- **D-03b — CSV/TSV export + delta-import.** Export `(key, text)` (UTF-8 **with BOM**); import computes a **per-entry diff** — only changed-text entries marked dirty; matching entries keep original bytes (honors D-02). Preview modal "N change, M unchanged; proceed?".
- **D-03c — Sort + filter** by key/text. Column-click sort is **view-only** (does NOT mutate canonical on-disk order). A live **filter box** over key+text ships (the one thing Phase 9 deferred).
- **D-03d — PO/gettext export** (`msgid = key`, `msgstr = text`). **Lowest priority** — first cut-to-V2 candidate under scope pressure (export-only is the floor; PO import NOT V1).

**Entry points & reload:**
- **D-04:** Two entry points, V1 — manual hand-off only: (1) **file picker**; (2) **TRE Browser "Open in String-table Editor"** (recognized by `.stf` extension OR `0xABCD` magic sniff `StringTableDecoder.LooksLikeStf`). **The IFF Editor "Switch to typed view" hand-off is N/A** — `.stf` is not IFF.

### Claude's Discretion
- **Editor surface / columns** (UI-SPEC settled most of this): exact column layout, whether `id`/`crc` shown read-only/diagnostic or hidden, dirty-state placement, add/remove/rename UX, CSV-import preview layout. **Locked floor:** text + key editable; `id` machine-managed, never user-edited.
- **D-02b crc-on-edit policy** — planner picks with researcher's confirmation of whether the client reads `sourceCrc`.
- **`StringTableDocument` ↔ existing `StringTableDecoder` relationship** — wrap/reuse vs supersede; **recommend reuse the proven parse path.**
- **CLI verb naming/shape** — `roundtrip-stf` is a placeholder.
- **Plan decomposition** — simpler than Phase 9. Expect **3–5 plans**. Planner has final say.

### Deferred Ideas (OUT OF SCOPE)
- **"New `.stf` from scratch"** (empty-state designer) — V2.
- **Raw `id` editing** — ids machine-managed in V1; manual editing is a collision footgun, lookup is by name. V2 only if a real need surfaces.
- **Cross-reference resolution / "find usages"** (`@stringfile:key` across other `.stf`/datatables/engine; dangling-key warnings on rename/remove) — V2.
- **PO *import*** — V1 ships PO *export* only; gettext round-trip import is V2.
- **Multi-language side-by-side editing** (`en/`/`fr/`/`de/` variants with LocalizationManager-aware diff) — V2.
- **In-memory live patch for `.stf`** (Phase 8 mode-3) — stays disabled inherited.
- **ImGui chromeless HUD-overlay presentation** — optional later polish; WinForms SubPanel ships V1.
- **Shared "abstract editor base class"** across IFF/Datatable/String-table/Object-template editors — post-Wave-1 refactor.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **PROD-W1-STF** | "View and edit `.stf` string tables; replaces SOE-era `SwgStringEditor`. Plugin loads in editor host; user can open a `.stf`, view string entries, edit text, save back; live SWG client renders edited strings on reload." Acceptance is **T1**; Phase 10 ships **T4** (D-01) by explicit founder decision. | Format layout + mutable model (§Format & Writer); editor SubPanel (§Architecture); save modes 1/2/4 + tier-(b) reload (§Save + Reload). SC1=subpanel loads; SC2=open/view/edit/save; SC3=live render on reload; SC4=non-ASCII round-trip (`João`). |
| **PROD-02** | Wave-1 edit aggregate (contributes; Phase 10 is the 4th of 5 Wave-1 SubPanels). | The whole phase contributes one more `IEditorPlugin` form into TJT `GetForms()`. |
</phase_requirements>

## Summary

Phase 10 is the **fourth Wave-1 TJT SubPanel editor** and is, by design, the **simplest of the four**. The format — SWG `.stf` `LocalizedStringTable` — is a **flat, little-endian custom binary, NOT an IFF container** (the ROADMAP's "layers on Phase 8 IFF read/write" line is inaccurate, corrected authoritatively in CONTEXT and UI-SPEC). Because the format is flat with two contiguous blocks in deterministic sorted order, **byte-exactness is achieved by canonical re-serialization (strings id-ascending, names name-ascending) rather than the Phase 9 IFF hybrid-DOM original-byte machinery.** This is the single most important architectural simplification versus Phase 9 and should drive a leaner plan count.

The work splits cleanly into four reuse-driven legs: (1) a **framework-side** typed mutable model + writer in `UtinniCoreDotNet/Formats/StringTable/`, composing the existing read-only Phase 7 `StringTableDecoder.cs`; (2) a **CLI `roundtrip-stf` golden gate** mirroring `roundtrip-iff`/`roundtrip-tab`, which is the automated SC4 correctness proof (and the only CI-coverable correctness gate, since the WinForms TJT assembly is not project-referenceable from the x86 test project — Phase 8/9 precedent); (3) the **WinForms editor SubPanel** (`FormStringTableEditor`, a sibling of `FormDatatableEditor`) with T4 mutation, two entry points, editor-local undo/redo, and the three save modes; and (4) the **translation/bulk features** (Find/Replace, CSV/TSV delta-import with preview, view-only sort + live filter, PO export). The UI is already fully specified and approved in `10-UI-SPEC.md`.

The phase carries **two surprising format facts the planner must not miss**: (a) `sourceCrc` is a **timestamp** (`int(time(0))`), not a content hash — this drives the deferred D-02b crc-on-edit policy and needs a research confirmation that the live client doesn't read it; (b) the SOE engine itself **mutates smart-quotes/ellipsis/CR on load+write** via `unfubarMicrosoftInvalidTextCharacters`, so engine output is NOT text-faithful — Utinni **deliberately diverges** to preserve text byte-exactly (honoring SC4 `João` and the STAB-03 typo-fix-don't-regress policy). The `roundtrip-stf` golden therefore asserts **our-reader ↔ our-writer self-consistency**, never engine-output parity.

**Primary recommendation:** Ship in **4 plans across 4 waves** (model+writer → CLI golden gate → editor SubPanel+entry points+T4+save+undo → bulk features), with a maintainer **live-SWG smoke** as the Phase-9-precedent acceptance step folded into the final plan's verification (automation-augmented + ACK-deferred-acceptable for V1). Build everything INLINE on the main tree (worktrees off). Confirm three MEDIUM-confidence facts against source in the first plan's Wave-0 before committing the writer.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `.stf` parse (read) | Framework (`UtinniCoreDotNet/Formats/Decoders/StringTableDecoder.cs`, existing) | — | Already done in Phase 7; reused as parse foundation (CF-01). |
| Mutable model + canonical writer | Framework (`UtinniCoreDotNet/Formats/StringTable/`) | — | Format primitives are framework-side, shared via `UtinniCoreDotNet.dll`; no plugin-API widening (CF-01, DEC-C4). |
| Byte-exact correctness gate | CLI (`Utinni.Cli` `roundtrip-stf`) + x86 test (`Utinni.Cli.Tests` goldens) | — | Only CI-coverable correctness surface; mirrors `roundtrip-iff`/`roundtrip-tab` (CF-02). |
| Edit controller (undo/redo, dirty, mutation verbs) | Editor-side controller (`IStringTableEditController`) — confirm home vs Phase 9 `DatatableEditController` | Framework (if Phase 9 put it there) | Editor-local undo, independent of scene `UndoRedoManager` (CF-04). |
| Editor UI (grid, toolbar, modals) | TJT WinForms (`UtinniPlugins/.../FormStringTableEditor`) | — | Subpanel-inside-TJT (CF-06); `UtinniForm` via `GetForms()`. |
| Entry points (file picker + TRE Browser hand-off) | TJT WinForms (`FormTreBrowser` context menu) | Framework (`LooksLikeStf` magic sniff) | Hand-off wiring is cross-repo: detection primitive is framework, menu wiring is TJT (D-04). |
| Save targets (loose override / Save-As / `.tre` repack) | Framework save infra (`Saving/`, `TreWriter.Repack`) | TJT (Save▾ menu wiring) | Reuse Phase 8 save targets verbatim (CF-03). |
| Reload classification + badge | Framework (`ReloadAssetClassifier`, existing) | TJT (badge label) | `.stf` already classified tier-(b); do NOT re-implement (CF-05). |
| CSV / PO serialization | Framework or CLI-shared helper | TJT (file pickers + preview modal) | Pure text transforms; keep serialization logic framework/CLI-testable, UI in TJT. |

## Standard Stack

This is an **in-repo extension phase**, not a library-selection phase. No new external packages are required or recommended. The "stack" is the existing Utinni codebase + BCL.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `UtinniCoreDotNet` (this repo) | current | Framework-side `Formats/`, `Saving/`, `UI/Controls`, `PluginFramework/IEditorPlugin` | The CF-01 consumption path; all format primitives live here. |
| `System.Windows.Forms` (BCL, .NET Framework) | repo target | Editor UI host, `DataGridView`, `OpenFileDialog`/`SaveFileDialog` | Existing TJT stack; no component registry (UI-SPEC §Registry Safety: N/A). |
| `System.Text.Encoding.Unicode` (BCL) | — | UTF-16LE encode/decode for `.stf` text — **little-endian** (`Encoding.Unicode`, not `BigEndianUnicode`) | Format is UTF-16**LE**; BCL `Encoding.Unicode` is LE. Do NOT hand-roll surrogate handling. |
| `System.Text.UTF8Encoding(emitBOM:true)` (BCL) | — | CSV/TSV export encoding (D-03b: UTF-8 **with BOM**) | `new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)` emits the BOM spreadsheets need for non-ASCII. |
| xUnit (existing `Utinni.Cli.Tests` / `UtinniCoreDotNet.Tests`) | repo version | Golden round-trip + model unit tests | Established test framework (Phase 1). |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `ThemedDataGridView` (Phase 9, TJT-side) | current | Two-column (key, text) grid | The main editing surface (UI-SPEC §Design System); inherit token map verbatim. |
| `FormSaveConfirmDialog` (Phase 8) | current | Risk-proportional confirmations (repack / discard-while-dirty) | REUSE — do NOT clone (UI-SPEC). |
| `FormCsvImportPreviewDialog` (Phase 9) | current | CSV delta-import preview | REUSE the shape (simpler: one text column, no type-invalid red list). |
| `ReloadAssetClassifier` (Phase 8) | current | tier-(b) classification for `.stf` | Consume directly; do NOT re-classify. |
| `TreWriter.Repack` (Phase 8) | current | `.tre` repack save target (mode 4) | Reuse; refuses V6000 (WR-06). |
| `IffPayloadCursor` (Phase 7) | current | Generic LE byte reader (despite `Iff` name) | CONTEXT confirms reusable for the flat `.stf`. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Canonical re-serialize (D-02 default) | Per-entry original-byte preservation (Phase 9 CF-04 style) | Escalate ONLY if fixtures prove real `.stf` files exist with non-canonical ordering that must be preserved. More machinery; adopt only with evidence (D-02 escalation clause). |
| Hand-rolled PO/gettext writer | A gettext/.NET PO library | CONTEXT discretion leans **hand-rolled** — the `.po` export is a trivial `msgid`/`msgstr` text emit; an external dep is overkill and adds a slopcheck/version surface for ~30 lines. Recommend hand-rolled. |
| Hand-rolled CSV escaping | A CSV library (e.g. CsvHelper) | Phase 9 already shipped a `DatatableCsvSerializer`; **reuse/port that escaping** rather than add a dep or re-invent. RFC-4180 quoting is the one footgun (see Pitfalls). |

**Installation:** None. No `npm`/`pip`/`cargo`/NuGet install. (UI-SPEC §Registry Safety confirms: no third-party UI registry, no new package install.)

## Package Legitimacy Audit

**N/A — this phase installs no external packages.** All work extends the in-repo `UtinniCoreDotNet` framework and `UtinniPlugins` TJT assembly using BCL types only. No `npm`/`PyPI`/`crates`/NuGet additions. The slopcheck gate is not applicable. (If the planner reverses the PO/CSV "hand-roll" recommendation and pulls in a gettext or CSV NuGet package, run the Package Legitimacy Gate at that time and gate the install behind a `checkpoint:human-verify` task.)

## Architecture Patterns

### System Architecture Diagram

```
                          ┌─────────────────────────────────────────────┐
   Entry point 1:         │            FormStringTableEditor             │
   Open… file picker ────►│              (UtinniForm, TJT)               │
                          │  ┌────────────────────────────────────────┐ │
   Entry point 2:         │  │ Toolbar: Open/Save▾/Undo/Redo/Add/Remove│ │
   TRE Browser ──────────►│  │  /Import CSV/Export CSV/Export PO/Find  │ │
   "Open in String-table  │  │  /Replace/Filter/Show id/Reload badge   │ │
    Editor" (LooksLikeStf │  ├────────────────────────────────────────┤ │
    magic sniff)          │  │ ThemedDataGridView  [Key | Text | (id)] │ │
                          │  └──────────────┬─────────────────────────┘ │
                          └─────────────────┼───────────────────────────┘
                                            │ mutation verbs (undoable)
                                            ▼
                          ┌─────────────────────────────────────────────┐
                          │      IStringTableEditController (CF-04)      │
                          │  editor-local undo/redo  ·  dirty tracking   │
                          │  edit-text / add / remove / rename-key       │
                          │  CSV-import delta (one transaction)          │
                          └─────────────────┬───────────────────────────┘
                                            │ wraps
                                            ▼
       ┌──────────────────────────────────────────────────────────────────────┐
       │            UtinniCoreDotNet/Formats/StringTable/  (CF-01, framework)   │
       │  MutableStringTableDocument  ·  StringTableEntry  ·  StringTableWriter │
       │           composes ↓ (reuse, recommended)                             │
       │  Formats/Decoders/StringTableDecoder.cs  (Phase 7, READ-ONLY parse)    │
       └───────────────┬───────────────────────────────────┬──────────────────┘
                       │ parse                              │ serialize (canonical:
                       ▼                                    │  strings id↑, names name↑)
              ┌────────────────┐                            ▼
              │  .stf bytes    │◄──────────────── Save targets (Phase 8, CF-03):
              │ magic 0xABCD   │  mode 1 loose override · mode 2 Save/Save-As
              │ ver · nextId   │  mode 4 .tre repack (refuses V6000)
              │ count          │  (mode 3 live-patch DISABLED inherited)
              │ string block   │
              │ name block     │            ReloadAssetClassifier (Phase 8):
              └────────────────┘            .stf → tier-(b) "reloads on next scene change"
                       ▲                                    │
                       │ parse→mutate→serialize→re-parse    ▼ badge text (CF-05, locked)
                       │ byte-exact (untouched)      FormStringTableEditor reload badge
              ┌────────────────────────────┐
              │ Utinni.Cli  roundtrip-stf   │  ← automated SC4 gate (CF-02)
              │ Utinni.Cli.Tests goldens    │     (CI-coverable correctness)
              └────────────────────────────┘
```

A reader can trace SC2 (open→view→edit→save): file enters via picker or TRE hand-off → decoder parses → grid binds Key/Text → user edits route through the controller (undoable) → Save▾ serializes canonically through a Phase 8 save target → reload badge tells the user it re-resolves on next scene change. SC4 (`João`) is independently proven by the CLI `roundtrip-stf` golden.

### `.stf` On-Disk Layout (the load-bearing format spec)

`[CITED: UtinniCoreDotNet/Formats/Decoders/StringTableDecoder.cs header comment + SOE LocalizedStringTable*.{h,cpp}]` — documented in CONTEXT.md; **planner re-verify against the decoder in the first plan's Wave-0.**

```
HEADER (fixed):
  magic        : long  (value 0xABCD)   ← CONFIRM the on-disk WIDTH of `magic`:
                                           CONTEXT says "magic(0xABCD, long)"; SOE ms_MAGIC is a `long`
                                           (4 bytes on 32-bit SOE). The decoder is the source of truth —
                                           re-verify width; the writer MUST emit the identical width.
  version      : byte   (0 or 1 supported; load_0000 vs load_0001)
  nextUniqueId : u32    (the engine's m_nextUniqueId — auto-id high-water mark)
  count        : u32    (number of entries; same count drives both blocks)

STRING BLOCK (count entries, written id-ascending — std::map<id,…> order):
  id        : u32
  sourceCrc : u32    (NOT a content hash — int(time(0)) timestamp; see D-02b)
  charCount : u32    (number of UTF-16 code units, NOT bytes)
  text      : UTF-16LE, charCount code units (no NUL terminator on disk per SOE str_write — CONFIRM)

NAME BLOCK (count entries, written name-ascending lexicographic — std::map<name,id> order):
  id     : u32
  length : u32       (ASCII char count)
  name   : ASCII, length bytes
```

**Endianness/alignment gotchas:**
- **Everything is little-endian.** Use BCL `BinaryReader`/`BinaryWriter` (LE by default) or the existing `IffPayloadCursor` (a generic LE byte reader, reusable per CONTEXT).
- **Text is UTF-16LE** → `Encoding.Unicode` (NOT `BigEndianUnicode`). `charCount` is **code units**, so byte length = `charCount * 2`.
- **No word-padding** between chunks (this is NOT IFF; the IFF pad gotcha from memory `project_swg_iff_no_pad` does NOT apply — there are no IFF chunks here at all).
- **Two separate blocks, two separate orderings.** The string block is sorted by **id**; the name block by **name**. Re-serializing requires two independent stable sorts. This is the byte-exactness mechanism (D-02).
- **`magic` width is the one ambiguity** — CONTEXT writes "magic(0xABCD, long)". The decoder already reads it correctly; the writer must match. The first plan's Wave-0 reads the decoder to lock this.

### What `roundtrip-stf` byte-exact gate must cover (CF-02)
1. Parse a real-ish `.stf` → serialize unchanged → **byte-identical to input** (canonical-order assumption; failure on a real fixture is the D-02 escalation signal — capture as a finding, not a silent normalize).
2. Parse → edit one entry's text → serialize → re-parse → **all untouched entries byte-identical**; edited entry reflects the change.
3. A **`João`-bearing fixture** (and ideally a smart-quote/ellipsis/CR-bearing fixture) round-trips with **zero byte change** to untouched entries and **no smart-quote substitution** (the SC4 + STAB-03 gate).
4. Add-entry: `nextUniqueId` advances correctly; new id unique; auto-name `NNN_default` valid.
5. Remove + rename: counts and both block orderings stay consistent.

### Recommended Project Structure
```
UtinniCoreDotNet/
└── Formats/
    └── StringTable/                       # NEW (CF-01) — sibling of Formats/Iff/, Formats/Datatable/
        ├── MutableStringTableDocument.cs  # mutable model: entries + nextUniqueId; MarkSaved rebaseline
        ├── StringTableEntry.cs            # id (machine-managed), name (key), text (UTF-16), sourceCrc, original-bytes baseline
        ├── StringTableWriter.cs           # canonical serialize: header + string block (id↑) + name block (name↑ ordinal)
        └── StringTableNameRules.cs        # validateStringName / fixupStringName port (D-01)
Utinni.Cli/
└── Commands/
    └── RoundtripStfCommand.cs             # NEW (CF-02) — mirrors RoundtripIffCommand/RoundtripTabCommand
Utinni.Cli.Tests/
└── fixtures/stf/                          # NEW goldens (real-ish .stf + João fixture + builder)
UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/
├── UI/Forms/
│   ├── FormStringTableEditor.cs(.Designer.cs)   # NEW — sibling of FormDatatableEditor
│   └── (reuse) FormCsvImportPreviewDialog, FormSaveConfirmDialog
├── (Editing)/
│   └── StringTableEditController.cs              # NEW — editor-local undo/redo, dirty, mutation verbs (CF-04)
├── (Export)/
│   ├── StringTablePoExporter.cs                  # NEW — hand-rolled minimal PO (D-03d)
│   └── (reuse/port) DatatableCsvSerializer       # CSV escaping (D-03b)
└── Plugin.cs                                     # register FormStringTableEditor in GetForms()
```
*(Confirm exact existing folder names — the controller/serializer/export sub-folders are inferred from the Phase 9 references in CONTEXT; the planner aligns to the real Phase 9 layout in Wave-0.)*

### Pattern 1: Mutable model wraps the proven decoder + baselines original bytes
**What:** `MutableStringTableDocument` parses via the Phase 7 `StringTableDecoder` (reuse), then holds each entry with its **original on-disk bytes** captured at load. `MarkSaved()` rebaselines (new bytes become the clean baseline). Dirty = current-serialized-bytes ≠ baseline (per entry).
**When to use:** Always — this is how D-02 byte-exactness + the UI dirty-state (UI-SPEC cell-state overlays) are both satisfied from one source of truth.
**Why:** Canonical re-serialize gives whole-table byte-exactness; per-entry baselines give the *per-cell* dirty indicator the UI needs and the CSV delta-import "keep original bytes for unchanged" guarantee (D-03b).

### Pattern 2: Canonical serialize = two independent stable sorts
**What:** `StringTableWriter` emits header → string block (entries sorted by `id` ascending) → name block (entries sorted by `name` ascending, ordinal/byte). Use a **stable, culture-invariant** sort for names (`StringComparer.Ordinal`) to match `std::map<std::string>` byte ordering — NOT a culture-aware compare.
**When to use:** Every save.
**Why:** Matches SOE `std::map` iteration → byte-exact for canonical inputs. `StringComparer.Ordinal` is critical: a culture-aware sort would reorder names differently from the C++ `std::map<std::string>` (compares by byte). **Flag: confirm SOE names compare case-sensitively by raw byte (they do, via default `std::map<std::string>` `<`).**

### Pattern 3: Edit controller as the single undoable funnel (CF-04)
**What:** Every mutation (edit text, add, remove, rename, CSV-import-delta) goes through `IStringTableEditController`, which pushes an inverse onto an **editor-local** undo stack. CSV import applies as **one transaction** (single undo unit).
**When to use:** All grid + toolbar + import mutations. Ctrl+Z/Ctrl+Y caught at the form via `ProcessCmdKey` BEFORE the `DataGridView` sees them; MUST NOT dispatch to scene `UndoRedoManager` (CON-M-05 / CF-04).
**Why:** Phase 8/9 precedent; keeps string-table edits out of scene state.

### Anti-Patterns to Avoid
- **Replicating `unfubarMicrosoftInvalidTextCharacters`.** The SOE engine rewrites smart-quotes/ellipsis/CR on load+write. **Do NOT port it.** Utinni preserves text byte-faithfully (D-02 / SC4 / STAB-03). The single most important non-behavior.
- **Culture-aware name sorting.** Use `StringComparer.Ordinal` to match `std::map<std::string>` byte ordering, else canonical re-serialize won't be byte-exact.
- **Treating `sourceCrc` as a content hash.** It's a timestamp. Do not compute it from text. (Policy is D-02b — confirm client-read first.)
- **Re-implementing reload classification.** `.stf` is already tier-(b) in `ReloadAssetClassifier`. Consume it (CF-05).
- **Letting column sort or filter mutate on-disk order.** Sort + filter are **view-only** (D-03c). Save always serializes canonically.
- **Widening `IEditorPlugin` / changing existing `Formats/*` public signatures** without rebuilding plugins in the same commit (memory `feedback_caller_attrs_binary_compat`). Add new types only.
- **`SendToBack` on the Fill grid.** Add the grid FIRST so it docks front-most (memory `feedback_winforms_dockfill_zorder`; UI-SPEC CF-09).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| UTF-16LE encode/decode | Custom surrogate-pair handler | `System.Text.Encoding.Unicode` (BCL, LE) | Surrogate pairs, `João` combining forms, BMP-vs-astral all handled. `charCount` is code units → bytes = ×2. |
| `.stf` parse | New parser | **Reuse Phase 7 `StringTableDecoder.cs`** (CF-01) | Proven parse path; handles version 0/1, `0xABCD` magic, U+FFFD replacement, `LooksLikeStf` sniff. |
| Two-column editable grid | Hand-painted control / `ListView` | **`ThemedDataGridView`** (Phase 9) | Cell editing, theming token map, sort glyphs already done. |
| Risk confirmations | New dialog | **`FormSaveConfirmDialog`** (Phase 8) | UI-SPEC: REUSE, do not clone. |
| CSV import preview | New modal | **`FormCsvImportPreviewDialog`** (Phase 9) | Same shape, simpler (one text column). |
| `.tre` repack | New repacker | **`TreWriter.Repack`** (Phase 8) | CRC/TOC rebuild; refuses V6000 (WR-06). |
| Reload tiering | New classifier | **`ReloadAssetClassifier`** (Phase 8) | `.stf` already tier-(b). |
| Save-target plumbing | New save modes | **Phase 8 save targets** | Modes 1/2/4 reuse verbatim (CF-03). |
| CSV escaping | New RFC-4180 quoter | **Port/reuse Phase 9 `DatatableCsvSerializer`** | Quote/escape rules are a footgun; reuse the audited one. |
| PO export | A full gettext library | **Hand-rolled minimal `msgid`/`msgstr` writer** (D-03d) | Export is ~30 lines; only need `msgid "key"` / `msgstr "text"` with C-string escaping. **Caveat:** escape `"`, `\`, newlines per PO C-string rules — the one non-trivial bit. |

**Key insight:** Phase 10's value is almost entirely **reuse of the Phase 7/8/9 chassis**. The genuinely new code is small: a flat-binary writer (header + two sorted blocks), a name-rules validator, a `roundtrip-stf` verb, one new Form, one controller, two trivial exporters (CSV reuse + PO hand-roll). The flat format is *simpler* than Phase 9's IFF chunks, so resist over-engineering the writer.

## Common Pitfalls

### Pitfall 1: Assuming canonical re-serialize is byte-exact without fixture proof
**What goes wrong:** Writer normalizes a real `.stf` that was NOT in canonical order, silently changing untouched bytes → `roundtrip-stf` fails on a real fixture, or a save reorders entries the user expected preserved.
**Why it happens:** D-02 default *assumes* real engine/SOE-tool output is canonically ordered (it should be, given `std::map` iteration). Hand-authored / third-party-tool `.stf` files might not be.
**How to avoid:** The CLI-golden plan must include **at least one real `.stf` from an actual SWG client** as a fixture and assert unchanged-round-trip. If it fails → invoke the D-02 escalation (per-entry original-byte + original-order preservation). Treat a failure as a *finding*, not a bug to patch by normalizing.
**Warning signs:** `roundtrip-stf` byte-diff on a never-edited file.

### Pitfall 2: Wrong name-sort comparer breaks byte-exactness
**What goes wrong:** Names sorted with a culture-aware/case-insensitive comparer don't match `std::map<std::string>` byte ordering → name block bytes differ → round-trip fails.
**Why it happens:** C# default string sort is culture-aware; `std::map<std::string>` uses byte `operator<`.
**How to avoid:** `StringComparer.Ordinal`. Confirm SOE comparison is raw-byte case-sensitive (it is, by default `std::map`).
**Warning signs:** Name-block-only byte diffs, especially with mixed-case keys.

### Pitfall 3: `nextUniqueId` mismanaged on add → id collision or non-determinism
**What goes wrong:** Add-entry reuses an id, or `nextUniqueId` isn't advanced/persisted → engine lookup collisions or a non-byte-exact header.
**Why it happens:** `nextUniqueId` is a header field that must advance exactly as SOE `addString` advances it.
**How to avoid:** Port SOE `addString`'s `m_nextUniqueId` maintenance exactly: new id = current `nextUniqueId`, then increment. Untouched-file round-trip must preserve the original `nextUniqueId` unchanged.
**Warning signs:** Header byte diff after an add; duplicate ids.

### Pitfall 4: `sourceCrc` policy makes goldens non-deterministic
**What goes wrong:** Setting fresh `int(time(0))` on edit/add makes `roundtrip-stf` goldens non-reproducible (timestamp changes every run).
**Why it happens:** Engine-faithful crc is a wall-clock timestamp.
**How to avoid:** For goldens, prefer the **deterministic** policy (preserve-original for untouched; 0-or-preserve for edited) — BUT this depends on D-02b, which depends on whether the client reads `sourceCrc`. **Resolve the Open Question first.** If the client ignores `sourceCrc`, choose deterministic.
**Warning signs:** Flaky golden tests that pass twice then fail.

### Pitfall 5: WinForms `DataGridView` / IME smart-substitution alters committed text bytes
**What goes wrong:** OS/IME auto-correct or a control's smart-quote substitution silently changes `"`→`"` on commit → SC4/STAB-03 regression.
**Why it happens:** Some text-edit controls apply substitutions; the `DataGridViewTextBoxColumn` editing control must not.
**How to avoid:** UI-SPEC §Unicode Fidelity mandates auto-correct OFF on the text editing control. Verify committed bytes equal typed bytes in the controller; the round-trip golden uses a `João`/smart-quote fixture as the backstop.
**Warning signs:** Round-trip diffs that only appear after manual UI edits, not CLI edits.

### Pitfall 6: WinForms TJT assembly is not project-referenceable from the x86 test project
**What goes wrong:** Planner writes xUnit tests against `FormStringTableEditor` → won't compile/reference.
**Why it happens:** Phase 8/9 precedent — the WinForms TJT assembly can't be project-referenced from the x86 test project; `dotnet build` also can't compile TJT `.resx` image resources (memory `feedback_dotnet_build_msbuild_resources`).
**How to avoid:** Test the **framework leg** (model, writer, name rules, CSV/PO serializers, `roundtrip-stf`) in CI; the **UI leg** is maintainer live-smoke (Phase 9 precedent: automation-augmented + ACK-deferred-acceptable for V1). Build with VS2026 MSBuild, run xUnit via `dotnet test --no-build`.
**Warning signs:** Test project won't reference the editor; `dotnet build` MSB3823 on `.resx`.

## Code Examples

### Canonical serialize (the core writer shape)
```csharp
// Source: pattern derived from SOE LocalizedStringTableReaderWriter::write() ordering
// [CITED: swg-client-v2/.../localization/src/shared/LocalizedStringTableReaderWriter.cpp]
// Planner: confirm exact field widths against StringTableDecoder.cs before committing.
void Write(BinaryWriter w, MutableStringTableDocument doc)
{
    w.Write(doc.Magic);            // 0xABCD — match decoder's read width exactly
    w.Write(doc.Version);          // byte
    w.Write(doc.NextUniqueId);     // u32
    w.Write((uint)doc.Entries.Count);

    // String block: id-ascending (std::map<id,...> order)
    foreach (var e in doc.Entries.OrderBy(e => e.Id))
    {
        w.Write(e.Id);                              // u32
        w.Write(e.SourceCrc);                       // u32  (D-02b policy applied upstream)
        w.Write((uint)e.Text.Length);              // u32  charCount = UTF-16 code units
        w.Write(Encoding.Unicode.GetBytes(e.Text)); // UTF-16LE, no NUL (CONFIRM no terminator)
    }

    // Name block: name-ascending ORDINAL (std::map<string,...> byte order)
    foreach (var e in doc.Entries.OrderBy(e => e.Name, StringComparer.Ordinal))
    {
        w.Write(e.Id);                              // u32
        w.Write((uint)e.Name.Length);              // u32  ASCII length
        w.Write(Encoding.ASCII.GetBytes(e.Name));
    }
}
```

### Name validation (D-01 — port the engine rules)
```csharp
// Source: SOE LocalizedStringTable validateStringName / fixupStringName / RW::rename
// [CITED: swg-client-v2/.../localization/src/shared/LocalizedStringTable.cpp]
// Planner/Wave-0: confirm the EXACT rule set — these are CONTEXT's enumerated rules; there may be more.
static bool IsValidStringName(string name, out string error)
{
    if (string.IsNullOrEmpty(name)) { error = "String name can't be empty."; return false; }
    if (char.IsDigit(name[0]))       { error = "String names can't start with a digit. Pick another key."; return false; }
    // CONFIRM additional rules: allowed character set? case rules? max length? whitespace?
    error = null; return true;
}
// Duplicate check is at the document level (two entries can't share a name).
```

### CSV export encoding (D-03b — UTF-8 with BOM)
```csharp
// Non-ASCII (João) must survive the spreadsheet round-trip → BOM required.
using var sw = new StreamWriter(path, append: false,
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)); // emits BOM
// ... reuse Phase 9 DatatableCsvSerializer escaping for (key, text) rows ...
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| SOE-era `SwgStringEditor` (standalone Win32 tool) | TJT SubPanel String-table Editor (`FormStringTableEditor`) | Phase 10 | Replaces the standalone editor at T4 parity for existing files. |
| Engine `unfubarMicrosoftInvalidTextCharacters` text mutation on load+write | Utinni preserves text byte-faithfully (NO unfubar) | Phase 10 (D-02) | Deliberate divergence from engine; honors SC4 + STAB-03. |
| Phase 9 IFF hybrid-DOM original-byte capture for byte-exactness | Canonical re-serialize (id↑ / name↑) for the flat `.stf` | Phase 10 (D-02) | Simpler — flat format makes ordering the byte-exactness mechanism. |

**Deprecated/outdated:**
- ROADMAP's "layers on Phase 8's IFF read/write" for Phase 10 — **inaccurate.** `.stf` is NOT IFF. (Authoritative correction in CONTEXT + UI-SPEC; reaffirmed here.)
- The IFF Editor "switch to typed view" hand-off (Phase 9 pattern) — **N/A** for `.stf`.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `.stf` on-disk layout (magic/version/nextUniqueId/count + string block + name block) is exactly as CONTEXT documents and as `StringTableDecoder.cs` reads it | Format layout | HIGH — wrong field widths/order corrupt every write. **Mitigation: first plan's Wave-0 re-reads the decoder before writing the writer.** |
| A2 | `magic` is written as a `long` matching the decoder's read width (4 bytes on 32-bit SOE) | Format layout | MED — header byte mismatch; caught by the first round-trip golden. |
| A3 | UTF-16 text has no on-disk NUL terminator (charCount is exact code-unit count) | Format layout | MED — off-by-one bytes; caught by round-trip golden. Confirm vs decoder + SOE `str_write`. |
| A4 | Name sort is ordinal/byte (`std::map<std::string>` default) | Pattern 2 / Pitfall 2 | MED — name-block reorder breaks byte-exactness for mixed-case keys. Confirm vs SOE. |
| A5 | The live client does NOT read `sourceCrc` at string-lookup time | D-02b / Pitfall 4 | MED — drives crc-on-edit policy + golden determinism + SC3 ("live client renders edited strings"). **Confirm via LocalizationManager source.** |
| A6 | Real engine/SOE-tool `.stf` files ARE canonically ordered (so re-serialize is byte-exact) | D-02 / Pitfall 1 | MED-HIGH — if false, must escalate to per-entry original-byte preservation. **Confirm with a real-client `.stf` fixture in the CLI-golden plan.** |
| A7 | Exact name-validation rule set is {non-empty, no-leading-digit, no-duplicate} (possibly more) | D-01 / Name validation | MED — extra/missing rules cause rejected-valid or accepted-invalid keys. Confirm vs `validateStringName`/`fixupStringName`. |
| A8 | `.stf` re-resolves on scene change (CF-05 tier-(b)) vs requires relog | CF-05 / SC3 | MED — if relog-only, badge wording must say so honestly (CONTEXT permits this). Confirm vs LocalizationManager cache semantics. |
| A9 | Phase 9 `DatatableCsvSerializer` escaping is reusable/portable for `.stf` CSV | Don't-Hand-Roll | LOW — worst case re-port the escaping. |
| A10 | WinForms TJT assembly remains non-project-referenceable from x86 test project (Phase 8/9 precedent holds) | Test strategy / Pitfall 6 | LOW — drives the framework-leg-tests + maintainer-smoke split; precedent is strong. |
| A11 | Edit controller home + exact Phase 9 folder names (`Editing/`, `Export/`) | Project structure | LOW — cosmetic; Wave-0 aligns to the real Phase 9 layout. |

**Note:** A1–A8 are the load-bearing format/behavior assumptions. A1–A4 + A7 are confirmable by reading two files (`StringTableDecoder.cs` + the SOE `LocalizedStringTable*` source) — this should be **the first plan's Wave-0 first task**. A5/A8 require reading `LocalizationManager.cpp`. A6 requires a real `.stf` fixture (CLI-golden plan).

## Open Questions

1. **Does the live client read `sourceCrc` at string-lookup time? (D-02b, A5)**
   - What we know: `sourceCrc` is `int(time(0))` (a timestamp, not a content hash) per SOE `addString`. CONTEXT flags this as decision-bearing for the crc-on-edit policy and SC3.
   - What's unclear: whether the runtime even consults it when resolving `@stringfile:key`.
   - Recommendation: first plan's Wave-0 reads `swg-client-v2/.../localization/.../LocalizationManager.cpp` (+ `LocalizedString.cpp`). If unused at lookup → choose the **deterministic** crc policy (preserve-original for untouched; 0-or-preserve for edited) for golden reproducibility. If used → set engine-faithful and use a crc-masking comparison in the golden.

2. **Is real-client `.stf` output canonically ordered? (D-02, A6)**
   - What we know: SOE writer iterates `std::map` → canonical id↑ / name↑. Should be canonical.
   - What's unclear: whether any real file in the wild is non-canonical.
   - Recommendation: the CLI-golden plan includes a **real extracted `.stf`** (e.g. `creature_names`, a `quest/*` table) as a golden fixture and asserts byte-exact unchanged round-trip. Failure → D-02 escalation. Fixtures live in-repo per Phase 4 CON-O-09 (confirm the `Utinni.Cli.Tests/fixtures/` location used by `roundtrip-iff`/`roundtrip-tab`).

3. **Exact name-validation + fixup rule set? (D-01, A7)**
   - What we know: no-leading-digit, no-duplicate, non-empty (CONTEXT).
   - What's unclear: allowed character set, case handling, max length, whitespace; and what `fixupStringName` *mutates* vs what `validateStringName` *rejects*.
   - Recommendation: first plan's Wave-0 reads `LocalizedStringTable.cpp` `validateStringName`/`fixupStringName` + `LocalizedStringTableReaderWriter.cpp` `rename`. Port exactly. UI-SPEC specifies **reject + revert**, so port the *validate/reject* path and surface the locked validation copy.

4. **Where does the edit controller live — framework or TJT? (Architecture map, A11)**
   - What we know: Phase 9 has a `DatatableEditController`; CONTEXT references it as the port source.
   - What's unclear: whether it lives in `UtinniCoreDotNet` or the TJT assembly.
   - Recommendation: first plan's Wave-0 locates `DatatableEditController.cs` and mirrors its home. (Editor-local undo is an editor concern → likely TJT-side, but confirm.)

5. **`.stf` reload semantics: scene change vs relog? (CF-05, A8)**
   - What we know: classified tier-(b); CONTEXT instructs honest wording either way.
   - What's unclear: whether `LocalizationManager` re-reads `.stf` on scene change or caches until relog.
   - Recommendation: same source read as Q1. If relog-only, the locked badge text must be adjusted to say so (CONTEXT explicitly permits this honesty adjustment despite "locked wording").

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| VS 2026 MSBuild (Dev18) | Building `UtinniCoreDotNet` + TJT WinForms assembly | ✓ (memory `project_vs2026_toolchain` / `reference_windows_toolchain_paths`) | v145 toolset | VS 2022 on disk |
| `dotnet test` | Running xUnit framework-leg + CLI goldens | ✓ | repo target | use `--no-build` after MSBuild (`dotnet build` fails on TJT `.resx` — memory `feedback_dotnet_build_msbuild_resources`) |
| Self-hosted CI runner | CI gate (push-triggered) | ✓ (memory `project_self_hosted_ci`) | v145/VS2026 | — |
| swg-client-v2 reference corpus | Confirming format/name-rules/crc/reload (read-only) | ✓ (`D:/Code/swg-client-v2`, memory `project_swg_client_v2_reference`) | — | CONTEXT's documented layout (already captured) |
| A real extracted `.stf` fixture | `roundtrip-stf` golden (A6 confirmation) | ✗ (must be sourced/extracted) | — | Build a synthetic canonical `.stf` via a fixture builder (Phase 8 `roundtrip-iff` builder analog) — but a real file is needed to *confirm* A6. |
| Live SWG client | SC1/SC3 maintainer smoke | maintainer-run | — | Phase 9 precedent: automation-augmented + ACK-deferred-acceptable for V1. |

**Missing dependencies with no fallback:** none that block planning.
**Missing dependencies with fallback:** a **real `.stf` fixture** — a synthetic canonical fixture covers round-trip mechanics, but A6 (real-world canonical-ordering) can only be *confirmed* with a real file. Planner adds a task to extract one (e.g. via the Phase 7 TRE Browser / `decode-iff` path) or have the maintainer supply one.

## Validation Architecture

> `workflow.nyquist_validation` is absent in config.json → treated as enabled.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (existing `Utinni.Cli.Tests` + `UtinniCoreDotNet.Tests`) |
| Config file | none beyond the `.csproj` test projects (Phase 1 scaffold) |
| Quick run command | `dotnet test Utinni.Cli.Tests --no-build` (after MSBuild) — fast golden + model tests |
| Full suite command | VS2026 MSBuild Release/x86 build, then `dotnet test --no-build` across test projects |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PROD-W1-STF (SC4) | Non-ASCII (`João`) round-trips byte-exact; no smart-quote substitution | golden (CLI) | `dotnet test Utinni.Cli.Tests --filter Stf --no-build` | ❌ Wave 0 (CLI-golden plan) |
| PROD-W1-STF (byte-exact) | Untouched entries byte-identical after parse→mutate→serialize | golden (CLI) | same | ❌ Wave 0 (CLI-golden plan) |
| D-01 (add/rename) | `nextUniqueId` advances; name rules enforced; no dup | unit (framework) | `dotnet test UtinniCoreDotNet.Tests --filter StringTable --no-build` | ❌ Wave 0 (model plan) |
| D-02 (canonical order) | Re-serialize emits id↑ / name↑-ordinal blocks | unit (framework) | same | ❌ Wave 0 (model plan) |
| D-03b (CSV delta) | Import marks only changed entries dirty; UTF-8+BOM export | unit (framework/CLI-shared serializer) | same | ❌ Wave 0 |
| D-03d (PO export) | `msgid`/`msgstr` emit with C-string escaping | unit | same | ❌ Wave 0 |
| SC1/SC2/SC3 (UI + live) | Subpanel loads; open/edit/save; live render on reload | manual maintainer smoke | live-SWG session (Phase 9 precedent) | manual-only |

### Sampling Rate
- **Per task commit:** `dotnet test <relevant project> --no-build`
- **Per wave merge:** full MSBuild + `dotnet test --no-build` (all test projects)
- **Phase gate:** full suite green before `/gsd:verify-work`; maintainer live-smoke (automation-augmented, ACK-deferred-acceptable) for SC1/SC3.

### Wave 0 Gaps
- [ ] `UtinniCoreDotNet.Tests/Formats/StringTable/StringTableWriterTests.cs` — covers D-01/D-02 (canonical order, add/rename, nextUniqueId)
- [ ] `UtinniCoreDotNet.Tests/Formats/StringTable/StringTableNameRulesTests.cs` — covers D-01 validation
- [ ] `Utinni.Cli.Tests/RoundtripStfTests.cs` + `fixtures/stf/` (synthetic canonical fixture, `João` fixture, smart-quote/ellipsis fixture, ideally one real extracted `.stf`) — covers SC4 + byte-exact (CLI-golden plan)
- [ ] CSV + PO serializer tests (framework/CLI-shared) — covers D-03b/D-03d
- [ ] (UI leg) no automated test infra — maintainer smoke checklist (mirror Phase 9 `09-07-SMOKE-LOG.md`)

## Security Domain

> `security_enforcement` not set to `false` in config → included. This is a local desktop file-editor; the threat surface is narrow (untrusted file parsing).

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A (local desktop tool) |
| V3 Session Management | no | N/A |
| V4 Access Control | no | N/A |
| V5 Input Validation | **yes** | Defensive `.stf` parsing: bounds-check `count`/`charCount`/`length` against file size before allocating/reading; the existing `StringTableDecoder` + `DecoderException` model this — the **writer** path and CSV/PO **import** path must validate lengths too. Name validation (D-01) is input validation. |
| V6 Cryptography | no | `sourceCrc` is NOT cryptographic (a timestamp); do not treat it as integrity. No crypto in scope. |

### Known Threat Patterns for {flat-binary file parser + WinForms editor}
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed `.stf` (huge `count`/`charCount`/`length`) → OOM / OOB read | Denial of Service | Bounds-check all length fields against remaining buffer before read/alloc; reuse the Phase 7 decoder's `DecoderException` discipline; `IffPayloadCursor` bounds-checks. |
| CSV/PO import injects malformed text → corrupt write | Tampering | Re-encode imported text to UTF-16LE verbatim; the per-entry delta + preview modal (D-03b) gives a confirmation gate before apply. |
| Repack writes to wrong/unintended `.tre` | Tampering | Inherited Phase 8 provenance-gating + `FormSaveConfirmDialog` confirm + opt-in backup (WR-06 refuses V6000). |
| Smart-quote/encoding "fix" silently corrupts text | Tampering (data integrity) | The explicit NO-unfubar non-behavior (D-02) + `roundtrip-stf` golden as the regression backstop. |

## Sources

### Primary (HIGH confidence)
- `.planning/phases/10-tjt-subpanel-string-table-editor-stf/10-CONTEXT.md` — LOCKED decisions D-01..D-04, CF-01..CF-06, format layout, swg-client-v2 reference paths, code-context, deferred ideas. **Read in full.**
- `.planning/phases/10-tjt-subpanel-string-table-editor-stf/10-UI-SPEC.md` — approved UI contract (host placement, columns, states, copywriting, Unicode-fidelity non-behavior, success-criteria mapping). **Read in full.**
- `.planning/ROADMAP.md` §Phase 10 — goal + 4 success criteria + preservation guard-rails (CON-M-01/02, CON-T-05, STAB-03). **Read in full.**
- `.planning/config.json` — worktrees off, nyquist enabled (absent→on), brave_search available. **Read in full.**

### Secondary (MEDIUM confidence — documented but not re-verified line-by-line this session)
- `[CITED]` `UtinniCoreDotNet/Formats/Decoders/StringTableDecoder.cs` (Phase 7) — documented layout/`LooksLikeStf` (via CONTEXT). **Planner re-read in the first plan's Wave-0.**
- `[CITED]` `swg-client-v2/.../localization/src/shared/LocalizedStringTable.{h,cpp}`, `LocalizedStringTableReaderWriter.{h,cpp}`, `LocalizedString.{h,cpp}`, `LocalizationManager.cpp` — authoritative format/write/add/rename/crc/reload spec (via CONTEXT annotations). **Read to resolve A4–A8 / Open Q 1,3,5.**
- `[CITED]` Phase 9 `Formats/Datatable/*`, `DatatableEditController`, `DatatableCsvSerializer`, `FormDatatableEditor`, `FormCsvImportPreviewDialog`, `ThemedDataGridView`; Phase 8 `Saving/ReloadAssetClassifier.cs`, `TreWriter.Repack`, save targets, `roundtrip-iff`/`roundtrip-tab` verbs — port sources (via CONTEXT canonical-refs).
- `.planning/phases/09-tjt-subpanel-datatable-editor-tab/09-RESEARCH.md` — the direct sibling RESEARCH template (structure/precedent).

### Tertiary (LOW confidence)
- Auto-memory entries (operator background): `project_swg_client_v2_reference`, `project_swg_iff_no_pad` (FIXED; not applicable — no IFF here), `feedback_winforms_dockfill_zorder`, `feedback_dotnet_build_msbuild_resources`, `project_vs2026_toolchain`, `project_self_hosted_ci`, `feedback_caller_attrs_binary_compat`, `project_gsd_worktrees_off`, `project_tre_version_support_gap` (V6000 encrypted → repack refuses), `project_scene_change_via_tjt` + `project_tjt_scene_change_naked_baseline` (reload-trigger path + "naked after scene change is baseline, not a regression").

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no external deps; all reuse from Phase 7/8/9 + BCL, locked by CONTEXT/UI-SPEC.
- Architecture: HIGH — decomposition + reuse map fully constrained by CF-01..CF-06 and the approved UI-SPEC.
- Format/byte-exactness: MEDIUM-HIGH — layout is documented + cross-checked against SOE source paths, but field widths (A2/A3), name-sort (A4), and real-file canonical ordering (A6) need a direct source/fixture read in Wave-0 before the writer is committed. Honestly flagged because byte-exactness is the SC4 gate.
- Pitfalls: HIGH — derived from the format facts + strong Phase 8/9 precedent + memory.
- Open behaviors (crc client-read A5, reload semantics A8, exact name rules A7): MEDIUM — resolvable by reading 2–3 SOE files in Wave-0.

**Research date:** 2026-05-30
**Valid until:** 2026-06-29 (stable in-repo domain; the only volatility is confirming A1–A8 against source, which the planner should do immediately in Wave-0 regardless of this date).

---

## VERIFIED Addendum — `StringTableDecoder.cs` read directly (2026-05-30)

> After the initial draft, I read the actual Phase 7 decoder source (`UtinniCoreDotNet/Formats/Decoders/StringTableDecoder.cs`, lines 76–167) in full. This resolves the format-layout assumptions A1/A2/A3 to **VERIFIED** and surfaces three model-design findings (A1b/A1c/A1d) the planner must act on. Where this addendum and the earlier "On-Disk Layout" section differ, **this addendum is authoritative.**

### Resolved (was assumed → now VERIFIED)
- **`magic` is a 4-byte LE u32**, not an 8-byte `long`. Decoder reads `cursor.ReadUInt32Le()`; `LooksLikeStf` checks the byte sequence `CD AB 00 00`. The C# constant is `const long StfMagic = 0xABCD` but the on-disk field is 32-bit. **Writer emits 4 bytes.** (Resolves A2.)
- **UTF-16 text has NO on-disk NUL terminator.** Decoder reads exactly `charCount * 2` bytes. (Resolves A3.) `charCount` is code units; text encoding is `Encoding.Unicode` (UTF-16LE).
- **Header is exactly:** `magic(u32) · version(byte) · nextUniqueId(u32) · count(u32)`. Version 0 and 1 accepted; any other rejected with `DecoderException(UnsupportedVersion)`. (Resolves A1.)
- **Decoder forged-count guards:** min string entry = 12 bytes (id+crc+charCount); rejects `count > Remaining/12`, rejects `charCount > Remaining/2`, rejects `nameLen > Remaining`. The writer/round-trip golden inherit this discipline; reuse `IffPayloadCursor` + `DecoderException`.

### NEW findings the planner MUST act on
- **A1b — the existing read model is LOSSY.** `StfEntry` exposes only `(Id, Name, Text)`; `StfTable` exposes `(Version, Entries)`. The decoder **reads but DISCARDS `nextUniqueId` and every entry's `sourceCrc`**, and decodes text with U+FFFD replacement (lossy for malformed input). Therefore the Phase 10 mutable model **cannot wrap `StfTable`** — it must do a **fuller parse pass** that retains `nextUniqueId`, per-entry `sourceCrc`, and (per A1d) per-entry original text bytes. CF-01's "reuse the proven parse path" means **reuse the `IffPayloadCursor` LE-read + bounds-check discipline**, not the lossy result type. (This is the single most important structural correction vs. the naive "wrap the decoder" reading.)
- **A1c — null/absent names and short/missing name blocks are real.** The decoder tolerates a string-only table (`if (cursor.Remaining < 8) break;`) and entries with no matching name (name = null via `TryGetValue`). A naive "every entry has a name" writer would NOT round-trip such files byte-exactly. The writer must emit only the names that exist (the name block can have fewer entries than the string block) and the round-trip golden needs a no-name-block fixture and a partial-names fixture.
- **A1d — malformed-text byte-faithfulness requires original text bytes.** Because the decoder replaces malformed UTF-16 with U+FFFD on read, re-encoding the decoded string yields **different bytes** than a file that originally contained malformed code units. This collides with STAB-03 (`Jo�o` must NOT be silently "repaired"). **The safest design: the mutable model holds each untouched entry's ORIGINAL text bytes and only re-encodes EDITED entries** (the D-02 per-entry-original-byte mechanism, scoped to the text field). The planner must choose this explicitly — it is the difference between SC4/STAB-03 passing and a subtle round-trip regression on malformed-text fixtures.

### Net effect on the model shape (recommendation, strengthened)
`StringTableEntry` should carry: `uint Id` (machine-managed), `string Name` (nullable key), `string Text` (decoded, for display/edit), `uint SourceCrc`, `byte[] OriginalTextBytes` (for untouched re-emit / STAB-03), and a per-entry dirty flag. `MutableStringTableDocument` carries `byte Version`, `uint NextUniqueId`, and the entry list. The writer: header → string block `OrderBy(Id)` (emit OriginalTextBytes for untouched entries, `Encoding.Unicode.GetBytes(Text)` for edited) → name block `OrderBy(Name, StringComparer.Ordinal)` over entries that HAVE a name. The `roundtrip-stf` golden fixtures must include: a canonical file, a `João` file, a malformed-text (U+FFFD) file, a no-name-block file, and a partial-names file.
