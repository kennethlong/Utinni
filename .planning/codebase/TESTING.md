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

## Tier 4 — Manual Residual (TEST-04)

**Added:** 2026-05-23 (06-01 sign-off, scenario (a) only). **Completed:** 2026-05-25 (Phase 6 plan 06-06, D-19 — full eight-scenario residual enumeration).

**Correcting the TL;DR above:** the "There Are No Tests" narrative at the top of this file describes the repository as it was at ingest time (2026-05-16), *before* the Phase-1/3/4/5 test infrastructure landed. As of Phase 6 the repo ships **three CI test lanes** — `dotnet test` (UtinniCoreDotNet.Tests xUnit), the CLI golden-fixture lane (UtinniCoreDotNetGen golden compare), and `UtinniCore.Tests.exe` (Catch2 native) — **plus** the new PreservationAudit Facts (06-05). The historical narrative is preserved deliberately as a record of where the project started; the Tier-4 residual enumerated below is the **bounded** scope that remains manual and ships unautomated in V1 per REQUIREMENTS.md §TEST-04.

Tier 4 captures the manual verifications that Tiers 1–3 cannot cover — typically because they require a live SWG-injected session, GPU-driver-specific behavior, or maintainer visual evaluation. Each scenario records why it is manual, the procedure, the success criterion, the SHA at which it was last verified, and the failure-mode escalation. This is the canonical Tier-4 boundary doc referenced from `CONVENTIONS.md` per TEST-04 acceptance.

The eight scenarios (a)–(h) are the D-19 enumeration. FlaUI-style automated WinForms UI driving is **explicitly EXCLUDED per CON-TT-03** — see scenario (h).

### Tier-4 Scenario (a): Imgui overlay rendering

- **Why manual:** Visual judgment + live d3d9 device state inside an injected SWG process; no scriptable assertion exists for "did the user see the demo screen correctly."
- **Procedure:**
  1. Flip `g_showDemoWindowProbe` to `true` in `UtinniCore/swg/ui/imgui_impl.cpp` (file-scope inside `namespace imgui_impl`, near `bool enableUi;` / `bool rendering;`).
  2. Rebuild Release x86: `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86 /t:UtinniCore`.
  3. Launch via `Launcher.exe` against live SWGEmu; log in; load any scene.
  4. Exercise the imgui Demo window's seven widget categories: menus, sliders, buttons, tabs, plots, popups, drag-and-drop.
  5. Flip the flag back to `false`, rebuild.
- **Success criterion:** Each of the seven widget categories behaves as in a standalone imgui demo application: menus cascade and dispatch; sliders drag smoothly with live updates and accept keyboard editing; buttons click and dispatch callbacks; tabs switch on click and render distinct content; plots render their animated waveforms; popups open as modals and dismiss cleanly; drag-and-drop source-to-target works with payload preview. The Demo window is draggable, resizable, and correctly layered atop SWG's client window (Z-order via `HWND_TOP`).
- **Last-verified SHA:** `29a128e` (2026-05-25, Kenneth Long — 06-06 Tier-4 UAT; originally verified 2026-05-23 at `2e0dcf5` per `06-01-VERIFICATION.md`).
- **Failure-mode escalation:** Re-open the 06-01 plan; rerun the [[feedback-d3d9-hook-diagnosis]] first-move check (d3d9.dll pattern-scan vs dummy-device) *before* assuming SWG-side RVA drift.

### Tier-4 Scenario (b): PanelGame.WndProc forwarding to live SWG

- **Why manual:** Requires a live SWG window receiving real OS input messages routed through the reparented FormMain → PanelGame → SWG client; the CUI key-context routing only resolves correctly with the game's input state machine running.
- **Procedure:**
  1. Launch SWGEmu via `Launcher.exe`; log in; enter any scene.
  2. With the game panel focused, press alphanumeric keys, the arrow keys, and Tab.
  3. Open the in-game chat (Enter) and type; confirm characters arrive.
- **Success criterion:** `WM_CHAR` / `WM_KEYDOWN` messages reach SWG: typed characters appear in the SWG chat box; arrow keys move the camera/selection; Enter dispatches as `openChat` (game-mode) not `chatEnter` (input-mode) per the context-routing fix. No keystrokes are swallowed by the WinForms host.
- **Last-verified SHA:** `29a128e` (2026-05-25, Kenneth Long — 06-06 Tier-4 UAT)
- **Failure-mode escalation:** Re-open the Phase 3 D-06 (R-C) WndProc-forwarding rework; verify the context-routing detour at `0x00F3E420` is installed (see [[project-swg-context-routing]]).

### Tier-4 Scenario (c): hkPresent + MMO render lifecycle

- **Why manual:** Scene-transition allocator behavior only manifests across repeated live scene loads; no headless harness reproduces SWG's per-frame Present pump + setup/cleanup callback ordering.
- **Procedure:**
  1. Launch, log in, then transition between scenes 3+ times (e.g. Tatooine → Naboo → Lok → Tatooine) via TJT's chat-command scene loader.
  2. Tail `utinni.log` during each transition.
- **Success criterion:** Per transition, `utinni.log` shows a clean single cycle: `hkMainLoop: loadNewScene -> Game::cleanupScene -> hkSetScene(null) -> hkMainLoop: setupScene -> hkSetScene(<new>) -> firing 1 setSceneCallbacks`. No allocator-fragmentation crash (e.g. the historical `0x0051fb0a`); setup callbacks fire exactly once per transition. (Landing naked after a scene change is the expected baseline, not a failure — see [[project-tjt-scene-change-naked-baseline]].)
- **Last-verified SHA:** `29a128e` (2026-05-25, Kenneth Long — 06-06 Tier-4 UAT)
- **Failure-mode escalation:** Re-open Phase 3 R-H; confirm `dispatchSnapshot` in `ground_scene.cpp` remains heap-free on the callback hot path (see [[project-rh-snapshot-no-heap-alloc]]).

### Tier-4 Scenario (d): D3D9 device-loss / reset paths

- **Why manual:** Device-loss is triggered by OS-level events (alt-tab, resolution change) against a third-party-owned d3d9 device; CON-N-06 preservation context cannot be asserted without the live device.
- **Procedure:**
  1. Launch, log in, load a scene with the overlay active.
  2. Alt-tab away from SWG and back repeatedly; alternatively change the SWG client resolution.
  3. Tail `utinni.log`.
- **Success criterion:** `imgui_impl::isSetup` invalidates and recreates its device objects per CON-N-06; the overlay re-renders correctly after focus return; **no `D3DERR_INVALIDCALL` fatal** appears in `utinni.log`. (Per [[feedback-d3d9-reset-third-party]], Utinni never calls `IDirect3DDevice9::Reset` on the app's device — the windowed Present handles backbuffer/window mismatch; only the window is resized.)
- **Last-verified SHA:** `29a128e` (2026-05-25, Kenneth Long — 06-06 Tier-4 UAT)
- **Failure-mode escalation:** Re-open the imgui embedded-D3D9 work; confirm render-target-space mapping (DisplaySize + mouse scaled) still holds (see [[feedback-imgui-embedded-d3d9-rt-space]]).

### Tier-4 Scenario (e): Plugin loader against real plugin DLLs (TJT)

- **Why manual:** MEF discovery + native plugin load only exercises end-to-end with a real signed-shape plugin DLL dropped into `Plugins/`; the bundled TheJawaToolbox is the canonical real-world plugin.
- **Procedure:**
  1. Install Utinni via the MSI on a clean Windows VM with TheJawaToolbox bundled (see scenario in 06-VERIFICATION.md step 9), OR via the dev-build path with TJT copied into `Plugins/TheJawaToolbox/`.
  2. Launch; open the editor host; observe plugin discovery.
- **Success criterion:** TJT loads as a panel/subpanel in the editor host with no exceptions in `utinni.log`; its chat-command parser callbacks register; no MEF compose `MissingMethodException` (cf. [[feedback-caller-attrs-binary-compat]] — cross-binary plugins must be rebuilt in lockstep).
- **Last-verified SHA:** `29a128e` (2026-05-25, Kenneth Long — 06-06 Tier-4 UAT)
- **Failure-mode escalation:** Re-open the plugin-framework work; check the MEF `[InheritedExport]` surface and the cross-repo TJT pin SHA in `06-06-MSI-TJT-PINNING.md`.

### Tier-4 Scenario (f): Drag-drop in editor + WinForms STA

- **Why manual:** OLE drag-drop is an STA-thread + cursor-capture interaction with the live world panel; ray-cast-on-drop commits against the SWG scene which has no headless equivalent.
- **Procedure:**
  1. In the editor host, open `FormObjectBrowser`.
  2. Drag a template entry into the world panel.
  3. Release over a ground target.
- **Success criterion:** The preview object follows the cursor during the drag; on drop the ray-cast resolves a world position and commits the create (undoable). No STA cross-thread exception; the drag preview does not corrupt on WinForms resize (cf. the GC-pinned-callback pattern in CONVENTIONS.md).
- **Last-verified SHA:** `29a128e` (2026-05-25, Kenneth Long — 06-06 Tier-4 UAT)
- **Failure-mode escalation:** Re-open the post-Phase-02.1 WR-09 drag-drop work; verify the STA marshaling seam.

### Tier-4 Scenario (g): GPU-driver-specific bugs

- **Why manual:** Driver-dependent depth-resolve / RESZ behavior and adapter-specific overlay rendering can only be validated on real silicon from different vendors.
- **Procedure:**
  1. Run scenarios (a)–(d) once on Nvidia hardware (the usual dev machine).
  2. Run once more on Intel and/or AMD hardware if available; record GPU vendor + driver version in 06-VERIFICATION.md.
- **Success criterion:** The overlay renders correctly on every tested vendor; depth-resolve callbacks fire correctly (no missing-depth artifacts); no vendor-specific crash. If only one vendor is available locally, the second vendor is explicitly DEFERRED to the rc-bake period and noted in 06-VERIFICATION.md.
- **Last-verified SHA:** `29a128e` (2026-05-25, Kenneth Long — 06-06 Tier-4 UAT)
- **Failure-mode escalation:** Capture the adapter line from `utinni.log`; if a vendor-specific failure appears, re-open the graphics/depth_texture work with the vendor + driver version recorded.

### Tier-4 Scenario (h): WinForms UI smoke

- **Why manual:** Whole-form visual + interaction smoke across the editor host; deliberately kept human.
- **Procedure:**
  1. Open every form in the editor host (enumerate at task time — `FormMain`, `FormLog`, `FormObjectBrowser`, `FormPlugins`, and any others present).
  2. Resize, minimize, and restore each.
- **Success criterion:** No UI hangs, no unhandled exceptions, no layout corruption on resize/restore.
- **FlaUI explicitly EXCLUDED per CON-TT-03** — automated WinForms UI driving (FlaUI / UIAutomation) is a deliberate Tier-3 V2 deferral; this scenario remains a manual smoke walk forever in V1 and is **not** a candidate for automation under this milestone.
- **Last-verified SHA:** `29a128e` (2026-05-25, Kenneth Long — 06-06 Tier-4 UAT)
- **Failure-mode escalation:** Re-open the offending form's layout/Designer; this scenario never escalates to "add FlaUI" within V1 scope.

*Eight-scenario residual complete (06-06, 2026-05-25). The `Last-verified SHA` placeholders for (b)–(h) are filled in at the 06-06 Task 4 maintainer-signed UAT with the rc.1 commit SHA.*

---

*Testing analysis: 2026-05-16; Tier-4 section added 2026-05-23; full eight-scenario residual completed 2026-05-25 (06-06).*
