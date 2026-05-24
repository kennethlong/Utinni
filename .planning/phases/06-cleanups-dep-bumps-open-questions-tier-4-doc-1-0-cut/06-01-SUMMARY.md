---
phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut
plan: 01
subsystem: ui
tags: [imgui, d3d9, overlay, tier-4, manual-uat, swg-injection, demo-window]

requires:
  - phase: 02.1-critical-bug-burn-down
    provides: "d3d9 dummy-device getVtbl (commit 2c57d38) replacing the broken byte-pattern scan"
  - phase: 02.1-critical-bug-burn-down
    provides: "Phase B/B-bis owned-popup window-ownership work (commits 2ce028c, 1789400) with HWND_TOP Z-order fix"
  - phase: 02.1-critical-bug-burn-down
    provides: "Phase H chat-context fix (commit 6047416) so WantTextInput-bound widgets route keyboard input correctly"
provides:
  - "Tier-4 sign-off that the imgui in-game overlay renders end-to-end over live SWG (D-11 exit criterion satisfied)"
  - ".planning/codebase/TESTING.md Tier 4 — Manual Residual Enumeration section with the first canonical row"
  - "Latent regression detectors in imgui_impl.cpp (two one-shot static-bool-guarded log lines) at debug level"
  - "g_showDemoWindowProbe gate (dormant at false; one-line edit re-enables for Wave-1 plugin styling work)"
  - "06-01-DEMO-PROBE-NOTES.md investigation log with d3d9 pattern-scan disposition + live-SWG transcript + Wave-1 transparency design notes"
  - "06-01-VERIFICATION.md maintainer-signed Tier-4 evidence"
affects: [06-02-dep-bumps, 06-06-tier-4-doc-1-0-cut, phase-07-tre-browser, phase-08-iff-editor, phase-09-datatable-editor, phase-10-stringtable-editor, phase-11-object-template-editor]

tech-stack:
  added: []
  patterns:
    - "Tier-4 row template (#, Scenario, Procedure, Success Criterion, Last-Verified SHA) — D-19 enumeration grows from this row as Phases 6-02 through 6-06 land their procedures"
    - "Probe-then-rollback pattern: ship diag instrumentation behind a gate (g_showDemoWindowProbe + static-bool one-shots), maintainer-verify, then flip the gate off and demote logs to debug while leaving the call sites in place for future re-enablement"

key-files:
  created:
    - .planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-DEMO-PROBE-NOTES.md
    - .planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-VERIFICATION.md
  modified:
    - UtinniCore/swg/ui/imgui_impl.cpp
    - .planning/codebase/TESTING.md

key-decisions:
  - "D-11 exit criterion (full ImGui::ShowDemoWindow over live SWG with all seven widget categories) confirmed satisfied — no fix required for the overlay render path"
  - "d3d9 pattern-scan suspicion resolved as already-not-the-cause (Phase 02.1 commit 2c57d38 replaced byte-pattern scan with dummy-device approach) — recorded in DEMO-PROBE-NOTES per [[feedback-d3d9-hook-diagnosis]] memory"
  - "Diag instrumentation retained at debug level behind static-bool one-shot guards as latent regression detectors — silent in normal play, fires once if regression returns"
  - "g_showDemoWindowProbe flag retained in source (flipped false) so Wave-1 TJT subpanel styling work can re-enable Demo with a one-line edit + rebuild"
  - "Wave-1 transparency / chromeless HUD-style overlay options captured as deferred design item — not 06-01 scope"

patterns-established:
  - "Tier-4 row table format in TESTING.md (#, Scenario, Procedure, Success Criterion, Last-Verified SHA)"
  - "Maintainer-signed VERIFICATION.md companion per Tier-4 row (date, machine identifier, GPU vendor, signature, one-paragraph exercise description, cross-links)"

requirements-completed: [STAB-03]

duration: 70min
completed: 2026-05-23
---

# Phase 6 Plan 01: Overlay-Debug Investigation Summary

**ImGui::ShowDemoWindow rendered end-to-end over live SWG with all seven D-11 widget categories functional; d3d9 pattern-scan suspicion resolved as already-not-the-cause; D-11 exit criterion satisfied and 06-02 imgui docking-branch switch unblocked.**

## Performance

- **Duration:** ~70 min (continuation execution; planning + investigation across the day per maintainer)
- **Started:** 2026-05-23 (continuation agent spawn-time)
- **Completed:** 2026-05-23T22:30Z (approx; per commit timestamp on 2e0dcf5)
- **Tasks:** 3 (2 diag-probe tasks + 1 sign-off checkpoint task)
- **Files modified:** 4 (`UtinniCore/swg/ui/imgui_impl.cpp`, `.planning/codebase/TESTING.md`, plus two new planning files)

## Accomplishments

- **Tier-4 sign-off recorded** that the imgui in-game overlay renders end-to-end in Utinni-injected SWG sessions. All seven D-11 exit-criterion widget categories (menus, sliders, buttons, tabs, plots, popups, drag-and-drop) verified functional over live SWG. Stale "imgui overlay has never displayed" belief disposed of as superseded by Phase 02.1 + Phase B/B-bis + Phase H landings; no separate fix needed.
- **30-second [[feedback-d3d9-hook-diagnosis]] memory honoured.** Pattern-scan check resolved in seconds (already not the cause per Phase 02.1 commit `2c57d38`'s dummy-device approach) — recorded with verbatim quotes of `directx9.cpp::getVtbl` header and STATE.md "Blockers/Concerns" item #2 in `06-01-DEMO-PROBE-NOTES.md`.
- **Latent regression detectors landed** in `imgui_impl.cpp`: two one-shot static-bool-guarded log lines (`imgui_impl::setup complete, isSetup=true` + `imgui_impl::render entered isSetup branch`) at debug level. Dormant in normal play; surface once each if the regression returns.
- **Probe gate retained for Wave-1.** `g_showDemoWindowProbe` (file-scope inside `namespace imgui_impl`, flipped `false`) keeps the `ImGui::ShowDemoWindow(nullptr)` call site in source so Wave-1 TJT subpanel styling work (Phases 7-11) can re-enable Demo with a one-line flag flip + rebuild.
- **Tier-4 row #1 authored** in `.planning/codebase/TESTING.md` per D-19; the full residual enumeration grows from this row as Phases 6-02 through 6-06 land their procedures.
- **Maintainer-signed VERIFICATION.md** captures the Tier-4 evidence (date, machine, signature, exercise paragraph, cross-links).

## Task Commits

Each task committed atomically:

1. **Task 1: d3d9 pattern-scan disposition + isSetup observation probe** — `2694d3f` (diag)
2. **Task 2: ShowDemoWindow probe additive to render()** — `23ac35f` (diag)
3. **Task 3: Tier-4 sign-off + TESTING.md row + diag rollback** — `2e0dcf5` (docs)

CppSharp regenerates `UtinniCoreDotNet/Generated/UtinniCore.cs` (~2084 lines) on every full-sln build via the post-build chain (`CON-T-01`). That drift was deliberately excluded from all three commits — staging was done with explicit per-file `git add`, never `git add -A`. The codegen drift is unrelated to 06-01's behavior changes; it would regenerate identically from any clean post-build of master.

## Files Created/Modified

- `UtinniCore/swg/ui/imgui_impl.cpp` — added two one-shot static-bool-guarded log probes (setup-complete + render-entry); added `g_showDemoWindowProbe` file-scope gate + `ImGui::ShowDemoWindow(nullptr)` call site immediately after `ImGui::NewFrame()` inside the `if (isSetup)` branch; flipped probe gate to `false` and demoted setup-complete log to `debug` at Task 3 sign-off.
- `.planning/codebase/TESTING.md` — appended new `## Tier 4 — Manual Residual Enumeration` section with one row ("Imgui overlay Demo screen over live SWG") containing procedure, success criterion, and last-verified SHA placeholder. D-19's full enumeration grows from here.
- `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-DEMO-PROBE-NOTES.md` (new) — investigation log: d3d9 pattern-scan disposition (verbatim quotes), Task 1 diag-instrumentation description, Task 2 ShowDemoWindow probe description, live-SWG observation transcript with utinni.log session timeline, root-cause disposition for the stale "never displayed" belief, and a Wave-1 TJT subpanel transparency / chromeless HUD design-options note (deferred — not 06-01 scope).
- `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-VERIFICATION.md` (new) — maintainer-signed Tier-4 evidence: 2026-05-23 date, Windows 11 26200.8457 machine identifier, GPU vendor (TBD by maintainer per session-log capture), Kenneth Long signature, one-paragraph exercise description, cross-links to DEMO-PROBE-NOTES + TESTING.md + 06-01-PLAN.md + 06-CONTEXT.md decisions D-06 / D-11. CI-green post-merge note flagging the orchestrator's owed validation.

## Decisions Made

- **No code-level fix required for the overlay render path.** D-11's exit criterion was satisfied by previously-landed work (Phase 02.1's d3d9 dummy-device + Phase B/B-bis owned-popup ownership + Phase H chat-context). Confirmed with live-SWG verification; no architectural escalation triggered.
- **Diag instrumentation retained at debug level rather than removed entirely.** Two static-bool one-shot probes survive into production as latent regression detectors — silent in normal play, fire once each if the regression returns. Chosen over the "remove" option in the plan's "convert to debug level only OR remove" guidance per [[feedback-max-harness]] (any future regression must be detectable without re-instrumenting from scratch).
- **`g_showDemoWindowProbe` call site retained behind a `false` gate.** Re-enabling for Wave-1 TJT subpanel styling work is a one-line edit + rebuild — cheaper than re-authoring the probe code from memory each time a styling question comes up.
- **Tier-4 row #1 lives in TESTING.md as a growable table.** D-19's full enumeration extends row-by-row across Phases 6-02 through 6-06, with the final list owned by plan 06-06.
- **GPU vendor in VERIFICATION.md marked "TBD by maintainer"** rather than guessed. The utinni.log capture would carry the adapter info; the orchestrator can fill it in if the verification session log is available, otherwise the field stays explicit-unknown rather than potentially-wrong.

## Deviations from Plan

**None — plan executed exactly as written.** All three tasks landed per their plan-spec acceptance criteria; the only out-of-scope notes captured are:

- The two-day-old previous-executor work (commits `d5d1e7e`, `b1bc760`, `171a5ec`) on a now-stale worktree branch was redone identically here (Tasks 1 + 2 commits `2694d3f` + `23ac35f`) and extended with Task 3 (`2e0dcf5`) on the current worktree branch. This is not a deviation — it's continuation-agent behavior per the orchestrator's spawn spec.
- The `UtinniCoreDotNet/Generated/UtinniCore.cs` CppSharp codegen drift (~2084 lines of unrelated churn produced by every full-sln post-build) was deliberately excluded from all three commits per the orchestrator's spawn instructions. Explicit per-file `git add` calls never `-A` / `.`.
- The `build_06_01.bat` VsDevCmd wrapper used to invoke msbuild from inside this Linux-style worktree shell stays untracked per orchestrator instruction; not committed.

## Issues Encountered

- **`msbuild` not on PATH** in the worktree's PowerShell/Bash environment. Resolved by authoring `build_06_01.bat` calling `VsDevCmd.bat -arch=x86 -no_logo` then `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86 /t:UtinniCore`. The bat lives in the worktree root untracked (per spawn-spec).
- **Three builds total** across Tasks 1 + 2 + 3, all exited cleanly (exit code 0). The only build warnings are pre-existing C4309 / C4091 / C4251 / C4099 / C4018 / C4244 (truncation, dll-interface, signed/unsigned, conversion) in unrelated translation units — same warnings as the master baseline at `0dc8646`. Not Phase 6 scope.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- **06-02 imgui docking-branch switch (D-06) is unblocked.** The base imgui render path is now confirmed healthy; the docking-branch port via vcpkg can proceed at plan-time without an "is it even rendering?" prerequisite hanging over it.
- **Wave-1 (Phases 7-11) gains a one-line Demo screen tool.** Maintainers and reviewers can flip `g_showDemoWindowProbe = true` + rebuild to compare a new TJT subpanel's appearance against the canonical imgui Demo as a known-good reference.
- **CI green confirmation on master is OWED by the orchestrator post-merge.** This executor cannot meaningfully run `gh run list --branch master --limit 1 --json conclusion -q '.[0].conclusion'` because the relevant commits only land on master after the worktree merge. The orchestrator should verify CI returns `"success"` on the merge commit before marking 06-01 done in `STATE.md`. Recorded in `06-01-VERIFICATION.md` § "Post-Merge Validation Owed by Orchestrator".

## Self-Check: PASSED

- File: `UtinniCore/swg/ui/imgui_impl.cpp` — present, contains `g_showDemoWindowProbe = false` (post-rollback) and `ImGui::ShowDemoWindow(nullptr)` call site (retained) and two static-bool one-shot debug probes
- File: `.planning/codebase/TESTING.md` — present, contains "Imgui overlay Demo screen over live SWG" Tier-4 row
- File: `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-DEMO-PROBE-NOTES.md` — present
- File: `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-VERIFICATION.md` — present
- Commit: `2694d3f` (Task 1) — present in `git log --oneline`
- Commit: `23ac35f` (Task 2) — present in `git log --oneline`
- Commit: `2e0dcf5` (Task 3) — present in `git log --oneline`

---
*Phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut*
*Completed: 2026-05-23*
