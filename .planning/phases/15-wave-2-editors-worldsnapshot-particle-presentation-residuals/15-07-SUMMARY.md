---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 07
subsystem: saving / reload-candor
tags: [RESID-03, reload-classifier, candor, worldsnapshot, particle, D-14]
requires:
  - "ReloadAssetClassifier 4-tier routing (Phase 8 Plan 05 Task 3)"
  - "15-UI-SPEC §Reload Candor Contract (LOCKED badge copy)"
provides:
  - "Honest tier-(b) PendingNextSceneChange classification for .ws + .prt reload paths"
  - "Named WorldSnapshotExtensions / ParticleExtensions routing sets (grep-gated)"
  - "ParticleSnapshotReloadRoutingTests routing-test map for the two new editors"
affects:
  - "FormSnapshotPlacements reload badge (backed by .ws tier-(b))"
  - "FormParticleEditor degraded reload badge (backed by .prt tier-(b))"
  - "15-08 live SC3 render-on-reload smoke (consumes this classification)"
tech-stack:
  added: []
  patterns:
    - "Extend the shipped ReloadAssetClassifier routing table; never loosen the conservative unknown fallback"
    - "Honesty-guard test: a new reload path may not classify as an instant/live (texture/terrain) tier"
key-files:
  created:
    - "UtinniCoreDotNet.Tests/SavingTests/ParticleSnapshotReloadRoutingTests.cs"
  modified:
    - "UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs"
decisions:
  - "WorldSnapshotExtensions={.ws} + ParticleExtensions={.prt} are EXPLICIT named sets routed to tier-(b), not left to the conservative fallback — so a grep-gated acceptance check + a named regression test exist (mirrors StringtableExtensions / ObjectTemplateExtensions precedent)"
  - "The Particle LIVE-capable badge (Re-triggers live instances on Preview.) is NOT a classifier tier — it is a form-side runtime affordance gated on the 15-03 retrigger hook + Game.IsRunning. The classifier only owns the honest tier-(b) FLOOR (the degraded copy)."
metrics:
  duration: ~20 min
  completed: 2026-06-07
  tasks: 1
  files: 2
---

# Phase 15 Plan 07: RESID-03 Reload Candor for .ws/.prt Summary

Extended the shipped `ReloadAssetClassifier` so the two new Wave-2 editor reload paths — WorldSnapshot (`.ws`) and Particle (`.prt`) — classify with honest tier-(b) `PendingNextSceneChange` candor (D-14), backing the LOCKED Reload Candor Contract badge copy without over-promising, while preserving the conservative unknown fallback exactly.

## What Shipped

- **`ReloadAssetClassifier.cs`** — two new named routing sets (`WorldSnapshotExtensions = { ".ws" }`, `ParticleExtensions = { ".prt" }`) and an explicit routing branch sending both to `ReloadTier.PendingNextSceneChange` (tier-(b)), placed BEFORE the `.iff` carrier branch and the conservative fallback. The fallback branch (`return ReloadTier.PendingNextSceneChange` for everything else, plus the null/empty short-circuit) is byte-for-byte unchanged. The class doc-comment routing-table list now records both new sets.
- **`ParticleSnapshotReloadRoutingTests.cs`** — 9 facts mirroring `StringTableReloadRoutingTests` / `DatatableReloadRoutingTests`:
  - Test 1 (`.ws`): classifies tier-(b); case-insensitive (`.WS`/`ws`); `.ws ∈ WorldSnapshotExtensions`.
  - Test 2 (`.prt`): classifies tier-(b) in the degraded case; case-insensitive; `.prt ∈ ParticleExtensions`.
  - Test 3 (conservative fallback UNCHANGED): unknown ext (`.xyz`, `.totallyunknown`) + null + empty all still route conservatively to tier-(b).
  - Test 4 (honesty guard, `[Theory]` over `.ws`/`.prt`): neither new extension classifies as `ReloadedTextures`/`ReloadedTerrain`; both equal `PendingNextSceneChange`.

## Honesty Basis (LOCKED copy this classification backs)

| Path | Classifier tier | Badge copy (15-UI-SPEC, LOCKED) |
|------|-----------------|----------------------------------|
| WorldSnapshot `.ws` | `PendingNextSceneChange` (tier-b) | `Placements re-resolve on the next scene change.` |
| Particle `.prt` (degraded) | `PendingNextSceneChange` (tier-b) | `Reloads on next scene change or relog.` |
| Particle live-capable | NOT a classifier tier | `Re-triggers live instances on Preview.` (form-side, 15-03 hook + Game.IsRunning) |

## Verification

- `dotnet test UtinniCoreDotNet.Tests --no-build --filter ReloadRouting` → **15/15 passed** (3 StringTable + 3 Datatable + 9 new). VS2026 MSBuild Debug|x86 build green.
- TDD discipline: the new test file references `WorldSnapshotExtensions`/`ParticleExtensions`, which did not exist before the classifier edit — so RED was a genuine compile-fail until GREEN added the named sets (true RED→GREEN, no REFACTOR needed).
- Generated/UtinniCore.cs: no CppSharp regen churn this build (test-project build does not trigger it); nothing to revert.

## Deviations from Plan

None — plan executed exactly as written. Both `files_modified` paths match; the single TDD task delivered all 4 specified behaviors.

## Scope Note (per repo_note)

This is the AUTOMATABLE half of RESID-03 only: the classifier routing table + test map. The live SC3 render-on-reload observation for `.stf`/`.ot` (and confirming the `.ws`/`.prt` edits actually render on reload vs relog-only) is the Tier-4 maintainer smoke folded into 15-08 — not attempted here.

## Known Stubs

None. No hardcoded empty values, placeholder text, or unwired data sources introduced.

## Self-Check: PASSED

- FOUND: UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs (modified)
- FOUND: UtinniCoreDotNet.Tests/SavingTests/ParticleSnapshotReloadRoutingTests.cs (created)
- FOUND commit: c63122a feat(15-07): route .ws/.prt to honest tier-(b) reload candor (RESID-03)
