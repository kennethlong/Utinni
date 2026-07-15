# Provider Request — Goal B rev-3: FROZEN Wave-1 row table (read/browse)

**Status:** REQUEST (rev-3 — Wave-1 rows FROZEN, ready to implement) · 2026-07-15
**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Baseline:** contract **v16 / 121 names**
**Responds to:** `2026-07-15-utinni-goalB-snapshot-editor-consult-ANSWERS.md` (provider rev-2 ANSWERS)
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-15-utinni-goalB-wave1-rows-REQUEST.md`;
the Utinni copy governs.

Your ANSWERS are accepted **in full — no pushback on any §5 decision or §4 delta**. All five
provider-only findings are internalized into the consumer ledger (§3 below). Per your §3 ask, this doc
freezes Wave 1 and answers the one open struct question: **yes to `int32 childCount`** — the placements
table wants per-row child counts for its expander affordance anyway, and it saves a per-row call on the
enumeration path. The struct freezes once, with it in (§2).

---

## 1. FROZEN Wave-1 row table — 7 names, v16 → **v17**, 121 → **128 names**

All `extern "C"` `__cdecl`, in `WorldSnapshot.cpp` (the file-local-singleton pattern), game-thread-only,
graceful-degradation rows (missing shim → affordance dark, never a crash). Shared contracts, pinned per
your §2: **enumeration is live, authored-only** (skip tombstones per §5.1b AND buildout per §5.1a — the
retained `ms_buildoutObjects` filter, sign-check assert as belt-and-braces); **reads inherit the
`finishLoadNow()`-when-`ms_parsePending` discipline**; enumeration indices are transient (consumer
snapshots ids, never holds an index across calls that could straddle a load/unload).

| # | Export | Signature | Contract |
|---|--------|-----------|----------|
| 1 | `utinni_wsGetNodeCount` | `int (void)` | Top-level authored non-tombstone count. `0` = empty or no snapshot loaded. |
| 2 | `utinni_wsGetTopNodeIdAt` | `__int64 (int index)` | Id of the index-th enumerable top-level node. `0` = out-of-range. |
| 3 | `utinni_wsGetChildCount` | `int (__int64 id)` | Enumerable (non-tombstone) direct-child count. `0` = miss/tombstone/leaf. |
| 4 | `utinni_wsGetChildIdAt` | `__int64 (__int64 id, int index)` | Id of the index-th enumerable child. `0` = miss/out-of-range. |
| 5 | `utinni_wsGetNodeInfo` | `int (__int64 id, UtinniWsNodeInfo* out)` | POD-out per §2. Caller fills `out->size` FIRST; provider writes `min(callerSize, providerSize)`, zeroes any trailing caller space it understands. Returns `1` ok, `0` miss/tombstone (per your §5.4: tombstones return miss, so flags bit0 reads 0 in practice). |
| 6 | `utinni_wsGetNodeTemplateName` | `int (__int64 id, char* buf, int cap)` | Copy-out: copies `min(cap, needed)` bytes (NUL-terminated when it fits). Returns needed length **including the NUL**; `0` = miss. |
| 7 | `utinni_wsGetGeneration` | `int (void)` | Bumps on `load`/`unload`/`clear` ONLY (your §2 pin). Consumer invalidates cached rows + undo targets on change. |

Reused, no new rows (unchanged from rev-2): `worldSnapshot::{load, moveObject, getLoadingPercent,
detailLevelChanged}`, `network::getObjectById` (v12). The advertised `worldSnapshot::removeObject` is
now on the consumer's NEVER-CALL list (your finding #3) — it stays advertised for the wild, the editor
will not touch it.

## 2. FROZEN `UtinniWsNodeInfo` (shared contract header, version-gated)

```c
// size-first protocol: caller sets size = sizeof(UtinniWsNodeInfo) before the call.
// Fixed-width members only. real == float verified provider-side (§5.4 ANSWERS).
struct UtinniWsNodeInfo {
    unsigned __int32 size;            // 0  — caller fills first
    unsigned __int32 flags;           // 4  — bit0 = deleted (reads 0 in practice, kept for shape
                                      //      stability); bit1 = buildout (RESERVED, always 0 in v1,
                                      //      against the §5.1a enumeration extension)
    __int64          containedById;   // 8
    __int32          cellIndex;       // 16
    unsigned __int32 portalLayoutCrc; // 20
    float            radius;          // 24
    float            transform[12];   // 28 — row-major 3x4, position = column 3 (layout-exact
                                      //      per your §5.4 confirmation)
    __int32          childCount;      // 76 — rev-3 addition: your §5.4 offer, ACCEPTED. Enumerable
                                      //      (non-tombstone) direct-child count, same value as row 3.
};                                    // sizeof = 80 (x86 MSVC, __int64 8-aligned at offset 8)
```

## 3. Consumer ledger — obligations we picked up from your ANSWERS (recorded so nothing regresses)

- **Never call advertised `removeObject` from the editor** (finding #3: irrecoverable live-object leak +
  id-zeroing). All editor removes wait for Wave 2's `utinni_wsRemoveNode`.
- **`wsAddNodeAt` replay batching is a hard contract** (Wave 2): top node + ALL children re-added in ONE
  game-thread marshal batch, before the next update tick — else the streaming recursion spawns a partial
  subtree. Our undo executor will marshal the whole recorded subtree as one closure.
- **Gizmo v1 clamps to same-container motion**; cell-contained writes are parent-relative (`_p`). A drag
  across a cell boundary is remove + re-add — deferred until both primitives exist.
- **Picker unions** `treeFile::enumerateFiles` with a plain directory listing of
  `<utinni_wsGetSavePath()>/snapshot/` (Wave 3) — loose saves never appear in the TOC enumeration.
- **Install-scan id floor** goes in via `utinni_wsConfigureIdAllocator(floor, ceiling)` (Wave 2, your
  NEW row — accepted); default ceiling `0x1000000` stands as consumer-supplied convention.
- Wave-2/3 deltas otherwise accepted as written: `wsAddObject` pre-validation before minting,
  `wsAddNodeAt` no-immediate-spawn semantic + fail-closed set (including the missing-container
  pre-check, finding #2), `wsRemoveNode` per your §5.5 six-step teardown, `wsSaveSnapshot` typed result
  codes + filtered-save entry point on the ReaderWriter + negative-cache invalidation (finding #1),
  `wsGetSavePath` copy-out. Wave-2/3 row tables freeze per-wave as usual.

## 4. Mechanics (unchanged, your §2 "Mechanics" pin)

+1 `ENGINE_HOOKPOINTS_VERSION` (→ **v17**), 7 NAME ADDs (→ **128**), constant `&fn` rows,
byte-identical `.h/.inc` resync + sha256 both repos, `dumpbin /exports` + boot smoke, tree COMMITTED
before handback. SWGEmu byte-unchanged (D-00).

**Ask:** implement Wave 1 per §1/§2 and hand back (HANDBACK doc + commit sha). On receipt we run the
consumer bind wave (slots + `world_snapshot.cpp` reroute on advertised + managed int64-id cleanup) and
gate on headless + maintainer live smoke before requesting Wave 2.

---

## 5. rev-3.1 addendum — your revised ANSWERS acknowledged; Wave-1 freeze UNAFFECTED

Your post-review revision of the ANSWERS (same file, re-delivered ~10:43 after this request went out
at ~10:33; 3 BLOCKER / 3 MAJOR / 2 MINOR folded) crossed this request mid-flight. We diffed both
versions: **nothing in the revision touches §1/§2 above — the Wave-1 row table, contracts, and
`UtinniWsNodeInfo` (with `childCount`) stay frozen exactly as written.** Proceed on §1/§2 as-is.

The §3 consumer ledger updates to your revised Wave-2/3 semantics (recorded here so the ledger has
one current home; all ACCEPTED, still no pushback):

- `wsRemoveNode` returns are now tri-state: `1` ok · `0` miss · distinct **"occupied"** code
  (non-client-cached object inside the containment subtree — the guard that keeps a building delete
  from cascade-deleting the player standing in it). Editor UX will surface "step/teleport out of the
  building first" on that code; no silent relocation in v1 — agreed.
- `wsAddNodeAt` spawn semantics per your redesign: data re-add + (top-level) sphere handle + diff-
  sentinel dirty, (child under a live spawned parent) immediate spawn, (parent not spawned) data-only.
  The **one-batch subtree replay contract stands** and remains ours to honor. Fail-closed set now also
  includes a LIVE object holding the id (`NetworkIdManager`).
- Allocator additionally checks `NetworkIdManager` — no consumer change (our install-scan floor still
  arrives via `wsConfigureIdAllocator`).
- Save error codes now also include **destination-shadowed** (post-write `TreeFile::getPathName`
  verification) and **buildout-set integrity** (non-negative id in the retained set — the v2
  positive-objid hole). Editor will message each distinctly.
- `wsUnloadSnapshot` resetting the sticky `ms_sceneName` (and the pending lists needing no purge) are
  provider-internal — noted, no consumer action.
