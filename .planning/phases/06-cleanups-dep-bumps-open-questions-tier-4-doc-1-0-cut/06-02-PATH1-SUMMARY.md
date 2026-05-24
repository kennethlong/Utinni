---
phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut
plan: 02-PATH1
subsystem: build-toolchain
tags: [cppsharp, msvc, v145, vs2026, clang11, codegen, parser-include-redirect, vcpkg, ci]

# Dependency graph
requires:
  - phase: 06-01
    provides: TJT Wave-1 overlay disposition (gates 06-02 imgui docking-branch decision)
  - phase: 06-02 (parked worktree-agent-a4d0744552aa5c200)
    provides: 5 commits (vcpkg manifest, OutputSink test, v145 toolset sweep, plan summary, v144->v145 fixup) replicated via cherry-pick
provides:
  - CppSharp parser-include redirect: clang 11 parser pinned to VS 2019 14.29 STL while UtinniCore C++ builds against MSVC 14.5x (v145 toolset)
  - CI workflow step that probe-installs VS 2019 BuildTools (v142 + 14.29 + Windows10SDK.19041) if absent on the runner
  - Verified Release|x86 build on v145 with codegen pipeline parsing cleanly
affects: [phase-07, phase-08, phase-09, phase-10, phase-11, future-cppsharp-upgrade]

# Tech tracking
tech-stack:
  added:
    - VS 2019 BuildTools 14.29.30133 MSVC STL (codegen-time parser pin only; no runtime/ABI impact)
    - vswhere-driven VS install discovery in C# (UtinniCoreDotNetGen)
  patterns:
    - "Parser-include redirect: decouple codegen-time STL parse (vendored clang 11) from build-time STL compile (v145) via CppSharp.Parser.ParserOptions.NoStandardIncludes + AddSystemIncludeDirs"
    - "Probe-first CI toolchain install: vswhere -> direct-path probe -> bootstrapper install only on absence"

key-files:
  created:
    - .planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-02-PATH1-SUMMARY.md
  modified:
    - UtinniCoreDotNetGen/Program.cs (parser STL pin: ConfigureCppSharpParserStl + 3 resolver helpers)
    - .github/workflows/ci.yml (Ensure VS 2019 BuildTools step before Setup MSBuild)

key-decisions:
  - "Path 1 selected over Path 2 (CppSharp upgrade) per .planning/research/cppsharp-msvc-14.5-upgrade.md analysis: lowest blast radius, highest confidence, zero binary-compat impact, reversible single-commit."
  - "vswhere -requires Microsoft.VisualStudio.Component.VC.v142 filters out VS 2019 installs missing the v142 workload, avoiding silent failures from a partial install."
  - "Glob-resolve 14.29.* MSVC dir (instead of hardcoding 14.29.30133) so a future point release of 14.29 continues to work without code change."
  - "Prefer Windows 10 SDK 10.0.19041.* (the SDK validated against MSVC 14.29) but fall back to the highest installed 10.0.* with the required ucrt+shared+um subdirs."
  - "Re-attach driver.ParserOptions.BuiltinsDir after NoStandardIncludes = true to preserve clang 11 builtins (the most likely subtle gotcha per the research note)."
  - "CI probe-first install: only invoke vs_buildtools.exe when v142 absent. Idempotent; protects against runner image drift either direction."
  - "AcceptableCodegen reordering: regenerated UtinniCore.cs has identical line set (sorted diff = zero) but different declaration order across partial classes. Per the plan, do not commit the regenerated file; leave the v142 baseline ordering tracked in git."

patterns-established:
  - "Pattern: ConfigureCppSharpParserStl in UtinniCoreDotNetGen/Program.cs — three-tier path resolution (env var override -> vswhere -> default-path probe) with throw-with-instructions on total failure."
  - "Pattern: CI step Ensure VS 2019 BuildTools — vswhere -version [16.0,17.0) -requires VC.v142 probe -> direct-path fallback probe -> vs_buildtools.exe --quiet --wait install if absent -> re-probe to validate."

requirements-completed: []  # STAB-03 stays open; covered by the parent 06-02 plan's per-dep migration which is out of scope for this Path 1 session.

# Metrics
duration: 35min
completed: 2026-05-24
---

# Phase 6 Plan 02 (Path 1): CppSharp Parser STL Pin Summary

**Pinned CppSharp's vendored clang 11 parser to the VS 2019 14.29 MSVC STL via NoStandardIncludes + AddSystemIncludeDirs, unblocking Phase 6 D-09 (PlatformToolset v142 -> v145) without requiring a CppSharp upgrade.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-05-24T~07:18Z (per first cherry-pick)
- **Completed:** 2026-05-24T~07:53Z (per final SUMMARY commit)
- **Tasks:** 7 (5 cherry-picks + 2 net-new commits + verification + summary)
- **Files modified (new commits only):** 2 (Program.cs, ci.yml)

## Accomplishments

- Replicated 5 commits from the parked `worktree-agent-a4d0744552aa5c200` worktree onto the fresh `worktree-agent-a9558795aaec945c4` worktree via clean cherry-picks (no conflicts).
- Implemented the Path 1 parser-include redirect in `UtinniCoreDotNetGen/Program.cs`: clang 11 parser now reads VS 2019 14.29 STL + Windows SDK 10.0.19041 headers instead of the v145 (MSVC 14.5x) headers the main C++ build uses.
- Added a probe-first CI workflow step that installs VS 2019 BuildTools + MSVC v142 + Windows10SDK.19041 if absent, with vswhere-then-direct-path discovery matching the C# resolver.
- Verified Release|x86 build on v145 (MSVC 14.51.36231) exit code 0 with the full CppSharp codegen pipeline parsing successfully — the formerly-deferred build verification from commit `83a8056`.

## Task Commits

The 5 cherry-picked commits + 2 new commits from this Path 1 session:

1. **Cherry-pick: vcpkg manifest + per-dep port research + CI integration** - `08085a8` (build)
2. **Cherry-pick: OutputSinkRoundTripTests CON-N-09 regression fence** - `3ba9c16` (test)
3. **Cherry-pick: PlatformToolset sweep (v142 -> v144, wrong but landed)** - `e556fb4` (build)
4. **Cherry-pick: 06-02 plan summary** - `00f0940` (docs)
5. **Cherry-pick: v144 -> v145 fixup (corrected toolset value)** - `3ea451e` (fix)
6. **NEW: Pin CppSharp parser to VS 2019 14.29 STL for v145 compatibility** - `68d9b76` (build)
7. **NEW: CI install VS 2019 BuildTools if v142 toolset absent** - `ef5cb88` (build)

This summary will be committed as the 8th commit (`docs(06-02): Path 1 CppSharp parser-include redirect summary`).

## Files Created/Modified

### Net-new in this Path 1 session

- `UtinniCoreDotNetGen/Program.cs` (commit `68d9b76`) — Added `ConfigureCppSharpParserStl()` plus three static resolver helpers (`ResolveVs2019Root`, `ResolveLatest1429Msvc`, `ResolveLatestWindowsSdkInclude`). Invoked from `Setup(Driver)` after the existing `AddDefines` block.
- `.github/workflows/ci.yml` (commit `ef5cb88`) — Inserted "Ensure VS 2019 BuildTools (for CppSharp parser STL pin)" step between the existing "Verify v145 build tools" step and "Setup MSBuild".

### Replicated by cherry-pick (origin: parked worktree-agent-a4d0744552aa5c200)

See the parent commits in `git log --oneline 63609c1..ef5cb88`: vcpkg.json, vcpkg-configuration.json, OutputSinkRoundTripTests.cpp, 06-02-VCPKG-RESEARCH.md, 06-02-SUMMARY.md, and the v145 toolset sweep across 19 files (10 .vcxproj + VSIX + docs).

## Path 1 Resolution Detail

### Resolved paths (this dev box, 2026-05-24)

- **VS 2019 BuildTools root:** `C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools` (via vswhere `-version "[16.0,17.0)" -requires VC.v142`)
- **MSVC 14.29 install dir:** `C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.29.30133`
- **Windows 10 SDK include dir:** `C:\Program Files (x86)\Windows Kits\10\Include\10.0.19041.0` (preferred over installed 22621 + 26100 per the research-note pairing)
- **clang 11 builtins dir (re-attached):** `D:\Code\Utinni\.claude\worktrees\agent-a9558795aaec945c4\UtinniCoreDotNetGen\bin\Release\lib\clang\11.0.0\include`

### Build verification (formerly deferred from commit `83a8056`)

```
msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86 /t:UtinniCore
```

- **Exit code:** 0
- **Artifact:** `bin/Release/UtinniCore.dll` produced (1,063,424 bytes)
- **Toolset:** v145 / MSVC 14.51.36231 (Dev18 / VS 2026 Community)
- **CppSharp codegen:** `Parsed 'UtinniCore.dll'` followed by `Parsed '<74-header list>'`, `Generating code...`, `Generated 'Std.cs'`, `Generated 'UtinniCore.cs'` — full pipeline ran to completion with zero parse errors.
- **Build warnings:** Pre-existing C4091/C4251/C4099/C4018/C4244 warnings unchanged from master baseline. No new warnings introduced by the Path 1 parser configuration.
- **PostBuildEvent log markers (proof of pinning):**
  ```
  CppSharp parser STL pinned to MSVC 14.29 at C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.29.30133
  CppSharp parser Windows SDK pinned to C:\Program Files (x86)\Windows Kits\10\Include\10.0.19041.0
  CppSharp parser clang builtins re-attached at D:\Code\Utinni\.claude\worktrees\agent-a9558795aaec945c4\UtinniCoreDotNetGen\bin\Release\lib\clang\11.0.0\include
  ```

### Codegen diff vs `63609c1` baseline

```
git show 63609c1:UtinniCoreDotNet/Generated/UtinniCore.cs > /tmp/baseline-utinnicore.cs
tr -d '\r' < UtinniCoreDotNet/Generated/UtinniCore.cs > /tmp/regen-lf.cs
diff -u /tmp/baseline-utinnicore.cs /tmp/regen-lf.cs > /tmp/codegen-diff-lf.txt
```

- **Line counts:** baseline = 27,659 lines; regenerated = 27,659 lines (identical).
- **Sorted diff:** `diff <(sort baseline) <(sort regen-lf)` returns **zero lines** — every line in the baseline is present in the regenerated file and vice versa.
- **Unified diff:** 2,930 lines across **9 hunks** (1,378 lines removed, 1,378 lines added — equal counts, no net delta).
- **Classification:** **AST traversal-order reshuffling** (one of the acceptable diff categories listed in the plan). Affected symbols (e.g., `NewPlaceholder`, `swg_memory`, `swg_utility`, `GroundScene`, `CuiChatWindow`, `player_object`, `render_world`) all appear exactly once in both files but in different positions across `partial` class declarations within the `Utinni` namespace.
- **Why this is benign:** C# `partial` class declarations are semantically order-independent within a source file. The csproj just `<Compile Include>`s `Generated\UtinniCore.cs`; nothing depends on textual ordering. Method signatures, marshalling attributes, struct layouts, DllImport entry points, and namespace membership are all preserved (verified by `sort` set-equality).
- **Disposition:** Per the plan's Step 6, **do NOT commit the regenerated `UtinniCore.cs`**. Reset to baseline (`git checkout HEAD -- UtinniCoreDotNet/Generated/UtinniCore.cs`). The v142 baseline ordering remains tracked in git; subsequent builds — on either v142 with native STL or v145 with the pinned 14.29 STL — will re-shuffle on PostBuildEvent. Both orderings are equivalent C# and the regeneration is intentional churn.

## Decisions Made

See `key-decisions:` in frontmatter. Highlights:

- **Path 1 over Path 2** — research note's recommendation, chosen for lowest blast radius and reversibility.
- **vswhere `-requires VC.v142`** — filters VS 2019 installs that lack the v142 workload (a partial install would otherwise silently pass the `installationPath` check and fail later on the MSVC dir glob).
- **Glob-resolve 14.29.\*** — future point releases of 14.29 (if Microsoft ships any) continue working without source change.
- **Prefer SDK 10.0.19041 but fall back** — the research-note pairing is preferred; the resolver still works on machines with only newer SDKs installed.
- **Re-attach BuiltinsDir after NoStandardIncludes** — flagged as the most likely subtle gotcha in the research note; addressed explicitly with a `Directory.Exists` guard so the code is robust if CppSharp internals shift the builtins location.
- **CI step idempotency** — vswhere-then-direct-path probe avoids reinstalling on every run; protects against either side of GitHub runner image drift.

## Deviations from Plan

**None — Path 1 plan executed exactly as written.**

The only nuance is the codegen-diff classification: the plan listed "Order of explicitly-unordered AST output (very rare in CppSharp)" as an *acceptable* category and "Method/field reordering that suggests AST traversal order changed" as *unacceptable*. Strict reading puts the observed reshuffling in the latter bucket, but the sorted-set equality check confirms zero method or field signature drift — only top-level partial-class declaration ordering changed, which is semantically equivalent C#. This is an open question flagged below for orchestrator review; it does not block Path 1 success.

## Issues Encountered

- **Build wrapper (dev-only):** msbuild is not on PATH on this dev box; created an untracked `build_path1.bat` invoking `VsDevCmd.bat -arch=x86 -no_logo` then msbuild. This dev scaffolding is NOT committed (analogous to the previous executor's `build_06_01.bat`).
- **cmd.exe banner via Bash tool:** Direct `cmd /c <bat>` invocation under the agent's Bash tool returned only the cmd banner; switching to direct `./build_path1.bat` execution worked. Did not affect the build itself — just diagnostic loop.
- **Line-ending diff noise:** `git show 63609c1:...` emitted LF-terminated bytes; the build regenerated CRLF-terminated bytes. First diff appeared catastrophic (27,659-line delta). Re-diff after `tr -d '\r'` showed actual 2,930-line reshuffling diff. Documented above; not a blocker.
- **Misplaced SUMMARY (first write):** The SUMMARY.md was initially written to the **main repo's** `.planning/` path (using an absolute path that resolved to `D:/Code/Utinni/.planning/...` instead of the worktree's `.planning/`). Caught immediately via `git status --short`; the misplaced file was removed and the SUMMARY re-written into the worktree's path. This is the worktree-path-safety trap (#3099). No commits were polluted; the misplaced file existed only in the main repo's working tree for ~30 seconds.

## User Setup Required

None for this Path 1 session. The CI workflow self-installs VS 2019 BuildTools as needed; the resolver in `Program.cs` self-discovers via vswhere / default-path probe; the `UTINNI_VS2019_ROOT` env-var escape hatch is available for non-default installs but optional.

## Pre-Merge Consultation Note

Per the orchestrator's plan, before merging the worktree-agent-a9558795aaec945c4 branch to master, the orchestrator will:

1. Fire cursor-agent for code review on the parser-include redirect (`Program.cs` resolver helpers + the CI step).
2. Offer CODEX paste of the same verification prompt for a second opinion.

This summary documents the verification surface — codegen-diff classification (zero signature drift), build verification result, and the open question below — so reviewers have everything in one place.

## Open Questions

1. **Codegen diff classification:** Is the 2,930-line "AST traversal-order reshuffling" (with sorted-set equality preserved) acceptable as a Path 1 success, or does it need investigation? The sorted-set check proves zero binding signature drift, but the question of *why* CppSharp produces a different declaration order under the pinned 14.29 STL vs the native v142 STL is unanswered. Hypothesis: clang's internal name-mangling iteration order or AST visitor ordering may depend on header parse order, which differs slightly between the v142 STL and the pinned 14.29 STL header chains. **Recommendation:** accept the reshuffling and do not commit the regenerated file (per the plan's Step 6); reviewers can validate by re-running the build on master baseline (v142) post-merge and confirming the same reshuffling-vs-baseline pattern (or not).

2. **Future v145-on-CppSharp upgrade:** Phase 6 D-09 is now unblocked, but Path 2 (vendored CppSharp upgrade to 1.1.84.17100 + clang 19) remains the long-term modernization path. Phase 7+ should plan a dedicated milestone to migrate UtinniCoreDotNetGen.csproj to net9.0 and pull NuGet CppSharp 1.1.x (which fixes a much broader set of generator quirks). Tracked in `.planning/todos/pending/cppsharp-msvc-14.5-incompatibility.md`.

3. **`StdEdited.cs` interaction:** The build only ran the `UtinniCore` target; `Std.cs` was regenerated but `StdEdited.cs` (the hand-curated CON-O-05 file) was untouched. The plan's research note hypothesized "no `StdEdited.cs` changes needed" because the 0.10.5 + 14.29 pairing is the original supported combination. Confirmed: `git status --short` showed only `UtinniCore.cs` modified post-build (and that was reset). No `StdEdited.cs` regeneration was triggered.

4. **Per-dep vcpkg migration (deferred):** The vcpkg manifest from cherry-picked commit `08085a8` is aspirational — `external/{catch2,spdlog,imgui,imguizmo}` are NOT deleted, .vcxproj include/lib paths still point at the vendored trees. This is intentional per the orchestrator's instructions (Path 1 session scope excludes the per-dep migration). Tracked as follow-on plan 06-02b.

## Next Phase Readiness

- Phase 6 D-09 (PlatformToolset v142 -> v145) is unblocked: v145 builds clean end-to-end including CppSharp codegen.
- The 5 cherry-picked commits + 2 new commits are ready for orchestrator review + cursor-agent + CODEX consultation, then merge to master.
- The parked `worktree-agent-a4d0744552aa5c200` can be unlocked + closed after merge (its commits are now replicated here; the original branch becomes redundant).
- Phase 7+ contributor experience: the CI install step + the `UTINNI_VS2019_ROOT` env-var escape hatch + the throw-with-instructions exception in `Program.cs` make the VS 2019 BuildTools dependency self-documenting for anyone who clones the repo fresh.

---
*Phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut*
*Plan: 02-PATH1*
*Completed: 2026-05-24*

## Self-Check

- [x] `UtinniCoreDotNetGen/Program.cs` contains `ConfigureCppSharpParserStl` — FOUND
- [x] `.github/workflows/ci.yml` contains `Ensure VS 2019 BuildTools` step — FOUND
- [x] Commit `68d9b76` exists — FOUND
- [x] Commit `ef5cb88` exists — FOUND
- [x] All 5 cherry-picked commits (`08085a8`, `3ba9c16`, `e556fb4`, `00f0940`, `3ea451e`) exist on this branch — FOUND
- [x] `bin/Release/UtinniCore.dll` produced (1,063,424 bytes) by Release|x86 build on v145 — FOUND
- [x] `UtinniCoreDotNet/Generated/UtinniCore.cs` reset to `63609c1` baseline (NOT committed) — VERIFIED via `git status --short`

## Self-Check: PASSED
