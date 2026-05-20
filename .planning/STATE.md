---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: completed
stopped_at: Phase 2 context gathered
last_updated: "2026-05-20T21:00:00.000Z"
last_activity: 2026-05-20 -- Issue #11 RESOLVED; #12 CLOSED NOT-A-BUG; #10 Phase A LANDED (cbe1de7); CODEX consult done -- Phase B will use borderless owned popup, NOT WS_CHILD (DirectInput top-level HWND requirement)
progress:
  total_phases: 12
  completed_phases: 3
  total_plans: 9
  completed_plans: 9
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-16)

**Core value:** A modder downloads Utinni, installs once, and from a single application can see, edit, and live-preview every asset the SWG client loads — replacing the fragmented 15-year-old editor zoo with one stable, plugin-driven tool.
**Current focus:** Phase 02.1 — phase-02-gap-closure-critical-correctness-harness-quality

## Current Position

Phase: 02.1 — COMPLETE
Plan: 1 of 3
Status: Phase 02.1 complete
Last activity: 2026-05-19 -- Phase 02.1 marked complete
Next action: `/gsd:discuss-phase 02.1` or `/gsd:plan-phase 02.1`

Progress: [░░░░░░░░░░] 0%

## Performance Metrics

**Velocity:**

- Total plans completed: 2
- Average duration: —
- Total execution time: —

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| — | — | — | — |
| 01 | 2 | - | - |

**Recent Trend:**

- Last 5 plans: (none yet)
- Trend: (no data)

*Updated after each plan completion*

## Accumulated Context

### Roadmap Evolution

- Phase 02.1 inserted after Phase 2: Phase 02 gap closure — critical correctness + harness quality from 02-REVIEW.md (URGENT)

### Decisions

Full decision log lives in PROJECT.md Key Decisions table. V1 starts with four locked anti-goal decisions (DEC-A1..A4 — not a server-side manager, not a launcher, not a DCC, not a cheat enabler) and three non-locked candidate decisions (DEC-C1 product target, DEC-C2 anti-goals as scope filter, DEC-C3 tiered testing strategy).

### Pending Todos

None yet.

### Blockers/Concerns

**Open concerns from live UAT 2026-05-18/19 (not phase-gated; track separately):**

1. **WR-03 exit dialog STILL FIRES** — Plan 02.1-02 successfully eliminated the `delete depthTexture` UAF in `directX::cleanup()` (verified empty body at `directx9.cpp:410-427`), but on injected-session SWG exit the "Direct3D could not be correctly initialized" dialog still appears. Different teardown path than the one we fixed — likely `clr::stop()` (called immediately after cleanup in `detatch()`) or SWG's own D3D9 device-release noticing leftover state. Investigation deferred — not blocking; exit-only nuisance. Earlier "no exit dialog" UAT report on 2026-05-18 was incorrect (user mistook a delayed dialog for a startup dialog); status reset to "partial-fix; exit-side teardown still flagged." Re-investigate in Phase 03 or a Phase 02.2 mini-effort. **2026-05-19 update:** disappears in passthrough-everything builds; reappears with any detour active — confirms exit dialog is downstream of a Utinni hook interaction with D3D9 lifecycle, not the cleanup() UAF we already fixed.

2. **~~D3D9 vtable pattern doesn't match modern d3d9.dll~~ RESOLVED 2026-05-19 (commit 2c57d38)** — Replaced the broken `d3d9.dll` byte-pattern scan in `directx9.cpp::getVtbl()` with the conventional dummy-device approach (`Direct3DCreate9` + hidden 1x1 window + `CreateDevice(HAL)` + read vtable pointer + snapshot 119 entries + release). Proved via probe of buildable SWG Source client that modern `d3d9.dll` (Win11 24H2 6.2.26100.8328) allocates IDirect3DDevice9 vtables per-instance on the heap — no static `.rdata` table exists for pattern scanning. Probe data archived in `.planning/SESSION-HANDOFF-2026-05-19.md`. After this commit, injection log shows no DirectX9 critical errors; D3D9 detours install cleanly.

3. **Editor-mode HWND-override hooks were wedging SWG init — RESOLVED 2026-05-19 (commits 18c5e22 + 74f64fc)** — Bisection (13 rounds) traced post-d3d9-fix audio-init stall to two editor-mode code paths that override SWG's HWND with the editor's:
   - `hkSetupStartInstall` set `pStartupData->createOwnWindow=false` + `windowHandle=Client::getHwnd()` → SWG silently hung after audio init.
   - `hkSetupInstall` (DirectInput) replaced SWG's HWND with editor's top-level HWND → `SetCooperativeLevel` returned `DIERR_INVALIDPARAM` because the editor HWND is on the CLR thread, not SWG's main thread.
   Both hooks now pass through. New integration model: SWG creates its own window normally; managed side will reparent that HWND into the editor's PanelGame via `SetParent` + `WS_CHILD` style change. Managed-side reparenting still not implemented — see open item #10.

4. **~~Managed-side CLR exception 0xE0434352 during character template load~~ LIKELY OBSOLETE 2026-05-19 night** — Was hypothesized as a downstream consequence of #6 (the jmp-self halt). With #6 now RESOLVED, the consequential CLR exception has not reproduced in any of tonight's successful boot runs (SWG progressed past character template load into the login screen and intro). Marked obsolete pending re-observation. If it re-surfaces independently, original investigation plan still valid: VS 2026 → Debug → Exception Settings → check "Common Language Runtime Exceptions" → run Launcher.exe under VS to capture the throwing managed line.

5. **~~SWG window invisible during runtime~~ RESOLVED IMPLICITLY 2026-05-19 night** — Symptom was a direct consequence of #6 (main thread halted in `EB FE` so `clientMain` never returned to enter the render loop). With #6 fixed, the SWG window now appears normally during the boot sequence and shows the pre-login flow + login screen. **Window reparenting into the editor's PanelGame is a separate concern** — see open item #10.

6. **~~SWG main thread halts in jmp-self at `0x0131DC7A`~~ RESOLVED 2026-05-19 night (commits `dad9845..20fbad5`)** — Three-session investigation. Root cause was **Utinni's own Launcher** writing `EB FE` to SWGEmu's PE entry as a stall mechanism while UtinniCore.dll was injected (`Launcher/main.cpp:351-352`). The matching restore code at lines 382-384 sat behind `inject(procInfo)` → `WaitForSingleObject(hInitThread, INFINITE)` → blocked forever because `utinni_init` blocks in `clr::load()` → `Application.Run(FormMain)` for the editor's lifetime. The function at `0x0131DC7A` is MSVC `__tmainCRTStartup` (CRT entry), NOT SWG `Os::install` as initially hypothesized — corrected via CODEX peer review. Variance between sessions ("sometimes halts immediately, sometimes runs to preloading then halts later") was CPU I-cache nondeterminism (`WriteProcessMemory` doesn't flush instruction cache) — CODEX's catch. **Fix architecture:** named-event signal-based sync. Launcher creates `Local\UtinniReady_<pid>` event, passes name to `utinni_init` via `lpThreadParam`. Managed `Startup.EntryPoint` calls `Native.SignalLauncherReady()` after all four `*Callbacks.Initialize()` calls and immediately before `Application.Run`. Launcher waits on the event (30s timeout) instead of the thread handle, then restores PE entry + `FlushInstructionCache` + resumes main thread. Full mechanics in `.planning/SESSION-HANDOFF-2026-05-19-NIGHT.md` and in Claude's auto-memory `project_eb_fe_patch_origin.md`.

**NEW issues from 2026-05-19 night session (downstream of the resolved boot pipeline; all non-blocking):**

7. **~~Lok scene-load second-cycle access violation at `0x00b3f620`~~ RESOLVED 2026-05-20 by Phase B fix (commit `2f02fad`)** — Original crash dump 2026-05-19 at `D:\SWGEmu-Client\SWGEmu\logs\SWGEmu.exe-stage.119798-20260520022742.{txt,mdmp}` showed access violation at fault-address `0x00b3f620` while constructing `shared_filler_building_corellia_style_02.iff` (cargo freighter, asset reused on Lok), terrain `lok.trn`, MainLoop=4208 (steady state, not startup), BytesAllocated=93M (not OOM — pointer deref of partially-released structure). **Hypothesis (confirmed):** before Phase B, `hkMainLoop` called SWG's mainLoop with `Client::getHwnd()` (PanelGame's HWND) and changing dimensions every frame (`PanelGame_Layout` rewrote `Client.SetHwnd(Handle)` on every layout). SWG's mainLoop seeing inconsistent HWND/size between frames triggered an internal scene re-init cycle. With `Client::getHwnd()` returning null post-Phase-B, `hkMainLoop`'s editor-mode branch auto-skips and SWG gets its own real HWND + dimensions stably. **Verification 2026-05-20 (live + log):** (a) user loaded Lok via TJT "Load Scene" panel (same path as the original crash) — Lok stayed loaded, fully playable. (b) `utinni.log` post-Phase-B shows the TJT load path produces single-cycle behavior: `hkMainLoop: loadNewScene -> Game::cleanupScene` → `hkSetScene(null)` → `hkMainLoop: setupScene via trampoline` → `hkSetScene(<new>)` → `firing 1 setSceneCallbacks`. **One** cleanup, **one** setup, **one** callback fire. No per-frame re-init churn. `Client::setHwnd` diag tripwire silent (never called → `Client::getHwnd()` stays null → editor-mode resize branches in `hkMainLoop` + `hkEndScene` auto-skip cleanly). `DirectInput::suspend` tripwire also silent throughout. The HWND/size oscillation hypothesis is confirmed dead in code. **Side observation worth a memory note:** initial Tatooine load fires `hkSetScene` **twice** with the same scene pointer (0x2297FCE0 → 0x2297FCE0 within 1 sec), meaning `setSceneCallbacks` runs twice for the initial scene at login. This is SWG-internal (intro→world transition), not Utinni regression — but TJT/plugin authors should know setScene callbacks may double-fire on initial login.

8. **~~Naboo scene-load: SWG memory pool exhausts at ~300 MB~~ RESOLVED 2026-05-20 by Phase B fix (commit `2f02fad`)** — Original crash dump 2026-05-19 at `D:\SWGEmu-Client\SWGEmu\logs\SWGEmu.exe-stage.119798-20260520021056.{txt,mdmp}` showed `BytesAllocated: 196M → 300M`, fatal allocation attempt for 38KB while loading `shared_frn_all_bed_sm_s1.iff`. Original hypothesis was Utinni's CLR+WinForms mappings starving SWG's 750 MB pool. **Revised hypothesis (confirmed):** the apparent "OOM at 300MB" was pool fragmentation from SWG's internal re-init cycling (see Issue #7), not true address-space exhaustion. The HWND/size oscillation per frame caused SWG to repeatedly re-allocate render targets / texture buffers, fragmenting the pool. **Verification 2026-05-20 (live + log):** user loaded Naboo via TJT "Load Scene" panel — Naboo stayed loaded, fully playable, no OOM. Single-cycle log evidence from Issue #7 (same hkMainLoop loadNewScene code path) applies here too. **Re-open if:** Naboo crashes on longer playthrough or with more assets streamed. **Original LARGEADDRESSAWARE investigation still useful as fallback** if Issue #8 reproduces.

9. **~~Cursor doesn't display + special keys (delete/tab/return) don't work in game~~ RESOLVED 2026-05-20 (commits `f5fa073..2f02fad`)** — Two-phase fix. **Diagnosis (Phase A, commit `f5fa073`):** added entry/exit logs to `Client::suspendInput/resumeInput`, `DirectInput::suspend/resume`, and `Client::setHwnd`. Live capture revealed every PanelGame focus-loss + mouse-leave fired `Client::SuspendInput` twice (back-to-back), which called `DirectInput::suspend()` and `SetFocus(nullptr)` — unacquiring DirectInput the instant the user clicked into SWG's window. Special keys (Tab/Del/Return) dispatched via DirectInput were dead; regular chars survived via `WM_CHAR` through the OS message pump. **Fix (Phase B, commit `2f02fad`):** stripped the old-model editor meddling: (a) removed PanelGame's MouseEnter/MouseLeave/MouseMove/GotFocus/LostFocus handlers — they assumed SWG rendered inside PanelGame, no longer valid in the SWG-owns-its-own-window model; (b) removed `Client.SetHwnd(Handle)` from `PanelGame_Layout` — was shadowing SWG's real top-level HWND with PanelGame's handle; (c) dropped editor-mode cursor side-effects from `hkSetupInstall` (HCURSOR write + `useHardwareCursor(false)`) — these forced software-cursor mode that only renders inside SWG's framebuffer, which the new model doesn't need. **Verification 2026-05-20:** login → mouse/keyboard both work → character select → mouse click logged into Tatooine. End-to-end pipeline functional. Phase A diag logs kept in source — nearly silent during normal play, will flag any future regression. **Knock-on cleanup deferred:** `game.cpp` `hkMainLoop` and `graphics.cpp` `hkEndScene` editor-mode resize branches both gate on `GetWindowRect(Client::getHwnd(), &rect)`; with `getHwnd()` now returning null they auto-skip, but the dead branches should be cleaned up when Issue #10 (window reparenting) decides the correct behavior.

10. **SWG window reparenting — Phase A LANDED 2026-05-20 (commit `cbe1de7`); Phase B planned per CODEX guidance** — Goal: embed SWG's top-level window into editor's PanelGame so they appear as one app instead of two windows.
    - **Phase A (done):** added `Client::setSwgHwnd`/`getSwgHwnd` (separate from existing `setHwnd`/`getHwnd` to avoid reactivating quiet editor-mode branches in `hkMainLoop`/`hkEndScene`/`Client::resumeInput`). Called from `imgui_impl::setup()` after `pDevice->GetCreationParameters(&cParam)`. Pure plumbing, no behavior change. CppSharp auto-regens managed bindings so PanelGame can read `Client.GetSwgHwnd()`.
    - **Phase B (CODEX-guided, NOT YET IMPLEMENTED):** CODEX consult 2026-05-20 explicitly recommended **borderless owned popup BEFORE WS_CHILD**. Reason: `IDirectInputDevice8::SetCooperativeLevel` per Microsoft docs requires a top-level HWND belonging to the process. WS_CHILD violates that — even if SWG already called SetCoopLevel pre-reparent, it'd fail on the next acquire/unacquire/focus transition in exactly the WASD/Enter path we just fixed in Phase H. **Recommended sequence:** (1) keep SWG as top-level `WS_POPUP`, strip visible frame styles (`WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU`), (2) `SetWindowLongPtr(GWL_HWNDPARENT, FormMain.Handle)` to make FormMain the owner, (3) `SetWindowPos` to position over `PanelGame.ClientSize`, (4) re-position on PanelGame.Layout/Resize events. Preserves DirectInput's top-level-HWND assumption, avoids child-activation weirdness, lower risk to Phase H. WS_CHILD only as opt-in flag experiment after owned-popup is proven.
    - **Phase B prerequisite (recommended by CODEX):** before any reparenting, instrument `IDirectInputDevice8::SetCooperativeLevel` (light COM-vtable shim) to log `hwnd` + `dwFlags`. Flags constants: `DISCL_EXCLUSIVE=0x1`, `NONEXCLUSIVE=0x2`, `FOREGROUND=0x4`, `BACKGROUND=0x8`, `NOWINKEY=0x10`. Confirms whether SWG uses FOREGROUND (which would break under WS_CHILD) vs BACKGROUND (which is more tolerant).
    - **D3D9 device:** CODEX says transparent passthrough — HWND value unchanged, just parent/style. Don't force device reset. The existing `hkReset` at `directx9.cpp:323` handles ImGui invalidation naturally via WM_SIZE.
    - **Phase H (chatEnter) survival:** detour is on SWG-internal code, independent of windowing. Only credible break is upstream — if DirectInput stops polling under WS_CHILD, Enter never reaches the action path. Owned-popup avoids this entirely.
    - **References:** Microsoft `SetParent` docs, `IDirectInputDevice8::SetCooperativeLevel` docs.

11. **~~In-game Return dead~~ RESOLVED 2026-05-20 by Phase H (commit `6047416`)** — Eight-phase diagnostic + CODEX consult chain. **Diagnosis path:** Phase A WndProc tripwires proved WM_KEYDOWN reaches SWG cleanly. Phase B cui_io tripwires proved `CuiIo::processEvent` passes type-6/7 events through (no drop). Phase C/D `hkActionPerformAction` instrumentation showed Esc reaches `CuiHud::actionPerformAction` (action 0x12) but Enter doesn't — different dispatch chains for the two keys. Phase E `enableTextInput` detour proved chat mediator works perfectly via F11 force-call but normal in-game Enter fires `chatEnter` (caller `0x00F3E43D`) with `value=0` (close) when chat is already closed — no-op. Phase F runtime probe of the SwgCuiChatWindow::performAction dispatcher's `.bss` action-name slots resolved all 12 cases: in-game Enter dispatches the `chatEnter` action string (slot 11) to handler `0x00F3E420`, which unconditionally submits+closes — correct for input-mode submit, broken for display-mode "open". **CODEX consult** confirmed SWG has separate dispatch chains and that the SwgCuiChatWindow `chatEnter` handler has no state-check (assumes the upstream dispatcher already filtered for input-mode-only). Phase G attempt to consume VK_RETURN in WndProc failed because SWG uses DirectInput keyboard polling, not WM_KEYDOWN, for in-game Enter. **Phase H fix (commit `6047416`):** detour `swg::cuiChatWindow::chatEnterHandler` at `0x00F3E420` directly. In `hkChatEnter`: if our tracked `s_chatInputActive` is false (chat in display mode), override SWG's broken submit-empty-and-close with a direct `enableTextInput(true)` — translates "in-game Enter while chat closed" into "open chat input". If `s_chatInputActive` is true (chat in input mode), pass through to original trampoline (legitimate submit+close). State machine perfect. **Verification 2026-05-20:** user pressed Enter → chat opened. Typed `/test`, pressed Enter → submitted (empty) + closed. Pressed Enter again → opened. Typed `/quit`, pressed Enter → submitted + `hkCleanupScene` fired → game exited cleanly. End-to-end Enter functionality restored without F11. Diag tripwires retained (silent under normal play, flag regressions). **Root cause (deferred for V2):** under editor injection, SWG's input-map context selector routes in-game Enter to the chat-input-mode binding instead of the game-mode "openChat" binding. The standalone client likely uses a contextual keymap that we'd need SWG source or a CuiActionManager-layer hook to investigate properly. Phase H is a targeted intercept at the right semantic level (the broken handler itself), not a workaround in the wrong place.

12. **~~In-game Esc doesn't open system menu~~ NOT-A-BUG / WORKING AS DESIGNED (2026-05-20)** — Closed after two diagnostic phases (commits `e3f966d`, `66e28e8`) revealed the user's expectation was wrong, not the code. **Phase A (`hkSwgCuiGameMenuCtor` detour at `0x00C7D360`):** SwgCuiGameMenu mediator ctor never fires even after 16 in-game Esc presses — strong signal that no menu activation is being attempted. **Phase B (val1 string dump in `hkActionPerformAction`):** runtime read of the string pointers in val1 revealed `str_a = 'untarget'`, `str_b = ''` for every Esc press. **Action ID `0x12` is `untarget`, not `gameMenuActivate`.** SWG binds Esc to "clear current target" — when no target is selected, the action is an invisible no-op. The system menu (`SwgCuiGameMenu` / `gameMenuActivate` action) exists but is bound to a different key or HUD button. This is normal SWG behavior, not a Utinni regression. **Verification 2026-05-20:** user confirmed pressing Esc while having a target correctly clears the target reticle. **Workaround for menu access:** use Phase H's `/quit` chat path for exit; menu-equivalent UI actions accessible via slash commands or HUD buttons. **If menu access via key becomes a priority later:** add a Utinni hotkey that dispatches the gameMenuActivate action directly (same pattern as F11→forceOpenChat in Phase H). Mediator class strings, RVAs, and dispatcher analysis are in commit messages `e3f966d` and `66e28e8`.

Eleven open questions (CON-O-01..CON-O-11) are tracked as phase-gated unresolved constraints — see ROADMAP.md "Open-Question → Phase Mapping" section.

## Deferred Items

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| *(none)* | | | |

## Resolved Deferred Items

| Category | Item | Resolved | Notes |
|----------|------|----------|-------|
| Tier-4 manual UAT | Plan 02-03 Task 3 — C-01 live SWG injection | 2026-05-18 | PASSED. Editor host + TJT plugin came up alongside live SWGEmu client; no loader-lock hang. See `02-HUMAN-UAT.md` §1. En-route fixes (commits `cb547bb`, `92758ff`, `9a108d1`, `3254059`, `389fc83`) landed during the UAT run. |
| Tier-4 manual UAT | Plan 02-04 Task 2 — C-09 live SWG minimize/restore | 2026-05-18 | PASSED. Editor stays responsive across rapid minimize/restore cycles; no UI-thread CPU spike. See `02-HUMAN-UAT.md` §2. |

## Session Continuity

Last session: 2026-05-16T23:04:55.398Z
Stopped at: Phase 2 context gathered
Resume file: .planning/phases/02-critical-bug-burn-down-c-01-c-15/02-CONTEXT.md

## Ingest Provenance

Bootstrapped 2026-05-16 via `/gsd:ingest-docs` from `docs/ai/vision.md`, `docs/ai/assessment.md`, and `docs/ai/test-harness-plan.md`. Zero blockers, zero warnings, four INFO items auto-resolved (all three sources are DOC-precedence; reciprocal vision↔assessment cross-reference is benign narrative linkage). Codebase intel at `.planning/codebase/` (from prior `/gsd:map-codebase`) treated as read-only reference. Synthesis artefacts at `.planning/intel/` (SYNTHESIS.md, decisions.md, requirements.md, constraints.md, context.md) and conflict report at `.planning/INGEST-CONFLICTS.md`.
