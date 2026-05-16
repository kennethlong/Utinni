# Native core (UtinniCore)

> Audience: C++ plugin authors + core contributors. .NET plugin authors can
> skip to [Managed bridge](bridge.md) — most C++ surface is auto-projected
> into C# and you rarely need to touch native directly.

`UtinniCore.dll` is the heart of Utinni: a 32-bit native DLL injected into the
SWG client. It's structured around three responsibilities:

1. **Wrap the running SWG client** — one `swg/<subsystem>/` folder per client
   subsystem. Each folder owns a thin layer of (a) function pointers into the
   client, (b) DetourXS hook installers, (c) C++ callbacks, and (d) a
   public-facing `utinni::<Subsystem>` API.
2. **Provide a plugin model** — `plugin_framework/` lets DLLs in `Plugins/`
   declare themselves and run `init()` after Utinni boots.
3. **Provide infrastructure** — `utility/` (log, memory, string),
   `clr.h/cpp` (CLR host), plus dearImgui+ImGuizmo wired through the
   D3D9 device.

## Layout

```
UtinniCore/
├── utinni.h / .cpp           ← DllMain, main(), createDetours/Patches, getPath/getConfig/getPluginManager
├── clr.h / .cpp              ← CLR v4 host (CLRCreateInstance, ExecuteInDefaultAppDomain)
├── UtinniCore.rc / resource.h
├── plugin_framework/
│   ├── utinni_plugin.h       ← UtinniPlugin interface + UTINNI_PLUGIN macro
│   ├── plugin_manager.h
│   └── plugin_manager.cpp
├── utility/
│   ├── log.h / .cpp          ← spdlog wrapper + sink callbacks
│   ├── memory.h / .cpp       ← pattern scan + read<T>/write<T>/createJMP
│   ├── string_utility.h/.cpp
│   └── utility.h / .cpp
└── swg/
    ├── appearance/   (skeleton, particle, appearance, portal, extent)
    ├── camera/       (camera, debug_camera)
    ├── client/       (client)
    ├── game/         (game)
    ├── graphics/     (graphics, directx9, shader, post_processing, depth_texture)
    ├── misc/         (config, direct_input, tree_file, network, audio, swg_math,
    │                  swg_memory, swg_string, swg_misc, swg_utility, crc_string, io_win)
    ├── object/       (object, creature_object, client_object, player_object)
    ├── scene/        (ground_scene, render_world, terrain, client_world, scene, world_snapshot)
    └── ui/           (cui_manager, cui_chat_window, cui_hud, cui_radial_menu, cui_menu,
                       cui_io, cui_misc, cui_login_screen, command_parser, imgui_impl,
                       utinni_command_parser, plus ui_* control-tree wrappers)
```

## How a single `swg/<subsystem>/` works

Each subsystem follows the same convention. Take `swg/game/game.h` / `game.cpp`
as the canonical example:

```cpp
// utinni.cpp
void createDetours() {
    ...
    utinni::Game::detour();   // ← per-subsystem hook installer
    ...
}
```

```cpp
// swg/game/game.cpp (sketch)
namespace swg::game {
    // Native function pointer types matching the SWG client's signatures.
    typedef void(__thiscall *install_t)(void* this_);
    install_t install = nullptr;
    // ... etc for mainLoop, setupScene, cleanupScene, etc.
}

namespace utinni::Game {
    // Public C++ API exported under utinni::Game
    void install() { swg::game::install_call(...); }
    bool isRunning();
    void loadScene(const char* terrain, const char* avatar);
    void cleanupScene();
    // Callback registration
    static std::vector<std::function<void()>> installCallbacks;
    void addInstallCallback(std::function<void()> cb) { installCallbacks.push_back(cb); }
    // ...

    void detour() {
        // Wire DetourXS to the SWG addresses
        swg::game::install = (swg::game::install_t)Detour::Create(
            (LPVOID)0x00422E80,           // RVA of Game::install in the SWG client
            myInstallHook,                // our replacement
            DETOUR_TYPE_PUSH_RET);
        // ... more hooks
    }
}
```

The pattern repeats across all ~9 subsystem folders. Once a `detour()`
installer has run, calls into the original SWG function are routed through our
hook; our hook typically (a) raises any registered callbacks, then (b) chains
back to the original via the saved trampoline pointer.

**A C++ plugin's job is to call `utinni::<Subsystem>::add*Callback()` to
subscribe.** The detours themselves are not user-extensible at runtime — to
add a new hook you edit `swg/<subsystem>/` in the core.

## Subsystem catalog

The detail tables below list the **public `utinni::` API** for each
subsystem. For the hard-coded RVAs and hook trampolines, see
[Internals](internals.md).

### `swg/client/client.h` — `utinni::Client`

The top-level client lifecycle (window, input, crash dumps). Editor mode
hangs off this — `Client::setEditorMode(bool)` is the master switch read by
many other subsystems.

| Symbol                                  | Purpose                                                              |
| --------------------------------------- | -------------------------------------------------------------------- |
| `setEditorMode(bool)` / `getEditorMode()` | Master toggle. Read from `ut.ini → [UtinniCore] enableEditorMode`.   |
| `setHwnd(HWND)` / `getHwnd()`           | Set when WinForms re-parents the SWG window into `PanelGame`.        |
| `setHInstance` / `getHInstance`         | Likewise for the SWG window HINSTANCE.                               |
| `setSize(w,h)` / `getWidth/getHeight`   | Forwarded to SWG resize when the host panel changes size.            |
| `suspendInput()` / `resumeInput()` / `isInputAllowed()` | Tells SWG to stop reading mouse/DInput while the user is in a non-game WinForms control. |

Wraps SWG's `Client` singleton entry points: `setupStartDataInstall`,
`clientMain`, `WndProc`, `writeCrashLog`, `writeMiniDump`.

### `swg/game/game.h` — `utinni::Game`

The main game loop. **The most-used callback surface for plugins.**

| Symbol                                  | Purpose                                                              |
| --------------------------------------- | -------------------------------------------------------------------- |
| `addInstallCallback(fn)`                | Fires once when `Game::install` completes (engine subsystems ready). |
| `addPreMainLoopCallback(fn)` / `addMainLoopCallback(fn)` | Before / after each frame.                                          |
| `addSetSceneCallback(fn)` / `addCleanupSceneCallback(fn)` | A scene was loaded / is being torn down.                            |
| `quit()`                                | Ask SWG to shut down cleanly.                                        |
| `isRunning()`                           | Has `Game::install` completed?                                       |
| `isSafeToUse()`                         | Checks the two SWG-side "safe" flags. Useful for re-entrancy guards. |
| `loadScene(terrain, avatar)`            | `setupScene` with two filenames. Avatar is e.g. `object/creature/player/shared_human_male.iff`. |
| `cleanupScene()`                        | Tear down the current scene.                                         |
| `getRepository()`                       | The client-side object repository (used by Network::getObjectById).  |
| `getPlayer()` / `getPlayerCreatureObject()` | The local player's `CreatureObject`.                                |
| `getPlayerLookAtTargetObject()`         | What the player is currently targeting.                              |
| `getCamera()` / `getConstCamera()`      | The active camera (debug or game).                                   |

### `swg/scene/ground_scene.h` — `utinni::GroundScene`

The ground-side scene. This is the most-detoured subsystem after `Game`
because almost every editor action eventually wants to touch the scene.

| Symbol                                  | Purpose                                                              |
| --------------------------------------- | -------------------------------------------------------------------- |
| `get()`                                 | Static singleton getter (reads `0x190885C` directly).                |
| `ctor(terrain, avatar)`                 | Manually construct a GroundScene — used by offline-mode boot.        |
| `addUpdateLoopCallback(fn)`             | Per-frame after `update()`.                                          |
| `addPreDrawLoopCallback(fn)` / `addPostDrawLoopCallback(fn)` | Around `draw()`.                                                    |
| `addCameraChangeCallback(fn)`           | Camera index swapped (e.g. free-cam toggled).                        |
| `removeDetour()`                        | Tear down hooks (used by offline-mode reload).                       |
| `getCurrentCamera()`                    | The active camera (index 0 = chase, etc.).                           |
| `toggleFreeCamera()` / `changeCameraMode()` / `isFreeCameraActive()` | Free-cam (debug camera) controls.                                   |
| `reloadTerrain()`                       | Force re-load `.trn` from disk.                                      |
| `createObjectAtPlayer(filename)` / `createAppearanceAtPlayer(filename)` | Temporary spawn for object-browser drag-drop preview.               |

### `swg/scene/render_world.h` — `utinni::renderWorld`

| Symbol                                  | Purpose                                                              |
| --------------------------------------- | -------------------------------------------------------------------- |
| `addObjectNotifications(obj)`           | Register a manually-instanced object with the renderer so it actually draws. |

### `swg/scene/client_world.h` — `utinni::clientWorld`

Wraps `ClientWorld::collide` for ray-cast queries. Currently the detour is
disabled in `createDetours()` (commented out); callable directly.

### `swg/scene/world_snapshot.h` — `utinni::WorldSnapshot`

Wraps the `.ws` world-snapshot system — placement records for "static"
objects within a planet. The Jawa Toolbox's snapshot editing all flows
through here. `WorldSnapshotReaderWriter` handles load/save.

### `swg/object/*` — `utinni::Object` and friends

`Object` is the base of the SWG object hierarchy. `CreatureObject` /
`ClientObject` / `PlayerObject` extend it. Notable hooks:

| Symbol                                  | Purpose                                                              |
| --------------------------------------- | -------------------------------------------------------------------- |
| `getObjectTemplateByFilename/Iff/Crc`   | Template lookup.                                                     |
| `createObject(template)`                | Spawn a new object instance.                                         |
| `creatureObject::addOnTargetCallback(fn)` | Detours `CreatureObject::setTarget`; fires whenever target changes. |

### `swg/graphics/graphics.h` — `utinni::Graphics`

Wraps SWG's `Graphics` class — the wrapper around D3D9 + render-loop
bookkeeping. The hot path is the begin/end-scene callbacks.

| Symbol                                  | Purpose                                                              |
| --------------------------------------- | -------------------------------------------------------------------- |
| `addPreUpdateLoopCallback` / `addPostUpdateLoopCallback`   | Around `Graphics::update`.                                          |
| `addPreBeginSceneCallback` / `addPostBeginSceneCallback`   | Around `Graphics::beginScene`.                                      |
| `addPreEndSceneCallback` / `addPostEndSceneCallback`       | Around `Graphics::endScene`.                                        |
| `addPrePresentCallback` / `addPostPresentCallback`         | Around `Graphics::present`.                                         |
| `addPrePresentWindowCallback` / `addPostPresentWindowCallback` | Likewise for the window present path.                              |
| `flushResources()` / `reloadTextures()`                    | Manual cache invalidation.                                          |
| `useHardwareCursor` / `showMouseCursor` / `setSystemMouseCursorPosition` | Cursor control.                                                    |
| `setStaticShader` / `setObjectToWorldTransformAndScale`    | Direct draw helpers used by ImGui overlay.                          |
| `drawExtent(transform)`                                    | Visualise an extent box (object collision bounds).                  |

### `swg/graphics/directx9.h` — `utinni::directX`

Lower-level: the wrapped `IDirect3DDevice9`. ImGui+ImGuizmo render here.

| Symbol                                  | Purpose                                                              |
| --------------------------------------- | -------------------------------------------------------------------- |
| `getDirectXDevice()`                    | The `IDirect3DDevice9*` SWG handed back to us.                       |
| `getDepthTexture()`                     | A depth texture resolve (when supported).                            |
| `enableWireframe()`                     | D3DRS_FILLMODE → wireframe.                                          |
| `cleanup()`                             | Free our owned device resources on detach.                           |

### `swg/graphics/shader.h` / `post_processing.h`

`shader::addDrawPhaseCallback(fn)` — fires on each draw phase (uses a JMP
patch at `0x00773E39` inside SWG's `midPopCell`).

`postProcessing::addPreSceneRenderCallback(fn)` / `addPostSceneRenderCallback(fn)`
— fire before/after the bloom pass (detoured via Bloom::preSceneRender /
postSceneRender). Generic "do something with the depth buffer" hook for
post-process effects.

### `swg/ui/imgui_impl.h` — `utinni::imgui_impl`

The bridge that puts dearImgui on top of SWG. **Plugins don't usually call
this directly — they use the higher-level `imgui_gizmo` and the managed
`ImGuiCallbacks` instead.**

| Symbol                                  | Purpose                                                              |
| --------------------------------------- | -------------------------------------------------------------------- |
| `enableInternalUi(bool)`                | Toggle developer ImGui panels.                                       |
| `Enable(Object*)`                       | Show the gizmo attached to an object.                                |
| `Disable()`                             | Hide the gizmo.                                                      |
| `SetGizmoMode(World/Local)`             | Coordinate space.                                                    |
| `SetOperationMode(Translate/Rotate)`    | Active manipulator.                                                  |
| `EnableSnap(bool)` / `SetSnapSize(f)`   | Snap-to-grid.                                                        |

### `swg/ui/cui_manager.h` — `utinni::CuiManager`, `utinni::UiManager`

The client UI manager. Two related things share this file:

- `CuiManager::setSize/togglePointer/isRenderingUi/hasObjectUnderCursor` — UI
  state inspection.
- `UiManager::get()` and `UiManager::drawCursor()` — the lower-level singleton.
- `SystemMessageManager::addReceiveMessageCallback(fn)` /
  `sendMessage(msg)` — system-message stream (chat-style messages from the
  game engine).

### `swg/ui/cui_chat_window.h` — `utinni::CuiChatWindow`

| Symbol                                  | Purpose                                                              |
| --------------------------------------- | -------------------------------------------------------------------- |
| `addCreateCommandParserCallback(fn)`    | Hook the chat window's parser construction — used by example C++ plugin to inject a slash-command. |
| `enableTextInput(bool)`                 | Toggle text-input mode.                                              |
| `writeToAllTabs(text)` / `writeToCurrentTab(text)` | Programmatic text injection.                                       |

### `swg/ui/cui_hud.h` — `utinni::cuiHud`

HUD update + action dispatch. Used by editor for "what is the player looking
at right now."

### `swg/ui/cui_radial_menu.h` — `utinni::cuiRadialMenuManager`

Radial context menu around the targeted object. Detoured for the gizmo
integration.

### `swg/ui/command_parser.h` — `utinni::CommandParser`

The base class for SWG slash-commands. Plugins (both C++ and via the
chat-window callback) subclass this to add their own commands. See the
`ExampleCppPlugin` for the canonical pattern.

### `swg/misc/config.h` — `utinni::config`

Detours `loadOverrideConfig` so that **`utinni.cfg` shadows `client.cfg`**.
The `useSwgOverrideCfg` boolean in `ut.ini` toggles this.

### `swg/misc/tree_file.h` — `utinni::treefile`

Wraps SWG's `TreeFile` — the union-mounted virtual filesystem composed of
`.tre` archives. Detoured to record every filename ever resolved; the running
set is `getAllFilenames()`. The object browser uses this to enumerate
everything available without re-opening the archives ourselves.

### `swg/misc/network.h` — `utinni::Network`

`getObjectById(id)` for managed-side lookups of network objects (used by
`ImGuiCallbacks` to find what the gizmo is attached to).

### `swg/misc/swg_math.h`, `swg_string.h`, `swg_memory.h`

SWG's own math (`Vector`, `Matrix`, `Transform`), strings (`String`, `WString`,
`CrcString`), and allocator. Cleanly reflected through CppSharp.

### `swg/camera/debug_camera.h` — `utinni::debugCamera`

Free-fly camera (independent of player). Speed, FOV, drag-the-player toggle,
input event processing.

### `swg/appearance/skeleton.h` — `utinni::skeletalAppearance`

`setRenderSkeleton(bool)` — draws the bone hierarchy of every skeletal
appearance. Useful debug visual.

## Plugin framework (C++)

`UtinniCore/plugin_framework/utinni_plugin.h` defines the contract:

```cpp
namespace utinni {
struct UtinniPlugin {
    struct Information {
        const char* name;
        const char* description;
        const char* author;
    };

    virtual void init() {}
    virtual const Information& getInformation() = 0;
};
}

#define UTINNI_PLUGIN extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin()
```

The plugin DLL exports one function: `createPlugin`. The example:

```cpp
class ExampleUtinniPlugin : public utinni::UtinniPlugin {
    Information information = { "Example Plugin", "...", "Author" };
public:
    void init() override {
        utinni::CuiChatWindow::addCreateCommandParserCallback(
            &example::ExampleCommandParser::create);
    }
    const Information& getInformation() override { return information; }
};

UTINNI_PLUGIN { return new ExampleUtinniPlugin(); }
```

### Discovery

`PluginManager::loadPlugins()` reads `[Plugins] plugin_0`, `plugin_1`, … from
`ut.ini`. Each entry is `enabled,DirectoryName` (e.g. `true,MyPlugin`). For
each enabled entry:

1. `LoadLibraryA("Plugins/<DirectoryName>/<DirectoryName>.dll")`
2. `GetProcAddress("createPlugin")` → call it to get the `UtinniPlugin*`
3. Call `plugin->init()` after all plugins are loaded.

Disabled plugins are *not* loaded — the DLL is never touched.

### Public API for plugin authors

```cpp
const std::string&        utinni::getPath();
const std::string&        utinni::getSwgCfgFilename();
utinni::UtINI&            utinni::getConfig();
utinni::PluginManager&    utinni::getPluginManager();
```

— available by `#include "utinni.h"` (linked against `UtinniCore.lib` /
imported from `UtinniCore.dll`).

## Utility layer

### `utility/log.h`

```cpp
utinni::log::info(const char* fmt, ...);
utinni::log::warning(...);
utinni::log::error(...);
utinni::log::critical(...);
utinni::log::debug(...);

utinni::log::addOutputSinkCallback(std::function<void(const std::string&)>);
```

Backed by spdlog; the managed side registers a sink callback so .NET plugins
see the same log stream.

### `utility/memory.h`

```cpp
// Pattern scan
auto addr = utinni::memory::findPattern(start, length, pattern, mask);
auto addr = utinni::memory::findPattern("kernel32.dll", pattern, mask);

// Typed read/write
T value     = utinni::memory::read<T>(address);
T deref     = utinni::memory::read<T>(address, offset);
utinni::memory::write<T>(address, value);

// Manual JMP patching
utinni::memory::createJMP(sourceAddr, targetAddr, byteCount);
```

These are how subsystems install their own ad-hoc patches outside of DetourXS.

### `utility/string_utility.h` / `utility.h`

Helpers for `WString` ↔ `std::wstring` ↔ `std::string`, path normalisation,
etc. Used internally; rarely needed from plugins.

## ImGui + ImGuizmo

`swg/ui/imgui_impl.cpp` hooks the D3D9 device's `BeginScene` / `EndScene` /
`Present`. ImGui is initialised against the device the first time we see it
after install. Two layers of UI:

- **Internal UI** — gated by `enableInternalUi`. Developer panels (log viewer,
  resource browser).
- **ImGuizmo** — the 3D transform handles for snapshot-node manipulation.
  Driven through `utinni::imgui_impl::Enable/Disable` and the managed
  `ImGuiCallbacks` for position/rotation/enabled events.

See [Callbacks — ImGuiCallbacks](callbacks.md#imguicallbacks) for the high-level
flow.

## Build configuration

| Property            | Value                                                                                                                                       |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| Configuration type  | DynamicLibrary (.dll)                                                                                                                       |
| Platform Toolset    | v142 (VS 2019)                                                                                                                              |
| Character set       | Multi-byte                                                                                                                                  |
| C++ standard        | C++17                                                                                                                                       |
| Platform            | Win32 (x86 only)                                                                                                                            |
| Output dir          | `bin/<Config>/UtinniCore.dll` (sibling: `UtinniCore.lib`, used by plugins)                                                                   |
| Preprocessor        | `EXPORT_UTINNI` (toggles dllexport/dllimport on `UTINNI_API`), `SPDLOG_NO_EXCEPTIONS`, `_CRT_SECURE_NO_WARNINGS`, build-config DEBUG/NDEBUG  |
| Includes            | `external/CppSharp/include`, `external/DetourXS`, `external/imgui`, `external/ImGuizmo`, `external/LeksysINI`, `external/spdlog/include`, `external/nvapi`, repo root |
| Post-build          | Copy `data/` to output dir; run `UtinniCoreDotNetGen.exe` to regenerate `UtinniCoreDotNet/Generated/UtinniCore.cs`                            |

`RelWithDbgInfo` is the configuration most plugin authors will use against —
optimised but with PDBs.

## Cross-references

- [Internals](internals.md) — the full RVA / patch / hook table.
- [Bridge](bridge.md) — how the native API surfaces through CppSharp.
- [swg-client/client.html](../../../swg-client/docs/client.html) — what the
  hooked client subsystems actually do.
