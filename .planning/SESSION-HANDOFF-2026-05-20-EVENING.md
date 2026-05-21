# Session Handoff: 2026-05-20 (Evening)

> Written 2026-05-20 ~21:30 to wrap a focused evening session that landed Issue #10 Phase B — borderless owned-popup reparent. SWG's top-level window now lives inside the editor's PanelGame, frameless, owned by FormMain, and drag-tracks the editor. DirectInput coop-level baseline captured + survives reparent unchanged. Phase H Enter intact. Position-only iteration; D3D9 reset for proper sizing deferred to Phase B-bis. Supersedes `SESSION-HANDOFF-2026-05-20-PM.md`. Master at `2ce028c`.

## TL;DR

One issue moved (#10), in two atomic commits:

- **#10 Phase B prereq (`18e79c3`)** — DirectInput `SetCooperativeLevel` vtable shim. Three-stage COM shim: `dinput8.dll!DirectInput8Create` function detour → `IDirectInput8A` vtbl[3] CreateDevice patch → `IDirectInputDevice8A` vtbl[13] SetCooperativeLevel patch. Captures `hwnd` + decoded `DISCL_*` flags + caller PC. **Baseline:** both keyboard and mouse use `NONEXCLUSIVE | FOREGROUND (0x6)` — exactly as CODEX predicted. Durable instrumentation; left in source as regression detector.
- **#10 Phase B core (`2ce028c`)** — owned-popup reparent. PanelGame strips frame styles (`WS_CAPTION | WS_THICKFRAME | WS_MIN/MAX/SYSMENU | WS_BORDER | WS_DLGFRAME`), keeps `WS_POPUP`, sets FormMain as owner via `GWLP_HWNDPARENT`, positions over PanelGame's screen-coord client origin with `SWP_NOSIZE` (size deferred to B-bis). Poll timer (100ms) waits for `Native.GetSwgHwnd() != IntPtr.Zero` then fires once. `OwnerForm.LocationChanged` + `PanelGame.Resize` re-trigger reposition. New C-linkage export `getSwgHwndExport` because CppSharp drops pointer-return getters.

Live-verified end-to-end on a real SWGEmu session: visual reparent + DirectInput survival + Phase H Enter + login keyboard flow + drag-tracking all confirmed.

## Commits shipped this session (2 total)

```
2ce028c feat(editor): #10 Phase B -- reparent SWG into PanelGame via owned popup
18e79c3 feat(input): #10 Phase B prereq -- DirectInput SetCooperativeLevel vtable shim
```

## Phase B mechanics (the shipped design)

### Hook chain (prereq commit `18e79c3`)

```
DirectInput::detour()  [runs in utinni_init, before SWG main thread released from EB FE]
  ├─ Detour::Create(dinput8.dll, DirectInput8Create, hkDirectInput8Create, PUSH_RET)
  │
  hkDirectInput8Create (function detour)  [fires once during SWG's setupInstall]
    ├─ call origDirectInput8Create -> IDirectInput8A* di8
    ├─ VirtualProtect(di8->vtbl[3], RW) + swap CreateDevice -> hkCreateDevice
    │
    hkCreateDevice (vtable patch)  [fires twice: kbd, mouse]
      ├─ call origCreateDevice -> IDirectInputDevice8A* dev
      ├─ patchDeviceVtableOnce(dev) - first device only:
      │    VirtualProtect(dev->vtbl[13], RW) + swap SetCooperativeLevel -> hkSetCooperativeLevel
      │  (kbd + mouse share the same vtable in dinput8, so one patch covers both)
      │
      hkSetCooperativeLevel (vtable patch)  [fires twice: kbd, mouse]
        └─ log hwnd + decoded DISCL_* flags + caller PC, call through
```

`dxguid.lib` linked for `GUID_SysKeyboard` / `GUID_SysMouse` symbol bodies; no static `dinput8.lib` dep and no `CoInitializeEx` needed (DI8 is in-proc, not COM-activated).

### Reparenting flow (core commit `2ce028c`)

```
PanelGame ctor
  ├─ create ReparentPollTimer (Interval=100ms)
  ├─ HandleCreated +=  start poll timer + cache FindForm() as ownerFormCached
  │                    + subscribe ownerFormCached.LocationChanged
  ├─ Resize +=         RepositionSwgWindow (no-op if !swgReparented)
  └─ Disposed +=       stop+dispose timer, unsubscribe LocationChanged

ReparentPollTimer_Tick (100ms)
  ├─ if swgReparented: stop+return
  ├─ if !IsHandleCreated || !ownerForm.IsHandleCreated: return
  ├─ swgHwnd = Native.GetSwgHwnd()   [P/Invoke -> getSwgHwndExport in client.cpp]
  ├─ if swgHwnd == IntPtr.Zero: return
  └─ ReparentSwgWindow(swgHwnd, ownerForm.Handle) + stop timer

ReparentSwgWindow
  ├─ style = GetWindowLong(GWL_STYLE)
  ├─ SetWindowLong(GWL_STYLE, (style & ~frameMask) | WS_POPUP)
  ├─ SetWindowLong(GWLP_HWNDPARENT, ownerHwnd)  [owner-set, NOT parent-set]
  ├─ swgReparented = true  [MUST flip before Reposition, else early-return]
  └─ RepositionSwgWindow()

RepositionSwgWindow  [called from initial reparent + LocationChanged + Resize]
  └─ SetWindowPos(swgHwnd, NULL,
       PointToScreen(Point.Empty).X, .Y,
       0, 0,                                            [ignored under SWP_NOSIZE]
       SWP_NOSIZE | SWP_FRAMECHANGED | SWP_NOZORDER
       | SWP_SHOWWINDOW | SWP_NOACTIVATE)
```

## Verification matrix (live SWGEmu session)

| Test | Result | Evidence |
|---|---|---|
| Visual reparent (frameless, PanelGame top-left, owned) | ✅ | user-confirmed; log: `oldStyle=0x16C80000 newStyle=0x96000000` (POPUP\|VISIBLE\|CLIP*) |
| Vtable shim captures DirectInput baseline | ✅ | `NONEXCLUSIVE \| FOREGROUND (0x6)` for both kbd+mouse, callers `0x0041E5C5` / `0x0041EC1A` (SWG-internal SetupKeyboard/SetupMouse) |
| DirectInput survives reparent | ✅ | NO second `SetCooperativeLevel` call appears in log after reparent — owned-popup preserved top-level identity, DI binding intact |
| Phase H chatEnter override survives | ✅ | log: `hkChatEnter: chat is in display mode -- overriding to open chat input (was: submit+close)` |
| Keyboard input flows (login dialog) | ✅ | 28+ `hkProcessEvent` type=6/7 events for chars `a/d/m/n/i/2`, VK_TAB (0x09), VK_RETURN (0x0D) |
| Clean `/quit` exit | ✅ | `hkCleanupScene: cleanUpSceneCallbacks complete; EXIT` then process closes |
| Drag FormMain → SWG window tracks | ✅ | 8 `RepositionSwgWindow: SetWindowPos(x=...,y=...,NOSIZE) -> OK` entries, coords trending diagonally as form was dragged |
| Resize FormMain → SWG resizes | ⏸️ Deferred | Phase B-bis (needs D3D9 Reset) — current iter is SWP_NOSIZE on purpose |
| WASD movement | ⏸️ Not log-confirmed | DirectInput polling has no instrumentation; successful login + /quit strongly imply working |

## False starts (worth keeping in mind for future similar work)

### Iter 1: full SetWindowPos with PanelGame.ClientSize → BLACK WINDOW

The first attempt resized SWG to PanelGame's `ClientSize`. Symptom: SWG window appeared at the right place + frameless + owned, but rendered solid black. User blindly typed login, eventually got content (probably after a scene transition forced a redraw), then closed.

**Diagnosis:** D3D9's swapchain was created at SWG's native dimensions (`hkPresent: destHwndOverride=0x004412CE`). Windowed D3D9 doesn't auto-rescale `Present()` to match a resized window; the app has to explicitly call `IDirect3DDevice9::Reset()` with new `BackBufferWidth/Height` and rebuild ImGui's device objects. CODEX's consult #3 note — "D3D9 transparent passthrough, hkReset handles WM_SIZE naturally" — was wrong on this point. `hkReset` only fires on explicit `Reset()` calls, not on `WM_SIZE`.

**Fix:** `SWP_NOSIZE` for now, deferring proper sizing to Phase B-bis.

### Iter 0: ordering bug — early-return on `!swgReparented` swallowed initial Reposition

First implementation set `swgReparented = true` in `ReparentPollTimer_Tick` AFTER calling `ReparentSwgWindow`, but `ReparentSwgWindow` calls `RepositionSwgWindow` internally — which guards on `!swgReparented` and early-returned. Net result: style changed but `SetWindowPos` never fired, so `SWP_FRAMECHANGED` never applied (frame stayed visible) and window never moved over PanelGame.

**Fix:** flip `swgReparented = true` inside `ReparentSwgWindow` BEFORE calling `RepositionSwgWindow`.

## Open after this session

### Issue #10 Phase B-bis (next session, primary target)

Trigger `IDirect3DDevice9::Reset()` with new `BackBufferWidth/Height` matching PanelGame's `ClientSize` after reparent (and on subsequent PanelGame resize).

Sketch:
1. Capture the `IDirect3DDevice9*` (we already have this from existing D3D9 detours in `directx9.cpp`).
2. Add a `directX::resizeBackbuffer(int w, int h)` function:
   - Build fresh `D3DPRESENT_PARAMETERS` from the current swapchain (`device->GetSwapChain(0, &sc)` + `sc->GetPresentParameters(&pp)`).
   - Set `pp.BackBufferWidth = w`, `pp.BackBufferHeight = h`.
   - Call `ImGui_ImplDX9_InvalidateDeviceObjects()`.
   - Call `device->Reset(&pp)`; loop on `D3DERR_DEVICELOST` until `D3DERR_DEVICENOTRESET` or `D3D_OK`.
   - Call `ImGui_ImplDX9_CreateDeviceObjects()`.
3. Add an extern "C" export `resizeSwgBackbuffer(int w, int h)` for managed-side P/Invoke.
4. PanelGame.Resize triggers both `RepositionSwgWindow` and `resizeSwgBackbuffer(ClientSize.Width, ClientSize.Height)`.
5. Remove `SWP_NOSIZE` flag in `RepositionSwgWindow` so the window itself also resizes.

**Concerns to anticipate:**
- Reset must run on the render thread, not the UI thread. Probably need to enqueue the resize for the next render frame (similar to how `BlockPresent` works).
- ImGui might need its display size updated via `io.DisplaySize = ImVec2(w, h)`.
- Test the round-trip with progressively larger windows (esp. when crossing the SWG-native dimensions in either direction).

### Working-tree noise (untracked, same as morning handoff)

```
?? .planning/phases/01-ci-tier-1-c-scaffold/01-PATTERNS.md
?? scripts/audit-utinni-rvas.ps1
?? scripts/find-hidden-error.ps1
?? scripts/rva-audit.csv
```

Plus modified `Launcher/Launcher.vcxproj.user` (always modified — VS user-specific).

Decide whether to commit, gitignore, or delete next session.

### Diag verbosity (still a quick-win opportunity)

`hkAppendMessageData msg=0x140` floods the log at scene-load — ~50 entries within 1 second. Either filter or reduce cap. ~5 min cleanup whenever convenient.

### Issue #1 (still open) — WR-03 exit dialog

Disappears in passthrough-everything builds, reappears with any detour active. Exit-only nuisance. Lowest priority.

## Diag state (kept in source as tripwires)

All prior Phase A-H diag logs are retained from `SESSION-HANDOFF-2026-05-20-PM.md`. New additions this session:

- `direct_input.cpp` Phase B prereq: `DI::DirectInput8Create`, `DI::patched IDirectInput8A::CreateDevice vtbl[3]`, `DI::CreateDevice`, `DI::patched IDirectInputDevice8A::SetCooperativeLevel vtbl[13]`, `DI::SetCooperativeLevel` (every call — kbd + mouse, ~2 entries per session at startup).
- `PanelGame.cs` Phase B core: `PanelGame: reparented SWG hwnd=... owner=... oldStyle=... newStyle=...` (fires once); `PanelGame.RepositionSwgWindow: SetWindowPos(x=...,y=...,NOSIZE) -> OK/FAIL` (capped at 8 fires per session — first 8 of the reparent + drag chain).

The vtable shim is durable instrumentation per intent — if a future change inadvertently switches SWG to `EXCLUSIVE` or strips `FOREGROUND`, the log flags it within microseconds of startup.

## Files referenced this session

### Source edits (committed)

- `UtinniCore/swg/misc/direct_input.cpp` — DI vtable shim (commit `18e79c3`)
- `UtinniCore/swg/client/client.cpp` — `getSwgHwndExport` C-linkage export (commit `2ce028c`)
- `UtinniCoreDotNet/Utility/Native.cs` — user32 P/Invokes (`GetWindowLong`, `SetWindowLong`, `SetWindowPos`) + `GetSwgHwnd` binding + GWL/WS/SWP constants (commit `2ce028c`)
- `UtinniCoreDotNet/UI/Controls/PanelGame.cs` — reparent poll timer + `ReparentSwgWindow` + `RepositionSwgWindow` + Resize/LocationChanged wiring (commit `2ce028c`)
- `UtinniCoreDotNet/Generated/UtinniCore.cs` — regenerated to pick up Phase A `setSwgHwnd` header + prior-session adds (commit `2ce028c`)
- `.planning/STATE.md` — Issue #10 updated with Phase B LANDED + Phase B-bis PENDING

### Files read for understanding (no edits)

- `UtinniCore/utinni.cpp` — confirmed `Client::detour()` runs in `utinni_init` (NOT DllMain), `dinput8.dll` already loaded at that point (statically imported by SWGEmu.exe)
- `UtinniCore/swg/client/client.cpp` — confirmed Phase A `setSwgHwnd` capture point
- `UtinniCore/swg/ui/imgui_impl.cpp` — confirmed `setSwgHwnd` is called from `imgui_impl::setup` after `pDevice->GetCreationParameters`
- `UtinniCore/swg/graphics/directx9.cpp` — `getPresentBlockedEvent` pattern reference for the new `getSwgHwndExport` C-linkage export
- `external/DetourXS/detourxs.h` — confirmed `Detour::Create(LPCSTR module, LPCSTR proc, ...)` overload available for the `dinput8.dll!DirectInput8Create` hook
- `UtinniCoreDotNet/UI/Forms/FormMain.cs` — confirmed `pnlGame.Controls.Add(game)` layout chain + `WaitForPresentBlock` pattern

## Next-session entry point

Suggested order:

1. **Pick up Issue #10 Phase B-bis — D3D9 Reset for proper sizing.** Sketch in "Open after this session" above. This finishes the V1 visual goal (SWG fills PanelGame, drag + resize work end-to-end).
2. **OR** Quick wins (working-tree noise cleanup + `hkAppendMessageData` log-flood trim, ~30 min) for a cleaner workspace before tackling B-bis.
3. **OR** Resume GSD roadmap (`/gsd:discuss-phase 02.1` or `/gsd:plan-phase 02.1`) — boot/scene/input/window-integration pipeline is now stable enough that V1 feature work (TJT subpanels for TRE/IFF/Datatable/Stringtable/Object Template per DEC-C4) can move forward in parallel.

Master at session end: `2ce028c`. All commits pushed to `origin/master` at `kennethlong/Utinni`.
