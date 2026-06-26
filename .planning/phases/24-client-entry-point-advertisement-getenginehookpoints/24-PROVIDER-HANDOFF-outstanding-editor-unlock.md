# Provider Handoff — Outstanding Editor-Unlock Work (`SwgClient_r.exe` / `GetEngineHookPoints`)

**Status:** HANDOFF — ready for the `swg-client-v2` provider instance. Consolidates and SUPERSEDES the
still-open parts of the scattered `24-PROVIDER-REQUEST-*` / `24-PROVIDER-CONSULT-*` docs.
**Author:** Utinni Phase 24 (advertised-client editor-unlock arc), 2026-06-26.
**Target repo:** `D:/Code/swg-client-v2` (`SwgClient_r.exe`, the from-source SWG client).
**Consumer:** Utinni `UtinniCore.dll` (injected 32-bit overlay).
**Contract baseline:** `engine_hookpoints.{h,inc}` at `ENGINE_HOOKPOINTS_VERSION 6`.
**Source of truth:** this file in the Utinni repo. Copy into `swg-client-v2/.planning/handoff/` as
`2026-06-26-utinni-provider-outstanding-editor-unlock.md`. If the two diverge, the Utinni copy governs
the consumer contract.

> **Read order:** this doc is the single live ledger of what the provider still owes. The original
> contract (mechanism, export shape, dual-path, sha256 re-sync discipline) is
> `24-ENGINE-ENTRYPOINT-ADVERTISEMENT-SPEC.md`; the mechanism-bucket taxonomy is
> `24-ADVERTISED-CLIENT-COVERAGE-STATUS.md`. Those two are still authoritative for HOW; this doc is WHAT
> is left.

---

## 0. TL;DR

The hard parts are **done**. Over the last waves the provider delivered the render attach, the DX11
embed-resize + UI reflow, the `game::loadScene` full-lifecycle scene entry, the table static-init-race
fix (40/96 → full population), `treeFile::enumerateFiles`, the `game::mainLoop`/loop-counter accessors,
and the MI real-entry re-advertisements (`groundScene::{update,handleInputMapEvent}`,
`cuiChatWindow::{enableTextInput,chatEnterHandler}`). The advertised DX11 client now **boots → login →
loads worlds in-world from TJT → embed scales → drives the Terrain editor's live `.trn` reload**.

What is LEFT is **incremental, per-editor**, and falls into five clean buckets:

- **A — Per-editor real-entry detour rows** (the bucket-C row set): one `&fn`/MI-thunk per remaining
  editor workflow (chat, HUD/world-pick, radial menu, login, system-message, input-diag, free-cam).
- **B — Render/appearance group** for the **Effects editor's live preview** (particle/skeletal/render-
  world entries + a cooperative particle-retrigger entry).
- **C — Virtual methods** resolvable off the live vtable (consumer-preferred; provider thunk optional).
- **D — Mid-function cooperative toggles** (JOINT decision: a boolean engine toggle per feature, or
  accept SWGEmu-only).
- **E — WS-5 scene-ready callback** (`Utinni_OnSceneReady`) — only for engine-INITIATED scene changes;
  off the critical path.

Every bucket-A/B name is a **NAME ADD → bump `ENGINE_HOOKPOINTS_VERSION` (+1 per wave) + re-sync
`engine_hookpoints.{h,inc}` byte-identical (sha256-verify)** into `D:/Code/Utinni/UtinniCore/swg/`.
Every advertised-client unlock lands only after a maintainer live inject-smoke.

---

## 1. Already RESOLVED — do NOT redo (closeout ledger)

So the provider does not re-open settled work, here is the disposition of every prior request doc:

| Prior doc | Disposition |
|-----------|-------------|
| `…FOLLOWUP-detour-vs-call` | ✅ RESOLVED — `groundScene::{update,handleInputMapEvent}` + `cuiChatWindow::{enableTextInput,chatEnterHandler}` re-advertised by real entry (`pmfRealEntry`, delta==0). (drove v3) |
| `…REQUEST-enginehook-rename` | ✅ RESOLVED — cosmetic paired rename applied; binary contract neutral (no bump). |
| `…REQUEST-gl11-startup-crash` | ✅ WITHDRAWN — stale June-2 dump, not a regression. No action. |
| `…REQUEST-s1-injection-startup-regression` | ✅ RESOLVED consumer-side (Utinni `d2040ca` gated 5 RENDER detours on `!advertised`). §1 build stays. |
| `…REQUEST-embed-resize-and-followups` | ✅ §1 crop + §2 UI-teardown null-deref RESOLVED. **§3 `cuiChatWindow::ctor` real-entry → carried to §2.A below.** |
| `…REQUEST-setupscene-remap` | ✅ SUPERSEDED by the scene-load-lifecycle consult (`game::loadScene`). |
| `…CONSULT-scene-load-lifecycle` | ✅ RESOLVED — `game::loadScene(terrain, player)` full-lifecycle thunk delivered + consumed. |
| `…REQUEST-table-static-init-race` | ✅ RESOLVED — function-call rows lazily populated; full name resolution at init. |
| `…REQUEST-treefile-enum-and-inworld-reflow` | ✅ RESOLVED — `treeFile::enumerateFiles` landed (v5); RNDR-04 in-world reflow done. |
| `…REQUEST-misc-input-editor-unlock` | ✅ §4a/4b/4c done. **§4d + §5 bucket-C + §6 bucket-E → carried to §2.A/§2.D below.** |
| `24-ADVERTISED-CLIENT-COVERAGE-STATUS` | reconciliation — mechanism-bucket taxonomy (still authoritative). |
| `24-DX11-ADVERTISED-CLIENT-GAP` | ✅ DX11 overlay install + embed-resize + scaling RESOLVED; advertised DX11 client fully functional under injection. |

**Net:** the only carry-forwards are `cuiChatWindow::ctor` real entry and the per-editor bucket-C/E rows.
Everything below is additive to a known-good v6 baseline.

---

## 2. OUTSTANDING DELIVERABLES

Per row: **Name** (contract identity) · **UtinniCore signature-source** (`file:line` of the `using
pFn = …` typedef — the authoritative prototype) · **SWGEmu RVA** (identification only; take `&fn`
instead) · **Mechanism** · **Unblocks**. Map each name to `&YourEngineSymbol`. A wrong `&` is worse
than a missing row (missing degrades; wrong detours the wrong function).

### 2.A — Per-editor real-entry detour rows  (the remaining bucket-C set)

Each is a DETOURED endpoint → advertise its **real engine code entry**, NOT a call-through `__fastcall`
forwarder or placement-new thunk (those are silently detour-dead — the `…FOLLOWUP-detour-vs-call`
lesson). For multiple-inheritance classes use the same `pmfRealEntry()` MI-decoding you applied to
`groundScene`/`cuiChatWindow` (verify delta==0 against the primary base).

| Name | UtinniCore signature-source | SWGEmu RVA | Mechanism | Unblocks |
|------|------------------------------|-----------|-----------|----------|
| `cuiChatWindow::ctor` | swg/ui/cui_chat_window.cpp:56 | 0x00F364B0 | MI ctor real entry (most-derived; `this` == primary base) | **Chat editor** — the ctor detour publishes the live chat-window instance every other chat hook depends on |
| `cuiManager::findObjectUnderCursor` | swg/ui/cui_manager.cpp:43 | 0x00BD3E20 | static `&fn` | World-object pick under cursor |
| `cuiHud::actionPerformAction` | swg/ui/cui_hud.cpp (hud set) | 0x00EDBAA0 | real entry | HUD action/keybind routing (Issue #11/#12 lineage) |
| `cuiHud::getTarget` | swg/ui/cui_hud.cpp | 0x00BD3E20 | real entry | Current-target read for target-aware editing |
| `cuiHud::update` | swg/ui/cui_hud.cpp | 0x00BD56F0 | real entry | Per-frame HUD instrumentation |
| `creatureObject::setTarget` | swg/object/creature_object.cpp:36 | 0x00434AB0 | `&fn` | Target-change callback |
| `cuiRadialMenuManager::update` | swg/ui/cui_radial_menu.cpp | 0x009698C0 | real entry | Radial-menu editing |
| `cuiMenu::infoTypesFindDefaultCursor` | swg/ui/cui_menu.cpp:50 | 0x00A08EE0 | confirm-or-OMIT (virtual/inline/MI per row) | Menu cursor behavior |
| `cuiLoginScreen::ctor` | swg/ui/cui_misc.cpp (login set) | 0x00C8CE00 | MI ctor real entry | Login-screen automation |
| `cuiLoginScreen::activate` | swg/ui/cui_misc.cpp | 0x00C8D190 | real entry | Login-screen automation |
| `systemMessageManager::receiveMessage` | swg/ui/cui_manager.cpp | 0x008ABEB0 | real entry | System-message editor/diagnostics |
| `messageQueue::appendMessage` | swg/misc/io_win.cpp | 0x00AA6640 | real entry | INPUT-path diagnostics (Issue #11) |
| `messageQueue::appendMessageData` | swg/misc/io_win.cpp | 0x00AA6480 | real entry | INPUT-path diagnostics |
| `debugCamera::alter` | swg/camera/debug_camera.cpp | 0x006DA1B0 | real entry | **Free-cam** on the advertised client |

> The exact signature-source line for each `using pFn` typedef is in the named file; the SPEC §6 catalog
> reproduces them. Where a row is virtual/inline/MI-inflated, decode per the coverage-status §2 table
> (or OMIT and tell the consumer to vtable-resolve — see §2.C).

### 2.B — Render / appearance group  (unblocks the **Effects editor live preview**)

The Effects/ClientEffect editor's *basic edit + save* path is **pure managed I/O and already works on the
advertised client** (no native dependency). What is dark is the **live "Preview in client" retrigger**.
Two parts:

**(i) Appearance/render detour rows** — today these hook hardcoded SWGEmu RVAs, are NOT in the contract,
and are consumer-gated `if (!advertised)` (utinni.cpp:247-254) precisely because the resolver has no name
to overwrite them with → on the advertised client the literals land on relocated `CuiStringIds`
static-init code → `0xC0000096`. Advertise them so the resolver can bind them:

| Name | UtinniCore signature-source | SWGEmu RVA | Mechanism | Unblocks |
|------|------------------------------|-----------|-----------|----------|
| `particleEffectAppearance::ctor` | swg/appearance/appearance.cpp:43 | 0x007A85A0 | `__thiscall(ParticleEffectAppearance*, swgptr tmpl)` real entry | Particle appearance live hook |
| `particleEffectAppearance::render` | swg/appearance/appearance.cpp:44 | 0x007A8A50 | real entry (5-byte detour) | Particle render hook |
| `skeletalAppearance::addShaderPrimitives` | swg/appearance/skeleton.cpp | 0x007E6C50 | real entry | Skeletal appearance preview |
| `skeletalAppearance::render` | swg/appearance/skeleton.cpp | 0x007C8B60 | real entry | Skeletal appearance preview |
| `skeletalAppearance::getDisplayLodSkeleton` | swg/appearance/skeleton.cpp | 0x007CA130 | `&fn` | Skeletal LOD read |
| `renderWorld::addObjectNotifications` | swg/scene/render_world.cpp | 0x007664F0 | real entry | Appearance-in-world render path |
| `renderWorld::render` | swg/scene/render_world.cpp | 0x00766DE0 | real entry | Appearance-in-world render path |
| `shaderPrimitiveSorter::*` | swg/graphics/shader.cpp | 0x00773E39 | real entry | Render sort hook |
| `bloom::preSceneRender` | swg/graphics/post_processing.cpp | 0x0064B500 | real entry | Post-processing preview |
| `bloom::postSceneRender` | swg/graphics/post_processing.cpp | 0x0064B560 | real entry | Post-processing preview |

> `particleEffectAppearance::render` (hkRender) also reads several SWGEmu render globals
> (`0x1922F8C` static-shader, `0x1945AD4`/`0x194596C` transform/scale, `0x1945A0C` extent draw arg).
> Advertise those as `(void*)&g` globals too, or — preferred — let the consumer drive the draw via the
> already-advertised `graphics::*` statics and skip raw-global advertisement. Coordinate the shape.

**(ii) A cooperative particle-retrigger entry** — the real value of the Effects editor is hot-reloading
a just-saved `.prt`/client-effect and seeing live scene instances restart. The reachability spike
(`particle_preview.h`) found **no clean reachable native retrigger**: `ClientEffectManager` holds the
live instances (`m_particleSystems`) but its add/remove/list surface is **private**, and there is no
public enumerate-and-restart entry. Provider deliverable:

- Expose a static `__cdecl` entry, e.g. `Utinni_RetriggerClientEffect(const char* logicalName)`, that
  walks `ClientEffectManager::m_particleSystems`, and for each instance whose template matches
  `logicalName` does the `AppearanceTemplateList::fetch(logicalName)` + `ParticleEffectAppearance::restart()`
  cycle (the engine path the spike identified). Advertise it as `particlePreview::retrigger`.
- **Threading/lifetime contract:** game-thread-only; called ONCE per save/reload (never per frame);
  must NOT allocate on any per-frame path (the `project_rh_snapshot_no_heap_alloc` lesson — a per-frame
  `std::vector::reserve()` previously fragmented SWG's allocator and crashed scene change at `0x0051fb0a`).
- Consumer side already has the stable seam: `utinni::ParticlePreview::{isRetriggerAvailable,
  retriggerLiveEffectInstances}` (`particle_preview.h`) with the `ParticlePreviewResult` enum. The real
  hook drops in behind that exact signature; the editor's "Preview in client" button + reload-candor
  badge light up automatically once `isRetriggerAvailable()` returns true.

### 2.C — Virtual methods (consumer-preferred: vtable-resolve; provider thunk OPTIONAL)

These are vtable slots, not stable function addresses. **Preferred resolution is consumer-side** (Utinni
resolves off the live vtable, exactly as it does for D3D9 `Present`) — so the provider does **not**
strictly owe a row. Listed here only so the provider knows they are intentionally NOT in the `.inc` and
need no action unless a provider thunk is later judged cleaner:

- `object::addToWorld` (0x00B225F0), `object::removeFromWorld` (0x00B22680), `object::setParentCell`
  (0x00B22C30) — Object editor in-world placement.
- `cui::io::processEvent` (vtable) — INPUT event editor.

> Decision owed (low priority): confirm "consumer vtable-resolves these" so we close the row out of the
> required-set permanently, OR provider supplies a non-virtual thunk that calls the virtual. Default =
> consumer-side, no provider work.

### 2.D — Mid-function cooperative toggles  (JOINT decision)

Each of these is a Utinni patch *into the middle* of a function body at a Pre-CU instruction offset —
**not expressible as a function pointer at all**, and it does not port to the from-source build by
address. For each, the provider either exposes a small cooperative **boolean engine toggle** (a real API
that achieves the same effect), or both sides **accept the feature is SWGEmu-only** until ported.

| Feature | SWGEmu patch site | Cooperative toggle the provider could expose | If declined |
|---------|-------------------|----------------------------------------------|-------------|
| Chat-context routing (Issue #11) | chat-context NOP | a real **"modal chat context" setter** (`setModalChat`/`getModalChat`, also §2.E-statics) | chat-routing fix SWGEmu-only |
| Enable offline/editor scenes | `cuiMisc::patch` 0x00C8D250 / 0x00C7D57F / 0x009CC385 | an **"enable offline scenes"** flag | offline-scene shortcut SWGEmu-only |
| Debug-camera input passthrough | `debugCamera::patch` NOP 0x0051AA8D | a **"debug-camera input passthrough"** flag | free-cam input-suppress SWGEmu-only |
| Crash-log inline hook | `client::midCrashLogWrite` JMP 0x00A9F766 | (covered by the optional crash-log-dir setter, §2.F) | crash-log capture SWGEmu-only |

Also the **file-local config statics** (a true mechanism gap, not mid-function): `config::setModalChat`
(0x00910A70) / `config::getModalChat` (0x00910D40) have internal linkage in the exe → not addressable
from `engine_advertise.cpp`. They are already NAMED in the `.inc` (config group) but unresolved on the
advertised client. Provider adds a one-line **external-linkage shim** over each, then they bind. (These
double as the Issue #11 cooperative chat-context setter above.)

### 2.E — WS-5: scene-ready callback for engine-INITIATED scene changes  (off critical path)

Needed ONLY for scene changes the **engine** initiates (the editor's `game::loadScene` does NOT need it —
that path is synchronous and the consumer fires its own notification after the call returns). Hand the
provider a **registered-callback** mechanism, NOT another short forwarder to detour (the trampoline-length
problem that forced the original `setupScene` skip applies to any short thunk):

- Provider exports `extern "C" __declspec(dllexport) void __cdecl Utinni_RegisterOnSceneReady(void (__cdecl* cb)(const char* terrain, const char* player, int sceneId))`
  and invokes the registered `cb` from **all** `_startScene` success paths (after full SceneCreator
  integration — NOT mid-`_setScene`, which is not the lifecycle boundary; proven by the v6 `loadScene` work).
- **Contract:** game-thread-only; fired once per completed scene load; not reentrant; not fired
  mid-mutation; null `cb` unregisters. Args best-effort (pass what is available; `nullptr`/`-1` for
  unknown).
- This is a NAME-equivalent capability add → version bump + `.h/.inc` re-sync. Draftable/sendable anytime;
  it gates nothing in buckets A–D.

### 2.F — Optional: crash-log/minidump parity  (low value, non-blocking)

`client::writeCrashLog` (0x00A9F640) and `client::setupStartDataInstall` (0x00A9F970) are **NONEXISTENT**
in the provider tree (grep = 0). The consumer has already re-gated around their absence, so this is not
blocking. If crash-log/minidump parity is wanted, the provider exposes a cooperative **crash-log-dir
setter** (and the `client::midCrashLogWrite` inline hook from §2.D rides it). `client::wndProc` and
`client::writeMiniDump` are already advertised (external-linkage shims) — no action.

---

## 3. Mechanics (unchanged from the SPEC — restated for convenience)

- **Per name add:** bump `ENGINE_HOOKPOINTS_VERSION` (currently **6**) by +1 per wave; add the
  `ENGINE_HOOKPOINT(group, name)` line + the hand-written `&fn` row in `engine_advertise.cpp`; re-copy
  `engine_hookpoints.{h,inc}` **byte-identical** into `D:/Code/Utinni/UtinniCore/swg/` and **sha256-verify**
  the two repos match. Coverage self-check (`utinni_verifyNoNullNoDup()`) must stay green for the grown
  required set.
- **Graceful degradation:** a name the consumer can't find logs + degrades that one editor; it never
  crashes. So partial waves are safe — ship buckets in any order.
- **No SWGEmu regression:** the hardcoded-RVA path is untouched; advertisement lights up only when the
  export is present. The existing SWGEmu Pre-CU D3D9 live-smoke must still pass.
- **Live-smoke gate:** every advertised-client unlock lands only after a maintainer live inject-smoke
  (ABI/ASLR/embed/render hazards can't be caught headless). Decompose by **editor workflow**, one slice
  per smoke.

## 4. Suggested priority order (by editor workflow value)

1. **Effects editor live preview** (§2.B) — the next Wave-2 editor in the priority order
   (Terrain ✅ → **Effects** → Animation → Shaders → Sound → UI); basic edit/save already works, so the
   render-group + retrigger entry is the whole unlock.
2. **Chat editor** (§2.A `cuiChatWindow::ctor` + §2.D modal-chat setter) — highest-touch, named in 3 prior docs.
3. **Free-cam / debug-camera** (§2.A `debugCamera::alter` + §2.D passthrough flag) — unblocks camera-driven editing.
4. **World-pick / HUD** (§2.A `cuiManager::findObjectUnderCursor`, `cuiHud::*`, `creatureObject::setTarget`) — Object/Snapshot editor interaction.
5. **System-message / input diagnostics** (§2.A `systemMessageManager::*`, `messageQueue::*`) — Issue #11 lineage.
6. **WS-5 scene-ready callback** (§2.E) — anytime; off the critical path.

## 5. Acceptance ("done")

A bucket is done when: the new names export undecorated (`dumpbin /exports stage/SwgClient_r.exe`), the
consumer resolves them non-null/non-dup, the `.inc`/`.h` are sha256-identical across both repos, the
coverage self-check is green for the grown required set, the dependent editor workflow functions under a
maintainer live smoke, and the SWGEmu D3D9 smoke still passes. **Full parity** (per coverage-status §5):
no subsystem `isAdvertisedClient()`-skipped except the documented bucket-D mid-patch features that both
sides accepted as SWGEmu-only.

## 6. Anchors

- Contract + mechanism: `24-ENGINE-ENTRYPOINT-ADVERTISEMENT-SPEC.md` (§4 single-source, §5 export
  mechanics, §6 full catalog, §7 acceptance).
- Mechanism-bucket taxonomy: `24-ADVERTISED-CLIENT-COVERAGE-STATUS.md` §2.
- Current contract: `UtinniCore/swg/engine_hookpoints.inc` (names) + `engine_hookpoints.h`
  (`ENGINE_HOOKPOINTS_VERSION 6`).
- Consumer gating model: `UtinniCore/utinni.cpp` `createDetours()` — bucket-B rows live in the
  `if (!advertised)` RENDER sub-gate (lines 247-254); bucket-A in the wholesale MISC/INPUT skips.
- Particle-preview seam: `UtinniCore/swg/scene/particle_preview.{h,cpp}` (the §2.B-ii drop-in surface).

---

*Generated for Utinni Phase 24. RVA columns are reproduced from a UtinniCore source sweep for
identification only — the live UtinniCore source governs the SWGEmu addresses; the Name + signature-source
governs the `&fn` mapping.*
