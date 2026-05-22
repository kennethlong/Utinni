# Session Handoff: 2026-05-22 (Night) — Phase 3 CLOSED

> Phase 3 is fully done. Live SWG TJT smoke surfaced a scene-change AV regression on the morning handoff (`SESSION-HANDOFF-2026-05-22.md`); this evening completed Cycles 10-11 of the bisect, ran a CODEX consult on the isolated `ground_scene.{cpp,h}` diff, implemented + shipped the fix at `7201700`, smoked clean, and closed Phase 3 verification. Master at **`9c8edd3`** (pushed to origin). Next session can move to Phase 4 (Tier 2 CLI shim + golden fixtures) or take the queued R-A heap-free migration follow-up first.

Supersedes `SESSION-HANDOFF-2026-05-22.md` (which was mid-investigation).

---

## TL;DR

- **Phase 3 status**: `passed`. 5/5 ROADMAP success criteria VERIFIED. 38/38 must-haves verified. HUMAN-UAT 1/1 pass. Build green. 106/106 tests green. Review: 0 Critical / 0 Warning open (6 Info-deferred).
- **Master at `9c8edd3`** (pushed to `origin/master` at end of session — `b36265e..9c8edd3`).
- **Scene-change AV bug**: root cause was per-frame heap allocation in R-H snapshot dispatch in `ground_scene.cpp` (`hkDrawLoop` / `hkUpdateLoop` did `std::vector<fn_ptr> snapshot; snapshot.reserve(N);` every frame). Fix swaps storage to `std::vector<CallbackEntry<fn_ptr>>` + stack-allocated fixed-size snapshot (kInlineCap=16) via an anonymous-namespace `dispatchSnapshot` template helper. R-A API + CR-01 mutex + R-H semantics all preserved. Scope: `ground_scene.cpp` only (the other 10 R-A files still use `unordered_map`, queued as follow-up).
- **Bisect dropped**: 11 cycles (Cycles 1-11) ran across two sessions. Cycle 12 (mutex-vs-storage within ground_scene) was skipped per CODEX recommendation. Total: 11 cycles + 1 fix cycle = 12 build/smoke iterations.
- **Memory updates this session**: 1 new (`project-rh-snapshot-no-heap-alloc`), linked into MEMORY.md.

---

## Commit ladder (this session, on top of `91d43fc`)

```
9c8edd3 docs(03): close verification + UAT after scene-change AV fix landed
7201700 fix(03): ground_scene heap-free dispatch via vector + stack snapshot
91d43fc docs(state): SESSION-HANDOFF-2026-05-22 — Phase 3 scene-change AV bisect mid-investigation   ← session start
```

Cross-repo: no UtinniPlugins changes this session.

---

## Session story (brief — full detail in `.planning/debug/03-scene-change-av-0x0051fb0a.md`)

| Step | What happened | Outcome |
|---|---|---|
| Cycle 10 | Revert only `creature_object.{cpp,h}` to `2523228`; build; smoke | CRASH at `0x0051fb0a` → H14a REFUTED (creature_object alone is NOT the bug) |
| Cycle 11 | Revert only `ground_scene.{cpp,h}` to `2523228`; build; smoke | CLEAN → H14b CONFIRMED (`ground_scene` is necessary AND sufficient) |
| CODEX consult | Wrote `.planning/debug/codex-consult-ground-scene-av.md` (305-line diff + bisect context + ranked priors). User pasted into CODEX. | CODEX named per-frame heap alloc in R-H snapshot dispatch as the mechanism; suspect commit `5e81410` (not `427f474` mutex); skip Cycle 12; design = vector storage + stack snapshot |
| Fix implementation | New branch `fix/03-scene-change-groundscene-vector-storage` off master HEAD; rewrote `ground_scene.cpp` per CODEX recommendation; build clean; 106/106 tests pass | Ready to smoke |
| Live SWG smoke | Naboo `/warp` via TJT chat parser | NO CRASH → fix VALIDATED |
| Land + close | Fast-forward merge to master (`7201700`); update VERIFICATION + HUMAN-UAT + SESSION-HANDOFF + memory; commit + push (`9c8edd3`) | Phase 3 done |

---

## Phase 3 final status board

| Item | Status | Notes |
| --- | --- | --- |
| R-A symmetric Subscribe/Unsubscribe (managed + ~32 native registries) | ✓ landed | `ground_scene` uses new vector+stack pattern post-`7201700`; other 10 files keep `unordered_map` storage (follow-up) |
| R-B symmetric plugin lifecycle (createPlugin/destroyPlugin) | ✓ landed | TJT updated via UtinniPlugins@73b1856 |
| R-C single-source-of-truth WndProc RVA | ✓ landed | `Client::getSwgWndProc()` + `extern "C" getSwgWndProcExport` |
| R-E `[CallerMemberName]` Log refactor | ✓ landed | Binary-compat caveat documented in [[feedback-caller-attrs-binary-compat]] |
| R-F CppSharp header auto-discovery | ✓ landed | Glob with `_internal/` filter |
| R-G idempotent Directory.Build.props merger | ✓ landed | XmlReader.Create + DtdProcessing=Prohibit |
| R-H snapshot iteration | ✓ landed | Managed `.Values.ToArray()` + native (now stack-snapshot for ground_scene; std::vector for other 10) |
| IN-05 CallbackHelpers.Drain consolidation | ✓ landed | 3 callsites → 1 helper |
| Code review (2 Critical + 7 Warning) | ✓ remediated | 03-REVIEW.md fully closed |
| **Scene-change regression** | **✓ FIXED** | Commit `7201700`; root cause = per-frame heap alloc in R-H snapshot dispatch on `ground_scene` |
| Phase verification | ✓ passed | 38/38 must-haves; UAT 1/1 pass |

---

## Watch items / carry-forward

| Item | Detail |
| --- | --- |
| **R-A heap-free migration for other 10 files** | `ground_scene.cpp` got the vector+stack-snapshot fix; the other 10 R-A files (creature_object, graphics, post_processing, depth_texture, shader, imgui_impl, cui_chat_window, cui_manager, log, game.cpp) still use `std::unordered_map<int, fn_ptr>` + heap-allocated `std::vector::reserve()` snapshot per dispatch. They were cleared by the bisect (not on the scene-change crash path), but the pattern is latent for any other hot dispatch site. Captured in `.planning/debug/03-scene-change-av-0x0051fb0a.md` resolution section as "follow-up phase will migrate them." Worth a small dedicated phase before Phase 4 starts piling on. |
| **12 debug branches alive** | `debug/03-scene-change-av-cycle1` through `cycle11-revert-groundscene` plus `cycle10-revert-creature`. Per SESSION-HANDOFF-2026-05-22 convention, prune "after the fix lands and stays green for a session or two." Don't prune this session — wait for at least one more session of stability. Cleanup command: `git branch -D debug/03-scene-change-av-*`. |
| **`fix/03-scene-change-groundscene-vector-storage` deleted** | Branch removed after fast-forward merge to master (commit `7201700` is on master). |
| **CppSharp regen artifact churn** | `UtinniCoreDotNet/Generated/UtinniCore.cs` dirties on every build (CppSharp re-emits some bindings with different ordering — +151/-151 textual shuffle, same content). Do NOT commit. Run `git restore UtinniCoreDotNet/Generated/UtinniCore.cs` before staging anything from a build. |
| **VS 2026 PlatformToolset bump deferred** | We use VS 2026 MSBuild but compile C++ against v142 (MSVC 14.29). Bump to v144 is a Phase 6 / dedicated phase, NOT scope creep. See [[project-vs2026-toolchain]]. |
| **`addCameraChangeCallback` not yet covered by R-H snapshot test** | `CallbacksSnapshotIterationTests.Subscribe_DuringDispatch_GroundSceneCameraChange_NotInvokedInCurrentIteration` covers managed-side; native-side `unordered_map` path → `vector` path migration is implicitly tested but no Fact asserts the vector ordering or stack-snapshot path explicitly. Low priority. |
| **`addCameraChangeCallback` migrated too** | The `toggleFreeCamera` dispatch site is on the user-toggle path (not per-frame), but uses the same `dispatchSnapshot` helper for consistency. Verified working via tests. |

---

## What's next on the ROADMAP

**Phase 4: Tier 2 CLI shim + golden fixtures** — goal: a `utinni-cli` executable in the same solution references the same core libraries, exposes the operations the UI calls, with golden-file tests against checked-in fixtures. Estimated to convert 60-70% of manual "Kenny please verify" loops into unattended CI runs.

Depends on Phase 3 (DONE). Open questions to resolve as part of phase: CON-O-09 (fixture storage — in-repo vs Git LFS) and CON-O-11 (CLI distribution — public artifact vs test-harness-internal).

**Optional pre-Phase-4 detour: R-A heap-free migration phase.** Tightens the bug-class window before more callback infrastructure piles on. ~1 day of work. Could be folded into the next session's first task.

Suggested next-session opener:
- `cat .planning/SESSION-HANDOFF-2026-05-22-NIGHT.md` (this file)
- Decide: R-A heap-free migration first, or jump straight to Phase 4?
- If migration: `/gsd-discuss-phase` would be heavy — this is more like a `gsd-quick` job (apply the same `dispatchSnapshot` pattern across 10 files in a single commit).
- If Phase 4: `/gsd-discuss-phase 4`.

---

## Memory updates this session

- **NEW `project-rh-snapshot-no-heap-alloc`** — R-H snapshot dispatch on render hot paths MUST use stack-allocated fixed-size snapshots, not heap-allocated `std::vector::reserve()`. Per-frame heap alloc-free pairs fragmented SWG's allocator and crashed scene change at `0x0051fb0a`. Fix lives in `ground_scene.cpp` as the `dispatchSnapshot` template. Other 10 R-A files queued for migration. Linked from MEMORY.md.

No other memory updates — the bisect mechanics and CODEX-consult pattern were already captured in prior memories ([[feedback-codex-peer-review]], [[feedback-d3d9-hook-diagnosis]] et al).

---

## Reproduction recipe (for resume)

```bash
# Confirm state
cat .planning/SESSION-HANDOFF-2026-05-22-NIGHT.md     # this file
git log --oneline -3                                   # master at 9c8edd3
git branch | grep cycle | wc -l                        # 12 debug branches still alive

# Verify the fix is on master + DLLs are fresh
git show --stat 7201700 | head -5
ls -la bin/Release/UtinniCore.dll bin/Release/UtinniCoreDotNet.dll
# UtinniCore.dll @ 808,960 bytes (post-fix); UtinniCoreDotNet.dll @ 1,209,344

# Sanity: rebuild + run tests (optional)
& 'D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' Utinni.sln /p:Configuration=Release /p:Platform=x86 /v:minimal
# Expect: 106/106 tests pass

# If next-session task = R-A migration of remaining 10 files:
#   Pattern lives at UtinniCore/swg/scene/ground_scene.cpp (anonymous-namespace dispatchSnapshot helper)
#   Files to migrate: UtinniCore/swg/object/creature_object.{cpp,h}, swg/graphics/graphics.{cpp,h}, swg/game/game.{cpp,h},
#                     swg/graphics/post_processing.{cpp,h}, swg/graphics/depth_texture.{cpp,h}, swg/graphics/shader.{cpp,h},
#                     swg/ui/imgui_impl.{cpp,h}, swg/ui/cui_chat_window.{cpp,h}, swg/ui/cui_manager.{cpp,h}, utility/log.{cpp,h}

# If next-session task = Phase 4 (CLI shim):
#   /gsd-discuss-phase 4
```

---

## Tasks closed this session

- `#1 Cycle 10: revert creature_object only and rebuild` → completed (CRASH; H14a REFUTED)
- `#2 Cycle 11: revert ground_scene only and rebuild` → completed (CLEAN; H14b CONFIRMED)
- `#3 Analyze ground_scene R-A diff and design fix` → completed (CODEX prompt drafted)
- `#4 Implement CODEX fix: vector storage + stack snapshot` → completed (commit `7201700`)
- `#5 Close Phase 3: update verification, UAT, debug log, memory` → completed (commit `9c8edd3`, pushed)

No tasks open at end of session.

---

*Written 2026-05-22 evening. Master at `9c8edd3` (pushed). Phase 3 closed. Next phase: Phase 4 (CLI shim) or R-A migration detour. Debug session file at `.planning/debug/03-scene-change-av-0x0051fb0a.md` (untracked working notes, retain for grep-back).*
