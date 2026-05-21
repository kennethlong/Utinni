# Session Handoff: 2026-05-21 (Morning)

> Written ~09:50 to close out a focused morning that landed Issue #10 Phase B-bis (proper window sizing + Z-order) AND closed Issue #1 (WR-03 exit dialog) as an implicit side effect of Phase B ownership. SWG now resizes with PanelGame end-to-end with a clean shutdown. The diagnostic chain (Reset crash → COPY discovery → Z-order fix) replaced two wrong hypotheses with a one-line fix. Master at `c7b3141`. Supersedes `SESSION-HANDOFF-2026-05-20-EVENING.md`.

## TL;DR

**Two issues closed in three commits:**

- **#10 Phase B-bis LANDED** (`e9025fc` + `1789400`) — SWG window resizes with PanelGame. Drop `SWP_NOSIZE`, drop `SWP_NOZORDER`, pass `HWND_TOP`. SWG fills PanelGame at any size; drag still tracks; FormMain keeps focus.
- **#1 WR-03 exit dialog RESOLVED implicitly** (`c7b3141`) — clean exit chain this morning, no dialog, no dump. Caused by Phase B's `GWLP_HWNDPARENT` ownership changing the shutdown sequence (FormMain now drives clean teardown of the owned-window group).

**Issue #10 (SWG window reparenting) is now FULLY RESOLVED.** Phases A + B prereq + B core + B-bis all LANDED. The original V1 visual goal — SWG embedded in editor as one app — is complete.

Live-verified end-to-end on a real SWGEmu session: reparent + resize + drag + login + Naboo scene + WASD movement + chat enter + Esc untarget + 3 scene transitions + clean /quit.

## Commits shipped this session (3 total)

```
c7b3141 docs(state): #1 WR-03 exit dialog RESOLVED implicitly by Phase B ownership
1789400 feat(editor): #10 Phase B-bis -- SWG resizes with PanelGame; HWND_TOP Z-order fix
e9025fc feat(graphics): #10 Phase B-bis -- log pSourceRect/pDestRect/SwapEffect on first Present
```

All on `origin/master` at `kennethlong/Utinni`.

## The diagnostic chain (the morning, in order)

Two wrong hypotheses, both disproved by live testing, before the real one-line fix.

### Hypothesis 1: "Reset() the backbuffer to new dims" (FROM HANDOFF) — WRONG

The 2026-05-20-EVENING handoff sketch said: trigger `IDirect3DDevice9::Reset(&pp)` with new `BackBufferWidth/Height` matching `PanelGame.ClientSize`, let `hkReset` invalidate/recreate ImGui via re-entry through the vtable patch.

**Implemented as:** atomic-based pending-dims queue, UI thread enqueues from `PanelGame.Resize`, render thread drains in `hkPresent` after Present.

**First launch result:**
```
[08:55:11] directX::resizeBackbuffer: Reset(735x460) -> 0x8876086C
[08:55:11] [SWG] FATAL 55fd64d9: SetVertexShaderConstantF failed 2156
[08:55:11] VEH int3: EIP=0x00AA1E3F
```

`0x8876086C` is `D3DERR_INVALIDCALL`. D3D9's `Reset()` requires **all default-pool resources released first** — render targets, dynamic VBs, textures with `D3DPOOL_DEFAULT`. SWG owns dozens we can't enumerate and SWG doesn't expose a "release everything for Reset" API. **Reset is fundamentally unworkable for SWG from the outside.** Device went DEVICELOST, next render call (`SetVertexShaderConstantF`) failed, SWG fatal-crashed.

**Action:** ripped out the entire Reset path. Kept the `hkPresent` first-fire diag because it would tell us what SWG actually passes to Present (`pSourceRect`, `pDestRect`, swapchain `SwapEffect`/`BackBufferWidth/Height`). That diag became commit `e9025fc`.

### Hypothesis 2: "D3D9 windowed `COPY` swapchain blocks stretching" — ALSO WRONG

Second launch with diag in place. Diag captured:
```
directX::hkPresent: first fire (block=0, destHwndOverride=0x009F00F4,
                                src=(0,0,1280,1024), dst=NULL
                                bb=1280x1024 fmt=21 effect=COPY windowed=1
                                hDevWnd=0x009F00F4)
```

User reported: SWG invisible. Music playing → SWG is running, render is firing, but nothing showing.

My read of MSDN on `D3DSWAPEFFECT_COPY`: "Both pSourceRect and pDestRect must be the same size as the back buffer." Strict dimension-match required — if backbuffer is 1280×1024 and window client area is 735×460, COPY can't stretch, result is implementation-defined (usually blank).

This was wrong. The MSDN reading is stricter than modern Windows actually behaves. **Modern D3D9 `COPY` swapchains DO handle backbuffer-vs-window dimension mismatch via internal stretching** (or letterboxing, depending on the driver path).

The real signal was buried in the user's next message: **"when I closed the client editor, I saw the swg window was showing behind it at the right location and size."**

That single sentence demolished the COPY hypothesis. SWG was rendering at the right size, in the right position — just invisible because it was buried behind FormMain in Z-order. The COPY swapchain wasn't the problem at all.

### Real bug: owned-popup Z-order doesn't recompute on `GWLP_HWNDPARENT`

Phase B (yesterday) sets FormMain as SWG's owner via `SetWindowLong(swgHwnd, GWLP_HWNDPARENT, ownerHwnd)`. The Win32 owned-window relationship is *supposed* to keep owned windows above their owners in Z-order. **But when you set ownership post-creation, the OS doesn't trigger a Z-order recomputation.** SWG stays at its creation-time Z-position, which was somewhere below FormMain after both windows were created and shown.

The subsequent `SetWindowPos` in `RepositionSwgWindow` used `SWP_NOZORDER` (preserve existing Z-order). So we never actively brought SWG above FormMain. Phase B "worked" only because at native 1280×1024 the SWG window was so large that even buried under FormMain its corners poked out visibly enough to confirm "SWG is reparented" — but the editor sat in front of most of it.

When B-bis dropped `SWP_NOSIZE` and shrunk SWG to 735×460, the entire window now fit behind FormMain's chrome. Hence "invisible."

### The fix (one line, basically)

```diff
- Native.SWP_FRAMECHANGED | Native.SWP_NOZORDER
- | Native.SWP_SHOWWINDOW | Native.SWP_NOACTIVATE
+ Native.SWP_FRAMECHANGED | Native.SWP_SHOWWINDOW | Native.SWP_NOACTIVATE
```

Drop `SWP_NOZORDER`. `hWndInsertAfter` stays at `IntPtr.Zero` (= `HWND_TOP`). `SWP_NOACTIVATE` still set so SWG comes to Z-top without stealing focus from FormMain.

Subsequent FormMain activations carry SWG with them as a group via the natural ownership relationship — that part works correctly, it just needed the initial Z-order set to be right.

## Phase B-bis mechanics (the shipped design)

### `PanelGame.RepositionSwgWindow` (final form)

```csharp
Point screenOrigin = PointToScreen(Point.Empty);
Size cs = ClientSize;
bool ok = Native.SetWindowPos(swgHwnd, IntPtr.Zero,
    screenOrigin.X, screenOrigin.Y,
    cs.Width, cs.Height,                              // was: 0, 0 under SWP_NOSIZE
    Native.SWP_FRAMECHANGED                            // (was: also SWP_NOZORDER)
    | Native.SWP_SHOWWINDOW
    | Native.SWP_NOACTIVATE);
```

Called from:
- `ReparentSwgWindow` (initial reparent)
- `Resize` (FormMain resize → PanelGame docks to fill → SWG follows)
- `OwnerForm.LocationChanged` (FormMain drag)

### Why no D3D9 Reset is needed

`D3DSWAPEFFECT_COPY` on modern Windows handles backbuffer-vs-window dimension mismatch by itself. SWG's backbuffer stays at its native 1280×1024 throughout the session; Present blits/stretches it into whatever client area the SWG HWND currently has. ImGui's `io.DisplaySize` is read from `GetClientRect(hwnd)` each `ImGui_ImplWin32_NewFrame`, so ImGui auto-tracks the resized window too.

This means **no per-resize D3D9 work** at all. Just SetWindowPos.

## Verification matrix (live SWGEmu session, 09:09:18 - 09:11:57)

| Test | Result | Evidence (utinni.log) |
|---|---|---|
| Initial reparent at HWND_TOP + correct size | ✅ | `SetWindowPos(x=608,y=190,w=735,h=460,HWND_TOP) -> OK` (line 52) |
| Visible at PanelGame's exact size | ✅ | user-confirmed |
| Drag FormMain → SWG tracks | ✅ | 7 reposition events with diagonal coord trend (lines 55-61) |
| Click SWG to give it focus | ✅ | `WM_ACTIVATE CLICKACTIVE` + `WM_SETFOCUS` (lines 62-63) |
| Login: WM_CHAR + Tab + Enter | ✅ | 28+ `hkProcessEvent` type=6/7 events for a/d/m/i/n/2/Tab/Enter (lines 64-109) |
| Naboo scene load via TJT panel | ✅ | `hkSetScene: ENTRY (scene=22A8DEB0)` x2 + `firing 1 setSceneCallbacks` (lines 110-118) |
| WASD camera movement | ✅ | 40+ `hkHandleInputEvent type=3 freeCam=1` events (lines 174-254+) |
| Chat: '/' + Enter (display→input mode) | ✅ | `hkChatEnter: chat is in display mode -- overriding to open chat input` x3 (lines 272, 293, 357) |
| Esc → untarget action | ✅ | `hkActionPerformAction: str_a = 'untarget'` (lines 287, 440) |
| Scene transitions (3 cycles) | ✅ | `hkMainLoop: loadNewScene -> cleanupScene -> setupScene` cycles complete (lines 375-430) |
| Clean /quit → process exit | ✅ | `hkCleanupScene: cleanUpSceneCallbacks complete; EXIT` then WM_ACTIVATE/WM_SETFOCUS, log ends cleanly (lines 449-457) |
| **No WR-03 "Direct3D could not be correctly initialized" dialog** | ✅ | no FATAL, no VEH int3, no SWGEmu.exe-stage.*.{txt,mdmp} dump generated by clean exit |

## Issue #1 (WR-03 exit dialog) — implicit resolution

The 2026-05-19 update on Issue #1 noted: *"disappears in passthrough-everything builds; reappears with any detour active — confirms exit dialog is downstream of a Utinni hook interaction with D3D9 lifecycle."*

**Theory why Phase B/B-bis ownership fixed it:**

Pre-Phase-B, SWG was a standalone top-level window owned by no one. Closing FormMain (the editor) just exited the editor's message loop; SWG's process kept running until `clr::stop()` and `Process.Exit` torn it down externally. D3D9 teardown happened at an awkward time relative to detours — detours were still wired up when SWG's own shutdown started, and SWG ran into still-live hooks during its self-release sequence, tripping the dialog.

Post-Phase-B with `GWLP_HWNDPARENT` ownership: closing FormMain triggers WM_CLOSE/WM_DESTROY for the entire **owned-window group**. SWG receives those messages through normal Win32 channels in the right order, runs its own self-shutdown the way it expects to, and D3D9 teardown happens in SWG's natural sequence where detours-still-installed isn't a problem (because SWG drives the operations itself).

The detours weren't the bug. The shutdown order was the bug.

Caveat banked in STATE.md: the 2026-05-18 "no exit dialog" report was a false negative (delayed dialog mistaken for startup). Today's clean exit is from a different code path (full window-group ownership), so the mechanism is genuinely different. Re-open if it ever surfaces again.

## Open after this session

### Working-tree noise (carried over from 3 sessions now)

```
?? .planning/phases/01-ci-tier-1-c-scaffold/01-PATTERNS.md
?? scripts/audit-utinni-rvas.ps1
?? scripts/find-hidden-error.ps1
?? scripts/rva-audit.csv
```

Plus modified `Launcher/Launcher.vcxproj.user` (always-modified VS user file; should probably gitignore).

User's next-session plan: decide commit/gitignore/delete for each. Quick win.

### `hkAppendMessageData msg=0x140` log flood

Floods the log at scene-load — ~50 entries within 1 second when CuiSettings/inventory state loads (visible in today's log lines 162-211). Currently capped somewhere but the cap is high. Trim to first 5 fires or remove entirely. ~5 min.

### Issue #4 (managed CLR exception 0xE0434352) — likely already obsolete

STATE.md says it was hypothesized as a downstream consequence of the EB FE halt (Issue #6) and hasn't reproduced since. Today's successful Naboo scene load + 3 scene transitions + clean /quit further reinforces "likely obsolete." Mark fully obsolete next session unless it surfaces.

## Diag state (kept in source as tripwires)

All prior Phase A-H diag logs retained from `SESSION-HANDOFF-2026-05-20-EVENING.md`. New additions this session:

- `directx9.cpp hkPresent` first-fire (commit `e9025fc`): pSourceRect/pDestRect contents (NULL-tolerant), GetSwapChain probe for BackBufferWidth/Height, SwapEffect decoded (DISCARD/FLIP/COPY/OTHER), Windowed flag, hDeviceWindow. Fires once per session at first Present. Durable regression detector — if SWG ever ships a different swapchain config it'll be obvious in the first log line.

- `PanelGame.RepositionSwgWindow` (commit `1789400`): log message now includes `w=..,h=..,HWND_TOP` instead of `NOSIZE`. Capped at 8 fires per session.

## Files referenced this session

### Source edits (committed)

- `UtinniCore/swg/graphics/directx9.cpp` — hkPresent first-fire diag for Present args + swapchain effect (commit `e9025fc`)
- `UtinniCoreDotNet/UI/Controls/PanelGame.cs` — drop SWP_NOSIZE + drop SWP_NOZORDER + HWND_TOP (commit `1789400`)
- `.planning/STATE.md` — Issue #10 marked FULLY RESOLVED; Issue #1 marked RESOLVED with ownership-shutdown theory (commits `1789400` + `c7b3141`)

### Files read for understanding (no edits)

- `UtinniCore/swg/graphics/directx9.h` — confirmed header signatures for directX namespace
- `UtinniCore/swg/ui/imgui_impl.cpp` — confirmed hkPresent call order (`render -> present -> depth -> setup`)
- `external/imgui/imgui_impl_dx9.cpp` — confirmed Invalidate/Create safely no-op on `!g_pd3dDevice`, so pre-setup drain wouldn't have crashed (moot — drain was ripped out anyway)
- `UtinniCore/swg/graphics/graphics.cpp` — confirmed `getCurrentRenderTargetWidth/Height` reads from globals at `0x1922E64` / `0x1922E60` (irrelevant once Reset path was abandoned)
- `scripts/local-test-setup.ps1` — confirmed MSBuild invocation pattern

### Files temporarily edited then reverted

- `UtinniCore/swg/graphics/directx9.cpp` — added then removed `<atomic>` include, `g_pendingResizeDims` atomic, `directX::resizeBackbuffer` function, `drainPendingResizeOnRenderThread` helper, `resizeSwgBackbuffer` extern "C" export (the entire Reset plumbing). Removed cleanly per CLAUDE.md "no // removed comments" guidance.
- `UtinniCore/swg/graphics/directx9.h` — added then removed `resizeBackbuffer` declaration
- `UtinniCoreDotNet/Utility/Native.cs` — added then removed `ResizeSwgBackbuffer` P/Invoke

User noted mid-session preference: **during in-progress feature iterations, prefer leaving scaffolding/exports/plumbing around until the feature is fully working** — saved as `feedback_keep_scaffolding_wip.md` in auto-memory. For the next iteration, default to commenting-out / gating / leaving-unwired over deleting until end-to-end verified.

## Key learnings (memory candidates)

Two patterns worth banking for future sessions:

1. **D3D9 `Reset()` on a third-party app's device is fundamentally unworkable.** The app holds default-pool resources you can't enumerate. Reset returns `D3DERR_INVALIDCALL`, leaves the device in DEVICELOST, the app's next render call fails, the app fatal-crashes. Don't try to retrofit Reset from the outside — find the app's internal video-resolution code path, or sidestep Reset entirely (D3D9 windowed Present handles dimension mismatches itself on modern Windows). Saved to a memory.

2. **Win32 owned-popup Z-order is NOT automatically reordered when you set `GWLP_HWNDPARENT` post-creation.** SWG window stays at its creation-time Z. To bring it above the owner, do `SetWindowPos(..., HWND_TOP, ..., SWP_NOACTIVATE)` (drop `SWP_NOZORDER`). After that the ownership relationship maintains Z-order through subsequent owner activations. Saved to a memory.

(Both saved as separate memory files; see auto-memory MEMORY.md.)

## Next-session entry point

Per user's intent:

1. **Quick-win cleanup (~30 min)**:
   - Working-tree noise (`scripts/audit-utinni-rvas.ps1`, `find-hidden-error.ps1`, `rva-audit.csv`, `.planning/phases/01-ci-tier-1-c-scaffold/01-PATTERNS.md`): decide commit/gitignore/delete for each.
   - Add `Launcher/Launcher.vcxproj.user` to `.gitignore` if not already.
   - Trim `hkAppendMessageData msg=0x140` log flood at scene-load.

2. **V1 feature work — TJT subpanels** (per DEC-C4: Wave-1 phases 7-11 ship inside The Jawa Toolbox, not separate plugins):
   - `/gsd-discuss-phase 02.1` or equivalent to start the first subpanel (likely TRE viewer per ROADMAP).
   - The integration plumbing (window reparenting, DirectInput, Enter handling, scene load, clean shutdown) is now genuinely stable enough for feature work.

Master at session end: `c7b3141`. All commits pushed to `origin/master` at `kennethlong/Utinni`.
