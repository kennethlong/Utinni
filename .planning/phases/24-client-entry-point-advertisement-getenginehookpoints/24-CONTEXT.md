# Phase 24: Client Entry-Point Advertisement (`GetEngineHookPoints`) - Context

**Gathered:** 2026-06-21
**Status:** Ready for planning

<domain>
## Phase Boundary

The **CONSUMER half** of the engine entry-point advertisement contract. The PROVIDER half is DONE and
verified in `swg-client-v2` (their Phase 37): `SwgClient_{d,r}.exe` export an undecorated
`extern "C" __cdecl GetEngineHookPoints()` returning a **79-row** `{name, void* addr}` table, each
address taken at compile time by `&EngineSymbol` (or a thunk/accessor), `UTINNI_HOOKPOINTS_VERSION`
pinned at 1.

This phase makes UtinniCore consume that table: at `utinni_init` time (the launcher remote thread, NOT
DllMain), `GetProcAddress(GetModuleHandleA(NULL), "GetEngineHookPoints")`, and if present, **overwrite
the `swg::*` function-pointer literals by NAME before `createDetours()` runs** — fixing the exact
`0xC0000005 READ target=0x00401000` first-detour crash (DetourXS/ADE32 reading instruction bytes at the
wrong hardcoded address). If the export is absent (SWGEmu Pre-CU), the hardcoded-RVA path stays
byte-for-byte unchanged. Auto-selected by export presence; no config toggle. Plus EPA-03: decouple the
DX11 `directX11::kickoff()` from the hardcoded `graphics::install` detour so the overlay starts on the
advertised D3D11 client.

**In scope:** the `swg::endpoints` resolver TU (dual-path discovery + in-place name→pointer binding +
coverage gate) wired as the FIRST step of `utinni_init`; binding the **full 79-name catalog** (minus the
two documented carve-outs below); read→call adaptation of ALL advertised accessor-style globals; EPA-03
DX11 kickoff decouple; the Wave-0 test harness; the maintainer live-smokes.

**Out of scope (deferred, see Deferred Ideas):** x64 advertisement; mid-function JMP/NOP/byte patches
(cannot be `&fn`); the `UI*::ctor` / MI-class ctor rows the provider never advertised; `groundScene::*`
(MI-class, not in the `.inc`); the WR-05 `consoleHelper::sendInput` provider thunk fix.

**Scope: 32-bit (x86) only.** x64 is user-locked-deferred (the other half of Backlog 999.7).

</domain>

<spec_lock>
## Contract Locked via SPEC.md

`24-ENGINE-ENTRYPOINT-ADVERTISEMENT-SPEC.md` is the **producer-side instrumentation spec + full RVA
catalog** handed to `swg-client-v2`. It is NOT a `/gsd:spec-phase` requirements lock (no `## Requirements`
section) — the consumer requirements are **EPA-01..04** (REQUIREMENTS.md / ROADMAP §188-203). The SPEC
locks: the contract shape (`{name, void* addr}` table + versioned struct), the export mechanics
(undecorated `__cdecl`, exe module), the single-shared-header no-drift rule, and the complete endpoint
catalog with per-row signature-source. Downstream agents MUST read it for the catalog and the
"endpoints you do NOT need to advertise" list (§6 tail) before binding any row.

**The provider's actual symbol mapping governs** (`swg-client-v2 .../37/utinni_advertise.cpp`) — several
SPEC §6 "best-guess" symbols were corrected provider-side (see Pitfall notes in 24-RESEARCH.md and the
name-mismatch list). Resolve by NAME; verify each consumer typedef against the provider's real symbol.

</spec_lock>

<decisions>
## Implementation Decisions

### Dual-path is permanent — nothing is deleted (non-negotiable)
- **D-00: "Retire" means runtime override on the advertised client ONLY — never source deletion.** The
  hardcoded SWGEmu RVA literals (`pFn fn = (pFn)0xRVA;`) STAY in the source permanently. On the
  advertised client (`SwgClient_*.exe`) the resolver **overwrites the in-memory pointer by name** so the
  literal is not dereferenced at runtime; on SWGEmu Pre-CU (`GetEngineHookPoints` export absent) the
  resolver is a **strict no-op** and the literals are used exactly as today. The existing SWGEmu D3D9
  live-smoke MUST still pass unchanged (ROADMAP success criterion 3 / EPA-02). Any phrase like "retire
  the RVAs" in the ROADMAP/SPEC refers to this per-client runtime selection, NOT to removing the SWGEmu
  mechanism. The SWGEmu calling path is co-equal and load-bearing.

### Binding scope
- **D-01: Bind the FULL 79-name catalog this phase** — not just the MVP boot/render/scene subset. The
  consumer's `s_bindings[]` is the entire `utinni_engine_hookpoints.inc` name list **minus the two
  documented carve-outs** (D-02, below). This pulls in the name-mismatch rows (resolve-by-name handles
  them; planner verifies each consumer typedef vs the provider's real symbol), the `commandParser`
  ctor `__thiscall` thunks (ABI-match verified), and all accessor-style globals (D-04).
- **D-02: `consoleHelper::sendInput` is the ONE carve-out — DEFER, leave on RVA, do NOT bind** (WR-05).
  The provider row maps to `CuiConsoleHelper::processInput`, a 3-arg PMF whose 2nd arg is a required
  caller-supplied recursion-tracking set; calling it through UtinniCore's 1–2 arg typedef corrupts the
  stack — the exact failure class this phase exists to kill. It is not on the boot/render/scene path.
  File a cross-repo follow-up for a provider thunk (see Deferred Ideas). This name stays in the `.inc`
  but is recorded as an **intentional unbound name**, NOT drift, for the coverage gate (D-03).

### Coverage gate (EPA-04) — all three layers
- **D-03a: Compile-time X-macro subset assert** — `static_assert` that every `s_bindings[]` name exists
  in `utinni_engine_hookpoints.inc` (fails the BUILD on drift), mirroring the provider's self-check.
- **D-03b: Catch2 unit test** — process-isolated `resolve()` against a synthetic `UtinniEngineHookPoints`
  fixture: bind / export-absent no-op / version-mismatch-soft-warn / coverage-count. Resolver MUST be
  factored to accept a `const UtinniEngineHookPoints*` + a binding list so the table-parse + name-bind
  logic is testable WITHOUT injection (`GetProcAddress` stays a thin shell). Per the max-harness
  preference — invent the harness over manual smoke.
- **D-03c: Runtime log-and-degrade** — at inject, log resolved/missing/coverage summary; a missing name
  leaves the RVA literal (graceful degrade, never crash). The known intentional non-resolution is the
  D-02 carve-out (`consoleHelper::sendInput`) — must be allow-listed so it does not read as a coverage
  failure.

### Accessor-style globals (read→call adaptation)
- **D-04: Adapt ALL advertised accessor globals read→call this phase** — not just the critical ones.
  Several "globals" are advertised as ACCESSOR FUNCTION POINTERS (`game::g_runningFlags` → `&Game::isOver`;
  `graphics::g_renderTargetWidth/Height` → static getters; `g_frameNumber` → `&Graphics::getFrameNumber`;
  `cuiManager::g_instance` / `cuiIo::g_instance` → accessors). You CANNOT `memory::read()` a function
  pointer — binding a global without adapting its site to **call** would read a code address as data
  (garbage). So every advertised global's consumer site (`memory::read<T>(addr)`) is rewritten to call
  the resolved accessor on the advertised path, keeping the RVA `memory::read` only on the SWGEmu path.
  This makes `s_bindings[]` the TRUE full catalog minus only D-02.

### EPA-03 — DX11 kickoff decouple
- **D-05: Approach A (research-recommended) — resolve `graphics::install` from the table** so the
  existing `hkInstall()` detour installs on the CORRECT function on the advertised client and
  `directX11::kickoff()` fires naturally. No new trigger site. Plan must confirm `hkInstall`'s
  `directX::detour()` (the D3D9 throwaway-device harvest) no-ops / is gated on a D3D9 device when reached
  on a D3D11-only client (Open Question 3 in RESEARCH).

### Claude's Discretion (research-recommended defaults; planner may refine)
- **Resolver shape:** in-place overwrite of the per-subsystem `pFn` namespace-scope literals via ONE
  resolver, ONE dual-path branch — NOT a central `unordered_map` queried at every detour site (that
  spreads the branch across ~30 files). The literal-indirection seam makes in-place trivial.
- **`UTINNI_HOOKPOINTS_VERSION` stays pinned at 1.** Version mismatch logs a soft warning and resolves
  by name anyway. The real drift gate is the byte-identical `.h`/`.inc` re-copy + the D-03a subset
  assert (re-`diff` the two repos' headers at plan time).
- **Plan decomposition:** ~4 plans — (1) resolver + dual-path + Wave-0 tests; (2) full-catalog binding
  + name-mismatch verification + globals read→call adaptation; (3) EPA-03 DX11 kickoff decouple;
  (4) maintainer live-smoke (closes D-08 + D-22).

### Reviewed Todos (not folded)
Four todos keyword-matched Phase 24 (score 0.6) but are all off-domain — none touch the
advertisement/resolver scope. Left for their own phases: Phase-9 datatable code-review warnings;
Phase-10 stringtable SC3 live-reload residual; Phase-21 terrain IHDR deeper-nesting; SWG
windowed↔fullscreen resize edge cases (D3D9 presentation, a separate deferred todo).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + contract
- `.planning/phases/24-client-entry-point-advertisement-getenginehookpoints/24-RESEARCH.md` — the full
  consumer design: resolver pattern, dual-path, coverage reconciliation (79 advertised vs ~230 RVAs),
  7 pitfalls (crash mechanism, EPA-02 thunk correction, WR-05, read-vs-call globals, drift), Wave-0
  test gaps, assumptions A1–A6, the 4 open questions. **Read first.**
- `.planning/phases/24-client-entry-point-advertisement-getenginehookpoints/24-ENGINE-ENTRYPOINT-ADVERTISEMENT-SPEC.md`
  — producer instrumentation spec + complete RVA catalog + §6 "do NOT advertise" list + §8 open items.
- `.planning/REQUIREMENTS.md` — EPA-01..04 (the consumer requirements). `.planning/ROADMAP.md` §188-203
  — phase scope + the 4 success criteria.

### The shared contract (byte-identical to provider — verified zero drift this session)
- `UtinniCore/swg/utinni_engine_hookpoints.h` — structs + `UTINNI_HOOKPOINTS_VERSION 1`.
- `UtinniCore/swg/utinni_engine_hookpoints.inc` — the 79-name X-macro list. ⚠ The full 37-03 catalog is
  currently **uncommitted in the working tree** (synced from swg-client-v2, byte-identical); the first
  plan should commit it.
- `D:/Code/swg-client-v2/src/game/client/application/SwgClient/src/shared/utinni_engine_hookpoints.inc`
  — the provider copy to `diff` against at plan time (Pitfall 5 drift gate).
- `D:/Code/swg-client-v2/.../37/utinni_advertise.cpp` — **the authoritative provider symbol mapping**
  (the real `&fn` per name, incl. the name-mismatch corrections + the ctor thunks). Verify consumer
  typedefs against THIS, not the SPEC §6 best-guesses.

### Consumer code anchors (UtinniCore)
- `UtinniCore/utinni.cpp` — `utinni_init` (~:330), `createDetours()` (config detour first, ~:127),
  `g_swgModule = GetModuleHandleA(nullptr)` (~:358). The resolver wires in as the FIRST `utinni_init` step.
- `UtinniCore/swg/graphics/directx11.cpp:115-227` — `tryInstall()`/`kickoff()`: **the shipped
  advertised-contract consumer to MIRROR** (GetModuleHandle → GetProcAddress → null-check → graceful
  bail → consume).
- `UtinniCore/swg/misc/config.cpp:37-39,59-77` — the crash site (`loadOverrideConfig = (pFn)0x00401000`)
  + `hkLoadOverrideConfig` (verify the provider thunk's `int __cdecl()` shape matches; Pitfall 2).
- `UtinniCore/swg/graphics/graphics.cpp:589-626` — `hkInstall`/`kickoff` coupling (EPA-03 / Approach A).
- `UtinniCore/swg/game/game.cpp:339,600` — the `memory::read` global sites adapted read→call (D-04).
- `external/DetourXS/detourxs.h` — `Detour::Create` / `CheckPointer` / `DETOUR_LEN_AUTO` (always auto).

### Carried-forward decisions
- `.planning/phases/19-dx11backend-config-detection-resize/19-CONTEXT.md` — D-09/D-10/D-11 (client
  advertises render hook points; `gl11_r.dll!GetHookPoints` shipped); D-22 live-smoke DEFERRED to this
  phase. This phase is the exe-side game-logic twin of that render-side contract.
- `D:/Code/swg-client-v2/.../37/37-REVIEW.md`, `37-VERIFICATION.md` — WR-01 (inline-emitted addresses
  call-safe, not RVA-stable) + WR-05 (the `consoleHelper::sendInput` ABI mismatch behind D-02).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`directx11.cpp` `tryInstall()`/`kickoff()`** — a working consumer of a real advertised contract
  (`gl11_r.dll!GetHookPoints`). The exe-side resolver is the simpler synchronous analog (the export
  exists at inject time; no per-frame swapchain poll needed).
- **The `swg::<subsystem>` `pFn fn = (pFn)0xRVA;` pattern** — the literal-indirection-through-a-mutable-
  pointer IS the seam that makes in-place name→pointer resolution trivial; detour code is unchanged.
- **`utinni_init` already captures `g_swgModule`** (the exe handle) — the resolver reuses it.
- **Catch2 + xUnit already present** — no framework install; Wave-0 adds `endpoints_tests.cpp`.

### Established Patterns
- **DllMain stays empty (CON-H-01)** — resolver runs in `utinni_init`, never `DLL_PROCESS_ATTACH`.
- **DetourXS `DETOUR_LEN_AUTO` mandatory** — explicit length corrupts (the DetourXS trap lesson).
- **The crash is a detour-length READ, not a call** (Pitfall 1) — fix = resolve BEFORE `createDetours()`.
- **Worktrees OFF** — build waves run inline; MSBuild (not `dotnet build`), `dotnet test --no-build`.

### Integration Points
- Resolver inserts at the top of `utinni_init`, before `createDetours()`.
- Binds into the existing per-subsystem `pFn` literals (config/graphics/game/cuiManager/cuiIo/object/
  camera/objectTemplate/worldSnapshot/extent/memory/audio/treeFile/report/commandParser).
- EPA-03 hooks the existing `hkInstall` → `kickoff` chain via the resolved `graphics::install` row.

</code_context>

<specifics>
## Specific Ideas

- The consumer must be **unit-testable without injection** — factor `resolve()` to take a table pointer
  + binding list (synthetic fixture); the 3 live-smokes prove only inject + render, not resolver logic.
- "Full catalog minus documented carve-outs" — the only unbound `.inc` name is `consoleHelper::sendInput`
  (D-02); it must be explicitly allow-listed in the coverage gate so it doesn't read as a failure.
- Provider's `_o` (Optimized) flavor never exports (pre-existing LNK1281 SAFESEH) — bar is Debug+Release;
  do NOT live-test against `_o`.

</specifics>

<deferred>
## Deferred Ideas

- **WR-05 `consoleHelper::sendInput` provider thunk** — cross-repo follow-up in `swg-client-v2`
  (sanctioned write target): wrap `processInput` in a `processCurrentInput(bool)` / `__fastcall` thunk
  that allocates the recursion set internally, then bind the row in a later wave. (D-02 carve-out.)
- **x64 advertisement** — Backlog 999.7's other half; user-locked-deferred.
- **Mid-function JMP/NOP/byte patches** (~4 JMP + ~15 NOP: chat-context routing Issue #11, UI Y-axis
  cascade, debug-camera input-suppress, arbitrary `.ws` load, CrashLog inline hook) — cannot be `&fn`;
  stay SWGEmu-only until a function-entry equivalent or cooperative shim is designed. None block
  boot/render/scene.
- **`UI*::ctor` rows + MI-class ctors** (`chatWindow`/`loginScreen`/`gameMenu`) and **`groundScene::*`**
  — provider DEFERRED (multiple-inheritance PMF inflation fails the `sizeof(PMF)==sizeof(void*)` guard;
  need `__thiscall` thunks). Not in the `.inc`; keep RVA on the advertised client.
- **Wider advertised-path coverage (~198/~230 endpoints)** — the 79-row catalog is this phase's honest
  override scope on the advertised client; deeper catalog waves (the MI-class thunks, the remaining UI
  ctors) are later provider+consumer work. This is about how many endpoints the advertised client
  resolves by name — the SWGEmu RVA source path is untouched and permanent regardless (D-00).

### Reviewed Todos (not folded)
- `phase09-datatable-editor-review-warnings.md` — Phase 9 code-quality; off-domain.
- `phase10-stringtable-sc3-live-reload-residual.md` — Phase 10 live-reload; off-domain.
- `phase21-terrain-active-flag-ihdr-deeper-nesting.md` — terrain codec; off-domain.
- `swg-window-resize-fullscreen-edge-cases.md` — D3D9 presentation; separate deferred todo, off-domain.

</deferred>

---

*Phase: 24-client-entry-point-advertisement-getenginehookpoints*
*Context gathered: 2026-06-21*
