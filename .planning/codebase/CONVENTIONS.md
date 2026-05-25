# Coding Conventions

**Analysis Date:** 2026-05-16

Utinni is a two-language project: native **C++17** (`UtinniCore`, `UtINI`, `Launcher`, `UtinniCore-Symbols`) for the in-process injected DLL, and **C# .NET Framework 4.7.2** (`UtinniCoreDotNet`, `UtinniCoreDotNetGen`) for the managed editor surface and plugin host. The two halves differ substantially in style, so each section below splits **C++** and **C#** rules. There is no `.editorconfig`, no `.clang-format`, no StyleCop, no analyzer config in the repo — conventions are de-facto, derived from reading the existing source. A prior audit (`docs/ai/assessment.md`) flagged unevenness, and grep confirms it: 22 C++ files and 9 C# files carry "ToDo" comments, several marked with hedges like "do this proper at some point" or "taken from IDA pseudo code" (e.g. `UtinniCore/swg/object/object.cpp:257`, `UtinniCore/swg/object/player_object.cpp:30`).

## Naming Patterns

**Files (C++):**
- `snake_case` for all C++ source/header pairs: `plugin_manager.cpp/.h`, `client_object.cpp/.h`, `world_snapshot.cpp/.h`, `string_utility.cpp/.h`
- One subsystem per directory under `UtinniCore/swg/` (e.g. `swg/object/object.cpp`, `swg/game/game.cpp`, `swg/scene/ground_scene.cpp`)
- Header guards use `#pragma once`, never `#ifndef` guards (see top of `UtinniCore/swg/object/object.h:25`)

**Files (C#):**
- `PascalCase` matching the primary type: `PluginLoader.cs` contains class `PluginLoader`, `UndoRedoManager.cs` contains class `UndoRedoManager`
- WinForms partials follow the standard `FormName.cs` + `FormName.Designer.cs` + `FormName.resx` triple (e.g. `UtinniCoreDotNet/UI/Forms/FormMain.cs` + `FormMain.Designer.cs` + `FormMain.resx`)
- WinForms files are explicitly typed in `UtinniCoreDotNet.csproj` with `<SubType>Form</SubType>`, `<SubType>UserControl</SubType>`, or `<SubType>Component</SubType>` (see `UtinniCoreDotNet.csproj:69-184`)
- Forms prefixed `Form`, custom controls prefixed `Utinni` (`UtinniButton`, `UtinniTextbox`, `UtinniToggle` in `UtinniCoreDotNet/UI/Controls/`)
- The single lowercase outlier is `UtinniCoreDotNet/main.cs` (which still contains a PascalCase `Startup` class — the file name is the anomaly)

**Functions (C++):**
- `camelCase` for free functions, static methods, and member methods: `getPath()`, `createDetours()`, `addToWorld()`, `setObjectToWorldDirty()`, `getObjectTemplateByFilename()`
- Hook handlers prefixed `hk`: `hkMainLoop`, `hkInstall`, `hkSetScene`, `hkCleanupScene` (see `UtinniCore/swg/game/game.cpp:115-195`)
- Game-function-pointer typedefs prefixed `p`: `using pInstall = void(__cdecl*)(int);` then `pInstall install = (pInstall)0x00422E80;` (see `UtinniCore/swg/game/game.cpp:38-67`)
- Detour setup method per subsystem is always named `detour()`; memory-patch setup is always `patch()` (see the symmetrical call lists in `UtinniCore/utinni.cpp:58-97`)
- Plugin factory in C++ is `extern "C" UTINNI_PLUGIN { return new ...(); }` returning a `UtinniPlugin*` (see `sdk/examples/ExampleCppPlugin/plugin.cpp:67-73`)

**Functions (C#):**
- `PascalCase` for all public/protected/private methods: `LoadPlugins()`, `Initialize()`, `AddInstallCallback()`, `ProcessInput()`, `OnCleanupCallback()`
- Event-handler private methods follow `OnXxx` (`OnCleanupCallback`, `OnDownCallback`)
- Initialization is always called `Initialize()` on static classes, `Setup()` on `Log` (see `UtinniCoreDotNet/Utility/Log.cs:41` and `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:44`)

**Variables (C++):**
- `camelCase` for locals, params, and member fields: `pluginDir`, `dirFilename`, `installCallbacks`, `loadNewScene`
- File-scope statics are lowercase camelCase without prefix: `static std::vector<...> installCallbacks;` (see `UtinniCore/swg/game/game.cpp:71-76`)
- Pointer-to-impl member is `pImpl` (see `UtinniCore/plugin_framework/plugin_manager.h:51`)
- "Unknown" struct fields use `unk01`, `unk02`, ... when the reverse-engineered layout has gaps (see `UtinniCore/swg/object/object.h:80-106`)
- `swgptr` (alias for `uint32_t`, defined `UtinniCore/utinni.h:36`) is used everywhere a raw game-side pointer is meant — do not use raw `uint32_t` for that purpose
- Compile-time constants use `constexpr static const` (see `UtinniCore/utility/string_utility.h:31`)

**Variables (C#):**
- `camelCase` for locals, params, and private fields: `pluginDir`, `pluginConfigs`, `installCallbacks`, `outputSinkCallbacks`
- `PascalCase` for public fields, properties, and constants: `Hotkeys`, `Enabled`, `OnGameFocusOnly`, `UndoCommands`, `RedoCommands`
- Private fields are NOT prefixed with `_` — they are plain `camelCase` and `private readonly` where the project's style is followed (see `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:34-39`)
- Callbacks held to prevent GC are stored as `private static UtinniCore.Delegates.Action_ xxxAction;` — the comment at `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:46` explicitly explains why ("Storing this in a variable is somehow needed to prevent corruption on WinForms resize")
- `var` is used freely for locals when the type is obvious from the right side; explicit type used otherwise — no strict rule

**Types (C++):**
- Classes: `PascalCase` (`Object`, `ClientObject`, `PluginManager`, `WorldSnapshotReaderWriter`)
- Inner namespaces use `camelCase`: `utinni::log`, `utinni::creatureObject`, `swg::game`, `swg::math`, `swg::object`, `swg::objectTemplateList`
- Note the dual-namespace pattern: `utinni::Object` is the public API class, `swg::object` is the namespace holding raw RE'd function pointers — the public class delegates to the namespace (see `UtinniCore/swg/object/object.cpp:240-345`)
- Function-pointer aliases use `pFoo` for "pointer-to-Foo" (see naming convention above)

**Types (C#):**
- Classes, structs, interfaces, enums: `PascalCase`
- Interfaces prefixed `I`: `IPlugin`, `IEditorPlugin`, `IUndoCommand`, `IEditorForm`
- Event arg classes follow `XxxEventArgs` (e.g. `AddUndoCommandEventArgs` in `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs:35`)
- Generated namespaces from the CppSharp bridge mirror C++ namespaces with PascalCase: `UtinniCore.Utinni`, `UtinniCore.Swg.Math`, `UtinniCore.Utinni.Log` (see `UtinniCoreDotNet/Generated/UtinniCore.cs`)

## Code Style

**Formatting:**
- No `.editorconfig`, no `.clang-format`, no `.csharpfmt` in the repo (only `external/imgui/.editorconfig` for the vendored dependency)
- Indentation: 4 spaces, both languages, never tabs (verified across 30+ sampled files)
- Brace style: **Allman** (opening brace on its own line) for both C++ and C#:
  ```cpp
  void Game::loadScene()
  {
      const char* terrainFilename = ...;
      ...
  }
  ```
  ```csharp
  public void Load()
  {
      Plugins = new List<IPlugin>();
      ...
  }
  ```
- One blank line between methods is the norm; no blank line after opening brace
- Long parameter lists are wrapped manually with alignment-to-prior-arg (see `UtinniCoreDotNet/PluginFramework/IPlugin.cs:36`, `sdk/examples/ExampleEditorPlugin/ExampleEditorPlugin.cs:49-51`)

**Linting:**
- **No analyzers configured.** Each `.vcxproj` sets `<WarningLevel>Level3</WarningLevel>` (see `UtinniCore/UtinniCore.vcxproj:77`); `UtinniCoreDotNet.csproj` sets `<WarningLevel>4</WarningLevel>` (line 23/33). No `<TreatWarningsAsErrors>` anywhere
- Debug builds enable `<SDLCheck>true</SDLCheck>` and `<ConformanceMode>true</ConformanceMode>` (C++17 strict mode via `<LanguageStandard>stdcpp17</LanguageStandard>`)
- Release C++ explicitly disables LTCG and intrinsics (`<WholeProgramOptimization>false</WholeProgramOptimization>`, `<IntrinsicFunctions>false</IntrinsicFunctions>`) — likely because the DLL is dynamically loaded into a 2005-era process and IRP optimizations break detour layout

## File Headers

- **Every** source file in the repo begins with the same 23-line MIT-license comment block crediting "Philip Klatt, 2020" (verified across 40+ sampled `.cs/.cpp/.h` files including `UtinniCore/utinni.h:1-23`, `UtinniCoreDotNet/main.cs:1-23`, `sdk/examples/*.cs/.cpp`). New files MUST include this block verbatim. The opening token is `/**` and the closing is `**/` (note the doubled asterisk on the close — non-standard but consistent)
- Authorship line on `AssemblyInfo.cs` is `Copyright © 2020, Philip Klatt` (see `UtinniCoreDotNet/Properties/AssemblyInfo.cs:13`). This project is a fork at `kennethlong/Utinni`; the per-file headers are unchanged from upstream

## Import Organization

**C++ Include Order:**
1. Pair header first (e.g. `object.cpp` opens with `#include "object.h"`)
2. Standard library headers (`<filesystem>`, `<sstream>`, `<vector>`)
3. Third-party / external headers using angle brackets (`<spdlog/spdlog.h>`, `<imgui/imgui_user.h>`)
4. Local project headers using quotes (`"utinni.h"`, `"swg/misc/swg_math.h"`)

Example from `UtinniCore/swg/game/game.cpp:25-34`:
```cpp
#include "game.h"
#include "utinni.h"
#include <imgui/imgui_user.h>
#include "swg/client/client.h"
#include "swg/misc/config.h"
...
```
The stray `;` that used to trail the `utinni.h` include in `game.cpp` was removed in Phase 6 (06-05), and the full-repo `.clang-format` adopted in 06-05 now normalises C++ formatting (Allman braces, 4-space indent), so the historical 3-space / 4-space / tab-mixed inconsistencies are gone.

**C# using Order:**
1. `System.*` first
2. Third-party (none in this codebase outside generated CppSharp bindings)
3. `UtinniCore.*` generated bindings
4. `UtinniCoreDotNet.*` project namespaces

Example from `UtinniCoreDotNet/UI/Forms/FormMain.cs:25-38`:
```csharp
using System;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Threading;
using System.Windows.Forms;
using UtinniCoreDotNet.Callbacks;
using UtinniCoreDotNet.Hotkeys;
using UtinniCoreDotNet.PluginFramework;
...
using Point = System.Drawing.Point;
```
Aliases (`using X = Y;`) go at the end of the using block.

**Path Aliases:**
- C++ uses absolute project-rooted include paths: `#include "swg/object/object.h"`, `#include "utility/log.h"`. Project sets `<AdditionalIncludeDirectories>$(SolutionDir);$(SolutionDir)external;$(ProjectDir);` (see `UtinniCore/UtinniCore.vcxproj:81`)
- C# uses fully qualified namespaces; no `global using` (project targets .NET Framework 4.7.2 which predates that feature)

## Error Handling

**C++ Patterns:**
- **No exceptions.** Build is configured with `SPDLOG_NO_EXCEPTIONS` (see `UtinniCore/UtinniCore.vcxproj:79`) and there is not a single `try/catch/throw` in the entire `UtinniCore` tree (verified by grep — 0 matches)
- Error reporting is done by:
  1. Logging via `utinni::log::error(...)` / `log::critical(...)` (see `UtinniCore/utility/log.h:33-37`)
  2. Returning `nullptr`, `false`, or empty optional values (see `Object::getObjectById` early-out at `UtinniCore/swg/object/object.cpp:247-252`)
  3. Skipping the operation silently with a `continue` after a log line (see `UtinniCore/plugin_framework/plugin_manager.cpp:67-70`)
- HRESULT checks use the `SUCCEEDED(hr)` macro with nested success guards rather than early-return (see `UtinniCore/clr.cpp:44-91` — five levels of nesting because every COM call is checked but no helper exists)
- Win32 errors surface via `utility::showLastErrorMessageBox()` (see `UtinniCore/utility/utility.cpp:44-56`) — calls `FormatMessageA` + `MessageBox`. Used sparingly, mainly during DLL injection

**C# Patterns:**
- **try/catch is essentially absent** from production code: grep finds it in only 2 files, both auto-generated (`UtinniCoreDotNet/Generated/StdEdited.cs`, `UtinniCoreDotNet/Generated/UtinniCore.cs`). No try/catch in `UndoRedoManager`, `PluginLoader`, `HotkeyManager`, `FormMain`, callbacks, or commands
- Null guards are explicit returns: `if (UndoCommands.Count == 0) { return; }` (see `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:78-81`)
- `String.IsNullOrEmpty` guard then log-and-return (see `UtinniCoreDotNet/Hotkeys/Hotkey.cs:68-72`)
- Exceptions can leak from `Enum.Parse` (no try/catch around `(Keys)Enum.Parse(...)` at `UtinniCoreDotNet/Hotkeys/Hotkey.cs:82,91`) — this is a documented fragile area, not a convention to follow

## Logging

**Framework:**
- C++ uses **spdlog** (vendored in `external/spdlog`), wrapped in `utinni::log` namespace (see `UtinniCore/utility/log.cpp`). A custom `OutputSink` (lines 34-58) forwards every log line to registered callbacks so the .NET layer can mirror them into a WinForms log window
- C# uses a static `Log` class (`UtinniCoreDotNet/Utility/Log.cs`) that delegates back into the native `log::` via the generated CppSharp binding (`UtinniCore.Utinni.Log.log`). It also reflects on the calling stack frame to optionally prepend `[ClassName][MethodName]` (see lines 50-69). The audit (`docs/ai/assessment.md:259-265`) flags this StackTrace walk as hot-path expensive and recommends `[CallerMemberName]` instead

**Patterns:**
- C++: `utinni::log::info("Loading C++ plugins");` — short, lowercased verb-first messages (see `UtinniCore/utinni.cpp:60,93,122,127`)
- C++: when concatenating, build the `std::string` inline and call `.c_str()`:
  ```cpp
  log::error(std::string("Failed to parse [Plugins] priority list value due to missing separator: " + curStr).c_str());
  ```
  (see `UtinniCore/plugin_framework/plugin_manager.cpp:68`)
- C#: `Log.Info(Plugins.Count() + " .NET Plugin(s) loaded");` — string concatenation, no `string.Format`/interpolation (see `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:72`)
- C# `Log` exposes both `Info()` (which prepends caller class via StackTrace) and `InfoSimple()` (which does not). Same dual for every level (see `UtinniCoreDotNet/Utility/Log.cs:71-119`)

## Comments

**When to Comment:**
- Block of `//` comments above sections of related code explaining intent (see `UtinniCore/plugin_framework/plugin_manager.cpp:57,87,109,117`)
- Inline `//` after a line when the meaning is non-obvious or temporary
- Reverse-engineering provenance is marked: comments like `"taken from IDA pseudo code"`, `"dirty taken from IDA"`, `"// might not need"` are common (see `UtinniCore/swg/object/object.cpp:257`, `UtinniCore/swg/object/player_object.cpp:30`, `UtinniCore/swg/object/object.h:101-103`). Preserve them — they are debugging breadcrumbs, not noise

**ToDo Tag:**
- The project uses `// ToDo` (capital T, capital D, no colon, no space conventions) — NOT the common `TODO:` form. Verified across 30+ occurrences: `UtinniCore/swg/game/game.cpp:128`, `UtinniCoreDotNet/UI/Forms/FormMain.cs:68`, `UtinniCore/swg/object/object.cpp:257`, etc.
- A handful use `// ToDo:` with a colon (`UtinniCore/swg/graphics/directx9.cpp:217`, `UtinniCoreDotNet/UI/Forms/FormMain.cs:68`) — both forms accepted, the colonless form is more common
- `// FIXME`, `// HACK`, `// XXX` are not used. New code should follow the existing `// ToDo` convention for consistency

**JSDoc/TSDoc:**
- Not applicable (no JS/TS)
- C++ does not use Doxygen-style `///` or `/**` comments for API surfaces — public methods in headers are bare declarations. The only `/**`-style block comment is the file-header MIT notice
- C# does not use XML doc comments (`///`). `IUndoCommand` (see `UtinniCoreDotNet/UndoRedo/IUndoCommand.cs:28-39`) uses plain `//` comments above each interface method instead

## Function Design

**Size:**
- Most C++ functions are short (5-30 lines) and act as thin wrappers around the raw RVA-bound function pointers in the matching `swg::` namespace (see the entire `Object::*` method block at `UtinniCore/swg/object/object.cpp:240-350` — every method is 1-3 lines)
- The exception: hook handlers and detour-setup functions can be larger (`PluginManager::loadPlugins` is 100+ lines at `UtinniCore/plugin_framework/plugin_manager.cpp:51-154`, doing both INI parsing and `LoadLibrary` traversal in one method — flagged as candidate for split)
- C# methods rarely exceed 50 lines outside of WinForms designers and `FormMain`. `FormMain.cs` has multiple flagged-but-untouched methods (see ToDo annotations at lines 258, 335, 362)

**Parameters:**
- C-style API surface preferred for cross-language ABI: `const char*` over `const std::string&` for any function that crosses the C++/C# boundary or appears in `UTINNI_API extern` declarations (compare `utinni::log::info(const char* text)` in `UtinniCore/utility/log.h:36` vs the internal `std::string` it then wraps)
- Default arguments are used where natural: `loadScene(const char* terrainFilename, const char* avatarObjectFilename = "object/creature/player/shared_human_male.iff")` (see `UtinniCore/swg/game/game.h:50`)
- C# uses `out` for tuple-return-likes (e.g. `TryDequeue(out var func)` in `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:104`)

**Return Values:**
- C++: Return raw pointers for game objects (caller does NOT own — they live in the SWG client's allocator). Const-correctness is followed: `const Camera* Game::getConstCamera()` vs `Camera* Game::getCamera()` (see `UtinniCore/swg/game/game.cpp:294-301`)
- C++: Return `std::string` by value when assembling a new string; return `const std::string&` for getter-style accessors over file-scope statics (see `UtinniCore/utinni.cpp:155-163`)
- C#: Methods that "give back" managed lists return `List<T>` (not `IList<T>` or `IEnumerable<T>`) — see `IEditorPlugin.GetForms()`, `.GetSubPanels()` in `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs:48-50`

## Module Design

**Exports:**
- C++ public API surface is gated by the `UTINNI_API` macro (`__declspec(dllexport)` when `EXPORT_UTINNI` is defined, `__declspec(dllimport)` otherwise — see `UtinniCore/utinni.h:42-46`). Every public class and free function needs it: `class UTINNI_API Object`, `UTINNI_API extern void info(const char* text);`. Without it, the CppSharp generator at `UtinniCoreDotNetGen/` will not pick up the symbol
- C# exports are interface-driven via MEF (`System.ComponentModel.Composition`): interfaces are marked `[InheritedExport(typeof(IPlugin))]` so any class implementing them is auto-discovered when its DLL is dropped into `Plugins/` (see `UtinniCoreDotNet/PluginFramework/IPlugin.cs:44-49`)
- Plugin entry point in C++ uses `extern "C"` with the `UTINNI_PLUGIN` macro for ABI-clean symbol export (see `sdk/examples/ExampleCppPlugin/plugin.cpp:67-73`)

**Barrel Files:**
- Not used. `UtinniCore/utinni.h` is the closest thing to a top-level facade — it forward-declares `PluginManager` and exposes the four global accessors (`getPath`, `getSwgCfgFilename`, `getConfig`, `getPluginManager`). It is intentionally minimal; consumers `#include "utility/log.h"` etc. directly
- No C# `Index.cs` or re-export equivalent — each subsystem lives in its own namespace and consumers reference it directly

## Cross-Language Bridge Conventions

- C++ → C# binding is auto-generated by **CppSharp** via the `UtinniCoreDotNetGen` console app (configured in `UtinniCoreDotNetGen/Program.cs`). Generated output lives in `UtinniCoreDotNet/Generated/UtinniCore.cs` and `Generated/StdEdited.cs`. **Do not hand-edit `Generated/UtinniCore.cs`** — it is regenerated on every build by a post-build step on `UtinniCore.vcxproj` (see `UtinniCore/UtinniCore.vcxproj:92-93`)
- C# delegate types passed to C++ MUST be stored as a field (not a local) to survive the GC. The canonical pattern with the canonical explanatory comment lives at `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:39-58` — preserve and replicate it for any new callback channel
- All cross-boundary types use C-compatible primitives: `const char*` (becomes `string` in C#), `swgptr` (becomes `uint`), function pointers (become `UtinniCore.Delegates.Action_*`)

## Graphics & Native Dependencies

- **No DXSDK June 2010.** The legacy DirectX SDK (June 2010) was retired in Phase 6 (Plan 06-03 Task 1, CON-O-08). It is no longer an include/lib path in any `.vcxproj`, and CI no longer verifies it. New code that needs GPU-side vector/matrix math must use **DirectXMath** (`<DirectXMath.h>`, shipped in the Windows SDK) rather than reintroducing the legacy `d3dx9.h` / `D3DXVECTOR*` / `D3DXMATRIX*` helpers. The sole prior `D3DXVECTOR3` use — a dummy vertex for the RESZ depth-resolve draw in `UtinniCore/swg/graphics/depth_texture.cpp` — is now a local 3-float `Vec3` struct (identical byte layout, so `DrawPrimitiveUP` stride math is unchanged).

---

*Convention analysis: 2026-05-16*
