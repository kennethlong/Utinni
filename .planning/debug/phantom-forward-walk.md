---
status: investigating
trigger: "Phantom forward-walk in injected SWG client — full dossier at .planning/debug-phantom-walk-dossier.md. Blocks 15-08 Tier-4 smoke. Maintainer at keyboard for live repro."
created: 2026-06-12
updated: 2026-06-12
---

## Symptoms

DATA_START
- **Expected:** Character stands idle after login in a Utinni-injected SWGEmu session (same as vanilla client).
- **Actual:** Character walks forward continuously in EVERY injected session — walk animation in place at character-select, real forward movement in-world. Present from boot of each injected session.
- **Errors:** None logged. utinni.log shows only NONEXCLUSIVE FOREGROUND (0x6) DirectInput cooperative-level requests at char select; no EXCLUSIVE, no rewrite.
- **Timeline:** NOT a Phase-15 regression — maintainer recalls identical walking in the 2026-06-03 live run (12-04 re-run). All native deltas since Jun 3 verified inert by inspection (15-03 particle_preview no-op stub; 15-05 direct_input suppress branch provably never fired; DISCL instrument dates to 2026-05-20 and was present in the Jun-3 run).
- **Reproduction:** Launch via Launcher.exe (injected) against local Core3 (WSL2, login 127.0.0.1:44453), acct `admin`. Repro matrix (live-bisected 2026-06-12):
  - Vanilla SWGEmu.exe launched directly (same acct/character/server): CLEAN — standing
  - Injected, full editor config: walks
  - Injected, TJT disabled (plugin_00=false): walks
  - Injected, enableInternalUi=false: walks
  - Injected, FULL PASSTHROUGH (enableEditorMode=false + UI off + TJT off): walks
  → Synthesizer lives in the BARE INJECTION LAYER: Launcher inject mechanics (EB-FE entry patch / I-cache / resume), passive detours, or the utinni.cfg extra client config. NOT editor host / overlay / TJT.
DATA_END

## Full dossier

See `.planning/debug-phantom-walk-dossier.md` (commit 46018ca) — contains repro matrix, eliminated causes, suspect priority order, utinni.cfg contents, environment state, and banked 15-08 smoke observations.

## Eliminated

- hypothesis: Server-side locomotion state — vanilla showed the same character standing on the same server minutes apart.
- hypothesis: Normal SWG movement sources (W tap, NumLock autorun, Up-arrow, both-mouse-buttons hold/release) — none stop it.
- hypothesis: OS-level stuck keys — GetAsyncKeyState clean for W/A/S/D/arrows/mouse buttons; NumLock toggle had no effect.
- hypothesis: Game controllers — none in HID list (vendor collections only).
- hypothesis: Phase-15 regression — same walk observed 2026-06-03; all native deltas since are inert by inspection.

## Evidence

- timestamp: 2026-06-12 (pre-session, live bisect) — Full-passthrough injected session still walks → cause is in bare injection layer or utinni.cfg, not editor/overlay/TJT.
- timestamp: 2026-06-12 (pre-session) — utinni.cfg contains hidden client debug flag `0fd345d9=1` (source of GOD MODE overlay), `singlePlayerStartLocationX/Z=0`, `groundScene=terrain/lok.trn` among others; vanilla-with-cfg run NOT yet tested.
- timestamp: 2026-06-12 (static analysis) — checked: how utinni.cfg reaches the SWG client. found: utinni.cfg is NOT passed on the SWG command line by the Launcher. Launcher/main.cpp `loadDll(cmdLine)` forwards ONLY Launcher.exe's own argv (main.cpp:357-368) to `CreateProcess` (main.cpp:211); the dossier launch passes no extra args, so cmdLine is empty. The cfg is applied entirely by INJECTED CODE: the `swg::config::detour()` (utinni.cpp:87) hooks SWG's `loadOverrideConfig` (RVA 0x00401000) with `hkLoadOverrideConfig` (config.cpp:59-77), which opens `getPath()+"utinni.cfg"` as an SWG TreeFile and feeds it to `loadConfigFileBuffer` (RVA 0x00A9C6C0). Gated on ut.ini `[UtinniCore] useSwgOverrideCfg`. implication: a vanilla client gets NONE of utinni.cfg's flags by default — so the existing "vanilla CLEAN" baseline did NOT have the cfg loaded. The cfg-vs-injected-code split was never actually isolated.
- timestamp: 2026-06-12 (static analysis) — checked: whether vanilla SWG can load utinni.cfg without injection. found: YES. The SWG engine's `ConfigFile::loadFromCommandLine` (tools/.../ConfigFile.cpp:136) parses the post-`--` command-line string; option 1 (line 154) treats any `@<path>` token as a config file and loads it via `ConfigFile::loadFile`. Same ConfigFile section map the injected loadConfigFileBuffer populates → config EFFECT is equivalent. implication: launching `SWGEmu.exe -- @<path-to>\utinni.cfg` replicates the injected cfg load on a vanilla (un-injected) client. This is the exact missing bisect. NOTE: `@file` resolves relative to the client's working dir, not the Utinni bin folder, so the cfg must be referenced by full path (or copied beside the client).

- timestamp: 2026-06-12 (static analysis) — checked: current `bin/Release/ut.ini` gate state and live cfg contents. found: ut.ini has `[UtinniCore] useSwgOverrideCfg = false` RIGHT NOW, and `swgClientPath = D:\SWGEmu-Client\SWGEmu\`, `swgClientName = SWGEmu.exe`. The live `bin/Release/utinni.cfg` has login filled for local Core3 (`loginServerPort0=44453`, `loginServerAddress0=127.0.0.1`, `0fd345d9=1`, `groundScene=terrain/lok.trn`, singlePlayerStartLocationX/Z=0, preloadWorldSnapshot=false). implication: IF `useSwgOverrideCfg` was already false during the dossier bisect runs, then utinni.cfg was NEVER loaded in ANY of those injected runs (the config.cpp:65 branch is short-circuited) — yet they all WALKED. That would EXONERATE the cfg as the cause and point hard at injected code (suspect #2 input/event detours or #3 inject mechanics), regardless of the vanilla+cfg result. This must be confirmed with the maintainer: was useSwgOverrideCfg true or false during the bisect? The dossier says login worked against Core3, which requires the login host/port to reach the client somehow — if not via utinni.cfg (gated off), then via SWGEmu's own login.cfg/options.cfg, meaning the client already had a working config path independent of utinni.cfg.

- timestamp: 2026-06-12 (orchestrator checks, post-checkpoint) — checked: alternate sources of the god flag + ut.ini state. found: (1) NO `0fd345d9` in any of the SWG client's own cfg files (options.cfg, swgemu.cfg, swgemu_live.cfg, swgemu_login.cfg, swgemu_preload.cfg) → the GOD MODE overlay can ONLY come from utinni.cfg. (2) Template `data/ut.ini` ships `useSwgOverrideCfg = true`; live `bin/Release/ut.ini` reads `false`, mtime 2026-06-12 10:45 (today's restore step) → yesterday's gate state unknowable from disk. (3) Maintainer recall: GOD MODE overlay WAS visible in all four of yesterday's walking injected runs.
- timestamp: 2026-06-12 (LIVE RUN, maintainer checkpoint) — test: injected run via Launcher with `useSwgOverrideCfg = false` (cfg should NOT load). result: WALKS at character select, WALKS in-world, **GOD MODE overlay PRESENT in game**. implication: utinni.cfg is reaching the client through a path that IGNORES the `useSwgOverrideCfg` gate. Either (a) `getConfig().getBool("UtinniCore","useSwgOverrideCfg")` is not returning false (default-true on missing/parse, wrong ini path), or (b) a second load path feeds utinni.cfg to the client (another loadConfigFileBuffer/loadConfigFileString caller, Launcher-side mechanism, CWD effect on SWG's own config search). The cfg is still fully in play as walk cause AND there is a gate-logic bug; yesterday's bisect runs never had the cfg off.

- timestamp: 2026-06-12 (LIVE, maintainer observation) — `/sit` → character sits and STOPS moving; `/stand` → stands and IMMEDIATELY resumes walking. implication: the movement source is a PERSISTENT, continuously-asserted forward-movement input state (autorun-like toggle or held virtual input in the client's input/locomotion state), not a one-shot event — posture change suppresses locomotion, and the still-active source re-engages the moment posture allows. Consistent with a cfg flag that enables an always-on movement mode OR an input-path detour continuously feeding forward motion.

- timestamp: 2026-06-12 (STATIC TRACE — gate-ignoring-load-path audit) — checked: (1) getBool default behavior, (2) ini path resolution, (3) ALL SWG config-loader callers in UtinniCore + Launcher, (4) SWG client CWD under CreateProcess. found:
  (1) `UtINI::getBool` (utini.cpp:667) = `Impl::toBool(getValue(...))`; `getValue` returns "" for a missing section/key; `toBool("")` returns FALSE (utini.cpp:186-194). A missing/unparseable key therefore DEFAULTS TO FALSE, not true. `UtINI::load()` (595-625) also *registers* `useSwgOverrideCfg` default "false" and validates it exists. The live `bin/Release/ut.ini` explicitly has `useSwgOverrideCfg = false` → gate read returns FALSE correctly. NO default-true bug.
  (2) ini path = `path + "ut.ini"` where `path` = the directory of UtinniCore.dll resolved via GetModuleFileNameA (utinni.cpp:277-280,294) = bin/Release. Correct ut.ini, no CWD-relative ambiguity (absolute module path).
  (3) The ONLY injected callers of `loadConfigFileBuffer`/`loadConfigFileString`/`loadOverrideConfig` are in `UtinniCore/swg/misc/config.cpp` (all `tools/...` matches are SOE engine source, NOT in the injected client). There is NO second load path. `hkLoadOverrideConfig` (config.cpp:59-77) is the sole loader of utinni.cfg, and the apply call `loadConfigFileBuffer(data,length)` (line 70) is INSIDE the `if (getBool(...useSwgOverrideCfg) && pFile != 0)` gate (line 65). The unconditional `treeFileOpen` on line 63 only OPENS the TreeFile handle; it does not apply any config. With the gate false, utinni.cfg is opened and immediately discarded, never fed to the engine.
  (4) Launcher `loadDll` (main.cpp:204-211) calls `CreateProcess(swgClientFilename, cmdLine, ... , swgClientPath.c_str(), ...)` — the 8th arg `lpCurrentDirectory` = `swgClientPath` = `D:\SWGEmu-Client\SWGEmu\`. The SWG client's CWD is its OWN install dir, NOT bin/Release, so SWG's native config loader cannot pick up bin/Release/utinni.cfg via CWD. `cmdLine` = Launcher.exe's own argv joined (main.cpp:357-368); the dossier launch passes none → empty → no `-- @utinni.cfg` token.
  implication: ***The static trace FALSIFIES the "gate-ignoring load path" hypothesis.*** With `useSwgOverrideCfg=false` there is NO code path in UtinniCore or Launcher that delivers utinni.cfg to the client. UtinniCore.dll is the Jun-7 build (post-gate; source matches binary).

- timestamp: 2026-06-12 (STATIC — overlay-source re-check) — checked: where `0fd345d9` (GOD MODE key) actually lives. found: `grep 0fd345d9` matches ONLY `D:\SWGEmu-Client\SWGEmu\SWGEmu.exe` (the key string is compiled into the client) — NOT present in ANY .cfg (utinni.cfg has it as a VALUE assignment `0fd345d9=1`; SWG's own swgemu*.cfg/options.cfg do not). SWG's login config comes from its OWN include chain: `swgemu.cfg` `.include`s swgemu_login.cfg (`loginServerAddress0=172.21.29.63`, the WSL IP), swgemu_live.cfg (`skipIntro=true`), swgemu_preload.cfg, options.cfg. implication: the client has a COMPLETE working config independent of utinni.cfg. The "GOD MODE overlay present ⇒ utinni.cfg loaded" inference is NOT airtight: `0fd345d9` is an engine-recognized key, and the overlay could be (a) defaulted-on by the engine build, (b) set by a cfg we haven't audited line-by-line, or (c) genuinely from utinni.cfg via a load path NOT in source (e.g., a stale on-disk utinni.cfg sitting where SWG's CWD/searchPath can see it — but the SWG dir scan found no utinni.cfg there). The cfg-aside LIVE run is the decisive disambiguator and does not depend on resolving this.

- timestamp: 2026-06-12 (LIVE RUN — cfg-aside disambiguation, maintainer checkpoint) — test: bin/Release/utinni.cfg renamed to .bak (physically absent), ut.ini untouched (gate false), injected launch via Launcher. result: **STILL WALKS at character select; "God Mode" yellow text STILL PRESENT in-world** (maintainer confirmed that yellow text is the overlay in question). cfg restored afterward. implication: ***H-B CONFIRMED — utinni.cfg fully EXONERATED.*** BOTH symptoms (persistent forward-walk AND god-mode flag) are produced by injected code with zero cfg input. One injection-side mechanism plausibly flips both: a passive detour corrupting client state/code (precedent: DetourXS explicit-length trap silently corrupts bytes at the hook target — memory feedback_detourxs_explicit_len), or the EB-FE entry-patch/resume mechanics, or a passive detour's side effect. The /sit-/stand evidence says the walk presents as a continuously-asserted forward-movement input state.

## Eliminated (continued)

- hypothesis: utinni.cfg flags cause the walk — cfg physically absent, still walks + god mode. EXONERATED (live, 2026-06-12).
- hypothesis: gate-ignoring utinni.cfg load path — falsified by static trace (sole gated loader, correct getBool=false, CWD=SWG dir, empty cmdline).
- hypothesis: DetourXS explicit-length trap (detLen<minDetLen corruption) — FALSIFIED. detourxs.cpp:90-91 now GUARDS `if (detLen < minDetLen) return nullptr;` (the historical trap is already fixed). Only explicit-length call sites are graphics.cpp:738-742 (DETOUR_TYPE_JMP, detLen=5=minDetLen, passes guard) and they target RUNTIME-resolved D3D9 vtable addresses (directx9.cpp), NOT SWG .text RVAs — cannot corrupt SWG client code. All PUSH_RET hook entries verified clean `55 8B EC` prologues in SWGEmu.exe (no overlap/corruption).
- hypothesis: Launcher EB-FE entry patch corrupts client state — FALSIFIED by static read. main.cpp:225-226 reads original 2 OEP bytes into `oep`; 251-252 writes `EB FE`; 310 restores the EXACT `oep` bytes; FlushInstructionCache both times. Touches ONLY the PE entry-point's first 2 bytes for an injection stall; no god/movement/locomotion state. Pure stall-and-restore.

## Passthrough detour/patch enumeration (STATIC, 2026-06-12)

createDetours() + createPatches() run UNCONDITIONALLY in utinni_init (utinni.cpp:307,310) — NO enableEditorMode/UI/TJT gating. Full set below. DetourXS type abbreviations: PR=DETOUR_TYPE_PUSH_RET (6-byte), JMP5=DETOUR_TYPE_JMP explicit len 5.

INPUT / EVENT / MOVEMENT / LOCOMOTION hooks (prime suspects):
- GroundScene::handleInputMapEvent  RVA 0x0051AA40  PR  (ground_scene.cpp:414) — THE input-map→locomotion handler. hkHandleInputEvent body: log-only(cap40) + debugCamera::processIoEvent ONLY if free-cam active (off in passthrough) + call orig. Pass-through in passthrough.
- GroundScene::update                RVA 0x0051AF10  PR  — per-frame; dispatch callbacks then orig.
- GroundScene::draw                  RVA 0x0051B770  PR
- MessageQueue::appendMessage        RVA 0x00AA6640  PR  (io_win.cpp:99) — pass-through + cap5 log.
- MessageQueue::appendMessageData    RVA 0x00AA6480  PR  (io_win.cpp:100) — pass-through + cap5 log.
- cuiIo::processEvent                RVA 0x0093BD50  PR  (cui_io.cpp:154) — DROPS event only if isKeyboardEnabled==false (default true) else pass-through+log.
- cuiHud::actionPerformAction        RVA 0x00EDBAA0  PR  (cui_hud.cpp:329) — RETURNS FALSE (skips orig) if imgui_gizmo::hasMouseHover() (false in passthrough) else pass-through+log. BEHAVIOR-CHANGER on action path but inert w/o gizmo.
- cuiHud::getTarget                  RVA 0x00BD3E20  PR
- cuiGameMenu::ctor                  RVA 0x00C7D360  PR  (cui_hud.cpp:333) — log-only.
- creatureObject::setTarget          RVA (creature_object.cpp:153)  PR
- debugCamera::alter                 RVA 0x006DA1B0  PR  (debug_camera.cpp:301)

*** UNCONDITIONAL CODE-WRITE ON THE INPUT PATH (top suspect) ***
- debugCamera::patch() (createPatches, debug_camera.cpp:307): memory::nopAddress(0x0051AA8D, 2). Disassembled SWGEmu.exe: bytes at 0x0051AA8D = `32 C9` = `xor cl,cl`, INSIDE GroundScene::handleInputMapEvent (entry 0x0051AA40). Surrounding: `...8B 76 58 | 32 C9(NOP'd) | 85 F6 | 74 43 | 84 C9 | 74 26...` — cl is a flag zeroed by this xor then later `test cl,cl; jz`. NOPing it leaves cl STALE → the `test cl,cl` branch can take the non-zero path every event. Comment claims "enable mouse wheel to debugCamera::alter" but it unconditionally mutates input-map control flow. This is the ONLY unconditional write that sits directly inside the locomotion input handler and the strongest single explanation for a continuously-asserted forward-movement state (consistent with /sit-stops, /stand-resumes).

RENDER hooks (lower priority): Graphics::install/update PR + beginScene/endScene/presentWindow/present JMP5 (graphics.cpp:735-744 — JMP5 targets RUNTIME D3D9 vtable addrs via directx9.cpp, not SWG .text), screenshot PR; renderWorld; shaderPrimitiveSorter; postProcessing pre/postSceneRender PR; skeletalAppearance::render PR; ParticleEffectAppearance::detour (body commented out — no-op).

MISC / UI / lifecycle hooks: config::loadOverrideConfig (cfg EXONERATED), Client (setupStartDataInstall/clientMain/writeCrashLog/writeMiniDump + midCrashLogWrite createJMP), clientWorld, Game (mainLoop/install/setupScene/cleanupScene), CuiChatWindow (ctor/enableTextInput/chatEnter + midCtor createJMP@0x00F36797), CuiManager (render/findObjectUnderCursor), cuiMenu, cuiRadialMenuManager, cuiLoginScreen (ctor/activate — auto-login gated, off), SystemMessageManager, report, treefile, IoWin (empty), cuiMisc::patch (fully gated on enableOfflineScenes=false → no-op).

God-mode audit: NO `0fd345d9`/god/cheat write anywhere in UtinniCore. The key string is compiled into SWGEmu.exe only. With cfg exonerated, the god-mode overlay must come from injected code SIDE-EFFECT (most likely the same input-handler corruption flipping a client debug/state byte) OR an engine default — to be disambiguated by the bisect (does god-mode track the walk when INPUT group is skipped?).

## First bisect build (PRODUCED 2026-06-12, commit 04fa26d)

Scaffolding: ini-gated skip groups in utinni.cpp createDetours()/createPatches(). [DebugBisect] keys default false (=run); set true to SKIP. ONE build (bin/Release/UtinniCore.dll, Jun 12 11:28) serves the whole binary search — no rebuild between runs, just edit ut.ini. utinni.log prints each group's run/SKIP state at init.
- skipInputGroup → GroundScene(draw/update/handleInputMapEvent) + MessageQueue + cuiIo + cuiHud + debugCamera detour + creatureObject + debugCamera::patch (the 0x0051AA8D NOP).
- skipRenderGroup → Graphics + renderWorld + shaderPrimitiveSorter + postProcessing + skeletalAppearance + ParticleEffectAppearance.
- skipMiscGroup → everything else (Client/Game/Cui*/config/treefile/IoWin/cuiMisc::patch).

First cut = skip the INPUT group (highest-value: it is the only group touching the locomotion input path AND contains the sole unconditional input-handler code-write).

## Checkpoint payload (maintainer live run — INPUT-group bisect cut #1)

Build is in place (bin/Release/UtinniCore.dll, commit 04fa26d). ut.ini currently has full editor config; the [DebugBisect] section is absent so default behavior is unchanged. For this run:
1. Add to bin/Release/ut.ini a new section:
   [DebugBisect]
   skipInputGroup = true
   skipRenderGroup = false
   skipMiscGroup = false
2. Launch injected via Launcher.exe against local Core3 (acct admin), same as prior runs.
3. At character select + in-world, observe: (a) does the character WALK? (b) is the GOD MODE yellow text PRESENT?
4. (Optional confirm) check bin/Release/utinni.log for the lines `[DebugBisect] skipInputGroup = SKIP` etc. to confirm the gate took effect.
5. After the run, REMOVE the [DebugBisect] section (or set skipInputGroup=false) to restore full behavior.

Branch logic for next cycle:
- WALK STOPS (stands) → the synthesizer is IN the INPUT group. Next: re-split INPUT — first isolate debugCamera::patch (the 0x0051AA8D NOP) alone by reverting only that patch (cheapest: a follow-up build that skips JUST the patch while keeping INPUT detours), since it is the prime single suspect. If walk persists with patch-only-skipped, bisect the INPUT detours (GroundScene vs MessageQueue/cuiIo/cuiHud).
- WALK PERSISTS (still walks) → synthesizer is NOT in the INPUT detour/patch group. Next: skipRenderGroup=true (INPUT back on) to test render group; then skipMiscGroup. Binary-search the remaining two groups.
- GOD-MODE coupling: note whether the yellow text tracks the walk. If god-mode DISAPPEARS exactly when the walk stops, both share one mechanism (confirms the input-handler-corruption theory). If god-mode persists while walk stops, they are independent and god-mode needs its own sub-investigation (engine default or a separate write).

## Current Focus

hypothesis: The strongest single passthrough-active suspect is debugCamera::patch()'s UNCONDITIONAL `memory::nopAddress(0x0051AA8D, 2)`, which NOPs `xor cl,cl` INSIDE GroundScene::handleInputMapEvent (0x0051AA40), corrupting input-map control flow so a forward-movement command is continuously asserted. Secondary: one of the INPUT-group detours (GroundScene/MessageQueue/cuiIo/cuiHud). DetourXS length-trap and Launcher EB-FE patch both FALSIFIED by static analysis.
test: First bisect cut — skipInputGroup=true (disables all INPUT detours + the debugCamera::patch NOP) via ut.ini [DebugBisect], single build already produced (commit 04fa26d). Maintainer live run observes walk + god-mode.
expecting: If the input-handler NOP / input detours are the cause, the character STANDS with skipInputGroup=true. God-mode text likely also clears if it shares the mechanism.
next_action: CHECKPOINT — maintainer runs with skipInputGroup=true; result (walks/stands × god-mode present/absent) branches the next cycle per the Checkpoint payload branch logic above.

## Environment / session state

- Local Core3 server UP in WSL2 Debian (`screen -r core3`), login port 44453, galaxy row IP 172.21.29.63 == WSL IP.
- bin/Release/ut.ini RESTORED to full editor config (editorMode=true, internalUi=true, TJT plugin_00=true).
- Build: Task-1 assembled injection build (Jun 7 binaries), all suites green.
- Maintainer hotkey gotcha: TJT ToggleFreeCam = Shift+Tab collides with Claude Code permission-mode cycling; permission prompts steal focus from the game.

## Resolution

root_cause:
fix:
verification:
files_changed:
