---
phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece
plan: 03
subsystem: api
tags: [mcp, modelcontextprotocol, net10, write-tools, apply-save, opaque-passthrough, exit-code-decision, dry-run-gate, path-escape-boundary, per-path-serialization]

# Dependency graph
requires:
  - phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece (plan 01)
    provides: "ResolvedRoot.Resolve (boundary path-escape gate) + CliDispatcher.RunAsync (subprocess seam) + CliInvocationResult"
  - phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece (plan 02)
    provides: "CliResultMapper.ToCallToolResult (opaque envelope pass-through + exit-code taxonomy) + CliResultMapper.DryRunNotice"
  - phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece (plan 03a)
    provides: "apply-save-tab/iff/stf/ot CLI verbs (apply+verify+commit atomically; exit 0=persisted / 2=verify-failed-no-write) + repack-tre destructive verb"
provides:
  - "SaveTools — 4 save_* write tools (save_datatable/save_iff/save_stringtable/save_object_template), each a THIN dispatcher over ONE apply-save-* verb, opaque envelope pass-through, persist-vs-fail on EXIT CODE"
  - "RepackTool — repack_tre distinct Destructive tool, host-side dry_run=true default (plan-only, no spawn, no backup claim), unreachable from save_*"
  - "VerifyTools — roundtrip_check verify-only (non-persisting) tool over roundtrip-* verbs"
  - "SaveVerb — format->verb map + typed-arg->argv builders + per-resolved-path async serialization (T-14-16)"
  - "CliDispatcher.RunAsync is now virtual (CliDispatcher unsealed) so write-tool tests substitute a recording stub; production behavior unchanged"
affects: [14-04-roundtrip-security, mcp-tool-plans]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Thin write tool = root.Resolve(rel) [boundary escape gate, no spawn on escape] -> SaveVerb argv builder -> SaveVerb.RunSerializedAsync(abs, () => cli.RunAsync(apply-save-*) -> CliResultMapper.ToCallToolResult) — host decides on EXIT CODE alone, never parses a domain field"
    - "Host-side dry_run gate for a verb with NO --dry-run flag: dry_run=true returns DryRunNotice WITHOUT spawning (plan-only, no backup claim); dry_run=false spawns the backup-routed destructive verb"
    - "Per-resolved-path SemaphoreSlim serialization (ConcurrentDictionary keyed by case-insensitive abs path): same-path writes serialized, different-path parallel"
    - "Unseal + virtual RunAsync to enable a recording stub dispatcher in tests (no separate interface) — proves exactly-one-spawn / typed-argv / zero-spawn-on-escape"

key-files:
  created:
    - "Utinni.Mcp/Tools/SaveTools.cs"
    - "Utinni.Mcp/Tools/RepackTool.cs"
    - "Utinni.Mcp/Tools/VerifyTools.cs"
    - "Utinni.Mcp/Server/SaveVerb.cs"
    - "Utinni.Mcp.Tests/SaveCompositionTests.cs"
    - "Utinni.Mcp.Tests/McpBoundaryPathEscapeTests.cs"
  modified:
    - "Utinni.Mcp/Server/CliDispatcher.cs"

key-decisions:
  - "save_* argv passes the RELATIVE path + --root (the apply-save-* verb re-resolves under --root as defense-in-depth); roundtrip-*/repack-tre take a positional ABSOLUTE path (no --root flag) so roundtrip_check/repack_tre pass the resolved abs path"
  - "Concurrency policy lives in SaveVerb.RunSerializedAsync (per-resolved-path lock), consumed by SaveTools + RepackTool + VerifyTools — one source of truth for the serialization contract"
  - "CliDispatcher unsealed + RunAsync virtual (NOT a new interface) is the minimal change to make the write tools testable with a recording stub; production always uses the concrete dispatcher via DI"
  - "save_object_template uses an 'operation' (add/edit/remove) discriminator; an unrecognized op falls through to add-override and the verb returns an in-band usage error (host stays thin, no host-side validation)"
  - "repack_tre dry_run gate is HOST-SIDE because repack-tre has no --dry-run flag; the DryRunNotice is plan-only and never claims a backup (Cursor LOW)"

patterns-established:
  - "Pattern: thin save_* write tool wrapping ONE apply-save-* verb, deciding persist-vs-fail on the exit code via CliResultMapper (no domain-field parse)"
  - "Pattern: host-side dry_run gate for a destructive verb with no --dry-run flag"
  - "Pattern: per-resolved-path serialization shared across the write + verify + repack surface"

requirements-completed: [MCP-02]

# Metrics
duration: ~25min
completed: 2026-06-07
---

# Phase 14 Plan 03: Write/Save MCP Tools + repack_tre + roundtrip_check Summary

**The write half of the centerpiece — four THIN `save_*` tools (`save_datatable`->`apply-save-tab`, `save_iff`->`apply-save-iff`, `save_stringtable`->`apply-save-stf`, `save_object_template`->`apply-save-ot`) each wrapping the SINGLE Plan-14-03a `apply-save-*` verb in ONE `cli.RunAsync`, passing the verb's envelope through OPAQUELY (the host decides persist-vs-fail on the EXIT CODE alone — 0 persisted / 2 verify-failed — and never parses `bytesEqualUntouched`), plus the distinct off-by-default `repack_tre` Destructive tool (host-side `dry_run=true` plan-only gate, no spawn, no backup claim, unreachable from `save_*`) and the verify-only non-persisting `roundtrip_check`, all sharing `SaveVerb`'s format->verb map + typed-arg->argv builders + per-resolved-path serialization — proven by 16 composition + MCP-boundary path-escape tests (zero CLI spawns on `../../outside.tab`).**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-06-07
- **Tasks:** 2 (both `auto`)
- **Files modified:** 7 (6 created, 1 modified)

## Accomplishments

- **Task 1 — `save_*` tools + `roundtrip_check` + concurrent-dispatch policy:** `SaveTools` (`[McpServerToolType]`) with 4 write tools, each taking format-appropriate TYPED mutation args (datatable: `recordIndex`+`columnId`+`value` or `removeRow`/`removeColumn`; iff: `leafId`+`payloadHex` or `removeLeaf`; stringtable: `key`+`value`; object_template: `field`+`operation`+`valueInt`) — never a free-form blob. Each body: (1) `root.Resolve(relativePath)` FIRST (boundary path-escape gate — throws on escape, NO CLI spawned), (2) build the apply-save-* argv via `SaveVerb` (relative path stays relative; `--root` passed for the verb's defense-in-depth re-resolve), (3) ONE `cli.RunAsync("apply-save-<fmt>", argv)`, (4) `CliResultMapper.ToCallToolResult(r)` — opaque pass-through, exit 0 -> `IsError=false`, exit 2 -> `IsError=true` in-band, no domain-field parse. `SaveVerb` carries the format->verb map (`ApplyVerb`/`RoundtripVerb`) + the typed-arg->argv builders so all four tools (and `roundtrip_check`) share argv assembly. `VerifyTools.roundtrip_check` runs the matching `roundtrip-*` verb (non-persisting verify) given a `format` discriminator + the same typed args; its `[Description]` states it writes NOTHING.
- **Concurrent-dispatch policy (Cursor, T-14-16):** `SaveVerb.RunSerializedAsync` — a `ConcurrentDictionary<string, SemaphoreSlim>` keyed by the case-insensitive resolved absolute path. Two parallel `save_*`/`repack_tre` calls on the SAME asset serialize (proven `MaxConcurrent==1`); calls on DIFFERENT paths run in parallel (proven `MaxConcurrent==2`).
- **Task 2 — `repack_tre`:** `RepackTool` with a single `[McpServerTool(Destructive=true, Idempotent=false, ReadOnly=false)]` `repack_tre`. Because the `repack-tre` verb has NO `--dry-run` flag (confirmed in `RepackTreCommand.cs`), the gate is HOST-SIDE: `dry_run` defaults to `true` and returns `CliResultMapper.DryRunNotice(abs)` WITHOUT spawning the verb (plan-only — does NOT validate lock/support/backup, does NOT claim a backup). Only `dry_run=false` spawns `repack-tre` (which routes through `TreBackupPath` backup + `TreRepackLock` refuse-in-use + refuse-V6000). Path-escape gate applies first (throws before the gate). Honors the same per-resolved-path serialization. NEVER reachable from any `save_*` tool.
- **Test seam:** `CliDispatcher` unsealed + `RunAsync` made `virtual` (the minimal change — no new interface) so the tests substitute a recording stub subclass that records `(verb, argv)` per spawn. Production always wires the concrete `CliDispatcher` via DI; only tests override.

## Task Commits

Each task was committed atomically:

1. **Task 1: save_* tools wrap apply-save-* opaquely + roundtrip_check + per-path serialization** - `d01be1a` (feat)
2. **Task 2: repack_tre destructive tool with host-side dry_run gate + MCP-boundary path-escape proof** - `d6aafcc` (feat)

## Files Created/Modified

- `Utinni.Mcp/Tools/SaveTools.cs` — 4 `save_*` write tools; typed args; resolve->one-apply-save-verb->opaque-map; serialized per resolved path.
- `Utinni.Mcp/Tools/RepackTool.cs` — `repack_tre` Destructive, host-side `dry_run=true` plan-only gate (no spawn, no backup claim), real run spawns backup-routed verb; unreachable from `save_*`.
- `Utinni.Mcp/Tools/VerifyTools.cs` — `roundtrip_check` verify-only (non-persisting) over `roundtrip-*` verbs with a `format` discriminator.
- `Utinni.Mcp/Server/SaveVerb.cs` — format->verb map (`ApplyVerb`/`RoundtripVerb`) + typed-arg->argv builders + `RunSerializedAsync` per-resolved-path lock.
- `Utinni.Mcp/Server/CliDispatcher.cs` — unsealed + `RunAsync` virtual (documented test seam; production unchanged).
- `Utinni.Mcp.Tests/SaveCompositionTests.cs` — 12 facts: exactly-one-spawn + typed argv (cell/remove-row/remove-column), exit-2 in-band opaque (envelope mirrored verbatim, no field parse), save_iff/stf/ot verb mapping, roundtrip_check->roundtrip-tab (positional abs path, no --root), same-path serialized, different-path parallel.
- `Utinni.Mcp.Tests/McpBoundaryPathEscapeTests.cs` — 4 facts: path-escape (`../../`, `..\\..\\`, `subdir/../../`) on save_datatable + repack_tre is a hard error with ZERO spawns; repack dry-run-default no-spawn.

## Decisions Made

- **`save_*` argv = relative path + `--root`; `roundtrip-*`/`repack-tre` argv = positional absolute path.** The apply-save-* verbs require `--root` (they re-resolve `relAsset` under it as defense-in-depth), so save tools keep the path relative and add `--root root.Path`. The roundtrip/repack verbs take a positional path with no `--root`, so those tools pass the already-resolved absolute path.
- **Concurrency policy single-sourced in `SaveVerb.RunSerializedAsync`** rather than duplicated in each tool — one lock-keying contract consumed by the write + verify + repack surface.
- **Unseal + virtual over a new interface** for the dispatcher test seam — the smallest change that lets a recording stub prove the spawn count/argv without restructuring the DI contract.
- **Host-side `dry_run` gate, plan-only notice, no backup claim** — `repack-tre` has no `--dry-run`; the gate must live in the host, and the notice must not pretend a backup happened (Cursor LOW).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `CliDispatcher` was `sealed` with a non-virtual `RunAsync` — could not substitute a recording stub for the dispatch tests**
- **Found during:** Task 1
- **Issue:** The plan's Task 1 test action requires proving "save_datatable spawns EXACTLY ONE apply-save-tab invocation with the typed argv" and "zero CLI spawns on path-escape" by mocking/stubbing the dispatcher. `CliDispatcher` (from 14-01) is `sealed` and `RunAsync` is non-virtual, so it cannot be subclassed/overridden and there is no interface.
- **Fix:** Unsealed `CliDispatcher` and made `RunAsync` `virtual` (documented as a test seam; production always uses the concrete class via DI — behavior unchanged). The plan explicitly allowed "mock/stub the CliDispatcher (or point it at a stub exe)"; a recording subclass is the cleanest stub and avoids a real exe in the unit lane.
- **Files modified:** Utinni.Mcp/Server/CliDispatcher.cs
- **Verification:** Build green (0 warnings); 16 target tests + full 63-test Mcp suite pass.
- **Committed in:** d01be1a (Task 1)

---

**Total deviations:** 1 auto-fixed (1 blocking — testability seam, no production behavior change).
**Impact on plan:** No scope change. The four save tools, repack_tre, and roundtrip_check match the plan's described surface exactly; the only adjustment is enabling the dispatcher to be stubbed.

## Concurrent-Dispatch Policy (for MCP-SECURITY.md, Plan 04)

Parallel `save_*` / `repack_tre` calls are serialized per RESOLVED ABSOLUTE PATH (case-insensitive key, Windows FS semantics) via a `SemaphoreSlim`-per-path in `SaveVerb.RunSerializedAsync`. Two writers targeting the SAME asset cannot interleave an atomic overwrite; writers on DIFFERENT assets run concurrently. `roundtrip_check` participates in the same lock (a verify and a same-path commit stay consistent), though roundtrip itself never writes. This is a host-side concurrency guard layered ON TOP of the verb's own single-process apply+verify+commit (no TOCTOU within a verb) — together they prevent both intra-verb and inter-call write races.

## Issues Encountered

- `dotnet test` defaults to the full net10 suite (63 tests, 1 Slow dispatcher-timeout fact) and takes ~1 min; the targeted `--filter "SaveCompositionTests|McpBoundaryPathEscapeTests"` run is sub-second (16 tests). Both green. No `dotnet build` of `UtinniCoreDotNet` was needed (the MCP host consumes the netstandard2.0 `UtinniCoreDotNet.PathContainment` only).

## Known Stubs

None. All 4 save tools, repack_tre, and roundtrip_check are fully wired over the real CliDispatcher seam. The recording stub dispatcher exists only in the test project. (`repack_tre dry_run=true` returning `DryRunNotice` without a spawn is the DOCUMENTED plan-only behavior, not a stub.)

## Threat Flags

None. All security-relevant surface introduced (T-14-01 boundary path-escape, T-14-03 destructive repack dry_run gate, T-14-08 verify-before-commit via the verb, T-14-16 per-path serialization) is enumerated in the plan's `<threat_model>`; MCP-SECURITY.md (Plan 04) consolidates. No new endpoints/auth/schema surface beyond the modeled apply-save + repack write paths.

## Coverage Notes (Plan 04 follow-on)

- Read-back-after-save (a save then a read confirming the persisted value) + the real `repack_tre dry_run` no-write / real-run-with-backup behavior are the Plan 14-04 integration layer (`RoundTripTests.RepackDryRun`). This plan unit-proves the composition (exactly-one-spawn, typed argv, opaque exit-code decision, boundary escape, concurrency) over a stub dispatcher; 14-04 proves the end-to-end persistence over the real CLI + a real MCP client.

## User Setup Required

None — no external service configuration. (Runtime use still requires `--root`/`UTINNI_MCP_ROOT`, enforced fail-closed by Plan 01's ResolvedRoot.)

## Next Phase Readiness

- The full write surface (4 `save_*` + `repack_tre` + `roundtrip_check`) is discoverable by `WithToolsFromAssembly` and ready for the Plan 14-04 real-MCP-client round-trip + read-back-after-save proofs + MCP-SECURITY.md consolidation.
- MCP-02's write half is in place (persist-on-clean-verify, fail-closed on verify-failed, destructive repack gated off-by-default).
- No blockers.

## Self-Check: PASSED

- All 6 created files verified present.
- Both task commits (d01be1a, d6aafcc) verified in git log.

---
*Phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece*
*Completed: 2026-06-07*
