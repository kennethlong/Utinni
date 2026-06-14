---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 19
subsystem: ui
tags: [winforms, the-jawa-toolbox, particle-editor, candor, tooltip]

requires:
  - phase: 15-16
    provides: PreviewNoHookTooltip / PreviewUnavailableTooltip LOCKED copy + Game.IsRunning state selection
provides:
  - Particle editor Preview affordance whose honest no-hook/no-client candor is reachable by hover AND click (B6 closed)
affects: [15-21]

tech-stack:
  added: []
  patterns:
    - "Reachable-candor: never hide a degraded reason behind a disabled control (WinForms renders no tooltip over disabled); keep the control enabled and branch on actual reachability inside the click handler"

key-files:
  created: []
  modified:
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormParticleEditor.cs"

key-decisions:
  - "Took the PRIMARY approach (enabled button + click candor), not the Panel-wrapper alternative — no Designer change needed"
  - "Kept the false-branch retrigger guard reading the same clientUp && IsRetriggerHookReachable() predicate so no live retrigger fires without a real hook"

patterns-established:
  - "Disabled-control tooltips are unreachable in WinForms: surface degraded candor via an enabled control on click + tooltip, gated by a reachability predicate in the handler"

requirements-completed: [PROD-W2-PRT]

duration: 9min
completed: 2026-06-13
---

# Phase 15 Plan 19: Particle Preview Candor Reachability (B6) Summary

**Made the Particle editor's honest no-hot-retrigger-hook candor reachable by keeping `btnPreview` enabled and surfacing the LOCKED degraded copy on click (plus the now-renderable hover tooltip), instead of swallowing it behind a disabled control.**

## Performance

- **Duration:** ~9 min
- **Started:** 2026-06-13
- **Completed:** 2026-06-13
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Closed 15-SMOKE Checklist B6 (live-confirmed defect: "B6 still no tooltip, tooltips might not display on disabled fields").
- `btnPreview.Enabled = hasDoc` in `RefreshButtonsState` — no longer gated on `clientUp`/`hookReachable`, so WinForms renders its tooltip on hover.
- `OnPreviewClicked` now branches FIRST on `clientUp && IsRetriggerHookReachable()`: false → surface the LOCKED degraded candor (`PreviewNoHookTooltip` when the client is up but no hook; `PreviewUnavailableTooltip` when no client) via `lblStatus` with `Colors.FontDisabled()` dimmed/informational styling, performing NO retrigger; true → the unchanged real hot-retrigger path (15-08 seam).
- LOCKED copy preserved verbatim; no-client stays distinct from no-hook; no over-promise of a live hook (T-15-19-01 mitigated).

## Task Commits

1. **Task 1: Make the Particle Preview no-hook/no-client candor reachable (B6)** — `446ea8e` (fix), in `kennethlong/UtinniPlugins` (pushed to master)

**Plan metadata:** committed separately in the `Utinni` repo (docs).

## Files Created/Modified
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormParticleEditor.cs` — Preview affordance candor reachability (RefreshButtonsState enable + OnPreviewClicked branch).

## Decisions Made
- **PRIMARY over Panel-wrapper.** The primary approach was clean and required no Designer change, so the Panel-wrapper alternative was not used. The button now reads as enabled, but clicking it in the degraded state yields the honest reason rather than a no-op — preferable to a dead-looking disabled control.
- Reused the existing `IsRetriggerHookReachable()` seam in the click handler's guard so the live retrigger path is reached only when a real hook lands (15-08+); the inline `clientUp && IsRetriggerHookReachable()` check mirrors `PreviewAvailable()` without an extra `effect != null` round-trip already handled at the top of the handler.

## Deviations from Plan
None - plan executed exactly as written (PRIMARY approach, no Designer change).

## Issues Encountered
- The git-bash Bash tool mangled the MSBuild `/p:` switches (stripped leading slashes) and nested-`cmd /c` quoting dropped into an interactive shell. Resolved by writing the exact plan command to a temp `.bat` and invoking it via `powershell -NoProfile -Command "& '...bat'"`. Build result was unaffected: **MSBuild exit 0**, both `TheJawaToolbox.dll` and `TheJawaToolboxDotNet.dll` emitted to `bin/Release/Plugins/TheJawaToolbox/`. Temp file removed.

## Verification
- `TheJawaToolbox.sln` Release|x86 — **MSBuild exit 0**.
- Read-confirm: `btnPreview.Enabled = hasDoc` (L1053); `OnPreviewClicked` false-branch surfaces `PreviewNoHookTooltip`/`PreviewUnavailableTooltip` (L743) with no retrigger; tooltip on enabled button (L1060).
- LOCKED copy verbatim: `PreviewUnavailableTooltip = "No live client — start SWG to preview in-scene."` (L81) and `PreviewNoHookTooltip = "Live preview isn't wired this build — edits show on the next scene change or relog."` (L85-86) unchanged. no-client distinct from no-hook.
- Scope honored: only the Preview-candor methods touched; `OnOpenClicked` (reserved for 15-20) untouched; `FormParticleEditor.Designer.cs` not modified; `GetSubPanels()` not widened.
- `Generated/UtinniCore.cs` (Utinni repo, not UtinniPlugins) not touched — `git status` on UtinniPlugins showed only `FormParticleEditor.cs`.
- LIVE B6 re-verify is gated to 15-21 (not attempted here).

## Self-Check: PASSED
- File exists: `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormParticleEditor.cs` — FOUND.
- Commit `446ea8e` — FOUND (pushed to UtinniPlugins master).

## Next Phase Readiness
- B6 candor reachable; live hover/click re-verify folded into 15-21.
- 15-20 will edit the same `FormParticleEditor.cs` (`OnOpenClicked` / raw Open… path) — that region was deliberately left untouched here to avoid a merge collision.

---
*Phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals*
*Completed: 2026-06-13*
