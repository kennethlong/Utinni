# Deferred Items — Phase 20

## Pre-existing (out-of-scope) — AbiSurfaceTests ABI drift

- **Test:** `UtinniCoreDotNet.Tests.AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn`
- **Symptom:** ADDED(0) / REMOVED(20) vs the blessed `abi-baseline-blockhashes.txt`.
- **Cause:** the CppSharp-regenerated `Generated/UtinniCore.cs` on this machine drifts from the
  Phase-17-frozen blessed baseline (known Phase-17 gotcha: the ABI gate needs `UtinniCoreDotNetGen.exe`
  to RUN + a re-bless; an incremental build + the mandated `git checkout -- Generated/UtinniCore.cs`
  leaves a stale/divergent file). **ADDED=0 proves Plan 20-03 introduced zero native ABI surface** —
  the entire delta is regen/reorder churn, not new bindings.
- **Why not fixed here:** Plan 20-03 touches only managed `Formats/Terrain/*.cs` + CLI verbs (zero
  native/Generated/ABI-baseline files). Re-blessing requires a TJT rebuild + fixture re-freeze, which is
  a maintainer-checkpoint operation (Phase-17 scope), not Wave-2 terrain-codec scope.
- **Action:** maintainer to re-run the gen EXE + `AbiBlockHash.Rebless` + re-freeze the fixture, or run
  the ABI lane only on a CI build that does not revert the regenerated file.
