# Phase 5: Tier 1 C++ unit tests - Pattern Map

**Mapped:** 2026-05-23
**Files analyzed:** 8 (5 to create + 3 to modify)
**Analogs found:** 7 / 8 (1 file is greenfield — no in-repo C++ test analog exists)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `external/catch2/catch_amalgamated.hpp` | vendor drop (header) | n/a (static asset) | `external/spdlog/spdlog.h` + top-level layout | flat-vendor layout match |
| `external/catch2/catch_amalgamated.cpp` | vendor drop (TU) | n/a (static asset) | `external/imgui/imgui.cpp` (precedent for vendored `.cpp` shipped in `external/`) | role-match |
| `UtinniCore.Tests/UtinniCore.Tests.vcxproj` | MSBuild test scaffold (Application/Console exe, triple-config) | request-response (CI invokes exe → exit code) | `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj` (sibling exe) **+** `UtinniCore/UtinniCore.vcxproj` (canonical triple-config block) | composite — see "Notable difference" below |
| `UtinniCore.Tests/main_smoke.cpp` | C++ test file (vendor sanity) | n/a (Catch2 runs in-process) | **greenfield** — no existing native test file in repo | follow RESEARCH.md §"Standalone smoke test (05-01)" verbatim |
| `UtinniCore.Tests/StringUtilityTests.cpp` | C++ test file (seed coverage) | n/a | **greenfield** — no existing native test file in repo | follow RESEARCH.md §"Seed coverage (05-02)" verbatim |
| `Utinni.sln` (modify) | solution registration | n/a (build graph) | existing `Utinni.LoaderLockHarness` entry (lines 40-44) + UtinniCore entry (lines 12-16) for triple-config postSolution mappings | exact — but flag the LoaderLockHarness `RelWithDbgInfo→Release` mis-mapping as the precedent NOT to copy |
| `.github/workflows/ci.yml` (modify) | CI lane addition | request-response (CI step invokes exe) | existing "Run CLI golden tests" + "Upload CLI test artifacts" steps (lines 94-108) | exact — third lane mirroring Phase 4 D-11 pattern |
| `docs/ai/test-harness-plan.md` (modify) | docs status update | n/a | existing "Tier 1 — Pure unit tests" section (lines 23-33) | exact — bullet replacement |

## Pattern Assignments

### `external/catch2/catch_amalgamated.hpp` + `.cpp` (vendor drop, flat layout)

**Analog:** `external/imgui/` — closest precedent for a vendored library that ships a `.cpp` (not just headers) in `external/`. `external/spdlog/` is header-only-inline so doesn't drop a `.cpp`, but its **flat top-level layout** (single dir + sub-trees like `cfg/`, `details/`, `sinks/`) matches what Catch2 amalgamated needs (flat, just 2 files).

**Directory tree pattern from `external/imgui/`** (Glob-confirmed, top-level only):

```
external/imgui/
  imgui.cpp                  # main vendored TU (compiled by consumer projects)
  imgui.h                    # main vendored header
  imgui_demo.cpp
  imgui_draw.cpp
  imgui_impl_dx9.cpp
  imgui_impl_dx9.h
  imgui_impl_win32.cpp
  imgui_impl_win32.h
  imgui_internal.h
  imgui_user.cpp
  imgui_user.h
  imgui_widgets.cpp
  imstb_rectpack.h
  imstb_textedit.h
  imstb_truetype.h
  imconfig.h
  LICENSE.txt
  imgui.vcxproj              # imgui has its own vcxproj (Catch2 will NOT — compiled in-place)
  imgui.vcxproj.user
  lib/imgui.lib
  .editorconfig
```

**Directory tree pattern from `external/spdlog/`** (Glob-confirmed; 94 files total; structurally header-only):

```
external/spdlog/
  LICENSE
  spdlog.h                   # main header (others are header-only-inline siblings)
  spdlog-inl.h
  async.h
  async_logger.h
  async_logger-inl.h
  common.h
  common-inl.h
  formatter.h
  fwd.h
  logger.h
  logger-inl.h
  pattern_formatter.h
  pattern_formatter-inl.h
  tweakme.h
  version.h
  cfg/                       # sub-trees by concern
  details/
  fmt/
  sinks/
```

**Recommended Phase 5 layout for `external/catch2/`** (mirrors spdlog's flat-with-LICENSE pattern; imgui adds a vcxproj that Catch2 does NOT need since the test project compiles `catch_amalgamated.cpp` directly):

```
external/catch2/
  catch_amalgamated.hpp      # v3.15.0
  catch_amalgamated.cpp      # v3.15.0
  LICENSE.txt                # Catch2 BSL-1.0 license — recommended polish (spdlog and imgui both ship one)
```

**Notable difference the planner must apply:**
- Do NOT add a Catch2 vcxproj. Unlike `external/imgui/imgui.vcxproj`, Catch2 amalgamated is compiled as a single `<ClCompile>` item inside `UtinniCore.Tests.vcxproj` (per RESEARCH.md line 885: `<ClCompile Include="..\external\catch2\catch_amalgamated.cpp" />`). Don't replicate imgui's "external lib as its own project" pattern.
- Catch2 ships a `LICENSE.txt` at the v3.15.0 release; vendor it alongside the amalgamated files (BSL-1.0). Not strictly required (the `external/imgui/LICENSE.txt` and `external/spdlog/LICENSE` precedents make it conventional).

---

### `UtinniCore.Tests/UtinniCore.Tests.vcxproj` (MSBuild test scaffold, Application, triple-config)

**Composite analog:** `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj` is the closest sibling **shape** (console exe, sibling to UtinniCore, ProjectReference with `LinkLibraryDependencies=false`), but it is **missing the third config (`RelWithDbgInfo|Win32`)** — that's exactly the load-bearing gap Phase 5 has to close per CON-T-02 / D-07.

For the triple-config block itself, graft from `UtinniCore/UtinniCore.vcxproj` (the canonical triple-config example RESEARCH.md §D-03 quotes from). Swap `<ConfigurationType>DynamicLibrary</ConfigurationType>` → `<ConfigurationType>Application</ConfigurationType>`.

**Verbatim 2-config block from `Utinni.LoaderLockHarness.vcxproj` (lines 3-12 — DEFICIENT, missing third config):**

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
</ItemGroup>
```

**Notable difference the planner must apply:** Add the third `<ProjectConfiguration Include="RelWithDbgInfo|Win32">` block. The exhaustive composite skeleton (filled in by the researcher) is already in `05-RESEARCH.md` lines 762-909 — the planner should copy that template verbatim and replace `{REPLACE-WITH-FRESH-GUID}` with a PowerShell-generated UUID.

**Verbatim `Application`-type console ItemDefinitionGroup pattern from `Utinni.LoaderLockHarness.vcxproj` (lines 54-91)** — load-bearing because this is the only existing Application-type vcxproj in the solution; the others are DLLs:

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

**Notable differences the planner must apply for the test exe:**
1. Add a third `<ItemDefinitionGroup>` for `RelWithDbgInfo|Win32` (RESEARCH.md lines 863-882 has the template — DebugInformationFormat=ProgramDatabase, Optimization=Disabled, IntrinsicFunctions=false).
2. Extend `AdditionalIncludeDirectories` with `$(SolutionDir)UtinniCore;` so the tests can `#include "utility/string_utility.h"` directly. LoaderLockHarness does not include this path (it `LoadLibrary`s UtinniCore at runtime; the test exe `#include`s the headers at compile time).
3. The `WholeProgramOptimization` setting in LoaderLockHarness Release|Win32 (line 30) is `true`. RESEARCH.md §D-03 sets it to `false` for the test vcxproj — match UtinniCore.vcxproj's `false` (line 138) rather than LoaderLockHarness's `true`, because the amalgamated build of Catch2 (~30K LOC) has measurable LTCG cost and no production runtime to benefit.

**Verbatim ProjectReference pattern from `Utinni.LoaderLockHarness.vcxproj` (lines 92-102) — copy verbatim:**

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

**Notable difference:** Reword the comment for the test exe context — the test exe does NOT `LoadLibrary` UtinniCore; it compiles `string_utility.cpp` directly into itself. Suggested comment: `<!-- Build-order dependency on UtinniCore (CON-T-01 post-build chain fires first). Does NOT link — string_utility.cpp is compiled directly into the test exe via the ClCompile item above. -->`

---

### `UtinniCore.Tests/main_smoke.cpp` (test file, vendor sanity — 05-01 scope)

**Analog:** **none in repo** — this is the first native test file in the project. Follow RESEARCH.md §"Standalone smoke test (05-01)" (lines 307-343) directly.

**Imports pattern (from RESEARCH.md §Code Examples, line 313):**

```cpp
// MIT header (23-line block per CONVENTIONS.md) omitted for brevity.

#include <catch2/catch_amalgamated.hpp>
```

**Core pattern — three TEST_CASEs proving (a) build OK, (b) exception machinery, (c) SECTION re-entry (from RESEARCH.md lines 315-342):**

```cpp
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

**Notable patterns to apply:**
- Add the 23-line MIT header per CONVENTIONS.md (other native files in the repo all carry it).
- The `[smoke]` tag is reserved per RESEARCH.md §"Tag Taxonomy" line 1024 for scaffold-proof tests.
- No `#include <stdexcept>` needed even though `std::runtime_error` is used — `catch_amalgamated.hpp` transitively includes `<stdexcept>`. Leaving the explicit include in is also fine.

---

### `UtinniCore.Tests/StringUtilityTests.cpp` (test file, seed coverage — 05-02 scope)

**Analog:** **none in repo** — greenfield. Follow RESEARCH.md §"Seed coverage (05-02)" (lines 346-422) directly.

**Imports pattern (from RESEARCH.md §Code Examples, lines 353-354):**

```cpp
#include <catch2/catch_amalgamated.hpp>
#include "utility/string_utility.h"
```

**Core pattern — six TEST_CASEs across `toBool` / `toString(int, fillCount)` / `toHexString` / `trim` variants (RESEARCH.md lines 356-421):**

```cpp
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
    SECTION("trim strips both ends") { /* ... */ }
    SECTION("trimStart strips only leading") { /* ... */ }
    SECTION("trimEnd strips only trailing") { /* ... */ }
    SECTION("PluginManager [Plugins] line idiom: trim then toBool") { /* ... */ }
}
```

**Notable patterns to apply:**
- Two tags per case: `[utility]` + `[string]` (RESEARCH.md §"Tag Taxonomy" line 1024 — cheap forward-compat).
- D-06 max-harness requirement: each TEST_CASE must catch a failure mode that would silently corrupt `PluginManager::loadPlugins`. RESEARCH.md lines 425-432 list the four specific reversion-scenarios each test catches; the planner should preserve those in the test-source comments so reviewers can audit them.
- The "PluginManager [Plugins] line idiom" SECTION explicitly reproduces the `toBool(trim(isEnabled))` and `trim(dirName)` calls from `plugin_manager.cpp:121-138` — this is the load-bearing round-trip test.

---

### `Utinni.sln` (modify — register new project + triple-config postSolution mappings)

**Analog:** existing `Utinni.LoaderLockHarness` entry at `Utinni.sln:40-44` and `UtinniCore` entry at `Utinni.sln:12-16`. The well-known C++ project type GUID `{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}` prefixes both.

**Verbatim project entry pattern from `Utinni.sln:40-44`:**

```
Project("{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}") = "Utinni.LoaderLockHarness", "Utinni.LoaderLockHarness\Utinni.LoaderLockHarness.vcxproj", "{6A57E74E-0D39-430B-AD0D-6E56AA99B54A}"
	ProjectSection(ProjectDependencies) = postProject
		{AEFED7F6-4BA9-44FC-A353-71A463A82FDE} = {AEFED7F6-4BA9-44FC-A353-71A463A82FDE}
	EndProjectSection
EndProject
```

**Notable difference the planner must apply:** Replace project name (`UtinniCore.Tests`), path (`UtinniCore.Tests\UtinniCore.Tests.vcxproj`), and the new project's GUID (PowerShell-generated). Keep the `{AEFED7F6-4BA9-44FC-A353-71A463A82FDE}` UtinniCore dependency line — same as LoaderLockHarness, mirrors the ProjectReference in the new vcxproj.

**Verbatim ProjectConfigurationPlatforms entries — TWO PRECEDENTS, OPPOSING:**

`UtinniCore.vcxproj` mapping (lines 82-87) — **the precedent to FOLLOW** (full triple-config, no collapse):

```
{AEFED7F6-4BA9-44FC-A353-71A463A82FDE}.Debug|x86.ActiveCfg = Debug|Win32
{AEFED7F6-4BA9-44FC-A353-71A463A82FDE}.Debug|x86.Build.0 = Debug|Win32
{AEFED7F6-4BA9-44FC-A353-71A463A82FDE}.Release|x86.ActiveCfg = Release|Win32
{AEFED7F6-4BA9-44FC-A353-71A463A82FDE}.Release|x86.Build.0 = Release|Win32
{AEFED7F6-4BA9-44FC-A353-71A463A82FDE}.RelWithDbgInfo|x86.ActiveCfg = RelWithDbgInfo|Win32
{AEFED7F6-4BA9-44FC-A353-71A463A82FDE}.RelWithDbgInfo|x86.Build.0 = RelWithDbgInfo|Win32
```

`Utinni.LoaderLockHarness` mapping (lines 100-104) — **the precedent NOT to follow** (only 5 lines; collapses RelWithDbgInfo → Release; no `.Build.0` for the third config):

```
{6A57E74E-0D39-430B-AD0D-6E56AA99B54A}.Debug|x86.ActiveCfg = Debug|Win32
{6A57E74E-0D39-430B-AD0D-6E56AA99B54A}.Debug|x86.Build.0 = Debug|Win32
{6A57E74E-0D39-430B-AD0D-6E56AA99B54A}.Release|x86.ActiveCfg = Release|Win32
{6A57E74E-0D39-430B-AD0D-6E56AA99B54A}.Release|x86.Build.0 = Release|Win32
{6A57E74E-0D39-430B-AD0D-6E56AA99B54A}.RelWithDbgInfo|x86.ActiveCfg = Release|Win32
```

**Notable difference the planner must apply:** Mirror the **UtinniCore** mapping (6 lines including a real `RelWithDbgInfo|Win32` `.Build.0`), NOT the LoaderLockHarness mapping. This is the load-bearing CON-T-02 gap RESEARCH.md §D-03 line 939 flagged. The other native projects that ALSO mis-collapse (`Utinni.CrtMatchPlugin` lines 109-110, `Utinni.LegacyPlugin` lines 115-116) are inherited from the same one-week-Phase-2 pattern — don't copy them either.

**Insertion location:** RESEARCH.md §D-03 line 919 recommends "after the existing Utinni.LegacyPlugin entry, before Utinni.Cli." Confirmed against sln line numbers: LegacyPlugin ends at line 54, Utinni.Cli starts at line 55. Insert between.

---

### `.github/workflows/ci.yml` (modify — third CI lane)

**Analog:** existing Phase 4 D-11 "Run CLI golden tests" + "Upload CLI test artifacts" pair at lines 94-108.

**Verbatim Phase 4 step pair from `ci.yml:94-108`:**

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

**New Phase 5 step pair (from RESEARCH.md lines 981-998 — insert immediately after the Phase 4 "Upload CLI test artifacts" step at line 108):**

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

**Notable differences the planner must apply:**
1. `shell: pwsh` is explicit here (Phase 4 step doesn't set `shell:` because `dotnet test` is cross-shell-safe; the native exe invocation uses PowerShell backtick line-continuation, so `pwsh` must be explicit).
2. NOT `dotnet test` — direct exe invocation. The runner is Catch2 self-runner, not a managed assembly.
3. Stacked `--reporter` flags (console + junit) per RESEARCH.md §Pitfall 5 line 296. Console reporter sets the exit code; junit reporter writes the XML for triage. Don't drop either.
4. Artifact name `native-test-results` (NOT `cli-test-results` which would collide with the Phase 4 step's artifact name). RESEARCH.md §"Naming match with Phase 4" line 1009 covers this.
5. The CI workflow currently builds **only Release|x86** (`ci.yml:78`), so the lane invokes `bin\Release\UtinniCore.Tests.exe`. The vcxproj declares triple-config for local-dev parity (CON-T-02) but CI exercises only Release.
6. The `# Verified action versions as of 2026-05-16` comment at the top of ci.yml stays current — both `microsoft/setup-msbuild@v2` and `actions/upload-artifact@v4` are already verified for this workflow.

---

### `docs/ai/test-harness-plan.md` (modify — close out Tier 1 C++ row)

**Analog:** existing `Tier 1 — Pure unit tests` section at lines 23-33; this is the "Tier 1 — C++ side" row CONTEXT.md §"In scope" line 19 says Phase 5 closes.

**Verbatim existing row state (`test-harness-plan.md:23-33`):**

```markdown
### Tier 1 — Pure unit tests (fully autonomous)

- **C# side:** xUnit (or NUnit) test project alongside Utinni's main solution. `dotnet test` runs in CI and locally without a game client.
- **C++ side:** Catch2 (header-only, easy to drop into UtinniCore's solution) wired through `ctest` or a `vcpkg`-managed dependency.
- **Targets:**
  - TRE / IFF parsers
  - Plugin manifest loading and discovery
  - Settings serialization / migration
  - Math helpers (transforms, quaternions, vector ops)
  - Pure data-model logic
- **Wins:** Catches every regression in pure logic with no manual loop.
```

**Existing "Suggested phase order" entry (`test-harness-plan.md:73`)** — also needs marking complete:

```markdown
3. **Tier 1 C++ unit tests** — fold in once UtinniCore has refactored seams (the prior audit flagged native code quality, so this likely pairs with cleanup).
```

**Notable difference the planner must apply:** Replace the **C++ side** bullet (line 26) with the Phase 5 outcome — Catch2 v3.15.0 vendored amalgamated, MSBuild + direct exe, no ctest/vcpkg. The "Targets" bullet list (lines 27-32) is now mostly stale (TRE/IFF moved to managed in Phase 4 D-06); recommend rewriting to reflect actual scope: native targets achieved (`utility/string_utility::*` via 05-02) and remaining candidates (`utility/log` R-A registry deferred to Phase 6 STAB-03). Update "Suggested phase order" item 3 to mark "**[Closed in Phase 5, 2026-XX-XX]**" with a link to `.planning/phases/05-tier-1-c-unit-tests/`.

CONTEXT.md "In scope" line 19 is explicit: "docs/ai/test-harness-plan.md 'Tier 1 — C++ side' row gets dispositioned with the resolved D-01..D-04" — the planner's docs update must reference all four decisions explicitly.

---

## Shared Patterns

### MIT Header (23-line block per CONVENTIONS.md)
**Source:** every `.cpp` / `.h` file in `UtinniCore/` (e.g., `UtinniCore/utility/string_utility.cpp` carries the canonical block).
**Apply to:** `UtinniCore.Tests/main_smoke.cpp` and `UtinniCore.Tests/StringUtilityTests.cpp`.

The vendored `catch_amalgamated.{hpp,cpp}` files keep their upstream BSL-1.0 headers — do NOT graft an MIT header onto them (consistent with `external/imgui/imgui.cpp` and `external/spdlog/spdlog.h`, which keep their upstream licenses).

### Sibling-project ProjectReference with `LinkLibraryDependencies=false`
**Source:** `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj:92-102` (only existing precedent for this pattern in the solution).
**Apply to:** `UtinniCore.Tests/UtinniCore.Tests.vcxproj`. Build order is preserved; no actual link against UtinniCore.dll happens. Useful upgrade path: if the optional 05-03 `LogSubscribeTests.cpp` lands, flip `LinkLibraryDependencies=true` and add `UtinniCore.lib` to `AdditionalDependencies`.

### `$(SolutionDir)bin\$(Configuration)\` OutDir convention
**Source:** every existing vcxproj — `UtinniCore.vcxproj:84` (RelWithDbgInfo), `Utinni.LoaderLockHarness.vcxproj:48,52`, etc.
**Apply to:** `UtinniCore.Tests.vcxproj` for all three configs. Ensures the test exe lands at `bin\Release\UtinniCore.Tests.exe` where the CI step expects it.

### Triple-config block (Debug + Release + RelWithDbgInfo) per CON-T-02
**Source:** `UtinniCore/UtinniCore.vcxproj:3-15` (ProjectConfigurations) + `24-45` (Configuration PropertyGroups) + `Utinni.sln:82-87` (UtinniCore postSolution mapping).
**Apply to:** Both `UtinniCore.Tests.vcxproj` and the `Utinni.sln` postSolution entries for the new project's GUID. **Do not copy the LoaderLockHarness 2-config / collapsed-mapping pattern.**

### `actions/upload-artifact@v4` with `if: failure()` + `if-no-files-found: warn`
**Source:** `.github/workflows/ci.yml:86-92` (Phase 1 lane) and `99-108` (Phase 4 D-11 lane).
**Apply to:** New Phase 5 "Upload native test artifacts" step. Mirror the structure exactly; the only changes are `name:` and `path:`.

---

## No Analog Found

Files with no close native-test analog in the codebase (planner uses RESEARCH.md §"Code Examples" directly):

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `UtinniCore.Tests/main_smoke.cpp` | C++ test file | n/a | First native test file in repo. RESEARCH.md lines 308-343 is the source of truth. |
| `UtinniCore.Tests/StringUtilityTests.cpp` | C++ test file | n/a | First native test file in repo. RESEARCH.md lines 346-422 + §D-01 candidate table (lines 575-624) are the source of truth. |

Both are "greenfield" — Phase 5 explicitly sets the pattern future Phase 6 STAB-03 native tests will copy from. RESEARCH.md §"Tag Taxonomy" (line 1022) is the canonical reference for what tag combinations to use.

---

## Metadata

**Analog search scope:**
- `external/` (Glob — confirmed flat-vendor layout precedents in `external/spdlog/` and `external/imgui/`)
- `Utinni.sln` (Read full — 161 lines, all 13 project entries surveyed)
- `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj` (Read full — 106 lines, closest sibling Application/console-exe precedent)
- `UtinniCore/UtinniCore.vcxproj` (referenced via RESEARCH.md §D-03 verbatim quotes — canonical triple-config block, not re-read)
- `.github/workflows/ci.yml` (Read full — 108 lines, Phase 4 D-11 step pair confirmed at 94-108)
- `docs/ai/test-harness-plan.md` (Grep + Read 23-72 — Tier 1 row and Suggested phase order item 3 located)

**Files scanned:** 8 (4 analog files Read in full + 1 grep + 3 Glob enumerations)

**Pattern extraction date:** 2026-05-23

**Notable codebase-wide finding:** Five of the seven existing native vcxproj entries in `Utinni.sln` collapse `RelWithDbgInfo→Release|Win32` in their postSolution mappings (LoaderLockHarness, CrtMatchPlugin, LegacyPlugin, UtinniCoreDotNet.Tests, Utinni.Cli.Tests are some — UtinniCore and Launcher are the only ones with a real RelWithDbgInfo|Win32 mapping). The Phase 5 vcxproj must follow the **UtinniCore** precedent, not the herd. This is a recurring CON-T-02 leak the planner should not propagate.
