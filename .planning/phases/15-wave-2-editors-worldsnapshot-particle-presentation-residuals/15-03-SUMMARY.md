---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 03
subsystem: native-scene (UtinniCore) + D-09 live-preview spike
tags: [particle, prt, live-preview, spike, native, cppsharp, D-09, PROD-W2-PRT]
requires:
  - "UtinniCore scene-side export pattern (world_snapshot.cpp / terrain.cpp detour-table facade)"
  - "swg-client-v2 clientParticle/clientEffect/sharedObject (read-only format spec)"
provides:
  - "utinni::ParticlePreview::retriggerLiveEffectInstances(...) CppSharp-exposable seam (documented no-op stub, NotReachable this phase)"
  - "utinni::ParticlePreview::isRetriggerAvailable() reachability predicate for the editor's disabled-state + degraded badge"
  - "15-PARTICLE-PREVIEW-HOOK.md: D-09 reachability decision (manager choice + no-hook finding + heap-free plan + fallback)"
affects:
  - "15-06 Particle editor (wires Preview-in-client disabled + tier-(b) badge against this seam)"
  - "15-07 reload candor (honest degraded badge copy)"
  - "15-08 live smoke (not blocked; real hook is a scoped follow-on behind this seam)"
tech-stack:
  added: []
  patterns:
    - "Scene-side static facade (utinni::ParticlePreview) mirroring world_snapshot/terrain export shape"
    - "Documented no-op stub seam to de-risk a greenfield No-Analog hook without forcing fragile RVAs"
    - "Heap-free / marshal-once-per-save-reload discipline (project_rh_snapshot_no_heap_alloc)"
key-files:
  created:
    - "UtinniCore/swg/scene/particle_preview.h"
    - "UtinniCore/swg/scene/particle_preview.cpp"
    - ".planning/phases/15-wave-2-editors-worldsnapshot-particle-presentation-residuals/15-PARTICLE-PREVIEW-HOOK.md"
  modified:
    - "UtinniCore/UtinniCore.vcxproj"
decisions:
  - "Reject ParticleManager as instance holder (static debug/config singleton, no live registry, no retrigger entry)"
  - "Choose ClientEffectManager (m_particleSystems) as the live-instance holder, driven via AppearanceTemplateList reload + ParticleEffectAppearance::restart()"
  - "Honest spike finding: NO reachable hot-retrigger hook this phase; ship a documented no-op stub seam returning NotReachable"
  - "Editor degrades to tier-(b) reload-candor badge (Reloads on next scene change or relog.) + state-disabled Preview-in-client"
metrics:
  duration: ~45 min
  completed: 2026-06-07
  tasks: 2
  files: 3 created + 1 modified
---

# Phase 15 Plan 03: D-09 Live In-Client Particle Preview Hot-Retrigger Spike Summary

EARLY SPIKE that resolved the one greenfield No-Analog item (RESEARCH Open Q2 / A3): there is **no
clean reachable native hot-retrigger hook this phase**, so 15-03 ships a documented no-op
`ParticlePreview` seam returning `NotReachable` + a reachability decision doc, unblocking the Particle
editor (15-06) and the live smoke (15-08) with an honest degraded-badge fallback instead of forcing a
fragile RVA guess.

## What Was Built

**Task 1 — Spike decision doc (`15-PARTICLE-PREVIEW-HOOK.md`, commit `cfe1b1d`).** Read the read-only
`swg-client-v2` corpus to settle the manager question and record the reachability finding:
- **`ParticleManager` rejected** — it is a static debug/config singleton (global toggles +
  `install()`); it holds **no** live-instance registry and exposes **no** retrigger entry.
- **`ClientEffectManager` chosen** as the live-instance holder — it owns
  `static ParticleList m_particleSystems` (the live `ManagedParticleSystem*` list) — but its
  add/remove/list surface is **private**; the public surface is only play/remove-by-object.
- **Actual retrigger path is engine-level and two-step:** reload the edited `.prt` template into the
  `sharedObject/AppearanceTemplateList` cache (`fetch`), then call the public
  `ParticleEffectAppearance::restart()` on the live instances. The `restart()` primitive **exists**,
  but Utinni has **no established RVAs** for `fetch` / `restart` / enumerating `m_particleSystems`, and
  establishing them safely requires a live injected session + a real `.prt` (out of scope for an early
  isolated spike). Committing speculative RVAs would be the fragile hook the objective warns against.
- **Heap-free plan** recorded (cites `project_rh_snapshot_no_heap_alloc`): marshal once per save/reload
  (never per frame); stack-snapshot (`kInlineCap`) if a per-frame path ever becomes unavoidable.
- **Fallback** recorded for 15-06/15-07: `Preview in client` state-disabled; reload badge degrades to
  the locked tier-(b) candor `Reloads on next scene change or relog.` (no over-promise).

**Task 2 — Native stub seam (`particle_preview.{h,cpp}` + vcxproj, commit `11cfc86`).** Created
`UtinniCore/swg/scene/particle_preview.{h,cpp}` exporting a CppSharp-exposable static facade
(`utinni::ParticlePreview`) mirroring the `world_snapshot.cpp` / `terrain.cpp` scene-side export shape:
- `ParticlePreviewResult` plain enum (`NotReachable` / `NotInjected` / `Retriggered`) so the managed
  editor seam branches on reachability without string parsing.
- `retriggerLiveEffectInstances(const char* effectName)` — documented no-op: returns `NotInjected` when
  `Game::isSafeToUse()` is false, otherwise logs once and returns `NotReachable`. Allocates nothing on
  a per-frame path and installs no callback, so it cannot regress SWG's allocator by construction.
- `isRetriggerAvailable()` — returns `false` this phase; the editor uses it to state-encode the
  disabled button + pick the honest degraded badge.
- Registered both files in `UtinniCore.vcxproj` (flat `ClCompile`/`ClInclude` ItemGroups — no per-config
  split, so the `Debug/` gitignore trap does not apply here).
- Built **Release|x86 green** via VS2026 MSBuild (`MSBUILD_EXITCODE=0`). CppSharp regenerated
  `Generated/UtinniCore.cs` (the new exposed type) — reverted via `git checkout --`, **not committed**
  (`project_utinnicore_cs_regen_churn`).

## How to Verify

- Spike doc names a manager: `grep -E "ParticleManager|ClientEffectManager" 15-PARTICLE-PREVIEW-HOOK.md` → `DOC_OK`.
- Export present: `grep -E "[Rr]etrigger" UtinniCore/swg/scene/particle_preview.*` → matches `retriggerLiveEffectInstances`.
- Registered: `grep particle_preview UtinniCore/UtinniCore.vcxproj` → 2 hits (ClCompile + ClInclude).
- Build: VS2026 MSBuild `-t:UtinniCore -p:Configuration=Release -p:Platform=x86` → exit 0 (verified).
- No `Generated/UtinniCore.cs` change in the commits: `git status --short` on that path is clean (verified).

## Deviations from Plan

**1. [Rule 1 - Bug] Log API signature.** The plan-implied `Log::info("...%s", x)` printf-style call does
not exist — Utinni's logger is `utinni::log::info(const char* text)` (single arg, no formatting). Fixed
by building the message into a small `std::string` (a once-per-save/reload path, explicitly NOT
per-frame, so no hot-path allocation concern) and passing `.c_str()`. Verified by the green Release|x86
build.

No other deviations — the plan's "documented no-op stub if the manager hook is unreachable this phase"
branch is exactly the outcome the spike reached.

## Known Stubs

`particle_preview.{h,cpp}` is an **intentional, documented stub** — this is the spike's expected
deliverable (artifact spec: "Native hot-retrigger export OR a documented no-op stub if the manager hook
is unreachable this phase"). `retriggerLiveEffectInstances` returns `NotReachable` and `isRetriggerAvailable`
returns `false` by design. The stub does NOT prevent the phase goal: it de-risks 15-06/15-08 by giving the
editor a stable seam + an honest degraded path. The real native hook is a scoped follow-on (documented in
`15-PARTICLE-PREVIEW-HOOK.md` "Path to a real hook"), to be wired behind this same signature in 15-08 or
later once a live injected session confirms the RVAs.

## Threat Surface

T-15-05 (DoS — retrigger on the game thread) is satisfied by construction this phase: the stub performs
no game-thread work, allocates nothing, and installs no per-frame callback. No new security-relevant
surface (no network endpoints, auth paths, file access, or schema changes) is introduced.

## Self-Check: PASSED
- FOUND: UtinniCore/swg/scene/particle_preview.h
- FOUND: UtinniCore/swg/scene/particle_preview.cpp
- FOUND: .planning/phases/15-wave-2-editors-worldsnapshot-particle-presentation-residuals/15-PARTICLE-PREVIEW-HOOK.md
- FOUND: commit cfe1b1d (Task 1 spike doc)
- FOUND: commit 11cfc86 (Task 2 native stub + vcxproj)
