# Phase 24 — Crew consult: embed render-sizing (gizmo aspect) — CONSOLIDATED DESIGN

**Date:** 2026-07-18. **Panel:** Codex (ChatGPT), Cursor, Claude Opus, Claude Fable — all four
reviewed the same self-contained brief (candidate designs A–D + 7 questions); Opus and Fable
verified every claim against Utinni + provider source with file:line evidence.

**Unanimous verdict: Design A — post-`--` `@file` config override — with mandatory hardening.**
Ranking A > C > B > D across all reviewers. B (PEB command-line rewrite) is dead: the ANSI
command-line buffer is cached by kernelbase at process init, which already ran before injection
completes — a PEB rewrite doesn't propagate. D (CreateDevice PP rewrite) splits engine state
(config globals/UI metrics vs actual backbuffer). C (provider config-setter row) is mechanically
valid at hkInstall-entry but costs a provider round-trip for what A does with data; keep as fallback.

## Brief corrections found by the panel (all verified)

1. **Ready-event fires BEFORE FormMain exists.** `Native.SignalLauncherReady()` at `main.cs:108`;
   `new FormMain(...)` at `main.cs:165` inside `Application.Run(...)`. Nothing measurable at signal
   time. (Codex, Cursor, Opus, Fable — unanimous.)
2. **Nothing maximizes FormMain at startup.** Ctor sizes from `[Editor] width/height` (defaults
   1200×500, `UtINI/utini.cpp:71-72`); maximize is a manual titlebar toggle (`UtinniForm.cs:249-256`).
   The "maximized 1455×1040" is a post-launch user action. (Cursor, Opus, Fable.)
3. **The @file token cannot carry a spaced path.** `ConfigFile::loadFromCommandLine` delimits
   `@filename` by whitespace, NO quote support (`ConfigFile.cpp:154-168`); the advertised client dir
   (`D:/Code/SWGSource Client v3.0/`) contains spaces. Must use CWD-relative `@utinni_embed.cfg`
   (launcher sets game CWD = client dir, `Launcher/main.cpp:211`). (Fable.)
4. **Silent forced-fullscreen clamp.** `checkDisplayMode` (`Direct3d9.cpp:1679-1718`) fails windowed
   mode when desktop width OR height merely EQUALS the requested size per axis (line 1690) or
   desktop is smaller (1700); on failure the engine silently creates a FULLSCREEN-EXCLUSIVE device
   (`Direct3d9.cpp:1241-1244`). The equality check is gated on `!ms_borderlessWindow` → mitigate by
   writing `borderlessWindow=true` in the same cfg (supported key, `ConfigClientGraphics.cpp:99`;
   AdjustWindowRect is a no-op on WS_POPUP so client==requested exactly). (Fable; Opus found the
   >-desktop half.)
5. **1.951 projection ratio is NOT an anomaly.** Provider derives vfov linearly in angle space:
   `verticalFieldOfView = hfov * viewportHeight/viewportWidth` (`Camera.cpp:121`), proj terms are
   cotangents of the half-angles → proj[1][1]/proj[0][0] ≠ viewport aspect BY DESIGN; ~1.95 is
   exactly what a correct 1600×900 render produces. Never derive aspect from proj terms; consume
   the raw matrix (already the case, `imgui_impl.cpp:1191-1208`). (All four confirm.)
6. **Stale DX11 comments in-tree.** `PanelGame.cs:248-256` + `imgui_impl.cpp:668-671` say "DX11
   embed" but the live client is rasterMajor=5/D3D9 (same-day handoff, live-verified). Design A is
   raster-agnostic; the 1:1-present analysis is D3D9-specific → log which `gl%02d_r.dll` actually
   loaded as part of the runtime assert. (Opus, Fable.)

## The hardened design (implementation spec)

**Launcher (`Launcher/main.cpp`):**
- Detect the advertised target before appending anything: check the mapped PE (already mapped at
  `main.cpp:218-222`) for the `GetEngineHookPoints` export. SWGEmu must NEVER receive the @ref
  (it shares the config-parsing lineage; appending would change SWGEmu behavior).
- Merge-aware append: if the pass-through args already contain a standalone ` -- `, append
  ` @utinni_embed.cfg` at the END of the post-string (later values win, `ConfigFile.cpp:797`);
  else append ` -- @utinni_embed.cfg`. Preserve the existing leading-space/no-argv[0] cmdline
  structure (`main.cpp:393-404`) — the CRT parse survives it only in that exact shape.
- Never hand-compose richer post-strings: a token without `=` triggers a negative-length memcpy in
  release (`ConfigFile.cpp:211-216`); a bare `--` inside the post-string FATALs the parser.

**Managed startup reorder (`UtinniCoreDotNet/main.cs` + FormMain):**
- Hoist FormMain construction before the ready signal: construct → force
  `WindowState = Maximized` → `Show()` (layout is synchronous after Show returns; use
  `FormMain.Shown`/`PerformLayout` if plugin-panel SuspendLayout is outstanding) → measure the
  INNER `PanelGame.ClientSize` (the value `RepositionSwgWindow` uses — NOT FormMain.ClientSize;
  the plugin dock changes it) → validate → write cfg → `SignalLauncherReady()` →
  `Application.Run(existingForm)`.
- Validation gates before writing: nonzero, ≥ ~1024×768 floor (engine UI minimum), strictly
  < desktop in BOTH axes. On any failure: write nothing / delete the file — a missing @file warns
  and degrades gracefully to today's 1600×900 stretch (`ConfigFile.cpp:345-351`), never a crash.
- Atomic write: temp file + File.Replace/MoveFileEx into the CLIENT dir (derive from
  `Process.MainModule.FileName`); delete on write failure (stale-file guard).
- Contents: `[ClientGraphics] screenWidth=<w> screenHeight=<h> borderlessWindow=true`.
- Timing is airtight by ordering: the game main thread is spin-parked at the patched entry and has
  executed ZERO own instructions until the launcher restores OEP after the ready event
  (`main.cpp:337-362`) — no read-before-write window exists once write precedes signal.

**Native assert — fail loud, fail closed (`directx9.cpp` + `imgui_impl.cpp`):**
- Extend the existing first-Present one-shot probe (`directx9.cpp:285-332`): compare
  `pp.BackBufferWidth/Height` vs `GetClientRect(getSwgHwnd())` vs the published embed size, and
  verify `ms_windowed` stayed true (fullscreen fallback must be detected, not eyeballed). Mismatch
  >1px → `log::critical`.
- Cheap per-frame gizmo gate in newFrame (it already computes rtw/clientW): RT != client →
  hard-disable the gizmo. Converts any future silent mis-calibration into "gizmo refuses + loud
  log" (no-half-working standard).

**ImGui overlay:**
- KEEP the RT-space mouse/DisplaySize overrides untouched (panel vote 3–1: Codex/Opus/Fable keep,
  Cursor remove). Decisive argument (Fable): the block is NOT advertised-gated today
  (`imgui_impl.cpp:654-702` runs on both clients) — removal would require new gating, i.e. a
  SWGEmu-path diff, for zero benefit; as identity no-ops they self-heal 1-2px rounding and handle
  `ToggleFullWindowGame` runtime panel resizes gracefully.
- STRIP only the bounded `gizmo-diag` logging block (`imgui_impl.cpp:668-698`).
- No gizmo-side math change: raw matrices into ImGuizmo + `SetRect(0,0,RT)` is correct once
  RT == window == mouse space.

**Provider ask (non-blocking insurance):**
- Request a `camera::getViewport` row: full-RT is correct today (Camera ctor defaults viewport to
  full RT, `Camera.cpp:51-54`; the only game-side `setViewport` sub-rect caller is the unused Qt
  GameWidget), but Utinni's existing `Camera::getViewport` uses SWGEmu-only hardcoded RVAs
  (`camera.cpp:48-102`) — unusable on the advertised client. The row converts the last assumption
  into a runtime-checked fact (`viewport == (0,0,RT)` else disable gizmo). Do NOT block on it.

## Known landmines recorded

- **Dormant device-Reset path:** `hkEndScene`'s editor-mode branch calls `swg::graphics::resize`
  on window-size change (`graphics.cpp:712-732`); `graphics::resize` IS bound on the advertised
  client (`endpoints_bindings.cpp:652`) and the provider's resize does a REAL device Reset
  (`Graphics.cpp:602-608` → `Direct3d9.cpp:2010-2026`). Dormant only because `Client::hwnd` is
  never set (`client.cpp:51`, setter has no callers). Never resurrect `setHwnd` on the advertised
  path without revisiting this.
- **Mid-session `WM_DISPLAYCHANGE`** re-runs checkDisplayMode at present and can drop to fullscreen
  (`Direct3d9.cpp:2491-2495`) — another reason for the per-frame gizmo gate.
- **DPI:** definitively a non-issue for distortion. SwgClient_r is system-DPI-aware
  (`EnableDpiAwareness` in SwgClient.vcxproj); the in-process WinForms host inherits it → panel
  ClientSize px == physical px == correct backbuffer size. Mixed-DPI multi-monitor stretches
  uniformly (sharpness, not calibration). Utinni itself never touches DPI awareness.
- **PRODUCTION VRAM clamp** (`Direct3d9.cpp:1163-1177`) rewrites resolution only on <32/64 MB VRAM
  or PRODUCTION builds — covered by the first-Present assert either way.
- **Post-launch resize stays out of scope** — but since maximize is now forced at startup,
  un-maximizing after device creation re-introduces stretch; the overrides + gizmo gate handle it
  gracefully (view distorts, gizmo refuses; no crash). Full dynamic resize = future RNDR-04.

## Wave plan (suggested)

1. **Wave A1 (launcher):** advertised-detect + merge-aware `@utinni_embed.cfg` append.
2. **Wave A2 (managed):** startup reorder + measure + validated atomic cfg write + signal move.
3. **Wave A3 (native):** first-Present assert + per-frame gizmo gate + strip gizmo-diag.
4. **Live smoke:** advertised launch → verify log `bb==client==embed`, undistorted view, gizmo drag
   on every axis anywhere; SWGEmu regression smoke (byte-identical native paths; launcher appends
   nothing for SWGEmu).
5. Provider request doc: `camera::getViewport` row (fold into the existing wanted-rows list).
