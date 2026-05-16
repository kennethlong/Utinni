# External Integrations

**Analysis Date:** 2026-05-16

Utinni is a desktop / in-process modding framework with **no network APIs, no cloud services, no remote databases, and no webhooks**. Its "integrations" are almost entirely **process-level**: it injects into the SWG client and integrates with Windows OS facilities, DirectX 9, and the Microsoft CLR. The truly load-bearing integrations are with the **target game binary** itself (via hard-coded function pointers and memory addresses) and across the **C++ ↔ .NET native interop boundary**.

## APIs & External Services

**Web APIs / REST / GraphQL:** None.
**Cloud SDKs:** None.
**Outbound HTTP / WebSocket:** Not detected.

`System.Net.Http` is listed as a reference in `UtinniCoreDotNet/UtinniCoreDotNet.csproj` line 59 but no `HttpClient`, `WebRequest`, `WebSocket`, or socket usage exists anywhere in the codebase. The reference appears to be default WinForms project scaffolding rather than active use.

## Native Process Integration — SWG Client (PRIMARY)

This is the **most important integration in the entire project**. UtinniCore is not a library the SWG client links against — it is a payload DLL that is **injected into the running `SwgClient_r.exe` process** and **patches the client's functions in-memory at known addresses**.

### Target binary identity

- **Process name:** `SwgClient_r.exe` (Star Wars Galaxies retail/SWGEmu).
- **Validation:** `Launcher/main.cpp::getSwgClientFilename()` lines 270-293 reads the EXE's Win32 `VERSIONINFO` resource and refuses to inject unless `\StringFileInfo\040904B0\ProductName == "Star Wars Galaxies"`.
- **Architecture pinning:** x86 only. Forced by every project file's `Win32` / `x86` platform.

### Injection mechanism (out-of-process → in-process bridge)

`Launcher/main.cpp::loadDll()` lines 298-368 — classical Windows DLL injection:

1. `CreateProcess(swgClientFilename, cmdLine, ..., CREATE_SUSPENDED, ..., &procInfo)` — spawn SWG suspended (line 305).
2. Map the EXE off disk and read `OptionalHeader.AddressOfEntryPoint` + `ImageBase` to compute the OEP in the remote process (lines 312-316).
3. Save the 2 bytes at OEP, then `WriteProcessMemory` patch them with `0xEB 0xFE` (`jmp $` — infinite loop) so the resumed main thread parks there (lines 319-324).
4. `ResumeThread`, then poll `GetThreadContext(... CONTEXT_CONTROL).Eip == entry` until the thread is verifiably parked (lines 329-343).
5. `VirtualAllocEx` + `WriteProcessMemory` to copy `"UtinniCore.dll"` into the target (lines 181-183 in `inject()`).
6. `CreateRemoteThread(..., LoadLibraryA, ...)` to make the SWG process load `UtinniCore.dll` (lines 185-186 in `inject()`).
7. Wait, restore OEP, resume — SWG enters its real `WinMain` with `UtinniCore.dll` already mapped and all detours installed (lines 354-356).

This is the **only** way the framework attaches to the game. There is no in-game loader, no plugin file SWG natively discovers — every entrypoint into the game runs through the injector + the in-process `DllMain` → `main()` boot in `UtinniCore/utinni.cpp` lines 99-130.

### Function-pointer "API" with SWG (hard-coded RVAs)

UtinniCore treats SWG functions like an external API whose endpoints are *memory addresses*. Every `swg::<subsystem>::` namespace in `UtinniCore/swg/` declares typedefs that match SWG's function signatures and **hard-codes the address** as a constant initializer. Examples:

| SWG subsystem | File | Hard-coded addresses |
|---------------|------|----------------------|
| Audio | `UtinniCore/swg/misc/audio.cpp` lines 30-33 | `setMasterVolume = 0x00412C20`, `getMasterVolume = 0x00412C70` |
| Network (id manager / cached id) | `UtinniCore/swg/misc/network.cpp` lines 38-44 | `idManagerGetObjectById = 0x00B380E0`, `idManagerGetInstance = 0x00B37F30`, `cachedNetworkIdGetObject = 0x00B30160`, `cast = 0xAA4900` |
| Tree-file search | `UtinniCore/swg/misc/tree_file.cpp` lines 30-33 | `searchTree = 0xA992E0` |
| World snapshot RW | `UtinniCore/swg/scene/world_snapshot.cpp` lines 52-78 | `openFile = 0x00B97D90`, `saveFile = 0x00B98120`, `addNode = 0x00B98410`, etc. |
| World snapshot manager | `UtinniCore/swg/scene/world_snapshot.cpp` lines 95-100 | `load = 0x0059C380`, `unload = 0x0059C1D0`, `createObject = 0x0059BBA0` |
| IoWin (input window) | `UtinniCore/swg/misc/io_win.cpp` lines 29-44 | `draw = 0x00AB58E0`, `MessageQueue::getCount = 0x00AA6660`, etc. |
| DirectInput | `UtinniCore/swg/misc/direct_input.cpp` lines 30-38 | `suspend = 0x00420880`, `resume = 0x00420890`, `setupInstall = 0x00421490` |
| Shader compiler | `UtinniCore/swg/graphics/directx9.cpp` line 66 | `compileShader = 0x62A4F9DB` (from `s207_r.dll`) |
| SWG WndProc | Referenced from managed: `UtinniCoreDotNet/UI/Controls/PanelGame.cs` | `0x00AA0970` (per `docs/ai/bridge.md` line 207) |
| Shader primitive sort hook | `UtinniCore/swg/graphics/shader.cpp` lines 43-44 | `midPopCell_Call = 0x772D60`, `start_midPopCell = 0x00773E39`, `return_midPopCell = 0x00773E41` |

These addresses are pinned to **one specific build of `SwgClient_r.exe`**. Any client patch invalidates them. Migrating to a different SWG client build is therefore a "find every hard-coded RVA and re-discover it" exercise (see CONCERNS.md when that document exists for the full impact).

### Function hooking (detour pattern)

The hooking primitive is **DetourXS** (`external/DetourXS/`):

```cpp
swg::treefile::searchTree = (swg::treefile::pSearchTree)
    Detour::Create(swg::treefile::searchTree, hkSearchTree, DETOUR_TYPE_PUSH_RET);
```
(`UtinniCore/swg/misc/tree_file.cpp` line 71)

`DETOUR_TYPE_*` covers `JMP`, `CALL`, and `PUSH_RET` variants. The whole list of detours installed at boot is enumerated by `createDetours()` in `UtinniCore/utinni.cpp` lines 58-89:

- `swg::config::detour()`, `utinni::Client::detour()`, `utinni::clientWorld::detour()`, `utinni::creatureObject::detour()`, `utinni::CuiChatWindow::detour()`, `utinni::CuiManager::detour()`, `utinni::cuiHud::detour()`, `utinni::cuiIo::detour()`, `utinni::cuiMenu::detour()`, `utinni::cuiRadialMenuManager::detour()`, `utinni::cuiLoginScreen::detour()`, `utinni::debugCamera::detour()`, `utinni::Game::detour()`, `utinni::GroundScene::detour()`, `utinni::Graphics::detour()`, `utinni::ParticleEffectAppearance::detour()`, `utinni::report::detour()`, `utinni::skeletalAppearance::detour()`, `utinni::SystemMessageManager::detour()`, `utinni::treefile::detour()`, `utinni::renderWorld::detour()`, `utinni::shaderPrimitiveSorter::detour()`, `utinni::IoWin::detour()`, `utinni::postProcessing::detour()`.

Plus `createPatches()` at `utinni.cpp` lines 91-97: `utinni::cuiMisc::patch()`, `utinni::debugCamera::patch()` — these *write bytes* rather than redirecting calls.

### Inline assembly trampolines

For mid-function hooks (where DetourXS would clobber instructions), UtinniCore uses `__declspec(naked)` x86 assembly thunks. See `UtinniCore/swg/graphics/shader.cpp` lines 45-90 — `midPopCell()` saves registers via `pushad/pushfd`, calls into SWG mid-function, then jumps back to a specific resume address (`return_midPopCell = 0x00773E41`).

## C++ ↔ .NET Interop (SECONDARY — the bridge)

This is the second-most-important integration. The framework hosts a managed runtime *inside* the native injected DLL.

### CLR hosting

`UtinniCore/clr.cpp` loads the CLR explicitly using the Windows CLR Hosting API:

- `#include <mscoree.h>`, `#include <metahost.h>` (lines 26-27).
- `#pragma comment(lib, "mscoree.lib")` (line 32).
- Sequence (`clr::start()` lines 42-91):
  1. `CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, ...)` → `pClrMetaHost`.
  2. `pClrMetaHost->GetRuntime(L"v4.0.30319", IID_PPV_ARGS(&pClrRuntimeInfo))` — pin .NET 4.x.
  3. `pClrRuntimeInfo->IsLoadable(&isLoadable)` — sanity check.
  4. `pClrRuntimeInfo->GetInterface(CLSID_CLRRuntimeHost, IID_PPV_ARGS(&pClrRuntimeHost))`.
  5. `pClrRuntimeHost->Start()`.
- `clr::load()` lines 104-127 calls `pClrRuntimeHost->ExecuteInDefaultAppDomain(<path>\UtinniCoreDotNet.dll, "UtinniCoreDotNet.Startup", "EntryPoint", "", &result)` — this is the native→managed jump into `UtinniCoreDotNet/main.cs::Startup.EntryPoint`.

### COM apartment

`CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED)` is called *before* loading the CLR in `UtinniCore/utinni.cpp` line 125. This sets up the COM single-threaded apartment that WinForms requires.

### Direction 1: Managed → Native (P/Invoke)

`UtinniCoreDotNet/Generated/UtinniCore.cs` (~16,658 lines) is the CppSharp-generated P/Invoke surface. Every public C++ function in the headers listed in `UtinniCoreDotNetGen/Program.cs` lines 67-92 becomes a `[DllImport("UtinniCore", ...)]` entry like:

```csharp
[DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
    EntryPoint = "?getPath@utinni@@YAABV?$basic_string@DU...")]
internal static extern IntPtr GetPath();
```
(pattern from `UtinniCoreDotNet/Generated/UtinniCore.cs` lines 19-22)

Two DLL endpoints carry the entire managed→native traffic:

- **`UtinniCore.dll`** — production code. Cdecl + ThisCall, mangled MSVC symbol names. Exported via `__declspec(dllexport)` gated by the `EXPORT_UTINNI` define (`UtinniCore/utinni.h` lines 42-46).
- **`UtinniCore-Symbols.dll`** — a one-file template-instantiation shim (`UtinniCore-Symbols/Std-symbols.cpp` lines 7-11) that re-exports MSVC's mangled `std::basic_string<char, ...>::basic_string()`, `~basic_string()`, `assign`, `data`, and `std::allocator<char>::allocator`. The hand-edited `UtinniCoreDotNet/Generated/StdEdited.cs` (~535 lines) P/Invokes into this DLL so that managed code can construct/destroy/round-trip `std::string` instances by exact mangled name.

### Direction 2: Native → Managed (callbacks via function pointers)

Native code never holds CLR delegates directly. Instead, the managed side passes a function pointer down through P/Invoke and the native side stores it as a plain `void (*)()`:

- Managed callback registration: `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` lines 44-58 — store a `UtinniCore.Delegates.Action_` instance in a static field to prevent GC, then pass it to `UtinniCore.Utinni.Game.AddInstallCallback(...)`. The static-field anchor is essential — comment at line 46 notes "Storing this in a variable is somehow needed to prevent corruption on WinForms resize."
- Callback files: `Callbacks/GameCallbacks.cs`, `Callbacks/GroundSceneCallbacks.cs`, `Callbacks/ObjectCallbacks.cs`, `Callbacks/CuiCallbacks.cs`, `Callbacks/ImGuiCallbacks.cs`.

### Threading model across the boundary

- Native callbacks fire on the **SWG game thread**, not on the WinForms UI thread.
- Managed callback handlers must marshal to UI via `Control.Invoke` to touch WinForms.
- UI handlers wanting to call game APIs enqueue via `GameCallbacks.AddMainLoopCall(...)` or `GroundSceneCallbacks.AddUpdateLoopCall(...)` (which are `ConcurrentQueue<Action>` drained by native-thread dequeue actions — see `Callbacks/GameCallbacks.cs` lines 35-43).

### CppSharp binding generation (build-time integration)

`UtinniCoreDotNetGen/Program.cs` orchestrates CppSharp:

- **Target triple:** `i686-pc-win32-msvc` (line 43) — must match the x86 build.
- **Build options:** `UnityBuild = true`, `EnableRTTI = true`, defines `SPDLOG_NO_EXCEPTIONS` + `FMT_EXCEPTIONS=0` (lines 46-49).
- **Output:** `UtinniCoreDotNet/Generated/` (line 53).
- **Symbols library:** `UtinniCore-Symbols` (line 56) — see "DLL endpoints" above.
- **Ignored headers (cause Clang failures):** `spdlog`, `detourxs`, `ADE32` (lines 105-107).
- **Driver:** `ConsoleDriver.Run(new Gen())` (line 118).
- **Triggered by build:** `UtinniCore.vcxproj` post-build step (lines 92, 123, 155) runs `$(SolutionDir)UtinniCoreDotNetGen\bin\$(Configuration)\UtinniCoreDotNetGen.exe`. Per `docs/ai/build.md` line 53, this re-runs after every Core build.

## Graphics — Direct3D 9 (PRIMARY)

The entire rendering integration with SWG goes through D3D9.

**SDK / Headers:**
- Windows SDK supplies `<d3d9.h>`, `<d3d9types.h>` (used in `UtinniCore/swg/graphics/directx9.cpp` lines 26-27).
- DirectX SDK June 2010 supplies `<d3dx9.h>` (`UtinniCore/swg/graphics/depth_texture.cpp` line 28). The DXSDK path is hard-coded into the `RelWithDbgInfo` configuration only (`UtinniCore.vcxproj` lines 72-73).
- Dear ImGui's D3D9 backend at `external/imgui/imgui_impl_dx9.{h,cpp}`.

**Hooked device methods** (`UtinniCore/swg/graphics/directx9.cpp` lines 46-65):
- `BeginScene`, `EndScene`, `Present`, `Reset`, `DrawIndexedPrimitive`, `SetRenderTarget`, `SetDepthStencil`, `SetRenderState`.
- The hook discovery technique is vtable index walking — see the `D3DInformation` enum at `directx9.cpp` lines 68+ listing every `IDirect3DDevice9` method slot (`d3di_Present_Index = 17`, etc.). Once UtinniCore has the device pointer, it indexes the vtable and DetourXS-patches the slot.

**ImGui pass:**
- `imgui_impl::setup(IDirect3DDevice9* pDevice)` is called from inside the hooked `BeginScene`/`Present` cycle (`UtinniCore/swg/graphics/directx9.cpp` references `swg/ui/imgui_impl.h`).
- The ImGui Win32 backend (`external/imgui/imgui_impl_win32.cpp`) handles keyboard/mouse via the same `WndProc` chain.

**ImGuizmo overlay:**
- `external/ImGuizmo/ImGuizmo.cpp` compiled into UtinniCore (`UtinniCore.vcxproj` line 162). Wrapped by `imgui_gizmo::` namespace in `UtinniCore/swg/ui/imgui_impl.h` lines 46-72.

**Depth buffer reading (Nvidia NVAPI path):**
- `UtinniCore/swg/graphics/depth_texture.{h,cpp}` reads SWG's depth buffer for post-processing effects.
- INTZ format check + fallback in `depth_texture.cpp` line 66 (`D3DFORMAT format = FOURCC_INTZ`).
- NVAPI calls: `NvAPI_D3D9_RegisterResource`, `NvAPI_D3D9_UnregisterResource`, `NvAPI_D3D9_StretchRectEx` (`depth_texture.cpp` lines 74-229).
- `#pragma comment(lib, "nvapi/x86/nvapi.lib")` (line 32). Static lib lives at `external/nvapi/x86/nvapi.lib`.

**Post-processing:**
- Shader compilation goes through SWG's own compile function at hard-coded address `0x62A4F9DB` (`s207_r.dll`) — see `UtinniCore/swg/graphics/directx9.cpp` line 66.
- Custom post-fx pipeline at `UtinniCore/swg/graphics/post_processing.{h,cpp}`.

## Input — DirectInput / Win32

**DirectInput (game side):**
- Suspend/resume of SWG's DirectInput pump via SWG's own `setupInstall` / `suspend` / `resume` functions at hard-coded addresses (`UtinniCore/swg/misc/direct_input.cpp` lines 35-38).
- `setupInstall` is detoured (`UtinniCore/swg/misc/direct_input.cpp` lines 53+) so that when SWG is running in editor-embedded mode the HWND is rewritten to a parent window before DirectInput grabs the cursor.

**Win32 keyboard / hotkeys:**
- Managed `HotkeyManager` reads keyboard state via `Native.GetAsyncKeyState` (`UtinniCoreDotNet/Utility/Native.cs` line 73).
- The custom title bar uses `WM_NCHITTEST` + `Native.SendMessage(SC_DRAGMOVE, ...)` (`Native.cs` lines 36-46, 67-77).

**WndProc forwarding:**
- `PanelGame` (managed WinForms panel hosting the SWG HWND) forwards every message to SWG's `WndProc` at the hard-coded address `0x00AA0970` via `Native.CallWindowProc` (`UtinniCoreDotNet/Utility/Native.cs` line 76 P/Invoke; usage documented in `docs/ai/bridge.md` lines 198-208).

## Audio — SWG's mixer (no third-party audio SDK)

UtinniCore does **not** integrate any audio SDK directly. There is no XAudio2, DirectSound, FMOD, OpenAL, or Wwise dependency. Instead, audio access goes through SWG's own master-volume functions at hard-coded addresses:

- `swg::audio::setMasterVolume = 0x00412C20`, `getMasterVolume = 0x00412C70` (`UtinniCore/swg/misc/audio.cpp` lines 30-33).
- Wrapped by `utinni::audio::setMasterVolume(float)` / `getMasterVolume()` (`UtinniCore/swg/misc/audio.cpp` lines 38-45) and surfaced to managed code via CppSharp.

## Data Storage

**Databases:** None. There is no SQL, NoSQL, ORM, document store, or embedded DB. No SQLite, no LiteDB, no Realm, no ESENT.

**File Storage:** Local filesystem only. Three patterns:

1. **SWG `.tre` archives — read indirectly through SWG.** Utinni does not parse `.tre` files itself. It detours SWG's `treefile::searchTree` function at `0xA992E0` (`UtinniCore/swg/misc/tree_file.cpp` line 32) and harvests the filenames SWG loads into a global `std::set` so plugin code can query "what's in the game data archive?" The `Repository` class (`UtinniCore/swg/misc/repository.{h,cpp}`) is built on top of this harvested list.
2. **World snapshot `.ws` files (Pre-CU `.ws` format).** Read via SWG's `WorldSnapshotReaderWriter::openFile` at `0x00B97D90`, written via `saveFile` at `0x00B98120` (`UtinniCore/swg/scene/world_snapshot.cpp` lines 52-54). The format itself is parsed by SWG; Utinni only manipulates the resulting node graph in memory.
3. **`.ini` / `.cfg` text files** — config persistence via LeksysINI (see "Configuration" below).

**Caching:** None at the framework level. Plugin-level caching is on the plugin.

## Authentication & Identity

**External auth provider:** None — there is nothing for the user to log in to. The framework runs entirely client-side.

**SWGEmu login:** The framework can pre-fill the login form via `[UtinniCore] autoLogin=true` / `autoLoginUsername=...` settings (defined in `UtINI/utini.cpp` lines 41-42), and offline scenes can be entered without connecting to a server at all via `[UtinniCore] enableOfflineScenes` (line 38). No credentials leave the local machine through Utinni's code.

## Monitoring & Observability

**Error Tracking:** None. No Sentry, Bugsnag, Rollbar, AppCenter, or telemetry of any kind. Errors surface as `MessageBox` (`Launcher/main.cpp::throwError` lines 48-52) or as `spdlog` log entries.

**Logs:**
- **spdlog 1.6.0** (`UtinniCore/utility/log.cpp`).
- File sink: `utinni.log` next to the `Launcher.exe` install dir; previous run rotated to `utinni_previous.log` (`log.cpp` lines 67-78).
- Custom in-process sink (`OutputSink`, lines 34-58) buffers messages into a `std::vector<std::string>` and fan-outs to registered callbacks — this is how the editor's bottom log rail and the in-game ImGui log panel receive live log lines.
- Managed side wraps native logging in `UtinniCoreDotNet/Utility/Log.cs`, optionally prefixing each line with caller `[ClassName][MethodName]` via `StackTrace` introspection (gated by `[Log] writeClassName` / `writeFunctionName` in `ut.ini`).

**Metrics / APM:** None.

## CI/CD & Deployment

**Hosting:** Not applicable — Utinni is a downloadable Windows desktop app, not a hosted service.

**CI Pipeline:**
- **GitHub Actions / Azure Pipelines / Jenkins:** Not detected. No `.github/workflows/`, no `azure-pipelines.yml`, no `Jenkinsfile`, no `appveyor.yml`.
- Builds run locally in Visual Studio 2019 per `docs/ai/build.md`.

**Release artifacts** (per `docs/ai/build.md` lines 186-200):
- `Launcher.exe`, `UtinniCore.dll`, `UtinniCoreDotNet.dll`, `UtinniCore-Symbols.dll` (implied; needed at runtime for `StdEdited.cs` P/Invokes), `ut.ini`, `utinni.cfg`, `Icons/`, plus the `Plugins/` tree.

## Environment Configuration

**Required env vars at runtime:** **None.** All configuration is in local `.ini`/`.cfg` files.

**Required env vars at build time:**
- `DXSDK_DIR` — only for `RelWithDbgInfo`, points at DirectX 9 SDK (June 2010). Hard-coded fallback to `C:\Program Files (x86)\Microsoft DirectX SDK (June 2010)\` in `UtinniCore/UtinniCore.vcxproj` lines 72-73.

**Required config files** (see STACK.md "Configuration" for the full schema):
- **`ut.ini`** — primary, in the launcher install dir. Read by `Launcher/main.cpp::getSwgClientFilename()` lines 220-261 and by `UtinniCore/utinni.cpp::main()` line 111. Sections: `[Launcher]`, `[UtinniCore]`, `[Editor]`, `[Log]`, `[Plugins]`. Schema seeded in `UtINI/utini.cpp` lines 30-55.
- **`utinni.cfg`** — SWG client override config used when `[UtinniCore] useSwgOverrideCfg=true`. Shipped template at `data/utinni.cfg`.
- **`<install>/Plugins/<PluginDir>/input.ini`** — per-plugin hotkey rebindings (managed by `UtinniCoreDotNet/Hotkeys/HotkeyManager.cs`).

**Secrets location:** No secrets exist or are required. `ut.ini` only stores file paths, booleans, and a username alias.

## Webhooks & Callbacks

**Incoming HTTP / webhooks:** None — no HTTP server is exposed.

**Outgoing HTTP:** None.

**In-process callback APIs (this is the framework's "event system"):**

Native registers and managed registers from opposite sides. See `UtinniCore/utinni.cpp` lines 99-129 (boot) and `UtinniCoreDotNet/main.cs` lines 39-65 (startup) for wiring. Callback categories:

| Category | Native owner | Managed wrapper |
|----------|--------------|-----------------|
| Game install / scene setup / scene cleanup / pre-main-loop / main-loop | `utinni::Game::add*Callback` | `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` |
| Ground scene update / pre-draw / post-draw / camera-change | `utinni::GroundScene::add*Callback` | `Callbacks/GroundSceneCallbacks.cs` |
| Object target-change / on-target queued | (creature_object.cpp) | `Callbacks/ObjectCallbacks.cs` |
| CUI system messages | (cui_*.cpp) | `Callbacks/CuiCallbacks.cs` |
| ImGui gizmo enabled / disabled / position-changed / rotation-changed | `imgui_gizmo::add*Callback` | `Callbacks/ImGuiCallbacks.cs` |
| Log output sink | `utinni::log::addOutputSinkCallback` | `UtinniCoreDotNet/Utility/Log.cs::AddOuputSinkCallback` |

All native callbacks fire on the SWG game thread (see `docs/ai/bridge.md` lines 219-229).

## File Formats Handled

**Read/written by Utinni directly:**
- **`.ini`** — via LeksysINI (`external/LeksysINI/iniparser.hpp` → `UtINI/utini.cpp`).
- **`.cfg`** — same as `.ini`; SWG's own client config format is also key/value INI-style with `[Section]` headers.
- **`.log`** — written by spdlog.

**Read/written via SWG, surfaced to plugins:**
- **`.ws` (World Snapshot)** — via `WorldSnapshotReaderWriter` detours (`UtinniCore/swg/scene/world_snapshot.cpp` lines 36-78). Plugin authors get a managed surface for add/remove/query of node graphs.
- **`.iff`** — referenced as the format SWG uses for object templates (e.g. `avatarSelection=object/creature/player/shared_human_male.iff` in `data/utinni.cfg` line 12). Utinni does not parse `.iff` itself.
- **`.tre`** — SWG's data archive. Utinni harvests the filename list via `treefile::searchTree` detour but does not extract archives itself.
- **`.trn`** — SWG terrain files (e.g. `groundScene=terrain/naboo.trn` in `data/utinni.cfg` line 9). Loaded by SWG, not by Utinni.

**No image / audio / video / archive / serialization libraries are linked.** The framework does not include `stb_image`, `libpng`, `zlib`, `lz4`, `protobuf`, `flatbuffers`, JSON / YAML / TOML parsers, etc. — the only structured-data dependency is INI.

## Visual Studio Extensibility (Optional)

Build-time integration only — for the plugin scaffolding wizard:

- **`Microsoft.VisualStudio.SDK 16.0.206`** + **`Microsoft.VSSDK.BuildTools 16.8.3038`** consumed via NuGet in `sdk/UtinniPluginTemplates/Vsix/Vsix.csproj` lines 74-75.
- **`Microsoft.VisualStudio.TemplateWizardInterface 16.0.0.0`** (`Vsix.csproj` line 66) for the IWizard implementation in `sdk/UtinniPluginTemplates/Vsix/Wizards/DotNetSolutionWizard.cs`.
- Output VSIX provides "DotNetPluginTemplate" and "DotNetEditorPluginTemplate" project templates that scaffold new Utinni plugin projects.
- The VSIX is **not required at runtime** — it only exists to make starting a new plugin faster.

## Sibling Repository — UtinniPlugins

`D:/Code/UtinniPlugins` (mentioned at `README.md` line 4 as the "Official plugins" home) is a separate, downstream repository that consumes this framework. From Utinni's perspective:

- **Coupling:** A plugin DLL references `UtinniCoreDotNet.dll` (managed plugin) or `UtinniCore.dll` (C++ plugin). It is loaded from `<install>/Plugins/<PluginDir>/<Plugin>.dll` either via MEF (`UtinniCoreDotNet/PluginFramework/PluginLoader.cs`) for managed plugins or via `LoadLibrary` + `GetProcAddress("createPlugin")` for native plugins (`UtinniCore/plugin_framework/plugin_manager.cpp` lines 129-150).
- **Plugin manifest:** `[Plugins] plugin_NN = <enabled>, <DirName>` entries in `ut.ini` (`plugin_manager.cpp` lines 57-83). UtinniCore auto-appends any directory found under `Plugins/` that isn't yet listed.
- **No build-time dependency from Utinni → UtinniPlugins.** The relationship is one-way: UtinniPlugins consumes Utinni.

---

*Integration audit: 2026-05-16*
