---
phase: 4
review_iteration: 2
reviewers: [codex, cursor]
reviewed_at: 2026-05-22T22:26:16Z
plans_reviewed: [04-01-PLAN.md, 04-02-PLAN.md, 04-03-PLAN.md, 04-04-PLAN.md]
prior_review_iteration: 1 (preserved in git: commit bb16090)
revision_input: 04-REVISION-NOTES.md (planner iter-1 + iter-2 fix-status table)
unavailable_reviewers:
  - gemini: not installed
  - coderabbit: not installed
  - opencode: not installed
  - qwen: not installed
  - claude: skipped (self — this session runs in claude-code)
  - ollama / lm_studio / llama_cpp: no local server detected on default ports
verdict: FAIL (both reviewers) — 20/22 prior findings CONCRETE FIX, 2 PARTIAL; 2 NEW HIGH (loaderObserved.loadErrors semantics + IFF error taxonomy mismatch) + 7 NEW MEDIUM + 3 NEW LOW must be addressed before /gsd:execute-phase 4
---

# Cross-AI Plan Re-Review — Phase 4 (Iteration 2)

**Context.** Iteration 1 of cross-AI review (commit `bb16090`) raised 22 findings (8 HIGH + 9 MEDIUM + 5 LOW). The planner ran `--reviews` mode and revised all four PLAN.md files; the plan-checker then ran two iterations of its own verification loop and surfaced one additional BLOCKER (04-01 task count) plus five WARNINGs which were also fixed. This is iteration-2 of cross-AI review against the REVISED plans, asking Codex + Cursor to verify the fixes landed concretely and surface anything new.

**Consensus on iter-1 fixes:** Both reviewers independently confirm 20/22 findings are CONCRETE FIXES with executable task detail (file paths, API surfaces, regression test names, grep gates). HIGH-5 and HIGH-8 are PARTIAL (managed plugin validation is still dual-path; threat_model understates MEF directory scan side-effects on native DLLs).

**Consensus on new issues:** Both reviewers independently flag the same critical new HIGH — the `loaderObserved.loadErrors: []` assumption in `valid-plugin/expected.json` conflicts with what `PluginLoader.LoadFromDirectory` actually does on a native-only directory. This is a real regression introduced by the HIGH-5 + WARNING-5 fixes.

---

## Codex Review

**Summary**
The revision pass fixed most of the original cross-AI findings concretely: the envelope contract is now explicit, the CLI scaffold has an entry point, stdout capture is corrected, TRE eager-read is specified, PEReader replaces the bad Windows API path, and `validate-plugin` now intends to exercise `PluginLoader`. However, the revised plan set is not ready for `/gsd:execute-phase 4`. The new revisions introduce several execution blockers, especially in 04-03’s IFF error-kind expectations and 04-04’s `PluginLoader` behavior against native DLLs. These are likely to produce failing tests or mismatched goldens during execution.

**Fix Verification**

| Finding | Verdict | Evidence |
|---|---|---|
| HIGH-1 NativeExportProbe API bug | CONCRETE FIX | 04-04 Task 4.1 replaces `LoadLibraryExW/GetProcAddress` with `System.Reflection.Metadata.PEReader`, adds NuGet refs and grep gates forbidding `LoadLibraryEx` / `GetProcAddress`. |
| HIGH-2 04-01 no `Main` / contradictory verify | CONCRETE FIX | 04-01 Task 1.1 lands stub `static int Main(string[] args) => 0;`; Task 1.4 replaces it with dispatch. |
| HIGH-3 JsonOutput bypasses stdout capture | CONCRETE FIX | 04-01 Task 1.2 writes via `writer ?? Console.Out`; adds regression tests and grep gate for zero `Console.OpenStandardOutput`. |
| HIGH-4 TRE stream lifetime | CONCRETE FIX | 04-02 Task 2.1 eager-reads record payloads into `byte[][]`; adds `GetRecordData_AfterOpenStreamDisposed_StillReturnsBytes`. |
| HIGH-5 PluginLoader reuse drift | CONCRETE BUT NEW REGRESSION | 04-04 Task 4.1 now calls `new PluginLoader(autoLoad: false).Load(dir)`, but this likely breaks native fixture expectations. See New Finding HIGH-2. |
| HIGH-6 schemaVersion placement | CONCRETE FIX | 04-01 `<json_contract>` locks root `{ command, result/error, schemaVersion }`; 04-02/03/04 all reference that envelope. |
| HIGH-7 04-02 field drift | CONCRETE FIX | 04-02 `<json_contract>` and Task 2.2 use `header.recordCount`, `records[].id`, `compressionKind`, `objects[].id`. |
| HIGH-8 threat model false PluginLoader claim | CONCRETE FIX | 04-04 threat model now says native path is PE parse, managed path executes through `PluginLoader` and may run static initializers. |
| MED-9 `kind: unknown` drift | CONCRETE FIX | 04-04 JSON contract documents `managed|native|mixed|unknown`; Task 4.1 adds unknown branch. |
| MED-10 managed reflection fragility | PARTIAL | `PluginLoader.Load` is now authoritative, but ReflectionOnly remains for classification and dependency-resolution edge cases remain documented. |
| MED-11 IFF missing pad leniency | CONCRETE BUT NEW EDGE RISK | 04-03 chooses strict behavior and adds fixture/test, but pad-byte validation is specified against `stream.Length`, not `parentEnd`. See New Finding MEDIUM-1. |
| MED-12 real-sample legal risk | CONCRETE FIX | 04-03 removes `real-sample.iff`; uses `synthetic-secondary.iff` only. |
| MED-13 `list-objects` sentinel scan | PERFORMATIVE | 04-02 only documents the byte-scan as architectural debt. No executable boundary validation or false-positive regression test was added. |
| MED-14 PROP container semantics | CONCRETE FIX | 04-03 `ContainerTypeIds = { FORM, LIST, CAT }`; PROP is a leaf with Tier-1 and fixture coverage. |
| MED-15 fragile source-path grep | PARTIAL | 04-04 adds `[AssemblyMetadata]` marker, but still retains a source-walk grep test as secondary. Less risky, but not fully removed. |
| MED-16 phase closure ordering | CONCRETE FIX | 04-04 frontmatter now depends on `[04-01, 04-02, 04-03]`; Task 4.4 also checks summary files before DEC-C3 promotion. |
| MED-17 PEReader NuGet on net472 | CONCRETE FIX | 04-04 Task 4.1 adds `System.Reflection.Metadata` and `System.Collections.Immutable`. |
| LOW-18 xUnit parallelization | CONCRETE FIX | 04-01 Task 1.1 adds `[assembly: CollectionBehavior(DisableTestParallelization = true)]`. |
| LOW-19 Unix snippets | PARTIAL | Many snippets were converted to PowerShell, but some `grep` gates remain. Acceptable if Git tools are assumed, but still mixed. |
| LOW-20 v0006 coverage | CONCRETE FIX | 04-02 adds `synthesized-2record-v0006.tre` and Tier-1/Tier-2 tests. |
| LOW-21 misleading IFF fixture name | CONCRETE FIX | 04-03 renames to `synthetic-nested.iff`. |
| LOW-22 help consistency | CONCRETE FIX | 04-04 adds both verb-specific and top-level help tests. |

**New Findings**

**HIGH — 04-03 IFF negative fixture expectations conflict with parser check order.**  
04-03 Task 3.1 specifies bounds checks in this order: negative length, `ChunkLengthExceedsCap`, `ChunkLengthExceedsFile`, `NestedChunkOverflow`. But the committed negative fixtures expect different errors:

- `malformed-chunk-overflow.iff` declares length `0x7FFFFFFF` and expects `ChunkLengthExceedsFile`; with a 64 MB cap checked first, it will throw `ChunkLengthExceedsCap`.
- `malformed-truncated.iff` declares outer FORM length `100` in a ~30-byte file and expects `Truncated`; with file-bound checking, it will more likely throw `ChunkLengthExceedsFile`.

This will produce failing goldens. Fix by either changing fixture lengths/error expectations or specifying a deterministic error taxonomy.

**HIGH — 04-04 native valid-plugin golden likely contradicts actual `PluginLoader.Load(dir)` behavior.**  
04-04 requires `PluginLoader.Load(dir)` to run once for every directory and expects `valid-plugin/expected.json` to contain `loaderObserved: { pluginsCount: 0, loadErrors: [] }` for `Utinni.CrtMatchPlugin.dll`. A native C++ DLL in a MEF `DirectoryCatalog` is likely to produce a `BadImageFormatException`/load error, not an empty `LoadErrors` list. That means the new “true loader observation” fix can make the valid native fixture fail its own golden. Fix by proving the behavior before locking expected JSON, accepting the native load error in `loaderObserved`, or only running `PluginLoader` against managed-candidate assemblies.

**MEDIUM — 04-03 strict pad-byte logic uses EOF instead of parent boundary.**  
Task 3.1 says if an odd-length chunk has `br.BaseStream.Position < br.BaseStream.Length`, consume a pad byte. For nested chunks, the relevant boundary is `parentEnd`, not file EOF. This can consume a byte outside the parent container and hide malformed nested data. Fix the algorithm to require `Position < parentEnd` for nested chunks and throw if `Position == parentEnd`.

**MEDIUM — 04-04 verification gates contain false-fail grep patterns.**  
04-04 final verification asks for `grep -nE "new PluginLoader\(autoLoad: ?false\)\.Load\("`, but Task 4.1’s implementation constructs `var loader = new PluginLoader(...)` and then calls `loader.Load(dir)` on a later line. That grep will fail even when the code is correct. Task 4.1 also asks for `public DirectoryReport InspectDirectory`, but the implementation is `public static DirectoryReport InspectDirectory`. Fix the verify gates to match the planned code.

**MEDIUM — 04-04 Task 4.1 unknown-kind test depends on future Task 4.3 fixture.**  
Task 4.1’s Test 7 says it consumes `wrong-iplugin-shape` “in a partial form OR builds an inline minimal DLL via Roslyn.” But `wrong-iplugin-shape` is not created until Task 4.3, and Roslyn is not otherwise specified as a dependency. This breaks the atomic task boundary. Make Task 4.1 build the DLL deterministically or move the unknown-kind test to Task 4.3.

**LOW — 04-01 test count is internally inconsistent.**  
04-01 Task 1.2 lists ten `JsonOutputTests` method names, but verification expects 9 tests. Later plan-level verification says 17 total tests from “9 JsonOutput + 4 dispatch Facts + 4 Theory rows,” while Task 1.5 actually describes 3 dispatch Facts plus 4 Theory rows. This is not a design bug, but it will confuse executors and summary gates.

**Risk Assessment**
Overall risk: **HIGH**.

Top risks:

1. The 04-03 IFF malformed-fixture error kinds are internally inconsistent with the parser algorithm, so execute-phase will likely fail Tier-2 goldens.
2. The 04-04 `PluginLoader` fix may make native plugin fixtures report loader errors, contradicting the expected JSON and making the “valid native plugin” golden unstable.
3. Several verification gates are now stricter than the code they instruct the executor to write, creating false failures even if the implementation is otherwise correct.

**REVIEW VERDICT**
**FAIL** — more revision needed before `/gsd:execute-phase 4`.

The high-level architecture is now much stronger than the prior version, but the plan set still contains executable contradictions that are likely to waste implementation time. Fix the IFF error taxonomy, validate or revise the native-plugin `loaderObserved` expectation, and clean up the false-failing grep/test-boundary issues before execution.


---

## Cursor Review (Independent — saw Codex's findings, instructed to cross-check + add new)

## Summary

The `--reviews` revision pass is a major improvement: all eight original HIGH findings now have task-level fixes with file paths, API shapes, regression test names, and grep gates. The plans are **much closer to executable** than the pre-revision set. They are **not quite ready** for `/gsd:execute-phase 4` without one substantive fix: **`validate-plugin`'s `loaderObserved.loadErrors` contract conflicts with real `PluginLoader.Load()` behavior on native-only directories**, which will likely break Tier-1 Test 6 and the `valid-plugin` golden during execution. Everything else is either concrete or acceptable documented debt.

---

## Fix Verification

| ID | Verdict | Evidence |
|----|---------|----------|
| **HIGH-1** NativeExportProbe | **CONCRETE FIX** | `04-04` Task 4.1: `NativeExportProbe.cs` via `PEReader`; grep forbids `LoadLibraryEx`/`GetProcAddress`; PRE-FLIGHT smoke on `Utinni.CrtMatchPlugin.dll`; NuGet `System.Reflection.Metadata` 1.6.0 + `System.Collections.Immutable` 1.5.0 in `Utinni.Cli.csproj`. |
| **HIGH-2** CS5001 / verify contradiction | **CONCRETE FIX** | `04-01` Task 1.1: stub `static int Main(string[] args) => 0` in `Utinni.Cli/Program.cs`; verify requires exe + `static int Main` grep; Task 1.4 replaces body. |
| **HIGH-3** JsonOutput stdout capture | **CONCRETE FIX** | `04-01` Task 1.2: `writer ?? Console.Out`; `EmitSuccess_DefaultWriter_RoutesThroughConsoleOutForSetOutCapture`; grep gate `Console.OpenStandardOutput` → zero matches. |
| **HIGH-4** TreFile stream lifecycle | **CONCRETE FIX** | `04-02` Task 2.1: `byte[][] _recordCompressedBytes` eager-read; `GetRecordData_AfterOpenStreamDisposed_StillReturnsBytes`; verify greps cache field. |
| **HIGH-5** PluginLoader reuse | **PARTIALLY CONCRETE / NEW REGRESSION** | `04-04` Task 4.1 **does** call `new PluginLoader(autoLoad: false).Load(dir)` with grep gates. But Test 6 + `valid-plugin/expected.json` assume `loadErrors: []` for a native-only dir. Actual `PluginLoader.LoadFromDirectory` MEF-composes every `.dll` and records failures via `Error()` → `LoadErrors` (see `PluginLoader.cs:128-139`, `173-177`). Native `CrtMatchPlugin.dll` will almost certainly produce a non-empty `LoadErrors`. Invocation is fixed; **golden semantics are wrong**. Per-plugin checks still use `ReflectionOnlyLoadFrom`, not `loader.Plugins`. |
| **HIGH-6** schemaVersion placement | **CONCRETE FIX** | Locked envelope `{ schemaVersion, command, result \| error }` at root in all four plans; `JsonOutput.cs` wraps at root; envelope regression tests in 04-01, 04-02 Task 2.4, 04-03 Task 3.3, 04-04 Task 4.3. |
| **HIGH-7** TRE JSON field drift | **CONCRETE FIX** | `04-02` Task 2.2 `BuildResult` snippet emits `recordCount`, `compressionKind`, `records[].id = "tre:"+ordinal`, `objects[].id`; matches `<json_contract>` and must_haves. |
| **HIGH-8** threat_model false claim | **PARTIALLY CONCRETE** | Threat table updated for PE-parse native path + truthful `--help`. Still understates that **`PluginLoader.Load(dir)` runs on the whole directory**, so MEF still touches native DLLs even though export probing does not. |
| **MED-9** `kind: unknown` | **CONCRETE FIX** | Documented in `04-04` `<json_contract>`; Test 7; grep gate for `"unknown"` branch. |
| **MED-10** ReflectionOnly fragility | **PARTIALLY ADDRESSED** | `loaderObserved` mitigates observability, but `InspectSingle` still classifies managed shape via `ReflectionOnlyLoadFrom` + `CustomAttributeData`, not `loader.Plugins`. Residual risk documented in pitfalls — acceptable if acknowledged at execute time. |
| **MED-11** pad-byte leniency | **CONCRETE FIX** | `04-03` Task 3.1 STRICT: odd-length EOF without pad → `Truncated`; fixture `malformed-missing-padbyte.iff`; Tier-1 + Tier-2 tests. |
| **MED-12** real-sample legal risk | **CONCRETE FIX** | `real-sample.iff` removed; `synthetic-secondary.iff` only; verify gate `NO file named real-sample.iff`. |
| **MED-13** list-objects byte scan | **DOCUMENTED DEBT** | Pitfall A + inline comment in Task 2.2; Phase 6+ IffReader refactor noted. Acceptable per prior review consensus. |
| **MED-14** PROP as container | **CONCRETE FIX** | `ContainerTypeIds = { FORM, LIST, CAT }`; `Read_CatContainerWithPropChildren_PropClassifiedAsLeaf`; `synthetic-secondary.iff`. |
| **MED-15** source-path grep fragility | **PARTIALLY ADDRESSED** | Primary fix: `[assembly: AssemblyMetadata("validate-plugin-version", "1")]` + Test 8. Test 9 source-walk retained as secondary (still cwd-sensitive). Artifacts block still says "Test 5" for metadata — stale numbering. |
| **MED-16** phase-closure ordering | **CONCRETE FIX** | `04-04` `depends_on: [04-01, 04-02, 04-03]`; Task 4.4 `Test-Path` gate for `04-02-SUMMARY.md` + `04-03-SUMMARY.md`. |
| **#17** PEReader NuGet | **CONCRETE FIX** | Folded into HIGH-1; explicit PackageReferences + lock file regen in Task 4.1. |
| **LOW-18** xUnit parallelization | **CONCRETE FIX** | `04-01` Task 1.1: `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `Utinni.Cli.Tests/Properties/AssemblyInfo.cs`. |
| **LOW-19** Unix verify snippets | **CONCRETE FIX** | PowerShell-native verify blocks across plans (`Test-Path`, `$env:TEMP`, etc.). |
| **LOW-20** v0006 coverage | **CONCRETE FIX** | `synthesized-2record-v0006.tre` + golden + Tier-1/Tier-2 tests in `04-02` Task 2.3/2.4. |
| **LOW-21** "5-chunk" naming | **CONCRETE FIX** | Renamed to `synthetic-nested.iff` throughout `04-03`. |
| **LOW-22** help consistency | **CONCRETE FIX** | `04-04` Task 4.3: `Help_ContainsTEoPMitigationWarning` + `Help_TopLevelListsValidatePluginVerb`. |

---

## New Findings

### HIGH

**1. `loaderObserved.loadErrors: []` is incompatible with `PluginLoader.Load()` on native-only fixture dirs** (`04-04-PLAN.md`, Task 4.1 Test 6, Task 4.3 `valid-plugin/expected.json`, json_contract line ~266)

The plan assumes native-only directories yield empty `LoadErrors` because "MEF DirectoryCatalog scans for managed types only." That is not what the code does: `LoadFromDirectory` iterates every `*.dll` and calls `LoadCatalog(new DirectoryCatalog(...))`, which composes and catches exceptions into `LoadErrors` via `Error()`. Existing `PluginLoaderTests` only cover managed fixtures; there is no evidence native `CrtMatchPlugin.dll` produces empty errors.

**Executor impact:** Tier-1 Test 6 and the `valid-plugin` golden will likely fail on first run unless goldens are baselined against actual loader output or the plan defines filtered/normalized `loadErrors` semantics.

### MEDIUM

**2. Cross-plan wave-ordering contradiction** (`04-02-PLAN.md` verification §9 vs `04-04` frontmatter)

`04-04` now correctly has `depends_on: [04-01, 04-02, 04-03]`, but `04-02` verification still says *"04-03 and 04-04 depend only on 04-01 … may proceed in parallel with 04-02."* Stale text from pre-iter-2; could mislead an executor about legal plan order before DEC-C3 promotion.

**3. `ValidatePluginCommand` artifacts still describe old return type** (`04-04-PLAN.md`, artifacts ~line 59)

Artifacts say ValidatePluginCommand *"converts the `IReadOnlyList<PluginReport>`"* while Task 4.2 correctly uses `DirectoryReport`. Tasks are right; artifacts are stale.

**4. Managed plugin validation is still dual-path, not loader-authoritative** (`04-04` Task 4.1 `InspectSingle`)

`loaderObserved` captures loader output, but per-DLL `managedIPlugin` / check pass-fail still come from `ReflectionOnlyLoadFrom`. A valid managed plugin that loads via MEF but fails ReflectionOnly attribute scraping could report `kind=unknown` or failing checks while `pluginsCount >= 1`. MEDIUM-10 residual is real, not fully closed.

**5. `04-04` Task 4.3 native fixture build depends on `cl.exe`** (Task 4.3 PRE-FLIGHT)

Three negative fixture DLLs need MSVC toolchain or a `checkpoint:human-action` fallback. Documented, but this is the highest execution-friction item after the loaderObserved issue.

### LOW

**6. `04-01` JsonOutput test count mismatch** (`04-01` Task 1.2)

Behavior lists **10** named `[Fact]` tests; verify/done say **9** passing; plan verification totals **17** CLI tests using 9 JsonOutput count. Off-by-one; won't block execution but verify gates will confuse the executor.

**7. CON-O-09 resolution text still says "5-chunk IFF"** (`04-02` Task 2.5)

Fixture was renamed to `synthetic-nested.iff` in `04-03`. Cosmetic doc drift only.

**8. `04-04` artifacts reference "PluginInspectionTests Test 5" for AssemblyMetadata** (artifacts ~line 57)

Task renumbering made metadata Test **8**. Stale cross-reference.

---

## Risk Assessment

**Overall risk: MEDIUM**

Top 3 risks:

1. **`validate-plugin` golden mismatch on `loaderObserved.loadErrors`** — highest probability of a red CI lane on first `04-04` execution; fix is bounded (rebaseline golden + Test 6 against actual loader output, or define normalization rules in Task 4.1).

2. **PEReader manual export-table walk on net472** — plan is detailed and PRE-FLIGHT gated, but PE RVA→file-offset math is easy to get wrong; mitigated by mandatory CrtMatchPlugin smoke test before goldens lock.

3. **`list-objects` OBJS sentinel scan** — explicitly deferred architectural debt; fine for synthesized fixtures, risky if later real ws.iff samples are added without the Phase 6+ IffReader refactor.

---

## REVIEW VERDICT

**FAIL** — one targeted revision pass needed before `/gsd:execute-phase 4`.

The original eight HIGH blockers are substantively addressed with executable task detail (not performative one-liners). The revision pass did **not** introduce major architectural regressions. The remaining blocker is **new/regression-level**: iter-2's `loaderObserved` hoisting fixed per-plugin duplication but baked in an incorrect assumption about empty `LoadErrors` on native plugin directories, which contradicts the actual `PluginLoader.cs` contract the plan now correctly invokes.

**Minimum fix to reach PASS:**

1. Run `PluginLoader.Load()` against a temp dir containing only `Utinni.CrtMatchPlugin.dll` and record actual `LoadErrors`.
2. Update `04-04` Task 4.1 Test 6, Task 4.3 `valid-plugin/expected.json`, and json_contract prose to match reality (or document explicit filtering if that is the intended semantics).
3. Scrub stale cross-plan text in `04-02` verification §9 and `04-04` artifacts line 59.

After that, the plan set is in good shape to execute. I would not recommend shipping as-is without fixing item 1 — it is the kind of issue that burns executor time on golden rebaseline churn mid-phase.


---

## Consensus Summary (Codex + Cursor, iter-2)

### Agreed Strengths (both reviewers)

- All 8 prior HIGH findings have task-level concrete fixes: file paths, API surfaces, regression test names, grep gates. Not performative.
- D-01 clean-room posture STRENGTHENED (no real-sample.iff committed; purely synthesized fixtures).
- Envelope shape locked uniformly across all 4 plans (top-level `schemaVersion` + `command`).
- Wave structure preserved; depends_on bumped on 04-04 to capture phase-closure ordering.
- NuGet additions for `System.Reflection.Metadata` 1.6.0 + `System.Collections.Immutable` 1.5.0 are explicit, lock file regenerated.

### Agreed New Concerns — Priority-Ordered

**HIGH (must fix before /gsd:execute-phase 4):**

1. **`loaderObserved.loadErrors: []` is incompatible with `PluginLoader.LoadFromDirectory` on native-only fixture dirs** [Codex + Cursor, both reference `PluginLoader.cs:128-139, 173-177`]. The iter-2 WARNING-5 hoisting moved `loaderObserved` to top-level and added a regression-guard test (`LoaderObservedAtTopLevelResult`) — but the GOLDEN ASSUMES `loadErrors: []` for a native-only directory containing `Utinni.CrtMatchPlugin.dll`. Reality: `LoadFromDirectory` iterates every `*.dll` and creates a `DirectoryCatalog`, which composes managed types and catches exceptions into `LoadErrors` via the `Error()` callback. A native C++ DLL will almost certainly produce a non-empty `LoadErrors` (likely `BadImageFormatException` or a MEF compose failure). **The HIGH-5 invocation is correct; the goldens are wrong.** Fix: run `PluginLoader.Load()` against a temp dir with only `Utinni.CrtMatchPlugin.dll`, capture actual `LoadErrors`, baseline the goldens against that — OR define explicit `loadErrors` filtering/normalization semantics in Task 4.1 (e.g., filter out `BadImageFormatException` from native DLLs).

2. **[Codex-only HIGH] 04-03 IFF negative fixture expectations conflict with parser check order** [Codex]. Task 3.1 specifies bounds checks in the order: negative length → `ChunkLengthExceedsCap` (64 MB) → `ChunkLengthExceedsFile` → `NestedChunkOverflow`. But the committed negative fixtures expect:
   - `malformed-chunk-overflow.iff` declares length `0x7FFFFFFF` and expects `ChunkLengthExceedsFile` → will actually throw `ChunkLengthExceedsCap` (because 0x7FFFFFFF > 64MB cap, which is checked first).
   - `malformed-truncated.iff` declares outer FORM length `100` in a ~30-byte file and expects `Truncated` → will more likely throw `ChunkLengthExceedsFile` (file-bound check fires first).
   Fix: either reorder the parser checks, change the fixture declared lengths, or change the expected `error.kind` values to match what the algorithm actually produces.

**MEDIUM:**

3. **04-03 strict pad-byte uses EOF (`stream.Length`) instead of parent boundary (`parentEnd`)** [Codex]. For nested chunks, the relevant boundary is the parent container's end, not file EOF. The current spec can consume a pad byte that belongs to the parent's slack space and hide malformed nested data. Fix: pad-byte consumption must check `Position < parentEnd`, not `Position < stream.Length`. Throw if `Position == parentEnd` with odd-length.

4. **04-04 verify gates contain false-fail grep patterns** [Codex]. Task 4.1's verify asks for a grep pattern that requires `new PluginLoader(autoLoad:false).Load(...)` on ONE line, but the implementation constructs `var loader = new PluginLoader(autoLoad: false); loader.Load(dir);` on TWO lines. The grep will report zero matches even when the code is correct. Also: verify asks for `public DirectoryReport InspectDirectory`, but the implementation is `public static DirectoryReport InspectDirectory`. Fix: relax grep patterns to match the actual code shape (allow split assignment + invocation; allow `static` modifier).

5. **04-04 Task 4.1 Test 7 (unknown-kind) depends on Task 4.3 fixture (`wrong-iplugin-shape`)** [Codex]. Task 4.1's Test 7 says it consumes `wrong-iplugin-shape` "in a partial form OR builds an inline minimal DLL via Roslyn." Task 4.3 is where that fixture is created. Atomic task boundary broken. Fix: either move Test 7 to Task 4.3, or have Task 4.1 build a deterministic inline DLL (and add Roslyn as an explicit test-project dependency).

6. **04-02 verification §9 stale "04-04 depends only on 04-01" text** [Cursor]. After the WARNING-4 depends_on bump, `04-02-PLAN.md` line 640 still says "04-03 and 04-04 depend only on 04-01 … may proceed in parallel with 04-02." Inconsistent with `04-04` frontmatter `depends_on: [04-01, 04-02, 04-03]`. Fix: update the prose to reflect the bumped contract.

7. **04-04 artifacts still describe `IReadOnlyList<PluginReport>` return type** [Cursor]. After the WARNING-5 hoist, `ValidatePluginCommand.Run` correctly emits the new `DirectoryReport` shape, but `04-04-PLAN.md` line ~59 artifacts block still says ValidatePluginCommand "converts the `IReadOnlyList<PluginReport>`". Fix: update artifacts text.

8. **04-04 managed plugin validation still dual-path; per-DLL checks remain ReflectionOnly-based** [Cursor]. `loaderObserved` captures whole-directory loader output, but per-DLL `managedIPlugin` shape checks in `InspectSingle` still use `ReflectionOnlyLoadFrom` + `CustomAttributeData` — NOT `loader.Plugins`. A managed plugin that loads via MEF (so `pluginsCount >= 1`) but fails ReflectionOnly attribute scraping (because of inherited Exports or dependency resolution) could report `kind=unknown` or failing checks. Document the residual risk in pitfalls OR use `loader.Plugins` for per-DLL signal.

9. **04-04 Task 4.3 native fixture build depends on `cl.exe`** [Cursor]. Three negative fixture DLLs need MSVC toolchain or a `checkpoint:human-action` fallback. Documented, but this is the highest execution-friction item after the loaderObserved issue. Not a blocker — but worth pre-validating that `cl.exe` is available in the dev environment.

**LOW:**

10. **04-01 JsonOutput test count off-by-one** [Codex + Cursor]. Task 1.2 behavior names 10 `[Fact]` tests; verify/done assert "9 tests passing"; plan-level verification totals 17 (using "9 JsonOutput + 4 dispatch Facts + 4 Theory rows"). Pick one count, propagate consistently. Cosmetic for execution, will confuse executor briefly.

11. **04-02 CON-O-09 resolution text references "5-chunk IFF"** [Cursor]. Stale after the LOW-21 rename to `synthetic-nested.iff`. Cosmetic doc drift.

12. **04-04 artifacts reference "PluginInspectionTests Test 5" for AssemblyMetadata marker** [Cursor]. Task renumbering moved the test to Test 8 (per the MEDIUM-15 fix). Cosmetic stale cross-reference.

13. **04-01/02/03/04 some grep verify gates still use Unix-style invocations** [Codex]. Mostly worked under git-bash's grep on Windows, but a few `grep -nE` calls with escaped backslash paths are fragile. Defer to Phase 6 polish — not a blocker.

### Iter-1 Items Promoted from MEDIUM to "Residual"

- **MED-10** (managed plugin reflection fragility) and **MED-15** (source-walk test) are PARTIALLY addressed. The HIGH-5 fix made PluginLoader.Load authoritative for directory-level observation, but per-DLL classification still uses ReflectionOnly. Acceptable if the residual is acknowledged at execute time; not blocking.

### Divergent Views

- **Codex** flagged the IFF error-taxonomy mismatch (New Finding HIGH-2) as a hard execute-phase failure. **Cursor** focused on the `loaderObserved` issue and didn't surface the IFF error-kind problem. Both are legitimate; the IFF issue is independently verifiable by tracing fixture byte counts against parser check ordering.
- **Cursor** flagged the cross-plan stale-text drift (04-02 verification §9, 04-04 artifacts) as MEDIUM. **Codex** didn't surface these. They're real but cosmetic-tier — they could mislead an executor but won't break the build.

---

## Recommended Path Forward

**Option A (recommended) — One more targeted `--reviews` pass:**

```
/gsd:plan-phase 4 --reviews
```

Fix: (a) `loaderObserved.loadErrors` semantics (validate against actual loader behavior + rebase goldens or define filter), (b) IFF parser check order vs fixture expectations, (c) verify-gate false-fail grep patterns, (d) cross-plan stale text in 04-02 §9 and 04-04 artifacts, (e) Task 4.1 Test 7 atomic-boundary fix. ~30 min planner pass.

**Option B — Surgical patch:** Use Edit tool against the specific tasks for the 2 NEW HIGH concerns + the 5 MEDIUMs. Faster (~15-20 min) but the IFF and PluginLoader fixes need real I/O validation, so Option A is lower-risk.

**Option C — Ship as-is and fix during execute-phase:** NOT RECOMMENDED. Both reviewers independently flagged the same critical HIGH (`loaderObserved.loadErrors`), which will produce a red CI lane on first 04-04 execution. Fixing during execute means churning goldens mid-phase — slower than just fixing the plan now.

**Two-reviewer adversarial coverage achieved this run:** Codex (OpenAI baseline) + Cursor (different model family). Both independently caught the `loaderObserved` regression introduced by the iter-2 WARNING-5 fix — exactly the signal the cross-AI review pattern is designed to produce. The iter-1 fixes themselves are confirmed substantive by both reviewers.
