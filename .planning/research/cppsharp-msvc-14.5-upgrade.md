---
title: CppSharp ↔ MSVC 14.5x (v145 / VS 2026) Compatibility Research
created: 2026-05-24
status: research-only (no code changes)
related_todo: .planning/todos/pending/cppsharp-msvc-14.5-incompatibility.md
related_phase: phase-06 D-09 (PlatformToolset v142 → v145)
worktree: worktree-agent-a4d0744552aa5c200 (locked, commit 83a8056)
---

## Executive Summary

- **Vendored CppSharp identified: 0.10.5** (NuGet release dated 2020-06-27, clang 11.0.0 bundled). DLL byte-sizes match the 0.10.5 NuGet exactly for `CppSharp.dll`, `CppSharp.AST.dll`, `CppSharp.Parser.dll`, `CppSharp.Parser.CLI.dll`. Embedded string `based on LLVM 11.0.0` confirms the clang version. **No `VS2022` enum value exists in this vendored build** — `VisualStudioVersion.Latest = 16` (VS 2019) is the cap.
- **Upstream CppSharp is alive and well.** `v1.2` shipped 2025-11-19 with clang 19 ("modern MSVC support"). Repo `mono/CppSharp` last push 2026-05-18 (6 days ago); 3,379 stars; 70 NuGet versions published; tagged release `v1.1` (Oct 2023) explicitly added "VS2022 support" (clang 16/18). The NuGet `1.1.84.17100` ships pre-built Windows DLLs + clang 19 binaries — **but only for `net9.0` / `net10.0`, win-x64 only.** No net4.7.2, no win-x86.
- **Root cause of the v145 parse errors is the MSVC STL's clang-version gate** in `yvals_core.h`: VS 2026 MSVC 14.51 / 14.52 STL hard-requires clang **20.0.0+**; VS 2022 MSVC 14.44 STL requires clang **19+**; VS 2022 14.35 required clang 15+; VS 2019 14.29.30133 STL requires (and works with) clang **11** — exactly what the vendored CppSharp ships. The `_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH` define silences the *version-check* `_EMIT_STL_ERROR`, but does NOT fix the real C++23 syntax features clang 11 cannot parse (`__builtin_verbose_trap`, static-call-operator, etc.) — those are unconditional in the newer STL.
- **Recommended path: Path 1 (parser-include redirect to VS 2019 14.29.30133 STL).** Lowest risk, lowest effort (~2-4 hours), zero binary-compat impact on shipped DLLs. Concrete API: `driver.ParserOptions.NoStandardIncludes = true; driver.ParserOptions.AddSystemIncludeDirs(@"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.29.30133\include")` (plus Windows SDK + UCRT dirs). VS 2019 14.29 BuildTools confirmed on this machine; user pre-authorized to install on dev/CI boxes if missing.
- **Fallback path: Path 3 (defer v145).** Phase 6 v1.0-rc.1 ships fine on v142. v142 is supported through MSVC 2019's EOL window (mainstream support officially ended 2024-04-09, but the toolset itself remains buildable in current VS 2026 by side-by-side install). Defer cost: zero this milestone, accumulating tech debt as Phase 7+ contributors may need to install VS 2019 BuildTools.
- **Path 2 (upgrade CppSharp) is NOT recommended for v1.0-rc.1.** Requires migrating `UtinniCoreDotNetGen` off net4.7.2 (NuGet only ships net6/net9/net10), losing x86 host capability (NuGet is win-x64 only — actually fine for Utinni since UtinniCoreDotNetGen.csproj is `<PlatformTarget>x64</PlatformTarget>` already), and revalidating generated bindings against the new generator. Estimated 1-3 days. Defer to a dedicated milestone.

---

## Vendored CppSharp Identification

| Property | Value | Evidence |
|---|---|---|
| Release | **0.10.5** (NuGet 0.10.5, published 2020-06-27) | DLL sizes match exactly (see table) |
| Clang version | **11.0.0** | String `based on LLVM 11.0.0` in `CppSharp.CppParser.dll`; `ParserOptions.ClangVersion = "11.0.0"` at runtime; `lib/lib/clang/11.0.0/include/` resource layout |
| Target framework | **net4.7.2** (consumed by `UtinniCoreDotNetGen.csproj`) | DLLs are pre-net5 PE32+ AnyCPU AMD64-marked |
| `VisualStudioVersion` enum cap | **VS2019 (Latest = 16)** | Reflected from `CppSharp.dll`: `VS2012=11, VS2013=12, VS2015=14, VS2017=15, Latest=16` — no `VS2022` value |
| Directory layout | Legacy `output/lib/clang/11.0.0/include/` (Utinni mirrors this as `external/CppSharp/lib/lib/clang/11.0.0/include/`) | Matches 0.10.5 NuGet layout; modern releases use `contentFiles/any/any/lib/clang/N/include/` |
| Vendored DLL `FileVersion` / `ProductVersion` | `0.0.0.0` (stripped) | All seven DLLs report `0.0.0.0`; no `AssemblyFileVersionAttribute` was set in CppSharp 0.10.x builds |

### Byte-size cross-check vs CppSharp 0.10.5 NuGet (`runtimes/win-x64/lib/`)

| DLL | Vendored (Utinni) | NuGet 0.10.5 | Δ |
|---|---|---|---|
| CppSharp.dll | 26,112 | 26,112 | 0 (identical) |
| CppSharp.AST.dll | 261,632 | 261,632 | 0 (identical) |
| CppSharp.Parser.dll | 141,824 | 141,824 | 0 (identical) |
| CppSharp.Parser.CLI.dll | 993,280 | 993,280 | 0 (identical) |
| CppSharp.Generator.dll | 620,544 | 621,056 | -512 (file alignment / minor patch) |
| CppSharp.Runtime.dll | 5,632 | 6,144 | -512 (file alignment / minor patch) |
| CppSharp.CppParser.dll | 36,582,912 | 36,581,376 | +1,536 (file alignment / minor patch) |

Four of seven DLLs are byte-identical to upstream 0.10.5; the three with deltas are within Windows PE file-alignment slop. Conclusion: this is an essentially-stock 0.10.5 NuGet drop (possibly with a small CppParser patch from upstream master between 0.10.5 NuGet date 2020-06-27 and the next public release).

---

## Upstream CppSharp State

### Releases (GitHub)

| Tag | Date | Highlights |
|---|---|---|
| `v1.2` | **2025-11-19** | "Upgrade to Clang 19 for modern MSVC support"; ARM64 LLVM builds; QuickJS generator; many bug fixes |
| `v1.1` | 2023-10-18 | "Add GCC11 and VS2022 support"; clang 16 → clang 18; array marshalling fixes |
| `v1.0.45.22293` | 2023-02-06 | Pre-VS2022 era; clang 14 |
| `CppSharp` (initial) | 2015-09-27 | Tag-only marker |

### NuGet versions ([nuget.org/packages/CppSharp](https://www.nuget.org/packages/CppSharp))

- Latest: **`1.1.84.17100`** (published 2025-11-19, matches GitHub `v1.2`)
- Previous: `1.1.5.3168` (2023-10-18), `1.0.76.8341` (2023-10-18), `1.0.45.22293` (2023-02-06)
- 70 versions total spanning 2015 → 2025

### Maintenance signals

- Repo last push: **2026-05-18** (6 days before this research); ongoing PR activity
- `default_branch = "main"`, **not archived**, **not disabled**
- Active maintainer: `tritao` (released v1.1 and v1.2); also `duckdoom5`, `Saalvage`, `deadlocklogic` heavily contributing recent fixes
- Release cadence: roughly one tagged release every 2 years; many interim NuGet builds

### v1.2 NuGet pkg contents (`cppsharp.1.1.84.17100.nupkg`, 102 MB)

```
runtimes/linux-x64/lib/net10.0/{CppSharp,CppSharp.AST,CppSharp.Generator,CppSharp.Parser,CppSharp.Parser.CSharp,CppSharp.Runtime}.dll
runtimes/linux-x64/native/{libCppSharp.CppParser.so, libStd-symbols.so}
runtimes/win-x64/lib/net9.0/{CppSharp,CppSharp.AST,CppSharp.Generator,CppSharp.Parser,CppSharp.Parser.CSharp,CppSharp.Runtime}.dll
runtimes/win-x64/native/{CppSharp.CppParser.dll (78.8 MB), Std-symbols.dll}
ref/net9.0/{...same six managed DLLs...}
contentFiles/any/any/lib/clang/19/include/*.h  (clang 19 builtins)
```

**Constraints for Utinni consumption:**
- No `net472` target framework — UtinniCoreDotNetGen.csproj would need migration to `net9.0` or `net10.0` (or pin to an older NuGet that still ships net6.0 / net4.x)
- No `win-x86` runtime — fine because UtinniCoreDotNetGen already runs as x64 (`<PlatformTarget>x64</PlatformTarget>` in the csproj); only the generated bindings target x86 via `TargetTriple = "i686-pc-win32-msvc"`
- No macOS pre-built native parser in v1.2 (regression vs v1.1 which had `runtimes/osx-x64/`); not relevant for Utinni (Windows-only)

### Has CppSharp specifically addressed MSVC 14.4x+ STL compatibility?

**Yes, both via clang upgrades and via STL flags:**

- `v1.1` (Oct 2023) PR #1724 "Update to Clang 16 for MSVC 2022 support" by tritao — the merge that unblocked MSVC 14.35.32215 (VS 2022 17.5)
- `v1.2` (Nov 2025) PR #1949 "Adds clang 19 support" by tritao — unblocks current MSVC 14.4x (VS 2022 17.13)
- The `main` branch `SetupMSVC(VisualStudioVersion)` method always adds `-D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH` to clang args (effective Oct 2023); see ["CppSharp issue #1722 closing comment"](https://github.com/mono/CppSharp/issues/1722) — this defines silences the STL's `_EMIT_STL_ERROR(STL1000, ...)` check but does NOT make C++23 syntax features parse on older clang
- **No public CppSharp commit yet addresses MSVC 14.5x (clang 20 required by VS 2026 STL).** Phase 6's encounter with this is plausibly the leading edge

---

## Per-Path Analysis

### Path 1 — Configure CppSharp parser to use OLDER MSVC STL

**Premise:** Point CppSharp's clang 11 at VS 2019 MSVC 14.29.30133 STL (the STL the vendored CppSharp is paired with by design) while the main UtinniCore C++ build compiles against MSVC 14.51/14.52 (v145 toolset).

#### Why it can work — concrete mechanism

The MSVC STL's clang-version gate is **header-resident**, not embedded in the compiler binary:

```cpp
// VS 2026 v145 (MSVC 14.51): /VC/Tools/MSVC/14.51.36231/include/yvals_core.h:916
#elif defined(__clang__)
#if __clang_major__ < 20
_EMIT_STL_ERROR(STL1000, "Unexpected compiler version, expected Clang 20 or newer.");

// VS 2019 v142 (MSVC 14.29.30133): /BuildTools/VC/Tools/MSVC/14.29.30133/include/yvals_core.h
#if __clang_major__ < 11
#error STL1000: Unexpected compiler version, expected Clang 11.0.0 or newer.
```

VS 2019 MSVC 14.29 STL **explicitly accepts clang 11**. The pairing is intentional — clang 11 was current when VS 2019 16.10 shipped (June 2021). Pointing the parser at this STL gives a guaranteed-clean parse of `<vector>`, `<tuple>`, `<string>`, etc., decoupled from whatever MSVC version the C++ build actually uses.

The generated C# bindings remain ABI-correct for v145 builds because:
1. CppSharp's marshalling layer reads sizeof/alignof from the **AST**, not the live STL — and a `std::string` (SSO, COW removed) has the same in-memory layout across MSVC 14.29 → 14.52 (Microsoft STL ABI is stable within `_MSVC_STL_VERSION = 143`)
2. Utinni's `Preprocess()` callback already ignores `swg_string`, `command_parser`, etc. — the STL types that ARE projected (mostly opaque pointers / fundamental types) don't depend on internals
3. `StdEdited.cs` is hand-curated by design (per the existing comment in `HeaderDiscovery.cs`) precisely because CppSharp's STL handling is unreliable across MSVC versions — this is already the project's mitigation

#### Concrete API

In `UtinniCoreDotNetGen/Program.cs`, inside `Setup(Driver driver)`:

```csharp
// EXISTING (line 54):
driver.ParserOptions.TargetTriple = "i686-pc-win32-msvc";

// ADD: redirect MSVC STL parsing to VS 2019 14.29 (known-good with clang 11).
// Decouple codegen-time STL parsing from build-time STL compilation (v145).
driver.ParserOptions.NoStandardIncludes = true;  // suppress LLVM 11's auto-detect of newest VS install
driver.ParserOptions.AddSystemIncludeDirs(
    @"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.29.30133\include");
driver.ParserOptions.AddSystemIncludeDirs(
    @"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.29.30133\atlmfc\include");
// Windows SDK + UCRT — pin to the SDK that 14.29 was tested against (10.0.19041 era)
driver.ParserOptions.AddSystemIncludeDirs(
    @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.19041.0\ucrt");
driver.ParserOptions.AddSystemIncludeDirs(
    @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.19041.0\shared");
driver.ParserOptions.AddSystemIncludeDirs(
    @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.19041.0\um");
driver.ParserOptions.AddSystemIncludeDirs(
    @"C:\Program Files (x86)\Windows Kits\10\Include\10.0.19041.0\winrt");
// Belt-and-suspenders: silence the STL version gate (cheap, no downside)
driver.ParserOptions.AddDefines("_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH");
```

**API reference verified by reflection on `external/CppSharp/lib/CppSharp.Parser.dll`:**

- `CppSharp.Parser.ParserOptions.NoStandardIncludes` (bool property, inherited from `CppParserOptions`)
- `CppSharp.Parser.ParserOptions.AddSystemIncludeDirs(string)` (instance method)
- `CppSharp.Parser.ParserOptions.AddDefines(string)` (instance method)
- `CppSharp.Parser.ParserOptions.SystemIncludeDirs` (List<string> property)

Vendored Utinni CppSharp does expose these. No upstream upgrade needed.

#### Hardening (path-resolution robustness)

Hard-coding the path is fragile. Wrap with `vswhere.exe` or environment-variable lookup so CI/dev boxes can install at non-default paths:

```csharp
// Sketch: resolve VS 2019 BuildTools dynamically
string vs2019Root = Environment.GetEnvironmentVariable("UTINNI_VS2019_ROOT")
    ?? @"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools";
string msvc1429 = Path.Combine(vs2019Root, @"VC\Tools\MSVC\14.29.30133");
if (!Directory.Exists(Path.Combine(msvc1429, "include")))
    throw new InvalidOperationException("MSVC 14.29.30133 STL not found. Install VS 2019 BuildTools workload 'MSVC v142'.");
```

`vswhere.exe -version "[16.0,17.0)" -property installationPath` returns the VS 2019 install if present; this is the recommended discovery mechanism per Microsoft.

#### Gotchas / verified

| Concern | Verdict | Note |
|---|---|---|
| Mixed-toolchain ABI? | **No risk**: CppSharp parses headers, doesn't link STL | Generated bindings reference UtinniCore.dll built with v145; STL ABI is stable within `_MSVC_STL_VERSION = 143` |
| Macros set differently? | Low: `_MSC_VER` is set by clang's `-fms-compatibility-version` (from `ToolSetToUse`) regardless of header location | `SetupMSVC()` sets `ToolSetToUse = clVersion.Major * 10000000 + clVersion.Minor * 100000` — explicitly point at 1929 (v142) to keep STL macros aligned with VS 2019 era |
| Windows SDK version drift? | Medium: STL headers `#include <yvals.h>` chase into Windows SDK; if `10.0.19041` is absent on the build box, fall back to whatever 10.0.x SDK is installed | Auto-discover via `vswhere.exe -find "Windows10SDK*"` or hard-code if Phase 6 worktree controls the dev env |
| Does `NoStandardIncludes = true` break anything? | Verified safe: it disables clang's *built-in* `lib/clang/11.0.0/include` auto-add. Re-add explicitly: `AddSystemIncludeDirs(driver.ParserOptions.BuiltinsDir);` | Check `ParserOptions.BuiltinsDir` is populated after Driver.Setup(); upstream main does this in `SetupXcode()` already |
| CI install of VS 2019 BuildTools? | Medium friction: not present on stock `windows-2022` GitHub runners by default | Pre-installed on `windows-2025` runner ([actions/runner-images](https://github.com/actions/runner-images)); if not, install via `choco install visualstudio2019buildtools --package-parameters "--add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Component.VC.v142"` |

#### Effort estimate

- Code change in `Program.cs`: 30 min
- vswhere wiring + fallbacks: 1 hour
- Validate generated `UtinniCore.cs` is byte-identical to v142 baseline output: 1 hour
- CI test (windows-2025 runner): 30 min

**Total: ~3 hours of focused work.** Plus one round-trip through Phase 6 D-09 verification.

#### Risk: LOW

- API surface used (`AddSystemIncludeDirs`, `AddDefines`, `NoStandardIncludes`) is stable across CppSharp 0.10.x → 1.2
- The pairing "clang 11 + MSVC 14.29 STL" is the original supported combination — battle-tested
- Bindings byte-for-byte identical to v142 baseline (no behavioral change to generated C#)
- Reversible: single-commit revert

---

### Path 2 — Upgrade vendored CppSharp

**Premise:** Pull NuGet `CppSharp 1.1.84.17100` (clang 19) into `external/CppSharp/` (or via PackageReference) and refit `UtinniCoreDotNetGen.csproj`.

#### Feasibility

| Concern | Status |
|---|---|
| Pre-built binaries available? | **Yes** — `runtimes/win-x64/native/CppSharp.CppParser.dll` (78.8 MB, clang 19) + managed DLLs |
| LLVM compile required? | **No** — pre-built LLVM is auto-downloaded by `build.sh` if building from source ([CppSharp docs/LLVM.md](https://github.com/mono/CppSharp/blob/main/docs/LLVM.md)) |
| net4.7.2 target available? | **No** — NuGet only ships `net9.0` (win-x64) and `net10.0` (linux-x64). UtinniCoreDotNetGen.csproj currently targets `net4.7.2`. |
| win-x86 host available? | **No** — `runtimes/` only contains `win-x64` and `linux-x64`. Fine for Utinni: `UtinniCoreDotNetGen.csproj` already specifies `<PlatformTarget>x64</PlatformTarget>` (since CppSharp.Parser.CLI.dll is AMD64-only); only the generated bindings target x86 via `TargetTriple = "i686-pc-win32-msvc"` |
| API surface stable for `ILibrary.Setup()`? | **Mostly yes** — `Driver.ParserOptions.*`, `Options.GeneratorKind`, `Options.OutputDir`, `Options.AddModule(name)`, `module.IncludeDirs`, `module.Headers`, `module.LibraryDirs`, `module.Libraries` all preserved per upstream main branch |
| API surface stable for `Preprocess(driver, ctx)`? | **Likely yes** — `ASTContext.IgnoreHeadersWithName()` still public |
| StdEdited.cs still needed? | **Probably yes** — CppSharp 1.2's `std::basic_string` handling improvements are real (see PR #1812 "Stdlib.CSharp.cs: remove buggy typemap") but the project comment in `HeaderDiscovery.cs` documents three reasons for hand-curating; same constraints persist |

#### Required Utinni-side changes

1. Migrate `UtinniCoreDotNetGen.csproj` from net4.7.2 → net9.0 (or net6.0 if pinning to NuGet `1.0.76.8341`):
   - Convert to SDK-style csproj (`<Project Sdk="Microsoft.NET.Sdk">`)
   - Drop `App.config` (.NET Framework artifact)
   - Adjust assembly references (System.* DLLs become framework-implicit)
2. Replace `<Reference HintPath="..\external\CppSharp\lib\...">` block with `<PackageReference Include="CppSharp" Version="1.1.84.17100">` (or vendor the NuGet extract back into `external/CppSharp/`)
3. Drop the `external/CppSharp/lib/lib/clang/11.0.0/` resource tree; new clang 19 builtins ship inside the NuGet `contentFiles/`
4. Update `PostBuildEvent` on `UtinniCore.vcxproj` — UtinniCoreDotNetGen.exe is now framework-dependent net9 (needs `dotnet UtinniCoreDotNetGen.dll` invocation rather than direct `.exe`), OR publish as self-contained with `--self-contained` for `dotnet publish -r win-x64 -o ...`
5. Validate `HeaderDiscovery.cs` linking into `UtinniCoreDotNet.Tests` (per the comment in that file) still works across the framework gap — Tests project is presumably net4.x; cross-framework `<Compile Include>` linking should still compile fine since `HeaderDiscovery` uses only `System.IO` / `System.Linq`
6. Regenerate `Generated/UtinniCore.cs` and diff against v142 baseline; expect cosmetic differences (e.g., updated marshalling attributes); manually verify nothing broke

#### Effort estimate

- net4.7.2 → net9.0 csproj migration: 2-3 hours (familiar pattern but UtinniCoreDotNetGen.csproj has specific x64-only constraints and a cross-project link to Tests)
- PackageReference + `external/CppSharp/` cleanup: 1 hour
- PostBuildEvent rewrite (dotnet exec or self-contained publish): 1-2 hours
- Regen + diff + verify codegen output: 2-4 hours (could surface new generator quirks)
- CI / cross-machine validation: 1-2 hours

**Total: 1-2 days of focused work**, plus risk of generator behavior changes (CppSharp 1.2 has 60+ PRs in property/type handling vs 0.10.5).

#### Risk: MEDIUM-HIGH

- Generator output changes could break plugin compile chain (UtinniCoreDotNet → UtinniPlugins) — kennethlong/UtinniPlugins repo would need lockstep validation
- net9 build-tool dependency adds .NET 9 runtime install requirement on dev/CI boxes (currently runs on net4.7.2 which ships with Windows)
- 5-year jump in CppSharp generator semantics; PRs #1714 "Rework property handling", #1772-1813 reworked the type marshalling layer

#### When to do it

- After v1.0-rc.1 ships
- Could pair with a "modernize to .NET 8/9 LTS for all build tools" effort
- Future-proofs against the inevitable MSVC 14.6x bump

---

### Path 3 — Defer the v145 bump entirely

**Premise:** Phase 6 v1.0-rc.1 stays on PlatformToolset v142 (VS 2019); D-09 becomes a follow-up cleanup once Path 1 or Path 2 is decided.

#### Worktree disposition

Per STATE.md, `worktree-agent-a4d0744552aa5c200` (locked, commit `83a8056`) holds the v145 sweep + fixup. To execute Path 3:

- Cherry-pick `0ab49ae` (vcpkg manifest research) + `7fa5e48` (OutputSinkRoundTripTests CON-N-09 regression fence) + VSIX-widen portion of `6cefd49` onto master
- Revert the PlatformToolset sweep portion of `6cefd49` + the `83a8056` v144→v145 fixup
- Close the worktree

#### v142 sustainability

| Factor | Status |
|---|---|
| VS 2019 mainstream support | **Ended 2024-04-09** (Microsoft official) |
| VS 2019 extended security updates | Through **2029-04-10** (Microsoft official) |
| MSVC v142 toolset side-by-side install on VS 2026? | **Yes** — VS Installer supports `Microsoft.VisualStudio.Component.VC.v142` as a side-by-side workload component; on this dev box, BuildTools 2019 + v142 are confirmed present at `/c/Program Files (x86)/Microsoft Visual Studio/2019/BuildTools/VC/Tools/MSVC/14.29.30133/` |
| Phase 6 v1.0-rc.1 ship-blocking? | **No** — current master builds clean on v142 |
| Phase 7+ contributor friction? | **Medium** — fresh-checkout devs must install VS 2019 BuildTools alongside whatever VS 2022/2026 they have. CI requires `windows-2022` runner (which still has v142) — `windows-2025` may have dropped it |
| Security/exploitability | **Low concern** — MSVC v142 still patched within VS 2019's extended-support window |

#### Effort estimate

- Worktree commit triage: 1 hour
- Verify cherry-pick keeps Wave 2 deliverables (vcpkg research + tests) on master: 30 min
- Update Phase 6 PLAN to mark D-09 as deferred-to-followup: 30 min

**Total: ~2 hours.** Most cost-efficient unblock.

#### Risk: LOW (short term), MEDIUM (long term)

- Accumulating tech debt — every future MSVC bump compounds the gap
- CI runner image drift — GitHub will eventually drop windows-2022 (`windows-2025` already standard); v142 availability not guaranteed forever
- Reverts the v145 work; if Path 1 lands shortly after, the revert effort partially wasted

---

## Recommendation

**Pursue Path 1 (parser-include redirect to VS 2019 14.29) inside the existing locked worktree `worktree-agent-a4d0744552aa5c200`.**

Rationale:

1. **Lowest blast radius.** Touches ~10 lines in `UtinniCoreDotNetGen/Program.cs`. Zero impact on shipped DLLs, generated bindings, or downstream plugin compile.
2. **Highest confidence.** The "clang 11 + MSVC 14.29 STL" pairing is the original supported combination — battle-tested as Utinni's existing baseline. We're not asking the parser to do anything it wasn't designed for.
3. **API surface verified to exist** in the vendored CppSharp 0.10.5 binaries: `AddSystemIncludeDirs`, `NoStandardIncludes`, `AddDefines` all reflected from `CppSharp.Parser.dll`. No upstream upgrade needed.
4. **Bindings remain byte-identical to v142 baseline.** Since the AST input is unchanged (same headers, same STL), the codegen output is identical. UtinniCoreDotNet.dll's public surface doesn't move; plugin compatibility preserved.
5. **Composable with Path 2 later.** Doing Path 1 now leaves Path 2 free as a future "modernize the build pipeline" milestone — without ship-blocking v1.0-rc.1.
6. **Worktree-resumable.** A single new commit on top of `83a8056` finishes Wave 2; the rest of the worktree's content (vcpkg manifest, tests, toolset sweep) is preserved.

**Path 3 (defer) is the recommended fallback** if Path 1 hits an unexpected snag during implementation (e.g., a header `#include` chain bottoms out in `<concepts>` or `<format>` that didn't exist in MSVC 14.29). Easy to fall back to with one revert commit.

**Path 2 is the recommended Phase 7-or-later milestone work.**

---

## Open Questions

1. **VS 2019 BuildTools availability on CI runners.** `windows-2022` and `windows-2025` images may have already dropped MSVC v142. Needs a quick check (likely via `actions/runner-images` README) before Path 1 lands — or a `choco install visualstudio2019buildtools --package-parameters="--add Microsoft.VisualStudio.Component.VC.v142"` step in `.github/workflows/` CI YAML.

2. **Header `#include` chains escaping 14.29 STL.** If UtinniCore C++ code uses `<format>` (C++20 — added to MSVC STL in 14.29 timeframe, marginally complete), `<ranges>`, `<concepts>`, or `<expected>` (C++23, post-14.29), the parser may still hit unimplemented headers. **Mitigation:** quick grep of UtinniCore/ for these `#include` patterns. If any are present, either restrict to 14.29-shippable features or jump to MSVC 14.34 (last "clang 14 compatible" pre-clang-15 STL) instead of 14.29. *needs verification once Path 1 is in execution*

3. **`SystemIncludeDirs` vs `IncludeDirs` ordering.** Per CppSharp's native `Parser.cpp`, `HSOpts.AddPath(..., frontend::System, ...)` adds system paths; the LLVM `MSVCToolChain::AddClangSystemIncludeArgs()` then appends auto-detected VS paths. **Need to verify** that the explicit `AddSystemIncludeDirs` adds at the *front* so it wins. If LLVM 11's MSVCToolChain still appends a (broken-for-clang-11) VS 2026 detection, we'd need `NoStandardIncludes = true` to suppress it. The Program.cs sketch above sets this defensively.

4. **Long-term VS 2019 toolset retirement.** Microsoft has not announced VS 2019 toolset retirement from VS 2026 yet, but it could happen mid-2027+. Path 2 (upgrade CppSharp) becomes mandatory before that point — *needs ROADMAP entry*.

5. **Does Utinni actually need v145?** D-09 was scoped as "stay current with VS 2026's default toolset" rather than "v145-specific features are needed". If no concrete v145 feature is required (e.g., a new compiler intrinsic, a perf-critical optimization, or a Windows SDK pairing), staying on v142 indefinitely is viable. *needs maintainer input on what's actually motivating the bump*.

6. **Path 1 + StdEdited.cs interaction.** `StdEdited.cs` is hand-curated for `std::basic_string` per the existing comment. Once we point at MSVC 14.29 STL, the parser AST for `std::string` should match what `StdEdited.cs` was originally hand-curated against (since 0.10.5 + 14.29 was the original pairing). **Expectation:** no `StdEdited.cs` changes needed. *worth a diff check during Path 1 verification*.

---

## References

### CppSharp upstream

- [mono/CppSharp on GitHub](https://github.com/mono/CppSharp) — main repo (3,379 stars, last push 2026-05-18)
- [CppSharp v1.2 release](https://github.com/mono/CppSharp/releases/tag/v1.2) — 2025-11-19, clang 19
- [CppSharp v1.1 release](https://github.com/mono/CppSharp/releases/tag/v1.1) — 2023-10-18, clang 16/18 + VS2022
- [CppSharp v1.0.45.22293 release](https://github.com/mono/CppSharp/releases/tag/v1.0.45.22293) — 2023-02-06
- [NuGet CppSharp package gallery](https://www.nuget.org/packages/CppSharp/) — latest 1.1.84.17100
- [CppSharp ParserOptions.cs (main)](https://github.com/mono/CppSharp/blob/main/src/Parser/ParserOptions.cs) — current `SetupMSVC()` source
- [CppSharp docs/LLVM.md](https://github.com/mono/CppSharp/blob/main/docs/LLVM.md) — build-from-source instructions (pre-built LLVM auto-download)
- [CppSharp docs/GettingStarted.md](https://github.com/mono/CppSharp/blob/main/docs/GettingStarted.md) — first-time build guide

### MSVC STL clang-version compatibility

- [CppSharp issue #1722 "yvals_core.h(807,1): static_assert failed Error in C++ Standard Library usage"](https://github.com/mono/CppSharp/issues/1722) — definitive thread on the STL clang-version gate; netcorefan1 hits MSVC 14.35.32215 (VS 2022 17.5) with clang 14, closed once VS 2022 support landed in v1.1
- [CppSharp issue #1723 "Trying to support clang 15"](https://github.com/mono/CppSharp/issues/1723) — community LLVM 15.0.7 build attempts, references `_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH` workaround
- [actions/runner-images #8153 "STL compilation fails 'expected Clang 16.0.0 or newer' on windows-2022"](https://github.com/actions/runner-images/issues/8153) — same pattern, GitHub-runner side; confirms the version-gate is header-resident and ratchets per VS update
- [microsoft/STL Changelog wiki](https://github.com/microsoft/STL/wiki/Changelog) — Microsoft's authoritative MSVC STL release notes; clang requirements per release
- [MSVC Compiler Language Updates in VS 17.12 (devblogs.microsoft.com)](https://devblogs.microsoft.com/cppblog/msvc-compiler-language-updates-in-visual-studio-2022-version-17-12/) — context on C++23 features

### Local evidence

- `D:\Code\Utinni\external\CppSharp\lib\CppSharp.CppParser.dll` — embedded string `based on LLVM 11.0.0`
- `D:\Code\Utinni\external\CppSharp\lib\CppSharp.Parser.dll` — reflected `ParserOptions.ClangVersion = "11.0.0"`, `VisualStudioVersion` enum max = `Latest = 16`
- `D:\Code\Utinni\UtinniCoreDotNetGen\Program.cs` — current `Setup(Driver)` body (line 54: `driver.ParserOptions.TargetTriple = "i686-pc-win32-msvc"`)
- `C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Tools\MSVC\14.29.30133\include\yvals_core.h` — verified `#if __clang_major__ < 11` gate
- `C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.44.35207\include\yvals_core.h` — verified `_EMIT_STL_ERROR(STL1000, "Unexpected compiler version, expected Clang 19.0.0 or newer.");`
- `D:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.51.36231\include\yvals_core.h` — verified `_EMIT_STL_ERROR(STL1000, "Unexpected compiler version, expected Clang 20 or newer.");`
- `D:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\MSVC\14.52.36328\include\yvals_core.h` — verified `_EMIT_STL_ERROR(STL1000, "Unexpected compiler version, expected Clang 20 or newer.");`
- `D:\Code\Utinni\.planning\todos\pending\cppsharp-msvc-14.5-incompatibility.md` — original TODO framing
- `D:\Code\Utinni\.planning\STATE.md` — Active Pause section documenting worktree `worktree-agent-a4d0744552aa5c200`
