---
phase: 16
slug: live-injected-mcp-bridge-blender-ecosystem-boundary
status: verified
threats_open: 0
asvs_level: 1
created: 2026-06-14
---

# Phase 16 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Phase 16 spans three waves: **16-01** Blender bundle validation (`ValidateBundleCommand`,
> read-only text validator), **16-02** in-client named-pipe server (`LivePipeServer`), and
> **16-03** MCP host half (`LivePipeClient` + `LiveTools`). The 16-03 host-half threats are also
> documented in the product doc `Utinni.Mcp/MCP-SECURITY.md` (Phase-16 Live-Tier Addendum, T-16-20..26).
> **Gate verdict: ASVS L1; no HIGH-severity threats; security gate passes.** All dispositions are
> mitigate / accept / n/a.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| disk → validate-bundle (16-01) | Blender-written bundle files (manifest / `.rsp` / `.cfg`) read locally; untrusted path content (absolute / `..`-traversal asset refs) crosses into the verb | Local file paths, asset refs (untrusted text) |
| local process → named pipe (16-02) | Any same-user local process that knows the pipe name can connect; server runs inside the injected x86 client. Current-user ACL AND in-client enable flag gate exposure; reload path contained against the SWG CLIENT root | Wire frames (relative path + op), bounded `MaxFrameBytes` |
| pipe worker thread → game thread (16-02) | Reload work crosses from background worker to game thread only via the marshal queue; native game state read ONLY on the game thread (cached snapshot) | Marshalled reload calls; cached game-state scalar |
| AI agent → MCP host (stdio, 16-03) | Untrusted tool-call args (`relativePath`) cross into the host | Tool-call JSON args (untrusted) |
| MCP host → named pipe (16-03) | Host's pipe client crosses the OS named-pipe boundary into the injected client; only a RELATIVE path crosses, re-resolved server-side under the in-client client root | Relative path on wire; `clientRoot` diagnostic in ack |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-16-01 | Tampering | Bundle path traversal in manifest / `.rsp` asset refs (16-01) | mitigate | Single shared `LooseOverridePath.IsContainedUnderRoot` predicate — relative refs via `LooseOverridePath.Resolve(bundleRoot, ref)`, absolute via `Path.GetFullPath` + SAME predicate; contained-absolute allowed+checked, escaping ref → `rejectedRefs` finding, NEVER `File.Exists`-probed; verb read-only. `ValidateBundleCommand.cs:387,400` | closed |
| T-16-02 | DoS | Oversize / malformed manifest or `.rsp` (16-01) | mitigate | Bounded text parse; JSON-parse / `.rsp`-grammar failure → exit-2 ParseError. `ValidateBundleCommand.cs:144-151,268-280` · tests `…_MalformedManifestJson_…` / `…_MalformedRspLine_…` | closed |
| T-16-03 | Info disclosure (correctness) | Contract doc states false version capability / phantom manifest field (16-01) | mitigate | `docs/ai/blender-boundary-contract.md` mirrors `TreVersion.cs`; manifest schema sourced verbatim from `export_manifest.py` + `export_bundle.as_dict`; doc↔verb parity test enforces lockstep (single `BucketFilenames` table) | closed |
| T-16-04 | Tampering | Silent fixture-refresh changes cross-validation bytes (16-01) | mitigate | SHA-256 of each pinned binary golden in `fixture-hashes.txt`, re-asserted by `BlenderBoundaryGoldenTests` | closed |
| T-16-05 | Info disclosure (correctness) | exit-0-with-findings misread as "validated clean" (16-01) | mitigate | Success envelope carries explicit `valid` + `hasRejectedRefs`; doc states agents must read `valid`. `ValidateBundleCommand.cs:220-235` · tests (a)/(e) | closed |
| T-16-10 | Spoofing/Elevation | Unauthorized local process connects + triggers reloads (16-02) | mitigate | `PipeSecurity` with exactly one `PipeAccessRule` for `WindowsIdentity.GetCurrent().User` + FullControl/Allow, deny-others-by-omission; `NamedPipeServerStream` local-only; listener starts only when in-client enable flag ON (fail-closed default OFF). `LivePipeServer.cs:244-256`, `main.cs:132` · test `PipeSecurity_HasExactlyOneCurrentUserFullControlAllowRule` | closed |
| T-16-11 | Tampering (integrity) | Reload work on wrong thread / native game-state deref off game thread → AV/crash (16-02) | mitigate | All reload marshals via `GameCallbacks.AddMainLoopCall`; game-state is a `volatile` cached scalar refreshed only on the game thread via `RefreshGameStateOnGameThread`; worker/accept loop reads ONLY the cache, contains no `Game.IsRunning` ref, never invokes the probe Func. `LivePipeServer.cs:83,131-133,408`, `main.cs:151` · source-absence + behavioral game-thread-only test | closed |
| T-16-12 | DoS | Malformed / oversize / partial / slow wire frame (16-02) | mitigate | Bounded `MaxFrameBytes` + op allow-list + explicit server-side connect/read timeouts; bad/partial frame closes that connection only, accept loop recovers. `LivePipeServer.cs:320-342,368` · oversize/partial/stalled/malformed-then-valid tests | closed |
| T-16-14 | Tampering | In-client reload-asset targets arbitrary absolute / out-of-root path (16-02) | mitigate | Wire carries RELATIVE path; server resolves via shared `LooseOverridePath.Resolve` against the pinned SWG CLIENT root (NOT injectRoot) and REJECTS absolute / `..`-escape (`accepted=false`, no enqueue); `live_ping` ack reports resolved `clientRoot`. `LivePipeServer.cs:487,491-507` · `LiveBridgeIntegrationTests.ReloadAsset_AbsoluteOrEscape_RejectedNoEnqueue` | closed |
| T-16-15 | Tampering (protocol drift) | net10/net472 wire shapes silently diverge (16-02 net472 side) | mitigate | OUTPUT via Utinni-owned `CanonicalJson` (fixed key order, camelCase, no whitespace, pinned-string `ReloadTier`, invariant numbers); all four golden wire byte-vectors asserted byte-exact produce+consume (net472); `protocolVersion`-skew → structured reject ack. `UtinniCoreDotNet/Live/CanonicalJson.cs` · `LiveBridgeProtocolTests` | closed |
| T-16-20 | Elevation | Agent reaches `live_*` when tier not enabled (16-03) | mitigate | Fail-closed gate: `live_*` tools AND `LivePipeClient` singleton UNREGISTERED unless `--enable-live`; `LiveTools` carries no `[McpServerToolType]`. `LiveTools.cs:69`, `Program.cs:89-93` · `LiveTools_AreAbsentWithoutEnableLive_PresentWithIt` (real `ListToolsAsync`) | closed |
| T-16-21 | Tampering | Path-escape via `live_reload_asset` path arg, MCP side (16-03) | mitigate | `ResolvedRoot.Resolve` runs BEFORE any pipe send (throws on escape → SDK tool error); RELATIVE path sent on wire, re-resolved server-side (T-16-14). `LiveTools.cs:124,128` · loopback + integration + root-reconciliation tests | closed |
| T-16-22 | DoS | Malformed / oversize / partial ack frame hangs the MCP call (16-03) | mitigate | Injectable short connect/read timeout + bounded `MaxFrameBytes`; transport failure returns a result object, never hangs. `LivePipeClient.cs` (`ExchangeAsync` CTS + `ReadFrameAsync` guard) · Oversize/Partial/MidMessage tests | closed |
| T-16-23 | Spoofing | No client listening → naive client hangs (16-03) | mitigate | Short connect timeout (2s) maps no-server to `listening:false`. `LivePipeClient` (`LivePingResult.NotListening`) · `NoClient_Ping_ReturnsListeningFalseQuickly_NeverHangs` | closed |
| T-16-24 | Candor (correctness) | Visible-render over-promise (16-03) | mitigate | Ack reports honest `ReloadAssetClassifier` tier with `reloadAttempted=false` + transport-only note; tool Description states queued != visible reload; render best-effort, not a success gate. `LiveTools.cs:110-111,144,147`, `LivePipeServer.cs:533` · `GoldenWire_ReloadAck_ConsumedExact` | closed |
| T-16-25 | Tampering (protocol drift) | net10/net472 wire shapes diverge — green net472 test hides broken net10 (16-03) | mitigate | OUTPUT bytes produced on BOTH sides by Utinni-owned canonical writer (net472 `CanonicalJson` + net10 field-for-field re-impl); committed golden wire byte-vectors (all four incl. nested acks) byte-exact on both; `protocolVersion` in envelope + skew → structured error; real net472 cross-impl round-trip. `Utinni.Mcp/Server/CanonicalJson.cs`, `UtinniCoreDotNet/Live/CanonicalJson.cs` · `LivePipeLoopbackTests.GoldenWire_*` + `LiveBridgeIntegrationTests` | closed |
| T-16-26 | Correctness | `--root` and in-client resolved client root diverge → agent previews a different file than MCP validated (16-03) | mitigate | `live_ping` ack carries `clientRoot` diagnostic; relative-on-wire + own-root resolution keeps containment safe regardless; MCP-SECURITY.md documents `--root` MUST equal the in-client client root with this field as the verification mechanism. `LivePipeServer.cs:473`, `LiveTools.cs:100` · `RootReconciliation_SameRoot_Succeeds_DifferentRoot_DiagnosticExposesDivergence` | closed |
| T-16-SC | Tampering (supply chain) | npm/pip/cargo installs (16-02/16-03) | n/a | No new packages — outputs use hand-rolled BCL-only `CanonicalJson`; net10 `System.Text.Json` in-box (parse-only); only the already-pinned `ModelContextProtocol 1.4.0`. Package Legitimacy Gate not triggered. `UtinniCoreDotNet.csproj`, `Utinni.Mcp.csproj` | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party) · n/a (not triggered)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-16-A | T-16-AR | Local file reads of a maintainer-supplied Blender bundle — single-user local desktop tool, bundle is maintainer-authored output, no network surface (analog AR-14-03). | Kenneth Long | 2026-06-14 |
| AR-16-B | T-16-13 | Pipe name collision / squatting — sufficiently-specific pipe name, server owns creation, single-user local desktop trust assumption (analog AR-14-03/04). | Kenneth Long | 2026-06-14 |
| AR-16-01 | T-16-AR16 | Single-user local desktop pipe; `--enable-live` / `UTINNI_MCP_ENABLE_LIVE` / pipe name are IDENTIFIERS not secrets. Local-only named pipe + current-user `PipeSecurity` ACL (server-side) + in-client start gate + MCP-side `--enable-live` gate. Dual-flag operator contract + root-reconciliation requirement documented; no remote / multi-tenant surface. Also logged in `Utinni.Mcp/MCP-SECURITY.md`. | Kenneth Long | 2026-06-14 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-14 | 18 | 18 | 0 | gsd-security-auditor (Claude Sonnet) — /gsd:secure-phase 16 |

*Note: T-16-SC appears once in the register (deduplicated across waves 16-02/16-03). Counting the
shared supply-chain threat per-wave, the auditor verified 22/22 wave-level threat instances closed.*

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / n/a)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-14
