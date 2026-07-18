# Provider Request — Goal B Wave 2: FROZEN row table (LIVE-ONLY mutation)

**Status:** REQUEST (Wave-2 rows FROZEN, ready to implement) · 2026-07-18
**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Baseline:** contract **v17 / 128 names**
**Precondition met:** Wave 1 live smoke **PASSED 2026-07-18** (maintainer, advertised client): placements
table populated with 5449/5449 authored naboo rows via the wsGet* path, `(live scene)` arming verified,
generation counter visible and bumping across a scene change (gen 2 on screen). One consumer-side panel
gating fix fell out (UtinniPlugins `286fd43` — normal login never fires the scene-active callback on
advertised; the live view now arms off `WorldSnapshotLive.isAvailable()` alone).
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-18-utinni-goalB-wave2-rows-REQUEST.md`;
the Utinni copy governs.

Semantics are exactly your revised ANSWERS (§5.2/5.3/5.5 + the §4 Wave-2 deltas, all previously
accepted in our rev-3.1 addendum — occupancy-guarded remove, redesigned `wsAddNodeAt` spawn, allocator
`NetworkIdManager` check). This doc only pins names, signatures, return codes, and the version bump.
Nothing here touches disk — Wave 2 is explicitly non-persistent; Wave 3 (save/unload/reload + the
negative-cache and `ms_sceneName` work) freezes separately after the Wave-2 smoke.

---

## 1. FROZEN Wave-2 row table — 5 names, v17 → **v18**, 128 → **133 names**

All `extern "C"` `__cdecl`, in `WorldSnapshot.cpp`, game-thread-only, graceful-degradation rows.
Transform WRITE stays the advertised `worldSnapshot::moveObject` (no new row); live refresh stays
`detailLevelChanged`.

| # | Export | Signature | Contract |
|---|--------|-----------|----------|
| 1 | `utinni_wsAddObject` | `__int64 (const char* sharedTemplateFilename, const float* transform12, __int64 containedById)` | Interactive add. Pre-validates EVERYTHING before minting or mutating the reader (your §5.3: origin-distance guards, template resolvable, full contiguous id range free against reader map + buildout set + `NetworkIdManager` + floor/ceiling band, container exists when `containedById != 0`). Provider mints `id..id+cellCount`; POB cells expand atomically; routes through the FULL streamed-create bookkeeping (sphere handle + in-world mark + `ms_loadedList`). Spawns immediately. Returns the new top id; `0` = fail-closed, nothing mutated. `transform12` = row-major 3x4, position column 3, parent-relative. |
| 2 | `utinni_wsAddNodeAt` | `int (__int64 explicitId, __int64 containedById, const char* templateFilename, int cellIndex, const float* transform12, float radius, unsigned int portalLayoutCrc)` | The UNDO-REPLAY primitive, per your redesigned spawn semantics: data re-add at the EXPLICIT id; top-level → sphere handle + dirty the update-diff sentinels (`ms_lastPosition_w`/`ms_lastCellProperty`); child whose parent is spawned and in-world → immediate spawn via the streamed pair; parent not spawned → data-only. Returns `1` ok, `0` fail-closed (nothing added) on: id present in reader, a LIVE object holding the id (`NetworkIdManager`), id ≤ 0, id > INT32_MAX, id in the buildout set, or `containedById` non-zero and missing (the FATAL pre-check, finding #2). |
| 3 | `utinni_wsRemoveNode` | `int (__int64 id)` | Your §5.5 7-step teardown, occupancy guard load-bearing: subtree id capture FIRST → occupancy check (`isClientCachedOnly` recursive) → sphere handle → node `removeFromWorld` → live Object delete (container cascade) → `ms_loadedList` subtree sweep (no pending-list purge — they rebuild) → tombstone every subtree id. Returns `1` removed · `0` miss (null-safe undo contract) · **`-1` occupied** (a non-client-cached object inside the containment subtree; editor surfaces "step out of the building first"). Veto the `-1` value freely — we only need the three outcomes distinguishable. |
| 4 | `utinni_wsSetNodeRadius` | `int (__int64 id, float radius)` | `1` ok, `0` miss/tombstone. |
| 5 | `utinni_wsConfigureIdAllocator` | `int (__int64 floor, __int64 ceiling)` | One-time, optional; `0` for either param = keep default (seed = max positive authored id + 1; default ceiling `0x1000000`, our consumer convention). This is where our install-scan floor lands. Returns `1` accepted, `0` rejected (invalid band — e.g. ceiling > INT32_MAX, floor ≥ ceiling); veto to `void` if you'd rather fail silently, but we'd like the rejection visible. |

## 2. Consumer obligations (already committed, restated for the record)

- All five calls game-thread marshaled; every op miss-safe by id.
- **`wsAddNodeAt` batching is a hard contract:** a recorded subtree replays top node + ALL children in
  ONE game-thread marshal batch, before the next update tick.
- The editor never calls the advertised `worldSnapshot::removeObject` row (finding #3).
- Gizmo v1 clamps to same-container motion; cell-contained writes parent-relative; drag across a cell
  boundary = remove + re-add (deferred until both primitives smoke).
- On the `-1` occupied return the editor messages the user; no silent relocation in v1.
- Consumer-side Wave-2 work on our HANDBACK receipt: bind wave (resync + 5 slots + facade mutation
  methods) → gizmo live-object write probe (§5.6 — we come back with evidence only if the notify path
  is dead) → undo-command reroute to `wsAddNodeAt`/`wsRemoveNode` on advertised → maintainer live smoke
  (add / remove / duplicate / radius / gizmo drag + the occupied path in a POB) → then Wave-3 freeze.

## 3. Mechanics (unchanged)

+1 `ENGINE_HOOKPOINTS_VERSION` (→ **v18**), 5 NAME ADDs (→ **133**), constant `&fn` rows,
byte-identical `.h/.inc` resync + sha256 both repos, `dumpbin /exports` + boot smoke, tree COMMITTED
before handback. SWGEmu byte-unchanged (D-00). Note `fb32e1c64` (Wave 1) is still local on your side —
fine by convention, just keep Wave 2 stacked on it so one push carries both.

**Ask:** implement Wave 2 per §1 and hand back (HANDBACK doc + commit sha). Flag any return-code or
signature veto in the HANDBACK rather than deviating silently.
