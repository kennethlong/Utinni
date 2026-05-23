---
phase: 05-tier-1-c-unit-tests
plan: 01
subsystem: testing
tags: [catch2, c++, msbuild, ci, vcxproj]

requires:
  - phase: 04-tier-2-cli-shim-golden-fixtures
    provides: ".github/workflows/ci.yml lane-extension pattern (Phase 4 D-11); cli-test-results artifact-upload precedent"
provides:
  - "Catch2 v3.15.0 vendored at external/catch2/ (amalgamated hpp+cpp, BSL-1.0 LICENSE, durable README with SHA-256)"
  - "UtinniCore.Tests.vcxproj sibling MSBuild test exe with full triple-config (Debug + Release + RelWithDbgInfo)"
  - "Utinni.sln registration honouring CON-T-02 (GUID-scoped 6-mapping postSolution block; no Release-collapse pattern)"
  - "main_smoke.cpp with 4 smoke TEST_CASEs (3 vendor-drop sanity + 1 include-graph compile smoke)"
  - "StringUtilityTests.cpp placeholder (real content owned by 05-02)"
  - "Third CI lane in .github/workflows/ci.yml: triple-config build + Release exe invocation + JUnit artifact upload"
affects:
  - "05-02 (seed coverage; this plan's CI lane gates 05-02's start)"
  - "Phase 6 STAB-03 (vcpkg-vs-vendored re-evaluation; Catch2 is now a Phase 6 input)"
  - "Future native test work (Phase 6 R-A LogSubscribe candidate; reuses this scaffold)"

tech-stack:
  added: [Catch2 v3.15.0]
  patterns:
    - "Vendored amalgamated dependency at external/<name>/ with durable README.md vendor metadata (vendor + version + source URL + license + SHA-256 + vendored-date)"
    - "Sibling MSBuild test exe project producing Catch2 self-runner exe (ConfigurationType=Application)"
    - "GUID-scoped sln registration for triple-config (Debug/Release/RelWithDbgInfo each with .ActiveCfg + .Build.0)"
    - "Stacked Catch2 --reporter flags in CI: console (sets exit code) + junit (writes XML for triage)"
    - "Directory pre-creation step in CI before Catch2 junit-out invocation (std::ofstream does not mkdir -p)"

key-files:
  created:
    - "external/catch2/catch_amalgamated.hpp (547,400 bytes; Catch2 v3.15.0)"
    - "external/catch2/catch_amalgamated.cpp (447,808 bytes; Catch2 v3.15.0)"
    - "external/catch2/LICENSE.txt (Boost Software License 1.0)"
    - "external/catch2/README.md (durable vendor metadata + integrity-verification runbook)"
    - "UtinniCore.Tests/UtinniCore.Tests.vcxproj (sibling MSBuild test exe, triple-config Application)"
    - "UtinniCore.Tests/main_smoke.cpp (4 TEST_CASEs: vendor sanity x3 + include-graph compile smoke x1)"
    - "UtinniCore.Tests/StringUtilityTests.cpp (placeholder; real content lands in 05-02)"
  modified:
    - "Utinni.sln (new project entry + GUID-scoped 6-mapping postSolution block)"
    - ".github/workflows/ci.yml (5 new steps appended after Phase 4 cli-test-results upload)"

key-decisions:
  - "Project GUID for UtinniCore.Tests.vcxproj: {345DFD73-E6CD-4B2F-81AF-6F643193B5F8}"
  - "ConformanceMode (/permissive-) DROPPED from all three configs — Catch2 v3.15.0 amalgamated source does not compile under /permissive- + MSVC v142 (operator<< C2593 ambiguity at .hpp:841 friend declaration vs .cpp:7079 free function); Pitfall 2 in RESEARCH.md was wrong on this point"
  - "MSBuild target name in CI /t: argument uses underscore form (UtinniCore_Tests) because MSBuild MSB5016 rejects '.' in target names; PROJECT name in sln/vcxproj is still UtinniCore.Tests (dot)"
  - "string_utility.cpp NOT compiled into the test exe (per 05-REVIEWS.md item U1); seed tests use header-only inline helpers; eliminates UTINNI_API/EXPORT_UTINNI dllimport conflict"

patterns-established:
  - "Vendored amalgamated test framework at external/catch2/ with durable README metadata (mirrors external/spdlog/, external/imgui/ posture; survives commit-message squash/rewrite per 05-REVIEWS.md item U6)"
  - "Sibling test vcxproj triple-config: Debug + Release + RelWithDbgInfo configs ALL produce a working test exe (no LoaderLockHarness RelWithDbgInfo→Release collapse pattern)"
  - "MSBuild /t:<TargetName> with '.' replaced by '_' (CI invocation escape; project name in sln/vcxproj remains dotted)"
  - "Catch2 JUnit reporter requires directory pre-creation in CI (std::ofstream does not create parent dirs)"

requirements-completed: [TEST-02]

duration: ~25 min
completed: 2026-05-23
---

# Phase 5 Plan 01: Catch2 Scaffold + Smoke Tests + CI Lane Summary

**Catch2 v3.15.0 vendored at external/catch2/ + sibling UtinniCore.Tests.vcxproj producing a triple-config Catch2 self-runner exe, registered in Utinni.sln with GUID-scoped 6-mapping postSolution block, plus a third CI lane in ci.yml building all three configs + running the Release exe with stacked console+junit reporters — gates master from day one (D-04).**

## Performance

- **Duration:** ~25 minutes (executor wall-clock)
- **Started:** 2026-05-23 (worktree-agent-a5574d657be3315cb)
- **Completed:** 2026-05-23
- **Tasks:** 3 of 3 completed
- **Files created:** 7 (4 vendored Catch2 files + 3 test project files)
- **Files modified:** 2 (Utinni.sln + .github/workflows/ci.yml)

## Accomplishments

- Catch2 v3.15.0 vendored at `external/catch2/` (amalgamated hpp+cpp, BSL-1.0 LICENSE, durable README.md with SHA-256 hashes + integrity-verification runbook)
- `UtinniCore.Tests/UtinniCore.Tests.vcxproj` sibling MSBuild test exe builds in all three configs (Debug + Release + RelWithDbgInfo) under MSVC v142; Release and RelWithDbgInfo PDBs have distinct SHA-256 hashes (D-07 anti-collapse verified)
- `bin/Release/UtinniCore.Tests.exe --reporter console` exits 0 and reports `All tests passed (5 assertions in 4 test cases)` — vendor drop is real, exception machinery works, SECTION re-entry produces fresh state, `utility/string_utility.h` include graph compiles under test-project preprocessor defines
- `Utinni.sln` registered the new project between `Utinni.LegacyPlugin` and `Utinni.Cli` with a fresh GUID (`{345DFD73-E6CD-4B2F-81AF-6F643193B5F8}`) and a GUID-scoped 6-mapping postSolution block — every line is `XxxxxxxxInfo|x86.{ActiveCfg|Build.0} = XxxxxxxxInfo|Win32`, no Release-collapse
- `.github/workflows/ci.yml` gained five new steps (Debug build, RelWithDbgInfo build, directory pre-creation, native test run, artifact upload) — gates master from day one per D-04 / 05-REVIEWS.md item C3 + U4

## Task Commits

Each task was committed atomically:

1. **Task 1: Vendor Catch2 v3.15.0 amalgamated at external/catch2/ + README.md metadata** — `d61cff4` (`chore(05-01)`)
2. **Task 2: UtinniCore.Tests.vcxproj + main_smoke.cpp (4 TEST_CASEs) + StringUtilityTests.cpp placeholder + Utinni.sln registration with GUID-scoped 6-mapping** — `542ea34` (`feat(05-01)`)
3. **Task 3: Third CI lane in .github/workflows/ci.yml (triple-config build + Release exe + JUnit artifact)** — `fca5ac2` (`ci(05-01)`)

**Plan metadata:** (this SUMMARY.md commit, to follow)

## Files Created/Modified

| Path | Action | Purpose |
|------|--------|---------|
| `external/catch2/catch_amalgamated.hpp` | created | Catch2 v3.15.0 amalgamated header (547,400 bytes) |
| `external/catch2/catch_amalgamated.cpp` | created | Catch2 v3.15.0 amalgamated translation unit (447,808 bytes; provides main()) |
| `external/catch2/LICENSE.txt` | created | Boost Software License 1.0 (upstream BSL-1.0, NOT grafted with MIT) |
| `external/catch2/README.md` | created | Durable vendor metadata (version, source URL, license, SHA-256 hashes, integrity-verification runbook) |
| `UtinniCore.Tests/UtinniCore.Tests.vcxproj` | created | Sibling MSBuild test exe project (ConfigurationType=Application, triple-config, PlatformToolset=v142, ProjectReference on UtinniCore with LinkLibraryDependencies=false) |
| `UtinniCore.Tests/main_smoke.cpp` | created | 4 TEST_CASEs in 64 lines (3 vendor-drop smoke + 1 include-graph compile smoke per 05-REVIEWS.md item U1) |
| `UtinniCore.Tests/StringUtilityTests.cpp` | created | Placeholder file; only the 23-line MIT header + a single comment line. Real content lands in 05-02. |
| `Utinni.sln` | modified | New project entry inserted between Utinni.LegacyPlugin (line 54) and Utinni.Cli (line 55); GUID-scoped 6-mapping postSolution block appended after the existing Utinni.Cli.Tests block (per 05-REVIEWS.md item U5) |
| `.github/workflows/ci.yml` | modified | Five new steps appended after the existing "Upload CLI test artifacts" step: Debug build, RelWithDbgInfo build, directory pre-creation, Release exe invocation, artifact upload |

## SHA-256 Vendor Hashes (recorded here AND in external/catch2/README.md per 05-REVIEWS.md item U6)

| File | SHA-256 |
|------|---------|
| `external/catch2/catch_amalgamated.hpp` | `DDF4E42976DEA2BBBE8E7464AD5AB156E7061CC8CCEF290E6E406477283483EE` |
| `external/catch2/catch_amalgamated.cpp` | `2AB441B2FA0051A547E88AF4AD98151C1CE1F2FBE3D5E9AD9367CFC2FD44DBF8` |

## Triple-Config Build (per 05-REVIEWS.md item C3)

Verified locally against `Utinni.sln /t:UtinniCore_Tests` (VS 2026 Dev18 MSBuild, MSVC v142 14.29.30133 toolset, x86):

| Config | Build status | Exe path | PDB sha256 |
|--------|-------------|----------|------------|
| Debug | green | `bin/Debug/UtinniCore.Tests.exe` | (built; PDB present) |
| Release | green | `bin/Release/UtinniCore.Tests.exe` | `32ED275BE682B8555957561715FBDA82F963A40D3DB8EE9723E2089A1B07EBB4` |
| RelWithDbgInfo | green | `bin/RelWithDbgInfo/UtinniCore.Tests.exe` | `25EBBB065D46C7A4FE22FD6739B776FDBCB1309DF2707D2AA2899D32EDB1B575` |

**Release vs RelWithDbgInfo PDB SHA-256 mismatch confirmed** — RelWithDbgInfo did NOT silently collapse to Release at the sln-mapping or vcxproj layer. D-07 / CON-T-02 honoured.

## Smoke Run

```
bin\Release\UtinniCore.Tests.exe --reporter console
```

Output:
```
Randomness seeded to: 4228963158
===============================================================================
All tests passed (5 assertions in 4 test cases)
```

Exit code: 0.

The four TEST_CASEs:
1. `Smoke: vendored Catch2 runs` — `REQUIRE(1 + 1 == 2)` (proves amalgamated TU linked, Pitfall 1 mitigation)
2. `Smoke: exception machinery works` — `REQUIRE_THROWS_AS(throw std::runtime_error("boom"), std::runtime_error)` (Pitfall 3 mitigation)
3. `Smoke: SECTION re-entry produces fresh state` — two SECTIONs with shared `counter` local (BDD-runner sanity)
4. `Smoke: utility/string_utility.h include graph compiles` — `REQUIRE(stringUtility::toBool("true") == true)` (per 05-REVIEWS.md item U1 include-graph compile smoke)

## Decisions Made

- **Fresh project GUID:** `{345DFD73-E6CD-4B2F-81AF-6F643193B5F8}` — generated once via `[System.Guid]::NewGuid().ToString().ToUpper()`, applied to (a) the vcxproj's `<ProjectGuid>`, (b) the Utinni.sln project entry's UUID slot, and (c) all six lines of the postSolution mapping block.
- **vcxproj source list:** `catch_amalgamated.cpp` + `main_smoke.cpp` + `StringUtilityTests.cpp`. `string_utility.cpp` deliberately EXCLUDED with an inline XML comment referencing 05-REVIEWS.md item U1 (UTINNI_API/EXPORT_UTINNI dllimport conflict avoidance).
- **ProjectReference posture:** `<ProjectReference Include="..\UtinniCore\UtinniCore.vcxproj">` with `<LinkLibraryDependencies>false</LinkLibraryDependencies>` — preserves CON-T-01 post-build chain ordering without dragging UtinniCore.dll into the test exe's link line.
- **MSBuild target-name escape:** CI `/t:` argument uses `UtinniCore_Tests` (underscore) because MSBuild MSB5016 rejects `.` in target names. The PROJECT name in sln/vcxproj remains `UtinniCore.Tests` (dotted); only the CI command-line target identifier uses the escaped form.
- **ConformanceMode dropped:** see "Deviations from Plan" below.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 — Blocking] Dropped ConformanceMode (/permissive-) from UtinniCore.Tests.vcxproj**

- **Found during:** Task 2 (first `msbuild .../t:UtinniCore_Tests` invocation against Debug|x86 produced ~13 instances of C2593 `'operator <<' is ambiguous` errors inside `external/catch2/catch_amalgamated.cpp`)
- **Issue:** RESEARCH.md §Pitfall 2 claimed Catch2 v3 is clean under `/permissive-`. In practice, with MSVC v142 (14.29.30133) + Catch2 v3.15.0, the amalgamated source's `operator<<(std::ostream&, Catch::StringRef)` is friend-declared in the class at `catch_amalgamated.hpp:841` and standalone-defined at `catch_amalgamated.cpp:7079`. Both candidates resolve to the same function but `/permissive-` treats them as two distinct overloads found via two lookup paths, producing C2593 at every `os << StringRef` call site (XmlWriter, JUnit reporter, console reporter, etc.).
- **Fix:** Removed `<ConformanceMode>true</ConformanceMode>` from all three ItemDefinitionGroups in `UtinniCore.Tests.vcxproj`. Matches UtinniCore.vcxproj's Release config posture (which also has it false). LoaderLockHarness has it true everywhere but doesn't compile Catch2.
- **Files modified:** `UtinniCore.Tests/UtinniCore.Tests.vcxproj`
- **Verification:** All three configs build clean after the fix; smoke exe exits 0 with `All tests passed`.
- **Committed in:** `542ea34` (Task 2 commit)

**2. [Rule 3 — Blocking] CI `/t:` target name uses `UtinniCore_Tests` (underscore), not `UtinniCore.Tests` (dot)**

- **Found during:** Task 2 (initial local build to verify the vcxproj scaffold)
- **Issue:** MSBuild emits `error MSB5016: The name "UtinniCore.Tests" contains an invalid character "."` when `/t:UtinniCore.Tests` is passed on the command line. MSBuild reserves `.` in target identifiers; the documented escape is to substitute `_`. (The PROJECT name in `.sln` and `.vcxproj` remains the dotted form — only the command-line `/t:` argument uses the underscore.)
- **Fix:** All three new CI build steps (`Build native tests Debug|x86`, `Build native tests RelWithDbgInfo|x86`) use `/t:UtinniCore_Tests`. The verification regexes were also updated to match the underscore form.
- **Files modified:** `.github/workflows/ci.yml`
- **Verification:** Local `msbuild Utinni.sln /m /restore /p:Configuration=<cfg> /p:Platform=x86 /t:UtinniCore_Tests` succeeds in all three configs and produces the expected exe at `bin/<cfg>/UtinniCore.Tests.exe`.
- **Committed in:** `fca5ac2` (Task 3 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 3 — blocking).
**Impact on plan:** Both fixes were required to land a buildable + CI-runnable scaffold. No scope creep; both deviations are recorded in their respective commit message bodies AND here for the next planner/researcher's awareness so the next phase doesn't re-trip them.

## Issues Encountered

- **Catch2 + `/permissive-` incompatibility under MSVC v142.** Documented above as deviation 1; the research's HIGH-confidence claim that v3.15.0 builds clean under `/permissive-` was wrong for this toolset + Catch2 version combination. Future Phase 6 STAB-03 should re-evaluate when the toolset bumps (v143 or later may handle the friend/free pair correctly).
- **`UtinniCoreDotNet/Generated/UtinniCore.cs` regenerated by CppSharp post-build.** Each `msbuild Utinni.sln` triggers UtinniCore's post-build chain (per CON-T-01), and CppSharp reorders the `Generated/UtinniCore.cs` file (2,084 lines moved). Reverted via `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs` before committing — out of scope per the SCOPE BOUNDARY rule (pre-existing build-tool noise, not caused by Task 2's changes). Worth flagging for Phase 6 STAB-03 as a possible cleanup candidate (deterministic CppSharp output ordering).

## User Setup Required

None — no external service configuration needed. The CI lane will run automatically on the next push to master.

## Red-Run Validation Procedure (per 05-REVIEWS.md item U2)

**This is the documented manual procedure the USER performs AFTER the plan PR merges to master and the first green CI run on master is confirmed.** The autonomous portion of the lane (workflow wiring) lands green; the red-run validation exercises the failure path.

### Status

| Item | Status |
|------|--------|
| Initial green CI run on master | **pending** (occurs post-merge) |
| Red-run validation (REQUIRE(false) on throwaway branch) | **pending** (user performs after the initial green is confirmed) |
| Red CI run URL | _to be recorded here after the user runs the procedure_ |
| Green master CI run URL (post-merge baseline) | _to be recorded here after the merge_ |

### Step-by-step runbook

1. Confirm the initial green run on `master` after this plan's PR merges. Open the GitHub Actions tab and verify the most-recent master commit's run shows ALL of:
   - `Build native tests (Debug|x86) — triple-config verification per 05-REVIEWS.md item C3` — green
   - `Build native tests (RelWithDbgInfo|x86) — triple-config verification per 05-REVIEWS.md item C3` — green
   - `Create native test results directory (per 05-REVIEWS.md item U4)` — green
   - `Run native unit tests (UtinniCore.Tests.exe, Release|x86)` — green
   - Workflow conclusion — green
   Record the run URL in the table above under "Green master CI run URL".

2. From `master` (after step 1 confirmed green), create a throwaway branch:

   ```bash
   git checkout -b phase05-redrun-validation master
   ```

3. Edit `UtinniCore.Tests/main_smoke.cpp` — add a single line `REQUIRE(false);` inside the first TEST_CASE body (the `Smoke: vendored Catch2 runs` case). Suggested sentinel commit message:

   ```
   chore(phase05): TEMPORARY — verify CI red-on-failure (DO NOT MERGE)

   Adds REQUIRE(false) to main_smoke.cpp's first TEST_CASE so the new
   third CI lane will fail. Confirms (a) the lane goes red on test
   failure (not green-with-warnings), (b) the native-test-results
   artifact uploads on failure, (c) the workflow conclusion is failure.

   This branch must NEVER merge to master. Delete after validation.

   Per 05-REVIEWS.md item U2 + Phase 1 "test the tester" precedent.
   ```

4. `git commit -am "..."` (using the message template above) and `git push origin phase05-redrun-validation`. **Do NOT open a PR to master.**

5. Open the GitHub Actions run for the throwaway branch push. Confirm THREE observations:
   - The `Run native unit tests (UtinniCore.Tests.exe, Release|x86)` step is **RED** (exit non-zero).
   - The `Upload native test artifacts (on failure)` step **ran and uploaded** a `native-test-results` artifact. Download it and verify it contains a parseable `junit-results.xml` with at least one `<failure>` element under the first TEST_CASE.
   - The workflow's overall conclusion is **failure**.
   Record the run URL in the table above under "Red CI run URL".

6. Delete the throwaway branch (no merge; no revert needed because master never touched the failure):

   ```bash
   git push origin --delete phase05-redrun-validation
   git branch -D phase05-redrun-validation
   ```

7. Update this SUMMARY.md by setting both pending statuses to ✅ confirmed and filling in the two run URLs. Commit + push as a follow-up `docs(05-01): record red-run validation evidence`.

## Next Phase Readiness

- 05-02 (seed coverage — `stringUtility::*` round-trip tests) is **CI-gated** on this plan's lane being green on master. Once the user (a) merges this plan to master and (b) confirms the initial green CI run, 05-02 may begin.
- `UtinniCore.Tests/StringUtilityTests.cpp` is a placeholder; 05-02 owns its content. The vcxproj already lists it as a `<ClCompile>` item so the build is stable across both plans.
- Next planners should know:
  - `ConformanceMode` is OFF for the test project (per deviation 1). If a future phase wants `/permissive-` for testability/strictness reasons, it needs to either bump Catch2 to a version that fixes the friend/free pair (probably none in the v3.x line — patch the amalgamated file or wait for a v3.16.x release), or bump the toolset to a version that handles the pattern correctly.
  - The MSBuild `/t:UtinniCore_Tests` (underscore) escape is a permanent CI invocation idiom. Future native test projects following this pattern should expect the same constraint when their names contain `.`.
  - Phase 6 STAB-03 should re-evaluate (a) vcpkg-vs-vendored for Catch2 (D-02 explicitly deferred this), (b) PlatformToolset bump from v142 to v143+ (the `/permissive-` issue may resolve), and (c) deterministic CppSharp output ordering (the `UtinniCoreDotNet/Generated/UtinniCore.cs` rewrite-noise observed during this plan's local builds).

## Self-Check

After writing this SUMMARY.md, verified claims:

- File existence checks:
  - `external/catch2/catch_amalgamated.hpp` — FOUND
  - `external/catch2/catch_amalgamated.cpp` — FOUND
  - `external/catch2/LICENSE.txt` — FOUND
  - `external/catch2/README.md` — FOUND
  - `UtinniCore.Tests/UtinniCore.Tests.vcxproj` — FOUND
  - `UtinniCore.Tests/main_smoke.cpp` — FOUND
  - `UtinniCore.Tests/StringUtilityTests.cpp` — FOUND
  - `bin/Debug/UtinniCore.Tests.exe` — FOUND
  - `bin/Release/UtinniCore.Tests.exe` — FOUND
  - `bin/RelWithDbgInfo/UtinniCore.Tests.exe` — FOUND
- Commit existence checks:
  - `d61cff4` — FOUND (Task 1)
  - `542ea34` — FOUND (Task 2)
  - `fca5ac2` — FOUND (Task 3)

## Self-Check: PASSED

---
*Phase: 05-tier-1-c-unit-tests*
*Plan: 01*
*Completed: 2026-05-23*
