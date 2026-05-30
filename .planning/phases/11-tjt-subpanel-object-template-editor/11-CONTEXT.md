# Phase 11: TJT subpanel — Object Template Editor - Context

**Gathered:** 2026-05-30
**Status:** Ready for planning

<domain>
## Phase Boundary

A **typed view + edit surface for object-template `.iff` files** (the SWG `ObjectTemplate` hierarchy that drives in-world object behaviour and appearance), shipped as an `IEditorPlugin` WinForms SubPanel **inside The Jawa Toolbox** (per DEC-C4). This is the **final V1 subpanel** — at its success the "Demo + CI green" milestone closes and V1 ships.

**Object templates ARE IFF** (unlike Phase 10's flat `.stf`). Consequences that propagate through the whole phase:
- Phase 8's `MutableIffDocument` **hybrid-DOM byte-exact-on-untouched** mechanism **applies directly** (the Phase 9 datatable lineage, NOT the Phase 10 engineered-byte-exactness path).
- The IFF Editor "Switch to typed view" hand-off (Phase 9 D-10.3) **applies** — an object template legitimately opens in the IFF Editor, and the IFF Editor can hand off to this typed editor (because the format IS IFF).
- On-disk layout (per the existing Phase 7 `ObjectTemplateDecoder`): `FORM <type>` → optional `FORM DERV` (a chunk holding the **base-template name string**) → `FORM <version>` (digit-tagged) → a count chunk (`int32 paramCount`) + `paramCount` param chunks, each a **NUL-terminated field name + a self-describing, type-tagged value**.

**The two NEW capabilities this phase adds** (both explicitly deferred to Phase 11 by the Phase 7 decoder's own comments — "Phase 11 makes this editable and resolves the full inherited chain (D-13)"):
1. **Inheritance-chain resolution** — Phase 7 reads only THIS template's local params + the declared base NAME. Phase 11 walks the `DERV`/`@base` chain across TRE entries to materialize **inherited field values**.
2. **Editing** — Phase 7 renders param values as raw hex. Phase 11 decodes the self-describing typed values into editable widgets and writes edits back byte-exactly.

**In scope:**
- Mutable typed model + writer in `UtinniCoreDotNet/Formats/ObjectTemplate/` (sibling to `Formats/Iff/`, `Formats/Datatable/`, `Formats/StringTable/`), composing on Phase 8 IFF primitives (`MutableIffDocument`, `IffWriter`) and the existing read-only `Formats/Decoders/ObjectTemplateDecoder.cs`.
- **Inheritance resolution** (D-01): walk the `DERV` chain across TRE entries (reuse the Phase 7/8 TRE archive-index / `TrePayloadResolver` façade) to build an **effective merged view** — one row per field, effective resolved value, origin marker (local override vs inherited-from-`<base>`), ancestor breadcrumb.
- **Hybrid typed editing** (D-02): typed widgets for common scalar param types; raw-hex/bytes fallback (Phase 8 IFF leaf editing) for complex types — so **no param type is ever uneditable**.
- **Generic across all object-template types** (D-03): driven purely by the self-describing `.iff`; no `.tdf`/generated-loader schema port.
- **Override/revert + edit mutations** (D-04): edit a local override's value; promote an inherited field to a local override; revert a local override back to inherited.
- Editor-local undo/redo (Phase 8 D-08 / CF-04 pattern).
- Round-trip CLI verb + golden fixtures (`roundtrip-???`, CF-02 pattern) — automated byte-exactness / no-corruption gate.
- Save modes 1/2/4 from Phase 8 D-05 (loose override, Save/Save-As, `.tre` repack); mode 3 (in-memory live patch) stays DISABLED inherited (CF-03).
- Reload UX per CF-05 tier-(b) candor — **researcher confirms the object-template reload trigger** (respawn vs scene-change vs relog; `ObjectTemplateList` cache semantics).
- Entry points: file picker + TRE Browser "Open in Object Template Editor" hand-off + IFF Editor "Switch to typed object-template view" hand-off (the last applies because the format IS IFF — contrast Phase 10).

**Explicitly OUT of scope (deferred to V2):**
- **Adding an override for a field that exists NOWHERE in the inheritance chain** — needs the `.tdf`/generated-loader schema to know the field exists and its type. V1's generic, self-describing-`.iff` approach (D-03) can only promote/edit/revert fields that already appear somewhere in the chain.
- **Type-aware schema** (ported `Shared*ObjectTemplate.cpp` field/type/enum tables) — friendly enum dropdowns, type validation, and not-yet-present-field addition. V2.
- **Change-base / DERV re-parenting** (D-04) — rewriting the base-template reference. Risky (invalidates the resolved view, can orphan local overrides); deliberate V2 feature.
- **Full typed widgets for struct params / weighted-random lists / dynamic-variable lists** — V1 uses the hex fallback for these (D-02); promoting them to typed widgets is V2.
- **Creating a new object template from scratch** (empty-state designer) — V2, same boundary as Phase 9 D-01 / Phase 10.
- **In-memory live patch** (Phase 8 mode 3) — stays disabled inherited (CF-03).
- **Cross-reference / "find usages"** of a template across world snapshots / datatables — V2.

</domain>

<decisions>
## Implementation Decisions

### Carried forward (locked — no re-decision; the mature Phase 8/9/10 CF-* lineage)
- **CF-01 (← 08 D-01 / 09 CF-01 / 10 CF-01):** Format primitives (typed mutable `ObjectTemplate` model + writer) ship **framework-side** in `UtinniCoreDotNet/Formats/ObjectTemplate/`, sibling to the other `Formats/<Type>/` folders. **NOT** in `TheJawaToolboxDotNet`. TJT consumes via the existing `UtinniCoreDotNet.dll` reference. The existing read-only `Formats/Decoders/ObjectTemplateDecoder.cs` (Phase 7) is the parse foundation — Phase 11 adds the mutable model + writer (recommend reuse the proven parse path, add a parallel mutable type). **Object template is IFF**, so the model composes on Phase 8's `MutableIffDocument` hybrid-DOM (the byte-exact-on-untouched mechanism comes free — this is the Phase 9 path, NOT Phase 10's engineered ordering).
- **CF-02 (← 08 D-02 / 09 CF-02 / 10 CF-02):** Round-trip CLI verb + golden fixtures is the automated correctness gate: parse → mutate → serialize → re-parse, assert **byte-exact identity for untouched params**. Placeholder verb name — planner picks per Phase 4/8/9/10 conventions (`roundtrip-iff` already exists and may subsume object templates since they ARE IFF — planner decides whether a dedicated verb adds value).
- **CF-03 (← 08 D-05 / 09 CF-03 / 10 CF-03):** Save modes 1, 2, 4 are V1 (loose override, Save / Save-As, `.tre` repack — refuses V6000 archives per Phase 8 WR-06). **Mode 3 (in-memory live patch) stays DISABLED** behind the honest inherited tooltip.
- **CF-04 (← 08 D-08 / 09 CF-06 / 10 CF-04):** **Editor-local undo/redo stack**, independent of Utinni's scene `UndoRedoManager`. Honors the Phase 11 preservation guard-rail (CON-M-05: object-template edits must not entangle the scene-cleanup UndoRedoManager).
- **CF-05 (← 08 D-06 tier (b) / 09 CF-05 / 10 CF-05):** **Reload UX locked to honest tier-(b) candor.** The editor tells the user how/when the asset re-resolves and does NOT fabricate a trigger. **Researcher MUST confirm** the actual object-template reload semantics (does an edited template re-resolve on respawn? on TJT-driven scene change? only on relog? — `ObjectTemplateList` is a cached singleton). Whether `.iff` object-template extensions are already classified in `ReloadAssetClassifier` must be checked; if not, classify honestly. The badge wording follows the confirmed reality — planner may NOT loosen it. This is the SC3 demo gate.
- **CF-06 (← DEC-C4 LOCKED):** Subpanel-inside-TJT (`IEditorPlugin` SubPanel registered in `TheJawaToolboxDotNet/Plugin.cs` `SubPanelContainer`, alongside Phase 7 TRE Browser + Phase 8 IFF Editor + Phase 9 Datatable Editor + Phase 10 String-table Editor). Not a separate plugin. The fifth and final V1 SubPanel.

### Inheritance resolution
- **D-01:** **Effective merged view with origin markers.** Show ONE row per field with its **effective (resolved) value** and an origin marker: **"local override"** vs **"inherited from `<base>`"**. The full ancestor chain (e.g. `shared_base_tangible → … → this`) is shown as a breadcrumb/header. Un-overridden inherited fields appear (greyed/italic) and are viewable; editing one **promotes it to a local override** (see D-04). This is the SOE object-template-editor mental model and is the most legible surface for editing — directly satisfies SC2 ("view inherited fields, edit overrideable fields").
  - **Base sourcing:** walk the `DERV`/`@base` reference recursively, loading each base template from the TRE archive set via the Phase 7/8 TRE archive-index / `TrePayloadResolver` façade. (Phase 7 deliberately did NOT touch this façade — Phase 11 is where it gets wired into object-template resolution.)
  - **Graceful degradation (planner edge-case, locked behaviour):** when a base template can't be resolved from the loaded archives, **never block the open** — show this template's local fields, render inherited rows as **"unresolved base `<name>`"**, and let the user still edit local params. A missing ancestor must degrade, not throw.

### Typed value editing
- **D-02:** **Hybrid: typed widgets for scalars + raw-hex fallback for complex types.** SWG param values are **self-describing on disk** (a data-type tag + value, including the single-value/weighted-list/range/delta wrappers), so typed decode needs NO external schema.
  - **Typed (V1):** bool checkbox, int/float spinners (with the single-value / range / delta wrapper surfaced), string textbox, stringId, template-reference (picker or textbox), enum (value shown; friendly names are V2 per D-03).
  - **Hex/bytes fallback (V1):** for param types not yet modeled — struct params, weighted/random lists, dynamic-variable lists — fall back to the Phase 8 IFF hex/text leaf editor. **Guarantee: no param type is ever uneditable.**
  - The `@derived`/`loaded` per-param flags and any wrapper framing are **maintained by the writer**, not hand-edited (see D-04 machine-managed boundary).

### Type coverage & schema
- **D-03:** **Generic across ALL object-template types — no schema port.** One generic param editor driven purely by the self-describing `.iff` (inline field names + type-tagged values). Covers viewing inherited fields and editing/overriding any field that **already exists somewhere in the chain**, for **every** template type uniformly (tangible, creature, weapon, building, ship, …). 
  - **No `.tdf`/`Shared*ObjectTemplate.cpp` schema porting in V1.** (The `.tdf` schema SOURCE is not in the swg-client-v2 corpus anyway; the generated loaders ARE present but porting them is V2 work.)
  - **Consequence (bounds D-04):** "add an override for a field that exists NOWHERE in the chain" is **inherently V2** — it requires schema to know the field exists and its type. V1 can promote/edit/revert only fields already present somewhere in the resolved chain.

### Editing scope (T-level)
- **D-04:** **Override / revert / edit — three mutations, V1.** Founder decision, consistent with Phase 9/10's "ship meaningful editing scope" T-level pattern.
  1. **Edit** a local override's value (the floor).
  2. **Add override** — promote an inherited field to a local override and set its value (writes a new local param chunk).
  3. **Remove override** — revert a locally-overridden field back to inherited (deletes the local param chunk; the effective view then shows the inherited value).
  - **Machine-managed (NOT user-editable):** the version-form param-count chunk and the per-param `@derived`/loaded flags + wrapper framing are maintained by the writer — mirrors Phase 10's machine-managed `id`. Users edit field VALUES and override membership, never the structural bookkeeping.
  - **NOT V1:** change-base / `DERV` re-parenting (deferred — invalidates the resolved view, can orphan local overrides); adding a field absent from the entire chain (needs schema, per D-03).

### Claude's Discretion (planner / `/gsd-ui-phase 11`)
- **Editor surface / columns** — exact column layout (field name / effective value / origin / type), how origin is rendered (icon, colour, italic), how "promote to override" / "revert to inherited" are triggered (context menu, toggle, button), how the ancestor breadcrumb is presented, dirty-state indicator placement, and how the hex-fallback sub-editor is surfaced for complex types. **Locked floor:** inherited fields are viewable with visible origin; field values are editable; override/revert operations exist; structural bookkeeping is machine-managed.
- **`ObjectTemplate` mutable model ↔ existing `ObjectTemplateDecoder` relationship** — wrap/reuse vs supersede; recommend reuse the proven parse path and add a parallel mutable type composing on `MutableIffDocument`.
- **CLI verb naming/shape** — whether the existing `roundtrip-iff` golden harness already covers object templates (they ARE IFF) or a dedicated verb adds value; planner picks per Phase 4/8/9/10 conventions.
- **Reload trigger wording (CF-05)** — exact badge copy follows the researcher's confirmation of `ObjectTemplateList` reload semantics.
- **Plan decomposition** — likely **4–6 plans**, e.g.: (1) mutable typed `ObjectTemplate` model + writer (framework) composing on `MutableIffDocument`, with the self-describing typed-value decode/encode + hex fallback; (2) inheritance-chain resolver (TRE-walked effective view + origin) + round-trip CLI golden; (3) editor SubPanel host + effective-view grid + origin markers + entry points (file picker, TRE Browser, IFF Editor hand-off) + undo/redo; (4) override/revert/edit mutations + typed widgets + hex-fallback sub-editor; (5) Save▾ modes 1/2/4 + reload badge; (6) live-SWG smoke + UAT + **V1 release-gate verification** (all 5 subpanels demo, CI green, tag V1). Planner has final say.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 11 roadmap / project decisions
- `.planning/ROADMAP.md` § Phase 11 — goal, success criteria (1: subpanel loads in TJT against a live SWG client; 2: open a template, view inherited fields, edit overrideable fields, save back; 3: live client reflects the edit on respawn/reload; **4: V1 release gate — all 15 critical bugs closed, Tier 1 + Tier 2 CI green on `main`, all five Wave-1 subpanels demo end-to-end inside TJT, then tag V1**), preservation guard-rails (CON-M-01/02 SPI, CON-T-05 `*Impl` separation, **CON-M-05 UndoRedoManager-on-scene-cleanup** — object templates can affect live-scene objects, so the editor-local undo stack must stay disentangled from the scene manager). "Open questions to resolve: None."
- `.planning/PROJECT.md` § Key Decisions — **DEC-C4** (subpanel-inside-TJT, LOCKED), **DEC-A3** (not-a-DCC-replacement, LOCKED — object templates are behaviour/appearance DATA references, not mesh/texture authoring, so no anti-goal overlap), § Current Milestone (V1 "Demo + CI green" — this phase closes it).
- `.planning/REQUIREMENTS.md` — **PROD-W1-OT** ("Edit object templates — the `.iff`-based template hierarchy that drives in-world object behaviour and appearance. Plugin loads in editor host; user can open an object template, view inherited fields, edit overrideable fields, save back; live SWG client reflects the edit when the object respawns or reloads.") and **PROD-02** (Wave-1 edit aggregate — this is the fourth/final editor that closes it).

### Phase 7/8/9/10 carried-forward decisions (CF-* above)
- `.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-CONTEXT.md` — origin of the read-only `ObjectTemplateDecoder` (the D-13 "Phase 11 makes this editable + resolves the chain" deferral lives in that decoder's comments) and the TRE archive-index / payload-resolver façade Phase 11 wires into inheritance resolution. D-02 swg-client-v2 reference policy.
- `.planning/phases/08-tjt-subpanel-iff-editor-read-write/08-CONTEXT.md` — **READ FIRST for the IFF mechanics.** D-05 (4 save modes; mode-3 disabled), D-06 (tiered reload), D-08 (editor-local undo/redo), and the `MutableIffDocument` hybrid-DOM byte-exactness + `IffWriter` the object-template model composes on. The IFF-Editor "Switch to typed view" hand-off (Phase 9 D-10.3) applies here because object templates ARE IFF.
- `.planning/phases/09-tjt-subpanel-datatable-editor-tab/09-CONTEXT.md` — closest **per-type typed-widget** precedent (D-02 hybrid editing mirrors the datatable per-type cell widgets) and the SubPanel host pattern, dirty-state, Save▾ wiring, TRE-Browser hand-off, round-trip golden harness.
- `.planning/phases/10-tjt-subpanel-string-table-editor-stf/10-CONTEXT.md` — the immediately-prior sibling; the mature CF-01..CF-06 lineage Phase 11 inherits verbatim. **Note the contrast:** `.stf` is flat (not IFF) so Phase 10 engineered byte-exactness and had NO IFF-Editor hand-off; Phase 11 is IFF so it gets both for free from Phase 8.

### Existing Utinni assets to extend / reuse (this repo)
- `UtinniCoreDotNet/Formats/Decoders/ObjectTemplateDecoder.cs` — **READ FIRST.** The existing read-only object-template decoder (Phase 7). Documents the on-disk layout (`FORM <type>` → `DERV` base-name → digit-tagged version form → count chunk + NUL-named, type-tagged param chunks), the bounded posture (local-only, no inheritance walk, values as hex), `LooksLikeObjectTemplate` sniff, and the `ObjectTemplateView` / `ObjectTemplateField` (with its `InheritedFrom` slot already designed for "local" vs base). Phase 11's mutable model + resolver build directly on this.
- `UtinniCoreDotNet/Formats/Iff/*` (Phase 8) — `MutableIffDocument` (hybrid DOM, byte-exact-on-untouched), `IffWriter` (byte-exact serializer), `IffDocument`, `IffContainerChunk`/`IffLeafChunk`, `IffPayloadCursor`. The object-template mutable model composes on these.
- TRE archive-index / `TrePayloadResolver` façade (Phase 7/8 `Formats/Tre/*`) — the cross-IFF base-template resolution path for D-01 inheritance walking. `TreFile.GetRecord*` APIs (Phase 8).
- `UtinniCoreDotNet/Saving/*` — `ReloadAssetClassifier` (check/add object-template `.iff` classification for CF-05), `LooseOverridePath`, `TreRepackSaveTarget`/`TreWriter.Repack`, the Phase 8/9/10 save-target infrastructure (CF-03 modes 1/2/4).
- `UtinniCoreDotNet/Formats/Datatable/*` (Phase 9) — structural template for `Formats/ObjectTemplate/` (mutable typed model + writer + per-type widgets); NOT shared code, a parallel analog.
- `Utinni.Cli/Commands/DecodeIffCommand.cs` (dispatches the object-template decoder today), `roundtrip-iff`/`roundtrip-tab`/`roundtrip-stf` verbs, `Utinni.Cli.Tests` golden fixtures — the CF-02 harness pattern.
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormDatatableEditor*` + `FormStringTableEditor*` — closest SubPanel precedents (toolbar + themed grid + dirty-state + Save▾ + entry-point hand-off + singleton-form hide-not-dispose policy).
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (+ `.Designer.cs`) — the TRE Browser the "Open in Object Template Editor" hand-off originates from; mirror the Phase 8/9/10 hand-off wiring (extension and/or `LooksLikeObjectTemplate` sniff).
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` — the IFF Editor; the "Switch to typed object-template view" hand-off lives here (object templates ARE IFF; mirror Phase 9 D-10.3's DTII switch).
- `UtinniCoreDotNet/UI/Controls/SubPanel.cs`, `SubPanelContainer.cs`, `ThemedDataGridView` (Phase 9) — host surface + dark theme.
- `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs` — MEF SPI (CON-M-01/02). `TheJawaToolboxDotNet/Plugin.cs` — `SubPanelContainer` registration site. `TheJawaToolboxDotNet.csproj` — `UtinniCoreDotNet.dll` HintPath (the CF-01 consumption path).

### swg-client-v2 reference (read-only spec/impl, NOT a runtime dep — per memory `project_swg_client_v2_reference.md`)
- `../swg-client-v2/src/engine/shared/library/sharedObject/src/shared/object/ObjectTemplate.{h,cpp}` — **AUTHORITATIVE FORMAT.** The base `ObjectTemplate` load path: the per-param self-describing type-tagged value encoding, the `@base`/`m_baseData` derivation + un-overridden-field fallback (the D-01 inheritance mechanism), and the param wrapper semantics. **Format/layout only — no code/identifier/comment copying** (the Phase 7 decoder's MIT-original posture).
- `../swg-client-v2/src/engine/shared/library/sharedGame/src/shared/objectTemplate/Shared*ObjectTemplate.cpp` — the generated per-type loaders (Tangible, Creature, Weapon, Building, Ship, Static, …). They encode field names + types per template type. **NOT ported in V1 (D-03 generic approach); reference only** for cross-checking that the generic self-describing decode matches the real per-type field reads, and as the V2 type-aware-schema source.
- `../swg-client-v2/src/engine/shared/library/sharedTemplateDefinition/.../TemplateData.{h,cpp}`, `TpfFile.h` — the `.tdf`/`.tpf` template-definition machinery (`ParamType`/`ListType` enums, struct/enum/list grammar). **V2 schema reference** — not consumed in V1. (`.tdf` SOURCE files are NOT present in the corpus; only the generated loaders are.)
- **`ObjectTemplateList`** (`../swg-client-v2/.../sharedObject/.../ObjectTemplateList.{h,cpp}`) — the runtime template cache. **Researcher: confirm CF-05** — whether an edited template re-resolves on respawn / scene change / relog (drives the honest reload-badge wording and SC3).

### Codebase intel (this repo)
- `.planning/codebase/STACK.md`, `STRUCTURE.md`, `ARCHITECTURE.md`, `CONVENTIONS.md`, `CONCERNS.md`, `INTEGRATIONS.md`, `TESTING.md` — repo-wide maps. CONCERNS/CONVENTIONS flag CON-M-01/02 (MEF SPI), **CON-M-05 (UndoRedoManager on scene cleanup — Phase 11 steers clear per CF-04, and the preservation guard-rail calls this out explicitly since object templates touch live-scene objects)**, CON-T-05 (`*Impl` separation).

### Relevant memory (operator background; informs planner choices, not direct agent input)
- `feedback_caller_attrs_binary_compat.md` — adding NEW types to `UtinniCoreDotNet/Formats/ObjectTemplate/` is safe; do NOT change existing public `Formats/*` signatures consumed by pre-built plugins without rebuilding in the same commit.
- `feedback_winforms_dockfill_zorder.md` — the grid docks Fill and stays front-most (Phase 9 CF-09 carries over).
- `project_swg_client_v2_reference.md` — locked reference-corpus policy (read-only, no runtime dep, no code/identifier copying; layout-study only — the Phase 7 decoder's posture).
- `project_scene_change_via_tjt.md` + `project_tjt_scene_change_naked_baseline.md` — TJT chat-command-parser scene change is a likely reload trigger users run after an object-template save; "naked after scene change" is a pre-existing baseline, NOT a Phase 11 regression signal.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`ObjectTemplateDecoder.cs` (Phase 7, read-only):** the proven object-template parse path. Its `ObjectTemplateView`/`ObjectTemplateField` types already carry an `InheritedFrom` slot ("local" vs base name) — Phase 11's effective-view model extends exactly this shape. `LooksLikeObjectTemplate` reused for the TRE-Browser hand-off detection.
- **Phase 8 `MutableIffDocument` + `IffWriter`:** the hybrid-DOM byte-exact-on-untouched mechanism. Because object templates ARE IFF, Phase 11 gets byte-exactness for free (the Phase 9 datatable path), NO Phase-10-style engineered ordering needed.
- **Phase 7/8 TRE archive-index / `TrePayloadResolver`:** the cross-IFF resolution façade Phase 7 deliberately left untouched; Phase 11 wires it into the D-01 base-template chain walk.
- **Phase 9 `Formats/Datatable/` per-type cell widgets + `FormDatatableEditor`:** structural template for the D-02 typed-value widgets and the editor host.
- **Phase 8/9/10 round-trip CLI verb + `Utinni.Cli.Tests` goldens:** the CF-02 harness (and `roundtrip-iff` may already cover object templates since they ARE IFF).
- **`ReloadAssetClassifier` + Phase 8/9/10 save targets (modes 1/2/4):** CF-03/CF-05 infrastructure consumed directly.
- **`SubPanel`/`SubPanelContainer` + Utinni themed controls + singleton-form hide-not-dispose policy (Phase 8 08-05):** WinForms host + dark theme + the MEF-registered-form lifecycle pattern Phases 9-11 must apply from the start.

### Established Patterns
- `IEditorPlugin` MEF export → aggregation in TJT `Plugin.cs` `SubPanelContainer` (CON-M-01/02); `*Impl` separation (CON-T-05).
- **IFF hybrid-DOM byte-exactness** (Phase 8/9): parse → mutate via `MutableIffDocument` → `IffWriter.Write` → untouched chunks byte-identical. The CF-02 golden gate asserts this for untouched params.
- **Per-type typed cell widgets with a structural fallback** (Phase 9 → D-02 here): typed editors for scalars, hex/text fallback for complex types.
- **Effective-value-with-origin inheritance view** (NEW, D-01): merge the `DERV` chain into one row-per-field surface with local-vs-inherited origin markers; graceful degradation on unresolved base.
- **Editor-local undo/redo disentangled from the scene `UndoRedoManager`** (CF-04 / CON-M-05) — extra-load-bearing here because object templates affect live-scene objects.
- **Honest tiered reload-status badge** (CF-05) — wording follows researcher-confirmed `ObjectTemplateList` reload semantics.
- **Binary-compat caution:** add new `Formats/ObjectTemplate/` types only; don't change existing public `Formats/*` signatures without rebuilding plugins in the same commit.

### Integration Points
- New Object Template Editor SubPanel registers via TJT `Plugin.cs` `SubPanelContainer` (the fifth and final V1 SubPanel, alongside TRE Browser + IFF Editor + Datatable Editor + String-table Editor).
- TRE Browser "Open in Object Template Editor" hand-off from `FormTreBrowser` selection (extension and/or `LooksLikeObjectTemplate` sniff).
- IFF Editor "Switch to typed object-template view" hand-off from `FormIffEditor` (visible IFF root is an object-template `FORM <type>` — mirrors Phase 9 D-10.3's DTII switch; applies because the format IS IFF).
- Base-template resolution reads through the Phase 7/8 TRE archive-index / payload-resolver façade.
- Loose-override + `.tre` repack save targets reuse the Phase 8 paths (CF-03 modes 1/4; refuses V6000 archives).
- Reload badge (CF-05) interacts with the TJT-driven scene-change / respawn path; the editor states how the asset re-resolves, does NOT trigger one.
- CLI round-trip golden adds to `Utinni.Cli` alongside `inspect-iff` / `decode-iff` / `roundtrip-iff` / `roundtrip-tab` / `roundtrip-stf`.

</code_context>

<specifics>
## Specific Ideas

- This is the **milestone-closing phase.** Success Criterion 4 is not a feature — it's the **V1 release gate**: all 15 critical bugs closed, Tier 1 + Tier 2 CI green on `main`, and **all five Wave-1 subpanels** (TRE Browser, IFF Editor, Datatable Editor, String-table Editor, Object Template Editor) demoing end-to-end inside TJT against a live SWG client. The final plan must include that aggregate verification and the V1 tag.
- The **mental model** is the SOE object-template editor: a property grid where you see every field's effective value, see whether it's locally set or inherited, and override/revert at will. The inheritance-with-origin view (D-01) is the heart of the phase — it's what makes this an *object-template* editor rather than just "the IFF editor pointed at a template."
- **Two format facts the planner/researcher must not miss:** (1) object-template param values are **self-describing** (type tag + value on disk), which is WHY the generic, no-schema approach (D-03) works and why typed decode needs no `.tdf`; (2) the inheritance fallback is a **client-runtime behaviour** (`m_baseData` → un-overridden fields return `base->getXxx()`), so the editor's resolved view must replicate that semantics, not invent its own.
- **The contrast with Phase 10 is instructive:** `.stf` was flat (engineered byte-exactness, no IFF-Editor hand-off); object templates are IFF (free byte-exactness via `MutableIffDocument`, IFF-Editor hand-off applies). Phase 11 leans on the **Phase 8/9 IFF lineage**, not the Phase 10 flat-format lineage.

</specifics>

<deferred>
## Deferred Ideas

- **Type-aware schema** (port `Shared*ObjectTemplate.cpp` / `.tdf` field/type/enum tables) — enables adding overrides for fields NOT present anywhere in the chain, friendly enum dropdowns, and type validation. The single biggest V2 enrichment of this editor.
- **Change-base / `DERV` re-parenting** — rewriting a template's base reference. Deferred (invalidates the resolved view, can orphan local overrides); a deliberate V2 feature with its own UX.
- **Full typed widgets for struct params / weighted-random lists / dynamic-variable lists** — V1 uses the hex fallback (D-02); promoting these to typed editors is V2.
- **Creating a new object template from scratch** (empty-state designer) — V2, same boundary as Phase 9 D-01 / Phase 10.
- **In-memory live patch for object templates** (Phase 8 mode 3) — stays disabled inherited (CF-03). A follow-up enabler phase.
- **Cross-reference / "find usages"** — surfacing which world snapshots / datatables / other templates reference an edited template; dangling-reference warnings. V2.
- **Shared "abstract editor base class"** across IFF / Datatable / String-table / Object-template editors — now the FIFTH SubPanel with shared toolbar/dirty-state/save-flow/hand-off patterns. The strongest candidate yet for a post-Wave-1 refactor (noted since Phase 9).

### Reviewed Todos (not folded)
- `phase10-stringtable-sc3-live-reload-residual.md` — Phase 10's tracked SC3 live-reload residual (the deferred live Step-7 scene-change/relog observation + stale-CRC check). **Belongs to Phase 10, not Phase 11** — but directly relevant background: Phase 11's CF-05 reload-trigger confirmation should leverage the same live-reload investigation, and resolving the Phase 10 residual may co-occur with the Phase 11 live smoke. Noted so the planner is aware, NOT absorbed into Phase 11 scope.
- `gamecallbacks-gc-av-flake-fix.md` — CI-stability flake-fix in `GameCallbacksTests`; resolved in 06-04. Keyword false-positive (same as Phases 7/8/9/10). Unrelated to Object Template Editor scope.
- `loader-lock-harness-flake-fix.md` — CI-stability flake-fix; resolved in 06-04 (memory `project_loader_lock_harness_ci_flake.md`). Keyword false-positive. Unrelated.
- `phase09-datatable-editor-review-warnings.md` — Phase 9 code-review Warnings/Info follow-ups. Belongs to Phase 9's outstanding-review backlog, not Phase 11. Noted so the planner does not absorb it.

</deferred>

---

*Phase: 11-tjt-subpanel-object-template-editor*
*Context gathered: 2026-05-30*
