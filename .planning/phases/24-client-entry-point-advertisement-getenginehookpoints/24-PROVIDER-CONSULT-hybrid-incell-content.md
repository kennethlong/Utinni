# 2026-07-19 — CONSULT: pre-existing in-cell snapshot content unreachable in hybrid sessions (remove/teardown, targeting, occupancy)

**Status:** QUESTIONS ONLY — no rows requested yet. Evidence from tonight's Wave-3 smoke
session on the v20 exe (staged 16:14). We want your read on the model before we design
anything; a follow-up freeze request may fall out of the answers.

## Session shape

Hybrid, same as your weather NOTE: server login (`Cluster: swg`) + editor scene via
`game::loadScene`, advertised v20 exe, consumer bind v20/142 (140/140 resolved, tonight's
`utinni.log` line 1). `wsSelfTestSaveOnLoad=1` armed. Player in/around the Mos Eisley
cantina area (tatooine).

## What works (baseline — no questions here)

- Editor-ADDED object lifecycle is perfect end-to-end: add → visible in-world → save →
  zone → reload → persisted → single-target remove → despawn + our SysMsg confirm.
  Note the remove happened AFTER a save+reload cycle in a maintainer-created scene — i.e.
  the object removed cleanly as an AUTHORED node of the loaded .ws (editor-minted id
  9995371 persisted in the file), not merely as a live-session add. So authored OUTDOOR
  nodes despawn fine regardless of provenance; `sendFakeSystemMessage` renders fine.
- OUTDOOR pre-existing snapshot deletes despawn visibly (earlier today: `wsRemoveNode OK`
  id=1028644 subtree=17 / id=1134557 subtree=70 / id=1256055 subtree=37, 22:11 UTC in
  `SwgClient_report.log`).
- Save path: multiple `wsSaveSnapshot OK` + `SELF-TEST result=0` across zone-ins, no crash.

## The seam: pre-existing IN-CELL content (three symptoms, one shape)

1. **In-cell node removes succeed on paper but the visible object never despawns.** The
   maintainer deleted every "cantina" placement row (22:27:57–22:28:05 UTC): a dozen-plus
   `wsRemoveNode OK` lines, ALL `subtree=1` (furniture/prop placements, incl. three rows
   the placements table showed as buildings), and **nothing changed visually inside the
   cantina** — the furniture stayed. No `OCCUPIED` line anywhere.
2. **In-cell objects can't be targeted** even with `setAllowTargetAnything(true)` armed
   (clicking a cantina chair does nothing; outdoor statics target fine — that's how the
   v19 targeting smoke passed).
3. **The occupancy refuse (−1) never fires from inside** — expected IF the player's
   parentCell owner is a different live instance than the snapshot node being removed
   (see hypothesis), but we'd like that confirmed rather than assumed.

## Our hypothesis (please confirm/refute)

This smells like the SAME asymmetry your occupancy fix (`835ad389c`) documented: in server
sessions, occupants aren't linked into cell Container CONTENTS — the client tracks cells
via `Object::getParentCell`. Two variants of the question:

- **(a) Teardown lookup:** does `wsRemoveNode`'s live-object resolution (node id → spawned
  client object → despawn) find IN-CELL client-cached objects in a server/hybrid session?
  If it walks Container contents (or `NetworkIdManager` by authored id), does the in-cell
  case fall through the same gap the occupancy walk did — node removed from data, spawned
  object never found? That would exactly produce symptom 1.
- **(b) Ownership:** OR are the visible cantina interiors in a hybrid session simply not
  the snapshot's spawns at all (server/buildout-streamed instances), with the snapshot's
  copies never spawned or hidden? That would produce symptoms 1–3 at once and make
  everything we saw CORRECT behavior. If so: what's the cheapest way to tell the layers
  apart from the client (id ranges? `NetworkIdManager` membership? a flag on the Object)?

## Questions

1. (a) vs (b) above — which is it (or both)?
2. Is in-cell targeting expected to work under `allowTargetAnything` (the
   `SwgCuiHud.cpp:198/365` pick path)? Symptom 2 suggests the pick never reaches those
   objects regardless of the preference.
3. Occupancy semantics in hybrid: with the player inside the (server?) instance, is
   no-refuse the DESIGNED outcome for removing the snapshot's copy? What is the correct
   occupied-POB test in a hybrid session — is "editor-add a POB, walk in, delete its row"
   (all-snapshot, no layer ambiguity) the intended shape?
4. Non-blocking: we have v20 `collideScreenRay` bound but not yet consumer-wired; wiring
   it as a "what is the cursor actually over" probe (id + networked-ancestor walk) is our
   next step if useful. Any provider-side diagnostic worth arming alongside it
   (`[SharedLog] logReportLogs=1` is already on)?

## Consumer-side state (for reference)

Utinni `da69a30` (v20 re-sync + bind) + UtinniPlugins `855ca1d` (every remove outcome now
surfaces as a SysMsg — the silent-miss paths that cost us a triage round tonight are gone).
All gates green. The weather-spinner crash from your NOTE is closed consumer-side
(`2c7c9e0`: fail-closed advertised guards on the SWGEmu-only Terrain RVAs).
