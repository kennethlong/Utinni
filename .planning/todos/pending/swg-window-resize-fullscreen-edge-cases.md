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
