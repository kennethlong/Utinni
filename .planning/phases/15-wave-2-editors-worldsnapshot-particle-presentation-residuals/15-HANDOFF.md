# Phase 15 Execution — Handoff (paused at 15-08 live-smoke checkpoint)

> Written 2026-06-09 mid `/gsd-execute-phase 15`. Session is being restarted.
> **TL;DR:** 7 of 8 plans are 100% complete and committed. The only remaining work is the
> **Task 2 human-action live-SWG smoke in plan 15-08** — a maintainer must inject the assembled
> build into a live SWG client and sign `15-SMOKE.md`. After that, a fresh continuation agent
> writes `15-08-SUMMARY.md`, then the phase gates + verifier run and the phase is marked complete.

---

## Where we are

- **Phase:** 15 — `wave-2-editors-worldsnapshot-particle-presentation-residuals`
- **Milestone:** v2.0 — AI-Assisted SWG Tools
- **Phase dir:** `.planning/phases/15-wave-2-editors-worldsnapshot-particle-presentation-residuals`
- **Requirements in this phase:** PROD-W2-WS, PROD-W2-PRT, RESID-03, RESID-04
- **Structure:** 8 plans across 4 waves. Worktrees are OFF for this repo → plans ran
  **sequentially inline** on the main tree, each via a fresh `gsd-executor` agent (model `opus`).
- **Branching:** `none` — all work is on `master`. No feature branch.

### Git state at pause
- **Utinni** `D:/Code/Utinni` HEAD = `ebd63ba` (`docs(15-08): record Task-1-complete / Task-2 checkpoint position in STATE`). Working tree **clean**.
- **UtinniPlugins** `D:/Code/UtinniPlugins` HEAD = `589f206` (`feat(15-06): FormParticleEditor …`). Working tree **clean**.
- Both repos clean; nothing uncommitted; nothing stashed.

> ⚠ The spawned agent IDs from the previous session are dead after restart. The 15-08
> continuation MUST be a **fresh** `gsd-executor` agent (GSD contract: "spawn continuation,
> not resume"). Do NOT try SendMessage to old agent IDs.

---

## Plan-by-plan status (waves 1–3 = DONE)

| Plan | Wave | Status | Commits | Verification |
|------|------|--------|---------|--------------|
| 15-01 WorldSnapshot placements editor + bulk ops (PROD-W2-WS) | 1 | ✅ done | Utinni `f1192e5`,`18eb8be`; UtinniPlugins `ed7f156`,`e0dacd7` | 5/5 SnapshotBulk facts; TJT Debug\|x86 green |
| 15-02 `.prt`/FORM PEFT typed codec (PROD-W2-PRT codec-half) | 1 | ✅ done | `cfb09b0`,`7242701`,`cdbf428`,`43243fb` | 21/21 particle facts + full 663/663 suite, Debug+Release |
| 15-03 D-09 live-preview hot-retrigger SPIKE | 1 | ✅ done | `cfe1b1d`,`11cfc86`,`a9797b6` | Honest finding: **no clean native hook reachable**; shipped documented no-op seam (`isRetriggerAvailable()→false`). UtinniCore Release green |
| 15-04 `.prt` CLI + MCP read-assist (PROD-W2-PRT cli/mcp-half) | 2 | ✅ done | `a868d4f`,`470dd28`,`7652b98` | CLI 249 pass/2 skip; MCP 77/77; zero format logic in MCP (D-06/D-07) |
| 15-05 RESID-04 exclusive-fullscreen suppress + no-Reset gate | 2 | ✅ done | `bf5843d`,`6ae1dd7`,`2702db1`,`b375fc7` | UtinniCore Release green; `[resid04]` 8 assertions pass |
| 15-06 FormParticleEditor UI (PROD-W2-PRT ui-half) | 3 | ✅ done | Utinni `26af8e6`,`3fc0eee`; UtinniPlugins `589f206` | ParticleHandoffPolicy 18 facts; TJT Debug\|x86 green; degraded preview wired to 15-03 seam |
| 15-07 RESID-03 reload-classifier routing (`.ws`/`.prt`→tier-b) | 3 | ✅ done | `c63122a`,`700bef9` | 15/15 ReloadRouting tests green |
| **15-08 Tier-4 maintainer live-SWG smoke** | 4 | ⏸ **Task 1 done, Task 2 (human-action) AWAITING** | Task 1: `2eef253` (smoke draft), `ebd63ba` (STATE pos) | see below |

All 7 completed plans have SUMMARY.md on disk. STATE/ROADMAP/REQUIREMENTS were updated by each
sequential executor as it finished.

### Important requirement-status nuance for the verifier
- 15-02 marked **PROD-W2-PRT in-progress (codec half)**; 15-04 then marked **PROD-W2-PRT complete**.
  The UI half (15-06) and live demo (15-08) are the remaining proof surfaces. The verifier should
  cross-check that PROD-W2-PRT is genuinely satisfied across codec+cli+mcp+ui and that the live
  demo half is captured by the 15-08 smoke.
- RESID-03 and RESID-04 were marked complete in REQUIREMENTS by 15-07 / 15-05 respectively, but
  both have a **live-confirmation half folded into the 15-08 smoke** (the badge-candor observation
  and the windowed-embedded/no-Reset matrix). They are not truly closed until 15-SMOKE.md is signed.

---

## 15-08 — the only remaining work

**Task 1 (auto) — COMPLETE & committed (`2eef253`).** All builds + suites green:
- `Utinni.sln` Release|x86 (VS2026 MSBuild v145) — exit 0
- `TheJawaToolbox.sln` Release|x86 — exit 0
- `UtinniCoreDotNet.Tests` 690 passed · `Utinni.Cli.Tests` 249 passed/2 skipped · `Utinni.Mcp.Tests` 77 passed
- Native `UtinniCore.Tests.exe` 84 assertions/27 cases; `[resid04]` no-Reset gate 8 assertions ✅
- **Assembled injection build:** `D:/Code/Utinni/bin/Release/` — `Launcher.exe` + `UtinniCore.dll` +
  `UtinniCoreDotNet.dll` + `Plugins/TheJawaToolbox/{TheJawaToolbox.dll, TheJawaToolboxDotNet.dll, Resources/, input.ini, settings.ini}`.
  DISCL diagnostic + suppress-toggle log will write to `D:/Code/Utinni/bin/Release/utinni.log`.
- Drafted checklist: `15-SMOKE.md` (committed) — four checklists ready to fill.

**Task 2 (checkpoint:human-action, gate=blocking) — AWAITING THE MAINTAINER.**
Automation cannot reach this (CON-TT-03: live GPU/D3D9 render judgment). The maintainer must:

1. **PROD-W2-WS** — Load a `.ws` snapshot → `Placements…` table → single-select drives gizmo →
   multi-select → bulk **Move / Delete (red confirm) / Retemplate** visibly change in-world
   placements → undo reverses each. Confirm SubPanel uses the unchanged Wave-1 MEF seam.
2. **PROD-W2-PRT** — Extract a real `.prt` via TRE Browser → open in `FormParticleEditor` →
   emitter tree + typed grid populate, unknowns greyed-hex (D-05) → edit a leaf → `Save (loose
   override)` → `Explain effect` read-assist fills. **Expected HONEST fallback = PASS:** `Preview in
   client` is **disabled** with tier-(b) badge `Reloads on next scene change or relog.` (no
   reachable native hook this phase, per 15-03 spike).
3. **RESID-04** — In `utinni.log` confirm `DI::SetCooperativeLevel … EXCLUSIVE …` (A4 trigger +
   caller) AND `D-12 suppress → redirected EXCLUSIVE to NONEXCLUSIVE`; walk the edge-case matrix
   (windowed↔fullscreen, login→world, chat-Enter, max/min/restore, free resize, multi-cycle,
   alt-tab, DPI); A/B the suppress toggle (`DirectInput::setSuppressExclusiveFullscreen(false)`
   live, then restore ON); confirm **no crash and no Utinni-initiated device Reset** (D-13).
4. **RESID-03** — Save `.stf` + `.ot` loose-override edits → TJT-driven scene change → record
   render-on-reload vs relog-only → confirm the editor badge copy is honest (amend to relog wording
   if relog-only).

Then record outcomes/defects in `15-SMOKE.md`, **update/close the two folded RESID todos**
(`.planning/todos/pending/swg-window-resize-fullscreen-edge-cases.md` [RESID-04],
`.planning/todos/pending/phase10-stringtable-sc3-live-reload-residual.md` [RESID-03]), and type
**"approved"** in the smoke-log sign-off block.

> Optional automation aid: the WinApp-MCP (`windows-mcp`, wired in `.mcp.json`) can drive the TJT
> WinForms UI click-throughs (UIA-based; TJT scene trigger is WinForms-drivable). But the visual
> judgments (in-world placement change, exclusive-fullscreen behavior, render-on-reload) need human
> eyes on a live GPU session. See memory `reference_winapp_mcp_testing`.

---

## How to resume (exact steps)

### If the maintainer has run the smoke and signed `15-SMOKE.md` ("approved")
1. **Spawn a fresh `gsd-executor` continuation agent** (model `opus`, NO worktree isolation —
   sequential on main tree) to close 15-08:
   - Verify the maintainer sign-off + the four checklists in `15-SMOKE.md`.
   - Update/close the two folded RESID todos per the recorded findings.
   - Write `15-08-SUMMARY.md`.
   - Update STATE.md + `gsd-sdk query roadmap.update-plan-progress 15 15-08 complete`.
   - Commit (SUMMARY first, then tracking).
2. Then run the **post-15-08 phase-completion flow** (see next section).

### If the maintainer reports DEFECTS
- Route to gap closure: `/gsd:plan-phase 15 --gaps` → creates `gap_closure: true` plans →
  `/gsd:execute-phase 15 --gaps-only`. Do NOT mark the phase complete.

### Simplest path
Re-run `/gsd-execute-phase 15`. The SDK will see 7/8 SUMMARYs present and only 15-08 incomplete;
discovery filters completed plans (skip `has_summary: true`), so it resumes at Wave 4 / 15-08.
**Caveat:** 15-08 Task 1 is already committed — the continuation must NOT rebuild/redo Task 1; it
should detect the `2eef253` smoke draft + checkpoint state and proceed straight to verifying the
human sign-off. Brief the spawned agent on this so it doesn't repeat the ~build.

---

## Post-15-08 phase-completion flow (orchestrator runs these, in order)

Per `~/.claude/get-shit-done/workflows/execute-phase.md`, after the last plan completes:
1. **code_review_gate** (REQUIRED, advisory): `Skill(gsd-code-review, "15")` → check
   `15-REVIEW.md` status; surface but don't block.
2. **regression_gate**: run prior-phase test suites (catch cross-phase regressions). For this repo:
   managed suites via `dotnet test --no-build`; native `UtinniCore.Tests.exe`. (Build with VS2026
   MSBuild — `dotnet build` fails on .resx, see gotchas.)
3. **schema_drift_gate / codebase_drift_gate**: non-blocking here (no DB ORM in this project).
4. **verify_phase_goal**: spawn `gsd-verifier` (model `opus`) → writes `15-VERIFICATION.md`.
   - Phase goal + req IDs: PROD-W2-WS, PROD-W2-PRT, RESID-03, RESID-04 (from ROADMAP).
   - If `passed` → update_roadmap. If `human_needed` → persist `15-HUMAN-UAT.md`. If `gaps_found`
     → present gaps, offer `/gsd:plan-phase 15 --gaps`.
5. **update_roadmap**: `gsd-sdk query phase.complete 15` (marks checkbox, advances STATE, updates
   REQUIREMENTS traceability), then commit ROADMAP/STATE/REQUIREMENTS/VERIFICATION.
6. **close_phase_todos / update_project_md / offer_next**: auto-close todos with
   `resolves_phase: 15`; evolve PROJECT.md; present next-phase options (NO auto-advance — auto-mode
   is false; do NOT run transition unless `--auto`).

---

## Critical config + gotchas (carry into the restart)

- **Worktrees OFF** (`workflow.use_worktrees=false`) → all executors run **sequentially inline** on
  the main tree. Never spawn parallel worktree agents for this repo.
- **executor_model=opus, verifier_model=opus, parallelization=true** (but worktrees-off forces
  serialization), **branching_strategy=none**, **commit_docs=true**, runtime **claude**.
- **SDK invocation:** `gsd-sdk query init.execute-phase 15` uses a **positional** phase arg
  (`--phase 15` returns phase_found=false — a gotcha that cost time this session).
- **Build reality (memory `feedback_dotnet_build_msbuild_resources`):** `dotnet build` FAILS on
  UtinniCoreDotNet/TJT (MSB3823 on image `.resx`). **Build with VS2026 MSBuild** at
  `D:/Program Files/Microsoft Visual Studio/18/Community`; run xUnit via `dotnet test --no-build`.
- **`Generated/UtinniCore.cs` regen churn (memory):** CppSharp reorders it every C++ build →
  symmetric no-op diff. Always `git checkout --` it, never commit. (All executors did this.)
- **CppSharp blocked on v145 (memory):** clang 11 can't parse v145 MSVC STL → cannot regenerate
  .NET bindings. This is why 15-03's live-preview hook is a documented no-op seam, not a new export.
- **Cross-repo authority (memory):** standing write authority on `D:/Code/UtinniPlugins`
  (kennethlong/UtinniPlugins); cross-repo paired commits need no human checkpoint. Files in each
  repo commit to that repo.
- **Standing push permission (memory):** `git push origin <branch>` pre-authorized for CI iteration
  (confirm force/delete). Nothing has been pushed yet this phase — both repos are ahead of origin.
- **`[CallerMemberName]`/defaulted-param additions break pre-built plugin DLLs (memory)** — rebuild
  cross-binary plugins in the same commit if a public UtinniCoreDotNet API changes.

## Key files touched this phase (for orientation)
- WorldSnapshot: `UtinniCoreDotNet/Commands/WorldSnapshotBulkComposer.cs`; UtinniPlugins
  `…/SWG/WorldSnapshotImpl.cs`, `…/UI/SubPanels/SnapshotPanel.cs`, `…/UI/Forms/FormSnapshotPlacements.cs`
- Particle codec: `UtinniCoreDotNet/Formats/Particle/*` (8 prod + tests)
- Particle CLI/MCP: `Utinni.Cli/Commands/{DecodeIffCommand,RoundtripParticleCommand}.cs`,
  `Utinni.Mcp/Tools/ReadTools.cs`
- Particle editor: UtinniPlugins `…/UI/Forms/FormParticleEditor.cs(.Designer.cs)`, `…/Plugin.cs`;
  `UtinniCoreDotNet/UI/ParticleHandoffPolicy.cs`
- RESID-04: `UtinniCore/swg/misc/direct_input.cpp`, `UtinniCore/swg/graphics/directx9.cpp`,
  `UtinniCoreDotNet/UI/Controls/PanelGame.cs`, `UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp`
- RESID-03: `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs`
- Preview seam: `UtinniCore/swg/scene/particle_preview.{h,cpp}`, `15-PARTICLE-PREVIEW-HOOK.md`
