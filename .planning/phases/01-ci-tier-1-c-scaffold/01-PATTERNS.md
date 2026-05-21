# Phase 1: CI + Tier 1 C# scaffold - Pattern Map

**Mapped:** 2026-05-16
**Files analyzed:** 7 (5 CREATE + 2 MODIFY)
**Analogs found:** 4 / 7 (3 files are first-of-their-kind in this repo and must defer to RESEARCH.md `Code Examples` rather than an in-repo analog)

---

## File Classification

| New/Modified File | Action | Role | Data Flow | Closest In-Repo Analog | Match Quality |
|-------------------|--------|------|-----------|------------------------|---------------|
| `.github/workflows/ci.yml` | CREATE | config (CI workflow) | event-driven (push/PR) | none — `.github/` directory does not exist | **no analog** — defer to RESEARCH.md §Code Examples Pattern 2 |
| `.editorconfig` (repo root) | CREATE | config (formatting) | static (build-time apply) | `external/imgui/.editorconfig` (vendored — read-only reference, NOT to be copied verbatim) | partial-match (structural reference only) |
| `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` | CREATE | config (project file) | build-graph | `UtinniCoreDotNet/UtinniCoreDotNet.csproj` (legacy non-SDK, x86, net472) | **role-match only** (same target framework + platform; SDK-style is intentional break per RESEARCH §Pattern 1) |
| `UtinniCoreDotNet.Tests/HotkeyTests.cs` | CREATE | test (unit, xUnit) | request-response (ctor input → assert state) | `UtinniCoreDotNet/Hotkeys/Hotkey.cs` (system under test — license header, namespace, using order analog) + `UtinniCoreDotNet/Hotkeys/HotkeyManager.cs` (companion class shape) | **exact** for license/namespace/style conventions; **no analog** for xUnit test shape (defer to RESEARCH §Code Example 3) |
| `UtinniCoreDotNet.Tests/packages.lock.json` (optional) | CREATE (post-restore) | config (generated) | build-graph | none | **no analog** — generated artifact, no human-authored convention |
| `Utinni.sln` | MODIFY | config (solution) | build-graph | itself (existing project entries are the within-file analog) | **exact** — copy existing project-entry + config-mapping shape |
| `README.md` | MODIFY | docs | static | itself (existing top-of-file shape is the within-file analog) | **exact** — single-line badge insertion under `# Utinni` title |

---

## Pattern Assignments

### `UtinniCoreDotNet.Tests/HotkeyTests.cs` (test, xUnit unit)

**Primary analog:** `UtinniCoreDotNet/Hotkeys/Hotkey.cs` (the system under test + canonical C# file shape).
**Secondary analog:** `UtinniCoreDotNet/Hotkeys/HotkeyManager.cs` (same directory, same style).

**License header pattern** (verbatim from `UtinniCoreDotNet/Hotkeys/Hotkey.cs:1-23`) — 23 lines exactly, opens with `/**`, closes with `**/` (note doubled asterisk on close — non-standard but consistent across repo per CONVENTIONS.md §File Headers):
```csharp
/**
 * MIT License
 *
 * Copyright (c) 2020 Philip Klatt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
**/
```
**Important:** preserve `Copyright (c) 2020 Philip Klatt` verbatim per CONVENTIONS.md §File Headers ("the per-file headers are unchanged from upstream"). This is a fork; do not change authorship lines on Klatt-authored conceptual code.

**Imports pattern** (mirrors `UtinniCoreDotNet/Hotkeys/Hotkey.cs:25-27`, ordered per CONVENTIONS.md §Import Organization: `System.*` → third-party → `UtinniCore.*` → `UtinniCoreDotNet.*`):
```csharp
using System.Windows.Forms;
using UtinniCoreDotNet.Hotkeys;
using Xunit;
```
Note: `Xunit` is the new third-party using; goes between `System.*` and `UtinniCoreDotNet.*` per the established ordering rule.

**Namespace + class shape** (matches `UtinniCoreDotNet/Hotkeys/Hotkey.cs:29-31` — Allman braces, namespace block-style, no file-scoped namespaces because C# 7.3):
```csharp
namespace UtinniCoreDotNet.Tests
{
    public class HotkeyTests
    {
        // [Fact] methods here
    }
}
```

**Test naming + body pattern** (per CONTEXT.md D-04 `[Method]_[Scenario]_[ExpectedOutcome]` and RESEARCH.md §Code Example 3):
```csharp
[Fact]
public void Ctor_StringConstructor_SingleKey_SetsKeyAndNoModifier()
{
    var hk = new Hotkey("test", "test", "F1", () => { }, overrideGameInput: false);
    Assert.Equal(Keys.None, hk.ModifierKeys);
    Assert.Equal(Keys.F1, hk.Key);
}
```
- 4-space indent, Allman braces (CONVENTIONS.md §Code Style).
- `var` used freely for locals when type obvious from RHS (CONVENTIONS.md §Variables (C#)).
- Private/local variables `camelCase`, no `_` prefix (CONVENTIONS.md §Variables (C#) line 44: "Private fields are NOT prefixed with `_`").
- Named arg `overrideGameInput: false` matches the calling style in the broader codebase for boolean params (improves readability — pattern used in research example).

**Skip-with-comment pattern for C-08 known-fail** (per RESEARCH.md §Pitfall 5 recommendation, the skip-with-comment posture):
```csharp
[Fact(Skip = "C-08: expected to fail until Phase 2 fix lands (Enum.TryParse refactor on Hotkey.cs:82,91). " +
              "When unskipped, this asserts that malformed input like 'Ctrl + T' (note 'Ctrl' is not a valid Keys enum name - should be 'Control') is gracefully handled instead of throwing ArgumentException.")]
public void Ctor_StringConstructor_MalformedInput_DoesNotThrow()
{
    var ex = Record.Exception(() => new Hotkey("test", "test", "Ctrl + T", () => { }, overrideGameInput: false));
    Assert.Null(ex);
}
```
This is the "executable documentation" pattern — the test ships in Phase 1 skipped (yellow), and Phase 2's C-08 fix removes the `Skip` parameter.

**Comment-style note:** if any inline comment is needed, use `// ToDo` (capital T, capital D, no colon — CONVENTIONS.md §Comments §ToDo Tag). Do NOT use `// TODO`, `// FIXME`, `// HACK`, or `// XXX`. The Skip-message uses `C-08:` (with a colon) as a defect-ID prefix — this is distinct from a `ToDo` tag and is acceptable because it references the assessment.md defect taxonomy.

**No analog in repo for actual xUnit test shape** — `[Fact]`, `[Theory]`, `[InlineData]`, `Record.Exception`, `Assert.Equal`, `Assert.Null` are all first-of-their-kind in the repo (TESTING.md confirms zero-tests baseline). Defer to RESEARCH.md §Code Example 3 for the canonical xUnit shape. Apply repo conventions (license header, using order, Allman braces, naming) on top of that shape.

---

### `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` (config, build-graph)

**Closest in-repo analog:** `UtinniCoreDotNet/UtinniCoreDotNet.csproj` — same `TargetFramework=net472`, same `PlatformTarget=x86`, same root namespace pattern, same flat-root project layout.

**WARNING — analog is structural reference only, NOT a copy target.** The existing csproj is **legacy non-SDK format** (line 1-2: `<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">`). The new test csproj **must be SDK-style** per RESEARCH §Pattern 1 (intentional break because SDK-style is the documented xunit.net path and is required for `dotnet test` ergonomics). RESEARCH §"State of the Art" explicitly says: "Existing `UtinniCoreDotNet.csproj` is legacy non-SDK because it predates this transition and has WinForms designer-file ergonomics — DO NOT migrate it. New `UtinniCoreDotNet.Tests.csproj` is SDK-style because it has no WinForms designer files and benefits from `dotnet test` integration."

**Extract from analog — `UtinniCoreDotNet/UtinniCoreDotNet.csproj`** to mirror in the new SDK-style file:

Property values to copy (lines 5-15):
```xml
<Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
<Platform Condition=" '$(Platform)' == '' ">x86</Platform>
<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>      <!-- SDK-style equivalent: <TargetFramework>net472</TargetFramework> -->
<!-- PlatformTarget>x86</PlatformTarget> appears per-config in lines 25, 34, 43 -->
```
- The new csproj uses `<TargetFramework>net472</TargetFramework>` (SDK syntax) instead of `<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>`.
- The new csproj uses `<PlatformTarget>x86</PlatformTarget>` + `<Platforms>x86</Platforms>` at top-level (not per-config) because SDK-style with a single platform doesn't need per-config blocks.
- The new csproj uses `<LangVersion>7.3</LangVersion>` to match the existing `UtinniCoreDotNetGen.csproj:44` and the per-config block in `UtinniCoreDotNet.csproj:44`.

**Canonical body** (from RESEARCH §Code Example 1, verbatim — this is the file content to write):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <PlatformTarget>x86</PlatformTarget>
    <Platforms>x86</Platforms>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
    <RootNamespace>UtinniCoreDotNet.Tests</RootNamespace>
    <AssemblyName>UtinniCoreDotNet.Tests</AssemblyName>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\UtinniCoreDotNet\UtinniCoreDotNet.csproj" />
  </ItemGroup>
</Project>
```

**XML file-header note:** the existing csproj files do NOT carry the 23-line MIT header (verified — `UtinniCoreDotNet.csproj` opens with `<?xml version="1.0" encoding="utf-8"?>` directly, no comment block). Do NOT add the MIT header to the new csproj — that would break the in-repo convention for project files. The 23-line header applies only to source files (`.cs`, `.cpp`, `.h`) per CONVENTIONS.md §File Headers ("Every source file in the repo").

**Anti-patterns explicitly NOT to copy from `UtinniCoreDotNet/UtinniCoreDotNet.csproj`:**
- Do NOT add `<Reference Include="System" />`-style BCL references (lines 49-60). SDK-style auto-references the BCL for net472. Adding them creates duplicate-reference warnings (RESEARCH §Anti-Patterns).
- Do NOT add per-config `<PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|x86'">` blocks (lines 16-47). SDK-style handles Debug/Release output paths via `bin\$(Configuration)\$(TargetFramework)\` automatically.
- Do NOT include `<Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />` (line 238). SDK-style imports targets via `Sdk="Microsoft.NET.Sdk"`.
- Do NOT explicitly `<Compile Include="...">` files. SDK-style globs `**/*.cs` by default — the single `HotkeyTests.cs` will be picked up automatically.

---

### `Utinni.sln` (config, build-graph) — MODIFY

**Analog:** the existing project entries within `Utinni.sln` itself (lines 17-22 for `UtinniCoreDotNet`, lines 23-24 for `UtinniCoreDotNetGen`).

**C# project-type GUID** (constant — copy verbatim from `Utinni.sln:17,23`):
```
{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}
```
This is the standard "C# project" GUID; it works for both legacy non-SDK and SDK-style csprojs per RESEARCH §Pattern 4.

**Project-entry pattern** (modeled on `Utinni.sln:17-22` which is the closest analog — a managed C# project with a `ProjectDependencies` section pointing at `UtinniCore`):
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "UtinniCoreDotNet", "UtinniCoreDotNet\UtinniCoreDotNet.csproj", "{39AB8A43-B916-4C6E-87DD-928B438CAE68}"
	ProjectSection(ProjectDependencies) = postProject
		{10EA6136-AAA0-4F1D-8117-07081C785E5B} = {10EA6136-AAA0-4F1D-8117-07081C785E5B}
		{AEFED7F6-4BA9-44FC-A353-71A463A82FDE} = {AEFED7F6-4BA9-44FC-A353-71A463A82FDE}
	EndProjectSection
EndProject
```
**Conventions to copy:**
- Tab-indented `ProjectSection` body (verify with hex dump; sln files use tabs).
- Project file path uses backslash (`UtinniCoreDotNet\UtinniCoreDotNet.csproj`) — Windows-native separator, Visual Studio convention.
- GUIDs uppercase, hyphenated, brace-wrapped.
- Dependency is declared as `{GUID} = {GUID}` (same GUID twice — VS convention).

**Entry to ADD** (the new test project — `UtinniCoreDotNet.Tests` depends on `UtinniCoreDotNet` per CONTEXT.md §Integration Points):
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "UtinniCoreDotNet.Tests", "UtinniCoreDotNet.Tests\UtinniCoreDotNet.Tests.csproj", "{NEW-GUID-HERE}"
	ProjectSection(ProjectDependencies) = postProject
		{39AB8A43-B916-4C6E-87DD-928B438CAE68} = {39AB8A43-B916-4C6E-87DD-928B438CAE68}
	EndProjectSection
EndProject
```
Generate a fresh GUID for `{NEW-GUID-HERE}` (uppercase, brace-wrapped). The dependency GUID `{39AB8A43-B916-4C6E-87DD-928B438CAE68}` is `UtinniCoreDotNet` (verified at `Utinni.sln:17`).

**Configuration-mapping pattern** (modeled on `Utinni.sln:48-53` which is the closest analog — `UtinniCoreDotNet`'s mappings, since both projects are managed C# x86 net472):
```
{39AB8A43-B916-4C6E-87DD-928B438CAE68}.Debug|x86.ActiveCfg = Debug|x86
{39AB8A43-B916-4C6E-87DD-928B438CAE68}.Debug|x86.Build.0 = Debug|x86
{39AB8A43-B916-4C6E-87DD-928B438CAE68}.Release|x86.ActiveCfg = Release|x86
{39AB8A43-B916-4C6E-87DD-928B438CAE68}.Release|x86.Build.0 = Release|x86
{39AB8A43-B916-4C6E-87DD-928B438CAE68}.RelWithDbgInfo|x86.ActiveCfg = RelWithDbgInfo|x86
{39AB8A43-B916-4C6E-87DD-928B438CAE68}.RelWithDbgInfo|x86.Build.0 = RelWithDbgInfo|x86
```

**Mappings to ADD** (place in `GlobalSection(ProjectConfigurationPlatforms) = postSolution` after line 71, before line 72's `EndGlobalSection`):
```
{NEW-GUID-HERE}.Debug|x86.ActiveCfg = Debug|x86
{NEW-GUID-HERE}.Debug|x86.Build.0 = Debug|x86
{NEW-GUID-HERE}.Release|x86.ActiveCfg = Release|x86
{NEW-GUID-HERE}.Release|x86.Build.0 = Release|x86
{NEW-GUID-HERE}.RelWithDbgInfo|x86.ActiveCfg = Release|x86
```
**Critical deviation from analog:** under `RelWithDbgInfo|x86`, the test project maps to `Release|x86` (not `RelWithDbgInfo|x86`) and has **NO `.Build.0` entry** — meaning "don't build the test project under the RelWithDbgInfo solution config." Justification per RESEARCH §Pattern 4: "Skip `RelWithDbgInfo|x86` for the test project — there's no test value in shipping it under that config, and the SDK template doesn't expect it." Also see RESEARCH §A8.

**Solution-section globals to LEAVE UNTOUCHED:**
- `SolutionConfigurationPlatforms` (lines 30-34) — already has `Debug|x86`, `Release|x86`, `RelWithDbgInfo|x86`. No new platform needed.
- `SolutionProperties` (lines 73-75) — unchanged.
- `ExtensibilityGlobals` (lines 76-78) — unchanged (preserves `SolutionGuid`).

**File-format conventions to preserve:**
- CRLF line endings (the entire repo per CONTEXT.md D-11; also `git config core.autocrlf` likely set).
- BOM-less or BOM? Run a quick file check before editing — `Utinni.sln` was generated by VS and may have a UTF-8 BOM (`EF BB BF`); preserve whatever is there.
- Tab indentation in `ProjectSection` and `GlobalSection` bodies (not spaces).

---

### `README.md` (docs) — MODIFY

**Analog:** itself — the existing top-of-file shape (lines 1-6 of current `README.md`).

**Current top-of-file shape** (verbatim from `README.md:1-6`):
```markdown
# Utinni
Utinni is a client plugin and injection framework which aims to provide an easier access to client and content development for Pre-CU Star Wars Galaxies and more specifically [SWGEmu](https://github.com/swgemu).

Official plugins can be found [here](https://github.com/ptklatt/UtinniPlugins).

> **Documentation:** an interactive doc set lives in [`docs/index.html`](./docs/index.html) — open it locally for the navigable HTML site (architecture, injection, plugin framework, callbacks, UI, hotkeys, undo/redo, SDK, build, tutorial, internals, regenerating bindings, glossary). The same content is available as plain markdown for AI/grep tooling under [`docs/ai/`](./docs/ai/). The plugin reference docs live in the sibling repo at [`../UtinniPlugins/docs/`](../UtinniPlugins/docs/index.html).
```

**Insertion pattern** (per CONTEXT.md D-10 + RESEARCH.md §Code Example 4 + §Pitfall 6 — note that the repo's default branch is **`master`**, not `main`, per gitStatus verification):
```markdown
# Utinni

[![CI](https://github.com/kennethlong/Utinni/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/kennethlong/Utinni/actions/workflows/ci.yml)

Utinni is a client plugin and injection framework which aims to provide an easier access to client and content development for Pre-CU Star Wars Galaxies and more specifically [SWGEmu](https://github.com/swgemu).
```

**Insertion rules:**
- Insert immediately after the `# Utinni` title.
- Add one blank line **above** the badge (between `# Utinni` and the badge line) — this differs from the current file which has the tagline directly under the title with no blank line. The blank line creates visual separation for the badge.
- Add one blank line **below** the badge (between the badge and the tagline).
- Wrap the badge image in a hyperlink `[![CI](badge-url)](workflow-url)` per OSS convention (RESEARCH.md §Open Question 2 recommendation).
- Owner is `kennethlong`, repo is `Utinni` (per `project_fork_strategy` memory + gitStatus verification).
- Branch is `master` (the repo's actual default branch — NOT `main` as CONTEXT.md D-07 says; RESEARCH.md §Pitfall 6 explicitly reconciles this and recommends matching the actual branch name).

**Everything else in `README.md` is UNCHANGED.** Do not touch the `> **Documentation:**` blockquote (line 6), the `**Features**` section, the `**Preview**` image, the `**Third party libraries used:**` section, the `**Credits**` section, or the `Official plugins can be found [here]` line.

---

### `.editorconfig` (config, formatting) — CREATE at repo root

**Closest in-repo reference:** `external/imgui/.editorconfig` (vendored). **This is a structural reference only — do NOT copy verbatim and do NOT modify the imgui file per CONTEXT.md D-11.**

**Extract from `external/imgui/.editorconfig` (lines 1-22)** showing the established structural pattern (sectioned by file glob, with `root = true` declaration):
```ini
# See http://editorconfig.org to read about the EditorConfig format.
# - Automatically supported by VS2017+ and most common IDE or text editors.

# top-most EditorConfig file
root = true

# Default settings:
# Use 4 spaces as indentation
[*]
indent_style = space
indent_size = 4
insert_final_newline = true
trim_trailing_whitespace = true

[imstb_*]
indent_size = 3
trim_trailing_whitespace = false

[Makefile]
indent_style = tab
indent_size = 4
```

**Conventions extracted to apply at root:**
- Lead with a comment block explaining the file's purpose.
- `root = true` on its own line near the top.
- One `[*]` default-glob section establishing defaults (`indent_style = space`, `indent_size = 4`, `insert_final_newline = true`, `trim_trailing_whitespace = true`).
- Subsequent named-glob sections for overrides (e.g. `[Makefile]` for tabs).
- All key-value pairs use the form `key = value` (single spaces around `=`).

**Canonical body** (per CONTEXT.md D-11 + RESEARCH.md §Code Example 2):
```ini
# See http://editorconfig.org to read about the EditorConfig format.
# Codifies the de-facto conventions documented in .planning/codebase/CONVENTIONS.md
# without enforcing analyzer rules (those land in Phase 6 with .clang-format).
# Vendored external/imgui/.editorconfig is preserved and overrides this file
# for files under external/imgui/.

root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

# C#, C++, and headers - Allman braces enforced by file content (no analyzer rules this phase)
[*.{cs,cpp,h}]
indent_style = space
indent_size = 4

# Markdown - preserve trailing whitespace so two-space line breaks are not stripped
[*.md]
trim_trailing_whitespace = false

# YAML (e.g. GitHub Actions) - 2-space indent per de-facto convention
[*.{yml,yaml}]
indent_size = 2

# Makefiles require tabs (POSIX)
[Makefile]
indent_style = tab
```

**Key deviations from the imgui analog (justified):**
- Add `end_of_line = crlf` — CONTEXT.md D-11 explicitly locks this ("CRLF — match existing repo norm"). The imgui file omits it (implicit = native-OS).
- Add `charset = utf-8` — defensive baseline; CONVENTIONS.md doesn't specify but every existing source file is UTF-8.
- Add per-language sections (`[*.{cs,cpp,h}]`, `[*.md]`, `[*.{yml,yaml}]`) — the imgui file is C/C++-only and doesn't need them. We have a multi-language repo.
- Do NOT add analyzer-style rules (`csharp_prefer_var`, `dotnet_naming_*`, etc.) — D-11 defers those to Phase 6.

**Why `external/imgui/.editorconfig` is NOT a hard analog:** it's a vendored file shipped with the upstream `dearImgui` project. It uses a single `[*]` rule plus imgui-specific globs (`[imstb_*]`). It is not "our" convention — it's imgui's. Use it for structural reference only.

---

### `.github/workflows/ci.yml` (config, CI workflow) — CREATE

**Closest in-repo analog: NONE.** The `.github/` directory does not exist (verified via `ls D:/Code/Utinni/` — entries are LICENSE, Launcher, README.md, UtINI, Utinni.sln, UtinniCore, UtinniCore-Symbols, UtinniCoreDotNet, UtinniCoreDotNetGen, data, docs, external, licenses.txt, sdk; no `.github`). There are no existing workflow files anywhere in the repo.

**Defer to RESEARCH.md §Code Examples / §Pattern 2 (GitHub Actions workflow shape)** for the canonical structure. The complete YAML is given verbatim there; the planner should copy it into `.github/workflows/ci.yml` with the following repo-specific adjustments already specified in RESEARCH:

1. **Branch name:** trigger on `master` (not `main`) per RESEARCH §Pitfall 6.
2. **Runner pin:** `windows-2022` (not `windows-latest`) per RESEARCH §Standard Stack.
3. **MSBuild action:** `microsoft/setup-msbuild@v2` with no `vs-version` argument per RESEARCH §Pitfall 1.
4. **Build command:** `msbuild Utinni.sln /m /restore /p:Configuration=Release /p:Platform=x86 /p:RestorePackagesConfig=true`.
5. **Test command:** `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release --logger "console;verbosity=normal" --logger "trx;LogFileName=test-results.trx"` — project-targeted, NOT solution-targeted, per RESEARCH §Pitfall 2.
6. **Artifact upload on failure:** `actions/upload-artifact@v4` for `*.trx` files.

**No file-header convention applies** to YAML files in this repo (none exist yet, so there's no precedent). RESEARCH's example workflow leads with `# Source: github.com/microsoft/setup-msbuild...` comments — that is a reasonable convention to adopt as the first-of-its-kind precedent.

**Indent:** 2-space (YAML standard; matches the `.editorconfig` rule for `[*.{yml,yaml}]` above).

---

### `UtinniCoreDotNet.Tests/packages.lock.json` (config, generated) — CREATE (after first restore)

**No analog and no convention to follow** — this file is generated by NuGet on first `dotnet restore` / `msbuild /restore` against a csproj that has `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`.

**Pattern:**
1. Author the csproj with `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` (already included above).
2. Run `msbuild Utinni.sln /restore /p:Configuration=Release /p:Platform=x86` locally once.
3. The lockfile materializes at `UtinniCoreDotNet.Tests/packages.lock.json`.
4. Commit the lockfile per RESEARCH.md §Pitfall 7 ("Yes, enable it. Phase 1 sets the test-infrastructure precedent...").

**No edits to this file ever** — regenerate via `dotnet restore --force-evaluate` when intentionally bumping versions.

**Do not add the MIT header** — it's machine-generated JSON, header convention does not apply (and would break NuGet parsing).

---

## Shared Patterns

### Pattern S-1: MIT License Header (23 lines, opens `/**`, closes `**/`)
**Source:** `UtinniCoreDotNet/Hotkeys/Hotkey.cs:1-23` (also identical in `Hotkeys/HotkeyManager.cs`, `UndoRedo/UndoRedoManager.cs`, every other `.cs` file in the repo)
**Apply to:** every new `.cs` file (i.e. `HotkeyTests.cs`)
**Do NOT apply to:** `.csproj`, `.sln`, `.editorconfig`, `.yml`, `.json`, `.md` (verified — no existing project/solution/config file in the repo carries this header)
**Verbatim copy:** copy bytes-for-bytes from `Hotkey.cs:1-23`. Do not retype. Do not edit the year or the copyright holder (CONVENTIONS.md §File Headers: "the per-file headers are unchanged from upstream").

### Pattern S-2: Allman Braces + 4-space Indent
**Source:** every `.cs` file in the repo (e.g. `UtinniCoreDotNet/Hotkeys/Hotkey.cs:31-115`)
**Apply to:** `HotkeyTests.cs`
**Concrete shape:**
```csharp
namespace X.Y
{
    public class Foo
    {
        public void Bar()
        {
            if (cond)
            {
                ...
            }
        }
    }
}
```
4 spaces per level. Opening brace on its own line. No same-line braces. No tabs (CONVENTIONS.md §Code Style).

### Pattern S-3: Using-Block Ordering
**Source:** `UtinniCoreDotNet/Hotkeys/Hotkey.cs:25-27`, `UtinniCoreDotNet/UI/Forms/FormMain.cs:25-38` (cited in CONVENTIONS.md §Import Organization)
**Apply to:** `HotkeyTests.cs`
**Order:**
1. `System.*` first (e.g. `using System.Windows.Forms;`)
2. Third-party (e.g. `using Xunit;` — new this phase)
3. `UtinniCore.*` generated bindings (none needed in the test file)
4. `UtinniCoreDotNet.*` project namespaces (e.g. `using UtinniCoreDotNet.Hotkeys;`)
5. Aliases (`using X = Y;`) last (none needed)

### Pattern S-4: PascalCase for public, camelCase for private/local, no `_` prefix
**Source:** `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs:34-45` (`private readonly Action onUpdateCommandsCallback;` — camelCase, no `_`)
**Apply to:** `HotkeyTests.cs` (test method names are PascalCase per xUnit + D-04; local variables `camelCase` like `var hk = ...`)

### Pattern S-5: CRLF line endings throughout
**Source:** all repo files (verified via `git config` defaults + CONTEXT.md D-11)
**Apply to:** every file created or modified this phase (`.cs`, `.csproj`, `.editorconfig`, `.sln`, `.yml`, `.md`).
**Verification:** after writing, run `file <path>` or check via Git Diff to ensure CRLF was preserved. Be especially careful with `.editorconfig` and `.yml` because some editors default to LF.

### Pattern S-6: Flat-root project layout
**Source:** `Utinni.sln:6-28` (every project entry is `<ProjectName>\<ProjectName>.{csproj,vcxproj}` directly under the repo root)
**Apply to:** the new `UtinniCoreDotNet.Tests` directory must sit at repo root, NOT under a `tests/` or `test/` subfolder (CONTEXT.md D-01 locks this).
**Verification:** the project entry path in `Utinni.sln` is `UtinniCoreDotNet.Tests\UtinniCoreDotNet.Tests.csproj` (no parent directory).

### Pattern S-7: No `try/catch` in production C# code (test code is the exception)
**Source:** CONVENTIONS.md §Error Handling §C# Patterns ("try/catch is essentially absent")
**Apply to (negative):** do NOT wrap `new Hotkey(...)` in `try/catch` in the test code — use `Record.Exception(() => ...)` which is the xUnit idiom for "did this throw?" (cleaner than try/catch). RESEARCH §Code Example 3 shows this pattern.

---

## No Analog Found

| File | Role | Reason | Mitigation |
|------|------|--------|-----------|
| `.github/workflows/ci.yml` | CI workflow | `.github/` directory does not exist; no GitHub Actions workflows in repo history | Follow RESEARCH.md §Pattern 2 verbatim (with the 6 repo-specific adjustments listed above). This is first-of-its-kind; it establishes the convention for future workflows. |
| `UtinniCoreDotNet.Tests/HotkeyTests.cs` (xUnit-specific shape) | xUnit test | TESTING.md confirms zero-tests baseline; no `[Fact]`/`[Theory]`/`Assert.*` precedent | Follow RESEARCH.md §Code Example 3 for xUnit shape. Apply S-1..S-7 shared patterns (license header, using order, Allman braces, naming, CRLF) on top. |
| `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` (SDK-style + PackageReference) | SDK-style csproj | All existing csprojs are legacy non-SDK with `<Reference>` items, no `<PackageReference>` anywhere in repo | Follow RESEARCH.md §Code Example 1 verbatim. This intentionally breaks the existing legacy-csproj convention per CONTEXT.md §Established Patterns and RESEARCH.md §"State of the Art" guidance. |
| `UtinniCoreDotNet.Tests/packages.lock.json` | NuGet lockfile | No NuGet usage in repo prior to this phase | Generated automatically by NuGet on first restore; commit as-is. |

---

## Convention Drift Risks

Things the planner should watch for that could accidentally violate established conventions:

1. **LF vs CRLF drift:** Windows editors and `dotnet` CLI can write LF. Verify every new file has CRLF before commit (`git status` will show "warning: LF will be replaced by CRLF" if `core.autocrlf=true`; check `.gitattributes` if present).
2. **`// TODO` vs `// ToDo`:** if any inline comment is added, use the repo's `// ToDo` spelling (capital T, capital D, optional colon) per CONVENTIONS.md §Comments §ToDo Tag. The Skip-message in HotkeyTests.cs uses `C-08:` as a defect-ID prefix — distinct convention, acceptable.
3. **`_field` private fields:** xUnit examples on the web often use `_fixture`-style private field names. Repo convention is `camelCase` with NO underscore prefix (CONVENTIONS.md §Variables (C#)). Do not import the `_` convention.
4. **Adding `[STAThread]` to test methods:** out of scope this phase (Hotkey.ProcessString is pure-logic) but flagged in RESEARCH.md §Anti-Patterns for future test files that touch WinForms designers.
5. **Solution-file tab vs space:** `Utinni.sln` uses tabs for `ProjectSection`/`GlobalSection` body indentation. Most editors auto-convert; verify after edit.
6. **CSproj XML header:** existing csproj files use `<?xml version="1.0" encoding="utf-8"?>` declaration. The SDK-style format does NOT require this line (SDK-style projects typically omit it). Either is acceptable; RESEARCH.md §Code Example 1 omits it. Recommend omit for SDK-style consistency.
7. **Hotkey constructor parameter order:** the string constructor (`Hotkey.cs:42`) takes 7 params with defaults on the last two. Test code uses named arg for `overrideGameInput:` for readability (matches the codebase's habit of named args for booleans where ambiguous).

---

## Metadata

**Analog search scope:** `D:\Code\Utinni\` (excluding `external/`, `bin/`, `obj/`, `.git/`, `.planning/`).
**Files Read during analog extraction:**
- `D:\Code\Utinni\Utinni.sln` (full file, 80 lines)
- `D:\Code\Utinni\UtinniCoreDotNet\Hotkeys\Hotkey.cs` (full file, 117 lines)
- `D:\Code\Utinni\UtinniCoreDotNet\Hotkeys\HotkeyManager.cs` (full file, 129 lines)
- `D:\Code\Utinni\UtinniCoreDotNet\UtinniCoreDotNet.csproj` (full file, 239 lines)
- `D:\Code\Utinni\UtinniCoreDotNetGen\UtinniCoreDotNetGen.csproj` (full file, 96 lines)
- `D:\Code\Utinni\UtinniCoreDotNet\Properties\AssemblyInfo.cs` (full file, 37 lines)
- `D:\Code\Utinni\UtinniCoreDotNet\UndoRedo\UndoRedoManager.cs` (lines 1-45, header + class shape only)
- `D:\Code\Utinni\external\imgui\.editorconfig` (full file, 22 lines)
- `D:\Code\Utinni\README.md` (full file, 51 lines)
- `D:\Code\Utinni\.gitignore` (full file, 25 lines)
- `D:\Code\Utinni\.planning\codebase\CONVENTIONS.md` (full file, 223 lines)
- `D:\Code\Utinni\.planning\phases\01-ci-tier-1-c-scaffold\01-CONTEXT.md` (full file, 132 lines)
- `D:\Code\Utinni\.planning\phases\01-ci-tier-1-c-scaffold\01-RESEARCH.md` (lines 1-799 of ~825, all relevant sections)

**Verified absent:**
- `D:\Code\Utinni\.github\` (directory does not exist)
- `D:\Code\Utinni\.editorconfig` (file does not exist at root)
- `D:\Code\Utinni\CLAUDE.md` (file does not exist)
- `D:\Code\Utinni\.claude\` and `D:\Code\Utinni\.agents\` (skill directories — neither exists)

**Pattern extraction date:** 2026-05-16
