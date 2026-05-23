---
phase: 05
slug: tier-1-c-unit-tests
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-23
---

# Phase 05 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `05-RESEARCH.md` §Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Catch2 v3.15.0 (vendored amalgamated build at `external/catch2/`) |
| **Config file** | none — Catch2 v3 doesn't require one; CLI flags drive behavior |
| **Quick run command** | `bin\Release\UtinniCore.Tests.exe --reporter console` |
| **Full suite command** | `bin\Release\UtinniCore.Tests.exe --reporter console --reporter junit::out=UtinniCore.Tests\TestResults\junit-results.xml` |
| **Estimated runtime** | < 5 seconds (smoke + seed coverage; pure inline helpers, no fixtures) |

---

## Sampling Rate

- **After every task commit:** Run `bin\Release\UtinniCore.Tests.exe --reporter console`
- **After every plan wave:** Run full suite (console + junit reporters stacked)
- **Before `/gsd:verify-work`:** Full suite must be green under Debug, Release, and RelWithDbgInfo (CON-T-02 triple-config gate)
- **Max feedback latency:** < 10 seconds (build + run combined)

---

## Per-Task Verification Map

> Filled in during plan-phase by the planner once 05-01 and 05-02 task IDs are assigned.
> Each task's `<verify>` block must map to one row here.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 05-01-XX | 01 | 1 | TEST-02 | — | n/a (test-only target, no security surface) | smoke | `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86 /t:UtinniCore.Tests` | ❌ W0 | ⬜ pending |
| 05-01-XX | 01 | 1 | TEST-02 | — | n/a | smoke | `bin\Release\UtinniCore.Tests.exe --reporter console` | ❌ W0 | ⬜ pending |
| 05-01-XX | 01 | 1 | TEST-02 | — | n/a | smoke (CON-T-02 triple-config) | `msbuild Utinni.sln /p:Configuration=RelWithDbgInfo /p:Platform=x86 /t:UtinniCore.Tests` | ❌ W0 | ⬜ pending |
| 05-01-XX | 01 | 1 | TEST-02 | — | n/a | integration (CI lane) | `.github/workflows/ci.yml` new "Run native unit tests" step + `if: failure()` artifact upload | ❌ W0 | ⬜ pending |
| 05-02-XX | 02 | 2 | TEST-02 | — | n/a | unit (max-harness per D-06) | `bin\Release\UtinniCore.Tests.exe "[utility][string]"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

> All artifacts below MUST exist before any task's automated verify can run. Per RESEARCH.md §Wave 0 Gaps.

- [ ] `external/catch2/catch_amalgamated.hpp` + `.cpp` — vendor drop (Catch2 v3.15.0)
- [ ] `UtinniCore.Tests/UtinniCore.Tests.vcxproj` — sibling MSBuild project, triple-config (Debug + Release + RelWithDbgInfo)
- [ ] `UtinniCore.Tests/main_smoke.cpp` — 2-3 smoke tests proving vendor drop works
- [ ] `UtinniCore.Tests/StringUtilityTests.cpp` — seed coverage (05-02)
- [ ] `Utinni.sln` — register new project + triple-config mappings
- [ ] `.github/workflows/ci.yml` — third CI lane added after Phase 4 CLI step
- [ ] `docs/ai/test-harness-plan.md` "Tier 1 — C++ side" row updated to "Closed by Phase 5"

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Max-harness failure-mode validation (D-06) | TEST-02 | "Test the tester" precedent from Phase 1 — verify the seed tests would FAIL if the covered function were reverted. Not automatable as a regression gate (would intentionally break code), but documented as a one-shot reviewer procedure. | On a throwaway branch: temporarily revert one of the four asserted properties in `string_utility.h` (e.g., remove `std::boolalpha` from `toBool`), run `[utility][string]`, confirm the corresponding TEST_CASE fails, discard branch. Repeat per failure mode in RESEARCH.md §Code Examples > Failure modes table. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] Triple-config CON-T-02 gate exercised (Debug + Release + RelWithDbgInfo)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
