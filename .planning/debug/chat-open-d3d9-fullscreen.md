# Bug: chat-open via Enter triggers true D3D9 exclusive fullscreen

> Captured 2026-05-23 after the R-A heap-free migration live smoke. The smoke itself passed (many warps, no scene-change AV at `0x0051fb0a`), but pressing Enter to open chat reliably triggers a D3D9 device mode switch to true exclusive fullscreen. The SWG window detaches from its embedded spot in the Utinni editor and is never returned. Chat input itself works post-fullscreen (user can type and send), so Phase H functionally succeeded — but with this unwanted side effect.

Status: **queued, not blocking**. R-A migration verified; next planned work is Phase 4 (CLI shim). This issue is the most likely follow-up phase after Phase 4 unless severity escalates.

---

## Symptom

1. Launch Utinni editor → SWGEmu injected, SWG window embedded inside the editor.
2. Log in to live SWGEmu, land in world.
3. Press **Enter** to open chat input.
4. **Observed:** display flicker, true D3D9 exclusive fullscreen takes over the monitor. SWG window detaches from the editor's embed container. Chat is open and interactive — typing + sending works.
5. SWG window never returns to the editor embed location, even after closing chat. Editor session is effectively broken until restart.

User-reported confirmation:
- "true D3D9 fullscreen (display flicker, exclusive mode)"
- "chat opens, I can type and send"

---

## Confirmed: not the R-A migration

R-A heap-free migration touched 10 files / 27 callback registries. None are on the input or window-mode dispatch path:
- `cui_chat_window.cpp` — addCommandParser registry (chat *commands*, not chat-open)
- `cui_manager.cpp` — receiveSystemMessage
- `creature_object.cpp` — onTarget
- `graphics.cpp`, `post_processing.cpp`, `depth_texture.cpp`, `shader.cpp` — render pipeline
- `game.cpp` — install/preMainLoop/mainLoop/setScene/cleanUpScene
- `imgui_impl.cpp` — render + 4 gizmo
- `log.cpp` — logging

Storage swap (`unordered_map<int,fn_ptr>` → `vector<CallbackEntry<Fn>>`) preserves insertion order, so even ordering-sensitive dispatches don't shift behavior. Same managed-side API at the boundary.

Phase 3 close HUMAN-UAT was 1/1 pass (per `SESSION-HANDOFF-2026-05-22-NIGHT.md`) but didn't explicitly note testing chat-open via Enter. Likely:
- Pre-existing, surfaced now because Phase H made Enter actually open chat (Issue #11), and prior to that Enter was a no-op so no fullscreen could happen.

**Bisect target if regression suspected:** master at `9c8edd3` (Phase 3 close, pre-R-A migration). If chat-open via Enter triggers fullscreen there too → pre-existing, not a regression.

---

## Suspect mechanism

Phase H's `hkChatEnter` (commit `6047416`, `UtinniCore/swg/ui/cui_chat_window.cpp:481`) is the active code path:

```cpp
void __fastcall hkChatEnter(swgptr pThis, swgptr EDX)
{
    if (!s_chatInputActive.load(std::memory_order_relaxed))
    {
        // display mode: translate "in-game Enter" -> "open chat input"
        swg::cuiChatWindow::enableTextInput(pThis, true, true, false);
        s_chatInputActive.store(true, std::memory_order_relaxed);
        return;
    }
    swg::cuiChatWindow::chatEnterHandler(pThis);  // input mode: pass through
}
```

The args `(true, true, false)` are `value=true, setKeyboardInput=true, unfocus=false`. **The `setKeyboardInput=true` is the prime suspect** — it likely triggers a DirectInput re-acquire on the chat input device, and if cooperative level is set to exclusive (see commit `18e79c3` DirectInput SetCooperativeLevel vtable shim), exclusive DirectInput can pull D3D9 into fullscreen with it on certain SWG code paths.

**Critical observation:** `CuiChatWindow::forceOpenChatInputFromCpp()` (line 326-348) — the editor's button-driven open-chat path — calls `enableTextInput` with **identical args** `(p, true, true, false)`. If the editor button does NOT trigger fullscreen (worth verifying), the difference between paths must be calling context, not args:
- Calling thread: `forceOpenChatInputFromCpp` runs on managed-side UI event thread; `hkChatEnter` runs from SWG's input dispatcher mid-DirectInput-pump.
- Stack state: `hkChatEnter` is mid-CUI-event-dispatch when it calls enableTextInput; the editor button is at top-of-stack.
- DirectInput state: input-pump cycle vs idle.

If the button path also fullscreens → it's the args / enableTextInput downstream.
If only Enter path fullscreens → it's the dispatcher / DirectInput-pump context.

This is the first test to run next session.

---

## Suspect code paths to read

| File | Lines | Why |
|---|---|---|
| `UtinniCore/swg/ui/cui_chat_window.cpp` | 481-495 | hkChatEnter (Phase H detour) |
| `UtinniCore/swg/ui/cui_chat_window.cpp` | 326-348 | forceOpenChatInputFromCpp (compare path) |
| `UtinniCore/swg/ui/cui_chat_window.cpp` | 400-424 | hkEnableTextInput (the detour wrapping enableTextInput) |
| `UtinniCore/direct_input/*` | * | DirectInput SetCooperativeLevel vtable shim (commit `18e79c3`) |
| `UtinniCore/client/*` | * | Client::suspendInput/resumeInput, Client::setHwnd (commit `f5fa073`) |

Commits to re-read with this lens:
- `6047416` — Phase H chatEnter detour (the active fix)
- `18e79c3` — DirectInput SetCooperativeLevel shim
- `74f64fc` — dropped HWND override that broke DirectInput cooperative level
- `f5fa073` — input lifecycle diag

---

## Verification steps for next session

1. **Build at `9c8edd3` (Phase 3 close, pre-R-A) and smoke chat-open via Enter.**
   - If fullscreen → pre-existing, the R-A migration is fully clean.
   - If no fullscreen → R-A regression somehow; deep-dive `imgui_impl.cpp` render dispatch ordering.

2. **On current master (`765738c` + handoff), smoke chat-open via the editor's chat button (if one exists / `forceOpenChatInputFromCpp` is reachable from UI).**
   - If fullscreen → args / enableTextInput downstream.
   - If no fullscreen → dispatcher context / DirectInput-pump state.

3. **Add diag logging in `hkChatEnter`** — log DirectInput cooperative-level state and D3D9 device present-params (`Windowed` flag) before and after `enableTextInput` call. Confirms whether the device-mode switch is synchronous to the enableTextInput call or fires later (e.g., next message-pump frame).

4. **Probe alternate args.** Try `enableTextInput(pThis, true, false, false)` (no setKeyboardInput) — does chat still open? Does fullscreen still trigger? Isolates the `setKeyboardInput=true` arg as cause.

---

## Cross-references

- [[project-swg-context-routing]] — original Issue #11 context-routing fix that Phase H landed
- [[feedback-d3d9-reset-third-party]] — don't call Reset on third-party device; relevant if any new code attempts to re-embed by Reset
- [[feedback-owned-popup-zorder]] — Z-order quirks when reparenting owned popups
- [[project-d3d11-migration]] — SWG Source D3D11 work; window-mode behavior likely diverges further on D3D11
- Phase 3 close: `.planning/SESSION-HANDOFF-2026-05-22-NIGHT.md`
- R-A migration: `.planning/SESSION-HANDOFF-2026-05-23.md`
- Commit `6047416` — Phase H chatEnter detour
- Commit `18e79c3` — DirectInput SetCooperativeLevel shim

---

## Phase scoping hint

This is its own bug, not a sub-task of Phase 4. Suggested as a dedicated debug-phase between Phase 4 and Phase 5 if severity stays low. If user-blocking on next chat session, promote ahead of Phase 4.

Rough estimate: 1 session (verify pre-existing vs regression, run diag probes, draft fix) + 1 session (implement + smoke + close). Could collapse to 1 if first probe is decisive.

*Written 2026-05-23 after R-A migration smoke. Untracked working notes — retain for grep-back.*
