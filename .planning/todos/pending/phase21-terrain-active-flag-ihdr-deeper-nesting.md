---
title: Terrain active-flag edit — IHDR DATA deeper version-form nesting
area: terrain-codec
status: OPEN
opened: 2026-06-16
source: 21-SMOKE-LOG.md (R1)
owner: maintainer (needs the fix + a re-validation, live or via a real-shape IHDR fixture)
severity: medium (typed-field editing works; active-flag toggle does not on real terrain)
---

# Terrain active-flag edit fails on real terrain — IHDR DATA is one version-form deeper

## What's open

On real high-era terrain (observed: naboo.trn, FORM PTAT/0014) the layer-item-header DATA leaf is nested:

```
LAYR → <layer-version FORM> → IHDR → <IHDR-version FORM> → DATA
```

The Phase-21 live-smoke fix added the `LAYR → layer-version` descent (in `TgenDecoder.DecodeLayer` and
`TerrainSaveTargets.ResolveIhdrLeafStableId`), which fixed the **typed-field** path (validated live). But the
**active-flag** path still fails with `LAYR FORM '...' has no IHDR DATA child leaf (cannot address active
flag)` because the `IHDR → IHDR-version → DATA` step is still not handled in EITHER:

- `TgenDecoder.ReadLayerItemHeader` — does `FindContainerChild(walkRoot, "IHDR")` then
  `EnumerateLeaves(ihdr, "DATA")` (direct DATA child). On real data the DATA is under an IHDR version form,
  so the read finds no DATA and the layer's `active` falls back to the C++ default `true` (i.e. the editor's
  active checkbox shows the DEFAULT, not the real flag).
- `TerrainSaveTargets.ResolveIhdrLeafStableId` — walks IHDR's direct children for DATA; same miss → throws.

## To close

1. Add a fixture (extend `TgenFixtureSynthesizer.WithRealLayrWrapper`) that models `IHDR → version → DATA`
   (the new fixture's IHDR currently has DATA as a direct child — it does NOT reproduce this bug). A test
   that decodes it and asserts the active flag reads + the IHDR leaf resolves should FAIL first (red).
2. Make `ReadLayerItemHeader` descend `IHDR → first-container-child (version) → DATA` (with the
   direct-DATA case as a fallback for the collapsed shape).
3. Make `ResolveIhdrLeafStableId` mirror that descent so read↔write hit the SAME leaf (concern #10).
4. Re-validate: active-flag toggle stages + saves byte-exact on the real-shape fixture (and ideally live).
