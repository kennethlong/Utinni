# Provider Request — MISC/INPUT editor-hook unlock on the advertised client

**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Date:** 2026-06-24
**Status:** REQUEST — opens the next follow-on after the v3 / 94-name catalog + DX11 live client.
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/`
as `2026-06-24-utinni-misc-input-editor-unlock.md`.
**Self-contained:** act on this doc without reading the Utinni Phase-24 plans/SUMMARYs. It cites the
exact Utinni detour sites (file:line) and the exact provider rows (`engine_advertise.cpp`) so each ask
is verifiable on both sides.

---

## 0. TL;DR

The advertised DX11 client boots + renders the overlay **RENDER-only**. The remaining feature is the
**editor unlock**: lighting up the MISC + INPUT detour subsystems (scene / chat / HUD / input editors)
on `SwgClient_r.exe`. Today Utinni wholesale-skips those two groups on the advertised client
(`utinni.cpp createDetours()` / `createPatches()` — `if (advertised) …`).

The bindings + per-target `installable()` gating infra are already in place (92/94 names bound,
`endpoints_bindings.cpp`). The unlock is **incremental and live-smoke-gated, one subsystem at a time** —
a wholesale drop is unsafe (proven: `ff7e80e` reverted for per-hook runtime crashes). This doc is the
**complete classified ledger** of every MISC/INPUT detour/patch target and what each needs. Most fall
into one of three provider-actionable buckets; the rest are consumer-side or joint decisions.

**The provider's near-term batch is small** (§4): two contract *corrections* to existing rows
(`game::mainLoop`, `treeFile::open`), one loop-counter accessor, and `cuiChatWindow::ctor` real-entry
for the chat editor. The larger INPUT/HUD row set (§5 bucket C) is a later wave, advertised
incrementally as each in-game editor is smoked — **do not advertise it all up front.**

---

## 1. How the unlock works (the contract both sides share)

On the advertised client, `swg::endpoints::resolveFromExe()` overwrites each `swg::*` function-pointer
slot **by name** from your `GetEngineHookPoints()` table, then each subsystem's `detour()` runs.
`installable(target)` (committed-+-executable check on the hooked address) is the per-target gate:

- **SWGEmu Pre-CU** (no export): `installable()` is always true → the full hook set installs
  byte-for-byte unchanged (D-00). **Nothing in this doc changes the SWGEmu path.**
- **Advertised client:** `installable(target)` is true only when `target` resolved from your catalog to
  real relocated code. A subsystem whose hooked target stayed at its (unmapped) SWGEmu RVA literal must
  be skipped, or `Detour::Create` writes a JMP into unrelated relocated code → `0xC0000096` crash.

The unlock = Utinni converts the wholesale `if (advertised) return;` group-skips into **per-`Detour::Create`
`installable()` gates**, so each subsystem installs exactly the hooks whose targets you advertise and
skips the rest. **For that to light up a feature, you must advertise the function that feature's hook
DETOURS — by its REAL engine entry** (see §2). Anything you don't advertise stays gracefully skipped on
the advertised client (no crash, feature just dark there) — so we unlock subsystems as you deliver them.

## 2. The one rule that bit us before — detour targets need the REAL entry

This generalizes the 38-05 detour-vs-call fix (`2026-06-22-utinni-detour-vs-call-followup.md`). For a
hook Utinni **DETOURS** (patches the prologue to intercept the engine's own call into the method), you
must advertise **the address the engine's call path actually reaches** — the compiled method body — not
a call-through `__fastcall` forwarder and not a placement-new ctor thunk. A forwarder/placement-thunk
advertised for a detoured row is **silently dead**: it links, exports, passes the self-check, boots, and
the hook never fires. You already applied this correctly for `groundScene::{update,handleInputMapEvent}`
and `cuiChatWindow::{enableTextInput,chatEnterHandler}` via `pmfRealEntry()` (delta==0 verified). Every
NEW row this doc asks for is likewise **DETOURED** unless explicitly marked CALLED — use `pmfRealEntry()`
(MI classes) or a plain `&fn` (single-inheritance / static), never a call-through thunk.

---

## 3. Complete classified ledger — every MISC/INPUT detour/patch target

Verified against the live Utinni detour sites (file:line) and your `engine_advertise.cpp` table.
"In contract?" = present + correctly bound in `endpoints_bindings.cpp s_bindings[]`.

| # | Utinni hook (detour site) | Engine target | In contract? | Bucket |
|---|---|---|---|---|
| MISC | | | | |
| 1 | `config::detour` → `loadOverrideConfig` (config.cpp:79) | `loadOverrideConfig` thunk | ✅ correct | **A** consumer-gate only |
| 2 | `CuiManager::detour` → `render` (cui_manager.cpp:186) | `CuiManager::render` | ✅ correct | **A** consumer-gate only |
| 3 | `report::detour` → `print` (swg_misc.cpp:50) | `Report::puts` | ✅ correct | **A** consumer-gate only |
| 4 | `Game::detour` → `mainLoop` (game.cpp:512) | provider `Game::run()` | ⚠️ **signature mismatch** | **B** correction |
| 5 | `Game::detour` → `install/setupScene/cleanupScene` | `Game::*` (static) | ✅ correct | A (rides #4's gate) |
| 6 | `Game::detour` gate → `getMainLoopCount()` (game.cpp:509) | reads hardcoded `0x1908830` | ❌ OMITTED (inline) | **B** needs accessor |
| 7 | `treefile::detour` → `searchTree` (tree_file.cpp:81) | provider static `TreeFile::open` | ⚠️ **semantic collision** | **B** correction |
| 8 | `CuiManager::detour` → `findObjectUnderCursor` (cui_manager.cpp:187) | `0x00BD3E20` | ❌ not advertised | **C** new row |
| 9 | `CuiChatWindow::detour` → `ctor` / `hkCtor` (cui_chat_window.cpp:540) | `0x00F364B0` | ❌ DEFERRED (MI ctor) | **C** new row (real entry) |
| 10 | `cuiRadialMenuManager::detour` → `update` (cui_radial_menu.cpp:51) | `0x009698C0` | ❌ not advertised | **C** new row |
| 11 | `cuiMenu::detour` → default-cursor hook (cui_menu.cpp:50) | (cursor-info hook) | ❌ not advertised | **C** new row |
| 12 | `cuiLoginScreen::detour` → `ctor`+`activate` (cui_misc.cpp:123) | `0x00C8CE00` / `0x00C8D190` | ❌ not advertised | **C** new row |
| 13 | `SystemMessageManager::detour` → `receiveMessage` (cui_manager.cpp:271) | `0x008ABEB0` | ❌ not advertised | **C** new row |
| 14 | `IoWin`/`MessageQueue::detour` → `appendMessage(Data)` (io_win.cpp:98) | `0x00AA6640` / `0x00AA6480` | ❌ not advertised | **C** new row |
| 15 | `Client::detour` → `clientMain` / `writeMiniDump` (client.cpp:290/297) | advertised | ✅ correct | A (but blocked by #16) |
| 16 | `Client::detour` gate → `setupStartDataInstall` (client.cpp:286/289) | `0x00A9F970` | ❌ **NONEXISTENT** | **F** SWGEmu-only |
| 17 | `Client::detour` → `writeCrashLog` + `midCrashLogWrite` (client.cpp:296/298) | `0x00A9F640` / `0x00A9F766` | ❌ **NONEXISTENT** / mid-fn | **F**/**E** |
| INPUT | | | | |
| 18 | `GroundScene::detour` → `update` + `handleInputMapEvent` (ground_scene.cpp) | real-entry v3 | ✅ correct | **A** consumer-gate only |
| 19 | `GroundScene::detour` → `draw` (ground_scene.cpp:411) | virtual | ❌ SKIP (virtual) | **D** consumer vtable |
| 20 | `cuiIo::detour` → `processEvent` (cui_io.cpp:203) | virtual `0x093BD50` | ❌ SKIP (virtual) | **D** consumer vtable |
| 21 | `cuiHud::detour` → `actionPerformAction`+`getTarget` (cui_hud.cpp:327) | `0x00EDBAA0` / `0x00BD3E20` | ❌ not advertised | **C** new row |
| 22 | `creatureObject::detour` → `setTarget` (creature_object.cpp:152) | `0x00434AB0` | ❌ not advertised | **C** new row |
| 23 | `debugCamera::detour` → `alter` (debug_camera.cpp:300) | `0x006DA1B0` | ❌ not advertised | **C** new row |
| 24 | `debugCamera::patch` → NOP `0x0051AA8D` (debug_camera.cpp:309) | mid-fn, in `handleInputMapEvent` | ❌ mid-fn | **E** joint |
| PATCHES | | | | |
| 25 | `cuiMisc::patch` → `0x00C8D250` / `0x00C7D57F` / `0x009CC385` (cui_misc.cpp:57) | mid-fn (offline scenes etc.) | ❌ mid-fn | **E** joint |
| — | `cuiChatWindow` Issue #11 chat-routing | mid-body NOP | ❌ mid-fn | **E** joint |
| — | `clientWorld::detour` (client_world.cpp:45) | empty / no-op | — | none |

**Bucket legend:**
- **A** — already advertised + ABI-correct. **No provider action.** Consumer adds the per-target gate
  and live-smokes. (#1,2,3,5,15,18)
- **B** — bound but the address is the wrong/incompatible function. **Provider correction needed** (§4).
- **C** — DETOURED by Utinni but not advertised. **Provider adds a real-entry row** (§5), incremental.
- **D** — virtual. **Consumer resolves off the live vtable** (as for D3D9 Present); provider correctly
  SKIPS. No provider row. (#19,20)
- **E** — mid-function byte/NOP patch. **Not expressible as a function pointer.** Joint decision: a
  cooperative engine API, or accept SWGEmu-only (§6). (#24,25, Issue #11, #17 midCrashLogWrite)
- **F** — NONEXISTENT in the provider tree. Utinni keeps on RVA / accepts SWGEmu-only / drops (§6). (#16,17)

---

## 4. Provider near-term batch (do these first — small, high-value)

These are **corrections to the existing contract** (rows that resolve today but to the wrong/incompatible
function) plus the one chat-editor blocker. They unblock the *game-lifecycle*, *asset-reload*, and *chat*
editors — the foundation the other subsystems build on.

### 4a. `game::mainLoop` — advertise a signature-compatible per-frame entry (or confirm the adapter)
- **Problem:** you advertise `game::mainLoop → &Game::run` (`void run()`). Utinni's hook `hkMainLoop` is
  `__cdecl(bool presentToWindow, HWND, int width, int height)` — the SWGEmu `mainLoop` signature. Both
  sides are `__cdecl`, so the precise failure is **not** stack-cleanup corruption — it's **wrong-entry +
  garbage-args + wrong lifecycle**: the trampoline reads four nonexistent args (`presentToWindow/hwnd/
  width/height`) off the caller frame and acts on garbage geometry, and `Game::run()` reads as the outer
  *once-per-process* loop, not the *per-frame* tick the old `mainLoop(present,hwnd,w,h)` was. (Stack
  corruption is only a possible downstream effect, not the primary mechanism — the existing game.cpp:502
  comment overstates it.) `Game::detour` is wholesale-skipped today to avoid the resulting fault.
- **Ask:** confirm the engine's actual per-frame loop function + its real signature. If `Game::run()` is
  genuinely the once-per-process loop (not per-frame), tell us which function is the per-frame tick so we
  retarget `hkMainLoop`. **This may be a consumer-side ABI-adapter rather than a provider change** — we
  just need the ground truth on `Game::run`'s contract (called once? per-frame? args?) to decide.

### 4b. `game` loop-counter — expose a non-inline accessor
- **Problem:** `Game::detour`'s entry gate reads the main-loop counter at hardcoded `0x1908830`
  (game.cpp:509, `getMainLoopCount()`), to detect the suspended-startup loop. You OMITTED
  `game::g_mainLoopCounter` (only the inline `Game::getLoopCount()` exists → no ODR address).
- **Ask:** add a non-inline accessor (e.g. `int Game::getLoopCount()` out-of-line, or a
  `g_mainLoopCounter` accessor row) so the consumer reads the relocated counter instead of `0x1908830`.
  Same pattern as the `g_runningFlags → &Game::isOver` accessor you already ship.

### 4c. `treeFile::open` — semantic collision, re-map to the path-registration function
- **Problem:** you advertise `treeFile::open → &TreeFile::open` (static `__cdecl AbstractFile* open(const
  char*, PriorityType, bool)` — opens a file). But Utinni's slot/hook `hkSearchTree` is a `__thiscall`
  **search-path registration** method `searchTree(pThis, priority, treeFilename)` (tree_file.cpp:51,81) —
  a *different function* that registers a `.tre`/loose dir at a search priority (this is what powers
  loose-override asset reload). The name "open" collided two unrelated functions; resolving it overwrites
  Utinni's searchTree slot with the static file-open address → stack corruption when the engine adds a
  search path during login (tree_file.cpp:72-77).
- **Ask:** advertise the engine's actual **search-path / TreeFile-priority registration** function (the
  `__thiscall` one that registers a tree at a priority) under a distinct contract name (suggest
  `treeFile::searchTree`), and leave `treeFile::open` mapped to the static file-open if any consumer
  wants it. We'll bind `searchTree` to the existing slot and drop the collision.

### 4d. `cuiChatWindow::ctor` — real ctor entry for the chat editor (DETOURED)
- **Problem:** you DEFERRED `cuiChatWindow::ctor` (MI ctor needing a live `UIPage&`). But Utinni
  **DETOURS** it (`hkCtor`, cui_chat_window.cpp:540) to track chat-window construction. Per §2, a
  placement-new `__fastcall` thunk would be **detour-dead** here — we need the **real engine ctor entry**.
- **Ask:** advertise the `SwgCuiChatWindow(UIPage&, Game::SceneType, std::string const&)` ctor's real
  code entry (the address the engine's own construction path reaches), resolved per the MI encoding like
  `pmfRealEntry` (delta==0 if the most-derived ctor's `this` is the primary base). This is the opposite
  mechanism from `groundScene::ctor` (which Utinni CALLS → your placement-new thunk is correct there); two
  ctors, opposite mechanisms, because Utinni uses them oppositely.

**Version:** 4a-4c re-point existing addresses (no name change → contract names stable); 4c adds the
distinct `treeFile::searchTree` name and 4d adds `cuiChatWindow::ctor` → **two NAME ADDs → bump
`ENGINE_HOOKPOINTS_VERSION` 3 → 4** and re-sync the `.h`/`.inc` byte-identical into `D:/Code/Utinni`.

---

## 5. Provider later wave (bucket C) — incremental, one editor at a time

These INPUT/HUD/menu hooks are DETOURED by Utinni but not advertised. Each lights up a specific in-game
editor interaction and **each needs its own maintainer live-smoke** before Utinni enables it — so
**advertise them incrementally, driven by which editor we're unlocking**, not all at once (an unverified
batch is a batch of `0xC0000005`s waiting for ASLR roulette). When you pick one up, advertise its REAL
entry (§2) and bump the version; the consumer wires + gates + smokes it.

| contract name (suggested) | engine symbol | Utinni hook | feature |
|---|---|---|---|
| `cuiManager::findObjectUnderCursor` | `0x00BD3E20` | `hkFindObjectUnderCursor` | world-object pick under cursor (shared w/ `cuiHud::getTarget`) |
| `cuiHud::actionPerformAction` | `0x00EDBAA0` | `hkActionPerformAction` | keybind / action routing (Issue #11/#12 lineage) |
| `cuiHud::getTarget` | `0x00BD3E20` | `hkGetTarget` | current target read |
| `creatureObject::setTarget` | `0x00434AB0` | `hkSetTarget` | target-change callback |
| `cuiRadialMenuManager::update` | `0x009698C0` | radial-menu hook | radial menu instrumentation |
| `cuiMenu::*` (default-cursor) | (cui_menu.cpp:50) | cursor-info hook | menu cursor |
| `cuiLoginScreen::ctor` + `activate` | `0x00C8CE00` / `0x00C8D190` | login-screen hooks | login-screen automation |
| `systemMessageManager::receiveMessage` | `0x008ABEB0` | `hkReceiveMessage` | system-message callback |
| `messageQueue::appendMessage` + `appendMessageData` | `0x00AA6640` / `0x00AA6480` | input-map output instrument | INPUT-path (Issue #11 diag) |
| `debugCamera::alter` | `0x006DA1B0` | debug-camera per-frame | free-cam |

For each: note whether the in-tree method is virtual (→ SKIP, consumer vtable), inline (→ OMIT, needs an
out-of-line twin), or MI (→ `pmfRealEntry`). Confirm-or-OMIT per row as you did for Bucket-4 — **a wrong
`&` is worse than a missing row.**

---

## 6. Joint decisions (buckets E + F — not a pointer contract)

These cannot be expressed as `{name, &fn}` and need a call, not a row:

- **Mid-function patches (E):** `debugCamera::patch` (2-byte NOP at `0x0051AA8D` inside
  `handleInputMapEvent`), `cuiMisc::patch` (offline-scenes / location-button / RESO.Y→CUI.X NOPs),
  `client::midCrashLogWrite` (mid-fn JMP at `0x00A9F766`), and the **Issue #11** chat-context-routing NOP.
  Each is a Pre-CU instruction-stream edit at a non-entry offset. **Per feature:** either the engine
  exposes a cooperative toggle (e.g. a real "modal chat context" setter for Issue #11, an "enable offline
  scenes" flag for `cuiMisc`, a "debug-camera input passthrough" flag for `debugCamera::patch`), or we
  accept the feature as **SWGEmu-only** on the advertised client. Lowest-effort wins are the boolean
  toggles — tell us which you'd rather expose vs. accept SWGEmu-only.

- **NONEXISTENT (F):** `client::setupStartDataInstall` (`0x00A9F970` — no twin; it's `Client::detour`'s
  gating primary, so the whole client crash-log/minidump subsystem is blocked behind it) and
  `client::writeCrashLog` (`0x00A9F640` — crash `.txt` is inline `sprintf` in `SetupSharedFoundation.cpp`,
  no named fn). Confirmed NONEXISTENT in 38-02. **Resolution:** Utinni changes `Client::detour`'s gating
  primary off `setupStartDataInstall` so `clientMain` + `writeMiniDump` (both advertised) can install
  independently, and accepts the crash-log relocation + mid-fn JMP as SWGEmu-only. **No provider action**
  unless you want to add a cooperative crash-log-dir setter (optional, low value).

---

## 7. Consumer-side worklist (Utinni — for coordination; not your action)

So you can see the whole shape. Utinni will, per subsystem, as you deliver:
1. Replace the wholesale `if (advertised) return;` group-skips with per-`Detour::Create` `installable()`
   gates (so `CuiManager::detour` installs `render` but skips `findObjectUnderCursor` until advertised;
   `Client::detour` installs `clientMain`/`writeMiniDump` but skips the NONEXISTENT pair; etc.).
2. Resolve the two **virtual** targets (`cuiIo::processEvent`, `groundScene::draw`) off the **live vtable**
   (bucket D) — no provider row.
3. Retarget `hkMainLoop` to `Game::run`'s real signature (pending your §4a answer) and read the loop
   counter via your §4b accessor.
4. Bind `treeFile::searchTree` (§4c) and `cuiChatWindow::ctor` real-entry (§4d).
5. **Live-smoke each subsystem** before committing it (the hard guardrail: `installable()` / headless
   builds / ABI gates cannot catch ABI / ASLR / embed-state / render-correctness failures — only a live
   inject-smoke on `SwgClient_r.exe` can).

## 8. Sync protocol & gates

- The `.h`/`.inc` stay **SHARED-VERBATIM** — re-sync byte-identical into `D:/Code/Utinni/UtinniCore/swg/`
  at each wave close (sha256-verify both files, as in the v3 handback). Provider commits its own repo;
  Utinni commits its own.
- Bump `ENGINE_HOOKPOINTS_VERSION` on any NAME add/remove (§4 → v4; each later-wave row → +1). Address-only
  re-points (4a/4b) don't strictly need a bump but ride the §4 batch's v4.
- Keep the `utinni_verifyNoNullNoDup()` name-set-equality self-check green for the grown required set.
- **Every advertised-client unlock lands only after a maintainer live inject-smoke** — see §7.5.

## 9. Pointers

- Provider contract: `src/game/client/application/SwgClient/src/shared/engine_hookpoints.{h,inc}` +
  `src/.../win32/engine_advertise.cpp` (table + thunks + `pmfRealEntry` + self-check + export).
- Consumer bindings: `UtinniCore/swg/endpoints_bindings.cpp` (`s_bindings[]`), gate
  `UtinniCore/swg/endpoints.{h,cpp}` (`installable()` / `isAdvertisedClient()`), unlock site
  `UtinniCore/utinni.cpp` (`createDetours()` / `createPatches()`).
- Prior handoffs this builds on: `2026-06-22-utinni-advertised-client-coverage-status.md` (the bucket
  taxonomy this ledger instantiates), `2026-06-22-utinni-provider-coverage-handback.md` (§4c "drop the
  group skips as coverage grows"; §4d DEFER items), `2026-06-22-utinni-detour-vs-call-followup.md` (§2's
  real-entry rule).
