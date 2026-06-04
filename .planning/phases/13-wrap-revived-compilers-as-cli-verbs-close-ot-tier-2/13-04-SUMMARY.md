---
phase: 13-wrap-revived-compilers-as-cli-verbs-close-ot-tier-2
plan: 04
subsystem: api
tags: [cli, build-verb, compile-template, build-tre, subprocess, byte-exact, rsp]

requires:
  - phase: 12-revive-feasibility-spike
    provides: TemplateCompiler + TreeFileBuilder green native exes
  - plan: 13-02
    provides: NativeToolRunner subprocess seam + RspSynthesizer
provides:
  - compile-template verb (AUTH-03) wrapping TemplateCompiler
  - build-tre verb (AUTH-04) wrapping TreeFileBuilder with synth-.rsp byte-exact ladder
  - NativeToolResolver (exe resolution beside utinni-cli / --tool-path)
  - NativeToolRunner stdin-close + 60s timeout backstop (hang-proofing)
affects: [13-05, 14-mcp]

tech-stack:
  added: []
  patterns: [BUILD verb = NativeToolRunner wrapper, synth-.rsp byte-exact determinism test]

key-files:
  created:
    - Utinni.Cli/Commands/CompileTemplateCommand.cs
    - Utinni.Cli/Commands/BuildTreCommand.cs
    - Utinni.Cli/Commands/Subprocess/NativeToolResolver.cs
    - Utinni.Cli.Tests/Commands/CompileTemplateGoldenTests.cs
    - Utinni.Cli.Tests/Commands/BuildTreGoldenTests.cs
  modified:
    - Utinni.Cli/Program.cs
    - Utinni.Cli/Commands/Subprocess/NativeToolRunner.cs (stdin-close + timeout)
    - Utinni.Cli/Commands/Subprocess/RspSynthesizer.cs (tree-first .rsp fix)
    - Utinni.Cli.Tests/Fixtures/dispatch/ (goldens refreshed for 2 new verbs)

key-decisions:
  - "TreeFileBuilder .rsp is TREE-first (<treePath> @ <diskPath>), NOT disk-first as RESEARCH claimed — verified in addResponseFile. Disk-first silently built empty archives."
  - "NativeToolRunner gained a stdin-close + 60s timeout backstop: the SOE tools wedge on error/exit paths (linked Perforce ClientUser / FATAL handler), so the verb must not hang."
  - "compile-template success golden is a documented gate-finding: a compilable .tpf needs a registered compiled-in template class + canonical SOE .tdf (none exist as assets). Verb + error paths shipped."

patterns-established:
  - "BUILD verb = thin NativeToolResolver + NativeToolRunner.Run wrapper; the subprocess seam owns exit-code mapping + envelope."
  - "Synth-.rsp byte-exact test: build twice from one source -> SHA256-identical + re-parses to original record set (regression guard against empty-archive)."

requirements-completed: [AUTH-03, AUTH-04]

duration: ~70min
completed: 2026-06-04
---

# Phase 13 Plan 04: compile-template + build-tre Summary

**Two BUILD verbs wrapping the Phase-12 natives ship: build-tre (AUTH-04) with a CONFIRMED synth-.rsp byte-exact ladder, and compile-template (AUTH-03) with error-path coverage + a documented .tpf gate-finding. The 13-02 subprocess seam gained two robustness fixes surfaced by probing the real exes.**

## Performance

- **Duration:** ~70 min (much of it reverse-engineering the .tpf/.tdf format + the .rsp tree-first discovery)
- **Completed:** 2026-06-04
- **Tasks:** 2
- **Files:** 5 created + 4 modified

## Accomplishments
- `build-tre` (AUTH-04): wraps TreeFileBuilder; `--from-tre` synthesizes the `.rsp` via RspSynthesizer. **D-06 byte-exact CONFIRMED** for the uncompressed case — same source → SHA256-identical `.tre`, re-parsing to the original record set.
- `compile-template` (AUTH-03): wraps TemplateCompiler; error-path mapping tested (missing-exe/-input → exit 3).
- `NativeToolResolver` + Program.cs registration of both verbs.
- Two seam fixes (see Deviations): tree-first `.rsp`, and stdin-close + timeout hang-proofing.

## Task Commits

- **Fixes (cross-cutting):** `72bd04d` (fix) — tree-first .rsp + NativeToolRunner stdin/timeout
- **Task 1: compile-template verb + resolver** — `ba00bd8` (feat)
- **Task 2: build-tre verb + byte-exact ladder** — `4e54abc` (feat)

## Deviations from plan (significant)

1. **`.rsp` is TREE-first, not disk-first.** RESEARCH asserted `<diskPath> @ <treePath>`; the actual TreeFileBuilder `addResponseFile` parser puts the in-tree name BEFORE `@` and the disk path AFTER. The disk-first synthesizer silently packed 0 files → a 36-byte header-only archive that a naive byte-stable check would falsely pass. Fixed RspSynthesizer + its tests; added a "non-empty + re-parses to N records" regression guard.
2. **The SOE tools hang on error/exit paths.** TemplateCompiler wedged after printing an error (the linked Perforce ClientUser / FATAL handler waits on console input), even with redirected+closed stdin and CreateNoWindow. Added a 60s wall-clock timeout (kill → ToolTimeout exit 2) to NativeToolRunner as the reliable backstop — important for the Phase-14 MCP server too.
3. **compile-template success golden = gate-finding.** A compilable `.tpf` requires a REGISTERED template class (compiled-in `Shared*ObjectTemplate`) whose `id` tag matches a canonical SOE `.tdf` with all params supplied — empirically confirmed (TemplateCompiler errors "Unable to create template class. May not be installed." on a synthetic class). No canonical SOE `.tpf`/`.tdf` assets exist (the documented Phase-12 gate-finding). The verb + error-path coverage ship; the byte-correct `.iff` golden is deferred until a real pair is supplied (the golden harness retires it then). The D-03 managed cross-check rides on that same future fixture.

## Gate-findings

- **compile-template `.tpf`/`.tdf` reference pair** — none exist; documented above (Phase-12 class). build-tre's real native compile proves the seam's success-envelope, so the seam is not itself a gate-finding.

## Verification

- `MSBuild Utinni.Cli.Tests.csproj /p:Configuration=Debug /p:Platform=x86` → green.
- `dotnet test --no-build` full suite → **195 passed, 2 skipped (pre-existing env-gated), 0 failed**.
- build-tre byte-exact ladder ran against the real `TreeFileBuilder_d.exe` (located via repo-root walk) — SHA256-identical rebuilds, non-empty, 2 records.
- Both verbs registered in Program.cs (ParseArguments + MapResult); dispatch goldens refreshed.
