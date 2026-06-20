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

### Form-internal regression tests need a UtinniPlugins test project + CI — follow-up

- **Found during:** 22-04 Task 3 live smoke (this surfaced four form-internal bugs the automated gates
  missed — blank window, Open/Save-As dialogs, missing TRE-menu wiring, reorder reselection).
- **What's covered now:** the TRE→Effects hand-off GATE is unit-tested in the existing
  `UtinniCoreDotNet.Tests` UITests lane via the new `EffectHandoffPolicy` (18 cases, CI-gated). The
  form-internal behavior (BuildContent no-throw, reorder reselection, dialog defaults, add/remove/undo/
  redo selection state) was validated by a process-isolated reflection harness during this session
  (26/26 paths green) but is NOT yet CI-gated.
- **Why out of CI:** `FormClientEffectEditor` lives in the sibling **UtinniPlugins** repo, which has NO
  test project and NO CI workflow, and the Utinni CI does not build UtinniPlugins — so a form-driving
  test has nowhere to run today. A faithful test also needs an STA + shown form (the form's `Shown`
  doesn't fire under `Application.Run` headless; the harness drove it via `Show()` + message pump).
- **Disposition:** follow-up — stand up a minimal WinForms test project + CI lane in UtinniPlugins, then
  promote the session harness (form construction no-throw + reorder/add/remove/undo/redo selection
  asserts) into it. Bounded work, but its own task (out of 22-04 scope).

### Root `bin/Release/utinni-cli.exe` is stale (pre-22-02) — deploy-copy gap

- **Found during:** 22-04 Task 3 D-14 dogfood. `roundtrip-effect` reported "Verb not recognized" from
  `bin/Release/utinni-cli.exe` (LastWrite 2026-06-13, version SHA `e059d165`), while the project output
  `Utinni.Cli/bin/Release/net472/utinni-cli.exe` (2026-06-19, SHA `0f0ce0f`) ran it correctly
  (`bytesIdentical: true`). So the root copy predates the 22-02 effect verbs.
- **Why it matters:** whatever copies the CLI to the repo-root `bin/Release/` did not refresh it on the
  current builds. Anything invoking the root copy (verify the `Utinni.Mcp` shell path) is running a CLI
  missing the 22-02 `effect-*` / `roundtrip-effect` / `decode-effect` verbs.
- **Disposition:** follow-up — confirm the MCP server's CLI path, and fix the build/deploy so the root
  `bin/Release/utinni-cli.exe` tracks the project output (or point all consumers at the project output).
