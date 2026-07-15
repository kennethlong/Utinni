# Provider HANDBACK — Goal B Wave 1: snapshot-editor READ shims (v17 / 128 names)

**Status:** DONE 2026-07-15, build-gated + boot-smoked, **committed `fb32e1c64`** (not pushed).
**From:** swg-client-v2 (provider) · **To:** Utinni (consumer) · **Contract:** **v16 → v17, 121 → 128 names**
**Responds to:** `2026-07-15-utinni-goalB-wave1-rows-REQUEST.md` (rev-3 frozen Wave-1 row table)
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-15-utinni-goalB-wave1-HANDBACK.md`;
the Utinni copy governs once mirrored.

All 7 rows implemented exactly per your frozen §1/§2 — no shape deviations, no OMITs. The staged
`SwgClient_r.exe` carries the v17 table.

## 1. What landed (`fb32e1c64`, 5 files)

| File | Change |
|------|--------|
| `clientGame/.../WorldSnapshot.cpp` | The 7 `extern "C" __cdecl utinni_ws*` shims + authored-only helpers + generation counter + `ms_buildoutObjects` retention (see §3) — all in the `ms_reader` TU per the file-local-singleton pattern. Shims wrapped `#if !defined(_WIN64)` (x64 untouched by construction). |
| `SwgClient/.../engine_hookpoints.h` | v16 → **17** + changelog; **`UtinniWsNodeInfo`** added (your §2 FROZEN layout verbatim: 80 bytes, `childCount` at offset 76). |
| `SwgClient/.../engine_hookpoints.inc` | 7 NAME ADDs → **128 names** (contract names in §2 below). |
| `SwgClient/.../engine_advertise.cpp` | 7 constant `&fn` rows (image-valid at load — no dyn[] placeholders needed). |
| `SwgClient/.../engine_worldSnapshot_forward.h` | NEW exe-local forward header (the advertise TU takes the addresses through it). |

## 2. Row-name mapping (the one freeze detail your table left implicit)

Your §1 table identified rows by **export symbol**; the contract is keyed by **`group::name`
strings** from the `.inc`. The mapping (baked into the `.inc` you'll copy verbatim, so no drift is
possible — flagging only so your binder's required-set constants match):

| Contract name (lookup key) | Export symbol |
|---|---|
| `worldSnapshot::wsGetNodeCount` | `utinni_wsGetNodeCount` |
| `worldSnapshot::wsGetTopNodeIdAt` | `utinni_wsGetTopNodeIdAt` |
| `worldSnapshot::wsGetChildCount` | `utinni_wsGetChildCount` |
| `worldSnapshot::wsGetChildIdAt` | `utinni_wsGetChildIdAt` |
| `worldSnapshot::wsGetNodeInfo` | `utinni_wsGetNodeInfo` |
| `worldSnapshot::wsGetNodeTemplateName` | `utinni_wsGetNodeTemplateName` |
| `worldSnapshot::wsGetGeneration` | `utinni_wsGetGeneration` |

## 3. Implementation notes (contract-relevant)

- **Authored-only filter, both prongs:** enumeration skips `isDeleted()` nodes AND ids in the
  retained `ms_buildoutObjects` set; id-keyed reads answer miss for both (`find()` already misses
  tombstones — erased from the map, id zeroed — the buildout check is explicit). The retention
  change: the set is no longer cleared at parse completion (`PP_done`); it still clears on every
  `load()`/`unload()`. Verified again at implementation: nothing else reads it post-parse.
- **`finishLoadNow` discipline:** every NODE read (`wsGetNodeCount/TopNodeIdAt/ChildCount/ChildIdAt/
  NodeInfo/TemplateName`) force-finishes a pending CONSULT-60 incremental parse before answering —
  same as the mutators. **`wsGetGeneration` deliberately does NOT** (pinned in the `.inc` comment):
  it is a pure counter read, so your editor can poll it during a loading screen without forcing the
  phased parse synchronous (which would resurrect the exact zone-in stall CONSULT-60 removed).
  Generation bumps in `unload()` — which every `load()` routes through — so one scene change may
  bump it more than once; compare `!=`, never `+1`.
- **`wsGetNodeInfo`:** size-first per your §2 — caller sets `size` first; provider writes
  `min(callerSize, 80)` bytes with its own `size` field = 80, and never touches caller space beyond
  what it understands. Returns 0 if `out` is null or `out->size < 4`. `flags` bit0 always reads 0
  (tombstones answer miss), bit1 reserved. The transform is composed from the engine `Transform`
  column accessors (I/J/K/position), not a raw memcpy — layout-defined row-major 3×4, position in
  column 3, regardless of `Transform`'s internal representation. `childCount` == the
  `wsGetChildCount` value (enumerable children only).
- **`wsGetNodeTemplateName`:** returns needed length INCLUDING the NUL; copies `min(cap, needed)`
  bytes (NUL included only when it fits — your §1 contract verbatim); `buf == NULL` or `cap <= 0`
  is a pure size query. Returns 0 on miss.
- **Frozen-ABI pins:** `static_assert(sizeof == 80)` + per-field `offsetof` asserts sit next to the
  shims — any future layout drift is a compile error on our side before it can ship.
- **Index transience** is on your side as pinned (snapshot ids, never hold an index across a
  possible load/unload) — enumeration order is `m_nodeList` order and stable between mutations, but
  nothing more is promised.

## 4. Gate (all green)

- Release/Win32 `/t:SwgClient` serial build, `/nodeReuse:false`, forced relink: exit 0,
  **0 `unresolved external symbol`** in the log (the /FORCE mask grep).
- `dumpbin /exports stage/SwgClient_r.exe` → `GetEngineHookPoints` present, undecorated (ord 82).
- `static_assert` count gate compiled: table rows == `.inc` required-set == **128**.
- Struct layout asserts compiled (80/0/4/8/16/20/24/28/76).
- Boot smoke: staged `SwgClient_r.exe` ran 45s clean (past static-init + config + advertise table
  init), zero crash artifacts, killed deliberately.
- x64: untouched by construction (shims compiled away; advertise TU was already Win32-only).

## 5. Maintainer re-sync (your side)

1. Copy `engine_hookpoints.h` + `engine_hookpoints.inc` **byte-identical** into
   `D:/Code/Utinni/UtinniCore/swg/`. sha256 (of our committed working-tree copies):
   - `engine_hookpoints.h`   `CF6185AAC3A7E866D3B0279053073B80ACA041EF1D9EA91100CF76F3C03A2626`
   - `engine_hookpoints.inc` `6934B709CA5A20783E43CA3BF613234DB27B002A0679ACBBB0571FB5957046D7`
2. Rebuild `UtinniCore.dll` on v17; consumer bind wave per your §4 (slots + `world_snapshot.cpp`
   reroute on advertised + managed int64-id cleanup).
3. Live smoke suggestion: inject → resolve 128/128 → load a ground scene → placements table
   populates from `wsGetNodeCount`/`wsGetTopNodeIdAt`/`wsGetNodeInfo` (counts should be
   noticeably SMALLER than SWGEmu-side raw walks — buildout rows no longer enumerate; that's the
   §5.1a contract working, not a bug) → target a snapshot building → `wsGetNodeInfo` transform
   matches the live object → `wsGetGeneration` changes across a scene change and the panel
   invalidates.
4. Then request Wave 2 freeze when ready.

## 6. Provider-side residuals (ours, tracked)

- Wave 2/3 per the accepted ANSWERS deltas (occupancy-guarded remove, `wsAddNodeAt` spawn
  semantics + diff-sentinel dirty, allocator with `NetworkIdManager` check, save destination
  shadowing check + negative-cache invalidation + `ms_sceneName` reset in unload) — awaiting your
  per-wave freeze.
- Not pushed: `fb32e1c64` is local; Kenny pushes after his own look (master is a live
  upstream-integration branch — fetch-before-push discipline).
