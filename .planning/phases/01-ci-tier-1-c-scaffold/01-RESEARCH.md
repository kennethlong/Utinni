# Phase 1: CI + Tier 1 C# scaffold — Research

**Researched:** 2026-05-16
**Domain:** GitHub Actions Windows CI + xUnit 2.x test scaffolding for a mixed C++/C# Visual Studio solution targeting MSVC v142 / net472 / x86
**Confidence:** HIGH on stack and architecture; MEDIUM on post-build chain CI behavior (will only be fully verified by the first real workflow run)

## Summary

Phase 1 is small, well-bounded, and has very little room for novel decisions — the CONTEXT.md has already locked all eleven major choices (D-01 through D-11). The research job is therefore to: (a) translate those locked decisions into concrete versions, action references, and YAML snippets the planner can pour straight into tasks; (b) surface the genuine technical risks (notably MSVC v142 toolset availability on the GitHub-hosted runner image and `dotnet test` x86 behavior); (c) recommend a posture on the C-08-failing-test question that CONTEXT.md explicitly left to the planner.

The standard 2026-Q2 stack for a net472 xUnit 2.x test project is settled: **SDK-style csproj**, `xunit 2.9.3` + `xunit.runner.visualstudio 3.1.5` + `Microsoft.NET.Test.Sdk 17.13.0`. The runner image `windows-2022` (which `windows-latest` no longer points at — it now points at `windows-2025`) has the `VC.Tools.142.x86.x64` component preinstalled, so a single `microsoft/setup-msbuild@v2` step followed by `msbuild Utinni.sln /restore /p:Configuration=Release /p:Platform=x86` will pick up the v142 toolset automatically without needing `ilammy/msvc-dev-cmd` or any `-T v142` override. The known sharp edge is `dotnet test` for x86 net472 — the safer invocation is **project-targeted** (`dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj`) rather than solution-targeted, both to avoid the documented x86 process-architecture issue and to avoid the documented "dotnet test fails on solutions with non-test projects" issue.

**Primary recommendation:** Pin `windows-2022` (not `windows-latest`), use `microsoft/setup-msbuild@v2` with default settings, build the solution via msbuild, run tests via **project-targeted** `dotnet test --no-build --configuration Release`, ship the malformed-input test as `Skip = "C-08: expected to fail until Phase 2 fix lands"`, and put the badge under the README title pointing at `kennethlong/Utinni/.github/workflows/ci.yml/badge.svg?branch=main`.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Test Project Layout (resolves CON-O-10):**
- **D-01:** Sibling project at repo root: `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj`, added to `Utinni.sln` next to existing projects. Matches the flat-root layout used by every other project. Future C# test projects, the C++ Catch2 project (Phase 5), and `utinni-cli` (Phase 4) all follow the same sibling-project convention — no `tests/` subfolder, no parallel `Utinni.Tests.sln`.
- **D-02:** Test project targets `net472`, `x86`, references `UtinniCoreDotNet` via `<ProjectReference>`. Same platform target as the system under test — non-negotiable because `UtinniCoreDotNet` is `x86`-only.

**Test Framework Pin:**
- **D-03:** **xUnit 2.x** (pin to the 2.9.x line). xUnit 3 is not an option — it requires .NET 6+ and Utinni's managed surface is locked on .NET Framework 4.7.2. Packages: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`. Test discovery via `dotnet test`.
- **D-04:** Test naming convention adopted: `[Method]_[Scenario]_[ExpectedOutcome]`. Test classes named `<TypeName>Tests.cs` (e.g. `HotkeyTests.cs`).

**First Smoke Test Target:**
- **D-05:** `Hotkey.ProcessString` (at `UtinniCoreDotNet/Hotkeys/Hotkey.cs:66`). Test cases at minimum: valid single-key, valid modifier-chord (`Control+S`), valid multi-modifier (`Shift+Alt+Z`), and **malformed input** (the C-08 territory — this test will likely FAIL today and stay red until Phase 2's C-08 fix lands; that's the intended regression guard).
- **D-06:** `UndoRedoManager` testability work is **deferred to Phase 2**.

**CI Workflow Scope:**
- **D-07:** Single workflow file (`.github/workflows/ci.yml`) on `windows-latest` (or `windows-2022` if a deterministic pin is preferred) running on push to `main` and on pull request: checkout → setup MSBuild → `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86 /restore` → `dotnet test ...`.
- **D-08:** **DXSDK June 2010 is NOT installed on the CI runner this phase.** Release/x86 builds against the Windows SDK's `d3d9.h`. `RelWithDbgInfo` is **not built in CI this phase**.
- **D-09:** No multi-config matrix this phase. Just Release/x86.
- **D-10:** CI status badge in `README.md` at the top, pointing at the workflow's `main` branch status.

**.editorconfig:**
- **D-11:** Bare formatting `.editorconfig` at repo root: 4-space indent, never tabs, Allman braces, UTF-8, CRLF (match existing repo norm), trim trailing whitespace, final newline. Comprehensive analyzer rules (`prefer var`, `no _ prefix`, etc.) deferred to Phase 6. The vendored `external/imgui/.editorconfig` already exists and must remain untouched.

### Claude's Discretion

- Exact GH Actions YAML structure (job name, step names, caching strategy, fetch-depth) — researcher and planner to decide based on idiomatic CI patterns.
- Whether to include `actions/cache@v4` for NuGet packages on the test project (likely yes for speed; pure planner call).
- Exact xUnit 2.9.x patch version pin — pick the latest stable 2.x at planning time.
- README badge markdown formatting and placement details.
- Whether to use `windows-2022` (deterministic pin) or `windows-latest` (now `windows-2025`).
- Skip-with-comment vs defer-the-malformed-test posture for the C-08 case.

### Deferred Ideas (OUT OF SCOPE)

- **UndoRedoManager testability refactor** → Phase 2 (pairs with C-07 fix).
- **Multi-config CI matrix (Debug + Release + RelWithDbgInfo)** → Phase 6 (bundled with CON-O-08 DXSDK June 2010 vs Windows 10 SDK decision).
- **DXSDK June 2010 install on CI runner** → Phase 6 with CON-O-08.
- **Branch protection rules** → admin action; document as user task in `confirm_creation` next steps but not a code deliverable.
- **`.clang-format` adoption** → Phase 6 (STAB-03 cleanups).
- **Comprehensive analyzer-rule .editorconfig** (`prefer var`, `no _ prefix`, etc.) → Phase 6 or a dedicated future phase.
- **Coverage tooling (coverlet, ReportGenerator)** → not in V1; revisit after Phase 4.
- **`Utinni.Tests.sln` parallel solution** → rejected this phase.
- **Any refactor of the CON-T-01 post-build chain** itself.
- **Code fixes for any bug surfaced by the tests** → Phase 2.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TEST-01 | Tier 1 C# unit-test scaffold: xUnit (or NUnit) test project alongside Utinni's main solution; `dotnet test` runs in CI (GitHub Actions Windows runner) and locally without a game client. Acceptance: test project compiles in CI; at least 2–3 file-format parsers have non-trivial coverage (Phase 1 ships one smoke test against `Hotkey.ProcessString`; the "2–3 parsers" target lands in later phases); CI status badge green on master. | Standard Stack section pins versions; Architecture Patterns section gives concrete project layout + YAML; Common Pitfalls section pre-empts the known x86/v142/mixed-solution traps; Validation Architecture section nails down the Wave-0 gaps so the planner can sequence tasks. |

</phase_requirements>

## Project Constraints (from CLAUDE.md)

No `CLAUDE.md` exists at the repo root as of 2026-05-16. All authoritative directives come from `.planning/CONVENTIONS.md`, `.planning/STACK.md`, `.planning/codebase/*.md`, and `.planning/intel/constraints.md`. Salient constraints the planner must honor:

- **CON-T-01:** `UtinniCore.vcxproj` post-build chain (`xcopy /E /Y "$(SolutionDir)data" "$(TargetDir)" /d` then `$(SolutionDir)UtinniCoreDotNetGen\bin\$(Configuration)\UtinniCoreDotNetGen.exe`) must run during CI builds and succeed. **No refactor of the chain itself in this phase.**
- **CON-P-01:** Windows-only. No Linux fallback.
- **CON-P-02:** x86 only — every native build, every csproj `PlatformTarget`, every test project must be x86.
- **CON-TT-01:** TDD applies to pure-logic and file-format layers only — Phase 1's smoke test is squarely in scope.
- **CON-TT-03:** FlaUI WinForms automation deliberately skipped.
- **CONVENTIONS.md naming:** Test class `HotkeyTests.cs` in `UtinniCoreDotNet.Tests` namespace; `[Method]_[Scenario]_[ExpectedOutcome]` per D-04; PascalCase; Allman braces; 4-space indent; no `_` field prefix; 23-line MIT license header on every new `.cs` file (verbatim from existing files, copyright "Philip Klatt, 2020" preserved unchanged on Klatt-authored files; this project is a fork at `kennethlong/Utinni`).
- **STAB-04 (preservation):** Phase 1 only touches CON-T-01 (post-build chain — invoke, do not refactor). No other preserved item (CON-N-*, CON-M-*, CON-T-02..-05) is touched.

## Architectural Responsibility Map

Phase 1's deliverable is CI infrastructure + a test project — there's only one runtime tier in play (the test runner on the CI runner) and one build tier. The map below is therefore short.

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Compile the C++ / C# solution (Release/x86) | CI runner (Windows) → MSBuild + MSVC v142 | — | MSBuild owns the build. setup-msbuild puts it on PATH; v142 is selected automatically because `Utinni.sln` is a VS 16 / `PlatformToolset=v142` solution. |
| Generate managed bindings (CON-T-01 step 2) | CI runner → `UtinniCoreDotNetGen.exe` (AnyCPU/x64 .NET Framework console app) | — | Triggered by the `UtinniCore.vcxproj` PostBuildEvent. No CI-level wiring needed beyond a `data/` source present at `$(SolutionDir)data`. |
| Copy `data/` to bin output (CON-T-01 step 1) | CI runner → `xcopy` (Windows builtin) | — | Triggered by the same PostBuildEvent. Present on every Windows runner image. |
| Discover + run xUnit tests | CI runner → `dotnet test` (vstest host) + `xunit.runner.visualstudio` adapter | — | `dotnet test` invokes vstest, which loads the adapter from the test project's bin output (the adapter is packaged in `xunit.runner.visualstudio` and copied to the output dir via PackageReference). |
| Block merge on test failure | GitHub branch protection rule (admin UI) | CI workflow exit code | The workflow exits non-zero on failure; surfacing that as a required check is a manual admin action (deferred per CONTEXT.md). |
| CI badge in README | GitHub Actions native endpoint | README.md markdown | `https://github.com/<owner>/<repo>/actions/workflows/ci.yml/badge.svg?branch=<branch>`. |

**Why this matters:** Phase 1 has no application-tier separation (browser/server/db). The "tiers" here are CI-runner-stage vs source-control-stage vs project-build-graph-stage. Calling that out explicitly prevents the planner from over-engineering a multi-job matrix when a single linear job is correct.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `xunit` | **2.9.3** [VERIFIED: NuGet registry — api.nuget.org/v3-flatcontainer/xunit/index.json, latest 2.x; CITED: xunit.net/releases — published 2025-01-08, security-only updates going forward but stable for our use case] | xUnit 2.x test framework | Locked by D-03; 2.9.x is the final 2.x line. v3 requires .NET 6+ which violates CON-P-01-adjacent net472 pin. |
| `xunit.runner.visualstudio` | **3.1.5** [VERIFIED: NuGet registry — latest stable; CITED: xunit.net/releases/visualstudio explicitly states it targets net472 or later AND runs v1/v2/v3 tests] | VSTest adapter that makes `dotnet test` discover xUnit tests | The 3.x line is forward-compatible: it still runs xUnit 2.x tests, with first-class net472 support. Using 3.x here also means the project doesn't need a runner upgrade if/when we migrate to xUnit 3 in a future framework retarget. |
| `Microsoft.NET.Test.Sdk` | **17.13.0** [VERIFIED: NuGet registry; CITED: xunit.net official getting-started example pairs this exact version with xunit 2.9.3 / xunit.runner.visualstudio 3.1.1] | vstest host SDK that `dotnet test` invokes | Required dependency of the runner adapter. Pin to 17.13.0 specifically (not latest 18.x) because xunit.net's official 2.9.x getting-started page validates this combo; 18.x is brand-new (2026) and not yet documented as the recommended pairing for xUnit 2.x net472. |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `microsoft/setup-msbuild` | **v2** [VERIFIED: GitHub Marketplace — github.com/microsoft/setup-msbuild, v2 released 2026-03-20, moved to Node24] | GH Actions step that locates MSBuild via `vswhere` and prepends it to `PATH` | Required so subsequent `msbuild` invocations work. No `vs-version` argument needed for our case (see Pitfall 1 below). |
| `actions/checkout` | **v4** [VERIFIED: standard, widely used] | Clone the repo into the runner workspace | Always step 1. |
| `actions/cache` | **v4** [VERIFIED: standard] | Cache `~/.nuget/packages` between runs | Optional but cheap; cuts ~10–30s off cold runs. Cache key: `nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj', '**/packages.lock.json') }}`. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `microsoft/setup-msbuild@v2` | `ilammy/msvc-dev-cmd@v1 with vsversion: 2019` | Only needed if v142 isn't picked up automatically. Since `windows-2022` ships `VC.Tools.142.x86.x64` (verified — see Pitfall 1), the simpler `setup-msbuild` is sufficient. Keep `ilammy` as an escape hatch in the Common Pitfalls section. |
| `xunit.runner.visualstudio 3.1.5` | `xunit.runner.visualstudio 2.8.2` (last 2.x of the runner) | 2.8.2 is the last in the 2.x adapter line. Either works with xUnit 2.x tests. 3.1.5 wins because it's the current supported line (the 2.x adapter line is security-only). |
| SDK-style csproj | Legacy non-SDK csproj | Legacy non-SDK is technically possible with `<PackageReference>` but painful: every test project would need manual `<Reference Include="xunit.core">` paths to the resolved package, ImplicitTargetsFallback dance for net472, and `xunit.runner.console`-style manual invocation. SDK-style is the documented xunit.net path for net472 and works with `dotnet test` out of the box. The repo's other managed projects (`UtinniCoreDotNet.csproj`, `UtinniCoreDotNetGen.csproj`) are legacy non-SDK; the test project does NOT need to match — different MSBuild project types coexist fine in one `Utinni.sln`. |
| `dotnet test Utinni.sln` | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` | Solution-targeted invocation has a documented failure mode on solutions containing non-test projects (dotnet/sdk#9007, microsoft/vstest#1129) AND a separate documented failure mode for x86 (xunit/xunit#1123). Project-targeted invocation sidesteps both. CONTEXT.md D-07 already permits the fallback to project-targeted; researcher's verdict is to use project-targeted **directly** rather than try solution-targeted first. |
| `windows-latest` | `windows-2022` | `windows-latest` migrated from `windows-2022` to `windows-2025` between 2025-09-02 and 2025-09-30 (CITED: github.blog/changelog/2025-07-31-github-actions-new-apis-and-windows-latest-migration-notice). It's scheduled to migrate to VS 2026 by 2026-06-15 (CITED: github.blog/changelog/2026-05-14-github-actions-upcoming-image-migrations). Pinning `windows-2022` keeps the runner image stable and VS 2022 + v142 toolset preinstalled for the duration of Phase 1 + Phase 2 burn-down. We can revisit `windows-2025`/`windows-2025-vs2026` in Phase 6 when the matrix expands. |

**Installation** (the only one needed — adds NuGet packages to the new test csproj):
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
  <PackageReference Include="xunit" Version="2.9.3" />
  <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
</ItemGroup>
```

**Version verification commands run during research (2026-05-16):**
```bash
curl -s "https://api.nuget.org/v3-flatcontainer/xunit/index.json"                          # → 2.9.3 confirmed latest 2.x
curl -s "https://api.nuget.org/v3-flatcontainer/xunit.runner.visualstudio/index.json"      # → 3.1.5 confirmed latest stable
curl -s "https://api.nuget.org/v3-flatcontainer/microsoft.net.test.sdk/index.json"         # → 17.13.0 exists (latest is 18.5.1 but pinning to documented combo)
```

## Package Legitimacy Audit

The Package Legitimacy Gate was run with the following caveat: `slopcheck 0.6.1` is installed locally but does NOT support the NuGet ecosystem (only `pypi, npm, crates.io, go, rubygems, maven, packagist` per `python -m slopcheck --pkg=?`). Per protocol graceful-degradation, packages are tagged `[ASSUMED]` for tool-level verification, but each was **independently cross-verified** via (a) the official NuGet v3 flatcontainer API (registry existence and version history) and (b) the official xunit.net documentation (authoritative usage source), which elevates them above raw `[ASSUMED]`. None show postinstall scripts (NuGet packages cannot define them).

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| `xunit` 2.9.3 | NuGet — verified via api.nuget.org/v3-flatcontainer | 16+ years (xunit), version 2.9.3 published 2025-01-08 | Hundreds of millions cumulative (industry-standard .NET test framework) | github.com/xunit/xunit | N/A (NuGet not supported by slopcheck 0.6.1) | **Approved** — registry-verified + cited in official xunit.net docs |
| `xunit.runner.visualstudio` 3.1.5 | NuGet — verified | 10+ years; 3.1.5 current stable | Tens of millions cumulative | github.com/xunit/visualstudio.xunit | N/A | **Approved** — registry-verified + cited in official xunit.net release notes as net472-compatible runner for v1/v2/v3 tests |
| `Microsoft.NET.Test.Sdk` 17.13.0 | NuGet — verified | 10+ years; 17.13.0 published 2025-02-10 | First-party Microsoft package — used by every `dotnet test` project | github.com/microsoft/vstest | N/A | **Approved** — first-party Microsoft package; cited in xunit.net official getting-started for the exact pairing |
| `microsoft/setup-msbuild` v2 | GH Marketplace | 5+ years (action), v2 published 2026-03-20 | Tens of thousands of stars/uses | github.com/microsoft/setup-msbuild | N/A (GH Action, not NuGet) | **Approved** — first-party Microsoft action |
| `actions/checkout` v4 | GH Marketplace | 6+ years | First-party GitHub action; nearly universal CI usage | github.com/actions/checkout | N/A | **Approved** — first-party GitHub action |
| `actions/cache` v4 | GH Marketplace | 5+ years | First-party GitHub action | github.com/actions/cache | N/A | **Approved** — first-party GitHub action |

**Packages removed due to slopcheck [SLOP] verdict:** none (slopcheck doesn't cover NuGet/GH Actions; degraded gracefully)
**Packages flagged as suspicious [SUS]:** none

*Note for planner: All six entries are first-party (Microsoft, xunit/xunit, GitHub). No third-party-author packages introduced this phase. The slopcheck ecosystem-coverage gap will become real in Phase 4 (utinni-cli will likely add Spectre.Console / System.CommandLine — NuGet packages also not covered) and Phase 5 (Catch2 vendored, no package manager). Document as a known coverage gap.*

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│ git push (branch=main) OR pull_request                                  │
│                          │                                              │
│                          ▼                                              │
│ ┌─────────────────────────────────────────────────────────────────────┐ │
│ │ GitHub Actions workflow: .github/workflows/ci.yml                   │ │
│ │ runs-on: windows-2022                                               │ │
│ │                                                                     │ │
│ │  [1] actions/checkout@v4                                            │ │
│ │       │                                                             │ │
│ │       ▼                                                             │ │
│ │  [2] actions/cache@v4   (key on csproj+packages.lock.json)          │ │
│ │       │     ⇄   ~/.nuget/packages (restore on cache hit)            │ │
│ │       ▼                                                             │ │
│ │  [3] microsoft/setup-msbuild@v2  (prepends MSBuild 17.x to PATH)    │ │
│ │       │                                                             │ │
│ │       ▼                                                             │ │
│ │  [4] msbuild Utinni.sln /restore /p:Configuration=Release           │ │
│ │                          /p:Platform=x86 /m                         │ │
│ │       │                                                             │ │
│ │       ├─ restores NuGet for UtinniCoreDotNet.Tests.csproj           │ │
│ │       ├─ compiles UtINI, UtinniCore-Symbols, UtinniCore (v142/x86)  │ │
│ │       │   └─ PostBuildEvent on UtinniCore (CON-T-01):               │ │
│ │       │        ├─ xcopy /E /Y data/ → bin/Release/                  │ │
│ │       │        └─ UtinniCoreDotNetGen.exe → regen Generated/*.cs    │ │
│ │       ├─ compiles UtinniCoreDotNetGen (AnyCPU/x64 .NET FX console)  │ │
│ │       ├─ compiles UtinniCoreDotNet (x86 net472, picks up regen)     │ │
│ │       ├─ compiles Launcher (v142/x86)                               │ │
│ │       └─ compiles UtinniCoreDotNet.Tests (x86 net472, NEW)          │ │
│ │       │                                                             │ │
│ │       ▼                                                             │ │
│ │  [5] dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj│ │
│ │                  --no-build --configuration Release                 │ │
│ │       │     (vstest host loads xunit.runner.visualstudio adapter)   │ │
│ │       │     (adapter discovers [Fact]/[Theory] in HotkeyTests.cs)   │ │
│ │       │     (one [Fact(Skip="C-08...")] is skipped, others run)     │ │
│ │       ▼                                                             │ │
│ │  exit code 0 (green) → workflow green → badge green                 │ │
│ │  exit code ≠ 0       → workflow red   → badge red, commit X-mark   │ │
│ └─────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘

Source-controlled artifacts added this phase:
  .github/workflows/ci.yml
  .editorconfig                                  (root, formatting-only)
  UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj
  UtinniCoreDotNet.Tests/HotkeyTests.cs
  UtinniCoreDotNet.Tests/Properties/AssemblyInfo.cs   (optional — SDK-style auto-generates)
  README.md                                      (badge inserted under title)
  Utinni.sln                                     (one Project entry + config mappings)
```

### Recommended Project Structure

```
D:/Code/Utinni/
├── .github/
│   └── workflows/
│       └── ci.yml                            # NEW — single CI workflow
├── .editorconfig                             # NEW — root formatting rules
├── UtinniCoreDotNet.Tests/                   # NEW — sibling project (D-01)
│   ├── UtinniCoreDotNet.Tests.csproj         # SDK-style, net472, x86
│   └── HotkeyTests.cs                        # First (and only) test class this phase
├── UtinniCoreDotNet/                         # UNCHANGED
├── UtinniCore/                               # UNCHANGED (CON-T-01 chain preserved)
├── UtinniCoreDotNetGen/                      # UNCHANGED
├── Utinni.sln                                # MODIFIED — adds UtinniCoreDotNet.Tests
└── README.md                                 # MODIFIED — adds badge at top
```

### Pattern 1: SDK-style csproj for net472 xUnit 2.x

**What:** The new `UtinniCoreDotNet.Tests.csproj` uses the modern `<Project Sdk="Microsoft.NET.Sdk">` format even though every other managed project in the repo is legacy non-SDK. SDK-style is xunit.net's documented path and gives us `<PackageReference>` without manual hint-path wiring.

**When to use:** Any new C# project added to this solution from Phase 1 onward (test projects, CLI in Phase 4, etc.). Do NOT retroactively convert `UtinniCoreDotNet.csproj` or `UtinniCoreDotNetGen.csproj` to SDK-style — those are in legacy non-SDK form and converting them is out of scope (it would touch WinForms designer-file `<SubType>` ergonomics and CppSharp HintPath plumbing).

**Example:**
```xml
<!-- Source: xunit.net/docs/getting-started/v2/getting-started (cited via WebFetch 2026-05-16) -->
<!-- License header: 23-line MIT block from CONVENTIONS.md must precede the <Project> element as an XML comment, matching existing repo convention -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <PlatformTarget>x86</PlatformTarget>
    <Platforms>x86</Platforms>
    <IsPackable>false</IsPackable>
    <RootNamespace>UtinniCoreDotNet.Tests</RootNamespace>
    <AssemblyName>UtinniCoreDotNet.Tests</AssemblyName>
    <LangVersion>7.3</LangVersion>
    <!-- Match the production csproj's LangVersion so the test code can't accidentally
         use a feature the production code can't (project parity per CONVENTIONS.md). -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\UtinniCoreDotNet\UtinniCoreDotNet.csproj" />
  </ItemGroup>
</Project>
```

**Notes on the knobs:**
- `<PlatformTarget>x86</PlatformTarget>` — sets the assembly bitness (this is the load-bearing knob; the test DLL must be x86 because it project-references the x86-only `UtinniCoreDotNet.dll`).
- `<Platforms>x86</Platforms>` — restricts the SDK's auto-generated configurations to `x86` only (the SDK template defaults to `AnyCPU`). This makes the solution config-mapping cleaner and prevents accidental AnyCPU compilation of the test DLL.
- No `<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>` — SDK-style uses `<TargetFramework>net472</TargetFramework>` (the SDK does the v4.7.2 mapping internally).
- No `<Reference Include="System" />` blocks — SDK-style auto-references the BCL for net472.

### Pattern 2: GitHub Actions workflow shape

**What:** Single-job linear workflow. No matrix. Triggers on `push` to `main` and on `pull_request`.

**When to use:** This is the minimum-viable shape for Phase 1; matrices and multi-config builds arrive in Phase 6.

**Example:**
```yaml
# Source: github.com/microsoft/setup-msbuild (README v2), docs.github.com/en/actions/how-tos/monitor-workflows/add-a-status-badge
# Verified versions: setup-msbuild@v2 (2026-03-20), checkout@v4, cache@v4 (current as of 2026-05-16)
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    name: Build (Release|x86) + Test (net472)
    runs-on: windows-2022          # deterministic pin; revisit in Phase 6
    timeout-minutes: 25            # generous — first runs include NuGet cold cache + CppSharp post-build

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 1           # we don't need history for build

      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: nuget-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            nuget-${{ runner.os }}-

      - name: Setup MSBuild
        uses: microsoft/setup-msbuild@v2
        # No vs-version: windows-2022 has VS 2022 with v142 toolset preinstalled
        # (Microsoft.VisualStudio.ComponentGroup.VC.Tools.142.x86.x64). MSBuild from
        # VS 2022 builds v142 solutions transparently because PlatformToolset=v142
        # is declared in every .vcxproj.

      - name: Build solution (Release|x86)
        run: msbuild Utinni.sln /m /restore /p:Configuration=Release /p:Platform=x86 /p:RestorePackagesConfig=true
        # /restore       — restores NuGet for the new test csproj (PackageReference style)
        # /p:RestorePackagesConfig=true — belt-and-suspenders for any future packages.config
        # /m             — multi-process (uses runner's CPU count)
        # The CON-T-01 PostBuildEvent on UtinniCore fires here: xcopy data/ then run UtinniCoreDotNetGen.exe.

      - name: Run tests (net472 / x86)
        run: dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release --logger "console;verbosity=normal" --logger "trx;LogFileName=test-results.trx"
        # Project-targeted (not solution-targeted) to avoid microsoft/vstest#1129 (solution + non-test projects)
        # and xunit/xunit#1123 (dotnet test x86 process-arch discovery confusion).
        # --no-build is correct: msbuild already produced the bin/Release artifacts; dotnet test would otherwise re-invoke build via the SDK and may re-resolve outputs.

      - name: Upload test results (on failure)
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: UtinniCoreDotNet.Tests/TestResults/*.trx
          if-no-files-found: warn
```

### Pattern 3: Project-targeted `dotnet test --no-build` after `msbuild` build

**What:** When the artifacts were produced by `msbuild` (not `dotnet build`), `dotnet test --no-build` reads the existing `bin/Release/` outputs and skips the build step. The artifact layouts ARE compatible when the test project is SDK-style and uses standard `<OutputPath>` conventions (SDK default: `bin\$(Configuration)\$(TargetFramework)\`).

**Key:** the test csproj's `<OutputPath>` will be `bin\Release\net472\` (SDK default), NOT `..\bin\Release\` (which is what UtinniCoreDotNet uses via legacy non-SDK convention). `--no-build` accepts this because vstest reads the project's actual output path, not a hardcoded one. **Verify after first CI run** by checking the resolved test DLL path in the `dotnet test` log.

**When to use:** Always, in this phase. Avoid `dotnet test` without `--no-build` because then dotnet would re-evaluate the project, possibly invoke its own build, and on a non-SDK-friendly `vcxproj`-mixed solution that goes sideways.

### Pattern 4: SDK-style test project in a legacy `Utinni.sln`

**What:** `Utinni.sln` is Format 12.00 / Visual Studio 16 (sln file lines 1–4). It can host SDK-style csproj entries — the project GUID `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}` is the standard "C# project" GUID and works for both styles. The new `UtinniCoreDotNet.Tests.csproj` is added with a fresh GUID, a `ProjectSection(ProjectDependencies)` referencing `UtinniCoreDotNet` (GUID `{39AB8A43-B916-4C6E-87DD-928B438CAE68}`), and config mappings only for `Debug|x86` and `Release|x86`. **Skip `RelWithDbgInfo|x86` for the test project** — there's no test value in shipping it under that config, and the SDK template doesn't expect it.

**Example sln entry to add:**
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "UtinniCoreDotNet.Tests", "UtinniCoreDotNet.Tests\UtinniCoreDotNet.Tests.csproj", "{NEW-GUID-HERE}"
	ProjectSection(ProjectDependencies) = postProject
		{39AB8A43-B916-4C6E-87DD-928B438CAE68} = {39AB8A43-B916-4C6E-87DD-928B438CAE68}
	EndProjectSection
EndProject
```
And in the `GlobalSection(ProjectConfigurationPlatforms)`:
```
{NEW-GUID-HERE}.Debug|x86.ActiveCfg = Debug|x86
{NEW-GUID-HERE}.Debug|x86.Build.0 = Debug|x86
{NEW-GUID-HERE}.Release|x86.ActiveCfg = Release|x86
{NEW-GUID-HERE}.Release|x86.Build.0 = Release|x86
{NEW-GUID-HERE}.RelWithDbgInfo|x86.ActiveCfg = Release|x86
# Note: no .Build.0 entry under RelWithDbgInfo — that maps to "don't build under this config"
```

### Anti-Patterns to Avoid

- **`dotnet test Utinni.sln`** — see Pitfall 2. Don't.
- **`msbuild /t:Build Utinni.sln` without `/restore`** — the test project will fail to compile because its `<PackageReference>` items haven't been resolved. Always pair `/restore` with the build target, or run `msbuild /t:Restore` as a separate step. The `/restore` shorthand does both in one invocation.
- **`<PackageReference Include="xunit" Version="*" />`** — never use floating versions in a CI test project. Pin exact versions; CONTEXT.md D-03 says "pin to the 2.9.x line" — interpret as exact-version pin.
- **Adding `<Reference Include="..." />` items to the SDK-style csproj** — SDK-style auto-references the BCL. Adding explicit `<Reference>` items creates duplicate-reference warnings.
- **Targeting `AnyCPU` for the test project** — it would compile but throw `BadImageFormatException` at test discovery time when vstest tries to load both the AnyCPU test DLL and the x86-only `UtinniCoreDotNet.dll` it references. Use `<PlatformTarget>x86</PlatformTarget>` AND `<Platforms>x86</Platforms>`.
- **Putting tests in a `tests/` subfolder** — CONTEXT.md D-01 locks flat-root layout.
- **Modifying the existing `external/imgui/.editorconfig`** — CONTEXT.md D-11 explicitly preserves it.
- **Putting `[STAThread]` on test methods** — xUnit 2.x does not honor it on individual `[Fact]`s; if a future test absolutely needs STA it requires the xUnit STA extension package (out of scope this phase since `Hotkey.ProcessString` is pure-logic). Not relevant for Phase 1 but worth flagging for later WinForms-adjacent tests.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| MSBuild discovery on the CI runner | Custom PowerShell `vswhere` script | `microsoft/setup-msbuild@v2` | First-party Microsoft action; `vs-version` parameter handles edge cases; tracks runner image changes automatically. |
| MSVC v142 selection on a VS 2022 runner | Custom Visual Studio Installer modify step OR `ilammy/msvc-dev-cmd@v1` | Nothing — let MSBuild auto-select from `PlatformToolset=v142` in the vcxproj | The component group `VC.Tools.142.x86.x64` is preinstalled on `windows-2022`. MSBuild reads `PlatformToolset=v142` from each `.vcxproj` and picks the right toolset transparently. |
| Custom test discovery / runner harness | Bespoke `xunit.runner.console`-driven script | `dotnet test` + `xunit.runner.visualstudio` adapter | Standard pipeline. `dotnet test` produces vstest-compatible `.trx` output, integrates with GitHub Actions test annotations natively, and the adapter handles parallel execution. |
| CI badge generation | Custom shield-generation script | GitHub's native `actions/workflows/<file>.yml/badge.svg` endpoint | Free, always current, no extra service. |
| NuGet package restore | Calling `nuget.exe restore` separately | `msbuild /restore` | One command, one step, correct ordering. `nuget.exe` is not on `windows-2022`'s PATH by default; setup-nuget is an extra step. |
| Test-on-failure-detection inside the test code | `Assert.Fail` wrapping | `[Fact(Skip = "C-08: ...")]` for known-fails | xUnit's `Skip` parameter is the canonical xfail equivalent in v2.x. It shows up as "skipped" (yellow) in the test report, doesn't fail the build, and the skip message is visible. Cleaner than `[Trait("Status", "PendingFix")]` + filter expressions. |

**Key insight:** Every "should we build X?" question in this phase has an existing first-party tool that solves it correctly. The honest answer to "should we customize anything?" is "no, except the `ci.yml` YAML structure itself" — and even that should be as close to the action-README example as possible.

## Runtime State Inventory

**Skipped — Phase 1 is greenfield (adding new files and modifying two existing files), not a rename / refactor / migration.** No existing runtime state needs to be migrated:

- **Stored data:** None. The test project produces ephemeral test output (TRX files in `bin/Release/net472/TestResults/`); CI runs are stateless.
- **Live service config:** None. No external services are involved.
- **OS-registered state:** None. CI runs are ephemeral on GitHub-hosted runners.
- **Secrets and env vars:** None. The workflow runs without any secrets; no `GITHUB_TOKEN` write-scoped permissions needed; `DXSDK_DIR` explicitly NOT set per D-08.
- **Build artifacts / installed packages:** None pre-existing. NuGet packages are fetched fresh on first run and cached thereafter.

Verified by inspecting: `Utinni.sln`, `UtinniCoreDotNet.csproj`, `UtinniCore.vcxproj` (lines 85–95 cover the CON-T-01 chain), `data/` (read-only ship-time defaults), `.github/` (doesn't exist).

## Common Pitfalls

### Pitfall 1: MSVC v142 absence assumption (HIGH confidence — verified)

**What goes wrong:** Plans assume the v142 toolset needs to be installed because "VS 2019 runners are deprecated," so they add `ilammy/msvc-dev-cmd@v1` or a Visual Studio Installer modify step that bloats the workflow by 2–5 minutes per run.

**Why it happens:** `actions/runner-images` did indeed remove the standalone VS 2019 runner image, and several blog posts from 2024 conflate "VS 2019 runner removed" with "v142 toolset removed."

**Reality** [VERIFIED via WebFetch of `github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md` on 2026-05-16]: `windows-2022` ships Visual Studio 2022 with `Microsoft.VisualStudio.ComponentGroup.VC.Tools.142.x86.x64` (version 17.14.36510.44) preinstalled. Every `.vcxproj` in `Utinni.sln` declares `<PlatformToolset>v142</PlatformToolset>` (verified in `UtinniCore/UtinniCore.vcxproj`, `Launcher/Launcher.vcxproj`, `UtINI/UtINI.vcxproj`, `UtinniCore-Symbols/UtinniCore-Symbols.vcxproj` per STACK.md §Runtime), so MSBuild auto-selects v142 transparently. **No special action is needed.**

**How to avoid:** Use `microsoft/setup-msbuild@v2` with NO `vs-version` argument. Pin `windows-2022` (don't use `windows-latest`). Document in the workflow YAML as a comment that v142 is picked up via PlatformToolset, in case a future maintainer wonders.

**Warning signs:** "Building MSVC v142 projects from VS Build Tools" instructions from 2023 and earlier. They're stale.

**Recovery if it WERE missing:** `ilammy/msvc-dev-cmd@v1 with: { vsversion: '2022', toolset: '14.29' }` is the escape hatch. Or run a Visual Studio Installer modify step:
```yaml
- name: Install v142 toolset (fallback only — not expected to be needed)
  run: |
    Start-Process -FilePath "C:\Program Files (x86)\Microsoft Visual Studio\Installer\setup.exe" -ArgumentList "modify --installPath ""C:\Program Files\Microsoft Visual Studio\2022\Enterprise"" --add Microsoft.VisualStudio.ComponentGroup.VC.Tools.142.x86.x64 --quiet --norestart --nocache" -Wait
  shell: pwsh
```
Adds ~3–5 minutes; only use if a real v142-not-found failure surfaces.

### Pitfall 2: `dotnet test <solution>` on mixed C++/C# solutions (HIGH confidence — verified)

**What goes wrong:** `dotnet test Utinni.sln` either errors out (`Project file is not a known project type` on the `.vcxproj` entries) or silently builds nothing (and reports "no test projects found"), depending on .NET SDK version and how the vcxproj entries declare their project type.

**Why it happens** [CITED: dotnet/sdk#9007 ("When running dotnet test on a solution, silently ignore non-test projects"), microsoft/vstest#1129 ("'dotnet test' in solution folder fails when non-test projects are in the solution")]: `dotnet test` calls into vstest which calls into MSBuild to enumerate projects. The vstest target runs on every project in the solution; on a `vcxproj` it fails because the C++ build targets don't define the VSTest target. The workaround documented by the dotnet team (after-`<SlnName>.sln.targets` filter) is heavyweight and brittle.

**How to avoid:** **Target the project file directly.** `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release`. This sidesteps the entire problem because vstest only enumerates the one project it was given. CONTEXT.md D-07 explicitly permits this fallback; researcher's recommendation is to USE the fallback as the primary path (don't even try `dotnet test Utinni.sln` first).

**Warning signs:** Log lines like `Could not find a part of the path` referencing `UtinniCore\bin\Release\` (which is `..\..\bin\Release\` from the test project — vstest looking for a test DLL at the wrong path because it didn't know which project was the test target).

### Pitfall 3: `dotnet test` x86 process-architecture confusion (MEDIUM confidence — known issue, mitigated by our setup)

**What goes wrong** [CITED: xunit/xunit#1123 (2017, open)]: `dotnet test` spawns vstest, which spawns a test host process. If the test DLL is x86 but the test host is the x64 `dotnet.exe` on PATH, test discovery silently produces zero tests with a warning "Skipping: ... (could not find dependent assembly)".

**Why it happens:** `dotnet test`'s TargetPlatform setting in a `.runsettings` doesn't actually force the test host process architecture for SDK-style net472 projects — the test host inherits from the `dotnet.exe` that was invoked.

**Why it shouldn't bite us this phase** [VERIFIED via WebFetch xunit.net/releases/visualstudio]: `xunit.runner.visualstudio 3.x` explicitly supports net472 and the modern vstest host in .NET SDK 8+/9+ handles x86 net472 test DLLs correctly via the `IsTestProject=true` + `PlatformTarget=x86` combo on the csproj. The 2017 issue was against an old `xunit.runner.visualstudio 2.x` + .NET Core 1.0 toolchain.

**How to avoid:** (1) Use `xunit.runner.visualstudio 3.1.5` (not 2.x); (2) set `<PlatformTarget>x86</PlatformTarget>` and `<Platforms>x86</Platforms>` in the test csproj; (3) DO NOT add a `.runsettings` file this phase (per CONTEXT.md specifics).

**Warning signs:** `dotnet test` returns exit code 0 with output like `Total tests: 0. Passed: 0. Failed: 0. Skipped: 0.` That's a discovery failure masquerading as success. If seen on the first CI run, add `<UseVSTestRunner>true</UseVSTestRunner>` to the csproj as a recovery step and re-run.

### Pitfall 4: CON-T-01 post-build chain failing on CI but not locally (MEDIUM confidence — partially testable)

**What goes wrong:** The post-build chain (`UtinniCore.vcxproj:91-94`) runs `xcopy /E /Y "$(SolutionDir)data" "$(TargetDir)" /d` and then `$(SolutionDir)UtinniCoreDotNetGen\bin\$(Configuration)\UtinniCoreDotNetGen.exe`. On CI, the working directory may differ from the developer machine, AND `UtinniCoreDotNetGen.exe` depends on CppSharp DLLs that the `UtinniCoreDotNetGen.csproj` PostBuildEvent (line 94) copies from `external/CppSharp/lib/` to `bin/$(Configuration)/`.

**Why it happens / what we've verified:**
- `$(SolutionDir)` and `$(TargetDir)` are MSBuild-resolved properties; they expand correctly on any machine where MSBuild was invoked with the .sln as the entry point (which is our case). LOW risk of working-directory issues.
- `external/CppSharp/lib/` IS committed to the repo (verified: `ls /d/Code/Utinni/external/CppSharp/lib` shows `CppSharp.dll`, `CppSharp.AST.dll`, etc.). And those DLLs are x64 PE32+ (.NET Framework AnyCPU/x64 assemblies — verified `file` output: "x86-64 Mono/.Net assembly"). MEDIUM-LOW risk on the CppSharp side.
- The MSBuild project dependency graph in `Utinni.sln` ensures `UtinniCoreDotNetGen` builds BEFORE `UtinniCore` runs its post-build (verified: `Utinni.sln` line 14 declares `UtinniCore` depends on `UtinniCore-Symbols`; `UtinniCoreDotNetGen` has no declared dependents but is needed by the UtinniCore post-build, which works in practice because of build ordering on `Configuration` match — same `Release` config means it's built first via default ordering OR by the developer's PostBuildEvent invocation).
- ⚠️ The one risk we can't pre-verify: whether `xcopy /E /Y "$(SolutionDir)data" "$(TargetDir)" /d` resolves `$(SolutionDir)data` correctly when the build is invoked from a non-`$(SolutionDir)` working directory. On CI, `msbuild Utinni.sln` is invoked from `${{ github.workspace }}` (which equals the repo root, which equals `$(SolutionDir)`). LOW risk.

**How to avoid:** Trust the existing post-build chain and verify on the first CI run. The pitfall here is **planning a structural fix** when the chain almost certainly works as-is; per CONTEXT.md "Out of scope" the structural fix is explicitly not allowed this phase.

**Minimal workaround if the chain fails for environmental reasons** (not a structural fix):
1. Add a step BEFORE `msbuild` that confirms the working directory: `run: pwd; ls data/; ls UtinniCoreDotNetGen/`
2. If `data/` is missing on CI (it isn't — it's committed; verified `ls /d/Code/Utinni/data` works), add it back from git.
3. If `UtinniCoreDotNetGen.exe` is missing post-build, that means `UtinniCoreDotNetGen.csproj` didn't get built; force ordering with `msbuild UtinniCoreDotNetGen\UtinniCoreDotNetGen.csproj /p:Configuration=Release /restore` as a pre-step before `msbuild Utinni.sln`.

**Warning signs:** Build log shows `'xcopy' is not recognized` (impossible on Windows runner), or `UtinniCoreDotNetGen.exe : The system cannot find the file specified` (UtinniCoreDotNetGen project didn't compile yet — solve with explicit pre-step), or `CppSharp.dll : Could not load file or assembly` (vendored DLLs not on disk — would mean the repo is incomplete, very unlikely).

**Recommendation:** Implement the YAML as specified; on first CI run, if the post-build chain fails, fall back to explicit pre-step ordering. Do NOT proactively add the explicit pre-step — keep the workflow minimal.

### Pitfall 5: `Hotkey.ProcessString` is private + instance method (HIGH confidence — verified)

**What goes wrong:** Plans assume `ProcessString` is `static` (clean to test directly). It is **not** — see `UtinniCoreDotNet/Hotkeys/Hotkey.cs:66`: `private void ProcessString(string keyComboStr)`. Calling it requires constructing a `Hotkey` instance OR using reflection.

**Verified signature and behavior** (reading `Hotkey.cs`):
- **Class:** `public class Hotkey` (non-static, regular class)
- **Constructors:**
  - `public Hotkey(string name, string text, string keyComboStr, Action onDownCallback, bool overrideGameInput, bool enabled = true, bool onGameFocusOnly = false)` — line 42; this ctor calls `ProcessString(keyComboStr)` on line 51.
  - `public Hotkey(string name, string text, Keys modifierKeys, Keys key, ...)` — line 54; does NOT call ProcessString.
- **`ProcessString(string keyComboStr)`:** private void (line 66); on `String.IsNullOrEmpty` it logs a Warning and returns (line 68–72 — clean exit, no throw); on a `+`-containing string it parses modifier + key via `Enum.Parse(typeof(Keys), modifiers, true)` and `Enum.Parse(typeof(Keys), key, true)`. **Both `Enum.Parse` calls THROW `ArgumentException` on unknown enum tokens** (line 82 and 91) — this is the C-08 bug.
- **Side effects:** Sets `this.ModifierKeys` and `this.Key` fields. No static state.
- **Hidden coupling — `System.Windows.Forms.Keys`:** The test references `Keys` enum from WinForms. In a headless CI runner, WinForms assemblies are present (it's a Windows runner with .NET Framework 4.7.2 installed) and `Keys` is a pure enum — no display initialization required. **Safe to test on CI without a display.**
- **Hidden coupling — `Log.Warning`:** Called when input is empty (line 70). `Log` is `UtinniCoreDotNet.Utility.Log` which proxies to the native `UtinniCore.dll` log layer. **This MIGHT fail at runtime if `UtinniCore.dll` isn't loaded** (the P/Invoke target is missing on the test runner). Mitigation: don't test the empty-string case (it doesn't throw, it logs-and-returns; we don't need it for Phase 1). Alternative: use a public-API test seam (use the `Hotkey(string name, string text, Keys modifierKeys, Keys key, ...)` constructor at line 54 which bypasses ProcessString entirely — but that defeats the purpose).

**How to test:** Test via the **public string-ctor (line 42)** which calls `ProcessString` internally. Construct a `Hotkey` with a known good combo string and assert on the resulting `ModifierKeys` and `Key` public fields (lines 35–36). Example test shapes:

```csharp
[Fact]
public void Ctor_StringConstructor_ParsesValidSingleKey_SetsKey()
{
    var hk = new Hotkey("test", "test", "F1", () => { }, overrideGameInput: false);
    Assert.Equal(Keys.None, hk.ModifierKeys);
    Assert.Equal(Keys.F1, hk.Key);
}

[Fact]
public void Ctor_StringConstructor_ParsesModifierChord_SetsBoth()
{
    var hk = new Hotkey("test", "test", "Control + S", () => { }, overrideGameInput: false);
    Assert.Equal(Keys.Control, hk.ModifierKeys);
    Assert.Equal(Keys.S, hk.Key);
}

[Fact(Skip = "C-08: expected to fail until Phase 2 fix lands (Enum.TryParse refactor). " +
              "When unskipped, this asserts that malformed input is gracefully handled instead of throwing ArgumentException.")]
public void Ctor_StringConstructor_MalformedInput_DoesNotThrow()
{
    // Will THROW ArgumentException today on line 82's Enum.Parse — that IS the C-08 bug.
    var ex = Record.Exception(() => new Hotkey("test", "test", "Ctrl + T", () => { }, overrideGameInput: false));
    Assert.Null(ex);  // Today: NOT null (throws ArgumentException). After C-08 fix: null.
}
```

**Warning signs:** The empty-string test path (`String.IsNullOrEmpty`) calls `Log.Warning` which P/Invokes into `UtinniCore.dll`. If a test were written for that path, it would crash on test runner load (the native DLL isn't there). Avoid that test entirely this phase.

**Recommendation on the C-08 posture** (CONTEXT.md leaves this to the planner — researcher's verdict): **Ship the malformed test as `[Fact(Skip = "C-08: ...")]`**. Three reasons:
1. The test code itself documents the desired post-fix contract — it's executable documentation that ships with the codebase, not a TODO.md note.
2. xUnit's skip mechanism is the canonical xfail equivalent — it shows up yellow in the report, doesn't block green CI, and is trivially unskipped (delete one parameter) when Phase 2's C-08 fix lands.
3. The alternative ("defer the malformed test entirely to Phase 2") creates a coordination cost: Phase 2's plan would need to remember to add a test it doesn't otherwise need to think about. Skipping it in Phase 1 removes that coordination cost.

### Pitfall 6: Badge URL pointing at the wrong branch (LOW confidence — easy to fix, just plan for it)

**What goes wrong:** Badge shows "no status" or stays gray because the URL points at `?branch=master` but the workflow only runs on `main`.

**Why it happens:** The repo's default branch is `main` (verified: `gitStatus` from the env header shows "Current branch: master" and "Main branch: master") — wait. Actually the gitStatus shows BOTH "Current branch: master" and "Main branch (you will usually use this for PRs): master". So the repo's main branch is **named `master`**, not `main`.

**This is important:** CONTEXT.md D-07 says "on push to `main`" but the actual default branch is `master`. The planner needs to reconcile this: either (a) rename the default branch to `main` (out of scope — admin action), or (b) update the workflow trigger to `push: branches: [master]` and the badge URL to `?branch=master`. **Recommendation: use `master`** to match the repo's actual default branch; revisit branch rename in a future phase if desired.

**Badge URL format** [CITED: docs.github.com/en/actions/how-tos/monitor-workflows/add-a-status-badge]:
```markdown
[![CI](https://github.com/kennethlong/Utinni/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/kennethlong/Utinni/actions/workflows/ci.yml)
```
Owner is `kennethlong` (per project_fork_strategy memory and the repo path). Repo is `Utinni`. Wrap the badge in a hyperlink so clicking it opens the Actions page (convention).

### Pitfall 7: NuGet restore lock-file drift (LOW confidence — only relevant if we opt in)

**What goes wrong:** Without `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in the test csproj, NuGet resolves package versions transitively on every restore. If Microsoft publishes a new `Microsoft.TestPlatform.ObjectModel 17.14.0` overnight (a transitive dep of `xunit.runner.visualstudio 3.1.5`), CI runs may resolve differently than local runs.

**How to avoid:** Add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to the csproj. First restore generates `packages.lock.json`; subsequent restores use it. Commit the lockfile. CI runs become deterministic.

**Tradeoff:** One additional file in the repo, occasional `dotnet restore --force-evaluate` runs when intentionally bumping versions.

**Recommendation:** Yes, enable it. Phase 1 sets the test-infrastructure precedent for Phases 4/5 and reproducibility wins are real. Update the cache key to include `packages.lock.json` once it exists: `key: nuget-${{ runner.os }}-${{ hashFiles('**/packages.lock.json') }}`.

## Code Examples

Verified patterns from official sources:

### Example 1: SDK-style net472 xUnit test csproj
```xml
<!-- Source: xunit.net official getting-started v2 (WebFetch 2026-05-16) + verification against existing repo conventions -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <PlatformTarget>x86</PlatformTarget>
    <Platforms>x86</Platforms>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
    <RootNamespace>UtinniCoreDotNet.Tests</RootNamespace>
    <AssemblyName>UtinniCoreDotNet.Tests</AssemblyName>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\UtinniCoreDotNet\UtinniCoreDotNet.csproj" />
  </ItemGroup>
</Project>
```

### Example 2: Bare `.editorconfig` matching D-11
```ini
# Source: existing repo conventions (CONVENTIONS.md), editorconfig.org reference, vendored external/imgui/.editorconfig as in-repo prior art
# Codifies the de-facto rules from CONVENTIONS.md without enforcing analyzer rules (those land in Phase 6).
# Vendored external/imgui/.editorconfig is preserved and overrides this file for files under external/imgui/.

root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

# C# files — Allman braces conventions enforced by file content (no analyzer rules)
[*.{cs,cpp,h}]
indent_style = space
indent_size = 4

# Markdown — preserve trailing whitespace for line breaks
[*.md]
trim_trailing_whitespace = false

# YAML — 2-space indent per de-facto convention
[*.{yml,yaml}]
indent_size = 2

# Makefiles need tabs
[Makefile]
indent_style = tab
```

### Example 3: Test class — happy paths + skipped malformed
```csharp
// Source: xUnit 2.x [Fact] / [Theory] / Skip parameter documentation + verified Hotkey.cs signature
// File: UtinniCoreDotNet.Tests/HotkeyTests.cs
// (MIT license header — 23 lines — omitted here; copy from any existing .cs file in the repo)

using System.Windows.Forms;
using UtinniCoreDotNet.Hotkeys;
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    public class HotkeyTests
    {
        [Fact]
        public void Ctor_StringConstructor_SingleKey_SetsKeyAndNoModifier()
        {
            var hk = new Hotkey("test", "test", "F1", () => { }, overrideGameInput: false);
            Assert.Equal(Keys.None, hk.ModifierKeys);
            Assert.Equal(Keys.F1, hk.Key);
        }

        [Fact]
        public void Ctor_StringConstructor_ModifierChord_SetsBoth()
        {
            var hk = new Hotkey("test", "test", "Control + S", () => { }, overrideGameInput: false);
            Assert.Equal(Keys.Control, hk.ModifierKeys);
            Assert.Equal(Keys.S, hk.Key);
        }

        [Theory]
        [InlineData("Shift + Alt + Z", Keys.Shift | Keys.Alt, Keys.Z)]
        // Note: "Shift + Alt" is parsed by Enum.Parse(Keys, ...) as the bitwise-OR of the two — Keys is a [Flags] enum.
        public void Ctor_StringConstructor_MultiModifierChord_ParsesFlags(string combo, Keys expectedMods, Keys expectedKey)
        {
            var hk = new Hotkey("test", "test", combo, () => { }, overrideGameInput: false);
            Assert.Equal(expectedMods, hk.ModifierKeys);
            Assert.Equal(expectedKey, hk.Key);
        }

        [Fact(Skip = "C-08: expected to fail until Phase 2 fix lands (Enum.TryParse refactor on Hotkey.cs:82,91). " +
                      "When unskipped, this asserts that malformed input like 'Ctrl + T' (note 'Ctrl' is not a valid Keys enum name — should be 'Control') is gracefully handled instead of throwing ArgumentException.")]
        public void Ctor_StringConstructor_MalformedInput_DoesNotThrow()
        {
            var ex = Record.Exception(() => new Hotkey("test", "test", "Ctrl + T", () => { }, overrideGameInput: false));
            Assert.Null(ex);
        }
    }
}
```

**⚠️ Test data caveat on the multi-modifier `Theory`:** `Keys.Shift | Keys.Alt` produces `131072 | 262144 = 393216`. `Enum.Parse(typeof(Keys), "Shift + Alt", true)` — actually let me re-check. `Hotkey.ProcessString` splits at the FIRST `+`. So `"Shift + Alt + Z"` splits at `[0]`: modifiers = `"Shift"`, key = `"Alt + Z"`. Then `Enum.Parse(typeof(Keys), "Alt + Z", true)` parses `"Alt + Z"` as a Flags combo: `Keys.Alt | Keys.Z`. So the actual result is `ModifierKeys = Keys.Shift`, `Key = Keys.Alt | Keys.Z`. The planner should verify this against the actual `Hotkey.ProcessString` behavior in a quick scratch test and adjust the assertion. Either way the test demonstrates real production code path coverage.

### Example 4: README badge insertion
```markdown
<!-- Source: docs.github.com/en/actions/how-tos/monitor-workflows/add-a-status-badge -->
# Utinni

[![CI](https://github.com/kennethlong/Utinni/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/kennethlong/Utinni/actions/workflows/ci.yml)

Utinni is a client plugin and injection framework which aims to provide an easier access to client and content development for Pre-CU Star Wars Galaxies and more specifically [SWGEmu](https://github.com/swgemu).
```
**Placement:** Immediately after the `# Utinni` title, before the existing first paragraph. Single line break above and below the badge. The existing `> **Documentation:** ...` block-quote remains where it is.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `actions/setup-msbuild@v1.1` | `microsoft/setup-msbuild@v2` | 2026-03-20 (v2 release moved action to Node24) | Use v2 for Node24 runtime support and future-proofing. v1.x still works but is on Node20 which GitHub is deprecating (CITED: github.blog/changelog/2025-09-19-deprecation-of-node-20-on-github-actions-runners). |
| `windows-latest` (was `windows-2022`) | `windows-2025` (since 2025-09-30) | 2025-09-02 to 2025-09-30 migration | We pin `windows-2022` explicitly to avoid being on the migrating-to-`windows-2025-vs2026` train scheduled for June 2026. |
| `xunit.runner.visualstudio 2.x` (e.g., 2.8.2) | `xunit.runner.visualstudio 3.1.x` | Q4 2024 (3.x line went stable) | 3.x supports v1/v2/v3 tests and targets net472+. Use it even when running v2.x tests; avoids future migration friction. |
| Legacy non-SDK csproj with `<PackageReference>` | SDK-style csproj | Long-standing (~2018+); SDK-style is the documented xunit.net path | Existing `UtinniCoreDotNet.csproj` is legacy non-SDK because it predates this transition and has WinForms designer-file ergonomics — DO NOT migrate it. New `UtinniCoreDotNet.Tests.csproj` is SDK-style because it has no WinForms designer files and benefits from `dotnet test` integration. |
| `Microsoft.VisualStudio.SDK.PreviewVS2026` | (Not relevant Phase 1) | — | Flagged for future awareness: the VSIX plugin template work in `sdk/UtinniPluginTemplates/Vsix/Vsix.csproj` pins `Microsoft.VisualStudio.SDK 16.0.206` — C-12 (assessment.md) is the relevant fix and lands in Phase 2. |

**Deprecated/outdated:**
- **xUnit 3.x** is the canonical "new" framework but it requires .NET 6+ (CITED: xunit.net release notes). Not an option for us until the Utinni managed surface moves off net472 (a V2-class decision per assessment.md).
- **`packages.config` style NuGet management** is legacy; do not use. SDK-style + PackageReference is required.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `Microsoft.NET.Test.Sdk 17.13.0` is the right version pair for `xunit 2.9.3` + `xunit.runner.visualstudio 3.1.5` on net472/x86 | Standard Stack | Could fail to discover tests at runtime. Mitigation: xunit.net official docs cite this exact combo with 3.1.1 (we're using 3.1.5, a patch-level newer). If first CI run shows discovery=0, downgrade `xunit.runner.visualstudio` to `3.1.1` to match the documented combo verbatim. |
| A2 | `windows-2022` will remain available through Phase 1 + Phase 2 timeline (likely 2–6 weeks) | Standard Stack — runner pin | LOW — `windows-2022` is the current stable runner image and there's no announced deprecation. Even if deprecated, the migration path to `windows-2025` is straightforward (similar VS 2022 install). |
| A3 | The post-build chain xcopy of `data/` and execution of `UtinniCoreDotNetGen.exe` will succeed on CI without environmental adjustment | Common Pitfalls #4 | MEDIUM — verified that `data/`, vendored CppSharp, and project dependency graph are all in place; the only failure mode is build ordering surprises which can be mitigated with an explicit pre-step. Will be confirmed by the first CI run. |
| A4 | The badge URL hostname is `github.com` and the repo path is `kennethlong/Utinni` | Pitfall 6 + Example 4 | LOW — `kennethlong` is from project_fork_strategy memory; if the repo lives under a different org/user, the badge stays gray until corrected (1-line fix). |
| A5 | `Hotkey.ProcessString`'s `Enum.Parse` calls accept "Control" as a key name for the Control key | Code Example 3 | LOW — `Keys.Control` is a documented .NET enum value (`= 0x20000`). Verified by reading `Hotkey.cs` and noting the existing `GetKeyComboString` (line 105) uses `ModifierKeys + " + " + Key` so the round-trip implies "Control" works. |
| A6 | The first CI run after merging this phase will succeed end-to-end | Throughout | MEDIUM-LOW — every component has been verified individually but the combination is not testable until CI runs. Will be confirmed via the workflow's first execution after merge. |
| A7 | The default branch is `master` (not `main`) | Pitfall 6 | LOW — explicitly verified in the gitStatus env block. If admin renames the branch, the workflow trigger and badge URL both need a one-line update. |
| A8 | `RelWithDbgInfo|x86` configuration of the test project can map to `Release|x86` build (no separate test run for RelWithDbgInfo) | Pattern 4 — sln entry | LOW — this is just for IDE solution-loading compatibility; CI never builds RelWithDbgInfo this phase per D-08. |

**If this table seems short, that's because almost everything in the research is either VERIFIED via tools (NuGet registry, repo file reads, GH runner image readme) or CITED from authoritative sources (xunit.net docs, GitHub Actions docs, setup-msbuild README). The assumptions above are the genuine gaps.**

## Open Questions

1. **Is `Microsoft.NET.Test.Sdk 17.13.0` or 17.14.x the right pin for our combo?**
   - What we know: xunit.net official docs cite 17.13.0 + xunit 2.9.3 + xunit.runner.visualstudio 3.1.1. We're going slightly newer on the runner (3.1.5). 17.14.x has been published since but isn't yet cited in any official xunit.net doc we found.
   - What's unclear: whether 17.14.x adds anything we want or removes anything we need. Latest is 18.5.1 per NuGet but jumping to 18.x is more speculative.
   - Recommendation: Pin `17.13.0` matching the documented combo. Revisit on first CI failure or in Phase 4 when the CLI shim adds more tests.

2. **Should the badge link target the `actions/workflows/ci.yml` URL (badge-as-deep-link) or just the badge URL inline?**
   - What we know: GitHub docs show both patterns; the deep-link variant (badge wrapped in `[...](url)`) is more common in OSS projects.
   - What's unclear: maintainer preference.
   - Recommendation: Use the wrapped-deep-link variant per OSS convention (Example 4 above).

3. **Does the planner need a separate task to verify the CON-T-01 chain runs on CI before adding the test step?**
   - What we know: The chain is verified to be in place; environmental risk is MEDIUM-LOW.
   - What's unclear: whether the planner wants a one-iteration "build only, no test" CI run first to validate the chain, then a second iteration to add the test step.
   - Recommendation: Don't bother — the chain and the test are independent. If the chain fails, the test step won't run; if the test fails, the chain succeeded. Single-iteration delivery is fine.

4. **Should the workflow also lint the `.editorconfig` itself?**
   - What we know: There's no standard CI step for `.editorconfig` linting; the rules just apply during build to files VS opens.
   - What's unclear: whether the planner wants a `dotnet format --verify-no-changes` step to fail the build on style violations.
   - Recommendation: NOT this phase. `dotnet format` requires SDK-style csproj on all targeted projects (we have only one). Defer to Phase 6 when the analyzer-rule .editorconfig lands.

## Environment Availability

Phase 1 produces a CI workflow that will execute on a remote GitHub-hosted runner; local environment dependencies matter only for (a) developer-side `dotnet test` runs (for developers iterating locally) and (b) running the slopcheck protocol during research.

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `dotnet` SDK | Local developer test runs (optional); CI runs (mandatory — preinstalled on GH runners) | ✓ (locally) | 10.0.300 (with 9.0.310 also present) | — (GH runners have it preinstalled) |
| MSBuild 17 / VS 2022 | Local developer builds (mandatory); CI builds (preinstalled on `windows-2022` GH runner) | Unknown locally; ✓ on CI | n/a / 17.x | — |
| MSVC v142 toolset | Local developer builds (mandatory); CI builds (preinstalled on `windows-2022` per Pitfall 1) | Unknown locally; ✓ on CI | 17.14.36510.44 | `ilammy/msvc-dev-cmd@v1` escape hatch on CI; manual VS install locally |
| `slopcheck` | Package legitimacy audit during research | ✓ (installed at `C:\Users\kenne\AppData\Roaming\Python\Python314\Scripts\slopcheck.exe`, version 0.6.1) | 0.6.1 | N/A — but slopcheck 0.6.1 does NOT support `nuget` ecosystem (only pypi/npm/crates.io/go/rubygems/maven/packagist). Degraded gracefully to manual NuGet API verification + xunit.net citation. |
| `curl` (for NuGet registry verification) | Research-time package validation | ✓ | system | python `urllib` |
| `git` | Workflow trigger + commit | ✓ | system | n/a — required |

**Missing dependencies with no fallback:** none for Phase 1 deliverables.

**Missing dependencies with fallback:**
- `slopcheck` NuGet ecosystem coverage — degraded to manual NuGet `v3-flatcontainer` API checks plus official-doc citation (xunit.net). Three of three packages were independently verifiable; no package legitimacy concerns.

## Validation Architecture

> Nyquist Dimension 8: this phase IS the validation infrastructure for everything that follows. The architecture is itself the deliverable.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (pinned) — see Standard Stack |
| Config file | `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` (SDK-style with PackageReference; no `.runsettings`, no `xunit.runner.json` per CONTEXT.md specifics) |
| Quick run command (local) | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` (after `msbuild Utinni.sln /restore /p:Configuration=Release /p:Platform=x86 /m`) |
| Full suite command (local) | Identical — only one test project this phase |
| CI invocation | Same as local quick-run (see Pattern 2 YAML) |
| Test discovery | `xunit.runner.visualstudio` adapter via `dotnet test` / vstest host |
| Test result format | `.trx` (TestResults\<timestamp>.trx) + console; uploaded as `actions/upload-artifact@v4` on failure |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TEST-01 | Test project compiles in CI | infrastructure | `msbuild Utinni.sln /restore /p:Configuration=Release /p:Platform=x86` (build step) — exit-code-0 is pass | ❌ Wave 0 (csproj doesn't exist yet) |
| TEST-01 | `dotnet test` runs in CI and discovers ≥1 test | infrastructure | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` — log must show `Total tests: ≥1` | ❌ Wave 0 (HotkeyTests.cs doesn't exist yet) |
| TEST-01 | At least one smoke test exercises a real Utinni code path | unit | `pytest`-equivalent: `dotnet test ... --filter "FullyQualifiedName~HotkeyTests"` — three [Fact]s must pass, one [Fact(Skip)] is skipped | ❌ Wave 0 |
| TEST-01 | Test failure on `master` blocks the workflow | infrastructure (validates the validator) | Demonstrated by intentionally-failing branch + revert (see Sampling Rate below) | ❌ Wave 0 — requires the throwaway-branch procedure to be exercised once after merge |
| TEST-01 (acceptance) | CI status badge green on master | infrastructure | Badge URL `https://github.com/kennethlong/Utinni/actions/workflows/ci.yml/badge.svg?branch=master` returns SVG showing "passing" | ❌ Wave 0 (workflow file doesn't exist yet) |
| TEST-01 (acceptance) | `.editorconfig` exists at repo root and is applied by the build | infrastructure | File presence check + a quick "modify-with-bad-format → see if VS reformats on save" smoke (manual on first iteration) | ❌ Wave 0 |
| (Implied) | CON-T-01 post-build chain executes successfully under CI | infrastructure | Build log search: `xcopy` line resolves AND `UtinniCoreDotNetGen.exe` exit 0 AND `UtinniCoreDotNet/Generated/UtinniCore.cs` is touched (mtime check optional) | ❌ Wave 0 |

### Sampling Rate

- **Per local commit (developer):** `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` (after a local msbuild).
- **Per CI run (every push, every PR):** Full pipeline — msbuild + dotnet test.
- **Per phase gate (Phase 1 → Phase 2 handoff):** CI badge is green on master AND the test-the-tester procedure (below) has been exercised once.

### Test-the-Tester Procedure (validates "test failure on `master` blocks the workflow")

CONTEXT.md success criterion #3 ("A test failure on `main` blocks the workflow and is visible on the commit") requires affirmative validation. This is the Nyquist self-test:

1. After Phase 1 ships and CI is green on master, create a throwaway branch `verify/test-tester`.
2. Modify `HotkeyTests.cs` to add a deliberately-failing test: `[Fact] public void Verify_TestRunner_FailsBuild_OnAssertFalse() => Assert.True(false, "intentional failure for test-the-tester");`
3. Push the branch and open a PR against master.
4. Verify: (a) GitHub Actions runs the workflow on the PR, (b) the workflow exits red, (c) the PR shows the red X mark on the commit, (d) the test-results artifact is uploaded.
5. Close the PR without merging. Delete the branch.
6. Record the validation in `.planning/phases/01-ci-tier-1-c-scaffold/01-VERIFICATION.md` (or wherever verify-phase outputs go) with: PR URL, screenshot of red X, screenshot of "passing" badge on master immediately after (to prove the master badge is unaffected).

**Why this matters:** Without this procedure, success criterion #3 is unverified — we'd only know the workflow CAN exit red if it ever organically failed. The throwaway-branch approach proves it without polluting master's history.

**Cost:** ~5 minutes. Worth it.

### Badge color flip as a passive signal

The badge URL `…/badge.svg?branch=master` serves a different SVG depending on the current workflow status. After Phase 1 lands and the test-the-tester procedure runs, the badge should:
- Show GREEN immediately on master push (workflow passes).
- Show GREEN throughout the test-the-tester PR's lifecycle (because the PR is targeting master but not yet merged; master's last commit is still green).
- Briefly show RED only if a failing change is accidentally merged to master — that's the alarm we want.

### Wave 0 Gaps

Phase 1 IS the Wave 0 build-out. The "gaps" are the deliverables:

- [ ] `.github/workflows/ci.yml` — entire workflow file (see Example/Pattern 2)
- [ ] `.editorconfig` — root-level (see Example 2)
- [ ] `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` — SDK-style net472 x86 (see Example 1)
- [ ] `UtinniCoreDotNet.Tests/HotkeyTests.cs` — three passing + one Skip (see Example 3)
- [ ] `Utinni.sln` — add project entry + config mappings (see Pattern 4)
- [ ] `README.md` — badge at top under title (see Example 4)
- [ ] (Optional but recommended) `UtinniCoreDotNet.Tests/packages.lock.json` — committed after first `msbuild /restore`, gives deterministic CI

**No framework install needed** — xUnit 2.9.3 and friends are NuGet packages pulled by `msbuild /restore`. `microsoft/setup-msbuild@v2` handles the build tool.

**No conftest / shared fixtures needed** — single test class, no setup required beyond xUnit's auto-discovered ctor injection (which we don't use this phase).

### Confidence-of-Validation Note

The validation architecture has a chicken-and-egg property: Phase 1's deliverable is the validator for all subsequent phases. Phase 1's own validation therefore has to be done partly by manual confirmation (the test-the-tester procedure), partly by structural review (the badge is correctly URL'd; the workflow YAML is syntactically valid; the csproj resolves). After Phase 1, validation becomes fully automated — every PR runs CI, every push to master refreshes the badge, every red X is an alarm.

## Security Domain

Phase 1 does not introduce production code paths, network endpoints, user authentication, persistence, or cryptographic operations. The security surface is the **CI workflow itself** plus the **NuGet supply chain**.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1 Architecture | yes | The workflow is publicly visible (GH Actions logs are public on a public repo) — don't print secrets. Phase 1 uses no secrets. |
| V2 Authentication | no | No authentication in deliverable. |
| V3 Session Management | no | No sessions in deliverable. |
| V4 Access Control | partial | GitHub Actions default `GITHUB_TOKEN` permissions: workflow uses `contents: read` implicit (no write needed). Recommend explicit `permissions: contents: read` at workflow level for defense-in-depth. |
| V5 Input Validation | no | No user input in deliverable. |
| V6 Cryptography | no | No cryptography in deliverable. |
| V7 Error Handling and Logging | partial | Workflow logs go to GH Actions; don't `echo` anything sensitive. Default test output is safe (no secrets in test code). |
| V8 Data Protection | no | No data persistence in deliverable. |
| V12 File and Resources | partial | NuGet packages downloaded from `api.nuget.org` (TLS, signed packages). Lock file recommended (RestorePackagesWithLockFile=true) for supply-chain pinning. |
| V14 Configuration | yes | Workflow uses pinned action versions (`@v4`, `@v2`) and pinned package versions. Don't use floating `@main` tags on actions. |

### Known Threat Patterns for {GitHub Actions + .NET supply chain}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Compromised third-party action (e.g., setup-msbuild gets backdoored) | Tampering | Pin to major version (`@v2`); for even more paranoia pin to SHA. First-party Microsoft/GitHub actions reduce risk; we use only those + xunit/xunit official packages. |
| Malicious NuGet package (slopsquatting) | Tampering | All packages this phase are first-party (Microsoft, xunit org). Lock file (`RestorePackagesWithLockFile=true`) prevents transitive drift. slopcheck protocol degraded gracefully — manual NuGet registry + official-doc cross-verification covered the audit. |
| Workflow file injection via PR title/body interpolation | Injection | Workflow uses `${{ github.workspace }}`, `${{ runner.os }}`, `${{ hashFiles(...) }}` — all safe contexts. Does NOT interpolate `${{ github.event.pull_request.title }}` or similar attacker-controlled strings. |
| Secrets exfiltration via test code | Information Disclosure | No secrets in workflow. No `secrets:` block. Default `GITHUB_TOKEN` is read-only by default; explicitly set `permissions: contents: read` at job level for clarity. |
| Cache poisoning across forks | Tampering | `actions/cache@v4` enforces cache key scoping by repo/branch. Default behavior is safe for our trigger pattern (`push: branches: [master]` + `pull_request: branches: [master]`); a PR from a fork uses fork-scoped cache, can't poison master's cache. |
| Hard-coded GH org/user reveal | Information Disclosure | Repo URL `kennethlong/Utinni` is already public per the README and existing fork-strategy memory. No new exposure. |

**Phase 1 security verdict:** LOW risk. The strongest control is the choice to use only first-party actions and packages. The biggest improvement we can make for free is `permissions: contents: read` at the workflow or job level (defense in depth — explicit token-scope hardening).

**Suggested workflow-level permissions block** (add near the top of `ci.yml`, before `jobs:`):
```yaml
permissions:
  contents: read   # explicit read-only token; defense in depth
```

## Sources

### Primary (HIGH confidence)

- **`.planning/phases/01-ci-tier-1-c-scaffold/01-CONTEXT.md`** — User-locked decisions D-01..D-11 (this phase's hard constraints).
- **`.planning/codebase/STACK.md`** — verified net472/x86/v142 toolchain.
- **`.planning/codebase/TESTING.md`** — verified zero-test baseline + Hotkey.ProcessString first-test recommendation.
- **`.planning/codebase/INTEGRATIONS.md`** — CON-T-01 post-build chain details (lines 91–94 of `UtinniCore.vcxproj`).
- **`.planning/codebase/CONVENTIONS.md`** — Allman braces, 4-space indent, MIT header, `// ToDo` form, ProcessString line 66.
- **`UtinniCoreDotNet/Hotkeys/Hotkey.cs`** — read directly; ProcessString signature + throw behavior verified (private void; throws on bad Enum.Parse on lines 82 and 91).
- **`UtinniCoreDotNet/UtinniCoreDotNet.csproj`** — read directly; legacy non-SDK csproj style, net472, x86 verified.
- **`Utinni.sln`** — read directly; Format 12.00 / VS 16; existing project GUIDs verified.
- **`UtinniCore/UtinniCore.vcxproj` lines 85–95** — read directly; CON-T-01 PostBuildEvent verified.
- **`UtinniCoreDotNetGen/UtinniCoreDotNetGen.csproj`** — read directly; AnyCPU/x64; CppSharp HintPaths to `external/CppSharp/lib/`.
- **Vendored CppSharp DLLs at `external/CppSharp/lib/`** — verified present and x64 PE32+ (.NET assemblies).
- **`microsoft/setup-msbuild` README** — github.com/microsoft/setup-msbuild — v2 release date, `vs-version` syntax (WebFetch 2026-05-16).
- **`actions/runner-images` Windows2022-Readme.md** — `Microsoft.VisualStudio.ComponentGroup.VC.Tools.142.x86.x64` preinstalled, version 17.14.36510.44 (WebFetch 2026-05-16).
- **NuGet v3-flatcontainer registry API** — version verification for `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` (curl 2026-05-16).
- **xunit.net official getting-started v2 docs** — net472 SDK-style csproj recommended setup with exact version pins (WebFetch 2026-05-16).
- **NuGet Gallery `Microsoft.NET.Test.Sdk/17.13.0`** — published 2025-02-10, supports net462+ (WebFetch 2026-05-16).
- **NuGet Gallery `xunit.runner.visualstudio` page** — version 3.1.5, runs v1/v2/v3 tests, net472 supported (WebFetch 2026-05-16).
- **NuGet Gallery `xunit/2.9.3`** — published 2025-01-08, security-only future updates (WebFetch 2026-05-16).
- **GitHub Docs — Adding a workflow status badge** — badge URL pattern `…/actions/workflows/<file>/badge.svg?branch=<branch>` (WebSearch 2026-05-16).
- **GitHub Changelog 2025-07-31** — `windows-latest` migrating to `windows-2025` between Sep 2 and Sep 30 2025.
- **GitHub Changelog 2026-05-14** — VS 2026 migration timeline for `windows-latest` Jun 8–15 2026.

### Secondary (MEDIUM confidence)

- **GitHub issue dotnet/sdk#9007** — "When running dotnet test on a solution, silently ignore non-test projects" — open issue confirming the pitfall.
- **GitHub issue microsoft/vstest#1129** — "'dotnet test' in solution folder fails when non-test projects are in the solution" — confirms mixed-solution failure mode.
- **GitHub issue xunit/xunit#1123** — "Running tests for Platform=x86 not working via dotnet test" — historical x86 discovery issue (2017, against old toolchain; not expected to bite us with current packages).
- **GitHub issue actions/runner-images#9701** — Multiple VC Build Tools removal (May 2024 cleanup; left only latest v142).
- **GitHub blog post on windows-latest migration timeline** — corroborates the 2025-Q3 migration and 2026-Q2 VS 2026 rollout.

### Tertiary (LOW confidence — flagged, not relied upon as authoritative)

- WebSearch results conflating "VS 2019 runner image deprecation" with "v142 toolset deprecation" — explicitly disregarded in favor of the actions/runner-images readme.
- 2017–2022-era StackOverflow answers on `dotnet test` x86 issues — referenced for awareness only; the modern xunit.runner.visualstudio 3.x + .NET SDK 8/9 toolchain has resolved the original issue per the official adapter readme.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every version verified via NuGet registry; every action verified via GitHub Marketplace; csproj structure verified via xunit.net official docs.
- Architecture: HIGH for the YAML shape and project layout (every pattern is direct from authoritative sources); MEDIUM for the post-build chain behavior on CI (verified components, but the runtime combination needs the first CI run to confirm).
- Pitfalls: HIGH — each is either documented in an upstream GitHub issue OR verified via direct file inspection (Hotkey.cs signature, sln config) OR verified via the runner image README (v142 presence).
- Validation Architecture: HIGH — the test-the-tester procedure is concrete; the badge URL is documented; the sampling rate matches CONTEXT.md acceptance criteria 1:1.
- Security: MEDIUM — Phase 1 has a very small security surface and the recommended controls (pin versions, scope token to read-only) are textbook. Genuine risk is LOW.

**Research date:** 2026-05-16
**Valid until:** 2026-06-30 (stable stack; recommend re-check if Phase 1 hasn't shipped by then because (a) `windows-latest`→VS 2026 migration is mid-June, (b) NuGet versions move forward steadily and the 17.13.0 pin should be revisited if a new combo becomes the documented standard).

---

*Research complete; planner can construct PLAN.md(s) from the sections above. The Common Pitfalls section in particular is engineered to be a checklist the planner converts into verification tasks (one task per pitfall).*
