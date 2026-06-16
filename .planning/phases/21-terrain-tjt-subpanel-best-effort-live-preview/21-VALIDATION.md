---
phase: 21
slug: terrain-tjt-subpanel-best-effort-live-preview
status: planned
nyquist_compliant: true
wave_0_complete: false   # flips true after Plan 01 actually executes (Wave-0 gaps are PLANNED in 21-01, not yet built)
created: 2026-06-16
---

# Phase 21 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> This phase is WinForms UI in the sibling repo (`TheJawaToolboxDotNet`); the codec/encoder/classifier
> it consumes are already covered by the Phase 20 + Phase 8 xUnit suites. New automated coverage is the
> tiered-candor string assert + an in-proc edit-save parity test. The live render-on-reload disposition
> (D-07) is the ONE non-automatable gate (maintainer live-smoke).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`) + native Catch2 (`UtinniCore.Tests`) |
| **Config file** | per-project `.csproj` |
| **Quick run command** | `dotnet test --no-build` (after VS2026 MSBuild on the touched project) |
| **Full suite command** | VS2026 MSBuild `Utinni.sln` (`/p:Configuration=Release /p:Platform=x86`) then `dotnet test --no-build` + native Catch2 |
| **Estimated runtime** | ~30–90 seconds (managed suites; build excluded) |

> Build note: use VS2026 MSBuild, NOT `dotnet build` (MSB3823 on WinForms `.resx`). Worktrees OFF — run waves inline.

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --no-build` on the touched test project.
- **After every plan wave:** Run full `dotnet test --no-build` + a cross-repo build of `TheJawaToolboxDotNet`.
- **Before `/gsd:verify-work`:** Full suite green + the D-07 maintainer live-smoke disposition recorded.
- **Max feedback latency:** ~90 seconds (excluding the manual live-smoke gate).

---

## Per-Task Verification Map

| Behavior | Requirement | Threat Ref | Test Type | Automated Command | File Exists | Status |
|----------|-------------|------------|-----------|-------------------|-------------|--------|
| `.trn` classifies as `ReloadTier.ReloadedTerrain` | PROD-W2-TRN-05 | — | unit | `dotnet test --no-build` | ✅ `UtinniCoreDotNet.Tests/Saving/ReloadAssetClassifierTests.cs` (add explicit `.trn` assertion if absent) | ⬜ pending |
| Edit→save byte-exact (one field span only) | PROD-W2-TRN-05 | T-21-save-containment | unit | `dotnet test --no-build` (apply-save-trn goldens) | ✅ Phase 20 `Utinni.Cli.Tests`; add in-proc parity test | ⬜ pending |
| Non-editable node (raw/dead) rejected | PROD-W2-TRN-05 | — | unit | `dotnet test --no-build` | ✅ Phase 20 (`ApplySaveTrnCommand`); mirror for in-proc path | ⬜ pending |
| Tiered candor copy correct per `ReloadTier` | PROD-W2-TRN-05 | — | unit (string assert) | `dotnet test --no-build` | ❌ W0 — assert editor status copy == locked strings per tier | ⬜ pending |
| Save stays inside loose-override `--root` containment | PROD-W2-TRN-05 | T-21-save-containment | unit | `dotnet test --no-build` | ✅ Phase 20 fail-closed `--root`; assert in-proc path enforces it | ⬜ pending |
| Live render-on-reload disposition (does edited `.trn` re-read in-session?) | PROD-W2-TRN-05 | — | **manual (maintainer)** | n/a — live SWG smoke | ❌ D-07 todo (precedent: phase10 SC3) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] A unit/string-assert that the Terrain editor's status footer maps each `ReloadTier`
      (`ReloadedTerrain` / `PendingNextSceneChange` / `Unavailable`) to the locked candor copy
      (default "live" disposition = `PendingNextSceneChange` per D-07 until the smoke says otherwise).
      *(PLANNED: 21-01 Task 1 — `TerrainReloadCandorTests`.)*
- [x] An in-proc edit-save parity test (UI save path produces byte-identical output to `apply-save-trn`
      for the same single-field edit) — required if the planner chooses the in-proc save path.
      *(PLANNED: 21-01 Task 2 — `TerrainInProcSaveParityTests`, covering BOTH a typed field AND the `--field active` IHDR path.)*
- [x] Confirm `ReloadAssetClassifierTests` has an explicit `.trn → ReloadedTerrain` assertion (add if missing). *(PLANNED: 21-01 Task 1 — `Classify_Trn_ReturnsReloadedTerrain`.)*

*Existing Phase 20 + Phase 8 infrastructure covers the codec/encoder/classifier behaviors.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Does an in-session edited `.trn` visibly re-render (D-07) | PROD-W2-TRN-05 | Native `ReloadTerrain` body not in our source; in-session procedural re-read is unknowable statically. Maintainer's loose `searchPath` currently disabled (phantom-walk mitigation). | Enable loose searchPath; inject; open a planet `.trn`; edit a fixed-length leaf; save (auto-reload) + manual Preview; observe whether terrain updates in-session, on next scene change, or not at all. Record which `ReloadTier` is the honest copy. Precedent: `phase10-stringtable-sc3-live-reload-residual.md`. |

---

## Validation Sign-Off

- [x] All tasks have automated verify or Wave 0 dependencies (except the D-07 manual gate)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (tiered-candor assert + in-proc parity test)
- [x] No watch-mode flags
- [x] Feedback latency < 90s
- [x] D-07 maintainer live-smoke task present and `autonomous: false`
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** plan-time validation PASSED (Wave-0 gaps PLANNED in 21-01; `wave_0_complete` flips after 21-01 executes; D-07 live-smoke is the lone deferred maintainer gate).
