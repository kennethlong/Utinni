# Provider Flag — occupancy guard did NOT fire deleting a POB with the player inside

**Status:** FLAG (behavioral, not a crash; needs your read on the containment model) · 2026-07-18
**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Baseline:** v18 / 133 (+ diag `d7dba07a6`)
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-18-utinni-goalB-occupancy-guard-flag.md`;
the Utinni copy governs.

## 1. Observation

Wave-2 smoke, Mos Eisley, **SERVER session** (client logged into a live server, character physically
inside the cantina — walked in through the door). Maintainer targeted the cantina POB via the
placements table and deleted it. Result:

```
[editor.ws] wsRemoveNode OK: id=1134557 subtree=70 nodes
```

**`OK`, not `-1 occupied`** — the guard did not fire, even though the player (a server-streamed,
NOT client-cached object) was inside. No crash, no fall-through reported; the building despawned
cleanly and the player remained. (Other same-session removes: id=1028644 subtree=17, id=1256055
subtree=37, plus ~12 singletons — all OK.)

## 2. Why this is a flag

Your Wave-2 HANDBACK §3 said the opposite should happen: *"server-streamed objects are NOT
client-cached, so `wsRemoveNode` on a POB the server has populated (vendors, NPCs inside) will
correctly return `-1` occupied."* The player is the canonical non-client-cached occupant, so
standing inside and deleting should have refused. Two possibilities, both yours to adjudicate:

- **(a) The player was not engine-*contained* by the cantina's live Object.** Visually-inside ≠
  `ContainedByProperty`-inside. If the cell the player stands in is not a descendant of the
  snapshot cantina node's spawned Object (separate instance, or the portal/cell containment never
  established on this editor-loaded-over-server hybrid), the recursive `isClientCachedOnly` walk
  from the POB root never reaches the player → correctly returns OK, but the guard then protects
  nothing. If so, the guard needs a broader occupancy test (position-in-cell? the engine's own
  "who is in this POB" query?) or we accept the limitation and document it.
- **(b) The player object *is* client-cached on this NGE build** (or the recursive check has a gap),
  so it's treated as removable. If so the guard's predicate is wrong for the player case.

Either way: **the fact that deleting an occupied POB SUCCEEDED without incident is itself the
concern** — if the cascade had reached the player under slightly different containment, it would
delete the player's Object (your §5.5: `Container::~Container` deletes contents unconditionally).
It didn't this time; we'd like to know why so it never does.

## 3. What we need

Your read on which case this is (a runtime probe on your side — log the subtree's per-node
`isClientCachedOnly` + whether the player's `ContainedByProperty` chain reaches the POB root when
the player is inside — would settle it), and whether the guard needs strengthening for the player
case before we call the occupied-refusal path smoke-verified. No contract change implied yet; this
is a correctness question about the §5.5 guard's reachability on the advertised/server-hybrid
client. Folds naturally into the Wave-3 conversation (the save-provenance flag + camera-accessor
ask are already queued there).

## 4. Not blocking

The rest of the mutation surface smoked clean this session: add/remove/duplicate(after a consumer
hotkey-persist fix)/radius all OK, undo/redo OK, the id allocator hardening holds (re-mints exact).
The gizmo live path is guarded dark pending your camera accessors (separate ask). This flag is the
one open behavioral question on the Wave-2 surface.
