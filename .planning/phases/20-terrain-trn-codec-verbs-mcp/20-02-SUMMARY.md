---
phase: 20-terrain-trn-codec-verbs-mcp
plan: 02
subsystem: terrain-trn-codec
tags: [terrain, trn, tgen, decoder, field-layouts, descriptor-table, palettes, raw-fallback, wave-1]
requires:
  - "TgenEraVersions + TgenFixtureSynthesizer (Plan 01 — consumed, not re-created)"
  - "IffReader / IffWriter / MutableIffDocument / IffPayloadCursor IFF DOM (the decode + re-emit primitives)"
provides:
  - "TgenFieldLayouts: the SINGLE-SOURCE per-tag x version binary field-descriptor table (offset/width/signedness/endian/enum-width/display-type/parser/editable) consumed by BOTH the decoder (this plan) and the Plan-03 encoder"
  - "TgenDecoder: version-first dispatch + recursive layer-tree walk + descriptor-driven typed decode + raw/truncated/unknown-version fallback + positional palette state machine + DEAD-skip + LooksLikeTerrain sniff"
  - "Terrain/ model: TerrainDocument (FromBytes/FromIff hold the MutableIffDocument + Serialize), TerrainLayer (name+active+children), TerrainNode (typed | IsRawPreserved | IsDeadSkipped + IsEditable + StableIdPath), TerrainPalettes (six positional familyId->name slots, Present/Ambiguous), TerrainParseException"
affects:
  - "Plan 03 (encoder/apply-save-trn): reads the SAME TgenFieldLayouts table; TerrainDocument.Serialize is the byte-exact roundtrip foundation; IHDR active flag is the read<->write parity leaf"
  - "Plan 04 (verbs/MCP): decode-iff TGEN branch routes through TgenDecoder.LooksLikeTerrain + TerrainDocument; AFCN version still-ASSUMED resolution lands there"
tech-stack:
  added: []
  patterns:
    - "Single-source binary field-descriptor table (offset-parity tested) shared by decoder + encoder"
    - "Version-first dispatch with whole-chunk raw-fallback; typed ONLY when consumed-length == payload-length"
    - "Sequential optional-slot palette state machine; positional MGRP disambiguation; lone MGRP -> Ambiguous"
key-files:
  created:
    - "UtinniCoreDotNet/Formats/Terrain/TgenFieldLayouts.cs"
    - "UtinniCoreDotNet/Formats/Terrain/TerrainDocument.cs"
    - "UtinniCoreDotNet/Formats/Terrain/TerrainLayer.cs"
    - "UtinniCoreDotNet/Formats/Terrain/TerrainNode.cs"
    - "UtinniCoreDotNet/Formats/Terrain/TerrainPalettes.cs"
    - "UtinniCoreDotNet/Formats/Terrain/TerrainParseException.cs"
    - "UtinniCoreDotNet/Formats/Decoders/TgenDecoder.cs"
    - "Utinni.Cli.Tests/Terrain/TgenFieldLayoutTests.cs"
  modified:
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj (eight new Compile Include entries — explicit-include csproj)"
    - "Utinni.Cli.Tests/Terrain/TgenDecoderTests.cs (un-Skipped + implemented the Wave-1 matrix)"
decisions:
  - "D-01..D-04 honored: Tier-1 typed set only; unknown version -> whole-chunk raw; DEAD recognized-and-skipped; six palettes read-only positional"
  - "TgenDecoder lands in the Task-1 commit (not Task-2) because TerrainDocument forward-references it and the csproj is explicit-include — a buildable per-task commit requires it present"
  - "Active-flag read parity (#10): TerrainLayer.Active is read from the IHDR layer-item-header int32 when present; absent IHDR -> Active defaults true (the C++ LayerItem default), matching the synthesizer's minimal layers"
requirements-completed: [PROD-W2-TRN-01, PROD-W2-TRN-02]   # added 2026-06-30 (v2.1 audit hygiene; covered by 20-VERIFICATION 4/4)
metrics:
  duration: "~11m wall (2026-06-16 13:04 -> 13:15 UTC)"
  completed: "2026-06-16"
  tasks_completed: 2
  files_created: 8
  files_modified: 2
---

# Phase 20 Plan 02: TGEN Decode Path — Single-Source Field Layouts + Navigable Layer Tree Summary

Built the genuinely-new read path of the terrain codec: a single-source `TgenFieldLayouts` binary
field-descriptor table (the reviewers' #2 ask — consumed by BOTH this decoder and Plan-03's encoder),
a `TgenDecoder` that version-first-dispatches the `TGEN→LYRS→LAYR` tree and typed-decodes the Tier-1
tags THROUGH that table, a positional palette state machine mirroring C++ `load_0000` optional-slot
semantics, explicit non-editable gating for raw/truncated/unknown-version nodes, physical-path stable
ids that include raw/dead siblings, and the `Terrain/` model with `Serialize()` for the Plan-03
roundtrip path. TRN-01/TRN-02 hold; the descriptor table is offset-parity-tested before any encode exists.

## What Was Built

### Task 1 — `TgenFieldLayouts` (single-source descriptor table) + `Terrain/` model (commit `9c9f1e4`)

- **`TgenFieldLayouts`** — a static `(tag, version) → IReadOnlyList<TgenFieldDescriptor>` table keyed at
  EXACTLY the versions pinned in `TgenEraVersions` (Plan 01). Each `TgenFieldDescriptor` is a sealed
  get-only record of `Name/Offset/Width/Signed/Endian/EnumStorageWidth/DisplayType/EditParser/Editable`.
  Populated for the full Tier-1 set (AHCN/AHTR/ACCN/ACRH/ASCN/ASRP/AFCN/AFSC/AFSN, BCIR/BREC, FHGT/FSLP).
  Higher-version tags carry the trailing feather block (function enum + distance float); the four
  v0000-pinned tags (AHCN/ACCN/ACRH/AFCN) carry no feather (Pitfall 5 — never read a field a version
  didn't write). `For` returns `null` (never throws) for an unrecognized pair. This is the SINGLE source
  of truth both the decoder and the Plan-03 encoder read.
- **`Terrain/` model** (mirrors `Particle/`): `TerrainDocument` (FromBytes/FromIff compose-on-DOM, HOLD
  the `MutableIffDocument`, `Serialize()` = `IffWriter.Write(held)`); `TerrainLayer` (Name/Active/Nodes/
  SubLayers); `TerrainNode` (mutually-exclusive Typed / `IsRawPreserved` / `IsDeadSkipped`, `IsEditable`
  gate, `StableIdPath`); `TerrainPalettes` (six positional `TerrainPalette` slots, each Present + Ambiguous
  + verbatim `TerrainPaletteFamily` familyId→name list); `TerrainParseException`.
- **`TgenFieldLayoutTests`** (filter `TgenLayout`, 32 green): per (tag,version), descriptor offsets are
  contiguous + non-overlapping AND widths sum to the EXACT synthesized DATA payload length (the offset-
  parity foundation Plan 03's encoder parity test builds on); unknown (tag,version) → null; named-field
  display types asserted.

### Task 2 — `TgenDecoder` + the navigation/typed/raw/palette test matrix (commit `47bd83e`)

- **`TgenDecoder`** (in `Formats/Decoders`): `LooksLikeTerrain(root)` (TGEN, or PTAT wrapping TGEN) +
  `Decode(MutableIffDocument)`/`Decode(IffDocument, bytes)`. Walks `TGEN → 0000`, deriving each node's
  `StableIdPath` from `MutableIffDocument.DeriveStableId` over the PHYSICAL DOM (raw/dead siblings keep
  their ordinal slot — no drift, #14); decodes the six palettes via a sequential optional-slot state
  machine (uniquely-tagged SGRP/FGRP/RGRP/EGRP by tag, the two MGRP slots by POSITION; lone MGRP →
  Fractal + `Ambiguous`, #3); recurses `LYRS → LAYR*` into `TerrainLayer` reading Active from the IHDR
  int32 leaf (the same leaf Plan 03 `--field active` mutates, #10); for each boundary/filter/affector
  reads the FORM version FIRST, looks up `TgenFieldLayouts.For(tag,version)`, decodes each field at its
  descriptor offset/width via the bounds-checked `IffPayloadCursor`, and emits a typed node ONLY when a
  complete descriptor exists AND consumed-length == payload-length — ANY mismatch (unknown tag,
  unrecognized version, truncated, trailing bytes, `DecoderException(Truncated)`) raw-falls-back to a
  non-editable node (#4/D-02), and the DEAD set (BALL/BSPL/AHSM/AHBM/ACBM/ASBM/AFBM) becomes
  `IsDeadSkipped` (D-03), never throwing.
- **`TgenDecoderTests`** (filters `TgenDecode` + `TgenRawFallback`, 33 green): minimal-TGEN navigation;
  compositional layer navigation; layer Name+Active; six positional palette slots; every Tier-1 tag
  typed at low AND high version with field names sourced from `TgenFieldLayouts`; AHCN operation-enum +
  height-float; BCIR feather both arms; non-drifting stable ids; the full negative battery (unknown tag,
  unrecognized version whole-chunk, truncated, trailing bytes, non-TGEN root throws, single/double MGRP
  positional + ambiguous, DEAD adjacency, byte-exact `Serialize()` roundtrip on unedited decode).

## Reconciliation: Fixture Shape vs Real `.trn` Shape (important for Plan 03/04)

The real `.trn` layer structure is `LYRS → LAYR → <version 0003> → [IHDR(active+name), ADTA, children]`
(verified in `TerrainGenerator.cpp` `Layer::load` :1451 + `LayerItem::load` :172/:203). The Wave-0
`TgenFixtureSynthesizer` COLLAPSES this to `LYRS → FORM <version "0003"> → [typed forms]` — no intervening
`LAYR` sub-type form and **no IHDR**. `TgenDecoder` was written to navigate BOTH shapes: a child of LYRS
is treated as a layer whether its SubTypeId is `LAYR` (descend into its version-form body) or a version
digit-string (the synthesizer's collapsed shape); Active is read from an IHDR leaf when present, else it
defaults to `true` (the C++ `LayerItem` default at :130). When Plan 03/04 exercise real assets, the IHDR
read path is already wired — and is the read↔write parity leaf for `--field active` (#10).

## Deviations from Plan

### Auto-fixed / structural (no architectural change)

**1. [Rule 3 — Blocking] `TgenDecoder.cs` lands in the Task-1 commit, not Task-2**
- **Found during:** Task 1 build.
- **Issue:** `TerrainDocument.FromBytes/FromIff` (a Task-1 file, with `Serialize()` per the plan's
  artifact spec) forward-reference `TgenDecoder.Decode`. `UtinniCoreDotNet.csproj` is an explicit
  `<Compile Include>` project (no SDK globbing), so a Task-1 commit that builds in isolation (CI builds
  HEAD) REQUIRES `TgenDecoder.cs` present and registered.
- **Fix:** Implemented the full `TgenDecoder` in the Task-1 commit; Task-2 then un-Skips and implements
  the decoder TEST matrix. Both commits build green at HEAD. No behavior lost — the same decoder code
  ships; only which commit holds the file moved by one.
- **Files:** `TgenDecoder.cs`, `UtinniCoreDotNet.csproj`.
- **Commit:** `9c9f1e4`.

**2. [Rule 3 — Build wiring] Eight new `<Compile Include>` entries added to `UtinniCoreDotNet.csproj`**
- The terrain files would otherwise not compile into `UtinniCoreDotNet.dll` (explicit-include csproj),
  surfacing as a `CS0234 'Terrain' namespace not found` in the test project. Added the six `Terrain/*`
  files + `TgenDecoder.cs` to the project. Test project is SDK-globbed — no edit needed there.
- **Commit:** `9c9f1e4`.

**3. [Rule 1 — Type fix] `FindContainerChild(IffContainerChunk,...)` return type corrected**
- The immutable-DOM overload (used only by `LooksLikeTerrain`) declared `MutableIffNode` but returns an
  `IffContainerChunk` (`CS0029`). Corrected the return type to `IffContainerChunk`.
- **Commit:** `9c9f1e4`.

### Clarification

- The `xUnit` runner emits "Skipping test case with duplicate ID" notices for the layout/decode theories
  on tags whose observed low version EQUALS the high version (AHTR/FHGT/FSLP/BCIR/etc. — Plan 01's key
  finding that SWGEmu == Infinity for all but BREC). These are harmless ID-dedup notices, not failures;
  the suite reports 0 failed.

No architectural deviations. No new packages (threat T-20-SC: accept — pure managed, zero installs).

## Authentication Gates

None.

## Known Stubs

None introduced by this plan. The 14 still-Skipped tests in the broader Cli.Tests lane are the Plan
01 RoundtripTrn/ApplySaveTrn (Wave 2/3 — Plan 03) and Mcp Terrain (Plan 04) scaffold stubs, not this
plan's surface. `AFCN`'s typed layout uses the still-ASSUMED v0000 (`TgenEraVersions`); its final
resolution remains Plan 04 Task 3 per the Plan 01 grounding — documented, not a blocking stub.

## Verification

- **MSBuild** `Utinni.sln /p:Configuration=Release /p:Platform=x86` → built with no errors (PowerShell-invoked).
- `Generated/UtinniCore.cs` reverted via `git checkout --` after each build (NOT committed — confirmed
  absent from both commits).
- `dotnet test Utinni.Cli.Tests --no-build -c Release --filter "Category=TgenLayout|Category=TgenDecode|Category=TgenRawFallback"`
  → **65 passed, 0 failed** (32 layout offset-parity + 33 decode/raw/palette/negative-battery).
- `dotnet test Utinni.Cli.Tests --no-build -c Release` (full lane) → **328 passed, 14 skipped, 0 failed**.
- Byte-exact `Serialize()` roundtrip on the compositional fixture asserted (the Plan-03 foundation).

## Self-Check: PASSED

- All eight created files + the SUMMARY present on disk (verified below).
- Both task commits (`9c9f1e4`, `47bd83e`) present in git log.
- No file deletions in either commit; `Generated/UtinniCore.cs` not committed.
