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
  → an id-less object; nothing in its parent chain carries a NetworkId).
- **Evidence caveat (maintainer review, same evening):** the ray and the hud pick are two
  different collision paths — "both resolved the same chair" was an INFERENCE in this run,
  not a measurement (rayId=0 proves the ray hit *an* id-less object at that pixel, not that
  the hud-picked object itself is id-less). Probe upgraded (`b93ebff`): it now reads the
  picked object's OWN NetworkId directly (`getNetworkIdValue`, the inspector getter — .ilf
  objects never get one assigned), logging `hudPickId=` per pick and `ownId=` per nudge.
  The next run turns the inference into a direct measurement; the manipulation half of the
  PASS (the latched pointer moved the object) stands regardless.
- **DIRECT MEASUREMENT OBTAINED (2026-07-19 20:47, upgraded probe):** re-hovering the chair:
  ```
  20:47:24  hudPick=0x52965310 hudPickId=0 rayResult=1 rayId=1082878 rayObj=0x48CC4E60
            -> DIVERGENCE (picked object is ID-LESS -- .ilf class, measured directly)
  ```
  The picked object's OWN id is 0 (three hovers, consistent) while the ray at the same pixel
  resolves to a DIFFERENT networked object (rayObj non-null, ids 1082877/1082878 — the
  server-streamed surroundings, exactly the synthesis's "the ray walks up to the networked
  building; the hud pick keeps the chair"). Three further nudges moved `ownId=0`. The PASS
  is now fully measured, no inference left. FREE RIDER also banked: this session's chair
  pointer is 0x52965310 vs the first run's 0x52BE5CD0 — different session, different
  pointer — the no-session-stable-handle datum that keeps id-minting rejected.
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
