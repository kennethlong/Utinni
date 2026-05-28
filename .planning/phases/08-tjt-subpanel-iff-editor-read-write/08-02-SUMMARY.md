---
phase: 08-tjt-subpanel-iff-editor-read-write
plan: 02
subsystem: cli-iff-roundtrip-harness
tags: [cli, iff, max-harness, golden-tests, mutation-golden, structural-removal-golden, byte-exact-roundtrip, csharp, net472]
requires:
  - "UtinniCoreDotNet.Formats.Iff.IffReader (existing)"
  - "UtinniCoreDotNet.Formats.Iff.IffDocument / IffChunk / IffContainerChunk / IffLeafChunk (existing)"
  - "UtinniCoreDotNet.Formats.Iff.MutableIffDocument / MutableIffNode (from 08-01)"
  - "UtinniCoreDotNet.Formats.Iff.IffWriter (from 08-01)"
  - "MutableIffDocument.RemoveByStableId(string) (from 08-01 — round-3 R3-M3 closure)"
  - "Utinni.Cli.Output.JsonOutput (existing)"
  - "Utinni.Cli.Tests.Infrastructure.{InProcessCliRunner, FixturePath, GoldenTestRunner} (existing)"
provides:
  - "roundtrip-iff CLI verb — the D-02 max-harness for the IFF write path"
  - "Four golden round-trip fixtures: synthetic-nested, odd-chunk-no-pad, mutation-leaf-edit, mutation-leaf-removed"
  - "Eleven xUnit tests in Utinni.Cli.Tests gating PROD-W1-IFF Success Criterion 4"
  - "Automated regression gate against the SWG no-pad quirk (Pitfall 1)"
  - "Automated regression gate against pad-byte corruption on dirty-leaf reserialization"
  - "Automated regression gate against ancestor-invalidation failure on structural removal (round-2 MEDIUM 8 / cursor N-M4 closure)"
affects:
  - "Utinni.Cli/Program.cs (one ParseArguments type + one MapResult lambda added)"
  - "Utinni.Cli.Tests/Fixtures/dispatch/{help,no-args}.expected.txt (new verb listing — Rule-3 follow-on)"
tech-stack:
  added: []
  patterns:
    - "Mirror-existing-verb shape: copy InspectIffCommand's [Verb]/Options/Run skeleton; preserve exit-code contract (FileNotFound->3, IffParseException/IOException->2, usage->1; generic Exception NOT caught)"
    - "Stable-id round-trip lookup: address leaves by IffChunk.Id (FORM:WSNP/0/DATA:DATA/0 path format) across both the original and the re-parsed rewritten document so untouched-leaf identity can be asserted without depending on byte offsets"
    - "Sibling-ordinal-shift fix-up for --remove-leaf: leaves whose stable id starts with the affected parent's id-prefix and whose first-segment ordinal exceeds the removed ordinal have their ids rewritten with ordinal-1 before lookup in the rewritten document (the golden fixture sidesteps this by removing the LAST sibling, but the implementation handles both)"
    - "Hex-string -> bytes parser local to the verb (no separator chars, even length, 0-9/a-f/A-F only) to keep --mutate-hex usage simple at the CLI surface"
    - "Test goldens use the <fixture-path> sentinel (matches the InspectIffCommandTests masking convention so the goldens are machine-independent)"
key-files:
  created:
    - "Utinni.Cli/Commands/RoundtripIffCommand.cs"
    - "Utinni.Cli.Tests/Commands/RoundtripIffCommandTests.cs"
    - "Utinni.Cli.Tests/Fixtures/iff/roundtrip/synthetic-nested.iff"
    - "Utinni.Cli.Tests/Fixtures/iff/roundtrip/synthetic-nested.expected.json"
    - "Utinni.Cli.Tests/Fixtures/iff/roundtrip/odd-chunk-no-pad.iff"
    - "Utinni.Cli.Tests/Fixtures/iff/roundtrip/odd-chunk-no-pad.expected.json"
    - "Utinni.Cli.Tests/Fixtures/iff/roundtrip/mutation-leaf-edit.iff"
    - "Utinni.Cli.Tests/Fixtures/iff/roundtrip/mutation-leaf-edit.expected.json"
    - "Utinni.Cli.Tests/Fixtures/iff/roundtrip/mutation-leaf-removed.iff"
    - "Utinni.Cli.Tests/Fixtures/iff/roundtrip/mutation-leaf-removed.expected.json"
  modified:
    - "Utinni.Cli/Program.cs (verb dispatch wiring; one type added to ParseArguments<>, one lambda added to MapResult, alphabetically positioned between decode-iff and validate-plugin)"
    - "Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt (Rule-3 follow-on: CommandLineParser's --help text now lists roundtrip-iff)"
    - "Utinni.Cli.Tests/Fixtures/dispatch/no-args.expected.txt (Rule-3 follow-on: the no-args ERROR(S) listing now lists roundtrip-iff)"
decisions:
  - "Fixtures use EVEN-LENGTH payloads everywhere (no pads anywhere inside any of the four IFF files) so the captured-slice round-trip and the dirty-leaf re-serialization both produce byte-identical output without any pad-handling subtlety to debug. The odd-chunk-no-pad fixture is the SOLE exception — by design, to gate against the SWG no-pad regression (Pitfall 1)."
  - "mutation-leaf-removed.iff removes the LAST sibling under FORM:OBJS (EXTR:EXTR/2) so the surviving siblings keep their ordinals. The implementation does handle the sibling-ordinal-shift case for non-last removals (the ordinal fix-up logic is in RunRemove), but the golden documents the pure structural-removal case for clarity."
  - "The dispatch help.expected.txt and no-args.expected.txt updates are a Rule-3 deviation (a CommandLineParser-driven blocking issue — adding a new [Verb] necessarily changes the help text). Both goldens updated in the Task 2 commit so the suite stays green."
  - "Both csproj files (Utinni.Cli.csproj + Utinni.Cli.Tests.csproj) are SDK-style with default **/*.cs Compile glob and an existing <Content Include='Fixtures\\**\\*' CopyToOutputDirectory='PreserveNewest'> entry. The new verb file, the new test file, and the eight fixture files (4 .iff + 4 .expected.json) all auto-include — no csproj edits were made. Verified via `git status --porcelain` showing zero csproj diffs."
  - "Hex-replacement payload for the mutation-leaf-edit golden was chosen to be the SAME length as the original NAME leaf's payload (4 bytes, 'DEADBEEF') so the WSNP container's roll-up length is unchanged and the output total file size stays 72 bytes — keeps the byteExact/rewrittenLength assertions clean."
metrics:
  duration_minutes: 75
  completed_date: "2026-05-28"
---

# Phase 8 Plan 2: Roundtrip-IFF CLI Verb + Golden Fixtures Summary

One-liner: D-02 max-harness `roundtrip-iff` CLI verb + four golden round-trip fixtures (identity, odd-length-no-pad, one-leaf payload mutation, one-leaf structural removal) — the automated CI gate for PROD-W1-IFF Success Criterion 4, exercising the same 08-01 framework write path the TJT editor will use.

## What Shipped

**`Utinni.Cli/Commands/RoundtripIffCommand.cs`** — a new `[Verb("roundtrip-iff", ...)]` command that:

1. **Parses** the input IFF via `IffReader.Read`.
2. **Builds the hybrid mutable DOM** via `MutableIffDocument.FromDocument(doc, originalBytes)`.
3. **Branches by option** into one of three modes:
   - **No mutation** — straight `IffWriter.Write(mutable)` → `SequenceEqual(original, rewritten)` → reports `byteExact, originalLength, rewrittenLength, source`.
   - **`--mutate-leaf <stable-id> --mutate-hex <hex>`** — locates the leaf by stable id, applies `SetPayload(parseHex(hex))`, serializes, re-parses the rewritten bytes, walks every UNTOUCHED leaf in the re-parsed tree (stable id ≠ mutated id), and asserts its `TypeId` + `Data` match the original. Reports `byteExact:false, mutatedLeafId, untouchedLeafRangesIdentical, untouchedLeafCount`.
   - **`--remove-leaf <stable-id>`** (round-2 MEDIUM 8 / cursor N-M4 closure) — captures the target leaf's `TypeId`, the parent container's child count, and the removed ordinal; invokes `MutableIffDocument.RemoveByStableId`; serializes; re-parses; locates the affected parent container in the re-parsed tree and reads its new child count; walks every UNTOUCHED original leaf, fixing up the stable id of any sibling whose ordinal lay AFTER the removed ordinal (ordinal -= 1) and asserts the rewritten counterpart has identical `TypeId` + `Data`. Reports `byteExact:false, byteExactExceptRemovedLeaf, removedLeafId, removedLeafTypeId, parentContainerChildCountBefore, parentContainerChildCountAfter, untouchedLeafCount`.
4. **Emits via `JsonOutput.EmitSuccess`** — result object never pre-adds `schemaVersion`/`command` (those go at root per REVIEWS HIGH-6); keys auto-sorted via `JsonOutput.SortJObjectKeys`.
5. **Exit-code contract** (mirroring `InspectIffCommand`):
   - `FileNotFound` → 3
   - `IffParseException` → 2 (with `Kind.ToString()` as the error kind)
   - `IOException` → 2 with kind "IoError"
   - usage errors (mutually exclusive flags, missing `--mutate-hex`, unknown leaf id, non-hex string) → 1 with kind "UsageError"
   - generic `Exception` is intentionally NOT caught (bubbles to top level for diagnosis).

**`Utinni.Cli/Program.cs`** — one type added to `ParseArguments<…>` (`Commands.RoundtripIffOptions`) and one lambda to `MapResult` (`(Commands.RoundtripIffOptions o) => Commands.RoundtripIffCommand.Run(o)`), alphabetically positioned between `decode-iff` and `validate-plugin`.

**Four golden round-trip fixtures** in `Utinni.Cli.Tests/Fixtures/iff/roundtrip/`:

| Fixture | Bytes | Purpose | Expected envelope highlights |
|---|---|---|---|
| `synthetic-nested.{iff,expected.json}` | 72 | No-mutation round-trip on a nested FORM with four even-length leaves under FORM:WSNP and FORM:OBJS — establishes the byte-exact baseline | `byteExact:true, originalLength:72, rewrittenLength:72` |
| `odd-chunk-no-pad.{iff,expected.json}` | 27 | No-mutation round-trip on an odd-length-payload (7 bytes) leaf with NO trailing pad byte — guards the SWG no-pad quirk (Pitfall 1) | `byteExact:true, originalLength:27, rewrittenLength:27` |
| `mutation-leaf-edit.{iff,expected.json}` | 72 | One-leaf payload edit (`FORM:WSNP/0/NAME:NAME/1` ← `DEADBEEF`, same length) — proves Criterion 4 across the PAYLOAD-EDIT path (closes 08-REVIEWS round-1 mutation-golden gap) | `byteExact:false, untouchedLeafRangesIdentical:true, untouchedLeafCount:3 (= N−1)` |
| `mutation-leaf-removed.{iff,expected.json}` | 72 → 60 after removal | One-leaf STRUCTURAL removal (`FORM:WSNP/0/FORM:OBJS/1/EXTR:EXTR/2`, the last sibling) — proves Criterion 4 across the STRUCTURAL-OP path (closes 08-REVIEWS round-2 MEDIUM 8 / cursor N-M4 gap) | `byteExact:false, byteExactExceptRemovedLeaf:true, parentContainerChildCountBefore:3, parentContainerChildCountAfter:2, removedLeafTypeId:"EXTR", untouchedLeafCount:3 (= N−1)` |

**`Utinni.Cli.Tests/Commands/RoundtripIffCommandTests.cs`** — eleven xUnit tests covering:

- Two `[Theory]` cases for no-mutation round-trip (synthetic-nested + odd-chunk-no-pad)
- One `[Fact]` for the one-leaf payload mutation (`--mutate-leaf`)
- One `[Fact]` for the one-leaf structural removal (`--remove-leaf`)
- One `[Fact]` for `FileNotFound` (exit 3)
- Three `[Fact]`s for usage-error paths (mutually exclusive flags / missing `--mutate-hex` / unknown leaf id — all exit 1)
- One `[Fact]` for REVIEWS HIGH-6 envelope-shape regression guard (`schemaVersion` + `command` at root, not inside `result`)
- (`[Theory]` counts as one `[Fact]`-equivalent containing two `InlineData` cases, plus four individual `[Fact]`s focused on the mutation/removal/error paths)

All eleven tests pass. Full Utinni.Cli.Tests suite: 123 passing, 1 skipped (pre-existing `SWG_SAMPLE_TRE_DIR` env-gated test), 0 failing.

## Deviations from Plan

### Auto-fixed / inline deviations

**1. [Rule 3 — Blocking issue] Updated `dispatch/{help,no-args}.expected.txt` to include the new verb**

- **Found during:** Task 2 acceptance, full-suite test run
- **Issue:** `CommandLineParser` auto-generates the help text by iterating every `[Verb]` discovered via `ParseArguments<…>`. Adding the new `Commands.RoundtripIffOptions` type necessarily injects a new block into the `--help` and no-args ERROR(S) output. The existing `dispatch/help.expected.txt` and `dispatch/no-args.expected.txt` goldens did not contain this block, so both `CommandDispatchTests.Run_WithHelpFlag_ExitsOneAndMatchesHelpGolden` and `Run_WithNoArgs_ExitsOneAndMatchesNoArgsGolden` failed after Task 1.
- **Fix:** Added the verbatim `roundtrip-iff      Parse -> [optional mutate-leaf | remove-leaf] -> serialize -> re-parse an IFF; assert byte-exact untouched chunks.` block (with CommandLineParser's whitespace-and-indent formatting) to both goldens, between the existing `decode-iff` and `validate-plugin` entries.
- **Files modified:** `Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt`, `Utinni.Cli.Tests/Fixtures/dispatch/no-args.expected.txt`.
- **Commit:** `9e1d990` (Task 2 commit; folded in alongside the test goldens).
- **Why this was Rule 3 and not Rule 2:** the failure was a blocking issue (Task 2 verification couldn't go green without it), and the only correct fix is to update the dispatch goldens to match the new verb listing — there is no behavioural code change to make.

### Pre-existing unstaged change (intentionally not committed)

The session opened with `UtinniCoreDotNet/Generated/UtinniCore.cs` in a modified state (11348-line reordering diff from a prior CppSharp regen). It was NOT staged or committed by either of this plan's two commits, and remains in the working tree as it was. Out of scope for 08-02.

### No Rule 4 architectural deviations

The plan executed exactly as written across both tasks. The acceptance criteria called out a `roundtrip-iff` verb with three modes; that is what shipped, byte-for-byte against the spec.

## Threat Surface Verification

All threat-model dispositions from the plan's `<threat_model>` are met:

| Threat ID | Disposition | Status |
|-----------|-------------|--------|
| T-08-04 | DoS on a crafted/huge IFF — reuse `IffReader` bounds checks + 64 MB cap; `IffParseException` → exit 2 | Met — verified via `IffParseException` catch handler at `RoundtripIffCommand.Run`; existing reader tests already cover the bounds paths |
| T-08-05 | Absolute path leak into golden JSON | Met — `MaskPath` applied in every test; goldens carry `<fixture-path>` sentinel |
| T-08-SC | npm/pip/cargo installs | N/A — no external packages added |

## Cross-AI Review Concerns Addressed (carried forward from prior rounds via the plan spec)

| Round-1 / Round-2 ID | Severity | Disposition |
|---|---|---|
| Round-1 mutation-golden gap (08-REVIEWS folded-in) | MEDIUM | RESOLVED — `mutation-leaf-edit.{iff,expected.json}` ships with `untouchedLeafRangesIdentical:true, untouchedLeafCount:3` |
| Round-2 MEDIUM 8 / cursor N-M4 — structural-op golden | MEDIUM | RESOLVED — `mutation-leaf-removed.{iff,expected.json}` ships with `byteExactExceptRemovedLeaf:true, parentContainerChildCountAfter == parentContainerChildCountBefore - 1, untouchedLeafCount:3` |
| HIGH-A — csproj coverage for new files | HIGH | RESOLVED at plan-write time — both CLI csproj are SDK-style; the assumes block was correct; no csproj edits needed and `git status --porcelain` confirms both .csproj files unchanged |

The structural-removal golden closes round-2 MEDIUM 8 / cursor N-M4: Criterion 4 is now gated across BOTH payload-edit (mutation-leaf-edit) AND structural removal (mutation-leaf-removed), not just the pure-identity case.

## Verification Evidence

**MSBuild** (VS2026 Dev18) of `Utinni.Cli` + `Utinni.Cli.Tests`: clean compile (only pre-existing CS0108 warnings in `Generated/UtinniCore.cs` and pre-existing xUnit2013 analyzer warnings — neither related to 08-02).

**Manual verb invocation** confirmed all four fixtures produce the expected envelopes from the binary itself (see commit message body of `94b8dbe`).

**xUnit run** (`dotnet test Utinni.Cli.Tests --no-build -c Release`):
- `--filter "FullyQualifiedName~Roundtrip"`: 11/11 passing in ~2 s.
- Full suite: 123 passing, 1 skipped (pre-existing env-gated test), 0 failing in ~4 s.

**Exit-code contract** spot-checked from the command line: missing file → 3 ✓; both `--mutate-leaf` and `--remove-leaf` → 1 ✓; `--mutate-leaf` without `--mutate-hex` → 1 ✓; unknown leaf id → 1 ✓.

**Grep gate** (per Task 1 `<verify>`): `grep -c "roundtrip-iff" Utinni.Cli/Commands/RoundtripIffCommand.cs` = 15 (well above the implicit `> 0` threshold).

**Csproj invariance**: `git status --porcelain Utinni.Cli/Utinni.Cli.csproj Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` → empty (no edits).

## Self-Check: PASSED

**Files verified present:**

- `Utinni.Cli/Commands/RoundtripIffCommand.cs` — FOUND
- `Utinni.Cli.Tests/Commands/RoundtripIffCommandTests.cs` — FOUND
- `Utinni.Cli.Tests/Fixtures/iff/roundtrip/synthetic-nested.iff` — FOUND
- `Utinni.Cli.Tests/Fixtures/iff/roundtrip/synthetic-nested.expected.json` — FOUND
- `Utinni.Cli.Tests/Fixtures/iff/roundtrip/odd-chunk-no-pad.iff` — FOUND
- `Utinni.Cli.Tests/Fixtures/iff/roundtrip/odd-chunk-no-pad.expected.json` — FOUND
- `Utinni.Cli.Tests/Fixtures/iff/roundtrip/mutation-leaf-edit.iff` — FOUND
- `Utinni.Cli.Tests/Fixtures/iff/roundtrip/mutation-leaf-edit.expected.json` — FOUND
- `Utinni.Cli.Tests/Fixtures/iff/roundtrip/mutation-leaf-removed.iff` — FOUND
- `Utinni.Cli.Tests/Fixtures/iff/roundtrip/mutation-leaf-removed.expected.json` — FOUND

**Commits verified present** (`git log --oneline` substring match):

- `94b8dbe` — `feat(08-02): roundtrip-iff CLI verb + structural --remove-leaf mode` — FOUND
- `9e1d990` — `test(08-02): roundtrip-iff golden fixtures + tests (identity, no-pad, payload-mutation, structural-removal)` — FOUND

**Test counts verified by execution:** Roundtrip-only 11/11; full Utinni.Cli.Tests 123/124 (1 skipped — pre-existing).
