# Phase 22 — Deferred / Out-of-Scope Items

Items discovered during execution that are NOT caused by this phase's changes (CLAUDE.md scope
boundary: only auto-fix issues directly caused by the current task's changes).

## 22-04 (ClientEffect editor)

### AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn — pre-existing harness artifact

- **Found during:** 22-04 Task 2 full `dotnet test --no-build` run (UtinniCoreDotNet.Tests).
- **Symptom:** `ADDED (0), REMOVED (20)` — the committed `UtinniCoreDotNet/Generated/UtinniCore.cs`
  is missing 20 ABI blocks relative to the blessed baseline `Fixtures/abi-baseline-blockhashes.txt`.
- **Root cause (NOT this plan):** the Phase 17 CPPS-04 ABI gate requires `UtinniCoreDotNetGen.exe`
  to be RUN as a post-build step so `Generated/UtinniCore.cs` is regenerated to its full surface; an
  incremental `msbuild /t:Build` (and `bin/Release/UtinniCoreDotNetGen.exe` being absent) skips the
  gen, so the test reads a stale/reduced committed Generated file. This is the documented harness
  reality (`[[project_phase17_cppsharp_v145_hardening]]`: "the ABI test needs UtinniCoreDotNetGen.exe
  RUN — incremental msbuild /t:Build skips the post-build gen → tests stale Generated file").
- **Why out of scope for 22-04:** this plan adds ZERO native headers, ZERO CppSharp generator
  changes, and ZERO bridged public surface. The Utinni-repo diff is a single managed test file that
  imports only `Formats.ClientEffect`/`Formats.Iff`. `git diff --stat` shows no tracked changes; the
  CppSharp bridge is untouched. The failure reproduces on clean `master` HEAD with the same
  incremental-build conditions. Re-blessing the ABI fixture here would be incorrect (the surface did
  not intentionally change) and requires the lockstep TJT/native rebuild the Phase 17 gate documents.
- **Disposition:** deferred — belongs to the ABI-gate / generator-run harness concern, not the
  ClientEffect editor. All ClientEffect codec + in-proc save-parity tests are green (53/53); the rest
  of the UtinniCoreDotNet.Tests suite is green (837/838 — the single failure is this item).
