# Utinni — documentation (AI-oriented)

> **For humans:** open [`docs/index.html`](../index.html) for the navigable HTML site.
> This `ai/` directory carries the same content as plain markdown so it can be
> consumed by LLMs, grep, and scripted tooling.

Utinni is a **client plugin and injection framework** for Pre-CU Star Wars
Galaxies clients (specifically the SWGEmu / SWG-Source family of community
builds running on the 2010-era binary). It launches `SwgClient_r.exe` suspended,
patches its entry point to an infinite loop, injects `UtinniCore.dll` via
`CreateRemoteThread → LoadLibraryA`, then restores the entry point so the game
boots with detours and patches already in place.

Once injected, Utinni:

1. Installs ~25 `DetourXS` hooks into game subsystems (`Client`, `Game`,
   `GroundScene`, `CuiManager`, CUI subwindows, `Graphics`, `DirectX9`,
   `TreeFile`, `Object`, `CreatureObject`, etc.).
2. Loads native C++ plugins from `Plugins/<name>/*.dll`.
3. Hosts the .NET v4.0.30319 CLR inside the SWG process and calls
   `UtinniCoreDotNet.Startup.EntryPoint`.
4. The managed entry point initialises callback systems
   (`GameCallbacks`, `GroundSceneCallbacks`, `ObjectCallbacks`,
   `CuiCallbacks`), discovers .NET plugins via MEF (`[InheritedExport]`),
   and — if `Editor.enableEditorMode=true` in `ut.ini` — spins up
   `FormMain`, the WinForms editor that re-parents the SWG window inside a
   `PanelGame`.

The result is a modding tool: write a C# (or C++) plugin, drop it in
`Plugins/<your-name>/`, and you get hotkey-bound, undo/redo-aware editor
panels that talk to the live SWG client.

## Mental model

```
Launcher.exe
   │ CreateProcess(SwgClient_r.exe, CREATE_SUSPENDED)
   │ patch EP → 0xEB 0xFE (jmp self)
   │ ResumeThread, wait for EIP == EP
   │ CreateRemoteThread → LoadLibraryA("UtinniCore.dll")
   │ restore EP
   ▼
SwgClient_r.exe (+ UtinniCore.dll injected)
   │ DllMain → CreateThread → utinni::main
   │   load ut.ini
   │   createDetours()         ← ~25 DetourXS hooks
   │   createPatches()         ← memory writes
   │   pluginManager.loadPlugins()   ← C++ plugins
   │   clr::load()             ← hosts .NET CLR v4
   ▼
UtinniCoreDotNet.dll (managed bridge)
   │ Startup.EntryPoint
   │   Log.Setup()
   │   PluginLoader (MEF DirectoryCatalog of Plugins/*)
   │   GameCallbacks / GroundSceneCallbacks / ObjectCallbacks / CuiCallbacks
   │   if (Editor.enableEditorMode) Application.Run(new FormMain(...))
   ▼
.NET plugins (IPlugin / IEditorPlugin)
   │ contribute SubPanels, IEditorForms, HotkeyManagers, undo commands
   │ subscribe to Callbacks
   │ talk to native via Generated/UtinniCore.cs (CppSharp P/Invoke)
   ▼
The Jawa Toolbox + your plugins
```

## Component map

| Project                  | Language          | Output                       | Role                                                                       |
| ------------------------ | ----------------- | ---------------------------- | -------------------------------------------------------------------------- |
| `Launcher`               | C++               | `Launcher.exe`               | Suspended-process injector. Reads `ut.ini`, finds & validates `SwgClient_r.exe`, injects `UtinniCore.dll`. |
| `UtinniCore`             | C++ (`/clr` off)  | `UtinniCore.dll`             | Native runtime. DetourXS hooks, ImGui+ImGuizmo render pass, spdlog, INI, C++ plugin manager, CLR host. |
| `UtinniCore-Symbols`     | C++ (static lib)  | `UtinniCore-Symbols.lib`     | Provides the symbol/PDB target used by CppSharp at bindgen time — see [Regenerating Bindings](regen-bindings.md). |
| `UtinniCoreDotNet`       | C# (.NET 4.7.2, x86) | `UtinniCoreDotNet.dll`    | Managed bridge. CppSharp-generated P/Invoke, callbacks, plugin loader (MEF), WinForms editor (`FormMain`), hotkey + undo/redo, theme. |
| `UtinniCoreDotNetGen`    | C# (.NET 4.7.2)   | `UtinniCoreDotNetGen.exe`    | CppSharp driver that regenerates `UtinniCoreDotNet/Generated/UtinniCore.cs`. |
| `UtINI` (project)        | C++ (static lib)  | `UtINI.lib`                  | A small INI library (LeksysINI under the hood). Used by Launcher and Core. **Not** the editor host — the editor is in `UtinniCoreDotNet`. |
| `UtinniPlugins/`         | C++ + C#          | per plugin                   | Companion repo. Houses the **Jawa Toolbox** reference editor plugin and Sytner's stub. |

## Where we're going

The strategic direction is a **one-stop modding tool** that replaces the
~30 separate apps a modder juggles today. See [the vision](vision.md) for
the long-form, and [the code-quality assessment](assessment.md) for the
near-term work-list (15 critical bugs, 8 reworks, sequencing to a 1.0).

## Where to dig in

| Topic                                            | For…                              |
| ------------------------------------------------ | --------------------------------- |
| [Vision](vision.md)                              | Strategic direction — read second after the overview. |
| [Assessment](assessment.md)                      | Code-quality audit + work-list to 1.0. |
| [Architecture](architecture.md)                  | Everyone — start here for the technical map. |
| [Injection & boot](injection.md)                 | Core contributors, .NET plugin authors curious about lifecycle. |
| [Native core (UtinniCore)](core.md)              | C++ plugin authors, core contributors. |
| [Managed bridge (UtinniCoreDotNet)](bridge.md)   | .NET plugin authors.              |
| [Plugin framework](plugin-framework.md)          | All plugin authors.               |
| [Callbacks reference](callbacks.md)              | .NET plugin authors.              |
| [UI framework](ui-framework.md)                  | .NET *editor* plugin authors.     |
| [Hotkeys](hotkeys.md)                            | .NET editor plugin authors.       |
| [Undo / Redo](undo-redo.md)                      | .NET editor plugin authors.       |
| [SDK & templates](sdk.md)                        | Anyone starting a new plugin.     |
| [Build & run](build.md)                          | Everyone.                         |
| [Tutorial: your first editor plugin](tutorial.md)| New plugin authors.               |
| [Internals: hooks, RVAs, patches](internals.md)  | Core contributors, reversers.     |
| [Regenerating bindings](regen-bindings.md)       | Core contributors.                |
| [Hard-won lessons](lessons.md)                   | Everyone — the traps that cost real time. |
| [Toolchain inventory & cross-walk](toolchain-inventory.md) | SWG tool → Utinni coverage map; Wave-2 priority order. |
| [Glossary](glossary.md)                          | Reference.                        |

## Conventions in this docset

- **SDK** badges / sections describe public surface — things plugin authors
  call and consume.
- **Internal** badges / sections describe implementation — things plugin
  authors should *not* depend on, and which may change between Utinni
  builds. Core contributors maintain them.
- All client-side hardcoded addresses are quoted with their RVA (e.g.
  `0x00422E80`) and refer to the specific SWGEmu/SWG-Source binary Utinni
  was built against. The corresponding source-level identifier (e.g.
  `Game::install`) is from the [swg-client](../../swg-client) tree.

## License & forks

Utinni and UtinniPlugins are **MIT-licensed forks** of
[ptklatt/Utinni](https://github.com/ptklatt/Utinni) and
[ptklatt/UtinniPlugins](https://github.com/ptklatt/UtinniPlugins). The
upstream repos appear dormant; our intent is to contribute improvements back
where practical and otherwise advance the forks.

## Cross-references

- **SWG client source**: [`D:/Code/swg-client/`](../../../swg-client/) — the
  2015 SOE source leak ("whitengold") which Utinni reverse-engineers
  against. Its [`docs/`](../../../swg-client/docs/index.html) covers the
  client side in depth (boot sequence, animation, foliage, middleware,
  build system); we cross-link rather than duplicate.
- **Plugins**: [`D:/Code/UtinniPlugins/docs/`](../../UtinniPlugins/docs/index.html)
  documents the Jawa Toolbox as the canonical worked example.
