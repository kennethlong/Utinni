# Provider Request — Goal B Wave 3: FREEZE (persistence) + four riders

**Status:** REQUEST (Wave-3 persistence rows FROZEN; 4 riders for design/confirm) · 2026-07-18
**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Baseline:** contract **v18 / 133 names**
**Precondition:** Wave-2 mutation smoke **PASSED** on the advertised client (add / remove / duplicate /
radius / undo-redo all verified via UI; id-allocator hardening `d7dba07a6` field-confirmed, re-mints
exact). Two Wave-2 items deferred here as riders (gizmo camera accessors §4C, occupancy guard §4D).
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-18-utinni-goalB-wave3-freeze-REQUEST.md`;
the Utinni copy governs.

Persistence semantics are exactly your ANSWERS §5.1 (a–d) + the §2 Wave-3 deltas — all pre-agreed.
§1 pins names/signatures/returns/version; §4 carries the four riders (two new-row asks with proposed
shapes, two design/answer items). Final name count reconciles at handback once the rider rows land.

---

## 1. FROZEN Wave-3 persistence row table — 3 names, v18 → **v19**, 133 → **136**

All `extern "C"` `__cdecl`, in `WorldSnapshot.cpp`, game-thread-only, graceful-degradation rows.
Nothing here mints or spawns — this is the disk half. `finishLoadNow()`-when-`ms_parsePending` at entry.

| # | Export | Signature | Contract |
|---|--------|-----------|----------|
| 1 | `utinni_wsSaveSnapshot` | `int (void)` | Save the CURRENT scene's authored `.ws`. TYPED result (not bool): `0` ok; distinct nonzero codes for **no-loose-search-path**, **destination-shadowed** (post-write `TreeFile::getPathName` check, §5.1d), **id-int32-overflow**, **buildout-set-integrity** (a non-negative id in the retained set, finding #5), **write-failure**. Filters: authored-only + tombstone-skip (recursive). Destination = `<highest-priority loose SearchPath root>/snapshot/<scene>.ws` absolute; invalidates the searchPath negative-cache for the written name (finding #1). Internally a filtered-save entry point on `WorldSnapshotReaderWriter` (additive method, `Node::save` stays the sole writer). Please publish the exact code→meaning enum in the handback so our editor messages match 1:1. |
| 2 | `utinni_wsGetSavePath` | `int (char* buf, int cap)` | Resolved save root for the picker's directory-listing union (§5.1d(ii)). Copy-out; returns needed length **including the NUL** (matches the Wave-1 `wsGetNodeTemplateName` convention); `buf==NULL`/`cap<=0` = pure size query; **0 = no loose SearchPath configured** (save would fail closed too). |
| 3 | `utinni_wsUnloadSnapshot` | `void (void)` | Unload the current snapshot AND reset the sticky `ms_sceneName` (§2 delta — else the advertised `load(currentScene)` early-outs at WorldSnapshot.cpp:481 and reload returns EMPTY). Reload = `wsUnloadSnapshot` + advertised `worldSnapshot::load(currentScene)`; works with no further consumer action given the negative-cache invalidation in row 1. |

Reused, no new row: `worldSnapshot::load` (reload leg), `wsGetGeneration` (bumps on the unload, so our cached rows + undo targets invalidate across a save/reload).

## 2. Consumer-side Wave-3 plan (context)

On handback: reroute the panel's Save / Save-As / Unload / Reload on advertised to `wsSaveSnapshot` /
`wsUnloadSnapshot` + advertised `load` (SWGEmu keeps the raw `WorldSnapshotReaderWriter::saveFile`
path, D-00); map each typed save code to a distinct editor message; union the picker with a directory
listing of `wsGetSavePath()/snapshot/`. Save-as deferred post-v1 (agreed). We supply the loose
SearchPath via the existing install config (the advertised client already carries one).

## 3. THE SAVE-CORRECTNESS RIDER (must land WITH row 1) — a third provenance class

Wave-2 smoke surfaced a runtime hole your static analysis couldn't see: **id 609,457,649
(server-object range) sits in the LIVE reader tagged authored** on a server session — something
inserts server-streamed content into `ms_reader` at runtime (a `WorldSnapshot::addObject` caller on
the server path is our guess; your tree). It **round-trips int32**, so it is NOT caught by the
id-width fail-closed check, and it is NOT in `ms_buildoutObjects`, so the retained-set filter doesn't
exclude it either. **As written, `wsSaveSnapshot` would serialize these runtime server nodes into the
saved `.ws` as authored world content** (a wandering NPC baked into the planet). This isn't optional
polish — it's a data-corruption path in the save you're about to build.

Ask: fold a third provenance class into the row-1 filter for runtime-inserted / server-session nodes.
Cheapest shapes we see (your call, per the §5.1a pattern): tag ids inserted into the reader OUTSIDE
the `.ws` parse into a second retained set and filter save + enumeration by both; or exclude anything
whose id was never parse-inserted (a "parsed" bit on the node). Enumeration question follows: should
Wave-1's placements table SHOW runtime server nodes? We say NO (same authored-only contract) — which
also means our current server-session placements counts may include them until this lands; we'll
re-check the table once you pick the mechanism.

## 4. RIDERS

**4A — (covered in §3, the provenance class; belongs with row 1.)**

**4B — Targeting filter (NEW row request).** In-world building/static selection on the advertised
client is currently impossible: our `cuiHud::patchAllowTargetEverything` blind-wrote SWGEmu RVA
`0x00BD3FA3` and corrupted NGE CUI code (guarded to a no-op, Utinni `ce0f8c6`). The NGE equivalent —
whatever gates the world-pick/target filter to "everything" vs "targetable creatures only" — needs a
provider entry point. Proposed shape: `void utinni_setTargetEverything(bool enabled)` (or a pair of
advertised rows if it's a member toggle). Needed so the occupied-POB smoke and gizmo selection can
target statics in-world rather than only via the placements table.

**4C — Gizmo camera accessors (NEW rows request).** The live gizmo render is guarded dark on
advertised (Utinni `3d813b2`): `imgui_impl::draw()` reads `camera->projectionMatrix` as a RAW
STRUCT-OFFSET field, and the NGE Camera layout differs → garbage matrix → execute-of-heap-data crash
(cdb-confirmed: `ecx=10.0f` __thiscall through garbage, fault in a mapped buffer). To unlock the
in-world manipulator we need advertised accessors for the two matrices the gizmo consumes: the
camera's **projection matrix** and its **object-to-world transform** (the current-camera o2w). Proposed
shapes (primitives-only, per the ABI rule): `int utinni_getCameraProjectionMatrix(float* out16)` and
`int utinni_getCameraTransformO2W(float* out12)` — copy-out into caller buffers, `0`/`1` valid. If the
live-object WRITE path (setTransform_o2p + PositionAndRotationChanged, your §5.6) also needs a row on
advertised, flag it — we'll probe the write side once the read matrices land.

**4D — Occupancy guard reachability (ANSWER request, no row implied yet).** Per
`2026-07-18-utinni-goalB-occupancy-guard-flag.md` (delivered, reproduced): deleting a POB from INSIDE
returns `OK` not `-1` — the guard doesn't fire because the player appears not to be in the POB's
live-Object containment on the editor scene. We need your read on whether the guard's
`isClientCachedOnly` reachability is correct here, or whether it needs a broader occupancy test, before
we call the `-1` path smoke-verified. No contract change implied unless your answer needs one.

## 5. Mechanics (unchanged)

+1 `ENGINE_HOOKPOINTS_VERSION` (→ **v19**), NAME ADDs (**≥136** — 3 persistence + however many rider
rows you land for 4B/4C), constant `&fn` rows, byte-identical `.h/.inc` resync + sha256 both repos,
`dumpbin /exports` + boot smoke, tree COMMITTED before handback. SWGEmu byte-unchanged (D-00). Please
**exercise a real save + reload-after-save in your gate** (the Wave-2 gate never exercised a real add —
the id-mint bug rode straight through; a save that writes nothing or a reload that comes back empty is
exactly the silent failure this whole consult exists to prevent).

**Ask:** implement the §1 persistence rows + the §3 provenance filter (with row 1), design/confirm the
§4B/§4C rider rows (propose final shapes/count in the handback), and answer §4D. Flag any veto rather
than deviating silently; publish the row-1 result-code enum.
