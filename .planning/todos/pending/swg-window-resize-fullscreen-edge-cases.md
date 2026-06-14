---
resolves_phase: 18
title: SWG window resize / windowed↔fullscreen edge cases
area: d3d9-presentation
---

# SWG window resize / windowed↔fullscreen edge cases

**Status:** OPEN — captured for a future phase (window-management / D3D9 presentation pass)
**Opened:** 2026-06-01 (post-V1; maintainer observation during the V1 OT smoke session)
**Owner:** maintainer (needs an injected live SWG session to enumerate + repro)
**Severity:** non-blocking for V1; quality/robustness of the embedded-window experience

## Symptom (reported)

When the embedded SWG window changes presentation mode — **windowed → fullscreen → back** (and
related transitions) — there are several issues. The exact per-transition symptoms still need to be
enumerated against a live session; this todo is the bucket to capture them all before scoping a fix.

## Edge-case matrix — to fill in during the next-phase investigation

For each transition, record: does the embed survive? does SWG render at the right size? does the
mouse map correctly (RT-space)? is the cursor clip correct? does it recover on the reverse transition?

- [ ] windowed → fullscreen
- [ ] fullscreen → windowed
- [ ] windowed → maximize → restore
- [ ] windowed → minimize → restore
- [ ] free resize-drag of the editor / PanelGame (continuous)
- [ ] multi-cycle (windowed → fullscreen → windowed → fullscreen …) — look for cumulative drift / leaks
- [ ] alt-tab away/back in each mode
- [ ] monitor / DPI change while embedded (if reproducible)

## Observed live (2026-06-03 — maintainer, injected SWGEmu session)

Surfaced while exercising the RESID-02 intro-skip repro (which did **not** crash — see
`.planning/phases/12-revive-feasibility-spike-hard-gate-intro-skip-crash/12-RESID-02-RCA.md`).
Concrete per-transition results to seed the matrix:

- **NEW trigger data point:** the switch to fullscreen fired on the **login → load-into-world** path,
  not only the chat-open Enter path (#1 below). Widens the prime-suspect trigger surface — the
  exclusive-fullscreen mode change is reachable from normal login, not just `enableTextInput`.
- **windowed → fullscreen (on login):** embed detaches; **SWG window overlays the WinForms editor
  window** (z-order/parent coupling). ✗
- **alt-tab away/back + resize:** SWG window is **never returned** to its embed rect. ✗ (matches #1)
- **cursor / input:** clicking the window does **not** establish the SWG cursor; **character cannot
  move** — input never routes to the game. Non-recoverable in-session (relaunch required). ✗
  (cursor-clip + input-routing fallout, #2)
- **minimize → restore:** minimizing takes **both** windows down together (they disappear as a unit) —
  confirms the SWG child and WinForms parent are z-order/parent-coupled. ✗ (fills the
  `windowed → minimize → restore` row)
- **Recovery workaround (maintainer):** reduce SWG's resolution from fullscreen down to the default —
  the resulting mode change drops SWG back out of exclusive fullscreen and re-establishes the
  windowed/embed path. Useful interim recovery; also a strong hint the fix is to **intercept/suppress
  the exclusive-fullscreen mode switch** (matches proposed scope #3 → keep it windowed-embedded).
- **Maintainer triage (2026-06-03):** low priority for now, likely not a hard find.

## Observed live (2026-06-13 — maintainer, injected SWGEmu session; Phase 15 C3 re-smoke)

- **windowed → fullscreen:** ✅ **embed SURVIVES** after the 15-13 watchdog fix (no detach, no
  overlay, focus/input recover). The previously-BLOCKING C3 gap is closed.
- **NEW bug — mouse position/click mapping is wrong when BOTH the app and the editor are fullscreen.**
  The OS cursor and the effective click point are scaled/offset incorrectly: to hit the in-game
  **Quest** button the maintainer had to aim **slightly above** the button and **~50 px to the left**
  of it. So the RT-space mouse mapping that holds for windowed/maximized does **not** hold for the
  fullscreen-embed condition — a constant-ish offset + scale error.
  - **Scope:** out of Phase 15 scope; captured here for the window-management / D3D9 presentation pass.
  - **Same family as:** the RT-space mouse-mapping note (`feedback_imgui_embedded_d3d9_rt_space`, #4
    below) and the cursor-clip dead-zone (`project_swg_cursor_clip_deadzone`, #2 below) — the mapping is
    computed against the backbuffer/render-target rect, which the window-level fullscreen restyle
    changes (different client-rect ↔ backbuffer stretch ratio + origin), so the windowed-derived
    scale/offset is stale.
  - **Repro:** put both the editor and SWG in fullscreen, try to click a known UI element (e.g. the
    Quest button); the hot-spot is up-and-left of the visible control.
  - **Fix direction (to investigate):** recompute the RT-space mouse scale + origin from the *current*
    fullscreen client-rect ↔ backbuffer mapping on the window-level fullscreen transition (the same
    transition the 15-13 watchdog already detects), instead of reusing the windowed mapping.

## Known-adjacent issues (link, don't re-investigate from scratch)

This is the same family as several already-recorded items — start here:

1. **D3D9 exclusive-fullscreen switch detaches the embed** — `.planning/debug/chat-open-d3d9-fullscreen.md`
   (UNRESOLVED, 2026-05-22). A fullscreen switch (originally observed via the Phase H chat-open path)
   flips SWG to true D3D9 exclusive fullscreen, detaches it from FormMain/PanelGame, and never
   reattaches. The windowed→fullscreen→back cluster is very likely the same mechanism. **Prime suspect
   to confirm first.**
2. **Cursor-clip right-edge dead zone when stretched/maximized** — auto-memory
   `project_swg_cursor_clip_deadzone` (deferred 06-06 → Wave-1). SWG `ClipCursor`s the OS cursor to its
   backbuffer rect; when the panel is stretched wider than the backbuffer, the rightmost ~175px is
   cursor-dead. Resize changes the stretch ratio, so this interacts directly with the resize cases.
3. **No `Reset` on the third-party device** — auto-memory `feedback_d3d9_reset_third_party`. Cannot
   `IDirect3DDevice9::Reset` SWG's device (owns untracked default-pool resources → `D3DERR_INVALIDCALL`
   → DEVICELOST → crash). Windowed `Present` self-stretches backbuffer↔window, which is why windowed
   resize mostly works — but a **real fullscreen mode change** is the hard case the no-Reset constraint
   makes thorny.
4. **RT-space mouse mapping for the embedded window** — auto-memory `feedback_imgui_embedded_d3d9_rt_space`.
   The overlay runs in render-target space (DisplaySize + mouse both scaled) because the reparented
   window is stretched onto an un-Reset backbuffer. Any resize that changes the stretch must keep this
   mapping correct.
5. **PanelGame owned-popup reparenting + reposition** — STATE "Issue #10 Phase B/B-bis" (`2ce028c`,
   Phase B-bis). The reposition triggers on `Resize` + `OwnerForm.LocationChanged`; the fullscreen path
   may bypass or fight these.

## Next-phase scope (proposed, not committed)

1. Reproduce + fill the edge-case matrix against a live session (record per-transition symptom).
2. Confirm whether the cluster is all one root cause (the exclusive-fullscreen switch in #1) or several.
3. Decide the policy: either **intercept/suppress** SWG's mode-change so it stays windowed-embedded
   (consistent with the no-Reset + RT-space model), or support a deliberate detached-fullscreen mode
   with clean re-attach. Likely the former, matching the Phase B owned-popup model.
4. Fold the resolved `chat-open-d3d9-fullscreen.md` debug session into the fix.

Cross-refs: `project_swg_cursor_clip_deadzone`, `feedback_d3d9_reset_third_party`,
`feedback_imgui_embedded_d3d9_rt_space`, `feedback_owned_popup_zorder`,
`.planning/debug/chat-open-d3d9-fullscreen.md`.
