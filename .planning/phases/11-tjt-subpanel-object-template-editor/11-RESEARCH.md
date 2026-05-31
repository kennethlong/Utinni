# Phase 11: TJT subpanel — Object Template Editor - Research

**Researched:** 2026-05-30
**Domain:** SWG object-template (`.iff`) typed/inheritance editor; WinForms `IEditorPlugin` SubPanel inside The Jawa Toolbox; closes the V1 "Demo + CI green" milestone.
**Confidence:** HIGH (format/inheritance/cache semantics verified against swg-client-v2 source; reuse paths verified in-repo)

## Summary

Phase 11 is the **fifth and final V1 SubPanel**, and the CONTEXT.md is exhaustive: every architecture decision (CF-01..CF-06, D-01..D-04) is locked. This research is therefore narrow and confirmatory — its primary deliverable is to resolve the three "Researcher MUST confirm" open items by reading the swg-client-v2 reference source, and to map each locked decision onto a concrete, already-proven Utinni reuse path. There is **no new mechanism to invent**: object templates ARE IFF, so byte-exactness comes free from Phase 8's `MutableIffDocument`/`IffWriter` (the Phase 9 datatable lineage, NOT Phase 10's engineered ordering); inheritance resolution rides the existing `Formats/Tre/TrePayloadResolver` façade; the editor host clones `FormDatatableEditor`'s shape; the controller clones `DatatableEditController`'s editor-local undo.

**All three MUST-confirm items are now resolved with HIGH confidence from primary source:**
1. **CF-05 reload semantics** — the `ObjectTemplateList` cache (`DataResourceList<ObjectTemplate>`) is keyed by filename-CRC and **never re-reads a cached template from disk**; a cached instance is only evicted when its refcount hits 0 (`release()`), and bases are refcounted and held by every derived template. An edited object-template `.iff` therefore does **NOT** reliably re-resolve on respawn (cached instance is reused) — only a full **relog/client restart** guarantees re-resolution; a TJT scene-change *may* evict templates whose objects are destroyed but bases shared across scenes persist. `ReloadAssetClassifier` **already** classifies object-template `.iff` (TypeIds `SHOT`/`STOT`/`SBOT`) → `PendingNextSceneChange`. The honest badge is tier-(b) "Pending next scene change (may require relog)".
2. **Self-describing param encoding** — confirmed: each param value is `int8 dataTypeTag` (`NONE=0/SINGLE=1/WEIGHTED_LIST=2/RANGE=3/DIE_ROLL=4`) optionally preceded/followed by per-type framing, plus a `delta-type` byte (`' '`/`'+'`/`'-'`) on numeric params. No `.tdf` schema needed to decode scalars.
3. **`@base`/`m_baseData` fallback** — confirmed: the per-field accessor pattern is literally `if (!m_param.isLoaded()) return base->getXxx();`. A field is "loaded" (a local override) only when its param chunk is physically present in the version form. Un-overridden fields fall through `m_baseData` recursively. The editor's effective-merged view (D-01) replicates this exactly.

**Primary recommendation:** Build `UtinniCoreDotNet/Formats/ObjectTemplate/` (mutable typed model + resolver + writer) composing on `MutableIffDocument`, reusing the Phase 7 `ObjectTemplateDecoder` parse path; clone `DatatableEditController` for editor-local undo; clone `FormDatatableEditor` for the host; extend `roundtrip-iff` rather than adding a dedicated CF-02 verb (object templates ARE IFF — see Validation Architecture); badge the reload tier exactly as `ReloadAssetClassifier` already returns it.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Object-template parse (read) | Framework (`UtinniCoreDotNet`) | — | CF-01: format primitives ship framework-side, sibling to other `Formats/<Type>/`. Reuses Phase 7 `ObjectTemplateDecoder`. |
| Self-describing typed value decode/encode | Framework | — | D-02: typed model in `Formats/ObjectTemplate/`; composes on Phase 8 `IffPayloadCursor`/`MutableIffDocument`. |
| Inheritance-chain resolution (DERV walk) | Framework | TRE archive index | D-01: walks `TrePayloadResolver` (`Formats/Tre/`) to fetch each base `.iff`; pure-managed effective-merge logic. |
| Byte-exact write | Framework | — | `IffWriter` over `MutableIffDocument` hybrid-DOM. Untouched param chunks re-emit verbatim. |
| Override/revert/edit mutations + undo | Framework (controller) | — | CF-04: editor-local undo stack in `UtinniCoreDotNet/Editing/`, NOT scene `UndoRedoManager` (CON-M-05). |
| Editor SubPanel UI (grid, widgets, hex fallback) | TJT plugin (`TheJawaToolboxDotNet`) | — | CF-06: WinForms `IEditorPlugin` SubPanel; consumes framework via `UtinniCoreDotNet.dll`. |
| Save modes 1/2/4 | TJT plugin | Framework save targets | CF-03: reuses Phase 8 `IffSaveTargets`/`TreRepackSaveTarget`/`LooseOverridePath`. |
| Reload badge / dispatch | TJT plugin | `ReloadAssetClassifier` (framework) | CF-05: classifier already routes object-template `.iff` → `PendingNextSceneChange`. |
| Round-trip golden gate | CLI (`Utinni.Cli`) + tests | Framework | CF-02: extend `roundtrip-iff`; goldens in `Utinni.Cli.Tests`. |
| Live re-resolution (respawn/scene/relog) | SWG client (`ObjectTemplateList` cache) | — | Out of Utinni's control; editor only observes/states, never triggers (CF-05 tier-b). |

## Project Constraints (from CLAUDE.md)

No `./CLAUDE.md` exists in the working directory (verified — `Read` returned "File does not exist"). Project conventions instead come from `.planning/codebase/CONVENTIONS.md`/`CONCERNS.md` and the auto-memory. The load-bearing constraints for this phase:

- **CON-M-01/02 (MEF SPI):** the `IEditorPlugin` export contract is preserved; register the new SubPanel in TJT `Plugin.cs` `SubPanelContainer` inside try/catch (Phase 8/9/10 precedent). Do not change the SPI signature.
- **CON-T-05 (`*Impl` separation):** maintain the PIMPL/`*Impl` separation where it already exists; new managed types do not breach it.
- **CON-M-05 (UndoRedoManager on scene cleanup):** **extra-load-bearing here** — object templates affect live-scene objects, so the editor-local undo stack (CF-04) must stay completely disentangled from the scene `UndoRedoManager`. Clone `DatatableEditController`'s independent stack; never touch `UndoRedoManager`.
- **Binary-compat (`feedback_caller_attrs_binary_compat.md`):** adding NEW types to `Formats/ObjectTemplate/` is safe; do NOT change existing public `Formats/*` signatures consumed by pre-built plugins without rebuilding every cross-binary plugin in the same commit.
- **`UtinniCore.cs` regen churn (`project_utinnicore_cs_regen_churn.md`):** if a build regenerates `Generated/UtinniCore.cs`, `git checkout --` it; never commit the symmetric no-op diff.
- **Worktrees OFF (`project_gsd_worktrees_off.md`):** run C++/MSBuild waves INLINE on the main tree. Build with VS2026 MSBuild, run xUnit via `dotnet test --no-build` (`feedback_dotnet_build_msbuild_resources.md`).
- **WinForms Dock.Fill front-most (`feedback_winforms_dockfill_zorder.md`):** the grid docks Fill and stays front-most (Phase 9 CF-09 carries over).
- **Reference policy (`project_swg_client_v2_reference.md`):** swg-client-v2 is read-only layout-study only — NO code/identifier/comment/test-fixture copying. The Phase 7 decoder's MIT-original posture (see its header comment) is the template for every new file's provenance comment.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PROD-W1-OT | Edit object templates (the `.iff`-based template hierarchy that drives in-world object behaviour and appearance). Plugin loads in editor host; user can open an object template, view inherited fields, edit overrideable fields, save back; live SWG client reflects the edit when the object respawns or reloads. | Inheritance-merge view (D-01, this doc §Inheritance Resolution) satisfies "view inherited fields"; typed/hex hybrid editing (D-02) + override/revert/edit (D-04) satisfies "edit overrideable fields, save back"; byte-exact write via `MutableIffDocument`/`IffWriter` + save modes 1/2/4 satisfies "save back"; CF-05 reload-tier honesty (this doc §CF-05) qualifies "reflects … when the object respawns or reloads" — the honest answer is **relog-reliable, respawn-NOT-reliable** (cache semantics), so the acceptance demo (SC3) must be read against tier-(b) candor. |
| PROD-02 (aggregate) | Wave-1 edit aggregate — Object Template Editor is the fourth/final editor that closes it. | This phase's SC4 is the **V1 release gate**: all 15 critical bugs closed, Tier 1 + Tier 2 CI green on `main`, and all five Wave-1 subpanels (TRE Browser, IFF Editor, Datatable Editor, String-table Editor, Object Template Editor) demoing end-to-end inside TJT against a live client, then tag V1. The final plan MUST include this aggregate verification + the V1 tag. |
</phase_requirements>

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Carried-forward CF lineage (locked — no re-decision):**
- **CF-01:** Format primitives (typed mutable `ObjectTemplate` model + writer) ship framework-side in `UtinniCoreDotNet/Formats/ObjectTemplate/`, sibling to the other `Formats/<Type>/` folders. NOT in `TheJawaToolboxDotNet`. The existing read-only `Formats/Decoders/ObjectTemplateDecoder.cs` (Phase 7) is the parse foundation — add a parallel mutable type composing on Phase 8's `MutableIffDocument` hybrid-DOM (byte-exact-on-untouched, the Phase 9 path NOT Phase 10's engineered ordering).
- **CF-02:** Round-trip CLI verb + golden fixtures is the automated correctness gate: parse → mutate → serialize → re-parse, assert byte-exact identity for untouched params. `roundtrip-iff` already exists and may subsume object templates (they ARE IFF) — planner decides whether a dedicated verb adds value.
- **CF-03:** Save modes 1, 2, 4 are V1 (loose override, Save/Save-As, `.tre` repack — refuses V6000 archives per Phase 8 WR-06). Mode 3 (in-memory live patch) stays DISABLED behind the honest inherited tooltip.
- **CF-04:** Editor-local undo/redo stack, independent of Utinni's scene `UndoRedoManager` (honors CON-M-05).
- **CF-05:** Reload UX locked to honest tier-(b) candor. The editor tells the user how/when the asset re-resolves and does NOT fabricate a trigger. Badge wording follows researcher-confirmed reality — planner may NOT loosen it. This is the SC3 demo gate.
- **CF-06:** Subpanel-inside-TJT (`IEditorPlugin` SubPanel registered in `TheJawaToolboxDotNet/Plugin.cs` `SubPanelContainer`). The fifth and final V1 SubPanel.

**New decisions:**
- **D-01:** Effective merged view with origin markers. ONE row per field with effective (resolved) value + origin marker ("local override" vs "inherited from `<base>`"). Full ancestor chain shown as breadcrumb. Un-overridden inherited fields appear (greyed/italic), viewable; editing one promotes it to a local override. Base sourcing: walk `DERV`/`@base` recursively via the Phase 7/8 TRE archive-index / `TrePayloadResolver` façade. Graceful degradation (LOCKED behaviour): when a base can't be resolved, NEVER block the open — show local fields, render inherited rows as "unresolved base `<name>`", still allow editing local params. A missing ancestor degrades, not throws.
- **D-02:** Hybrid — typed widgets for scalars + raw-hex fallback for complex types. Typed (V1): bool checkbox, int/float spinners (single/range/delta surfaced), string textbox, stringId, template-reference, enum (value shown). Hex/bytes fallback (V1): struct params, weighted/random lists, dynamic-variable lists. Guarantee: no param type is ever uneditable. The `@derived`/loaded flags + wrapper framing are maintained by the writer, not hand-edited.
- **D-03:** Generic across ALL object-template types — NO schema port. One generic param editor driven purely by the self-describing `.iff`. No `.tdf`/`Shared*ObjectTemplate.cpp` schema porting in V1. Consequence: "add an override for a field that exists NOWHERE in the chain" is inherently V2.
- **D-04:** Override / revert / edit — three mutations, V1. (1) Edit a local override's value; (2) Add override — promote inherited field to local override; (3) Remove override — revert local override back to inherited. Machine-managed (NOT user-editable): version-form param-count chunk + per-param `@derived`/loaded flags + wrapper framing. NOT V1: change-base/DERV re-parenting; adding a field absent from the entire chain.

### Claude's Discretion
- Editor surface / columns (exact layout, origin rendering, promote/revert triggers, breadcrumb presentation, dirty-state placement, hex-fallback sub-editor surfacing). Locked floor: inherited fields viewable with visible origin; field values editable; override/revert operations exist; structural bookkeeping machine-managed.
- `ObjectTemplate` mutable model ↔ existing `ObjectTemplateDecoder` relationship — recommend reuse the proven parse path + add a parallel mutable type composing on `MutableIffDocument`.
- CLI verb naming/shape — whether `roundtrip-iff` already covers object templates or a dedicated verb adds value.
- Reload trigger wording (CF-05) — exact badge copy follows researcher's confirmation (see §CF-05 below).
- Plan decomposition — likely 4–6 plans (planner has final say).

### Deferred Ideas (OUT OF SCOPE — V2)
- Type-aware schema (port `Shared*ObjectTemplate.cpp` / `.tdf` field/type/enum tables) — enables adding NOWHERE-in-chain overrides, friendly enum dropdowns, type validation.
- Change-base / `DERV` re-parenting.
- Full typed widgets for struct params / weighted-random lists / dynamic-variable lists (V1 uses hex fallback).
- Creating a new object template from scratch (empty-state designer).
- In-memory live patch for object templates (Phase 8 mode 3 — stays disabled inherited).
- Cross-reference / "find usages" across world snapshots / datatables / other templates.
- Shared "abstract editor base class" across IFF / Datatable / String-table / Object-template editors (post-Wave-1 refactor candidate).
</user_constraints>

## MUST-CONFIRM #1: CF-05 Object-Template Reload Semantics

**Confidence: HIGH.** Confirmed by reading the cache implementation directly.

### What the cache actually does

`ObjectTemplateList` is a thin wrapper over `DataResourceList<ObjectTemplate>` (a templated singleton map). [CITED: swg-client-v2 `.../sharedObject/.../ObjectTemplateList.cpp`, `.../sharedFoundation/.../DataResourceList.h`]

- The loaded-template cache is `std::map<const CrcString*, const T*> ms_loaded`, keyed by **filename CRC**.
- `fetch(CrcString)` — **if the template is already in `ms_loaded`, it returns the cached instance and `addReference()`. It never re-opens or re-reads the file.** Only a cache miss opens the `.iff` from `TreeFile`, parses it, and inserts it. **[VERIFIED: swg-client-v2 source, DataResourceList.h lines 298-326]**
- `release(dataResource)` — only erases from `ms_loaded` and `delete`s the instance **when `getReferenceCount() == 0`.** **[VERIFIED: DataResourceList.h lines 335-351]**
- `reload(Iff&)` — re-reads into the *existing* cached instance via `loadFromIff`, but **must be called explicitly** and warns/no-ops if the template is not already loaded. SWG does not call this on respawn for object templates. **[VERIFIED: DataResourceList.h lines 362-382]**
- **Base templates are themselves cached and refcounted.** `ObjectTemplate::addReference()`/`releaseReference()` propagate to `m_baseData`, and a base is fetched (and held) by every derived template that DERVs from it. **[VERIFIED: ObjectTemplate.cpp lines 166-185, 1301-1310]**

### Consequences for SC3 (the honest tier-(b) answer)

| Trigger | Does the edited `.iff` re-resolve? | Why |
|---------|-----------------------------------|-----|
| **Object respawn** (same template still cached) | **NO (not reliable)** | The respawned object calls `ObjectTemplateList::fetch(name)` → cache HIT → returns the **stale cached instance**. The on-disk edit is never re-read. |
| **TJT-driven scene change** | **MAYBE** | Scene cleanup destroys live objects, dropping their references. A template whose refcount reaches 0 is evicted and re-read on next fetch. BUT a base template shared across scenes (or any still-referenced template) stays cached → edit NOT picked up. Bases are the most-shared and least-evicted. |
| **Full relog / client restart** | **YES (reliable)** | All references dropped; cache rebuilt from disk on next world load. |

### What this means for the badge (LOCKED — planner may NOT loosen)

- `ReloadAssetClassifier` **already** classifies object-template `.iff` (root TypeIds `SHOT`/`STOT`/`SBOT`) → `ReloadTier.PendingNextSceneChange`. **No new classification work is needed — verify the SHOT/STOT/SBOT set covers the object-template root types the editor opens, and extend the set ONLY if a real fixture surfaces a root TypeId outside it.** **[VERIFIED: in-repo `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` lines 96-138]**
- The honest badge copy must convey: **"Edits apply on the next scene change for objects re-instantiated then; objects already in the world (and shared base templates) keep the cached version until a relog."** The badge must NOT claim "reflected on respawn" because respawn alone hits the cache. Tier-(b) candor = "Pending scene change (relog to guarantee)".
- The editor STATES this; it does NOT trigger a reload (no `reload(Iff&)` call from Utinni — that path is not wired and would require resolving the cached instance by CRC; out of V1 scope and risky given CON-M-05).

### Object-template root TypeId note for the classifier / sniff

The Phase 7 `ObjectTemplateDecoder.LooksLikeObjectTemplate` sniffs on a `DERV` child OR a digit-tagged version form with a 4-byte leading count chunk — it is **type-agnostic** and already works for any object-template root (tangible `SHOT`, creature/`STOT`, building `SBOT`, etc.). The `ReloadAssetClassifier` SHOT/STOT/SBOT allowlist is narrower than the sniff; both surface `.iff` object templates as `PendingNextSceneChange` (the classifier's `.iff` fallback also returns `PendingNextSceneChange`), so the conservative tier is correct even for a root TypeId not in the explicit allowlist. **[VERIFIED: ObjectTemplateDecoder.cs lines 103-113; ReloadAssetClassifier.cs lines 129-138]**

## MUST-CONFIRM #2: Self-Describing Param-Value Encoding

**Confidence: HIGH.** Layout confirmed from `TemplateParameter.{h,cpp}` and the generated `SharedTangibleObjectTemplate::load`. **Layout study only — no identifiers/code copied.** [CITED: swg-client-v2 `.../sharedUtility/.../TemplateParameter.cpp` + `.../sharedGame/.../objectTemplate/SharedTangibleObjectTemplate.cpp`]

### The container framing (already decoded by Phase 7)

`FORM <rootType>` → optional `FORM DERV` (one chunk: NUL-terminated base-template filename) → `FORM <version>` (digit tag, e.g. `0000`/`0010`) → first leaf chunk = `int32 paramCount` → `paramCount` param chunks. **Each param chunk = NUL-terminated field name, then the self-describing value bytes below.** The class-hierarchy parent forms (letter-tagged) chain via a nested `SharedObjectTemplate::load` at the tail. **[VERIFIED in-repo: ObjectTemplateDecoder.cs already parses this exact framing]**

### The per-param self-describing value (the NEW Phase 11 decode)

Each scalar param begins with a 1-byte **data-type tag**:

| Tag value | Name | Meaning |
|-----------|------|---------|
| 0 | NONE | param explicitly cleared (no value bytes follow) |
| 1 | SINGLE | one value follows |
| 2 | WEIGHTED_LIST | `int32 count`, then `count × (int32 weight + nested value)` |
| 3 | RANGE | two values follow (min, max) — numeric types only |
| 4 | DIE_ROLL | three int32 follow (num_dice, die_sides, base) — integer only |

**Numeric params (int/float) additionally carry a 1-byte delta-type immediately AFTER the data-type tag** (`' '` = absolute, `'+'`/`'-'` = delta-on-base). Bool/string/stringId/struct do **not** carry the delta byte. **[VERIFIED: IntegerParam::loadFromIff reads `read_int8()` type THEN `read_int8()` delta; BoolParam/StringParam read only the type byte]**

Per-type single-value payloads:

| Param type | SINGLE payload | Notes |
|------------|---------------|-------|
| `IntegerParam` | `int8 deltaType` + `int32` | RANGE = two int32; DIE_ROLL = three int32 |
| `FloatParam` | `int8 deltaType` + `float32` | RANGE = two float32; no DIE_ROLL |
| `BoolParam` | `int8 bool` | no delta byte |
| `StringParam` | NUL-terminated string | no delta byte; backslash→forward-slash fixup for `.iff` paths |
| `StringIdParam` | `StringParam table` + `StringParam index` | both nested (each re-emits its own type tag) |
| `VectorParam` | `bool ignoreY` + nested `FloatParam` x [,y] z radius | y omitted when ignoreY |
| `TriggerVolumeParam` | nested `StringParam name` + `FloatParam radius` | |
| `StructParamOT` (template-ref / struct) | `int32 structTag` then nested struct body (chunk-exit/enter dance) | complex — HEX FALLBACK in V1 |
| `DynamicVariableParam` | `bool extendBaseList` + type tag + nested data | complex — HEX FALLBACK in V1 |
| weighted list (any type) | `int32 count` + `count × (int32 weight + nested value)` | complex — HEX FALLBACK in V1 |

**This confirms D-02 and D-03:** the data-type tag + per-type framing IS the self-describing schema. **No `.tdf` is needed to decode scalars** — the tag tells you the shape. The list-shaped params (the `Shared*ObjectTemplate::load` `else if` branches that read `read_bool8()` append-flag + `int32 listCount` + N `*Param::loadFromIff`) are the ones that require knowing the param is list-typed BEFORE decoding — these are the **hex-fallback** cases (V1), because the generic decoder cannot tell a list param from a scalar without the per-type schema. **[VERIFIED: SharedTangibleObjectTemplate.cpp lines 1326-1453 — list params are dispatched by name-string match, not a self-describing list marker at the param-chunk level]**

### Planner consequence: which params get typed widgets vs hex fallback

- **Typed widget (V1):** a param chunk whose value, after the field name, starts with a data-type tag in {SINGLE, RANGE, DIE_ROLL, NONE} AND whose payload length is consistent with a scalar (`Integer`/`Float`/`Bool`/`String`/`StringId`/`Vector`/`TriggerVolume`). The decoder reads the tag, decodes the scalar, and surfaces the single/range/delta in the widget.
- **Hex fallback (V1):** WEIGHTED_LIST tag; struct/dynamic-variable params; any param whose self-describing decode does not cleanly consume exactly the chunk payload (ambiguity guard). Falls to the Phase 8 IFF hex/text leaf editor. **Guarantee: no param type is ever uneditable.**

> **Important decode-robustness note for the planner:** because the generic decoder does NOT have the per-type schema, it must decode a param chunk **defensively**: attempt the scalar decode for the leading tag, and if the decoded value does not consume exactly the chunk's payload bytes, treat the whole param as hex-fallback rather than guessing. This is the safe generic posture and preserves byte-exactness (hex fallback edits the raw leaf via `MutableIffDocument`).

## MUST-CONFIRM #3: `@base`/`m_baseData` Inheritance Fallback

**Confidence: HIGH.** [CITED: swg-client-v2 `.../sharedObject/.../ObjectTemplate.{h,cpp}` + the generated accessor pattern in `SharedTangibleObjectTemplate.cpp`]

### The runtime derivation semantics the editor MUST replicate

1. **Declaration:** the `DERV` form holds the base-template filename string. On load, the loader does `m_baseData = ObjectTemplateList::fetch(baseFilename)` — fetching (and reference-holding) the base template. The chain is walked one link at a time; each base is itself an object template with its own (possibly `DERV`-declared) base. **[VERIFIED: SharedTangibleObjectTemplate.cpp lines 1294-1311]**
2. **Per-field fallback (the canonical pattern):** every typed accessor is literally:
   ```
   if (!m_field.isLoaded())       // no local param chunk for this field
       return base->getField();   // recurse up the chain (DEBUG_FATAL if base==NULL and no default)
   return m_field.getValue();     // local override present → use it
   ```
   **[VERIFIED: SharedTangibleObjectTemplate::getTargetable, lines 965-1005]**
3. **"Loaded" == "local override present":** a `*Param`'s `m_loaded` flag is set true ONLY inside `loadFromIff` when its param chunk is physically read. A field absent from the version form's param chunks is `!isLoaded()` and falls through to the base. **[VERIFIED: TemplateParameter.h m_loaded init=false (line 138), set true on each loadFromIff success]**
4. **`derivesFrom`:** the chain is queryable via name comparison up `m_baseData` recursively. **[VERIFIED: ObjectTemplate.cpp lines 238-247]**

### How the editor's D-01 effective-merged view maps to this

- Open template T: parse its local param chunks (these are the **local overrides**, `InheritedFrom="local"`).
- Read T's `DERV` base name → resolve the base `.iff` bytes via `TrePayloadResolver` → parse it → its local params are candidate inherited fields. Recurse up the chain.
- **Effective value of a field = the value from the nearest ancestor (including T itself) that has a local param chunk for it.** This replicates `if(!isLoaded()) return base->getXxx()`.
- **Origin marker:** "local override" if T has the chunk; "inherited from `<ancestorName>`" otherwise (name the nearest ancestor that supplies it).
- **Promote to override (D-04 add):** copy the inherited value into a NEW local param chunk in T's version form, increment the machine-managed count.
- **Revert (D-04 remove):** delete T's local param chunk, decrement the count; the effective view then shows the inherited value again.
- **Graceful degradation (D-01 locked):** if `TrePayloadResolver.TryResolve` returns false for a base (encrypted/enumerate-only V6000, or simply not in the loaded archive set), render inherited rows as "unresolved base `<name>`" and still allow editing T's local params. NEVER throw on a missing ancestor. **[VERIFIED in-repo: TrePayloadResolver.TryResolve returns false for EnumerateOnly / missing archive — the degradation hook already exists]**

> **The delta-type subtlety (planner must preserve):** a numeric param with delta-type `'+'`/`'-'` is a **delta on the base's value**, not an absolute. The effective merged view should surface this honestly (show "base + N" or render the delta in the widget), and the writer must preserve the delta byte. For V1, surfacing delta numeric params as a typed widget is fine IF the delta byte is round-tripped verbatim; if the UX of editing a delta is unclear, the safe floor is hex-fallback for delta params. Planner's call within D-02's discretion. **[VERIFIED: m_dataDeltaType read/written in Integer/FloatParam loadFromIff/saveToIff]**

## Standard Stack

This is a pure C#/.NET-Framework WinForms + framework-library phase. **No new external packages.** Everything composes on already-shipped Utinni assets and the existing solution dependencies (xUnit, CommandLineParser, Newtonsoft.Json for CLI envelopes).

### Core (all in-repo, already shipped)
| Asset | Location | Purpose | Why Standard |
|-------|----------|---------|--------------|
| `ObjectTemplateDecoder` + `ObjectTemplateView`/`ObjectTemplateField` | `UtinniCoreDotNet/Formats/Decoders/` | Proven read parse path; `InheritedFrom` slot already designed for local-vs-base; `LooksLikeObjectTemplate` sniff | Phase 7, the explicit D-13 foundation |
| `MutableIffDocument` + `IffWriter` + `IffPayloadCursor` | `UtinniCoreDotNet/Formats/Iff/` | Hybrid-DOM byte-exact-on-untouched edit + serialize; cursor for reading typed values from leaf payloads | Phase 8 — object templates ARE IFF, byte-exactness free |
| `TrePayloadResolver` + `TreArchiveIndex` + `TreRecordIndexResolver` | `UtinniCoreDotNet/Formats/Tre/` | Resolve a base-template name → `.iff` bytes for the D-01 chain walk; `TryResolve` already returns false for enumerate-only/missing (degradation hook) | Phase 7/8 — Phase 7 left this untouched for OT; Phase 11 wires it in |
| `DatatableEditController` (clone) | `UtinniCoreDotNet/Editing/` | Editor-local undo/redo precedent (`CaptureState`/`RestoreState`, `MarkSaved`, NeedsReview seams) — clone for `ObjectTemplateEditController` | Phase 9 CF-04/CON-M-05 precedent |
| `LooseOverridePath`, `TreRepackSaveTarget`/`TreWriter.Repack`, `TreBackupPath`, `TreRepackLock` | `UtinniCoreDotNet/Saving/` | Save modes 1/4 plumbing (refuses V6000) | Phase 8 — reuse verbatim |
| `ReloadAssetClassifier` | `UtinniCoreDotNet/Saving/` | CF-05 reload-tier routing — already classifies object-template `.iff` | Phase 8/10 — already correct; verify, don't rebuild |

### Supporting (TJT plugin side, in `D:/Code/UtinniPlugins`)
| Asset | Location | Purpose | When to Use |
|-------|----------|---------|-------------|
| `FormDatatableEditor` (+ `.Designer.cs`) | `.../TheJawaToolboxDotNet/UI/Forms/` | Closest SubPanel host precedent: toolbar + themed grid + dirty-state + Save▾ + singleton hide-not-dispose | Clone shape for `FormObjectTemplateEditor` |
| `FormStringTableEditor`, `FormIffEditor`, `FormTreBrowser` | same | Save▾ modes, hand-off precedents, "Switch to typed view" (IFF Editor) | D-10.x entry-point hand-offs |
| `TJT.Saving.DatatableSaveTargets` | `.../TheJawaToolboxDotNet/Saving/` | <100-line composition shim forwarding to Phase 8 `IffSaveTargets`/`TreRepackSaveTarget` | Clone as `ObjectTemplateSaveTargets` |
| `ThemedDataGridView`, per-type cell widgets, `DatatableColumnFactory` | `.../TheJawaToolboxDotNet/UI/Controls/` | Dark-theme grid + per-type widget factory (D-02 typed widgets) | Adapt for OT field/value/origin/type columns + hex-fallback sub-editor |
| `SingletonFormClosePolicy` | `UtinniCoreDotNet/UI/Controls/` (framework) | hide-not-dispose decision for MEF-registered forms (CI-coverable) | Apply from the start (Phase 8 08-05 canonical pattern) |
| `Plugin.cs` `SubPanelContainer` | `.../TheJawaToolboxDotNet/` | Fifth/final SubPanel registration site (try/catch) | CF-06 |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Dedicated `roundtrip-ot` CLI verb | Extend existing `roundtrip-iff` | Object templates ARE IFF — `roundtrip-iff` already exercises the `MutableIffDocument`/`IffWriter` byte-exact path on any IFF. A dedicated verb adds value ONLY if it asserts *param-level* (not chunk-level) untouched identity after a typed mutation. **Recommendation: extend `roundtrip-iff` for the no-mutation byte-exact gate; add a small `roundtrip-ot` ONLY if the typed override/revert mutation needs a param-level slice assertion analogous to `roundtrip-tab`'s per-cell comparison.** See Validation Architecture. |
| Porting `Shared*ObjectTemplate.cpp` schema (V2) | Generic self-describing decode (D-03) | Schema port gives friendly enums + NOWHERE-in-chain field add, but is large and `.tdf` source is absent from the corpus. D-03's generic approach covers all types uniformly for V1. |
| Calling `ObjectTemplateList::reload` to force live re-resolve | Honest tier-(b) badge (CF-05) | `reload` requires resolving the cached instance by CRC on the game thread and risks CON-M-05 entanglement; not worth it for V1. Badge candor is the locked path. |

**Installation:** None — no new packages. (Package Legitimacy Audit below is N/A for that reason.)

## Package Legitimacy Audit

**Not applicable.** This phase installs **no external packages**. All work composes on already-shipped in-repo framework assets and the existing solution's dependency set (xUnit, CommandLineParser, Newtonsoft.Json — all introduced and audited in prior phases). slopcheck/registry verification is moot: there is nothing to install.

| Package | Registry | Disposition |
|---------|----------|-------------|
| *(none — no new dependencies)* | — | N/A |

## Architecture Patterns

### System Architecture Diagram

```
                 ┌─────────────────────────────────────────────────────────────┐
  USER opens an  │ ENTRY POINTS (TJT plugin)                                    │
  object-template│  • File picker  • TRE Browser "Open in OT Editor" (sniff)    │
  .iff           │  • IFF Editor "Switch to typed OT view" (root FORM <type>)   │
                 └───────────────────────────┬─────────────────────────────────┘
                                             │ bytes + OpenSource provenance
                                             ▼
        ┌───────────────────────────────────────────────────────────────────────┐
        │ FRAMEWORK: Formats/ObjectTemplate/ (NEW)                                │
        │                                                                         │
        │  IffReader ──► IffDocument ──► MutableIffDocument (hybrid DOM, capture) │
        │                     │                                                   │
        │                     ▼                                                   │
        │  ObjectTemplate parse (reuse Phase 7 ObjectTemplateDecoder path):       │
        │   FORM<type> → DERV(baseName) → FORM<version> → count + param chunks    │
        │                     │                                                   │
        │   ┌─────────────────┴────────────────┐                                  │
        │   ▼ local params (overrides)          ▼ DERV base name                  │
        │  typed-decode each param chunk    ┌──────────────────────────────────┐  │
        │  (data-type tag → scalar | hex)   │ INHERITANCE RESOLVER (D-01)       │  │
        │                                   │ TrePayloadResolver.TryResolve     │  │
        │                                   │  (base .iff bytes) ── recurse ──┐ │  │
        │                                   │  unresolved → "unresolved base" │ │  │
        │                                   └──────────────┬──────────────────┘ │  │
        │                                                  │ ancestor params    │  │
        │                     ┌────────────────────────────▼──────────────┐     │  │
        │                     │ EFFECTIVE MERGED VIEW                       │    │  │
        │                     │ one row/field: effective value + origin     │    │  │
        │                     │ (local override | inherited from <base> |   │    │  │
        │                     │  unresolved base <name>) + breadcrumb       │    │  │
        │                     └────────────────────────────┬───────────────┘    │  │
        └──────────────────────────────────────────────────┼────────────────────┘  │
                                                            │ rows
                                                            ▼
        ┌───────────────────────────────────────────────────────────────────────┐
        │ TJT PLUGIN: FormObjectTemplateEditor (clone FormDatatableEditor)        │
        │  ThemedDataGridView: Field | Effective Value | Origin | Type            │
        │   • typed widget per scalar (D-02) OR hex-fallback sub-editor           │
        │   • promote-to-override / revert-to-inherited (D-04) context actions    │
        │   • dirty-state, ancestor breadcrumb header                             │
        │            │ edit / add-override / remove-override                      │
        │            ▼                                                            │
        │  ObjectTemplateEditController (clone DatatableEditController, CF-04)     │
        │   editor-local undo/redo  ── DISENTANGLED from scene UndoRedoManager    │
        │   (CON-M-05) ── mutates MutableIffDocument param chunks                 │
        └────────────────────────────┬──────────────────────────────────────────┘
                                     │ save
                                     ▼
        ┌───────────────────────────────────────────────────────────────────────┐
        │ SAVE (CF-03 modes 1/2/4, reuse Phase 8 targets)                         │
        │  IffWriter.Write(MutableIffDocument) → byte-exact untouched params      │
        │   mode 1 loose override | mode 2 Save/Save-As | mode 4 .tre repack      │
        │   (mode 3 live-patch DISABLED inherited)                                │
        │            │                                                            │
        │            ▼                                                            │
        │  ReloadAssetClassifier → PendingNextSceneChange  ──► honest tier-(b)    │
        │   badge: "applies on next scene change for re-instantiated objects;     │
        │   relog to guarantee" (CF-05; editor STATES, never triggers)            │
        └───────────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure
```
UtinniCoreDotNet/
├── Formats/
│   └── ObjectTemplate/                 # NEW — sibling to Datatable/, StringTable/
│       ├── ObjectTemplateParam*.cs     # typed param value model (scalar union + hex-fallback marker)
│       ├── MutableObjectTemplate*.cs    # mutable model over MutableIffDocument
│       ├── ObjectTemplateResolver.cs   # D-01 DERV-chain walk via TrePayloadResolver → effective view
│       ├── EffectiveField.cs           # row: name + effective value + origin + ancestor breadcrumb
│       └── ObjectTemplateWriter.cs     # composes IffWriter.Write (byte-exact)
├── Editing/
│   └── ObjectTemplateEditController.cs # NEW — clone DatatableEditController (CF-04 undo, CON-M-05)
└── Saving/                             # REUSE — ReloadAssetClassifier already classifies OT .iff

Utinni.Cli/Commands/                     # extend RoundtripIffCommand OR add RoundtripOtCommand (CF-02)

UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/
├── UI/Forms/FormObjectTemplateEditor.cs(.Designer.cs)   # NEW — clone FormDatatableEditor host
├── UI/Controls/ (typed widgets reused/adapted from Datatable)
├── Saving/ObjectTemplateSaveTargets.cs                   # NEW — <100-line shim to Phase 8 targets
└── Plugin.cs                                             # register 5th SubPanel (try/catch)
```

### Pattern 1: Hybrid-DOM byte-exact edit (Phase 8/9)
**What:** Parse → `MutableIffDocument.FromDocument(doc, sourceBytes)` (captures each node's verbatim byte slice) → mutate only touched param chunks → `IffWriter.Write` re-emits untouched nodes byte-for-byte; any edit dirties the node AND every ancestor.
**When to use:** All OT edits. Object templates ARE IFF so this works directly; no Phase-10-style engineered ordering.
**Source:** in-repo `UtinniCoreDotNet/Formats/Iff/MutableIffDocument.cs` lines 80-128 (verified this session).

### Pattern 2: Effective-merged inheritance view with origin (NEW, D-01)
**What:** Replicate the client's `if(!isLoaded()) return base->getXxx()` per-field fallback by merging the parsed local params of T and each resolved ancestor; one row per field with effective value + nearest-ancestor origin; degrade gracefully on an unresolved base.
**When to use:** The heart of the phase — what makes this an *object-template* editor rather than the IFF editor pointed at a template.
**Source:** semantics from swg-client-v2 `SharedTangibleObjectTemplate::getTargetable` (verified); resolution via in-repo `TrePayloadResolver.TryResolve`.

### Pattern 3: Editor-local undo disentangled from scene manager (CF-04 / CON-M-05)
**What:** `ObjectTemplateEditController` owns an independent undo/redo stack using `CaptureState`/`RestoreState` over the mutable param chunks, exactly like `DatatableEditController`. Never references `UndoRedoManager`.
**When to use:** Always. Extra-load-bearing because object templates touch live-scene objects.
**Source:** in-repo `UtinniCoreDotNet/Editing/DatatableEditController.cs` (clone target).

### Anti-Patterns to Avoid
- **Decoding a param chunk by guessing its type without consuming-exactly verification** → silent corruption. Decode defensively; fall to hex on any payload-length mismatch.
- **Hand-editing the param-count chunk or the `@derived`/loaded/wrapper framing** → writer must own these (D-04 machine-managed). Users edit values + override membership only.
- **Calling `ObjectTemplateList::reload` / touching the scene `UndoRedoManager`** → CON-M-05 violation; CF-05 says STATE the reload, don't trigger it.
- **Throwing on an unresolved base** → D-01 locked degradation says never block the open.
- **Over-promising "reflected on respawn" in the badge** → cache semantics make respawn unreliable; CF-05 candor is locked.
- **Promoting struct/weighted-list/dynamic-variable params to typed widgets in V1** → explicitly V2; use hex fallback.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| IFF parse/serialize/byte-exactness | A new OT-specific serializer | `MutableIffDocument` + `IffWriter` (Phase 8) | OT is IFF; byte-exactness + ancestor-invalidation already solved + tested |
| OT container parse (FORM/DERV/version/count/params) | A second parser | Reuse `ObjectTemplateDecoder` parse path (Phase 7) | Already handles the framing, forged-count guards, the sniff |
| Base-template resolution from name | A new TRE reader | `TrePayloadResolver.TryResolve` (Phase 7/8) | Handles path-traversal defense, enumerate-only/V6000 false-return (the degradation hook), loose vs master-index descriptors |
| Editor-local undo/redo | A bespoke stack | Clone `DatatableEditController` (Phase 9) | `CaptureState`/`RestoreState`/`MarkSaved` proven + keeps CON-M-05 separation |
| Save modes 1/2/4 | New save plumbing | `IffSaveTargets`/`TreRepackSaveTarget`/`LooseOverridePath` (Phase 8) | V6000 refusal, backup, repack lock, path containment all done |
| Reload-tier routing | A new badge classifier | `ReloadAssetClassifier` (Phase 8/10) | Already classifies OT `.iff` → PendingNextSceneChange |
| SubPanel host shell | A from-scratch form | Clone `FormDatatableEditor` (Phase 9) | Toolbar/grid/dirty-state/Save▾/singleton hide-not-dispose proven |
| CF-02 round-trip gate | A new harness | Extend `roundtrip-iff` (+ optional param-slice `roundtrip-ot`) | The IFF byte-exact gate already exists |

**Key insight:** Phase 11 adds essentially ZERO new infrastructure. The only genuinely new code is (a) the self-describing typed-value decode/encode (the data-type-tag scalar reader), (b) the DERV-chain effective-merge resolver, and (c) the three override/revert/edit mutations on the param chunk set. Everything else is composition over Phase 7/8/9 assets.

## Common Pitfalls

### Pitfall 1: Assuming "respawn re-resolves the edit" for SC3
**What goes wrong:** The SC3 demo expects the live client to show the edit on respawn; it does not, because `ObjectTemplateList::fetch` returns the cached instance.
**Why it happens:** The cache is CRC-keyed and refcount-evicted; bases are the most-shared and least-evicted.
**How to avoid:** Demo the edit with a **relog** (or a scene change that fully drops the edited template's references) and badge tier-(b) candor. Plan SC3 as relog-reliable, respawn-best-effort.
**Warning signs:** A demo script that says "respawn the object" without a relog fallback.

### Pitfall 2: Generic decoder mis-typing a list/struct param as a scalar
**What goes wrong:** A list/struct param's leading byte happens to look like a valid data-type tag; a naive scalar decode produces garbage and a non-byte-exact write.
**Why it happens:** Without the per-type schema (D-03 deferred), the generic decoder cannot distinguish a list param at the chunk level.
**How to avoid:** Decode defensively — only accept a scalar decode if it consumes EXACTLY the chunk payload; otherwise hex-fallback. Round-trip golden a template with the complex param types to lock this.
**Warning signs:** `roundtrip-iff`/`roundtrip-ot` byte-diff on a template that has customization-variable lists.

### Pitfall 3: Delta-type numeric params edited as absolutes
**What goes wrong:** A `'+'`/`'-'` delta param edited as if absolute changes meaning and/or loses the delta byte.
**Why it happens:** The delta byte sits between the data-type tag and the value and is easy to drop.
**How to avoid:** Round-trip the delta byte verbatim; surface delta-ness in the widget or hex-fallback delta params (planner's D-02 call).
**Warning signs:** Edited numeric param whose on-disk byte length shrinks by 1.

### Pitfall 4: Entangling the edit with the scene UndoRedoManager
**What goes wrong:** Object-template edits touch live-scene objects; routing undo through the scene manager risks the CON-M-05 scene-cleanup entanglement that the preservation guard-rail forbids.
**Why it happens:** Tempting to reuse the scene undo for "consistency."
**How to avoid:** Editor-local stack only (clone DatatableEditController). Never reference UndoRedoManager.
**Warning signs:** Any `using` or call into the scene undo manager from the OT controller.

### Pitfall 5: Throwing on an unresolved base
**What goes wrong:** Opening a template whose base isn't in the loaded archive set (or is V6000-encrypted) throws and blocks the open.
**Why it happens:** Treating `TryResolve == false` as an error.
**How to avoid:** D-01 locked degradation — render "unresolved base `<name>`" rows, still allow editing local params.
**Warning signs:** An exception path on `TryResolve` returning false.

## Code Examples

> Per the reference policy, these are **shape sketches in original Utinni style** describing the verified on-disk layout and reuse APIs — NOT copied from any reference source.

### Reading a scalar param value (self-describing tag)
```csharp
// Source: layout verified from swg-client-v2 TemplateParameter.cpp (IntegerParam::loadFromIff etc.)
// param chunk payload = [NUL-terminated name][int8 dataTypeTag]([int8 deltaType] for numeric)[value...]
var cursor = new IffPayloadCursor(paramLeaf.Data);
string fieldName = cursor.ReadCString(Encoding.ASCII);
byte dataTypeTag = cursor.ReadByte();          // 0 NONE,1 SINGLE,2 WEIGHTED_LIST,3 RANGE,4 DIE_ROLL
// For Integer/Float params only, a delta byte follows the tag:
//   byte deltaType = cursor.ReadByte(); // ' ', '+', '-'
// SINGLE int: int value = cursor.ReadInt32Le();
// If decode does not consume exactly cursor.Remaining == 0 → treat as hex-fallback (defensive).
```

### Resolving the DERV base chain for the effective view (D-01)
```csharp
// Source: reuse in-repo TrePayloadResolver (verified) + ObjectTemplateDecoder parse path (Phase 7)
if (TrePayloadResolver.TryResolve(baseDescriptor, out byte[] baseBytes))
{
    var baseDoc = new IffReader().Read(baseBytes);           // Phase 8 reader
    var baseView = ObjectTemplateDecoder.Decode(baseDoc);     // Phase 7 parse (local params + its DERV)
    // merge baseView's local params as "inherited from <baseName>" where T has no local override;
    // recurse on baseView.BaseTemplate
}
else
{
    // D-01 locked degradation: render inherited rows as "unresolved base <name>"; never throw.
}
```

### Byte-exact save (mode 2) and reload badge (CF-05)
```csharp
// Source: in-repo MutableIffDocument/IffWriter (Phase 8) + ReloadAssetClassifier (verified)
byte[] outBytes = new IffWriter().Write(mutableDoc);          // untouched param chunks byte-identical
// ... write via Phase 8 IffSaveTargets (mode 1/2) or TreRepackSaveTarget (mode 4) ...
ReloadTier tier = ReloadAssetClassifier.Classify(".iff", rootTypeId); // → PendingNextSceneChange
// badge copy follows tier verbatim: "Applies on next scene change for re-instantiated objects;
// relog to guarantee. Objects already in world keep the cached template." (CF-05 — do not loosen)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Phase 7 read-only OT decode (local params + base NAME, values as hex) | Phase 11 typed decode + DERV-chain effective-merge + editing | Phase 11 (this) | The D-13 deferral the Phase 7 decoder's own comment promised |
| SOE-era separate object-template editor (SOE `TemplateEditor` app, present in corpus under `engine/client/application/TemplateEditor/`) | TJT in-app SubPanel | This phase | Replaces the standalone tool; the SOE app is a UX *mental-model reference only* (property grid + per-field origin), not a code reference |

**Deprecated/outdated:**
- The Phase 7 hex-only value rendering for object-template params is superseded by the typed/hex hybrid (D-02) — but the Phase 7 *parse path* and `LooksLikeObjectTemplate` sniff are reused unchanged.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Object-template root TypeIds the editor opens are covered by the existing `ReloadAssetClassifier` SHOT/STOT/SBOT allowlist OR fall through its conservative `.iff`→PendingNextSceneChange fallback. | CF-05 | LOW — the `.iff` fallback already returns the conservative tier for any unlisted root, so the badge stays honest even if a fixture surfaces a new root TypeId. Planner should still confirm the demo fixtures' root types. |
| A2 | The Phase 8 `IffPayloadCursor` exposes the int8/int32/float/CString reads needed for the self-describing scalar decode. | MUST-CONFIRM #2 / Code Examples | LOW — Phase 7 decoder already uses `ReadCString`/`ReadInt32Le`/`ReadBytes`; a float read may need a thin helper if absent (verify in `IffPayloadCursor`). |
| A3 | Extending `roundtrip-iff` (vs a dedicated `roundtrip-ot`) is sufficient for the no-mutation byte-exact gate. | Validation Architecture | LOW — `roundtrip-iff` already round-trips any IFF byte-exactly; the only gap is a *param-level* untouched assertion after a typed mutation, which a small `roundtrip-ot` (mirroring `roundtrip-tab`'s per-cell slice) would cover. Planner decides. |
| A4 | A TJT scene change can evict an edited template only when all its references drop; bases shared across scenes persist. | CF-05 | LOW — follows directly from the verified refcount-eviction cache; the badge is conservative regardless. |

**No `[ASSUMED]` package or compliance claims exist** — there are no new dependencies and no security/retention/compliance surface in this phase.

## Open Questions (RESOLVED)

1. **Does `IffPayloadCursor` already expose a little-endian float read?**
   - **RESOLVED (PATTERNS.md):** `ReadFloatLe()` already EXISTS on `IffPayloadCursor`. The only additive change is a 1-byte `ReadInt8` helper (binary-compat safe — the type is `internal sealed`). Planned in 11-01 Task 1.
   - What we knew: it exposes `ReadCString`, `ReadInt32Le`, `ReadBytes`, `Remaining` (used by Phase 7).

2. **Which demo fixtures for SC3?**
   - **RESOLVED (deferred to smoke):** a tangible template with a SINGLE bool/string local override gives the cleanest live demo; the concrete shipped template is confirmed with the user at smoke time. This is the SC3/UAT concern (11-05 Task 2), not format work.
   - What we knew: SC3 needs a relog-reliable demo per the cache semantics.

3. **V6000 / encrypted base in the chain.**
   - **RESOLVED (regression-covered):** an unresolved-base fixture is included in the golden set so the D-01 degradation path is regression-tested regardless of how common a V6000 base is in the user's archive set. `TrePayloadResolver.TryResolve` returns false (never throws) for enumerate-only/V6000 → renders "unresolved base". Planned in 11-02 (resolver) + Wave-0 goldens.
   - What we knew: degradation renders "unresolved base" via the `TryResolve == false` path.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| swg-client-v2 reference corpus | Format/inheritance/cache study (read-only spec) | ✓ | D:/Code/swg-client-v2 | — (study complete this session) |
| UtinniPlugins sibling repo | TJT SubPanel host (cross-repo commit) | ✓ | D:/Code/UtinniPlugins (standing write authority per memory) | — |
| VS 2026 MSBuild (v145) | Build TJT + UtinniCoreDotNet | ✓ | Dev18 / v145 | VS 2022 fallback on disk |
| `dotnet test` (xUnit) | Tier-1 + CLI golden tests | ✓ | existing solution | run via `--no-build` after MSBuild (resx quirk) |
| Live SWGEmu/Restoration client | SC1/SC3 live smoke + V1 release-gate demo | ✓ (user-driven) | per user's clients | Tier-4 manual residual (automation-augmented per Phase 8/9/10 precedent) |

**Missing dependencies with no fallback:** None — all reference material and build/test infra confirmed present.
**Missing dependencies with fallback:** Live-client SC3 observation is the documented Tier-4 residual (precedent: Phases 8/9/10 accepted automation-augmented smoke; Phase 10 deferred its SC3 live-reload residual). The CF-05 cache reality means SC3's live observation is best-effort/relog-gated — plan accordingly.

## Validation Architecture

> Nyquist validation ENABLED (`config.json` has no `workflow.nyquist_validation: false`).

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (C#, .NET Framework) — `UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`; Catch2 for native (not used this phase) |
| Config file | existing `.csproj` test projects (no new config) |
| Quick run command | `dotnet test UtinniCoreDotNet.Tests --no-build --filter ObjectTemplate` (after MSBuild) |
| Full suite command | MSBuild (VS2026, Debug+Release\|x86) then `dotnet test --no-build` across `UtinniCoreDotNet.Tests` + `Utinni.Cli.Tests` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PROD-W1-OT | Byte-exact write on untouched params (CF-02) | golden/round-trip | `dotnet test Utinni.Cli.Tests --no-build --filter Roundtrip` (extend roundtrip-iff and/or add roundtrip-ot) | ❌ Wave 0 (new goldens) |
| PROD-W1-OT | Self-describing scalar decode/encode round-trip | unit | `dotnet test UtinniCoreDotNet.Tests --no-build --filter ObjectTemplateParam` | ❌ Wave 0 |
| PROD-W1-OT | Inheritance effective-merge matches `if(!isLoaded()) base->get` semantics | unit | `dotnet test UtinniCoreDotNet.Tests --no-build --filter ObjectTemplateResolver` | ❌ Wave 0 |
| PROD-W1-OT | Unresolved-base graceful degradation (D-01) | unit | `...--filter UnresolvedBase` | ❌ Wave 0 |
| PROD-W1-OT | Override / add-override / revert mutations + editor-local undo (D-04/CF-04) | unit | `...--filter ObjectTemplateEditController` | ❌ Wave 0 |
| PROD-W1-OT | Reload-tier classification = PendingNextSceneChange (CF-05) | unit | `...--filter ReloadAssetClassifier` (likely already covered; add OT root-type case) | ✅ exists, extend |
| PROD-02 | All-five-subpanels demo + CI green (V1 gate) | manual/Tier-4 | live-client demo script + CI status | manual residual |

### Byte-exactness assertion (CF-02 — required fixtures)
- **Multi-level inheritance chain fixture:** a child template DERV→mid→base, child overrides a subset of fields. Assert effective-merge resolves each field to the correct nearest-ancestor value AND the child round-trips byte-exact on untouched params.
- **Complex-param (hex-fallback) fixture:** a template with a customization-variable list / struct / weighted-list param. Assert the generic decoder routes it to hex-fallback (does NOT mis-type it) AND round-trips byte-exact when untouched.
- **Unresolved-base degradation fixture:** a template whose DERV base is NOT in the test archive set (or is enumerate-only). Assert the open does not throw, inherited rows render "unresolved base", local params remain editable, and untouched local params round-trip byte-exact.
- **Delta-numeric fixture (optional but recommended):** a numeric param with `'+'`/`'-'` delta byte; assert the delta byte round-trips verbatim.

### Inheritance-resolution validation against the reference semantics
The resolver's per-field result MUST equal "value from the nearest ancestor (incl. self) whose param chunk is present" — the C# equivalent of the verified `if(!m_field.isLoaded()) return base->getField()`. Unit-test by constructing the multi-level fixture and asserting each field's `(effectiveValue, origin)` against a hand-computed expected table derived from which template declares which param.

### SC3 live-reload observation (and its honest tier-(b) limits)
- **What it looks like:** edit a scalar override on a tangible template, save (mode 1 loose override or mode 4 repack), then **relog the client** and observe the object reflects the edit. A scene change MAY suffice if the edited template's objects are all destroyed/recreated, but is not guaranteed.
- **Honest limit:** respawn alone does NOT re-resolve (cache hit). The validation is therefore **relog-reliable, respawn-best-effort, scene-change-conditional** — documented as the Tier-4 residual, mirroring Phase 10's deferred SC3 live-reload residual. The automated lane validates byte-exactness + decode + merge + degradation; the live observation is the bounded manual residual.

### Sampling Rate
- **Per task commit:** quick filtered run (`--filter ObjectTemplate`).
- **Per wave merge:** full `dotnet test --no-build` across both test projects, both build configs.
- **Phase gate:** full suite green + the V1 release-gate aggregate (all 5 subpanels demo, Tier 1+2 CI green on `main`, 15 critical bugs closed) before `/gsd:verify-work`, then tag V1.

### Wave 0 Gaps
- [ ] `UtinniCoreDotNet.Tests/ObjectTemplate/ObjectTemplateParamTests.cs` — self-describing scalar decode/encode (REQ PROD-W1-OT)
- [ ] `UtinniCoreDotNet.Tests/ObjectTemplate/ObjectTemplateResolverTests.cs` — effective-merge + origin + unresolved-base (PROD-W1-OT, D-01)
- [ ] `UtinniCoreDotNet.Tests/Editing/ObjectTemplateEditControllerTests.cs` — override/revert/edit + undo (D-04/CF-04)
- [ ] `Utinni.Cli.Tests` goldens — multi-level chain, complex-param hex-fallback, unresolved-base (CF-02)
- [ ] Extend `ReloadAssetClassifier` tests with an OT root-type case if not already present.
- [ ] Framework install: none — existing xUnit infra covers all.

## Security Domain

`security_enforcement` is not present in `config.json` (treated as enabled), but this phase has an **extremely narrow attack surface**: it parses local `.iff` files the user already controls and writes local files / `.tre` archives. There is no auth, session, network, or remote-input dimension.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — (local desktop tool) |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | **yes** | Defensive parse: forged-count guards (Phase 7 `ObjectTemplateDecoder` already has `NegativeCount`/`CountExceedsCap`), bounded slice capture (`MutableIffDocument` already validates offsets), path-traversal defense on base resolution (`TrePayloadResolver` already rejects `..`/rooted names), `MaxBlockSize`/DoS caps. Reuse all — do not weaken. |
| V6 Cryptography | no | — (V6000 archives are refused, not decrypted) |

### Known Threat Patterns for the OT `.iff` / TRE stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed/forged param count → over-read | Tampering / DoS | Reuse Phase 7 forged-count guards; defensive scalar decode (consume-exactly-or-hex) |
| Path traversal via a malicious DERV base name resolving outside the archive base dir | Tampering / Elevation | `TrePayloadResolver` already rejects `..`/rooted tree-file names and enforces master-index containment (verified this session) |
| Deeply-recursive / cyclic DERV chain → stack overflow | DoS | Add a recursion depth cap + visited-set in the resolver (NEW — small, in the resolver). Flag for the planner. |
| Oversized `.iff`/record → memory exhaustion | DoS | `TrePayloadResolver.MaxBlockSize` (256 MB) cap already enforced |

> **One genuinely new defensive control to add:** a **DERV-chain depth/cycle guard** in the new resolver (a malicious or accidentally-cyclic `DERV` could otherwise recurse without bound). This is a small addition local to `ObjectTemplateResolver` — the planner should make it an explicit task/verification.

## Sources

### Primary (HIGH confidence)
- swg-client-v2 `.../sharedObject/.../ObjectTemplate.{h,cpp}` — base lifecycle, `m_baseData`, `addReference`/`releaseReference` propagation, `derivesFrom`, `loadFromIff` flow (verified).
- swg-client-v2 `.../sharedObject/.../ObjectTemplateList.cpp` + `.../sharedFoundation/.../DataResourceList.h` — the CRC-keyed loaded-cache, `fetch` cache-hit-no-reread, refcount eviction in `release`, explicit `reload` (CF-05, verified).
- swg-client-v2 `.../sharedUtility/.../TemplateParameter.{h,cpp}` — `DataTypeId` enum (NONE/SINGLE/WEIGHTED_LIST/RANGE/DIE_ROLL), per-type self-describing load/save, delta byte, `m_loaded` flag, weighted-list framing (MUST-confirm #2, verified).
- swg-client-v2 `.../sharedGame/.../objectTemplate/SharedTangibleObjectTemplate.cpp` — the version-form/count/param-chunk container framing, list-param name-dispatch, and the canonical `if(!isLoaded()) return base->getXxx()` accessor (MUST-confirm #3, verified).
- In-repo `UtinniCoreDotNet/Formats/Decoders/ObjectTemplateDecoder.cs`, `Formats/Iff/MutableIffDocument.cs`, `Formats/Tre/TrePayloadResolver.cs`, `Saving/ReloadAssetClassifier.cs`, `Editing/DatatableEditController.cs`, `Utinni.Cli/Commands/RoundtripTabCommand.cs` (reuse paths, verified this session).
- In-repo `.planning/phases/11-.../11-CONTEXT.md`, `.planning/REQUIREMENTS.md`, `.planning/STATE.md` (locked decisions + Phase 7/8/9/10 lineage).

### Secondary (MEDIUM confidence)
- swg-client-v2 `engine/client/application/TemplateEditor/` (SOE's own object-template editor) — UX mental-model reference only (property-grid + per-field origin); NOT a code reference.

### Tertiary (LOW confidence)
- None — all load-bearing claims are verified from primary source.

## Metadata

**Confidence breakdown:**
- CF-05 reload semantics: HIGH — read the cache implementation directly.
- Self-describing encoding (MUST #2): HIGH — read every `*Param::loadFromIff` + the container framing.
- Inheritance fallback (MUST #3): HIGH — read the canonical accessor + base chaining.
- Standard stack / reuse paths: HIGH — verified each asset exists in-repo with the API shape claimed.
- Architecture / pitfalls: HIGH — derived from verified format + cache reality + Phase 8/9 precedent.

**Research date:** 2026-05-30
**Valid until:** 2026-06-29 (stable — the reference corpus and in-repo assets are frozen; only the live-client demo plan is time-sensitive).

## RESEARCH COMPLETE

**Phase:** 11 - TJT subpanel — Object Template Editor
**Confidence:** HIGH

### Key Findings
- **CF-05 (highest priority) resolved:** `ObjectTemplateList` is a CRC-keyed, refcount-evicted cache that NEVER re-reads a cached template from disk on fetch. Edits re-resolve reliably only on **relog**; respawn hits the cache; scene change is conditional on full reference drop (bases persist). `ReloadAssetClassifier` already classifies OT `.iff` → `PendingNextSceneChange` — the honest tier-(b) badge is "applies next scene change for re-instantiated objects; relog to guarantee." Planner may not loosen it.
- **Self-describing encoding (MUST #2) resolved:** 1-byte data-type tag (NONE/SINGLE/WEIGHTED_LIST/RANGE/DIE_ROLL) + per-type framing + a delta byte on numeric params. Scalars decode with no schema (drives D-02 typed widgets); lists/structs/dynamic-vars are the hex-fallback set. Decode defensively (consume-exactly-or-hex).
- **Inheritance fallback (MUST #3) resolved:** canonical `if(!m_field.isLoaded()) return base->getXxx()`; "loaded" == local param chunk present. The D-01 effective-merge view = nearest-ancestor-with-the-chunk; degrade (never throw) on unresolved base via `TrePayloadResolver.TryResolve == false`.
- **Zero new infrastructure / zero new packages:** compose on Phase 7 decoder, Phase 8 `MutableIffDocument`/`IffWriter`/save targets, Phase 7/8 `TrePayloadResolver`, Phase 9 `DatatableEditController`/`FormDatatableEditor`. Only genuinely new code: typed scalar decode, DERV-chain resolver (add a depth/cycle guard), three mutations.
- **CF-02:** extend `roundtrip-iff` for the byte-exact gate; add a small `roundtrip-ot` only if a param-level slice assertion (à la `roundtrip-tab`) is wanted. Required goldens: multi-level chain, complex-param hex-fallback, unresolved-base degradation.

### File Created
`D:\Code\Utinni\.planning\phases\11-tjt-subpanel-object-template-editor\11-RESEARCH.md`

### Confidence Assessment
| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | Every reuse asset verified in-repo with claimed API shape |
| Architecture | HIGH | Derived from verified format + cache + Phase 8/9 precedent |
| Pitfalls | HIGH | Each rooted in a verified source fact (cache, delta byte, list mis-typing, CON-M-05) |

### Open Questions (RESOLVED)
- RESOLVED: `IffPayloadCursor.ReadFloatLe()` already exists; only a 1-byte `ReadInt8` helper is added (11-01).
- RESOLVED: SC3 demo fixture = a tangible with one bool/string local override; confirmed at smoke (11-05).
- RESOLVED: DERV-chain depth/cycle guard is a planned task + grep-gated verification in 11-02.

### Ready for Planning
Research complete. The CONTEXT.md decisions are all confirmed actionable against verified source; the planner can produce the 4–6 plans (model+writer, resolver+golden, host+entry-points+undo, mutations+widgets+hex-fallback, save+reload-badge, live-smoke + V1 release-gate).
