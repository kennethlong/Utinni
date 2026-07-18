# Consumer handoff — id-mint mechanism CAPTURED; adds/removes now mint; one Wave-3 provenance flag

**Status:** EVIDENCE + FLAG (your §4 ask answered; blocker resolved by the `d7dba07a6` hardening) · 2026-07-18
**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Contract:** unchanged, v18 / 133
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-18-utinni-goalB-wave2-idmint-evidence.md`;
the Utinni copy governs.

## 1. The line you asked for (restaged exe, 2026-07-18 ~12:21 local / 17:21Z, Mos Eisley SERVER session)

```
[editor.ws] wsAllocateIdRange: authored ids at/above the ceiling exist (max=609457649 >= 16777216) -- excluded from seeding
[editor.ws] wsAddObject OK: id=9995371 cells=0 template=object/tangible/furniture/cheap/shared_armoire_s01.iff containedById=0
[editor.ws] wsRemoveNode OK: id=9995371 subtree=1 nodes
(add→remove→add cycles re-mint the SAME id — the tombstone free-test is exact, as designed)
```

Mechanism: your (a′) variant was right at RUNTIME even though every on-disk `.ws` is clean —
**id 609,457,649 (a server-object-range id) sits in the live reader tagged authored** on a server
session. Something inserts server-streamed content into `ms_reader` at runtime (a
`WorldSnapshot::addObject` caller on the server-session path is our guess — your tree, your grep).
The seed walk read it, exploded past the ceiling, and every mint refused. Your defensive hardening
(exclude out-of-band ids from seeding) is the correct fix for the mint path and is field-verified:
adds, removes, and re-mints all work on the live session now. Maintainer smoke of the rest of the
Wave-2 surface (undo replay, duplicate, radius, occupied-POB, the §5.6 gizmo probe) is in progress.

## 2. The Wave-3 provenance flag (please fold into the §5.1 answers before the Wave-3 freeze)

609,457,649 **round-trips int32** — so at Wave 3 these runtime server-id nodes would pass the
id-width fail-closed check and **serialize into a saved `.ws` as authored content** (a wandering
NPC baked into the planet snapshot). They are NOT in `ms_buildoutObjects`, so the retained-set
filter doesn't exclude them either. Wave 3 needs a third provenance class for
runtime-inserted/server-session nodes. Cheapest shapes we see (your call, per the §5.1a pattern):
tag ids inserted into the reader outside the `.ws` parse into a second retained set and filter
save/enumeration by both; or exclude anything ≥ the mint band's ceiling AND anything whose id was
never parse-inserted. Whatever you pick, the enumeration question follows (should the editor's
placements table SHOW server-session nodes? we'd say no — same authored-only contract).

Also worth naming: whatever inserts those nodes is ALSO why our Wave-1 placements counts on server
sessions may include runtime rows — we'll re-check the table against this once the smoke completes.

## 3. No ask beyond the flag

Wave-2 smoke resumes on our side; Wave-3 freeze follows it and will reference this flag. Keep the
allocator diagnostics permanent as agreed.
