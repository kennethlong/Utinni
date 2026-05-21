# Phase 3: Strategic reworks (R-A..R-H) - Pattern Map

**Mapped:** 2026-05-21
**Files analyzed:** 31 (3 new files + 23 modified source files + 5 new test/test-fixture files)
**Analogs found:** 31 / 31 (every new and modified file has a strong in-tree analog)

## File Classification

### New files Phase 3 creates

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `UtinniCoreDotNet/Callbacks/CallbackHelpers.cs` | callback-utility (static helpers, `internal` API) | request-response | `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs` (existing `internal static Drain` at lines 110-116) | exact (same namespace, same `internal` visibility, same xUnit `[InternalsVisibleTo]` consumer) |
| `Utinni.CrtMatchPlugin/Utinni.CrtMatchPlugin.vcxproj` | native fixture DLL project | request-response | `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj` (sibling .exe project under solution root) | role-match (sibling vcxproj; new project is `DynamicLibrary` not `Application` — Sytner/TJT plugin vcxproj is the closer DLL-shape analog if available in UtinniPlugins repo) |
| `Utinni.CrtMatchPlugin/main.cpp` | UtinniPlugin implementation (`createPlugin` + new `destroyPlugin`) | request-response | `Utinni.LoaderLockHarness/main.cpp` (sibling-project entry-point pattern, MIT header + minimal Windows.h includes) | role-match (entry-point shape; plugin-class definition needs `utinni_plugin.h` macro instead of harness's `int main`) |
| `Utinni.LegacyPlugin/Utinni.LegacyPlugin.vcxproj` | native fixture DLL project (no `destroyPlugin` export — exercises legacy/fallback path) | request-response | Same as `Utinni.CrtMatchPlugin.vcxproj` above | role-match |
| `Utinni.LegacyPlugin/main.cpp` | UtinniPlugin (only `createPlugin`, no `destroyPlugin`) | request-response | Same as `Utinni.CrtMatchPlugin/main.cpp` above | role-match |
| `UtinniCoreDotNet.Tests/Fixtures/CrtMatchPlugin/` (subdir, no managed csproj) | native-DLL deployment landing dir | file-I/O | `UtinniCoreDotNet.Tests/Fixtures/GoodPlugin/` (managed-DLL fixture subdir; `PluginLoaderTests.FindFixtureDll` resolution path is the analog) | role-match (Phase 02 precedent for `Fixtures/<Name>/` layout — R-B fixtures are native, not managed, so no csproj here; native vcxproj OutDir copies via `CopyNativeArtifactsForTests`-style target) |
| `UtinniCoreDotNet.Tests/Fixtures/LegacyPlugin/` | native-DLL deployment landing dir | file-I/O | Same as `Fixtures/CrtMatchPlugin/` above | role-match |
| Per-R-X regression test files in `UtinniCoreDotNet.Tests/` (R-A, R-H, R-B, R-C, R-E, R-F, R-G tests) | xUnit regression test | request-response | `UtinniCoreDotNet.Tests/GroundSceneCallbacksTests.cs` (Drain/queue tests, reflection-based access to private state) + `UtinniCoreDotNet.Tests/PluginLoaderTests.cs` (fixture-driven multi-DLL test) + `UtinniCoreDotNet.Tests/CppSharpSlnDirTests.cs` (pure-function harness with temp dirs) + `UtinniCoreDotNet.Tests/UtinniCfgTests.cs` (file-content grep-style assertion) + `UtinniCoreDotNet.Tests/VsixManifestTests.cs` (XML XDocument parse-and-assert) + `UtinniCoreDotNet.Tests/ExportResolutionTests.cs` + `UtinniCoreDotNet.Tests/FindPatternHarnessTests.cs` (P/Invoke harness) | exact (one analog per R-X test shape — see Pattern Assignments below) |

### Files being modified

| Modified File | Role | Data Flow | Closest Analog (for surrounding-code pattern) | Match Quality |
|---------------|------|-----------|------------------------------------------------|---------------|
| `UtinniCore/swg/game/game.cpp` (R-A native) | native callback registry | event-driven | self (already the canonical shape — extend in place) | n/a (self-modification) |
| `UtinniCore/swg/scene/ground_scene.cpp` (R-A native) | native callback registry | event-driven | `swg/game/game.cpp` (lines 71-104 + 178-181 dispatch loop) | exact |
| `UtinniCore/swg/object/creature_object.cpp` (R-A native) | native callback registry | event-driven | `swg/game/game.cpp` | exact |
| `UtinniCore/swg/graphics/post_processing.cpp` (R-A native) | native callback registry | event-driven | `swg/game/game.cpp` | exact |
| `UtinniCore/swg/graphics/depth_texture.cpp` (R-A native) | native callback registry (member method, not free function) | event-driven | `swg/game/game.cpp` (free-function variant); `swg/object/creature_object.cpp` (closer — member of `addOnTargetCallback`) | role-match (depth_texture is `class::method` not free-function; same vector pattern) |
| `UtinniCore/swg/graphics/shader.cpp` (R-A native) | native callback registry (parameterized callback) | event-driven | `swg/scene/ground_scene.cpp:106-115` (parameterized `void(*)(GroundScene*)` shape) | exact |
| `UtinniCore/swg/graphics/graphics.cpp` (R-A native, 10 callback registries) | native callback registry | event-driven | `swg/game/game.cpp` (5 callback registries) | exact (10 mechanical applications of same per-callback Subscribe/Unsubscribe transform) |
| `UtinniCore/swg/ui/imgui_impl.cpp` (R-A native, render + 4 gizmo callbacks) | native callback registry | event-driven | `swg/game/game.cpp` | exact |
| `UtinniCore/swg/ui/cui_chat_window.cpp` (R-A native) | native callback registry | event-driven | `swg/game/game.cpp` | exact |
| `UtinniCore/swg/ui/cui_manager.cpp` (R-A native — `addReceiveMessageCallback`) | native callback registry | event-driven | `swg/game/game.cpp` | exact |
| `UtinniCore/utility/log.cpp` (R-A native — `addOutputSinkCallback`) | native callback registry | event-driven | `swg/game/game.cpp` | exact |
| `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` (R-A + R-H managed + IN-05 site) | managed callback registry | event-driven | self (canonical Add/Remove pair already at lines 88-101; extend with `Subscribe`/`Unsubscribe`; refactor `Drain` callsite at line 116) | n/a (self) |
| `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs` (R-A + R-H + IN-05 site) | managed callback registry | event-driven | `GameCallbacks.cs` (Add/Remove + Drain are co-located; ground-scene needs Add/Remove added) | exact |
| `UtinniCoreDotNet/Callbacks/ObjectCallbacks.cs` (R-A + IN-05 site) | managed callback registry | event-driven | `GameCallbacks.cs` | exact |
| `UtinniCoreDotNet/Callbacks/CuiCallbacks.cs` (R-A — Add-only today) | managed callback registry | event-driven | `GameCallbacks.cs` lines 88-101 | exact |
| `UtinniCoreDotNet/Callbacks/ImGuiCallbacks.cs` (R-A — partial Remove today; lines 74-82) | managed callback registry | event-driven | `GameCallbacks.cs` lines 88-101 | exact |
| `UtinniCoreDotNet/Utility/Log.cs` (R-A `AddOuputSinkCallback` typo + Add/Remove; R-E FormatText rewrite) | managed callback registry + logging-helper | event-driven (callback half) + request-response (logging half) | `ImGuiCallbacks.cs` (typo-style Add/Remove pair); self at lines 50-69 for FormatText (replace with CallerMemberName) | exact |
| `UtinniCore/plugin_framework/plugin_manager.cpp` (R-B) | plugin lifecycle | request-response | self (extend `Impl` struct + `loadPlugins` two-pass; CON-N-08 mandates Impl stays inside pImpl) | n/a (self-extension; CON-N-08 invariant) |
| `UtinniCore/plugin_framework/utinni_plugin.h` (R-B macro extension) | plugin ABI macro | request-response | self (line 50 single-line macro becomes multi-line); decisions doc has the exact final form | n/a (self) |
| `UtinniCore/swg/client/client.h` (R-C — new `getSwgWndProc` declaration) | UTINNI_API getter declaration | request-response | self lines 78-79 (`static HWND getSwgHwnd()` — exact pattern for adding a static IntPtr-returning getter to the `Client::` class) | exact |
| `UtinniCore/swg/client/client.cpp` (R-C — new `getSwgWndProc` definition + `extern "C"` export if pointer-return drops in CppSharp) | UTINNI_API getter definition | request-response | self lines 122-125 (`HWND Client::getSwgHwnd()`); + lines 290-293 (`extern "C" __declspec(dllexport) HWND __cdecl getSwgHwndExport()` for CppSharp-drops-pointer-getter workaround) | exact |
| `UtinniCoreDotNet/UI/Controls/PanelGame.cs:41` (R-C — replace literal `0x00AA0970`) | managed RVA consumer | request-response | self lines 207-208 (`IntPtr swgHwnd = Native.GetSwgHwnd();` — exact pattern for replacing a literal IntPtr with a P/Invoke or auto-projected getter) | exact |
| `UtinniCoreDotNetGen/Program.cs` (R-F — header allowlist → glob) | build-tooling generator | batch | self at lines 73-103 (current allowlist body — replace with `Directory.EnumerateFiles` glob + `_internal/` filter) | n/a (self-rewrite of the same method body) |
| `sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs` (R-G — early-return → idempotent merge) | build-tooling wizard | file-I/O + XML transform | `UtinniCoreDotNet.Tests/VsixManifestTests.cs` (XDocument round-trip pattern for manipulating PropertyGroup XML; load → modify → save) | role-match (test uses XDocument to ASSERT; Props.cs needs XDocument to LOAD-MODIFY-WRITE — same `System.Xml.Linq` API surface) |

## Pattern Assignments

### `UtinniCoreDotNet/Callbacks/CallbackHelpers.cs` (NEW — callback-utility static class, IN-05 Drain consolidation)

**Analog:** `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs` (existing `internal static Drain` at lines 110-116) plus `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` (lines 116-122) and `UtinniCoreDotNet/Callbacks/ObjectCallbacks.cs` (lines 75-81). All three contain the same duplicated `Drain` body; D-11 consolidates them into this new file.

**File-header pattern** (copy from `GroundSceneCallbacks.cs:1-23` verbatim, then namespace-line at 29):
```csharp
/**
 * MIT License
 *
 * Copyright (c) 2020 Philip Klatt
 * [...standard MIT body, 23 lines total...]
**/

using System;
using System.Collections.Concurrent;

namespace UtinniCoreDotNet.Callbacks
{
```

**Core pattern to extract** (`GroundSceneCallbacks.cs:110-116` — the body that already comments "duplicated per file intentionally — a cross-file shared helper is R-A territory (Phase 3 strategic rework)"):
```csharp
// Shared drain helper introduced for C-04 (CON-O-02 default-fallback per D-12):
// a single TryDequeue loop pattern that every queue-drain call site reuses. The
// outer Count > 0 check from the original code was dropped because TryDequeue
// already returns false on an empty queue, and the Count read was racey under
// concurrent producers anyway.
internal static void Drain(ConcurrentQueue<Action> queue)
{
    while (queue.TryDequeue(out var func))
    {
        func();
    }
}
```

**Visibility pattern:** `internal static` (matches all three existing analog sites). `[InternalsVisibleTo("UtinniCoreDotNet.Tests")]` already in `UtinniCoreDotNet/Properties/AssemblyInfo.cs:28` so the existing `Drain_EmptyQueue_DoesNothing` test in `GroundSceneCallbacksTests.cs:91-97` can re-target the new home without test-file moves.

**Static class shape** (copy from `GroundSceneCallbacks.cs:31` declaration + closing brace):
```csharp
    public static class CallbackHelpers
    {
        internal static void Drain(ConcurrentQueue<Action> queue) { ... }
    }
}
```

---

### `Utinni.CrtMatchPlugin/Utinni.CrtMatchPlugin.vcxproj` + `main.cpp` (NEW R-B fixture pair)

**Analog (vcxproj):** `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj`

**Project-shape pattern** (lines 1-46 of `Utinni.LoaderLockHarness.vcxproj`):
- New GUID required (use `New-Guid` or VS auto-generate; sibling-project convention)
- `<RootNamespace>` matches assembly name
- `<ConfigurationType>Application</ConfigurationType>` → **change to `DynamicLibrary`** for R-B fixtures (DLL not exe)
- `<PlatformToolset>v142</PlatformToolset>` (preserve; matches CON-T-02 toolset)
- `<WindowsTargetPlatformVersion>10.0</WindowsTargetPlatformVersion>` (preserve)
- Three solution configs: `Debug|Win32`, `Release|Win32`, plus `RelWithDbgInfo|Win32` per Phase 02 D-09 sln entry pattern

**OutDir pattern** (lines 46-53 — DO copy verbatim; co-locates fixture DLL next to UtinniCore.dll for test discovery):
```xml
<PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
    <LinkIncremental>false</LinkIncremental>
    <OutDir>$(SolutionDir)bin\$(Configuration)\</OutDir>
</PropertyGroup>
```

**Include-dirs pattern** (line 60 — needs UtinniCore headers for `UTINNI_PLUGIN` macro):
```xml
<AdditionalIncludeDirectories>$(SolutionDir);$(SolutionDir)external;$(ProjectDir);%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
```

**CRT discipline knob for R-B's two-fixture split** (D-07 says one fixture matches host CRT, the other deliberately mismatches — must be set in `<ClCompile>` per-config):
- `<RuntimeLibrary>MultiThreadedDLL</RuntimeLibrary>` (`/MD`) for `Utinni.CrtMatchPlugin` Release (matches UtinniCore.dll's CRT)
- `<RuntimeLibrary>MultiThreaded</RuntimeLibrary>` (`/MT`) for `Utinni.LegacyPlugin` Release (deliberately mismatches — exercises the cross-CRT delete crash class that D-13 destroyPlugin fixes)

**ProjectReference pattern for UtinniCore dependency** (lines 95-102 — same `<LinkLibraryDependencies>false</LinkLibraryDependencies>` because fixtures LoadLibrary at runtime, don't link statically):
```xml
<ItemGroup>
    <ProjectReference Include="..\UtinniCore\UtinniCore.vcxproj">
        <Project>{AEFED7F6-4BA9-44FC-A353-71A463A82FDE}</Project>
        <LinkLibraryDependencies>false</LinkLibraryDependencies>
    </ProjectReference>
</ItemGroup>
```

**Analog (main.cpp):** `Utinni.LoaderLockHarness/main.cpp` (file-header pattern, MIT, ~60 lines).

**File-header pattern** (lines 1-23 — copy verbatim):
```cpp
/**
 * MIT License
 *
 * Copyright (c) 2020 Philip Klatt
 * [...standard MIT body...]
**/
```

**Plugin body pattern** (use `UtinniCore/plugin_framework/utinni_plugin.h` UTINNI_PLUGIN macro at line 50 — Phase 3 extends to also export `destroyPlugin`):

For `Utinni.CrtMatchPlugin`:
```cpp
#include "plugin_framework/utinni_plugin.h"

class CrtMatchPlugin : public utinni::UtinniPlugin
{
public:
    void init() override { /* test-observable side-effect: increment a counter via export */ }
    const Information& getInformation() const override
    {
        static Information info = { "CrtMatchPlugin", "R-B fixture (CRT match)", "Phase 3" };
        return info;
    }
};

extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin()
{
    return new CrtMatchPlugin();
}

// R-B / D-13: symmetric destroyPlugin export. Plugin owns both alloc and free
// in its own CRT — eliminates cross-CRT delete (CON-B-04 class).
extern "C" __declspec(dllexport) void destroyPlugin(utinni::UtinniPlugin* p)
{
    delete p;
}
```

For `Utinni.LegacyPlugin` — same shape BUT **omit `destroyPlugin` export entirely** (exercises the fallback path where R-B's loader must detect the missing export via `GetProcAddress` returning null and fall back to virtual destructor).

**Diagnostic-export pattern for test observability** (precedent: `extern "C" __declspec(dllexport) HWND __cdecl getSwgHwndExport()` at `client.cpp:290-293`). Each fixture exports a test-observable counter so xUnit can assert `init()` actually ran:
```cpp
static int s_initCount = 0;
extern "C" __declspec(dllexport) int __cdecl crtmatch_getInitCount() { return s_initCount; }
```

---

### `UtinniCoreDotNet.Tests/Fixtures/CrtMatchPlugin/` + `Fixtures/LegacyPlugin/` (NEW subdirs, no csproj — native fixture landing zones)

**Analog:** `UtinniCoreDotNet.Tests/Fixtures/GoodPlugin/` and `Fixtures/BrokenPlugin/` (managed precedent — Phase 02 C-06 fixture pair).

**Layout pattern:** Phase 02 placed managed fixtures at `Tests/Fixtures/<Name>/<Name>.csproj`. R-B native fixtures are **not** managed and **do not** need an in-fixture-dir csproj — the vcxproj is at solution root (Phase 02 `Utinni.LoaderLockHarness` precedent). The `Fixtures/CrtMatchPlugin/` and `Fixtures/LegacyPlugin/` directories serve as **deployment landing zones** for the built DLLs.

**Deployment pattern** (extend the `CopyNativeArtifactsForTests` Target in `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj:57-66`):
```xml
<Target Name="CopyNativeArtifactsForTests" AfterTargets="Build">
    <ItemGroup>
        <_NativeTestArtifacts Include="$(SolutionDir)bin\$(Configuration)\UtinniCore.dll" />
        <_NativeTestArtifacts Include="$(SolutionDir)bin\$(Configuration)\Utinni.LoaderLockHarness.exe" />
        <!-- Phase 3 R-B additions: -->
        <_NativeTestArtifacts Include="$(SolutionDir)bin\$(Configuration)\Utinni.CrtMatchPlugin.dll" />
        <_NativeTestArtifacts Include="$(SolutionDir)bin\$(Configuration)\Utinni.LegacyPlugin.dll" />
    </ItemGroup>
    <Copy SourceFiles="@(_NativeTestArtifacts)"
          DestinationFolder="$(TargetDir)"
          SkipUnchangedFiles="true"
          Condition="Exists('%(_NativeTestArtifacts.Identity)')" />
</Target>
```

The Phase 3 R-B test (analog: `PluginLoaderTests.cs:49-68` — `FindFixtureDll`) creates a temp dir, copies the fixture DLLs in, points `PluginManager` (via P/Invoke or by direct file-system staging in the host's `Plugins/` dir) at the temp dir.

---

### `UtinniCore/swg/<area>/<file>.cpp` (R-A native, ~12 files mechanically — Subscribe/Unsubscribe with handle-based registry per D-08/D-09)

**Analog (canonical):** `UtinniCore/swg/game/game.cpp` (lines 71-104 registry declarations + Add functions; lines 117-181 dispatch loops).

**Current registry pattern** (game.cpp:71-75 — duplicated across all ~12 files):
```cpp
static std::vector<void(*)()> installCallbacks;
static std::vector<void(*)()> preMainLoopCallbacks;
static std::vector<void(*)()> mainLoopCallbacks;
static std::vector<void(*)()> setSceneCallbacks;
static std::vector<void(*)()> cleanUpSceneCallbacks;
```

**Target registry pattern (per D-09 — opaque int handle, monotonic next-id, unordered_map-backed):**
```cpp
static std::unordered_map<int, void(*)()> installCallbacks;
static std::unordered_map<int, void(*)()> preMainLoopCallbacks;
// ...
static int s_nextCallbackId = 1; // 0 reserved as invalid sentinel per D-09
```

**Current Add pattern** (game.cpp:81-104 — five mechanical near-duplicates, one per registry):
```cpp
void Game::addInstallCallback(void(*func)())
{
    installCallbacks.emplace_back(func);
}
```

**Target Subscribe/Unsubscribe pattern** (D-08/D-09/D-10 — `Add*` kept as wrapper, return-value discarded):
```cpp
// New primary API: Subscribe returns opaque handle for later Unsubscribe.
int Game::subscribeInstallCallback(void(*func)())
{
    int id = s_nextCallbackId++;
    installCallbacks[id] = func;
    return id;
}

bool Game::unsubscribeInstallCallback(int handle)
{
    return installCallbacks.erase(handle) > 0;
}

// D-10: Add* retained as a wrapper around Subscribe for source-compat
// (existing UtinniPlugins/TJT/Sytner keep working without recompile).
void Game::addInstallCallback(void(*func)())
{
    subscribeInstallCallback(func); // return value discarded
}
```

**Current dispatch pattern** (game.cpp:178-181 — repeated at 5 sites in game.cpp, dozens across the 12 files):
```cpp
for (const auto& func : installCallbacks)
{
    func();
}
```

**Target dispatch pattern (R-H snapshot iteration per D-12 — snapshot under nothing-fancy since native side has no lock today; the snapshot guards against Subscribe-during-dispatch invalidating the iterator):**
```cpp
// R-H: snapshot before iteration so Subscribe()-during-dispatch can't
// invalidate the iterator. Subscribers added mid-iteration land in the
// registry but fire on the NEXT dispatch.
auto snapshot = std::vector<void(*)()>{};
snapshot.reserve(installCallbacks.size());
for (const auto& [id, func] : installCallbacks)
{
    snapshot.push_back(func);
}
for (const auto& func : snapshot)
{
    func();
}
```

**Per-file enumeration (all 12 native callback registry sites for R-A + R-H — exact list deliverable per orchestrator request):**

| File | Add* function lines | Dispatch site lines | Registries (count) |
|------|---------------------|---------------------|--------------------|
| `UtinniCore/swg/game/game.cpp` | 81-104 | 117-119, 135-138, 178-181, 213-216, 239-242, 366-369 | 5 |
| `UtinniCore/swg/scene/ground_scene.cpp` | 106-115, 132-140 | 119-122, 126-129, 144-147 | 4 |
| `UtinniCore/swg/object/creature_object.cpp` | 41-44 | 52-55 | 1 |
| `UtinniCore/swg/graphics/post_processing.cpp` | 62-70 | 44-47, 56-59 | 2 |
| `UtinniCore/swg/graphics/depth_texture.cpp` | 206-209 | 248-251 | 1 (member method of `DepthTexture::`, not free-function — D-08 still applies; handle stored per-instance OR static-class registry, planner's discretion) |
| `UtinniCore/swg/graphics/shader.cpp` | 80-83 | (dispatch in mid* asm trampoline — not greppable; orchestrator note: parameterized `void(*)(int)` callback, dispatch likely in `midPopCell` naked function or elsewhere — planner verifies during R-A pass) | 1 |
| `UtinniCore/swg/graphics/graphics.cpp` | 101-149 | (10 dispatch sites in `hkBeginScene`/`hkEndScene`/`hkPresent`/`hkUpdate`; planner enumerates in-line during R-A pass) | 10 |
| `UtinniCore/swg/ui/imgui_impl.cpp` | 411-414, 479-497 | (dispatch in `imgui_impl::renderInGame` ~line 395-403; `imgui_gizmo::enable/disable/positionChanged/rotationChanged` ~lines 444-470) | 5 |
| `UtinniCore/swg/ui/cui_chat_window.cpp` | 80-83 | (CommandParser dispatch — find during pass) | 1 |
| `UtinniCore/swg/ui/cui_manager.cpp` | 130-133 | 156-159 | 1 |
| `UtinniCore/utility/log.cpp` | 106-109 | (dispatch in `log` sink helper — find during pass) | 1 |

**Total native registries:** ~32 across 11 files (one file = `swg/graphics/graphics.cpp` carries 10 registries; the orchestrator's "~12 files" estimate is roughly right; the exact-count metric is ~32 callback registries to convert).

---

### `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` (R-A managed + R-H snapshot + IN-05 Drain consolidation)

**Analog:** self (lines 88-101 already have Add/Remove pair shape — extend with Subscribe/Unsubscribe + apply snapshot iteration).

**Current Add/Remove pattern** (lines 88-101 — KEEP per D-10 as wrapper, add new Subscribe/Unsubscribe alongside):
```csharp
public static void AddInstallCallback(Action call)
{
    installCallbacks.Add(call);
}

public static void RemoveInstallCallback(Action call)
{
    installCallbacks.Remove(call);
}
```

**Target Subscribe/Unsubscribe pattern (D-08/D-09 — `Dictionary<int, Action>` mirrors native `unordered_map`):**
```csharp
private static readonly Dictionary<int, Action> installSubscribers = new Dictionary<int, Action>();
private static int s_nextId = 1; // 0 reserved as invalid sentinel per D-09
private static readonly object subscriberLock = new object();

public static int SubscribeInstall(Action call)
{
    lock (subscriberLock)
    {
        int id = s_nextId++;
        installSubscribers[id] = call;
        return id;
    }
}

public static bool UnsubscribeInstall(int handle)
{
    lock (subscriberLock)
    {
        return installSubscribers.Remove(handle);
    }
}

// D-10: AddInstallCallback retained as wrapper (existing UtinniPlugins keep working).
public static void AddInstallCallback(Action call)
{
    SubscribeInstall(call); // return value discarded
}
```

**Current dispatch pattern** (lines 124-130 — replicated 3x in this file):
```csharp
private static void CallInstallCallbacks()
{
    foreach (Action callback in installCallbacks)
    {
        callback();
    }
}
```

**Target dispatch pattern (D-12 managed — snapshot via `.ToArray()` under lock; iteration outside lock):**
```csharp
private static void CallInstallCallbacks()
{
    Action[] snapshot;
    lock (subscriberLock)
    {
        // R-H: snapshot under lock; Subscribe-during-dispatch via callback
        // re-entry lands in installSubscribers but doesn't appear in snapshot,
        // so it fires on the NEXT dispatch instead of throwing
        // InvalidOperationException.
        snapshot = installSubscribers.Values.ToArray();
    }
    foreach (Action callback in snapshot)
    {
        callback();
    }
}
```

**IN-05 Drain refactor pattern** (lines 103-122 — three duplicated bodies become three callsites + one shared helper):

**Before** (this file's lines 116-122):
```csharp
internal static void Drain(ConcurrentQueue<Action> queue)
{
    while (queue.TryDequeue(out var func))
    {
        func();
    }
}
```

**After** (lines 103-111 reference the new shared helper):
```csharp
private static void DequeuePreMainLoopCalls()
{
    CallbackHelpers.Drain(preMainLoopCallQueue);
}

private static void DequeueMainLoopCalls()
{
    CallbackHelpers.Drain(mainLoopCallQueue);
}
// (Drain helper removed from this file — moved to CallbackHelpers.cs)
```

**Apply same Subscribe/Unsubscribe transform per-callback to:**
- `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` (5 callbacks: install, setupScene, cleanupScene, preMainLoop, mainLoop — the last two are queue-backed not subscriber-backed; D-08 Subscribe applies to SynchronizedCollection-backed registries, queues stay queues per IN-05)
- `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs` (1 subscriber-backed: cameraChange; 3 queue-backed: update/preDraw/postDraw)
- `UtinniCoreDotNet/Callbacks/ObjectCallbacks.cs` (1 subscriber-backed: onTarget; 1 queue-backed: onTargetCall)
- `UtinniCoreDotNet/Callbacks/CuiCallbacks.cs` (1 subscriber-backed: onReceiveSystemMessage — currently Add-only; needs Subscribe/Unsubscribe + the existing Remove* missing slot filled)
- `UtinniCoreDotNet/Callbacks/ImGuiCallbacks.cs` (4 subscriber-backed: onEnabled/onDisabled/onPositionChanged/onRotationChanged — partial Remove already at lines 74-82; complete the pair for onEnabled+onDisabled + add Subscribe for all)
- `UtinniCoreDotNet/Utility/Log.cs:121-129` (1 subscriber-backed: outputSink — fix typo `AddOuputSinkCallback` → `AddOutputSinkCallback`; add Subscribe/Unsubscribe alongside)

---

### `UtinniCoreDotNet/Utility/Log.cs` (R-E CallerMemberName + R-A typo fix)

**Analog:** self at lines 50-69 (FormatText with StackTrace.GetFrame walk — replace).

**Current pattern** (lines 50-69):
```csharp
private static string FormatText(string text)
{
    if (writeClassName)
    {
        var method = new StackTrace().GetFrame(2).GetMethod();

        if (writeFunctionName)
        {
            return "[" + method.ReflectedType.Name + "][" + method.Name + "] " + text;
        }
        else
        {
            return "[" + method.ReflectedType.Name + "] " + text;
        }
    }
    else
    {
        return text;
    }
}
```

**Target pattern (D-21 — `[CallerMemberName]`/`[CallerFilePath]` on each public Log method; FormatText takes the resolved strings; StackTrace gone):**
```csharp
using System.Runtime.CompilerServices;
// ... existing usings ...

private static string FormatText(string text, string callerName, string callerFile)
{
    if (writeClassName)
    {
        // Class name comes from Path.GetFileNameWithoutExtension(callerFile) — compile-time
        // resolved by [CallerFilePath]; zero runtime cost.
        string className = System.IO.Path.GetFileNameWithoutExtension(callerFile);
        if (writeFunctionName)
        {
            return "[" + className + "][" + callerName + "] " + text;
        }
        else
        {
            return "[" + className + "] " + text;
        }
    }
    else
    {
        return text;
    }
}

public static void Info(string text,
    [CallerMemberName] string callerName = "",
    [CallerFilePath] string callerFile = "")
{
    log.Info(FormatText(text, callerName, callerFile));
}
// ... same pattern for Debug/Warning/Error/Critical ...
```

**R-A typo-fix pattern** (lines 121, 126):

**Before:**
```csharp
public static void AddOuputSinkCallback(Action<string> call) { outputSinkCallbacks.Add(call); }
public static void RemoveOuputSinkCallback(Action<string> call) { outputSinkCallbacks.Remove(call); }
```

**After (typo corrected; Add* retained as wrapper per D-10; Subscribe/Unsubscribe added):**
```csharp
public static void AddOutputSinkCallback(Action<string> call) { SubscribeOutputSink(call); }
public static void RemoveOutputSinkCallback(Action<string> call) { /* legacy — see Subscribe */ }
// Deprecated-typo aliases retained for source-compat if any UtinniPlugin code calls them today:
public static void AddOuputSinkCallback(Action<string> call) { AddOutputSinkCallback(call); }
public static void RemoveOuputSinkCallback(Action<string> call) { RemoveOutputSinkCallback(call); }

public static int SubscribeOutputSink(Action<string> call) { /* dict + lock per D-09 */ }
public static bool UnsubscribeOutputSink(int handle) { /* dict.Remove */ }
```

---

### `UtinniCore/plugin_framework/plugin_manager.cpp` (R-B two-phase init + HMODULE tracking + LoadLibrary error log)

**Analog:** self at lines 33-49 (`Impl` struct + `~PluginManager`) and lines 129-150 (`loadPlugins` body).

**Current Impl shape** (lines 33-37 — CON-N-08 invariant says this MUST stay inside pImpl):
```cpp
struct PluginManager::Impl
{
    std::vector<PluginConfig> pluginConfigs;
    std::vector<UtinniPlugin*> plugins;
};
```

**Target Impl shape (D-16 — HMODULE paired with plugin pointer; struct stays inside the pImpl per CON-N-08):**
```cpp
struct PluginManager::Impl
{
    std::vector<PluginConfig> pluginConfigs;

    // R-B / D-16: track HMODULE alongside plugin pointer so ~PluginManager
    // can call destroyPlugin (in plugin's CRT) + FreeLibrary (host's CRT)
    // in the correct order. CON-N-08 invariant preserved: this struct stays
    // entirely inside the pImpl — public PluginManager header surface
    // (plugin_manager.h:33-52) does NOT change.
    struct LoadedPlugin
    {
        HMODULE hModule;
        UtinniPlugin* plugin;
        // R-B / D-13: cached destroyPlugin export (nullptr for legacy plugins
        // that only export createPlugin — fall back to virtual destructor).
        void(*destroyFn)(UtinniPlugin*);
    };
    std::vector<LoadedPlugin> plugins;
};
```

**Current ~PluginManager** (lines 41-49):
```cpp
PluginManager::~PluginManager()
{
    for (const auto& plugin : pImpl->plugins)
    {
        delete plugin;
    }
    delete pImpl;
}
```

**Target ~PluginManager (D-13 + D-16 — destroyPlugin then FreeLibrary; legacy fallback to virtual dtor):**
```cpp
PluginManager::~PluginManager()
{
    // Shutdown order per D-16: destroyPlugin (in plugin's CRT) → FreeLibrary
    // (host's ref-count decrement, OK to do from host CRT — DLL ref-count
    // is not a CRT allocation).
    for (const auto& loaded : pImpl->plugins)
    {
        if (loaded.destroyFn != nullptr)
        {
            // R-B / D-13: symmetric path — plugin allocated, plugin frees.
            loaded.destroyFn(loaded.plugin);
        }
        else
        {
            // D-13 legacy fallback: plugin only exported createPlugin (no
            // destroyPlugin). Best-effort delete via virtual destructor.
            // CON-O-07 disposition: no compat target preserved.
            delete loaded.plugin;
        }
        FreeLibrary(loaded.hModule);
    }
    delete pImpl;
}
```

**Current loadPlugins body** (lines 129-150 — the LoadLibrary site that silently swallows failures):
```cpp
for (const auto& file : std::filesystem::recursive_directory_iterator(currentPluginDir))
{
    if (file.path().extension() == ".dll")
    {
        const HINSTANCE hDllInstance = LoadLibrary(file.path().string().c_str());
        if (hDllInstance != nullptr)
        {
            const auto createPlugin = (pCreatePlugin) GetProcAddress(hDllInstance, "createPlugin");
            if (createPlugin != nullptr)
            {
                UtinniPlugin* plugin = createPlugin();
                if (plugin != nullptr)
                {
                    pImpl->plugins.emplace_back(plugin);
                }
            }
        }
    }
}
```

**Target loadPlugins body** (D-14 two-phase + D-16 HMODULE capture + D-17 LoadLibrary error logging):
```cpp
using pDestroyPlugin = void(*)(UtinniPlugin*);

// Pass 1: createPlugin all DLLs. init() deferred to pass 2 so a plugin's
// init() can look up sibling plugins via PluginManager (mirrors the MEF
// two-phase composition pattern from Phase 2 C-06).
for (const auto& file : std::filesystem::recursive_directory_iterator(currentPluginDir))
{
    if (file.path().extension() == ".dll")
    {
        const HINSTANCE hDllInstance = LoadLibrary(file.path().string().c_str());
        if (hDllInstance == nullptr)
        {
            // D-17: surface LoadLibrary failures visibly. No MessageBox
            // (would block startup); log + continue. Phase 02 C-06 isolation
            // disposition extends here.
            char errMsg[512];
            snprintf(errMsg, sizeof(errMsg),
                "Failed to load plugin DLL '%s': GetLastError=%lu",
                file.path().string().c_str(), GetLastError());
            log::error(errMsg);
            continue;
        }

        const auto createPlugin = (pCreatePlugin) GetProcAddress(hDllInstance, "createPlugin");
        if (createPlugin == nullptr)
        {
            FreeLibrary(hDllInstance); // not a valid Utinni plugin
            continue;
        }

        UtinniPlugin* plugin = createPlugin();
        if (plugin == nullptr)
        {
            FreeLibrary(hDllInstance);
            continue;
        }

        // D-13: destroyPlugin is optional — legacy plugins (CON-O-07 Sytner)
        // only export createPlugin. Cache the function pointer (may be null).
        const auto destroyPlugin = (pDestroyPlugin) GetProcAddress(hDllInstance, "destroyPlugin");
        pImpl->plugins.push_back({ hDllInstance, plugin, destroyPlugin });
    }
}

// Pass 2: init all. Per-plugin try/catch isolation (Phase 02 C-06 disposition).
for (const auto& loaded : pImpl->plugins)
{
    try
    {
        loaded.plugin->init();
    }
    catch (const std::exception& ex)
    {
        char errMsg[512];
        snprintf(errMsg, sizeof(errMsg),
            "Plugin init() threw: %s (plugin name=%s)",
            ex.what(),
            loaded.plugin->getInformation().name);
        log::error(errMsg);
    }
    catch (...)
    {
        log::error("Plugin init() threw unknown exception");
    }
}
```

**`UTINNI_PLUGIN` macro extension** (`utinni_plugin.h:50` — single line becomes multi-line):

**Before:**
```cpp
#define UTINNI_PLUGIN extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin()
```

**After (per D-13 — exact form from CONTEXT.md decisions):**
```cpp
#define UTINNI_PLUGIN \
    extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin(); \
    extern "C" __declspec(dllexport) void destroyPlugin(utinni::UtinniPlugin* p)
```

---

### `UtinniCore/swg/client/client.h` + `client.cpp` (R-C single-source WndProc RVA)

**Analog:** self at `client.h:78-79` (Phase B `static HWND getSwgHwnd()` is the exact analog for adding a static pointer-returning getter to the `Client::` class; with the matching `extern "C"` workaround for CppSharp pointer-return drops at `client.cpp:290-293`).

**client.h target addition** (lines 78-90 area — add `getSwgWndProc` alongside `getSwgHwnd`):
```cpp
// R-C / D-18..D-20: single-source the SWG WndProc RVA (0x00AA0970).
// Previously duplicated as `new IntPtr(0x00AA0970)` literal in
// UtinniCoreDotNet/UI/Controls/PanelGame.cs:41. Surfacing the value once
// via UTINNI_API getter eliminates the duplication and gives the managed
// side a runtime read of the constant (R-A pattern template; if a future
// phase adds a managed reader for the two isSafeToUse RVAs they get the
// same treatment per D-18).
static IntPtr getSwgWndProc(); // returns 0x00AA0970 as IntPtr (32-bit on x86)
```

**client.cpp target addition** (sibling to existing `Client::getSwgHwnd()` at lines 122-125):
```cpp
IntPtr Client::getSwgWndProc()
{
    return (IntPtr)swg::client::wndProc; // 0x00AA0970, declared at client.cpp:43
}
```

**`extern "C"` export pattern** (per `client.cpp:290-293` precedent — required because CppSharp drops pointer-returning getters; same workaround as `getSwgHwndExport`):
```cpp
// R-C: C-linkage export so PanelGame.cs can read the WndProc address
// without going through CppSharp-generated bindings (the generator drops
// pointer-returning getters — same constraint as getSwgHwndExport at
// client.cpp:290). Pattern mirrors getPresentBlockedEvent in directx9.cpp.
extern "C" __declspec(dllexport) IntPtr __cdecl getSwgWndProcExport()
{
    return (IntPtr)swg::client::wndProc;
}
```

**`PanelGame.cs:41` consumer pattern** (analog: `PanelGame.cs:168` — `IntPtr swgHwnd = Native.GetSwgHwnd();`):

**Before:**
```csharp
protected override void WndProc(ref Message m)
{
    IntPtr swgWndProc = new IntPtr(0x00AA0970);
    Native.CallWindowProc(swgWndProc, m.HWnd, m.Msg, m.WParam, m.LParam);
    base.WndProc(ref m);
}
```

**After (D-20 — resolve once in ctor, cache as field; matches existing field-cache shape at line 71-73):**
```csharp
// R-C / D-20: cache once at ctor time; WndProc reads from the field on the
// hot path with zero per-message overhead.
private readonly IntPtr swgWndProcAddr;

public PanelGame(PluginLoader pluginLoader)
{
    // ... existing ctor body ...
    swgWndProcAddr = Native.GetSwgWndProc(); // P/Invoke via extern "C" export
}

protected override void WndProc(ref Message m)
{
    Native.CallWindowProc(swgWndProcAddr, m.HWnd, m.Msg, m.WParam, m.LParam);
    base.WndProc(ref m);
}
```

**`Native.cs` P/Invoke pattern** (analog: `Native.cs:121-123` — `GetSwgHwnd` declaration; mirror exactly):
```csharp
[DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
    EntryPoint = "getSwgWndProcExport")]
public static extern IntPtr GetSwgWndProc();
```

---

### `UtinniCoreDotNetGen/Program.cs` (R-F CppSharp header auto-discovery)

**Analog:** self at lines 73-103 (current explicit allowlist body — rewrite the same method's body).

**Current pattern** (lines 78-103):
```cpp
module.Headers.Add("utinni.h");
module.Headers.Add("utility\\log.h");
module.Headers.Add("plugin_framework\\plugin_manager.h");
// ... 23 lines of explicit headers ...
```

**Target pattern (D-22 — Directory.EnumerateFiles glob + `_internal/` filter; closes CON-O-05):**
```csharp
// R-F / D-22: auto-discover headers under UtinniCore/. The CppSharp generator
// reads ALL public headers under UtinniCore/ except those under an _internal/
// directory (convention: any header NOT to be projected to managed lives in
// _internal/). CON-O-05 disposition (D-24): StdEdited.cs is NOT a regenerated
// artifact — it's hand-curated for std::basic_string and other unstable
// MSVC-template symbols. Std.cs (generated by UtinniCore-Symbols) is also
// out of scope. Only Generated/UtinniCore.cs is regenerated by this glob.
string utinniCoreRoot = Path.Combine(slnDir, targetProjName);
foreach (string headerPath in Directory.EnumerateFiles(
    utinniCoreRoot, "*.h", SearchOption.AllDirectories))
{
    // _internal/ convention per D-22: headers under any directory segment
    // named "_internal" are explicitly hidden from managed projection.
    string relPath = headerPath.Substring(utinniCoreRoot.Length + 1);
    if (relPath.Split('\\', '/').Any(seg =>
        string.Equals(seg, "_internal", StringComparison.OrdinalIgnoreCase)))
    {
        continue;
    }
    // CppSharp expects backslash-separated relative-path strings.
    module.Headers.Add(relPath.Replace('/', '\\'));
}
```

**Test pattern (R-F fixture test per D-05 — temp dir fixture tree):**

**Analog:** `UtinniCoreDotNet.Tests/CppSharpSlnDirTests.cs` (full file — same pattern of temp-dir fixture tree + walk-and-assert, including the `Directory.CreateDirectory`/`File.WriteAllText` fixture seed and the `try/finally Directory.Delete recursive: true` cleanup).

Excerpt for the R-F test (mirror `CppSharpSlnDirTests.cs:74-104`):
```csharp
[Fact]
public void DiscoverHeaders_FixtureTree_ExcludesInternalDirectory()
{
    // Arrange: temp/UtinniCore/{public.h, _internal/private.h, sub/public2.h}
    string tempRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    string utinniCoreRoot = Path.Combine(tempRoot, "UtinniCore");
    Directory.CreateDirectory(Path.Combine(utinniCoreRoot, "_internal"));
    Directory.CreateDirectory(Path.Combine(utinniCoreRoot, "sub"));
    File.WriteAllText(Path.Combine(utinniCoreRoot, "public.h"), "");
    File.WriteAllText(Path.Combine(utinniCoreRoot, "_internal", "private.h"), "");
    File.WriteAllText(Path.Combine(utinniCoreRoot, "sub", "public2.h"), "");

    try
    {
        // Act
        var discovered = HeaderDiscovery.Discover(utinniCoreRoot); // exposed via R-F refactor

        // Assert
        Assert.Contains("public.h", discovered);
        Assert.Contains(@"sub\public2.h", discovered);
        Assert.DoesNotContain(@"_internal\private.h", discovered);
        Assert.Equal(2, discovered.Count);
    }
    finally
    {
        Directory.Delete(tempRoot, recursive: true);
    }
}
```

---

### `sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs` (R-G idempotent merge — early-return becomes XML merger)

**Analog (caller-side):** self at lines 9-14 (the `if (File.Exists(...)) { return; }` early-return is the bug; rewrite the body around it).

**Analog (XML manipulation pattern):** `UtinniCoreDotNet.Tests/VsixManifestTests.cs:79-99` uses `XDocument.Load` + namespace-aware `Descendants` to read PropertyGroup-style XML; that exact pattern is what `Props.cs` needs for the load-modify-write half of D-23.

**Current pattern** (Props.cs:9-14):
```csharp
public static void CreateDotNetDirectoryProps(string slnPath)
{
    if (File.Exists(slnPath + DirectoryBuildPropsFilename))
    {
        return; // R-G BUG: silently no-ops if user has an existing Directory.Build.props.
    }
    // ... 50+ lines of StringBuilder XML construction ...
    File.WriteAllText(slnPath + DirectoryBuildPropsFilename, sb.ToString());
}
```

**Target pattern (D-23 — idempotent merger; CON-T-04 mandates this stays the only method in Props.cs):**
```csharp
public static void CreateDotNetDirectoryProps(string slnPath)
{
    string filePath = slnPath + DirectoryBuildPropsFilename;

    // R-G / D-23: idempotent merge replaces the silent early-return.
    // CON-T-04 invariant: this method stays factored in Props.cs as the single
    // entry point; only its body changes. The wizard remains callable with the
    // same signature from all existing call sites.

    XDocument doc;
    XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

    if (File.Exists(filePath))
    {
        // (b)/(c) per D-05: existing file — load, locate-or-create PropertyGroup,
        // merge Utinni properties without disturbing user properties.
        doc = XDocument.Load(filePath);
    }
    else
    {
        // (a) per D-05: no file — create the minimal skeleton.
        doc = new XDocument(new XElement(ns + "Project"));
    }

    XElement project = doc.Element(ns + "Project");
    if (project == null)
    {
        project = new XElement(ns + "Project");
        doc.Add(project);
    }

    // Utinni-property merge target: an unconditional PropertyGroup with
    // a tag-name marker so we can re-find it idempotently.
    UpsertPropertyGroup(project, ns, conditionAttr: null,
        new (string, string)[] {
            ("PluginOutputDir", @"$(SolutionDir)\bin\$(Configuration)\"),
            ("UtinniCoreDotNetPath", @"..\..\..\bin\Release\"),
        });

    // Three configuration-conditional PropertyGroups follow the same
    // upsert pattern (per-config OutputPath/DebugType/Optimize/etc.).
    UpsertPropertyGroup(project, ns,
        conditionAttr: "'$(Configuration)|$(Platform)' == 'RelWithDbgInfo|AnyCPU'",
        properties: /* the existing RelWithDbgInfo block, hoisted */);
    // ... Release|AnyCPU, Debug|AnyCPU groups same pattern ...

    doc.Save(filePath);
}

private static void UpsertPropertyGroup(XElement project, XNamespace ns,
    string conditionAttr, (string name, string value)[] properties)
{
    XElement group = project.Elements(ns + "PropertyGroup")
        .FirstOrDefault(pg =>
            (conditionAttr == null && pg.Attribute("Condition") == null) ||
            (conditionAttr != null && pg.Attribute("Condition")?.Value == conditionAttr));
    if (group == null)
    {
        group = new XElement(ns + "PropertyGroup");
        if (conditionAttr != null)
        {
            group.SetAttributeValue("Condition", conditionAttr);
        }
        project.Add(group);
    }
    foreach (var (name, value) in properties)
    {
        XElement el = group.Element(ns + name);
        if (el == null)
        {
            group.Add(new XElement(ns + name, value));
        }
        else
        {
            el.Value = value; // (c) per D-05: idempotent re-run updates value.
        }
    }
}
```

**Test pattern (R-G three-case fixture per D-05):**

**Analog:** `UtinniCoreDotNet.Tests/CppSharpSlnDirTests.cs:74-104` (temp-dir fixture pattern) + `UtinniCoreDotNet.Tests/VsixManifestTests.cs:79-99` (XDocument-load-and-assert pattern).

Three xUnit `[Fact]` tests:
1. `CreateDotNetDirectoryProps_NoExistingFile_CreatesFreshSkeleton` — temp dir with no file → assert file created + has expected PropertyGroups + values.
2. `CreateDotNetDirectoryProps_ExistingFileMissingUtinniProps_MergesIn` — temp dir with file containing only `<Project xmlns=...><PropertyGroup><UserProperty>foo</UserProperty></PropertyGroup></Project>` → assert UserProperty preserved + Utinni properties added.
3. `CreateDotNetDirectoryProps_ExistingFileWithUtinniPropsAlready_NoOpsIdempotently` — call twice, assert file unchanged on second call (or value-stable).

---

## Shared Patterns

### MIT License Header (`CONVENTIONS.md` invariant — every C++ and C# source file)

**Source:** `UtinniCore/swg/game/game.cpp:1-23` (or any source file in repo — all carry the identical 23-line header).

**Apply to:** All new files Phase 3 creates (`CallbackHelpers.cs`, `Utinni.CrtMatchPlugin/main.cpp`, `Utinni.LegacyPlugin/main.cpp`, and every new test file in `UtinniCoreDotNet.Tests/`).

```cpp
/**
 * MIT License
 *
 * Copyright (c) 2020 Philip Klatt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
**/
```

### `Utinni.sln` sibling-project entry pattern (Phase 02 + Phase 02.1 precedent)

**Source:** `Utinni.sln:30-44` (BrokenPlugin csproj entry + Utinni.LoaderLockHarness vcxproj entry).

**Apply to:** New `Utinni.CrtMatchPlugin.vcxproj` and `Utinni.LegacyPlugin.vcxproj` sln entries (sibling-project entries at solution root).

```
Project("{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}") = "Utinni.CrtMatchPlugin", "Utinni.CrtMatchPlugin\Utinni.CrtMatchPlugin.vcxproj", "{<new-guid>}"
    ProjectSection(ProjectDependencies) = postProject
        {AEFED7F6-4BA9-44FC-A353-71A463A82FDE} = {AEFED7F6-4BA9-44FC-A353-71A463A82FDE}
    EndProjectSection
EndProject
```

Plus three configuration mappings per `Utinni.sln:80-84` (`Debug|x86`, `Release|x86`, `RelWithDbgInfo|x86` → `*|Win32` for the vcxproj — note Phase 02 sln line 84 maps `RelWithDbgInfo|x86` → `Release|Win32` for harness projects that don't carry a `RelWithDbgInfo` config; planner copies this exact mapping).

### xUnit fixture-driven test (multi-DLL, fixture-resolver helper)

**Source:** `UtinniCoreDotNet.Tests/PluginLoaderTests.cs:49-68` (`FindFixtureDll`) + lines 70-75 (`MakeTempDir`) + lines 77-107 (`Load_WithOneBrokenAndOneGoodPlugin_LoadsGoodAndLogsBroken`) + lines 151-156 (`TryDeleteDir` finally cleanup).

**Apply to:** R-B regression test (`PluginManagerTests.cs` or `R_B_Lifecycle_Tests.cs`). Same FindFixtureDll-resolves-from-AppContext.BaseDirectory pattern, same MakeTempDir + try/finally cleanup, applied to NATIVE fixture DLLs instead of managed ones (P/Invoke into `PluginManager::loadPlugins` from a thin `utinni_test_*` export, OR set up the temp dir as the SWG `Plugins/` dir + invoke through host).

### xUnit reflection-based access to private statics (Drain/queue-state probing)

**Source:** `UtinniCoreDotNet.Tests/GroundSceneCallbacksTests.cs:45-59` (`GetQueue` + `InvokePrivate`).

**Apply to:** R-A managed-side subscriber-state probe (test that `Unsubscribe(handle)` actually removes the entry from the private `Dictionary<int, Action>`).

```csharp
private static Dictionary<int, Action> GetSubscribers(string fieldName)
{
    var f = typeof(GameCallbacks).GetField(
        fieldName, BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(f);
    return (Dictionary<int, Action>)f.GetValue(null);
}
```

### xUnit P/Invoke harness (native-bridge test class)

**Source:** `UtinniCoreDotNet.Tests/GameCallbacksTests.cs:47-52` (NativeBridge nested static class with `[DllImport]` declarations) + `UtinniCoreDotNet.Tests/FindPatternHarnessTests.cs:42-67` (multi-method NativeBridge + GCHandle pin pattern) + `UtinniCoreDotNet.Tests/ExportResolutionTests.cs:48-56` (one-export harness with EntryPoint = "utinni_test_*").

**Apply to:** R-C `getSwgWndProc` P/Invoke test, R-A native Subscribe/Unsubscribe P/Invoke harness via new `utinni_test_*` exports.

```csharp
private static class NativeBridge
{
    [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "getSwgWndProcExport")]
    public static extern IntPtr GetSwgWndProc();
}

[Fact]
public void GetSwgWndProc_Returns_0x00AA0970()
{
    IntPtr addr = NativeBridge.GetSwgWndProc();
    Assert.Equal(new IntPtr(0x00AA0970), addr);
}
```

### grep-style negative test (assert a string NO LONGER appears in a file)

**Source:** `UtinniCoreDotNet.Tests/UtinniCfgTests.cs:64-74` (`Assert.Matches` / `Assert.DoesNotMatch` against file content with regex — "loginServerAddress0 = blank").

**Apply to:**
- R-C grep test (`PanelGame.cs` must not contain literal `0x00AA0970` anymore).
- R-E grep test (`Log.cs` must not contain `new StackTrace().GetFrame` anymore).

```csharp
[Fact]
public void PanelGame_NoLongerContainsLiteralWndProcRva()
{
    string panelGameSrc = File.ReadAllText(FindPanelGameSrc());
    Assert.DoesNotMatch(@"0x00AA0970", panelGameSrc); // R-C: surfaced via getSwgWndProc()
}

[Fact]
public void Log_NoLongerWalksStackTraceInFormatText()
{
    string logSrc = File.ReadAllText(FindLogSrc());
    Assert.DoesNotMatch(@"new StackTrace\(\)\.GetFrame", logSrc); // R-E: replaced by [CallerMemberName]
}
```

Source-locator helper follows the `FindCfg` / `FindManifest` precedent (4-level walk-up from `AppContext.BaseDirectory`).

### XDocument round-trip (load → assert → save)

**Source:** `UtinniCoreDotNet.Tests/VsixManifestTests.cs:79-99` (XDocument.Load + namespace-aware Descendants + attribute reads).

**Apply to:**
1. **R-G test fixtures** — assert post-merge `Directory.Build.props` XML state by loading + walking PropertyGroups.
2. **R-G implementation** in `Props.cs` (same `System.Xml.Linq` API surface — load-mutate-save instead of load-assert).

### Per-collection xUnit `[Collection("StaticCallbackState")]` attribute

**Source:** `UtinniCoreDotNet.Tests/GroundSceneCallbacksTests.cs:42` (the test class declares membership in the collection that serializes static-callback state across tests in the same assembly).

**Apply to:** All R-A and R-H managed-side tests that touch shared static state on the `*Callbacks` classes (multiple tests in the same xUnit assembly run in parallel by default; static collections + queues require the `[Collection]` serialization).

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| (none) | — | — | Every Phase 3 file has at least a role-match analog in-tree. The closest-to-novel is R-G's XML idempotent-merger logic, but `VsixManifestTests.cs` provides the XDocument API surface and Phase 02 `Props.cs` itself provides the I/O scaffolding — the merge logic is novel code, not a novel pattern. |

## Preservation Invariants Made Visible

(Per orchestrator's request to surface CON-N-08, CON-T-04, CON-N-02, CON-T-05 in PATTERNS.md so planner can copy them into plan actions.)

### CON-N-08 — `PluginManager` pImpl

**Source:** `UtinniCore/plugin_framework/plugin_manager.h:49-51`:
```cpp
private:
    struct Impl;
    Impl* pImpl{};
```

**Invariant:** The R-B extension of `Impl` from `vector<UtinniPlugin*>` to `vector<{HMODULE, UtinniPlugin*, destroyFn}>` MUST stay entirely inside `plugin_manager.cpp`'s anonymous `struct PluginManager::Impl { ... }` definition (currently lines 33-37). The public header `plugin_manager.h` surface (lines 33-52) is frozen — no new members on `PluginManager` itself, only on the hidden `Impl`.

**Apply to:** Plan 03-02 R-B task.

### CON-T-04 — `Props.cs` factoring

**Source:** `sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs:9` (the `CreateDotNetDirectoryProps` static method is THE entry point; CON-T-04 mandates this method stays the single factored point of `Directory.Build.props` creation).

**Invariant:** R-G's refactor changes only the method **body**, not its **signature** (`public static void CreateDotNetDirectoryProps(string slnPath)`) and not its **location** (`sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs`). The XML-merge logic can live in a `private static` helper inside `Props.cs` (e.g., `UpsertPropertyGroup`) but does NOT get factored into a new file.

**Apply to:** Plan 03-03 R-G task.

### CON-N-02 — Thin-wrapper firewall

**Source:** `UtinniCore/plugin_framework/plugin_manager.cpp:29` (`namespace utinni { ... }` wraps the entire PluginManager implementation — the thin-wrapper firewall lives at this layer).

**Invariant:**
- R-B `destroyPlugin` is invoked from `~PluginManager` (inside `namespace utinni`); the macro it consumes (`UTINNI_PLUGIN`) lives in `utinni_plugin.h` which is in `namespace utinni`. Both sides of the destroyPlugin contract sit in the wrapper layer.
- R-C `getSwgWndProc` is declared on `class Client` (`namespace utinni`, `swg/client/client.h:60`) and defined in `client.cpp` inside `namespace utinni` (lines 61-126 are the `utinni::` block). The `extern "C"` export `getSwgWndProcExport` lives outside the namespace (matches the existing `getSwgHwndExport` precedent at `client.cpp:290-293`) but is just a C-linkage shim around the in-wrapper getter.

**Apply to:** Plan 03-02 (both R-B and R-C tasks).

### CON-T-05 — Jawa Toolbox `*Impl` separation (cross-repo R-B touch)

**Source:** Not in this repo. Lives in `kennethlong/UtinniPlugins` per `project_fork_strategy` memory and CONTEXT.md D-26.

**Invariant:** TJT's plugin entry point in `UtinniPlugins` exports `createPlugin` returning a `TheJawaToolbox*` instance, where `TheJawaToolbox` is the public-API class and `TheJawaToolboxImpl` is the implementation behind it (CON-T-05 separation pattern). R-B's TJT-side migration adds the symmetric `destroyPlugin` export that deletes the `TheJawaToolbox*` through the public-API class's virtual destructor — must NOT collapse the `Impl` separation.

**Apply to:** Plan 03-02 D-26 cross-repo task (paired UtinniPlugins commit).

---

## Metadata

**Analog search scope:**
- `UtinniCore/swg/**/*.cpp` (12 callback-registry files enumerated, listed in pattern assignments)
- `UtinniCoreDotNet/Callbacks/*.cs` (5 files, all read in full)
- `UtinniCoreDotNet/Utility/*.cs` (2 files: Log.cs, Native.cs)
- `UtinniCoreDotNet.Tests/*.cs` (16 test files surveyed; 7 used as test-pattern analogs)
- `UtinniCoreDotNet.Tests/Fixtures/{BrokenPlugin,GoodPlugin}/` (managed-fixture precedent for R-B native-fixture layout)
- `Utinni.LoaderLockHarness/` (sibling-project vcxproj + main.cpp precedent)
- `UtinniCore/plugin_framework/*.{cpp,h}` (PluginManager + UtinniPlugin)
- `UtinniCore/swg/client/{client.h,client.cpp}` (R-C analog source + Phase B extern-C export precedent)
- `UtinniCoreDotNet/UI/Controls/PanelGame.cs` (R-C consumer site)
- `UtinniCoreDotNetGen/Program.cs` (R-F current allowlist)
- `sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs` (R-G)
- `Utinni.sln` (sibling-project sln-entry pattern)

**Files read in full:** 23
**Files spot-read with offset:** 7
**Patterns extracted per category:** imports/headers, MIT header, registry storage, Add/Subscribe/Unsubscribe shape, dispatch + snapshot iteration, lifecycle (ctor/dtor + two-phase init), error logging, extern-C CppSharp workaround, P/Invoke from C# Native helper, XDocument load-mutate-save, fixture deployment via msbuild Target, xUnit `[Fact]` + `[Collection]` + reflection-private-state-probe + grep-style negative + P/Invoke harness + XDocument fixture.

**Pattern extraction date:** 2026-05-21
