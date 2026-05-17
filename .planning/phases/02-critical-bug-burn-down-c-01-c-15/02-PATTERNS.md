---
phase: 02
slug: critical-bug-burn-down-c-01-c-15
mapped: 2026-05-16
files_analyzed: 23 new + 14 modified
analogs_found: 22 / 23 (1 = no in-repo analog → flagged)
---

# Phase 02 Patterns — File → Closest Analog Map

> Per-file pattern assignments for Phase 2 bug burn-down (C-01..C-16 + KB-05).
> Consumed by `gsd-planner` to write `<read_first>` blocks in 02-01..02-04 PLAN files.
> All analogs are concrete file paths with line numbers; planner cites verbatim.

**Project context:**
- No `./CLAUDE.md` at repo root — conventions live in `.planning/codebase/CONVENTIONS.md` (Allman braces, 4-space indent, 23-line MIT header on every `.cs/.cpp/.h`, `// ToDo` not `// TODO`, no underscore prefix on private fields).
- No `.planning/codebase/PATTERNS.md` — Phase 1's `01-PATTERNS.md` is the operative cross-phase pattern source.
- No `.claude/skills/` or `.agents/skills/` directory.
- Phase 1 established `UtinniCoreDotNet.Tests` (SDK-style csproj, net472/x86, xUnit 2.9.3) as the single managed-test home. Phase 2 absorbs into it.

---

## Section 1 — Managed xUnit test files (14 new)

**Universal analog for ALL 14 files:** `UtinniCoreDotNet.Tests/HotkeyTests.cs` (lines 1-68, full file).

This file establishes everything a Phase-2 test file needs:
1. **23-line MIT header** (lines 1-23, opens `/**`, closes `**/`, `Copyright (c) 2020 Philip Klatt` — verbatim, unchanged from upstream per CONVENTIONS.md §File Headers).
2. **Using-block ordering** per CONVENTIONS.md §C# using Order: `System.*` → third-party (`using Xunit;`) → `UtinniCore.*` → `UtinniCoreDotNet.*`. See `HotkeyTests.cs:25-27`.
3. **Namespace + class** Allman-braced (`namespace UtinniCoreDotNet.Tests { public class HotkeyTests { ... } }`, lines 29-31).
4. **Test naming** `[Method]_[Scenario]_[ExpectedOutcome]` per Phase 1 D-04 (e.g. `Ctor_StringConstructor_SingleKey_SetsKeyAndNoModifier`).
5. **Test body** uses 4-space indent, Allman braces, `var` for locals with obvious RHS, no `_` prefix on locals, named args for booleans (`overrideGameInput: false`), `Record.Exception(() => ...)` for "did it throw" assertions (NOT try/catch).
6. **Skip-with-comment pattern** for known-fail tests: `[Fact(Skip = "C-08: explanation...")]` (lines 49-66). Used in C-08 task to flip Phase-1 skips green.

| New file | Closest analog | Why | What to copy verbatim |
|----------|---------------|-----|----------------------|
| `UtinniCoreDotNet.Tests/GroundSceneCallbacksTests.cs` (C-04) | `UtinniCoreDotNet.Tests/HotkeyTests.cs:1-68` | Same xUnit project, same conventions; subject (`GroundSceneCallbacks`) is `static` so test uses reflection or `InternalsVisibleTo` per CONTEXT.md Wave 0 list | Header 1-23, using order 25-27, namespace+class 29-31, `[Fact]` method 33-39, naming convention |
| `UtinniCoreDotNet.Tests/GameCallbacksTests.cs` (C-16) | `HotkeyTests.cs:1-68` + `Native.cs:67-77` for `[DllImport]` shape | xUnit shape from HotkeyTests; P/Invoke shape from `Native.cs` user32 examples (closest in-repo `[DllImport]` to first-party DLL) | Header 1-23, `[DllImport(..., CallingConvention=Cdecl, EntryPoint="...")]` form |
| `UtinniCoreDotNet.Tests/GameDragDropEventHandlersTests.cs` (C-05) | `HotkeyTests.cs:1-68` (also `using System.Windows.Forms;` exists at line 25 — proves WinForms ref already wired in Phase 1) | Same xUnit shape; `System.Windows.Forms` reference already in `UtinniCoreDotNet.Tests.csproj:28` (no csproj edit needed) | Header 1-23, WinForms `using` line 25 |
| `UtinniCoreDotNet.Tests/PluginLoaderTests.cs` (C-06) | `HotkeyTests.cs:1-68` for shape; `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:39-73` for subject behavior (read to understand the fix surface) | xUnit shape + need to understand existing MEF compose to write the per-plugin try/catch test | Header 1-23 |
| `UtinniCoreDotNet.Tests/UndoRedoManagerTests.cs` (C-07) | `HotkeyTests.cs:1-68` for shape; `UndoRedoManager.cs:1-119` for subject + the testability seam (registerCleanupCallback ctor param per Phase-1 D-06 deferred work) | xUnit shape + subject under test reads necessary to construct the `new UndoRedoManager(..., registerCleanupCallback: _ => {})` test fixture | Header 1-23, ctor seam pattern from research |
| `UtinniCoreDotNet.Tests/Clr10HarnessTests.cs` (C-10) | `HotkeyTests.cs:1-68` + `Native.cs:67-77` for `[DllImport]` | P/Invoke against test-only export `utinni_clr_stop`; assert no AV on double-call | Header 1-23, P/Invoke pattern |
| `UtinniCoreDotNet.Tests/FindPatternHarnessTests.cs` (C-11) | `HotkeyTests.cs:1-68` + `Native.cs:67-77` for `[DllImport]` | P/Invoke against `utinni_findPattern` + `utinni_getVtbl`; assert absent-pattern returns 0 + no crash | Header 1-23, P/Invoke pattern |
| `UtinniCoreDotNet.Tests/VsixManifestTests.cs` (C-12) | `HotkeyTests.cs:1-68` | Pure XML/XPath assertion against `sdk/UtinniPluginTemplates/Vsix/source.extension.vsixmanifest`; no P/Invoke; uses `System.Xml.Linq` (BCL in net472) | Header 1-23 |
| `UtinniCoreDotNet.Tests/UtinniCfgTests.cs` (C-14) | `HotkeyTests.cs:1-68` | File-content assertion against `data/utinni.cfg`; reads file via `System.IO.File.ReadAllText` (BCL) | Header 1-23 |
| `UtinniCoreDotNet.Tests/CppSharpSlnDirTests.cs` (C-15) | `HotkeyTests.cs:1-68` | Pure-function test of refactored `ResolveSlnDir` (extract from `UtinniCoreDotNetGen/Program.cs:39-41`); xUnit `[Theory]` + `[InlineData]` for the three resolution modes | Header 1-23, `[Theory]/[InlineData]` shape from `HotkeyTests.cs:49-58` |
| `UtinniCoreDotNet.Tests/LoaderLockHarnessTests.cs` (C-01) | `HotkeyTests.cs:1-68` | Spawns `Utinni.LoaderLockHarness.exe` via `System.Diagnostics.Process.Start`; asserts exit code 0 (timing < 50 ms) | Header 1-23 |
| `UtinniCoreDotNet.Tests/FormMainSignallerTests.cs` (C-09) | `HotkeyTests.cs:1-68` | Mock signaller via injectable `EventWaitHandle`; assert `WaitOne(timeout)` returns true; no `Thread.Sleep` polled | Header 1-23 |
| `UtinniCoreDotNet.Tests/ConfigBufferFreeTests.cs` (C-02) | `HotkeyTests.cs:1-68` + `Native.cs:67-77` | Partial proof — calls test-only `utinni_test_freeConfigBuffer` export; asserts return without crash | Header 1-23, P/Invoke pattern |
| `UtinniCoreDotNet.Tests/NetworkCastTests.cs` (C-03) | `HotkeyTests.cs:1-68` + `Native.cs:67-77` | Partial proof — post-condition wrapper test; assert returned value `!= 0xCCCCCCCC` (MSVC debug-init pattern) and `!= 0` (uninit-stack pattern) | Header 1-23, P/Invoke pattern |

**Shared additional patterns for the P/Invoke subset (C-02, C-03, C-10, C-11, C-16):**

See `Section 5 — P/Invoke shape` below. Closest in-repo `[DllImport]` against a first-party DLL is `UtinniCoreDotNet/Generated/UtinniCore.cs:19-22` (auto-generated CppSharp). Closest hand-written `[DllImport]` is `UtinniCoreDotNet/Utility/Native.cs:67-77` (user32). The Phase-2 test files should follow `Native.cs`'s hand-written shape, not the CppSharp shape — the CppSharp shape uses mangled C++ symbol names which we deliberately avoid by using `extern "C"` exports on the test-only native side.

---

## Section 2 — Fixture projects (2 new)

### `UtinniCoreDotNet.Tests/Fixtures/BrokenPlugin/BrokenPlugin.csproj`
### `UtinniCoreDotNet.Tests/Fixtures/GoodPlugin/GoodPlugin.csproj`

**Closest in-repo analog (partial):** `sdk/UtinniPluginTemplates/DotNetPluginTemplate/ProjectTemplate.csproj` (lines 1-50). This IS a plugin csproj — but it's a Visual-Studio **template** (with `$projectname$` / `$guid1$` placeholders, legacy non-SDK format) and references `UtinniCoreDotNet.dll` via a `<HintPath>$(UtinniCoreDotNetPath)UtinniCoreDotNet.dll</HintPath>` that the Vsix wizard fills in. Not a copy target.

**Why no clean analog exists:** plugins live in the sister repo `UtinniPlugins` (the consumer ecosystem). This repo has zero in-tree plugin csprojs that compile against the local `UtinniCoreDotNet.csproj`. The fixtures must be authored from scratch.

**Fixture csproj template (planner emits from-scratch in the plan):**

Use SDK-style for ergonomics + match the test project's tooling (Phase 1 D-01 sibling convention):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <PlatformTarget>x86</PlatformTarget>
    <Platforms>x86</Platforms>
    <AppendPlatformToOutputPath>false</AppendPlatformToOutputPath>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
    <RootNamespace>UtinniCoreDotNet.Tests.Fixtures.BrokenPlugin</RootNamespace>
    <AssemblyName>BrokenPlugin</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\UtinniCoreDotNet\UtinniCoreDotNet.csproj">
      <!-- Don't bundle UtinniCoreDotNet.dll with the fixture output — the test
           loads the fixture DLL standalone via a per-plugin DirectoryCatalog. -->
      <Private>False</Private>
    </ProjectReference>
  </ItemGroup>
</Project>
```

**Source pattern** (BrokenPlugin/BrokenPlugin.cs) — minimal IPlugin implementation following `sdk/UtinniPluginTemplates/DotNetPluginTemplate/Plugin.cs:1-25` shape but with a deliberately-throwing ctor:

```csharp
// [23-line MIT header here, copied verbatim from UtinniCoreDotNet/Hotkeys/Hotkey.cs:1-23]

using UtinniCore.Utinni;
using UtinniCoreDotNet.PluginFramework;

namespace UtinniCoreDotNet.Tests.Fixtures.BrokenPlugin
{
    public class BrokenPlugin : IPlugin
    {
        public BrokenPlugin()
        {
            throw new System.InvalidOperationException(
                "BrokenPlugin deliberately throws during construction — exercises C-06 isolation.");
        }

        public PluginInformation Information { get; }
        public UtINI GetConfig() => null;
    }
}
```

`IPlugin` is declared at `UtinniCoreDotNet/PluginFramework/IPlugin.cs:44-49` with `[InheritedExport(typeof(IPlugin))]` — that attribute is what MEF's `DirectoryCatalog` discovers, so the fixture inherits it automatically.

**GoodPlugin** is identical except the ctor does `Information = new PluginInformation("GoodPlugin", "...", "...");` and returns successfully.

**Solution wiring** (per VALIDATION.md Wave 0): add both fixture csprojs to `Utinni.sln` with C# project-type GUID `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}` and the same x86-only config-mapping shape as `UtinniCoreDotNet.Tests` (see `01-PATTERNS.md` §`Utinni.sln` for exact mapping pattern).

**Flag for planner:** No clean in-repo analog. Use the template above; cite `sdk/UtinniPluginTemplates/DotNetPluginTemplate/Plugin.cs:1-25` as the IPlugin-shape reference and `UtinniCoreDotNet/PluginFramework/IPlugin.cs:30-49` as the interface contract.

---

## Section 3 — Sibling C++ project (1 new)

### `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj`
### `Utinni.LoaderLockHarness/main.cpp`

**Closest in-repo analog:** `Launcher/Launcher.vcxproj` (lines 1-157, full file) + `Launcher/main.cpp` (lines 1-401, full file).

**Why this is an exact analog:**
- `Launcher` is a Win32 console exe that does `LoadLibrary`-style work (`GetProcAddress`, `CreateRemoteThread` at `:185-186`). Same `ConfigurationType=Application`, same `Win32` platform, same `v142` toolset, same `stdcpp17`, same MIT header.
- Loads dynamically against `UtinniCore.dll` (line 181-182: `currentDir + "UtinniCore.dll"`).
- `Launcher.vcxproj` already has the Debug/Release/RelWithDbgInfo three-config matrix Phase 2 needs.
- Output to `$(SolutionDir)bin\$(Configuration)\` (`Launcher.vcxproj:63,67,71`) — the C-01 harness needs the same output path so the test project can `Process.Start` it without path acrobatics.

**vcxproj — copy from `Launcher/Launcher.vcxproj`** with these deltas:

| Line(s) in analog | Keep | Change |
|---|---|---|
| 3-15 (`ItemGroup Label="ProjectConfigurations"`) | All three configs (Debug/Release/RelWithDbgInfo \| Win32) | Unchanged |
| 17-22 (`Globals`) | `<VCProjectVersion>16.0</VCProjectVersion>`, `<WindowsTargetPlatformVersion>10.0`, `<ConfigurationType>Application</ConfigurationType>` | New `<ProjectGuid>`, `<RootNamespace>Utinni.LoaderLockHarness</RootNamespace>` |
| 24-45 (per-config Configuration blocks) | `<UseDebugLibraries>` per config, `<PlatformToolset>v142</PlatformToolset>`, `<CharacterSet>NotSet</CharacterSet>` | Unchanged |
| 61-71 (per-config `<OutDir>`) | `$(SolutionDir)bin\$(Configuration)\` | Unchanged |
| 73-136 (`ItemDefinitionGroup`) | `<WarningLevel>Level3</WarningLevel>`, `<LanguageStandard>stdcpp17</LanguageStandard>`, `<AdditionalIncludeDirectories>$(SolutionDir);$(SolutionDir)external;$(ProjectDir);` | Unchanged |
| 82-87 (`<Link><SubSystem>Console</SubSystem>`) | Console subsystem | Unchanged (harness is a console exe; stdout exit-code matters for the test) |
| 137-153 (ItemGroups: `<ClCompile>`, `<ProjectReference>`, `<ClInclude>`, `<ResourceCompile>`, `<Image>`) | `<ClCompile Include="main.cpp" />` | **Drop** the `UtINI` `<ProjectReference>` (lines 141-143) — harness doesn't read ini. **Drop** `resource.h`, `Launcher.rc`, `TJT.ico` (lines 146-152) — harness has no resources. |

**main.cpp shape** — replaces `Launcher/main.cpp:174-210` (`inject` function) with a simpler `QueryPerformanceCounter`-bracketed `LoadLibraryA`:

```cpp
// [23-line MIT header copied verbatim from Launcher/main.cpp:1-23]

#include <Windows.h>
#include <cstdio>

// Harness for C-01: measure DllMain entry-exit timing of UtinniCore.dll.
// Exits 0 if LoadLibraryA returns within 50 ms threshold (DllMain did no heavy work).
// Exits 1 otherwise (DllMain regression — heavy startup leaked back in).

int main(int /*argc*/, char* /*argv*/[])
{
    LARGE_INTEGER freq, start, end;
    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&start);

    HMODULE hDll = LoadLibraryA("UtinniCore.dll");

    QueryPerformanceCounter(&end);

    if (hDll == nullptr)
    {
        std::fprintf(stderr, "[ERROR] LoadLibraryA(UtinniCore.dll) returned nullptr (GLE=%lu)\n", GetLastError());
        return 2;
    }

    const double elapsedMs = (double)(end.QuadPart - start.QuadPart) * 1000.0 / (double)freq.QuadPart;
    std::printf("UtinniCore DllMain elapsed: %.3f ms\n", elapsedMs);

    FreeLibrary(hDll);

    return (elapsedMs < 50.0) ? 0 : 1;
}
```

**Deltas vs `Launcher/main.cpp`:**
- No `CreateRemoteThread`, no SWG-client discovery, no `OPENFILENAME`, no `GetFileVersionInfo`.
- No `UtINI` include (drop the `#include "UtINI/utini.h"` from `Launcher/main.cpp:43`).
- Single-purpose `main()`; matches C-01 partial-proof design per CONTEXT.md D-05.
- Console subsystem (the analog at `Launcher/main.cpp:45` switches to `/SUBSYSTEM:windows` with `#pragma comment(linker, ...)`; harness keeps Console default so stdout is visible in the test runner).

**Solution wiring:** new entry in `Utinni.sln` with C++ project-type GUID `{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}` (cite from any existing `.vcxproj` entry in `Utinni.sln`). Configs Debug/Release/RelWithDbgInfo \| Win32 — Build all three (it's the simplest project in the solution; no per-config skips like the test project has for `RelWithDbgInfo`).

---

## Section 4 — Native exports in existing UtinniCore.dll

**Test-only exports** (new symbols added to `UtinniCore.dll` to make P/Invoke harnesses feasible):
- `utinni_clr_stop` (C-10)
- `utinni_findPattern` (C-11)
- `utinni_getVtbl` (C-11)
- `Game::triggerInstallCallbacks` — actually a member, but exported via free-function wrapper `utinni_triggerInstallCallbacks` (C-16)
- `getPresentBlockedEvent` — also production code (C-09); exports the `HANDLE` from `CreateEvent`
- `utinni_test_freeConfigBuffer` (C-02 partial proof)
- `utinni_test_networkCast` (C-03 partial proof)

**Production export:**
- `utinni_init` (C-01 fix surface — invoked by launcher's second `CreateRemoteThread`)

**Closest analog for the `extern "C" __declspec(dllexport)` pattern:** `UtinniCore/plugin_framework/utinni_plugin.h:50`:

```cpp
#define UTINNI_PLUGIN extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin()
```

This is the ONLY in-tree use of `extern "C" __declspec(dllexport)`. All other UtinniCore exports use the `UTINNI_API` macro (`utinni.h:42-46`) which wraps `__declspec(dllexport)`/`__declspec(dllimport)` without `extern "C"` linkage — those produce mangled C++ symbol names that CppSharp consumes (see `UtinniCoreDotNet/Generated/UtinniCore.cs:21` for the mangled `?getPath@utinni@@YAABV?$basic_string@DU?$char_traits...` entry point).

**Why Phase 2 uses `extern "C"` not `UTINNI_API`:** Phase-2 P/Invokes from `UtinniCoreDotNet.Tests` deliberately bypass CppSharp (the test code is hand-written, no regenerated bindings). `extern "C"` gives a clean unmangled symbol name (`utinni_clr_stop` exports as exactly `utinni_clr_stop`, no `?utinni_clr_stop@@YAXXZ` decoration), so `[DllImport("UtinniCore", EntryPoint = "utinni_clr_stop")]` works without mangle-decoded symbol strings.

**Linkage pattern to use in new test-only exports** (template — planner cites this verbatim):

```cpp
// In a new file UtinniCore/test_exports.cpp (or appended to existing utinni.cpp):

extern "C" __declspec(dllexport) void __cdecl utinni_clr_stop()
{
    clr::stop();
}

extern "C" __declspec(dllexport) uintptr_t __cdecl utinni_findPattern(
    const uint8_t* buffer, size_t bufferLen, const char* pattern, const char* mask)
{
    return utinni::memory::findPattern(buffer, bufferLen, pattern, mask);
}

extern "C" __declspec(dllexport) DWORD WINAPI utinni_init(LPVOID /*lpThreadParam*/)
{
    // C-01 fix surface — body extracted from utinni.cpp::main() lines 100-130 per RESEARCH.md.
    // Returns DWORD per LPTHREAD_START_ROUTINE so launcher's CreateRemoteThread can call it.
    main();
    return 0;
}
```

**Calling convention rationale:** Use `__cdecl` (default for free functions on x86 MSVC) — matches the CppSharp-generated `CallingConvention = CallingConvention.Cdecl` pattern at `UtinniCoreDotNet/Generated/UtinniCore.cs:20`. `WINAPI` (= `__stdcall`) only on `utinni_init` because `LPTHREAD_START_ROUTINE` mandates `__stdcall` (per Win32 contract).

**P/Invoke shape on the managed side** — copy from `UtinniCoreDotNet/Utility/Native.cs:67-77`:

```csharp
// In UtinniCoreDotNet.Tests/<TestFile>.cs:

using System.Runtime.InteropServices;
// ...

private static class NativeBridge
{
    [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "utinni_clr_stop")]
    public static extern void Utinni_ClrStop();

    [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl, EntryPoint = "utinni_findPattern")]
    public static extern uint Utinni_FindPattern(byte[] buffer, uint bufferLen, string pattern, string mask);
}
```

**Notes for the planner:**
- DLL name is `"UtinniCore"` (not `"UtinniCore.dll"`) — matches existing precedent at `UtinniCoreDotNet/Generated/UtinniCore.cs:20`. The CLR appends `.dll` automatically.
- `CallingConvention = CallingConvention.Cdecl` — matches existing pattern; do NOT use `Winapi`/`StdCall`.
- `EntryPoint` set explicitly — defends against future C# method-name refactors.
- Wrap in a private nested static class (`NativeBridge`) inside the test class — keeps the harness self-contained, no public surface bleed.

**Export visibility:** Phase 2 must NOT touch `UtinniCore-Symbols.vcxproj` (the CppSharp-side mangled-symbol surface) — the new `extern "C"` exports live in `UtinniCore.vcxproj` directly. No `.def` file exists in the repo (verified — `Glob UtinniCore/**/*.def` returned zero hits). Continue using `__declspec(dllexport)` per the established pattern at `utinni.h:42-46` and `utinni_plugin.h:50`.

---

## Section 5 — Files that ARE their own pattern (no analog mapping needed)

These are the **modified-in-place** source files getting C-NN fix patches. Per the planner's `<read_first>` convention: the planner cites the file path + line range directly (not a separate analog). The pattern to follow is the existing code's local style.

| C-NN | File (modify in place) | Line range | Pattern source |
|------|------------------------|------------|----------------|
| C-01 | `UtinniCore/utinni.cpp` | `:99-130` (`main`), `:138-151` (`DllMain`) | self — extract main body to `utinni_init` export, slim DllMain to `DisableThreadLibraryCalls + return TRUE` |
| C-01 | `Launcher/main.cpp` | `:174-210` (`inject`) | self — chase `LoadLibraryA` `CreateRemoteThread` with a second `CreateRemoteThread` against `utinni_init` per RESEARCH path (a) |
| C-02 | `UtinniCore/swg/misc/config.cpp` | `:59-76` (`hkLoadOverrideConfig`) | self — remove `delete[] data` at `:71`; trust file dtor `:72` (per RESEARCH §C-02) |
| C-03 | `UtinniCore/swg/misc/network.cpp` | `:65-69` (`Network::cast`) | self — initialize `swgptr networkId = 0;`, return `networkId` (the OUT param), not function return |
| C-04 | `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs` | `:97-106` (`DequeuePostDrawLoopCalls`) | self — fix `preDrawLoopCallQueue` typo to `postDrawLoopCallQueue`; extract `Drain(ConcurrentQueue<Action>)` helper; apply to `:75-95` and to `GameCallbacks.cs:100-120` and `ObjectCallbacks.cs` (file:line per RESEARCH) |
| C-05 | `UtinniCoreDotNet/UI/GameDragDropEventHandlers.cs` | `:33-44` + call site `UI/Controls/PanelGame.cs:68` | self — convert static delegate fields to `static event` + forwarder-lambda pattern (per RESEARCH §C-05) |
| C-06 | `UtinniCoreDotNet/PluginFramework/PluginLoader.cs` | `:39-73` (`Load`) | self — refactor single `AggregateCatalog` to per-plugin loop with try/catch (per RESEARCH §C-06); also accepts optional `pluginDir` parameter as testability seam |
| C-07 | `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs` | `:60-74` (`AddUndoCommand`), `:76-87` (`Undo`), `:97-108` (`Redo`), `:50` (ctor) | self + Phase 1 D-06 seam — add `private readonly object syncRoot = new object();`, lock all stack mutation; call `Peek().AllowMerge()` before `Peek().Merge()`; move `RedoCommands.Clear()` after merge check (TD-29); inject `Action<Action> registerCleanupCallback = null` ctor param defaulting to `GameCallbacks.AddCleanupSceneCall` |
| C-07 | `UtinniCoreDotNet/UndoRedo/IUndoCommand.cs` | `:36` | self — DISPOSITION: keep `AllowMerge()` in interface per RESEARCH §C-07 (`docs/ai/undo-redo.html:54-55` contract is intentional) |
| C-08 | `UtinniCoreDotNet/Hotkeys/Hotkey.cs` | `:66-92` (`ProcessString`) | self — replace `Enum.Parse` with `Enum.TryParse`; split-on-`+` then flag-combine all-but-last as modifiers; warn-and-disable on failure (per RESEARCH §C-08) |
| C-08 | `UtinniCoreDotNet/Hotkeys/HotkeyManager.cs` | `:106-114` (`Load`) | self — affected by same root cause (FR-07); fix flows through `Hotkey.UpdateKeys(string)` once `ProcessString` is fixed |
| C-08 | `UtinniCoreDotNet.Tests/HotkeyTests.cs` | `:49-66` (two `[Skip = "C-08:..."]` markers) | self — remove `Skip = ...` parameter, leaving `[Fact]` / `[Theory]` bare; tests flip green |
| C-09 | `UtinniCoreDotNet/UI/Forms/FormMain.cs` | `:57-78` (WndProc busy-wait) | self — replace `Thread.Sleep(1)` spin with `ManualResetEventSlim.WaitOne(timeoutMs)`; injectable signaller per RESEARCH §C-09 |
| C-09 | `UtinniCore/swg/graphics/directx9.cpp` | `:216-226` | self — emit `SetEvent` on present-block transition; export `HANDLE getPresentBlockedEvent()` per VALIDATION Wave 0 |
| C-10 | `UtinniCore/clr.cpp` | `:93-102` (`clr::stop`) | self — guard `pClr->Release()` with null check; set `pClr = nullptr` after release (per RESEARCH §C-10) |
| C-10 | `UtinniCore/utinni.cpp` | `:132-136` (`detatch`) | self — already calls `clr::stop()`; preserve typo `detatch` (Phase 6 cleanup) |
| C-11 | `UtinniCore/swg/graphics/directx9.cpp` | `:297-303` (`getVtbl`) | self — null-check `findPattern` result before `memcpy`; bail with `utinni::log::critical(...)`; preserve CON-N-04 VirtualProtect bracket per RESEARCH §C-11 |
| C-12 | `sdk/UtinniPluginTemplates/Vsix/source.extension.vsixmanifest` | `:9-11,17` | self — change `Version="[16.0,17.0)"` → `Version="[16.0,18.0)"` on all four `<InstallationTarget>` / `<Prerequisite>` lines (verified file content `Vsix/source.extension.vsixmanifest:9-17`) |
| C-13 | `UtinniPlugins/The Jawa Toolbox/TheJawaToolbox/TheJawaToolbox.vcxproj` | `:63` (cross-repo) | self — `..\..\..\..\` → `..\..\..\`; also restore `Debug\|Win32.Build.0` entry in sister-repo `.sln` per CONTEXT.md D-09 |
| C-14 | `data/utinni.cfg` | `:4-5` | self — set `loginServerAddress0=` and `loginServerPort0=` to blank per CON-D-01 / RESEARCH §C-14 |
| C-15 | `UtinniCoreDotNetGen/Program.cs` | `:39-41` | self — extract `ResolveSlnDir(string[] args, IDictionary envVars, string executablePath)` pure function with three resolution modes (per RESEARCH §C-15); production call site stays at top of `Main` |
| C-16 | `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` | `:46` (comment) + audit `:39-43` + `GroundSceneCallbacks.cs:38-41` + `ObjectCallbacks.cs:36` | self — rewrite `:46` comment to cite CLR P/Invoke delegate-marshalling semantics (per RESEARCH §C-16); audit confirms field-anchoring already covers existing sites; add new test |
| KB-05 | `UtinniCore/swg/game/game.cpp` | `:305-308` (`isSafeToUse`) | self — change `\|\|` to `&&` per `docs/ai/internals.md:218-231` (default-fallback per D-12); add `// ToDo` if archaeology surfaces a different answer |

**Planner usage:** for each row above, the task's `<read_first>` block cites the file path + line range (`UtinniCore/utinni.cpp:99-151` for C-01) and the executor reads the existing code as its own pattern. No analog lookup needed because the file IS its own pattern.

---

## Section 6 — Cross-cutting code conventions (apply to ALL new files)

These shared patterns from `01-PATTERNS.md` §S-1..S-7 are unchanged in Phase 2:

### S-1: MIT License Header (23 lines, opens `/**`, closes `**/`)
**Source:** `UtinniCoreDotNet/Hotkeys/Hotkey.cs:1-23` (identical in every `.cs/.cpp/.h` in the repo).
**Apply to:** every new `.cs`, `.cpp`, `.h` file in Phase 2 (all 14 test files, both fixture Plugin.cs files, the LoaderLockHarness `main.cpp`, any new test-exports `.cpp`).
**Do NOT apply to:** `.csproj`, `.vcxproj`, `.sln`, `.cfg`, `.vsixmanifest`, `.lock.json` files (verified — existing project/data files don't carry the header).
**Verbatim:** copy bytes-for-bytes from `Hotkey.cs:1-23`. Do not retype. Do not edit `Copyright (c) 2020 Philip Klatt` (CONVENTIONS.md §File Headers — fork preserves upstream authorship).

### S-2: Allman Braces + 4-space Indent
**Source:** every `.cs`/`.cpp` in repo (e.g. `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:30-118`, `Launcher/main.cpp:174-210`).
**Apply to:** every new source file.
**Form:** opening brace on its own line; 4 spaces per nesting level; never tabs.

### S-3: Using-Block Ordering (C#)
**Source:** `UtinniCoreDotNet.Tests/HotkeyTests.cs:25-27` (`System.Windows.Forms` → `Xunit` → `UtinniCoreDotNet.Hotkeys`).
**Order:** `System.*` → third-party (`Xunit`, `System.Runtime.InteropServices`) → `UtinniCore.*` → `UtinniCoreDotNet.*` → aliases last.

### S-4: PascalCase public, camelCase private/local, no `_` prefix
**Source:** `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:34-39` (`private readonly Action onUpdateCommandsCallback;` — camelCase, no underscore).
**Apply to:** every new C# file. Test method names PascalCase (`[Method]_[Scenario]_[ExpectedOutcome]`); locals camelCase (`var hk = ...`).

### S-5: CRLF line endings throughout
**Source:** all repo files (Phase 1 D-11).
**Apply to:** every new file. Watch for editors that default to LF — verify via `git status` for "LF will be replaced by CRLF" warnings.

### S-6: Flat-root project layout
**Source:** `Utinni.sln:6-28` (every project entry is `<Name>\<Name>.{csproj,vcxproj}` directly under repo root).
**Apply to:** `Utinni.LoaderLockHarness/` sits at repo root, NOT under `tests/` or `harness/`. Fixture csprojs sit under `UtinniCoreDotNet.Tests/Fixtures/<Name>/` per VALIDATION.md Wave 0 (acceptable nesting — fixtures are owned by the test project, not standalone solution citizens).

### S-7: No try/catch in production C# (test code excepted)
**Source:** CONVENTIONS.md §Error Handling §C# Patterns.
**Apply to (negative):** in test code, use `Record.Exception(() => ...)` for "did it throw" assertions (NOT try/catch). See `HotkeyTests.cs:62-66`. In **C-06 production code**, the per-plugin try/catch IS the fix — this is the explicit exception per RESEARCH §C-06 (the existing convention is broken precisely because exceptions are unhandled).

### Additional Phase-2 conventions

**P/Invoke calling convention:**
- All Phase-2 new test-only exports use `__cdecl` (matches `UtinniCoreDotNet/Generated/UtinniCore.cs:20` precedent).
- Managed side: `CallingConvention = CallingConvention.Cdecl` + explicit `EntryPoint = "..."` (matches `Native.cs:67-77` shape).
- Exception: `utinni_init` uses `WINAPI` (= `__stdcall`) because `LPTHREAD_START_ROUTINE` mandates it.

**Comment style:**
- Inline comments use `// ToDo` (capital T, capital D — per CONVENTIONS.md §Comments §ToDo Tag).
- Defect-ID prefixes use `// C-NN:` (capital C, digit, colon) — distinct from `ToDo`, acceptable per `01-PATTERNS.md`.
- Do NOT use `// TODO`, `// FIXME`, `// HACK`, `// XXX`.

**xUnit version:** 2.9.3 only (pinned in `UtinniCoreDotNet.Tests.csproj:19`). Do NOT pull `xunit 3.x` — net472 incompatible.

**No `.runsettings`, no coverage tooling, no analyzer-rule `.editorconfig`:** all Phase 6 territory.

**No new NuGet packages:** every Phase-2 fix uses net472 BCL primitives (`ManualResetEventSlim`, `GCHandle`, `Process`, `EventWaitHandle`, `System.Xml.Linq`). The only conditional package bump is `Microsoft.VisualStudio.SDK` for C-12 widening (already a `<PackageReference>` in `sdk/UtinniPluginTemplates/Vsix/Vsix.csproj:74`).

---

## Section 7 — Verbatim code excerpts (key analogs the planner cites)

### Excerpt 1: 23-line MIT header (copy bytes-for-bytes)
**Source:** `UtinniCoreDotNet/Hotkeys/Hotkey.cs:1-23`

```
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

### Excerpt 2: Canonical xUnit `[Fact]` shape
**Source:** `UtinniCoreDotNet.Tests/HotkeyTests.cs:33-39`

```csharp
[Fact]
public void Ctor_StringConstructor_SingleKey_SetsKeyAndNoModifier()
{
    var hk = new Hotkey("test", "test", "F1", () => { }, overrideGameInput: false);
    Assert.Equal(Keys.None, hk.ModifierKeys);
    Assert.Equal(Keys.F1, hk.Key);
}
```

### Excerpt 3: Canonical xUnit `[Theory]` + `[InlineData]` shape (for parameterized tests like C-15 ResolveSlnDir)
**Source:** `UtinniCoreDotNet.Tests/HotkeyTests.cs:49-58`

```csharp
[Theory(Skip = "...")]
[InlineData("Shift + Alt + Z", Keys.Shift, Keys.Alt | Keys.Z)]
public void Ctor_StringConstructor_MultiModifierChord_ParsesFlags(string combo, Keys expectedMods, Keys expectedKey)
{
    var hk = new Hotkey("test", "test", combo, () => { }, overrideGameInput: false);
    Assert.Equal(expectedMods, hk.ModifierKeys);
    Assert.Equal(expectedKey, hk.Key);
}
```

### Excerpt 4: Skip-with-comment pattern (for tasks that need a red test → green flip)
**Source:** `UtinniCoreDotNet.Tests/HotkeyTests.cs:60-66`

```csharp
[Fact(Skip = "C-08: expected to fail until Phase 2 fix lands (Enum.TryParse refactor on Hotkey.cs:82,91). " +
              "When unskipped, this asserts that malformed input like 'Ctrl + T' (note 'Ctrl' is not a valid Keys enum name - should be 'Control') is gracefully handled instead of throwing ArgumentException.")]
public void Ctor_StringConstructor_MalformedInput_DoesNotThrow()
{
    var ex = Record.Exception(() => new Hotkey("test", "test", "Ctrl + T", () => { }, overrideGameInput: false));
    Assert.Null(ex);
}
```

### Excerpt 5: `Record.Exception` "did it throw" idiom (use INSTEAD of try/catch)
**Source:** `UtinniCoreDotNet.Tests/HotkeyTests.cs:64-65`

```csharp
var ex = Record.Exception(() => new Hotkey(...));
Assert.Null(ex);   // OR: Assert.IsType<ArgumentException>(ex);
```

### Excerpt 6: Hand-written `[DllImport]` shape (the Phase-2 P/Invoke template)
**Source:** `UtinniCoreDotNet/Utility/Native.cs:67-77`

```csharp
[DllImport("user32.dll", EntryPoint = "ReleaseCapture")]
public static extern void ReleaseCapture();

[DllImport("user32.dll", EntryPoint = "SendMessage")]
public static extern void SendMessage(System.IntPtr hWnd, int wMsg, int wParam, int lParam);
```

**Adapt for Phase 2:** swap `"user32.dll"` → `"UtinniCore"`, add `CallingConvention = CallingConvention.Cdecl`, swap `EntryPoint = "..."` to the new `utinni_xxx` symbol name.

### Excerpt 7: Auto-generated `[DllImport]` shape (for `CallingConvention`/`EntryPoint` form reference only — don't copy mangled symbol names)
**Source:** `UtinniCoreDotNet/Generated/UtinniCore.cs:19-22`

```csharp
[SuppressUnmanagedCodeSecurity]
[DllImport("UtinniCore", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.Cdecl,
    EntryPoint="?getPath@utinni@@YAABV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@XZ")]
internal static extern global::System.IntPtr GetPath();
```

**Adapt for Phase 2:** use a clean unmangled `EntryPoint` (`"utinni_clr_stop"`, not the mangled form). `[SuppressUnmanagedCodeSecurity]` optional — only needed if hot-path performance matters; skip for test code.

### Excerpt 8: `extern "C" __declspec(dllexport)` linkage pattern
**Source:** `UtinniCore/plugin_framework/utinni_plugin.h:50`

```cpp
#define UTINNI_PLUGIN extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin()
```

**Adapt for Phase 2:**
```cpp
extern "C" __declspec(dllexport) void __cdecl utinni_clr_stop()
{
    clr::stop();
}
```

### Excerpt 9: `Launcher/main.cpp` LoadLibrary pattern (for the C-01 harness)
**Source:** `Launcher/main.cpp:181-186`

```cpp
std::string dllFilename = currentDir + "UtinniCore.dll";
LPVOID lpMemory = (LPVOID)VirtualAllocEx(procInfo.hProcess, nullptr, dllFilename.length(), MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
WriteProcessMemory(procInfo.hProcess, lpMemory, (LPVOID)dllFilename.c_str(), dllFilename.length(), nullptr);

LPVOID lpLoadLibraryA = (LPVOID)GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
HANDLE hThread = CreateRemoteThread(procInfo.hProcess, nullptr, 0, (LPTHREAD_START_ROUTINE)lpLoadLibraryA, lpMemory, 0, nullptr);
```

**Phase-2 harness simplification:** drops the cross-process `VirtualAllocEx`/`WriteProcessMemory`/`CreateRemoteThread` — the harness runs `LoadLibraryA("UtinniCore.dll")` in-process and times the call. The Launcher's actual injection pattern stays the source-of-truth for C-01's launcher-side change (path a: chase the `LoadLibrary` `CreateRemoteThread` with a second `CreateRemoteThread` against `utinni_init`).

### Excerpt 10: `Launcher.vcxproj` C++ console-exe template
**Source:** `Launcher/Launcher.vcxproj:1-157` (full file — see Section 3 for deltas)

Key load-bearing lines:
- `Launcher.vcxproj:3-15` — three-config matrix (`Debug|Win32`, `Release|Win32`, `RelWithDbgInfo|Win32`).
- `Launcher.vcxproj:18` — `<VCProjectVersion>16.0</VCProjectVersion>` (VS 2019; works in VS 2022 too — C-12 widening makes this explicit).
- `Launcher.vcxproj:21` — `<WindowsTargetPlatformVersion>10.0</WindowsTargetPlatformVersion>` (Windows 10 SDK, picked dynamically).
- `Launcher.vcxproj:27,33,41` — `<PlatformToolset>v142</PlatformToolset>` per config.
- `Launcher.vcxproj:63,67,71` — `<OutDir>$(SolutionDir)bin\$(Configuration)\</OutDir>` (matches all other native projects).
- `Launcher.vcxproj:79,103,125` — `<AdditionalIncludeDirectories>$(SolutionDir);$(SolutionDir)external;$(ProjectDir);` (standard include cone).
- `Launcher.vcxproj:80,102,124` — `<LanguageStandard>stdcpp17</LanguageStandard>` (C++17).
- `Launcher.vcxproj:83,107,129` — `<SubSystem>Console</SubSystem>` (the harness needs Console so stdout is visible; `Launcher/main.cpp:45` overrides to `/SUBSYSTEM:windows` via pragma — harness should NOT do this).

### Excerpt 11: Fixture IPlugin source shape
**Source:** `sdk/UtinniPluginTemplates/DotNetPluginTemplate/Plugin.cs:1-25` + `UtinniCoreDotNet/PluginFramework/IPlugin.cs:30-49`

Template pattern (rewritten without the Vsix `$placeholder$` substitutions):
```csharp
// [23-line MIT header here]

using UtinniCore.Utinni;
using UtinniCoreDotNet.PluginFramework;
using UtinniCoreDotNet.Utility;

namespace UtinniCoreDotNet.Tests.Fixtures.BrokenPlugin
{
    public class BrokenPlugin : IPlugin
    {
        public BrokenPlugin()
        {
            throw new System.InvalidOperationException("BrokenPlugin throws on construction (C-06 fixture).");
        }

        public PluginInformation Information { get; }
        public UtINI GetConfig() => null;
    }
}
```

`IPlugin` interface contract (verbatim from `UtinniCoreDotNet/PluginFramework/IPlugin.cs:44-49`):
```csharp
[InheritedExport(typeof(IPlugin))]
public interface IPlugin
{
    PluginInformation Information { get; }
    UtINI GetConfig();
}
```

The `[InheritedExport]` attribute means MEF's `DirectoryCatalog` discovers ANY class implementing `IPlugin` — no per-fixture `[Export(typeof(IPlugin))]` needed.

---

## Section 8 — No Analog Found (flag for planner)

| File | Role | Reason | Mitigation |
|------|------|--------|------------|
| `UtinniCoreDotNet.Tests/Fixtures/BrokenPlugin/BrokenPlugin.csproj` | Fixture csproj | No in-tree plugin csprojs compile against local `UtinniCoreDotNet.csproj`; sister-repo `UtinniPlugins` is out-of-tree; the Vsix template (`sdk/UtinniPluginTemplates/DotNetPluginTemplate/ProjectTemplate.csproj`) is a Vsix-substituted template, not a compileable artifact | Use the from-scratch SDK-style template in Section 2; cite `sdk/.../ProjectTemplate.csproj` and `Plugin.cs` as shape references |
| `UtinniCoreDotNet.Tests/Fixtures/GoodPlugin/GoodPlugin.csproj` | Fixture csproj | Same as BrokenPlugin | Same as BrokenPlugin (identical csproj, only `AssemblyName` differs) |

**Note:** The C++ harness has a perfect analog (`Launcher/`); the fixture csprojs do not. The planner should treat the Section 2 template as the from-scratch authoritative shape, with explicit citation that no precedent exists.

---

## Section 9 — Metadata

**Analog search scope:** `D:\Code\Utinni\` (excluding `external/`, `bin/`, `obj/`, `.git/`, `.planning/`, `UtinniCoreDotNet.Tests/obj/`).

**Files Read during analog extraction:**
- `.planning/phases/02-critical-bug-burn-down-c-01-c-15/02-CONTEXT.md` (lines 1-204, full file)
- `.planning/phases/02-critical-bug-burn-down-c-01-c-15/02-RESEARCH.md` (lines 1-400 — fix-shape sections C-01..C-08 inclusive; rest skimmed via Grep)
- `.planning/phases/02-critical-bug-burn-down-c-01-c-15/02-VALIDATION.md` (lines 1-137, full file — Wave 0 list extraction)
- `.planning/phases/01-ci-tier-1-c-scaffold/01-PATTERNS.md` (lines 1-491, full file — Phase 1 pattern carry-forward)
- `.planning/codebase/CONVENTIONS.md` (lines 1-120, header/style/naming sections)
- `UtinniCoreDotNet.Tests/HotkeyTests.cs` (lines 1-68, full file — universal xUnit analog)
- `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` (lines 1-30, full file — test csproj precedent)
- `UtinniCoreDotNet/Hotkeys/Hotkey.cs` (header range — verified MIT header source)
- `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs` (lines 1-117, full file — C-04 subject)
- `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` (lines 1-145, full file — C-16 subject + delegate-anchoring pattern)
- `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs` (lines 1-119, full file — C-07 subject + Phase-1 D-06 seam)
- `UtinniCoreDotNet/PluginFramework/PluginLoader.cs` (lines 1-75, full file — C-06 subject)
- `UtinniCoreDotNet/PluginFramework/IPlugin.cs` (lines 1-51, full file — IPlugin contract for fixtures)
- `UtinniCoreDotNet/Utility/Native.cs` (lines 1-80, full file — hand-written `[DllImport]` analog)
- `UtinniCoreDotNet/Generated/UtinniCore.cs` (lines 1-100 — auto-generated `[DllImport]` analog)
- `UtinniCore/utinni.cpp` (lines 1-174, full file — C-01 subject + export precedent)
- `UtinniCore/utinni.h` (lines 1-60, full file — `UTINNI_API` macro definition)
- `UtinniCore/plugin_framework/utinni_plugin.h` (lines 1-52, full file — `extern "C" __declspec(dllexport)` precedent)
- `UtinniCore/swg/misc/config.cpp` (lines 50-80 — C-02 surface)
- `Launcher/main.cpp` (lines 1-401, full file — C-01 launcher + harness analog)
- `Launcher/Launcher.vcxproj` (lines 1-157, full file — C++ console-exe vcxproj analog)
- `sdk/UtinniPluginTemplates/DotNetPluginTemplate/ProjectTemplate.csproj` (lines 1-50, full file — plugin csproj shape reference)
- `sdk/UtinniPluginTemplates/DotNetPluginTemplate/Plugin.cs` (lines 1-25, full file — IPlugin source shape)
- `sdk/UtinniPluginTemplates/Vsix/source.extension.vsixmanifest` (lines 1-25, full file — C-12 fix surface)

**Greps run:**
- `extern "C"` in `UtinniCore/` → 1 hit (`plugin_framework/utinni_plugin.h:50`)
- `__declspec(dllexport)` in `UtinniCore/` → 2 hits (`utinni_plugin.h`, `utinni.h` — the macro definition)
- `DllImport` in `UtinniCoreDotNet/` → 30+ hits (Generated/* + Utility/Native.cs)

**Verified absent:**
- `D:\Code\Utinni\CLAUDE.md` (no project instructions file at repo root)
- `D:\Code\Utinni\.planning\codebase\PATTERNS.md` (no cross-phase codebase patterns file)
- `D:\Code\Utinni\.claude\skills\` and `D:\Code\Utinni\.agents\skills\` (no skill directories)
- `D:\Code\Utinni\UtinniCore\**\*.def` (no module-definition export files; exports come from `__declspec(dllexport)` only)

**Pattern extraction date:** 2026-05-16
