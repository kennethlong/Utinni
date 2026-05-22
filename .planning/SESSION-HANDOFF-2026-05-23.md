# Session Handoff: 2026-05-23 — R-A heap-free migration shipped

> Picks up from `SESSION-HANDOFF-2026-05-22-NIGHT.md` (Phase 3 closed). This session executed the queued "R-A heap-free migration for other 10 files" detour. One commit, 10 files, 27 callback registries migrated from `std::unordered_map<int, fn_ptr>` to `std::vector<CallbackEntry<Fn>>` + stack-allocated fixed-size snapshot. Master at **`765738c`** (pushed). Tests green (106/106). Live SWG smoke deferred — recommended as opener for next session.

---

## TL;DR

- **Migration status**: shipped. The `dispatchSnapshot` template + `CallbackEntry<Fn>` storage pattern from `ground_scene.cpp` (commit `7201700`) is now applied across all 11 R-A files. Bug class (per-frame heap alloc in R-H snapshot dispatch → SWG allocator fragmentation → scene-change AV) is closed on every callback dispatch site, not just `ground_scene`.
- **Master at `765738c`** (pushed to `origin/master` — `d939be5..765738c`).
- **Tests**: 106/106 xUnit pass on Release/x86. Build clean. Same managed-side API; storage swap is transparent at the boundary.
- **Live SWG smoke NOT run** — flagged in commit message and recommended as opener for next session before declaring fully done. The unit-testable failure modes are covered; the per-frame heap-fragmentation class is a runtime-observed bug that needs SWG-side allocator pressure to surface.
- **Memory updates this session**: 1 (`project-rh-snapshot-no-heap-alloc` updated to reflect 11-of-11 migrated state; flipped "queued follow-up" → "migrated, shared-header consolidation is clean follow-up if/when").

---

## Commit ladder (this session, on top of `d939be5`)

```
765738c refactor(core): R-A heap-free dispatch migration across remaining 10 files
d939be5 docs(state): SESSION-HANDOFF-2026-05-22-NIGHT — Phase 3 closed          ← session start
```

Cross-repo: no UtinniPlugins changes this session.

---

## What changed (per file)

| File | Registries | Notes |
| --- | --- | --- |
| `creature_object.cpp` | 1 (onTarget) | Smallest case. |
| `graphics.cpp` | 10 (pre/post Update, BeginScene, EndScene, PresentWindow, Present) | Highest volume. Stripped the legacy `dispatchVoid` / `dispatchFloat` / `dispatchPresentWindow` file-static helpers in favor of inline `dispatchSnapshot` calls at each hk site. |
| `game.cpp` | 5 (install, preMainLoop, mainLoop, setScene, cleanUpScene) | The size-log diagnostics in `hkSetScene` / `hkCleanupScene` (previously inline with the snapshot copy under lock) were pulled out into their own brief locked blocks ahead of the dispatchSnapshot call — same semantics, cleaner code. |
| `post_processing.cpp` | 2 (preSceneRender, postSceneRender) | Simple. |
| `depth_texture.cpp` | 1 (depthResolve) | Tab+space indentation preserved exactly. |
| `shader.cpp` | 1 (drawPhase) | Keeps the non-naked `dispatchDrawPhaseCallbacks` wrapper because `midPopCell` is `__declspec(naked)` and can't host `std::vector` stack frames. The wrapper now just delegates to `dispatchSnapshot`. |
| `imgui_impl.cpp` | 5 (render + 4 gizmo: Enabled, Disabled, PositionChanged, RotationChanged) | Helper template inserted at **file scope** (before `namespace imgui_impl`) so both `namespace imgui_impl` (renderCallbacks) and the file-scope `onGizmo*Callbacks` registries can dispatch through it. |
| `cui_chat_window.cpp` | 1 (addCommandParser) | Captures `mainCommandParser` from outer scope via static-storage-duration reference (no capture needed in `[]`). |
| `cui_manager.cpp` | 1 (receiveSystemMessage) | Hoists `msgStr.c_str()` to a local before the dispatch lambda — pointer is valid through end of function. |
| `log.cpp` | 1 (outputSink) | Called inside `OutputSink::sink_it_` which spdlog already wraps in its sink mutex; the dispatchSnapshot's own lock-around-snapshot still applies under that for the cross-thread subscribe / unsubscribe case per WR-07. |

Total: **27 registries** across 10 files all converted. 11 anonymous-namespace copies of the helper template (one per file including ground_scene's original). 

Storage shape: `std::vector<CallbackEntry<Fn>>` (insertion-order). Subscribe is unchanged. Unsubscribe is now linear in N (fine — N ≤ 16 by design). Dispatch uses `kInlineCap = 16` stack snapshot; heap fallback only if subscriber count exceeds 16 (won't trigger in practice — most registries have 1 subscriber).

---

## What's next

**Recommended opener:** live SWG smoke + branch prune.

1. **Live SWG smoke** — TJT-driven scene change (e.g. `/warp` Tatooine → Naboo) to confirm no scene-change AV regression. See [[project-scene-change-via-tjt]]. End-to-end works if user lands naked in world (per [[project-tjt-scene-change-naked-baseline]]).
2. **Debug branch cleanup** — `git branch -D debug/03-scene-change-av-cycle{1..11}` plus `debug/03-scene-change-av-cycle10-revert-creature`. The night handoff said "wait at least one more session of stability" — this session counts. After the smoke confirms, prune.
3. **Phase 4: Tier 2 CLI shim + golden fixtures.** `/gsd-discuss-phase 4`. Goal: `utinni-cli` exe in the same solution, golden-file tests against checked-in fixtures, target ~60-70% of "Kenny please verify" loops becoming unattended CI runs. Resolves CON-O-09 (fixture storage) and CON-O-11 (CLI distribution).

**Optional cleanup deferred** (not on the critical path):
- Consolidate the 11 anonymous-namespace `dispatchSnapshot` / `CallbackEntry<Fn>` template copies into a shared header (e.g. `UtinniCore/utility/callback_dispatch.h`). Drops ~500 lines of duplication. Worth doing as a deliberate refactor but not urgent — internal linkage means the duplication has zero runtime cost. Captured in [[project-rh-snapshot-no-heap-alloc]] as follow-up.

---

## Watch items / carry-forward

| Item | Detail |
| --- | --- |
| **Live SWG smoke deferred** | This commit landed without live verification. Test suite (106/106) covers the API contract but not the runtime allocator-pressure class of bug. Smoke on next session opener before declaring fully done. |
| **12 debug branches still alive** | `debug/03-scene-change-av-cycle1` through `cycle11-revert-groundscene` plus `cycle10-revert-creature`. The night handoff said wait at least one more session of stability. This session counts. Prune after next session's live smoke confirms stable. Cleanup command: `git branch -D debug/03-scene-change-av-*`. |
| **11-fold template duplication** | Each R-A file's anonymous namespace has its own copy of the `CallbackEntry<Fn>` struct + `dispatchSnapshot<Fn, Invoke>` template (~50 lines). Internal linkage means no link conflict, zero runtime cost. Consolidation to a shared header is a clean follow-up refactor but explicitly out of scope for this migration — keeps the proven pattern unchanged file-by-file. |
| **CppSharp regen artifact churn (again)** | `UtinniCoreDotNet/Generated/UtinniCore.cs` dirtied during this session's build (textual reshuffle, +302/-302). Restored via `git restore` before staging. The build behavior is unchanged from the night handoff — same +151/-151 ordering shuffle on every build cycle. |
| **VS 2026 PlatformToolset bump still deferred** | We compile C++ against v142 (MSVC 14.29). v144 bump remains a dedicated future phase, NOT scope creep. See [[project-vs2026-toolchain]]. |

---

## Memory updates this session

- **UPDATED `project-rh-snapshot-no-heap-alloc`** — flipped "10 files queued for follow-up migration" to "all 11 R-A files migrated 2026-05-22 after Phase 3 close; consolidating into shared header is a clean follow-up if/when a deliberate refactor takes it on." The reference implementation pointer (`ground_scene.cpp`) is preserved.

No new memories. The migration mechanics didn't surface anything that wasn't already captured by prior memories.

---

## Reproduction recipe (for resume)

```bash
# Confirm state
cat .planning/SESSION-HANDOFF-2026-05-23.md       # this file
git log --oneline -3                               # master at 765738c
git branch | grep cycle | wc -l                    # 12 debug branches still alive

# Verify the migration is on master + DLLs are fresh
git show --stat 765738c | head -15
ls -la bin/Release/UtinniCore.dll bin/Release/UtinniCoreDotNet.dll

# Sanity: rebuild + tests
& 'D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' Utinni.sln /p:Configuration=Release /p:Platform=x86 /v:minimal /m
git restore UtinniCoreDotNet/Generated/UtinniCore.cs   # drop CppSharp regen churn
dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" --nologo
# Expect: 106/106 tests pass

# Live SWG smoke (next-session opener, recommended)
# 1. Launch SWGEmu via Utinni Launcher
# 2. Log in to live SWGEmu
# 3. Trigger TJT scene change via chat command: /warp naboo (or similar)
# 4. Confirm: scene loads, user lands naked in world, no AV at 0x0051fb0a or related sites
# 5. If clean: branch prune
git branch -D debug/03-scene-change-av-cycle1 \
              debug/03-scene-change-av-cycle2 \
              debug/03-scene-change-av-cycle3 \
              debug/03-scene-change-av-cycle4 \
              debug/03-scene-change-av-cycle5 \
              debug/03-scene-change-av-cycle6 \
              debug/03-scene-change-av-cycle7 \
              debug/03-scene-change-av-cycle8 \
              debug/03-scene-change-av-cycle9 \
              debug/03-scene-change-av-cycle10-revert-creature \
              debug/03-scene-change-av-cycle11-revert-groundscene
# (Run `git branch | grep cycle` first to confirm the actual names.)

# Then Phase 4:
#   /gsd-discuss-phase 4
```

---

## Tasks closed this session

- `#1 Survey all 10 R-A target files for variance` → completed
- `#2 Migrate creature_object.cpp` → completed
- `#3 Migrate graphics.cpp (10 registries, has existing helpers)` → completed
- `#4 Migrate game.cpp (5 registries)` → completed
- `#5 Migrate post_processing.cpp (2 registries)` → completed
- `#6 Migrate depth_texture.cpp (1 registry)` → completed
- `#7 Migrate shader.cpp (1 registry)` → completed
- `#8 Migrate imgui_impl.cpp (5 registries)` → completed
- `#9 Migrate cui_chat_window.cpp + cui_manager.cpp + log.cpp` → completed
- `#10 Build Release x86 + run test suite` → completed (106/106)
- `#11 Commit + push migration` → completed (`765738c` pushed)

No tasks open at end of session.

---

*Written 2026-05-23 morning. Master at `765738c` (pushed). R-A migration shipped, tests green, live SWG smoke deferred. Next session: smoke + branch prune, then Phase 4 (CLI shim).*
