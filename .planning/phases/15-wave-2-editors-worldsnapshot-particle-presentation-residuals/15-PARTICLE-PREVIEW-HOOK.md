# 15-03 Spike: D-09 Live In-Client Particle Preview (Hot-Retrigger) — Reachability Decision

**Spiked:** 2026-06-07
**Plan:** 15-03 (EARLY SPIKE, Wave 1)
**Requirement:** PROD-W2-PRT (live in-client preview half)
**Open question resolved:** RESEARCH Open Q2 / Assumption A3 — *which native manager holds the live
in-scene effect instances, and is there a reachable entry to re-trigger an effect from its (reloaded)
template under injection?*

> **Provenance.** The format/lifecycle understanding below was obtained by *reading* the read-only
> SOE/Bootprint `swg-client-v2` corpus (`clientParticle`, `clientGame/clientEffect`, `sharedObject`)
> to learn the on-disk class shapes and call graph. **No SOE code or identifiers are copied into
> Utinni.** The Utinni-side artifact (`particle_preview.{h,cpp}`) is original work under MIT.

---

## TL;DR (the honest spike outcome)

**No clean, single reachable hot-retrigger hook exists this phase.** The chosen manager is the
engine-level appearance-template cache (`AppearanceTemplateList`) feeding per-instance
`ParticleEffectAppearance::restart()` — but reaching that path requires native RVAs + a live-instance
enumeration that Utinni has **not** yet established, and the obvious "manager" candidate
(`ParticleManager`) does **not** hold live instances at all.

Therefore 15-03 ships `particle_preview.{h,cpp}` as a **documented no-op stub** that exposes the
stable managed seam `RetriggerLiveEffectInstances(...)` and returns a `NotReachable` status. The
Particle editor (15-06) wires the **degraded tier-(b) badge** candor against that status; the live
smoke (15-08) is **not blocked** waiting on this hook. The full native hook is a scoped follow-on
(see "Path to a real hook" below) once a live `.prt` + injected session confirm the RVAs.

---

## Manager decision: which one holds the live instances?

Two candidates were named in RESEARCH Open Q2. Reading both settles it:

### `ParticleManager` — REJECTED as the instance holder
`clientParticle/.../ParticleManager.h` is a **static debug/config singleton**. Its entire public
surface is global toggles (`setParticlesEnabled`, `isTexturingEnabled`, `setDebug*Enabled`, …) plus
`install()`. It keeps **no registry of live `ParticleEffectAppearance` instances** and exposes **no
retrigger / re-create-from-template entry**. It is the wrong object to hook for D-09. (It is named
here to satisfy the decision-coverage gate: `ParticleManager` was evaluated and rejected.)

### `ClientEffectManager` — holds live instances, but the surface is private
`clientGame/.../clientEffect/ClientEffectManager.h` **does** hold the live scene effect instances:
`static ParticleList m_particleSystems;` (a `ManagedParticleSystem*` list), plus `m_lights` and
`m_shaders`. But the lifecycle entries that touch that list — `addManagedParticleSystem(...)`,
`remove()`, `removeAllClientEffectsForObject(...)` — are **`private`** (only the `friend` effect
classes call them). The reachable **public** surface is *play/remove-by-object*:
`playClientEffect(name, object, hardpoint/transform)`,
`removeAllClientEffectsForObjectByLabel(object, label, softTerminate)`,
`removeNonStealthClientEffects(object)`, `sendHeartbeat(timeElapsed)`. There is **no public
"enumerate the live systems and restart them from a reloaded template"** entry.

### The actual retrigger path (engine-level), and why it is not yet reachable
The author-relevant retrigger is two-step and lives **below** both managers:

1. **Reload the template into the cache.** `.prt` is a `ParticleEffectAppearanceTemplate`
   (`createAppearance()` / `static create(name, iff)` / `getTag()`), cached by the engine's
   `sharedObject/AppearanceTemplateList` (`fetch(fileName)` / `fetch(Iff*)` /
   `fetchNew(AppearanceTemplate*)`). A live preview must get the edited `.prt` bytes re-fetched into
   that cache so new appearances pick up the edit.
2. **Re-trigger the live instances.** Each live `ParticleEffectAppearance` exposes a public
   `restart()` (and `setEnabled`, `setPaused`, `setPlayBackRate`) — the per-instance retrigger
   primitive. So the *primitive exists*, but it must be invoked on the instances currently in the
   scene, which are reachable only via `ClientEffectManager::m_particleSystems` (private) or by
   walking scene objects' appearances.

**Why not reachable in 15-03:** Utinni has **no established RVA** for `AppearanceTemplateList::fetch`,
for `ParticleEffectAppearance::restart`, or for enumerating `m_particleSystems`. Establishing those
needs a live injected session + a real `.prt` to confirm addresses (the same empirical RVA discipline
used for `world_snapshot.cpp` / `terrain.cpp` detour tables). 15-03 is an *early, isolated* spike with
**no live session in scope**, so committing speculative RVAs would be a fragile guess — exactly the
"forcing a fragile hook" the objective warns against.

**Chosen manager (for the eventual hook): `ClientEffectManager`** as the live-instance holder, driven
through the engine `AppearanceTemplateList` reload + `ParticleEffectAppearance::restart()` primitive.
`ParticleManager` is rejected. This is recorded so 15-06/15-08 and the follow-on know exactly where to
aim.

---

## Reachable entry vs. no-hook finding

**Finding: NO reachable hot-retrigger entry this phase.** Specifically:
- No public `ClientEffectManager` enumerate-and-restart entry (the list + add/remove are private).
- No Utinni-established RVA for `AppearanceTemplateList::fetch`, `ParticleEffectAppearance::restart`,
  or `m_particleSystems` enumeration.
- `ParticleManager` is not an instance holder at all.

**Consequence (the seam we DO ship):** `particle_preview.{h,cpp}` exports a CppSharp-exposable
`utinni::ParticlePreview::RetriggerLiveEffectInstances(const char* effectName)` returning a status
enum. This phase it returns `NotReachable` (documented no-op stub). The managed/editor side gets a
**stable, named seam now** so the degraded path is exercised end-to-end without churn when the real
hook lands behind the same signature.

---

## Heap-free, marshal-once-per-save/reload plan (`project_rh_snapshot_no_heap_alloc`)

When the real hook is implemented, it MUST obey the heap-free hot-path discipline that
`project_rh_snapshot_no_heap_alloc` records: a per-frame `std::vector::reserve()` in callback dispatch
fragmented SWG's allocator and **crashed scene change at `0x0051fb0a`**. The retrigger therefore:

- **Marshals ONCE per save/reload, never per frame.** The editor calls
  `RetriggerLiveEffectInstances(...)` exactly once on a successful save/reload; it queues a single
  `GroundSceneCallbacks.AddUpdateLoopCall` (managed seam) / `GameCallbacks.AddMainLoopCall` to run the
  reload+restart on the game thread on the next frame, then unsubscribes. No standing per-frame
  callback is installed.
- **Keeps any unavoidable hot path allocation-free.** If a future variant must touch a per-frame
  path, it uses the **stack-allocated fixed-size snapshot** pattern (`kInlineCap=16`,
  `Fn stackSnap[kInlineCap]`) already proven in `ground_scene.cpp`'s `dispatchSnapshot` template —
  lock → copy into a stack buffer → release lock → invoke outside the lock, heap only when N>cap.
- **The stub honors this by construction:** the no-op does zero allocation and installs no callback,
  so the degraded path cannot regress the allocator.

---

## Fallback (the honest degraded-badge candor) — what 15-06 / 15-07 wire

Because the hook is `NotReachable` this phase, the Particle editor degrades honestly (mirrors the
LOCKED CF-05 tier-(b) reload-candor precedent from Phase 11 OT + the `ReloadAssetClassifier` tier-(b)
pattern):

- **`Preview in client` button: state-encoded-disabled.** Enabled ONLY when
  `RetriggerLiveEffectInstances` reports reachable AND `Game.IsRunning`. Otherwise disabled with an
  honest tooltip ("Live preview requires an injected session and a reachable retrigger hook").
- **Reload badge degrades to the locked tier-(b) copy:** `Reloads on next scene change or relog.`
  (NOT the over-promising "Re-triggers live instances on Preview." — that copy is honest only once the
  real hook is reachable). Do not loosen badge copy to over-promise (RESID-03 / D-14 discipline).

This keeps 15-06 (editor) and 15-07 (reload candor) wiring *honest* and unblocks 15-08 (live smoke):
the smoke verifies the degraded path now, and the live retrigger rolls in when the follow-on hook
lands.

---

## Path to a real hook (scoped follow-on, NOT this phase)

For 15-08 (or a later plan) once a live injected session + a real `.prt` are available:

1. With the client injected, establish RVAs for: `AppearanceTemplateList::fetch(const char*)` (and/or
   `fetch(Iff*)`), `ParticleEffectAppearance::restart()`, and a way to enumerate the live
   `ClientEffectManager::m_particleSystems` (or walk scene objects' appearances filtering
   `ParticleEffectAppearance::asParticleEffectAppearance(appearance)`).
2. Wire `particle_preview.cpp` like `world_snapshot.cpp` / `terrain.cpp`: a `swg::particlePreview`
   detour-table namespace of `(pType)0xRVA` function pointers behind the `utinni::ParticlePreview`
   facade (CON-N-01 detour-table pattern; CON-H-03 single-source RVAs via `UTINNI_API`).
3. On save/reload: re-`fetch` the edited template into the cache, enumerate live instances of that
   effect, call `restart()` on each — all inside ONE game-thread `AddUpdateLoopCall`, heap-free.
4. Flip the stub's return from `NotReachable` to `Retriggered{count}`; the editor's button + badge
   light up automatically (same seam). If the `.prt` CppSharp-exposed type churns
   `Generated/UtinniCore.cs`, `git checkout --` it — never commit (`project_utinnicore_cs_regen_churn`).

---

## Decision summary

| Question | Answer |
|----------|--------|
| Live-instance holder | `ClientEffectManager` (`m_particleSystems`) — driven via `AppearanceTemplateList` reload + `ParticleEffectAppearance::restart()` |
| `ParticleManager`? | Rejected — static debug/config singleton, holds no instances |
| Reachable retrigger entry this phase? | **No** — list/add/remove are private; no Utinni RVAs for fetch/restart/enumerate yet |
| Hot-path discipline | Marshal once per save/reload (not per frame); stack-snapshot (`kInlineCap`) if a per-frame path is ever needed (`project_rh_snapshot_no_heap_alloc`) |
| What 15-03 ships | `particle_preview.{h,cpp}` exporting `RetriggerLiveEffectInstances(...)` as a documented no-op stub returning `NotReachable` |
| Fallback (15-06/15-07) | `Preview in client` state-disabled; badge degrades to tier-(b) `Reloads on next scene change or relog.` |
| Real hook | Scoped follow-on behind the SAME seam, once a live session confirms RVAs (15-08 or later) |
