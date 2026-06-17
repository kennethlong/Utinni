---
phase: 21-terrain-tjt-subpanel-best-effort-live-preview
verified: 2026-06-17T00:00:00Z
status: passed
score: 3/3 residuals closed (PROD-W2-TRN-05 holds, no regression)
scope: gap-closure (21-05/06/07 closing live-smoke residuals R1/R2/R3)
overrides_applied: 0
re_verification:
  note: "Feature (21-01..04) verified at original phase close. This run verifies the 3 gap-closure plans only."
  gaps_closed:
    - "R1 — high-era IHDR version-form deeper-nested active flag (read + write)"
    - "R2 — terrain loose override now composed under <root>/loose/"
    - "R3 — collapsed-section TerrainSubPanel hand-off resolves via WrappedSubPanel"
  gaps_remaining: []
  regressions: []
---

# Phase 21: Terrain TJT SubPanel (+ best-effort live preview) — Gap-Closure Verification

**Phase Goal:** A modder edits a planet's terrain inside The Jawa Toolbox and sees the change — live in-client where reachable, otherwise via an honestly-labeled save-then-reload.
**Requirement:** PROD-W2-TRN-05
**Verified:** 2026-06-17
**Status:** PASS
**Scope:** Gap-closure run — verifies plans 21-05 (R1), 21-06 (R2), 21-07 (R3) actually close the three 21-04 live-smoke residuals and do not regress the shipped feature.

## Per-Residual Verdict

### R1 — IHDR version-form deeper-nested active flag (21-05) — ✅ CLOSED

The 21-04 smoke found that on real high-era terrain (naboo.trn, PTAT/0014) the layer-item-header `DATA` leaf is nested `LAYR → version → IHDR → IHDR-version → DATA` — one form deeper than both the decoder read and the save-side resolver handled. The active flag read as the C++ default `true`, and the toggle threw `"no IHDR DATA child leaf"` on save.

| Check | Evidence | Status |
|-------|----------|--------|
| Decoder descends IHDR → version → DATA with direct-DATA fallback | `TgenDecoder.ReadLayerItemHeader` line 288: `leafSearchRoot = HasDirectLeaf(ihdr, "DATA") ? ihdr : (FirstContainerChild(ihdr) ?? ihdr)` then `EnumerateLeaves(leafSearchRoot, "DATA")` | ✓ VERIFIED |
| Save-side resolver mirrors the EXACT descent (read↔write parity) | `TerrainSaveTargets.ResolveIhdrLeafStableId` (production, lines 160-201): direct-DATA branch (a) then version-form fallback (b) via `FirstContainerChild(child)` + `DeriveStableId` re-derivation | ✓ VERIFIED |
| Local test-mirror updated to match production resolver | `TerrainInProcSaveParityTests.cs` lines 354-357: version-form `FirstContainerChild` descent present in the mirror | ✓ VERIFIED |
| RED-first fixture models the deeper nesting | `TgenFixtureSynthesizer.WithRealLayrIhdrVersion` (line 357), token `IhdrVersion` present | ✓ VERIFIED |
| Behavioral: active reads real flag, resolver hits leaf, toggle round-trips byte-exact | `RealLayrIhdrVersionShape_ActiveReadsReal_ResolverHitsLeaf_BothEditPathsSave` **PASSES** (4 assertions: real active read, non-null resolver leaf, active-toggle round-trip, typed AHCN round-trip) | ✓ VERIFIED |
| No regression to direct-DATA goldens | `RealLayrWrapperShape...` + `InProcActiveFlagEdit...` still PASS | ✓ VERIFIED |

Read and write descend the identical shape (direct-DATA preferred, version-form fallback), so they address the same leaf — the read↔write parity concern is satisfied in code, not just claimed.

### R2 — terrain override loose/ sub-dir composition (21-06) — ✅ CLOSED

The 21-04 smoke had to relocate the saved override by hand: `TerrainSaveTargets.SaveLooseOverride` resolved `LooseOverridePath.Resolve(resolvedRoot, relAssetPath)` with no `loose/` segment, so overrides landed at `<root>/terrain/…` off the documented searchPath.

| Check | Evidence | Status |
|-------|----------|--------|
| `looseOverrideSubDir` threaded through SaveLooseOverride | New param (line 297); two-step composition lines 345-367 mirroring `IffSaveTargets` verbatim (each leg its own try/catch → `SaveResult.Failure`) | ✓ VERIFIED |
| Empty subdir preserves legacy `<root>/<logical>` (no regression for empty callers) | `string.IsNullOrEmpty(looseOverrideSubDir) ? resolvedRoot : Resolve(...)` (line 348-350) | ✓ VERIFIED |
| Call site passes `"loose"` | `FormTerrainEditor` line 1050: `...pendingEdit.Value, ResolveLooseOverrideSubDir())`; `ResolveLooseOverrideSubDir()` returns `"loose"` (line 1220) | ✓ VERIFIED |
| Overwrite-confirm prediction points at the SAME destination | `PredictOverridePath` lines 1189-1193: same two-step `LooseOverridePath.Resolve(Resolve(root, subDir), tre.LogicalPath)` | ✓ VERIFIED |
| Framework test pins destination under `<root>/loose/` + escape rejection | `TerrainLooseOverridePathTests` **PASSES** | ✓ VERIFIED |
| Containment preserved (no `..`/rooted escape) | Both Resolve legs go through the fail-closed `LooseOverridePath` gate; escape `[Fact]` green | ✓ VERIFIED |

Byte-content parity with `apply-save-trn` is unchanged (same single-source codec; only the destination relocates) — the change is purely destination composition.

### R3 — collapsed-section TerrainSubPanel hand-off (21-07) — ✅ CLOSED

The 21-04 smoke had to expand the Terrain section first: `FindTerrainSubPanel` walked `Controls` recursively, but `CollapsiblePanel` realizes its SubPanel into `Controls` only on expand, so on a fresh (collapsed) session the walk returned null → "Terrain Editor is unavailable in this session."

| Check | Evidence | Status |
|-------|----------|--------|
| `CollapsiblePanel` exposes expand-state-independent accessor | `public SubPanel WrappedSubPanel { get { return subPanel; } }` (line 69); field held from ctor (line 62) | ✓ VERIFIED |
| Lazy-realize layout unchanged (no behavioral regression) | `Controls.Add(subPanel)` still gated on expand (line 88); accessor returns the field directly, sidesteps Controls | ✓ VERIFIED |
| Consumer consults `WrappedSubPanel` BEFORE the Controls walk | `FormTreBrowser.FindTerrainSubPanelIn` lines 402-407: `CollapsiblePanel` → `collapsible.WrappedSubPanel as TerrainSubPanel`, returned if non-null; direct-cast + recursive walk retained as fallback (lines 410-413) | ✓ VERIFIED |
| Framework regression proves reachability while collapsed | `CollapsiblePanelWrappedSubPanelTests` **PASSES** | ✓ VERIFIED |

The live in-session click is the only un-automatable leg; it is covered by the existing phase live-smoke path and was deliberately not re-checkpointed for this low-severity UX fix (per the 21-07 plan). The collapsed-state reachability — the actual root cause — is verified framework-side.

## Phase Goal (PROD-W2-TRN-05) — not regressed

| Success Criterion | Status | Evidence |
|---|---|---|
| TerrainSubPanel ships in TJT, opens from TRE Browser + loose override, saves via loose-override matrix | ✓ HOLDS | Verified at original close; R2 corrects the loose destination, R3 restores the TRE-Browser hand-off — both reinforce SC1, neither regresses it |
| Save degrades to honest save-then-reload (never standalone renderer) | ✓ HOLDS | `TerrainReloadCandor.LivePreviewObserved=false`, `PendingNextSceneChange` copy unchanged (21-04 D-07 disposition); no candor code touched by gap-closure |
| Heap-free hot path (no 0x0051fb0a crash); MEF-safe ctor; Dock.Fill/SplitContainer layout | ✓ HOLDS | No native/hot-path/MEF code touched by 21-05/06/07; scene-change guard held across the 21-04 live smoke |

## Test Evidence

| Suite | Result |
|---|---|
| Targeted gap-closure filter (R1/R2/R3 + parity goldens) | **6 passed / 0 failed** |
| `UtinniCoreDotNet.Tests` (full) | **784 passed / 1 failed** — sole failure is the known artifact below |
| `Utinni.Cli.Tests` (full, holds the synthesizer fixture) | **413 passed / 2 skipped (env)** — no fixture regression |

Test assemblies rebuilt 2026-06-17 09:55, after the source edits (09:36–09:53), so `--no-build` runs exercise the fixed code.

## Known Non-Blocking Failure — dispositioned

`AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn` — **FAIL, NON-BLOCKING, pre-existing.**

- This is the documented Phase-17 **CPPS-04 gotcha**: incremental MSBuild skips running `UtinniCoreDotNetGen.exe`, leaving a stale `Generated/UtinniCore.cs` so the surface diff trips. CI runs the generator and gates this; the local build does not.
- **Confirmed unrelated to this phase:** `git diff a42fc25..HEAD --name-only` shows the gap-closure work touched **zero** `Generated/UtinniCore.cs` and **zero** native `UtinniCore/` files (only `TgenDecoder.cs`, `CollapsiblePanel.cs`, three test files, one fixture, the UtinniPlugins files, and `.planning/` docs). ADDED ABI surface = 0.
- Treated as a known build-environment artifact, **not** a phase-blocking failure — consistent with the milestone's standing CI-gated disposition.

## Anti-Patterns

None. Debt-marker scan (`TBD|FIXME|XXX|not yet implemented|coming soon|PLACEHOLDER`) over all four modified source files: zero matches. All four referenced UtinniPlugins commits (`3a73165`, `110d065`, `10cc1c7`, `8b8c5e3`) and the Utinni commits exist; cross-repo paired commits intact.

## Human Verification

None required for this gap-closure run. The single un-automatable leg (live in-session "Open in Terrain Editor" click while collapsed) is low-severity UX, its root cause is verified framework-side, and the 21-07 plan explicitly waives a new live checkpoint. The honest save-then-reload candor disposition (D-07) was already maintainer-observed in the 21-04 live smoke.

## Final Verdict: PASS

All three live-smoke residuals (R1/R2/R3) are closed in code with read↔write/predict↔save parity verified, backed by RED-first regressions that now pass. The shipped feature (PROD-W2-TRN-05) is not regressed — gap-closure touched only the codec read/resolver, the loose-override composition, and a UI accessor, all additive with preserved fallbacks. The lone test failure is the known, CI-gated, ABI-stale-build artifact, provably untouched by this phase.

---

_Verified: 2026-06-17_
_Verifier: Claude (gsd-verifier)_
