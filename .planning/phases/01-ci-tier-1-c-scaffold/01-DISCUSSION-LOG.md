# Phase 1: CI + Tier 1 C# scaffold - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-16
**Phase:** 1-ci-tier-1-c-scaffold
**Areas discussed:** Test project layout, Test framework pin, First smoke test target, CI workflow scope

---

## Test project layout (CON-O-10)

| Option | Description | Selected |
|--------|-------------|----------|
| Sibling project in Utinni.sln | `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` at repo root, added to `Utinni.sln` next to existing projects. Matches the codebase's flat layout per STRUCTURE.md and TESTING.md's first-tests recommendation. Future C++ Catch2 + utinni-cli also land as sibling projects. | ✓ |
| `tests/` subfolder | `tests/UtinniCoreDotNet.Tests/` housing one or more `*.Tests.csproj`. Cleaner separation as tests grow but breaks existing flat-root convention. | |
| Separate `Utinni.Tests.sln` | Parallel test solution. Keeps `Utinni.sln` load fast but CI drives two solutions and ProjectReference across solutions is not idiomatic. | |

**User's choice:** Sibling project in Utinni.sln (Recommended).
**Notes:** Resolves CON-O-10. Future test phases (TEST-02 C++ Catch2 in Phase 5, TEST-03 CLI in Phase 4) inherit the same sibling-project convention.

---

## Test framework pin

| Option | Description | Selected |
|--------|-------------|----------|
| xUnit 2.x | Pin to 2.9.x. Works on net472. Most idiomatic for modern .NET. Matches TESTING.md's recommendation. Constructor-based setup, [Fact]/[Theory], parallel by default. | ✓ |
| NUnit 3.x | 3.13 works on net472. [Test]/[TestCase] attributes. SetUp/TearDown. Slightly more legacy-friendly. | |
| MSTest v2 | Microsoft's first-party framework. [TestMethod] attributes. Built into VS. Less idiomatic in modern .NET community. | |

**User's choice:** xUnit 2.x (Recommended).
**Notes:** xUnit 3 was never an option — net472 hard-pins the framework to the 2.x line. Naming convention `[Method]_[Scenario]_[ExpectedOutcome]` adopted per TESTING.md.

---

## First smoke test target

| Option | Description | Selected |
|--------|-------------|----------|
| Hotkey.ProcessString | Pure string-to-(Keys, Keys) parse at `UtinniCoreDotNet/Hotkeys/Hotkey.cs:66`. Zero refactor needed. Surfaces bug C-08 territory (malformed input throws). | ✓ |
| UndoRedoManager (after refactor) | Stack-based undo/redo at `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs`. Cleaner test surface but requires refactoring `GameCallbacks.AddCleanupSceneCall` (ctor:50) behind a constructor-injected `Action<Action>` first. Refactor touches CON-M-05 preservation. | |
| Both | Hotkey.ProcessString now + UndoRedoManager as Phase 2 follow-up paired with C-07 fix. | |

**User's choice:** Hotkey.ProcessString (Recommended).
**Notes:** Malformed-input test will likely fail today (C-08). Two postures available for Phase 1: ship the malformed test red with skip-pending-Phase-2 marker, or defer the malformed-input test to Phase 2 entirely. Planner to pick. Happy-path tests must pass unconditionally in Phase 1.

---

## CI workflow scope

| Option | Description | Selected |
|--------|-------------|----------|
| Build Release/x86 + dotnet test | Single job on windows-latest. `msbuild Utinni.sln Release/x86` (which runs CON-T-01 post-build) + `dotnet test`. DXSDK NOT installed (Release uses Windows SDK d3d9.h). RelWithDbgInfo deferred to Phase 6 with CON-O-08. | ✓ |
| dotnet test only | Skip full sln build; only build + test the new test project. Faster but misses native-build regressions — defeats the purpose of a green-build gate. | |
| Multi-config matrix | Debug + Release + RelWithDbgInfo in parallel. RelWithDbgInfo needs DXSDK June 2010 on the runner (~570MB, no clean choco package). High setup cost; better as Phase 6 work bundled with CON-O-08. | |

**User's choice:** Build Release/x86 + dotnet test (Recommended).
**Notes:** DXSDK install and multi-config matrix both deferred to Phase 6 (1.0 cut), where CON-O-08 (DXSDK modernization) gets decided. Phase 1 stays the "smallest possible durability unlock" per ROADMAP.md.

---

## Claude's Discretion

- Exact GH Actions YAML (job names, step ordering, NuGet caching, fetch-depth) — researcher + planner decide based on idiomatic CI patterns.
- xUnit 2.9.x patch version — planner picks latest stable 2.x at planning time.
- Whether `dotnet test Utinni.sln` works cleanly with the mixed C++/C# solution vs targeting the test csproj explicitly — researcher to verify.
- README badge markdown formatting/placement details.
- Whether the malformed-input Hotkey test ships red-with-skip-marker or defers to Phase 2 (planner choice — see Specific Ideas in CONTEXT.md).

## Deferred Ideas

- UndoRedoManager testability refactor → Phase 2 (alongside C-07 fix).
- Multi-config CI matrix (Debug + Release + RelWithDbgInfo) → Phase 6.
- DXSDK June 2010 install on CI runner → Phase 6 with CON-O-08.
- Branch protection rules → admin action; not a code deliverable. Document as user follow-up in next-steps.
- `.clang-format` adoption → Phase 6 (STAB-03 cleanups).
- Comprehensive analyzer-rule `.editorconfig` (`prefer var`, no `_` prefix, etc.) → Phase 6 or dedicated future phase.
- Coverage tooling (coverlet, ReportGenerator) → revisit after Phase 4 lands more breadth.
- `Utinni.Tests.sln` parallel solution → rejected unless `Utinni.sln` load time becomes intolerable.
