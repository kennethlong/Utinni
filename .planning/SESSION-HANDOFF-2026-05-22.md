# Session Handoff: 2026-05-22

> Written mid-session to checkpoint a long Phase 3 execution + scene-change-AV bisect. Phase 3 LANDED on master (`b63fff1`) → code review found 2 Critical + 7 Warning → all fixed via gsd-code-fixer → verifier returned `human_needed` → live SWG smoke surfaced a NEW regression: AV on scene change at SWG `0x0051fb0a` / VEH int3 at `0x00AA1E3F`. Nine bisect cycles narrowed the regression to **commit `5e81410` (R-A native Task 3a)**, and within that to **either `creature_object.{cpp,h}` or `ground_scene.{cpp,h}`**. Next action: Cycle 10 reverts only `creature_object.{cpp,h}` first.

Master at `1facb01`. Active investigation branch: `debug/03-scene-change-av-cycle9-creature-groundscene` at `ef974c3` (smoked = no crash; H13 confirmed).

Supersedes `SESSION-HANDOFF-2026-05-21.md`.

---

## TL;DR

**Phase 3 execution outcome:**
- All 3 plans (03-01, 03-02, 03-03) shipped — R-A through R-H + IN-05 (Drain helper) all `done` in `docs/ai/assessment.md`.
- 104 → 106 tests on master (added 28 R-A + 7 R-B/R-C + 21 R-E/R-F/R-G + post-review fix tests; some lost to bisect branches).
- 2 Critical + 7 Warning code-review findings remediated via `gsd-code-fixer` (commits `427f474..f72721d`).
- Phase verifier returned `status: human_needed` (38/38 must-haves verified, awaiting live-SWG smoke).
- VS 2022 → VS 2026 toolchain swap mid-session (MSBuild only; PlatformToolset still v142 — formal bump deferred to Phase 6).
- TJT cross-repo paired commit: `kennethlong/UtinniPlugins@73b1856` (destroyPlugin export added).

**Then live SWG smoke surfaced a regression:**
- Reliable AV at SWG `0x0051fb0a` on every scene change (not first scene load). Same address across Talus + Naboo.
- VEH catches int3 at `EIP=0x00AA1E3F` (between SWG WndProc @ `0x00AA0970` and PersistentCrcString_ctor @ `0x00AA4050`). Byte pattern `16 CE 87 00 [CC] E8 DB 04 00 00 6A 28 E8 04 71 FE` = execution jumped into 0xCC-padded freed memory → vtable/fn-ptr corruption signature.

**9 bisect cycles narrowed to 2 files of 17 candidate commits:**
- Refuted: CR-01 mutex, R-C WndProc P/Invoke, R-B init + CR-02 (CODEX's combined top picks), Task 3b (`e4b2b59` cui_chat_window etc.), Task-3a `game.{cpp,h}`.
- Confirmed: bug is in `5e81410` (R-A native Task 3a), specifically in `creature_object.{cpp,h}` OR `ground_scene.{cpp,h}` (Cycle 9 reverted both combined → no crash).
- Cycle 10 (next step): revert only `creature_object.{cpp,h}` on master — CODEX flagged it for "lifetime-risk for raw fn-ptr" and it's the smaller surface (1 registry vs 4).

---

## Phase 3 execution shipped (already on master `1facb01`)

### Commit ladder (Phase 3 → review fixes → tracking → UAT)

```
1facb01 test(03): persist human verification items as UAT (live SWG TJT smoke)
c0a228a docs(03-review): record fix dispositions for CR-01..CR-02 + WR-01..WR-07
f72721d fix(03-review-wr-06): atomic pCuiChatWindow + pCuiConsoleHelper
e17d123 fix(03-review-wr-05): promote s_chatInputActive to std::atomic<bool>
c1681bd fix(03-review-wr-04): skip-zero handle overflow guard across all registries
9248a1a fix(03-review-wr-03): move test-only Game accessors out of public header
cb6fad3 fix(03-review-wr-02): document destroyPlugin shutdown-lifecycle contract
9626174 fix(03-review-wr-01): atomic<int> s_ioEventLogCount + document _ReturnAddress
bc2b4ad fix(03-review-cr-02): reject plugins missing destroyPlugin at load time
427f474 fix(03-review-cr-01): add per-registry std::mutex to native callback layer
b63fff1 docs(phase-03): update tracking after wave 3 (plan 03-03 complete)
6e3fce3 chore: merge executor worktree (worktree-agent-a5fd6b6c1d13d2055) — Plan 03-03
4d3955d docs(03-03): complete plan 03-03 SUMMARY — Phase 3 (STAB-02) functionally complete
a0a3d62 docs(03-03): mark R-D / R-E / R-F / R-G done; resolve CON-O-05 (Task 4)
e8fe682 feat(03-03): R-G idempotent Directory.Build.props merger (Task 3)
8aea6af feat(03-03): R-F CppSharp header auto-discovery + CON-O-05 disposition (Task 2)
cb3f373 feat(03-03): R-E Log [CallerMemberName] refactor (Task 1)
07bf6e1 docs(phase-03): update tracking after wave 2 (plan 03-02 complete)
daeabf1 docs(03-02): close R-B + R-C status tracking; finalize SUMMARY.md (Task 5)
49510b4 chore: merge executor worktree (worktree-agent-a265293e4ff1f79f5) — Plan 03-02 Tasks 1-3
8f9c046 docs(03-02): partial SUMMARY — Tasks 1-3 complete; paused at Task 4 checkpoint
9337da7 feat(03-02): R-C single-source-of-truth SWG WndProc RVA via UTINNI_API getter
2884c2c feat(03-02): R-B PluginManager two-phase init + HMODULE tracking + LoadLibrary error log
ff0b473 feat(03-02): extend UTINNI_PLUGIN macro for destroyPlugin + add R-B fixture plugins
f9b64e0 docs(phase-03): update tracking after wave 1 (plan 03-01 complete)
04c86dc chore: merge executor worktree (worktree-agent-aa18191d0189baff3) — Plan 03-01
33e545e docs(03-01): complete callbacks (R-A + R-H + IN-05) plan
e40675e docs(03-01): mark R-A and R-H done in assessment.md (Task 4)
ddda9f0 feat(03-01): native R-A test bridge + NativeCallbacksHandleTests (Task 3c)
e4b2b59 feat(03-01): R-A + R-H native-side post_processing/depth/shader/imgui/cui/log (Task 3b)
5e81410 feat(03-01): R-A + R-H native-side game/scene/object/graphics (Task 3a)  ← REGRESSING COMMIT (narrowed)
2e1b61d feat(03-01): land R-A + R-H managed-side + Log.cs typo fix (Task 2)
b220e36 feat(03-01): land IN-05 Drain helper consolidation (Task 1)
2523228 docs(03): create phase plan  ← PRE-PHASE-3 KNOWN-GOOD
```

Cross-repo paired commit on `kennethlong/UtinniPlugins@73b1856` (TJT destroyPlugin export).

### Phase 3 status board

| Item | Status | Notes |
| --- | --- | --- |
| R-A symmetric Subscribe/Unsubscribe (managed + ~32 native registries) | ✓ landed | Buggy in native creature_object/ground_scene — current debug target |
| R-B symmetric plugin lifecycle (createPlugin/destroyPlugin) | ✓ landed | TJT updated via UtinniPlugins@73b1856 |
| R-C single-source-of-truth WndProc RVA | ✓ landed | `Client::getSwgWndProc()` + `extern "C" getSwgWndProcExport` |
| R-E `[CallerMemberName]` Log refactor | ✓ landed | Caveat: binary-breaking — see [[feedback-caller-attrs-binary-compat]] |
| R-F CppSharp header auto-discovery | ✓ landed | Glob with `_internal/` filter |
| R-G idempotent Directory.Build.props merger | ✓ landed | XmlReader.Create + DtdProcessing=Prohibit |
| R-H snapshot iteration | ✓ landed | Managed `.Values.ToArray()` + native `std::vector` snapshot |
| IN-05 CallbackHelpers.Drain consolidation | ✓ landed | 3 callsites → 1 helper |
| Code review (2 Critical + 7 Warning) | ✓ remediated | dispositions in `03-REVIEW.md` |
| Phase verification | ⚠ partial | `human_needed` — live SWG smoke surfaced new regression |
| **Scene-change regression** | **🔴 IN INVESTIGATION** | **9 bisect cycles complete, 1-2 more to isolate** |

### Phase 3 artifacts

- Plans: `.planning/phases/03-strategic-reworks-r-a-r-h/03-{01,02,03}-PLAN.md`
- Summaries: `.planning/phases/03-strategic-reworks-r-a-r-h/03-{01,02,03}-SUMMARY.md`
- Review: `.planning/phases/03-strategic-reworks-r-a-r-h/03-REVIEW.md` (status: `clean` post-fix, severity counts 0/0/6-info-deferred)
- Verification: `.planning/phases/03-strategic-reworks-r-a-r-h/03-VERIFICATION.md` (`status: human_needed`, 38/38 must-haves verified)
- HUMAN-UAT: `.planning/phases/03-strategic-reworks-r-a-r-h/03-HUMAN-UAT.md` (`status: partial` — TJT live-SWG smoke still pending due to regression)
- Debug session: `.planning/debug/03-scene-change-av-0x0051fb0a.md` (current investigation, 9 cycles logged)

---

## Scene-change-AV bisect: state & next step

### What's known

- **Pre-Phase-3 commit `2523228` is the working baseline** — `SESSION-HANDOFF-2026-05-21.md:126` records "Scene transitions (3 cycles) ✅" at that point.
- **Master HEAD `1facb01` crashes 100% on scene change.**
- **Repro requires TJT loaded** — user issues scene changes via TJT's chat command parser (`addCreateCommandParserCallback`). Memory: [[project-scene-change-via-tjt]].
- **"Naked" after scene change is baseline, NOT a regression signal.** Memory: [[project-tjt-scene-change-naked-baseline]].
- **The 1 setSceneCallback that fires before crash is the managed bridge** (`callSetupSceneCallbacksAction` from `GameCallbacks.Initialize`), not a plugin subscriber. Reverting only `game.{cpp,h}` did NOT fix it (Cycle 8). So the bug is downstream, not in setScene dispatch itself.

### Hypothesis ladder (9 cycles)

| # | Hypothesis | Cycle | Branch | Result |
|---|---|---|---|---|
| H1 | CR-01 per-registry mutex (`427f474`) | 1 | `debug/03-scene-change-av-cycle1` | REFUTED |
| H3 | R-C WndProc P/Invoke (`9337da7`) | 2 | `debug/03-scene-change-av-cycle2` | REFUTED |
| H7 | Pre-Phase-3 baseline test | 3 | `debug/03-scene-change-av-cycle3-baseline` | UNSMOKEABLE (TJT disabled = no scene-change trigger) |
| H8 | R-B init + CR-02 reject (`2884c2c` + `bc2b4ad`) — CODEX top pick | 4 | `debug/03-scene-change-av-cycle4-rb-cr02` | REFUTED |
| H9 (combined) | R-A native cluster (`5e81410` + `e4b2b59` + `ddda9f0`) | 5b | `debug/03-scene-change-av-cycle5b-ra-native` | **CONFIRMED** — no crash |
| H11 | R-A Task 3b alone (`e4b2b59`) | 6 | `debug/03-scene-change-av-cycle6-revert-e4b2b59` | REFUTED |
| H2 | R-A Task 3a alone (`5e81410`) | 7 | `debug/03-scene-change-av-cycle7-revert-5e81410` | **CONFIRMED** — no crash |
| H12 | Task-3a `game.{cpp,h}` alone | 8 | `debug/03-scene-change-av-cycle8-revert-game` | REFUTED |
| H13 | Task-3a `creature_object` + `ground_scene` (binary split) | 9 | `debug/03-scene-change-av-cycle9-creature-groundscene` | **CONFIRMED** — no crash |

### What's confirmed

- Regressing commit: **`5e81410` (R-A native Task 3a)**
- Within that, the bug is in **`creature_object.{cpp,h}` (1 registry: onTarget) OR `ground_scene.{cpp,h}` (4 registries: cameraChange/update/preDraw/postDraw)**
- NOT in `game.{cpp,h}` (5 scene/install/main-loop registries) — Cycle 8 cleared
- NOT in `graphics.{cpp,h}` (10 D3D9 registries) — Cycle 9 cleared (since reverting only creature_object + ground_scene fixed it without touching graphics)

### Next step — Cycle 10

**Revert only `creature_object.{cpp,h}` on master HEAD.** If it works → bug is in creature_object's onTarget registry. If it crashes → bug is in `ground_scene.{cpp,h}` (Cycle 11 confirms).

CODEX's #2 ranking specifically flagged `creature_object.{cpp,h}` for "lifetime risk for raw fn-ptr" — onTarget is a single-registry surface and the smallest revert. Most likely culprit per static analysis.

**Mechanics for Cycle 10 (when resumed):**
```bash
git checkout master
git checkout -b debug/03-scene-change-av-cycle10-revert-creature 1facb01
git checkout 2523228 -- UtinniCore/swg/object/creature_object.cpp UtinniCore/swg/object/creature_object.h
# Rebuild
& 'D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' Utinni.sln /p:Configuration=Release /p:Platform=x86 /v:minimal
# Verify TJT still in bin/Release/Plugins/TheJawaToolbox/
# Tell user to smoke
```

**After regressing file is identified — fix design:**
- Don't ship a permanent revert (gives up R-A surface for that file)
- Most likely root cause class (per CODEX): "R-A formalizes raw callback storage but does not bind callback lifetime to plugin/module/delegate lifetime." onTarget callback might be registered with a fn-ptr that goes stale when creature_object is destroyed/recreated during scene change.
- Plan a CODEX consult on the specific diff of the regressing file pair — design a narrow fix (e.g., insertion-order preservation via a parallel `vector<int>` + `unordered_map`; OR clear the registry on cleanupScene before re-init; OR bind subscriber lifetime to a guard).
- Land the fix on master, then close `03-HUMAN-UAT.md` after green smoke.

---

## Watch items (carry across resume)

| Item | Detail |
| --- | --- |
| **Phase 3 verification gate** | `human_needed` until scene-change regression is fixed + live smoke is clean. Don't mark Phase 3 complete yet. |
| **`03-HUMAN-UAT.md` outstanding** | TJT live-SWG smoke (subpanels open + clean shutdown) still pending — gated on the regression fix. |
| **No regression test for old-plugin-DLL binary compat** | R-E `[CallerMemberName]` would have caught the TJT MissingMethodException at compile time if there were a "loaded plugin DLL with old signature still resolves" Fact. Future hardening item. |
| **No regression test for R-G XmlReader.Create XXE behavior** | `XDocument.Load(string)` empirically doesn't inherit `DtdProcessing=Prohibit` from `XmlReaderSettings.Default` on .NET 4.7.2; we have the fix but no frozen pre-R-G template fixture to assert against. Future hardening item. |
| **9 debug branches alive** | `debug/03-scene-change-av-cycle1` through `cycle9-creature-groundscene` — keep until the regression is fully closed (useful for re-deriving evidence). After fix lands and stays green for a session or two, prune. |
| **VS 2026 PlatformToolset bump deferred** | We use VS 2026 MSBuild but compile C++ against v142. Bump to v144 is a Phase 6 / dedicated phase, NOT a Phase 3 scope item. See [[project-vs2026-toolchain]] for sequencing rules. |
| **`UtinniCoreDotNet/Generated/UtinniCore.cs` dirties on every build** | R-F CppSharp regen re-emits some bindings; build artifact, not a real change. Don't commit it. |

---

## Reproduction recipe (for resume)

```bash
# Get a fresh agent oriented:
cat .planning/SESSION-HANDOFF-2026-05-22.md          # this file
cat .planning/debug/03-scene-change-av-0x0051fb0a.md  # 9-cycle session state
git log --oneline -5                                  # confirm master at 1facb01
git branch | grep cycle                               # see all 9 debug branches

# Latest dump for evidence:
ls -t D:/SWGEmu-Client/SWGEmu/logs/SWGEmu.exe-stage.119798-*.txt | head -1

# Last working build (Cycle 9, no crash):
git checkout debug/03-scene-change-av-cycle9-creature-groundscene
# UtinniCore.dll @ 803,840 bytes; UtinniCoreDotNet.dll @ 1,207,296 bytes (in bin/Release/)

# To start Cycle 10:
git checkout master && git checkout -b debug/03-scene-change-av-cycle10-revert-creature 1facb01
git checkout 2523228 -- UtinniCore/swg/object/creature_object.{cpp,h}
# Rebuild, restore TJT in Plugins/TheJawaToolbox/, smoke.
```

---

## Memory updates this session

- **`project-d3d11-migration`** — User started adding D3D11 to SWG Source client. Utinni hooks D3D9 explicitly; future R-letter item.
- **`feedback-utinniplugins-authority`** — Standing authority to edit/commit/push `kennethlong/UtinniPlugins`; cross-repo paired commits don't need human-action checkpoints.
- **`project-vs2026-toolchain`** — Local default is VS 2026; PlatformToolset stays v142 for now. Now includes "Intent: bump to latest toolchain eventually (deferred until stable)" section with sequencing rules.
- **`feedback-caller-attrs-binary-compat`** — Adding `[CallerMemberName]` defaulted params is source-compat but NOT binary-compat. Caught at TJT MEF compose-time. Future cycles need to rebuild all cross-binary plugins in the same commit.
- **`project-scene-change-via-tjt`** — User triggers scene changes via TJT's chat command parser; disabling TJT loses the repro path. Bisect cycles that disable TJT are unsmokeable.
- **`project-tjt-scene-change-naked-baseline`** — User always lands naked after TJT-driven scene change. Pre-existing baseline behavior, NOT a regression signal.

All entries also linked in `~/.claude/projects/D--Code-Utinni/memory/MEMORY.md`.

---

## Tasks (in_progress)

- `#6 Phase verification + mark complete` — gated on scene-change regression fix
- `#8 Phase 3 regression: scene-change crash at SWG 0x0051fb0a` — narrowed to `creature_object.{cpp,h}` OR `ground_scene.{cpp,h}` within `5e81410`; Cycle 10 reverts only creature_object first

---

*Written 2026-05-22 mid-session. Master at `1facb01`. Active branch: `debug/03-scene-change-av-cycle9-creature-groundscene` at `ef974c3`. Debug session file at `.planning/debug/03-scene-change-av-0x0051fb0a.md`.*
