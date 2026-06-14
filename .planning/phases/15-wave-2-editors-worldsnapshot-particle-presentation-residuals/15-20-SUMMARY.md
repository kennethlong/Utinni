---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 20
subsystem: saving
tags: [loose-override, path-containment, logical-path, stringtable, iff, swg-client, netstandard2.0]

# Dependency graph
requires:
  - phase: 14-mcp-server
    provides: "UtinniCoreDotNet.PathContainment netstandard2.0 single-source lib + [TypeForwardedTo] shim (LooseOverridePath placement this helper mirrors)"
  - phase: 08-iff-editor
    provides: "IffSaveTargets.SaveLooseOverride + LooseOverridePath.Resolve root-containment (the save-side gate this open-side derivation feeds)"
provides:
  - "LogicalAssetPath.TryFromAbsolute — pure BCL-only open-side derivation of an asset's client-root-relative logical subpath (loose-prefix-stripped, separator-normalized, outside-root => false, never throws)"
  - "Raw-Open… loose-override saves across all five Wave-1/2 editors now preserve the logical subpath (loose\\string\\en\\ui_auc.stf) instead of flattening (loose\\ui_auc.stf) — closes 15-SMOKE Checklist D-ii at the code level"
affects: [15-21-reassembly-signoff, future-loose-override-work]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Open-side logical-path derivation as the BCL-only complement to the save-side LooseOverridePath.Resolve gate (Try-pattern, never throws, filename fallback)"

key-files:
  created:
    - "UtinniCoreDotNet.PathContainment/LogicalAssetPath.cs"
    - "UtinniCoreDotNet.Tests/SavingTests/LogicalAssetPathTests.cs"
  modified:
    - "The Jawa Toolbox/TheJawaToolboxDotNet/Saving/IffSaveTargets.cs"
    - "The Jawa Toolbox/TheJawaToolboxDotNet/Saving/StringTableSaveTargets.cs"

key-decisions:
  - "Fixed at the actual flatten site (the two SaveLooseOverride methods) instead of the form Open… handlers the plan named — the loose relAssetPath is re-derived at save time from loose.Path, NOT carried from the LoadDocument 3rd arg, so a form-level edit would have been a no-op"
  - "Two SaveTargets edits cover all five editors: Iff/Datatable/ObjectTemplate/Particle delegate to IffSaveTargets.SaveLooseOverride; StringTable has its own"
  - "No csproj Compile-Include edits needed — both PathContainment and Tests projects are SDK-style globbing"

patterns-established:
  - "LogicalAssetPath (open side) ⟷ LooseOverridePath (save side): defense-in-depth path-containment pair in the same netstandard2.0 lib"

requirements-completed: [RESID-03, PROD-W2-PRT]

# Metrics
duration: ~25min
completed: 2026-06-13
---

# Phase 15 Plan 20: LogicalAssetPath raw-Open loose-override subpath preservation Summary

**Pure BCL-only `LogicalAssetPath.TryFromAbsolute` open-side derivation (12 xUnit facts green) wired into the two `SaveLooseOverride` flatten sites so raw-`Open…` loose overrides land at `loose\string\en\ui_auc.stf` where the SWG client resolves them — not flattened to `loose\ui_auc.stf` — closing 15-SMOKE Checklist D-ii across all five editors.**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-06-13
- **Completed:** 2026-06-13
- **Tasks:** 2
- **Files modified:** 2 created (Utinni), 2 modified (UtinniPlugins)

## Accomplishments
- `LogicalAssetPath.TryFromAbsolute(absolutePath, clientRoot, out logicalRelPath)` — pure, BCL-only (System + System.IO), netstandard2.0, namespace `UtinniCoreDotNet.Saving` (mirrors `LooseOverridePath`). Canonicalizes both inputs, OrdinalIgnoreCase trailing-separator StartsWith gate (sibling-prefix-safe), forward-slash normalize, strips a leading `loose/` segment for round-trip parity, returns `false` (never throws) outside the root / on degraded input.
- 12 xUnit facts (`LogicalAssetPathTests`) — subpath preserved, loose-prefix stripped, file-directly-under-root, outside-root false, sibling-prefix false, root trailing-separator idempotent, case-insensitive root match, forward-slash result, no `..` escape, null/empty safe, root-only false, loose-dir-only false. **12/12 green.**
- Wired into the two loose-override derivation sites (`IffSaveTargets.SaveLooseOverride` L110-116, `StringTableSaveTargets.SaveLooseOverride` L91-97): `relAssetPath = LogicalAssetPath.TryFromAbsolute(loose.Path, resolvedRoot, out logical) ? logical : Path.GetFileName(loose.Path)`. Covers all five editors with two surgical edits.
- Full `UtinniCoreDotNet.Tests` suite: **718/718 passed, 0 failed** (no regression; the known D3D9 harness flake did not trip).
- Both solutions build Release|x86, **MSBuild exit 0** each.

## Task Commits

1. **Task 1: Pure LogicalAssetPath derivation helper + unit coverage** — `8343136` (feat, Utinni repo). TDD: test written first (caught the bare-`loose` root edge case → impl fixed), then impl, 12/12 green.
2. **Task 2: Wire LogicalAssetPath into the editors' loose-override path** — `b13c251` (fix, UtinniPlugins repo).

## Files Created/Modified
- `UtinniCoreDotNet.PathContainment/LogicalAssetPath.cs` (Utinni) — open-side logical-path derivation helper.
- `UtinniCoreDotNet.Tests/SavingTests/LogicalAssetPathTests.cs` (Utinni) — 12 xUnit facts.
- `The Jawa Toolbox/TheJawaToolboxDotNet/Saving/IffSaveTargets.cs` (UtinniPlugins) — loose relAssetPath now derives the logical subpath (used by Iff/Datatable/ObjectTemplate/Particle).
- `The Jawa Toolbox/TheJawaToolboxDotNet/Saving/StringTableSaveTargets.cs` (UtinniPlugins) — same derivation for StringTable's own loose path.

## Editors Wired vs Skipped
All five editors are covered — none skipped — but via the two shared SaveTargets rather than per-form:

| Editor | Loose-override route | Status |
|--------|----------------------|--------|
| IFF (`FormIffEditor`) | `IffSaveTargets.SaveLooseOverride` (direct) | Wired (IffSaveTargets edit) |
| Datatable (`FormDatatableEditor`) | `DatatableSaveTargets.SaveLooseOverride` → `IffSaveTargets.SaveLooseOverride` | Wired (via IffSaveTargets) |
| Object Template (`FormObjectTemplateEditor`) | `ObjectTemplateSaveTargets.SaveLooseOverride` → `IffSaveTargets.SaveLooseOverride` | Wired (via IffSaveTargets) |
| Particle (`FormParticleEditor`) | `ParticleSaveTargets.SaveLooseOverride` → `IffSaveTargets.SaveLooseOverride` | Wired (via IffSaveTargets) |
| String Table (`FormStringTableEditor`) | `StringTableSaveTargets.SaveLooseOverride` (own) | Wired (StringTableSaveTargets edit) |

No editor was skipped as a read-only viewer; all five support a loose-override save and all now derive the logical subpath.

## Decisions Made
- **Fixed at the SaveTargets layer, not the form Open… handlers (plan-named callsite was inaccurate).** See Deviations.
- **No csproj edits.** Both `UtinniCoreDotNet.PathContainment.csproj` and `UtinniCoreDotNet.Tests.csproj` are SDK-style globbing; the new `.cs` files are auto-included. The plan's "explicit `<Compile Include>` if non-globbing" branch did not apply.
- **Namespace `UtinniCoreDotNet.Saving`** (not `UtinniCoreDotNet.PathContainment`) so net472 callers in TJT resolve the type with no `using` change — exactly the convention `LooseOverridePath` follows.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Wrong callsite in plan] Fix applied at the loose-override derivation site, not the form Open… handler**
- **Found during:** Task 2 (read_first tracing of how the LoadDocument 3rd arg threads to relAssetPath)
- **Issue:** The plan asserted the flatten happens at the form's raw-`Open…` handler (`FormStringTableEditor.cs:341`, the `Path.GetFileName(path)` 3rd `LoadDocument` arg) and listed the five `Form*.cs` files as the edit targets. In reality the loose `relAssetPath` is NOT carried from that 3rd arg — it is re-derived at SAVE time from `loose.Path` inside `IffSaveTargets.SaveLooseOverride` (L115) and `StringTableSaveTargets.SaveLooseOverride` (L92). Editing the form Open… handlers would have been a no-op for the loose-override target. (The form's 3rd arg / `Path.GetFileName(path)` feeds only the DISPLAY name + Save-As default filename, which the plan says to keep.)
- **Fix:** Applied `LogicalAssetPath.TryFromAbsolute(loose.Path, resolvedRoot, out logical) ? logical : Path.GetFileName(loose.Path)` at the two `SaveLooseOverride` derivation sites, where both `resolvedRoot` (the client root, resolved the same way the save uses) and `loose.Path` (the absolute on-disk path) are already in scope. This honors the plan's `key_links` intent verbatim ("the loose-override save relAssetPath … via `LogicalAssetPath.TryFromAbsolute` instead of `Path.GetFileName(path)`") and fixes all five editors with two edits instead of five.
- **Files modified:** `IffSaveTargets.cs`, `StringTableSaveTargets.cs` (UtinniPlugins) — instead of the five `Form*.cs`.
- **Verification:** `TheJawaToolbox.sln` Release|x86 MSBuild exit 0; the `tre.LogicalPath` (TRE-Browser hand-off) branch and all form display-name `Path.GetFileName` callsites confirmed unchanged.
- **Committed in:** `b13c251` (Task 2 commit)

**2. [Rule 1 - Bug caught by RED test] Bare `<root>\loose` directory path leaked as a logical path**
- **Found during:** Task 1 (TDD RED → first test run: 11/12, `TryFromAbsolute_LooseRootDirectoryOnly_ReturnsFalse` failed)
- **Issue:** The first implementation only stripped a `loose/` *prefix* (with trailing slash). A path that is exactly `<root>\loose` (remainder `"loose"`, no trailing content) fell through and returned `"loose"` as a logical asset path, which is not an asset.
- **Fix:** Added an explicit `remainder == "loose"` (OrdinalIgnoreCase) → `return false` guard before the prefix strip.
- **Files modified:** `LogicalAssetPath.cs`
- **Verification:** Rebuild + re-run → 12/12 green.
- **Committed in:** `8343136` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (1 plan-callsite correction, 1 RED-caught edge-case bug)
**Impact on plan:** The callsite correction is the load-bearing finding — the literal plan edit would not have closed D-ii. The chosen site is strictly more correct (covers all editors, is where the data actually lives) and leaves the TRE-Browser hand-off and display names untouched as the plan required. No scope creep; `files_modified` differs from the plan (SaveTargets instead of Forms) for the documented reason.

## Issues Encountered
- Git-bash mangles MSBuild `/p:` switches + nested `cmd /c` quoting (15-19 trap). Worked around by writing the exact verify command to a temp `.bat` and invoking via `cmd //c` with an absolute path, then deleting the `.bat`. Both builds exited 0.
- `Generated/UtinniCore.cs` churned (CppSharp reorder, 5864/5723 symmetric no-op) after the Task-1 build; reverted via `git checkout --` per the standing rule. Confirmed clean at commit time; the TJT build did not re-churn it.

## User Setup Required
None - no external service configuration required.

## Threat Model Status
- **T-15-20-01 (lost override / flatten):** mitigated — the logical subpath is now derived + preserved at every loose-override save site.
- **T-15-20-02 (`..` traversal):** mitigated — `LogicalAssetPath` emits a canonicalized remainder under the root (no `..` possible; covered by `TryFromAbsolute_DerivedPathNeverContainsDotDot`), and the downstream `LooseOverridePath.Resolve` independently rejects `..`/rooted/escape (defense in depth, unchanged).
- No new threat surface introduced (one pure helper + two in-scope derivation edits).

## Next Phase Readiness
- Code-level D-ii closure is complete and both solutions build green. **The LIVE on-disk re-verify is gated to 15-21** (open `string\en\ui_auc.stf` via raw `Open…`, edit, Save as loose override → confirm it lands at `D:\SWGEmu-Client\SWGEmu\loose\string\en\ui_auc.stf`, not flat). Not attempted here per plan scope.
- Both commits are on `master` in their respective repos (`8343136` Utinni, `b13c251` UtinniPlugins).

## Self-Check: PASSED
- FOUND: `UtinniCoreDotNet.PathContainment/LogicalAssetPath.cs`
- FOUND: `UtinniCoreDotNet.Tests/SavingTests/LogicalAssetPathTests.cs`
- FOUND: `The Jawa Toolbox/TheJawaToolboxDotNet/Saving/IffSaveTargets.cs`
- FOUND: `The Jawa Toolbox/TheJawaToolboxDotNet/Saving/StringTableSaveTargets.cs`
- FOUND: commit `8343136` (Utinni, Task 1)
- FOUND: commit `b13c251` (UtinniPlugins, Task 2)

---
*Phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals*
*Completed: 2026-06-13*
