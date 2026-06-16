---
phase: 20
slug: terrain-trn-codec-verbs-mcp
status: complete
nyquist_compliant: true
wave_0_complete: true
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

> Every auto/tdd task in the revised plans carries a concrete `<automated>` verify (the planning-time
> Nyquist contract is satisfied — see Sign-Off). The "File Exists / Status" columns track *execution*
> state: the test files + fixtures are created during Wave 0 of execute-phase, so they read `W0 / pending`
> until that wave runs, then flip to ✅ green per the sampling rate above.

| Req ID | Behavior | Test Type | Automated Command | File Exists | Status |
|--------|----------|-----------|-------------------|-------------|--------|
| PROD-W2-TRN-01 | Navigate TGEN tree (TGEN→Layers→Boundaries/Filters/Affectors/sub-layers), names + active flags, six read-only palettes | unit | `dotnet test Utinni.Cli.Tests --filter TgenDecode --no-build` | ✅ | ✅ green |
| PROD-W2-TRN-02 | Typed Tier-1 tags; raw-fallback on unknown tag/version (never hard fail); DEAD-tag skip | unit | `dotnet test Utinni.Cli.Tests --filter TgenRawFallback --no-build` | ✅ | ✅ green |
| PROD-W2-TRN-03 | Byte-exact fixed-length field edit + active-flag toggle; untouched-leaf byte-identity; active toggle re-decodes to the edited value (read↔write parity) | golden roundtrip | `dotnet test Utinni.Cli.Tests --filter "ApplySaveTrn|RoundtripTrn" --no-build` | ✅ | ✅ green |
| PROD-W2-TRN-04 | Verbs (`decode-trn`/`roundtrip-trn`/`apply-save-trn` + `decode-iff` TGEN branch) + MCP read tool, BOTH lineages (SWGEmu + Infinity) | golden + MCP | `dotnet test Utinni.Mcp.Tests --filter Terrain --no-build` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `Utinni.Cli.Tests/Fixtures/trn/` — synthesized ≤200-byte `.trn` fixtures (low-version "SWGEmu-era" + high-version "Infinity-era") covering: minimal TGEN (no palettes/layers), one Tier-1 affector (`AHCN`), one Tier-1 boundary (`BCIR`), one unknown-tag (raw-fallback), one DEAD tag (skip)
- [x] Fixture synthesizer helper (hand-emit `TGEN` via `IffWriter`) — shared test fixture
- [x] `TgenDecoderTests.cs` — navigation + typed-field + raw-fallback assertions (TRN-01/02)
- [x] `RoundtripTrnTests.cs` — byte-exact whole-file identity across the fixture matrix (TRN-03/04)
- [x] `ApplySaveTrnTests.cs` — single-field edit + active toggle, untouched-leaf byte-identity, active-toggle round-trip parity (TRN-03)
- [x] `Utinni.Mcp.Tests/Fixtures/` + `TerrainReadToolTests.cs` — MCP thin-wrapper dispatch (TRN-04)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Real per-lineage `.trn` version pinning (D-13) | TRN-04 (supporting) | Requires live SWGEmu + Restoration client archives + TRE extraction; assets stay OUT of committed goldens (large / v6000+ encrypted) | Extract `terrain/<small-planet>.trn` via `utinni-cli` TRE verbs from each client; run `roundtrip-trn` as an extra byte-exact check; record observed FORM versions to pin the synthesized matrix |

*Synthesized fixtures carry the committed automated coverage; the real-asset pair is a supporting manual cross-check only.*

---

## Validation Sign-Off

- [x] All tasks have automated verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (fixtures + test stubs above)
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

> `wave_0_complete` flipped to `true` once execute-phase ran Wave 0 and the matrix went green — all
> per-task `<automated>` verifies above are now ✅ green.

**Approval:** planning-time validation contract signed off (Nyquist-compliant); execution complete.

---

## DEC-C3 Closure (2026-06-16)

**DEC-C3 (byte-exact round-trip across BOTH lineages) is CLOSED.**

The both-lineage byte-exact gate passes across the full low+high `TgenEraVersions` matrix plus the opt-in
`PaletteLineage` large-fixture set: `dotnet test Utinni.Cli.Tests -c Release --filter "RoundtripTrn|PaletteLineage"`
→ **55 passed / 0 failed**; `dotnet test Utinni.Mcp.Tests -c Release --filter Terrain` → **10 passed / 0
failed**. Full lanes green (411 Cli, 112 Mcp).

**Ratified pin (maintainer checkpoint, 2026-06-16):** SWGEmu and SWG Infinity ship the IDENTICAL
`PTAT/0014` terrain format with identical per-tag FORM versions for every observed tag; nothing was v6000+/
encrypted (concern #15 did not trigger). The ONE genuine lineage divergence is `BREC` (low `0002` /
high `0003`), preserved as a real low/high pair so the version-divergence dispatch path stays exercised.
The version pin was front-loaded as a PREREQUISITE (Plan 01 Task 3, observed by dogfooding `utinni-cli`
`parse-tre` + `inspect-iff`) and ratified here — NOT a post-hoc sign-off (review concern #1, both reviewers HIGH).

**Limitation recorded (non-blocking):** `AFCN` was absent from every sampled planet of both clients; it
stays annotated **ASSUMED v0000** in `TgenEraVersions.cs` and raw-falls-back cleanly (non-editable) if a
real asset's version ever differs. No AFCN version was fabricated (maintainer decision 2).

**Real-asset roundtrip:** SKIPPED by maintainer decision (decision 3) — the synthetic low+high matrix +
the `BREC` divergence + the committed MCP fixtures already cover both lineages byte-exactly; no real client
asset is in the repo (D-14).
