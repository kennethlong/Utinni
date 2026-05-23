---
phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut
plan: 01
subsystem: imgui-overlay-investigation
tags: [diag, imgui, d3d9, tier4, overlay-debug, paused-at-checkpoint]
status: paused-at-checkpoint
checkpoint:
  type: human-verify
  gate: blocking
  task: 3
dependency_graph:
  requires:
    - Phase 02.1 d3d9 dummy-device approach (commit 2c57d38) — pattern-scan
      already-not-the-cause baseline
    - utinni::log::info / utinni::log::debug facade (UtinniCore/utility/log.h)
  provides:
    - 06-01-DEMO-PROBE-NOTES.md (live disposition write-up + placeholders for
      maintainer observations)
    - imgui_impl::setup one-shot info tripwire (`imgui_impl::setup complete,
      isSetup=true`)
    - imgui_impl::render one-shot debug tripwire (`imgui_impl::render entered
      isSetup branch`)
    - file-scope `static bool g_showDemoWindowProbe = true;` flag in
      imgui_impl.cpp
    - ImGui::ShowDemoWindow(nullptr) call site gated behind the probe flag
  affects:
    - 06-02 imgui docking-branch switch (gated on this plan's exit criterion)
tech_stack:
  added: []
  patterns:
    - one-shot static-bool guarded log tripwire (matches existing
      `s_firstBeginScene` / `s_firstPresent` pattern in directx9.cpp)
    - file-scope flag-gated additive ImGui probe (preserves existing
      renderCallbacks dispatch unchanged)
key_files:
  created:
    - .planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-DEMO-PROBE-NOTES.md
  modified:
    - UtinniCore/swg/ui/imgui_impl.cpp (Task 1: 2 one-shot log lines + guards;
      Task 2: file-scope probe flag + ShowDemoWindow call site)
decisions:
  - "Pattern-scan disposition recorded ALREADY-NOT-THE-CAUSE (Phase 02.1 commit
    2c57d38 retired the broken pattern-scan; dummy-device approach in
    directx9.cpp::getVtbl is the canonical path). Per
    [[feedback-d3d9-hook-diagnosis]] 30-sec first move, this resolved in
    seconds, not multi-day investigation."
  - "ShowDemoWindow probe defaults to enabled (`g_showDemoWindowProbe = true`)
    so the next maintainer launch against this build lights up the Demo
    screen automatically. Task 3 (continuation agent) flips to `false` after
    Tier-4 sign-off but keeps the call site for re-enablement."
  - "Codegen drift in UtinniCoreDotNet/Generated/UtinniCore.cs (2084 lines of
    churn per build run) treated as out-of-scope per SCOPE BOUNDARY rule.
    Reverted from both Task 1 and Task 2 commits — drift is pre-existing
    CppSharp-regen noise, not caused by imgui_impl.cpp changes."
metrics:
  duration_minutes: ~20
  tasks_completed: 2
  tasks_total: 3
  tasks_remaining: 1
  files_created: 1
  files_modified: 1
  commits: 2
  builds: 2
  completed_date: "2026-05-23"
  paused_at: "Task 3 (checkpoint:human-verify, gate=blocking)"
---

# Phase 6 Plan 1: Overlay-debug investigation — ImGui::ShowDemoWindow exit criterion Summary

**One-liner:** Diag tripwires + `ImGui::ShowDemoWindow` probe shipped on
`worktree-agent-aedca507c501c71b9` for the imgui-overlay-never-displays
investigation; pattern-scan disposition recorded as already-resolved by Phase
02.1 commit `2c57d38`; Tasks 1+2 atomic-committed; Task 3 PAUSED awaiting
maintainer live-SWG Demo-screen exercise.

## Completed Tasks

| Task | Name                                                    | Status   | Commit    |
| ---- | ------------------------------------------------------- | -------- | --------- |
| 1    | d3d9 pattern-scan disposition + isSetup observation probe | done   | `d5d1e7e` |
| 2    | ShowDemoWindow probe additive to render()                | done    | `b1bc760` |
| 3    | Tier-4 sign-off + TESTING.md Tier-4 row + diag rollback  | **paused (checkpoint:human-verify, blocking)** | — |

### Task 1: d3d9 pattern-scan disposition + isSetup observation probe (`d5d1e7e`)

- Created `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-DEMO-PROBE-NOTES.md` with:
  - Verbatim quote of `directX::getVtbl()` body from `UtinniCore/swg/graphics/directx9.cpp` lines 426-526 (dummy-device approach).
  - Verbatim quote of `.planning/STATE.md` "Blockers/Concerns" item #2 (resolved 2026-05-19 by commit `2c57d38`).
  - Explicit conclusion: **pattern-scan is NOT the cause of imgui not rendering** (the `[[feedback-d3d9-hook-diagnosis]]` "30-second first move" resolves immediately to "already not the cause").
  - Placeholder sections for maintainer's live-SWG observation transcripts (Task 1 log lines + Task 2 Demo-screen YES/NO exercise result).
- Added two diag tripwires to `UtinniCore/swg/ui/imgui_impl.cpp`:
  - `setup(IDirect3DDevice9*)`: one-shot `utinni::log::info("imgui_impl::setup complete, isSetup=true")` gated by `static bool sLoggedOnce`. Fires exactly once when the `isSetup` gate flips true.
  - `render()`: one-shot `utinni::log::debug("imgui_impl::render entered isSetup branch")` gated by `static bool sLoggedOnceRender`, placed at the top of the `if (isSetup)` branch. Fires exactly once when render() first crosses the gate.
- **Pattern parity:** these match the existing `s_firstBeginScene` / `s_firstPresent` one-shot static-bool guarded tripwires already in `directx9.cpp:242, 278`. Same idiom, same intent.

### Task 2: ShowDemoWindow probe additive to render() (`b1bc760`)

- Added file-scope `static bool g_showDemoWindowProbe = true;` near the top of `namespace imgui_impl` in `imgui_impl.cpp` (next to `bool enableUi;` / `bool rendering;`).
- Added `ImGui::ShowDemoWindow(nullptr)` call immediately after `ImGui::NewFrame()` inside the `if (isSetup)` branch of `render()`, gated by `if (g_showDemoWindowProbe)`.
- Existing `renderCallbacks` dispatch path is **unchanged** — the Demo window is additive instrumentation, not a replacement. `ShowDemoWindow` is self-contained (calls `ImGui::Begin/End` internally), so it composes safely with the surrounding `Begin("Tests")` / `End()` in the existing render() body.
- Default value `true` so the Demo screen lights up by default on the next maintainer live-SWG session. Task 3 will flip to `false` post-Tier-4 sign-off (preserves re-enable path).

## Verification Status

| Criterion                                                                              | Status        |
| -------------------------------------------------------------------------------------- | ------------- |
| 06-01-DEMO-PROBE-NOTES.md exists + quotes `directx9.cpp::getVtbl` verbatim             | ✓ passed      |
| 06-01-DEMO-PROBE-NOTES.md quotes STATE.md Blockers item #2 verbatim                    | ✓ passed      |
| imgui_impl.cpp grep `imgui_impl::setup complete` returns 1 match inside static guard   | ✓ passed (1 match @ line 340 inside `if (!sLoggedOnce)`) |
| imgui_impl.cpp grep `render entered isSetup branch` returns 1 match inside guard       | ✓ passed (1 match @ line 433 inside `if (!sLoggedOnceRender)` inside `if (isSetup)`) |
| Task 1: imgui_impl.cpp grep `ShowDemoWindow` returns no matches                        | ✓ passed at Task 1 commit (Task 2 then added the call site as planned) |
| Task 2: imgui_impl.cpp grep `ShowDemoWindow` returns ≥1 match inside render() body     | ✓ passed (1 match @ line 451 inside `if (g_showDemoWindowProbe)`) |
| Task 2: imgui_impl.cpp grep `g_showDemoWindowProbe` returns 1 declaration + ≥1 read    | ✓ passed (1 decl @ line 135, 1 read @ line 449) |
| Release x86 build succeeds (`msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86 /t:UtinniCore` exit code 0) | ✓ passed (Task 1 build + Task 2 build both `MSBUILD_EXIT=0`) |
| Task 1 commit prefix `diag(06-01):`                                                    | ✓ passed (`d5d1e7e diag(06-01): imgui isSetup + render-entry one-shot probe`) |
| Task 2 commit prefix `diag(06-01):`                                                    | ✓ passed (`b1bc760 diag(06-01): ShowDemoWindow probe additive to render()`) |
| Task 1 + Task 2 human-check (live-SWG observation)                                     | **deferred — placeholders left for maintainer in 06-01-DEMO-PROBE-NOTES.md** |
| Task 3 (Tier-4 sign-off + TESTING.md row + diag rollback)                              | **paused — checkpoint:human-verify gate=blocking** |

The two `human-check` lines from Tasks 1 and 2 (one-shot log lines + Demo
screen YES/NO disposition) are explicitly deferred to maintainer live-SWG
observation per `plan_specific_guidance`. They are NOT blocking for the
executor — Task 1 and Task 2 are atomically committed once build passes and
the verify-able acceptance criteria (greps, file existence, build exit code,
commit prefix) pass.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking issue] Reverted codegen drift in `UtinniCoreDotNet/Generated/UtinniCore.cs`**

- **Found during:** Task 1 build (and again during Task 2 build).
- **Issue:** Running the Release x86 UtinniCore build also runs the
  CppSharp-driven post-build step that regenerates
  `UtinniCoreDotNet/Generated/UtinniCore.cs`. Each regeneration produces
  ~2084 lines of churn (including removal of a `NewPlaceholder` class that
  was last committed in commit `9248a1a`). This drift is **pre-existing**
  (visible in every build invocation across the repo's history) and is
  unrelated to my imgui_impl.cpp changes — my edits only added two
  `utinni::log::*` call sites and a new file-scope flag inside
  UtinniCore, none of which alter the UtinniCore.dll public API surface
  consumed by CppSharp.
- **Fix:** `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs`
  before staging both Task 1 and Task 2 commits. Per SCOPE BOUNDARY
  rule, pre-existing unrelated drift is out of scope for plan 06-01 — it
  belongs in a future "stabilize generated file" effort (a candidate for
  Phase 6 Plan 5 STAB-03 cleanups per D-16 polish bundle, or a separate
  STAB-03 item if it persists).
- **Files modified:** none (revert restored to base).
- **Commits:** —

**2. [Rule 3 - Blocking issue] Created `build_06_01.bat` helper for VsDevCmd
+ msbuild invocation**

- **Found during:** Task 1 build setup.
- **Issue:** `msbuild` is not on PATH in this worktree's shell; the local
  VS toolchain is VS 2026 (Dev18 at `D:\Program Files\Microsoft Visual
  Studio\18\Community\`) per `[[project-vs2026-toolchain]]`. Initial
  attempts to invoke msbuild via `cmd.exe /c` or PowerShell wrappers had
  output-capture issues (the cmd banner echoed but the build output was
  swallowed before printing).
- **Fix:** Authored a small `build_06_01.bat` that `call`s
  `VsDevCmd.bat -arch=x86 -no_logo` then runs the documented msbuild
  command. Executing the batch file directly (`./build_06_01.bat`)
  produced clean output and `MSBUILD_EXIT=0` on both Task 1 and Task 2
  builds.
- **Files modified:** untracked helper at repo root; intentionally
  **not committed** (transient scaffolding, not a deliverable). Belongs
  in a `tools/` folder if it survives the cleanup pass — current
  positioning is intentional to keep it out of the commit and signal
  "throwaway".
- **Commits:** —

### No Deviations vs. Plan Acceptance Criteria

Every acceptance criterion in Task 1 and Task 2 was hit exactly as written.
The `<human-check>` lines (one-shot log observation + Demo screen YES/NO)
are explicitly maintainer post-commit observations per
`plan_specific_guidance` and are intentionally left as placeholders in
`06-01-DEMO-PROBE-NOTES.md`.

## Authentication Gates

None. This plan touches no external service; all changes are local source +
build.

## Known Stubs

None. The two diag log lines and the `g_showDemoWindowProbe`-gated
`ShowDemoWindow` call are intentional, fully wired, and exercised by the
build. They are NOT stubs — they are observation instrumentation expected to
fire on the maintainer's next live SWG session.

## Checkpoint Pause — Task 3

Per `<task type="checkpoint:human-verify" gate="blocking">` and
`plan_specific_guidance`, **Task 3 is paused.** A continuation agent will be
spawned by the orchestrator AFTER the maintainer's live-SWG sign-off to:

1. Confirm `06-01-DEMO-PROBE-NOTES.md` "Demo Screen Exercise Result"
   maintainer-signed YES across all seven widget categories.
2. Flip `g_showDemoWindowProbe = false` in `imgui_impl.cpp` (literal
   `static bool g_showDemoWindowProbe = false;`) AND remove the two
   one-shot static-bool diag log lines from `setup()` + `render()` (or
   convert them to `debug` level — planner discretion at sign-off, pick
   one and stay consistent). **Keep the `ShowDemoWindow` call site
   itself** so future Wave-1 work can re-enable by flipping the flag.
3. Author the `.planning/codebase/TESTING.md` Tier-4 enumeration row
   "Imgui overlay Demo screen over live SWG" with procedure + success
   criterion + last-verified SHA.
4. Write `06-01-VERIFICATION.md` as the maintainer-signed Tier-4
   evidence (date + machine identifier + GPU vendor + paragraph
   describing the exercise).
5. Commit atomically as `docs(06-01): Tier-4 overlay Demo signoff +
   TESTING.md row + diag rollback`.
6. Confirm CI green on master post-merge.

## Self-Check

- [x] `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-DEMO-PROBE-NOTES.md` exists
- [x] Commit `d5d1e7e` exists on `worktree-agent-aedca507c501c71b9`
- [x] Commit `b1bc760` exists on `worktree-agent-aedca507c501c71b9`
- [x] `UtinniCore/swg/ui/imgui_impl.cpp` modified (4 changes: 2 from Task 1, 2 from Task 2)
- [x] Release x86 UtinniCore.dll built successfully on both Task 1 and Task 2 builds

## Self-Check: PASSED
