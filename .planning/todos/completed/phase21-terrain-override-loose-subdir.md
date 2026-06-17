---
title: Terrain override save omits the loose/ subdir (inconsistent with all other editors)
area: terrain-save
status: DONE
opened: 2026-06-16
closed: 2026-06-17
source: 21-SMOKE-LOG.md (R2)
owner: maintainer
severity: medium (terrain overrides land off the documented loose searchPath → not picked up by the client)
---

# Terrain loose-override save path skips the `loose/` subdir

## What's open

`TerrainSaveTargets.SaveLooseOverride` resolves the destination as
`LooseOverridePath.Resolve(resolvedRoot, relAssetPath)` with **no `looseOverrideSubDir`** — so a terrain
override for `terrain/naboo.trn` lands at `<root>\terrain\naboo.trn`, NOT `<root>\loose\terrain\naboo.trn`
like every other editor (IFF / Datatable / STF / OT all go through
`LooseOverridePath.Resolve(resolvedRoot, looseOverrideSubDir)` → `<root>\loose\<asset>`).

Consequence: the documented loose-`searchPath` convention (`<root>\loose`, the same one the phantom-walk
mitigation toggles) does NOT cover terrain overrides. In the 21-04 smoke the file had to be relocated from
`<root>\terrain\` to `<root>\loose\terrain\` by hand for the client to even search it.

## To close

- Thread a `looseOverrideSubDir` (default `"loose"`, matching `IffSaveTargets`) through
  `TerrainSaveTargets.SaveLooseOverride` so terrain overrides land at `<root>\loose\<logical>`.
- Keep byte-parity with `apply-save-trn` (verify the CLI uses the same subdir convention; align if not).
- Add/adjust a test asserting the resolved destination is under `<root>\loose\`.

## Closed (2026-06-17, Phase 22 Plan 03)

All three close-items satisfied:

1. **Editor side** — shipped in 21-06 R2: `TerrainSaveTargets.SaveLooseOverride` threads
   `looseOverrideSubDir` (default `"loose"`); empty preserves the legacy `<root>/<logical>`, else the
   two-step compose `overrideBase = Resolve(root, "loose")` → `Resolve(overrideBase, relAsset)` lands the
   override at `<root>/loose/<logical>`.
2. **Framework test** — `UtinniCoreDotNet.Tests/SavingTests/TerrainLooseOverridePathTests.cs` already pins
   the `\loose\` segment + the traversal-escape reject at the framework layer.
3. **CLI half** — aligned in Phase 22 (Plan 03): `apply-save-trn` gained an optional `--loose-subdir`
   (default `"loose"`) and now composes the destination with the SAME two-step
   `LooseOverridePath.Resolve` (both legs fail-closed), so a CLI-written terrain override lands at
   `<root>/loose/<relAsset>` — consistent with the editor. The new test
   `ApplySaveTrn_TerrainLooseSubdir_LandsUnderLooseDir` (filter token `TerrainLooseSubdir`) asserts the
   `<root>/loose/` destination; `ApplySave_PathOutsideRoot_FailClosed_NoWrite` stays green through the
   compose.

Convention is self-contained PER VERB (REVIEWS HIGH #3): `apply-save-effect` (Plan 02) defines its OWN
`--loose-subdir` in its own command — the two verbs share the convention by mirroring the same compose,
not by a cross-plan dependency. The whole loose-override save matrix is now consistent (D-10): editor,
`apply-save-trn`, and `apply-save-effect` all land under `<root>/loose/`.
