# Internals: hooks, RVAs, patches

> Audience: core contributors and reversers. Plugin authors don't need this
> page — depend on the curated `utinni::*` / `UtinniCore.*` APIs, not the
> raw RVAs.

This is the reverse-engineering record for the SWG client build Utinni was
originally written against. **All addresses below are pinned to that specific
binary** — a different `SwgClient_r.exe` (different SWGEmu drop, different
SWG-Source build) will have different RVAs and will require an updated set.

If you're porting Utinni to a new client, your job is to find the
corresponding addresses in your binary (typically via signature scanning or
symbol matching) and update the per-subsystem `detour()` and `patch()`
functions.

## How detours are installed

`UtinniCore/swg/<subsystem>/<subsystem>.cpp`'s `detour()` typically does:

```cpp
swg::<subsystem>::<fn> = (swg::<subsystem>::<Fn_t>)Detour::Create(
    (LPVOID)0x...RVA...,             // address in SwgClient_r.exe
    Hook_<subsystem>_<fn>,            // our replacement
    DETOUR_TYPE_PUSH_RET);            // detour style
```

`DETOUR_TYPE_PUSH_RET` is DetourXS's standard inline hook — the original
prologue bytes are copied to a trampoline, the original site is patched with
a `push imm32 / ret` jump to our hook, and our hook is responsible for
chaining via the trampoline when it wants to call the original.

`patch()` functions, where they exist, do non-detour memory writes — typically
either a 5-byte JMP (`utinni::memory::createJMP`) or a direct byte sequence
write (`utinni::memory::write<T>`).

## Hook table

The list below is the union of every `detour()` / `patch()` invocation found
in the surveyed source. Subsystem grouping matches `swg/*` layout.

### `swg/client/client.cpp` — `utinni::Client::detour()`

| RVA                    | Original function                              | Hook style          | Reason                                                            |
| ---------------------- | ---------------------------------------------- | ------------------- | ----------------------------------------------------------------- |
| `0x00A9F970`           | `Client::setupStartDataInstall`                | PUSH_RET            | Editor-mode HWND/HINSTANCE handoff before window creation.        |
| `0x00401050`           | `clientMain`                                   | PUSH_RET            | Crash-log redirection, top-level lifecycle.                       |
| `0x00AA0970`           | SWG main `WndProc`                             | PUSH_RET            | Used as the forwarded WndProc by `PanelGame.WndProc`; also patched. |
| `0x00A9F640`           | `Client::writeCrashLog`                        | PUSH_RET            | Redirect crash logs to `logs/`.                                   |
| `0x00A8A170`           | `Client::writeMiniDump`                        | PUSH_RET            | Redirect minidumps to `logs/`.                                    |
| `0xA9F766`             | (mid-`writeCrashLog`)                          | JMP patch (`patch()`) | Splice a custom log line into the crash log.                    |

### `swg/scene/ground_scene.cpp` — `utinni::GroundScene::detour()`

| RVA                    | Original function                              | Hook style          | Reason                                                            |
| ---------------------- | ---------------------------------------------- | ------------------- | ----------------------------------------------------------------- |
| `0x00519830`           | `GroundScene::ctor` (offline-mode variant)     | PUSH_RET            | Allows manual GroundScene construction.                            |
| `0x0051A4F0`           | `GroundScene::reloadTerrain`                   | PUSH_RET            | Exposed as `utinni::GroundScene::reloadTerrain`.                   |
| `0x0051A350`           | `GroundScene::changeCamera`                    | PUSH_RET            | Fires camera-change callbacks.                                     |
| `0x0051A4D0`           | `GroundScene::getCurrentCamera`                | PUSH_RET            | Exposed.                                                           |
| `0x0051B770`           | `GroundScene::draw`                            | PUSH_RET            | Pre/post-draw callbacks.                                            |
| `0x0051AF10`           | `GroundScene::update`                          | PUSH_RET            | Update-loop callbacks.                                              |
| `0x0051AB20`           | `GroundScene::handleInputMapUpdate`            | PUSH_RET            | Input map editor.                                                   |
| `0x0051AA40`           | `GroundScene::handleInputMapEvent`             | PUSH_RET            | Input event editor.                                                 |
| `0x00518EB0`           | `GroundScene::init`                            | PUSH_RET            | Init.                                                               |
| `0x190885C` *(data)*   | `GroundScene*` singleton pointer               | direct read         | `utinni::GroundScene::get()` dereferences this address.            |

### `swg/scene/render_world.cpp` — `utinni::renderWorld::detour()`

| RVA                    | Original function                              | Hook style          | Reason                                                            |
| ---------------------- | ---------------------------------------------- | ------------------- | ----------------------------------------------------------------- |
| `0x765C20`             | `RenderWorld::clearVisibleCells`               | PUSH_RET (disabled) | Commented out in current source.                                  |
| `0x007664F0`           | `RenderWorld::addObjectNotifications`          | PUSH_RET            | Exposed; required to register dynamically-spawned objects.        |
| `0x00766DE0`           | `RenderWorld::render`                          | PUSH_RET (disabled) | Commented out in current source.                                  |

### `swg/scene/client_world.cpp` — `utinni::clientWorld::detour()`

| RVA                    | Original function                              | Hook style          | Reason                                                            |
| ---------------------- | ---------------------------------------------- | ------------------- | ----------------------------------------------------------------- |
| `0x00561350`           | `ClientWorld::collide`                         | PUSH_RET (disabled) | Available but disabled.                                            |
| `0x00562940`           | `ClientWorld::internalCollide`                 | PUSH_RET (disabled) | Available but disabled.                                            |
| `0x00562680`           | `ClientWorld::internalCollideFindAllObjects`   | PUSH_RET (disabled) | Available but disabled.                                            |
| `0x199CB34` *(data)*   | unknown collision context pointer              | direct read         | Used internally during collide calls.                              |

### `swg/object/creature_object.cpp` — `utinni::creatureObject::detour()`

| RVA                    | Original function                              | Hook style          | Reason                                                            |
| ---------------------- | ---------------------------------------------- | ------------------- | ----------------------------------------------------------------- |
| `0x00434AB0`           | `CreatureObject::setTarget`                    | PUSH_RET            | Fires `addOnTargetCallback` (the `ObjectCallbacks.AddOnTarget*` flow). |

### `swg/object/object.cpp` (direct calls, no detour)

| RVA                    | Original function                              |
| ---------------------- | ---------------------------------------------- |
| `0x00B28700`           | `ObjectTemplateList::getByFilename`            |
| `0x00B28720`           | `ObjectTemplateList::getByIff`                 |
| `0x00B28740`           | `ObjectTemplateList::getByCrc`                 |
| `0x00B289B0`           | `Object::reloadObject`                         |
| `0x00B28A10` / `0x00B28AA0` | `CrcString` getters                       |
| `0x00B2E760`           | `Object::createObject(template)`               |
| `0x011A6C10` / `0x011A6D30` / `0x011A6E50` | `SharedObjectTemplate` getters |
| `0x011A8B60`           | `SharedObjectTemplate::getGameObjectType`      |

### `swg/ui/cui_manager.cpp` — `utinni::CuiManager::detour()`

| RVA                    | Original function                              | Hook style          | Reason                                                            |
| ---------------------- | ---------------------------------------------- | ------------------- | ----------------------------------------------------------------- |
| `0x00881210`           | `CuiManager::render`                           | PUSH_RET            | Hook for render bookkeeping.                                       |
| `0x00BD3E20`           | `findObjectUnderCursor`                        | PUSH_RET            | Exposed via `hasObjectUnderCursor()`.                              |
| `0x00882410`           | `CuiManager::setSize`                          | PUSH_RET            | Exposed.                                                           |
| `0x00881940`           | `CuiManager::togglePointer`                    | PUSH_RET            | Exposed.                                                           |
| `0x00881560`           | `CuiManager::restartMusic`                     | PUSH_RET            | Exposed.                                                           |
| `0x010E8410`           | `UiManager::drawCursor`                        | PUSH_RET            | Cursor draw override (editor-mode).                                |
| `0x008ABEB0`           | `SystemMessageManager::receiveMessage`         | PUSH_RET            | Fires `addReceiveMessageCallback`.                                 |
| `0x008AC250`           | `SystemMessageManager::sendMessage`            | PUSH_RET            | Exposed.                                                           |

### `swg/ui/cui_chat_window.cpp` — `utinni::CuiChatWindow::detour()`

| RVA                    | Original function                              | Hook style          | Reason                                                            |
| ---------------------- | ---------------------------------------------- | ------------------- | ----------------------------------------------------------------- |
| `0x00F364B0`           | `CuiChatWindow::ctor`                          | PUSH_RET            | Wires `addCreateCommandParserCallback` (where plugins register custom slash commands). |
| `0x00F38500`           | `enableTextInput`                              | PUSH_RET            | Exposed.                                                           |
| `0x00F3BFD0`           | `writeToAllTabs`                               | PUSH_RET            | Exposed.                                                           |
| `0x00F3C1F0`           | `writeToCurrentTab`                            | PUSH_RET            | Exposed.                                                           |
| `0x009141D0`           | `CuiConsoleHelper::sendInput`                  | PUSH_RET            | Console-input hook.                                                |

### `swg/ui/cui_hud.cpp` / `cui_radial_menu.cpp` / `cui_menu.cpp` / `cui_io.cpp` / `cui_misc.cpp`

| RVA                    | Original function                              | Hook style          | Subsystem                                                          |
| ---------------------- | ---------------------------------------------- | ------------------- | ------------------------------------------------------------------ |
| `0x00BD56F0`           | `CuiHud::update`                               | PUSH_RET            | `cuiHud`                                                           |
| `0x00EDBAA0`           | `actionPerformAction`                          | PUSH_RET            | `cuiHud`                                                           |
| `0x00BD3E20`           | `CuiHud::getTarget`                            | PUSH_RET            | `cuiHud`                                                           |
| `0x009698C0`           | `CuiRadialMenuManager::update`                 | PUSH_RET            | `cuiRadialMenuManager`                                             |
| `0x0096C550`           | `CuiRadialMenuManager::clear`                  | PUSH_RET            | `cuiRadialMenuManager`                                             |
| (various)              | `cuiMisc::patch()` writes                      | direct writes       | `cuiMisc`                                                          |

### `swg/graphics/graphics.cpp` — `utinni::Graphics::detour()`

| RVA                    | Original function                              | Hook style |
| ---------------------- | ---------------------------------------------- | ---------- |
| `0x007548A0`           | `Graphics::install`                            | PUSH_RET   |
| `0x00755700`           | `Graphics::update`                             | PUSH_RET   |
| `0x00755730`           | `Graphics::beginScene`                         | PUSH_RET   |
| `0x00755740`           | `Graphics::endScene`                           | PUSH_RET   |
| `0x00755810`           | `Graphics::presentWindow`                      | PUSH_RET   |
| `0x00755800`           | `Graphics::present`                            | PUSH_RET   |
| `0x00755940`           | `useHardwareCursor`                            | PUSH_RET   |
| `0x00755A50`           | `showMouseCursor`                              | PUSH_RET   |
| `0x00755AC0`           | `setSystemMouseCursorPosition`                 | PUSH_RET   |
| `0x00754E40`           | `Graphics::resize`                             | PUSH_RET   |
| `0x00755520`           | `Graphics::flushResources`                     | PUSH_RET   |
| `0x00764B70`           | `textureListReloadTextures`                    | PUSH_RET   |
| `0x00755910`           | `setStaticShader`                              | PUSH_RET   |
| `0x00755D30`           | `setObjectToWorldTransformAndScale`            | PUSH_RET   |
| `0x00759A70`           | `drawExtent`                                   | PUSH_RET   |
| `0x00755890`           | `Graphics::screenshot`                         | PUSH_RET   |

### `swg/graphics/directx9.cpp`

DirectX9 device methods are hooked via **vtable index**, not RVA, because
the device interface is dynamic:

| vtable index           | Method                                         | Used for                                                  |
| ---------------------- | ---------------------------------------------- | --------------------------------------------------------- |
| 8                      | `Reset`                                        | Reinit ImGui textures on device reset.                    |
| 16                     | `Present`                                      | ImGui + ImGuizmo render pass.                             |
| 20                     | `BeginScene`                                   | Pre/post-begin-scene callbacks.                           |
| 21                     | `EndScene`                                     | Pre/post-end-scene callbacks.                             |

Plus a hook in `s207_r.dll` for the shader compiler at `0x62A4F9DB` (used to
intercept compile errors for the depth-texture path).

### `swg/graphics/shader.cpp` / `post_processing.cpp`

| RVA                    | Hook / patch                                   |
| ---------------------- | ---------------------------------------------- |
| `0x00773E39`           | JMP patch on `midPopCell` (5 bytes) — fires `shader::drawPhaseCallback`. |
| `0x772D60`             | Sibling call target referenced from `midPopCell`. |
| `0x0064B500`           | `Bloom::preSceneRender` — PUSH_RET → `postProcessing::preSceneRenderCallback`. |
| `0x0064B560`           | `Bloom::postSceneRender` — PUSH_RET → `postProcessing::postSceneRenderCallback`. |

### `swg/misc/config.cpp` — `swg::config::detour()`

| RVA                    | Original function                              | Hook style |
| ---------------------- | ---------------------------------------------- | ---------- |
| `0x00A9C6C0`           | `loadConfigFileBuffer`                         | PUSH_RET   |
| `0x00A9C780`           | `loadConfigFileString`                         | PUSH_RET   |
| `0x00401000`           | `loadOverrideConfig`                           | PUSH_RET   |
| `0x00910A70`           | `setModalChat`                                 | PUSH_RET   |
| `0x00910D40`           | `getModalChat`                                 | PUSH_RET   |

The `loadOverrideConfig` hook is what implements `useSwgOverrideCfg=true` —
the original SWG config path is replaced with our `utinni.cfg`.

### `swg/misc/direct_input.cpp`

| RVA                    | Original function                              | Hook style |
| ---------------------- | ---------------------------------------------- | ---------- |
| `0x00420880`           | `DirectInput::suspend`                         | PUSH_RET   |
| `0x00420890`           | `DirectInput::resume`                          | PUSH_RET   |
| `0x00421490`           | `DirectInput::setupInstall`                    | PUSH_RET   |

### `swg/misc/tree_file.cpp`

| RVA                    | Original function                              | Hook style |
| ---------------------- | ---------------------------------------------- | ---------- |
| `0xA992E0`             | `TreeFile::searchTree`                         | PUSH_RET — records every requested filename into a set queried by `getAllFilenames()`. |

### `swg/camera/debug_camera.cpp`

| RVA                    | Original function                              | Hook style |
| ---------------------- | ---------------------------------------------- | ---------- |
| `0x006DA1B0`           | `GameCamera::alter` (debug variant)            | PUSH_RET (plus `patch()`) |

### `swg/game/game.cpp`

| RVA                    | Original function                              | Hook style          | Use                                                   |
| ---------------------- | ---------------------------------------------- | ------------------- | ----------------------------------------------------- |
| `0x00422E80`           | `Game::install`                                | PUSH_RET            | Fires install callbacks.                              |
| `0x00423720`           | `Game::quit`                                   | PUSH_RET            | Exposed.                                              |
| `0x004237C0`           | `Game::mainLoop`                               | PUSH_RET            | Pre/post-main-loop callbacks; drains queues.          |
| `0x00424220`           | `Game::setupScene`                             | PUSH_RET            | Fires setup-scene callbacks.                          |
| `0x00423700`           | `Game::cleanupScene`                           | PUSH_RET            | Fires cleanup-scene callbacks.                        |
| `0x00425140`           | `Game::getPlayer`                              | PUSH_RET            | Exposed.                                              |
| `0x004251D0`           | `Game::getPlayerCreatureObject`                | PUSH_RET            | Exposed.                                              |
| `0x00425BB0`           | `Game::getCamera`                              | PUSH_RET            | Exposed.                                              |
| `0x00425BE0`           | `Game::getConstCamera`                         | PUSH_RET            | Exposed.                                              |
| `0x1908830` *(data)*   | `Game::mainLoopCount` (int)                    | direct read         | Used by callback bookkeeping.                          |
| `0x01908858` *(data)*  | `Game::isSafeToUse` flag #1                    | direct read         | `Game::isSafeToUse()` reads this OR...                 |
| `0x01919410` *(data)*  | `Game::isSafeToUse` flag #2                    | direct read         | ...this. EITHER being set means safe. (2026-07-19 live: in a fully-loaded in-world session one flag stays unset; the earlier "both must be true" model made isSafeToUse false and silently blocked all world-snapshot mutations — the remove regression. OR is the field-proven semantics.) |

### `swg/appearance/skeleton.cpp`

| RVA                    | Original function                              | Hook style          | Use                                                   |
| ---------------------- | ---------------------------------------------- | ------------------- | ----------------------------------------------------- |
| `0x007C8B60`           | `SkeletalAppearance::render`                   | PUSH_RET            | Skeleton-rendering toggle.                            |
| `0x007CA130`           | `SkeletalAppearance::getDisplayLodSkeleton`    | PUSH_RET            | LOD selection for skeleton draw.                      |
| `0x007E6C50`           | `Skeleton::addShaderPrimitives`                | PUSH_RET            | Inject our bone-overlay draw call.                    |

### Static singletons / data addresses

These are read directly (no detour) by various subsystems:

| RVA                    | Symbol                                                 | Notes                                                          |
| ---------------------- | ------------------------------------------------------ | -------------------------------------------------------------- |
| `0x190885C`            | `GroundScene*` singleton                                | Read by `GroundScene::get()`.                                  |
| `0x1908830`            | `Game::mainLoopCount` (int)                             | Used to debounce callback firing.                              |
| `0x01908858`           | `Game::isSafeToUse` flag (bool)                         | Composite of two flags.                                        |
| `0x01919410`           | `Game::isSafeToUse` flag (bool)                         | Composite of two flags.                                        |
| `0x0193C5E0`           | HCURSOR cell (editor-mode DirectInput)                  | Cursor handle override.                                        |
| `0x0193C268`           | (unknown — used in crash-log hook)                      | Some kind of state ptr.                                        |
| `0x199CB34`            | (unknown — collision context)                           | Used during `ClientWorld::collide`.                            |

## Memory utilities

`utinni::memory::createJMP(source, target, byteCount)` does the patch above
that `midPopCell` uses — writes a `E9 <rel32>` jump and pads to `byteCount`
with NOPs. The source must be at least 5 bytes from any branch target;
otherwise you'll corrupt control flow.

`utinni::memory::read<T>` / `write<T>` are typed `*((T*)addr)` reads/writes —
useful for grabbing or poking the singleton pointers above.

`utinni::memory::findPattern(...)` is a signature scanner — accepts a pattern
+ mask and returns the first match in a memory range or module. Used in
practice when an RVA is too volatile across builds; not heavily used in
current code because RVAs are stable for the targeted binary.

## Vtable hooking (DirectX9)

`swg/graphics/directx9.cpp` uses the **first-call-back** pattern:

1. Detour `Graphics::install` (RVA `0x007548A0`) — fires after the device is
   created.
2. In the hook, read the device pointer from a known offset, dereference
   its vtable, and patch entries 8/16/20/21 with our trampolines.
3. From then on, every `BeginScene` / `EndScene` / `Present` / `Reset`
   passes through us.

This is fragile if SWG ever recreates the device (resize, mode change). The
current code re-installs the hooks on `Reset` to handle this.

## Porting checklist for a new client build

If you want Utinni against a different SWG binary:

1. Open the binary in IDA Pro / Ghidra. Find:
   - `Game::install`, `mainLoop`, `setupScene`, `cleanupScene` — typically a
     cluster of public static functions on the `Game` namespace.
   - `GroundScene::ctor`, `draw`, `update`, `reloadTerrain`,
     `changeCamera`, `getCurrentCamera`.
   - `Client::setupStartDataInstall`, `clientMain`, `writeCrashLog`,
     `writeMiniDump`.
   - `Graphics::install` / `beginScene` / `endScene` / `present` /
     `presentWindow` / `update` / `resize`.
   - `TreeFile::searchTree`.
   - `CuiManager::render`, `CuiChatWindow::ctor`, etc.
   - `CreatureObject::setTarget`.
2. Replace the RVAs in each `swg/*/<file>.cpp::detour()`.
3. Verify the WndProc address used by `PanelGame.WndProc`
   (`UtinniCoreDotNet/UI/Controls/PanelGame.cs`) — that's a separate
   constant duplicating `Client::WndProc` RVA.
4. Run; iterate on any crashes by attaching a debugger and resolving the
   bad call site.

The `UtinniCore-Symbols` project exists specifically so CppSharp can resolve
mangled native symbols — if you change ABI (calling conv, mangled names),
you'll need to update that side too. See [Regenerating bindings](regen-bindings.md).

## See also

- [Native core](core.md) — public API on top of these RVAs.
- [Injection & boot](injection.md) — how the EP-loop trick works.
- [Regenerating bindings](regen-bindings.md) — when adding a new hook means
  re-running CppSharp.
- [swg-client docs](../../../swg-client/docs/index.html) — to find the
  source-level function being hooked.
