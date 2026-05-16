# Glossary

A reference of Utinni-specific and SWG-specific terms used throughout the
docs.

## Utinni terms

| Term                          | Meaning                                                                                                |
| ----------------------------- | ------------------------------------------------------------------------------------------------------ |
| **CppSharp**                  | The Mono project's C++ → C# binding generator. Used to produce `Generated/UtinniCore.cs`.              |
| **DetourXS**                  | A small x86 function-detour library used by `UtinniCore` to hook SWG client functions.                 |
| **detour**                    | A function-pointer redirection. In Utinni: `swg/<subsystem>/<file>.cpp::detour()` installs the hooks for that subsystem. |
| **EditorPlugin**              | A managed plugin implementing `IEditorPlugin` — contributes UI to `FormMain`.                          |
| **`EB FE`**                   | The x86 byte sequence for `jmp $` (jump to self). Used by `Launcher.exe` to keep the SWG main thread parked at the entry point during injection. |
| **`FormMain`**                | The WinForms editor shell (`UtinniCoreDotNet.UI.Forms.FormMain`). Embeds the SWG window via `PanelGame`. |
| **Hotkey**                    | A named, persistent key binding with an `OnDownCallback`. Lives in a `HotkeyManager` per plugin.       |
| **`IEditorForm`**             | A plugin-provided free-standing tool window definition. Returned from `IEditorPlugin.GetForms()`.      |
| **`IEditorPlugin`**           | The editor-plugin contract. Extends `IPlugin`.                                                          |
| **ImGui** / **dearImgui**     | The immediate-mode UI library Utinni overlays on the D3D9 device.                                       |
| **ImGuizmo**                  | The 3D translate/rotate/scale gizmo, an extension of ImGui.                                             |
| **injection**                 | Loading a DLL into another process. Utinni uses suspended-process injection via `CreateRemoteThread` + `LoadLibraryA`. |
| **`IPlugin`**                 | The runtime-plugin contract. The baseline interface; an `IEditorPlugin` is also an `IPlugin`.            |
| **MEF**                       | Microsoft's Managed Extensibility Framework. `PluginLoader` uses `DirectoryCatalog` + `[InheritedExport]` for plugin discovery. |
| **`PanelGame`**               | The `Panel` subclass in `UtinniCoreDotNet.UI.Controls` that re-parents the SWG client window into the editor UI. |
| **`PluginConfig`**            | The native struct (`{IsEnabled, DirectoryName}`) read from `ut.ini → [Plugins]`. Source of truth for both C++ and .NET plugin gating. |
| **`PluginLoader`**            | The managed plugin discovery class. Composes everything in `Plugins/<enabled-dirs>/` via MEF.            |
| **RVA**                       | Relative Virtual Address — an offset within a PE file. Utinni's hooks reference hard-coded RVAs into `SwgClient_r.exe`. |
| **`SubPanel`**                | A WinForms `UserControl` subclass that docks inline on the editor's right rail. Plugins return them from `IEditorPlugin.GetSubPanels()`. |
| **`SubPanelContainer`**       | A grouping of `SubPanel`s; surfaced via `IEditorPlugin.GetStandalonePanels()`.                          |
| **`UtinniCore`**              | The native (C++) DLL. Hosts the hooks, ImGui, spdlog, plugin manager, CLR.                              |
| **`UtinniCoreDotNet`**        | The managed (C# .NET 4.7.2 x86) DLL. CppSharp bindings + plugin framework + editor.                     |
| **`UtinniCoreDotNetGen`**     | The CppSharp driver that generates `UtinniCoreDotNet/Generated/UtinniCore.cs`.                          |
| **`UtinniCore-Symbols`**      | A separate library project providing the mangled-symbol target for CppSharp's P/Invoke.                 |
| **`UtinniPlugin`**            | The C++ plugin base class (`utinni::UtinniPlugin`).                                                     |
| **VSIX**                      | A Visual Studio extension. `sdk/UtinniPluginTemplates/Vsix/` packages the .NET plugin templates.        |
| **`ut.ini`**                  | Utinni's master config. Lives next to `Launcher.exe` / `UtinniCore.dll`.                                |
| **`utinni.cfg`**              | Utinni's override for SWG's own `client.cfg`. Loaded by hooking `loadOverrideConfig`.                   |
| **`input.ini`**               | Per-plugin (or per-form) hotkey-binding persistence. Written by `HotkeyManager.Save()`.                 |

## SWG-specific terms (Utinni-relevant subset)

For comprehensive SWG terminology see [swg-client/docs/glossary.html](../../../swg-client/docs/glossary.html).

| Term                          | Meaning                                                                                                |
| ----------------------------- | ------------------------------------------------------------------------------------------------------ |
| **`Appearance`**              | The visual representation of an `Object` (mesh, texture, animation set). Loaded from a TRE asset.       |
| **CRC string**                | A 32-bit hash + string-pool index. SWG uses these everywhere for object names, animation IDs, etc.      |
| **`CreatureObject`**          | SWG's class for animated, AI-driven, or player entities. Subclass of `Object`.                          |
| **CUI**                       | "Client UI" — SWG's in-game UI library. `CuiManager`, `CuiHud`, `CuiChatWindow`, `CuiRadialMenuManager`, etc. |
| **`DebugCamera`**             | SWG's free-fly camera. Utinni exposes it as "free-cam" with controls in the editor.                     |
| **detail level**              | LOD (level-of-detail) selection within an `Appearance`. After a transform change, `DetailLevelChanged()` re-evaluates which LOD to draw. |
| **`Extent`**                  | The bounding shape / collision volume of an object. `Graphics::drawExtent` visualises it.               |
| **`Game`**                    | The SWG client's top-level game-loop manager. `Game::install`, `Game::setupScene`, etc.                 |
| **`Graphics`**                | SWG's wrapper around the D3D9 device + render-loop scaffolding.                                          |
| **`GroundScene`**             | A `NetworkScene` subclass that runs the player-on-a-planet gameplay scene. Distinct from `SpaceScene`.   |
| **IFF**                       | "Interchange File Format" — the binary container most SWG asset files use (e.g. `.iff`, `.trn`, `.app`, `.ws`). |
| **`Network`**                 | The client object-repository singleton. `getObjectById(id)` returns a network-replicated object.        |
| **`Object`**                  | Base of SWG's runtime object hierarchy. Every world-object is an `Object` or subclass.                  |
| **`ObjectTemplate`**          | Static description of an object kind: appearance filename, slots, type. Loaded by template-list lookups. |
| **NGE**                       | "New Game Experience" — the post-2005 revamp of SWG's gameplay. Pre-CU = pre-Combat Upgrade, the SWGEmu/SWG-Source target. |
| **`PortalLayout`**            | The interior layout of a building object (cells, portals between them). Used during render culling.    |
| **Pre-CU**                    | The pre-2005 game state Utinni targets.                                                                 |
| **scene**                     | A loaded planet (or space zone) with its terrain, world snapshot, and dynamic objects. `Game::setupScene` / `cleanupScene` are the lifecycle hooks. |
| **`SharedObjectTemplate`**    | The shared (client+server) half of an `ObjectTemplate`. Holds `appearanceFilename`, `portalLayoutFilename`, etc. |
| **`SkeletalAppearance`**      | An `Appearance` driven by a skeleton/animation system (creatures, players).                              |
| **system message**            | A chat-stream message originating from the engine (e.g. "You gain some experience."). Routed via `SystemMessageManager`. |
| **template**                  | See `ObjectTemplate`.                                                                                   |
| **Terrain**                   | The procedural terrain system. `.trn` describes a planet's terrain.                                     |
| **TRE**                       | The pack-file format SWG ships assets in (`*.tre`). Mounted in a search order by `TreeFile`.            |
| **`TreeFile`**                | SWG's union-mounted virtual file system over `.tre` archives. Every asset request goes through `TreeFile::searchTree`. |
| **`WorldSnapshot`**           | A `.ws` placement record: positions/rotations of static objects for a planet. Edited by the Jawa Toolbox. |
| **`WorldSnapshotReaderWriter`** | The runtime save/load of `.ws` files.                                                                  |

## Visual Studio / build terms

| Term                          | Meaning                                                                                                |
| ----------------------------- | ------------------------------------------------------------------------------------------------------ |
| **`Directory.Build.props`**   | MSBuild file auto-imported into every project under a directory. Used by the VSIX wizard to inject `PluginOutputDir` / `UtinniCoreDotNetPath`. |
| **`PlatformToolset=v142`**    | Visual Studio 2019's C++ toolchain. Utinni pins to this.                                                |
| **`RelWithDbgInfo`**          | A custom configuration: optimised like Release, but with PDBs. Recommended for plugin development.      |
| **VSIX wizard**               | The `IWizard` implementation that runs when a project is created from a template. `DotNetSolutionWizard.cs` writes the Directory.Build.props. |

## See also

- [swg-client/docs/glossary.html](../../../swg-client/docs/glossary.html) for a
  much larger SWG-side glossary.
