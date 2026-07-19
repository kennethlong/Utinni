# Phase 24 — Session Handoff (2026-07-18): Goal B Waves 1–3 COMPLETE + functional; open item = embed-render-sizing (gizmo aspect)

Marathon session. **Goal B (Snapshot editor on the advertised NGE client) is code-complete across all
three waves and functional end-to-end** — read/browse, live mutation, and persistence all work on the
advertised client. ~10 advertised-client bugs found and fixed (several via live cdb attach). The one
open functional item is a **render-aspect/window-scaling problem** on the embedded advertised client that
distorts the view and miscalibrates the gizmo hit-test; the fix direction is **DECIDED** (render tracks
the embed) and needs a **crew design session** before implementing.

**Repo state (both clean):** Utinni `96b927b` · UtinniPlugins `199fce8` · provider swg-client-v2 has
Goal B commits LOCAL (Kenny pushes: `fb32e1c64` W1, `85877bae4` W2, `4a8b5d605` W2-diag, `d7dba07a6`
id-mint, `835ad389c` occupancy, `fd06a2ad6` W3).

---

## 0. What is TRUE now (advertised NGE client, `SwgClient_r.exe`, rasterMajor=5 / D3D9)

Contract **v19 / 140 names / 138 bound**. All three waves smoke-verified except the gizmo aspect polish:

- **Wave 1 (read/browse) — SMOKE PASSED.** Placements table populates from the live snapshot via 7
  id-keyed `worldSnapshot::wsGet*` rows; 5449/5449 authored naboo rows; generation counter bumps on
  scene change. `utinni::WorldSnapshotLive` facade + `WorldSnapshotNodeInfo` POD.
- **Wave 2 (live mutation) — SMOKE PASSED.** Add / Remove / Duplicate (button) / Radius + Undo/Redo all
  work. `wsAddObject/wsAddNodeAt/wsRemoveNode(tri-state 1/0/-1)/wsSetNodeRadius/wsConfigureIdAllocator`.
  `LiveWorldSnapshotNodeRecord` = id-keyed undo (one-batch subtree replay).
- **Wave 3 (persistence) — WIRED, save/reload cycle not yet smoke-confirmed.** `wsSaveSnapshot` (typed
  `SaveResult` enum → distinct SysMsg), `wsGetSavePath` (→ managed property `SavePath`), `wsUnloadSnapshot`,
  `reloadSnapshot` (native unload + advertised load via terrain-derived scene name). Panel Save/Unload/
  Reload branch on `IsPersistenceAvailable`.
- **Riders:** targeting filter (`cuiPreferences::{set,get}AllowTargetAnything`) replaces the guarded-off
  byte-patch; gizmo camera accessors (`camera::getProjectionMatrix` float[16], `getTransformO2W` float[12]).
- **Gizmo:** renders + tracks + moves the object visually on advertised. TWO residual issues below.

## 1. THE OPEN ITEM — embed render-aspect (gizmo hit-test + visible distortion). CREW DESIGN SESSION.

**Symptom:** the advertised view is visibly stretched/distorted; the gizmo hit-test is miscalibrated
(axes don't track the cursor; gizmo unreachable near the bottom). **This is NOT a code-mapping bug and
NOT DX11 — rasterMajor=5 is the D3D9 path.**

**Diagnosis (from the window-handle audit diag still in `imgui_impl.cpp` newFrame, and cdb):**
- getSwgHwnd's client rect == io.DisplaySize == the embed window (1455x1040 on Kenny's box) → NOT a
  handle mismatch; the RT-space mouse scale is internally consistent.
- ROOT = the **ASPECT CHAIN is distorted 3 ways**: projection aspect **1.951** (proj[1][1]/proj[0][0])
  != pinned backbuffer **1600x900 = 1.778** (client.cfg `screenWidth/screenHeight`, chosen for
  windowed-fit + RenderDoc, NOT to match the embed) != embed window **1455x1040 = 1.399**.
- The pinned client.cfg resolution is divorced from the maximized-Utinni-minus-TJT-panel embed geometry;
  the non-uniform present-stretch distorts, and the gizmo mapping fights that distortion.

**DECISION (Kenny):** fix approach = **RENDER TRACKS THE EMBED** — the backbuffer + projection follow the
actual embed window so the view is undistorted at any resolution and the gizmo maps 1:1 (RT == window,
no scaling). NOT letterbox, NOT a fixed cfg value. Standard: **gizmos ship only when a drag on every axis,
anywhere, tracks perfectly — no half-working version.** ([[feedback_gizmo_no_half_working]].)

**PLAN / crew-design inputs:**
- D3D9 (rasterMajor=5) can't Reset a third-party device (never-Reset, [[feedback_d3d9_reset_third_party]])
  → can't resize the backbuffer AFTER creation. BUT the maximized-Utinni embed window exists BEFORE the
  game creates its device → **set the game's resolution to the embed CLIENT SIZE at STARTUP** (before
  device/swapchain creation) instead of pinned 1600x900. Then backbuffer == embed window → present 1:1,
  undistorted, projection aspect follows the resolution → gizmo maps 1:1.
- Implementation surface (design this with the crew): intercept/override the device-create resolution
  (launcher, or the D3D9 device-create hook) with `GetClientRect` of the embed hwnd; OR write
  screenWidth/Height into the effective config from the measured embed size before `Graphics::install`.
  Keep windowed-valid + RenderDoc constraints (why 1600x900 was chosen).
- Where does projection aspect **1.951** come from? It matches neither backbuffer (1.778) nor window
  (1.399) — anomalous; the crew should sanity-check the game's camera-aspect derivation (provider side).
- Caveat: tracks embed size AT LAUNCH (maximized = fixed); post-launch Utinni resize won't re-track
  without Reset — acceptable for the maximized workflow; full dynamic resize = deeper RNDR-04.
- On RT == embed window: revert the RT-space mouse override + DisplaySize override in `imgui_impl.cpp`
  newFrame to a no-op (they compensate a stretch that no longer exists); SetRect = full RT = window;
  strip the `gizmo-diag` log; smoke the gizmo drag on every axis.
- **Substantial architectural pass (device-create resolution interception). Do it fresh, crew-reviewed.**

**Crew consult:** Codex (`codex exec --skip-git-repo-check -`), Cursor (`& cursor-agent.cmd -p --mode ask
--trust`), in-harness Agent (`sonnet`/`opus`). Ask: the startup-resolution-from-embed approach on D3D9
never-Reset; the device-create interception point; the 1.951 projection-aspect anomaly; whether to
involve the provider (camera aspect / a resolution hook) vs consumer-only launcher/device-hook.

## 2. Other open items (tracked, lower priority)

- **positionAndRotationChanged** (§5.6 write-notify) is an unadvertised SWGEmu RVA (0x00B22A50) → guarded
  no-op on advertised (`71cb019`); gizmo drag moves the object VISUALLY but spatial bookkeeping
  (sphere-tree/portal/collision) is deferred. Provider row requested (`b66d4bf`). On the row: drop the guard.
- **cuiRadialMenuManager::clear** — SWGEmu-only RVA, skipped on advertised (`b3144e7`); a provider row
  would restore the radial-menu-clear-on-gizmo nicety (optional).
- **camera::getViewport** — provider row wanted for the EXACT 3D viewport (currently aspect-derived);
  folds into the aspect design.
- **Keyboard routing** — on the advertised embed, Enter sometimes triggers SWG's window-level fullscreen
  restyle and the resulting size change isn't tracked correctly by the Utinni client (feeds the same
  RT-vs-window desync). Related to [[project_swg_context_routing]], WS-0/WS-2 Enter-mask. Own focused pass.
- **"Out Of Range" red target marker** over an added snapshot object — ADVERTISED-ONLY (does NOT happen
  on SWGEmu-embed → NOT server-desync; my initial theory was wrong). Cosmetic NGE target/HUD behavior for
  a client-only tangible. Cosmetic, doesn't affect editing.
- **Wave-3 save/reload live smoke** still owed: SavePath → Save (file on disk at root) → edit → Save →
  Unload → Reload → edit SURVIVED; provider self-test key `[ClientGame/WorldSnapshot] wsSelfTestSaveOnLoad=1`.
- **SWGEmu D-00 regression smoke** owed on the next SWGEmu session (native SWGEmu paths byte-unchanged).

## 3. Durable gotchas learned this session

- **cdb holds UtinniCore.pdb** after a dump-analysis session → next build fails LNK1201/LNK1104.
  `Get-Process cdb,SwgClient_r | Stop-Process -Force` before rebuilding.
- **Live-crash recipe (nailed 3 bugs):** `cdb.exe -p <pid> -y bin\Release -c "sxe av; g; r; kb 40;
  .dump /ma <path>; q"` in run_in_background; user toggles the repro after "ready"; then `cdb -z <dump>`
  + `.ecxr` / `u <addr>` / `dds <ebp>`. WER LocalDumps needs admin (denied); live attach works.
- **PS 5.1 `git commit -m @'...'@`** breaks on embedded double-quotes (arg splits → pathspec error).
  Keep commit messages quote-free.
- **CppSharp** turned `getSavePath()` into a PROPERTY `SavePath`, and `isXxx()` bools into properties —
  check the generated surface for property-vs-method after adding facade methods.
- **Class of bug — unlocking a dark feature wakes latent SWGEmu-RVA callees** gated behind its enabled
  state (radial-menu clear, positionAndRotationChanged): audit the newly-reachable path's callees, not
  just the feature's own code.
- **Class of bug — advertised shims are game-thread-only CONTRACTS**: audit shared managed handlers
  (OnTarget) for UI-thread entry paths; SWGEmu raw reads tolerated it, shims crash.
- **Keyboard hotkeys are DEAD on the advertised embed** (the game window eats keys via DirectInput before
  the WinForms overlay); every editor op needs a UI BUTTON. Also `FormHotkeyEditor.btnSave` checked
  DialogResult.Yes on an OKCancel dialog → rebinds never persisted (fixed `c591026`).

## 4. Pointers
- Memory: [[project_phase24_editor_unlock_inflight]] (full ledger, updated), [[feedback_gizmo_no_half_working]]
  (the standard + the aspect decision + plan), [[feedback_d3d9_reset_third_party]], [[project_swg_context_routing]].
- Provider handbacks (mirrored under this phase dir): `24-PROVIDER-HANDBACK-goalB-wave{1,2,3}.md`,
  `-wave2-add-diagnostics`, `-wave2-idmint-round2`. Requests: `-wave{1,2,3}-*-REQUEST`,
  `-positionchanged-row`, `-occupancy-guard-not-firing`.
- Diag currently in tree: `imgui_impl.cpp` newFrame `gizmo-diag` (bounded, advertised-only) — strip when
  the aspect fix lands.
