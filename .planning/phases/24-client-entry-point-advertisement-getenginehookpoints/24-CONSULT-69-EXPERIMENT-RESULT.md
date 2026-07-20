# 2026-07-19 — CONSULT-69 decisive experiment: **PASS**

**Run:** 2026-07-19 19:36 local, advertised v21 exe, hybrid session, Mos Eisley cantina
interior (a SERVER-streamed building — the .ilf decorations inside are the exact target
class). Consumer probe = Utinni `c2cf79d` + UtinniPlugins `641de8d`.

## Evidence (utinni.log)

```
19:36:48  ilfProbe: ARMED (hover objects in-world; watch utinni.log)
19:36:54  ilfProbe: hudPick=0x52BE5CD0 rayResult=1 rayId=0 rayObj=0x00000000
          -> DIVERGENCE (pointer path reached an object the id path can't)
19:37:05  ilfProbeNudge: moved 0x52BE5CD0 +0.25 (parent-space Y)
```

- Hovering a cantina chair with `setAllowTargetAnything(true)`: the hud's pointer-keyed
  selection watcher (`cuiHud::getTarget` → `m_lastSelectedObject`) held a stable non-null
  `Object*` while the id-keyed ray at the same cursor pixel returned **id 0** (objectsOnly=1
  → an id-less object; nothing in its parent chain carries a NetworkId). The predicted
  divergence, measured.
- The latched pointer driven through the advertised `object::move_p` row: **the chair
  visibly moved in-world** (maintainer-confirmed). Pointer-keyed manipulation works.

## Verdict per the synthesis

**Selection + manipulation of id-less .ilf interior decorations ship on v21 as-is — zero
engine changes.** The crew's unanimous prediction (pointer-keyed pick reaches .ilf; only the
id-keyed half dead-ends) is confirmed on the first run. Id-minting stays rejected (nothing
here needed an id).

## Still open

- **Free-rider measurement (not yet run):** leave draw range, return, re-hover the same
  chair — the pointer must differ (the measured no-session-stable-handle proof). Low
  priority; the main result stands without it.
- **Control case (not yet run):** hover a networked outdoor object → expect a `SAME`
  verdict line.
- **The deciding product question (Kenny + maintainer):** when you move a chair in one
  building, must the identical building across the street stay unchanged? → shapes the
  persistence freeze request ((C) overlay / (B) materialize / (D) template-derive+rebind
  per-instance vs (A) per-template editor).
- The probe (native + MiscPanel buttons) stays in-tree until the real .ilf gizmo path
  ships, then strips (marked CONSULT-69).

## Next provider wave (when the product question is answered)

Per the synthesis: the spawn-seam registry keyed `(building NetworkId, cellName,
rowIndex-in-cell)` + the model-specific rows — the one small engine addition every
persistence path needs. No rows required for selection/manipulation itself.
