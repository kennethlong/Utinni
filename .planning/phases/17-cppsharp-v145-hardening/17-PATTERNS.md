# Phase 17: CppSharp / v145 Hardening - Pattern Map

**Mapped:** 2026-06-14
**Files analyzed:** 9 new artifacts + 2 doc updates + 5 read-only source-of-truth files
**Analogs found:** 8 with strong matches / 9 new artifacts (1 has no managed-test analog by design — the CI YAML steps)

> Pure toolchain/CI hardening phase. NO product feature, NO new external dependency. Almost every
> mechanism this phase needs already exists in-repo (the redirect, the MEF compose harness, the
> plugin inspector, the fixture-DLL pattern, the SHA256/golden-vector test idiom, the verify-only CI
> step idiom). The genuinely-new code is thin glue + committed baseline state.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `UtinniCoreDotNet.Tests/AbiSurfaceTests.cs` (NEW) | test (xUnit) | file-I/O + transform (hash-set diff) | `HeaderDiscoveryTests.cs` (real-file walk-up + Discover) + `LiveBridgeProtocolTests.cs` (golden byte-vector assert) | role+flow match |
| ABI block-hash diff tool (NEW; location = discretion) | utility (BCL-only) | transform (parse → normalize → SHA256 set) | `UtinniCoreDotNetGen/HeaderDiscovery.Discover` (static pure file→list) + `PluginInspection.RawBytesHasPluginAttributeString` (BCL byte/string scan) | role match |
| ABI baseline block-hash text file (NEW, committed) | config/data | stored data | the four `Fixtures/live/wire-*.json` golden vectors (committed, content-copied) | partial (committed-baseline pattern) |
| `UtinniCoreDotNet.Tests/FrozenPluginComposeTests.cs` (NEW) | test (xUnit) | event-driven (MEF compose) | `PluginLoaderTests.cs` (`Load(dir)` + `LoadErrors` assertion) | exact |
| `UtinniCoreDotNet.Tests/Fixtures/FrozenPlugin/<frozen>.dll` (NEW, committed binary) | test fixture | stored data | `Fixtures/{BrokenPlugin,GoodPlugin}` (DELIBERATE inverse — frozen, NOT rebuilt) | role match (inverted) |
| `allowed-cpp-stl-headers.txt` (NEW, or inline allowlist) | config/data | stored data | (no direct analog — new artifact) | none |
| `ci.yml` C++23-header scan step (NEW, verify-only) | CI step | batch scan (`#include` grep under `UtinniCore/`) | the clang-format style-gate step + the two redirect-verify steps in `ci.yml` | role+flow match |
| `ci.yml` clang-20 pin tripwire step (NEW, verify-only/WARN) | CI step | batch (pin assert) | the "Verify VS 2019 BuildTools (MSVC 14.29)" verify step in `ci.yml` | role match |
| CPPS-01 spike script (NEW) | utility/script | batch (grep `__clang_major__` per MSVC `yvals_core.h`) | the v145 / VS2019 vswhere-resolve verify steps in `ci.yml` (PowerShell 5.1) | partial |
| `docs/ai/regen-bindings.md` (UPDATE — stale facts) | doc | n/a | itself (existing structure) | n/a |
| supported-config doc / section (NEW, CPPS-02) | doc | n/a | `regen-bindings.md` prose style | n/a |

**Read-only source-of-truth (the executor MUST read, MUST NOT rewrite):**

| File | Why read | Allowed touch |
|------|----------|---------------|
| `UtinniCoreDotNetGen/Program.cs` → `ConfigureCppSharpParserStl` | the LIVE redirect being documented (CPPS-02) | header-comment / doc-pointer ONLY; do not change resolver logic |
| `Utinni.Cli/Commands/PluginInspection.cs` → `InspectDirectory` | reuse for a structured compose report if wanted | none (reuse via call) |
| `Utinni.Cli/Commands/ValidatePluginCommand.cs` | the `validate-plugin` verb wrapping the inspector | none |
| `UtinniCoreDotNet/Generated/UtinniCore.cs` | the ABI contract the diff parses (27,659 lines; reorders every build; **never commit** — `git checkout --`) | none (consume freshly-regenerated) |
| `.github/workflows/ci.yml` | the self-hosted, push-only, PS-5.1, verify-only model the new steps must fit | append two steps |

## Pattern Assignments

### `UtinniCoreDotNet.Tests/FrozenPluginComposeTests.cs` (test, MEF compose) — CPPS-04b

**Primary analog:** `UtinniCoreDotNet.Tests/PluginLoaderTests.cs` (EXACT)
**Reused harness:** `UtinniCoreDotNet/PluginFramework/PluginLoader.cs` → `Load(dir)` + `LoadErrors`

**Imports / namespace** (copy from `PluginLoaderTests.cs:25-31`):
```csharp
using System;
using System.IO;
using System.Linq;
using Xunit;
using UtinniCoreDotNet.PluginFramework;

namespace UtinniCoreDotNet.Tests
```

**Fixture-locate pattern** (copy & adapt from `PluginLoaderTests.cs:49-68` — `AppContext.BaseDirectory` + relative walk to `Fixtures/<Name>/...`). For the FROZEN fixture the DLL is committed directly (no `bin/Release/net472/` build-output subpath); point at `Fixtures/FrozenPlugin/<frozen>.dll`:
```csharp
private static string FindFrozenDll()
{
    var baseDir = AppContext.BaseDirectory;
    var candidate = Path.GetFullPath(Path.Combine(
        baseDir, "..", "..", "Fixtures", "FrozenPlugin", "<frozen>.dll"));
    if (File.Exists(candidate)) return candidate;
    // (keep the ../../../ fallback from the analog for x86-rerouted output paths)
    throw new FileNotFoundException("frozen plugin fixture missing near " + baseDir);
}
```

**Core compose-assertion pattern** (the RESEARCH-sketched assertion, grounded in the real `PluginLoader` API at `PluginLoader.cs:55-88` + `PluginLoaderTests.cs:88-92`):
```csharp
[Fact]
public void FrozenPlugin_ComposesAgainstCurrentBindings_NoLoadErrors()
{
    var frozen = FindFrozenDll();
    var tempDir = MakeTempDir();              // copy MakeTempDir from analog lines 70-75
    try
    {
        File.Copy(frozen, Path.Combine(tempDir, Path.GetFileName(frozen)));

        // autoLoad:false skips LoadFromPluginManager's native P/Invoke into
        // UtinniCore.dll (not deployed beside the test runner) — analog lines 136-138.
        var loader = new PluginLoader(autoLoad: false);
        loader.Load(tempDir);

        // A regen that removed/re-signatured a member the frozen plugin calls lands a
        // MissingMethod/Composition/ReflectionTypeLoad failure in LoadErrors (PluginLoader.cs:159-178).
        Assert.True(loader.LoadErrors.Count == 0,
            "frozen plugin failed to compose — a regen likely broke the binding ABI:\n"
            + string.Join("\n", loader.LoadErrors));
        Assert.NotEmpty(loader.Plugins);
    }
    finally { TryDeleteDir(tempDir); }        // copy TryDeleteDir from analog lines 151-156
}
```

**Cleanup pattern:** copy `MakeTempDir` (`PluginLoaderTests.cs:70-75`) and `TryDeleteDir` (`151-156`) verbatim — DirectoryCatalog locks the DLL until process exit, so cleanup is best-effort (`catch (IOException)` / `catch (UnauthorizedAccessException)`).

**DELIBERATE inversion vs analog:** `BrokenPlugin`/`GoodPlugin` are *rebuilt* each CI run (their own csprojs). The frozen fixture must be a **committed pre-built binary** — see Fixture pattern below. A rebuilt "frozen" plugin always matches the current surface → dead gate (RESEARCH Pitfall 6).

---

### `UtinniCoreDotNet.Tests/Fixtures/FrozenPlugin/<frozen>.dll` (committed binary fixture) — CPPS-04b

**Analog:** `Fixtures/{BrokenPlugin,GoodPlugin}` dirs (structure) — but the build wiring is the INVERSE.
**Source DLL:** freeze The Jawa Toolbox host plugin (CONTEXT D-02: 778 binding references across 72 files = broadest consumer). NOT Sytner (D-02a: `SytnersUtinniPlugin/sup.h` is a 27-line C++ header, no `.cs`, no DLL).

**Content-copy wiring** — do NOT give this fixture a csproj that rebuilds. Commit the `.dll` and mark it copy-to-output. Use the `<Content ... CopyToOutputDirectory="PreserveNewest" />` idiom already in `UtinniCoreDotNet.Tests.csproj:42-46` (the live wire-vector fixtures), NOT the rebuild-from-csproj pattern that `Fixtures/BrokenPlugin` uses. The `DefaultItemExcludes` guard at `csproj:25` (`Fixtures\**`) already keeps `Fixtures/` source out of the Tests compile — confirm the committed DLL is referenced as `Content`, not `Compile`.

---

### `UtinniCoreDotNet.Tests/AbiSurfaceTests.cs` (test, hash-set diff) — CPPS-04a

**Primary analogs:** `HeaderDiscoveryTests.cs` (real-file walk-up + pure static helper assertion) + `LiveBridgeProtocolTests.cs` (committed-golden byte-vector compare).

**Locate the freshly-regenerated `Generated/UtinniCore.cs`** — copy the 4-level walk-up idiom from `HeaderDiscoveryTests.cs:50-67` (`FindUtinniCoreRoot`), retargeted to `UtinniCoreDotNet/Generated/UtinniCore.cs`:
```csharp
private static string FindGeneratedFile()
{
    var baseDir = AppContext.BaseDirectory;
    var candidate = Path.GetFullPath(Path.Combine(
        baseDir, "..", "..", "..", "..", "UtinniCoreDotNet", "Generated", "UtinniCore.cs"));
    if (File.Exists(candidate)) return candidate;
    // keep the +1-level fallback from the analog (lines 59-64)
    throw new FileNotFoundException("Generated/UtinniCore.cs not found from " + baseDir);
}
```

**Locate the committed baseline** — copy the `Fixtures/` resolution idiom (`PluginLoaderTests.cs:49-68`) for the baseline block-hash text file (co-locate with the diff tool per CONTEXT discretion).

**Assertion shape** — mirror the golden-vector compare in `LiveBridgeProtocolTests.cs` (read committed golden, compare to produced), but compare HASH SETS not bytes:
```csharp
[Fact]
public void GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn()
{
    var current  = AbiBlockHash.Extract(FindGeneratedFile());   // HashSet<string>
    var baseline = AbiBlockHash.LoadBaseline(FindBaseline());   // HashSet<string>
    var added   = current.Except(baseline).ToList();
    var removed = baseline.Except(current).ToList();
    Assert.True(added.Count == 0 && removed.Count == 0,
        "ABI surface drifted (re-bless with --rebless if intentional):\n"
        + "ADDED:\n  " + string.Join("\n  ", added) + "\n"
        + "REMOVED:\n  " + string.Join("\n  ", removed));
}
```

---

### ABI block-hash diff tool (utility, BCL-only) — CPPS-04a

**Analogs:** `UtinniCoreDotNetGen/HeaderDiscovery.Discover` (pure static `string → List<string>` file processor) + `PluginInspection.RawBytesHasPluginAttributeString` (`PluginInspection.cs:604-634` — BCL byte/string scan, no dep).

**Block structure to extract** — VERIFIED from the head of `Generated/UtinniCore.cs` (lines 1-73). The file is nested namespaces → `public unsafe partial class <Name>` → `public partial struct __Internal` carrying the P/Invoke anchors. The native-ABI anchor is the mangled `EntryPoint` string:
```csharp
// Generated/UtinniCore.cs:55-58 (verified) — the ABI-load-bearing line to hash:
[DllImport("UtinniCore", CallingConvention = ... .ThisCall,
    EntryPoint="??0UtINI@utinni@@QAE@XZ")]
internal static extern global::System.IntPtr ctor(global::System.IntPtr __instance);
```
Key each block by public-surface identity (CONTEXT "Established Patterns"): namespace+class FQN; public method/property signatures; every `EntryPoint="<mangled>"` string (a mangled-name change IS an ABI change); enum members / `[FieldOffset(N)]` layout. EXCLUDE the `<auto-generated>` banner (lines 1-6) and any ordering-only artifact — the head confirms there is **no CppSharp version line** to key off (RESEARCH-verified; `regen-bindings.md` is STALE claiming one exists).

**Hashing** — BCL `System.Security.Cryptography.SHA256` over each normalized block → `HashSet<string>` (CONTEXT discretion: BCL-only default; escalate to Roslyn ONLY if line-extraction proves brittle, tooling project only).

**`--rebless` mode** (CONTEXT D-01): regenerate the baseline file AND print the lockstep checklist (rebuild TJT, re-freeze the fixture, commit all artifacts together). Mirror the env-var/arg resolver discipline of `SlnDirResolver.Resolve` (tested in `CppSharpSlnDirTests.cs`) for any path inputs.

---

### `ci.yml` — two new verify-only steps — CPPS-03a / CPPS-03b

**Analogs (all in `ci.yml`):** the clang-format style gate (`44-68`), "Verify v145 build tools" (`141-154`), "Verify VS 2019 BuildTools (MSVC 14.29)" (`166-185`). All three are PowerShell-5.1, vswhere-resolved, `throw`-on-failure, verify-only — the exact shape the new steps must copy.

**CPPS-03a — C++23-header scan (HARD-FAIL, CONTEXT D-04):** copy the include/exclude-set + `Get-ChildItem -Recurse | Where-Object { $_.FullName -notmatch $excludePattern }` idiom from the clang-format step (`ci.yml:56-67`). Scope the scan to `#include` lines under `UtinniCore/` ONLY; exclude `external/`, `Generated`, docs (RESEARCH Pitfall 4 + CONTEXT grep-gate hygiene — the scan must not flag its own allowlist/comments). `throw` on a disallowed header → non-zero exit fails the job (same as the style gate at `65-67`):
```powershell
# fit ci.yml's PS-5.1 verify-only model; throw == hard-fail (mirror lines 65-67 / 151-153)
if ($disallowed.Count -gt 0) { throw "UtinniCore adopted a 14.29-unparseable C++23 STL header: $disallowed" }
```

**CPPS-03b — clang-20 pin tripwire (WARN-loud, NOT block, CONTEXT D-03/D-04):** copy the structure of the 14.29-verify step (`ci.yml:166-185`) but assert a **committed pin** (`CppSharp v1.2 / clang 19`) rather than probing the network (D-03: no self-hosted egress dependency; deterministic). A newer-than-pin observation is GOOD NEWS (native-v145 reachable) → emit `Write-Host "::warning::..."` (idiom already used at `ci.yml:132`), do NOT `throw` — it must not turn master red:
```powershell
# WARN, never throw — a clang>=20 release UNBLOCKS retiring the redirect (D-04).
Write-Host "::warning::CppSharp release newer than pinned clang-19 baseline — native v145 may now be reachable; review retiring the 14.29 redirect."
```

**Placement:** slot both beside the two existing redirect-verify steps (after `ci.yml:185`, before "Setup MSBuild"). Do NOT add a `pull_request` trigger (locked RCE invariant — `ci.yml:10-15`).

---

### CPPS-01 spike script (utility) — the FIRST task

**Analog:** the vswhere-resolve verify steps (`ci.yml:141-185`) — PowerShell 5.1, enumerate installed MSVC dirs.
**Mechanism (CONTEXT discretion):** for each installed MSVC `yvals_core.h`, grep the `#if __clang_major__ < N` line; tabulate N vs CppSharp's bundled clang (11 vendored, 19 latest released). Mechanical conclusion: 11 < 20 and 19 < 20 → no released CppSharp parses v145 → documented negative result RE-SETS acceptance to harden-the-redirect (RESEARCH Pitfall 1; CONTEXT specifics). Evidence lines (verified, RESEARCH):
- v145 14.51/14.52: `__clang_major__ < 20`
- VS2022 14.44: `< 19`
- VS2019 14.29: `< 11` ← the redirect target

---

### `docs/ai/regen-bindings.md` (UPDATE) — CPPS-02

Fix the two STALE facts (both verified):
- **Line 147:** "~5000+ lines" → actual **27,659**.
- **Lines 160-161 + 200-201:** the claim of "a CppSharp version line" in the banner. VERIFIED FALSE from `Generated/UtinniCore.cs:1-6` — the `<auto-generated>` banner carries NO version line. The ABI diff must key off structural surface, NOT a version string. Update the "Diff the file" guidance (lines 199-205) to point at the new per-block-hash gate + the `--rebless` procedure (D-01).

Add the re-bless procedure (D-01 / RESEARCH "Runtime State Inventory"): on an intentional surface change — (a) rebuild TJT in lockstep, (b) regen the baseline block-hash file via `--rebless`, (c) rebuild & re-freeze the plugin fixture, (d) commit all three together.

---

### supported-config doc / section (NEW) — CPPS-02

Lift the redirect rationale OUT of `Program.cs` comments (`Program.cs:64-73`, `164-229`) into discoverable in-repo prose: the spike result, why the 14.29 redirect is load-bearing, the ABI-stability assumption (`_MSVC_STL_VERSION = 143` across 14.29→14.52), and the documented prerequisites (VS2019 14.29, VS2026 v145, Win10 SDK 19041, `UTINNI_VS2019_ROOT`/`UTINNI_SLN_DIR` env overrides). Prose style = `regen-bindings.md`. Stop the config being "silently load-bearing" (CONTEXT CPPS-02).

## Shared Patterns

### MEF compose harness (reuse, do NOT hand-roll)
**Source:** `UtinniCoreDotNet/PluginFramework/PluginLoader.cs` — `Load(string pluginDir=null)` (lines 72-88), `LoadErrors` (line 45), `autoLoad:false` ctor (lines 55-61), per-DLL isolated `DirectoryCatalog` + `ComposeParts` with `ReflectionTypeLoadException`/`Exception` capture (lines 143-178).
**Apply to:** `FrozenPluginComposeTests.cs`. `Assert.Empty(loader.LoadErrors)` + `Assert.NotEmpty(loader.Plugins)` is the whole gate.

### Fixture-locate via AppContext.BaseDirectory walk-up
**Source:** `PluginLoaderTests.cs:49-68` (`FindFixtureDll`) + `HeaderDiscoveryTests.cs:50-67` (`FindUtinniCoreRoot`) — both walk up from `AppContext.BaseDirectory` with a `+1-level` fallback for x86-rerouted output.
**Apply to:** `AbiSurfaceTests.cs` (find `Generated/UtinniCore.cs` + the baseline) and `FrozenPluginComposeTests.cs` (find the frozen DLL).

### Committed-golden compare idiom
**Source:** `LiveBridgeProtocolTests.cs:52-55` (`ReadFixtureBytes`) + the byte-exact `Assert` against `wire-*.json` goldens; csproj wiring `UtinniCoreDotNet.Tests.csproj:42-46` (`<Content ... CopyToOutputDirectory="PreserveNewest" Link="..."/>`).
**Apply to:** the ABI baseline block-hash file and the frozen-plugin binary fixture (both committed, content-copied — NOT rebuilt).

### Verify-only CI step idiom (PowerShell 5.1, vswhere, throw-on-fail)
**Source:** `ci.yml` clang-format gate (44-68), v145 verify (141-154), 14.29 verify (166-185); `::warning::` emit (132).
**Apply to:** both new tripwire steps + (optionally) the spike. HARD-FAIL = `throw`; WARN = `Write-Host "::warning::..."`. Push-only, no `pull_request` trigger (locked invariant, `ci.yml:10-15`).

### BCL pure-function file processor (no new dep)
**Source:** `UtinniCoreDotNetGen/HeaderDiscovery.Discover` (static `string → List<string>`, tested by `HeaderDiscoveryTests.cs`) + `PluginInspection.RawBytesHasPluginAttributeString` (`PluginInspection.cs:604-634`, BCL byte scan).
**Apply to:** the ABI block-hash tool (`SHA256` + line/regex extraction; BCL-only default).

### Env/arg resolver discipline (don't regress to hard-coded paths)
**Source:** `Program.cs` resolvers (`ResolveVs2019Root` env→vswhere→default-probe, `ResolveLatestWindowsSdkInclude`) tested via `SlnDirResolver.Resolve` pattern in `CppSharpSlnDirTests.cs`.
**Apply to:** any path input in the diff tool / `--rebless` / spike — use env-var + probe, never a literal install path (RESEARCH Pitfall 3; CONTEXT "reuse, don't regress").

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `allowed-cpp-stl-headers.txt` (or inline allowlist) | config/data | stored data | First header-allowlist artifact in the repo; no existing analog. Keep example header names OUT of the scanned source/comments (grep-gate self-trip — RESEARCH Pitfall 4). |
| supported-config doc (CPPS-02 NEW doc/section) | doc | n/a | New doc; nearest *style* reference is `regen-bindings.md`, but no structural analog. |

## Metadata

**Analog search scope:** `UtinniCoreDotNet.Tests/` (xUnit lanes), `UtinniCoreDotNet/PluginFramework/`, `Utinni.Cli/Commands/`, `UtinniCoreDotNetGen/`, `.github/workflows/`, `docs/ai/`, `UtinniCoreDotNet/Generated/`.
**Files scanned:** ~12 read in full/targeted (PluginLoaderTests, PluginLoader, HeaderDiscoveryTests, CppSharpSlnDirTests, LiveBridgeProtocolTests head, PluginInspection, ValidatePluginCommand head, Program.cs redirect section, regen-bindings.md, ci.yml, Generated/UtinniCore.cs head, Tests csproj).
**Pattern extraction date:** 2026-06-14
**Skills dirs:** none present (`.claude/skills`, `.agents/skills` absent).
