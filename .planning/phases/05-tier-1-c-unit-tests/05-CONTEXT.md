# Phase 5: Tier 1 C++ unit tests - Context

**Gathered:** 2026-05-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Stand up the first native C++ unit-test target in the Utinni solution: Catch2 vendored single-header drives a `UtinniCore.Tests.exe` sibling MSBuild project that runs as a third CI lane gating `master` alongside the two `dotnet test` lanes from Phases 1 and 4. The phase completes TEST-02 (Tier 1 C++ unit-test scaffold) per REQUIREMENTS.md and ROADMAP.md, and gives the project its first native-side regression net.

The seed coverage candidate is **deferred to the phase researcher** (D-01). Original ROADMAP-named candidates (TRE / IFF parsers, plugin manifest loading) all moved to managed C# in Phase 4 D-06 — they are no longer native. The researcher will survey what's left of the native code (`UtinniCore/utility/*`, `UtinniCore/plugin_framework/*`, the `swg/*` boundary, and the existing `test_exports.cpp` P/Invoke harness) and propose 2-3 candidate seams with effort estimates, including whether any are best covered as-is vs. behind a light refactor for testability.

**In scope (this phase):**
- Catch2 vendored at `external/catch2/catch_amalgamated.{hpp,cpp}` against a specific tag (D-02).
- `UtinniCore.Tests/UtinniCore.Tests.vcxproj` sibling MSBuild project producing `UtinniCore.Tests.exe` Catch2 self-runner (D-03). Added to `Utinni.sln` next to the existing native projects; triple-config (Debug + Release + RelWithDbgInfo) inherited from project defaults per CON-T-02.
- One or more native seed-coverage test targets, identified by the researcher and locked at plan-phase from the candidate list.
- A third CI lane in `.github/workflows/ci.yml` (Phase 4 D-11 pattern — extend the existing workflow, no parallel YAML) that runs `UtinniCore.Tests.exe` with `--reporter junit` and uploads results on failure. **Required for `master` green from day one** (D-04).
- Tier-1 native test scaffolding (file layout, naming convention, fixture pattern, log/output convention) that future native test work in Phase 6 STAB-03 and beyond can copy.
- `docs/ai/test-harness-plan.md` "Tier 1 — C++ side" row gets dispositioned with the resolved D-01..D-04.
- Code-review at phase end (`/gsd:code-review 05`) to confirm no new critical findings.

**Out of scope (this phase):**
- **vcpkg / any package-manager introduction** (D-02) — Phase 6 STAB-03 owns the vcpkg-vs-vendored decision for the broader dep tree (imgui, spdlog, ImGuizmo). Phase 5 stays consistent with the existing zero-package-manager posture.
- **CMake / ctest** (D-03) — ROADMAP language "wired through ctest (or vcpkg)" is aspirational. Operational requirement is "CI runs the native test target on every push," which the MSBuild + direct-exe path satisfies. No CMakeLists.txt anywhere.
- **VS Test Adapter for Catch2** (D-03 alt-rejected) — adapter quality on net472 + the dual-runner complexity is not worth it for a single native test target.
- **Refactoring `swg/*` modules to gain testability** — the deferred-to-research seed proposal may suggest light extraction of one helper into a pure module, but Phase 5 is not the home for broad `swg/*` refactoring. If the researcher's top recommendation requires a heavy refactor, that becomes a Phase 6 STAB-03 candidate, not Phase 5.
- **Coverage-tooling integration (OpenCover, coverlet, Codecov, etc.)** — TESTING.md notes none exist project-wide; introducing one is a separate decision. Phase 5 ships green tests, not a coverage gate.
- **Tier 3 (recorded-fixture + mock-D3D9) work** per test-harness-plan.md — deferred to V2 per ROADMAP.
- **Live-SWG-injection coverage** — Tier 4 manual residual, lives in Phase 6 TEST-04.
- **Cross-repo touch to UtinniPlugins** — Phase 5 is fully Utinni-side. UtinniPlugins gets touched at Phase 7 (first Wave-1 plugin).
- **Adding new tests to the existing managed test projects** — `UtinniCoreDotNet.Tests` and `Utinni.Cli.Tests` are full from Phase 4; Phase 5 only adds the native sibling. Any managed test gaps surface in Phase 6 STAB-03.

</domain>

<decisions>
## Implementation Decisions

### Native Coverage Seed (deferred to research)

- **D-01:** **The native coverage seed is deferred to the phase researcher.** Original ROADMAP candidates (TRE / IFF parsers, plugin manifest loading) all moved to managed C# in Phase 4 D-06 — they are no longer native. The researcher will survey what's actually left in `UtinniCore/`, propose **2-3 candidate seed targets** with effort estimates, and identify whether each is best covered as-is or behind a light refactor-for-testability. Candidate pool (non-exhaustive, to be confirmed by researcher): `utility/string_utility.{cpp,h}` (toBool/trim/toHexString/toString round-trip — pure helpers), `utility/memory.{cpp,h}` (`copy` round-trip), `utility/log.{cpp,h}` (formatters and rotation logic), parts of `plugin_framework/PluginManager.{cpp,h}` (registration / lookup / duplicate-handling), and `test_exports.cpp` (already exists as a P/Invoke harness for the managed test side — may already be partially covered by its consumers). Researcher to flag any candidate whose test value is gated on a refactor too heavy for Phase 5 (those become Phase 6 STAB-03 candidates instead). Final seed locked at plan-phase from the researcher's table.

### Catch2 Delivery (resolves test-harness-plan.md Tier 1 C++ row)

- **D-02:** **Catch2 vendored as `external/catch2/catch_amalgamated.{hpp,cpp}` against a specific tag.** Matches the existing zero-package-manager posture exactly — every other dep in `external/` (CppSharp, DetourXS, ImGuizmo, LeksysINI, imgui, nvapi, spdlog) is vendored. ~30K LOC drop-in but trivially auditable; pinned tag, no resolution surprises. Phase 6 STAB-03 owns the broader vcpkg-vs-vendored call for the imgui/spdlog/ImGuizmo dep bumps it's already planning; Phase 5 does not pre-empt that decision. Researcher to pick the specific Catch2 tag at research time (latest stable v3.x is the default unless they surface a reason to prefer v2.x for net472/x86 compat reasons).

### Build Wiring (resolves test-harness-plan.md "ctest or vcpkg" gap)

- **D-03:** **Sibling `UtinniCore.Tests/UtinniCore.Tests.vcxproj` produces `UtinniCore.Tests.exe` as a Catch2 self-runner; no CMake, no ctest, no test adapter.** Project added to `Utinni.sln` next to the existing native projects (`UtinniCore`, `Launcher`, `Utinni.LoaderLockHarness`, `Utinni.CrtMatchPlugin`, `Utinni.LegacyPlugin`, etc.). Triple-config (Debug + Release + RelWithDbgInfo) inherited from project defaults — explicitly preserves CON-T-02. `main()` is Catch2's standalone runner (no custom main needed for the seed; can swap to Catch2's `CATCH_CONFIG_RUNNER` later if a fixture-init hook is needed). CI invokes the exe with `--reporter junit --out testresults.xml` and uploads on failure. The ROADMAP word "ctest" is treated as aspirational shorthand — the operational success criterion is "CI runs the native test target on every push," which the MSBuild + direct-exe path satisfies cleanly. **DEC-C3 (tiered testing strategy)** is not affected; Phase 5 is implementing the Tier 1 C++ side of that locked decision.

### CI Gate Posture (resolves CI-lane question from Phase 4 D-11 follow-on)

- **D-04:** **The new third lane is required for `master` green from day one** — same posture as Phase 4 D-11's CLI-golden lane. Extends `.github/workflows/ci.yml` (the existing single workflow); does not add a parallel YAML. Job step lands AFTER the existing `dotnet test Utinni.Cli.Tests/...` step from Phase 4. **Risk mitigation:** Plan structure (per D-05 below) lands the scaffold + 2-3 smoke tests first as a separate PR; verify the lane goes green twice; then add the researcher-recommended seed tests in the second plan. This soak-window protects against first-run flake (path issues, msbuild output layout surprises) without falling into the "warn-only" trap of letting a yellow lane go un-fixed for weeks.

### Plan Structuring (D-05) — 2 plans by concern, CI-gated

- **D-05:** Plans grouped by **concern**, not by test target. **05-01 scaffold** (vendor Catch2 + add `UtinniCore.Tests.vcxproj` + add it to `Utinni.sln` + CI lane wire-up + 2-3 trivial smoke tests like `REQUIRE(1+1 == 2)` and a sanity check on the vendored Catch2 itself; goal is to prove the build + CI green-on-master pipeline, NOT to deliver coverage value). **05-02 seed coverage** (researcher-recommended seed target from D-01; the actual non-trivial coverage that satisfies ROADMAP success criterion #2). 05-02 doesn't start until 05-01 is green on `master` (Phase 1/2/2.1/3/4 precedent — plan boundaries are CI-gated). Planner may add a 05-03 if the researcher's seed proposal naturally splits (e.g., if the seed is `plugin_framework` and the refactor-for-testability work needs its own plan-and-review cycle separate from the test-writing).

### Verification Posture (max-harness preserved)

- **D-06:** **Max-harness posture preserved from Phases 2 / 02.1 / 3 / 4** ([[feedback-max-harness]] is the standing user preference). The seed coverage in 05-02 ships tests that would fail if the covered function were reverted to a broken state. Pure smoke tests (e.g., "the test exe runs and exits 0") do not qualify as the seed coverage — they qualify as the scaffold proof in 05-01. The researcher's seed proposal must include the failure-mode each test catches, so reviewers can confirm the harness is real rather than ceremonial.

### Triple-Config Preservation (CON-T-02 guard-rail)

- **D-07:** **`UtinniCore.Tests.vcxproj` honours the triple-config layout end-to-end.** Debug, Release, and RelWithDbgInfo all build the test exe; CI's existing `MSBuild Utinni.sln /p:Configuration=...` matrix (or single-config Release lane, depending on what Phase 4 D-11 left in `ci.yml`) sees the new project automatically. Planner verifies at scaffold time that all three configs produce a working exe and that the CI lane builds the same config that produces it. **No new `RelWithDbgInfo`-only oddities** — Catch2 vendored as a `.cpp` compiled into the test exe directly, no separate library project, no per-config conditional includes.

### Claude's Discretion

- Exact directory layout under `UtinniCore.Tests/` (flat vs. mirror of `UtinniCore/`'s tree) — planner picks based on the researcher's seed.
- Catch2 v3.x vs v2.x — researcher proposes, planner confirms at scaffold time.
- The exact CI exe-invocation shape (`--reporter junit` vs `--reporter console + parse`, output path, artifact upload glob) — planner picks; mirrors the Phase 4 D-11 `actions/upload-artifact@v4` pattern.
- Whether to include a `CATCH_CONFIG_RUNNER` custom main for the seed or stay on Catch2's default main — planner picks based on whether the seed needs per-test setup beyond what `SECTION` and `GIVEN/WHEN/THEN` provide.
- Test-class naming convention — adopt Catch2's `TEST_CASE("descriptive sentence", "[tag]")` idiom; planner finalises the tag taxonomy (e.g., `[utility]`, `[plugin_framework]`, `[smoke]`).
- Whether `UtinniCore.Tests.exe` links against `UtinniCore.dll` (production binary, with all its globals + RVA hooks) or against a `UtinniCore.Lib.lib` static-archive variant if one is producible without re-architecting — researcher to flag this if it matters for the seed; planner decides.

### Folded Todos

None — `gsd-sdk query todo.match-phase 5` not run during this discussion (no GSD todos system active in this project). Will be re-checked at plan-phase if relevant.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context (locked decisions, requirements, constraints)
- `.planning/PROJECT.md` — Key Decisions table; DEC-C3 (LOCKED tiered testing strategy) is what this phase implements the Tier-1-native side of.
- `.planning/REQUIREMENTS.md` §TEST-02 — Tier 1 C++ unit-test scaffold; ROADMAP success criteria + acceptance language.
- `.planning/ROADMAP.md` §"Phase 5: Tier 1 C++ unit tests" — goal, depends-on, requirements, preservation guard-rails, success criteria.

### Phase 4 context (parsers moved to managed — affects native pool)
- `.planning/phases/04-tier-2-cli-shim-golden-fixtures/04-CONTEXT.md` §D-06 — confirms TRE and IFF parsers are MANAGED, not native; original ROADMAP Tier-1 candidates are gone.
- `.planning/phases/04-tier-2-cli-shim-golden-fixtures/04-CONTEXT.md` §D-11 — CI lane extension pattern Phase 5 inherits.

### Codebase maps
- `.planning/codebase/TESTING.md` — exhaustive read; note that the doc was written BEFORE Phases 1-4 added managed tests, so the "zero tests" framing is now out of date; the "Recommended First Tests If You Are Adding Coverage" §#5 explicitly anticipates this phase ("Catch2 C++ unit tests for `utility/string_utility.h`").
- `.planning/codebase/STRUCTURE.md` — directory tree; locate the native modules.
- `.planning/codebase/CONVENTIONS.md` — naming + structure conventions to follow for the new `UtinniCore.Tests/` project.
- `.planning/codebase/STACK.md` — toolchain (MSBuild, vcxproj triple-config, net472/x86 for managed); informs why pure-MSBuild path matches the existing posture.

### Test harness source-of-truth
- `docs/ai/test-harness-plan.md` §"Tier 1 — Pure unit tests" — the doc that motivated this phase; Phase 5 closes its "Tier 1 — C++ side" row.
- `docs/ai/assessment.md` — broader code-quality audit; references native code-quality concerns that may inform the researcher's seed proposal.

### Constraints flagged in this phase
- **CON-T-02** (preserve RelWithDbgInfo + Release + Debug triple-config layout) — Phase 5 preservation guard-rail per ROADMAP. Honoured by D-07.
- **CON-N-01..-09** (preserve native code structure) — STAB-04 cross-cutting; Phase 5 must not refactor `swg/*` for testability without explicit researcher recommendation and reviewer sign-off.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `external/` already houses 7 vendored deps with no package manager; `external/catch2/` slots in identically.
- `test_exports.cpp` (in `UtinniCore/`) is a P/Invoke harness exposing native helpers to the managed test side — already a native testability seam, may inform the researcher's candidate list.
- The Phase 2 `Utinni.LoaderLockHarness` and Phase 3 cross-repo `Utinni.CrtMatchPlugin` are precedents for adding sibling MSBuild native projects to `Utinni.sln`; planner can model `UtinniCore.Tests.vcxproj` on either.
- `.github/workflows/ci.yml` already has two MSBuild + two `dotnet test` steps from Phases 1 and 4; the new lane is a third invocation of the test exe after the existing `Utinni.Cli.Tests` step.

### Established Patterns
- **Sibling-project pattern** (Phase 1 D-01, Phase 2 sibling-project, Phase 4 D-02) — new test work adds a sibling project to `Utinni.sln`, never reaches into the production project's csproj/vcxproj.
- **Triple-config preservation** (CON-T-02) — every native project in the solution builds in Debug + Release + RelWithDbgInfo; new project must too.
- **CI lane extension** (Phase 4 D-11) — extend `ci.yml`, don't add a parallel workflow; required for `master` from day one.

### Integration Points
- `Utinni.sln` — add `UtinniCore.Tests.vcxproj` project entry + dependencies on `UtinniCore` if linking against the prod DLL.
- `.github/workflows/ci.yml` — add MSBuild step (if not already implicit) + new exe-invocation step after the Phase 4 CLI step.
- `external/catch2/` — new directory; mirror the structure of `external/spdlog/` or `external/imgui/` for consistency.

</code_context>

<specifics>
## Specific Ideas

- The researcher should explicitly evaluate whether `test_exports.cpp` already exists in a form that's covered indirectly via the managed tests (`UtinniCoreDotNet.Tests` exercises `test_exports.cpp` via P/Invoke). If so, that's a hint about how thin the "non-trivial coverage" bar can be while still being defensible.
- The 05-01 scaffold smoke tests should include at least one that exercises the vendored Catch2 itself (e.g., a `REQUIRE_THROWS_AS` to verify the exception machinery, or a `SECTION` to verify the BDD-style runner) — proves the vendor drop landed correctly before the seed coverage rides on it.
- Phase 6 STAB-03's planned imgui / spdlog / ImGuizmo dep bumps should NOT be pre-empted by Phase 5. If the researcher finds a reason vcpkg would be easier than vendoring Catch2, surface that as a Phase 6 input (deferred), don't pull it forward.

</specifics>

<deferred>
## Deferred Ideas

- **vcpkg as the project's package manager** — surfaced during D-02 discussion; explicitly deferred to Phase 6 STAB-03. If Phase 5 research surfaces a reason vcpkg would simplify Catch2 delivery specifically, surface as Phase 6 input, don't pull forward.
- **Coverage tooling (OpenCover / coverlet / Codecov)** — surfaced during D-04 discussion; explicitly out-of-scope for Phase 5. Candidate for Phase 6 STAB-03 or V2.
- **CMake migration for the whole solution** — never raised explicitly but the ROADMAP word "ctest" implied it. Out of scope; D-03 explicitly chose to stay MSBuild-pure. Could be revisited at V2 if the project's build-system pain warrants it.
- **Refactoring `swg/*` modules for testability** — if researcher's seed proposal points here, the heavy-refactor candidates become Phase 6 STAB-03 inputs, not Phase 5 scope.
- **VS Test Adapter for Catch2 / unified `dotnet test` runner** — rejected at D-03; could be revisited if a future phase ships enough native test work that the dual-runner CI shape becomes annoying.

</deferred>

---

*Phase: 05-tier-1-c-unit-tests*
*Context gathered: 2026-05-23*
