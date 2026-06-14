---
phase: 16
slug: live-injected-mcp-bridge-blender-ecosystem-boundary
status: validated
nyquist_compliant: true
wave_0_complete: true
created: 2026-06-13
validated: 2026-06-14
---

# Phase 16 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: 16-RESEARCH.md §"Validation Architecture". Two independent tracks —
> MCP-03 (live named-pipe bridge) and ECO-01 (Blender boundary doc + reader reuse).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework (MCP host)** | xUnit 2.9.3 + Microsoft.NET.Test.Sdk 17.13.0, net10 (`Utinni.Mcp.Tests`) |
| **Framework (in-client managed)** | xUnit, net472 (`UtinniCoreDotNet.Tests`) |
| **Framework (CLI verbs)** | xUnit, net472 (`Utinni.Cli.Tests`) |
| **Quick run command** | `dotnet test Utinni.Mcp.Tests --no-build` (per-project, fast lane) |
| **Full suite command** | the three `dotnet test` lanes in `.github/workflows/ci.yml` (net10 MCP, net472 CLI, native) |
| **Estimated runtime** | ~30–90 seconds per lane (loopback pipe tests are in-process, no live client) |

---

## Sampling Rate

- **After every task commit:** Run the relevant per-project `dotnet test` quick lane.
- **After every plan wave:** All three CI `dotnet test` lanes green.
- **Before `/gsd:verify-work`:** Full suite green, plus the Tier-4 manual live-bridge smoke (non-gating, documented per D-01).
- **Max feedback latency:** ~90 seconds (per-project quick lane).

---

## Per-Task Verification Map

| Req ID | Behavior | Test Type | Automated Command | Proving Test(s) | Status |
|--------|----------|-----------|-------------------|-----------------|--------|
| MCP-03 | `live_ping` round-trips over a loopback pipe (no live client) | integration (loopback) | `dotnet test Utinni.Mcp.Tests --filter LivePipe` | `LivePipeLoopbackTests.Loopback_Ping_RoundTripsAndMapsClientRoot` | ✅ green |
| MCP-03 | `live_reload_asset` sends a well-formed envelope + maps the ack tier | integration (loopback) | `dotnet test Utinni.Mcp.Tests --filter LivePipe` | `LivePipeLoopbackTests.Loopback_ReloadAsset_SendsRelativeEnvelope_AndMapsAck` | ✅ green |
| MCP-03 | `--enable-live` OFF ⇒ `live_*` tools NOT registered (fail-closed, D-04) | integration (real `ListToolsAsync`) + unit | `dotnet test Utinni.Mcp.Tests --filter LivePipe` + `--filter EnableLive` | `LivePipeProtocolTests.LiveTools_AreAbsentWithoutEnableLive_PresentWithIt` + 6 `ServerArgsTests.EnableLive_*` facts | ✅ green |
| MCP-03 | In-client server: envelope parse → `ReloadAssetClassifier` tier → enqueue (game-state injected) | unit (pure-managed) | `dotnet test UtinniCoreDotNet.Tests --filter LivePipeServer` | `LivePipeServerTests.Decide_*` + `HandleRequestBytes_Contained*_EnqueuesExactlyOneAction` | ✅ green |
| MCP-03 | Pipe framing edge cases (partial read / oversize / server-close) → hard error, no hang | integration | `dotnet test Utinni.Mcp.Tests --filter LivePipe` | `LivePipeLoopbackTests.{NoClient,ServerClosesMidMessage,OversizeAckFrame,PartialAckFrame}_…NeverHangs` + net472 `Framing_*` | ✅ green |
| MCP-03 | Cross-plan pipe-name agreement: both ends' `PipeName` byte-equal the canonical `pipe-name.txt` fixture | unit (both lanes) | `dotnet test UtinniCoreDotNet.Tests --filter LiveBridgeProtocol` + `dotnet test Utinni.Mcp.Tests --filter LivePipe` | net472 `PipeNameFixture_Line1AndLine2_MatchConstants` + net10 `FixtureEquality_PipeNameAndFraming_ByteEqualTheCanonicalAnchor` | ✅ green |
| MCP-03 | Live in-client confirmation (visible/queued reload) | **Tier-4 manual** | — (maintainer smoke; non-gating per D-01) | n/a (manual-only) | ⬜ manual |
| ECO-01 | `parse-tre` over the pinned Blender synthetic `.tre` surfaces the **CV-1** crc-first-vs-size-first v0005 TOC disagreement (honest exit-2, NOT exit-0 — plan premise corrected) | golden | `dotnet test Utinni.Cli.Tests --filter Blender` | `BlenderBoundaryGoldenTests.ParseTre_BlenderSyntheticMiniTre_SurfacesCrcVsSizeFirstDisagreement` | ✅ green |
| ECO-01 | `decode-iff` summarizes the pinned `.msh` (MESH appearance, count-only, DEC-A3-clean) | golden | `dotnet test Utinni.Cli.Tests --filter Blender` | `BlenderBoundaryGoldenTests.DecodeIff_BlenderStaticMesh_EmitsCountOnlyAppearanceSummary` + `BlenderGoldens_MatchPinnedSha256Provenance` | ✅ green |
| ECO-01 | `validate-bundle` accepts a contained `.rsp`/manifest + rejects-not-probes escapes | golden | `dotnet test Utinni.Cli.Tests --filter ValidateBundle` | 10 `ValidateBundleTests.*` facts (happy-path, contained-absolute, escape-not-probed, malformed exit codes) | ✅ green |
| ECO-01 | Contract doc exists + doc↔verb parity (every bucket filename present in the doc) | doc/presence + golden | `dotnet test Utinni.Cli.Tests --filter Blender` | `docs/ai/blender-boundary-contract.md` + `BlenderBoundaryGoldenTests.ContractDoc_ContainsEveryBucketFilenameFromVerbTable` | ✅ green |
| ECO-01 | Cross-repo pointer note in `swg-blender-plugin/REFERENCES.md` (Task 4) | doc/presence (3rd repo) | — (blocking-human checkpoint; outside CI + standing write authority) | n/a (manual-only) | ⬜ manual |

*Status: ⬜ pending/manual · ✅ green · ❌ red · ⚠️ flaky*

**Verification note (2026-06-14):** net10 lane spot-run live during this audit — `dotnet test Utinni.Mcp.Tests --filter LivePipe` = **13/13 passed**. The net472 lanes (`UtinniCoreDotNet.Tests` LiveBridgeProtocol 19 / LivePipeServer 21 / LiveBridgeIntegration 5, `Utinni.Cli.Tests` Blender+ValidateBundle 14) are documented green via the VS2026-MSBuild-then-`dotnet test --no-build` recipe (`feedback_dotnet_build_msbuild_resources`) and are CI-enforced across the three `ci.yml` lanes.

---

## Wave 0 Requirements

- [x] `Utinni.Cli.Tests/Fixtures/live/pipe-name.txt` — the canonical pipe-name + framing-descriptor fixture (16-02, eol=lf-pinned); both pipe ends assert byte-equality against it (cross-plan agreement anchor). Linked into `UtinniCoreDotNet.Tests` (net472) and `Utinni.Mcp.Tests` (net10). ✅ present
- [x] `Utinni.Mcp.Tests/LivePipeProtocolTests.cs` + `LivePipeLoopbackTests.cs` — real-`ListToolsAsync` gating lock + loopback ping/reload + framing edge cases + the net10 `PipeName`/framing fixture byte-equality + the 4 golden-wire byte-vectors (covers MCP-03 host half). ✅ green (13/13)
- [x] `Utinni.Mcp.Tests/ServerArgsTests.cs` (extended) — `--enable-live` presence-flag parse + `UTINNI_MCP_ENABLE_LIVE` env alias (6 EnableLive facts). ✅ green
- [x] `UtinniCoreDotNet.Tests/Live/LivePipeServerTests.cs` (+ `LiveBridgeProtocolTests.cs`, `LiveBridgeIntegrationTests.cs`) — pure-managed envelope→tier→enqueue (game-state injected), source-confinement assertion, bounded lifecycle, the in-client `PipeName` fixture byte-equality, and the REAL cross-impl wire round-trip against the actual 16-02 server. ✅ green (21 + 19 + 5)
- [x] `Utinni.Cli.Tests/Commands/BlenderBoundaryGoldenTests.cs` — pinned `.tre`/`.msh` cross-validation + SHA-256 provenance + doc↔verb parity (surfaced CV-1). ✅ green
- [x] `Utinni.Cli.Tests/Fixtures/blender/` — pinned `frn_all_bed_sm_s1_l0.msh` + `retail_mini_0005.tre` + `.rsp`/`.cfg`/manifest + `fixture-hashes.txt` (in-repo, no LFS — CON-O-09). ✅ present
- [x] `Utinni.Cli.Tests/Commands/ValidateBundleTests.cs` — the thin `validate-bundle` verb, 10 facts (happy-path / contained-absolute / escape-not-probed / malformed exit codes). ✅ green

*(The MESH/SKMG appearance decoders and the `parse-tre`/`decode-iff`/`inspect-iff` read paths already exist and are golden-tested — only the Blender-specific fixtures + asserts and the thin `validate-bundle` verb are net-new.)*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live in-client reload confirmation (agent → pipe → injected client applies/reloads, visible/queued) | MCP-03 | CI cannot inject into a live `SWG.exe`; requires a running injected client | Launch injected SWG client with BOTH `[Live] enableLiveBridge=true` (in-client config) AND `Utinni.Mcp --enable-live` (the dual-flag CUR-NEW-6 contract); from an MCP agent call `live_ping` (expect `listening:true` + the injected `clientRoot`) then `live_reload_asset` on an edited asset; confirm the ack envelope reports the honest `ReloadAssetClassifier` tier and `reloadAttempted=false` candor (C-14, transport-only until RESID-03). Visible re-render is best-effort and NON-gating per D-01. |
| Cross-repo pointer note in `swg-blender-plugin/REFERENCES.md` → `blender-boundary-contract.md` (16-01 Task 4) | ECO-01 | Target is a THIRD repo (`D:/Code/swg-blender-plugin`) outside CI and standing write authority; a `checkpoint:human-action`, gate=blocking-human | Append the proposed row (16-01-SUMMARY §"Task 4 Checkpoint") to the "External references (D:/Code)" table in `swg-blender-plugin/REFERENCES.md`; commit `docs: point at Utinni-authoritative Blender boundary contract (ECO-01)`; verify `git grep -q "blender-boundary-contract" -- REFERENCES.md`. |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 90s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** 2026-06-13

---

## Validation Audit 2026-06-14

State A retroactive audit of the executed phase (plans 16-01/02/03 complete; commit `53927fa`). All 17 test files + fixtures verified present on disk; every per-task-map requirement cross-referenced to a named, green test method. Per-task statuses promoted from ⬜ pending → ✅ green; Wave 0 marked complete. The ECO-01 `.tre` row was corrected to the **CV-1** reality (test asserts honest exit-2 for the synthetic crc-first v0005 TOC, not the plan's assumed exit-0). No automated gaps found — auditor agent not spawned.

| Metric | Count |
|--------|-------|
| Requirements audited | 12 (6 MCP-03 + 6 ECO-01) |
| Automated — green | 10 |
| Manual-only (non-gating) | 2 (Tier-4 live ping · cross-repo pointer note) |
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |
| New tests generated | 0 (full coverage already shipped during execution) |

**Live spot-check:** `dotnet test Utinni.Mcp.Tests --filter LivePipe` → 13/13 passed. net472 lanes documented green (LiveBridgeProtocol 19 / LivePipeServer 21 / LiveBridgeIntegration 5 / Blender+ValidateBundle 14) via the MSBuild-then-`--no-build` recipe + CI-enforced.

**Auditor:** 2026-06-14
