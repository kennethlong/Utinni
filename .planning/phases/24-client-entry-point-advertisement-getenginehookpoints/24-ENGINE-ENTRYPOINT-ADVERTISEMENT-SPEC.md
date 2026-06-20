# SWG-Source Engine Entry-Point Advertisement (`GetEngineHookPoints`) — Instrumentation Spec

**Status:** SPEC — ready to hand to the `swg-client-v2` implementer instance. Not yet implemented.
**Author:** Utinni Phase 24 (Client Entry-Point Advertisement)
**Date:** 2026-06-20
**Target repo:** `D:/Code/swg-client-v2` (the SWG-Source client; `SwgClient_r.exe`)
**Consumer:** Utinni `UtinniCore.dll` (injected 32-bit modding overlay)
**Scope:** 32-bit only (x64 is out of v2.1; it is 999.7's other half, user-locked-deferred).
**Companion to:** `swg-client-v2/.planning/handoff/2026-06-15-utinni-dx11-hookpoint-advertisement-spec.md`
(the *graphics-side* `gl11_r.dll!GetHookPoints`, already SHIPPED). This is its **exe-side game-logic twin**.

---

## 0. TL;DR for the implementer

The graphics handoff you already shipped (`gl11_r.dll!GetHookPoints`, 3 DXGI pointers) solved the
**render** attach. This spec solves the **game-logic** attach: today UtinniCore reaches ~230 engine
functions on the client by **hardcoded SWGEmu RVAs** (`(pFn)0x00B21B80`, etc.). Those literals match
the Pre-CU SWGEmu `SWG.exe` byte-for-byte but match **nothing** in your from-source `SwgClient_r.exe`
— so injecting Utinni into your client crashes on the very first detour:

> `VEH FATAL 0xC0000005 … READ target=0x00401000` — the first detour off `swg::config::detour()`,
> `Config::loadOverrideConfig` hardcoded as `(pLoadOverrideConfig)0x00401000`.

**Your deliverable:** add **one new exported C function** to `SwgClient_r.exe` that hands Utinni a
**name→pointer table** of the engine functions it hooks, each entry populated at **compile time by
symbol reference** (`(void*)&ConfigFile::loadFromBuffer`). The client stays completely Utinni-agnostic
— `GetEngineHookPoints()` is a pure read-only getter; if Utinni is not injected, nothing calls it.

```cpp
// New shared header (single source of truth, generated; see §4) — utinni_engine_hookpoints.h:
struct UtinniEngineHookPoint { const char* name; void* addr; };   // one row per endpoint
struct UtinniEngineHookPoints {
    unsigned int version;                       // contract version; bump on any name add/remove
    unsigned int count;                         // number of rows below
    const UtinniEngineHookPoint* entries;       // static array, NUL-name terminated optional
};

// New, in the exe (e.g. beside main / in a small utinni_advertise.cpp), exported undecorated:
extern "C" __declspec(dllexport) const UtinniEngineHookPoints* __cdecl GetEngineHookPoints();
```

The table rows you populate look like:

```cpp
static const UtinniEngineHookPoint s_engineHookPoints[] = {
    { "config::loadOverrideConfig", (void*)&ConfigFile::loadFromBuffer },   // ← confirm the real symbol
    { "game::install",              (void*)&Game::install },
    { "graphics::install",          (void*)&Graphics::install },
    // … one row per endpoint in §6 …
};
static const UtinniEngineHookPoints s_table = { UTINNI_HOOKPOINTS_VERSION,
    (unsigned)(sizeof s_engineHookPoints / sizeof s_engineHookPoints[0]), s_engineHookPoints };
const UtinniEngineHookPoints* GetEngineHookPoints() { return &s_table; }
```

That is the whole contract. **Everything else** (detouring, the overlay, dual-path selection, the
no-Reset D3D9 contract) lives on Utinni's side and is NOT your concern.

**Critical division of labor:** the §6 catalog gives you, for each endpoint, its **stable contract
name**, the **UtinniCore signature-source** (`file:line` of the `using pFn = …` typedef — the
authoritative prototype), and its **SWGEmu RVA** (informational only — proof of what the function *is*
on Pre-CU). **You map each name to `&YourEngineSymbol` from your own source tree.** A best-guess engine
symbol is given where derivable, but **confirm every one against your source** — a wrong `&` is worse
than a missing row (a missing row degrades gracefully; a wrong row detours the wrong function).

---

## 1. Why this exists (context)

Utinni is an in-process ImGui modding overlay injected into the live SWG client. It installs ~40
detours and reads ~25 engine globals to drive editors (terrain, world-snapshot, object templates,
chat-context routing, the camera, etc.). On the **SWGEmu Pre-CU** client it locates every one of those
by a **hardcoded RVA** baked into UtinniCore at build time — a literal like `(pFn)0x00B21B80`. That is
fast and zero-cooperation, and it will **keep working unchanged** on SWGEmu (see §3 dual-path).

Your `SwgClient_r.exe` is a *different binary* built from source — same engine semantics, totally
different addresses. No hardcoded RVA matches. Rather than maintain a second brittle RVA table per
client build (and re-bless it on every recompile), we want the client to **advertise its own entry
points cooperatively**, exactly as you already did for the D3D11 render hooks. Because you build the
client, you can take `&fn` at compile time — the addresses are then always correct by construction and
survive every rebuild for free.

Benefits over a second hardcoded RVA table (why advertisement):
- **Always correct.** `&fn` is resolved by the linker; it can never drift from the actual code.
- **Self-documenting + greppable.** The table *is* the list of what Utinni touches.
- **Graceful degradation.** A name Utinni doesn't find simply stays on its fallback / disables that
  feature — no crash. A name you haven't populated yet just means that one editor is dark.
- **Version-tolerant.** Adding endpoints later is additive; the `version` field + name lookup means old
  and new can coexist.

---

## 2. What Utinni needs (the requirements behind the contract)

| Need | Why | How satisfied by this spec |
|------|-----|----------------------------|
| Address of each hooked engine fn | Install a detour / call the original | A `{name, &fn}` row in the table |
| Address of each read engine global | Read singletons/state (GroundScene, CuiManager, Terrain, render-target W/H, …) | A `{name, &global}` row (same table; the value is the global's address) |
| A way to tell "advertised client" from SWGEmu | Auto-select discovery path, no config toggle | Presence of the `GetEngineHookPoints` export (absent on SWGEmu) |
| Stable identity per endpoint across builds | Resolve by meaning, not by address | The **name** column (e.g. `"object::addToWorld"`) is the contract key |
| Detect a partial/missing contract | Log + degrade, never crash | `version` + `count` + per-name lookup; Utinni coverage-checks its required set |

Utinni does **all** detour installation, trampolining, overlay rendering, and dual-path selection
itself. The contract is purely: *expose the address of each named endpoint when the process is live.*

> **Detours vs reads:** ~40 of these endpoints are *detoured* (Utinni installs a hook); the rest are
> *called* (Utinni invokes the original to drive an editor) or *read* (globals). From your side there is
> **no difference** — every row is just `{name, address}`. You do not need to know which Utinni hooks.

---

## 3. Dual-path discovery (how Utinni picks advertised vs hardcoded)

UtinniCore runs **both** discovery paths and auto-selects with **no config toggle** (EPA-02):

```
at init, after module is mapped:
  hExe = GetModuleHandle(NULL)                          // the client exe
  pGet = GetProcAddress(hExe, "GetEngineHookPoints")
  if (pGet) {                                            // → advertised client (SwgClient_r.exe)
      table = pGet()
      for each required swg::* endpoint:
          addr = lookupByName(table, endpoint.name)
          if (addr) endpoint.fn = addr
          else      log("missing endpoint: " + name); endpoint.fn = nullptr  // feature degrades
      if (table->version != UTINNI_HOOKPOINTS_VERSION) log a soft mismatch warning
  } else {                                               // → SWGEmu Pre-CU
      use the existing hardcoded-RVA literals unchanged   // NO regression to the working client
  }
```

So: **SWGEmu keeps its hardcoded path verbatim** (the existing D3D9 live-smoke must still pass), and
the advertised path lights up **only** when your export is present. An older/unpatched client without
the export is detected and bails cleanly to the SWGEmu path or to no-overlay (EPA-04) — it never
dereferences a hardcoded literal on the advertised client.

---

## 4. Single source of truth (no drift between the two repos)

The endpoint **name list** must not drift between your export and Utinni's consumer. Use **one shared
generated header**, `utinni_engine_hookpoints.h`, committed to both repos from a single generator (or
committed to Utinni and copied into `swg-client-v2`, the same way this very doc is dual-placed):

- It defines `UtinniEngineHookPoint` / `UtinniEngineHookPoints` (the structs in §0) and
  `#define UTINNI_HOOKPOINTS_VERSION n`.
- It declares the canonical **name constants** (or an X-macro list) so both sides spell every name
  identically. Recommended: an X-macro so the name list exists exactly once:

  ```cpp
  // utinni_engine_hookpoints.inc  — the canonical name list (shared, generated)
  //   UTINNI_HOOKPOINT(group, name)
  UTINNI_HOOKPOINT(config,   loadOverrideConfig)
  UTINNI_HOOKPOINT(game,     install)
  UTINNI_HOOKPOINT(graphics, install)
  // … one line per §6 endpoint …
  ```

  Your side expands it to table rows with `&fn`; Utinni's side expands it to the resolver's required
  set + the coverage assertion. Neither side can add/remove a name without the other seeing it.

- **`version`** is bumped whenever a name is added or removed. A consumer on an older version logs a
  soft mismatch and resolves by name anyway (missing names degrade; extra names are ignored).

> This is the EPA "generated header shared by both repos so the field/name list cannot drift" decision
> from ROADMAP §192. If an X-macro is awkward in your build, a hand-maintained header is acceptable —
> the coverage test (§7) is what actually guarantees zero drift.

---

## 5. Export mechanics (match how `GetApi` / `GetHookPoints` are exported today)

- **Calling convention:** `__cdecl`, `extern "C"` (no name mangling). 32-bit.
- **Module:** the **exe** (`SwgClient_r.exe`), resolved by Utinni via
  `GetProcAddress(GetModuleHandle(NULL), "GetEngineHookPoints")`. (The graphics twin lives in
  `gl11_r.dll` because it only sees graphics symbols; this one must live in the exe because the game
  symbols — `Game`, `Object`, `CuiManager`, `WorldSnapshot`, … — are linked into the exe.)
- **Undecorated name:** if your toolchain prefixes `__cdecl` exports with a leading underscore, force
  the undecorated name with a `.def` entry or
  `#pragma comment(linker, "/EXPORT:GetEngineHookPoints")` — exactly as you did to keep `GetHookPoints`
  undecorated alongside `GetApi`. Verify with `dumpbin /exports stage/SwgClient_r.exe`.
- **Return by pointer-to-static** (`const UtinniEngineHookPoints*`) — the table is a process-lifetime
  static, so there are no lifetime concerns; Utinni only reads it.
- **Build suffixes:** export from **every** client flavor you build — `_r` (release), `_d` (debug),
  `_o` (optimized), etc. Utinni resolves by the live module handle, so whichever exe is running just
  works. The name list is identical across suffixes; only the addresses differ (handled for free by
  `&fn`).

---

## 6. THE COMPLETE CATALOG — every endpoint UtinniCore hooks/reads

**How to read this table.** Per row:
- **Name** — the stable contract identity. Spell it exactly in your table and the shared header.
- **UtinniCore signature-source** — `file:line` in `D:/Code/Utinni/UtinniCore/` of the
  `using pFn = ret(conv*)(args)` typedef. **This is the authoritative prototype** — match your `&fn`'s
  signature to it. (UtinniCore declares, per subsystem, `using pFn = …; pFn fn = (pFn)0xRVA;` — the
  typedef is the contract, the RVA is just the SWGEmu instance.)
- **SWGEmu RVA** — informational. It is what the function *is* on Pre-CU SWGEmu, to help you identify
  the matching engine symbol. **Do not** transcribe it into your client; take `&fn` instead.
- **Engine symbol (best guess)** — a likely `swg-client-v2` source symbol. **CONFIRM every one.**
  Where left blank, resolve from your source by the Name + signature.

> ⚠️ **Authority note:** the SWGEmu RVA column is reproduced from a UtinniCore source sweep and is for
> *identification only*. If any RVA here disagrees with the live UtinniCore source, **the UtinniCore
> source governs** (`UtinniCore/swg/<subsystem>/*.{h,cpp}`). Your `&fn` mapping does not depend on the
> RVA being perfect — it depends on the Name + signature-source.

### MVP subset (boot → render overlay → TJT scene-change repro path)

These are the endpoints needed to **boot without crashing, render the overlay, and exercise the
TJT-driven scene-change repro** (ROADMAP §193). Populate these FIRST — they unblock the Phase 18 (D-08)
and Phase 19 (D-22) live-smokes. Groups: `config`, `client`, `graphics`, `game`, `scene`, `cui`,
`command_parser`.

| Name | UtinniCore signature-source | SWGEmu RVA | Engine symbol (CONFIRM) |
|------|------------------------------|-----------|--------------------------|
| `config::loadOverrideConfig` | swg/misc/config.cpp:39 | 0x00401000 | `ConfigFile::loadFromBuffer` / override loader — **the first detour; this is the crash** |
| `config::loadConfigFileBuffer` | swg/misc/config.cpp:37 | 0x00A9C6C0 | `ConfigFile::loadFromBuffer(byte*,int)` |
| `config::loadConfigFileString` | swg/misc/config.cpp:38 | 0x00A9C780 | `ConfigFile::loadFromString(const char*)` |
| `config::setModalChat` | swg/misc/config.cpp:41 | 0x00910A70 | modal-chat setter |
| `config::getModalChat` | swg/misc/config.cpp:42 | 0x00910D40 | modal-chat getter |
| `client::setupStartDataInstall` | swg/client/client.cpp:40 (`pSetupInstall`) | 0x00A9F970 | client StartupData install |
| `client::clientMain` | swg/client/client.cpp:41 (`pMainLoop`) | 0x00401050 | client main loop |
| `client::wndProc` | swg/client/client.cpp:43 (`pWndProc`, `__stdcall`) | 0x00AA0970 | SWG top-level `WndProc` |
| `client::writeCrashLog` | swg/client/client.cpp:45 | 0x00A9F640 | crash-log writer |
| `client::writeMiniDump` | swg/client/client.cpp:46 | 0x00A8A170 | minidump writer |
| `graphics::install` | swg/graphics/graphics.cpp:61 | 0x007548A0 | `Graphics::install` (DX11 kickoff decouples from this — EPA-03) |
| `graphics::update` | swg/graphics/graphics.cpp:63 | 0x00755700 | `Graphics::update` |
| `graphics::beginScene` | swg/graphics/graphics.cpp:64 | 0x00755730 | `Graphics::beginScene` |
| `graphics::endScene` | swg/graphics/graphics.cpp:65 | 0x00755740 | `Graphics::endScene` |
| `graphics::present` | swg/graphics/graphics.cpp:68 | 0x00755800 | `Graphics::present` |
| `graphics::presentWindow` | swg/graphics/graphics.cpp:67 | 0x00755810 | `Graphics::presentWindow` |
| `graphics::resize` | swg/graphics/graphics.cpp:74 | 0x00754E40 | `Graphics::resize` |
| `graphics::flushResources` | swg/graphics/graphics.cpp:75 | 0x00755520 | `Graphics::flushResources` |
| `graphics::screenshot` | swg/graphics/graphics.cpp:83 | 0x00755890 | `Graphics::screenshot` |
| `graphics::useHardwareCursor` | swg/graphics/graphics.cpp:70 | 0x00755940 | hardware-cursor toggle |
| `graphics::showMouseCursor` | swg/graphics/graphics.cpp:71 | 0x00755A50 | mouse-cursor show |
| `graphics::setSystemMouseCursorPosition` | swg/graphics/graphics.cpp:72 | 0x00755AC0 | system cursor pos |
| `graphics::setStaticShader` | swg/graphics/graphics.cpp:79 | 0x00755910 | static-shader set |
| `graphics::g_renderTargetWidth` (global) | swg/graphics/graphics.cpp:556 | 0x01922E64 | RT width global |
| `graphics::g_renderTargetHeight` (global) | swg/graphics/graphics.cpp:561 | 0x01922E60 | RT height global |
| `game::install` | swg/game/game.cpp:56 | 0x00422E80 | `Game::install` |
| `game::quit` | swg/game/game.cpp:57 | 0x00423720 | `Game::quit` |
| `game::mainLoop` | swg/game/game.cpp:58 | 0x004237C0 | `Game::runGame`/main loop |
| `game::setupScene` | swg/game/game.cpp:60 | 0x00424220 | `Game::setupScene` |
| `game::cleanupScene` | swg/game/game.cpp:61 | 0x00423700 | `Game::cleanupScene` |
| `game::getPlayer` | swg/game/game.cpp:63 | 0x00425140 | `Game::getPlayer` |
| `game::getPlayerCreatureObject` | swg/game/game.cpp:64 | 0x004251D0 | `Game::getPlayerCreatureObject` |
| `game::getCamera` | swg/game/game.cpp:66 | 0x00425BB0 | `Game::getCamera` |
| `game::getConstCamera` | swg/game/game.cpp:67 | 0x00425BE0 | `Game::getConstCamera` |
| `game::isViewFirstPerson` | swg/game/game.cpp:69 | 0x00425C10 | first-person view test |
| `game::isHudSceneTypeSpace` | swg/game/game.cpp:70 | 0x00426170 | space-HUD test |
| `game::g_mainLoopCounter` (global) | swg/game/game.cpp:339 | 0x01908830 | mainloop counter |
| `game::g_runningFlags` (globals) | swg/game/game.cpp:600 | 0x01908858, 0x01919410 | run-state flags |
| `scene::groundScene::ctor` | swg/scene/ground_scene.cpp:58 | 0x00519830 | `GroundScene::GroundScene` (scene-change AV at +0x22da; heap-free dispatch required) |
| `scene::groundScene::init` | swg/scene/ground_scene.cpp:68 | 0x00518EB0 | `GroundScene::init` |
| `scene::groundScene::reloadTerrain` | swg/scene/ground_scene.cpp:59 | 0x0051A4F0 | `GroundScene::reloadTerrain` |
| `scene::groundScene::changeCamera` | swg/scene/ground_scene.cpp:60 | 0x0051A350 | `GroundScene::changeCamera` |
| `scene::groundScene::getCurrentCamera` | swg/scene/ground_scene.cpp:61 | 0x0051A4D0 | `GroundScene::getCurrentCamera` |
| `scene::groundScene::draw` | swg/scene/ground_scene.cpp:63 | 0x0051B770 | `GroundScene::draw` |
| `scene::groundScene::update` | swg/scene/ground_scene.cpp:64 | 0x0051AF10 | `GroundScene::update` |
| `scene::groundScene::handleInputMapUpdate` | swg/scene/ground_scene.cpp:65 | 0x0051AB20 | input-map update |
| `scene::groundScene::handleInputMapEvent` | swg/scene/ground_scene.cpp:66 | 0x0051AA40 | input-map event |
| `scene::groundScene::g_instance` (global) | swg/scene/ground_scene.cpp:161 | 0x0190885C | GroundScene singleton ptr |
| `cui::manager::render` | swg/ui/cui_manager.cpp:42 | 0x00881210 | `CuiManager::render` |
| `cui::manager::findObjectUnderCursor` | swg/ui/cui_manager.cpp:43 | 0x00BD3E20 | find-under-cursor |
| `cui::manager::setSize` | swg/ui/cui_manager.cpp:45 | 0x00882410 | `CuiManager::setSize` |
| `cui::manager::togglePointer` | swg/ui/cui_manager.cpp:46 | 0x00881940 | pointer toggle |
| `cui::manager::restartMusic` | swg/ui/cui_manager.cpp:47 | 0x00881560 | restart music |
| `cui::manager::g_instance` (global) | swg/ui/cui_manager.cpp:183 | 0x01996E98 | CuiManager singleton |
| `cui::io::processEvent` | swg/ui/cui_io.cpp:154 (from vtable) | — | `CuiIoWin::processEvent` |
| `cui::io::setKeyboardInputActive` | swg/ui/cui_io.cpp:39 | 0x0093D490 | kbd-input active set |
| `cui::io::requestKeyboard` | swg/ui/cui_io.cpp:40 | 0x0093D560 | request keyboard |
| `cui::io::draw` | swg/ui/cui_io.cpp:41 | 0x0093B2B0 | `CuiIoWin::draw` |
| `cui::io::g_instance` (global) | swg/ui/cui_io.cpp:59 | 0x0192613C | CuiIo singleton |
| `cui::chatWindow::ctor` | swg/ui/cui_chat_window.cpp:56 | 0x00F364B0 | `SwgCuiChatWindow::ctor` |
| `cui::chatWindow::enableTextInput` | swg/ui/cui_chat_window.cpp:57 | 0x00F38500 | enable text input |
| `cui::chatWindow::writeToAllTabs` | swg/ui/cui_chat_window.cpp:58 | 0x00F3BFD0 | write to all tabs |
| `cui::chatWindow::writeToCurrentTab` | swg/ui/cui_chat_window.cpp:59 | 0x00F3C1F0 | write to current tab |
| `cui::chatWindow::chatEnterHandler` | swg/ui/cui_chat_window.cpp:54 | 0x00F3E420 | chat-Enter handler (Issue #11 context routing) |
| `cui::consoleHelper::sendInput` | swg/ui/cui_chat_window.cpp:65 | 0x009141D0 | `SwgCuiConsoleHelper::sendInput` |
| `commandParser::ctor1` | swg/ui/command_parser.cpp:35 | 0x00A83EF0 | `CommandParser` ctor (1) |
| `commandParser::ctor2` | swg/ui/command_parser.cpp:36 | 0x00A84130 | `CommandParser` ctor (2) |
| `commandParser::createDelegateCommands` | swg/ui/command_parser.cpp:38 | 0x00A862F0 | create delegate cmds (TJT scene-change parser path) |
| `commandParser::addSubCommand` | swg/ui/command_parser.cpp:39 | 0x00A85CD0 | add sub-command |

### Full set (remaining ~30 subsystems) — populate after MVP is green

The MVP above boots + renders + drives the scene-change repro. The endpoints below light up the rest
of the editors (object/world-snapshot/terrain/appearance/camera editors, etc.). Same row format; same
`&fn` rule. Grouped by subsystem.

**`object` / `client_object` / `creature_object` / `player_object`** — `UtinniCore/swg/object/`
`object.cpp:131-174`, `client_object.cpp:42-63`, `creature_object.cpp:36`, `player_object.cpp:34`:
`object::ctor` 0x00B21B80, `getType` 0x00B23C60, `setParentCell` 0x00B22C30, `addToWorld` 0x00B225F0,
`removeFromWorld` 0x00B22680, `move` 0x00B23960, `getTransform_o2w` 0x00B22C80, `setTransform_o2w`
0x00B22CC0, `getTransform_a2w` 0x00B22E90, `setTransform_a2w` 0x00B22E10, `getPosition` 0x00B243F0,
`setPosition` 0x00B23960, `setScale` 0x00B23A10, `getAppearance` 0x00B22FF0, `setAppearance`
0x00B22F60, `getAppearanceFilename` 0x00B243E0, `setAppearanceByFilename` 0x00B243A0,
`addNotification` 0x00B225A0, `removeNotification` 0x00B225D0, `getParentCell` 0x00B22C00,
`setObjectToWorldDirty` 0x00B24CE0, `positionAndRotationChanged` 0x00B22A50, `getClientObject`
0x00554BC0, `getTemplateFilename` 0x00B23C40; `clientObject::setParentCell` 0x00555410,
`getGameObjectType` 0x00557360, `getGameObjectTypeStringIdKey` 0x00557370, `getGameObjectTypeName`
0x00557390, `getStaticObject` 0x00554BF0, `getTangibleObject` 0x00554C00, `ctor` 0x0070DBB0,
`addToWorld` 0x0070DD00, `removeFromWorld` 0x0070DD20; `creatureObject::setTarget` 0x00434AB0;
`playerObject::teleportPlayer` 0x0062A8B0; player health/stats global 0x0191BFB4 (+0x674).

**`objectTemplate` / asset loading** — `UtinniCore/swg/object/object.cpp:43-81`, `swg/misc/crc_string.cpp`:
`getByFilename` 0x00B28700, `getByIff` 0x00B28720, `getByCrc` 0x00B28740, `reload` 0x00B289B0,
`getCrcStringByName` 0x00B28A10, `getCrcStringByCrc` 0x00B28AA0, `createObject` 0x00B2E760,
`getAppearanceFilename` 0x011A6C10, `getPortalLayoutFilename` 0x011A6D30, `getClientDataFilename`
0x011A6E50, `getGameObjectType` 0x011A8B60, `getTerrainLayerFilename` 0x01231910,
`getInteriorLayoutFilename` 0x01231A30; `crcString::constCharCrcString_ctor` 0x00AA55B0,
`persistentCrcString_ctor` 0x00AA4050.

**`appearance` / `portal` / `skeleton` / `extent`** — `UtinniCore/swg/appearance/`:
`appearance::createAppearance` 0x00B262A0, `collide` 0x00B2C410, `ctor` 0x007A85A0, `render` 0x007A8A50;
`portal::getCrc` 0x00B47BD0, `getCellCount` 0x00B47BE0, `getExteriorAppearanceName` 0x00B47C90,
`getPobByCrcString` 0x00B497E0, `setPortalTransitions` 0x00B2A990;
`skeletalAppearance::addShaderPrimitives` 0x007E6C50, `render` 0x007C8B60, `getDisplayLodSkeleton`
0x007CA130; `extent::intersect` 0x0126AF70 **(retail)** / 0x0125FA10 **(SWGEmu)** — note the
build-specific split, `extent.cpp:32,45`; `intersect3` 0x0126AFB0 (retail).

**`scene::terrain` / `worldSnapshot` / `proceduralTerrainAppearance`** — `UtinniCore/swg/scene/`:
`terrain::reloadTerrain` 0x0051A4F0, `setTimeOfDay` 0x00B5CBD0, `getTimeOfDay` 0x00B5CBC0,
`setWeatherIndex` 0x00845C90, singleton 0x01947194, weather-idx global 0x01924B6C, filename global
0x019113C1; `worldSnapshot::openFile` 0x00B97D90, `saveFile` 0x00B98120, `clear` 0x00B98290,
`getObjectTemplateName` 0x00B98720, `nodeCount` 0x00B986A0, `nodeCountTotal` 0x00B986D0,
`getNodeByNetworkId` 0x00B98740, `getNodeByIndex` 0x00B986B0, `addNode` 0x00B98410, `removeNode`
0x00B98780, `getNodeNetworkId` 0x00B971D0, `getNodeSpatialSubdivisionHandle` 0x00B97390,
`setNodeSpatialSubdivisionHandle` 0x00B973A0, `removeFromWorld` 0x00B97440;
`proceduralTerrainAppearance::load` 0x0059C380, `unload` 0x0059C1D0, `clearPreloadList` 0x00404D50,
`createObject` 0x0059BBA0, `addObject` 0x0059BF20, `detailLevelChanged` 0x0059DC30.

**`camera` / `debugCamera`** — `UtinniCore/swg/camera/`:
`camera::getViewportInt` 0x00767DF0, `getViewportFloat` 0x00767E40, `setViewport` 0x00767E90,
`setNearPlane` 0x00767EC0, `setFarPlane` 0x00767EE0, `setHorizontalFieldOfView` 0x00767F00,
`reverseProjectInViewportSpaceInt` 0x007682B0, `reverseProjectInViewportSpaceFloat` 0x00768390,
`addExcludedObject` 0x00778FE0, `clearExcludedObjects` 0x00779130, `alter` 0x00788740;
`debugCamera::alter` 0x006DA1B0.

**`scene::clientWorld` (collision) / `renderWorld`** — `UtinniCore/swg/scene/`:
`clientWorld::collide` 0x00561350, `internalCollide` 0x00562940, `internalCollideFindAllObjects`
0x00562680; `renderWorld::addObjectNotifications` 0x007664F0, `render` 0x00766DE0.

**`cui::hud` / `cui::misc` (login) / `cui::radialMenu` / `cui::menu` / `io_win` / `systemMessage`** —
`UtinniCore/swg/ui/`:
`hud::update` 0x00BD56F0, `actionPerformAction` 0x00EDBAA0, `getTarget` 0x00BD3E20, view-distance
global 0x019488C8; `gameMenu::ctor` 0x00C7D360; `loginScreen::ctor` 0x00C8CE00, `activate` 0x00C8D190,
`login` 0x00C8D5D0; `radialMenu::update` 0x009698C0, `clear` 0x0096C550;
`menu::infoTypesFindDefaultCursor` 0x00A08EE0; `messageQueue::appendMessage` 0x00AA6640,
`appendMessageData` 0x00AA6480, `getMessage` 0x00AA63B0, `getCount` 0x00AA6660; `ioWin::draw`
0x00AB58E0; `systemMessage::receiveMessage` 0x008ABEB0, `sendMessage` 0x008AC250.

**`misc` (input / audio / treefile / math / memory / report / network)** — `UtinniCore/swg/misc/`:
`directInput::setupInstall` 0x00421490, `suspend` 0x00420880, `resume` 0x00420890; `audio::setMasterVolume`
0x00412C20, `getMasterVolume` 0x00412C70; `treeFile::open` 0x00A931E0 (`swg_utility.cpp:33`),
`searchTree` (resolved via detour, `tree_file.cpp:71`); `crcString::calculateCrc` 0x00AA4790;
`math::vectorNormalize` 0x00AB5C40; `memory::allocate` 0x00AC15C0, `allocateString` 0x012EA770,
`deallocate` 0x00AC1640, `deallocateString` 0x012EA920; `report::print` 0x00A88F90;
`network::idManagerGetObjectById` 0x00B380E0, `idManagerGetInstance` 0x00B37F30,
`cachedNetworkIdGetObject` 0x00B30160.

**`graphics::shader` / `post_processing`** — `UtinniCore/swg/graphics/`:
`shader::popCell` mid-function (target 0x772D60, `shader.cpp:116-117`); `bloomShader::preSceneRender`
0x0064B500, `postSceneRender` 0x0064B560.

**Low-level UI control constructors (`UI*::ctor`)** — `UtinniCore/swg/ui/ui_*.cpp:31`:
`uiBaseObject` 0x010F2A00, `uiButton` 0x011149E0, `uiCursor` 0x0112C630, `uiCursorSet` 0x0116A360,
`uiData` 0x01133130, `uiDropdownBox` 0x0117F540, `uiGrid` 0x0117B990, `uiList` 0x011388F0, `uiListBox`
0x011369A0, `uiPage` 0x010FD200, `uiPie` 0x01134080, `uiProgressBar` 0x0117E860, `uiSliderPlane`
0x0117D600, `uiTable` 0x0113E510, `uiTabSet` 0x0117AC30, `uiText` 0x0110ED20, `uiTextBox` 0x0112CFC0,
`uiTextBox::setLocalText` 0x01120250, `uiTreeView` 0x011549C0, `uiUnknown` 0x01167510, `uiWidget`
0x01105910.

**Globals (read-only singletons/state)** — addresses are of the **global**, advertise as `(void*)&g`:
`config` login avatar 0x01911218, login groundScene 0x01911240; static-shader 0x01922F8C; plus all the
per-subsystem singletons/globals noted inline above (GroundScene/CuiManager/CuiIo/Terrain/render-target
W&H/etc.).

### Endpoints you do NOT need to advertise

These are Utinni-internal or resolved without a client symbol — **skip them** in your table:
- **D3D9 device vtable hooks** (`Present`/`Reset`/`EndScene`/`BeginScene`/`DrawIndexedPrimitive`/
  `SetRenderTarget`/`SetDepthStencilSurface`, vtbl idx 17/16/42/41/82/37/39) — Utinni harvests these
  off the live device vtable at runtime; no client symbol needed. On **D3D11** they are replaced by the
  already-shipped `gl11_r.dll!GetHookPoints` (swapchain Present idx 8 / ResizeBuffers idx 13).
- **`D3DXCompileShader`** (`s207_r.dll` 0x62A4F9DB) — resolved in the D3DX DLL, not your exe.
- **`IDirectInput8A::CreateDevice` / `SetCooperativeLevel` vtable writes** — Utinni patches the COM
  object instance after creation; no symbol export needed. (Advertise `directInput::setupInstall`/
  `suspend`/`resume` from the MVP/misc set if you want the input-suspend editor behavior; the vtable
  writes themselves are Utinni-side.)
- **Mid-function JMP patches / NOP / byte patches** (CrashLog inline hook 0x00A9F766, ChatWindow ctor
  mid-hook, `shader::popCell`, chat-context NOPs, UI-cascade NOPs, DebugCamera input-suppress NOP,
  WorldSnapshot `.trn`-name NOP) — these patch *into the middle* of a function body and depend on the
  exact Pre-CU instruction layout. **They cannot be expressed as `&fn`** and do not port to your build
  by address. They are an **open item** (§8) — for the advertised client they will need a *function-
  entry* equivalent or a small cooperative shim, decided when those editors are smoke-tested on your
  client. The MVP boot/render/scene path does **not** require any mid-function patch.

> Approximate totals (from the UtinniCore sweep): ~230 hardcoded RVAs + ~25 read globals across ~30
> subsystems ≈ the "~198 across ~30 subsystems" the ROADMAP estimated (the sweep found somewhat more
> once every `UI*::ctor` and global is counted). The **coverage test (§7)** makes "complete" exact —
> trust it over any hand count.

---

## 7. Acceptance — how Utinni will consume this (so you know "done")

1. **Export present, undecorated.** `dumpbin /exports stage/SwgClient_r.exe` shows
   `GetEngineHookPoints` (and on `_d`/`_o` flavors). Utinni resolves it via
   `GetProcAddress(GetModuleHandle(NULL), "GetEngineHookPoints")`.
2. **First-detour no longer crashes.** Injecting UtinniCore resolves `config::loadOverrideConfig`
   through the table — the exact `0xC0000005 … target=0x00401000` crash from 2026-06-15 is gone
   (success-criterion 1 / EPA-02).
3. **Coverage = zero missing (required set).** A coverage test asserts every endpoint in Utinni's
   *required* set (the shared `utinni_engine_hookpoints.inc` list, MVP first then full) has a non-null,
   non-duplicate pointer in the advertised table. Missing names are logged and that feature degrades;
   **zero missing in the required set is the gate** (success-criterion 2 / EPA-04). Build this test on
   your side against the shared header so the two repos cannot drift.
4. **No SWGEmu regression.** The existing SWGEmu Pre-CU D3D9 live-smoke still passes unchanged — the
   hardcoded path is untouched; advertisement lights up only when the export is present
   (success-criterion 3 / EPA-02).
5. **DX11 overlay starts off the contract.** With the graphics `GetHookPoints` (shipped) + this
   `GetEngineHookPoints`, the Phase-19 DX11 overlay installs + renders on `SwgClient_r.exe` with the
   kickoff no longer gated on a hardcoded `graphics::install` address (success-criterion 4 / EPA-03) —
   closing the Phase 18 (D-08) and Phase 19 (D-22) live-smokes.

**Your deliverable is complete when:** `SwgClient_r.exe` exports `GetEngineHookPoints` (undecorated,
`__cdecl`) returning the name→pointer table populated by `&fn`, the MVP subset (§6) is fully populated,
the shared header `utinni_engine_hookpoints.h`/`.inc` exists in both repos, the coverage test passes
for the MVP set, and a build is staged. Full-set population + its coverage gate follow in a second pass.

---

## 8. Open items (resolve in discuss/plan, not blocking the §6 MVP)

1. **Mid-function patches (§6 "do NOT advertise" list).** ~4 JMP + ~15 NOP/byte patches hook *into*
   function bodies at Pre-CU instruction offsets. They don't port by address and can't be `&fn`. For
   the advertised client each needs either (a) a function-*entry* hook that achieves the same effect,
   (b) the engine exposing a cooperative toggle (e.g. a real "modal chat context" setter you already
   have), or (c) acceptance that the specific editor behaviour (chat-context routing Issue #11, UI Y-axis
   cascade, debug-camera input suppress, arbitrary `.ws` load) is SWGEmu-only until ported. None block
   boot/render/scene. Triage these when the dependent editors are smoke-tested on your client.
2. **`extent::intersect` build split.** UtinniCore already carries two RVAs (retail 0x0126AF70 vs
   SWGEmu 0x0125FA10). With `&fn` this split disappears for your client — one more reason advertisement
   beats RVA tables.
3. **Globals as `&g`.** Confirm the read-only globals (singletons, render-target W/H, weather index,
   filenames) are addressable by symbol in your build (they are statics — `(void*)&g` is fine). If any
   is a local static behind an accessor, advertise the accessor instead and tell Utinni (it can call vs
   read).
4. **Contract shape sanity.** This spec specifies a **name→pointer table** (ROADMAP §191 "leaning
   table"). If during implementation you find a flat versioned struct easier on your side, that's
   acceptable — but the table's graceful-degradation + version-tolerance is why it's recommended;
   coordinate the shape with Utinni's consumer before diverging.

---

## 9. Anchors quick-reference

**UtinniCore (consumer / signature-source) — `D:/Code/Utinni/UtinniCore/`:**
- `swg/client/client.cpp:30-47` — `client::*` typedefs + RVA literals (the per-subsystem pattern model).
- `swg/misc/config.{h,cpp}` — `config::*`, incl. the first-detour `loadOverrideConfig` 0x00401000.
- `swg/game/game.cpp:54-70` — `game::*`. `swg/graphics/graphics.cpp:61-83` — `graphics::*`.
- `swg/scene/{ground_scene,terrain,world_snapshot,client_world,render_world}.cpp` — scene set.
- `swg/object/{object,client_object,creature_object,player_object}.cpp` — object set.
- `swg/ui/{cui_manager,cui_io,cui_chat_window,cui_hud,cui_misc,cui_radial_menu,cui_menu,command_parser,io_win,ui_*}.cpp` — UI set.
- `swg/appearance/*`, `swg/camera/*`, `swg/misc/*` — remaining subsystems.

**swg-client-v2 (this implementer) — `D:/Code/swg-client-v2/`:**
- `src/engine/client/application/Direct3d11/Direct3d11.cpp:857-883` — the shipped graphics
  `GetHookPoints` (the pattern to mirror for the exe-side export).
- Add `GetEngineHookPoints` + `s_engineHookPoints[]` in a small `utinni_advertise.cpp` linked into the
  exe (near `main`/the engine entry), exported undecorated via `.def` or `/EXPORT`.

---

*Generated for Utinni Phase 24. Source of truth: this file in the Utinni repo
(`.planning/phases/24-client-entry-point-advertisement-getenginehookpoints/24-ENGINE-ENTRYPOINT-ADVERTISEMENT-SPEC.md`).
A copy is placed in `swg-client-v2/.planning/handoff/`. If the two diverge, the Utinni copy governs the
consumer contract. RVA columns are reproduced from a UtinniCore source sweep for identification only —
the live UtinniCore source governs the SWGEmu addresses; the Name + signature-source governs the `&fn`
mapping.*
