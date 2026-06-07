---
phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece
plan: 01
subsystem: api
tags: [mcp, modelcontextprotocol, net10, stdio, subprocess, path-containment, typeforwardedto, netstandard2.0, generic-host]

# Dependency graph
requires:
  - phase: 13-wrap-revived-compilers
    provides: "utinni-cli.exe JSON verbs + NativeToolRunner subprocess discipline (the analog the CliDispatcher mirrors one layer up)"
  - phase: 08-tjt-iff-editor
    provides: "UtinniCoreDotNet.Saving.LooseOverridePath root-containment helper (now single-sourced to netstandard2.0)"
provides:
  - "net10 SDK-style Utinni.Mcp host project on ModelContextProtocol 1.4.0 (generic-host bootstrap, logs to stderr, stdio MCP transport)"
  - "ResolvedRoot — fail-closed startup access-control pin (--root ?? UTINNI_MCP_ROOT) + per-call Resolve delegating to LooseOverridePath"
  - "CliDispatcher — subprocess seam with injectable timeout (default 60s), ArgumentList per-arg, async-read-both-streams, kill-on-timeout, exe-missing-without-throw"
  - "CliLocator — deterministic ABSOLUTE utinni-cli.exe resolver (override / UTINNI_CLI_PATH / AppContext.BaseDirectory probe; never CWD-relative)"
  - "ServerArgs — small tested arg parser (space + equals forms, missing-value tolerance, first-wins)"
  - "CliInvocationResult — published downstream contract for Wave-2/3 tool plans"
  - "UtinniCoreDotNet.PathContainment — netstandard2.0 single-source-of-truth containment lib consumable by both net472 and net10"
  - "[TypeForwardedTo] binary-identity shim preserving net472 plugin type resolution"
  - "net10 dotnet-test CI lane + net472 LooseOverridePath binary-forward regression gate"
affects: [14-02-read-tools, 14-03-write-save-tools, 14-04-roundtrip-security, mcp-tool-plans]

# Tech tracking
tech-stack:
  added: [ModelContextProtocol 1.4.0, Microsoft.Extensions.Hosting 10.0.0, Microsoft.Extensions.Logging.Console 10.0.0, net10.0 TFM, netstandard2.0 TFM]
  patterns:
    - "Two-process honest seam: net10 host shells the net472/x86 utinni-cli.exe; the MCP SDK is never hosted in-proc in the x86 client"
    - "Cross-TFM single-source-of-truth via [assembly: TypeForwardedTo] (NOT thin re-export) to preserve compiled-plugin binary type identity"
    - "Fail-closed access-control pin at startup before the transport opens"
    - "Injectable-timeout subprocess dispatch with async-read-both-streams (no pipe deadlock) + kill-tree on timeout"
    - "net10 projects mapped into the .sln with ActiveCfg-only (no .Build.0) so the Release|x86 MSBuild pass skips them; CI builds them on a separate dotnet lane"

key-files:
  created:
    - "Utinni.Mcp/Utinni.Mcp.csproj"
    - "Utinni.Mcp/Program.cs"
    - "Utinni.Mcp/Server/ResolvedRoot.cs"
    - "Utinni.Mcp/Server/CliDispatcher.cs"
    - "Utinni.Mcp/Server/CliLocator.cs"
    - "Utinni.Mcp/Server/ServerArgs.cs"
    - "Utinni.Mcp/Server/CliInvocationResult.cs"
    - "UtinniCoreDotNet.PathContainment/UtinniCoreDotNet.PathContainment.csproj"
    - "UtinniCoreDotNet.PathContainment/LooseOverridePath.cs"
    - "Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj"
    - "Utinni.Mcp.Tests/ResolvedRootTests.cs"
    - "Utinni.Mcp.Tests/ServerArgsTests.cs"
    - "Utinni.Mcp.Tests/CliLocatorTests.cs"
    - "Utinni.Mcp.Tests/DispatcherTests.cs"
  modified:
    - "UtinniCoreDotNet/Saving/LooseOverridePath.cs"
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj"
    - "Utinni.sln"
    - ".github/workflows/ci.yml"

key-decisions:
  - "Fork-2 ruling: single-source LooseOverridePath in netstandard2.0 + [TypeForwardedTo] (binary identity), NOT a re-export wrapper (review Consensus #5)"
  - "Dispatcher timeout is constructor-injectable (TimeSpan overload); production default 60s; tests use sub-second (Codex)"
  - "Deterministic timeout long-runner is powershell Start-Sleep, NOT %ComSpec% /c pause (terminates on closed stdin) (Codex+Cursor)"
  - "CliLocator returns an ABSOLUTE AppContext.BaseDirectory path, never the bare CWD-relative name (Codex #11)"
  - "net10 projects get ActiveCfg-only (no .Build.0) in the .sln so msbuild /p:Platform=x86 skips them (RESEARCH A3)"

patterns-established:
  - "Pattern: net10 MCP host shells net472/x86 utinni-cli via CliDispatcher (two-process seam)"
  - "Pattern: cross-TFM type sharing via [TypeForwardedTo] to a netstandard2.0 lib"
  - "Pattern: fail-closed ResolvedRoot pin + per-call Resolve as the agent access-control boundary"

requirements-completed: [MCP-01, MCP-02]

# Metrics
duration: ~35min
completed: 2026-06-07
---

# Phase 14 Plan 01: Headless Utinni.Mcp Foundation Summary

**net10 Utinni.Mcp generic-host skeleton on ModelContextProtocol 1.4.0 with a fail-closed ResolvedRoot access-control pin, an injectable-timeout CliDispatcher subprocess seam, a deterministic absolute CliLocator, and the LooseOverridePath containment helper single-sourced into a netstandard2.0 lib via [TypeForwardedTo] — all proven by 28 net10 Wave-0 unit tests plus the 18-test net472 binary-forward regression gate.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-06-07T01:20Z
- **Completed:** 2026-06-07T01:36Z
- **Tasks:** 3 (Task 0 was a pre-approved supply-chain checkpoint)
- **Files modified:** 18 (14 created, 4 modified)

## Accomplishments
- Stood up the net10 `Utinni.Mcp` console host (generic-host bootstrap, all logging routed to stderr so stdout stays clean for the MCP stdio transport; `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` — no tools yet, as expected this wave).
- Resolved the path-containment cross-TFM fork: moved `LooseOverridePath` verbatim into a new `netstandard2.0` `UtinniCoreDotNet.PathContainment` lib (consumable by both net472 and net10) and replaced the net472 location with an `[assembly: TypeForwardedTo]` shim — binary type identity preserved for already-compiled net472 plugins (re-export forbidden per Consensus #5).
- Implemented and unit-tested the four non-protocol contracts the Wave-2/3 tool plans consume: `ResolvedRoot` (fail-closed pin + SC3-delegating Resolve), `CliDispatcher` (injectable timeout, async-read-both-streams, kill-on-timeout, exe-missing-without-throw), `CliLocator` (absolute deterministic resolution), `ServerArgs` (tolerant flag parser), plus the published `CliInvocationResult`.
- Added the net10 `dotnet test` CI lane (fast, excludes the Slow-trait 60s test) + the net472 `LooseOverridePathTests` binary-forward regression gate, without routing the net10 project through the Release|x86 MSBuild pass.

## Task Commits

Each task was committed atomically:

1. **Task 1: Extract LooseOverridePath to netstandard2.0 + TypeForwardedTo shim + scaffold net10 Utinni.Mcp** - `a76ecf6` (feat)
2. **Task 2: ServerArgs + ResolvedRoot + CliLocator + CliDispatcher + Wave-0 unit tests** - `3e10bc0` (feat)
3. **Task 3: net472 binary-forward regression gate + net10 dotnet-test CI lane** - `d7f05b0` (ci)

_Task 0 was a pre-approved supply-chain legitimacy checkpoint (ModelContextProtocol 1.4.0 confirmed against nuget.org by the orchestrator); no commit._

## Files Created/Modified
- `Utinni.Mcp/Utinni.Mcp.csproj` — net10 SDK-style console project; ModelContextProtocol 1.4.0 + Microsoft.Extensions.Hosting/Logging.Console; RestorePackagesWithLockFile pin.
- `Utinni.Mcp/Program.cs` — generic-host bootstrap (stderr logging, fail-closed ResolvedRoot singleton, located CliDispatcher singleton, AddMcpServer/Stdio/ToolsFromAssembly).
- `Utinni.Mcp/Server/ResolvedRoot.cs` — fail-closed startup pin + per-call Resolve delegating to LooseOverridePath.
- `Utinni.Mcp/Server/CliDispatcher.cs` — injectable-timeout subprocess seam (default 60s); ArgumentList per-arg; stdin-close; both streams read async before WaitForExitAsync; Kill-tree + drain on timeout.
- `Utinni.Mcp/Server/CliLocator.cs` — override / UTINNI_CLI_PATH / absolute AppContext.BaseDirectory probe (never CWD-relative).
- `Utinni.Mcp/Server/ServerArgs.cs` — tolerant `--root`/`--cli-path` parser (space + equals forms, first-wins, missing-value tolerance).
- `Utinni.Mcp/Server/CliInvocationResult.cs` — published downstream result contract + factories.
- `UtinniCoreDotNet.PathContainment/{LooseOverridePath.cs, *.csproj}` — netstandard2.0 single-source-of-truth containment lib.
- `UtinniCoreDotNet/Saving/LooseOverridePath.cs` — now a `[TypeForwardedTo]` shim (no class definition).
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj` — ProjectReference to PathContainment (carries the netstandard2.0 DLL into net472 output).
- `Utinni.Mcp.Tests/{*.csproj, ServerArgsTests, ResolvedRootTests, CliLocatorTests, DispatcherTests}` — separate net10 Wave-0 suite (28 fast tests + 1 Slow-trait).
- `Utinni.sln` — added PathContainment (AnyCPU, builds in x86 pass), Utinni.Mcp + Utinni.Mcp.Tests (ActiveCfg-only, no .Build.0 — skipped by the x86 pass).
- `.github/workflows/ci.yml` — net472 regression gate + net10 build + net10 fast-test steps + on-failure artifact; Slow test documented as nightly/manual; job name updated.

## Decisions Made
- **Single-source LooseOverridePath via [TypeForwardedTo]** rather than a re-export wrapper — a wrapper class would create a distinct type and break binary identity for compiled net472 plugins (MEF compose). Verified by the 18-test net472 regression gate passing against the forwarded type.
- **Injectable dispatcher timeout** (constructor `TimeSpan` overload) — keeps the fast test suite sub-second while production defaults to 60s.
- **PowerShell `Start-Sleep` long-runner** for the timeout tests instead of `%ComSpec% /c pause` (which terminates on the dispatcher's closed stdin and would falsely pass).
- **net10 projects mapped ActiveCfg-only in the .sln** (no `.Build.0`) so `msbuild Utinni.sln /p:Platform=x86` skips them; CI builds them on the separate `dotnet` lane (RESEARCH A3 — a net10 project cannot target x86-only).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Split Program.cs wiring across Task 1 and Task 2 to keep each task's build green**
- **Found during:** Task 1
- **Issue:** The plan's Task 1 action text describes a `Program.cs` that already wires `ResolvedRoot.PinOrThrow` / `CliLocator` / `CliDispatcher`, but those classes are listed as Task 2 files. Writing the full wiring in Task 1 would make `dotnet build Utinni.Mcp` (Task 1's own acceptance gate) fail on missing types.
- **Fix:** Task 1's `Program.cs` is the host skeleton (stderr logging + AddMcpServer scan) with an explicit note that Task 2 wires the singletons; Task 2 edits `Program.cs` to add the fail-closed `ResolvedRoot` singleton + located `CliDispatcher` singleton. Both tasks build green independently.
- **Files modified:** Utinni.Mcp/Program.cs (created Task 1, edited Task 2)
- **Verification:** `dotnet build Utinni.Mcp/Utinni.Mcp.csproj` exits 0 at both Task 1 and Task 2; 28 fast tests green.
- **Committed in:** a76ecf6 (Task 1) + 3e10bc0 (Task 2)

---

**Total deviations:** 1 auto-fixed (1 blocking — task-ordering build-gate reconciliation, no scope change).
**Impact on plan:** No scope creep; the final Program.cs matches the plan's described full wiring. The split only sequences the edits so each task's own build gate passes.

## Issues Encountered
- The UtinniCoreDotNet.Tests `packages.lock.json` updated on restore to reflect the new transitive PathContainment dependency (UtinniCoreDotNet now references it). Expected side effect of the single-source move; staged with Task 1.
- The `.sln` uses tab indentation; the first config-block edit failed on a whitespace mismatch and was retried against a unique single-line anchor (no `#` comments — invalid in `.sln` ProjectConfigurationPlatforms).

## Known Stubs
None. The only intentionally-empty surface is `WithToolsFromAssembly()` finding no `[McpServerTool]` types this wave — that is expected and documented in `Program.cs`; the Wave-2/3 tool plans add the tools.

## User Setup Required
None - no external service configuration required. (Runtime use of the server requires `--root <path>` or `UTINNI_MCP_ROOT`, enforced fail-closed; that is operational config, not setup.)

## Next Phase Readiness
- The four published contracts (`ResolvedRoot`, `CliDispatcher`, `CliLocator`, `ServerArgs`) + `CliInvocationResult` are in place and unit-proven — the Wave-2/3 read/write/repack tool plans can build `[McpServerTool]` types against them.
- The net10 CI lane is green locally; first push will exercise it on the self-hosted runner.
- No blockers.

## Self-Check: PASSED

- All created files verified present (see below).
- All three task commits verified in `git log`.

---
*Phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece*
*Completed: 2026-06-07*
