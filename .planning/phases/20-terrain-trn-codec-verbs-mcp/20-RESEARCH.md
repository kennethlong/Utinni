# Phase 20: Terrain `.trn` Codec + Verbs + MCP - Research

**Researched:** 2026-06-15
**Domain:** SWG procedural-terrain (`TerrainGenerator` / FORM `TGEN`) IFF codec; byte-exact loose-override save; verbs-first CLI + thin MCP read tool
**Confidence:** HIGH (format facts ported directly from the pinned `swg-client-v2/sharedTerrain` source; Utinni-side patterns read from the live repo)

## Summary

Phase 20 ports the SWG `TerrainGenerator` IFF format (FORM `TGEN`) into a C# read/edit/save codec that plugs into Utinni's *existing* IFF DOM stack (`IffReader` → `IffDocument` → `MutableIffDocument`/`IffWriter`) and surfaces through the *existing* `decode-iff` root-FORM dispatcher + the thin MCP `decode_iff`/typed-read wrapper. The format is the most variant-rich Utinni has tackled — a recursive layer tree (`TGEN → LYRS → LAYR → {boundaries, filters, affectors, sub-LAYR}`) whose leaves carry per-class version forms (`0000`..`0004`), preceded by six shared palette groups (`SGRP/FGRP/RGRP/EGRP/MGRP×2`) each with their own multi-version load paths. The whole structure is plain EA-IFF-85 FORM/chunk nesting with NO word-padding — exactly the no-pad reality Utinni already handles for datatable/stf/particle (`[VERIFIED: codebase IffWriter.cs:39-41,141]`).

The good news for scoping: the heavy lifting (byte-exact edit via captured-slice + dirty-bit re-emit, root-FORM auto-dispatch, the 23-verb CLI past the old 16-ceiling, the loose-override `apply-save-*` family, the thin MCP read tool) all already exists and is proven across Phases 8/11/14/15. Phase 20 is mostly **a new `TgenDecoder` (read → navigable tree + typed/raw-fallback fields)** + **a `trn`-aware field-level editor** (because `TGEN` `DATA` leaves pack *multiple* scalars in one payload, so the generic `apply-save-iff` whole-leaf-replace is insufficient) + **three verbs and one MCP tool wired into the established dispatchers** + **a synthesized ≤200-byte fixture matrix across both lineages**.

**Primary recommendation:** Build a read-only `TgenDecoder` over the existing `IffDocument` (recurse the `TGEN→LYRS→LAYR` tree, typed-decode the common tags, raw-fallback everything else), then a `trn` field-editor that re-encodes a single `DATA` payload via `MutableIffNode.SetPayload` and saves through `IffWriter` — reusing the captured-slice byte-exact guarantee. Wire `decode-trn`/`roundtrip-trn`/`apply-save-trn` into the existing `Type[]` `ParseArguments` + `Dispatch` switch (the 16-verb ceiling is ALREADY solved — criterion #4's "confirm it registers cleanly" is a smoke check, not new work). Synthesize fixtures by hand-emitting `TGEN` bytes with `IffWriter`; do NOT depend on shipping retail `.trn` assets (the largest are huge and v6000+ TRE payloads are encrypted).

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PROD-W2-TRN-01 | Open `.trn`, navigate layer tree (TGEN→Layers→Boundaries/Filters/Affectors/sub-layers) with names + active flags; six palettes read-only | TGEN structure + recursion mapped from `TerrainGenerator.cpp` (§Architecture). `LayerItem` carries active-flag + name (`load_0001`, lines 226-241). Palettes = `SGRP/FGRP/RGRP/EGRP/MGRP×2` loaded in fixed order (`load_0000` lines 2174-2212). |
| PROD-W2-TRN-02 | Common tags typed; unknown/long-tail tags degrade to generic field list — never a hard decode failure | Full tag taxonomy + typed-vs-long-tail split table (§Tag Taxonomy). Raw-fallback pattern proven in Phase 11/15 decoders. |
| PROD-W2-TRN-03 | Edit + save scalar/enum leaf + toggle active flag, byte-exact, via loose-override matrix | `MutableIffNode` captured-slice + dirty-bit re-emit (`IffWriter.cs:103`); `apply-save-iff` precedent. CAVEAT: `DATA` packs multiple fields → need `trn`-aware field encoder, not whole-leaf replace (§Pitfall 1). |
| PROD-W2-TRN-04 | Golden-tested `utinni-cli` verbs (decode/roundtrip/apply-save) + MCP read tool, BOTH lineages | Verb pattern (`RoundtripParticleCommand`), `Type[]` ParseArguments + Dispatch switch, thin MCP `ReadTools` wrapper all read from repo. 16-verb ceiling already solved (§Verbs). Fixture-matrix strategy §Fixtures. |

## User Constraints

> No CONTEXT.md exists yet (this is standalone research feeding a later discuss/plan). The binding constraints below are the **carried locks** from STATE.md + AGENTS.md and the phase success criteria — treat them with the same authority as locked decisions.

### Locked Decisions (carried, from STATE.md / AGENTS.md)
- **Byte-exact round-trip** across BOTH SWGEmu + Restoration fixtures is THE codec gate (DEC-C3).
- **Verbs-first** (DEC-V2-VERBS-FIRST): every capability lands as a golden-tested `utinni-cli` verb FIRST; MCP is a thin dispatcher with ZERO business logic.
- **MCP-OOP** (DEC-V2-MCP-OOP): the MCP read tool shells `utinni-cli`; the named pipe / process boundary is real; no format logic in `Utinni.Mcp`.
- **Editors as TJT SubPanels** (DEC-C4) — but that is **Phase 21** (PROD-W2-TRN-05), NOT this phase. Phase 20 is codec + verbs + MCP read tool only.
- **No standalone renderer** (DEC-A3 + live-in-client lock) — not in scope this phase (no preview here at all).
- Confirm the **CommandLineParser verb-count ceiling** registers cleanly before adding `trn-*` verbs (ALREADY solved via `Type[]` overload — see §Verbs).
- **Reference corpus is READ-ONLY**: `D:/Code/swg-client-v2/...` has no runtime dependency; port understanding, never `#include`/ProjectReference (DEC-V2-LIFT-SHIFT scope discipline).

### Claude's Discretion
- Exact split of verbs vs. shared codec library; how many typed tags to cover in v1 vs. raw-fallback (success criterion names the common set — see §Tag Taxonomy "Tier-1").
- Internal decoder class layout under `UtinniCoreDotNet/Formats/` (precedent: `Decoders/` for the dispatch decoder + a `Terrain/` subdir for the model, mirroring `Particle/`).
- Fixture synthesis mechanism (hand-rolled `IffWriter` emit vs. byte-literal `.trn` test assets).

### Deferred Ideas (OUT OF SCOPE — do not research/plan)
- Terrain **2D sampled-map preview** (needs the `Sampler*` port; deferred to v2.1.x).
- **Structural authoring / boundary painting** (the full SOE TerrainEditor surface; own milestone).
- **Long-tail affector typed coverage** (river/road/ribbon/environment/exclude/passable) beyond raw-fallback.
- **Live in-client regen-on-save** and the **TJT SubPanel** → those are Phase 21 (PROD-W2-TRN-05).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `.trn` byte parsing (FORM/chunk framing) | `UtinniCoreDotNet/Formats/Iff` (existing `IffReader`) | — | The IFF DOM is format-agnostic; TGEN is just another FORM tree. No new parser. |
| `TGEN` typed decode (layer tree, tags, palettes) | `UtinniCoreDotNet/Formats/Decoders` + new `Formats/Terrain/` model | — | Mirrors `ObjectTemplateDecoder` / particle codec placement. Pure managed (net4.7.2). |
| Field-level edit + byte-exact save | `MutableIffDocument` / `IffWriter` (existing) + new `trn` field-encoder | — | Captured-slice re-emit already byte-exact; the new bit is *re-encoding one multi-field `DATA` payload*. |
| CLI verbs (`decode/roundtrip/apply-save-trn`) | `Utinni.Cli/Commands` (net4.7.2) | — | Verbs-first lock; existing `Type[]` dispatch absorbs new verbs. |
| MCP read tool | `Utinni.Mcp/Tools/ReadTools` (net10, OOP) | shells `utinni-cli` | MCP-OOP lock; thin wrapper over `decode-iff` root-FORM auto-dispatch. |
| Fixtures | `Utinni.Cli.Tests/Fixtures` + `Utinni.Mcp.Tests/Fixtures` | — | Golden-test precedent; synthesized, not shipped retail assets. |

## Standard Stack

This phase adds **no new external packages**. It is pure managed C# built on the existing in-repo codec stack. The "stack" is therefore the internal components it composes.

### Core (existing, reused)
| Component | Location | Purpose | Why Standard |
|-----------|----------|---------|--------------|
| `IffReader` | `UtinniCoreDotNet/Formats/Iff/IffReader.cs` | Parse bytes → `IffDocument` tree; BE int framing; no-pad detect | The single IFF entry point all Utinni codecs use `[VERIFIED: codebase]` |
| `IffDocument` / `IffContainerChunk` / `IffLeafChunk` | same dir | Immutable parsed DOM; `.Root`, `.Children`, `.SubTypeId`, leaf `.Data` | Read-side model for navigation/decode `[VERIFIED: codebase]` |
| `MutableIffDocument` / `MutableIffNode` | same dir | Edit DOM; captured-slice + `IsDirty`; `SetPayload`; stable-id addressing | The byte-exact edit foundation (`FromDocument`, `DeriveStableId`) `[VERIFIED: codebase]` |
| `IffWriter` | same dir | Serialize DOM → bytes; verbatim re-emit of clean nodes; no-pad | The byte-exact save engine `[VERIFIED: codebase IffWriter.cs:103,141]` |
| `DecodeIffCommand` dispatcher | `Utinni.Cli/Commands/DecodeIffCommand.cs` | Root-FORM `SubTypeId` switch → per-type decoder | TGEN slots in as a new branch alongside PEFT/datatable/OT `[VERIFIED: codebase:84-169]` |
| `ReadTools` (MCP) | `Utinni.Mcp/Tools/ReadTools.cs` | `[McpServerTool]` thin wrappers shelling `utinni-cli` | The MCP-OOP read pattern; `summarize_particle` is the template `[VERIFIED: codebase:97-108]` |

### Supporting (new, to build)
| Component | Purpose | When to Use |
|-----------|---------|-------------|
| `TgenDecoder` (in `Formats/Decoders`) | Recurse `TGEN→LYRS→LAYR` tree; emit typed + raw-fallback field model; palette summaries | Read path (criteria 1, 2) |
| `Terrain/` model classes | `TerrainLayer`, `TerrainAffector/Boundary/Filter` (typed + raw), palette DTOs | Decode result + edit target |
| `Trn` field-encoder | Re-encode ONE multi-field `DATA` payload with a single scalar/enum/active-flag changed | Edit path (criterion 3) — generic `apply-save-iff` cannot (see Pitfall 1) |
| `DecodeTrnCommand` / `RoundtripTrnCommand` / `ApplySaveTrnCommand` | The three verbs | Criterion 4 |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Dedicated `apply-save-trn` field-editor | Generic `apply-save-iff --mutate-leaf` | REJECTED: `apply-save-iff` replaces a *whole leaf payload* by hex; `TGEN` `DATA` chunks pack 2-6 fields, so the caller would have to re-encode the full payload itself. A `trn`-aware verb that takes `--field/--value` and re-emits the DATA chunk is the modder/agent-usable surface (success criterion 3 says "edit scalar/enum leaf values"). |
| New `decode-trn` verb routing | Reuse `decode-iff` (it auto-dispatches on root FORM) | BOTH — `decode-iff` should grow a `TGEN` branch (so the MCP `decode_iff` tool works for free), AND a thin `decode-trn` verb can exist for discoverability/symmetry with `roundtrip-trn`/`apply-save-trn`. Confirm with planner; minimal-surface answer is "decode-iff branch + roundtrip-trn + apply-save-trn". |
| Port `sharedTerrain` C++ via the bridge | Reimplement decode in pure C# | Pure C# — the C++ `TerrainGenerator` uses the streaming `Iff` enter/exit API and is entangled with the runtime generator (`prepare()`, fractal sampling); Utinni's DOM model is the right abstraction and avoids a bridge/x86 dependency. Port *understanding* only (DEC-V2-LIFT-SHIFT discipline). |

**Installation:** none — no package adds. (Package Legitimacy Audit omitted: zero external packages.)

## Architecture Patterns

### System Architecture Diagram

```
.trn bytes (from TRE record OR loose override)
   │
   ▼
IffReader.Read(bytes) ───────────────► IffDocument (immutable DOM; root FORM "TGEN")
   │                                          │
   │   READ PATH                              │   EDIT PATH
   ▼                                          ▼
TgenDecoder.Decode(doc)            MutableIffDocument.FromDocument(doc, sourceBytes)
   │   recurse:                               │   (every node keeps captured-slice + IsDirty=false)
   │   TGEN ─ load 6 palettes (SGRP/FGRP/     │
   │          RGRP/EGRP/MGRP×2) ─► read-only  ▼
   │   └ LYRS ─ LAYR* (recursive)      locate target leaf by stable-id
   │            ├ IHDR (active flag + name)   (TGEN/.../LAYR/.../AHCN/.../0000/DATA:DATA/0)
   │            ├ boundaries (B***)           │
   │            ├ filters    (F***)           ▼
   │            ├ affectors  (A***) ──┐  Trn field-encoder:
   │            └ sub-LAYR (recurse)  │   read DATA payload → replace ONE field
   │                                  │   (scalar/enum/active) → SetPayload(new bytes)
   ▼                                  │   → node.IsDirty = true
typed fields (Tier-1 tags)           ▼
   + raw-fallback field list   IffWriter.Write(mutable):
   (unknown/long-tail tags)       clean nodes → verbatim captured slice
   + palette name/index lists     dirty node  → re-emit BE framing, NO pad byte
   │                                  │
   ▼                                  ▼
JSON envelope (utinni-cli)      new .trn bytes  ──► atomic write to loose-override path
   │   (decode-trn / decode-iff)      │            (verify untouched leaves byte-identical)
   ▼                                  ▼
MCP read tool (thin shell)      roundtrip-trn asserts bytes-in == bytes-out
```

### Recommended Project Structure
```
UtinniCoreDotNet/Formats/
├── Decoders/
│   └── TgenDecoder.cs          # recurse + typed/raw-fallback decode → result model
├── Terrain/                    # NEW (mirrors Particle/)
│   ├── TerrainDocument.cs      # decoded tree root (palettes + layer list)
│   ├── TerrainLayer.cs         # name, active, invert flags, children
│   ├── TerrainNode.cs          # affector/boundary/filter: tag, version, typed fields + raw bytes
│   ├── TerrainPalettes.cs      # six palette DTOs (family id ↔ name)
│   └── TrnFieldEncoder.cs      # re-encode one DATA field, byte-exact
Utinni.Cli/Commands/
│   ├── DecodeTrnCommand.cs     # (or fold into DecodeIffCommand TGEN branch)
│   ├── RoundtripTrnCommand.cs
│   └── ApplySaveTrnCommand.cs
Utinni.Mcp/Tools/ReadTools.cs   # + summarize_terrain (thin shell over decode-iff)
```

### Pattern 1: Root-FORM auto-dispatch (read path)
**What:** `decode-iff` reads root `SubTypeId`, switches to the matching decoder. PEFT/datatable/OT already branch here.
**When to use:** the TGEN read path — add `if (root.SubTypeId == "TGEN") TgenDecoder.Decode(doc)`.
```csharp
// Source: codebase Utinni.Cli/Commands/DecodeIffCommand.cs:84-169 (PEFT precedent)
if ((doc.Root as IffContainerChunk)?.SubTypeId == "PEFT")  // ← add a "TGEN" twin
    result = BuildParticleResult(...);
```

### Pattern 2: Captured-slice byte-exact edit
**What:** `MutableIffDocument.FromDocument` captures each node's exact byte slice; only a node whose payload you `SetPayload` is marked `IsDirty` and re-emitted; everything else is written verbatim.
**When to use:** the edit/save path — flip one scalar/active flag, leave the rest of the file untouched at the byte level.
```csharp
// Source: codebase Formats/Iff/IffWriter.cs:103 + MutableIffNode.cs:255
if (!node.IsDirty) { byte[] captured = node.GetCapturedSliceInternal(); /* verbatim */ }
// else re-emit BE framing + payload, NO trailing pad (IffWriter.cs:141)
```

### Pattern 3: Stable-id leaf addressing (no byte offsets)
**What:** `DeriveStableId` builds a hierarchical ordinal path (`parentPrefix + type + ":" + sub + "/" + ordinal`), so the many identically-named `DATA` chunks across affectors are each uniquely addressable.
**When to use:** the `apply-save-trn` target selector — address a leaf as e.g. `TGEN:.../LYRS:.../LAYR:.../AHCN:.../0000:.../DATA:DATA/0` rather than by file offset.
```csharp
// Source: codebase Formats/Iff/MutableIffDocument.cs:161-177
```

### Anti-Patterns to Avoid
- **Re-implementing the procedural generator** to "validate" decode — preview is Phase 21 / live-in-client only (DEC-A3). Decode = structure only.
- **Whole-file re-serialize from a typed model** — that risks byte drift on every untouched chunk. Always edit through `MutableIffDocument` so clean nodes re-emit verbatim.
- **Assuming a trailing pad byte** — SWG IFF is no-pad; `IffWriter` already handles this. Don't add alignment.
- **Hard-failing on an unknown tag** — criterion 2 mandates raw-fallback. Any FourCC not in the Tier-1 typed set must degrade to `{tag, version, rawBytes}`, never throw.

## Tag Taxonomy (the heart of this phase)

All tags below are `[VERIFIED: swg-client-v2/sharedTerrain/.../TerrainGeneratorType.h]` and the dispatch is `[VERIFIED: TerrainGeneratorLoader.cpp]`.

### Structural / container tags
| FourCC | Role |
|--------|------|
| `TGEN` | top-level FORM; single version `0000` |
| `LYRS` | layer list FORM (optional — `enterForm(...,true)`) |
| `LAYR` | a layer FORM; versions `0000`..`0004` (active+name+invert flags+notes) |
| `IHDR` | layer-item header FORM (active flag + name); versions `0000`,`0001` |
| `ACTN` | (legacy nested-action layer form, v0000-0002) |
| `ADTA` | layer data chunk (invertBoundaries/invertFilters/expanded/notes) |
| `DATA` | the ubiquitous leaf payload chunk inside every typed form |
| `PARM` | alternate param chunk (e.g. AffectorHeightFractal v0002/0003) |

### Boundaries (Tier-1 typed = criterion's "circle/rect"; rest raw-fallback)
| FourCC | Class | Tier |
|--------|-------|------|
| `BCIR` | BoundaryCircle | **Tier-1 typed** (center, radius, feather fn+distance) |
| `BREC` | BoundaryRectangle | **Tier-1 typed** (x0,y0,x1,y1, feather) |
| `BPOL` | BoundaryPolygon | Tier-2 raw-fallback |
| `BPLN` | BoundaryPolyline | Tier-2 raw-fallback |
| `BALL` `BSPL` | DEAD (loader skips: `enterForm/exitForm(...,true)`) | skip — emit as "obsolete, ignored" |

### Filters (Tier-1 = "height/slope")
| FourCC | Class | Tier |
|--------|-------|------|
| `FHGT` | FilterHeight (low/high height, feather) | **Tier-1 typed** |
| `FSLP` | FilterSlope | **Tier-1 typed** |
| `FFRA` | FilterFractal (refs FractalGroup family id) | Tier-2 raw-fallback |
| `FBIT` | FilterBitmap | Tier-2 raw-fallback |
| `FDIR` | FilterDirection | Tier-2 raw-fallback |
| `FSHD` | FilterShader | Tier-2 raw-fallback |

### Affectors (Tier-1 = "height/shader/color/flora")
| FourCC | Class | Tier |
|--------|-------|------|
| `AHCN` | AffectorHeightConstant (operation enum + height float) | **Tier-1 typed** |
| `AHTR` | AffectorHeightTerrace | **Tier-1 typed** (common) |
| `AHFR` | AffectorHeightFractal (refs FractalGroup) | Tier-2 raw-fallback (multi-version, fractal ref) |
| `ACCN` | AffectorColorConstant | **Tier-1 typed** |
| `ACRH` | AffectorColorRampHeight | **Tier-1 typed** |
| `ACRF` | AffectorColorRampFractal (refs FractalGroup) | Tier-2 raw-fallback |
| `ASCN` | AffectorShaderConstant (familyId → ShaderGroup) | **Tier-1 typed** (+ feather override v0001) |
| `ASRP` | AffectorShaderReplace (source/dest familyId) | **Tier-1 typed** |
| `AFCN`/`AFSC` | FloraStaticCollidableConstant | **Tier-1 typed** (flora) |
| `AFSN` | FloraStaticNonCollidableConstant | **Tier-1 typed** (flora) |
| `ARCN`/`AFDN` | FloraDynamicNearConstant | Tier-1/2 (flora) |
| `AFDF` | FloraDynamicFarConstant | Tier-2 |
| `AENV` | AffectorEnvironment (refs EnvironmentGroup) | Tier-2 raw-fallback |
| `AEXC` `APAS` `AROA` `ARIV` `ARIB` | Exclude/Passable/Road/River/Ribbon | Tier-2 raw-fallback (explicitly deferred long-tail) |
| `AHSM` `AHBM` `ACBM` `ASBM` `AFBM` | DEAD (loader skips) | skip — "obsolete, ignored" |

> **Planner note:** Tier-1 = decode to named fields (criterion 2's "common tags"). Tier-2 = raw-fallback `{tag, version, hex}` (criterion 2's "long-tail"). The split above is a *recommendation*; the discuss-phase can move tags between tiers. The DEAD tags must be recognized-and-skipped, not raw-emitted, to stay byte-exact (the loader consumes their form without reading payload).

## The Six Shared Palettes (read-only, criterion 1)

Loaded in a **fixed order** at `TGEN/0000` start `[VERIFIED: TerrainGenerator.cpp:2174-2212]`. Each is its own optional FORM with its own version range:

| # | Palette | FORM tag | Family chunk | Versions | Notes |
|---|---------|----------|--------------|----------|-------|
| 1 | Shader | `SGRP` | `SFAM` | `0000`..`0006` | familyId 0..255; name + color + weighted child shaders. Referenced by `ASCN`/`ASRP` via familyId. |
| 2 | Flora | `FGRP` | `FFAM` (per src) | `0001`..`0008` | widest version range (most lineage drift). |
| 3 | Radial (flora) | `RGRP` | `RFAM` | `0000`..`0004` | |
| 4 | Environment | `EGRP` | `EFAM` | `0000`..`0002` | referenced by `AENV`. |
| 5 | Fractal | `MGRP` | `MFAM` | `0000` | **⚠ shares the `MGRP` tag with Bitmap** — disambiguated ONLY by load order, not by tag (Pitfall 4). |
| 6 | Bitmap | `MGRP` | `MFAM` | `0000` | second `MGRP` in sequence. |

**Reference mechanism:** affectors store an integer `familyId` (e.g. `AffectorShaderConstant` `[VERIFIED: AffectorShader.cpp:185]`), NOT a name or array index — the palette maps familyId → name/color. For read-only display, decode each palette to a `familyId → name` list so the navigator can resolve an affector's family reference to a human name. **Do not renumber familyIds on save** (would break byte-exact and the affector references).

## SWGEmu vs Restoration version dispatch

The lineage difference is expressed entirely as **per-form version numbers** (`TAG_0000`..`TAG_000N`), dispatched in a `switch(iff.getCurrentName())` at every level. There is no separate "lineage flag" — the codec must handle the full version range per tag:

- **Layer (`LAYR`):** versions `0000`-`0004` add fields progressively (v0001 adds invertFilters; v0002 adds `expanded`; v0003 adds `notes` string) `[VERIFIED: TerrainGenerator.cpp:1451-1646]`.
- **Palettes:** ShaderGroup `0000`-`0006`, FloraGroup `0001`-`0008` — the wide ranges are where SWGEmu (older, lower versions) and Restoration (newer, higher versions) diverge.
- **Affectors/Filters/Boundaries:** most have `0000`-`0001` (v0001 typically adds feather-override / feather-function fields).

**Implication for decode:** a typed decoder must read the version FIRST, then read exactly the fields that version wrote. A version it doesn't recognize → raw-fallback (don't guess). **Implication for fixtures:** the fixture matrix must cover at least two versions of a representative tag (a low version = SWGEmu-era, a high version = Restoration-era) to prove the version-dispatch path on both lineages.

## Verbs-First Surface + the 16-verb ceiling (criterion 4)

**The 16-verb ceiling is ALREADY solved** `[VERIFIED: Utinni.Cli/Program.cs:43-74]`. The CLI is at **23 verbs** using the `Type[]` `ParseArguments` overload + a single `object`-typed `MapResult` → `Dispatch(opts)` switch. Adding `trn-*` verbs is:
1. Add `typeof(Commands.DecodeTrnOptions)` etc. to the `ParseArguments` `Type[]` list.
2. Add `case Commands.DecodeTrnOptions o: return Commands.DecodeTrnCommand.Run(o);` to `Dispatch`.

Criterion 4's "confirm the CommandLineParser verb-count ceiling registers cleanly first" = a **smoke check** that `--help` enumerates the new verbs and they parse — NOT new infrastructure. (Recommend a Wave-0 task: add a no-op `trn` verb, confirm registration, then build it out.)

**Verb shapes** (mirror `RoundtripParticleCommand` `[VERIFIED: codebase]`):
- `decode-trn <path>` → JSON tree (or fold into `decode-iff` TGEN branch + keep this as alias).
- `roundtrip-trn <path>` → `ReadAllBytes` → `MutableIffDocument`→`IffWriter` → assert `SequenceEqual` (whole-file byte identity).
- `apply-save-trn --root <r> --asset <a> --leaf <stable-id> --field <name> --value <v>` → re-encode ONE `DATA` field, verify untouched leaves byte-identical, atomic write (mirror `apply-save-iff` `[VERIFIED: ApplySaveIffCommand.cs:38-66]` but field-aware).

**MCP (thin, MCP-OOP):** add `summarize_terrain` to `ReadTools` as a `[McpServerTool(ReadOnly=true)]` that shells `decode-iff` (which now routes TGEN) — copy `summarize_particle` verbatim `[VERIFIED: ReadTools.cs:97-108]`. ZERO format logic in `Utinni.Mcp`.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| IFF FORM/chunk parsing | A new TGEN-specific byte reader | `IffReader` → `IffDocument` | Already handles BE framing, no-pad detect, EOF short-read, nested FORMs `[VERIFIED]` |
| Byte-exact save | Re-serialize a typed model | `MutableIffDocument` + `IffWriter` | Captured-slice re-emits clean nodes verbatim; only dirty nodes re-encode |
| Leaf addressing | File byte offsets | `DeriveStableId` ordinal paths | Stable across edits; uniquely addresses the many identical `DATA` chunks |
| Verb registration | A new dispatcher / fight the 16-cap | `Type[]` ParseArguments + Dispatch switch | Already proven at 23 verbs |
| MCP read tool | Hosting codec logic in net10 | Thin `[McpServerTool]` shelling `utinni-cli` | MCP-OOP lock; `summarize_particle` is the copy-template |
| Fixtures | Shipping retail `.trn` assets | Hand-emit ≤200-byte `TGEN` bytes via `IffWriter` | Retail `.trn` are large; v6000+ TRE payloads encrypted; tiny synth fixtures isolate the version matrix |

**Key insight:** Phase 20 is ~80% composition of proven Utinni machinery. The genuinely new code is (a) the `TgenDecoder` recursion + per-tag typed field readers, and (b) the multi-field `DATA` re-encoder for `apply-save-trn`. Everything else is wiring.

## Runtime State Inventory

> Greenfield codec phase — no rename/refactor/migration. Section included only to record the one stateful surface.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — `.trn` assets live in TRE archives / loose overrides on disk; no DB keys this phase. | none |
| Live service config | None. | none |
| OS-registered state | None. | none |
| Secrets/env vars | None. | none |
| Build artifacts | New decoder/verbs compile into existing `UtinniCoreDotNet.dll` / `utinni-cli.exe`; no new project. CppSharp `Generated/UtinniCore.cs` is NOT touched (pure managed). | none beyond normal build |

**Loose-override save destination** is the one real runtime touchpoint: `apply-save-trn` writes under a contained `--root` (same fail-closed containment as `apply-save-iff` `[VERIFIED: ApplySaveIffCommand.cs:44]`). No new state category.

## Common Pitfalls

### Pitfall 1: `DATA` chunks pack MULTIPLE fields — generic whole-leaf replace is wrong
**What goes wrong:** `apply-save-iff --mutate-leaf` replaces the *entire* leaf payload by hex. A `TGEN` `DATA` leaf is e.g. `int32 operation + float height` (`AffectorHeightConstant` `[VERIFIED: AffectorHeight.cpp:159-166]`) or 3-6 packed scalars. Editing "height" via the generic verb forces the caller to re-encode the whole payload by hand.
**Why it happens:** the success criterion says "edit scalar/enum leaf *values*" (field granularity), but the generic verb is leaf granularity.
**How to avoid:** build `apply-save-trn` to take a field name, read the `DATA` payload via the tag's known layout, replace ONE field, re-emit the whole payload, `SetPayload`. Byte-exact still holds (only that leaf is dirty).
**Warning signs:** a plan that says "reuse apply-save-iff for TRN edits" with no field-encoder.

### Pitfall 2: Dead/obsolete tags must be skipped, not raw-emitted
**What goes wrong:** `BALL`,`BSPL`,`AHSM`,`AHBM`,`ACBM`,`ASBM`,`AFBM` are recognized-and-skipped by the loader (`enterForm/exitForm(...,true)`, no payload read) `[VERIFIED: TerrainGeneratorLoader.cpp:68-74,226-264]`. If the decoder raw-fallbacks them as `{tag, hex}` and a careless re-encoder rewrites them, you may drift bytes — but more importantly they shouldn't appear as editable nodes.
**How to avoid:** maintain a DEAD-tag set; decode them as "obsolete, ignored" (display-only), never editable, and re-emit verbatim (they will, via captured-slice, since untouched).

### Pitfall 3: `LYRS` and all six palettes are OPTIONAL forms
**What goes wrong:** the C++ loader uses `enterForm(TAG, true)` (optional) for `LYRS` and every palette `[VERIFIED: TerrainGenerator.cpp:2197; ShaderGroup.cpp:614]`. A minimal/synthetic `.trn` may omit them. A decoder that *requires* them throws on a valid file.
**How to avoid:** treat each palette + `LYRS` as optional; absent → empty list. This also lets ≤200-byte fixtures legitimately omit palettes.

### Pitfall 4: `FractalGroup` and `BitmapGroup` BOTH use FORM tag `MGRP`
**What goes wrong:** both palettes serialize under `TAG_MGRP` `[VERIFIED: FractalGroup.cpp:25,156; BitmapGroup.cpp:24,298]`. A tag-keyed decoder will mis-assign the second `MGRP` or double-read the first.
**How to avoid:** decode the palettes **positionally** in the fixed load order (shader, flora, radial, environment, fractal, bitmap), exactly as `load_0000` does — NOT by tag lookup. Bitmap is the 6th/last `MGRP`.

### Pitfall 5: Version-first reads — never read fields a version didn't write
**What goes wrong:** v0000 `BoundaryCircle` has no feather fields; v0001+ does `[VERIFIED: Boundary.cpp:215-273]`. Reading feather from a v0000 chunk overruns into the next chunk → cascade corruption.
**How to avoid:** dispatch on the form version first; read exactly that version's field list; unknown version → raw-fallback.

### Pitfall 6: No-pad on save (the recurring SWG trap)
**What goes wrong:** EA-IFF-85 normally word-pads odd-length chunks; real SWG does NOT. Re-emitting with a pad byte breaks byte-exact (bit Phases 7/9/15).
**How to avoid:** `IffWriter` already omits the pad `[VERIFIED: IffWriter.cs:141]` — just don't re-implement framing. Fixture round-trip will catch any regression.

### Pitfall 7: `read_string` is NUL-terminated, variable-length — affects leaf size
**What goes wrong:** layer names / palette family names are NUL-terminated strings inside `DATA` (`iff.read_string` / `insertChunkString` `[VERIFIED: TerrainGenerator.cpp:210,255]`). Editing a name changes the payload length, which changes the chunk length field and the parent FORM lengths.
**How to avoid:** v1 scope is *scalar/enum/active-flag* edits (success criterion 3) — name edits change length and ripple parent sizes. `IffWriter` recomputes container lengths from children on dirty re-emit, so it's safe IF you go through the DOM, but confirm the writer recomputes ancestor FORM lengths (it does for dirty subtrees). Recommend: keep name-editing out of v1 unless a round-trip fixture proves the length ripple; criterion 3 names "scalar/enum leaf values and toggle active flag" — active flag is an int32 (fixed length), safest first target.

## Code Examples

### Recurse the layer tree (read path)
```csharp
// Model after TerrainGenerator.cpp load_0000 (lines 2174-2212) + Layer::load (1451+)
// Source: swg-client-v2/sharedTerrain/.../TerrainGenerator.cpp
// TGEN -> 0000 -> [6 palettes in order] -> LYRS? -> LAYR* (each LAYR recurses)
IffContainerChunk tgen = (IffContainerChunk)doc.Root;        // SubTypeId == "TGEN"
IffContainerChunk v0000 = FirstFormChild(tgen, "0000");
DecodePalettesInOrder(v0000);                                 // positional, NOT by tag (Pitfall 4)
IffContainerChunk lyrs = OptionalFormChild(v0000, "LYRS");    // optional (Pitfall 3)
foreach (var layr in FormChildren(lyrs, "LAYR"))
    layers.Add(DecodeLayer(layr));                            // recurses into sub-LAYR
```

### Byte-exact single-field edit (save path)
```csharp
// Source: codebase MutableIffNode.cs:255 (GetPayloadCopy) + SetPayload + IffWriter.cs:103
MutableIffNode dataLeaf = mutable.FindByStableId(targetId);   // e.g. ".../AHCN/.../0000/DATA:DATA/0"
byte[] payload = dataLeaf.GetPayloadCopy();                   // [int32 operation][float height]
WriteFloatBE(payload, offset: 4, newHeight);                 // replace ONLY the height field
dataLeaf.SetPayload(payload);                                 // marks IsDirty; everything else verbatim
byte[] outBytes = IffWriter.Write(mutable);                  // no-pad, recompute ancestor lengths
```

### Typed leaf decode with raw-fallback
```csharp
// Source: pattern from Particle/ObjectTemplate decoders (codebase)
switch (tag) {
    case "AHCN": return DecodeHeightConstant(version, dataBytes);   // Tier-1 typed
    case "BCIR": return DecodeBoundaryCircle(version, dataBytes);   // Tier-1 typed
    // ... Tier-1 set ...
    default:     return RawFallback(tag, version, dataBytes);       // never throw (criterion 2)
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| C++ streaming `Iff` enter/exit + runtime generator entanglement (`prepare`, fractal sampling) | Utinni managed `IffDocument` DOM, structure-only decode, preview deferred to live-in-client | this project | Avoids x86/bridge dependency; decode is pure managed and testable headless |
| Whole-file re-serialize | Hybrid captured-slice DOM (clean=verbatim, dirty=re-emit) | Phase 8 (D-07) | Byte-exact edits without touching unrelated chunks |
| `ParseArguments<T...>` (≤16 verbs) | `Type[]` overload + object `MapResult` | Phase 13 | Verb count unbounded; ceiling is a non-issue now |

**Deprecated/outdated:** the SOE TerrainEditor tool's structural-authoring surface (100+ files) is explicitly OUT of scope (future milestone). The `Sampler*ProceduralTerrainAppearance` classes (2D preview) are deferred.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The Tier-1 vs Tier-2 (typed vs raw-fallback) tag split matches the modder's real edit needs | Tag Taxonomy | LOW — discuss-phase can rebalance; raw-fallback is the safety net so nothing breaks |
| A2 | `apply-save-trn` (field-aware) is preferred over reusing `apply-save-iff` | Alternatives / Pitfall 1 | LOW-MED — if the team accepts whole-payload hex edits, a verb could be skipped; but criterion 3 reads as field-level |
| A3 | Name-edits (variable-length) are out of v1 scope; v1 = fixed-length scalar/enum/active edits | Pitfall 7 | MED — if name editing is required, plan must prove the length-ripple round-trip explicitly |
| A4 | `decode-iff` should grow a TGEN branch (so MCP works for free) AND a `decode-trn` alias exists | Verbs / Alternatives | LOW — both are cheap; minimal answer is decode-iff branch + roundtrip/apply-save verbs |
| A5 | Restoration `.trn` differs from SWGEmu only via higher per-form version numbers (no separate container) | SWGEmu vs Restoration | MED — VERIFY against a real Restoration `.trn` if one is reachable; the source shows only version-number divergence, but the team has live access to both clients |

## Open Questions

1. **Do we have (or can we synthesize from a real asset) a Restoration-lineage `.trn` to confirm A5?**
   - What we know: the C++ source expresses lineage as version numbers; FloraGroup spans `0001`-`0008`, ShaderGroup `0000`-`0006`.
   - What's unclear: which exact versions ship in each live client the user mods.
   - Recommendation: in discuss-phase, ask the user to drop one `.trn` from each client (or `decode-iff inspect` one) to pin the version pair; otherwise synthesize a low-version + high-version fixture per Tier-1 tag.

2. **`decode-trn` standalone verb vs. `decode-iff` TGEN-branch only?**
   - Recommendation: do the `decode-iff` branch (MCP gets it free) + a thin `roundtrip-trn`/`apply-save-trn`; treat a standalone `decode-trn` as optional symmetry. Let discuss-phase decide.

3. **Does `IffWriter` recompute ALL ancestor FORM lengths when a deep leaf changes length (name edit)?**
   - What we know: it re-emits dirty subtrees and recomputes container framing.
   - What's unclear: variable-length payload ripple depth.
   - Recommendation: a round-trip fixture with a deliberately length-changed leaf settles it; or keep v1 to fixed-length edits (A3).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `swg-client-v2/sharedTerrain` reference source | Format porting | ✓ | pinned SHA (read-only) | — |
| VS2026 MSBuild (`Utinni.sln`, x86) | Build managed codec + CLI | ✓ | Dev18 / v145 | — |
| `dotnet test --no-build` (xUnit) | Golden fixtures | ✓ | net4.7.2 + net10 lanes | — |
| net10 SDK (`Utinni.Mcp`) | MCP read tool | ✓ | net10 | — |
| Retail/Restoration `.trn` asset | Fixture A5 confirmation | ✗ (unconfirmed) | — | Synthesize ≤200-byte fixtures via `IffWriter` (preferred regardless) |

**Missing with no fallback:** none. **Missing with fallback:** a confirmed Restoration `.trn` (fallback: synthesize both-version fixtures; ask user in discuss-phase).

## Validation Architecture

> `workflow.nyquist_validation` not confirmed false in config — section included.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (`Utinni.Cli.Tests` net4.7.2, `Utinni.Mcp.Tests` net10, `UtinniCoreDotNet.Tests` net4.7.2) |
| Config file | per-project `.csproj`; build via MSBuild, run `dotnet test --no-build` |
| Quick run command | `dotnet test Utinni.Cli.Tests --no-build` |
| Full suite command | `dotnet test --no-build` (all lanes) + native Catch2 (unaffected here) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TRN-01 | Navigate TGEN tree, names+active+palettes | unit | `dotnet test Utinni.Cli.Tests --filter TgenDecode --no-build` | ❌ Wave 0 |
| TRN-02 | Typed Tier-1 tags + raw-fallback on unknown | unit | `... --filter TgenRawFallback` | ❌ Wave 0 |
| TRN-03 | Byte-exact field edit + active toggle | golden roundtrip | `... --filter ApplySaveTrn` + `RoundtripTrn` | ❌ Wave 0 |
| TRN-04 | Verbs + MCP, BOTH lineages | golden + MCP | `dotnet test Utinni.Mcp.Tests --filter Terrain --no-build` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test Utinni.Cli.Tests --no-build` (codec/verb lane).
- **Per wave merge:** full `dotnet test --no-build` across CLI + MCP + CoreDotNet lanes.
- **Phase gate:** full suite green (incl. both-lineage roundtrip goldens) before `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `Utinni.Cli.Tests/Fixtures/trn/` — synthesized `.trn` fixtures (low-version "SWGEmu" + high-version "Restoration", each ≤200 bytes) covering: minimal TGEN (no palettes/layers), one Tier-1 affector (`AHCN`), one Tier-1 boundary (`BCIR`), one unknown-tag (raw-fallback), one DEAD tag (skip).
- [ ] `TgenDecoderTests.cs` — navigation + typed-field + raw-fallback assertions (TRN-01/02).
- [ ] `RoundtripTrnTests.cs` — byte-exact whole-file identity across the fixture matrix (TRN-03/04).
- [ ] `ApplySaveTrnTests.cs` — single-field edit + active toggle, untouched-leaf byte-identity (TRN-03).
- [ ] `Utinni.Mcp.Tests/Fixtures/` + `TerrainReadToolTests.cs` — MCP thin-wrapper dispatch (TRN-04).
- [ ] Fixture synthesizer helper (hand-emit `TGEN` via `IffWriter`) — shared test fixture.

## Security Domain

> `security_enforcement` not set false — section included. This phase is a local file codec; the only attack surface is path containment on save.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | n/a (local tool) |
| V3 Session Management | no | n/a |
| V4 Access Control | no | n/a |
| V5 Input Validation | **yes** | Defensive IFF parse: `IffReader` already guards EOF short-read, malformed FourCc, length overrun; the decoder must raw-fallback (not throw) on unknown tags and bounds-check every field read against the leaf payload length (Pitfall 5). |
| V6 Cryptography | no | n/a (v6000+ TRE encryption is enumerate-only and upstream of this codec) |
| V12 File / Path | **yes** | `apply-save-trn` must contain the save path under `--root` (fail-closed), exactly as `apply-save-iff` does `[VERIFIED: ApplySaveIffCommand.cs:44]`. No path escape. |

### Known Threat Patterns for a local IFF codec
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Malformed/truncated `.trn` triggers over-read | Tampering / DoS | Bounds-check every field read vs. leaf length; raw-fallback on unknown version (Pitfall 5); reuse `IffReader` EOF guards |
| Path traversal on save | Tampering | `--root` containment, atomic write (reuse `apply-save-iff` mechanism) |
| Length-ripple corruption on edit | Tampering | Round-trip golden gate; go through `MutableIffDocument` (recomputes container lengths) |

## Sources

### Primary (HIGH confidence)
- `D:/Code/swg-client-v2/src/engine/shared/library/sharedTerrain/src/shared/generator/` — `TerrainGenerator.{cpp,h}` (top-level TGEN load/save, layer recursion, palette load order), `TerrainGeneratorType.{h,def}` (full FourCC + enum taxonomy), `TerrainGeneratorLoader.cpp` (tag→class dispatch + DEAD tags), `AffectorHeight.cpp`/`AffectorShader.cpp` (typed leaf field layout + familyId ref), `Boundary.cpp`/`Filter.cpp` (feather/version fields), `ShaderGroup.cpp`/`FractalGroup.cpp`/`BitmapGroup.cpp` (palette versions + `MGRP` collision).
- Utinni repo (read this session): `UtinniCoreDotNet/Formats/Iff/{IffReader,IffWriter,IffDocument,MutableIffDocument,MutableIffNode}.cs`; `Utinni.Cli/Program.cs` (verb dispatch); `Utinni.Cli/Commands/{DecodeIff,RoundtripParticle,ApplySaveIff}Command.cs`; `Utinni.Mcp/Tools/ReadTools.cs`; `UtinniCoreDotNet/Formats/Decoders/`.

### Secondary (MEDIUM confidence)
- `.planning/REQUIREMENTS.md`, `.planning/ROADMAP.md` (Phase 20 block), `.planning/STATE.md` (carried locks), `AGENTS.md`/`CLAUDE.md` (invariants).

### Tertiary (LOW confidence)
- None — all claims are sourced to the reference corpus or the live repo.

## Metadata

**Confidence breakdown:**
- Tag taxonomy / TGEN structure: HIGH — ported directly from the pinned reference source.
- Utinni-side patterns (DOM, verbs, MCP): HIGH — read from the live repo this session.
- SWGEmu-vs-Restoration version pair specifics: MEDIUM — source shows version-number divergence; exact shipped versions per client unconfirmed (A5/Open Q1).
- Edit-path length-ripple for name edits: MEDIUM — recommend keeping v1 to fixed-length edits (A3) pending a round-trip fixture.

**Research date:** 2026-06-15
**Valid until:** ~2026-09-15 (90 days — the reference source is a pinned SHA and Utinni's codec stack is stable; only re-validate if the IFF DOM or verb-dispatch internals change).
