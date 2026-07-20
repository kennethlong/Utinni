# 2026-07-19 — ANSWERS: hybrid in-cell content (remove/targeting/occupancy) — hypothesis (b) CONFIRMED + a third layer; NO defect, no rows needed yet

**Status:** READY (answers only, no code change). Every symptom in the consult is **correct
behavior** once the three content layers are separated. All claims below are source-verified
(file:line) or measured against the LIVE loaded snapshot (parsed
`stage/override/snapshot/tatooine.ws`, the 17:35 self-test save — 15,034 nodes, the winning
priority-10 copy your session loaded).

## TL;DR — the three layers

A hybrid session's visible world is THREE layers, only ONE of which the .ws editor owns:

| Layer | Created by | Has NetworkId? | In NetworkIdManager? | .ws-editable? | Targetable? |
| --- | --- | --- | --- | --- | --- |
| **Snapshot** (statics, some in-cell props) | client, `WorldSnapshot::createObject` | YES — the **authored node id** (WorldSnapshot.cpp:274), `setClientCached` (:272) | YES | **YES** | YES (id-keyed) |
| **Server-streamed** (NPCs, terminals, **the Mos Eisley cantina itself** — see below) | server baselines | YES — server id | YES | NO | YES |
| **Interior-layout (.ilf)** (the visible furniture inside POB interiors) | client, `ClientInteriorLayoutManager` per-cell on visibility (ClientInteriorLayoutManager.cpp:141-156) | **NO — never assigned** (`addClientOnlyInteriorLayoutObject`; id stays invalid/0) | NO | NO (it's template/.ilf data, not world state) | **NO — structurally** (targeting is id-keyed) |

## The data punchline (question 1: (a) vs (b) → **(b), in a stronger form than you guessed**)

**The Mos Eisley cantina is NOT a node of tatooine.ws.** The snapshot's top-level nodes in
the Mos Eisley box (3200..3800, −5100..−4500 — 1,347 of them) contain filler buildings,
`shared_bank_tatooine`, `shared_parking_garage`, `shared_shuttleport_tatooine`, pillars/walls/
debris — and **zero cantina templates**. The whole planet has exactly **3**
`shared_cantina_tatooine` POB nodes: id **1028644** (−1404, −3669), **1134557** (−5172, −6572),
**1256055** (−3001, 2172) — the OTHER towns' cantinas, precisely the three you deleted at 22:11
(subtrees 17/70/37 ✓ match my parse: 15 cells each + their authored in-cell rows). The building
you were standing in is a **server-streamed object**; no .ws operation can ever affect it, its
cells, or refuse with −1 on its occupancy. That is symptoms 1–3 "at once", exactly as your (b)
predicted.

**The 22:27 batch was a NAME-MATCH sweep, not a "contents of this cantina" sweep.** Census of
every row in the loaded .ws whose template contains "cantina" (~119):

- 57 × `frn_tatt_chair_cantina_seat*` / tables — **inside NPC housing buildings planet-wide**
  (e.g. cell 1154122 of `shared_housing_tatt_style01_med` at (3781, 2391); others at
  (1391, 3124), (1600, 2953), (−999, −3536)…) — kilometers from you, never spawned
  (streaming), so remove = data-only tombstone with nothing live to despawn. CORRECT.
- 31 × `soundobject_cantina_*` — **invisible by nature**; a visually-null despawn is correct.
- 18 × `cantina_droid_detector` — scattered in other towns' buildings.
- 10 × `object/static/worldbuilding/sign/shared_thm_sign_cantina.iff` — top-level OUTDOOR sign
  statics, subtree=1. **These are almost certainly your "three rows the table showed as
  buildings"** (a type-heuristic on `worldbuilding`/path, we'd guess) — the 3 real cantina
  POBs can never report subtree=1 (they have 15 cell children each).
- 3 × the cantina POBs themselves.

So: `ALL subtree=1` + "nothing changed inside the cantina" is the expected outcome of that
batch — none of those rows was in (or even near) the building you occupied.

**(a) is refuted as a mechanism.** Snapshot spawns — top-level AND in-cell — are registered in
`NetworkIdManager` under the authored node id (`createObject` sets it at WorldSnapshot.cpp:274
before `endBaselines`), and `wsRemoveNode` resolves each subtree id through
`NetworkIdManager::getObjectById` (WorldSnapshot.cpp:2352/2413). There is no lookup gap: if a
snapshot node's object is live, the remove finds and deletes it (your outdoor despawns prove
the path); if it was never spawned (distance) the remove is correctly data-only. Note also
`createObject`'s first check (:244): if ANY live object already holds the authored id, the
snapshot spawn is SKIPPED (`CEC_objectAlreadyExists`) — the id-collision direction is handled.

## Question 2 — in-cell targeting under allowTargetAnything

Expected to work for objects **that have a NetworkId**; structurally impossible for the rest:

- The pick RAY does reach in-cell contents: cell containment attaches the object to the cell
  owner's child list with `isChildObject()==false` (`Object::setParentCell` →
  `attachToObject_w(&cellOwner, false)`, Object.cpp:1387/1405), and the in-cell collide
  recursion traverses exactly those (`internalCollideObject` child walk, ClientWorld.cpp:1330
  — the `!isChildObject() || CF_childObjects` gate). `getRootParent()` walks only while
  `m_childObject` is true (Object.cpp:2105-2108), so the pick resolves to the chair itself,
  not the building. `testFindObject` under `allowTargetAnything` then filters almost nothing
  (SwgCuiHud.cpp:198-224).
- But NGE **target selection is id-keyed** (`m_lookAtTarget` is a `CachedNetworkId`;
  `setLookAtTarget(NetworkId)`). An .ilf object has **no id** — `ClientInteriorLayoutManager`
  never assigns one — so "target it" degenerates to setting an invalid id: your "clicking a
  chair does nothing". `allowTargetAnything` widens the pick FILTER; it cannot make an id-less
  object referencable. Your outdoor statics target fine because they are snapshot spawns
  carrying authored ids. In-cell SNAPSHOT rows (the housing furniture, droid detectors — and
  1134557's authored in-cell props) are equally targetable when spawned; cantina interiors
  just contain almost none of them.

## Question 3 — occupancy semantics in hybrid

Designed outcome, yes: −1 fires only when the deleted SUBTREE's own live objects include, or
host by parentCell, a non-client-cached occupant (both directions, WorldSnapshot.cpp:2350-2396).
Your batch deleted nodes that neither contained nor hosted anyone — correct no-fire. You can
never get −1 "from inside the Mos Eisley cantina" because that building has no .ws node to
delete.

**Canonical occupied-POB test (confirmed as the intended shape):** editor-add a POB
(`wsAddObject`), walk in, delete its row from inside → the upward sweep sees your parentCell's
owner (the added building's minted cell id ∈ subtree) and you are non-client-cached →
`OCCUPIED (parent-cell)` −1. All-snapshot, zero layer ambiguity. Equivalent second repro with
pre-existing content: travel to one of the three REAL snapshot cantinas (1028644 / 1134557 /
1256055), stand inside, delete that node id → expect −1; step out → expect OK + visible
building despawn.

## Question 4 — collideScreenRay as the layer oracle (recommended, no new rows needed)

Wire it exactly as you proposed — it is the **cheapest layer discriminator**, using only
existing v20 rows:

1. `collideScreenRay` hit with **id == 0** while indoors → interior-layout (.ilf) decoration
   (or raw cell geometry). Not world state; not editable via .ws; not targetable. (The shim's
   networked-ancestor walk already ran — id 0 means NOTHING in the parent chain has an id.)
2. **id != 0** and `wsGetNodeInfo(id)` **hits** → SNAPSHOT layer: yours. Remove/move/save all
   apply.
3. **id != 0** and `wsGetNodeInfo(id)` **misses** → server-streamed (or non-snapshot) object:
   targetable, not .ws-editable.

That one `wsGetNodeInfo` membership probe is the "flag on the Object" you asked for — no
provider-side addition required. Provider diagnostics: nothing new needed;
`[editor.ws]` lines already log every ws mutation, and the pick side is fully observable from
your end via the triage above. (The engine's `reportWorldSnapshotCreates` flag is
Debug-build-only — not available on the staged Release exe.)

## Editor-UX suggestions that fall out of the data (consumer-side, take or leave)

- The placements table needs a **spatial view**, not just name filter: "contents of the
  building I'm in" = `wsGetChildCount`/`wsGetChildIdAt` walk of a root node, and/or a
  distance-to-player column (node position is in `UtinniWsNodeInfo`). A name filter over
  15,034 planet-wide rows will always read as "nothing happened" when the matches are remote.
- Type-classify rows by template path segment (`soundobject/` = invisible; `static/worldbuilding/sign/`
  is not a POB) — the "three buildings, subtree=1" confusion looks like exactly that heuristic.
- Surface "hit but id=0" in the gizmo/picker UI as "interior decoration (not editable)" so the
  .ilf layer is self-explaining in the tool.

## Follow-up freeze request — what would actually need provider rows (only if you want it)

Editing **interior-layout content** is a different artifact class (.ilf files, shared between
every instance of a building template — an edit to the cantina .ilf changes EVERY cantina).
If the toolkit ever wants that: it needs its own read/save surface
(`InteriorLayoutReaderWriter` exists engine-side and has a save path), and a design decision
about per-instance vs per-template semantics. Out of scope until you ask.

## ADDENDUM (2026-07-19 evening) — making id-less objects targetable/manipulatable

Kenny asked the natural follow-up; provider position, source-verified:

**1. Manipulation may already work TODAY, id-free — smoke this first (zero cost).**
The hud's selection is NOT id-keyed: `m_lastSelectedObject = foundObject`
(SwgCuiHud.cpp:1436) stores the picked **`Object*`** in a Watcher, fed from the pick path
that honors `allowTargetAnything` — id-less .ilf objects included. That watcher is already
advertised: **`cuiHud::getTarget`**. Chain to try:
`setAllowTargetAnything(true)` → hover/click the chair → `cuiHud::getTarget` → non-null
`Object*` → gizmo drives the advertised object transform rows. What broke in your smoke was
only the id-keyed half (brackets, `getObjectById`, ws bookkeeping) — not the Object*-keyed
half. If a later filter does drop id-less objects, the fallback is a trivial v21 sibling:
`clientWorld::collideScreenRayObject` returning the borrowed `Object*` (per-frame-safe ONLY —
.ilf objects are deleted on cell unload; never cache across frames).

**2. Minting ids ("guid") for .ilf objects — feasible, reserved-band, with audit items.**
`NetworkId` is int64; `isValid()` only checks `!= 0` — a reserved band (negative is natural:
servers + snapshots mint positive only) can be assigned at ilf-create and registered in
`NetworkIdManager`, making them targetable through every normal id path. Stability: hash
`(buildingId, cellName, ilfIndex)` into the band → the same chair gets the same id every
session (selection/undo continuity). Audit before landing: (i) id-leak into server traffic in
hybrid sessions (`setLookAtTarget` uplinks the id; radial requests on it become server
no-ops — likely harmless, verify); (ii) ws allocator ignores the band (negative is
out-of-band by construction — confirm the seeding filter).

**3. Ids make them selectable, NOT persistent — the real design decision is where edits live:**
- **(a) Edit the .ilf itself (provider recommendation).** The engine writer EXISTS and is
  complete: `InteriorLayoutReaderWriter::{save,clear,addObject}` (sharedUtility, verified).
  A small advertised surface (enumerate entries of the building under edit, move/add/remove
  entry, save to the loose override dir where the searchPath already wins) = real
  persistence. Semantics are PER-TEMPLATE — moving the chair moves it in every instance of
  that building style, everywhere. For a world-building toolkit, arguably the honest and
  useful semantic of that layer (fix a bad layout once).
- **(b) Promote to snapshot rows** — convert a picked .ilf object into a `wsAddObject`
  contained node + suppress the .ilf original. Per-instance semantics, but only possible
  inside SNAPSHOT buildings (server-streamed POBs have no cell node to contain into), and
  the suppression sidecar is real machinery (else duplicates on next cell load).
- **(c) Session-only** — ids + gizmo, edits revert on reload. Demo mode.

Suggested sequencing: smoke #1 now (may unblock in-cell gizmo work with zero changes); if
the layer is wanted for real, (a) is a clean bounded freeze request — the writer, the
override mechanism, and the v20 pick oracle already line up for it. A provider-side design
consult on this is queued our side.
