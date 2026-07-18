# Provider Request — Wave-2 add refusal ROOT-CAUSED to the id allocator (paired logs inside)

**Status:** REQUEST (bug fix in `wsAllocateIdRange` / its inputs; no contract change expected) · 2026-07-18
**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Baseline:** v18 / 133 (+ your `4a8b5d605` diagnostics)
**Responds to:** `2026-07-18-utinni-goalB-wave2-add-diagnostics-HANDBACK.md` §4 ("send the paired logs")
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-18-utinni-goalB-wave2-idmint-fix-REQUEST.md`;
the Utinni copy governs.

## 1. The paired logs (diag exe, 2026-07-18 ~11:59 local / 16:59Z)

Consumer (utinni.log):
```
[11:59:19] WorldSnapshotLive::addObject('object/tangible/furniture/cheap/shared_armoire_s01.iff', pos=(3471.2, 4.0, -4865.3), container=0) -> id=0   (Mos Eisley, SERVER login)
[11:59:21] (same, second click)
[11:59:32] WorldSnapshotLive::addObject('...armoire...', pos=(0.0, 20.3, 0.0), container=0) -> id=0   (naboo, EDITOR scene)
```

Provider (SwgClient_report.log):
```
20260718165919 [editor.ws] wsAddObject REFUSED (id-mint): band [floor=0 ceiling=16777216) exhausted/invalid for cellCount=0
20260718165921 [editor.ws] wsAddObject REFUSED (id-mint): band [floor=0 ceiling=16777216) exhausted/invalid for cellCount=0
20260718165932 [editor.ws] wsAddObject REFUSED (id-mint): band [floor=0 ceiling=16777216) exhausted/invalid for cellCount=0
```

Timestamp-exact pairing; your §2 null-slot theory is out (our diag line only prints after invoking a
non-null slot). The branch is **id-mint**, on BOTH a server tatooine session and an editor naboo
scene. Every other branch (args/origin/template/pob/createObject) never fired.

## 2. Why we believe the allocator inputs — not real collisions — are the bug

Real exhaustion needs EVERY candidate in `[seed, 0x1000000)` occupied — millions of consecutive
live/reader/buildout hits on two different planets. Offline facts: naboo's authored max id is
9,895,360 (0x96FDC0; we parsed `snapshot/naboo.ws` out of `patch_17_00.tre`), leaving ~6.8M free
candidates; the refusal returns instantly on click. Two candidate mechanisms fit:

- **(a) Seed explosion past the ceiling.** `wsAllocateIdRange`'s seed walk advances past any
  READER id that is `>= seed` and NOT in `ms_buildoutObjects`. A buildout row whose id is a large
  POSITIVE int64 (your own finding #5: the v2 loader XORs only `objId < 0` — a positive objid
  column passes through sign-intact) that for any reason is MISSING from the retained set (insert
  path condition, set cleared/rebuilt between phases, id transformed after insert) would drive
  `seed` past 0x1000000 in one step and the for-loop guard (`id + cellCount < ceiling`) is false on
  entry — "exhausted" without one iteration. SWGSource v3 data is exactly the lineage where
  positive v2 objids live.
- **(b) `NetworkIdManager::getObjectById` non-null for arbitrary ids** on this client build (a
  default/sentinel return instead of null) — every candidate "collides", the walk runs its millions
  of map lookups fast, exhausts, returns 0. Consistent with both scenes and with your own gate
  history: the Wave-2 gate boot-smoked and world-loaded but never exercised an actual add, so
  either mechanism would have slipped through.

Discriminator suggestion (one diag line): log the computed `seed` before the loop (and, if you
want it airtight, the first 3 collided ids + which of the three predicates hit). Seed ≥ ceiling ⇒
(a) with the offending reader id one map-walk away; seed sane but every candidate colliding ⇒ (b).

## 3. Housekeeping

- Everything else in the diag HANDBACK checks out from our side: §3's hybrid-session notes are
  understood (the `-1` occupied on server-populated POBs is in our smoke script as an expected
  outcome, not a bug).
- Consumer-side scene-arm root fix landed (Utinni `df81ccf`): setSceneCallbacks now dispatch from
  the update-loop latch's first tick on ANY advertised login — your §2 "check the resolve count"
  reminder is moot going forward; both this session's logs show 131/131 + the arm line.

**Ask:** fix the id-mint path (with whatever the discriminator shows), keep the diagnostics,
restage, HANDBACK with the sha. Our re-smoke is one click; the paired-log channel works.
