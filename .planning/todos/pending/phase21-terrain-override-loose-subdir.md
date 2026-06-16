---
title: Terrain override save omits the loose/ subdir (inconsistent with all other editors)
area: terrain-save
status: OPEN
opened: 2026-06-16
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
