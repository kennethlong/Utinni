---
phase: 16-live-injected-mcp-bridge-blender-ecosystem-boundary
plan: 03
subsystem: api
tags: [mcp, named-pipe, ipc, canonical-json, wire-protocol, cross-tfm, net10, live-bridge, fail-closed-gating]

# Dependency graph
requires:
  - phase: 16-live-injected-mcp-bridge (16-02)
    provides: CanonicalJson + LiveBridgeProtocol + LivePipeServer (net472), the four golden wire byte-vectors + pipe-name/framing fixture, ServerArgs.EnableLive
  - phase: 14-headless-mcp
    provides: Utinni.Mcp host shape, CliDispatcher (never-hang twin), ResolvedRoot/LooseOverridePath containment, RoundTripTests McpClient.ListToolsAsync precedent, MCP-SECURITY.md register
provides:
  - CanonicalJson (net10) — field-for-field re-impl of the 16-02 canonical writer/reader; deterministic byte-exact OUTPUT (R3-1)
  - LivePipeClient — named-pipe dispatch target (CliDispatcher twin; never-hang; protocolVersion-skew aware; clientRoot diagnostic; relative-on-wire)
  - LiveTools — live_ping + live_reload_asset (non-McpServerToolType; registered ONLY via WithTools<LiveTools> under --enable-live)
  - D-04 fail-closed gating PROVEN via real McpClient.ListToolsAsync enumeration (off⇒absent / on⇒present)
  - LiveBridgeIntegrationTests (net472) — REAL cross-impl wire round-trip against the real 16-02 LivePipeServer (C-02)
  - MCP-SECURITY.md Phase-16 live-tier addendum (ASVS + STRIDE T-16-20..26 + dual-flag contract + root reconciliation)
affects: [MCP-03 now complete (Tier-2 automated deliverable); Tier-4 live in-client ping is the non-gating residual per D-01]

# Tech tracking
tech-stack:
  added: [System.IO.Pipes.NamedPipeClientStream (BCL, no package)]
  patterns:
    - "net10 re-implementation of a net472 canonical wire writer (field-for-field, cross-TFM golden byte-vector anchored)"
    - "Named-pipe client as a CliDispatcher twin: short injectable timeout, virtual exchange seam, never-throw/never-hang"
    - "Fail-closed tool gating via a non-[McpServerToolType] class registered only conditionally; proven by real ListToolsAsync"

key-files:
  created:
    - Utinni.Mcp/Server/CanonicalJson.cs
    - Utinni.Mcp/Server/LivePipeClient.cs
    - Utinni.Mcp/Tools/LiveTools.cs
    - Utinni.Mcp.Tests/LivePipeProtocolTests.cs
    - Utinni.Mcp.Tests/LivePipeLoopbackTests.cs
    - UtinniCoreDotNet.Tests/Live/LiveBridgeIntegrationTests.cs
  modified:
    - Utinni.Mcp/Program.cs
    - Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj
    - .planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/MCP-SECURITY.md

key-decisions:
  - "live_* tools reach the SDK ONLY via the conditional WithTools<LiveTools>() under serverArgs.EnableLive; LiveTools is NOT [McpServerToolType] so WithToolsFromAssembly() skips it (A1 lock, proven empirically by the off-state enumeration assert)"
  - "net10 CanonicalJson is a SEPARATE hand-rolled writer/reader (cannot project-ref net472) that produces byte-identical OUTPUT, anchored to the SAME committed wire-*.json goldens both sides assert (R3-1/R3-7)"
  - "LivePipeClient sends the RELATIVE path on the wire (CUR-NEW-1); ResolvedRoot.Resolve validates at the MCP boundary FIRST, the in-client server re-resolves under its OWN pinned root"
  - "C-02 cross-impl real-server round-trip lives on the net472 side (only side that can reference the real LivePipeServer); the net10 agreement is held by the golden byte-vectors + framing fixture"
  - "LiveTools must be a non-static class (private ctor) — WithTools<T>() rejects a static type (CS0718); the tool methods stay static"

patterns-established:
  - "Cross-TFM canonical-wire agreement: a net472 writer + a net10 field-for-field re-impl, both byte-exact against shared golden vectors — drift fails a Tier-2 test, not a Tier-4 smoke"
  - "Real-McpClient enumeration as the fail-closed-gating proof (mirrors RoundTripTests.Handshake)"

requirements-completed: [MCP-03]  # MCP-03 spanned 16-02 (in-client) + 16-03 (host); the Tier-2 automated deliverable is now complete. Tier-4 live ping is the non-gating residual (D-01).

# Metrics
duration: ~15 min
completed: 2026-06-14
---

# Phase 16 Plan 03: MCP-03 Out-of-Proc Host Half Summary

**The net10 MCP host half of the live bridge: a field-for-field net10 CanonicalJson re-implementation (byte-exact to the 16-02 net472 wire via the shared golden vectors), a never-hang LivePipeClient (CliDispatcher's named-pipe twin, protocolVersion-skew aware, relative-on-wire, clientRoot diagnostic), the live_ping + live_reload_asset tools gated fail-closed on --enable-live (proven via real McpClient.ListToolsAsync), a REAL net472 cross-impl wire round-trip against the actual 16-02 LivePipeServer, and the Phase-14 MCP-SECURITY.md live-tier addendum.**

## Performance
- **Duration:** ~15 min
- **Started/Completed:** 2026-06-14
- **Tasks:** 4 (0, 1, 2, 3)
- **Files:** 9 (6 created, 3 modified)

## Accomplishments
- **Task 0 — D-04 gating lock (real enumeration):** `LiveTools` is a non-`[McpServerToolType]` class so `WithToolsFromAssembly()` skips it; the live tools reach the SDK ONLY via the conditional `WithTools<LiveTools>()` in `Program.cs` gated on `serverArgs.EnableLive`. Proven failing→passing NOW by `LivePipeProtocolTests.LiveTools_AreAbsentWithoutEnableLive_PresentWithIt`, which launches the built exe TWICE (off/on) and calls `McpClient.ListToolsAsync()` (mirrors `RoundTripTests.Handshake`). The off-state assert doubles as the explicit CDX-NEW-6 verification.
- **Task 1 — net10 CanonicalJson + LivePipeClient:** `CanonicalJson.cs` is a field-for-field re-impl of the 16-02 net472 writer/reader (fixed-order `JsonObjectWriter`, camelCase, no whitespace, invariant numbers, JSON-spec escaping; tolerant tokenizer reader). `LivePipeClient.cs` is the CliDispatcher twin: dual ctors (short 2s default), `protected virtual ExchangeAsync` (test-substitutable), 4-byte LE framing + `MaxFrameBytes` guard, all transport failures → a result object (`NotListening` / structured error), never throw/hang. Requests carry `protocolVersion` (C-12); reload path is RELATIVE (CUR-NEW-1); ping surfaces `clientRoot` (R3-3); a skewed ack surfaces a structured error (CUR-NEW-5). Duplicated `PipeName`/`ProtocolVersion`/`MaxFrameBytes`/`FramingDescriptor` consts byte-identical to 16-02.
- **Task 2 — LiveTools bodies + gated singleton:** `live_ping` (ReadOnly) maps listening/gameRunning/pid + the `clientRoot` diagnostic (no-client = honest `listening:false`, skew/malformed = in-band error). `live_reload_asset` (ReadOnly=false) calls `root.Resolve(relativePath)` FIRST (throws on escape → tool error) then sends the RELATIVE path; maps accepted/tier/queued/reloadAttempted/path/note + a candor field; the Description states queued != visible reload, transport-only until RESID-03, reloadAttempted=false (C-14). `Program.cs` registers the `LivePipeClient` singleton AND `WithTools<LiveTools>()` BOTH only under `serverArgs.EnableLive` (C-13).
- **Task 3 — golden-wire byte-equality + real cross-impl + security doc:**
  - (A) `Utinni.Mcp.Tests.csproj` links the SAME committed `pipe-name.txt` + four `wire-*.json` goldens (Link form). `LivePipeLoopbackTests`: all four goldens byte-exact via the net10 CanonicalJson (produce requests / consume acks incl. nested + clientRoot + string tier, R3-1/R3-7); name+framing fixture-equality (line 1 + line 2, C-05); loopback ping (maps clientRoot) + reload (sends RELATIVE envelope, maps ack); never-hang edge cases (no-client / oversize / partial / server-close, each within the timeout); protocolVersion skew → structured error.
  - (B) `LiveBridgeIntegrationTests` (net472, C-02): spins up the REAL 16-02 `LivePipeServer` on a random pipe + temp client root + injectable probe/enqueue sink, round-trips ping (clientRoot == pinned root) + reload (contained relative → accepted/tier/queued + exactly one enqueue) + absolute/escape → rejected + no enqueue (server-side containment over the actual wire) + same/different-root reconciliation exposing the diagnostic.
  - (C) `MCP-SECURITY.md` (Phase-14 path, C-11): a Phase-16 live-tier section — ASVS rows (V1/V4/V5/V13/V7), the STRIDE register (T-16-20..26 + T-16-AR16 + T-16-SC) each citing the proving test, the dual-flag operator contract (CUR-NEW-6), the root-reconciliation requirement (R3-3) with `live_ping clientRoot` as the verification mechanism, the AR-16-01 accepted-risk row, and updated audit-trail + sign-off.

## Task Commits
1. **Task 0: D-04 gating lock via real tool enumeration** — `13db929` (feat)
2. **Task 1: net10 CanonicalJson re-impl + LivePipeClient** — `33abf4c` (feat)
3. **Task 2: LiveTools bodies + EnableLive-gated client singleton** — `77e6087` (feat)
4. **Task 3: golden-wire byte-equality + real cross-impl wire + MCP-SECURITY live tier** — `abce1e4` (test)

**Plan metadata** (this SUMMARY + STATE/ROADMAP) committed separately as `docs(16-03): ...`.

## Tests
- `dotnet test Utinni.Mcp.Tests --filter LivePipe` — **13/13 green** (1 gating + 4 golden-wire byte-equality + 1 fixture-equality + 2 loopback round-trips + 5 edge/skew).
- `dotnet test UtinniCoreDotNet.Tests --filter LiveBridgeIntegration` (built via VS2026 MSBuild Release|x86, then `--no-build`) — **5/5 green** (ping, contained reload+enqueue, 2 absolute/escape theory cases, root reconciliation).
- `RoundTripTests.Handshake_ListsExactlyTheTwelveNamedTools` re-run — still green (12-tool surface unchanged when live is OFF; live tools are additive only under `--enable-live`).
- No CppSharp `Generated/UtinniCore.cs` churn (C#-only build; verified absent from `git status`).

## Deviations from Plan

### Adaptations (necessary, not scope changes)

**1. [Rule 3 - Blocking] `LiveTools` must be a non-static class (CS0718)**
- **Found during:** Task 0 build.
- **Issue:** `WithTools<LiveTools>()` rejected a `static class` type argument (CS0718 — static types cannot be type arguments). The plan said "static class … NOT McpServerToolType-decorated."
- **Fix:** Made `LiveTools` a plain `class` with a `private` ctor (never instantiated); the `[McpServerTool]` methods stay `static`. The D-04 intent (no `[McpServerToolType]`, scan-skipped, conditional registration only) is unchanged and proven by the off-state enumeration assert.
- **Files:** `Utinni.Mcp/Tools/LiveTools.cs`. **Committed in:** `13db929`.

**2. [Rule 3 - Blocking] net472 test lane built via VS2026 MSBuild, tested `--no-build`**
- **Found during:** Task 3 (B).
- **Issue:** `dotnet build`/`dotnet test` on `UtinniCoreDotNet.Tests` fails MSB3823 (WinForms image `.resx`) — the documented project constraint inherited from 16-02.
- **Fix:** Built `UtinniCoreDotNet.Tests.csproj` with VS2026 MSBuild (`-p:Configuration=Release -p:Platform=x86`), then `dotnet test --no-build -c Release --filter LiveBridgeIntegration`. The net10 Utinni.Mcp lanes build/test with `dotnet` normally.
- **Files:** none (build-recipe adaptation). **Committed in:** N/A.

**3. [Rule 1 - Bug] Catch-clause ordering in `LivePipeClient.ExchangeAsync`**
- **Found during:** Task 1 build (CS0160).
- **Issue:** `EndOfStreamException` / `InvalidDataException` derive from `IOException`; the `IOException` catch came first, making the subtype catches unreachable.
- **Fix:** Reordered the subtype catches before `IOException`.
- **Files:** `Utinni.Mcp/Server/LivePipeClient.cs`. **Committed in:** `33abf4c`.

**4. [Rule 1 - lint] xUnit2000 expected/actual order in the fixture-equality fact**
- **Found during:** Task 3 build warnings.
- **Fix:** Swapped to `Assert.Equal(LivePipeClient.PipeName, lines[0])` (constant as expected).
- **Files:** `Utinni.Mcp.Tests/LivePipeLoopbackTests.cs`. **Committed in:** `abce1e4`.

### Plan-file-list note
The plan listed `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` as a Task-3 modified file (to link the fixtures). 16-02 ALREADY linked the `live/*` fixtures into that csproj (lines 41-47), so NO csproj change was needed there — the `**/*.cs` glob auto-includes the new `Live/LiveBridgeIntegrationTests.cs`. Not a deviation, just an already-satisfied prerequisite.

**Total deviations:** 4 (2 blocking-build adaptations, 1 catch-order bug, 1 lint). All necessary; none change scope. The wire contract, gate, tools, cross-impl proof, and security doc match the plan exactly.

## Known Stubs
None. The Task-0 `LiveTools` placeholders were fully replaced in Task 2 (real `pipe.PingAsync`/`ReloadAssetAsync` dispatch + clientRoot diagnostic + root reconciliation + C-14 candor). The only intentional best-effort placeholder is the in-client server's enqueued reload Action (16-02, RESID-03) — out of scope here and honestly surfaced as `reloadAttempted=false`.

## Threat Flags
None. The live-tier surface introduced here (named-pipe client, two gated tools) is fully modeled in the MCP-SECURITY.md Phase-16 addendum (T-16-20..26 + T-16-AR16 + T-16-SC); no new endpoints / auth paths / schema changes beyond the modeled bridge.

## Next Phase Readiness
- **MCP-03 Tier-2 automated deliverable COMPLETE** across 16-02 (in-client) + 16-03 (host): agent-facing tools, the never-hang pipe client, the fail-closed gate (real-enumeration proven), the cross-plan name/framing/golden-wire agreement across the TFM wall, the real cross-impl wire round-trip, the root-reconciliation diagnostic, and the loopback proof.
- **Tier-4 manual residual (D-01, non-gating):** live in-client confirmation (launch with BOTH `[Live] enableLiveBridge` ON AND `--enable-live`; `live_ping` then `live_reload_asset`; compare `--root` vs the ping `clientRoot`). Documented in MCP-SECURITY.md + 16-VALIDATION.md.
- **Remaining in Phase 16:** ECO-01 (Blender file-format boundary) + the 16-01 Task 4 cross-repo pointer human checkpoint.

## Self-Check: PASSED

All 6 created source files + the SUMMARY verified present on disk; all 4 task commits (13db929, 33abf4c, 77e6087, abce1e4) verified in git log. Tests green: net10 LivePipe 13/13, net472 LiveBridgeIntegration 5/5, RoundTripTests handshake unchanged. No CppSharp UtinniCore.cs churn committed.
