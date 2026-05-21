---
phase: 03-strategic-reworks-r-a-r-h
plan: 02
subsystem: plugin-lifecycle-and-rvas
tags: [plugin-abi, destroyPlugin, two-phase-init, HMODULE-tracking, single-source-rva, pinvoke, xunit, fixture-plugin, CON-N-08, CON-O-07]
status: complete

# Dependency graph
requires:
  - phase: 03-strategic-reworks-r-a-r-h
    plan: 01
    provides: "Plan 03-01 R-A + R-H callback symmetry (closed 2026-05-21) -- this plan rides on the stable callback shape it left behind; destroyPlugin-aware plugins can Subscribe/Unsubscribe handles for clean teardown."
  - phase: 02-critical-bug-burn-down-c-01-c-15
    provides: "C-06 PluginLoader per-plugin try/catch isolation precedent (extended here from MEF compose-time to native createPlugin->init() init-time); test_exports.cpp utinni_test_* C-linkage export pattern; LoaderLockHarness sibling-vcxproj convention for adding native test artifacts at solution root."
  - phase: 01-ci-tier-1-c-scaffold
    provides: "xUnit 2.9.x + net472 x86 test infrastructure, [Method]_[Scenario]_[ExpectedOutcome] naming, CI gate on master."

provides:
  - "UTINNI_PLUGIN macro now declares BOTH createPlugin AND destroyPlugin -- symmetric ABI eliminates cross-CRT delete crash class (CON-B-04 structurally fixed for new-shape plugins)."
  - "PluginManager::loadPlugins runs two passes (createPlugin all -> init all) with per-plugin try/catch around init() per D-14; LoadLibrary failures logged with GetLastError per D-17; HMODULE tracked + FreeLibrary in ~PluginManager per D-16; legacy-fallback to virtual destructor per D-13 + D-15."
  - "Single-source-of-truth WndProc RVA: 0x00AA0970 declared once in UtinniCore/swg/client/client.cpp:43; surfaced via Client::getSwgWndProc() + extern \"C\" getSwgWndProcExport() shim; consumed by PanelGame.cs via cached Native.GetSwgWndProc() at ctor (D-18..D-20)."
  - "Two new fixture plugin projects added to Utinni.sln: Utinni.CrtMatchPlugin (/MD, exports BOTH createPlugin + destroyPlugin -- symmetric ABI path) and Utinni.LegacyPlugin (/MT, exports ONLY createPlugin -- exercises virtual-destructor fallback)."
  - "Four new utinni_test_* C-linkage exports in plugin_manager.cpp (pluginManagerLoadFromDir / LoadedCount / Dispose / lastLoadLibraryError) wiring the standalone test Impl to PluginManagerLifecycleTests via P/Invoke."
  - "Seven new xUnit Facts: PluginManagerLifecycleTests (5) + GetSwgWndProcTests (2). Total test count 76 -> 83."

affects: [phase-03-03-build-tooling-logging, plugins-tjt, plugins-sytner, code-review-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Symmetric plugin ABI: extern \"C\" __declspec(dllexport) utinni::UtinniPlugin* createPlugin() paired with extern \"C\" __declspec(dllexport) void destroyPlugin(utinni::UtinniPlugin*). UTINNI_PLUGIN macro declares both."
    - "Two-phase plugin init with per-plugin try/catch isolation (extends Phase 02 C-06 from PluginLoader compose-time to PluginManager init-time): pass 1 LoadLibrary + createPlugin all -> pass 2 init() each with try/catch(std::exception&)/catch(...) around each invocation."
    - "HMODULE tracking in PluginManager::Impl::LoadedPlugin{HMODULE, UtinniPlugin*, destroyFn}: shutdown order destroyPlugin (plugin CRT) then FreeLibrary (host CRT); legacy fallback delete plugin via virtual destructor."
    - "LoadLibrary failure path: log::error with full path + GetLastError; no MessageBox (would block startup); load+continue disposition aligned with Phase 02 C-06."
    - "Single-source-of-truth RVA surfacing: declare constant once in client.cpp; expose via Client:: static getter + matching extern \"C\" __cdecl shim outside the namespace (works around CppSharp's pointer-return-getter drop). Mirrors Phase B getSwgHwndExport precedent."
    - "Fixture-DLL test bridge: separate test_internal::TestImpl struct in plugin_manager.cpp (NOT PluginManager::Impl which is private) keeps the test seam entirely in the .cpp -- CON-N-08 byte-identical preservation. Module binding via GetModuleHandleA(stagedPath) + Marshal.GetDelegateForFunctionPointer ensures test reads same counters PluginManager mutated."

key-files:
  created:
    - "Utinni.CrtMatchPlugin/Utinni.CrtMatchPlugin.vcxproj (NEW sibling vcxproj, /MD, GUID 6767F360-3D54-4EE2-BD93-04DCBD9F0B02)"
    - "Utinni.CrtMatchPlugin/main.cpp (R-B fixture: symmetric createPlugin+destroyPlugin path + diagnostic counters)"
    - "Utinni.LegacyPlugin/Utinni.LegacyPlugin.vcxproj (NEW sibling vcxproj, /MT, GUID 3B8736D5-6E4A-472A-99D1-16FB49E26595)"
    - "Utinni.LegacyPlugin/main.cpp (R-B fixture: createPlugin only -- exercises virtual-destructor fallback path)"
    - "UtinniCoreDotNet.Tests/Fixtures/CrtMatchPlugin/.gitkeep (deployment landing zone)"
    - "UtinniCoreDotNet.Tests/Fixtures/LegacyPlugin/.gitkeep (deployment landing zone)"
    - "UtinniCoreDotNet.Tests/PluginManagerLifecycleTests.cs (5 R-B regression Facts via P/Invoke)"
    - "UtinniCoreDotNet.Tests/GetSwgWndProcTests.cs (2 R-C regression Facts: P/Invoke getter + grep-style negative test)"
  modified:
    - "UtinniCore/plugin_framework/utinni_plugin.h (UTINNI_PLUGIN macro extended to declare destroyPlugin)"
    - "UtinniCore/plugin_framework/plugin_manager.cpp (Impl::LoadedPlugin shape + two-phase loadPlugins + log::error on LoadLibrary failure + ~PluginManager destroyPlugin/FreeLibrary + test_internal::TestImpl + four extern \"C\" test exports)"
    - "UtinniCore/swg/client/client.h (added static void* getSwgWndProc() declaration)"
    - "UtinniCore/swg/client/client.cpp (Client::getSwgWndProc definition + extern \"C\" getSwgWndProcExport shim)"
    - "UtinniCore/test_exports.cpp (4 new R-B bridge exports added to expected-exports list)"
    - "UtinniCoreDotNet/UI/Controls/PanelGame.cs (literal 0x00AA0970 replaced with cached swgWndProcAddr field; ctor reads via Native.GetSwgWndProc once)"
    - "UtinniCoreDotNet/Utility/Native.cs (added GetSwgWndProc P/Invoke declaration)"
    - "UtinniCoreDotNet/Generated/UtinniCore.cs (CppSharp regenerated; picks up Client::getSwgWndProc symbol but managed consumer uses the C-linkage shim)"
    - "UtinniCoreDotNet.Tests/ExportResolutionTests.cs (ExpectedExportCount 18 -> 22)"
    - "Utinni.sln (two new Project() entries + six new configuration mappings for the fixture vcxprojs)"
    - "UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj (CopyNativeArtifactsForTests target extended with the two fixture DLLs)"

key-decisions:
  - "D-13 enforced strictly: UTINNI_PLUGIN macro break is intentional. Plugins that don't add destroyPlugin link-break against the new macro. Sytner (CON-O-07 disposition D-15) treated as legacy; no compat target preserved through R-B. Loader has a virtual-destructor fallback for legacy DLLs that only export createPlugin."
  - "CON-N-08 byte-identity preserved: plugin_manager.h unchanged from base. The Impl struct extension and LoadedPlugin shape live entirely inside the anonymous pImpl in plugin_manager.cpp. Test seam uses a SEPARATE test_internal::TestImpl struct (not PluginManager::Impl which is private) to avoid even reading the private inner type from outside the class."
  - "D-14 two-phase ordering implemented via the loadPlugins pass split: per-plugin currentDirPlugins vector populated in pass 1, init() invoked in pass 2 after all createPlugin returns. Per-plugin try/catch around init() extends Phase 02 C-06 isolation from compose-time to init-time."
  - "D-17 LoadLibrary error logging: log::error with GetLastError; no MessageBox. Captured via Impl::lastLoadLibraryError for the test bridge so xUnit can assert non-zero error code without grepping log output."
  - "D-19 R-C mechanism: void* return type on the C-side (not the IntPtr type which doesn't exist in C++); CppSharp generates `?getSwgWndProc@...` mangled symbol via the auto-projection. PanelGame.cs uses the hand-rolled extern \"C\" getSwgWndProcExport shim to avoid the CppSharp-drops-pointer-getter issue (same workaround as getSwgHwndExport)."
  - "D-20 caching: PanelGame's swgWndProcAddr is a readonly IntPtr field set once in the ctor via Native.GetSwgWndProc(). WndProc(ref Message m) hot path reads from the field directly -- zero per-message P/Invoke overhead."
  - "D-22 test-bridge fixture-DLL load strategy: stage fixture DLL into a fresh temp dir per test; LoadFromDir against the staged path; Marshal.GetDelegateForFunctionPointer the diagnostic counters by GetProcAddress on GetModuleHandleA(stagedPath) so the test reads the SAME module instance PluginManager loaded (avoids the dual-load problem when the same DLL name exists at multiple absolute paths)."

patterns-established:
  - "test_internal::TestImpl pattern: when CON-N-08 forbids widening a public class with test seams, declare a parallel struct with the same shape in an anonymous (or named) sub-namespace of the same TU. The production class stays untouched; the test seam has full access to the same Impl layout because they share the .cpp file."
  - "Module-instance binding for native fixture DLLs in tests: LoadLibrary -> GetModuleHandleA(stagedPath) -> GetProcAddress(hModule, exportName) -> Marshal.GetDelegateForFunctionPointer. Bypasses DllImport's short-name probing and gives stable bindings to a specific module instance even when multiple copies of the same-name DLL exist at different paths."
  - "Extra-refcount LoadLibrary in tests that read counters AFTER NativeBridge.Dispose: holding our own LoadLibrary ref keeps the module loaded past PluginManager's FreeLibrary, so our delegate function pointers don't dangle when assertions run on shutdown-side state (e.g., destroyPlugin counter)."

requirements-completed: [STAB-02-partial]

# Metrics
duration: "~1h Tasks 1-3 (worktree); ~5 min Tasks 4-5 (inline on master after checkpoint resolution)"
started: "2026-05-21 (worktree agent-a265293e4ff1f79f5 spawn)"
completed_tasks: 5
total_tasks: 5
completed_at: "2026-05-21 (Task 4 cross-repo work landed via UtinniPlugins commit 73b1856; Task 5 inline)"
---

# Phase 3 Plan 02: Plugin lifecycle + RVAs (R-B + R-C) Summary

**All 5 tasks complete. R-B (symmetric plugin lifecycle ABI + two-phase init + HMODULE tracking) and R-C (single-source-of-truth WndProc RVA) landed. Cross-repo paired commit in `kennethlong/UtinniPlugins` (TJT destroyPlugin export at commit `73b1856`) closes Task 4. Task 5 (assessment.md status update) closes the plan.**

**Checkpoint resolution note:** Task 4 was originally classified as `checkpoint:human-action` because the planner assumed Claude had no cross-repo write authority. User clarified mid-execution (2026-05-21) that standing authority covers `kennethlong/UtinniPlugins` (recorded as a memory: `feedback-utinniplugins-authority`). The cross-repo code+commit+push portion of Task 4 was therefore handled inline; live SWG injection smoke (the irreducibly manual portion of the original checkpoint) remains the operator's responsibility — flagged as a watch item in the next phase's CONTEXT.

## Performance

- **Duration so far:** ~1h (single executor session, 2026-05-21)
- **Started:** 2026-05-21 (worktree agent-a265293e4ff1f79f5 spawn)
- **Paused at:** 2026-05-21T20:01:54Z (post Task 3 commit)
- **Tasks complete:** 3 / 5 (Tasks 1, 2, 3)
- **Tasks awaiting:** Task 4 (checkpoint:human-action -- cross-repo work in kennethlong/UtinniPlugins), Task 5 (docs update post-checkpoint)
- **Files modified:** 11 source files + 2 test files + 1 csproj + 1 sln + 2 fixture vcxprojs + 4 fixture/landing-zone files = **21 files**
- **New files:** 8 (2 vcxproj + 2 main.cpp fixtures + 2 .gitkeep + 2 new test files)
- **Commits so far:** 3 (Task 1 / Task 2 / Task 3)
- **Test count:** 76 -> **83** (+7 net new Facts: 5 R-B + 2 R-C)

## Accomplishments (Tasks 1-3)

- **R-B macro extension (Task 1):** `UTINNI_PLUGIN` in `UtinniCore/plugin_framework/utinni_plugin.h` now declares BOTH `createPlugin` AND `destroyPlugin` extern "C" exports. The change is a compile break for any plugin that uses the macro without defining a matching `destroyPlugin` body -- intentional per D-13 + D-15 (CON-O-07: Sytner = legacy, no compat target).
- **Two fixture plugin projects (Task 1):** `Utinni.CrtMatchPlugin` (/MD, exports both createPlugin AND destroyPlugin -- exercises the symmetric path) and `Utinni.LegacyPlugin` (/MT, exports ONLY createPlugin -- exercises the virtual-destructor fallback). Both build green in Release|Win32 + Debug|Win32 configs; co-located with UtinniCore.dll in `bin/$(Configuration)/`. CopyNativeArtifactsForTests target deploys both DLLs into the test bin dir alongside UtinniCore.dll.
- **PluginManager two-phase init + HMODULE tracking + LoadLibrary error log (Task 2):**
  - `Impl::LoadedPlugin{HMODULE, UtinniPlugin*, destroyFn}` shape inside the anonymous pImpl struct in `plugin_manager.cpp`. CON-N-08 strictly preserved -- `plugin_manager.h` byte-identical to the base of plan 03-02.
  - `loadPlugins` rewritten as two pass: pass 1 walks `.dll` files, LoadLibrarys each (LoadLibrary failure logs `log::error` with full path + GetLastError, then continues), captures destroyPlugin (may be nullptr for legacy), pushes onto `Impl::plugins`. Pass 2 invokes `init()` on each with per-plugin `try { ... } catch (std::exception& ex) { log + continue; } catch (...) { log + continue; }` isolation -- extends Phase 02 C-06 from compose-time to init-time.
  - `~PluginManager` invokes `destroyFn(plugin)` if present (symmetric ABI path, plugin frees in plugin's CRT) OR `delete plugin` via the virtual destructor (legacy fallback, best-effort per D-15). Followed by `FreeLibrary(hModule)` (host CRT ref-count decrement only -- not a CRT allocation, safe).
- **Test bridge (Task 2):** 4 new `utinni_test_*` C-linkage exports in `plugin_manager.cpp`: `pluginManagerLoadFromDir` / `pluginManagerLoadedCount` / `pluginManagerDispose` / `lastLoadLibraryError`. Backed by a `test_internal::TestImpl` struct in the same TU (NOT the private `PluginManager::Impl` -- keeps CON-N-08 byte-identical). The test seam uses a function-local static `s_testImpl()` accessor pattern so the symbol stays internal-linkage.
- **5 R-B regression Facts (Task 2):** `PluginManagerLifecycleTests.cs`:
  1. `LoadPlugins_BothFixturesLoaded_BothInitsCalled` -- two-phase init invokes both fixtures' init() exactly once.
  2. `LoadLibraryFailure_LoggedWithGetLastError_OtherPluginsStillLoad` -- 0-byte Broken.dll: LoadLibrary fails with non-zero GetLastError (captured via the test bridge); CrtMatchPlugin still loads.
  3. `PluginInitThrows_OtherPluginsStillInit_NoCrash` -- CrtMatchPlugin's init() throws (via `crtmatch_setInitShouldThrow`); LegacyPlugin's init still runs (per-plugin try/catch isolation).
  4. `DestroyPlugin_InvokedOnShutdown_WhenPresent` -- destroyPlugin counter increments to 1 after Dispose.
  5. `LegacyPlugin_NoDestroyPlugin_FallbackToVirtualDestructor_NoCrash` -- legacy fixture loads, init runs, Dispose triggers the virtual-destructor fallback path -- no crash IS the regression case for D-13's structural fix.
- **R-C single-source RVA (Task 3):**
  - `UtinniCore/swg/client/client.h` -- added `static void* Client::getSwgWndProc()` declaration alongside existing `getSwgHwnd` at lines 78-79.
  - `UtinniCore/swg/client/client.cpp` -- added `Client::getSwgWndProc()` definition (returns the constant at line 43); added `extern "C" __declspec(dllexport) void* __cdecl getSwgWndProcExport()` C-linkage shim mirroring the `getSwgHwndExport` precedent (CppSharp drops pointer-return getters; the C-linkage shim is the managed-side P/Invoke target).
  - `UtinniCoreDotNet/Utility/Native.cs` -- added `[DllImport(EntryPoint = "getSwgWndProcExport")] GetSwgWndProc()` declaration.
  - `UtinniCoreDotNet/UI/Controls/PanelGame.cs` -- replaced the inline `new IntPtr(0x00AA0970)` literal in `WndProc(ref Message m)` with a `readonly IntPtr swgWndProcAddr` field cached at ctor time via `Native.GetSwgWndProc()`. Hot-path read in WndProc has zero per-message P/Invoke overhead (D-20).
- **2 R-C regression Facts (Task 3):** `GetSwgWndProcTests.cs`:
  1. `GetSwgWndProc_ReturnsLiteralConstant` -- P/Invoke into `getSwgWndProcExport`, assert returned IntPtr equals `new IntPtr(0x00AA0970)`. Catches a future drift in the constant declaration.
  2. `PanelGameSource_NoLongerContainsLiteralRVA` -- grep-style negative test on PanelGame.cs source: asserts the literal `0x00AA0970` does NOT appear; positively asserts `Native.GetSwgWndProc()` does appear. Both forms of the hex literal (lower + upper case) checked.

## Task Commits

Each task committed atomically per D-03:

1. **Task 1: UTINNI_PLUGIN macro + fixture plugins** -- `ff0b473` (feat)
2. **Task 2: PluginManager two-phase init + HMODULE + R-B regression Facts** -- `2884c2c` (feat)
3. **Task 3: R-C single-source WndProc RVA + grep-style negative test** -- `9337da7` (feat)
4. **Task 4: Cross-repo TJT destroyPlugin export** -- `UtinniPlugins@73b1856` (cross-repo: `kennethlong/UtinniPlugins` master, pushed 2026-05-21). TJT `plugin.cpp` rewritten from `extern "C" { UTINNI_PLUGIN { return new ...; } }` (old single-body form) to `UTINNI_PLUGIN; createPlugin() {...} destroyPlugin(p) { delete p; }` (new symmetric form). Built green with VS 2026 Release|x86; dumpbin confirms both exports.
5. **Task 5: docs/ai/assessment.md status update** -- inline on master (status-tracking row R-B + R-C → done with implementing SHAs; CON-O-07 disposition resolved in §"Open questions").

## CON-N-08 Verification

`git diff f9b64e03 -- UtinniCore/plugin_framework/plugin_manager.h` reports no changes. `plugin_manager.h` is byte-identical to the wave-1 merge point (the base of Plan 03-02).

## Fixture-Plugin Build Artifacts

| File | Path | Size |
|------|------|------|
| Utinni.CrtMatchPlugin.dll | `bin/Release/Utinni.CrtMatchPlugin.dll` | 12,288 bytes |
| Utinni.LegacyPlugin.dll | `bin/Release/Utinni.LegacyPlugin.dll` | 85,504 bytes |
| UtinniCore.dll (regenerated) | `bin/Release/UtinniCore.dll` | 800,256 bytes |

dumpbin /exports confirms:
- `Utinni.CrtMatchPlugin.dll` exports `createPlugin` AND `destroyPlugin` (plus 5 diagnostic counters: crtmatch_getCreateCount / getInitCount / getDestroyCount / resetCounters / setInitShouldThrow).
- `Utinni.LegacyPlugin.dll` exports `createPlugin` only (plus 2 diagnostic counters: legacy_getInitCount / resetCounters). **No destroyPlugin symbol present.**
- `UtinniCore.dll` exports both the mangled C++ symbol `?getSwgWndProc@Client@utinni@@SAPAXXZ` AND the C-linkage shim `getSwgWndProcExport`.

## Files Created/Modified

**Created (8):**
- `Utinni.CrtMatchPlugin/Utinni.CrtMatchPlugin.vcxproj`
- `Utinni.CrtMatchPlugin/main.cpp`
- `Utinni.LegacyPlugin/Utinni.LegacyPlugin.vcxproj`
- `Utinni.LegacyPlugin/main.cpp`
- `UtinniCoreDotNet.Tests/Fixtures/CrtMatchPlugin/.gitkeep`
- `UtinniCoreDotNet.Tests/Fixtures/LegacyPlugin/.gitkeep`
- `UtinniCoreDotNet.Tests/PluginManagerLifecycleTests.cs`
- `UtinniCoreDotNet.Tests/GetSwgWndProcTests.cs`

**Modified (11):**
- `UtinniCore/plugin_framework/utinni_plugin.h` (macro extension)
- `UtinniCore/plugin_framework/plugin_manager.cpp` (Impl shape + two-phase init + ~PluginManager + test bridge)
- `UtinniCore/swg/client/client.h` (getSwgWndProc declaration)
- `UtinniCore/swg/client/client.cpp` (getSwgWndProc definition + getSwgWndProcExport shim)
- `UtinniCore/test_exports.cpp` (R-B bridge exports in expected list)
- `UtinniCoreDotNet/UI/Controls/PanelGame.cs` (literal removed + ctor cache)
- `UtinniCoreDotNet/Utility/Native.cs` (GetSwgWndProc P/Invoke)
- `UtinniCoreDotNet/Generated/UtinniCore.cs` (CppSharp regen)
- `UtinniCoreDotNet.Tests/ExportResolutionTests.cs` (ExpectedExportCount 18 -> 22)
- `Utinni.sln` (2 new Project entries + 6 configuration mappings)
- `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` (CopyNativeArtifactsForTests extended)

## Decisions Made

- **Test-bridge design (Task 2):** Considered three approaches to expose `PluginManager`-shaped state to xUnit without breaking CON-N-08:
  1. Add a `getImplForTest()` member function to PluginManager. **Rejected** -- requires modifying `plugin_manager.h` (CON-N-08 violation).
  2. Add a `friend` declaration in the .h. **Rejected** -- same CON-N-08 concern.
  3. Add a separate `test_internal::TestImpl` struct with the same layout in `plugin_manager.cpp`. **Chosen** -- no header change, byte-identity preserved, full access to the same Impl logic because both structs live in the same TU.
- **Module-instance binding for fixture-counter reads (Task 2):** First implementation tried `[DllImport("Utinni.CrtMatchPlugin", ...)]` directly. Failed with `DllNotFoundException` under `dotnet test` -- testhost.exe's DllImport search path doesn't include `AppContext.BaseDirectory`. Switched to explicit `LoadLibraryA(absolutePath)` + `GetProcAddress` + `Marshal.GetDelegateForFunctionPointer`. This also solves the dual-load problem: when PluginManager LoadLibrarys the temp-dir path, our binding (via `GetModuleHandleA(tempDirPath)`) resolves to the same module instance with shared static counters.
- **Extra LoadLibrary refcount in `DestroyPlugin_InvokedOnShutdown_WhenPresent`:** After `NativeBridge.Dispose()`, PluginManager's ~Impl calls `FreeLibrary` which drops the refcount to 0 -- the module unloads and our function pointers dangle. Pre-loading the module ourselves with `LoadLibraryA` keeps the refcount at 1 after Dispose, so the post-Dispose `GetDestroyCount()` call still works.
- **R-C return type `void*` (not `IntPtr`):** PATTERNS.md interfaces example showed `IntPtr` as the C++ return type, but `IntPtr` is a C# type, not C++. Used `void*` on the native side (CppSharp projects pointer-returning getters as `IntPtr` on the managed side). The C-linkage shim returns `void*` matching the Win32 convention.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] vcxproj XML comments containing `--` are MSBuild-illegal**
- **Found during:** Task 1 (first build of `Utinni.CrtMatchPlugin.vcxproj`)
- **Issue:** Three XML comments in the new vcxprojs contained `-- frame text --` style hyphen-pairs mid-content. MSBuild rejects XML comments that contain `--` (per W3C XML spec for `<!-- -->` comments).
- **Fix:** Rewrote the comments using single hyphens, colons, or different punctuation. Files affected: `Utinni.CrtMatchPlugin/Utinni.CrtMatchPlugin.vcxproj`, `Utinni.LegacyPlugin/Utinni.LegacyPlugin.vcxproj`.
- **Verification:** MSBuild reads both vcxprojs cleanly post-fix.
- **Committed in:** `ff0b473` (Task 1)

**2. [Rule 3 - Blocking] CrtMatchPlugin missing `<stdexcept>` include**
- **Found during:** Task 1 (Compile pass)
- **Issue:** Plan called for `init() override { ... throw std::runtime_error(...) }` to support the init-throws regression test, but `<stdexcept>` wasn't included. C2039 + C3861 errors.
- **Fix:** Added `#include <stdexcept>` to `Utinni.CrtMatchPlugin/main.cpp`.
- **Verification:** Build clean.
- **Committed in:** `ff0b473` (Task 1)

**3. [Rule 3 - Blocking] CrtMatchPlugin missing UtinniCore.lib linkage**
- **Found during:** Task 1 (Link pass)
- **Issue:** `UtinniPlugin` is `UTINNI_API` (dllimport when consumed), so its vtable + virtual ~UtinniPlugin + UtinniPlugin() ctor are imported symbols. Without linking against `UtinniCore.lib`, the linker resolves these as `__imp_*` unresolved externals (LNK2001 + LNK1120).
- **Fix:** Added `<AdditionalDependencies>UtinniCore.lib;%(AdditionalDependencies)</AdditionalDependencies>` and `<AdditionalLibraryDirectories>$(SolutionDir)bin\$(Configuration)\;%(AdditionalLibraryDirectories)</AdditionalLibraryDirectories>` to both Debug and Release `<Link>` blocks in `Utinni.CrtMatchPlugin.vcxproj`. LegacyPlugin doesn't need this because it uses its own layout-compatible `utinni_legacy::UtinniPlugin` mirror class (deliberately avoids importing from UtinniCore to exercise the legacy /MT-no-UtinniCore-link path).
- **Verification:** CrtMatchPlugin links cleanly; both fixtures build green.
- **Committed in:** `ff0b473` (Task 1)

**4. [Rule 3 - Blocking] DllImport short-name probe doesn't find fixture DLLs under testhost.exe**
- **Found during:** Task 2 (first run of `PluginManagerLifecycleTests`)
- **Issue:** `[DllImport("Utinni.CrtMatchPlugin", ...)]` failed with `DllNotFoundException` even though the DLL was deployed to the test bin dir by `CopyNativeArtifactsForTests`. `dotnet test`'s testhost.exe has a different working directory than `AppContext.BaseDirectory`, and DllImport's name-based probing doesn't unconditionally check the loaded-modules table.
- **Fix:** Switched to explicit `LoadLibraryA(absolutePath)` + `GetProcAddress` + `Marshal.GetDelegateForFunctionPointer` pattern. Each test stages fixtures into a temp dir, calls `LoadFromDir(tempDir)` which loads them; then `GetModuleHandleA(stagedPath)` resolves to the same module PluginManager loaded; `GetProcAddress` + delegate marshalling binds each export.
- **Files modified:** `UtinniCoreDotNet.Tests/PluginManagerLifecycleTests.cs`
- **Verification:** All 5 R-B Facts now pass.
- **Committed in:** `2884c2c` (Task 2)

**5. [Rule 1 - Bug] AccessViolationException in `DestroyPlugin_InvokedOnShutdown_WhenPresent`**
- **Found during:** Task 2 (first successful build of the test)
- **Issue:** After `NativeBridge.Dispose()`, PluginManager's ~Impl calls `FreeLibrary(hModule)` which drops the refcount to 0; the module unloads. The test's already-bound function pointer for `GetDestroyCount` then dangles and AVs when invoked.
- **Fix:** Pre-`LoadLibraryA` the fixture DLL ourselves to add a refcount. PluginManager's `FreeLibrary` then drops to 1 (not 0), so the module stays loaded. `FreeLibrary` our own ref in the `finally`.
- **Files modified:** `UtinniCoreDotNet.Tests/PluginManagerLifecycleTests.cs`
- **Verification:** Test passes.
- **Committed in:** `2884c2c` (Task 2)

**6. [Rule 1 - Bug] PanelGame.cs comment contained the literal RVA, tripping the grep-style negative test**
- **Found during:** Task 3 (first run of `GetSwgWndProcTests`)
- **Issue:** My documentation comment in `PanelGame.cs` mentioned the hex literal `0x00AA0970` for context, but the grep-style test `Assert.DoesNotContain("0x00AA0970", content)` correctly caught it. The test is doing its job -- the comment was just sloppy documentation.
- **Fix:** Rewrote the comment to describe the refactor without including the literal value. The literal now appears ONLY in `UtinniCore/swg/client/client.cpp:43` (single source of truth).
- **Files modified:** `UtinniCoreDotNet/UI/Controls/PanelGame.cs`
- **Verification:** Both R-C Facts pass.
- **Committed in:** `9337da7` (Task 3)

---

**Total deviations (Tasks 1-3):** 6 auto-fixed (4 blocking, 2 bugs)
**Impact on plan:** All six were mechanical or testbed issues discovered during execution. Plan scope and success criteria unchanged. Each deviation strengthened the implementation (e.g., the dual-load discovery led to the more robust GetModuleHandleA-based test bridge).

## Issues Encountered

- **Git dubious-ownership warning at worktree spawn:** `fatal: detected dubious ownership in repository at 'D:/Code/Utinni/.claude/worktrees/agent-a265293e4ff1f79f5'` on the first `git symbolic-ref` call. Resolved by `git config --global --add safe.directory <worktree path>`. Same pattern as Plan 03-01's worktree spawn.
- **Worktree base mismatch at spawn:** Worktree initialized at `b36265e` (a Phase 02 state commit). Reset to the spawn-header-specified base `f9b64e0` (the Phase 03-01 merge point) before any work began.
- **xUnit2013 style warnings:** Pre-existing `Assert.Equal(N, collection.Count)` patterns trigger xUnit's "use Assert.Empty/Single" analyzer. Not regressions; pre-existing in Plan 03-01's test files. Out of scope for this plan.
- **GameCallbacksTests flaky:** A pre-existing GC-survival test (added in Phase 02 C-16) is occasionally flaky -- timing of `GC.Collect()` is non-deterministic. Not caused by Task 2 changes; reruns consistently green. Not blocking.

## Task 4 Resolution (historical: was checkpoint:human-action; closed inline)

**Task 4 was classified `checkpoint:human-action` in the original plan.** Resolution path: the cross-repo write authority gap was the only thing forcing the checkpoint; once user clarified that Claude has standing UtinniPlugins write access, the code+commit+push portion was driven inline. The kept-manual portion (live SWG injection smoke) is flagged as a watch item in §"Next Phase Readiness".

The Utinni framework side of R-B is fully landed (UTINNI_PLUGIN macro + symmetric ABI + loader two-phase init + destroyPlugin/FreeLibrary shutdown). The companion plugin (TJT) lives in the separate `kennethlong/UtinniPlugins` repo; per D-26 + Phase 02 D-09 (no UtinniPlugins CI yet), the migration is operator-driven manual work.

**User steps (per Task 4 of `.planning/phases/03-strategic-reworks-r-a-r-h/03-02-PLAN.md`):**

1. `cd` into the local clone of `kennethlong/UtinniPlugins`.
2. Locate TJT plugin entry point (per CON-T-05 *Impl separation: `TheJawaToolbox.h` declares the public-API class; `TheJawaToolboxImpl.h`/`.cpp` carries the implementation). The export site is wherever `extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin()` currently lives.
3. **Add the symmetric `destroyPlugin` export adjacent to the existing `createPlugin`:**
   ```cpp
   extern "C" __declspec(dllexport) void destroyPlugin(utinni::UtinniPlugin* p)
   {
       delete p;
   }
   ```
   The `delete p` invokes `utinni::UtinniPlugin`'s virtual destructor, which dispatches to `~TheJawaToolboxImpl` via the CON-T-05 *Impl separation. **Precondition (verified by this plan):** `~UtinniPlugin` is virtual in `UtinniCore/plugin_framework/utinni_plugin.h:41` -- so the delete dispatches correctly through the vtable.
4. **Build TJT manually in VS 2022** against the updated Utinni framework (Release|Win32). Confirm the build green.
5. **Smoke-test:** launch SWG with Utinni injection + TJT loaded; verify TJT panels open as before (no behavioral regression). The lifecycle shutdown path (destroyPlugin invocation) is exercised only on PluginManager destruction at process exit.
6. Commit to UtinniPlugins. Suggested commit message:
   ```
   feat(R-B): export destroyPlugin for symmetric ABI

   Symmetric with createPlugin per Utinni Phase 3 Plan 03-02 (commit ff0b473
   in kennethlong/Utinni). Plugin allocates with plugin's `new`, frees with
   plugin's `delete` -- structurally eliminates the cross-CRT delete crash
   class (CON-B-04). The base `~UtinniPlugin` is virtual so `delete p`
   dispatches to `~TheJawaToolboxImpl` via the CON-T-05 *Impl separation.

   Companion Utinni commit: kennethlong/Utinni@ff0b473
   Verified-by: live SWG smoke -- TJT panels open + clean exit
   ```
7. Push the UtinniPlugins commit to `origin`.
8. **Resume signal:** Type `approved` in chat with the UtinniPlugins commit SHA. The orchestrator will then advance to Task 5 (`docs/ai/assessment.md` status update for R-B + R-C + CON-O-07 disposition).

**If the TJT build or smoke fails:** type `blocked: <description>` in chat. The most-likely failure mode is `~UtinniPlugin` not being virtual -- but verification of `utinni_plugin.h:41` (`virtual ~UtinniPlugin() {}`) confirms this precondition is satisfied. Other failures (Visual Studio version mismatch, framework path mis-configuration) are environmental and fixed in the UtinniPlugins repo workspace.

## Next Phase Readiness

- **Plan 03-03 (R-E + R-F + R-G) is now unblocked** per D-04's CI-gated ordering. Master is green: msbuild Release|x86 exits 0 (verified post-merge with VS 2026 MSBuild v18.6.3); `dotnet test UtinniCoreDotNet.Tests` 83/83 pass.
- **Open watch item:** live SWG injection smoke for TJT post-R-B is the operator's responsibility — verify TJT panels open + clean shutdown with destroyPlugin path exercised on process exit. Not phase-blocking; surface in next phase CONTEXT or as a UAT item if it becomes urgent.
- **Toolchain note:** local development bumped from VS 2022 to VS 2026 (Dev18 v18.6.1) mid-Phase-3 per user direction. PlatformToolset stays `v142` for now; a formal bump to `v144` (or `v143`) is a Phase 6-class project.

## Self-Check: PASSED

- `Utinni.CrtMatchPlugin/Utinni.CrtMatchPlugin.vcxproj` -- exists
- `Utinni.CrtMatchPlugin/main.cpp` -- exists
- `Utinni.LegacyPlugin/Utinni.LegacyPlugin.vcxproj` -- exists
- `Utinni.LegacyPlugin/main.cpp` -- exists
- `UtinniCoreDotNet.Tests/Fixtures/CrtMatchPlugin/.gitkeep` -- exists
- `UtinniCoreDotNet.Tests/Fixtures/LegacyPlugin/.gitkeep` -- exists
- `UtinniCoreDotNet.Tests/PluginManagerLifecycleTests.cs` -- exists
- `UtinniCoreDotNet.Tests/GetSwgWndProcTests.cs` -- exists
- Commit `ff0b473` (Task 1) -- present in git log
- Commit `2884c2c` (Task 2) -- present in git log
- Commit `9337da7` (Task 3) -- present in git log
- `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86` exits 0
- `dotnet test UtinniCoreDotNet.Tests`: 83 passed / 0 failed
- `dumpbin /exports bin/Release/Utinni.CrtMatchPlugin.dll`: createPlugin + destroyPlugin both present
- `dumpbin /exports bin/Release/Utinni.LegacyPlugin.dll`: createPlugin only, NO destroyPlugin
- `dumpbin /exports bin/Release/UtinniCore.dll`: getSwgWndProcExport present
- `Select-String -Path UtinniCoreDotNet/UI/Controls/PanelGame.cs -Pattern '0x00AA0970'`: 0 matches
- `git diff f9b64e03 -- UtinniCore/plugin_framework/plugin_manager.h`: no changes (CON-N-08 preserved)

---
*Phase: 03-strategic-reworks-r-a-r-h*
*Plan: 02 -- PARTIAL: Tasks 1-3 complete; Task 4 (cross-repo human-action) awaiting user; Task 5 deferred to post-checkpoint.*
*Paused: 2026-05-21*
