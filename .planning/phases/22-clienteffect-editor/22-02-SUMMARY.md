---
phase: 22-clienteffect-editor
plan: 02
subsystem: cli
tags: [clef, clienteffect, cli, verbs, mcp, stableid, loose-override, mutation-mode-verify, net472]

# Dependency graph
requires:
  - phase: 22-clienteffect-editor
    plan: 01
    provides: ClientEffectDocument.FromBytes/FromIff + MutableClientEffect (add/remove/reorder/edit by stableId) + ClefFieldCodec encoders + ClientEffectParseException
  - phase: 20-terrain-codec
    provides: DecodeIffCommand BuildXxxResult envelope shape + the apply-save-* / LooseOverridePath two-step loose-subdir convention (cloned)
provides:
  - "decode-effect / roundtrip-effect / apply-save-effect utinni-cli verbs (verbs-first surface, DEC-V2-VERBS-FIRST)"
  - "decode-iff FORM CLEF auto-dispatch branch (BuildClefResult) — gives the MCP decode_iff tool CLEF routing for free"
  - "Per-command stableId in the decode envelope (the ordinal-path id; --leaf addressing + editor contract, REVIEWS HIGH #1)"
  - "apply-save-effect: own --loose-subdir default loose + mutation-mode structural verify + canonical-payload + locked add-defaults (REVIEWS HIGH #2/#3, MEDIUM #5)"
  - "ClefCommandDefaults: the single-source LOCKED add-command default values shared by CLI / fixtures / Plan-04 Form"
  - "summarize_clienteffect MCP read tool (shells decode-iff, ZERO format logic — DEC-V2-MCP-OOP)"
affects: [22-04 EffectsSubPanel (reuses ClefCommandDefaults + the apply-save save path)]

# Tech tracking
tech-stack:
  added: []  # pure-managed composition of in-repo assemblies; ZERO external packages
  patterns:
    - "Alias-delegation CLI verb (decode-effect -> DecodeIffCommand.BuildClefResult, cf. decode-trn)"
    - "Mutation-mode structural verify split (field-edit by stableId; reorder/add/remove by CONTENT) — the ordinal stableId shifts under reorder/add"
    - "Field-edit by whole-payload re-encode from CURRENT decoded values + one substituted field (CLEF payloads are variable-length; no single-field editor)"
    - "Two-step LooseOverridePath.Resolve(root, looseSubDir) then Resolve(base, relAsset) — <root>/loose/<relAsset>, both legs fail-closed"

key-files:
  created:
    - Utinni.Cli/Commands/DecodeEffectCommand.cs
    - Utinni.Cli/Commands/RoundtripEffectCommand.cs
    - Utinni.Cli/Commands/ApplySaveEffectCommand.cs
    - UtinniCoreDotNet/Formats/ClientEffect/ClefCommandDefaults.cs
    - Utinni.Cli.Tests/ClientEffect/ClefTestFixtures.cs
    - Utinni.Cli.Tests/ClientEffect/DecodeEffectTests.cs
    - Utinni.Cli.Tests/ClientEffect/RoundtripEffectCommandTests.cs
    - Utinni.Cli.Tests/ClientEffect/ApplySaveEffectCommandTests.cs
    - Utinni.Cli.Tests/ClientEffect/ClefLooseOverrideTests.cs
    - Utinni.Cli.Tests/ClientEffect/CliHelpEnumeratesEffectVerbsTests.cs
  modified:
    - Utinni.Cli/Commands/DecodeIffCommand.cs (CLEF branch + BuildClefResult + ClientEffectParseException catch rung)
    - Utinni.Cli/Program.cs (3 effect verbs wired into Type[] + Dispatch)
    - Utinni.Mcp/Tools/ReadTools.cs (summarize_clienteffect read tool)
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj (register ClefCommandDefaults.cs)
    - Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt (regen for 3 new verbs)
    - Utinni.Cli.Tests/Fixtures/dispatch/no-args.expected.txt (regen for 3 new verbs)

key-decisions:
  - "Field edit re-encodes the WHOLE command payload from the command's CURRENT decoded values + one substituted field (the codec exposes only whole-payload encoders; CLEF payloads are variable-length so there is no single-field span to overwrite)"
  - "Verify SPLIT BY MUTATION MODE: field-edit compares untouched leaves by ordinal stableId (stable under edit); reorder/add/remove compare by CONTENT multiset / presence / byte-identity (the ordinal id deliberately shifts) — REVIEWS HIGH #2"
  - "ClefCommandDefaults lives in the Plan-01 codec namespace (UtinniCoreDotNet.Formats.ClientEffect) so Plan 04's Form + the test fixtures reuse the SAME locked constants — editor/CLI parity (REVIEWS MEDIUM #5)"
  - "Production code folded into the Task-1 commit (incl. apply-save-effect); the Task-2 commit is tests-only — because Program.cs wiring references all three options types, the tree must compile at every commit"

patterns-established:
  - "When a new verb changes utinni-cli --help, the committed dispatch help/no-args goldens MUST be regenerated in the same plan (Rule 3 — additions only)"

requirements-completed: [PROD-W2-CFX-02]

# Metrics
duration: ~55min
completed: 2026-06-17
---

# Phase 22 Plan 02: ClientEffect Verbs + MCP Read Tool Summary

**The Plan-01 CLEF codec exposed verbs-first: `decode-effect` / `roundtrip-effect` / `apply-save-effect` + a `decode-iff` FORM CLEF auto-dispatch branch emitting a per-command `stableId`, a thin `summarize_clienteffect` MCP read tool (zero format logic), and an `apply-save-effect` that applies length-changing field edits + add/remove/reorder with a mutation-mode structural verify, canonical-payload assertions, locked add-defaults, and fail-closed `<root>/loose` containment — proven by 25 green ClientEffect tests.**

## Performance

- **Duration:** ~55 min
- **Completed:** 2026-06-17
- **Tasks:** 2 of 2
- **Files created/modified:** 16 (4 production + 1 codec-namespace default + 6 test files + 5 modified)

## Accomplishments
- Landed the three `effect-*` verbs (DEC-V2-VERBS-FIRST). `decode-effect` is a thin alias delegating to a new `DecodeIffCommand.BuildClefResult`; the `decode-iff` CLEF branch + the `ClientEffectParseException` catch rung give the existing MCP `decode_iff` tool CLEF routing for free.
- `BuildClefResult` surfaces a per-command `stableId` (sourced from `ClientEffectCommand.StableId`, the `DeriveStableId` ordinal-path) so `--leaf` addressing and the editor have a real contract (REVIEWS HIGH #1).
- `apply-save-effect` owns its `--loose-subdir` default `"loose"` via the SAME two-step `LooseOverridePath.Resolve` as `apply-save-trn` (the convention is self-contained in THIS plan — REVIEWS HIGH #3), removes the fixed-length guard (a length change is the point — D-01), and verifies STRUCTURALLY BY MUTATION MODE (field-edit by stableId + canonical-payload; reorder/add/remove by content — REVIEWS HIGH #2) with locked add-defaults (REVIEWS MEDIUM #5) and per-version D-03 proven through the CLI.
- `summarize_clienteffect` MCP read tool shells `utinni-cli decode-iff` with ZERO format logic (MCP-OOP lock).

## Task Commits

1. **Task 1: effect-* verbs + decode-iff CLEF branch + Program wiring + MCP tool** (incl. the apply-save-effect command + ClefCommandDefaults, folded in so the tree compiles at this commit) — `9d734a7` (feat)
2. **Task 2: the verb test suite + regenerated dispatch goldens** — `494dd33` (test)

## Decisions Made
See `key-decisions` frontmatter. The two load-bearing ones: (1) the field edit re-encodes the whole command payload from the command's current decoded values (the codec has no single-field editor because CLEF payloads are variable-length), and (2) the verify splits by mutation mode because the ordinal-based stableId is stable under a field edit but deliberately shifts under reorder/add — a single "untouched-by-stableId" compare would false-fail (reorder/add) or false-pass.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Regenerate the dispatch help / no-args goldens for the three new verbs**
- **Found during:** Task 2 (full-suite run after the ClientEffect filter passed).
- **Issue:** Adding the three `effect-*` verbs changed `utinni-cli --help` (and the no-args usage), which have committed text goldens (`Fixtures/dispatch/{help,no-args}.expected.txt`); `CommandDispatchTests` failed on the golden mismatch. Directly caused by this plan's Program.cs wiring (in scope).
- **Fix:** Copied the test runner's dumped actual output over the two source goldens (additions only — the three new verb lines), rebuilt, re-ran: full suite green.
- **Files modified:** `Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt`, `Utinni.Cli.Tests/Fixtures/dispatch/no-args.expected.txt`
- **Commit:** `494dd33`

### Task/commit split note
Program.cs wires all three effect-options types (per the plan's Task 1), but `ApplySaveEffectOptions`/`ApplySaveEffectCommand` are Task 2 artifacts. To keep the tree buildable at every commit, the apply-save-effect command + `ClefCommandDefaults` were folded into the Task-1 commit; the Task-2 commit is tests-only. (Same compile-ordering reality Plan 01 recorded.)

## Verification
- MSBuild of `Utinni.Cli` + `Utinni.Mcp` (Release/x86): clean (only pre-existing Generated/UtinniCore.cs CS0108 warnings).
- MSBuild of `Utinni.Cli.Tests` (Release/x86): clean.
- `dotnet test Utinni.Cli.Tests --no-build --filter "FullyQualifiedName~ClientEffect"`: **25 passed, 0 failed**.
- Full `Utinni.Cli.Tests` suite: **439 passed, 0 failed, 2 skipped** (env-gated real-asset tests).
- Acceptance grep gates: `newPayload.Length != originalPayload.Length` guard = 0; `loose-subdir` in ApplySaveEffectCommand = 3; `LooseOverridePath.Resolve` = 5 (two-step); generic `Exception` catch in the three new commands = 0; MCP `ClientEffectDocument|IffReader|ClefFieldCodec` refs = 0; `stableId` in DecodeIffCommand CLEF builder ≥ 1; `summarize_clienteffect` = 1.
- `utinni-cli --help` enumerates `decode-effect`, `roundtrip-effect`, `apply-save-effect` (CliHelpEnumerates test).
- `git status` shows `Generated/UtinniCore.cs` unmodified (CppSharp churn `git checkout --`'d after each build).

## Known Stubs
None — every verb is fully wired to the Plan-01 codec; no placeholder data. (The TJT EffectsSubPanel / Form is out of this plan's scope — Plan 04.)

## Threat Flags
None new. The threat register's `mitigate` dispositions are all covered: T-22-path (two-step LooseOverridePath, both legs fail-closed — `ClefLooseOverrideTests` traversal-reject on either leg); T-22-misaddress (mutation-mode verify + canonical-payload + version-unchanged); T-22-halfunderstood (raw/non-editable leaf rejected exit 1 before any write); T-22-malformed (typed catch ladder, generic Exception NOT caught); T-22-mcp-escape (ResolvedRoot.Resolve before subprocess, zero in-proc format logic).

## Self-Check: PASSED
