---
phase: 17-cppsharp-v145-hardening
plan: 03
subsystem: testing
tags: [cppsharp, abi-gate, mef-compose, plugin-framework, sha256, frozen-fixture, lockstep, v145]

# Dependency graph
requires:
  - phase: 17-01
    provides: clang-capability spike outcome + the SUPPORTED VS 2019 14.29 parser-include redirect this gate guards
  - phase: 06-02
    provides: CppSharp parser redirect (Path 1) + Generated/UtinniCore.cs partial-class block structure the diff keys on
provides:
  - BCL-only per-block-hash ABI diff (AbiBlockHash) over the freshly-regenerated Generated/UtinniCore.cs — order-independent SET diff that ignores CppSharp reorder churn but trips on a real public-surface change (member add/remove/re-signature, DllImport EntryPoint change, enum/field-layout change)
  - One-command --rebless re-bless path that regenerates the committed block-hash baseline and prints the D-01 lockstep checklist
  - Committed, NEVER-rebuilt-in-CI frozen TheJawaToolboxDotNet.dll fixture (the broadest binding consumer) wired as Content, MEF-composing against the freshly-built bindings with zero LoadErrors
  - Lockstep-rebuilt TJT + re-frozen fixture + re-blessed baseline reflecting the same surface
provides_caveat: Maintainer live-smoke inject confirmed TJT MEF-composes live with no compose errors (APPROVED)
affects: [phase-18-render-backend-seam, regen-bindings, any-future-cppsharp-upgrade, plugin-abi]

# Tech tracking
tech-stack:
  added: []  # BCL-only — System.Security.Cryptography.SHA256; zero new NuGet/external packages
  patterns:
    - "Per-block-hash SET diff (order-independent) as the regen-churn-tolerant ABI gate — the typed analog of a whole-file byte diff that would drown a real break in reorder noise"
    - "Frozen pre-built DLL as a committed Content fixture (DELIBERATE inverse of a rebuild csproj) — a rebuilt 'frozen' plugin always matches the current surface = dead gate"
    - "--rebless as a deliberate maintainer action that re-prints the commit-all-artifacts-together lockstep checklist"

key-files:
  created:
    - UtinniCoreDotNet.Tests/AbiBlockHash.cs
    - UtinniCoreDotNet.Tests/AbiSurfaceTests.cs
    - UtinniCoreDotNet.Tests/Fixtures/abi-baseline-blockhashes.txt
    - UtinniCoreDotNet.Tests/FrozenPluginComposeTests.cs
    - UtinniCoreDotNet.Tests/Fixtures/FrozenPlugin/TheJawaToolboxDotNet.dll
  modified:
    - UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj

key-decisions:
  - "BCL-only (SHA256) ABI diff — no Roslyn / no new NuGet dependency (CONTEXT Claude's-Discretion default honored)"
  - "Block-extractor scope tracking rewritten from emit-order to an Allman-aware brace-depth scope stack — emit-order drifted ~2450 blocks across regens (a permanent red gate); brace-depth scoping is regen-stable (proven: two consecutive fresh regens → identical 4386-key set, 0 diff)"
  - "Frozen TJT DLL is a committed binary with NO rebuild csproj (deliberate inversion vs BrokenPlugin/GoodPlugin) so it does not track the current surface — the gate stays live"
  - "UtinniCore-Symbols.dll added to CopyNativeArtifactsForTests — the frozen TJT ctor P/Invokes std::basic_string at compose; without it the headless compose tripped DllNotFoundException instead of exercising the managed ABI"
  - "No paired UtinniPlugins commit — TJT source was already in lockstep with the current surface (clean Release|x86 build, byte-identical re-frozen fixture); lockstep verified by the green compose gate, not a source diff"

patterns-established:
  - "Order-independent per-block-hash ABI SET diff: the regen-churn-tolerant gate primitive (defense layer 1)"
  - "Committed never-rebuilt frozen-DLL MEF-compose fixture: defense-in-depth if the baseline is wrong (defense layer 2)"

requirements-completed: [CPPS-04]

# Metrics
duration: ~35min
completed: 2026-06-15
---

# Phase 17 Plan 03: CPPS-04 ABI Gate Summary

**BCL-only per-block-hash ABI diff (order-independent SET diff over the freshly-regenerated Generated/UtinniCore.cs) + a committed never-rebuilt frozen TJT MEF-compose fixture — two defense-in-depth layers that make a binding regen unable to silently detonate pre-built plugin DLLs at MEF compose.**

## Performance

- **Duration:** ~35 min (execution run; finalize-only continuation)
- **Tasks:** 4 (3 auto + 1 maintainer live-smoke checkpoint)
- **Files modified:** 6 (5 created + 1 modified)

## Accomplishments

- **CPPS-04a — ABI diff (defense layer 1):** `AbiBlockHash` extracts normalized public-surface blocks (namespace+class FQN, public method/property signatures, every `[DllImport(... EntryPoint=...)]` mangled-name anchor, enum members / `[FieldOffset]` layout), SHA256 → `HashSet<string>`. Reorder churn is invisible (set equality); a real surface change (added/removed/re-signatured member, changed EntryPoint) trips the gate. `AbiSurfaceTests` asserts current Except baseline + baseline Except current both empty, printing ADDED/REMOVED keys on failure, with negative/synthetic [Fact]s proving the gate trips. `--rebless` regenerates `abi-baseline-blockhashes.txt` and prints the lockstep checklist. Committed baseline = 4386 sorted block-hash keys.
- **CPPS-04b — frozen MEF-compose fixture (defense layer 2):** committed pre-built `TheJawaToolboxDotNet.dll` (the broadest binding consumer per D-02) wired as `<Content CopyToOutputDirectory>` (NOT a rebuild csproj — the deliberate inverse of Broken/GoodPlugin). `FrozenPluginComposeTests` copies it to a temp dir, `new PluginLoader(autoLoad:false).Load(dir)`, asserts `LoadErrors.Count == 0` + `Assert.NotEmpty(loader.Plugins)`. A regen that removed/re-signatured a member the frozen plugin calls lands a MissingMethod/Composition/ReflectionTypeLoad failure there. No Sytner fixture (D-02a).
- **CPPS-04 lockstep:** TJT rebuilt Release|x86 against the current binding surface; the re-built DLL was byte-identical to the frozen fixture → no paired UtinniPlugins commit needed (source already in lockstep). Baseline re-blessed from a true fresh regen after the extractor scope-stack fix.
- **Maintainer live-smoke (Task 4) APPROVED:** TJT host + SubPanels MEF-compose live in an injected SWG.exe session with no compose errors — the only place a real ABI/compose break ultimately surfaces; the two automated lanes are the proxy that catches it first.

## Task Commits

Each task was committed atomically:

1. **Task 1: CPPS-04a — BCL per-block-hash ABI diff + --rebless + blessed baseline + AbiSurfaceTests** — `edd48cc` (feat)
2. **Task 2: CPPS-04b — freeze TJT host plugin DLL as committed Content fixture + FrozenPluginComposeTests** — `993f26f` (feat)
3. **Task 3: CPPS-04 lockstep — regen-stable FQN scoping fix + re-bless from fresh surface + full-suite green** — `f098586` (fix)
4. **Task 4: Maintainer live-smoke — TJT MEF-composes in a live inject** — verified by maintainer on approval ("approved"), no commit (manual checkpoint)

**Plan metadata:** see final `docs(17-03)` commit.

_Generated/UtinniCore.cs was NEVER committed (checkout-- policy honored across all tasks)._

## Files Created/Modified

- `UtinniCoreDotNet.Tests/AbiBlockHash.cs` — BCL-only (SHA256) per-block-surface extraction + order-independent set-diff + LoadBaseline/SaveBaseline + `--rebless` (regen-stable Allman brace-depth scope stack)
- `UtinniCoreDotNet.Tests/AbiSurfaceTests.cs` — `[Fact]` current-surface == blessed-baseline ignoring reorder churn + negative/synthetic [Fact]s proving the gate trips on a real change
- `UtinniCoreDotNet.Tests/Fixtures/abi-baseline-blockhashes.txt` — committed sorted block-hash baseline (4386 keys; the ABI contract snapshot)
- `UtinniCoreDotNet.Tests/FrozenPluginComposeTests.cs` — `[Fact]` MEF-composing the frozen TJT DLL via `PluginLoader.Load` + asserting `LoadErrors.Count == 0`
- `UtinniCoreDotNet.Tests/Fixtures/FrozenPlugin/TheJawaToolboxDotNet.dll` — committed pre-built frozen plugin fixture (NEVER rebuilt in CI)
- `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` — Content wiring for the baseline + frozen DLL + UtinniCore-Symbols.dll native-artifact copy

## Decisions Made

- **BCL-only ABI diff** (SHA256), no Roslyn / no new NuGet — honors the CONTEXT Claude's-Discretion default; keeps the phase's zero-external-package invariant intact.
- **Regen-stable scope tracking** via an Allman-aware brace-depth scope stack rather than CppSharp emit-order — the only way to make the diff a stable gate rather than a permanent false-red.
- **Frozen DLL stays frozen** — committed binary, no rebuild csproj, so it cannot silently track the surface and go dead (Pitfall 6).
- **Lockstep proven by a green compose gate + byte-identical re-freeze**, not a source diff — so no paired UtinniPlugins commit was warranted this plan.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added UtinniCore-Symbols.dll to CopyNativeArtifactsForTests**
- **Found during:** Task 2 (FrozenPluginCompose fixture)
- **Issue:** The frozen TJT ctor P/Invokes into `UtinniCore-Symbols.dll` (`std::basic_string`) at MEF compose; the headless test tripped a `DllNotFoundException` instead of exercising the managed ABI surface — masking the gate's real purpose.
- **Fix:** Added `UtinniCore-Symbols.dll` to the test project's `CopyNativeArtifactsForTests` step so the native dependency is present at compose time and the test reaches the managed ABI.
- **Files modified:** `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj`
- **Verification:** FrozenPluginCompose lane green (no DllNotFoundException; compose exercises the managed surface).
- **Committed in:** `993f26f` (Task 2 commit)

**2. [Rule 1 - Bug] Rewrote the block extractor namespace/scope tracking (emit-order → Allman-aware brace-depth scope stack)**
- **Found during:** Task 3 (lockstep + fresh-regen re-bless)
- **Issue:** The original extractor keyed block FQN scope off CppSharp's emit ORDER, which drifts ~2450 blocks across regens — the gate would have been a permanent RED (every regen flags thousands of phantom ADDED/REMOVED keys), hiding any real break.
- **Fix:** Rewrote scope tracking to a brace-depth scope stack (Allman-aware) so the FQN of each block is derived from lexical nesting, not emit order; re-blessed the baseline from a true fresh regen.
- **Files modified:** `UtinniCoreDotNet.Tests/AbiBlockHash.cs`, `UtinniCoreDotNet.Tests/Fixtures/abi-baseline-blockhashes.txt`
- **Verification:** Two consecutive fresh regens → identical 4386-key set, 0 diff (determinism proven); AbiSurface lane green.
- **Committed in:** `f098586` (Task 3 commit)

**3. [Note] No paired UtinniPlugins commit**
- **Found during:** Task 3 (cross-repo lockstep step)
- **Observation:** TJT source was already in lockstep with the current binding surface — a clean Release|x86 build produced a byte-identical re-frozen fixture. The plan anticipated a possible paired UtinniPlugins commit under standing authority; none was warranted because there was no source delta.
- **Verification:** Lockstep proven by the green FrozenPluginCompose gate + byte-identical re-freeze, not a source diff.

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug) + 1 note
**Impact on plan:** Both auto-fixes were essential for the gate to function as a *true* gate (DllNotFoundException would have masked the managed ABI surface; emit-order scope drift would have made the gate permanently red). No scope creep — BCL-only invariant preserved.

## Verification Results

- **AbiSurface + FrozenPluginCompose:** 7/7 pass.
- **Full UtinniCoreDotNet.Tests:** 771/771 pass.
- **Utinni.Cli.Tests:** 263 passed (2 pre-existing skips).
- **Build:** Utinni.sln + UtinniPlugins TJT both clean Release|x86.
- **Generated/UtinniCore.cs:** never committed (checkout-- policy honored across all three task commits and the final metadata commit).
- **Determinism:** two consecutive fresh regens → identical 4386-key set, 0 diff.
- **Maintainer live-smoke:** APPROVED 2026-06-15 — TJT MEF-composes live in an injected SWG.exe session with no compose errors.

## Issues Encountered

The emit-order scope-drift discovery (deviation 2) was the central engineering issue: the first baseline was blessed from a single regen and looked correct, but a second regen would have flagged ~2450 phantom changes. Caught during the Task-3 lockstep re-bless when the fresh-regen baseline diverged from the committed one; root-caused to emit-order scope keying and fixed with a regen-stable brace-depth scope stack.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- **Phase 18 (render-backend seam) ready.** The CPPS-04 ABI gate is the foundation guard for every future binding regen that Phases 18→19 and beyond will trigger: a regen that would break the plugin ABI now goes RED at the AbiSurface + FrozenPluginCompose lanes (and ultimately at the live inject) instead of silently detonating TJT at MEF compose.
- **Re-bless discipline:** any intentional binding-surface change must run `--rebless`, rebuild + re-freeze the TJT fixture, and commit all three artifacts together (the printed lockstep checklist).
- **Phase 17 (CppSharp / v145 hardening) is complete** — CPPS-01..04 all delivered.

## Self-Check: PASSED

All 5 created files + the SUMMARY verified present on disk; all 3 task commits (edd48cc, 993f26f, f098586) verified in git history.

---
*Phase: 17-cppsharp-v145-hardening*
*Completed: 2026-06-15*
