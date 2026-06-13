---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 18
type: execute
gap_closure: true
status: complete
disposition: defects — new --gaps round
date: 2026-06-13
---

# 15-18 SUMMARY — Maintainer live-SWG re-smoke (gap-closure round 2 gate)

## Outcome

**Disposition: DEFECTS — see notes. Phase 15 is NOT closed; routes to a new `--gaps` round (15-19+).**

The maintainer (Kenneth Long) completed the still-open Tier-4 live smoke against the 15-17 reassembled +
content-verified `bin/Release/` injection build. The four wave-5 gap-closure goals all **passed live**, two
new minor defects were found, and the RESID-03 live render observation was deferred by maintainer decision.

This pass was **Claude-driven** through the staged `windows-mcp` RESID-04 loop — the editor was
opened/edited/saved by coordinate-click off live screenshots (WinForms UIA labels did not surface).
**bypass-permissions on** prevented the focus-theft that made prior MCP-driven attempts unreliable,
validating the windows-mcp loop for the WinForms editor surface. Only the in-game chat scene-change
remained maintainer-territory.

## Results

| Check | Result |
|-------|--------|
| B4 param-grid rebind on raw-hex edit | ✅ PASS |
| B5 loose-override Save under injection | ✅ PASS (re-confirmed via `ui_auc.stf` save) |
| B6 no-hook preview tooltip | ⛔ DEFECT |
| B7 Explain effect (`decode-iff`) | ✅ PASS |
| B8 boundary footer | ✅ PASS |
| C3 windowed→fullscreen embed re-assert | ✅ PASS (no detach, focus/input recover, no device Reset) |
| D1 `.stf` loose-override Save | ✅ PASS |
| D-render SC3 render-on-reload | ⏸ DEFERRED (Option B) |
| D8 badge candor | ✅ honest as-shipped |

**Core gap-round goals B5 / B7 / C3 / A9 — all PASS live.**

## New defects → gaps round 2 (15-19+)

1. **B6 — no-hook preview tooltip unreachable on a disabled button.** 15-16 set the honest
   `PreviewNoHookTooltip` text but `FormParticleEditor.btnPreview` is disabled this phase, and WinForms
   `ToolTip` does not render over disabled controls. Fix: wrap in a tooltip-bearing `Panel`, OR keep the
   button enabled and surface the message on click, OR owner-draw the tooltip.
2. **D-ii — `.stf` loose-override subpath flatten (Phase-8 Open Q2, concretely reproduced).** An `.stf`
   opened via the raw `Open…` dialog saves the loose override **flat** (`loose\ui_auc.stf`) instead of
   preserving the logical subpath (`loose\string\en\ui_auc.stf`) the client resolves by. The TRE-Browser
   "Open in String-table Editor" hand-off carries the logical path and saves correctly. Fix: derive/preserve
   the logical subpath on raw-dialog open, or steer loose-override save to TRE-Browser when the logical path
   is unknown.

## Deferred residuals (tracked as todos, NOT this round)

- **D-render** — RESID-03 live render-on-reload is gated on the priority-27 `…\loose` searchPath, which is
  disabled in `swgemu_live.cfg` (2026-06-12 phantom-walk mitigation; `ut.ini useSwgOverrideCfg=false`).
  Re-enabling re-introduces the machine-wide retail-data shadow. Logged to
  `phase10-stringtable-sc3-live-reload-residual.md`. The 15-07 classifier proxy + the honest badge stand.
- **Fullscreen mouse-mapping offset** — cursor/click hot-spot is up + ~50px left of the target when both
  app + editor are fullscreen (RT-space mapping stale for window-level fullscreen). Logged to
  `swg-window-resize-fullscreen-edge-cases.md`.
- **Particle codec hard-abort on edited over-count** — `decode-iff` hard-aborts when a count leaf is edited
  above the present sub-form count instead of degrading (D-05 tension; read-tool only, honest error).

## Artifacts updated

- `15-SMOKE.md` — 15-18 re-smoke results table + signed Maintainer Sign-Off (disposition: defects).
- `.planning/todos/pending/swg-window-resize-fullscreen-edge-cases.md` — C3 PASS + fullscreen mouse-mapping.
- `.planning/todos/pending/phase10-stringtable-sc3-live-reload-residual.md` — D-render config blocker + D-ii.

## Next step

Plan + execute gap-closure round 2 (15-19+) for **B6** and **D-ii**, then re-smoke the two fixes. Phase 15
requirements (PROD-W2-WS, PROD-W2-PRT, RESID-03, RESID-04) remain **not Validated** until that round closes.
