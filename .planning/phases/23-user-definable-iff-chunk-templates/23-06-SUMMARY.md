---
phase: 23-user-definable-iff-chunk-templates
plan: 06
subsystem: mcp
tags: [mcp, mcp-oop, dec-v2-mcp-oop, d-16, template-engine, thin-dispatcher, read-tool, path-containment, verbs-first]

# Dependency graph
requires:
  - phase: 23-user-definable-iff-chunk-templates
    plan: 05
    provides: "decode-with-template verb (path [Value(0)] + optional --template/--templates-dir; navigable decode envelope + D-07 FitReport)"
  - phase: 14-mcp-server
    provides: "ReadTools thin-tool pattern (ResolvedRoot.Resolve + CliDispatcher.RunAsync + CliResultMapper.ToCallToolResult) + the RecordingDispatcher/SpawnRecordingDispatcher test idiom"
provides:
  - "summarize_with_template: a thin ReadOnly+Idempotent MCP tool that shells utinni-cli decode-with-template, resolves the agent path under the pinned root, and passes the envelope through verbatim — ZERO template/format logic in the net10 MCP process (DEC-V2-MCP-OOP, D-16)"
affects: [23-07, 23-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "summarize_with_template is the verbatim 3-line ReadTools thin-shell body (root.Resolve -> cli.RunAsync(verb) -> CliResultMapper.ToCallToolResult) cloned from SummarizeClientEffect — the ONLY delta is the verb name (decode-with-template), preserving the MCP-OOP boundary by construction"
    - "TemplateMcpToolTests clones the Terrain/Particle RecordingDispatcher + SpawnRecordingDispatcher idiom: dispatch (verb + Resolve), verbatim envelope pass-through, nonzero-exit -> tool error, and path-escape -> hard error with ZERO CLI spawns — proven WITHOUT a real CLI"

key-files:
  created:
    - Utinni.Mcp.Tests/Template/TemplateMcpToolTests.cs
  modified:
    - Utinni.Mcp/Tools/ReadTools.cs

key-decisions:
  - "The tool dispatches decode-with-template with ONLY the resolved absolute path (new[]{abs}) — the optional --template/--templates-dir flags are NOT plumbed through the MCP surface this plan; the verb auto-resolves the single eligible leaf + the effective pack allow-list (TemplatePackStore.DefaultRoots), keeping the tool the verbatim 3-line shell the plan specifies and matching the existing read-tool surface (no flag pass-through on any summarize_* tool)"
  - "Test + implementation committed together as one atomic feat (the test references the new method and cannot compile without it); the RED behavior was confirmed by the method-not-found compile gap before the ReadTools edit, then GREEN after"

patterns-established:
  - "Wrapping a verbs-first CLI verb as an MCP tool is a one-method clone of the ReadTools thin-shell with the verb name swapped — the MCP process never gains format logic, so the OOP boundary cannot regress"

requirements-completed: [PROD-IFFT-03]

# Metrics
duration: 4min
completed: 2026-06-21
---

# Phase 23 Plan 06: Thin MCP Read Tool (summarize_with_template) Summary

**The thin MCP read tool (D-16 / DEC-V2-MCP-OOP): one `summarize_with_template` ReadOnly+Idempotent tool in `ReadTools.cs` that resolves the agent-supplied relative path under the pinned root and shells `utinni-cli decode-with-template`, passing the navigable decode envelope + D-07 FitReport through verbatim — ZERO template/format logic in the net10 MCP process. An AI agent can now read an unknown chunk through a schema template via the same engine the CLI + UI use. The shell-out shape test (`TemplateMcpToolTests`) proves dispatch, verbatim pass-through, nonzero-exit-to-tool-error, and the path-escape fail-closed guard (zero CLI spawns).**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-06-21T04:52:40Z
- **Completed:** 2026-06-21T04:56:xxZ
- **Tasks:** 1 (tdd)
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments
- **Task 1 — summarize_with_template MCP tool + shell-out test:** Added ONE method to `ReadTools.cs`: `[McpServerTool(Name="summarize_with_template", ReadOnly=true, Idempotent=true)]` with a `[Description]` stating the ZERO-format-logic / MCP-OOP boundary (mirroring `SummarizeClientEffect`). The DI signature is the exact thin-tool shape (`ResolvedRoot root, CliDispatcher cli, [Description(PathParamDescription)] string relativePath`) and the body is the verbatim 3 lines: `string abs = root.Resolve(relativePath);` -> `await cli.RunAsync("decode-with-template", new[]{abs})` -> `return CliResultMapper.ToCallToolResult(r);`. No template/format logic in this process. Added `Utinni.Mcp.Tests/Template/TemplateMcpToolTests.cs` cloning the existing MCP shell-out test idiom (RecordingDispatcher + SpawnRecordingDispatcher): asserts the dispatched verb is `decode-with-template` and the resolved absolute path is the single argv; asserts the envelope passes through verbatim; asserts a nonzero CLI exit surfaces as a tool error (not a success envelope); asserts a root-escape path throws with ZERO CLI spawns.
- **Lane GREEN:** `dotnet test Utinni.Mcp.Tests --no-build --filter "FullyQualifiedName~Template"` = 8 passed / 0 skipped / 0 failed (net10 lane). MCP test project build = 0 warnings / 0 errors.

## Task Commits

Each task was committed atomically:

1. **Task 1: summarize_with_template thin MCP read tool + shell-out test** - `1a93095` (feat)

**Plan metadata:** (this commit) (docs: complete plan)

_TDD note: the task is `tdd="true"`. The test (`TemplateMcpToolTests`) references `ReadTools.SummarizeWithTemplate`, which did not exist before the implementation edit — the RED state was the compile gap (method-not-found). The implementation then turned the 8 assertions GREEN. Test + implementation committed together as one atomic feat because the test cannot compile (RED-as-failing-run) without the method symbol; the behavioral assertions (dispatch verb, verbatim pass-through, nonzero-exit-to-tool-error, path-escape zero-spawn) are real checks, not skips._

## Files Created/Modified
- `Utinni.Mcp/Tools/ReadTools.cs` - added the `summarize_with_template` thin tool (root.Resolve -> cli.RunAsync(decode-with-template) -> CliResultMapper.ToCallToolResult), verbatim 3-line body, ZERO format logic
- `Utinni.Mcp.Tests/Template/TemplateMcpToolTests.cs` - shell-out shape test: dispatch (verb + Resolve), verbatim envelope pass-through, nonzero-exit -> tool error, path-escape -> hard error with zero CLI spawns (Trait Category=Template)

## Decisions Made
See the `key-decisions` frontmatter. Load-bearing: (1) the tool dispatches `decode-with-template` with ONLY the resolved absolute path — the optional `--template`/`--templates-dir` flags are not plumbed through the MCP surface (matching every other `summarize_*` read tool; the verb auto-resolves the eligible leaf + the effective pack allow-list); (2) the test + implementation were committed together as one atomic feat because the test cannot compile without the new method symbol.

## Deviations from Plan

None - plan executed exactly as written.

## Authentication Gates

None.

## Issues Encountered

- **Pre-existing out-of-scope failure — `AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline`:** the documented Phase-17 harness gotcha (already in deferred-items.md + the prior-wave note). Out of scope: this plan is pure managed net10 `Utinni.Mcp` work; `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs` confirmed the Generated file is unchanged. Not chased.

## Known Stubs

None. The tool is fully wired to the real engine — it shells the shipped `decode-with-template` verb (23-05), which routes through TemplateResolver + KernelCodec + TemplatePackStore. No hardcoded empty values, placeholder text, or unwired data paths.

## Next Phase Readiness
- The MCP surface for templates is complete: 23-07 (UI) consumes the same `list-templates` / `decode-with-template` / `apply-save-template` verbs for its picker + editor; 23-08 can wrap any remaining verb as a thin tool with the same one-method clone.
- The MCP-OOP boundary is preserved by construction — `summarize_with_template` carries ZERO format logic; the subprocess boundary to the x86 `utinni-cli` is the architecture boundary.

## Self-Check: PASSED
- FOUND: Utinni.Mcp/Tools/ReadTools.cs (contains summarize_with_template)
- FOUND: Utinni.Mcp.Tests/Template/TemplateMcpToolTests.cs
- FOUND commit: 1a93095 (Task 1)

---
*Phase: 23-user-definable-iff-chunk-templates*
*Completed: 2026-06-21*
