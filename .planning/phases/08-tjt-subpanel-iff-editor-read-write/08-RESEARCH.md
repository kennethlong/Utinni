# Phase 8: TJT subpanel — IFF Editor (read + write) - Research

**Researched:** 2026-05-27
**Domain:** Binary file-format read/write (EA-IFF-85 chunked containers + SWG `.tre` archives), WinForms editor UX, live-client memory patch + asset reload, max-harness round-trip testing
**Confidence:** HIGH for the framework/edit-model/CLI/loose-override surfaces; MEDIUM for `.tre` repack (CRC algorithm unverified); MEDIUM-LOW for D-06 forced in-session reload (no general-purpose IFF reload hook exists — partial hooks + scene-change fallback only)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** IFF write primitives ship **framework-side** in `UtinniCoreDotNet/Formats/Iff/` (`IffWriter` + mutable edit model), next to the existing `IffReader`. NOT in `TheJawaToolboxDotNet`. **ROADMAP RECONCILIATION REQUIRED:** Phase 8 Success Criterion 5 must be amended from "exported from `TheJawaToolboxDotNet`" to "exported from a shared, non-plugin assembly (`UtinniCoreDotNet/Formats/Iff`) that Phases 9–11 reference directly." Honor the CONTEXT decision over the ROADMAP literal wording.
- **D-02:** Add a CLI round-trip verb + golden fixtures (e.g. `roundtrip-iff`, or a `--write`/round-trip mode) that parses → (optionally mutates) → serializes → re-parses and asserts **byte-exact identity for untouched chunks**. Automated gate for Success Criterion 4. Mirrors the Phase-4 `inspect-iff`/`decode-iff` golden pattern.
- **D-03:** **Chunk-level editing only.** Edit (a) leaf chunk payload bytes and (b) tree structure: add / remove / rename-retag / reorder / duplicate chunks, and edit FORM sub-type tags. **No format-specific field parsing** — typed editing is Phases 9–11.
- **D-04:** Leaf payload editing offers three modes: (1) editable hex view (primary; length may grow/shrink), (2) Replace-bytes-from-file / Export-bytes-to-file, (3) inline text edit when payload is printable-ASCII (detection heuristic is planner's discretion).
- **D-05:** **All four save modes are hard V1 must-haves** that gate completion: (1) loose override file, (2) Save / Save-As, (3) in-memory live patch via CON-N-04 VirtualProtect bracket, (4) repack into source `.tre` (full repack + CRC/TOC rebuild). **PLAN-SPLIT FLAG:** split into multiple plans; `.tre` repack and mapped-memory live patch each warrant **isolated plans** with their own verification. Files opened from the TRE Browser come from read-only packed `.tre`, so for those, save modes 1/2/4 apply (not in-place).
- **D-06:** Editor forces an in-session client reload after a file-based save. **RESEARCH ITEM (high):** investigate whether a client-side asset-reload / cache-invalidation hook exists; surface a fallback if no direct hook (e.g. scene-change-style reload).
- **D-07:** **Hybrid mutable DOM.** Each node retains its original raw bytes from the read. On save: untouched leaves emit original bytes verbatim (byte-exact + preserves SWG no-pad quirk); edited/added leaves emit fresh bytes; container lengths roll up bottom-up. The existing `IffDocument` is `sealed`/immutable — the mutable model is a sibling, not a mutation of the reader's output type.
- **D-08:** Editor-local undo/redo stack, **independent** of Utinni's scene `UndoRedoManager` (avoids CON-M-05 entanglement entirely). NOT wired through `IEditorPlugin.AddUndoCommand`.
- **D-09:** A dedicated, editable IFF Editor SubPanel/Form, separate from Phase 7's read-only TRE Browser detail pane. **Extract** Phase 7's chunk-tree control (`TreDetailPane.LoadIff` + `tvChunks`) into a shared control; the TRE Browser stays read-only. Entry points: "Open in IFF Editor" hand-off from a TRE entry + a file picker. Phases 9–11 reuse the shared chunk-tree control.

### Claude's Discretion
- Exact UI layout (defer to the approved 08-UI-SPEC.md and planner).
- ASCII-ish detection heuristic for inline text edit (D-04.3).
- Validation-before-save behavior, conflict handling when a loose override already exists, large-file/perf guards, and "new IFF from scratch" — standard approaches; researcher/planner call.
- CLI verb naming/shape for the round-trip harness (D-02).
- Plan decomposition — strongly expected multi-plan given D-05.

### Deferred Ideas (OUT OF SCOPE)
- **Format-specific typed editing** (datatable cells, STF entries, object-template fields) → Phases 9, 10, 11.
- **Art-asset WRITE/authoring parity** (mesh/skeleton/animation/shader) → post-V1 milestone gated behind LOCKED DEC-A3.
- **Validation-before-save / structural integrity warnings, conflict handling, large-file perf guards, "new IFF from scratch"** — raised at wrap-up, not locked as V1 requirements. Planner may include lightweight versions at discretion.
- **ImGui chromeless HUD-overlay presentation** of the editor — optional later polish.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PROD-W1-IFF | Read + write IFF chunks; open, view chunk hierarchy, edit chunk content, save back to a file the live client reloads correctly; CLI shim covers `inspect-iff` with golden fixtures. | `IffWriter` + mutable DOM design (§ Architecture Patterns, Pattern 1–2); four save modes (§ Save Modes); D-02 round-trip verb (§ Validation Architecture); D-06 reload (§ Pitfall: forced reload). |
| PROD-02 | Wave-1 edit aggregate (contributes; closes at Phase 11). | Phase 8 ships the shared IFF write primitives Phases 9–11 consume via the same-assembly DLL reference. |
</phase_requirements>

## Summary

Phase 8 turns Phase 7's read-only IFF chunk tree into an editable surface and ships the IFF
**write primitives** (`IffWriter` + a mutable edit model) framework-side in
`UtinniCoreDotNet/Formats/Iff/`, next to the existing `IffReader` (D-01). The read path is already
complete and golden-tested; the write path is its sibling. The core technical insight is that the
IFF write framing is **trivially simple and fully specified by the existing reader + the
swg-client-v2 references**: a chunk is `4-byte BE tag · 4-byte BE u32 length · payload`; a FORM/LIST/CAT
container's payload is `4-byte BE sub-type tag · concatenated child chunks`; payload scalars are
little-endian (already irrelevant to Phase 8 since D-03 is chunk-level, byte-opaque). The D-07
hybrid-DOM design (untouched leaves re-emit original bytes verbatim) makes Success Criterion 4
("no corruption of unedited chunks") **structurally near-tautological** and automatically preserves
the SWG no-pad quirk the reader already detects.

The risk is concentrated in two of the four save modes (D-05): **in-memory live patch** (writes
mapped client memory via the CON-N-04 `memory::copy` VirtualProtect bracket — already exposed to
managed code) and **`.tre` repack** (a full archive rebuild: re-CRC paths, re-zlib-compress
payloads, rebuild the TOC + name block, rewrite the header). The swg-client-v2 reference is
unambiguous that **SWG does not support in-place `.tre` patching** — the only sanctioned update
models are (a) a loose-file override on a higher-priority search path and (b) a full rebuild via
`TreeFileBuilder`. This validates making the **loose-override file the primary, low-risk save mode**
and treating `.tre` repack as the highest-blast-radius mode.

The D-06 forced reload is the softest area: there is **no general-purpose "reload this IFF" hook** in
SWG/Utinni. Two narrow native reload paths are already exposed to C# (`Graphics.ReloadTextures()`
and `GroundScene.ReloadTerrain()`) plus `Graphics.flushResources(bool)`, but nothing reloads
datatables/templates/arbitrary IFF. The realistic implementation is a **scene-change-style reload**
(re-trigger the TJT setupScene path) for general assets, with the texture/terrain hooks as targeted
fast-paths — and a candid "reloads on next scene change" fallback when no live hook applies. This is
the only surface that must be validated by live-SWG smoke (Tier 4).

**Primary recommendation:** Build an `IffWriter` + `MutableIffNode`/`MutableIffDocument` model in
`UtinniCoreDotNet/Formats/Iff/` driven by the D-07 hybrid-DOM (raw-bytes-or-rebuilt), gate it with a
`roundtrip-iff` CLI verb + golden fixtures, extract Phase 7's chunk tree into a shared `UserControl`,
and split the phase so loose-override + Save/Save-As + the writer + CLI land first (8a), with the
in-memory live patch and `.tre` repack each in their own isolated risk plan (8b/8c).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| IFF chunk parse (read) | Framework (`UtinniCoreDotNet/Formats/Iff`) | — | Already shipped (Phase 4/7); the mutable model mirrors it. |
| IFF chunk serialize (write) | Framework (`UtinniCoreDotNet/Formats/Iff`) | — | D-01 locks framework-side; shared by CLI + TJT. |
| Mutable edit model + undo/redo | Framework (model) + TJT (UI binding) | — | Model is reusable data structure; undo stack is editor-local (D-08), wired in the TJT editor. |
| Round-trip verification | CLI (`Utinni.Cli`) → tests | Framework writer | D-02 max-harness; the CLI is the second consumer of the shared writer. |
| Editor UI (tree edit, hex edit, structural ops) | TJT (`TheJawaToolboxDotNet`, WinForms `UtinniForm`) | Framework (shared chunk-tree control) | DEC-C4: editor ships inside TJT; shared control extracted from `TreDetailPane`. |
| Loose-override + Save/Save-As (file writes) | TJT (UI) → BCL `System.IO` | Framework writer | Pure managed file I/O off the UI thread; no live-client touch. |
| In-memory live patch | TJT (UI) → game thread via `GameCallbacks.AddMainLoopCall` → native `Memory.memory.copy` (CON-N-04) | Framework writer (produces bytes) | Mapped-client-memory write MUST be on the game thread + VirtualProtect-bracketed. |
| `.tre` repack (CRC/TOC/zlib rebuild) | Framework (new `TreWriter`/`TreBuilder` in `Formats/Tre`) | TJT (UI invokes off-thread) | Archive rebuild is format logic; belongs with the existing `TreFile` reader; shared with a future CLI verb. |
| Forced in-session reload | TJT (UI) → game thread → native `Graphics.ReloadTextures` / `GroundScene.ReloadTerrain` / scene-change | — | Reload touches the live client; must run on the game thread. |

## Standard Stack

This phase adds **no external packages.** All dependencies are already present in the solution
(verified against the csproj files, 2026-05-27).

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET Framework (BCL) | net472 / v4.7.2 | `System.IO`, `System.IO.Compression` (DeflateStream for `.tre` zlib), `System.Text` | [VERIFIED: csproj] The whole solution targets net472; matches the WinForms host + the existing `TreFile`/`IffReader`. |
| `System.Windows.Forms` | net472 BCL | Editor UI host (`UtinniForm`, `TreeView`, `TextBox`, `SplitContainer`, `OpenFileDialog`/`SaveFileDialog`) | [VERIFIED: codebase] Phase 7 + entire TJT is WinForms; UI-SPEC mandates reuse of the themed control suite. |
| `CommandLineParser` | 2.9.1 | New `roundtrip-iff` CLI verb (D-02) | [VERIFIED: Utinni.Cli.csproj] Every existing CLI verb uses `[Verb]`/`MapResult`. |
| `Newtonsoft.Json` | 13.0.3 | CLI round-trip JSON envelope (`schemaVersion: 1`) | [VERIFIED: csproj] The `inspect-iff`/`decode-iff` verbs already use it via `JsonOutput`. |
| `xunit` | 2.9.3 | Golden round-trip tests in `Utinni.Cli.Tests` | [VERIFIED: Utinni.Cli.Tests.csproj] Existing IFF golden tests use it. |

### Supporting (existing framework primitives — consume, do not rebuild)
| Primitive | Where | Purpose | When to Use |
|-----------|-------|---------|-------------|
| `Memory.memory.copy(dest, src, len)` | `Generated/UtinniCore.cs` (binds native `memory::copy`) | CON-N-04 VirtualProtect-bracketed write to mapped client memory | The ONLY sanctioned way to do D-05.3 in-memory live patch. [VERIFIED: codebase, line 661 of Generated] |
| `GameCallbacks.AddMainLoopCall(Action)` | `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` | Marshal work onto the SWG game thread | Required for ANY live-client interaction (live patch D-05.3 + forced reload D-06). [VERIFIED: codebase] |
| `Graphics.ReloadTextures()` | `Generated/UtinniCore.cs` → `swg::graphics::textureListReloadTextures()` | Reload the engine's texture list | D-06 targeted fast-path for texture/shader edits only. [VERIFIED: codebase, graphics.cpp:568] |
| `GroundScene.ReloadTerrain()` / `Graphics.flushResources(bool)` | `Generated/UtinniCore.cs` | Terrain reload / resource flush | D-06 targeted reload for terrain; `flushResources` for a broader GPU-resource flush. [VERIFIED: codebase] |
| `Game.AddSetSceneCallback` / TJT scene-change path | `GameCallbacks` + TJT `ScenePanel` | Scene re-setup | D-06 general-asset reload fallback (re-load assets via a scene change). [VERIFIED: codebase + MEMORY: scene-change-via-TJT] |
| `TreDetailPane.LoadIff(IffDocument)` + `tvChunks` | TJT `UI/Controls/TreDetailPane.cs` | The read-only chunk tree built `public`/standalone for this phase (D-13) | Extract into a shared `UserControl` (D-09). [VERIFIED: codebase] |
| `UtINI` settings (`[IffEditor]` section) | `UtinniCoreDotNet` | Window-size/splitter persistence | Mirror `FormTreBrowser`'s `[TreBrowser]` ini pattern. [VERIFIED: codebase] |
| `Utinni.Cli.Output.JsonOutput` | `Utinni.Cli` | Stable sorted-key JSON envelope for the round-trip verb | Mirror `InspectIffCommand`. [VERIFIED: codebase] |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Existing `txtHex` TextBox made editable | A purpose-built hex-grid control | UI-SPEC Assumption 2 explicitly keeps the textbox (don't hand-roll a hex grid for V1); revisit only if byte-accurate column editing proves unworkable. |
| Pure-managed `.tre` repack (`System.IO.Compression`) | Shell out to a built `TreeFileBuilder.exe` from swg-client-v2 | swg-client-v2 is a read-only reference (D-02 of Phase 7), NOT a runtime dependency; shelling out adds a build/distribution coupling. Pure-managed repack keeps the single-codebase invariant. |
| Scene-change-style reload (D-06 fallback) | A new native asset-cache-invalidation hook | Designing a new hook is a multi-day reverse-engineering risk item with live-SWG-only validation; the scene-change path already reloads assets and is the user's established repro path. |

**Installation:** None. No `npm install` / `pip install` / `dotnet add package` for this phase.

**Version verification (run 2026-05-27):**
```
CommandLineParser 2.9.1   — present in Utinni.Cli.csproj
Newtonsoft.Json 13.0.3    — present in Utinni.Cli.csproj + Utinni.Cli.Tests.csproj
xunit 2.9.3               — present in Utinni.Cli.Tests.csproj
System.IO.Compression     — BCL (net472); already used by TreFile.Inflate
```

## Package Legitimacy Audit

> **No external packages are installed by this phase.** Every dependency (CommandLineParser,
> Newtonsoft.Json, xunit, BCL `System.IO.Compression`/`System.Windows.Forms`) is already a resolved,
> in-use dependency of the existing solution — they were vetted in Phases 1/4. There is no new
> registry surface, no `dotnet add package`, no npm/PyPI/crates install. The Package Legitimacy
> Gate is therefore not applicable; slopcheck/registry verification has no new package to evaluate.

| Package | Registry | New in Phase 8? | Disposition |
|---------|----------|-----------------|-------------|
| (none) | — | — | No external packages added. |

## Architecture Patterns

### System Architecture Diagram

```
                        ┌─────────────────────────────────────────────────────┐
   "Open in IFF Editor" │  TJT  FormIffEditor (UtinniForm, GetForms())          │
   from TRE Browser ───►│   ┌──────────────┐   ┌───────────────────────────┐   │
   OR Open… file picker │   │ shared chunk │   │ leaf editor pane          │   │
                        │   │ tree control │◄─►│ hex / inline-text / replace│   │
                        │   │ (extracted   │   └───────────────────────────┘   │
                        │   │ from         │           ▲                        │
                        │   │ TreDetailPane│           │ edit events            │
                        │   └──────┬───────┘           ▼                        │
                        │          │      ┌──────────────────────────┐          │
                        │          └─────►│ editor-local Undo/Redo    │ (D-08)   │
                        │                 │ stack (independent of     │          │
                        │                 │ scene UndoRedoManager)    │          │
                        └──────────────────────┬──────────────────────┘          
                                               │ load / mutate / save
                                               ▼
        ┌──────────────────────────────────────────────────────────────────────┐
        │  FRAMEWORK  UtinniCoreDotNet/Formats/Iff/  (D-01)                       │
        │  ┌───────────┐   parse    ┌──────────────────┐  serialize  ┌─────────┐  │
        │  │ IffReader │ ─────────► │ MutableIffDocument│ ──────────► │IffWriter│  │
        │  │ (exists)  │            │  (NEW, hybrid DOM)│             │ (NEW)   │  │
        │  └───────────┘            │  node = raw bytes │             └────┬────┘  │
        │     ▲                     │   OR rebuilt bytes│                  │       │
        │     │ re-parse (verify)   └──────────────────┘                  │bytes  │
        │     └──────────────────────────────────────────────────────────┘       │
        └────────┬───────────────────────────────────────────────────┬───────────┘
                 │ same code path                                     │
                 ▼ (D-02 gate)                                        ▼ (4 save modes, D-05)
   ┌──────────────────────────┐         ┌────────────────────────────────────────────────┐
   │ CLI  roundtrip-iff verb  │         │ 1. loose override file  ─► BCL File.Write (off-UI)│  ◄─ PRIMARY, low risk
   │  parse→mutate→write→parse│         │ 2. Save / Save-As       ─► BCL File.Write (off-UI)│
   │  assert byte-exact       │         │ 3. live patch  ─► game thread ─► Memory.copy (N-04)│  ◄─ ISOLATED PLAN
   │  golden fixtures (xunit) │         │ 4. .tre repack ─► TreWriter (CRC/TOC/zlib rebuild)│  ◄─ ISOLATED PLAN
   └──────────────────────────┘         └───────────────────────┬────────────────────────┘
                                                                 │ after file-based save (1,2,4)
                                                                 ▼ D-06 forced reload (game thread)
                                              ┌──────────────────────────────────────────────┐
                                              │ Graphics.ReloadTextures (textures only)        │
                                              │ GroundScene.ReloadTerrain (terrain only)       │
                                              │ scene-change-style re-setup (general fallback) │
                                              │ else: "reloads on next scene change" (candid)  │
                                              └──────────────────────────────────────────────┘
```

### Recommended Project Structure
```
UtinniCoreDotNet/Formats/Iff/        # D-01: write primitives ship here, next to the reader
├── IffReader.cs            # EXISTS — read path
├── IffDocument.cs          # EXISTS — sealed/immutable read result
├── IffChunk.cs             # EXISTS — abstract base (TypeId, LengthBytes, Id, OffsetBytes)
├── IffContainerChunk.cs    # EXISTS — SubTypeId + Children
├── IffLeafChunk.cs         # EXISTS — Data (note: property is `Data`, not `Payload`)
├── IffParseException.cs    # EXISTS
├── MutableIffNode.cs       # NEW — mutable node (raw-bytes OR rebuilt; container vs leaf)
├── MutableIffDocument.cs   # NEW — mutable DOM root; FromDocument(IffDocument); structural ops
└── IffWriter.cs            # NEW — serialize MutableIffDocument → byte[]/Stream

UtinniCoreDotNet/Formats/Tre/        # .tre repack (D-05.4) lives with the existing reader
├── TreFile.cs              # EXISTS — read path (header/TOC/names/zlib)
└── TreWriter.cs            # NEW — repack: re-CRC, re-zlib, rebuild TOC+names+header

Utinni.Cli/Commands/
└── RoundtripIffCommand.cs  # NEW — D-02 round-trip verb (mirror InspectIffCommand shape)

Utinni.Cli.Tests/Fixtures/iff/        # NEW round-trip golden fixtures (mirror existing iff/ dir)
└── roundtrip/*.iff + *.expected.json

[sibling repo D:/Code/UtinniPlugins]
The Jawa Toolbox/TheJawaToolboxDotNet/UI/
├── Controls/IffChunkTree.cs (or similar)  # NEW — shared chunk-tree UserControl extracted (D-09)
├── Controls/TreDetailPane.cs              # EDIT — consume the shared control (keep read API)
├── Forms/FormIffEditor.cs (+ .Designer.cs)# NEW — editable editor window (UI-SPEC)
└── Forms/FormTreBrowser.cs                # EDIT — add "Open in IFF Editor" hand-off
Plugin.cs                                  # EDIT — add new FormIffEditor to forms list (try/catch isolated)
```

### Pattern 1: IFF write framing (the entire on-disk format)
**What:** Serialize a chunk graph back to EA-IFF-85 bytes. This is the whole writer.
**When to use:** `IffWriter.Write(MutableIffDocument)`.
**Example (semantics confirmed by the existing reader + swg_iff/writer.py reference):**
```
chunk    = BE_u32(tag4) · BE_u32(payloadLength) · payload
container = BE_u32(tag4) · BE_u32(innerLength) · (BE_u32(subType4) · child0 · child1 · …)
            where innerLength = 4 (subType) + Σ child sizes
leaf     = BE_u32(tag4) · BE_u32(len) · rawOrRebuiltBytes
```
- Tag + length are **big-endian** (the reader's `ReadInt32Be` confirms; `swg_iff/writer.py` uses `>I`).
- **No word-align pad byte** for odd-length chunks — SWG omits it, and the reader DETECTS rather
  than assumes it (`IffReader.ReadLeafChunk`, the 07-04a no-pad reversal). The writer must likewise
  **emit no pad** so round-trips stay byte-exact. (EA-IFF-85 *allows* a 0x00 pad; SWG does not write
  one — see `MEMORY: SWG IFF no-pad`.)
- Length fields are **bottom-up rolled up**: a container's declared length is computed from its
  children's serialized sizes (D-07). Compute child bytes first, then prepend the container header.

### Pattern 2: Hybrid mutable DOM (D-07) — the round-trip-fidelity mechanism
**What:** Each mutable node holds either (a) the original raw bytes captured at read time, or (b)
freshly-built bytes if the node was edited/added. Untouched leaves re-emit (a) verbatim.
**When to use:** `MutableIffDocument.FromDocument(IffReader.Read(...))`; the editor mutates it; the
writer serializes it.
**Why it makes Criterion 4 near-tautological:** an untouched subtree's serialized output is a
byte-for-byte copy of its source bytes, so "no corruption of unedited chunks" holds *by
construction* — and the no-pad quirk is preserved for free because the original bytes already lack
the pad.
**Design notes:**
- For a **leaf**, the raw bytes ARE the on-disk `tag·len·payload` slice (or store payload + a dirty
  flag; either is valid — capturing the full slice incl. header is simplest for verbatim re-emit).
  Note `IffChunk.OffsetBytes` (07-03) gives the start offset, enabling a slice capture from the
  source buffer.
- For a **container**, "untouched" means *no descendant changed*; if any child is dirty, the
  container re-rolls its length and re-emits its sub-type + children (children themselves still
  re-emit verbatim if untouched). Propagate a dirty bit upward on any edit.
- `IffDocument` is `sealed` and `IffLeafChunk.Data` is copy-on-construction immutable — the mutable
  model is a **separate type hierarchy**, built FROM an `IffDocument`, never a mutation of it.

### Pattern 3: Live-client write must marshal to the game thread, then VirtualProtect-bracket
**What:** In-memory live patch (D-05.3) and forced reload (D-06) touch the live client.
**When to use:** mode 3 save; any reload.
**Example:**
```csharp
// From the UI thread, queue work onto the game thread:
GameCallbacks.AddMainLoopCall(() =>
{
    // On the game thread now. The mapped-memory write is VirtualProtect-bracketed by
    // memory::copy itself (CON-N-04) — do NOT hand-roll a write that skips the bracket.
    Memory.memory.copy(targetAddr, srcBytesAddr, length);   // native binding, Generated/UtinniCore.cs
});
```
**Anti-pattern:** writing mapped client memory directly from the UI thread or via any path that
bypasses `memory::copy`'s VirtualProtect save/restore.

### Pattern 4: `.tre` repack = full rebuild (NOT in-place patch)
**What:** Save mode 4. Reverse of `TreFile`'s reader.
**Confirmed by swg-client-v2 `iff-tre-codebase-map.md` §6:** *"The codebase does not appear to
support patching a `.tre` in place."* Updates are done by (1) loose-file search-path override or
(2) full rebuild via `TreeFileBuilder`.
**Repack steps (mirror `TreFile.Parse` in reverse — header layout in `sample-tre-files.md` §4):**
1. Gather all entries: for the edited entry, the new payload bytes; for every other entry, its
   original payload (read via `TreFile.GetRecordData(i)`). **Preserve byte-for-byte** all untouched
   entries.
2. For each entry: compute compressed bytes (zlib via `DeflateStream` + RFC1950 framing — invert
   `TreFile.Inflate`), the uncompressed size, the compressor flag, the **path CRC** (see Open
   Question #1 — algorithm unverified), and the name-block offset.
3. Lay out the file: 36-byte header (`EERT` + version tag + 7 u32 fields), then payload blobs, then
   the (optionally zlib-compressed) TOC block (24- or 32-byte stride per version), then the name
   block. Write the header offsets/sizes last once the block sizes are known.
4. For unchanged entries, **reuse the stored CRC** (`TreRecord.Checksum`) — only entries whose
   *path* changed need a recomputed CRC. Phase 8 edits payloads, not paths, so CRC recomputation may
   be entirely avoidable for the common case (strong risk reducer — see Open Question #1).

### Anti-Patterns to Avoid
- **Mutating the read model.** `IffDocument`/`IffLeafChunk` are immutable by design; build a sibling.
- **Re-implementing IFF parse in the editor UI.** The shared `IffReader` is the one parser; the
  editor binds to the mutable DOM, never re-parses ad hoc.
- **Routing IFF edits through the scene `UndoRedoManager`** (D-08 forbids — would entangle CON-M-05
  scene-cleanup; a scene cleanup would wipe IFF edit history). Build an editor-local stack.
- **Wiring the editor's undo through `IEditorPlugin.AddUndoCommand`** — that feeds the scene stack.
- **Targeting `TheJawaToolboxDotNet` for the write primitives** (ROADMAP literal) — D-01 reconciles
  this to `UtinniCoreDotNet/Formats/Iff`.
- **In-place `.tre` patch** — unsupported by SWG; do a full rebuild or a loose override.
- **Writing client memory off the game thread / without the VirtualProtect bracket.**

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| zlib (de/in)flate for `.tre` | A custom DEFLATE codec | BCL `System.IO.Compression.DeflateStream` + manual RFC1950 2-byte header + 4-byte Adler trailer | `TreFile.Inflate` already does exactly this for read; invert it. Adler32 trailer can be computed with a tiny loop (BCL DeflateStream does not emit/validate it). |
| Mapped-memory write protection | A bespoke `VirtualProtect` wrapper | `Memory.memory.copy` (CON-N-04, already bound) | Hand-rolling risks skipping the save/restore bracket — a preserved foundation. |
| Game-thread marshaling | Manual thread sync / busy-wait | `GameCallbacks.AddMainLoopCall` / `Control.Invoke` | Established pattern; C-09 already burned the busy-wait lesson. |
| Hex view widget | A custom hex-grid control | The existing `txtHex` TextBox made editable (UI-SPEC Assumption 2) | Don't-hand-roll for V1; revisit only if column-accurate editing fails. |
| IFF chunk tree control | A new TreeView from scratch | Extract `TreDetailPane.tvChunks`/`LoadIff` into a shared `UserControl` (D-09/D-13) | Phase 7 built it `public`/standalone for exactly this reuse. |
| CLI verb + golden harness | A new test runner | `[Verb]` + `MapResult` + `JsonOutput` + xunit golden fixtures | Phase 4 established the `inspect-iff`/`decode-iff` pattern. |
| Window-size persistence | New settings code | `UtINI` `[IffEditor]` section, mirroring `[TreBrowser]` | `FormTreBrowser` shows the exact pattern (with the SplitterDistance guard gotcha). |

**Key insight:** Almost nothing in Phase 8 is genuinely novel code — the read path, the zlib
framing, the memory-write bracket, the game-thread marshaling, the chunk-tree control, and the CLI
harness all already exist. The new code is the *write* mirror of each (writer, mutable DOM, repack)
plus the editor UI. Treat every "new" item as "invert/extend an existing tested primitive."

## Runtime State Inventory

> Phase 8 is primarily new code + file writes, not a rename/refactor. This section covers the
> live-client and on-disk state the **save modes** touch — the planner needs it to scope verification.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data (on disk) | The client's packed `.tre` archives (read-only source for "open from TRE"); loose-override files written under the client load path; arbitrary Save-As targets; the source `.tre` rewritten by repack (mode 4). | File writes (modes 1/2/4). Mode 4 rewrites a shared archive → back up first (UI-SPEC offers an opt-in "back up the .tre" checkbox). |
| Live service / client config | The injected client's `.tre` / loose load path (where SWG resolves loose overrides with priority over packed `.tre`). Derived at runtime, NOT in git. | Mode 1 must derive the override directory from the injected client config — reuse `FormTreBrowser.ResolveClientTreDir()` (process module dir → `GetWorkingDirectory()` → `[…]clientDir` ini fallback). The loose-override sub-dir within that root is the open question for the planner (see Open Question #2). |
| OS-registered state | None — the editor registers no OS-level tasks/services. | None. |
| Secrets / env vars | None. `SWG_SAMPLE_TRE_DIR` env var (Phase 7 fixture resolver) is test-only, unchanged. | None. |
| Build artifacts | None new beyond standard `bin/` outputs. The new shared chunk-tree control changes `TreDetailPane`'s compile unit — rebuild TJT in the same commit (it references `UtinniCoreDotNet.dll`). | Rebuild TJT after any `UtinniCoreDotNet` public-surface addition (binary-compat caution: adding NEW types is safe; do not change existing signatures consumed by pre-built plugins). |
| In-memory (volatile) client state | The mapped IFF bytes patched by mode 3 — lost on reload, can destabilize the session. | Mode 3 is volatile by design; verification is live-SWG smoke only. |

**The canonical question (live patch):** after an in-memory patch, the change exists ONLY in mapped
client memory and is lost on any reload/scene change — this is the documented, intended behavior
(D-05.3), surfaced in the UI confirm dialog.

## Common Pitfalls

### Pitfall 1: Pad byte regression breaks byte-exact round-trip
**What goes wrong:** The writer emits an EA-IFF-85 0x00 pad after an odd-length chunk; real SWG IFF
has no pad, so a re-read + re-write of an untouched file is no longer byte-identical → Criterion 4
fails.
**Why it happens:** EA-IFF-85 *specifies* the pad; a spec-faithful writer adds it. SWG diverges.
**How to avoid:** D-07's verbatim re-emit of untouched leaves sidesteps this entirely (original
bytes have no pad). For *edited/added* leaves, the writer must **not** add a pad. Add a golden
fixture with an odd-length chunk (the existing `odd-chunk-no-pad.iff` is the model) round-tripped
through `roundtrip-iff` asserting byte-identity.
**Warning signs:** round-trip output is 1 byte longer per odd chunk than the input.

### Pitfall 2: Container length not rolled up after a structural edit
**What goes wrong:** A child is added/removed but the parent FORM's declared length still reflects
the old size → the client (or the reader) mis-parses subsequent chunks.
**Why it happens:** Lengths are redundant with content; a naive edit changes content but not the
length field.
**How to avoid:** D-07 bottom-up roll-up — serialize children first, then write the container
header with the summed length. Re-parse via `roundtrip-iff` after every structural op in tests.
**Warning signs:** `IffParseException` (NestedChunkOverflow / Truncated) on re-read.

### Pitfall 3: Live-memory write off the game thread or without the bracket (CON-N-04)
**What goes wrong:** Writing mapped client memory from the UI thread, or via a path that skips
`memory::copy`'s VirtualProtect save/restore → access violation / device-lost / client crash.
**Why it happens:** WinForms events run on the UI thread; mapped game memory is the game thread's.
**How to avoid:** Always `GameCallbacks.AddMainLoopCall` → `Memory.memory.copy`. Never widen or
bypass the bracket. Gate mode 3 behind a `Game.IsRunning` check + the UI-SPEC confirm dialog.
**Warning signs:** intermittent AV at the patch site; crash on the next render frame.

### Pitfall 4: Forced reload (D-06) — no general IFF reload hook exists
**What goes wrong:** The plan assumes a single "reload this file" call exists; it does not. Only
`Graphics.ReloadTextures()` (textures), `GroundScene.ReloadTerrain()` (terrain), and
`Graphics.flushResources(bool)` are exposed. Datatables/templates/arbitrary IFF have no reload hook.
**Why it happens:** SWG loads most assets once at scene setup and caches them; there is no public
per-file invalidation.
**How to avoid:** Treat D-06 as a **tiered reload**: (a) if the saved asset is a texture/shader,
call `ReloadTextures`; (b) if terrain, `ReloadTerrain`; (c) otherwise trigger a scene-change-style
re-setup (the user's established TJT repro path — `MEMORY: scene-change-via-TJT`); (d) if none
applies or no live client, show the candid UI-SPEC copy: *"In-session reload isn't available for
this asset — reload happens on the next scene change."* Validate on live SWG only (Tier 4).
**Warning signs:** edits to a saved file don't appear until a manual scene change — for many asset
classes this is the *expected* baseline, not a bug. NOTE: post-scene-change the user always lands
"naked" (equipment not re-rendered) under Utinni-injected SWGEmu — that is the documented baseline,
NOT a reload failure (`MEMORY: tjt-scene-change-naked-baseline`).

### Pitfall 5: `.tre` repack CRC mismatch corrupts archive resolution
**What goes wrong:** The repacked TOC stores a recomputed path CRC that does not match what the
client computes from the path → the client fails to resolve the entry (or resolves the wrong one).
**Why it happens:** The exact SWG TreeFile path-CRC algorithm is not verified in this session
(Open Question #1). A wrong polynomial/seed/casing produces a plausible-but-wrong CRC.
**How to avoid:** For payload-only edits (Phase 8's case), **preserve every entry's stored CRC**
(`TreRecord.Checksum`) — the path is unchanged, so the CRC is unchanged. Only recompute if a path is
added/renamed (not a Phase 8 chunk-level op). Verify a repacked archive byte-compares cleanly for
untouched entries against the original (the `fc /b` playground discipline from `sample-tre-files.md`
§7). This sidesteps the unverified-CRC risk for the common path.
**Warning signs:** the client can't find a file that was present before repack; CRC field differs
from the original for an untouched entry.

### Pitfall 6: Dock.Fill z-order / SplitContainer construction order (WinForms)
**What goes wrong:** A Dock.Fill control sent to back starves Top/Bottom siblings; setting
`SplitterDistance` before `Size` throws in the ctor → MEF load of the plugin fails → editor missing
from the menu.
**Why it happens:** WinForms docking + SplitContainer ctor ordering rules.
**How to avoid:** Keep Fill at front (add first / BringToFront); set `Size` BEFORE `SplitterDistance`
(the `TreDetailPane`/`FormTreBrowser` code documents both gotchas). Wrap the editor Form ctor in the
`Plugin.cs` try/catch isolation pattern so a ctor throw can't take down all of TJT.
**Warning signs:** an empty/missing region; "The Jawa Toolbox" disappears from the editor menu.

### Pitfall 7: Loose-override directory derivation is environment-dependent
**What goes wrong:** Mode 1 writes the override to a directory SWG doesn't actually search → "saved
but client never reloads."
**Why it happens:** The override search path depends on the injected client's config/working dir,
which `GetWorkingDirectory()` (== `GetCurrentDirectory`) frequently gets wrong.
**How to avoid:** Reuse `FormTreBrowser.ResolveClientTreDir()` (process module dir primary). The
*sub-directory* SWG searches for loose overrides (and its priority vs packed `.tre`) is the planner's
open item — derive from the client config, and verify on live SWG (Open Question #2).

## Code Examples

### Mutable-DOM construction from the read model (sketch — design, not literal API)
```csharp
// FRAMEWORK: UtinniCoreDotNet/Formats/Iff/MutableIffDocument.cs (NEW)
// Build a mutable tree FROM an immutable IffDocument; capture original bytes per node for D-07.
public static MutableIffDocument FromDocument(IffDocument doc, byte[] sourceBytes)
{
    // sourceBytes is the full file buffer; each node's verbatim slice is
    // sourceBytes[node.OffsetBytes .. node.OffsetBytes + headerLen + node.LengthBytes].
    // Untouched nodes re-emit that slice; dirty nodes rebuild.
    return Build(doc.Root, sourceBytes);
}
```

### IFF write framing (the writer core — confirmed by reader + swg_iff/writer.py)
```csharp
// FRAMEWORK: UtinniCoreDotNet/Formats/Iff/IffWriter.cs (NEW)
private static void WriteBe32(Stream s, uint v)
{
    s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16));
    s.WriteByte((byte)(v >> 8));  s.WriteByte((byte)v);
}
// leaf:      WriteBe32(tag); WriteBe32(len); s.Write(payload);   // NO pad byte (SWG quirk)
// container: build children → innerLen = 4 + Σ childBytes;
//            WriteBe32(tag); WriteBe32(innerLen); WriteBe32(subType); write children;
```
Source semantics: `IffReader.ReadInt32Be` (BE tag+length) and
`../swg-client-v2/tools/swg_blender/swg_iff/writer.py` (`make_chunk`/`make_form`: `>I` BE tag+length,
FORM payload = subtype + children).

### CLI round-trip verb shape (mirror InspectIffCommand)
```csharp
// Utinni.Cli/Commands/RoundtripIffCommand.cs (NEW)
[Verb("roundtrip-iff", HelpText = "Parse → serialize → re-parse an IFF; assert byte-exact untouched chunks.")]
public class RoundtripIffOptions {
    [Value(0, MetaName = "path", Required = true)] public string Path { get; set; }
}
// Run: read original bytes → IffReader.Read → MutableIffDocument.FromDocument
//      → IffWriter.Write → assert SequenceEqual(original, rewritten) for the no-mutation case
//      → emit JsonOutput envelope { byteExact: true, originalLength, rewrittenLength, ... }.
```

### Game-thread live patch + tiered reload (D-05.3 / D-06)
```csharp
// TJT: from the editor UI thread after a save.
GameCallbacks.AddMainLoopCall(() => {
    // mode 3: Memory.memory.copy(addr, srcAddr, len);  // CON-N-04 bracket
    // D-06 reload: if texture/shader -> Graphics.ReloadTextures();
    //              else if terrain   -> groundScene.ReloadTerrain();
    //              else               -> trigger scene-change-style reload (or candid fallback copy).
});
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Write primitives in `TheJawaToolboxDotNet` (ROADMAP literal) | Write primitives in `UtinniCoreDotNet/Formats/Iff` (D-01) | 08-CONTEXT 2026-05-27 | One IFF code path shared by CLI + TJT; reconcile ROADMAP Criterion 5 wording. |
| Strict EA-IFF-85 pad consumption | Detect-don't-assume no-pad | 07-04a (commit 7012d82), 2026-05-27 | Writer must NOT emit a pad; round-trips stay byte-exact. |
| In-place `.tre` edit (intuitive assumption) | Loose override (primary) + full rebuild (mode 4) | swg-client-v2 reference | No in-place patch exists; loose override is the standard modder loop. |
| Assume a general asset-reload hook | Tiered reload (textures/terrain hooks + scene-change fallback) | This research | D-06 is partially supported; general IFF reload needs a scene change. |

**Deprecated/outdated:** Nothing in the existing IFF/TRE reader is deprecated for this phase; it is
all the foundation to extend.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | SWG's `.tre` path CRC, if it must be recomputed, can be avoided for payload-only edits by preserving the stored `TreRecord.Checksum`. | Pitfall 5 / Open Q1 | If repack must recompute CRC for some reason (e.g. the client re-validates differently), an unverified algorithm could corrupt resolution. Mitigation: payload-only edits don't change paths; preserve CRC. |
| A2 | The loose-override search directory is derivable from the injected client config via the `ResolveClientTreDir` approach, and SWG resolves loose files with priority over packed `.tre`. | Pitfall 7 / Open Q2 | If the override sub-dir/priority differs per client lineage (SWGEmu vs Restoration), mode 1 may write to a dir SWG doesn't search. Validate on live SWG. |
| A3 | A scene-change-style reload is a viable D-06 fallback for general (non-texture/terrain) assets. | Pitfall 4 | If a scene change doesn't re-read the edited asset class, the fallback degrades to "reload on next natural load." Live-SWG-only validation. |
| A4 | The `txtHex` TextBox made editable is adequate for byte-accurate hex editing in V1. | Don't Hand-Roll | If column-accurate editing proves unworkable, a hex-grid control becomes necessary (UI-SPEC flags this as revisitable). |
| A5 | Capturing each node's verbatim source-byte slice (via `OffsetBytes` + length) is the cleanest D-07 implementation. | Pattern 2 | An alternative (store payload + dirty flag, re-synthesize header) is equally valid; planner's call. Low risk either way. |

## Open Questions

1. **Exact SWG `.tre` TreeFile path-CRC algorithm (for repack mode 4).**
   - What we know: each TOC entry stores a 32-bit path CRC; the reader reads it but never computes
     it. `sample-tre-files.md` calls it "path CRC." Web search did not surface the exact
     polynomial/seed/casing for the TreeFile CRC (distinct from the SWG packet CRC).
   - What's unclear: the precise algorithm. SWGEmu/swg-client-v2 source (`TreeFile_SearchNode.cpp`,
     `Crc.cpp`) is the authority; not read this session.
   - Recommendation: **Avoid the problem** — for Phase 8's payload-only edits, preserve each entry's
     stored CRC (path unchanged ⇒ CRC unchanged). If the planner wants add/rename support later,
     reverse `Crc.cpp` then (a documented sub-phase). Verify repack byte-compares untouched entries
     against the original.

2. **Loose-override directory + priority (save mode 1).**
   - What we know: SWG resolves loose files with priority over packed `.tre` (swg-client-v2 §6,
     "add dev search path for loose files"). `FormTreBrowser.ResolveClientTreDir()` finds the client
     root.
   - What's unclear: the exact sub-directory SWG searches for loose overrides, and whether it
     differs between SWGEmu and Restoration clients.
   - Recommendation: derive from the injected client config; expose a `[IffEditor] looseOverrideDir`
     ini fallback (mirror `[TreBrowser] clientDir`); verify on live SWG which directory the client
     actually re-reads.

3. **Which asset classes a scene-change-style reload actually refreshes (D-06).**
   - What we know: `ReloadTextures`/`ReloadTerrain` are class-specific; scene change reloads scene
     assets.
   - What's unclear: whether a scene change re-reads datatables/templates/STF that were already
     cached at first load.
   - Recommendation: a Tier-4 live-SWG smoke per asset class; document the matrix in
     `TESTING.md`. The UI affordance + copy already degrade gracefully (UI-SPEC).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET Framework 4.7.2 SDK + VS 2026 MSBuild | Build TJT + UtinniCoreDotNet (WinForms .resx → use MSBuild, not `dotnet build`) | ✓ | net472 / Dev18 | `MEMORY: dotnet-build-msbuild-resources` — build with VS2026 MSBuild; `dotnet test --no-build` for xUnit. |
| `dotnet test` (xUnit lane) | D-02 round-trip golden tests in CI | ✓ | xunit 2.9.3 | — |
| Self-hosted CI runner (v145/VS2026) | CI green gate | ✓ | — | `MEMORY: self-hosted-ci`; push-only trigger. |
| Live SWGEmu / Restoration client (injected) | Success Criteria 1, 2 (live load) + D-05.3 live patch + D-06 reload | ✗ at CI; ✓ on maintainer machine | — | Tier-4 manual smoke only (TEST-04 documented residual); the D-02 CLI harness covers the write path unattended. |
| swg-client-v2 reference repo | Format-spec reference (read-only) | ✓ | `D:/Code/swg-client-v2` | Already present; not a runtime dep. |
| `D:\Sample-TRE-Files` (COT2000 + v6000 archives) | `.tre` repack byte-compare verification (playground) | likely ✓ | — | `SWG_SAMPLE_TRE_DIR` env var; synthetic in-repo TRE fixtures (Phase 7 07-00) for CI. |

**Missing dependencies with no fallback:** None block CI — the D-02 harness covers the write path
unattended. The live-SWG surfaces (Criteria 1/2 reload, live patch) have no CI fallback and are the
documented Tier-4 manual residual.

**Missing dependencies with fallback:** Live-client behavior → Tier-4 maintainer smoke; the
worktrees-off build runs inline on the main tree (`MEMORY: gsd-worktrees-off`).

## Validation Architecture

> nyquist_validation is enabled (config has no `workflow.nyquist_validation` key → treated as on).

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (managed) — the existing `Utinni.Cli.Tests` golden-fixture lane |
| Config file | `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` (net472) |
| Quick run command | `dotnet test Utinni.Cli.Tests --no-build --filter "FullyQualifiedName~Roundtrip"` |
| Full suite command | `dotnet test --no-build` (run after a VS2026 MSBuild) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PROD-W1-IFF | Round-trip byte-exact for untouched chunks (Criterion 4) | golden / unit | `dotnet test Utinni.Cli.Tests --filter "FullyQualifiedName~Roundtrip"` | ❌ Wave 0 (new `roundtrip-iff` verb + fixtures) |
| PROD-W1-IFF | Edited leaf re-serializes correctly + parent length rolls up | unit | `IffWriter` xUnit tests in a new framework test fixture | ❌ Wave 0 |
| PROD-W1-IFF | Structural ops (add/remove/rename/reorder/duplicate) survive a write→re-parse | unit | `MutableIffDocument` xUnit tests | ❌ Wave 0 |
| PROD-W1-IFF | No-pad preserved for odd-length chunks on round-trip | golden | reuse `odd-chunk-no-pad.iff` model through `roundtrip-iff` | ❌ Wave 0 |
| PROD-W1-IFF | `.tre` repack: untouched entries byte-compare to original | golden / harness | repack a synthetic TRE fixture, `fc /b`-style assert | ❌ Wave 0 |
| PROD-W1-IFF | `inspect-iff` still covers the read path the editor uses (Criterion 3) | golden (existing) | `dotnet test Utinni.Cli.Tests --filter "InspectIff"` | ✅ exists |
| PROD-W1-IFF | Live load + edit + save + reload (Criteria 1, 2) | manual smoke (Tier 4) | live-SWG injection; document outcome | n/a — maintainer-in-loop |
| PROD-W1-IFF | In-memory live patch applies in-session (D-05.3) | manual smoke (Tier 4) | live-SWG injection | n/a |

### Sampling Rate
- **Per task commit:** `dotnet test Utinni.Cli.Tests --no-build --filter "FullyQualifiedName~Roundtrip"` (after MSBuild).
- **Per wave merge:** `dotnet test --no-build` full managed suite.
- **Phase gate:** full suite green + Tier-4 maintainer smoke for live load/save/reload (Criteria 1/2)
  documented before `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `Utinni.Cli/Commands/RoundtripIffCommand.cs` — new `roundtrip-iff` verb (covers PROD-W1-IFF round-trip)
- [ ] `Utinni.Cli.Tests/Fixtures/iff/roundtrip/*.iff` + `*.expected.json` — golden round-trip fixtures (incl. odd-length no-pad case)
- [ ] A framework-side xUnit fixture for `IffWriter` / `MutableIffDocument` (structural ops + length roll-up). NOTE: `UtinniCoreDotNet.Tests` exists for framework unit tests; add IFF-writer cases there.
- [ ] A `.tre` repack harness test using the Phase-7 synthetic TRE fixtures (07-00) — byte-compare untouched entries.
- *(Live-SWG behavior — Criteria 1/2 + live patch — is the documented Tier-4 manual residual, not automatable.)*

## Security Domain

> security_enforcement is enabled (config has no `security_enforcement: false` key → treated as on).
> Phase 8 is a local desktop editor over local files + the local injected client; there is no
> network/auth/session surface. The relevant security category is **input validation of untrusted
> binary input** (a crafted IFF/`.tre` must not crash/DoS the editor) — the reader already enforces
> this; the writer must not reintroduce DoS vectors.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | No auth surface (local tool). |
| V3 Session Management | no | No sessions. |
| V4 Access Control | no | Local files only; user already has client access. |
| V5 Input Validation | **yes** | The IFF reader's bounds checks (NegativeLength, 64 MB chunk cap, NestedChunkOverflow, streaming-EOF) and the TRE reader's checked-arithmetic guards (256 MB block cap, division/subtraction-form bounds) already cover malformed input. The writer must cap output sizes (don't serialize a chunk > 64 MB; reuse `IffReader.MaxChunkSize`) and the repack must cap inflate output (reuse `TreFile.MaxBlockSize`). |
| V6 Cryptography | no (but CRC) | The `.tre` path CRC is integrity metadata, not security crypto — never hand-roll a *security* hash; the CRC is for client resolution only (see Open Q1). |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed IFF/`.tre` crashes or DoS-es the editor | Denial of Service | Reuse the existing reader's size caps + bounds checks; the writer caps serialized sizes; one bad file shows a state panel, never crashes (the `TreDetailPane` four-state pattern). |
| Decompression bomb on `.tre` repack/read | Denial of Service | `TreFile.Inflate` already caps inflate at 256 MB; the repack path must keep that cap. |
| Path traversal via a crafted loose-override / Save-As path | Tampering | Use BCL path APIs; write only under the resolved client dir / user-chosen path; the editor never auto-elevates. (Low risk: user-driven local writes.) |
| Live-memory write destabilizing the client | Tampering / DoS | CON-N-04 VirtualProtect bracket + game-thread marshaling + the UI confirm dialog; volatile by design. |
| Crafted client modifications flagged by a shard | (out of scope) | DEC-A4: all editing is local/offline; shards may detect/reject modified clients — accepted. |

## Sources

### Primary (HIGH confidence)
- Codebase (this repo): `UtinniCoreDotNet/Formats/Iff/{IffReader,IffDocument,IffChunk,IffContainerChunk,IffLeafChunk}.cs` — read model + no-pad detection.
- Codebase: `UtinniCoreDotNet/Formats/Tre/TreFile.cs` — TRE header/TOC/names/zlib read path (invert for repack).
- Codebase: `UtinniCore/utility/memory.{h,cpp}` + `Generated/UtinniCore.cs` (line 661, `Memory.memory.copy`) — CON-N-04 bracket, exposed to managed.
- Codebase: `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` — `AddMainLoopCall` game-thread marshaling.
- Codebase: `Generated/UtinniCore.cs` — `Graphics.ReloadTextures` (11774), `GroundScene.ReloadTerrain` (14342), `Game.*SetSceneCallback`; `graphics.cpp:568` (reloadTextures → textureListReloadTextures).
- Codebase: TJT `UI/Controls/TreDetailPane.cs`, `UI/Forms/FormTreBrowser.cs`, `Plugin.cs` — chunk-tree control, client-dir resolution, registration pattern.
- Codebase: `Utinni.Cli/Commands/InspectIffCommand.cs`, `Program.cs`, `Utinni.Cli.Tests/Fixtures/iff/*` — CLI verb + golden pattern.
- swg-client-v2 (read-only reference): `tools/swg_blender/swg_iff/writer.py` — IFF write framing (BE tag+length, FORM=subtype+children, LE scalars).
- swg-client-v2: `docs/research/iff-tre-codebase-map.md` §6 — "no in-place `.tre` patch"; loose override + TreeFileBuilder rebuild model.
- swg-client-v2: `docs/research/sample-tre-files.md` — COT2000/v6000 TRE header + 32-byte TOC entry layout (path CRC field).
- 08-CONTEXT.md, 08-UI-SPEC.md, 08-DISCUSSION-LOG.md — locked decisions D-01..D-09 + UI contract.
- MEMORY notes: SWG IFF no-pad (FIXED); scene-change-via-TJT; naked-after-scene-change baseline; CON-N-04; dotnet-build-msbuild-resources; gsd-worktrees-off; WinForms Dock.Fill z-order; self-hosted CI.

### Secondary (MEDIUM confidence)
- [EA IFF 85 Standard (AmigaOS wiki)](https://wiki.amigaos.net/wiki/EA_IFF_85_Standard_for_Interchange_Format_Files) — chunk framing, FORM, big-endian length, optional 0x00 pad for odd chunks (not counted in ckSize).
- [Interchange File Format — ModdingWiki](https://moddingwiki.shikadi.net/wiki/Interchange_File_Format_(IFF)) — FORM:type notation, word-align padding rule.
- [Interchange File Format — Wikipedia](https://en.wikipedia.org/wiki/Interchange_File_Format) — general IFF structure corroboration.

### Tertiary (LOW confidence — flagged for validation)
- [SWG Packet CRC — SWGANH Wiki](http://wiki.swganh.org/index.php/SWG_Packet_CRC) and [CRC — SWGEmu KB](https://kb.privatecode.net/swgemu/general/soe-swg-packets/crc) — SWG uses a seeded CRC32 for *packets*; this is NOT confirmed to be the same algorithm as the TreeFile **path** CRC. Treat the TreeFile CRC algorithm as UNVERIFIED (Open Question #1).
- [Consolidating .tre files — Mod the Galaxy](https://modthegalaxy.com/index.php?threads/consolidating-tre-files.231/) — community context on `.tre` consolidation; not a format spec.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all primitives verified in-codebase.
- IFF write framing + hybrid DOM (D-01/D-07): HIGH — fully determined by the existing reader + swg_iff/writer.py + EA-IFF-85.
- Loose-override + Save/Save-As (D-05.1/2): HIGH-MEDIUM — pure file I/O; the exact override dir is Open Question #2.
- In-memory live patch (D-05.3): MEDIUM — the CON-N-04 + game-thread mechanism is verified; live behavior is Tier-4-only.
- `.tre` repack (D-05.4): MEDIUM — read path fully understood (invert it); the path-CRC algorithm is unverified (Open Question #1), mitigated by preserving stored CRC for payload-only edits.
- Forced reload (D-06): MEDIUM-LOW — partial hooks verified; no general IFF reload hook; scene-change fallback + Tier-4 validation.
- CLI round-trip harness (D-02): HIGH — direct mirror of the shipped `inspect-iff` pattern.

**Research date:** 2026-05-27
**Valid until:** 2026-06-26 (stable domain; codebase-internal — re-verify only if `Formats/Iff` or the native memory/reload bindings change)
