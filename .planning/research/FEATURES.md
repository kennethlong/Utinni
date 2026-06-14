# Feature Research

**Domain:** SWG client asset editors (Wave-2) — procedural Terrain `.trn`, effects-family `.iff`, plus two IFF/TRE quick-win surfaces — shipped inside The Jawa Toolbox as `IEditorPlugin` subpanels.
**Researched:** 2026-06-14
**Confidence:** HIGH (format facts grounded in the `swg-client-v2` `sharedTerrain` / `clientParticle` / `clientEffect` reference source + the original SOE `TerrainEditor`; product framing grounded in PROJECT.md, toolchain-inventory.md, ROADMAP backlog 999.2/999.3, and the SIE comparison). LOW only on relative modder-demand ranking *within* the effects family.

---

## Format ground-truth (so the scoping below is real, not guessed)

**`.trn` is NOT a heightmap.** A `.trn` is a serialized `TerrainGenerator` (top tag `TGEN`) — a *procedural recipe* the engine runs at chunk-load time to synthesize height, color, shader/texture, flora (static + radial/dynamic), and environment per tile. Its structure (from `sharedTerrain/.../generator/TerrainGenerator.h`):

- **Six shared "groups"** (palettes referenced by index): `ShaderGroup`, `FloraGroup`, `RadialGroup`, `EnvironmentGroup`, `FractalGroup`, `BitmapGroup`. These are the families a layer's rules point at.
- **A list of `Layer`s** (tag `LAYR`), each an ordered tree of four child-kinds, all subclasses of `LayerItem` (each carries `active`/`name`/`tag`):
  - **Boundaries** (`BCIR` circle, `BREC` rectangle, `BPOL` polygon, `BPLN` polyline, `BSPL` spline, `BALL`) — *where* the layer applies, with a feather function + distance.
  - **Filters** (`FHGT` height, `FSLP` slope, `FFRA` fractal, `FSHD` shader, `FDIR` direction, `FBIT` bitmap) — *conditions* gating the layer.
  - **Affectors** (~25 tags: `AHCN`/`AHTR`/`AHFR`/`AHBM` height-constant/terrace/fractal/bitmap; `ACCN`/`ACRH`/`ACRF`/`ACBM` color; `ASCN`/`ASRP`/`ASBM` shader; `AFSC`/`AFSN`/`AFDN`/`AFDF` flora static-collidable/static-noncollidable/dynamic-near/dynamic-far; `AENV` environment; `AEXC` exclude; `APAS` passable; `ARIV`/`ARIB`/`AROA` river/ribbon/road) — *what the layer does* to each affected map.
  - **Sub-layers** (recursion) — layers nest, forming the tree.
- Versioned chunks throughout (`load_0000..0004`, per-affector versions). The original SOE editor is a 100+ -file MFC app (`TerrainEditor/`) with a `FormBaseLayer`-derived property page **per affector/boundary/filter type** — that's the surface area, and it's the reason a full clone is NOT a v1.

**Effects family** (clientParticle / clientEffect):
- **ClientEffect `.cef`/`.iff`** (`ClientEffectTemplate`, versions `0001..0003`) — a flat **command list**: CreateAppearance, PlaySound, CreateLight, CameraShake, ForceFeedback. Simple, datatable-like; the easiest of the three.
- **Lightning** (`LightningAppearanceTemplate`) and **Swoosh** (`SwooshAppearanceTemplate`) — appearance templates living in the **same `clientParticle` library the Particle/`.prt` editor already shipped against** (v2.0 PROD-W2-PRT). Scalar/color/waveform-ish parameter blocks, not geometry authoring.

**Key reuse implication:** the v2.0 Particle editor already established the codec + panel pattern for `clientParticle` appearance templates. ClientEffect/Lightning/Swoosh are the *adjacent* members of that same family — which is exactly why "one adjacent effects editor" is the right second feature.

---

## Feature Landscape — A) Terrain Editor (`.trn`) — the headline

### Table Stakes (a `.trn` editor is broken without these)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Decode `.trn` → layer tree view** (TGEN → Layers → Boundaries/Filters/Affectors/Sub-layers, with names + active flags) | The whole point: a `.trn` is the layer tree. Hex is useless here. | HIGH | New `TerrainGenerator` codec over the existing **`IffReader`**. Versioned chunks per type — must handle `load_0000..0004` variants. This is the bulk of the work. |
| **Browse the six shared groups** (Shader/Flora/Radial/Environment/Fractal/Bitmap) as palettes | Affectors reference these by index; unreadable groups = unreadable rules | MEDIUM | Read-only display is enough for v1; each group is a flat list of named entries. |
| **Show each layer item's parameters** (read-only, typed per tag) | A height-constant affector showing "0x3F800000" is a non-starter; modders need "height = 1.0m" | HIGH | One typed view per tag family (~37 tags). The long pole. Mitigate by covering the **common** tags first (height/shader/color/flora affectors; circle/rect boundaries; height/slope filters) and degrading the rest to a generic field list. |
| **Open a `.trn` from the TRE Browser / loose override** | Consistency with every other Wave-1/2 editor | LOW | Reuses **`TreArchiveIndex` + `TrePayloadResolver`** exactly as the other editors do. |
| **Edit scalar/enum leaf parameters + save** (e.g. an affector's height, a feather distance, a flora density, active on/off) | "Editor" not "viewer"; the whole product promise is see+edit+save | MEDIUM-HIGH | Round-trips through **`MutableIffDocument` + `IffWriter`** + the four-tier D-05 save matrix (loose-override default), like IFF/OT editors. Scalar edits are tractable; tree-structure mutation is NOT (see deferred). |
| **Live-in-client preview of the edited terrain** | Utinni's locked differentiator: preview = the real SWG engine, never a standalone renderer | MEDIUM-HIGH | Save to loose override → trigger the client to reload/regenerate the planet's terrain. Honest-degrade if a full live regen isn't reachable this milestone (precedent: v2.0 Particle live-preview shipped degraded). NOT a Utinni-side terrain renderer — that violates the locked appearance-preview decision. |

### Differentiators (where Terrain beats the 2003 SOE tool / a standalone editor)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Live regenerate-in-client on save** | No SOE tool could edit a live planet; injection makes this uniquely Utinni | HIGH | The marquee differentiator *if* reachable. Gate behind feasibility; ship the save-tier even if live-regen degrades. |
| **2D top-down map / bitmap preview of generated output** (height or shader map) | The SOE editor's core view was a 2D sampled map; gives modders spatial feedback without running the game | HIGH | Requires porting the **`Sampler`** path (`SamplerProceduralTerrainAppearance`) to rasterize a region offline. High value, high cost — a strong **v1.x**, likely too big for v1. |
| **MCP/CLI verb to read+edit a `.trn`** (per DEC-V2-VERBS-FIRST) | Lets an AI agent inspect/tweak terrain rules; extends the v2.0 MCP surface | MEDIUM | Falls out naturally if the codec lands as a `utinni-cli` verb first (which the locked verbs-first discipline requires anyway). |
| **Layer/affector search + "what affects height here?" filtering** | Tarkin/Corellia `.trn`s have hundreds of layers; navigation is the real pain | MEDIUM | Tree filter over the decoded model; cheap once decode exists. |

### Anti-Features (tempting, wrong for v1 — or ever)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Standalone Utinni terrain renderer / 3D fly-through** | "I want to see the planet in the editor" | Violates the **locked** appearance-preview decision (live-in-client only); re-implements the engine's procedural generator + renderer = months; the exact trap SIE fell into | Live-in-client regen; optional offline 2D sampled map as the visual, not a 3D renderer |
| **Full tree authoring** (create/delete/reorder layers, add new affectors/boundaries from scratch, paint boundaries on a map) | "A real terrain editor lets me build a planet" | Each of ~37 item types needs a typed create-form + the boundary-painting UI is a whole sub-app; this is the 100-file MFC tool. Unbounded for v1 | v1 = inspect + edit existing leaves + toggle active; defer structural authoring to a later milestone |
| **Procedural-generation engine in C#** (reimplement affect/filter math to preview) | "Preview without the client" | Re-deriving the generator is a multi-month port and a correctness minefield; duplicates the engine Utinni already has live | Live-in-client (the engine itself) for fidelity; sampled-map (ported `Sampler`) only if offline preview is needed |
| **Editing baked `.ans`/heightmap data** | Confusion that terrain = heightmap | SWG terrain is procedural; there is no baked heightmap to edit | Educate via the layer-tree UI; the recipe IS the terrain |
| **Server-side terrain collision/pathing regen** | "My edits should update the server too" | Server-side is DEC-A1 (out of scope); `.trn` is shared but server regen is swg-main's job | Client-side preview only; note the server-publish step is external |

---

## Feature Landscape — B) Effects Editor (one adjacent Wave-2, ClientEffect / Lightning / Swoosh)

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Decode the chosen effect `.iff` into a typed editor** | Same see+edit promise as every other editor | LOW-MEDIUM | **ClientEffect is the cheapest target**: a flat command list (CreateAppearance/PlaySound/CreateLight/CameraShake/ForceFeedback), versions 0001-0003. Lightning/Swoosh are parameter blocks in the **already-touched `clientParticle` lib**. |
| **Edit + save effect parameters** (sound name, light RGB/attenuation, shake magnitude, appearance ref, scales/durations) | Editor not viewer | LOW-MEDIUM | Reuses `MutableIffDocument`/`IffWriter` + D-05 save matrix. ClientEffect fields are mostly scalars/strings — datatable-grade. |
| **Open from TRE / loose override** | Consistency | LOW | Reuses the TRE index surface. |
| **Reference validation** (does the named appearance/sound template exist in the load order?) | Dangling refs are the #1 effect bug | LOW-MEDIUM | Cross-check names against `TreArchiveIndex`; cheap and high-value. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Live-in-client play/trigger of the edited effect** | See the muzzle flash / lightning fire in the real engine after edit | MEDIUM-HIGH | Aligns with the locked live-preview model; degrade honestly like Particle did. |
| **Shared effects-family panel** (Particle + ClientEffect + Lightning + Swoosh under one codec pattern) | Compounds the v2.0 Particle work; one mental model for the whole `clientParticle` family | MEDIUM | Architectural payoff — pick the target that maximizes reuse of the shipped Particle codec/panel. |
| **MCP/CLI verb for the effect format** | Extends agent-drivable surface | LOW-MEDIUM | Verbs-first anyway. |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Particle-system geometry/curve authoring from scratch in the effects panel** | "Make new effects" | That's the Particle editor's domain (already shipped) + DCC territory for meshes | Edit existing templates; reference Blender-authored appearances by name |
| **Doing all three (ClientEffect + Lightning + Swoosh) in this milestone** | "Finish the family" | Triples the surface; milestone scope is explicitly *one* adjacent editor | Ship one (recommend ClientEffect — cheapest, broadest use); fast-follow the other two |

---

## Feature Landscape — C) Quick Win: User-definable IFF chunk templates (Backlog 999.2)

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Define a chunk schema** (ordered fields of primitives: int8/16/32, float, bool, string) | The minimum that turns "any chunk" into "a readable struct" | MEDIUM | New schema model + a schema-driven decode pass over **`IffPayloadCursor`**. The reader already exists; this is the decode/encode layer + a definition UI. |
| **Auto-decode matching chunks** (apply schema by chunk tag) | The payoff: open an unknown `.iff`, see fields not hex | MEDIUM | Match on tag; fall back to hex when no schema (current behavior preserved). |
| **Edit + save through the schema** | Editor not viewer | MEDIUM | Encode pass must round-trip byte-exact; this is the correctness risk. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Rich field types** (color/PackedRgb, Vector, Quaternion, Matrix, fixed + count-prefixed arrays, nested structs) | This is SIE's standout power feature; covers most real SWG chunk shapes | MEDIUM-HIGH | The differentiator vs a flat primitive list. Arrays/structs add real parser complexity. |
| **Schema-derivable-by-MCP** | An agent can author a schema and read/edit an unknown chunk | MEDIUM | Falls out of verbs-first: schema decode as a CLI verb the MCP layer dispatches. |
| **Shareable schema library** (save/load schema files) | Modders crowdsource format knowledge | LOW | Just (de)serialize the schema model. |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Turing-complete / conditional schemas** (if field A == x then layout B) | "Real formats branch on a version byte" | A schema DSL with control flow is a parser-generator project; unbounded | Tag+version-keyed schemas (pick schema by chunk version), not in-schema branching |
| **Auto-infer schema from bytes** | "Guess the format for me" | Inference is unreliable; produces confidently-wrong layouts | Human-authored schemas; optional heuristics as *hints* only |

---

## Feature Landscape — D) Quick Win: TRE override / version-history view (Backlog 999.3)

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Show every version of a logical path across the load order** (which `.tre`/`.toc` provides it, in priority order) | The exact modder pain: "which archive is actually winning?" | LOW-MEDIUM | **`TreArchiveIndex` already resolves logical paths across the load order** + `CotMasterIndex` for COT. The new piece is *exposing the full resolution chain* instead of just the winner. |
| **Open/extract any historical version** (not just the winning one) | The point of a history view | LOW | `TrePayloadResolver` already fetches a record's bytes; parameterize by source archive. |
| **Indicate the winner** (which version the client actually loads) | Without this it's just a list | LOW | The index already computes the winner; surface it. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Diff base vs override** (binary, and structured for known formats) | The natural payoff of seeing two versions | MEDIUM | Binary diff is cheap; structured diff (IFF-aware) reuses the IFF reader and is higher-value. |
| **"Open in the right editor" from any version** | One click from history to the IFF/datatable/OT editor | LOW | Dispatch to the existing editor subpanels by extension. |
| **Load-order doctor** (flag shadowed-by-loose-override, duplicate paths, encrypted-payload v6000+ enumerate-only) | Surfaces the exact class of bug from the 06-12 phantom-walk memory | MEDIUM | Composes the index + TreVersion; high diagnostic value for this codebase specifically. |

### Anti-Features

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Edit/repack across the whole archive set** | "Fix the override here" | Cross-archive repack is heavy + the repack path has known latent bugs (compressed-bytes) | History view is read/extract/diff; edits go through loose override (the existing write model) |
| **Decrypt v6000+ retail payloads to diff content** | "Diff Restoration archives" | v6000+ payloads are encrypted → enumerate-only (locked reality) | Show metadata/presence diff; mark payload encrypted, don't pretend to decode |

---

## Feature Dependencies

```
[Terrain v1: layer-tree decode] ──requires──> [existing IffReader / IffPayloadCursor]
        └──requires──> [TreArchiveIndex + TrePayloadResolver]  (open from TRE/override)
        └──enables───> [Terrain edit+save] ──requires──> [MutableIffDocument + IffWriter + D-05 save matrix]
                              └──enables──> [Terrain live-in-client regen preview]  (degradable)
        └──enables───> [Terrain CLI/MCP verb]   (verbs-first)
        └──enables───> [2D sampled-map preview] ──requires──> [ported Sampler*]  (v1.x — heavy)

[Effects editor] ──reuses──> [v2.0 Particle codec/panel pattern (clientParticle family)]
        └──requires──> [IffReader/Writer + TRE index]  (same base as Terrain)
        └──enables───> [live-in-client effect trigger]  (degradable)

[IFF chunk templates] ──requires──> [IffPayloadCursor]  +  [new schema model + decode/encode pass]
        └──enhances──> [every editor]  (unknown chunks become readable)

[TRE history view] ──requires──> [TreArchiveIndex (already resolves load order) + CotMasterIndex + TrePayloadResolver]
        └──enhances──> [IFF chunk templates]  (diff any version with a user schema)

[D3D11 render-path foundation]  &  [v145/CppSharp bump]  ──underpin──> [all live-preview features]
        (foundation-before-features: land these before the live-preview editors lean on them)
```

### Dependency Notes

- **Terrain decode is the critical path** — every other Terrain feature (edit, save, preview, CLI, search) sits on the `TerrainGenerator` codec. Get decode right first; it's the long pole and the milestone risk.
- **Effects reuses Particle** — the second editor is deliberately *adjacent* to the shipped Particle/`.prt` work; choosing ClientEffect (or a Lightning/Swoosh that maximally reuses the `clientParticle` codec) keeps cost low. This is the cheap second feature, not a second long pole.
- **Both quick wins compose on existing surfaces** — 999.2 on `IffPayloadCursor`, 999.3 on `TreArchiveIndex`. They are genuinely "quick" *relative to* Terrain, but 999.2's byte-exact encode round-trip is the hidden risk (same class as the v2.0 canonical-writer / golden-vector work).
- **Live preview depends on the hardened base** — per PROJECT.md's foundation-before-features strategy, the D3D11 path + v145 bump should land before the live-preview editors rely on them; otherwise preview breaks the moment the client flips to D3D11.

---

## MVP Definition

### Launch With (v2.1)

- [ ] **Terrain: decode `.trn` → navigable layer tree** (TGEN → Layers → Boundaries/Filters/Affectors/Sub-layers, names + active flags, six shared groups read-only) — the headline; without decode there is no editor.
- [ ] **Terrain: typed read view for the common item types** (height/shader/color/flora affectors; circle/rect boundaries; height/slope filters), generic field-list fallback for the rest — usable, not exhaustive.
- [ ] **Terrain: edit + save scalar/enum leaf params + active toggle** through loose override (D-05) — earns "editor."
- [ ] **Terrain: open from TRE Browser / loose override** — table-stakes consistency.
- [ ] **One effects editor (recommend ClientEffect)**: decode + edit + save the command list, reference validation, open from TRE — the adjacent Wave-2 feature.
- [ ] **(Foundation) D3D11 render-path + v145/CppSharp bump** — enabling debt landed first so live preview survives the client's D3D9→D3D11 flip.

### Add After Validation (v1.x / fast-follow)

- [ ] **Terrain live-in-client regen on save** — promote from degraded once the reload path is proven (mirrors the Particle live-preview trajectory).
- [ ] **Terrain 2D sampled-map preview** (ported `Sampler`) — the offline visual; high value, too big for v1.
- [ ] **Terrain: typed coverage for the long-tail affector tags** (river/road/ribbon/environment/exclude/passable).
- [ ] **Second + third effects editors** (Lightning, Swoosh) — finish the `clientParticle` family.
- [ ] **IFF chunk templates (999.2)** — schema model + rich field types; gate on the byte-exact encode round-trip.
- [ ] **TRE history view (999.3)** — resolution chain + extract + diff + load-order doctor.

### Future Consideration (v2.2+)

- [ ] **Terrain structural authoring** (create/delete/reorder layers, add items, paint boundaries on a map) — the full SOE-editor surface; a milestone of its own.
- [ ] **Remaining Wave-2 editors** (Animation, Shaders/Textures, Sound, UI) — per toolchain-inventory priority order.

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Terrain: `.trn` layer-tree decode + view | HIGH | HIGH | P1 |
| Terrain: typed common-tag params (read) | HIGH | HIGH | P1 |
| Terrain: edit+save scalar leaves + active toggle | HIGH | MEDIUM | P1 |
| Terrain: open from TRE/override | MEDIUM | LOW | P1 |
| Effects (ClientEffect) decode+edit+save+ref-check | HIGH | LOW-MEDIUM | P1 |
| Foundation: D3D11 path + v145/CppSharp bump | HIGH (enabler) | HIGH | P1 |
| Terrain: live-in-client regen on save | HIGH | HIGH | P2 |
| TRE history view (resolution chain + extract + diff) | HIGH | LOW-MEDIUM | P2 |
| IFF chunk templates (schema decode/edit) | HIGH | MEDIUM-HIGH | P2 |
| Terrain: 2D sampled-map preview | MEDIUM-HIGH | HIGH | P3 |
| Effects: Lightning + Swoosh | MEDIUM | MEDIUM | P3 |
| Terrain: long-tail affector typed coverage | MEDIUM | MEDIUM | P3 |
| Terrain: structural authoring / boundary painting | MEDIUM | VERY HIGH | P3 (later milestone) |

**Priority key:** P1 = must have for v2.1 launch · P2 = should have, add when possible · P3 = nice to have / future.

---

## Competitor Feature Analysis

| Feature | SOE `TerrainEditor` (2003 MFC) | Sytner's IFF Editor (SIE) | Utinni v2.1 approach |
|---------|-------------------------------|---------------------------|----------------------|
| `.trn` layer-tree edit | Full authoring (create/edit/reorder, per-type forms, boundary painting) — 100+ files | None (general IFF editor) | v1: inspect + edit leaves + toggle; defer structural authoring |
| Terrain visual preview | 2D sampled maps + 3D viewport (own renderer) | None | **Live-in-client via the real engine** (locked differentiator); optional 2D sampled-map as v1.x — never a standalone 3D renderer |
| Effects editing | Separate ClientEffect/Lightning/Swoosh editors | None | One adjacent editor reusing the shipped Particle/`clientParticle` codec |
| User-defined chunk templates | N/A | **Standout feature** (auto-applied schemas) | Match + extend (rich field types, MCP-derivable) — 999.2 |
| TRE override/version history | `TreeFileExtractor` (single archive) | **Repository view: show/extract any version in override history** | Match + extend (winner-marking, diff, load-order doctor) — 999.3 |
| Live, injected editing | No (offline tools) | No (standalone, own renderer) | **Yes — the structural differentiator a standalone editor can't match** |

---

## Sources

- `D:/Code/swg-client-v2/src/engine/shared/library/sharedTerrain/.../generator/TerrainGenerator.h`, `TerrainGeneratorType.h`, `Affector*.h`, `Boundary.h`, `Filter.h` — `.trn` procedural structure + tag set (HIGH).
- `D:/Code/swg-client-v2/src/engine/client/application/TerrainEditor/` (`How To.txt`, `Form*`, `*View` files) — original SOE editor surface area = the "don't clone this as v1" baseline (HIGH).
- `D:/Code/swg-client-v2/src/engine/client/library/clientGame/.../clientEffect/ClientEffectTemplate.h` — ClientEffect command-list format (HIGH); `clientParticle/.../LightningAppearanceTemplate.h`, `SwooshAppearanceTemplate.h` — effects-family appearance templates in the already-shipped Particle library (HIGH).
- `D:/Code/Utinni/UtinniCoreDotNet/Formats/` — existing surfaces this builds on: `Iff/IffReader.cs`, `IffWriter.cs`, `MutableIffDocument.cs`, `Decoders/IffPayloadCursor.cs`, `IffStructureSummary.cs`; `Tre/TreArchiveIndex.cs`, `TrePayloadResolver.cs`, `CotMasterIndex.cs`, `TreVersion.cs`; `Particle/*` (the codec/panel precedent) (HIGH).
- `.planning/PROJECT.md` (Core Value, anti-goals DEC-A1..A4, v2.1 milestone goal/strategy), `docs/ai/toolchain-inventory.md` (Wave-2 priority order + locked live-in-client appearance-preview decision + SIE comparison), `.planning/ROADMAP.md` (Backlog 999.2 / 999.3 / 999.4 / 999.5 context) (HIGH).

---
*Feature research for: SWG Wave-2 asset editors (Terrain `.trn` + one effects editor) + IFF/TRE quick wins*
*Researched: 2026-06-14*
