# Debug dossier — phantom forward-walk in injected SWG client

> **RESOLVED 2026-06-12** — root cause was NOT injection: stale swg-blender-plugin
> searchPath overrides (`all_m.lat`/`all_b.skt`) in `swgemu_live.cfg` shadowing retail
> animation data. Full record: `.planning/debug/resolved/phantom-forward-walk.md`.
> 15-08 Tier-4 smoke is UNBLOCKED.

> Written 2026-06-12 mid 15-08 live smoke, immediately before a context clear.
> **Feed this to `/gsd-debug` as the issue description.** Maintainer is at the keyboard;
> live repro available on demand. This defect BLOCKS the 15-08 Tier-4 smoke visual checks.

## Symptom

Character walks forward continuously in every Utinni-injected session: walk animation
in place on the character-select screen, real forward movement in-world. Present from
boot of each injected session.

## Repro matrix (live-bisected 2026-06-12)

| Config | Result |
|--------|--------|
| Vanilla `SWGEmu.exe` launched directly (same acct `admin`, same character, same local Core3) | ✅ CLEAN — character standing |
| Injected via `Launcher.exe`, full editor config | ❌ walks |
| Injected, TJT disabled (`plugin_00=false`) | ❌ walks |
| Injected, `enableInternalUi=false` (no imgui overlay) | ❌ walks |
| Injected, FULL PASSTHROUGH (`enableEditorMode=false` + UI off + TJT off) | ❌ walks |

→ Synthesizer lives in the **bare injection layer**: Launcher inject mechanics
(EB-FE entry patch / I-cache / resume), passive detours, or the `utinni.cfg` extra
client config. NOT editor host / overlay / TJT.

## Eliminated

- **Server-side locomotion state**: vanilla showed the same character standing on the
  same server minutes apart.
- **Normal SWG movement sources**: W tap, NumLock (autorun), Up-arrow, both-mouse-buttons
  hold/release — none stop it.
- **OS-level stuck keys**: GetAsyncKeyState clean for W/A/S/D/arrows/mouse buttons.
  (NumLock toggle was ON system-wide; toggling it had no effect.)
- **Game controllers**: none in HID list (Corsair kbd VID_24F0, gaming mouse VID_18F8,
  ASUS VID_0B05 vendor collections only).
- **Phase-15 regression**: maintainer recalls the same walking in the 2026-06-03 live
  run (12-04 re-run). All native deltas since Jun 3 are inert by inspection:
  - `11cfc86` 15-03 `particle_preview.cpp` = documented no-op stub
  - `bf5843d` 15-05 `direct_input.cpp` suppress branch **provably never fired**
    (utinni.log shows only `NONEXCLUSIVE FOREGROUND (0x6)` requests at char select;
    no EXCLUSIVE, no rewrite). PanelGame.cs delta = comments only.
  - DISCL instrument itself dates to 2026-05-20 (`18e79c3`) — present in Jun-3 run.

## Next untested bisect (was staged when debug was requested)

Run the **vanilla client WITH `utinni.cfg` loaded** (replicate Launcher's command line
minus injection) to split *utinni.cfg flags* vs *injected code*:

- `Launcher/main.cpp:211` — `CreateProcess(swgClientFilename, cmdLine, ... CREATE_SUSPENDED ...)`;
  find `loadDll(cmdLine)`'s caller to see how `utinni.cfg` reaches the SWG cmdline.
- `bin/Release/utinni.cfg` contents: `loginServer 127.0.0.1:44453`, `skipIntro=1`,
  **`0fd345d9=1`** (hidden client debug/god flag — source of the GOD MODE overlay),
  `groundScene=terrain/lok.trn`, `singlePlayerStartLocationX/Z=0`,
  `preloadWorldSnapshot=false`, `disableFileCaching=1`, `environmentStartTime=500`,
  `splashTimeoutSeconds=0`, `debugExamine=1`, `loginClientID=Local`.

## Suspects (priority order)

1. **`utinni.cfg` client flags** — `0fd345d9=1` debug/god side-effects on movement input,
   or `singlePlayerStartLocation*` / `groundScene` interactions.
2. **A passive detour corrupting the input/event path** — `hkProcessEvent` /
   `hkWndProcHandler` run even in passthrough; precedent: SWG's CUI key-context selector
   mis-routes keys under injection (memory `project_swg_context_routing`).
3. **Launcher inject mechanics** — EB-FE entry patch + FlushInstructionCache + resume
   timing (memory `project_eb_fe_patch_origin`); DetourXS length trap
   (memory `feedback_detourxs_explicit_len`).

## Environment / session state

- Local Core3 server UP in WSL2 Debian (`screen -r core3`), login port 44453,
  galaxy row IP `172.21.29.63` == WSL IP. Start procedure: Core3 project memory
  `core3-wsl2-start.md`.
- `bin/Release/ut.ini` RESTORED to full editor config (editorMode=true, internalUi=true,
  TJT plugin_00=true).
- Build: Task-1 assembled injection build (Jun 7 binaries), all suites green.

## 15-08 smoke position (resume after debug)

`15-SMOKE.md` all four checklists still unsigned. Banked observations to transcribe:
- A1 ✅ Snapshot panel + `Placements…` button via unchanged Wave-1 MEF seam
- A3 ✅ `Snapshot Placements — naboo` form opens; 5440 placements; filter works (543 theed)
- A4 (partial) ✅ row-select → sidebar Selected Node sync (`shared_streetlamp_naboo_theed_style_1.iff`); gizmo visual NOT confirmed
- RESID-03 WS LOCKED badge verbatim ✅: "Placements re-resolve on the next scene change."
- DEC-A3/D-11 boundary footer verbatim ✅ (Blender-lane sentence)
- TJT Scene▸Load naboo.trn scene change: embed survived, hkSetScene clean (C4 data point)
- OBSERVATION for log: TJT `ToggleFreeCam = Shift + Tab` hotkey collides with Claude Code's
  permission-mode cycling; permission prompts steal focus from the game (made MCP-driven
  UI automation unreliable; fell back to maintainer-drives mode).
- Checklists B (particle editor) and C.1 (DISCL log read) are smokeable even while
  the walk defect is open; A6-A9, C matrix, D need the walk fixed or worked around.
