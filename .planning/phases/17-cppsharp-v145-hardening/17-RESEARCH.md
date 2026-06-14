# Phase 17: CppSharp / v145 Hardening - Research

**Researched:** 2026-06-14
**Domain:** Binding-generation toolchain hardening (CppSharp 0.10.5 / clang 11 parser-include redirect, MSVC v145 STL gate, plugin-ABI guards, self-hosted CI tripwires)
**Confidence:** HIGH

## Summary

Phase 17 is a pure-toolchain hardening phase with **no new external dependencies** and **no product feature**. The central technical fact — already proven by the project's own prior research note `.planning/research/cppsharp-msvc-14.5-upgrade.md` (2026-05-24, and corroborated independently by the v2.1 milestone research `SUMMARY.md`, 2026-06-14) — is that **no released CppSharp can parse the MSVC v145 (14.5x) STL.** The v145 STL's `yvals_core.h` gate hard-requires clang ≥ 20 (`_EMIT_STL_ERROR(STL1000, "Unexpected compiler version, expected Clang 20 or newer.")`), and the newest released CppSharp (v1.2, 2025-11-19) bundles only clang 19 (reaching MSVC 14.4x / v143). `[VERIFIED: github.com/mono/CppSharp/releases]` This means the milestone's *originally-stated* goal — "retire the redirect / run the generator natively on v145" — is **unachievable in v2.1**. The phase re-scopes to **harden-the-redirect + tripwires + an ABI/MEF-compose gate.**

Critically, **the VS2019-14.29 parser-include redirect is already implemented and shipping** — it lives in `UtinniCoreDotNetGen/Program.cs::ConfigureCppSharpParserStl()` (merged Phase 6 Wave-2, commit `2f57dfa`), is hardened with vswhere/env-var/default-path resolution, and is already CI-verified by two existing self-hosted CI steps. So Phase 17 is **not building the redirect** — it is (1) running and documenting a confirmatory clang-capability spike, (2) lifting the redirect's rationale out of source comments into a discoverable in-repo doc, (3) adding two cheap fail-fast CI tripwires, and (4) building the new ABI-diff + frozen-DLL MEF-compose gate that catches a regen that would detonate pre-built plugin DLLs. Items (1)-(3) are small; item (4) (CPPS-04) is the real engineering work and the primary acceptance gate.

**Primary recommendation:** Make the clang-capability spike (CPPS-01) the FIRST task — its outcome is already strongly predicted (harden-the-redirect), so design the remaining tasks for that fork, but let the spike *empirically reconfirm* against the actual v145 STL on this box before locking acceptance. Reuse the existing `validate-plugin` CLI verb + `PluginLoader` + `BrokenPlugin` test-fixture infrastructure for the MEF-compose gate (CPPS-04) rather than building plugin-load machinery from scratch. Compute the per-block-hash ABI diff by normalizing-then-hashing structural units of `Generated/UtinniCore.cs` (namespaces/classes/method signatures/P-Invoke entry-points), so the known CppSharp reorder churn is order-independent and a real public-surface change still trips.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Clang-parse of MSVC STL (codegen-time) | Build tooling (`UtinniCoreDotNetGen`, net4.7.2 x64 host) | — | The generator runs at C++ post-build; parses headers, never links STL. |
| Parser-include STL redirect | Build tooling (`Program.cs::ConfigureCppSharpParserStl`) | CI verify-only steps | Decouples codegen-time STL parse (14.29/clang 11) from build-time STL compile (v145). |
| C++23-header tripwire | CI (self-hosted, push-triggered) | Build tooling (header allowlist source) | A source-scan guard; belongs in CI, fed by an in-repo allowlist/denylist. |
| clang-20-CppSharp release tripwire | CI (self-hosted, push-triggered) | — | A version probe against a pinned baseline; pure CI step. |
| ABI surface diff (per-block hash) | Build/CI tooling (new) consuming `Generated/UtinniCore.cs` | Repo (committed baseline hash) | The generated C# public surface is the ABI contract for MEF plugins; diff is a text-analysis step. |
| Frozen-DLL MEF-compose gate | Test lane (`UtinniCoreDotNet.Tests` / `Utinni.Cli.Tests`) | `validate-plugin` verb / `PluginLoader` | Plugin compose is a managed runtime behavior; verifiable via the existing test harness against a frozen plugin DLL. |
| Lockstep TJT/Sytner rebuild | Cross-repo (`D:/Code/UtinniPlugins`) | Standing authority (no checkpoint) | A regen that moves the surface forces a paired rebuild of the sibling plugin repo. |

## Standard Stack

### Core (all already present — this phase adds NO new packages)
| Library / Tool | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| CppSharp (vendored) | **0.10.5** (clang 11.0.0) | C++ header → C# binding generator | The project's existing, pinned binding generator; `external/CppSharp/lib/*.dll`. `[VERIFIED: repo + research note byte-size cross-check]` |
| MSVC v142 STL | **14.29.30133** | The parser-include redirect target (clang-11-paired STL) | The original supported pairing for clang 11 (`yvals_core.h: #if __clang_major__ < 11`). Present on dev box + CI runner. `[VERIFIED: ls of 14.29.30133/ on this box]` |
| MSVC v145 toolset | **14.51 / 14.52** | The actual C++ build toolset (VS 2026 Dev18) | Project default since Phase 6 D-09. `[VERIFIED: project_vs2026_toolchain]` |
| Windows 10 SDK | **10.0.19041** (preferred) | UCRT/shared/um includes the redirect chases into | Paired with 14.29; `ResolveLatestWindowsSdkInclude()` prefers it. `[CITED: Program.cs]` |
| System.ComponentModel.Composition (MEF) | net4.7.2 BCL | Plugin discovery/compose | Existing plugin framework (`PluginLoader.cs`). `[VERIFIED: repo]` |
| System.Reflection.Metadata / `PEReader` | net4.7.2 BCL | Managed-DLL inspection (already used by `validate-plugin`) | `PluginInspection.cs` already reflects plugin DLLs via `PEReader`. `[VERIFIED: repo]` |

### Supporting (for the ABI-diff tooling — choose from BCL, no new dep needed)
| Approach | Purpose | When to Use |
|---------|---------|-------------|
| `System.Security.Cryptography.SHA256` over normalized text blocks | Per-block-hash ABI diff | Recommended — zero new dep, deterministic, runs in the existing net472 test lane. |
| Roslyn (`Microsoft.CodeAnalysis.CSharp`) syntax-tree walk | Structural parse of `Generated/UtinniCore.cs` for block extraction | OPTIONAL — only if line-based block extraction proves too brittle; adds a NuGet dep to the tooling project (NOT to shipped DLLs). Score separately. |

### Alternatives Considered (and ruled out — do NOT pursue in v2.1)
| Instead of | Could Use | Tradeoff / Why Ruled Out |
|------------|-----------|--------------------------|
| Harden the 14.29 redirect | **Upgrade CppSharp to v1.2 (clang 19)** | Only reaches MSVC 14.4x/v143 — still needs a redirect, just to 14.4x; AND forces `UtinniCoreDotNetGen` net4.7.2 → net9.0 migration + regen revalidation. Buys NO v145-native capability. Deferred (REQUIREMENTS "Future"). `[VERIFIED: nuget + research note]` |
| CppSharp | **ClangSharp** | A raw libclang P/Invoke binding; produces no C++-class→C#-class bridge — you would hand-write the entire marshalling layer CppSharp generates. Plugin-ABI break + multi-week. Ruled out by advisor 2026-06-14. `[CITED: REQUIREMENTS.md]` |
| CppSharp | **Biohazrd** | A binding-generation *framework* (also libclang-based), not a drop-in C#-class emitter; same large hand-written-shim problem. Ruled out 2026-06-14. `[CITED: REQUIREMENTS.md]` |
| CppSharp | **C++/CLI bridge** | Would require a managed-C++ shim layer hand-written per type and an x86 C++/CLI host; reintroduces exactly the hand-marshalling CppSharp exists to remove. Ruled out 2026-06-14. `[CITED: REQUIREMENTS.md]` |

**Installation:** None. This phase installs nothing. The only environment prerequisite (VS 2019 BuildTools MSVC v142 / 14.29) is **already present on the dev box and the self-hosted CI runner** and already CI-verified.

## Package Legitimacy Audit

> Not applicable in the standard sense — Phase 17 installs **zero external packages.** All tooling is BCL (SHA256, MEF, PEReader) or already-vendored (CppSharp 0.10.5). If the planner elects the OPTIONAL Roslyn approach for block extraction, that single package must pass the gate:

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| Microsoft.CodeAnalysis.CSharp (OPTIONAL only) | NuGet | 10+ yrs | very high | github.com/dotnet/roslyn | not run (Microsoft first-party) | Approved IF Roslyn path chosen; default is BCL-only (no package) |

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none
**Default recommendation:** BCL-only (SHA256 + line-based block extraction) — no NuGet package at all.

## Architecture Patterns

### System Architecture Diagram

```
                    ┌─────────────────────── BUILD-TIME ───────────────────────┐
  UtinniCore C++    │                                                            │
  headers (v145) ───┼──► UtinniCore.vcxproj                                      │
                    │      build  (MSVC v145 / 14.5x STL)  ──► UtinniCore.dll     │
                    │         │                                    │             │
                    │   PostBuildEvent                             │             │
                    │         ▼                                    ▼             │
                    │   UtinniCoreDotNetGen.exe (net4.7.2 x64, CppSharp 0.10.5)   │
                    │         │                                                  │
                    │   ConfigureCppSharpParserStl():                            │
                    │     NoStandardIncludes=true                                │
                    │     + AddSystemIncludeDirs(MSVC 14.29 STL) ◄─── REDIRECT    │
                    │     + AddSystemIncludeDirs(Win10 SDK 19041)                 │
                    │     + re-attach clang-11 builtins                           │
                    │         │                                                  │
                    │         ▼  clang 11 parses 14.29 STL (clean)                │
                    │   Generated/UtinniCore.cs  (public C# surface = the ABI)    │
                    └─────────┼──────────────────────────────────────────────────┘
                              │
        ┌──── PHASE 17 GATES ─┼─────────────────────────────────────────────┐
        │                     ▼                                              │
        │   (CPPS-04a) per-block-hash ABI diff                              │
        │     normalize blocks → SHA256 → compare to committed baseline     │
        │       ├─ reorder-only churn  → PASS (order-independent set)        │
        │       └─ real surface change → FAIL (block hash set differs)       │
        │                     │                                              │
        │   (CPPS-04b) frozen-DLL MEF-compose fixture                       │
        │     load a pre-built plugin DLL against the NEW UtinniCoreDotNet   │
        │       └─ MissingMethodException/CompositionException → FAIL        │
        │                                                                    │
        │   (CPPS-03a) C++23-STL-header tripwire (CI source scan)           │
        │   (CPPS-03b) clang-20-CppSharp release tripwire (CI version probe)│
        └────────────────────────────────────────────────────────────────────┘
                              │  surface moved?
                              ▼
                    Lockstep rebuild: TJT + SytnersUtinniPlugin
                    (D:/Code/UtinniPlugins — standing authority, no checkpoint)
                              │
                              ▼
                    Live-smoke inject (maintainer-only checkpoint — the ONLY
                    place a compose break ultimately surfaces)
```

### Recommended Tooling Structure (no shipped-DLL changes)
```
UtinniCoreDotNetGen/
├── Program.cs                  # redirect ALREADY here (ConfigureCppSharpParserStl) — touch only to add a self-describing header comment / doc pointer
docs/ai/
├── regen-bindings.md           # UPDATE: add the redirect-is-supported section (CPPS-02)
├── cppsharp-v145-redirect.md   # NEW (or a section): the supported-config doc (CPPS-02) — spike result + why redirect is load-bearing
tools/ or scripts/  (per repo convention — verify; CI calls it)
├── abi-block-hash.*            # NEW: per-block-hash ABI diff over Generated/UtinniCore.cs (CPPS-04a)
├── allowed-cpp-stl-headers.txt # NEW: header allowlist/denylist the CI tripwire scans against (CPPS-03a)
UtinniCoreDotNet.Tests/
├── Fixtures/FrozenPlugin/      # NEW: a checked-in pre-built plugin DLL frozen at a known-good surface (CPPS-04b)
├── AbiSurfaceTests.cs          # NEW: asserts baseline block-hash set (CPPS-04a)
├── FrozenPluginComposeTests.cs # NEW: MEF-composes the frozen DLL, asserts no MissingMethod (CPPS-04b)
.github/workflows/ci.yml
└── + two new VERIFY-ONLY steps: C++23-header scan, clang-20-release probe (CPPS-03)
```

### Pattern 1: Parser-include redirect (ALREADY IMPLEMENTED — this is the supported config)
**What:** Point CppSharp's clang 11 parser at the VS2019 14.29 MSVC STL + Win10 SDK 19041 while the C++ build uses v145.
**When to use:** Always, for every regen, until a clang-20-bearing CppSharp ships.
**Mechanism (verified in `Program.cs`):**
```csharp
// Source: UtinniCoreDotNetGen/Program.cs::ConfigureCppSharpParserStl
driver.ParserOptions.NoStandardIncludes = true;                   // suppress LLVM-11 auto-detect of newest VS (would re-pick v145 STL)
driver.ParserOptions.AddSystemIncludeDirs(<14.29>/include);       // resolved via UTINNI_VS2019_ROOT → vswhere [16.0,17.0) → default-path probe
driver.ParserOptions.AddSystemIncludeDirs(<Win10SDK 19041>/{ucrt,shared,um[,winrt]});
driver.ParserOptions.AddDefines("_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH"); // silences the STL1000 version gate
driver.ParserOptions.AddSystemIncludeDirs(driver.ParserOptions.BuiltinsDir); // re-attach clang-11 builtins (throws if missing)
```
**Why it is ABI-safe:** CppSharp reads sizeof/alignof from the AST, not the live STL; the Microsoft STL ABI is stable within `_MSVC_STL_VERSION = 143` (14.29 → 14.52), so the generated bindings remain layout-correct for the v145-built `UtinniCore.dll`. `[CITED: research note "Gotchas / verified"]`

### Pattern 2: Per-block-hash ABI diff (separate real change from reorder churn) — CPPS-04a
**What:** Parse `Generated/UtinniCore.cs` into structural blocks, normalize each (strip whitespace/ordering-only differences), SHA256 each block, and compare the **set** of hashes to a committed baseline set. A set comparison is order-independent, so the CppSharp reorder churn (same blocks, different order) produces an identical set; a real change (added/removed/re-signatured member) changes the set.
**When to use:** Every CI build after the regen post-build step; the assertion lives in the net472 test lane.
**Block granularity recommendation (HIGH-signal, low-noise):** key each block by its **public-surface identity**, not its byte text:
- namespace + class fully-qualified name
- public method/property signatures (name + parameter types + return type)
- `[DllImport(... EntryPoint="<mangled>")]` entry-point strings (the actual native ABI anchor — a mangled-name change IS an ABI change)
- public enum members and field layouts
Exclude: the `<auto-generated>` banner, the `__Internal` struct's *ordering*, and any `// CppSharp version` style comment.
**Note (verified):** the current `Generated/UtinniCore.cs` header does **NOT** carry a CppSharp version string in the auto-generated banner — so the diff must NOT rely on a version line to detect generator changes; key off the structural surface instead. `[VERIFIED: head of Generated/UtinniCore.cs, 27,659 lines]`

### Pattern 3: Frozen-DLL MEF-compose fixture — CPPS-04b
**What:** Check a *pre-built* plugin DLL (frozen against a known-good binding surface) into the test fixtures tree, then at test time MEF-compose it against the **freshly-built** `UtinniCoreDotNet.dll`. If a regen removed/re-signatured a public member the frozen plugin calls, composition throws `MissingMethodException` / `CompositionException` / `ReflectionTypeLoadException` and the test fails.
**Reuse the existing harness — do NOT build new plugin-load code:**
- `PluginLoader.Load(string pluginDir)` already does per-plugin isolated `DirectoryCatalog` + `ComposeParts` and records failures in `LoadErrors`. `[VERIFIED: PluginLoader.cs]`
- `PluginLoaderTests.cs` already loads `BrokenPlugin`/`GoodPlugin` fixture DLLs from `Fixtures/<Name>/bin/Release/net472/` and asserts on `LoadErrors`. `[VERIFIED: PluginLoaderTests.cs]`
- `validate-plugin` CLI verb + `PluginInspection.InspectDirectory` already reflect a plugin dir (PEReader + PluginLoader) and emit a structured report. `[VERIFIED: ValidatePluginCommand.cs / PluginInspection.cs]`
The new fixture is a *frozen* (not rebuilt-each-CI) plugin DLL — that is the whole point: it must NOT track the current surface, so it detects drift.

### Anti-Patterns to Avoid
- **Byte-diffing the whole generated file as the ABI gate.** It will trip on every reorder churn (false positive) and is what the existing `git checkout -- Generated/UtinniCore.cs` policy exists to suppress. Use the per-block *set* hash. `[VERIFIED: project_utinnicore_cs_regen_churn]`
- **Rebuilding the frozen plugin DLL in CI.** Defeats the gate — a rebuilt plugin always matches the current surface. The fixture must be frozen.
- **Hard-coding 14.29/19041 paths.** Already avoided in `Program.cs` (vswhere/env/default-probe). Don't regress it when adding the doc.
- **Removing the redirect to "test native v145."** The spike confirms it fails; do not ship a config that depends on a clang-20 CppSharp that does not exist.
- **Grep-gate token hazard:** Phase 17's CI tripwire scans for C++23 header tokens. Per repo policy (`feedback_gsd_grep_gate_hygiene`), any acceptance criterion phrased as "grep `<format>` returns zero matches" is LITERAL — keep the example header names out of gated source comments, or the tripwire flags its own documentation.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Load a plugin DLL & detect compose failure | A fresh AppDomain + reflection loader | `PluginLoader.Load(dir)` + `LoadErrors` | Already does isolated per-DLL `DirectoryCatalog` compose with failure capture; battle-tested by C-06. |
| Reflect a plugin's exports | Manual `Assembly.LoadFrom` + attribute scan | `PluginInspection.InspectDirectory` (PEReader-based) | Already emits structured pass/fail with `[Export]` detection; `validate-plugin` verb wraps it. |
| Resolve VS2019 14.29 install path | Hard-coded path | `Program.cs` resolver (env → vswhere → default probe) | Already handles non-default installs + CI hosts; throws with an actionable install command. |
| Detect the v145 STL clang gate | Re-derive the gate logic | Read `yvals_core.h` `_EMIT_STL_ERROR` line | The gate is header-resident and self-documenting; the spike just *reads* it. |
| Diff two CppSharp outputs ignoring order | Custom line-by-line diff with heuristics | Normalized per-block SHA256 set comparison | Order-independence falls out of set semantics; no heuristic needed. |

**Key insight:** Almost every mechanism Phase 17 needs already exists in the repo — the redirect (Program.cs), the plugin compose harness (PluginLoader + PluginLoaderTests), the plugin inspector (validate-plugin), and a broken-plugin fixture pattern. The genuinely new artifacts are: the supported-config doc, two CI steps, the per-block-hash diff tool, the frozen-plugin fixture, and two test files. This is a **small, well-bounded phase** whose risk is concentrated entirely in CPPS-04.

## Runtime State Inventory

> This is a toolchain/build phase, not a rename/migration — but it DOES introduce committed baseline state (the ABI baseline hash and the frozen plugin DLL) and depends on installed toolchain state. Categories answered explicitly:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | **Committed ABI baseline** (the per-block-hash set must be checked into the repo so CI compares against it) + **frozen plugin DLL fixture** (checked into `UtinniCoreDotNet.Tests/Fixtures/`). | Generate baseline + commit it; build & freeze one plugin DLL + commit it. Document the regen-and-rebless procedure. |
| Live service config | **None** — no external service holds Phase-17 state. (Self-hosted CI runner config is in `ci.yml`, already in git.) | None — verified by inspection of `ci.yml`. |
| OS-registered state | **Installed toolchains** the redirect + CI depend on: VS2019 BuildTools MSVC **14.29.30133** (present), VS2026 v145 **14.51/14.52** (present), Win10 SDK **19041**. These are machine-local installs, NOT in git. | None to change; CI already verify-checks 14.29 + v145. Document them as prerequisites in the supported-config doc. |
| Secrets/env vars | `UTINNI_VS2019_ROOT` (optional override the redirect reads) + `UTINNI_SLN_DIR` (generator slnDir override). Code-read only; not secrets. | Document in the supported-config doc; no value change. |
| Build artifacts | `Generated/UtinniCore.cs` (27,659 lines, regenerated every build, **never committed** — `git checkout --` policy); `external/CppSharp/lib/*` vendored DLLs (committed). | The ABI-diff tool consumes the *freshly regenerated* file; do NOT commit it. The baseline-hash file IS committed. |

**The re-bless procedure (must be documented):** when a regen *intentionally* changes the public surface, the maintainer (a) rebuilds TJT + Sytner in lockstep, (b) re-generates the baseline block-hash file, (c) rebuilds & re-freezes the plugin fixture, (d) commits all three together. Without a documented re-bless path, the gate becomes a permanent red light on legitimate API additions.

## Common Pitfalls

### Pitfall 1: Treating the milestone goal "retire the redirect" as the acceptance criterion
**What goes wrong:** The phase is graded against an impossible bar (native v145 parse) and either never completes or someone sinks days into the Path-2 TFM migration that *still* needs a redirect.
**Why it happens:** The 999.4 backlog marker and the milestone name both said "finish the bump / remove the redirect."
**How to avoid:** Spike FIRST; the spike's documented negative result *re-sets* acceptance to harden-the-redirect. REQUIREMENTS.md already encodes this re-scope (CPPS-01..04). `[VERIFIED: REQUIREMENTS.md scope-correction note]`
**Warning signs:** Any plan task whose acceptance is "redirect removed" or "UtinniCoreDotNetGen runs on net9."

### Pitfall 2: A real ABI break hiding inside the reorder churn
**What goes wrong:** CppSharp reorders `Generated/UtinniCore.cs` on every build; a whole-file diff is pure noise, so a real removed/re-signatured public member slips through and detonates every plugin at MEF compose on inject.
**Why it happens:** The reorder churn trains everyone to ignore the diff (`git checkout --` reflex).
**How to avoid:** Per-block-hash **set** comparison (Pattern 2) — order-independent, so churn is invisible but a surface change trips. Pair with the frozen-DLL compose fixture (Pattern 3) as defense-in-depth.
**Warning signs:** A plugin throws `MissingMethodException`/`CompositionException` only at inject time, not at build time. `[VERIFIED: feedback_caller_attrs_binary_compat, project_utinnicore_cs_regen_churn]`

### Pitfall 3: Adding a `[Caller*]`-style defaulted param (or any signature change) without lockstep plugin rebuild
**What goes wrong:** Adding a defaulted `[CallerMemberName]` param to a public method changes its binary signature; pre-built plugin DLLs `MissingMethodException` at MEF compose even though source still compiles.
**Why it happens:** Defaulted params *look* source-compatible but are NOT binary-compatible.
**How to avoid:** The frozen-DLL compose fixture catches this in CI; the lockstep TJT/Sytner rebuild (standing cross-repo authority, no checkpoint) keeps the shipped plugins matched. Add a 1-arg binary-compat shim if a public method must change.
**Warning signs:** Source builds green, frozen-plugin compose test goes red. `[VERIFIED: feedback_caller_attrs_binary_compat]`

### Pitfall 4: The C++23-header tripwire flagging its own documentation (grep-gate self-trip)
**What goes wrong:** The CI step scans UtinniCore C++ for C++23 STL header includes; if the allowlist/denylist or a code comment literally contains the example token, the scan flags itself.
**Why it happens:** Grep gates are literal token matches.
**How to avoid:** Keep example header names in non-scanned files; scope the scan to `#include` lines under `UtinniCore/` only (exclude `external/`, `Generated`, docs). `[VERIFIED: feedback_gsd_grep_gate_hygiene — bit the project twice in 06-03]`
**Warning signs:** CI red on a doc-only commit.

### Pitfall 5: The 14.29 STL can't parse a C++23 header UtinniCore newly adopts
**What goes wrong:** UtinniCore C++ adopts `<expected>`/`<format>`/`<ranges>` etc.; clang 11 against the 14.29 STL chokes; the regen breaks silently or produces broken bindings.
**Why it happens:** 14.29 predates C++23; the redirect is only safe for the feature surface 14.29 supports.
**How to avoid:** This is EXACTLY what the CPPS-03a tripwire guards — a header allowlist scan that fails CI *before* the regen breaks. Current scan baseline is **clean** (no C++23-risky headers found in `UtinniCore/` today). `[VERIFIED: grep of UtinniCore/ for format/ranges/concepts/expected/span/coroutine/etc. → zero matches, 2026-06-14]`
**Warning signs:** A generator crash "Clang couldn't parse <header>"; or the tripwire firing on a new `#include`.

### Pitfall 6: Frozen plugin fixture that silently tracks the current surface
**What goes wrong:** If CI rebuilds the "frozen" plugin from source each run, it always matches and never detects drift — a dead gate.
**Why it happens:** Treating it like the existing `BrokenPlugin`/`GoodPlugin` fixtures (which ARE rebuilt).
**How to avoid:** Commit the *compiled* plugin DLL (binary fixture) frozen at a known surface; load it as-is. Document the deliberate re-bless step.
**Warning signs:** The compose test never fails even when you intentionally break the surface in a local experiment.

## Code Examples

### Reading the v145 STL clang gate (the spike's core evidence — CPPS-01)
```
// Source: yvals_core.h on this box (verified paths in the research note)
// VS 2026 v145 (MSVC 14.51.36231 & 14.52.36328):
//   #if __clang_major__ < 20
//   _EMIT_STL_ERROR(STL1000, "Unexpected compiler version, expected Clang 20 or newer.");
// VS 2022 (MSVC 14.44.35207):
//   _EMIT_STL_ERROR(STL1000, "Unexpected compiler version, expected Clang 19.0.0 or newer.");
// VS 2019 v142 (MSVC 14.29.30133):
//   #if __clang_major__ < 11   ← the redirect target; clang 11 accepted.
```
The spike script: for each installed MSVC version, `grep` the `__clang_major__ < N` line out of `yvals_core.h`; tabulate N vs CppSharp's bundled clang (11 vendored; 19 latest released). Conclusion is mechanical: 11 < 20 and 19 < 20 → no released CppSharp parses v145. `[VERIFIED: research note "Local evidence"]`

### Per-block-hash set comparison (CPPS-04a sketch, BCL-only)
```csharp
// Source: pattern derived for this phase (BCL SHA256; no new dep)
// 1. Extract public-surface blocks from Generated/UtinniCore.cs:
//      - namespace + class FQN
//      - public method/property signatures
//      - [DllImport(... EntryPoint="<mangled>")] strings  ← the native ABI anchor
//      - public enum members / field layout
// 2. Normalize each block (trim, collapse ws, drop ordering-only artifacts).
// 3. SHA256(block) → add to a HashSet<string>.
// 4. Compare HashSet to committed baseline:
//      reorder churn  → identical set  → PASS
//      real change    → set difference → FAIL, print added/removed block keys
```

### Frozen-DLL compose assertion (CPPS-04b — reuse PluginLoader)
```csharp
// Source: pattern over existing PluginLoader.cs (verified API)
var loader = new PluginLoader(autoLoad: false);
loader.Load(frozenPluginDir);          // isolated DirectoryCatalog + ComposeParts
Assert.Empty(loader.LoadErrors);       // any MissingMethod/Composition failure lands here
Assert.NotEmpty(loader.Plugins);       // the frozen plugin actually composed
```

### CI tripwire steps (CPPS-03 — fit the self-hosted, verify-only, PowerShell-5.1 model)
```powershell
# (a) C++23-STL-header tripwire: fail if UtinniCore/ adopts a header 14.29 can't parse.
#     Scope to #include lines under UtinniCore/ only (exclude external/, Generated, docs).
#     Compare against an in-repo allowlist; new risky header → throw.
# (b) clang-20-CppSharp release tripwire: probe the latest CppSharp release/NuGet;
#     if a release ships clang >= 20 (i.e. native v145 becomes reachable), fail LOUD
#     so the team knows the redirect can finally be retired.
#     Cheapest reliable form: pin a known baseline (v1.2 / clang 19) and assert the
#     observed latest == baseline; a newer release trips the step for human review.
```
Note: the clang-20 probe must degrade gracefully when the self-hosted runner has no network / the registry is unreachable — a probe failure should warn, not hard-fail the build, OR run against a committed "last-known-latest" pin updated by a separate scheduled job. Confirm the self-hosted runner's egress policy during planning.

## State of the Art

| Old Approach (milestone-as-stated) | Current Approach (re-scoped) | When Changed | Impact |
|--------------|------------------|--------------|--------|
| "Upgrade CppSharp, run generator natively on v145, retire the redirect" | "Harden + document the 14.29 redirect; add tripwires; gate the ABI surface" | 2026-06-14 (v2.1 research SUMMARY + REQUIREMENTS scope-correction) | Acceptance criteria re-set; native-v145 deferred to a future milestone, gated by the clang-20 tripwire. |
| Whole-file `git checkout --` to suppress regen churn | Per-block-hash set diff that ignores churn but catches real ABI change | This phase | Real breaks no longer hide in churn. |
| Redirect rationale buried in `Program.cs` comments | Redirect documented in-repo as the *supported* config | This phase (CPPS-02) | Stops the config being "silently load-bearing." |

**Deprecated/outdated:**
- `docs/ai/regen-bindings.md` is **stale on two facts**: it says the generated file is "~5000+ lines" (actually **27,659**) and references "a CppSharp version line" in the output (the current auto-generated banner has **no version line**). Update both when adding the supported-config section. `[VERIFIED: repo, 2026-06-14]`
- The 999.4 backlog marker's "Path 2 → v143, net9 migration" framing is the now-deferred future work, not this phase's deliverable.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The self-hosted CI runner can reach the network to probe the latest CppSharp release for the clang-20 tripwire (or a committed "last-known-latest" pin is acceptable). | CPPS-03b CI step | If no egress, the probe must use a committed pin + scheduled refresh; affects tripwire design. **Confirm runner egress in planning.** |
| A2 | A line/regex-based block extraction of `Generated/UtinniCore.cs` is robust enough; Roslyn is not required. | CPPS-04a | If extraction proves brittle, add Roslyn to the *tooling* project (not shipped DLLs) — scored separately. |
| A3 | The Microsoft STL ABI is stable across 14.29 → 14.52 (`_MSVC_STL_VERSION = 143`), so 14.29-parsed bindings remain layout-correct for v145 builds. | Pattern 1 | This is the redirect's load-bearing assumption; already shipping & live-smoked since Phase 6. Cited from research note, not re-verified this session. |
| A4 | `StdEdited.cs` needs no changes (14.29 was 0.10.5's original pairing). | Pattern 1 / regen | Low risk — already true in the shipping config; re-confirm with a diff during the spike. |
| A5 | One representative frozen plugin DLL (e.g. a Sytner/TJT editor plugin) gives sufficient ABI coverage for the compose gate. | CPPS-04b | If different plugins exercise disjoint binding surfaces, may need >1 frozen fixture. Confirm which plugin touches the widest surface. |

## Open Questions

1. **Self-hosted runner network egress for the clang-20 probe.**
   - What we know: CI is push-triggered, verify-only, PowerShell 5.1; it already clones vcpkg from GitHub (so *some* egress exists).
   - What's unclear: whether an arbitrary NuGet/GitHub-API probe is allowed/reliable at CI time.
   - Recommendation: default to a committed "last-known-latest CppSharp = v1.2/clang 19" pin asserted in CI, refreshed by a separate scheduled/manual job; treat live probe as a nice-to-have.

2. **Which plugin DLL to freeze for the compose fixture.**
   - What we know: `D:/Code/UtinniPlugins` holds `SytnersUtinniPlugin` + `The Jawa Toolbox`; both consume the binding surface.
   - What's unclear: which exercises the widest public surface (best coverage for one fixture).
   - Recommendation: freeze the TJT host plugin (broadest binding consumer) and/or `SytnersUtinniPlugin`; confirm during planning by inspecting which references the most `UtinniCore.*` types.

3. **Block-hash baseline storage + re-bless ergonomics.**
   - What we know: the baseline must be committed; regen churn must not trip it.
   - What's unclear: file format + the exact maintainer re-bless command.
   - Recommendation: a single committed text file of sorted block-hash keys + a `--rebless` mode on the diff tool; document in `regen-bindings.md`.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| VS2019 BuildTools MSVC v142 (STL) | Parser-include redirect | ✓ | **14.29.30133** | env `UTINNI_VS2019_ROOT`; else CI throws with `choco install` command |
| VS2026 v145 toolset | C++ build | ✓ | 14.51 / 14.52 | none (the build target) |
| Windows 10 SDK | Redirect include chase | ✓ (assumed; resolver prefers) | 10.0.19041 pref | highest-installed 10.0.* fallback in resolver |
| CppSharp (vendored) | Binding generation | ✓ | 0.10.5 / clang 11 | none (pinned) |
| net4.7.2 (generator host) | `UtinniCoreDotNetGen.exe` | ✓ | x64 PlatformTarget | none |
| net472 test runner | ABI + compose test lanes | ✓ | `dotnet test --no-build` | none |
| Self-hosted CI runner | All gates | ✓ | windows / x64 / utinni-v145 | none (v145 is Insiders-only) |

**Missing dependencies with no fallback:** none — every prerequisite is present on the dev box and CI runner and already CI-verified.
**Missing dependencies with fallback:** Win10 SDK version is auto-resolved (preferred 19041 else highest installed).

## Validation Architecture

> `workflow.nyquist_validation` is absent from `.planning/config.json` → treated as ENABLED.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (`UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`) |
| Config file | per-project `.csproj` (net472); built via VS2026 MSBuild |
| Quick run command | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build -c Release` |
| Full suite command | MSBuild `Utinni.sln` (Release\|x86) THEN `dotnet test --no-build` per project (mixed C++/C# solution — never `dotnet build`, never `dotnet test Utinni.sln`) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CPPS-01 | Spike empirically shows no released CppSharp parses v145 STL; result documented | doc + script | `grep '__clang_major__' yvals_core.h` tabulation script (committed) | ❌ Wave 0 |
| CPPS-02 | 14.29 redirect documented as supported config | doc gate | doc-exists check / link from `regen-bindings.md` | ❌ Wave 0 |
| CPPS-03a | CI fails if UtinniCore adopts a 14.29-unparseable C++23 header | CI scan | new `ci.yml` step: `#include` allowlist scan under `UtinniCore/` | ❌ Wave 0 |
| CPPS-03b | CI fails/warns if a CppSharp release ships clang ≥ 20 | CI probe | new `ci.yml` step: version-pin assert | ❌ Wave 0 |
| CPPS-04a | Per-block-hash ABI diff catches real surface change, ignores churn | unit | `dotnet test UtinniCoreDotNet.Tests --no-build -c Release --filter AbiSurface` | ❌ Wave 0 |
| CPPS-04b | Frozen-DLL MEF-compose fixture fails on a surface-breaking regen | unit | `dotnet test UtinniCoreDotNet.Tests --no-build -c Release --filter FrozenPluginCompose` | ❌ Wave 0 |
| CPPS-04 (lockstep) | TJT + Sytner rebuild green in the same wave | cross-repo build | MSBuild both repos Release\|x86 | n/a (build, not test) |

### Sampling Rate
- **Per task commit:** the relevant filtered test (`AbiSurface` or `FrozenPluginCompose`) + a clean regen + `git checkout -- Generated/UtinniCore.cs`.
- **Per wave merge:** full net472 test lanes + the two new CI tripwire steps + both-repo Release\|x86 build.
- **Phase gate:** full suite green; **maintainer live-smoke inject** (the only place a compose break ultimately surfaces — RNDR/MEF compose is runtime).

### Wave 0 Gaps
- [ ] `UtinniCoreDotNet.Tests/AbiSurfaceTests.cs` — covers CPPS-04a
- [ ] `UtinniCoreDotNet.Tests/FrozenPluginComposeTests.cs` — covers CPPS-04b
- [ ] `UtinniCoreDotNet.Tests/Fixtures/FrozenPlugin/<frozen>.dll` — committed binary fixture (CPPS-04b)
- [ ] ABI baseline block-hash file (committed) + the diff tool (CPPS-04a)
- [ ] `allowed-cpp-stl-headers.txt` (or inline allowlist) for the CI scan (CPPS-03a)
- [ ] Spike script + result doc (CPPS-01) and supported-config doc (CPPS-02)
- [ ] Two new `ci.yml` steps (CPPS-03a/b) — must fit self-hosted, push-only, PowerShell-5.1, verify-only model
- [ ] Framework install: none — xUnit/net472 lanes already exist.

## Security Domain

> `security_enforcement` not set in config → treated as enabled. Phase 17 is a build-tooling phase; the threat surface is narrow but real (CI scripts + a verb that loads arbitrary DLLs).

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1 Architecture | yes | Redirect documented as supported config (CPPS-02) removes a silent single-point-of-failure. |
| V5 Input Validation | yes | The CI header-scan + version-probe must validate/escape inputs (paths, registry responses) before use. |
| V10 Malicious Code / Supply Chain | yes | `validate-plugin` and the compose fixture **load and execute DLL static initializers** — the verb already warns "only run against trusted plugin directories." The frozen fixture must be a repo-controlled, reviewed binary. |
| V12 Files & Resources | yes | Block-hash baseline + frozen DLL are committed artifacts; treat as trusted-only, review on change. |
| V14 Configuration | yes | Self-hosted runner is push-only by design (no fork-PR RCE); do NOT add a `pull_request` trigger that would run untrusted code against the v145 runner. `[CITED: ci.yml comment]` |

### Known Threat Patterns for {self-hosted CI + DLL-loading verb}
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Untrusted PR code reaching the self-hosted runner | Elevation of Privilege / RCE | Keep push-only trigger; never add `pull_request` from forks (existing, locked). |
| Frozen-plugin fixture or baseline replaced with malicious binary | Tampering | Repo review on any fixture/baseline change; the fixture executes static ctors at compose. |
| clang-20 probe trusting a spoofed/poisoned registry response | Tampering / Spoofing | Prefer a committed pin asserted in CI over live network trust; if live, pin the host + validate the response shape. |
| Header-scan command injection via crafted file paths | Tampering | Quote/escape paths; scope the scan to a fixed root (`UtinniCore/`). |

## Sources

### Primary (HIGH confidence)
- `.planning/research/cppsharp-msvc-14.5-upgrade.md` (2026-05-24) — definitive Path-1/2/3 analysis; vendored-CppSharp identification (0.10.5/clang 11, byte-size cross-check); yvals_core.h gate evidence per MSVC version; local-evidence file paths.
- `.planning/research/SUMMARY.md` (v2.1 research, 2026-06-14) — independent corroboration of the scope correction; Pitfalls 4 & 5; phase sequencing.
- `UtinniCoreDotNetGen/Program.cs` — the live `ConfigureCppSharpParserStl()` redirect + resolvers (vswhere/env/default-probe).
- `UtinniCoreDotNet/PluginFramework/PluginLoader.cs`, `UtinniCoreDotNet.Tests/PluginLoaderTests.cs`, `Utinni.Cli/Commands/{ValidatePluginCommand,PluginInspection}.cs` — the reusable compose/inspect harness.
- `.github/workflows/ci.yml` — self-hosted, push-only, PowerShell-5.1, verify-only model + the two existing redirect-verify steps.
- `docs/ai/regen-bindings.md` — current regen procedure (note: stale line-count + version-line claims).
- Repo grep (2026-06-14) — UtinniCore/ has ZERO C++23-risky STL `#include`s today (clean tripwire baseline); `Generated/UtinniCore.cs` is 27,659 lines with no version banner.
- This box: `ls 2019/.../MSVC/` → `14.29.30133` present.

### Secondary (MEDIUM confidence)
- `github.com/mono/CppSharp/releases` (fetched 2026-06-14) — latest v1.2 / clang 19; no clang-20 release. Confirms harden-redirect scope.

### Tertiary (LOW confidence)
- None — all load-bearing claims are grounded in repo inspection or the prior research note.

## Metadata

**Confidence breakdown:**
- Standard stack (no new deps; CppSharp 0.10.5 + 14.29 redirect): HIGH — directly inspected + dual-research corroboration.
- Architecture (redirect mechanism, ABI-diff & compose-gate design): HIGH — redirect verified in source; gate design reuses existing harness.
- Pitfalls: HIGH — every pitfall anchored to a captured project incident or verified repo fact.
- clang-20 tripwire baseline: HIGH — latest release verified via GitHub releases this session.

**Research date:** 2026-06-14
**Valid until:** ~2026-09-14 (stable; the one moving part is a future CppSharp release shipping clang ≥ 20 — which is precisely what CPPS-03b is built to detect).
