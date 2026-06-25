# Provider Request — re-map game::setupScene to a pre-built-scene entry (editor scene-load crash)

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Date:** 2026-06-25
**Status:** REQUEST — fixes a HARD CRASH on the editor's "Load scene" (advertised client). One advertised-row
re-map, no `.inc`/name change (a NAME would only change if you prefer a distinct name — see §3).
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/`.

---

## 0. TL;DR

`game::setupScene` is advertised as the **strings-overload** `Game::setScene(bool, const char* terrain,
const char* player, CreatureObject* customized)` — which *builds* a scene from filenames via
`SinglePlayerSceneCreator`. But Utinni's editor uses the **build-then-set** pattern: it constructs the scene
itself with the already-advertised `groundScene::ctor`, then hands the pointer to `setupScene`. So Utinni
calls `setupScene(GroundScene*)` and the advertised `Game::setScene(bool,...)` reads that pointer as
`immediately`, then garbage for the filename args → builds a scene with a garbage `player` → crash in
`GroundScene::GroundScene` at `player->setPosition_p(...)` (`0xC0000005 WRITE`, confirmed via PDB symbolize).

**Ask:** re-map `game::setupScene` to a thunk over **`Game::_setScene(Scene*)`** (the pre-built-scene setter,
Game.cpp:1386) — the modern equivalent of SWGEmu's `setupScene(GroundScene*)`. Then Utinni's existing call
**and** its `hkSetScene` detour work unchanged on both targets, exactly like `groundScene::ctor` already does.

## 1. Why this entry (not the strings overload)

- The normal multiplayer login→world uses the **8-arg** `setScene(bool, terrain, NetworkId, template, …)`
  (not advertised, not detoured) — that's why initial world-load works.
- The editor's "Load scene" is the **single-player offline** path. Utinni builds the scene with
  `groundScene::ctor` (already advertised, MI `__fastcall` thunk) and sets it — the SWGEmu
  `setupScene(GroundScene*)` shape. The right modern target for "set this pre-built scene active" is
  `Game::_setScene(Scene*)` (Game.cpp:1386: `ms_scene = newScene; …`), NOT the strings-builder.
- This keeps Utinni's scene-load logic identical across SWGEmu and the advertised client (the contract
  abstracts the difference), and avoids a consumer-side per-target scene-load fork + the `immediately`
  semantics guesswork.

## 2. The thunk (private `_setScene` → in-TU forwarder, like the groundScene private methods)

```cpp
// engine_advertise.cpp (or a small in-TU forwarder where _setScene is accessible -- it's private,
// so a friend/in-TU forwarder is needed exactly like utinni_groundScene*).
static void __cdecl utinni_gameSetupScene(void * groundScene)
{
    // Utinni passes the GroundScene* from groundScene::ctor. GroundScene is MULTIPLE-INHERITANCE
    // (NetworkScene : Scene, MessageDispatch::Receiver), so the Scene base is at a non-zero offset --
    // the static_cast applies the correct this-adjustment; a raw reinterpret_cast would NOT.
    Game::_setScene(static_cast<Scene *>(reinterpret_cast<GroundScene *>(groundScene)));
}
```
Row: `{ "game::setupScene", (void *)&utinni_gameSetupScene }` (replacing the
`static_cast<...>(&Game::setScene)` row). `__cdecl(void*)` matches Utinni's
`swg::game::setupScene` typedef `void(__cdecl*)(GroundScene*)` exactly (single pointer arg).

**Consumer side: ZERO change** — `hkMainLoop`'s `setupScene(GroundScene::ctor(...))` and the `hkSetScene`
detour both already use the `(GroundScene*)` shape. (Utinni keeps the `game::g_mainLoopCounter` / per-frame
tick etc. as-is.)

## 3. Contract / version

- If you keep the **same name** `game::setupScene` (recommended — Utinni binds it already), this is a pure
  **address re-map**: no `.inc`/`.h` change, no version bump, no re-sync. Just rebuild + re-stage.
- Only if you'd rather expose it under a distinct name (e.g. `game::setActiveScene`) does it become a NAME
  ADD (bump + re-sync). Not necessary — same-name re-map is cleanest.

## 4. Acceptance
Maintainer live smoke: in-world, pick a terrain in the TJT Scene panel → **Load** → the scene loads (or
honestly degrades) with **no `0xC0000005` in `GroundScene::GroundScene`**. (`cleanupScene` already works;
this is the `setupScene` leg.)

## 5. Pointers
- Crash: `GroundScene::GroundScene(char const*,char const*,CreatureObject*)` GroundScene.cpp:948
  (`player->setPosition_p` through a garbage `player`), via `SinglePlayerSceneCreator::create` ←
  `Game::_startScene` ← `Game::_setScene(SceneCreator&)` ← Utinni `hkMainLoop`.
- Target: `Game::_setScene(Scene*)` Game.cpp:1386; overloads at Game.h:108 (strings, currently advertised)
  / Game.h:126 (`_setScene(Scene*)`).
- Consumer: Utinni `swg/game/game.cpp` `hkMainLoop` (loadNewScene → `setupScene(GroundScene::ctor(...))`),
  `hkSetScene` detour; slot `swg::game::setupScene` typedef `void(__cdecl*)(GroundScene*)`.
