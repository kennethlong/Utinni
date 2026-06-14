---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 21
subsystem: release-gate-signoff
tags: [gap-closure, release-gate, injection-build, content-verification, windows-mcp, live-reverify, phase-signoff, wave-9]
requires: ["15-17", "15-19", "15-20"]
provides:
  - "Content-verified round-3 injection build at D:/Code/Utinni/bin/Release/ carrying the 15-19 (B6) + 15-20 (D-ii) fixes"
  - "Live re-verify of B6 (Particle Preview no-hook candor reachable by hover+click) + D-ii (raw-Open .stf loose override lands at loose/string/en/ui_auc.stf) — both PASS, recorded in 15-SMOKE.md"
  - "Signed final Maintainer Sign-Off: approved-with-deferred-residual — Phase 15 CLOSED"
affects:
  - "Phase 15 completion: PROD-W2-PRT + RESID-03 save-tier re-verified Validated; RESID-03 live render-on-reload remains a tracked DEFERRED residual"
tech-stack:
  added: []
  patterns:
    - "Claude-driven live re-verify via windows-mcp coordinate-click off screenshots (UIA labels do not surface on the Utinni host; bypass-permissions on avoids focus theft)"
    - "Defect re-verify by before/after on-disk state diff: capture loose-tree state pre-save (correct subpath ABSENT, flat defect artifact present), drive the live save, confirm the subpath file is CREATED + content-verified (UTF-16LE) while the flat artifact is untouched"
    - "Content-of-fix gate via reflection-only enumeration + byte-string grep of the DEPLOYED PEs (mirrors 15-17)"
key-files:
  created:
    - ".planning/phases/15-wave-2-editors-worldsnapshot-particle-presentation-residuals/15-21-SUMMARY.md"
  modified:
    - ".planning/phases/15-wave-2-editors-worldsnapshot-particle-presentation-residuals/15-SMOKE.md"
  artifacts:
    - "D:/Code/Utinni/bin/Release/ (reassembled, gitignored; both solutions Release|x86 exit 0; content-verified to carry LogicalAssetPath + the 15-19 Preview-candor change)"
    - "D:/SWGEmu-Client/SWGEmu/loose/string/en/ui_auc.stf (live-created during D-ii re-verify — subpath preserved, carries the edited UTF-16LE string)"
decisions:
  - "Task 1 produces gitignored build output (bin/Release/); mirroring the 15-17 discipline there is no source commit for the reassembly — the 15-19/15-20 source fixes (446ea8e, b13c251) are already committed. Only the 15-SMOKE.md re-verify + sign-off annotation is committed (alongside STATE/ROADMAP)."
  - "B6 + D-ii re-verify driven by Claude via windows-mcp (maintainer delegated 'take it' on a live Tatooine session) — viable because both defects are verifiable without game-knowledge/in-game chat; the final Maintainer Sign-Off disposition was still taken from the maintainer (autonomous:false gate respected, not self-signed)."
  - "Final disposition approved-with-deferred-residual: Phase 15 closes with RESID-03 live render-on-reload explicitly carried as a tracked deferred residual (gated on the disabled priority-27 loose searchPath); fullscreen mouse-mapping + particle codec hard-abort remain out-of-phase todos."
metrics:
  duration: ~20 min
  completed: 2026-06-13
---

# Phase 15 Plan 21: Round-3 Reassembly + B6/D-ii Live Re-Verify + Final Maintainer Sign-Off Summary

Reassembled and content-verified the deployable Release|x86 injection build at `D:/Code/Utinni/bin/Release/`
carrying the round-3 fixes (15-19 B6, 15-20 D-ii), re-verified both defects live against the deployed build,
recorded the results in `15-SMOKE.md`, and took the maintainer's signed final disposition closing Phase 15.

## Task 1 — Rebuild + reassemble + content-verify (auto)

- `Utinni.sln` + `TheJawaToolbox.sln` Release|x86 via VS2026 (Dev18) MSBuild — **both exit 0** (D: MSBuild path
  via a temp `.bat` through PowerShell to dodge the git-bash `/p:` mangling trap).
- Automated gate: `UtinniCoreDotNet.Tests` **718/718** (incl. the 12 new `LogicalAssetPath` facts);
  `Utinni.Cli.Tests` **249 passed / 2 fixture-gated skips**; native `UtinniCore.Tests.exe` **84 assertions / 27
  cases** + `[resid04]` **8 assertions / 1 case** — zero regressions; the one known pre-existing D3D9 harness
  failure did not trip.
- `bin/Release/` reassembled with the `netstandard.dll` façade + `utinni-cli.exe` net472 closure intact.
- Content-verified the DEPLOYED PEs: `UtinniCoreDotNet.PathContainment.dll` defines
  `UtinniCoreDotNet.Saving.LogicalAssetPath` (method `TryFromAbsolute`); `TheJawaToolboxDotNet.dll` carries the
  `LogicalAssetPath` typeref (15-20 wiring compiled in) AND the 15-19 LOCKED Preview-candor literals (UTF-16LE)
  + `OnPreviewClicked`/`IsRetriggerHookReachable`/`RefreshButtonsState`.
- `Generated/UtinniCore.cs` reverted; both working trees clean. No commit for the gitignored reassembly (15-17 discipline).

## Task 2 — Live re-verify + final sign-off (blocking human-verify; Claude-driven, maintainer-delegated)

Injected the round-3 `bin/Release/` live (Tatooine / Mos Eisley, SWG running, GOD MODE). Claude drove every
action by coordinate-click off screenshots via `windows-mcp` (bypass-permissions on, no focus theft).

- **B6 — PASS.** Opened `loose\appearance\pt_airport_race_light.prt` in the Particle editor. The
  `Preview in client` button is now **enabled whenever a doc is open** (15-19 `btnPreview.Enabled = hasDoc`).
  **Hover** renders the LOCKED tooltip *"Live preview isn't wired this build — edits show on the next scene
  change or relog."* (unreachable before, on the disabled control). **Click** surfaces the same LOCKED copy in
  the status line (dimmed/informational), with **no hot-retrigger attempted** and no error. The dead,
  explanation-less disabled button is gone.
- **D-ii — PASS.** Opened the **source** `string\en\ui_auc.stf` via the raw `Open…` dialog (244 entries),
  edited `accept_bid` → `"Accept Bid (D-ii test)"`, `Save ▾ → Save as loose override` → status
  *"Saved ui_auc.stf (loose override)"*. Headless on-disk check: the override landed at
  `D:\SWGEmu-Client\SWGEmu\loose\string\en\ui_auc.stf` (**subpath preserved** — created at save time, ABSENT
  pre-save), content-verified to carry the edited string (UTF-16LE). The old flat `loose\ui_auc.stf` defect
  artifact was **untouched** — no re-flatten.

**Final disposition (maintainer):** **approved-with-deferred-residual** — Phase 15 CLOSED. PROD-W2-PRT and
RESID-03 save-tier re-verified Validated. RESID-03 **live render-on-reload remains a tracked DEFERRED residual**
(gated on the disabled priority-27 loose searchPath; re-enabling re-introduces the phantom-walk shadow).
Out-of-phase deferred todos: fullscreen mouse-mapping offset, particle codec hard-abort-on-edited-count.

## Verification

- Both solutions Release|x86 MSBuild exit 0; suites green (no regression).
- bin/Release content-verified to carry `LogicalAssetPath` + the 15-19 Preview-candor change.
- LIVE: B6 candor reachable (hover + click); D-ii loose override at `loose\string\en\ui_auc.stf` (content-verified).
- `15-SMOKE.md` B6 + D-ii round-3 rows recorded; Maintainer Sign-Off signed **approved-with-deferred-residual**.
