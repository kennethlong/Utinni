# WS-4 — Terrain Editor Slice on the Advertised Client: SCOPE

**Date:** 2026-06-26 · **Status:** ✅ DONE (smoke-validated) · **Owner:** consumer (Utinni)
**Precondition:** WS-0/WS-1/WS-2/WS-3 done; report + CuiManager render-split slices landed.

> **✅ COMPLETE — 2026-06-26.** Implemented via **Option A (narrowed accessor)** + the probe-first sequence:
> probe (`81551e6`, validated the advertised `update` real-entry fires with a stable `pThis`), then the latch
> + `utinni_reloadCurrentTerrain` export (Utinni `2647c67`) + dispatcher routing (UtinniPlugins `9520d0e`).
> `GroundScene::get()` was kept nullptr on advertised (zero blast radius — no Tier-2 guards needed). Smoke:
> edit a `.trn` field → Save → terrain visibly reloads/regenerates on `SwgClient_r.exe`, repeated, no crash
> (previously a silent no-op). **Cosmetic follow-up (non-blocking):** on reload the sky/environment briefly
> flashes magenta then fades to the correct colors — the environment/shader isn't re-applied instantly during
> regen. Not a fault. Also: whether a *saved edit* renders still depends on the loose-override searchPath
> (RESID-03), independent of this slice.

This scopes lighting up the **Terrain editor's live `.trn` reload** on the advertised DX11 client
(`SwgClient_r.exe`) — the Wave-2 priority editor — following the established WS-4 gating idiom.

---

## 0. The finding that collapses the scope

The Terrain editor (`UtinniPlugins/.../UI/Forms/FormTerrainEditor.cs` + `Saving/ClientReloadDispatcher`)
is **minimal and action-driven**. Verified native surface (agent recon, 2026-06-26):

| Native call | Role | Advertised status |
|---|---|---|
| `Game.IsRunning` | gate the reload | ✅ live |
| `utility.GetWorkingDirectory()` | save-path resolve | ✅ live |
| `GameCallbacks.AddMainLoopCall()` | queue reload on game thread | ✅ live (v2.0+) |
| `GroundScene.ReloadTerrain()` | the reload (`0x0051A4F0`) | ✅ **advertised / BOUND** |
| `GroundScene.Get()` | fetch the instance for the reload | ❌ **nullptr on advertised** |

It registers **ZERO** callbacks (no preDraw/postDraw/updateLoop/cameraChange, no setup/cleanupScene),
and touches **no** weather / time-of-day / camera / freecam / player state. So:

- The unadvertised **`draw` virtual** is irrelevant (Terrain uses no draw-loop callbacks).
- The unadvertised **Tier-2 `Terrain::*` globals** (weather `0x01924B6C`, timeOfDay `0x00B5CBC0/D0`,
  filename `0x019113C1`, TerrainObject ptr `0x01947194`, setWeatherIndex `0x00845C90`) are irrelevant —
  the editor never calls them.
- The reverted `groundScene+cuiChatWindow` unlock's **`std::string` fault** (`ff7e80e`) was attributed to
  **cuiChatWindow** ("its method hooks depend on state its ctor hook publishes, but the ctor is
  unadvertised"); a groundScene-only path does not touch it. The `setPreloadSnapshot` install crash is
  already gated (`fe97a52`). The "editor reads unguarded RVA ~1s after load" class is now covered by the
  WS-1 (player_object / GroundScene::get) + WS-3 (world_snapshot) guards.

**Net: the entire slice = "make `GroundScene::get()` return a valid pointer on the advertised client."**
`ReloadTerrain` (advertised) then works through the existing dispatch path with no other change.

---

## 1. Two ways to supply the `GroundScene*` (the decision)

`GroundScene::get()` currently reads the unadvertised SWGEmu global `0x190885C` → guarded to `nullptr` on
advertised (WS-1). Options to get a real instance:

### Option A — consumer-only latch from `hkUpdateLoop` (RECOMMENDED)
`swg::groundScene::update` (`0x0051AF10`) is **advertised / BOUND** (v3 real-entry, `delta==0` verified) and
its hook `hkUpdateLoop(GroundScene* pThis, …)` receives the live instance every frame. Install **only** the
`update` detour on the advertised client (a surgical subset of `GroundScene::detour`) and latch `pThis`:

- `s_advertisedGroundScene = pThis` in `hkUpdateLoop` (cheap, every frame).
- Clear it in `hkCleanupScene` (already hooked — WS-1 clears `g_editorSceneLoaded` there) so the cached
  pointer never dangles past engine scene teardown.
- `GroundScene::get()` on advertised returns `s_advertisedGroundScene` (nullptr until the first post-load
  update; nullptr again after cleanup — which is exactly correct: reload only valid while a scene ticks).

**Pros:** consumer-only, ships now, no contract bump, no cross-repo round-trip. Surgical — installs the ONE
advertised detour needed and nothing else (skip `draw` unadvertised, skip `handleInputMapEvent` not-needed,
`setPreloadSnapshot` gated). **Cons:** installs a per-frame detour on advertised → the one piece that needs a
live smoke (the advertised `update` real-entry has never been cleanly validated firing — the prior attempt
crashed earlier, in setPreloadSnapshot/cuiChatWindow, before steady state).

### Option B — provider advertises a `groundScene::g_instance` accessor
Mirror the existing `cuiManager::g_instance → getIoWin` / `cuiIo::g_instance → getIoWin` accessor pattern:
provider exports `&<GroundScene singleton accessor>`; `GroundScene::get()` on advertised calls it instead of
reading `0x190885C`. **Pros:** zero detour risk (a pure accessor read), no per-frame hook. **Cons:** provider
round-trip — contract version bump + `.h/.inc` re-sync + sha256-verify; cross-repo latency.

**Recommendation: Option A.** It needs nothing from the provider, is fully reversible, and the only advertised
detour it adds (`update`) is already bound. If the `update` smoke shows trouble, fall back to Option B (the
accessor) — they are not mutually exclusive.

---

## 2. Proposed change set (Option A)

1. **`ground_scene.cpp`** — add a file-scope `std::atomic<utinni::GroundScene*> s_advertisedGroundScene{nullptr}`
   (game-thread write in `hkUpdateLoop`, read in `GroundScene::get()`; relaxed, same pattern as
   `g_editorSceneLoaded`). Set it at the top of `hkUpdateLoop`. `GroundScene::get()` on advertised returns it
   instead of `nullptr`. Clear it in `hkCleanupScene` (game.cpp) — add the one line next to the WS-1 clear.
2. **`ground_scene.cpp` `GroundScene::detour()`** — fix `draw`'s gate from `installable()` to
   `!isAdvertisedClient()` (WS-4 idiom rule: `draw` is an unadvertised virtual; `installable()` is
   insufficient for a stale literal). On advertised install **only** `update`; explicitly skip
   `handleInputMapEvent` there too (not needed for Terrain — minimize surface; revisit when a freecam/input
   editor slice needs it). `setPreloadSnapshot` stays gated.
3. **`utinni.cpp`** — lift `GroundScene::detour()` out of the `!skipInput && !advertised` INPUT block into its
   own `if (!skipInput)` block (honor the bisect flag); behavior-identical on SWGEmu (D-00).
4. **`advertised-rva-baseline.tsv`** — update Reasons for the `update` / `draw` / `0x190885C` rows.

No managed (`.cs`) change — the bridge already calls `GroundScene.Get()/.ReloadTerrain()`.

## 3. Risks / what the smoke must prove

- **Does the advertised `update` real-entry fire per-frame with a valid `pThis`?** The core unknown. Smoke:
  load a ground scene → confirm `hkUpdateLoop` fires (add a one-shot log) and `s_advertisedGroundScene` latches.
- **Does `ReloadTerrain()` on the latched instance actually reload in-session?** Edit a `.trn` field → Save/
  Preview → terrain visibly regenerates (note: per memory, ReloadTerrain regenerates in-session but does NOT
  re-read disk — confirm the in-session regen path holds on advertised).
- **No install-time or per-frame crash** from the `update` detour (setPreloadSnapshot gated; draw skipped).
- **Pointer lifetime:** confirm no stale-pointer fault across a scene change (latch cleared in hkCleanupScene
  before teardown; reload only while `g_editorSceneLoaded`).
- **SWGEmu D-00:** identical behavior (the lift is `!skipInput`-equivalent; latch path is advertised-only).

## 4. Decomposition / follow-ons (NOT this slice)
- Tier-2 Terrain editing (weather, time-of-day, filename display) — guard `Terrain::*` on
  `isAdvertisedClient()` to degrade, or request provider advertisement, when a Tier-2 editor needs them.
- `draw`-driven features (gizmo/preview overlays) — vtable-resolve `GroundScene::draw` (extend
  `vtbl_resolve.h`; MI class, index needs derivation) or provider real-entry, when a slice needs draw-loop.

## 5. Adversarial review — DONE 2026-06-26 (Codex + Cursor, converged)

Both reviewers independently surfaced a **HIGH-severity blast-radius issue the original scope missed**, and
otherwise agreed: the latch is sound on the game-thread happy path, clearing in `hkCleanupScene` is the right
lifetime boundary, `update`-only is materially lower-risk than the reverted bundle, and the `draw` gate MUST
be `!isAdvertisedClient()` (not `installable()`).

### HIGH — flipping `GroundScene::get()` is GLOBAL, not Terrain-scoped
`GroundScene::get()` is a shared accessor; making it non-null on advertised re-enables **every** existing
`GroundScene.Get()` consumer, not just the Terrain reload. The worst is `GroundSceneImpl.cs`'s poll loop:
```
if (GroundScene.Get() != null && Terrain.Get() != null)
    GroundSceneCallbacks.AddUpdateLoopCall(() => scenePanel.UpdateTimeOfDay((int)(Terrain.Get().TimeOfDay*1000)));
```
`Terrain::get()` reads unadvertised `0x01947194`; `TimeOfDay` hits unadvertised `0x00B5CBC0`. Once `get()` is
non-null AND the now-installed `hkUpdateLoop` dispatches `updateLoopCallbacks`, this runs **Tier-2 terrain
reads every frame** — exactly the "unguarded RVA after scene load" class WS-1/WS-3 exist to prevent. Other
exposed consumers: `SnapshotPanel.Name → getFilename` (`0x019113C1`), `FreeCamImpl`, `ObjectBrowser
getCurrentCamera`. **This blast radius applies to Option B too** unless contained. **Containment (required):**
guard the unadvertised `Terrain::*` accessors on advertised (degrade — same shape as the WS-1 player_object
guard); crucially guard `Terrain::get()` → nullptr so the `GroundSceneImpl` poll's `Terrain.Get() != null`
short-circuits and never queues the Tier-2 read. (Terrain RELOAD uses `GroundScene.Get().ReloadTerrain()`, NOT
`Terrain.Get()`, so this does not break reload.)

### Revised sequence (both reviewers' converged recommendation) — PROBE FIRST
1. **Probe (cheap, isolated, the one A-specific unknown):** install ONLY the `update` detour on advertised
   with a rate-limited one-shot log (`pThis`, fire count) and log the bound `update` address at install — but
   do **NOT** change `GroundScene::get()` yet. Smoke: confirm `hkUpdateLoop` fires steadily with a stable
   non-null `pThis` in-world. If it never fires → Option A is blocked → switch to Option B.
2. **If probe passes:** add the latch + flip `get()` + **contain the blast radius** (guard `Terrain::*` Tier-2,
   incl. `Terrain::get()`→nullptr) + null-guard the reload dispatch path (`DequeueMainLoopCalls` has no
   `DispatchSafely` wrapper — a pre-first-update reload would NRE on the game thread).
3. **Smoke:** Terrain edit→Save/Preview→in-session regen (NOT disk re-read), THEN a scene-change + 30s idle
   soak (catches the latch clear + any Tier-2 polling).

### Other flagged items
- Pre-first-update reload NRE: null-guard the dispatcher or `get()` path (no try/catch in the drain).
- `ReloadTerrain` does not re-read disk (smoke verifies in-session regen).
- `skipInput` bisect now also gates the latch (intentional, matches today's SWGEmu behavior).

### A vs B verdict
Both: **Option B** (provider `groundScene::g_instance` accessor) is the cleaner long-term contract (no detour,
authoritative singleton, no first-update window). **Option A** is acceptable as a ship-now consumer probe
**only with the containment above**. They compose — A's probe also de-risks B (proves the engine ticks a
stable GroundScene on advertised).
