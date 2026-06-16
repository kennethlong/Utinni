---
phase: 20-terrain-trn-codec-verbs-mcp
reviewed: 2026-06-16T00:00:00Z
depth: standard
files_reviewed: 16
files_reviewed_list:
  - UtinniCoreDotNet/Formats/Terrain/TgenFieldLayouts.cs
  - UtinniCoreDotNet/Formats/Decoders/TgenDecoder.cs
  - UtinniCoreDotNet/Formats/Terrain/TerrainDocument.cs
  - UtinniCoreDotNet/Formats/Terrain/TerrainLayer.cs
  - UtinniCoreDotNet/Formats/Terrain/TerrainNode.cs
  - UtinniCoreDotNet/Formats/Terrain/TerrainPalettes.cs
  - UtinniCoreDotNet/Formats/Terrain/TerrainParseException.cs
  - UtinniCoreDotNet/Formats/Terrain/TrnFieldEncoder.cs
  - Utinni.Cli/Commands/ApplySaveTrnCommand.cs
  - Utinni.Cli/Commands/DecodeTrnCommand.cs
  - Utinni.Cli/Commands/RoundtripTrnCommand.cs
  - Utinni.Cli/Commands/DecodeIffCommand.cs
  - Utinni.Cli/Program.cs
  - Utinni.Mcp/Tools/ReadTools.cs
  - UtinniCoreDotNet/UtinniCoreDotNet.csproj
  - Utinni.Cli.Tests/Fixtures/trn/TgenFixtureSynthesizer.cs
findings:
  critical: 1
  warning: 3
  info: 4
  total: 8
status: issues_found
---

# Phase 20: Code Review Report

**Reviewed:** 2026-06-16
**Depth:** standard
**Files Reviewed:** 16
**Status:** issues_found

## Summary

Phase 20 ships a pure-managed `.trn`/FORM TGEN codec (single-source field-layout table, version-first
decoder with typed/raw/DEAD tri-state, exact-byte-span field re-encoder), three CLI verbs, a `decode-iff`
TGEN branch, and a thin MCP `summarize_terrain` shell. The architecture is sound and the byte-exact
invariants are largely well-defended: the decoder reads version-first and raw-falls-back on any
descriptor/length mismatch (`TgenDecoder.cs:301-327`), the encoder overwrites only the target span and
copies the rest verbatim (`TrnFieldEncoder.cs:131-133`), `apply-save-trn` runs a defense-in-depth verify
(untouched-leaf identity + exact-span) before an atomic commit, the MCP tool holds zero format logic and
propagates nonzero exits as tool errors, and path containment reuses the proven `LooseOverridePath` guard.
NaN/Infinity float rejection, the optional-palette state machine, and the lone-MGRP "Ambiguous" handling
are all correct and tested.

The one blocker is a **stable-id prefix-collision** in `apply-save-trn`'s editability gate
(`WalkLayerForNode`), which uses an unanchored `String.StartsWith` against sibling node ids. With ten or
more sibling nodes in a layer (routine in real terrain files) the gate can resolve the WRONG sibling's
editability — every committed fixture has ≤4 siblings, so the matrix never exercises ordinals ≥10. The
remaining findings concern a misleading verb contract, a verify-gate accumulation quirk, and doc/comment
drift.

## Critical Issues

### CR-01: `apply-save-trn` editability gate uses unanchored stable-id prefix match — wrong sibling resolved at ordinals ≥10

**File:** `Utinni.Cli/Commands/ApplySaveTrnCommand.cs:350-363` (with `:341-348`)
**Issue:**
`WalkLayerForNode` decides whether the addressed leaf belongs to an editable node by:

```csharp
if (node.StableIdPath != null && leafId.StartsWith(node.StableIdPath, StringComparison.Ordinal))
    return node.IsEditable;
```

Stable ids are slash-delimited ordinal paths ending in `.../FORM:AHCN/<ordinal>` (see
`MutableIffDocument.DeriveStableId`, no trailing separator). `StartsWith` has no segment boundary, so the
path of the node at ordinal `1` (`.../FORM:AHCN/1`) is a string prefix of any leaf id inside the node at
ordinal `10` (`.../FORM:AHCN/10/FORM:0000/0/DATA:DATA/0`). Because `layer.Nodes` is walked in ascending
ordinal order and the method returns on the FIRST prefix hit, a leaf inside node-10/11/.../19 resolves to
node-1's `IsEditable` instead of its own.

Consequences (both directions are wrong):
- A typed-edit on an editable node at ordinal 10 is wrongly REJECTED when the colliding node-1 is
  raw/DEAD/non-editable (silent loss of a legitimate edit).
- A typed-edit on a NON-editable node at ordinal 10 is wrongly PERMITTED past the `#4` gate when node-1 is
  editable. The downstream encoder catches the unknown-tag and truncated cases (no descriptor / span
  overrun), but a known-tag-with-trailing-bytes node (decoded raw because consumed-length != payload-length,
  yet has a valid in-range descriptor span) would slip through and rewrite a half-understood payload —
  exactly the failure mode the `#4` gate exists to prevent.

Real terrain layers routinely carry dozens of affectors/boundaries/filters, so ordinals ≥10 are the common
case; the committed fixtures top out at 4 siblings (`CompositionalLayer`), so no test exercises this.

**Fix:** Match on a full-segment boundary instead of a raw prefix. Either compare the enclosing-node id
exactly (derive the node id from the leaf id by trimming the trailing `/FORM:<version>/<n>/DATA:DATA/<n>`
segments), or require the prefix to be followed by `/`:

```csharp
private static bool? WalkLayerForNode(TerrainLayer layer, string leafId)
{
    foreach (var node in layer.Nodes)
    {
        if (node.StableIdPath != null &&
            (leafId == node.StableIdPath ||
             leafId.StartsWith(node.StableIdPath + "/", StringComparison.Ordinal)))
            return node.IsEditable;
    }
    foreach (var sub in layer.SubLayers)
    {
        bool? r = WalkLayerForNode(sub, leafId);
        if (r != null) return r;
    }
    return null;
}
```

Add a regression fixture with ≥11 sibling nodes (mixing editable and raw/DEAD) and assert the gate resolves
each leaf to its own node.

## Warnings

### WR-01: `roundtrip-trn` reports `bytesIdentical=false` but still exits 0 — the byte-exact gate verb never fails

**File:** `Utinni.Cli/Commands/RoundtripTrnCommand.cs:80-91`
**Issue:**
The verb computes `bytesEqual` and emits it in the JSON envelope, then unconditionally returns
`JsonOutput.EmitSuccess(...)` (exit 0) even when the round-trip is NOT byte-identical. The class docstring
claims it "asserts the round-tripped bytes are IDENTICAL" (`:49-51`), but no assertion or nonzero exit
backs that claim. A CI lane or agent that gates on exit code (the natural "byte-exact gate" usage) would
treat a genuine codec regression as a pass; only a consumer that parses and checks the `bytesIdentical`
field catches it. This mirrors the pre-existing `RoundtripParticleCommand` convention, so it is a
consistency/contract issue rather than a fresh logic break — but the DEC-C3 byte-exact gate is THE codec
gate for this phase, so a verb that advertises an assertion it does not enforce is a real trap.

**Fix:** Return a nonzero exit (e.g. 2 `VerifyFailed`) when `!bytesEqual`, OR soften the docstring to "reports
byte identity in the envelope (does not fail the process)". Preferred: fail closed —

```csharp
if (!bytesEqual)
    return JsonOutput.EmitError("roundtrip-trn", "VerifyFailed",
        "round-trip was not byte-identical (whole-file).", exitCode: 2);
return JsonOutput.EmitSuccess("roundtrip-trn", result);
```

If parity with `roundtrip-particle` must be preserved, apply the same fix there or document the convention
explicitly in both.

### WR-02: `TargetTypedNodeIsEditable` accumulates last-non-null across all layers instead of stopping at the match

**File:** `Utinni.Cli/Commands/ApplySaveTrnCommand.cs:341-348`
**Issue:**

```csharp
bool? editable = null;
foreach (var layer in terrain.Layers)
    editable = WalkLayerForNode(layer, leafId) ?? editable;
return editable == true;
```

This walks EVERY top-level layer and keeps the last non-null result rather than returning on the first
match. A stable id is unique to exactly one node, so in practice at most one layer should claim it — but if
the prefix-collision in CR-01 (or any future id-derivation change) causes two layers to both "claim" the
leaf, the LAST layer's verdict silently wins, which is order-dependent and non-obvious. The `?? editable`
idiom also obscures intent (it reads as "prefer earlier" but actually prefers later because assignment
overwrites).

**Fix:** Return on the first match for clear, order-independent semantics:

```csharp
foreach (var layer in terrain.Layers)
{
    bool? e = WalkLayerForNode(layer, leafId);
    if (e != null) return e == true;
}
return false;
```

### WR-03: `apply-save-trn` `--field active` path bypasses the single-source encoder, duplicating LE int32 packing

**File:** `Utinni.Cli/Commands/ApplySaveTrnCommand.cs:305-322`
**Issue:**
The active-flag branch hand-rolls the int32 little-endian write (`result[0..3] = ...`) and re-implements
true/false/1/0 parsing, instead of routing through `TrnFieldEncoder` like every other field. This is a
second copy of the exact LE-packing logic already centralized in `TrnFieldEncoder.Int32LeWidth`
(`TrnFieldEncoder.cs:202-217`) — the "single-source" property the layout table and encoder were built to
guarantee (review concern #2). The two copies happen to agree today, but a future change to endianness or
width handling in one place would silently diverge. The active flag is not in `TgenFieldLayouts` (IHDR has
no descriptor), which is why the bypass exists, but the *encoding* primitive should still be shared.

**Fix:** Add an IHDR/`active` descriptor (`Offset=0, Width=4, ActiveFlag, Bool32, editable=true`) to the
single-source table (or expose `TrnFieldEncoder.Int32LeWidth`/a small `EncodeBool32` helper) and have the
active branch call it, so there is exactly one LE int32 packer.

## Info

### IN-01: `TgenFieldEndian` enum has a single value (`Little`) — dead generality

**File:** `UtinniCoreDotNet/Formats/Terrain/TgenFieldLayouts.cs:39-43`
**Issue:** `TgenFieldEndian` declares only `Little`, and every descriptor sets `Endian = Little`. The
`TgenFieldDescriptor.Endian` property is stored but never branched on by decoder or encoder (both assume
LE). This is speculative generality that adds a field nobody reads.
**Fix:** Either drop the enum + property until a big-endian field actually exists, or add an assertion in
the decode/encode path that rejects a non-`Little` descriptor so the unused field cannot silently lie.

### IN-02: `DecodeIffCommand` leaves the TGEN/PEFT-branch `MemoryStream` undisposed

**File:** `Utinni.Cli/Commands/DecodeIffCommand.cs:82`
**Issue:** `var doc = IffReader.Read(new MemoryStream(bytes));` constructs a `MemoryStream` that is never
disposed, unlike the `using (var ms = ...)` pattern used in the new TRN commands
(`DecodeTrnCommand.cs:88`-style, `ApplySaveTrnCommand.cs:126`). A `MemoryStream` over a byte[] holds no
unmanaged resources so impact is negligible, but it is an inconsistency the TGEN branch now routes through.
**Fix:** Wrap in `using` for consistency with the rest of the file's IFF reads.

### IN-03: `EncodeValue` default-case fall-through relies on enum exhaustiveness

**File:** `UtinniCoreDotNet/Formats/Terrain/TrnFieldEncoder.cs:185-196`
**Issue:** The `switch (d.EditParser)` collapses `Int32` and `default` into one arm. Today the only parsers
are `Float`/`Bool32`/`Int32`, so `default` is unreachable, but folding an unknown future parser into the
int32 path would silently mis-encode rather than reject. The decoder's `DecodeFields` switch
(`TgenDecoder.cs:339-352`) has the same default-as-int32 pattern.
**Fix:** Make `default` throw `ArgumentException("unhandled EditParser " + d.EditParser)` so a new parser
value fails loudly instead of defaulting to int32.

### IN-04: `roundtrip-trn` re-decodes the round-tripped bytes but only uses the result for `layerCount`

**File:** `Utinni.Cli/Commands/RoundtripTrnCommand.cs:78,87`
**Issue:** `rtModel` is produced solely to validate re-parse and to report `rtModel.Layers.Count`. The
re-parse-for-validity intent is reasonable, but combined with WR-01 (success regardless of byte equality)
the second decode adds cost without a gate. Harmless, but worth noting alongside WR-01 when deciding the
verb's failure contract.
**Fix:** Keep the re-parse for validity, but tie its (and `bytesEqual`'s) result to the exit code per WR-01.

---

_Reviewed: 2026-06-16_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
