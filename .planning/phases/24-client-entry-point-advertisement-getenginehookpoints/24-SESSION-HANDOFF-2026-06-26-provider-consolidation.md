# Phase 24 — Session Handoff (2026-06-26 PM): PROVIDER-SIDE CONSOLIDATION

Resume pointer for the advertised-client editor-unlock arc. This session did NOT write consumer code —
it **consolidated every outstanding swg-client-v2 provider deliverable into one handoff** and handed it
off, because the next editor slices are provider-gated. Read this top-to-bottom before resuming.

**Live pointers after this session:**
- **Provider work:** `24-PROVIDER-HANDOFF-outstanding-editor-unlock.md` (the single ledger; committed `ac6253a`).
- **Consumer plan:** `24-FOLLOWUPS-PLAN.md` (WS-5 section now points at the provider handoff).
- **Prior session:** `24-SESSION-HANDOFF-2026-06-26-followups-wave.md` (the WS-0..WS-4 wave that preceded this).

---

## 0. STATUS in one paragraph

The advertised DX11 client boots, loads worlds in-world from TJT, and drives the Terrain editor's live
`.trn` reload (all from the prior wave, smoke-green). This session established — via a full inventory of
the 10+ `24-PROVIDER-*` docs — that **nearly every past provider ask is already RESOLVED**, and that the
**remaining editor unlocks are blocked on NEW provider work**, not consumer work. I authored a single
consolidated provider handoff enumerating exactly what's left (6 mechanism buckets), committed + pushed
it to Utinni `master`, and dropped a copy into the provider's repo handoff dir. A paste-ready provider
prompt is in §5. **No consumer code changed; nothing needs a live smoke this session.**

## 1. What landed this session (committed + pushed)

**Utinni (`master`, …→`ac6253a`):**
- `ac6253a` — **`24-PROVIDER-HANDOFF-outstanding-editor-unlock.md`** (the consolidated ledger) + a pointer
  from `24-FOLLOWUPS-PLAN.md`'s WS-5 section to it.

**swg-client-v2 (provider repo) — UNCOMMITTED drop-off:**
- Copied the handoff to `D:/Code/swg-client-v2/.planning/handoff/2026-06-26-utinni-provider-outstanding-editor-unlock.md`.
- **Left uncommitted there** — my standing push authority covers only Utinni + UtinniPlugins, NOT
  swg-client-v2. The provider or the maintainer commits it on that side.

**Memory:** updated `project_phase24_editor_unlock_inflight.md` (description + provider-consolidation block).

## 2. The key finding that drove the consolidation

The next editor in the Wave-2 priority order is **Effects/ClientEffect**. Scoping it surfaced that it's
**provider-gated**:
- The Effects editor's **basic edit + save already works** on the advertised client (pure managed I/O —
  `ClientEffectSaveTargets` → `LooseOverridePath`/`IffWriter`, zero native dependency).
- Only the **live "Preview in client" retrigger** is dark: `ParticleEffectAppearance::detour()` lives in
  the `if (!advertised)` RENDER sub-gate at `utinni.cpp:247-254` because its hooks are **hardcoded SWGEmu
  RVAs NOT in the advertised catalog** → on the advertised client they land on relocated `CuiStringIds`
  static-init code → `0xC0000096`. And `ParticlePreview::{isRetriggerAvailable,retriggerLiveEffectInstances}`
  (`particle_preview.h`) are honest stubs because no clean native retrigger entry is reachable
  (`ClientEffectManager::m_particleSystems` is private).

So finishing WS-4 (Effects) **and** WS-5 both wait on the same provider work → consolidate it, hand it
off, and resume the consumer side per-bucket as the provider delivers.

## 3. What the inventory established (so nobody re-opens settled work)

**RESOLVED** (do not re-ask): render attach, DX11 overlay install, embed-resize crop + in-world CUI
reflow, `CuiMediator::deactivate` null-deref, `game::loadScene` full-lifecycle scene entry, `setupScene`
`_setScene` re-map, the table static-init race (40/96 → full population), `treeFile::enumerateFiles`,
`game::mainLoop`/loop-counter accessors, the MI real-entry re-advertisements (`groundScene::{update,
handleInputMapEvent}`, `cuiChatWindow::{enableTextInput,chatEnterHandler}`), and the EngineHook rename.
The provider's own `…provider-collaboration-STATE.md` (v6, 99 names) corroborates: nothing was pending on
their side — until this handoff.

**OUTSTANDING** (the consolidated handoff, by bucket):
- **A — per-editor real-entry detour rows:** `cuiChatWindow::ctor` (0x00F364B0), `cuiManager::findObjectUnderCursor`,
  `cuiHud::{actionPerformAction,getTarget,update}`, `creatureObject::setTarget`, `cuiRadialMenuManager::update`,
  `cuiMenu::*`, `cuiLoginScreen::{ctor,activate}`, `systemMessageManager::receiveMessage`,
  `messageQueue::{appendMessage,appendMessageData}`, `debugCamera::alter`.
- **B — Effects render/appearance group + retrigger:** `particleEffectAppearance::{ctor,render}`
  (0x007A85A0/0x007A8A50), `skeletalAppearance::*`, `renderWorld::*`, `shaderPrimitiveSorter`, `bloom::*`,
  PLUS a cooperative `particlePreview::retrigger` entry.
- **C — virtual vtable rows** (consumer-preferred, provider thunk optional): `object::{addToWorld,
  removeFromWorld,setParentCell}`, `cuiIo::processEvent`.
- **D — mid-function cooperative-toggle JOINT decisions:** Issue #11 modal-chat setter (+ `config::
  {setModalChat,getModalChat}` external-linkage shims), offline-scenes flag, debug-cam passthrough flag.
- **E — WS-5 scene-ready callback:** `Utinni_OnSceneReady` registered export (engine-initiated scenes; off path).
- **F — optional crash-log-dir setter** (`writeCrashLog`/`setupStartDataInstall` absent in provider tree).

**Discipline per name add:** +1 `ENGINE_HOOKPOINTS_VERSION` (baseline **6**) + sha256 `.inc/.h` re-sync +
maintainer live smoke. **Suggested priority:** Effects → chat → free-cam → world-pick/HUD → sysmsg → WS-5.

## 4. Next actions (resume here)

1. **Hand the provider the prompt in §5** (or point them at the dropped-off doc). Bucket B (Effects) first.
2. **When the provider delivers a bucket** (e.g. v7 with the Effects render/retrigger group + a HANDBACK):
   - Re-sync `engine_hookpoints.{h,inc}` byte-identical into `UtinniCore/swg/` (sha256-verify against the handback).
   - Bind the new names; for Effects: drop the `!advertised` gate on `ParticleEffectAppearance::detour()`
     (utinni.cpp:247-254) — but follow the **gating idiom rule** (advertised-clean → `installable()`;
     never an unadvertised literal); wire `ParticlePreview` to the new `particlePreview::retrigger` export
     (replace the stub so `isRetriggerAvailable()` returns true).
   - Headless gates, then **maintainer live smoke** (the only gate that catches ABI/ASLR/embed/render).
3. **WS-5 is draftable anytime** and gates nothing — the provider can do it in parallel.

## 5. PASTE-READY PROVIDER PROMPT

```
You are the swg-client-v2 provider instance (SwgClient_r.exe / GetEngineHookPoints).
There is new inbound work from the Utinni consumer.

READ FIRST:
  .planning/handoff/2026-06-26-utinni-provider-outstanding-editor-unlock.md
This is the consolidated ledger of all outstanding provider deliverables. It closes
out the resolved 24-PROVIDER-REQUEST-* history and carries forward only per-editor +
joint work. Reconcile it against your own .planning/handoff/2026-06-25-utinni-
provider-collaboration-STATE.md — that STATE says "nothing pending on our side," which
is now stale; this handoff reopens provider work. Update STATE accordingly.

CONTRACT BASELINE: ENGINE_HOOKPOINTS_VERSION = 6 (99 names). Files:
  src/.../shared/engine_hookpoints.{h,inc}  (shared contract)
  src/.../win32/engine_advertise.cpp        (table + thunks + ensureDynamicRowsFilled + export)

DO THIS WAVE — Bucket B (the Effects editor live-preview unlock, top priority):
  1. Advertise the render/appearance detour rows the consumer needs (handoff §2.B-i):
     particleEffectAppearance::{ctor,render}, skeletalAppearance::{addShaderPrimitives,
     render,getDisplayLodSkeleton}, renderWorld::{addObjectNotifications,render},
     shaderPrimitiveSorter, bloom::{preSceneRender,postSceneRender}. Map each name to
     &YourEngineSymbol by REAL CODE ENTRY (not a call-through forwarder — those are
     detour-dead). The SWGEmu RVAs in the doc are identification-only; take &fn.
  2. Add the cooperative particle-RETRIGGER entry (handoff §2.B-ii): a static __cdecl
     Utinni_RetriggerClientEffect(const char* logicalName) that walks
     ClientEffectManager::m_particleSystems and does AppearanceTemplateList::fetch +
     ParticleEffectAppearance::restart() for matching instances. Advertise it as
     particlePreview::retrigger. Contract: game-thread-only, once per save/reload,
     allocation-free on any per-frame path. The consumer's drop-in seam is already
     staged (utinni::ParticlePreview in particle_preview.h).
  3. For the render globals hkRender reads (0x1922F8C static-shader, 0x1945AD4/0x194596C
     transform/scale, 0x1945A0C extent arg): advertise as (void*)&g, OR propose driving
     the draw via the already-advertised graphics::* statics — coordinate the shape.

DISCIPLINE (unchanged):
  - Each NAME ADD bumps ENGINE_HOOKPOINTS_VERSION 6 -> 7. Same-name re-maps do NOT bump.
  - Re-sync engine_hookpoints.{h,inc} byte-identical and provide sha256s in the handback.
  - Keep the 32-bit-only advertise TU. Any new function-call row must be added as a
    {name,0} placeholder AND to the ensureDynamicRowsFilled dyn[] list (or it's null on
    the consumer's pre-resume read); constant (void*)&Symbol rows are fine as-is.
  - Build Release/Win32, /nodeReuse:false, delete SwgClient_r.exe to force relink, grep
    log for "unresolved external symbol" (must be 0), dumpbin for undecorated
    GetEngineHookPoints. Stage the exe.
  - Live inject-smoke is MAINTAINER-side — do not claim the editor works; claim the
    contract is populated + staged and list what the maintainer should smoke.

DELIVERABLE: commit + push to origin/master, then write
  .planning/handoff/2026-06-26-effects-render-retrigger-HANDBACK.md
with: commits, the v7 sha256s, the new names, and the exact maintainer smoke steps
(load a world -> open Effects editor -> edit+save a .prt -> "Preview in client" should
light up and live instances restart). Do NOT touch D:/Code/Utinni.

After Bucket B lands, the remaining buckets (A per-editor rows, C virtual vtable,
D mid-function joint toggles, E WS-5 scene-ready callback, F crash-log setter) follow
in priority order per the handoff §4 — one wave per maintainer smoke.
```

## 6. OPERATIONAL FACTS (carried from the prior handoff; unchanged)

- **Build (native):** `MSBuild Utinni.sln -t:UtinniCore -p:Configuration=Release -p:Platform=x86 -m -nologo -v:minimal -nodeReuse:false`
  (MSBuild at `…/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe`). **Always
  `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs`** after a UtinniCore build (CppSharp regen churn).
- **Headless gates (before any consumer commit):** `bin/Release/UtinniCore.Tests.exe "[endpoints]"` (357
  assertions / 10 cases); clang-format-20 dry-run-Werror; WS-3 audit ratchet
  `scripts/audit-advertised-rva-safety.ps1` (322 sites baselined).
- **Live smoke = maintainer only.** Launch via Utinni `Launcher.exe` (injects `SwgClient_r.exe`); read
  `bin/Release/utinni.log`. Advertised-client detour/render changes need a live smoke before commit.
- **ut.ini:** `bin/Release/ut.ini` — `swgClientName=SwgClient_r.exe`, `swgClientPath=D:\Code\swg-client-v2\stage\`.
  TJT editors read a SEPARATE `bin/Release/Plugins/TheJawaToolbox/settings.ini`; the advertised client
  loads `.tre`/`.toc` from `D:\Code\SWGSource Client v3.0\` (`[TreBrowser] clientDir`).
- **Contract files (provider):** `engine_hookpoints.{h,inc}` (shared, sha256-synced) + `engine_advertise.cpp`
  (table/thunks/export). Consumer: `UtinniCore/swg/engine_hookpoints.{h,inc}` + `endpoints*.{h,cpp}` (resolver).
- **Cross-repo authority:** Utinni + UtinniPlugins = standing edit/commit/push. **swg-client-v2 = NOT** in
  my push authority (the §1 drop-off was left uncommitted there by design).

## 7. THE BIG PICTURE

The advertised-client editor-unlock milestone is now **provider-bound**: the consumer-achievable slices
(scene load, render, input, report, CuiManager render-split, Terrain reload) are in; every remaining
editor needs the provider to advertise its entry points (or expose a cooperative toggle). All of that is
now one document and one prompt away. The work resumes as a per-bucket cadence — provider advertises →
re-sync + bind consumer-side → maintainer smoke — starting with the Effects editor's live preview.
