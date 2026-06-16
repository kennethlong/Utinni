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
