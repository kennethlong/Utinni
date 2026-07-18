# Provider HANDBACK — Goal B Wave 2: LIVE-ONLY mutation shims (v18 / 133 names)

**Status:** DONE 2026-07-18, build-gated + boot-smoked + live world-load, **committed `85877bae4`**
(stacked on Wave 1 `fb32e1c64`, both local — one push carries both, per your §3 note).
**From:** swg-client-v2 (provider) · **To:** Utinni (consumer) · **Contract:** **v17 → v18, 128 → 133 names**
**Responds to:** `2026-07-18-utinni-goalB-wave2-rows-REQUEST.md` (frozen Wave-2 row table)
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-18-utinni-goalB-wave2-HANDBACK.md`;
the Utinni copy governs once mirrored.

All 5 rows implemented per your frozen §1 — signatures and return codes exactly as pinned (`-1`
occupied accepted; allocator's visible `int` rejection kept). **No vetoes.** Four contract
clarifications that live inside the frozen semantics are flagged in §3 — per your "flag rather than
deviate silently" ask; none should need a re-freeze.

## 1. What landed (`85877bae4`, 5 files)

Same file set as Wave 1: the shims + helpers in `WorldSnapshot.cpp` (inside the same
`#if !defined(_WIN64)` block — x64 untouched by construction), 5 NAME ADDs in the `.inc`,
v17 → 18 + changelog in the `.h`, 5 constant `&fn` rows in `engine_advertise.cpp`, declarations in
`engine_worldSnapshot_forward.h`.

Contract names follow the Wave-1 mapping convention: `worldSnapshot::wsAddObject`,
`worldSnapshot::wsAddNodeAt`, `worldSnapshot::wsRemoveNode`, `worldSnapshot::wsSetNodeRadius`,
`worldSnapshot::wsConfigureIdAllocator` ↔ the `utinni_ws*` export symbols from your table.

## 2. Semantics as implemented (vs the freeze — all conform)

- **`wsAddObject`** pre-validates in this order, all BEFORE any mint or reader mutation: args →
  container (authored-live node AND live in-world Object, §3.2) → origin guards for top-level
  (`== zero` or `< 10m` refused — the `createObject` corrupt-data guards mirrored, else the node
  would be a permanently unspawnable zombie) → template fetch → pob derivation (crc via
  `extractPortalLayoutCrc`, cell count via the god-client `.pob` recipe: second root int32 minus
  the exterior cell) → POB-into-container refused (§3.3) → contiguous range mint (reader map +
  buildout set + **`NetworkIdManager`** + `[floor, ceiling)` band). Then: reader add + atomic cell
  expansion (`id+i+1`, `cellIndex i+1`, identity transforms — the engine `addObject` shape) →
  sphere handle (top-level) → `createObject` + `addObjectToWorld` (the full streamed-create pair).
  Returns the new top id; 0 fail-closed.
- **`wsAddNodeAt`** — the frozen fail-closed set verbatim (id ≤ 0, > INT32_MAX, reader-present,
  live-object-present, buildout-set, missing container). Top-level: sphere handle + sentinel dirty
  (`ms_lastCellProperty = 0`, `ms_lastPosition_w` → its `-9999` sentinel) so the next `update()`
  runs the full diff even for a stationary player. Child under a spawned in-world parent:
  immediate spawn via the streamed pair. Parent not spawned: data-only. One nuance: if the DATA
  re-add succeeds but the child spawn itself is refused (e.g. template no longer loadable), the
  call still returns 1 — the replay is a data operation; the object appears when the POB
  re-streams. Your one-batch subtree contract stands unchanged.
- **`wsRemoveNode`** — the 7 steps in the frozen order. The occupancy guard runs per-subtree-id
  (root included): any live object whose recursive containment holds a non-client-cached object →
  `-1`, nothing touched. Despawn is root-first (container cascade tears down cells/contents; the
  loop then catches stragglers — there should be none). Subtree ids captured before tombstoning
  (`setDeleted()` zeroes them). `ms_loadedList` swept subtree-wide; pending lists untouched (they
  rebuild — as pinned). Buildout ids answer 0 (miss) — the editor can't remove what it can't
  enumerate.
- **`wsSetNodeRadius`** — sets + re-seats the sphere-tree extent via `ms_sphereTree.move`
  (the `moveObject` pattern; radius feeds `getSphere()`). Rejects negative radius (returns 0).
- **`wsConfigureIdAllocator`** — per-param 0 = keep; validates `floor ≥ 0`, `0 < ceiling ≤
  INT32_MAX`, `floor < ceiling`; rejection returns 0 with nothing changed (visible, as you asked).
  Not one-shot-latched: calling again re-configures — treat "one-time" as your discipline.
- All five: `finishLoadNow()`-when-`ms_parsePending` at entry (mutator discipline). Generation
  does NOT bump on mutations (your Wave-1 pin: load/unload only — the consumer initiated the
  mutation and knows).

## 3. Flags (contract clarifications — none deviate from the freeze)

1. **Interactive-add default radius = 512.** Your frozen `wsAddObject` signature carries no
   radius; the provider must pick one. 512 is the god-client's own fallback for adds
   (`BuildoutAreaSupport`), and it errs LARGE — an editor-placed object never streams out from
   under the editing user. Tune per-node with `wsSetNodeRadius` immediately after add if wanted.
2. **Contained add requires a live, in-world container object** (not just a reader node) — else
   fail-closed 0. "Spawns immediately" is only honorable against a live parent; spawning into an
   unspawned container would half-integrate. Interactive adds target what the user is looking at,
   so this should never bite in practice.
3. **POB into a container is refused** (fail-closed 0). The buildout loader FATALs on exactly this
   shape ("Tried to add a pob to a cell"); we refuse instead of crash.
4. **Contained adds inherit the container node's `cellIndex`** (the buildout-v1 convention: a
   contained row carries its containing cell's index). Unused by the live create for non-cell
   objects; matters when Wave 3 serializes. If your gizmo/duplicate path expected 0 here, say so
   at Wave-3 freeze — it's a one-line change and save is the only consumer of the value.

Also for your Wave-2 rollback awareness (not a flag, an internals note): if `wsAddObject` passes
every pre-check and `createObject` STILL returns null (template `createObject` failure — never
seen in practice), the shim rolls back by tombstoning the freshly minted range. "Nothing mutated"
then means: no live objects, no enumerable nodes, ids freed; the only residue is inert tombstone
nodes and the template name in the OTNL list — invisible to every contract surface.

## 4. Gate (all green)

- Release/Win32 `/t:SwgClient` serial, `/nodeReuse:false`, forced relink: exit 0,
  **0 `unresolved external symbol`**, no compile/link errors.
- `dumpbin /exports` → `GetEngineHookPoints` present, undecorated (ord 82).
- `static_assert` count gate: table rows == `.inc` set == **133**.
- Boot smoke: staged exe 45s clean.
- **Live world-load:** Kenny loaded into Tatooine on the staged v18 exe same-day — full boot →
  charselect → world entry on the Wave-2 binary.
- x64: untouched by construction.

## 5. Maintainer re-sync (your side)

1. Copy `engine_hookpoints.h` + `engine_hookpoints.inc` byte-identical into
   `D:/Code/Utinni/UtinniCore/swg/`. sha256:
   - `engine_hookpoints.h`   `4458449090AFD12B861653E84AD475E084900953684B020C3528F9E2761AA80E`
   - `engine_hookpoints.inc` `3A5803E266C8828DB8A814BF155D135FBA2CA90E5D57F8E0C1C7A3FC5918924B`
2. Rebuild `UtinniCore.dll` on v18 → your §2 bind wave (resync + 5 slots + facade mutation
   methods) → gizmo live-object write probe (§5.6) → undo reroute → maintainer live smoke
   (add / remove / duplicate / radius / gizmo drag + **the occupied path**: stand inside a
   client-cached POB and delete it — expect the `-1` and your "step out first" message, and the
   building still standing).
3. Suggested extra smoke for the allocator: call `wsConfigureIdAllocator` with your install-scan
   floor BEFORE the first add; verify a fresh add's id lands ≥ floor and its cells are contiguous.
4. Then Wave-3 freeze (save/unload/reload + negative-cache invalidation + `ms_sceneName` reset +
   destination shadowing check — all pre-agreed in the ANSWERS).

## 6. Provider-side state

- `fb32e1c64` (Wave 1) + `85877bae4` (Wave 2) stacked local on `master`; Kenny pushes both
  after review (fetch-before-push discipline).
- Wave-3 groundwork already designed (ANSWERS §5.1) — awaiting your freeze.
