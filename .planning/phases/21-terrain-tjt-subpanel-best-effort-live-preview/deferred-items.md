# Phase 21 — Deferred Items (out-of-scope discoveries)

> Logged per the executor SCOPE BOUNDARY rule. These are pre-existing issues NOT caused by this
> phase's tasks; do NOT fix them inside Phase 21 plan execution.

## 21-01

- **`AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn` is RED (pre-existing).**
  - Discovered: 21-01 Task 2 (full `UtinniCoreDotNet.Tests` suite run, per the per-wave sampling rate).
  - Result: 779 passed / 1 failed. The lone failure is the Phase-17 CPPS-04 ABI gate.
  - Why out of scope: Plan 21-01 adds ONLY managed `Saving/TerrainReloadCandor.cs` + tests and a
    cross-repo `TerrainSaveTargets.cs` — it adds ZERO CppSharp bindings, so the Generated ABI surface
    is untouched. Both the committed `UtinniCoreDotNet/Generated/UtinniCore.cs` AND the blessed baseline
    `UtinniCoreDotNet.Tests/Fixtures/abi-baseline-blockhashes.txt` are byte-unchanged since commit
    `93ded43` (the commit immediately before this plan's work began) — the drift predates Phase 21.
  - Root cause (known gotcha, `[[project_phase17_cppsharp_v145_hardening]]`): the ABI test needs
    `UtinniCoreDotNetGen.exe` to be RUN; an incremental `msbuild /t:Build` skips the post-build gen, so
    the built/Generated surface and the blessed baseline fall out of sync. Re-bless lockstep
    (rebuild TJT, re-freeze fixture, commit together) is the Phase-17-owned remedy — not a Phase-21 task.
  - Action: NONE in Phase 21. Surfaced here for the verifier; the 21-01 acceptance gates
    (`TerrainReloadCandor` / `Classify_Trn` / `TerrainInProcSaveParity`) are all green.

## 21-05

- **`AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn` STILL RED (pre-existing).**
  - Discovered: 21-05 full `UtinniCoreDotNet.Tests` suite run — 781 passed / 1 failed (the same lone ABI gate).
  - Drift detail this run: ADDED (0) / REMOVED (20) — the committed Generated surface lacks 20 blocks the
    blessed baseline expects; ADDED=0 proves nothing 21-05 introduced any native surface.
  - Why out of scope: Plan 21-05 changes ONLY managed C# (`TgenDecoder.cs`, `TerrainInProcSaveParityTests.cs`,
    cross-repo `TerrainSaveTargets.cs`) + a test fixture; it adds ZERO CppSharp bindings. `git diff a42fc25 HEAD`
    confirms the 21-05 commits touched neither `Generated/UtinniCore.cs` nor the baseline fixture.
  - Same root cause + remedy as the 21-01 entry above (Phase-17-owned re-bless; needs `UtinniCoreDotNetGen.exe`
    RUN — the incremental build used here skips the post-build gen). CI (which runs the gen) gates master.
