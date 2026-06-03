---
phase: 12-revive-feasibility-spike-hard-gate-intro-skip-crash
plan: 01
subsystem: tools/ (revived SOE build CLIs)
tags: [auth-01, lift-and-shift, msbuild, v145, treefilebuilder, hard-gate]
requires: []
provides:
  - "tools/Utinni.Tools.sln (standalone, Debug|Win32 + Release|Win32)"
  - "TreeFileBuilder_d.exe / _r.exe (builds + links + runs at v145/Win32)"
  - "tools/Directory.Build.props (standalone-build include shim + UTINNI_TOOLS_NO_SHAREDLOG)"
  - "tools/PINNED-SHA.md (provenance: swg-client-v2 @5fce7bb8)"
affects: [12-02, 12-03]
tech-stack:
  added: [MSBuild v145/Win32, zlib-1.1.4 (prebuilt lib), zlib-1.2.3 (headers)]
  patterns: [lift-and-shift via git-archive, Directory.Build.props global include shim, source-port for engine-API drift]
key-files:
  created:
    - tools/Utinni.Tools.sln
    - tools/PINNED-SHA.md
    - tools/Directory.Build.props
    - tools/src/** (TreeFileBuilder + 11 shared libs + fileInterface + external compile closure, lifted verbatim)
  modified:
    - tools/src/engine/shared/application/TreeFileBuilder/src/shared/TreeFileBuilder.cpp
    - tools/src/engine/shared/application/TreeFileBuilder/build/win32/TreeFileBuilder.vcxproj
    - tools/src/engine/shared/library/sharedFoundation/src/win32/SetupSharedFoundation.cpp
    - .gitignore
key-decisions:
  - "TreeFileBuilder is NOT clean lift-and-shift: needed a source port + SAFESEH:NO + a logging decouple to go green at v145 (the AUTH-01 hard-gate finding)."
  - "Full link-closure path rejected on evidence (pulls whole game+engine via archive's PCH); surgical sharedLog decouple chosen instead (user decision)."
  - "zlib: link the 1.1.4 prebuilt (byte-exact determinant); lift 1.2.3 headers for sharedCompression compile only."
requirements-completed: [AUTH-01]
duration: ~3.5h
completed: 2026-06-02
---

# Phase 12 Plan 01: Lift TreeFileBuilder + build green at v145 — Summary

Revived **TreeFileBuilder** — the first of the three SOE build CLIs behind the AUTH-01 hard gate — from a non-compiling state at the pinned SHA to a green-building, running `TreeFileBuilder_d.exe` / `_r.exe` at v145/Win32, inside a new standalone Utinni-owned `tools/` tree. **Task count:** 2. **Files:** ~440 tracked (lifted closure + 4 build/glue files).

## What shipped
- `tools/Utinni.Tools.sln` — standalone solution, **Debug|Win32 + Release|Win32 only** (no Optimized/x64/AnyCPU; CON-P-02), 13 internally-consistent project entries.
- `tools/` lifted **verbatim** from `swg-client-v2 @5fce7bb8` via `git archive` (byte-exact to the commit): TreeFileBuilder + its 11 shared-library ProjectReferences + fileInterface + the external **compile** closure.
- `tools/PINNED-SHA.md` — provenance (SHA, not branch) + x64/CPP20 divergence watch.
- `tools/Directory.Build.props` — standalone-build shim (see deviations).
- Green build, both configs; the EXE runs and prints its CLI usage.

## HARD-GATE FINDING (the point of the spike)
**"v145 is in the vcxproj" ≠ "builds + links."** TreeFileBuilder did **not** compile at the pinned SHA even in its home tree. It needed three revival deltas — meaning revive of these tools is a *porting* effort, not pure lift-and-shift. This directly informs 12-02 (template compilers, larger closures) and the AUTH-01 feasibility conclusion.

## Deviations from Plan

**[Rule 4 — Architectural, user-approved] Source port: removed compressor factory.**
Found during Task 2 build. `TreeFileBuilder.cpp` called `TreeFile::SearchTree::borrowCompressor/returnCompressor` — a factory the `koogie-msvc-cpp20-base` branch deleted from `sharedFile` (the tool is stale vs its own engine). Ported the single call site (`compressAndWrite`) to the concrete `ZlibCompressor` (CT_zlib is the only type it uses; `SetupSharedCompression::install()` already initializes its pool). Files: `TreeFileBuilder.cpp`. Verified: compiles + runs.

**[Rule 2 — Missing critical, user-approved] /SAFESEH:NO on the EXE.**
The 2002-era prebuilt `zlib.lib` (1.1.4) predates Safe-SEH → `LNK2026`. Set `ImageHasSafeExceptionHandlers=false` on TreeFileBuilder rather than rebuild zlib.lib (the byte-exact `.tre` determinant per Pitfall 3 — must not change). x86 image metadata only; no output-byte impact. Files: `TreeFileBuilder.vcxproj`.

**[Rule 4 — Architectural, user-approved] sharedLog decouple.**
The only remaining link gap was `sharedFoundation`'s crash handler calling `TailFileLogObserver::flushAllTailFileLogObservers`. Resolving it the "full-closure" way pulls `sharedLog → sharedNetworkMessages → Archive` — and **archive's `FirstArchive.h` PCH unconditionally includes the entire game serialization registry** (`sharedGame`/`sharedObject`/`sharedSkillSystem`/`swgSharedNetworkMessages`/… via broken `../../../../../` relative includes). I presented both paths twice; the user chose to **decouple** ("don't avoid edits that are the right solution"). Gated the one call + its include behind `UTINNI_TOOLS_NO_SHAREDLOG` (defined globally in `Directory.Build.props`). Minidump path preserved; only the tail-file-log flush is dropped (a headless CLI installs no tail-file observer). Files: `SetupSharedFoundation.cpp`, `Directory.Build.props`. Result: links green with the original 11 libs.

**[Rule 2 — Missing critical, user-approved] zlib-1.2.3 + external compile closure.**
The plan's Task-1 guard forbade lifting `zlib-1.2.3`, but `sharedCompression/ZlibCompressor.cpp` `#include "zlib.h"` resolves from the 1.2.3 path → required to compile. Relaxed the guard; Pitfall 3 still honored (the *linked* lib is 1.1.4). Also lifted 8 external include dirs the plan omitted but the shared libs need to compile (`archive`, `sharedMathArchive`, `localization`, `unicode`, `localizationArchive`, `unicodeArchive`, `debugHelp`, `vtune`). Include-only; only the 11 libs link.

**[Rule 1 — Build fix] Directory.Build.props include shim.**
The lifted vcxprojs locate siblings via `$(SolutionDir)..\..\<lib>` paths authored for swg-client-v2's solution depth — broken for `tools/Utinni.Tools.sln`. Added a tree-wide `Directory.Build.props` prepending the real `include/public` roots (CI-durable; cl.exe silently skips the dead `$(SolutionDir)` entries). Header search only.

**Total deviations:** 5 (2 architectural user-approved, 2 missing-critical, 1 build-fix). **Impact:** TreeFileBuilder revived and green; the closure was kept tight (game/engine libs explored while scoping were removed — they belong to 12-02).

## Notes for 12-02 / 12-03
- The `sharedLog` decouple lives in **shared** `SetupSharedFoundation.cpp` → benefits the template compilers (12-02) too.
- 12-02's compilers (`TemplateCompiler`/`TemplateDefinitionCompiler`, ~26/28 ProjectReferences) will need the larger game/engine closure removed here — lift it there.
- Benign `MSB8012` warning (TargetPath `TreeFileBuilder.exe` vs OutputFile `_d.exe`) is cosmetic and matches upstream verbatim; left as-is.
- The `borrowCompressor` port + SAFESEH + `$(SolutionDir)` include breakage are likely to recur for the 12-02 tools — expect similar deltas.

## Self-Check: PASSED
- MSBuild exit 0 for `/t:TreeFileBuilder` at both Debug|Win32 and Release|Win32; `TreeFileBuilder_d.exe`/`_r.exe` produced and run.
- `PINNED-SHA.md` records SHA `5fce7bb8…`; linked `zlib.lib` (1.1.4) present.
- No lifted vcxproj references the live `swg-client-v2` tree (D-01).
- `tools/compile/` git-ignored; no build outputs committed.

**Ready for 12-02** (TemplateCompiler + TemplateDefinitionCompiler).
