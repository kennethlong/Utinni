# Phase 2: Critical bug burn-down (C-01..C-15) - Context

**Gathered:** 2026-05-16
**Status:** Ready for planning

<domain>
## Phase Boundary

Close all 15 enumerated critical bugs from `docs/ai/assessment.md` (C-01..C-15) plus one parked addition (C-16, CON-O-03 delegate-pinning in `GameCallbacks.cs`) plus the line-of-code follow-through of one open-question resolution (KB-05, `||` vs `&&` at `game.cpp:305-308`). Honor class-of-bug constraints CON-H-01..-05, CON-L-01..-04, CON-B-04, CON-D-01 going forward. CI from Phase 1 (`windows-2022`, Release/x86, `dotnet test`) must stay green across the burn-down — every fix lands behind a green build.

Phase 2 produces 4 plans by risk tier:
- **Plan 02-01 — Trivial criticals:** C-04 (queue drain), C-06 (plugin loader isolation), C-08 (Hotkey TryParse), C-12 (VSIX VS 2019 → 2022), C-13 (TJT Debug path — cross-repo), C-14 (utinni.cfg blank login).
- **Plan 02-02 — Single-file criticals:** C-02 (cross-CRT delete[]), C-03 (`Network::cast` uninitialized), C-05 (`GameDragDropEventHandlers` static-field), C-07 (`UndoRedoManager` thread-safety + dead `AllowMerge` + `RedoCommands.Clear` ordering), C-10 (`clr::stop` null deref), C-11 (DirectX9 `findPattern` null check), C-15 (CppSharp `slnDir` brittle), **C-16** (`GameCallbacks` delegate-pinning via `GCHandle.Alloc`), KB-05 (`isSafeToUse` operator).
- **Plan 02-03 — Architectural: C-01** (DllMain loader-lock). Starts with a research substep before plan tasks are generated.
- **Plan 02-04 — Architectural: C-09** (UI / game-thread busy-wait deadlock).

**In scope (this phase):** code fixes for C-01..C-15 + C-16 + KB-05; regression tests in `UtinniCoreDotNet.Tests` for every managed-side fix; harness scaffolding (P/Invoke into `UtinniCore.dll`, WinForms `Panel` fixtures, file-content checks, VSIX manifest schema asserts, process-isolated `LoadLibrary` timing helper for C-01); cross-repo commit + PR in `UtinniPlugins` for C-13; the `UndoRedoManager` testability refactor that was explicitly deferred from Phase 1 (inject `GameCallbacks.AddCleanupSceneCall` behind an `Action<Action>` ctor param, paired with C-07 fix); inline disposition updates in `docs/ai/assessment.md` §"Open questions" for CON-O-01, -02, -03, -04 (researcher answers or default fallback).

**Out of scope (this phase):**
- C++ Catch2 unit tests (Phase 5 — TEST-02).
- CLI shim and golden fixtures (Phase 4 — TEST-03).
- Strategic reworks R-A..R-H (Phase 3 — STAB-02), even where they overlap a C-NN file (e.g., `GameCallbacks.cs` symmetric Add/Remove pass = R-A territory, separate from C-16 delegate-pinning fix).
- The `~30 cleanups + dep bumps` (Phase 6 — STAB-03), including TD-25 empty stub files, TD-26 disabled hooks in detour tables, TD-27 hardcoded font path, TD-28 `TJT.ico` framework-default leak, `.clang-format` adoption, imgui / spdlog / ImGuizmo version bumps.
- DXSDK June 2010 install on CI runner + multi-config matrix (Phase 6 — bundled with CON-O-08 decision).
- UtinniPlugins CI bootstrap (no GitHub Actions workflow added there in Phase 2; first Wave-1 plugin phase or earlier dedicated phase owns that).
- Open questions CON-O-05 (`StdEdited.cs` curation → Phase 3 R-F), CON-O-06 (LeksysINI replacement → Phase 6), CON-O-07 (Sytner's plugin → Phase 3 R-B), CON-O-08 (DXSDK replaceability → Phase 6 STAB-03), CON-O-09 (fixture storage → Phase 4), CON-O-11 (CLI distribution → Phase 4). CON-O-10 already resolved Phase 1.
- Plaintext password storage (SEC-01), `VirtualAllocEx PAGE_EXECUTE_READWRITE` (SEC-02), AV/EDR false-positive surface (SEC-03), plugin signing (SEC-04, MCF-04) — out of V1 entirely or Phase 6 cleanup pass.
- Branch protection rules on `master` (admin action, document as user task, not a code deliverable).

</domain>

<decisions>
## Implementation Decisions

### Plan Structuring (4 plans, risk-tier grouped)
- **D-01:** Plans are grouped by risk tier per `docs/ai/assessment.md` §"Recommended sequencing": trivial criticals (Plan 02-01), single-file criticals (Plan 02-02), architectural (Plans 02-03, 02-04). CI gates each plan boundary; the next plan does not start until the prior plan's CI is green on `master`.
- **D-02:** The two architectural fixes (C-01, C-09) are SPLIT into separate plans (02-03 and 02-04, not combined). They share no code, each is multi-day with mostly-manual verification, and each gets its own research/plan/execute lane. Rejected: combined arch plan (less revertable, larger PR), defer-C-09-to-2.1 (violates ROADMAP success criterion 1).
- **D-03:** Each bug fix lands as its own atomic commit (GSD executor default) — ~16 fix commits across the four plans plus harness/test commits. Within a plan, bug order is researcher/planner's call.

### Verification Contract (max-harness posture)
- **D-04:** Managed-side fixes (C-04, C-06, C-07, C-08, C-10 reflection-discoverable bits, C-15, C-16) MUST land with at least one xUnit regression test in the SAME commit as the fix. Test must be red before the fix is applied and green after. Matches Phase 1 `Hotkey.ProcessString` precedent.
- **D-05:** **Max-harness posture for not-unit-testable-out-of-the-box bugs.** Every C-NN bug gets a harness unless physically impossible. Per-bug shapes:
  - **C-04 (queue drain):** pure managed xUnit on `GroundSceneCallbacks.DequeuePostDrawLoopCalls` — drain-the-right-queue assertion.
  - **C-05 (drag-drop):** WinForms `Panel` test fixture; synthesize `DragEventArgs`; assert subscriber receives event.
  - **C-06 (plugin loader):** `/Fixtures/BrokenPlugin/` deliberately-broken DLL; assert surviving plugins still load AND broken-plugin name surfaces in log output.
  - **C-08 (Hotkey TryParse):** already covered by Phase-1 smoke test target (`HotkeyTests.cs` malformed-input case — likely red on master today, turns green when C-08 fix lands).
  - **C-10 (clr::stop):** P/Invoke `clr::stop()` from `UtinniCore.dll`; call twice from xUnit; assert no AV.
  - **C-11 (findPattern):** P/Invoke `utility/memory::findPattern` with a buffer where the pattern is absent; assert return is 0 AND call site at `directx9.cpp:297-303` bails with `log::critical` rather than `memcpy`-from-0x2.
  - **C-14 (utinni.cfg):** xUnit file-content assertion on `data/utinni.cfg` — `loginServerAddress0=` is blank, `loginServerPort0=` is blank.
  - **C-15 (CppSharp slnDir):** refactor `slnDir` resolution into a pure function; xUnit with synthetic paths (CI-runner-style path without `\bin\`, `$(SolutionDir)` arg path, env-var fallback path).
  - **C-12 (VSIX widening):** xUnit asserts `source.extension.vsixmanifest` XML has `InstallationTarget Version="[16.0,18.0)"`. Actual install into VS 2019 + VS 2022 IDEs stays manual (`Verified-by:` block in commit message).
  - **C-01 (DllMain loader-lock):** sibling helper-exe project does `LoadLibrary("UtinniCore.dll")` in process-isolation; harness measures `DllMain` entry-exit timing; assert `<` threshold (e.g., 50 ms). Partial proof — full "no deadlock under loader-lock contention" remains manual but timing harness catches "heavy work inside DllMain" regression class.
  - **C-09 (UI/game-thread busy-wait):** mock signaller harness; assert `ManualResetEventSlim.WaitOne` returns within timeout AND no `Thread.Sleep(1)` spin observed.
  - **C-02 (cross-CRT delete[]):** wrap the buffer-free as a testable free function; xUnit calls it with a synthetic SWG-style buffer + asserts the correct allocator is invoked. Partial proof — CRT-mismatch detection without a CRT-mismatch fixture is hard.
  - **C-03 (Network::cast):** test the post-condition wrapper only; the cast itself calls into SWG at hard RVA `0xAA4900` and stays manual. Hardest case.
  - **C-07 (UndoRedoManager):** xUnit on thread-safety (concurrent push from N threads + assert no races), `AllowMerge` contract (disposition: call it before `Peek().Merge` OR remove from interface — researcher decides), and `RedoCommands.Clear` ordering (assert Clear happens AFTER merge check, not before). Requires the testability refactor from Phase 1 deferred work.
  - **C-13 (TJT Debug path):** cross-repo commit; no automated test in this repo. Manual verify: build TJT Debug locally + confirm output appears in `bin/Debug/`.
  - **C-16 (delegate-pinning):** P/Invoke test that registers a delegate via `GameCallbacks.Add*Call`, forces `GC.Collect()` between registration and unmanaged invocation, asserts no AV.
- **D-06:** **The truly-manual residual after max-harness:** C-12 IDE install confirmation (must be done by the maintainer in both VS 2019 and VS 2022); C-13 cross-repo build (UtinniPlugins has no CI); C-03 live-injection cast against SWG; C-01 full proof of no deadlock under loader-lock contention. Everything else has a CI harness gate.
- **D-07:** **Single test project absorbs everything.** `UtinniCoreDotNet.Tests` (the Phase-1 project) gets `System.Windows.Forms` ref (for C-05 panel fixture), P/Invokes into `UtinniCore.dll` (C-10, C-11, partial C-02, C-16), and a `/Fixtures` subdir for synthetic broken plugins and `utinni.cfg` samples. A new sibling project (working name: `Utinni.LoaderLockHarness` — researcher names it final) houses the C-01 process-isolated `LoadLibrary` timing exe. Matches Phase-1 flat-root precedent (sibling project in `Utinni.sln`).

### C-01 Architectural Approach (deferred to researcher)
- **D-08:** **Researcher picks the C-01 fix path during Plan 02-03's research substep.** Three candidate paths:
  - (a) Export `utinni_init`; launcher does `LoadLibraryA` → `GetProcAddress("utinni_init")` → `CreateRemoteThread`. Small native + small launcher delta.
  - (b) Defer all heavy startup until the first SWG callback (`Game::install` detour fires). No launcher change, larger native restructure.
  - (c) Hybrid: `utinni_init` exported AND `Game::install` detour as fallback. More surface, more failure modes.
  Researcher audits launcher injection timing (`Launcher/main.cpp:174-210` `inject`, `:298-368` `loadDll`), `Game::install` detour mechanics, and CLR bring-up dependencies, then recommends. Launcher changes (`Launcher/main.cpp`) are conditionally in Plan 02-03 scope depending on the pick — launcher lives in this repo so no cross-repo concern. Phase 2 success criterion #4 (DllMain no heavy startup, CLR deferred) is a hard requirement — no defer-to-V2 off-ramp.

### Out-of-list Scope (C-13 cross-repo, C-16, KB-05, open-Qs)
- **D-09:** **C-13 is a cross-repo direct commit during Phase 2 execution.** Executor performs the path fix (`..\..\..\..\` → `..\..\..\` at `TheJawaToolbox.vcxproj:63`) and restores the missing `Debug|Win32.Build.0` `.sln` entry in `UtinniPlugins`. Separate clone, separate commit, separate PR. No CI workflow added to UtinniPlugins as part of Phase 2 (that's first-Wave-1-phase or earlier dedicated phase territory). Plan 02-01 tracks the C-13 task with an explicit `repo:UtinniPlugins` flag. Manual verification: maintainer builds TJT Debug locally + confirms output lands in `bin/Debug/`.
- **D-10:** **C-16 added — `GameCallbacks` delegate-pinning fix (resolves CON-O-03).** Not in assessment.md C-01..C-15 but parked in Phase 2 by ROADMAP. Lands in Plan 02-02 as a 16th task. Researcher confirms fix shape (audit every `Add*Call` delegate-passed-to-native site across `GameCallbacks`, `GroundSceneCallbacks`, `ObjectCallbacks`; likely `GCHandle.Alloc(handler, GCHandleType.Normal)` per assessment.md Open Question #3). Closes the open question AND the latent bug in the same commit.
- **D-11:** **KB-05 (`||` vs `&&` operator at `game.cpp:305-308`) folds into the CON-O-01 disposition commit.** No separate C-17 numbering. Plan 02-02 lands the one-line operator change in the same task that updates the open-question disposition in `docs/ai/assessment.md`. Resolves the discrepancy between code (`||`) and `docs/ai/internals.md:231` ("AND ... Both must be true").
- **D-12:** **CON-O-01, -02, -04 dispositions:** researcher investigates during each plan's research substep (git archaeology on this fork + upstream `ptklatt/Utinni`, IDA spot-checks where applicable, in-tree comment trawling). If an answer surfaces, it's cited in commit + folded into a `docs/ai/assessment.md` §"Open questions" disposition update. If unanswerable, the safe-default contract applies:
  - **CON-O-01 (`isSafeToUse` `||` vs `&&`)** — default: use `&&` per `docs/ai/internals.md:231` ("AND ... Both must be true"). Code at `game.cpp:307` changes from `||` to `&&` with a comment explaining the disposition.
  - **CON-O-02 (was `AddPostDrawLoopCall` ever used?)** — default: assume it IS used (treat the C-04 fix as load-bearing rather than dead-code-removal). Land the queue-drain fix; add the `Drain(ConcurrentQueue<Action>)` helper per assessment.md.
  - **CON-O-04 (VS 2019 pin rationale)** — default: audit the VS 2022 build of the VSIX templates for actual issues (x86 + CLR hosting + `Microsoft.VisualStudio.SDK` PackageReference upgrade). If clean, widen `InstallationTarget` to `[16.0,18.0)` per C-12 fix. If broken, document the broken case as the disposition + scope a narrower widening.

### Claude's Discretion
- Exact xUnit test naming for new tests in each task (follow Phase-1 `[Method]_[Scenario]_[ExpectedOutcome]` convention; class is `<TypeName>Tests.cs`).
- Per-bug task ordering WITHIN a plan (researcher/planner picks based on dependency, e.g., C-06 before C-08 within Plan 02-01 since C-06 surfaces C-08 errors cleanly).
- Whether C-15 (CppSharp `slnDir`) lands in Plan 02-02 first (since CI is already calling `UtinniCoreDotNetGen.exe` per Phase-1 CI workflow and a broken `slnDir` would block CI) or follows assessment-week sequence — researcher confirms whether Phase-1 CI is already affected by C-15 and prioritizes accordingly.
- Sibling helper-exe project name for the C-01 timing harness (working name `Utinni.LoaderLockHarness`; final name researcher's call).
- Whether the `UndoRedoManager` testability refactor (Phase-1 deferred work) lands as a SEPARATE task or as part of the C-07 fix task within Plan 02-02 (planner's call based on commit-size hygiene).
- Whether the C-16 delegate-pinning fix is one task (entire `GameCallbacks` + `GroundSceneCallbacks` + `ObjectCallbacks` audit + fix) or three tasks (one per file).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context (locked decisions, requirements, constraints)
- `.planning/PROJECT.md` — V1 milestone scope, anti-goals (DEC-A1..A4), preservation guard-rails (24 load-bearing foundations), class-of-bug constraints CON-H-01..-05, CON-L-01..-04, CON-B-04, CON-D-01 (all directly relevant to this phase).
- `.planning/REQUIREMENTS.md` §STAB-01 — Fix 15 critical bugs requirement (the requirement this phase delivers); §STAB-04 — preservation cross-cutting; §STAB-05 — open-question dispositions (mapped to Phase 6 but four are scoped here per ROADMAP).
- `.planning/ROADMAP.md` §"Phase 2" — phase goal, success criteria (#1 every C-NN closed, #4 DllMain no heavy startup), preservation guard-rails (CON-M-05 UndoRedo cleanup, CON-N-04 memory VirtualProtect, CON-N-01 detour-table).
- `.planning/intel/constraints.md` — CON-H-01..-05 class-of-bug constraints; CON-N-01 (detour-table pattern); CON-N-04 (`utility/memory::copy` VirtualProtect bracket); CON-M-05 (`UndoRedoManager.OnCleanupCallback` cleanup-on-scene-cleanup); CON-O-01..-04 (open questions scoped here); CON-D-01 (blank login default).
- `.planning/intel/decisions.md` — non-locked candidate decisions; D-08 tiered testing strategy is the philosophical home for the "max-harness, no Catch2 yet" stance taken here.
- `.planning/phases/01-ci-tier-1-c-scaffold/01-CONTEXT.md` — carried-forward decisions: D-01 sibling test project convention; D-02 `net472`/x86; D-03 xUnit 2.9.x; D-04 test naming; D-06 UndoRedoManager testability refactor explicitly deferred to Phase 2 alongside C-07; D-08 DXSDK NOT on CI runner; D-09 no multi-config matrix.

### Source documents (assessment, internals, vision)
- `docs/ai/assessment.md` §"Critical issues" (C-01..C-15 enumeration with file paths and fix approaches) — primary source.
- `docs/ai/assessment.md` §"Open questions" (CON-O-01..-08; this phase scopes -01, -02, -03, -04).
- `docs/ai/assessment.md` §"Recommended sequencing" (Week 1 trivial / Week 2 single-file / Week 3 architectural — D-01 plan grouping is directly derived).
- `docs/ai/assessment.md` §"Status tracking" — table to update as each C-NN closes during execution.
- `docs/ai/internals.md` §"isSafeToUse" (line ~231 — documents `&&` semantics; KB-05 / CON-O-01 disposition cites this).
- `docs/ai/vision.md` — anti-goals (DEC-A1..A4 inform CON-D-01 blank-login decision in C-14).
- `docs/ai/test-harness-plan.md` — Tier 1 = managed xUnit (Phase 1 + this phase's harnesses). Tier 2 (Phase 4 CLI golden) and Tier 3 (V2 mock-D3D9) are out of scope but inform why C-01 / C-09 / C-11 harness shapes are partial proofs rather than full coverage.

### Codebase intel (read-only reference)
- `.planning/codebase/CONCERNS.md` — TD-01..TD-15 map directly to C-01..C-15; TD-29 = C-07's `RedoCommands.Clear` ordering sub-bug; KB-05 = `||` vs `&&` actual code fix (in Phase 2 scope per D-11); TC-02, TC-03, TC-05 = test coverage gaps this phase closes.
- `.planning/codebase/STACK.md` §"Runtime" + §"Testing" — `net472`/x86 constraint that pins xUnit 2.x and shapes the harness language choice.
- `.planning/codebase/ARCHITECTURE.md` — three-layer architecture (`swg::*` → `utinni::*` → CLR bridge → MEF SPI → WinForms) frames why C-01 / C-06 / C-07 cross-layer fixes are riskier than single-file ones.
- `.planning/codebase/INTEGRATIONS.md` §"CON-T-01 post-build chain" — CI invokes this chain; C-15 fix must keep `UtinniCoreDotNetGen.exe` working when invoked from CI (non-`\bin\` paths).
- `.planning/codebase/CONVENTIONS.md` §"Code Style" — Allman braces, 4-space, PascalCase, MIT header on every C# file (applies to new test code).
- `.planning/codebase/TESTING.md` — verified zero-tests baseline (now zero+1 after Phase 1); "no precedent to deviate from" recommendation for naming; `xUnit 3 / net472` constraint.

### Surface this phase touches
- `UtinniCore/utinni.cpp:99-130` (`main`), `:138-151` (`DllMain`) — C-01.
- `Launcher/main.cpp:174-210` (`inject`), `:298-368` (`loadDll`) — C-01 conditional scope (researcher picks path).
- `UtinniCore/swg/misc/config.cpp:59-76` (`hkLoadOverrideConfig`) — C-02.
- `UtinniCore/swg/misc/network.cpp:65-69` (`Network::cast`) — C-03.
- `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs:97-106` (`DequeuePostDrawLoopCalls`) — C-04.
- `UtinniCoreDotNet/UI/GameDragDropEventHandlers.cs:33-44`, `UtinniCoreDotNet/UI/Controls/PanelGame.cs:68` — C-05.
- `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:39-73` — C-06.
- `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:60-74`, `UtinniCoreDotNet/UndoRedo/IUndoCommand.cs:36` — C-07 (touches CON-M-05 — preservation note: cleanup-on-scene-cleanup behavior must remain).
- `UtinniCoreDotNet/Hotkeys/Hotkey.cs:66-92`, `UtinniCoreDotNet/Hotkeys/HotkeyManager.cs:106-114` — C-08 (FR-07 same root cause).
- `UtinniCoreDotNet/UI/Forms/FormMain.cs:57-78`, `UtinniCore/swg/graphics/directx9.cpp:216-226` — C-09.
- `UtinniCore/clr.cpp:93-102`, `UtinniCore/utinni.cpp:132-136` — C-10.
- `UtinniCore/swg/graphics/directx9.cpp:297-303` — C-11 (preservation note: CON-N-04 `utility/memory::copy` VirtualProtect bracket must remain intact when adding the null check).
- `sdk/UtinniPluginTemplates/Vsix/source.extension.vsixmanifest:9-11,17` — C-12.
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolbox/TheJawaToolbox.vcxproj:63` + `.sln` Build entry — C-13 (cross-repo: `github.com/kennethlong/UtinniPlugins`).
- `data/utinni.cfg:4-5` — C-14.
- `UtinniCoreDotNetGen/Program.cs:39-41` — C-15.
- `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:46` + ObjectCallbacks/GroundSceneCallbacks delegate-passing sites — C-16.
- `UtinniCore/swg/game/game.cpp:305-308` — KB-05 operator fix (folds into CON-O-01 disposition commit).
- `UtinniCoreDotNet.Tests/` (Phase-1 project) — single test home for all new managed-side and P/Invoke harnesses.
- New sibling project for C-01 process-isolated `LoadLibrary` timing helper exe — added to `Utinni.sln` per Phase-1 flat-root convention.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`UtinniCoreDotNet.Tests` (from Phase 1)** — xUnit 2.9.x project at repo root, `net472`/x86, already wired into Phase 1 CI. Absorbs all managed-side regression tests + P/Invoke + WinForms fixtures + file-content checks for Phase 2. Adds `System.Windows.Forms` ref (for C-05 panel fixture) and one `/Fixtures` subdir.
- **Phase-1 CI workflow (`.github/workflows/ci.yml`)** — already runs `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86 /restore` (triggers CON-T-01 post-build chain) and `dotnet test`. Each Phase-2 fix's harness piggybacks on this — no new CI workflow needed.
- **CON-T-01 post-build chain** (`UtinniCore.vcxproj:91-94`) — `xcopy data/` + `UtinniCoreDotNetGen.exe`. C-15 fix must not break this; the path argument refactor at `UtinniCoreDotNetGen/Program.cs:39-41` is the C-15 fix surface, and the new path resolution must work when invoked from the post-build (`$(SolutionDir)`) and CI environment.
- **`Hotkey.ProcessString` malformed-input test (Phase 1)** — likely red on master today per Phase-1 D-05 / `01-CONTEXT.md` specifics. C-08 fix turns it green; that's the Phase-1→Phase-2 handoff.

### Established Patterns
- **Atomic-commit-per-task** — GSD executor default; every bug = its own commit. ~16 fix commits across the four plans, plus harness/test commits where they don't fold into the fix commit.
- **MIT license header on every C# file** — applies to new tests in `UtinniCoreDotNet.Tests` (23-line block at top per Phase-1 CONTEXT.md).
- **Detour-table pattern (CON-N-01)** — `using pX = ...; pX x = (pX)0xRVA; Detour::Create(...)`. C-01's research substep must respect this — any new detour (e.g., `Game::install` for path-b) follows the same pattern.
- **`utinni::` thin-wrapper firewall (CON-N-02)** — managed code calls `utinni::*` not `swg::*`. C-16's `GCHandle.Alloc` audit walks the delegates passed through this firewall.
- **No package manager for managed projects today** — `UtinniCoreDotNet.Tests` is the only managed project with `<PackageReference>` (xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk from Phase 1). Any new packages (e.g., `Microsoft.VisualStudio.Threading` if researcher reaches for it on C-09) go here.
- **AnyCPU vs x86 split** — `UtinniCoreDotNet.Tests` is x86 (matches the SUT). The new C-01 helper-exe project must also be x86 (it `LoadLibrary`s the x86 `UtinniCore.dll`).

### Integration Points
- **`Utinni.sln`** — adds one new project (C-01 helper exe). Project dependency graph: helper-exe depends on `UtinniCore` (so `UtinniCore.vcxproj` post-build chain runs first; matches the `UtinniCoreDotNet` → `UtinniCore` precedent).
- **CI workflow** — no changes needed; existing `dotnet test` against `UtinniCoreDotNet.Tests.csproj` picks up new tests automatically. The new helper-exe needs to be discoverable from the test project (either bundled as test content or referenced by path); planner picks.
- **`docs/ai/assessment.md` §"Status tracking"** — update each row's `Status` to `done` with PR/SHA link as each C-NN closes. Last fix in the phase flips the last row.
- **`docs/ai/assessment.md` §"Open questions"** — researcher (or default-fallback) lands a disposition update for CON-O-01, -02, -03, -04 alongside the related code fix.
- **`UtinniPlugins` sister repo** — C-13 cross-repo PR. Separate clone, separate commit, separate review. Phase 2 plan tracking marks the C-13 task with `repo:UtinniPlugins`.

</code_context>

<specifics>
## Specific Ideas

- **CI must stay green THROUGHOUT the burn-down, not just at phase boundaries.** Each fix commit ships behind a green CI run; if a commit breaks CI, fix-forward or revert before moving on. The 4-plan ordering protects against late-discovered architectural blockers (trivial criticals first → durability fixes → architectural last).
- **The `Drain(ConcurrentQueue<Action>)` helper for C-04** is named in assessment.md "so this category of bug can't reoccur." Land it as part of the C-04 fix — both queues drain via the helper, regression test covers both queue variants.
- **The C-08 malformed-input test from Phase 1 turns green when C-08 fix lands.** Phase 1 left two postures available (red-with-skip-marker or defer-the-malformed-test). Whichever the Phase-1 planner picked, Plan 02-01's C-08 task must close out the test status (remove the skip marker / un-defer the malformed case).
- **C-15 priority within Plan 02-02.** If Phase-1 CI is already affected by the brittle `slnDir` (e.g., the CI runner's output path doesn't contain `\bin\` — researcher to confirm), C-15 jumps to the FIRST task in Plan 02-02 so subsequent CI runs are clean. If Phase-1 CI is unaffected, C-15 follows normal assessment-week order.
- **C-07's `AllowMerge` disposition** — assessment.md says either call `Peek().AllowMerge()` before `Merge`, OR remove `AllowMerge()` from `IUndoCommand`. Researcher reads UtinniPlugins / Jawa Toolbox `IUndoCommand` implementations to see what callers expect, then picks. Either way, contract is documented after the fix.
- **C-13 cross-repo PR title/format** — follow Phase-1 commit style: `fix(C-13): TJT Debug path uses ..\..\..\ instead of ..\..\..\..\`. PR body cites assessment.md C-13 + this phase's CONTEXT.md.
- **Researcher's archaeology budget for CON-O-01, -02, -04** — bounded. Researcher checks: git log on `master` + upstream `ptklatt/Utinni` for relevant SHAs, in-tree comments (`// ToDo`, `// XXX`, `// HACK`), and obvious IDA pseudo-paste markers in C++ files. Does NOT do deep IDA reverse-engineering — that's too expensive for an archaeology step. If no answer in the bounded search, default-fallback applies (D-12).
- **No `.runsettings`, no coverage tooling, no analyzer-rule `.editorconfig` in Phase 2** — all Phase 6 STAB-03 cleanup territory.

</specifics>

<deferred>
## Deferred Ideas

- **UtinniPlugins CI bootstrap** — first Wave-1 plugin phase (Phase 7+) or a dedicated bridging phase owns this. Phase 2 keeps the sister repo at zero-CI; manual TJT Debug verification is acceptable for the one C-13 commit.
- **C-12 actual install into VS 2019 + VS 2022 IDEs** — stays manual (commit-message `Verified-by:` block). Automating cross-IDE template install is a Phase 6+ ergonomics rework, not a Phase-2 cost.
- **Phase-3 strategic reworks R-A..R-H** — R-A symmetric callback Add/Remove (CON-O-03 fix is C-16, not the full R-A pass — R-A audits every `Add*Callback` for symmetric `Remove*` across all callback classes; that's Phase 3). R-B plugin lifecycle (`destroyPlugin` symmetric ABI), R-C single-source RVAs (currently duplicated `0xAA0970` WndProc address across C++/C#), R-D CI (closed by Phase 1), R-E `[CallerMemberName]` logging, R-F CppSharp header auto-discovery, R-G `Directory.Build.props` wizard, R-H snapshot-iteration over callback collections.
- **TD-25 empty stub files** (`particle.cpp`, `scene.cpp`), **TD-26 disabled detour-table hooks** (commented `Detour::Create` calls in `render_world.cpp`, `client_world.cpp`, `io_win.cpp`, `utinni.cpp`), **TD-27 hardcoded font path** (`C:/Windows/Fonts/micross.ttf`), **TD-28 `TJT.ico` framework-default leak**, **TD-29 redo-stack-clear ordering** (this one IS in scope as part of C-07 fix) — Phase 6 STAB-03 territory unless specifically subsumed by a C-fix.
- **Open questions CON-O-05 → Phase 3 R-F**, **CON-O-06 (LeksysINI replacement) → Phase 6**, **CON-O-07 (Sytner's plugin) → Phase 3 R-B**, **CON-O-08 (DXSDK vs Windows 10 SDK replaceability) → Phase 6 STAB-03**.
- **CON-O-09 (fixture storage in-repo vs Git LFS), CON-O-11 (CLI public vs internal) → Phase 4** TEST-03 CLI shim.
- **Plaintext password storage (SEC-01)** — disable `autoLogin` by default until DPAPI encryption lands; full fix is Phase 6+ ergonomics.
- **`VirtualAllocEx PAGE_EXECUTE_READWRITE` in launcher (SEC-02)** — Phase 6 cleanup pass.
- **AV/EDR false-positive surface (SEC-03), plugin signing / sandboxing (SEC-04, MCF-04), SWG-client version validation (MCF-03), remote-debugging story (MCF-05), mouse+keyboard combo hotkeys (MCF-07)** — V2 or out of V1.
- **Coverage tooling (coverlet, ReportGenerator)** — revisit after Phase 4 lands more test breadth.
- **`.clang-format` adoption + comprehensive analyzer-rule `.editorconfig`** — Phase 6 STAB-03 cleanups.
- **DXSDK June 2010 install on CI runner + multi-config matrix (Debug + Release + RelWithDbgInfo)** — Phase 6, bundled with CON-O-08.
- **Branch protection rules on `master`** — admin action; document as user task in `confirm_creation` next steps.

</deferred>

---

*Phase: 02-critical-bug-burn-down-c-01-c-15*
*Context gathered: 2026-05-16*
