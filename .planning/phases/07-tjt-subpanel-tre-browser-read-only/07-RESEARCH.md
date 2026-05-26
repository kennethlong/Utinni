# Phase 7: TJT subpanel — TRE Browser (read-only) - Research

**Researched:** 2026-05-26
**Domain:** SWG `.tre` virtual-filesystem enumeration + IFF/per-type binary decode + WinForms `IEditorPlugin` subpanel inside The Jawa Toolbox
**Confidence:** HIGH (existing code + real fixtures verified by direct byte-level decode; reference impls read in full)

## Summary

Phase 7 ships the first Wave-1 editor: a read-only, format-aware browser over the SWG `.tre` virtual filesystem, shipped as an `IEditorPlugin` WinForms SubPanel **inside** The Jawa Toolbox (DEC-C4, `UtinniPlugins` repo). The format readers it consumes live framework-side in `UtinniCoreDotNet/Formats/` and are shared with the Phase-4 `utinni-cli` (success criterion #4, TEST-03 precedent). The phase is an *extend, don't fork* job on three existing assets: the `Formats/Tre/` reader, the `Formats/Iff/` reader, and the `FormObjectBrowser` themed-`TreeView` UI pattern.

Research surfaced **three concrete, verified problems the planner MUST address** that are not visible from the phase description: (1) the existing `Formats/Tre/TreFile.cs` per-record info-struct field order is **size-first** (`dataSize, dataOffset, dataCompression, dataCompressedSize, checksum, nameOffset`), but the SWG engine's canonical `SearchTree::TableOfContentsEntry` — which v6000/COT2000 follows — is **crc-first** (`crc, length, offset, compressor, compressedLength, fileNameOffset`); these are different layouts, proven by decoding a real `D:\Sample-TRE-Files\SwgRestoration_06.tre`. (2) The existing reader uses `System.IO.Compression.DeflateStream` which expects **raw deflate**, but real `.tre` blocks are **zlib RFC1950-framed** (`0x78 0x9c` header) — so the current deflate path works only on the synthesized fixtures (authored as raw deflate) and would FAIL on every real archive. (3) **No `5000`-version fixture or layout spec exists anywhere** — not in `D:\Sample-TRE-Files\` (all 46 archives are v6000), not in `swg-client-v2`, not in Utinni; `5000` must be implemented defensively (recognized tag → structural sibling of 6000, content degrades to enumerate-only) without asserting a layout.

The good news: `D:\Sample-TRE-Files\` (the full 46-archive SwgRestoration COT2000 set, 213,086 indexed paths) **exists on this machine right now** and is a real v6000/COT2000 golden-fixture source; `swg-main/serverdata` (if present) supplies ~48k loose readable IFF/MESH/STF for per-type decoder fixtures; and the `swg-client-v2` Python (`tre_reader.py`, `swg_iff/reader.py`) + C++ engine loaders (`DataTable.cpp`, `*Template.cpp`) are read-only format-spec references to port to C#.

**Primary recommendation:** Split into ~4 plans — (P1) TRE reader version-dispatch refactor + zlib fix + lazy/TOC-only enumeration, shared with CLI; (P2) the subpanel shell + virtual-path `TreeView` + search/filter + `Game.Repository` overlay; (P3) the detail pane: universal IFF chunk tree + type/version banner + metadata header; (P4) per-type structured decoders (datatable/STF/object-template from C++ engine loaders; mesh/skeleton/anim from `swg_blender`). Gate every TRE/IFF code path behind golden tests that run in the existing CI lanes, using `D:\Sample-TRE-Files` and synthesized fixtures.

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Phase 7 is **read-only parity** with the SWG asset-inspection surface. Maya-exporter (write/authoring) parity is the eventual goal, deferred to a later milestone. Re-opening DEC-A3 is a conscious milestone-level decision, not made here.
- **D-02:** Use `swg-client-v2` artifacts as **reference implementations + format spec ported to C#** — NOT a runtime dependency. Python `swg_blender` is a reference to read/port, not to ship or shell out to.
- **D-03:** Ship as a **WinForms `IEditorPlugin` SubPanel** on the `FormObjectBrowser` themed-`TreeView` pattern (DEC-C4 dockable UserControl). Pattern Phases 8-11 inherit.
- **D-04:** ImGui chromeless HUD-overlay presentation is **deferred to optional later polish.** Phase 7 is the explicit exception to the 06-01 HUD directive. Phases 8-11 should not treat Phase 7 as a binding precedent either way.
- **D-05:** **Hybrid data source.** Full `.tre` TOC/name-block enumeration (read directly, no payload decrypt) builds the **complete** filename tree. Live `Game.Repository` (the `treefile::searchTree`-harvested set) is an **overlay** indicating which entries are currently loaded/resolvable.
- **D-06:** C# TRE reader must be **version-dispatching across both lineages**: SWGEmu Pre-CU **0004/0005/0006** AND newer/Restoration **5000 / 6000 / COT2000**.
- **D-06b:** **`5000` is in scope.** Recognized tag, but NO reference impl/fixture/layout spec exists. Source a fixture/spec, or implement defensively as a structural sibling of 6000 pending one. Don't assert 5000 layout without a fixture.
- **D-07:** **v6000 (and presumably 5000) payloads are encrypted/obfuscated** — enumeration works but content preview/decode degrades gracefully to "enumerate-only; extract via `TreeFileExtractor.exe`." 0004/0005/0006 payloads are directly readable.
- **D-08:** **Extend** the existing `UtinniCoreDotNet/Formats/Tre/` reader (add TOC-only/lazy enumeration — must not eager-read all payloads; 100k+ entries) + v6000/COT2000 — so browser and Phase-4 `parse-tre` CLI share one code path. The universal IFF reader at `Formats/Iff/` is the base for chunk-tree inspection.
- **D-09:** **Reference split for per-type decoders:** `swg_blender` Python = strongest reference for mesh / skeletal mesh / skeleton / animation / shader / vertex-buffer decode. For datatable / string-table / object-template the reference is the C++ runtime `*Template.cpp` / loader (`iff-tre-codebase-map.md`) + Utinni's `Formats/Iff/` parser.
- **D-10:** Primary tree = **SWG virtual-path hierarchy** (`object/creature/...`, `appearance/`, `shader/`, `datatables/`, …), reusing the `FormObjectBrowser` `TreeView` approach. Source `.tre` archive + asset type shown in detail pane / as columns.
- **D-11:** **In-tree search/filter is in scope** (non-negotiable, 100k+ entries). Exact UX (substring vs glob, debounce, type facets) is planner discretion.
- **D-12:** **Deep per-type decode in Phase 7** (scope-expanding, consciously accepted). On selection: metadata header + universal IFF chunk tree + type/version banner + type-specific structured views; raw hex peek where payload readable; graceful enumerate-only for encrypted v6000.
- **D-13:** These read-only decoders are the **foundation Phases 8-11 make editable** — no rework, just add write + editing UI. The IFF chunk tree is the surface Phase 8's IFF editor makes editable.

### Claude's Discretion

- **Code placement:** read parsers/decoders extended in framework-side `UtinniCoreDotNet/Formats/` (shared with the `parse-tre`/`inspect-iff` CLI per D-08). IFF **write** primitives still land in `TheJawaToolboxDotNet`/`TheJawaToolbox` in Phase 8 per DEC-C4.
- **Plan splitting:** Phase 7 is large given D-12 — split into multiple plans (browser shell + TRE enumeration/tree + Repository overlay → per-type decoders → detail-view UI). Planner may flag to the roadmap whether per-type decode warrants a decimal sub-phase.
- Exact search/filter UX, column set, theming, and SubPanel vs StandalonePanel vs Form placement within TJT — planner's call.

### Deferred Ideas (OUT OF SCOPE)

- **Maya-exporter WRITE / authoring parity → later milestone** (contradicts LOCKED DEC-A3; route to `/gsd:new-milestone`).
- **ImGui chromeless HUD-overlay presentation** of the browser — optional later polish.
- **v6000 payload extraction/decrypt** — blocked without `TreeFileExtractor.exe`; enumerate-only for v6000 in V1 (D-07).
- **Editable surfaces** for the decoded types — by design these land in Phases 8 (IFF), 9 (datatable), 10 (STF), 11 (object template).

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PROD-W1-TRE | Read-only browser over `.tre` virtual filesystem; replaces SOE `TreeFileExtractor`; surfaces the asset graph the client loads | TRE version-dispatch reader (extend `Formats/Tre/`) + virtual-path `TreeView` (`FormObjectBrowser` pattern) + `Game.Repository` overlay + CLI golden tests against `D:\Sample-TRE-Files` |
| PROD-01 | Browse every `.tre`, IFF, datatable, template, UI page, shader, string-table entry the client can load; powered by the `treefile::getAllFilenames` hook | Hybrid (D-05): full `.tre` TOC enumeration = "can load"; `Game.Repository.getAllFilenames` (harvested via `treefile::searchTree` detour, CON-N-02) = "currently loaded" overlay. Asset-class coverage proven by IFF chunk tree (universal) + per-type decoders (D-09/D-12) |

## Project Constraints (from preservation set + STATE)

The planner must verify every plan honors these (treat as LOCKED):

- **CON-M-01/02 — MEF SPI shape.** New panel implements/extends `IEditorPlugin` (`InheritedExport`). `GetSubPanels()` / `GetStandalonePanels()` / `GetForms()` aggregation returning null where unused is the clean low-coupling SPI; preserve it. Do NOT widen the interface.
- **CON-T-05 — Jawa Toolbox `*Impl` separation.** Any native/managed split in TJT follows the canonical `*Impl` factoring. (Phase 7 is mostly managed; the only native surface touched is the read-only `Game.Repository` consumption.)
- **CON-N-02 — `utinni::` thin-wrapper firewall + `treefile::getAllFilenames` hook.** Consume the harvested filename set via the existing `Repository` binding **without modifying the native `hkSearchTree` detour** (`tree_file.cpp`). This is read-only consumption of an existing seam.
- **CON-N-08 — `PluginManager` pImpl.** Not touched; do not introduce STL across the DLL boundary.
- **CON-M-05 — `UndoRedoManager.OnCleanupCallback`.** Phase 7 is read-only (no undo commands), so this is dormant here, but Phase 8+ inherits the surface.
- **GSD worktrees OFF for this repo** (`workflow.use_worktrees=false`, STATE memory): run single-plan C++/build waves INLINE on the main tree. Note Phase 7 is a cross-repo phase (format code in Utinni, UI in UtinniPlugins) — see the cross-repo write-authority memory: cross-repo paired commits do NOT need human-action checkpoints (only the live-SWG smoke does).
- **GSD grep-gate hygiene** (memory): plan acceptance "grep X returns zero matches" is literal — word source comments to avoid gated tokens.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `.tre` TOC/name-block enumeration ("can load") | Framework C# (`UtinniCoreDotNet/Formats/Tre`) | CLI (`Utinni.Cli`) | Pure file-format read; shared by CLI + browser per D-08/TEST-03. No game process needed. |
| Live mounted-filename set ("currently loaded") | Native `utinni::Repository` (already built) | Managed `Game.Repository` binding | Harvested from the running client's `searchTree` calls; only meaningful in an injected session. CON-N-02. |
| IFF chunk-tree parse | Framework C# (`UtinniCoreDotNet/Formats/Iff`) | CLI (`inspect-iff`) | Universal, version-agnostic, pure read; base for every per-type decoder. |
| Per-type decode (datatable/STF/template/mesh/skeleton) | Framework C# (`Formats/` new decoders) | — | Pure read over the IFF parse output; CLI-testable. |
| Virtual-path tree + search/filter UI | Managed WinForms (TJT `TheJawaToolboxDotNet`) | — | UI host; `FormObjectBrowser`/`SubPanel` pattern. DEC-C4. |
| Detail pane (metadata + chunk tree + structured views) | Managed WinForms (TJT) | Framework decoders | Renders framework decode output; no business logic in UI. |
| UI ↔ game-thread marshaling for overlay | Managed (`Control.Invoke` / `GameCallbacks`) | — | `Game.Repository` populated on the game thread at install; marshal reads. |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.IO.Compression.DeflateStream` | .NET Framework 4.7.2 (BCL) | Inflate `.tre` deflate/zlib blocks | Already used by `TreeFile.cs`; in-box, no new dep. **MUST be fed raw deflate** — strip the 2-byte zlib header for real archives (see Pitfall 2). [VERIFIED: dotnet/runtime#16923, decoded `SwgRestoration_06.tre` directly] |
| `System.Windows.Forms.TreeView` | .NET Framework 4.7.2 (BCL) | Virtual-path hierarchy tree | The `FormObjectBrowser` already uses it; themed via `Colors.*`. [VERIFIED: codebase `FormObjectBrowser.cs`] |
| `UtinniCoreDotNet.Formats.Tre` / `.Iff` | in-repo (Phase 4) | TRE + IFF readers to EXTEND | D-08 mandates extend-not-fork; CLI shares the path. [VERIFIED: codebase] |
| `CommandLine` (CommandLineParser) | as pinned in `Utinni.Cli` | CLI verb dispatch | Already the CLI's parser; new/extended verbs reuse it. [VERIFIED: `Program.cs`] |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Threading.Tasks` | BCL | async tree load (avoid UI freeze on 100k+ entries) | `FormObjectBrowser.LoadRepo()` already uses `async Task` + `await Task.Delay` polling `Game.IsRunning`. [VERIFIED] |
| Newtonsoft.Json (via `JsonOutput`) | as pinned in `Utinni.Cli` | CLI sorted-key JSON envelopes for golden tests | Only in the CLI layer — the `Formats/` parsers stay JSON-free (parser purity rule, `IffReader` docstring). [VERIFIED] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| BCL `DeflateStream` + manual zlib-header strip | A managed zlib lib (e.g. `System.IO.Compression.ZLibStream`) | `ZLibStream` does NOT exist in .NET Framework 4.7.2 (added in .NET 6). Header-strip on `DeflateStream` is the in-box answer; avoids a new dependency. [VERIFIED: dotnet/runtime#2236, #16923] |
| Eager full-payload read (current `TreeFile.cs` model) | TOC-only lazy enumeration | D-08 requires lazy: 213,086 entries across 46 archives (~5.5 GB) cannot be eager-read. Enumerate TOC+names only; read a single payload on demand for the detail view. |
| Porting the Python `swg_blender` reader wholesale | Targeted C# port of just the algorithm | D-02: reference only, not a runtime dep, no Python at runtime. Port the layout/algorithm, keep Utinni's original code (MIT, no copied identifiers). |

**Installation:** No new NuGet packages required. Phase 7 extends existing in-repo projects and consumes BCL types only.

**Version verification:** No external packages added — nothing to slopcheck. (`DeflateStream` and `TreeView` are BCL; `Formats/Tre`, `Formats/Iff`, `Utinni.Cli` are in-repo.)

## Package Legitimacy Audit

No external packages are installed by Phase 7. The phase consumes only:
- BCL types (`System.IO.Compression`, `System.Windows.Forms`, `System.Threading.Tasks`).
- In-repo projects (`UtinniCoreDotNet`, `Utinni.Cli`, `TheJawaToolboxDotNet`).
- Pre-existing pinned deps (`CommandLineParser`, `Newtonsoft.Json`) already vetted in Phase 4.

**Packages removed due to slopcheck [SLOP] verdict:** none (no install step).
**Packages flagged as suspicious [SUS]:** none.

## Architecture Patterns

### System Architecture Diagram

```
                          ┌─────────────────────── DATA SOURCES ───────────────────────┐
                          │                                                              │
  Disk: *.tre archives    │   Injected SWG client (live session only)                    │
  + master .toc/.tre      │   native utinni::Repository (built at Game::install)         │
        │                 │        ← treefile::searchTree detour harvests mounted names  │
        │                 │          (CON-N-02 — consume, don't modify)                  │
        ▼                 │                          │                                   │
 ┌──────────────────┐     │                          ▼                                   │
 │ TRE reader        │    │            Game.Repository (managed binding)                  │
 │ (Formats/Tre,     │    │            GetDirectoryInfo / GetFilenameAt / FilenameCount   │
 │  version-dispatch)│    └──────────────────────────┬───────────────────────────────────┘
 │ 0004/0005/0006    │                               │
 │ 5000(defensive)   │   "everything client CAN load"│ "currently loaded" overlay
 │ 6000 / COT2000    │                               │
 └────────┬──────────┘                               │
          │ enumerate TOC + names (lazy, no payload)  │
          ▼                                           ▼
   ┌──────────────────────────────────────────────────────────┐
   │  TreeModel: merge full enumeration + loaded-overlay flag    │
   │  keyed by SWG virtual path (object/…, appearance/…, …)      │
   └───────────────────────────┬─────────────────────────────────┘
                               │ (shared core — also feeds utinni-cli parse-tre/list-objects)
            ┌──────────────────┴───────────────────┐
            ▼                                       ▼
  ┌───────────────────┐                  ┌────────────────────────────┐
  │ WinForms SubPanel  │  AfterSelect →   │  Detail pane                │
  │ TreeView (D-10)    │  (path → entry)  │  • metadata header          │
  │ + search/filter    │                  │  • universal IFF chunk tree │
  │ (D-11)             │                  │    (Formats/Iff)            │
  └───────────────────┘                  │  • type/version banner      │
                                          │  • per-type structured view │
  on-demand single payload read           │    (datatable/STF/template/ │
  ─────────────────────────────────────►  │     mesh/skeleton — D-09)   │
  0004/5/6: readable → decode              │  • v6000: enumerate-only    │
  6000/5000: encrypted → enumerate-only    │    "extract via TreeFile-   │
                                          │     Extractor.exe" (D-07)   │
                                          └────────────────────────────┘
```

A reader can trace the primary use case: a `.tre` (or all mounted archives) → version-dispatched TOC/name enumeration → virtual-path tree → user selects an entry → metadata + IFF chunk tree + type-specific decode (or graceful enumerate-only banner for encrypted v6000).

### Recommended Project Structure

```
UtinniCoreDotNet/Formats/
├── Tre/
│   ├── TreFile.cs            # EXTEND: version dispatch + lazy/TOC-only + zlib fix
│   ├── TreHeader.cs          # EXTEND: add toc/name field semantics (engine layout)
│   ├── TreRecord.cs          # EXTEND or split: crc-first vs size-first record decode
│   ├── TreVersion.cs         # NEW: enum/dispatch {V0004,V0005,V0006,V5000,V6000}
│   ├── CotMasterIndex.cs     # NEW: COT2000 + SearchTOC master-index reader (D-05)
│   └── TreParseException.cs  # extend error kinds (UnsupportedVersion already exists)
├── Iff/                      # reuse as-is (base for chunk tree + decoders)
└── Decoders/                 # NEW (D-09/D-12) — pure read over IffDocument
    ├── DataTableDecoder.cs   # DTII → COLS/TYPE/ROWS  (ref: engine DataTable.cpp)
    ├── StringTableDecoder.cs # STF  (ref: LocalizedStringTableReaderWriter.cpp)
    ├── ObjectTemplateDecoder.cs # inherited-field walk (ref: ObjectTemplate.cpp)
    └── AppearanceSummary.cs  # MESH/SKMG/SKTM/KFAT counts (ref: swg_blender)

UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/
├── SubPanels/TreBrowserPanel.cs   # NEW SubPanel (or Form) — registers in Plugin.cs
└── Forms/  (FormTreBrowser.cs if Form placement chosen — planner's call, D-03)
```

### Pattern 1: Version-dispatched TRE record decode (the central refactor)

**What:** The TRE per-record TOC entry layout differs between lineages. The reader must dispatch on the 4-char version tag and select the correct struct decode + stride.
**When to use:** Always, in the extended `Formats/Tre` reader. This is the #1 risk item.
**Example (verified field orders):**
```
// Source: decoded D:\Sample-TRE-Files\SwgRestoration_06.tre (v6000) +
//         Utinni\Utinni.Cli.Tests\Fixtures\tre\synthesized-3record-v0005.tre
//
// ENGINE / v6000 / COT2000 canonical SearchTree::TableOfContentsEntry (32-byte stride for 6000):
//   uint32 crc; int32 length(uncompressed); int32 offset; int32 compressor;
//   int32 compressedLength; int32 fileNameOffset; + 8 bytes padding (6000/COT2000 only)
//
// UTINNI'S EXISTING synthesized 0005 fixture (24-byte stride) decodes coherently ONLY as:
//   int32 dataSize(uncompressed); int32 dataOffset; int32 dataCompression;
//   int32 dataCompressedSize; int32 checksum; int32 nameOffset
//
// → These are DIFFERENT field orders. The existing reader's order is what its OWN fixtures
//   were authored to. The planner MUST decide the canonical order for real 0004/0005/0006
//   (verify against a real SWGEmu .tre fixture — see Open Question 1) and version-dispatch.
```

### Pattern 2: zlib-framed block inflate (the compression fix)

**What:** Real `.tre` deflate blocks are zlib RFC1950-framed (`0x78 0x9c`...+ trailing Adler32). `DeflateStream` expects raw deflate.
**When to use:** Every compressed block (TOC, names, payload) in real archives.
**Example:**
```
// Source: dotnet/runtime#16923; decoded SwgRestoration_06.tre TOC = 0x78 0x9c (zlib),
//         Utinni synthesized fixture payload = 0x0b 0xc9 (raw deflate).
// Detect framing: if first byte == 0x78 (and (b0<<8|b1) % 31 == 0) → zlib: skip 2 header bytes,
// feed remainder to DeflateStream, ignore the trailing 4-byte Adler32.
// Otherwise → already raw deflate (Utinni's synthesized fixtures), feed directly.
// Keep BOTH paths so existing CLI golden fixtures keep passing while real archives also work.
```

### Pattern 3: SubPanel registration inside TJT (DEC-C4)

**What:** New panel is added to a `SubPanelContainer` in `Plugin.cs`, or as a Form in the `forms` list. `GetSubPanels()` currently returns null; `GetStandalonePanels()` returns the container list.
**When to use:** Plan that wires the panel into TJT.
**Example:**
```csharp
// Source: D:\Code\UtinniPlugins\...\TheJawaToolboxDotNet\Plugin.cs (verbatim pattern)
panels.Add(new SubPanelContainer("Controls", new SubPanel[] {
    new ScenePanel(this, hotkeyManager, ini),
    /* ... existing ... */
    new TreBrowserPanel(this, ini),   // NEW — or add FormTreBrowser to `forms`
}));
// SubPanel base is 417px fixed width, FlowLayout host. (UtinniCoreDotNet/UI/Controls/SubPanel.cs)
```

### Pattern 4: virtual-path tree + async load + AfterSelect path reconstruction

**What:** Build a nested `TreeNode` hierarchy by splitting virtual paths on `/`; reconstruct the full path on `AfterSelect` by walking parents.
**When to use:** The browser tree. Differs from `FormObjectBrowser` in TWO ways: (a) it must cover ALL directories, not just `object/`, and ALL extensions, not just `.iff`; (b) source is the full TRE enumeration (not just `Game.Repository`).
**Example:**
```csharp
// Source: FormObjectBrowser.cs LoadRepo() + tvDirectories_AfterSelect (pattern to follow)
// Path reconstruction (AfterSelect): walk curNode.Parent up to root, prepend each .Text + "/".
// Async: `await Task.Delay(1)` while `!Game.IsRunning` for the live overlay; the TRE
// enumeration itself does NOT require a running game (it reads disk) — so the tree can
// populate from disk first and overlay "loaded" flags once Game.IsRunning.
```

### Pattern 5: per-type decode dispatch by root FORM

**What:** After IFF parse, dispatch on the root container's `SubTypeId` (or known extension) to a structured decoder.
**When to use:** Detail pane type-specific views (D-12).
**Example (datatable — verified against engine source):**
```
// Source: swg-client-v2 .../sharedUtility/src/shared/DataTable.cpp load_0000/load_0001
// Root: FORM "DTII" → FORM "0000"|"0001" →
//   chunk COLS: int32 numCols, then numCols null-terminated strings (column names)
//   chunk TYPE: int32 per col (0000) OR format string per col (0001) — i/f/s/...
//   chunk ROWS: int32 numRows, then numRows*numCols cells
// IFF chunk scalars are LITTLE-endian; block tag+length are BIG-endian (Formats/Iff already
// reads tags BE; cell payloads need LE reads — Formats/Iff leaf .Data is raw bytes, decode LE).
```

### Anti-Patterns to Avoid

- **Eager-reading all payloads.** The existing `TreeFile.Open` eager-reads every record's compressed bytes (`RecordCompressedBytes[][]`). For 213k entries / 5.5 GB this is fatal. D-08: enumerate TOC+names only; read one payload on demand. The lazy refactor must keep the CLI's `parse-tre` (which only emits metadata) fast.
- **Assuming one record-struct layout.** Proven different between Utinni's fixtures and the engine — version-dispatch is mandatory, do not reuse the 0005 loop for 6000.
- **Feeding zlib-framed bytes to `DeflateStream`.** Throws/garbles. Strip the 2-byte header.
- **Asserting a 5000 layout.** No fixture exists. Recognize the tag; degrade to enumerate-only or treat as structural sibling of 6000 *behind a clearly-flagged, fixture-gated code path*.
- **Naive `OBJS` byte-scan for structured decode.** `ListObjectsCommand` uses a provisional byte-scan (REVIEWS MEDIUM-13 debt). Phase 7's per-type decoders should go through the real `IffReader`, not re-introduce sentinel scanning.
- **Putting JSON/Console in the parsers.** `Formats/` stays serialization-free (parser-purity rule). JSON lives only in `Utinni.Cli`.
- **Modifying the native `hkSearchTree` detour.** CON-N-02 — consume `Game.Repository` read-only.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| IFF chunk parsing | A new chunk walker | Existing `Formats/Iff/IffReader` | Already EA-IFF-85 correct, bounds-checked, golden-tested (Phase 4); base for all decoders (D-08). |
| Deflate inflation | A custom inflate | BCL `DeflateStream` + zlib-header strip | In-box; only the framing wrapper needs handling. |
| TRE TOC/name enumeration algorithm | Reverse-engineer from scratch | Port the algorithm from `tre_reader.py` + `sample-tre-files.md` offsets | Reference impl already covers 0004/0005/6000/COT2000/SearchTOC layouts (D-02). |
| Datatable/STF/object-template decode | Guess the chunk layout | Port from engine `DataTable.cpp` / `LocalizedStringTableReaderWriter.cpp` / `ObjectTemplate.cpp` (D-09) | These are the authoritative loaders; `swg_blender` does NOT implement them. |
| Mesh/skeleton/anim structure | Guess vertex/joint layout | Port from `swg_blender` mesh/skeleton readers (D-09) | Strongest reference for graphics asset decode. |
| Virtual-path tree + search UI | A new tree control | `FormObjectBrowser` `TreeView` pattern | Proven themed pattern Phases 8-11 inherit (D-03/D-10). |
| Live mounted-filename harvest | A new hook | Existing `Game.Repository` binding | Already harvested via `treefile::searchTree` detour (CON-N-02). |

**Key insight:** Almost everything in Phase 7 is *port + extend + wire*, not *invent*. The two genuinely new pieces of engineering are (1) the version-dispatch + zlib + lazy refactor of the TRE reader and (2) the per-type decoder set — and both have authoritative references to port from.

## Runtime State Inventory

> Phase 7 is NOT a rename/refactor/migration phase — it is greenfield feature work that *extends* existing readers. A full runtime-state inventory is not applicable. The one runtime-state-adjacent concern (the live-harvested filename set) is documented here for completeness:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — Phase 7 reads `.tre` archives + a live in-memory filename set; writes nothing. | None |
| Live service config | `utinni::Repository` is built once at `Game::install` from the *destructive* one-shot `treefile::getAllFilenames()` (moves out of + clears the static set). It captures only what `searchTree` mounted. | Consume read-only via `Game.Repository`; do NOT call `getAllFilenames()` a second time (returns empty). Verified in `repository.cpp` + `tree_file.cpp`. |
| OS-registered state | None. | None |
| Secrets/env vars | `SWG_SAMPLE_TRE_DIR` (optional, points at `D:\Sample-TRE-Files`) is a *test-fixture* path hint only, not a runtime secret. | Document for golden-test setup; not required at runtime. |
| Build artifacts | The cross-repo TJT pin (Phase 06-06 pinned TJT in the Utinni release). A new TJT panel changes the TJT build output. | Rebuild TJT; cross-repo paired commit (write-authority memory — no human checkpoint needed except live-SWG smoke). |

**Verified by:** reading `UtinniCore/swg/misc/repository.cpp`, `tree_file.cpp`, and the `Game.Repository` managed binding.

## Common Pitfalls

### Pitfall 1: TRE per-record field-order divergence (existing reader vs engine/v6000)
**What goes wrong:** Reusing the existing 0005/0006 record-parse loop for v6000 yields garbage offsets/sizes; or "fixing" the existing reader to the engine order breaks the Phase-4 CLI golden fixtures.
**Why it happens:** Utinni's synthesized fixtures were authored to a **size-first** struct (`dataSize, dataOffset, dataCompression, dataCompressedSize, checksum, nameOffset`); the engine's canonical `SearchTree::TableOfContentsEntry` (which 6000/COT2000 follow) is **crc-first** (`crc, length, offset, compressor, compressedLength, fileNameOffset`). Proven by decoding both a real v6000 archive and the synthesized fixture.
**How to avoid:** Version-dispatch the record decode. Treat the existing size-first decode as the (fixture-validated) 0005/0006 path *only until a real SWGEmu fixture confirms the true 0004/0005/0006 engine order* (Open Question 1); decode 6000/COT2000 with the crc-first + 32-byte-stride layout. Keep the CLI golden fixtures green by not changing the path their fixtures exercise without re-authoring them.
**Warning signs:** Decoded names resolve to garbage; offsets point inside the header; `compressor` reads as 13/44.

### Pitfall 2: zlib RFC1950 framing vs raw deflate (`DeflateStream`)
**What goes wrong:** `DeflateStream.Decompress` throws `InvalidDataException` ("incorrect header check") on real `.tre` blocks; or you "fix" it and break the synthesized fixtures (which are raw deflate).
**Why it happens:** Real archives use zlib (`0x78 0x9c` + Adler32 trailer); `DeflateStream` on .NET Framework 4.7.2 only accepts raw deflate. The synthesized fixtures happen to be raw deflate.
**How to avoid:** Detect framing (first byte 0x78 and `(b0<<8|b1)%31==0` ⇒ zlib): strip 2 header bytes, ignore trailing 4-byte Adler32. Keep a raw-deflate fallback for existing fixtures.
**Warning signs:** "incorrect header check"; works in CI (synthesized fixtures) but fails on `D:\Sample-TRE-Files`.

### Pitfall 3: `5000` has no fixture or layout spec
**What goes wrong:** Implementing a guessed 5000 layout that's wrong, or crashing on a 5000 tag.
**Why it happens:** `5000` is a recognized version tag (D-06b) but no reference impl/fixture exists anywhere on this machine or in `swg-client-v2` (`PHASE6_VERIFICATION.md`: "Tags `0006`/`5000` raise `UnsupportedTreVersionError` until fixtures exist"). All 46 `D:\Sample-TRE-Files` archives are v6000.
**How to avoid:** Recognize the `5000` tag; route it through the v6000 structural-sibling path *behind a clearly-flagged, fixture-gated branch* and degrade content to enumerate-only. Do NOT assert the layout. Add a TODO + a skipped/`[Trait]`-gated test that activates when a fixture appears.
**Warning signs:** A 5000 archive surfaces in testing with mis-decoded TOC — treat as "need fixture," not "fix the guess."

### Pitfall 4: v6000 payload obfuscation/encryption
**What goes wrong:** Trying to show decoded content for a v6000 entry produces garbage even for `compressor=0` entries.
**Why it happens:** Restoration v6000 payloads are obfuscated/encrypted at rest (`sample-tre-files.md` §4.4: even `compressor=0` entries don't start with valid IFF tags). Enumeration (TOC/names/metadata/CRC) works; content does not.
**How to avoid:** Per D-07, degrade gracefully — show metadata + "enumerate-only; extract via `TreeFileExtractor.exe`" banner. Detect by: archive version is 6000/5000, OR the decompressed bytes don't begin with a printable-ASCII FORM/known tag.
**Warning signs:** High-entropy bytes where an IFF header is expected.

### Pitfall 5: 100k+ entries on the UI thread
**What goes wrong:** UI freeze / OOM building the tree, or eager-reading payloads.
**Why it happens:** 213,086 paths; eager payload read = 5.5 GB.
**How to avoid:** TOC-only lazy enumeration (D-08); build the tree on a background `Task` and marshal to the UI (`Control.Invoke`); read a single payload only on `AfterSelect`; debounce the search filter (D-11). Consider virtualizing or lazy-expanding `TreeView` nodes.
**Warning signs:** Multi-second hang on panel open; memory spike.

### Pitfall 6: IFF endianness (tags BE, payload scalars LE)
**What goes wrong:** Per-type decoders read column counts / cell values byte-swapped.
**Why it happens:** IFF block tag+length are big-endian (`ntohl`); chunk *payload* scalars are native little-endian on Windows (`read_int32`/`read_float` memcpy). `Formats/Iff` already reads tags BE and exposes leaf `.Data` as raw bytes — the decoders must read payload ints/floats **little-endian**.
**How to avoid:** In the new decoders, read scalars from `IffLeafChunk.Data` as little-endian (`BitConverter` on LE host, or explicit shift). Verified against `swg_iff/reader.py` and `iff-tre-codebase-map.md`.
**Warning signs:** numCols = 16777216 instead of 1.

### Pitfall 7: drifting CLI and browser apart (success criterion #4)
**What goes wrong:** The browser reads via a new code path the CLI goldens don't cover.
**Why it happens:** Two consumers, one core — easy to add a browser-only helper.
**How to avoid:** All TRE/IFF/decoder logic lands in `Formats/` and is exercised by `Utinni.Cli` verbs with golden fixtures (TEST-03 precedent). The browser calls the same `Formats/` APIs. Extend `parse-tre`/`list-objects`/`inspect-iff` (and possibly add a decode verb) so the goldens cover the exact paths the browser uses.
**Warning signs:** A method only called from `TheJawaToolboxDotNet`, never from the CLI or tests.

## Code Examples

### Detecting master-index kind (COT2000 vs SearchTOC) for the hybrid full-enumeration
```
// Source: swg-client-v2 tre_reader.py detect_master_index_kind (algorithm to port, D-02)
// COT2000:   first 8 bytes == b" COT2000" (space + COT2000)
// SearchTOC: bytes[0:4]==TAG_TOC (" COT" no — it's 'TOC ' as LE 0x20434F54) AND bytes[4:8]==TAG_0001
// COT2000 header (offset → field): 0:magic(8) 8:reserved 12:numFiles 16:sizeOfTocBlock
//   20:sizeOfNameBlock 24:dupNameBlock 28:numTreeFiles 32:sizeOfTreeNameBlock
//   36:null-terminated .tre names (numTreeFiles of them) → then TOC block → then name block
// COT2000 global TOC entry (32 bytes): 0:u8 compressor 1:u8 unused 2:u16 treeFileIndex
//   4:u32 crc 8:u32 fileNameLength 12:u32 offset 16:u32 length 20:u32 compressedLength 24:8 pad
//   (fileNameLength is a LENGTH; convert to cumulative offset: off[0]=0, off[i]=off[i-1]+len[i-1]+1)
```

### Per-TRE v6000 header + 32-byte TOC entry (verified live)
```
// Source: decoded D:\Sample-TRE-Files\SwgRestoration_06.tre
// Header (36 bytes): 0:"EERT"(LE 'TREE') 4:"6000" 8:numFiles 12:tocOffset 16:tocCompressor(2=zlib)
//   20:sizeOfTOC 24:blockCompressor(2=zlib) 28:sizeOfNameBlock 32:uncompSizeOfNameBlock
// Body: [36 header][payload blobs from ~48][tocOffset: zlib TOC = numFiles*32][names: zlib][opt MD5 = numFiles*16 at tail]
// TOC entry (32): 0:u32 crc 4:i32 length 8:i32 offset 12:i32 compressor 16:i32 compressedLength
//   20:i32 fileNameOffset 24:8 pad
// Decoded rec0 → "playback/fire_projectiles_arc.pst" len 344 off 6720 comp 0  ✓ (names + sizes sane)
```

### Datatable structured decode (verified against engine)
```
// Source: swg-client-v2 .../sharedUtility/src/shared/DataTable.cpp
// FORM "DTII" → FORM "0000"|"0001"
//   COLS chunk: int32 numCols; numCols × null-terminated column-name strings
//   TYPE chunk: 0000 → int32 type-enum per col (Int/Float/String); 0001 → format string per col
//   ROWS chunk: int32 numRows; numRows*numCols cells (decode per column type, LE scalars)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `TreeFileExtractor.exe` (SOE-era) browse | Read-only TRE Browser inside TJT (this phase) | Phase 7 | Replaces the SOE browse tool; in-app, format-aware. |
| Eager full-payload `TreeFile.Open` (Phase 4 CLI) | Lazy TOC-only enumeration + on-demand payload (D-08) | Phase 7 | Required for 100k+ entry archives; existing eager model is the thing being extended. |
| 0005/0006-only TRE reader | Version-dispatch 0004/0005/0006/5000/6000/COT2000 | Phase 7 | Client-agnostic (SWGEmu + Restoration). |
| `DeflateStream` raw-deflate only (works on synth fixtures) | zlib-header-aware inflate (works on real archives) | Phase 7 | Fixes a latent bug that blocks reading any real `.tre`. |

**Deprecated/outdated:**
- The `ListObjectsCommand` `OBJS` byte-scan (REVIEWS MEDIUM-13 provisional debt) — Phase 7's structured decoders should route through `IffReader`, and `list-objects` could be migrated onto the real parser as part of keeping CLI+browser in lock-step.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Real SWGEmu 0004/0005/0006 archives use the engine **crc-first** `SearchTree::TableOfContentsEntry` order (24-byte stride), and Utinni's existing **size-first** decode is an artifact of how its synthesized fixtures were authored | Pitfall 1 / Open Q1 | If the existing size-first order is actually correct for real SWGEmu `.tre`, the version-dispatch table is simpler than feared — but the planner must still confirm against a real fixture before changing the CLI path. Getting this wrong silently mis-decodes either real SWGEmu archives or the existing goldens. |
| A2 | `5000` is structurally a sibling of `6000` (32-byte stride, encrypted payload) | D-06b / Pitfall 3 | If 5000 is actually closer to 0006 or a distinct layout, the defensive sibling-of-6000 path mis-enumerates. Mitigated by gating behind a fixture and degrading to enumerate-only. |
| A3 | `swg-main/serverdata` loose IFF assets are available on this machine for per-type decoder fixtures | Validation Architecture | If absent, per-type decoder goldens must be synthesized by hand or extracted via `TreeFileExtractor.exe`. (Not verified this session — `D:\Sample-TRE-Files` IS verified present.) |
| A4 | The Restoration v6000 client accepts the `6000` tag natively vs requiring a patched `SearchTree` | D-07 context | Irrelevant to read-only enumeration (we read the archive directly); only matters for the deferred write milestone. Low risk for Phase 7. |
| A5 | `Game.Repository` directory index (built from the destructive one-shot harvest) is stable for the session and safe to read repeatedly from the UI thread via the managed binding | D-05 / Runtime State | If the directory map is rebuilt or the harvest re-runs, overlay flags could go stale/empty. Mitigated: treat the overlay as best-effort, source-of-truth is disk enumeration. |

## Open Questions (RESOLVED)

1. **What is the true per-record TOC field order for real SWGEmu 0004/0005/0006 `.tre` archives?**
   - What we know: Utinni's synthesized 0005 fixture decodes coherently ONLY as size-first; the engine/v6000 canonical order is crc-first. The two disagree.
   - What's unclear: whether a *real* SWGEmu (Pre-CU) `.tre` follows size-first (matching Utinni's fixtures) or crc-first (matching the engine). No real SWGEmu fixture is on this machine — only Restoration v6000.
   - Recommendation: source one real SWGEmu `.tre` (from the user's SWGEmu client install) and decode both ways during P1; until then, keep the existing size-first path as the 0004/0005/0006 decoder (it satisfies the existing goldens) and use crc-first strictly for 6000/COT2000. Flag this in the plan as a fixture-acquisition task. **This is the single most important thing to confirm before changing the shared CLI code path.**
   - **RESOLVED (fixture-gated, size-first default kept):** Plan 07-01 keeps the existing **size-first** field order EXACTLY for V0004/V0005/V0006 (the path the existing CLI goldens exercise — byte-identical, no contract change) and version-dispatches **crc-first** strictly for V6000/COT2000. The true real-SWGEmu engine order remains a Wave-0 fixture-acquisition task (07-VALIDATION Wave 0: "Source a real SWGEmu 0004/0005/0006 fixture if available"); until such a fixture is sourced, the shared CLI path is deliberately left unchanged. No silent re-decode of the existing goldens occurs.

2. **Is `swg-main/serverdata` present for loose-IFF per-type decoder fixtures?**
   - What we know: `iff-tre-codebase-map.md` §11 describes it (~48k loose meshes, datatables, shaders, STFs); `D:\Sample-TRE-Files` is confirmed present but its v6000 payloads are encrypted.
   - What's unclear: whether the loose-asset sibling repo exists at `D:\Code\swg-main` on this machine (not verified this session).
   - Recommendation: P4 plan should probe for it; if absent, extract a handful of readable IFFs from an SWGEmu (0004/0005/0006) client via the new reader to author small in-repo golden fixtures (CON-O-09 precedent: in-repo synth, no LFS).
   - **RESOLVED (probe-or-synthesize-in-repo):** Plan 07-04 Task 1 codifies this — "probe for `D:/Code/swg-main/serverdata` loose IFF (Open Q2); if absent, author tiny in-repo synthesized datatable/STF/object-template `.iff` fixtures by hand (CON-O-09 in-repo-synth precedent, no LFS) and place them under `Utinni.Cli.Tests/Fixtures/iff/`." Either branch produces golden-tested decoders; the phase does not block on the sibling repo's presence.

3. **SubPanel vs StandalonePanel vs Form placement within TJT (D-03 discretion).**
   - What we know: `FormObjectBrowser` is a Form (in `forms`); the control panels are `SubPanel`s in a `SubPanelContainer`. A dense data browser with a tree + detail pane may want more than 417px fixed width.
   - Recommendation: A resizable Form (like `FormObjectBrowser`) likely fits the dense browse-and-inspect UX better than the 417px fixed-width SubPanel; planner's call per D-03. The `IEditorPlugin` aggregation supports both.
   - **RESOLVED (resizable UtinniForm chosen):** Settled by the approved `07-UI-SPEC.md` "Host Placement Decision" — ship as a resizable `UtinniForm` registered via `GetForms()` (1100×700 default, 760×480 minimum), NOT a fixed-417px SubPanel; `GetSubPanels()` stays null and `GetStandalonePanels()` is unchanged (CON-M-01/02 not widened). Plan 07-02 implements exactly this. The 417px SubPanel and the StandalonePanel are recorded as the rejected option and the deferred fallback respectively. This sets the Wave-1-editor host precedent for Phases 8-11.
