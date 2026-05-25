---
phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut
plan: 05
subsystem: infra
tags: [clang-format, vcpkg, msbuild, directory-build-props, xunit, preservation-audit, cross-repo, icon, binary-compat]

requires:
  - phase: 06-04
    provides: CI-green master baseline (loader-lock + GameCallbacks flake fixes) that the full-repo reformat rebases onto cleanly
provides:
  - Repo-wide .clang-format (Allman/4-space, left pointers) + .git-blame-ignore-revs + CI clang-format-check style gate
  - Neutral framework icon (utinni.ico); TJT.ico/TJT.png ejected to kennethlong/UtinniPlugins (paired commit)
  - D-16 polish bundle: Directory.Build.props SDK unification, Prefer32Bit removal, ExampleEditorPlugin Release path, .gitignore Std/StdEdited doc, empty namespace Std removal, Native.SendMessage IntPtr overload + binary-compat shim, licenses.txt (DetourXS/nvapi + UTF-8 João), typo fixes
  - Dead-code purge (Launcher VS-attach machinery, render_world/client_world/io_win dead hooks, particle.cpp+scene.cpp stubs) with swg/ui carve-out
  - STAB-04 preservation audit (06-AUDIT.md) + 23 fail-on-violation xUnit grep Facts in UtinniCoreDotNet.Tests/PreservationAudit/
affects: [06-06, future plugin authors, any cross-binary plugin DLL]

tech-stack:
  added: [".clang-format (clang-format 20.1.8)", "Directory.Build.props (root)", "UtinniCoreDotNet.Tests/PreservationAudit (xUnit grep harness)"]
  patterns: ["repo-root-anchored source grep tests (RepoRoot.cs)", "binary-compat overload shim for public API signature changes", "CI fail-fast style gate before build"]

key-files:
  created:
    - .clang-format
    - .git-blame-ignore-revs
    - Directory.Build.props
    - UtinniCoreDotNet/Resources/utinni.ico
    - UtinniCoreDotNet.Tests/PreservationAudit/PreservationAuditTests.cs
    - UtinniCoreDotNet.Tests/PreservationAudit/RepoRoot.cs
    - .planning/phases/06-.../06-AUDIT.md
    - .planning/phases/06-.../06-05-TJT-ICO-CROSS-REPO-NOTES.md
  modified:
    - 169 C++ files (clang-format), 10 .vcxproj (WindowsTargetPlatformVersion strip)
    - UtinniCoreDotNet/Utility/Native.cs, UI/Forms/UtinniForm.cs, Properties/Resources.resx + Resources.Designer.cs, UtinniCoreDotNet.csproj
    - Launcher/main.cpp, UtinniCore/swg/scene/{render_world,client_world}.cpp, UtinniCore/swg/misc/io_win.cpp
    - licenses.txt, .gitignore, docs/ai/assessment.md, .planning/codebase/CONVENTIONS.md

key-decisions:
  - ".clang-format adds PointerAlignment:Left (plan omitted it; LLVM default Right would flip all T* x to T *x against CONVENTIONS.md)"
  - "LineEnding left unpinned (DeriveLF) so the CI clang-format dry-run is EOL-agnostic across CRLF working trees and LF checkouts"
  - "TJT.png ejected alongside TJT.ico for clean framework/plugin separation (was unused in code)"
  - "CON-M-08 audited as evolved: SetHwnd-on-layout superseded by Issue #9/#10 reparent; foundation intact, test checks ReparentSwgWindow"
  - "CON-T-02 audit scoped to the 5 core projects + C++ example; test-only fixtures (CrtMatchPlugin/LegacyPlugin/LoaderLockHarness) and the tokenised template are out of scope"

patterns-established:
  - "RepoRoot.cs: walk up to Utinni.sln, grep live source (build dirs + external excluded) — works identically locally and on the self-hosted runner"
  - "Public API signature change ships an int-overload binary-compat shim per the caller-attrs rule"

requirements-completed: [STAB-03, STAB-04]

duration: ~1h 45m
completed: 2026-05-25
---

# Phase 6 Plan 05: STAB-03 cleanup sweep + STAB-04 preservation audit Summary

**Full-repo clang-format + a CI style gate, TJT.ico ejected cross-repo behind a neutral framework icon, the eight-item D-16 build/polish bundle, a dead-code purge (with the swg/ui carve-out), and a 23-Fact fail-on-violation preservation audit of every load-bearing foundation — all CI-green on master.**

## Performance

- **Duration:** ~1h 45m (incl. reconnaissance, 4 local Release x86 builds, 4 CI cycles)
- **Started:** 2026-05-25 (~14:00 CDT)
- **Completed:** 2026-05-25T20:44Z
- **Tasks:** 4
- **Files modified:** 202 files (4871 insertions / 4063 deletions across the plan) + 1 paired UtinniPlugins commit

## Accomplishments

- **Task 1 — style codified:** authored `.clang-format` (Allman, 4-space, left pointers, `SortIncludes:false`), reformatted 168 C++ files in one commit, recorded the SHA in `.git-blame-ignore-revs`, and added a fail-fast CI `clang-format check` step (resolves clang-format from the VS LLVM component via vswhere; EOL-agnostic).
- **Task 2 — framework de-branding + polish:** replaced the leaked TJT default icon with a neutral gear `utinni.ico`; ejected `TJT.ico`+`TJT.png` to `kennethlong/UtinniPlugins/TheJawaToolbox` (paired commit `c9cfa9d`, with `FormObjectBrowser` now loading its own icon). Shipped all eight D-16 items including `Native.SendMessage`'s IntPtr overload + int binary-compat shim.
- **Task 3 — dead-code purge:** removed the commented EnvDTE VS-auto-attach machinery from Launcher, the never-installed `hkRender`/`hkClearVisibleCells`/`hkInternalCollide`/`hkDraw` hooks, and the empty `particle.cpp`/`scene.cpp` stubs (+ their ClCompile entries). The `swg/ui/` commented detours were left intact (carve-out) and recorded as "deferred pending post-overlay-fix audit".
- **Task 4 — STAB-04 audit:** `06-AUDIT.md` documents all 23 foundations (CON-N/M/T) with PASS dispositions; 23 xUnit Facts grep the live source and fail the build on violation. A deliberate-violation probe (hiding `imgui_impl.cpp`) made `CON_N_06` fail with its exact message, proving fail-on-violation; reverted, not merged.

## Task Commits

20 atomic commits on `master` (oldest → newest):

**Task 1 (style):** `47b1f1d` style · `5f98e89` chore (blame-ignore) · `2b773ae` ci (clang-format check)
**Task 2 (icon + D-16):** `bfbf7fc` feat (SendMessage IntPtr+shim) · `a3093be` feat (TJT.ico→utinni.ico) · `e319f29` build (Directory.Build.props) · `04ee2b7` build (Prefer32Bit) · `f60e149` build (ExampleEditorPlugin path) · `231b6e6` docs (.gitignore) · `2972e34` chore (namespace Std) · `e0667de` docs (licenses.txt) · `bbffb1e` chore (typos) · `ede7b59` docs (cross-repo notes)
**Task 3 (dead code):** `7299a78` · `7aec540` · `fcadd5b` · `4b62d9d` · `ff9a8ec` (chore) · `a3e3bd3` docs (status tracking)
**Task 4 (audit):** `0f03e5f` test (STAB-04 audit + 23 grep Facts)

**Cross-repo:** UtinniPlugins `c9cfa9d` (paired with `a3093be`).

CI green on `master` after each task's push (runs 264170997, 26417839694, 26418304577, 26418717467 — all `success`).

## Decisions Made

See frontmatter `key-decisions`. Most consequential: adding `PointerAlignment:Left` to `.clang-format` (the plan's spec would otherwise have flipped every pointer declaration), and auditing `CON-M-08` as *evolved* rather than verbatim-intact.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] `.clang-format` PointerAlignment**
- Found during: Task 1. The plan's config list omitted `PointerAlignment`; LLVM's default (`Right`) would rewrite every `const char* x` → `const char *x`, violating CONVENTIONS.md. Added `PointerAlignment:Left` + `DerivePointerAlignment:false`. Verified the dry-run diff preserves left alignment.

**2. [Rule 1 - Plan inaccuracy] File-path corrections**
- `StdEdited.cs` is at `UtinniCoreDotNet/Generated/` (plan said `UtinniCoreDotNetGen/Generated/`); `IUndoCommand.cs` is at `UtinniCoreDotNet/UndoRedo/` (plan referenced `Plugins/Commands/`). Used the real paths.

**3. [Rule 1 - No-op] utini.cpp commented detours (D-14 item 2)**
- Already removed by 06-03's LeksysINI rewrite; recorded as a no-op, no commit.

**4. [Rule 1 - Scope correction] detatch rename**
- Plan cited `utinni.cpp:132`; actual = def at `utinni.cpp:259` + DLL_PROCESS_DETACH call at `:322` + two doc-comment refs (`clr.cpp`, `directx9.cpp`). Renamed all four. The commented `ui_base_object.h:46 detatch` is a different method in the swg/ui carve-out — left untouched.

**5. [Rule 2 - Cleanliness] TJT.png ejected too**
- The plan targeted TJT.ico; TJT.png (the unused `TJT1` resource) was also ejected to complete the framework/plugin separation.

**6. [Rule 2 - Honest audit] CON-M-08 evolved**
- The original `SetHwnd`-on-every-layout was superseded by owned-popup reparenting (Issue #9/#10). The foundation persists; the audit records it as evolved and the Fact checks the current `ReparentSwgWindow` mechanism rather than the removed call.

**7. [Rule 1 - Audit scope] CON-T-02 triple-config**
- Initial test checked every .vcxproj and flagged the Phase-3/6 test fixtures (CrtMatchPlugin, LegacyPlugin, LoaderLockHarness — Debug+Release only by design) and the tokenised C++ template. Narrowed to the five core projects + the C++ example, which CON-T-02 actually governs. Documented in 06-AUDIT.md.

**8. [Rule 1 - Count] 23 vs 24 grep tests**
- There are 23 distinct CON foundations (constraints.md); the assessment's "24" double-counts the *Impl promotion. Shipped 23 Facts (24 audit subsections incl. the cross-repo dispositions section).

---

**Total deviations:** 8 (2 missing-critical, 6 plan-inaccuracy/scope). **Impact:** all necessary for correctness or honest auditing; no scope creep beyond the TJT.png ejection (which strengthens D-15's intent).

## Issues Encountered

- **CppSharp `Generated/UtinniCore.cs` reorder-noise:** each Release build regenerated this file with a different member ordering (5674 ins == 5674 del). Verified pure reordering (identical 631-EntryPoint set + identical sorted-line content) and reverted it before each commit so the cosmetic/dead-code commits stay clean.
- **licenses.txt encoding:** the "João" mojibake was an encoding *mismatch* (file was Latin-1, `0xE3`=ã, invalid as UTF-8) not corruption. Converted the file to UTF-8 (no BOM) so grep/GitHub render it correctly.
- **Session toolchain hunt (resolved):** `gh`/`gsd-sdk` live in `C:\Users\kenne\bin`, which was on git-bash's PATH but not the Windows user PATH; PowerShell couldn't see `gh`. Added the dir to the persistent user PATH and recorded all toolchain paths in memory for future sessions.

## Regression Probe (per [[feedback-max-harness]])

Hid `UtinniCore/swg/ui/imgui_impl.cpp` on a scratch basis and re-ran `CON_N_06_ImguiIsSetupGuard_Present` with `--no-build` (the Facts grep source at runtime): the Fact **FAILED** with `CON-N-06 violated: imgui_impl.cpp must keep the isSetup device-loss guard`, then the file was restored (working tree clean). Confirms the audit Facts are real CI gates, not warn-only.

## User Setup Required

None.

## Next Phase Readiness

- Plan 06-05 complete and CI-green; this was the **wave-5 filter** of `/gsd:execute-phase 6 --wave 5`. **Plan 06-06 (TEST-04 Tier-4 doc + WiX MSI installer + release.yml + v1.0.0-rc.1 tag) remains** — phase-level verification is intentionally deferred until 06-06 lands.
- The new CI `clang-format check` gate means any future C++ commit that drifts from `.clang-format` fails fast — keep edited C++ files formatted (run `clang-format -i`) before pushing.
- `Native.SendMessage` now has both an IntPtr primary and an int shim; new managed code should prefer the IntPtr overload.

## Self-Check: PASSED

- All 20 production commits + cross-repo paired commit present on master; key files exist on disk (verified `.clang-format`, `.git-blame-ignore-revs`, `Directory.Build.props`, `utinni.ico`, `06-AUDIT.md`, `PreservationAuditTests.cs`, `RepoRoot.cs`).
- All task acceptance criteria re-run and pass (TJT.ico zero in UtinniCoreDotNet; both SendMessage overloads; zero WindowsTargetPlatformVersion in vcxprojs; 23 [Fact]s; 34 CON matches in 06-AUDIT.md; STAB-04 phrase in assessment.md).
- Local Release x86 build green ×3; `dotnet test` PreservationAudit 23/23 pass; regression probe failed-then-reverted as designed.
- CI green on master for all four task pushes.

---
*Phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut*
*Completed: 2026-05-25*
