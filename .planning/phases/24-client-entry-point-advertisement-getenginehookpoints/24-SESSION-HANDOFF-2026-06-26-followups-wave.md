# Phase 24 — Session Handoff (2026-06-26): advertised-client editor-unlock FOLLOW-UP WAVE

Resume pointer for the advertised-client editor-unlock arc. This session knocked out the bulk of the
WS-0..WS-4 follow-up wave. **Live planning pointer remains `24-FOLLOWUPS-PLAN.md`** (now mostly ✅);
the Terrain slice has its own `24-WS4-TERRAIN-SLICE-SCOPE.md`. Read this top-to-bottom before resuming.

---

## 0. STATUS in one paragraph

The advertised DX11 client (`SwgClient_r.exe`) boots, loads scenes in-world from TJT, and now drives the
**Terrain editor's live `.trn` reload** — the Wave-2 headline editor working in the real client. This
session: confirmed **WS-2** (Enter-mask armed), and landed **WS-4** as three editor slices — `report`,
`CuiManager` render-split, and **Terrain reload** (latch + native export). All smoke-validated by the
maintainer, no regressions. Both repos clean + pushed. Two reusable patterns were proven and saved (see §4).

## 1. What landed this session (all committed + pushed)

**Utinni (`master`, …→`600ba6f`):**
- `d168d1d` WS-0 — lift `DirectInput::detour()` onto both targets (per-target split; Enter-mask staged-disarmed behind `[Editor] advertisedEnterMask`).
- `ba46f05` WS-1 — advertised scene-change notify shim + scene-active RVA guards (`playerObject::{getSpeed,setSpeed,teleport}`, `GroundScene::get`) + `g_editorSceneLoaded` flag; `hkMainLoop` fail-closed.
- `07f3b0d` WS-4 slice 1 — `report::detour` lifted onto advertised (advertised-clean, pure pass-through+log forward).
- `3c3bd5c` WS-4 slice 2 — `CuiManager` render-split (advertised-clean `render` via `installable()`; unadvertised `findObjectUnderCursor` gated off via `isAdvertisedClient()`).
- `81551e6` WS-4 Terrain **probe** — `groundScene::update` detour on advertised; validated it fires with a stable `pThis`.
- `2647c67` WS-4 **Terrain reload** — per-frame `GroundScene*` latch from `hkUpdateLoop` + `utinni_reloadCurrentTerrain()` export + `hkCleanupScene` clear + `Native.cs` P/Invoke.
- docs: `499e16c`/`9a46b6c`/`d26b6aa`/`7d39f1d`/`751f5db`/`600ba6f` (plan/scope/status).
- WS-3 (`9f476cd`/`aaae8b1`/`f74b6ca`) landed just before this session — RVA-safety audit infra + `world_snapshot` sweep + CI ratchet.

**UtinniPlugins (`master`, …→`9520d0e`):**
- `3f1c811` WS-1 — skip FreeCam/Player `OnSetupSceneCallback` poll loops on advertised (they'd spin forever once WS-1 fires setup callbacks).
- `9520d0e` WS-4 — route terrain reload through the `utinni_reloadCurrentTerrain` export.

## 2. The follow-up wave status (vs `24-FOLLOWUPS-PLAN.md`)

- **WS-0 (DI lift) ✅ · WS-1 (notify shim + guards) ✅ · WS-2 (Enter-mask armed) ✅ · WS-3 (audit infra) ✅**
- **WS-4 (MISC/editor slices) — ongoing, 3 done:** `report` ✅, `CuiManager render-split` ✅, **Terrain reload ✅**.
- **WS-5 (provider scene-ready callback) — not started, off critical path** (only needed for engine-INITIATED scene changes; editor `loadScene` doesn't need it).

## 3. Open follow-ups / known issues (none blocking)

- **Next WS-4 slices:** other editors (Snapshot, Effects/ClientEffect, Object, chat) each get their own slice
  on the SAME proven idioms (§4). Decompose by EDITOR WORKFLOW. Each is maintainer-live-smoke-gated.
- **Cosmetic — magenta env flash on terrain regen:** on reload the sky/environment briefly flashes magenta
  then fades to correct colors (environment/shader not re-applied instantly during regen). Not a fault.
- **RESID-03 (pre-existing):** whether a *saved* `.trn` edit RENDERS depends on the loose-override searchPath
  (disabled after the 06-12 phantom-walk). The WS-4 reload PATH works; edit-visibility is a separate config item.
- **Transient `nvwgf2um` (NV D3D11 driver) crash** — one-off during a Naboo load earlier in the wave; not
  reproducible, render/driver-layer, not a consumer regression. (See WS-1 banner in the plan.)
- **Space-terrain load** fatals in `SwgCuiHudSpace` on a missing `buttonEnterSpace` UI element — a provider
  space-HUD asset gap, untested path, not a Utinni bug. (WS-4 banner in the plan.)

## 4. Reusable patterns proven this session (also in memory)

- **Don't flip a shared accessor on the advertised client.** `GroundScene::get()` is called by many editor
  poll loops (FreeCam/Snapshot/GroundSceneImpl) started by the WS-1 setup-scene callbacks; making it non-null
  wakes them ALL into unadvertised Tier-2 RVA reads (Codex+Cursor flagged HIGH). Prefer a **dedicated native
  export + per-frame instance latch** (consumer-only, zero blast radius) over widening the shared accessor.
- **Probe-first** for any "does this advertised real-entry fire?" assumption: install the one detour + a
  rate-limited log, change nothing else, smoke. Only build on it once it's proven (the Terrain probe `81551e6`).
- **Gating idiom rule:** advertised-CLEAN row → `installable()` gate (resolver rebinds it → authoritative);
  UNADVERTISED literal → `isAdvertisedClient()` gate OFF (`installable()` is necessary-NOT-sufficient — wrongly
  passes on a stale RVA that landed on relocated code → DetourXS corruption, the CuiStringIds crash class).
- **Lift shape:** pull `subsys::detour()` out of `utinni.cpp`'s `!skip* && !advertised` block into its own
  `if (!skip*)` block; do the per-detour split INSIDE the `detour()` (mirrors DirectInput/CuiManager/GroundScene).
- **Adversarial review (Codex + Cursor) earns its keep** on detour/accessor changes — it caught the blast
  radius the first Terrain scope missed. Use it before building on a risky assumption.

## 5. OPERATIONAL FACTS

- **Build (native):** `MSBuild Utinni.sln -t:UtinniCore -p:Configuration=Release -p:Platform=x86 -m -nologo -v:minimal -nodeReuse:false`
  (MSBuild at `…/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe`; use `-` switch syntax in Git Bash).
  Targets: `UtinniCore`, `UtinniCore_Tests`, `UtinniCoreDotNet`; TJT plugin via `"/d/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolbox.sln"`.
  **Always `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs`** after a UtinniCore build (CppSharp regen churn).
- **Headless gates (run all before commit):** `bin/Release/UtinniCore.Tests.exe "[endpoints]"` (357 assertions / 10 cases);
  clang-format-20 (`…/VC/Tools/Llvm/bin/clang-format.exe --style=file --dry-run --Werror`); WS-3 audit ratchet
  `scripts/audit-advertised-rva-safety.ps1` (322 sites baselined; `scripts/advertised-rva-baseline.tsv`).
- **Live smoke = maintainer only.** Launch via Utinni `Launcher.exe` (injects `SwgClient_r.exe`); read `bin/Release/utinni.log`.
  Advertised-client detour/window/render changes **need a live smoke before commit** (ABI/ASLR/embed/render can't be caught headless).
  Bisect knobs: `ut.ini [DebugBisect] skipMisc/skipInput/skipRender`.
- **ut.ini:** `bin/Release/ut.ini` — `swgClientName=SwgClient_r.exe`, `swgClientPath=D:\Code\swg-client-v2\stage\`.
  Switch to SWGEmu D-00 target by setting `swgClientName=SWGEmu.exe` / `swgClientPath=D:\SWGEmu-Client\SWGEmu\`.
- **TJT editors read a SEPARATE config — `bin/Release/Plugins/TheJawaToolbox/settings.ini`, NOT `ut.ini`**
  (`Plugin.cs:55` `new UtINI(<plugin dir>\settings.ini)`). The advertised client loads `.tre`/`.toc` from
  `D:\Code\SWGSource Client v3.0\` (4 `.toc` + 209 `.tre`, per `stage/client.cfg`), NOT the stage dir — so the
  TRE Browser needs `[TreBrowser] clientDir = D:\Code\SWGSource Client v3.0` in settings.ini (already set).
  `FormTreBrowser` `ini.Load()`s on every open → close+reopen the browser picks up changes (no full relaunch).
- **Symbolizing crashes:** `llvm-symbolizer.exe --obj=/d/Code/swg-client-v2/stage/SwgClient_r.exe <0x400000+rva>`;
  the engine writes `stage/SwgClient_r.exe-unknown.0-<ts>.{mdmp,txt}` (the `.txt` has the FATAL line + exception
  addr); `cdb.exe` (x86, `…/Windows Kits/10/Debuggers/x86/`) with `-z <dump> -c ".ecxr;kb"` for the faulting stack.
- **Cross-repo:** standing authority to edit/commit/push `D:/Code/UtinniPlugins`; paired commits need no checkpoint
  (only the live smoke does). Push pre-authorized for both repos during CI iteration.

## 6. THE BIG PICTURE

This is the advertised-client editor-unlock milestone: making the real DX11 SWG client drive Utinni's editors
the way SWGEmu does. The foundation (scene load, render, input) + three editor slices (`report` diagnostics,
`CuiManager` render coordination, **Terrain live-reload**) are in. Each remaining editor is its own per-workflow
slice on the proven latch/export + gating idioms, gated by a maintainer live smoke. The provider↔consumer
contract pattern and the RVA-safety guardrails are mature; the work is now incremental editor-by-editor.
