# Phase 24 — Advertised-Client Editor-Unlock: Follow-Up Wave PLAN

**Date:** 2026-06-25 · **Status:** PLAN (crew-reviewed: Codex + Cursor) · **Owner:** consumer (Utinni)
**Precondition:** in-editor scene load works on the advertised DX11 client (v6 `game::loadScene`,
smoke-green, committed `e99e27c`). This plan knocks out the three open §4 follow-ups from
`24-SESSION-HANDOFF-2026-06-25-editor-unlock.md`.

Crew briefs: `scratchpad/crew-consult-followups.md` (the consult); both reviewers' verbatim replies are
captured in the session. Where they diverged, the resolution is noted inline.

---

## 0. Verified findings that reshape the naive plan

1. **The Enter-mask (`505d2da`) is DEAD CODE on the advertised client.** `DirectInput::detour()` is called
   ONLY from `Client::detour()` (`UtinniCore/swg/client/client.cpp:293`), and `Client::detour()` runs only
   inside `utinni.cpp:144`'s `if (!skipMisc && !advertised)` block. So on the advertised client the
   `DirectInput8Create` detour is never installed → the `GetDeviceData` vtbl[10] Enter-mask is never
   patched. The in-hook advertised-gate never gets to run. **Verified by source read, 2026-06-25.**
2. **Scene-change notification is asymmetric on advertised.** `Game::detour()` IS lifted to run on both
   targets (`utinni.cpp:168-171`) and installs `cleanupScene`, so teardown notifications fire; but the
   `setupScene` detour is skipped (tiny-thunk trampoline corruption) → `hkSetScene` setup-side never fires.
   Editors enable on teardown, never on load. Stale comment to fix: any "setup callbacks fire on advertised"
   note is wrong.
3. **`installable(addr)` is necessary, not sufficient.** It proves committed+executable memory; it does NOT
   catch ABI mismatch, DATA writes to hardcoded addresses, CALL sites to bound-but-wrong symbols, or
   mid-function JMP patches. The "hardcoded-SWGEmu-RVA on advertised" crash class (e.g. `generateHighestId`
   → `worldSnapshotReaderWriter`) lives in this gap and must be audited, not discovered per-crash.
4. **"Terrain first" (editor priority) ≠ first detour unlock.** There is no `terrain::detour`. Terrain
   depends on scene load (done), repository (done), in-world tick, and the snapshot/object paths in
   `world_snapshot.cpp` — which are still largely unguarded hardcoded RVAs. Guards must LEAD any
   terrain/snapshot editor smoke.

---

## 0b. SMOKE 1 OUTCOME (2026-06-25, WS-0 + WS-1 attempt — REVERTED)

First implementation of WS-0 + WS-1 was smoked and **reverted** (not committed). Two confirmed root causes,
both validating the crew's "guards + clamp before input/editor-path unlock" sequencing:

- **WS-1 crashed the scene load.** Firing `setSceneCallbacks` after the advertised `loadScene` switched the
  editor into "scene-active" mode, whose per-frame/refresh code reads **player state** — but the advertised
  editor load passes `customizedPlayer = nullptr` (no valid player object). Symbolized crash:
  `0xC0000005` in `utinni::playerObject::getSpeed()` (UtinniCore.dll rva `0x1D7DD`, READ `target=0x673` off a
  null `this`), firing right after `setSceneCallbacks complete`. **WS-1 is premature**: it lights an editor
  notification path before the player-state reads are advertised-safe. → MUST follow WS-3 guards, and/or only
  fire once a valid player exists (or the editor's scene-active readers null-guard `getPlayer()`/`getSpeed`).
- **WS-0 broke pre-world Enter.** The DI hook installed fine (`DI: patched ... GetDeviceData vtbl[10]`), but
  the Enter-mask drops `DIK_RETURN` **unconditionally** on the advertised client → Enter dead on login and
  character-select (both legitimately need Enter; neither triggers the fullscreen restyle the mask targets).
  The mask needs an **in-world gate**, but there is **no advertised `game::getScene`/`isInWorld` signal**
  (confirmed: not in `engine_hookpoints.inc`) — so gating requires a consumer-maintained scene-active flag.

**Revised sequencing takeaways:**
1. **WS-3 (guards) genuinely comes first** — it is the prerequisite for WS-1, not a parallel nicety. The
   `getSpeed`-on-null-player crash is the same class as `generateHighestId`.
2. **WS-2's WM embed-clamp is independent of the DI Enter-mask** — verify the clamp on its own (no DI hook).
   The Enter-mask is a *separate, later* input slice that needs (a) an in-world scene-active flag and (b) to
   land AFTER the clamp is verified (crew: input is coupled/risky, do it last).
3. WS-0 as "just lift the DI hook" is fine; WS-0 as "arm the unconditional Enter-mask" is NOT — split them.

---

## 1. Workstreams (sequenced)

### WS-0 — Lift `DirectInput::detour()` out of the skipped MISC block  ⟶ PREREQ, consumer-only
Mirror the existing `Game::detour()` lift (`utinni.cpp:168-171`). Add a per-target call so DI hooks install
on BOTH targets, keeping the Enter-mask's existing in-hook advertised+keyboard gate (so SWGEmu is
unaffected). This ARMS the Enter-mask on advertised and is the precondition for WS-2.
- File: `utinni.cpp createDetours()` — pull `DirectInput::detour()` (currently reached via
  `Client::detour()`) up to a standalone call that runs regardless of `advertised`, OR lift the whole
  `Client::detour()` carefully (NO — `Client::detour()` also installs un-advertised hooks; lift ONLY
  `DirectInput::detour()`).
- Cleanest: call `DirectInput::detour()` directly from `createDetours()` (not via `Client::detour()`),
  gate the rest of `Client::detour()` as today.
- Risk: `Client::detour()` may rely on ordering/state that `DirectInput::detour()` assumed. Verify
  `DirectInput::detour()` is self-contained (it installs a `DirectInput8Create` import detour — should be).
- **No live smoke strictly required to land** (SWGEmu path unchanged by the in-hook gate), but folds into
  the WS-2 smoke.

### WS-1 — Consumer-side scene-change notification shim  ⟶ closes §4 item (1) for editor-driven loads
After the advertised `swg::game::loadScene(terrain, avatar)` call returns in `hkMainLoop`
(`swg/game/game.cpp` ~line 402, the per-target branch added by `e99e27c`), fire the same scene-change
notification path that `hkSetScene` fires on SWGEmu (drive `GroundSceneImpl.OnSetupSceneCallback` /
scene-panel refresh). This covers ~95% of the editor workflow (every load is editor-initiated) with ZERO
provider latency. Crew consensus (Codex + Cursor): do this first; the provider contract is for
engine-INITIATED scene changes only and is off the critical path.
- Confirm the notification dispatch is callable from `hkMainLoop` context (game thread) and that firing it
  AFTER `loadScene` returns sees a settled scene (Codex caveat: "callback can fire too early" — `loadScene`
  is synchronous via `setScene(true,…)` so this should be post-integration, but verify the scene is
  renderable at the call site, not mid-`_startScene`).
- Fix the stale "setup callbacks fire on advertised" comment(s).
- Live-smoke-gated (folds into WS-2): load a scene → scene panel refreshes without manual reselect.

### WS-2 — Embed-clamp + Enter-mask live confirm  ⟶ closes §4 item (2)
**After WS-0** (else only half the defense stack exists). Maintainer live smoke:
- Trigger SWG's in-world Enter → confirm Enter-mask now suppresses the fullscreen restyle (was dead code
  pre-WS-0).
- Confirm the `WM_WINDOWPOSCHANGING` clamp ("clamped SWG to embed" log path) fires if fullscreen still
  attempts, + the 250ms watchdog (`PanelGame.ReassertEmbed`) as backstop.
- Note (Cursor): `getSwgWndProcExport()` returns null on advertised (`client.cpp:354-355`) — embed relies
  on the imgui subclass path, not PanelGame WndProc forwarding. Don't assume forwarding works; verify the
  subclass path owns the clamp on advertised.

### WS-3 — RVA-safety audit infra  ⟶ makes §4 item (3) systematic, not whack-a-mole
Build the audit BEFORE unlocking more subsystems. **Scope revised 2026-06-25 after crew review (Codex +
Cursor, near-identical verdict — briefs in `scratchpad/ws3-resolver-design-brief.md`).** Both reviewers said
the original "source-tagged resolver with per-call `slotSafeOnAdvertised` guards" optimizes the one crash
class we have NOT hit (state-2 bound-but-missed) while giving false coverage for the classes we DID hit:
crash A = unbound raw literal (state 3 → static audit + guard sweep), crash B = null OBJECT (not a slot
problem → WS-1 sequencing). A 5th state both surfaced — **resolved-but-wrong** (advertised addr present +
executable but ABI/signature/precondition mismatch, e.g. the `treeFile::detour` hazard) — is NOT catchable
by source tags at all; only the static-audit skip-lists / provider contract catch it. Revised deliverables:
- **Source-tagged resolver → reduced to INIT-ONLY TELEMETRY (no runtime safety layer).** Extend the pure
  `resolve(table, bindings, count, Source* out)` to tag each slot `{Unresolved, Advertised, SwgemuRva}`;
  on the advertised path, one-shot LOG every bound name that ended `SwgemuRva` (drift / version skew) after
  `resolveFromExe()`. NO per-call `slotSafeOnAdvertised`, NO inline call-site wrapping, NO release assert
  (log-and-degrade only — a false assert in a live injected client is its own regression). The static-init
  race the diagnostics chased is provider-fixed (96/96 at init), so init-tagging is authoritative; comment
  that a provider race regression would mislabel.
- **Static audit (CI) — THE authoritative net; must fail hard.** Enumerate every raw `0x[0-9A-Fa-f]{6,}`
  literal + `memory::(read|write|nop|createJMP)` site under `UtinniCore/swg/**`; classify each
  DETOUR | CALL | DATA | PATCH (pattern-scan + patch sites are a SEPARATE axis — classify explicitly, don't
  fold into one bucket); cross-map DETOUR/CALL to `s_bindings[]`/`.inc`. Any unbound literal must be either
  `isAdvertisedClient()`-guarded or on an explicit allowlist (entry = name + reason + scope, and every
  allowlisted use is logged so it can't become a quiet escape hatch), else CI fails.
- **`world_snapshot.cpp` guard sweep — DONE.** Centralized `offlineSnapshotUnavailable()` helper (Cursor:
  "the right shape") + 23 guarded entry points joining the existing `generateHighestId` guard. Built clean,
  clang-format-clean, SWGEmu byte-for-byte unchanged. Folds into the WS-4 smoke (first object op).
- **Advertised-mode test fixture:** extend `endpoints_tests.cpp` — assert `resolve(…, Source*)` tags
  correctly (Advertised for hits, SwgemuRva for non-null-slot misses, Unresolved for null-slot misses) over
  a synthetic table (process-isolated, same pattern as the resolver tests).

### WS-4 — First MISC slice (smallest safe increment)  ⟶ opens §4 item (3)
**After WS-3.** NOT a wholesale MISC drop (already reverted once: `ff7e80e`). Smallest safe slice:
- The DI lift (WS-0) is already part of this.
- Un-skip ONLY a minimal bucket of `installable()`-clean MISC detours — candidates: `config::detour`,
  `report::detour`, `CuiManager::detour` — by removing them from the wholesale `!advertised` block and
  letting per-target `installable()` gate them. **Verify each is contract-clean first** (advertised or
  no-op-on-advertised), do NOT lead with `treefile::detour` (ABI mismatch `installable()` can't catch),
  `cuiChatWindow::ctor` (mid-JMP `0x00F36797`, not advertised), or `Client::detour()` (gated on a
  NONEXISTENT `setupStartDataInstall`).
- Then live smoke. Stop. Each subsequent editor (Terrain via `GroundScene::detour`, then chat, etc.) is its
  own slice on the same pattern, gated by WS-3's guards.
- Decompose by EDITOR WORKFLOW, not by historical subsystem label (both reviewers).

### WS-5 — Provider contract for engine-initiated scene-ready  ⟶ PARALLEL / off critical path
Only needed for scene changes the engine initiates (not the editor's `loadScene`). Hand the provider a
request for a **registered-callback** mechanism, NOT another thunk to detour (the trampoline-length problem
that forced the original skip applies to any short forwarder):
- Preferred: provider exports `Utinni_OnSceneReady(...)` and invokes it from all `_startScene` success
  paths; consumer registers a function pointer. Document: signature (terrain/player/scene id if available),
  threading (game-thread-only), lifetime (valid until unregister/shutdown), reentrancy (not fired mid-
  mutation). This is a NAME add → version bump + `.h/.inc` re-sync + sha256-verify.
- Do NOT detour `_setScene(Scene*)` for scene events (not the lifecycle boundary — proven by the v6 work).
- This can be drafted/sent anytime; it does not block WS-0..WS-4.

---

## 2. Critical path & ordering

```
WS-0 (DI lift) ─┬─> WS-2 (embed+Enter smoke)
                │
WS-1 (notify shim) ──> (folds into WS-2 smoke)
                │
WS-3 (audit infra + world_snapshot guards) ──> WS-4 (first MISC slice + smoke)

WS-5 (provider scene-ready callback) ......... parallel, off critical path
```

- **Do WS-0 + WS-1 together** (both small consumer edits in `utinni.cpp` / `game.cpp`), then one WS-2 smoke
  covers both + the embed clamp.
- **WS-3 before WS-4** — non-negotiable; the audit + `world_snapshot` guards are what make the unlock wave
  safe instead of per-crash whack-a-mole.
- **WS-5 anytime** — draft the provider request now if convenient, but it gates nothing.

## 3. Smoke checkpoints (maintainer-only)

1. **After WS-0+WS-1+WS-2:** load a scene → scene panel auto-refreshes (WS-1); in-world Enter does NOT
   trigger fullscreen-over-editor (WS-0 Enter-mask now live); if fullscreen attempts, clamp log fires (WS-2).
2. **After WS-4:** the first un-skipped MISC slice installs without per-hook crash; the targeted editor
   workflow functions; no SWGEmu-RVA crash on first object op (WS-3 guards hold).

## 4. Risks / traps carried from the crew review

- Mixed scene APIs: keep `loadScene` vs `setupScene/_setScene` clearly separated in code + docs so nobody
  reuses `setupScene` for a full load again.
- Provider callback (WS-5) firing too early → TJT observes half-built scene. Document the fire site.
- INPUT (cuiIo/groundScene virtual detours) needs a vtable-resolve plan or editors come up "half-lit" —
  defer until a slice explicitly needs it.
- Bucket-E patches (Issue #11 chat routing, `debugCamera::patch`, `cuiMisc::patch`) have NO contract
  expression — they stay SWGEmu-only unless the provider exposes engine toggles. Do NOT bundle into the
  unlock waves.
- Treat advertised support as CAPABILITY-based ("which editor workflows work"), not "same editor, fewer
  hooks." A populated Repository makes dormant editor paths hot.
