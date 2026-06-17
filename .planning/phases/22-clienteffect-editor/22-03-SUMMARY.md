---
phase: 22-clienteffect-editor
plan: 03
subsystem: terrain-cli-save
tags: [terrain, cli, loose-override, save-matrix, path-containment]
requires:
  - "LooseOverridePath.Resolve (two-step compose, fail-closed containment)"
  - "TerrainSaveTargets.SaveLooseOverride convention (editor side, shipped 21-06 R2)"
provides:
  - "apply-save-trn lands a CLI terrain override under <root>/loose/<relAsset> (D-10 matrix consistency)"
  - "ApplySaveTrnOptions.--loose-subdir (default \"loose\", own copy per REVIEWS HIGH #3)"
  - "xunit.runner.json methodDisplay=method (short-name filters resolve in Utinni.Cli.Tests)"
affects:
  - "phase21-terrain-override-loose-subdir todo closed (pending -> completed)"
tech-stack:
  added: []
  patterns:
    - "Two-step loose-override compose mirrored per-verb (apply-save-trn + apply-save-effect each own their --loose-subdir)"
key-files:
  created:
    - "Utinni.Cli.Tests/xunit.runner.json"
    - ".planning/todos/completed/phase21-terrain-override-loose-subdir.md"
  modified:
    - "Utinni.Cli/Commands/ApplySaveTrnCommand.cs"
    - "Utinni.Cli.Tests/Terrain/ApplySaveTrnTests.cs"
    - "Utinni.Cli.Tests/Utinni.Cli.Tests.csproj"
decisions:
  - "Followed the actual repo todo dir name `.planning/todos/completed/` (the plan said `done/`, which does not exist) — git mv into the established directory."
  - "Added xunit.runner.json (methodDisplay=method) to make short-name test filtering work; `Name~` is not a supported VSTest filter property for the xUnit adapter, so the 22-VALIDATION `Name~` token must read `DisplayName~`."
metrics:
  duration: ~25 min
  completed: 2026-06-17
---

# Phase 22 Plan 03: apply-save-trn loose-subdir parity Summary

Closed the residual CLI half of the Phase-21 terrain `loose/`-subdir fix: `apply-save-trn` now composes
its loose-override destination as `<root>/loose/<relAsset>` (the documented searchPath every editor +
`apply-save-effect` target), via its own optional `--loose-subdir` flag (default `"loose"`) and a
two-step fail-closed `LooseOverridePath.Resolve`, keeping the whole loose-override save matrix consistent
(D-10). The folded `phase21-terrain-override-loose-subdir` todo is closed.

## What Shipped

- **`ApplySaveTrnCommand.cs`** — added `[Option("loose-subdir", Default = "loose")] LooseSubDir` to
  `ApplySaveTrnOptions`; replaced the single-step `Resolve(o.Root, o.RelAsset)` with the two-step compose
  (`overrideBase = Resolve(Root, LooseSubDir)` then `Resolve(overrideBase, RelAsset)`) when `LooseSubDir`
  is non-empty, else the legacy single-step. Both legs stay inside the existing `catch (ArgumentException)`
  → exit 2 `PathContainment`, so a traversal in EITHER `--loose-subdir` or `relAsset` fails closed with no
  write (T-22-path-trn). The verify / atomic-write logic is unchanged.
- **`ApplySaveTrnTests.cs`** — `WriteLooseAsset` now seeds the asset under `<_work>/loose/<rel>` (the new
  default resolved destination), so every existing positive edit still resolves. Added
  `ApplySaveTrn_TerrainLooseSubdir_LandsUnderLooseDir` (mirrors `TerrainLooseOverridePathTests`): asserts
  the written `result.path` contains the `loose` + separator segment and starts under
  `Path.GetFullPath(<root>/loose)`. `ApplySave_PathOutsideRoot_FailClosed_NoWrite` stays green through the
  compose (`../escape.trn` still exits 2).
- **`xunit.runner.json` + csproj `<None>` copy** — `methodDisplay: method` so VSTest short-name filters
  match the test method name (the 22-VALIDATION filter scheme). See the deviation below re: `Name~`.
- **Todo closed** — `git mv` pending → `completed/`, `status: DONE`, `closed:` date, and a `## Closed`
  section recording all three close-items (editor 21-06 R2; framework `TerrainLooseOverridePathTests`;
  CLI half this plan) and the REVIEWS HIGH #3 per-verb self-containment note.

## Verification

- `dotnet test Utinni.Cli.Tests --no-build -c Release --filter "FullyQualifiedName~ApplySaveTrn"` →
  **11 passed, 0 failed**.
- `dotnet test --no-build --filter "DisplayName~TerrainLooseSubdir"` → **1 passed** (the new test).
- Full `Utinni.Cli.Tests` suite → **414 passed, 2 skipped (pre-existing fixture-gated), 0 failed** — the
  `methodDisplay` change caused no regression.
- `git status` shows `Generated/UtinniCore.cs` unmodified (restored after build per CppSharp churn rule).

## Deviations from Plan

### [Rule 3 - Blocking] Todo dir is `completed/`, not `done/`
- **Found during:** Task 1 (closing the folded todo).
- **Issue:** The plan instructs moving the todo to `.planning/todos/done/`, but the repo convention is
  `.planning/todos/completed/` (`done/` does not exist; `completed/` holds the other 4 closed todos).
- **Fix:** `git mv` into the established `completed/` directory.
- **Files:** `.planning/todos/completed/phase21-terrain-override-loose-subdir.md`.
- **Commit:** 132bae2.

### [Rule 3 - Blocking] `Name~` is not a supported VSTest filter property for the xUnit adapter
- **Found during:** Task 1 verification (acceptance criterion #4: `--filter "Name~TerrainLooseSubdir"`
  must resolve to ≥1 test).
- **Issue:** The xUnit VSTest adapter (xunit.runner.visualstudio 3.x) does NOT expose a `Name` filter
  property; `Name~`/`Name=` silently match zero tests for EVERY test in `Utinni.Cli.Tests` (verified
  against existing methods too), not just the new one. The 22-VALIDATION matrix uses `Name~` on every row
  — this is a systemic VALIDATION-doc authoring error, not specific to this plan. The supported equivalents
  are `DisplayName~` and `FullyQualifiedName~`.
- **Fix:** Added `xunit.runner.json` with `methodDisplay: method` so each test's `DisplayName` is its short
  method name — the closest possible enablement of the VALIDATION intent (short-name filtering now works
  via `DisplayName~TerrainLooseSubdir`, which resolves to exactly the new test and passes). The test is
  named with the required `TerrainLooseSubdir` token.
- **Files:** `Utinni.Cli.Tests/xunit.runner.json`, `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj`.
- **Commit:** 132bae2.
- **Follow-up for the verifier / future plans:** 22-VALIDATION rows that say `--filter "Name~X"` should be
  read/run as `--filter "DisplayName~X"`. (Affects 22-01/02/04 rows identically.)

### [Plan deviation] todo frontmatter `status: DONE`
- The other closed todos in `completed/` keep `status: OPEN` + a resolution section. This plan's acceptance
  artifact explicitly requires `contains: "status: DONE"`, so the closed file uses `status: DONE`
  (plan requirement takes precedence over the prior loose convention).

## Known Stubs

None.

## Self-Check: PASSED

- FOUND: `Utinni.Cli/Commands/ApplySaveTrnCommand.cs`
- FOUND: `Utinni.Cli.Tests/xunit.runner.json`
- FOUND: `.planning/todos/completed/phase21-terrain-override-loose-subdir.md`
- FOUND commit: 132bae2 (`git log --oneline` confirms)
- pending todo: GONE (correct); completed todo has `status: DONE`
- `Generated/UtinniCore.cs`: unmodified
