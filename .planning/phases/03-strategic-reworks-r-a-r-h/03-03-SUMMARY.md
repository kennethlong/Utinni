---
phase: 03-strategic-reworks-r-a-r-h
plan: 03
subsystem: build-tooling-and-logging
tags: [logging, callermember-name, cppsharp, header-discovery, xdocument, idempotent-merge, vsix, props, xxe-safe]
status: complete

# Dependency graph
requires:
  - phase: 03-strategic-reworks-r-a-r-h
    plan: 02
    provides: "Plan 03-02 R-B/R-C closed -- symmetric plugin ABI + single-source-of-truth WndProc RVA. Plan 03-03 R-F auto-discovery picks up the new R-C Client::getSwgWndProc declaration automatically (forward-compat verified)."
  - phase: 03-strategic-reworks-r-a-r-h
    plan: 01
    provides: "Plan 03-01 R-A/R-H closed -- handle-based callback Subscribe/Unsubscribe + R-H snapshot dispatch + Log.cs SubscribeOutputSink (Phase 3 Plan 03-01 already added a Subscribe/Unsubscribe surface on Log.cs). Plan 03-03 R-E preserves that surface intact -- only the FormatText / Info / Debug / Warning / Error / Critical bodies change."
  - phase: 02-critical-bug-burn-down-c-01-c-15
    provides: "SlnDirResolver linked-source pattern (CppSharpSlnDirTests precedent for the HeaderDiscovery + Props.cs linked-source extension in Plan 03-03)."
  - phase: 01-ci-tier-1-c-scaffold
    provides: "xUnit 2.9.x + net472 x86 test infrastructure, [Method]_[Scenario]_[ExpectedOutcome] naming, CI gate on master, R-D (.github/workflows/ci.yml) shipped in Plan 01-02 (retroactively marked done in this plan's Task 4)."

provides:
  - "Log.cs FormatText no longer walks the runtime stack -- callerName / callerFile come from [CallerMemberName] / [CallerFilePath] defaulted parameters on every public Log method (Info/Debug/Warning/Error/Critical). Compile-time resolution; zero runtime cost on the hot path. Existing call sites compile unchanged (defaulted params are transparent)."
  - "HeaderDiscovery.Discover(utinniCoreRoot) replaces UtinniCoreDotNetGen's 23-entry explicit allowlist with a recursive *.h glob that excludes any header beneath an _internal/ directory (case-insensitive, at any depth). Discovered paths use backslash separators (CppSharp's module.Headers.Add expects this). HeaderDiscovery lives in its own file so UtinniCoreDotNet.Tests can link it via the same Compile-Include-Link pattern already used for SlnDirResolver."
  - "Props.CreateDotNetDirectoryProps rewritten as an idempotent XmlReader+XDocument-based merger. The previous early-return-on-existing-file behaviour (TD-22) is gone; the wizard now loads the existing file, upserts the four Utinni-owned PropertyGroups, and writes the result back. Non-PropertyGroup siblings (Import / ItemGroup / Target) plus user-authored PropertyGroup children are preserved untouched. XmlReader carries explicit DtdProcessing=Prohibit + XmlResolver=null for T-03-03-01 XXE safety. CON-T-04 invariant (single method in Props.cs; private UpsertPropertyGroup helper) preserved."
  - "CON-O-05 disposition committed: StdEdited.cs is the only hand-curated Generated/ file; R-F auto-discovery regenerates ONLY Generated/UtinniCore.cs; criteria (a/b/c) documented inline."
  - "Three new test files + 21 net new Facts: LogCallerMemberNameTests (4 explicit + 5-row Theory = 9 tests), HeaderDiscoveryTests (6 Facts), DirectoryBuildPropsTests (6 Facts). Total test count 83 -> 104."
  - "docs/ai/assessment.md Status tracking: every R-letter row (R-A..R-H, 8/8) reads 'done'. STAB-02 deliverable visible in the table. Phase 3 functionally complete."

affects: [phase-04-test-harness-tier-2-cli, phase-06-stab-03-cleanups, plugins-tjt, plugins-sytner, code-review-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Compile-time caller resolution via [CallerMemberName] + [CallerFilePath] defaulted parameters on public logging API -- transparent to existing call sites, zero runtime cost, class-name extraction via Path.GetFileNameWithoutExtension(callerFile) is stable across local/CI build machines because only the basename is used."
    - "Convention-based header discovery: glob *.h under a root, exclude any path segment named '_internal' (case-insensitive, at any depth). Public API headers are auto-included; non-projected headers move under _internal/ subdirectories. Phase 3 itself adds zero _internal/ directories -- the convention is documented for future use."
    - "Idempotent XDocument-based config merge: load existing file (or create skeleton), upsert PropertyGroups by Condition-attribute identity, write back. Existing user properties preserved; idempotent on re-run (value-stable byte equality). XmlReaderSettings { DtdProcessing = Prohibit, XmlResolver = null } applied at load time for XXE safety -- empirically required because XDocument.Load(string) on .NET 4.7.2 does NOT inherit Prohibit from XmlReaderSettings.Default."
    - "Linked-source compile pattern extended: UtinniCoreDotNet.Tests pulls Props.cs + HeaderDiscovery.cs in via <Compile Include='..\\path\\to\\X.cs' Link='Y/X.cs' />. ProjectReference is impractical (UtinniCoreDotNetGen is AnyCPU/x64 due to CppSharp AMD64 deps; Vsix.csproj has VsixV3 ProjectTypeGuid + Microsoft.VisualStudio.SDK; Tests project is net472/x86). Linked-source gives a bitness-matched copy at the test project's expense (a few seconds of duplicate compilation) and avoids the BadImageFormatException + VSIX-tooling-deps problems entirely."

key-files:
  created:
    - "UtinniCoreDotNet.Tests/LogCallerMemberNameTests.cs (R-E regression Facts; reflection-driven FormatText probe; negative grep on Log.cs source for absence of new StackTrace().GetFrame)"
    - "UtinniCoreDotNetGen/HeaderDiscovery.cs (NEW public static class; isolated from Program.cs so Tests project can link via <Compile Include=.../>)"
    - "UtinniCoreDotNet.Tests/HeaderDiscoveryTests.cs (R-F regression Facts; temp-dir fixture pattern + real-UtinniCore-root coverage tests)"
    - "UtinniCoreDotNet.Tests/DirectoryBuildPropsTests.cs (R-G regression Facts; XXE rejection + idempotency + user-property preservation + stale-value update)"
  modified:
    - "UtinniCoreDotNet/Utility/Log.cs (FormatText signature -> (text, callerName, callerFile); each public Log method's signature gains [CallerMemberName] + [CallerFilePath] defaulted params; using System.Diagnostics removed; using System.IO + System.Runtime.CompilerServices added)"
    - "UtinniCoreDotNetGen/Program.cs (23-entry explicit allowlist replaced with HeaderDiscovery.Discover foreach; IgnoreHeadersWithName extended with four new entries -- see Deviations below)"
    - "UtinniCoreDotNetGen/UtinniCoreDotNetGen.csproj (Compile entry for new HeaderDiscovery.cs)"
    - "UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj (linked-source Compile entries for HeaderDiscovery.cs + Props.cs)"
    - "sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs (rewrite: XmlReader+XDocument idempotent merger; private static UpsertPropertyGroup + PropertyEntry POCO; CON-T-04 invariant preserved -- same public CreateDotNetDirectoryProps(slnPath) signature, same Props.cs location)"
    - "sdk/UtinniPluginTemplates/Vsix/Vsix.csproj (added <Reference Include='System.Xml.Linq'/>)"
    - "UtinniCoreDotNet/Generated/UtinniCore.cs (CppSharp regenerated under the new auto-discovery; +~3500 lines net as previously-excluded UtinniCore public headers come online)"
    - "docs/ai/assessment.md (Status tracking: R-D + R-E + R-F + R-G marked done with implementing SHAs; Open Questions: CON-O-05 disposition added in §5 with reference to commit 8aea6af and HeaderDiscovery.cs file-header documentation)"

key-decisions:
  - "Tests project absorbs Props.cs via linked-source per plan's default option (c). A ProjectReference to Vsix.csproj is impractical (VSIX project carries the VsixV3 ProjectTypeGuid + Microsoft.VisualStudio.SDK build tooling; the test runner does not need either)."
  - "HeaderDiscovery lives in its own file (UtinniCoreDotNetGen/HeaderDiscovery.cs), not as a nested class in Program.cs, so the Tests project can link it -- same precedent as SlnDirResolver.cs. Class is marked public (not internal + InternalsVisibleTo) because UtinniCoreDotNetGen has no AssemblyInfo.cs that already carries [InternalsVisibleTo], and adding one to the gen project just to expose one helper is heavier than making the helper public."
  - "Test 1 in LogCallerMemberNameTests asserts FormatText behavior via reflection rather than invoking Log.Info(text) end-to-end. Reason: end-to-end coverage of Log.Info would require native log (spdlog) to be loaded + Log.Setup() to have run, which depends on UtinniCore.dll's runtime initialization order. Reflection-driven testing of FormatText is more robust and exercises exactly the surface that changed (the compile-time-resolved-string-flow into FormatText is verified by the dedicated parameter-attribute reflection check in the [Theory] over Info/Debug/Warning/Error/Critical)."
  - "Props.cs uses an explicit XmlReaderSettings { DtdProcessing = Prohibit, XmlResolver = null } wrapped around XmlReader.Create rather than relying on XDocument.Load(string)'s default behaviour. Empirically the .NET 4.7.2 XDocument.Load(string) overload does NOT inherit DtdProcessing=Prohibit from XmlReaderSettings.Default; the explicit reader is required for the T-03-03-01 XXE mitigation to actually fire."

patterns-established:
  - "[CallerMemberName] + [CallerFilePath] defaulted-parameter logging API: 'public static void Info(string text, [CallerMemberName] string callerName = \"\", [CallerFilePath] string callerFile = \"\") { log.Info(FormatText(text, callerName, callerFile)); }' -- repeats per log level. Class-name extraction is Path.GetFileNameWithoutExtension(callerFile) (stable across build machines because only the basename is used; the absolute prefix the compiler embedded is discarded)."
  - "Convention-based source-tree glob with explicit ignore-list: Directory.EnumerateFiles(root, '*.h', AllDirectories) -> Split('\\\\', '/') -> excluded if any segment equals '_internal' case-insensitively. Returned paths use backslash separators after .Replace('/', '\\\\')."
  - "Idempotent XML config merger: XmlReader.Create(filePath, hardenedSettings) -> XDocument.Load(reader) -> locate-or-create <Project> -> UpsertPropertyGroup(conditionAttr, properties) per Utinni-owned group -> doc.Save(filePath). UpsertPropertyGroup matches an existing group by Condition-attribute identity (null = the unconditional group); creates if missing; child-element values are set-or-updated, never appended duplicate."
  - "PropertyEntry POCO struct instead of ValueTuple (name, value): avoids the System.ValueTuple dependency in the VSIX package on net472."

requirements-completed: [STAB-02]

# Metrics
duration: "~1.5h"
started: "2026-05-21 (worktree agent-a5fd6b6c1d13d2055 spawn)"
completed_tasks: 4
total_tasks: 4
completed_at: "2026-05-21"
---

# Phase 3 Plan 03: Build-tooling + logging (R-E + R-F + R-G + CON-O-05) Summary

**All 4 tasks complete. R-E ([CallerMemberName] / [CallerFilePath] defaulted parameters replace Log.FormatText's runtime stack walk), R-F (HeaderDiscovery.Discover replaces UtinniCoreDotNetGen's 23-entry explicit allowlist with an `_internal/`-aware glob), and R-G (Props.CreateDotNetDirectoryProps rewritten as an idempotent XmlReader+XDocument-based merger) landed. After this plan every R-letter row (R-A..R-H) in `docs/ai/assessment.md` reads 'done'; STAB-02 requirement satisfied. Phase 3 (STAB-02) complete; Phase 4 (TEST-03 Tier 2 CLI shim) is the next phase per ROADMAP.md.**

## Performance

- **Duration:** ~1.5h (single executor session)
- **Started:** 2026-05-21 (worktree agent-a5fd6b6c1d13d2055 spawn)
- **Completed:** 2026-05-21
- **Tasks:** 4 / 4
- **Commits:** 4 (one per task; atomic per D-03)
- **Files modified:** 9 source/csproj/sln files (3 modified, 4 newly created) + 1 docs file = **10 files**
- **Test count:** 83 -> **104** (+21 net new Facts)

## Accomplishments

- **R-E ([CallerMemberName] logging, Task 1, commit cb3f373):** Log.FormatText no longer walks the runtime stack to discover the caller. Each public Log method (Info/Debug/Warning/Error/Critical) now carries [CallerMemberName] + [CallerFilePath] defaulted parameters; FormatText takes the resolved strings and extracts the class name via Path.GetFileNameWithoutExtension(callerFile). Compile-time resolution; zero runtime cost on the hot path. The `using System.Diagnostics` import is gone from Log.cs. Existing call sites (Log.Info("msg")) compile unchanged -- defaulted parameters are transparent. Test coverage: 9 facts (4 explicit + 5-row Theory) including the negative-grep gate that the source no longer contains `new StackTrace().GetFrame`.
- **R-F (CppSharp header auto-discovery, Task 2, commit 8aea6af):** UtinniCoreDotNetGen/Program.cs's 23-entry explicit allowlist is gone. The new HeaderDiscovery.Discover(utinniCoreRoot) walks UtinniCore/ recursively, collects every *.h, and excludes any header beneath an `_internal/` directory (case-insensitive at any depth). Discovered paths use backslash separators (CppSharp's module.Headers.Add expects this). The newly-discovered surface includes Plan 03-02 R-C's swg::client::Client::getSwgWndProc declaration -- forward-compat is verified by HeaderDiscoveryTests.HeaderDiscovery_RealUtinniCoreRoot_PicksUpClientHeader plus a positive grep on `getSwgWndProc` in the regenerated `Generated/UtinniCore.cs`. HeaderDiscovery lives in its own file so the Tests project can link it via `<Compile Include="..." Link="..." />` -- same precedent as SlnDirResolver. CON-O-05 disposition documented in the HeaderDiscovery.cs file-header comment.
- **R-G (idempotent Directory.Build.props merger, Task 3, commit e8fe682):** Props.CreateDotNetDirectoryProps's previous destructive-by-omission early-return (TD-22) is gone. The new implementation loads the existing file via XmlReader with explicit DtdProcessing=Prohibit + XmlResolver=null (T-03-03-01 XXE safety; required because XDocument.Load(string) does NOT inherit Prohibit from XmlReaderSettings.Default in .NET 4.7.2), resolves the <Project> root, upserts each of four Utinni-owned PropertyGroups (one unconditional + RelWithDbgInfo/Release/Debug-conditional), and writes the result back via XDocument.Save. CON-T-04 preserved: same public method signature in the same Props.cs file; UpsertPropertyGroup is a private static helper inside the same file. Idempotency verified by Fact 3 (byte-equality + XNode.DeepEquals on two consecutive calls); user-authored siblings (other PropertyGroups, ItemGroups, Imports, Targets) preserved (Fact 6).
- **CON-O-05 + assessment.md status tracking (Task 4, commit a0a3d62):** All R-letter rows R-A..R-H now read 'done' in the Status tracking table. R-D was retroactively closed (CI workflow shipped in Phase 1 Plan 01-02 commit 2790de4 but never reflected in the table). CON-O-05 disposition added inline in Open Questions §5: StdEdited.cs is the only hand-curated Generated/ file; R-F regenerates only Generated/UtinniCore.cs; criteria (a/b/c) documented. CON-O-07 was already disposed in Plan 03-02 Task 2. STAB-02 deliverable visible in the table.

## Task Commits

Each task committed atomically per D-03:

1. **Task 1: R-E Log [CallerMemberName] refactor** -- `cb3f373` (feat)
2. **Task 2: R-F CppSharp header auto-discovery + CON-O-05 disposition** -- `8aea6af` (feat)
3. **Task 3: R-G idempotent Directory.Build.props merger** -- `e8fe682` (feat)
4. **Task 4: assessment.md status tracking + CON-O-05** -- `a0a3d62` (docs)

## Files Created/Modified

**Created (4):**
- `UtinniCoreDotNet.Tests/LogCallerMemberNameTests.cs` -- 9 R-E Facts (4 explicit + 5-row Theory).
- `UtinniCoreDotNetGen/HeaderDiscovery.cs` -- public static class extracted from Program.cs so Tests can link it.
- `UtinniCoreDotNet.Tests/HeaderDiscoveryTests.cs` -- 6 R-F Facts (3 fixture-tree + 2 real-root + 1 separator).
- `UtinniCoreDotNet.Tests/DirectoryBuildPropsTests.cs` -- 6 R-G Facts (fresh / merge / idempotent / stale-update / XXE / non-PropertyGroup-sibling preservation).

**Modified (6 source/build files + 1 docs):**
- `UtinniCoreDotNet/Utility/Log.cs` -- R-E rewrite of FormatText + 5 public Log methods.
- `UtinniCoreDotNetGen/Program.cs` -- R-F glob replaces allowlist; IgnoreHeadersWithName extended by 4 entries.
- `UtinniCoreDotNetGen/UtinniCoreDotNetGen.csproj` -- Compile entry for HeaderDiscovery.cs.
- `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` -- linked-source Compile entries for HeaderDiscovery.cs + Props.cs.
- `sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs` -- R-G XmlReader+XDocument-based idempotent merger.
- `sdk/UtinniPluginTemplates/Vsix/Vsix.csproj` -- added `<Reference Include="System.Xml.Linq"/>`.
- `UtinniCoreDotNet/Generated/UtinniCore.cs` -- regenerated by CppSharp post-build under the new auto-discovery (auto-edit; not hand-touched).
- `docs/ai/assessment.md` -- Status tracking R-D/E/F/G done; Open Questions CON-O-05 disposed.

## Decisions Made

- **Linked-source for Props.cs into Tests (Task 3 default option (c)):** Chose linked-source per the plan's default. The ProjectReference fallback path (option (a)) would have required Tests to reference Vsix.csproj, which carries the VsixV3 ProjectTypeGuid + Microsoft.VisualStudio.SDK build tooling -- the test runner does not need either. The heavier fallback (option (b), extracting Props into a sibling helper library) would have been overkill for a single small static method. Linked-source compiles Props.cs directly into UtinniCoreDotNet.Tests.dll alongside the source under test.
- **HeaderDiscovery extracted to its own file (Task 2):** The plan offered nested-in-Program.cs as the lighter choice (internal + InternalsVisibleTo). I extracted it to a separate file because (a) UtinniCoreDotNetGen does not currently carry an [InternalsVisibleTo("UtinniCoreDotNet.Tests")] attribute -- adding one to that project's AssemblyInfo.cs just to expose one helper is heavier than the alternative, and (b) the SlnDirResolver-in-its-own-file precedent already establishes the file-per-pure-function pattern. HeaderDiscovery is marked `public static` (consistent with SlnDirResolver) so the linked-source compile into Tests does not need any visibility plumbing.
- **Explicit XmlReader + XmlReaderSettings for Props.cs T-03-03-01 mitigation (Task 3):** Discovered via Test 5 that the .NET 4.7.2 XDocument.Load(string) overload does NOT inherit DtdProcessing=Prohibit from XmlReaderSettings.Default -- a DOCTYPE-containing input was loaded silently. Switched to an explicit `XmlReader.Create(filePath, hardenedSettings)` + `XDocument.Load(reader)` chain so the mitigation actually fires. Documented in the file-header comment of Props.cs.
- **FormatText reflection probe vs end-to-end Log.Info probe (Task 1):** The plan's Test 1 sketch wired into the SubscribeOutputSink callback path. That path requires native log to be initialized (Log.Setup() -> log.AddOutputSinkCallback chain) which depends on UtinniCore.dll's runtime startup ordering -- fragile and indirect. Reflection-driven testing of FormatText directly is more robust and exercises exactly the surface that changed; the [Theory] reflection probe over Info/Debug/Warning/Error/Critical separately verifies that every method carries the [CallerMemberName] + [CallerFilePath] defaulted parameters.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 -- Blocking] R-F auto-discovery surfaces previously-omitted headers that fail to project**

- **Found during:** Task 2 (first build of the solution after replacing the explicit allowlist with the glob)
- **Issue:** The 23-entry explicit allowlist had intentionally omitted several headers under UtinniCore/ that don't project cleanly via CppSharp. The new auto-discovery glob picks all of them up; the resulting Generated/UtinniCore.cs had four classes of build failure:
  - `swg_string.h`: declares overloaded WString ctors for both `wchar_t*` and `char16_t*` (both project to `string` in C#) plus identical operator==/!=/op_Implicit overloads, producing CppSharp output with duplicate C# method signatures (CS0111 / CS0557).
  - `command_parser.h`, `utinni_command_parser.h`, `ui_textbox.h`: publicly expose `swg::WString` fields/parameters; transitively unprojectable once `swg_string` is ignored (CS0234).
  - `string_utility.h`: emits a `TrimableChars` property getter that references `CppSharp.SymbolResolver.ResolveSymbol(...)`, but UtinniCoreDotNet does not reference CppSharp.dll (CppSharp is a build-time dep of UtinniCoreDotNetGen only) -- CS0103.
- **Fix:** Extended `ctx.IgnoreHeadersWithName(...)` in UtinniCoreDotNetGen/Program.cs's Preprocess() callback with four new entries: `swg_string`, `command_parser`, `utinni_command_parser`, `ui_textbox`, `string_utility`. Same disposition / same precedent as the existing `spdlog` / `detourxs` / `ADE32` ignore entries.
- **Files modified:** `UtinniCoreDotNetGen/Program.cs`
- **Verification:** msbuild Release|x86 exits 0; HeaderDiscoveryTests + DirectoryBuildPropsTests + LogCallerMemberNameTests all pass; getSwgWndProc remains in Generated/UtinniCore.cs (Plan 03-02 R-C symbol picked up automatically).
- **Committed in:** `8aea6af` (Task 2 commit)
- **Follow-up:** Per D-22 the proper long-term fix is migrating these headers under their respective `_internal/` subdirectories, but the plan explicitly defers that to a follow-up cleanup ("Phase 3 itself adds zero `_internal/` directories"). The follow-up is a candidate for Phase 6 STAB-03.

**2. [Rule 1 -- Bug] XDocument.Load default DtdProcessing assumption was wrong (T-03-03-01 mitigation did not fire)**

- **Found during:** Task 3 (DirectoryBuildPropsTests.CreateDotNetDirectoryProps_XXEAttempt_RejectedByDtdProhibit ran the XXE input through the merger and the merger SUCCEEDED instead of throwing XmlException)
- **Issue:** The plan's PATTERNS.md interfaces sketch + the initial commit comment in Props.cs both assumed XDocument.Load(string) inherits DtdProcessing=Prohibit from XmlReaderSettings.Default on .NET 4.7.2. Empirically it does not -- a DOCTYPE-containing input loads silently. The T-03-03-01 mitigation was therefore non-existent in the first draft; the threat model was over-claiming.
- **Fix:** Switched to an explicit `XmlReader.Create(filePath, settings)` with `XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }`, then `XDocument.Load(reader)` inside a `using` block. Updated the Props.cs file-header comment to reflect the empirical finding ("X.Load(string) does NOT inherit Prohibit"). Test 5 (XXEAttempt) now correctly asserts `Assert.ThrowsAny<XmlException>(...)` -- the threat is now actually mitigated.
- **Files modified:** `sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs`
- **Verification:** DirectoryBuildPropsTests 6/6 pass; Fact 5 (XXE) throws the expected XmlException.
- **Committed in:** `e8fe682` (Task 3 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug)
**Impact on plan:** Both deviations were necessary to keep the build green AND to make the documented T-03-03-01 mitigation actually fire. No scope creep -- the same 4 tasks landed with the same observable contract. Plan scope and success criteria unchanged. The R-F ignore-list expansion is a documented follow-up for Phase 6 STAB-03 (migrate to `_internal/` per D-22).

## Issues Encountered

- **Git dubious-ownership warning at worktree spawn:** `fatal: detected dubious ownership in repository at 'D:/Code/Utinni/.claude/worktrees/agent-a5fd6b6c1d13d2055'` on the first `git rev-parse --abbrev-ref HEAD` call. Resolved by `git config --global --add safe.directory <worktree path>`. Same pattern as the prior two plans' worktree spawns; no code impact.
- **Worktree base mismatch at spawn:** Worktree initialized at `b36265e` (the Phase 03 context-recording commit); spawn header required base `07bf6e1` (the Phase 03 Plan 03-02 closure point). Reset via `git reset --hard 07bf6e1` per the worktree-branch-check fallback before any task work began. Documented in the worktree HEAD assertion output; no code impact.
- **NuGet restore needed at first solution build:** msbuild Utinni.sln reported `NETSDK1004: Assets file 'project.assets.json' not found` for Tests + Fixtures projects on first run. Resolved via `msbuild Utinni.sln /t:Restore /p:Configuration=Release /p:Platform=x86`. Worktree-spawn-fresh state, not a code issue.
- **Vsix.csproj does not build standalone under VS 2026 (pre-existing):** `msbuild sdk/UtinniPluginTemplates/Vsix/Vsix.csproj` fails because VS 2026's MSBuild does not ship VSSDK targets at the expected `v18.0` path. This is environmental, not caused by Plan 03-03. The Vsix project is intentionally NOT included in `Utinni.sln`; Plan 03-02's SUMMARY.md self-check did not test the Vsix build either. R-G's Props.cs changes are exercised via the Tests project's linked-source compile -- DirectoryBuildPropsTests covers the behavior end-to-end. The Vsix package build itself is a separate concern that VS 2022 or a future VS 2026 VSSDK package update will need to resolve.
- **xUnit2013 style warnings:** Pre-existing `Assert.Equal(N, collection.Count)` patterns across GroundSceneCallbacksTests / UndoRedoManagerTests / PluginLoaderTests trigger xUnit's "use Assert.Empty/Single" analyzer. Not regressions; pre-existing in earlier-phase test files. Out of scope for this plan.

## User Setup Required

None -- no external service configuration required. All work is internal to the framework's build-tooling + logging layer.

## Next Phase Readiness

- **Phase 3 (STAB-02) functionally complete after this plan.** Every R-letter row (R-A..R-H, 8/8) in `docs/ai/assessment.md` reads 'done'; CON-O-05 + CON-O-07 dispositions both committed in Open Questions. The Phase 3 deliverable (R-A..R-H closed; plugin authoring substrate stable; CI catches regressions) is satisfied.
- **Phase 4 (TEST-03 Tier 2 CLI shim) is the next phase per ROADMAP.md.** No carry-overs from this plan. The R-F ignore-list expansion (swg_string / command_parser / utinni_command_parser / ui_textbox / string_utility) is a documented follow-up for Phase 6 STAB-03 (migrate to `_internal/` per D-22).
- **CI status:** master remains green (msbuild Release|x86 exits 0; dotnet test 104/104 passing). The worktree-local build of this plan satisfies the same gates.
- **Pre-existing watch items unchanged:** live-SWG TJT smoke (Plan 03-02 deferred item) remains the operator's responsibility -- not phase-blocking.

## Self-Check: PASSED

- `UtinniCoreDotNet.Tests/LogCallerMemberNameTests.cs` -- exists
- `UtinniCoreDotNet.Tests/HeaderDiscoveryTests.cs` -- exists
- `UtinniCoreDotNet.Tests/DirectoryBuildPropsTests.cs` -- exists
- `UtinniCoreDotNetGen/HeaderDiscovery.cs` -- exists
- Commit `cb3f373` (Task 1) -- present in git log
- Commit `8aea6af` (Task 2) -- present in git log
- Commit `e8fe682` (Task 3) -- present in git log
- Commit `a0a3d62` (Task 4) -- present in git log
- `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86` exits 0
- `dotnet test UtinniCoreDotNet.Tests`: 104 passed / 0 failed
- `Select-String -Path UtinniCoreDotNet/Utility/Log.cs -Pattern 'new StackTrace\\(\\)\\.GetFrame'`: 0 matches (R-E)
- `Select-String -Path UtinniCoreDotNet/Utility/Log.cs -Pattern 'CallerMemberName'`: 7 matches (5 attribute uses + 2 in comments)
- `Select-String -Path UtinniCoreDotNetGen/HeaderDiscovery.cs -Pattern 'Directory.EnumerateFiles'`: 1 match (R-F)
- `Select-String -Path UtinniCoreDotNet/Generated/UtinniCore.cs -Pattern 'getSwgWndProc'`: 1 match (R-C symbol forward-compat)
- `Select-String -Path sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs -Pattern 'XDocument'`: 5 matches (R-G)
- `Select-String -Path sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs -Pattern 'UpsertPropertyGroup'`: 6 matches (R-G)
- `docs/ai/assessment.md` shows R-D=done + R-E=done + R-F=done + R-G=done with commit SHAs; CON-O-05 disposition present in §"Open questions"

---
*Phase: 03-strategic-reworks-r-a-r-h*
*Completed: 2026-05-21*
