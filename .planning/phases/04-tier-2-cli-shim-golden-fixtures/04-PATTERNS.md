# Phase 4: Tier 2 CLI shim + golden fixtures — Pattern Map

**Mapped:** 2026-05-23
**Files analyzed:** 18 new + 3 modified (golden fixtures aggregated as 4 dir-groups)
**Analogs found:** 18 / 18 (every new file has a strong in-repo analog; one file — `validate-plugin` reflection — has a partial analog because the reflection-over-IPlugin walk is novel)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Utinni.Cli/Utinni.Cli.csproj` | new C# exe project (SDK-style) | build artifact | `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` | role-match (SDK-style net472/x86 sibling; differs only in `OutputType=Exe` + nuget set) |
| `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` | new xUnit test project | build artifact | `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` | exact (xUnit + Microsoft.NET.Test.Sdk pin set; Fixtures-copy pattern via `<Content>`) |
| `Utinni.Cli/Program.cs` | CLI entry point + verb dispatch | request-response | (no exact analog) `UtinniCoreDotNet/main.cs` + CommandLineParser docs | role-match (Main entry; verb routing is new) |
| `Utinni.Cli/Commands/ParseTreCommand.cs` | CLI command verb handler | request-response → file-I/O → JSON-out | (none; greenfield CLI surface) | no exact analog — see §"No Analog Found" |
| `Utinni.Cli/Commands/ListObjectsCommand.cs` | CLI command verb handler | request-response → file-I/O → JSON-out | sibling `ParseTreCommand.cs` once it exists | sibling (planner picks task ordering) |
| `Utinni.Cli/Commands/InspectIffCommand.cs` | CLI command verb handler | request-response → file-I/O → JSON-out | sibling `ParseTreCommand.cs` once it exists | sibling |
| `Utinni.Cli/Commands/ValidatePluginCommand.cs` | CLI command verb handler | request-response → MEF reflection → JSON-out | `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:72-141` (consumes the `Load(string)` seam) | role-match (reuses existing loader; reflection-over-loaded-types is new) |
| `Utinni.Cli/Output/JsonOutput.cs` | utility (JSON envelope helper) | transform | (none; greenfield) — closest stylistic match is `UtinniCoreDotNet/Utility/Log.cs:34-50` (static utility class shape) | partial — see §"No Analog Found" |
| `Utinni.Cli/Output/SortedKeyContractResolver.cs` | utility (Newtonsoft contract resolver) | transform | (none; framework subclass) | no analog (framework idiom) |
| `Utinni.Cli.Tests/Infrastructure/InProcessCliRunner.cs` | test helper (stdout capture) | event-driven (test harness) | `UtinniCoreDotNet.Tests/PluginLoaderTests.cs:70-75` (MakeTempDir helper pattern) | partial (helper-class pattern matches; Console.SetOut redirection is new) |
| `Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs` | test helper (JToken DeepEquals + dump) | transform | `UtinniCoreDotNet.Tests/PluginLoaderTests.cs:49-68` (FindFixtureDll helper + AppContext.BaseDirectory walk) | partial (fixture-path resolution matches; JToken assertion is new) |
| `Utinni.Cli.Tests/Infrastructure/FixturePath.cs` | test helper (path resolver) | transform | `UtinniCoreDotNet.Tests/PluginLoaderTests.cs:49-68` (FindFixtureDll) | exact (same `AppContext.BaseDirectory` walk pattern) |
| `Utinni.Cli.Tests/Commands/ParseTreCommandTests.cs` | xUnit test class | event-driven | `UtinniCoreDotNet.Tests/PluginLoaderTests.cs` | exact (per-Fact assertion shape; per-Theory parametrisation; temp-dir lifecycle) |
| `Utinni.Cli.Tests/Commands/ListObjectsCommandTests.cs` | xUnit test class | event-driven | `UtinniCoreDotNet.Tests/PluginLoaderTests.cs` | exact |
| `Utinni.Cli.Tests/Commands/InspectIffCommandTests.cs` | xUnit test class | event-driven | `UtinniCoreDotNet.Tests/PluginLoaderTests.cs` | exact |
| `Utinni.Cli.Tests/Commands/ValidatePluginCommandTests.cs` | xUnit test class | event-driven | `UtinniCoreDotNet.Tests/PluginLoaderTests.cs` | exact (existing class already tests the seam this command consumes) |
| `UtinniCoreDotNet/Formats/Tre/TreFile.cs` (+ supporting types) | pure-C# parser | file-I/O (read) | (no exact analog — first format parser in repo) — closest stylistic match is `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:35-50` (class shape, MIT header) | partial — see §"No Analog Found" |
| `UtinniCoreDotNet/Formats/Iff/IffReader.cs` (+ chunk types) | pure-C# parser | file-I/O (read) | sibling `TreFile.cs` once it exists | sibling |
| `UtinniCoreDotNet.Tests/FormatsTests/Tre/TreFileTests.cs` | xUnit parser test | event-driven | `UtinniCoreDotNet.Tests/HotkeyTests.cs:30-83` (pure-managed Tier-1 test; no native deps) | exact (Tier-1 pure-managed test pattern; no fixture-DLL dance) |
| `UtinniCoreDotNet.Tests/FormatsTests/Iff/IffReaderTests.cs` | xUnit parser test | event-driven | `UtinniCoreDotNet.Tests/HotkeyTests.cs` | exact |
| `Utinni.Cli.Tests/Fixtures/{tre,iff,plugins,world-snapshot}/` | static data fixtures | file-I/O | `UtinniCoreDotNet.Tests/Fixtures/{BrokenPlugin,GoodPlugin}/` | role-match (directory-per-fixture pattern; CSproj fixtures are different shape but the `<Content CopyToOutputDirectory="PreserveNewest">` copy mechanic is the same idiom) |
| `Utinni.sln` (modified) | solution file | build artifact | self (the existing solution is its own template) | exact — see §"Shared Patterns" |
| `.github/workflows/ci.yml` (modified) | CI workflow | event-driven | self (existing single-job extends with a second step) | exact — see §"Shared Patterns" |
| `.gitattributes` (new or modified) | git config | n/a | (none in repo — `Grep` confirms no `.gitattributes` currently exists; planner creates one) | no analog (Plan 04-01 creates from scratch per Research §Pitfall 6) |
| `.planning/PROJECT.md` (modified) | docs | transform (DEC-C3 row edit) | self | exact (table-row edit) |
| `docs/ai/assessment.md` (modified) | docs | transform (CON-O-09 + CON-O-11 disposition row edits) | self | exact |

---

## Pattern Assignments

### `Utinni.Cli/Utinni.Cli.csproj` (new SDK-style net472/x86 exe project)

**Analog:** `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` lines 1-31 + `UtinniCoreDotNet/UtinniCoreDotNet.csproj` lines 16-36 (for the Debug|x86 / Release|x86 platform pin)

**SDK-style PropertyGroup pattern** (UtinniCoreDotNet.Tests.csproj lines 1-15 — apply identically):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <PlatformTarget>x86</PlatformTarget>
    <Platforms>x86</Platforms>
    <!-- Without this, SDK-style emits to bin\x86\Release\net472\ when built via
         msbuild /p:Platform=x86, but `dotnet test` (with the no-build flag)
         probes bin\Release\net472\. Omitting Platform from the path keeps the
         build output and the test discovery in sync. -->
    <AppendPlatformToOutputPath>false</AppendPlatformToOutputPath>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
    <RootNamespace>Utinni.Cli</RootNamespace>
    <AssemblyName>utinni-cli</AssemblyName>
    <OutputType>Exe</OutputType>          <!-- override Tests' Library -->
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
```

**PackageReference + ProjectReference pattern** (UtinniCoreDotNet.Tests.csproj lines 27-34):
```xml
<ItemGroup>
  <PackageReference Include="CommandLineParser" Version="2.9.1" />
  <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="..\UtinniCoreDotNet\UtinniCoreDotNet.csproj" />
</ItemGroup>
```

**Why this csproj must be SDK-style, not the older format:** `UtinniCoreDotNet.csproj` (legacy ToolsVersion="15.0" full XML) is the editor surface that PostBuildEvent + Designer.cs require. New projects with no Designer / no Forms use SDK-style. Both `UtinniCoreDotNet.Tests` and the existing fixture csprojs (`GoodPlugin.csproj`, `BrokenPlugin.csproj`) confirm SDK-style is the established pattern for new C# projects in this repo.

---

### `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` (new SDK-style xUnit test project)

**Analog:** `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` (entire file — copy verbatim, then tweak)

**Full pattern to copy** (UtinniCoreDotNet.Tests.csproj lines 1-31):
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <PlatformTarget>x86</PlatformTarget>
    <Platforms>x86</Platforms>
    <AppendPlatformToOutputPath>false</AppendPlatformToOutputPath>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
    <RootNamespace>Utinni.Cli.Tests</RootNamespace>
    <AssemblyName>Utinni.Cli.Tests</AssemblyName>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Utinni.Cli\Utinni.Cli.csproj" />
    <ProjectReference Include="..\UtinniCoreDotNet\UtinniCoreDotNet.csproj" />
  </ItemGroup>
```

**`<Content>` fixture-copy pattern** (Research §Pitfall 3 calls this out; UtinniCoreDotNet.Tests does NOT use this idiom — its fixtures are csprojs. The Phase 4 fixtures are static binary/JSON files, so add):
```xml
<ItemGroup>
  <Content Include="Fixtures\**\*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

**DefaultItemExcludes warning** (UtinniCoreDotNet.Tests.csproj lines 16-25): that exclude was specifically for nested csproj children. Phase 4's `Utinni.Cli.Tests/Fixtures/` will not contain csprojs (only static .tre/.iff/.json files), so **do NOT include the `<DefaultItemExcludes>$(DefaultItemExcludes);Fixtures\**</DefaultItemExcludes>` line** — that would block the `<Content>` glob above.

**CopyNativeArtifactsForTests adaptation** (UtinniCoreDotNet.Tests.csproj lines 63-86): the validate-plugin tests need `Utinni.CrtMatchPlugin.dll` co-located. Copy the Target verbatim but reduce the file list to just the CRT-match plugin DLL (the Plan 04-04 `valid-plugin/` fixture). The pattern:
```xml
<Target Name="CopyValidPluginFixture" AfterTargets="Build">
  <ItemGroup>
    <_PluginArtifacts Include="$(SolutionDir)bin\$(Configuration)\Utinni.CrtMatchPlugin.dll" />
  </ItemGroup>
  <Copy SourceFiles="@(_PluginArtifacts)"
        DestinationFolder="$(TargetDir)Fixtures\plugins\valid-plugin\"
        SkipUnchangedFiles="true"
        Condition="Exists('%(_PluginArtifacts.Identity)')" />
</Target>
```

---

### `Utinni.Cli/Program.cs` (entry point + verb dispatch)

**Analog:** none exact; closest is `UtinniCoreDotNet/main.cs` (consumed but not a literal template — its Main bootstraps the editor). The verb-dispatch pattern is per CommandLineParser docs (Research §Pattern 1). The pattern shape:

**MIT header** (required — copy from `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:1-23` verbatim, change nothing in the boilerplate):
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

**Namespace + using block pattern** (PluginLoader.cs:25-35 — convention: outer-to-inner alphabetical-ish, Utinni* last):
```csharp
using System;
using CommandLine;
// (verbs imported via the Commands namespace below)

namespace Utinni.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            return Parser.Default
                .ParseArguments<ParseTreOptions, ListObjectsOptions, InspectIffOptions, ValidatePluginOptions>(args)
                .MapResult(
                    (ParseTreOptions       o) => Commands.ParseTreCommand.Run(o),
                    (ListObjectsOptions    o) => Commands.ListObjectsCommand.Run(o),
                    (InspectIffOptions     o) => Commands.InspectIffCommand.Run(o),
                    (ValidatePluginOptions o) => Commands.ValidatePluginCommand.Run(o),
                    errs => 1);  // exit 1 on usage error per D-02
        }
    }
}
```

**Allman braces / 4-space indent / no `_` prefix** — observe how PluginLoader.cs:37-50 declares fields (`public IEnumerable<IPlugin> Plugins;` without `_` underscore; PascalCase). Apply identically.

---

### `Utinni.Cli/Commands/ParseTreCommand.cs` (and siblings)

**Analog (structural):** `UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs` is the closest "Commands" sibling but is a different kind of command (game-side); use it only for namespace folder layout. For the run+exit-code+JSON-emit shape, see Research §Code Examples §3 (`ValidatePluginCommand`).

**Static class + Run(options) → int pattern** (matches Program.cs's MapResult callees):
```csharp
namespace Utinni.Cli.Commands
{
    [Verb("parse-tre", HelpText = "Parse a .tre archive and emit sorted-key JSON to stdout.")]
    public class ParseTreOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .tre file.")]
        public string Path { get; set; }
    }

    public static class ParseTreCommand
    {
        public static int Run(ParseTreOptions o)
        {
            if (!System.IO.File.Exists(o.Path))
                return Output.JsonOutput.EmitError("parse-tre", "FileNotFound", o.Path, exitCode: 3);
            try
            {
                var tre = UtinniCoreDotNet.Formats.Tre.TreFile.Open(o.Path);
                return Output.JsonOutput.EmitSuccess("parse-tre", BuildResult(tre));
            }
            catch (UtinniCoreDotNet.Formats.Tre.TreParseException ex)
            {
                return Output.JsonOutput.EmitError("parse-tre", ex.Kind.ToString(), ex.Message, exitCode: 2);
            }
        }
        // ...
    }
}
```

**Exit-code contract (D-02):** `0 ok / 1 usage / 2 parse-error / 3 fixture-not-found`. The MapResult in Program.cs handles `1`; each command's Run handles `0`, `2`, `3`.

---

### `Utinni.Cli/Commands/ValidatePluginCommand.cs`

**Analog (primary):** `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:72-141` — this command CONSUMES the existing `Load(string pluginDir)` test seam.

**Reuse pattern** (PluginLoader.cs:55-61 — the `autoLoad: false` ctor + `Load(pluginDir)` call sequence is exactly what tests already do):
```csharp
// From PluginLoaderTests.cs:88-89 — the exact invocation shape to copy:
var loader = new PluginLoader(autoLoad: false);
loader.Load(tempDir);

// Then iterate loader.Plugins + loader.LoadErrors. The shape of Plugins is
// IEnumerable<IPlugin> (see IPlugin.cs:44-49 for the [InheritedExport] contract).
```

**Reflection-over-IPlugin pattern** (novel; no exact analog — Research §Code Examples §3 is the canonical reference):
- `typeof(IEditorPlugin).IsAssignableFrom(plugin.GetType())` — kind discrimination.
- `plugin.Information?.Name / .Description / .Author` — pull from `IPlugin.Information` (IPlugin.cs:30-49).
- For hybrid plugins, `kernel32!GetProcAddress` for `createPlugin`/`destroyPlugin` (Phase 3 R-B D-13/D-14 contract).

**Security warning in --help text** (Research §Security Domain row "Plugin DLL load executes attacker-controlled code"): include a one-liner in the `[Verb(...)]` HelpText noting "loads each .dll under the given directory; only run against trusted plugin directories."

---

### `Utinni.Cli/Output/JsonOutput.cs` + `SortedKeyContractResolver.cs`

**Analog (stylistic only):** `UtinniCoreDotNet/Utility/Log.cs:33-50` (static utility class shape, conservative try/catch wrapping). The JSON envelope logic itself is novel — see Research §Pattern 2.

**Static-class + private-readonly settings field pattern** (Log.cs:34-50):
```csharp
namespace Utinni.Cli.Output
{
    public static class JsonOutput
    {
        private static readonly Newtonsoft.Json.JsonSerializerSettings Settings =
            new Newtonsoft.Json.JsonSerializerSettings
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
                ContractResolver = new SortedKeyContractResolver(),
            };

        public static int EmitSuccess(string command, object result) { /* ... */ }
        public static int EmitError(string command, string kind, string message, int exitCode) { /* ... */ }
    }
}
```

**Per-command-shape `schemaVersion` envelope** (D-10 + Research §Pitfall 7):
```json
{
  "command": "parse-tre",
  "result": {
    "schemaVersion": 1,
    "header": { ... },
    "records": [ ... ]
  }
}
```

**LF + UTF-8-no-BOM emission** (Research §Pattern 2 — the StreamWriter with `UTF8Encoding(false)` + `NewLine = "\n"` + `.Replace("\r\n", "\n")`). Defensive: always call `.Replace("\r\n", "\n")` on the serialised string before write, since `Formatting.Indented` will emit `Environment.NewLine`.

---

### `Utinni.Cli.Tests/Infrastructure/FixturePath.cs`

**Analog:** `UtinniCoreDotNet.Tests/PluginLoaderTests.cs:49-68` (FindFixtureDll) — this is the canonical AppContext.BaseDirectory walk pattern in this repo.

**Pattern to copy** (PluginLoaderTests.cs:49-68):
```csharp
private static string FindFixtureDll(string fixtureName)
{
    var baseDir = AppContext.BaseDirectory;
    var candidate = Path.GetFullPath(Path.Combine(
        baseDir, "..", "..", "Fixtures", fixtureName, "bin", "Release", "net472", fixtureName + ".dll"));
    if (File.Exists(candidate)) return candidate;
    // Fallback walk...
    throw new FileNotFoundException(...);
}
```

**Adaptation for Phase 4:** the fixtures live under `bin/Release/net472/Fixtures/<command>/<fixture>` thanks to the `<Content CopyToOutputDirectory="PreserveNewest">` glob (not in a nested csproj's bin dir). So the walk is simpler:
```csharp
public static string Resolve(string commandDir, string fixtureName)
{
    return System.IO.Path.Combine(
        System.AppContext.BaseDirectory, "Fixtures", commandDir, fixtureName);
}
```

---

### `Utinni.Cli.Tests/Infrastructure/InProcessCliRunner.cs`

**Analog:** none exact for Console.SetOut redirection. The static-class + result-record pattern matches `UtinniCoreDotNet/Utility/Log.cs:33-50`. The temp-dir lifecycle resembles `PluginLoaderTests.cs:70-75`.

**Full pattern** (Research §Pattern 3 — copy verbatim into the file, adapted to the namespace):
```csharp
public sealed class InProcessCliRunner
{
    public sealed class CliResult
    {
        public int ExitCode { get; set; }
        public string Stdout { get; set; }
        public string Stderr { get; set; }
    }
    public static CliResult Run(params string[] args) { /* SetOut/SetError, call Program.Main */ }
}
```

---

### `Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs`

**Analog (assertion helper shape):** `UtinniCoreDotNet.Tests/DirectoryBuildPropsTests.cs:52-80` (test-helper static methods + xUnit assertion pattern, MIT header).

**Static GoldenAssert + dump-on-failure pattern** (Research §Pattern 4 — copy adapted). Key elements:
- Load `expected.json` via `File.ReadAllText` + `.Replace("\r\n", "\n")` (D-10).
- `JToken.Parse(expected)` vs `JToken.Parse(actual)`.
- `JToken.DeepEquals(...)` boolean.
- On mismatch: `Directory.CreateDirectory(TestResults/<test-name>/)`, dump both files, throw `Xunit.Sdk.XunitException` with truncated head-of-diff in the message.
- Dump path under `AppContext.BaseDirectory` so CI's `actions/upload-artifact@v4` block picks them up (matches the existing ci.yml lines 86-92 pattern that uploads `*.trx`; Phase 4 adds `*.json` to that path).

---

### `Utinni.Cli.Tests/Commands/<Command>CommandTests.cs` (×4)

**Analog:** `UtinniCoreDotNet.Tests/PluginLoaderTests.cs` (entire file) — the test class shape, MIT header, [Fact]/[Theory] naming, temp-dir lifecycle, and Assert.* idioms are all directly transferable.

**Test class skeleton pattern** (PluginLoaderTests.cs:1-47 — copy verbatim adapted):
```csharp
/** [MIT header copied from PluginLoader.cs:1-23] **/

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Utinni.Cli.Tests.Commands
{
    public class ParseTreCommandTests
    {
        [Fact]
        public void Run_WithSynthesized3RecordTre_EmitsExpectedGolden()
        {
            var fixturePath = Infrastructure.FixturePath.Resolve("tre", "synthesized-3record.tre");
            var result = Infrastructure.InProcessCliRunner.Run("parse-tre", fixturePath);
            Assert.Equal(0, result.ExitCode);
            Infrastructure.GoldenTestRunner.Matches("tre/synthesized-3record", result.Stdout);
        }
        // ... negative cases + real-tiny case ...
    }
}
```

**Test naming convention** (Phase 1 D-04 — `[Method]_[Scenario]_[ExpectedOutcome]`): PluginLoaderTests.cs:78 = `Load_WithOneBrokenAndOneGoodPlugin_LoadsGoodAndLogsBroken`. Apply identically.

---

### `UtinniCoreDotNet/Formats/Tre/TreFile.cs` (+ TreHeader, TreRecord, TreParseException)

**Analog (stylistic only):** `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:35-50` (class shape, MIT header, namespace organisation). The binary-parser logic itself is novel — first format parser in the repo. See Research §Code Examples §1 and §"Format-Specific Knowledge §TRE" for the implementation reference.

**MIT header with D-01 disposition note** (planner appends this line under the standard MIT block):
```csharp
/** MIT License ... [23-line block from PluginLoader.cs:1-23] **/
// Format understood by reading swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}
// (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. No code,
// comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.
```

**Class + namespace pattern** (PluginLoader.cs:35-50):
```csharp
using System;
using System.IO;

namespace UtinniCoreDotNet.Formats.Tre
{
    public sealed class TreFile
    {
        public TreHeader Header { get; }
        public IReadOnlyList<TreRecord> Records { get; }

        public static TreFile Open(string path) { /* ... */ }
    }
}
```

**Allman braces, 4-space indent, PascalCase, no underscore prefix** — observe PluginLoader.cs throughout. Field declarations like `public IEnumerable<IPlugin> Plugins;` (no underscore). Private static methods like `LoadCatalog` (PascalCase). Apply identically.

---

### `UtinniCoreDotNet/Formats/Iff/IffReader.cs` (+ ContainerChunk, LeafChunk, IffParseException)

**Analog:** sibling `TreFile.cs` once it exists; otherwise same as TRE — `UtinniCoreDotNet/PluginFramework/PluginLoader.cs` stylistic only.

**Endianness separation** (Research §"Endianness pitfall"): IFF is big-endian; TRE is little-endian. Do not share a `BinaryReader` subclass — keep `IffReader` and `TreFile` in separate namespaces with separate read helpers (`ReadInt32BE` for IFF, default `br.ReadInt32()` LE for TRE).

---

### `UtinniCoreDotNet.Tests/FormatsTests/Tre/TreFileTests.cs` (+ `Iff/IffReaderTests.cs`)

**Analog:** `UtinniCoreDotNet.Tests/HotkeyTests.cs` (entire file) — pure-managed Tier-1 test, no native deps, no fixture-DLL dance. This is the cleanest template for parser unit tests.

**Pattern to copy** (HotkeyTests.cs:1-83):
```csharp
/** [MIT header from HotkeyTests.cs:1-23] **/

using System.IO;
using Xunit;
using UtinniCoreDotNet.Formats.Tre;

namespace UtinniCoreDotNet.Tests.FormatsTests.Tre
{
    public class TreFileTests
    {
        [Fact]
        public void Open_ValidV5000Header_ReadsExpectedRecordCount()
        {
            byte[] bytes = BuildHeader(version: "0005", recordCount: 3);
            using (var ms = new MemoryStream(bytes))
            {
                var tre = TreFile.Open(ms);  // overload that takes a Stream for testability
                Assert.Equal(3, tre.Header.ResourceCount);
            }
        }

        [Fact]
        public void Open_BadMagic_ThrowsBadMagicException()
        {
            byte[] bytes = BuildHeader(magic: "XXXX");
            using (var ms = new MemoryStream(bytes))
            {
                var ex = Record.Exception(() => TreFile.Open(ms));
                Assert.IsType<TreParseException>(ex);
                Assert.Equal(TreParseError.BadMagic, ((TreParseException)ex).Kind);
            }
        }

        private static byte[] BuildHeader(...) { /* fixture builder */ }
    }
}
```

**Folder-based nesting per Research §Pitfall 4** (`FormatsTests/Tre/` not flat `TreFileTests.cs`). Namespace follows folder: `UtinniCoreDotNet.Tests.FormatsTests.Tre`.

---

### `Utinni.Cli.Tests/Fixtures/{tre,iff,plugins,world-snapshot}/` (golden fixture directories)

**Analog:** `UtinniCoreDotNet.Tests/Fixtures/{BrokenPlugin,GoodPlugin}/` — directory-per-fixture pattern.

**Differences from existing pattern:**
- Existing fixtures are csprojs that produce DLLs. New fixtures are static `.tre` / `.iff` / `.json` files + (for `plugins/valid-plugin/`) a copy of `Utinni.CrtMatchPlugin.dll` placed there by the CopyValidPluginFixture Target.
- No DefaultItemExcludes needed (no nested csprojs).
- `<Content CopyToOutputDirectory="PreserveNewest">` glob copies them to `bin/Release/net472/Fixtures/...`.

**Per-fixture `expected.json` pattern** (Research §Pattern 4):
```
Fixtures/
├── tre/
│   ├── synthesized-3record.tre
│   ├── synthesized-3record.expected.json
│   ├── malformed-magic.tre
│   └── malformed-magic.expected.json
```

---

## Shared Patterns

### MIT License Header (apply to EVERY new .cs file)

**Source:** `UtinniCoreDotNet/PluginFramework/PluginLoader.cs:1-23` (also IPlugin.cs:1-23, HotkeyTests.cs:1-23, GoodPlugin.cs:1-23 — all identical)
**Apply to:** all new files under `Utinni.Cli/`, `Utinni.Cli.Tests/`, `UtinniCoreDotNet/Formats/`, `UtinniCoreDotNet.Tests/FormatsTests/`

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

**D-01 disposition addendum for parser files only** (`UtinniCoreDotNet/Formats/Tre/*.cs` + `Formats/Iff/*.cs`):
```csharp
// Format understood by reading swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}
// (SOE/Bootprint, All Rights Reserved) and the EA-IFF-85 public standard. No code,
// comments, identifier names, or test fixtures copied from any reference source.
// Implementation original to Utinni under MIT.
```

**Note:** `Utinni.Cli/`, `Utinni.Cli.Tests/`, `UtinniCoreDotNet/Formats/PluginManifest/`, and `UtinniCoreDotNet.Tests/FormatsTests/` do NOT need the disposition addendum — only the format-reference-read files do.

---

### Allman Braces + 4-space + PascalCase (apply to EVERY new .cs file)

**Source:** every existing C# file in the repo — `PluginLoader.cs`, `IPlugin.cs`, `HotkeyTests.cs`, `PluginLoaderTests.cs`, `GoodPlugin.cs`, `BrokenPlugin.cs`.
**Apply to:** all new C# files in Phase 4.

**Style elements visible across analogs:**
- Opening brace on its own line (Allman). E.g. `public class PluginLoader` { newline `{` newline body.
- 4-space indent, no tabs.
- PascalCase for types, methods, properties. camelCase for parameters + locals.
- Public fields ok (PluginLoader.cs:39: `public IEnumerable<IPlugin> Plugins;`) — no underscore prefix.
- `using` directives grouped: BCL first (`using System;` `using System.IO;`), then project (`using UtinniCoreDotNet.PluginFramework;`).
- `private static readonly` for shared settings/sentinels (Log.cs:45-49).

---

### Solution File Project Entry + Configuration Mapping

**Source:** `Utinni.sln` lines 17-21 (UtinniCoreDotNet entry — C# project with x86 platform) + lines 78-83 (its config block in the SolutionConfigurationPlatforms section).
**Apply to:** Plan 04-01 — add two project entries for `Utinni.Cli.csproj` and `Utinni.Cli.Tests.csproj` plus the matching Debug|x86 / Release|x86 / RelWithDbgInfo|x86 config rows.

**C# project entry pattern** (Utinni.sln:17-21):
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Utinni.Cli", "Utinni.Cli\Utinni.Cli.csproj", "{<NEW-GUID>}"
	ProjectSection(ProjectDependencies) = postProject
		{39AB8A43-B916-4C6E-87DD-928B438CAE68} = {39AB8A43-B916-4C6E-87DD-928B438CAE68}
	EndProjectSection
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Utinni.Cli.Tests", "Utinni.Cli.Tests\Utinni.Cli.Tests.csproj", "{<NEW-GUID>}"
	ProjectSection(ProjectDependencies) = postProject
		{39AB8A43-B916-4C6E-87DD-928B438CAE68} = {39AB8A43-B916-4C6E-87DD-928B438CAE68}
	EndProjectSection
EndProject
```

(GUID `{39AB8A43-...}` = UtinniCoreDotNet — both new projects depend on it.)

**Configuration mapping pattern** (Utinni.sln:78-83 — UtinniCoreDotNet's row; new C# projects follow this `Debug|x86 = Debug|x86` pattern, NOT the C++ projects' `Debug|x86 = Debug|Win32` pattern):
```
{<NEW-GUID>}.Debug|x86.ActiveCfg = Debug|x86
{<NEW-GUID>}.Debug|x86.Build.0 = Debug|x86
{<NEW-GUID>}.Release|x86.ActiveCfg = Release|x86
{<NEW-GUID>}.Release|x86.Build.0 = Release|x86
{<NEW-GUID>}.RelWithDbgInfo|x86.ActiveCfg = RelWithDbgInfo|x86
{<NEW-GUID>}.RelWithDbgInfo|x86.Build.0 = RelWithDbgInfo|x86
```

**GUID generation:** standard new-GUID generator (e.g. `[guid]::NewGuid()` in PowerShell, or any GUID generator). Each new project gets its own GUID.

---

### CI Workflow Extension (Plan 04-01)

**Source:** `.github/workflows/ci.yml` lines 81-92 (the existing "Run tests" + "Upload test results" steps).
**Apply to:** extend the same job after the existing `Run tests (net472 / x86)` step.

**Existing step pattern to extend after** (ci.yml lines 81-84):
```yaml
- name: Run tests (net472 / x86)
  run: dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release --logger "console;verbosity=normal" --logger "trx;LogFileName=test-results.trx"
  # Project-targeted (not `dotnet test Utinni.sln`) per RESEARCH §Pitfall 2 — dotnet/sdk#9007 + microsoft/vstest#1129 confirm the solution form fails on mixed C++/C# solutions.
  # --no-build because msbuild above already produced bin/Release/net472/UtinniCoreDotNet.Tests.dll.
```

**New step pattern (D-11)** — copy the structure verbatim, change the project path + logger name:
```yaml
- name: Run CLI golden tests (net472 / x86)
  run: dotnet test Utinni.Cli.Tests/Utinni.Cli.Tests.csproj --no-build --configuration Release --logger "console;verbosity=normal" --logger "trx;LogFileName=cli-test-results.trx"
  # Phase 4 D-11: second test lane gates `master` on golden suite.
  # Same --no-build + project-targeted reasoning as the previous step.

- name: Upload CLI test artifacts (on failure)
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: cli-test-results
    path: |
      Utinni.Cli.Tests/TestResults/**/*.trx
      Utinni.Cli.Tests/bin/Release/net472/TestResults/**/*.json
    if-no-files-found: warn
```

The existing `Upload test results (on failure)` block at ci.yml:86-92 is the exact template for the new artifact-upload step — copy lines 86-92 and rename `name:` and `path:`.

---

### Test-fixture-path resolution (apply to every Tier-2 golden test + parser test that loads a file)

**Source:** `UtinniCoreDotNet.Tests/PluginLoaderTests.cs:49-68` (FindFixtureDll).
**Apply to:** `Utinni.Cli.Tests/Infrastructure/FixturePath.cs` + any test that resolves a fixture path.

```csharp
var baseDir = AppContext.BaseDirectory;
var candidate = Path.GetFullPath(Path.Combine(baseDir, "Fixtures", "<command>", "<file>"));
if (!File.Exists(candidate)) throw new FileNotFoundException(...);
```

(Phase 4's `<Content>` copy mechanic places fixtures directly under `bin/.../Fixtures/`, so the walk is simpler than PluginLoaderTests' `../../Fixtures/` nested-csproj walk.)

---

### packages.lock.json (Plan 04-01)

**Source:** `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj:15` (`<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`).
**Apply to:** both new csprojs (`Utinni.Cli.csproj`, `Utinni.Cli.Tests.csproj`). After first `dotnet restore`, commit the resulting `packages.lock.json` per Research §"Package Legitimacy Audit" planner directive (checkpoint:human-verify on hashes).

---

## No Analog Found

Files with no close in-repo match (planner should use RESEARCH.md patterns as primary guidance):

| File | Role | Data Flow | Reason | RESEARCH.md reference |
|------|------|-----------|--------|-----------------------|
| `Utinni.Cli/Program.cs` | CLI entry + verb dispatch | request-response | Repo has no prior CLI; CommandLineParser MapResult shape is new | §Architecture Patterns §Pattern 1 (verb-based dispatch with exit-code propagation) |
| `Utinni.Cli/Commands/*.cs` | verb handlers | request-response → JSON-out | Greenfield CLI surface | §Code Examples §3 (ValidatePluginCommand prototype shows shape) |
| `Utinni.Cli/Output/JsonOutput.cs` | sorted-key indented JSON envelope helper | transform | Repo has no existing JSON output layer | §Architecture Patterns §Pattern 2 (stable-JSON envelope + sorted-key contract resolver) — note D-10 + Pitfall 10 |
| `Utinni.Cli/Output/SortedKeyContractResolver.cs` | Newtonsoft IContractResolver subclass | transform | Framework-subclass pattern; repo doesn't currently consume Newtonsoft outside test deps | §Architecture Patterns §Pattern 2 + §Pitfall 10 (JObject keys need recursive sort, not just contract resolver) |
| `Utinni.Cli.Tests/Infrastructure/InProcessCliRunner.cs` | Console.SetOut redirection for in-process Main() invocation | event-driven | First test in repo to redirect stdout | §Architecture Patterns §Pattern 3 (in-process CLI invocation with stdout redirection) |
| `Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs` | JToken.DeepEquals + diff dump | transform | First golden-comparison helper in repo | §Architecture Patterns §Pattern 4 (golden-file comparison via JToken.DeepEquals with diff dump) |
| `UtinniCoreDotNet/Formats/Tre/TreFile.cs` (+ types) | TRE binary container parser | file-I/O (read) | First format parser in repo | §Format-Specific Knowledge §TRE container format + §Code Examples §1 |
| `UtinniCoreDotNet/Formats/Iff/IffReader.cs` (+ types) | IFF chunk recursive-descent reader | file-I/O (read) | First format parser in repo | §Format-Specific Knowledge §IFF chunk format + §Code Examples §2 |
| `.gitattributes` | git eol normalization config | n/a | No existing `.gitattributes` in repo (Grep confirmed) | §Common Pitfalls §Pitfall 6 (CR/LF normalization is mandatory cross-machine) |

For all of the above, RESEARCH.md provides the canonical idiom; this repo provides MIT header + Allman + namespace conventions (apply via the §"Shared Patterns" section above).

---

## Metadata

**Analog search scope:** `UtinniCoreDotNet/`, `UtinniCoreDotNet.Tests/`, `Utinni.sln`, `.github/workflows/`, `Utinni.LoaderLockHarness/`, `Utinni.CrtMatchPlugin/`, `Utinni.LegacyPlugin/`, repo root for `.gitattributes`.
**Files scanned (read in full or grep-located):** 12.
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj`
- `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj`
- `UtinniCoreDotNet.Tests/Fixtures/GoodPlugin/GoodPlugin.csproj`
- `UtinniCoreDotNet.Tests/Fixtures/BrokenPlugin/BrokenPlugin.csproj`
- `UtinniCoreDotNet.Tests/Fixtures/GoodPlugin/GoodPlugin.cs`
- `UtinniCoreDotNet.Tests/Fixtures/BrokenPlugin/BrokenPlugin.cs`
- `UtinniCoreDotNet.Tests/PluginLoaderTests.cs`
- `UtinniCoreDotNet.Tests/HotkeyTests.cs`
- `UtinniCoreDotNet.Tests/DirectoryBuildPropsTests.cs` (excerpt)
- `UtinniCoreDotNet/PluginFramework/PluginLoader.cs`
- `UtinniCoreDotNet/PluginFramework/IPlugin.cs`
- `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs`
- `UtinniCoreDotNet/Utility/Log.cs` (excerpt)
- `Utinni.sln`
- `.github/workflows/ci.yml`
- `.planning/phases/04-tier-2-cli-shim-golden-fixtures/04-CONTEXT.md`
- `.planning/phases/04-tier-2-cli-shim-golden-fixtures/04-RESEARCH.md`

**Pattern extraction date:** 2026-05-23

## PATTERN MAPPING COMPLETE
