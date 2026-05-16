<!-- refreshed: 2026-05-16 -->
# Architecture

**Analysis Date:** 2026-05-16

## System Overview

Utinni is a **DLL-injection modding framework** for the Pre-CU Star Wars Galaxies client (`SwgClient_r.exe`). A native loader patches the suspended game process, injects a C++ core that detours ~25 client functions, then hosts the .NET CLR in-process and runs a C# WinForms editor that re-parents the live D3D9 game window inside its own panel. Plugins (C++ and .NET) drop into `Plugins/<name>/` and are discovered at startup.

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│  Launcher.exe (Win32 native, x86)                                            │
│  `Launcher/main.cpp`                                                         │
│  - reads `ut.ini`, validates SwgClient_r.exe ProductName                     │
│  - CreateProcess(CREATE_SUSPENDED) + EP patch (`0xEB 0xFE`) + CreateRemoteThread │
└─────────────────────────────┬───────────────────────────────────────────────┘
                              │ LoadLibraryA("UtinniCore.dll")
                              ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  SwgClient_r.exe process (DirectX 9, x86)                                    │
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────────┐ │
│  │ UtinniCore.dll (native, C++17)                                          │ │
│  │ `UtinniCore/utinni.cpp` `UtinniCore/clr.cpp`                            │ │
│  │ - DllMain → CreateThread(utinni::main)                                  │ │
│  │ - swg/* shims: native fn-ptrs, detour(), patch(), utinni:: API          │ │
│  │ - PluginManager (loads C++ plugin DLLs)                                 │ │
│  │ - imgui + ImGuizmo overlay piggy-backed on D3D9                         │ │
│  │ - spdlog logging · UtINI config · DetourXS hooks                        │ │
│  │ - ICLRRuntimeHost (v4.0.30319) → ExecuteInDefaultAppDomain              │ │
│  └─────────────────┬──────────────────────────────────────┬─────────────────┘ │
│                    │ detours / fn-ptrs                    │ CLR call           │
│                    ▼                                      ▼                    │
│  ┌────────────────────────┐         ┌────────────────────────────────────┐   │
│  │ SwgClient_r.exe        │         │ UtinniCoreDotNet.dll (managed, C#) │   │
│  │ game engine            │◀─P/Inv─▶│ `UtinniCoreDotNet/main.cs`         │   │
│  │ (untouched binary)     │         │ - Startup.EntryPoint               │   │
│  └────────────────────────┘         │ - PluginLoader (MEF)               │   │
│                                     │ - Callbacks (Game/Scene/Object/Cui)│   │
│                                     │ - FormMain (WinForms editor host) │   │
│                                     │ - HotkeyManager · UndoRedoManager │   │
│                                     └──────────────┬─────────────────────┘   │
│                                                    │                          │
│  ┌─────────────────────────────────────┐           │                          │
│  │ Plugins/<name>/*.dll                │◀──────────┘                          │
│  │ C++:  UtinniPlugin subclass         │     MEF DirectoryCatalog             │
│  │ .NET: IPlugin / IEditorPlugin       │     + native PluginManager           │
│  └─────────────────────────────────────┘                                      │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| Launcher | Suspended-process injection, SWG client validation, command-line cfg passthrough | `Launcher/main.cpp` |
| UtINI (shared library) | INI file read/write used by both Launcher and Core; PIMPL-hidden `LeksysINI` wrapper exported as `UTINNI_API` | `UtINI/utini.h`, `UtINI/utini.cpp` |
| UtinniCore | Native shim layer: DllMain bootstrap, DetourXS hooks, memory patches, CLR host, ImGui overlay, native plugin loading | `UtinniCore/utinni.cpp`, `UtinniCore/clr.cpp` |
| `swg/` shims | One file per SWG subsystem; declare hard-coded RVA function pointers and expose a clean `utinni::` API plus `detour()` / `patch()` hooks | `UtinniCore/swg/game/game.cpp`, `UtinniCore/swg/scene/ground_scene.cpp`, ... |
| Native PluginManager | Discovers `Plugins/<name>/*.dll`, calls exported `createPlugin()`, stores `UtinniPlugin*` instances | `UtinniCore/plugin_framework/plugin_manager.cpp` |
| UtinniCore-Symbols | Companion C++ project compiling STL symbol stubs so CppSharp can bind `std::basic_string` etc. | `UtinniCore-Symbols/Std-symbols.cpp` |
| UtinniCoreDotNetGen | Build-time CppSharp generator that parses `UtinniCore` headers into C# P/Invoke bindings | `UtinniCoreDotNetGen/Program.cs` |
| Generated bindings | CppSharp-generated P/Invoke surface for the managed side | `UtinniCoreDotNet/Generated/UtinniCore.cs`, `UtinniCoreDotNet/Generated/StdEdited.cs` |
| UtinniCoreDotNet | Managed entry point; runs MEF plugin discovery and the WinForms editor shell | `UtinniCoreDotNet/main.cs` |
| Managed PluginLoader | MEF `DirectoryCatalog` over `Plugins/<name>/`; composes `IPlugin` / `IEditorPlugin` parts | `UtinniCoreDotNet/PluginFramework/PluginLoader.cs` |
| Callbacks layer | Bridges native event firing (game install, scene setup, pre/main loop, object lifecycle, CUI, ImGui gizmo) to C# `Action` lists | `UtinniCoreDotNet/Callbacks/GameCallbacks.cs`, `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs`, `UtinniCoreDotNet/Callbacks/ObjectCallbacks.cs`, `UtinniCoreDotNet/Callbacks/CuiCallbacks.cs`, `UtinniCoreDotNet/Callbacks/ImGuiCallbacks.cs` |
| FormMain | Top-level WinForms host; embeds `PanelGame`, aggregates plugin sub-panels/standalone-panels/forms, wires Undo/Redo and Hotkeys | `UtinniCoreDotNet/UI/Forms/FormMain.cs` |
| PanelGame | WinForms Panel that owns SWG's HWND; intercepts WndProc, forwards input, drives focus-gated game suspend/resume | `UtinniCoreDotNet/UI/Controls/PanelGame.cs` |
| HotkeyManager | Rebindable chord-based hotkey registry, per-plugin or per-form, persisted to `input.ini` | `UtinniCoreDotNet/Hotkeys/HotkeyManager.cs`, `UtinniCoreDotNet/Hotkeys/Hotkey.cs` |
| UndoRedoManager | Stack-based undo/redo, wired via `IEditorPlugin.AddUndoCommand` event, cleared on scene cleanup | `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs`, `UtinniCoreDotNet/UndoRedo/IUndoCommand.cs` |
| Custom WinForms control library | Themed buttons, panels, titlebar widgets, drop-downs, sliders, etc. consumed by both Utinni and plugins | `UtinniCoreDotNet/UI/Controls/`, `UtinniCoreDotNet/UI/Theme/` |
| External libs (vendored) | Imgui, ImGuizmo, DetourXS, LeksysINI, spdlog, CppSharp, nvapi | `external/imgui/`, `external/ImGuizmo/`, `external/DetourXS/`, `external/LeksysINI/`, `external/spdlog/`, `external/CppSharp/`, `external/nvapi/` |

## Pattern Overview

**Overall:** In-process DLL-injection framework with a native ↔ managed bridge and an MEF-based plugin host.

**Key Characteristics:**
- **Mixed-language core**: C++17 (`UtinniCore.dll`) owns the SWG client process; C# 7.3 / .NET 4.7.2 x86 (`UtinniCoreDotNet.dll`) owns the editor UI. They communicate via auto-generated P/Invoke produced by CppSharp at build time.
- **Layered shim per subsystem**: every SWG client subsystem (game, scene, graphics, object, ui, misc, camera, appearance, client) has a matching pair `swg/<area>/<feature>.{h,cpp}` that declares the relevant native function pointers at hard-coded RVAs and exposes a clean `utinni::` API. This is the only place RVAs live.
- **Detour + patch**: behavior changes are either DetourXS hooks (`detour()` functions, called once from `createDetours()` in `UtinniCore/utinni.cpp`) or direct memory patches (`patch()` functions in `createPatches()`).
- **Observer / callback bus**: native code registers `void(*)()` callbacks per lifecycle event; the managed `Callbacks/*.cs` layer subscribes one delegate per event and fans out to per-plugin `Action` lists. Plugins never touch the native callback APIs directly.
- **Plugin discovery is filesystem-driven**: both worlds scan `Plugins/<name>/` and load DLLs that implement the relevant contract (`createPlugin()` export for C++, `[InheritedExport] IPlugin` for .NET). Load order is configurable via `ut.ini` `[Plugins]` section.
- **WinForms re-parenting**: rather than draw a separate editor window, `PanelGame` adopts SWG's HWND so the game renders inside the editor's WinForms layout.

## Layers

**Launcher (process-spawn):**
- Purpose: Bootstrap a SWG client process with `UtinniCore.dll` already injected before SWG's `WinMain` runs.
- Location: `Launcher/`
- Contains: One translation unit (`main.cpp`) that does `CreateProcess(CREATE_SUSPENDED)`, entry-point patch with `0xEB 0xFE`, `CreateRemoteThread(LoadLibraryA, ...)`, then restores the original EP bytes.
- Depends on: `UtINI/utini.h` (for `ut.ini`), Win32 (`Shlwapi`, `version.lib`, `TlHelp32`)
- Used by: end user (double-click) and Windows shortcuts (cmd-line passthrough for SWG `.cfg` overrides via `--` syntax)

**UtINI (shared INI library):**
- Purpose: A single PIMPL-hidden INI wrapper consumed by Launcher, Core, plugins, and the managed side (re-exported via CppSharp).
- Location: `UtINI/`
- Contains: `UtINI` class wrapping LeksysINI, default settings list (`utinniSettings`), typed getters/setters.
- Depends on: `external/LeksysINI/`
- Used by: every other component that touches `.ini` files.

**UtinniCore (native core):**
- Purpose: The injected DLL. Owns the hook installation, native plugin loading, ImGui overlay, logging, and CLR boot.
- Location: `UtinniCore/`
- Contains: `utinni.{h,cpp}` (entry point), `clr.{h,cpp}` (CLR host), `plugin_framework/`, `utility/`, and `swg/` (per-subsystem shims).
- Depends on: `UtINI`, `external/DetourXS`, `external/imgui`, `external/ImGuizmo`, `external/spdlog`, `external/nvapi`, `mscoree.lib`.
- Used by: SWG client process (injected at runtime), and re-exported to C++ plugins via `UTINNI_API`.

**UtinniCore-Symbols (CppSharp symbol shim):**
- Purpose: Force-instantiates STL templates so CppSharp can produce stable mangled symbols for `std::basic_string`-shaped types.
- Location: `UtinniCore-Symbols/`
- Depends on: `UtinniCore` headers.
- Used by: only the CppSharp generator at binding-regeneration time.

**UtinniCoreDotNetGen (build-time generator):**
- Purpose: Run CppSharp against `UtinniCore`'s headers and emit `UtinniCoreDotNet/Generated/UtinniCore.cs`.
- Location: `UtinniCoreDotNetGen/`
- Contains: A `CppSharp.ILibrary` that lists headers to bind (`Program.cs`), targets `i686-pc-win32-msvc`.
- Depends on: `external/CppSharp`.
- Used by: manually invoked when native headers change (see `docs/ai/regen-bindings.md`).

**UtinniCoreDotNet (managed host):**
- Purpose: The .NET side of Utinni. Hosts plugins, runs the editor, provides the curated managed API surface that plugins should depend on.
- Location: `UtinniCoreDotNet/`
- Contains: `main.cs` (Startup.EntryPoint), `Callbacks/`, `PluginFramework/`, `UI/`, `Hotkeys/`, `UndoRedo/`, `Commands/`, `Utility/`, `Generated/`.
- Depends on: `UtinniCore.dll` (via CppSharp bindings), `System.ComponentModel.Composition` (MEF), `System.Windows.Forms`, `UtinniCore-Symbols`.
- Used by: every .NET plugin (`IPlugin` / `IEditorPlugin`).

**Plugins (downstream):**
- Purpose: The user-extensibility layer. Each plugin is one or more DLLs dropped into `Plugins/<name>/`. The sibling repo `D:/Code/UtinniPlugins/` ships the canonical plugins (SytnersUtinniPlugin, The Jawa Toolbox — TRE Explorer, Object Explorer, World Snapshot editor, etc.).
- Native plugins: subclass `utinni::UtinniPlugin`, export `extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin()` (the `UTINNI_PLUGIN` macro).
- .NET plugins: implement `IPlugin` (runtime-only) or `IEditorPlugin` (editor UI surface); MEF discovers them via `[InheritedExport]`.
- See `sdk/examples/ExampleCppPlugin/plugin.cpp` and `sdk/examples/ExampleEditorPlugin/ExampleEditorPlugin.cs` for minimal templates.

## Data Flow

### Primary Request Path — boot to editor visible

1. User launches `Launcher.exe` (`Launcher/main.cpp:383` — `main`)
2. Launcher reads `ut.ini` and resolves `SwgClient_r.exe` (`Launcher/main.cpp:213` — `getSwgClientFilename`)
3. `CreateProcess(CREATE_SUSPENDED)` + EP patch with `0xEB 0xFE` (`Launcher/main.cpp:298` — `loadDll`)
4. `CreateRemoteThread(LoadLibraryA, "UtinniCore.dll")` (`Launcher/main.cpp:174` — `inject`)
5. SWG process loads `UtinniCore.dll`; `DllMain` spawns a thread on `utinni::main` (`UtinniCore/utinni.cpp:138` — `DllMain`, `UtinniCore/utinni.cpp:99` — `main`)
6. `ini.load("ut.ini")` → `createDetours()` (~25 DetourXS hooks) → `createPatches()` (`UtinniCore/utinni.cpp:58` — `createDetours`)
7. `pluginManager.loadPlugins()` — scans `Plugins/<name>/*.dll`, calls `createPlugin()` per native plugin (`UtinniCore/plugin_framework/plugin_manager.cpp:51` — `loadPlugins`)
8. `CoInitializeEx(COINIT_APARTMENTTHREADED)` + `clr::load()` boots the .NET v4.0.30319 runtime (`UtinniCore/clr.cpp:104` — `load`)
9. `ICLRRuntimeHost::ExecuteInDefaultAppDomain` → `UtinniCoreDotNet.Startup.EntryPoint` (`UtinniCoreDotNet/main.cs:39` — `EntryPoint`)
10. Managed side: `new PluginLoader()` runs MEF `DirectoryCatalog` over enabled plugin dirs (`UtinniCoreDotNet/PluginFramework/PluginLoader.cs:44` — `Load`)
11. `GameCallbacks/GroundSceneCallbacks/ObjectCallbacks/CuiCallbacks.Initialize()` register native→managed bridge delegates (`UtinniCoreDotNet/Callbacks/GameCallbacks.cs:44`)
12. If `[Editor] enableEditorMode = true`, `Application.Run(new FormMain(pluginLoader))` creates the WinForms editor (`UtinniCoreDotNet/UI/Forms/FormMain.cs:80`)
13. `FormMain` instantiates `PanelGame`, walks `pluginLoader.Plugins` as `IEditorPlugin`, adopts each plugin's sub-panels/standalone-panels/forms (`UtinniCoreDotNet/UI/Forms/FormMain.cs:261` — `CreatePluginControls`)
14. Launcher restores the original EP bytes and resumes SWG's main thread; SWG's `WinMain` runs — but all detours are already armed and the editor is already visible (`Launcher/main.cpp:355`)

### Game-loop callback dispatch

1. SWG calls a hooked function (e.g. `Game::mainLoop` at `0x004237C0`)
2. The DetourXS trampoline calls the `utinni::` replacement defined in `UtinniCore/swg/game/game.cpp`
3. That replacement iterates `mainLoopCallbacks: std::vector<void(*)()>` and invokes each registered callback
4. One of those callbacks is `GameCallbacks.dequeueMainLoopCallsAction` (a C# delegate marshaled as `void(*)()`)
5. The C# method drains `mainLoopCallQueue: ConcurrentQueue<Action>` and invokes every queued `Action` (`UtinniCoreDotNet/Callbacks/GameCallbacks.cs:111` — `DequeueMainLoopCalls`)
6. Each `Action` was previously enqueued by a plugin via `GameCallbacks.AddMainLoopCall(...)`

### Plugin → editor wiring (one-time, at `FormMain` ctor)

1. `FormMain` filters `pluginLoader.Plugins` to `IEditorPlugin` (`UtinniCoreDotNet/UI/Forms/FormMain.cs:89`)
2. For each plugin: subscribe `UndoRedoManager.AddUndoCommand` to the plugin's `AddUndoCommand` event
3. For each plugin: pull `GetSubPanels()` → wrap each in `CollapsiblePanel` → add to the default `SubPanelContainer`
4. For each plugin: pull `GetStandalonePanels()` → add to the right-rail combo
5. For each plugin: pull `GetForms()` → register a "Open..." menu entry that calls `IEditorForm.Create` lazily
6. Plugin keystrokes route through `FormMain.ProcessCmdKey` → `editorPlugin.GetHotkeyManager().ProcessInput(...)` (`UtinniCoreDotNet/UI/Forms/FormMain.cs:161`)

**State Management:**
- Native state: module-level singletons (`static UtINI ini`, `utinni::PluginManager pluginManager`, callback `std::vector`s per subsystem in `swg/*/*.cpp`).
- Managed state: `UndoRedoManager` per `FormMain`; `HotkeyManager` per plugin (+ one per form); `Plugins` collection composed once by MEF and held by `PluginLoader`.
- Configuration state: `ut.ini` (master), `utinni.cfg` (SWG client.cfg override), per-plugin `settings.ini` and `input.ini` written next to the plugin assembly.

## Key Abstractions

**`utinni::UtinniPlugin` (C++ plugin contract):**
- Purpose: Base class every native plugin subclasses; one virtual `init()` + one pure-virtual `getInformation()`.
- File: `UtinniCore/plugin_framework/utinni_plugin.h`
- Pattern: Abstract base + exported C factory (`UTINNI_PLUGIN` macro expands to `extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin()`).

**`IPlugin` / `IEditorPlugin` (.NET plugin contracts):**
- Purpose: MEF-discovered interfaces. `IPlugin` for headless runtime plugins; `IEditorPlugin : IPlugin` for plugins that contribute UI to the editor.
- Files: `UtinniCoreDotNet/PluginFramework/IPlugin.cs`, `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs`
- Pattern: `[InheritedExport]` so subclasses are auto-discovered without each implementor adding `[Export]`.

**`swg::<area>` namespace + RVA function pointers:**
- Purpose: One namespace per SWG subsystem inside the shim layer. Declares `using pXxx = ReturnType(__cdecl*)(...)` aliases and pins each pointer to a known RVA (e.g. `pInstall install = (pInstall)0x00422E80;`). The `utinni::` counterpart wraps each one with type-safe calls and adds callback registration.
- Examples: `UtinniCore/swg/game/game.cpp:54`, `UtinniCore/swg/scene/ground_scene.cpp`, `UtinniCore/swg/object/creature_object.cpp`.
- Pattern: Hard-coded RVA + typedef + thin wrapper.

**Callback bus (`<Subsystem>Callbacks` static classes):**
- Purpose: One static class per native lifecycle event family. Holds `SynchronizedCollection<Action>` / `ConcurrentQueue<Action>` lists and a single retained-delegate field per native registration (the field is critical — losing the GC root causes corruption on WinForms resize, see `GameCallbacks.cs:46` comment).
- Examples: `UtinniCoreDotNet/Callbacks/GameCallbacks.cs`, `UtinniCoreDotNet/Callbacks/ImGuiCallbacks.cs`.
- Pattern: Observer / pub-sub, with a single native subscriber that fans out to many managed subscribers.

**`UtINI` (shared INI wrapper):**
- Purpose: Same C++ class is consumed by Launcher, Core, native plugins, and (via CppSharp) the managed side. Lets a plugin describe its expected settings as a `vector<Value>` so missing keys get auto-populated on first load.
- File: `UtINI/utini.h`
- Pattern: PIMPL + value-list defaults.

**`IUndoCommand` + `UndoRedoManager`:**
- Purpose: Two-method interface (`Execute`/`Unexecute` semantics) + a stack-based manager. The manager auto-clears on `CleanupSceneCallback` because object IDs are no longer valid across scenes.
- Files: `UtinniCoreDotNet/UndoRedo/IUndoCommand.cs`, `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs`.
- Pattern: Command pattern + observer for UI button-state updates.

## Entry Points

**`Launcher.exe :: main`:**
- Location: `Launcher/main.cpp:383`
- Triggers: User double-click or Windows shortcut; cmd-line args after `--` get rebuilt into a SWG cfg-override string.
- Responsibilities: Read `ut.ini`, locate + validate the SWG client, suspend-launch it, inject `UtinniCore.dll`.

**`UtinniCore.dll :: DllMain → utinni::main`:**
- Location: `UtinniCore/utinni.cpp:99` (`main`), `UtinniCore/utinni.cpp:138` (`DllMain`)
- Triggers: `LoadLibraryA` called by Launcher's `CreateRemoteThread`.
- Responsibilities: Resolve dll path, init logging, load `ut.ini`, install detours + patches, load native plugins, boot CLR, hand off to managed entry point.

**`UtinniCoreDotNet.dll :: Startup.EntryPoint`:**
- Location: `UtinniCoreDotNet/main.cs:39`
- Triggers: `ICLRRuntimeHost::ExecuteInDefaultAppDomain` from `clr.cpp:117`.
- Responsibilities: Enable WinForms visual styles, set up logging, instantiate `PluginLoader` (which runs MEF composition), initialize callback bridges, conditionally `Application.Run(new FormMain(...))`.

**`UtinniCoreDotNetGen.exe :: Program.Main`:**
- Location: `UtinniCoreDotNetGen/Program.cs:116`
- Triggers: Developer manually runs the project (see `docs/ai/regen-bindings.md`).
- Responsibilities: Drive CppSharp to regenerate `UtinniCoreDotNet/Generated/UtinniCore.cs`. Not part of the runtime path.

## Architectural Constraints

- **Threading:** Three threads of consequence. (a) **Game thread** — SWG's main loop; all native detours and all callback delegates fire here. (b) **UI thread (STA)** — created when `Application.Run(new FormMain(...))` is called from `Startup.EntryPoint`; owns every WinForms control. (c) **Native init thread** — spawned by `DllMain` for `utinni::main`; exits after bootstrap. WinForms calls from a game-thread callback throw `InvalidOperationException`; marshal with `Control.Invoke` or queue work back via `GameCallbacks.AddMainLoopCall`.
- **Global state:** Module-level singletons in `UtinniCore/utinni.cpp` (`ini`, `pluginManager`, `path`, `swgOverrideCfgFilename`). Per-subsystem `std::vector<void(*)()>` callback lists live as file-static globals in each `swg/*/*.cpp`. On the managed side, every `Callbacks/*.cs` is `static` with `SynchronizedCollection<Action>` / `ConcurrentQueue<Action>` fields.
- **GC-rooted delegate fields:** Every managed delegate handed to native code must be held in a `static` field of the registering class (see comments in `GameCallbacks.cs:46`, `ImGuiCallbacks.cs:43`). Letting it be GC'd silently corrupts the native callback list on WinForms resize.
- **Hard-coded RVAs:** Every detour / function pointer is pinned to a specific SWGEmu Pre-CU `SwgClient_r.exe` build. A different client requires re-addressing every `swg/*/*.cpp`. There is no auto-discovery / signature scanning.
- **x86 only, .NET Framework 4.7.2:** SWG is a 32-bit DirectX 9 binary, so the entire stack is x86. The hosted CLR is .NET v4.0.30319 (`UtinniCore/clr.cpp:51`). Plugins must target the same.
- **Single-build assumption:** Solution configurations `Debug|x86`, `Release|x86`, `RelWithDbgInfo|x86`. Native projects map to `Win32`, managed to `x86`.
- **Bindings drift:** `UtinniCoreDotNet/Generated/UtinniCore.cs` must be regenerated after any header change in `UtinniCore/` (see `UtinniCoreDotNetGen/Program.cs:67-92` for the header list). The generator does *not* run automatically as part of a normal solution build.
- **Plugin DLL discovery is recursive but unsandboxed:** `PluginManager::loadPlugins` does `std::filesystem::recursive_directory_iterator` on each enabled plugin folder and `LoadLibrary`s every `.dll` it finds. A malicious or broken plugin DLL crashes the host process.

## Anti-Patterns

### Touching WinForms from inside a native callback

**What happens:** A plugin author registers a callback via `GameCallbacks.AddInstallCallback(...)` and tries to update a `TextBox.Text` or call `form.Show()` directly inside it.
**Why it's wrong:** The callback fires on the game thread, not the WinForms UI thread; you get `InvalidOperationException: Cross-thread operation not valid`. Worse, if the WinForms layout layer happens to be touching the same control, you can deadlock the renderer.
**Do this instead:** Marshal with `Control.Invoke` (or `BeginInvoke`), or enqueue UI work onto a queue the UI thread already drains. The codebase's preferred pattern is `GameCallbacks.AddMainLoopCall(() => { ... })` for native work and `Control.Invoke(...)` for UI updates (`UtinniCoreDotNet/Hotkeys/HotkeyManager.cs:68`).

### Letting a native-bound delegate get GC'd

**What happens:** Author calls `UtinniCore.Utinni.Game.AddInstallCallback(MyMethod)` directly, with no field to hold the delegate.
**Why it's wrong:** The implicit `Action_` delegate has no managed root; the GC collects it; the native callback list now points at freed memory; on the next WinForms resize the process AVs. See the comments in `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:46` and `UtinniCoreDotNet/Callbacks/ImGuiCallbacks.cs:43`.
**Do this instead:** Store the delegate in a `static` (or instance) field on the registering class, then pass that field to the native side. The `Callbacks/*.cs` pattern of `private static UtinniCore.Delegates.Action_ xxxAction;` is the project's convention.

### Hard-coding an RVA in a plugin

**What happens:** A plugin reaches into the SWG binary at a known address (`*(int*)0x00ABCDEF`) to read game state.
**Why it's wrong:** RVAs are pinned to one specific `SwgClient_r.exe` build; plugin breaks the moment Utinni is updated or a different client is targeted. It also bypasses the `swg/*` shim layer, which is the only place RVAs are supposed to live.
**Do this instead:** Add a typed wrapper to the relevant `UtinniCore/swg/<area>/<feature>.{h,cpp}` shim, regenerate bindings, and call through the typed API. See `UtinniCore/swg/game/game.cpp:54` for the canonical RVA-declaration style.

### Editing `Generated/UtinniCore.cs` by hand

**What happens:** Author tweaks the CppSharp output to fix a binding bug.
**Why it's wrong:** The next regeneration nukes the edit. `Generated/StdEdited.cs` is the *only* hand-curated generated file (it's specifically named "Edited" to flag that).
**Do this instead:** Either fix the header in `UtinniCore/` so CppSharp produces correct output, or add hand-written interop in a non-`Generated/` location.

### Long work inside a callback

**What happens:** A callback does file I/O, network calls, or `Thread.Sleep` before returning.
**Why it's wrong:** Game-thread callbacks block the game loop; rendering freezes for the duration. The game thread is shared by every callback in the bus.
**Do this instead:** Kick the work to a `Task` and re-marshal results via `GameCallbacks.AddMainLoopCall` or `Control.Invoke`. Treat callbacks as "fire and yield."

## Error Handling

**Strategy:** Defensive — log and continue on non-fatal failures (plugin load errors, missing INI values); abort early on fatal startup errors (Launcher target-validation, CLR start failure).

**Patterns:**
- Launcher uses `MessageBoxA` + `throw std::runtime_error` for unrecoverable injection failures (`Launcher/main.cpp:48` — `throwError`).
- UtinniCore writes to `spdlog` via `utinni::log::{info,warning,error,critical}` (`UtinniCore/utility/log.h`); critical CLR-host failures call `utinni::Game::quit()` and tear down the CLR (`UtinniCore/clr.cpp:124`).
- Managed side uses `UtinniCoreDotNet.Utility.Log` which writes to spdlog through the same sink (so all logs converge into `logs/utinni.log`).
- Plugin discovery is best-effort: invalid plugin directories are dropped from the `[Plugins]` list (`UtinniCore/plugin_framework/plugin_manager.cpp:124`) and DLLs missing `createPlugin` are silently skipped.
- INI parse failures fall back to defaults: if `ut.ini` doesn't exist or is missing keys, the default `utinniSettings` list is written out (`UtINI/utini.cpp:75`).

## Cross-Cutting Concerns

**Logging:** spdlog (native) with a managed sink. `UtinniCore/utility/log.{h,cpp}` exposes `utinni::log::info/debug/warning/error/critical`. `UtinniCoreDotNet/Utility/Log.cs` mirrors them on the managed side, forwarding through P/Invoke into the same spdlog instance. Optional message-buffer accessors (`getMessageBufferCount`, `getMessageAt`) feed the `FormLog` window.

**Validation:** Launcher validates the target SWG client by reading PE `\StringFileInfo\040904B0\ProductName` and requiring `"Star Wars Galaxies"` (`Launcher/main.cpp:281`). UtINI validates missing keys on load and writes defaults. There is otherwise no managed validation framework (no FluentValidation etc.); callers do their own argument checking.

**Authentication:** None. This is an offline-or-private-server modding tool. SWGEmu login is handled by the unmodified SWG client; Utinni can override server/port and even auto-login via `[UtinniCore] autoLogin / autoLoginUsername` in `ut.ini`.

**Configuration:** Two file kinds. `ut.ini` (Launcher + Core + plugin enable list) is the master settings file. `utinni.cfg` (read via the `swg::config::detour` hook against `0x00401000`) replaces SWG's `client.cfg` and can override `loginServerAddress0`, `groundScene`, `freeChaseCameraMaximumZoom`, etc. Per-plugin `settings.ini` and `input.ini` are written next to the plugin assembly.

**Theming:** A static color palette in `UtinniCoreDotNet/UI/Theme/Colors.cs` consumed by every custom WinForms control under `UtinniCoreDotNet/UI/Controls/`. Plugins should consume these via the `Utinni*` control library rather than re-styling stock WinForms controls.

---

*Architecture analysis: 2026-05-16*
