# Feature Research

**Domain:** AI-drivable SWG asset-modding pipeline (MCP server + revived compile CLIs + DCC-style editors) on top of Utinni V1's byte-exact codecs
**Researched:** 2026-06-01
**Confidence:** HIGH for MCP model + safety (Context7 / MCP spec 2025-11-25 + multiple security sources); HIGH for compiler semantics (swg-client-v2 source census, on-disk); MEDIUM for editor UX expectations (training data + SWG tool census, no Context7 anchor); HIGH for the Blender boundary (swg-client-v2 `blender-mcp-vs-addon.md`, on-disk precedent).

> **Scope note.** This milestone adds to shipped Utinni V1. The five Wave-1 editors, `UtinniCoreDotNet` byte-exact codecs (TRE/IFF/datatable/stf/object-template), and the `Utinni.Cli` JSON verbs already exist and are NOT re-researched here. V2.0 = (1) MCP server, (2) revive+wrap SWG compile CLIs, (3) new DCC-style editors, (4) formalize the Utinni ↔ swg-blender boundary. Theme: *Utinni authors, not just edits.*

> **Existing Utinni capabilities this milestone builds on** (dependency anchors):
> - `Utinni.Cli` verbs (9): `parse-tre`, `list-objects`, `inspect-iff`, `decode-iff`, `roundtrip-iff`, `roundtrip-tab`, `roundtrip-stf`, `roundtrip-ot`, `validate-plugin` — stable sorted-key JSON envelopes (`schemaVersion`/`command`). **Read + roundtrip only; no save-to-archive verb exists yet.**
> - `UtinniCoreDotNet` editing/saving layer: `IffWriter`/`TreWriter` byte-exact serializers, `MutableIffDocument`, `IffEditController` (+ undo/redo), and the four-tier D-05 save matrix: **loose-override file**, **Save/Save-As**, **in-memory live-patch** (infra-ready, user-disabled), **`.tre` archive repack**. Safety primitives already shipped: `LooseOverridePath`, `TreBackupPath`, `TreRepackLock`, `TreRecordIndexResolver`, `LivePatchValidator`, `ReloadAssetClassifier`, path-traversal canonicalization (WR-01..07).
> - Byte-exact roundtrip goldens under CI for all five formats.

---

## Part A — MCP Server ("AI-drivable modding pipeline")

### The MCP model (grounding, HIGH confidence — MCP spec 2025-11-25)

An MCP server exposes three primitive kinds over JSON-RPC 2.0:

| Primitive | What it is | Who drives it | Utinni use |
|-----------|------------|---------------|------------|
| **Tools** | Model-callable functions with `inputSchema` (+ optional `outputSchema`) JSON Schema; return `content[]` + optional `structuredContent` + `isError`. | Model decides when to call (model-controlled). | read/edit/save/compile/pack/validate verbs |
| **Resources** | Read-only addressable data (URI-identified), listable + readable. | App/user-controlled; provide context. | browse a `.tre` TOC, fetch an asset's decoded JSON, expose the param→type schema |
| **Prompts** | Server-authored parameterized prompt templates. | User-controlled (e.g. slash-commands). | canned workflows: "compile this template and repack into a test .tre" |

- **Discovery:** client calls `tools/list`, `resources/list`, `prompts/list`; each tool advertises name, `title`, `description`, `inputSchema`, optional `outputSchema`, and `annotations`.
- **Transport:** **stdio is the correct choice for a local tool** — the server is a child process spoken to over stdin/stdout framed JSON-RPC; no network surface, no auth complexity, runs on the modder's machine next to the game files. (Streamable HTTP exists but is for remote/multi-client servers — not this use case.)
- **Structured results:** define `outputSchema` so the agent gets validated `structuredContent` (e.g. a save result `{ written: bool, path, bytesWritten, backupPath }`) instead of having to parse prose. Utinni's existing sorted-key JSON envelopes map almost 1:1 onto `structuredContent`.

### MCP WRITE-TOOL SAFETY MODEL (the key differentiator — detailed)

The risk: *an agent silently corrupts a game archive.* A `.tre` is a packed binary that the live client loads; a bad write can brick a modder's client install or destroy hand-authored assets. The mitigation is layered — **the MCP spec's own hints are necessary but NOT sufficient**, and Utinni's existing V1 primitives close the gap.

**Layer 1 — Tool annotations (advisory, for the client UI).** Each tool declares `ToolAnnotations`:
- `readOnlyHint: true` on `parse-tre`, `inspect-iff`, `decode-iff`, `list-objects`, `validate-*`, resource reads → client MAY auto-approve.
- `readOnlyHint: false` + `destructiveHint: true` on archive-repack / overwrite tools → client SHOULD prompt.
- `destructiveHint: false` on additive ops (loose-override write — it creates an override file, doesn't mutate the source archive).
- `idempotentHint: true` on deterministic compiles (`.tpf` → `.iff` is a pure transform).
- **Critical caveat (MCP spec, verbatim):** *"all properties in ToolAnnotations are hints and should not be used for critical decision-making when received from untrusted servers."* So annotations drive UX, not enforcement. Real safety is server-side (Layers 2-5).

**Layer 2 — Human-in-the-loop via MCP elicitation.** For any destructive write, the server issues an `elicitation/create` request (`form` mode, with a `requestedSchema` for the confirmation) describing the *concrete* outcome — "repack `objects.tre`, replacing record `object/tangible/foo.iff` (+412 bytes), backup → `objects.tre.bak`" — and the user accepts / declines / cancels before the byte is written. **Fail-closed:** if the connected client doesn't support elicitation, destructive tools refuse rather than silently proceeding. (Community consensus across multiple security writeups; aligns with MCP spec's elicitation primitive.)

**Layer 3 — Default to non-destructive surfaces (Utinni's structural advantage).** Utinni already ships the **loose-override** save tier: writes go to a parallel override file the client search-path picks up, leaving the source `.tre` untouched. The MCP server should make loose-override the **default write path** and treat archive-repack (in-place mutation) as the explicitly-confirmed, rarely-needed escalation. This is a bigger safety lever than any annotation: most agent edits never touch the original archive.

**Layer 4 — Verify-before-commit (Utinni's byte-exact moat).** Every write tool runs the existing roundtrip/validate path before returning success: re-decode the written bytes, structurally validate the IFF/datatable/stf, and (for repack) confirm the archive TOC resolves. `LivePatchValidator` bounds-checks live patches today; the same discipline applies to file/archive writes. Return `isError: true` + the validation failure rather than reporting a corrupt success.

**Layer 5 — Recoverability.** `TreBackupPath` already produces a `.bak` before repack; `TreRepackLock` serializes concurrent repacks. The MCP save-result `structuredContent` surfaces `backupPath` so an agent (or human) can roll back. Path-traversal canonicalization (WR-01..07, already shipped) prevents an agent from writing outside the mod root.

> **Net:** the write-safety story is *defense-in-depth* — advisory hints for the client UI, in-band elicitation for human confirmation, loose-override-by-default to avoid touching source archives, byte-exact re-validation before reporting success, and backups for recovery. Utinni can credibly claim "an agent cannot silently corrupt a game archive" because four of the five layers are already-shipped V1 primitives, not new code.

### Feature Landscape — MCP

#### Table Stakes (an "AI-drivable modding pipeline" is incomplete without these)

| Feature | Why Expected | Complexity | Notes / Dependency |
|---------|--------------|------------|--------------------|
| stdio MCP server skeleton (init, `tools/list`, `tools/call`) | The baseline of any local MCP server | LOW | Thin host process; .NET MCP SDK or hand-rolled JSON-RPC |
| Read tools wrapping the 9 `Utinni.Cli` verbs | Agents must *see* assets before editing | LOW | Direct shim over existing CLI/`UtinniCoreDotNet`; envelopes → `structuredContent` |
| Resources for TRE TOC + decoded-asset fetch | Standard MCP context surface; lets the agent browse without burning tool calls | LOW-MED | Backed by `parse-tre` / `decode-iff` |
| JSON-Schema'd inputs for every tool | Agents can't reliably call un-schema'd tools | LOW | Schemas mirror existing CLI options |
| `readOnlyHint`/`destructiveHint`/`idempotentHint` annotations | Lets clients auto-approve reads, gate writes | LOW | Per Layer 1 above |
| Write tools (loose-override default) | "Authors, not just edits" requires writing | MED | Wraps existing save tiers; **needs a save verb the CLI lacks today** |
| Elicitation-gated destructive writes | Industry-standard guardrail; the corruption defense | MED | Per Layer 2; fail-closed if unsupported |
| Verify-before-commit on writes | Trust requires it; cheap given roundtrip goldens | LOW-MED | Reuses roundtrip/validate path |
| Structured save/compile results (`outputSchema`) | Agents act on results programmatically | LOW | `{written,path,bytesWritten,backupPath,validated}` |

#### Differentiators (set Utinni's MCP apart)

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Byte-exact verify-before-commit as a first-class guarantee** | "The agent cannot return a corrupt success" — most MCP servers can't claim this | LOW (infra exists) | The V1 roundtrip goldens become a runtime safety check |
| **Loose-override-by-default write model** | Agent edits never touch source archives unless explicitly escalated | LOW (tier exists) | Structural, not bolted-on, safety |
| Compile/pack tools (`.tpf`→`.iff`, source→`.tre`) exposed as MCP tools | Agent can drive the *full* author→build→pack pipeline, not just edit | MED-HIGH | Depends on Part B revive landing |
| Prompts for canned pipelines ("edit → compile → repack → validate") | One-call multi-step workflows; lowers agent error rate | LOW-MED | Server-authored prompt templates |
| Live-preview tool (inject edit into running client) | Agent edits and *sees the result in-game* — unique to Utinni's injection model | HIGH | Live-patch tier is infra-ready but user-disabled; gated, opt-in |
| `validate-plugin` / structural-validate as standalone agent tool | Agent self-checks its own output | LOW | Verb exists |

#### Anti-Features — MCP

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| HTTP/SSE remote transport | "Make it accessible / multi-user" | Adds auth, network attack surface, deployment burden for a local single-user modding tool | stdio only; revisit only if a hosted scenario appears |
| Auto-approve all writes for "agent autonomy" | Faster agent loops | Defeats the entire corruption-safety model | Auto-approve reads; always gate destructive writes |
| A raw "run arbitrary CLI / exec" tool | Maximum flexibility | Unbounded blast radius; the agent can do anything to the filesystem | Curated, schema'd tools only — each maps to a known safe operation |
| Trusting `destructiveHint` for enforcement | "The spec has a flag for it" | Spec explicitly says hints are advisory, untrusted | Enforce server-side (elicitation + validate + backup) |
| Exposing in-place archive repack as the default write | "It's what they edited" | Highest-corruption-risk path as the path of least resistance | Loose-override default; repack is explicit escalation |
| MCP tools for 3D mesh/skeleton/anim authoring | "Complete the pipeline" | That's the Blender suite's lane (DEC-A3) | See Part D boundary |

---

## Part B — SWG Asset Compile CLIs (revive + wrap)

### What each compiler actually does (grounding, HIGH — swg-client-v2 source census, on-disk)

There are **two distinct template compilers**, frequently conflated; the distinction matters for OT Tier-2:

| Tool | Input → Output | What it produces | Audience (historical) |
|------|----------------|------------------|----------------------|
| **`TemplateDefinitionCompiler`** | `.tdf`/`.tpd` template *definition* → generated C++ template classes **+ the per-class param→type schema** | The **schema** ("for class `tangible`, param `volume` is an int, `scale` is a float…") | engine/gameplay engineers (build-time) |
| **`TemplateCompiler`** | `.tpf` template *instance* source (+ the definitions above) → object-template **`.iff`** | The actual binary object template the client loads; can also generate a default `.tpf` from a `.tdf` | designers/engineers |
| **`DataTableTool`** (compile path) | `.tab`/XML spreadsheet → datatable **`.iff`** | Binary datatable (Utinni edits existing ones; CSV import exists, full compile doesn't) | designers, build engineers |
| **`TreeFileBuilder` / `TreeFileRspBuilder`** | source tree (+ `.rsp` response/manifest) → **`.tre`** archive | A `.tre` **built from a directory of loose assets** (Utinni only *repacks* an existing archive today) | build engineers |
| **Exporters** (`ArmorExporterTool`, `WeaponExporterTool`, `CoreWeaponExporterTool`, `SwgSchematicXmlParser`) | schematic datatable `.iff` / XML → server+shared `.tpf` (then compile templates) | Generated `.tpf` instances for tangibles/schematics, then their `.iff` | systems designers |

**Why this unblocks OT Tier-2 (key dependency):** the Object Template Editor's Tier-2 typed list-param display needs to know *what type each param is per class*. That map is exactly what **`TemplateDefinitionCompiler`** emits when it processes the `.tdf` definitions. Reviving it is the cheapest route to OT Tier-2 — confirmed in PROJECT.md and toolchain-inventory.md.

**Revive blockers (HIGH — source census):**
- `TemplateCompiler`/`TemplateDefinitionCompiler` link the **Perforce C++ API** and use **PCRE 4.1**; `MayaExporter` adds **Alienbrain**. The Perforce/Alienbrain submit/check-out paths are source-control integration, **not** the compile logic — they must be **decoupled/stubbed** so the compile/use path builds without the legacy asset-DB SDKs.
- `TreeFileBuilder`/`DataTableTool` link **zlib** (straightforward) and **libxml2 2.6.7** (old, vendored).
- All revives are subject to the **lift-and-shift + v143→v145 toolchain port** constraint (locked in PROJECT.md) — copy source into a Utinni-owned build location, borrow swg-client-v2's SOE-source modernization, port the v143→v145 delta. Watch for modern-STL friction (cf. CppSharp clang-11 pin). **Feasibility of this port is a separate research spike — see STACK/feasibility, not assumed here.**

### Feature Landscape — Compile pipeline

#### Table Stakes

| Feature | Why Expected | Complexity | Notes / Dependency |
|---------|--------------|------------|--------------------|
| `.tpf` → object-template `.iff` compile | The core author-new-template gap; Utinni edits but can't compile | MED (revive) + LOW (wrap) | `TemplateCompiler`; gated on lift-and-shift port |
| `.tpd`/`.tdf` → param→type schema | Unblocks OT Tier-2 typed display | MED (revive) | `TemplateDefinitionCompiler`; **dependency of the OT Tier-2 residual** |
| source-tree → `.tre` build | Repack ≠ build; authoring new content needs build-from-source | MED (revive) | `TreeFileBuilder` (+ `.rsp` manifest understanding) |
| Datatable compile (XML/CSV → `.iff`) | Round out the datatable editor's author story | MED | `DataTableTool` compile path |
| Decouple Perforce/Alienbrain from compile path | The tools won't build with the legacy asset-DB SDKs | MED | Stub the source-control integration; keep transform logic |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Compilers wired as MCP write/build tools | Agent drives author→compile→pack end-to-end | LOW (once revived) | The revive's payoff; per Part A |
| Param→type schema surfaced as an MCP **resource** | Agent (and OT editor) get typed template knowledge | LOW | Reuses `TemplateDefinitionCompiler` output |
| Item-exporter wrappers (armor/weapon/schematic) | One-call "generate the `.tpf` for this weapon row" | MED | Niche but high-leverage for content modders |

#### Anti-Features — Compile pipeline

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Reimplement the template compiler from scratch in C# | "Byte-exact, no legacy deps" | Large port; the `.tpf`/`.tdf` grammar + per-class schema is substantial; revive+wrap is the locked strategy | Revive+wrap the original `.exe`; reimplement later only where live-editing/byte-exact round-trip demands |
| Revive the Perforce/Alienbrain submit paths | "Full original behavior" | Dead SDKs, no modern relevance, hardest legacy dep | Stub them; modders use git/filesystem |
| Build in-place against the swg-client-v2 tree | "Reuse their build" | Couples Utinni to their active D3D9→D3D11 churn (lift-and-shift constraint, locked) | Copy source into Utinni-owned build location |
| Revive `MayaExporter` | "We need an exporter" | Maya7+Alienbrain, unbuildable; superseded | swg-blender-plugin owns export (Part D) |

---

## Part C — DCC-style Editors (replace)

### UX expectations (grounding, MEDIUM — SWG tool census + general DCC-editor conventions)

The litmus from toolchain-inventory.md: *interactive editor → replace with a themed, undo/redo, live-injectable Utinni SubPanel*; the original 2003 MFC/Qt editors are not worth reviving. The three V2.0 targets:

| Editor | Format | What modders expect (table-stakes UX) | Differentiator only Utinni can offer |
|--------|--------|----------------------------------------|--------------------------------------|
| **Terrain** | `.trn` | Layer/shader-rule tree, height/fractal/filter affectors, flora/radial rules, a 2D map view; edit a rule and see the heightmap region change | **Live in-client preview** of the terrain edit via injection |
| **Particle / client-effect** | `.prt`, effect `.iff` | Emitter list, curve/ramp editors (size/color/alpha over age), texture ref, timeline scrub, a preview pane | **Preview the effect in the running client**, not a mock viewport |
| **WorldSnapshot / object-placement** | snapshot `.iff` | Object list, transform gizmo (move/rotate/scale), parent/cell nesting, add/remove objects, save snapshot — *extend the existing Snapshot save panel into a full viewer+editor* | Already injection-native (Utinni's origin is the Jawa world-snapshot editor); gizmo editing in the live scene |

### Feature Landscape — Editors

#### Table Stakes (per editor)

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Themed WinForms SubPanel inside TJT | Consistency with the five Wave-1 editors (DEC-C4 pattern) | LOW-MED | `IEditorPlugin` subpanel; nested SplitContainers (per WinForms Dock.Fill memory) |
| Undo/redo | Every Wave-1 editor has it; modders expect it | MED | Mirror `IffEditController` command pattern |
| Open / Save / Save-As + loose-override | The shipped four-tier save matrix | MED | Reuse `UtinniCoreDotNet` saving layer |
| Read the format via existing/extended codecs | Can't edit what you can't parse | MED-HIGH | `.trn`/`.prt`/snapshot codecs — **new codec work in `UtinniCoreDotNet`** |
| WorldSnapshot: transform gizmo | Object placement is inherently spatial | MED | Utinni already has gizmo editing (its origin) |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Live in-client preview** (terrain/particle/snapshot) | See edits in the running game, no rebuild/repack loop — Utinni's core moat | HIGH | Live-patch/reload tier; per-format reload classifier |
| Terrain 2D map + affector visualization | Beats a flat property grid | HIGH | Custom rendering |
| Particle curve/timeline editors | Effects are time-based; grids are painful | MED-HIGH | Custom curve control |
| MCP tools mirroring each editor's read/edit/save | Agent can drive terrain/particle/placement edits too | LOW (once codec + save exist) | Reuses Part A model |

#### Anti-Features — Editors

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **3D mesh / skeleton / animation authoring** | "Editors should do everything" | **DEC-A3 anti-goal (LOCKED); Blender's lane** | Open/preview Blender exports only (Part D) |
| Full sculpt-brush terrain like a modern DCC | "Make it like World Machine" | Massive scope; `.trn` is rule-based (affectors), not a heightmap canvas | Edit the affector/shader rules; preview the result |
| Revive the original MFC/Qt editors | "They already exist" | 2003 toolkits, unmaintainable, not injection-native | Replace with Utinni SubPanels (locked strategy) |
| Texture / shader authoring inside the particle editor | "Effects need textures" | DCC/texture-tool lane; scope creep | Reference existing `.dds`/`.sht`; author textures elsewhere |
| Animation authoring in the snapshot editor | "Place + animate" | Blender owns skeletal/anim (DEC-A3) | Animation *live-in-client preview* only, coordinated with Blender suite |

---

## Part D — Utinni ↔ swg-blender-plugin Boundary (formalize)

**The boundary (HIGH — swg-client-v2 `blender-mcp-vs-addon.md` + `maya-exporter-reference.md`, on-disk; DEC-A3 LOCKED):**

| Owns | Utinni | swg-blender-plugin |
|------|--------|--------------------|
| Domain | binary/format read·edit·save + **live in-client** preview/injection; TRE/IFF/datatable/stf/object-template; compile/pack CLIs | DCC authoring: mesh/skeleton/animation/shader **export** (`.msh`/`.mgn`/`.skt`/`.lod`/`.pob`/`.sat`/`.apt`/`.lmg`/`.ans`) |
| Interface | MCP server + WinForms SubPanels | Blender MCP (`execute_blender_code`) + thin Python addon + `swg_iff`/`swg_blender` shared libs |
| Meet point | **shared file formats** (`.iff`/`.tre`) — Utinni opens/previews what Blender exports |

**Formalization features:**

| Feature | Category | Complexity | Notes |
|---------|----------|------------|-------|
| Utinni opens/previews Blender-exported `.iff` (mesh/skeleton/anim) | Table stakes | MED | Read + in-client preview; no authoring |
| Documented format-version contract between the two suites | Table stakes | LOW | Both build on the same `.iff`/`.tre` understanding; avoid drift |
| Animation live-in-client preview coordinated with Blender | Differentiator | HIGH | Utinni previews; Blender authors |
| `TreeFileBuilder` (Utinni-revived) as the pack step Blender shells out to | Differentiator | LOW (once revived) | swg-blender's `rsp_builder.py` reimplements the `.rsp` format; Utinni's revived builder is the C++ ground-truth alternative |

**Anti-feature (the load-bearing boundary):** Utinni must **NOT** own 3D mesh/skeleton/animation/texture authoring (DEC-A3). The precedent doc's rule — *"put format knowledge in the shared lib; MCP and addon are thin shells"* and *"never target FBX/glTF as final format, always IFF"* — applies symmetrically: Utinni stays the format+live-injection tool; Blender stays the DCC. The in-client **Viewer is ground truth**, which is Utinni's natural contribution to the Blender export loop.

---

## Feature Dependencies

```
TemplateDefinitionCompiler (revive)
    └──produces──> param→type schema
                       └──unblocks──> OT Tier-2 typed list-param display (carried residual)
                       └──feeds──────> MCP resource: template schema

TemplateCompiler (revive) ──requires──> TemplateDefinitionCompiler output
    └──produces──> .tpf → object-template .iff
                       └──exposed-as──> MCP compile/write tool

lift-and-shift + v143→v145 toolchain port  ──gates──>  ALL Part B revives
    (separate feasibility spike — STACK.md / FEASIBILITY)

Utinni.Cli verbs (exist) ──wrapped-by──> MCP read tools (low cost)
UtinniCoreDotNet save tiers (exist) ──wrapped-by──> MCP write tools
    └──requires──> a NEW save/compile CLI verb (CLI is read+roundtrip today)

MCP write tools ──require──> elicitation gate + verify-before-commit + backup
    (Layers 2/4/5 — Layers 4/5 reuse shipped V1 primitives)

New editors (terrain/particle/snapshot)
    └──require──> NEW format codecs in UtinniCoreDotNet (.trn/.prt/snapshot)
    └──reuse─────> IffEditController pattern, four-tier save matrix
    └──enhanced-by──> live-patch/reload tier (HIGH complexity)

Blender boundary ──requires──> Utinni read+preview of Blender .iff exports
    └──enhanced-by──> TreeFileBuilder revive (shared pack step)
```

### Dependency Notes

- **OT Tier-2 depends on `TemplateDefinitionCompiler`:** the typed param map is the definition compiler's output — reviving it is the cheapest path to closing the carried residual.
- **MCP write tools need a CLI save verb:** `Utinni.Cli` is read+roundtrip today; the write surface lives in `UtinniCoreDotNet`'s editing/saving layer driven by the editors. The MCP server can call `UtinniCoreDotNet` directly OR a new CLI save verb — the latter keeps the "thin shim over CLI" architecture consistent. **This gap is the single biggest net-new code item for Part A.**
- **All Part B revives are gated on the toolchain port** being feasible — do not assume; it's the first concrete milestone-research spike (PROJECT.md).
- **New editors need new codecs** (`.trn`/`.prt`/snapshot) — the editors are downstream of `UtinniCoreDotNet` codec work, mirroring how Wave-1 editors sat on TRE/IFF/datatable/stf/OT codecs.

---

## MVP Definition

### Launch With (v2.0 core — validates "Utinni authors, not just edits")

- [ ] **MCP server skeleton (stdio) + read tools** over the 9 existing CLI verbs — lowest cost, immediate "agent can see SWG assets" value.
- [ ] **MCP write tools (loose-override default) with the full safety model** — annotations + elicitation gate + verify-before-commit + backup. *The centerpiece differentiator.* Needs a save verb/path.
- [ ] **Revive `TemplateDefinitionCompiler` + `TemplateCompiler`** — unblocks OT Tier-2 AND gives the agent a real compile tool; validates the revive+wrap strategy and the lift-and-shift port.
- [ ] **OT Tier-2 typed list-param display** — closes the carried residual using the revived definition compiler.

### Add After Validation (v2.x)

- [ ] **Revive `TreeFileBuilder`** (build-from-source `.tre`) + wire as MCP pack tool — once the first compile revive proves the toolchain port.
- [ ] **First DCC-style editor** (terrain or particle — high modder demand) as a TJT SubPanel + its `UtinniCoreDotNet` codec.
- [ ] **WorldSnapshot editor** — extend the existing Snapshot panel (lowest-risk editor; injection-native already).
- [ ] **MCP prompts** for canned pipelines (edit → compile → repack → validate).
- [ ] **DataTableTool compile path** + exporter wrappers.

### Future Consideration (post-v2.0)

- [ ] **Live-in-client preview** as an MCP tool (live-patch tier is infra-ready but user-disabled — high risk, opt-in).
- [ ] **Animation live-preview** coordinated with the Blender suite.
- [ ] Second/third DCC editors; Wave-2 editor backlog (shader, UI, sound, lightning, swoosh).

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| MCP read tools (wrap CLI verbs) | HIGH | LOW | P1 |
| MCP write tools + safety model (loose-override, elicitation, verify, backup) | HIGH | MEDIUM | P1 |
| Revive TemplateDefinitionCompiler (param→type schema) | HIGH | MEDIUM | P1 |
| Revive TemplateCompiler (.tpf→.iff) | HIGH | MEDIUM | P1 |
| OT Tier-2 typed display (residual) | MEDIUM | LOW (given schema) | P1 |
| Revive TreeFileBuilder (source→.tre) | HIGH | MEDIUM | P2 |
| First DCC editor (terrain or particle) + codec | HIGH | HIGH | P2 |
| WorldSnapshot editor (extend Snapshot panel) | MEDIUM | MEDIUM | P2 |
| MCP prompts (canned pipelines) | MEDIUM | LOW | P2 |
| DataTableTool compile + exporters | MEDIUM | MEDIUM | P2 |
| Blender-export open/preview + boundary doc | MEDIUM | MEDIUM | P2 |
| Live-in-client preview via MCP | HIGH | HIGH | P3 |
| Animation live-preview | MEDIUM | HIGH | P3 |

**Priority key:** P1 = must-have for v2.0 launch · P2 = add when possible · P3 = future.

---

## Competitor / Precedent Feature Analysis

| Aspect | SWG original toolchain (2003) | swg-blender-plugin (sibling) | Utinni v2.0 (our plan) |
|--------|-------------------------------|------------------------------|------------------------|
| Asset edit | MFC/Qt standalone editors | n/a (DCC export only) | Themed injection-native SubPanels |
| Compile | `TemplateCompiler` etc. (P4/Alienbrain-coupled) | shells out to native CLIs | Revive+wrap the same CLIs, decoupled from P4 |
| Pack | `TreeFileBuilder` (+ `.rsp`) | `rsp_builder.py` reimpl | Revive `TreeFileBuilder` as shared ground-truth |
| AI/agent surface | none | Blender MCP (`execute_blender_code`) + thin addon | **MCP server over byte-exact pipeline w/ write-safety model** |
| Live preview | run the client manually | in-client Viewer = ground truth | **live in-client injection preview** (the moat) |
| 3D authoring | MayaExporter | **owns it** (mesh/skel/anim) | **explicitly out (DEC-A3)** |

The MCP write-safety model and live-in-client preview are the two features no precedent tool offers; the Blender suite establishes the "MCP-as-thin-shell-over-shared-format-lib" pattern Utinni's MCP server should mirror.

---

## Sources

- MCP specification 2025-11-25 (Context7 `/websites/modelcontextprotocol_io_specification_2025-11-25`): ToolAnnotations (`readOnlyHint`/`destructiveHint`/`idempotentHint`/`openWorldHint` + the "hints are advisory / untrusted" caveat), Tools (`inputSchema`/`outputSchema`/`structuredContent`/`isError`), Resources, Prompts, Elicitation (`elicitation/create`, form/url modes, `requestedSchema`, accept/decline/cancel), stdio transport — HIGH.
- `D:/Code/swg-client-v2/docs/research/swg-tools-and-likely-studio-toolchain.md` (653-line tool census, on-disk): TemplateCompiler vs TemplateDefinitionCompiler semantics, `.tpf`/`.tdf` flow, exporter chain, Perforce/Alienbrain/PCRE/libxml2/zlib dependency map — HIGH.
- `D:/Code/swg-client-v2/docs/research/blender-mcp-vs-addon.md` (on-disk): Utinni↔Blender boundary, "format knowledge in shared lib / MCP as thin shell" rule, "always IFF, Viewer is ground truth" — HIGH.
- `D:/Code/Utinni/docs/ai/toolchain-inventory.md`: revive-vs-replace litmus, partial-coverage gaps, lift-and-shift constraint — HIGH (project doc).
- `D:/Code/Utinni/.planning/PROJECT.md` + `Utinni.Cli/Program.cs` (verb surface) + MEMORY (save-tier primitives): existing-capability dependency anchors — HIGH.
- MCP write-tool safety patterns (WebSearch, MEDIUM, multiple sources agree): human-in-the-loop elicitation, fail-closed destructive ops, concrete-outcome confirmations, annotation-driven UX vs server-side enforcement —
  [Zeo: Human-in-the-Loop Controls](https://zeo.org/resources/blog/mcp-server-safety-human-in-the-loop-controls-risk-assessment),
  [4sysops: MCP tool annotations](https://4sysops.com/archives/mcp-tool-annotations-securing-mcp-servers-against-the-lethal-trifecta/),
  [WRITER: MCP security](https://writer.com/engineering/mcp-security-considerations/),
  [Towards Data Science: MCP Security Survival Guide](https://towardsdatascience.com/the-mcp-security-survival-guide-best-practices-pitfalls-and-real-world-lessons/),
  [PolicyLayer: MCP Security](https://policylayer.com/mcp-security).

---
*Feature research for: AI-Assisted SWG asset-modding pipeline (Utinni v2.0)*
*Researched: 2026-06-01*
