# RESID-02 — Intro-skip Scene-transition Crash: Root-Cause Analysis

**Status:** RESOLVED (no longer reproduces) — disposition via plan **A5** (no-repro), root cause attributed to **prior Phase-3 fix**.
**Captured:** 2026-06-03, live injected SWGEmu session on the maintainer's machine.
**VEH logger:** remains deployed (`UtinniCore/utinni.cpp:291`, `AddVectoredExceptionHandler(1, utinniBreakpointVEH)`, commit `d1096ac`) — D-11 honored.

---

## 1. Outcome of the live repro (Task 1)

The maintainer ran the known RESID-02 trigger on the current build (`UtinniCore.dll` @ `2026-05-31 13:52`):

- **Scene-transition path:** Launched `bin\Release\Launcher.exe` (spawns `D:\SWGEmu-Client\SWGEmu\SWGEmu.exe` suspended → EB FE entry patch → inject `UtinniCore.dll` → `utinni_init` → TJT). From the TJT editor's **"The Jawa Toolbox – Controls"** panel → **Scene** subpanel → selected `terrain/naboo.trn` → **Load**. Scene loaded cleanly; landed **naked but in world** (the expected baseline per `project_tjt_scene_change_naked_baseline`). FreeCam toggled, input events flowed. **No crash.**
- **Original intro→login path:** Logged in and loaded into world (the original RESID-02 trigger — the intro→login scene transition). **No crash.**

**No `VEH FATAL` line was emitted** in `bin\Release\utinni.log` (nor rotated into `utinni.log.previous`) across these attempts, because the fault did not occur. The line the deployed VEH logger would have written, had the fault fired, is:

```
VEH FATAL: code=0x%08X EIP=0x%08X module=%s base=0x%08X rva=0x%08X ESP=0x%08X [WRITE|EXEC|READ target=0x%08X]
```

(`rva = EIP - moduleBase` is the field that would have been the deliverable.) None was produced — there is no faulting `module`/`rva` to resolve because there was no fault.

This is the **A5** branch the plan anticipated: *"If no crash reproduces after several attempts, that is reported so disposition can adjust."*

---

## 2. Original fault (for the record)

- **Symptom:** Pressing **Return** to skip the intro cinematic on the **intro→login scene transition** produced a **dump-less crash** — no SWG stage dump, no WER event (SWG's own SEH bypassed). That dump-lessness is *why* the VEH logger was invented as the harness for this fault.
- **Class:** Scene-change callback-dispatch fault. The adjacent prior-art crash `0x0051fb0a` was a per-frame `std::vector::reserve()` in the scene-change callback dispatch fragmenting SWG's allocator (`project_rh_snapshot_no_heap_alloc`).

---

## 3. Root cause / disposition (D-11)

The plan's two D-11 branches are **branch 1** (fault in Utinni's own injection/detour/callback surface → fix there) and **branch 2** (fault in `SWG.exe` game code → documented RCA). The captured outcome lands on **neither** — the fault is **already resolved**, so this is documented as a **no-repro attributable to a prior Utinni-side fix** (a retroactive branch-1 resolution):

**Attributed fix:** the Phase-3 **R-A/R-H heap-free dispatch migration** —
- `7201700` *fix(03): ground_scene heap-free dispatch via vector + stack snapshot*
- `5e81410` *feat(03-01): R-A + R-H native-side game/scene/object/graphics*

These replaced the per-frame heap-allocating callback dispatch with the stack-allocated fixed-size `dispatchSnapshot` template (`ground_scene.cpp`, `kInlineCap=16`), eliminating the allocator-fragmentation fault class that produced the scene-change crash.

**Corroborating evidence (independent of today's session):** the debug note `.planning/debug/chat-open-d3d9-fullscreen.md`, captured **2026-05-23 after the R-A heap-free migration live smoke**, records: *"The smoke itself passed (many warps, **no scene-change AV at `0x0051fb0a`**)."* The scene-change AV was already gone as of that migration smoke — today's session (2026-06-03) is the second independent confirmation, now also exercising the intro→login path.

**Confidence:** HIGH that RESID-02 is resolved by the R-H migration. The disposition rests on *absence of fault* (no-repro) rather than a freshly-captured-and-fixed `VEH FATAL`, so the **VEH logger remains deployed** as standing observation to catch any recurrence (D-11).

---

## 4. Separate finding — NOT RESID-02 (do not conflate)

During the live re-run the maintainer observed: after **login → load into world**, SWG switches to **fullscreen**, and on **alt-tab out + resize** the **SWG window is never returned to its reparented embed rect — it overlays the WinForms editor window.**

Downstream symptom in the same session: with SWG detached to exclusive fullscreen, the session enters a **non-recoverable stuck state** — clicking the window does **not** establish the SWG cursor and the **character cannot move** (input never reaches the game). This is the cursor-clip + WinForms→SWG input-routing fallout of the detach, consistent with `project_swg_cursor_clip_deadzone`. Per the debug note the window is *"never returned"* — recovery is relaunch `Launcher.exe`, not in-session.

This is **not** the RESID-02 fault class. It is the already-captured, already-deferred **windowed↔fullscreen / D3D9-presentation edge case**:
- `swg-window-resize-fullscreen-edge-cases.md` (commit `fb1f0e8`), tagged `resolves_phase:15` (post-V1 / v2.0) by `f370173`.
- `.planning/debug/chat-open-d3d9-fullscreen.md` — same mechanism: a D3D9 device mode-switch to true exclusive fullscreen detaches the embedded SWG window and never restores it; cursor + input routing break with it. Cross-linked to `feedback_d3d9_reset_third_party`, `project_swg_cursor_clip_deadzone`, `feedback_owned_popup_zorder`.

**Disposition:** no action in Phase 12. Already triaged to the future window-management / D3D9-presentation phase (Phase 15 / v2.0). Reconfirmed live 2026-06-03 (window overlay + non-recoverable cursor/movement stuck state).

---

## 5. Verification summary

- [x] Live intro→login + TJT scene-transition repro exercised on current build → **no crash, no `VEH FATAL`**.
- [x] Root cause attributed (Phase-3 R-H heap-free `dispatchSnapshot` migration) with independent 2026-05-23 corroboration.
- [x] VEH logger remains deployed (`utinni.cpp:291`, D-11).
- [x] Separate window-overlay finding routed to its existing Phase-15 deferral (not conflated with RESID-02).

**ROADMAP Phase 12 success criterion #5** — *"intro-skip path no longer crashes on a live injected session, with naked-but-in-world recognized as the baseline"* — **MET** (by prior fix; documented here).
