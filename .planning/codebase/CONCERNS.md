# Codebase Concerns

**Analysis Date:** 2026-05-16

**Scope:** Whole-repo audit of the Utinni framework (Launcher, UtinniCore native, UtinniCoreDotNet managed, UtINI helper, UtinniCoreDotNetGen CppSharp generator, sdk/ plugin templates and examples). Builds on the prior three-agent audit at `docs/ai/assessment.md` (C-01..C-15, R-A..R-H) — verified against current source, then extended.

This document is the canonical "what's broken / fragile / risky" reference for Utinni. The numbered items below are work-able units: each has a file, an impact, and a fix approach.

---

## Tech Debt

### TD-01 — DLL bootstrap runs inside the loader lock

- **Issue:** `DllMain` spawns a thread that calls `main()`, which immediately calls `LoadLibrary` on plugin DLLs (`pluginManager.loadPlugins()`) and brings up the CLR (`CoInitializeEx` + `clr::load`). Both are explicitly forbidden inside `DLL_PROCESS_ATTACH`. Works today only because the launcher's `WaitForSingleObject(hThread, INFINITE)` (`Launcher/main.cpp:195`) happens to serialize things — load-bearing luck.
- **Files:** `UtinniCore/utinni.cpp:99-130` (`main`), `UtinniCore/utinni.cpp:138-151` (`DllMain`), `Launcher/main.cpp:174-210` (`inject`).
- **Impact:** Sporadic startup deadlocks, "Added multiple potential fixes for startup issues" commit (`fd54055`) is a symptom not a cure. Any change to launcher timing can resurface this.
- **Fix approach:** Defer all heavy startup. Export a `utinni_init` symbol; have the launcher fire `CreateRemoteThread(utinni_init)` after `LoadLibraryA` returns. Or wait for the first SWG callback (`Game::install`) to bring up the CLR.
- **Tracking:** Assessment C-01.

### TD-02 — Cross-CRT free in config override path

- **Issue:** SWG allocates a buffer via its own CRT inside the override-cfg detour; Utinni then calls `delete[] data` (`UtinniCore/swg/misc/config.cpp:71`). Different heaps. Undefined behaviour. Line 72 then re-invokes SWG's destructor on the file pointer, possibly double-freeing.
- **Files:** `UtinniCore/swg/misc/config.cpp:59-76` (`hkLoadOverrideConfig`).
- **Impact:** Random heap corruption / crash on startup when `useSwgOverrideCfg` is true. The comment on line 67 (`// ToDo clean this IDA pseudo paste up`) admits the function is unfinished.
- **Fix approach:** Reverse-engineer SWG's matching free function (likely a virtual method on the TreeFile handle) and call it instead of `delete[]`. Verify via IDA.
- **Tracking:** Assessment C-02.

### TD-03 — `Network::cast` returns uninitialized stack memory

- **Issue:** `swgptr networkId;` declared, never written, the call's return value is discarded. The comment is candid: `// This is broken`. `WorldSnapshotReaderWriter::Node::getNodeNetworkId` forwards the garbage.
- **Files:** `UtinniCore/swg/misc/network.cpp:65-69`.
- **Impact:** Any code path that relies on `Network::cast` reads stack garbage. Confirmed by the workaround commit `0372c93` ("Added loop to find the corrent parent node -- Temp work around til network issues are fixed") and the `world_snapshot.cpp:604,642` comments "Workaround to the unreliable ptr return of reader->addNode".
- **Fix approach:** Reverse-engineer the ABI of `pCast = int64_t(__thiscall*)(swgptr*, int, int)` at `0xAA4900`. It likely writes through the first parameter and returns void or returns the same pointer — confirm with IDA decompilation, then read `networkId` after the call.
- **Tracking:** Assessment C-03.

### TD-04 — `DequeuePostDrawLoopCalls` drains the wrong queue

- **Issue:** Copy-paste bug: drains `preDrawLoopCallQueue` (line 99) instead of `postDrawLoopCallQueue`. Means `AddPostDrawLoopCall` is effectively a no-op for post-draw semantics — items enqueued via `AddPostDrawLoopCall` accumulate forever.
- **Files:** `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs:97-106`.
- **Impact:** Memory leak (queue grows unbounded) and any plugin that depended on post-draw ordering gets pre-draw timing. Has been broken since at least the move to `ConcurrentQueue` (commit `0a802fc`, ~2020).
- **Fix approach:** Two-line fix on lines 99 and 101 (`preDrawLoopCallQueue` → `postDrawLoopCallQueue`). Factor a `Drain(ConcurrentQueue<Action>)` helper so the bug class can't recur.
- **Tracking:** Assessment C-04.

### TD-05 — `GameDragDropEventHandlers` static-field pattern is silently broken

- **Issue:** `Initialize(PanelGame)` wires `panel.DragDrop += OnDragDrop` where `OnDragDrop` is currently `null` (no subscribers yet). Later `GameDragDropEventHandlers.OnDragDrop += handler` only updates the static field — never the panel's event. Result: plugin drag-drop handlers never fire on the live game window.
- **Files:** `UtinniCoreDotNet/UI/GameDragDropEventHandlers.cs:33-44`, called from `UtinniCoreDotNet/UI/Controls/PanelGame.cs:68`.
- **Impact:** Drag-drop to the game viewport is a documented feature in the README ("dragDropEventHandlers to be utilized by plugins") but doesn't work. The Jawa Toolbox object-browser may be working via a different code path; needs verification.
- **Fix approach:** Replace static `DragEventHandler` fields with proper `static event` and a single forwarder on the panel, or expose `PanelGame` and let plugins subscribe directly.
- **Tracking:** Assessment C-05.

### TD-06 — `PluginLoader.Load` swallows every plugin exception silently

- **Issue:** Single `AggregateCatalog` + `ComposeParts` for *all* plugin DLLs. One bad plugin (missing dep, ctor throw, x86/x64 mismatch, see TD-07) → `ComposeParts` throws → the entire editor tears down with no user-visible error pointing at the offending plugin.
- **Files:** `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:39-73`.
- **Impact:** New-modder onboarding cliff: any plugin that fails composition kills the whole editor with no diagnostic. The only message is `Log.Info(Plugins.Count() + " .NET Plugin(s) loaded")` which never gets reached.
- **Fix approach:** Per-plugin `AssemblyCatalog` inside try/catch. On exception, log the DLL name, `ex.Message`, and `ReflectionTypeLoadException.LoaderExceptions[*]`. Let surviving plugins load.
- **Tracking:** Assessment C-06.

### TD-07 — `Hotkey.ProcessString` throws on any unknown enum token

- **Issue:** `Enum.Parse(typeof(Keys), modifiers, true)` and `Enum.Parse(typeof(Keys), key, true)` both throw on typos. A bad `input.ini` value (e.g. `Ctrl + T` instead of `Control + T`) crashes the plugin ctor → MEF composition fails → entire editor dies silently (because of TD-06). Compounding failure.
- **Files:** `UtinniCoreDotNet/Hotkeys/Hotkey.cs:66-92`.
- **Impact:** Hand-edited `input.ini` files become editor-killers. The `ProcessString` happens inside the plugin constructor (line 51) via `formHotkeyManager.Add(...)`, see `FormMain.cs:126-131`.
- **Fix approach:** `Enum.TryParse` for both modifier and key. On failure: `Log.Warning` + leave `Key`/`ModifierKeys` as `Keys.None` and set `Enabled = false`. Never throw from a hotkey ctor.
- **Tracking:** Assessment C-08.

### TD-08 — `UndoRedoManager` is thread-unsafe and `AllowMerge` is dead

- **Issue:** Two unrelated bugs in the same class:
  1. `Stack<IUndoCommand>` is mutated from the game thread (when commands are pushed via callbacks) and the UI thread (when user clicks Undo) without locking.
  2. `IUndoCommand.AllowMerge()` is declared (`IUndoCommand.cs:36`) but never called — `AddUndoCommand` blindly invokes `Peek().Merge(args.UndoCommand)` and trusts the return (line 65). The merge contract is half-implemented.
  3. `RedoCommands.Clear()` happens before the merge check (line 64), so a merged-away command still clears the redo stack — destroys redo history unexpectedly.
- **Files:** `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:60-74`, `UtinniCoreDotNet/UndoRedo/IUndoCommand.cs:36`.
- **Impact:** Race-condition data loss on undo. Inconsistent merge behaviour (some commands erroneously merge, others fail to). Redo stack disappears when it shouldn't.
- **Fix approach:** `lock(syncRoot)` around all stack mutations in `AddUndoCommand`, `Undo`, `Redo`, `OnCleanupCallback`. Either call `Peek().AllowMerge()` before `Merge`, or remove `AllowMerge` from the interface. Move `RedoCommands.Clear()` *after* the merge check.
- **Tracking:** Assessment C-07.

### TD-09 — UI thread busy-waits on the game thread during minimize/restore

- **Issue:** `WndProc` handler for `WM_SYSCOMMAND` does `BlockPresent(true)` then `while (!IsPresentBlocked()) Thread.Sleep(1)` with no timeout. If the game thread is awaiting the UI thread for any reason, hard deadlock. The comment `// ToDo: Find better solution in the future` (line 68) admits it.
- **Files:** `UtinniCoreDotNet/UI/Forms/FormMain.cs:57-78`. Mirrored hack in `UtinniCore/swg/graphics/directx9.cpp:216-226` (`blockPresentCall` / `isPresenting` two-flag dance).
- **Impact:** Editor occasionally freezes on minimize/maximize/restore. Hard to reproduce, harder to recover (Task Manager only).
- **Fix approach:** Use `ManualResetEventSlim` signaled from `hkPresent` when it observes the block. Wait with a 100 ms timeout — fall through and proceed if it doesn't signal.
- **Tracking:** Assessment C-09.

### TD-10 — `clr::stop()` dereferences null pointers after failed startup

- **Issue:** `pClrRuntimeHost->Release()` etc. with no null checks (lines 95-97). If `clr::start()` failed at any of the four `SUCCEEDED(hr)` branches, the cleanup path in `start()` (lines 73-90) already nulled the pointers. Then `detatch()` calls `stop()` from `DLL_PROCESS_DETACH` and crashes inside the loader lock.
- **Files:** `UtinniCore/clr.cpp:93-102`. Called from `UtinniCore/utinni.cpp:132-136` (`detatch`).
- **Impact:** Crash on exit when CLR startup failed (silent crash, user just sees the SWG process die). Also crashes shut-down of any process where `clr::stop()` is called twice.
- **Fix approach:** Null-check each `Release` and null after. Or replace raw COM pointers with `Microsoft::WRL::ComPtr<T>` which is reset-safe.
- **Tracking:** Assessment C-10.

### TD-11 — DirectX9 hook installation has no null check on pattern scan

- **Issue:** `getVtbl()` does `memory::findPattern((swgptr)GetModuleHandle("d3d9.dll"), 0x128000, pattern, mask)` then `memcpy(&vtbl, (void*)(((swgptr)pDevice) + 2), 4)`. If `findPattern` returns 0 (pattern not found, d3d9.dll not yet loaded, wrong version) → `memcpy` from address `0x2` → access violation.
- **Files:** `UtinniCore/swg/graphics/directx9.cpp:297-303`, `getVtbl()` and `detour()`.
- **Impact:** Hard crash on startup with any d3d9.dll variant that doesn't match the pattern. Silently introduces a hard SWG-build dependency. Especially fragile because the pattern targets a vendor-specific d3d9 (the comment on line 66 references `s207_r.dll`).
- **Fix approach:** Bail with `log::critical` and `MessageBox` if `GetModuleHandle("d3d9.dll")` returns null or `findPattern` returns 0. Consider falling back to a vtable hook via standard d3d9 device creation pattern.
- **Tracking:** Assessment C-11.

### TD-12 — VSIX manifest pinned to VS 2019 only

- **Issue:** `InstallationTarget Version="[16.0,17.0)"` (lines 9-11, 17). VS 2019 only — anyone on VS 2022 cannot install the plugin templates.
- **Files:** `sdk/UtinniPluginTemplates/Vsix/source.extension.vsixmanifest:9-11,17`.
- **Impact:** Most visible new-user onboarding blocker. The README points at the SDK; the SDK doesn't install on the most common modern IDE.
- **Fix approach:** Widen to `[16.0,18.0)`. Update `Microsoft.VisualStudio.SDK` PackageReference. Smoke-test extension load in both VS 2019 and VS 2022.
- **Tracking:** Assessment C-12.

### TD-13 — `utinni.cfg` ships with `login.swgemu.com:44453` as default

- **Issue:** Default config bakes the SWGEmu login server into a sovereign-fork distribution.
- **Files:** `data/utinni.cfg:4-5` (`loginServerPort0=44453`, `loginServerAddress0=login.swgemu.com`).
- **Impact:** Some shards may not auth Utinni-launched clients. Implicitly endorses one community fork over others. Potential ToS issue.
- **Fix approach:** Blank both. Add a comment line "# Set your shard's login host and port here; see <docs>".
- **Tracking:** Assessment C-14.

### TD-14 — CppSharp `slnDir` computation is brittle

- **Issue:** `string slnDir = Directory.GetParent(workingDir.Substring(0, workingDir.LastIndexOf("\\bin\\"))).FullName + "\\";` — requires the binary's path to literally contain `\bin\`. CI runners, non-default output paths, and any developer with a customized output directory throw `ArgumentOutOfRangeException`.
- **Files:** `UtinniCoreDotNetGen/Program.cs:39-41`.
- **Impact:** Re-running CppSharp from anywhere other than the default build output silently fails with a stack trace. Blocks CI adoption.
- **Fix approach:** Accept `$(SolutionDir)` as `args[0]` from the .vcxproj post-build step; walk up looking for `Utinni.sln` if no arg; fall back to env var.
- **Tracking:** Assessment C-15.

### TD-15 — Native callback vectors are append-only (no Remove)

- **Issue:** All native callback registries are `std::vector<void(*)()>` with only an `add*Callback` function — no remove. Examples: `game.cpp:71-75`, `render_world.cpp:38` (via `static`), `post_processing.cpp:37-38`, `directx9.cpp` depth resolve callbacks at `depth_texture.cpp:37`, all the `swg::*::detour` files.
- **Files:** `UtinniCore/swg/game/game.cpp:71-104`, `UtinniCore/swg/graphics/post_processing.cpp:37-69`, `UtinniCore/swg/graphics/depth_texture.cpp:37,206-209`, and similar across `swg/*`.
- **Impact:** A plugin that registers a callback and is later disposed/unloaded leaves a dangling function pointer. Next dispatch → access violation. Plugins cannot reload cleanly.
- **Fix approach:** Standardize a handle-based API: `int Subscribe(fn)` returns an opaque id, `Unsubscribe(id)` removes it. Mechanical change across ~12 files but enables proper plugin lifecycle.
- **Tracking:** Assessment R-A.

### TD-16 — Managed callback `Add`/`Remove` is asymmetric

- **Issue:** `GameCallbacks` has both `AddInstallCallback`/`RemoveInstallCallback`, `AddSetupSceneCall`/`RemoveSetupSceneCall`, `AddCleanupSceneCall`/`RemoveCleanupSceneCall` — but `GroundSceneCallbacks` (`GroundSceneCallbacks.cs:55-73`) has only `Add*Call` / `Add*Callback`, no removes. `ObjectCallbacks` similarly mixed. `Log` has both `Add` and `Remove` for output sinks (lines 121, 126) — but the names are misspelled `AddOuputSinkCallback`.
- **Files:** `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:85-98`, `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs:55-73`, `UtinniCoreDotNet/Callbacks/ObjectCallbacks.cs`, `UtinniCoreDotNet/Utility/Log.cs:121,126`.
- **Impact:** Plugins that subscribe in OnStartup and try to unsubscribe in OnShutdown get `ObjectDisposedException` on `Control.BeginInvoke` once their forms close, because some callbacks have no way to detach.
- **Fix approach:** Add symmetric `Remove*` to every `Add*`. Fix the `Ouput` typo with an `[Obsolete]` shim for back-compat. Consider switching to standard `event` semantics.
- **Tracking:** Assessment R-A.

### TD-17 — Plugin lifecycle is incomplete

- **Issue:** Three related gaps in `PluginManager`:
  1. `UtinniPlugin::init()` is declared in `utinni_plugin.h` but **never called** by `loadPlugins()` — see `plugin_manager.cpp:129-150`.
  2. `LoadLibrary` failures are silently ignored (line 134-148: `if (hDllInstance != nullptr)` — no else branch).
  3. `HMODULE` handles are never tracked. `~PluginManager` (lines 41-49) does `delete plugin` but never `FreeLibrary` on the host DLL — and `delete plugin` is only safe if the plugin uses the same CRT as UtinniCore (it might not).
- **Files:** `UtinniCore/plugin_framework/plugin_manager.cpp:41-49,51-154`, `UtinniCore/plugin_framework/utinni_plugin.h`.
- **Impact:** Plugins that fail to load do so silently. CRT-mismatch plugins crash on shutdown. Cannot hot-reload plugins.
- **Fix approach:** Call `plugin->init()` after the load loop. Log `LoadLibrary` failure with `GetLastError`. Track HMODULEs in `pImpl->plugins`. Add a symmetric `destroyPlugin` export to the plugin ABI; call it instead of `delete`.
- **Tracking:** Assessment R-B.

### TD-18 — Hard-coded SWG RVAs duplicated across the C++/C# boundary

- **Issue:** SWG WndProc address `0x00AA0970` appears in both `UtinniCore/swg/client/client.cpp:43` and `UtinniCoreDotNet/UI/Controls/PanelGame.cs:40`. The two `isSafeToUse` flag addresses (`0x01908858`, `0x01919410`) are referenced in `game.cpp:307` and documented in `docs/ai/internals.md:230-231`. The C++ code uses logical-OR (`||`) but the docs say `&&` — discrepancy, one of them is wrong.
- **Files:** `UtinniCore/swg/client/client.cpp:43`, `UtinniCoreDotNet/UI/Controls/PanelGame.cs:40`, `UtinniCore/swg/game/game.cpp:305-308`, `docs/ai/internals.md:230-231`.
- **Impact:** Any SWG client rebuild (different RVAs) requires hunting through both C++ and C# code. C# code with a hard-coded pointer is a P/Invoke smell that will silently break with no compile error if the address moves.
- **Fix approach:** Export `Client::getSwgWndProc()` via `UTINNI_API`. Have `PanelGame.WndProc` resolve it at runtime. Same pattern for the two `isSafeToUse` addresses — expose them as named functions, not magic numbers. Decide `||` vs `&&` and update both code and docs.
- **Tracking:** Assessment R-C, Assessment Open Question #1.

### TD-19 — No CI, no tests, no analyzers, no formatter

- **Issue:** Repo has no `.github/workflows/`, no `.editorconfig`, no `.clang-format`, no test project. Every change relies entirely on the contributor's local build environment.
- **Files:** Repo root — confirmed missing.
- **Impact:** Regressions land silently (TD-04 has been broken since 2020). Indentation is inconsistent (3-space / 4-space / tab mix in C++ files — see `directx9.cpp`, `depth_texture.cpp`). Visible to anyone reading the code as "this is a hobby project."
- **Fix approach:** Single `.github/workflows/build.yml` running `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86` catches 90% of regressions immediately. Add `.editorconfig` (start with 4-space, LF, UTF-8). Add `.clang-format` and run it once over the C++ tree.
- **Tracking:** Assessment R-D.

### TD-20 — `Log` reflection on every call

- **Issue:** `Log.FormatText` walks the stack via `new StackTrace().GetFrame(2).GetMethod()` on every log call (line 54) when `writeClassName` is enabled. Stack walks are expensive — meant to be `[Conditional("DEBUG")]` or compile-time only.
- **Files:** `UtinniCoreDotNet/Utility/Log.cs:50-69`.
- **Impact:** Hot-path log calls (especially in render callbacks) measurably slow editor frame rate when class-name prefixing is enabled.
- **Fix approach:** Replace `StackTrace` with `[CallerMemberName]`, `[CallerFilePath]`, `[CallerLineNumber]` parameters. Compile-time resolution, zero runtime cost.
- **Tracking:** Assessment R-E.

### TD-21 — CppSharp header list is manually maintained

- **Issue:** `Program.cs:67-92` lists 27 headers by hand. The actual `UtinniCore/` tree has ~60 headers including subsystems like `swg/appearance/*` (extent, particle, portal, skeleton), `swg/object/*`, `swg/scene/*`, most of which are *not* projected to managed. New C++ APIs silently don't appear in C# until someone remembers to register them. The comment on line 64 (`// ToDo make this a loop to grab all the subfolders`) admits it.
- **Files:** `UtinniCoreDotNetGen/Program.cs:67-92`.
- **Impact:** Discoverability problem — plugins can't use C++ APIs that aren't projected, but it's not obvious which APIs those are. Forces a duplicate "list known managed APIs" doc.
- **Fix approach:** Glob `UtinniCore/**/*.h`, filter via a `_internal/` directory convention or explicit blocklist for headers that shouldn't be projected (e.g., `direct_input.h` if x86-only inline asm makes it fail).
- **Tracking:** Assessment R-F.

### TD-22 — `Directory.Build.props` wizard is destructive-by-omission

- **Issue:** `Props.CreateDotNetDirectoryProps(slnPath)` does `if (File.Exists(...)) return;` (line 11-14). If a `Directory.Build.props` already exists in the user's solution, the wizard silently leaves it untouched — and the user's plugin then fails to find `UtinniCoreDotNet.dll` with a cryptic MSBuild error.
- **Files:** `sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs:9-14`.
- **Impact:** Users with existing solutions (most realistic case for a plugin framework) hit this. Hard to diagnose: the wizard says it succeeded.
- **Fix approach:** Idempotent merge — parse the existing XML, inject the missing properties under the right `<PropertyGroup Condition=...>`. Or emit a separate `Utinni.props` and add `<Import Project="Utinni.props"/>` to the existing file.
- **Tracking:** Assessment R-G.

### TD-23 — Callback dispatch iterates without snapshot

- **Issue:** Both native (`std::vector<void(*)()>`) and managed (`SynchronizedCollection<Action>`) callback lists are iterated with raw `foreach` / range-for. A subscriber that adds or removes from inside its own callback during dispatch hits `InvalidOperationException` / vector reallocation UB.
  - Example managed: `GameCallbacks.cs:124,131,138` — `foreach (Action callback in installCallbacks)` over `SynchronizedCollection` without snapshot.
  - Example native: `game.cpp:117,135,161,178,191` — bare `for (const auto& func : ...)`.
- **Files:** `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:122-144`, `UtinniCore/swg/game/game.cpp:115-195`, all `swg::*` files with callback dispatch.
- **Impact:** Any plugin that does "subscribe once, run, then unsubscribe" inside a callback crashes the editor.
- **Fix approach:** Snapshot under `SyncRoot.lock` before iteration: `var snapshot = collection.ToArray();`. On the C++ side, copy the vector into a local before iterating.
- **Tracking:** Assessment R-H.

### TD-24 — `LeksysINI` is acknowledged-temporary

- **Issue:** README line 44 says: "LeksysINI -- Temporary, will most likely be replaced soon." No replacement plan documented. INI parsing is on the critical path (every config load: `utinni.cpp:111`, `plugin_manager.cpp:55`, `HotkeyManager.cs:45`).
- **Files:** `external/LeksysINI/*`, `UtINI/utini.cpp`, `UtINI/utini.h`.
- **Impact:** Long-standing technical debt with no defined exit. Replacement is risky because the API is used from both C++ and C#.
- **Fix approach:** Either commit to LeksysINI (delete the README disclaimer) or pick a replacement now (e.g., `mINI`, `inih`) and migrate. Don't leave it as "temporary."
- **Tracking:** Assessment Open Question #6.

### TD-25 — Empty / stub source files

- **Issue:** Several `.cpp` files contain only the MIT header and an `#include "<self>.h"` line — no code:
  - `UtinniCore/swg/appearance/particle.cpp` (26 lines, all license + include)
  - `UtinniCore/swg/scene/scene.cpp` (26 lines, all license + include)
- **Files:** As listed.
- **Impact:** Minor — implies headers expose API that has no implementation. Confusing for grep-based discovery.
- **Fix approach:** Either implement, delete, or comment in the header "implementation pending — see issue #N."

### TD-26 — Disabled hooks living in the detour table

- **Issue:** Detour functions exist with bodies but the `Detour::Create` line is commented out, leaving dead hook scaffolding:
  - `UtinniCore/swg/scene/render_world.cpp:46-66,70-71` — `hkRender` and `hkClearVisibleCells` have commented bodies; both `Detour::Create` calls commented in `detour()`. File only really installs `addObjectNotifications`.
  - `UtinniCore/swg/scene/client_world.cpp:45-58,63` — `hkInternalCollide` exists with commented `swg::clientWorld::internalCollideFindAllObjects` calls; detour commented.
  - `UtinniCore/swg/misc/io_win.cpp:50-57` — `hkDraw` defined; `detour()` commented out.
  - `UtinniCore/utinni.cpp:71,75` — `cuiIntro::detour()` and `cuiMediatorFactorySetup::detour()` commented in `createDetours()`.
- **Files:** As listed.
- **Impact:** Code that compiles but isn't wired in. Reads as "this is enabled" until you check `detour()`. Carries TODO debt.
- **Fix approach:** Delete dead hooks. If they represent future work, move to a tracked branch and remove from main.

### TD-27 — Hardcoded font path

- **Issue:** `ImGui::GetIO().Fonts->AddFontFromFileTTF("C:/Windows/Fonts/micross.ttf", 14);` — absolute path to a Microsoft Sans Serif file that may not exist on all Windows installs or may differ on Server SKUs.
- **Files:** `UtinniCore/swg/ui/imgui_impl.cpp:133`.
- **Impact:** ImGui falls back to its embedded font silently if the file is missing — but on a system without `micross.ttf` the UI looks different than the developer expects. Worse: passing a missing path may assert in debug ImGui builds.
- **Fix approach:** Use `GetEnvironmentVariable("WINDIR")` to build the path, or ship a font alongside the editor.

### TD-28 — `UtinniForm` icon is plugin-specific (`TJT.ico`)

- **Issue:** The framework's `UtinniForm` base class sets `this.Icon = Resources.TJT;` (Jawa Toolbox icon) as the default for every form.
- **Files:** `UtinniCoreDotNet/UI/Forms/UtinniForm.cs:84`.
- **Impact:** Plugin-specific branding leaking into the framework. Anyone forking Utinni for a non-Jawa-Toolbox use case inherits the Jawa Toolbox icon by default.
- **Fix approach:** Default to a neutral Utinni icon or `null`; expose `Icon` as a regular property.

### TD-29 — `Stack` typed inconsistently with merge bug ordering

- **Issue:** `UndoRedoManager.AddUndoCommand` (lines 60-73) does `RedoCommands.Clear()` *before* the merge check. If the new command merges into the previous one (no new stack entry), redo history was already destroyed for nothing.
- **Files:** `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:60-73`.
- **Impact:** Drag a gizmo, click Undo, click Redo — works once. Drag again (merging into the new top), Redo stack is now wrong. Caught by the audit but worth flagging separately because the fix is *not* the same as the locking fix in TD-08.
- **Fix approach:** Move the `RedoCommands.Clear()` after the merge check (only clear when actually pushing).

---

## Known Bugs

### KB-01 — `cuiIo` keyboard re-enable broken when called from .NET

- **Symptoms:** Hotkey override (suppress game input during a hotkey callback) occasionally fails to re-enable keyboard input. Game becomes unresponsive to keys until a click in the game viewport.
- **Files:** `UtinniCore/swg/ui/cui_io.cpp:68` (comment: `// ToDo this can get broken somehow when called from .NET, figure out why. Use enableKeyboard(true) for now`).
- **Trigger:** Use any hotkey with `OverrideGameInput = true` (see `FormMain.cs:126-128`). Symptom is intermittent.
- **Workaround:** `HotkeyManager.ProcessInput` (`HotkeyManager.cs:65-83`) does a triple-nested pre-main / main / pre-main callback dance to work around it. Comment line 74: `// ToDo really far from perfect, occasionally doesn't proper block a call`.

### KB-02 — Random crash with `getHwnd` on scene load

- **Symptoms:** Sporadic crash in `hkMainLoop` when loading or switching scenes.
- **Files:** `UtinniCore/swg/game/game.cpp:128` (comment: `// ToDo fix random crash with getHwnd on load Scene?`).
- **Trigger:** Load scene while editor mode is on; `GetWindowRect(Client::getHwnd(), &rect)` returns invalid data because hwnd is being re-parented at the same time.
- **Workaround:** None — just don't load scenes rapidly.

### KB-03 — `Application.OpenForms` broken under `UtinniForm`

- **Symptoms:** "Open Log Window" finds the existing log window via `typeof(FormLog)` check on `formChildren` (custom tracking list), because `Application.OpenForms` doesn't return `UtinniForm` instances.
- **Files:** `UtinniCoreDotNet/UI/Forms/FormMain.cs:362` (comment: `// ToDo fix this, it's broken since using UtinniForm`).
- **Trigger:** Calling `Application.OpenForms` to enumerate windows. UtinniForm bypasses standard form initialization in a way that prevents registration.
- **Workaround:** `FormMain.formChildren` (line 54) is the local replacement.

### KB-04 — `cursorHideCount` accounting hack

- **Symptoms:** A single `Cursor.Show()` doesn't always re-show the cursor after `Cursor.Hide()`.
- **Files:** `UtinniCoreDotNet/UI/Controls/PanelGame.cs:132-148` (comment: `// ToDo Implement proper, hacky workaround when a single Cursor.Show() doesn't show the Cursor`).
- **Trigger:** Hovering between game viewport and ImGui overlays.
- **Workaround:** `cursorHideCount` counter tracks how many times Hide was called; ShowCursor loops to balance it. Real issue is that `Cursor.Hide`/`Show` are a reference count and the panel logic over-decrements.

### KB-05 — `IsSetSafeToUse` `||` vs `&&` discrepancy

- **Symptoms:** Subtle: code uses logical-OR but docs say logical-AND. One of them is wrong.
- **Files:** `UtinniCore/swg/game/game.cpp:305-308` (uses `||`), `docs/ai/internals.md:231` (says "AND ... Both must be true").
- **Trigger:** Querying `Game::isSafeToUse()` during scene transitions when one of the two flags is set but not both.
- **Workaround:** None — need historical knowledge to resolve. Documented in assessment Open Question #1.

### KB-06 — `WorldSnapshot::createAddNode` parent-finding fragile

- **Symptoms:** Adding objects from inside POBs (player-owned buildings) requires camera to be inside the POB or the parent lookup fails silently.
- **Files:** `UtinniCore/swg/scene/world_snapshot.cpp:551-562` (comment: `// Temporary check to get parent, make this better`), `UtinniCore/swg/scene/world_snapshot.cpp:553` (comment: `// If camera is outside of the POB and the new node to be created is inside, it crashes as parentObject is nullptr`).
- **Trigger:** Add object via Jawa Toolbox while flying outside a building.
- **Workaround:** Move camera inside the building first.

### KB-07 — `reader->addNode` returns unreliable pointer

- **Symptoms:** Cannot get a usable pointer to the newly-added node from the API itself.
- **Files:** `UtinniCore/swg/scene/world_snapshot.cpp:604,642` (comment: `// Workaround to the unreliable ptr return of reader->addNode`).
- **Trigger:** Adding any new world-snapshot node.
- **Workaround:** Re-fetch the node from `reader->nodeList->back()` or `parentNode->children->back()` immediately after `addNode`. Fragile if anything else mutates the list between calls.

---

## Security Considerations

### SEC-01 — Plaintext password storage for autoLogin

- **Risk:** `autoLoginData` file stores the user's SWG password in plaintext on disk in the editor working directory.
- **Files:** `UtinniCore/swg/ui/cui_misc.cpp:99-117` (comments: `// ToDo absolutely WIP, currently stored in plaintext, store with simple encryption to not leave a plaintext password on disk`). Commit `8e651a4` is titled `UtinniCore -- WIP autoLogin -- [NOT SAFE] Needs file encryption for password` and is still in main.
- **Current mitigation:** None.
- **Recommendations:**
  1. Disable `autoLogin` config option by default until encrypted.
  2. Encrypt with `DPAPI` (`CryptProtectData` / `CryptUnprotectData`) — Windows-native, user-scoped, no key management needed.
  3. Or document loudly that this is a developer-only feature and stays plaintext.

### SEC-02 — `VirtualAllocEx` with `PAGE_EXECUTE_READWRITE` in launcher

- **Risk:** Launcher allocates remote memory with executable + writable permissions to hold the DLL path string. Not strictly needed for a data buffer.
- **Files:** `Launcher/main.cpp:182`.
- **Current mitigation:** None — buffer holds a filename, never executed.
- **Recommendations:** Use `PAGE_READWRITE` for the path buffer. Reserves W^X best-practice even if unenforced here. Reduces AV false-positive surface.

### SEC-03 — Antivirus / EDR false-positive surface

- **Risk:** The whole injection model (`CreateProcess` suspended + entry-point patch `EB FE` + `CreateRemoteThread(LoadLibraryA)` + OEP restore) is textbook DLL injection. Modern AV/EDR flags this pattern aggressively.
- **Files:** `Launcher/main.cpp:298-368` (`loadDll`), `Launcher/main.cpp:174-210` (`inject`).
- **Current mitigation:** None documented. SWG users tend to whitelist their game directory anyway.
- **Recommendations:**
  1. Document required Windows Defender / Defender for Endpoint exclusion paths in the README.
  2. Sign the launcher binary with a code-signing certificate (Authenticode).
  3. Consider a less aggressive injection model — e.g., AppInit DLLs or set `_NT_SYMBOL_PATH`-style env var — but those have their own issues.

### SEC-04 — `LoadLibrary` of arbitrary `.dll` from `Plugins/` directory

- **Risk:** `PluginManager::loadPlugins()` walks `Plugins/**/*.dll` recursively and `LoadLibrary`s each. Any DLL planted in the plugins directory by another process runs with the SWG client's privileges.
- **Files:** `UtinniCore/plugin_framework/plugin_manager.cpp:129-150`, `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:48-64`.
- **Current mitigation:** None. No signature check, no manifest validation.
- **Recommendations:** Long term: require plugins to be Authenticode-signed and verify before `LoadLibrary`. Short term: only `LoadLibrary` files explicitly listed in `ut.ini [Plugins]` (don't `recursive_directory_iterator` the whole tree).

---

## Performance Bottlenecks

### PERF-01 — Stack walk on every log call

- **Problem:** See TD-20 — `Log.FormatText` walks the stack via `new StackTrace().GetFrame(2)` on every call when class-name prefixing is enabled.
- **Files:** `UtinniCoreDotNet/Utility/Log.cs:50-69`.
- **Cause:** `StackTrace` reflection is ~microseconds per call. Multiplied by per-frame logging in any plugin = visible frame drop.
- **Improvement path:** `[CallerMemberName]` / `[CallerFilePath]` resolved at compile time. Free at runtime.

### PERF-02 — Per-frame `new` allocations in callback dispatch

- **Problem:** `GroundSceneCallbacks.DequeueUpdateLoopCalls` calls `updateLoopCallQueue.Count > 0` then `TryDequeue` in a loop. Each `Action` invocation potentially allocates closures. Hot path (every frame, every queue).
- **Files:** `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs:75-106`, `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:100-120`.
- **Cause:** Closure-capturing lambdas registered via `AddUpdateLoopCall(() => ...)` allocate a delegate per call. Frequent callers (e.g., gizmo updates) generate GC pressure.
- **Improvement path:** Document the contract: "queue is for one-shot calls; for recurring, use Add*Callback". Audit callers for accidental per-frame `AddUpdateLoopCall`.

### PERF-03 — `WorldSnapshotReaderWriter::Node` linear search by id

- **Problem:** Several functions in `world_snapshot.cpp` linear-scan `nodeList` looking for a matching id (e.g., `removeNode` lines 264-270, `removeNodeFull`). O(n) per lookup, can become O(n²) when bulk-editing.
- **Files:** `UtinniCore/swg/scene/world_snapshot.cpp:264-270,273-283`.
- **Cause:** No index by id. World snapshots can have tens of thousands of nodes.
- **Improvement path:** Maintain an `unordered_map<int, Node*>` alongside `nodeList`. Update on add/remove. Bench against real-world snapshot sizes first.

### PERF-04 — DirectX texture re-creation on every Present miss

- **Problem:** `hkPresent` allocates `new DepthTexture()` and calls `depthTexture->createTexture(...)` on the first frame where `pTextureDepth == nullptr`. Inside the present hook (per frame). The null check is the only guard.
- **Files:** `UtinniCore/swg/graphics/directx9.cpp:228-237`.
- **Cause:** Lazy initialization in a hot path. If `release()` is called (on Reset, line 247), the next frame re-allocates the textures.
- **Improvement path:** Move texture creation to a deferred `setup()` similar to `imgui_impl::setup`. Add a "needs recreate" flag set on Reset, checked once per frame, reset after creation.

---

## Fragile Areas

### FR-01 — DirectX hook is build-specific

- **Files:** `UtinniCore/swg/graphics/directx9.cpp:297-303` (`getVtbl`), `directx9.cpp:66` (`compileShader = 0x62A4F9DB; // from s207_r.dll`).
- **Why fragile:**
  - The vtable scan pattern (`xx????xx????xx`) targets the specific d3d9.dll variant SWG ships with.
  - `compileShader` at `0x62A4F9DB` is an inside-`s207_r.dll` (a SWG vendor renderer) address.
  - Both fail silently with TD-11 (no null check). Different d3d9 → memcpy from `0x2`.
- **Safe modification:** Don't refactor without a known-good test client. Add the null check from TD-11 first. Then any change to detour ordering or vtable scan logic must run on multiple SWG client builds.
- **Test coverage:** None. A smoke test would just be "load Utinni against the canonical Pre-CU client, see editor window open."

### FR-02 — SWG RVA dependency

- **Files:** Every `swg::*` namespace file. Roughly 50+ hard-coded addresses across `client.cpp`, `game.cpp`, `config.cpp`, `network.cpp`, `cui_misc.cpp`, `world_snapshot.cpp`, etc.
- **Why fragile:** Any SWG client rebuild moves these. No central RVA table — they're scattered as `(pX)0x00ABCDEF` literals. No version check at startup.
- **Safe modification:** Don't change RVAs without IDA-confirmed new values. Adding a version-check (read the SWG client's `ProductVersion` resource at startup, hard-fail on mismatch) would prevent silent corruption on a wrong-build client. Launcher already does a `ProductName` check (`Launcher/main.cpp:280-292`) but it accepts any SWG client.
- **Test coverage:** None.

### FR-03 — CLR/.NET hosting via legacy `ICLRRuntimeHost`

- **Files:** `UtinniCore/clr.cpp:42-127`.
- **Why fragile:** Uses the .NET Framework 4.x `ICLRRuntimeHost` API targeting `v4.0.30319`. Won't work on machines without .NET Framework 4.x installed (Windows 11 ships it, but Windows 10 Server SKUs may not). Plus the cleanup path crashes on partial init (TD-10).
- **Safe modification:** Replacement is a big project — `coreclr.dll` for .NET Core hosting requires reworking the entire CLR bridge. For now, just fix TD-10 (null checks) and document the .NET 4.7.2+ requirement (already partially noted in VSIX manifest `[4.7.2,)`).
- **Test coverage:** None.

### FR-04 — Loader-lock startup

- **Files:** `UtinniCore/utinni.cpp:138-151`.
- **Why fragile:** See TD-01. Works today because of timing luck; one CRT-init reorder away from a deadlock that nobody can debug.
- **Safe modification:** Don't touch the launcher injection logic or the `main()` startup sequence without a plan to defer CLR/plugin load. Mitigate by adopting TD-01's `utinni_init` deferred-init pattern.
- **Test coverage:** None.

### FR-05 — `imgui_impl::setup` runs inside `hkPresent`

- **Files:** `UtinniCore/swg/graphics/directx9.cpp:239` (`imgui_impl::setup(pDevice)`), `UtinniCore/swg/ui/imgui_impl.cpp:112-168`.
- **Why fragile:** ImGui creates the context, hooks the WndProc (via `SetWindowLongPtr`, line 131), loads a font from a hardcoded path (line 133), and configures docking — all inside the first frame's `hkPresent`. `isSetup` flag prevents re-entry, but the very first frame does *a lot* before letting Present continue. Adds visible startup hitch.
- **Safe modification:** Don't move `imgui_impl::setup` out of the Present hook — it needs the device. But the font-load and theme configuration could move to a separate post-setup callback that runs once before the first interactive frame.
- **Test coverage:** None.

### FR-06 — World snapshot mutation requires `Game::isSafeToUse`

- **Files:** `UtinniCore/swg/scene/world_snapshot.cpp:299-302` and many similar guards.
- **Why fragile:** The `isSafeToUse` check is a binary "game is in a state where the world snapshot can be touched" — but the implementation reads two flag bytes (`game.cpp:307`) and combines them with `||` (or maybe `&&`, see KB-05). The actual semantics aren't documented.
- **Safe modification:** Always check `Game::isSafeToUse` before any world-snapshot mutation. Don't refactor the check until KB-05 is resolved.
- **Test coverage:** None.

### FR-07 — `Hotkey.UpdateKeys(string)` retries the `Enum.Parse` path

- **Files:** `UtinniCoreDotNet/Hotkeys/Hotkey.cs:94-97`, `UtinniCoreDotNet/Hotkeys/HotkeyManager.cs:106-114`.
- **Why fragile:** `HotkeyManager.Load()` calls `Hotkey.UpdateKeys(string)` for every hotkey from `input.ini`. Same `Enum.Parse` throw-on-typo bug as TD-07. Triggered during settings load *after* plugin compose has succeeded — so the fallout pattern is different (a single bad row aborts the whole settings load, leaving the remaining hotkeys at their defaults).
- **Safe modification:** Fix TD-07 in `ProcessString` and the `UpdateKeys` path is fixed too.
- **Test coverage:** None.

---

## Scaling Limits

### SL-01 — x86-only

- **Current capacity:** SWG is a 32-bit client; Utinni is x86-only. All RVAs assume 4-byte pointers. `swgptr` is defined as a 32-bit type.
- **Limit:** ~3 GB user-mode virtual address space, including SWG itself which already uses a lot. Plugins that allocate large buffers can OOM the process.
- **Scaling path:** Not feasible — SWG would need to be rebuilt as x64. Not a Utinni problem to solve.

### SL-02 — Single-threaded world-snapshot edits

- **Current capacity:** Most world-snapshot mutations are gated on the game thread via `Game::isSafeToUse` and assume single-threaded access.
- **Limit:** Bulk operations (importing a 10k-node region) freeze the editor for several seconds because mutations must run on the main loop thread.
- **Scaling path:** Move heavy bulk operations to a background thread that prepares deltas, then commit on the main loop via `AddMainLoopCall`. Risk: snapshot consistency during long imports.

### SL-03 — Plugin count / load order

- **Current capacity:** Plugin load order is stored as `plugin_00`, `plugin_01`, ... in `ut.ini [Plugins]`. Two-digit naming = 100 plugins max (`stringUtility::toString(i, 2)` in `plugin_manager.cpp:61`).
- **Limit:** 100 plugins.
- **Scaling path:** Three-digit if it ever matters. Not currently an issue.

---

## Dependencies at Risk

### DEP-01 — DXSDK June 2010 (out of support since 2012)

- **Risk:** Utinni depends on the DirectX SDK June 2010 release for `d3dx9.h` and the `d3dx9` math helpers. Microsoft has deprecated the standalone DXSDK in favor of the Windows SDK, but Windows SDK only ships `d3d9.h` — not the `d3dx9` extensions.
- **Impact:** New contributors must hunt down DXSDK Jun 2010 from archived Microsoft pages. `DXSDK_DIR` env var must be set. Configurations that don't set it fail silently (see `UtinniCore.vcxproj` — DXSDK include/lib paths are configured per build mode and not always present).
- **Migration plan:** Audit `d3dx9` usage. If Utinni only uses math helpers (`D3DXVECTOR3`, `D3DXMATRIX`), replace with `DirectXMath.h` from the Windows 10 SDK. If it uses texture loaders or effect compilation, those would need replacing too. Tracked in assessment Open Question #8.

### DEP-02 — `CppSharp` Mono fork pinned

- **Risk:** `UtinniCoreDotNetGen` depends on CppSharp via committed `external/CppSharp/`. No version pinning, no NuGet, no upgrade path. CppSharp upstream has moved significantly since the vendored version.
- **Impact:** Cannot easily upgrade to fix CppSharp bugs. Cannot use newer C++ features in projected headers without testing whether CppSharp handles them.
- **Migration plan:** Switch to a NuGet PackageReference for `CppSharp` (latest stable). Run the existing header set through it to verify the output matches. If it doesn't, decide: re-vendor or migrate.

### DEP-03 — ImGui / ImGuizmo / spdlog vendored at old versions

- **Risk:** All three under `external/` are committed copies, no version pinning. ImGui especially has had significant API and feature changes (docking branch is now main).
- **Impact:** Can't easily pick up ImGui bug fixes or new widgets without redoing the vendoring. Plugins that want newer ImGui APIs (e.g., `ImGuiWindowFlags_NoDocking` semantics) are stuck.
- **Migration plan:** Subtree-merge or replace vendored copies with `git submodule`s pinned to specific tags. Bump to ImGui 1.91+ / spdlog 1.14+ / latest ImGuizmo as a single coordinated change.

### DEP-04 — `nvapi` x86-only

- **Risk:** Depth-buffer resolve uses NVAPI on NVIDIA hardware (`UtinniCore/swg/graphics/depth_texture.cpp:51-58`). NVIDIA-specific path. AMD/Intel fall back to RESZ trick (`resolveDepthWithResz`).
- **Impact:** Vendor-specific code path with separate compile flags. AMD's equivalent (driver-level depth resolve) is well-supported via RESZ but the fallback is older / less tested.
- **Migration plan:** Add a "use RESZ even on NVIDIA" config flag for testing. Long term, evaluate whether `IDirect3DDevice9Ex::StretchRect` with a properly-cooperated depth format covers both vendors uniformly.

### DEP-05 — `DetourXS` and `nvapi` missing from `licenses.txt`

- **Risk:** Third-party license file omits two used dependencies. README lists `DetourXS` (line 42); `external/DetourXS/` and `external/nvapi/` are in-tree. `licenses.txt` has CppSharp, dearImgui, ImGuizmo, LeksysINI, spdlog only — no DetourXS, no NVAPI.
- **Impact:** Distribution compliance issue. NVAPI in particular has redistribution requirements.
- **Migration plan:** Append DetourXS license (MIT) and NVAPI SDK license terms to `licenses.txt`. Also fix the mojibake (`Jo�o Matos` should be `João Matos`, line 7 — UTF-8 file currently mis-encoded).

---

## Missing Critical Features

### MCF-01 — No CI

- **Problem:** No build verification, no test runs, no analyzer gates on PRs. See TD-19.
- **Blocks:** Confident upstreaming of changes. Catching regressions like TD-04 before they ship.

### MCF-02 — No tests of any kind

- **Problem:** No unit tests, no smoke tests, no integration tests. Code that doesn't compile is the only failure surface caught today.
- **Blocks:** Refactoring with confidence. Verifying behaviour stability when bumping a dependency.

### MCF-03 — No SWG-client version validation

- **Problem:** Launcher checks only `ProductName == "Star Wars Galaxies"` (line 281). Any SWG client passes. The hard-coded RVAs assume a specific build.
- **Blocks:** Failing fast on wrong-build clients. Today, mismatched-build clients fail at runtime with random crashes in the detour-installation phase.
- **Fix approach:** Add a `ProductVersion` or file hash check. Fail with a clear message: "Utinni requires SWG client build <X>; detected <Y>."

### MCF-04 — No plugin signing / sandboxing

- **Problem:** See SEC-04. Any DLL planted in `Plugins/` is loaded with full process privilege.
- **Blocks:** Distributing Utinni to less-technical users where AV/malicious-plugin risk is real.

### MCF-05 — No remote-debugging story

- **Problem:** The Launcher tried to implement `attachToVisualStudio` (commented out, lines 33-172) — admits it never worked reliably. Currently there's no documented "how do I debug Utinni" workflow.
- **Blocks:** Contributor velocity. Anyone investigating C-01 (loader lock) or TD-10 (CLR cleanup) needs a debugger attached at the right moment.
- **Fix approach:** Drop the broken DTE attach code (mentioned in assessment cleanup section). Document the manual `Debug → Attach to Process` flow with a screenshot. Add an opt-in `Launcher` sleep before injection so the user can attach.

### MCF-06 — `init()` callback never invoked

- **Problem:** `UtinniPlugin::init()` declared but `PluginManager::loadPlugins()` never calls it. See TD-17.
- **Blocks:** Plugins that need a second-stage init after all peers are loaded (e.g., subscribing to another plugin's callbacks) have no hook.

### MCF-07 — Mouse + keyboard combo hotkeys

- **Problem:** README "Planned key features" line 28: "Combined mouse and keyboard hotkeys (Currently keyboard only)."
- **Blocks:** Standard editor workflows like Shift+RightClick are not bindable.

---

## Test Coverage Gaps

### TC-01 — Everything

- **What's not tested:** Literally everything. No `*.test.*`, no test project, no xUnit/NUnit/MSTest reference in any `.csproj`. No `gtest`/`catch2`/anything in any `.vcxproj`.
- **Files:** Entire repo.
- **Risk:** Every change is a potential regression. The "in-codebase comments" effectively serve as the only documentation of which corner cases the author thought about.
- **Priority:** **High** — but starts with TD-19 (CI). Once a build job exists, a smoke-level xUnit project that just verifies `UtinniCoreDotNet` types load is a 30-line addition that catches BadImageFormatException-class breakage.

### TC-02 — Plugin lifecycle paths

- **What's not tested:** Plugin DLL load failure, x86/x64 mismatch, missing dependency, ctor throw, hotkey parse failure (TD-07).
- **Files:** `UtinniCoreDotNet/PluginFramework/PluginLoader.cs`, `UtinniCore/plugin_framework/plugin_manager.cpp`.
- **Risk:** TD-06 + TD-07 chain crash silently. Test would be a tiny "intentionally broken plugin" DLL + assertion that the surviving plugins still load.
- **Priority:** **High** (after TD-06 fix).

### TC-03 — Native ↔ managed marshalling

- **What's not tested:** Any of the `Action_` / `Action_IntPtr_C` etc. delegates from `UtinniCore.Delegates`. The "store this in a variable somehow needed to prevent corruption" comment (`GameCallbacks.cs:46`) is a strong hint that GC-of-delegate-passed-to-unmanaged is happening — `GCHandle.Alloc(handler, GCHandleType.Normal)` is probably the right fix, but nobody's tested it.
- **Files:** `UtinniCoreDotNet/Callbacks/*.cs`, `UtinniCoreDotNet/Generated/UtinniCore.cs`.
- **Risk:** Sporadic AVs on resize / scene load. Hard to attribute without tests.
- **Priority:** **High** — known smell, see assessment Open Question #3.

### TC-04 — DirectX hook reentrancy

- **What's not tested:** Behavior under `IDirect3DDevice9::Reset` (windowed/fullscreen switch), device-loss, multi-monitor present.
- **Files:** `UtinniCore/swg/graphics/directx9.cpp:243-256` (`hkReset`).
- **Risk:** Editor crash on alt-tab, monitor change, resolution change.
- **Priority:** Medium.

### TC-05 — UndoRedoManager under concurrent push

- **What's not tested:** Thread-safety of the undo/redo stacks (TD-08).
- **Files:** `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs`.
- **Risk:** Data-loss race.
- **Priority:** **High**.

---

## Cross-References

- **Source assessment:** `docs/ai/assessment.md` (status table at bottom).
- **Vision document:** `docs/ai/vision.md` — the work in this document is the prerequisite for the Wave-1 plugin work named there.
- **Architecture overview:** `docs/ai/architecture.md`, `docs/ai/internals.md` (RVA reference).
- **Fork strategy:** `kennethlong/{Utinni,UtinniPlugins}` on GitHub; upstream `ptklatt/*` is dormant. All concerns above can be addressed unilaterally.

---

*Concerns audit: 2026-05-16*
