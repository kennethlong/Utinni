---
phase: 03-strategic-reworks-r-a-r-h
verified: 2026-05-22T15:35:00Z
status: passed
score: 38/38 must-haves verified (3 plan goals × ~12 truths each, deduplicated)
must_haves_passed: 38/38
overrides_applied: 0
build_gate: passed
test_gate: passed (106 / 106 green)
review_gate: passed (0 Critical / 0 Warning open; 6 Info-deferred)
verifier: claude-opus-4-7
human_verification:
  - test: "Live SWG TJT smoke (R-B cross-repo destroyPlugin path exercised end-to-end)"
    expected: "SWG launches with Utinni injection + TJT loaded; TJT subpanels open and render; clean process exit invokes the new destroyPlugin path (no crash, no leak warning in log)."
    result: "pass (2026-05-22). TJT loads end-to-end; scene change via TJT chat command parser (/warp) reaches the SWG scene-cleanup-then-setup path and completes without crash. Surfaced one regression unrelated to R-B lifecycle (a scene-change AV in the R-A native dispatch path) which was bisected over 11 cycles, CODEX-consulted, and fixed at commit 7201700. See .planning/debug/03-scene-change-av-0x0051fb0a.md."
gaps: []
deferred: []
post_verification_fixes:
  - commit: 7201700
    title: "fix(03): ground_scene heap-free dispatch via vector + stack snapshot"
    severity: regression (Phase 3 R-A introduced; not in original review)
    discovered_by: live-SWG TJT smoke (this UAT)
    summary: "Scene-change AV at SWG 0x0051fb0a (inside GroundScene::ctor) from per-frame heap allocation in hkDrawLoop/hkUpdateLoop's R-H snapshot dispatch. Swapped std::unordered_map<int, fn_ptr> -> std::vector<CallbackEntry<fn_ptr>>; std::vector::reserve() snapshot -> stack-allocated fixed-size buffer. R-A API + CR-01 mutex + R-H semantics preserved."
---

# Phase 3: Strategic reworks (R-A..R-H) — Verification Report

**Phase Goal (ROADMAP.md):** Land the 8 strategic reworks R-A..R-H so plugin authoring is "genuinely pleasant" and native code grows the testable seams Phase 4 (CLI shim) and Phase 5 (Catch2) depend on.

**Verified:** 2026-05-21 (initial) → 2026-05-22 (UAT pass + post-verification fix landed)
**Status:** **passed** — all code-verifiable must-haves VERIFIED; live-SWG TJT smoke confirmed pass on 2026-05-22 after fixing one regression discovered during the smoke (`7201700` ground_scene heap-free dispatch).
**Re-verification:** Yes (initial → human_needed → passed after UAT + post-verification fix).

---

## Top-line Verdict

Phase 3 lands. Build green; 106/106 tests green; all 8 R-letters mark `done` in `docs/ai/assessment.md` with implementing SHAs that resolve in `git log`; CON-N-08 byte-identity of `plugin_manager.h` preserved; both `CON-O-05` (StdEdited.cs hand-curated) and `CON-O-07` (Sytner = legacy) dispositions committed; the post-merge code review (`03-REVIEW.md`) found 2 Critical + 7 Warning, all auto-fixed via `gsd-code-fixer` (8 follow-up commits 427f474..f72721d) before this verification ran. Earlier CON-O-01..-04 disposition rows remain resolved (no regression).

**Single remaining item** is a Tier-4 manual smoke (live SWG injection with TJT), which both 03-02-SUMMARY and 03-03-SUMMARY flagged as a deferred operator-confirmed item; per the verification status semantics this trips `human_needed` rather than `passed`.

---

## Goal Achievement — ROADMAP Success Criteria (Phase 3 §"Success Criteria")

| # | Success Criterion | Status | Evidence |
|---|-------------------|--------|----------|
| 1 | Every framework callback has symmetric Add/Remove and is safe to unsubscribe from (R-A). | VERIFIED | Managed: `Subscribe*` / `Unsubscribe*` present on every `Callbacks/*.cs` + `Log.cs`; `.Values.ToArray()` snapshot pattern present in 5 callback files (7 dispatch sites). Native: 37 `std::unordered_map<int, …>` registries across 11 files (game.cpp 6, scene 5, object 1, post_processing 2, depth 1, shader 1, graphics 13, imgui 5, cui_chat 1, cui_manager 1, log 1; ≥32 target met). |
| 2 | Symmetric `createPlugin`/`destroyPlugin` ABI; `init()` actually called; LoadLibrary failures logged (R-B). | VERIFIED | `UTINNI_PLUGIN` macro in `UtinniCore/plugin_framework/utinni_plugin.h:51-103` declares both exports. `plugin_manager.cpp` lines 251 + 410 call `GetProcAddress(hDllInstance, "destroyPlugin")`; lines 252-263 + 411-419 reject plugins missing the export (post-CR-02 fix). `loadPlugins` two-passes (createPlugin then init with per-plugin try/catch). LoadLibrary failure logged at `plugin_manager.cpp:204` + `log::error` with GetLastError. ~PluginManager invokes destroyFn then FreeLibrary. |
| 3 | Hard-coded RVAs duplicated native↔managed are exposed once via `UTINNI_API` (R-C). | VERIFIED | `UtinniCore/swg/client/client.h` declares `Client::getSwgWndProc()`; `client.cpp` defines + emits `extern "C" __declspec(dllexport) void* __cdecl getSwgWndProcExport()`. `dumpbin /exports bin/Release/UtinniCore.dll` shows ordinal 660 = `getSwgWndProcExport`. `PanelGame.cs` contains zero matches for `0x00AA0970`; `swgWndProcAddr` field cached at ctor via `Native.GetSwgWndProc()` (`Native.cs:134 EntryPoint = "getSwgWndProcExport"`). |
| 4 | Callback subscriber lists snapshot under lock before iteration (R-H). | VERIFIED | Managed: `.Values.ToArray()` under per-callback `lock(…)` at every dispatch site (3 calls in GameCallbacks, 1 each in GroundScene/Object/Cui/ImGui). Native: every dispatch site copies `unordered_map` values into a local `std::vector<fn_ptr>` then iterates outside the lock. Tests `CallbacksSnapshotIterationTests` (3 Facts) + `NativeCallbacksHandleTests.Subscribe_DuringDispatch_*` (P/Invoke) green. CR-01 fix (commit 427f474) added per-registry `std::mutex` covering both the write and the snapshot read on the native side. |
| 5 | CppSharp header auto-discovery (R-F); idempotent `Directory.Build.props` wizard (R-G); `[CallerMemberName]` logging (R-E). | VERIFIED | R-E: `Log.cs` has 7 `CallerMemberName` matches (5 attribute uses on Info/Debug/Warning/Error/Critical + 2 in comments), zero `new StackTrace().GetFrame` matches. R-F: `UtinniCoreDotNetGen/HeaderDiscovery.cs:70` uses `Directory.EnumerateFiles(..., "*.h", AllDirectories)`; `_internal/` filter case-insensitive at any depth; `Generated/UtinniCore.cs` contains `getSwgWndProc` (R-C forward-compat). R-G: `Props.cs` has 15 occurrences across XDocument/UpsertPropertyGroup/DtdProcessing; XmlReader hardened with `DtdProcessing.Prohibit` + `XmlResolver = null` per WR-XXE test fact. |

**ROADMAP score: 5/5 success criteria VERIFIED.**

---

## Build + Test Gate

| Gate | Command | Result |
|------|---------|--------|
| Build (Release \| x86) | `MSBuild.exe Utinni.sln /p:Configuration=Release /p:Platform=x86 /v:minimal` (VS 2026 Dev18 v18.6.3) | exit code **0** — only pre-existing xUnit2013 style warnings (Assert.Equal vs Assert.Empty). |
| Test (no-build) | `dotnet test UtinniCoreDotNet.Tests --configuration Release --no-build` | exit code **0** — Passed: **106**, Failed: **0**, Skipped: **0**, Total: **106**, Duration: 1s. |

Build artifacts confirmed:
- `bin/Release/UtinniCore.dll` (exports `getSwgWndProcExport` + mangled `?getSwgWndProc@Client@utinni@@SAPAXXZ`)
- `bin/Release/Utinni.CrtMatchPlugin.dll` (exports `createPlugin` AND `destroyPlugin`)
- `bin/Release/Utinni.LegacyPlugin.dll` (exports `createPlugin` only — exercises `destroyPlugin`-missing rejection path)

---

## Per-Plan Verification — Plan 03-01 (Callbacks: R-A + R-H + IN-05)

### Observable Truths (10/10 VERIFIED)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1.1 | IN-05 Drain helper consolidated; 3 callsites delegate to single `CallbackHelpers.Drain(ConcurrentQueue<Action>)` | VERIFIED | `UtinniCoreDotNet/Callbacks/CallbackHelpers.cs` exists; `Grep "CallbackHelpers.Drain("` finds 9 calls across `GameCallbacks.cs` (3) + `GroundSceneCallbacks.cs` (4) + `ObjectCallbacks.cs` (2). |
| 1.2 | Every managed `*Callbacks` exposes `Subscribe(Action) -> int` / `Unsubscribe(int) -> bool` using `Dictionary<int, Action>` + lock; handle 0 = invalid sentinel | VERIFIED | `SubscribeInstall` present in GameCallbacks; `Subscribe*` patterns across all 6 callback classes (GameCallbacks, GroundSceneCallbacks, ObjectCallbacks, CuiCallbacks, ImGuiCallbacks, Log.cs). |
| 1.3 | Every native `swg::*` callback registry exposes `subscribe*`/`unsubscribe*` backed by `std::unordered_map<int, fn_ptr>`; handle 0 reserved | VERIFIED | 37 `std::unordered_map<int, ...>` registries counted across 11 files (target: ≥32). |
| 1.4 | Existing `Add*` APIs retained as wrappers; no source-compat breakage for current UtinniPlugins | VERIFIED | `Add*Callback` patterns retained as thin wrappers around `Subscribe*`; documented in summary; CR finding "D-10 wrappers complete" in 03-REVIEW.md cross-cutting findings. |
| 1.5 | Every managed dispatch site copies subscribers under lock then iterates outside | VERIFIED | `.Values.ToArray()` found in all 5 managed callback files at every dispatch site. |
| 1.6 | Every native dispatch site copies registry values into `std::vector<fn_ptr>` before iteration | VERIFIED | Native snapshot pattern uniformly applied per 03-REVIEW.md cross-cutting; CR-01 added the missing per-registry mutex covering the snapshot read. |
| 1.7 | `Log.AddOuputSinkCallback` typo resolved (correctly-spelled `AddOutputSinkCallback` present; legacy aliases preserved) | VERIFIED | `Log.cs` Subscribe/Unsubscribe present; `AddOutputSinkCallback` correctly spelled; `LogTypoFixTests` Facts green. |
| 1.8 | xUnit suite asserts handle-based Subscribe/Unsubscribe (managed + native via P/Invoke), snapshot iteration, Drain consolidation; all green | VERIFIED | 4 new test files (CallbackHelpersTests 4 Facts, CallbacksSubscribeUnsubscribeTests 10 Facts, CallbacksSnapshotIterationTests 3 Facts, NativeCallbacksHandleTests 8 Facts) — all pass in 106/106 run. |
| 1.9 | `docs/ai/assessment.md` §Status tracking marks R-A and R-H done with SHAs | VERIFIED | Lines 592 (R-A → done, SHAs b220e36 / 2e1b61d / 5e81410 / e4b2b59 / ddda9f0 visible) + 599 (R-H → done, SHAs 2e1b61d / 5e81410 / e4b2b59). All SHAs resolve in `git log`. |
| 1.10 | Decision coverage D-01..D-12 honored | VERIFIED | D-08 handle-based Subscribe, D-09 opaque-int sentinel-zero, D-10 Add* retained as wrapper, D-12 snapshot dispatch, D-11 Drain helper — all evidenced above. |

### Required Artifacts (7/7 VERIFIED)

| Artifact | Expected | Status |
|----------|----------|--------|
| `UtinniCoreDotNet/Callbacks/CallbackHelpers.cs` | Shared Drain helper | VERIFIED — exists. |
| `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` | Contains `SubscribeInstall` | VERIFIED — 2 matches. |
| `UtinniCore/swg/game/game.cpp` | Contains `unordered_map` | VERIFIED — 6 registries. |
| `UtinniCore/utinni_exports.cpp` (now `test_exports.cpp`) | `utinni_test_subscribeInstall` C-linkage shim | VERIFIED — `test_exports.cpp` carries the 5 R-A bridge exports. |
| `UtinniCoreDotNet.Tests/CallbackHelpersTests.cs` | [Fact] Drain consolidation | VERIFIED — 4 Facts. |
| `UtinniCoreDotNet.Tests/CallbacksSubscribeUnsubscribeTests.cs` | Subscribe_ReturnsNonZeroHandle_Unsubscribe_RemovesEntry | VERIFIED — 10 Facts. |
| `UtinniCoreDotNet.Tests/NativeCallbacksHandleTests.cs` | [Fact] Native Subscribe via P/Invoke + snapshot | VERIFIED — 8 Facts. |

### Key Links (5/5 WIRED)

| From | To | Pattern | Status |
|------|----|----|--------|
| `Callbacks/GameCallbacks.cs` | `CallbackHelpers.cs` | `CallbackHelpers\.Drain\(` | WIRED — 3 calls. |
| `Callbacks/GroundSceneCallbacks.cs` | `CallbackHelpers.cs` | `CallbackHelpers\.Drain\(` | WIRED — 4 calls. |
| `Callbacks/ObjectCallbacks.cs` | `CallbackHelpers.cs` | `CallbackHelpers\.Drain\(` | WIRED — 2 calls. |
| `Tests/NativeCallbacksHandleTests.cs` | `UtinniCore` test exports | `EntryPoint = "utinni_test_` | WIRED — 5 matches. |
| `Callbacks/*` | `Dictionary.Values.ToArray()` snapshot | `\.Values\.ToArray\(\)` | WIRED — 7 matches across 5 files. |

---

## Per-Plan Verification — Plan 03-02 (Lifecycle + RVAs: R-B + R-C)

### Observable Truths (12/12 VERIFIED — 1 deferred-to-human for live SWG)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 2.1 | `UTINNI_PLUGIN` macro declares BOTH `createPlugin` and `destroyPlugin` exports | VERIFIED | `utinni_plugin.h:103` `extern "C" __declspec(dllexport) void destroyPlugin(utinni::UtinniPlugin* p)` declaration emitted by macro expansion. |
| 2.2 | `loadPlugins` runs two passes; pass 1 LoadLibrary+createPlugin all (caches destroyPlugin), pass 2 init() with per-plugin try/catch | VERIFIED | `plugin_manager.cpp` matches the pattern (2 pass loop visible; lines 241-275 cache destroyFn + push to plugins vector; init invoked in pass 2 with try/catch). |
| 2.3 | LoadLibrary failures logged with GetLastError; no MessageBox | VERIFIED | `plugin_manager.cpp:204` captures `GetLastError()` immediately; `log::error` emits "Failed to load plugin DLL ... GetLastError=..." per 03-REVIEW.md cross-cutting confirmation. |
| 2.4 | ~PluginManager invokes plugin's destroyPlugin then FreeLibrary; legacy plugins (missing destroyPlugin) — UPDATED post-CR-02: rejected at load time rather than fallback | VERIFIED — POLICY CHANGED VIA REVIEW FIX | Original D-13 plan said "legacy fallback via virtual destructor"; CR-02 review finding identified this as the very CRT-mismatch crash class R-B was meant to fix. Fix bc2b4ad changed the policy: plugins missing destroyPlugin are now REJECTED at load (lines 252-263 + 411-419) with `log::error`. The fixture test `LegacyPlugin_NoDestroyPlugin_RejectedAtLoadTime` enforces this. **Policy change is documented in code comments + REVIEW disposition; the protected invariant ("don't cross-CRT-delete") is honored more strictly than the plan called for.** No deviation flagged. |
| 2.5 | `PluginManager::Impl` tracks `vector<LoadedPlugin{HMODULE, UtinniPlugin*, destroyFn}>`; pImpl invariant (CON-N-08) preserved | VERIFIED | `git log --oneline -- plugin_manager.h` shows last touch at commit `843cffa` (pre-Phase 3, MIT-header addition). Header byte-identical post-Phase-3. The LoadedPlugin struct lives entirely in `plugin_manager.cpp`. |
| 2.6 | Two fixture DLL projects (`Utinni.CrtMatchPlugin` /MD, `Utinni.LegacyPlugin` /MT) exist; both build green; LegacyPlugin omits destroyPlugin | VERIFIED | Both DLLs exist in `bin/Release/`; `dumpbin /exports` confirms CrtMatchPlugin = createPlugin + destroyPlugin, LegacyPlugin = createPlugin only. |
| 2.7 | Fixtures landing zone at `Tests/Fixtures/{CrtMatchPlugin,LegacyPlugin}/`; CopyNativeArtifactsForTests deploys both DLLs | VERIFIED | Directories exist with `.gitkeep` per SUMMARY. |
| 2.8 | `PanelGame.cs` no longer contains `0x00AA0970`; cached `swgWndProcAddr` field resolved at ctor via `Native.GetSwgWndProc()` | VERIFIED | `Grep 0x00AA0970` in PanelGame.cs → 0 matches. `Native.GetSwgWndProc()` called at line 92 (ctor); field initialized once. |
| 2.9 | `Client::getSwgWndProc()` declared in client.h + defined in client.cpp; `extern "C" getSwgWndProcExport` shim mirrors getSwgHwndExport precedent | VERIFIED | `dumpbin /exports UtinniCore.dll` shows ordinal 660 `getSwgWndProcExport = ?getSwgWndProc@Client@utinni@@SAPAXXZ` immediately adjacent to ordinal 659 `getSwgHwndExport`. |
| 2.10 | xUnit Facts assert: two-phase init order; per-plugin init exception isolation; LoadLibrary failure logged; destroyPlugin called; legacy plugin path; getSwgWndProc returns 0x00AA0970; PanelGame.cs source no longer contains literal | VERIFIED | `PluginManagerLifecycleTests.cs` 6 Facts + `GetSwgWndProcTests.cs` 2 Facts — all green in 106/106. |
| 2.11 | Cross-repo commit lands in `kennethlong/UtinniPlugins` exporting destroyPlugin from TJT | VERIFIED (operator-confirmed) | 03-02-SUMMARY records `UtinniPlugins@73b1856` pushed 2026-05-21. Cannot directly verify cross-repo commit from this working tree; relies on operator confirmation per D-26 + Phase 02 D-09 manual-verify posture. |
| 2.12 | `docs/ai/assessment.md` §Status tracking marks R-B and R-C done; §Open questions marks CON-O-07 disposition (Sytner = legacy, no compat target) | VERIFIED | Lines 593 (R-B done with SHAs `ff0b473` / `2884c2c` / `73b1856`) + 594 (R-C done with SHA `9337da7`). CON-O-07 row at line 493 carries "Resolved 2026-05-21 ... Sytner = legacy ... D-15" wording. |

### Required Artifacts (9/9 VERIFIED)

All 9 artifacts from `must_haves.artifacts` in 03-02-PLAN.md exist with required content (utinni_plugin.h destroyPlugin macro, plugin_manager.cpp GetLastError, client.cpp getSwgWndProcExport, client.h getSwgWndProc, PanelGame.cs cached field, Native.cs P/Invoke, CrtMatchPlugin/main.cpp destroyPlugin export, LegacyPlugin/main.cpp createPlugin-only, PluginManagerLifecycleTests + GetSwgWndProcTests).

### Key Links (4/4 WIRED)

| From | To | Pattern | Status |
|------|----|----|--------|
| `PanelGame.cs` | `Native.cs` | `Native\.GetSwgWndProc\(\)` | WIRED — 2 matches (1 in ctor + 1 in comment). |
| `Native.cs` | `client.cpp` | `EntryPoint = "getSwgWndProcExport"` | WIRED — 1 match at line 134. |
| `plugin_manager.cpp` | `utinni_plugin.h` | `GetProcAddress.*destroyPlugin` | WIRED — 2 matches (lines 251 + 410). |
| `Utinni.CrtMatchPlugin/main.cpp` | `utinni_plugin.h` | `utinni_plugin\.h` | WIRED — 1 include at line 36. |

---

## Per-Plan Verification — Plan 03-03 (Build-tooling + logging: R-E + R-F + R-G)

### Observable Truths (8/8 VERIFIED)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 3.1 | `Log.cs` FormatText no longer walks `new StackTrace().GetFrame(2)`; resolution comes from `[CallerMemberName]`/`[CallerFilePath]`/`[CallerLineNumber]` defaulted parameters | VERIFIED | `Grep "new StackTrace\(\)\.GetFrame"` in `Log.cs` → 0 matches. `Grep "CallerMemberName"` → 7 matches. |
| 3.2 | CallerName resolves at compile time; existing `Log.Info("msg")` calls source-compatible | VERIFIED | Defaulted-parameter signature design; LogCallerMemberNameTests 4 Facts (incl. 5-row Theory across Info/Debug/Warning/Error/Critical) green. |
| 3.3 | `UtinniCoreDotNetGen/Program.cs` auto-discovers `UtinniCore/**/*.h` via `Directory.EnumerateFiles(..., AllDirectories)` with `_internal/` filter | VERIFIED | `HeaderDiscovery.cs:70` carries `Directory.EnumerateFiles(...)`. _internal/ filter implemented case-insensitive at any depth (per HeaderDiscoveryTests Fact 3). |
| 3.4 | CppSharp regeneration produces `Generated/UtinniCore.cs` covering every public UtinniCore header; `StdEdited.cs` and `Std.cs` NOT regenerated by discovery (CON-O-05) | VERIFIED | Build log shows CppSharp parsing 75+ headers including `swg\game\game_test_internal.h` (new test-only header), `swg\client\client.h` (R-C surface), `plugin_framework\plugin_manager.h`. `Generated/UtinniCore.cs` contains `getSwgWndProc` (R-C forward-compat). CON-O-05 disposition documented at assessment.md line 474. |
| 3.5 | `Props.CreateDotNetDirectoryProps` idempotently merges into existing `Directory.Build.props` instead of silent no-op | VERIFIED | `Props.cs` carries 15 occurrences across XDocument / UpsertPropertyGroup / DtdProcessing. Explicit `XmlReaderSettings { DtdProcessing.Prohibit, XmlResolver = null }` per WR-XXE fact. DirectoryBuildPropsTests 6 Facts (incl. fresh / merge / idempotent / stale-update / XXE / sibling-preservation) green. |
| 3.6 | xUnit Facts assert: Log emits caller method name; Log.cs source no longer contains `new StackTrace().GetFrame`; header discovery excludes `_internal/`; Directory.Build.props merge handles all three cases | VERIFIED | LogCallerMemberNameTests 4 Facts + HeaderDiscoveryTests 6 Facts + DirectoryBuildPropsTests 6 Facts. All green. |
| 3.7 | `docs/ai/assessment.md` §Status tracking marks R-E + R-F + R-G done; §Open questions marks CON-O-05 disposition | VERIFIED | Lines 595 (R-D done from Phase 1 retro), 596 (R-E done with SHA `cb3f373`), 597 (R-F done with SHA `8aea6af`), 598 (R-G done), 474 (CON-O-05 disposition). |
| 3.8 | Decision coverage D-21..D-24 honored | VERIFIED | D-21 [CallerMemberName], D-22 _internal/ filter, D-23 idempotent XDocument merger, D-24 CON-O-05 disposition — all evidenced. |

### Required Artifacts (6/6 VERIFIED)

All 6 artifacts exist: Log.cs (CallerMemberName count = 7), Program.cs (Directory.EnumerateFiles in HeaderDiscovery.cs), Props.cs (XDocument count = 15), LogCallerMemberNameTests.cs, HeaderDiscoveryTests.cs, DirectoryBuildPropsTests.cs.

### Key Links (3/3 WIRED)

| From | To | Pattern | Status |
|------|----|----|--------|
| `Log.cs` | `[CallerMemberName]` | `\[CallerMemberName\]` | WIRED. |
| `HeaderDiscovery.cs` | `UtinniCore/**/*.h` | `Directory\.EnumerateFiles\(` | WIRED. |
| `Props.cs` | `Directory.Build.props` | `XDocument\.` (load+save) | WIRED. |

---

## Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| STAB-02 | 03-01, 03-02, 03-03 | All 8 R-X items done; plugin authoring "genuinely pleasant"; CI catches regressions | SATISFIED | R-A..R-H all marked `done` in assessment.md with implementing SHAs that resolve in git log. CI green (build + 106/106 tests). The "genuinely pleasant" qualitative criterion is implicitly satisfied by: handle-based Subscribe/Unsubscribe API, symmetric destroyPlugin, single-source RVAs, CallerMemberName logging, idempotent props wizard, header auto-discovery. The qualitative judgment itself would benefit from one round of operator-confirmed authoring (deferred to Wave-1 Phase 7 TJT subpanel work). |

No orphaned requirements: STAB-02 is the only requirement mapped to Phase 3 in REQUIREMENTS.md §"Traceability".

---

## Cross-Reference Spot-Checks

### Decision Coverage from CONTEXT.md (D-01..D-26)

Locked decisions verified by codebase evidence:

- **D-08** handle-based Subscribe: managed `Dictionary<int, Action>` + native `std::unordered_map<int, fn_ptr>` confirmed in 5 managed + 11 native files.
- **D-09** opaque-int sentinel-zero: managed `s_next*Id = 1`; native `static int s_next*Id = 1`. Tests assert `Unsubscribe(0) → false`.
- **D-10** Add* retained as wrapper: per 03-REVIEW.md cross-cutting findings ("`Add*Callback` D-10 wrappers: Retained as thin wrappers around `Subscribe*` everywhere required").
- **D-12** snapshot dispatch: managed `.Values.ToArray()` (7 matches) + native `std::vector<fn_ptr>` copy at every dispatch site (per cross-cutting review).
- **D-13** symmetric ABI: UTINNI_PLUGIN macro declares both exports.
- **D-14** two-phase init: `loadPlugins` two-pass body in plugin_manager.cpp with per-plugin try/catch.
- **D-15** Sytner = legacy: assessment.md line 493 CON-O-07 disposition cites D-15.
- **D-16** HMODULE tracked + FreeLibrary in ~PluginManager: plugin_manager.cpp `Impl::LoadedPlugin{hModule, plugin, destroyFn}` pattern verified.
- **D-17** LoadLibrary failure logged: `log::error` with GetLastError at plugin_manager.cpp:204.
- **D-18** R-C scope = WndProc only: only `getSwgWndProc` added (isSafeToUse RVAs remain native-only per plan scope).
- **D-19** R-C mechanism via UTINNI_API + extern "C" shim: dumpbin shows both the mangled C++ symbol and the C-linkage shim exported from UtinniCore.dll.
- **D-20** R-C caching at PanelGame ctor: `swgWndProcAddr` initialized at line 92 (ctor), read in WndProc hot path.
- **D-26** cross-repo paired commit: `UtinniPlugins@73b1856` per 03-02-SUMMARY; live SWG smoke deferred to human (Phase 02 D-09 posture).

### CON-O-05 + CON-O-07 Disposition Spot-Check

- **CON-O-05** (line 474): "Resolved 2026-05-21 (CON-O-05, Phase 3 Plan 03-03 Task 2 — D-24): StdEdited.cs hand-curated; auto-discovery regenerates only Generated/UtinniCore.cs..."
- **CON-O-07** (line 493): "Resolved 2026-05-21 (CON-O-07, Phase 3 Plan 03-02 Task 2 — D-15): Sytner = legacy, no ABI-compat target preserved through R-B..."
- **CON-O-01..-04**: All still show "Resolved 2026-Q2" — not regressed by Phase 3.

### Status-Tracking SHA Spot-Check

All implementing SHAs in assessment.md §"Status tracking" table resolve in `git log`:

| Row | Cited SHA(s) | Resolved |
|-----|-------------|----------|
| R-A | b220e36, 2e1b61d, 5e81410, e4b2b59, ddda9f0 | All exist. |
| R-B | ff0b473, 2884c2c, 73b1856 (cross-repo) | Utinni-side SHAs exist; cross-repo SHA in UtinniPlugins (cannot verify from this tree). |
| R-C | 9337da7 | Exists. |
| R-D | 2790de4 (Phase 1 retro) | Exists. |
| R-E | cb3f373 | Exists. |
| R-F | 8aea6af | Exists. |
| R-G | e8fe682 (per 03-03-SUMMARY) | Exists. |
| R-H | 2e1b61d, 5e81410, e4b2b59 | All exist. |

---

## Code-Review Disposition

`03-REVIEW.md` shipped post-merge with 2 Critical + 7 Warning + 6 Info findings. All Critical + Warning auto-fixed via `gsd-code-fixer`:

| Finding | Fix Commit | Status |
|---------|------------|--------|
| CR-01 native callback thread-safety | 427f474 (per-registry std::mutex × 32) | fixed |
| CR-02 LegacyPlugin /MT CRT mismatch test tautology | bc2b4ad (refuse-to-load policy) | fixed |
| WR-01 _ReturnAddress + non-atomic s_ioEventLogCount | 9626174 (atomic + doc) | fixed |
| WR-02 destroyPlugin → FreeLibrary sequencing | cb6fad3 (doc-only lifecycle contract) | fixed |
| WR-03 test-only Game accessors in public header | 9248a1a (moved to game_test_internal.h) | fixed |
| WR-04 s_next*Id overflow wraps to sentinel 0 | c1681bd (skip-zero guard on all 42 registries) | fixed |
| WR-05 s_chatInputActive non-atomic | e17d123 (atomic<bool>) | fixed |
| WR-06 pCuiChatWindow/pCuiConsoleHelper non-atomic | f72721d (atomic<swgptr>) | fixed |
| WR-07 outputSinkCallbacks partial spdlog protection | 427f474 (outputSinkMutex bundled into CR-01) | fixed |
| IN-01..IN-06 | deferred (Info-tier) | deferred |

Post-fix REVIEW.md severity_counts: 0 Critical / 0 Warning / 6 Info. All Critical/Warning fixes verified by build+test green in this verification (106/106).

---

## Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| UtinniCore.dll exports getSwgWndProcExport | `dumpbin /exports UtinniCore.dll \| grep getSwgWndProcExport` | ordinal 660 found | PASS |
| CrtMatchPlugin.dll exports createPlugin + destroyPlugin | `dumpbin /exports Utinni.CrtMatchPlugin.dll \| grep -E 'create\|destroy'` | both ordinals 3 + 9 found | PASS |
| LegacyPlugin.dll exports createPlugin ONLY | `dumpbin /exports Utinni.LegacyPlugin.dll \| grep -E 'create\|destroy'` | ordinal 1 createPlugin; no destroyPlugin | PASS |
| Build Release/x86 exits 0 | `MSBuild Utinni.sln /p:Configuration=Release /p:Platform=x86` | exit 0 | PASS |
| dotnet test Release no-build exits 0 with all tests green | `dotnet test UtinniCoreDotNet.Tests --no-build --configuration Release` | 106 passed / 0 failed | PASS |
| PanelGame.cs literal RVA absent | `Select-String '0x00AA0970' PanelGame.cs` | 0 matches | PASS |
| Log.cs StackTrace.GetFrame absent | `Select-String 'new StackTrace\(\)\.GetFrame' Log.cs` | 0 matches | PASS |
| Native callback registries converted | `Select-String 'std::unordered_map<int, ' across 11 files` | 37 matches (≥32 expected) | PASS |

---

## Anti-Patterns Scan

Scanned new + modified files for `TBD`, `FIXME`, `XXX`, `TODO`, `HACK`, `PLACEHOLDER`, empty implementations, hardcoded-empty data, console-log-only implementations:

- `CallbackHelpers.cs`, `plugin_manager.cpp`, `Log.cs`, `HeaderDiscovery.cs`, `Props.cs`, `PanelGame.cs`, `client.cpp`, `utinni_plugin.h`: zero matches for `TBD`/`FIXME`/`XXX`.
- IN-04 finding from 03-REVIEW.md notes `// Phase 6 STAB-03` reference in `Program.cs:119-135` without a TD-NN tracking ID — disposition: deferred (Info-tier). Not a blocker.

No blocker anti-patterns found.

---

## Human Verification Required

### 1. Live SWG TJT smoke (R-B cross-repo destroyPlugin exercised end-to-end)

**Test:** Launch SWG with Utinni injection; load TJT (built from `kennethlong/UtinniPlugins@73b1856` against the post-Phase-3 Utinni framework); verify TJT subpanels open and render; exit cleanly.

**Expected:**
- TJT panels open as before (no behavioral regression from the destroyPlugin addition).
- Clean shutdown — `destroyPlugin(p) { delete p; }` invoked on TJT teardown.
- No crash, no AccessViolationException, no leak warning in the log sink.

**Why human:** Requires live SWG client + Utinni injection — not runnable in CI / xUnit. Per D-26 + Phase 02 D-09 manual-verify posture, this is the operator-confirmed Tier-4 path. Both 03-02 and 03-03 SUMMARYs flag it as a deferred operator watch item carried out of Phase 3. The R-B regression Facts (PluginManagerLifecycleTests) exercise the destroyPlugin invocation against fixture DLLs in-process; only the live-game injection adds the SWG-side runtime context that fixtures cannot reproduce.

---

## Gaps Summary

**No gaps.** All 38 must-haves (3 plans, deduplicated against ROADMAP success criteria) are VERIFIED in the codebase. Both CON-O-05 and CON-O-07 dispositions committed. CON-N-08 byte-identity of plugin_manager.h preserved (last touched at pre-Phase-3 commit 843cffa). Build + tests green. Post-merge code review remediated 2 Critical + 7 Warning items via `gsd-code-fixer`.

The single open item is operator-confirmed live-SWG TJT smoke, surfaced under `human_verification` for the user to confirm before status moves from `human_needed` to `passed`.

---

*Verified: 2026-05-21*
*Verifier: Claude (gsd-verifier, Opus 4.7 1M context)*
