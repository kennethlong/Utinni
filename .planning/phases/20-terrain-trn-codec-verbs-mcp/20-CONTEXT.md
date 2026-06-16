# Phase 20: Terrain `.trn` Codec + Verbs + MCP - Context

**Gathered:** 2026-06-15
**Status:** Ready for planning

<domain>
## Phase Boundary

A pure-managed C# `.trn` (SWG `TerrainGenerator` / FORM `TGEN`) codec that **reads →
navigates → edits → byte-exactly saves** a procedural terrain graph, surfaced through
golden-tested `utinni-cli` verbs and a thin MCP read tool, across **both** SWG lineages
(SWGEmu + Restoration). Built entirely by composition on the existing IFF DOM stack
(`IffReader` → `IffDocument` → `MutableIffDocument` / `IffWriter`) and the established
verb-dispatch + `apply-save-*` machinery — **no new external packages, no native/bridge
dependency** (pure net4.7.2 + net10 MCP).

**In scope:** TGEN structural decode (layer tree + six read-only palettes), Tier-1 typed
tags + raw-fallback long-tail, fixed-length field/active-flag byte-exact edit-save, three
`trn` verbs + a `decode-iff` TGEN branch + one MCP read tool, a both-lineage fixture matrix.

**OUT of scope (do not research/plan — later phases):**
- 2D sampled-map preview (needs the `Sampler*` port) — v2.1.x.
- Structural authoring / boundary painting (full SOE TerrainEditor surface) — own milestone.
- Live in-client regen-on-save **and** the TJT SubPanel — **Phase 21** (PROD-W2-TRN-05).
- Variable-length **name edits** (layer/family names) — deferred (see D-06).
- Long-tail affector typed coverage (river/road/ribbon/environment/exclude/passable) beyond raw-fallback.
- No standalone renderer (DEC-A3); no preview of any kind this phase.

</domain>

<decisions>
## Implementation Decisions

### Typed-tag coverage (criterion 2)
- **D-01:** v1 typed (named-field) decode = the **research Tier-1 set**: affectors
  `AHCN`/`AHTR` (height), `ACCN`/`ACRH` (color), `ASCN`/`ASRP` (shader),
  `AFCN`/`AFSC`/`AFSN` (flora); boundaries `BCIR`/`BREC`; filters `FHGT`/`FSLP`.
  These cover criterion 2's "common tags." Everything outside the set is **raw-fallback**
  `{tag, version, hex}` — never a hard decode failure.
- **D-02:** **Unknown FORM version → raw-fallback the whole chunk** (do NOT best-effort
  partial-decode). The decoder reads the version FIRST, then reads exactly that version's
  field list; an unrecognized version degrades to raw rather than guessing field offsets
  (Pitfall 5 — guards against over-read into the next chunk and keeps byte-exact).
- **D-03:** **DEAD/obsolete tags** (`BALL`,`BSPL`,`AHSM`,`AHBM`,`ACBM`,`ASBM`,`AFBM`) are
  **recognized-and-skipped** — displayed as "obsolete, ignored," never editable, re-emitted
  verbatim via captured-slice (the C++ loader consumes their form without reading payload).
  Do NOT raw-fallback them as editable nodes (Pitfall 2).
- **D-04:** The six shared palettes (`SGRP`/`FGRP`/`RGRP`/`EGRP`/`MGRP`×2) are **read-only**,
  decoded **positionally in fixed load order** (shader, flora, radial, environment, fractal,
  bitmap) — NOT by tag lookup (the two `MGRP` palettes collide on tag; Pitfall 4). Decode each
  to a `familyId → name` list so affector family references resolve to human names. **Never
  renumber familyIds on save.** `LYRS` + every palette are **optional** forms (Pitfall 3).

### Edit scope (criterion 3)
- **D-05:** `apply-save-trn` v1 supports **fixed-length edits only**: scalar float/int values,
  enum values (e.g. `AHCN` operation), and **active-flag toggle**. All fixed-length → no
  parent FORM length ripple → byte-exact is trivially guaranteed. Matches criterion 3 verbatim.
- **D-06:** **Variable-length name edits are DEFERRED** out of v1 (would change payload length
  and ripple ancestor FORM lengths; Open Q3 unproven). Active-flag (an int32, fixed length) is
  the safest first edit target.
- **D-07:** Save path uses the **same fail-closed `--root` containment + atomic write** as
  `ApplySaveIffCommand` (`Utinni.Cli/Commands/ApplySaveIffCommand.cs:44`) — no path escape.
  Consistent with the existing `apply-save-*` family the MCP already shells.

### Verb / read surface (criterion 4)
- **D-08:** Surface = **`decode-iff` TGEN branch + `decode-trn` + `roundtrip-trn` +
  `apply-save-trn`** + a thin MCP `summarize_terrain` read tool. The `decode-iff` branch makes
  the existing MCP `decode_iff` tool route terrain **for free**; the standalone `decode-trn`
  exists for discoverability/symmetry with the other format families.
- **D-09:** `apply-save-trn` is **field-aware** (`--leaf <stable-id> --field <name> --value <v>`):
  it reads the tag's known `DATA` layout, replaces ONE field, re-emits the whole packed payload,
  `SetPayload` → `IsDirty`. NOT a reuse of whole-leaf `apply-save-iff` — TGEN `DATA` leaves pack
  2–6 scalars (e.g. `[int32 operation][float height]`), so whole-leaf hex replace would push the
  packing onto the caller (Pitfall 1). Address leaves by `DeriveStableId` ordinal path, not byte offset.
- **D-10:** MCP `summarize_terrain` is a `[McpServerTool(ReadOnly=true)]` that **shells
  `utinni-cli`** (copy `summarize_particle`) — ZERO format logic in `Utinni.Mcp` (MCP-OOP lock).
- **D-11:** The 16-verb `CommandLineParser` ceiling is **already solved** (`Type[]` overload +
  `object` `MapResult` → `Dispatch` switch, currently at 23 verbs). Criterion 4's "confirm it
  registers cleanly first" is a **Wave-0 smoke check** (add a no-op `trn` verb, confirm `--help`
  enumerates + parses), NOT new infrastructure.

### Fixtures / both-lineage gate (criterion 4 + DEC-C3)
- **D-12:** **Synthesized ≤200-byte `TGEN` fixtures (hand-emitted via `IffWriter`) are the
  committed golden corpus.** Matrix covers: a **low version ("SWGEmu-era") + high version
  ("Restoration-era") per Tier-1 tag**, plus minimal/no-palette TGEN, an unknown-tag
  (raw-fallback), and a DEAD-tag (skip) case. Deterministic, tiny, isolates the version matrix.
- **D-13:** **Additionally, source a real per-lineage `.trn` pair** to pin the EXACT FORM
  versions each live client ships (resolves research A5 / Open Q1). **Extract via Utinni's own
  revived `utinni-cli` TRE verbs** against the SWGEmu + Restoration client archives (dogfoods our
  toolchain). The planner should specify the exact planet + archive paths as a Wave-0 task
  (prefer a small/simple planet).
- **D-14:** **Use the real pair to (a) pin versions feeding D-12's synthesized matrix and (b)
  run `roundtrip-trn` as an extra byte-exact check — but keep them OUT of the committed golden
  corpus** (retail `.trn` are large; v6000+ TRE payloads are encrypted). Synthesized fixtures
  remain the committed goldens. If a sourced asset turns out small + unencrypted, committing it
  is optional (not required).

### Claude's Discretion
- Internal decoder class layout under `UtinniCoreDotNet/Formats/` (research precedent:
  `Decoders/TgenDecoder.cs` for the dispatch decoder + a `Terrain/` subdir for the model,
  mirroring `Particle/`).
- Exact split between a shared codec library and the verb commands.
- JSON envelope shape for `decode-trn` / `decode-iff` TGEN output (follow the existing
  particle/OT decoder envelope conventions).
- Whether to fold `decode-trn` into `DecodeIffCommand` or keep a separate `DecodeTrnCommand`
  alias (D-08 requires the `decode-iff` branch regardless; the alias is the cheap symmetry add).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase research & requirements
- `.planning/phases/20-terrain-trn-codec-verbs-mcp/20-RESEARCH.md` — the authoritative
  format taxonomy, tag tiers, palette load order, version-dispatch, pitfalls, and verb/fixture
  strategy. The tag taxonomy + six-palette tables + seven pitfalls are the implementation spine.
- `.planning/ROADMAP.md` §"Phase 20" — goal + 4 success criteria (the acceptance contract).
- `.planning/REQUIREMENTS.md` — PROD-W2-TRN-01..04.

### Reference source (READ-ONLY — port understanding, never `#include`/ProjectReference)
- `D:/Code/swg-client-v2/src/engine/shared/library/sharedTerrain/src/shared/generator/` —
  `TerrainGenerator.{cpp,h}` (TGEN load/save, layer recursion, palette load order
  `cpp:2174-2212`), `TerrainGeneratorType.{h,def}` (full FourCC + enum taxonomy),
  `TerrainGeneratorLoader.cpp` (tag→class dispatch + DEAD tags), `AffectorHeight.cpp` /
  `AffectorShader.cpp` (typed leaf layout + `familyId` ref), `Boundary.cpp` / `Filter.cpp`
  (feather/version fields), `ShaderGroup.cpp` / `FractalGroup.cpp` / `BitmapGroup.cpp`
  (palette versions + the `MGRP` collision).

### Utinni reuse targets (live repo — the composition surface)
- `UtinniCoreDotNet/Formats/Iff/{IffReader,IffWriter,IffDocument,MutableIffDocument,MutableIffNode}.cs`
  — the IFF DOM + captured-slice byte-exact edit engine (no-pad at `IffWriter.cs:39-41,141`;
  re-emit at `:103`; stable-id at `MutableIffDocument.cs:161-177`).
- `Utinni.Cli/Commands/DecodeIffCommand.cs:84-169` — root-FORM auto-dispatch (PEFT precedent;
  add the TGEN twin here).
- `Utinni.Cli/Commands/{RoundtripParticleCommand,ApplySaveIffCommand}.cs` — verb shape +
  `--root` containment (`ApplySaveIffCommand.cs:44`) templates.
- `Utinni.Cli/Program.cs:43-74` — the `Type[]` `ParseArguments` + `Dispatch` switch (D-11).
- `Utinni.Mcp/Tools/ReadTools.cs:97-108` — `summarize_particle`, the copy-template for
  `summarize_terrain` (MCP-OOP).
- `UtinniCoreDotNet/Formats/Decoders/` + `UtinniCoreDotNet/Formats/Particle/` — decoder +
  model placement precedent for the new `Terrain/` subdir.

### Project invariants
- `AGENTS.md` — DEC-C3 (byte-exact gate), DEC-V2-VERBS-FIRST, DEC-V2-MCP-OOP, DEC-A1 boundary
  note (terrain `.trn` are authoritative shared assets, **not** fenced), DEC-A3 (no renderer).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `IffReader` / `IffDocument`: format-agnostic FORM/chunk parse with BE framing + no-pad detect
  + EOF guards — TGEN is "just another FORM tree," no new parser.
- `MutableIffDocument` + `IffWriter`: captured-slice DOM — clean nodes re-emit verbatim, only a
  `SetPayload`'d node is re-encoded. This IS the byte-exact edit foundation; the only new bit is
  re-encoding ONE multi-field `DATA` payload (D-09).
- `DeriveStableId`: hierarchical ordinal paths uniquely address the many identically-named `DATA`
  chunks across affectors — the `apply-save-trn --leaf` selector.
- `Type[]` verb dispatch + `apply-save-*` `--root` containment + `ReadTools` thin-shell pattern:
  all proven across Phases 8/11/13/14/15.

### Established Patterns
- Root-FORM `SubTypeId` auto-dispatch in `decode-iff` (PEFT/datatable/OT branch here) — TGEN slots
  in as one more branch (gives MCP routing for free).
- Raw-fallback on unknown chunks (Phase 11/15 decoders) — the safety net for D-01/D-02.
- IFF no-pad (datatable/stf/particle) — `IffWriter` already omits the pad; do NOT re-implement framing.

### Integration Points
- New `TgenDecoder` + `Terrain/` model compile into the existing `UtinniCoreDotNet.dll` (no new
  project; `Generated/UtinniCore.cs` NOT touched — pure managed).
- New verbs compile into `utinni-cli.exe`; `summarize_terrain` into `Utinni.Mcp` (net10) shelling out.

</code_context>

<specifics>
## Specific Ideas

- User has access to **all** client versions (SWGEmu + Restoration) **and** the TRE extractor, and
  wants the real-asset pair sourced **via Utinni's own `utinni-cli` TRE verbs** (dogfooding) — see
  D-13. Prefer a small/simple planet's `terrain/<planet>.trn`.
- Treat the research `20-RESEARCH.md` tag taxonomy + seven pitfalls as the implementation spine —
  the user did not move any tags between Tier-1/Tier-2 (accepted the recommended split, D-01).

</specifics>

<deferred>
## Deferred Ideas

- **Variable-length name edits** (layer/palette-family names) — deferred from v1 (D-06); needs a
  length-ripple round-trip proof (research Open Q3) before shipping. Future terrain-edit phase.
- **2D sampled-map preview** (needs `Sampler*` port) — v2.1.x.
- **Structural authoring / boundary painting** — own milestone.
- **Long-tail affector typed coverage** (river/road/ribbon/environment/exclude/passable, plus
  fractal-referencing `AHFR`/`ACRF`/`FFRA`) beyond raw-fallback — Tier-2 follow-up.
- **Live in-client regen-on-save + TJT SubPanel** — Phase 21 (PROD-W2-TRN-05).

### Reviewed Todos (not folded)
Three todos surfaced via weak generic-keyword matches ("phase"/"swg" only) — all off-domain for a
terrain codec phase, so reviewed and NOT folded:
- `phase09-datatable-editor-review-warnings.md` — datatable editor code-review edges (Phase 9 domain).
- `phase10-stringtable-sc3-live-reload-residual.md` — stringtable live-reload (Phase 10 domain).
- `swg-window-resize-fullscreen-edge-cases.md` — D3D9/DXGI presentation resize (render-backend domain, Phase 18/19/24).

</deferred>

---

*Phase: 20-terrain-trn-codec-verbs-mcp*
*Context gathered: 2026-06-15*
