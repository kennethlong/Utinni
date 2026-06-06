---
phase: 14
slug: headless-mcp-server-utinni-mcp-the-centerpiece
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-05
---

# Phase 14 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 — in a **net10** test project (`Utinni.Mcp.Tests`); existing `Utinni.Cli.Tests` stays net472 |
| **Config file** | new `Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj` (net10.0) — Wave 0 creates it |
| **Quick run command** | `dotnet test Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj` |
| **Full suite command** | `dotnet test Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj` (net10 lane) + `dotnet test Utinni.sln` (x86 lane) |
| **Estimated runtime** | ~30 seconds (net10 lane; round-trip launches a child server exe) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj`
- **After every plan wave:** Run the net10 lane + the existing x86 `dotnet test Utinni.sln`
- **Before `/gsd:verify-work`:** Both lanes must be green
- **Max feedback latency:** 60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 14-XX-XX | TBD | TBD | MCP-02 (SC3) | T-14-01 | Relative path with `..`/rooted/escape rejected by resolved-root pin | unit | `dotnet test --filter ResolvedRootTests` | ❌ W0 | ⬜ pending |
| 14-XX-XX | TBD | TBD | MCP-01 (SC5) | — | Real `McpClient` completes stdio handshake; `ListToolsAsync` returns expected tools | integration | `dotnet test --filter RoundTripTests.Handshake` | ❌ W0 | ⬜ pending |
| 14-XX-XX | TBD | TBD | MCP-01 (SC5) | — | `read_tre`/`decode_iff` returns CLI envelope as structured content | integration | `dotnet test --filter RoundTripTests.ReadRoundTrip` | ❌ W0 | ⬜ pending |
| 14-XX-XX | TBD | TBD | MCP-02 (SC5) | — | edit→save writes loose-override file + returns `{written,path,bytesWritten,validated}` | integration | `dotnet test --filter RoundTripTests.EditSaveRoundTrip` | ❌ W0 | ⬜ pending |
| 14-XX-XX | TBD | TBD | MCP-02 (SC2) | T-14-03 | `repack_tre` dry_run=true does NOT write; dry_run=false writes + backs up | integration | `dotnet test --filter RoundTripTests.RepackDryRun` | ❌ W0 | ⬜ pending |
| 14-XX-XX | TBD | TBD | MCP-02 | T-14-05 | Wedged/missing CLI → hard MCP error after 60s timeout | integration | `dotnet test --filter DispatcherTests.Timeout` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky · plan/wave/task IDs finalized by the planner*

---

## Wave 0 Requirements

- [ ] `Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj` — net10 xUnit project + `ModelContextProtocol` 1.4.0 ref
- [ ] `ResolvedRootTests.cs` — path-escape unit tests (SC3)
- [ ] `RoundTripTests.cs` — `McpClient` handshake + read + edit→save + repack-dry-run (SC5/SC2)
- [ ] `DispatcherTests.cs` — timeout / exe-missing hard-error tests
- [ ] Fixture root dir — reuse `Utinni.Cli.Tests` sample `.tre`/`.iff`/`.tab`/`.stf` under a temp resolved-root
- [ ] CI step — net10 `dotnet test` lane added alongside the x86 MSBuild lane

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live MCP client (Claude Desktop, etc.) drives the registered `utinni-mcp` server end-to-end | MCP-01/MCP-02 (UX confidence) | Real third-party client launch is environment-specific; not CI-automatable | Register `utinni-mcp` in `.mcp.json` with `--root`, restart client, call one read + one edit→save tool |

*The three testable success criteria (path-escape, McpClient round-trip, subprocess timeout) are all automated above — the manual check is optional UX confidence, not a gate.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
