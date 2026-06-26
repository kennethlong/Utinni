# Phase 24 — Session Handoff (2026-06-25): advertised-client EDITOR UNLOCK, mid-flight

> **⚠️ SUPERSEDED (2026-06-25, later same day).** §1/§2 are DONE: v6 `game::loadScene` is wired,
> smoke-green, and committed (`e99e27c`, `0e4f2b3`, `505d2da`, `07f39d1`). In-editor scene loading works
> on the advertised client. **The current resume pointer for the remaining §4 follow-ups is
> `24-FOLLOWUPS-PLAN.md`** (committed `6ccb6c3`) — crew-reviewed plan + a first WS-0/WS-1 attempt that was
> smoked, root-caused, and reverted (Enter-mask scope bug + `getSpeed`-on-null-player crash). Read that,
> not §1, before resuming. The rest of this doc is retained as historical context for how the v6 work landed.

Resume pointer for the advertised-client editor-unlock arc (the follow-on after the v2.1 close-out).
This session got the **GAME subsystem + scene-list working** on the advertised DX11 client and is one
consumer-wiring step away from **in-editor scene loading**. Read this top-to-bottom before touching code.

---

## 0. STATUS in one paragraph

The advertised DX11 client (`SwgClient_r.exe`) now: resolves **96/96** hookpoints, **populates the TJT
scene list** (via the new `treeFile::enumerateFiles`), and gets through install + into the world. The
**editor "Load scene" still crashes** — but we have the fix in hand: the provider shipped **contract v6
`game::loadScene`** (a full-`SceneCreator` string-based load). The **only remaining consumer work is to
wire v6 `loadScene`** (re-sync + bind + one `hkMainLoop` change), then re-smoke and **commit the large
uncommitted batch**. Everything else (Game unlock, Repository population, embed clamp, diagnostics) is
built into the staged DLLs and working.

---

## 1. ⏭️ IMMEDIATE NEXT TASK — wire v6 `game::loadScene` (then re-smoke, then commit)

The provider re-staged `SwgClient_r.exe` (2026-06-25 14:59) at **contract v6 / 99 names**, adding
`game::loadScene → utinni_gameLoadScene(const char* terrain, const char* player)` →
`Game::setScene(true, terrain, player, nullptr)` (the full SceneCreator lifecycle). Handback:
`swg-client-v2/.planning/handoff/2026-06-25-utinni-scene-load-lifecycle-HANDBACK.md`.

**Why:** the SWGEmu "build a `GroundScene` + `setupScene(it)`" pattern does NOT port. `Game::_setScene(Scene*)`
(what `setupScene` is re-mapped to) only does `ms_scene = newScene` and skips the `_setScene(SceneCreator&)`
→ `_startScene()` lifecycle (loading-manager, deferred-creation, `endDeferredCreation`), so a pre-built
scene is half-integrated → engine throws ~1s later (`MyUnhandledExceptionFilter → Fatal → int3`). The
editor must drive the **string-based** load and let the engine build the scene.

**Steps (consumer = Utinni repo):**
1. **Re-sync the contract** byte-identical from the provider, sha256-verify:
   - `cp $SRC/engine_hookpoints.{h,inc} UtinniCore/swg/` where `$SRC=/d/Code/swg-client-v2/src/game/client/application/SwgClient/src/shared`
   - Expect: `engine_hookpoints.h` sha256 `b869747687dc1bef8490129eec30c3b1ca9a4b256b4f597f9b92ec6ae679afc0`,
     `engine_hookpoints.inc` sha256 `12143e1f1459aef663028edb8b46030a343ea9474bfd1576b07d1801cca36544`.
     Version becomes **6**, `.inc` count **99** (`game::loadScene` added after the v5 names).
2. **Bind it** in `UtinniCore/swg/endpoints_bindings.cpp`:
   - In `namespace swg::game` (the v4 additions block, near `g_mainLoopCounter`), add slot extern +
     typedef: `using pLoadScene = void(__cdecl*)(const char* terrain, const char* player); extern pLoadScene loadScene;`
   - Define the slot in `UtinniCore/swg/game/game.cpp` `namespace swg::game` (advertised-only, `= nullptr`,
     same pattern as `g_mainLoopCounter`): `using pLoadScene = void(__cdecl*)(const char* terrain, const char* player); pLoadScene loadScene = nullptr;`
   - Add the binding row + `kBindingNames` entry (lockstep) for `"game::loadScene"`.
   - Bump the static_asserts: `kIncCount 98→99`, `kBindingCount 96→97`.
3. **Bump test counts** in `UtinniCore.Tests/endpoints_tests.cpp` (title `96 of 98`→`97 of 99`,
   `kIncCount==98`→99, `expectedNames.size()==96`→97, `resolved==96`→97).
4. **Change `hkMainLoop`'s loadNewScene handler** (`UtinniCore/swg/game/game.cpp`, ~line 402, the
   `if (loadNewScene && sceneCleaned)` block). Currently:
   ```cpp
   swg::game::setupScene(GroundScene::ctor(sceneToLoadTerrainFilename.c_str(), sceneToLoadAvatarObjectFilename.c_str()));
   ```
   Make it per-target: on the advertised client call `swg::game::loadScene(terrain, avatar)` and DO NOT
   build a GroundScene; on SWGEmu keep the existing `setupScene(GroundScene::ctor(...))`:
   ```cpp
   if (swg::endpoints::isAdvertisedClient() && swg::game::loadScene != nullptr)
       swg::game::loadScene(sceneToLoadTerrainFilename.c_str(), sceneToLoadAvatarObjectFilename.c_str());
   else
       swg::game::setupScene(GroundScene::ctor(sceneToLoadTerrainFilename.c_str(), sceneToLoadAvatarObjectFilename.c_str()));
   ```
   ⚠️ **`sceneToLoadAvatarObjectFilename` MUST be a loadable avatar `.iff`** (default is
   `"object/creature/player/shared_human_male.iff"` — fine) or the GroundScene ctor `FATAL`s
   (GroundScene.cpp:942). Confirm the editor's Scene panel passes a real terrain `.trn` + that avatar.
5. **Build** `UtinniCore` + `UtinniCore_Tests` (MSBuild, see §5), run `[endpoints]` tests, `git checkout --`
   the Generated churn. Then **maintainer live-smoke**: in-world → Scene panel → pick terrain → **Load** →
   scene loads + is renderable, **no Fatal ~1s later**.
6. **If green → COMMIT the whole batch** (§2) and **strip the temporary diagnostics** (§3).

---

## 2. The UNCOMMITTED batch (all built into the staged DLLs; commit after the §1 smoke is green)

**Committed already (do NOT redo):** Utinni `f23e892` (v4 sync), `a69fb32` (clang-format), `11f7805`
(GAME unlock + Repository hardening + GameCallbacks dispatcher-contain + VEH int3 module/rva diag);
UtinniPlugins `0a793d8` (editor empty-Repository null-guards + ScenePanel/SnapshotPanel SelectedIndex fix).
**UtinniPlugins working tree is CLEAN** — nothing pending there.

**Uncommitted (Utinni working tree — 12 files):** these are the v5 + the Game-unlock follow-on fixes +
the embed clamp + diagnostics, all live in the staged `UtinniCore.dll`(13:46)/`UtinniCoreDotNet.dll`(13:25):
- `engine_hookpoints.{h,inc}` — v5 sync (will become **v6** after §1.1).
- `endpoints_bindings.cpp` — `treeFile::enumerateFiles` binding + slot + asserts (98/96 → 99/97 after §1)
  + the `countResolvableNow()` diagnostic.
- `endpoints.h` / `endpoints.cpp` — `countResolvableNow()` decl + the per-name `MISSING by name` diagnostic.
- `tree_file.cpp` — `enumerateFiles` slot + `enumerateFilesCallback` + Repository population in `getAllFilenames`.
- `world_snapshot.cpp` — `generateHighestId()` guard (skip the offline-reader scan on the advertised client;
  it uses hardcoded-SWGEmu-RVA `worldSnapshotReaderWriter` → crashed once the Repository was populated).
- `game/game.cpp` — `hkMainLoop` advertised-client RT pass-through (the editor-mode resize override is
  D3D9/SWGEmu-only); `g_mainLoopCounter` slot; `countResolvableNow` call in hkInstall; `Game::detour`
  per-target gating + **setupScene-detour SKIP on the advertised client** (detouring the tiny provider
  thunk corrupts the DetourXS trampoline → null jump). **§1 adds `loadScene` here.**
- `ui/imgui_impl.cpp` — the **embed clamp**: `WM_WINDOWPOSCHANGING` clamps the SWG window to the cached
  embed rect (blocks SWG's own fullscreen toggle from covering the editor) + the `utinni_setEmbedClampRect`
  export.
- `UtinniCoreDotNet/Utility/Native.cs` — `SetEmbedClampRect` P/Invoke.
- `UtinniCoreDotNet/UI/Controls/PanelGame.cs` — pushes the embed rect to native before each
  `SetWindowPos`; verbose `RepositionSwgWindow` bounds diagnostic (parent/bounds/formState).

**Commit plan (after green):** one Utinni commit for the v5/v6 contract + `enumerateFiles` + `loadScene`
wiring; one for the Game-unlock follow-on fixes (generateHighestId guard, setupScene-detour skip, hkMainLoop
RT pass-through) + the embed clamp (paired Native.cs/PanelGame.cs). Keep `Generated/UtinniCore.cs` reverted.
The 4 untracked provider-request/consult docs in `.planning/phases/24-.../` should be committed too.

---

## 3. Temporary diagnostics to STRIP once the Load path is green

These were added to chase this session's bugs; remove before/at the final commit (or keep `countResolvableNow`
if useful — it's cheap and one-shot):
- `endpoints.cpp` — the per-name `endpoints: MISSING by name -- %s` loop (keep the summary line).
- `endpoints.{h,cpp}` + `game.cpp` hkInstall — `countResolvableNow()` + its one-shot call (proved the
  static-init race; now redundant since 96/96 resolves at init).
- `imgui_impl.cpp` — the `s_clampLogCount` clamp log is fine to keep (capped at 8).
- `PanelGame.cs` — the verbose `RepositionSwgWindow` bounds diagnostic can drop back to the short form, OR
  keep it (capped at 40) until the embed clamp's fullscreen-block is verified (see §4).

---

## 4. OPEN ITEMS / known issues / follow-ups

- **Embed clamp UNVERIFIED for the fullscreen case.** The clamp (block SWG growing past the embed) is
  staged but the last smoke never triggered SWG's Enter→fullscreen, so the `clamped SWG to embed` path
  hasn't fired live. Confirm on the next smoke (hit Enter in-world; SWG's own fullscreen toggle is a
  window-level restyle via DirectInput, not a WM we see as a keypress). The 250ms watchdog
  (`PanelGame.ReassertEmbed`) is the reactive backstop.
- **setSceneCallbacks don't fire on the advertised client.** The `setupScene` detour is skipped (thunk
  trampoline corruption), so `hkSetScene` never fires → editor scene-change notifications
  (`GroundSceneImpl.OnSetupSceneCallback`, scene-panel refresh) are silent there. Follow-up: hook the
  engine's REAL scene-set path (not the thunk) if editors need the notification.
- **More "editor uses unadvertised SWGEmu-RVA" crashes will surface per-feature.** Now that
  `enumerateFiles` populates the Repository, any editor code that walks it and calls a hardcoded-RVA helper
  not in the catalog will crash on the advertised client (like `generateHighestId`'s `worldSnapshotReaderWriter`
  did). Guard each on `isAdvertisedClient()` as it appears (these fire on user action, not install).
- **Only the GAME subsystem is unlocked.** MISC/INPUT subsystems (chat, cuiManager, cuiIo, object,
  terrain, snapshot editors, input hooks) are still wholesale-skipped in `utinni.cpp createDetours()`
  (`if (!skipMisc && !advertised)` / `if (!skipInput && !advertised)`). Each is its own per-subsystem
  unlock (the original `24-PROVIDER-REQUEST-misc-input-editor-unlock.md` ledger) behind a live smoke.
  `cuiManager::setSize`/`graphics::resize` now resolve (for the consumer-driven reflow escape hatch if
  ever needed — provider chose provider-detected reflow, Option P, which is working).
- **Enter context-routing (Issue #11 lineage).** Enter under injection is tangled (chat vs fullscreen vs
  command); the in-world fullscreen toggle is part of this. Not chased this session.
- **`game::setupScene` stays mapped to `_setScene(Scene*)`** on the provider — a valid low-level
  "set-pre-built-scene-active" primitive; the consumer just won't use it for a full load (uses `loadScene`).

---

## 5. OPERATIONAL FACTS

- **Build (native, no live smoke needed for compile):** MSBuild at
  `/d/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe`. Build via the
  SOLUTION so `$(SolutionDir)` resolves (DetourXS include): `MSBuild Utinni.sln -t:UtinniCore -p:Configuration=Release -p:Platform=x86 -m -nologo -v:minimal -nodeReuse:false`.
  **Use `-` switch syntax** (Git Bash mangles `/p:`). Targets: `UtinniCore`, `UtinniCore_Tests`,
  `UtinniCoreDotNet`, and the TJT plugin via its own sln
  (`"/d/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolbox.sln"`). **Always `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs UtinniCoreDotNet/Generated/Std.cs`** after a UtinniCore build (CppSharp regen churn).
- **clang-format gates master** (clang-format-20 at `…/VC/Tools/Llvm/bin/clang-format.exe`,
  `--style=file --dry-run --Werror`). The shared `engine_hookpoints.h` is EXCLUDED by name in ci.yml —
  never reformat it. Pre-existing drift in `endpoints_tests.cpp` was fixed in `a69fb32`.
- **Live smoke = maintainer only.** Launch via Utinni `Launcher.exe` (injects into `SwgClient_r.exe`);
  `ut.ini swgClientPath=D:\Code\swg-client-v2\stage\`; read `bin/Release/utinni.log` after. Advertised-client
  detour/window/render changes **need a live smoke before commit** (ABI/ASLR/embed/render can't be caught
  headless). `ut.ini [DebugBisect] skipMiscGroup=true` disables the GAME unlock (RENDER-only) for bisection.
- **Symbolizing crashes:** `llvm-symbolizer.exe --obj=/d/Code/swg-client-v2/stage/SwgClient_r.exe <VA>`
  where `VA = 0x400000 + rva` (exe ImageBase 0x400000; the log's rva = EIP - runtime-base). The VEH logs
  module+rva for FATAL (AV-class) and now int3 too. The int3 at rva `0xC7FCEE` is the engine's
  `InternalFatal` (the fatal HANDLER) — look at the FATAL line / `MyUnhandledExceptionFilter` for the real
  cause; **C++ throws (0xE06D7363) are NOT logged by the VEH** (only AV-class in swg/utinni modules).
- **Staged binaries (current):** `bin/Release/UtinniCore.dll` 13:46, `UtinniCoreDotNet.dll` 13:25,
  `Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` 21:04; provider `stage/SwgClient_r.exe` **14:59 = v6/99**.
  After §1, rebuild UtinniCore + UtinniCoreDotNet → they go stale; the TJT plugin is unchanged.
- **Cross-repo:** the contract `.h/.inc` are SHARED-VERBATIM with swg-client-v2 (`$SRC` above). The PROVIDER
  owns swg-client-v2 edits; hand it a prompt per request. Provider repo NOT committed by us. The provider's
  exe is rebuilt + re-staged by the maintainer.

## 6. Provider handback chain (this session, newest last)

In `swg-client-v2/.planning/handoff/` (and mirrored as Utinni `24-PROVIDER-REQUEST/CONSULT-*.md`):
1. `…-misc-input-editor-unlock` (§4 batch: game::mainLoop re-point, g_mainLoopCounter, treeFile::searchTree,
   cuiChatWindow::createNewWindow) → contract v4.
2. `…-treefile-enum-and-inworld-reflow` → `treeFile::enumerateFiles` (v5) + in-world CUI reflow (provider-
   detected, Option P, working).
3. `…-table-static-init-race` → lazy-fill `GetEngineHookPoints()` (Option A, plain-bool guard) — fixed
   40/96 → **96/96** (the table's function-call rows were null at our pre-resume read).
4. `…-setupscene-remap` → `game::setupScene → _setScene(Scene*)` (fixed the garbage-args crash; insufficient
   for a full load).
5. `…-scene-load-lifecycle` → **`game::loadScene` (v6)** — THE current task (§1).

## 7. THE BIG PICTURE (why this matters)

This is the advertised-client editor-unlock follow-on milestone: making the real DX11 SWG client
(`SwgClient_r.exe`, which advertises its hookpoints) drive Utinni's editors the way SWGEmu does. GAME +
scene-list are the foundation; once `loadScene` lands, a modder can pick a terrain and load it live in the
real client. The remaining editors (terrain/effects/object/snapshot/chat) each get a per-subsystem unlock
on the same provider↔consumer contract pattern proven here.
