---
phase: 05
reviewers: [codex, cursor-agent]
skipped_reviewers: [claude]
skipped_reason: "claude skipped: orchestrator is running inside Claude Code (CLAUDE_CODE_ENTRYPOINT=cli) — self-review excluded for independence per /gsd:review protocol"
unavailable_reviewers: [gemini, coderabbit, opencode, qwen, ollama, lm_studio, llama_cpp]
reviewed_at: 2026-05-23
plans_reviewed:
  - .planning/phases/05-tier-1-c-unit-tests/05-01-PLAN.md
  - .planning/phases/05-tier-1-c-unit-tests/05-02-PLAN.md
risk_assessment:
  codex: MEDIUM
  cursor-agent: MEDIUM
  consensus: MEDIUM
---

# Cross-AI Plan Review — Phase 5

Two independent external AI reviewers (Codex via `codex exec`, Cursor agent via `cursor-agent.cmd`) reviewed the Phase 5 plans (05-01 scaffold + smoke + CI lane; 05-02 seed coverage + docs close + code review). Both rated overall risk **MEDIUM** and judged the plans **ready to execute with minor amendments**. The verbatim per-reviewer feedback is below; the consensus synthesis at the end identifies the items that should drive a `/gsd:plan-phase 5 --reviews` replan.

---

## Codex Review

## Summary

The two-plan structure is generally strong: 05-01 proves the native test scaffold and CI lane before 05-02 adds real coverage, which is the right sequencing for this phase. The seed choice is defensible despite being header-only because `stringUtility::*` is pure, load-bearing, and tied to `PluginManager` config parsing. Main risks are acceptance drift around "CI under all three configs," a likely CI artifact-path failure because the plan assumes Catch2 creates missing output directories, and a real C++ correctness issue in the planned `toBool` invalid-input tests.

## Strengths

- Clear wave gate: 05-02 depends on 05-01 being green on `master`, which reduces first-run CI flake risk.
- Good preservation focus around CON-T-02: full `Debug`, `Release`, and `RelWithDbgInfo` project configs are explicitly required.
- CI lane is genuinely gating: 05-01 Task 3 adds a normal workflow step with no `continue-on-error`, so this is not warn-only.
- Vendored Catch2 approach matches the existing `external/` posture and avoids pulling vcpkg/CMake scope into Phase 5.
- The scaffold smoke tests are useful: link/run, exception machinery, and `SECTION` re-entry cover the test runner itself.
- `stringUtility::*` is a reasonable first native helper target: pure, low-risk, not RVA-bound, and tied to plugin config behavior.

## Concerns

- **HIGH: Catch2 probably will not create `UtinniCore.Tests\TestResults\` automatically.**
  In 05-01 Task 3, the YAML comment says "Output dir is created implicitly by Catch2 v3," and the command writes `junit::out=UtinniCore.Tests\TestResults\junit-results.xml`. `std::ofstream`-style reporters generally do not create parent directories. If the directory does not already exist, the first CI run may fail or silently omit XML.

- **HIGH: `toBool` invalid-input assertions may test undefined behavior.**
  05-02 Task 1 plans assertions for `toBool("") == false` and `toBool("garbage") == false`. The quoted implementation uses `bool result; std::istringstream(input) >> std::boolalpha >> result; return result;`. On extraction failure, `result` is uninitialized. The test may be flaky or expose a real production bug. Either fix `toBool` to initialize `result = false`, or avoid invalid-input assertions in this phase.

- **HIGH: Requirement mismatch risk around triple-config CI.**
  The ROADMAP success criterion says the Catch2 executable "builds in CI under all three native configs." 05-01 explicitly says CI builds only `Release|x86` and triple-config is verified locally. That may be acceptable per later D-07 interpretation, but the plan should explicitly reconcile this conflict or add a CI matrix/extra MSBuild validation steps.

- **MEDIUM: Automated sln verification is not tied tightly enough to the new project GUID.**
  05-01 Task 2 checks that the solution has at least two `RelWithDbgInfo|x86.ActiveCfg = RelWithDbgInfo|Win32` mappings. Better is to assert the exact new GUID has all six mappings, including `.Build.0`. Otherwise a malformed new project block could slip through if the count happens to pass.

- **MEDIUM: Test-count language is inconsistent in 05-02.**
  The plan repeatedly says "6 TEST_CASEs," but the concrete source describes 4 top-level `TEST_CASE`s with `SECTION`s. Catch2 may report this differently depending on reporter/version. This could create false review failures. Use "4 top-level TEST_CASEs covering 6 conceptual cases" consistently.

- **MEDIUM: SHA-256 in commit message is useful but not durable enough alone.**
  05-01 relies on commit-message hashes for Catch2 integrity. If commits are squashed or rewritten, that audit trail is easy to lose. For a vendored 30k LOC dependency, a small `external/catch2/README.md` or `VERSION.txt` with source URL, tag, license, and hashes would be stronger.

- **LOW: 05-01 frontmatter includes `StringUtilityTests.cpp`, but 05-02 owns the real content.**
  The placeholder approach is workable, but it creates a minor ownership wrinkle. The plan documents it clearly, so this is mostly process risk.

- **LOW: The smoke-test expected assertion count looks wrong.**
  05-01 done text expects `5 assertions in 3 test cases`, but the listed tests appear closer to 4 assertions depending on Catch2 counting. Avoid hardcoding assertion counts unless verified.

## Suggestions

- Add this before the CI invocation in 05-01 Task 3:

  ```powershell
  New-Item -ItemType Directory -Force -Path UtinniCore.Tests\TestResults | Out-Null
  ```

- Resolve the `toBool` undefined behavior before landing 05-02. Best fix:

  ```cpp
  bool result = false;
  std::istringstream(input) >> std::boolalpha >> result;
  return result;
  ```

  Then the invalid-input tests become valuable regression coverage.

- Decide explicitly whether "all three configs in CI" is still required. If yes, add CI build steps for `Debug` and `RelWithDbgInfo`, even if only `Release` runs the exe. If no, update the phase success language so reviewers do not reject the plan against the original ROADMAP wording.

- Strengthen solution-file verification by checking the generated GUID's exact six mappings, not just global counts.

- Add `external/catch2/README.md` with:
  - Catch2 version/tag
  - download URLs
  - SHA-256 hashes
  - license note
  - date vendored

- Normalize 05-02 wording to "4 top-level TEST_CASEs / 6 conceptual coverage cases."

- Make the max-harness claim more verifiable by adding a short mutation checklist to `05-02-SUMMARY.md`: e.g. "removing `setfill('0')` fails X; removing `std::hex` fails Y; removing trim-end logic fails Z."

## Risk Assessment

**Overall risk: MEDIUM.**

The design is sound and appropriately scoped, but there are a few implementation traps that could cause CI failure or review rejection. The two highest-impact fixes are simple: create the JUnit output directory explicitly and resolve the `toBool` uninitialized-result issue before asserting invalid-input behavior. The acceptance mismatch around triple-config CI should also be clarified before implementation, because that is the most likely source of late phase disagreement.

---

## Cursor-agent Review

# Cross-AI Plan Review: Phase 5 (05-01 + 05-02)

## 1. Summary

Phase 5 plans are unusually strong: CONTEXT/RESEARCH decisions are carried through with concrete file paths, verbatim XML/YAML, pitfall-driven mitigations, and a sensible 05-01/05-02 wave gate. The scaffold plan (Catch2 vendor → triple-config vcxproj → CI lane → smoke tests) is executable and aligned with Phase 4 precedents. The seed plan (`stringUtility::*`) is the best available native target after Phase 4 moved parsers to managed code, and the PluginManager consumer trace in `plugin_manager.cpp:121-138` makes the D-06 failure-mode story credible.

The main gaps are **build/link correctness around `#include "utility/string_utility.h"`** (transitive `utinni.h` + `UTINNI_API` / `string_utility.cpp` without `EXPORT_UTINNI`), **requirement traceability drift** (`REQUIREMENTS.md` TEST-02 still says `ctest`), and **plan-internal counting inconsistencies** ("6 TEST_CASEs" vs 4 top-level cases). None of these invalidate the phase goal, but the first could block 05-01 green on first build.

---

## 2. Strengths

- **Wave gate is well-designed (D-05).** 05-01 proves build + CI before 05-02 adds coverage; 05-02 `depends_on: ['05-01']` and the human CI check on master (05-01 Task 3, `<human-check>`) enforce this cleanly.
- **Triple-config guard-rail is concrete, not rhetorical.** 05-01 Task 2 verify script checks `RelWithDbgInfo|x86.ActiveCfg = RelWithDbgInfo|Win32` count ≥ 2 and **Release vs RelWithDbgInfo PDB SHA-256 mismatch** — directly targets the LoaderLockHarness collapse at `Utinni.sln:104` (`RelWithDbgInfo → Release`).
- **CI lane posture matches Phase 4 D-11.** Third lane after CLI upload (after `ci.yml:108`), no warn-only escape hatch, stacked `--reporter console` + junit (Pitfall 5), artifact upload with `if: failure()`.
- **Seed selection is research-backed and codebase-verified.** `stringUtility` helpers are inline/pure; `PluginManager::loadPlugins` uses `toString(i,2)`, `trim`, and `toBool(trim(...))` exactly as claimed.
- **Max-harness is auditable in principle.** 05-02 Task 1 requires embedding the failure-mode table as a C++ comment; automated verify checks for `D-06 max-harness` and `PluginManager::loadPlugins` strings.
- **Scope discipline.** LogSubscribe / 05-03, vcpkg, CMake, coverage tooling correctly deferred; no `swg/*` refactor creep.
- **Security proportionate to risk.** ~30k LOC vendor with SHA-256 in commit message is reasonable for a test-only, non-runtime dep under ASVS V14.

---

## 3. Concerns

### HIGH

- **`string_utility.h` → `utinni.h` transitive include not addressed in vcxproj (05-01 RESEARCH skeleton ~lines 762–909; 05-01 Task 2).**
  `string_utility.h:27` includes `utinni.h`, which pulls DetourXS, UtINI, `log.h`, `memory.h`, and `utility.h`. The plan compiles `string_utility.cpp` into the test exe but **does not define `EXPORT_UTINNI`** (UtinniCore sets this; test skeleton only has `_DEBUG/_CONSOLE` or `NDEBUG/_CONSOLE`). The wstring overload is `UTINNI_API extern` (`string_utility.h:35`) — without `EXPORT_UTINNI` that becomes `dllimport` while the TU defines it locally → likely **C4273 / inconsistent DLL linkage** at link time.
  **Mitigation missing from plans:** either drop `string_utility.cpp` from the test project (seed tests don't use wstring `toString`), or add `EXPORT_UTINNI` to all three configs, or link `UtinniCore.dll`.

- **No "test the tester" red-run for the native lane (05-01 Task 3).**
  Phase 1 validated red-on-failure for managed tests; 05-01 only verifies green. Pitfall 5 (junit reporter swallowing exit code) is mitigated on paper but **not procedurally verified** before gating master.

### MEDIUM

- **`toBool` tests assert behavior the implementation does not guarantee (05-02 / RESEARCH seed examples).**
  `toBool` leaves `result` uninitialized when `istringstream` extraction fails (`string_utility.h:44–48`). Tests like `toBool("garbage") == false` document intent but test **accidental/UB behavior**, weakening D-06 "revert and fail" rigor for that case.

- **Internal TEST_CASE count inconsistency.**
  - 05-02 frontmatter `must_haves`: "6 TEST_CASEs"
  - 05-02 Task 1 action: "4 top-level TEST_CASEs"
  - 05-02 automated verify: regex for 4 `TEST_CASE("stringUtility::...` patterns
  RESEARCH mixes "6 TEST_CASEs" with 4 blocks using SECTIONs. Executors will be unsure what "done" means.

- **`REQUIREMENTS.md` TEST-02 acceptance still says `ctest` (`.planning/REQUIREMENTS.md:53`).**
  Plans correctly choose MSBuild + direct exe (D-03), and 05-02 updates `docs/ai/test-harness-plan.md`, but **TEST-02 itself is not in `files_modified`**. Phase may "complete" with stale requirement text.

- **Triple-config validated locally, not in CI (by design, but residual risk).**
  CI builds only `Release|x86` (`ci.yml:77–78`). CON-T-02 relies on 05-01's local triple build + PDB hash check. If a developer never runs RelWithDbgInfo locally, regressions in that config won't surface until release-time.

- **05-02 `autonomous: false` + blocking code-review checkpoint.**
  Appropriate for quality, but if `/gsd:code-review 05` surfaces CON-N/STAB-04 items, wave-2 completion depends on human resume signal — not a flaw, but a schedule risk.

### LOW

- **Compiling `string_utility.cpp` in 05-01 before seed exists (05-01 Task 2).**
  Unnecessary for smoke tests; adds build surface (Windows API via `WideCharToMultiByte`) earlier than needed. Not scope creep, but couples scaffold success to production TU compilation.

- **Catch2 ~30k LOC vendor audit is SHA-256-only.**
  Sufficient for V14 given test-only scope; PGP verification deferred is reasonable. Note BSL-1.0 vs project MIT convention — plans correctly say don't graft MIT onto vendor files.

- **`trim_*_copy` helpers in `string_utility.h:89–102` look buggy** (`trim_copy` only trimStart; names swapped) — not in plan scope, but 05-02's PluginManager idiom tests won't catch them. Fine for Phase 5 boundary.

- **05-01 success criteria assertion count** ("5 assertions in 3 test cases") is brittle if someone adds a REQUIRE to smoke tests.

---

## 4. Suggestions

1. **05-01 Task 2 — resolve `UTINNI_API` / include graph explicitly.** Pick one and document in the vcxproj:
   - **Preferred for minimal scope:** remove `string_utility.cpp` from 05-01 vcxproj; seed uses header-only inline functions only.
   - **Alternative:** add `EXPORT_UTINNI` to all three `PreprocessorDefinitions` blocks if keeping `string_utility.cpp`.
   - Add a build-verify line: `#include "utility/string_utility.h"` compiles in `main_smoke.cpp` or a dedicated compile smoke TU.

2. **05-01 Task 3 — add one throwaway red-run step** (Phase 1 precedent): temporary failing `REQUIRE(false)` on a branch/PR, confirm CI step fails and artifact uploads, then revert. One sentence in success criteria is enough.

3. **05-02 — normalize language to "4 TEST_CASEs (6 assertion groups via SECTIONs)"** everywhere: frontmatter, must_haves, verify script, SUMMARY template. Align with Catch2 console output expectations.

4. **05-02 Task 2 — also patch `.planning/REQUIREMENTS.md` TEST-02 acceptance** to say "native test exe in CI" instead of `ctest`, or add a one-line note in phase SUMMARY that requirement text is intentionally superseded by D-03 (better: update the requirement).

5. **05-02 — strengthen one `toBool` test or comment.** Either test only `"true"`/`"false"`/`"True"` (defined behavior), or add a comment that garbage-input false is **documented desired behavior** pending a production fix (uninitialized read today).

6. **05-01 CI step — optional explicit build target** after full sln build:
   `msbuild ... /t:UtinniCore.Tests`
   Makes failure attribution clearer if another project breaks; low cost.

7. **05-02 docs task — add `05-01-SUMMARY.md` cross-link** (GUID, Catch2 hashes, master CI URL) when closing `test-harness-plan.md` for audit trail.

8. **Defer nothing material** — optional 05-03 LogSubscribe is already correctly deferred; don't pull it in.

---

## 5. Risk Assessment

**Overall: MEDIUM**

| Area | Level | Justification |
|------|-------|----------------|
| Phase goal achievement | **Low–Medium** | Scaffold + CI + seed coverage will satisfy TEST-02 intent and ROADMAP success criteria once built. |
| First-build failure | **Medium–High** | `UTINNI_API` / `string_utility.cpp` without `EXPORT_UTINNI` is the most likely Day-1 blocker; not called out in plan tasks. |
| CI gate integrity | **Low–Medium** | Stacked reporters + no warn-only is sound; lack of red-run verification leaves residual Pitfall 5 risk. |
| Triple-config (CON-T-02) | **Low** | PDB hash + sln mapping checks in 05-01 Task 2 verify are strong for this specific footgun. |
| Max-harness credibility | **Medium** | PluginManager-linked failure modes for `toString`, `trim`, `toHexString` are solid; `toBool` garbage case and "non-trivial parser" wording are softer. |
| Security (Catch2 vendor) | **Low** | Test-only, pinned tag, SHA-256 — adequate for V14. |
| Scope creep | **Low** | Plans stay tight; 2-plan split is appropriate. |

**Bottom line:** These plans are **ready to execute with minor amendments**. Fix the `string_utility` / `EXPORT_UTINNI` / optional drop of `string_utility.cpp` before or during 05-01 Task 2, reconcile TEST_CASE counting in 05-02, and add a native-lane red-run check. With those, Phase 5 should deliver TEST-02, close the Tier 1 C++ row in `test-harness-plan.md`, and establish a reusable native test pattern for Phase 6 — even though the seed is utility helpers rather than parsers, which is defensible given Phase 4's managed parser move and the thin remaining native surface.

---

## Consensus Summary

### Agreed Strengths (2/2 reviewers)
- Wave gate (05-02 depends on 05-01 green-on-master) is well-designed and reduces first-run CI flake risk.
- Triple-config guard-rail (CON-T-02 / D-07) is concrete: explicit sln-mapping count ≥ 2 plus Release-vs-RelWithDbgInfo PDB SHA-256 mismatch directly targets the LoaderLockHarness collapse anti-pattern.
- CI lane is genuinely gating (no `continue-on-error`, no warn-only escape hatch) and matches the Phase 4 D-11 step pattern (stacked reporters per Pitfall 5).
- Seed pick (`stringUtility::*`) is defensible despite being header-only — pure, load-bearing through `PluginManager::loadPlugins`, not RVA-bound.
- Vendored Catch2 at `external/catch2/` is proportionate to risk (test-only, ASVS V14 only) and matches existing zero-package-manager posture.
- Scope discipline is tight — no vcpkg/CMake/`swg/*`-refactor creep; 05-03 correctly deferred.

### Agreed Concerns (2/2 reviewers — highest priority for `--reviews` replan)

| # | Severity | Concern | Codex | Cursor | Action |
|---|---------|---------|-------|--------|--------|
| C1 | **HIGH** | `toBool` invalid-input tests (`toBool("") == false`, `toBool("garbage") == false`) test **undefined behavior** — `bool result;` is uninitialized when `istringstream` extraction fails. Either fix `string_utility.h` (`bool result = false;`) before asserting, or drop the UB cases from the seed test set. | ✓ | ✓ | Replan input: either patch `string_utility.h:44–48` as a side-effect of 05-02, or restrict 05-02 tests to defined-behavior inputs only. |
| C2 | **HIGH** | TEST_CASE counting drift in 05-02: frontmatter says "6 TEST_CASEs", action says "4 top-level TEST_CASEs", verify regex matches 4. Catch2 reporter output will disagree with plan claims. | ✓ | ✓ | Normalize to "4 top-level TEST_CASEs covering 6 conceptual cases via SECTIONs" across frontmatter / must_haves / verify / source. |
| C3 | **HIGH/MEDIUM** | Triple-config CI mismatch: ROADMAP success criterion #1 says "builds in CI under all three native configs", but 05-01 has CI build only `Release|x86` with triple-config validated locally via PDB SHA-256. Either accept the local-validation interpretation explicitly OR extend CI to build all three. | ✓ HIGH | ✓ MEDIUM | Replan input: pick one — add a phase SUMMARY note documenting the locked interpretation, OR add CI build steps for Debug + RelWithDbgInfo (Release-only `.exe` invocation is fine; just need msbuild to succeed for all three). |

### Cursor-agent unique concerns (worth surfacing — Codex did not catch)

| # | Severity | Concern | Action |
|---|---------|---------|--------|
| U1 | **HIGH** | `UTINNI_API` / `EXPORT_UTINNI` linkage trap: `string_utility.h` transitively includes `utinni.h`; the `UTINNI_API extern` wstring overload becomes `dllimport` in the test exe (no `EXPORT_UTINNI`) while `string_utility.cpp` is compiled locally — likely C4273 / inconsistent-DLL-linkage link error on first build. **This is the most likely Day-1 build blocker.** | Replan input — preferred fix: **drop `string_utility.cpp` from the test vcxproj** (seed only needs header-only inline functions). Alternative: add `EXPORT_UTINNI` to all three PreprocessorDefinitions blocks. Add a `#include "utility/string_utility.h"` smoke compile check. |
| U2 | **HIGH** | No "test the tester" red-run for the native lane — Pitfall 5 (junit reporter swallowing exit code) is mitigated on paper but never procedurally verified by a deliberate failing test that confirms CI goes red. Phase 1 set this precedent for managed tests. | Replan input: add a one-time throwaway red-run task to 05-01 (`REQUIRE(false)` on a branch, confirm CI fails + artifact uploads, revert). |
| U3 | MEDIUM | `REQUIREMENTS.md` TEST-02 still says `ctest` (line 53). D-03 superseded this but the requirement text is not in `files_modified` for either plan. Phase may "complete" with stale requirement language. | Replan input: add `.planning/REQUIREMENTS.md` to 05-02 `files_modified` with a TEST-02 acceptance-text update. |

### Codex unique concerns (worth surfacing — cursor-agent did not catch)

| # | Severity | Concern | Action |
|---|---------|---------|--------|
| U4 | **HIGH** | Catch2 will not auto-create `UtinniCore.Tests\TestResults\` directory — `std::ofstream`-style reporters do not create parent dirs. CI first run likely fails or silently omits the XML artifact. | Replan input: prepend `New-Item -ItemType Directory -Force -Path UtinniCore.Tests\TestResults | Out-Null` to the CI step before the exe invocation. |
| U5 | MEDIUM | Sln verification (count `RelWithDbgInfo|x86.ActiveCfg = RelWithDbgInfo|Win32` ≥ 2) could pass with a malformed new-project block — should match the **new project's GUID** explicitly across all 6 expected mappings (Debug + Release + RelWithDbgInfo × ActiveCfg + Build.0). | Replan input: tighten 05-01 Task 2 `<automated>` verify to capture the new project's GUID first, then assert all 6 GUID-scoped postSolution lines exist. |
| U6 | MEDIUM | SHA-256-in-commit-message is fragile across squash/rewrite. For a 30k-LOC vendor, an `external/catch2/README.md` (or `VERSION.txt`) with source URL, tag, SHA-256, license, vendored-date is more durable. | Replan input: add `external/catch2/README.md` to 05-01 `files_modified`. |

### Divergent Views

- **Triple-config CI severity** — Codex rates HIGH (ROADMAP literal reading); Cursor rates MEDIUM (D-03/D-07 interpretation makes local-only validation defensible). Both agree the conflict must be reconciled in plan text either way. Defer the severity call to user.
- **Drop vs. keep `string_utility.cpp` in the test vcxproj** — Cursor's preferred fix is to drop it (header-only seed tests don't need it; eliminates the `EXPORT_UTINNI` problem entirely); the alternative is to define `EXPORT_UTINNI` in the test project. Codex did not weigh in on this specific issue (missed the linkage finding). Recommend: **drop `string_utility.cpp`** unless a future seed expansion needs the wstring `toString` overload (none planned for 05-02).

### Replan Recommendation

The 6 actionable items below are what `/gsd:plan-phase 5 --reviews` should fold into the next revision. None are blockers (both reviewers explicitly approved execution), but C1, C2, U1, and U4 should be addressed before `/gsd:execute-phase 5` runs to avoid a guaranteed first-build / first-CI-run failure.

**Replan checklist:**

1. **[C1 / HIGH]** Resolve `toBool` UB: either patch `UtinniCore/utility/string_utility.h:44` to `bool result = false;` as a side-effect task in 05-02, or drop garbage-input assertions from the seed test set. Recommended: **patch + assert** (turns the test into real regression coverage).
2. **[C2 / HIGH]** Normalize 05-02 test-count language to "4 top-level TEST_CASEs covering 6 conceptual cases via SECTIONs" across frontmatter, must_haves, verify regex, and source comments.
3. **[C3 / HIGH-MEDIUM]** Reconcile triple-config CI interpretation — either add Debug + RelWithDbgInfo build steps to CI lane, OR document in 05-01 SUMMARY that local-only validation is the locked interpretation of D-07.
4. **[U1 / HIGH]** Drop `string_utility.cpp` from `UtinniCore.Tests.vcxproj` source list (preferred) — test seed uses header-only inline functions; eliminates the `EXPORT_UTINNI`/dllimport C4273 link error.
5. **[U2 / HIGH]** Add a one-time native-lane red-run validation task to 05-01 (throwaway `REQUIRE(false)` on a branch, confirm CI red + artifact upload, revert).
6. **[U4 / HIGH]** Prepend `New-Item -ItemType Directory -Force -Path UtinniCore.Tests\TestResults` to the CI exe-invocation step.

**Lower-priority replan items (defer-able):**
7. [U3 / MEDIUM] Patch `.planning/REQUIREMENTS.md` TEST-02 acceptance text to remove `ctest`.
8. [U5 / MEDIUM] Tighten sln verification to GUID-scoped 6-mapping match.
9. [U6 / MEDIUM] Add `external/catch2/README.md` with vendor metadata.
10. [Codex LOW] Avoid hardcoding smoke-test assertion counts ("5 assertions in 3 test cases").
11. [Cursor LOW] Note `trim_*_copy` helper bugs in `string_utility.h:89–102` as out-of-scope for Phase 5 (Phase 6 STAB-03 input).

To apply: `/gsd:plan-phase 5 --reviews`
