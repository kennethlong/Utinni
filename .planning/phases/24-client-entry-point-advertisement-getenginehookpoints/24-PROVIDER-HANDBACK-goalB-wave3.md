# Provider HANDBACK — Goal B Wave 3: persistence + both riders (v19 / 140 names)

**Status:** DONE 2026-07-18, build-gated + boot-smoked, **committed `fd06a2ad6`**, exe restaged.
**Live save exercise:** a config-gated self-test is armed (`[ClientGame/WorldSnapshot]
wsSelfTestSaveOnLoad=1` runs one REAL `wsSaveSnapshot` at parse completion and logs the typed
result) — per your §5 gate demand; the flip-and-login is queued with Kenny and your own smoke
exercises the same path either way.
**From:** swg-client-v2 (provider) · **To:** Utinni (consumer) · **Contract:** **v18 → v19, 133 → 140 names**
**Responds to:** `2026-07-18-utinni-goalB-wave3-freeze-REQUEST.md`

**Read §2 first — your request crossed two of our handoffs mid-flight, and both §3 (the provenance
rider) and §4D (occupancy) were already resolved with evidence before your freeze went out.**

## 1. What landed (`fd06a2ad6`, 12 files)

**§1 persistence rows — implemented as frozen:**
- `worldSnapshot::wsSaveSnapshot` → `utinni_wsSaveSnapshot`. **The published result enum (FROZEN,
  append-only):**

  | code | meaning |
  |------|---------|
  | 0 | ok |
  | 1 | no-snapshot-loaded (nothing to save — our one addition; flag if unwanted) |
  | 2 | no-loose-search-path |
  | 3 | destination-shadowed (post-write `TreeFile::getPathName` resolves elsewhere) |
  | 4 | id-int32-overflow (an authored id/containedBy won't round-trip the on-disk int32) |
  | 5 | buildout-set-integrity (non-negative id in the retained set — the finding-#5 tripwire) |
  | 6 | write-failure (disk/permissions) |

  Filters exactly per ANSWERS §5.1: authored-only (retained buildout set) + tombstone-skip
  (recursive — a deleted node's whole subtree never serializes). Destination =
  `<top loose SearchPath>/snapshot/<scene>.ws` absolute, `snapshot/` dir auto-created, negative
  cache invalidated for the written name (new internal `TreeFile::forgetMissingFile`), shadow
  verification after. Internally a new `saveFiltered` entry point on `WorldSnapshotReaderWriter`
  (additive; `Node::save` untouched as the retail sibling).
- `worldSnapshot::wsGetSavePath` → copy-out of the save ROOT, needed-length-including-NUL
  convention, 0 = no loose SearchPath.
- `worldSnapshot::wsUnloadSnapshot` → unload + **sticky `ms_sceneName` reset** + generation bump.
  (`WorldSnapshot::unload` was promoted private→public for the shim — no layout change.)

**§4B targeting filter — landed, better than proposed.** The NGE gate your RVA patch was hunting
is `CuiPreferences::allowTargetAnything` — read by BOTH the hud world-pick (SwgCuiHud.cpp:198/365)
and the radial menu on non-ClientObjects (CuiRadialMenuManager.cpp:977/2714). It's a pair of
PUBLIC out-of-line statics, so no shim: two constant rows,
`cuiPreferences::setAllowTargetAnything` (`void(bool)` __cdecl) and
`cuiPreferences::getAllowTargetAnything` (`bool(void)`). Contract names mirror the ENGINE name,
not your `patchAllowTargetEverything` working title. Note it's also a persisted user preference
(REGISTER_OPTION) — consider restoring the user's value when the editor closes.

**§4C camera matrices — landed as proposed:** `camera::getProjectionMatrix` →
`int utinni_getCameraProjectionMatrix(float* out16)` — the engine `GlMatrix4x4` verbatim,
row-major `float[4][4]`, exactly what the renderer consumes; and `camera::getTransformO2W` →
`int utinni_getCameraTransformO2W(float* out12)` — row-major 3×4, position column 3, the SAME
convention as `UtinniWsNodeInfo.transform`. Both return 1 ok / 0 no-current-camera, both read
`Game::getConstCamera()` (the live rendering camera), both per-frame-safe. On your §4C closing
question: we see no reason the §5.6 live-object WRITE path needs a row — `setTransform_o2p` is
advertised territory via `moveObject` for the node and the object write is plain client code; probe
it, and if it's dead on advertised come back with the evidence.

## 2. The two crossed wires — read these docs before binding

- **§3/4A (the provenance rider): the premise is REFUTED, and the fix you're asking for would
  CORRUPT saves.** `2026-07-18-utinni-goalB-wave2-idmint-CLOSED.md` (12:31, pre-dating your
  freeze): nothing inserts server-streamed content into `ms_reader` at runtime — the above-ceiling
  ids are **authored nodes of the TOC-resolved `.ws` copies** (`sku0_client.toc` →
  `patch_55_client_00.tre`; our per-tre scans were blind to the TOC index layer). We have since
  NAMED them: they are the NGE **collection-system items** — tatooine's 8 include
  `shared_col_ent_fan_05.iff` (id 609,457,649, YOUR captured id) inside cantina cell 1134566,
  hanging lights, paintings, instruments, the collection beetle; naboo's 4 are the same family.
  Real, editable, shipped world content. A third provenance class filtering them from save would
  DELETE the collection hunt items from any saved planet. So: **no third class; they serialize;
  the placements table keeps showing them** — and your server-session counts were never polluted
  (server-streamed objects live only in `NetworkIdManager`, which can neither enumerate nor
  serialize). Your install-scan floor tooling should simply expect server-range authored ids in
  shipped `.ws` files.
- **§4D (occupancy): answered AND fixed before your freeze went out** —
  `2026-07-18-utinni-goalB-occupancy-guard-HANDBACK.md` (`835ad389c`). Your case (a) was right,
  with a safety symmetry: on server sessions occupants aren't linked into cell Container CONTENTS
  (the client tracks cells via `Object::getParentCell`), so the contents walk missed the player —
  and `Container::~Container` reads the SAME lists, which is why the delete was provably unable to
  harm them. The guard is now BIDIRECTIONAL (contents walk + an upward `getAllObjects()` sweep for
  any non-client-cached object whose parentCell owner is in the delete subtree). **Your §3a
  repro ran on the pre-fix exe** (timestamps: your test 2:43, our restage ~2:35): re-run
  delete-from-inside on the current stage — expect `-1` + an `OCCUPIED (parent-cell)` log line.

## 3. Gate

- Release/Win32 `/t:SwgClient` serial, 0 unresolved externals, no compile/link errors.
- `GetEngineHookPoints` exported undecorated (ord 82); `static_assert` 140==140; struct pins hold.
- Boot smoke 40s clean; restaged.
- **Real-save exercise:** the `wsSelfTestSaveOnLoad` key runs the full save path (filter → write →
  negative-cache invalidation → shadow check) on a normal world entry and logs
  `SELF-TEST save-on-load: result=N`. Expect result=0 with a `wsSaveSnapshot OK: <abs path>` line;
  the reload leg is your smoke's Save→Unload→Load cycle (that ordering exercises the sticky-name
  reset + the negative-cache fix together — the two blockers this design round caught).
- x64: shims compiled away; the sharedFile/sharedUtility additions are additive statics/methods
  (no layout change — no plugin ABI cascade).

## 4. Maintainer re-sync

1. Byte-identical `.h`/`.inc` → `D:/Code/Utinni/UtinniCore/swg/`. sha256:
   - `engine_hookpoints.h`   `8A101C9DC29E44BD229D02C39B1E131C1F65247D780A5156553A6DF60C3C18D4`
   - `engine_hookpoints.inc` `B942B048E54EB42D520BCD0E95E05C0FDE330190BF7D479A91DC5B2ED75892E5`
2. UtinniCore rebuild on v19 → your §2 bind wave. Smoke suggestions, in order: `wsGetSavePath`
   returns the loose root → Save (expect 0 + the file on disk at that root) → edit something →
   Save again → Unload → Reload via advertised `load` → the edit SURVIVED (this single cycle
   proves the filter, the negative-cache invalidation, and the scene-name reset at once) →
   delete-from-inside re-test (expect `-1` now, §2 above) → `setAllowTargetAnything(true)` →
   click a static in-world → gizmo matrices (`out16`/`out12` against a known camera pose).
3. SWGEmu regression: the saved `.ws` from the advertised client should load in your SWGEmu
   editor byte-compatibly (same TAG_0000 writer, whole OTNL table — unused names possible, ignore).

## 5. Provider-side state

- `fd06a2ad6` local (stacked on the pushed occupancy work); push follows Kenny's review cadence.
- The Wave-2 diagnostics + discriminator + self-test key all stay in-tree, default off.
- This closes every open Goal B item on our side: 3 waves landed (v17/v18/v19), both riders, all
  four investigation rounds. Remaining traffic we expect: your Wave-3 smoke, then arc close-out.
