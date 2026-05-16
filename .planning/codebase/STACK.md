# Technology Stack

**Analysis Date:** 2026-05-16

Utinni is a **DLL-injection-based modding framework** for Pre-CU Star Wars Galaxies (SWGEmu). It is a polyglot Windows-only desktop project that ships:

- A native x86 C++ injector (`Launcher.exe`)
- A native x86 C++ payload DLL (`UtinniCore.dll`) that hosts the CLR
- A managed .NET WinForms shell (`UtinniCoreDotNet.dll`) loaded by that CLR
- A code-generator (`UtinniCoreDotNetGen.exe`) that emits the managed P/Invoke bridge from C++ headers via CppSharp

The whole solution targets **x86 only** because `SwgClient_r.exe` is a 32-bit binary.

## Languages

**Primary:**
- **C++17** — used by `UtinniCore`, `UtinniCore-Symbols`, `UtINI`, `Launcher`. C++ standard is pinned via `<LanguageStandard>stdcpp17</LanguageStandard>` in every `.vcxproj` (`UtinniCore/UtinniCore.vcxproj` line 82, `Launcher/Launcher.vcxproj` line 80, `UtINI/UtINI.vcxproj` line 83).
- **C# 7.3** — used by `UtinniCoreDotNet` and `UtinniCoreDotNetGen`. Language version is explicit in `UtinniCoreDotNet/UtinniCoreDotNet.csproj` line 44 (`<LangVersion>7.3</LangVersion>`).

**Secondary:**
- **x86 assembly** — naked-function shims for inline detour trampolines. See `__declspec(naked)` blocks in `UtinniCore/swg/graphics/shader.cpp` lines 45–56 (mid-pop-cell trampoline) and similar patterns across `swg/` for "before-call/after-call" detours.
- **Resource Compiler (RC)** — `Launcher/Launcher.rc`, `UtinniCore/UtinniCore.rc` for `VERSIONINFO` / icon embedding.
- **INI** — `data/ut.ini`, `data/utinni.cfg` for runtime configuration.
- **HTML/CSS/Markdown** — `docs/*.html`, `docs/style.css`, `docs/ai/*.md` documentation set.

## Runtime

**Native (target process — SWG client):**
- **Architecture:** x86 (32-bit Windows). Enforced by `<Platform>Win32</Platform>` on every C++ vcxproj and by SWG itself.
- **Subsystem:** Console for `UtinniCore.dll` (`UtinniCore.vcxproj` line 85: `<SubSystem>Console</SubSystem>`), Windows for `Launcher.exe` (set via `#pragma comment(linker, "/SUBSYSTEM:windows /ENTRY:mainCRTStartup")` in `Launcher/main.cpp` line 45).
- **CRT:** MSVC. `RelWithDbgInfo` explicitly uses `MultiThreadedDLL` (`UtinniCore.vcxproj` line 143).
- **Toolset:** **MSVC v142** (Visual Studio 2019). Hard-coded in every `.vcxproj` (`<PlatformToolset>v142</PlatformToolset>`).

**Managed (CLR loaded into the SWG process):**
- **.NET Framework 4.7.2** — `UtinniCoreDotNet/UtinniCoreDotNet.csproj` line 12 (`<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>`).
- **CLR version:** v4.0.30319, loaded explicitly via `mscoree.lib` in `UtinniCore/clr.cpp` line 51 (`pClrMetaHost->GetRuntime(L"v4.0.30319", ...)`).
- **AppDomain entry:** `pClrRuntimeHost->ExecuteInDefaultAppDomain(...UtinniCoreDotNet.dll, "UtinniCoreDotNet.Startup", "EntryPoint", ...)` — `UtinniCore/clr.cpp` line 117.
- **PlatformTarget:** `x86` for `UtinniCoreDotNet`, `AllowUnsafeBlocks=true` (csproj lines 25, 35, 43). 32-bit-only is non-negotiable because the host process is 32-bit.
- **WinForms** — Application shell is `System.Windows.Forms` (loaded via `Application.EnableVisualStyles()`, `Application.Run(new FormMain(...))` in `UtinniCoreDotNet/main.cs` lines 44, 59).

**Generator (build-time only):**
- **AnyCPU / x64** — `UtinniCoreDotNetGen` (`UtinniCoreDotNetGen/UtinniCoreDotNetGen.csproj` line 17: `<PlatformTarget>x64</PlatformTarget>`). The generator runs CppSharp at build time and emits managed bindings; it does not link into the runtime payload.

**Target Process:**
- **Star Wars Galaxies client** — `SwgClient_r.exe` (Pre-CU SWGEmu or SWG-Source variant). Validated by `Launcher/main.cpp` lines 280–293 via Win32 `GetFileVersionInfo`/`VerQueryValue` against `ProductName == "Star Wars Galaxies"`.

## Package Manager

- **None at the source level.** All third-party C++ libraries are **vendored under `external/`** (`external/CppSharp/`, `external/DetourXS/`, `external/ImGuizmo/`, `external/LeksysINI/`, `external/imgui/`, `external/nvapi/`, `external/spdlog/`).
- **NuGet** is used for one project only — the optional Visual Studio extension scaffolding `sdk/UtinniPluginTemplates/Vsix/Vsix.csproj` references `Microsoft.VisualStudio.SDK 16.0.206` and `Microsoft.VSSDK.BuildTools 16.8.3038` (lines 74–75). No `packages.config` or `<PackageReference>` exists in `UtinniCoreDotNet`, `UtinniCoreDotNetGen`, or `UtinniCore`.

**Lockfile:** Not detected (the only NuGet-using project does not pin a lockfile).

## Frameworks

**UI:**
- **Windows Forms (System.Windows.Forms)** — managed editor shell. See `UtinniCoreDotNet/UI/Forms/FormMain.cs`, `UtinniCoreDotNet/UI/Forms/FormHotkeyEditor.cs`, `UtinniCoreDotNet/UI/Controls/*.cs`.
- **Dear ImGui v1.76** — in-game immediate-mode GUI overlay rendered through the hooked Direct3D 9 device. Version stamped at `external/imgui/imgui.h` line 1 (`// dear imgui, v1.76`). DirectX backend at `external/imgui/imgui_impl_dx9.{h,cpp}`. Custom integration in `UtinniCore/swg/ui/imgui_impl.{h,cpp}`.
- **ImGuizmo v1.61 WIP** — 3D translate/rotate gizmo overlay built on top of ImGui. Version stamped at `external/ImGuizmo/ImGuizmo.h` line 2.

**Rendering:**
- **Microsoft Direct3D 9** — the only graphics API. Headers `<d3d9.h>`, `<d3d9types.h>`, `<d3dx9.h>` included throughout `UtinniCore/swg/graphics/` (`directx9.cpp` lines 26-27, `depth_texture.cpp` lines 28-29). DirectX 9 SDK (June 2010) is referenced by the `RelWithDbgInfo` configuration in `UtinniCore/UtinniCore.vcxproj` lines 72-73 (`C:\Program Files (x86)\Microsoft DirectX SDK (June 2010)\Include\` and `Lib\x86\`).
- **NVAPI** — used for INTZ depth-buffer access on Nvidia hardware. Linked via `#pragma comment(lib, "nvapi/x86/nvapi.lib")` in `UtinniCore/swg/graphics/depth_texture.cpp` line 32. SDK at `external/nvapi/`.

**Logging:**
- **spdlog 1.6.0** — `external/spdlog/`. Version pinned at `external/spdlog/version.h` lines 6-8 (`SPDLOG_VER_MAJOR 1`, `_MINOR 6`, `_PATCH 0`). Built with `SPDLOG_NO_EXCEPTIONS` (`UtinniCore.vcxproj` line 79). Custom sink at `UtinniCore/utility/log.cpp` lines 32–58.

**Function Hooking:**
- **DetourXS 1.0** — vendored at `external/DetourXS/`. Author "Sinner" (per file header `external/DetourXS/detourxs.h` lines 1-8). Drives every runtime function-pointer detour in `UtinniCore/swg/*/`. Compiled directly into UtinniCore via `<ClCompile Include="..\external\DetourXS\detourxs.cpp" />` (`UtinniCore.vcxproj` lines 160-161).

**INI Parsing:**
- **LeksysINI** — vendored header-only library at `external/LeksysINI/iniparser.hpp`. Wrapped by the `UtINI` static lib (`UtINI/utini.cpp` line 26: `#include "LeksysINI/iniparser.hpp"`). README marks this as "Temporary, will most likely be replaced soon."

**Binding Generation:**
- **CppSharp** (Mono CppSharp, Clang-frontend C# code emitter). Vendored binaries at `external/CppSharp/lib/CppSharp.{dll,AST.dll,Generator.dll,Parser.dll,Parser.CLI.dll,Runtime.dll,CppParser.dll}`. Referenced from `UtinniCoreDotNetGen/UtinniCoreDotNetGen.csproj` lines 50-73 with `<HintPath>..\external\CppSharp\lib\CppSharp.dll</HintPath>` etc.

**Plugin Discovery:**
- **MEF (System.ComponentModel.Composition)** — managed plugin discovery. Imported in `UtinniCoreDotNet.csproj` line 50 (`<Reference Include="System.ComponentModel.Composition" />`). Usage: `UtinniCoreDotNet/PluginFramework/PluginLoader.cs` lines 36, 49 (`[ImportMany(typeof(IPlugin))]`, `new AggregateCatalog(new DirectoryCatalog(pluginDir))`).

**Testing:**
- **Not detected.** There are no xUnit, NUnit, MSTest, Catch2, GoogleTest, or doctest references anywhere in the solution. No `*.Tests.*` projects, no `[Test]`/`[Fact]` attributes, no `TEST_CASE` macros. The project relies on manual in-game verification.

**Build:**
- **MSBuild / Visual Studio 2019 (16.0)** — `Utinni.sln` line 3 (`# Visual Studio Version 16`).
- Configurations: `Debug|x86`, `Release|x86`, `RelWithDbgInfo|x86` (`Utinni.sln` lines 31-33). The custom `RelWithDbgInfo` config is optimized but ships full PDBs (see `UtinniCore.vcxproj` lines 127-152).

## Key Dependencies

**Critical (runtime, ship in `bin/<Config>/`):**
- **`UtinniCore.dll`** — the payload DLL. Hosts detours, ImGui, plugin manager, and the CLR loader.
- **`UtinniCoreDotNet.dll`** — the managed bridge + editor shell. Loaded by `UtinniCore`'s embedded CLR.
- **`UtinniCore-Symbols.dll`** — exports a tiny set of MSVC-mangled `std::basic_string` ctors/dtors so the managed P/Invoke layer can call them by mangled name. Produced from a one-file template-instantiation project (`UtinniCore-Symbols/Std-symbols.cpp` lines 7-11). Without this DLL, every `std::string` round-trip across the managed boundary breaks.
- **`Launcher.exe`** — the injector executable that spawns SWG suspended, patches the entry point, and `LoadLibrary`'s `UtinniCore.dll` into it.
- **`UtINI.lib`** — static lib statically linked into both `UtinniCore.dll` and `Launcher.exe`. Provides `utinni::UtINI` (the `.ini` wrapper around LeksysINI).

**Critical (vendored third-party):**
- **CppSharp** — used at *build* time by `UtinniCoreDotNetGen.exe` to regenerate `UtinniCoreDotNet/Generated/UtinniCore.cs` (~16,658 lines) and `Generated/StdEdited.cs` (~535 lines) from C++ headers. License: MIT (per `licenses.txt`).
- **Dear ImGui v1.76** — license: MIT.
- **ImGuizmo v1.61 WIP** — license: MIT.
- **DetourXS 1.0** — license terms in `licenses.txt` (custom but permissive).
- **LeksysINI** — license: see `external/LeksysINI/LICENSE`.
- **spdlog 1.6.0** — license: MIT.
- **NVAPI** — Nvidia proprietary, used per their "commercial item" license terms in `external/nvapi/nvapi.h` lines 1-37.

**Critical (system / OS):**
- **`mscoree.lib` / `metahost.h`** — Windows CLR hosting API. `#pragma comment(lib, "mscoree.lib")` in `UtinniCore/clr.cpp` line 32. Calls: `CLRCreateInstance`, `ICLRMetaHost`, `ICLRRuntimeInfo`, `ICLRRuntimeHost::ExecuteInDefaultAppDomain` (`clr.cpp` lines 47–117).
- **`version.lib`** — for `GetFileVersionInfo` / `VerQueryValue` validation of the SWG client. `#pragma comment(lib, "version.lib")` in `Launcher/main.cpp` line 46.
- **`kernel32.dll`** — for the injection primitives: `CreateProcess(... CREATE_SUSPENDED)`, `VirtualAllocEx`, `WriteProcessMemory`, `CreateRemoteThread`, `LoadLibraryA`, `GetThreadContext` (`Launcher/main.cpp` lines 174–210, 305–356).
- **`user32.dll`** — P/Invoked from `UtinniCoreDotNet/Utility/Native.cs` lines 67-77: `ReleaseCapture`, `SendMessage`, `GetAsyncKeyState`, `CallWindowProc`. Used for custom title-bar hit-testing and forwarding messages into the SWG `WndProc`.

**Critical (managed BCL references — `UtinniCoreDotNet/UtinniCoreDotNet.csproj` lines 49-60):**
- `System` / `System.Core` / `Microsoft.CSharp`
- `System.ComponentModel.Composition` — MEF (plugin discovery)
- `System.Drawing`
- `System.ServiceModel`
- `System.Windows.Forms`
- `System.Xml`, `System.Xml.Linq`, `System.Data`, `System.Data.DataSetExtensions`
- `System.Net.Http`

## Configuration

**Runtime config files (in `bin/<Config>/`, copied from `data/` by the UtinniCore post-build step in `UtinniCore.vcxproj` lines 92, 123, 155):**

- **`ut.ini`** — primary runtime config. Sections (defaults seeded in `UtINI/utini.cpp` lines 30-55):
  - `[Launcher]` — `swgClientPath`, `swgClientName` (which `Launcher.exe` reads to know which SWG client EXE to spawn).
  - `[UtinniCore]` — `enableInternalUi`, `enableOfflineScenes`, `useSwgOverrideCfg`, `autoLoadScene`, `autoLogin`, `autoLoginUsername`.
  - `[Editor]` — `enableEditorMode`, `defaultPluginPanel`, `autoOpenLogWindow`, `width`, `height`.
  - `[Log]` — `writeClassName`, `writeFunctionName` (controls log prefix introspection in `UtinniCoreDotNet/Utility/Log.cs`).
  - `[Plugins]` — `plugin_00 = <enabled>, <DirectoryName>` priority-ordered list (parsed in `UtinniCore/plugin_framework/plugin_manager.cpp` lines 57-83).
- **`utinni.cfg`** — SWG client override config. Overrides `SwgClient_r.exe`'s own `swgclient_r.cfg` when `[UtinniCore] useSwgOverrideCfg=true`. See `data/utinni.cfg` for the shipped template (sections `[ClientGame]`, `[ClientTerrain]`, `[ClientUserInterface]`).
- **`<plugin-dir>/input.ini`** — per-plugin hotkey persistence, written by `HotkeyManager.Save()` (see `UtinniCoreDotNet/Hotkeys/HotkeyManager.cs`).
- **`utinni.log` / `utinni_previous.log`** — spdlog output. Path is `<install>/utinni.log`. Rotated by renaming current → previous on each launch (`UtinniCore/utility/log.cpp` lines 67-78).

**Build-time config:**
- **Preprocessor defines** for `UtinniCore` (per `UtinniCore.vcxproj` lines 79, 103, 134): `EXPORT_UTINNI` (toggles `__declspec(dllexport)` vs `dllimport` in `utinni.h` lines 42-46), `SPDLOG_NO_EXCEPTIONS`, plus standard `_DEBUG`/`NDEBUG`/`_CONSOLE`.
- **Preprocessor defines** for `Launcher` (per `Launcher.vcxproj` lines 77, 100, 122): `RELDBG` is set only in `RelWithDbgInfo` and gates the (currently-commented-out) Visual-Studio-DTE auto-attach code in `Launcher/main.cpp` lines 33-41, 345-349.
- **CppSharp generator options** (`UtinniCoreDotNetGen/Program.cs` lines 43-49): `TargetTriple = "i686-pc-win32-msvc"`, `UnityBuild = true`, `EnableRTTI = true`, plus defines `SPDLOG_NO_EXCEPTIONS`, `FMT_EXCEPTIONS=0`. Output goes to `UtinniCoreDotNet/Generated/`.

**Environment variables:**
- **`DXSDK_DIR`** — only required when building `RelWithDbgInfo`, points at the DirectX 9 SDK (June 2010) installation. Path is otherwise hard-coded as `C:\Program Files (x86)\Microsoft DirectX SDK (June 2010)\` in `UtinniCore.vcxproj` lines 72-73.

**Secrets:**
- No `.env`, `secrets.*`, or credential files present in the repo. The only "credentials" pattern is `[ClientGame] loginClientID=Local` in `data/utinni.cfg` line 11, which is a non-secret SWGEmu-side login alias.

## Platform Requirements

**Development:**
- **Windows 10 (or later)** with Windows 10 SDK 10.0.19041.0+. Set via `<WindowsTargetPlatformVersion>10.0</WindowsTargetPlatformVersion>` in every `.vcxproj`.
- **Visual Studio 2019 (16.x)** with workloads "C++ desktop development" and ".NET desktop development". The .NET 4.7.2 targeting pack must be installed.
- **DirectX 9 SDK (June 2010)** — required only for the `RelWithDbgInfo` configuration (otherwise unused at build time because `Release` and `Debug` rely on the Windows SDK's own `d3d9.h`).
- **(Optional) Microsoft.VisualStudio.SDK 16.x** — only required if building `sdk/UtinniPluginTemplates/Vsix/` to produce the plugin-template VSIX (`Vsix.csproj` line 74).
- **5–10 GB free disk** — VS + SDKs + intermediates.

**Production:**
- **Windows 10 / Windows 11** (32-bit or 64-bit host OS, since the SWG client runs under WoW64 on 64-bit Windows).
- **A Pre-CU SWG client** — typically the SWGEmu redistributable `SwgClient_r.exe`. The launcher refuses to inject into anything whose `VERSIONINFO.ProductName` is not `Star Wars Galaxies` (`Launcher/main.cpp` lines 280-292).
- **`.NET Framework 4.7.2` runtime** installed on the user's machine (ships with Windows 10 1803+).
- **An Nvidia GPU** is *not* required — the NVAPI depth-buffer path falls back to `D3DFMT_INTZ` queries when not on Nvidia (`UtinniCore/swg/graphics/depth_texture.cpp` line 66 and surrounding logic).

## Sibling Repository — UtinniPlugins

`D:/Code/UtinniPlugins` (referenced at `README.md` line 6) is the **official plugin repository** that consumes this framework. It is not part of this solution but is the canonical example of:
- A managed plugin DLL referencing only `UtinniCoreDotNet.dll`, dropped into `<install>/Plugins/<PluginName>/`.
- The "Jawa Toolbox" (`TJT.ico` resource embedded in `UtinniCoreDotNet/Resources/`) is the headline plugin shipped from that sibling repo.

The `sdk/UtinniPluginTemplates/` directory in *this* repo ships a VSIX that produces empty plugin projects suitable for `UtinniPlugins`.

---

*Stack analysis: 2026-05-16*
