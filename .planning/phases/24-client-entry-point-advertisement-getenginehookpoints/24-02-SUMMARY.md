---
phase: 24-client-entry-point-advertisement-getenginehookpoints
plan: 02
subsystem: infra
tags: [native-cpp, x86, injection, resolver, getenginehookpoints, dual-path, full-catalog, read-vs-call, accessor-globals]

# Dependency graph
requires:
  - phase: 24-01
    provides: the swg::endpoints resolver TU (pure resolve + lookupByName + resolveFromExe shell) + the critical-path s_bindings[] seed + the X-macro subset static_assert + the Wave-0 Catch2 harness
provides:
  - the FULL-catalog s_bindings[] (77 of 78 .inc names) -- every advertised endpoint minus the D-02 consoleHelper::sendInput carve-out
  - the D-04 read->call dual-path adaptation of the advertised accessor-style globals (Game::isSafeToUse, Graphics::getCurrentRenderTargetWidth/Height)
  - 6 new full-catalog accessor/static slots the consumer did not previously hook (object::getObjectTemplate/getObjectTemplateName/getNetworkId, worldSnapshot::removeObject/moveObject/getLoadingPercent) + 6 D-04 accessor slots (g_runningFlags, g_renderTargetWidth/Height, g_frameNumber, cuiManager/cuiIo g_instance)
  - the parallel constexpr kBindingNames[] + the count/subset/carve-out static_asserts (EPA-04 layer a, full-catalog scope)
  - a full-catalog Catch2 [endpoints][coverage] unit (77/77 resolve over the all-names synthetic fixture, carve-out allow-listed)
affects: [24-03 EPA-03 DX11 kickoff decouple, 24-04 maintainer live-smoke]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "extern typedef MUST byte-match the originating TU: MSVC encodes a namespace-scope variable's full pointee type in its mangled symbol (?name@ns@@3P6A...@ZA), so `extern void*` does NOT alias a `pFn` definition -- LNK2001. Forward-declare the engine types behind pointers to keep the typedefs without the full type graph."
    - "parallel constexpr kBindingNames[] for the subset gate: s_bindings[] is non-constexpr (its .slot initializers take addresses of extern objects), so the compile-time subset/carve-out asserts iterate a string-literal name array kept in lockstep via a count static_assert"
    - "D-04 read->call dual-path: `if (accessorSlot != nullptr) return accessorSlot(); else return memory::read<T>(RVA);` -- a function pointer is NEVER memory::read'd (Pitfall 4); SWGEmu fallback byte-for-byte unchanged"
    - "accessor-style global slots start nullptr (no SWGEmu RVA literal -- the consumer reads the underlying global on the SWGEmu path); they resolve only on the advertised client"

key-files:
  created: []
  modified:
    - UtinniCore/swg/endpoints_bindings.cpp
    - UtinniCore/swg/game/game.cpp
    - UtinniCore/swg/graphics/graphics.cpp
    - UtinniCore/swg/object/object.cpp
    - UtinniCore/swg/scene/world_snapshot.cpp
    - UtinniCore/swg/ui/cui_io.cpp
    - UtinniCore/swg/ui/cui_manager.cpp
    - UtinniCore.Tests/endpoints_tests.cpp

key-decisions:
  - "The contract is 78 names (byte-identical to provider; provider table = 78 rows), NOT 79. The 79 in CONTEXT/ROADMAP/24-01 prose was an off-by-one (a // Format: UTINNI_HOOKPOINT(...) comment line counted by `grep -c`). Bound scope is 77 of 78 (minus the D-02 carve-out), not 78 of 79."
  - "extern slots declared with the EXACT originating typedef (forward-declaring the engine types behind pointers), NOT `extern void*` -- MSVC mangles the pointee type into the symbol, so a void* extern is a different symbol -> LNK2001."
  - "6 full-catalog names the consumer never hooked (object::getObjectTemplate/getObjectTemplateName/getNetworkId, worldSnapshot::removeObject/moveObject/getLoadingPercent) get NEW nullptr accessor/static slots rather than being reported as coverage gaps -- they resolve on the advertised client and stay inert (null) on SWGEmu (D-01 full catalog, no invented RVA)."
  - "Game::isSafeToUse advertised path = !g_runningFlags() (the accessor is &Game::isOver; isOver() true == shutting down, so safe-to-use is its inverse)."
  - "RT-size + g_frameNumber + cuiManager/cuiIo g_instance accessors are BOUND but the only RT consumers are Graphics::getCurrentRenderTargetWidth/Height (adapted); the DX11 overlay derives size from the swapchain (Open Q2 resolved), so no other read-site needs adaptation this phase."

patterns-established:
  - "Full-catalog binding while preserving the dual-path no-op on SWGEmu: every advertised name overwrites its slot on the advertised client; on SWGEmu the export is absent so resolveFromExe() is a strict no-op and every literal stays on its RVA (D-00)."

requirements-completed: [EPA-02, EPA-04]

# Metrics
duration: ~80min
completed: 2026-06-21
---

# Phase 24 Plan 02: Full-Catalog Binding + D-04 Read->Call Adaptation Summary

**Populated `s_bindings[]` with the entire `utinni_engine_hookpoints.inc` catalog (77 of 78 names, minus the D-02 `consoleHelper::sendInput` carve-out) -- verifying every consumer typedef against the provider's real `&symbol` in `utinni_advertise.cpp`, including all name-mismatch rows and 6 newly-slotted endpoints -- then closed the read-vs-call semantic gap (Pitfall 4) by adapting `Game::isSafeToUse` and `Graphics::getCurrentRenderTargetWidth/Height` to CALL the resolved accessor on the advertised path while keeping `memory::read` byte-for-byte on the SWGEmu path.**

## Performance
- **Duration:** ~80 min
- **Completed:** 2026-06-21
- **Tasks:** 2 (both tdd)
- **Files modified:** 8 (0 created, 8 modified)

## Accomplishments
- Extended `s_bindings[]` from the 4-row critical-path seed (24-01) to the **full 77-row catalog** -- every `.inc` name except the single D-02 carve-out. Each row's contract NAME is the resolution key; the slot is the consumer literal that serves that engine function (so the name-mismatch rows resolve by name).
- **Verified every bound row's consumer typedef against the provider's real `&symbol`** (`utinni_advertise.cpp:154-288`). The name-mismatch rows (`game::mainLoop`->`Game::run`, `game::getPlayerCreatureObject`->`getPlayerCreature`, `graphics::screenshot`->`screenShot`, `graphics::useHardwareCursor`->`setHardwareMouseCursorEnabled`, `cuiManager::togglePointer`->`setPointerToggledOn`, `memory::free`->`MemoryManager::free`, `treeFile::open`->`TreeFile::open`->consumer `searchTree`, `extent::intersect`->`baseExtent::intersect`, `object::get*_w`->`get*`, `object::move_p`->`move`, `objectTemplate::getClientDataFile`->`getClientDataFilename`) are documented inline with the provider semantics.
- Added **6 new full-catalog slots** for `.inc` names the consumer never hooked (`object::getObjectTemplate/getObjectTemplateName/getNetworkId`, `worldSnapshot::removeObject/moveObject/getLoadingPercent`) -- nullptr slots that resolve on the advertised client and stay inert on SWGEmu (D-01 full catalog without inventing a wrong RVA).
- Added the **6 D-04 accessor-style global slots** (`game::g_runningFlags`, `graphics::g_renderTargetWidth/Height/g_frameNumber`, `cuiManager::g_instance`, `cuiIo::g_instance`) in their owning TUs, then **adapted the live read-sites read->call**: `Game::isSafeToUse` (`!g_runningFlags()` on the advertised path) and `Graphics::getCurrentRenderTargetWidth/Height` (call the resolved accessor). No function pointer is `memory::read`'d (Pitfall 4 closed).
- Hardened the EPA-04 compile-time gate for the full catalog: a parallel `constexpr kBindingNames[]` + `static_assert`s prove the binding count is 77, the `.inc` is 78, every binding name is advertised in the `.inc`, and the carve-out is absent -- all BUILD-time.
- Added a full-catalog `[endpoints][coverage]` Catch2 unit: re-includes the `.inc` to build the all-names set, synthesizes a table advertising all 78 names, and proves the 77-name binding list resolves 77/0-missing with the carve-out allow-listed.

## Task Commits
1. **Task 1 RED: full-catalog coverage unit** - `8be2b64` (test)
2. **Task 1 GREEN: full-catalog s_bindings[] + accessor slots + subset gate** - `1b5ea51` (feat)
3. **Task 2: D-04 read->call adaptation (dual-path)** - `fa21543` (feat)

## Files Created/Modified
- `UtinniCore/swg/endpoints_bindings.cpp` - extern typedefs for all 77 consumer slots (matching the originating TUs; engine types forward-declared behind pointers) + the full `s_bindings[]` + the parallel `kBindingNames[]` subset/count/carve-out static_asserts.
- `UtinniCore/swg/game/game.cpp` - added `g_runningFlags` accessor slot; adapted `Game::isSafeToUse` read->call (dual-path).
- `UtinniCore/swg/graphics/graphics.cpp` - added `g_renderTargetWidth/Height/g_frameNumber` accessor slots; adapted `Graphics::getCurrentRenderTargetWidth/Height` read->call (dual-path).
- `UtinniCore/swg/object/object.cpp` - added 3 new full-catalog slots (`getObjectTemplate/getObjectTemplateName/getNetworkId`).
- `UtinniCore/swg/scene/world_snapshot.cpp` - added 3 new full-catalog static slots (`removeObject/moveObject/getLoadingPercent`).
- `UtinniCore/swg/ui/cui_manager.cpp`, `UtinniCore/swg/ui/cui_io.cpp` - added the `g_instance` accessor slots.
- `UtinniCore.Tests/endpoints_tests.cpp` - the full-catalog coverage unit + the renamed one-absent-name coverage unit.

## Decisions Made
- **Contract size is 78, not 79** (see Deviations). Bound scope is 77 of 78.
- **extern typedefs must byte-match the originating TU** -- `extern void*` is a different mangled symbol under MSVC (LNK2001). Engine types forward-declared behind pointers keep the typedefs without dragging the full type graph.
- **6 unhooked `.inc` names get new nullptr slots** rather than being surfaced as coverage gaps -- D-01 wants the full catalog, and a nullptr slot that resolves only on the advertised client (inert on SWGEmu) is the honest binding (no invented RVA).
- **`isSafeToUse` advertised path is `!g_runningFlags()`** -- the accessor is `&Game::isOver`; `isOver()` true means shutting-down, so "safe to use" is its inverse.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Contract is 78 names (77 bound), not 79 (78 bound) -- corrected the off-by-one in the plan's truths/acceptance**
- **Found during:** Task 1 (RED test build -- `REQUIRE(kIncCount == 79)` failed with `78 == 79`).
- **Issue:** The plan's must-have truths, acceptance criteria, and the 24-01 prose all said "79 .inc names / 78 bound". The actual `.inc` expands to **78** `UTINNI_HOOKPOINT` rows (verified byte-identical to the swg-client-v2 provider; the provider's `s_engineHookPoints[]` table also has exactly 78 rows). The "79" came from `grep -c "UTINNI_HOOKPOINT("` counting the `// Format: UTINNI_HOOKPOINT(group, name)` comment on line 9 in addition to the 78 macro invocations.
- **Fix:** Set the coverage test + the compile-time count `static_assert`s to the true contract size: 78 `.inc` names, 77 bound (minus the D-02 carve-out). The provider-side ground truth (78-row table, byte-identical `.inc`) is authoritative.
- **Files modified:** UtinniCore.Tests/endpoints_tests.cpp, UtinniCore/swg/endpoints_bindings.cpp
- **Verification:** `[endpoints][coverage]` reports `resolved 77/77 by name (0 missing)`; the build-time `static_assert(kIncCount == 78)` + `static_assert(kBindingCount == 77)` pass.
- **Committed in:** 8be2b64 (test), 1b5ea51 (feat)

**2. [Rule 3 - Blocking] extern slots need the exact originating typedef, not `extern void*`**
- **Found during:** Task 1 (GREEN build -- LNK2001 on every slot when first declared `extern void*`).
- **Issue:** MSVC encodes a namespace-scope variable's full pointee type into its mangled symbol (`?loadConfigFileBuffer@config@swg@@3PAXA` for `void*` vs the function-pointer-typed symbol the originating TU defines). An `extern void*` declaration is therefore a DIFFERENT symbol and never links.
- **Fix:** Re-declared every extern with the exact `using pFn = ...` typedef copied from its originating TU, forward-declaring the engine class types (`utinni::Object/Camera/Appearance/CellProperty/ExtentBase/GroundScene/SharedObjectTemplate`) -- they appear only behind pointers/refs so a forward decl suffices; only `CommandParser::CommandData` (nested) needed the real header (`command_parser.h`).
- **Files modified:** UtinniCore/swg/endpoints_bindings.cpp
- **Verification:** UtinniCore.dll links clean; the typedef match is itself a drift guard (a wrong typedef would fail to link).
- **Committed in:** 1b5ea51

**3. [Rule 2 - Missing slot] 6 full-catalog `.inc` names had no consumer literal**
- **Found during:** Task 1 (reconciling every `.inc` name to a consumer slot).
- **Issue:** `object::getObjectTemplate/getObjectTemplateName/getNetworkId` and `worldSnapshot::removeObject/moveObject/getLoadingPercent` are advertised by the provider but the consumer had no `pFn` literal for them (the consumer never hooked these). D-01 mandates the full catalog.
- **Fix:** Added new nullptr-initialized typed slots in `object.cpp` / `world_snapshot.cpp` (no SWGEmu RVA -- the consumer has no SWGEmu call-site), bound them by name. They resolve on the advertised client and stay null/inert on SWGEmu. This is the honest full-catalog binding (a wrong invented RVA would be worse than a missing row).
- **Files modified:** UtinniCore/swg/object/object.cpp, UtinniCore/swg/scene/world_snapshot.cpp
- **Verification:** Build clean; coverage 77/77; the new slots are inert (null) until a future consumer call-site uses them.
- **Committed in:** 1b5ea51

**Total deviations:** 3 auto-fixed (1 bug, 1 blocking, 1 missing-functionality). None architectural; none required user input.

## Signature Concerns (T-24-05, recorded per the plan)
Resolution is by NAME so a name mismatch is not a bug; a SIGNATURE mismatch would corrupt the call. Per-row consumer typedefs were checked against `utinni_advertise.cpp`. Two rows carry a note for the 24-04 live-smoke:
- **`worldSnapshot::addObject`**: consumer typedef is `void(__cdecl*)(swgptr object, swgptr node)`; the provider symbol is `static Object* addObject(...)` (returns `Object*`, different arg shape). The consumer's existing SWGEmu call-site uses the void form; on the advertised client this row resolves by name but the consumer would need a matching call-site before invoking it. Not on the boot/render/scene path; bound for catalog completeness.
- **`treeFile::open`**: consumer slot is `swg::treefile::searchTree` (`swgptr(__thiscall*)(swgptr pThis, int priority, const char* filename)`); provider is `static AbstractFile* TreeFile::open(const char*, PriorityType, bool)` (`__cdecl`, different convention/arity). Resolves by name but the call ABI differs -- the consumer must not call this resolved slot through the `searchTree` `__thiscall` typedef on the advertised client without an adapter. Bound for completeness; not on the critical path.

These are NOT used on the boot/render/scene critical path; flagged here so 24-04 does not exercise them blindly.

## Known Stubs
The 6 newly-slotted full-catalog rows (`object::getObjectTemplate/getObjectTemplateName/getNetworkId`, `worldSnapshot::removeObject/moveObject/getLoadingPercent`) plus the diagnostic accessors (`graphics::g_frameNumber`, `cuiManager::g_instance`, `cuiIo::g_instance`) are bound but have NO consumer call/read-site yet -- they are nullptr on SWGEmu and resolve-only-on-advertised. This is intentional (D-01 full-catalog completeness); they are inert, not broken. A future plan/editor that needs one wires its call-site (the binding is already in place).

## Issues Encountered
- Git Bash MSYS path-mangling on MSBuild `/t:` `/p:` switches -- used the `-t:`/`-p:`/`-m` dash form (inherited 24-01 gotcha).
- `Generated/UtinniCore.cs` churns on every UtinniCore build (CppSharp) -- `git checkout --`'d it each time, never committed (per repo discipline).
- Pre-existing utini.h C4091/C4251/C4005 warnings surfaced compiling the bindings TU -- out of scope (not caused by this task); not fixed.

## TDD Gate Compliance
Task 1 is `tdd="true"`: the RED commit (`8be2b64`, `test(...)`) precedes the GREEN implementation (`1b5ea51`, `feat(...)`) -- a genuine RED gate (the coverage unit failed `kIncCount==79` first, then passed at 77/77 after the count correction + binding). Task 2 is `tdd="true"` but its behavior (read->call of a live `memory::read`) is not unit-testable without injection; it is gated by the `[endpoints]` no-regression suite + grep-verified dual-path acceptance (no `memory::read` of a resolved accessor; SWGEmu fallback byte-for-byte unchanged) per the plan's `<verify>`. The maintainer live-smoke (Plan 04) proves the advertised-path call at runtime.

## User Setup Required
None. The maintainer live-smoke (inject into `SwgClient_r.exe`) is Plan 04, not this plan.

## Next Phase Readiness
- The full 77-name override scope resolves on the advertised client; the D-04 accessor read-sites are call-adapted; SWGEmu reads preserved verbatim (D-00). **Plan 03** (EPA-03) binds `graphics::install` (already in the catalog now) so its `hkInstall` detour fires on the advertised client and `directX11::kickoff()` runs.
- **Plan 04** is the maintainer live-smoke -- it should NOT exercise the two signature-concern rows (`worldSnapshot::addObject`, `treeFile::open`) blindly.
- No blockers. Contract `.inc` re-confirmed byte-identical to the provider this session (78 names).

## Self-Check: PASSED
- Files: endpoints_bindings.cpp / game.cpp / graphics.cpp / object.cpp / world_snapshot.cpp / cui_manager.cpp / cui_io.cpp / endpoints_tests.cpp all modified + present.
- Commits: 8be2b64 FOUND, 1b5ea51 FOUND, fa21543 FOUND.
- Build: UtinniCore.dll + UtinniCore.Tests.exe clean (Release/x86); `[endpoints]` 182 assertions / 7 cases green; full native suite 396 assertions / 40 cases green.

---
*Phase: 24-client-entry-point-advertisement-getenginehookpoints*
*Completed: 2026-06-21*
