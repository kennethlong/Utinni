---
phase: 03-strategic-reworks-r-a-r-h
plan: 01
subsystem: callbacks
tags: [callbacks, pinvoke, xunit, unordered_map, snapshot-iteration, handle-based-api]

# Dependency graph
requires:
  - phase: 02.1-phase-02-gap-closure-critical-correctness-harness-quality-fr
    provides: "IN-05 carry-over (Drain helper consolidation), Phase 02.1 max-harness posture, utinni_test_* C-linkage export precedent, [Collection(\"StaticCallbackState\")] xUnit serialization precedent"
  - phase: 02-critical-bug-burn-down-c-01-c-15
    provides: "C-04 GroundSceneCallbacks Drain helper (inline body now consolidated), C-16 GameCallbacks GC-survival delegate-pin pattern, sibling test project / DllImport NativeBridge pattern, ExportResolutionTests baseline"
  - phase: 01-ci-tier-1-c-scaffold
    provides: "xUnit 2.9.x + net472 x86 test infrastructure, [Method]_[Scenario]_[ExpectedOutcome] naming convention, CI gate on master"
provides:
  - "Handle-based Subscribe(Action)->int / Unsubscribe(int)->bool across 6 managed *Callbacks classes (Game/GroundScene/Object/Cui/ImGui + Log)"
  - "Handle-based subscribe*()->int / unsubscribe*(int)->bool across 11 native callback-registry files (32 registries) backed by std::unordered_map<int, fn_ptr>"
  - "Snapshot iteration pattern at every dispatch site (managed: lock + Dictionary.Values.ToArray(); native: std::vector copy from unordered_map values) — Subscribe-during-dispatch is invisible to current iteration, fires next iteration"
  - "Shared UtinniCoreDotNet/Callbacks/CallbackHelpers.cs::Drain consolidating 3 prior per-class duplicates (Phase 02.1 IN-05 carry-over closed)"
  - "Log.AddOuputSinkCallback typo paired with correctly-spelled AddOutputSinkCallback (legacy alias retained for source-compat)"
  - "5 new utinni_test_* C-linkage exports (subscribeInstall / unsubscribeInstall / dispatchInstall / installSubscriberCount / addInstall) wiring Game::installCallbacks to xUnit P/Invoke harness"
  - "28 new xUnit Facts across 4 new test files (CallbackHelpersTests, CallbacksSubscribeUnsubscribeTests + LogTypoFixTests, CallbacksSnapshotIterationTests, NativeCallbacksHandleTests) — total test count 48 → 76"
affects: [phase-03-02-plugin-lifecycle, phase-03-03-build-tooling, plugins-tjt, plugins-sytner, code-review-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Handle-based Subscribe/Unsubscribe with opaque int (0 = invalid sentinel) for both managed Dictionary<int, Action>+lock and native std::unordered_map<int, fn_ptr>"
    - "Snapshot iteration (R-H D-12) — copy registry under lock to local array (managed) or local std::vector (native), iterate the copy outside the lock; Subscribe-during-dispatch isolated to next iteration"
    - "D-10 source-compat wrappers — Add*Callback retained as thin wrapper around Subscribe*, return value discarded so existing UtinniPlugins (TJT, Sytner) keep working without recompile"
    - "Native-side test bridge via __declspec(naked)-incompatible dispatch helper factoring (shader.cpp dispatchDrawPhaseCallbacks) — works around MSVC C2489/C3068 for std::vector inside naked functions"
    - "utinni_test_* C-linkage export naming convention (Phase 02.1 precedent) extended with subscribe/unsubscribe/dispatch/count quadruple per representative registry"

key-files:
  created:
    - "UtinniCoreDotNet/Callbacks/CallbackHelpers.cs (shared Drain helper, IN-05 consolidation)"
    - "UtinniCoreDotNet.Tests/CallbackHelpersTests.cs (Drain semantics + grep-gate)"
    - "UtinniCoreDotNet.Tests/CallbacksSubscribeUnsubscribeTests.cs (managed Subscribe/Unsubscribe + LogTypoFixTests)"
    - "UtinniCoreDotNet.Tests/CallbacksSnapshotIterationTests.cs (R-H dispatch semantics)"
    - "UtinniCoreDotNet.Tests/NativeCallbacksHandleTests.cs (native P/Invoke harness)"
  modified:
    - "UtinniCoreDotNet/Callbacks/GameCallbacks.cs (Subscribe/Unsubscribe + snapshot dispatch + Drain delegation)"
    - "UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs (cameraChange Subscribe/Unsubscribe + Drain delegation; queue-backed callbacks retained)"
    - "UtinniCoreDotNet/Callbacks/ObjectCallbacks.cs (onTarget Subscribe/Unsubscribe + Drain delegation)"
    - "UtinniCoreDotNet/Callbacks/CuiCallbacks.cs (Subscribe/Unsubscribe + completes missing Remove pair)"
    - "UtinniCoreDotNet/Callbacks/ImGuiCallbacks.cs (4 callbacks Subscribe/Unsubscribe + completes missing onEnabled/onDisabled Remove pair)"
    - "UtinniCoreDotNet/Utility/Log.cs (SubscribeOutputSink/UnsubscribeOutputSink + AddOutputSinkCallback typo-corrected twin with aliases)"
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj (Compile entry for new CallbackHelpers.cs)"
    - "UtinniCore/swg/game/game.cpp + .h (5 registries → unordered_map + Subscribe/Unsubscribe + snapshot dispatch + getInstallSubscriberCount accessor)"
    - "UtinniCore/swg/scene/ground_scene.cpp + .h (4 registries)"
    - "UtinniCore/swg/object/creature_object.cpp + .h (1 registry)"
    - "UtinniCore/swg/graphics/graphics.cpp + .h (10 registries + 3 dispatch helpers)"
    - "UtinniCore/swg/graphics/post_processing.cpp + .h (2 registries)"
    - "UtinniCore/swg/graphics/depth_texture.cpp + .h (1 registry, static-class storage per D-08 planner-discretion)"
    - "UtinniCore/swg/graphics/shader.cpp + .h (1 registry; dispatch factored to dispatchDrawPhaseCallbacks helper because std::vector is disallowed inside __declspec(naked))"
    - "UtinniCore/swg/ui/imgui_impl.cpp + .h (5 registries: render + 4 gizmo)"
    - "UtinniCore/swg/ui/cui_chat_window.cpp + .h (1 registry)"
    - "UtinniCore/swg/ui/cui_manager.cpp + .h (1 registry)"
    - "UtinniCore/utility/log.cpp + .h (1 registry)"
    - "UtinniCore/test_exports.cpp (5 new utinni_test_* exports + bumped resolveExports list)"
    - "UtinniCoreDotNet.Tests/ExportResolutionTests.cs (ExpectedExportCount 13 → 18)"
    - "UtinniCoreDotNet/Generated/UtinniCore.cs (regenerated by CppSharp — auto-picks up new subscribe*/unsubscribe* surface)"
    - "docs/ai/assessment.md (R-A and R-H rows marked done with implementing commit SHAs)"

key-decisions:
  - "D-09 sentinel: handle 0 reserved as the invalid sentinel uniformly across all 6 managed and 32 native registries — Unsubscribe(0) returns false universally without touching the registry"
  - "D-10 source-compat: every Add*Callback retained as a thin wrapper around Subscribe* (return value discarded). RemoveCallback (where it existed pre-Phase-3) retained as best-effort delegate-equality lookup; new code uses Unsubscribe(handle) directly"
  - "D-12 snapshot dispatch: managed sites use lock + Dictionary.Values.ToArray(); native sites copy unordered_map values into a local std::vector. Same observable semantics: Subscribe-during-dispatch lives in the registry but doesn't appear in the current iteration's snapshot — fires on NEXT dispatch instead of throwing InvalidOperationException (managed) or invalidating iterator (native)"
  - "D-08 planner-discretion (depth_texture.cpp): kept static-class storage even though DepthTexture::addDepthResolveCallback is a class-static member — every existing call site treats this as a process-wide registry"
  - "Naked-function workaround (shader.cpp): MSVC C2489 (initialized auto inside naked) + C3068 (objects requiring unwinding inside naked) block local std::vector inside __declspec(naked) midPopCell. Factored the snapshot dispatch into a non-naked static helper dispatchDrawPhaseCallbacks(int) that owns its own stack frame; the naked function calls into it"

patterns-established:
  - "Managed handle-based registry: private static readonly Dictionary<int, Action> + private static int s_next{Name}Id = 1 + private static readonly object {name}Lock + public Subscribe{Name}/Unsubscribe{Name}/legacy Add{Name}Callback wrapper + snapshot dispatch (.Values.ToArray() under lock)"
  - "Native handle-based registry: static std::unordered_map<int, fn_ptr> + static int s_next{Name}Id = 1 + Class::subscribe{Name}Callback / Class::unsubscribe{Name}Callback / Class::add{Name}Callback wrapper + snapshot dispatch (local std::vector copy then iterate)"
  - "utinni_test_* native bridge per representative registry: subscribe / unsubscribe / dispatch / count / legacy-add quintet — gives xUnit P/Invoke harness everything needed to verify Subscribe/Unsubscribe/dispatch/D-09/D-10 invariants"
  - "Per-test delegate isolation in P/Invoke tests: test-local NativeCallback closure capturing local counter variable + GCHandle.Alloc pin + try/finally cleanup; delta-based assertions vs InstallSubscriberCount because the native registry is process-wide and includes managed-initialization residue plus intentional D-10 leaks"

requirements-completed: [STAB-02]

# Metrics
duration: ~5h
completed: 2026-05-21
---

# Phase 3 Plan 01: Callbacks (R-A + R-H + IN-05) Summary

**Handle-based Subscribe/Unsubscribe (opaque int, sentinel 0) + R-H snapshot dispatch across 6 managed + 11 native callback registry files; IN-05 Drain helper consolidated; Log.cs `AddOuputSinkCallback` typo corrected; 28 new xUnit Facts (managed + native via P/Invoke) all green.**

## Performance

- **Duration:** ~5h (single executor session, 2026-05-21)
- **Started:** 2026-05-21 (worktree agent-aa18191d0189baff3 spawn)
- **Completed:** 2026-05-21
- **Tasks:** 4 (Task 3 split into 3 sub-commits per D-03 / PATTERNS.md line 414)
- **Files modified:** 31 source files + 5 test files + 1 docs file = **37 files** (+5 new files)
- **Commits:** 6 (Task 1 + Task 2 + Task 3a + Task 3b + Task 3c + Task 4)
- **Test count:** 48 → **76** (+28 net new Facts)

## Accomplishments

- **IN-05 carry-over closed (D-11):** Single `internal static CallbackHelpers.Drain(ConcurrentQueue<Action>)` body in a new shared file replaces the three duplicated per-class drains in `GameCallbacks` / `GroundSceneCallbacks` / `ObjectCallbacks`. A negative grep gate in `CallbackHelpersTests.Drain_NoDuplicateBodies_RemainInPerClassFiles` keeps the consolidation honest — re-introducing a per-class `while … TryDequeue` body in `Callbacks/*.cs` (outside `CallbackHelpers.cs`) fails CI.
- **Managed-side R-A landed across every `*Callbacks` class plus `Log.cs`:** handle-based `Subscribe(Action) -> int` paired with `Unsubscribe(int) -> bool`; handle `0` reserved as invalid sentinel per D-09; `Unsubscribe(unknown)` and `Unsubscribe(0)` both return `false` without touching the registry. Legacy `Add*Callback` retained as thin wrapper per D-10 — existing UtinniPlugins (TJT, Sytner) compile-and-run unchanged. The `CuiCallbacks.OnReceiveSystemMessage` callback (Add-only pre-Phase-3) gained a Remove pair; `ImGuiCallbacks` gained the previously-missing Remove pair for `onEnabled` / `onDisabled`.
- **`Log.AddOuputSinkCallback` typo corrected via R-A overlap:** correctly-spelled `AddOutputSinkCallback` / `RemoveOutputSinkCallback` are the new primary API; misspelled aliases retained for source-compat with any in-the-wild UtinniPlugin caller. New `SubscribeOutputSink(Action<string>) -> int` / `UnsubscribeOutputSink(int) -> bool` follow the same Dictionary<int, Action<string>> + lock pattern.
- **Managed-side R-H snapshot dispatch landed at every dispatch site:** lock → `.Values.ToArray()` → unlock → `foreach` iterate. Subscribe-during-dispatch lands in the registry but doesn't appear in the current iteration's snapshot — fires on NEXT dispatch instead of throwing `InvalidOperationException`. Same protection covers Unsubscribe-during-dispatch (callback self-removal). Verified by 3 Facts in `CallbacksSnapshotIterationTests`.
- **Native-side R-A landed across all 11 callback-registry files (32 registries):** every `swg::*` and `utility::log` callback storage converted from `std::vector<fn_ptr>` to `std::unordered_map<int, fn_ptr>` plus per-registry monotonic `s_next{Name}Id` counter. Subscribe / Unsubscribe declared in matching `.h` files alongside the legacy `add*` declaration; `add*` retained as wrapper. `Game::getInstallSubscriberCount()` added as test-only accessor (used by the bridge exports).
- **Native-side R-H snapshot dispatch landed at every dispatch site:** each site copies `unordered_map` values into a local `std::vector<fn_ptr>` before iteration. Three helpers (`dispatchVoid` / `dispatchFloat` / `dispatchPresentWindow`) keep `graphics.cpp`'s 10 dispatch sites readable. `shader.cpp` factored its naked-function dispatch into a non-naked static helper `dispatchDrawPhaseCallbacks(int)` because MSVC C2489 / C3068 reject local `std::vector` inside `__declspec(naked)` functions (no exception-unwind frame).
- **`utinni_test_*` C-linkage native bridge added** (5 new exports — subscribe / unsubscribe / dispatch / count / legacy-add against `Game::installCallbacks`) wiring the native registry to `NativeCallbacksHandleTests` via P/Invoke. `ExportResolutionTests.ExpectedExportCount` bumped 13 → 18 to keep the export-decoration harness honest.
- **`docs/ai/assessment.md` §Status tracking:** R-A and R-H rows marked `done` with the implementing commit SHAs.

## Task Commits

Each task was committed atomically (Task 3 split into 3 sub-commits per D-03 and PATTERNS.md line 414):

1. **Task 1: IN-05 Drain helper consolidation** — `b220e36` (feat)
2. **Task 2: R-A + R-H managed-side + Log.cs typo fix** — `2e1b61d` (feat)
3. **Task 3a: R-A + R-H native game/scene/object/graphics** — `5e81410` (feat)
4. **Task 3b: R-A + R-H native post_processing/depth/shader/imgui/cui/log** — `e4b2b59` (feat)
5. **Task 3c: native R-A test bridge + NativeCallbacksHandleTests** — `ddda9f0` (feat)
6. **Task 4: docs/ai/assessment.md R-A and R-H done** — `e40675e` (docs)

## Files Created/Modified

**Created (5):**
- `UtinniCoreDotNet/Callbacks/CallbackHelpers.cs` — shared `internal static Drain(ConcurrentQueue<Action>)` (IN-05 consolidation)
- `UtinniCoreDotNet.Tests/CallbackHelpersTests.cs` — 4 Facts: empty-drain, FIFO order, producer-mid-drain, negative grep gate
- `UtinniCoreDotNet.Tests/CallbacksSubscribeUnsubscribeTests.cs` — 14 Facts: Subscribe/Unsubscribe round-trip, handle-0 sentinel, parameterized `[Theory]` over every (Type, Subscribe, Unsubscribe), legacy Add wrapper still fires, LogTypoFixTests
- `UtinniCoreDotNet.Tests/CallbacksSnapshotIterationTests.cs` — 3 Facts: Subscribe-during-dispatch (next-iteration semantics, managed + cross-class), Unsubscribe-during-dispatch (no InvalidOperationException)
- `UtinniCoreDotNet.Tests/NativeCallbacksHandleTests.cs` — 6 Facts via P/Invoke: Subscribe returns non-zero handle + dispatch, Unsubscribe prevents dispatch, Unsubscribe(unknown), Unsubscribe(0), Subscribe-during-dispatch, legacy AddInstall still dispatches

**Modified (31 source files + 1 docs):**
- 6 managed `*Callbacks` classes + `Log.cs` + `UtinniCoreDotNet.csproj`
- 11 native `.cpp` files + 11 matching `.h` files (game/scene/object + 4 graphics + 3 ui + utility/log)
- `UtinniCore/test_exports.cpp` (5 new exports + bumped resolveExports list)
- `UtinniCoreDotNet.Tests/ExportResolutionTests.cs` (ExpectedExportCount 13 → 18)
- `UtinniCoreDotNet/Generated/UtinniCore.cs` (regenerated by CppSharp — auto-picked up new surface)
- `docs/ai/assessment.md` (R-A + R-H done with commit SHAs)

## Decisions Made

- **Sub-commit boundary inside Task 3 (per PATTERNS.md line 414):** Task 3 touches ~24 files, exceeds the 15-file reviewability threshold, so was split into 3 sub-commits (Task 3a / 3b / 3c) per D-03's multi-commit allowance. Each sub-commit keeps CI green on its own per D-04: 3a = 4 biggest files (game / ground_scene / creature_object / graphics); 3b = remaining 7 native files; 3c = test bridge layer.
- **Naked-function dispatch workaround (shader.cpp):** MSVC C2489 ("initialized auto or register variable not allowed at function scope in 'naked' function") + C3068 ("a 'naked' function cannot contain objects that would require unwinding") rejected an inline `std::vector` snapshot inside `__declspec(naked) midPopCell`. Factored to a non-naked file-scope static helper `dispatchDrawPhaseCallbacks(int)`. The naked function calls into the helper; the helper owns its own stack frame and exception-unwind metadata.
- **`depth_texture.cpp` storage choice (D-08 planner-discretion):** Even though `DepthTexture::addDepthResolveCallback` is a class-static member function of `DepthTexture::`, kept file-scope `static std::unordered_map<int, …>` storage instead of a per-instance map. Every existing call site treats this as a process-wide registry, not per-instance state — moving to per-instance would have changed observable behavior.
- **Wrapper-Drain in GameCallbacks/GroundSceneCallbacks/ObjectCallbacks:** Rather than fully delete the per-class `Drain` method, retained it as a one-line wrapper around `CallbackHelpers.Drain`. Reason: existing tests (`GroundSceneCallbacksTests.ClearAllQueues`, `Drain_EmptyQueue_DoesNothing`) reach `GroundSceneCallbacks.Drain(queue)` directly. The wrapper preserves the test surface without duplicating the body — the negative grep gate confirms no inline `while ... TryDequeue` body remains.
- **`Game::triggerInstallCallbacks` updated to use snapshot dispatch:** the existing test-only function (added in Phase 2 for `utinni_triggerInstallCallbacks`) was rewritten to use the same R-H snapshot pattern as `hkInstall`. This means the C-16 GameCallbacksTests now exercise the snapshot path too — kept passing post-rewrite, so no regression.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] `UtinniCoreDotNet/Callbacks/CallbackHelpers.cs` missing from csproj Compile list**
- **Found during:** Task 1 (managed build after creating CallbackHelpers.cs)
- **Issue:** UtinniCoreDotNet uses an old-style csproj (each `.cs` file is explicitly listed in `<Compile Include="…"/>`); SDK-style file globbing doesn't apply. The new `CallbackHelpers.cs` was therefore invisible to the compiler — `CS0103: The name 'CallbackHelpers' does not exist in the current context` from the three refactored call sites.
- **Fix:** Added `<Compile Include="Callbacks\CallbackHelpers.cs" />` to `UtinniCoreDotNet/UtinniCoreDotNet.csproj` next to the existing per-callback file entries.
- **Files modified:** `UtinniCoreDotNet/UtinniCoreDotNet.csproj`
- **Verification:** msbuild Release|x86 exits 0; `dotnet test` discovers and runs the new `CallbackHelpersTests` (4 Facts pass).
- **Committed in:** `b220e36` (Task 1 commit)

**2. [Rule 3 — Blocking] `shader.cpp` `__declspec(naked)` rejects local `std::vector`**
- **Found during:** Task 3b (UtinniCore build after applying the snapshot transform to all 11 native files)
- **Issue:** The plan called for an inline snapshot dispatch inside the naked function `utinni::shaderPrimitiveSorter::midPopCell`. MSVC rejects this with C2489 ("initialized auto or register variable not allowed at function scope in 'naked' function") and C3068 ("a 'naked' function cannot contain objects that would require unwinding if a C++ exception occurred") — naked functions have no compiler-generated prologue/epilogue and no exception-unwind frame, so std::vector's destructor cannot be safely called.
- **Fix:** Factored the snapshot dispatch into a non-naked file-scope static helper `static void dispatchDrawPhaseCallbacks(int phase)`. The naked function now calls into the helper with one line: `dispatchDrawPhaseCallbacks(phase);` — the helper owns its own stack frame and exception-unwind metadata. Documented in shader.cpp comments and the Task 3b commit message.
- **Files modified:** `UtinniCore/swg/graphics/shader.cpp`
- **Verification:** msbuild Release|x86 exits 0 after the fix.
- **Committed in:** `e4b2b59` (Task 3b commit)

**3. [Rule 2 — Missing Critical] `Game::getInstallSubscriberCount()` accessor added for test bridge**
- **Found during:** Task 3 (designing `utinni_test_installSubscriberCount`)
- **Issue:** The plan's `<interfaces>` block called for a `utinni_test_installSubscriberCount` C-linkage export, but `installCallbacks` is a translation-unit-private `static std::unordered_map` in `game.cpp` — there was no public way to query its size from outside the file. Returning the size directly from a C-linkage export inside `game.cpp` would have worked, but feels like a leak of the test concern into production code.
- **Fix:** Added a `static int Game::getInstallSubscriberCount()` member function (declared in `game.h`, defined in `game.cpp`) returning `static_cast<int>(installCallbacks.size())`. Documented as test-only in both the header and the cpp comment. The `utinni_test_installSubscriberCount` export wraps this accessor cleanly.
- **Files modified:** `UtinniCore/swg/game/game.cpp`, `UtinniCore/swg/game/game.h`
- **Verification:** msbuild Release|x86 exits 0; `NativeCallbacksHandleTests.LegacyAddInstallCallback_StillWorks` calls `InstallSubscriberCount()` to compute the delta and asserts +1 after `AddInstall`.
- **Committed in:** `5e81410` (Task 3a commit, with the rest of game.cpp's transform)

**4. [Rule 1 — Bug] `NativeCallbacksHandleTests.Subscribe_DuringDispatch` test pollution from static delegate fields**
- **Found during:** Task 3c (first run of `NativeCallbacksHandleTests` after applying the native R-A transform)
- **Issue:** Initial draft used class-level static `s_callback` / `s_innerCallback` fields (pinned via `GCHandle.Alloc`) to host the test delegates. Because the `LegacyAddInstallCallback_StillWorks` test deliberately cannot Unsubscribe (D-10 documented limitation), its delegate remains in `installCallbacks` for the test-class lifetime. When `Subscribe_DuringDispatch` then runs and reassigns `s_callback` to a new lambda, the OLD delegate still in the registry has captured the static `s_callCount`. So the next `DispatchInstall` fired BOTH the new (current-test) callback AND the stale one — both incrementing `s_callCount`. Assertion `Assert.Equal(1, s_callCount)` failed with actual `2`.
- **Fix:** Refactored to per-test-local delegates: each `[Fact]` creates its own `NativeCallback cb = () => callCount++;` closure capturing a local `int callCount = 0;`. The GCHandle pin still applies to the local instance. Stale delegates from prior tests now increment their OWN captured counter (which the next test never inspects), not the new test's local counter. Removed the unused static `s_callback` / `s_innerCallback` / `s_callCount` / `s_innerCallCount` fields entirely.
- **Files modified:** `UtinniCoreDotNet.Tests/NativeCallbacksHandleTests.cs`
- **Verification:** Full `dotnet test` run: 76/76 passing.
- **Committed in:** `ddda9f0` (Task 3c commit, before the commit was made — refactor happened during local TDD before commit)

---

**Total deviations:** 4 auto-fixed (2 blocking, 1 missing-critical, 1 bug)
**Impact on plan:** All four were necessary mechanical/contract issues discovered during execution that didn't change the plan's scope or success criteria. No scope creep — the same 4 tasks landed with the same observable contract.

## Issues Encountered

- **Worktree base mismatch at agent spawn:** The worktree was initialized at `b36265e` (a Phase 02 state-recording commit) but the plan's spawn header expected base `2523228` (the Phase 03-01 plan-creation commit). Used `git reset --hard 2523228` per the spawn header's recovery clause to align the worktree before any task work began. Documented in the worktree HEAD assertion output; no code impact.
- **CppSharp regenerated `Generated/UtinniCore.cs` on every UtinniCore build:** Each rebuild of the native side caused `UtinniCoreDotNetGen.exe` (post-build CppSharp generator) to regenerate the managed binding, which picked up the new `subscribe*`/`unsubscribe*` surface automatically. Resulting `+470 line` diff was committed alongside Task 3a, then a smaller diff with the remaining 7 files in Task 3b. This is expected per CON-T-01 + R-F's eventual auto-discovery scope. No manual edits required to the generated file.
- **Line-ending CRLF warnings on `git add`:** Windows worktree autocrlf converted LF to CRLF on commit for new test files. Pre-existing repo behavior; no action needed.

## User Setup Required

None — no external service configuration required. All work is internal to the framework's callback layer.

## Next Phase Readiness

- **Plan 03-02 (Lifecycle + RVAs: R-B + R-C) is unblocked** per D-02's CI-gated ordering. The new handle-based callback API gives R-B's `destroyPlugin` lifecycle a stable shape to ride on top of — a plugin can `Subscribe` callbacks on init, store the handles in its `Impl`, and call `Unsubscribe(handle)` from `destroyPlugin` to avoid the dangling-fn-ptr-on-unload class structurally.
- **No carry-overs** from this plan. IN-05 was the only Phase 02.1 carry-over and is closed.
- **`Add*` legacy wrappers preserved** per D-10 — UtinniPlugins/TJT can migrate to `Subscribe/Unsubscribe` opportunistically; no forced flag day. Phase 6 STAB-03 may revisit `[Obsolete]`-marking the legacy API once UtinniPlugins migrates.
- **`utinni_test_*` native bridge layer** established the pattern Plan 03-02's R-C test (`getSwgWndProc` P/Invoke harness) will copy for its single-export bridge.

## Self-Check: PASSED

- `UtinniCoreDotNet/Callbacks/CallbackHelpers.cs` — exists
- `UtinniCoreDotNet.Tests/CallbackHelpersTests.cs` — exists
- `UtinniCoreDotNet.Tests/CallbacksSubscribeUnsubscribeTests.cs` — exists
- `UtinniCoreDotNet.Tests/CallbacksSnapshotIterationTests.cs` — exists
- `UtinniCoreDotNet.Tests/NativeCallbacksHandleTests.cs` — exists
- Commit `b220e36` (Task 1) — present in git log
- Commit `2e1b61d` (Task 2) — present in git log
- Commit `5e81410` (Task 3a) — present in git log
- Commit `e4b2b59` (Task 3b) — present in git log
- Commit `ddda9f0` (Task 3c) — present in git log
- Commit `e40675e` (Task 4) — present in git log
- `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86` exits 0
- `dotnet test UtinniCoreDotNet.Tests`: 76 passed / 0 failed
- `Select-String -Pattern "std::unordered_map<int, "` across the 11 native files: 32 registries (≥32 expected)
- `docs/ai/assessment.md` shows R-A=done and R-H=done with commit SHAs

---
*Phase: 03-strategic-reworks-r-a-r-h*
*Completed: 2026-05-21*
