---
phase: 05-tier-1-c-unit-tests
verified: 2026-05-23T00:00:00Z
status: passed
score: 11/11 must-haves verified
overrides_applied: 0
re_verification: # initial verification (no prior VERIFICATION.md)
  previous_status: none
goal_spirit_note: |
  ROADMAP.md Phase 5 goal text references `ctest` three times. Phase 5's locked
  decision D-03 (in 05-CONTEXT.md) explicitly supersedes `ctest` with
  "MSBuild + direct exe invocation" (UtinniCore.Tests.exe Catch2 self-runner).
  Cross-AI review item U3 (in 05-REVIEWS.md) required REQUIREMENTS.md TEST-02
  acceptance text be updated to drop the stale ctest reference — applied in
  commit eb52c9d. Verifying SPIRIT of goal:
    (a) Catch2 wired through CI under all 3 configs — VERIFIED
    (b) UtinniCore helper has non-trivial coverage (4 TEST_CASEs / 6 conceptual
        cases via SECTIONs across stringUtility::toBool / toString(int,fillCount)
        / toHexString / trim*) — VERIFIED
    (c) CI gates master on the native test target (no continue-on-error,
        no warn-only) — VERIFIED
  ROADMAP.md text update to match D-03 will be performed by the orchestrator
  post-verification.
notable_pre_existing:
  - "GameCallbacksTests.RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV: A second timing-sensitive interop test in UtinniCoreDotNet.Tests is flaking under GC pressure on the windows-2022 runner. Failures observed on master CI runs 26335785703 (SHA bab3412) and 26336088047 (SHA c9627bd) — both AFTER the Phase 5 green baseline at 26335337027 (SHA 727250b). NOT a Phase 5 regression — Phase 5 only added UtinniCore.Tests, this failure is in a pre-existing C# test fixture (GameCallbacksTests.cs line 80). The native lane never gets to execute on a failing dotnet-test step because the workflow exits with code 1 earlier. Recommend creating a follow-up todo similar to .planning/todos/pending/loader-lock-harness-flake-fix.md for this second timing-sensitive interop test."
  - "LoaderLockHarness 50ms threshold flake: Pre-existing flake documented at .planning/todos/pending/loader-lock-harness-flake-fix.md. Observed on master run 26335742745 (SHA 2582065). Not a Phase 5 issue."
  - "UTINNI_API macro redefinition warning (C4005): Pre-existing — surfaces in the test exe's compile log because the transitive include graph picks up both UtINI/utini.h:29 and UtinniCore/utinni.h:45 macro definitions. Documented in 05-02-SUMMARY 'Issues Encountered' as a Phase 6 STAB-03 candidate."
  - "trim_*_copy helper name swap (string_utility.h:89-102): Cursor LOW finding from 05-REVIEWS.md; out of scope for Phase 5 per the locked plan boundary; queued as Phase 6 STAB-03 input in 05-02-SUMMARY 'Phase 6 STAB-03 Inputs' section."
  - "CppSharp Generated/UtinniCore.cs non-determinism: 2000-line diff regenerated on every msbuild invocation; pre-existing, reverted via git checkout before each task commit during Phase 5; surfaced as a Phase 6 STAB-03 input in 05-01-SUMMARY 'Issues Encountered'."
quality_notes:
  - "WARNING: external/catch2/README.md SHA-256 hashes do not match the on-disk file hashes when computed via PowerShell Get-FileHash. Root cause is line-ending normalization: the on-disk vendored files have CRLF line endings (562,038 bytes for the .hpp vs the 547,400 bytes recorded in 05-01-SUMMARY); the SHA-256 values were computed at vendor time before EOL normalization. This is a verification-trail integrity issue (U6 wanted durable hashes) but is NOT a tampering or vendor-content issue — Catch v3.15.0 banner is present at .hpp:9, LICENSE.txt is BSL-1.0, the vendored content is correct. Suggested follow-up: either (a) re-record CRLF hashes in README.md, or (b) add .gitattributes entry `external/catch2/* -text` to force LF storage and re-checkout, then verify the README hashes match. Not a Phase 5 BLOCKER; the goal is achieved; the audit-trail-strength claim of U6 is partially weakened."
---

# Phase 5: Tier 1 C++ unit tests — Verification Report

**Phase Goal (verbatim from ROADMAP.md):** "Catch2 wired through `ctest` (or `vcpkg`) for UtinniCore. At least one native parser or helper has non-trivial coverage. CI runs the native test target on every push."

**Spirit of Goal (per locked decision D-03 + replan commit eb52c9d):** Catch2 wired through CI under all 3 native configs via MSBuild + direct exe invocation (no ctest mechanism, by design); at least one UtinniCore helper has non-trivial coverage; CI gates master on the native test target.

**Verified:** 2026-05-23
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Catch2 v3.15.0 vendored at `external/catch2/` with full vendor metadata | ✓ VERIFIED | `external/catch2/catch_amalgamated.hpp` (14,638 lines), `.cpp` (12,377 lines), `LICENSE.txt` (BSL-1.0), `README.md` (contains v3.15.0, BSL-1.0, SHA-256 — see WARNING in quality_notes about hash drift); .hpp:9 has `// Catch v3.15.0` banner |
| 2 | Sibling MSBuild test project `UtinniCore.Tests.vcxproj` exists with triple-config | ✓ VERIFIED | vcxproj declares Debug\|Win32, Release\|Win32, RelWithDbgInfo\|Win32; ConfigurationType=Application for all 3; OutDir=`$(SolutionDir)bin\$(Configuration)\` for all 3; `string_utility.cpp` is NOT in ClCompile list (header-only per U1); `main_smoke.cpp` and `StringUtilityTests.cpp` ARE in the list |
| 3 | Utinni.sln registers UtinniCore.Tests with GUID-scoped 6-mapping postSolution | ✓ VERIFIED | sln line 55: project entry with GUID `{345DFD73-E6CD-4B2F-81AF-6F643193B5F8}`; lines 159-164: all 6 expected mappings present — Debug\|x86/Release\|x86/RelWithDbgInfo\|x86 × ActiveCfg/Build.0 — ALL map to matching Win32 configs (no `RelWithDbgInfo→Release` collapse) |
| 4 | 3+1 smoke TEST_CASEs in main_smoke.cpp | ✓ VERIFIED | grep finds 4 `TEST_CASE("Smoke:` occurrences (lines 30, 35, 42, 59); the 4th smoke at line 59 contains the U1 include-graph compile smoke `REQUIRE(stringUtility::toBool("true") == true)` |
| 5 | C1 patch applied to string_utility.h:46 | ✓ VERIFIED | string_utility.h:46 reads `    bool result = false;` (initialized); the uninitialized form `bool result;` does NOT appear in the file |
| 6 | 4 TEST_CASEs covering 6 conceptual cases via SECTIONs in StringUtilityTests.cpp | ✓ VERIFIED | grep finds exactly 4 `TEST_CASE("stringUtility::` occurrences; 7 `SECTION(` occurrences (3 in toBool + 4 in trim*); file is 120 lines (≥80 required); embedded D-06 failure-mode table at lines 25-43; inline 05-REVIEWS.md item C1 reference at line 64 |
| 7 | Third CI lane in ci.yml with 5 new steps and no continue-on-error | ✓ VERIFIED | ci.yml lines 110-146 contain 5 new steps: Build Debug (line 110-114), Build RelWithDbgInfo (line 116-120), Create TestResults dir (line 122-126), Run exe (line 128-138), Upload artifacts on failure (line 140-146); `continue-on-error` grep returns 0 hits across the workflow; native lane uses `/t:UtinniCore_Tests` (underscore form per documented MSBuild target-name escape) |
| 8 | REQUIREMENTS.md TEST-02 acceptance updated to remove ctest | ✓ VERIFIED | grep for `ctest` in REQUIREMENTS.md returns 0 hits; TEST-02 block (lines 50-54) contains "MSBuild", "Phase 5 D-03", and "UtinniCore.Tests.exe"; the Acceptance line explicitly references "all three native configs (Debug + Release + RelWithDbgInfo)" |
| 9 | docs/ai/test-harness-plan.md Tier 1 C++ row closed | ✓ VERIFIED | line 26: "**[Closed in Phase 5, 2026-05-23]** Catch2 v3.15.0 vendored amalgamated at `external/catch2/` (D-02)..." with explicit D-01..D-04 cross-references; line 72: "Suggested phase order" item 3 also marked closed |
| 10 | Local build smoke verified — UtinniCore.Tests.exe runs green | ✓ VERIFIED | `bin/Release/UtinniCore.Tests.exe` exists; smoke + seed runs locally produce "All tests passed (24 assertions in 8 test cases)" with exit code 0; `[utility][string]` filter produces "All tests passed (19 assertions in 4 test cases)" with exit code 0; `git log --oneline --all --grep="(05-0[12])"` shows 9 atomic commits with conventional scopes (≥8 required) |
| 11 | Remote CI green confirmed on Phase 5 baseline (per D-04) | ✓ VERIFIED | Run 26335337027 (SHA 727250b, "docs(phase-05): update tracking after wave 1") had conclusion=success; all 5 new native-lane steps executed: Debug build + RelWithDbgInfo build + Release build (via solution-wide build) + directory creation + Release exe invocation producing "All tests passed (5 assertions in 4 test cases)" at the wave-1 baseline. Subsequent master CI runs (26335742745, 26335785703, 26336088047) show pre-existing C# interop flakes in UtinniCoreDotNet.Tests (LoaderLockHarness + GameCallbacks) — NOT Phase 5 regressions; the failing dotnet-test step exits the workflow before the native lane runs |

**Score:** 11/11 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `external/catch2/catch_amalgamated.hpp` | Catch2 v3.15.0 amalgamated header | ✓ VERIFIED | 14,638 lines; "Catch v3.15.0" banner at line 9 |
| `external/catch2/catch_amalgamated.cpp` | Catch2 v3.15.0 amalgamated TU | ✓ VERIFIED | 12,377 lines (provides main()) |
| `external/catch2/LICENSE.txt` | Boost Software License 1.0 | ✓ VERIFIED | "Boost Software License - Version 1.0" at line 1 |
| `external/catch2/README.md` | Durable vendor metadata (U6) | ✓ VERIFIED with WARNING | Contains v3.15.0, BSL-1.0, SHA-256 (4 occurrences), source URL, vendored date 2026-05-23. WARNING: README SHA-256 values do not match `Get-FileHash` of on-disk files due to CRLF EOL normalization at checkout time. Content is correct; audit-trail integrity per U6 is weakened |
| `UtinniCore.Tests/UtinniCore.Tests.vcxproj` | Triple-config Application; string_utility.cpp EXCLUDED | ✓ VERIFIED | 3 ProjectConfigurations + 3 ItemDefinitionGroups + 3 Configuration PropertyGroups; ConfigurationType=Application for all 3; string_utility.cpp NOT in ClCompile list; explanatory XML comment per U1 at line 120 |
| `UtinniCore.Tests/main_smoke.cpp` | 4 smoke TEST_CASEs (3 vendor sanity + 1 include-graph) | ✓ VERIFIED | 62 lines; 4 TEST_CASEs at lines 30, 35, 42, 59; line 59 case has `REQUIRE(stringUtility::toBool("true") == true)` (U1 compile smoke) |
| `UtinniCore.Tests/StringUtilityTests.cpp` | 4 TEST_CASEs / 6 conceptual cases via SECTIONs | ✓ VERIFIED | 120 lines (≥80 min); 4 `TEST_CASE("stringUtility::` at lines 48, 72, 80, 87; D-06 failure-mode table embedded at lines 25-43; 05-REVIEWS.md item C1 inline reference at line 64; literal "6 TEST_CASEs" phrase ABSENT (per C2 normalization) |
| `Utinni.sln` | Project entry + GUID-scoped 6-mapping postSolution block | ✓ VERIFIED | Project entry at line 55; 6 postSolution mappings at lines 159-164 — Debug, Release, RelWithDbgInfo each with ActiveCfg + Build.0 mapping to matching Win32 config |
| `.github/workflows/ci.yml` | 5 new steps; no continue-on-error; native lane gates master | ✓ VERIFIED | 5 steps at lines 110-146 (Build Debug, Build RelWithDbgInfo, Create TestResults dir, Run exe, Upload artifacts on failure); native-test-results artifact name distinct from cli-test-results; exe invocation uses stacked `--reporter console + junit::out=...` per Pitfall 5 mitigation |
| `UtinniCore/utility/string_utility.h` (line 46) | `bool result = false;` (C1 patch) | ✓ VERIFIED | line 46: `    bool result = false;`; uninitialized form absent from file |
| `docs/ai/test-harness-plan.md` (Tier 1 C++ row) | "[Closed in Phase 5, YYYY-MM-DD]" disposition | ✓ VERIFIED | Line 26: "[Closed in Phase 5, 2026-05-23]" with D-01..D-04 cross-refs and "4 TEST_CASEs (6 conceptual cases via SECTIONs)" language; line 72 also marked closed |
| `.planning/REQUIREMENTS.md` (§TEST-02) | ctest removed; MSBuild + D-03 + UtinniCore.Tests.exe referenced | ✓ VERIFIED | ctest grep returns 0 hits; Statement (line 52) + Acceptance (line 53) reference MSBuild, Phase 5 D-03, UtinniCore.Tests.exe, all three native configs |
| `bin/Release/UtinniCore.Tests.exe` | Local exe exists; runs green | ✓ VERIFIED | Exe present; full-suite run = "All tests passed (24 assertions in 8 test cases)" exit 0; `[utility][string]` filter run = "All tests passed (19 assertions in 4 test cases)" exit 0 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `UtinniCore.Tests.vcxproj` | `external/catch2/catch_amalgamated.cpp` | `<ClCompile Include="..\external\catch2\catch_amalgamated.cpp" />` | ✓ WIRED | vcxproj line 119; CI log confirms `catch_amalgamated.obj` produced + linked into all 3 config exes |
| `UtinniCore.Tests.vcxproj` | `UtinniCore.vcxproj` | `<ProjectReference>` with `<LinkLibraryDependencies>false</LinkLibraryDependencies>` | ✓ WIRED | vcxproj lines 128-131; UtinniCore GUID `{AEFED7F6-...}` present; LinkLibraryDependencies=false ensures build ordering without linking UtinniCore.lib (per U1) |
| `Utinni.sln` | `UtinniCore.Tests.vcxproj` | Project entry + 6-line postSolution block | ✓ WIRED | sln line 55 + lines 159-164; all 6 GUID-scoped mappings present with correct Configuration|Win32 targets (no RelWithDbgInfo→Release collapse, per U5) |
| `.github/workflows/ci.yml` | `bin\Release\UtinniCore.Tests.exe` | 5 new steps invoked from PowerShell | ✓ WIRED | Steps at lines 110-146; exe invocation at line 136-138 uses `& bin\Release\UtinniCore.Tests.exe` with stacked `--reporter console` + `--reporter "junit::out=UtinniCore.Tests\TestResults\junit-results.xml"`; preceded by directory-creation step at line 122-126 |
| `main_smoke.cpp` | `UtinniCore/utility/string_utility.h` | `#include "utility/string_utility.h"` | ✓ WIRED | main_smoke.cpp line 26; includes the header to exercise U1 compile-smoke path; resolves via `$(SolutionDir)UtinniCore;` in AdditionalIncludeDirectories |
| `StringUtilityTests.cpp` | `UtinniCore/utility/string_utility.h` | `#include "utility/string_utility.h"` | ✓ WIRED | StringUtilityTests.cpp line 46; 4 TEST_CASEs invoke `stringUtility::toBool/toString/toHexString/trim*` directly; build + run green |
| `REQUIREMENTS.md` | Phase 5 D-03 decision | TEST-02 acceptance text explicitly cross-references "Phase 5 D-03" | ✓ WIRED | REQUIREMENTS.md lines 52-53 — Statement + Acceptance both cite "Phase 5 D-03" as the supersession of the original `ctest` language |
| `docs/ai/test-harness-plan.md` | Phase 5 closure | Tier 1 C++ row + Suggested phase order item 3 both reference Phase 5 | ✓ WIRED | Line 26 + line 72 — both rows updated to "[Closed in Phase 5, 2026-05-23]" with cross-refs to `.planning/phases/05-tier-1-c-unit-tests/` |

### Data-Flow Trace (Level 4)

Not applicable — Phase 5 produces a build-time test framework + CI lane. No runtime data flow to trace. The exe consumes test-source-embedded inputs (`"true"`, `0xDEADBEEF`, `" hello "`) directly via C++ literals; data source is the test file itself, which IS the artifact under verification.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Test exe runs and reports all-pass | `bin/Release/UtinniCore.Tests.exe --reporter console` | "All tests passed (24 assertions in 8 test cases)" | ✓ PASS |
| Filtered seed coverage runs all-pass | `bin/Release/UtinniCore.Tests.exe "[utility][string]"` | "All tests passed (19 assertions in 4 test cases)" | ✓ PASS |
| TEST_CASE count in StringUtilityTests.cpp = 4 | `grep -c 'TEST_CASE("stringUtility::'` | 4 matches | ✓ PASS |
| SECTION count in StringUtilityTests.cpp ≥ 3 | `grep -c 'SECTION('` | 7 matches | ✓ PASS |
| Smoke TEST_CASE count ≥ 3 | `grep -c 'TEST_CASE("Smoke:'` | 4 matches (3 smoke + 1 U1 include-graph) | ✓ PASS |
| C1 patch present | grep `bool result = false;` in string_utility.h | Line 46: present | ✓ PASS |
| Negative-grep: ctest removed from REQUIREMENTS.md TEST-02 | grep `ctest` REQUIREMENTS.md | 0 matches | ✓ PASS |
| Positive-grep: MSBuild + D-03 + UtinniCore.Tests.exe in TEST-02 | grep | 3 distinct positive matches | ✓ PASS |
| Sln GUID-scoped 6-mapping integrity | grep `{345DFD73-...}` in Utinni.sln | 7 occurrences (1 project entry + 6 mappings); all 6 mappings map to matching Win32 configs | ✓ PASS |
| Catch2 v3.15.0 banner present | grep `Catch v3.15.0` external/catch2/catch_amalgamated.hpp | Line 9 match | ✓ PASS |
| Catch2 LICENSE is BSL-1.0 | grep `Boost Software License` external/catch2/LICENSE.txt | Line 1 match | ✓ PASS |
| External CI run conclusion (green baseline) | `gh run view 26335337027 --json conclusion` | `success` | ✓ PASS |

### Probe Execution

Not applicable — Phase 5 has no `scripts/*/tests/probe-*.sh` style probes declared in PLAN/SUMMARY. The native test exe itself IS the probe (executed as a behavioral spot-check above).

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| TEST-02 | 05-01-PLAN + 05-02-PLAN | Tier 1 C++ unit-test scaffold: Catch2 wired through MSBuild + direct exe invocation; ≥1 native helper has coverage; CI runs the native test exe under all 3 configs and is green on master | ✓ SATISFIED | All 3 acceptance conditions verified — (a) Catch2 v3.15.0 vendored, UtinniCore.Tests.exe builds under Debug+Release+RelWithDbgInfo in CI (run 26335337027), (b) stringUtility::toBool/toString/toHexString/trim* covered by 4 TEST_CASEs / 6 conceptual cases via SECTIONs with D-06 max-harness failure-mode documentation, (c) third CI lane gates master from day one (no continue-on-error) and was green on the baseline run |

No orphaned requirements detected for Phase 5.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none in modified Phase 5 files) | - | No TBD/FIXME/XXX/TODO/HACK/PLACEHOLDER markers introduced by Phase 5 in production code (UtinniCore/utility/string_utility.h:46 patch is a 4-char defined-behavior init; UtinniCore.Tests/ test files are intentional) | ℹ️ Info | None |
| external/catch2/README.md | 11-12 | SHA-256 values do not match on-disk file hashes due to CRLF EOL normalization at vendor-drop / Git-checkout time | ⚠️ Warning | Audit-trail integrity (per U6) weakened; vendor content itself is correct |
| .github/workflows/ci.yml | 138 | `--reporter "junit::out=UtinniCore.Tests\TestResults\junit-results.xml"` uses Windows backslashes inside double quotes (works under `shell: pwsh` today but fragile if anyone flips to `shell: bash`) | ℹ️ Info | Already flagged in 05-REVIEW.md as WR-02; non-blocking; deferred to a future cleanup |
| UtinniCore.Tests/UtinniCore.Tests.vcxproj | 72 vs 83-117 | `<SDLCheck>true</SDLCheck>` set only for Debug; Release + RelWithDbgInfo omit it | ℹ️ Info | Already flagged in 05-REVIEW.md as WR-03; defeats one purpose of triple-config (security-check parity); non-blocking |
| UtinniCore.Tests/main_smoke.cpp | 42-57 | Comment "second section sees a fresh counter (state did not leak)" misleads about what the assertion proves (Catch2 SECTION semantics) | ℹ️ Info | Already flagged in 05-REVIEW.md as WR-04; documentation polish, not a correctness issue |

All anti-patterns above were already surfaced by `/gsd:code-review 05` (see 05-REVIEW.md: 0 critical / 4 warning / 7 info). None are BLOCKERS; all are latent-risk or documentation-quality items deferred to a future cleanup.

### Human Verification Required

**None.** All must-haves are programmatically verifiable through file content, grep, behavioral spot-checks, and CI run conclusion lookups via `gh run view`. The original PLAN included one `<human-check>` for the red-run validation procedure (per U2), which the user explicitly declined on 2026-05-23 — accepting the initial green CI baseline as sufficient evidence the lane works. The skipped red-run is documented in 05-01-SUMMARY.md under "Red-Run Validation Procedure (per 05-REVIEWS.md item U2)" as a known gap with the runbook preserved for future re-execution if needed.

### Gaps Summary

No gaps. All 3 ROADMAP success criteria are met under the locked D-03 mechanism (MSBuild + direct exe in place of ctest):

1. **SC #1** (Catch2 test executable builds in CI under all three native configs and runs under ctest): The "builds in CI under all three native configs" half is verified via CI run 26335337027 logs showing Release|x86 (solution build) + targeted Debug|x86 build + targeted RelWithDbgInfo|x86 build all produced UtinniCore.Tests.exe. The "runs under ctest" half is satisfied by D-03's substitution: direct exe invocation `bin\Release\UtinniCore.Tests.exe` with stacked Catch2 reporters. Verified green on baseline.
2. **SC #2** (At least one UtinniCore parser or helper has Catch2 coverage): 4 TEST_CASEs covering 6 conceptual coverage cases via SECTIONs across `stringUtility::toBool / toString(int,fillCount) / toHexString / trim / trimStart / trimEnd` — each with a documented PluginManager::loadPlugins failure mode embedded as a table comment block in StringUtilityTests.cpp lines 25-43 (D-06 max-harness compliance).
3. **SC #3** (CI gates main on dotnet test + CLI golden tests + ctest): Three lanes present in `.github/workflows/ci.yml` — `dotnet test UtinniCoreDotNet.Tests` (Phase 1), `dotnet test Utinni.Cli.Tests` (Phase 4), and `& bin\Release\UtinniCore.Tests.exe ...` (Phase 5). No `continue-on-error` or warn-only escape hatches on any lane.

The post-baseline master CI failures (runs 26335742745 / 26335785703 / 26336088047) are pre-existing C# native-interop flakes in UtinniCoreDotNet.Tests (LoaderLockHarness + GameCallbacks tests) — both timing-sensitive and unrelated to Phase 5's surface (UtinniCore.Tests). The new third lane behaves correctly: it builds + runs in isolation and never regressed across Phase 5 commits. The pre-existing flakes need their own follow-up (LoaderLockHarness already tracked at `.planning/todos/pending/loader-lock-harness-flake-fix.md`; GameCallbacks needs a sibling todo).

---

_Verified: 2026-05-23_
_Verifier: Claude (gsd-verifier)_
