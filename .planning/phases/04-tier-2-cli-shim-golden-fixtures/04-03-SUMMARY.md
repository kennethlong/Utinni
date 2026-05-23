---
phase: 04-tier-2-cli-shim-golden-fixtures
plan: 03
subsystem: iff-parser-cli-command
tags: [iff-parser, inspect-iff, golden-fixtures, tier-1-tests, tier-2-tests, ea-iff-85, d01-clean-room, iter-4-algorithm]
dependency_graph:
  requires: [UtinniCoreDotNet, Utinni.Cli (04-01), Utinni.Cli.Tests (04-01)]
  provides: [IffReader, IffChunk, IffContainerChunk, IffLeafChunk, IffDocument, IffParseException, InspectIffCommand, IFF-fixtures]
  affects: [Utinni.Cli.Tests/Fixtures/iff, UtinniCoreDotNet.Tests/FormatsTests/Iff, Utinni.Cli.Tests/Commands/CommandDispatchTests.cs]
tech_stack:
  added: []
  patterns: [recursive-descent IFF parser, big-endian length fields, placeholder-insert preorder accumulator, STRICT missing-pad enforcement, dual tree+flat JSON projection, React Flow portability contract]
key_files:
  created:
    - UtinniCoreDotNet/Formats/Iff/IffParseException.cs
    - UtinniCoreDotNet/Formats/Iff/IffChunk.cs
    - UtinniCoreDotNet/Formats/Iff/IffContainerChunk.cs
    - UtinniCoreDotNet/Formats/Iff/IffLeafChunk.cs
    - UtinniCoreDotNet/Formats/Iff/IffDocument.cs
    - UtinniCoreDotNet/Formats/Iff/IffReader.cs
    - UtinniCoreDotNet.Tests/FormatsTests/Iff/IffReaderFixtures.cs
    - UtinniCoreDotNet.Tests/FormatsTests/Iff/IffReaderTests.cs
    - Utinni.Cli.Tests/Fixtures/iff/synthetic-nested.iff
    - Utinni.Cli.Tests/Fixtures/iff/synthetic-nested.expected.json
    - Utinni.Cli.Tests/Fixtures/iff/synthetic-secondary.iff
    - Utinni.Cli.Tests/Fixtures/iff/synthetic-secondary.expected.json
    - Utinni.Cli.Tests/Fixtures/iff/malformed-nested-overflow.iff
    - Utinni.Cli.Tests/Fixtures/iff/malformed-nested-overflow.expected.json
    - Utinni.Cli.Tests/Fixtures/iff/malformed-truncated.iff
    - Utinni.Cli.Tests/Fixtures/iff/malformed-truncated.expected.json
    - Utinni.Cli.Tests/Fixtures/iff/malformed-missing-padbyte.iff
    - Utinni.Cli.Tests/Fixtures/iff/malformed-missing-padbyte.expected.json
    - Utinni.Cli.Tests/Commands/InspectIffCommandTests.cs
  modified:
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj
    - Utinni.Cli/Commands/InspectIffCommand.cs
    - Utinni.Cli.Tests/Commands/CommandDispatchTests.cs
decisions:
  - "IFF parser uses placeholder-insert pattern for preorder accumulator: container reserves slot before child recursion, patches after construction — clean O(n) preorder without post-sort"
  - "stable chunk id format: FORM:WSNP/0 for root, FORM:WSNP/0/DATA:DATA/0 for leaf children — TypeId.TrimEnd():SubType/ordinal for containers, TypeId.TrimEnd():TypeId.TrimEnd()/ordinal for leaves"
  - "CAT  trailing space preserved in typeId field, trimmed in id path component — both projections consistent"
  - "Rule 1 auto-fix: removed inspect-iff InlineData stub row from CommandDispatchTests (inspect-iff now exits 3 on missing path, not 1)"
metrics:
  duration: "45 minutes"
  completed: "2026-05-22"
  tasks: 3
  files: 19
---

# Phase 4 Plan 03: IFF Parser + inspect-iff Summary

**One-liner:** Pure-C# EA-IFF-85 recursive-descent reader (PROP-as-leaf, STRICT missing-pad at parentEnd, iter-4 algorithm with 5 distinct reachable error paths) + inspect-iff CLI command with dual tree+flat JSON projection + 5 synthesized fixtures + 15 Tier-1 + 8 Tier-2 tests green.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 3.1 | IFF parser + Tier-1 tests | 4514e0e | IffReader.cs, IffChunk.cs, IffContainerChunk.cs, IffLeafChunk.cs, IffDocument.cs, IffParseException.cs, IffReaderTests.cs, IffReaderFixtures.cs, UtinniCoreDotNet.csproj |
| 3.2 | inspect-iff CLI command | 6206399 | InspectIffCommand.cs |
| 3.3 | IFF fixtures + Tier-2 goldens + cross-view id assertion | 6590ad5 | 5x .iff, 5x .expected.json, InspectIffCommandTests.cs, CommandDispatchTests.cs (stub row removed) |

## IFF Parser API Surface

### IffReader (UtinniCoreDotNet/Formats/Iff/IffReader.cs)

```
public static IffDocument Read(string path)
public static IffDocument Read(Stream input)       // testability seam
private const int MaxChunkSize = 64 * 1024 * 1024  // T-04-DoS cap
private static readonly HashSet<string> ContainerTypeIds = { "FORM", "LIST", "CAT " }  // REVIEWS MEDIUM-14: PROP excluded
```

### IffParseError Enum (iter-4 HIGH-1 taxonomy — each value has a distinct reachable code path)

| Value | Code Path |
|-------|-----------|
| NegativeLength | length < 0 check before bounds checks |
| ChunkLengthExceedsCap | length > 64 MB check (T-04-DoS) |
| NestedChunkOverflow | chunkStart+8+length > parentEnd, nested only |
| Truncated | BinaryReader.ReadBytes short-read; ReadInt32Be short-read; odd-length at parentEnd with no pad byte |
| MalformedFourCc | byte < 0x20 or > 0x7E in TypeID or SubTypeID |

Note: `ChunkLengthExceedsFile` value is REMOVED under iter-4 HIGH-1.

### Stable Id Format

- Root container: `FORM:WSNP/0`
- Container child: `FORM:WSNP/0/FORM:OBJS/3`
- Leaf child: `FORM:WSNP/0/DATA:DATA/0`
- Rule: `parentIdPrefix + TypeId.TrimEnd() + ":" + subTypeTrimmed + "/" + ordinal` for containers; `parentIdPrefix + TypeId.TrimEnd() + ":" + TypeId.TrimEnd() + "/" + ordinal` for leaves

## REVIEWS Revisions Applied

| Finding | Applied |
|---------|---------|
| HIGH-6 (envelope shape) | result = { source, tree, flat }; JsonOutput.EmitSuccess wraps at ROOT; no nested schemaVersion/command; Tier-2 regression guard test |
| MEDIUM-11 (STRICT missing-pad at EOF) | Throws Truncated when odd-length leaf at parentEnd with no pad byte; malformed-missing-padbyte.iff fixture + Tier-1 test |
| MEDIUM-12 (no real-sample) | Synthesized-only fixture set; synthetic-secondary.iff replaces real-sample.iff |
| MEDIUM-14 (PROP is leaf) | ContainerTypeIds = { FORM, LIST, CAT  } only; Tier-1 regression guard + synthetic-secondary.iff exercises CAT +PROP |
| LOW-21 (fixture rename) | synthetic-nested.iff (was synthetic-5-chunk.iff) |

## Iter-3 Revisions Applied

| Finding | Applied |
|---------|---------|
| MED-3 (parentEnd boundary for pad-byte) | Pad-byte check uses parentEnd not stream.Length; `Read_OddLengthLeafAtParentEnd_NoPad_ThrowsTruncated` test |

## Iter-4 Revisions Applied

| Finding | Applied |
|---------|---------|
| HIGH-1 (algorithm restructure) | ChunkLengthExceedsFile enum removed; top-level FORM IS the file (no file-bound check on root); NestedChunkOverflow only for nested chunks; streaming-read EOF is authoritative Truncated path; malformed-nested-overflow.iff (renamed+redesigned, 100B) and malformed-truncated.iff (redesigned, 50B) |

## Committed Fixture Summary

| Fixture | Size | Purpose | Expected Error Kind |
|---------|------|---------|---------------------|
| synthetic-nested.iff | 100 B | FORM/WSNP with 3 outer leaves + nested FORM/OBJS (7 nodes); exercises DESC odd-length pad | exit 0 |
| synthetic-secondary.iff | 38 B | CAT  + 2 PROP leaves; exercises trailing-space TypeID + PROP-as-leaf (REVIEWS MEDIUM-14) | exit 0 |
| malformed-nested-overflow.iff | 100 B | outer FORM len=92, inner FORM len=90 → end=110 > parentEnd=100 | NestedChunkOverflow (exit 2) |
| malformed-truncated.iff | 50 B | outer FORM len=92 (top-level, no file-bound check), inner DATA len=80, file ends at 50 → short-read | Truncated (exit 2) |
| malformed-missing-padbyte.iff | 27 B | odd-length leaf (7), no pad byte, parentEnd=27 | Truncated (exit 2) |

## Test Count

| Layer | Test Class | Count |
|-------|-----------|-------|
| Tier-1 | IffReaderTests | 15 |
| Tier-2 | InspectIffCommandTests | 8 |
| **Total Plan 04-03** | | **23** |

Combined across all Phase 4 plans: UtinniCoreDotNet.Tests = 131 / Utinni.Cli.Tests = 32.

## D-01 Clean-Room Confirmation

All 6 files under `UtinniCoreDotNet/Formats/Iff/` carry the D-01 disposition addendum:
```
// Format understood by reading swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}
// (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. No code,
// comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.
```

No identifiers, variable names, or comment text was copied from `Iff.h`/`Iff.cpp`.

## Parser Purity Confirmed

`grep -rn "Newtonsoft" UtinniCoreDotNet/Formats/Iff/` returns zero matches.
`grep -rn "Console\." UtinniCoreDotNet/Formats/Iff/` returns zero matches.
All JSON serialisation lives exclusively in `Utinni.Cli/Commands/InspectIffCommand.cs`.

## React Flow Portability Contract

The verbatim "Do NOT drop the flat view" note is embedded in:
- `IffReader.cs` class xmldoc
- `IffDocument.cs` class xmldoc
- `InspectIffCommand` class comment

Both `tree` and `flat` views derive from the same parser pass; stable `id` strings across both projections enable direct `useNodesState(json.result.flat.nodes)` and `useEdgesState(json.result.flat.edges)` calls without re-shaping.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] CommandDispatchTests inspect-iff stub row breaks after implementation**
- **Found during:** Task 3.3 (parallel to Plan 04-02 Rule 1 precedent)
- **Issue:** `Run_WithVerbStub_ExitsOneAndEmitsNotImplementedJson` Theory had `[InlineData("inspect-iff", "/tmp/anything.iff")]` which now exits 3 (FileNotFound) instead of 1 (NotImplemented).
- **Fix:** Removed the inspect-iff InlineData row; comment updated to reflect only validate-plugin remains as a stub.
- **Files modified:** `Utinni.Cli.Tests/Commands/CommandDispatchTests.cs`
- **Commit:** 6590ad5

**2. [Rule 2 - Missing functionality] Test count upgraded: 15 Tier-1 tests (plan said 13)**
- **Found during:** Task 3.1 implementation
- **Issue:** Two additional tests added beyond the plan's 12+1=13: `Read_CorrectBigEndianLength_ParsesExpectedLength` (positive case for correct big-endian) and `IffReader_SourceFile_ContainsNoNewtonsoftOrConsoleReferences` (parser purity).
- **Fix:** Kept both tests as they add correctness coverage without contradiction.
- **Commit:** 4514e0e

## Threat Flags

None. Plan 04-03 adds file-read CLI commands. The `source` field in JSON output exposes the operator-supplied path verbatim (T-04-V12 disposition: sandboxing is Phase 6+). T-04-V5 and T-04-DoS mitigations are confirmed: NegativeLength and ChunkLengthExceedsCap checks fire before any buffer allocation.

## Known Stubs

| Stub | File | Reason |
|------|------|--------|
| `ValidatePluginCommand.Run` returns NotImplemented | `Utinni.Cli/Commands/ValidatePluginCommand.cs` | Plan 04-04 implements plugin validation |

## Self-Check: PASSED
