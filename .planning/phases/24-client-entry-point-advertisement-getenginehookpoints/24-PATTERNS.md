# Phase 24: Client Entry-Point Advertisement (`GetEngineHookPoints`) - Pattern Map

**Mapped:** 2026-06-21
**Files analyzed:** 6 (2 new source + 1 new test + 3 modified consumer TUs; the contract `.h`/`.inc` are pre-existing inputs, not produced here)
**Analogs found:** 6 / 6 (every file has a strong in-repo analog — this is a "mirror the graphics twin" phase)

This is a native C++ (x86) injection phase. There are no controllers/components/services — the role
taxonomy below is mapped to this codebase's native-injection vocabulary (resolver TU, detour-subsystem
TU, init entry, pure-decision test). The dominant instruction to the planner: **the resolver structurally
mirrors `directX11::tryInstall()`/`kickoff()` (`directx11.cpp:115-227`), the slot-overwrite seam is the
existing `pFn fn = (pFn)0xRVA;` literal in each subsystem, and the unit-test shape mirrors the pure
`selectBackend()` decision in `backend_select.h` + its `Dx11DetectionTests.cpp`.**

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `UtinniCore/swg/endpoints.{h,cpp}` (NEW) | resolver / discovery TU | event-driven (one-shot at init) → transform (name→pointer bind) | `UtinniCore/swg/graphics/directx11.cpp` `tryInstall()`/`kickoff()` (115-227) | exact (advertised-contract consumer; exe-side is the simpler synchronous twin) |
| `UtinniCore.Tests/endpoints_tests.cpp` (NEW) | test (Catch2 unit) | request-response (fixture in → assert) | `UtinniCore.Tests/Graphics/Dx11DetectionTests.cpp` (pure-fn injection) | exact (same "factor pure, inject inputs, no DLLs" shape) |
| `UtinniCore/utinni.cpp` (MODIFY) | init entry / orchestration | event-driven (remote-thread bootstrap) | itself — `utinni_init` (330) + `createDetours()` (110) | self (in-place insertion before `createDetours()`) |
| `UtinniCore/swg/misc/config.cpp` (MODIFY) | detour-subsystem TU | request-response (detour-through-pointer) | itself — `config.cpp:37-39` literals + `detour()` (79-82) | self (the literal IS the resolvable slot; crash site) |
| `UtinniCore/swg/graphics/graphics.cpp` (MODIFY) | detour-subsystem TU + render kickoff | request-response + event-driven (EPA-03 kickoff) | itself — `graphics.cpp:61` `install` literal + `hkInstall` (589-627) | self (Approach A: resolve `graphics::install` so `hkInstall` fires correctly) |
| `UtinniCore/swg/game/game.cpp` (MODIFY) | detour-subsystem TU + global reads | CRUD (global read→call adaptation, D-04) | itself — `game.cpp:339,600` `memory::read<T>` sites | self (read→call accessor adaptation) |

**Contract inputs (NOT produced this phase, read-only dependencies):**
- `UtinniCore/swg/utinni_engine_hookpoints.h` — `UtinniEngineHookPoint{name,addr}` + `UtinniEngineHookPoints{version,count,entries}` + `UTINNI_HOOKPOINTS_VERSION 1`.
- `UtinniCore/swg/utinni_engine_hookpoints.inc` — the X-macro name list. **NOTE: contains 79 `UTINNI_HOOKPOINT(...)` rows** (CONTEXT/RESEARCH say "78/79" — the actual in-tree count is **79**; the first plan must re-`diff` against `swg-client-v2`'s copy and reconcile the count before sizing `s_bindings[]`). The `.inc` is currently uncommitted in the working tree per CONTEXT — plan 1 commits it.

---

## Pattern Assignments

### `UtinniCore/swg/endpoints.{h,cpp}` (NEW — resolver, event-driven→transform)

**Analog:** `UtinniCore/swg/graphics/directx11.cpp:115-227` (`tryInstall()` discovery skeleton + `kickoff()` idempotence).

**Discovery + graceful-bail skeleton to copy** (`directx11.cpp:115-146`) — GetModuleHandle → GetProcAddress → null-check → one-shot warn → graceful `return false`:
```cpp
bool tryInstall()
{
    if (s_installed) { return true; }                 // latch fronts the sequence

    HMODULE hGl11 = GetModuleHandleA("gl11_r.dll");
    if (hGl11 == nullptr) { hGl11 = GetModuleHandleA("gl11_d.dll"); }
    if (hGl11 == nullptr) { return false; }           // no client -> leave default

    auto getHookPoints = (pGetHookPoints)GetProcAddress(hGl11, "GetHookPoints");
    if (getHookPoints == nullptr)
    {
        static bool s_warnedNoExport = true;
        if (s_warnedNoExport) {
            s_warnedNoExport = false;
            utinni::log::warning("directX11::tryInstall: gl11 loaded but GetHookPoints not exported; ...");
        }
        return false;                                 // graceful bail -- must NOT crash
    }
    UtinniDx11HookPoints hp = getHookPoints();
    ...
}
```
For the exe-side resolver this collapses: the module is `GetModuleHandleA(nullptr)` (the exe, already
captured as `g_swgModule` in `utinni.cpp:358`), the export is `"GetEngineHookPoints"`, and there is NO
per-frame poll (the export exists at inject time). Null-`pGet` is the SWGEmu Pre-CU branch → strict no-op
return false (D-00).

**Contract-POD typedef + `__cdecl` getptr cast pattern to mirror** (`directx11.cpp:46-52`):
```cpp
struct UtinniDx11HookPoints { IDXGISwapChain1* swapChain; ID3D11Device* device; ID3D11DeviceContext* context; };
using pGetHookPoints = UtinniDx11HookPoints(__cdecl*)();
```
The endpoints equivalent (the struct already exists in `utinni_engine_hookpoints.h`):
```cpp
#include "swg/utinni_engine_hookpoints.h"
using pGetEngineHookPoints = const UtinniEngineHookPoints*(__cdecl*)();
```

**Idempotent one-shot subscription discipline** (`kickoff()`, `directx11.cpp:219-227`) — the EPA-03 trigger
shape if the planner drives kickoff from the resolver rather than `hkInstall` (Approach B fallback only;
Approach A is preferred, see graphics.cpp below).

**The binding-table + slot-overwrite design** (CONTEXT D-03/Claude's-Discretion + RESEARCH Pattern 1) — the
resolver owns a `struct Binding { const char* name; void** slot; }` table whose `slot` is `(void**)&<the
existing per-subsystem pFn literal>`. The literal-through-mutable-pointer seam (next section) makes the
overwrite trivial. Skeleton (RESEARCH:174-210):
```cpp
struct Binding { const char* name; void** slot; };
static const Binding s_bindings[] = {
    { "config::loadOverrideConfig",   (void**)&swg::config::loadOverrideConfig   },
    { "graphics::install",            (void**)&swg::graphics::install            },
    // ... one per advertised name minus the D-02 carve-out ...
};
// resolve(const UtinniEngineHookPoints* t): for each binding, lookupByName(t,name);
// if addr -> *slot = addr (++resolved); else leave RVA literal (++missing, log once).
// NEVER null a slot on miss (anti-pattern RESEARCH:226) -- the RVA is the graceful degrade.
```

**Testability factoring (D-03b mandate):** `resolve()` MUST take `const UtinniEngineHookPoints*` + the
binding list as parameters so the parse+bind logic runs WITHOUT injection. `GetProcAddress` stays a thin
outer shell (e.g. `resolveFromExe()` that calls `GetProcAddress` then delegates to `resolve(table, ...)`).
This is the exact split `backend_select.h` made (`selectBackend(bool,bool)` pure; the DLL probe lives in the
caller).

**Coverage gate (D-03a/c):**
- Compile-time X-macro subset assert — re-include `utinni_engine_hookpoints.inc` with a `UTINNI_HOOKPOINT`
  macro that builds the valid-name set, then `static_assert` each `s_bindings[]` name is a member (CONTEXT
  D-03a; mirrors the provider's `utinni_advertise.cpp` self-check). The X-macro consume idiom is documented
  in `utinni_engine_hookpoints.inc:9-13` (`#define UTINNI_HOOKPOINT before #include, #undef after`).
- Runtime log-and-degrade — `utinni::log::info` a resolved/missing/coverage summary (D-03c). The D-02
  carve-out (`consoleHelper::sendInput`) must be allow-listed so it is NOT counted as a coverage failure.

---

### `UtinniCore/utinni.cpp` (MODIFY — init entry, in-place insertion)

**Analog:** itself. The resolver wires in as the FIRST `utinni_init` step, **before `createDetours()`**.

**Insertion point** (`utinni.cpp:357-375`) — `g_swgModule` is already captured; insert `resolve()`
immediately after the eager-init block and before `createDetours()`:
```cpp
g_swgModule = GetModuleHandleA(nullptr); // the injected SWG client exe  (:358 -- REUSE this)
...
directX::initPresentBlockedEvent(); // CR-04 eager init
directX::initDepthTexture();        // WR-03 eager init
// >>> NEW: swg::endpoints::resolveFromExe();  // dual-path bind; no-op on SWGEmu (:before 375)
createDetours();                    // (:375) first detour (config) now sees a resolved pointer
```

**Why before `createDetours()`** (Pitfall 1) — `swg::config::detour()` is the FIRST detour
(`utinni.cpp:127`), and the crash is DetourXS/ADE32 *reading* bytes at the wrong hardcoded
`0x00401000` to compute auto-length. Resolving the pointer first makes ADE32 read valid bytes.

**CON-H-01 compliance** — `utinni_init` runs on the launcher remote thread, never DllMain
(`utinni.cpp:330` is the exported entry). The resolver inherits this — do NOT move it to DllMain.

---

### `UtinniCore/swg/misc/config.cpp` (MODIFY — the crash site / resolvable slot)

**Analog:** itself — the `pFn fn = (pFn)0xRVA;` literal-indirection seam (`config.cpp:30-39`).

**The seam that makes in-place resolution trivial** (`config.cpp:30-39`):
```cpp
using pLoadOverrideConfig = int(__cdecl*)();                         // <-- consumer typedef governs (Pitfall 2)
pLoadOverrideConfig loadOverrideConfig = (pLoadOverrideConfig)0x00401000;  // <-- the crash literal & the slot
```
The resolver overwrites `loadOverrideConfig` by name; `detour()` (`config.cpp:79-82`) and `hkLoadOverrideConfig`
(`config.cpp:59-77`) are UNCHANGED — they already indirect through the pointer:
```cpp
void detour() {
    loadOverrideConfig = (pLoadOverrideConfig)Detour::Create(loadOverrideConfig, hkLoadOverrideConfig, DETOUR_TYPE_PUSH_RET);
}
```
**Minimal modification:** the literal must be at namespace scope and externally addressable so the resolver's
`(void**)&swg::config::loadOverrideConfig` binding compiles — it already is. **Pitfall 2:** the provider maps
`config::loadOverrideConfig` to a crash-fixer thunk; the consumer typedef is the zero-arg `int(__cdecl*)()` —
verify the provider thunk's `int __cdecl()` shape matches (it does, per RESEARCH A4). Bind by name; trust the
thunk.

---

### `UtinniCore/swg/graphics/graphics.cpp` (MODIFY — EPA-03 kickoff decouple, Approach A)

**Analog:** itself — `graphics.cpp:61` `install` literal + `hkInstall`/`kickoff` coupling (`graphics.cpp:589-627`).

**The coupling to decouple** (`graphics.cpp:603-619`) — `directX11::kickoff()` fires ONLY inside `hkInstall`,
which is the detour ON `swg::graphics::install` (`graphics.cpp:61`, hardcoded `0x007548A0`):
```cpp
bool __cdecl hkInstall()
{
    ...
    bool result = swg::graphics::install();   // indirects through the resolvable literal
    ...
    directX::detour();                        // D3D9 throwaway-device harvest (Open Q3: confirm no-op on D3D11)
    directX11::kickoff();                     // the SINGLE owned DX11 kick-off site
    ...
}
```
**Approach A (D-05, recommended):** bind `graphics::install` in `s_bindings[]` so the existing `hkInstall`
detour installs on the CORRECT function on the advertised client → `kickoff()` fires naturally. **No new
trigger site.** The literal is the slot (`graphics.cpp:61`):
```cpp
pInstall install = (pInstall)0x007548A0;     // <-- resolvable slot
```
**Plan must confirm (Open Q3 / D-05):** `directX::detour()` (the D3D9 throwaway-device harvest) no-ops or is
gated on a D3D9 device when reached via the resolved install hook on a D3D11-only client. The `directx11.cpp`
header note (`:25-32`) says the D3D9 harvest is REPLACED by the `GetHookPoints` poll in production — confirm
the gate.

**Globals read→call (D-04)** — `graphics::g_renderTargetWidth/Height`, `g_frameNumber` are advertised as
ACCESSOR function pointers. Any consumer site doing `memory::read<T>(addr)` for these must be rewritten to
CALL the resolved accessor on the advertised path (see game.cpp analog below). RESEARCH Open Q2: check whether
the DX11 backend already gets RT size from the swapchain (`directx11.cpp` `tryInstall` derives HWND from the
swapchain) — if so these globals can stay deferred.

---

### `UtinniCore/swg/game/game.cpp` (MODIFY — global read→call adaptation, D-04)

**Analog:** itself — the `memory::read<T>(literalAddr)` global-read sites (`game.cpp:339,600`).

**The read-vs-call gap (Pitfall 4 / anti-pattern RESEARCH:227)** — the consumer currently READS globals:
```cpp
int getMainLoopCount() { return memory::read<int>(0x1908830); }                          // :337-340
bool Game::isSafeToUse() { return memory::read<bool>(0x01908858) && memory::read<bool>(0x01919410); } // :600
```
The provider advertises the corresponding "globals" as ACCESSOR FUNCTION POINTERS (`game::g_runningFlags`
→ `&Game::isOver`; `g_frameNumber` → `&Graphics::getFrameNumber`; `cuiManager::g_instance`/`cuiIo::g_instance`
→ accessors). **You cannot `memory::read` a function pointer** — binding the slot without adapting the site
reads a code address as data (garbage). D-04 (FULL adaptation this phase): every advertised global's consumer
site is rewritten to call the resolved accessor on the advertised path, keeping the RVA `memory::read` only
on the SWGEmu path. This is a per-row adaptation, NOT a drop-in. Verify each consumer typedef against the
provider's real symbol (`utinni_advertise.cpp`), since several rows are name-mismatch corrections
(`game::mainLoop`→`Game::run`, `getPlayerCreatureObject`→`getPlayerCreature`).

---

## Shared Patterns

### Discovery + graceful-bail skeleton (THE phase pattern)
**Source:** `UtinniCore/swg/graphics/directx11.cpp:115-146` (`tryInstall()`).
**Apply to:** `endpoints.cpp` `resolveFromExe()`.
GetModuleHandle → GetProcAddress → null-check → one-shot `utinni::log::warning` → graceful `return false`.
Exe-side simplifications: module = `GetModuleHandleA(nullptr)` (== `g_swgModule`), export =
`"GetEngineHookPoints"`, no per-frame poll. Null-`pGet` is the permanent SWGEmu no-op branch (D-00).

### Literal-indirection slot seam (the in-place overwrite mechanism)
**Source:** every `swg::<subsystem>` TU — canonical examples `config.cpp:37-39`, `graphics.cpp:61-83`.
**Apply to:** `s_bindings[]` in `endpoints.cpp` — `(void**)&swg::<sub>::<literal>` per advertised name.
```cpp
using pFn = ret(__cdecl*)(args);
pFn fn = (pFn)0xRVA;          // namespace-scope, externally addressable -> resolver overwrites by name
// detour()/call sites indirect through `fn` UNCHANGED
```
Anti-pattern (RESEARCH:226): NEVER null a slot on a missing-name miss — leave the RVA literal (graceful
degrade, not a null-deref).

### DetourXS detour install (unchanged; auto-length mandatory)
**Source:** `external/DetourXS/detourxs.h` + `config.cpp:81` / `directx11.cpp:163-165`.
**Apply to:** no new detour code — detours already indirect through the (now-resolved) pointers.
```cpp
fn = (pFn)Detour::Create(fn, hkFn, DETOUR_TYPE_PUSH_RET);   // detourLen defaults to DETOUR_LEN_AUTO
swgptr addr = Detour::CheckPointer(rawAddr);                // optional: validate/follow-thunk a resolved addr
```
`DETOUR_LEN_AUTO` is mandatory (explicit length corrupts — the DetourXS trap lesson). The crash is the
auto-length READ at the wrong address — fixed by resolving BEFORE `createDetours()`, not by touching DetourXS.

### Logging (graceful-degrade + version-mismatch soft warning)
**Source:** `UtinniCore/utility/log.h:33-37` — `utinni::log::{info,warning,critical}(const char*)`.
**Apply to:** `endpoints.cpp` coverage summary + the version-mismatch soft warning. Version mismatch
(`t->version != UTINNI_HOOKPOINTS_VERSION`) logs `warning` and resolves by name anyway (Claude's Discretion;
version pinned at 1, the real drift gate is the X-macro subset assert).

### X-macro consume idiom (compile-time subset assert + drift gate)
**Source:** `UtinniCore/swg/utinni_engine_hookpoints.inc:9-13` (the `#define UTINNI_HOOKPOINT / #include / #undef` contract).
**Apply to:** the D-03a compile-time subset assert in `endpoints.cpp` AND the consumer self-check.
The includer `#define`s `UTINNI_HOOKPOINT(group,name)` to expand each row (e.g. into a name-set entry),
`#include "swg/utinni_engine_hookpoints.inc"`, then `#undef`s. Mirrors `utinni_advertise.cpp`'s provider-side
`s_requiredNames[]`/count self-check.

---

## Test Pattern

### `UtinniCore.Tests/endpoints_tests.cpp` (NEW)
**Analog:** `UtinniCore.Tests/Graphics/Dx11DetectionTests.cpp` + `swg/graphics/backend_select.h`.

**The "factor pure, inject inputs, no DLLs" shape** (`Dx11DetectionTests.cpp:36-54`):
```cpp
#include <catch2/catch_all.hpp>
#include "swg/graphics/backend_select.h"          // the pure decision under test
using render_backend::selectBackend;

TEST_CASE("RNDR-03 backend selection across the four states", "[rndr03][detect]")
{
    SECTION("...") { REQUIRE(selectBackend(/*gl11Present=*/false, /*resolved=*/false) == BackendChoice::Dx9); }
}
```
Apply to endpoints: include the resolver header, build a synthetic `UtinniEngineHookPoint entries[]` + a
`UtinniEngineHookPoints` fixture, call `resolve(&fixture, bindings)` (the pure overload), and `REQUIRE` on
the bind/no-op/version/coverage outcomes (CONTEXT D-03b / RESEARCH:344-347 test map):
- `[endpoints][resolve]` — binds names from the fixture; bound slot now holds the fixture addr.
- `[endpoints][dualpath]` — null table / export-absent → resolver is a strict no-op; slots untouched.
- `[endpoints][version]` — `version = 999` fixture → soft warning, still resolves by name.
- `[endpoints][coverage]` — resolved/missing counts; D-02 carve-out allow-listed (not a failure).

**Test-project wiring** (`UtinniCore.Tests/UtinniCore.Tests.vcxproj`):
- Include path already covers the resolver header: `AdditionalIncludeDirectories` contains
  `$(SolutionDir)UtinniCore` (vcxproj:73/90/109), so `#include "swg/endpoints.h"` and
  `#include "swg/utinni_engine_hookpoints.h"` resolve.
- To compile the resolver into the test binary WITHOUT pulling in injection deps, add a
  `<ClCompile Include="..\UtinniCore\swg\endpoints.cpp" />` row — this is exactly how
  `render_backend.cpp` is pulled in (vcxproj:153). Add the new `endpoints_tests.cpp` next to the
  other `<ClCompile>` rows (vcxproj:128-147). This requires the resolver's pure core to NOT
  transitively `#include` DX/injection-only headers (keep it as lean as `backend_select.h` — the
  testability factoring above enforces this).

---

## No Analog Found

None. Every file maps to a strong in-repo analog — this phase is deliberately a "mirror the shipped
graphics-side `GetHookPoints` consumer" exercise. The only NEW *file* (`endpoints.{h,cpp}`) has an exact
structural twin in `directx11.cpp`; its NEW *concern* (a name→pointer binding table over the existing
literal seam) is assembled from two existing patterns (the `pFn (pFn)0xRVA` literal + the X-macro `.inc`
consume idiom), not invented.

## Metadata

**Analog search scope:** `UtinniCore/swg/{graphics,misc,game}/`, `UtinniCore/swg/utinni_engine_hookpoints.{h,inc}`,
`UtinniCore/utinni.cpp`, `UtinniCore/utility/{log,memory}.h`, `UtinniCore.Tests/` (+ `.vcxproj`),
`external/DetourXS/detourxs.h`.
**Files scanned:** 11 read in full or in targeted ranges.
**Key cross-file invariant:** the resolver's `s_bindings[]` names are a strict subset of the **79**-row
`utinni_engine_hookpoints.inc` minus the one D-02 carve-out (`consoleHelper::sendInput`); enforce at compile
time via the X-macro subset assert; re-`diff` the `.h`/`.inc` against `swg-client-v2` at plan time (Pitfall 5).
**Pattern extraction date:** 2026-06-21
