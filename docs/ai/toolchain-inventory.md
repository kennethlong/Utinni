# SWG Toolchain Inventory & Utinni Cross-Walk

**Purpose.** Map the original SWG studio toolchain (the ~60 standalone tools in the leaked
client/server source) to **what Utinni already covers**, and classify each remaining tool by the
right strategy: **revive** the original, **replace** it with a Utinni surface, or leave it. This is
the cross-walk that was missing — `swg-client-v2/docs/research/` has a comprehensive *census* of the
tools, Utinni's own docs cover only the *injection framework*, and Core3's docs cover only the
*server runtime*. None of them said "tool → format → Utinni status → decision." This doc does.

Captured 2026-06-01, post-V1 (`v1.0.0`). Build-status of revive candidates is **unverified** — see
[Open items](#open-items).

## Sources already on disk

| Doc | What it has |
|-----|-------------|
| `swg-client-v2/docs/research/swg-tools-and-likely-studio-toolchain.md` | The authoritative **653-line tool census** (every application project + 3rd-party deps + likely studio workflow) |
| `swg-client-v2/docs/research/iff-tre-codebase-map.md` | IFF chunk + TRE archive structure |
| `swg-client-v2/docs/research/maya-exporter-reference.md` | Historical MayaExporter behavior + formats |
| `swg-client-v2/docs/research/blender-iff-interchange-PLAN.md` | Blender-replacement roadmap |
| `swg-client-v2/docs/data-pipeline.{md,html}` | The offline asset → `.iff` → `.tre` → publish pipeline |
| `Utinni/docs/ai/*` | Injection framework only (build, injection, core, bridge, regen-bindings) — **does not** cover the offline toolchain |
| `Core3/docs/*` | Server runtime + `idlc` (IDL→C++) + CMake — **no asset authoring tools** (Core3 *consumes* client `.tre`/`.iff`) |

The original tools live in `swg-client-v2/src/{engine,game}/{client,shared,server}/application/`, built
by `src/build/win32/swg.sln` (VS2022-era MSVC; many tools enumerated in the `_all_tools` metaproject).

## The clean line — two archetypes, two strategies

The 60 tools split cleanly, and the split dictates the strategy:

- **Interactive editors → REPLACE.** GUI/DCC-style editors (terrain, particle, animation, object
  template, datatable…). These are the rewrite-into-Utinni targets: a modern, themed, undo/redo,
  live-injectable SubPanel beats reviving a 2003 MFC/Qt editor. This is exactly what Wave-1 did for
  the five asset-pipeline-core editors, and what Wave-2+ should do for the rest.
- **Build-chain CLIs → REVIVE + WRAP.** Console tools that compile/pack/extract (`TemplateCompiler`,
  `TreeFileBuilder`, exporters…). Reimplementing a template-*definition* compiler or the full TRE
  builder is a large port; these are headless `.exe`s already in `swg.sln`. The cheap, correct path
  is to **get them compiling and shell out / wrap them** — Utinni (or the future MCP server, backlog
  999.1) calls them. Reimplement later only where byte-exact round-trip or live editing demands it.

A useful litmus: *does a human need to see/manipulate the asset?* → editor → replace. *Is it a
deterministic source→binary transform?* → CLI → revive+wrap.

## Cross-walk

### ✅ Subsumed by Utinni V1 (original now redundant)

| Original tool | Format | Utinni replacement |
|---------------|--------|--------------------|
| `ViewIff`, `Viewer` (iff side) | `.iff` | **IFF Editor** (and *editable*, not just view) |
| `DataTableTool` (read/edit) | datatable `.tab`/`.iff` | **Datatable Editor** (+ CSV, find/replace, type-cascade) |
| `StringFileTool`, `UpdateLocalizedStrings`, `WordCountTool` | `.stf` | **String-table Editor** (+ CSV/PO, find/replace) |
| `TreeFileExtractor` | `.tre` | **TRE Browser** (browse + extract; lazy TOC) |
| `TemplateEditor`, `DatabaseObjectViewer` | object template `.iff` | **Object Template Editor** (DERV inheritance view + edit/override/save) |

### 🟡 Partially covered — gaps to close in Utinni

| Original tool | Gap vs Utinni | Note |
|---------------|---------------|------|
| `TreeFileBuilder`, `TreeFileRspBuilder` | Build a `.tre` **from a source tree**; Utinni only **repacks** an existing archive | revive+wrap is the fast path |
| `TemplateCompiler`, `TemplateDefinitionCompiler` | Compile `.tpf` source + `.tpd` schema → `.iff`; Utinni edits existing `.iff` only | **directly tied to the OT Tier-2 follow-up** (see [[project-ot-multichunk-list-params]]) — the typed param map lives in the template *definitions* these compile |
| `DataTableTool` (compile path) | XML/CSV → datatable `.iff` *compile*; Utinni edits existing tables | partial: CSV import exists, full-compile doesn't |
| `WorldSnapshotViewer` | Full snapshot view; Utinni has a Snapshot *save* panel, not a viewer | |

### 🔵 Replace-next — Wave-2 editor candidates (no Utinni coverage)

Interactive editors → rewrite into Utinni SubPanels, roughly by modder demand:

- **Terrain** — `TerrainEditor`, `Turf` (`.trn`)
- **Effects** — `ParticleEditor`, `ClientEffectEditor`, `LightningEditor`, `SwooshEditor` (`.prt`, effect `.iff`)
- **Animation** — `AnimationEditor` (`.lat`/`.ash` state machines, skeletal anim)
- **Shaders/Textures** — `ShaderBuilder`, `CreateShaderTemplate`, `TextureBuilder` (`.dds`, shader templates) *(some of these are also CLI-ish — could be revive+wrap)*
- **Sound** — `SoundEditor`
- **UI** — `UiBuilder` (UI `.iff`)

### 🟢 Revive-as-is (compile + wrap — don't rewrite)

> **Lift-and-shift (locked, v2.0).** Revive by **copying the tool's source + required shared libs into a
> Utinni-owned build location** — do NOT build in-place against the `swg-client-v2` tree or modify it.
> `swg-client-v2` has an **active D3D9→D3D11 migration**; lift-and-shift keeps our build decoupled from
> that churn and out of their way. These revive targets are headless/console and don't need the
> renderer, so the shift is clean (no D3D dependency to drag along).
>
> **Toolchain (shared target — CONFIRMED).** No v143→v145 port: `swg-client-v2` is **already on
> VS2026 / v145 / stdcpp20** (`swg.sln` = `VisualStudioVersion 18.1`; revive targets' `.vcxproj` =
> `PlatformToolset v145`; STLport453 gone) — Utinni's exact toolset. The revive spike is therefore
> **verify standalone v145 build + link**, **strip dead deps** (`TemplateCompiler`/`sharedTemplate` carry
> a dead `perforce/include` path never `#included`; ~25 transitive ProjectReferences to prune), and
> **produce a per-tool dependency manifest**. Status is uneven — `TemplateCompiler` already has built
> v145 objects; `TreeFileBuilder` is v145-configured but unbuilt (unverified). Pin the lifted-from
> `swg-client-v2` SHA (it's actively churning on `koogie-msvc-cpp20-base` + a live `x64bit-Upgrade`
> branch; watch x64 vs Utinni's x86). (Corrected from an earlier "port the delta" overstatement.)

Headless build-chain CLIs; reviving + wrapping fills authoring gaps fastest and feeds the MCP server (999.1):

- `TemplateCompiler` / `TemplateDefinitionCompiler` — `.tpf`/`.tpd` → `.iff`
- `TreeFileBuilder` / `TreeFileRspBuilder` — source tree → `.tre`
- `ArmorExporterTool`, `WeaponExporterTool`, `CoreWeaponExporterTool` — datatable → item `.tpf`
- `Miff` — text → IFF; `LabelHashTool`, `Md5sum`, `VersionNumber` — build utils
- `SwgSchematicXmlParser` — schematic XML → `.tpf`

### 🟣 Server-content tools (Core3 side; likely out of Utinni-client scope)

`SwgConversationEditor`, `SwgDraftSchematicEditor`, `SwgSpaceQuestEditor`, `SwgSpaceZoneEditor`,
`NpcEditor`, `QuestEditor`, `ShipComponentEditor`, `SwgContentBuilder`, `SwgNameGenerator`,
`SwgBattlefieldTool`. These author *server* content; a Utinni story here would be a later, separate
milestone (and overlaps Core3's domain).

### ⚪ Superseded elsewhere / ignore

| Tool(s) | Disposition |
|---------|-------------|
| `MayaExporter` | **Being replaced by `swg-blender-plugin`** — see below |
| `Direct3d11` | Incomplete D3D11 migration; relevant to Utinni's tracked D3D11-migration future ([[project-d3d11-migration]]), not a tool to revive |
| `SwgClient`, `SwgGodClient`, `SwgHeadlessClient`, `Headless`, `LaunchMeFirst` | Client variants, not tools |
| `P4Qt`, Perforce/Alienbrain hooks, `BugTool`, `CrashReporter`, `RemoteDebugTool`, `AddressToLine`, `SwgCsTool` | SOE-internal infra; dead/irrelevant |

## Maya → Blender export path (already in progress)

The 3D-asset **export/authoring** path that `MayaExporter` (Maya 7 + Alienbrain, unbuildable today)
covered is being **reviewed-and-replaced** by **`D:/Code/swg-blender-plugin`** — a modern Python +
Blender suite:

- **`swg_iff`** — pure-Python IFF/TRE parsing (the format core)
- **`swg_blender`** — import/export for static + skeletal + animation
- **`swg_blender_addon`** — Blender File-menu + View3D sidebar UI (v0.2)
- **`swg_pipeline/rsp_builder.py`** — reimplements the `TreeFileRspBuilder` `.rsp` manifest format;
  `export_bundle.py` builds a client test bundle + `client_search_paths.cfg`
- Formats: `.msh`, `.mgn`, `.skt`, `.lod`, `.pob`, `.sat`, `.apt`, `.lmg`, `.ans`
- Phased (Phase 5–7 verification docs), pytest + golden fixtures; an older community addon
  `io_scene_swg_msh` (`io_scene_swg`) sits alongside as prior art.

**Implication for Utinni:** Utinni should NOT try to own 3D mesh/skeleton/anim authoring — that's
Blender's job and is well underway. Utinni stays the **binary/format + live-injection** tool
(TRE/IFF/datatable/stf/object-template, live preview); the Blender suite owns DCC authoring. The two
meet at the file formats (both build on the same `.iff`/`.tre` understanding).

**Appearance-preview decision (locked):** do NOT build a standalone mesh/appearance renderer (the path
Sytner's IFF Editor took — its own SWG-format renderer reused from his world editor). Utinni's visual
preview = **live in-client via the real SWG engine** (uniquely enabled by the injection model — perfect
fidelity, no format-chasing); offline 3D viewing stays Blender's lane. This is the differentiator a
standalone editor structurally can't match. (From the 2026-06-02 SIE feature comparison.)

## Strategic next steps

1. **Verify build status** of the top revive candidates — try compiling `TemplateCompiler` and
   `TreeFileBuilder` from `swg.sln`. "Get compiling" is half the question; confirm they're live before
   committing to wrap vs. reimplement.
2. **Close the OT `.tpf`-compile gap** by reviving `TemplateCompiler` (+ `TemplateDefinitionCompiler`)
   — this also yields the per-class param→type map that OT Tier-2 typed display needs.
3. **Revive `TreeFileBuilder`** for build-from-source `.tre` (Utinni only repacks today).
4. Both feed the **MCP server (backlog 999.1)** as write/build tools an agent can call.
5. Pick the first **Wave-2 interactive editor** to replace (terrain or particle are high modder-demand).

## Open items

- Build-status of every "Buildable: unknown" tool is unverified (the Explore sweep read source, not
  build output). A compile pass against `swg.sln` is the next concrete step.
- The server-content tools (🟣) need a scope decision: Utinni-client vs. Core3-side vs. out-of-scope.

---
*Generated from a two-agent Explore sweep of `swg-client-v2` (~60 application projects), Core3, and the
three docs folders, cross-referenced against Utinni V1 coverage. The `.html` peer of this doc is not yet
generated; regenerate via the docs build if needed.*
