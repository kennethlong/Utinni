# Provider Design Consult — Goal B: the Snapshot editor on the advertised client

**Status:** CONSULT (design decisions wanted BEFORE any row table is frozen) · **rev-2**, 2026-07-13
**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Baseline:** contract **v16 / 121 names**
**Review ledger:** rev-1 → 2-AI adversarial review (Codex + tree-verifying Sonnet) → **9 findings folded
in** (5 save-correctness/data-corruption, undo-replay primitive, enumeration zombies, id policy, save-path
resolution). rev-2 is the post-review shape.
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-13-utinni-goalB-snapshot-editor-consult.md`;
the Utinni copy governs.

This is the LAST major editor still SWGEmu-only. Its native surface today is ~25 consumer wrappers over
the 2002-layout `WorldSnapshotReaderWriter` singleton + raw `Node` walks — the C++-object ABI rule at
struct scale, so none of it ports. We've inventoried the editor's complete operation set, verified your
tree, and had the design adversarially reviewed. **§5 holds the decision points; the save-correctness
cluster (§5.1) is the heart of this consult — everything else is comparatively mechanical.**

---

## 0. TL;DR

Proposed shape: an **id-keyed, primitives-only shim API over your live `ms_reader`** — every operation
keyed by `int64 networkIdInt`, no `Node*` ever crossing, `extern "C"` shims in `WorldSnapshot.cpp` (the
established file-local-singleton pattern), landed in 3 smoke-gated waves: **read/browse → live-only
mutation → persistence**. Four of your advertised `worldSnapshot` rows are reused as-is. Review verdict
we internalized: the id-keyed direction is sound, but **save is NOT a one-line `ms_reader.save()` wrap**
— your reader is a merged pot of authored-`.ws` + buildout + tombstoned nodes, and the on-disk format
truncates ids to int32. Wave 3 needs your design input more than your labor.

## 1. What the editor needs (verified requirement set)

Full inventory swept 2026-07-13 (native `world_snapshot.{h,cpp}`, managed WorldSnapshotImpl /
SnapshotPanel / placements + 2 bulk forms / undo commands). Condensed:

| Group | Operations | Notes |
|-------|-----------|-------|
| Lifecycle | load CURRENT scene's snapshot, unload, reload, **save** (save-as: §5.1d) | |
| Enumeration | top-level count + id-by-index; child count/id-by-index | placements table today walks top-level only; child enumeration serves the panel's subtree ops |
| Lookup | node by networkId — the HOT path (every target change) | your `m_networkIdNodeMap` covers subtree nodes too → the consumer's SWGEmu-era parent-walk dies |
| Field reads | containedById, cellIndex, transform, radius, portalLayoutCrc, deleted, objectTemplateName | POD-out struct, §5.4 |
| Field writes | transform (per-frame during gizmo drag), radius | transform write = your advertised `moveObject` (verified: node + sphere-tree, NOT the live Object — correct, the gizmo moves the live Object separately; see the §5.6 prerequisite) |
| Mutation (interactive) | add node (template + transform [+ container]) with atomic POB cell expansion, provider-minted id; remove node; duplicate | |
| Mutation (replay) | **add-at-EXPLICIT-id** (top node AND each child at recorded ids) | the undo/redo model replays a recorded node at its original id — a mint-only add cannot express Undo(Delete)/Redo(Add). Review-caught HIGH. |
| Live reflection | despawn on remove; engine refresh (`detailLevelChanged`, advertised) | |
| Id allocation | provider-owned; contiguous `id..id+cellCount` per add; **int32-safe** (§5.1c) | |

**Consumer-side contracts that stay:** game-thread-only + on-demand (everything is update-loop
marshaled; only the gizmo-drag transform write is per-frame); every id-keyed op null-safe on a missing
id (the undo system re-resolves by id and must miss safely); your mutators' existing
`finishLoadNow()`-when-`ms_parsePending` discipline inherited by the shims.

## 2. Provider-side reality (verified in your tree, 2026-07-13)

- `ms_reader` is file-scope anon-namespace in `WorldSnapshot.cpp` → everything is shims in that TU.
- Advertised + consumer-safe as-is: `worldSnapshot::{load, moveObject, removeObject, getLoadingPercent,
  detailLevelChanged}`. Advertised but consumer-UNCALLABLE: `addObject` (`CrcString const&` +
  `std::string const&` — the sysmsg-rev-2 trap) → needs an `extern "C"` twin.
- `WorldSnapshot::addObject` (WorldSnapshot.cpp:1287) does add + POB cell children (`id+i+1`) +
  `createObject` + `object->addToWorld()` — **but NOT the streamed-create bookkeeping**: it skips the
  `addObjectToWorld()` helper (node in-world mark + `ms_loadedList` push, :337-344) and takes no
  sphere-tree handle. The add shim should route through the same bookkeeping as streamed creates (§5.3).
- `WorldSnapshotReaderWriter::removeNode` (:933) tombstones (`setDeleted()` → also zeroes the id) +
  erases the map entry — the node STAYS in `m_nodeList`/parent children. `Node::save`/top-level `save`
  have **no deleted filter** → tombstones serialize, and they'd enumerate as phantom id-0 rows forever.
- `loadOneBuildoutArea` inserts buildout rows **into `ms_reader`** (:734), ids carrying an
  `areaIndex << 48` shift; `ms_buildoutObjects` (the provenance set) is **cleared at parse completion**
  (:852) — origin information is discarded.
- The on-disk `TAG_0000` node payload writes ids as **`(int32)` casts** (ReaderWriter.cpp:430-431) and
  `load_0000` reads int32 — the file format is 32-bit-id, whatever the memory model says. `m_eventName`
  is **not serialized** at all in this form.
- `Iff::write` → `Os::writeFile` = **CWD-relative OS path**, while the editor's snapshot picker reads
  through the TreeFile-virtualized Repository — two different namespaces on the advertised client
  (its TRE data dir is a separate configured location).
- `real` == `float` (FirstSharedFoundation.h:46); `Transform` = `float[3][4]`, position in column 3 —
  layout-compatible with the consumer's mirror.
- `ms_reader.addObject` silently no-ops the map insert on id collision for `cellIndex < 0` while still
  linking the node into the list → a colliding node is enumerable but unreachable by `find(id)`.

## 3. Why id-keyed shims (options considered)

- **A. Opaque `Node*` handle API** — rejected: handle lifetime is hostile (unload/reload/scene change +
  your incremental load mutates the node set), and it's the widest contract (~25 rows).
- **B. Serialized snapshot exchange** (consumer edits the `.ws` as a managed IFF doc, provider reflects
  live) — rejected as primary: dual source of truth against a streaming engine, plus §2's format
  findings mean the managed codec inherits every save-correctness problem anyway. Kept in the back
  pocket for offline editing.
- **C. Id-keyed primitives-only shims (RECOMMENDED)** — ids are already the editor's resolution key,
  miss-safe by construction, no layout crosses. The review confirmed the direction and moved the risk
  where it belongs: persistence semantics (§5.1), not transport.

## 4. Candidate shim set (DRAFT — you own final shapes; all `extern "C"`, in WorldSnapshot.cpp)

**Wave 1 — read/browse the CURRENT scene's loaded snapshot** (panel + placements table light up;
explicitly NOT arbitrary-`.ws` browsing; reads force-finish the incremental parse on first call):
| Shim | Sketch |
|------|--------|
| `utinni_wsGetNodeCount` / `utinni_wsGetTopNodeIdAt` | `int(void)` / `__int64(int index)` — **skip-tombstone contract**: deleted nodes are never enumerated (else they surface as phantom id-0 rows colliding with the miss sentinel) |
| `utinni_wsGetChildCount` / `utinni_wsGetChildIdAt` | `int(__int64 id)` / `__int64(__int64 id, int index)` — same skip-tombstone contract |
| `utinni_wsGetNodeInfo` | `int(__int64 id, UtinniWsNodeInfo* out)` — POD-out per §5.4; 0 on miss/tombstone |
| `utinni_wsGetNodeTemplateName` | `int(__int64 id, char* buf, int cap)` — copy-out; returns needed length |
| `utinni_wsGetGeneration` | `int(void)` — bumps on load/unload/clear so the consumer invalidates cached rows + undo targets across snapshot generations |

**Wave 2 — LIVE-ONLY mutation** (explicitly non-persistent: gizmo/add/remove/duplicate work in-session;
nothing touches disk until Wave 3 lands the §5.1 answers):
| Shim | Sketch |
|------|--------|
| `utinni_wsAddObject` | `__int64(const char* sharedTemplateFilename, const float* transform12, __int64 containerId)` — provider mints the id (+cell range, §5.2 policy), derives pobCrc/cellCount from the template, routes through the SAME create/bookkeeping path as streamed nodes (§5.3); returns new id, 0 = fail-closed |
| `utinni_wsAddNodeAt` | `int(__int64 explicitId, __int64 containedById, const char* templateFilename, int cellIndex, const float* transform12, float radius, unsigned int portalLayoutCrc)` — the UNDO-REPLAY primitive: re-adds a recorded node at its exact id (caller replays children one-by-one, so POB subtrees restore at their recorded ids). MUST fail closed (return 0, add nothing) if the id is already present — never shadow (§2 collision note) |
| `utinni_wsRemoveNode` | `int(__int64 id)` — FULL logical delete: unlink from enumeration (or equivalent skip-tombstone guarantee), map erase, safe live despawn (§5.5) |
| `utinni_wsSetNodeRadius` | `int(__int64 id, float radius)` |
| (transform write) | none — consumer calls advertised `moveObject`; live-object motion is the gizmo's job (§5.6 prerequisite) |

**Wave 3 — persistence** (shapes depend entirely on the §5.1 answers):
| Shim | Sketch |
|------|--------|
| `utinni_wsSaveSnapshot` | `int(void)` — current scene's `.ws` only in v1; **authored-nodes-only** (buildout excluded), **tombstones excluded**, id-range enforcement per §5.1c, destination per §5.1d |
| `utinni_wsUnloadSnapshot` / (reload) | `void(void)`; reload = unload + advertised `load(currentScene)` |

Reused, no new rows: `worldSnapshot::{load, moveObject, removeObject, getLoadingPercent,
detailLevelChanged}`, `network::getObjectById` (v12).

## 5. DECISION POINTS

**5.1 THE SAVE-CORRECTNESS CLUSTER (the consult's center of gravity).** Your runtime never calls
`ms_reader.save()`; making the editor's Save real needs four coupled decisions:
   - **(a) Provenance.** Buildout rows live in the same reader and their origin set is discarded at
     parse completion. Save must write authored-`.ws` nodes only. Cheapest fix we can see: retain
     `ms_buildoutObjects` for the session (don't clear at :852, or copy it aside) and filter save +
     enumeration by it; alternatively tag nodes at insert. Your call — you know what else reads that set.
   - **(b) Deleted nodes.** Tombstones must not serialize (today they would, with zeroed ids). Save-skip
     recursively, or make remove actually unlink (which Wave 2's remove wants anyway — one fix can serve
     both).
   - **(c) Id width policy.** The `TAG_0000` on-disk format is int32; memory is int64. For v1 we propose:
     keep the format untouched, allocator mints only int32-safe ids (below the server-id floor —
     consumer treats `>= 0x1000000` as server; confirm the right ceiling), and **save fails closed
     (returns error, writes nothing) if any authored node id doesn't round-trip int32**. A new
     64-bit-id form (`0002`) would orphan every other `.ws` consumer — we'd rather not, veto if you
     disagree. Note buildout ids (`areaIndex << 48`) are un-saveable by construction — provenance
     filtering (a) also solves their truncation hazard.
   - **(d) Destination + picker visibility.** `Iff::write` is CWD-relative; the editor's picker
     enumerates the TreeFile-virtualized `snapshot/` directory. On the advertised client those are
     different namespaces (stage CWD vs the configured TRE data dir). Where should Save write so the
     engine + picker can load the result back — a loose search-path dir the TreeFile layer already
     honors? Fixed `<cwd>/snapshot/` + the consumer registers it as a search path? You know the
     search-path config best. Save-as (arbitrary name) is deferred until (a)–(d) are settled; v1 =
     current scene's snapshot only.
2. **Id allocation policy (interactive adds).** Provider-owned. Not naive `max+1`: a free CONTIGUOUS
   range search (`id..id+cellCount`) over the full current reader state (including tombstoned and
   buildout-range ids), below the (c) ceiling, fail-closed. Sanity-check whether max-of-loaded is a safe
   seed or whether cross-snapshot collisions (ids in other `.ws` files that aren't loaded) matter for
   your streaming — the consumer's SWGEmu-era allocator scanned every `.ws` on disk at install
   precisely to avoid that; we can keep doing that scan consumer-side and PASS a floor into the
   allocator if useful.
3. **Add bookkeeping.** Should the add shim route through your streamed-create path
   (`addObjectToWorld` + sphere-tree insert + `ms_loadedList`) rather than `WorldSnapshot::addObject`'s
   bare `object->addToWorld()`? We believe yes (else the added node is invisible to
   streaming/update/unload bookkeeping); you know what the sphere-tree insert needs (a
   `SpatialSubdivisionHandle` per node) and whether mid-session insert is safe.
4. **POD-out struct.** `UtinniWsNodeInfo`, defined in the shared contract header, version-gated:
   fixed-width members only (`int64`, `int32`, `uint32`, `float` — no bool/enum), caller fills
   `size` first, provider writes `min(callerSize, providerSize)` and never assumes growth, trailing
   reserved fields zeroed. Layout: `{ uint32 size; uint32 flags(bit0=deleted); int64 containedById;
   int32 cellIndex; uint32 portalLayoutCrc; float radius; float transform[12] /* row-major 3x4,
   position = column 3, provider real==float verified */ }`. Veto if you'd rather per-field rows.
5. **Remove = full despawn.** rev-1 offered a menu here; review killed that — an editor delete that
   leaves the object visible isn't delete. Requirement: remove unlinks the node AND despawns the live
   streamed Object (your side knows the engine-safe way — the consumer's SWGEmu path did
   `getObjectById(id)->remove()`). If a mid-frame external despawn is genuinely unsafe, say so and
   we'll gate delete behind "engine-refresh + persists till scene change" as an explicit downgrade,
   not a default.
6. **Gizmo prerequisite (consumer-side, flagging for completeness).** Live gizmo drag moves the spawned
   Object directly (raw transform write + `PositionAndRotationChanged` notify) and then syncs the node
   via `moveObject`. That live-object write path is UNPROVEN on the advertised client (the gizmo has
   never been enabled there). We'll probe it consumer-side in Wave 2; if the notify/write path needs a
   row, that becomes a small follow-on ask — nothing for you to do now unless you already know it's
   unsafe.
7. **Cheaper seam we can't see?** Standing question: if your tree already has a cleaner editor path
   (WorldSnapshotViewer plumbing, buildout editor relics), prefer it and tell us the shape.

## 6. Consumer-side plan (context)

On your rev-2 answers we freeze the wave-1 row table. Consumer work per wave: reroute the
`world_snapshot.cpp` wrappers to id-keyed slots on advertised (SWGEmu paths byte-unchanged, D-00; the
Node*-walk guards + §2b CI invariant stay on the SWGEmu-only raw paths); managed editor goes int64-id
clean (additive API only, binary-compat rule); undo commands keep their managed node COPIES but replay
through `utinni_wsAddNodeAt` on advertised (the copies carry exactly the fields the shim takes);
generation counter invalidates stale undo targets across load/unload. Placements table gains child
recursion only if/when wanted — wave 1's child shims are cheap either way. Each wave: headless gates +
maintainer live smoke on the advertised client + an SWGEmu snapshot-editor regression smoke.

## 7. Mechanics (unchanged)

Per wave: +1 `ENGINE_HOOKPOINTS_VERSION`, NAME ADDs, constant `&fn` rows, byte-identical `.h/.inc`
resync + sha256 both repos, `dumpbin /exports` + boot smoke, tree COMMITTED before handback. Graceful
degradation: any missing shim → that affordance stays dark, never a crash. SWGEmu byte-unchanged (D-00).

**Ask:** answer §5 (a paragraph per point; §5.1 is the one that matters), veto/adjust §4 freely, then
we send rev-3 as the frozen wave-1 row table.
