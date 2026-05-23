---
phase: 04-tier-2-cli-shim-golden-fixtures
plan: 02
subsystem: tre-parser-cli-commands
tags: [tre-parser, parse-tre, list-objects, golden-fixtures, tier-1-tests, tier-2-tests, eager-read, d01-clean-room]
dependency_graph:
  requires: [UtinniCoreDotNet, Utinni.Cli (04-01)]
  provides: [TreFile, TreHeader, TreRecord, TreParseException, ParseTreCommand, ListObjectsCommand, TRE-fixtures, ws.iff-fixture]
  affects: [Utinni.Cli.Tests/Fixtures, UtinniCoreDotNet.Tests/FormatsTests, docs/ai/assessment.md]
tech_stack:
  added: [System.IO.Compression.DeflateStream (raw deflate for TRE records)]
  patterns: [eager-read byte[][] cache, ValidateLength security helper, OBJS sentinel byte-scan, MaskPath JSON path masking]
key_files:
  created:
    - UtinniCoreDotNet/Formats/Tre/TreFile.cs
    - UtinniCoreDotNet/Formats/Tre/TreHeader.cs
    - UtinniCoreDotNet/Formats/Tre/TreRecord.cs
    - UtinniCoreDotNet/Formats/Tre/TreParseException.cs
    - UtinniCoreDotNet.Tests/FormatsTests/Tre/TreFileTests.cs
    - UtinniCoreDotNet.Tests/FormatsTests/Tre/TreFileFixtures.cs
    - Utinni.Cli.Tests/Fixtures/tre/synthesized-3record-v0005.tre
    - Utinni.Cli.Tests/Fixtures/tre/synthesized-3record-v0005.expected.json
    - Utinni.Cli.Tests/Fixtures/tre/synthesized-2record-v0006.tre
    - Utinni.Cli.Tests/Fixtures/tre/synthesized-2record-v0006.expected.json
    - Utinni.Cli.Tests/Fixtures/tre/malformed-magic.tre
    - Utinni.Cli.Tests/Fixtures/tre/malformed-magic.expected.json
    - Utinni.Cli.Tests/Fixtures/tre/truncated.tre
    - Utinni.Cli.Tests/Fixtures/tre/truncated.expected.json
    - Utinni.Cli.Tests/Fixtures/tre/unsupported-version.tre
    - Utinni.Cli.Tests/Fixtures/tre/unsupported-version.expected.json
    - Utinni.Cli.Tests/Fixtures/world-snapshot/synthesized-ws.iff
    - Utinni.Cli.Tests/Fixtures/world-snapshot/synthesized-ws.expected.json
    - Utinni.Cli.Tests/Commands/ParseTreCommandTests.cs
    - Utinni.Cli.Tests/Commands/ListObjectsCommandTests.cs
  modified:
    - Utinni.Cli/Commands/ParseTreCommand.cs
    - Utinni.Cli/Commands/ListObjectsCommand.cs
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj
    - Utinni.Cli.Tests/Commands/CommandDispatchTests.cs
    - docs/ai/assessment.md
decisions:
  - "REVIEWS HIGH-4 fix path A — eager-read all compressed bytes into byte[][] during Open(Stream); no lazy seek after dispose"
  - "T-04-DoS check in GetRecordData: reject records claiming >256MB uncompressed before inflation"
  - "list-objects OBJS sentinel byte-scan documented as architectural debt (REVIEWS MEDIUM-13); Phase 6+ refactor on IffReader"
  - "MaskPath replaces JSON-escaped backslash form in golden tests; both raw path and \\-escaped form handled"
  - "Rule 1 auto-fix: removed parse-tre/list-objects InlineData stub rows from CommandDispatchTests (now implemented)"
metrics:
  duration: "28 minutes"
  completed: "2026-05-23"
  tasks: 5
  files: 21
---

# Phase 4 Plan 02: TRE Parser + parse-tre + list-objects Summary

**One-liner:** Pure-C# TRE container parser (v0005/v0006, eager-read byte[][] contract, 256MB deflate cap) + parse-tre + list-objects CLI commands + 10 Tier-1 unit tests + 9 Tier-2 golden tests + 6 TRE fixtures + 1 ws.iff fixture — all REVIEWS guardrails applied.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 2.1 | TRE parser + Tier-1 tests | e02813b | TreFile.cs, TreHeader.cs, TreRecord.cs, TreParseException.cs, TreFileTests.cs, TreFileFixtures.cs, UtinniCoreDotNet.csproj |
| 2.2 | parse-tre + list-objects CLI commands | 7ee47e0 | ParseTreCommand.cs, ListObjectsCommand.cs |
| 2.3 | TRE + ws.iff fixtures + expected.json goldens | 0327edc | 5x .tre + 5x .expected.json + synthesized-ws.iff + synthesized-ws.expected.json |
| 2.4 | Tier-2 golden tests | 2b45c8f | ParseTreCommandTests.cs, ListObjectsCommandTests.cs, CommandDispatchTests.cs (stub rows removed) |
| 2.5 | CON-O-09 disposition in assessment.md | 56a77ae | docs/ai/assessment.md |

## TRE Parser API Surface

### TreFile (UtinniCoreDotNet/Formats/Tre/TreFile.cs)

```
public static TreFile Open(string path)
public static TreFile Open(Stream stream)    // testability seam + eager-read enforcer
public TreHeader Header { get; }
public IReadOnlyList<TreRecord> Records { get; }
public byte[] GetRecordData(int index)        // returns from byte[][] cache — REVIEWS HIGH-4
private const int MaxBlockSize = 256 * 1024 * 1024  // T-04-V5 + T-04-DoS
private readonly byte[][] RecordCompressedBytes      // REVIEWS HIGH-4 fix path A
```

### TreHeader (field names per REVIEWS HIGH-7)
`Version`, `RecordCount`, `InfoOffset`, `InfoCompression`, `InfoCompressedSize`, `NameCompression`, `NameCompressedSize`, `NameSize`

### TreRecord (field names per REVIEWS HIGH-7)
`UncompressedSize`, `Offset`, `CompressionKind` ("none"|"deflate"), `CompressedSize`, `Checksum`, `NameOffset`, `Name`

### TreParseException
`Kind`: `BadMagic | UnsupportedVersion | Truncated | NegativeLength | ChunkLengthExceedsCap | DeflateExpansionExceedsCap`

## REVIEWS HIGH-4: Eager-Read Contract (Fix Path A)

`Open(Stream)` reads ALL record compressed bytes into `byte[][] RecordCompressedBytes` before returning.
`GetRecordData(int)` returns from this cache — never accesses the original stream.
**Proof:** `GetRecordData_AfterOpenStreamDisposed_StillReturnsBytes` Tier-1 test disposes the MemoryStream before calling GetRecordData and asserts the result is still correct.

## Committed Fixture Summary

| Fixture | Size | Key Field | Purpose |
|---------|------|-----------|---------|
| synthesized-3record-v0005.tre | 196 B | version=0005, recordCount=3 | Happy path; covers none+deflate+empty records |
| synthesized-2record-v0006.tre | 138 B | version=0006, recordCount=2 | REVIEWS LOW-20: dedicated v0006 golden |
| malformed-magic.tre | 36 B | magic=XXXX | BadMagic error path |
| truncated.tre | 60 B | recordCount=3, infoSize=24 | Truncated error (too few info bytes) |
| unsupported-version.tre | 36 B | version=0009 | UnsupportedVersion error |
| synthesized-ws.iff | 200 B | FORM/WSNP/OBJS, 3 paths | list-objects golden fixture |

All fixtures < 128 KB (D-03 cap). All `expected.json` goldens use `<fixture-path>` sentinel for the `source` field.

## Test Count

| Layer | Test Class | Count |
|-------|-----------|-------|
| Tier-1 | TreFileTests | 10 |
| Tier-2 | ParseTreCommandTests | 7 |
| Tier-2 | ListObjectsCommandTests | 2 |
| **Total Plan 04-02** | | **19** |

Combined with Plan 04-01 (18 base - 2 removed stub rows = 16 remaining): **35 total** in the two test lanes.
Active green count: Utinni.Cli.Tests = 25 / UtinniCoreDotNet.Tests new TRE tests = 10.

REVIEWS HIGH-6 envelope-shape regression guard present in `Run_AnyHappyPath_EnvelopeHasTopLevelSchemaVersionAndCommand`:
- `root["schemaVersion"] == 1` (root-level)
- `root["command"] == "parse-tre"` (root-level)
- `root["result"]["schemaVersion"] == null` (not nested)
- `root["result"]["command"] == null` (not nested)

## BuildResult CONTRACT-LOCKED Field Names (REVIEWS HIGH-7)

```csharp
header.recordCount        // NOT resourceCount
records[].id              // "tre:<ordinal>" — stable identifier
records[].compressionKind // "none"|"deflate" enum string, NOT int
records[].compressedSize  // NOT dataCompressedSize
records[].uncompressedSize// NOT dataSize
records[].offset          // NOT dataOffset
objects[].id              // = templateName string (list-objects)
objects[].templateName    // duplicate for dual-field contract
```

## list-objects Architectural Debt Note (REVIEWS MEDIUM-13)

`ListObjectsCommand.Run` uses a byte-level scan for the `OBJS` 4-byte sentinel. This is provisional — documented inline as:
`// REVIEWS MEDIUM-13: byte-scan is provisional architectural debt; Phase 6+ refactor on top of Plan 04-03's IffReader`

Phase 6+ refactor: walk `IffDocument.AllNodesInPreorder` filtering `IffLeafChunk` instances with `TypeId == "OBJS"`.

## CON-O-09 Closed

`docs/ai/assessment.md` §"Open questions" item 9 now carries the CON-O-09 resolution paragraph:
> Fixture storage = **in-repo synthesized + tiny real samples, no LFS**. ...

## D-01 Clean-Room Confirmation

All 4 files under `UtinniCoreDotNet/Formats/Tre/` carry the D-01 disposition addendum:
```
// Format understood by reading swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}
// (SOE/Bootprint, All Rights Reserved). No code, comments, identifier names, or test fixtures copied
// from any reference source. Implementation original to Utinni under MIT.
```

No identifiers, variable names, or comment text was copied from `TreeFile.h`/`TreeFile.cpp`.

## REVIEWS Revisions Applied

| Finding | Applied |
|---------|---------|
| HIGH-4 (TreFile eager-read) | `Open(Stream)` caches `byte[][]`; `GetRecordData` works post-dispose; Tier-1 regression guard test |
| HIGH-6 (envelope shape) | `EmitSuccess`/`EmitError` wrap at ROOT level; no nested schemaVersion/command; Tier-2 regression guard test |
| HIGH-7 (field-name drift) | `recordCount`, `compressionKind`, `compressedSize`, `offset`, `uncompressedSize`, `id` locked |
| MEDIUM-13 (list-objects debt) | Inline source comment pointing to Phase 6+ IffReader refactor |
| LOW-20 (v0006 coverage) | Dedicated `synthesized-2record-v0006.tre` fixture + Tier-2 golden row |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] T-04-DoS test expected DeflateExpansionExceedsCap but dataSize=1GB was rejected at Open-time**
- **Found during:** Task 2.1 test run (Test 7)
- **Issue:** `ValidateLength(dataSize, MaxBlockSize, ...)` fired during `Open` for dataSize=1_000_000_000 before `GetRecordData` could check. Plan spec says `Open` succeeds but `GetRecordData` throws `DeflateExpansionExceedsCap`.
- **Fix:** Removed dataSize cap check from `Open`. Added explicit `if (rec.UncompressedSize > MaxBlockSize)` guard at the start of `GetRecordData` to throw `DeflateExpansionExceedsCap`. T-04-DoS is still mitigated: the memory cost of caching compressed bytes is bounded by `CompressedSize` (validated at Open), not the claimed uncompressed size.
- **Files modified:** TreFile.cs
- **Commit:** e02813b

**2. [Rule 1 - Bug] MaskPath in golden tests did not account for JSON-escaped backslashes**
- **Found during:** Task 2.4 test run
- **Issue:** `result.Stdout.Replace(fixturePath, "<fixture-path>")` didn't find the string because the JSON output escapes Windows backslashes as `\\` while `fixturePath` has single `\`.
- **Fix:** `MaskPath` now replaces both the JSON-escaped form (`path.Replace("\\", "\\\\")`) and the raw path.
- **Files modified:** ParseTreCommandTests.cs, ListObjectsCommandTests.cs
- **Commit:** 2b45c8f

**3. [Rule 1 - Bug] CommandDispatchTests Theory rows for parse-tre/list-objects now break (implemented)**
- **Found during:** Task 2.4 full suite run
- **Issue:** `Run_WithVerbStub_ExitsOneAndEmitsNotImplementedJson` Theory had `[InlineData("parse-tre", ...)]` and `[InlineData("list-objects", ...)]` which now exit 3 (FileNotFound) instead of 1 (NotImplemented).
- **Fix:** Removed the two implemented-command InlineData rows. Only `inspect-iff` and `validate-plugin` remain as valid stub test rows.
- **Files modified:** CommandDispatchTests.cs
- **Commit:** 2b45c8f

## Known Stubs

| Stub | File | Reason |
|------|------|--------|
| `InspectIffCommand.Run` returns NotImplemented | `Utinni.Cli/Commands/InspectIffCommand.cs` | Plan 04-03 implements IFF inspection |
| `ValidatePluginCommand.Run` returns NotImplemented | `Utinni.Cli/Commands/ValidatePluginCommand.cs` | Plan 04-04 implements plugin validation |

## Threat Flags

None. Plan 04-02 adds file-read CLI commands. The `source` field in JSON output exposes the operator-supplied path verbatim (T-04-V12 disposition: sandboxing is Phase 6+; the `--help` for parse-tre mentions 'reads the given file with operator privileges').

## Self-Check: PASSED
