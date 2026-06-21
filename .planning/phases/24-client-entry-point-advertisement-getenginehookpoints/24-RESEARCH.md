# Phase 24: Client Entry-Point Advertisement (`GetEngineHookPoints`) — Research

**Researched:** 2026-06-21
**Domain:** Native x86 in-process injection — exe-export discovery, function-pointer resolution, DetourXS hooking, dual-path (advertised-vs-RVA) selection, DXGI/D3D11 overlay kickoff decoupling
**Confidence:** HIGH (provider side built + verified in-repo; consumer surface read directly from source; the two open truths are exactly this phase's live acceptance)

---

## Summary

The PROVIDER half of this contract is **complete and verified in `swg-client-v2`** (their Phase 37). `SwgClient_d.exe` and `SwgClient_r.exe` export an undecorated `extern "C" __cdecl GetEngineHookPoints()` returning a **78-row** `{name, void* addr}` table, each address taken at compile time by `&EngineSymbol` (or a thunk/accessor), version-pinned at 1, with a compile-time count `static_assert` + a runtime `utinni_verifyNoNullNoDup()` name-set-equality gate (EPA-04, provider side). The shared `utinni_engine_hookpoints.h` + `.inc` are **byte-identical between the two repos** (already re-copied into `UtinniCore/swg/`, commit visible at `UtinniCore/swg/utinni_engine_hookpoints.{h,inc}`, 78 names confirmed). The `_o` (Optimized) flavor cannot link due to a **pre-existing, unrelated LNK1281 SAFESEH defect** — out of scope here; validation bar is Debug+Release.

This phase is the **CONSUMER half**: UtinniCore must, at `utinni_init` time (the launcher remote thread — NOT DllMain), `GetProcAddress(GetModuleHandleA(NULL), "GetEngineHookPoints")`, and if present, populate the `swg::*` function-pointer literals from the advertised table BY NAME before `createDetours()` runs — fixing the exact `0xC0000005 READ target=0x00401000` crash (the first `Detour::Create` reads bytes at the wrong hardcoded address to compute detour length). If the export is absent (SWGEmu Pre-CU), keep the hardcoded-RVA path byte-for-byte unchanged. No config toggle; auto-selected by export presence. Plus EPA-03: decouple the DX11 `directX11::kickoff()` from the hardcoded `graphics::install` detour so the overlay starts on the advertised D3D11 client.

**Primary recommendation:** Build a single `swg::endpoints` resolver TU that runs FIRST in `utinni_init` (before `createDetours()`), reads the table, and **overwrites the existing per-subsystem `pFn xxx = (pFn)0xRVA;` namespace-scope pointers in place** by name. This is minimally invasive (the detour code already indirects through those pointers), preserves the SWGEmu path exactly (the resolver is a no-op when the export is absent), and the existing `directx11.cpp` `tryInstall()`/`kickoff()` pattern is the proven model to mirror. The 78-row advertised set is **narrower than UtinniCore's full ~230-RVA hook list** — the dual-path design is mandatory, not optional: advertised names override where present, every other endpoint silently keeps its RVA literal (which is dead-but-harmless on the advertised client as long as its detour is never installed, or which simply degrades that one editor).

---

## User Constraints (from CONTEXT.md)

No CONTEXT.md exists for Phase 24 (`has_context: false`). This is a standalone research run. The binding constraints below are extracted from ROADMAP Phase 24, REQUIREMENTS (EPA-01..04), and the LOCKED invariants in AGENTS.md/CLAUDE.md — treat them with the same authority as locked decisions until a `/gsd:discuss-phase 24` produces CONTEXT.md.

### Locked Decisions (from ROADMAP / REQUIREMENTS / AGENTS.md)
- **32-bit (x86) ONLY.** x64 is user-locked-deferred (the other half of Backlog 999.7). The provider TU is `#if !defined(_WIN64)`-guarded + vcxproj `Platform=Win32`-conditioned; the consumer must mirror that scope.
- **Dual-path, auto-selected by export presence — NO config toggle** (EPA-02). SWGEmu Pre-CU keeps the hardcoded-RVA path byte-for-byte unchanged (no regression to the working client).
- **Resolve by NAME, not by address** (EPA-01). The name column is the contract key; a wrong `&` is worse than a missing row (a missing row degrades gracefully).
- **Single shared header, no drift** — `utinni_engine_hookpoints.{h,inc}` re-copied verbatim between repos at each wave; `UTINNI_HOOKPOINTS_VERSION` pinned at 1.
- **Graceful degradation, never crash** (EPA-04): a missing/partial/absent contract is detected, logged, and degrades — mirrors the shipped `gl11 GetHookPoints` graceful-bail.
- **swg-client-v2 is a SANCTIONED write target for this phase** (the user owns + builds it) — distinct from the standing UtinniPlugins authority. Provider work is already done; consumer work is Utinni-repo-side. Any further provider tweak (e.g. the WR-05 fix) crosses into swg-client-v2.
- **DllMain does NO heavy startup** (CON-H-01) — the resolver runs in `utinni_init`, never `DLL_PROCESS_ATTACH`.

### Claude's Discretion
- The exact shape of the `swg::endpoints` accessor (in-place pointer overwrite vs. a central lookup map vs. a per-subsystem `resolve()` hook) — research recommends in-place overwrite; see Architecture Patterns.
- Whether the consumer-side coverage check (EPA-04) is a runtime log-and-degrade, a debug-only assert, or a unit test against a fixture table.
- Plan decomposition granularity (ROADMAP suggests ~4 plans; this research refines to 3 consumer plans + 1 live-smoke, provider being done).

### Deferred Ideas (OUT OF SCOPE)
- x64 advertisement (Backlog 999.7 other half).
- Mid-function byte/JMP/NOP patches (~4 JMP + ~15 NOP) — cannot be expressed as `&fn`; they stay SWGEmu-only this phase (the MVP boot/render/scene path needs none). See provider §8 #1.
- Advertising the ~20 `UI*::ctor` rows and the MI-class UI ctors (chatWindow/loginScreen/gameMenu) — provider DEFERRED them (multiple-inheritance PMF inflation + live `UIPage&` ctor arg); they keep RVA on the advertised client.
- The full ~198/~230 RVA retirement — provider shipped 78; full retirement is a later catalog wave, not this phase.

---

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| EPA-01 | SWG-Source client advertises ~198 entry points via versioned exe export sourced by `&fn` | **PROVIDER DONE** — 78 rows shipped + verified (`utinni_advertise.cpp`, dumpbin-confirmed undecorated export on `_d`/`_r`). Consumer reads it. The 78-vs-198 gap is reconciled in "Coverage Reconciliation" below — full retirement is NOT achievable this phase; dual-path is mandatory. |
| EPA-02 | UtinniCore consumes the contract; single resolver populates `swg::*` pointers; retires literals on the advertised client; dual-path auto-selected by export; no hardcoded literal dereferenced on the advertised client | Resolver design (in-place overwrite at `utinni_init`), the crash mechanism (DetourXS ADE32 read at `0x00401000`), the branch location (top of `createDetours()` / a new pre-step), and the SWGEmu no-regression proof are all documented below. |
| EPA-03 | DX11 overlay kickoff decoupled from SWGEmu-addressed `graphics::install` hook; `directX11::kickoff()` driven from a binary-agnostic trigger | Current coupling traced: `kickoff()` fires only inside `hkInstall()` (the detour ON hardcoded `graphics::install` `0x007548A0`). Decoupling options documented (resolve `graphics::install` from the table so its detour fires on the right client; or drive kickoff from `utinni_init` directly). |
| EPA-04 | Missing/partial/absent contract detected + logged; degrades cleanly; coverage check flags any UtinniCore-hooked endpoint absent from the struct | Consumer-side coverage check design + the version-mismatch handling + the graceful-bail pattern (mirror `directx11.cpp` `tryInstall`) documented. The honest 78-row scope is the measurable target, not the aspirational ~198. |

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Advertise engine entry points (`&fn` table + export) | swg-client-v2 exe (PROVIDER) | — | Only the from-source build can take `&EngineSymbol` at compile time. DONE. |
| Discover the export at runtime | UtinniCore `utinni_init` (CONSUMER) | — | The injected DLL owns discovery; runs on the launcher remote thread, not DllMain. |
| Resolve `swg::*` pointers by name | UtinniCore `swg::endpoints` resolver TU | per-subsystem `swg::<sub>` namespaces | Resolver overwrites the existing namespace-scope `pFn` literals; subsystems stay unchanged consumers. |
| Install detours / trampolines | UtinniCore per-subsystem `detour()` | DetourXS | Unchanged — detours indirect through the (now-resolved) pointers. |
| DX11 overlay kickoff | UtinniCore `directX11::kickoff()` + graphics resolver row | `tryInstall()` poll on prePresent | Must decouple from the hardcoded `graphics::install` detour (EPA-03). |
| Dual-path selection | UtinniCore resolver (export presence test) | — | `GetProcAddress != null` is the only selector; no config toggle. |
| Graceful degradation | UtinniCore resolver (log + leave RVA/disable feature) | — | Mirrors the shipped `gl11 GetHookPoints` bail. |

---

## Standard Stack

This is a native C++ injection phase inside an existing, mature codebase. No new external packages — the "stack" is the in-repo machinery already proven on the graphics-side `GetHookPoints` twin.

### Core (all in-repo, no install)
| Component | Location | Purpose | Why Standard |
|-----------|----------|---------|--------------|
| DetourXS (`Detour::Create`/`CheckPointer`) | `external/DetourXS/detourxs.{h,cpp}` | Function detour install; ADE32 auto-length | Already the project's only detour lib; `DETOUR_LEN_AUTO` mandated (memory: DetourXS explicit-length trap) |
| `swg::<subsystem>` pointer pattern | `UtinniCore/swg/*/` | `using pFn = ret(conv*)(args); pFn fn = (pFn)0xRVA;` then `detour()` indirects through `fn` | The literal-indirection-through-a-mutable-pointer IS the seam that makes in-place resolution trivial |
| `utinni_init` remote-thread entry | `UtinniCore/utinni.cpp:330` | Runs detour setup post-load, off DllMain | CON-H-01-compliant; already captures `g_swgModule = GetModuleHandleA(nullptr)` (the exe handle) at line 358 |
| `directx11.cpp` `tryInstall()`/`kickoff()` | `UtinniCore/swg/graphics/directx11.cpp:115-227` | The shipped advertised-contract consumer (graphics twin) | **The exact pattern to mirror** for the exe-side: GetModuleHandle → GetProcAddress → null-check → graceful bail → consume |
| Shared contract header | `UtinniCore/swg/utinni_engine_hookpoints.{h,inc}` | `UtinniEngineHookPoint{name,addr}` + `UtinniEngineHookPoints{version,count,entries}` + X-macro `.inc` | Byte-identical to provider; already in-tree |
| `std::bit_cast` (C++20) | provider-side only | PMF→void* (the provider already did this) | Consumer reads `void*` and casts to its own `pFn` typedef — no bit_cast needed consumer-side |

### Supporting
| Component | Location | Purpose | When to Use |
|-----------|----------|---------|-------------|
| `memory::read<T>(addr)` | `UtinniCore/utility/memory.h` | Reads engine globals at a literal address | The globals-as-`&g` reconciliation problem (see Pitfall 4) — provider advertises ACCESSORS, consumer currently READS |
| `utinni::log::{info,warning,critical}` | `UtinniCore` | One-shot diagnostic logging | The graceful-degrade log lines + the version-mismatch soft warning |
| `Detour::CheckPointer(addr)` | DetourXS | Validates/normalizes a pointer (jmp-thunk follow) before Create | Already used for vtable slots in directx9/11; consider for resolved addresses too |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| In-place overwrite of `pFn` literals | Central `std::unordered_map<string,void*>` queried at each detour site | Map is cleaner but touches every `detour()` call site (~30 files) + adds a lookup per hook. In-place overwrite touches only the resolver + leaves subsystems untouched. **Recommend in-place.** |
| Resolver runs in `utinni_init` before `createDetours()` | Per-subsystem lazy `resolve()` inside each `detour()` | Lazy spreads the dual-path branch across 30 files (error-prone, EPA-02 wants a SINGLE resolver). **Recommend one resolver, one branch.** |
| Name-keyed table lookup | A flat versioned struct of 198 named fields | ROADMAP §199 settled on the table (version-tolerant, graceful-degrade). Provider shipped the table. Locked. |

**Installation:** None. No package changes. The phase is C++ edits inside `UtinniCore/` + (if the WR-05 fix is taken) one row edit in `swg-client-v2`.

**Version verification:** Not applicable — no registry packages. The contract "version" is `UTINNI_HOOKPOINTS_VERSION == 1` (pinned, in `utinni_engine_hookpoints.h:40`, byte-identical both repos — verified by reading both files this session).

---

## Package Legitimacy Audit

Not applicable — this phase installs **zero external packages**. All machinery (DetourXS, the contract header, the resolver) is in-repo C++. No npm/PyPI/crates surface exists. slopcheck not run (no packages to check).

---

## Architecture Patterns

### System Architecture Diagram

```
                          ┌─────────────────────────────────────────────┐
   INJECTION              │  SwgClient_r.exe (advertised, D3D11)          │
   (Launcher              │   exports: GetEngineHookPoints()  ─────┐      │
    CreateRemoteThread)   │   exports: gl11_r.dll!GetHookPoints    │      │
        │                 └────────────────────────────────────────┼──────┘
        ▼                                                           │
   ┌────────────────────────────────────────────────────────────┐ │
   │ UtinniCore.dll  utinni_init (launcher remote thread)         │ │
   │                                                              │ │
   │  g_swgModule = GetModuleHandleA(NULL)  ◄── the exe handle    │ │
   │        │                                                     │ │
   │        ▼  NEW: swg::endpoints::resolve()  (runs FIRST)       │ │
   │   pGet = GetProcAddress(g_swgModule,"GetEngineHookPoints")   │ │
   │        │                                                     │ │
   │   ┌────┴─────────────┐                                       │ │
   │   │ pGet != null?    │                                       │ │
   │   ├──── YES ─────────┤──── NO (SWGEmu Pre-CU) ───────────┐   │ │
   │   ▼                  │                                   ▼   │ │
   │ table = pGet() ◄─────┼────(reads the 78-row contract)────┼───┘ │
   │ version check (soft) │                              leave all  │
   │ for each table row:  │                              pFn = 0xRVA │
   │   find swg::* literal │                              UNCHANGED   │
   │   by name → OVERWRITE │                              (no regress)│
   │   coverage log (EPA-04)                                          │
   │        │                                                         │
   │        ▼                                                         │
   │  createDetours()  ── detours indirect through the (resolved or   │
   │        │             RVA) pointers; first detour no longer       │
   │        │             reads 0x00401000 on the advertised client   │
   │        ▼                                                         │
   │  graphics::install detour fires (now correct addr) ── EPA-03 ──► │
   │        directX11::kickoff() ── subscribes prePresent poll ──►    │
   │            tryInstall() ─► gl11_r.dll!GetHookPoints ─► swapchain  │
   │                            latch ─► imgui DX11 overlay renders    │
   └──────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure
```
UtinniCore/swg/
├── utinni_engine_hookpoints.h     # EXISTS (shared, byte-identical to provider)
├── utinni_engine_hookpoints.inc   # EXISTS (78 names, X-macro)
├── endpoints.h / endpoints.cpp     # NEW — the resolver: resolve(), lookupByName(),
│                                   #       coverage check, dual-path branch, the
│                                   #       name→&literal binding table
├── misc/config.cpp                 # MODIFY (minimal) — literals become resolvable
├── client/client.cpp               # (no advertised rows in MVP — see reconciliation)
├── game/game.cpp                   # MODIFY — globals reconciliation (read vs accessor)
└── graphics/graphics.cpp           # MODIFY — graphics::install resolvable + EPA-03 kickoff decouple
```

### Pattern 1: In-place pointer resolution at `utinni_init`
**What:** A new `swg::endpoints::resolve()` reads the table once and overwrites the existing namespace-scope `pFn` literals by name. The detour code is unchanged — it already indirects through those pointers.
**When to use:** Always, as the first step of `utinni_init` before `createDetours()`.
**Example:**
```cpp
// Source: derived from UtinniCore/swg/misc/config.cpp:37-39 + utinni.cpp:358
// The resolver binds each contract NAME to the ADDRESS of the existing literal.
// On the advertised client it overwrites; on SWGEmu it is a no-op (export absent).
namespace swg::endpoints {

// One binding per advertised name → the storage cell of the existing pFn literal.
// (void** so we can overwrite whatever typedef it is — the cast is the subsystem's.)
struct Binding { const char* name; void** slot; };

static const Binding s_bindings[] = {
    { "config::loadOverrideConfig",   (void**)&swg::config::loadOverrideConfig   },
    { "config::loadConfigFileBuffer", (void**)&swg::config::loadConfigFileBuffer },
    { "graphics::install",            (void**)&swg::graphics::install            },
    // … one per advertised name the consumer actually hooks …
};

bool resolve() {
    HMODULE hExe = GetModuleHandleA(nullptr);
    auto pGet = (const UtinniEngineHookPoints*(__cdecl*)())
                GetProcAddress(hExe, "GetEngineHookPoints");
    if (!pGet) {                       // SWGEmu Pre-CU — keep RVA path verbatim
        utinni::log::info("endpoints: no GetEngineHookPoints export — RVA path (SWGEmu)");
        return false;
    }
    const UtinniEngineHookPoints* t = pGet();
    if (t->version != UTINNI_HOOKPOINTS_VERSION)
        utinni::log::warning("endpoints: contract version mismatch — resolving by name anyway");

    int resolved = 0, missing = 0;
    for (const Binding& b : s_bindings) {
        void* addr = lookupByName(t, b.name);   // linear scan of t->entries
        if (addr) { *b.slot = addr; ++resolved; }
        else      { utinni::log::warning(/* missing: b.name */); ++missing; }
        // NOTE: do NOT null the slot on miss — leaving the RVA literal is the
        // graceful degrade (that one feature is dark or RVA-wrong, not a crash).
    }
    utinni::log::info(/* resolved/missing/coverage summary — EPA-04 */);
    return true;
}
} // namespace swg::endpoints
```

### Pattern 2: Mirror the shipped graphics-side consumer (`directx11.cpp`)
**What:** The exe-side resolver should structurally mirror `directX11::tryInstall()` — `GetModuleHandle → GetProcAddress → null-check → graceful one-shot-warn bail → consume`.
**When to use:** For the discovery + bail skeleton. The graphics twin already proves the idiom against a real advertised contract (`gl11_r.dll!GetHookPoints`).
**Example:** see `UtinniCore/swg/graphics/directx11.cpp:115-190` (the `tryInstall()` body — the exe-side resolve is the simpler synchronous analog: no per-frame poll needed because the exe export exists at inject time, unlike the swapchain which materializes later).

### Pattern 3: EPA-03 — decouple DX11 kickoff from the hardcoded install hook
**What:** Today `directX11::kickoff()` runs ONLY from inside `hkInstall()` (the detour ON `swg::graphics::install`, hardcoded `0x007548A0`). On `SwgClient_r.exe` that address is wrong, so the detour never fires on the right function → kickoff never runs → no DX11 overlay.
**Two viable approaches (settle in discuss/plan):**
- **(A) Resolve `graphics::install` from the table first** (it IS row `graphics::install` at `utinni_advertise.cpp:151`). Then the existing `hkInstall()` detour installs on the CORRECT function and `kickoff()` fires naturally. Smallest change; keeps the kickoff site where it is. **Recommended** — it reuses the resolver and needs no new trigger.
- **(B) Drive `kickoff()` directly from `utinni_init`** after `createDetours()`, independent of the install detour. More decoupled but the kickoff's per-frame `tryInstall()` poll subscribes to `prePresent`, which itself depends on the graphics detours being installed — so this only helps if the prePresent tick is alive. Approach (A) is cleaner given that dependency.

### Anti-Patterns to Avoid
- **Branching dual-path inside every `detour()`** — spreads the export test across ~30 files; EPA-02 wants ONE resolver, ONE branch. Resolve once, up front.
- **Nulling a pFn literal on a missing-name miss** — that turns a graceful degrade into a guaranteed null-deref when its `detour()` runs. Leave the RVA literal; only the resolved ones change.
- **`memory::read<>` an advertised accessor row** — the provider advertises several "globals" as ACCESSOR function pointers (`game::g_runningFlags` → `&Game::isOver`, `graphics::g_renderTargetWidth` → `&getCurrentRenderTargetWidth`). The consumer currently READS those globals directly (`memory::read<bool>(0x01908858)`). You cannot `memory::read` a function pointer — these rows need a CALL, not a read. See Pitfall 4 (highest-impact reconciliation item).
- **Touching the SWGEmu path** — the resolver must be a strict no-op when the export is absent. Any behavioral change to the RVA branch risks the existing D3D9 live-smoke (success criterion 3).
- **Running the resolver in DllMain** — CON-H-01 violation. It runs in `utinni_init`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| The advertised table + `&fn` rows | A second hardcoded RVA table per client build | The shipped `GetEngineHookPoints` contract | Provider already built it; `&fn` is correct-by-construction, survives rebuilds |
| Detour install / trampoline | A custom hook | `Detour::Create(..., DETOUR_LEN_AUTO)` | In-repo, proven; explicit length corrupts (DetourXS trap memory) |
| The advertised-contract consumer skeleton | A new discovery flow | Mirror `directx11.cpp` `tryInstall()` | Already ships against a real export; same GetProcAddress/null-check/bail shape |
| PMF→void* on the provider side | union type-pun | `std::bit_cast` under a `sizeof` guard | Provider already did this correctly; consumer just casts `void*` → its own typedef |
| Exporting undecorated `__cdecl` | `.def` file / `/EXPORT` pragma juggling | `extern "C" __declspec(dllexport)` alone | Provider proved this yields the undecorated name (dumpbin-confirmed) |

**Key insight:** The hard, expensive parts (taking compile-time addresses, the byte-identical contract, the undecorated export, the PMF/ctor-thunk/virtual-skip taxonomy) are DONE on the provider side. The consumer side is a small, well-bounded resolver that the codebase already has a working template for. The risk is not in building it — it is in the **coverage reconciliation** (which of the 78 advertised names actually map to UtinniCore hook sites, and the read-vs-call mismatch) and the **live acceptance** (the two unverified truths).

---

## Coverage Reconciliation (78 advertised vs ~230 UtinniCore RVAs)

The provider shipped **78 rows** (the `.inc` count, confirmed). UtinniCore hooks/reads **~230 RVAs + ~25 globals across ~30 subsystems**. The gap is real and the dual-path is mandatory. Classification of the gap:

**Advertised AND directly consumable (overwrite the literal, call/detour as-is):**
- `config::loadConfigFileBuffer`, `config::loadConfigFileString` (consumer typedefs `pLoadConfigFileBuffer`/`pLoadConfigFileString` match) — `[VERIFIED: source read config.cpp:30-39]`.
- `graphics::*` 15 rows (install/update/beginScene/endScene/present/presentWindow/resize/flushResources/screenshot/useHardwareCursor/showMouseCursor/setSystemMouseCursorPosition/setStaticShader) — these are the EPA-03 critical set; consumer typedefs are `__cdecl` static-fn shaped, matching. `[VERIFIED: source read graphics.cpp:37-83]`.
- `game::*` static fns (install/quit/mainLoop/setupScene/cleanupScene/getPlayer/getCamera/getConstCamera/getPlayerCreatureObject/isViewFirstPerson/isHudSceneTypeSpace). `[CITED: utinni_advertise.cpp:137-148]`.
- `cuiManager::{render,setSize,togglePointer,restartMusic}`, `cuiIo::{setKeyboardInputActive,requestKeyboard}`, `commandParser::addSubCommand`, `commandParser::{ctor1,ctor2}` (thunks ABI-match the consumer `__thiscall pCtor1/pCtor2` — VERIFIED against `command_parser.cpp:29-30`), `object::*` non-virtual getters, `camera::*` setters, `extent::intersect`, `memory::{allocate,free}`, `audio::*`, `treeFile::open`, `report::print`, `worldSnapshot::*`, `objectTemplate::*`.

**Advertised with a NAME MISMATCH (provider already corrected to the real symbol — consumer must use the provider's actual symbol semantics):**
- `config::loadConfigFileString` → provider maps to `ConfigFile::loadFile` (NOT `loadFromString`). `game::mainLoop` → `Game::run`. `game::getPlayerCreatureObject` → `getPlayerCreature`. `graphics::screenshot` → `screenShot`. `graphics::useHardwareCursor` → `setHardwareMouseCursorEnabled`. `cuiManager::togglePointer` → `setPointerToggledOn`. `memory::free` (not `deallocate`). `report::print` → `Report::puts`. These are name-only mismatches — the contract NAME is the key, so resolution works; but the planner must verify the consumer's typedef signature matches the provider's actual symbol. `[CITED: utinni_advertise.cpp:131,139,143,159,160,180,251,255]`.

**Advertised as ACCESSOR but consumer READS a global (the read-vs-call gap — see Pitfall 4):**
- `game::g_runningFlags` → `&Game::isOver` (consumer does `memory::read<bool>(0x01908858) && memory::read<bool>(0x01919410)` at `game.cpp:600`). `graphics::g_renderTargetWidth/Height` → static getters. `cuiManager::g_instance`/`cuiIo::g_instance` → `&CuiManager::getIoWin`. `graphics::g_frameNumber` → `&Graphics::getFrameNumber`. **These do NOT drop in as `memory::read` replacements** — they are function pointers to call. Requires consumer-side adaptation per row.

**NOT advertised — stays on RVA (the ~150-row remainder, dual-path keeps them):**
- ALL `scene::groundScene::*` (MI-class PMF inflation — DEFERRED by provider). `cui::chatWindow::*` (MI). `cui::io::processEvent`, `Object::addToWorld/removeFromWorld/setParentCell`, `Appearance::render/collide`, `GroundScene::draw`, `RenderWorld::render` (VIRTUAL — Utinni resolves off the live vtable, not by `&fn`). The ~20 `UI*::ctor` rows, `loginScreen::ctor`, `gameMenu::ctor`. Terrain time-of-day/weather, player health/stats, hud view-distance, static-shader globals (private, no accessor). Mid-function patches (§8 #1). All `config::{setModalChat,getModalChat}`, `client::*` (wndProc/writeMiniDump/writeCrashLog/setupStartDataInstall) — provider deferred (file-local exe statics / unresolved symbols).

**Consumer-side EPA-04 coverage check:** assert every name in the consumer's `s_bindings[]` resolved to a non-null address from the table. The bindings list IS the consumer's "required set" — it must be a subset of the 78-name `.inc`. Anything UtinniCore hooks that is NOT in the bindings list silently keeps its RVA (logged at most once). This makes "zero missing" measurable against the honest 78-row scope, not the aspirational ~198.

**The critical MVP-for-acceptance subset** (what must resolve to close the live-smokes): `config::loadOverrideConfig` (criterion 1 — the crash), `graphics::install` (criterion 4 / EPA-03 — DX11 kickoff), plus the graphics group + `game::install`/`mainLoop`/`setupScene` + `cuiManager::render` to boot+render+scene. Everything else can land in a follow-up.

---

## Common Pitfalls

### Pitfall 1: The crash is a READ during detour-length disassembly, not a call
**What goes wrong:** Assuming the `0xC0000005 READ target=0x00401000` is the hook *calling* the function. It is not — it is `Detour::Create(loadOverrideConfig, ...)` → DetourXS → ADE32 reading instruction bytes AT `0x00401000` to compute the auto-detour-length, on a `SwgClient_r.exe` where that address is not the function (or not even mapped/readable as code).
**Why it happens:** `swg::config::detour()` is the FIRST detour in `createDetours()` (`utinni.cpp:127`), and `loadOverrideConfig = (pLoadOverrideConfig)0x00401000` (`config.cpp:39`).
**How to avoid:** Resolve the pointer BEFORE `createDetours()` runs. After resolution it points at the real function, ADE32 reads valid bytes, detour installs. Proving the fix = the resolver ran first AND `config::loadOverrideConfig` got a non-null table address.
**Warning signs:** A `0xC0000005 READ` whose target equals a known SWGEmu RVA literal = the resolver didn't run or didn't bind that name.

### Pitfall 2: `config::loadOverrideConfig` is a thunk, not the buffer-loader — the EPA-02 correction
**What goes wrong:** The original SPEC best-guessed `config::loadOverrideConfig` → `ConfigFile::loadFromBuffer`. That is WRONG — the consumer's `pLoadOverrideConfig` is `int(__cdecl*)()` (a zero-arg orchestrator), and the provider correctly maps the row to a **crash-fixer thunk** `utinni_loadOverrideConfig` → `installConfigFileOverride()`. The buffer-loader is the INNER call.
**Why it happens:** The SPEC's RVA column is "identification only"; the consumer typedef governs.
**How to avoid:** Bind `config::loadOverrideConfig` and trust the provider's thunk. The consumer's `hkLoadOverrideConfig` (`config.cpp:59`) calls the original via the resolved pointer — verify the thunk's `int()` shape matches. `[VERIFIED: source read config.cpp:32,39,59-77 + utinni_advertise.cpp:80-84,129]`.
**Warning signs:** Calling the original with the wrong arg count (it takes none).

### Pitfall 3: WR-05 — `consoleHelper::sendInput` is a 3-arg ABI mismatch (provider-flagged, consumer-fatal)
**What goes wrong:** The provider row `consoleHelper::sendInput` → `&CuiConsoleHelper::processInput`, whose REAL signature is `bool processInput(const Unicode::String&, stdset<Unicode::String>::fwd& recursionCheckStack, bool addToHistory)`. The 2nd arg is a REQUIRED caller-supplied recursion-tracking set. UtinniCore's `sendInput` typedef almost certainly expects 1–2 args. Calling through this PMF with the wrong arg count is a **stack/ABI corruption at the first detour for this row** — the exact failure class the phase set out to kill.
**Why it happens:** Provider mapped the name to a callable PMF (compiles, non-virtual, passes the self-check) but the consumer call boundary is where it bites.
**How to avoid:** Either (a) do NOT bind `consoleHelper::sendInput` this phase (leave it on RVA — it's not in the MVP boot/render/scene set), or (b) coordinate a provider thunk fix in swg-client-v2 (`processCurrentInput(bool)` or a `__fastcall` thunk that allocates the recursion set internally). The planner MUST flag this as a cross-repo coordination item. `[CITED: 37-REVIEW.md WR-05 + 37-VERIFICATION.md anti-pattern table]`.
**Warning signs:** A crash specifically when the console-helper editor path fires, not at boot.

### Pitfall 4: Globals advertised as accessors — read-vs-call semantic gap
**What goes wrong:** UtinniCore reads several engine globals via `memory::read<T>(literalAddr)` (e.g. `game.cpp:600` reads two bools; `game.cpp:339` reads the loop counter). The provider advertises the corresponding "globals" as ACCESSOR FUNCTION POINTERS (`game::g_runningFlags` → `&Game::isOver`; `graphics::g_renderTargetWidth` → `&getCurrentRenderTargetWidth`). You cannot `memory::read` a function pointer.
**Why it happens:** §8 #3 — the private globals have no addressable `&g`, so the provider advertised the only legitimate handle: a static accessor. "call-not-read" is noted in every such row comment.
**How to avoid:** For each accessor-style row the consumer wants, change the consumer site from `memory::read<T>(addr)` to `call the resolved accessor`. This is a per-row adaptation, not a drop-in. For the MVP, most of these globals are not on the boot/render/scene critical path — defer them and keep the RVA reads on the advertised client (they'll read wrong memory, but only the affected editor degrades — verify none crash). `[VERIFIED: source read game.cpp:339,600 + utinni_advertise.cpp:148,164,165,182,190,286]`.
**Warning signs:** Garbage values from a "global" read on the advertised client = you read a code address as data.

### Pitfall 5: Contract DRIFT between repos (the shared header)
**What goes wrong:** The `.h`/`.inc` are kept in sync by manual re-copy at each wave, NOT by a build-time generator. A consumer change without a provider re-copy (or vice versa) silently diverges.
**Why it happens:** `UTINNI_HOOKPOINTS_VERSION` is PINNED at 1 (the comment says per-wave bumps are cosmetic because re-copy is the real sync mechanism) — so the version field does NOT catch a name-list drift.
**How to avoid:** (a) Verify the consumer's in-tree `.h`/`.inc` are byte-identical to swg-client-v2's at plan time (`diff` the two). (b) The consumer's `s_bindings[]` names must be a strict subset of the `.inc` X-macro names — assert this at compile time with the same X-macro trick the provider uses. (c) Treat any consumer-side name not in the `.inc` as a drift bug. `[VERIFIED: both files read this session — currently byte-identical, 78 names]`.
**Warning signs:** A binding name that the table never returns an address for, when the export IS present = drift, not graceful degrade.

### Pitfall 6: Approach-B kickoff depends on prePresent being alive
**What goes wrong:** Driving `directX11::kickoff()` from `utinni_init` (decoupled from `hkInstall`) seems cleaner for EPA-03, but `kickoff()` subscribes a poll to `Graphics::prePresent`, which only ticks once the graphics detours are installed and the engine renders. Calling kickoff before that just subscribes a callback that never fires until the render loop is alive anyway.
**How to avoid:** Prefer Approach A (resolve `graphics::install` from the table so the existing `hkInstall` detour fires on the correct function and calls kickoff naturally). `[VERIFIED: source read graphics.cpp:589-626 + directx11.cpp:219-227]`.

### Pitfall 7: WR-01 inline-emitted addresses are call-safe but NOT RVA-stable
**What goes wrong:** Several provider Object/Camera rows (`getPosition_w`, `setPosition_w`, `getNetworkId`, etc.) are inline-defined; taking `&` forces MSVC to emit an out-of-line copy. The address is a VALID CALLABLE pointer but is NOT the canonical engine RVA.
**How to avoid:** For call-through use (which is all the consumer does for these) this is fine. Only matters if the consumer ever cross-checks a resolved address against a known SWGEmu RVA — which it should NOT (resolve by name, not address). `[CITED: 37-REVIEW.md WR-01]`.

---

## Runtime State Inventory

This phase is code-only (UtinniCore C++ edits + optionally one swg-client-v2 row). It edits no datastores and registers no OS state. Still, the rename-class question — "after every file is updated, what runtime state still has the old behavior?" — has one real answer here:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — verified, no datastore touched | none |
| Live service config | None — verified, no external service | none |
| OS-registered state | None — verified, no scheduled task / service / registry entry | none |
| Secrets/env vars | None — verified | none |
| Build artifacts | **The two `SwgClient_{d,r}.exe` binaries in `swg-client-v2/stage/` carry the export ONLY after the provider build ran** (DONE per 37-03). If swg-client-v2 is rebuilt without the `utinni_advertise.cpp` TU compiled in, the export vanishes and the consumer silently falls to no-overlay. Also: `_o.exe` will NEVER export (pre-existing SAFESEH) — do not test against `_o`. | Verify `dumpbin /exports stage/SwgClient_r.exe` shows `GetEngineHookPoints` before the live smoke; re-copy the contract `.h`/`.inc` if either repo edited them. |

---

## Validation Architecture

> nyquist_validation key is ABSENT in `.planning/config.json` → treated as ENABLED.

### Test Framework
| Property | Value |
|----------|-------|
| Framework (native) | Catch2 (`UtinniCore.Tests`, built via MSBuild) `[VERIFIED: AGENTS.md]` |
| Framework (managed) | xUnit (`UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`) `[VERIFIED: AGENTS.md]` |
| Config / build | VS2026 MSBuild on `Utinni.sln` `/p:Configuration=Release /p:Platform=x86`; run with `dotnet test --no-build` (managed) |
| Quick run command | `dotnet test --no-build --filter FullyQualifiedName~Endpoints` (after a new resolver unit-test fixture) |
| Full suite command | MSBuild build + Catch2 `UtinniCore.Tests` + `dotnet test --no-build` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| EPA-02 | Resolver binds names from a fixture table; missing-name leaves the RVA literal unchanged | unit (Catch2) | Catch2 `[endpoints][resolve]` against a synthetic `UtinniEngineHookPoints` | ❌ Wave 0 — new `endpoints_tests.cpp` |
| EPA-02 | Export-absent → resolver is a strict no-op (SWGEmu path unchanged) | unit (Catch2) | Catch2 `[endpoints][dualpath]` — null `pGet` returns false, slots untouched | ❌ Wave 0 |
| EPA-04 | `s_bindings[]` names are a compile-time subset of the `.inc`; coverage summary counts resolved/missing | unit + compile-time | `static_assert` X-macro subset check + Catch2 coverage-count assert | ❌ Wave 0 |
| EPA-04 | Version-mismatch logs a soft warning but still resolves by name | unit (Catch2) | Catch2 `[endpoints][version]` with `version = 999` fixture | ❌ Wave 0 |
| EPA-01/02 (live) | First detour against `config::loadOverrideConfig` completes — no `0xC0000005` on `SwgClient_r.exe` | manual live-smoke | Inject into `SwgClient_r.exe`; grep log for VEH FATAL absence | n/a — maintainer checkpoint |
| EPA-02 (live) | SWGEmu Pre-CU D3D9 live-smoke unchanged (no regression) | manual live-smoke | Existing SWGEmu inject + overlay eyeball | n/a — maintainer checkpoint |
| EPA-03 (live) | DX11 overlay renders on `SwgClient_r.exe` (closes D-08/D-22) | manual live-smoke | Inject, rasterMajor=11, ImGui overlay visible | n/a — maintainer checkpoint |

### Sampling Rate
- **Per task commit:** the new Catch2 `[endpoints]` quick filter.
- **Per wave merge:** full MSBuild + Catch2 + `dotnet test --no-build`.
- **Phase gate:** full suite green, THEN the 3 maintainer live-smokes (the 2 unverified provider truths + DX11 render). Worktrees OFF — build waves run inline (AGENTS.md).

### Wave 0 Gaps
- [ ] `UtinniCore.Tests/endpoints_tests.cpp` — resolver bind/no-op/version/coverage units (covers EPA-02, EPA-04). The resolver must be unit-testable WITHOUT injection: factor `resolve()` to accept a `const UtinniEngineHookPoints*` (the synthetic fixture) and a binding list, so the table-parsing + name-binding logic is tested process-isolated. The `GetProcAddress` wrapper stays a thin shell. (Per CLAUDE.md max-harness preference — invent the harness over manual smoke.)
- [ ] A compile-time X-macro subset assert that every `s_bindings[]` name exists in `utinni_engine_hookpoints.inc`.
- [ ] No new framework install needed — Catch2 + xUnit already present.

*The 3 live-smokes are irreducibly manual (maintainer-only injection per AGENTS.md — no headless path). The harness target is to make EVERYTHING ELSE green automatically so the live smoke only proves the inject + render, not the resolver logic.*

---

## Security Domain

> `security_enforcement` absent in config → treated as enabled. Scoped to reality: this is an **offline, single-user, local modding tool** that injects into a client the user owns and runs. There is NO untrusted input, NO network surface, NO multiplayer-cheat path (locked anti-goal DEC). The "input" to the resolver is a table the user's own build produced.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | No auth surface (in-process injection) |
| V3 Session Management | no | n/a |
| V4 Access Control | no | n/a |
| V5 Input Validation | yes (narrow) | The table is trusted (own build), but the resolver MUST null-check `pGet`, the returned pointer, `entries`, and every `addr` before binding — treat a malformed/partial table as graceful-degrade, never deref. This is robustness, not adversarial defense. |
| V6 Cryptography | no | n/a |

### Known Threat Patterns for native x86 injection
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Resolver dereferences a null/partial table | Denial (crash) | Null-check `pGet`/`table`/`entries`/`addr`; graceful bail (mirror `directx11.cpp` `tryInstall` null-checks) |
| Binding a wrong-signature symbol (WR-05) | Tampering (stack corruption) | Verify each bound row's consumer typedef matches the provider's actual symbol; defer WR-05 row or fix the provider thunk |
| Detour-length read on an unmapped address (the crash) | Denial | Resolve before `createDetours()`; consider `Detour::CheckPointer` on resolved addrs |
| Contract drift silently mis-resolves | Tampering | Byte-identical `.h`/`.inc` + X-macro subset assert |

No new attack surface is introduced — the export is a read-only getter, inert when Utinni is not injected (provider verified). The threat model is robustness/crash-avoidance, not adversarial.

---

## Likely Plan Decomposition

ROADMAP §197 suggested ~4 plans assuming the provider was unbuilt. Provider is DONE, so the consumer decomposition is:

1. **`swg::endpoints` resolver + dual-path selection + Wave-0 tests** (EPA-02, EPA-04 logic).
   - New `endpoints.{h,cpp}`: `resolve()` (testable, takes a table ptr), `lookupByName()`, the `s_bindings[]` table, the coverage summary, the X-macro subset assert.
   - Wire `swg::endpoints::resolve()` as the FIRST step in `utinni_init` before `createDetours()`.
   - Catch2 `endpoints_tests.cpp` (bind / export-absent no-op / version-mismatch / coverage).
   - Acceptance: SWGEmu RVA path is a strict no-op (proven by unit test + the existing D3D9 build).

2. **Retire literals behind the resolver for the MVP set + globals reconciliation** (EPA-02).
   - Populate `s_bindings[]` with the MVP-critical names (config + graphics group + game core + cuiManager::render).
   - Handle the name-mismatch rows (verify consumer typedef vs provider symbol).
   - Decide per accessor-style "global" row: adapt the consumer site (read→call) OR defer it to RVA (Pitfall 4). MVP defers most.
   - Explicitly DO NOT bind `consoleHelper::sendInput` (Pitfall 3 / WR-05) — or take the provider thunk fix as a cross-repo task.

3. **Decouple DX11 kickoff (EPA-03).**
   - Bind `graphics::install` so its existing `hkInstall` detour fires on the correct function on the advertised client (Approach A). Confirm `directX11::kickoff()` then runs.
   - Keep SWGEmu D3D9 path untouched.

4. **Live-smoke acceptance (maintainer checkpoint — closes D-08 + D-22).**
   - Verify `dumpbin` export present; inject into `SwgClient_r.exe`; confirm no `0xC0000005` (criterion 1), DX11 overlay renders (criterion 4), SWGEmu D3D9 still works (criterion 3).

Plans 1–3 are headless/CI-gated; plan 4 is the irreducible maintainer live smoke.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | In-place overwrite of the `pFn` literals is the lowest-risk resolver shape (vs a central map) | Architecture Patterns | If the codebase has hidden non-pointer-indirected hook sites, some won't resolve — mitigated by the binding being a subset and RVA being the fallback |
| A2 | Approach A (resolve `graphics::install` → existing `hkInstall` fires) is the cleanest EPA-03 path | Pattern 3 / Pitfall 6 | If the install detour has a SWGEmu-specific assumption beyond the address, kickoff may still not fire — discuss/plan must confirm |
| A3 | Most accessor-style "global" rows are off the MVP boot/render/scene critical path and can stay on RVA this phase | Coverage Reconciliation / Pitfall 4 | If a critical editor needs one (e.g. RT width for overlay sizing), it must be adapted read→call in this phase, not deferred |
| A4 | The consumer's `pLoadOverrideConfig` `int(__cdecl*)()` shape matches the provider thunk's `int __cdecl()` | Pitfall 2 | VERIFIED by source read; low risk |
| A5 | `consoleHelper::sendInput` (WR-05) is NOT in the MVP set so can be deferred without blocking acceptance | Pitfall 3 | If a smoke path exercises the console helper, it crashes that row — keep it unbound until the provider thunk fix lands |
| A6 | The contract `.h`/`.inc` are byte-identical between repos right now | Pitfall 5 | VERIFIED this session (both read, 78 names); re-diff at plan time in case either repo edits before execution |

**If this table is non-empty:** A1–A6 are design assumptions a `/gsd:discuss-phase 24` (or the planner) should confirm. None are blockers; all have a documented fallback.

---

## Open Questions

1. **Take the WR-05 provider thunk fix now, or defer the row?**
   - What we know: `consoleHelper::sendInput` → `&CuiConsoleHelper::processInput` is a 3-arg ABI mismatch that will corrupt the stack if the consumer calls it with its 1–2 arg typedef.
   - What's unclear: whether the console-helper editor is exercised by any acceptance smoke this phase.
   - Recommendation: do NOT bind it in the MVP; file a cross-repo task to wrap it in a provider thunk (swg-client-v2 sanctioned write) for a later wave.

2. **Which accessor-style globals (if any) must be adapted read→call this phase?**
   - What we know: RT width/height (`graphics::g_renderTargetWidth/Height`) feed overlay sizing; the rest (running flags, frame number) are diagnostic.
   - What's unclear: whether the DX11 overlay on the advertised client needs the advertised RT-size accessor or gets size from the swapchain (the `tryInstall` path already gets HWND from the swapchain).
   - Recommendation: check whether `tryInstall`/the DX11 backend already derive size from the swapchain; if so, defer all global accessors to a follow-up.

3. **EPA-03 Approach A vs B — confirm `hkInstall` has no SWGEmu-only assumption beyond the address.**
   - What we know: `hkInstall` calls `swg::graphics::install()` then `directX::detour()` then `directX11::kickoff()`.
   - What's unclear: whether `directX::detour()` (the D3D9 throwaway-device harvest) does anything harmful on a D3D11-only client when reached via the resolved install hook.
   - Recommendation: trace `directX::detour()` for D3D11 safety in plan 3 (the D-10 note in `directx11.cpp` says the D3D9 harvest is replaced in production — confirm it no-ops or is gated on a D3D9 device).

4. **Should the consumer bump/verify `UTINNI_HOOKPOINTS_VERSION` semantics given it's pinned at 1?**
   - What we know: version is pinned; re-copy is the real sync.
   - Recommendation: keep pinned; rely on the X-macro subset assert + a plan-time `diff` of the two repos' headers as the actual drift gate.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `SwgClient_r.exe` / `_d.exe` with `GetEngineHookPoints` export | Live acceptance (criterion 1, 4) | ✓ (provider built + dumpbin-confirmed) | export ordinal 51/52 | — (must rebuild swg-client-v2 with the TU if missing) |
| `gl11_r.dll!GetHookPoints` (graphics twin) | EPA-03 DX11 render | ✓ (shipped, consumed by `directx11.cpp`) | n/a | — |
| VS2026 MSBuild (v145, x86) | Build UtinniCore + tests | ✓ | Dev18 | — (do NOT use `dotnet build` — MSB3823 on WinForms resx) |
| Catch2 / xUnit | Wave-0 tests | ✓ | in-repo | — |
| SWGEmu Pre-CU client | No-regression smoke (criterion 3) | ✓ (maintainer env) | n/a | — |
| `dumpbin` | Verify export presence | ✓ (VS toolchain) | Dev18 | — |
| `_o.exe` (Optimized flavor) | (NOT required) | ✗ | — | Do NOT test against `_o` — pre-existing LNK1281 SAFESEH; bar is Debug+Release |

**Missing dependencies with no fallback:** none — the provider build is done and the export is confirmed.
**Missing dependencies with fallback:** none blocking; `_o` is intentionally excluded.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Hardcoded RVA literals per client build, re-blessed each recompile | Cooperative `&fn` advertisement table, correct-by-construction | 2026-06 (provider Phase 37) | The from-source D3D11 client is reachable without an RVA table; SWGEmu keeps RVA (dual-path) |
| DX11 kickoff gated on a hardcoded `graphics::install` detour | Decouple via advertised `graphics::install` row | this phase (EPA-03) | DX11 overlay starts on the advertised client |
| Graphics-only advertised contract (`gl11_r.dll!GetHookPoints`, 3 DXGI ptrs) | + exe-side game-logic twin (`GetEngineHookPoints`, 78 rows) | provider Phase 37 | Both render attach AND game-logic attach are cooperative |

**Deprecated/outdated:** Nothing deprecated. The hardcoded-RVA path is NOT deprecated — it remains the SWGEmu path permanently (dual-path).

---

## Sources

### Primary (HIGH confidence)
- `UtinniCore/swg/utinni_engine_hookpoints.{h,inc}` — the in-tree contract (78 names, version 1) — read this session.
- `UtinniCore/swg/misc/config.cpp`, `client/client.cpp`, `graphics/graphics.cpp`, `game/game.cpp` — the consumer literal/detour pattern + the crash site + globals reads — read this session.
- `UtinniCore/swg/graphics/directx11.cpp` + `.h` — the shipped advertised-contract consumer pattern (the model to mirror) — read this session.
- `UtinniCore/utinni.cpp` — `utinni_init` / `createDetours()` / DllMain / `g_swgModule` — read this session.
- `external/DetourXS/detourxs.h` — `Detour::Create`/`CheckPointer` signatures + `DETOUR_LEN_AUTO` — read this session.
- `swg-client-v2/.../37/utinni_advertise.cpp` — the 78-row provider table, pmfToVoid, ctor thunks, coverage check, export — read this session.
- `swg-client-v2/.../37/37-VERIFICATION.md`, `37-REVIEW.md`, `37-03-SUMMARY.md`, `deferred-items.md` — the 7/9 truths, the 5 warnings (WR-01/05 critical to consumer), the OMIT/DEFER taxonomy, the `_o` SAFESEH — read this session.
- `.planning/phases/24-.../24-ENGINE-ENTRYPOINT-ADVERTISEMENT-SPEC.md` — the full RVA catalog + contract spec — read this session.
- `.planning/ROADMAP.md` §188-203, `.planning/REQUIREMENTS.md` (EPA-01..04) — phase scope + criteria — read this session.
- AGENTS.md / CLAUDE.md — LOCKED invariants (32-bit, DllMain-safety, worktrees-off, MSBuild-not-dotnet-build, DetourXS auto-length).

### Secondary (MEDIUM confidence)
- `command_parser.cpp:29-30` — confirms consumer `__thiscall pCtor1/pCtor2` ABI-matches provider `__fastcall`-emulation (cross-checked source).

### Tertiary (LOW confidence)
- None — all claims are source-verified or cited from in-repo provider artifacts. No WebSearch was needed (the domain is entirely in-repo native injection mechanics).

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all in-repo, no packages, the consumer pattern is read directly from working source.
- Architecture (resolver shape + dual-path + EPA-03 decouple): HIGH — the graphics-twin `tryInstall`/`kickoff` proves the idiom; the literal-indirection seam makes in-place resolution trivial.
- Coverage reconciliation: HIGH — the 78-vs-230 gap is enumerated against both the provider table and the consumer typedefs; the read-vs-call and WR-05 gaps are source-verified.
- Pitfalls: HIGH — the crash mechanism, the EPA-02 thunk correction, WR-05, and the globals gap are all source-traced or provider-flagged.
- Live acceptance: irreducibly maintainer-manual (no headless inject path) — the harness target is to automate everything else.

**Research date:** 2026-06-21
**Valid until:** ~30 days for the consumer mechanics (stable in-repo); re-diff the shared `.h`/`.inc` against swg-client-v2 at plan time in case either repo edits the contract before execution.
