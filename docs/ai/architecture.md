# Architecture

> Audience: everyone. This is the orientation page — read it first.

Utinni's job is to put **arbitrary user code inside a running SWG client** —
both unmanaged C++ and managed .NET — and give that code (a) the ability to
react to game events, (b) a UI it can draw, (c) a way to package itself as a
plugin, and (d) an editor host that re-parents the game window. It does this by
combining four classic techniques:

1. **Suspended-process injection** — `Launcher.exe` starts `SwgClient_r.exe`
   with `CREATE_SUSPENDED`, patches the entry point with `EB FE` (jump-to-self),
   resumes the thread, waits for `EIP` to land on the EP, calls
   `CreateRemoteThread(LoadLibraryA, "UtinniCore.dll")`, restores the EP.
2. **Function detouring** — `UtinniCore.dll`'s `DllMain` spawns a thread that
   uses [DetourXS](https://github.com/DarthTon/DetourXS) to install ~25 hooks
   at hard-coded RVAs that correspond to known SWG client functions
   (`Game::install`, `GroundScene::draw`, `CuiManager::render`, etc.).
3. **In-process CLR hosting** — once detours and C++ plugins are loaded,
   `clr::load()` calls `CLRCreateInstance` / `ICLRRuntimeHost::Start` to bring
   up the .NET v4.0.30319 runtime, then `ExecuteInDefaultAppDomain` invokes
   `UtinniCoreDotNet.Startup.EntryPoint`.
4. **MEF plugin discovery** — managed `PluginLoader` scans `Plugins/*` via
   `DirectoryCatalog`, MEF composes anything implementing `IPlugin` /
   `IEditorPlugin`, and `FormMain` wires the plugin-contributed
   `SubPanel`s, forms, hotkeys, and undo-event handlers into the editor.

## High-level topology

```mermaid
flowchart TB
  classDef external fill:#231720,stroke:#ff7676,color:#d6e1ee
  classDef launcher fill:#2b231a,stroke:#ffb338,color:#d6e1ee
  classDef native   fill:#28344a,stroke:#57c1ff,color:#d6e1ee
  classDef managed  fill:#1d2a23,stroke:#6dd58c,color:#d6e1ee
  classDef plugin   fill:#2a1d2b,stroke:#cc7be0,color:#d6e1ee

  User["User double-clicks<br/>Launcher.exe (+ cmd args)"]:::external

  subgraph LauncherProc["Launcher process"]
    Launcher["Launcher.exe<br/>· read ut.ini<br/>· locate SwgClient_r.exe<br/>· validate ProductName='Star Wars Galaxies'<br/>· CreateProcess(suspended)<br/>· patch EP, inject, restore"]:::launcher
  end

  subgraph SWGProc["SwgClient_r.exe (after injection)"]
    direction TB
    SWG["SwgClient_r.exe<br/>(DirectX 9, 32-bit)"]:::external
    Core["UtinniCore.dll<br/>· ~25 DetourXS hooks<br/>· ImGui + ImGuizmo<br/>· spdlog<br/>· UtINI (ut.ini, utinni.cfg)<br/>· C++ PluginManager<br/>· CLR host"]:::native
    DotNet["UtinniCoreDotNet.dll<br/>· Startup.EntryPoint<br/>· PluginLoader (MEF)<br/>· Callbacks (Game/GroundScene/Object/Cui/ImGui)<br/>· FormMain (editor)<br/>· HotkeyManager · UndoRedoManager<br/>· WinForms controls + theme"]:::managed
    CppPlugins["C++ Plugins<br/>Plugins/<name>/*.dll"]:::plugin
    NetPlugins[".NET Plugins<br/>Plugins/<name>/*.dll<br/>IPlugin / IEditorPlugin"]:::plugin
  end

  User --> Launcher
  Launcher -- CreateRemoteThread\nLoadLibraryA --> Core
  Core -- detours --> SWG
  Core -- C++ PluginManager loads --> CppPlugins
  Core -- CLR host\nExecuteInDefaultAppDomain --> DotNet
  DotNet -- MEF DirectoryCatalog --> NetPlugins
  CppPlugins -- "utinni::*" --> Core
  NetPlugins -- "UtinniCore.* (P/Invoke)" --> Core
  NetPlugins -- "Callbacks / UI / Undo" --> DotNet
  DotNet -- "P/Invoke (CppSharp)" --> Core
```

## Layered view

```mermaid
flowchart TB
  classDef game fill:#231720,stroke:#ff7676,color:#d6e1ee
  classDef nativeShim fill:#28344a,stroke:#57c1ff,color:#d6e1ee
  classDef nativeCore fill:#1c2935,stroke:#57c1ff,color:#d6e1ee
  classDef gen fill:#2a1d2b,stroke:#cc7be0,color:#d6e1ee
  classDef managed fill:#1d2a23,stroke:#6dd58c,color:#d6e1ee
  classDef plugin fill:#2b231a,stroke:#ffb338,color:#d6e1ee

  Game["SwgClient_r.exe — DirectX 9 native, ~2010 SOE/SWGEmu binary"]:::game

  subgraph N["UtinniCore (native, C++17, /clr off)"]
    Shim["swg/*  — per-subsystem shim files<br/>(client, game, scene, graphics, object, ui, misc, camera, appearance)<br/>each declares native pointers + detour() + patch() + utinni:: API"]:::nativeShim
    Cb["Native callbacks<br/>(addInstallCallback, addMainLoopCallback,<br/>addSetSceneCallback, addOnReceiveSystemMessageCallback, …)"]:::nativeShim
    PM["plugin_framework / PluginManager<br/>(loads Plugins/*/*.dll, calls createPlugin())"]:::nativeShim
    Util["utility/  log · memory · string · utility<br/>imgui_impl, imgui_gizmo (in swg/ui)"]:::nativeShim
    Host["CLR host (mscoree, ICLRRuntimeHost) — clr.cpp"]:::nativeCore
  end

  Game <-- DetourXS hooks at hard-coded RVAs --> Shim

  subgraph G["Generated bindings (CppSharp)"]
    GenCs["UtinniCoreDotNet/Generated/UtinniCore.cs<br/>(P/Invoke to mangled native symbols)"]:::gen
    Edited["Generated/StdEdited.cs (hand-edited std::basic_string wrapper)"]:::gen
  end

  Shim -. parsed by CppSharp .-> GenCs

  subgraph M["UtinniCoreDotNet (managed, C# 7.3, .NET 4.7.2 x86)"]
    Entry["Startup.EntryPoint  (called by clr.cpp)"]:::managed
    CbMgd["Callbacks/<br/>· GameCallbacks · GroundSceneCallbacks<br/>· ObjectCallbacks · CuiCallbacks · ImGuiCallbacks"]:::managed
    PF["PluginFramework/<br/>· IPlugin · IEditorPlugin · PluginLoader (MEF)"]:::managed
    UI["UI/  Controls · Forms (FormMain · PanelGame) · Theme · DragDrop"]:::managed
    HK["Hotkeys/  Hotkey · HotkeyManager (.ini persisted)"]:::managed
    UR["UndoRedo/  IUndoCommand · UndoRedoManager"]:::managed
    Cmds["Commands/  WorldSnapshotCommands (4 built-in)"]:::managed
    UtilM["Utility/  Log · Native"]:::managed
  end

  Host -- ExecuteInDefaultAppDomain --> Entry
  Entry --> CbMgd & PF & UI & HK & UR
  GenCs --> CbMgd
  GenCs --> M

  subgraph P["Plugins"]
    CppP["C++ plugins<br/>UtinniPlugin subclass +<br/>UTINNI_PLUGIN factory"]:::plugin
    NetP[".NET runtime plugins (IPlugin)<br/>.NET editor plugins (IEditorPlugin)"]:::plugin
  end

  PM --> CppP
  PF --> NetP
```

## The eight things Utinni *is*

| #  | Capability                                                              | Where it lives                                                |
| -- | ----------------------------------------------------------------------- | ------------------------------------------------------------- |
| 1  | Suspended-process injector with target validation                       | `Launcher/main.cpp`                                           |
| 2  | DetourXS-based hook installer for ~25 client functions                  | `UtinniCore/swg/*` + `utinni.cpp::createDetours()`            |
| 3  | Memory patcher (e.g. `EB FE` infinite-loop trick, JMP injection)        | `UtinniCore/utility/memory.h` + `swg/*::patch()`              |
| 4  | dearImgui + ImGuizmo overlay piggy-backing on the D3D9 device           | `UtinniCore/swg/ui/imgui_impl.*`                              |
| 5  | spdlog-backed logging, with managed sink callbacks                      | `UtinniCore/utility/log.*` + `UtinniCoreDotNet/Utility/Log.cs`|
| 6  | In-process CLR host                                                     | `UtinniCore/clr.cpp`                                          |
| 7  | C++ plugin model: subclass `UtinniPlugin`, export `createPlugin()`      | `UtinniCore/plugin_framework/`                                |
| 8  | .NET plugin model: implement `IPlugin` / `IEditorPlugin`, MEF discovery | `UtinniCoreDotNet/PluginFramework/`                           |

## The four things Utinni *gives plugin authors*

| Capability                                          | Touch point                                            | Audience          |
| --------------------------------------------------- | ------------------------------------------------------ | ----------------- |
| React to game lifecycle / scene / target / messages | `Callbacks/*.cs` (.NET) or `swg/*/add*Callback` (C++)  | All plugin authors |
| Draw editor UI (panels, forms, themed controls)     | `UI/Controls/*`, `UI/Forms/IEditorForm`                | .NET editor plugins |
| Bind hotkeys (with chord, scope, INI persistence)   | `Hotkeys/HotkeyManager`                                | .NET editor plugins |
| Push reversible edits onto an undo stack            | `IEditorPlugin.AddUndoCommand` event + `IUndoCommand`  | .NET editor plugins |

## Process lifecycle in one timeline

```mermaid
sequenceDiagram
  autonumber
  participant U as User
  participant L as Launcher.exe
  participant S as SwgClient_r.exe
  participant C as UtinniCore.dll
  participant N as UtinniCoreDotNet.dll
  participant P as Plugins

  U->>L: launch (with optional cmd-line cfg args)
  L->>L: load ut.ini, validate ProductName == "Star Wars Galaxies"
  L->>S: CreateProcess(CREATE_SUSPENDED)
  L->>S: patch EP with 0xEB 0xFE (jmp self)
  L->>S: ResumeThread, poll until EIP==EP
  L->>S: CreateRemoteThread(LoadLibraryA, "UtinniCore.dll")
  S->>C: DllMain(DLL_PROCESS_ATTACH) → CreateThread(utinni::main)
  C->>C: load ut.ini
  C->>C: createDetours()  ← ~25 DetourXS hooks
  C->>C: createPatches()  ← memory writes (e.g. midPopCell JMP)
  C->>C: pluginManager.loadPlugins()
  C->>P: createPlugin() + init()   [native plugins]
  C->>C: CoInitializeEx + clr::load()
  C->>N: ICLRRuntimeHost::ExecuteInDefaultAppDomain("UtinniCoreDotNet.Startup.EntryPoint")
  N->>N: Application.EnableVisualStyles + Log.Setup
  N->>N: new PluginLoader()  → MEF DirectoryCatalog over Plugins/*
  N->>P: compose IPlugin / IEditorPlugin parts
  N->>N: GameCallbacks/GroundSceneCallbacks/ObjectCallbacks/CuiCallbacks Initialize()
  L-->>S: restore original EP bytes, ResumeThread (main thread proceeds)
  S->>S: SwgClient WinMain() runs — but detours are already live
  N-->>N: if Editor.enableEditorMode → Application.Run(new FormMain(...))
  N->>P: FormMain consumes IEditorPlugin.GetForms / GetSubPanels / GetStandalonePanels
  S-->>U: editor window with embedded game appears
```

## Threading model (the part that bites plugin authors)

There are **three threads of consequence**:

| Thread                  | Owned by    | Runs                                                                                  |
| ----------------------- | ----------- | ------------------------------------------------------------------------------------- |
| **Game thread**         | SWG         | The main game loop. All native detours fire here. All callback delegates fire here.   |
| **UI thread** (STA)     | UtinniCoreDotNet | `FormMain` and every WinForms control. Created when `Application.Run(new FormMain())` blocks. |
| **Native init thread**  | UtinniCore  | `utinni::main()` itself runs on a thread spawned by `DllMain`; it's done after init.  |

Plugin authors must respect this division:

- **Callbacks fire on the game thread.** Touching WinForms directly from inside
  a callback throws `InvalidOperationException`. Marshal with `Control.Invoke`
  or queue work back into a callback the UI can drain.
- **WinForms input** (key/mouse on `PanelGame`) fires on the UI thread; if it
  needs to call into the game engine, it should enqueue via
  `GameCallbacks.AddMainLoopCall(...)` or `GroundSceneCallbacks.AddUpdateLoopCall(...)`.
- **Long work blocks the game.** A `Thread.Sleep` inside a callback freezes
  rendering. Move work to a background `Task` and re-marshal results.

See [Callbacks](callbacks.md) for the per-callback threading guarantees.

## Configuration files

| File                        | Owner           | Purpose                                                                  |
| --------------------------- | --------------- | ------------------------------------------------------------------------ |
| `ut.ini`                    | UtinniCore + Launcher | Master settings. Sections: `[Launcher]` (swgClientPath/Name), `[UtinniCore]` (enableEditorMode, enableInternalUi, useSwgOverrideCfg, autoLoadScene), `[Log]`, `[Plugins]` (`plugin_0=true,MyPlugin`). |
| `utinni.cfg`                | UtinniCore      | Replacement for SWG's own `client.cfg`. Loaded by the override-detour on `0x00401000`. Lets Utinni dictate `loginServerPort0/Address0`, `groundScene`, `freeChaseCameraMaximumZoom`, `splashTimeoutSeconds=0`, etc. |
| `<plugin>/settings.ini`     | per plugin      | Plugin-defined; managed via the `UtINI` native wrapper from C#.          |
| `<plugin>/input.ini`        | `HotkeyManager` | Rebound hotkeys (per plugin or per form).                                |
| `logs/utinni.log` (default) | spdlog          | All native + managed log output (sink callbacks merge them).             |

## Trust boundaries and what can break

```mermaid
flowchart LR
  classDef stable fill:#1d2a23,stroke:#6dd58c,color:#d6e1ee
  classDef wobbly fill:#2b231a,stroke:#ffb338,color:#d6e1ee
  classDef fragile fill:#231720,stroke:#ff7676,color:#d6e1ee

  Plugin["Your plugin"]:::stable
  Cb["Callbacks API"]:::stable
  Bridge["Managed bridge<br/>(typed P/Invoke wrappers)"]:::stable
  Gen["Generated/UtinniCore.cs<br/>(CppSharp output)"]:::wobbly
  Shim["UtinniCore swg/* shims"]:::wobbly
  RVA["Hard-coded RVAs"]:::fragile
  SWG["SWG client binary"]:::fragile

  Plugin --> Cb --> Bridge --> Gen --> Shim --> RVA --> SWG
```

- **Stable surface (green):** the curated managed bridge — `Callbacks/*`,
  `PluginFramework/*`, `UI/*`, `Hotkeys/*`, `UndoRedo/*`, `Commands/*`. These
  are the things plugins should depend on.
- **Wobbly (yellow):** the auto-generated bindings and the C++ shim layer.
  Public API there is mostly stable, but exact mangled entry points change
  if the headers change.
- **Fragile (red):** the hard-coded RVAs in `swg/*/*.cpp`. They are pinned to
  a specific SWG build. A different `SwgClient_r.exe` will need new
  addresses — see [Internals](internals.md).

## What's *not* in Utinni

| Out of scope                                 | Why / where it lives instead                                                       |
| -------------------------------------------- | ---------------------------------------------------------------------------------- |
| Server-side modding                          | This is *client* injection. Use SWG-Source/swg-main for server mods.               |
| Asset extraction (.tre browsing as a CLI)    | Utinni reads from the running client. Use `TreeFileExtractor.exe` (in swg-client/tools) for offline. |
| Client *source* changes                      | Out of scope; Utinni works with stock binaries. See [swg-client](../../../swg-client/docs/index.html) for source-level work. |
| Updates for non-Pre-CU builds (NGE-only, …)  | The hard-coded RVAs are pinned to the SWGEmu Pre-CU client.                        |
| Anti-cheat circumvention against live shards | Out of scope and not what this tool is for.                                        |

## Next

Continue with one of:

- [Injection & boot](injection.md) — the suspended-process technique and DllMain dance.
- [Native core](core.md) — the C++ shim catalog.
- [Managed bridge](bridge.md) — CppSharp output, FormMain wiring.
- [Plugin framework](plugin-framework.md) — `IPlugin` / `IEditorPlugin` contracts.
- [Tutorial](tutorial.md) — build a hello-world editor plugin.
