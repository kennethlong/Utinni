---
phase: 4
review_iteration: 3
reviewers: [codex, cursor]
reviewed_at: 2026-05-22T23:24:00Z
plans_reviewed: [04-01-PLAN.md, 04-02-PLAN.md, 04-03-PLAN.md, 04-04-PLAN.md]
prior_review_iterations:
  - iter-1: commit bb16090 (FAIL — 22 findings)
  - iter-2: commit 6431991 (FAIL — 12 new findings against iter-1+iter-2 plans)
revision_input: 04-REVISION-NOTES.md (planner iter-1 + iter-2 + iter-3 fix-status tables)
unavailable_reviewers:
  - gemini: not installed
  - coderabbit: not installed
  - opencode: not installed
  - qwen: not installed
  - claude: skipped (self — this session runs in claude-code)
  - ollama / lm_studio / llama_cpp: no local server detected on default ports
verdict: SPLIT — Codex FAIL (1 HIGH IFF math contradiction unresolved + 1 MEDIUM managed-fallback kind contradiction); Cursor PASS-WITH-GUARDRAILS (same managed-fallback flagged MEDIUM + AssemblyInfo.cs clobber risk MEDIUM + 3 LOW stale-text). Codex's IFF math catch is independently verifiable and correct.
---

# Cross-AI Plan Re-Review — Phase 4 (Iteration 3)

**Context.** Iter-1 + iter-2 + iter-3 = three planner revision passes addressing 22 + 12 = 34 distinct cross-AI findings. The iter-3 replan (commit `941b5e1`) addressed all 12 iter-2 findings. This iter-3 review checks whether the fixes landed AND whether new issues were introduced.

**Consensus on iter-3 fixes:** Both reviewers independently confirm 10/12 are CONCRETE FIX with executable task detail. Two are flagged as incomplete or contradictory:

- **iter-3 HIGH-2 (IFF fixture redesign):** Codex says NEW REGRESSION (the `malformed-truncated.iff` byte-layout math is internally impossible); Cursor says CONCRETE FIX (math checks out). **Codex is correct upon independent verification** — see analysis below.
- **iter-3 MED-9 (cl.exe + managed fallback):** Both reviewers flag the same contradiction — managed-fallback DLLs would classify as `kind=unknown` per Task 4.1's `InspectSingle` rules, not `kind=managed` as the `must_haves` and golden branching claim.

**Verdict divergence:** Codex FAIL, Cursor PASS-WITH-GUARDRAILS. The divergence is real (Codex is stricter on "plans must be internally self-consistent"; Cursor is more pragmatic about "MSVC is present on dev + CI machines, so the managed-fallback contradiction is a no-op in practice"). User judgment is appropriate.

---

## Codex Review

**Summary**

Iter-3 fixed most of the specific documentation and task-boundary problems concretely, especially `FilterNativeLoadErrors`, `loaderObserved` placement, the `parentEnd` pad-byte boundary, relaxed grep gates, and stale cross-references. However, the plan set is **not ready** for execute-phase: 04-03 still contains an executable contradiction in the IFF error taxonomy/fixture math, and 04-04’s managed fallback path for plugin fixtures contradicts the actual `kind` classification/check logic.

**Iter-3 Fix Verification**

| Finding | Verdict | Evidence |
|---|---|---|
| HIGH-1 `FilterNativeLoadErrors` | CONCRETE FIX | 04-04 documents the exact patterns at `04-04-PLAN.md:43`, `:281`, implements helper at `:454`, wires it before `LoaderObservation` at `:477`, tests it at `:365`, and updates goldens/verify expectations at `:639`, `:660`. The managed-no-export uncertainty is honestly handled with a pre-flight probe at `:354`, `:371`. |
| HIGH-2 IFF fixture redesign/check order | NEW REGRESSION | `malformed-chunk-overflow` is fixed (`04-03-PLAN.md:37`, `:76`, `:317`), but `malformed-truncated` is internally impossible as written: root length 200 must either pass with a ~210-byte file or truncate at offset 100, not both (`:38`, `:79`, `:319`). Also the parser’s check 3 uses `parentEnd` (`:348`), which conflicts with the later `NestedChunkOverflow` expectation (`:318`, `:349`). |
| MED-3 parentEnd pad-byte boundary | CONCRETE FIX | 04-03 specifies `parentEnd`, not stream length, for pad consumption and throws at boundary (`04-03-PLAN.md:180`, `:354-355`), with EOF and nested-parent tests (`:315-316`, `:371-372`) and verify gates (`:386`). |
| MED-4 false-fail grep gates | CONCRETE FIX | 04-04 split the PluginLoader constructor/load greps and relaxed optional `static` on `InspectDirectory` (`04-04-PLAN.md:515`). |
| MED-5 unknown-kind test boundary | CONCRETE FIX | Test 7 is explicitly moved to Task 4.3 where the fixture exists (`04-04-PLAN.md:141`, `:362`, `:657`, `:660`). |
| MED-6 stale 04-02 dependency text | CONCRETE FIX | 04-02 now says 04-04 depends on 04-01, 04-02, and 04-03 (`04-02-PLAN.md:123`, `:651`). |
| MED-7 stale return type text | CONCRETE FIX | 04-04 artifacts now describe `DirectoryReport`, not `IReadOnlyList<PluginReport>` (`04-04-PLAN.md:60`, `:142`, `:552`). |
| MED-8 loader-vs-reflection warn | CONCRETE FIX | `directoryChecks` is in the contract (`04-04-PLAN.md:243-289`), POCO/action (`:427`, `:477-480`), emission (`:559`), and tests/verify (`:660`). |
| MED-9 cl.exe fallback | PARTIAL | Discovery/fallback is actionable (`04-04-PLAN.md:605-610`), but the fallback says managed stubs emit `kind: managed` while the classification logic makes them `unknown` unless `managedIPlugin` or `managedIEditorPlugin` is true (`:489-497`). See new finding. |
| LOW-10 JsonOutput count | PARTIAL | Most counts are fixed to 10/18 (`04-01-PLAN.md:356`, `:541`, `:577`), but Task 1.2 still says “nine [Fact] tests” before listing 10 (`:341`). |
| LOW-11 stale “5-chunk IFF” | CONCRETE FIX | CON-O-09 wording now says `synthetic-nested IFF` (`04-02-PLAN.md:124`, `:617`). |
| LOW-12 stale Test 5 reference | CONCRETE FIX | AssemblyMetadata artifact text now references Test 8 (`04-04-PLAN.md:58`, `:145`, `:783`). |

**New Findings**

**HIGH — 04-03 still has contradictory IFF error-kind mechanics.**  
`ReadChunk` check 3 throws `ChunkLengthExceedsFile` when `Position + length > parentEnd` (`04-03-PLAN.md:348`). That means the “nested child declares length 60 inside parent length 40” case described as `NestedChunkOverflow` (`:318`) will be caught earlier as `ChunkLengthExceedsFile`, not `NestedChunkOverflow`. Separately, `malformed-truncated.iff` says the outer FORM length 200 passes because the file is ~210 bytes, while the same file “truncates at offset 100” so the inner read hits EOF (`:38`, `:79`, `:319`). Those cannot both be true under the described stream-length checks.

**MEDIUM — 04-04 managed fallback branch contradicts `kind` and check semantics.**  
The fallback managed stubs use `[Export(typeof(SomeBogusInterface))]` and no `IPlugin`/`IEditorPlugin` export (`04-04-PLAN.md:607-610`). But `kind` is `managed` only when `managedIPlugin || managedIEditorPlugin` is true (`:489-497`); otherwise it is `unknown`. That conflicts with the fallback prose saying goldens branch to `kind: managed` (`:319`, `:610`) and with must-haves expecting native-style missing-create/missing-destroy outcomes (`:39-41`). On a machine without `cl.exe`, the fallback can produce different checks than the plan says to golden.

**LOW — 04-04 Task 4.1 test count is still off.**  
The plan says Task 4.1 ships “8 tests” while naming Tests 1-6, 8, 9, and 10, which is 9 tests (`04-04-PLAN.md:141`, `:362`, `:515`, `:517`, `:720`). This is not a design blocker, but it will confuse execute verification.

**Risk Assessment**

Overall risk: **HIGH**.

Top risks:

1. 04-03’s malformed IFF fixtures still do not deterministically map to the claimed `error.kind` values.
2. 04-04’s no-`cl.exe` fallback may generate goldens that contradict the inspection logic.
3. Remaining test-count drift is low severity but will slow executor verification.

**REVIEW VERDICT**

**FAIL** — more revision needed before `/gsd:execute-phase 4`.

Minimum fix: correct the 04-03 taxonomy so `ChunkLengthExceedsFile`, `NestedChunkOverflow`, and `Truncated` have mutually reachable byte layouts under the stated algorithm; then make 04-04’s managed fallback expected `kind`/checks match the actual classification logic, or change the fallback DLL shape so it truly produces the documented branch.


---

## Cursor Review

## Summary

The iter-3 revisions are **substantive and mostly executable**. The two iter-2 blockers — `FilterNativeLoadErrors` for `loaderObserved.loadErrors` and the IFF fixture/parser alignment — are concretely specified with code shapes, tests, verify gates, and golden semantics. The plan set is **ready for `/gsd:execute-phase 4` on the primary path** (windows-2022 + MSVC, which GitHub Actions provides). I would not require a full iter-4 replan, but executors should watch three **new internal contradictions** in 04-04 (managed fallback vs `InspectSingle` rules, `must_haves` vs `unknown` kind, and a possible `AssemblyInfo.cs` clobber) that could cause friction off the happy path.

---

## Iter-3 Fix Verification

| ID | Verdict | Evidence |
|----|---------|----------|
| **HIGH-1** `FilterNativeLoadErrors` | **CONCRETE FIX** | `04-04-PLAN.md` Task 4.1 (~434–470): `PluginInspectionFilters.FilterNativeLoadErrors` with three patterns; wired before `LoaderObservation.LoadErrors`; documented in `<json_contract>` (~281), `<pitfalls>` Pitfall A (~315), `must_haves` (~38); Test 10 + verify grep (~515); all four `expected.json` assume filtered `loadErrors: []`. Matches `PluginLoader.cs:137–138` error shape (`BadImageFormatException` in message). |
| **HIGH-2** IFF fixture redesign + locked check order | **CONCRETE FIX** | `04-03-PLAN.md` Task 3.1 (~345–357): order NegativeLength → Cap → File → NestedOverflow. `BuildChunkLengthExceedsFile()` at `0x00100000` in ~30-byte file → `ChunkLengthExceedsFile` (8 + 1_048_576 > 30, under 64MB cap). `BuildTruncatedFile()` outer FORM len 200 in ~210-byte file, inner 180-byte claim, truncate at 100 → `Truncated` on payload read. Task 3.3 verify gates (~malformed-chunk-overflow / malformed-truncated) lock golden `error.kind`. Math checks out. |
| **MED-3** `parentEnd` pad boundary | **CONCRETE FIX** | `04-03-PLAN.md` Task 3.1 (~352–357): pad path uses `Position < parentEnd` / `>= parentEnd` throw; Test 6b `Read_OddLengthLeafAtParentEnd_NoPad_ThrowsTruncated`; fixture builder `BuildOddLengthLeafAtParentEndNoPad()`; verify greps for `parentEnd` and zero EOF-based pad checks (~verify block). |
| **MED-4** False-fail verify greps | **CONCRETE FIX** | `04-04-PLAN.md` Task 4.1 verify (~515): split `new PluginLoader(autoLoad: ?false)` + `loader.Load(dir)` count gates; `public (static )?DirectoryReport InspectDirectory` allows `static`. |
| **MED-5** Test 7 atomic boundary | **CONCRETE FIX** | Test 7 moved to Task 4.3 (~655+): `InspectDirectory_UnknownKindFixture_AllChecksFail` consumes `wrong-iplugin-shape` in same commit; Task 4.1 ships 8 tests only (~362). |
| **MED-6** 04-02 §9 stale text | **CONCRETE FIX** | `04-02-PLAN.md` verification item 9 (~651): explicitly states 04-04 depends on 04-01 + 04-02 + 04-03. |
| **MED-7** Artifacts `DirectoryReport` | **CONCRETE FIX** | `04-04-PLAN.md` artifacts (~59–60): `ValidatePluginCommand` converts `DirectoryReport`, not `IReadOnlyList<PluginReport>`. |
| **MED-8** `directoryChecks` / loader-vs-reflection | **CONCRETE FIX** | `04-04-PLAN.md` Task 4.1 (~479), `<json_contract>` (~233–236, ~281–282), Task 4.2 emission (~558), ValidatePluginCommandTests Test 5 (~653). Envelope extended cleanly; `warn` does not affect `overallStatus`. |
| **MED-9** cl.exe discovery + managed fallback | **PARTIAL** | Task 4.3 PRE-FLIGHT (~596–613) is actionable (where.exe, VS paths, three-path strategy). **But** fallback prose contradicts `InspectSingle` rules and `must_haves` — see New Findings. |
| **LOW-10** JsonOutput test count | **PARTIAL** | Fixed in 04-01 verify/done/plan-level §3 (~116, ~356, ~541, ~577). **Still stale:** action block says "nine [Fact] tests" (~341); 04-02 Task 2.4 verify (~594) and plan verification (~645) still say "04-01 **17** + …". |
| **LOW-11** CON-O-09 "5-chunk IFF" | **CONCRETE FIX** | `04-02-PLAN.md` Task 2.5 (~617): `synthetic-nested IFF`. |
| **LOW-12** Artifacts Test 8 reference | **CONCRETE FIX** | `04-04-PLAN.md` artifacts (~57–58): "PluginInspectionTests Test 8". |

---

## New Findings

### MEDIUM — MED-9 managed fallback contradicts `InspectSingle` + `must_haves`

**Where:** `04-04-PLAN.md` Task 4.3 (~607–635) vs Task 4.1 `InspectSingle` (~494) vs `must_haves` (~39–41)

Task 4.3 claims managed fallback preserves `createplugin-export=fail` "the same way" as native. But Task 4.1 explicitly sets **managed → `createplugin-export` / `destroyplugin-export` = `n/a`**, not `fail`. A managed stub with bogus `[Export]` would also classify as **`kind=unknown`**, not `kind=native` or `kind=managed`.

`must_haves` hardcode:
- `missing-createplugin/` → **`kind=native`** + `createplugin-export=fail` (~39)
- `wrong-iplugin-shape/` → **`kind=managed`** (~41)

…while Test 7 accepts **`kind=managed` OR `kind=unknown`** (~655+). On a no-`cl.exe` machine, golden generation and phase verification can diverge.

**Impact:** Low on GitHub Actions (MSVC present); real on agent-only or cross-compiling environments.

---

### MEDIUM — Task 4.1 may clobber `AssemblyInfo.cs` from 04-01

**Where:** `04-04-PLAN.md` Task 4.1 (~382–391)

Says **"Create"** `Utinni.Cli/Properties/AssemblyInfo.cs` with only `AssemblyMetadata` + `InternalsVisibleTo`. Plan 04-01 Task 1.1 already created this file with **`[assembly: CollectionBehavior(DisableTestParallelization = true)]`** (LOW-18). The snippet does not preserve it.

**Impact:** Possible `Console.SetOut` test races in `Utinni.Cli.Tests` if executor follows the snippet literally.

---

### LOW — Test 10 API name drift in `<behavior>` vs `<action>`

**Where:** `04-04-PLAN.md` Task 4.1 (~365 vs ~510)

`<behavior>` calls `PluginInspection.FilterNativeLoadErrors`; `<action>` implements `PluginInspectionFilters.FilterNativeLoadErrors`. Minor executor confusion only.

---

### LOW — Cross-plan test-count drift (LOW-10 incomplete propagation)

**Where:** `04-02-PLAN.md` Task 2.4 verify (~594), verification §3 (~645); `04-03-PLAN.md` verification (~514)

Still references **"Plan 04-01 17"** instead of **18**. Won't break builds; may confuse summary gates.

---

### LOW — `wrong-iplugin-shape` golden expects `kind=managed` but discrimination yields `unknown`

**Where:** `04-04-PLAN.md` `must_haves` (~41) vs Task 4.1 kind logic (~488–494)

A DLL with `[Export(typeof(SomeBogusInterface))]` has no `InheritedExport(typeof(IPlugin))` → `hasManaged=false`, no native exports → **`kind=unknown`**. Golden locked to `kind=managed` may fail unless the fixture is shaped to trigger managed classification (not fully specified).

---

## Risk Assessment

**Overall: MEDIUM**

Top 3 risks:

1. **Plugin negative-fixture semantics on managed-fallback path** — internal plan contradiction between MED-9, `InspectSingle`, and `must_haves`; CI with MSVC is fine, fallback path is not.
2. **`AssemblyInfo.cs` overwrite dropping test serialisation** — regression of 04-01 LOW-18 if Task 4.1 snippet is copied verbatim.
3. **PEReader export-table walk on real `CrtMatchPlugin.dll`** — well mitigated by PRE-FLIGHT smoke (Task 4.1), but still the highest implementation-risk surface in 04-04.

---

## REVIEW VERDICT

**PASS** — ready for `/gsd:execute-phase 4`.

The iter-2 blockers that caused FAIL are **concretely closed** in iter-3. IFF fixture math and parser check order align; `FilterNativeLoadErrors` is specified end-to-end with tests and golden semantics grounded in actual `PluginLoader.cs` behavior. Remaining issues are **executor-friction / secondary-path** items, not plan-level architecture gaps. Recommended execute-time guardrails (no replan required):

1. **Merge** into `AssemblyInfo.cs` (do not replace): keep `DisableTestParallelization` + add metadata/`InternalsVisibleTo`.
2. **Treat native C++ plugin fixtures as required for CI**; document managed fallback as best-effort only, or align `InspectSingle`/`must_haves` if fallback must be first-class.
3. **Generate `wrong-iplugin-shape/expected.json` from actual output** and reconcile `kind` with Test 7's flexible assertion.


---

## Consensus Summary (Codex + Cursor, iter-3)

### Agreed CONCRETE FIXES (10 of 12)

Both reviewers independently confirm these iter-3 fixes landed with executable task detail:
- HIGH-1 `FilterNativeLoadErrors` (drops native-DLL MEF composition errors; wired before `LoaderObservation.LoadErrors`; documented in 4 places; goldens updated; PRE-FLIGHT probe added)
- MED-3 pad-byte boundary at `parentEnd`
- MED-4 verify-gate regex looseness
- MED-5 Test 7 moved to Task 4.3
- MED-6 04-02 §9 stale text
- MED-7 04-04 artifacts DirectoryReport text
- MED-8 loader-vs-reflection cross-check (`directoryChecks` field, `warn` status)
- LOW-11 04-02 CON-O-09 "synthetic-nested" rename
- LOW-12 04-04 artifacts "Test 8" reference

### Agreed Unresolved or Newly Surfaced Concerns

**HIGH — 04-03 `malformed-truncated.iff` byte layout is internally impossible (Codex flagged; verified upon independent inspection)**

The plan describes `malformed-truncated.iff` (04-03-PLAN.md:79) as: "outer FORM declares length 200 in a ~210-byte file (passes cap; passes file-bound). The FORM contains an inner sub-chunk header claiming 180 bytes of payload, but the file actually truncates at offset 100 — the recursive descent into the inner chunk reads past EOF mid-payload and throws Truncated."

These two statements are mutually exclusive. Either:
- File is **210 bytes**: outer length 200 passes file-bound (208 ≤ 210 ✓); inner chunk header at offset 16 claims 180 bytes → payload runs offset 20–200, all within the 210-byte file → inner payload reads completely → **Truncated never fires**.
- File is **100 bytes**: outer length 200 fails file-bound (208 > 100) → **`ChunkLengthExceedsFile` fires first** for the outer chunk → Truncated never reached.

Codex's catch: "Those cannot both be true under the described stream-length checks." Confirmed by algebra against the locked check order (Cap → File → Nested).

**Fix paths (pick one):**

1. **Change the algorithm**: do file-bound check only for the TOP-LEVEL chunk (implicit "FORM is the file"); nested chunks use parent-bound check (NestedChunkOverflow); leaf chunks rely on streaming-read raising Truncated when actual stream hits EOF before declared payload length. This is the conventional EA-IFF-85 approach and matches how the algorithm was likely intended. Update Task 3.1 to specify this.

2. **Change the fixture**: rebuild `BuildTruncatedFile()` to land in Truncated via the missing-pad-byte path (which IS reachable under MED-3 fix), and rename the fixture to something descriptive of that case. Lose the "FORM-200-inner-180-file-100" example.

3. **Document Truncated as reachable only via missing-pad-byte at parentEnd**: remove the malformed-truncated.iff fixture entirely; the existing `malformed-missing-padbyte.iff` (MED-11 + MED-3) covers the only reachable Truncated path under the strict cap/file/nested order.

**Recommendation: Fix path 1.** Algorithm change is the right answer; it matches IFF semantics, makes Truncated truly reachable via streaming-read in nested chunks, and the byte layout described in the plan (file 210, outer 200, inner 180 with file truncating at 100) THEN makes sense — outer is "the file" (no file-bound check), inner declares 180 of payload but stream ends after 80 bytes → Truncated fires on streaming read.

**MEDIUM — 04-04 managed-fallback DLLs would classify as `kind=unknown`, not `kind=managed` (Codex + Cursor consensus)**

Task 4.3 PRE-FLIGHT fallback (`04-04-PLAN.md:607-610`) builds managed stub DLLs with `[Export(typeof(SomeBogusInterface))]` — no IPlugin export, no native createPlugin export. Per Task 4.1 `InspectSingle` rules (`:488-494`): `kind=managed` requires `managedIPlugin || managedIEditorPlugin == true`. Bogus-export DLL → both false → **`kind=unknown`**.

But:
- `must_haves` (`:39`) says `missing-createplugin/` → `kind=native` + `createplugin-export=fail` (must_haves assume native fixtures)
- `must_haves` (`:41`) says `wrong-iplugin-shape/` → `kind=managed`
- Task 4.3 fallback prose says goldens branch to `kind: managed` (`:319`)

These three statements are mutually inconsistent on the managed-fallback path. On the **native** (cl.exe-present) path, the contradiction is a no-op — the fixtures ship as native DLLs. On the **managed-fallback** path (no cl.exe), the executor will produce DLLs that classify as `kind=unknown` while the goldens demand `kind=managed`.

**Fix paths (pick one):**

1. **Drop the managed fallback**: require cl.exe at execute-time. Per user memory `project_vs2026_toolchain.md`, VS 2026 is installed locally; GitHub Actions windows-2022 runners include MSVC; this is a safe requirement. Replace MED-9 fallback prose with "if cl.exe is missing, execute-phase fails; install VS Build Tools to proceed."

2. **Reshape the managed-fallback stubs**: managed `missing-createplugin/` becomes a managed DLL that DOES export `[InheritedExport(typeof(IPlugin))]` (so `kind=managed`) but lacks the native createPlugin export (which native fixtures have via cl.exe). Native-fixture must_haves stay; managed fallback's stubs are designed to land in `kind=managed` with the same `createplugin-export=fail` outcome.

3. **Document the contradiction as best-effort**: keep the managed fallback but explicitly mark it as "fallback path — phase verification may show kind=unknown on machines without cl.exe; this is acceptable; goldens are calibrated for the native path."

**Recommendation: Fix path 1.** MSVC is universally available on Windows dev + CI; the managed-fallback complexity isn't worth the inconsistency.

**MEDIUM — Task 4.1 may CLOBBER `Utinni.Cli/Properties/AssemblyInfo.cs` (Cursor)**

Task 4.1 says "Create" `AssemblyInfo.cs` with only `AssemblyMetadata` + `InternalsVisibleTo`. But Task 1.1 (Plan 04-01) ALREADY creates this file with `[assembly: CollectionBehavior(DisableTestParallelization = true)]` (REVIEWS LOW-18 fix). If the Task 4.1 snippet is copied verbatim, the LOW-18 attribute is dropped → potential `Console.SetOut` test races.

**Fix:** Change Task 4.1 `<action>` from "Create" to "Append to existing `AssemblyInfo.cs` (preserve `CollectionBehavior` attribute from Task 1.1)". Add a `<read_first>` reference to the existing file.

Note: this is actually in `Utinni.Cli/Properties/AssemblyInfo.cs` (for the EXE assembly metadata) vs `Utinni.Cli.Tests/Properties/AssemblyInfo.cs` (for the test parallelization attribute). They're DIFFERENT files. Cursor may have misread. Need to verify before declaring this a fix.

### LOW (3 items)

- **LOW-10 incomplete propagation [Codex + Cursor]**: `04-01` action block at line 341 still says "nine [Fact] tests" before listing 10; `04-02` Task 2.4 verify (~594) and verification §3 (~645) still say "Plan 04-01 17" instead of 18. Mostly cosmetic.
- **04-04 Task 4.1 test count drift [Codex]**: plan says "8 tests" but enumerates Tests 1-6, 8, 9, 10 = 9 tests. Minor.
- **04-04 Task 4.1 Test 10 API name drift [Cursor]**: `<behavior>` calls `PluginInspection.FilterNativeLoadErrors`; `<action>` implements `PluginInspectionFilters.FilterNativeLoadErrors`. Minor executor confusion.

### Divergent Views

- **Codex IFF math vs Cursor IFF math**: Codex did the algebra and found the contradiction; Cursor concluded "math checks out" without showing the work. Codex is correct.
- **Codex's verdict (FAIL) vs Cursor's verdict (PASS-WITH-GUARDRAILS)**: same evidence, different threshold for blocker vs caveat. Codex requires internal plan consistency; Cursor accepts execute-time mitigations. Both are defensible review philosophies.

---

## Recommended Path Forward

**Option A — One more `--reviews` pass (iter-4):**

```
/gsd:plan-phase 4 --reviews
```

Fix: (a) iter-3 HIGH IFF algorithm change OR fixture redesign + remove ambiguity, (b) iter-3 MED managed-fallback contradiction (drop fallback OR reshape stubs), (c) iter-3 MED AssemblyInfo.cs append-not-clobber, (d) LOW propagation completions. ~20 min planner pass — most fixes are small. **Recommended.**

**Option B — Surgical edits + execute:**

Three concrete edits via Edit tool, ~15 min:
1. 04-03 Task 3.1: change algorithm so file-bound check is top-level-only; nested chunks rely on streaming-read for Truncated. Update Task 3.3 fixture description to match (file actually IS 100 bytes; outer FORM length 200; inner reads past EOF → Truncated).
2. 04-04 Task 4.3 PRE-FLIGHT: drop the managed fallback; require cl.exe at execute time; update fallback prose to "human-action checkpoint if cl.exe missing".
3. 04-04 Task 4.1 AssemblyInfo.cs: change "Create" to "Append" with explicit preserve-clause for the LOW-18 attribute.

Then execute. Skip a fourth cross-AI review.

**Option C — Execute as-is and patch during execute-phase:**

The IFF contradiction is a real plan-level bug; executor will hit it. The managed-fallback is unlikely to fire on a Windows dev machine with MSVC. The AssemblyInfo.cs clobber is a real risk. Not recommended.

**Recommendation: Option B.** Three small targeted edits resolve the HIGH and both MEDIUMs without burning another full replan cycle. The plans are 95% there; the remaining issues are fixable in 15 min.
