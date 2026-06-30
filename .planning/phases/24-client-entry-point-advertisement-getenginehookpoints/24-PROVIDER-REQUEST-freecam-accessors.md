# Provider Request — Free-cam editor unlock via ACCESSORS (no Utinni-side offsets)

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Date:** 2026-06-29
**Status:** REQUEST (rev. 2 — incorporates a 4-AI adversarial review: Codex, Cursor, Opus, Sonnet).
The §8 "biggest single editor" (free-cam) on the advertised DX11/NGE client.
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/`
as `2026-06-29-utinni-freecam-accessors.md`.
**Self-contained:** act on this doc without reading the Utinni Phase-24 plans. It cites the exact
Utinni read sites (file:line, verified) and the exact struct offsets each requested accessor replaces.

---

## 0. TL;DR — read this carefully (rev. 2 corrects an over-claim)

Free-cam is the last big editor to light up on `SwgClient_r.exe`. **Two facts, kept distinct:**

1. **Provider-side:** the toggle (`groundScene::changeCamera`) and camera fetch
   (`groundScene::getCurrentCamera`) are advertised + bound — the *native functions are resolvable*.
2. **Consumer-side:** the end-to-end free-cam path does **NOT** yet work on the advertised client. The
   managed driver `FreeCamImpl` and the native `processIoEvent` both route through
   `GroundScene::get()`, which **deliberately returns `nullptr` on the advertised client** (blast-radius
   control, see §4). So today the toggle path NREs and the input path early-returns before reaching any
   of the reads below.

**Therefore the accessors in §3 are NECESSARY BUT NOT SUFFICIENT.** They are paired with a consumer-side
wave (§4) that re-sources the live `GroundScene*`/camera without `get()`. This doc specifies BOTH so the
provider can see the accessors will actually be reached once delivered — otherwise the provider builds
rows that unblock nothing.

**Architectural decision (consumer side):** we will NOT hardcode NGE struct byte-offsets, even though a
diagnostic probe (Utinni `618e517`) showed free-cam's offsets read back sane on NGE today. A hardcoded
offset is a hidden coupling to your struct layout that can silently drift in a future from-source build.
We want the advertised interface to be the **complete contract**. So §3 asks you to advertise a small
accessor per fragile read (same pattern as Bucket B's `particlePreview::retrigger` friend-free-function
and the `config::setModalChat` shims). The movement-entry detour target (`alter`) is **virtual → we
vtable-resolve it ourselves** (Object slot 4) — no row, just an index confirmation (§5).

**The provider batch: ~5 called-accessor rows, delivered as ONE v13 bump** (not five waves).

---

## 1. How the unlock works (the contract both sides share)

On the advertised client, `resolveFromExe()` overwrites each `swg::*` slot **by name** from your
`GetEngineHookPoints()` table; then each subsystem's `detour()`/call path runs, gated per-target by
`installable()`. SWGEmu Pre-CU is byte-for-byte unchanged (D-00) — nothing here touches the SWGEmu path.
A read we route through an advertised accessor is safe on BOTH targets; a raw struct-offset read is the
thing that silently misreads (or AVs) when your NGE layout differs.

## 2. Complete ledger — every free-cam read/call and its disposition

Verified against the live Utinni sites (line numbers current as of `aefe19c` + the probe block). Files:
`swg/camera/debug_camera.cpp` (dc), `swg/scene/ground_scene.cpp` (gs).

| # | Utinni read/call (site) | Today | Disposition |
|---|---|---|---|
| 1 | `alter` — movement entry we DETOUR (`dc:179`, target `swg::debugCamera::alter` `0x006DA1B0`) | virtual override | **consumer vtable-resolves (Object slot 4)** — no row; confirm index + instance (§5) |
| 2 | `groundScene::changeCamera` — toggle (`gs:557/561`) | `0x0051A350` | ✅ **advertised + bound** (native resolvable; consumer path is §4) |
| 3 | `groundScene::getCurrentCamera` (`gs:550`) | `0x0051A4D0` | ✅ **advertised + bound** |
| 4 | `object::getTransform_o2w` — camera transform for the move | — | ✅ **advertised** (replaces the `objectToParent` field read; §6 note) |
| 5 | `object::move_p` — camera move (`dc:289`) | already bound | ✅ **advertised** (no action — listed for completeness) |
| 6 | `groundScene::handleInputMapEvent` — input detour entry (`gs:534` install) | — | ✅ **advertised**; consumer un-skips on advertised **as part of §4** (not standalone — see §4.3) |
| 7 | `messageQueue::appendMessage` — input enqueue (`dc:101…`) | — | ✅ **advertised** |
| 8 | `IoEvent::{type,arg1,arg2,arg3}` — input fields (`dc:92,98`; `gs:490`) | raw struct fields | **acceptable as-is — see §3.F (justified, not a row)** |
| **9** | **`GroundScene::currentView`** — `isFreeCameraActive()` (`gs:577`) | raw field | **❌ NEW accessor — §3.A** |
| **10** | **`GroundScene::debugPortalCameraInputMap + 0xC`** — input MessageQueue (`dc:90`) | raw field + offset | **❌ NEW accessor — §3.B** |
| **11** | **GameCamera `+ 0x248`** — movement MessageQueue (`dc:204`) | raw field offset | **❌ NEW accessor — §3.C** |
| **12** | **`MessageQueue::getCount`** — drain loop bound (`dc:210`) | not advertised | **❌ NEW accessor — §3.D** |
| **13** | **`MessageQueue::getMessage(...)`** — drain (`dc:214`) | not advertised | **❌ NEW accessor — §3.D (5-arg — see spec)** |
| **14** | **`Object::isActive`** — `hkAlter` guard (`dc:181`, `0xB222D0`) | not advertised | **❌ NEW accessor — §3.E** |
| 15 | mouse-wheel NOP `debugCamera::patch` (`dc:309`, `0x0051AA8D`) | SWGEmu-RVA patch | **SWGEmu-only for v1 — see §6 (wheel movement stays dark on advertised)** |

## 3. The new rows requested — all CALLED accessors, delivered as one v13 bump

All §3 rows are **CALLED** (we invoke them), not detoured → a plain `&fn` / `pmfRealEntry()` is fine; no
real-entry-vs-forwarder subtlety. Each replaces a Utinni-side offset (or supplies a missing read-side fn).

**§3.A — `groundScene::isFreeCameraActive`** (replaces `GroundScene::currentView`, #9)
- `bool GroundScene::isFreeCameraActive() const` — returns whether the current view == `cm_Free`. (We'll
  stop reading `pThis->currentView` and call this instead.)

**§3.B — `groundScene::getDebugPortalCameraMessageQueue`** (replaces `debugPortalCameraInputMap + 0xC`, #10)
- `MessageQueue* GroundScene::getDebugPortalCameraMessageQueue()` — the `MessageQueue*` Utinni currently
  reads at offset `0xC` inside the debug-portal-camera **input map** and `appendMessage`s WASD onto.
- **Use a GroundScene-level accessor, NOT an InputMap-level one.** An `InputMap::getMessageQueue()` alone
  doesn't remove the offset, because we'd still need `GroundScene::debugPortalCameraInputMap` (itself a
  field read) to get the `InputMap*`. The GroundScene-level getter encapsulates both. (If you'd rather
  expose a one-shot `GroundScene::enqueueDebugPortalCameraCommand(int cmd, float value)`, that removes the
  MessageQueue dependency entirely and we'd happily take it instead of §3.B+appendMessage.)

**§3.C — `gameCamera::getMessageQueue`** (replaces camera `+0x248`, #11)
- `MessageQueue* GameCamera::getMessageQueue()` — the queue `hkAlter` drains movement commands from.
- **Aliasing note:** §3.B and §3.C are read from **different base objects** (the InputMap vs the
  GameCamera), so both accessors are needed — but they very likely return the **same underlying
  `MessageQueue*` instance** when the debug camera is active (standard SWG InputMap→camera-MQ wiring).
  Please **confirm whether they alias**; if they always do, we'd accept a single shared accessor and one
  fewer row. We did not prove aliasing from the probe.

**§3.D — `messageQueue::getCount` + `messageQueue::getMessage`** (#12/#13)
- `int MessageQueue::getCount() const`.
- **Exact signature matters here.** Utinni's current typedef is 5-arg:
  `void MessageQueue::getMessage(int i, int* outType, float* outValue, uint32_t* outFlags) const` (we pass
  `nullptr` for `outFlags`). Advertise the **real** method at that signature, OR if you make a shim,
  explicitly name it as a 3-arg `getMessage(int, int*, float*)` shim so we bind the right ABI. (`appendMessage`
  is already advertised; these are its read-side counterparts and must ship in the SAME v13 wave, else
  `hkAlter`'s drain loop calls unresolved RVAs and AVs after input otherwise works.)

**§3.E — `object::isActive`** (#14)
- `bool Object::isActive() const`. Declared non-virtual on our side (`object.h:113`) and absent from the
  Object vtable slot list (`vtbl_resolve.h` has `setActive`=12 but no `isActive`), so we expect a plain
  advertised CALL endpoint (it is NOT a struct-offset accessor). **If it IS virtual/inline on your build,
  give us the vtable slot index in the SAME reply** (so we don't need a second round-trip) — or tell us to
  drop the guard.

**§3.F — `IoEvent` field reads (NO row requested — justification)**
- `processIoEvent`/`hkHandleInputEvent` read `ioEvent->{type,arg1,arg2,arg3}` as raw fields. We treat
  `IoEvent` as a **stable shared layout** because the struct is delivered *to us through the advertised
  `handleInputMapEvent` hook* — i.e. it's part of the input-event ABI both builds already share, not an
  internal struct we reach into uninvited. **If `IoEvent`'s layout differs on your NGE build, flag it** and
  we'll request `IoEvent` accessors too; otherwise we proceed reading these fields.

## 4. PAIRED CONSUMER PLAN (the load-bearing half — accessors are necessary, not sufficient)

This is Utinni's side; it does not block your delivery, but it's why the §3 accessors will actually be
reached. The 4-AI review flagged that without this section the request reads as "~5 rows → smoke-pass,"
which is false.

1. **Re-source the live `GroundScene*` without `get()`.** `GroundScene::get()` returns `nullptr` on the
   advertised client by design (`gs:199`); the WS-4 latch `s_advertisedGroundScene` holds the live
   instance but deliberately does NOT feed `get()` (blast-radius control). Free-cam will read the latch
   (or thread the hooked `pThis`) — NOT widen `get()`.
2. **`processIoEvent` rework.** It opens with `GroundScene::get()` and early-returns on null (`dc:84`).
   Change its signature to accept the `GroundScene*` that `hkHandleInputEvent` already holds (`gs:499`),
   so the §3.B accessor is reachable.
3. **Un-skip `handleInputMapEvent` on advertised** (`gs:534` is currently `!isAdvertisedClient()`-gated) —
   but ONLY together with step 2, or it still lands in `processIoEvent`'s null early-return.
4. **Managed `FreeCamImpl` null-gates.** It dereferences `GroundScene.Get()` throughout
   (`FreeCamImpl.cs:78,125,135,152,177,188`) — all NRE on advertised. Re-route to the latch-backed path /
   a new advertised export.
5. **`alter` deferred install + self-check** — see §5 (needs a live camera, which doesn't exist at
   `debugCamera::detour()` startup time).

## 5. `alter` — consumer vtable-resolve (no provider row; please confirm index + instance)

- **Name precisely:** the detour target is the **debug/free-camera `alter` override**
  (`swg::debugCamera::alter` = `0x006DA1B0`), NOT `swg::gameCamera::alter` (`0x00788740`, a *different*
  function). Please confirm against the debug/free camera's vtable, not a generic GameCamera.
- **Slot index:** we resolve the `alter` override at **Object virtual slot 4**. Single inheritance
  (`GameCamera : RenderWorldCamera : Camera : Object`) means an `Object::alter` override reuses slot 4.
  This index is documented in a `vtbl_resolve.h` comment but is **not yet in the empirically self-checked
  constant set** (only slots 2/3/13 are). We will add a `kObjectAlter=4` self-check (resolved slot ==
  known RVA on SWGEmu). **Please confirm slot 4 against your build's vtable dump** so a silent layout drift
  can't break movement without a version bump.
- **Instance + timing:** vtable-resolve needs a LIVE `GameCamera` instance, but `debugCamera::detour()`
  runs at startup before any camera exists. We will defer the resolve/install to first scene-up and read
  slot 4 off `getCurrentCamera()` (advertised) AFTER `changeCamera(cm_Free)` — i.e. NOT off the raw
  `groundScene->debugPortalCamera` field (which would re-introduce an offset). Please confirm the
  free/debug camera returned by `getCurrentCamera()` post-`changeCamera(cm_Free)` is the one whose `alter`
  we should detour.

## 6. Notes / decisions

- **`objectToParent` (movement rotate, `dc:289`).** We switch from `pThis->objectToParent` (raw field) to
  the advertised transform — calling `pThis->getTransform_o2w()->rotate_l2p(direction)` (NOT
  `getTransform()`, which itself returns `&objectToParent` when parented, `object.cpp:280`). Correct for a
  free-flying (unparented) camera; the only loss is a camera parented into a building cell (o2p ≠ o2w),
  accepted for v1.
- **Mouse wheel stays SWGEmu-only for v1** (#15). `hkAlter`'s wheel handling (msg 167) depends on the
  `debugCamera::patch` NOP at `0x0051AA8D`, a mid-function SWGEmu patch with no advertised equivalent. WASD
  + space/shift fly works without it; wheel-speed is the §2.D cooperative-toggle bucket — request later if
  wanted.
- **No SWGEmu impact.** Every consumer change is `isAdvertisedClient()`-gated or uses an accessor that
  resolves to the existing RVA on SWGEmu (D-00).

## 7. Probe appendix (context — we ask for accessors regardless)

Utinni `618e517` added an advertised-only, rate-limited, pointer-guarded probe in `hkUpdateLoop`. On a
loaded world (NGE advertised client) it observed: `currentView` = 2/5 (= `cm_FreeChase`/`cm_Free`, #9 reads
correctly today); `debugPortalCameraInputMap + 0xC` = a clean heap `MessageQueue*` (#10 correct today);
`getCurrentCamera()+0x248` = null (inconclusive — `getCurrentCamera` returns the ACTIVE FreeChase camera,
whose movement MQ is null while free-cam is off; `hkAlter` drives the debug camera). So the offsets are
*currently* aligned — but per §0 we want accessors anyway so neither side depends on that holding. The
probe is removed when free-cam lands on the accessors.

## 8. Priority

Free-cam is the highest-value remaining editor (camera-driven world editing). Deliver §3.A–E as **one v13
bump** (baseline 12 → 13) + sha256 `.inc/.h` resync + the §5 confirmations, then one maintainer live smoke
after the paired §4 consumer wave lands. §3.B/C (the offset replacers) are the core; §3.D/E complete the
movement path and must ride the same wave.
