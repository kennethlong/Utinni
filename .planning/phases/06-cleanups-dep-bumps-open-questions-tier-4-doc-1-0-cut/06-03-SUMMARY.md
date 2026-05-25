---
phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut
plan: 03
subsystem: infra
tags: [dxsdk, directxmath, d3d9, ini-parser, leksysini, pimpl, catch2, vcxproj]

# Dependency graph
requires:
  - phase: 06-02
    provides: vcpkg manifest build + v145 toolset across every .vcxproj (the .vcxproj churn this plan edits on top of)
provides:
  - DXSDK June 2010 dependency removed entirely (depth_texture.cpp uses a local Vec3 struct; no .vcxproj or CI references DXSDK)
  - LeksysINI replaced by a hand-rolled, round-trip-preserving INI parser inside UtINI::Impl (public ABI byte-for-byte unchanged)
  - 12-case Catch2 regression fence pinning the new parser's behaviour and legacy-matching coercion
  - all 8 CON-O-* open questions dispositioned in assessment.md §Open questions (CON-O-06 + CON-O-08 closed here)
affects: [06-04, 06-05, 06-06, 1.0-rc.1]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Raw-line INI model (std::vector<Line> sum-type + section->key index) for order/comment/blank/malformed-line-preserving round-trip"
    - "Local 3-float struct in lieu of D3DXVECTOR3 to retire DXSDK math (DirectXMath is the forward path)"

key-files:
  created:
    - UtinniCore.Tests/UtINI/IniParserTests.cpp
  modified:
    - UtinniCore/swg/graphics/depth_texture.cpp
    - UtinniCore/UtinniCore.vcxproj
    - .github/workflows/ci.yml
    - UtINI/utini.cpp
    - UtINI/UtINI.vcxproj
    - docs/ai/assessment.md
    - .planning/codebase/CONVENTIONS.md
    - README.md

key-decisions:
  - "Passed &vDummyPoint (address-of) to DrawPrimitiveUP: a plain Vec3 struct has no implicit FLOAT* conversion that D3DXVECTOR3 relied on, so the plan's literal substitution would not compile."
  - "Removed the dead $(SolutionDir)external; include path from UtINI.vcxproj — UtINI no longer includes anything from external/ after LeksysINI's removal."
  - "Disposition pointers in assessment.md use the stable 'Plan 06-03 Task N' identifier rather than self-referential commit SHAs (an atomic commit cannot contain its own final SHA)."
  - "Test links UtINI transitively via UtinniCore's import lib (UtINI symbols are dllexport-compiled into UtinniCore.dll); no direct UtINI ProjectReference, which would cause LNK2005 duplicate symbols."

patterns-established:
  - "Avoid the literal gated token in source comments: zero-match acceptance greps (d3dx9/D3DXVECTOR in depth_texture.cpp; LeksysINI in utini.cpp) mean even prose mentions must be reworded; historical names live in assessment.md/CONVENTIONS.md instead."

requirements-completed: [STAB-05, STAB-03]

# Metrics
duration: ~45min
completed: 2026-05-25
---

# Phase 06-03: Close last two STAB-05 open questions (DXSDK + LeksysINI) Summary

**DXSDK June 2010 fully retired (local `Vec3` replaces the sole `D3DXVECTOR3`) and LeksysINI replaced by a hand-rolled, round-trip-preserving INI parser inside `UtINI::Impl` — both fenced by Catch2 and CI-green on master.**

## Performance

- **Duration:** ~45 min (executed inline, sequential, on the main tree)
- **Completed:** 2026-05-25
- **Tasks:** 3
- **Files modified:** 8 (+1 created)

## Accomplishments
- **CON-O-08 + CON-B-03 closed:** removed `#include <d3dx9.h>`, replaced `D3DXVECTOR3 vDummyPoint` with a file-local `Vec3` struct (identical byte layout) and `&vDummyPoint`/`sizeof(Vec3)` at the `DrawPrimitiveUP` call; stripped the DXSDK include/lib paths from `UtinniCore.vcxproj` (all 3 configs) and deleted the CI "Verify DirectX SDK (June 2010)" step + runs-on comment.
- **CON-O-06 closed:** deleted `external/LeksysINI/`; reimplemented every public `UtINI` method against a hand-rolled parser living inside the PIMPL `UtINI::Impl`. `utini.h` is byte-for-byte unchanged → all 15+ callsites keep linking. The new parser preserves line order, comments, blanks, inline comments, and malformed lines on save (LeksysINI re-sorted alphabetically and dropped formatting); coercion mirrors the old AsBool/AsInt/AsDouble semantics.
- **Max-harness fence:** 12 Catch2 `TEST_CASE`s (`UtinniCore.Tests/UtINI/IniParserTests.cpp`). Full native suite green: 76 assertions / 26 test cases (Release|x86).
- **Docs:** `assessment.md` §Open questions now disposition all 8 CON-O-01..08 (CON-O-06 + CON-O-08 added) plus two §Status-tracking rows; `CONVENTIONS.md` gained a DirectXMath substitution note; README's "LeksysINI is temporary" line removed.

## Task Commits

Each task was committed atomically:

1. **Task 1: remove DXSDK June 2010 (CON-O-08 + CON-B-03)** - `4f5b5b6` (feat) — pushed; CI green (run 26408771329).
2. **Task 2: replace LeksysINI with custom INI parser inside UtINI::Impl (CON-O-06)** - `164ca59` (feat)
3. **Task 3: Catch2 fence for custom INI parser (12 cases)** - `a18f503` (test)

## Files Created/Modified
- `UtinniCore/swg/graphics/depth_texture.cpp` - local `Vec3` replaces `D3DXVECTOR3`; `&vDummyPoint`/`sizeof(Vec3)`; `d3dx9.h` include removed.
- `UtinniCore/UtinniCore.vcxproj` - DXSDK include/lib paths removed from all 3 configs.
- `.github/workflows/ci.yml` - "Verify DirectX SDK (June 2010)" step + runs-on DXSDK comment removed.
- `UtINI/utini.cpp` - new raw-line INI parser inside `UtINI::Impl`; all public methods reimplemented; LeksysINI include gone.
- `UtINI/UtINI.vcxproj` - dead `$(SolutionDir)external;` include path removed (3 configs).
- `external/LeksysINI/` - deleted (iniparser.hpp + LICENSE).
- `UtinniCore.Tests/UtINI/IniParserTests.cpp` - new 12-case Catch2 fence.
- `UtinniCore.Tests/UtinniCore.Tests.vcxproj` - registered the new test file.
- `docs/ai/assessment.md` - CON-O-06 + CON-O-08 dispositions + 2 status rows.
- `.planning/codebase/CONVENTIONS.md` - DirectXMath substitution note.
- `README.md` - removed the LeksysINI "temporary" line.

## Decisions Made
See `key-decisions` frontmatter. Most consequential: the `&vDummyPoint` address-of fix (the plan's literal edit would not have compiled), and using stable plan-task identifiers instead of self-referential SHAs in the assessment dispositions.

## Deviations from Plan

### Auto-fixed Issues

**1. [Correctness] Address-of required for DrawPrimitiveUP vertex pointer**
- **Found during:** Task 1
- **Issue:** Plan said replace `D3DXVECTOR3 vDummyPoint` → `Vec3 vDummyPoint` and keep passing `vDummyPoint`; but `DrawPrimitiveUP` takes `const void*`, and `D3DXVECTOR3` only converted implicitly via its `operator FLOAT*`. A plain struct does not, so it would not compile.
- **Fix:** Pass `&vDummyPoint` and `sizeof(Vec3)`.
- **Verification:** Release|x86 build exit 0.

**2. [Gate hygiene] Reworded comments to avoid literal gated tokens**
- **Found during:** Task 1 + Task 2
- **Issue:** Acceptance greps require zero `d3dx9`/`D3DXVECTOR` in depth_texture.cpp and zero `LeksysINI` in utini.cpp, but my explanatory comments named those (historical) tokens.
- **Fix:** Reworded comments ("the legacy DXSDK vector type", "the legacy INI library"); historical names retained in assessment.md/CONVENTIONS.md (not gated).

**3. [Cleanup] Removed dead `$(SolutionDir)external;` from UtINI.vcxproj**
- **Issue:** No LeksysINI-specific include token existed; the generic `external` path was the only thing pulling LeksysINI and is now unused by UtINI.
- **Fix:** Removed it from all 3 configs. Build confirms UtINI needs no external include.

**4. [Stale plan refs] ci.yml line numbers were pre-06-02**
- **Issue:** Plan referenced "Cache/Install DirectX SDK" steps at lines ~38-69; post-06-02 the file instead had a single "Verify DirectX SDK" step (self-hosted runner).
- **Fix:** Removed the actual Verify step + the runs-on DXSDK comment so zero `DXSDK`/`DirectX SDK` remain.

---

**Total deviations:** 4 auto-fixed. **Impact:** all necessary for correctness/gate-compliance/cleanliness. No scope creep.

## Issues Encountered
- None blocking. `UtinniCore-Symbols.vcxproj` had no DXSDK references (acceptance already satisfied) so it needed no edit.

## Out-of-scope notes
- Ancillary docs still mention LeksysINI as a vendored dep: `docs/ai/build.md`, `docs/ai/core.md`, `docs/ai/index.md`, and the generated `docs/*.html`. These are outside this plan's files; flag for a future docs-regen pass.
- The D-04 audit grep `grep -E "^.*CON-O-0[1-8]"` over the whole file also matches §Status-tracking cross-references (e.g. "closes CON-O-02"), so it returns >8 lines by construction — the *intent* (all 8 dispositioned in §Open questions) is met; the literal whole-file grep was never satisfiable given the pre-existing status rows.

## Next Phase Readiness
- Zero open CON-O-* at the 1.0-rc.1 tag. Wave 4 (06-04, CI-flake fixes) is unblocked.
- Orchestrator note: this wave ran with `workflow.use_worktrees=false` (set this session) and was executed inline rather than via a spawned executor — rationale was the Windows/vcpkg build recipe + PowerShell-only MSBuild + delicate ABI-preserving parser. Future single-plan C++ waves can follow the same pattern.

---
*Phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut*
*Completed: 2026-05-25*
