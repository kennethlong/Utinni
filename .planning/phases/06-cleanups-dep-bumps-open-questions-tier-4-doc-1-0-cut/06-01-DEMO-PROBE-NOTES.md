# 06-01 Demo Probe Notes

**Plan:** 06-01 (Overlay-debug investigation)
**Started:** 2026-05-23
**Owner:** Kenneth Long (kenny.alan.long@gmail.com)

## Investigation Charter

Close the long-standing concern that the imgui in-game overlay has never displayed in Utinni-injected SWG sessions. Per [[feedback-d3d9-hook-diagnosis]], the d3d9.dll pattern-scan check is the 30-second first move BEFORE assuming SWG-side RVA drift or multi-day investigation. Exit gate is intentionally high: `ImGui::ShowDemoWindow` exercising the full Demo screen (menus, sliders, buttons, tabs, plots, popups, drag-and-drop) over a live SWG client.

## d3d9 Pattern-Scan Disposition (Task 1)

**Disposition: ALREADY NOT THE CAUSE. Resolved 2026-05-19 by Phase 02.1 commit `2c57d38`.**

The [[feedback-d3d9-hook-diagnosis]] memory's "test pattern-scan FIRST" advice resolves in 30 seconds here because the broken pattern-scan was already replaced with the dummy-device approach four days before this plan opened.

### Evidence #1 — `UtinniCore/swg/graphics/directx9.cpp::getVtbl()` is the dummy-device path

The current `getVtbl()` body (lines 436-526) creates a throwaway IDirect3DDevice9 via the public D3D9 API, snapshots its vtable, then releases. The vtable entries point into d3d9.dll's `.text` section and remain valid after the dummy device is released because we patch the function bodies there rather than mutating any vtable. The header comment block is explicit:

> 2026-05-19 — Replaced the d3d9.dll code-pattern scan that broke on modern Windows (probe of Win11 24H2 d3d9.dll 6.2.26100.8328 showed the IDirect3DDevice9 vtable is allocated per-instance on the heap, NOT as a static array in d3d9.dll's read-only data — modern d3d9 ships without an .rdata section at all). The new approach creates a throwaway IDirect3DDevice9 via the public D3D9 API, snapshots its vtable, and releases. The method addresses inside the vtable point into d3d9.dll's .text section (verified 119/119 entries) and remain valid after the dummy device is released, because we patch the function bodies there rather than mutating any vtable. This works identically against the SWG Source build and the stock SWGEmu client because both load the OS-provided d3d9.dll.

The function uses `Direct3DCreate9` + a hidden 1x1 `WS_POPUP` window + `CreateDevice(D3DDEVTYPE_HAL, ...)` + a `memcpy(s_vtbl, liveVtbl, sizeof(swgptr) * kD3D9VtblEntries)` snapshot of 119 entries (3 IUnknown + 116 D3D9-specific), then releases the device + destroys the window + releases the D3D9 interface. HAL is mandatory — NULLREF/REF would return a different IDirect3DDevice9 implementation whose addresses don't intercept HAL Present calls.

### Evidence #2 — STATE.md "Blockers/Concerns" item #2

The state file explicitly marks the pattern-scan investigation as resolved:

> **~~D3D9 vtable pattern doesn't match modern d3d9.dll~~ RESOLVED 2026-05-19 (commit 2c57d38)** — Replaced the broken `d3d9.dll` byte-pattern scan in `directx9.cpp::getVtbl()` with the conventional dummy-device approach (`Direct3DCreate9` + hidden 1x1 window + `CreateDevice(HAL)` + read vtable pointer + snapshot 119 entries + release). Proved via probe of buildable SWG Source client that modern `d3d9.dll` (Win11 24H2 6.2.26100.8328) allocates IDirect3DDevice9 vtables per-instance on the heap — no static `.rdata` table exists for pattern scanning. Probe data archived in `.planning/SESSION-HANDOFF-2026-05-19.md`. After this commit, injection log shows no DirectX9 critical errors; D3D9 detours install cleanly.

### Conclusion

Pattern-scan is **not** the cause of the overlay never displaying. The 30-second first move is done; the chain has already moved on. Next probe (Task 1's instrumentation) is to confirm the `isSetup` gate in `imgui_impl.cpp` actually flips true in a live SWG session, and that `render()`'s `if (isSetup)` body actually executes per-frame after that flip. Both diag log lines below are static-bool-guarded so they fire exactly once.

## Task 1 Diag Instrumentation Probes

Two one-shot `utinni::log` calls landed in `UtinniCore/swg/ui/imgui_impl.cpp`:

1. **`info`-level** at the bottom of `imgui_impl::setup(IDirect3DDevice9*)`, guarded by `static bool sLoggedOnce`. Fires the first time `isSetup` flips true. Expected line in utinni.log: `imgui_impl::setup complete, isSetup=true`.
2. **`debug`-level** at the top of the `if (isSetup)` branch in `imgui_impl::render()`, guarded by `static bool sLoggedOnceRender`. Fires the first time render() crosses the gate. Expected line in utinni.log: `imgui_impl::render entered isSetup branch`.

Both probes are dormant after first fire — no per-frame log spam. They survive into production as latent regression detectors and are converted to `debug`-only at Task 3 sign-off.

## Task 2 Diag Probe — ShowDemoWindow Additive

`UtinniCore/swg/ui/imgui_impl.cpp` declares a file-scope `static bool g_showDemoWindowProbe = true;` flag inside `namespace imgui_impl`, and calls `ImGui::ShowDemoWindow(nullptr)` inside `render()` immediately after `ImGui::NewFrame()` and BEFORE the existing renderCallbacks dispatch when the flag is true. The Demo window is purely additive instrumentation — the existing renderCallbacks path (which lives in the `if (!enableUi)` branch) is untouched. Flag flipped to `false` at Task 3 sign-off; call site remains so future Wave-1 work can re-enable by flipping the flag without recompiling everything else.

## Live-SWG Demo Screen Exercise Result (2026-05-23)

**Disposition: YES — Demo screen rendered end-to-end. NO FIX REQUIRED.**

**Maintainer:** Kenneth Long (kenny.alan.long@gmail.com)

### Session Timeline (utinni.log)

The two one-shot tripwires fired exactly once each as designed:

```
[21:50:40] [info]  imgui_impl::setup complete, isSetup=true
[21:50:40] [debug] imgui_impl::render entered isSetup branch
```

### Surrounding Context

- **21:50:37** — detours installed + TJT plugin loaded.
- **21:50:38-39** — SWG init + audio + D3D9 detours installed (7 D3D9 hooks via dummy-device `getVtbl()`); DirectInput vtable shim patched (vtbl[3] CreateDevice, vtbl[13] SetCooperativeLevel).
- **21:50:40** — `imgui_impl::setup complete, isSetup=true` (info) immediately followed by `imgui_impl::render entered isSetup branch` (debug). The render gate flipped within the same second as setup completion. Both static-bool guards consumed exactly one fire each.
- **21:55:37** — clean shutdown: `hkCleanupScene → hkSetScene(null) → cleanUpSceneCallbacks complete; EXIT`. No errors, no SWGEmu-stage `.txt`/`.mdmp` dumps, no fatal codes.

### Demo Widget Categories Verified

All seven widget categories per the D-11 exit criterion behaved as they do in a standalone imgui demo application:

- **Menus** — menu bar opens, submenus cascade, menu items click-able and dispatch.
- **Sliders** — `SliderFloat`, `SliderInt` drag smoothly with live value updates; keyboard editing via Ctrl+Click works.
- **Buttons** — basic + `SmallButton` + `ArrowButton` all dispatch their callbacks; hover styling renders.
- **Tabs** — `TabBar` switches active tab on click; tabs render their distinct content correctly.
- **Plots** — `PlotLines` + `PlotHistogram` render their animated waveforms.
- **Popups** — `OpenPopup` modals appear above the demo window, modal blocking works, dismissal returns control cleanly.
- **Drag-and-drop** — source-to-target drag works; payload preview renders during drag; drop callback fires.

The Demo window itself was draggable + resizable + correctly layered atop the SWG client window per the post-Phase-B-bis owned-popup Z-order work (`SetWindowPos(HWND_TOP, ...)` without `SWP_NOZORDER`, with `SWP_NOACTIVATE`).

### Root-Cause Disposition for the Stale "Imgui Overlay Has Never Displayed" Belief

The render path was healthy by the time Phase 6 opened. The original concern was superseded by three earlier landings:

1. **Phase 02.1 commit `2c57d38` (2026-05-19)** — replaced the broken d3d9.dll pattern-scan with the dummy-device approach in `directX::getVtbl()`. D3D9 detours now install cleanly on modern Windows.
2. **Phase B / B-bis owned-popup window-ownership work (commits `2ce028c` + `1789400` + the Z-order `HWND_TOP` fix)** — SWG's HWND is now correctly owned by FormMain, and the imgui overlay renders into SWG's framebuffer at the correct Z-order.
3. **Phase H chat-context fixes (commit `6047416`)** — keyboard input routing through `hkWndProcHandler` + the chat mediator works, so the Demo's `WantTextInput` path (sliders typed numerically, etc.) functions correctly.

By the time 06-01 picked up the investigation, no separate fix was needed. The 30-second pattern-scan check (Task 1) confirmed already-not-the-cause; the isSetup gate observation (Task 1 probes) and the additive ShowDemoWindow probe (Task 2) confirmed render is alive end-to-end. **D-11 exit criterion satisfied. 06-02 imgui docking-branch switch unblocked.**

### Future Design Note (Out of Scope for 06-01)

**Maintainer directive (2026-05-23):** Wave-1 TJT subpanels target a **HUD-style overlay**, NOT floating windows-on-game-view. The Demo screen exercise visually confirmed render works but also confirmed the aesthetic problem: an opaque framed window sitting atop the SWG view is not the intended UX. Wave-1 plugin styling work starts from this constraint.

The Demo window currently uses Utinni's hard-coded ImGui style at `UtinniCore/swg/ui/imgui_impl.cpp:305-329` with `ImGuiCol_WindowBg = ImVec4(0.13, 0.13, 0.13, 1.00f)` — fully opaque. Implementation options ranked by directive fit:

- **(Primary) HUD-style chromeless windows** — `ImGuiWindowFlags_NoBackground | ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoResize` per-panel. Removes the title bar and frame for true overlay HUDs. Pair with fixed anchored positions (corners / edges) and a configurable visibility toggle. **This is the direction Wave-1 plans should default to.**
- **(Secondary) Per-window alpha** — `ImGui::SetNextWindowBgAlpha(0.0f)` (fully transparent) or `0.35f` (subtly tinted) before `ImGui::Begin(...)`. Useful for panels that need slight backdrop contrast against busy game-view areas without a hard frame.
- **(Style-block override) Global alpha** — drop the alpha of `ImGuiCol_WindowBg` / `ChildBg` / `PopupBg` in the style block to a default-translucent value (e.g., `0.0f` for chromeless or `0.35f` for tinted). Apply once at imgui setup; individual panels can still override via `SetNextWindowBgAlpha`.
- **(Deferred) Frosted-glass via D3D9 backbuffer-copy + blur shader** — frosted-glass aesthetic needs a backbuffer-copy + blur shader before the imgui draw. Stretches into D3D9-pipeline work; defer unless TJT specifically wants the effect for V2 polish.

Capture as a Wave-1 plugin design constraint (TJT subpanels default to HUD-style chromeless overlays). NOT a 06-01 deliverable, but the directive is recorded here so Phase 7+ planning inherits it.

## Task 3 Sign-Off Actions

1. `g_showDemoWindowProbe` flipped from `true` to `false` in `imgui_impl.cpp`. Call site `if (g_showDemoWindowProbe) { ImGui::ShowDemoWindow(nullptr); }` retained so future Wave-1 work can re-enable with a one-line edit.
2. `imgui_impl::setup complete, isSetup=true` log line demoted from `utinni::log::info` to `utinni::log::debug`. The render-entry probe was already at debug level. Both stay behind their static-bool one-shot guards as latent regression detectors.
3. `.planning/codebase/TESTING.md` Tier-4 row "Imgui overlay Demo screen over live SWG" authored with procedure + success criterion + last-verified SHA.
4. `06-01-VERIFICATION.md` authored as the maintainer-signed Tier-4 evidence.
5. CI green confirmation on master is **DEFERRED to post-merge orchestrator validation** — the worktree merge back to master surfaces the commit that the orchestrator runs `gh run list` against.
