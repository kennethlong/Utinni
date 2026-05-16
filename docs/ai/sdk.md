# SDK & templates

> Audience: anyone starting a new plugin.

Utinni ships first-party project templates and example plugins under
[`sdk/`](../../sdk/). Two delivery paths:

- **Visual Studio templates via a VSIX** — `sdk/UtinniPluginTemplates/Vsix/`
  packages a project-template extension that adds *UtinniDotNetPlugin* and
  *UtinniDotNetEditorPlugin* entries to the New Project dialog. Recommended
  for .NET plugin authors.
- **Copy-from-template** — the `sdk/UtinniCppPluginTemplate/` and
  `sdk/examples/*` directories are full projects you can clone. Required for
  native C++ plugins (no VSIX for those).

## What's in `sdk/`

```
sdk/
├── UtinniCppPluginTemplate/
│   ├── UtinniCppPlugin.props        ← shared MSBuild props for C++ plugins
│   ├── UtinniCppPluginTemplate.sln
│   └── UtinniCppPluginTemplate/
│       ├── UtinniCppPluginTemplate.vcxproj
│       ├── plugin.cpp                ← minimal subclass of utinni::UtinniPlugin
│       ├── UtinniPlugin.rc + resource.h
│
├── UtinniPluginTemplates/
│   ├── DotNetPluginTemplate/         ← runtime-plugin (.IPlugin) starter
│   │   ├── DotNetPluginTemplate.csproj      (template-source project)
│   │   ├── DotNetPluginTemplate.vstemplate  (manifest)
│   │   ├── ProjectTemplate.csproj           ← the actual instantiated csproj
│   │   ├── Plugin.cs                         ← Plugin class with $projectname$ tokens
│   │   ├── AssemblyInfo.cs
│   │   └── TJT.ico
│   ├── DotNetEditorPluginTemplate/   ← editor-plugin (IEditorPlugin) starter
│   │   └── (same shape, with extra UI/Forms + UI/SubPanels folders)
│   └── Vsix/                          ← packages both into a VS extension
│       ├── Vsix.csproj
│       ├── PluginTemplateWizardPackage.cs
│       ├── source.extension.vsixmanifest
│       ├── Wizards/DotNetSolutionWizard.cs    ← runs at New Project time
│       ├── Utility/Props.cs                    ← writes Directory.Build.props
│       └── key.snk
│
└── examples/
    ├── ExampleCppPlugin/             ← fully-built native plugin
    │   ├── ExampleCppPlugin.vcxproj
    │   ├── plugin.cpp
    │   └── example_command_parser.h / .cpp
    └── ExampleEditorPlugin/          ← fully-built editor plugin
        ├── ExampleEditorPlugin.csproj
        ├── ExampleEditorPlugin.cs
        ├── ExampleEditorSubPanel.cs
        ├── ExampleEditorSubPanel.Designer.cs
        ├── ExampleEditorSubPanel.resx
        └── Properties/AssemblyInfo.cs
```

## .NET plugin via VSIX (recommended)

### Install

1. Open `sdk/UtinniPluginTemplates/Vsix/Vsix.csproj` in Visual Studio 2019
   (the manifest currently targets `[16.0,17.0)`).
2. Build → produces `bin/Release/Vsix.vsix`.
3. Double-click the `.vsix` → installer adds the templates.

After install, **File → New → Project** offers:

- **UtinniDotNetPlugin** — runtime plugin (`IPlugin`).
- **UtinniDotNetEditorPlugin** — editor plugin (`IEditorPlugin`).

### What the wizard does

`Wizards/DotNetSolutionWizard.cs` runs on project creation:

1. **First-project guard.** Detects whether the new project will live in an
   existing solution or a fresh one.
2. **`Props.CreateDotNetDirectoryProps()`** — writes (or updates)
   `Directory.Build.props` at the solution root, defining:
   - `$(PluginOutputDir)` — `$(SolutionDir)\bin\$(Configuration)\Plugins\$(ProjectName)\`
   - `$(UtinniCoreDotNetPath)` — relative path the user must edit to point at
     the built `UtinniCoreDotNet.dll`.
   - Per-config property groups for Debug / Release / RelWithDbgInfo, each
     pinned to `x86` and `LangVersion=7.3`.
3. **RelWithDbgInfo.** Adds the configuration to the new solution if it's
   not already there.

### Token substitution

`ProjectTemplate.csproj` and `Plugin.cs` use VSTemplate placeholders:

| Placeholder                     | Substitution                                          |
| ------------------------------- | ----------------------------------------------------- |
| `$guid1$`                       | new project GUID                                      |
| `$safeprojectname$`             | project name with bad characters stripped             |
| `$projectname$`                 | project name as typed                                 |
| `$targetframeworkversion$`      | matches the user's selected .NET target               |

After instantiation, `Plugin.cs` has e.g. `MyAwesomePlugin` everywhere
`$projectname$` appeared.

## .NET plugin without the VSIX

If you don't want to install the extension, copy the relevant `Plugin.cs` and
`.csproj` content into your own project and reference `UtinniCoreDotNet.dll`
with `HintPath`:

```xml
<ItemGroup>
  <Reference Include="UtinniCoreDotNet">
    <HintPath>$(UtinniCoreDotNetPath)UtinniCoreDotNet.dll</HintPath>
    <Private>False</Private>
  </Reference>
  <Reference Include="System.Windows.Forms" />
  <Reference Include="System.Drawing" />
</ItemGroup>
```

Key project properties:

```xml
<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
<PlatformTarget>x86</PlatformTarget>
<OutputType>Library</OutputType>
<LangVersion>7.3</LangVersion>
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

Configure your output to write into `bin/<Config>/Plugins/<PluginName>/`
either via `OutputPath` or a `Directory.Build.props` like the wizard's.

## C++ plugin

1. Copy `sdk/UtinniCppPluginTemplate/` somewhere — typically into the same
   solution that builds `UtinniCore` so the project reference resolves.
2. Rename the folder, `.sln`, `.vcxproj`, and any internal class/file names
   from `UtinniCppPluginTemplate` to your name.
3. Edit `plugin.cpp` — set `Information { name, description, author }` and
   anything you want to run in `init()`.
4. Build.

The shared `UtinniCppPlugin.props`:

```
<PropertyGroup>
  <OutDir>$(SolutionDir)bin\$(Configuration)\Plugins\$(ProjectName)\</OutDir>
  <IntDir>$(ProjectDir)obj\$(Configuration)\</IntDir>
</PropertyGroup>
<ItemDefinitionGroup>
  <ClCompile>
    <PreprocessorDefinitions>SPDLOG_NO_EXCEPTIONS;$(Configuration)=DEBUG;%(Preproc...)</PreprocessorDefinitions>
    <LanguageStandard>stdcpp17</LanguageStandard>
    <AdditionalIncludeDirectories>...UtinniCore;...external;%(...)</AdditionalIncludeDirectories>
  </ClCompile>
  <Link>
    <AdditionalDependencies>UtinniCore.lib;%(AdditionalDependencies)</AdditionalDependencies>
  </Link>
</ItemDefinitionGroup>
```

so any `.vcxproj` that `<Import>`s this props file inherits the right
defines, include paths, and link line. Adapt for your own plugin if you
move the project tree.

## Example plugins

### `examples/ExampleCppPlugin`

```cpp
class ExampleUtinniPlugin : public utinni::UtinniPlugin
{
    Information info = { "Example C++ Plugin",
                         "Adds 'example' slash command",
                         "ptklatt" };
public:
    void init() override {
        utinni::CuiChatWindow::addCreateCommandParserCallback(
            &example::ExampleCommandParser::create);
    }
    const Information& getInformation() override { return info; }
};

UTINNI_PLUGIN { return new ExampleUtinniPlugin(); }
```

`example_command_parser.{h,cpp}` subclasses `utinni::CommandParser` to add a
single command. Read these for the canonical "add a slash command" pattern.

### `examples/ExampleEditorPlugin`

The most useful reference for new .NET editor plugin authors:

- **`ExampleEditorPlugin.cs`** — implements `IEditorPlugin`, returns a single
  `SubPanel` from `GetSubPanels()`, logs a creation message.
- **`ExampleEditorSubPanel.cs`** — a WinForms `UserControl` (technically a
  `SubPanel`) that:
  - Subscribes to `ObjectCallbacks.AddOnTargetCallback`, displays the
    targeted object's filename in a label.
  - Subscribes to `ImGuiCallbacks.AddOnPositionChangedCallback` and
    `AddOnRotationChangedCallback`, displays current position.
  - Has buttons to add / remove a world-snapshot node at the targeted
    object's position, demonstrating undo command emission.
  - Uses `GroundSceneCallbacks.AddUpdateLoopCall` to read state per-frame
    and marshal to UI via async `Task.Delay` loop.

Reading this end-to-end is the fastest way to understand "what does a
working editor plugin look like."

## Build outputs (final layout)

A built install ready for the SWG client folder:

```
<install>/
├── Launcher.exe
├── UtinniCore.dll
├── UtinniCoreDotNet.dll
├── ut.ini
├── utinni.cfg
└── Plugins/
    ├── ExampleCppPlugin/
    │   └── ExampleCppPlugin.dll
    ├── ExampleEditorPlugin/
    │   └── ExampleEditorPlugin.dll
    └── TheJawaToolbox/
        ├── TheJawaToolbox.dll
        ├── TheJawaToolboxDotNet.dll
        ├── settings.ini       (created on first run)
        └── input.ini          (HotkeyManager)
```

Followed by the line in `ut.ini`:

```ini
[Plugins]
plugin_0 = true,ExampleCppPlugin
plugin_1 = true,ExampleEditorPlugin
plugin_2 = true,TheJawaToolbox
```

## See also

- [Tutorial](tutorial.md) — step-by-step from VSIX install to first SubPanel.
- [Build & run](build.md) — get a usable `UtinniCore.dll` /
  `UtinniCoreDotNet.dll` first.
- [Regenerating bindings](regen-bindings.md) — only relevant if you change
  the C++ public headers.
- The Jawa Toolbox as a complete worked example:
  [UtinniPlugins/docs/jawa-toolbox.md](../../../UtinniPlugins/docs/ai/jawa-toolbox.md).
