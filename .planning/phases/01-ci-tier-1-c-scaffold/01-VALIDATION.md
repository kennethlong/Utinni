---
phase: 1
slug: ci-tier-1-c-scaffold
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-16
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
>
> Phase 1 IS the validation infrastructure for every subsequent phase. This document is intentionally bootstrapped from `01-RESEARCH.md` §Validation Architecture; see that section for full rationale.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (pinned) |
| **Config file** | `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` (SDK-style with PackageReference; no `.runsettings`, no `xunit.runner.json`) |
| **Quick run command** | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` (after a local `msbuild Utinni.sln /restore /p:Configuration=Release /p:Platform=x86 /m`) |
| **Full suite command** | Identical — only one test project this phase |
| **Estimated runtime** | < 5 s (single-class xUnit suite, no fixtures) |
| **Discovery adapter** | `xunit.runner.visualstudio 3.1.5` (runs xUnit 2.x tests on net472) |
| **Test result format** | `.trx` (TestResults/<timestamp>.trx) + console; uploaded as `actions/upload-artifact@v4` on failure |

---

## Sampling Rate

- **After every task commit (developer local):** `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` (after a local msbuild).
- **After every plan wave:** CI runs the full pipeline (`msbuild` then `dotnet test`) on push.
- **Before `/gsd:verify-work`:** CI must be green on `master` AND the test-the-tester procedure (below) must be exercised once.
- **Max feedback latency:** ~3–5 minutes per CI run (cold cache); ~1–2 minutes (warm NuGet cache).

---

## Per-Task Verification Map

(Plan-level task IDs filled in during /gsd:plan-phase — this is the per-requirement skeleton.)

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| TEST-01 | Test project compiles in CI | infrastructure | `msbuild Utinni.sln /restore /p:Configuration=Release /p:Platform=x86` — exit 0 | ❌ W0 | ⬜ pending |
| TEST-01 | `dotnet test` discovers ≥1 test in CI | infrastructure | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` — log shows `Total tests: ≥1` | ❌ W0 | ⬜ pending |
| TEST-01 | ≥1 smoke test exercises a real Utinni code path | unit | `dotnet test ... --filter "FullyQualifiedName~HotkeyTests"` — 3 `[Fact]`s pass, 1 `[Fact(Skip="C-08: ...")]` skipped | ❌ W0 | ⬜ pending |
| TEST-01 | Test failure on `master` blocks the workflow | infrastructure (test-the-tester) | Throwaway-branch procedure (see below) | ❌ W0 | ⬜ pending |
| TEST-01 (acceptance) | CI status badge green on `master` | infrastructure | `https://github.com/kennethlong/Utinni/actions/workflows/ci.yml/badge.svg?branch=master` returns SVG showing "passing" | ❌ W0 | ⬜ pending |
| TEST-01 (acceptance) | `.editorconfig` at repo root applied by build | infrastructure | File presence + IDE/editor reformat behavior on save | ❌ W0 | ⬜ pending |
| (Implied) | CON-T-01 post-build chain executes under CI | infrastructure | Build log shows `xcopy` line resolved AND `UtinniCoreDotNetGen.exe` exit 0 AND `UtinniCoreDotNet/Generated/UtinniCore.cs` mtime updated | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Phase 1 IS the Wave 0 build-out. The "gaps" are the deliverables themselves:

- [ ] `.github/workflows/ci.yml` — full workflow file (windows-2022, msbuild + dotnet test, badge target on `master`)
- [ ] `.editorconfig` — repo-root formatting (4-space, Allman, CRLF-tolerant, trim trailing, final newline)
- [ ] `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` — SDK-style, net472, x86, PackageReference (`xunit 2.9.3`, `xunit.runner.visualstudio 3.1.5`, `Microsoft.NET.Test.Sdk 17.13.0`)
- [ ] `UtinniCoreDotNet.Tests/HotkeyTests.cs` — 3 passing `[Fact]` (single-key, modifier-chord, multi-modifier) + 1 `[Fact(Skip = "C-08: ...")]` (malformed input)
- [ ] `Utinni.sln` — add project entry + Debug|x86 / Release|x86 configuration mappings; project dependency `UtinniCoreDotNet.Tests` → `UtinniCoreDotNet`
- [ ] `README.md` — CI badge at top under the title, link to `actions/workflows/ci.yml?query=branch%3Amaster`
- [ ] (Recommended) `UtinniCoreDotNet.Tests/packages.lock.json` — committed after first `msbuild /restore`, gives deterministic CI

**No framework install needed** — xUnit + Test SDK ship as NuGet packages, pulled by `msbuild /restore`. `microsoft/setup-msbuild@v2` handles the build tool on the runner.

**No conftest / shared fixtures needed** — single test class, no setup beyond xUnit's default ctor.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| "A test failure on `master` blocks the workflow and is visible on the commit" | TEST-01 (acceptance #3) | The only honest way to prove the alarm works is to fire it once on a throwaway branch — automating this would require polluting `master`'s history. | **Test-the-tester procedure** (one-shot, ~5 min): (1) After CI is green on `master`, create branch `verify/test-tester`. (2) Add `[Fact] public void Verify_TestRunner_FailsBuild_OnAssertFalse() => Assert.True(false, "intentional");` to `HotkeyTests.cs`. (3) Push and open a PR against `master`. (4) Confirm: workflow runs on PR, exits red, PR shows red X on commit, test-results artifact uploaded. (5) Close PR without merging; delete branch. (6) Record in `01-VERIFICATION.md` (PR URL + screenshots of red X and the still-green `master` badge). |
| `.editorconfig` reformat-on-save round-trip | TEST-01 (acceptance #4) | Visual confirmation in an editor — no command-line equivalent without adding `dotnet format` (deferred to Phase 6). | Open any `.cs` file in VS / VS Code with the EditorConfig extension active, deliberately use tabs instead of 4-space indent, save, confirm reformatted to 4-space. |

---

## Validation Sign-Off

- [ ] All Phase 1 tasks have either an automated verify command or a Wave 0 dependency
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify (Phase 1 is small enough this is trivially satisfied)
- [ ] Wave 0 covers all MISSING references (every deliverable above is created in Phase 1 itself)
- [ ] No watch-mode flags in any test command
- [ ] Feedback latency < 5 minutes per CI run (cold cache); under 2 minutes warm
- [ ] Test-the-tester procedure exercised once on a throwaway branch
- [ ] `nyquist_compliant: true` set in frontmatter after the above

**Approval:** pending
