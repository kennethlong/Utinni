# Phase 1: CI + Tier 1 C# scaffold - Context

**Gathered:** 2026-05-16
**Status:** Ready for planning

<domain>
## Phase Boundary

A green GitHub Actions Windows-runner workflow on `main` that builds `Utinni.sln` Release/x86 (which exercises the CON-T-01 post-build chain) and runs `dotnet test` against a new sibling `UtinniCoreDotNet.Tests` xUnit 2.x project containing at least one real smoke test (`Hotkey.ProcessString`). A `.editorconfig` at the repo root applies during build. Test failure on `main` blocks the workflow and surfaces on the commit. CI status badge visible in `README.md`.

**In scope:** GH Actions workflow file; new sibling test project added to `Utinni.sln`; `Hotkey.ProcessString` tests; `.editorconfig` (formatting-only); CI status badge in README; documentation of the runner image + toolset chosen.

**Out of scope (this phase):** Code fixes for any bug surfaced by the tests (those belong to Phase 2); UndoRedoManager testability refactor (defer to Phase 2 alongside C-07); C++ Catch2 (Phase 5); CLI golden-file tests (Phase 4); multi-config matrix builds (Phase 6, paired with CON-O-08 DXSDK decision); branch protection rules (admin action, not a code deliverable); any refactor of the CON-T-01 post-build chain itself.

</domain>

<decisions>
## Implementation Decisions

### Test Project Layout (resolves CON-O-10)
- **D-01:** Sibling project at repo root: `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj`, added to `Utinni.sln` next to existing projects. Matches the flat-root layout used by every other project. Future C# test projects, the C++ Catch2 project (Phase 5), and `utinni-cli` (Phase 4) all follow the same sibling-project convention — no `tests/` subfolder, no parallel `Utinni.Tests.sln`.
- **D-02:** Test project targets `net472`, `x86`, references `UtinniCoreDotNet` via `<ProjectReference>`. Same platform target as the system under test — non-negotiable because `UtinniCoreDotNet` is `x86`-only.

### Test Framework Pin
- **D-03:** **xUnit 2.x** (pin to the 2.9.x line). xUnit 3 is not an option — it requires .NET 6+ and Utinni's managed surface is locked on .NET Framework 4.7.2 (CON-P-01 territory; broader move off net472 is a separate V2-class decision per assessment.md). Packages: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`. Test discovery via `dotnet test`.
- **D-04:** Test naming convention adopted: `[Method]_[Scenario]_[ExpectedOutcome]` (per TESTING.md's "no precedent to deviate from" recommendation). Test classes named `<TypeName>Tests.cs` (e.g. `HotkeyTests.cs`).

### First Smoke Test Target
- **D-05:** `Hotkey.ProcessString` (at `UtinniCoreDotNet/Hotkeys/Hotkey.cs:66`). Pure string-to-`(Keys, Keys)` parse, zero refactor required, exercises a real production code path. Test cases at minimum: valid single-key, valid modifier-chord (`Control+S`), valid multi-modifier (`Shift+Alt+Z`), and **malformed input** (the C-08 territory — this test will likely FAIL today and stay red until Phase 2's C-08 fix lands; that's the intended regression guard).
- **D-06:** `UndoRedoManager` testability work is **deferred to Phase 2**, where it pairs with the C-07 fix (thread-safety + dead `AllowMerge`). The refactor needed to test `UndoRedoManager` (injecting `GameCallbacks.AddCleanupSceneCall` behind an `Action<Action>` constructor parameter) touches CON-M-05 territory and is best done alongside the bug it tests for.

### CI Workflow Scope
- **D-07:** Single workflow file (`.github/workflows/ci.yml`) on `windows-latest` (or `windows-2022` if a deterministic pin is preferred) running on push to `main` and on pull request:
  1. Checkout
  2. Setup MSBuild (microsoft/setup-msbuild action) — needs MSVC v142 toolset (VS 2019 build tools). Researcher to confirm the cleanest action to install v142 on a windows-2022 runner since native VS 2019 runners are deprecated.
  3. `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86 /restore` — this triggers the CON-T-01 post-build chain (`xcopy data/` + run `UtinniCoreDotNetGen.exe`). Both must succeed for the build to be green.
  4. `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` (or use `dotnet test Utinni.sln` if it cleanly handles the mixed C++/C# solution — researcher to verify).
- **D-08:** **DXSDK June 2010 is NOT installed on the CI runner this phase.** Release/x86 builds against the Windows SDK's `d3d9.h` per STACK.md. `RelWithDbgInfo` (which does need DXSDK) is **not built in CI this phase**. The DXSDK question (CON-O-08) is owned by Phase 6 and the multi-config matrix is bundled with it.
- **D-09:** No multi-config matrix this phase. Just Release/x86. Bigger matrix is Phase 6 work.
- **D-10:** CI status badge in `README.md` at the top, pointing at the workflow's `main` branch status.

### .editorconfig
- **D-11:** Bare formatting `.editorconfig` at repo root: 4-space indent, never tabs, Allman braces, UTF-8, LF or CRLF (match existing repo norm — files appear to be CRLF), trim trailing whitespace, final newline. Codifies the de-facto conventions documented in CONVENTIONS.md without enforcing analyzer rules. Comprehensive analyzer rules (`prefer var`, `no _ prefix`, etc.) are deferred — they belong with the `.clang-format` pass in Phase 6 (STAB-03 cleanups). The vendored `external/imgui/.editorconfig` already exists and must remain untouched.

### Claude's Discretion
- Exact GH Actions YAML structure (job name, step names, caching strategy, fetch-depth) — researcher and planner to decide based on idiomatic CI patterns.
- Whether to include `actions/cache@v4` for NuGet packages on the test project (likely yes for speed; pure planner call).
- Exact xUnit 2.9.x patch version pin — pick the latest stable 2.x at planning time.
- README badge markdown formatting and placement details.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context (locked decisions, requirements, constraints)
- `.planning/PROJECT.md` — V1 milestone scope, anti-goals (DEC-A1..A4), preservation guard-rails, four candidate decisions (DEC-C1..C3).
- `.planning/REQUIREMENTS.md` §TEST-01 — Tier 1 C# unit-test scaffold acceptance criteria (the requirement this phase delivers).
- `.planning/ROADMAP.md` §"Phase 1" — phase goal, success criteria, preservation guard-rails (CON-T-01).
- `.planning/intel/constraints.md` §CON-O-10, §CON-P-01, §CON-TT-01, §CON-TT-03 — open question being resolved + platform-only + testing philosophy + FlaUI-skipped.
- `.planning/intel/decisions.md` — D-08 (tiered testing strategy) candidate decision.

### Codebase intel (read-only reference)
- `.planning/codebase/TESTING.md` — verified zero-tests baseline + recommended first-tests list (Hotkey.ProcessString is recommendation #1). xUnit 3 / net472 constraint noted here.
- `.planning/codebase/STACK.md` §"Runtime" + §"Testing" + §"Build" — net472, x86, MSVC v142 (VS 2019), no test framework configured today, no NuGet for the C# projects, MSBuild via Utinni.sln.
- `.planning/codebase/STRUCTURE.md` §"Directory Layout" + §"Where to Add New Code" — flat-root project layout convention.
- `.planning/codebase/CONVENTIONS.md` §"Code Style" — Allman braces, 4-space indent, no `_` prefix, PascalCase test class naming; informs `.editorconfig` defaults.
- `.planning/codebase/INTEGRATIONS.md` — CON-T-01 post-build chain details.

### Source documents (immutable inputs from ingest)
- `docs/ai/test-harness-plan.md` §"Four tiers" + §"Open questions" — Tier 1 definition; CON-O-10 origin.
- `docs/ai/assessment.md` §"Critical issues" C-08 + §"Open questions" + §"Smoke-test xUnit project third" recommendation — context for the Hotkey.ProcessString failing-test-as-guard pattern and DXSDK June 2010 dependency notes.
- `docs/ai/vision.md` — product target (informs why we test pure-logic + file-format layers first).

### Build / preservation surface this phase touches
- `Utinni.sln` — adds one new project (`UtinniCoreDotNet.Tests`).
- `UtinniCore/UtinniCore.vcxproj` §lines 91-94 — CON-T-01 post-build chain. CI must invoke it; no refactor.
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj` — referenced by new test project; not modified.
- `UtinniCoreDotNet/Hotkeys/Hotkey.cs:66` — system under test for the smoke test.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`UtinniCoreDotNet/Hotkeys/Hotkey.cs:66` (`ProcessString`)**: Pure string-to-(`Keys`, `Keys`) parser. No native deps, no static state. Ideal first smoke test. Symmetric round-trip with `GetKeyComboString` (line 105) gives a clean property-style test if desired.
- **Vendored CppSharp at `external/CppSharp/`**: Used at build time by `UtinniCoreDotNetGen` via the post-build chain. CI doesn't need separate CppSharp install — vendored binaries are sufficient.
- **`Utinni.sln` flat-root layout**: New `UtinniCoreDotNet.Tests` slot in naturally next to `UtinniCoreDotNet`, `UtinniCoreDotNetGen`, etc.

### Established Patterns
- **PascalCase project + file naming** (CONVENTIONS.md): `UtinniCoreDotNet.Tests.csproj`, `HotkeyTests.cs`. Follow the type-mirrors-filename rule.
- **No package manager for managed projects today**: `UtinniCoreDotNet.csproj` has no `<PackageReference>`. The new test project will be the first to introduce NuGet packages (`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`). Use `<PackageReference>` (PackageReference style, not `packages.config`) for forward compatibility.
- **AnyCPU vs x86 split**: `UtinniCoreDotNetGen` is x64 (build-time tool); `UtinniCoreDotNet` is x86 (runtime). Test project must be **x86** to match the system under test.
- **MIT license header on every C# source file**: 23-line block at top. New test files must include it.

### Integration Points
- **`Utinni.sln`** — adds one project entry + configuration mappings (Debug|x86, Release|x86; can skip RelWithDbgInfo|x86 since the test project doesn't ship with the runtime). Project dependency: `UtinniCoreDotNet.Tests` → `UtinniCoreDotNet`.
- **CON-T-01 post-build chain** (`UtinniCore.vcxproj:91-94`) — CI must build `UtinniCore` BEFORE the test project so `UtinniCoreDotNet/Generated/UtinniCore.cs` is fresh; the existing `Utinni.sln` dependency graph already enforces this (`UtinniCoreDotNet` depends on `UtinniCore`).
- **GitHub Actions runner image** — windows-2022 is current; native VS 2019 runners are deprecated. v142 toolset must be installed (it's available on windows-2022 via "Desktop development with C++" / VS 2019 Build Tools); researcher to nail down the exact action sequence.

</code_context>

<specifics>
## Specific Ideas

- **The "failing test as regression guard" pattern is intentional.** The malformed-input test on `Hotkey.ProcessString` is expected to fail today (C-08 says ProcessString throws on bad `input.ini` tokens). The test ships in Phase 1 red, sits red until Phase 2's C-08 fix makes it green. This is the design: tests document the desired contract before the fix lands. The CI workflow must therefore allow this single test to be marked `Skip` or `xfail`-equivalent with an `// XXX(C-08): expected to fail until Phase 2` comment, OR the malformed test is deferred to Phase 2 and only the happy-path tests ship in Phase 1. **Planner to choose** which posture (skip-with-comment vs defer-the-malformed-test). The happy-path tests must pass in Phase 1 unconditionally.
- **CI badge placement**: top of `README.md`, just under the title. Match common open-source convention.
- **No `.runsettings` this phase**: keep configuration in the `.csproj` and the CI yaml. Add a `.runsettings` only if/when a test phase needs it.

</specifics>

<deferred>
## Deferred Ideas

- **UndoRedoManager testability refactor** → Phase 2 (pairs with C-07 fix). The `GameCallbacks.AddCleanupSceneCall` injection seam refactor lands alongside the bug fix it enables testing for.
- **Multi-config CI matrix (Debug + Release + RelWithDbgInfo)** → Phase 6 (1.0 cut). Bundled with the CON-O-08 DXSDK June 2010 vs Windows 10 SDK decision.
- **DXSDK June 2010 install on CI runner** → Phase 6 with CON-O-08.
- **Branch protection rules** (required check, dismissal policy, force-push prohibition) → admin action; document as user task in `confirm_creation` next steps but not a code deliverable.
- **`.clang-format` adoption** → Phase 6 (STAB-03 cleanups).
- **Comprehensive analyzer-rule .editorconfig** (`prefer var`, `no _ prefix`, etc.) → Phase 6 or a dedicated future phase.
- **Coverage tooling (coverlet, ReportGenerator)** → not in V1; revisit after Phase 4 (CLI golden tests) lands more breadth.
- **`Utinni.Tests.sln` parallel solution** → rejected this phase; revisit only if `Utinni.sln` load time becomes intolerable (unlikely with one test project added).

</deferred>

---

*Phase: 01-ci-tier-1-c-scaffold*
*Context gathered: 2026-05-16*
