# Phase 22: ClientEffect Editor - Context

**Gathered:** 2026-06-17
**Status:** Ready for planning

<domain>
## Phase Boundary

A **ClientEffect** (FORM **CLEF**) command-list editor across **both** SWG lineages
(SWGEmu + Restoration) — a pure-managed C# codec that **reads → edits → byte-exactly saves**
a flat command list, surfaced through golden-tested `effect-*` `utinni-cli` verbs + a thin MCP
read tool (the Phase 20 idiom), **and** a TJT editor surface (`EffectsSubPanel`, DEC-C4) (the
Phase 21 idiom). Built by composition on the existing IFF DOM stack (`IffReader` →
`MutableIffDocument` / `IffWriter`) + the established verb-dispatch + `apply-save-*` machinery +
the `ClientReloadDispatcher` preview tier — **no new external packages, no native/bridge
dependency** (pure net4.7.2 + net10 MCP).

**Format shape (from the swg-client-v2 reference):** `FORM CLEF { FORM 0001|0002|0003 { command-chunks } }`.
Five typed command chunks, each repeated 0..N, **flat** (no nesting, unlike terrain's recursive
layer tree):
- `CPAP` — CreateAppearance (string appearance name + time; **grew across versions**: v0002 adds
  soft-particle-terminate flag, v0003 adds min/max scale + min/max playback-rate floats).
- `PSND` — PlaySound (string sound name). Stable across versions.
- `CLGT` — CreateLight (RGB uint8 + time + attenuation floats + range). Stable.
- `CAMS` — CameraShake (magnitude/frequency/time/falloff floats). Stable.
- `FFBK` — ForceFeedback (string FF file + int32 iterations + range float). Stable.

Every command carries a **variable-length string** (appearance / sound / FF path) — the
length-ripple case terrain Phase 20 deferred (D-06), but here it is the primary edit (see D-01).

**In scope:**
- Pure-managed CLEF codec (decode all 5 typed commands incl. CPAP version deltas; raw-fallback
  unknown versions/command tags — never a hard decode failure).
- Full edits: variable-length string refs **and** scalar/flag/color fields, byte-exact.
- Full list authoring: **add / remove / reorder** commands.
- `effect-*` `utinni-cli` verbs (verbs-first) + a `decode-iff` CLEF branch + a thin MCP read tool.
- Both-lineage golden fixture matrix (all three CPAP versions + each command + unknown-version
  raw-fallback) + a real-asset pair sourced via Utinni's own TRE verbs.
- An `EffectsSubPanel` (thin docked entry, DEC-C4) that launches a roomy `FormClientEffectEditor`.
- A manual **Preview in client** replay action (matching the Particle editor).
- Fold the terrain `loose/`-subdir save bug fix (see Folded Todos).

**OUT of scope (do not research/plan — later phases / deferred):**
- A **generic multi-format Effects container** for Lightning/Swoosh — name `EffectsSubPanel` for
  future growth, but build ClientEffect-only this phase (D-05). Lightning/Swoosh are an explicit
  future milestone (ROADMAP §"Effects family").
- **Auto-replay on save** — manual Preview only (D-07); an effect firing every save is disruptive.
- **Resolving** appearance/sound/FF template references — that fetch is SWG-side; the codec
  preserves string names verbatim, never resolves them.
- Any **version normalization / upgrade** — preserve the file's source CLEF version (D-03).
- A standalone renderer of any kind (DEC-A3); preview is live-in-client only.

</domain>

<decisions>
## Implementation Decisions

### Edit scope (criterion 1)
- **D-01:** **Variable-length string edits are IN scope** — the modder can repoint the
  appearance / sound / ForceFeedback string refs (the high-value edit) **and** edit scalar /
  flag / color fields, all byte-exact. Unlike terrain (which deferred variable-length name edits,
  Phase 20 D-06), this is the whole point of a ClientEffect editor. The captured-slice
  `MutableIffDocument` / `IffWriter` re-stamps parent FORM lengths from actual child bytes, so the
  length-ripple is a solved DOM mechanism — the planner must **verify** that dirty-node ancestor
  FORM-length re-computation holds for a length-changing leaf (the byte-exact gate).
- **D-02:** **Full list authoring** — edit existing command fields, **plus add a new command,
  delete a command, and reorder** the list. (User chose the fullest option.) SOE notes the list
  carries no timing/sequence semantics, so reorder is cosmetic but harmless. Add/remove requires
  byte-exact child-chunk insertion/removal + version-FORM length re-stamp.
- **D-03:** **Preserve the source CLEF version verbatim** on save — never silently upgrade. The
  editor shows/edits only the fields that the file's version defines (a v0001 `CPAP` shows
  name + time only; v0003 exposes scale/rate). **Added** commands emit at the file's existing
  version. This keeps byte-exact and respects the two lineages shipping different versions. Do NOT
  normalize all CPAP to v0003.

### Editor form factor (criterion 1)
- **D-04:** Ship as a **thin docked `EffectsSubPanel` (`IEditorPlugin`) that launches a roomy
  standalone `FormClientEffectEditor`** — the Phase 21 terrain idiom (`TerrainSubPanel` →
  `FormTerrainEditor`), reconciling the ROADMAP "EffectsSubPanel" name with the Particle editor's
  Form pattern. The flat command list + per-command field grid wants horizontal room the narrow
  docked panel (~417px) can't give. (User chose this over standalone-Form-only and
  docked-panel-only.)
- **D-05:** Name it `EffectsSubPanel` (room for a later phase to grow it) but **scope this phase to
  ClientEffect only** — no speculative multi-format Effects container, no Lightning/Swoosh
  extension seams (YAGNI; those are a future milestone). (User chose ClientEffect-scoped.)
- **D-06 (planner's discretion):** Exact interior controls are the planner's, from precedent.
  Locked invariants only: a **flat command-list** control (no tree needed — CLEF has no nesting) +
  a **per-command typed field editor** where the 5 known commands display as typed fields and
  unknown command tags / unknown CLEF versions **degrade to a raw/hex field list — never a hard
  failure**. Precedent: `IffChunkTree` + `TreDetailPane` vs. stock `PropertyGrid`. Honor Pitfall 8
  (Dock.Fill front-most / nested `SplitContainer`, size before splitter distance).

### Live preview (beyond written criteria — user opted in)
- **D-07:** **Include a manual "Preview in client" replay action**, matching the Particle editor's
  hot-retrigger (the criteria omit preview, but ClientEffects are inherently replayable — adjacent
  and cheap). **Manual button only — NOT auto-on-save** (an effect firing at the player on every
  save is disruptive, unlike terrain's quiet regen). (User chose manual-only.)
- **D-08:** The replay path inherits the Phase 21 disciplines: dispatched onto the game thread via
  `GameCallbacks.AddMainLoopCall`, gated on `Game.IsRunning` first, **heap-free on the hot path**
  (`[[project_rh_snapshot_no_heap_alloc]]`, the `0x0051fb0a` guard), with **honest reload candor**
  (never over-promise; surface the honest tier if replay isn't reachable — the Phase 10/21
  observe-then-label precedent). Reuse the **Particle editor's existing "Preview in client"
  mechanism + effect-instantiation target** — do not invent a new replay path; the
  target/instantiation specifics are the planner's to pin from that precedent.

### Open / save workflow (criterion 1)
- **D-09:** Entry points = **open read-only from the TRE Browser → edits write to a loose override**
  (TREs never edited in place), **AND** direct open of an existing loose-override `.iff`. Matches
  terrain Phase 21 D-08 + the loose-override matrix. (User chose this over loose-override-only.)
- **D-10:** Both the new ClientEffect save **and** terrain save MUST land under **`<root>/loose/`**
  via `LooseOverridePath.Resolve(resolvedRoot, looseOverrideSubDir)` with `looseOverrideSubDir`
  defaulting to `"loose"` (the `IffSaveTargets` convention). Save path uses the same fail-closed
  `--root` containment + atomic write as `ApplySaveIffCommand` (Phase 20 D-07). See Folded Todos
  for the terrain half.

### Verb / read surface (criterion 2)
- **D-11:** Surface = **`effect-*` verbs** (verbs-first per DEC-V2-VERBS-FIRST — e.g.
  `decode-effect` / `roundtrip-effect` / `apply-save-effect`; exact names + the field-aware
  `apply-save-effect` flag shape are the planner's, following the `apply-save-trn` `--leaf`/`--field`
  precedent and the add/remove list-mutation needs of D-02) **+ a `decode-iff` CLEF branch** (gives
  the existing MCP `decode_iff` tool CLEF routing for free, like the TGEN branch) **+ a thin MCP
  read tool** `summarize_clienteffect` that **shells `utinni-cli`** (copy `summarize_particle` /
  `summarize_terrain`; ZERO format logic in `Utinni.Mcp` — MCP-OOP lock).
- **D-12:** The 16-verb `CommandLineParser` ceiling is **already solved** (`Type[]` overload +
  `object` `MapResult` → `Dispatch` switch). Adding `effect-*` verbs is a Wave-0 smoke check
  (confirm `--help` enumerates + parses), not new infrastructure.

### Fixtures / both-lineage gate (criterion 2 + DEC-C3)
- **D-13:** **Synthesized small `CLEF` fixtures (hand-emitted via `IffWriter`) are the committed
  golden corpus.** Matrix MUST cover: **all three CPAP versions (v0001 / v0002 / v0003)**, **each
  of the 5 command types**, an **unknown-version** case (raw-fallback the whole CLEF FORM), and an
  **unknown-command-tag** case (raw-fallback + re-emit verbatim). Deterministic, tiny, isolates the
  version × command matrix. Mirrors Phase 20 D-12.
- **D-14:** **Additionally source a real per-lineage ClientEffect `.iff` pair** to pin the EXACT
  CLEF versions each live client ships — **extract via Utinni's own revived `utinni-cli` TRE verbs**
  (dogfooding). Use it to (a) confirm which versions feed D-13's synthesized matrix and (b) run
  `roundtrip-effect` as an extra byte-exact check — but keep real assets OUT of the committed
  goldens unless small + unencrypted. Mirrors Phase 20 D-13/D-14.

### Claude's Discretion
- Internal codec class layout under `UtinniCoreDotNet/Formats/` (precedent: a `ClientEffect/`
  model subdir mirroring `Particle/` + a `Decoders/ClefDecoder.cs` dispatch decoder).
- Exact `effect-*` verb names + the `apply-save-effect` flag shape (D-11).
- JSON envelope shape for `decode-effect` / `decode-iff` CLEF output (follow the existing
  particle/terrain decoder envelope conventions).
- Interior `EffectsSubPanel`/`FormClientEffectEditor` control choice (D-06).
- Whether `decode-effect` is a standalone command or a thin alias delegating to `DecodeIffCommand`
  (the `decode-iff` branch is required regardless; the alias is the cheap symmetry add — cf.
  `DecodeTrnCommand`).

### Folded Todos
- **`phase21-terrain-override-loose-subdir.md`** (area: terrain-save; severity: medium) — Terrain's
  `TerrainSaveTargets.SaveLooseOverride` resolves the destination with **no `looseOverrideSubDir`**,
  so a terrain override for `terrain/naboo.trn` lands at `<root>\terrain\naboo.trn` instead of
  `<root>\loose\terrain\naboo.trn` like every other editor → off the documented loose `searchPath`,
  not picked up by the client (had to be hand-relocated in the 21-04 smoke). **Folded because**
  Phase 22 is already in the shared loose-override save plumbing (D-10) and must get the `loose/`
  subdir right for ClientEffect; fixing terrain in the same pass keeps the whole save matrix
  consistent. **To close:** thread a `looseOverrideSubDir` (default `"loose"`, matching
  `IffSaveTargets`) through `TerrainSaveTargets.SaveLooseOverride` so terrain overrides land at
  `<root>\loose\<logical>`; keep byte-parity with `apply-save-trn` (align the CLI subdir convention
  if it differs); add/adjust a test asserting the resolved destination is under `<root>\loose\`.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/ROADMAP.md` §"Phase 22: ClientEffect Editor" — goal + 2 success criteria (the
  acceptance contract); §"Effects family" note (Lightning/Swoosh = future milestone, D-05).
- `.planning/REQUIREMENTS.md` — `PROD-W2-CFX-01` (open/edit/byte-exact save + EffectsSubPanel),
  `PROD-W2-CFX-02` (golden-tested verbs + MCP read tool, both lineages).

### ClientEffect format reference (READ-ONLY — port understanding, never `#include`/ProjectReference)
- `D:/Code/swg-client-v2/src/engine/client/library/clientGame/src/shared/clientEffect/ClientEffectTemplate.cpp`
  — `load()` + version dispatch (`load_0001` ~line 271, `load_0002` ~line 366, `load_0003` ~line 462);
  the per-command parse for CPAP / PSND / CLGT / CAMS / FFBK and the version-delta CPAP field growth.
- `D:/Code/swg-client-v2/src/engine/client/library/clientGame/src/shared/clientEffect/ClientEffectTemplate.h`
  — the command struct definitions (CreateAppearanceFunc / PlaySoundFunc / CreateLightFunc /
  CameraShakeFunc / ForceFeedbackFunc) = the typed-field layout.
- `D:/Code/swg-client-v2/src/engine/client/library/clientGame/src/shared/clientEffect/ClientEffectTemplateRW.cpp`
  — the **save** path (TAG_0003 write, ~line 83+) = the byte-exact emit reference + current write version.

### Utinni reuse targets — the three-layer reuse template (live repo)
- **Particle (the named reuse template):**
  `UtinniCoreDotNet/Formats/Particle/{ParticleEffectDocument,ParticleEffectWriter}.cs` (codec/model
  shape), `Utinni.Cli/Commands/RoundtripParticleCommand.cs` (verb shape),
  `Utinni.Mcp/Tools/ReadTools.cs:97-109` (`summarize_particle` — copy template for
  `summarize_clienteffect`), `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormParticleEditor.cs`
  (the Form + the **"Preview in client" hot-retrigger** mechanism to reuse for D-07/D-08).
- **Terrain (the most recent codec+SubPanel analog):**
  `.planning/phases/20-terrain-trn-codec-verbs-mcp/20-CONTEXT.md` (codec/verb/fixture decision
  precedents D-08..D-14), `.planning/phases/21-terrain-tjt-subpanel-best-effort-live-preview/21-CONTEXT.md`
  (SubPanel→Form form factor, MEF-safety, preview disciplines),
  `Utinni.Cli/Commands/DecodeTrnCommand.cs` (the alias-delegation precedent for `decode-effect`),
  `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/SubPanels/TerrainSubPanel.cs` +
  `UI/Forms/FormTerrainEditor.cs` (the thin-SubPanel→roomy-Form idiom, D-04),
  `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs:384-415`
  (`CollapsiblePanel.WrappedSubPanel` discovery for the TRE-Browser hand-off, D-09).

### IFF DOM + save/dispatch infrastructure (the composition surface)
- `UtinniCoreDotNet/Formats/Iff/{IffReader,IffWriter,IffDocument,MutableIffDocument,MutableIffNode}.cs`
  — the captured-slice byte-exact edit engine (no-pad detect; verbatim re-emit of clean nodes;
  parent-FORM length re-stamp for dirty nodes — the D-01 length-ripple mechanism to verify).
- `Utinni.Cli/Commands/DecodeIffCommand.cs` — root-FORM auto-dispatch (add the CLEF branch here,
  D-11).
- `Utinni.Cli/Commands/ApplySaveIffCommand.cs` (`--root` containment ~line 44) — the
  `apply-save-effect` + loose-override save template (D-10).
- `Utinni.Cli/Program.cs` — the `Type[]` `ParseArguments` + `Dispatch` switch (D-12).
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/` — `IffSaveTargets`
  (the `looseOverrideSubDir = "loose"` convention, D-10) + `TerrainSaveTargets.SaveLooseOverride`
  (the folded-todo fix target) + `LooseOverridePath.Resolve`.
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/ClientReloadDispatcher.cs` —
  the game-thread-dispatched, `Game.IsRunning`-gated reload/replay tier + honest candor vocabulary
  (the path to ride for the D-07/D-08 Preview action; do NOT reinvent).
- `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs` — the plugin contract (`GetForms()` /
  `GetStandalonePanels()` / `GetSubPanels()` + the settable undo seam; guard the ctor against MEF
  silent-reject, Pitfall 8).

### Project invariants
- `AGENTS.md` — DEC-C4 (editors ship inside TJT), DEC-V2-VERBS-FIRST, DEC-V2-MCP-OOP, DEC-A3 (no
  standalone renderer; live-in-client only), DEC-C3 / byte-exact gate, IFF no-pad, loose-override
  matrix, format-support reality (both lineages; raw-fallback never hard-aborts).
- `docs/ai/lessons.md` + auto-memory `[[project_rh_snapshot_no_heap_alloc]]` (heap-free hot path,
  `0x0051fb0a`), `[[feedback_winforms_dockfill_zorder]]` (Dock.Fill front-most), Pitfall 8
  (throwing `IEditorPlugin` ctor silently drops the plugin).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **Particle three-layer stack (Phase 15):** `ParticleEffectDocument`/`Writer` model + codec,
  `roundtrip-particle` verb, `summarize_particle` MCP tool, `FormParticleEditor` (incl. the
  "Preview in client" hot-retrigger). The CLEF analogs clone these shapes near-1:1.
- **Terrain SubPanel→Form idiom (Phase 21):** `TerrainSubPanel` → `FormTerrainEditor`, the
  `CollapsiblePanel.WrappedSubPanel` TRE-Browser hand-off, MEF-safe ctor, Dock.Fill/SplitContainer
  ordering — the `EffectsSubPanel` template (D-04).
- **IFF captured-slice DOM:** `MutableIffDocument` + `IffWriter` re-emit clean nodes verbatim and
  re-stamp parent FORM lengths for dirty nodes — the byte-exact foundation for D-01's
  variable-length edits + D-02's add/remove list mutation. The only genuinely new bit vs. terrain.
- **`ClientReloadDispatcher`:** game-thread-dispatched, `Game.IsRunning`-gated reload tier with
  honest candor — the path the D-07 Preview action rides.
- **Verb dispatch + `apply-save-*` `--root` containment + `ReadTools` thin-shell:** proven across
  Phases 8/11/13/14/15/20.

### Established Patterns
- Root-FORM `SubTypeId` auto-dispatch in `decode-iff` (PEFT / TGEN / datatable / OT branch) — CLEF
  slots in as one more branch, giving MCP routing for free.
- Raw-fallback on unknown chunks/versions (Phase 11/15/20 decoders) — the D-13 unknown-version /
  unknown-command safety net.
- IFF no-pad (datatable / stf / particle / terrain) — `IffWriter` already omits the pad; do NOT
  re-implement framing.
- Heap-free push-on-edit dispatch (`dispatchSnapshot`) — the preview hot-path crash guard (D-08).

### Integration Points
- New `ClefDecoder` + `ClientEffect/` model compile into the existing `UtinniCoreDotNet.dll` (no new
  project; `Generated/UtinniCore.cs` NOT touched — pure managed).
- New `effect-*` verbs compile into `utinni-cli.exe`; `summarize_clienteffect` into `Utinni.Mcp`
  (net10) shelling out.
- New `EffectsSubPanel` + `FormClientEffectEditor` compile into `TheJawaToolboxDotNet` (sibling
  UtinniPlugins repo — standing cross-repo write authority; paired commit, no human checkpoint
  except the live smoke). The terrain `loose/` fix (folded todo) also lands in UtinniPlugins.

</code_context>

<specifics>
## Specific Ideas

- User explicitly opted **into** live preview even though the written criteria omit it — they value
  the iterate-and-watch workflow, but chose **manual replay only** (an effect firing on every save
  is disruptive, unlike terrain's quiet regen). Reuse the Particle editor's existing preview path.
- User chose the **fullest edit surface** at every fork: variable-length string edits (D-01), full
  add/remove/reorder list authoring (D-02) — the editor should be genuinely useful for authoring,
  not a glorified viewer. The byte-exact bar rises accordingly; the captured-slice writer is the
  mechanism the planner must prove holds for length-changing edits.
- User chose to **fix the terrain `loose/` bug in this phase** (D-10 / folded todo) — keep the
  whole loose-override save matrix consistent while in the shared plumbing.
- `EffectsSubPanel` named for future growth but **ClientEffect-scoped now** — no speculative
  Lightning/Swoosh container (those are a future milestone).

</specifics>

<deferred>
## Deferred Ideas

- **Generic multi-format Effects container** (host Lightning / Swoosh alongside ClientEffect under
  one `EffectsSubPanel`) — future milestone (ROADMAP §"Effects family"); build the seam when those
  editors are actually designed, not before (D-05).
- **Auto-replay on save** — deliberately not built (D-07); could be a future opt-in toggle if the
  manual flow proves too slow.
- **Resolving** appearance/sound/FF template references (showing the actual resolved asset, not just
  the string) — SWG-side fetch, out of the codec's lane; possible future read-assist.

### Reviewed Todos (not folded)
Surfaced via weak generic-keyword matches (score ≤ 0.6) — off-domain for a ClientEffect editor,
reviewed and NOT folded:
- `phase09-datatable-editor-review-warnings.md` — Phase 9 datatable code-review edges.
- `phase10-stringtable-sc3-live-reload-residual.md` — stringtable live-reload (Phase 10/15 domain);
  is the live-reload-candor *precedent* cited in D-08, but not folded as scope.
- `phase21-terrain-active-flag-ihdr-deeper-nesting.md` — terrain codec IHDR nesting edge (Phase 20/21
  terrain-codec domain), not ClientEffect.
- `swg-window-resize-fullscreen-edge-cases.md` — D3D9/DXGI presentation resize (render-backend
  domain, Phase 18/19/24).

(`phase21-terrain-override-loose-subdir.md` WAS folded — see Folded Todos in `<decisions>`.)

</deferred>

---

*Phase: 22-clienteffect-editor*
*Context gathered: 2026-06-17*
