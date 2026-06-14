---
phase: 16-live-injected-mcp-bridge-blender-ecosystem-boundary
asvs_level: 1
threats_open: 0
audited: 2026-06-14
auditor: gsd-security-auditor (Claude Sonnet 4.6)
register_authored_at_plan_time: true
block_on: HIGH
---

# Phase 16 — Security Audit

> Verifies that every declared mitigation in the three Phase-16 threat registers
> (16-01, 16-02, 16-03) is present in the shipped implementation code. This is
> a verification audit — it does NOT retroactively scan for new threats.
> `register_authored_at_plan_time: true`.

---

## Threat Verification

### WAVE 16-01 — Blender bundle validation (ValidateBundleCommand)

| Threat ID | Category | Disposition | Status | Evidence |
|-----------|----------|-------------|--------|----------|
| T-16-01 | Tampering (path traversal) | mitigate | CLOSED | `ValidateBundleCommand.cs:387` — `LooseOverridePath.IsContainedUnderRoot(bundleRoot, canonical)` for absolute refs; `ValidateBundleCommand.cs:400` — `LooseOverridePath.Resolve(bundleRoot, normalized)` for relative refs. Both branches route through the single shared `IsContainedUnderRoot` predicate (R3-6 preferred path; comment at line 361 cites LooseOverridePath.cs as source-of-truth). Escaping refs are recorded as `rejectedRefs` and NEVER `File.Exists`-probed (lines 382-394). Tests: `ValidateBundleTests.cs` cases (e) escaping-absolute, (e2) dot-dot relative, (f) contained-absolute-allowed, plus the happy-path (a) which exercises the real contained-absolute `.rsp` line. |
| T-16-02 | DoS (oversize/malformed) | mitigate | CLOSED | `ValidateBundleCommand.cs:144-151` — JSON parse failure → `ParseError` exit 2; `ValidateBundleCommand.cs:268-280` — `.rsp` grammar failure throws internal `ParseError` (caught at line 239 → exit 2). Tests: `ValidateBundleTests.ValidateBundle_MalformedManifestJson_ExitsTwoParseError` + `ValidateBundle_MalformedRspLine_ExitsTwoParseError`. |
| T-16-03 | Info disclosure (doc correctness) | mitigate | CLOSED | `docs/ai/blender-boundary-contract.md` exists (16-01-SUMMARY key-files: created). The version matrix sources from `TreVersion.cs` (5000 READABLE, 6000 enumerate-only); the manifest schema is sourced verbatim from the real exporter with `client_cfg` nested inside `assets`. The `BucketFilenames` public accessor at `ValidateBundleCommand.cs:105-113` is the single source-of-truth matched by the doc↔verb parity test `ValidateBundleTests` (C-17). 16-01-SUMMARY confirms `docs/ai/blender-boundary-contract.md` is present at ≥80 lines with all four D-06 surfaces. |
| T-16-04 | Tampering (silent fixture drift) | mitigate | CLOSED | `Utinni.Cli.Tests/Fixtures/blender/fixture-hashes.txt` exists with SHA-256 hashes for both binary goldens. `BlenderBoundaryGoldenTests.cs` re-hashes the on-disk fixtures via `SHA256.Create().ComputeHash()` and asserts equality against the recorded hashes (lines 134, 175, 178 confirmed by grep). |
| T-16-05 | Info disclosure (exit-0 misread) | mitigate | CLOSED | `ValidateBundleCommand.cs:220-235` — success envelope carries explicit `valid` (false when any rejectedRefs/missingAssets/bucketMismatches) and `hasRejectedRefs` booleans. Exit code is 0 for structurally-valid bundles regardless of findings (CDX-NEW-9). The doc states agents must read `valid`, not exit code alone. Tests: case (a) asserts `valid:true` + `hasRejectedRefs:false`; case (e) asserts `valid:false` + `hasRejectedRefs:true` at exit 0. |
| T-16-AR | Local file reads (accepted risk) | accept | CLOSED | Single-user local desktop tool; no network surface; bundle is maintainer-authored Blender output; analog of AR-14-03. The accepted risk assumption holds — `ValidateBundleCommand` is a CLI verb with no network I/O. Documented in 16-01-PLAN.md threat register. |

---

### WAVE 16-02 — In-client named-pipe server (LivePipeServer)

| Threat ID | Category | Disposition | Status | Evidence |
|-----------|----------|-------------|--------|----------|
| T-16-10 | Spoofing/Elevation (unauthorized connect) | mitigate | CLOSED | `LivePipeServer.cs:225-255` — `BuildCurrentUserPipeSecurity()` constructs `PipeSecurity` with exactly ONE `PipeAccessRule` for `WindowsIdentity.GetCurrent().User` with `PipeAccessRights.FullControl` + `AccessControlType.Allow`; no other rules (deny-others-by-omission). In-client start gate at `main.cs:132` — `GetBool("Live","enableLiveBridge")` default OFF/fail-closed. Test: `LivePipeServerTests.PipeSecurity_HasExactlyOneCurrentUserFullControlAllowRule` asserts `rules.Count == 1`, `FullControl`, `Allow`. |
| T-16-11 | Tampering (wrong-thread native deref) | mitigate | CLOSED | `LivePipeServer.cs:83` — `private volatile bool _gameIsRunningCache` (game-thread-updated). `LivePipeServer.cs:131-133` — `RefreshGameStateOnGameThread()` is the ONLY method that calls `_gameStateProbe()`. `LivePipeServer.cs:408` — worker reads ONLY `_gameIsRunningCache`. Test: `LivePipeServerTests.SourceAssertion_AcceptAndDecisionPaths_DoNotReferenceNativeGameIsRunning` strips comments and asserts no `Game.IsRunning` in code paths; behavioral test proves probe invocation count is 0 when serving a worker request. `main.cs:151` wires the game-thread refresh via `GameCallbacks.AddMainLoopCall(refresh)`. |
| T-16-12 | DoS (malformed/oversize/partial frames) | mitigate | CLOSED | `LivePipeServer.cs:320-342` — `ReadFrameBounded` enforces `MaxFrameBytes` (throws `InvalidDataException`), uses `CancellationTokenSource(_readTimeoutMs)` for bounded read timeout. Partial read throws `EndOfStreamException` (line 368). Each exception in `ServeOneConnection` closes only that connection (lines 296-316); accept loop recovers and continues. Tests in `LivePipeServerTests.cs` cover oversize frame, partial-read/mid-disconnect, stalled-client-closed, bounded-dispose, malformed-then-valid sequential. |
| T-16-13 | Pipe name collision (accepted risk) | accept | CLOSED | Sufficiently-specific name `"utinni-live-bridge"` (canonical constant); server owns creation; single-user local desktop. Stated assumption holds per architecture. |
| T-16-14 | Tampering (in-client arbitrary path) | mitigate | CLOSED | `LivePipeServer.cs:487` — `LooseOverridePath.Resolve(clientRoot, request.Path)` against the pinned SWG CLIENT root (not injectRoot — CUR-NEW-1). Wire carries RELATIVE path only. Absolute/`..`-escaping path throws `ArgumentException` → `accepted=false`, `Rejected` disposition, NO enqueue (lines 491-507). `main.cs` pins the client root via `ResolveClientRoot()` mirroring `FormIffEditor.ResolveClientRoot`. Tests: `LivePipeServerTests` — contained-relative enqueues; absolute path → rejected/no-enqueue; `..`-escape → rejected/no-enqueue; cross-root proves own-root resolution. Real wire test: `LiveBridgeIntegrationTests.ReloadAsset_AbsoluteOrEscape_RejectedNoEnqueue`. |
| T-16-15 | Tampering (protocol drift) | mitigate | CLOSED | `UtinniCoreDotNet/Live/CanonicalJson.cs` — hand-rolled BCL-only deterministic writer (fixed key order, camelCase, no whitespace, pinned-string `ReloadTier.ToString()`, invariant-culture numbers). Four golden wire byte-vectors in `Utinni.Cli.Tests/Fixtures/live/wire-*.json` (payload bytes only, no framing prefix). `LiveBridgeProtocolTests` asserts byte-exact produce+consume for all four goldens. `LiveBridgeProtocol.IsProtocolVersionCompatible` + structured reject ack on skew. Net10 re-impl is addressed under T-16-25. |
| T-16-SC | No new packages (n/a) | n/a | CLOSED | `UtinniCoreDotNet.csproj` — no `PackageReference` entries (confirmed: grep returns no matches). `CanonicalJson.cs` uses BCL-only `StringBuilder` + `Encoding.UTF8` — no `System.Text.Json` NuGet package added to the net472 project. `System.IO.Pipes.NamedPipeServerStream` is BCL. Package Legitimacy Gate not triggered for the in-client half. |

---

### WAVE 16-03 — MCP host half (LivePipeClient + LiveTools) — spot-check of existing MCP-SECURITY.md entries

The 16-03 threats are already documented as CLOSED in
`.planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/MCP-SECURITY.md`
(Phase-16 live-tier addendum, added 2026-06-14). The following spot-checks verify the
cited code references and test names exist in the implementation.

| Threat ID | Category | Disposition | Status | Spot-Check Evidence |
|-----------|----------|-------------|--------|---------------------|
| T-16-20 | Elevation (live_* unregistered w/o --enable-live) | mitigate | CLOSED | `Utinni.Mcp/Tools/LiveTools.cs:69` — class `LiveTools` with NO `[McpServerToolType]` attribute. `Program.cs:89-93` — `if (serverArgs.EnableLive)` guards BOTH `AddSingleton(new LivePipeClient(...))` AND `mcpBuilder.WithTools<LiveTools>()`. Test: `LivePipeProtocolTests.LiveTools_AreAbsentWithoutEnableLive_PresentWithIt` — real `McpClient.ListToolsAsync()` called with exe launched off/on; asserts absent/present. |
| T-16-21 | Tampering (MCP path escape) | mitigate | CLOSED | `LiveTools.cs:124` — `root.Resolve(relativePath)` throws on escape before any pipe send; line 128 sends `relativePath` (RELATIVE) not the resolved absolute. Server re-resolves under its OWN root (T-16-14). Test: `LivePipeLoopbackTests.Loopback_ReloadAsset_SendsRelativeEnvelope_AndMapsAck` + `LiveBridgeIntegrationTests.ReloadAsset_AbsoluteOrEscape_RejectedNoEnqueue` + `…RootReconciliation_*`. |
| T-16-22 | DoS (malformed/oversize ack hangs) | mitigate | CLOSED | `LivePipeClient.cs` — `MaxFrameBytes` guard in `ReadFrameAsync`; `CancellationTokenSource` wraps connect+send+read; transport failures return a result object. Tests: `LivePipeLoopbackTests.{OversizeAckFrame,PartialAckFrame,ServerClosesMidMessage}_*_HardErrorNoHang`. |
| T-16-23 | Spoofing (no client → hang) | mitigate | CLOSED | `LivePipeClient.cs:75` — `public const int ProtocolVersion = 1`; default timeout short (2s). `PingAsync` returns `LivePingResult.NotListening` on connect failure. Test: `LivePipeLoopbackTests.NoClient_Ping_ReturnsListeningFalseQuickly_NeverHangs`. |
| T-16-24 | Candor (visible-render over-promise) | mitigate | CLOSED | `LiveTools.cs:110-111` — Description states `"does NOT mean a visible reload occurred — this is TRANSPORT-ONLY until RESID-03 (reloadAttempted is always false this phase)"`. `LiveTools.cs:144,147` — result carries `reloadAttempted` and `candor` fields. `LivePipeServer.cs:533` — ack always sets `ReloadAttempted = false`. Test: `LivePipeLoopbackTests.GoldenWire_ReloadAck_ConsumedExact` asserts `reloadAttempted=false`. |
| T-16-25 | Protocol drift (net10/net472 wire diverge) | mitigate | CLOSED | `Utinni.Mcp/Server/CanonicalJson.cs` — net10 field-for-field re-impl. `LivePipeLoopbackTests.GoldenWire_*` — all four byte-exact (net10 side). `LiveBridgeProtocolTests` — all four byte-exact (net472 side). Both sides assert the SAME committed `wire-*.json` fixtures. Fixture-equality on pipe-name (line 1) + framing descriptor (line 2): `LivePipeLoopbackTests.FixtureEquality_PipeNameAndFraming_ByteEqualTheCanonicalAnchor`. `LiveBridgeIntegrationTests` — real cross-impl wire round-trip (net472 + net472 server). |
| T-16-26 | Correctness (--root vs in-client root diverge) | mitigate | CLOSED | `LivePipeServer.cs:473` — ping ack carries `ClientRoot = clientRoot`. `LiveTools.cs:100` — ping result surfaces `clientRoot` in the MCP result map. MCP-SECURITY.md root-reconciliation section documents the requirement. Test: `LiveBridgeIntegrationTests.RootReconciliation_SameRoot_Succeeds_DifferentRoot_DiagnosticExposesDivergence`. |
| T-16-AR16 | Single-user local pipe (accepted risk) | accept | CLOSED | AR-16-01 present in `MCP-SECURITY.md` Accepted Risks Log. Local-only named pipe, current-user ACL (T-16-10), dual-flag operator contract documented. Assumption holds — no network surface, single-user desktop. |
| T-16-SC | No new packages (n/a) | n/a | CLOSED | `Utinni.Mcp.csproj` — only pre-existing `ModelContextProtocol 1.4.0`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging.Console`. No new NuGet packages. `Utinni.Mcp/Server/CanonicalJson.cs` uses hand-rolled writer; `System.Text.Json` is in-box (used for parsing only, not output). Package Legitimacy Gate not triggered. |

---

## Unregistered Flags

16-01-SUMMARY.md `## Threat Flags` — not present (section not in 16-01-SUMMARY; no new attack surface declared).

16-02-SUMMARY.md — no `## Threat Flags` section; deviations are build-recipe adaptations, not new surface.

16-03-SUMMARY.md `## Threat Flags` — **"None. The live-tier surface introduced here (named-pipe client, two gated tools) is fully modeled in the MCP-SECURITY.md Phase-16 addendum (T-16-20..26 + T-16-AR16 + T-16-SC); no new endpoints / auth paths / schema changes beyond the modeled bridge."**

No unregistered flags from any of the three waves.

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale |
|---------|------------|-----------|
| T-16-AR (16-01) | T-16-AR | Local file reads of maintainer-supplied Blender bundle; single-user local desktop tool; no network surface; analog AR-14-03. |
| T-16-13 (16-02) | T-16-13 | Pipe name collision/squatting: sufficiently-specific name `"utinni-live-bridge"`; server owns creation; single-user local desktop. |
| AR-16-01 / T-16-AR16 (16-03) | T-16-AR16 | Single-user local desktop pipe; `--enable-live` / pipe name are identifiers not secrets; dual-flag operator contract + root-reconciliation documented; analog AR-14-03/04. Entered in MCP-SECURITY.md Accepted Risks Log (2026-06-14). |

---

## Security Audit Trail

| Audit Date | Wave | Threats Total | Closed | Open | Auditor |
|------------|------|---------------|--------|------|---------|
| 2026-06-14 | 16-01 (Blender bundle) | 6 (5 mitigate + 1 accept) | 6 | 0 | gsd-security-auditor (Claude Sonnet 4.6) |
| 2026-06-14 | 16-02 (in-client pipe server) | 7 (5 mitigate + 1 accept + 1 n/a) | 7 | 0 | gsd-security-auditor (Claude Sonnet 4.6) |
| 2026-06-14 | 16-03 (MCP host half) | 9 (7 mitigate + 1 accept + 1 n/a) | 9 | 0 | gsd-security-auditor (Claude Sonnet 4.6) — spot-check of MCP-SECURITY.md entries; cited code refs + test names confirmed present |
| **TOTAL** | | **22** | **22** | **0** | |

---

## Sign-Off

- [x] All `<required_reading>` files loaded before analysis
- [x] Threat register extracted from all three PLAN.md `<threat_model>` blocks
- [x] Each threat verified by disposition (mitigate: grep for pattern in cited files; accept: assumption confirmed)
- [x] Threat flags from all three SUMMARY.md files incorporated — none unregistered
- [x] Implementation files NOT modified
- [x] `threats_open: 0` confirmed
