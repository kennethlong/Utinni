---
phase: 20
slug: terrain-trn-codec-verbs-mcp
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-15
---

# Phase 20 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `20-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`Utinni.Cli.Tests` net4.7.2, `Utinni.Mcp.Tests` net10, `UtinniCoreDotNet.Tests` net4.7.2) |
| **Config file** | per-project `.csproj`; build via VS2026 MSBuild, run `dotnet test --no-build` |
| **Quick run command** | `dotnet test Utinni.Cli.Tests --no-build` |
| **Full suite command** | `dotnet test --no-build` (all managed lanes; native Catch2 unaffected) |
| **Estimated runtime** | ~30 seconds (CLI lane); ~90s full managed suite |

> **Build-before-test invariant:** `dotnet build` fails on WinForms `.resx` (MSB3823). Build the
> solution with MSBuild first, then `dotnet test --no-build`. Never `dotnet build` these projects.

---

## Sampling Rate

- **After every task commit:** Run `dotnet test Utinni.Cli.Tests --no-build` (codec/verb lane)
- **After every plan wave:** Run `dotnet test --no-build` across CLI + MCP + CoreDotNet lanes
- **Before `/gsd:verify-work`:** Full suite green, including both-lineage roundtrip goldens
- **Max feedback latency:** ~30 seconds (quick lane)

---

## Per-Task Verification Map

| Req ID | Behavior | Test Type | Automated Command | File Exists | Status |
|--------|----------|-----------|-------------------|-------------|--------|
| PROD-W2-TRN-01 | Navigate TGEN tree (TGEN→Layers→Boundaries/Filters/Affectors/sub-layers), names + active flags, six read-only palettes | unit | `dotnet test Utinni.Cli.Tests --filter TgenDecode --no-build` | ❌ W0 | ⬜ pending |
| PROD-W2-TRN-02 | Typed Tier-1 tags; raw-fallback on unknown tag/version (never hard fail); DEAD-tag skip | unit | `dotnet test Utinni.Cli.Tests --filter TgenRawFallback --no-build` | ❌ W0 | ⬜ pending |
| PROD-W2-TRN-03 | Byte-exact fixed-length field edit + active-flag toggle; untouched-leaf byte-identity | golden roundtrip | `dotnet test Utinni.Cli.Tests --filter "ApplySaveTrn|RoundtripTrn" --no-build` | ❌ W0 | ⬜ pending |
| PROD-W2-TRN-04 | Verbs (`decode-trn`/`roundtrip-trn`/`apply-save-trn` + `decode-iff` TGEN branch) + MCP read tool, BOTH lineages | golden + MCP | `dotnet test Utinni.Mcp.Tests --filter Terrain --no-build` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Utinni.Cli.Tests/Fixtures/trn/` — synthesized ≤200-byte `.trn` fixtures (low-version "SWGEmu-era" + high-version "Restoration-era") covering: minimal TGEN (no palettes/layers), one Tier-1 affector (`AHCN`), one Tier-1 boundary (`BCIR`), one unknown-tag (raw-fallback), one DEAD tag (skip)
- [ ] Fixture synthesizer helper (hand-emit `TGEN` via `IffWriter`) — shared test fixture
- [ ] `TgenDecoderTests.cs` — navigation + typed-field + raw-fallback assertions (TRN-01/02)
- [ ] `RoundtripTrnTests.cs` — byte-exact whole-file identity across the fixture matrix (TRN-03/04)
- [ ] `ApplySaveTrnTests.cs` — single-field edit + active toggle, untouched-leaf byte-identity (TRN-03)
- [ ] `Utinni.Mcp.Tests/Fixtures/` + `TerrainReadToolTests.cs` — MCP thin-wrapper dispatch (TRN-04)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real per-lineage `.trn` version pinning (D-13) | TRN-04 (supporting) | Requires live SWGEmu + Restoration client archives + TRE extraction; assets stay OUT of committed goldens (large / v6000+ encrypted) | Extract `terrain/<small-planet>.trn` via `utinni-cli` TRE verbs from each client; run `roundtrip-trn` as an extra byte-exact check; record observed FORM versions to pin the synthesized matrix |

*Synthesized fixtures carry the committed automated coverage; the real-asset pair is a supporting manual cross-check only.*

---

## Validation Sign-Off

- [ ] All tasks have automated verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (fixtures + test stubs above)
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
