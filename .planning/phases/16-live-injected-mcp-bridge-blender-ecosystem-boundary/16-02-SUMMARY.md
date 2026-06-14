---
phase: 16-live-injected-mcp-bridge-blender-ecosystem-boundary
plan: 02
subsystem: api
tags: [mcp, named-pipe, ipc, canonical-json, wire-protocol, game-thread-marshal, net472, injected-client]

# Dependency graph
requires:
  - phase: 14-headless-mcp
    provides: ServerArgs tolerant flag parser; ResolvedRoot/LooseOverridePath single-source containment; net10 Utinni.Mcp host shape
  - phase: 08-tjt-iff-editor
    provides: LooseOverridePath.Resolve (shared client-root containment), ReloadAssetClassifier (4-tier honest reload disposition), LivePatchValidator (gameIsRunning-as-scalar purity precedent)
  - phase: 03-callbacks
    provides: GameCallbacks.AddMainLoopCall (hot-path-safe game-thread marshal seam)
provides:
  - CanonicalJson — Utinni-owned hand-rolled deterministic JSON writer/reader (BCL-only, no STJ package — C-03)
  - LiveBridgeProtocol — shared pipe name + protocol version + envelope DTOs + 4-byte LE framing + skew helper
  - Four golden wire byte-vectors (ping/reload request+ack) asserted byte-exact produce+consume on net472 (R3-1)
  - LivePipeServer — in-client named-pipe server (bounded accept loop + dispose, exact current-user ACL, client-root containment, game-thread-cached game-state, protocolVersion skew, marshal, D-01 candor)
  - ServerArgs.EnableLive — fail-closed --enable-live flag + UTINNI_MCP_ENABLE_LIVE env alias (explicit-truthy)
  - main.cs StartListener wiring — client-root-pinned, game-thread cache-refresh, in-client-flag gated
affects: [16-03 MCP host half (LivePipeClient re-implements CanonicalJson field-for-field + asserts the same goldens; live_* tools gate on EnableLive), MCP-SECURITY.md live-tier addendum]

# Tech tracking
tech-stack:
  added: [System.IO.Pipes.NamedPipeServerStream (BCL, no package), System.Security.AccessControl PipeSecurity]
  patterns:
    - "Utinni-owned canonical JSON writer for deterministic byte-exact wire (fixed key order via explicit emission, NOT reflection)"
    - "Cross-TFM agreement anchors: committed golden byte-vector fixtures both TFMs assert against independently"
    - "Game-thread-confined native read: injected probe Func invoked ONLY in a game-thread refresh; worker reads a volatile cache"
    - "Comment-stripped source grep-gate for semantic invariants (15-05 NoDeviceResetTests precedent)"

key-files:
  created:
    - UtinniCoreDotNet/Live/CanonicalJson.cs
    - UtinniCoreDotNet/Live/LiveBridgeProtocol.cs
    - UtinniCoreDotNet/Live/LivePipeServer.cs
    - UtinniCoreDotNet.Tests/Live/LiveBridgeProtocolTests.cs
    - UtinniCoreDotNet.Tests/Live/LivePipeServerTests.cs
    - Utinni.Cli.Tests/Fixtures/live/pipe-name.txt
    - Utinni.Cli.Tests/Fixtures/live/wire-ping-request.json
    - Utinni.Cli.Tests/Fixtures/live/wire-ping-ack.json
    - Utinni.Cli.Tests/Fixtures/live/wire-reload-request.json
    - Utinni.Cli.Tests/Fixtures/live/wire-reload-ack.json
  modified:
    - UtinniCoreDotNet/main.cs
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj
    - UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj
    - Utinni.Mcp/Server/ServerArgs.cs
    - Utinni.Mcp.Tests/ServerArgsTests.cs
    - .gitattributes

key-decisions:
  - "Live-wire OUTPUT bytes produced by a Utinni-owned hand-rolled CanonicalJson (BCL-only, no System.Text.Json package — C-03 honesty preserved); determinism from fixed field emission order, not serializer reflection (R3-1)"
  - "All four golden wire byte-vectors asserted byte-EXACT on net472 (produce + consume) incl. nested acks — no key-order-independent fallback (R3-7)"
  - "gameIsRunning is a game-thread-updated volatile cache; the injected probe (native Game.IsRunning) is invoked ONLY by RefreshGameStateOnGameThread; the pipe worker reads the cache and never the probe (R3-2)"
  - "Server reload path contained against the pinned SWG CLIENT root via the shared LooseOverridePath.Resolve — NOT injectRoot (CUR-NEW-1)"
  - "StartListener wired into main.cs post-Callbacks/pre-Application.Run, gated on [Live] enableLiveBridge (default OFF), failure-isolated (C-01/C-06)"

patterns-established:
  - "Canonical wire writer with cross-TFM golden byte-vector anchors (16-03 net10 re-implements field-for-field, asserts same fixtures)"
  - "Bounded named-pipe server lifecycle: idempotent StartListener (Interlocked), bounded read/connect timeout, bounded dispose via WaitForConnectionAsync + linked CTS"
  - "Source-assertion tests (comment-stripped grep-gate) to prove the native game-state read is structurally confined off the worker thread"

requirements-completed: []  # MCP-03 spans 16-02 (in-client half) + 16-03 (host half); NOT complete until 16-03 lands.

# Metrics
duration: ~60 min
completed: 2026-06-14
---

# Phase 16 Plan 02: MCP-03 In-Client Live Bridge Summary

**In-client named-pipe live bridge: a deterministic Utinni-owned CanonicalJson wire (four byte-exact golden vectors), a bounded-lifecycle LivePipeServer with client-root containment + a game-thread-confined game-state cache + honest ReloadAssetClassifier ack candor, the ServerArgs.EnableLive fail-closed flag, and the main.cs StartListener wiring that makes it real.**

## Performance

- **Duration:** ~60 min
- **Started:** 2026-06-14
- **Completed:** 2026-06-14
- **Tasks:** 4 (1a, 1b, 2, 3)
- **Files modified:** 16 (10 created, 6 modified)

## Accomplishments
- **Deterministic wire contract (Task 1a):** `CanonicalJson` (hand-rolled, BCL-only writer/reader — no System.Text.Json package, C-03) + `LiveBridgeProtocol` (pipe name, protocol version, envelope DTOs, 4-byte LE framing, skew helper) + the canonical pipe-name/framing fixture (line 1 name, line 2 framing+json-policy+tier-encoding) + four golden wire byte-vectors asserted byte-EXACT produce+consume (R3-1/R3-7). 19 facts.
- **In-client pipe server (Task 1b):** `LivePipeServer` — background-thread `NamedPipeServerStream` accept loop with idempotent StartListener + bounded dispose + bounded read timeout (CDX-NEW-7/8), exact current-user `PipeSecurity` ACL (CDX-NEW-4), client-root containment via the shared `LooseOverridePath.Resolve` (C-04/CUR-NEW-1), a game-thread-confined `_gameIsRunningCache` the worker reads but never natively probes (R3-2), protocolVersion-skew reject (CUR-NEW-5), one-Action marshal via `GameCallbacks.AddMainLoopCall`, and D-01 `reloadAttempted=false` candor (C-14). 21 facts (incl. the Task-3 main.cs source-assertions).
- **`--enable-live` flag (Task 2):** `ServerArgs.EnableLive` fail-closed-by-absence + `UTINNI_MCP_ENABLE_LIVE` explicit-truthy env alias; XML doc states the CUR-NEW-6 dual-flag operator contract. 23 ServerArgs facts (12 new).
- **main.cs wiring (Task 3):** `StartListener` wired post-Callbacks / pre-`Application.Run`, pinned to the resolved SWG CLIENT root (mirror `ResolveClientRoot`, NOT injectRoot), game-thread cache refresh wired via a self-re-enqueuing `AddMainLoopCall`, gated on `[Live] enableLiveBridge` (default OFF), failure-isolated in try/catch.

## Task Commits

1. **Task 1a: wire contract (CanonicalJson + LiveBridgeProtocol + fixtures + goldens + tests)** — `fe7e7cc` (feat)
2. **Task 1b: LivePipeServer (bounded lifecycle + containment + cached game-state + tests)** — `75d7c06` (feat)
3. **Task 2: ServerArgs.EnableLive flag + extended ServerArgsTests** — `8188cad` (feat)
4. **Task 3: wire LivePipeServer.StartListener into main.cs + source-assertions** — `7ffce31` (feat)

**Plan metadata:** (this SUMMARY + STATE/ROADMAP) committed separately as `docs(16-02): ...`

## Files Created/Modified
- `UtinniCoreDotNet/Live/CanonicalJson.cs` — hand-rolled deterministic JSON writer (`JsonObjectWriter` fixed-order builder) + tolerant tokenizer reader; the single OUTPUT chokepoint (R3-1)
- `UtinniCoreDotNet/Live/LiveBridgeProtocol.cs` — PipeName/ProtocolVersion/MaxFrameBytes/FramingDescriptor consts, request/ack DTOs (ping result carries clientRoot — R3-3), Serialize*/ParseRequest via CanonicalJson, WriteFrame/ReadFrame (4-byte LE + MaxFrameBytes guard), IsProtocolVersionCompatible
- `UtinniCoreDotNet/Live/LivePipeServer.cs` — the in-client server; pure static `Decide()` + `HandleRequestBytes()` + `RefreshGameStateOnGameThread()` + bounded accept/serve loop + `BuildCurrentUserPipeSecurity()`
- `UtinniCoreDotNet.Tests/Live/LiveBridgeProtocolTests.cs` — 19 facts (goldens, order-stability, camelCase/string-tier, clientRoot, skew, fixture-equality, framing)
- `UtinniCoreDotNet.Tests/Live/LivePipeServerTests.cs` — 21 facts (decision/tier/enqueue, R3-2 source+behavioral, containment, clientRoot diagnostic, skew, ACL, bounded lifecycle, loopback ping, malformed-then-valid, stalled-client-closed, main.cs wiring)
- `Utinni.Cli.Tests/Fixtures/live/{pipe-name.txt, wire-*.json}` — canonical agreement anchors (payload bytes only, no framing prefix — R3-6); pinned eol=lf
- `UtinniCoreDotNet/main.cs` — `Startup.EntryPoint` StartListener wiring + `ResolveClientRoot()` helper
- `Utinni.Mcp/Server/ServerArgs.cs` — `EnableLive` parse + dual-flag XML doc
- `*.csproj` — Live/*.cs compile entries; fixture link entries (Tests project)
- `.gitattributes` — pin `pipe-name.txt` to eol=lf

## Decisions Made
See `key-decisions` frontmatter. Highlights: hand-rolled CanonicalJson (no STJ, C-03); byte-exact-as-sole-contract for all four goldens incl. nested acks (R3-7); game-thread-confined native game-state read with worker reading a volatile cache (R3-2); client-root (not injectRoot) containment (CUR-NEW-1).

## Deviations from Plan

### Adaptations (necessary for the platform, not scope changes)

**1. [Rule 3 - Blocking] Build via VS2026 MSBuild then `dotnet test --no-build` for the net472 lane**
- **Found during:** Tasks 1a/1b/3 (UtinniCoreDotNet.Tests)
- **Issue:** The plan's `<verify>` says `dotnet test UtinniCoreDotNet.Tests --filter ...`, but `dotnet build`/`dotnet test` (which builds) fails MSB3823 on the net472 WinForms image `.resx` (documented project constraint).
- **Fix:** Built `UtinniCoreDotNet.Tests.csproj` with VS2026 MSBuild (`-p:Configuration=Release -p:Platform=x86`), then ran `dotnet test --no-build -c Release --filter ...`. The Utinni.Mcp.Tests net10 lane (Task 2) builds with `dotnet` normally.
- **Verification:** All filters green — LiveBridgeProtocol 19, LivePipeServer 21, ServerArgs 23; full UtinniCoreDotNet.Tests 758/758.
- **Committed in:** N/A (build-recipe adaptation; no source change)

**2. [Rule 2 - Correctness] Comment-stripped source grep-gate for the R3-2 source-assertion**
- **Found during:** Task 1b (R3-2 source-assertion test)
- **Issue:** The server's documentation comments legitimately mention `Game.IsRunning` (e.g. "the native read is confined to the game thread"). A blanket `DoesNotContain("Game.IsRunning", source)` over the raw file would fail on the docs, not the code.
- **Fix:** The source-assertion strips `//` and `/* */` comments first (mirroring the 15-05 `NoDeviceResetTests` comment-stripped grep-gate), then asserts no `Game.IsRunning` in CODE, plus an anti-trivial self-check (the cache field declaration survives stripping). This is the faithful, non-brittle form of the acceptance criterion ("no production accept/read-thread path contains Game.IsRunning").
- **Files modified:** UtinniCoreDotNet.Tests/Live/LivePipeServerTests.cs
- **Verification:** `SourceAssertion_AcceptAndDecisionPaths_DoNotReferenceNativeGameIsRunning` passes; the probe is invoked in exactly one CODE location.
- **Committed in:** `75d7c06`

**3. [Rule 3 - Blocking] Pin `pipe-name.txt` to eol=lf in .gitattributes**
- **Found during:** Task 1a (committing the fixture)
- **Issue:** The `*.json` fixtures were already eol=lf, but `pipe-name.txt` (2 lines with an embedded `\n`) was not covered; autocrlf would rewrite its line ending on checkout, making the cross-checkout bytes non-deterministic.
- **Fix:** Added `Utinni.Cli.Tests/Fixtures/live/pipe-name.txt text eol=lf` to `.gitattributes` (the test already tolerates CRLF via `Replace("\r\n","\n")`, but pinning makes the canonical anchor byte-stable).
- **Files modified:** .gitattributes
- **Verification:** `git show HEAD:.../pipe-name.txt | xxd` confirms the committed 2-line bytes; fixture-equality test green.
- **Committed in:** `fe7e7cc`

---

**Total deviations:** 3 (1 build-recipe adaptation, 1 correctness-faithful test form, 1 line-ending pin)
**Impact on plan:** All three are necessary platform/correctness adaptations that strengthen the acceptance, not scope creep. The wire contract, server semantics, flag, and wiring match the plan exactly.

## Issues Encountered
- Git Bash strips a leading `/` from `/p:` MSBuild switches into a path, triggering MSB1008. Resolved by using `-p:` switch form. (Tooling quirk, not a code issue.)

## User Setup Required

**The live bridge requires a TWO-FLAG operator contract (CUR-NEW-6) — both flags, neither alone:**
1. **In-client listener:** set `[Live] enableLiveBridge=true` in the in-client config (`utinni.GetConfig().GetBool("Live","enableLiveBridge")`, default OFF) so `LivePipeServer.StartListener` actually runs inside the injected client.
2. **MCP tool tier (16-03):** launch `Utinni.Mcp` with `--enable-live` (or `UTINNI_MCP_ENABLE_LIVE=1`, default OFF) so the `live_*` tools are advertised + the `LivePipeClient` is registered.

`--enable-live` alone advertises the tools but the listener never started → `live_ping` returns `listening:false`; the in-client flag alone starts the listener but the agent never sees the tools. (To be documented in MCP-SECURITY.md live-tier section + 16-VALIDATION.md Tier-4 checklist in 16-03.)

## Next Phase Readiness
- **Ready for 16-03 (MCP host half):** the wire contract is stable and byte-exact-proven. 16-03 re-implements `CanonicalJson` field-for-field in net10 (cannot project-ref net472) and asserts against the SAME committed `Utinni.Cli.Tests/Fixtures/live/wire-*.json` goldens + the `pipe-name.txt` framing fixture — the cross-TFM agreement anchors. `ServerArgs.EnableLive` is ready for the conditional `WithTools<LiveTools>()` gate.
- **Tier-4 manual residual (D-01, non-gating):** live in-client confirmation that `live_ping` returns `listening:true` over a real injected client is the Tier-4 manual residual; the GATED deliverable here is the wired + client-root-pinned + game-thread-refresh-wired + idempotent listener start, all code-asserted.
- **MCP-03 NOT yet complete:** requires 16-03 (host half) before the requirement can be marked done.

## Self-Check: PASSED

All 11 created files verified present on disk; all 4 task commits (fe7e7cc, 75d7c06, 8188cad, 7ffce31) verified in git log. Tests green: LiveBridgeProtocol 19, LivePipeServer 21, ServerArgs 23; full UtinniCoreDotNet.Tests 758/758 with zero regression. No CppSharp UtinniCore.cs regen churn committed.

---
*Phase: 16-live-injected-mcp-bridge-blender-ecosystem-boundary*
*Completed: 2026-06-14*
