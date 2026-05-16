# Codebase Structure

**Analysis Date:** 2026-05-16

## Directory Layout

```
D:/Code/Utinni/
├── Launcher/                       # Native Win32 EXE — suspended-process injector
│   ├── main.cpp                    # entry point + injection logic
│   ├── Launcher.rc                 # Win32 resource script
│   ├── resource.h
│   └── Launcher.vcxproj
│
├── UtINI/                          # Shared INI library (built into UtINI.dll/.lib)
│   ├── utini.cpp
│   ├── utini.h                     # `UTINNI_API class UtINI` — used by Launcher, Core, plugins
│   └── UtINI.vcxproj
│
├── UtinniCore/                     # Injected native DLL (the C++ core)
│   ├── utinni.cpp                  # DllMain → utinni::main → detours + CLR boot
│   ├── utinni.h                    # `UTINNI_API` namespace `utinni` getters
│   ├── clr.cpp                     # in-process CLR host (mscoree)
│   ├── clr.h
│   ├── UtinniCore.rc               # version info / resources
│   ├── resource.h
│   ├── UtinniCore.vcxproj
│   ├── plugin_framework/           # C++ plugin contract + loader
│   │   ├── plugin_manager.cpp      # scans Plugins/<name>/*.dll, calls createPlugin()
│   │   ├── plugin_manager.h
│   │   └── utinni_plugin.h         # base class + UTINNI_PLUGIN factory macro
│   ├── swg/                        # one folder per SWG client subsystem (shim layer)
│   │   ├── appearance/             # appearance, extent, particle, portal, skeleton
│   │   ├── camera/                 # camera, debug_camera
│   │   ├── client/                 # client (top-level SWG Client class)
│   │   ├── game/                   # game (lifecycle: install/quit/mainLoop/scene)
│   │   ├── graphics/               # graphics, directx9, depth_texture, post_processing, shader
│   │   ├── misc/                   # audio, config, crc_string, direct_input, io_win,
│   │   │                           # network, repository, swg_math, swg_memory, swg_misc,
│   │   │                           # swg_string, swg_utility, tree_file
│   │   ├── object/                 # object, client_object, creature_object, player_object
│   │   ├── scene/                  # scene, ground_scene, client_world, render_world,
│   │   │                           # terrain, world_snapshot
│   │   └── ui/                     # cui_* (game UI), imgui_impl, command_parser, controls/
│   │       └── controls/           # ui_* (per-widget CUI shims: button, list, grid, ...)
│   └── utility/                    # cross-cutting helpers
│       ├── log.cpp / log.h         # spdlog wrapper (utinni::log::info/...)
│       ├── memory.cpp / memory.h
│       ├── string_utility.cpp / string_utility.h
│       └── utility.cpp / utility.h
│
├── UtinniCore-Symbols/             # Companion C++ project — STL symbol stubs for CppSharp
│   ├── Std-symbols.cpp
│   └── UtinniCore-Symbols.vcxproj
│
├── UtinniCoreDotNetGen/            # Build-time CppSharp generator
│   ├── Program.cs                  # CppSharp.ILibrary that emits Generated/UtinniCore.cs
│   ├── App.config
│   ├── Properties/
│   └── UtinniCoreDotNetGen.csproj
│
├── UtinniCoreDotNet/               # Managed host (C# 7.3, .NET 4.7.2, x86)
│   ├── main.cs                     # Startup.EntryPoint (called by clr.cpp)
│   ├── Callbacks/                  # native→managed bridge (one static class per event family)
│   │   ├── CuiCallbacks.cs
│   │   ├── GameCallbacks.cs
│   │   ├── GroundSceneCallbacks.cs
│   │   ├── ImGuiCallbacks.cs
│   │   └── ObjectCallbacks.cs
│   ├── Commands/                   # built-in IUndoCommand implementations
│   │   └── WorldSnapshotCommands.cs
│   ├── Generated/                  # CppSharp output — DO NOT hand-edit UtinniCore.cs
│   │   ├── UtinniCore.cs           # P/Invoke surface (regenerated)
│   │   └── StdEdited.cs            # std::basic_string wrapper (hand-curated)
│   ├── Hotkeys/                    # rebindable chord-based hotkey system
│   │   ├── Hotkey.cs
│   │   └── HotkeyManager.cs        # persists to <plugin-assembly-dir>/input.ini
│   ├── PluginFramework/            # MEF-discovered plugin contracts
│   │   ├── IPlugin.cs              # [InheritedExport] runtime-only plugin
│   │   ├── IEditorPlugin.cs        # [InheritedExport] editor-UI plugin (extends IPlugin)
│   │   └── PluginLoader.cs         # DirectoryCatalog over Plugins/<name>/
│   ├── Properties/
│   │   ├── AssemblyInfo.cs
│   │   ├── Resources.Designer.cs
│   │   └── Resources.resx
│   ├── Resources/                  # PNG/ICO assets used by the editor chrome
│   ├── UI/                         # WinForms editor shell + control library
│   │   ├── Controls/               # Utinni* themed controls (button, combobox, slider, ...)
│   │   │   ├── PanelGame.cs        # the Panel that adopts SWG's HWND
│   │   │   ├── SubPanel.cs         # base for plugin sub-panels
│   │   │   ├── SubPanelContainer.cs
│   │   │   ├── CollapsiblePanel.cs
│   │   │   └── Utinni*.cs          # ~13 themed control wrappers
│   │   ├── Forms/                  # WinForms windows
│   │   │   ├── FormMain.cs         # the editor shell
│   │   │   ├── FormHotkeyEditor.cs / FormHotkeyEditorDialog.cs
│   │   │   ├── FormLog.cs
│   │   │   ├── UtinniForm.cs       # base form for the project (chromeless, themed)
│   │   │   └── IEditorForm.cs      # interface plugin forms must implement
│   │   ├── Theme/
│   │   │   ├── Colors.cs           # static palette
│   │   │   └── ThemeUtility.cs
│   │   └── GameDragDropEventHandlers.cs
│   ├── UndoRedo/                   # stack-based undo system
│   │   ├── IUndoCommand.cs
│   │   └── UndoRedoManager.cs
│   ├── Utility/                    # managed cross-cutting helpers
│   │   ├── Log.cs                  # spdlog sink + FormLog feed
│   │   └── Native.cs               # Win32 P/Invoke (CallWindowProc, WM_* consts, ...)
│   └── UtinniCoreDotNet.csproj
│
├── data/                           # build-output runtime data (committed)
│   ├── Icons/                      # editor chrome icons
│   ├── ut.ini                      # master config (Launcher + Core + Plugins)
│   └── utinni.cfg                  # SWG client.cfg override (loaded via detour)
│
├── docs/                           # documentation site (HTML + AI-readable mirror)
│   ├── index.html ... internals.html, plugin-framework.html, sdk.html, ...
│   ├── style.css
│   └── ai/                         # plain-markdown mirror (AI/grep-friendly)
│       ├── architecture.md, assessment.md, bridge.md, build.md, callbacks.md,
│       ├── core.md, glossary.md, hotkeys.md, index.md, injection.md, internals.md,
│       └── plugin-framework.md, regen-bindings.md, sdk.md, tutorial.md, ui-framework.md,
│           undo-redo.md, vision.md
│
├── external/                       # vendored third-party libs (committed source)
│   ├── CppSharp/                   # binding generator
│   ├── DetourXS/                   # function detours
│   ├── ImGuizmo/                   # 3D gizmo for ImGui
│   ├── LeksysINI/                  # INI parser (used by UtINI)
│   ├── imgui/                      # immediate-mode UI
│   ├── nvapi/                      # NVIDIA stereo/gpu queries
│   └── spdlog/                     # logging
│
├── sdk/                            # plugin author kit (separate solutions)
│   ├── UtinniCppPluginTemplate/    # standalone C++ plugin template
│   │   ├── UtinniCppPlugin.props   # .props consumed by template projects
│   │   ├── UtinniCppPluginTemplate.sln
│   │   └── UtinniCppPluginTemplate/
│   ├── UtinniPluginTemplates/      # Visual Studio template package (.vsix)
│   │   ├── DotNetEditorPluginTemplate/
│   │   ├── DotNetPluginTemplate/
│   │   ├── Vsix/
│   │   └── UtinniPluginTemplates.sln
│   └── examples/                   # reference plugin implementations
│       ├── ExampleCppPlugin/       # minimal UtinniPlugin + UTINNI_PLUGIN
│       └── ExampleEditorPlugin/    # minimal IEditorPlugin with one SubPanel
│
├── LICENSE                         # MIT
├── README.md                       # project overview + features + planned features
├── Utinni.sln                      # Visual Studio 2019 solution (Format 12.00, VS16)
└── licenses.txt                    # third-party license attributions
```

## Directory Purposes

**`Launcher/`:**
- Purpose: Build artifact `Launcher.exe`. The user-facing entry point.
- Contains: One C++ translation unit + Win32 resource + vcxproj.
- Key files: `Launcher/main.cpp`

**`UtINI/`:**
- Purpose: Standalone INI library so Launcher (pre-injection) and UtinniCore (post-injection) can share the same `ut.ini` reader without depending on each other's PDB. Re-exported to .NET via CppSharp.
- Contains: A single `UtINI` class with PIMPL hiding `LeksysINI::INI::File`.
- Key files: `UtINI/utini.h`, `UtINI/utini.cpp`

**`UtinniCore/`:**
- Purpose: The injected DLL. Everything that needs to run in-process inside SWG.
- Contains: Boot code (`utinni.cpp`, `clr.cpp`), the `swg/` shim catalog, native plugin loader, utility helpers.
- Key files: `UtinniCore/utinni.cpp`, `UtinniCore/clr.cpp`, `UtinniCore/plugin_framework/plugin_manager.cpp`

**`UtinniCore/swg/`:**
- Purpose: The "shim layer" — one folder per SWG client subsystem. Each `<feature>.cpp` declares the RVAs for that subsystem's native functions and exposes a typed `utinni::` API plus `detour()` / `patch()` hooks.
- Contains: 9 subfolders (`appearance`, `camera`, `client`, `game`, `graphics`, `misc`, `object`, `scene`, `ui`).
- Key files: `UtinniCore/swg/game/game.cpp`, `UtinniCore/swg/scene/ground_scene.cpp`, `UtinniCore/swg/ui/imgui_impl.cpp`

**`UtinniCore/utility/`:**
- Purpose: Cross-cutting native helpers (logging, memory poking, string ops).
- Contains: 4 paired `.h`/`.cpp` files.
- Key files: `UtinniCore/utility/log.h`, `UtinniCore/utility/memory.h`

**`UtinniCore-Symbols/`:**
- Purpose: A side project that exists *only* to force-instantiate STL templates with stable mangled symbols so CppSharp's generated P/Invoke can link against them. Not loaded at runtime.
- Contains: One source file (`Std-symbols.cpp`) and a vcxproj.

**`UtinniCoreDotNetGen/`:**
- Purpose: The CppSharp generator. Run manually when native headers in `UtinniCore/` change; emits `UtinniCoreDotNet/Generated/UtinniCore.cs`. Not a runtime dependency.
- Contains: `Program.cs` (the `ILibrary` implementation).
- Key files: `UtinniCoreDotNetGen/Program.cs`

**`UtinniCoreDotNet/`:**
- Purpose: The managed host — the editor application + the curated managed bridge plugins should depend on.
- Contains: `main.cs` (entry), seven subfolders for `Callbacks`, `Commands`, `Generated`, `Hotkeys`, `PluginFramework`, `UI`, `UndoRedo`, `Utility`, plus `Properties` and `Resources`.
- Key files: `UtinniCoreDotNet/main.cs`, `UtinniCoreDotNet/UI/Forms/FormMain.cs`, `UtinniCoreDotNet/PluginFramework/PluginLoader.cs`

**`UtinniCoreDotNet/Callbacks/`:**
- Purpose: One static class per native event family (`Game`, `GroundScene`, `Object`, `Cui`, `ImGui`). Each registers exactly one delegate with the native side and fans out to plugin-supplied `Action`s.
- Pattern: `static <Event>Callbacks { Initialize() + Add*Call(...) + Remove*Call(...) + private static <retained delegate field> }`.

**`UtinniCoreDotNet/PluginFramework/`:**
- Purpose: The MEF contracts every .NET plugin implements, plus the loader.
- Contains: `IPlugin.cs`, `IEditorPlugin.cs`, `PluginLoader.cs`.

**`UtinniCoreDotNet/UI/`:**
- Purpose: The custom WinForms control library + the editor shell. Plugins compose their UI from these controls so the editor looks consistent.
- Contains: `Controls/`, `Forms/`, `Theme/`, `GameDragDropEventHandlers.cs`.
- Key files: `UtinniCoreDotNet/UI/Forms/FormMain.cs`, `UtinniCoreDotNet/UI/Controls/PanelGame.cs`, `UtinniCoreDotNet/UI/Theme/Colors.cs`

**`UtinniCoreDotNet/Generated/`:**
- Purpose: CppSharp-generated P/Invoke. `UtinniCore.cs` is regenerated wholesale by `UtinniCoreDotNetGen`; `StdEdited.cs` is hand-curated (its name signals "do not regenerate, edit by hand").
- Contains: 2 files.
- Key files: `UtinniCoreDotNet/Generated/UtinniCore.cs`, `UtinniCoreDotNet/Generated/StdEdited.cs`

**`data/`:**
- Purpose: Runtime data that ships with the editor binary. `ut.ini` is the master settings file written-through by both Launcher and Core; `utinni.cfg` is the SWG client.cfg replacement loaded via the `swg::config::detour` hook.
- Committed: Yes (these are reference defaults; user runs may rewrite them in the build output dir).

**`docs/`:**
- Purpose: User & developer documentation. The HTML site at `docs/index.html` is the canonical reference; the markdown mirror under `docs/ai/` is what AI tooling reads.
- Key files: `docs/index.html` (entry), `docs/ai/architecture.md`, `docs/ai/plugin-framework.md`, `docs/ai/regen-bindings.md`

**`external/`:**
- Purpose: Vendored third-party source. Committed in full so the solution builds offline; no package manager.
- Contains: 7 libraries (CppSharp, DetourXS, ImGuizmo, LeksysINI, imgui, nvapi, spdlog).

**`sdk/`:**
- Purpose: What plugin authors download — Visual Studio templates, an example C++ plugin, and an example .NET editor plugin. Has its own solutions (`UtinniCppPluginTemplate.sln`, `UtinniPluginTemplates.sln`) decoupled from `Utinni.sln`.
- Contains: `UtinniCppPluginTemplate/`, `UtinniPluginTemplates/`, `examples/`.
- Key files: `sdk/examples/ExampleCppPlugin/plugin.cpp`, `sdk/examples/ExampleEditorPlugin/ExampleEditorPlugin.cs`

## Key File Locations

**Entry Points:**
- `Launcher/main.cpp:383`: process entry — `main(argc, argv)`
- `UtinniCore/utinni.cpp:138`: native DLL entry — `DllMain`
- `UtinniCore/utinni.cpp:99`: thread entry — `utinni::main` (the real init body)
- `UtinniCore/clr.cpp:104`: native→managed handoff — `clr::load()` calls `ExecuteInDefaultAppDomain`
- `UtinniCoreDotNet/main.cs:39`: managed entry — `Startup.EntryPoint`
- `UtinniCoreDotNet/UI/Forms/FormMain.cs:80`: editor entry — `FormMain` constructor
- `UtinniCoreDotNetGen/Program.cs:116`: binding generator entry — `Program.Main` (manual)

**Configuration:**
- `data/ut.ini`: master settings, sections `[Launcher]`, `[UtinniCore]`, `[Log]`, `[Editor]`, `[Plugins]`
- `data/utinni.cfg`: SWG `client.cfg` override (loginServer*, groundScene, freeChaseCameraMaximumZoom, splashTimeoutSeconds, ...)
- `UtINI/utini.cpp:30`: the canonical default-values table `utinniSettings[]`
- Per-plugin: `Plugins/<name>/settings.ini` and `Plugins/<name>/input.ini` (HotkeyManager-managed)
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj.user` / `UtinniCore.vcxproj.user`: developer-machine paths (not committed conventions vary)

**Core Logic:**
- `UtinniCore/utinni.cpp:58`: `createDetours()` — the single registry of every DetourXS hook
- `UtinniCore/utinni.cpp:91`: `createPatches()` — the single registry of direct memory patches
- `UtinniCore/plugin_framework/plugin_manager.cpp:51`: native plugin loader (`PluginManager::loadPlugins`)
- `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:44`: managed plugin loader (`PluginLoader.Load`)
- `UtinniCoreDotNet/UI/Forms/FormMain.cs:261`: plugin → editor wiring (`CreatePluginControls`)
- `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:44`: native→managed callback bridge initialization

**Testing:**
- None. This codebase has no test projects. Validation is manual via launching SWG with `Launcher.exe` and exercising plugins.

**Plugin Contracts (for plugin authors):**
- C++: `UtinniCore/plugin_framework/utinni_plugin.h` — base class + `UTINNI_PLUGIN` macro
- .NET runtime: `UtinniCoreDotNet/PluginFramework/IPlugin.cs`
- .NET editor: `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs`
- Example C++: `sdk/examples/ExampleCppPlugin/plugin.cpp`
- Example .NET editor: `sdk/examples/ExampleEditorPlugin/ExampleEditorPlugin.cs`

## Naming Conventions

**Files:**
- **C++:** `lower_snake_case.{h,cpp}` — e.g. `client_object.cpp`, `plugin_manager.h`, `cui_chat_window.cpp`, `imgui_impl.h`. One subsystem-feature pair per file.
- **C#:** `PascalCase.cs` — e.g. `FormMain.cs`, `PluginLoader.cs`, `GameCallbacks.cs`, `UtinniButton.cs`. WinForms designer-partials follow the standard `<Name>.Designer.cs` / `<Name>.resx` pair.
- **Project files:** Match project name: `UtinniCore.vcxproj`, `UtinniCoreDotNet.csproj`, `UtINI.vcxproj`.
- **MIT license header:** Every source file in `UtinniCore/`, `UtinniCoreDotNet/`, `UtINI/`, `Launcher/`, `UtinniCoreDotNetGen/`, and the SDK examples opens with the 23-line MIT block (`/** MIT License ... **/`).

**Directories:**
- **Native:** `lower_snake_case` — `swg/appearance`, `plugin_framework`, `utility`.
- **Managed:** `PascalCase` — `Callbacks`, `PluginFramework`, `UndoRedo`, `UI/Controls`, `UI/Forms`, `UI/Theme`.
- **Generated:** Always under a `Generated/` directory and clearly marked.

**Types & functions (C++):**
- **Namespaces:** `lower_snake_case` — `utinni`, `swg::game`, `utinni::log`, `clr`. Plugin code lives under `utinni`; SWG client functions live under `swg::<area>`.
- **Classes:** `PascalCase` — `Game`, `GroundScene`, `CreatureObject`, `UtinniPlugin`, `PluginManager`. Free-function shims often use a lower-case wrapping namespace instead (`utinni::treefile::detour()`).
- **Functions / methods:** `camelCase` — `createPlugin()`, `loadPlugins()`, `getPlayer()`, `addInstallCallback()`, `setupScene()`.
- **Variables:** `camelCase` (`pluginConfigs`, `currentPluginDir`, `hDllInstance`).
- **Member fields:** No `m_` prefix; just `camelCase` (`pImpl`, `iniFilename`).
- **Function-pointer typedefs:** `pXxx` style — `pInstall`, `pMainLoop`, `pCreatePlugin`. Declared with `using` inside the relevant `swg::<area>` namespace before the RVA assignments.
- **RVA assignments:** Always at file scope inside `namespace swg::<area>`, in the form `pName name = (pName)0xHHHHHHHH;`.
- **API macros:** `UTINNI_API` (`__declspec(dllexport/import)`) on every public class/function; `UTINNI_PLUGIN` for the plugin-factory entry point.

**Types & functions (C#):**
- **Namespaces:** `PascalCase` mirroring directory structure — `UtinniCoreDotNet.UI.Forms`, `UtinniCoreDotNet.PluginFramework`, `UtinniCoreDotNet.Callbacks`. Generated bindings live under `UtinniCore.<Subsystem>` (e.g. `UtinniCore.Utinni`, `UtinniCore.DirectX`, `UtinniCore.ImguiGizmo`).
- **Classes:** `PascalCase` — `FormMain`, `PanelGame`, `HotkeyManager`, `UndoRedoManager`.
- **Custom controls:** `Utinni`-prefixed when they wrap/replace a stock WinForms control — `UtinniButton`, `UtinniComboBox`, `UtinniLabel`, `UtinniTextbox`, `UtinniSlider`, `UtinniNumericUpDown`, `UtinniToggle`, etc.
- **Forms:** `Form`-prefixed — `FormMain`, `FormLog`, `FormHotkeyEditor`, `FormHotkeyEditorDialog`. Base form `UtinniForm`.
- **Plugin contracts:** `I`-prefixed interfaces — `IPlugin`, `IEditorPlugin`, `IEditorForm`, `IUndoCommand`.
- **Static callback classes:** `<Subsystem>Callbacks` — `GameCallbacks`, `GroundSceneCallbacks`, `ObjectCallbacks`, `CuiCallbacks`, `ImGuiCallbacks`.
- **Methods:** `PascalCase` (`Initialize`, `AddMainLoopCall`, `CreatePluginControls`).
- **Fields:** `camelCase` for private fields, sometimes prefixed by role (`private readonly PanelGame game`). Public/readonly collections use `PascalCase` (`public readonly Dictionary<string, Hotkey> Hotkeys`).
- **Generated-then-edited file:** `Generated/StdEdited.cs` carries "Edited" in its name to flag that it is *not* regenerated.

**Configuration keys (ut.ini):**
- **Sections:** `PascalCase` — `[Launcher]`, `[UtinniCore]`, `[Log]`, `[Editor]`, `[Plugins]`.
- **Keys:** `camelCase` — `swgClientPath`, `enableEditorMode`, `useSwgOverrideCfg`, `autoLoginUsername`, `defaultPluginPanel`, `writeClassName`.
- **Plugin entries:** `plugin_NN = <enabled>, <directoryName>` with NN zero-padded ordinal (e.g. `plugin_00 = true, MyPlugin`).

## Where to Add New Code

**Adding a new SWG client function wrapper:**
1. Identify which subsystem it belongs to under `UtinniCore/swg/<area>/`.
2. Open the matching `<feature>.{h,cpp}` (or create a new pair, e.g. `swg/object/vehicle_object.{h,cpp}`).
3. In the `.cpp`, inside `namespace swg::<area>`, add a `using pYourFn = ...` typedef and the RVA assignment `pYourFn yourFn = (pYourFn)0xHHHHHHHH;`.
4. In the `.h`, declare the public `utinni::ClassName` method with `UTINNI_API`.
5. In the `.cpp`, implement the wrapper (typically a one-liner that calls the function pointer).
6. If it needs hooking, add `void detour();` + register from `createDetours()` in `UtinniCore/utinni.cpp:58`.
7. Regenerate managed bindings (`UtinniCoreDotNetGen`) — see `docs/ai/regen-bindings.md`.

**Adding a new lifecycle callback:**
1. Native side: add `addYourCallback(void(*func)())` to the relevant `swg/<area>/<feature>.{h,cpp}`, plus a `static std::vector<void(*)()> yourCallbacks;` and an invocation site inside the detour function.
2. Managed side: add a new `<Subsystem>Callbacks` static class under `UtinniCoreDotNet/Callbacks/` (or extend an existing one). Store the retained delegate in a `static` field, register it from `Initialize()`.
3. Call `<Subsystem>Callbacks.Initialize()` from `UtinniCoreDotNet/main.cs:52` (or from `FormMain.InitializeEditorCallbacks` if it's editor-only — see `FormMain.cs:352`).

**Adding a new custom WinForms control (themed):**
- Primary code: `UtinniCoreDotNet/UI/Controls/Utinni<Name>.cs` (and `.Designer.cs` + `.resx` if WinForms-designed).
- Pull colors from `UtinniCoreDotNet/UI/Theme/Colors.cs` rather than hard-coding.
- If it's a titlebar widget, mirror `UtinniTitlebarButton` / `UtinniTitlebarDropDownButton` and add it from `FormMain`'s `LeftTitleBarButtons` / `RightTitleBarButtons`.

**Adding a new editor form:**
- Primary code: `UtinniCoreDotNet/UI/Forms/<Name>Form.cs` (matching `Form*` naming, e.g. `FormMyTool.cs`).
- Inherit from `UtinniForm` (`UtinniCoreDotNet/UI/Forms/UtinniForm.cs`) for theme consistency.
- For plugin-contributed forms, implement `IEditorForm` (`UtinniCoreDotNet/UI/Forms/IEditorForm.cs`) and return it from `IEditorPlugin.GetForms()` in the plugin.

**Adding a new built-in undo command:**
- Primary code: `UtinniCoreDotNet/Commands/<Domain>Commands.cs` (e.g. `WorldSnapshotCommands.cs` is the existing example).
- Implement `IUndoCommand` (`UtinniCoreDotNet/UndoRedo/IUndoCommand.cs`).
- Plugin-authored commands should ship in the plugin DLL, not here.

**Adding a new plugin (downstream developer):**
- C++ plugin: copy `sdk/examples/ExampleCppPlugin/` or use `sdk/UtinniCppPluginTemplate/`. Subclass `utinni::UtinniPlugin`, end the file with `extern "C" { UTINNI_PLUGIN { return new MyPlugin(); } }`. Build a DLL, drop it in `Plugins/<MyPlugin>/`.
- .NET runtime plugin: use `sdk/UtinniPluginTemplates/DotNetPluginTemplate/`. Implement `IPlugin`. Build a DLL, drop it in `Plugins/<MyPlugin>/`.
- .NET editor plugin: use `sdk/UtinniPluginTemplates/DotNetEditorPluginTemplate/` or copy `sdk/examples/ExampleEditorPlugin/`. Implement `IEditorPlugin`. Build a DLL, drop it in `Plugins/<MyPlugin>/`. MEF auto-discovers it on next launch; the editor adds your sub-panels/standalone-panels/forms to `FormMain` automatically.

**New documentation:**
- HTML: add a page under `docs/` and link it from `docs/index.html`.
- Markdown mirror: add the same content under `docs/ai/` so AI/grep tooling sees it.

**New cross-cutting helper:**
- Native: `UtinniCore/utility/<name>.{h,cpp}` matching the existing four files (`log`, `memory`, `string_utility`, `utility`).
- Managed: `UtinniCoreDotNet/Utility/<Name>.cs` matching the existing two files (`Log.cs`, `Native.cs`).

## Special Directories

**`UtinniCoreDotNet/Generated/`:**
- Purpose: CppSharp-generated P/Invoke bindings.
- Generated: Yes — `UtinniCore.cs` is fully regenerated by running `UtinniCoreDotNetGen`.
- Committed: Yes (so the solution builds without first running the generator).
- Caveat: `StdEdited.cs` is hand-curated despite living next to the regenerated file. Its filename embeds the warning.

**`external/`:**
- Purpose: Vendored third-party source. Built as part of the relevant project (e.g. `imgui` is compiled into `UtinniCore.dll`).
- Generated: No.
- Committed: Yes — full source.
- Caveat: Replacing any of these is a binding-affecting change; for `LeksysINI` the upstream is dormant and `README.md` flags it as "will most likely be replaced soon."

**`data/`:**
- Purpose: Default `ut.ini` and `utinni.cfg` shipped with the binary.
- Generated: No.
- Committed: Yes.
- Caveat: At runtime, the *active* `ut.ini` lives next to `Launcher.exe` in the build-output directory, not here. The Launcher will write defaults into that runtime location on first launch.

**`Plugins/` (NOT in this repo):**
- Purpose: The runtime plugin directory the loader scans. Lives next to `Launcher.exe`, not in source control.
- Generated: Created at first launch by `PluginManager::loadPlugins` if absent (`UtinniCore/plugin_framework/plugin_manager.cpp:85`).
- Committed: No.
- Layout: `Plugins/<PluginName>/<PluginName>.dll` (plus optional `settings.ini`, `input.ini`, satellite assemblies).
- Sibling repo `D:/Code/UtinniPlugins/` contains the official plugins (`SytnersUtinniPlugin/`, `The Jawa Toolbox/`); their build output is copied into `Plugins/<name>/` for runtime use.

**`docs/ai/`:**
- Purpose: Plain-markdown mirror of `docs/*.html`. Same content, different rendering, intended for AI and grep tooling.
- Generated: Hand-maintained alongside the HTML site (no automated transform).
- Committed: Yes.

**`*.vcxproj.user` / `*.csproj.user`:**
- Purpose: Per-developer overrides (debugger commands, working dirs). Visual Studio writes them; the project ignores them via build expectations.
- Generated: Yes (by VS).
- Committed: Mixed — `Launcher.vcxproj.user`, `UtinniCore.vcxproj.user`, etc. are present in the repo. Treat them as machine-local.

---

*Structure analysis: 2026-05-16*
