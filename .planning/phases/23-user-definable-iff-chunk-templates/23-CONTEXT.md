# Phase 23: User-Definable IFF Chunk Templates - Context

**Gathered:** 2026-06-20
**Status:** Ready for planning

<domain>
## Phase Boundary

A schema-driven, **user-definable IFF chunk codec**: a modder describes an arbitrary IFF *leaf
chunk's* binary layout **once** (as a reusable, shareable template) and Utinni then auto-decodes,
displays, edits, and **byte-exactly re-encodes** any matching chunk — turning Utinni from "the
formats we coded" into "any format a modder can describe." Built by composition on the existing
pure-managed IFF stack (`IffPayloadCursor` for payload scalars; `MutableIffDocument` / `IffWriter`
for the captured-slice, "field == byte range" byte-exact edit/re-emit) + the established
verbs-first + `apply-save-*` + MCP-read-tool machinery, surfaced through the **existing Phase-8
`FormIffEditor`** (DEC-C4 — editors live inside The Jawa Toolbox). No new external packages; no
native/bridge dependency (pure net4.7.2 engine + net10 MCP).

**The reframe that defines this phase (brainstormed, user-approved):** the conventional tools in
this space (`.tab` type-headers, `.tdf`, 010 Editor `.bt`, Kaitai `.ksy`, ImHex `.hexpat`) all
optimize for *transcribing* a layout you already know — and are hostile to the expensive part,
*discovering* a layout from raw bytes. Utinni builds the **better mousetrap**: authoring is bound
to the live bytes (Tier B, below). This is *in scope* — a better *how* for criterion 1 ("describe
a layout") + criterion 3 ("manage from the IFF Editor UI"), not a new capability.

**In scope:**
- A pure-managed, **two-layer template type system** (kernel + presets) that decodes/encodes
  arbitrary leaf-chunk payloads byte-exact (D-08..D-11).
- **Tier B in-place hex-driven template builder** inside `FormIffEditor`: select a byte range →
  assign type+name → template grows → **live decode preview + continuous byte-exact round-trip
  check**; un-annotated bytes stay visibly raw (D-01..D-03).
- **Auto-apply** a matching template to an otherwise-hex chunk; create / edit / save / select
  templates from the IFF Editor UI (criterion 3) (D-04..D-07).
- Templates persisted as portable **JSON files** across a **scanned list of dirs** (shipped +
  app-data + project-local packs); ship type **presets + a couple of worked example chunk
  templates** (D-12..D-14).
- **Verbs-first engine surface** (`utinni-cli` template verbs) + a golden byte-exact round-trip
  gate + a thin MCP read tool (D-15..D-16).
- **Tier-C readiness guardrails** baked into *how* B is built — each also earns its keep in B, so
  zero speculative cost (D-17).

**OUT of scope (do not research/plan — deferred / later phases):**
- **Tier C corpus inference** (sample every instance of a tag across the loaded TREs → *propose* a
  structure) — its own future phase. Tier B lays its substrate; it does NOT build the inference
  pass, the constant-vs-varying differ, the array-boundary detector, or an `infer-template` verb.
- **Templates overriding built-in codecs** (deliberately shadowing CLEF/datatable/OT/terrain to
  re-describe a format Utinni decodes "wrong") — deferred to keep the quick win collision-free.
- **Sentinel-terminated arrays** (read-until-a-marker-value) — planner discretion / defer; the
  three locked array kinds (D-10) cover the real SWG chunk shapes.
- A standalone renderer / any non-IFF concern (DEC-A3); the Tier-A grid-form builder (built only as
  a scope-bite fallback to Tier B).

</domain>

<decisions>
## Implementation Decisions

### Authoring surface (criteria 1 + 3)
- **D-01:** **A template is a portable JSON file** = the canonical source of truth (diffable,
  shareable, version-controllable; the Utinni decoder-envelope idiom). Both research threads
  (SOE pipeline + modern RE tooling: 010/Kaitai/ImHex) converge decisively: the template is a
  hand-authorable *file*; a GUI is a *view over* the file, never the source of truth. The JSON
  carries its own `version` field for forward migration as the type system grows (D-13).
- **D-02:** **Primary authoring interaction = Tier B in-place hex-driven builder** inside
  `FormIffEditor`. Select a byte range in the chunk's real hex → right-click → assign type + name →
  the template grows, the **decoded preview updates live**, and a **byte-exact round-trip check
  runs continuously**; **un-annotated bytes stay visibly raw** so "you haven't consumed the whole
  payload" is impossible to miss. Offsets are *selections*, not arithmetic. The builder emits the
  same D-01 JSON artifact. (Rationale: the hard part is *discovery against real bytes*, not typing
  a schema — bind authoring to the bytes. Utinni is uniquely positioned: it already has the
  `MutableIffDocument` "field == byte range" DOM + the `FormIffEditor` hex view.)
- **D-03 (fallback only):** A **Tier-A grid field-builder** (add/remove/reorder typed rows
  reading/writing the JSON) is the **scope-bite fallback** if Tier B proves too large — NOT a
  parallel deliverable. Do not build both; Tier B is the target.

### Match & auto-apply (criterion 2)
- **D-04:** **Match key = ancestor-FORM-path + leaf tag**, auto-captured at Tier-B authoring time
  from where the user is standing in the IFF tree, **author-widenable to tag-only** (e.g. "`XXXX`
  only under `FOO/0003`" vs "`XXXX` under any parent"). **Version-FORM awareness is REQUIRED, not
  optional** — the CLEF `CPAP` chunk has three different byte layouts across version FORMs
  `0001/0002/0003`; tag-only matching would silently mis-decode and break byte-exact.
- **D-05:** **Built-ins win; templates fill otherwise-hex leaves.** This falls out of an **altitude
  difference**, not a policed rule: built-in codecs (CLEF/datatable/OT/terrain) claim a whole
  *file format* at the root-FORM; a template describes a single *leaf-chunk payload*. A file
  Utinni recognizes → its built-in claims it wholesale (templates never engage); a file Utinni
  doesn't recognize → every leaf chunk is template-eligible (nothing else decodes them).
- **D-06 (forward-compat):** Design the **template decode/encode contract to mirror the built-in
  decoder interface**, so "built-ins win" is a *precedence ordering*, not an architecture wall —
  leaving the door open for a later phase to express some built-ins *as* templates (dogfooding).
  Build the ordering; do NOT build the override path (deferred).
- **D-07:** **The locked round-trip check doubles as a match-fit confidence signal.** Key-match +
  consumes-payload-exactly-with-plausible-values → green, auto-applied silently. Key-match but the
  bytes don't round-trip → show it, flag "template doesn't fit these bytes." This is NOT inference
  (no structure guessing) — it surfaces the gate already being computed. Multi-match ties →
  most-specific key wins; genuine tie → small picker (planner's discretion).

### Type system depth — the encode-parity crux (criteria 1 + 2)
- **D-08:** **Two-layer type system.** A small **KERNEL** is the *only* thing the engine
  decodes/encodes: sized ints (signed/unsigned, **LE default** per Pitfall 6), `f32`/`f64`,
  **NUL-terminated C-string + fixed `char[n]`** (Pitfall 2 — never length-prefixed; encoding
  attribute, ASCII default), raw bytes + **explicit padding**, **struct**, **array**. A library of
  **PRESETS** (`color` / `vector` / `quaternion` / `matrix` / `stringId`) are **pure sugar =
  pre-built structs over the kernel**. **Byte-exactness lives ENTIRELY in the kernel** — a preset
  cannot introduce a parity bug, and fixing/adding a preset never touches the engine. This
  satisfies criterion-1's named-type list ("colors, vectors, quaternions, matrices, …") without
  bloating the engine.
- **D-09 (flagged research line item):** The starter presets need their **EXACT** SWG byte layouts
  — color packing (ARGB-`u32` vs 3×f32 vs 4×`u8`), quaternion component order (`wxyz` vs `xyzw`),
  matrix dimensions + row/column-major — and these MUST come from the `swg-client-v2` engine
  reference, **not guessed**. SWG uses *multiple* conventions, which is exactly why presets are
  *editable data*: a chunk that disagrees is a one-click fix, not an engine change.
- **D-10:** **Ship all three array kinds:** (1) **fixed-count** (constant in the template);
  (2) **count-from-prior-field** — the array length = the value of a named earlier field, and the
  **encoder AUTO-RECOMPUTES that count field on write** (the core encode-parity mechanism for
  count-then-N-records chunks; the documented gap that makes Kaitai *not* byte-safe); (3)
  **until-end / trailing-remainder** (consume to the chunk boundary — the byte-exact safety net).
  Sentinel-terminated arrays = planner discretion / defer.
- **D-11:** **Enum / bitfield display sugar is IN** — an *optional named-value map attribute* on an
  int field, rendering `active(0)` / `walk|run` instead of raw numbers. Mirrors the SOE DataTable
  `e(a=0,b=1)` / `v(walk=1,run=2)` convention modders already know. Cheap (an attribute, not a
  kernel type); high readability win.

### Storage, share & CLI surface (criterion 3 + DEC-V2-VERBS-FIRST / DEC-V2-MCP-OOP)
- **D-12:** Templates persist across a **scanned list of dirs** (mirrors SWG's own
  searchPath/load-order mental model): a **shipped/built-in set** + a **per-user app-data set** +
  an **optional project-local folder** a modder can git-version and share as a **pack**. Makes
  "share a template pack" first-class (clone a pack), not "email a JSON file." Dirs auto-scanned;
  the UI offers import/export.
- **D-13:** A template is a **single self-contained JSON file** carrying its own format **`version`
  field** (forward migration as the type system grows).
- **D-14:** Ship **presets** (the `vector/quaternion/color/matrix/stringId` set — needed anyway)
  **plus a couple of good worked example chunk templates** (simple real-ish SWG chunks, fully
  described). The examples **double as golden byte-exact fixtures AND teaching artifacts** — shared
  cost.
- **D-15:** **Verbs-first for the ENGINE** (DEC-V2-VERBS-FIRST): a `utinni-cli` template verb family
  — e.g. `decode-with-template` / `roundtrip-template` / `apply-save-template` / `validate-template`
  / `list-templates` (exact names + flag shapes are the planner's, following the `apply-save-trn`
  `--root`-containment + atomic-write precedent) — with a **golden byte-exact round-trip gate**
  (DEC-C3). Plus a **thin MCP read tool** that shells `utinni-cli` (zero format logic in
  `Utinni.Mcp` — MCP-OOP lock).
- **D-16:** The **interactive hex-authoring (Tier B) stays UI-only** — a *legitimate non-exception*
  to verbs-first: it is an *interaction*, not a batch capability (you cannot "hex-select"
  headlessly). The decode/encode/validate/roundtrip *capabilities* are all verbs (D-15); only the
  authoring *gesture* is UI.

### Tier-C readiness — guardrails on HOW we build B (not new scope)
- **D-17:** Tier C (corpus inference) is deferred to its own phase, but Tier B is built so Tier C ≈
  "widen the sample from 1 → N + add a cross-sample differ," not a rewrite. Each guardrail also
  improves B, so there is **zero speculative cost**:
  1. **Engine headless in `UtinniCoreDotNet/Formats`** (no WinForms), UI only in TJT — the
     *load-bearing* decision; the only thing that would foreclose C is welding the engine to the
     UI. (Also: this is what makes D-15's verbs nearly free.)
  2. **Fit-check as a pure function** `(template, payloadBytes) → FitReport { consumedExactly,
     perFieldPlausibility[] }` — built for B's live round-trip indicator anyway; Tier C's scoring
     loop becomes `samples.Select(fitCheck)`.
  3. **Type-plausibility predicates as a standalone reusable library** (`looksLikeFloat`,
     `looksLikeCStringRun`, `looksLikeCount`, …) — powers B's optional **select → suggest type**
     assist (single-sample inference) and is reused verbatim as Tier C's detection primitives.
     This is *the one small genuine addition to B*, justified because it makes B's authoring nicer.
  4. **Match key as a corpus query** — the `(ancestor-path, tag)` key is a predicate the existing
     TRE/IFF enumerator can filter on, and the chunk-walk operates on *any* IFF stream (not just
     the open document). This IS B's auto-apply index; Tier C's "collect every payload for this
     key across the load order" is then one call.

### Claude's Discretion
- Exact `utinni-cli` template verb names + flag shapes (D-15); whether `decode-with-template` is
  standalone or a `decode-iff --template` branch (follow the `DecodeTrnCommand`/`decode-iff`
  alias-delegation precedent).
- JSON envelope shape for the decode output + the JSON *template schema* itself (field record:
  name / type / repeat-spec / optional enum-map / encoding) — follow existing decoder-envelope
  conventions; include the D-13 `version` field.
- Multi-match tie-break UI (D-07); interior Tier-B builder controls (honor Pitfall 8 — Dock.Fill
  front-most / nested `SplitContainer`, size before splitter distance; MEF-safe `IEditorPlugin`
  ctor).
- Whether template authoring rides the existing `FormIffEditor` `IffEditController` undo stack or
  gets its own; the exact app-data + project-local dir paths and scan order (D-12).
- Sentinel-terminated array support (D-10) if cheap once the three locked kinds exist.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/ROADMAP.md` §"Phase 23: User-Definable IFF Chunk Templates" — goal + 3 success
  criteria (the acceptance contract; QUICK WIN / Backlog 999.2; independent P2 on
  `IffPayloadCursor`).
- `.planning/REQUIREMENTS.md` §"IFF chunk templates — quick win 999.2 (PROD-IFFT)" — `PROD-IFFT-01`
  (describe an arbitrary layout as a named reusable template), `PROD-IFFT-02` (auto-apply +
  byte-exact re-encode, round-trip verified — the hidden encode-parity risk), `PROD-IFFT-03`
  (create/edit/save/select from the IFF Editor UI).

### Format-reference understanding (READ-ONLY — port understanding, never `#include`/ProjectReference)
- `D:/Code/swg-client-v2/src/engine/shared/library/sharedTemplateDefinition/src/shared/core/TemplateData.cpp`
  (field-type parsing ~lines 528–622) + `.../TemplateDefinitionFile.cpp` +
  `D:/Code/swg-client-v2/src/engine/shared/application/TemplateCompiler/src/shared/TemplateCompiler.cpp`
  — the SOE `.tdf` type vocabulary (`int float bool string filename stringId vector enum<X>
  struct<Y> template<Z> list`, nested structs, fixed/var arrays, min/max limits): the closest
  historical analog to "describe an arbitrary layout once." **Grounds the D-08 type catalog.**
- `D:/Code/swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTableColumnType.cpp`
  (type-code grammar ~lines 84–230) — the SOE two-row-header spreadsheet model (`i/f/s/b/h/e/v/p/z`
  + inline `e(a=0,b=1)[def]` / bitfield `v(walk=1,run=2)` + default brackets). **Grounds D-11's
  enum/bitfield display sugar.**
- **SWG composite byte layouts (D-09 — MUST pin exactly, do not guess):** the engine's
  `Vector` / `Quaternion` / `PackedRgb` (or `VectorArgb`) / `Transform` definitions in
  `D:/Code/swg-client-v2/src/engine/shared/library/sharedMath/` (and `clientGraphics` for any
  packed-color path) — exact component order, packing, matrix dims/major-ness for the starter
  presets.

### Utinni reuse targets (live repo — the composition surface)
- `UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs` — the LE-scalar / NUL-CString / raw-bytes
  bounds-checked cursor to **extend** with the kernel primitives (D-08). The phase's dependency
  anchor.
- `UtinniCoreDotNet/Formats/Iff/{IffReader,IffWriter,IffDocument,MutableIffDocument,MutableIffNode}.cs`
  — the captured-slice, "field == byte range" byte-exact edit engine: verbatim re-emit of clean
  nodes + parent-FORM length re-stamp for dirty nodes (the D-10 length-ripple / auto-recompute
  mechanism the planner must verify holds for length-changing template edits).
- `UtinniCoreDotNet/Formats/ClientEffect/ClefFieldCodec.cs` — the **most recent variable-length
  LE-scalar + NUL-C-string encode/decode precedent** (Phase 22; the "genuinely new asset" vs the
  fixed-span `TrnFieldEncoder`). The closest model for the kernel codec.
- `Utinni.Cli/Commands/{DecodeIffCommand,ApplySaveIffCommand,DecodeTrnCommand}.cs` +
  `Utinni.Cli/Program.cs` — the root-FORM auto-dispatch branch, the `--root`-containment +
  atomic-write `apply-save-*` template, the alias-delegation precedent, and the `Type[]`
  `ParseArguments` + `Dispatch` switch (D-15).
- `Utinni.Mcp/Tools/ReadTools.cs` (`summarize_particle` / `summarize_terrain` / `summarize_clienteffect`
  shell-out pattern) — the thin MCP read-tool template (D-16; ZERO format logic — MCP-OOP).
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` (+
  `.Designer.cs`) and `.../UI/Controls/IffChunkTree.cs` — the **Phase-8 host** the Tier-B builder
  grows into (today: hex/text/replace-from-file leaf pane + `IffEditController` undo +
  `ProcessCmdKey` shortcuts + the provenance-gated Save▾ matrix). The select→annotate→live-decode
  pane attaches here.
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/` (`IffSaveTargets` —
  `looseOverrideSubDir = "loose"`; `LooseOverridePath.Resolve`) — the loose-override save matrix a
  template-applied edit flows into.
- `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs` — the plugin contract (guard the ctor against
  MEF silent-reject, Pitfall 8).

### Prior-phase decision precedents (codec + verb + fixture + SubPanel idioms)
- `.planning/phases/22-clienteffect-editor/22-CONTEXT.md` — variable-length byte-exact edits,
  raw-fallback-never-hard-aborts, synthesized-golden + real-asset fixture matrix, verbs-first +
  thin MCP, loose-override save (D-01..D-16 there map closely onto this phase).
- `.planning/phases/20-terrain-trn-codec-verbs-mcp/20-CONTEXT.md` — the codec/verb/fixture decision
  template + the fixed-span `TrnFieldEncoder` (the *wrong* tool for length-changing edits — contrast).

### Project invariants
- `AGENTS.md` — DEC-C4 (editors inside TJT), DEC-V2-VERBS-FIRST, DEC-V2-MCP-OOP, DEC-A3 (no
  standalone renderer), DEC-C3 / byte-exact round-trip gate, IFF no-pad, loose-override matrix,
  format-support reality (both lineages; raw-fallback never hard-aborts).
- `docs/ai/lessons.md` + auto-memory: `[[feedback_winforms_dockfill_zorder]]` (Dock.Fill
  front-most, Pitfall 8), `[[project_swg_iff_no_pad]]` (datatable .iff not word-padded; reader
  detects the pad), and the Pitfall 2/6 notes embedded in `IffPayloadCursor.cs` / `ClefFieldCodec.cs`
  (payload scalars LE; strings NUL-terminated, never length-prefixed).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`IffPayloadCursor` (the dependency anchor):** LE scalars / NUL-CString / raw bytes,
  bounds-checked, never allocates on attacker-controlled counts — extend with the D-08 kernel
  primitives (sized signed/unsigned ints, f64, padding, struct/array framing).
- **`MutableIffDocument` / `IffWriter` captured-slice DOM:** the "field == byte range" model that
  makes byte-exact re-encode nearly free (the design property research found separates a
  010/ImHex-grade tool that round-trips from a Kaitai-grade one that doesn't). Parent-FORM length
  re-stamp is the D-10 auto-recompute / length-ripple mechanism.
- **`ClefFieldCodec` (Phase 22):** the most recent variable-length LE + NUL-C-string encode/decode
  — the closest precedent for the kernel codec's string + scalar handling.
- **`FormIffEditor` (Phase 8):** the host editor with an existing hex leaf pane, undo controller,
  shortcut capture, and the loose-override Save▾ matrix — the Tier-B builder attaches as a new
  leaf-pane mode.
- **Verb dispatch + `apply-save-*` `--root` containment + `ReadTools` thin-shell:** proven across
  Phases 8/11/13/14/15/20/22 — D-15/D-16 clone these shapes.

### Established Patterns
- Root-FORM `SubTypeId` auto-dispatch in `decode-iff` (PEFT/TGEN/CLEF/datatable/OT branches) — D-05
  templates sit *below* this at the leaf-chunk altitude; the dispatch is the "is there a built-in?"
  check.
- Raw-fallback on unknown chunks/versions (Phase 11/15/20/22 decoders) — the natural home for
  template auto-apply: a leaf with no built-in is exactly where a template engages (D-05).
- IFF no-pad (datatable/stf/particle/terrain) — `IffWriter` already omits the pad; do NOT
  re-implement framing.

### Integration Points
- New template **engine** (kernel codec + fit-check + type-plausibility predicates + JSON
  template (de)serializer) compiles into the **headless** `UtinniCoreDotNet.dll`
  (`Formats/Template/` or similar; `Generated/UtinniCore.cs` NOT touched — pure managed) (D-17.1).
- New `template-*` verbs compile into `utinni-cli.exe`; the thin MCP read tool into `Utinni.Mcp`
  (net10), shelling out (D-15/D-16).
- The **Tier-B hex-driven builder pane** compiles into `TheJawaToolboxDotNet` (sibling UtinniPlugins
  repo — standing cross-repo write authority; paired commit, no human checkpoint except the live
  smoke), attaching to `FormIffEditor` (D-02).

</code_context>

<specifics>
## Specific Ideas

- The user explicitly steered this from a "QUICK WIN" transcription tool toward a **better
  mousetrap**: "are they using paper because the tools suck? can we build a better mousetrap?" The
  answer — bind authoring to the live bytes (Tier B) — is the heart of the phase. This is a
  *noticeably more ambitious* phase than the QUICK WIN tag implied, and that is the deliberate,
  on-vision call (Utinni replaces the ~30 crappy tools modders juggle; don't reimplement the
  crappiest one).
- The user values these brainstorming sessions ("leads to better outcomes") — downstream agents
  should treat the *reasoning* in the decisions (altitude difference, kernel+presets, Tier-C
  substrate earning its keep in B) as load-bearing, not just the conclusions.
- Origin instinct (confirmed + bounded by research): the SOE pipeline DID use a
  spreadsheet-with-type-header model (`.tab` datatables) — ergonomic for *flat* layouts but unable
  to express nested structs / variable arrays, and only ever produced one fixed format. The
  arbitrary-layout path was always the `.tdf` text DSL. Hence JSON-file canonical + hex-driven
  builder, not a spreadsheet importer (a spreadsheet on-ramp for flat chunks was considered and
  not adopted for the quick win).
- The user chose the **fullest** option at the genuine forks: all three array kinds (incl. the
  encode-parity-critical count-from-prior-field), enum/bitfield sugar in, scanned-list dirs with
  shareable packs, worked examples shipped. The byte-exact bar is high; the captured-slice writer +
  the kernel-only parity surface is the mechanism the planner must prove holds.

</specifics>

<deferred>
## Deferred Ideas

- **Tier C — corpus inference** (its own future phase): when a chunk `XXXX` is opened, sample
  *every* `XXXX` across the loaded TREs and *propose* a structure — constant-vs-varying regions,
  ASCII+NUL string runs, plausible-float regions, count→N-records array detection — so the modder
  starts from a guess, not a blank hex dump. The moat: Utinni's whole-corpus access + live engine,
  which no standalone hex editor has. Tier B lays the substrate (headless engine, pure fit-check,
  type-plausibility predicate library, corpus-query match key) so this becomes "widen sample 1→N +
  a cross-sample differ." Add an `infer-template` verb then. (D-17.)
- **Templates overriding built-in codecs** — let a template deliberately shadow a built-in to
  re-describe a format Utinni decodes "wrong." Architecture left open via D-06 (contract mirrors
  built-in decoders); the override *path* + precedence UI deferred. Revisit if a concrete
  "Utinni decodes this wrong, let me fix it myself" case appears.
- **Sentinel-terminated arrays** (read-until-marker) — planner discretion this phase; otherwise a
  later type-system extension.
- **Tier-A grid field-builder** — retained only as the scope-bite fallback for Tier B (D-03), not a
  parallel deliverable.

### Reviewed Todos (not folded)
Surfaced via weak generic-keyword matches (score ≤ 0.6) — all off-domain for an IFF chunk-template
feature, reviewed and NOT folded:
- `phase09-datatable-editor-review-warnings.md` — Phase 9 datatable code-review edges (code-quality).
- `phase10-stringtable-sc3-live-reload-residual.md` — stringtable live-reload residual (Phase 10/15).
- `swg-window-resize-fullscreen-edge-cases.md` — D3D9/DXGI presentation resize (render-backend,
  Phase 18/19/24).
- `phase21-terrain-active-flag-ihdr-deeper-nesting.md` — terrain codec IHDR version-form nesting
  (Phase 20/21 terrain domain).

</deferred>

---

*Phase: 23-user-definable-iff-chunk-templates*
*Context gathered: 2026-06-20*
