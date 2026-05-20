# Session Handoff: 2026-05-20 (PM)

> Written 2026-05-20 ~21:00 to wrap a session that closed Issue #11 (in-game Return), closed Issue #12 as NOT-A-BUG (Esc=untarget, not menu), confirmed Issues #7+#8 RESOLVED via log scan, and landed Phase A plumbing for Issue #10 (window reparenting). CODEX consults paid off twice. Supersedes `SESSION-HANDOFF-2026-05-20.md` (AM session). Master at `cbe1de7`.

## TL;DR

Today's three-issue arc:

- **#7 (Lok AV) + #8 (Naboo OOM)** — confirmed RESOLVED via log scan (commit `a326ba2`). Per-frame HWND/size oscillation hypothesis from yesterday's handoff is dead in code; TJT scene swap is single-cycle clean.
- **#11 (in-game Return dead)** — RESOLVED via an eight-phase diagnostic chain (A–H) plus one CODEX consult. Final fix is a detour of SwgCuiChatWindow's chatEnter handler at `0x00F3E420` that overrides display-mode submit-empty-and-close behavior with `enableTextInput(true)` instead. `/quit` works end-to-end naturally — no more F11 needed.
- **#12 (in-game Esc no system menu)** — opened during #11 diagnostics, closed as NOT-A-BUG. Runtime val1 string-pointer dump in `hkActionPerformAction` resolved action `0x12` to literal `"untarget"`. SWG's Esc is bound to clear-target, not open-menu. WoW-era expectation mismatch.
- **#10 (window reparenting)** — Phase A plumbing landed (commit `cbe1de7`). Second CODEX consult done; Phase B path is **borderless owned popup, NOT WS_CHILD** (DirectInput needs top-level HWND).

Two memory notes added: `project_swg_context_routing.md` (Phase H pattern for future similar bugs), `project_swg_keymap_reality.md` (don't assume WoW conventions).

## Commits shipped this session (15 total)

```
cbe1de7 feat(client): Issue #10 Phase A -- capture + expose SWG's top-level HWND
4cffbdf docs(state): #12 Esc system-menu CLOSED as NOT-A-BUG -- action 0x12 is "untarget"
66e28e8 diag(input): Issue #12 Phase B -- dump val1 string contents for action 0x12
e3f966d diag(input): Issue #12 Phase A -- detour SwgCuiGameMenu ctor at 0x00C7D360
5d51609 docs(state): #11 in-game Return RESOLVED via Phase H chatEnter override; open #12 Esc system-menu
6047416 fix(input): Phase H -- detour chatEnter handler to override display-mode close (Issue #11)
29128dd feat(input): Phase G -- in-game Enter workaround + GroundScene/MQ diag (Issue #11)
02af0d5 diag(input): Phase F -- F12 runtime probe for chat dispatcher action strings
9ec702b diag(input): Phase E -- enable enableTextInput detour + fix pThis bug (Issue #11)
eb93e34 diag(input): Phase D per CODEX consult -- F11 chat bypass + expanded action dump
28c0a66 diag(input): cui_hud hkActionPerformAction trace+bypass (Issue #11 Phase C)
022bf85 diag(input): log every type-6/7 CUI event (Issue #11 Phase B-2)
5431493 diag(input): cui_io.cpp tripwires for Issue #11 (Enter/Esc dropped in-game)
d58428c diag(input): WndProc tripwires for in-game Return/Esc dead (Issue #11)
a326ba2 docs(state): #7+#8 RESOLVED (log scan confirms); open Issue #11 (in-game Return/Esc dead)
```

## Issue #11 diagnostic chain (summary)

| Phase | What | Outcome |
|---|---|---|
| **A** | WndProc tripwires in `imgui_impl.cpp::hkWndProcHandler` for WM_KEYDOWN/VK_RETURN/VK_ESCAPE + WM_CHAR + WM_ACTIVATE | WM_KEYDOWN reaches subclass cleanly with SWG foregrounded + imgui not capturing. Ruled out Win32 message routing. |
| **B** | `hkProcessEvent` in `cui_io.cpp` — log type-6/7 events + drop tracking | `isKeyboardEnabled` stays true; no drops. Then expanded to log EVERY type-6/7 event (Phase B-2): in-game Return/Esc both reach `processEvent` cleanly. Ruled out SWG CUI event delivery. |
| **C** | `hkActionPerformAction` in `cui_hud.cpp` — log calls + bypass gizmo-hover skip | Esc fires actionPerformAction (action `0x12`); Enter doesn't. Different dispatch chains. gizmoHover always 0 → bypass was no-op anyway. |
| **D** | (Per CODEX consult #1) F11 bypass calling `enableTextInput` directly + expanded action dump | F11 works perfectly — opens chat, typing works, Enter submits, `/quit` exits. Chat mediator + window 100% fine. Bug is upstream. |
| **E** | Enable `enableTextInput` detour (was commented out) with caller-PC logging + fix CODEX-spotted `pThis` vs `pCuiChatWindow` bug | In-game Enter fires `enableTextInput(value=0)` from caller `0x00F3E43D` (chatEnter handler) → tries to close already-closed chat → no-op. |
| **F** | F12 runtime probe of SwgCuiChatWindow::performAction dispatcher's .bss action-name slots | Resolved all 12 action strings. Slot 11 = `chatEnter` (CLOSE handler at `0x00F3E420`). In-game Enter dispatches chatEnter — wrong action for game-mode. |
| **G** | (Per CODEX consult #2) VK_RETURN WM_KEYDOWN consume + force-open + GroundScene/MessageQueue diag | WM_KEYDOWN consumption ineffective — SWG uses DirectInput polling for in-game Enter. Force-open then chatEnter immediately closes. Workaround failed but proved DirectInput is the dispatch path. |
| **H** | **FIX**: detour `SwgCuiChatWindow::chatEnter` at `0x00F3E420`, override display-mode behavior to call `enableTextInput(true)` instead of submit+close | Works end-to-end. Enter opens chat → type `/quit` → Enter submits → clean exit. |

## CODEX consult summaries (paste-ready prompts archived in commit messages)

### Consult #1 — Issue #11 chat dispatch root cause (after Phase C)
- Validated diagnosis chain
- Recommended F11 enableTextInput bypass as A/B test (became Phase D)
- Suggested instrumentation: GroundScene::handleInputMapEvent, MessageQueue::appendMessage*, SwgCuiChatWindow::performAction entry logger
- Caught the `pCuiChatWindow` vs `pThis` bug in `hkEnableTextInput` (Phase E fix)
- Clarified SWG-Source repos don't have client UI source

### Consult #2 — Issue #11 context routing (after Phase F)
- Recommended hooking GroundScene::handleInputMapEvent at `0x0051AA40` first
- MessageQueue::appendMessage at `0x00AA6640`, appendMessageData at `0x00AA6480`
- ret=1 from CuiHud::actionPerformAction means "consumed", not "menu opened"
- Suggested signature-scanning for activations by mediator name (`SwgCuiSystemMenu`, etc.)
- Endorsed VK_RETURN intercept workaround scoped to ground scene (became Phase G — turned out ineffective, led to Phase H fix at right semantic level)

### Consult #3 — Issue #10 reparenting strategy (this session, after Phase A)
- **DON'T do WS_CHILD first**: `IDirectInputDevice8::SetCooperativeLevel` per Microsoft docs requires top-level HWND
- **Use borderless owned popup**: keep top-level WS_POPUP, strip frame styles (`WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU`), set FormMain as owner via `SetWindowLongPtr(GWL_HWNDPARENT)`, `SetWindowPos` over PanelGame.ClientSize
- **Before reparenting**: instrument `IDirectInputDevice8::SetCooperativeLevel` via COM-vtable shim to confirm flags. Decode: `DISCL_EXCLUSIVE=0x1`, `NONEXCLUSIVE=0x2`, `FOREGROUND=0x4`, `BACKGROUND=0x8`, `NOWINKEY=0x10`
- **D3D9**: transparent passthrough, no forced device reset. `hkReset` in `directx9.cpp:323` handles ImGui invalidation naturally on WM_SIZE
- **Phase H survival**: detour is SWG-internal, only credible break is upstream DirectInput failure under WS_CHILD — owned-popup avoids this
- Approve Phase A plumbing approach (separate setSwgHwnd/getSwgHwnd, not repurposing existing setHwnd)

## Memory notes added this session

- `C:\Users\kenne\.claude\projects\D--Code-Utinni\memory\project_swg_context_routing.md` — Phase H pattern (detour wrong-context handler at SWG-internal level, override stateless behavior). General technique for future similar bugs.
- `C:\Users\kenne\.claude\projects\D--Code-Utinni\memory\project_swg_keymap_reality.md` — SWG keymap reality (Esc=untarget, NOT open-menu). Don't assume WoW conventions; verify action strings via val1[0] string-pointer dump before assuming Utinni injection broke something.

## Current diag state (kept in source as tripwires)

All Phase A-H diag logs are retained. They're silent or near-silent under normal play and will flag regressions instantly:

- `client.cpp` Phase A: `setHwnd`/`suspendInput`/`resumeInput` entry logs
- `direct_input.cpp` Phase A: `suspend`/`resume` entry logs
- `cui_io.cpp` Phases B/B-2: type-6/7 event passthrough log (cap 60)
- `cui_hud.cpp` Phases C/D + #12 A/B: `hkActionPerformAction` PASS/SKIPPED log with pThis/vtbl/val1/val2 dump + caller PC + **val1[0..1] / val1[3..4] string contents** (cap 20)
- `cui_hud.cpp` #12 Phase A: `hkSwgCuiGameMenuCtor` log (cap 20, never fired in any session — confirms ctor isn't on the Esc path)
- `cui_chat_window.cpp` Phase E: `hkEnableTextInput` with caller-PC (cap 30)
- `cui_chat_window.cpp` Phase H: `hkChatEnter` display-mode override log (silent under normal play)
- `cui_chat_window.cpp` Phase F: `dumpActionStringSlotsFromCpp` — F12 hotkey, runtime probe of dispatcher .bss strings
- `cui_chat_window.cpp`: F11 hotkey → `forceOpenChatInputFromCpp` (backup chat-open path)
- `ground_scene.cpp` Phase G: `hkHandleInputEvent` IoEvent log (cap 40, filters t_Update spam)
- `io_win.cpp` Phase G: `hkAppendMessage` + `hkAppendMessageData` log (cap 60 each — **noisy at scene-load from msg=0x140 floods**)
- `imgui_impl.cpp` Phase A: WM_KEYDOWN(Return/Esc), WM_CHAR, WM_ACTIVATE/WM_SETFOCUS/WM_KILLFOCUS

**Diag cleanup deferred** — verbosity is annoying but informative. Address as a quick win during next session or after Phase B (when fewer experiments are needed).

## What's open after this session

### Issue #10 Phase B (next session, primary target)

CODEX-guided implementation order:

1. **(prereq)** COM-vtable shim around `IDirectInputDevice8::SetCooperativeLevel` to log SWG's actual cooperative-level flags. Light: hook the vtable entry, log `hwnd` + `dwFlags`, call through.
2. **Borderless owned popup** in PanelGame:
   - Read `Client.GetSwgHwnd()` (Phase A exposed it)
   - When non-null + PanelGame.IsHandleCreated:
     - `style = GetWindowLong(swgHwnd, GWL_STYLE)`
     - `style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU)` (keep WS_POPUP)
     - `SetWindowLong(swgHwnd, GWL_STYLE, style)`
     - `SetWindowLongPtr(swgHwnd, GWLP_HWNDPARENT, FormMain.Handle)` (owner, not parent)
     - `SetWindowPos(swgHwnd, IntPtr.Zero, panelClientOrigin.X, panelClientOrigin.Y, panel.ClientSize.Width, panel.ClientSize.Height, SWP_FRAMECHANGED | SWP_NOZORDER | SWP_SHOWWINDOW)`
3. **PanelGame.Resize** → re-`SetWindowPos`
4. **FormMain.Move** → re-`SetWindowPos` (since SWG window is positioned in screen coordinates relative to PanelGame)
5. **Verify Phase H Enter still works** (chat opens, /quit exits cleanly)
6. **Verify WASD movement** (DirectInput polling — most-likely-to-break test case)

### WS_CHILD experiment (deferred)

After owned-popup ships and is verified, optionally add a flag-gated experiment with WS_CHILD reparenting to see if it works on this SWGEmu binary specifically. Per CODEX, treat as opt-in research, not default.

### Working-tree noise (still pre-existing, untouched)

Same as morning handoff. Untracked files from earlier RVA audit:

```
?? .planning/phases/01-ci-tier-1-c-scaffold/01-PATTERNS.md
?? scripts/audit-utinni-rvas.ps1
?? scripts/find-hidden-error.ps1
?? scripts/rva-audit.csv
```

Plus modified `Launcher/Launcher.vcxproj.user` (always modified — VS user-specific).

Decide whether to commit, gitignore, or delete next session.

### Diag verbosity (quick-win opportunity)

`hkAppendMessageData msg=0x140` floods the log at scene-load — ~50 entries within 1 second. Either filter (don't log msg=0x140 unless it appears outside scene-load) or reduce cap. Quick 5-min cleanup whenever convenient.

### Issue #1 (still open) — WR-03 exit dialog

Disappears in passthrough-everything builds, reappears with any detour active. Exit-only nuisance. Lowest priority.

## Verification observations (this session)

**Issue #11 final verification** (Phase H, 15:37-15:38 log):
- Login → Tatooine → Enter pressed in-game → `hkChatEnter` override fires → chat opens with cursor
- Typed `/test`, pressed Enter → submitted (empty content) + closed via chatEnter trampoline
- Pressed Enter again → opened
- Typed `/quit`, pressed Enter → submitted + close → `hkCleanupScene` → clean exit
- Phase A WndProc tripwires + Phase B processEvent passthroughs + Phase G hkHandleInputEvent + Phase E enableTextInput callers all logged consistently with the state machine

**Issue #12 verification** (16:12 log):
- F12 dispatcher probe + actionPerformAction string dump showed `str_a = 'untarget'` for action 0x12
- User confirmed Esc with target → reticle clears
- Closed as WAD

## Files referenced this session

### Source edits (committed)

- `UtinniCore/swg/ui/imgui_impl.cpp` — Phase A WndProc tripwires + F11/F12 hotkey wiring + Phase A SWG HWND capture (#10)
- `UtinniCore/swg/ui/cui_io.cpp` — Phase B processEvent tripwires
- `UtinniCore/swg/ui/cui_hud.cpp` — Phase C/D actionPerformAction trace + #12 Phase A SwgCuiGameMenu ctor detour + #12 Phase B val1 string dump
- `UtinniCore/swg/ui/cui_chat_window.cpp` + `cui_chat_window.h` — Phase E enableTextInput detour + Phase F dispatcher probe + Phase H chatEnter override + Phase G `forceOpenChatInputFromCpp` + isChatInputModeActive() getter
- `UtinniCore/swg/scene/ground_scene.cpp` — Phase G IoEvent tripwires in hkHandleInputEvent
- `UtinniCore/swg/misc/io_win.h` + `io_win.cpp` — Phase G MessageQueue::detour + hkAppendMessage/hkAppendMessageData
- `UtinniCore/swg/client/client.h` + `client.cpp` — #10 Phase A setSwgHwnd/getSwgHwnd
- `UtinniCore/utinni.cpp` — MessageQueue::detour() registration
- `.planning/STATE.md` — #7+#8 RESOLVED, #11 RESOLVED Phase H, #12 NOT-A-BUG, #10 Phase A landed + CODEX-guided Phase B planned

### Memory files written

- `~/.claude/projects/D--Code-Utinni/memory/project_swg_context_routing.md`
- `~/.claude/projects/D--Code-Utinni/memory/project_swg_keymap_reality.md`
- `~/.claude/projects/D--Code-Utinni/memory/MEMORY.md` — both indexed

### Files read for understanding (no edits)

- `UtinniCoreDotNet/UI/Controls/PanelGame.cs` — confirmed minimal state post-Phase-B
- `UtinniCoreDotNet/UI/Forms/FormMain.cs` — confirmed `pnlGame.Controls.Add(game)` layout chain
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/SWG/GroundSceneImpl.cs` + `UI/SubPanels/ScenePanel.cs` — confirmed forceModalChat is gated (one-time flip didn't fix #11 but was the first hypothesis we ruled out)
- `D:\SWGEmu-Client\SWGEmu\SWGEmu.exe` — extensively via PowerShell/PE parsing for Phase F dispatcher decode, action string hunting, vtable searches

## Next-session entry point

Suggested order:

1. **Pick up Issue #10 Phase B per CODEX's owned-popup guidance.** Phase A plumbing is in place (`Client::getSwgHwnd()` ready). Prereq is the DirectInput cooperative-level vtable shim — that's a small targeted detour (one method on `IDirectInputDevice8`), then a single launch to see the flags SWG actually uses. Then implement the borderless-owned-popup logic in PanelGame.
2. **OR** Quick wins first (working-tree noise cleanup + diag verbosity trim, ~30 min total) for a cleaner workspace.
3. **OR** Resume GSD roadmap (`/gsd:discuss-phase 02.1` or `/gsd:plan-phase 02.1`) — the boot/scene/input pipeline is stable enough that V1 feature work (TRE/IFF/Datatable/Stringtable/Object Template TJT subpanels) can move forward.

Master at session end: `cbe1de7`. All commits pushed to `origin/master` at `kennethlong/Utinni`.
