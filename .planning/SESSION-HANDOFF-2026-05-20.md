# Session Handoff: 2026-05-20

> Written 2026-05-20 to wrap a session that closed Issue #9 (cursor + special keys) and implicitly resolved Issues #7 (Lok second-cycle crash) and #8 (Naboo OOM) via the same Phase B fix. Supersedes `SESSION-HANDOFF-2026-05-19-NIGHT.md` for what's next. SWG now boots end-to-end under Utinni injection: login → character select → in-world (Tatooine confirmed) → mid-session scene swap via TJT (Lok + Naboo confirmed).

## TL;DR

The morning handoff's Issue #9 (cursor invisible + Tab/Del/Return dead on login) is **resolved**. Root cause was the editor-side input/cursor/HWND meddling code still operating on the **old "SWG renders into PanelGame" model** after the 2026-05-19 fixes flipped SWG to the new "SWG owns its own top-level window" model. PanelGame's focus-loss handlers were calling `Client::SuspendInput` on every interaction with SWG's window, unacquiring DirectInput in the process. The cursor was invisible because `Graphics::useHardwareCursor(false)` forced software-cursor mode (a holdover from the old framebuffer-embedded model).

**Bonus**: the same Phase B fix appears to have closed Issues #7 (Lok scene-load second-cycle access violation) and #8 (Naboo terrain "OOM" at ~300 MB) as side effects. Hypothesis: pre-Phase-B, `hkMainLoop` was calling SWG's mainLoop with PanelGame's HWND + size every frame; the per-frame inconsistency was triggering SWG-internal scene/resource re-init cycles. Post-Phase-B, `Client::getHwnd()` returns null, the editor-mode branch auto-skips, and SWG's mainLoop gets called with its own stable HWND.

**Status**: Editor + SWG full pipeline works. User confirmed loading both Lok and Naboo via TJT's "Load Scene" stayed fully playable. Four atomic commits pushed (`f5fa073..64a0eb0` on master). Three follow-up issues effectively closed in one fix.

## What we shipped this session

| Commit | Layer | What |
|---|---|---|
| `f5fa073` | UtinniCore | Phase A diagnostic. Entry/exit logs in `Client::suspendInput`/`resumeInput` (with `Game::isRunning` value), `DirectInput::suspend`/`resume`, and `Client::setHwnd` (rate-limited to HWND-change events). Kept in source after fix — nearly silent during normal play, will instantly flag any future caller. |
| `2f02fad` | UtinniCoreDotNet + UtinniCore | Phase B tactical fix. PanelGame: removed MouseEnter/MouseLeave/MouseMove/GotFocus/LostFocus handlers (the auto-suspend/resume cycling); removed `Client.SetHwnd(Handle)` from `PanelGame_Layout` (was shadowing SWG's real top-level HWND); removed `HideCursor`/`ShowCursor` helpers + `cursorHideCount` field. Kept `Client.SetHInstance` (still needed as hkMainLoop's startup gate). Kept `HasFocus` field pinned to false (FormMain.ProcessCmdKey reads it for hotkey routing). direct_input.cpp: dropped editor-mode cursor side-effects from `hkSetupInstall` (HCURSOR global write + `useHardwareCursor(false)`) — let SWG's default hardware-cursor path stand. |
| `7d467ba` | docs | STATE.md issue #9 marked RESOLVED with full diagnosis chain, fix details, verification result. |
| `64a0eb0` | docs | STATE.md issues #7 + #8 marked LIKELY RESOLVED with hypothesis (per-frame HWND/size inconsistency causing SWG internal re-init) and re-open triggers. |

## Diagnosis chain (brief)

Two threads, both productive:

1. **Phase A architecture mapping** (no code, ~20 min): grep'd for `setHwnd`, `SuspendInput`, `useHardwareCursor`, traced the data flow between `PanelGame.cs`, `client.cpp`, `direct_input.cpp`, `game.cpp`, and `graphics.cpp`. Identified six concrete sites where editor-side code still assumed the old "SWG renders into PanelGame" model:
   - `PanelGame.cs:85` writing `Client.SetHwnd(Handle)` per layout
   - `game.cpp:122-128` `hkMainLoop` substituting `Client::getHwnd()` into SWG's mainLoop call
   - `client.cpp:99-108` `Client::resumeInput()` doing `SetFocus(Client::getHwnd())` — focuses PanelGame, not SWG
   - `PanelGame.cs:78-115` mouse/focus handlers auto-suspending DirectInput on every PanelGame focus loss
   - `PanelGame.cs:132-148` `HideCursor`/`ShowCursor` affecting process-wide Win32 cursor state
   - `direct_input.cpp:73-78` `hkSetupInstall` writing SWG's HCURSOR global + forcing software-cursor mode

2. **Phase A diagnostic capture** (commit `f5fa073`, ~30 min to add + build + user retest): logged the relevant code paths. User's log capture confirmed every PanelGame focus-loss + mouse-leave fired `Client::SuspendInput` **twice** (back-to-back from both handlers), with `Game::isRunning=1` at the login screen. `DirectInput::suspend()` fired the same way. `Client::setHwnd` never logged — meaning the value was set once at PanelGame's first layout (`0x00830342` = PanelGame's handle) and never changed. Smoking gun confirmed.

3. **Phase B tactical fix** (commit `2f02fad`, ~30 min): minimal surgery to stop the editor-side meddling. No new APIs, no new state — just removed code paths that don't apply in the new model. Build clean, push.

4. **Live verification by user**: logged in as a character, typed credentials with mouse + Enter, clicked through character select, loaded into Tatooine. Mouse + keyboard fully functional. Cursor visible. Subsequent test: TJT "Load Scene" → Naboo loaded clean → Lok loaded clean. **Both scenes stayed loaded and fully playable** — implicitly closing Issues #7 and #8.

## What's open after this session

### Pending log capture (low effort, high value)

The user has the post-Phase-B build and successfully loaded both Lok and Naboo. They haven't yet pasted the `utinni.log` from that session. Capturing the log and confirming clean single-cycle `hkSetScene`/`hkCleanupScene` behavior would upgrade Issues #7 and #8 from "LIKELY RESOLVED" to fully RESOLVED.

**Ask**: log lines containing `hkSetScene`, `hkCleanupScene`, `hkMainLoop: loadNewScene`, `hkInstall`. If single-cycle, mark RESOLVED in STATE.md. If extra cycles appear, re-diagnose.

### Issue 10 (still open) — Window reparenting not yet implemented

SWG creates its own top-level window. The plan was `SetParent` + `WS_CHILD` style change to embed it in PanelGame, mentioned in `hkSetupStartInstall`'s log line as "reparent-after-creation model". Not implemented. SWG currently displays as a separate window alongside the editor. Cosmetic / integration-quality issue, not a functional blocker.

**Next steps if pursued**: managed-side hook on `Client::setHwnd` (already exists per `client.cpp`) — when fired, PanelGame should grab the HWND and `SetParent` it into itself. Requires careful coordination with D3D9's focus-window assumption (`cParam.hFocusWindow` from `CreateDevice` is captured by imgui setup at `imgui_impl.cpp:131`) and DirectInput's `SetCooperativeLevel` binding.

### Knock-on cleanup (deferred until #10 decides architecture)

Two editor-mode branches are now dead code, auto-skipping because `Client::getHwnd()` returns null:

- `game.cpp:122-128` `hkMainLoop` editor-mode branch (substitutes `Client::getHwnd()` into SWG's mainLoop call)
- `graphics.cpp:281-300` `hkEndScene` editor-mode resize-tracking branch

Both gate on `GetWindowRect(Client::getHwnd(), &rect)` which fails on null HWND, so they auto-fall-through to the non-editor path. Functional, but the dead branches are misleading to read. Cleanup deferred until Issue #10 decides whether reparenting brings them back to life or formally removes them.

### Issue 1 (still open) — WR-03 exit dialog

Disappears in passthrough-everything builds; reappears with any detour active. Confirms exit dialog is downstream of a Utinni hook interaction with D3D9 lifecycle, not the `cleanup()` UAF we already fixed. Deferred — exit-only nuisance, not blocking.

### Phase A diagnostic logs — kept in source

The logs added in `f5fa073` are kept after Phase B. Rationale: PanelGame stopped the auto-cycling, so the logs are nearly silent during normal play. Any future caller (plugin, new editor code, regression) that touches `Client::suspendInput`/`resumeInput`/`SetHwnd` will instantly show in the log. Useful regression tripwire at very low cost.

## Verification — what the user observed

**Login screen** (post-Phase-B build):
- OS cursor visible, tracking with mouse
- Mouse click on Login button worked
- Enter key submitted credentials
- Tab, Delete, Return all functional in text fields
- No DirectInput suspend/resume cycling in log

**Character select** (post-Phase-B build):
- Mouse click on character → logged into Tatooine

**Mid-session scene swap via TJT Load Scene** (post-Phase-B build):
- Loaded Naboo (was Issue #8 OOM suspect) — stayed loaded, fully playable
- Loaded Lok (was Issue #7 crash suspect) — stayed loaded, fully playable
- UX detail noted: clicking Load Scene briefly shows character select, then loads into the new scene as a "naked character" (offline-style scene replacement, not server-mediated zone change)

## Files referenced this session

- `UtinniCore/swg/client/client.cpp` — Phase A diag logs in `setHwnd`, `suspendInput`, `resumeInput` (commit `f5fa073`)
- `UtinniCore/swg/misc/direct_input.cpp` — Phase A diag logs in `suspend`/`resume` (`f5fa073`); editor-mode cursor side-effects dropped from `hkSetupInstall` (`2f02fad`)
- `UtinniCoreDotNet/UI/Controls/PanelGame.cs` — focus/mouse handlers + `Client.SetHwnd` call + cursor helpers removed (`2f02fad`)
- `.planning/STATE.md` — Issues #7, #8, #9 status updates (`7d467ba`, `64a0eb0`)

### Files read for understanding (no edits)

- `UtinniCoreDotNet/main.cs` — confirmed `Native.SignalLauncherReady` placement in `Startup.EntryPoint`
- `UtinniCoreDotNet/UI/Forms/FormMain.cs` — confirmed `ProcessCmdKey` reads `game.HasFocus` for hotkey routing (informs why `HasFocus` field is kept pinned-false rather than removed)
- `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` — confirmed `AddMainLoopCall` enqueues into `ConcurrentQueue` with `Drain` semantics (i.e., one-shot dispatch — initial TJT-loop theory was wrong)
- `UtinniCore/swg/game/game.cpp` — confirmed `getMainLoopCount()` reads `*(int*)0x1908830`; `Game::isRunning()` returns non-zero once SWG's main loop is ticking, true throughout login + game
- `UtinniCore/swg/ui/imgui_impl.cpp` — confirmed imgui subclasses SWG's WndProc but forwards all messages, so SWG's input dispatch isn't being eaten
- `UtinniCore/swg/graphics/graphics.cpp` — confirmed `useHardwareCursor` / `showMouseCursor` thunk to RVAs `0x00755940` / `0x00755A50`; SWG's default if not called is hardware cursor mode
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/SWG/GroundSceneImpl.cs` — TJT's `Load`/`Unload`/`Reload` route through `GameCallbacks.AddMainLoopCall` (one-shot, correct)
- `D:\SWGEmu-Client\SWGEmu\logs\SWGEmu.exe-stage.119798-20260520022742.txt` — Lok crash dump confirming fault address `0x00b3f620` on `shared_filler_building_corellia_style_02.iff` construction, terrain `lok.trn`, MainLoop=4208, BytesAllocated=93M (not OOM)

## Memory updates

No new memory files written this session — the lessons were specific to the Utinni HWND architecture mismatch and didn't generalize to broader principles worth capturing as standing rules.

Existing memory `feedback_max_harness.md` (max-harness verification preference) was honored: Phase A diagnostic logging was the lightweight harness that confirmed the hypothesis before Phase B code surgery.

Existing memory `feedback_push_permission.md` (standing push permission for Utinni repo) was used — all four commits pushed without per-commit re-authorization.

## Working-tree noise (pre-existing, untouched this session)

```
M Launcher/Launcher.vcxproj.user
?? .planning/phases/01-ci-tier-1-c-scaffold/01-PATTERNS.md
?? scripts/audit-utinni-rvas.ps1
?? scripts/find-hidden-error.ps1
?? scripts/rva-audit.csv
```

Same as 2026-05-19 night handoff. Probably from earlier RVA audit work — worth deciding whether to commit, gitignore, or delete before next session starts a clean branch.

Master at session end: `64a0eb0`. Pushed to `origin/master` at `kennethlong/Utinni`.

## Next-session entry point

**Top priority (quick win):** Ask user to paste log capture from the Lok+Naboo session. If single-cycle behavior confirms, upgrade Issues #7 and #8 to RESOLVED in STATE.md.

**After that, four reasonable paths:**

1. **Issue #10 — window reparenting** (~half-day to a day). The "real" embedded-editor model. Touches imgui's `cParam.hFocusWindow` capture, DirectInput's `SetCooperativeLevel` binding, and the WndProc subclass routing. Higher complexity than what we did today; benefits include single-window UX and unblocks the deferred knock-on cleanup of dead editor-mode branches.

2. **Knock-on dead-code cleanup** (~1 hour). Remove the now-auto-skipping editor-mode branches in `hkMainLoop` (`game.cpp:122-128`) and `hkEndScene` (`graphics.cpp:281-300`). Decision input: keep them as breadcrumbs for Issue #10, or remove them as confusing dead code. The PRO of keeping is "future me will see what was here and understand the reparenting design". The PRO of removing is "future me won't have to puzzle out why these branches exist but auto-skip".

3. **Return to GSD workflow.** STATE.md says Phase 02.1 complete, next action `/gsd:discuss-phase 02.1` or `/gsd:plan-phase 02.1`. The roadmap had 11 phases originally; with the boot pipeline + input pipeline + scene pipeline all working, the path forward is the original V1 plan (TRE/IFF/Datatable/Stringtable/Object Template subpanels per DEC-C4).

4. **Take a break.** Three issues closed in one session is a good day. The pipeline is healthy enough that nothing is bleeding right now.

Suggested order: log-capture confirmation → then GSD workflow (path 3). The boot+input+scene tactical fixes have stabilized things enough that the original feature roadmap can move forward. Reserve Issue #10 (window reparenting) for when integration polish becomes the priority.
