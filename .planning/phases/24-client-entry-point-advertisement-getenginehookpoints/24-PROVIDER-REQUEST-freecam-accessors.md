# Provider Request — Free-cam editor unlock via ACCESSORS (no Utinni-side offsets)

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Date:** 2026-06-29
**Status:** REQUEST — the §8 "biggest single editor" (free-cam) on the advertised DX11/NGE client.
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/`
as `2026-06-29-utinni-freecam-accessors.md`.
**Self-contained:** act on this doc without reading the Utinni Phase-24 plans. It cites the exact
Utinni read sites (file:line) and the exact struct offsets each requested accessor replaces.

---

## 0. TL;DR

Free-cam is the last big editor to light up on `SwgClient_r.exe`. Toggling it and fetching the camera
already work — `groundScene::changeCamera` and `groundScene::getCurrentCamera` are advertised + bound.
What's left is that the free-cam input/movement code reads a handful of **raw struct field byte-offsets**
(camera `+0x248`, `GroundScene::debugPortalCameraInputMap + 0xC`, `GroundScene::currentView`).

**Architectural decision (consumer side):** we will NOT hardcode those NGE byte-offsets, even though a
diagnostic probe (Utinni `618e517`) showed they currently read back sane on NGE. A hardcoded offset is a
hidden coupling to your struct layout that can silently drift in a future from-source build. We want the
advertised interface to be the **complete contract** — zero Utinni-side offsets into your struct
internals. So this request asks you to **advertise a small accessor function for each fragile read** (the
same pattern as Bucket B's `particlePreview::retrigger` friend-free-function and the `config::setModalChat`
external-linkage shims). Each accessor encapsulates the offset inside your compiled code, where it belongs.

**The batch is small: ~5 new rows** (§3). The movement-entry detour target (`alter`) is **virtual → we
vtable-resolve it ourselves** (slot 4), so it needs **no** provider row — please just confirm the index.

---

## 1. How the unlock works (the contract both sides share)

On the advertised client, `resolveFromExe()` overwrites each `swg::*` slot **by name** from your
`GetEngineHookPoints()` table; then each subsystem's `detour()`/call path runs, gated per-target by
`installable()`. SWGEmu Pre-CU is byte-for-byte unchanged (D-00) — nothing here touches the SWGEmu path.
A read we route through an advertised accessor is safe on BOTH targets; a raw struct-offset read is the
thing that silently misreads (or AVs) when your NGE layout differs. This request converts every remaining
free-cam offset read into an advertised accessor call.

## 2. Complete ledger — every free-cam read/call and its disposition

Verified against the live Utinni sites (`swg/camera/debug_camera.cpp`, `swg/scene/ground_scene.cpp`).

| # | Utinni read/call (site) | Today | Disposition |
|---|---|---|---|
| 1 | `debugCamera::alter` — movement entry we DETOUR (`debug_camera.cpp:179`) | virtual `GameCamera::alter`, SWGEmu RVA `0x006DA1B0` | **consumer vtable-resolves (Object slot 4)** — no row; confirm index |
| 2 | `groundScene::changeCamera` — toggle (`ground_scene.cpp:518/522`) | RVA `0x0051A350` | ✅ **advertised + bound** |
| 3 | `groundScene::getCurrentCamera` (`ground_scene.cpp:511`) | RVA `0x0051A4D0` | ✅ **advertised + bound** |
| 4 | `object::getTransform_o2w` — camera transform for the move (`debug_camera.cpp:289/293`) | — | ✅ **advertised** (replaces the `objectToParent` field read; see §4) |
| 5 | `object::move_p` — camera move (`debug_camera.cpp:289`) | — | ✅ **advertised** (confirm the consumer `Object::move` binds to it) |
| 6 | `groundScene::handleInputMapEvent` — input detour entry (`ground_scene.cpp:463`) | — | ✅ **advertised** (consumer currently skips on advertised; we'll un-skip) |
| 7 | `messageQueue::appendMessage` — input enqueue (`debug_camera.cpp:101…`) | — | ✅ **advertised** |
| **8** | **`GroundScene::currentView`** — `isFreeCameraActive()` (`ground_scene.cpp:540`) | raw field read | **❌ NEW accessor — §3.A** |
| **9** | **`GroundScene::debugPortalCameraInputMap + 0xC`** — input MessageQueue (`debug_camera.cpp:90`) | raw field + offset | **❌ NEW accessor — §3.B** |
| **10** | **GameCamera `+ 0x248`** — movement MessageQueue (`debug_camera.cpp:204`) | raw field offset | **❌ NEW accessor — §3.C** |
| **11** | **`MessageQueue::getCount`** — drain loop bound (`debug_camera.cpp:210`) | not advertised | **❌ NEW accessor — §3.D** |
| **12** | **`MessageQueue::getMessage(i, &msg, &val)`** — drain (`debug_camera.cpp:214`) | not advertised | **❌ NEW accessor — §3.D** |
| **13** | **`Object::isActive`** — `hkAlter` guard (`debug_camera.cpp:181`) | RVA `0xB222D0`, not advertised | **❌ NEW accessor — §3.E** |
| 14 | `playerObject::teleport` — optional "drag player" sub-feature (`debug_camera.cpp:294`) | — | confirm advertised, else OMIT (sub-feature can stay SWGEmu-only) |

## 3. The new rows requested (all DETOURED? no — all CALLED accessors)

All of these are **CALLED** (we invoke them), not detoured — so a plain `&fn` / `pmfRealEntry()` is fine;
no real-entry-vs-forwarder subtlety. Each replaces a Utinni-side offset with a getter compiled against
your layout.

**§3.A — `groundScene::isFreeCameraActive`** (replaces `GroundScene::currentView` read, offset #8)
- Signature: `bool GroundScene::isFreeCameraActive() const` (or `int getCurrentView()` if you prefer the
  raw view enum; we only need to know free-cam is active). Returns whether the current view == `cm_Free`.

**§3.B — `groundScene::getDebugPortalCameraMessageQueue`** (replaces `debugPortalCameraInputMap + 0xC`, offset #9)
- Utinni reads the `MessageQueue*` at offset `0xC` inside the debug-portal-camera **input map**, then
  `appendMessage`s WASD/space/shift onto it. Please advertise an accessor that returns that `MessageQueue*`
  directly — e.g. `MessageQueue* GroundScene::getDebugPortalCameraMessageQueue()` (or expose
  `InputMap::getMessageQueue()` and we'll call it on the input map you already let us reach).

**§3.C — `gameCamera::getMessageQueue`** (replaces camera `+0x248`, offset #10)
- `hkAlter` reads the `MessageQueue*` at `GameCamera+0x248` and drains movement commands from it. Please
  advertise `MessageQueue* GameCamera::getMessageQueue()` (whatever the engine calls the member at +0x248).

**§3.D — `messageQueue::getCount` + `messageQueue::getMessage`** (offsets #11/#12)
- `int MessageQueue::getCount() const` and `void MessageQueue::getMessage(int i, int* outType, float* outValue) const`
  (match your real signatures). We drain the camera MQ with these each frame in `hkAlter`. `appendMessage`
  is already advertised; these are its read-side counterparts.

**§3.E — `object::isActive`** (offset #13)
- `bool Object::isActive() const` (SWGEmu `0xB222D0`). The `hkAlter` guard short-circuits when the camera
  isn't active. If it's virtual/inline on your side, OMIT and tell us to vtable-resolve or drop the guard.

## 4. Notes / decisions

- **`objectToParent` (movement rotate, `debug_camera.cpp:289`).** Utinni currently rotates the move vector
  by `pThis->objectToParent` (a raw field). We will switch to the advertised `getTransform_o2w()` instead
  — correct for a free-flying (unparented) camera. The only loss is a camera parented into a building cell,
  where o2p ≠ o2w; we accept that edge case for v1 rather than add a row. Flag if you'd rather expose a
  camera-local-transform accessor.
- **`alter` index.** We vtable-resolve `GameCamera::alter` at **Object virtual slot 4** (`vtbl_resolve.h`,
  cross-validated by Codex). Please confirm `alter` is the slot-4 override in your build so the consumer
  self-check (slot[4] == known RVA on SWGEmu) stays valid. No row needed.
- **No SWGEmu impact.** Every consumer change is `isAdvertisedClient()`-gated or uses an accessor that
  resolves to the existing RVA on SWGEmu (D-00).

## 5. Probe appendix (for context — we are asking for accessors regardless)

Utinni `618e517` added an advertised-only, rate-limited, pointer-guarded probe in `hkUpdateLoop`. On a
loaded world (NGE advertised client) it observed:
- `currentView` = 2 / 5 → exactly `cm_FreeChase` / `cm_Free` (offset #8 reads correctly today).
- `debugPortalCameraInputMap + 0xC` → a clean heap `MessageQueue*` (offset #9 reads correctly today).
- `getCurrentCamera()+0x248` = null (inconclusive — `getCurrentCamera` returns the ACTIVE FreeChase camera,
  whose movement MQ is null while free-cam is off; `hkAlter` drives the debug camera).

So the offsets are *currently* aligned — but per the §0 decision we want accessors anyway, so neither side
ever depends on that alignment holding. The probe will be removed when free-cam lands on the accessors.

## 6. Priority

Free-cam is the highest-value remaining editor (camera-driven world editing). The §3 batch is ~5 small
called-accessor rows + 1 index confirmation. Order within: §3.A/B/C (the offset replacers) first; §3.D/E
are cheap and complete the path. Each `+1 ENGINE_HOOKPOINTS_VERSION` (baseline 12) + sha256 `.inc/.h`
resync + maintainer live smoke, per the established cadence.
