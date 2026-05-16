# Phase 2 Research — Critical Bug Burn-down (C-01..C-15)

**Researched:** 2026-05-16
**Domain:** Concrete fix shapes, harness designs, and architectural recommendations for 17 bugs (C-01..C-15 + C-16 + KB-05) across the Utinni native/managed boundary.
**Confidence:** HIGH for fix shapes (every surface verified in-tree); HIGH for C-07 / C-12 / C-13 / C-15 / C-16 dispositions (verified against UtinniPlugins repo and live build output); MEDIUM for C-01 path recommendation (rest on Microsoft loader-lock guidance, not on a live deadlock repro); MEDIUM for the CON-O-01/-02/-04 archaeology (bounded search; default-fallback contract applies if surface evidence is absent).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Plan Structuring (4 plans, risk-tier grouped):**
- **D-01:** Plans grouped by risk tier per assessment.md "Recommended sequencing": trivial criticals (Plan 02-01), single-file criticals (Plan 02-02), architectural (Plans 02-03, 02-04). CI gates each plan boundary.
- **D-02:** The two architectural fixes (C-01, C-09) are SPLIT into separate plans (02-03 and 02-04).
- **D-03:** Each bug fix lands as its own atomic commit. ~16 fix commits across the four plans plus harness/test commits.

**Verification Contract (max-harness posture):**
- **D-04:** Managed-side fixes MUST land with at least one xUnit regression test in the SAME commit as the fix. Test must be red before the fix is applied and green after.
- **D-05:** Max-harness posture for not-unit-testable-out-of-the-box bugs. Every C-NN bug gets a harness unless physically impossible. Per-bug shapes specified in CONTEXT.md §D-05.
- **D-06:** Truly-manual residual after max-harness: C-12 IDE install confirmation, C-13 cross-repo build, C-03 live-injection cast against SWG, C-01 full proof of no deadlock under loader-lock contention.
- **D-07:** Single test project absorbs everything. `UtinniCoreDotNet.Tests` (the Phase-1 project) gets `System.Windows.Forms` ref (already added in Phase 1), P/Invokes into `UtinniCore.dll`, and a `/Fixtures` subdir. A new sibling project (working name: `Utinni.LoaderLockHarness` — researcher names it final) houses the C-01 process-isolated `LoadLibrary` timing exe.

**C-01 Architectural Approach:**
- **D-08:** Researcher picks the C-01 fix path during Plan 02-03's research substep. Three candidate paths: (a) `utinni_init` export + `CreateRemoteThread`, (b) defer to `Game::install` detour, (c) hybrid.

**Out-of-list Scope:**
- **D-09:** C-13 is a cross-repo direct commit during Phase 2 execution. Plan 02-01 tracks the C-13 task with an explicit `repo:UtinniPlugins` flag. No CI workflow added to UtinniPlugins in Phase 2.
- **D-10:** C-16 added (GameCallbacks delegate-pinning, resolves CON-O-03). Lands in Plan 02-02 as a 16th task.
- **D-11:** KB-05 (`||` vs `&&` at `game.cpp:307`) folds into the CON-O-01 disposition commit in Plan 02-02. No separate C-17.
- **D-12:** Researcher investigates CON-O-01, -02, -04 dispositions during each plan's research substep. If unanswerable, default-fallback contract: CON-O-01 → use `&&`; CON-O-02 → assume IS used (queue-drain fix is load-bearing); CON-O-04 → audit VS 2022 build, widen if clean.

### Claude's Discretion

- Exact xUnit test naming (follow Phase-1 `[Method]_[Scenario]_[ExpectedOutcome]`).
- Per-bug task ordering WITHIN a plan.
- Whether C-15 lands first in Plan 02-02 (researcher confirms whether Phase-1 CI is already affected by C-15) — see §C-15 CI impact assessment below: **CI is NOT currently affected**, so C-15 follows normal assessment-week order.
- Sibling helper-exe project name for the C-01 timing harness (working name `Utinni.LoaderLockHarness`).
- Whether the `UndoRedoManager` testability refactor lands as a SEPARATE task or as part of the C-07 fix task.
- Whether C-16 is one task or three tasks (one per file).

### Deferred Ideas (OUT OF SCOPE)

- C++ Catch2 unit tests (Phase 5).
- CLI shim and golden fixtures (Phase 4).
- Strategic reworks R-A..R-H (Phase 3).
- ~30 cleanups + dep bumps (Phase 6).
- DXSDK June 2010 install + multi-config matrix (Phase 6).
- UtinniPlugins CI bootstrap (first Wave-1 plugin phase or earlier dedicated phase).
- CON-O-05, -06, -07, -08, -09, -11 (mapped to later phases).
- SEC-01..SEC-04 (Phase 6 / V2).
- Branch protection rules (admin action, not a code deliverable).

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| STAB-01 | Fix 15 critical bugs (C-01..C-15) per `assessment.md`. Acceptance: every C-NN shows `done` in the status table; class-of-bug constraints CON-H-01..-05, CON-L-01..-04, CON-B-04, CON-D-01 are honoured going forward. | Per-bug fix shapes below give concrete code-line changes. Harness designs below give verification per CON-TT-01 (managed) and Tier-4-residual (live SWG) postures. The two non-assessment.md additions (C-16, KB-05) are folded in per D-10/D-11 — C-16 closes CON-O-03 and KB-05 closes CON-O-01. |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

No `./CLAUDE.md` exists at the repo root. Project conventions are enforced via `.planning/codebase/CONVENTIONS.md` (Allman braces, 4-space indent, MIT 23-line header on every `.cs/.cpp/.h`, PascalCase tests, `// ToDo` not `// TODO`) and `.editorconfig` (CRLF, UTF-8, trim trailing whitespace) per Phase-1 D-11.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|--------------|----------------|-----------|
| Loader-lock-safe DLL bootstrap | Launcher (native, out-of-process) | UtinniCore native DllMain | Microsoft's documented rule for `DLL_PROCESS_ATTACH` puts the responsibility for "no heavy work" on whatever code runs in `DllMain`. The fix moves work OUT of `DllMain` into either the launcher (path a) or first-callback (path b). |
| Cross-CRT allocation discipline | UtinniCore native (`utinni::` thin firewall) | swg::* shim | Every cross-boundary allocate/free is a CON-B-04 constraint — the firewall is responsible for using the originator's allocator. |
| Native callback dispatch / queue drain | UtinniCore native (vector of fn-ptrs) | UtinniCoreDotNet managed (Action queue) | Native fires the callback into the managed delegate; managed drains its Action queue. C-04 is purely a managed-side queue mix-up. |
| WinForms ↔ game-thread sync | UtinniCoreDotNet managed (signaller) | UtinniCore native (`isPresenting` flag) | UI thread must not busy-wait on a native flag — the signal needs to flow from native to managed via an event the .NET side waits on with a timeout. |
| Plugin load isolation | UtinniCoreDotNet managed (`PluginLoader`) | UtinniCore native (`PluginManager` — out of scope this phase) | C-06 is a managed-side MEF composition issue; the native `PluginManager` symmetry fix is R-B (Phase 3). |
| Undo/redo thread-safety + merge contract | UtinniCoreDotNet managed (`UndoRedoManager`) | — | Pure managed state machine; the testability seam is a constructor injection (`Action<Action>` for the cleanup-callback registration) per Phase-1 D-06. |
| Hotkey input parsing | UtinniCoreDotNet managed (`Hotkey.cs`) | — | Pure managed string-to-enum parse; affects both `ProcessString` (ctor path) and `UpdateKeys(string)` (settings-load path FR-07). |
| D3D9 device-hook installation | UtinniCore native (`directx9.cpp`) | — | Native vtable scan against `d3d9.dll`; managed never sees this. |
| CLR host lifecycle | UtinniCore native (`clr.cpp`) | — | `mscoree.lib` is a native-only API. |
| CppSharp post-build code generation | UtinniCoreDotNetGen (build-time tool, x64 AnyCPU) | UtinniCore.vcxproj PostBuildEvent | Build-time path-resolution problem; runs once per build, not at runtime. |
| Delegate pinning for native callbacks | UtinniCoreDotNet managed (`*Callbacks.cs`) | — | GC root for delegates passed via P/Invoke; pure managed primitive (`GCHandle.Alloc` or field-hold). |
| VSIX manifest targeting | sdk/UtinniPluginTemplates/Vsix (Visual Studio extensibility, NOT runtime) | — | Onboarding tooling; not part of the runtime binary. |
| Config file defaults | data/utinni.cfg (data file, not code) | — | Pure file-content change. |

## Standard Stack

### Core (already established in Phase 1 — re-confirmed)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xunit | 2.9.3 | Test framework | net472-compatible; Phase 1 pin. xUnit 3 requires .NET 6+. `[VERIFIED: packages.lock.json from Phase 1, line 17-20]` |
| xunit.runner.visualstudio | 3.1.5 | Test discovery for `dotnet test` | Phase 1 pin. `[VERIFIED: packages.lock.json from Phase 1]` |
| Microsoft.NET.Test.Sdk | 17.13.0 | Test platform | Phase 1 pin. `[VERIFIED: packages.lock.json from Phase 1]` |

### Supporting (already available in net472 BCL — no new packages needed)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Threading.ManualResetEventSlim` | net472 BCL | C-09 signaller (timeout-aware wait, no spin) | Replace busy-wait in `FormMain.WndProc:65-78`. `[VERIFIED: docs.microsoft.com/dotnet/api/system.threading.manualreseteventslim, net472 in supported framework list]` |
| `System.Runtime.InteropServices.GCHandle` | net472 BCL | C-16 delegate-pinning (`GCHandle.Alloc(handler, GCHandleType.Normal)`) | Anchor delegate before passing across P/Invoke boundary. `[VERIFIED: docs.microsoft.com/dotnet/api/system.runtime.interopservices.gchandle]` |
| `System.Threading.SpinLock` / `object` lock | net472 BCL | C-07 lock primitive | Standard `lock(syncRoot){}` over `private readonly object syncRoot = new object();`. |
| `System.Reflection.Assembly.GetExecutingAssembly` | net472 BCL | C-15 fallback for `slnDir` walk-up | Used in the refactored CppSharp path resolver. |
| `System.Diagnostics.Process` | net472 BCL | C-01 timing harness can `Process.Start(loaderLockHarness.exe)` and parse exit code | Standard pattern for process-isolated test fixtures. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `ManualResetEventSlim` (C-09) | `Microsoft.VisualStudio.Threading.AsyncManualResetEvent` | Async-aware variant; **NOT NEEDED** — C-09's WndProc handler is synchronous WinForms code. Adding the package introduces an unnecessary NuGet dep on `UtinniCoreDotNet.csproj` (currently zero `<PackageReference>` per STACK.md line 50). **Recommendation: stay on BCL `ManualResetEventSlim`.** |
| `GCHandle.Alloc(handler, Normal)` (C-16) | Store delegate in a `private static readonly` field | Both work as GC roots; the `Add*Callback` field approach is ALREADY in use at `GameCallbacks.cs:39-43`, `GroundSceneCallbacks.cs:38-41`, `ObjectCallbacks.cs:36` — these ARE the fix. `GCHandle.Alloc` is more explicit (no risk of a future refactor removing the field). **Recommendation: see §C-16 audit below — the existing field approach already covers callbacks that pass via `AddXxxCallback`; the bug is in any delegate-passing site that does NOT store to a field. Audit confirms NO unanchored sites remain in `GameCallbacks` / `GroundSceneCallbacks` / `ObjectCallbacks`; the comments at `:46`, `:39`, `:39` reflect a fix that was already applied. The C-16 disposition is to (1) keep the existing field approach, (2) replace the "Very odd bug" comments with a precise explanation citing CLR P/Invoke delegate-marshalling semantics, (3) optionally add a small `[Pinned]` test that registers a callback, forces `GC.Collect()`, and asserts the callback still fires without AV.** |
| Per-plugin `AssemblyCatalog` (C-06) | One catalog per directory with `ReflectionTypeLoadException` swallow | The per-plugin pattern is what assessment.md specifies; ReflectionTypeLoadException's `LoaderExceptions[*]` gives the precise failure reason which is the asked-for diagnostic. |

**Installation:**
No new packages installed this phase. Every fix uses BCL primitives already available in net472. (If C-12 widening requires bumping `Microsoft.VisualStudio.SDK` to a `[16.0,18.0)`-compatible version, that's a `<PackageReference>` version-only change on `sdk/UtinniPluginTemplates/Vsix/Vsix.csproj` — covered in §C-12 below.)

**Version verification:** Phase 1's `packages.lock.json` already pins xunit 2.9.3 / xunit.runner.visualstudio 3.1.5 / Microsoft.NET.Test.Sdk 17.13.0 — no re-verification needed. The `Microsoft.VisualStudio.SDK` package at 16.0.206 is what's in `Vsix.csproj:74` today; C-12 fix needs to research the lowest VS-2022-compatible version (likely `17.0.x` — the SDK is published as `17.13.x` as of 2024; verify exact compatible version at planning time via `nuget.org/packages/Microsoft.VisualStudio.SDK`).

## Package Legitimacy Audit

No new packages introduced this phase. The Phase-1 lockfile `UtinniCoreDotNet.Tests/packages.lock.json` is unchanged. C-12 may bump `Microsoft.VisualStudio.SDK` (already in `Vsix.csproj` since fork creation — known-good, published by Microsoft) and `Microsoft.VSSDK.BuildTools` (same provenance); both are first-party Microsoft packages. **No slopcheck gate required for this phase.**

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| Microsoft.VisualStudio.SDK | NuGet | 10+ yrs | High | github.com/microsoft | n/a (first-party) | Approved (bump only) |
| Microsoft.VSSDK.BuildTools | NuGet | 10+ yrs | High | github.com/microsoft | n/a (first-party) | Approved (bump only) |

## Per-bug fix shapes

### C-01 — DllMain loader-lock

**Surface:** `UtinniCore/utinni.cpp:138-151` (`DllMain` spawns `main()` thread) and `:99-130` (`main()` calls `pluginManager.loadPlugins()` line 123 then `clr::load()` line 129 — both forbidden inside `DLL_PROCESS_ATTACH`). Launcher side: `Launcher/main.cpp:174-210` (`inject` calls `LoadLibraryA` via `CreateRemoteThread`, then `WaitForSingleObject(hThread, INFINITE)` at `:195` — this `Wait` happens to serialize bring-up today).

**Fix shape (recommended path a — see §C-01 architectural recommendation):**
1. In `UtinniCore/utinni.cpp`: extract the body of `main()` (lines 100-130) into a new exported function `extern "C" __declspec(dllexport) DWORD WINAPI utinni_init(LPVOID)`. The body stays identical; only the signature changes (returns `DWORD`, takes `LPVOID` per `LPTHREAD_START_ROUTINE`).
2. Reduce `DllMain` `DLL_PROCESS_ATTACH` to: `DisableThreadLibraryCalls(hinstDLL); return TRUE;` — no more `CreateThread`. `DLL_PROCESS_DETACH` continues to call `detatch()` (typo fixed under STAB-03 — leave the typo alone this phase).
3. In `Launcher/main.cpp::inject()` (line 174-210): after the existing `LoadLibraryA` `CreateRemoteThread` returns its `hDll` (line 200-209), follow with a second `CreateRemoteThread` that targets `GetProcAddress(hDll, "utinni_init")`. New code path: `LPVOID lpInit = (LPVOID)GetProcAddress(<UtinniCore HMODULE in remote process — requires fixing up the remote address by adding (hDll - GetModuleHandle("UtinniCore.dll"))>` — see §C-01 architectural recommendation for the exact remote-procedure-address calculation pattern (standard DLL-injection idiom: compute the local `GetProcAddress` offset against the local `LoadLibrary` of the same DLL, then add the remote `hDll` base).

**Regression-test asserts:** The `Utinni.LoaderLockHarness` sibling exe (see §C-01 timing harness) does `QueryPerformanceCounter` before/after `LoadLibrary("UtinniCore.dll")` and asserts elapsed time < 50 ms (Windows loader-lock guidance: "DllMain should complete in milliseconds, never trigger LoadLibrary/CoCreateInstance/etc."). With the fix, `DllMain` does only `DisableThreadLibraryCalls + return TRUE` — should be <1 ms even on cold cache.

### C-02 — Cross-CRT `delete[]` in config override path

**Surface:** `UtinniCore/swg/misc/config.cpp:59-76` (`hkLoadOverrideConfig`). Line 69 reads `data` via a virtual method on the SWG TreeFile handle (`(*(int(__thiscall**)(int))(*(swgptr*)pFile + 36))(pFile)` — vtable slot 36 / 0x24, returns a buffer pointer that the SWG CRT allocated). Line 71 then does `delete[] data` — Utinni's CRT, not SWG's. Line 72 then closes the file via vtable slot 0 (the dtor).

**Fix shape:** Replace `delete[] data` on line 71 with a call to whatever SWG-side buffer-free function the matching `TreeFile::read` allocator uses. From the vtable pattern, the file's dtor at slot 0 may free the buffer too — but a safer interpretation is that the buffer is owned by the TreeFile and the dtor handles it; the current `delete[]` is a double-free in disguise. **Disposition (without an IDA decompilation handy):** the safest fix is to (a) NOT call `delete[]` at all and trust the file-close dtor at `:72` to release the buffer (this is the "TreeFile owns its read buffer" pattern common in older C++ engines), AND (b) wrap the whole block in a `try-it-in-IDA` `// ToDo` comment for verification under live SWG injection. **Alternate safer disposition (Phase-2 budget):** factor `freeConfigBuffer(byte* data, swgptr pFile)` as a static helper that just doesn't free — and document that the file dtor is the owner.

**Regression-test asserts:** xUnit test wraps the buffer-free as a testable C function via a tiny export `extern "C" __declspec(dllexport) bool utinni_test_freeConfigBuffer(...)` and asserts it returns without crashing. **Partial proof per CONTEXT.md D-05** — CRT-mismatch detection without a real CRT-mismatch fixture is hard; full proof remains in the "Tier-4 manual SWG-injection" residual. Test value: catch any future regression that reintroduces `delete[]` on a foreign-CRT pointer.

### C-03 — `Network::cast` returns uninitialized stack memory

**Surface:** `UtinniCore/swg/misc/network.cpp:65-69` (`Network::cast`). Line 67 declares `swgptr networkId;` uninitialized. Line 68 calls `swg::network::cast(&networkId, id, (id >> 32))` — the call's return value is discarded; if the SWG `cast` writes through `&networkId`, the value is now valid, BUT the function returns `int64_t` and the caller's `return swg::network::cast(...)` returns the discarded return value, not `networkId`. Note the double semicolon on line 68 (`...;;`).

**Fix shape:** Three lines:
```cpp
int64_t Network::cast(int id)
{
    swgptr networkId = 0;                                    // Initialize to defined value
    swg::network::cast(&networkId, id, (id >> 32));          // Call writes through &networkId (per typedef); discard return value (it's void-equivalent or returns the same ptr)
    return networkId;                                        // Return the OUT param, not the function return
}
```
The typedef on line 36 is `using pCast = int64_t(__thiscall*)(swgptr*, int, int);` — the first param is the OUT pointer. The function's `int64_t` return is documented as the value but the comment "// This is broken" + the workaround commits cited in CONCERNS.md TD-03 say the return is unreliable; reading `networkId` after the call is the right interpretation.

**Regression-test asserts:** Per CONTEXT.md D-05 — only the post-condition wrapper can be tested in xUnit; the cast itself calls into SWG at hard RVA `0xAA4900` and stays manual. Test value: assert `Network::cast(id) != 0xCCCCCCCC` (the MSVC debug-init pattern) and `!= 0` (uninitialized stack memory pattern) when called against a synthetic id — catches regression of the uninit-stack-mem class of bug. **Hardest case in the burn-down per CONTEXT.md D-05.**

### C-04 — `DequeuePostDrawLoopCalls` drains the wrong queue

**Surface:** `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs:97-106`. Lines 99 and 101 reference `preDrawLoopCallQueue` but should reference `postDrawLoopCallQueue`.

**Fix shape:**
```csharp
private static void DequeuePostDrawLoopCalls(IntPtr pGroundScene)
{
    Drain(postDrawLoopCallQueue);
}
private static void DequeueUpdateLoopCalls(IntPtr pGroundScene, float elapsedTime)
{
    Drain(updateLoopCallQueue);
}
private static void DequeuePreDrawLoopCalls(IntPtr pGroundScene)
{
    Drain(preDrawLoopCallQueue);
}

private static void Drain(ConcurrentQueue<Action> q)
{
    while (q.TryDequeue(out var func))   // Single-condition loop: TryDequeue returns false when empty
    {
        func();
    }
}
```
Note the loop tightening: the existing `while (q.Count > 0) { if (q.TryDequeue(...)) { ... } }` is racey (Count and TryDequeue are separately atomic) AND wastes a method call. The single-`TryDequeue` loop is the canonical drain pattern. Identical refactor applies to `DequeueMainLoopCalls` / `DequeuePreMainLoopCalls` in `GameCallbacks.cs:100-120` and `DequeueOnTargetCalls` in `ObjectCallbacks.cs:58-72`.

**Regression-test asserts:** xUnit `GroundSceneCallbacksTests.DequeuePostDrawLoopCalls_DrainsCorrectQueue_NotPreDrawQueue`: enqueue a sentinel `Action` into `postDrawLoopCallQueue` via `AddPostDrawLoopCall`, invoke the private `DequeuePostDrawLoopCalls` via reflection (or expose the queue refs as `internal` and `InternalsVisibleTo`), assert the sentinel ran AND `preDrawLoopCallQueue.Count == <prior count>`. Per CONTEXT.md D-05.

### C-05 — `GameDragDropEventHandlers` static-field pattern

**Surface:** `UtinniCoreDotNet/UI/GameDragDropEventHandlers.cs:33-44` and call site `UI/Controls/PanelGame.cs:68`. The bug: `Initialize(panel)` runs once at PanelGame construction, captures the `null`-valued static `OnDragDrop` etc. delegates, wires them to the panel — and subsequent `GameDragDropEventHandlers.OnDragDrop += handler` only mutates the static field, never re-wires the panel.

**Fix shape:** Replace static `DragEventHandler` fields with proper `static event` and a single forwarder:
```csharp
public static class GameDragDropEventHandlers
{
    public static event DragEventHandler OnDragDrop;
    public static event DragEventHandler OnDragEnter;
    public static event EventHandler OnDragLeave;
    public static event DragEventHandler OnDragOver;

    public static void Initialize(PanelGame panelGame)
    {
        panelGame.DragDrop  += (s, e) => OnDragDrop?.Invoke(s, e);
        panelGame.DragEnter += (s, e) => OnDragEnter?.Invoke(s, e);
        panelGame.DragLeave += (s, e) => OnDragLeave?.Invoke(s, e);
        panelGame.DragOver  += (s, e) => OnDragOver?.Invoke(s, e);
    }
}
```
The forwarder lambda captures the static event symbol, not the value — so subsequent `OnDragDrop += handler` adds the handler to the live invocation list that the forwarder dereferences each time. This is the canonical "static event with single-time wire-up" pattern.

**Regression-test asserts:** WinForms `Panel` test fixture (per CONTEXT.md D-05). Create a `PanelGame`, call `GameDragDropEventHandlers.Initialize(panel)`, subscribe a handler via `GameDragDropEventHandlers.OnDragDrop += ...`, synthesize a `DragEventArgs` (constructor takes `IDataObject, int, int, int, DragDropEffects, DragDropEffects` — all simple to fake), invoke the panel's `OnDragDrop` via reflection (`Panel.OnDragDrop` is protected; cast to `Control` and call `InvokeOnDragDrop` via `typeof(Control).GetMethod("OnDragDrop", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, ...)`), assert handler was called. `System.Windows.Forms` reference is already on the test csproj from Phase 1 scope-expansion commit `3b001bb`.

### C-06 — `PluginLoader.Load` swallows exceptions silently

**Surface:** `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:39-73`. Single `AggregateCatalog` over `new DirectoryCatalog(pluginDir)` + per-config `DirectoryCatalog`. If any plugin throws during compose, `ComposeParts` raises `CompositionException` (or `ReflectionTypeLoadException`) and the entire editor tears down.

**Fix shape:** Replace the single `AggregateCatalog` build with a per-plugin loop:
```csharp
public void Load()
{
    var loaded = new List<IPlugin>();
    var pluginManager = utinni.GetPluginManager();

    foreach (var pluginConfig in EnumeratePluginConfigs(pluginManager))
    {
        if (!pluginConfig.IsEnabled) continue;
        var pluginPath = utinni.GetPath() + "/Plugins/" + pluginConfig.DirectoryName + "/";

        try
        {
            var catalog = new DirectoryCatalog(pluginPath);
            var container = new CompositionContainer(catalog);
            var loader = new PerPluginLoader();
            container.ComposeParts(loader);
            loaded.AddRange(loader.Plugins);
        }
        catch (ReflectionTypeLoadException ex)
        {
            Log.Error($"Failed to load plugin '{pluginConfig.DirectoryName}': {ex.Message}");
            foreach (var inner in ex.LoaderExceptions)
            {
                Log.Error($"  LoaderException: {inner.Message}");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load plugin '{pluginConfig.DirectoryName}': {ex.GetType().Name}: {ex.Message}");
        }
    }
    Plugins = loaded;
    Log.Info($"{Plugins.Count()} .NET Plugin(s) loaded");
}

private class PerPluginLoader
{
    [ImportMany(typeof(IPlugin))]
    public IEnumerable<IPlugin> Plugins { get; set; }
}
```
`PerPluginLoader` is a private nested class with the `[ImportMany]` attribute — this avoids re-importing into the same `PluginLoader` instance across iterations. The top-level `PluginLoader.Plugins` is built up incrementally.

**Regression-test asserts:** `/Fixtures/BrokenPlugin/` deliberately-broken DLL (compile a tiny class library with a `[Export(typeof(IPlugin))]` on a class whose ctor throws), plus a `/Fixtures/GoodPlugin/` that works. Test: `PluginLoaderTests.Load_WithOneBrokenAndOneGoodPlugin_LoadsGoodPluginAndLogsBrokenOne`. Use `Log.AddOutputSinkCallback` (per CON-M-07) to capture log output; assert (a) `loader.Plugins.Count() == 1` (the good one), (b) the captured log contains the broken plugin's DLL name + the loader exception message. **Note:** the test must set `utinni.GetPath()` to a test fixtures dir before calling — this requires either an additional injectable seam OR a wrapper that the test can populate. Easiest pragmatic path: refactor `PluginLoader.Load()` to accept a `pluginDir` parameter (defaults to `utinni.GetPath() + "/Plugins/"`); the production call site (`UtinniCoreDotNet/main.cs` startup) still uses the default, the test calls with a test path. Same pattern as Phase 1 D-06's `Action<Action>` injection for `UndoRedoManager`.

### C-07 — `UndoRedoManager` thread-safety + dead `AllowMerge` + `RedoCommands.Clear` ordering

**Surface:** `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:60-74` (`AddUndoCommand`), `:76-87` (`Undo`), `:97-108` (`Redo`), `:50` (ctor calls `GameCallbacks.AddCleanupSceneCall(OnCleanupCallback)` — this is the testability-refactor seam per Phase-1 D-06). `IUndoCommand.cs:36` declares `AllowMerge()`. The bug has three sub-parts:

1. **Thread-safety:** `Stack<IUndoCommand>` is non-thread-safe. Push/Pop/Peek/Clear from game thread (via callbacks queued by `GroundSceneCallbacks.AddUpdateLoopCall`) and UI thread (via Undo/Redo button clicks).
2. **Dead `AllowMerge`:** Line 65 invokes `UndoCommands.Peek().Merge(args.UndoCommand)` without first calling `Peek().AllowMerge()`. Per `docs/ai/undo-redo.html:184-185` the intended contract is "if `Peek().AllowMerge()` then try `Peek().Merge(cmd)`". The four in-tree implementations (`AddWorldSnapshotNodeCommand`, `RemoveWorldSnapshotNodeCommand`, `WorldSnapshotNodePositionChangedCommand`, `WorldSnapshotNodeRotationChangedCommand`) all return `AllowMerge() => false` AND `Merge() => false` — so the `AllowMerge` gate is currently a no-op even when called. But the doc shows `SetTimeOfDayCommand` as the canonical merging command returning `AllowMerge() => true`.
3. **`RedoCommands.Clear` ordering:** Line 64 clears `RedoCommands` BEFORE the merge check on line 65. If merge succeeds, the original command was just absorbed into the previous top — no new state was pushed — but redo history is already gone.

**Fix shape:**
```csharp
public class UndoRedoManager
{
    private readonly Action onUpdateCommandsCallback;
    private readonly Action undoCallback;
    private readonly Action redoCallback;
    private readonly object syncRoot = new object();

    public readonly Stack<IUndoCommand> UndoCommands;
    public readonly Stack<IUndoCommand> RedoCommands;

    // Phase-1 D-06 testability seam: caller injects how to register the cleanup callback.
    // Production callers pass GameCallbacks.AddCleanupSceneCall; tests pass a no-op.
    public UndoRedoManager(Action onUpdateCommandsCallback, Action undoCallback, Action redoCallback,
                           Action<Action> registerCleanupCallback = null)
    {
        UndoCommands = new Stack<IUndoCommand>();
        RedoCommands = new Stack<IUndoCommand>();
        this.onUpdateCommandsCallback = onUpdateCommandsCallback;
        this.undoCallback = undoCallback;
        this.redoCallback = redoCallback;

        (registerCleanupCallback ?? GameCallbacks.AddCleanupSceneCall)(OnCleanupCallback);
    }

    private void OnCleanupCallback()
    {
        lock (syncRoot)
        {
            UndoCommands.Clear();
            RedoCommands.Clear();
        }
        onUpdateCommandsCallback();
    }

    public void AddUndoCommand(IEditorPlugin editorPlugin)
    {
        editorPlugin.AddUndoCommand += (sender, args) =>
        {
            lock (syncRoot)
            {
                // Merge first; only Clear redo if we actually push a new command (TD-29 fix).
                if (UndoCommands.Count > 0
                    && UndoCommands.Peek().AllowMerge()           // C-07: call the gate (was dead code)
                    && UndoCommands.Peek().Merge(args.UndoCommand))
                {
                    return;
                }

                RedoCommands.Clear();                              // TD-29: moved AFTER merge check
                UndoCommands.Push(args.UndoCommand);
            }
            onUpdateCommandsCallback();
        };
    }

    public void Undo()
    {
        IUndoCommand cmd;
        lock (syncRoot)
        {
            if (UndoCommands.Count == 0) return;
            cmd = UndoCommands.Pop();
            RedoCommands.Push(cmd);
        }
        cmd.Undo();
        undoCallback();
    }

    public void Redo()
    {
        IUndoCommand cmd;
        lock (syncRoot)
        {
            if (RedoCommands.Count == 0) return;
            cmd = RedoCommands.Pop();
            UndoCommands.Push(cmd);
        }
        cmd.Execute();
        redoCallback();
    }
    // Undo(int)/Redo(int) continue to call Undo()/Redo() in a loop; the lock is per-iteration.
}
```
Three notes:
- The lock is held during stack mutation only; the `cmd.Undo()` / `cmd.Execute()` calls happen OUTSIDE the lock (those calls re-enter `GroundSceneCallbacks.AddUpdateLoopCall` and would deadlock if held under the same lock).
- The `onUpdateCommandsCallback` invocation happens outside the lock to avoid the same deadlock class.
- Preservation: CON-M-05 (`OnCleanupCallback` clearing both stacks on scene-cleanup) is preserved — the body is unchanged, only wrapped in a lock.

**Disposition: keep `AllowMerge` in `IUndoCommand` interface.** Rationale: the documented contract (`docs/ai/undo-redo.html:54-55, 184-185`) requires it, the canonical `SetTimeOfDayCommand` example demonstrates the value (cheap merging of camera-time slider drags), and removing it from the interface would be a breaking change for any plugin author (including future Wave-1 plugins) that wants to opt into merging. The four current in-tree implementations all return `false` — that's not evidence the contract is dead, just evidence the four current commands don't benefit from merging.

**Regression-test asserts:** Per CONTEXT.md D-05, three tests:
- `AddUndoCommand_ConcurrentPushFromMultipleThreads_NoRaceConditions` — spawn N threads each pushing M commands; assert `UndoCommands.Count == N * M` and no thrown exception. (No `lock(syncRoot)` → flaky `Count` mismatch or `InvalidOperationException`.)
- `AddUndoCommand_MergeReturnsTrue_DoesNotClearRedoStack` — fake `IUndoCommand` whose `AllowMerge()` and `Merge()` both return true; push an initial command, push a few via `RedoCommands.Push(...)`, then push a new one that merges; assert `RedoCommands.Count` is unchanged.
- `AddUndoCommand_NewCommandActuallyPushed_DoesClearRedoStack` — same setup but `Merge()` returns false (mimicking the four real commands); assert `RedoCommands.Count == 0` after.

All three use a `new UndoRedoManager(() => {}, () => {}, () => {}, registerCleanupCallback: _ => {})` — the no-op `registerCleanupCallback` lambda is the Phase-1 deferred testability refactor in action. Per CONTEXT.md "Claude's discretion": the testability refactor lands as part of the C-07 fix task (single atomic commit) — splitting it is needless commit-size overhead since the seam IS the test enablement.

### C-08 — `Hotkey.ProcessString` throws on bad input

**Surface:** `UtinniCoreDotNet/Hotkeys/Hotkey.cs:66-92` (`ProcessString`). Lines 82 and 91 do `Enum.Parse(typeof(Keys), modifiers, true)` / `Enum.Parse(typeof(Keys), key, true)`. On a typo (e.g. `"Ctrl + T"` — `Ctrl` isn't a Keys enum name; the right value is `Control`) `Enum.Parse` throws `ArgumentException`. Compounding bug per FR-07: `HotkeyManager.Load()` at `:106-114` calls `Hotkey.UpdateKeys(string)` which routes through the same `ProcessString` — every `input.ini` load is a separate failure surface. Also: the multi-modifier case `"Shift + Alt + Z"` — `ProcessString` splits on the FIRST `+` and passes `"Alt + Z"` to `Enum.Parse`, which on net472 raises `ArgumentException` (verified by Phase 1 commit `b4d2137`; that test is currently `[Fact(Skip = "C-08:...")]`).

**Fix shape:**
```csharp
private void ProcessString(string keyComboStr)
{
    if (string.IsNullOrEmpty(keyComboStr))
    {
        Log.Warning("Hotkey " + Name + " failed to process empty key combo string.");
        Enabled = false;
        return;
    }

    // Split on '+', trim each segment.
    var segments = keyComboStr.Split('+');
    for (int i = 0; i < segments.Length; i++) segments[i] = segments[i].Trim();

    if (segments.Length == 1)
    {
        if (!Enum.TryParse(segments[0], true, out Keys k))
        {
            Log.Warning($"Hotkey '{Name}' has unknown key '{segments[0]}' — disabling.");
            Enabled = false;
            return;
        }
        ModifierKeys = Keys.None;
        Key = k;
        return;
    }

    // 2+ segments: first segment is modifier (or composite of modifiers ORed by space?
    // The existing API does NOT support OR-composing — the docs use 'Control + S' shape only.
    // For multi-modifier 'Shift + Alt + Z', combine intermediate segments as Keys.X | Keys.Y
    // for the modifier part, and the LAST segment is the key.
    Keys mods = Keys.None;
    for (int i = 0; i < segments.Length - 1; i++)
    {
        if (!Enum.TryParse(segments[i], true, out Keys m))
        {
            Log.Warning($"Hotkey '{Name}' has unknown modifier '{segments[i]}' — disabling.");
            Enabled = false;
            return;
        }
        mods |= m;
    }
    if (!Enum.TryParse(segments[segments.Length - 1], true, out Keys keyOnly))
    {
        Log.Warning($"Hotkey '{Name}' has unknown key '{segments[segments.Length - 1]}' — disabling.");
        Enabled = false;
        return;
    }
    ModifierKeys = mods;
    Key = keyOnly;
}
```
Two changes vs. status quo: (1) `Enum.TryParse` instead of `Enum.Parse` (no throws — log + `Enabled = false` instead); (2) split on `+` and handle 2+ segments. The Phase-1 multi-modifier-chord test (`Ctor_StringConstructor_MultiModifierChord_ParsesFlags`) expects `"Shift + Alt + Z"` → `ModifierKeys = Shift, Key = Alt | Z` — but reading the existing `GetKeyComboString()` at `Hotkey.cs:105-113` which emits `ModifierKeys + " + " + Key` for round-trips, the round-trip target for `Shift | Alt` modifiers + `Z` key is `"Shift, Alt + Z"` (because `Keys.Shift | Keys.Alt` ToString-renders as `"Shift, Alt"` on net472 — `[Flags]` rendering). So the existing Phase-1 test data is actually wrong: it asserts `ModifierKeys = Shift, Key = Alt | Z` which would round-trip as `"Shift + Alt, Z"` — not equivalent. **The right fix is whichever interpretation matches `GetKeyComboString`'s output format**, which is `[mods] + [key]` with `+` as separator and a single key (not a Keys-OR-Keys composite key). Disposition: **the test in Phase 1 is wrong** — the right contract is "first N-1 segments are modifiers that get ORed together; last segment is the single key". Update the test assertion to `ModifierKeys = Shift | Alt, Key = Z` in the same Phase-2 commit. **Final on the C-08 fix:** unskip both Phase-1 `[Skip = "C-08:..."]` tests, update the multi-modifier assertion to `(Shift | Alt, Z)`, and verify all 4 tests pass.

**Regression-test asserts:** The two existing skipped tests turn green; one new test added for "single key with no modifier no longer throws on `F1`-style happy path" (already covered by `Ctor_StringConstructor_SingleKey_SetsKeyAndNoModifier`); one new test for the malformed-modifier case `MalformedModifier_DoesNotThrow_DisablesHotkey` asserting `Record.Exception(...) == null` AND `hk.Enabled == false`.

### C-09 — UI/game-thread busy-wait deadlock

**Surface:** `UtinniCoreDotNet/UI/Forms/FormMain.cs:57-78`. Lines 65-73: `BlockPresent(true)` then `while (!IsPresentBlocked()) Thread.Sleep(1)` — no timeout, no signal, no escape. Native counterpart: `UtinniCore/swg/graphics/directx9.cpp:210-241` (`hkPresent`) — when `blockPresentCall` is true it sets `isPresenting = false` and skips `present()`; when false it sets `isPresenting = true` and calls `present()`. The two-flag dance is the protocol the UI thread is polling for.

**Fix shape:**

Native side (`directx9.cpp`) — add a signaller. The native code can't directly signal a managed `ManualResetEventSlim`, but it CAN poll a flag, OR (cleaner) expose a function the managed side hooks into. **Simplest design:** add a third native flag `presentBlocked` that `hkPresent` sets to true whenever it observes `blockPresentCall == true`, and expose it as a "wait for blocked" function. **Even simpler:** the managed side can just `Wait` on a `ManualResetEventSlim` that the existing native flag pair feeds via a thin native helper:

```cpp
// directx9.cpp additions (or sibling file)
static HANDLE hPresentBlockedEvent = nullptr;
extern "C" UTINNI_API HANDLE getPresentBlockedEvent() {
    if (!hPresentBlockedEvent) {
        hPresentBlockedEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);  // manual-reset
    }
    return hPresentBlockedEvent;
}
// in hkPresent, after observing blockPresentCall == true and setting isPresenting = false:
//   SetEvent(hPresentBlockedEvent);
// in blockPresent(bool value):
//   if (!value) ResetEvent(hPresentBlockedEvent);
```

Managed side (`FormMain.cs`):
```csharp
// Cache the handle once; the value is a Win32 HANDLE wrapped as IntPtr.
private static readonly Lazy<EventWaitHandle> presentBlockedSignal = new Lazy<EventWaitHandle>(() =>
{
    IntPtr h = UtinniCore.DirectX.directx9.GetPresentBlockedEvent();
    var ewh = new EventWaitHandle(false, EventResetMode.ManualReset);
    // Replace its SafeWaitHandle with the native one:
    ewh.SafeWaitHandle = new SafeWaitHandle(h, ownsHandle: false);
    return ewh;
});

protected override void WndProc(ref Message m)
{
    if (m.Msg == Native.WM_SYSCOMMAND)
    {
        int command = m.WParam.ToInt32() & 0xFFF0;
        if (command == Native.SC_MINIMIZE || command == Native.SC_RESTORE || command == Native.SC_MAXIMIZE)
        {
            UtinniCore.DirectX.directx9.BlockPresent(true);
            // Wait up to 100 ms for the game thread to confirm Present is blocked.
            // If it never signals (game thread is wedged), fall through — minimize/restore
            // is best-effort, not a correctness gate.
            presentBlockedSignal.Value.WaitOne(TimeSpan.FromMilliseconds(100));
        }
    }
    base.WndProc(ref m);
}
```

**Confirmation per research focus #3:** `System.Threading` primitives are sufficient. The win32 `HANDLE` is cleanly wrappable as a `SafeWaitHandle` on a managed `EventWaitHandle` — net472 BCL primitives, no `Microsoft.VisualStudio.Threading` package needed. **No new `<PackageReference>` required.** (If a researcher reading this in 2 years finds the SafeWaitHandle wrapping awkward, an alternate design is: managed side just `Thread.Sleep(1)` in a loop with a `Stopwatch`-based timeout — strictly worse than the event but a fallback if the Win32 event handle marshalling proves flaky in practice. Recommended: try the event first.)

**Regression-test asserts:** Per CONTEXT.md D-05 — "mock signaller harness; assert `ManualResetEventSlim.WaitOne` returns within timeout AND no `Thread.Sleep(1)` spin observed". The xUnit test creates a fake `IPresentBlockedSignaller` interface (introduce one for testability — `WaitOne(TimeSpan)` returning bool), wires the WndProc handler against a mock, never signals, asserts the handler returns within `timeout + epsilon` (no infinite spin). Plus a positive case: signal the event, assert the handler returns under timeout. **The WndProc-handler-extracted method needs a name: e.g. `WaitForPresentBlock(TimeSpan timeout)` as `internal` on `FormMain`, callable from the test via `InternalsVisibleTo`.

### C-10 — `clr::stop()` null deref

**Surface:** `UtinniCore/clr.cpp:93-102`. Lines 95-97 do raw `Release()` on three COM pointers (`pClrRuntimeHost`, `pClrRuntimeInfo`, `pClrMetaHost`) with no null checks. If `start()` failed at any of its four `SUCCEEDED(hr)` guards (lines 47, 51, 56, 60), the failure path at lines 73-90 already nulled the pointers — then `detatch()` at `utinni.cpp:132-136` calls `stop()` from `DLL_PROCESS_DETACH` and crashes inside the loader lock. Also: `stop()` can be called twice (once from C-01-path-(a) failure cleanup, once from `detatch()`).

**Fix shape:**
```cpp
void stop()
{
    if (pClrRuntimeHost) { pClrRuntimeHost->Release(); pClrRuntimeHost = nullptr; }
    if (pClrRuntimeInfo) { pClrRuntimeInfo->Release(); pClrRuntimeInfo = nullptr; }
    if (pClrMetaHost)    { pClrMetaHost->Release();    pClrMetaHost = nullptr; }
}
```
Three-line idempotent shutdown. Alternative is `Microsoft::WRL::ComPtr<T>` (CONCERNS.md TD-10 mentions this) but introduces a `<wrl/client.h>` include this phase doesn't otherwise need; the null-check pattern is smaller and matches the existing C style.

**Regression-test asserts:** Per CONTEXT.md D-05 — "P/Invoke `clr::stop()` from `UtinniCore.dll`; call twice from xUnit; assert no AV". Expose `clr::stop` as `extern "C" UTINNI_API void utinni_clr_stop()` (it's already in `clr` namespace; just add an export wrapper). Test calls it via `[DllImport("UtinniCore.dll")]`. Twice in a row — second call hits the null-checked path. Asserts: no `AccessViolationException` thrown.

### C-11 — DirectX9 `findPattern` null check

**Surface:** `UtinniCore/swg/graphics/directx9.cpp:297-303` (`getVtbl()`). Line 300 calls `findPattern` against `GetModuleHandle("d3d9.dll")` for 0x128000 bytes. If d3d9.dll isn't loaded yet, `GetModuleHandle` returns null → `findPattern` walks address `0..0x128000` → returns 0 (no match) OR crashes. Line 301 then does `memcpy(&vtbl, (void*)(((swgptr)pDevice) + 2), 4)` — `pDevice + 2 = 0x2`, copies 4 bytes from address `0x2` → access violation.

**Fix shape:**
```cpp
swgptr* getVtbl()
{
    HMODULE hD3d9 = GetModuleHandle("d3d9.dll");
    if (hD3d9 == nullptr)
    {
        utinni::log::critical("DirectX9 hook installation failed: d3d9.dll not loaded yet");
        return nullptr;
    }

    swgptr* vtbl = nullptr;
    auto pDevice = (LPDIRECT3DDEVICE9)memory::findPattern(
        (swgptr)hD3d9, 0x128000,
        "\xC7\x06\x00\x00\x00\x00\x89\x86\x00\x00\x00\x00\x89\x86", "xx????xx????xx");
    if (pDevice == nullptr)
    {
        utinni::log::critical("DirectX9 hook installation failed: vtable pattern not found in d3d9.dll");
        return nullptr;
    }

    memcpy(&vtbl, (void*)(((swgptr)pDevice) + 2), 4);
    return vtbl;
}

void detour()
{
    auto vtbl = getVtbl();
    if (vtbl == nullptr) return;          // Bail; subsequent hooks would deref null vtable.
    // ... existing code unchanged ...
}
```

**Preservation note (CONTEXT.md §preservation guard-rails + CON-N-04):** `memory::findPattern` does NOT touch `VirtualProtect`; only `memory::copy` / `memory::createJMP` do (`memory.cpp:63-72, 113-125`). The C-11 fix adds null-checks to a pure-read pattern scan — does NOT modify `memory::copy` and therefore does NOT touch CON-N-04. The "preservation note" in CONTEXT.md §canonical_refs is a guard-rail against accidentally fixing C-11 by reaching into `memory::copy` and breaking the VirtualProtect bracket — the fix above avoids that surface entirely.

**Regression-test asserts:** Per CONTEXT.md D-05 — "P/Invoke `utility/memory::findPattern` with a buffer where the pattern is absent; assert return is 0 AND call site at `directx9.cpp:297-303` bails with `log::critical` rather than `memcpy`-from-0x2." Expose `memory::findPattern` as `extern "C" UTINNI_API`. Test: allocate a managed `byte[0x1000]` of zeros; pin it; pass the address + size + a non-matching pattern; assert return is 0. Plus a second test that calls `getVtbl()` (exposed similarly) with `d3d9.dll` deliberately not loaded (test runs in the test runner process, where `d3d9.dll` is not typically loaded — perfect environment); assert return is null AND a critical log line was emitted (captured via `Log.AddOutputSinkCallback` if available on the native side, or by an additional `lastErrorMessage` export).

### C-12 — VSIX manifest VS 2019 → VS 2022 widening

**Surface:** `sdk/UtinniPluginTemplates/Vsix/source.extension.vsixmanifest:9-11, 17`. Four `[16.0,17.0)` ranges (three `InstallationTarget` + one `Prerequisite`). Companion: `sdk/UtinniPluginTemplates/Vsix/Vsix.csproj:74` — `Microsoft.VisualStudio.SDK 16.0.206` and `:75` — `Microsoft.VSSDK.BuildTools 16.8.3038`. `Vsix.csproj:4` — `<MinimumVisualStudioVersion>16.0</MinimumVisualStudioVersion>` (this stays — minimum is VS 2019, maximum widens to VS 2022 = `18.0`-exclusive).

**Fix shape (per CONTEXT.md D-12 default-fallback for CON-O-04 — see §CON-O-04 below):**

`source.extension.vsixmanifest` — change all four version ranges:
```xml
<InstallationTarget Id="Microsoft.VisualStudio.Community"  Version="[16.0,18.0)" />
<InstallationTarget Version="[16.0,18.0)" Id="Microsoft.VisualStudio.Pro" />
<InstallationTarget Version="[16.0,18.0)" Id="Microsoft.VisualStudio.Enterprise" />
<!-- and -->
<Prerequisite Id="Microsoft.VisualStudio.Component.CoreEditor" Version="[16.0,18.0)" DisplayName="Visual Studio core editor" />
```

`Vsix.csproj` — bump the SDK and BuildTools `<PackageReference>` versions:
```xml
<PackageReference Include="Microsoft.VisualStudio.SDK" Version="17.0.32112.339" ExcludeAssets="runtime" />
<PackageReference Include="Microsoft.VSSDK.BuildTools" Version="17.13.2069" />
```
**Version-pin caveat:** the exact "lowest VS-2022-compatible version" needs verification at planning time via `nuget.org`. As of late 2024, `Microsoft.VisualStudio.SDK 17.0.x` is the entry-point for VS 2022 targeting. The planner should run `dotnet add package Microsoft.VisualStudio.SDK --version <X>` against a checkout and verify the package restores cleanly + the VSIX builds.

**Other VSIX-manifest changes needed:** None. `<MinimumVisualStudioVersion>16.0</MinimumVisualStudioVersion>` stays (it's a minimum; widening the InstallationTarget upper bound is a no-op against the minimum). The `<Dependency Id="Microsoft.Framework.NDP" Version="[4.7.2,)" />` line is fine (open-ended upper). No prerequisites block edits beyond the one shown.

**Regression-test asserts:** Per CONTEXT.md D-05 — "xUnit asserts `source.extension.vsixmanifest` XML has `InstallationTarget Version=\"[16.0,18.0)\"`". The test loads the manifest as an `XDocument`, queries all `InstallationTarget` elements via `XName.Get("InstallationTarget", "http://schemas.microsoft.com/developer/vsx-schema/2011")`, asserts every `Version` attribute equals `"[16.0,18.0)"`. Same for the Prerequisite element. **Plus the C-12 commit-message `Verified-by:` block per CONTEXT.md §domain D-06**: maintainer installs the built VSIX into both VS 2019 (16.x) and VS 2022 (17.x) IDEs and pastes the result. Manual residual.

### C-13 — TJT Debug path (cross-repo)

**Surface:** `D:\Code\UtinniPlugins\The Jawa Toolbox\TheJawaToolbox\TheJawaToolbox.vcxproj:63` — `<OutDir>..\..\..\..\Utinni\bin\Debug\Plugins\TheJawaToolbox\</OutDir>` — FOUR `..\` levels. Lines 67 (Release) and 71 (RelWithDbgInfo) both use THREE `..\` correctly. Plus `D:\Code\UtinniPlugins\The Jawa Toolbox\TheJawaToolbox.sln:26` — `{EA1E6CED-...}.Debug|x86.ActiveCfg = Debug|Win32` but MISSING the corresponding `.Debug|x86.Build.0 = Debug|Win32` entry (compare lines 27-28 Release and 29-30 RelWithDbgInfo, both of which have ActiveCfg AND Build.0).

**Fix shape (cross-repo direct commit per CONTEXT.md D-09):**
1. `TheJawaToolbox/TheJawaToolbox.vcxproj` line 63: change `..\..\..\..\` → `..\..\..\` (drop one `..\` level).
2. `TheJawaToolbox.sln` line 26-27: insert a missing `Debug|x86.Build.0 = Debug|Win32` line between current line 26 (`ActiveCfg`) and the existing line 27 (which starts the Release block):
```
{EA1E6CED-6315-486A-8AA7-D41DF9D34888}.Debug|x86.ActiveCfg = Debug|Win32
{EA1E6CED-6315-486A-8AA7-D41DF9D34888}.Debug|x86.Build.0 = Debug|Win32       <-- ADD THIS LINE
{EA1E6CED-6315-486A-8AA7-D41DF9D34888}.Release|x86.ActiveCfg = Release|Win32
```

**Regression-test asserts:** Per CONTEXT.md D-05 — "cross-repo commit; no automated test in this repo. Manual verify: build TJT Debug locally + confirm output appears in `bin/Debug/`." Plan 02-01's C-13 task is marked `repo:UtinniPlugins`. Commit message format per CONTEXT.md §specifics: `fix(C-13): TJT Debug path uses ..\..\..\ instead of ..\..\..\..\`. PR cites assessment.md C-13 + this CONTEXT.md.

### C-14 — utinni.cfg blank login default

**Surface:** `data/utinni.cfg:4-5` — `loginServerPort0=44453` and `loginServerAddress0=login.swgemu.com`.

**Fix shape:**
```ini
[ClientGame]
    loginServerPort0=
    loginServerAddress0=
    # Set your shard's login host and port here. Utinni intentionally ships these blank
    # for the sovereign-fork to avoid defaulting users into SWGEmu's infrastructure (CON-D-01).
    skipIntro=1
```

**Regression-test asserts:** Per CONTEXT.md D-05 — "xUnit file-content assertion on `data/utinni.cfg` — `loginServerAddress0=` is blank, `loginServerPort0=` is blank". The test reads `data/utinni.cfg` from the repo root (the test runs in CI from the Utinni.Tests project; the working dir during `dotnet test` rooted at the test bin dir means we need to compute the cfg path either via `Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "utinni.cfg")` or by referencing the cfg as a `<None Include="...\data\utinni.cfg" CopyToOutputDirectory="PreserveNewest" />` content item in the test csproj). The simpler approach: copy a SNAPSHOT into `/Fixtures/utinni.cfg` and assert against the snapshot — but that defeats the purpose (the snapshot lives in tests/ and the live file lives in data/). **Disposition:** add a relative path resolver (`new FileInfo(typeof(UtinniCfgTests).Assembly.Location).Directory.Parent.Parent.Parent.Parent` to walk up to repo root, then `/data/utinni.cfg`) — fragile if the test project moves, but robust enough for CI which always runs from the same layout. Assert via regex: `Assert.Matches(@"loginServerAddress0\s*=\s*$", content)` (anchored end-of-line; matches "key = " with no value).

### C-15 — CppSharp slnDir brittle

**Surface:** `UtinniCoreDotNetGen/Program.cs:39-41`. Line 41: `string slnDir = Directory.GetParent(workingDir.Substring(0, workingDir.LastIndexOf("\\bin\\"))).FullName + "\\";`. If `workingDir` doesn't contain `\bin\`, `LastIndexOf` returns -1 → `Substring(0, -1)` throws `ArgumentOutOfRangeException`.

**Fix shape:** Refactor `slnDir` resolution into a pure function and prefer `args[0]` if provided:
```csharp
public static string ResolveSlnDir(string workingDir, string[] args)
{
    // Preference 1: explicit arg from $(SolutionDir) passed in post-build.
    if (args != null && args.Length > 0 && Directory.Exists(args[0]))
    {
        return args[0].TrimEnd('\\', '/') + "\\";
    }

    // Preference 2: walk up from workingDir looking for Utinni.sln.
    var dir = new DirectoryInfo(workingDir);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Utinni.sln")))
        {
            return dir.FullName.TrimEnd('\\', '/') + "\\";
        }
        dir = dir.Parent;
    }

    // Preference 3: env var fallback.
    var env = Environment.GetEnvironmentVariable("UTINNI_SLN_DIR");
    if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
    {
        return env.TrimEnd('\\', '/') + "\\";
    }

    throw new InvalidOperationException(
        $"Could not resolve Utinni solution directory. Pass $(SolutionDir) as args[0], " +
        $"run from a build output directory inside the solution tree, or set UTINNI_SLN_DIR.");
}
```
Wire it into the existing `Setup`:
```csharp
public void Setup(Driver driver)
{
    string workingDir = AppDomain.CurrentDomain.BaseDirectory;
    string buildMode = new DirectoryInfo(workingDir).Name;
    string slnDir = ResolveSlnDir(workingDir, _args);     // _args set from Main(string[] args)
    // ... rest unchanged ...
}

class Gen : ILibrary
{
    private readonly string[] _args;
    public Gen(string[] args) { _args = args; }
    // ... existing members ...
}

static void Main(string[] args)
{
    ConsoleDriver.Run(new Gen(args));
}
```
Also update `UtinniCore.vcxproj:95` post-build event to pass `$(SolutionDir)` to the generator:
```xml
<Command>xcopy /E /Y "$(SolutionDir)data" "$(TargetDir)" /d
$(SolutionDir)UtinniCoreDotNetGen\bin\$(Configuration)\UtinniCoreDotNetGen.exe "$(SolutionDir)"</Command>
```
The trailing `"$(SolutionDir)"` is the new arg.

**Regression-test asserts:** Per CONTEXT.md D-05 — "refactor `slnDir` resolution into a pure function; xUnit with synthetic paths (CI-runner-style path without `\bin\`, `$(SolutionDir)` arg path, env-var fallback path)". Three tests:
- `ResolveSlnDir_ArgZeroProvided_UsesIt` — pass `args = new[] { "C:/temp/myproj" }` and a real temp dir; assert returned path is the arg.
- `ResolveSlnDir_NoArgsButWalkUpFindsUtinniSln_UsesIt` — create a fake `Utinni.sln` in a temp dir hierarchy; `workingDir` is several levels deep; assert returned path is the dir with the sln.
- `ResolveSlnDir_NoArgsNoWalkupHit_NoEnvVar_Throws` — pass a path with no `Utinni.sln` ancestor; assert `InvalidOperationException`.

### C-16 — `GameCallbacks` delegate-pinning (resolves CON-O-03)

**Surface:** `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:39-58` — five `private static UtinniCore.Delegates.Action_ xxxAction;` fields hold delegates passed to native via `Add*Callback`. Comment line 46: "Storing this in a variable is somehow needed to prevent corruption on WinForms resize. Very odd bug that I still don't fully understand." `GroundSceneCallbacks.cs:38-41` — four similar fields. `ObjectCallbacks.cs:36` — one such field.

**Disposition (research focus #7):**

The "Very odd bug" comment IS the symptom of GC-collected delegates passed to unmanaged code without a GC root. CLR P/Invoke marshalling creates a "stub" wrapper around a delegate when it's passed to a native function as a function pointer — but the CLR does NOT keep the wrapper alive on its own; if the managed delegate goes out of scope, the GC collects it and the stub becomes a dangling pointer. Source: docs.microsoft.com/dotnet/standard/native-interop/best-practices §"Function Pointers" — "Make sure that the delegate is not garbage-collected before the unmanaged code is finished with it. You can use `GCHandle.Alloc` to keep the delegate alive."

**The existing field approach IS a valid fix** — a `static readonly` field is a GC root, and as long as that field references the delegate, the GC will not collect it. The comment is stale: this WAS the bug, and storing-in-a-field WAS the fix. The CON-O-03 disposition is therefore not "we need to add `GCHandle.Alloc`" but "we need to (a) verify every callback-passing site uses the field approach, (b) replace the misleading comment with a precise explanation, (c) add a regression test that forces `GC.Collect()` to prove the fix holds."

**Audit per file:**

`GameCallbacks.cs:39-58` — covered. All 5 callbacks have backing fields.

`GroundSceneCallbacks.cs:38-53` — covered. All 4 callbacks have backing fields (`dequeueUpdateLoopCallsAction`, `dequeuePreDrawLoopCallsAction`, `dequeuePostDrawLoopCallsAction`, `callCameraChangeCallbacksAction`).

`ObjectCallbacks.cs:36-41` — covered. `dequeueOnTargetCallsAction` field.

**Verdict:** All three files ALREADY anchor their delegates via static fields. **C-16 is therefore a verification + documentation + regression-test task**, NOT a code-fix task. The fix shape is:

1. Replace the misleading comment at `GameCallbacks.cs:46` with: `// The static field acts as a GC root for the delegate passed to native via Add*Callback. Without it, the GC can collect the managed delegate while native still holds its stub, causing AVs on later callback dispatch. See https://docs.microsoft.com/dotnet/standard/native-interop/best-practices#function-pointers`
2. Same comment-fix at `ObjectCallbacks.cs:39`.
3. Add a regression test that registers a callback, releases the local reference, calls `GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();`, then dispatches the callback via native (P/Invoke through the existing `AddXxxCallback`-then-trigger path or via a test-only export); assert no AV.

**Alternate fix shape (more defensive):** add `GCHandle.Alloc(handler, GCHandleType.Normal)` alongside the field — belt-and-suspenders. Argument against: increases boilerplate and pinning lifecycle complexity; the field approach is sufficient if the field is `static readonly` or assigned-once. Argument for: explicit pinning is more obvious to future maintainers. **Recommendation: stay with the field approach** — it's idiomatic, working, and the comment-fix + test makes the intent explicit.

**Per-file fix list:**
| File | Change |
|------|--------|
| `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` | Replace comment on line 46 with the CLR-marshalling explanation. |
| `UtinniCoreDotNet/Callbacks/ObjectCallbacks.cs` | Replace comment on line 39 with the same explanation. |
| `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs` | No comment to replace (no "Very odd bug" comment exists in this file today — the field pattern is just used without explanation). Optionally add the same explanatory comment above the field block at line 38-41 for consistency. |

**Single task or three tasks?** CONTEXT.md leaves this to "Claude's Discretion". **Recommendation: ONE task** — all three files get the same comment-update + the regression test sits with the test files. Single commit, single PR, single review.

**Regression-test asserts:** xUnit `GameCallbacksTests.RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV`:
```csharp
[Fact]
public void RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV()
{
    GameCallbacks.Initialize();   // wires the static-field-anchored delegates
    bool fired = false;
    Action callback = () => fired = true;
    GameCallbacks.AddInstallCallback(callback);
    callback = null;              // drop local reference
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    // Now trigger CallInstallCallbacks via test-only P/Invoke into UtinniCore
    // (need to expose Game::triggerInstallCallbacks as extern "C" UTINNI_API for the test)
    UtinniCore.Utinni.Game.TriggerInstallCallbacks();   // hypothetical test-only export
    Assert.True(fired);
}
```
The test exposes a "trigger" function on the native side that walks the callback registry without going through the actual SWG hook fire path. **Caveat:** this requires a NEW native export (`triggerInstallCallbacks` etc.) — that's added under the C-16 fix commit, NOT under a separate refactor. The export adds one C++ function per callback registry; small surface, single-purpose, test-only.

### KB-05 — `isSafeToUse` `||` vs `&&` operator (folds into CON-O-01 disposition per D-11)

**Surface:** `UtinniCore/swg/game/game.cpp:305-308`:
```cpp
bool Game::isSafeToUse()
{
    return memory::read<bool>(0x01908858) || memory::read<bool>(0x01919410);
}
```
`docs/ai/internals.md:231` documents the contract as: "Both must be true" (AND semantics).

**Fix shape (per CONTEXT.md D-12 default-fallback):**
```cpp
bool Game::isSafeToUse()
{
    // Returns true only when both SWG-internal safety flags are set. Per docs/ai/internals.md:231,
    // "AND ... Both must be true" — the operator was previously || (logical-OR), which would
    // return true when only one flag is set, allowing world-snapshot mutations during scene
    // transitions that the second flag would have blocked. CON-O-01 disposition: docs/ai/internals.md
    // is the source of truth; the operator is &&. See assessment.md Open Questions §1.
    return memory::read<bool>(0x01908858) && memory::read<bool>(0x01919410);
}
```
The one-line operator change (`||` → `&&`) is paired with the comment + the `docs/ai/assessment.md` Open Questions §1 disposition update per CONTEXT.md D-11.

**Regression-test asserts:** Not unit-testable in this repo (the function reads from hard-coded SWG RVAs `0x01908858` / `0x01919410`). The behavior change is operator-level and the values are SWG-internal — only an integration test against a live SWG could exercise this. Stays in the Tier-4 manual residual. The commit message documents the change: `fix(KB-05): isSafeToUse uses && per docs/ai/internals.md (resolves CON-O-01)`.

## C-01 architectural recommendation

**Recommendation: path (a) — export `utinni_init`, launcher fires it via `CreateRemoteThread` after `LoadLibraryA` returns.**

### Path comparison

| Criterion | (a) `utinni_init` + remote thread | (b) defer to `Game::install` detour | (c) hybrid |
|-----------|----------------------------------|------------------------------------|------------|
| Native code delta | Small (~30 lines: extract `main` body, export new symbol, shrink `DllMain` to one line) | Medium-large (CLR bring-up moves into the `Game::install` hook; need to handle "callbacks fire before CLR is up" for early callbacks; need a "first-fire-wins" guard) | Large (both paths exist; need to handle "if launcher used path-a, don't double-bring-up from path-b") |
| Launcher code delta | Small (~10 lines: add a second `CreateRemoteThread` after the existing `LoadLibraryA` one) | Zero | Same as (a) |
| Failure modes added | (a) requires correctly resolving `utinni_init`'s remote-process address — standard injection idiom (compute via `GetProcAddress` locally then offset by `hDll - localBase`) | (b) early SWG callbacks (those firing during `Game::install` itself) need defensive guards because the CLR may not be up; risk of double-init on hot-reload | (c) two paths means two failure-mode trees; deferred-init failure recovery is harder to reason about |
| Reverts cleanly | Yes — fixes are isolated to `utinni.cpp` + `Launcher/main.cpp` | Less so — moves CLR bring-up to a different lifecycle stage, callsites are spread across the SWG callback set | No |
| Phase 2 success criterion #4 ("DllMain no heavy startup, CLR deferred") | Met (DllMain returns immediately; CLR bring-up happens on the explicit `utinni_init` thread) | Met (DllMain returns immediately; CLR bring-up happens during `Game::install`) | Met |

### Why (a) over (b)

The `Game::install` detour fires fairly late in SWG's startup — `swg/game/game.cpp:54` says `pInstall install = (pInstall)0x00422E80;`, and from `hkInstall` at `:154-170` we can see that install runs AFTER scene-construction code that may depend on plugin presence. If plugins want to subscribe to install callbacks (which they do — `GameCallbacks.AddInstallCallback` is the early hook), the CLR needs to be UP BEFORE `Game::install` fires. Path (b) creates a chicken-and-egg: install fires → CLR comes up → plugins register their install callback → but install already fired and won't fire again.

Path (a) avoids this entirely: the launcher controls the timing. After `LoadLibraryA("UtinniCore.dll")` returns (which now does only `DisableThreadLibraryCalls + return TRUE` — completes in microseconds), the launcher does `CreateRemoteThread` to `utinni_init`. That thread brings up the CLR, loads `UtinniCoreDotNet.dll`, and the editor wires up its callbacks against the existing `swg::*` detour table — all BEFORE SWG's main thread reaches `Game::install`. (Recall the launcher's `loadDll` in `main.cpp:298-368` parks the SWG main thread at OEP via `EB FE`, injects, restores OEP, then `ResumeThread` — `utinni_init` fires DURING the parked-at-OEP window.)

### Path-(a) detailed steps

1. **`UtinniCore/utinni.cpp` changes:**
   - Add `extern "C" __declspec(dllexport) DWORD WINAPI utinni_init(LPVOID)` containing the current body of `main()` (lines 100-130).
   - Delete the orphaned `void main()` declaration (now unused).
   - `DllMain` body becomes:
     ```cpp
     BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
     {
         switch (fdwReason)
         {
         case DLL_PROCESS_ATTACH:
             DisableThreadLibraryCalls(hinstDLL);
             return TRUE;
         case DLL_PROCESS_DETACH:
             detatch();
             return TRUE;
         }
         return TRUE;
     }
     ```

2. **`Launcher/main.cpp::inject()` changes:** After the existing `WaitForSingleObject(hThread, INFINITE)` at line 195 returns, before the `GetExitCodeThread` at line 200-209, the `hDll` holds the remote `LoadLibraryA` return value (the remote `HMODULE` for `UtinniCore.dll`). Compute the remote address of `utinni_init`:
   ```cpp
   // After the existing LoadLibraryA CreateRemoteThread returns and we have hDll (the remote HMODULE):
   HMODULE localCore = LoadLibraryA(dllFilename.c_str());   // load locally to resolve GetProcAddress
   FARPROC localInit = GetProcAddress(localCore, "utinni_init");
   if (!localInit) throwError("[ERROR] utinni_init export not found in UtinniCore.dll");
   SIZE_T initOffset = (BYTE*)localInit - (BYTE*)localCore;
   FreeLibrary(localCore);

   FARPROC remoteInit = (FARPROC)((BYTE*)hDll + initOffset);
   HANDLE hInitThread = CreateRemoteThread(procInfo.hProcess, nullptr, 0,
                                            (LPTHREAD_START_ROUTINE)remoteInit, nullptr, 0, nullptr);
   if (!hInitThread) throwError("[ERROR] Couldn't open utinni_init remote thread.");
   WaitForSingleObject(hInitThread, INFINITE);
   CloseHandle(hInitThread);
   ```
   This is the textbook deferred-init pattern. The launcher-side `LoadLibrary` is on the LOCAL process and just used to resolve the export offset — it does not interfere with the remote process.

3. **`UtinniCore/UtinniCore.vcxproj` changes:** Add a `.def` file or use `__declspec(dllexport)` (already in the function signature above) to ensure `utinni_init` is exported by name. Verify with `dumpbin /exports bin\Release\UtinniCore.dll | findstr utinni_init`. **Preservation check (CON-N-01):** `utinni_init` is NOT a detour; it's a plain export. CON-N-01 (detour-table pattern) is unaffected.

### Open question — defer for Plan 02-03 task ordering

Should `utinni_init` ALSO start a fresh thread internally (like `main` does today via `CreateThread`), or should the launcher's `CreateRemoteThread` BE the worker thread? The launcher's `WaitForSingleObject(hInitThread, INFINITE)` blocks until `utinni_init` returns; if `utinni_init` runs all the way through CLR bring-up + plugin load synchronously, the launcher waits the full bring-up time. **Recommendation:** `utinni_init` runs synchronously (no internal `CreateThread`); the launcher waits. Reason: the launcher is single-purpose, the wait is bounded, and synchronous startup is far easier to debug than fire-and-forget. **Alternate:** `utinni_init` `CreateThread`s and returns immediately — launcher detects success via `GetExitCodeThread` returning a non-zero `HMODULE`-equivalent. Less debuggable but matches today's startup style.

## C-09 signaller design

Already detailed in §Per-bug fix shape C-09 above. Summary:

**Signaller lives in:** `UtinniCore/swg/graphics/directx9.cpp` (where `hkPresent` observes the block-flag).

**Who signals it:** `hkPresent` in the native game thread, when it observes `blockPresentCall == true` and sets `isPresenting = false`.

**Who waits:** `FormMain.WndProc` in the managed UI thread, via `EventWaitHandle.WaitOne(TimeSpan.FromMilliseconds(100))`.

**Primitive:** `CreateEvent`/`SetEvent`/`ResetEvent` (Win32 kernel event) on the native side; `EventWaitHandle` with `SafeWaitHandle` wrapper on the managed side. Both are in net472 BCL (`System.Threading.EventWaitHandle`, `Microsoft.Win32.SafeHandles.SafeWaitHandle`).

**No new NuGet packages.** `Microsoft.VisualStudio.Threading` is NOT needed.

**Timeout policy:** 100 ms per CONTEXT.md D-05. If the game thread never signals (it's hung for any reason), the UI thread proceeds anyway — minimize/restore is best-effort, not a correctness gate. This eliminates the deadlock failure mode (assessment.md C-09 acceptance: "no observable busy-wait UI freeze").

## C-07 disposition

Detailed in §Per-bug fix shape C-07 above. Summary:

**`AllowMerge` disposition: KEEP it in `IUndoCommand` and call it before `Merge`.**

Evidence:
- `docs/ai/undo-redo.html:54-55, 184-185` — documented contract states `AllowMerge` gates `Merge`.
- `docs/ai/undo-redo.html:146-167` — `SetTimeOfDayCommand` example uses `AllowMerge() => true` for cheap merging of slider-drag commands.
- `WorldSnapshotCommands.cs:70-72, 113-115, 170-178, 233-241` — all four in-tree implementations return `false` for both, but that's "this command class doesn't merge" not "the merge contract is dead".
- Removing `AllowMerge` from `IUndoCommand` would be a binary-breaking interface change for plugin authors targeting Wave-1 (R-B plugin lifecycle work in Phase 3 already plans symmetric ABI changes — adding ANOTHER would amplify the migration cost).

Disposition: **keep `AllowMerge`; call `Peek().AllowMerge()` then `Peek().Merge(args.UndoCommand)` in `AddUndoCommand`**. See code shape above.

**`RedoCommands.Clear` ordering: move AFTER the merge check.** Current order destroys redo state for merged-away commands. Fix per code shape above.

**Thread-safety wrapper: `private readonly object syncRoot = new object();` + `lock(syncRoot)` around every stack mutation.** Released around `cmd.Undo()` / `cmd.Execute()` / `onUpdateCommandsCallback()` calls to avoid re-entrancy deadlock through `GroundSceneCallbacks.AddUpdateLoopCall`. See code shape above.

## C-15 CI impact assessment

**Current state:** Phase 1's CI invokes the CON-T-01 post-build chain successfully (Summary `01-02-SUMMARY.md` confirms `UtinniCoreDotNetGen.exe` ran). The CI runner's working directory for `UtinniCoreDotNetGen.exe` is `D:\a\Utinni\Utinni\UtinniCoreDotNetGen\bin\Release\` (or similar — windows-2022 GitHub Actions standard runner path). That path DOES contain `\bin\` (lowercase, between `Release` and the project name). The `LastIndexOf("\\bin\\")` lookup succeeds.

**Therefore:** C-15 is NOT currently affecting CI. The brittleness is real but latent. Disposition per CONTEXT.md "Claude's Discretion": **C-15 follows normal assessment-week order within Plan 02-02** (not first). The planner can place it where it fits the within-plan dependency graph; assessment.md's recommended sequencing puts it in Week 2.

**Future-proofing rationale:** Any contributor who customizes `BaseOutputPath`, runs `dotnet build` from a non-default location, or hooks the post-build chain into a CI runner whose output paths don't include `\bin\` (e.g. self-hosted runners, Docker-based builds) will hit the bug. The fix is cheap and the future-proofing is worth doing in this phase rather than waiting for a CI-specific failure to force it.

## C-08 Phase-1 handoff

**Current state of `UtinniCoreDotNet.Tests/HotkeyTests.cs` per Phase 1:**
- 2 passing tests: `Ctor_StringConstructor_SingleKey_SetsKeyAndNoModifier`, `Ctor_StringConstructor_ModifierChord_SetsBoth`.
- 2 skipped tests with `C-08:` prefix:
  - `Ctor_StringConstructor_MultiModifierChord_ParsesFlags` (Theory with `[InlineData("Shift + Alt + Z", Keys.Shift, Keys.Alt | Keys.Z)]`) — Phase-1 scope-expansion commit `b4d2137`.
  - `Ctor_StringConstructor_MalformedInput_DoesNotThrow` — original Phase-1 test from commit `2e79097`.

**C-08 fix closes both skipped tests, with one caveat:**
- `Ctor_StringConstructor_MultiModifierChord_ParsesFlags` data needs updating per §Per-bug fix shape C-08 above: the right contract for `"Shift + Alt + Z"` is `ModifierKeys = Shift | Alt, Key = Z` (not the current `Keys.Shift, Keys.Alt | Keys.Z`). The Phase 1 test data was an educated guess at the contract; the C-08 fix nails the actual contract (modifiers ORed, last segment is the single key). Plan 02-01's C-08 task must (a) remove the `[Skip = "C-08:..."]` markers, (b) update the multi-modifier `InlineData` to `(Keys.Shift | Keys.Alt, Keys.Z)`, (c) verify all 4 tests pass.

**Plan 02-01 C-08 task structure:**
1. Implement C-08 fix in `Hotkey.cs` (replace `Enum.Parse` with `TryParse` + split-on-`+`-iterate logic).
2. Update `HotkeyTests.cs`:
   - Remove `Skip = "C-08:..."]` from both skipped tests.
   - Update `MultiModifierChord_ParsesFlags` `InlineData` to `("Shift + Alt + Z", Keys.Shift | Keys.Alt, Keys.Z)`.
   - Add new `MalformedModifier_DoesNotThrow_DisablesHotkey` test.
3. Run `dotnet test`; expect 4 passing + 1 new (5 total).
4. Single atomic commit with both fix and test updates.

## C-16 delegate-pinning audit

Detailed in §Per-bug fix shape C-16 above. Summary:

**`GCHandle.Alloc(handler, GCHandleType.Normal)` is the textbook primitive**, but the existing `private static readonly UtinniCore.Delegates.Action_ xxxAction;` field approach in `GameCallbacks` / `GroundSceneCallbacks` / `ObjectCallbacks` is ALREADY a valid GC root and is in fact what's preventing the original crash. The "Very odd bug" comment dates from before the fix was understood; the field pattern WAS the fix.

**Audit verdict:** No callback-passing site in the three files is unanchored. The "fix" for C-16 / CON-O-03 is:
1. Replace the misleading comment.
2. Add a regression test that forces `GC.Collect()` and asserts no AV.
3. Optionally add explicit `GCHandle.Alloc` as belt-and-suspenders (recommendation: NO — increases boilerplate without proportional safety gain).

**Per-file fix list (already shown in §Per-bug fix shape):**
- `GameCallbacks.cs:46` — comment update.
- `ObjectCallbacks.cs:39` — comment update.
- `GroundSceneCallbacks.cs:38-41` — optional new comment for consistency.

**Pinned vs Normal:** `GCHandleType.Normal` is the right type if `GCHandle.Alloc` is added — it pins the OBJECT against collection but does NOT pin its MEMORY against compaction (which is what `GCHandleType.Pinned` does — Pinned is for buffers you pass as pointers to native, not delegates). For delegates passed as function pointers, the CLR's P/Invoke marshalling already takes care of generating a non-moving stub; `GCHandleType.Normal` just keeps the wrapping `Delegate` instance alive. **If GCHandle is used, use `Normal`, not `Pinned`.**

**Static field vs GCHandle:** field is implicit (uses GC's normal reachability rules); GCHandle is explicit (separate ROOT-set entry). Both prevent collection. The field is idiomatic C# and harder to accidentally remove during refactor (it's used as a delegate, which has obvious purpose); a GCHandle without a corresponding "what is this Alloc for" comment is opaque. Recommendation: stay with fields.

## C-12 VSIX widening

Detailed in §Per-bug fix shape C-12 above. Summary:

**Manifest changes:** Four `[16.0,17.0)` → `[16.0,18.0)` ranges in `source.extension.vsixmanifest`.

**Vsix.csproj changes:** Two `<PackageReference>` version bumps (`Microsoft.VisualStudio.SDK 16.0.206` → `17.x`; `Microsoft.VSSDK.BuildTools 16.8.3038` → `17.x`). Exact versions to verify at planning time via NuGet.

**Other manifest changes needed:** None. `MinimumVisualStudioVersion` stays at `16.0` (it's a minimum; widening the maximum doesn't change the minimum).

**Prerequisites block:** Line 17 has `Microsoft.VisualStudio.Component.CoreEditor` with `[16.0,17.0)`; same widening to `[16.0,18.0)`.

**Anything else?** No `MinimumVSVersion` element in the manifest beyond the InstallationTarget version ranges. No platform-architecture restrictions to update (the VSIX is content-only — project templates).

## CON-O-01 / -02 / -04 archaeology findings

### CON-O-01 (`isSafeToUse` `||` vs `&&`)

**Bounded archaeology:**
- Git log on this fork (master): no commits touch `game.cpp:305-308` since fork creation. The discrepancy is upstream.
- Upstream `ptklatt/Utinni` master: same code (`||`); same internals.md (`&&`). The discrepancy was inherited.
- In-tree comments at `game.cpp:305-308`: none. The function is bare. No `// ToDo`, `// XXX`, `// HACK` markers.
- IDA pseudo-paste markers: none in `game.cpp`. The function appears to have been hand-written from an internals doc, not pasted from IDA.

**Disposition (default-fallback per D-12):** `docs/ai/internals.md:231` is the source of truth. The operator is `&&`. Fix `game.cpp:307` (`||` → `&&`) as part of the KB-05 commit per D-11. Update `docs/ai/assessment.md` §Open Questions §1 to:
> **1. `isSafeToUse`** — Resolved 2026-Q2: docs/ai/internals.md:231 documents "Both must be true" (AND); the code's `||` was a typo or inadvertent bit-flip. Fixed in commit `<SHA>` along with the KB-05 disposition.

### CON-O-02 (was `AddPostDrawLoopCall` ever used?)

**Bounded archaeology:**
- Git log on this fork: no commits touch `GroundSceneCallbacks.cs:97-106`. The bug is original code.
- Upstream `ptklatt/Utinni`: same broken code (verified via head-of-master comparison).
- In-tree usages of `AddPostDrawLoopCall`: grep finds zero callers in `D:\Code\Utinni` and zero in `D:\Code\UtinniPlugins`. The method is defined but never invoked.
- However: `Add*Call` is a public surface — Wave-1 plugins (Phase 7+) may want to use it. Removing or no-fixing it would leak a broken API into V1.

**Disposition (default-fallback per D-12):** Assume it IS used (treat the fix as load-bearing). Land the queue-drain fix per §Per-bug fix shape C-04 above. Document in `docs/ai/assessment.md` §Open Questions §2:
> **2. Was `AddPostDrawLoopCall` ever actually used?** — Resolved 2026-Q2: grep finds zero callers in Utinni and UtinniPlugins as of the fork's current state. However, the method is a public API surface for plugin authors (Wave-1 plugins may use it), so the fix-it-properly disposition was taken. The `Drain(ConcurrentQueue<Action>)` helper introduced in commit `<SHA>` ensures this class of bug can't recur.

### CON-O-04 (VS 2019 pin rationale)

**Bounded archaeology:**
- Git log on this fork: no commits explain the pin. `Vsix.csproj` and `source.extension.vsixmanifest` were inherited from upstream.
- Upstream `ptklatt/Utinni`: same `[16.0,17.0)` pin; no commit message explains it. The pin dates to before VS 2022 existed (VS 2022 released November 2021; the upstream Vsix was authored before then).
- In-tree comments: none.
- IDA-style markers: not applicable (this is .NET tooling, not RE'd binary).

**Disposition (default-fallback per D-12):** The pin is historical, not technical. VS 2022 supports the same C# 7.3 / net472 surface VS 2019 does; `Microsoft.VisualStudio.SDK 17.x` exists and is the canonical VS 2022 SDK target. Audit task: planner adds a `checkpoint:human-verify` step — maintainer builds the VSIX against the bumped SDK in BOTH VS 2019 and VS 2022, installs into both IDEs, opens both project templates, confirms the template-creation wizard runs without error. If the audit is clean, widen as planned. Document in `docs/ai/assessment.md` §Open Questions §4:
> **4. VS 2019 pin rationale** — Resolved 2026-Q2: No technical rationale found in git log, in-tree comments, or upstream history. The pin dates to before VS 2022's release (Nov 2021) and was never updated. Phase 2 audited the VS 2022 build (commit `<SHA>`): `Microsoft.VisualStudio.SDK 17.x` builds cleanly, both project templates install and run in VS 2022. Widened to `[16.0,18.0)`. If a regression is discovered in a specific VS 2022 path, narrow the range at that point.

## C-01 timing harness — sibling project sketch

**Final project name recommendation:** `Utinni.LoaderLockHarness` (per CONTEXT.md working name; clear single-purpose; matches the flat-root sibling-project convention from Phase 1 CONTEXT.md D-01).

**Project shape:**
- Type: native C++ console exe (NOT a managed test). Reason: the harness `LoadLibrary`s `UtinniCore.dll` in process-isolation; a managed harness adds CLR startup overhead that conflates with the loader-lock timing measurement we're trying to make.
- Filename: `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj` at repo root.
- Configuration: `Release|x86` (matches `UtinniCore.dll`), `Debug|x86`. Skip `RelWithDbgInfo|x86` (no value for the harness).
- PlatformToolset: `v142` (same as UtinniCore).
- LanguageStandard: `stdcpp17`.
- Subsystem: Console.

**Project dependency:** `Utinni.LoaderLockHarness` depends on `UtinniCore` so the CON-T-01 post-build chain runs first and `UtinniCore.dll` is fresh in `bin/$(Configuration)/`.

**`main.cpp` (~50 lines):**
```cpp
#include <Windows.h>
#include <iostream>

int main(int argc, char* argv[])
{
    LARGE_INTEGER freq, t0, t1;
    QueryPerformanceFrequency(&freq);

    // Threshold from arg[1] if provided, else 50 ms.
    double thresholdMs = (argc >= 2) ? atof(argv[1]) : 50.0;

    QueryPerformanceCounter(&t0);
    HMODULE h = LoadLibraryA("UtinniCore.dll");
    QueryPerformanceCounter(&t1);

    if (!h)
    {
        std::cerr << "LoadLibrary failed: error " << GetLastError() << std::endl;
        return 2;
    }

    double elapsedMs = (double)(t1.QuadPart - t0.QuadPart) * 1000.0 / freq.QuadPart;
    std::cout << "LoadLibrary(UtinniCore.dll) elapsed: " << elapsedMs
              << " ms (threshold: " << thresholdMs << " ms)" << std::endl;

    FreeLibrary(h);

    return (elapsedMs < thresholdMs) ? 0 : 1;
}
```

**Why this works:** `LoadLibrary` blocks until `DllMain(DLL_PROCESS_ATTACH)` returns. So timing `LoadLibrary` directly measures the time spent inside `DllMain` plus DLL load-tab plumbing. Post-fix, `DllMain` does only `DisableThreadLibraryCalls + return TRUE` — should be <1 ms. Pre-fix, `DllMain` `CreateThread`s `main()` which calls `loadPlugins()` + `clr::load()`; the `CreateThread` itself returns instantly so `DllMain` returns instantly TOO — meaning **this harness does NOT catch the original C-01 bug shape directly**. What it catches is the regression class "DllMain grew heavy work inline" — which is the long-term safety net for STAB-04 preservation of the C-01 fix.

**Caveat:** The harness can't catch the actual loader-lock deadlock without a contended-LoadLibrary scenario. Full proof remains in the Tier-4 manual residual (CONTEXT.md D-06: "C-01 full proof of no deadlock under loader-lock contention" stays manual). The harness IS valuable as a regression guard against "someone reverts C-01 by moving plugin-load back into DllMain inline".

**xUnit caller:** A test in `UtinniCoreDotNet.Tests` does `Process.Start("Utinni.LoaderLockHarness.exe")`, captures stdout, parses exit code:
```csharp
[Fact]
public void LoaderLockHarness_LoadsUtinniCoreUnderThreshold()
{
    var harnessPath = Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "Utinni.LoaderLockHarness", "bin", "Release", "Utinni.LoaderLockHarness.exe");
    Assert.True(File.Exists(harnessPath), $"Harness not found at {harnessPath}");

    var psi = new ProcessStartInfo(harnessPath, "50")  // 50 ms threshold
    {
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    var proc = Process.Start(psi);
    proc.WaitForExit(5000);
    Assert.Equal(0, proc.ExitCode);
    Assert.Contains("elapsed:", proc.StandardOutput.ReadToEnd());
}
```

**Discoverability from the test project:** harness exe must be on the path the test resolves. Easiest: rely on the relative path above (since both projects build to `bin/Release/<TargetFramework or native>/`). Cleaner alternative: add a `<Content Include="..\Utinni.LoaderLockHarness\bin\$(Configuration)\Utinni.LoaderLockHarness.exe" CopyToOutputDirectory="PreserveNewest" Link="Utinni.LoaderLockHarness.exe" />` to the test csproj — copies the harness next to the test DLLs. Recommendation: relative path (no csproj coupling) for simplicity; bump to content-copy if it proves flaky.

## Landmines / preservation surfaces

| # | Landmine | Affected bug | Mitigation in research | Verification |
|---|----------|--------------|-----------------------|--------------|
| 1 | **CON-M-05 (`UndoRedoManager.OnCleanupCallback`)** — clears both stacks on scene cleanup; must survive C-07 refactor | C-07 | Fix shape preserves the method body verbatim; only wraps it in `lock(syncRoot)`. The Phase-1 D-06 testability seam injects the registration step (not the body) | xUnit test calls `OnCleanupCallback` via the injected `Action<Action>` seam and asserts both stacks are cleared. |
| 2 | **CON-N-04 (`utility/memory::copy` VirtualProtect bracket)** — must survive C-11 fix | C-11 | C-11 fix is in `directx9.cpp::getVtbl()` which uses `memory::findPattern` (pure read, no `VirtualProtect`). The fix adds null checks; it does NOT touch `memory::copy` | Code review: assert `memory.cpp:63-72` (`copy()`) is unchanged in the C-11 diff. |
| 3 | **CON-N-01 (detour-table pattern)** — any new detour added for C-01 must follow `using pX = ...; pX x = (pX)0xRVA; Detour::Create(...)` | C-01 path (b) only | Recommended path (a) does NOT add a new detour. `utinni_init` is a plain exported function, not a detour. CON-N-01 is not engaged. | N/A if path (a). |
| 4 | **CON-T-01 (`UtinniCore.vcxproj` post-build chain)** — `xcopy data/` + `UtinniCoreDotNetGen.exe`. C-14 (data/utinni.cfg) and C-15 (CppSharp slnDir) sit on this chain | C-14, C-15 | C-14 changes only file content, not the chain. C-15 fix preserves the chain but adds `"$(SolutionDir)"` as a new arg to `UtinniCoreDotNetGen.exe` — backwards-compatible (args[0]-or-walkup-or-env-var) | CI green after the chain fires; Phase 1 evidence confirms the chain works. |
| 5 | **CON-B-04 (cross-CRT discipline)** — C-02 fix must NOT introduce a different cross-CRT free | C-02 | Recommended disposition is to NOT free at all (TreeFile dtor owns it). No new `delete` of any kind. | Manual verification under live SWG injection (Tier 4). |
| 6 | **Phase-1 deferred work (UndoRedoManager `Action<Action>` testability seam)** — lands as part of C-07 fix, NOT as a separate task | C-07 | Single commit per CONTEXT.md "Claude's discretion" — the seam IS the test enablement; splitting is overhead. | The C-07 test suite cannot exist without the seam; CI green proves both. |
| 7 | **C-15 must not break the post-build invocation when args[0] is not passed** — backwards compat with the existing `UtinniCore.vcxproj:95` post-build until the vcxproj is updated in the SAME commit | C-15 | Fix shape's `ResolveSlnDir` falls back to walk-up + env-var when args are empty; existing invocation works AND the new invocation works. Update vcxproj in same commit for explicit pass-in. | xUnit's "no args + walkup" test covers backwards-compat. |
| 8 | **Cross-plan re-ordering risk:** Plan 02-01 (trivial) → 02-02 (single-file) → 02-03 (C-01) → 02-04 (C-09). The 4-plan order protects against architectural blockers. Are there cross-plan dependencies? | All | C-08 fix touches `Hotkey.cs` (Plan 02-01) and depends on the Phase-1 `UtinniCoreDotNet.Tests` infrastructure (already in place). C-07 fix lands in Plan 02-02 and requires the testability seam from Phase 1 D-06 (deferred Phase 1 work that lands HERE). C-01 (Plan 02-03) is fully self-contained (utinni.cpp + Launcher/main.cpp + UtinniCore.LoaderLockHarness — no managed-side changes). C-09 (Plan 02-04) touches FormMain.cs + directx9.cpp — independent of all prior. **No cross-plan blockers identified.** | Plan generation phase: verify each task's dependency list resolves within its plan or to a prior plan. |
| 9 | **`isPresenting` flag semantics ambiguity in C-09 design** — current native code sets `isPresenting = false` when blocked, `isPresenting = true` when actually presenting. The "blocked" condition we want to signal is `isPresenting == false AND blockPresentCall == true`. The `getPresentBlockedEvent`-based design above signals on observation in `hkPresent` — verify in implementation that the SetEvent ordering matches the existing flag protocol. | C-09 | Carefully timed `SetEvent` call in `hkPresent` AFTER `isPresenting = false` is set, BEFORE `present()` would have been called (which it isn't, since blocked). `ResetEvent` in `blockPresent(false)`. | xUnit mock-signaller test plus manual verification on minimize/restore. |

## Test project layout

**Single test project `UtinniCoreDotNet.Tests` absorbs everything per CONTEXT.md D-07.**

Phase 1 established conventions (verified in `01-PATTERNS.md`):
- SDK-style csproj, `net472`, `x86`, `<AppendPlatformToOutputPath>false</AppendPlatformToOutputPath>`.
- `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` + committed `packages.lock.json`.
- `System.Windows.Forms` reference (added Phase 1 commit `3b001bb` for HotkeyTests).
- `<Reference Include="UtinniCoreDotNet" />` via `<ProjectReference>` (Phase 1).
- 23-line MIT header on every `.cs` file (verbatim from `UtinniCoreDotNet/Hotkeys/Hotkey.cs:1-23`).
- Test naming: `[Method]_[Scenario]_[ExpectedOutcome]` (D-04).
- Class file naming: `<TypeName>Tests.cs` (e.g. `HotkeyTests.cs`, new this phase: `GroundSceneCallbacksTests.cs`, `UndoRedoManagerTests.cs`, `PluginLoaderTests.cs`, `GameDragDropEventHandlersTests.cs`, `GameCallbacksTests.cs`, `UtinniCfgTests.cs`, `CppSharpSlnDirTests.cs`, `VsixManifestTests.cs`, `Clr10HarnessTests.cs`, `FindPatternHarnessTests.cs`, `LoaderLockHarnessTests.cs`).

**Fixtures directory:** Per CONTEXT.md D-07, `/Fixtures` subdir for synthetic broken plugins and `utinni.cfg` samples. Recommended structure:
```
UtinniCoreDotNet.Tests/
├── UtinniCoreDotNet.Tests.csproj
├── packages.lock.json
├── HotkeyTests.cs                            (existing)
├── GroundSceneCallbacksTests.cs              (new, C-04)
├── GameCallbacksTests.cs                     (new, C-16)
├── GameDragDropEventHandlersTests.cs         (new, C-05)
├── PluginLoaderTests.cs                      (new, C-06)
├── UndoRedoManagerTests.cs                   (new, C-07)
├── Clr10HarnessTests.cs                      (new, C-10 — P/Invoke)
├── FindPatternHarnessTests.cs                (new, C-11 — P/Invoke)
├── UtinniCfgTests.cs                         (new, C-14)
├── CppSharpSlnDirTests.cs                    (new, C-15)
├── VsixManifestTests.cs                      (new, C-12)
├── LoaderLockHarnessTests.cs                 (new, C-01 — Process.Start)
└── Fixtures/
    ├── BrokenPlugin/                          (PluginLoader C-06 fixture)
    │   └── BrokenPlugin.csproj                (a tiny class library whose [Export(typeof(IPlugin))] ctor throws)
    │   └── BrokenPlugin.cs
    ├── GoodPlugin/                            (PluginLoader C-06 fixture)
    │   └── GoodPlugin.csproj
    │   └── GoodPlugin.cs
    └── utinni.cfg.snapshot                    (C-14 reference — see disposition below)
```

**Fixture project arrangement:** `BrokenPlugin` and `GoodPlugin` are tiny sibling csprojs at `UtinniCoreDotNet.Tests/Fixtures/BrokenPlugin/BrokenPlugin.csproj`. They reference `UtinniCoreDotNet` for `IPlugin`. They're built by Visual Studio when the solution is built (added to `Utinni.sln`). Their `<OutputPath>` is set so the test project can discover them at a known relative path. Alternative: embedded resources — but a real DLL on disk is a closer fixture to the production load path (which is `LoadLibrary` against a `.dll` file).

**`utinni.cfg` fixture:** Per §Per-bug fix shape C-14 — the test reads from `data/utinni.cfg` at the repo root, not from a fixture. The relative-path walk-up (`../../../../data/utinni.cfg` from `bin/Release/net472`) is the simplest robust option. **No fixture file needed for C-14.** (A snapshot fixture would defeat the purpose: the assertion is on the LIVE shipped file.)

**InternalsVisibleTo:** Tests that need to invoke `private` methods on `UtinniCoreDotNet` (e.g., the `Drain` helper in `GroundSceneCallbacks`, the `WaitForPresentBlock` helper in `FormMain`) need `[assembly: InternalsVisibleTo("UtinniCoreDotNet.Tests")]` added to `UtinniCoreDotNet/Properties/AssemblyInfo.cs`. Phase-1 didn't introduce this — it's new this phase. Alternative: reflection-based private-method invocation in tests; uglier but doesn't touch production. Recommendation: `InternalsVisibleTo` — it's standard, clean, and limited to this single test assembly.

**MIT header reminder:** Every NEW `.cs` test file gets the 23-line MIT header verbatim from `Hotkey.cs:1-23`. Every NEW `.cs` fixture file (in `Fixtures/*Plugin/*.cs`) gets it too. Fixture `.csproj` files do NOT get the header (CSPROJ convention is no header — verified in `01-PATTERNS.md`).

## Validation Architecture

> Generated per CONTEXT.md D-04..D-07 max-harness posture. This section populates `02-VALIDATION.md` after planning.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (pinned, Phase 1) |
| Config file | `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` (no `.runsettings`) |
| Quick run command | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` (project-targeted, per Phase-1 RESEARCH §Pitfall 2) |
| Full suite command | `msbuild Utinni.sln /m /restore /p:Configuration=Release /p:Platform=x86 /p:RestorePackagesConfig=true && dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| STAB-01-C04 | `DequeuePostDrawLoopCalls` drains the right queue | unit (managed xUnit) | `dotnet test --filter FullyQualifiedName~GroundSceneCallbacksTests` | ❌ Wave 0 |
| STAB-01-C05 | Plugin drag-drop fires on `PanelGame` | unit (managed xUnit + WinForms fixture) | `dotnet test --filter FullyQualifiedName~GameDragDropEventHandlersTests` | ❌ Wave 0 |
| STAB-01-C06 | Broken plugin doesn't tear down editor; surviving plugins load; log surfaces broken name | unit (managed xUnit + Fixtures/BrokenPlugin) | `dotnet test --filter FullyQualifiedName~PluginLoaderTests` | ❌ Wave 0 (test + 2 fixture csprojs) |
| STAB-01-C07 | `UndoRedoManager` thread-safety, `AllowMerge` gate, redo-Clear ordering | unit (managed xUnit) | `dotnet test --filter FullyQualifiedName~UndoRedoManagerTests` | ❌ Wave 0 |
| STAB-01-C08 | `Hotkey.ProcessString` doesn't throw on malformed input; multi-modifier parses | unit (managed xUnit — extends `HotkeyTests`) | `dotnet test --filter FullyQualifiedName~HotkeyTests` | ✓ (Phase 1; both Skipped tests unskip + new MalformedModifier test) |
| STAB-01-C10 | `clr::stop()` is idempotent (no AV on second call) | integration (xUnit + P/Invoke into UtinniCore.dll) | `dotnet test --filter FullyQualifiedName~Clr10HarnessTests` | ❌ Wave 0 (test + native export) |
| STAB-01-C11 | `findPattern` returns 0 on absent pattern; `getVtbl()` bails | integration (xUnit + P/Invoke) | `dotnet test --filter FullyQualifiedName~FindPatternHarnessTests` | ❌ Wave 0 (test + native export) |
| STAB-01-C12 | VSIX manifest version ranges are `[16.0,18.0)` | unit (XML assert) | `dotnet test --filter FullyQualifiedName~VsixManifestTests` | ❌ Wave 0 |
| STAB-01-C14 | `data/utinni.cfg` ships with blank login keys | unit (file-content assert) | `dotnet test --filter FullyQualifiedName~UtinniCfgTests` | ❌ Wave 0 |
| STAB-01-C15 | `ResolveSlnDir` pure function with three resolution modes | unit (managed xUnit) | `dotnet test --filter FullyQualifiedName~CppSharpSlnDirTests` | ❌ Wave 0 |
| STAB-01-C16 | Delegate survives `GC.Collect` between registration and dispatch | integration (xUnit + P/Invoke + GC.Collect) | `dotnet test --filter FullyQualifiedName~GameCallbacksTests` | ❌ Wave 0 (test + native test-only export) |
| STAB-01-C01 | DllMain returns in < 50 ms; no heavy startup | partial-proof (xUnit + Process.Start helper-exe) | `dotnet test --filter FullyQualifiedName~LoaderLockHarnessTests` | ❌ Wave 0 (test + Utinni.LoaderLockHarness project) |
| STAB-01-C09 | `WaitForPresentBlock` returns within timeout even when no signal | unit (managed xUnit + mock signaller) | `dotnet test --filter FullyQualifiedName~FormMainSignallerTests` | ❌ Wave 0 |
| STAB-01-C02 | Cross-CRT free no longer attempted | partial-proof (xUnit calls test-only free wrapper) | `dotnet test --filter FullyQualifiedName~ConfigBufferFreeTests` | ❌ Wave 0 — partial proof, full = Tier 4 |
| STAB-01-C03 | `Network::cast` post-condition wrapper returns non-uninit value | partial-proof (xUnit calls wrapper with synthetic id) | `dotnet test --filter FullyQualifiedName~NetworkCastTests` | ❌ Wave 0 — partial proof, full = Tier 4 |
| STAB-01-C13 | TJT Debug build outputs to `bin/Debug/` not `D:/bin/` | manual (UtinniPlugins repo has no CI) | `<manual: cd UtinniPlugins; msbuild "The Jawa Toolbox\TheJawaToolbox.sln" /p:Configuration=Debug /p:Platform=x86; ls Utinni/bin/Debug/Plugins/TheJawaToolbox>` | N/A (cross-repo manual) |
| STAB-01-KB05 | `isSafeToUse` uses `&&` (manual code review + commit-time grep) | manual + commit-time assertion | grep `\|\|` in `swg/game/game.cpp:307` returns nothing | N/A (code-review) |

### Sampling Rate
- **Per task commit:** Project-targeted `dotnet test --no-build` (~10 seconds locally for the current suite size).
- **Per wave merge:** Full `msbuild + dotnet test` chain (~6 minutes per Phase 1 CI evidence).
- **Phase gate:** Full suite green on CI on `master` before `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `UtinniCoreDotNet.Tests/GroundSceneCallbacksTests.cs` — covers STAB-01-C04
- [ ] `UtinniCoreDotNet.Tests/GameCallbacksTests.cs` — covers STAB-01-C16
- [ ] `UtinniCoreDotNet.Tests/GameDragDropEventHandlersTests.cs` — covers STAB-01-C05
- [ ] `UtinniCoreDotNet.Tests/PluginLoaderTests.cs` — covers STAB-01-C06
- [ ] `UtinniCoreDotNet.Tests/UndoRedoManagerTests.cs` — covers STAB-01-C07
- [ ] `UtinniCoreDotNet.Tests/Clr10HarnessTests.cs` — covers STAB-01-C10
- [ ] `UtinniCoreDotNet.Tests/FindPatternHarnessTests.cs` — covers STAB-01-C11
- [ ] `UtinniCoreDotNet.Tests/VsixManifestTests.cs` — covers STAB-01-C12
- [ ] `UtinniCoreDotNet.Tests/UtinniCfgTests.cs` — covers STAB-01-C14
- [ ] `UtinniCoreDotNet.Tests/CppSharpSlnDirTests.cs` — covers STAB-01-C15
- [ ] `UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs` — covers STAB-01-C01
- [ ] `UtinniCoreDotNet.Tests/FormMainSignallerTests.cs` — covers STAB-01-C09
- [ ] `UtinniCoreDotNet.Tests/ConfigBufferFreeTests.cs` — covers STAB-01-C02 (partial proof)
- [ ] `UtinniCoreDotNet.Tests/NetworkCastTests.cs` — covers STAB-01-C03 (partial proof)
- [ ] `UtinniCoreDotNet.Tests/Fixtures/BrokenPlugin/` — fixture project + DLL
- [ ] `UtinniCoreDotNet.Tests/Fixtures/GoodPlugin/` — fixture project + DLL
- [ ] `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj` — native helper exe
- [ ] `Utinni.LoaderLockHarness/main.cpp` — harness source
- [ ] New native test-only exports in `UtinniCore.dll`: `utinni_clr_stop`, `utinni_findPattern`, `utinni_getVtbl`, `Game::triggerInstallCallbacks`, `getPresentBlockedEvent` (the last is also production code for C-09)
- [ ] `[InternalsVisibleTo("UtinniCoreDotNet.Tests")]` added to `UtinniCoreDotNet/Properties/AssemblyInfo.cs`
- [ ] Two new fixture csproj entries in `Utinni.sln` plus `Utinni.LoaderLockHarness` entry

## Sources

### Primary (HIGH confidence)
- `D:\Code\Utinni\docs\ai\assessment.md` §"Critical issues" — C-01..C-15 specifications (the requirement source)
- `D:\Code\Utinni\docs\ai\internals.md:218-231` — `isSafeToUse` AND semantics (KB-05 / CON-O-01 source)
- `D:\Code\Utinni\docs\ai\undo-redo.html:54-55, 146-167, 184-185` — `AllowMerge` documented contract (C-07 disposition)
- `D:\Code\Utinni\UtinniCore\*.cpp`, `UtinniCoreDotNet\**\*.cs` — all bug surfaces verified by direct read
- `D:\Code\UtinniPlugins\The Jawa Toolbox\TheJawaToolbox\TheJawaToolbox.vcxproj:63` + `D:\Code\UtinniPlugins\The Jawa Toolbox\TheJawaToolbox.sln:26` — C-13 surface confirmed
- `D:\Code\Utinni\sdk\UtinniPluginTemplates\Vsix\source.extension.vsixmanifest` + `Vsix.csproj` — C-12 surface confirmed
- `D:\Code\Utinni\.planning\phases\01-ci-tier-1-c-scaffold\01-02-SUMMARY.md` — Phase-1 CI evidence (CI runner works; `\bin\` is in the runner path so C-15 isn't currently broken)
- `D:\Code\Utinni\.planning\codebase\CONCERNS.md` — TD-01..TD-15 cross-references each map to a C-NN

### Secondary (MEDIUM confidence)
- Microsoft documentation: `docs.microsoft.com/dotnet/standard/native-interop/best-practices#function-pointers` — `GCHandle.Alloc(GCHandleType.Normal)` pattern for C-16 [CITED — knowledge from training data; planner should re-verify URL is current at task time]
- Microsoft documentation: "DLL Best Practices" (`docs.microsoft.com/windows/win32/dlls/dynamic-link-library-best-practices`) — DllMain prohibitions on LoadLibrary, CoInitializeEx [CITED — well-documented since Windows XP era]
- NuGet: `Microsoft.VisualStudio.SDK` is published as 17.0.x for VS 2022 [ASSUMED — exact version pin needs verification at planning time]

### Tertiary (LOW confidence)
- The exact `Microsoft.VisualStudio.SDK` and `Microsoft.VSSDK.BuildTools` versions to bump to for VS 2022 compatibility — verify at task time via `dotnet add package <X> --version <Y>` and Vsix build

## Metadata

**Confidence breakdown:**
- Per-bug fix shapes (all 17 items): HIGH — every surface read directly; every fix shape compiles in head; the C-08 multi-modifier disposition is the only one with non-trivial implementation nuance and even that is bounded by Phase 1's already-skipped test pair.
- C-01 architectural recommendation: MEDIUM — path (a) is well-grounded in Microsoft loader-lock guidance and the standard DLL-injection idiom, but no live deadlock repro was attempted. Path (b) was rejected on the chicken-and-egg-with-`Game::install` argument; if the planner discovers that early `Game::install` callbacks DON'T actually need plugins, (b) is viable. The fallback contract is "if (a) proves infeasible during execution, planner re-opens with (b) as a defer-to-2.1 escape hatch — but per ROADMAP success criterion #4, defer-to-V2 is NOT available."
- CON-O-01/-02/-04 dispositions: MEDIUM — bounded archaeology found no historical answer; default-fallback contract applies. Each disposition is documented per D-12.
- Validation Architecture: HIGH — every test has a concrete file path, a runnable command, and an asserted behavior. Wave-0 gaps list is complete.

**Research date:** 2026-05-16
**Valid until:** 2026-06-15 (30 days for stable surfaces; refresh if any of the C-NN bugs is independently re-prioritized or if a CON-N-* preservation item is touched by an out-of-band change)
