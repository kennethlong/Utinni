# Phase 5: Tier 1 C++ unit tests - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-23
**Phase:** 05-tier-1-c-unit-tests
**Areas discussed:** Native coverage seed, Catch2 delivery, Build wiring, CI gate posture

---

## Native coverage seed

Phase 4 D-06 moved TRE / IFF parsers to managed C# — the ROADMAP-named Tier-1 candidates are no longer native. Pool of remaining native candidates is materially smaller; the discussion was framed around how much investment the seed coverage warrants.

| Option | Description | Selected |
|--------|-------------|----------|
| Pure utility headers only | `string_utility`, `memory::copy`, `log` — ~15-25 tests, ships fast, proves wiring. Risk: too thin for "non-trivial" per ROADMAP success criterion #2. | |
| Utility headers + plugin_framework lifecycle | Layer 1 utilities + `PluginManager` registration/lookup. ~30-50 tests; needs light refactor of statics into injectable shape (mirroring Phase 2's `Connection.setCastForTest` pattern). | |
| Grow a new testable native seam | Identify a piece of `swg/*` logic that could be pure if extracted; refactor and cover. Highest forward investment; largest blast radius. | |
| Recommend after research | Defer to phase researcher; survey native code, propose 2-3 candidates with effort estimates. | ✓ |

**User's choice:** Recommend after research
**Notes:** Locked as **D-01** — researcher surveys native code and proposes 2-3 candidates with effort estimates + failure-modes-caught for each. Final seed locked at plan-phase from the researcher's table.

---

## Catch2 delivery

Framing: Utinni today has zero package manager (everything in `external/` is vendored — CppSharp, DetourXS, ImGuizmo, LeksysINI, imgui, nvapi, spdlog). Phase 6 STAB-03 plans imgui/spdlog/ImGuizmo dep bumps that could be cleaner under vcpkg, but introducing vcpkg in Phase 5 would pre-empt that decision.

| Option | Description | Selected |
|--------|-------------|----------|
| Vendored single-header in external/catch2/ | Drop catch_amalgamated.{hpp,cpp} alongside other vendored deps. Matches existing posture; trivially auditable; pinned tag. | ✓ |
| vcpkg manifest mode integration | Add vcpkg.json + cmake-integration; Catch2 is first dep; unlocks Phase 6 dep bumps as follow-up. Wider blast radius. | |
| Defer to research | Have researcher weigh in on vcpkg net472/x86 compat + ImGui/spdlog/ImGuizmo port states. | |

**User's choice:** Vendored single-header in external/catch2/ (Recommended)
**Notes:** Locked as **D-02**. Researcher to pick the specific Catch2 tag at research time (v3.x default unless surfaced reason for v2.x). Phase 6 STAB-03 owns the broader vcpkg-vs-vendored call.

---

## Build wiring

The ROADMAP literally says "wired through ctest (or vcpkg)" — but Utinni is pure MSBuild/.vcxproj with no CMake anywhere. The discussion clarified that "ctest" is aspirational language; the operational requirement is "CI runs the native test target on every push." Also relevant: CON-T-02 (preserve RelWithDbgInfo + Release + Debug triple-config).

| Option | Description | Selected |
|--------|-------------|----------|
| Sibling UtinniCore.Tests.vcxproj as Catch2 self-runner exe | Pure MSBuild posture; triple-config inherited; CI invokes the exe with --reporter junit. Zero new build tools. | ✓ |
| Standalone CMakeLists.txt + ctest for the test target only | Matches ROADMAP language; introduces CMake as a second build system just for tests. Dual maintenance surface. | |
| VS Test Adapter for Catch2 | Wires Catch2 into vstest; single CI step. Adapter quality on net472 is mediocre. | |

**User's choice:** Sibling UtinniCore.Tests.vcxproj as Catch2 self-runner exe (Recommended)
**Notes:** Locked as **D-03**. ROADMAP's "ctest" treated as aspirational shorthand. No CMake. Catch2's standalone runner main() is the default; planner can swap to CATCH_CONFIG_RUNNER later if needed.

---

## CI gate posture

Phase 4 D-11 added the second `dotnet test` lane (CLI golden) and gated master immediately. The discussion was whether Phase 5's third lane (native) should follow the same pattern or land warn-only first to absorb native-side flake.

| Option | Description | Selected |
|--------|-------------|----------|
| Block master from day 1 | Required from first landing commit, alongside the existing two lanes. Matches Phase 4 D-11. Risk: first-run flake blocks merges. Mitigation: split-plan scaffold + soak + seed. | ✓ |
| Warn-only for 3 commits, then enforce | Land as continue-on-error for 3-5 commits, then flip required: true. Conservative; risk of warn-only being forgotten. | |
| Land separate workflow (decouple from main ci.yml) | Add native-tests.yml as separate workflow. Higher isolation; counter to Phase 4 D-11 pattern. | |

**User's choice:** Block master from day 1 (Recommended)
**Notes:** Locked as **D-04**. Risk mitigation via D-05 plan split — 05-01 lands scaffold + smoke tests (verify green twice), 05-02 lands the researcher-recommended seed coverage. Soak-window protects against first-run flake without falling into the warn-only trap.

---

## Claude's Discretion

- Exact directory layout under `UtinniCore.Tests/` (flat vs. mirror of `UtinniCore/`).
- Catch2 v3.x vs v2.x tag selection (researcher proposes, planner confirms).
- CI exe-invocation shape (--reporter junit format, output path, artifact upload glob).
- Whether to include a CATCH_CONFIG_RUNNER custom main for the seed.
- Test-tag taxonomy (e.g., `[utility]`, `[plugin_framework]`, `[smoke]`).
- Whether `UtinniCore.Tests.exe` links against `UtinniCore.dll` (production) or a hypothetical `UtinniCore.Lib.lib` static-archive variant.

## Deferred Ideas

- vcpkg as the project's package manager — surfaced during D-02; deferred to Phase 6 STAB-03.
- Coverage tooling (OpenCover / coverlet / Codecov) — surfaced during D-04; out-of-scope for Phase 5; Phase 6 / V2 candidate.
- CMake migration for the whole solution — implied by ROADMAP's "ctest" wording; rejected at D-03 in favour of MSBuild-pure; V2 revisit if build-system pain warrants.
- Refactoring `swg/*` modules for testability — if researcher's seed candidate points here, the heavy-refactor work becomes Phase 6 STAB-03 inputs.
- VS Test Adapter / unified `dotnet test` runner — rejected at D-03; revisit if native test surface grows.
