---
phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
plan: 12
subsystem: managed-host-assembly-resolution
tags: [assembly-resolve, injection, netstandard-facade, loose-override-save, gap-closure]
requires:
  - "UtinniCoreDotNet.PathContainment (netstandard2.0 LooseOverridePath single source — Phase 14)"
  - "UtinniCore/clr.cpp ExecuteInDefaultAppDomain managed entrypoint"
provides:
  - "InjectedAssemblyResolver.ResolveProbePath — pure inject-root probe-path decision"
  - "AppDomain.AssemblyResolve handler at Startup.EntryPoint (installed before plugin load)"
  - "netstandard.dll deploy rule into the UtinniCoreDotNet output (CopyToOutputDirectory)"
affects:
  - "Loose-override Save tier for every editor (IFF/Datatable/Stringtable/ObjectTemplate/WorldSnapshot/Particle)"
  - "15-SMOKE Checklist B5–B8 + Checklist D (.stf/.ot saves, RESID-03)"
tech-stack:
  added: []
  patterns:
    - "Framework-leg discipline (checker B-1): pure BCL-only decision helper + injected Func<string,bool> probe seam"
    - "Narrow file-existence-gated AssemblyResolve allow-list (never a general LoadFrom-anything resolver)"
key-files:
  created:
    - "UtinniCoreDotNet/Utility/InjectedAssemblyResolver.cs"
    - "UtinniCoreDotNet.Tests/Utility/InjectedAssemblyResolverTests.cs"
  modified:
    - "UtinniCoreDotNet/main.cs"
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj"
decisions:
  - "Allow-list is exactly { netstandard, UtinniCoreDotNet.PathContainment } — narrow by design (T-15-12-02); any other name returns null"
  - "Handler installed as the FIRST statement inside if(!initialized), before new PluginLoader() — plugins call LooseOverridePath at registration"
  - "netstandard.dll deploy item uses $(SystemRoot), not a hardcoded C:\\Windows; physical bin/Release copy finalized by 15-17"
  - "Handler never throws: LoadFrom wrapped in try/catch that Log.Info's and returns null so the bind cascade is not aborted"
metrics:
  duration: ~25 min
  completed: 2026-06-13
---

# Phase 15 Plan 12: Injected-Host AssemblyResolve for the netstandard2.0 PathContainment Façade Summary

An inject-root `AppDomain.AssemblyResolve` handler (narrow, file-existence-gated, unit-tested) installed before plugin load, plus a `netstandard.dll` deploy rule, so the netstandard2.0 `UtinniCoreDotNet.PathContainment` façade binds in the injected .NET-Framework host — re-enabling loose-override Save for every editor (15-SMOKE B5).

## What Was Built

**Task 1 — `InjectedAssemblyResolver` probe-decision helper + unit coverage (commit e97d388):**
- `UtinniCoreDotNet/Utility/InjectedAssemblyResolver.cs`: a pure, BCL-only static helper (no `System.Windows.Forms`, no `UtinniCore.Utinni`, no `GroundSceneCallbacks` — same framework-leg discipline as `WorldSnapshotCommandGuard`). `ResolveProbePath(string injectRoot, string requestedAssemblyName, Func<string,bool> fileExists)`:
  1. reduces the full display name (`name, Version=…, Culture=…, PublicKeyToken=…`) to its simple name (substring before the first comma, trimmed) — the `AssemblyResolve` event hands the full display name;
  2. gates on a single auditable `static readonly string[] HandledSimpleNames = { "netstandard", "UtinniCoreDotNet.PathContainment" }` (ordinal-ignore-case); anything else → `null`;
  3. returns `Path.Combine(injectRoot, simpleName + ".dll")` only when `fileExists(candidate)` is true, else `null`.
- `UtinniCoreDotNet.Tests/Utility/InjectedAssemblyResolverTests.cs`: 5 facts (Theory expands to 7 cases) covering resolve-when-present (netstandard + PathContainment), null-when-absent, ignores-non-allow-listed names (System.Core / Newtonsoft.Json / UtinniCoreDotNet), and full-display-name → simple-name reduction. `fileExists` is stubbed — no disk dependency.
- `UtinniCoreDotNet.csproj`: explicit `<Compile Include="Utility\InjectedAssemblyResolver.cs" />` (old-style non-globbing project) + a `<Content Include="$(SystemRoot)\Microsoft.NET\Framework\v4.0.30319\netstandard.dll"><Link>netstandard.dll</Link><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>` deploy item. Verified: `netstandard.dll` lands in `bin/Release/` on build. The Tests project is SDK-style (`**/*.cs` glob) so the new test file needed no explicit include.

**Task 2 — Install the `AssemblyResolve` handler at `Startup.EntryPoint` (commit a05954f):**
- `UtinniCoreDotNet/main.cs`: as the FIRST statement inside `if (!initialized)` (before `new PluginLoader()`), compute `injectRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)` once, then `AppDomain.CurrentDomain.AssemblyResolve += …` delegating to `InjectedAssemblyResolver.ResolveProbePath(injectRoot, resolveArgs.Name, File.Exists)`; on a non-null probe `return Assembly.LoadFrom(probePath)` inside a try/catch that `Log.Info`s and returns `null` on failure; on a null probe `return null` (let the default loader continue). Added `System.IO` + `System.Reflection` usings. `SignalLauncherReady` ordering and the `FormMain` run gate are unchanged.

## Verification

- Task 1: `dotnet test … --filter InjectedAssemblyResolver` → **Passed: 7, Failed: 0** (5 facts, Theory expands the 3 non-allow-listed names).
- Task 2: `Utinni.sln` Release|x86 MSBuild **exit 0** (all native + managed projects, incl. `UtinniCoreDotNet.PathContainment.dll`).
- Full `UtinniCoreDotNet.Tests` Release suite: **Passed: 706, Failed: 0** — no regression. (The plan flagged one known pre-existing out-of-scope failure, `FindPatternHarnessTests.GetVtbl_WithD3d9Loaded_ReturnsNonZero`; it did not fire this run — net zero failures.)
- `netstandard.dll` confirmed deployed to `D:\Code\Utinni\bin\Release\netstandard.dll`.
- `Generated/UtinniCore.cs` reverted after each build (never committed).

## Deviations from Plan

None — plan executed exactly as written. Both tasks landed on the planned files with the planned signatures and wiring.

## Live Behavior (gated to 15-18, not a code change)

A cached FAILED bind is not re-driven by the new handler — the maintainer must RELAUNCH to pick up the fix in a fresh process. This is injected-only behavior and is not unit-assertable end-to-end. Live confirmation (loose-override Save from Particle + Checklist D `.stf`/`.ot` saves under injection) is gated to the 15-18 re-smoke against the 15-17 reassembled build. The physical copy of `netstandard.dll` into the shipped `bin/Release/` injection build is finalized by 15-17 (this plan owns the resolver + the deploy rule; the build deposited it into `bin/Release/` already as a side effect of the Release build).

## Threat Surface

Per the plan's `<threat_model>`: T-15-12-02 (over-broad handler hijacking unrelated binds) is mitigated by the narrow file-existence-gated allow-list — unit-proven by the non-allow-listed-name test (returns null for System.Core / Newtonsoft.Json / UtinniCoreDotNet even with `fileExists ⇒ true`). No new security surface beyond the plan's threat register.

## Self-Check: PASSED

- `UtinniCoreDotNet/Utility/InjectedAssemblyResolver.cs` — FOUND
- `UtinniCoreDotNet.Tests/Utility/InjectedAssemblyResolverTests.cs` — FOUND
- `UtinniCoreDotNet/main.cs` (modified) — FOUND
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj` (modified) — FOUND
- Commit e97d388 — FOUND
- Commit a05954f — FOUND
