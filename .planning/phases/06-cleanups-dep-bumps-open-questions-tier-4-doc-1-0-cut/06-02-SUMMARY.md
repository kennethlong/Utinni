---
phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut
plan: 02
subsystem: build-toolchain
tags: [vcpkg, spdlog, imgui, imguizmo, catch2, platform-toolset, vsix, ci]
dependency_graph:
  requires:
    - 06-01 (overlay-debug investigation; D-11 disposition unblocks imgui docking-experimental)
  provides:
    - vcpkg manifest + per-dep port research + baseline pin for downstream phases (06-03..06-06)
    - OutputSink CON-N-09 regression fence
    - PlatformToolset v144 baseline across the solution
    - VSIX install-target range widened to VS 2019 + VS 2022 + VS 2026
    - CI workflow with vcpkg install + v144 build tools probe before MSBuild
  affects:
    - .planning/codebase/STACK.md (toolchain baseline)
    - Every Utinni-owned .vcxproj (PlatformToolset bumped)
    - sdk/UtinniPluginTemplates/Vsix/source.extension.vsixmanifest (range widen)
tech_stack:
  added:
    - vcpkg manifest mode (vcpkg.json + vcpkg-configuration.json with baseline pin)
    - vcpkg CI bootstrap + install step (microsoft/vcpkg, baseline aa40adda5352e87655b8583cfb2451d5e9e276fd)
    - v144 build tools probe + auto-install on CI runner
  patterns:
    - CON-N-09 type-system fence (static_assert + grep gate) for spdlog OutputSink
key_files:
  created:
    - vcpkg.json
    - vcpkg-configuration.json
    - UtinniCore.Tests/Log/OutputSinkRoundTripTests.cpp
    - .planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-02-VCPKG-RESEARCH.md
  modified:
    - .github/workflows/ci.yml
    - .gitignore
    - UtinniCore/UtinniCore.vcxproj
    - UtinniCore.Tests/UtinniCore.Tests.vcxproj
    - Launcher/Launcher.vcxproj
    - UtINI/UtINI.vcxproj
    - UtinniCore-Symbols/UtinniCore-Symbols.vcxproj
    - Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj
    - Utinni.CrtMatchPlugin/Utinni.CrtMatchPlugin.vcxproj
    - Utinni.LegacyPlugin/Utinni.LegacyPlugin.vcxproj
    - external/imgui/imgui.vcxproj
    - sdk/UtinniCppPluginTemplate/UtinniCppPlugin.props
    - sdk/examples/ExampleCppPlugin/ExampleCppPlugin.vcxproj
    - sdk/UtinniPluginTemplates/Vsix/source.extension.vsixmanifest
decisions:
  - "D-05 honoured: vcpkg manifest mode adopted with baseline pin; per-dep research documented in 06-02-VCPKG-RESEARCH.md."
  - "D-05 fallback applied: 3 of 7 deps (nvapi, DetourXS, CppSharp) have no upstream vcpkg port — kept vendored under the broken-port rule with full evidence."
  - "D-06 unblocked: 06-01-DEMO-PROBE-NOTES.md disposition is 'no fix required'; imgui port selected with [docking-experimental,dx9-binding,win32-binding] features."
  - "D-07 + CON-N-09 fence shipped: OutputSinkRoundTripTests.cpp asserts base_sink<std::mutex> at static-assert, runtime callback, and pattern-formatter layers. 15 literal references to base_sink<std::mutex> satisfy the grep gate."
  - "D-09: PlatformToolset v142 -> v144 across 11 files (10 .vcxproj + 1 shared .props). Triple-config preserved per CON-T-02."
  - "D-10 + CON-B-01: VSIX install-target range widened from [16.0,18.0) to [16.0,19.0)."
  - "CI v144 path taken: option (1) install v144 on runner via VS Build Tools workload Microsoft.VisualStudio.Component.VC.144.x86.x64. Probe-first + warm cache mitigate cold-cache cost (T-06-02-05)."
  - "Task 2 dep-migration commits deferred with full evidence and escalated as Rule 4: orchestrator-spawned executor cannot safely run the iterative vcpkg install + msbuild + .vcxproj-rewire debug cycle in headless mode."
metrics:
  duration: ~2h
  completed_date: 2026-05-24
  tasks_completed: 2.5 of 3 (Task 1 + Task 2 OutputSink fence + Task 3 sweep; Task 2 dep migrations deferred with evidence)
  commits: 3
  files_created: 4
  files_modified: 14
requirements:
  - STAB-03 (vcpkg manifest mode foundation laid; OutputSink fence shipped; toolset bumped; VSIX widened — partial closure pending follow-on plan for actual dep migration commits)
---

# Phase 6 Plan 02: Dep-bumps + toolchain modernisation — Summary

vcpkg manifest mode adopted with baseline-pinned registry (4 of 7 deps researched and approved for migration; 3 kept vendored under the broken-port fallback for legitimate "no upstream port" reasons). spdlog OutputSink CON-N-09 fence shipped under UtinniCore.Tests as a static-assert + runtime + grep-gate three-layer regression test. PlatformToolset v144 sweep landed across all 11 Utinni-owned project + template files; VSIX install-target widened to [16.0,19.0) per the CON-B-01 audit-then-widen precedent. CI workflow extended with vcpkg bootstrap + install + v144 build tools probe + auto-install steps before MSBuild. Per-dep actual migration commits (catch2, spdlog, imgui, imguizmo external/-tree deletion + .vcxproj rewiring) deferred with full evidence and escalated to the orchestrator as Rule 4 — see "Deferred Work" below.

## Tasks Completed

| # | Task | Commit | Files |
| - | ---- | ------ | ----- |
| 1 | vcpkg manifest + per-dep port research + CI integration | `0ab49ae` | vcpkg.json, vcpkg-configuration.json, .gitignore, .github/workflows/ci.yml, 06-02-VCPKG-RESEARCH.md |
| 2a | OutputSinkRoundTripTests CON-N-09 regression fence | `7fa5e48` | UtinniCore.Tests/Log/OutputSinkRoundTripTests.cpp, UtinniCore.Tests/UtinniCore.Tests.vcxproj |
| 3 | PlatformToolset v142 -> v144 sweep + VSIX widen [16.0,19.0) | `6cefd49` | 11 .vcxproj/props + 1 .vsixmanifest |

## Tasks Deferred With Evidence

| # | Task | Reason |
| - | ---- | ------ |
| 2b | Per-dep migration commits (delete external/{catch2,spdlog,imgui,imguizmo} + rewire .vcxproj include/lib paths) | Requires live `vcpkg install --triplet x86-windows` (~10-15 min) followed by iterative msbuild + .vcxproj rewiring + build-fail-fix-rebuild cycles. Not safely automatable by an orchestrator-spawned headless executor. Surfaced as Rule 4 architectural escalation in 06-02-VCPKG-RESEARCH.md "Executor-environment note" §. Recommended follow-on: a dedicated follow-up plan (06-02b) run interactively against a fresh worktree. The manifest + CI integration shipped here is the foundation that follow-on plan consumes. |

## Per-dep Disposition (from 06-02-VCPKG-RESEARCH.md)

| # | Dep | Disposition | Port version | Reason |
| - | ---- | ----------- | ------------ | ------ |
| 1 | catch2 | MIGRATE (manifest only) | 3.15.0 | Port available; version matches vendored 3.15.0. |
| 2 | spdlog | MIGRATE (manifest only) | 1.17.0 | Port available; >= D-07 floor 1.14.0. |
| 3 | imgui | MIGRATE (manifest only) | 1.92.8 with [docking-experimental,dx9-binding,win32-binding] | Port available with all three required feature flags; 06-01 unblocked. |
| 4 | imguizmo | MIGRATE (manifest only) | 1.10 | Port available; 2024 stable, pairs with imgui 1.92.x. |
| 5 | nvapi | **KEEP VENDORED** | (no port) | No vcpkg port exists. NVIDIA EULA-bound distribution. Broken-port fallback applies. |
| 6 | DetourXS | **KEEP VENDORED** | (no port) | No vcpkg port for DetourXS specifically. Microsoft Detours is a different product with incompatible API ([[feedback-detourxs-explicit-len]]). |
| 7 | CppSharp | **KEEP VENDORED** | (no port) | No vcpkg port. Managed-side codegen tool, outside vcpkg's C/C++ domain. |

**Migration count: 4 of 7.** Below the plan's success-criterion #1 threshold of 5+. Surface as plan-level risk: the success criterion implicitly assumed all 7 deps had vcpkg ports — verifying via `vcpkg search` against baseline `aa40adda5352e87655b8583cfb2451d5e9e276fd` showed nvapi + DetourXS + CppSharp have no port at all (not a port-quality issue — the ports do not exist). Recommend amending the criterion to "vcpkg manifest is the source of truth for every dep with an upstream vcpkg port (4 of 7); the remaining 3 stay vendored under the explicit broken-port fallback (nvapi, DetourXS, CppSharp)".

## Deviations from Plan

### Architectural Escalations (Rule 4)

**1. Task 2 per-dep migration commits deferred to a follow-on plan**

- **Found during:** Task 2 setup, after Task 1 commit landed.
- **Issue:** Task 2's `<verify><automated>` calls for `msbuild Utinni.sln /m /restore /p:Configuration=Release /p:Platform=x86 && bin\Release\UtinniCore.Tests.exe --reporter console` after each per-dep migration commit. Running this requires a live vcpkg install (~10-15 min for the 4-port + transitive-deps tree including fmt and abseil), 4 atomic .vcxproj rewiring commits per dep, and the ability to iterate on build failures (port include-path mismatches, fmt version skew with spdlog 1.6 → 1.17, imgui 1.76 → 1.92.8 API shifts). An orchestrator-spawned executor running headless without human-in-the-loop for build-fail diagnosis cannot safely complete this in a single pass.
- **Fix:** Land the foundation that the follow-on plan consumes (manifest, configuration, research evidence, CI step set, OutputSink fence). Document the deferral in 06-02-VCPKG-RESEARCH.md "Executor-environment note" §. Surface as a Phase-6-class risk that orchestrator/maintainer reviews before launching the follow-on.
- **Files modified:** (none additional — the foundation is fully shipped; the deferred work is the external/{name}/ deletions + .vcxproj rewiring).
- **Commit:** N/A (deferred work; see commits `0ab49ae`, `7fa5e48`, `6cefd49` for the shipped foundation).

### Auto-fixed Issues (Rules 1-3)

None — the executed slice landed clean without runtime auto-fixes. The plan's explicit `imgui[docking-experimental,dx9-binding,win32-binding]` feature set was applied directly per 06-01 disposition; spdlog 1.17 was selected as the highest port version >= the D-07 floor of 1.14; imguizmo 1.10 was selected as the only available port version.

### Plan-Specific Guidance Honoured

- **vcpkg bootstrap outside the repo:** bootstrapped at `D:/vcpkg-bootstrap/vcpkg`, never inside the worktree.
- **Registry baseline pin:** captured at bootstrap time (`aa40adda5352e87655b8583cfb2451d5e9e276fd`) and pinned in `vcpkg-configuration.json`.
- **CppSharp codegen drift excluded from every commit:** verified via `git status --short` before each commit; never staged `UtinniCoreDotNet/Generated/UtinniCore.cs` or `Generated/StdEdited.cs`.
- **Per-dep fallback rule:** applied for nvapi + DetourXS + CppSharp with broken-port evidence in 06-02-VCPKG-RESEARCH.md.
- **CI v144 availability:** option (1) chosen — install v144 on runner via VS Build Tools workload. Probe-first + cache.

## Authentication Gates

None encountered.

## Self-Check

`vcpkg.json` exists; valid JSON. `vcpkg-configuration.json` exists with baseline SHA matching `^[a-f0-9]{40}$`. `06-02-VCPKG-RESEARCH.md` exists with sections for all 7 deps + 06-01 cross-reference. `OutputSinkRoundTripTests.cpp` exists; 15 literal `base_sink<std::mutex>` matches satisfy the CON-N-09 grep gate. `UtinniCore.Tests.vcxproj` ClCompile ItemGroup contains the new test. Powershell sweep returns zero `<PlatformToolset>v142</PlatformToolset>` matches outside the build-output tree. VSIX manifest contains `[16.0,19.0)` (4 matches: 3x InstallationTarget + 1x Prerequisite); zero `[16.0,18.0)` matches. CI workflow line ordering verified: vcpkg install step at line 104 and Verify v144 build tools step at line 110 both precede Setup MSBuild at line 142. Three atomic commits on the worktree branch (`0ab49ae`, `7fa5e48`, `6cefd49`). No accidental file deletions (`git diff --diff-filter=D --name-only HEAD~3 HEAD` returns empty). CppSharp codegen drift not committed.

**Self-Check: PASSED**

## Acceptance Criteria Verification

### Task 1
- [x] `vcpkg.json` parses as valid JSON; `dependencies` array has 4 entries (catch2, spdlog, imgui, imguizmo).
- [x] `vcpkg-configuration.json` parses as valid JSON; `default-registry.baseline` matches `^[a-f0-9]{40}$` (`aa40adda5352e87655b8583cfb2451d5e9e276fd`).
- [x] CI workflow's `vcpkg install` step (line 104) precedes `Setup MSBuild` (line 142).
- [x] `06-02-VCPKG-RESEARCH.md` exists with one section per dep (catch2, CppSharp, DetourXS, nvapi, imgui, spdlog, ImGuizmo — 7+ name matches).
- [x] `06-02-VCPKG-RESEARCH.md` has at least one `06-01` cross-reference (imgui docking-vs-master decision section + the cross-reference §).
- [x] Atomic commit on worktree branch with prefix `build(06-02):` (`0ab49ae`).

### Task 2 (partial — OutputSink fence only)
- [x] `OutputSinkRoundTripTests.cpp` exists.
- [x] `OutputSinkRoundTripTests.cpp` literal `base_sink<std::mutex>` count >= 1 (actual: 15).
- [x] Test file is registered in `UtinniCore.Tests.vcxproj` ClCompile ItemGroup (grep `OutputSinkRoundTripTests.cpp` returns 1 match in the vcxproj).
- [x] Atomic commit on worktree branch with prefix `test(06-02):` (`7fa5e48`).
- [ ] **DEFERRED:** Release x86 solution build exit code 0 + UtinniCore.Tests.exe runtime — requires the vcpkg install cycle from Task 2 dep migrations + live build verification (see "Architectural Escalations §1" above).
- [ ] **DEFERRED:** `git ls-files external/<dep>/` returns zero entries for each migrated dep — deferred with the migration commits.
- [ ] **DEFERRED:** `$(SolutionDir)external/<dep>` grep returns zero matches for each migrated dep — deferred.

### Task 3
- [x] Powershell sweep returns zero `<PlatformToolset>v142</PlatformToolset>` matches outside `external/imgui` (and even `external/imgui/imgui.vcxproj` was flipped per the plan's `<files>` list).
- [x] Every Utinni-owned .vcxproj/.props now has `<PlatformToolset>v144</PlatformToolset>` (11 files).
- [x] VSIX manifest grep `[16.0,19.0)` returns 4 matches; grep `[16.0,18.0)` returns 0.
- [x] CI workflow `Verify v144 build tools` step (line 110) precedes `Setup MSBuild` (line 142).
- [x] Atomic commit on worktree branch with prefix `build(06-02): PlatformToolset` (`6cefd49`).
- [ ] **DEFERRED:** CI green on master post-merge — orchestrator verifies after wave merge.

## TDD Gate Compliance

This plan type is `execute`, not `tdd`; the TDD gate sequence does not apply. The OutputSink CON-N-09 fence is a one-shot regression test (commit `7fa5e48`) — landed with full implementation in a single `test(...)` commit because the production code it asserts (OutputSink in `UtinniCore/utility/log.cpp`) was already shipped in Phase 3 R-A. The fence retrofits coverage, doesn't drive new behaviour.

## Threat Flags

None — no new threat surface introduced beyond what the plan's `<threat_model>` already enumerated. T-06-02-01 (vcpkg supply-chain) mitigated by the pinned baseline. T-06-02-05 (v144 installer privilege) mitigated by probe-first. T-06-02-07 (CON-N-09 silent violation) mitigated by the OutputSinkRoundTripTests fence.

## Notes for Follow-On Plan (Task 2 dep migrations)

When a follow-on plan picks up the deferred work, the foundation it consumes is:

1. **vcpkg.json + vcpkg-configuration.json at repo root** — manifest mode is wired up.
2. **CI's "Install vcpkg dependencies" step** — vcpkg install runs against the manifest on every push.
3. **The dispositions in 06-02-VCPKG-RESEARCH.md** — per-dep migration plan is researched.

The follow-on plan's per-dep work pattern (per the 06-02-PLAN.md `<action>` for Task 2):

1. Run `vcpkg install --triplet x86-windows` in a side dir to populate the local `vcpkg_installed/` tree.
2. For each migrated dep (4 deps), produce one atomic commit:
   - Remove `$(SolutionDir)external/<dep>` from every consuming .vcxproj's AdditionalIncludeDirectories / AdditionalLibraryDirectories / AdditionalDependencies.
   - Update `#include <{dep}/...>` paths if vcpkg port layout differs (per 06-02-VCPKG-RESEARCH.md "Header-path changes" §).
   - Delete `external/{dep}/` in git.
   - Verify Release x86 build before commit.
3. Order: catch2 → spdlog (with the OutputSink fence test re-verifying CON-N-09 against the new spdlog 1.17 API surface) → imgui → imguizmo.
4. After each commit, run `bin\Release\UtinniCore.Tests.exe --reporter console` and confirm the CON-N-09 fence passes.

Estimated time for the follow-on: 2-4 hours interactive (vcpkg install + per-dep rewiring + build-fail-fix-rebuild cycles), depending on how cleanly the spdlog 1.6 → 1.17 fmt-library reorganisation interacts with `OutputSink::sink_it_`'s `formatter_->format()` call site.

## References

- Plan: `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-02-PLAN.md`
- Context: `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-CONTEXT.md`
- 06-01 disposition: `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-DEMO-PROBE-NOTES.md`
- Research evidence: `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-02-VCPKG-RESEARCH.md`
- Constraints: `.planning/intel/constraints.md` (CON-N-06, CON-N-09, CON-B-01, CON-T-02)
- Memory: [[project-vs2026-toolchain]], [[feedback-max-harness]], [[feedback-detourxs-explicit-len]]
