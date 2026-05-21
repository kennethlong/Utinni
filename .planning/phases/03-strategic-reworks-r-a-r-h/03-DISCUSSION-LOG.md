# Phase 3: Strategic reworks (R-A..R-H) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-21
**Phase:** 3-strategic-reworks-r-a-r-h
**Areas discussed:** Plan structuring, R-A depth (callback symmetry), R-B plugin lifecycle ABI, R-C single-source RVAs

---

## Plan structuring

### Q1: How should the 7 remaining R-items be grouped into plans?

| Option | Description | Selected |
|--------|-------------|----------|
| By category (3 plans) | Callbacks (R-A + R-H + IN-05) / Lifecycle+RVAs (R-B + R-C) / Build-tooling+logging (R-E + R-F + R-G) | ✓ |
| Per-rework (7 plans) | One plan per R-letter. Maximum atomicity but lots of plan overhead. | |
| By layer (3 plans, different split) | Native / Managed / Build-tooling | |
| By risk-tier (3 plans like Phase 02) | Trivial / single-file / architectural | |

**Notes:** Categories share dispositional decisions (Subscribe shape informs snapshot mechanism; destroyPlugin shape informs CRT discipline). Matches Phase 02's risk-tier precedent in spirit.

### Q2: Within the 3-plan structure, what plan ordering across CI gates?

| Option | Description | Selected |
|--------|-------------|----------|
| Callbacks → Lifecycle/RVAs → Build-tooling | Lowest blast radius first | ✓ |
| Build-tooling → Callbacks → Lifecycle/RVAs | Front-load R-F header auto-discovery | |
| Lifecycle/RVAs → Callbacks → Build-tooling | R-B most plugin-author-visible first | |

**Notes:** R-A/R-H are greppable mechanical refactors; verifying green CI before R-B/R-C work isolates regressions. R-F last so it picks up new UTINNI_API symbols from R-B/R-C cleanly.

### Q3: Cross-repo touch: R-B's plugin lifecycle changes will affect UtinniPlugins. How to handle?

| Option | Description | Selected |
|--------|-------------|----------|
| Coordinated PR pair | Plan tracks UtinniPlugins-side as paired commit (Phase 02 C-13 precedent) | ✓ |
| Ship Utinni-side with back-compat shim | Keep old ABI as fallback alongside new | |
| Utinni-only (break UtinniPlugins temporarily) | Cleanest Utinni-side, biggest user-visible regression | |

**Notes:** Manual TJT build verifies (no UtinniPlugins CI yet; Phase 02 D-09 precedent).

### Q4: Verification posture for R-items without clean unit-test path?

| Option | Description | Selected |
|--------|-------------|----------|
| Max-harness with documented carve-outs | Real harness where possible + Verified-by: block + partial-proof for hard cases | ✓ |
| Max-harness uniformly | Force a real harness for every R-X, even if requires fixture infra | |
| Lighter — preservation tests only | Skip per-R-X harness | |

**Notes:** Honors feedback_max_harness with explicit boundaries. Continuation of Phase 02/02.1 posture.

---

## R-A depth (callback symmetry)

### Q1: What's the R-A depth target?

| Option | Description | Selected |
|--------|-------------|----------|
| Handle-based Subscribe/Unsubscribe | int Subscribe(fn) → id; Unsubscribe(id). Solves TD-15 dangling-fn-ptr class. | ✓ |
| Mechanical Add/Remove only | Matches assessment.md letter; still leaves dangling-ptr-on-unload | |
| C# event semantics for managed side | Replace SynchronizedCollection with .NET event; mixed paradigms across boundary | |

**Notes:** Lines up with R-B's lifecycle work; best long-term seam.

### Q2: Handle type for native Subscribe?

| Option | Description | Selected |
|--------|-------------|----------|
| Opaque int handle | Simplest ABI, P/Invoke-friendly, matches assessment.md suggestion | ✓ |
| Typed CallbackHandle struct (id + generation) | Generation guards against id reuse; ABI churn for struct return | |
| Function pointer is its own handle | O(N) Unsubscribe + breaks for capturing-lambda managed delegates | |

**Notes:** Handle 0 reserved as invalid sentinel.

### Q3: Where does IN-05's Drain(ConcurrentQueue<Action>) helper land?

| Option | Description | Selected |
|--------|-------------|----------|
| Inside R-A managed plan as standalone task | First task of Callbacks plan; shared location | ✓ |
| Bundled into each Add*/Remove* task | More commits, tighter cohesion per file | |
| Defer to Phase 6 STAB-03 | Phase 02.1 deferral pattern; but already called out as R-A territory | |

**Notes:** Lands first so new Remove paths can reuse the helper.

### Q4: Back-compat for existing UtinniPlugins (TJT, Sytner)?

| Option | Description | Selected |
|--------|-------------|----------|
| Keep Add* as wrapper around Subscribe, no Remove from Add | Source-compat preserved; existing plugins work without recompile | ✓ |
| Mark Add* [Obsolete] and require Subscribe | Forces migration; warning noise | |
| Hard-break: remove Add*, require Subscribe | Cleanest API but requires coordinated UtinniPlugins update | |

**Notes:** Migration is opt-in per plugin; new code uses Subscribe.

---

## R-B plugin lifecycle ABI

### Q1: destroyPlugin ABI shape — how does the host release a plugin's instance?

| Option | Description | Selected |
|--------|-------------|----------|
| Symmetric exported destroyPlugin(UtinniPlugin*) | Plugin owns alloc and free in its own CRT; industry-standard symmetric factory | ✓ |
| Virtual destructor only (current state, harden) | Trust virtual ~UtinniPlugin() + CRT-equality check at load time | |
| destroyPlugin via UtinniPlugin virtual method | Add virtual void destroyThis() = 0; plugin overrides | |

**Notes:** Eliminates cross-CRT crash class (CON-B-04 territory). UTINNI_PLUGIN macro extended to template both exports.

### Q2: When is plugin->init() invoked? Currently never.

| Option | Description | Selected |
|--------|-------------|----------|
| After ALL plugins instantiated (two-pass) | Pass 1 createPlugin all; pass 2 init all in load order | ✓ |
| Per-plugin, immediately after createPlugin returns | Simpler; isolates init failures; blocks sibling-lookup pattern | |
| Lazy / first-use | Hardest to reason about | |

**Notes:** Lets a plugin's init() look up sibling plugins. Per-plugin try/catch around init() (extending Phase 02 C-06 isolation).

### Q3: CON-O-07 — Sytner's plugin status / ABI compat target?

| Option | Description | Selected |
|--------|-------------|----------|
| Treat as legacy / no compat target | Upstream dormant; our kennethlong fork doesn't actively ship it | ✓ |
| Preserve as best-effort compat | Host falls back to virtual destructor if no destroyPlugin found | |
| Active maintenance — require Sytner update | Coordinate with maintainer; hard ABI break | |

**Notes:** Documented disposition: 'dormant; no compat target'. Commit closes CON-O-07 alongside R-B fix.

### Q4: HMODULE tracking + FreeLibrary on shutdown?

| Option | Description | Selected |
|--------|-------------|----------|
| Store HMODULE per plugin + FreeLibrary in ~PluginManager | Proper symmetric cleanup; required for V2 hot-reload | ✓ |
| Track HMODULE but never FreeLibrary | Diagnostics only; process is dying anyway | |
| No HMODULE tracking | Smallest change but blocks future hot-reload | |

**Notes:** Shutdown order: destroyPlugin in plugin's CRT, then FreeLibrary (host's call is fine — DLL ref-count, not allocation).

---

## R-C single-source RVAs

### Q1: R-C scope — which RVAs get the single-source treatment?

| Option | Description | Selected |
|--------|-------------|----------|
| Just the actual duplicate (WndProc 0x00AA0970) | Smallest blast radius; closes TD-18 actual duplication | ✓ |
| All native↔managed boundary RVAs (proactive) | Surface every RVA that *could* be referenced from managed | |
| All ~25 detoured RVAs | Maximalist read of assessment.md; mostly hypothetical duplications | |

**Notes:** isSafeToUse RVAs (0x01908858, 0x01919410) stay native-only — not duplicated to managed. Don't pre-architect.

### Q2: Managed access mechanism for the WndProc RVA?

| Option | Description | Selected |
|--------|-------------|----------|
| UTINNI_API getter function | UTINNI_API IntPtr getSwgWndProc() in client.h; CppSharp auto-projects | ✓ |
| Compile-time constant via #define + codegen | Pure compile-time; zero runtime cost | |
| Read from a rvas.json file at startup | Most flexible; heaviest; overkill for V1 | |

**Notes:** Native stays source of truth. CppSharp picks up via R-F or current allowlist.

### Q3: Resolution timing for the WndProc getter?

| Option | Description | Selected |
|--------|-------------|----------|
| Once in PanelGame.Initialize/ctor + cache as field | Matches today's shape; zero per-message overhead | ✓ |
| Read every WndProc call | Wastes P/Invoke calls in hot path | |
| Read at process startup + static cache | Marginal improvement vs ctor cache | |

**Notes:** Literal `new IntPtr(0x00AA0970)` at PanelGame.cs:41 becomes cached P/Invoke result.

### Q4: Verification harness shape for R-C?

| Option | Description | Selected |
|--------|-------------|----------|
| Two-part: file grep + P/Invoke return-value test | Verify 'no longer duplicated' AND 'getter works' | ✓ |
| Grep-only (single test) | Verifies rework, not getter runtime behavior | |
| Manual Verified-by: only | Cheap but contradicts max-harness | |

**Notes:** Live-SWG forwarding path is Tier-4 manual (D-06).

---

## Claude's Discretion

- Exact xUnit test naming (follow `[Method]_[Scenario]_[ExpectedOutcome]` convention from Phase 1).
- Task ordering WITHIN a plan (planner picks based on dependency).
- Whether the `Drain` helper lives in a new `Callbacks/CallbackHelpers.cs` or folds into an existing utility file.
- Final naming for the `Utinni.CrtMatchPlugin` / `Utinni.LegacyPlugin` fixture projects.
- Whether R-F's `_internal/` directory convention is exercised in Phase 3 by moving any existing header.
- Order of R-E/R-F/R-G within Plan 03-03.
- Whether CON-O-05/-07 disposition updates fold into their related fix commits or get a roll-up.

## Deferred Ideas

- Plugin hot-reload runtime exercise — V2 (R-B enables structurally; actual flow deferred).
- Add* marked [Obsolete] — Phase 6 STAB-03 or V2.
- Surfacing isSafeToUse RVAs via UTINNI_API — not needed today.
- Other ~25 detoured RVAs surfaced via UTINNI_API — R-C pattern is the template if duplication appears.
- TD-25 empty stubs / TD-26 disabled hooks / TD-27 hardcoded font / TD-28 TJT.ico framework default — Phase 6 STAB-03.
- CON-O-08 / CON-O-06 / CON-O-09 / CON-O-11 — Phase 4 or Phase 6 depending on the open question.
- UtinniPlugins CI bootstrap — Phase 7+ or dedicated bridging phase.
- .clang-format / .editorconfig sweep — Phase 6 STAB-03.
- Coverage tooling (coverlet, ReportGenerator) — revisit after Phase 4/5.
