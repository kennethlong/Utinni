---
phase: 21-terrain-tjt-subpanel-best-effort-live-preview
plan: 05
type: execute
status: complete
autonomous: true
gap_closure: true
requirements: [PROD-W2-TRN-05]
subsystem: terrain-codec
tags: [terrain, trn, tgen, ihdr, active-flag, stable-id, cross-repo, tdd]
requires:
  - "Phase 20 TgenDecoder + TgenFieldLayouts + TrnFieldEncoder (single-source codec)"
  - "21-04 LAYR -> version stable-id descent (DecodeLayer + ResolveIhdrLeafStableId)"
provides:
  - "Active-flag read + write correctness on real high-era terrain (IHDR -> version -> DATA nesting)"
  - "WithRealLayrIhdrVersion fixture modelling the deeper IHDR-version nesting"
affects:
  - "UtinniCoreDotNet/Formats/Decoders/TgenDecoder.cs"
  - "D:/Code/UtinniPlugins/.../Saving/TerrainSaveTargets.cs"
tech-stack:
  added: []
  patterns: ["direct-leaf-preferred-with-version-form-fallback descent (read<->write leaf parity, concern #10)"]
key-files:
  created: []
  modified:
    - "Utinni.Cli.Tests/Fixtures/trn/TgenFixtureSynthesizer.cs"
    - "UtinniCoreDotNet.Tests/Formats/Terrain/TerrainInProcSaveParityTests.cs"
    - "UtinniCoreDotNet/Formats/Decoders/TgenDecoder.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/TerrainSaveTargets.cs"
decisions: []
metrics:
  duration: ~14 min
  tasks: 3
  files: 4 (2 repos)
  date: 2026-06-17
---

# Phase 21 Plan 05: Close R1 — IHDR version-form deeper-nested active flag Summary

Restored active-flag read + write correctness on real high-era terrain by making both `TgenDecoder.ReadLayerItemHeader` and `TerrainSaveTargets.ResolveIhdrLeafStableId` descend `IHDR -> <IHDR-version FORM> -> DATA` (with a direct-DATA fallback), so the editor reads the real on-disk active flag (not the C++ default `true`) and the active-flag toggle saves byte-exact — RED-then-GREEN against a new deeper-nesting synthesizer fixture, with every shipped collapsed/direct-DATA golden preserved.

## Self-Check: PASSED

- Created/modified files all exist on disk (verified below).
- All three task commits exist in git log (verified below).

## What was wrong (R1)

On real high-era terrain (naboo.trn, `PTAT/0014`) the layer-item-header DATA leaf is one version-form deeper than the 21-04 fix handled: `LAYR -> <layer-version FORM> -> IHDR -> <IHDR-version FORM> -> DATA`. The 21-04 fix added the `LAYR -> layer-version` descent (fixing the typed-field path, validated live), but both the decoder and the save-side resolver still assumed `DATA` was a DIRECT child of `IHDR`. Result: the active flag silently read as the C++ default `true` (editor showed the default, not the real flag), and the active-flag toggle threw `LAYR FORM '...' has no IHDR DATA child leaf (cannot address active flag)` on save.

## How it was fixed (RED -> GREEN)

**Task 1 (RED, `17e1f1d`):**
- New `TgenFixtureSynthesizer.WithRealLayrIhdrVersion(active, name)` models the deeper shape (`IHDR -> FORM:0001 -> DATA`), distinct from `WithRealLayrWrapper` (whose DATA is a direct IHDR child). Uses the literal token `IhdrVersion` per the must-have artifact grep.
- New `[Fact] RealLayrIhdrVersionShape_ActiveReadsReal_ResolverHitsLeaf_BothEditPathsSave` (seeded `active: false, name: "alpha"`) asserts the real active read, a non-null IHDR leaf id that re-locates a Leaf, the active toggle round-trip, and the typed AHCN byte-exact round-trip.
- Confirmed RED: failed on `Assert.False(doc.Layers[0].Active)` (read default `true` instead of seeded `false`).

**Task 2 (GREEN decoder, `b96167c`):**
- `ReadLayerItemHeader` now chooses its leaf-search root: prefer a DIRECT IHDR DATA leaf (collapsed / current direct-DATA fixtures), else descend one level via `FirstContainerChild(ihdr)` to the IHDR version FORM. Added `HasDirectLeaf` helper. After this task the active-read assertion passed; the failure moved to the resolver assertion (expected — the mirror still needed Task 3).

**Task 3 (GREEN resolver, paired cross-repo — Utinni `c3d4c0a`, UtinniPlugins `3a73165`):**
- Production `TerrainSaveTargets.ResolveIhdrLeafStableId` and the local test mirror both gained the matching descent: direct-DATA preferred, else `FirstContainerChild(ihdr)` to the IHDR version FORM, re-derive its stable id via `MutableIffDocument.DeriveStableId(versionForm, ihdrPrefix, IndexOf(...))`, then resolve its DATA leaf. The "no IHDR DATA child leaf" throw now fires ONLY when neither the direct nor the version-form DATA leaf exists. Read and write descend identically, so they address the SAME leaf (concern #10).

## Verification

- `RealLayrIhdrVersionShape_...`: RED at Task 1 (active read), GREEN after Task 3 (all four assertions).
- `RealLayrWrapperShape`, `InProcActiveFlagEdit`, `ActiveFlag` parity tests: stayed green (direct-DATA fallback intact — no golden regression).
- Managed solution built with VS2026 MSBuild (`-p:Configuration=Release -p:Platform=x86`); UtinniPlugins TJT built clean in the same wave.
- `UtinniCoreDotNet.Tests`: 781 passed / 1 failed (see Deferred Issues). `Utinni.Cli.Tests`: 413 passed / 2 skipped.
- Task 2 grep: `grep -v '^#' TgenDecoder.cs | grep -c "FirstContainerChild(ihdr)"` = 2 (>= 1).
- Task 3 grep (comment lines filtered): `FirstContainerChild` count = 3 in the test mirror AND 3 in the production resolver (>= 1 each).
- `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs` done before each commit (CppSharp churn never staged).

## Deviations from Plan

None — plan executed exactly as written (3 tasks, RED-then-GREEN, paired cross-repo commit). No deviation rules triggered.

## Deferred Issues (out of scope — pre-existing)

- **`AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn` is RED (pre-existing).** Drift this run: ADDED (0) / REMOVED (20) — the committed Generated surface lacks 20 blocks the blessed baseline expects; ADDED=0 proves 21-05 introduced no native surface. `git diff a42fc25 HEAD` confirms the 21-05 commits touched neither `Generated/UtinniCore.cs` nor `Fixtures/abi-baseline-blockhashes.txt` (last edited in Phase 06-02 / Phase 17 respectively). Known Phase-17 gotcha: the ABI gate needs `UtinniCoreDotNetGen.exe` RUN; an incremental `msbuild /t:Build` skips the post-build gen, leaving the on-disk Generated file stale vs the blessed baseline. CI (which runs the gen) gates master. Remedy is the Phase-17-owned lockstep re-bless, not a 21-05 task. Logged in `deferred-items.md` (21-01 + 21-05 entries).

## Commits

- `17e1f1d` test(21-05): RED fixture+test for IHDR version-form deeper-nested active flag
- `b96167c` fix(21-05): TgenDecoder descends IHDR -> version -> DATA for active flag
- `c3d4c0a` fix(21-05): local-mirror ResolveIhdrLeafStableId descends IHDR -> version -> DATA
- `3a73165` (UtinniPlugins) fix(21-05): ResolveIhdrLeafStableId descends IHDR -> version -> DATA (R1)

## R1 status

Closed in code + a RED-then-GREEN regression test. The deeper-shape live re-validation on real naboo.trn remains a maintainer-only live smoke (no headless path) — the codec correctness is now pinned by the `WithRealLayrIhdrVersion` fixture so the live check is a confirmation, not a discovery.
