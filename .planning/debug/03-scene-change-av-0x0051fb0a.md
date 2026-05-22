---
slug: 03-scene-change-av-0x0051fb0a
status: resolved
created: 2026-05-21
resolved: 2026-05-22
trigger: scene_change_post_phase_3
goal: find_and_fix
outcome: fixed
fix_commit: 7201700
fix_branch: fix/03-scene-change-groundscene-vector-storage (fast-forward merged to master)
tdd_mode: false
specialist_dispatch: false
faulting_address: 0x0051fb0a
faulting_routine: swg::scene::ground_scene::ctor + 0x22da (likely SWG-internal asset/scene-init code adjacent to ctor symbol)
repro_rate: 100%
pre_phase_3_baseline: clean (different addresses pre-Phase-3 indicate this is a new regression)
naked_after_scene_change: baseline (NOT a regression signal -- documented Cycle 5b/6)
root_cause: per-frame heap allocation in R-H snapshot dispatch (std::vector::reserve() in hkDrawLoop/hkUpdateLoop) on the render hot path
mechanism: SWG's allocator is sensitive during the scene-cleanup-then-setup window; per-frame heap alloc-free pairs from snapshot vector reserve() fragmented the heap such that GroundScene::ctor's later allocation (or an indirect-call through a vtable read from heap memory) faulted at 0x0051fb0a
cycles_to_isolate: 11
cycles_to_fix: 1 (Cycle 12 was skipped; CODEX consult pinned mechanism directly)
---

# Debug Session: Phase 3 scene-change AV at 0x0051fb0a

## Symptoms

**Primary:** Access violation (`c0000005`) at SWG instruction pointer `0x0051fb0a`. Reporter
labels this `swg::scene::ground_scene::ctor (0x00519830)` + `0x22da` based on the lowest
known symbol -- the true function could be inside the ctor or in an unsymbolized adjacent
routine the ctor calls during asset / scene initialization.

**Trigger:** Scene CHANGE only (not initial scene load). Reliably reproduces 100% on every
transition (e.g., Naboo -> Talus, Talus -> Naboo). First scene-load on login completes
fine. The crash is in the `cleanupScene -> setupScene` transition path driven from
`hkMainLoop:305` in `UtinniCore/swg/game/game.cpp` when `loadNewScene && sceneCleaned`
triggers `swg::game::setupScene(GroundScene::ctor(...))`.

**Universality:** Same fault address on multiple maps (Talus, Naboo), with different
terrains and assets at crash time. Indicates a single broken code path, not asset-specific
data corruption.

**Pre-Phase-3 baseline:** Crashes existed pre-Phase-3 but at DIFFERENT addresses:
- `0x00b3f620` on 2026-05-20
- `0x77454984` (C++ exception) on 2026-05-19

The `0x0051fb0a` address is NEW post-Phase-3. Phase 3 either introduced this specific
defect or restructured something so a latent SWG defect now triggers here instead of
elsewhere.

**Pre-Phase-3 working evidence:** `SESSION-HANDOFF-2026-05-21.md` line 126 records
"Scene transitions (3 cycles) ✅" with master at `ed3c77a` (2026-05-21 10:20), confirming
scene change DID work immediately before Phase 3 landed. The pre-Phase-3 `utinni.log`
baseline file only exercises `/quit`, not scene change, but the handoff doc is dispositive.

**Naked-after-scene-change is BASELINE, not a regression signal:** Cycle 5b's smoke ("Naked
but in world! Scene change worked!"), Cycle 6's smoke, and Cycle 7's smoke ("no crash,
naked in world naboo") all indicate that nakedness is a known cosmetic side-effect of the
native/managed shape mismatch when Task-3a's `creature_object.cpp` is reverted (or
otherwise non-load-bearing). It is NOT a separate bug. Do not interpret "naked" as evidence
of regression in any future cycle. Only the AV-at-0x0051fb0a signal is dispositive for
this investigation.

## Repro Conditions

- Live SWG client (SWGEmu) launched with Utinni injection via the Launcher
- Log in to game
- Initiate scene change (shuttle, /quit-to-character-select then reconnect, or planet hop)
- Crash within ~5-10 frames of new scene setup (MainLoop=1329, UpTime~60s in captured dump)

**CRITICAL repro constraint (discovered 2026-05-22 ~13:00):** Scene-change requests in
this environment are issued via the TJT chat command parser. TJT's
`TheJawaToolboxPlugin` constructor registers
`tjt::TheJawaToolboxCommandParser::create` via
`utinni::CuiChatWindow::addCreateCommandParserCallback(...)`. The user's warp/teleport
commands flow through that parser and trigger the scene-change code path. **Without TJT
loaded into `bin/Release/Plugins/`, there is no way to issue a scene-change command** —
meaning any cycle that moves TJT out is unsmokeable for this bug. This invalidates Cycle 5
(TJT-disabled, master HEAD) and likely invalidated Cycle 3 (pre-Phase-3 + TJT disabled)
as well. The trigger mechanism is documented at
`C:\Users\kenne\.claude\projects\D--Code-Utinni\memory\feedback\project_scene_change_via_tjt.md`
so future cycles don't repeat this design error.

## Artifacts

- Latest dump (text + minidump):
  - `D:/SWGEmu-Client/SWGEmu/logs/SWGEmu.exe-stage.119798-20260522022332.txt`
  - `D:/SWGEmu-Client/SWGEmu/logs/SWGEmu.exe-stage.119798-20260522022332.mdmp`
- Earlier post-Phase-3 dump (Naboo, same address):
  - `D:/SWGEmu-Client/SWGEmu/logs/SWGEmu.exe-stage.119798-20260522022159.{txt,mdmp}`
- **Cycle 1 smoke dump (CR-01 reverted, still crashed at same address):**
  - `D:/SWGEmu-Client/SWGEmu/logs/SWGEmu.exe-stage.119798-20260522032423.{txt,mdmp}`
- **Cycle 2 smoke dump (R-C reverted, still crashed at same address):**
  - `D:/SWGEmu-Client/SWGEmu/logs/SWGEmu.exe-stage.119798-20260522121538.{txt,mdmp}`
- **Cycle 4 smoke dump (R-B + CR-02 combined revert, still crashed at same address):**
  - `D:/SWGEmu-Client/SWGEmu/logs/SWGEmu.exe-stage.119798-20260522124713.{txt,mdmp}`
- **Cycle 6 smoke dump (Task 3b e4b2b59 reverted alone, still crashed at same address):**
  - `D:/SWGEmu-Client/SWGEmu/logs/SWGEmu.exe-stage.119798-20260522133904.{txt,mdmp}`
- Live injection log (lines 206-219 capture the crashing sequence):
  - `D:/Code/Utinni/bin/Release/utinni.log`
- Previous-session log (working baseline -- only exercises `/quit`, not scene change):
  - `D:/Code/Utinni/bin/Release/utinni.log.previous`

## Pre-Phase-3 Known-Good HEAD

`2523228` (`docs(03): create phase plan`) -- last commit before Phase 3 (Wave 1) starts.

## Phase 3 Commit List (Bisection Candidates)

In landing order:
- `b220e36` feat(03-01): IN-05 Drain helper consolidation (Task 1)
- `2e1b61d` feat(03-01): R-A + R-H managed-side + Log.cs typo fix (Task 2)
- `5e81410` feat(03-01): R-A + R-H native game/scene/object/graphics (Task 3a)   [H2 CONFIRMED Cycle 7 -- bug is here; narrowing within file pairs started Cycle 8]
- `e4b2b59` feat(03-01): R-A + R-H native post_processing/depth/shader/imgui/cui/log (Task 3b)  [H11 REFUTED -- Cycle 6]
- `ddda9f0` feat(03-01): native R-A test bridge + NativeCallbacksHandleTests (Task 3c)  [exonerated by elimination -- test bridge only]
- `ff0b473` feat(03-02): UTINNI_PLUGIN macro + R-B fixture plugins
- `2884c2c` feat(03-02): R-B PluginManager two-phase init + HMODULE tracking + LoadLibrary error log [H8 REFUTED -- Cycle 4]
- `9337da7` feat(03-02): R-C single-source SWG WndProc RVA                       [H3 REFUTED -- Cycle 2]
- `cb3f373` feat(03-03): R-E Log [CallerMemberName] refactor
- `8aea6af` feat(03-03): R-F CppSharp header auto-discovery + CON-O-05 disposition [H4 REFUTED by elimination once H2 CONFIRMED -- Cycle 7]
- `e8fe682` feat(03-03): R-G idempotent Directory.Build.props merger
- `427f474` fix(03-review-cr-01): per-registry std::mutex                          [H1 REFUTED -- Cycle 1]
- `bc2b4ad` fix(03-review-cr-02): reject plugins missing destroyPlugin             [H8 REFUTED -- Cycle 4]
- `9626174` fix(03-review-wr-01): atomic<int> s_ioEventLogCount
- `cb6fad3` fix(03-review-wr-02): document destroyPlugin shutdown contract (doc-only)
- `9248a1a` fix(03-review-wr-03): move test-only Game accessors out of public header
- `c1681bd` fix(03-review-wr-04): skip-zero handle overflow guard
- `e17d123` fix(03-review-wr-05): atomic<bool> s_chatInputActive
- `f72721d` fix(03-review-wr-06): atomic pCuiChatWindow + pCuiConsoleHelper

## Hypothesis Pool (revised post-Cycle 8 REFUTED)

### H1 -- CR-01 per-registry std::mutex (commit 427f474) -- **REFUTED 2026-05-21 22:24**

Revert of CR-01 alone did not fix the crash. Same fault address (0x0051fb0a), same terrain
(Naboo), same repro path. The live `utinni.log` at crash time captured an INT3
(0xCC) exception via Utinni's VEH at `EIP=0x00AA1E3F` with disassembly bytes
`16 CE 87 00 [CC] E8 DB 04 00 00 6A 28 E8 04 71 FE` showing execution jumped into
0xCC-filled memory through what looks like a vtable / function-pointer indirect call.
Corrupted-pointer signature, not lock-contention. CR-01 is not the cause.

### H3 -- R-C single-source-of-truth SWG WndProc RVA (commit 9337da7) -- **REFUTED 2026-05-22 12:15**

Revert of R-C alone did not fix the crash. Same fault address `0x0051fb0a`, same Naboo
scene, identical VEH int3 byte pattern at `0x00AA1E3F`. R-C is not the regressing change.

### H7 -- "Phase 3 contains the bug" (Cycle 3 baseline) -- **UNSMOKEABLE (TJT was disabled)**

Cycle 3 built green on pre-Phase-3 source (`2523228`) with TJT moved out of `Plugins/`.
Per the 2026-05-22 ~13:00 discovery, that branch was unsmokeable for scene-change because
TJT registers the command parser that triggers scene changes. The user's earlier reply
"Didnt load TJT plugin - CODEX response: [...]" is now understood as "couldn't test
scene-change because TJT didn't load." Cycle 3 branch retained on disk but its smoke is
permanently unobtainable.

### H8 -- CODEX combined R-B + CR-02 plugin lifecycle (commits 2884c2c + bc2b4ad) -- **REFUTED 2026-05-22 12:47**

Cycle 4 reverted both R-B (two-phase init) and CR-02 (reject-missing-destroyPlugin +
FreeLibrary) on `debug/03-scene-change-av-cycle4-rb-cr02` from master HEAD `1facb01`,
TJT restored to `Plugins/`. Build green, tests 100/100. Smoke result: **CRASHED** at the
same `0x0051fb0a` with the same VEH int3 signature
`16 CE 87 00 [CC] E8 DB 04 00 00 6A 28 E8 04 71 FE` at `EIP=0x00AA1E3F`.
The plugin-lifecycle surface in isolation is not the cause -- neither R-B's `init()` pass
nor CR-02's FreeLibrary path is the regression in compound. Dump:
`D:/SWGEmu-Client/SWGEmu/logs/SWGEmu.exe-stage.119798-20260522124713.{txt,mdmp}`.

### H9 -- "Bug requires TJT loaded" vs "framework-only bug" (Cycle 5 binary split) -- **UNSMOKEABLE / DESIGN ERROR**

Cycle 5 attempted to test framework-only-with-TJT-disabled vs framework-only-with-TJT-loaded
by moving TJT out of `bin/Release/Plugins/` on master HEAD source (no reverts). Build was
green (106/106 tests). Before user could smoke, recognized that scene-change in this
environment requires TJT loaded (TJT's command parser is the entry point for warp/teleport
commands). With TJT moved out, the repro path itself is gone. The split was unsmokeable
by construction. Branch `debug/03-scene-change-av-cycle5-tjt-disabled` and the
`bin/Release/TheJawaToolbox.cycle5-disabled/` staged TJT folder retained on disk; both
abandoned as cycles.

**Information yielded by the design-error:** the bug REQUIRES TJT in the loop to repro.
This narrows the suspect pool dramatically -- anything that interacts with a
TJT-registered callback (R-A native registries, CR-01 mutex on R-A registries, R-F
P/Invoke regen of the TJT-facing surface) is high-priority; anything purely framework-side
that TJT doesn't touch is low-priority. Re-elevates R-A native (commit `e4b2b59`
specifically, where `CuiChatWindow::addCreateCommandParserCallback` lives) to PRIME
SUSPECT.

### H10 -- "R-A native combined (3 commits) is the regression" -- **CONFIRMED 2026-05-22**

Combined revert of all three R-A native commits via file-level checkout from `2523228`:
- `5e81410` (Task 3a): game/scene/object/graphics registry storage swap
- `e4b2b59` (Task 3b): post_processing/depth/shader/imgui/cui/log registry storage swap (CONTAINS cui_chat_window where TJT registers)
- `ddda9f0` (Task 3c): native R-A test bridge + NativeCallbacksHandleTests

User smoke result on Cycle 5b: **NO CRASH on scene change.** User report verbatim:
"Naked, but in world! Scene change worked!" Interpretation:
- **"Scene change worked"** is the dispositive signal: the AV at `0x0051fb0a` IS in R-A
  native. With all 3 R-A native commits reverted to pre-Phase-3 shape, the scene-change
  code path completes cleanly. **R-A native is THE regression cluster.**
- **"Naked"** is a known/acceptable side-effect documented in frontmatter.

### H11 -- "Task 3b (e4b2b59) alone is the regression" -- **REFUTED 2026-05-22 ~13:39**

Cycle 6 reverted only the 14 Task-3b files from pre-Phase-3 `2523228` on
`debug/03-scene-change-av-cycle6-revert-e4b2b59` from master HEAD `1facb01`. Task 3a
(`5e81410`) and Task 3c (`ddda9f0`) stayed at master HEAD. Tests 106/106 pass; TJT loaded.
User smoke result: **CRASHED at `0x0051fb0a` with identical VEH int3 byte pattern**.
Task 3b is not the regressing surface. By elimination, the bug must be in `5e81410` (Task 3a).

### H2 -- R-A native game/scene/object/graphics storage swap (commit 5e81410) -- **CONFIRMED 2026-05-22 (Cycle 7 smoke)**

Cycle 7 reverted only Task 3a's 8 files on master HEAD + tied collateral. Task 3b and Task
3c stayed at master HEAD. User smoke result on Cycle 7: **NO CRASH.** User report verbatim:
"no crash, naked in world naboo".
- **"no crash"** is the dispositive signal: H2 CONFIRMED. **The regressing commit is
  `5e81410` (Task 3a).** Scene change completes cleanly when only Task 3a is reverted.
- **"naked in world naboo"** is the baseline cosmetic side-effect documented in frontmatter
  (creature_object.cpp reverted to pre-Phase-3 while managed-side R-A bridges remain at
  master HEAD). NOT a regression signal.

The 4 Task-3a file pairs touched by `5e81410` are now the search space for Cycle 8+:
1. **`game/game.{cpp,h}`** — Game's setSceneCallbacks, cleanUpSceneCallbacks,
   installCallbacks, preMainLoopCallbacks, mainLoopCallbacks (5 registries). Most direct
   path to the crashing setupScene/cleanupScene transition. **ELIMINATED Cycle 8 -- H12 REFUTED.**
2. **`object/creature_object.{cpp,h}`** — `onTarget` callback (1 registry). CODEX
   flagged this as lifetime-risk during peer review (single-registry storage swap).
3. **`scene/ground_scene.{cpp,h}`** — `cameraChange`, `update`, `preDraw`, `postDraw`
   (4 per-frame registries). Less likely to interact specifically with scene CHANGE
   transition but on the per-frame hot path.
4. **`graphics/graphics.{cpp,h}`** — 10 registries (hkBeginScene/hkEndScene/hkPresent/
   hkUpdate). Largest single-file rewrite within Task 3a. Could be affected by scene-change
   DEVICELOST/Reset interactions.

### H12 -- "game.{cpp,h} alone is the regressing pair within 5e81410" -- **REFUTED 2026-05-22 (Cycle 8 smoke)**

Cycle 8 reverted only `game.{cpp,h}` from `2523228` on
`debug/03-scene-change-av-cycle8-revert-game` from master HEAD `1facb01`; the other 3
Task-3a pairs (`creature_object`, `ground_scene`, `graphics`) stayed at master HEAD.
Tests 99/99 pass; TJT loaded.

User smoke result: **CRASHED on naboo scene load.** Bug is NOT isolated to game.{cpp,h}
alone. Surprising given that log evidence ("firing 1 setSceneCallbacks" emits from
`game.cpp::hkSetScene` immediately before every observed crash) — but conclusive. The
crashing surface is somewhere in the remaining 3 Task-3a pairs: creature_object (1
registry), ground_scene (4 registries), or graphics (10 registries).

### H13 -- "creature_object.{cpp,h} OR ground_scene.{cpp,h} (combined revert) is the regression within 5e81410" -- **ACTIVE (Cycle 9)**

Cycle 9 binary-split: revert `creature_object.{cpp,h}` + `ground_scene.{cpp,h}` combined
(5 registries: onTarget + cameraChange/update/preDraw/postDraw) on a fresh branch from
master HEAD `1facb01`. The remaining Task-3a pair (`graphics.{cpp,h}` with 10 D3D9 registries)
stays at master HEAD. Game.{cpp,h} also stays at master HEAD (Cycle 8 proved it's clean
when in isolation).

Rationale: with game.{cpp,h} ruled out (Cycle 8) and graphics.{cpp,h} being the largest
single-file rewrite (10 registries vs 5 in the combined creature_object+ground_scene),
binary-splitting maximizes information yield — either outcome cuts the 3-file search space
in half.

If H13 CONFIRMED (no crash) -> bug is in creature_object OR ground_scene. Cycle 10 narrows
to one (most likely creature_object first, per CODEX lifetime-risk flag).
If H13 REFUTED (still crashes) -> bug is in graphics.{cpp,h} alone (10 D3D9 registries).
Cycle 10 verifies by reverting only graphics.{cpp,h}; if that single revert fixes the
crash, we have isolated to a single file. Then CODEX consult for fix design.
If crashed differently -> composite/cascading bug; capture new dump.

### H4 -- R-F CppSharp regeneration (commit 8aea6af) -- **REFUTED by elimination (Cycle 7 confirmed H2)**

H2 CONFIRMED means the regressing commit is `5e81410`. R-F is not the cause.

### H5 -- R-B PluginManager two-phase init standalone -- **SUBSUMED INTO H8 (refuted)**

### H6 -- WR-05/WR-06 atomic statics (commits e17d123, f72721d) -- unchanged (low priority)

## Bisection Plan (revised post-Cycle 8 REFUTED)

**Cycle 1 -- H1 (CR-01):** REFUTED.

**Cycle 2 -- H3 (R-C WndProc P/Invoke):** REFUTED.

**Cycle 3 -- H7 (Phase 3 baseline):** UNSMOKEABLE (TJT was disabled, no way to issue
scene-change command).

**Cycle 4 -- H8 (CODEX combined R-B + CR-02):** REFUTED.

**Cycle 5 -- H9 (TJT-disabled binary split):** UNSMOKEABLE (design error -- moving TJT out
removed the repro path).

**Cycle 5b -- H10 (R-A native combined revert):** **CONFIRMED.** All 3 R-A native commits
reverted, scene change worked. R-A native is the regression cluster.

**Cycle 6 -- H11 (Task 3b `e4b2b59` alone):** **REFUTED.** Task-3b files reverted alone,
scene change still crashed at `0x0051fb0a`. e4b2b59 is not the regressing commit.

**Cycle 7 -- H2 (Task 3a `5e81410` alone):** **CONFIRMED.** Task-3a's 8 files reverted
alone, scene change worked (user: "no crash, naked in world naboo"). 5e81410 IS the
regressing commit.

**Cycle 8 -- H12 (game.{cpp,h} pair alone within 5e81410):** **REFUTED.** game.{cpp,h}
reverted alone, scene change still crashed. Bug is in one of the remaining 3 Task-3a pairs.

**Cycle 9 -- H13 (creature_object.{cpp,h} + ground_scene.{cpp,h} combined within 5e81410):**
ACTIVE. Revert both pairs from `2523228` on `debug/03-scene-change-av-cycle9-creature-groundscene`
from master HEAD `1facb01`. game.{cpp,h} and graphics.{cpp,h} stay at master HEAD. Task 3b
at master HEAD. R-A managed-side at master HEAD. TJT loaded. No collateral handling needed
this cycle (unlike Cycles 7/8 which had WR-03/test_exports/NativeCallbacksHandleTests tied
to game.h).

**Cycle 10+ candidates (post-Cycle-9 outcome):**
- If Cycle 9 NO CRASH -> H13 CONFIRMED; bug is in creature_object OR ground_scene.
  Cycle 10 reverts only creature_object.{cpp,h} (CODEX lifetime-risk flag) on a fresh
  branch. If that fixes the crash, creature_object is the offender; if still crashes,
  Cycle 11 reverts only ground_scene.{cpp,h}. Either way, isolate to one file then
  CODEX consult for fix design.
- If Cycle 9 CRASHED at 0x0051fb0a -> H13 REFUTED; bug is in graphics.{cpp,h} alone.
  Cycle 10 reverts only graphics.{cpp,h} to verify. Single-file isolation, then CODEX
  consult for fix design.
- If crashed differently -> composite; capture new dump and compare.

## Cycle Log

### Cycle 1 -- H1 (CR-01 mutex) -- REFUTED

- 2026-05-21 21:43 -- Created branch `debug/03-scene-change-av-cycle1` from master HEAD (1facb01).
- 2026-05-21 21:43 -- `git revert --no-edit 427f474` succeeded. Auto-merged across 13 files.
  Revert commit: `5f462d9`. Net diff: 13 files / -286 +632 inverted.
- 2026-05-21 21:44 -- Rebuild `UtinniCore.dll` via MSBuild Release x86. Green.
- 2026-05-21 21:44 -- Rebuild `UtinniCoreDotNet.dll`. Green (warnings only).
- 2026-05-21 21:44 -- Both DLLs fresh in `bin/Release/`. Unit tests: 105 / 105 pass.
- 2026-05-21 22:24 -- User smoke result: **CRASHED**. Same fault address, vtable-corruption
  signature incompatible with a locking bug. H1 REFUTED.

### Cycle 2 -- H3 (R-C WndProc P/Invoke) -- REFUTED

- 2026-05-21 22:28 -- Created branch `debug/03-scene-change-av-cycle2` from master HEAD.
- 2026-05-21 22:28 -- `git revert --no-edit --no-commit 9337da7`. Resolved auto-gen conflict.
  Revert commit `a0cfb1b`, 5 files / -205 +2.
- 2026-05-21 22:30 -- Both DLLs built green; CppSharp regen dropped the orphan
  `getSwgWndProc` / `GetSwgWndProc` binding. Tests 102 passed / 2 failed (expected
  revert-related sentinel + flaky timing harness).
- 2026-05-22 12:15 -- User smoke result: **CRASHED**. Identical VEH int3 byte pattern. H3 REFUTED.

### Cycle 3 -- H7 (Phase 3 baseline test) -- UNSMOKEABLE (TJT was disabled)

- 2026-05-22 07:20 -- Created branch `debug/03-scene-change-av-cycle3-baseline` from
  pre-Phase-3 commit `2523228`. TJT compatibility mitigation: moved
  `bin/Release/Plugins/TheJawaToolbox` -> `bin/Release/TheJawaToolbox.plugin.disabled`.
- 2026-05-22 07:22 -- Both DLLs built green from pre-Phase-3 headers.
- 2026-05-22 07:30 -- PIVOT decision: CODEX peer-review raised H8 as more specific; skip
  the baseline smoke and run CODEX's combined revert as Cycle 4 directly.
- 2026-05-22 ~13:00 -- **Post-hoc reclassification: UNSMOKEABLE.** With TJT moved out,
  there is no way to issue a scene-change command (TJT's command parser is the trigger).
  Even if Cycle 3 had been smoked, the result would have been "couldn't repro" not "bug
  absent."

### Cycle 4 -- H8 (CODEX combined R-B + CR-02 revert) -- REFUTED

- 2026-05-22 07:31 -- Created branch `debug/03-scene-change-av-cycle4-rb-cr02` from master
  HEAD `1facb01`.
- 2026-05-22 07:31 -- `git revert --no-commit bc2b4ad` (CR-02). Then `git revert --no-commit 2884c2c` (R-B).
  Combined revert commit `fcdb936`, 6 files / -963 +39.
- 2026-05-22 07:33 -- TJT restored to `bin/Release/Plugins/TheJawaToolbox/`.
- 2026-05-22 07:39 -- Both DLLs built green. UtinniCore.dll 803,328 bytes (Δ -5,632 vs
  Cycle 2). Tests 100/100 (lost the 523-line PluginManagerLifecycleTests.cs with R-B).
- 2026-05-22 12:47 -- User smoke result: **CRASHED**. Same fault address, same VEH int3
  bytes, same SWG asset/template machinery at fault. H8 REFUTED.

### Cycle 5 -- H9 (binary split: TJT-disabled vs TJT-loaded) -- UNSMOKEABLE (design error)

- 2026-05-22 12:55 -- Created branch `debug/03-scene-change-av-cycle5-tjt-disabled` from
  master HEAD `1facb01`. NO source reverts. TJT moved OUT of `Plugins/` to
  `bin/Release/TheJawaToolbox.cycle5-disabled/`. Both DLLs rebuilt fresh from master HEAD.
  Unit tests: 106/106 pass.
- 2026-05-22 ~13:00 -- **Design error caught before user smoke.** User noted "I cant
  request a scene change if plugin not loaded." Cycle 5 abandoned; TJT restored.

### Cycle 5b -- H10 (R-A native combined revert) -- CONFIRMED

- 2026-05-22 ~13:05 -- Created branch `debug/03-scene-change-av-cycle5b-ra-native` from
  master HEAD `1facb01`. File-level checkout of 16 native R-A files from `2523228` + delete
  `game_test_internal.h` + delete `NativeCallbacksHandleTests.cs` + revert
  `UtinniCore.vcxproj` + revert `ExportResolutionTests.cs`. R-A managed-side at master HEAD.
- 2026-05-22 08:09 -- Both DLLs built green. UtinniCore.dll 780,288 bytes; UtinniCoreDotNet.dll
  1,198,592 bytes. Tests 99/99 pass.
- 2026-05-22 (smoke) -- **USER SMOKE RESULT: NO CRASH.** "Naked, but in world! Scene change
  worked!" H10 CONFIRMED. R-A native is the regression cluster.

### Cycle 6 -- H11 (Task 3b `e4b2b59` alone via file-level checkout) -- REFUTED

- 2026-05-22 08:25 -- Branch `debug/03-scene-change-av-cycle6-revert-e4b2b59` from master
  HEAD `1facb01`. File-level checkout of 14 Task-3b files from `2523228`. Task 3a + Task 3c
  stay at master HEAD. Commit `7aa33aa`. UtinniCore.dll 798,208 bytes; UtinniCoreDotNet.dll
  1,205,248 bytes. Tests 106/106 pass. TJT verified.
- 2026-05-22 ~13:39 -- **USER SMOKE RESULT: CRASHED at `0x0051fb0a`.** Identical VEH int3
  byte pattern. H11 REFUTED.

### Cycle 7 -- H2 (Task 3a `5e81410` alone via file-level checkout) -- CONFIRMED

- 2026-05-22 08:42 -- Branch `debug/03-scene-change-av-cycle7-revert-5e81410` from master
  HEAD `1facb01`. File-level checkout of 8 Task-3a source files from `2523228` (game,
  ground_scene, creature_object, graphics + matching headers) + tied collateral
  (test_exports.cpp, UtinniCore.vcxproj, ExportResolutionTests.cs) + deletes
  (game_test_internal.h, NativeCallbacksHandleTests.cs). Commit `7adc1c6`. Task 3b stays
  at master HEAD; Task 3c effectively reverted by collateral. R-A managed-side at master
  HEAD; CppSharp regen drops Task-3a Subscribe* bindings.
- 2026-05-22 08:43 -- UtinniCore.dll 795,136 bytes; UtinniCoreDotNet.dll 1,202,688 bytes.
  Tests 99/99 pass. TJT verified in `bin/Release/Plugins/TheJawaToolbox/`.
- 2026-05-22 (smoke) -- **USER SMOKE RESULT: NO CRASH.** User report verbatim: "no crash,
  naked in world naboo". "no crash" is the dispositive signal: H2 CONFIRMED. The regressing
  commit is `5e81410` (Task 3a). "naked in world naboo" is the documented baseline cosmetic
  side-effect (creature_object.cpp reverted while managed-side R-A bridges remain at master
  HEAD). NOT a regression signal.

### Cycle 8 -- H12 (game.{cpp,h} pair alone within 5e81410) -- REFUTED

- 2026-05-22 09:00 -- Checked out master, branched `debug/03-scene-change-av-cycle8-revert-game`
  from master HEAD `1facb01` (working tree clean post-Cycle-7).
- 2026-05-22 09:00 -- **File-level checkout of 2 game files from `2523228`** (same
  conflict-free pattern as Cycles 5b/6/7 but scoped to just the game.cpp/.h pair):
  - `UtinniCore/swg/game/game.{cpp,h}` (2)
- 2026-05-22 09:00 -- **Tied collateral also reverted from `2523228`** (identical
  collateral set to Cycle 7 because game.h symbols disappear):
  - `UtinniCore/test_exports.cpp` (drops Task-3a Subscribe* P/Invoke bridge + R-B
    pluginManager test exports + WR-03 Game accessor moves)
  - `UtinniCore/UtinniCore.vcxproj` (drops the `game_test_internal.h` include line)
  - `UtinniCoreDotNet.Tests/ExportResolutionTests.cs` (ExpectedExportCount = 13)
  Files deleted:
  - `UtinniCore/swg/game/game_test_internal.h` (WR-03 header; symbols don't exist post-revert)
  - `UtinniCoreDotNet.Tests/NativeCallbacksHandleTests.cs` (Task-3c P/Invokes reference
    reverted game.cpp symbols)
  Commit `bb38290`, 7 files / -798 +39.
- 2026-05-22 09:05 -- Both DLLs fresh. UtinniCore.dll 804,864 bytes;
  UtinniCoreDotNet.dll 1,207,808 bytes. Tests 99/99 pass after staging fresh UtinniCore.dll
  to test bin. TJT verified.
- 2026-05-22 (smoke) -- **USER SMOKE RESULT: CRASHED on naboo scene load.** H12 REFUTED.
  Bug is NOT isolated to game.{cpp,h} alone — surprising given log evidence (the 1
  setSceneCallback fires from game.cpp immediately before every crash), but conclusive.
  Bug must be in one of the remaining 3 Task-3a pairs: creature_object (1 reg),
  ground_scene (4 reg), or graphics (10 reg).

### Cycle 9 -- H13 (creature_object.{cpp,h} + ground_scene.{cpp,h} combined within 5e81410) -- AWAITING USER SMOKE

- 2026-05-22 09:14 -- Discarded the CppSharp working-tree regen left on Cycle 8 branch
  (`git restore UtinniCoreDotNet/Generated/UtinniCore.cs`). Tree clean.
- 2026-05-22 09:14 -- `git checkout master && git checkout -b debug/03-scene-change-av-cycle9-creature-groundscene 1facb01`.
- 2026-05-22 09:14 -- **File-level checkout of 4 source files from `2523228`**
  (binary-split: combined creature_object + ground_scene revert; graphics stays at master HEAD):
  - `UtinniCore/swg/object/creature_object.{cpp,h}` (1 registry: onTarget)
  - `UtinniCore/swg/scene/ground_scene.{cpp,h}` (4 registries: cameraChange/update/preDraw/postDraw)
- 2026-05-22 09:14 -- **No collateral handling this cycle.** Unlike Cycles 7/8 (which had
  WR-03 `game_test_internal.h` + `test_exports.cpp` Game accessor moves + Task-3c
  `NativeCallbacksHandleTests.cs` all tied to game.{cpp,h}), the WR-03 test bridge was
  scoped to Game only. creature_object and ground_scene have no WR-03 / test_exports /
  unit-test collateral. test_exports.cpp, UtinniCore.vcxproj, ExportResolutionTests.cs,
  game_test_internal.h, and NativeCallbacksHandleTests.cs ALL STAY AT MASTER HEAD this
  cycle (Cycle 8 commit body retained NativeCallbacksHandleTests.cs because game.cpp's
  Game::Subscribe* surface is gone there; in Cycle 9 game.cpp stays at master HEAD so
  Game::Subscribe* still exists and the test compiles).
  Commit `ef974c3`, 4 files / -240 +30.
- 2026-05-22 09:14 -- Other 2 Task-3a pairs verified UNCHANGED vs master HEAD:
  - `UtinniCore/swg/game/game.{cpp,h}` (5 registries) -- stays at master HEAD
  - `UtinniCore/swg/graphics/graphics.{cpp,h}` (10 registries) -- stays at master HEAD
- 2026-05-22 09:14 -- Task 3b (`e4b2b59`) and other Phase 3 commits stay at master HEAD.
- 2026-05-22 09:14 -- Trade-off (same as Cycles 5b/6/7/8): file-level checkout wipes CR-01
  mutex + WR-04 skip-zero from creature_object.{cpp,h} and ground_scene.{cpp,h}. Review
  fixes affecting OTHER files (game.{cpp,h}, graphics.{cpp,h}, and Task-3b files) remain
  at master HEAD. Hardening-only losses on the 4 reverted files.
- 2026-05-22 09:14 -- R-A managed-side (Task 2 `2e1b61d`) at master HEAD; CppSharp regen
  against the reverted creature_object + ground_scene correctly drops Subscribe* bindings
  for the 5 reverted-pair registries (SubscribeOnTarget + SubscribeCameraChange +
  Subscribe*UpdateLoop/PreDrawLoop/PostDrawLoop) while keeping all other Subscribe*
  bindings (5 game.cpp + 10 graphics.cpp + 8 Task-3b). Managed-side bridges through the
  legacy `add*Callback` exports that the reverted pre-Phase-3 creature_object/ground_scene
  still expose (`addOnTargetCallback`, `addCameraChangeCallback`, `addUpdateLoopCallback`,
  `addPreDrawLoopCallback`, `addPostDrawLoopCallback`). The managed
  `GroundSceneCallbacks.SubscribeCameraChange` and `ObjectCallbacks.SubscribeOnTarget` are
  pure managed-side registry helpers (do not P/Invoke), so the reflection-based test
  `CallbacksSubscribeUnsubscribeTests.cs:145-146` still compiles and exercises the managed
  paths.
  CppSharp regen diff: 422 lines changed (-271/+151) in Generated/UtinniCore.cs.
- 2026-05-22 09:15 -- Rebuild UtinniCore.dll via solution-level MSBuild target
  (`-t:UtinniCore:Rebuild -p:Configuration=Release -p:Platform=x86`). Green.
- 2026-05-22 09:16 -- Rebuild UtinniCoreDotNet.dll via direct csproj `-t:Rebuild` with
  `-p:Configuration=Release -p:Platform=x86`. Green (CS0108 inheritance warnings only).
- 2026-05-22 09:16 -- Both DLLs fresh in `bin/Release/`:
  - `UtinniCore.dll` 803,840 bytes @ 09:16 (Δ -1,024 vs Cycle 8's 804,864 -- game.cpp's
    5 Subscribe* bindings RE-added but creature_object's 1 + ground_scene's 4 dropped;
    net small negative.)
  - `UtinniCoreDotNet.dll` 1,207,296 bytes @ 09:16 (Δ -512 vs Cycle 8's 1,207,808 --
    same pattern.)
- 2026-05-22 09:16 -- Unit tests: **106 passed / 0 failed / 0 skipped / 106 total** after
  manually staging fresh UtinniCore.dll + UtinniCoreDotNet.dll to test bin. Test count is
  106 (UP from 99 in Cycle 8) because `NativeCallbacksHandleTests.cs` is RETAINED this
  cycle (game.cpp at master HEAD still exposes Game::Subscribe* surface that the test
  P/Invokes consume). All Subscribe* reflection assertions in
  `CallbacksSubscribeUnsubscribeTests.cs` pass — the reverted-pair managed-side
  Subscribe/Unsubscribe methods are pure managed and remain functional.
- 2026-05-22 09:16 -- TJT verified in `bin/Release/Plugins/TheJawaToolbox/` (8 files,
  unchanged since Cycle 5b/6/7/8, last built 2026-05-21 19:10).
- 2026-05-22 09:16 -- **AWAITING USER SMOKE** on branch
  `debug/03-scene-change-av-cycle9-creature-groundscene` (commit `ef974c3`). Expected outcomes:
  - `no crash` (naked expected -- creature_object reverted to pre-Phase-3 shape; naked is
    documented baseline per Cycles 5b/7) -> H13 CONFIRMED; bug is in creature_object OR
    ground_scene. Cycle 10 narrows by reverting only creature_object.{cpp,h} first
    (CODEX lifetime-risk flag); Cycle 11 if needed for ground_scene.{cpp,h} only.
  - `crashed at 0x0051fb0a` -> H13 REFUTED; bug is in graphics.{cpp,h} alone (10 D3D9
    registries). Cycle 10 reverts only graphics.{cpp,h} to verify single-file isolation,
    then CODEX consult for fix design.
  - `crashed differently` -> composite/cascading bug; capture new dump and compare.

## Evidence

- timestamp: 2026-05-21 (session opened)
  source: handoff context
  finding: AV at 0x0051fb0a, 100% repro on scene change, NEW post-Phase-3 address
- timestamp: 2026-05-21 (session opened)
  source: handoff context
  finding: first scene load OK, only scene CHANGE crashes -> cleanup->setup transition is regressed
- timestamp: 2026-05-21 21:42
  source: `utinni.log` lines 206-219 analysis
  finding: Crash sequence: hkMainLoop calls `Game::cleanupScene` (line 206); SWG-internal
  cleanup invokes `setupScene(null)` which routes through hkSetScene (line 207) with NULL;
  hkMainLoop then triggers second setupScene path with the ctor result (line 213);
  hkSetScene fires successfully (lines 214-218) with non-null scene 0x31F71410 and 1
  setSceneCallback; FATAL fires immediately after EXIT but before hkMainLoop's
  "setupScene returned" log line -- crash is on the return path / post-callback cleanup.
- timestamp: 2026-05-21 21:42
  source: dump file comparison
  finding: Both Talus and Naboo dumps show IDENTICAL player coordinates
  (3332.79, 5.00, -4772.09). Player is still at OLD scene position -> crash happens before
  the new scene's player state is settled.
- timestamp: 2026-05-21 21:42
  source: code analysis of `Game::cleanupScene`
  finding: `Game::cleanupScene` calls `swg::game::cleanupScene` directly (the trampoline,
  not the detour). `hkCleanupScene` is BYPASSED -- `cleanUpSceneCallbacks` are never fired
  during scene change. This is pre-existing behavior, unchanged by Phase 3.
- timestamp: 2026-05-21 22:24
  source: Cycle 1 smoke `utinni.log` + `SWGEmu.exe-stage.119798-20260522032423.txt`
  finding: H1 (CR-01 mutex) REFUTED. Crash recurs at `0x0051fb0a` with CR-01 reverted +
  both DLLs rebuilt clean. VEH caught an INT3 at `EIP=0x00AA1E3F` (SWG .text neighborhood
  between WndProc and PersistentCrcString_ctor) with bytes
  `16 CE 87 00 [CC] E8 DB 04 00 00 6A 28 E8 04 71 FE` -- vtable / fn-pointer indirect jump
  into 0xCC-filled (uninitialized/freed) memory. Corrupted-pointer signature, not
  lock-contention.
- timestamp: 2026-05-21 22:30
  source: Cycle 2 build verification
  finding: After reverting R-C (commit `9337da7`), CppSharp's build-time regen of
  `UtinniCoreDotNet/Generated/UtinniCore.cs` correctly dropped the orphan
  `?getSwgWndProc@Client@utinni@@SAPAXXZ` binding. Both UtinniCore.dll and
  UtinniCoreDotNet.dll built green. Tests effectively 104/104.
- timestamp: 2026-05-22 12:15
  source: Cycle 2 smoke `SWGEmu.exe-stage.119798-20260522121538.{txt,mdmp}` + `utinni.log` tail
  finding: H3 (R-C WndProc P/Invoke) REFUTED. Crash recurs at `0x0051fb0a` with R-C
  reverted + both DLLs rebuilt clean. Identical VEH int3 byte pattern at `0x00AA1E3F`.
  Player ObjectTemplate at crash was `shared_frn_all_bed_sm_s1.iff`. Both top single-commit
  suspects (CR-01, R-C) now refuted; one-commit-at-a-time strategy has low information
  yield against the 12 remaining Phase 3 commits.
- timestamp: 2026-05-22 12:18
  source: handoff doc `SESSION-HANDOFF-2026-05-21.md` line 126
  finding: Pre-Phase-3 working state confirmed -- "Scene transitions (3 cycles) ✅" with
  master at `ed3c77a` (2026-05-21 10:20), right before Phase 3 started landing.
- timestamp: 2026-05-22 12:18
  source: git log diff `2523228..HEAD -- 'UtinniCore/swg/misc/**'`
  finding: ZERO Phase 3 commits modified the 0x00AA-region neighborhood. The int3 byte
  pattern at `0x00AA1E3F` belongs to SWG's own .text; no Phase 3 changes touch SWG's
  WndProc / PersistentCrcString / calculateCrc / io_win neighborhood except R-C (already
  refuted as a single-commit cause). The corruption affecting that SWG code must come
  from somewhere ELSE in Phase 3 that writes data the SWG code consumes.
- timestamp: 2026-05-22 12:18
  source: dump comparison across 4 post-Phase-3 crashes
  finding: Asset templates at crash time differ wildly per crash; shared characteristic is
  "asset loader is running". Crash is in SWG's template/CRC/asset machinery itself.
  Environment-side (SWGEmu.exe mtime 2026-05-17 12:17, D3D driver, Windows version) is
  unchanged across all 4 dumps. Bug is in Utinni's code, not SWG or environment.
- timestamp: 2026-05-22 07:22
  source: Cycle 3 build verification
  finding: Pre-Phase-3 baseline (`2523228`) builds green. UtinniCore.dll = 775,168 bytes.
  Cycle 3 ready for user smoke; outcome will dispositively answer "does the bug live in
  Phase 3?" (Later reclassified as unsmokeable -- TJT was disabled and no chat command
  could trigger a scene change.)
- timestamp: 2026-05-22 07:40
  source: Cycle 4 build verification
  finding: Combined revert of R-B (`2884c2c`) + CR-02 (`bc2b4ad`) on branch
  `debug/03-scene-change-av-cycle4-rb-cr02` from master HEAD `1facb01` (revert commit
  `fcdb936`, 6 files / -963 +39). plugin_manager.cpp confirmed back to pre-Phase-3 shape.
  Both DLLs built green. Tests: 100/100 pass. TJT restored. Cycle 4 build ready for user smoke.
- timestamp: 2026-05-22 12:47
  source: Cycle 4 smoke `SWGEmu.exe-stage.119798-20260522124713.{txt,mdmp}` + `utinni.log`
  finding: H8 (CODEX combined R-B + CR-02 plugin lifecycle revert) REFUTED. Crash recurs
  at `0x0051fb0a` with both lifecycle commits reverted + both DLLs rebuilt clean. Identical
  VEH int3 byte pattern. Four hypotheses now refuted across three cycles; per-cycle
  information yield is too low.
- timestamp: 2026-05-22 12:55
  source: Cycle 5 build verification
  finding: H9 binary-split test ready. Branch `debug/03-scene-change-av-cycle5-tjt-disabled`
  from master HEAD, NO source reverts. TJT moved OUT of `bin/Release/Plugins/`. Both DLLs
  rebuilt fresh from master HEAD: UtinniCore.dll 808,960 bytes @ 07:54;
  UtinniCoreDotNet.dll 1,209,344 bytes @ 07:55. Unit tests: 106/106 pass.
- timestamp: 2026-05-22 ~13:00
  source: user clarification + design-error reflection
  finding: **CRITICAL.** TJT's `TheJawaToolboxPlugin` constructor registers
  `tjt::TheJawaToolboxCommandParser::create` via
  `utinni::CuiChatWindow::addCreateCommandParserCallback(...)`. Scene-change commands
  (warp/teleport) flow through that parser. **With TJT moved out of `Plugins/`, no chat
  command can issue a scene change** -- the repro path itself is gone. Cycle 5 is
  unsmokeable by construction. Likely Cycle 3 was unsmokeable for the same reason. The
  user's earlier "Didnt load TJT plugin" comment retroactively makes sense as "couldn't
  test scene-change because TJT didn't load." Memory saved at
  `~/.claude/projects/D--Code-Utinni/memory/feedback/project_scene_change_via_tjt.md`.
  Information yield: bug REQUIRES TJT to repro. Re-elevates R-A native (commit `e4b2b59`
  containing cui_chat_window's `createCommandParserCallbacks` registry where TJT lands) to
  PRIME SUSPECT.
- timestamp: 2026-05-22 08:09
  source: Cycle 5b build verification
  finding: H10 R-A native combined revert ready. Branch `debug/03-scene-change-av-cycle5b-ra-native`
  from master HEAD `1facb01`. File-level checkout of 16 native R-A files from pre-Phase-3
  `2523228`. R-A managed-side at master HEAD. Both DLLs fresh; tests 99/99 pass. TJT restored.
- timestamp: 2026-05-22 (smoke)
  source: user smoke result on Cycle 5b
  finding: **H10 CONFIRMED.** User report verbatim: "Naked, but in world! Scene change
  worked!" R-A native is the regression cluster. Next cycle splits R-A native.
- timestamp: 2026-05-22 08:26
  source: Cycle 6 build verification
  finding: H11 (Task 3b `e4b2b59` alone) revert ready. Branch
  `debug/03-scene-change-av-cycle6-revert-e4b2b59`. UtinniCore.dll 798,208 bytes;
  UtinniCoreDotNet.dll 1,205,248 bytes. Tests 106/106 pass. TJT verified.
- timestamp: 2026-05-22 ~13:39
  source: Cycle 6 smoke `SWGEmu.exe-stage.119798-20260522133904.{txt,mdmp}`
  finding: **H11 REFUTED.** Cycle 6 crashed at `0x0051fb0a` with identical VEH int3 byte
  pattern. Task 3b is NOT the regressing surface. By elimination, bug must be in 5e81410
  (Task 3a). H2 elevated to ACTIVE for Cycle 7.
- timestamp: 2026-05-22 08:43
  source: Cycle 7 build verification
  finding: H2 (Task 3a `5e81410` alone) revert ready. Branch
  `debug/03-scene-change-av-cycle7-revert-5e81410`. UtinniCore.dll 795,136 bytes;
  UtinniCoreDotNet.dll 1,202,688 bytes. Tests 99/99 pass. TJT verified.
- timestamp: 2026-05-22 (Cycle 7 smoke)
  source: user smoke result on Cycle 7
  finding: **H2 CONFIRMED.** User report verbatim: "no crash, naked in world naboo".
  "no crash" is dispositive — scene change works when only Task 3a (`5e81410`) is reverted
  to pre-Phase-3 shape; Task 3b and Task 3c stay at master HEAD. The regressing commit IS
  `5e81410`. "naked in world naboo" is the documented baseline cosmetic side-effect, not
  a regression signal. Next cycle narrows within Task 3a's 4 file pairs; game.{cpp,h}
  picked first (Cycle 8) because the log evidence "firing 1 setSceneCallbacks" emits from
  game.cpp::hkSetScene immediately before every crash, and game.cpp holds 5 of the 20
  Task-3a registries including the scene-transition callbacks.
- timestamp: 2026-05-22 09:05
  source: Cycle 8 build verification
  finding: H12 (game.{cpp,h} pair alone within 5e81410) revert ready. Branch
  `debug/03-scene-change-av-cycle8-revert-game` from master HEAD `1facb01`. File-level
  checkout of 2 game source files from `2523228` (game.cpp, game.h) + tied collateral
  (test_exports.cpp, UtinniCore.vcxproj, ExportResolutionTests.cs) + deletes
  (game_test_internal.h, NativeCallbacksHandleTests.cs). Commit `bb38290`, 7 files /
  -798 +39. Other 3 Task-3a pairs (creature_object, ground_scene, graphics) stay at master
  HEAD; Task 3b at master HEAD; Task 3c effectively reverted by collateral. R-A managed-side
  at master HEAD; CppSharp regen drops Subscribe* bindings for the 5 game.cpp registries
  while keeping all other Subscribe* bindings (15 non-game Task-3a + 8 Task-3b).
  UtinniCore.dll 804,864 bytes @ 09:05 (Δ +9,728 vs Cycle 7's 795,136 -- non-game Task-3a
  Subscribe restored; Δ -4,096 vs Cycle 5's master-HEAD -- game.cpp Subscribe removed).
  UtinniCoreDotNet.dll 1,207,808 bytes @ 09:05 (Δ +5,120 vs Cycle 7 -- non-game Task-3a
  bindings restored). Tests 99/99 pass after staging fresh UtinniCore.dll to test bin.
  TJT verified in `bin/Release/Plugins/TheJawaToolbox/`. Cycle 8 build ready for user smoke.
- timestamp: 2026-05-22 (Cycle 8 smoke)
  source: user smoke result on Cycle 8
  finding: **H12 REFUTED.** Crashed on naboo scene load with game.{cpp,h} reverted alone.
  Bug is NOT isolated to game.{cpp,h}. Surprising given log evidence that the 1
  setSceneCallback fires from game.cpp immediately before every crash, but conclusive. The
  regressing surface is somewhere in the remaining 3 Task-3a pairs: creature_object (1
  registry), ground_scene (4 registries), or graphics (10 registries). Cycle 9 elects
  binary-split: revert creature_object + ground_scene combined (5 registries), leaving
  graphics (10 registries) at master HEAD. Either outcome cuts the 3-file search space in
  half.
- timestamp: 2026-05-22 09:16
  source: Cycle 9 build verification
  finding: H13 (creature_object + ground_scene combined within 5e81410) revert ready.
  Branch `debug/03-scene-change-av-cycle9-creature-groundscene` from master HEAD `1facb01`.
  File-level checkout of 4 source files from `2523228` (creature_object.cpp/.h,
  ground_scene.cpp/.h). Commit `ef974c3`, 4 files / -240 +30. No collateral handling needed
  this cycle: WR-03 test bridge / test_exports.cpp Game accessor moves / Task-3c
  NativeCallbacksHandleTests.cs were scoped to game.{cpp,h} only — those stay at master
  HEAD this cycle (which means NativeCallbacksHandleTests.cs is RETAINED and the test count
  is 106 rather than 99). Other 2 Task-3a pairs verified UNCHANGED vs master HEAD:
  game.{cpp,h} (5 registries) stays, graphics.{cpp,h} (10 registries) stays. Task 3b stays
  at master HEAD. R-A managed-side stays at master HEAD; CppSharp regen drops Subscribe*
  bindings for the 5 reverted-pair registries (SubscribeOnTarget + SubscribeCameraChange +
  Subscribe*UpdateLoop/PreDrawLoop/PostDrawLoop) while keeping all other Subscribe*
  bindings (5 game + 10 graphics + 8 Task-3b). Managed-side bridges via legacy
  add*Callback exports that the reverted pre-Phase-3 sources still expose. The managed
  `GroundSceneCallbacks.SubscribeCameraChange` and `ObjectCallbacks.SubscribeOnTarget`
  helpers are pure managed-side registries (no P/Invoke), so the reflection-based test
  CallbacksSubscribeUnsubscribeTests.cs:145-146 continues to compile and pass.
  CppSharp regen diff: 422 lines (-271/+151) in Generated/UtinniCore.cs.
  UtinniCore.dll 803,840 bytes @ 09:16 (Δ -1,024 vs Cycle 8 -- game.cpp Subscribe* added
  back, creature_object + ground_scene Subscribe* dropped; net small negative).
  UtinniCoreDotNet.dll 1,207,296 bytes @ 09:16 (Δ -512 vs Cycle 8 -- same pattern).
  Tests **106/106 pass** after staging fresh DLLs to test bin (UP from Cycle 8's 99
  because NativeCallbacksHandleTests.cs is retained -- game.cpp's Game::Subscribe*
  surface still exists at master HEAD). TJT verified in `bin/Release/Plugins/TheJawaToolbox/`.
  Cycle 9 build ready for user smoke.

## Cycle 10 (2026-05-22): single-file narrow — creature_object only

- Branch: `debug/03-scene-change-av-cycle10-revert-creature` (from master HEAD `91d43fc`,
  which is `1facb01` + the SESSION-HANDOFF doc — the docs commit doesn't affect the build).
- Action: `git checkout 2523228 -- UtinniCore/swg/object/creature_object.{cpp,h}`.
  Reverts only the R-A native onTarget registry (handle-based subscribe/unsubscribe + mutex
  + R-H snapshot). `addOnTargetCallback` falls back to its pre-Phase-3 `std::vector<fnptr>`
  storage with no locking and no snapshot. `ground_scene.{cpp,h}` and `graphics.{cpp,h}`
  remain at master HEAD.
- Diff: 2 files, +3 -45 lines. Both files staged but uncommitted on the branch (consistent
  with Cycles 7/8/9 mechanics — branch is the build, not a checkpoint commit).
- Build: VS 2026 MSBuild Release/x86, no errors. UtinniCore.dll 808,448 bytes @ 09:36
  (Δ +4,608 vs Cycle 9 — ground_scene's R-A registry plus all its R-H snapshot code is
  retained this cycle). UtinniCoreDotNet.dll 1,208,832 bytes @ 09:36 (Δ +1,536 vs Cycle 9).
  TJT verified at `bin/Release/Plugins/TheJawaToolbox/` (DLLs from
  UtinniPlugins@73b1856; unchanged).
- Why creature_object first (not ground_scene): CODEX's #2 ranking flagged
  `creature_object.{cpp,h}` for "lifetime risk for raw fn-ptr" — onTarget is the smallest
  single-registry surface in the 2-file remainder and the highest static-analysis prior.
  If Cycle 10 clears, root cause is the onTarget subscribe/snapshot path. If Cycle 10 still
  crashes, Cycle 11 reverts only `ground_scene.{cpp,h}` (4 registries:
  cameraChange/update/preDraw/postDraw — per-frame render path) and the bug lives there.

NOTE on "naked" expectation Cycle 10: creature_object.cpp is REVERTED, so naked-after-scene-change
IS the expected baseline (same as Cycles 5b/7/9 which also reverted creature_object). "naked
in world" combined with "no crash" is the H14a-CONFIRMED signal — DO NOT treat naked as a
regression.

## Cycle 10 result (2026-05-22 09:40): REFUTED

User smoke: **crash reproduced at `0x0051fb0a`** on Naboo scene change (MainLoop=1216,
UpTime=57s). Dump:
`D:/SWGEmu-Client/SWGEmu/logs/SWGEmu.exe-stage.119798-20260522144001.{txt,mdmp}`.

Same exact exception code (`c0000005`) and address as the post-Phase-3 baseline. Reverting
`creature_object.{cpp,h}` alone does NOT fix the regression.

Deduction (Cycle 9 was CLEAN with both `creature_object.{cpp,h}` + `ground_scene.{cpp,h}`
reverted; Cycle 10 is CRASH with only `creature_object.{cpp,h}` reverted) -> bug is in
`ground_scene.{cpp,h}` (4 registries: cameraChange/update/preDraw/postDraw).
**H14a REFUTED, H14b promoted.**

## Cycle 11 result (2026-05-22): CONFIRMED

User smoke: **no crash** on Naboo scene change. naked-after-scene-change as expected
(documented baseline). **H14b CONFIRMED**: bug is in `ground_scene.{cpp,h}` and only
`ground_scene.{cpp,h}` (necessary and sufficient).

Master HEAD with only `ground_scene.{cpp,h}` reverted to `2523228` survives scene change.
All other Phase 3 R-A code (creature_object, graphics, game.cpp, post_processing,
depth_texture, shader, cui_*, log, imgui_impl, plugin lifecycle, WndProc) is at master HEAD
and clean.

### Bisect within ground_scene (4 commits since baseline)

The `git checkout 2523228 -- ground_scene.{cpp,h}` revert collapses 4 commits. Per
`git log 2523228..master -- UtinniCore/swg/scene/ground_scene.cpp UtinniCore/swg/scene/ground_scene.h`:

| Commit | Change | Per-file impact |
|---|---|---|
| `5e81410` | R-A + R-H: vector→unordered_map storage + snapshot dispatch | +123/-16 cpp, +12 h |
| `427f474` | CR-01: per-registry std::mutex around all ops | +43/-15 cpp |
| `9626174` | WR-01: atomic<int> diag-counter (hkHandleInputEvent only, NOT on dispatch path) | +28/-11 cpp |
| `c1681bd` | WR-04: skip-zero handle guard (4 lines, only fires after 2^31 subs) | +4 cpp |

Practical suspects: `5e81410` (storage) and `427f474` (mutex). WR-01 is on the diag IO event
counter (separate code path); WR-04 only fires after 2^31 subscribes (unreachable in repro).

### Decision: CODEX consult (Path B) instead of Cycle 12 narrow

User opted to skip Cycle 12 (which would revert `ground_scene` to commit `5e81410`'s file
state — has R-A+R-H, no mutex — to discriminate between `5e81410` and `427f474`) and route
straight to CODEX with the full diff. Rationale: CODEX has access to the same project; a
focused diff review may spot the bug more efficiently than another smoke cycle, especially
since the practical suspect set is small (2 commits).

CODEX consult prompt: `.planning/debug/codex-consult-ground-scene-av.md` (self-contained,
includes bisect history, runtime hot-path analysis, weighted hypothesis ranking, and the
305-line `git diff 2523228..master` saved alongside at
`.planning/debug/codex-consult-ground-scene-diff.patch`).

## Cycle 11 (2026-05-22): single-file narrow — ground_scene only

- Branch: `debug/03-scene-change-av-cycle11-revert-groundscene` (from master HEAD `91d43fc`).
- Action: `git checkout 2523228 -- UtinniCore/swg/scene/ground_scene.{cpp,h}`. Reverts all 4
  R-A handle-based subscribe/unsubscribe pairs (preDraw, postDraw, update, cameraChange) +
  R-H snapshot dispatch + per-registry mutex. Legacy `add*` API remains. `creature_object`
  and `graphics` stay at master HEAD.
- Diff: 2 files, +27 -195 lines. ground_scene.cpp loses 210 lines, ground_scene.h loses 12.
- Build: VS 2026 MSBuild Release/x86, clean. UtinniCore.dll 805,376 @ 09:42 (Δ +1,536 vs
  Cycle 9 — creature_object's onTarget R-A code retained). UtinniCoreDotNet.dll 1,207,808
  @ 09:42 (Δ +512 vs Cycle 9).
- TJT verified at `bin/Release/Plugins/TheJawaToolbox/` (DLLs from UtinniPlugins@73b1856).

Cycle 11 isn't strictly necessary for the deduction (Cycle 9 ∩ Cycle 10 already pinpoints
ground_scene by elimination), but a confirmation cycle:
1. Rules out a composite bug (both files together required) -- if Cycle 11 crashes too,
   the bug is composite and we need a different fix design.
2. Gives us a "ground_scene-only revert" build as the fix-design baseline.
3. Eliminates the small risk that Cycle 10's smoke involved some uncaptured environmental
   difference (e.g., partial DLL load).

## Current Focus

hypothesis: H14b -- the regressing single file is
`UtinniCore/swg/scene/ground_scene.{cpp,h}` (the 4 GroundScene registries:
cameraChange/update/preDraw/postDraw -- per-frame render-path callbacks dispatched from
inside `GroundScene::ctor` / `setupScene` and from the main-loop iteration). The Phase-3
change replaced their pre-Phase-3 `std::vector<fnptr>` storage with handle-based
`unordered_map` + per-registry mutex + R-H snapshot dispatch. Likely failure modes per
CODEX's static analysis:
- Iteration-order divergence in `unordered_map` causing one of the 4 callbacks to fire
  before its peer has initialized scene-dependent state.
- Lifetime: a raw fn-ptr stored in the map points into a TJT-DLL function whose state is
  cleanup-scene-invalidated (vtable/fn-ptr address now in CC-padded freed memory -- matches
  the `[CC] E8 DB 04 00 00` byte pattern at VEH EIP=0x00AA1E3F).
- Snapshot-vs-mutate race: scene-change drives concurrent register/unregister against
  per-frame draw iteration; the per-registry mutex protects the registry but not the
  callback's own state (the callback dereferences scene-cleaned-up `GroundScene*`).

If Cycle 11 clean (no crash, naked is fine) -> H14b CONFIRMED; CODEX consult for fix design
on `ground_scene.{cpp,h}` alone. Don't ship the permanent revert -- preserve R-A surface
for the 4 registries via a narrow fix (e.g., insertion-order-preserving registry, OR
clear-on-cleanupScene reset of the registry, OR bind subscriber lifetime to a guard tied to
plugin/DLL lifetime).

If Cycle 11 crashes at `0x0051fb0a` -> H14b REFUTED, composite bug. Bug requires BOTH
files' R-A code present. Different fix design -- can't narrow further without per-callsite
instrumentation in both files. Would consider a temporary revert of both R-A native pairs
as a stopgap while CODEX consults on the underlying interaction.

next_action: **AWAITING CODEX RESPONSE on `.planning/debug/codex-consult-ground-scene-av.md`.**

When CODEX responds, expected outcomes:
- CODEX agrees the bug is in `5e81410` or `427f474` and proposes a narrow fix → apply,
  rebuild, smoke. If smoke clean → Phase 3 verification gate releases; commit + close
  `03-HUMAN-UAT.md`.
- CODEX wants Cycle 12 (revert to `5e81410`'s file state to discriminate R-A/R-H vs
  mutex) before proposing a fix → run Cycle 12; smoke; report back to CODEX for fix.
- CODEX spots a third option (a possibility I didn't enumerate) → evaluate, possibly
  run a confirmation cycle.

After fix lands and stays green for a session or two, prune the 11 debug branches
(`debug/03-scene-change-av-cycle1` through `cycle11-revert-groundscene`).

## Resolution (2026-05-22)

**Outcome:** Fixed on master at commit `7201700` (fast-forward merge from
`fix/03-scene-change-groundscene-vector-storage`). Live SWG smoke on Naboo scene
change via TJT `/warp` — no crash. Phase 3 verification flipped from
`human_needed` to `passed`; `03-HUMAN-UAT.md` test #1 closed.

**Root cause:** Per-frame heap allocation in the R-H snapshot dispatch path. The
Phase 3 R-A change (`5e81410`) converted `std::vector<fn_ptr>` storage to
`std::unordered_map<int, fn_ptr>` and added R-H snapshot dispatch that
allocated a fresh `std::vector<fn_ptr>` on the stack and called `reserve(N)`
(heap allocation) every frame inside `hkDrawLoop`, `hkUpdateLoop`, and the
postDraw branch. With only one native subscriber per registry (the managed
bridge in `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs:Initialize`),
this was 3 malloc/free pairs per frame for ~4 bytes each.

The fault address `0x0051fb0a` is inside SWG's `GroundScene::ctor` at +0x22da
(SWG-internal scene init; Utinni does NOT detour this). The exact mechanism
isn't fully proven, but consistent with: per-frame heap churn from the snapshot
allocations fragmented the CRT heap such that SWG's later ctor-time allocations
returned poisoned/freed memory, and an indirect dispatch through a vtable read
from that memory landed on 0xCC alignment padding (matches the VEH int3 byte
pattern at 0x00AA1E3F that an earlier investigation flagged).

CODEX consult (`.planning/debug/codex-consult-ground-scene-av.md`) ranked this
as the most likely mechanism on a static read of the diff -- "every hkDrawLoop /
hkUpdateLoop dispatch now touches unordered_map nodes and allocates/frees
snapshot storage on SWG's scene hot path. During scene change, that path is
running while SWG is tearing down and constructing scene/asset state. The
resulting AV signature looks like downstream corruption/invalid indirect
dispatch, not a direct null callback." Pinned `5e81410` (not `427f474` mutex)
as the suspect commit and recommended skipping the Cycle 12 mutex-vs-storage
bisect.

**Fix (commit `7201700`, scope: `ground_scene.{cpp,h}` only, +130 -76):**

- Storage: `std::unordered_map<int, fn_ptr>` (4 registries) →
  `std::vector<CallbackEntry<fn_ptr>>` where
  `CallbackEntry = { int handle; Fn func; }`. Insertion-order semantics; matches
  pre-Phase-3 iteration order on the managed bridge side.
- Subscribe: `push_back({id, func})` instead of `map[id] = func`. Same monotonic
  handle allocation with WR-04 skip-zero.
- Unsubscribe: linear `find_if` + `erase` instead of `map::erase(handle)`.
  O(N) with N ≤ 16 in practice — trivial cost.
- Dispatch: factored snapshot+iterate into an anonymous-namespace template
  helper `dispatchSnapshot(registry, mutex, invoke)`. Helper stack-allocates a
  `Fn stackSnap[16]` (kInlineCap=16, 16x headroom over current N=1) and copies
  values out under the mutex. Overflow path uses a stack `std::vector<Fn>`
  with capacity 0 by default — only heap-allocates if N>16 (won't trigger in
  practice).
- Preserved: R-A handle-based public API (no signature changes), per-registry
  `std::mutex` (CR-01), R-H snapshot semantics (Subscribe-during-dispatch
  fires on next dispatch), legacy `add*Callback` wrappers, WR-04 skip-zero,
  WR-01 atomic counter on `hkHandleInputEvent` diag.
- Removed: `<unordered_map>` include (no longer needed).
- No header (.h) change — the public R-A API surface is unchanged.

**Tests:** all 106 UtinniCoreDotNet.Tests pass (incl.
`CallbacksSnapshotIterationTests` covering subscribe-during-dispatch + R-H
semantics, `CallbacksSubscribeUnsubscribeTests` covering handle invariants,
`GroundSceneCallbacksTests`, `LegacyAddCallback_StillWorks`). Build VS 2026
MSBuild Release/x86 green.

**Scope limit:** Only `ground_scene.{cpp,h}` was migrated. The other 10 R-A
files (creature_object, graphics, post_processing, depth_texture, shader,
imgui_impl, cui_chat_window, cui_manager, log, game.cpp) keep
`std::unordered_map<int, fn_ptr>` storage for now. They were cleared by the
bisect (game.cpp by Cycle 8; creature_object by Cycle 10 alone; graphics by
Cycle 9) and aren't on the scene-change crash path. A follow-up phase will
migrate them to the same pattern for consistency and to preemptively remove
per-frame heap alloc from any other dispatch site that might be on a hot
path. Captured as a tracking item in `docs/ai/assessment.md` or ROADMAP.

**Debug branches:** 11 branches alive (`debug/03-scene-change-av-cycle1`
through `cycle11-revert-groundscene` + `cycle10-revert-creature`). Per
SESSION-HANDOFF-2026-05-22, prune them "after the fix lands and stays green
for a session or two." Keep until at least one follow-up session confirms no
regression.

root_cause: per-frame std::vector::reserve() heap allocation in R-H snapshot
dispatch (hkDrawLoop / hkUpdateLoop), interacting with SWG's allocator during
scene-cleanup-then-setup window.
fix: 7201700 — std::vector<CallbackEntry> + stack-allocated snapshot;
ground_scene.cpp only.
