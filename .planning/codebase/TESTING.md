# Testing Patterns

**Analysis Date:** 2026-05-16

## TL;DR — There Are No Tests

**The Utinni repository contains zero automated tests.** This is not a documentation gap; it is the actual state of the codebase. There are no test runners configured, no test projects in the Visual Studio solution, no test files anywhere in the source tree, no CI workflow, and no coverage tooling. Every "test" performed on Utinni today is manual: launch SWG with the injected DLL, observe whether the editor mode boots, drag-and-drop a world snapshot node, click undo/redo, watch the log window.

This section documents that state honestly and prescribes what an executor adding tests would have to introduce from scratch. Do not invent test patterns when writing new code — there is no precedent to match.

## Evidence — How "Zero Tests" Was Verified

1. **No test files in source tree:**
   - `Glob **/*test*` in the repo root returns only `external/CppSharp/lib/lib/clang/11.0.0/include/xtestintrin.h` and a duplicate under `external/CppSharp/output/...` — both are clang header artifacts shipped with the CppSharp dependency, unrelated to Utinni
   - `Glob **/*Test*` returns nothing
   - `Glob **/*.Tests.csproj` returns nothing
   - No `*.spec.cs`, `*.spec.cpp`, `*_test.cpp`, `test_*.cpp` files anywhere under `UtinniCore`, `UtinniCoreDotNet`, `UtinniCoreDotNetGen`, `Launcher`, `UtINI`, `sdk`, or `data`

2. **No test projects in the solution:**
   - `Utinni.sln` declares exactly five projects: `Launcher`, `UtinniCore`, `UtinniCoreDotNet`, `UtinniCoreDotNetGen`, `UtINI`, `UtinniCore-Symbols` (see `Utinni.sln:6-28`). None of them is a test project. None references xUnit, NUnit, MSTest, Google Test, Catch2, or doctest

3. **No test runner config:**
   - No `xunit.runner.json`, no `*.runsettings`, no `nunit.config`, no `mstest.runsettings`, no `CMakeLists.txt` with `enable_testing()`

4. **No CI:**
   - No `.github/workflows/`, no `.gitlab-ci.yml`, no `azure-pipelines.yml`, no `appveyor.yml`, no `.circleci/`

5. **No coverage tooling:**
   - No `coverlet.collector` reference, no OpenCover, no Cobertura artifacts, no `.coverage` outputs in `bin/`

6. **External corroboration:**
   - The prior code-quality audit in this repo (`docs/ai/assessment.md:252-257`) explicitly states: "There is no CI, no tests, no analyzers, no `.editorconfig`." and recommends "Smoke-test xUnit project third" as a future remediation step

## Why There Are No Tests

Some of this is genuinely hard, not just neglected:

- **Tight binding to a 32-bit Win32 process.** UtinniCore is a DLL injected into the SWG client (`swgclient_r.exe`). Its public surface is a thin façade over function pointers cast to RVA constants like `(pInstall)0x00422E80` (see `UtinniCore/swg/game/game.cpp:54`). Calling `Game::install()` without SWG mapped at the expected base address segfaults. Most of `UtinniCore/swg/` is effectively untestable without either (a) running the real game, or (b) building a fake process image with the same memory layout.

- **No exceptions, no isolation seams.** `UtinniCore` is compiled with `SPDLOG_NO_EXCEPTIONS` (see `UtinniCore/UtinniCore.vcxproj:79`) and contains zero `try/catch/throw`. There is no dependency injection, no interface-based seams. Subsystems are accessed via global free functions (`utinni::getPath()`, `utinni::getConfig()`) and module-level statics (`installCallbacks` at `UtinniCore/swg/game/game.cpp:71-76`). A unit-test harness would have to either accept that ~80% of UtinniCore is effectively unmockable, or aggressively refactor toward interfaces first

- **WinForms + COM apartment threading.** `UtinniCoreDotNet` runs inside `[STAThread]` and explicitly synchronizes with SWG's render thread via `BlockPresent`/`IsPresentBlocked` polling (see `UtinniCoreDotNet/UI/Forms/FormMain.cs:65-78`). Even pure-managed code paths are entangled with the game's main loop callbacks (`GameCallbacks.AddPreMainLoopCall`, `GameCallbacks.AddMainLoopCall`)

- **Heavy reliance on auto-generated CppSharp bindings.** `UtinniCoreDotNet/Generated/UtinniCore.cs` is regenerated on every build by a post-build step (see `UtinniCore/UtinniCore.vcxproj:92-93`). Any test asserting on its surface is at risk of breaking when bindings shift

## Test Framework

**Runner:** None configured

**Assertion Library:** None configured

**Run Commands:**
- None. `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86` builds the product but runs nothing afterward
- The audit recommendation (`docs/ai/assessment.md:256`) suggests starting with a 30-line `.github/workflows/build.yml` invoking `msbuild` — even establishing "the code compiles" as a CI gate would be progress

## Test File Organization

**Location:** No tests exist, so no location convention is established. If tests are added:

- C# tests should go in a new sibling project: `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj`, referencing `UtinniCoreDotNet` via `<ProjectReference>`. Place it at the repo root next to the other projects and add it to `Utinni.sln`
- C++ tests would need a new `.vcxproj` and a vendored test framework under `external/` (Catch2 single-header is the lightest option for this repo's preferences — no package manager is currently used; everything in `external/` is vendored)
- Following the project's existing `PascalCase`-by-primary-type convention (see `CONVENTIONS.md`), test classes should be `LoggerTests.cs`, `HotkeyTests.cs`, `UndoRedoManagerTests.cs`

**Naming:** Not established. Adopt the standard `[Method]_[Scenario]_[ExpectedOutcome]` style for xUnit, since the project has no precedent to deviate from

**Structure:**
```
(does not exist)
```

## Test Structure

**Suite Organization:** Not established

**Patterns:** None — no test code exists to derive patterns from

## Mocking

**Framework:** None configured

**Patterns:** None established. **Important constraint for any future test author:** `UtinniCoreDotNet` is built against `.NET Framework 4.7.2` (see `UtinniCoreDotNet.csproj:12`). Moq still works there, but xUnit 3 requires .NET 6+ — pin to xUnit 2.x or migrate to `.NET 8` first. The audit (`docs/ai/assessment.md`) elsewhere recommends a broader move off .NET Framework, which would unblock modern tooling

**What to Mock:**
- If you add tests against `UtinniCoreDotNet`, the entire `UtinniCore.Utinni.*` namespace (generated CppSharp bindings) is a hard boundary — you cannot exercise it without the native DLL loaded. Wrap it in interfaces in the production code before attempting to test consumers
- Specifically: `GameCallbacks.Initialize()` calls `UtinniCore.Utinni.Game.AddInstallCallback(...)` (see `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:53`) — this needs an injectable seam before `GameCallbacks` itself can be unit tested

**What NOT to Mock:**
- `IUndoCommand` is the cleanest seam in the codebase (see `UtinniCoreDotNet/UndoRedo/IUndoCommand.cs`). `UndoRedoManager` could be unit tested with hand-rolled `IUndoCommand` fakes today, no mocking framework required, provided `GameCallbacks.AddCleanupSceneCall` is first refactored to an injectable dependency (currently called directly in `UndoRedoManager`'s constructor at `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:50`)

## Fixtures and Factories

**Test Data:** None

**Location:** N/A

## Coverage

**Requirements:** None. No coverage gate, no reporting

**View Coverage:** N/A

## Test Types

**Unit Tests:** Zero exist. The candidates with the lowest barrier-to-entry are:
- `UtinniCoreDotNet/Hotkeys/Hotkey.cs` — `ProcessString` (line 66) and `GetKeyComboString` (line 105) are pure functions over strings and `System.Windows.Forms.Keys`. The `Enum.Parse` calls would surface bugs in malformed input strings — exception-throwing behavior is currently undocumented
- `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs` — Stack-based undo/redo logic is pure once `GameCallbacks.AddCleanupSceneCall` is injected
- `UtinniCore/utility/string_utility.h` — header-only inline string helpers (`toString`, `toBool`, `trim`, `toHexString`) are pure and trivially testable with any C++ unit framework

**Integration Tests:** Zero. Would require launching SWG or building a stub process — neither has been attempted

**E2E Tests:** Zero. Not used. The only "end-to-end verification" is manual operator testing inside the live SWG client

## Common Patterns

**Async Testing:**
- N/A. The codebase uses callback queues (`ConcurrentQueue<Action>` in `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:36-37`) rather than `async/await`. No `Task`-returning code paths exist in the production source

**Error Testing:**
- N/A. No tests, and as documented in `CONVENTIONS.md`, the production code does not throw exceptions either — error states surface as null returns, `false` returns, or logged messages

## Recommended First Tests If You Are Adding Coverage

In priority order, lowest-effort to highest-effort:

1. **`UtinniCoreDotNet.Tests` xUnit project** targeting net472, referencing `UtinniCoreDotNet` directly. Start with `Hotkey.ProcessString` — pure string-to-`(Keys, Keys)` mapping, no native dependencies
2. **`UndoRedoManagerTests`** — refactor the `GameCallbacks.AddCleanupSceneCall` direct call in the constructor (`UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:50`) behind a constructor-injected `Action<Action>` parameter first. Then write tests around `Undo`, `Redo`, `Undo(int count)`, command-merging via `AllowMerge`
3. **`Hotkey.GetKeyComboString` round-trip** — assert `ProcessString(GetKeyComboString())` produces an equivalent state
4. **CI build gate** — add `.github/workflows/build.yml` running `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86 /p:RestorePackagesConfig=true`. Before adding any test discovery to it. This catches the bulk of regressions for free (the audit at `docs/ai/assessment.md:250-257` makes the same recommendation)
5. **Catch2 C++ unit tests for `utility/string_utility.h`** — vendor `external/catch2/catch_amalgamated.hpp`, add a `UtinniCore.Tests.vcxproj` consuming it, assert on `stringUtility::toString`, `toBool`, `trim`. Inline header-only helpers, no linker dependency on `UtinniCore.dll`

Do not attempt to write tests that load the SWG client. That is an integration-test concern requiring a non-trivial harness (likely a mock process or `LoadLibrary`-against-a-stub-DLL approach) and should be a separate planning phase if scoped at all.

---

*Testing analysis: 2026-05-16*
