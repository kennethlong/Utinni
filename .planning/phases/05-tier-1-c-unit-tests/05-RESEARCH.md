# Phase 5: Tier 1 C++ unit tests - Research

**Researched:** 2026-05-23
**Domain:** Native C++ unit testing (Catch2 vendored, MSBuild + direct-exe runner, MSVC v142 / x86 / net472-adjacent)
**Confidence:** HIGH

## Summary

Phase 5 lands the first native C++ unit-test target in `Utinni.sln`. CONTEXT.md is comprehensive and has 7 locked decisions; this research closes the one open question the researcher was tasked with (D-01 — the seed coverage target) plus the version pin for Catch2 (D-02), the vcxproj scaffold pattern (D-03), and the CI lane shape (D-04). All four findings are HIGH confidence — they were verified by reading the actual source files plus the existing sibling project precedents (`Utinni.LoaderLockHarness`, `UtinniCoreDotNet.Tests`) and the live `.github/workflows/ci.yml`.

The native pool that survived Phase 4 D-06 (TRE/IFF parsers moved to managed) is much thinner than the ROADMAP language implies — most of `UtinniCore/` is either (a) globally-bound to RVAs (the entire `swg/*` shim), (b) already covered indirectly by the managed Tier-1 tests via P/Invoke through `test_exports.cpp` (Game callbacks, PluginManager lifecycle, FindPattern, the depth-texture lazy-init harness), or (c) shallow forwarders to vendored libs (the `log` API is a 4-line wrapper around `spdlog::critical/error/...`). The substantive native-pure surface is the `stringUtility` namespace in `utility/string_utility.h`.

**Primary recommendation:**

- **D-01 seed:** **`stringUtility::toBool / toString(int) / toHexString / trim` round-trip + boundary tests** — pure inline header-only helpers, no globals, no RVA hooks, no linker dependency on `UtinniCore.dll`. The functions are also load-bearing: `PluginManager::loadPlugins` uses `stringUtility::toString(i, 2)` and `stringUtility::toBool(stringUtility::trim(...))` to parse `[Plugins] plugin_NN = enabled, dir` — if a regression silently broke `toBool("true ")` (trailing space) or `toString(0, 2)` (zero-padding), the plugin priority list would be silently corrupted at save-time. **Backup pick:** add `utinni::log::subscribe / unsubscribe` registry tests (handle-based R-A surface) for a second test file demonstrating link-against-`UtinniCore.dll`. Skip `memory::*` (whole API exists to mutate live SWG memory via `VirtualProtect`, completely untestable in isolation), `PluginManager` (already covered by Tier-1 managed `PluginManagerLifecycleTests.cs` via the `utinni_test_pluginManager*` P/Invoke surface — re-covering native-side would be ceremonial, fails D-06 max-harness).
- **D-02 Catch2 tag:** **`v3.15.0` (released 2026-05-12, verified via GitHub release API).** Ship `catch_amalgamated.{hpp,cpp}` from the release assets at `external/catch2/`. C++14 minimum, supports VS2017+ explicitly — well within UtinniCore's C++17 / MSVC v142 pin. No known x86/MSVC v14x gotchas.
- **D-03 vcxproj scaffold:** **Model on `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj`** — same `ConfigurationType=Application` console exe shape, same `PlatformToolset=v142`, same `$(SolutionDir)bin\$(Configuration)\` OutDir. **Add the third config (RelWithDbgInfo) the harness omits** — CON-T-02 requires triple-config preservation; the harness is the precedent for the first two configs only.
- **D-04 CI lane:** **Insert as the third `dotnet test` lane in `.github/workflows/ci.yml`** — but the new lane is *not* `dotnet test`, it's a direct exe invocation: `& bin\Release\UtinniCore.Tests.exe --reporter junit --out UtinniCore.Tests/TestResults/cli-test-results.xml`. Mirrors the Phase 4 D-11 artifact-upload pattern. Drop right after the existing "Run CLI golden tests" step (ci.yml:94).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Catch2 framework hosting | Native test exe (`UtinniCore.Tests.exe`) | — | Self-runner; ships with `main()` provided by `catch_amalgamated.cpp`. |
| Vendored Catch2 source | `external/catch2/` directory | — | Mirrors the existing zero-package-manager posture (CppSharp, DetourXS, ImGuizmo, LeksysINI, imgui, nvapi, spdlog all live under `external/`). |
| Build config (triple) | `UtinniCore.Tests.vcxproj` | `Utinni.sln` | CON-T-02 — Debug + Release + RelWithDbgInfo all build the exe; sln maps `RelWithDbgInfo|x86 → Release|Win32` for projects that don't have the third native config (precedent: LoaderLockHarness mapping). |
| Test execution in CI | `.github/workflows/ci.yml` third lane | `actions/upload-artifact@v4` on failure | Extends Phase 4 D-11; gates `master` from day one. |
| Pure-helper coverage (`stringUtility::*`) | `UtinniCore.Tests/StringUtilityTests.cpp` | — | Header-only inline functions; no link dependency on `UtinniCore.dll`. |
| Optional second target (R-A log handle registry) | `UtinniCore.Tests/LogSubscribeTests.cpp` | UtinniCore.lib (if produced) OR direct `.cpp` include | Demonstrates the link-against-prod-DLL path for future native test work; deferred to 05-03 or skipped per planner discretion. |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Catch2 (amalgamated) | v3.15.0 | Test framework + self-runner `main()` | [VERIFIED: GitHub releases API 2026-05-23] Vendor-friendly single-source-pair, supports VS2017+ (UtinniCore is v142/VS2019 — comfortably inside), C++14 minimum (UtinniCore is C++17 — fine). The amalgamated build is exactly the "drop in one .hpp + one .cpp" shape that matches the project's `external/` posture. |

### Supporting

None. Catch2's amalgamated build is self-contained; no helper libs, no companion vendor needed for the seed scope.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Catch2 v3.15.0 amalgamated | Catch2 v2.x single-header (`catch.hpp`) | v2.x is one file (smaller), but v3.x ships maintenance fixes through 2026 and is now the actively maintained line. CONTEXT.md D-02 already specifies amalgamated v3.x (`catch_amalgamated.{hpp,cpp}`); v2 is not on the table. |
| Catch2 v3.15.0 amalgamated | doctest single-header | doctest is single-header truly (no .cpp), and the v3.x amalgamated build still has a .cpp. But Catch2 is dramatically better-known, well-documented BDD-style `SECTION` / `GIVEN/WHEN/THEN` matches what CONTEXT.md "smoke set" specifies, and TESTING.md §5 explicitly names Catch2. No reason to deviate. |
| Catch2 amalgamated drop | Catch2 via vcpkg/CMake `find_package` | Deferred to Phase 6 STAB-03 per CONTEXT.md D-02. **Do not** pre-empt. |
| Catch2 amalgamated drop | Catch2 single static lib (`Catch2Main.lib` + `Catch2.lib`, the non-amalgamated build) | Requires the library build step in MSBuild; amalgamated drop just compiles `catch_amalgamated.cpp` as a `<ClCompile>` item alongside the test sources. Simpler. CONTEXT.md D-02 locks this. |

**Installation:**

```bash
# Manual vendor drop (no package manager).
mkdir external/catch2
curl -L https://github.com/catchorg/Catch2/releases/download/v3.15.0/catch_amalgamated.hpp -o external/catch2/catch_amalgamated.hpp
curl -L https://github.com/catchorg/Catch2/releases/download/v3.15.0/catch_amalgamated.cpp -o external/catch2/catch_amalgamated.cpp
```

**Version verification:**

```bash
# Live-fetched 2026-05-23 via GitHub releases API:
#   tag_name:     v3.15.0
#   published_at: 2026-05-12T11:19:23Z
#   assets:       catch_amalgamated.cpp / .hpp (plus .asc signatures)
curl -s https://api.github.com/repos/catchorg/Catch2/releases/latest \
  | jq -r '.tag_name, .published_at'
```

[VERIFIED: GitHub releases API, 2026-05-23] Pin in commit message: `catch2 v3.15.0 (sha256 of catch_amalgamated.hpp/cpp recorded in commit body)`.

## Package Legitimacy Audit

> Not applicable in the npm/PyPI/crates sense — Catch2 is **vendored source**, not an installed package. No package-manager registry is touched by this phase. The CONTEXT.md D-02 decision explicitly preserves the zero-package-manager posture; vcpkg and equivalents are deferred to Phase 6 STAB-03.

| Package | Source | Verification |
|---------|--------|--------------|
| Catch2 v3.15.0 | github.com/catchorg/Catch2 (official) | [VERIFIED: GitHub releases API 2026-05-23] Tag exists, assets `catch_amalgamated.hpp` + `catch_amalgamated.cpp` published with PGP signature `.asc` files. Catchorg is the canonical org (Phil Nash, ~19k stars). Two-decade-old project; not a slop risk. |

**Verification recommendation for the planner:** capture the SHA-256 of both `catch_amalgamated.hpp` and `catch_amalgamated.cpp` in the commit message that adds them, so future tag bumps have a tamper-detection trail. (CONTEXT.md doesn't require this; offered as a polish.)

## Architecture Patterns

### System Architecture Diagram

```
                       master push / PR
                              │
                              ▼
              .github/workflows/ci.yml (windows-2022)
                              │
       ┌──────────────────────┼──────────────────────┐
       ▼                      ▼                      ▼
  msbuild Utinni.sln      dotnet test           [NEW Phase 5]
  /Configuration=Release  UtinniCoreDotNet.Tests dotnet test
  /Platform=x86           (Phase 1 lane)         Utinni.Cli.Tests
       │                                          (Phase 4 lane)
       ▼                                                │
  CON-T-01 post-build:                                  ▼
    xcopy data/                                  [NEW Phase 5]
    UtinniCoreDotNetGen.exe                      bin\Release\
       │                                         UtinniCore.Tests.exe
       ▼                                         --reporter junit
  bin\Release\                                   --out test-results.xml
   ├── UtinniCore.dll                                   │
   ├── Launcher.exe                                     ▼
   ├── UtinniCoreDotNet.dll                       if: failure()
   ├── Utinni.LoaderLockHarness.exe              actions/upload-
   ├── Utinni.Cli.exe                            artifact@v4
   └── UtinniCore.Tests.exe   ◄── NEW

UtinniCore.Tests.exe internal flow:
   catch_amalgamated.cpp::main()
        │
        ▼
   Discovers TEST_CASE in:
   ├── StringUtilityTests.cpp  (seed coverage — D-01)
   ├── SmokeTests.cpp           (vendor-drop sanity — 05-01)
   └── [optional] LogSubscribeTests.cpp  (R-A handle registry, if linked)
        │
        ▼
   Each TEST_CASE runs to completion;
   junit reporter writes test-results.xml;
   exe exit code = 0 (green) or 1 (red).
```

### Recommended Project Structure

```
UtinniCore.Tests/                          # NEW sibling project
├── UtinniCore.Tests.vcxproj               # MSBuild console exe; triple-config
├── main_smoke.cpp                         # 05-01: trivial smoke tests
│                                           #   - REQUIRE(1+1 == 2)
│                                           #   - REQUIRE_THROWS_AS exception machinery
│                                           #   - SECTION + GIVEN/WHEN/THEN BDD runner sanity
├── StringUtilityTests.cpp                 # 05-02: seed coverage (D-01 pick)
│                                           #   - [utility] tag
│                                           #   - toBool round-trip
│                                           #   - toString(int, fillCount) zero-pad
│                                           #   - toHexString
│                                           #   - trim / trimStart / trimEnd
└── [optional] LogSubscribeTests.cpp       # 05-03 (planner discretion): R-A registry

external/catch2/                            # NEW vendor directory (mirrors external/spdlog/)
├── catch_amalgamated.hpp                  # v3.15.0
└── catch_amalgamated.cpp                  # v3.15.0
```

### Pattern 1: TEST_CASE with tags

**What:** Catch2's primary test-declaration macro. One file = many TEST_CASEs.

**When to use:** Every test in the seed.

**Example (proposed for StringUtilityTests.cpp):**

```cpp
// Source: Catch2 v3.15.0 docs - https://github.com/catchorg/Catch2/tree/v3.15.0/docs
// MIT License header (23-line block per CONVENTIONS.md) - omitted for brevity.

#include <catch2/catch_amalgamated.hpp>
#include "utility/string_utility.h"

TEST_CASE("stringUtility::toBool parses true/false case-sensitively", "[utility][string]")
{
    REQUIRE(stringUtility::toBool("true")  == true);
    REQUIRE(stringUtility::toBool("false") == false);
    REQUIRE(stringUtility::toBool("True")  == false);  // boolalpha is case-sensitive
    REQUIRE(stringUtility::toBool("")      == false);
    REQUIRE(stringUtility::toBool("garbage") == false);
}

TEST_CASE("stringUtility::toString zero-pads ints to digitFillCount", "[utility][string]")
{
    REQUIRE(stringUtility::toString(0,  2) == "00");
    REQUIRE(stringUtility::toString(7,  2) == "07");
    REQUIRE(stringUtility::toString(42, 2) == "42");
    REQUIRE(stringUtility::toString(123,2) == "123");  // overflow — not truncated
}

TEST_CASE("stringUtility::trim strips whitespace from both ends", "[utility][string]")
{
    std::string s = "  hello  ";
    REQUIRE(stringUtility::trim(s) == "hello");

    // Round-trip with PluginManager's ut.ini parsing idiom:
    // PluginManager::loadPlugins does
    //   stringUtility::toBool(stringUtility::trim(isEnabled))
    // where isEnabled comes from "true , MyPlugin" split on the comma.
    std::string isEnabled = "true ";
    REQUIRE(stringUtility::toBool(stringUtility::trim(isEnabled)) == true);
}
```

### Pattern 2: SECTION-based shared setup

**What:** Catch2's BDD-lite mechanism — each `SECTION` re-runs the enclosing TEST_CASE from the top, giving fresh state per assertion group without explicit fixture classes.

**When to use:** When the same setup is repeated across several closely-related assertions. Lighter than xUnit's `[Fact]` + `[Theory]` split.

**Example:**

```cpp
TEST_CASE("PluginManager [Plugins] line round-trip", "[utility][string][plugin-manager-adjacent]")
{
    // Reproduces the line-format PluginManager writes & reads.
    SECTION("enabled true, name MyPlugin")
    {
        std::string isEnabled = "true ";
        std::string dirName   = " MyPlugin";
        REQUIRE(stringUtility::toBool(stringUtility::trim(isEnabled)) == true);
        REQUIRE(stringUtility::trim(dirName) == "MyPlugin");
    }

    SECTION("zero-pad ordinal at the write side")
    {
        REQUIRE(stringUtility::toString(0,  2) == "00");
        REQUIRE(stringUtility::toString(15, 2) == "15");
        REQUIRE(stringUtility::toString(100,2) == "100");
    }
}
```

### Anti-Patterns to Avoid

- **Linking UtinniCore.Tests.exe against UtinniCore.dll for stringUtility coverage** — `stringUtility::toBool / toString(int) / toHexString / trim*` are all `inline` in `string_utility.h` (lines 37, 44, 51, 58, 65, 72, 78, 84, 89, 94, 99); the only out-of-line symbol is `toString(const std::wstring&)` which is `UTINNI_API extern` (line 35). For the seed, **just `#include "utility/string_utility.h"` and compile `string_utility.cpp` directly into the test exe** — no link against UtinniCore.dll needed. This avoids dragging in globals + RVA hooks + spdlog + DetourXS + the entire `swg/*` tree.
- **Re-covering Phase 3 R-B `PluginManager` lifecycle natively** — already covered by `UtinniCoreDotNet.Tests/PluginManagerLifecycleTests.cs` via the `utinni_test_pluginManager*` P/Invoke surface in `test_exports.cpp:497-530`. Adding a native-side TEST_CASE for the same logic is ceremonial — fails D-06 max-harness ("the seed coverage must catch a real failure mode the existing tests don't").
- **Targeting `memory::*` in `utility/memory.cpp`** — every function in this TU exists to mutate live SWG memory via `VirtualProtect` + `WriteProcessMemory`-like semantics (`findPattern`, `copy`, `set`, `patchAddress`, `nopAddress`, `restoreBytes`, `createJMP`). All take `swgptr` (a raw `uint32_t` address into the SWG process), all need the address to be readable/writable in the test process. Untestable in isolation without building a fake memory region — which is what `test_exports.cpp::utinni_findPattern` already does indirectly via P/Invoke from `FindPatternHarnessTests.cs`. Skip.
- **Targeting `utility/log.cpp`** — the public API (`critical/error/info/warning/debug`) is 4-line forwarders to `spdlog::*`; not test value. The R-A subscribe/unsubscribe registry IS testable (`subscribeOutputSinkCallback` + `unsubscribeOutputSinkCallback` lines 184-209) but it's already covered indirectly by `LogCallbackTests.cs` in the managed Tier-1 suite. Possible 05-03 if planner wants a second native test file demonstrating link-against-DLL; otherwise skip.
- **Adding `#include <Windows.h>` to test files without the `WIN32_LEAN_AND_MEAN` guard** — Catch2 v3 ships with workarounds for the `min`/`max` macro pollution but the project's `string_utility.cpp` calls `WideCharToMultiByte` directly (no LEAN guard in the header). The amalgamated build handles this; just be aware.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Test discovery | Custom `for (auto& fn : tests) fn();` runner | Catch2 `TEST_CASE` macro auto-registers via static init | Catch2 handles assertion expansion ("expected 5, got 7"), section re-entry, exception trapping, JUnit reporter — months of work to re-derive. |
| Assertion failure formatting | `if (!cond) printf(...)` | Catch2 `REQUIRE(expr)` | Catch2 decomposes the expression tree at macro time; you get "REQUIRE(stringUtility::toBool("True") == false) — got: true" automatically. |
| JUnit XML output for CI | Custom xml-writer | Catch2 `--reporter junit --out test-results.xml` | Native; first-party; matches actions/upload-artifact@v4 + GitHub's test-results UI conventions. |
| BDD-style sectioning | Custom test-state machine | `SECTION` / `GIVEN/WHEN/THEN` | Same TEST_CASE re-enters for each section, fresh state per leaf; built-in. |
| Floating-point equality | `actual == expected` | `REQUIRE_THAT(actual, Catch::Matchers::WithinAbs(expected, eps))` | Catch2's Matchers handle float epsilon; project's `swg_math.h` (if ever covered) would need this. |

**Key insight:** This is a 30K-LOC drop-in vendor. The planner should **not** try to subset it ("we don't need matchers, strip them out") — the amalgamated build is meant to be opaque. Audit happens once at vendor time; thereafter it's a black box.

## Runtime State Inventory

> Not applicable — this is a greenfield phase (adding a new project + new files, no rename / refactor / migration). Skipped.

## Common Pitfalls

### Pitfall 1: Missing the third config (`RelWithDbgInfo|Win32`) on the test vcxproj

**What goes wrong:** `Utinni.LoaderLockHarness.vcxproj` (the closest precedent) only declares `Debug|Win32` and `Release|Win32` — no RelWithDbgInfo. Looking at the .sln (lines 100-104), the third config is mapped via `RelWithDbgInfo|x86.ActiveCfg = Release|Win32` (i.e., the harness builds `Release` when the solution is set to `RelWithDbgInfo`). That works for a tiny harness but **CONTEXT.md D-07 explicitly says all three configs must produce a working test exe**. If the planner copies the LoaderLockHarness vcxproj verbatim, the test exe will silently build the Release version under RelWithDbgInfo with no debug info, breaking CON-T-02.

**Why it happens:** The LoaderLockHarness was a one-week Phase 2 deliverable; the implementer (correctly for that scope) didn't bother with the third config because no one was going to debug the loader-lock measurement script. Phase 5 has different requirements — D-07 is explicit.

**How to avoid:** When the planner writes 05-01, declare all three `ProjectConfiguration Include` entries up front. Mirror the UtinniCore.vcxproj triple-config block (lines 3-15 of `UtinniCore.vcxproj`) — that's the canonical example.

**Warning signs:** `Utinni.sln` ProjectConfigurationPlatforms section showing `RelWithDbgInfo|x86.ActiveCfg = Release|Win32` for the new project's GUID. **Should be** `RelWithDbgInfo|x86.ActiveCfg = RelWithDbgInfo|Win32`.

### Pitfall 2: Forgetting `<ConformanceMode>true</ConformanceMode>` (`/permissive-`)

**What goes wrong:** Without `/permissive-`, MSVC accepts non-standard C++ that Catch2 may rely on the compiler rejecting (e.g., two-phase name lookup edge cases in Catch2's matcher templates). UtinniCore.vcxproj sets `ConformanceMode=true` for Debug but explicitly `false` for Release (line 109 + 139). The LoaderLockHarness sets it to `true` on both configs (lines 59, 78).

**Why it happens:** Inherited inconsistency. The Debug config wants strict checking; the Release config got `false` to work around some long-forgotten warning.

**How to avoid:** Follow the LoaderLockHarness precedent — `ConformanceMode=true` everywhere. Catch2 v3 is clean under `/permissive-`.

**Warning signs:** Build warnings like C2065 or C2143 in `catch_amalgamated.cpp` under a specific config. (Unlikely with v3.15.0 + v142 + C++17, but flag if it appears.)

### Pitfall 3: `_HAS_EXCEPTIONS=0` is NOT set on UtinniCore.dll, but Catch2 v3 relies on exceptions

**What goes wrong:** UtinniCore is compiled with `SPDLOG_NO_EXCEPTIONS` (vcxproj line 83/107/138) — but that's a spdlog-specific flag, NOT a global `_HAS_EXCEPTIONS=0` ban. Catch2's `REQUIRE_THROWS_AS`, `CHECK_THROWS_WITH`, and matcher infrastructure all use real C++ exceptions. UtinniCore.dll's production code has zero `try/catch/throw` (CONVENTIONS.md confirms), so this hasn't been tested. **Catch2's exception infrastructure will work fine in `UtinniCore.Tests.exe`** because it's a fresh translation unit with default exception settings. **But:** if the planner later decides to call into `UtinniCore.dll` for a test (e.g., the optional `LogSubscribeTests.cpp`), and that DLL call indirectly invokes a path that would throw, the throw will propagate across the DLL boundary. That's a normal C++ pattern, but `SPDLOG_NO_EXCEPTIONS` confuses people into thinking the whole codebase is exception-free. **It's not. The flag is spdlog-specific.**

**Why it happens:** The flag name is misleading. `SPDLOG_NO_EXCEPTIONS` means "spdlog itself does not throw"; it does NOT mean "this DLL is compiled with `/EHs-`".

**How to avoid:** Do not add `_HAS_EXCEPTIONS=0` or `/EHs-` to `UtinniCore.Tests.vcxproj`. Leave the default `/EHsc` in place (matches UtinniCore.dll's actual posture, even if not explicitly stated in its vcxproj).

**Warning signs:** Build error `cannot use try in /clr without /EHa or exceptions disabled` (would only fire under /clr, which this project does not use).

### Pitfall 4: Test exe runs from the wrong working directory in CI

**What goes wrong:** Catch2's `--out test-results.xml` writes relative to the exe's working directory. Under `actions/upload-artifact@v4`, the workflow's `cwd` is the repo root, but the exe lives under `bin\Release\`. If invoked as `& bin\Release\UtinniCore.Tests.exe --reporter junit --out test-results.xml`, the .xml lands in the **repo root**, not under `UtinniCore.Tests/TestResults/` where Phase 4 D-11 puts its `.trx`.

**Why it happens:** Path defaults; PowerShell `&` invocation respects current dir, not exe dir.

**How to avoid:** Use an absolute `--out` path: `--out UtinniCore.Tests\TestResults\junit-results.xml`. Mirror Phase 4's directory naming convention (`Utinni.Cli.Tests/TestResults/cli-test-results.trx`).

**Warning signs:** Local run works (Visual Studio sets `cwd` to the project dir); CI run produces a junit-results.xml in the repo root that the upload step doesn't find.

### Pitfall 5: Catch2 default `main()` swallows the assertion failure exit code on certain reporter configs

**What goes wrong:** Catch2 v3 with `--reporter junit` exits 0 even on test failure if the reporter doesn't get a chance to write the xml (e.g., a SEH crash in the test, or an `abort()` from inside the SWG code under test). The xml IS written, but exit-code parsing by `actions/upload-artifact@v4`'s `if: failure()` may not trigger.

**Why it happens:** Reporter machinery writes incrementally; abrupt termination skips the exit-code remap.

**How to avoid:** Use **two reporters simultaneously** — `--reporter junit --out junit-results.xml --reporter console` — so the console reporter sets the exit code while the junit reporter writes the xml for triage. Catch2 v3 supports stacked reporters via repeated `--reporter` flags. Verify in CI by deliberately landing a red commit on a throwaway branch (the Phase 1 "test the tester" precedent).

**Warning signs:** A test fails locally (exit 1) but CI shows green; the junit-results.xml shows the failure but the workflow step's exit code says 0.

## Code Examples

Verified patterns from Catch2 v3.15.0 documentation:

### Standalone smoke test (05-01)

```cpp
// Source: Catch2 v3.x docs §"Tutorial — getting Catch2 up and running"
//   https://github.com/catchorg/Catch2/blob/v3.15.0/docs/tutorial.md
// MIT header (23-line block per CONVENTIONS.md) omitted for brevity.

#include <catch2/catch_amalgamated.hpp>

TEST_CASE("Smoke: vendored Catch2 runs", "[smoke]")
{
    REQUIRE(1 + 1 == 2);
}

TEST_CASE("Smoke: exception machinery works", "[smoke]")
{
    REQUIRE_THROWS_AS(
        []() { throw std::runtime_error("boom"); }(),
        std::runtime_error);
}

TEST_CASE("Smoke: SECTION re-entry produces fresh state", "[smoke]")
{
    int counter = 0;

    SECTION("first section increments counter")
    {
        counter++;
        REQUIRE(counter == 1);
    }

    SECTION("second section sees a fresh counter (state did not leak)")
    {
        counter++;
        REQUIRE(counter == 1);  // would be 2 if SECTION re-entry were broken
    }
}
```

### Seed coverage (05-02) — stringUtility round-trip

```cpp
// Source: production code in UtinniCore/utility/string_utility.h
//   (header-only, inline) -- coverage rationale documented in
//   .planning/phases/05-tier-1-c-unit-tests/05-RESEARCH.md §D-01.
// MIT header omitted.

#include <catch2/catch_amalgamated.hpp>
#include "utility/string_utility.h"

TEST_CASE("stringUtility::toBool round-trip via boolalpha", "[utility][string]")
{
    SECTION("canonical true/false strings")
    {
        REQUIRE(stringUtility::toBool("true")  == true);
        REQUIRE(stringUtility::toBool("false") == false);
    }

    SECTION("case-sensitivity (std::boolalpha is case-sensitive)")
    {
        REQUIRE(stringUtility::toBool("True")  == false);
        REQUIRE(stringUtility::toBool("FALSE") == false);
    }

    SECTION("non-matching input defaults to false (istringstream fails silently)")
    {
        REQUIRE(stringUtility::toBool("")        == false);
        REQUIRE(stringUtility::toBool("garbage") == false);
    }
}

TEST_CASE("stringUtility::toString(int, fillCount) zero-pads correctly", "[utility][string]")
{
    REQUIRE(stringUtility::toString(0,   2) == "00");
    REQUIRE(stringUtility::toString(7,   2) == "07");
    REQUIRE(stringUtility::toString(42,  2) == "42");
    REQUIRE(stringUtility::toString(100, 2) == "100");  // wider than fill — not truncated
}

TEST_CASE("stringUtility::toHexString lowercase hex with zero padding", "[utility][string]")
{
    REQUIRE(stringUtility::toHexString(0,    4) == "0000");
    REQUIRE(stringUtility::toHexString(0xAB, 4) == "00ab");
    REQUIRE(stringUtility::toHexString(static_cast<uint32_t>(0xDEADBEEF), 8) == "deadbeef");
}

TEST_CASE("stringUtility::trim / trimStart / trimEnd strip default whitespace", "[utility][string]")
{
    SECTION("trim strips both ends")
    {
        std::string s = "  hello  ";
        REQUIRE(stringUtility::trim(s) == "hello");
        REQUIRE(s == "hello");  // in-place mutation
    }

    SECTION("trimStart strips only leading")
    {
        std::string s = "  hello  ";
        REQUIRE(stringUtility::trimStart(s) == "hello  ");
    }

    SECTION("trimEnd strips only trailing")
    {
        std::string s = "  hello  ";
        REQUIRE(stringUtility::trimEnd(s) == "  hello");
    }

    SECTION("PluginManager [Plugins] line idiom: trim then toBool")
    {
        std::string isEnabled = " true ";
        REQUIRE(stringUtility::toBool(stringUtility::trim(isEnabled)) == true);

        std::string dirName = "  MyPlugin  ";
        REQUIRE(stringUtility::trim(dirName) == "MyPlugin");
    }
}
```

**Failure modes each test catches (per D-06 max-harness):**

| Test | What it catches if reverted |
|------|-----------------------------|
| `toBool("true") == true` | A regression of `>> std::boolalpha` to `>> std::noboolalpha` would silently return false for every plugin-config flag. PluginManager would treat every plugin as disabled. |
| `toString(0, 2) == "00"` | A regression of `std::setw(digitFillCount) << std::setfill('0')` to `std::setw(digitFillCount)` (forgetting setfill) would yield ` 0` instead of `00`. PluginManager would write `[Plugins] plugin_ 0 = ...` (space, not zero), and the next-startup read would fail to find `plugin_00`, dropping every plugin from the priority list. |
| `toHexString(0xDEADBEEF, 8) == "deadbeef"` | A regression of `std::hex` to default would write `3735928559` (decimal) for every memory address logged via `toHexString(addr)`. Every log line referencing an RVA would become impossible to grep. |
| `trim(" hello ") == "hello"` | A regression of `find_first_not_of(trimChars)` to `find_first_of(trimChars)` (operator inversion) would invert the trim — `trim(" hello ")` would return ` `. PluginManager's `trim(directoryName)` would corrupt every plugin directory string. |

### CI invocation (proposed third step in ci.yml — after the existing Phase 4 CLI step at line 94)

```yaml
- name: Run native unit tests (UtinniCore.Tests.exe)
  shell: pwsh
  run: |
    & bin\Release\UtinniCore.Tests.exe `
      --reporter console `
      --reporter junit::out=UtinniCore.Tests\TestResults\junit-results.xml
  # Phase 5 D-04: third CI lane gates `master` on the native test suite.
  # Direct exe invocation (not `dotnet test`) because the test runner is Catch2 self-runner
  # in a Win32 native exe, not a managed assembly. Stacked reporters: console sets exit
  # code on failure (Pitfall 5 mitigation); junit writes the xml for triage. mkdir is
  # implicit -- Catch2 v3 creates the output dir if missing.

- name: Upload native test artifacts (on failure)
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: native-test-results
    path: UtinniCore.Tests/TestResults/junit-results.xml
    if-no-files-found: warn
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Catch v1.x single-header (`catch.hpp`) | Catch2 v3.x amalgamated `.hpp + .cpp` | 2022 with Catch2 v3.0.1 release | Header-only Catch was slow to compile because every TU got the full framework. v3.x splits into header + compiled .cpp, dramatically improving rebuild times. Amalgamated drop is the v3-era "single file pair" equivalent. |
| `ctest` orchestration | Direct exe invocation under CI | Always an option; not all projects use CMake | Phase 5 chooses direct invocation (CONTEXT.md D-03) — the test exe IS the runner; ctest would just shell out to it anyway. |
| VS Test Adapter for Catch2 | Direct exe invocation | Rejected per D-03 alt | Adapter quality on net472 + dual-runner complexity not worth it for a single native test target. |

**Deprecated/outdated:**

- Catch v1.x — replaced by Catch2 v2.x circa 2017, then v3.x in 2022. Do not use v1.
- `Catch.hpp` (single-file v2.x) — still supported, but v3.x is the actively maintained line. CONTEXT.md D-02 picks v3.x explicitly.

## Assumptions Log

> All claims in this research were verified against (a) the actual source files at `D:\Code\Utinni\UtinniCore\utility\*` and `D:\Code\Utinni\Utinni.LoaderLockHarness\Utinni.LoaderLockHarness.vcxproj`, (b) the GitHub releases API for Catch2 v3.15.0, or (c) the existing `.github/workflows/ci.yml` (Phase 4 D-11 precedent).

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Catch2 v3.15.0's amalgamated build compiles cleanly under MSVC v142 + C++17 + x86 + `/permissive-` + `_HAS_EXCEPTIONS=1`. | D-02 / Pitfall 2 / Pitfall 3 | LOW — the v3.x release notes explicitly restore VS2017 support (newer than v142 is VS2019); C++14 is the minimum (UtinniCore is C++17). The planner will discover any incompatibility at scaffold time (05-01) before the seed lands; CONTEXT.md D-05 explicitly soak-tests the scaffold on master before 05-02 builds on it. |
| A2 | The R-A `subscribeOutputSinkCallback` registry IS testable from a native test exe linked against UtinniCore.dll. | Anti-Patterns / D-05 | LOW — the symbol is `UTINNI_API extern` (`utility/log.h:40`), so it's exported. The test would need to instantiate spdlog enough to fire `sink_it_`, which would require `utinni::log::create()` to be called — that does file I/O for the log file. Workable but heavier than the stringUtility path. Planner discretion; researcher recommends deferring to 05-03 if pursued. |

## Open Questions

> Few open questions remain — CONTEXT.md was thorough.

1. **Should the seed coverage be a single tag (`[utility]`) or two (`[utility][string]`)?**
   - What we know: Catch2 supports multi-tag TEST_CASEs. The phase only ships ~6 tests in 05-02, all on stringUtility.
   - What's unclear: Whether future phases (06 STAB-03 or beyond) will add native tests under other utility helpers (`utility/memory`, `utility/utility.cpp`) — if yes, two tags is cleaner; if no, one tag is fine.
   - Recommendation: Two tags from day one (`[utility]` + `[string]`). Cheap forward compatibility. Smoke tests get `[smoke]`. The optional R-A LogSubscribe tests (05-03 if pursued) would get `[utility][log][plugin_framework]`.

2. **Should `UtinniCore.Tests.vcxproj` declare a `<ProjectReference>` on `UtinniCore.vcxproj`?**
   - What we know: `Utinni.LoaderLockHarness.vcxproj` (line 98-101) declares the reference with `<LinkLibraryDependencies>false</LinkLibraryDependencies>` so the post-build chain (CON-T-01) fires in the right order but the harness doesn't actually link against UtinniCore.dll. The harness `LoadLibrary`'s it at runtime.
   - What's unclear: For the stringUtility seed (header-only, with `string_utility.cpp` compiled directly into the test exe), the planner could go either way. With the reference (`LinkLibraryDependencies=false`), build ordering is preserved; without it, the test exe is independent.
   - Recommendation: **Add the reference** with `LinkLibraryDependencies=false`, mirroring the LoaderLockHarness precedent. This keeps the build graph honest (UtinniCore must build first so the post-build chain runs) and gives the optional 05-03 LogSubscribe path a clean upgrade — just flip `LinkLibraryDependencies` to `true` and add UtinniCore.lib as an additional dependency.

3. **CONTEXT.md "Reusable Assets" mentions `Utinni.CrtMatchPlugin` as a precedent for sibling MSBuild projects. Is it a better model than LoaderLockHarness?**
   - What we know: `Utinni.CrtMatchPlugin` is a DLL (`.vcxproj` produces a DLL output), `Utinni.LoaderLockHarness` is an EXE. The test exe is also an EXE.
   - Recommendation: **LoaderLockHarness is the better precedent** — both are console EXEs sibling-built to UtinniCore.dll. Use it.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| MSBuild | Build the test vcxproj | ✓ | v142 (VS 2019) confirmed in every existing vcxproj | — |
| MSVC v142 toolset | Compile Catch2 amalgamated + tests | ✓ | Confirmed in `UtinniCore.vcxproj` line 27/33 | — |
| Catch2 v3.15.0 release assets | Vendor `catch_amalgamated.{hpp,cpp}` | ✓ | v3.15.0, 2026-05-12 | Older v3.x (e.g., v3.7) also works — C++14 min preserved across all 3.x |
| Windows SDK 10.0 | Build any vcxproj | ✓ | Confirmed in every existing vcxproj | — |
| windows-2022 CI runner | Run native test exe under CI | ✓ | Confirmed in `.github/workflows/ci.yml:18` | — |
| `actions/upload-artifact@v4` | Artifact upload on test failure | ✓ | Confirmed in `.github/workflows/ci.yml:88, 100` (Phase 4 precedent) | — |
| DirectX SDK June 2010 | Build *UtinniCore.dll* (test exe depends on it via ProjectReference) | ✓ | Cached + installed by CI workflow (ci.yml:38-69) | — |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** None — every dependency Phase 5 needs is already on the CI runner from Phase 1 + Phase 4.

## Validation Architecture

> `.planning/config.json` does not exist; per the agent instruction "absent = enabled," include this section.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | Catch2 v3.15.0 (vendored amalgamated build at `external/catch2/`) |
| Config file | none — Catch2 v3 doesn't require one; CLI flags drive behavior |
| Quick run command | `bin\Release\UtinniCore.Tests.exe --reporter console` |
| Full suite command | `bin\Release\UtinniCore.Tests.exe --reporter console --reporter junit::out=UtinniCore.Tests\TestResults\junit-results.xml` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TEST-02 | Catch2 builds in CI under at least one config | smoke | `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86 /t:UtinniCore.Tests` | ❌ Wave 0 |
| TEST-02 | Catch2 self-runner produces JUnit-format XML | smoke | `bin\Release\UtinniCore.Tests.exe --reporter junit::out=...` | ❌ Wave 0 |
| TEST-02 | At least one native parser or helper has Catch2 coverage (seed) | unit | `bin\Release\UtinniCore.Tests.exe "[utility][string]"` | ❌ Wave 0 |
| TEST-02 | CI gates `master` on the new lane | integration | `.github/workflows/ci.yml` updated with new "Run native unit tests" step + `if: failure()` artifact upload | ❌ Wave 0 |
| TEST-02 | `RelWithDbgInfo|Win32` config produces a working test exe | smoke | `msbuild Utinni.sln /p:Configuration=RelWithDbgInfo /p:Platform=x86 /t:UtinniCore.Tests` (per D-07 / CON-T-02) | ❌ Wave 0 |
| TEST-02 | Seed coverage detects the failure modes in §"Code Examples > Failure modes" table | unit (max-harness per D-06) | `bin\Release\UtinniCore.Tests.exe "[utility][string]" --break` after deliberately reverting one of the four asserted properties on a throwaway branch | ❌ Wave 0 (procedure documented but only exercised if planner adopts "test the tester" precedent from Phase 1) |

### Sampling Rate

- **Per task commit:** `bin\Release\UtinniCore.Tests.exe --reporter console`
- **Per wave merge:** `bin\Release\UtinniCore.Tests.exe --reporter console --reporter junit::out=UtinniCore.Tests\TestResults\junit-results.xml`
- **Phase gate:** Full suite green on `windows-2022` CI for both 05-01 (scaffold + smoke) and 05-02 (seed coverage). Then `/gsd:verify-work`.

### Wave 0 Gaps

- [ ] `external/catch2/catch_amalgamated.hpp` + `.cpp` — vendor drop (05-01 first task)
- [ ] `UtinniCore.Tests/UtinniCore.Tests.vcxproj` — sibling MSBuild project, triple-config
- [ ] `UtinniCore.Tests/main_smoke.cpp` — 2-3 smoke tests proving the vendor drop works (05-01)
- [ ] `UtinniCore.Tests/StringUtilityTests.cpp` — seed coverage (05-02)
- [ ] `Utinni.sln` — register new project + triple-config mappings (05-01)
- [ ] `.github/workflows/ci.yml` — third lane (05-01)
- [ ] `docs/ai/test-harness-plan.md` "Tier 1 — C++ side" row update (05-02 close, per CONTEXT.md domain "In scope")

## Security Domain

> `.planning/config.json` does not exist (absent = enabled). However, **Phase 5 has zero security surface** — it adds a test-only executable that runs on the CI runner and on developer machines, consumes no network input, parses no untrusted data, and exposes no public API. The only relevant ASVS family would be V14 (Configuration), specifically the "vendor pinning + integrity" guidance.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | n/a |
| V3 Session Management | no | n/a |
| V4 Access Control | no | n/a |
| V5 Input Validation | no | Test exe has no input surface beyond `argv` (Catch2 CLI flags); Catch2 itself parses argv. |
| V6 Cryptography | no | n/a |
| V14 Configuration | yes | Pin Catch2 to `v3.15.0` in commit message; record SHA-256 of `catch_amalgamated.{hpp,cpp}` in the same commit for future tamper detection. CONTEXT.md D-02 specifies "pinned tag, no resolution surprises" — this section operationalizes it. |

### Known Threat Patterns for {stack}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Vendor source tampering | Spoofing/Tampering | SHA-256 captured at vendor time in commit message; Catch2 ships PGP `.asc` signatures (the planner may optionally verify, but the project doesn't have a key-trust chain yet — flagged as Phase 6 STAB-03 optional polish) |
| Stale vendor (CVEs) | Tampering | Catch2 is a test framework, not a runtime dependency; vendor age has minimal product impact. Bumps revisit at Phase 6 STAB-03 alongside imgui/spdlog/ImGuizmo. |

## §D-01 Native Seed Candidates — Decision Table

> The deliverable: planner reads this table, picks the top recommendation (or a clearly-justified alternative), and writes 05-02 against it.

| Target | What it covers | Sample test cases | Failure mode each test catches (D-06 max-harness) | Effort (S/M/L) | Refactor required? | Already covered by Tier-1 managed? | Verdict |
|--------|---------------|-------------------|--------------------------------------------------|----------------|--------------------|------------------------------------|---------|
| **`utility/string_utility.h` — `toBool / toString(int) / toHexString / trim*`** | 6 inline helpers, header-only, used by PluginManager's `[Plugins]` line parsing (load + save) | ~6 TEST_CASEs across boolalpha case-sensitivity, zero-pad fill, hex format, trim/trimStart/trimEnd, round-trip via PluginManager's idiom | (1) boolalpha-flag drop → PluginManager treats every plugin as disabled. (2) setfill-drop → `plugin_ 0` written, never read back. (3) hex-flag drop → log lines become un-greppable. (4) trim operator inversion → plugin dir strings corrupted. | **S** (~1 day, ~6 tests + 1 file) | **No** (header-only, pure) | **No** (managed tests don't exercise these directly; PluginManagerLifecycleTests covers the consumer but not these primitives) | **★ RECOMMENDED PICK** |
| `utility/log.h / log.cpp` — `subscribeOutputSinkCallback / unsubscribeOutputSinkCallback / dispatch via spdlog::OutputSink::sink_it_` | R-A handle-based subscribe/unsubscribe registry; thread-safety contract (CR-01/WR-07 from 03-REVIEW) | ~5 TEST_CASEs: register-and-fire, unsubscribe-removes-from-fire-list, two subscribers fire in insertion order, handle-zero rejected, double-unsubscribe is a no-op | (1) Registry mutation during dispatch races (CR-01 regression) → crash. (2) Wrong insertion order → broken FIFO contract. (3) Handle 0 accepted as valid → conflicts with "0 = invalid" sentinel. | **M** (~2 days; needs `utinni::log::create()` setup and teardown, which writes a file) | **No** (R-A surface already designed for test) | **Indirectly** — `LogCallbackTests.cs` exercises the managed side P/Invoke through `addOutputSinkCallback`, which routes to `subscribeOutputSinkCallback`. The native-side direct surface IS distinct, but the value-add is "tests at a lower layer than the managed P/Invoke" — defensible but D-06 marginal. | **Backup pick** — defer to 05-03 if planner wants to demonstrate the link-against-UtinniCore.dll path. |
| `utility/memory.h / memory.cpp` — `findPattern / copy / read<T> / write<T>` | Memory-pattern scan + cross-boundary copy via VirtualProtect | None viable in isolation (every function operates on live process addresses) | n/a — would need a fake memory region, which `test_exports.cpp::utinni_findPattern` already P/Invokes from `FindPatternHarnessTests.cs` | **L** (would need a refactor to extract a pure helper) | **Yes** (heavy) | **Yes** (`FindPatternHarnessTests.cs` covers via P/Invoke; native re-cover is ceremonial) | **Skip** — already covered, refactor too heavy for Phase 5. Candidate for Phase 6 STAB-03 IF the refactor materially improves the API. |
| `plugin_framework/plugin_manager.h / .cpp` — `PluginManager::loadPlugins / test_internal::test_loadFromDirectory` | Phase 3 R-B plugin lifecycle; LoadLibrary failure log+continue; destroyPlugin contract; two-phase init/dispose | None new — `PluginManagerLifecycleTests.cs` ALREADY covers `test_loadFromDirectory` via the `utinni_test_pluginManager*` P/Invoke surface | n/a — re-covering would be duplicate work | **L** (test infrastructure already exists managed-side) | **No** | **Yes — fully covered.** Re-covering natively fails D-06 max-harness. | **Skip** — Phase 3 already shipped Tier-1 coverage via managed tests; native re-cover provides zero value-add. |
| `utility/utility.h / utility.cpp` — `showLastErrorMessageBox` etc. | Win32 error helpers | Hard to test (calls `MessageBox` synchronously, which blocks) | n/a | **L** (would need refactor to extract pure formatting) | **Yes** | **No** (not covered) | **Skip** — refactor too heavy for Phase 5. Phase 6 STAB-03 cleanup candidate at best. |
| `swg/misc/swg_math.{h,cpp}` or similar | SWG math helpers (vectors, quaternions) | n/a — every function calls `swg::math::*` RVAs that don't exist in the test process | n/a | **L** (cannot be tested without a fake SWG process — Tier 3 territory) | n/a | n/a | **Skip** — Tier-3 work, deferred to V2. |

### Recommended Pick: `stringUtility::*` (D-06 max-harness PASS)

**Why this beats the alternatives:**

1. **Already used by load-bearing code.** `PluginManager::loadPlugins` (lines 121-138) parses `[Plugins] plugin_NN = enabled, dir` lines using exactly these helpers. A regression in `toBool` or `trim` silently corrupts plugin loading — exactly the "would fail if reverted" criterion D-06 demands.
2. **Pure, header-only, no link dependency on UtinniCore.dll.** Just `#include "utility/string_utility.h"` and compile `string_utility.cpp` directly into the test exe. Zero globals, zero RVA hooks, zero spdlog init.
3. **Not covered by the managed Tier-1 tests.** `PluginManagerLifecycleTests.cs` exercises the *consumer* (PluginManager) but not the *primitive* (stringUtility). A managed Tier-1 test for stringUtility would have to round-trip through CppSharp — far more painful than a native test.
4. **Cheapest possible D-06-passing seed.** ~6 TEST_CASEs in 1 file. ~1 day of work. The Phase budget for 05-02 is small (CONTEXT.md D-05 implies a 2-3-plan split with 05-01 the scaffold).
5. **Sets the pattern.** Future phases (Phase 6 STAB-03 native cleanups) can grow the `[utility]` test suite incrementally without needing to re-derive the test infrastructure.

**Quoted source confirmation** (from `D:\Code\Utinni\UtinniCore\utility\string_utility.h`):

```cpp
// line 37-42:
inline std::string toString(bool b)
{
    std::ostringstream oss;
    oss << std::boolalpha << b;
    return oss.str();
}

// line 44-49:
inline bool toBool(const std::string& input)
{
    bool result;
    std::istringstream(input) >> std::boolalpha >> result;
    return result;
}

// line 51-56:
inline std::string toString(int i, int digitFillCount = 0)
{
    std::ostringstream oss;
    oss << std::setw(digitFillCount) << std::setfill('0') << i;
    return oss.str();
}
```

All inline, all pure, all driveable from a test exe with zero ceremony.

## §D-02 Catch2 Tag Recommendation

**Tag: `v3.15.0` (released 2026-05-12).**

[VERIFIED: GitHub releases API 2026-05-23] Direct API call confirmed: `tag_name = "v3.15.0"`, `published_at = "2026-05-12T11:19:23Z"`, release assets include `catch_amalgamated.cpp` and `catch_amalgamated.hpp` (plus matching `.asc` signature files).

**Why this tag (not v3.7.x or v2.x):**

- **v3.15.0** is the actively-maintained line. Eleven days old at research time, so it's both current and battle-tested enough for vendor work.
- C++14 minimum (per release notes); UtinniCore is C++17 — comfortably inside.
- VS2017+ support explicitly restored in v3.5.3 (per release notes). UtinniCore is v142 (VS2019) — well inside.
- No known x86/MSVC v14x gotchas at v3.15.0. (The v3.x line has had isolated workaround commits for older GCC versions; none affect MSVC x86.)
- v2.x single-header is still an option but is not the actively-maintained line; v3.x is the obvious choice for new vendor work.

**Files to vendor (from `https://github.com/catchorg/Catch2/releases/tag/v3.15.0`):**

- `catch_amalgamated.hpp` — single header, ~30K LOC
- `catch_amalgamated.cpp` — single source, compiled as one `<ClCompile>` item in `UtinniCore.Tests.vcxproj`

**Skip:** `.asc` PGP signatures. The project does not currently have a key-trust chain; sig verification is a Phase 6 STAB-03 polish item at best. Capture SHA-256 in the commit message as the lightweight tamper-detection layer.

**Catch2 v3 vs Catch2 v2 (CONTEXT.md asks researcher to confirm):**

Catch2 v3 wins on every axis the project cares about: actively maintained, amalgamated build is the v3-blessed delivery shape (CONTEXT.md D-02 names it explicitly), better diagnostics, JUnit reporter improvements. v2 is single-file (advantage) but the v3 amalgamated build is *also* single-file-pair — no meaningful header-only-ness gap.

## §D-03 vcxproj Scaffold Pattern

### Verbatim triple-config block from `UtinniCore.vcxproj` (the canonical triple-config example, lines 3-15):

```xml
<ItemGroup Label="ProjectConfigurations">
  <ProjectConfiguration Include="Debug|Win32">
    <Configuration>Debug</Configuration>
    <Platform>Win32</Platform>
  </ProjectConfiguration>
  <ProjectConfiguration Include="Release|Win32">
    <Configuration>Release</Configuration>
    <Platform>Win32</Platform>
  </ProjectConfiguration>
  <ProjectConfiguration Include="RelWithDbgInfo|Win32">
    <Configuration>RelWithDbgInfo</Configuration>
    <Platform>Win32</Platform>
  </ProjectConfiguration>
</ItemGroup>
```

### Verbatim Configuration property groups from `UtinniCore.vcxproj` (lines 24-45):

```xml
<PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
  <ConfigurationType>DynamicLibrary</ConfigurationType>
  <UseDebugLibraries>true</UseDebugLibraries>
  <PlatformToolset>v142</PlatformToolset>
  <CharacterSet>NotSet</CharacterSet>
</PropertyGroup>
<PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
  <ConfigurationType>DynamicLibrary</ConfigurationType>
  <UseDebugLibraries>false</UseDebugLibraries>
  <PlatformToolset>v142</PlatformToolset>
  <WholeProgramOptimization>false</WholeProgramOptimization>
  <CharacterSet>NotSet</CharacterSet>
  <CLRSupport>false</CLRSupport>
</PropertyGroup>
<PropertyGroup Condition="'$(Configuration)|$(Platform)'=='RelWithDbgInfo|Win32'" Label="Configuration">
  <ConfigurationType>DynamicLibrary</ConfigurationType>
  <UseDebugLibraries>false</UseDebugLibraries>
  <PlatformToolset>v142</PlatformToolset>
  <WholeProgramOptimization>false</WholeProgramOptimization>
  <CharacterSet>NotSet</CharacterSet>
  <CLRSupport>false</CLRSupport>
</PropertyGroup>
```

For the test exe, swap `<ConfigurationType>DynamicLibrary</ConfigurationType>` → `<ConfigurationType>Application</ConfigurationType>` (we're producing an .exe, not a .dll).

### Verbatim include-path + ClCompile + Link block from `Utinni.LoaderLockHarness.vcxproj` (the closest sibling precedent for a console-exe test target — lines 54-91):

```xml
<ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
  <ClCompile>
    <WarningLevel>Level3</WarningLevel>
    <SDLCheck>true</SDLCheck>
    <PreprocessorDefinitions>_DEBUG;_CONSOLE;%(PreprocessorDefinitions)</PreprocessorDefinitions>
    <ConformanceMode>true</ConformanceMode>
    <AdditionalIncludeDirectories>$(SolutionDir);$(SolutionDir)external;$(ProjectDir);%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
    <LanguageStandard>stdcpp17</LanguageStandard>
  </ClCompile>
  <Link>
    <SubSystem>Console</SubSystem>
    <GenerateDebugInformation>true</GenerateDebugInformation>
    <AdditionalDependencies>%(AdditionalDependencies)</AdditionalDependencies>
    <AdditionalLibraryDirectories>%(AdditionalLibraryDirectories)</AdditionalLibraryDirectories>
  </Link>
</ItemDefinitionGroup>
<ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
  <ClCompile>
    <WarningLevel>Level3</WarningLevel>
    <FunctionLevelLinking>true</FunctionLevelLinking>
    <IntrinsicFunctions>true</IntrinsicFunctions>
    <SDLCheck></SDLCheck>
    <PreprocessorDefinitions>NDEBUG;_CONSOLE;%(PreprocessorDefinitions)</PreprocessorDefinitions>
    <ConformanceMode>true</ConformanceMode>
    <LanguageStandard>stdcpp17</LanguageStandard>
    <AdditionalIncludeDirectories>$(SolutionDir);$(SolutionDir)external;$(ProjectDir);%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
    <MultiProcessorCompilation>true</MultiProcessorCompilation>
  </ClCompile>
  <Link>
    <SubSystem>Console</SubSystem>
    <EnableCOMDATFolding>true</EnableCOMDATFolding>
    <OptimizeReferences>true</OptimizeReferences>
    <GenerateDebugInformation>true</GenerateDebugInformation>
    <AdditionalDependencies>%(AdditionalDependencies)</AdditionalDependencies>
    <AdditionalLibraryDirectories>%(AdditionalLibraryDirectories)</AdditionalLibraryDirectories>
  </Link>
</ItemDefinitionGroup>
```

### Verbatim ProjectReference block from `Utinni.LoaderLockHarness.vcxproj` (lines 92-102):

```xml
<ItemGroup>
  <!-- Harness depends on UtinniCore so the CON-T-01 post-build chain fires first.
       Does NOT link against UtinniCore — it LoadLibrarys at runtime. -->
  <ProjectReference Include="..\UtinniCore\UtinniCore.vcxproj">
    <Project>{AEFED7F6-4BA9-44FC-A353-71A463A82FDE}</Project>
    <LinkLibraryDependencies>false</LinkLibraryDependencies>
  </ProjectReference>
</ItemGroup>
```

### Composite skeleton for `UtinniCore.Tests/UtinniCore.Tests.vcxproj`

**Drop-in template the planner can fill out.** Generate a fresh GUID for `ProjectGuid` (one option: `guidgen.exe` on Windows, or `[System.Guid]::NewGuid()` in PowerShell — `Get-Random` is NOT acceptable; this is a real GUID slot).

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup Label="ProjectConfigurations">
    <ProjectConfiguration Include="Debug|Win32">
      <Configuration>Debug</Configuration>
      <Platform>Win32</Platform>
    </ProjectConfiguration>
    <ProjectConfiguration Include="Release|Win32">
      <Configuration>Release</Configuration>
      <Platform>Win32</Platform>
    </ProjectConfiguration>
    <ProjectConfiguration Include="RelWithDbgInfo|Win32">
      <Configuration>RelWithDbgInfo</Configuration>
      <Platform>Win32</Platform>
    </ProjectConfiguration>
  </ItemGroup>
  <PropertyGroup Label="Globals">
    <VCProjectVersion>16.0</VCProjectVersion>
    <ProjectGuid>{REPLACE-WITH-FRESH-GUID}</ProjectGuid>
    <RootNamespace>UtinniCore.Tests</RootNamespace>
    <WindowsTargetPlatformVersion>10.0</WindowsTargetPlatformVersion>
  </PropertyGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'" Label="Configuration">
    <ConfigurationType>Application</ConfigurationType>
    <UseDebugLibraries>true</UseDebugLibraries>
    <PlatformToolset>v142</PlatformToolset>
    <CharacterSet>NotSet</CharacterSet>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'" Label="Configuration">
    <ConfigurationType>Application</ConfigurationType>
    <UseDebugLibraries>false</UseDebugLibraries>
    <PlatformToolset>v142</PlatformToolset>
    <WholeProgramOptimization>false</WholeProgramOptimization>
    <CharacterSet>NotSet</CharacterSet>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='RelWithDbgInfo|Win32'" Label="Configuration">
    <ConfigurationType>Application</ConfigurationType>
    <UseDebugLibraries>false</UseDebugLibraries>
    <PlatformToolset>v142</PlatformToolset>
    <WholeProgramOptimization>false</WholeProgramOptimization>
    <CharacterSet>NotSet</CharacterSet>
  </PropertyGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />
  <ImportGroup Label="ExtensionSettings" />
  <ImportGroup Label="Shared" />
  <ImportGroup Label="PropertySheets" Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
    <Import Project="$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props" Condition="exists('$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props')" Label="LocalAppDataPlatform" />
  </ImportGroup>
  <ImportGroup Label="PropertySheets" Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
    <Import Project="$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props" Condition="exists('$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props')" Label="LocalAppDataPlatform" />
  </ImportGroup>
  <ImportGroup Label="PropertySheets" Condition="'$(Configuration)|$(Platform)'=='RelWithDbgInfo|Win32'">
    <Import Project="$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props" Condition="exists('$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props')" Label="LocalAppDataPlatform" />
  </ImportGroup>
  <PropertyGroup Label="UserMacros" />
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
    <LinkIncremental>true</LinkIncremental>
    <OutDir>$(SolutionDir)bin\$(Configuration)\</OutDir>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
    <LinkIncremental>false</LinkIncremental>
    <OutDir>$(SolutionDir)bin\$(Configuration)\</OutDir>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='RelWithDbgInfo|Win32'">
    <LinkIncremental>false</LinkIncremental>
    <OutDir>$(SolutionDir)bin\$(Configuration)\</OutDir>
  </PropertyGroup>
  <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|Win32'">
    <ClCompile>
      <WarningLevel>Level3</WarningLevel>
      <SDLCheck>true</SDLCheck>
      <PreprocessorDefinitions>_DEBUG;_CONSOLE;%(PreprocessorDefinitions)</PreprocessorDefinitions>
      <ConformanceMode>true</ConformanceMode>
      <AdditionalIncludeDirectories>$(SolutionDir);$(SolutionDir)external;$(SolutionDir)UtinniCore;$(ProjectDir);%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
      <LanguageStandard>stdcpp17</LanguageStandard>
    </ClCompile>
    <Link>
      <SubSystem>Console</SubSystem>
      <GenerateDebugInformation>true</GenerateDebugInformation>
    </Link>
  </ItemDefinitionGroup>
  <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|Win32'">
    <ClCompile>
      <WarningLevel>Level3</WarningLevel>
      <FunctionLevelLinking>true</FunctionLevelLinking>
      <IntrinsicFunctions>true</IntrinsicFunctions>
      <PreprocessorDefinitions>NDEBUG;_CONSOLE;%(PreprocessorDefinitions)</PreprocessorDefinitions>
      <ConformanceMode>true</ConformanceMode>
      <LanguageStandard>stdcpp17</LanguageStandard>
      <AdditionalIncludeDirectories>$(SolutionDir);$(SolutionDir)external;$(SolutionDir)UtinniCore;$(ProjectDir);%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
      <MultiProcessorCompilation>true</MultiProcessorCompilation>
    </ClCompile>
    <Link>
      <SubSystem>Console</SubSystem>
      <EnableCOMDATFolding>true</EnableCOMDATFolding>
      <OptimizeReferences>true</OptimizeReferences>
      <GenerateDebugInformation>true</GenerateDebugInformation>
    </Link>
  </ItemDefinitionGroup>
  <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='RelWithDbgInfo|Win32'">
    <ClCompile>
      <WarningLevel>Level3</WarningLevel>
      <FunctionLevelLinking>true</FunctionLevelLinking>
      <IntrinsicFunctions>false</IntrinsicFunctions>
      <PreprocessorDefinitions>NDEBUG;_CONSOLE;%(PreprocessorDefinitions)</PreprocessorDefinitions>
      <ConformanceMode>true</ConformanceMode>
      <LanguageStandard>stdcpp17</LanguageStandard>
      <AdditionalIncludeDirectories>$(SolutionDir);$(SolutionDir)external;$(SolutionDir)UtinniCore;$(ProjectDir);%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
      <DebugInformationFormat>ProgramDatabase</DebugInformationFormat>
      <MultiProcessorCompilation>true</MultiProcessorCompilation>
      <Optimization>Disabled</Optimization>
    </ClCompile>
    <Link>
      <SubSystem>Console</SubSystem>
      <EnableCOMDATFolding>true</EnableCOMDATFolding>
      <OptimizeReferences>true</OptimizeReferences>
      <GenerateDebugInformation>true</GenerateDebugInformation>
    </Link>
  </ItemDefinitionGroup>
  <ItemGroup>
    <!-- Vendored Catch2 amalgamated build (D-02 - v3.15.0). -->
    <ClCompile Include="..\external\catch2\catch_amalgamated.cpp" />
    <!-- Production source compiled directly into the test exe (no link against UtinniCore.dll).
         string_utility.cpp has only one out-of-line function (toString(std::wstring&)) that's
         not exercised by the seed; the rest is header-only inline. -->
    <ClCompile Include="..\UtinniCore\utility\string_utility.cpp" />
    <!-- Test sources (D-05 plan boundaries):
         05-01 scaffold smoke -->
    <ClCompile Include="main_smoke.cpp" />
    <!-- 05-02 seed coverage -->
    <ClCompile Include="StringUtilityTests.cpp" />
  </ItemGroup>
  <ItemGroup>
    <ClInclude Include="..\external\catch2\catch_amalgamated.hpp" />
  </ItemGroup>
  <ItemGroup>
    <!-- Build-order dependency on UtinniCore (CON-T-01 post-build chain fires first).
         Does NOT link against UtinniCore.dll - the seed seed compiles string_utility.cpp directly above. -->
    <ProjectReference Include="..\UtinniCore\UtinniCore.vcxproj">
      <Project>{AEFED7F6-4BA9-44FC-A353-71A463A82FDE}</Project>
      <LinkLibraryDependencies>false</LinkLibraryDependencies>
    </ProjectReference>
  </ItemGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
  <ImportGroup Label="ExtensionTargets" />
</Project>
```

### `Utinni.sln` registration block

The planner adds **two** sections to `Utinni.sln`:

**1. Project entry (place after the existing `Utinni.LegacyPlugin` entry, before `Utinni.Cli`):**

```
Project("{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}") = "UtinniCore.Tests", "UtinniCore.Tests\UtinniCore.Tests.vcxproj", "{REPLACE-WITH-FRESH-GUID}"
	ProjectSection(ProjectDependencies) = postProject
		{AEFED7F6-4BA9-44FC-A353-71A463A82FDE} = {AEFED7F6-4BA9-44FC-A353-71A463A82FDE}
	EndProjectSection
EndProject
```

(The `{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}` prefix is the well-known Visual Studio "C++ project type" GUID — same as all the other vcxproj entries. Verified by reading `Utinni.sln:6, 12, 40, 45, 50, 65, 67`.)

**2. ProjectConfigurationPlatforms entries (add to the existing block, after the LegacyPlugin entries at line 111-116):**

```
{REPLACE-WITH-FRESH-GUID}.Debug|x86.ActiveCfg = Debug|Win32
{REPLACE-WITH-FRESH-GUID}.Debug|x86.Build.0 = Debug|Win32
{REPLACE-WITH-FRESH-GUID}.Release|x86.ActiveCfg = Release|Win32
{REPLACE-WITH-FRESH-GUID}.Release|x86.Build.0 = Release|Win32
{REPLACE-WITH-FRESH-GUID}.RelWithDbgInfo|x86.ActiveCfg = RelWithDbgInfo|Win32
{REPLACE-WITH-FRESH-GUID}.RelWithDbgInfo|x86.Build.0 = RelWithDbgInfo|Win32
```

**Critical:** the `RelWithDbgInfo|x86.ActiveCfg` mapping is `RelWithDbgInfo|Win32`, NOT `Release|Win32` (which is what LoaderLockHarness uses — see sln line 104). This honors CONTEXT.md D-07 (CON-T-02 triple-config preservation). The LoaderLockHarness mapping was an oversight that's fine for the harness (it's a build-time-only diagnostic) but not for a test target that gates `master`.

### GUID Generation Convention

`Utinni.sln` GUIDs are random UUIDs (verified by inspection — no deterministic pattern visible across the 11 project GUIDs in the file). Use either:

- PowerShell: `[System.Guid]::NewGuid().ToString().ToUpper()`
- VS: Tools → Create GUID → "Registry Format"
- Online tools — avoid; commit a fresh local-generated GUID.

The well-known type-prefix GUIDs are:

- `{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}` — C++ project type (Visual Studio convention)
- `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}` — C# project type

The PROJECT-instance GUID (`{REPLACE-WITH-FRESH-GUID}` above) is the per-project UUID and must be unique.

## §D-04 CI Lane Shape

### Verbatim existing Phase 4 step from `.github/workflows/ci.yml` (lines 94-108):

```yaml
- name: Run CLI golden tests (net472 / x86)
  run: dotnet test Utinni.Cli.Tests/Utinni.Cli.Tests.csproj --no-build --configuration Release --logger "console;verbosity=normal" --logger "trx;LogFileName=cli-test-results.trx"
  # Phase 4 D-11: second test lane gates `master` on the CLI golden suite.
  # Project-targeted form per RESEARCH §Pitfall 2; --no-build because msbuild above already produced bin/Release/net472/Utinni.Cli.Tests.dll.

- name: Upload CLI test artifacts (on failure)
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: cli-test-results
    path: |
      Utinni.Cli.Tests/TestResults/cli-test-results.trx
      Utinni.Cli.Tests/bin/Release/net472/TestResults/**/*.json
      Utinni.Cli.Tests/bin/Release/net472/TestResults/**/*.txt
    if-no-files-found: warn
```

### Proposed new Phase 5 step (insert after the CLI lane, immediately before the file end):

```yaml
- name: Run native unit tests (UtinniCore.Tests.exe)
  shell: pwsh
  run: |
    # Phase 5 D-04: third CI lane gates `master` on the native test suite.
    # Direct exe invocation (not `dotnet test`) because the runner is Catch2 self-runner.
    # Stacked --reporter flags: console sets the exit code on failure (RESEARCH §Pitfall 5);
    # junit writes XML for triage. Output dir is created implicitly by Catch2 v3.
    & bin\Release\UtinniCore.Tests.exe `
      --reporter console `
      --reporter "junit::out=UtinniCore.Tests\TestResults\junit-results.xml"

- name: Upload native test artifacts (on failure)
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: native-test-results
    path: UtinniCore.Tests/TestResults/junit-results.xml
    if-no-files-found: warn
```

### CI builds

The workflow currently builds **only Release|x86** (`.github/workflows/ci.yml:78` — `/p:Configuration=Release /p:Platform=x86`). It does NOT build Debug or RelWithDbgInfo in CI. The Phase 5 lane should match — invoke `bin\Release\UtinniCore.Tests.exe`.

**The new test vcxproj must still declare all three configs (CON-T-02 / D-07)** because developers running `msbuild Utinni.sln /p:Configuration=Debug` locally must see the test target build cleanly under Debug + RelWithDbgInfo as well. The CI lane validates only Release; the triple-config posture is validated locally + at release-time.

### Naming match with Phase 4

Phase 4 uses `cli-test-results.trx` + artifact name `cli-test-results`. Phase 5 mirrors with `junit-results.xml` + artifact name `native-test-results`. This is intentional — the file extensions differ (`.trx` is MSTest format, `.xml` is JUnit) so the names don't collide even if both lanes fail in the same run.

## §D-05 Smoke-Test Recommendations for 05-01

The scaffold plan (05-01) should land **3 smoke tests** that, collectively, prove:

1. **The amalgamated build compiled and linked** (any TEST_CASE running proves this — REQUIRE(1+1==2) is enough).
2. **Exception machinery works end-to-end** (`REQUIRE_THROWS_AS` exercises the `try/catch` plumbing inside Catch2; CONTEXT.md "Specific Ideas" explicitly calls this out).
3. **SECTION re-entry produces fresh state** (this is the BDD-runner sanity check — if it fails, every multi-section seed test in 05-02 is unreliable).

These three tests are in the §"Code Examples > Standalone smoke test (05-01)" block above.

### Tag Taxonomy

| Tag | Purpose | Files using it |
|-----|---------|---------------|
| `[smoke]` | Scaffold proof. Tests that exercise Catch2 itself, not project code. | `main_smoke.cpp` (05-01) |
| `[utility]` | Coverage of `UtinniCore/utility/*` modules. | `StringUtilityTests.cpp` (05-02); future native tests of `utility/memory`, `utility/utility` would also use this |
| `[string]` | Subtag for `[utility]` — string-related coverage. | `StringUtilityTests.cpp` (05-02) |
| `[plugin_framework]` | (Reserved — would be used by an optional 05-03 LogSubscribe test or future PluginManager native re-cover; not needed for V1.) | n/a |
| `[log]` | (Reserved — would be used by an optional 05-03 LogSubscribe test.) | n/a |

**Tag combination rule:** Each TEST_CASE gets one primary functional tag (`[smoke]`, `[utility]`, `[plugin_framework]`, etc.) plus zero or more subtype tags (`[string]`, `[memory]`, `[log]`). Catch2 supports both single-tag and combined-tag filtering — `--test-spec "[utility]&[string]"` runs only stringUtility tests.

### Smoke-test acceptance criteria (planner uses this in 05-01-PLAN.md)

- ✅ `UtinniCore.Tests.exe` builds in all three configs (Debug, Release, RelWithDbgInfo).
- ✅ Running the exe with no args prints `All tests passed (N assertions in 3 test cases)`.
- ✅ Running with `--reporter junit::out=test-results.xml` produces a parseable XML file.
- ✅ Running with `--test-spec "[smoke]"` runs all 3 smoke tests; runs zero others.
- ✅ Exit code 0 on green, 1 on red (deliberately landed red test on a throwaway branch to verify).
- ✅ New CI lane is green on `master` for **two consecutive pushes** (CONTEXT.md D-04 soak window).

## §Open questions for the planner

The planner has very few open decisions to make — CONTEXT.md was thorough. The remaining choices are:

1. **GUID generation for `UtinniCore.Tests.vcxproj`** — one fresh UUID; generate with PowerShell `[System.Guid]::NewGuid()` at scaffold time.

2. **Whether to add a `<ProjectReference>` on `UtinniCore.vcxproj`** — recommendation **yes** (with `<LinkLibraryDependencies>false</LinkLibraryDependencies>`), to preserve build ordering for the CON-T-01 post-build chain. Mirrors LoaderLockHarness precedent.

3. **Whether to split 05-02 into 05-02 (scaffold the file + tests for `toBool` + `toString`) and 05-03 (extend to `toHexString` + `trim` + the PluginManager idiom round-trip)** — **no, keep 05-02 as a single plan.** 6 tests in 1 file is appropriate atomic-commit granularity. Splitting would introduce ceremony with no value.

4. **Optional 05-03 (LogSubscribe / R-A handle registry tests)** — **defer to Phase 6 STAB-03.** Phase 5 ships with 1 scaffold plan + 1 seed plan. The second seed (R-A) is nice-to-have, not phase-critical, and the planner should resist scope creep. CONTEXT.md D-05 explicitly allows the planner to add 05-03 "if the seed proposal naturally splits" — stringUtility does NOT naturally split.

5. **Whether to ship a `.gitattributes` line for `external/catch2/catch_amalgamated.{hpp,cpp}` marking them as vendored** — out of scope; the existing `external/` doesn't have one. Phase 6 STAB-03 cleanup at best.

## §Out-of-scope confirmations

The researcher confirms the following items were **not** investigated, per CONTEXT.md "Out of scope" and the user's `<additional_context>` direction:

- **vcpkg as alternative dependency manager** — Deferred to Phase 6 STAB-03 per CONTEXT.md D-02. Not investigated.
- **CMake migration / ctest wiring** — D-03 explicitly chose MSBuild + direct-exe. Not investigated.
- **VS Test Adapter for Catch2 (vstest adapter)** — D-03 alt-rejected. Not investigated.
- **Refactoring `swg/*` modules for testability** — out of scope; no seed candidate required it. Not investigated.
- **Coverage tooling (OpenCover / coverlet / Codecov)** — out of scope per CONTEXT.md "Deferred Ideas". Not investigated.
- **Tier 3 (recorded-fixture + mock-D3D9)** — deferred to V2. Not investigated.
- **Tier 4 (live-SWG-injection)** — Phase 6 TEST-04. Not investigated.
- **Adding tests to existing managed test projects** — Phase 4 ground; out of scope. Not investigated.
- **Cross-repo touch to UtinniPlugins** — Phase 5 is fully Utinni-side. Not investigated.
- **`.clang-format` / `.editorconfig` for the new test files** — out of scope; phase respects the existing zero-config posture. New files get the standard 23-line MIT header per CONVENTIONS.md.

## Sources

### Primary (HIGH confidence)

- **`D:\Code\Utinni\.planning\phases\05-tier-1-c-unit-tests\05-CONTEXT.md`** — 7 locked decisions, in-scope/out-of-scope, reusable assets.
- **`D:\Code\Utinni\.planning\REQUIREMENTS.md`** — §TEST-02 (Tier 1 C++ unit-test scaffold).
- **`D:\Code\Utinni\.planning\PROJECT.md`** — DEC-C3 (LOCKED tiered testing strategy).
- **`D:\Code\Utinni\.planning\ROADMAP.md`** — §"Phase 5".
- **`D:\Code\Utinni\.planning\phases\04-tier-2-cli-shim-golden-fixtures\04-CONTEXT.md`** — D-06 (parsers managed) + D-11 (CI lane extension pattern).
- **`D:\Code\Utinni\.planning\codebase\TESTING.md`** — "Recommended First Tests" §5 explicitly names `string_utility.h` as the C++ seed candidate.
- **`D:\Code\Utinni\.planning\codebase\STRUCTURE.md`** + **`STACK.md`** + **`CONVENTIONS.md`** — toolchain, conventions, x86/v142 pin.
- **`D:\Code\Utinni\docs\ai\test-harness-plan.md`** — Tier 1 C++ row, motivating doc.
- **`D:\Code\Utinni\UtinniCore\utility\string_utility.h`** (read in full) — confirms inline pure helpers, no global state, no RVA dependency.
- **`D:\Code\Utinni\UtinniCore\utility\string_utility.cpp`** — confirms the one out-of-line function (`toString(std::wstring&)`) calls only `WideCharToMultiByte`, also pure.
- **`D:\Code\Utinni\UtinniCore\utility\memory.h`** + **`.cpp`** — confirms `memory::*` is RVA-bound + VirtualProtect-bound, unsuitable for isolated testing.
- **`D:\Code\Utinni\UtinniCore\utility\log.h`** + **`.cpp`** — confirms R-A registry is testable but heavier than stringUtility (needs `create()` setup).
- **`D:\Code\Utinni\UtinniCore\plugin_framework\plugin_manager.h`** + **`.cpp`** — confirms `test_internal::test_loadFromDirectory` is already exercised by managed Tier-1 tests via `utinni_test_pluginManager*` exports.
- **`D:\Code\Utinni\UtinniCore\test_exports.cpp`** (lines 497-530) — confirms PluginManager native re-cover would be ceremonial.
- **`D:\Code\Utinni\Utinni.LoaderLockHarness\Utinni.LoaderLockHarness.vcxproj`** (read in full) — canonical sibling console-exe precedent for triple-config + ProjectReference + LinkLibraryDependencies pattern.
- **`D:\Code\Utinni\Utinni.LoaderLockHarness\main.cpp`** — canonical native-harness-exe pattern.
- **`D:\Code\Utinni\UtinniCore\UtinniCore.vcxproj`** (lines 3-160) — canonical triple-config block + DXSDK include path + post-build chain.
- **`D:\Code\Utinni\Utinni.sln`** (read in full) — project entry conventions, GUID format, ProjectConfigurationPlatforms mapping (LoaderLockHarness's `RelWithDbgInfo→Release` mapping flagged as the precedent NOT to follow).
- **`D:\Code\Utinni\.github\workflows\ci.yml`** (read in full) — Phase 4 D-11 CI lane structure to mirror.
- **GitHub Releases API for catchorg/Catch2** (`https://api.github.com/repos/catchorg/Catch2/releases/latest`) — verified Catch2 v3.15.0, 2026-05-12.

### Secondary (MEDIUM confidence)

- **`https://github.com/catchorg/Catch2/blob/v3.15.0/docs/release-notes.md`** — confirms C++14 minimum, VS2017+ support, GCC 5/6 support restoration in v3.5.4 (irrelevant for this project but confirms ongoing maintenance posture).

### Tertiary (LOW confidence)

- None — every claim was verified against a primary or secondary source.

## Metadata

**Confidence breakdown:**

- D-01 seed candidate selection: **HIGH** — all candidates read in full; failure modes traced through `plugin_manager.cpp` consumer code; the dropped alternatives (`memory`, `PluginManager` re-cover, `swg/*`) have explicit grounds for skip.
- D-02 Catch2 tag: **HIGH** — verified live via GitHub releases API at research time.
- D-03 vcxproj scaffold: **HIGH** — verbatim quotes from `UtinniCore.vcxproj` (triple-config canon) + `Utinni.LoaderLockHarness.vcxproj` (console-exe sibling canon).
- D-04 CI lane: **HIGH** — verbatim quote from `.github/workflows/ci.yml:94-108` (Phase 4 D-11 precedent).
- D-05 smoke set + tag taxonomy: **HIGH** — derived from Catch2 v3 docs + CONTEXT.md "Specific Ideas" explicit guidance.

**Research date:** 2026-05-23

**Valid until:** 2026-06-22 (30 days for stable native-test scaffolding; revisit if Catch2 ships v3.16.x or if Phase 6 vcpkg work pre-empts the vendored Catch2 path).

---

*Phase: 05-tier-1-c-unit-tests*
*Research synthesized: 2026-05-23*
