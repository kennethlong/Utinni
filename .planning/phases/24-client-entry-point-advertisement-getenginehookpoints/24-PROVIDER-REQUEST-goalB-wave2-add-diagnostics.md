# Provider Request — Goal B Wave 2: refusal-reason diagnostics for the mutation shims

**Status:** REQUEST (small; no contract change, no version bump — internal logging only) · 2026-07-18
**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Baseline:** contract **v18 / 133 names**
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-18-utinni-goalB-wave2-add-diagnostics-REQUEST.md`;
the Utinni copy governs.

## 1. The live repro (Wave-2 mutation smoke, 2026-07-18)

`utinni_wsAddObject` refuses EVERY interactive add on the staged v18 exe, and fails closed with no
diagnostic anywhere, so we cannot tell which pre-check fires. Consumer-side instrumentation proves
the inputs cross the boundary intact:

```
[11:41:47 local / 16:41:47Z] WorldSnapshotLive::addObject('object/tangible/furniture/cheap/shared_armoire_s01.iff',
                              pos=(0.0, 20.3, 0.0), container=0) -> id=0   (x3 clicks, same result)
```

- Scene: EDITOR naboo load (`game::loadScene`, avatar at the spawn point) — note the position is
  X/Z≈0 by legitimate spawn placement, magnitudeSquared ≈ 412 > sqr(ms_closeToOriginDistance)=100,
  so by our reading of `85877bae4` the origin guard passes (barely — flag if our reading is wrong).
- Also refused with `object/static/structure/general/shared_planter_generic_style_4.iff` → not
  template-specific. Both templates verified present in the client TREs (armoire in
  `swgsource_3.0.tre`, planter enumerable in the loaded naboo snapshot itself).
- Allocator band verified offline: naboo max authored id = 9,895,360 (0x96FDC0) < default ceiling
  0x1000000, so the seed has ~6.8M free candidates.
- `SwgClient_report.log` shows NO warning at the click timestamps (`instantiateObject`'s
  "unable to load template" WARNING never fired), so the refusal is one of the SILENT branches:
  the shim's own `ObjectTemplateList::fetch`, `wsAllocateIdRange` → 0, or a silent
  `createObject` CEC_* → rollback.
- One environment note: the session was a SERVER login (local 192.168.1.200) with the editor
  scene loaded ON TOP via `game::loadScene` — if any pre-check behaves differently in that hybrid
  (e.g. `NetworkIdManager` holding server-streamed objects that the allocator walks differently,
  or `Game::getSinglePlayer()` gating), flag it.

## 2. Ask

Add a refusal-reason diagnostic to every fail-closed branch of `utinni_wsAddObject`,
`utinni_wsAddNodeAt`, and `utinni_wsRemoveNode` — one `REPORT_LOG`/`WARNING` line naming the branch
(args / container-not-live / origin / template-fetch / pob / pob-into-container / id-mint /
createObject-CEC-with-code / rollback), plus the offending value. These are on-demand editor
actions — no spam risk. Keep them permanent (the save shims will want the same discipline in
Wave 3; "silently did nothing" is the failure mode the whole consult exists to prevent).

No table change, no name adds, no `.h/.inc` resync — just the shim TU + a re-staged exe. HANDBACK
with the commit sha and we re-run the smoke; the consumer-side diagnostic (above) stays in place so
the two logs line up timestamp-for-timestamp.
