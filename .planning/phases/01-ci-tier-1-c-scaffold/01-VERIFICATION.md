---
phase: 01-ci-tier-1-c-scaffold
verified: 2026-05-16T00:00:00Z
status: passed
score: 10/10 must-haves verified
overrides_applied: 0
re_verification: false
---

# Phase 01: CI + Tier 1 C# Scaffold — Verification Report

**Phase Goal:** A green GitHub Actions Windows-runner CI workflow runs `msbuild Release/x86` and `dotnet test` on every push to `master`, with a smoke-test xUnit project in the solution. This is the smallest possible durability unlock; every subsequent phase gates on this pipeline being green.
**Verified:** 2026-05-16
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Methodology

Goal-backward verification. Must-haves loaded from PLAN frontmatter (01-01-PLAN.md and 01-02-PLAN.md) then merged with ROADMAP.md Success Criteria. All artifacts verified against actual codebase files, not SUMMARY claims. Live CI run evidence provided by orchestrator (run 25970818947 green on master; PR #1 run 25970920246 red on intentional failure).

ROADMAP.md uses "main" throughout Phase 1 details. The actual default branch is "master" (confirmed via gitStatus header and the CI workflow's `branches: [master]` trigger). This is a documented documentation lag — CONTEXT.md D-07 calls it a typo corrected by RESEARCH §Pitfall 6. The branch discrepancy is not a gap: the CI workflow correctly targets master and master is green.

---

## Goal Achievement

### ROADMAP.md Success Criteria (primary contract)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| SC-1 | A push to master triggers a GH Actions Windows-runner workflow that builds Release/x86 and runs `dotnet test`, with a visible green badge | VERIFIED | CI run 25970818947 (5m 48s, all steps green). Badge SVG at `badge.svg?branch=master` returns "passing". Workflow trigger verified in ci.yml lines 6-10: `branches: [master]`. |
| SC-2 | An xUnit test project exists in the Utinni solution and contains at least one smoke test that exercises a real Utinni code path | VERIFIED | `UtinniCoreDotNet.Tests/HotkeyTests.cs` has 4 test methods exercising `Hotkey.ProcessString` via the string ctor — a live production code path via ProjectReference. 2 pass, 2 skip (C-08). |
| SC-3 | A test failure on master blocks the workflow and is visible on the commit | VERIFIED | PR #1 (run 25970920246): intentional `Assert.True(false)` caused workflow to exit red; PR commit showed red X; `.trx` artifact uploaded (2543 bytes); master badge stayed green throughout. PR closed without merging, branch deleted. |
| SC-4 | `.editorconfig` exists at repo root and is applied by the build | VERIFIED | `.editorconfig` present at repo root (commit cf9eb1a); contains `root = true`, `indent_size = 4`, `end_of_line = crlf`, `[*.{cs,cpp,h}]`, `[*.md]`, `[*.{yml,yaml}]`, `[Makefile]` sections. No analyzer rules. |

**Score: 4/4 ROADMAP Success Criteria verified.**

---

### Observable Truths (Plan 01-01 must_haves)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| T1 | `msbuild Utinni.sln /restore /p:Configuration=Release /p:Platform=x86 /m` exits 0 and produces UtinniCoreDotNet.Tests.dll | VERIFIED | CI run 25970818947 Build step: success. Note: local executor environment could not run the full solution build (pre-existing UtinniCoreDotNetGen config mismatch + stale Generated/UtinniCore.cs); CI on windows-2022 is the authoritative build environment and produced exit 0. |
| T2 | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` exits 0 | VERIFIED with deviation | CI run 25970818947 test step: exit 0. Actual: 2 passed + 2 skipped. Plan specified 3 passed + 1 skipped. The second skip is `MultiModifierChord_ParsesFlags` — same C-08 root cause (Hotkey.cs:91 splits on first ' + ', passes "Alt + Z" to Enum.Parse which raises ArgumentException on net472). Both skipped tests carry `C-08` labels; Phase 2 fix target is co-located. The exit code is still 0 (skip is not failure). |
| T3 | CON-T-01 post-build chain runs during the solution build | VERIFIED | CI run 25970818947 build log confirmed: xcopy copied 141 files, UtinniCoreDotNetGen.exe ran, data tree copied. Preservation of STAB-04 confirmed — chain invoked, not refactored. |
| T4 | `.editorconfig` declares 4-space indentation, CRLF, UTF-8, trim trailing whitespace, final newline via `[*]` + per-language sections | VERIFIED | File read directly: `indent_size = 4`, `end_of_line = crlf`, `charset = utf-8`, `trim_trailing_whitespace = true`, `insert_final_newline = true` all present in `[*]` section. Per-language sections confirmed for `.{cs,cpp,h}`, `.md`, `.{yml,yaml}`, `Makefile`. |
| T5 | `external/imgui/.editorconfig` is byte-for-byte unchanged | VERIFIED | `git diff --quiet external/imgui/.editorconfig` exits 0. SHA256 recorded in 01-01-SUMMARY.md: `2B5A1F9649D34A0FD967A5467EC2361E8474C40D819714DDB7014B35124384DD`. |

**Score: 5/5 Plan 01-01 truths verified (T2 has a documented, non-blocking deviation).**

---

### Observable Truths (Plan 01-02 must_haves)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| T6 | Pushing a commit to master triggers `.github/workflows/ci.yml` on a `windows-2022` runner | VERIFIED | ci.yml lines 6-10: `push: branches: [master]`; `runs-on: windows-2022`. Confirmed by CI run 25970818947. |
| T7 | CI job runs: checkout → NuGet cache → setup-msbuild → msbuild solution → dotnet test, in that order | VERIFIED | ci.yml steps 1-5 verified in file (lines 22-84). CI run 25970818947 shows all steps green in this order. |
| T8 | On test failure the workflow exits non-zero and uploads `*.trx` test-results via `actions/upload-artifact@v4` | VERIFIED | ci.yml line 86-92: `if: failure()` upload-artifact step present. PR #1 run 25970920246 confirmed artifact upload of 2543-byte .trx file. |
| T9 | CI uses project-targeted `dotnet test` form (NOT `dotnet test Utinni.sln`) | VERIFIED | ci.yml line 82: `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj`. No `dotnet test Utinni.sln` present. |
| T10 | README.md displays a CI badge under `# Utinni` title pointing at `…badge.svg?branch=master` | VERIFIED | README.md lines 1-4 read directly: line 1 `# Utinni`, line 2 blank, line 3 badge with `badge.svg?branch=master` wrapped deep-link, line 4 blank. Original tagline on line 5 preserved. |
| T11 | GITHUB_TOKEN is explicitly scoped to `contents: read` at the workflow level | VERIFIED | ci.yml lines 12-13: `permissions: contents: read`. |

**Score: 6/6 Plan 01-02 truths verified.**

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `.editorconfig` | Repo-root formatting, `root = true`, CRLF, 4-space, per-language sections | VERIFIED | Exists. Contains all required keys. No analyzer rules. Commit cf9eb1a. |
| `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` | SDK-style, net472, x86, xunit 2.9.3, xunit.runner.visualstudio 3.1.5, Microsoft.NET.Test.Sdk 17.13.0, RestorePackagesWithLockFile, ProjectReference | VERIFIED | Exists. All required elements present. Scope-expansion additions verified: `<Reference Include="System.Windows.Forms" />` and `<AppendPlatformToOutputPath>false</AppendPlatformToOutputPath>` present and explained in comments. |
| `UtinniCoreDotNet.Tests/HotkeyTests.cs` | MIT header (Philip Klatt), namespace UtinniCoreDotNet.Tests, public class HotkeyTests, 4 named test methods, Skip="C-08:", Record.Exception | VERIFIED | Exists (68 lines, >50 min_lines). MIT header lines 1-23 confirmed. All 4 method names present. `[Fact(Skip = "C-08:` on both skipped tests. `Record.Exception` on line 64. |
| `UtinniCoreDotNet.Tests/packages.lock.json` | NuGet lockfile, `"version": 1` | VERIFIED | Exists. First line `"version": 1`. Generated 2026-05-16 via `dotnet restore --use-lock-file`. |
| `Utinni.sln` | Project entry `{FAE04EC0...} = "UtinniCoreDotNet.Tests"`, 5 config mappings (Debug+Release ActiveCfg+Build.0, RelWithDbgInfo ActiveCfg only, NO RelWithDbgInfo Build.0), ProjectDependencies on `{39AB8A43}` | VERIFIED | Project entry on line 25 confirmed. 5 config lines 77-81 confirmed (lines 77: Debug ActiveCfg, 78: Debug Build.0, 79: Release ActiveCfg, 80: Release Build.0, 81: RelWithDbgInfo ActiveCfg — no Build.0). ProjectDependencies `{39AB8A43}` on line 27 confirmed. |
| `.github/workflows/ci.yml` | Single-job CI on windows-2022, triggers on push+PR to master | VERIFIED | Exists. `runs-on: windows-2022` on line 18. `branches: [master]` on lines 8 and 10. No `windows-latest`, no `main`. |
| `README.md` | Badge under `# Utinni` title, `badge.svg?branch=master`, deep-link wrapped | VERIFIED | Line 3 confirmed as `[![CI](...badge.svg?branch=master)](...ci.yml)`. All preserved sections confirmed present. |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` | `UtinniCoreDotNet/UtinniCoreDotNet.csproj` | ProjectReference | VERIFIED | Line 23: `<ProjectReference Include="..\UtinniCoreDotNet\UtinniCoreDotNet.csproj" />` |
| `Utinni.sln` | `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` | Project entry + config mappings | VERIFIED | Project entry line 25; 5 config mapping lines 77-81 verified. |
| `UtinniCoreDotNet.Tests/HotkeyTests.cs` | `UtinniCoreDotNet/Hotkeys/Hotkey.cs` | string ctor exercises ProcessString | VERIFIED | Line 36, 44, 55, 64: `new Hotkey(...)` present in all test methods. ProductionCode reached via ProjectReference (not mocked). |
| `.github/workflows/ci.yml` | `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` | project-targeted dotnet test | VERIFIED | Line 82: `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` exact match. |
| `.github/workflows/ci.yml` | `Utinni.sln` | msbuild solution build | VERIFIED | Line 78: `msbuild Utinni.sln /m /restore /p:Configuration=Release /p:Platform=x86 /p:RestorePackagesConfig=true`. |
| `README.md` | `.github/workflows/ci.yml` | GitHub Actions badge endpoint | VERIFIED | Line 3: `badge.svg?branch=master` and deep-link to `actions/workflows/ci.yml`. |

---

### Data-Flow Trace (Level 4)

Not applicable — phase produces test infrastructure and CI configuration, not user-facing components that render dynamic data. The test project exercises production code directly via ProjectReference; the "data" is the real `Hotkey.ProcessString` behavior, not a UI rendering pipeline.

---

### Behavioral Spot-Checks

CI-based verification substitutes for local runnable checks; the project requires a full MSVC v142 + DXSDK build environment not available in the verification context.

| Behavior | Evidence | Status |
|----------|----------|--------|
| msbuild Release/x86 exits 0 | CI run 25970818947 build step: success | PASS |
| dotnet test exits 0, 4 tests discovered (2 pass, 2 skip) | CI run 25970818947 test step: Total: 4, Passed: 2, Skipped: 2, Failed: 0 | PASS |
| CON-T-01 chain fires (xcopy + UtinniCoreDotNetGen.exe) | CI run 25970818947 log: xcopy 141 files, UtinniCoreDotNetGen.exe ran | PASS |
| Test failure on PR produces red X and uploads .trx | PR #1, run 25970920246: red X on commit, 2543-byte .trx artifact uploaded | PASS |
| Master badge stays green during failing PR | Badge verified via curl during PR #1 lifetime | PASS |

---

### Probe Execution

No conventional `scripts/*/tests/probe-*.sh` probes exist for this phase. The human-verification checkpoint in Task 3 of Plan 01-02 served as the authoritative probe. Evidence:

| Probe | Result | Status |
|-------|--------|--------|
| Part A: CI green on master | Run 25970818947 — success, 5m 48s | PASS |
| Part B: Test-the-tester (intentional failure) | Run 25970920246 — failure as expected; PR #1 red X; .trx uploaded; master badge unchanged | PASS |

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| TEST-01 | 01-01-PLAN.md, 01-02-PLAN.md | Tier 1 C# unit-test scaffold; `dotnet test` in CI; CI status badge green on master | SATISFIED | Test project compiles in CI; 2 passing smoke tests exercise `Hotkey.ProcessString` (a real Utinni code path); CI badge green on master. REQUIREMENTS.md acceptance: "Test project compiles in CI; at least 2–3 file-format parsers have non-trivial coverage; CI status badge green on master" — test count acceptance met (2 passing Facts cover `ProcessString`); the "2-3 file-format parsers" clause is a V1 aggregate across phases; Phase 1 delivers the scaffold, not full parser coverage. |

No orphaned requirements found — REQUIREMENTS.md traceability table maps TEST-01 to Phase 1 only.

---

### Per-Task Verification Map (01-VALIDATION.md rows)

| Row | Behavior | Expected | Actual | Status |
|-----|----------|----------|--------|--------|
| 1 | Test project compiles in CI | msbuild exit 0 | CI run 25970818947: build step success | GREEN |
| 2 | `dotnet test` discovers ≥1 test in CI | `Total tests: ≥1` | Total: 4 | GREEN |
| 3 | ≥1 smoke test exercises a real Utinni code path | 3 pass + 1 skipped (`HotkeyTests` filter) | 2 pass + 2 skipped | GREEN with deviation — 2 passing Facts still exercise real `Hotkey.ProcessString` production code; both skips are C-08–labeled deferrals to Phase 2 |
| 4 | Test failure on master blocks the workflow | Throwaway-branch procedure | PR #1 run 25970920246: red X, .trx uploaded, master badge stayed green | GREEN |
| 5 | CI status badge green on master | SVG shows "passing" | Badge SVG confirmed "passing" | GREEN |
| 6 | `.editorconfig` at repo root applied by build | File presence + sections | File present; all required sections confirmed | GREEN |
| 7 | CON-T-01 post-build chain executes | xcopy + UtinniCoreDotNetGen.exe exit 0 | CI run log: xcopy 141 files, exe ran | GREEN |

**All 7 per-task rows GREEN** (row 3 has a documented count deviation that does not change the green status).

---

### Anti-Patterns Found

Scanned: `.editorconfig`, `.github/workflows/ci.yml`, `README.md`, `Utinni.sln`, `UtinniCore/UtinniCore.vcxproj`, `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj`, `UtinniCoreDotNet.Tests/HotkeyTests.cs`, `UtinniCoreDotNet.Tests/packages.lock.json`.

No `TBD`, `FIXME`, or `XXX` markers found in any phase-01 modified file. No `TODO`, `HACK`, or `PLACEHOLDER` markers found. No empty implementations, hardcoded empty returns, or unresolved placeholders.

The code review (01-REVIEW.md) identified 2 warnings and 6 info items. None are blockers for the phase goal:

| Finding | File | Severity | Phase Goal Impact |
|---------|------|----------|-------------------|
| WR-01: DXSDK installer downloaded without SHA-256 verification | ci.yml:59-66 | WARNING | Does not block goal — CI is green; risk is supply-chain integrity for future runs. Phase 6 hardening candidate. |
| WR-02: DXSDK include/lib paths added to Release\|Win32 only, not Debug\|Win32 | UtinniCore.vcxproj:61-64 | WARNING | Latent — CI only builds Release; debug builds on fresh machines will fail. Does not block Phase 1 goal or any subsequent phase (CI gates on Release). |
| IN-01..IN-06 | Various | INFO | Documentation/robustness improvements; none affect the phase goal or subsequent phase gating. |

---

### Test-Count Deviation Assessment

**Plan specified:** 3 passed + 1 skipped.
**Actual:** 2 passed + 2 skipped.

**Root cause:** `MultiModifierChord_ParsesFlags` was written assuming `Enum.Parse(typeof(Keys), "Alt + Z", true)` returns `Keys.Alt | Keys.Z` on net472. On net472, `Enum.Parse` does not handle the `+` token for flags — it raises `ArgumentException`. This is the same root cause as the existing C-08 malformed-input test. The fix requires changes to `Hotkey.cs:91` and is explicitly deferred to Phase 2.

**Impact on phase goal:** None. The test count deviation does not affect:
- Whether the CI runs (it does)
- Whether the CI exits 0 on a green build (it does — skip is not failure)
- Whether the CI exits non-zero on a real failure (proven by PR #1)
- Whether the badge is green (it is)
- Whether the smoke test exercises a real production code path (2 passing Facts both do)

The phase goal "every subsequent phase gates on this pipeline being green" is achieved. Both deferred tests are labeled `C-08` and co-located for Phase 2 resolution.

---

### D-08 Override Assessment

CONTEXT.md D-08 stated DXSDK would NOT be installed this phase, with Release/x86 building against Windows SDK `d3d9.h`. The actual implementation installs DXSDK June 2010 in CI because `UtinniCore.vcxproj` hardcodes the DXSDK default install path unconditionally (not guarded by configuration). This was a user-authorized scope expansion documented in 01-02-SUMMARY.md.

This deviation from D-08 is a correct response to reality — the pre-existing vcxproj code forced the decision. The STAB-04 preservation guard ("no refactor of CON-T-01 chain") was honored: the CI step installs DXSDK to make the existing vcxproj build, rather than refactoring the vcxproj to remove the DXSDK dependency (which would be a Phase 6 STAB-03 cleanup).

---

### Human Verification Required

No additional human verification items remain. The human-verify checkpoint (Plan 02 Task 3) was exercised and documented:
- Part A (green-on-master): CI run 25970818947 confirmed.
- Part B (test-the-tester): PR #1, run 25970920246 confirmed red, master badge stayed green, PR closed without merging, branch deleted.

The one remaining human-only verification from VALIDATION.md §Manual-Only Verifications (`.editorconfig` reformat-on-save round-trip) is a non-blocking quality check deferred to Phase 6 when `dotnet format --verify-no-changes` is added (RESEARCH §Open Question 4). It does not affect the phase goal.

---

## Decisions Honored

| Decision | Status |
|----------|--------|
| D-01: Sibling project at repo root, no tests/ subfolder | HONORED |
| D-02: net472 + x86 + ProjectReference to UtinniCoreDotNet | HONORED |
| D-03: xunit 2.9.3 + xunit.runner.visualstudio 3.1.5 + Microsoft.NET.Test.Sdk 17.13.0 | HONORED |
| D-04: `[Method]_[Scenario]_[ExpectedOutcome]` naming, HotkeyTests.cs class | HONORED |
| D-05: Hotkey.ProcessString tested via string ctor; passing + Skip(C-08) pattern | HONORED |
| D-06: UndoRedoManager deferred to Phase 2 | HONORED |
| D-07: Single workflow on windows-2022, push+PR to master | HONORED |
| D-08: DXSDK install — OVERRIDDEN by CI reality (user-authorized, documented in 01-02-SUMMARY.md) | OVERRIDDEN (acceptable) |
| D-09: No multi-config matrix | HONORED |
| D-10: Badge under # Utinni title, deep-link wrapped, branch=master | HONORED |
| D-11: Bare formatting .editorconfig, CRLF, no analyzer rules, imgui preserved | HONORED |

---

## Gaps Summary

No blocking gaps. The phase goal is fully achieved.

Non-blocking items for future phases:
1. **WR-01 (Phase 6 candidate):** Add SHA-256 verification to the DXSDK installer download in ci.yml.
2. **WR-02 (Phase 6 / STAB-03 candidate):** Add DXSDK IncludePath/LibraryPath to Debug|Win32 PropertyGroup in UtinniCore.vcxproj for consistency.
3. **C-08 test expansion (Phase 2):** Both skipped tests (`MultiModifierChord_ParsesFlags` and `MalformedInput_DoesNotThrow`) become green when `Hotkey.cs:82,91` is refactored to use `Enum.TryParse`.
4. **01-VALIDATION.md frontmatter (housekeeping):** Set `nyquist_compliant: true` and `wave_0_complete: true` per 01-02-SUMMARY.md §Next Phase Readiness recommendation.

---

_Verified: 2026-05-16_
_Verifier: Claude (gsd-verifier)_
_CI evidence: run 25970818947 (green master), run 25970920246 (red PR #1)_
