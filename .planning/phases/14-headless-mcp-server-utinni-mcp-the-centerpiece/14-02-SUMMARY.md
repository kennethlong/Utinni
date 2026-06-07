---
phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece
plan: 02
subsystem: api
tags: [mcp, modelcontextprotocol, net10, read-tools, envelope-mapper, shape-validation, exit-code-taxonomy, temp-boundary, get-template-schema]

# Dependency graph
requires:
  - phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece (plan 01)
    provides: "ResolvedRoot.Resolve + CliDispatcher.RunAsync + CliInvocationResult (the contracts the read tools + mapper consume)"
  - phase: 13-wrap-revived-compilers
    provides: "the parse-tre/inspect-iff/decode-iff/list-objects/compile-definition utinni-cli JSON verbs the read tools wrap"
provides:
  - "CliResultMapper — CliInvocationResult -> CallToolResult: verbatim envelope pass-through (text mirror + StructuredContent), envelope-SHAPE validation, exit-code -> MCP error taxonomy"
  - "ReadTools — 5 ReadOnly+Idempotent MCP tools (read_tre / inspect_iff / decode_iff / list_world_objects / get_template_schema), each a thin resolve->dispatch->map wrapper"
  - "TempSchemaOutput — IDisposable host-temp --out boundary OUTSIDE resolvedRoot for get_template_schema (cleaned up on Dispose)"
  - "CliResultMapper.DryRunNotice(abs) — non-spawning dry-run notice stub consumed by Plan 03's repack_tre"
affects: [14-03-write-save-tools, 14-04-roundtrip-security]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Single chokepoint for CLI-stdout interpretation: CliResultMapper is the ONLY place stdout is parsed; tool bodies never touch CLI output (thin-host invariant)"
    - "Envelope SHAPE validation (schemaVersion + command + result XOR error) as a hard gate, distinct from 'valid JSON' (REVIEWS Consensus #7)"
    - "Exit-code/envelope cross-check: exit 0 MUST carry result, exit 1/2/3 MUST carry error; any contradiction or out-of-range exit -> hard McpException"
    - "Documented writable-path boundary OUTSIDE resolvedRoot via IDisposable temp handle (get_template_schema --out)"

key-files:
  created:
    - "Utinni.Mcp/Tools/CliResultMapper.cs"
    - "Utinni.Mcp/Tools/ReadTools.cs"
    - "Utinni.Mcp/Tools/TempSchemaOutput.cs"
    - "Utinni.Mcp.Tests/CliResultMapperTests.cs"
  modified: []

key-decisions:
  - "CallToolResult.StructuredContent is a JsonElement? (not JsonNode) in ModelContextProtocol 1.4.0 — the mapper re-parses the validated raw stdout into a cloned JsonElement (byte-identical to the CLI output, not a rewrite)"
  - "Exit-code/envelope cross-check is part of shape validation: exit-0-with-error-envelope AND non-zero-with-result-envelope are both hard errors (contradictions), beyond the bare result-XOR-error rule"
  - "get_template_schema parses NOTHING from the temp file; it returns the CLI's stdout {schemaPath,...} envelope via the mapper (the temp file is a side-effect artifact, not a data source)"

patterns-established:
  - "Pattern: CliResultMapper as the shared verbatim-pass-through + shape-validator both read (this plan) and write (Plan 03) tools consume"
  - "Pattern: thin [McpServerTool] wrapper = root.Resolve -> cli.RunAsync(verb, [abs]) -> CliResultMapper.ToCallToolResult"

requirements-completed: [MCP-01]

# Metrics
duration: ~20min
completed: 2026-06-07
---

# Phase 14 Plan 02: Read MCP Tools + Envelope Mapper Summary

**The read half of the centerpiece — `CliResultMapper` passes the utinni-cli sorted-key JSON envelope through verbatim (text mirror + StructuredContent), validating the envelope SHAPE (schemaVersion + command + result XOR error) and applying the full exit-code -> MCP error taxonomy (hard error on exe-missing / timeout / non-JSON / empty / malformed-shape / out-of-range exit / exit-0-with-error / non-zero-with-result), feeding 5 thin ReadOnly tools (`read_tre` / `inspect_iff` / `decode_iff` / `list_world_objects` / `get_template_schema`) that each resolve a relative path under the pinned root, dispatch one CLI verb, and map the result — proven by 18 mapper taxonomy tests including a deep-equality semantic pass-through assertion.**

## Performance

- **Duration:** ~20 min
- **Started:** 2026-06-07T02:00Z (approx)
- **Completed:** 2026-06-07
- **Tasks:** 2 (both `auto`; Task 1 TDD)
- **Files modified:** 4 (4 created, 0 modified)

## Accomplishments

- **Task 1 (TDD): `CliResultMapper`** — the shared, single-chokepoint translator from `CliInvocationResult` to MCP `CallToolResult`. Validates the envelope SHAPE (not merely "valid JSON" — REVIEWS Consensus #7): requires `schemaVersion` + `command` + exactly one of (`result`, `error`). Applies the Phase-13 exit-code taxonomy and an exit-code/envelope cross-check: exit 0 + `result` -> `IsError=false`; exit 1/2/3 + `error` -> `IsError=true` (agent self-corrects); exe-missing / timeout / non-JSON / empty stdout / malformed-shape / exit outside {0,1,2,3} / exit-0-with-error-envelope / non-zero-with-result-envelope -> hard `McpException`. The envelope is passed through VERBATIM (raw stdout as a text block + the parsed node as `StructuredContent`); no field is ever rewritten. `stderr` is captured for diagnostics, never parsed. Includes the `DryRunNotice(abs)` stub Plan 03's `repack_tre` consumes.
- **Task 2: the 5 read tools** — `ReadTools` (`[McpServerToolType]`) with 4 primary reads wrapping `parse-tre` / `inspect-iff` / `decode-iff` / `list-objects` plus the ReadOnly `get_template_schema` wrapping `compile-definition --skip-native`. Every tool is a thin `root.Resolve(relativePath) -> cli.RunAsync(verb, [abs]) -> CliResultMapper.ToCallToolResult(r)` wrapper with zero format/business logic. `decode_iff` is the single typed-read surface (CLI auto-dispatches by root FORM); `inspect_iff` stays distinct (raw chunk tree vs typed model).
- **`TempSchemaOutput`** — the IDisposable host-temp `--out` boundary for `get_template_schema`: allocates a unique file under `Path.GetTempPath()` (explicitly OUTSIDE `resolvedRoot` — the documented writable boundary, T-14-13) and deletes it on `Dispose` regardless of outcome. The tool parses NOTHING from the temp file; it returns the CLI's `{schemaPath,...}` stdout envelope through the mapper.

## Task Commits

Each task was committed atomically:

1. **Task 1: CliResultMapper — envelope-SHAPE validation + exit-code taxonomy** - `06c3848` (feat)
2. **Task 2: read MCP tools + get_template_schema with temp boundary** - `d68eca1` (feat)

## Files Created/Modified

- `Utinni.Mcp/Tools/CliResultMapper.cs` — verbatim envelope pass-through + SHAPE validation + exit-code taxonomy + `DryRunNotice` stub.
- `Utinni.Mcp/Tools/ReadTools.cs` — `[McpServerToolType]` with 5 `[McpServerTool(ReadOnly, Idempotent)]` thin wrappers.
- `Utinni.Mcp/Tools/TempSchemaOutput.cs` — IDisposable temp `--out` boundary under `Path.GetTempPath()`, cleaned up on Dispose.
- `Utinni.Mcp.Tests/CliResultMapperTests.cs` — 18 taxonomy facts (success, in-band 1/2/3, exe-missing, timeout, non-JSON, empty stdout, missing schemaVersion/command, both result+error, neither, exit-0-with-error, out-of-range exit, non-zero-with-result, stderr pollution, DryRunNotice, deep-equality semantic pass-through).

## Decisions Made

- **`StructuredContent` is `JsonElement?`, not `JsonNode`** in ModelContextProtocol 1.4.0. The mapper validates the envelope shape on a parsed `JsonObject` (cheap `ContainsKey` checks), then re-parses the validated raw stdout into a cloned `JsonElement` for `StructuredContent`. This is byte-identical to the CLI output (a pass-through, not a rewrite) — the test asserts deep-equality between the original envelope and the round-tripped structured content.
- **Exit-code/envelope cross-check folded into shape validation.** Beyond the bare `result XOR error` rule, the mapper rejects exit-0-with-error-envelope and non-zero-with-result-envelope as contradictions (hard errors). This closes the "a child crash with JSON-looking stdout becomes an in-band answer" gap (REVIEWS Consensus #7).
- **`get_template_schema` returns the CLI stdout envelope, not the temp file.** Per the CompileDefinitionCommand contract, the CLI writes the schema to `--out` AND emits a `{schemaPath, classCount, nativeStatus}` envelope on stdout. The tool maps the stdout envelope and parses nothing from the temp file — the temp file is a side-effect artifact behind the documented writable boundary, cleaned up on Dispose.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - API shape] `StructuredContent` typed as `JsonElement?`, not `JsonNode`**
- **Found during:** Task 1 (first GREEN build)
- **Issue:** The plan's `<behavior>` and test sketch implied a `JsonNode`-based `StructuredContent` with `.ToJsonString()`; the actual ModelContextProtocol 1.4.0 `CallToolResult.StructuredContent` property is `System.Text.Json.JsonElement?`.
- **Fix:** The mapper assigns a cloned `JsonElement` (re-parsed from the validated raw stdout — byte-identical, not a rewrite); the deep-equality test reads `StructuredContent.Value.GetRawText()` and compares via `JsonNode.DeepEquals`. Shape validation still runs against a parsed `JsonObject`.
- **Files modified:** Utinni.Mcp/Tools/CliResultMapper.cs, Utinni.Mcp.Tests/CliResultMapperTests.cs
- **Verification:** `dotnet test --filter CliResultMapperTests` -> 18/18 pass.
- **Committed in:** 06c3848

---

**Total deviations:** 1 auto-fixed (1 API-shape reconciliation, no scope change).
**Impact on plan:** No scope change — the published behavior (verbatim pass-through, shape validation, taxonomy) is exactly as planned; only the concrete `StructuredContent` type differs from the sketch.

## Issues Encountered

- A reflection-based runtime probe to enumerate the 5 discovered `[McpServerTool]` methods failed under PowerShell's `Add-Type` / `AssemblyResolve` load context (transitive MCP SDK deps + recursive resolve handler). Tool discoverability is instead evidenced by the build succeeding with the `ModelContextProtocol.Analyzers` active (which validates `[McpServerTool]` method signatures at compile time) plus the correct `[McpServerToolType]` + `[McpServerTool]` attributes and DI-injectable `ResolvedRoot`/`CliDispatcher` parameters. End-to-end tool discovery over a real MCP client is the explicit Plan 14-04 integration proof.

## Known Stubs

- `CliResultMapper.DryRunNotice(abs)` is an intentional, plan-specified stub for Plan 03's `repack_tre` dry-run path (returns a non-error notice describing what WOULD happen, no CLI spawn). Documented in its doc-comment and wired by 14-03. Not a gap.

## Threat Flags

None. All security-relevant surface introduced this plan (the `get_template_schema` host-temp `--out` boundary T-14-13, the resolved-absolute-path info-disclosure T-14-12, the mapper opaque-pass-through T-14-07) is already enumerated in the plan's `<threat_model>`; MCP-SECURITY.md (Plan 04) consolidates.

## Coverage Notes (Cursor LOW / Codex)

- SC1 "read any supported asset" is kept format-agnostic via `decode_iff` auto-dispatch; this plan unit-proves the mapper taxonomy. Broadened integration coverage (`decode_iff` over `.tab`/`.stf`/OT fixtures, `read_tre`) is the Plan 14-04 integration layer. The read tools are unit-proven (mapper) + build-proven (tool wiring) here; integration-proven in 14-04.

## User Setup Required

None — no external service configuration. (Runtime use still requires `--root`/`UTINNI_MCP_ROOT`, enforced fail-closed by Plan 01's ResolvedRoot; operational config, not setup.)

## Next Phase Readiness

- `CliResultMapper` (incl. `DryRunNotice`) is the shared seam Plan 03's write/save + `repack_tre` tools consume — the verify-vs-fail decision rides on the exit code alone, exactly as the mapper's taxonomy enforces.
- The 5 read tools are discoverable by `WithToolsFromAssembly` and ready for the Plan 14-04 real-MCP-client round-trip + broadened read coverage.
- No blockers.

## Self-Check: PASSED

- All created files verified present (see below).
- Both task commits verified in `git log`.

---
*Phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece*
*Completed: 2026-06-07*
