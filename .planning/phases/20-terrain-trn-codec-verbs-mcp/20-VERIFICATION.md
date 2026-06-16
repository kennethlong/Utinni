---
phase: 20-terrain-trn-codec-verbs-mcp
verified: 2026-06-16T00:00:00Z
status: passed
score: 4/4 must-haves verified
overrides_applied: 0
---

# Phase 20: Terrain `.trn` Codec + Verbs + MCP Verification Report

**Phase Goal:** A modder (and an AI agent) can decode, navigate, edit, and byte-exactly save a procedural `.trn` TerrainGenerator graph through golden-tested CLI verbs and an MCP read tool, across both SWG lineages.
**Verified:** 2026-06-16
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (ROADMAP Success Criteria)

| # | Truth | Status | Evidence |
| --- | ----- | ------ | -------- |
| 1 | Open a `.trn` and navigate the procedural layer tree (TGEN→Layers→Boundaries/Filters/Affectors/sub-layers) with names + active flags; view six shared palettes read-only | ✓ VERIFIED | `TgenDecoder.Decode` walks TGEN→(PTAT wrap)→body→LYRS→LAYR* recursively (`DecodeLayer`, sub-layer recursion), reads layer Name+Active from the IHDR int32 leaf (`ReadLayerItemHeader`), decodes six palettes positionally via the sequential optional-slot state machine with MGRP positional disambiguation (`DecodePalettes`, lone MGRP→Ambiguous). `DecodeIffCommand.BuildTerrainResult` emits the navigable envelope (`layers[]` with name/active/children/subLayers + `palettes[]` slot/present/ambiguous/families). Tests: `TgenDecode`/`TgenRawFallback` lanes green (part of 148 passed). |
| 2 | Common terrain tags display as typed fields; unknown/long-tail tags degrade to a generic raw field list — never a hard decode failure | ✓ VERIFIED | `TgenDecoder.DecodeNode`: version read FIRST; typed node emitted ONLY when a complete `TgenFieldLayouts.For(tag,version)` descriptor exists AND consumed-length == payload-length; ANY mismatch (unknown tag, unrecognized version, truncated via `SliceAt`→`DecoderException`, trailing bytes) → `TerrainNode.RawPreserved` non-editable. DEAD set (BALL/BSPL/AHSM/…) → `DeadSkipped`, never throws. Tier-1 set (AHCN/AHTR/ACCN/ACRH/ASCN/ASRP/AFCN/AFSC/AFSN/BCIR/BREC/FHGT/FSLP) populated in `TgenFieldLayouts`. Negative battery in `TgenDecoderTests`; `TgenLayout` offset-parity green. |
| 3 | Edit + save scalar/enum leaf values and toggle a layer/affector active flag, byte-exact, via the loose-override save matrix | ✓ VERIFIED | `TrnFieldEncoder.EncodeField` overwrites ONLY the descriptor's `[offset..offset+width)` span, copies every other byte verbatim (untouched floats keep exact bits; NaN/Infinity rejected). `ApplySaveTrnCommand`: `.tre` reject → `LooseOverridePath.Resolve` fail-closed containment → `IsEditable` gate (raw/truncated/DEAD rejected, no write) → `ResolveFieldContext` parent-walk recovers (tag,version) → encode ONE field → `IffWriter.Write` → re-parse → untouched-leaf byte-identity + exact-span verify → atomic `WriteAtomic` only on clean verify. `--field active` writes the int32 @ offset 0 of the IHDR leaf (the SAME leaf the decoder reads). Tests: `ApplySaveTrn` (incl. `ApplySave_ToggleActive_RoundtripDecodeReflectsEdit` read↔write parity) + `RoundtripTrn` whole-file byte-identity green. |
| 4 | `.trn` decode/edit/save exposed as golden-tested utinni-cli verbs (decode-trn/roundtrip-trn/apply-save-trn) + an MCP read tool, validated against a both-lineage fixture matrix | ✓ VERIFIED | Three verbs registered in `Program.cs` (`DecodeTrnOptions`/`RoundtripTrnOptions`/`ApplySaveTrnOptions` → Dispatch); `decode-iff` auto-routes TGEN/PTAT (`LooksLikeTerrain`→`BuildTerrainResult`). MCP `summarize_terrain` `[McpServerTool(ReadOnly=true)]` shells `decode-iff` (`cli.RunAsync("decode-iff",…)`→`CliResultMapper.ToCallToolResult`) — ZERO format logic (MCP-OOP). Verb-ceiling confirmed by test-only `trn-smoke` sentinel (no shipped no-op). Both-lineage matrix: `SwgEmuEra`/`InfinityEra` constants pinned to observed real assets, BREC the one genuine 0002/0003 divergence pair. Tests: `TrnVerbGolden` + `RoundtripTrn`/`PaletteLineage` (148 CLI) + MCP `Terrain` (10) green. |

**Score:** 4/4 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
| -------- | -------- | ------ | ------- |
| `UtinniCoreDotNet/Formats/Terrain/TgenFieldLayouts.cs` | Single-source per-tag×version descriptor table | ✓ VERIFIED | 16.7 KB; `TgenFieldDescriptor` records; consumed by both decoder + encoder (offset-parity tested) |
| `UtinniCoreDotNet/Formats/Decoders/TgenDecoder.cs` | Version-first dispatch + layer walk + palette machine + raw-fallback + DEAD-skip + LooksLikeTerrain | ✓ VERIFIED | 25.9 KB; substantive, no stubs; wired into DecodeIffCommand |
| `UtinniCoreDotNet/Formats/Terrain/{TerrainDocument,TerrainLayer,TerrainNode,TerrainPalettes,TerrainParseException}.cs` | Navigable model + Serialize() | ✓ VERIFIED | All present; `TerrainDocument.Serialize`=`IffWriter.Write(held)`; `TerrainNode` IsEditable/IsRawPreserved/IsDeadSkipped gates |
| `UtinniCoreDotNet/Formats/Terrain/TrnFieldEncoder.cs` | Exact-byte-span single-field re-encode | ✓ VERIFIED | 12.3 KB; reads shared `TgenFieldLayouts`; byte-verbatim copy of untouched bytes; NaN/Inf rejected |
| `Utinni.Cli/Commands/{DecodeTrnCommand,RoundtripTrnCommand,ApplySaveTrnCommand}.cs` | Three verbs | ✓ VERIFIED | All present + registered in Program.cs; ApplySaveTrn 21.5 KB with full fail-closed verify path |
| `Utinni.Cli/Commands/DecodeIffCommand.cs` | TGEN branch + navigable envelope | ✓ VERIFIED | `LooksLikeTerrain`→`BuildTerrainResult` (layer tree + palettes + typed/raw/dead) |
| `Utinni.Mcp/Tools/ReadTools.cs` | summarize_terrain thin shell | ✓ VERIFIED | `[McpServerTool(Name="summarize_terrain",ReadOnly=true)]`; shells decode-iff; zero format logic |
| `Utinni.Cli.Tests/Fixtures/trn/TgenEraVersions.cs` | Pinned era constants (relabel) | ✓ VERIFIED | `SwgEmuEra`/`InfinityEra`; `git grep RestorationEra` → zero matches; BREC 0002/0003 divergence; AFCN annotated ASSUMED |

### Key Link Verification

| From | To | Via | Status |
| ---- | -- | --- | ------ |
| `TgenDecoder` | `TgenFieldLayouts` | descriptor offset/width reads (no literals) | ✓ WIRED (`TgenFieldLayouts.For`) |
| `TrnFieldEncoder` | `TgenFieldLayouts` | same descriptor table (offset-parity) | ✓ WIRED (`TgenFieldLayouts.For`) |
| `ApplySaveTrnCommand` | `TrnFieldEncoder` | single-field re-pack → SetPayload | ✓ WIRED (`EncodeOneField`→`TrnFieldEncoder`) |
| `ApplySaveTrnCommand` | `LooseOverridePath` | fail-closed containment | ✓ WIRED (`LooseOverridePath.Resolve`) |
| `DecodeIffCommand` | `TerrainDocument` | TGEN root → FromIff | ✓ WIRED (`TerrainDocument.FromIff`) |
| `Program.cs` | three Trn verbs | Type[] ParseArguments + Dispatch | ✓ WIRED (cases 107-109) |
| `ReadTools.SummarizeTerrain` | `utinni-cli decode-iff` | CliDispatcher.RunAsync | ✓ WIRED (`cli.RunAsync("decode-iff",…)`) |
| `TgenFixtureSynthesizer` | `TgenEraVersions` | version args from era constants | ✓ WIRED (Low/High routes to Swg/Infinity) |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
| -------- | ------------- | ------ | ------------------ | ------ |
| `BuildTerrainResult` envelope | `doc.Layers`/`doc.Palettes` | `TgenDecoder.Decode` over the IFF DOM read from real file bytes | Yes — decoder walks actual chunk tree | ✓ FLOWING |
| `summarize_terrain` (MCP) | CLI stdout envelope | subprocess `utinni-cli decode-iff` | Yes — propagates verbatim; nonzero exit → tool error | ✓ FLOWING |
| `apply-save-trn` write | `mutatedBytes` | `IffWriter.Write(mutable)` after `SetPayload` | Yes — re-parsed + byte-verified before atomic write | ✓ FLOWING |

### Behavioral Spot-Checks / Probe Execution

| Behavior | Command | Result | Status |
| -------- | ------- | ------ | ------ |
| Terrain CLI codec/verb lanes | `dotnet test Utinni.Cli.Tests --no-build -c Release --filter "RoundtripTrn\|TgenLayout\|TgenDecode\|TgenRawFallback\|TrnVerbGolden\|ApplySaveTrn\|PaletteLineage"` | 148 passed, 0 failed | ✓ PASS |
| MCP terrain thin-shell lane | `dotnet test Utinni.Mcp.Tests --no-build -c Release --filter Terrain` | 10 passed, 0 failed | ✓ PASS |

> The xUnit "Skipping test case with duplicate ID" notices are harmless theory-dedup for tags whose observed low version EQUALS the high version (Plan 01's finding that SWGEmu == Infinity for all tags but BREC) — documented in 20-02-SUMMARY; the suite reports 0 failed.

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
| ----------- | -------------- | ----------- | ------ | -------- |
| PROD-W2-TRN-01 | 20-02 | Navigate procedural layer tree + read-only palettes | ✓ SATISFIED | TgenDecoder layer walk + palette machine + navigable envelope (truth 1) |
| PROD-W2-TRN-02 | 20-02 | Typed common tags; raw-fallback long-tail, never hard-fail | ✓ SATISFIED | Version-first descriptor decode + raw-fallback + DEAD-skip (truth 2) |
| PROD-W2-TRN-03 | 20-03 | Byte-exact scalar/enum edit + active toggle via loose-override | ✓ SATISFIED | TrnFieldEncoder + ApplySaveTrnCommand fail-closed verify (truth 3) |
| PROD-W2-TRN-04 | 20-01, 20-03, 20-04 | Golden-tested verbs + MCP read tool, both lineages | ✓ SATISFIED | 3 verbs + decode-iff branch + summarize_terrain + both-lineage matrix (truth 4) |

All four requirement IDs from PLAN frontmatter are present in REQUIREMENTS.md and accounted for. No orphaned requirements. REQUIREMENTS.md PROD-W2-TRN-04 reworded "Restoration"→"Infinity" (maintainer-accepted; consistent with the substitution noted in verification context). PROD-W2-TRN-05 (live in-client preview + TJT SubPanel) is correctly OUT of scope — Phase 21.

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
| ---- | ------- | -------- | ------ |
| (none) | TBD/FIXME/XXX/HACK/PLACEHOLDER scan of all phase-20 source | — | Zero debt markers found in Formats/Terrain/*, TgenDecoder.cs, and the Trn CLI commands |

### Accepted Limitations (not gaps)

- **AFCN ASSUMED v0000:** absent from every sampled planet of both clients; annotated ASSUMED in `TgenEraVersions.cs`; raw-falls-back cleanly (non-editable) if a real asset's version ever differs. Maintainer-accepted, non-blocking.
- **Infinity substituted for Restoration:** the two grounded lineages are SWGEmu + SWG Infinity (Restoration terrain is proprietary-encrypted/unreachable). ROADMAP SC#4 "Restoration" wording is historical; REQUIREMENTS.md PROD-W2-TRN-04 reworded to "Infinity". Era constants are `SwgEmuEra`/`InfinityEra`. Per verification context, NOT a gap.
- **Real-asset roundtrip SKIPPED:** maintainer decision; the synthesized low+high matrix + BREC divergence + committed MCP fixtures already cover both lineages byte-exactly; no real client asset committed (D-14).
- **AbiSurfaceTests ADDED(0)/REMOVED(20):** pre-existing Phase-17 CppSharp regen churn (ADDED=0 ⇒ zero new native ABI surface this phase — Plan 20-03 is pure managed). Documented in `deferred-items.md`. NOT a Phase-20 regression.

### Human Verification Required

None. Phase delivers pure-managed runnable code (verbs + MCP tool) fully covered by golden/roundtrip/MCP-dispatch tests; both lanes re-run green by the verifier in-process. The planner deferred no `<verify><human-check>` blocks; DEC-C3 byte-exact gate was closed at the 20-04 maintainer checkpoint. No visual/live-SWG/real-time behavior is in this phase's scope (that is Phase 21 PROD-W2-TRN-05).

### Gaps Summary

No gaps. All four ROADMAP success criteria are observably true in the committed code, every PLAN must-have artifact exists and is substantive (no stubs), every key link is wired (decoder↔descriptor↔encoder↔verb↔MCP), data flows real (decode walks actual DOM; apply-save re-parses + byte-verifies before atomic write), and the both-lineage byte-exact matrix passes (148 CLI + 10 MCP terrain tests, 0 failed). Requirement traceability is complete with no orphans. Recorded limitations (AFCN-assumed, Infinity-for-Restoration, skipped real-asset roundtrip, pre-existing ABI churn) are all maintainer-accepted decisions per the verification context, not gaps.

---

_Verified: 2026-06-16_
_Verifier: Claude (gsd-verifier)_
