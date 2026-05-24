# 06-01 Tier-4 Verification — Imgui Overlay Demo Screen Over Live SWG

**Plan:** 06-01 (Overlay-debug investigation)
**Phase:** 06 (Cleanups, dep bumps, open questions, Tier-4 doc, 1.0-rc.1 cut)
**Verification date:** 2026-05-23
**Maintainer signature:** Verified by Kenneth Long (kenny.alan.long@gmail.com) 2026-05-23

## Machine Identifier

- **OS:** `Microsoft Windows [Version 10.0.26200.8457]` (Windows 11 Home 10.0.26200)
- **Workstation:** kenne's local dev workstation (D:/Code/Utinni)
- **Toolchain:** Visual Studio 2026 (Dev18) Community at `D:\Program Files\Microsoft Visual Studio\18\Community`; VS 2022 Build Tools 14.29.30133 used for native compile under v142 PlatformToolset.
- **GPU vendor:** TBD by maintainer (log line not captured into this artifact; if needed, grep `utinni.log` from the verification session for adapter info — Phase 6's preservation-audit pass may surface a canonical capture line).

## Tier-4 Exercise (One-Paragraph Description)

The maintainer rebuilt `UtinniCore.dll` Release x86 with `g_showDemoWindowProbe = true`, launched `Launcher.exe` against a live SWGEmu client, logged in to a character, and entered Tatooine. The imgui Demo window appeared end-to-end over the live SWG render target at the post-Phase-B-bis owned-popup Z-order (atop FormMain, atop SWG's client framebuffer). The maintainer then exercised each of the seven widget categories defined by D-11's exit criterion: menus (menu bar cascading, items dispatching), sliders (`SliderFloat` / `SliderInt` drag + keyboard edit), buttons (basic + `SmallButton` + `ArrowButton` dispatch), tabs (`TabBar` switching), plots (`PlotLines` + `PlotHistogram` animated waveforms), popups (modal `OpenPopup` blocking + dismissal), and drag-and-drop (source-to-target with payload preview and drop callback firing). All seven categories behaved as in a standalone imgui demo application. The session ended with a clean shutdown via `/quit` → `hkCleanupScene → hkSetScene(null) → cleanUpSceneCallbacks complete; EXIT`, producing no SWGEmu-stage `.txt`/`.mdmp` dumps and no fatal codes. Both Task 1 one-shot diag tripwires fired exactly once each (`imgui_impl::setup complete, isSetup=true` followed within the same second by `imgui_impl::render entered isSetup branch`), confirming the setup → render gate chain is alive in production injection.

## Tier-4 Sign-Off

**Status: PASSED.**

D-11's exit criterion is satisfied. The imgui in-game overlay does display in Utinni-injected sessions; the stale "never displayed" belief is superseded by Phase 02.1 commit `2c57d38` (d3d9 dummy-device approach) + Phase B/B-bis owned-popup window-ownership work + Phase H chat-context fixes. No additional code-level fix is required for the overlay itself. The Task 1 diag tripwires remain in source as latent regression detectors at `debug` level.

## Cross-Links

- Full investigation log + d3d9 pattern-scan disposition + live-SWG observation transcript + future design notes: [`06-01-DEMO-PROBE-NOTES.md`](./06-01-DEMO-PROBE-NOTES.md).
- Tier-4 procedure row: `.planning/codebase/TESTING.md` § "Tier 4 — Manual Residual Enumeration" row #1 ("Imgui overlay Demo screen over live SWG").
- Plan: [`06-01-PLAN.md`](./06-01-PLAN.md).
- Decision context: `06-CONTEXT.md` D-11 (06-01 investigation + ShowDemoWindow exit criterion) and D-06 (imgui docking-branch switch gated on 06-01 success — now unblocked).

## Post-Merge Validation Owed by Orchestrator

CI green confirmation on master is **deferred to the parent orchestrator** after the worktree branch (`worktree-agent-a33a47192c0c0ea9e`) merges back. The executor running inside the worktree cannot meaningfully run `gh run list --branch master --limit 1 --json conclusion -q '.[0].conclusion'` against master because the relevant commit only lands on master post-merge. The orchestrator should verify CI returns `"success"` on the merge commit before marking 06-01 done in `STATE.md`.
