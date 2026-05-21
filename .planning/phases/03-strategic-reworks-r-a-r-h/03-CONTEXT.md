# Phase 3: Strategic reworks (R-A..R-H) - Context

**Gathered:** 2026-05-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Land the 7 remaining strategic reworks from `docs/ai/assessment.md` (R-A, R-B, R-C, R-E, R-F, R-G, R-H — R-D = CI was closed by Phase 1). Mechanical refactors that produce testable seams plugin authors will actually depend on. Phase exists so Wave-1 subpanels (Phases 7-11) land on a framework where plugin authoring is "genuinely pleasant" per assessment.md — not the current state where `init()` is silently never called, callbacks have no `Remove`, RVAs are duplicated across the boundary, and the VSIX template wizard silently destroys existing `Directory.Build.props` files. Also resolves CON-O-05 (`StdEdited.cs` curation criteria, bears on R-F) and CON-O-07 (Sytner's plugin status, bears on R-B ABI-compat target).

Phase 3 produces 3 plans by category:

- **Plan 03-01 — Callbacks: R-A + R-H + IN-05 Drain helper.** Native `swg::*` callback registries get handle-based `Subscribe/Unsubscribe` (~12 files mechanically); managed `Callbacks/*.cs` get symmetric `Remove*` for every `Add*` plus snapshot-iteration over `SynchronizedCollection<Action>`; IN-05's `Drain(ConcurrentQueue<Action>)` helper lands as the first task and consolidates 3 callsites.
- **Plan 03-02 — Plugin lifecycle + RVAs: R-B + R-C.** `UtinniPlugin` ABI grows symmetric `destroyPlugin`; `PluginManager::loadPlugins` two-passes (createPlugin all-then-init all); `LoadLibrary` failures logged; HMODULE tracked + `FreeLibrary` in `~PluginManager`. R-C lifts the lone duplicated RVA (`0x00AA0970` WndProc) into a `UTINNI_API` getter consumed via P/Invoke from `PanelGame`.
- **Plan 03-03 — Build-tooling + logging: R-E + R-F + R-G.** `[CallerMemberName]`/`CallerFilePath`/`CallerLineNumber` replaces `Log.FormatText`'s `StackTrace.GetFrame` walk; CppSharp generator globs `UtinniCore/**/*.h` with an `_internal/` blocklist; `Props.CreateDotNetDirectoryProps` idempotently merges into an existing `Directory.Build.props` rather than silently no-oping.

**In scope (this phase):**
- All 7 R-items above with their R-N specific success criteria (PROJECT.md §STAB-02)
- Per-rework regression test that would fail before the fix (max-harness posture)
- IN-05 (`Drain` helper) — Phase 02.1 carry-over that lives in R-A scope
- CON-O-05 disposition: `StdEdited.cs` is the only hand-curated `Generated/` file; R-F's auto-discovery does NOT regenerate it. Documented in the R-F task.
- CON-O-07 disposition: Sytner's plugin treated as legacy (dormant upstream); no ABI-compat target preserved through R-B.
- Cross-repo touch for R-B: paired commits in `kennethlong/UtinniPlugins` to migrate TJT to the new `createPlugin` + `destroyPlugin` symmetric ABI (no UtinniPlugins CI yet — manual TJT build verifies, Phase 02 D-09 precedent).
- Update `docs/ai/assessment.md` to record disposition of each R-X closed here.
- Code-review at phase end (`/gsd:code-review 03`) to confirm no new critical findings.

**Out of scope (this phase):**
- **R-D** (CI workflow) — already closed by Phase 1.
- **The two `isSafeToUse` RVAs (`0x01908858`, `0x01919410`)** — referenced only in `game.cpp:361`, not duplicated to managed; R-C scope intentionally narrowed to the actual duplicate (WndProc). If a future phase introduces a managed reader, R-C's pattern is the template.
- **~25 detoured RVAs in `swg/*.cpp`** — none are duplicated across the native↔managed boundary today; R-C targets actual duplication, not hypothetical future duplication.
- **Plugin hot-reload** — destroyPlugin + HMODULE tracking enables it structurally but actually exercising the flow is V2.
- **CON-O-08 (DXSDK vs Windows 10 SDK replaceability)** — Phase 6 STAB-03.
- **CON-O-06 (LeksysINI replacement)** — Phase 6 STAB-03.
- **The ~30 cleanups** (TD-25 empty stubs, TD-26 disabled hooks, TD-27 hardcoded font path, TD-28 `TJT.ico` framework default leak) — Phase 6 STAB-03.
- **Phase 4 CLI shim + golden fixtures** (TEST-03) — Phase 4.
- **Phase 5 C++ Catch2 tests** (TEST-02) — Phase 5, depends on R-A..R-H seams landing here.
- **UtinniPlugins CI bootstrap** — Phase 7+ or earlier dedicated phase; this phase keeps Phase 02 D-09 manual-verify posture.
- **Mark `Add*` as `[Obsolete]`** — keep them as wrappers around `Subscribe` (no warning noise); revisit in Phase 6 or V2 if/when UtinniPlugins migrates.

</domain>

<decisions>
## Implementation Decisions

### Plan Structuring (3 plans by category)

- **D-01:** Plans grouped by **category/risk-class**, not by R-letter or layer. Plan 03-01 (Callbacks: R-A + R-H + IN-05), Plan 03-02 (Lifecycle + RVAs: R-B + R-C), Plan 03-03 (Build-tooling + logging: R-E + R-F + R-G). Categories share dispositional decisions (e.g., the Subscribe/Unsubscribe shape from R-A informs R-H's snapshot mechanism; R-B's destroyPlugin shape informs the CRT discipline R-F's auto-discovery must respect on internal headers).
- **D-02:** **CI-gated ordering: 03-01 → 03-02 → 03-03.** Lowest blast-radius first (R-A/R-H are mechanical refactors over greppable patterns; verifying CI green before the lifecycle+RVA work isolates regressions). R-B's plugin lifecycle ride on top of stable callback shape. Build-tooling last because R-F header auto-discovery could surface API gaps from earlier changes (and now wires the new R-B+R-C `UTINNI_API` symbols into CppSharp output cleanly).
- **D-03:** **Each R-X = one or more atomic commits**, GSD executor default. ~10-14 fix commits total across the three plans plus harness/test commits where they don't fold into the fix commit.
- **D-04:** **CI gate at every plan boundary** — same `windows-2022` workflow as Phase 02/02.1. Plan 03-02 doesn't start until 03-01 is green on `master`; same for 03-03.

### Verification Posture (max-harness with documented carve-outs)

- **D-05:** **Max-harness posture preserved from Phase 02 D-05 and Phase 02.1 D-04.** Every R-X gets a regression test that would fail before the fix. Per-R-X harness shape:
  - **R-A (callbacks symmetric Add/Remove + Subscribe/Unsubscribe):** xUnit asserts `Subscribe(fn) -> int handle != 0`; `Unsubscribe(handle)` removes the entry; iterating after unsubscribe doesn't invoke `fn`. Cross-layer parallel test on native side via P/Invoke into a thin test wrapper around one `swg::game` callback registry.
  - **R-H (snapshot iteration):** xUnit registers a callback that itself calls `Subscribe(another_fn)` during its own dispatch; assert no `InvalidOperationException` and that `another_fn` is NOT invoked in the current iteration but IS in the next. Tests both managed `SynchronizedCollection<Action>` and (via P/Invoke) one native `std::vector<void(*)()>` site.
  - **R-B (plugin lifecycle):** `/Fixtures/CrtMatchPlugin/` (with proper `createPlugin` + `destroyPlugin` exports) and `/Fixtures/LegacyPlugin/` (only `createPlugin`). xUnit asserts: (a) loader calls `init()` on both after all createPlugin returns; (b) destroyPlugin export is called on shutdown when present, falls back to virtual destructor when absent; (c) `LoadLibrary` failure on a deliberately-broken fixture is logged with `GetLastError`. Closes CON-O-07 disposition in the same commit.
  - **R-C (single-source WndProc RVA):** Two-part. (i) grep test asserts the literal `0x00AA0970` no longer appears in `UtinniCoreDotNet/UI/Controls/PanelGame.cs`. (ii) P/Invoke test loads `UtinniCore.dll`, calls `getSwgWndProc()`, asserts return value equals `0x00AA0970`. Covers "no longer duplicated" AND "getter works at runtime."
  - **R-E (`[CallerMemberName]` logging):** xUnit calls `Log.Info("test")` from a known method; asserts the emitted message contains the calling method name when class-name prefix is on. Negative test: `StackTrace.GetFrame` is gone (grep test).
  - **R-F (CppSharp header auto-discovery):** unit test on the header-discovery function with a fixture directory tree (`fixture/UtinniCore/{public.h, _internal/private.h, sub/public2.h}`) asserts the discovered set is `{public.h, sub/public2.h}` and excludes `_internal/private.h`. Documents CON-O-05 disposition (`StdEdited.cs` remains hand-curated; not regenerated by discovery).
  - **R-G (`Directory.Build.props` idempotent merge):** unit test calls `Props.CreateDotNetDirectoryProps` against a fixture solution dir with (a) no existing props (creates fresh), (b) existing props missing Utinni properties (merges them in), (c) existing props with Utinni properties already present (no-op). All three end states verified by parsing the resulting XML.
- **D-06:** **Documented carve-out — no automated test for the live-SWG side of R-C.** The `PanelGame.WndProc` actually-calls-into-SWG path is Tier-4 manual (`Verified-by: live SWG window forwards WM_KEYDOWN end-to-end` in the commit body). The getter unit-test from D-05's R-C item covers everything testable without live SWG.
- **D-07:** **Test home: `UtinniCoreDotNet.Tests`** absorbs all managed-side regression tests plus P/Invoke harnesses (same as Phase 02 D-07). New `/Fixtures/CrtMatchPlugin/` and `/Fixtures/LegacyPlugin/` subdirs for R-B fixtures. A small `Utinni.CrtMatchPlugin` and `Utinni.LegacyPlugin` solution-root project pair for the fixture DLLs (precedent: Phase 02 `Utinni.LoaderLockHarness` sibling-project pattern; Phase 02.1 `Utinni.CrossCrtFreeFixture` precedent for separate-CRT fixtures).

### R-A Depth (callback symmetry)

- **D-08:** **Handle-based `Subscribe(fn) -> int id` / `Unsubscribe(int id)`** is the R-A target depth — not mechanical Add/Remove. Solves TD-15's dangling-fn-ptr-on-plugin-unload class structurally. Touches ~12 native files + 3-5 managed files; mechanical pattern is identical per call site. Lines up with R-B's `destroyPlugin` lifecycle.
- **D-09:** **Opaque int handle.** Simplest ABI, P/Invoke-friendly, matches assessment.md literal suggestion. Native side uses a monotonic `int next_id = 1; std::unordered_map<int, fn_ptr> registry;` per callback registry. Managed side mirrors with `Dictionary<int, Action>`. Handle `0` is reserved as the invalid/sentinel value.
- **D-10:** **`Add*` retained as wrapper around `Subscribe`; no `Remove*` added to the old API.** Internal call delegates to `Subscribe`, return value discarded. Source-compat preserved for existing UtinniPlugins (TJT, Sytner) — they keep working without recompile. New `Subscribe`/`Unsubscribe` is the primary API for new code (R-B's destroyPlugin path uses it). Migration is opt-in per plugin.
- **D-11:** **IN-05 `Drain(ConcurrentQueue<Action>)` helper lands as the FIRST task of Plan 03-01.** Factor the helper into `UtinniCoreDotNet/Callbacks/CallbackHelpers.cs` (or similar; planner names final), refactor 3 callsites (`GameCallbacks.cs`, `GroundSceneCallbacks.cs`, `ObjectCallbacks.cs`). Lands before the symmetric-Remove work so new Remove paths reuse the helper if they touch queues. Closes Phase 02.1 carry-over.
- **D-12:** **R-H snapshot mechanism (paired with D-08):**
  - **Managed:** `var snapshot = subscribers.ToArray(); foreach (var fn in snapshot) fn();` under `SyncRoot.lock` only during the `.ToArray()` copy. Subscribe-during-dispatch goes to the registry; not visible until next iteration.
  - **Native:** `auto snapshot = std::vector<fn_ptr>(registry.begin(), registry.end()); for (auto fn : snapshot) fn();` (or `std::vector` copy via the registry's value view). Same semantics.

### R-B Plugin Lifecycle ABI

- **D-13:** **Symmetric exported `destroyPlugin(UtinniPlugin*)`** alongside existing `createPlugin()`. Plugin owns both alloc and free in its own CRT — eliminates the cross-CRT crash class (CON-B-04 territory). The `UTINNI_PLUGIN` macro extends to define both:
  ```cpp
  #define UTINNI_PLUGIN \
    extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin(); \
    extern "C" __declspec(dllexport) void destroyPlugin(utinni::UtinniPlugin* p)
  ```
  Existing plugins (Sytner) compile-broken-at-link until they add the export — acceptable per D-15.
- **D-14:** **Two-phase init: all `createPlugin()` first, then all `init()`.** `PluginManager::loadPlugins` collects plugins in pass 1; pass 2 iterates the populated list and calls `plugin->init()` on each. Order = load-order from `[Plugins]` cfg. Lets a plugin's `init()` look up sibling plugins via `PluginManager` (matches typical MEF two-phase composition pattern). Per-plugin try/catch around `init()` so one throwing plugin doesn't kill the rest (Phase 02 C-06 isolation precedent extended to init phase).
- **D-15:** **CON-O-07 disposition: Sytner's plugin treated as legacy / no compat target.** Upstream `ptklatt/UtinniPlugins` (which contained Sytner's plugin) is dormant per `project_fork_strategy` memory. Our `kennethlong/UtinniPlugins` doesn't actively ship it. R-B free to break Sytner's ABI without a fallback path. Documented in `docs/ai/assessment.md` §"Open questions" CON-O-07 disposition update commit.
- **D-16:** **HMODULE tracked + `FreeLibrary` in `~PluginManager`.** Extend `PluginManager::Impl` from `vector<UtinniPlugin*>` to `vector<{HMODULE hModule, UtinniPlugin* plugin}>` (or paired vectors). Shutdown order: `destroyPlugin(plugin)` (in plugin's CRT) → `FreeLibrary(hModule)` (host's `FreeLibrary` call is fine — DLL ref-count, not allocation). Enables future hot-reload structurally even if V1 doesn't exercise it.
- **D-17:** **`LoadLibrary` failure surfaces visibly.** Add the missing `else` after line 135 of `plugin_manager.cpp`: `log::error("Failed to load plugin DLL <path>: GetLastError=<code>")`. No MessageBox (would block startup); log + continue, same disposition as Phase 02 C-06's per-plugin try/catch.

### R-C Single-Source RVAs

- **D-18:** **R-C scope: just the actual duplicate (WndProc).** Surface `0x00AA0970` once via `Client::getSwgWndProc()` exported in `UTINNI_API` from `UtinniCore/swg/client/client.h`. The two `isSafeToUse` RVAs stay native-only — they're not duplicated to managed. Future RVA duplications get the same treatment as they emerge; don't pre-architect for the hypothetical.
- **D-19:** **Mechanism: `UTINNI_API IntPtr getSwgWndProc()`** in the `Client::` namespace. CppSharp auto-projects it (R-F's auto-discovery from Plan 03-03 picks it up automatically; if Plan 03-02 lands first, `client.h` is in the explicit allowlist already — add it). Managed side accesses via `UtinniCore.Utinni.Client.GetSwgWndProc()`.
- **D-20:** **Resolution timing: once in `PanelGame.Initialize`/ctor, cached as field.** Read at panel-construction time, store in `IntPtr swgWndProc` field (matches today's shape — just the literal `new IntPtr(0x00AA0970)` becomes a P/Invoke call). `PanelGame.WndProc` reads from cache in the hot path — zero per-message overhead.

### R-E / R-F / R-G (planner-discretion within sensible defaults)

- **D-21 (R-E):** Replace `Log.FormatText`'s `new StackTrace().GetFrame(2).GetMethod()` with `[CallerMemberName] string callerName = ""`, `[CallerFilePath] string callerFile = ""`, `[CallerLineNumber] int callerLine = 0` parameters on `Log.Info`/`Debug`/`Warning`/`Error`/`Critical`. Compile-time resolution; zero runtime cost. The `Log` API surface stays unchanged from caller perspective (defaulted parameters). Apply to managed side only — native `spdlog` already has cheap source-location.
- **D-22 (R-F):** **Header auto-discovery via `Directory.EnumerateFiles("UtinniCore", "*.h", SearchOption.AllDirectories)` with a path filter excluding `**/_internal/**`.** Convention: any header that should NOT be projected to managed lives under an `_internal/` directory. Phase 3 itself adds zero `_internal/` directories — that's a follow-up cleanup if any existing header should be hidden. Closes CON-O-05: `StdEdited.cs` is NOT a regenerated artifact; the discovery's output is `UtinniCoreDotNet/Generated/UtinniCore.cs` only. Both `StdEdited.cs` and `Std.cs` (the latter generated separately) live in `Generated/` but only `UtinniCore.cs` is the auto-discovery target.
- **D-23 (R-G):** **`Props.CreateDotNetDirectoryProps` becomes an idempotent merger.** Load the existing `Directory.Build.props` XML if present (XDocument); locate or create the `<PropertyGroup Condition="'$(Platform)' == 'x86'">` element; insert/update `<UtinniPath>`/`<UtinniRefAssemblies>`/etc.; preserve untouched siblings; write back. Test fixtures per D-05 cover create-fresh, merge-into-existing, and idempotent re-run.

### CON-O-05 + CON-O-07 dispositions

- **D-24 (CON-O-05):** **`StdEdited.cs` curation criteria** — `StdEdited.cs` exists specifically because CppSharp can't generate stable bindings for STL templates (`std::basic_string`); the file is the curated counterpart to the symbol stubs in `UtinniCore-Symbols/Std-symbols.cpp`. Criteria for keeping a binding hand-curated in `StdEdited.cs`: (a) CppSharp generates incorrect output for it, OR (b) the symbol name is unstable across MSVC versions, OR (c) the binding requires marshaling logic that CppSharp can't infer. R-F's auto-discovery only regenerates `Generated/UtinniCore.cs`; `StdEdited.cs` (and `Std.cs`, generated by the symbol-stub project) are out of scope for the discovery. Disposition committed alongside R-F task in Plan 03-03.
- **D-25 (CON-O-07):** **Sytner's plugin status** — dormant; not actively maintained in any known downstream fork. No ABI-compat target preserved through R-B. Disposition committed alongside R-B task in Plan 03-02 (resolves the open question and the latent ABI issue in the same commit).

### Cross-Repo (R-B only)

- **D-26:** **R-B's UtinniPlugins-side migration lands as paired commits in `kennethlong/UtinniPlugins`.** TJT updated to export `destroyPlugin` symmetrically with `createPlugin`. Manual TJT build verifies (Phase 02 D-09 precedent; no UtinniPlugins CI yet). Plan 03-02 tracks the TJT-side migration task with explicit `repo:UtinniPlugins` flag. Separate clone, separate commit, separate PR.

### Claude's Discretion

- Exact xUnit test naming (follow Phase 1/2/2.1 `[Method]_[Scenario]_[ExpectedOutcome]` convention).
- Task ordering WITHIN a plan (planner picks based on dependency).
- Whether the `Drain` helper lives in a new `Callbacks/CallbackHelpers.cs` or folds into an existing utility file (planner's call based on namespace hygiene).
- Final naming for the `Utinni.CrtMatchPlugin` / `Utinni.LegacyPlugin` fixture projects (precedent: `Utinni.LoaderLockHarness` from Phase 02).
- Whether R-F's `_internal/` directory convention gets exercised in Phase 3 by moving any existing header (planner audits; if no existing header is private, no moves happen — convention is documented for future use).
- Order of R-E/R-F/R-G within Plan 03-03 (planner picks; R-F probably first because it surfaces what gets projected to managed, R-G probably last because it's the most isolated).
- Whether CON-O-05/-07 disposition updates to `docs/ai/assessment.md` fold into their related fix commits or get a single roll-up disposition commit per plan boundary.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context (locked decisions, requirements, constraints)
- `.planning/PROJECT.md` — V1 milestone scope, anti-goals (DEC-A1..A4), preservation guard-rails (24 load-bearing foundations including CON-N-01 detour-table, CON-N-02 thin-wrapper firewall, CON-N-08 PluginManager pImpl, CON-M-01/02 IPlugin/IEditorPlugin MEF SPI, CON-T-04 Props.cs factoring, CON-T-05 Jawa-Toolbox `*Impl` separation).
- `.planning/REQUIREMENTS.md` §STAB-02 — R-A..R-H requirement (the requirement this phase delivers); §STAB-04 — preservation cross-cutting; §STAB-05 — open-question dispositions (CON-O-05 and CON-O-07 mapped to this phase).
- `.planning/ROADMAP.md` §"Phase 3" — phase goal, success criteria (#1 every callback symmetric Add/Remove, #2 createPlugin/destroyPlugin + init() called + LoadLibrary failures logged, #3 single-source RVAs via UTINNI_API, #4 snapshot iteration, #5 CppSharp auto-discovery + idempotent props wizard + CallerMemberName logging), preservation guard-rails.
- `.planning/intel/constraints.md` — CON-N-01 (detour-table; not modified by R-A/B/C/H), CON-N-02 (thin-wrapper firewall; R-B `destroyPlugin` is in the wrapper layer), CON-N-04 (memory VirtualProtect bracket; R-C getter must not touch protected memory), CON-N-08 (PluginManager pImpl; R-B Impl struct grows but stays pImpl-hidden), CON-M-01..-09 (managed SPI shape; R-B createPlugin/destroyPlugin contract change is acceptable per CON-M-01/02 since IPlugin/IEditorPlugin signatures aren't changing — only the native plugin ABI), CON-T-04 (Props.cs factoring; R-G refactor keeps this), CON-T-05 (Jawa-Toolbox `*Impl` separation; R-B TJT migration preserves), CON-O-05 (StdEdited.cs curation), CON-O-07 (Sytner's plugin).
- `.planning/intel/decisions.md` — non-locked candidate decisions; D-08 tiered testing philosophical home for max-harness posture.

### Prior-phase carry-forward (decisions inherited from Phase 1, 2, 2.1)
- `.planning/phases/01-ci-tier-1-c-scaffold/01-CONTEXT.md` — D-01 sibling test project convention; D-02 `net472`/x86; D-03 xUnit 2.9.x; D-04 `[Method]_[Scenario]_[ExpectedOutcome]` test naming.
- `.planning/phases/02-critical-bug-burn-down-c-01-c-15/02-CONTEXT.md` — D-04 max-harness posture (every C-NN gets a regression test); D-05 per-bug harness shapes; D-07 single test project absorbs everything; D-09 cross-repo direct commit (no UtinniPlugins CI yet — manual verify); C-16 closed CON-O-03 (delegate-pinning); KB-05 closed CON-O-01 (`||` → `&&` at game.cpp:361).
- `.planning/phases/02.1-phase-02-gap-closure-critical-correctness-harness-quality-fr/02.1-CONTEXT.md` — D-04 max-harness preserved; D-10 no cross-repo work in 02.1; IN-05 `Drain(ConcurrentQueue<Action>)` helper explicitly deferred to R-A scope.

### Source documents (assessment, internals, vision)
- `docs/ai/assessment.md` §"Strategic reworks" — R-A through R-H enumeration with file paths and approaches; primary source for this phase.
- `docs/ai/assessment.md` §"Open questions" — CON-O-05 (`StdEdited.cs` curation) and CON-O-07 (Sytner's plugin) are the two scoped here.
- `docs/ai/assessment.md` §"Status tracking" — table to update as each R-X closes during execution.
- `docs/ai/internals.md` §"isSafeToUse" — already-applied disposition (`&&` per Phase 02 KB-05 fix).
- `docs/ai/vision.md` — anti-goals (DEC-A1..A4 inform what plugin loading must NOT pull in: no server, no launcher, no DCC).
- `docs/ai/test-harness-plan.md` — Tier 1 = managed xUnit (this phase's R-A/R-B/R-C/R-E/R-F/R-G harnesses). Tier 2 (Phase 4 CLI) is downstream of these reworks per assessment.md sequencing.

### Codebase intel (read-only reference)
- `.planning/codebase/CONCERNS.md` — TD-15 (R-A native), TD-16 (R-A managed), TD-17 (R-B), TD-18 (R-C), TD-20 (R-E), TD-21 (R-F), TD-22 (R-G), TD-23 (R-H) — direct one-to-one mapping to R-letters.
- `.planning/codebase/ARCHITECTURE.md` §"Callback bus" — pattern this phase systematizes; §"`utinni::UtinniPlugin`" — interface this phase extends with `destroyPlugin`; §"`swg::<area>`" — namespace pattern R-C respects.
- `.planning/codebase/STACK.md` §"Runtime" + §"Testing" — `net472`/x86/xUnit 2.x constraints carrying from Phase 1.
- `.planning/codebase/CONVENTIONS.md` — Allman braces, 4-space, PascalCase, MIT header on every C# file; applies to new test code + any new helper file (`CallbackHelpers.cs`).
- `.planning/codebase/INTEGRATIONS.md` §"CON-T-01 post-build chain" — `UtinniCoreDotNetGen.exe` invoked here; R-F's header glob change must keep this working under CI's path conventions.
- `.planning/codebase/TESTING.md` — verified zero-baseline + Phase 1 + Phase 2 + Phase 02.1 incremental adds; this phase continues the same trajectory.

### Surface this phase touches

**R-A (native callback registries — ~12 files):**
- `UtinniCore/swg/game/game.cpp:71-104` — Add*Callback functions, callback vectors
- `UtinniCore/swg/scene/ground_scene.cpp` — same shape per file
- `UtinniCore/swg/object/*.cpp` — same shape per file
- `UtinniCore/swg/graphics/post_processing.cpp:37-69` — post-draw callbacks
- `UtinniCore/swg/graphics/depth_texture.cpp:37,206-209` — depth resolve callbacks
- `UtinniCore/swg/ui/imgui_impl.cpp` — ImGui callbacks
- Plus ~5 more `swg::*` files with callback registries (planner enumerates exact list during research substep)

**R-A (managed callback classes):**
- `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:85-98` — Add*/Remove*
- `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs:55-73` — Add*-only today
- `UtinniCoreDotNet/Callbacks/ObjectCallbacks.cs` — mixed
- `UtinniCoreDotNet/Callbacks/CuiCallbacks.cs`, `ImGuiCallbacks.cs` — same shape
- `UtinniCoreDotNet/Utility/Log.cs:121,126` — `AddOuputSinkCallback` typo + Add/Remove pair (R-A overlap)

**R-A (IN-05 Drain helper):**
- `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` — drain queue site
- `UtinniCoreDotNet/Callbacks/GroundSceneCallbacks.cs:97-106` (post-Phase 02 fix: drains the right queue but pattern is duplicated)
- `UtinniCoreDotNet/Callbacks/ObjectCallbacks.cs` — drain queue site
- New file: `UtinniCoreDotNet/Callbacks/CallbackHelpers.cs` (or similar; planner names final)

**R-B (plugin lifecycle):**
- `UtinniCore/plugin_framework/plugin_manager.cpp:41-49` — `~PluginManager`
- `UtinniCore/plugin_framework/plugin_manager.cpp:129-150` — `loadPlugins` loop (`init()` invocation + LoadLibrary failure logging + HMODULE tracking)
- `UtinniCore/plugin_framework/utinni_plugin.h` — `UTINNI_PLUGIN` macro extension for `destroyPlugin`
- Cross-repo: `kennethlong/UtinniPlugins` TJT — TJT plugin entry exports `destroyPlugin` matching new ABI

**R-C (single-source WndProc):**
- `UtinniCore/swg/client/client.h` — new `UTINNI_API IntPtr getSwgWndProc()` declaration
- `UtinniCore/swg/client/client.cpp:43` — existing `0x00AA0970` constant; new getter implementation
- `UtinniCoreDotNet/UI/Controls/PanelGame.cs:41` — replace literal `new IntPtr(0x00AA0970)` with cached call to `UtinniCore.Utinni.Client.GetSwgWndProc()`
- (Generated/UtinniCore.cs auto-picks up via R-F or current allowlist)

**R-E (CallerMemberName logging):**
- `UtinniCoreDotNet/Utility/Log.cs:50-69` — `FormatText` rewrite

**R-F (CppSharp header auto-discovery):**
- `UtinniCoreDotNetGen/Program.cs:67-92` — header list → glob replacement

**R-G (Directory.Build.props idempotent wizard):**
- `sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs:9-14` — early-return → merge

**R-H (snapshot iteration):**
- Native: `UtinniCore/swg/game/game.cpp:115-195` callback dispatch sites + analogous sites in other `swg::*` files (paired with R-A; if R-A uses `unordered_map`-backed registry, R-H is the iteration-snapshot pattern at every dispatch site)
- Managed: `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:122-144` + analogous sites in other `Callbacks/*.cs` files

**Test home:**
- `UtinniCoreDotNet.Tests/` — absorbs all managed-side regression tests + P/Invoke harnesses
- New `UtinniCoreDotNet.Tests/Fixtures/CrtMatchPlugin/` and `Fixtures/LegacyPlugin/` subdirs
- New sibling projects: `Utinni.CrtMatchPlugin` and `Utinni.LegacyPlugin` (fixture DLLs) — added to `Utinni.sln` per Phase 02 sibling-project precedent

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`UtinniCoreDotNet.Tests`** (from Phase 1 + extended in Phase 2/2.1) — xUnit 2.9.x project, `net472`/x86, already wired into CI. Absorbs all new managed-side regression tests, P/Invoke harnesses, fixture-driven tests. Adds `/Fixtures/CrtMatchPlugin/` and `/Fixtures/LegacyPlugin/` subdirs for R-B fixtures.
- **CI workflow** (`.github/workflows/ci.yml` from Phase 1) — runs `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86` and `dotnet test`. Each Phase 3 fix's harness piggybacks; no new CI workflow needed. R-F's auto-discovery must compose with the `UtinniCoreDotNetGen.exe` post-build invocation (CON-T-01).
- **Sibling-fixture-project pattern** (Phase 02 `Utinni.LoaderLockHarness`; Phase 02.1 `Utinni.CrossCrtFreeFixture`) — precedent for adding small dedicated test-fixture DLLs to `Utinni.sln`. Phase 3 adds `Utinni.CrtMatchPlugin` + `Utinni.LegacyPlugin` for R-B.
- **`PanelGame.cs` already has the `IntPtr swgWndProc` field-cache shape** at line 41 — R-C's refactor is one-line: replace `new IntPtr(0x00AA0970)` with `UtinniCore.Utinni.Client.GetSwgWndProc()`.

### Established Patterns
- **Atomic commit per task** — GSD executor default; each R-X spans one or several commits, paired with its harness.
- **MIT license header on every C# file** — applies to new files (`CallbackHelpers.cs`, new fixture projects).
- **PIMPL idiom for native singletons** (CON-N-08) — `PluginManager::Impl` extension for HMODULE tracking stays inside pImpl, doesn't widen the public header.
- **Detour-table pattern (CON-N-01)** — not modified by Phase 3 (R-C is RVA surfacing, not a new detour).
- **`utinni::` thin-wrapper firewall (CON-N-02)** — R-B's `destroyPlugin` and R-C's `getSwgWndProc` both live in this layer (or behind it). R-A's `Subscribe`/`Unsubscribe` exposed as `utinni::` API.
- **CppSharp auto-projection** — anything in the `Generated/UtinniCore.cs` set with `UTINNI_API` becomes available to managed code via `UtinniCore.Utinni.*`. R-C relies on this; R-F formalizes the discovery.
- **Two-phase composition (Phase 1 / Phase 2 MEF pattern)** — `PluginLoader` does `AggregateCatalog` → `ComposeParts` (post Phase 02 C-06 fix: per-plugin try/catch). R-B's two-phase init mirrors this on the native side (all createPlugin → all init).

### Integration Points
- **`Utinni.sln`** — adds two new projects (R-B fixtures). Project dependency: fixture-plugin DLLs reference `UtinniCore` headers (for `UTINNI_PLUGIN` macro + `UtinniPlugin` base class), build with `/MT` for one fixture and `/MD` for the other (to exercise the CRT-match vs CRT-mismatch paths). Planner picks final names per D-07.
- **CppSharp generator** — R-F's glob walks `UtinniCore/**/*.h`; output continues to land in `UtinniCoreDotNet/Generated/UtinniCore.cs`; `StdEdited.cs` and `Std.cs` are untouched (D-22 + D-24).
- **`docs/ai/assessment.md` §"Status tracking"** — update each R-X row's `Status` to `done` with PR/SHA as each R-X closes. Last R-X flips the last row + the "remaining R-items" count to 0.
- **`docs/ai/assessment.md` §"Open questions"** — CON-O-05 + CON-O-07 disposition rows updated alongside the R-F + R-B commits respectively.
- **`UtinniPlugins` sister repo (R-B only)** — cross-repo commit for TJT `destroyPlugin` export. Separate clone, separate PR, manual TJT build verifies.

</code_context>

<specifics>
## Specific Ideas

- **CI must stay green THROUGHOUT — not just at phase boundaries.** Each fix commit ships behind a green CI run. The 3-plan ordering (Callbacks → Lifecycle/RVAs → Build-tooling) protects against late-discovered architectural blockers.
- **Plan 03-01 first task is the IN-05 `Drain` helper, NOT a Subscribe/Unsubscribe refactor.** Land the helper, refactor 3 callsites, prove green CI — then start the R-A Subscribe/Unsubscribe per-file work. Keeps the carry-over from Phase 02.1 closed cleanly and gives a low-risk first task to validate the plan structure.
- **R-B's two fixture plugins (`Utinni.CrtMatchPlugin` + `Utinni.LegacyPlugin`) are the load-bearing harness for the entire phase.** They exercise: (a) symmetric `createPlugin`/`destroyPlugin` path, (b) legacy-plugin path where `destroyPlugin` is absent (fallback to virtual destructor or skip-and-log), (c) two-phase init ordering, (d) `LoadLibrary` failure (a deliberately-corrupt fixture). Build them early in Plan 03-02 because subsequent harnesses depend on them.
- **R-C verification needs no live SWG.** The getter returns the literal `0x00AA0970` constant; xUnit P/Invokes the DLL, calls the getter, asserts value match. The actually-call-into-SWG path is Tier-4 manual (D-06).
- **R-E is the smallest R-item by line count** (Log.FormatText is one function). Likely first task of Plan 03-03 to land a quick win and validate the plan structure.
- **CON-O-05 + CON-O-07 disposition wording** — follow Phase 02 D-12 style (cite the disposition in a one-line comment in `docs/ai/assessment.md`, link to the implementing commit SHA in the disposition row).

</specifics>

<deferred>
## Deferred Ideas

- **Plugin hot-reload** — R-B's `destroyPlugin` + HMODULE tracking enables the *structural* groundwork, but actually exercising hot-reload (unload-while-running + reload) is V2. Don't extend Phase 3 to cover the runtime flow.
- **`Add*` marked `[Obsolete]`** — Keep wrappers compiling silently for V1. Revisit in Phase 6 (STAB-03 cleanup pass) or V2 once UtinniPlugins has fully migrated to `Subscribe`/`Unsubscribe`.
- **Surfacing the two `isSafeToUse` RVAs via `UTINNI_API`** — not needed today (no managed reader). Add when/if a Wave-1 subpanel needs them (likely never; isSafeToUse is internal scene-load gating).
- **Other ~25 detoured RVAs surfaced via UTINNI_API** — none are duplicated to managed today. R-C pattern is the template if duplication ever appears.
- **Hardcoded font path (TD-27 `C:/Windows/Fonts/micross.ttf`)** — Phase 6 STAB-03 cleanup.
- **`UtinniForm` icon TD-28 `TJT.ico` framework default** — Phase 6 STAB-03 cleanup.
- **TD-25 empty stub files** (`particle.cpp`, `scene.cpp`) — Phase 6 STAB-03.
- **TD-26 disabled hooks in detour table** (`render_world.cpp`, `client_world.cpp`, `io_win.cpp`, `utinni.cpp` commented `Detour::Create` calls) — Phase 6 STAB-03; some may flip to active during R-A audit if they have meaningful bodies, but Phase 3 doesn't enable any.
- **CON-O-08 (DXSDK vs Windows 10 SDK)** — Phase 6 STAB-03 bundled with dep bumps.
- **CON-O-06 (LeksysINI replacement)** — Phase 6 STAB-03.
- **CON-O-09 (fixture storage in-repo vs Git LFS), CON-O-11 (CLI public vs internal)** — Phase 4 TEST-03.
- **UtinniPlugins CI bootstrap** — Phase 7+ or dedicated bridging phase. Phase 3 keeps Phase 02 D-09 manual-verify posture for the one R-B cross-repo commit.
- **`.clang-format` adoption + comprehensive analyzer-rule `.editorconfig`** — Phase 6 STAB-03.
- **Coverage tooling (coverlet, ReportGenerator)** — revisit after Phase 4/5 lands more test breadth.

</deferred>

---

*Phase: 03-strategic-reworks-r-a-r-h*
*Context gathered: 2026-05-21*
