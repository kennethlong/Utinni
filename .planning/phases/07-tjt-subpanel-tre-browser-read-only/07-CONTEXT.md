# Phase 7: TJT subpanel — TRE Browser (read-only) - Context

**Gathered:** 2026-05-26
**Status:** Ready for planning

<domain>
## Phase Boundary

A **read-only, format-aware browser** over the SWG `.tre` virtual filesystem, shipped as an `IEditorPlugin` WinForms SubPanel **inside The Jawa Toolbox** (per DEC-C4, in the `UtinniPlugins` repo — not a standalone plugin). It surfaces the complete asset graph the client can load and lets the user inspect each file's structure and per-type decoded content. It is the first Wave-1 editor and **establishes the subpanel + format-read pattern that Phases 8-11 build on** (those phases add the *editable* surface on top of the read-only decoders shipped here).

**In scope:** TRE archive enumeration (complete filename graph), virtual-path tree navigation with search/filter, and read-only inspection — including **deep per-type decode** of the major asset types (IFF chunk tree for everything; datatable rows/columns; string-table entries; object-template inherited fields; mesh/skeleton/animation structure).

**Explicitly OUT of scope (deferred — see Deferred Ideas):** any **write / authoring / export** capability. Full Maya-exporter parity (art authoring) is an eventual goal planned for a later milestone and will require consciously re-opening locked anti-goal DEC-A3.
</domain>

<decisions>
## Implementation Decisions

### Scope & Parity Posture
- **D-01:** Phase 7 is **read-only parity** with the SWG asset-inspection surface. Maya-exporter (write/authoring) parity is the **eventual goal, deferred to a later milestone** — not this phase. Re-opening DEC-A3 (LOCKED anti-goal: "Utinni is NOT a Maya/3ds Max replacement") is a conscious milestone-level decision to be made then, not here.
- **D-02:** Use the `swg-client-v2` artifacts as **reference implementations + format spec ported to C#** — NOT a runtime dependency. The Python `swg_blender` code is a reference to read/port, not to ship or shell out to. (User: "I didn't mean we should support python… use the implementation there as a reference… you can use the Python code as a reference implementation.")

### Render Surface
- **D-03:** Ship Phase 7 as a **WinForms `IEditorPlugin` SubPanel** on the existing `FormObjectBrowser` themed-`TreeView` pattern (matches DEC-C4 "dockable UserControl" + the roadmap). This is the pattern Phases 8-11 inherit.
- **D-04:** An **ImGui chromeless HUD-overlay presentation is deferred to optional later polish.** Phase 7 is therefore the explicit **exception to the 06-01 HUD-style overlay directive** (which suits live-play HUD widgets, not a dense data browser). Phases 8-11 should not treat Phase 7 as a binding precedent either way — revisit per panel.

### Data Source & Completeness
- **D-05:** **Hybrid.** Full `.tre` **TOC/name-block enumeration** (read directly, no payload decrypt needed) builds the **complete** filename tree — "everything the client *can* load." The live `Game.Repository` (the `treefile::searchTree`-harvested set) is an **overlay** indicating which entries are currently loaded/resolvable (and a resolution path for content). This satisfies both roadmap clauses: PROD-01's "powered by the `treefile::getAllFilenames` hook" *and* "covers every asset class" / the `parse-tre` golden tie-in.

### Parsing Baseline (TRE/IFF read)
- **D-06:** The C# TRE reader must be **version-dispatching across both client lineages**: SWGEmu Pre-CU **0004/0005/0006** *and* Restoration `swg-client-v2` **v6000 / COT2000**. (User mods/cares about both clients.)
- **D-07:** **v6000 payloads are encrypted** — for those archives, enumeration (TOC/names/metadata/CRC) works but **content preview/decode degrades gracefully** to "enumerate-only; extract via `TreeFileExtractor.exe`." 0005/0006 (SWGEmu) payloads are directly readable.
- **D-08:** **Extend** the existing `UtinniCoreDotNet/Formats/Tre/` reader (add **TOC-only / lazy** enumeration — must not eager-read all payloads; the tree is 100k+ entries) rather than fork a parallel reader, so the browser and the **Phase-4 `parse-tre` CLI share one code path** (success criterion #4). The universal IFF reader at `UtinniCoreDotNet/Formats/Iff/` is the base for chunk-tree inspection and ports cleanly from `swg_iff/reader.py` (IFF is version-agnostic).
- **D-09:** **Reference split for the per-type decoders:** `swg_blender` Python is the strongest reference for **mesh / skeletal mesh / skeleton / animation / shader / vertex-buffer** decode. It does **NOT** implement **datatable / string-table / object-template** — for those the reference is the C++ runtime `*Template.cpp` loaders (`iff-tre-codebase-map.md`) + Utinni's existing `Formats/Iff/` parser.

### Tree Organization
- **D-10:** Primary tree = **SWG virtual-path hierarchy** (`object/creature/...`, `appearance/`, `shader/`, `datatables/`, …), mirroring how modders navigate SWG and reusing the `FormObjectBrowser` `TreeView` approach. **Source `.tre` archive + asset type** shown in the detail pane / as columns.
- **D-11:** **In-tree search / filter is in scope** (non-negotiable given 100k+ entries across two client formats). Exact search UX (substring vs glob, debounce, type facets) is planner discretion.

### File Detail View
- **D-12:** **Deep per-type decode in Phase 7** (user choice, scope-expanding and consciously accepted). On selection, show: metadata header (path / size / source archive / CRC / compression) + a **universal IFF chunk tree** (FORM/chunk tags, sizes, offsets) + a **type/version banner** from the root FORM + **type-specific structured views**: datatable rows/columns, string-table entries, object-template inherited fields, mesh vertex/shader counts, skeleton joint tree. Raw hex peek where payload is readable; graceful enumerate-only for encrypted v6000 (per D-07).
- **D-13:** These read-only decoders are the **foundation Phases 8-11 make editable** — no rework, just add write + editing UI on top. The IFF chunk tree specifically is the surface Phase 8's IFF editor will make editable.

### Claude's Discretion
- **Code placement:** read parsers/decoders extended in framework-side `UtinniCoreDotNet/Formats/` (shared with the `parse-tre`/`inspect-iff` CLI per D-08); this refines DEC-C4's "format code lives in TJT" for the *read* path (Phase 4 already shipped read parsers framework-side). IFF **write** primitives still land in `TheJawaToolboxDotNet`/`TheJawaToolbox` in Phase 8 per DEC-C4.
- **Plan splitting:** Phase 7 is large given D-12. Planner should likely split into multiple plans (e.g. browser shell + TRE enumeration/tree + Repository overlay → then per-type decoders → then detail-view UI). Planner may also flag to the roadmap whether per-type decode warrants a decimal sub-phase.
- Exact search/filter UX, column set, theming, and SubPanel vs StandalonePanel vs Form placement within TJT — standard approaches, planner's call.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### swg-client-v2 reference implementations & format spec (user-directed — read these)
> Sibling repo at `D:/Code/swg-client-v2` (Restoration/whitengold lineage). Paths below are relative to the Utinni repo root.
- `../swg-client-v2/tools/swg_blender/swg_pipeline/tre_reader.py` — **reference impl** for TRE enumeration: `TreHeader`/`TreEntry` dataclasses, TOC + name-block parse, zlib decompression, and the **COT2000** master-index reader. Covers 0004/0005/6000. Port the enumeration logic to C# (D-06/D-08).
- `../swg-client-v2/tools/swg_blender/swg_iff/reader.py` — **reference impl** for IFF navigation (stack-based FORM/chunk, BE headers / LE payloads; mirrors C++ `Iff.cpp`). Version-agnostic; base for the chunk-tree inspector.
- `../swg-client-v2/docs/research/sample-tre-files.md` — authoritative **COT2000 + TRE v6000 binary layout** spec (header offsets, 32-byte TOC entry, name-block reconstruction).
- `../swg-client-v2/docs/research/iff-tre-codebase-map.md` — index of the C++ `Iff`/`TreeFile` loaders, readers, writers + reuse strategy. **The read-path reference for datatable/STF/object-template decode** (D-09).
- `../swg-client-v2/docs/research/maya-exporter-reference.md` — MayaExporter source map (asset-type taxonomy, Writer→Loader pairings). For read-only: the **runtime loaders** are the relevant reference. Primary input for the *deferred* write-parity milestone.
- `../swg-client-v2/docs/research/maya-exporter-parity-checklist.md` — exhaustive MayaExporter feature inventory. **The parity backlog for the deferred write/authoring milestone** (see Deferred Ideas).
- `../swg-client-v2/src/engine/client/application/MayaExporter/` — C++ MayaExporter source (format authority; ~141 files). Read-only relevance is the loader cross-refs; full relevance is the deferred write milestone.

### Existing Utinni assets to extend / reuse (this repo)
- `UtinniCoreDotNet/Formats/Tre/TreFile.cs` (+ `TreHeader.cs`, `TreRecord.cs`, `TreParseException.cs`) — existing 0005/0006 TRE reader; **extend** with TOC-only/lazy enumeration + v6000/COT2000 (D-06/D-08).
- `UtinniCoreDotNet/Formats/Iff/` (`IffReader.cs`, `IffDocument.cs`, `IffChunk.cs`, `IffContainerChunk.cs`, `IffLeafChunk.cs`, `IffParseException.cs`) — existing IFF parser (Phase 4); base for the chunk-tree inspector + per-type decoders.
- `Utinni.Cli` / `Utinni.Cli.Tests` — Phase-4 `parse-tre` / `list-objects` / `inspect-iff` verbs + golden fixtures. Browser must share these code paths (success criterion #4).
- `UtinniCoreDotNet/UI/Controls/SubPanel.cs`, `SubPanelContainer.cs` — SubPanel base (417px fixed-width, FlowLayout) + container the panel registers into.
- `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs` — the MEF SPI the TJT plugin implements (CON-M-01/02).
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormObjectBrowser.cs` — **the closest analog**: existing themed `TreeView` over `Game.Repository` with async load + path reconstruction. Pattern to follow for the TRE Browser tree.
- `UtinniPlugins/.../TheJawaToolboxDotNet/Plugin.cs` — `SubPanelContainer` registration pattern (where the new panel plugs in).
- `UtinniCore/swg/misc/repository.h` + `Generated/UtinniCore.cs` `Repository` binding (`GetAllFilenames`/`GetDirectoryInfo`/`GetFilenameAt`) — the live-harvest overlay source (D-05); use **without** modifying the native hook (CON-N-02).

### Roadmap / project decisions
- `.planning/ROADMAP.md` § Phase 7 — goal, success criteria, preservation guard-rails (CON-M-01/02, CON-T-05, CON-N-02).
- `.planning/PROJECT.md` § Key Decisions — **DEC-C4** (subpanel-inside-TJT, LOCKED) and **DEC-A3** (not-a-DCC-replacement, LOCKED — gates the deferred write milestone).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`FormObjectBrowser` (TJT):** existing themed `TreeView` over `Game.Repository` with async `LoadRepo()` + `AfterSelect` path reconstruction — the direct template for the TRE Browser's tree.
- **`Formats/Tre` + `Formats/Iff` (UtinniCoreDotNet):** existing C# TRE (0005/0006) + IFF parsers from Phase 4; extend rather than rebuild.
- **`Utinni.Cli` (`parse-tre`/`list-objects`/`inspect-iff`):** golden-tested code paths the browse must reuse.
- **`SubPanel`/`SubPanelContainer` + Utinni themed controls:** the WinForms host surface and styling.

### Established Patterns
- `IEditorPlugin` MEF export → `GetStandalonePanels()` / `GetSubPanels()` / `GetForms()` aggregation in `FormMain` (CON-M-01/02).
- `*Impl` separation (CON-T-05) for any native/managed split in TJT.
- Repository harvested-filename set via `treefile::searchTree` detour (CON-N-02) — consume, don't modify the native hook.
- Game-thread vs UI-thread marshaling (`Control.Invoke` / `GameCallbacks.AddMainLoopCall`) for any live-resolution overlay.

### Integration Points
- New panel registers via TJT `Plugin.cs` `SubPanelContainer`.
- TRE mount list comes from the injected client's config (where SWG loads `.tre`); browser reads those archives' TOCs directly + overlays `Game.Repository`.
- Read decoders extend `UtinniCoreDotNet/Formats/`, keeping the `Utinni.Cli` consumer in lock-step (two consumers of one core, per the TEST-03 precedent).

</code_context>

<specifics>
## Specific Ideas

- User wants **read-only parity with what the SOE asset tools / Maya plugin could *show*** — i.e., understand and inspect every asset type — using the `swg-client-v2` research + Python + C++ as reference, ported to C#.
- "Both clients" matters: the browser should be **client-agnostic**, reading whichever `.tre` lineage is present (SWGEmu 0005/0006 *and* Restoration v6000/COT2000).
- Replaces the SOE-era `TreeFileExtractor` browse experience (read side).

</specifics>

<deferred>
## Deferred Ideas

- **Maya-exporter WRITE / authoring parity → later milestone.** Art-asset export (mesh/skeleton/animation/shader/collision/floor/building) per `maya-exporter-parity-checklist.md`. This is the **eventual goal** but contradicts LOCKED **DEC-A3** and overlaps the `swg_blender`/Blender effort — so it needs a conscious milestone/vision decision (route to `/gsd:new-milestone` or a PROJECT.md vision update; re-open DEC-A3 then). The parity checklist is the ready-made backlog.
- **ImGui chromeless HUD-overlay presentation** of the browser — optional later polish (per D-04 / the 06-01 directive), if the in-game aesthetic is wanted.
- **v6000 payload extraction/decrypt** — blocked without `TreeFileExtractor.exe`; enumerate-only for v6000 in V1 (D-07). Revisit if Restoration payload obfuscation is solved.
- **Editable surfaces** for the decoded types — by design these land in Phases 8 (IFF), 9 (datatable), 10 (STF), 11 (object template), built on Phase 7's read-only decoders.

### Reviewed Todos (not folded)
- `gamecallbacks-gc-av-flake-fix.md` — CI-stability flake; already resolved in 06-04. Keyword false-positive match; unrelated to TRE Browser.
- `loader-lock-harness-flake-fix.md` — CI-stability flake; already resolved in 06-04. Keyword false-positive match; unrelated to TRE Browser.

</deferred>

---

*Phase: 7-tjt-subpanel-tre-browser-read-only*
*Context gathered: 2026-05-26*
