# Provider ANSWERS — Goal B: the Snapshot editor on the advertised client

**Status:** rev-2 ANSWERS (design only — no rows frozen, no code landed) · 2026-07-15
**From:** swg-client-v2 (provider) · **To:** Utinni (consumer) · **Baseline:** contract **v16 / 121 names**
**Responds to:** `2026-07-13-utinni-goalB-snapshot-editor-consult.md` (rev-2)
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-15-utinni-goalB-snapshot-editor-consult-ANSWERS.md`;
the Utinni copy governs once mirrored.

Every §2 claim in your rev-2 was re-verified against the tree 2026-07-15 and **all hold**. We also
found five provider-side facts you could not see from outside — three of them change §5 answers, so
they lead. Then a paragraph per §5 point, then §4 vetoes/adjustments. Direction is ACCEPTED:
id-keyed, primitives-only, `extern "C"` shims in WorldSnapshot.cpp, three smoke-gated waves.

---

## 0. Provider-only findings (read these first — they reshape three answers)

1. **The searchPath negative cache will eat your first save.** Our CONSULT-59 loose-searchPath
   stat-storm fix (`TreeFile_SearchNode.h:99-110`, landed 2026-07-06, default ON) caches per-node
   MISSES: a name probed once as missing is answered from the cache without touching disk for the
   rest of the session — and `snapshot/<scene>.ws` is probed against every loose search path at
   every scene load. A `.ws` saved into a loose dir mid-session is therefore INVISIBLE to
   reload-after-save until client restart. Wave 3's save shim must invalidate the cached miss for
   the written name (internal `TreeFile` helper, not an advertised row). This is baked into the
   §5.1d answer; without it, save→reload silently loads the OLD file and looks like data loss.
2. **`ms_reader.addObject` FATALs on an unknown container** (WorldSnapshotReaderWriter.cpp:853).
   A `wsAddNodeAt` replay whose `containedById` is missing would hard-crash the client, not fail
   closed. The shim pre-checks and returns 0. Also: the id-collision silent no-op you found for
   `cellIndex < 0` is a `DEBUG_FATAL` for `cellIndex >= 0` — which compiles out in Release, so in
   the shipping client **every** collision is silent. Fail-closed pre-checks in the shim are the
   only real guard; there is no engine backstop.
3. **The advertised `worldSnapshot::removeObject` leaks the live object irrecoverably.** It
   removes the sphere handle and tombstones — but never despawns the spawned Object, and
   `setDeleted()` **zeroes the node id** (ReaderWriter.cpp:138), so afterwards even `unload()`'s
   object lookup (keyed by node id) misses it: the object stays visible and alive until process
   exit. Your §5.5 "remove must despawn" is not just the right editor UX — it is the only correct
   remove on this engine. Consumer rule: once `wsRemoveNode` exists, never call the advertised
   `removeObject` row from the editor.
4. **Child nodes reach `ms_loadedList` too.** The update() cell-fill path (WorldSnapshot.cpp:1209)
   calls `addObjectToWorld` for CHILD nodes of a visible cell — so remove must sweep the whole
   subtree out of `ms_loadedList`, not just the top node. The pending create/delete lists also
   persist across frames under the create budget; a tombstoned node left in `ms_pendingCreateList`
   would spawn an id-0 object. `wsRemoveNode` purges the subtree from all three lists.
5. **All buildout ids are negative by construction.** v1 mints from
   `getSharedBaseId() = -(areaIndex+1)*30000` (SharedBuildoutAreaManager.cpp:64-66) and stays
   inside that negative 30k band; v2 ids are "random 31-bit negative" and the `areaIndex << 48`
   XOR doesn't touch bit 63. Authored `.ws` ids are positive int32 (`load_0000` reads int32).
   So authored-vs-buildout is sign-separable — we still keep your retained-set filter as the
   authoritative source (see §5.1a), with the sign check as a belt-and-braces assert.

Also verified for §5.3: the `WorldSnapshot::addObject` bookkeeping gap is WORSE than your §2 note —
it skips the sphere-tree insert too, so its adds are invisible to the streaming diff entirely
(never enumerated by `update()`, never unloaded, live object orphaned from delete bookkeeping).

## 1. §5 ANSWERS

### 5.1a Provenance — retain `ms_buildoutObjects`, filter save AND enumeration by it

Agreed, and your "cheapest fix" is the right one: we stop clearing `ms_buildoutObjects` at parse
completion (WorldSnapshot.cpp:852) and keep it for the session. Verified safe: the only post-parse
reader of that set is the PP_sphereTree phase itself (already complete by PP_done), and it is
re-cleared at both `load()` (:531) and `unload()` (:419), so scene changes stay clean. Memory cost
is trivial next to the reader. Save and Wave-1 enumeration both filter by it: a node serializes /
enumerates only if its id is NOT in the set. Belt-and-braces, we also assert the sign invariant
from finding #5 (buildout negative, authored positive) — if a future buildout format ever mints
positive ids the assert fires in Debug instead of silently corrupting a save. Enumeration is
therefore **authored-only** in v1; if the editor ever wants to SEE buildout rows (e.g. to duplicate
one into the snapshot), that's a later flags-bit extension (§5.4 reserves it), not a v1 behavior.

### 5.1b Deleted nodes — tombstone stays, contracts skip it; remove gets real teardown

One fix serves both, but not by physically unlinking: the reader has no unlink API and adding one
buys nothing over the skip-tombstone contract you already specified in §4. Decision: `wsRemoveNode`
does the FULL live teardown (§5.5) and then tombstones via the existing `removeNode`; enumeration
shims skip `isDeleted()` nodes; **save skips deleted nodes recursively** (a deleted node's whole
subtree is skipped — children of a tombstoned parent are unreachable by contract). The recursive
save-skip also defends against any legacy tombstones produced by the advertised `removeObject` row
(finding #3) — we are NOT changing that row's semantics (something in the wild may depend on it);
the editor just stops using it.

### 5.1c Id width — keep TAG_0000/int32; allocator enforces; save fails closed; ceiling is yours to supply

Agreed on all counts, and we confirm your veto instinct: **no `0002` form.** Every `.ws` consumer
(retail tools, SWGEmu, our own loader) reads int32 node payloads; orphaning that for v1 editor
saves is a bad trade. Policy we'll implement: the allocator mints only ids in
`(floor, ceiling)` with `ceiling <= INT32_MAX`, positive, outside the buildout band (trivially true
— buildout is negative); save walks the authored node set before writing and **fails closed
(typed error, writes nothing)** if any id or containedById doesn't round-trip int32 — unreachable
if only our allocator minted, but old hand-edited snapshots exist. On "confirm the right ceiling":
**we cannot — the constant does not exist in this engine.** There is no server-id floor anywhere in
the tree (we grepped); `0x1000000` is a server-side convention of yours. So it stays
consumer-supplied: default ceiling `0x1000000`, overridable via the one-time allocator-config shim
(§4 adjustments), same place your install-scan floor goes. Buildout truncation is moot via (a) —
negative ids never serialize.

### 5.1d Destination — absolute path into the winning loose SearchPath root; save invalidates the negative cache; picker needs one extra row

Not CWD-relative — the shim resolves an ABSOLUTE destination:
`<highest-priority loose SearchPath root>/snapshot/<scene>.ws`, using the existing public
`TreeFile::getNumberOfSearchPaths()/getSearchPath(int)`. That's the same override layer that
already wins resolution over the TOCs on every advertised install (our stage setups always carry
one), so the engine loads the saved file back by construction: reload = `unload` + advertised
`load(currentScene)` → `TreeFile::open("snapshot/<scene>.ws")` → loose file wins. Two traps we own
(finding #1 + one more): **(i)** save must invalidate the searchPath negative-cache entry for the
written name or reload-after-save loads the OLD TOC copy until restart — internal provider detail,
handled inside the save shim; **(ii)** your picker enumerates via `treeFile::enumerateFiles`, which
by its own contract walks **only SearchTree/SearchTOC nodes** (TreeFile.h:97-101) — a loose saved
`.ws` will never appear there. Rather than widen `enumerateFiles` (it runs under TreeFile's
critical section; enumerating a live directory there is a hazard), Wave 3 adds
`utinni_wsGetSavePath(char* buf, int cap)` returning the resolved save root; your Repository unions
a plain directory listing of `<saveRoot>/snapshot/` with the enumerated set. If NO loose SearchPath
is configured, save fails closed with a typed error (never falls back to CWD). Save-as deferred to
post-v1, agreed — the destination policy above extends to it naturally.

### 5.2 Id allocation — provider-owned first-fit over live reader state; your scan becomes an optional floor

Provider-owned, per your shape: contiguous `id..id+cellCount` first-fit, every id in the range
verified free against the live reader map (`find()`), the retained buildout set, and the
floor/ceiling band; fail-closed 0. Seed = max positive authored id at parse completion + 1, raised
by your optional floor. On the sanity-check: max-of-loaded is a safe seed **on this engine**
because the reader is a per-scene singleton (cleared on every scene change) — the only ids that can
collide at runtime are ids reachable in the SAME session, i.e. live server-streamed ids, which is
exactly what the floor/ceiling band excludes. Unloaded sibling `.ws` files can't collide with a
running scene; they only matter if you want ids unique ACROSS snapshots as an authoring discipline
— which your install-scan gives you. Keep the scan consumer-side and pass the result as the floor.
Tombstone-freed ids: reusable safely ONLY because `wsRemoveNode` fully despawns (no live Object
retains the id) — one more reason the editor must never use the advertised `removeObject` row
(finding #3). Note `setDeleted()` zeroes the stored id, so a freed id is genuinely unrecorded — the
map-miss IS the free test.

### 5.3 Add bookkeeping — yes, full streamed-create routing; and the gap is bigger than you knew

Confirmed, emphatically — see finding #6: `WorldSnapshot::addObject` skips the sphere-tree insert
too, so its adds are entirely outside the streaming machinery. The `wsAddObject` shim routes:
reader-add (+ cell children) → `node->setSpatialSubdivisionHandle(ms_sphereTree.addObject(node))`
→ spawn via the SAME `createObject` + `addObjectToWorld` pair the streamed path uses (node in-world
mark + `ms_loadedList` push). Mid-session sphere-tree insert is safe — `moveObject`/`removeObject`
already mutate the tree mid-session (`move`/`removeObject` on live handles); insert is the same
class of operation. The handle needs nothing from you: `ms_sphereTree.addObject(node)` returns it
and the node stores it. Two engine guards to know about: `createObject` REFUSES top-level nodes at
or within 10m of the world origin (`CEC_orphanedAtOrigin`/`CEC_tooCloseToOrigin`, corrupt-data
guards) — the shim pre-validates position before minting an id and fails closed, so you never get
a half-added node; and event-named nodes divert to the event map — editor adds pass empty
eventName, so no divert.

### 5.4 POD-out struct — accepted as proposed

`UtinniWsNodeInfo` accepted: size-first protocol, fixed-width members only, layout as you wrote it.
Provider-side confirmations: `Transform` is `float[3][4]` row-major with position in column 3 and
`real == float` — your 12-float mirror is layout-exact. Two notes, neither a change: flags bit0
(deleted) will always read 0 in practice — `wsGetNodeInfo` returns 0 (miss) on tombstones per the
skip-tombstone contract — keep the bit for shape stability; and we reserve **bit1 = buildout**
(always 0 in v1, since enumeration is authored-only) against the §5.1a extension. If you want to
save a per-row call in the panel, we're happy to append `int32 childCount` after `transform` —
your call at rev-3; the size-first protocol makes it a free addition.

### 5.5 Remove = full despawn — committed; here is the engine-safe teardown

Committed, no downgrade needed — mid-frame external despawn is safe on the game thread; it is
literally what `update()`'s own delete path and `removeEventObjects` already do mid-frame
(`removeFromWorld` + `delete object`; deleting a POB root cascades through its contained
cells/contents, same as `unload()` which deletes only top-level objects for exactly that reason).
`wsRemoveNode(id)`, game-thread marshaled, does — in this order, because `setDeleted()` zeroes ids
(finding #3): **(1)** capture the full subtree id list FIRST; **(2)** remove the root's sphere
handle (children never hold handles — only top-level nodes enter the tree); **(3)**
`node->removeFromWorld()` (recursive in-world unmark); **(4)** look up the live Object by root id →
`removeFromWorld()` + `delete` (container cascade); **(5)** erase every subtree node from
`ms_loadedList` (children can be in it — finding #4) and purge them from
`ms_pendingCreateList`/`ms_pendingDeleteList` (persist across frames under the create budget);
**(6)** `ms_reader.removeNode(each subtree id)` — so the whole subtree tombstones and the map frees
every id, keeping the §5.2 free-test exact. Returns 1 on success, 0 on miss (null-safe per your
undo contract). Your side's only obligation is the one you already committed to: game-thread-only.

### 5.6 Gizmo prerequisite — nothing needed from us now; two heads-ups for your Wave-2 probe

The `setTransform_o2p` + `PositionAndRotationChanged` write path is the standard client-side object
write and we expect it to work on the advertised client. Heads-up one: node transforms and
`moveObject` are PARENT-relative (`_p`) — for a cell-contained object your gizmo must write
cell-relative transforms, not world. Heads-up two: nothing in the node model or `moveObject` can
express a containment CHANGE — `cellIndex`/`containedById` are fixed at add — so a drag across a
cell boundary is not a move, it's a remove + re-add (you own both primitives, and your undo model
already thinks in those terms). Suggest the gizmo clamps to same-container motion in v1. If your
probe finds the notify path dead on advertised, come back with the evidence and we'll look at a
row.

### 5.7 Cheaper seam — no; this is the seam

We looked. `WorldSnapshotViewer` is an MFC read-only viewer over the same ReaderWriter (and its
build is pre-broken with the rest of the Qt/MFC editor tier); there is no buildout-editor relic
client-side; the god client has no snapshot surface worth borrowing. Your Option C over `ms_reader`
in WorldSnapshot.cpp is the seam we would have picked ourselves — everything the shims need
(`ms_reader`, `ms_loadedList`, both pending lists, `ms_sphereTree`, `ms_buildoutObjects`) lives in
that one TU's anonymous namespace, and the four advertised rows reuse as-is.

## 2. §4 vetoes / adjustments (for your rev-3 row table)

**Wave 1 — accepted as sketched**, with contracts pinned: enumeration = live, authored-only (skip
tombstones AND buildout, per §5.1a/b); reads inherit the `finishLoadNow()`-when-`ms_parsePending`
discipline (matches every existing mutator); `wsGetGeneration` bumps on `load`/`unload`/`clear`
only. `wsGetNodeTemplateName` copy-out returns needed length — accepted.

**Wave 2 — accepted with these deltas:**
- `utinni_wsAddObject`: as sketched, plus pre-validation (origin-distance guard, template
  resolvable) BEFORE minting, so failure never mutates the reader. Spawns immediately via the full
  streamed-create routing (§5.3). Returns new id, 0 fail-closed.
- `utinni_wsAddNodeAt`: accepted shape, one semantic pin — it is a pure DATA re-add + sphere-handle
  (top-level only), **no immediate spawn**. The live spawn happens through the normal streaming
  pass: next `update()` sees the node in range and `createObject` recurses the children present at
  that moment. That is exactly what an undo-replay wants (a distant undone object should NOT
  force-spawn; a near one appears within a frame) — but it makes batching a hard contract:
  **replay the top node and ALL its children in one game-thread marshal batch**, before the next
  update tick, or the recursion spawns a partial subtree. Fail-closed (return 0, add nothing) on:
  id present, id ≤ 0, id > INT32_MAX, id in the buildout set/band, or `containedById` non-zero and
  missing (finding #2 — the engine would FATAL, not error).
- NEW (small): `utinni_wsConfigureIdAllocator(__int64 floor, __int64 ceiling)` — one-time,
  optional; 0 = keep default (seed per §5.2, ceiling `0x1000000`). This is where your install-scan
  floor lands.
- `utinni_wsRemoveNode` / `utinni_wsSetNodeRadius`: accepted; remove per the §5.5 teardown
  (subtree-wide, returns 0 on miss).

**Wave 3 — accepted with these deltas:**
- `utinni_wsSaveSnapshot`: returns a typed result, not bool — `0` ok; distinct codes for
  no-loose-search-path, id-int32-overflow, write-failure (you'll want distinct editor messages;
  "save silently did nothing" is the failure mode this whole consult exists to prevent). Filters:
  authored-only + tombstone-skip (recursive). The OTNL template-name table is written WHOLE
  (excluded buildout nodes may leave unused names in it — harmless, keeps every surviving node's
  name index valid without a remap pass). Internally this lands as a filtered-save entry point on
  `WorldSnapshotReaderWriter` (additive method, no layout change — sharedUtility stays
  ABI-compatible with the tools that link it) rather than a shim-side reimplementation of the
  format; `Node::save` is private and the format should have exactly one writer.
- NEW: `utinni_wsGetSavePath(char* buf, int cap)` — resolved save root for your picker's
  directory-listing union (§5.1d(ii)). Copy-out, returns needed length, 0-length = no loose path
  configured (save would fail closed too).
- `utinni_wsUnloadSnapshot` / reload via advertised `load`: accepted. Note reload-after-save works
  only because the save shim invalidates the negative cache (finding #1) — no consumer action.

**Mechanics:** unchanged from your §7 — per wave: +1 `ENGINE_HOOKPOINTS_VERSION`, NAME ADDs,
constant `&fn` rows, byte-identical `.h/.inc` resync + sha256 both repos, `dumpbin /exports` +
boot smoke, committed before handback. All shims game-thread-only; graceful degradation stands.

## 3. What we need back

Rev-3 frozen Wave-1 row table (5 rows as sketched; none of our deltas touch Wave 1). Flag now if
you want `childCount` in `UtinniWsNodeInfo` (§5.4) so the struct freezes once. Wave-2/3 row names
can freeze per-wave as usual.
