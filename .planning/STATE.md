---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: completed
stopped_at: Phase 2 context gathered
last_updated: "2026-05-20T15:00:00.000Z"
last_activity: 2026-05-20 -- Issue #9 RESOLVED; SWG logs into Tatooine end-to-end under Utinni injection
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

7. **NEW: Lok scene-load second-cycle access violation at `0x00b3f620`** — Lok scene loads cleanly the first time (`hkSetScene: ENTRY (scene=234F5EC0)` → `setupScene returned` → `setSceneCallbacks complete; EXIT`, all in `~3s`). Then ~3 seconds later a second scene-load cycle starts (`loadNewScene=true` again, `Game::cleanupScene` fires, `hkSetScene` fires with `scene=00000000` — SWG's internal teardown call). SWG access-violates at `0x00b3f620` while touching `cargo_freighter_l0.msh`. Crash dump: `D:\SWGEmu-Client\SWGEmu\logs\SWGEmu.exe-stage.119798-20260520022742.{txt,mdmp}`. **Open questions:** what triggers the second cycle (editor UI action? a `setSceneCallback` that flips `loadNewScene` back to true? SWG-internal logic?); is `0x00b3f620` a fault address or EIP (SWG dump format is ambiguous). **Next steps:** (a) VS attach + breakpoints on `swg::game::setupScene` and `Game::cleanupScene`, trigger Lok load, observe what calls them the second time; (b) audit the TJT plugin's `setSceneCallback` (the one logged "firing 1 setSceneCallbacks") for any side effect that retriggers load.

8. **NEW: Naboo scene-load: SWG memory pool exhausts at ~300 MB** — `terrain/naboo.trn` exhausts SWG's 750 MB internal allocator at ~300 MB used (`BytesAllocated: 196M → 219M → 258M → 288M → 300M`), fataling with `b0780503: failed allocation attempt for 38048 (38017 actual)` while loading `shared_frn_all_bed_sm_s1.iff` (furniture template). Crash dump: `D:\SWGEmu-Client\SWGEmu\logs\SWGEmu.exe-stage.119798-20260520021056.{txt,mdmp}`. Standalone SWG loads Naboo fine — Utinni-side mappings (CLR + WinForms + plugins) appear to be consuming address space that SWG's allocator wants. **Next steps:** (a) `dumpbin /headers SWGEmu.exe | findstr large` — check `/LARGEADDRESSAWARE` flag (32-bit non-LAA caps at 2 GB total user vaddr); (b) if not set, patch PE characteristics to add `IMAGE_FILE_LARGE_ADDRESS_AWARE` (one-bit PE header change); (c) failing that, RVA-patch SWG's hardcoded memory cap or audit Utinni allocations for trimming.

9. **~~Cursor doesn't display + special keys (delete/tab/return) don't work in game~~ RESOLVED 2026-05-20 (commits `f5fa073..2f02fad`)** — Two-phase fix. **Diagnosis (Phase A, commit `f5fa073`):** added entry/exit logs to `Client::suspendInput/resumeInput`, `DirectInput::suspend/resume`, and `Client::setHwnd`. Live capture revealed every PanelGame focus-loss + mouse-leave fired `Client::SuspendInput` twice (back-to-back), which called `DirectInput::suspend()` and `SetFocus(nullptr)` — unacquiring DirectInput the instant the user clicked into SWG's window. Special keys (Tab/Del/Return) dispatched via DirectInput were dead; regular chars survived via `WM_CHAR` through the OS message pump. **Fix (Phase B, commit `2f02fad`):** stripped the old-model editor meddling: (a) removed PanelGame's MouseEnter/MouseLeave/MouseMove/GotFocus/LostFocus handlers — they assumed SWG rendered inside PanelGame, no longer valid in the SWG-owns-its-own-window model; (b) removed `Client.SetHwnd(Handle)` from `PanelGame_Layout` — was shadowing SWG's real top-level HWND with PanelGame's handle; (c) dropped editor-mode cursor side-effects from `hkSetupInstall` (HCURSOR write + `useHardwareCursor(false)`) — these forced software-cursor mode that only renders inside SWG's framebuffer, which the new model doesn't need. **Verification 2026-05-20:** login → mouse/keyboard both work → character select → mouse click logged into Tatooine. End-to-end pipeline functional. Phase A diag logs kept in source — nearly silent during normal play, will flag any future regression. **Knock-on cleanup deferred:** `game.cpp` `hkMainLoop` and `graphics.cpp` `hkEndScene` editor-mode resize branches both gate on `GetWindowRect(Client::getHwnd(), &rect)`; with `getHwnd()` now returning null they auto-skip, but the dead branches should be cleaned up when Issue #10 (window reparenting) decides the correct behavior.

10. **NEW: SWG window not yet reparented into editor's PanelGame** — SWG creates its own top-level window (correct, per the HWND-override removal from earlier today). Plan was to `SetParent` + `WS_CHILD` style change to embed it in the editor's PanelGame, mentioned as "reparent-after-creation model" in `hkSetupStartInstall`'s log line. Managed-side hook on `Client::setHwnd` (already exists in `client.cpp`) is the natural place — when fired, the editor's PanelGame should grab the HWND and reparent. **Defer until #7-#9 stabilize** — wrong moment to add complexity to the window pipeline. Cosmetic / integration-quality issue, not a functional blocker.

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
