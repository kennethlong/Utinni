---
phase: 16
slug: live-injected-mcp-bridge-blender-ecosystem-boundary
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-13
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

| Req ID | Behavior | Test Type | Automated Command | File Exists | Status |
|--------|----------|-----------|-------------------|-------------|--------|
| MCP-03 | `live_ping` round-trips over a loopback pipe (no live client) | integration (loopback) | `dotnet test Utinni.Mcp.Tests --filter LivePipe` | ❌ W0 | ⬜ pending |
| MCP-03 | `live_reload_asset` sends a well-formed envelope + maps the ack tier | integration (loopback) | `dotnet test Utinni.Mcp.Tests --filter LivePipe` | ❌ W0 | ⬜ pending |
| MCP-03 | `--enable-live` OFF ⇒ `live_*` tools NOT registered (fail-closed, D-04) | unit | `dotnet test Utinni.Mcp.Tests --filter ServerArgs` | ❌ W0 | ⬜ pending |
| MCP-03 | In-client server: envelope parse → `ReloadAssetClassifier` tier → enqueue (game-state injected) | unit (pure-managed) | `dotnet test UtinniCoreDotNet.Tests --filter LivePipeServer` | ❌ W0 | ⬜ pending |
| MCP-03 | Pipe framing edge cases (partial read / oversize / server-close) → hard error, no hang | integration | `dotnet test Utinni.Mcp.Tests --filter LivePipe` | ❌ W0 | ⬜ pending |
| MCP-03 | Live in-client confirmation (visible/queued reload) | **Tier-4 manual** | — (maintainer smoke; non-gating per D-01) | n/a | ⬜ pending |
| ECO-01 | `parse-tre` opens the pinned Blender golden `.tre` | golden | `dotnet test Utinni.Cli.Tests --filter Blender` | ❌ W0 (fixture pin) | ⬜ pending |
| ECO-01 | `decode-iff` summarizes the pinned `.msh` (MESH appearance, count-only, DEC-A3-clean) | golden | `dotnet test Utinni.Cli.Tests --filter Blender` | ⚠️ decoder exists; fixture+assert new | ⬜ pending |
| ECO-01 | `validate-bundle`/`validate-rsp` accepts a pinned `.rsp` against the contract rules | golden | `dotnet test Utinni.Cli.Tests --filter ValidateBundle` | ❌ W0 (new verb) | ⬜ pending |
| ECO-01 | Contract doc exists + Blender pointer note exists | doc/presence | (presence check / review) | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Utinni.Mcp.Tests/LivePipeProtocolTests.cs` — loopback ping + reload-asset + framing edge cases (covers MCP-03).
- [ ] `Utinni.Mcp.Tests/ServerArgsTests.cs` (extend) — `--enable-live` parse + gated-registration assertion.
- [ ] `UtinniCoreDotNet.Tests/LivePipeServerTests.cs` — pure-managed envelope→tier→enqueue (game-state injected).
- [ ] `Utinni.Cli.Tests/Commands/BlenderBoundaryGoldenTests.cs` — pinned `.tre`/`.msh`/`.rsp` cross-validation.
- [ ] `Utinni.Cli.Tests/Fixtures/blender/` — pinned `frn_all_bed_sm_s1_l0.msh` (~6099 B) + `retail_mini_0005.tre` (~119 B) + a sample `.rsp` (in-repo, no LFS — CON-O-09).
- [ ] `Utinni.Cli.Tests/Commands/ValidateBundleTests.cs` — the new thin verb (if a `validate-bundle`/`validate-rsp` verb is planned).

*(The MESH/SKMG appearance decoders and the `parse-tre`/`decode-iff`/`inspect-iff` read paths already exist and are golden-tested — only the Blender-specific fixtures + asserts and the thin `validate-bundle` verb are net-new.)*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live in-client reload confirmation (agent → pipe → injected client applies/reloads, visible/queued) | MCP-03 | CI cannot inject into a live `SWG.exe`; requires a running injected client | Launch injected SWG client with `Utinni.Mcp --enable-live`; from an MCP agent call `live_ping` (expect injected-and-listening ack) then `live_reload_asset` on an edited asset; confirm the ack envelope reports the honest `ReloadAssetClassifier` tier. Visible re-render is best-effort and NON-gating per D-01. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
