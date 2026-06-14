---
phase: 16-live-injected-mcp-bridge-blender-ecosystem-boundary
verified: 2026-06-14T12:30:00Z
status: passed
score: 4/4 roadmap success criteria verified (MCP-03 + ECO-01); 35/35 plan must-have truths verified
overrides_applied: 0
requirements:
  MCP-03: SATISFIED
  ECO-01: SATISFIED
residuals:  # non-gating per D-01 — NOT defects
  - "Tier-4 manual live in-client confirmation (launch BOTH [Live] enableLiveBridge AND --enable-live; live_ping then live_reload_asset against a real injected SWGEmu; compare --root vs the ping clientRoot). Visible in-client re-render rides on the deferred RESID-03 path and is explicitly NON-gating (D-01)."
findings:
  - id: CV-1
    type: honest-boundary-finding
    severity: info
    note: "Blender synthetic retail_mini_0005.tre uses crc-first v0005 TOC; Utinni reads size-first (validated against live SWGEmu). parse-tre exits 2 (UnknownCompressor). Correctly asserted as REAL behavior + documented in the contract doc; reader NOT changed (it is correct for real TreeFileBuilder output). This is the class of mismatch cross-validation exists to catch — handled honestly, not a defect."
  - id: REQ-bookkeeping
    type: doc-lag
    severity: info
    note: "REQUIREMENTS.md line 139 shows ECO-01 [ ] and the coverage table (212-213) shows MCP-03/ECO-01 'Pending'. The implementation is verifiably complete; this is a metadata-update lag, not a code gap."
---

# Phase 16: Live-injected MCP bridge + Blender ecosystem boundary — Verification Report

**Phase Goal:** Let an AI agent drive the LIVE-injected client over an MCP bridge to preview an edit in-client, via a NEW named-pipe IPC hop into the x86 host (never host the SDK in-proc). In parallel, formalize the Utinni ↔ swg-blender-plugin boundary as a documented file-format / `.rsp` search-path contract — docs + reuse of existing readers, honoring DEC-A3.

**Verified:** 2026-06-14
**Status:** passed
**Re-verification:** No — initial verification
**Methodology:** Goal-backward. Read all 3 PLANs/SUMMARYs + CONTEXT; inspected every shipped source file; RAN all four test lanes (net10 + net472 via the documented VS2026-MSBuild + `dotnet test --no-build` recipe).

## Goal Achievement

### Roadmap Success Criteria

| # | Success Criterion | Status | Evidence |
|---|-------------------|--------|----------|
| 1 | AI agent can drive the live client over the MCP bridge (named-pipe IPC into x86) to preview an edit (MCP-03) | ✓ VERIFIED (Tier-2 per D-01) | `live_ping`/`live_reload_asset` tools (LiveTools.cs) → `LivePipeClient` (net10) → real `LivePipeServer` (net472) round-trips over an actual pipe. `LiveBridgeIntegrationTests` 5/5 green; net10 `LivePipe*` 13/13 green. Visible re-render is the NON-gating Tier-4 residual (D-01). |
| 2 | MCP host out-of-proc; SDK never hosted inside SWG.exe; cross to client only via the named pipe | ✓ VERIFIED | `grep ModelContextProtocol\|AddMcpServer` over `UtinniCoreDotNet/` = ZERO matches. The pipe SERVER (System.IO.Pipes, BCL only) is the only thing in-proc; the pipe CLIENT lives in the out-of-proc net10 host (LivePipeClient.cs). |
| 3 | Boundary formalized as a documented `.iff`/`.tre` version + `.rsp` contract, open/preview verbs, no runtime coupling (ECO-01) | ✓ VERIFIED | `docs/ai/blender-boundary-contract.md` (243 lines, all 4 D-06 surfaces + manifest schema + reachability). `validate-bundle` verb ships. Anti-coupling §4 states DEC-A3 explicitly. Neither repo imports the other. |
| 4 | C# and Blender side cross-validate against shared golden fixtures | ✓ VERIFIED | Pinned `frn_all_bed_sm_s1_l0.msh` + `retail_mini_0005.tre` (SHA-256 manifest, drift-guard test). `decode-iff` over the `.msh` = working cross-validation (count-only). CV-1 surfaced + honestly asserted. `BlenderBoundaryGoldenTests` green. |

**Score: 4/4 roadmap success criteria verified.**

### Requirement Verdicts

| Requirement | Verdict | Evidence |
|-------------|---------|----------|
| **MCP-03** | SATISFIED (Tier-2 automated deliverable; Tier-4 live ping is the non-gating D-01 residual) | Named-pipe bridge round-trips end-to-end across the TFM wall; fail-closed gating PROVEN via real `McpClient.ListToolsAsync`; in-client path client-root-contained; gameIsRunning game-thread-cached; SDK out-of-proc. |
| **ECO-01** | SATISFIED | 4-surface contract doc + `validate-bundle` (contained-absolute aware, single shared predicate) + pinned cross-validated goldens + cross-repo pointer note; DEC-A3 honored (no 3D codec). |

### Plan Must-Have Truths (35 total across 3 plans)

**16-01 (ECO-01) — 8/8 VERIFIED**
- Contract doc teaches `.rsp` line format (absolute RHS), bucket rules, TRE version matrix (5000 readable / 6000 enumerate-only), bundle layout, manifest schema verbatim from exporter, no-coupling rule — VERIFIED (doc §1-§5, manifest §1b with `client_cfg` nested inside `assets`).
- `validate-bundle` parses manifest+.rsp+.cfg, contained-absolute allowed+probed, escapes rejected-never-probed, explicit `valid`/`hasRejectedRefs` envelope — VERIFIED (ValidateBundleCommand.cs: `TryContainResolve` routes absolute via `Path.GetFullPath`→`IsContainedUnderRoot`, relative via `LooseOverridePath.Resolve`; escapes never File.Exists-probed).
- `parse-tre`/`decode-iff` over pinned Blender goldens — VERIFIED (.msh decode count-only passes; .tre is CV-1).
- SHA-256-pinned goldens + drift-guard test — VERIFIED (fixture-hashes.txt + BlenderBoundaryGoldenTests).
- Malformed manifest AND malformed .rsp → exit-2 ParseError — VERIFIED (Run + ValidateRspFile throw ParseError; tests green).
- Single shared `IsContainedUnderRoot` for both branches — VERIFIED (both branches call the one predicate; doc §4 states it).
- Bucket-filename set ↔ doc parity (C-17) — VERIFIED (`BucketFilenames` static + parity test green).
- Cross-repo pointer note — VERIFIED (present in `D:/Code/swg-blender-plugin/REFERENCES.md` line 8 — the 16-01 Task-4 blocking-human checkpoint was completed).

**16-02 (MCP-03 in-client) — 14/14 VERIFIED**
- Parses request, classifies via ReloadAssetClassifier, enqueues exactly one Action, never touches native off game thread — VERIFIED (LivePipeServer.HandleRequestBytes/Decide; LivePipeServerTests 21 green).
- Cached-false gameIsRunning → honest Unavailable, no enqueue — VERIFIED (Decide `!gameIsRunning` branch).
- gameIsRunning is a game-thread-updated volatile cache; worker never P/Invokes native — VERIFIED (`_gameIsRunningCache` volatile; probe invoked ONLY in `RefreshGameStateOnGameThread`; comment-stripped source-assertion test green).
- StartListener wired post-Callbacks/pre-Application.Run, flag-gated, idempotent, cache-refresh wired — VERIFIED (main.cs lines 110-156: `[Live] enableLiveBridge` gate, `ResolveClientRoot()`, self-re-enqueuing AddMainLoopCall refresh, Interlocked idempotency).
- In-client path contained against SWG CLIENT root (not injectRoot) — VERIFIED (main.cs `ResolveClientRoot` mirrors editor save path; Decide uses `LooseOverridePath.Resolve(clientRoot,...)`).
- Bounded pipe lifecycle (cancel/dispose/timeout/malformed-recovery) — VERIFIED (AcceptLoop + ReadFrameBounded + WaitForConnection; lifecycle tests green).
- ServerArgs.EnableLive, fail-closed absence — VERIFIED (ServerArgs.cs; ServerArgsTests green).
- Canonical JSON writer Utinni-owns; all 4 goldens byte-exact on net472 (produce+consume) — VERIFIED (CanonicalJson.cs hand-rolled; LiveBridgeProtocolTests 19 green).
- Byte-exact sole contract incl. nested acks; payload-only goldens + framing fixture line 2 — VERIFIED (wire-*.json hold payload bytes; pipe-name.txt line 2 framing descriptor).
- protocolVersion skew → structured reject — VERIFIED (Decide skew branch + skew test).
- ReloadTier pinned string names in goldens — VERIFIED (`.Tier.ToString()`; "PendingNextSceneChange" in wire-reload-ack.json).
- live_ping ack carries clientRoot diagnostic — VERIFIED (PingResult.ClientRoot; in golden).
- net472 OUTPUT uses canonical writer, no STJ package — VERIFIED (C-03; hand-rolled, BCL-only).

**16-03 (MCP-03 host) — 9/9 VERIFIED** (validated by 13/13 net10 + 5/5 net472)
- live_ping loopback round-trips, no-server → listening:false never hangs — VERIFIED (LivePipeClient + loopback tests).
- live_reload_asset resolves under ResolvedRoot BEFORE send, sends RELATIVE path, maps tier — VERIFIED (LiveTools.LiveReloadAsset: `root.Resolve` first, sends relative).
- REAL cross-impl net472 integration test over the actual wire — VERIFIED (LiveBridgeIntegrationTests spins up real LivePipeServer; 5/5 green).
- net10 CanonicalJson field-for-field, byte-EXACT vs SAME goldens (all four incl. nested) — VERIFIED (Utinni.Mcp/Server/CanonicalJson.cs; LivePipeLoopbackTests GoldenWire_* green).
- --enable-live OFF → live_* NOT advertised, proven via REAL ListToolsAsync (off⇒absent/on⇒present) — VERIFIED (LivePipeProtocolTests launches built exe twice, real McpClient enumeration; green).
- Framing edge cases (partial/oversize/server-close) → hard error never hang; skew → structured error — VERIFIED (ExchangeAsync catch ladder; edge-case tests green).
- net10 PipeName line-1 + framing line-2 byte-equal fixture — VERIFIED (duplicated consts + fixture-equality test).
- live_ping surfaces clientRoot; MCP-SECURITY documents root-reconciliation — VERIFIED (T-16-26 row).
- live_reload_asset Description + ack candor (queued != visible, reloadAttempted=false) — VERIFIED (Description + candor field).

### Required Artifacts

| Artifact | Status | Details |
|----------|--------|---------|
| `docs/ai/blender-boundary-contract.md` | ✓ VERIFIED | 243 lines; all 4 D-06 surfaces + manifest schema + version matrix + DEC-A3. |
| `Utinni.Cli/Commands/ValidateBundleCommand.cs` | ✓ VERIFIED | Text-only validator; shared predicate; no codec; wired into Program.cs dispatch; tests green. |
| `UtinniCoreDotNet/Live/LivePipeServer.cs` | ✓ VERIFIED | Bounded accept loop, client-root containment, game-thread cache, marshal, D-01 candor. |
| `UtinniCoreDotNet/Live/LiveBridgeProtocol.cs` | ✓ VERIFIED | Pipe name/version/framing/DTOs; output via CanonicalJson. |
| `UtinniCoreDotNet/Live/CanonicalJson.cs` | ✓ VERIFIED | Hand-rolled deterministic writer/reader (net472). |
| `UtinniCoreDotNet/main.cs` | ✓ VERIFIED | StartListener wiring (post-Callbacks/pre-Run, dual-flag gated, client-root pinned, cache-refresh wired). |
| `Utinni.Mcp/Server/LivePipeClient.cs` | ✓ VERIFIED | CliDispatcher twin; never-hang; net10 CanonicalJson; relative-on-wire. |
| `Utinni.Mcp/Server/CanonicalJson.cs` | ✓ VERIFIED | net10 field-for-field re-impl; byte-exact to goldens. |
| `Utinni.Mcp/Tools/LiveTools.cs` | ✓ VERIFIED | live_ping + live_reload_asset; non-[McpServerToolType]; candor. |
| `Utinni.Mcp/Program.cs` | ✓ VERIFIED | Conditional WithTools<LiveTools>() + LivePipeClient singleton gated on EnableLive. |
| pinned goldens (.msh/.tre/.rsp/.cfg/manifest/hashes + live wire-*.json) | ✓ VERIFIED | All present; SHA-256 manifest; live wire vectors confirm camelCase/string-tier/clientRoot/nested. |
| MCP-SECURITY.md live-tier addendum | ✓ VERIFIED | STRIDE T-16-20..26 + AR-16-01; each threat cites a passing test. |

### Key Link Verification

| From | To | Status | Details |
|------|----|--------|---------|
| main.cs | LivePipeServer.StartListener | ✓ WIRED | lines 132-153, dual-flag gated, client-root pinned. |
| LivePipeServer | GameCallbacks.AddMainLoopCall | ✓ WIRED | enqueue ctor arg + self-re-enqueuing refresh. |
| LivePipeServer | ReloadAssetClassifier.Classify | ✓ WIRED | Decide reload branch. |
| LivePipeServer | LooseOverridePath.Resolve (client root) | ✓ WIRED | containment, not injectRoot. |
| LiveBridgeProtocol | CanonicalJson | ✓ WIRED | all Serialize*/Parse route through it. |
| Program.cs | WithTools<LiveTools> | ✓ WIRED | `if (serverArgs.EnableLive)`. |
| LiveTools | ResolvedRoot.Resolve | ✓ WIRED | before pipe send. |
| LivePipeClient | net10 CanonicalJson | ✓ WIRED | byte-exact to net472 wire. |
| ValidateBundleCommand | LooseOverridePath + shared IsContainedUnderRoot | ✓ WIRED | both branches; doc §4. |
| validate-bundle | Program.cs dispatch | ✓ WIRED | help/no-args goldens updated; tests green. |
| cross-repo pointer | swg-blender-plugin/REFERENCES.md | ✓ WIRED | row present (line 8). |

### Behavioral Spot-Checks (RAN)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| net10 live-bridge lane (gating + golden-wire + loopback + edge) | `dotnet test Utinni.Mcp.Tests --filter ~LivePipe` | 13 passed / 0 failed | ✓ PASS |
| ECO-01 validate-bundle + golden cross-validation | `dotnet test Utinni.Cli.Tests --no-build -c Release --filter ~ValidateBundle\|~BlenderBoundary` | 14 passed / 0 failed | ✓ PASS |
| net472 protocol + server + REAL cross-impl integration | `dotnet test UtinniCoreDotNet.Tests --no-build -c Release --filter ~LiveBridgeProtocol\|~LivePipeServer\|~LiveBridgeIntegration` | 45 passed / 0 failed | ✓ PASS |
| SDK-out-of-proc constraint | `grep ModelContextProtocol\|AddMcpServer UtinniCoreDotNet/` | zero matches | ✓ PASS |
| Fail-closed gating is real enumeration | inspect LivePipeProtocolTests | launches built exe twice + real `McpClient.ListToolsAsync` | ✓ PASS |

All net472 lanes built with VS2026 MSBuild (`-p:Configuration=Release -p:Platform=x86`) then `dotnet test --no-build` per the documented MSB3823 recipe. No UtinniCore.cs regen churn left in the working tree.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| LivePipeServer.cs | 417 | `TODO(RESID-03)` | ℹ️ Info | The intentional D-01 best-effort reload placeholder. References the formal RESID-03 follow-up; the ack honestly reports `reloadAttempted=false`. NOT an unreferenced debt marker — this is the candor D-01 requires, not a defect. |

No FIXME/XXX/TBD, no `return null` stubs in render paths, no hardcoded-empty data feeding output. The validate-bundle `assets["..."]` reads are real manifest parsing, not stubs.

### CV-1 Assessment (per verification focus)

CV-1 is **correctly handled, not a defect.** The synthetic Blender `retail_mini_0005.tre` uses crc-first TOC order; Utinni's v0005 reader is size-first (validated against the live SWGEmu client). The plan premise ("parse-tre exits 0") was contradicted by reality; the executor asserted the REAL exit-2 `UnknownCompressor` behavior, left the reader unchanged (it is correct for real `TreeFileBuilder` output), and documented the finding in the contract doc (§2 CV-1) with the rule that hand-authored cross-tool v0005 `.tre` must use size-first order. This is precisely the class of boundary mismatch cross-validation exists to catch, surfaced honestly. The genuine D-08 cross-validation proof (the `.msh` decode-iff path) passes.

### D-01 Honesty Assessment (per verification focus)

Confirmed: visible in-client re-render is correctly NON-gating. The ack candor (`reloadAttempted=false` + transport-only note) is present in the server (LivePipeServer.cs:533), the wire golden (wire-reload-ack.json), the client result mapping, and the tool Description/candor field. The Tier-4 manual live ping is the documented residual by design. The phase is NOT failed for absence of visible render — that absence is the intended D-01 ceiling.

### Human Verification

None **required for phase passage** (Tier-2 automated deliverable is complete and proven). One **optional non-gating Tier-4 residual** is documented for whenever a real injected client is available (launch BOTH flags, run live_ping/live_reload_asset, compare --root vs the ping clientRoot). This is explicitly NON-gating per D-01 and does not block proceeding.

### Gaps Summary

No gaps. All 4 roadmap success criteria and 35 plan must-have truths are verified against the shipped code, with all four test lanes run and green (13 net10 + 14 ECO-01 + 45 net472 = 72 phase-relevant tests passing). The out-of-proc anti-pattern lock holds (zero MCP SDK references in the injected host). Both requirements (MCP-03, ECO-01) are satisfied. Two informational items (CV-1 honest boundary finding; a REQUIREMENTS.md bookkeeping `[ ]`/Pending lag for ECO-01) do not affect goal achievement.

---

_Verified: 2026-06-14_
_Verifier: Claude (gsd-verifier)_
