---
phase: 04-tier-2-cli-shim-golden-fixtures
plan: 01
subsystem: cli-scaffold
tags: [cli, golden-fixtures, json-output, commandlineparser, xunit, ci]
dependency_graph:
  requires: [UtinniCoreDotNet]
  provides: [Utinni.Cli, Utinni.Cli.Tests, JsonOutput, CommandDispatch, GoldenHarness]
  affects: [Utinni.sln, .github/workflows/ci.yml, .gitattributes, docs/ai/assessment.md]
tech_stack:
  added: [CommandLineParser 2.9.1, Newtonsoft.Json 13.0.3 (Utinni.Cli)]
  patterns: [SDK-style csproj, in-process CLI runner, JToken.DeepEquals goldens, sorted-key JSON envelope]
key_files:
  created:
    - Utinni.Cli/Utinni.Cli.csproj
    - Utinni.Cli/Program.cs
    - Utinni.Cli/Output/JsonOutput.cs
    - Utinni.Cli/Output/SortedKeyContractResolver.cs
    - Utinni.Cli/Commands/ParseTreCommand.cs
    - Utinni.Cli/Commands/ListObjectsCommand.cs
    - Utinni.Cli/Commands/InspectIffCommand.cs
    - Utinni.Cli/Commands/ValidatePluginCommand.cs
    - Utinni.Cli.Tests/Utinni.Cli.Tests.csproj
    - Utinni.Cli.Tests/Properties/AssemblyInfo.cs
    - Utinni.Cli.Tests/Infrastructure/FixturePath.cs
    - Utinni.Cli.Tests/Infrastructure/InProcessCliRunner.cs
    - Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs
    - Utinni.Cli.Tests/Output/JsonOutputTests.cs
    - Utinni.Cli.Tests/Commands/CommandDispatchTests.cs
    - Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt
    - Utinni.Cli.Tests/Fixtures/dispatch/no-args.expected.txt
    - Utinni.Cli.Tests/Fixtures/dispatch/unknown-command.expected.txt
    - Utinni.Cli/packages.lock.json
    - Utinni.Cli.Tests/packages.lock.json
    - .gitattributes
  modified:
    - Utinni.sln
    - .github/workflows/ci.yml
    - docs/ai/assessment.md
decisions:
  - "Used new Parser(...) with HelpWriter=Console.Error resolved at call-time instead of Parser.Default singleton; prevents test stdout capture regression"
  - "Golden fixtures use version masking regex (X.Y.Z) so fixture re-baselining is not needed on package bumps or commits"
  - "In-process CLI banner shows CommandLine library metadata (not assembly metadata) when new Parser() is used; fixtures reflect this actual behavior"
  - "Added 4th [Fact] (Run_WithVerbAndMissingRequiredArg_ExitsOne) to reach 18-test target"
metrics:
  duration: "30 minutes"
  completed: "2026-05-23"
  tasks: 5
  files: 23
---

# Phase 4 Plan 01: Tier-2 CLI Shim + Golden Harness Scaffold Summary

**One-liner:** Net472/x86 `utinni-cli` exe + `Utinni.Cli.Tests` xUnit golden harness scaffolded with four verb stubs (all NotImplemented), LOCKED JSON envelope, sorted-key output, in-process runner, three dispatch goldens, and CI second test lane — 18 tests green.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1.1 | Scaffold Utinni.Cli + Utinni.Cli.Tests csproj pair, wire sln, stub Main, CON-O-11 disposition | f016494 | Utinni.Cli.csproj, Utinni.Cli.Tests.csproj, Program.cs (stub), AssemblyInfo.cs, Utinni.sln, docs/ai/assessment.md, packages.lock.json x2 |
| 1.2 | JsonOutput envelope helper + SortedKeyContractResolver + recursive JObject sort + 10 self-tests | 3537cdb | JsonOutput.cs, SortedKeyContractResolver.cs, JsonOutputTests.cs |
| 1.3 | Test infrastructure (FixturePath + InProcessCliRunner + GoldenTestRunner) and .gitattributes LF rule | b69131e | FixturePath.cs, InProcessCliRunner.cs, GoldenTestRunner.cs, .gitattributes |
| 1.4 | Real verb dispatch in Program.cs; four NotImplemented stub Commands/*.cs | 5f04f9a | Program.cs, ParseTreCommand.cs, ListObjectsCommand.cs, InspectIffCommand.cs, ValidatePluginCommand.cs |
| 1.5 | Dispatch goldens (help/no-args/unknown-cmd) + CommandDispatchTests + CI YAML extension | 3efd184 | CommandDispatchTests.cs, help.expected.txt, no-args.expected.txt, unknown-command.expected.txt, ci.yml |

## Solution File GUIDs

| Project | GUID | Use |
|---------|------|-----|
| Utinni.Cli | `{3C6A1162-DADD-4CBE-9ACC-ECE2B2030FF8}` | Plans 04-02/03/04 may reference for cross-project work |
| Utinni.Cli.Tests | `{43082BFF-53F1-4CB6-837E-7ACA8468C12D}` | Plans 04-02/03/04 may reference |

## csproj PackageReference Versions Confirmed

- `CommandLineParser 2.9.1` (Utinni.Cli only)
- `Newtonsoft.Json 13.0.3` (both Utinni.Cli and Utinni.Cli.Tests)
- `Microsoft.NET.Test.Sdk 17.13.0` (Utinni.Cli.Tests)
- `xunit 2.9.3` (Utinni.Cli.Tests)
- `xunit.runner.visualstudio 3.1.5` (Utinni.Cli.Tests)

Lock file SHAs committed at: `Utinni.Cli/packages.lock.json` + `Utinni.Cli.Tests/packages.lock.json` (commit f016494).

## Dispatch Golden File Contents

### help.expected.txt
```
CommandLine <version>
Copyright (c) 2005 - 2020 Giacomo Stelluti Scala & Contributors

  parse-tre          Parse a .tre archive and emit sorted-key JSON to stdout.

  list-objects       List world-snapshot objects from a ws.iff via the TRE
                     reader.

  inspect-iff        Emit the chunk tree of an IFF file as JSON.

  validate-plugin    Reflect on a plugin directory and report compliance.
                     WARNING: loads each .dll under the given directory; only
                     run against trusted plugin directories.

  help               Display more information on a specific command.

  version            Display version information.
```

Note: In-process runner (via `new Parser(...)`) shows CommandLine library metadata rather than the exe's assembly metadata. The `<version>` sentinel masks `X.Y.Z` patterns so fixtures don't need re-baselining on package bumps.

### no-args.expected.txt
Same banner + `ERROR(S): No verb selected.` + full verb list.

### unknown-command.expected.txt
Same banner + `ERROR(S): Verb 'totally-unknown-verb' is not recognized.` + `--help/--version` options.

## CI Workflow Diff Summary

File: `.github/workflows/ci.yml`
- Lines added: ~16 (two new steps after the existing `Upload test results (on failure)` block)
- New step 1: `Run CLI golden tests (net472 / x86)` — project-targeted `dotnet test Utinni.Cli.Tests/...`
- New step 2: `Upload CLI test artifacts (on failure)` — artifact name `cli-test-results`; paths: TRX + `TestResults/**/*.json` + `TestResults/**/*.txt`

## docs/ai/assessment.md CON-O-11

CON-O-11 resolution paragraph added at the end of §"Open questions for project history". Exact text follows the plan prescription (public artifact disposition, stable JSON contract, modder-facing surface). CON-O-09 remains open — Plan 04-02 Task 2.5 closes it.

## Test Count: 18

| Class | Tests |
|-------|-------|
| JsonOutputTests | 10 [Fact] |
| CommandDispatchTests | 4 [Fact] + 1 [Theory]/4 InlineData rows = 8 executions |
| **Total** | **18** |

## Wave-0 Checklist from 04-VALIDATION.md

- [x] Utinni.Cli project builds under Release|x86 — Task 1.1 (f016494)
- [x] Utinni.Cli.Tests project builds under Release|x86 — Task 1.1 (f016494)
- [x] JsonOutput.EmitSuccess/EmitError emit LOCKED envelope shape — Task 1.2 (3537cdb)
- [x] [assembly: CollectionBehavior(DisableTestParallelization = true)] — Task 1.1 (f016494)
- [x] FixturePath/InProcessCliRunner/GoldenTestRunner compile — Task 1.3 (b69131e)
- [x] .gitattributes LF rules for *.expected.json + *.expected.txt — Task 1.3 (b69131e)
- [x] Four verb stubs compile and exit 1 with NotImplemented JSON — Task 1.4 (5f04f9a)
- [x] Three dispatch goldens (help/no-args/unknown-cmd) pass — Task 1.5 (3efd184)
- [x] CI YAML extends with second test lane — Task 1.5 (3efd184)
- [x] CON-O-11 disposition in docs/ai/assessment.md — Task 1.1 (f016494)
- [x] 18 tests total green — Task 1.5 (3efd184)

## REVIEWS Revisions Applied

| Finding | Action |
|---------|--------|
| HIGH-2: CS5001 / verify-done contradiction | Task 1.1 lands stub `Main`; Task 1.4 replaces body |
| HIGH-3: JsonOutput must write through Console.Out | `writer ?? System.Console.Out` in WriteEnvelope; verified by Test 3a/3b |
| HIGH-6: schemaVersion + command at envelope ROOT | Envelope construction uses top-level keys; Test 1 + Test 7 assert root-level invariants; CommandDispatchTests Theory also asserts |
| LOW-18: DisableTestParallelization | [assembly: CollectionBehavior(...)] in Properties/AssemblyInfo.cs |
| LOW-19: PowerShell-native verify snippets | Not applicable to executor (plan-phase planning artifact only) |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Parser.Default singleton caches Console.Error before InProcessCliRunner.SetError**
- **Found during:** Task 1.5
- **Issue:** `Parser.Default` is a static singleton that captures `Console.Error` at first access. When InProcessCliRunner calls `Console.SetError(swErr)` then invokes `Program.Main`, the parser still writes to the original (pre-redirect) stderr stream, leaving captured stderr empty.
- **Fix:** Replaced `Parser.Default.ParseArguments(...)` with `new Parser(settings => { settings.HelpWriter = System.Console.Error; })` so the help writer is resolved after `SetError` at each call-site.
- **Impact:** In-process banner uses `CommandLine 2.9.1` (CLP library metadata) instead of `utinni-cli 1.0.0+<hash>` (assembly metadata). Fixtures updated to reflect actual in-process output with version masking (`<version>` sentinel).
- **Files modified:** `Utinni.Cli/Program.cs`, `Utinni.Cli.Tests/Fixtures/dispatch/*.expected.txt`
- **Commit:** 3efd184

**2. [Rule 2 - Missing critical functionality] 4th [Fact] test missing to reach 18-test target**
- **Found during:** Task 1.5 (test count = 17 after 3 [Fact] + 4 [Theory rows])
- **Issue:** Plan's §3 verification explicitly says "18 tests: 10 JsonOutput + 4 [Fact] dispatch + 4 [Theory rows]". Only 3 [Fact] dispatch tests were initially created.
- **Fix:** Added `Run_WithVerbAndMissingRequiredArg_ExitsOne` [Fact] verifying that a verb without required positional arg exits 1.
- **Files modified:** `Utinni.Cli.Tests/Commands/CommandDispatchTests.cs`
- **Commit:** 3efd184

## Threat Flags

None. Plan 04-01 adds no new network endpoints, auth paths, or schema changes. The `validate-plugin` command's HelpText surfaces the T-04-EoP warning ("only run against trusted plugin directories"); full DLL-load threat mitigation deferred to Plan 04-04.

## Known Stubs

| Stub | File | Reason |
|------|------|--------|
| `ParseTreCommand.Run` returns NotImplemented | `Utinni.Cli/Commands/ParseTreCommand.cs` | Intentional — Plan 04-02 implements TRE parsing |
| `ListObjectsCommand.Run` returns NotImplemented | `Utinni.Cli/Commands/ListObjectsCommand.cs` | Intentional — Plan 04-02 implements list-objects |
| `InspectIffCommand.Run` returns NotImplemented | `Utinni.Cli/Commands/InspectIffCommand.cs` | Intentional — Plan 04-03 implements IFF inspection |
| `ValidatePluginCommand.Run` returns NotImplemented | `Utinni.Cli/Commands/ValidatePluginCommand.cs` | Intentional — Plan 04-04 implements plugin validation |

All stubs are intentional scaffolding that Plans 04-02/03/04 will replace. The stubs do not prevent this plan's goal (scaffold + dispatch smoke + harness) from being achieved.

## Self-Check: PASSED
