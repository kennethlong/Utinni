# Phase 2: Critical bug burn-down (C-01..C-15) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-16
**Phase:** 2-critical-bug-burn-down-c-01-c-15
**Areas discussed:** Plan structuring / grouping, Verification contract per fix, C-01 architectural approach, Out-of-list scope (C-13 cross-repo, CON-O-03 delegate-pinning, open-Q resolution)

---

## Plan structuring / grouping

### Q1: How should we chunk the 15 fixes into plans?

| Option | Description | Selected |
|--------|-------------|----------|
| By risk tier (assessment.md weeks) | 3 plans: Plan 02-01 'Trivial criticals' (C-04, C-06, C-08, C-12, C-13, C-14), Plan 02-02 'Single-file criticals' (C-02, C-03, C-05, C-07, C-10, C-11, C-15), Plan 02-03 'Architectural' (C-01, C-09). Matches assessment.md §Recommended sequencing. Each plan = one weeklike chunk; CI gates between plans. | ✓ |
| Single bug per plan | 15 plans (02-01..02-15). Maximally reviewable; each fix lands behind its own green CI. Cost: more state-tracking overhead. | |
| By subsystem / file area | Group by code surface: 02-01 Native startup; 02-02 Managed callbacks; 02-03 Plugin loader/hotkeys; 02-04 Build/distribution; 02-05 UI thread. Cleaner review surface but interleaves risk classes. | |

**User's choice:** By risk tier (assessment.md weeks) (Recommended).
**Notes:** 3-plan tier structure further split in Q2.

### Q2: Plan 02-03 (architectural tier) holds C-01 + C-09 — combine or split?

| Option | Description | Selected |
|--------|-------------|----------|
| Split into 02-03 (C-01) + 02-04 (C-09) | Two separate plans; each architectural fix gets its own research/plan/execute lane. Cleaner roll-back surface; CI green between them. Phase 2 ends with 4 plans total. | ✓ |
| Combined Plan 02-03 (C-01 + C-09) | Single plan covers both. Tighter '~4-6 plans' fit. Larger PR / more manual verification scope; less revertable. | |
| Defer C-09 to its own follow-up phase | Carve C-09 into a Phase 2.1 decimal insertion. Cost: violates ROADMAP success criterion 1 (every C-01..C-15 closed in Phase 2). | |

**User's choice:** Split into 02-03 (C-01) + 02-04 (C-09) (Recommended).
**Notes:** Final plan structure — 02-01 trivial (6 bugs), 02-02 single-file (7 bugs + C-16 + KB-05), 02-03 architectural C-01, 02-04 architectural C-09.

---

## Verification contract per fix

### Q1: Verification posture for unit-testable managed-side fixes (C-04, C-06, C-07, C-08, C-10 reflection-bits, C-15)?

| Option | Description | Selected |
|--------|-------------|----------|
| Test required with every fix | Each managed-side fix lands with at least one xUnit regression test in the SAME commit. Test must be red before fix, green after. Matches Phase 1 Hotkey precedent. | ✓ |
| Test optional — fix + CI green is enough | Each fix lands behind green CI; regression test encouraged but not mandatory. Risks regression resurfacing (e.g., C-04 silently regressed since 2020). | |
| Test required only for resurfacing-class bugs | C-04, C-06, C-07 MUST have tests. C-08, C-10, C-15 — fix-only acceptable. Middle ground. | |

**User's choice:** Test required with every fix (Recommended).

### Q2 (user clarification): Can we generate a harness for any of these, so that we can minimize the requirement for me to review?

Claude reflected back the per-bug harness feasibility table (Easy → Hardest) and reformulated:

### Q2 (reformulated): How much harness work for not-unit-testable-out-of-the-box bugs?

| Option | Description | Selected |
|--------|-------------|----------|
| Max-harness: build the harness with each fix | Treat every bug as harnessable unless physically impossible. xUnit on managed, P/Invoke into UtinniCore for native, WinForms fixtures, file-content checks, VSIX manifest schema asserts, AppDomain-isolated LoadLibrary timing test for C-01. Truly-manual residual shrinks to: C-12 IDE install, C-13 cross-repo, C-03 live-injection, C-01 full-contention proof. | ✓ |
| Pragmatic harness: only the cheap ones | xUnit harnesses for Easy/Easy-Medium tier (C-04, C-05, C-06, C-08, C-11, C-14, C-15) + partial ones (C-10, C-12 schema). C-01 / C-09 / C-02 / C-03 stay manual-smoke. Middle ground. | |
| Stay with manual smoke | Managed-side gets unit tests; everything else relies on maintainer manual smoke + commit-message verification log. Lowest upfront effort; user keeps being the bottleneck. | |

**User's choice:** Max-harness: build the harness with each fix (Recommended; aligns with user's stated goal to minimize manual review).
**Notes:** Per-bug harness shapes captured in 02-CONTEXT.md D-05. The truly-manual residual is now narrow and clearly bounded.

### Q3: Where does the new test code live?

| Option | Description | Selected |
|--------|-------------|----------|
| Single project absorbs everything | `UtinniCoreDotNet.Tests` (Phase-1 project) gets System.Windows.Forms ref, P/Invokes into UtinniCore.dll, /Fixtures subdir. Small helper exe for C-01 process-isolated LoadLibrary timing lives as a sibling project. Matches Phase-1 flat-root precedent. | ✓ |
| Split managed-side vs native-bridge | New sibling projects `UtinniCore.Tests.Native` + `UtinniCore.Tests.LoaderLock`. Cleaner separation; more sln cost. | |
| Researcher decides at plan time | Defer the call to per-plan researcher. Risks ad-hoc structure. | |

**User's choice:** Single project absorbs everything (Recommended).

---

## C-01 architectural approach

### Q1: Which C-01 approach do we lock in for Phase 2?

| Option | Description | Selected |
|--------|-------------|----------|
| Export `utinni_init`; launcher calls via CreateRemoteThread | DllMain returns immediately. New exported symbol. Small native + small launcher delta. Plays nicely with the C-01 process-isolated LoadLibrary timing harness. | |
| Defer to first SWG callback (`Game::install`) | DllMain only registers the Game::install detour. No launcher change. More native restructure. Harder harness shape. | |
| Hybrid: utinni_init exported AND wired through Game::install as fallback | Both paths available. Defensive. More surface, more failure modes. | |
| Researcher picks after RVA/timing audit | Defer — researcher reviews launcher injection timing, Game::install detour mechanics, and CLR bring-up dependencies before recommending. Plan generates after research lands. Cost: blocks 02-03 plan creation until research completes. | ✓ |

**User's choice:** Researcher picks after RVA/timing audit.
**Notes:** Plan 02-03 starts with a research substep. Launcher may pull into 02-03 plan scope conditionally on researcher's pick — launcher lives in this repo so no cross-repo concern. Phase 2 success criterion #4 is a hard requirement — no defer-to-V2 off-ramp.

---

## Out-of-list scope (C-13 cross-repo, CON-O-03 delegate-pinning, open-Q resolution)

### Q1: How do we handle C-13 (lives in sister repo UtinniPlugins)?

| Option | Description | Selected |
|--------|-------------|----------|
| Cross-repo commit during Phase 2 execution; no CI bootstrap for sister repo | Executor performs path fix + .sln Build entry restoration in UtinniPlugins repo (separate clone, commit, PR). Manual verification: Kenny builds TJT Debug locally. Lowest friction. | ✓ |
| Bootstrap minimal CI in UtinniPlugins as part of Plan 02-01 | Add a build workflow to UtinniPlugins (Debug + Release matrix) AS the C-13 fix vehicle. Pulls test-harness debt forward. Cost: expands Phase 2 scope. | |
| Defer C-13 to a Phase 2.1 cross-repo sweep | Carve a Phase 2.1 'sister-repo plumbing' decimal phase. Cost: violates ROADMAP success criterion 1. | |

**User's choice:** Cross-repo commit during Phase 2 execution; no CI bootstrap for sister repo (Recommended).
**Notes:** UtinniPlugins CI bootstrap deferred to first Wave-1 plugin phase (Phase 7+) or a dedicated bridging phase.

### Q2: CON-O-03 delegate-pinning fix placement?

| Option | Description | Selected |
|--------|-------------|----------|
| Add as C-16 in Plan 02-02 single-file criticals | Treat as a 16th critical. Researcher confirms fix shape (audit every Add*Call delegate-passed-to-native site). Harness: P/Invoke test that triggers GC.Collect between delegate registration and unmanaged invocation; assert no AV. Closes both the open question and the latent bug in same commit. | ✓ |
| Researcher folds into whichever plan touches GameCallbacks first | Plan 02-01 touches C-04 (GroundSceneCallbacks); Plan 02-02 touches C-07 via GameCallbacks. Risks growing one plan's scope unpredictably. | |
| Defer to Phase 3 (R-A symmetric callbacks) | Fits naturally with R-A rework. Cost: pushes a latent crash bug to Phase 3; ROADMAP currently parks CON-O-03 in Phase 2. | |

**User's choice:** Add as C-16 in Plan 02-02 single-file criticals (Recommended).

### Q3: KB-05 (`||` vs `&&` actual code fix at game.cpp:305-308) — folds into Phase 2?

| Option | Description | Selected |
|--------|-------------|----------|
| Fold into Phase 2 alongside CON-O-01 disposition | Once researcher (or default-fallback) lands the CON-O-01 answer, the one-line operator change lands as part of the SAME task. No separate C-17 numbering. Plan 02-02 most likely host. | ✓ |
| Defer KB-05 code fix to Phase 6 | Phase 2 lands disposition only (docs/ai/ commit). Cost: latent bug stays live another 4 phases. | |
| Researcher decides whether KB-05 is in or out | Defer — researcher reads game.cpp + docs/ai/internals.md, recommends in-Phase-2 or defer based on risk. | |

**User's choice:** Fold into Phase 2 alongside CON-O-01 disposition (Recommended).

### Q4: CON-O-01, -02, -04 dispositions — project-history answers or default fallback?

| Option | Description | Selected |
|--------|-------------|----------|
| Researcher investigates each during plan substeps; default-fallback if unanswerable | For each Phase-2 plan that touches a gated bug, researcher does git archaeology + upstream `ptklatt/*` history + IDA spot-checks. If unanswerable: CON-O-01 → use `&&` per docs/ai/internals.md:231; CON-O-02 → assume AddPostDrawLoopCall IS used; CON-O-04 → audit VS 2022 build and widen if clean. | ✓ |
| I'll answer now (let me type) | User types answers inline; no researcher archaeology needed. | |
| Defer all three to Phase 6 (STAB-05 home phase) | Push dispositions to Phase 6; ship gated fixes using safe-default assumptions. | |

**User's choice:** Researcher investigates each during plan substeps; default-fallback if unanswerable (Recommended).
**Notes:** Researcher's archaeology budget is bounded (git log, in-tree comments, obvious IDA pseudo-paste markers — not deep IDA RE).

---

## Claude's Discretion

- Exact xUnit test naming for new tests (follow Phase-1 `[Method]_[Scenario]_[ExpectedOutcome]` convention).
- Per-bug task ordering WITHIN a plan (researcher/planner picks based on dependency).
- Whether C-15 jumps to first task in Plan 02-02 if Phase-1 CI is already affected by the brittle `slnDir`, else follows assessment-week order.
- Sibling helper-exe project name for the C-01 timing harness (working name `Utinni.LoaderLockHarness`).
- Whether the `UndoRedoManager` testability refactor (Phase-1 deferred work) lands as a separate task or as part of the C-07 fix task within Plan 02-02.
- Whether the C-16 delegate-pinning fix is one task (entire GameCallbacks + GroundSceneCallbacks + ObjectCallbacks audit + fix) or three tasks (one per file).
- C-07's `AllowMerge` disposition (call before `Merge` OR remove from `IUndoCommand` — researcher picks after reading UtinniPlugins/Jawa Toolbox `IUndoCommand` implementations).

## Deferred Ideas

- UtinniPlugins CI bootstrap → first Wave-1 phase (Phase 7+) or a dedicated bridging phase.
- C-12 actual install into VS 2019 + VS 2022 IDEs → stays manual; cross-IDE template install automation is Phase 6+ ergonomics.
- Phase-3 strategic reworks R-A..R-H (R-A full symmetric Add/Remove pass across all callback classes is a Phase-3 superset of C-16's GCHandle.Alloc fix).
- TD-25 empty stub files, TD-26 disabled detour-table hooks, TD-27 hardcoded font path, TD-28 TJT.ico framework-default leak → Phase 6 STAB-03.
- Open questions CON-O-05 → Phase 3 R-F; CON-O-06 → Phase 6; CON-O-07 → Phase 3 R-B; CON-O-08 → Phase 6 STAB-03; CON-O-09, -11 → Phase 4.
- SEC-01 plaintext password storage (disable autoLogin by default until DPAPI lands; full fix Phase 6+); SEC-02 launcher PAGE_EXECUTE_READWRITE → Phase 6; SEC-03 AV/EDR, SEC-04 plugin signing, MCF-03 SWG version validation, MCF-05 remote-debugging story, MCF-07 mouse+keyboard combo hotkeys → V2 or out of V1.
- Coverage tooling (coverlet, ReportGenerator) → revisit after Phase 4.
- `.clang-format` + comprehensive analyzer-rule `.editorconfig` → Phase 6 STAB-03.
- DXSDK June 2010 install on CI runner + multi-config matrix → Phase 6 bundled with CON-O-08.
- Branch protection rules on master → admin action; not a code deliverable.
