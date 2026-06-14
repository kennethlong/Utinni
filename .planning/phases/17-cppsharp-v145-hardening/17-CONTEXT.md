# Phase 17: CppSharp / v145 Hardening - Context

**Gathered:** 2026-06-14
**Status:** Ready for planning

<domain>
## Phase Boundary

The binding-generation toolchain becomes an **explicit, documented, CI-guarded configuration**, and a binding regen **can never silently break a pre-built plugin DLL**. This is a pure toolchain-hardening phase — **no new external dependencies, no product feature**.

Scope is fixed by REQUIREMENTS.md CPPS-01..04 (the spike outcome is near-certain → **harden-the-redirect**, NOT native-v145 / retire-the-redirect, which is deferred to a future milestone and gated by the CPPS-03b tripwire):
- **CPPS-01** — empirically reconfirm + document that no released CppSharp parses the MSVC v145 (14.5x) STL (the FIRST task; its documented negative result re-sets acceptance).
- **CPPS-02** — document the VS2019-14.29 parser-include redirect as the *supported* config (stop it being silently load-bearing).
- **CPPS-03** — two cheap fail-fast CI tripwires: (a) UtinniCore C++ adopting a 14.29-unparseable C++23 STL header; (b) a CppSharp release shipping clang ≥ 20.
- **CPPS-04** — an ABI gate: per-block-hash diff (ignores reorder churn, catches a real surface change) + a frozen-DLL MEF-compose fixture, with lockstep plugin rebuild.

**Not in scope (anti-creep):** upgrading CppSharp (v1.2/clang 19 only reaches v143, still needs a redirect — deferred), migrating `UtinniCoreDotNetGen` to net9, building new plugin-load machinery (reuse exists), removing the redirect.

</domain>

<decisions>
## Implementation Decisions

These four were the open implementation questions the research flagged; all were discussed and locked. Each took the research-recommended default.

### CPPS-04a — ABI baseline re-bless ergonomics
- **D-01:** When a regen **intentionally** changes the public C# surface, the maintainer re-blesses via a **`--rebless` mode on the ABI-diff tool** that regenerates the committed baseline block-hash file AND prints the lockstep checklist (rebuild TJT [+ Sytner when it becomes a C# plugin], re-freeze the plugin fixture, commit all artifacts together). The procedure is documented in `docs/ai/regen-bindings.md`. Rationale: a one-command re-bless with an emitted checklist is the mitigation for the #1 risk — the gate becoming a permanent red light on legitimate API additions.

### CPPS-04b — Frozen plugin DLL fixture
- **D-02:** Freeze **The Jawa Toolbox host plugin DLL** as the single MEF-compose fixture. Decided by inspection during this discussion (not deferred): TJT references the binding surface in **778 places across 72 files** and is the broadest consumer (touches `UtinniCore.{DirectX,ImguiGizmo,Memory,Swg,Utility,Utinni}` + `UtinniCoreDotNet.{Callbacks,Commands,Editing,Formats,Hotkeys,PluginFramework,Saving,UI,Utility}`). One fixture gives full coverage.
- **D-02a (correction to research Open-Q #2 / assumption A5):** `SytnersUtinniPlugin` is **NOT a C# MEF plugin** — the `D:/Code/UtinniPlugins/SytnersUtinniPlugin/` dir contains only a single 27-line C++ header (`sup.h`, an ASCII-art header guard), zero `.cs` files, no buildable DLL. It **cannot** be a frozen compose fixture and the planner must not hunt for a Sytner DLL. The "TJT/Sytner lockstep rebuild" success criterion effectively means **TJT today**; the standing cross-repo authority over `D:/Code/UtinniPlugins` still applies if/when Sytner becomes a C# plugin.

### CPPS-03b — clang-20 release tripwire mechanism
- **D-03:** Use a **committed "last-known-latest" pin** (CppSharp v1.2 / clang 19) asserted in CI, refreshed by a **separate manual/scheduled job** — NOT a live network probe at CI time. Rationale: no self-hosted-runner egress dependency, deterministic, can't be spoofed/poisoned. (CI does clone vcpkg from GitHub so *some* egress exists, but the pin is preferred over trusting a live registry response.)

### CPPS-03a/b — CI tripwire severity (asymmetric)
- **D-04:** **C++23-STL-header scan = HARD-FAIL** the build (a real impending break the 14.29 redirect can't parse). **clang-20 pin tripwire = WARN-loud, not block** (it is an *unblock / good-news* signal that native-v145 just became reachable — it must not turn master red and stall work; it prompts human review to consider retiring the redirect).

### Claude's Discretion
- **ABI-diff block extraction tech (CPPS-04a):** default to **BCL-only** (SHA256 over normalized public-surface blocks via line/regex extraction — no new dependency). Escalate to Roslyn (`Microsoft.CodeAnalysis.CSharp`, tooling project only, never shipped DLLs) **only if** line-based extraction proves brittle; score that package separately if reached.
- **Block-hash baseline file format / location:** a single committed sorted-key text file is fine; exact name/path is Claude's discretion (suggest co-locating with the diff tool and referencing it from `regen-bindings.md`).
- **CI step placement / scripting:** must fit the existing self-hosted, **push-only**, PowerShell-5.1, verify-only model; do NOT add a `pull_request`-from-fork trigger (locked security invariant — RCE on the v145 runner).
- **Spike script shape (CPPS-01):** the mechanical `grep '__clang_major__ < N' yvals_core.h` tabulation per installed MSVC vs CppSharp's bundled clang — exact form is discretion.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase research & scope (read first)
- `.planning/phases/17-cppsharp-v145-hardening/17-RESEARCH.md` — the full phase research (HIGH confidence): redirect mechanics, ABI-diff & compose-gate patterns, pitfalls, validation architecture, security domain.
- `.planning/REQUIREMENTS.md` — CPPS-01..04 (the binding scope; the scope-correction note that re-sets acceptance from "retire redirect" to "harden redirect").
- `.planning/ROADMAP.md` §"Phase 17" — goal + 4 success criteria.
- `.planning/research/cppsharp-msvc-14.5-upgrade.md` — definitive Path-1/2/3 analysis; CppSharp 0.10.5/clang 11 identification; `yvals_core.h` gate evidence per MSVC version.
- `.planning/research/SUMMARY.md` (v2.1) — independent corroboration of the scope correction + phase sequencing.

### The redirect (CPPS-02) — the supported config to document
- `UtinniCoreDotNetGen/Program.cs` → `ConfigureCppSharpParserStl()` — the LIVE redirect (vswhere/env/default-probe resolvers); already shipping since Phase 6 (`2f57dfa`). Phase 17 documents/guards it, does not build it.
- `docs/ai/regen-bindings.md` — current regen procedure. **STALE on two facts (fix when updating):** claims generated file is "~5000+ lines" (actual **27,659**) and references a "CppSharp version line" in output (the current auto-generated banner has **no version line** — the ABI diff must key off structural surface, not a version string).
- `docs/ai/bridge.md`, `docs/ai/build.md` — bridge + build subsystem docs.

### The ABI/compose gate harness to REUSE (CPPS-04) — do not hand-roll
- `UtinniCoreDotNet/PluginFramework/PluginLoader.cs` → `Load(dir)` + `LoadErrors` — isolated per-plugin `DirectoryCatalog` compose with failure capture.
- `UtinniCoreDotNet.Tests/PluginLoaderTests.cs` — existing `BrokenPlugin`/`GoodPlugin` fixture-load pattern (note: those are *rebuilt*; the frozen fixture must NOT be — see pitfall below).
- `Utinni.Cli/Commands/ValidatePluginCommand.cs`, `Utinni.Cli/Commands/PluginInspection.cs` — `InspectDirectory` (PEReader-based plugin reflection); the `validate-plugin` verb wraps it.
- `UtinniCoreDotNet/Generated/UtinniCore.cs` — the generated public surface = the ABI contract (27,659 lines; reorders every build; **never commit** — `git checkout --` policy).

### CI (CPPS-03) — fit the existing model
- `.github/workflows/ci.yml` — self-hosted, push-only, PowerShell-5.1, verify-only; the two existing redirect-verify steps; the locked no-`pull_request`-from-fork invariant.

### Cross-repo (CPPS-04 lockstep)
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/` — the plugin to freeze (broadest binding consumer). Standing edit/commit/push authority; paired commits need no human checkpoint.
- `D:/Code/UtinniPlugins/SytnersUtinniPlugin/sup.h` — NOT a C# plugin (single C++ header); excluded from the fixture decision.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PluginLoader.Load(dir)` + `LoadErrors`: directly powers the frozen-DLL MEF-compose assertion (CPPS-04b) — `Assert.Empty(loader.LoadErrors)` + `Assert.NotEmpty(loader.Plugins)`.
- `PluginInspection.InspectDirectory` / `validate-plugin` verb: PEReader-based reflection if the compose gate wants a structured report.
- `Program.cs` VS2019-14.29 + Win10-SDK resolvers (env → vswhere → default-probe): reuse, don't regress to hard-coded paths when adding the doc/header comment.
- BCL `System.Security.Cryptography.SHA256`: the per-block-hash diff (CPPS-04a) — zero new dependency.

### Established Patterns
- Per-block-hash **set** comparison keyed on public-surface identity (namespace+class FQN, public method/property signatures, `[DllImport(EntryPoint="<mangled>")]` strings, enum members/field layout) → order-independent so reorder churn is invisible but a real change trips.
- Mixed C++/C# build: **MSBuild `Utinni.sln` (Release|x86), never `dotnet build`**; then `dotnet test --no-build` per net472 test project.
- Worktrees OFF — run any build waves inline on the main tree.
- Grep-gate hygiene: the C++23 header scan is a LITERAL token match — keep example header names out of scanned source/comments (scope the scan to `#include` lines under `UtinniCore/` only; exclude `external/`, `Generated`, docs) or it flags its own documentation.

### Integration Points
- ABI-diff tool consumes the **freshly regenerated** `Generated/UtinniCore.cs` (post-build), compares to the committed baseline; never commits the generated file.
- Frozen TJT DLL fixture lives under `UtinniCoreDotNet.Tests/Fixtures/FrozenPlugin/` (committed binary); two new test files `AbiSurfaceTests.cs` + `FrozenPluginComposeTests.cs`.
- Two new verify-only `ci.yml` steps slot beside the existing redirect-verify steps.

</code_context>

<specifics>
## Specific Ideas

- The frozen fixture must be a **committed, pre-built** TJT DLL frozen at a known-good surface — **NEVER rebuilt in CI** (a rebuilt plugin always matches the current surface → dead gate). This is the deliberate opposite of the existing `BrokenPlugin`/`GoodPlugin` fixtures.
- The clang-20 tripwire fires as **good news** ("the redirect can finally be retired"), not a regression — hence WARN, and hence the pin can be refreshed lazily out-of-band.
- Spike is task #1; its documented negative result is what formally re-sets phase acceptance to harden-the-redirect.

</specifics>

<deferred>
## Deferred Ideas

- **CppSharp upgrade to v1.2 (clang 19) + `UtinniCoreDotNetGen` net9 migration** — only reaches MSVC v143, still needs a redirect, buys no v145-native capability. Deferred (REQUIREMENTS "Future"); the CPPS-03b tripwire is what signals when native-v145 (clang ≥ 20) finally becomes reachable.
- **Roslyn-based block extraction** — only if BCL line/regex extraction proves brittle (assumption A2); tooling project only.
- **A second frozen fixture for Sytner** — moot until/unless `SytnersUtinniPlugin` becomes a buildable C# plugin.

### Reviewed Todos (not folded)
`todo.match-phase` surfaced three keyword-only matches, all **off-domain** (generic keywords "phase"/"regen"/"churn") and left for their own phases:
- `phase09-datatable-editor-review-warnings.md` — Phase 9 datatable code-review residual.
- `phase10-stringtable-sc3-live-reload-residual.md` — Phase 10 live-reload residual.
- `swg-window-resize-fullscreen-edge-cases.md` — D3D9 presentation (Phase 18 roadmap item).

None relate to binding-toolchain hardening; not folded.

</deferred>

---

*Phase: 17-cppsharp-v145-hardening*
*Context gathered: 2026-06-14*
