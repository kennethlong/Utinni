# Provider Consult — editor scene-load needs the full SceneCreator lifecycle, not _setScene(Scene*)

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Date:** 2026-06-25
**Status:** CONSULT (a question + a proposed direction). Follows the `setupScene` re-map handback — that
fixed the signature crash, but exposed a deeper lifecycle gap. Your call on the cleanest entry.
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/`.

---

## What happened (the re-map worked, but it's not enough)

Your `game::setupScene → Game::_setScene(Scene*)` re-map fixed the garbage-args crash — `setupScene` now
**returns cleanly** with the editor's pre-built `GroundScene`. But **~1s later the engine throws an
unhandled exception** processing the scene:

```
hkMainLoop: swg::game::setupScene returned          ← _setScene(Scene*) set ms_scene, returned OK
VEH int3 ... rva 0xC7FCEE (InternalFatal)           ← via Fatal <- "ExceptionHandler invoked"
VEH int3 stack: Fatal <- ...16BDE64 <- MyUnhandledExceptionFilter
```
No `VEH FATAL` (so it's a C++ throw, not an AV) → caught by `MyUnhandledExceptionFilter` → `Fatal` → int3.

## Our read (please confirm)

`Game::_setScene(Scene*)` [Game.cpp:1386] just does `ms_scene = newScene` — it **skips the
`SceneCreator`/`_startScene` lifecycle**. The modern scene-load path (from the original crash stack) is:
```
Game::setScene(bool, terrain, player, customized) -> _setScene(SceneCreator&, bool) -> _startScene
   -> SinglePlayerSceneCreator::create -> new GroundScene(...)  -> full integration
```
So handing the engine a **pre-built** `GroundScene` and only `ms_scene = it` leaves the scene
half-integrated (loading state, render registration, async-loader handoff, whatever `_startScene` does) →
the next frame faults. The SWGEmu "build a GroundScene, then `setupScene(it)`" pattern does the full setup
on SWGEmu, but on your client that work lives in the `SceneCreator` path, not in `_setScene`.

**So the editor must drive the FULL string-based load** (let the engine build via `SceneCreator`), not
pass a pre-built `GroundScene`.

## The question / proposed direction

We'll change the consumer so that on the advertised client the editor's "Load scene" passes the **terrain
+ player filenames** (it has both) instead of building a `GroundScene`. We need the right engine entry:

**Option A (preferred — a thunk, encapsulates the `immediately` decision on your side):** advertise a new
row, e.g.
```cpp
// game::loadScene  -- full editor scene-load via the SceneCreator path
static void __cdecl utinni_gameLoadScene(const char* terrainFilename, const char* playerFilename)
{
    Game::setScene(/*immediately=*/???, terrainFilename, playerFilename, nullptr);   // YOU pick immediately
}
```
We bind `game::loadScene → &utinni_gameLoadScene` and call it `(terrain, avatar)` on the advertised
client. (NAME ADD → version bump + `.h/.inc` re-sync, like `enumerateFiles`.) `game::setupScene` can stay
mapped to `_setScene` (we just won't call it on the advertised client) or revert — your call.

**Option B:** we call `Game::setScene(bool immediately, const char*, const char*, CreatureObject*)`
directly (you re-advertise that strings-overload under a clear name and tell us the right `immediately`
value for an editor-initiated load). Then no thunk — but we own the `immediately` choice, which you know
better.

**Three things to confirm:**
1. Is our lifecycle read correct — `_setScene(Scene*)` is insufficient and the `SceneCreator` path is
   required for a usable scene?
2. Which entry do you want us on — the `loadScene(terrain, player)` thunk (A) or direct `setScene`
   strings-call (B)?
3. What `immediately` value for an editor "Load this scene now"? (And is `customizedPlayer = nullptr`
   correct — the editor has no custom player object, just the avatar filename?)

If our read is wrong and a pre-built `GroundScene` *can* be fully integrated through some entry you'd
rather expose, that works too — we just need a scene-load that leaves the engine in a renderable state.

## Pointers
- Crash: `hkMainLoop` (Utinni `swg/game/game.cpp`) `setupScene(GroundScene::ctor(terrain, avatar))` →
  returns → engine throws next frame.
- Engine: `Game::_setScene(Scene*)` Game.cpp:1386 (just sets `ms_scene`); `Game::setScene(strings)`
  Game.h:108; `_setScene(SceneCreator&, bool)` / `_startScene` (the full path).
- Prior: `2026-06-25-utinni-setupscene-remap.md` + its handback (the `_setScene` re-map).
