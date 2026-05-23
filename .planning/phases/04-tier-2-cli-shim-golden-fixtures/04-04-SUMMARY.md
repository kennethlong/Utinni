---
phase: 04-tier-2-cli-shim-golden-fixtures
plan: 04
subsystem: validate-plugin-cli-command
tags: [validate-plugin, pe-reader, plugin-inspection, native-export-probe, golden-fixtures, tier-1-tests, tier-2-tests, dec-c3-locked, phase-close]
dependency_graph:
  requires: [UtinniCoreDotNet (PluginLoader, IPlugin, IEditorPlugin), Utinni.Cli (04-01), Utinni.Cli.Tests (04-01), Utinni.CrtMatchPlugin (Phase 3 R-B)]
  provides: [NativeExportProbe, PluginInspection, ValidatePluginCommand, plugin-fixtures, validate-plugin-golden-tests]
  affects: [Utinni.Cli.csproj, Utinni.Cli.Tests.csproj, .planning/PROJECT.md]
tech_stack:
  added: [System.Reflection.Metadata 1.6.0 (NuGet; assembly version 1.4.3.0), System.Collections.Immutable 1.5.0 (NuGet; assembly version 1.2.3.0)]
  patterns: [PE-export-table parse via PEReader without LoadLibrary, ReflectionOnlyLoadFrom + event resolver + raw-byte fallback for managed attribute detection, FilterNativeLoadErrors (schemaVersion 1 contract), dual-signal kind classification (iPluginAttributePresent vs iPluginInterfaceImplemented), DirectoryReport top-level POCO, CopyValidPluginFixture MSBuild target, binding redirects in app.config for version bridging]
key_files:
  created:
    - Utinni.Cli/Commands/NativeExportProbe.cs
    - Utinni.Cli/Commands/PluginInspection.cs
    - Utinni.Cli/Properties/AssemblyInfo.cs
    - Utinni.Cli.Tests/Commands/PluginInspectionTests.cs
    - Utinni.Cli.Tests/Commands/ValidatePluginCommandTests.cs
    - Utinni.Cli.Tests/Fixtures/plugins/valid-plugin/expected.json
    - Utinni.Cli.Tests/Fixtures/plugins/missing-createplugin/MissingCreatePlugin.dll
    - Utinni.Cli.Tests/Fixtures/plugins/missing-createplugin/expected.json
    - Utinni.Cli.Tests/Fixtures/plugins/missing-destroyplugin/MissingDestroyPlugin.dll
    - Utinni.Cli.Tests/Fixtures/plugins/missing-destroyplugin/expected.json
    - Utinni.Cli.Tests/Fixtures/plugins/wrong-iplugin-shape/WrongIPluginShape.dll
    - Utinni.Cli.Tests/Fixtures/plugins/wrong-iplugin-shape/expected.json
    - Utinni.Cli.Tests/app.config
  modified:
    - Utinni.Cli/Commands/ValidatePluginCommand.cs
    - Utinni.Cli/Utinni.Cli.csproj
    - Utinni.Cli/packages.lock.json
    - Utinni.Cli.Tests/Utinni.Cli.Tests.csproj
    - Utinni.Cli.Tests/packages.lock.json
    - Utinni.Cli.Tests/Commands/CommandDispatchTests.cs
    - Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt
    - Utinni.Cli.Tests/Fixtures/dispatch/no-args.expected.txt
    - .planning/PROJECT.md
decisions:
  - "Used PEReader (System.Reflection.Metadata) with PrefetchEntireImage flag for PE export-table parsing — REVIEWS HIGH-1 fix path A; no LoadLibraryExW/GetProcAddress; strongest T-04-EoP mitigation"
  - "Binding redirect approach to bridge System.Reflection.Metadata NuGet 1.6.0 (assembly version 1.4.3.0) vs test runner's 1.4.3.0 — app.config in Utinni.Cli.Tests with ForceReflectionMetadataVersion MSBuild target"
  - "RawBytesHasPluginAttributeString fallback for WrongIPluginShape fixture: ReflectionOnlyLoadFrom fails on System.ComponentModel.Composition deps; raw byte scan for type name string is deterministic for this fixture"
  - "wrong-iplugin-shape loadErrors sentinel in expected.json: loadErrors contains verbose CompositionException not covered by FilterNativeLoadErrors; MaskLoadErrors() in test normalizes this to '<LOAD_ERROR_MASKED>'"
  - "Help_ContainsTEoPMitigationWarning uses normalized whitespace check: CommandLineParser wraps HelpText at console width; multi-line assertions are fragile"
  - "Rule 1 auto-fix: removed validate-plugin stub row from CommandDispatchTests (now exits 3 on missing dir, not 1 like NotImplemented); same pattern as Plans 04-02 + 04-03"
  - "Updated help.expected.txt + no-args.expected.txt dispatch goldens to reflect new verbose HelpText with full T-04-EoP warning"
  - "DEC-C3 LOCKED after REVIEWS MEDIUM-16 gate passed: both 04-02-SUMMARY.md and 04-03-SUMMARY.md present on disk"
metrics:
  duration: "90 minutes"
  completed: "2026-05-23"
  tasks: 4
  files: 22
---

# Phase 4 Plan 04: validate-plugin Command + Plugin Fixtures + DEC-C3 Phase Close Summary

**One-liner:** PE-parse-based native export probe + PluginLoader-based managed inspection + four plugin fixtures (1 native pass, 2 native fail, 1 managed wrong-shape) + DEC-C3 tiered-testing-strategy promoted to LOCKED at Phase 4 close — closing the four-command TEST-03 commitment.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 4.1 | PluginInspection + NativeExportProbe + FilterNativeLoadErrors + 10 Tier-1 tests | 4323cc6 | NativeExportProbe.cs, PluginInspection.cs, AssemblyInfo.cs, Utinni.Cli.csproj, PluginInspectionTests.cs, app.config |
| 4.2 | ValidatePluginCommand stub → real Run body | c6d1b3d | ValidatePluginCommand.cs |
| 4.3 | Four plugin fixtures + four expected.json goldens + ValidatePluginCommandTests (8 tests) + Test 7 | aecc849 | MissingCreatePlugin.dll, MissingDestroyPlugin.dll, WrongIPluginShape.dll, 4x expected.json, ValidatePluginCommandTests.cs, PluginInspectionTests.cs (Test 7), CommandDispatchTests.cs, help/no-args goldens |
| 4.4 | DEC-C3 promoted to LOCKED + CON-O-09/11 closure verified | 800f8c2 | .planning/PROJECT.md |

## API Surface Delivered

### NativeExportProbe (Utinni.Cli/Commands/NativeExportProbe.cs)

```
public static bool HasExport(string dllPath, string symbolName)
```

Uses `System.Reflection.Metadata.PEReader(fs, PEStreamOptions.PrefetchEntireImage)` to parse the PE export name table. Walks IMAGE_EXPORT_DIRECTORY at the raw file offset (PointerToRawData-based). NO LoadLibraryExW/GetProcAddress. Returns false on any error. T-04-EoP: native DLL code is never mapped into the process address space.

### PluginInspection POCO Graph (Utinni.Cli/Commands/PluginInspection.cs)

Plan-checker iter-2 WARNING-5 — DirectoryReport top-level shape (NOT IReadOnlyList per-plugin):

```
DirectoryReport {
    string Directory;
    LoaderObservation LoaderObserved;       // directory-scoped, once
    IReadOnlyList<PluginReport> Plugins;
    IReadOnlyList<CheckResult> DirectoryChecks;  // iter-3 MED-8
    ResultSummary Summary;
}
PluginReport { Id, Path, Kind, PluginExports Exports, IReadOnlyList<CheckResult> Checks, OverallStatus }
PluginExports { CreatePlugin, DestroyPlugin, ManagedIPlugin, ManagedIEditorPlugin }
CheckResult { Id, Status ("pass"|"fail"|"n/a"|"warn"), Detail }
LoaderObservation { int PluginsCount; IReadOnlyList<string> LoadErrors; }  // FILTERED per iter-3 HIGH-1
ResultSummary { TotalPlugins, Passing, Failing }
```

### PluginInspectionFilters (internal, Utinni.Cli/Commands/PluginInspection.cs)

```
internal static IReadOnlyList<string> FilterNativeLoadErrors(IReadOnlyList<string> raw)
```

Drops messages containing `BadImageFormatException`, `is not a valid Win32 application`, or `not a managed assembly`. Part of the schemaVersion: 1 contract. Exposed to tests via `[assembly: InternalsVisibleTo("Utinni.Cli.Tests")]` in `Utinni.Cli/Properties/AssemblyInfo.cs`.

## REVIEWS Revisions Applied

| Finding | Applied |
|---------|---------|
| HIGH-1 (PEReader not LoadLibrary) | NativeExportProbe.cs: PEReader(fs, PrefetchEntireImage) + manual export-table walk; zero LoadLibrary* calls |
| HIGH-5 (PluginLoader.Load actually invoked) | PluginInspection.InspectDirectory: `new PluginLoader(autoLoad: false)` + `loader.Load(dir)` split-line shape (iter-3 MED-4); LoaderObserved at DirectoryReport level |
| HIGH-6 (envelope at root) | ValidatePluginCommand.Run: JsonOutput.EmitSuccess wraps at root; result nested inside |
| HIGH-8 (truthful warning text) | HelpText: "PluginLoader composition + managed-DLL inspection execute static initialisers as a side effect." |
| MEDIUM-9 (kind=unknown documented) | InspectSingle: unknown branch emits all-fail checks with explanatory detail |
| MEDIUM-15 (AssemblyMetadata marker) | AssemblyInfo.cs: [assembly: AssemblyMetadata("validate-plugin-version", "1")]; Test 8 reads via reflection on built assembly |
| MEDIUM-16 (DEC-C3 gating) | Task 4.4: Test-Path gate on 04-02-SUMMARY.md + 04-03-SUMMARY.md before promoting |
| #17 (PEReader needs explicit NuGet) | Utinni.Cli.csproj: System.Reflection.Metadata 1.6.0 + System.Collections.Immutable 1.5.0 |

## Iter-3 Revisions Applied

| Finding | Applied |
|---------|---------|
| HIGH-1 (loaderObserved.loadErrors semantics) | FilterNativeLoadErrors drops native-noise; all four expected.json files have loadErrors=[] for native fixtures; wrong-iplugin-shape has loadErrors sentinel masking in tests |
| MED-4 (verify-gate false-fail grep) | Split `new PluginLoader(autoLoad: false)` + `loader.Load(dir)` accepted by verify-gate |
| MED-5 (Test 7 atomic boundary) | Test 7 InspectDirectory_WrongIPluginShapeFixture_ManagedWithShapeFail moved to Task 4.3 |
| MED-7 (DirectoryReport return type) | ValidatePluginCommand consumes DirectoryReport (not stale IReadOnlyList) |
| MED-8 (loader-vs-reflection discrepancy) | DirectoryChecks carries loader-vs-reflection-agreement entry; warn when MEF found plugins but ReflectionOnly saw none |

## Iter-4 Revisions Applied

| Finding | Applied |
|---------|---------|
| MED-2 (managed-fallback dropped; cl.exe required) | MissingCreatePlugin.dll + MissingDestroyPlugin.dll built via cl.exe /LD /MD x86; WrongIPluginShape.dll is managed by design; no fallback |
| MED-3 (AssemblyInfo.cs clobber — FALSE POSITIVE) | Utinni.Cli/Properties/AssemblyInfo.cs (EXE) is distinct from Utinni.Cli.Tests/Properties/AssemblyInfo.cs (TEST); no clobber |
| LOW-5 (Task 4.1 test count 8→9) | PluginInspectionTests ships Tests 1-6, 8, 9, 10 (9 tests); gap at 7 signals iter-3 MED-5 move |
| LOW-6 (FilterNativeLoadErrors class name) | Class is `PluginInspectionFilters` (separate static class); `PluginInspection` doesn't define it |

## Fixture Summary

| Fixture | DLL | Size | Kind | Export Check | Expected Result |
|---------|-----|------|------|-------------|-----------------|
| valid-plugin | Utinni.CrtMatchPlugin.dll (Phase 3 R-B) | ~12KB (from bin/) | native | createPlugin + destroyPlugin | pass |
| missing-createplugin | MissingCreatePlugin.dll | 8KB | native | destroyPlugin only | createplugin=fail, overallStatus=fail |
| missing-destroyplugin | MissingDestroyPlugin.dll | 8KB | native | createPlugin only | destroyplugin=fail, overallStatus=fail |
| wrong-iplugin-shape | WrongIPluginShape.dll | 4KB | managed | [InheritedExport(typeof(IPlugin))] no impl | iplugin-export-shape=fail, overallStatus=fail |

## Test Count

| Layer | Test Class | Count |
|-------|-----------|-------|
| Tier-1 | PluginInspectionTests | 10 (Tests 1-6, 7, 8, 9, 10) |
| Tier-2 | ValidatePluginCommandTests | 8 (4 Theory rows + 4 Facts) |
| **Total Plan 04-04** | | **18** |

Combined across all Phase 4 plans: Utinni.Cli.Tests = **50** / UtinniCoreDotNet.Tests = **131**.

## DEC-C3 Promotion

PROJECT.md row DEC-C3 changed from:
`Candidate — non-locked | Promote to ADR when Tier 2 CLI shim lands (Phase 4).`

To: `LOCKED ✓ | Promoted at Phase 4 close (Plan 04-04).`

Gate confirmed: 04-02-SUMMARY.md + 04-03-SUMMARY.md exist on disk (REVIEWS MEDIUM-16).

## CON-O-09 + CON-O-11 Closure

Both confirmed present in docs/ai/assessment.md:
- CON-O-09: "Resolved 2026-05-23 (CON-O-09, Phase 4 Plan 04-02 — D-03)..." — Plan 04-02 Task 2.5
- CON-O-11: "Resolved 2026-05-23 (CON-O-11, Phase 4 Plan 04-01 — D-02)..." — Plan 04-01 Task 1.1

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] System.Reflection.Metadata NuGet version vs test runner assembly version mismatch**
- **Found during:** Task 4.1 test run
- **Issue:** NuGet 1.6.0 package has assembly version 1.4.3.0. The `Microsoft.TestPlatform.ObjectModel.dll` (net462) was compiled against assembly version 1.4.3.0, placing that file in the test bin. The binding redirect generated by MSBuild was pointing to 1.6.0.0 (NuGet package version, not assembly version), causing `FileLoadException` at runtime.
- **Fix:** Corrected `app.config` binding redirects to use the ACTUAL assembly versions (1.4.3.0 for System.Reflection.Metadata, 1.2.3.0 for System.Collections.Immutable). Added `ForceReflectionMetadataVersion` MSBuild Target to force-copy the NuGet 1.6.0 DLL after the test platform copies an older version.
- **Files modified:** `Utinni.Cli.Tests/app.config`, `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj`
- **Commit:** 4323cc6

**2. [Rule 3 - Blocking] BlobReader.ReadUtf8NullTerminated does not exist in System.Reflection.Metadata 1.6.0**
- **Found during:** Task 4.1 compile
- **Issue:** Plan referenced `BlobReader.ReadUtf8NullTerminated()` which was not in the API surface. Also, `PEReader.GetEntireImage()` returns `PEMemoryBlock` with `GetContent()` returning `ImmutableArray<byte>` (not `byte[]`).
- **Fix:** Rewrote NativeExportProbe to use raw file-offset arithmetic with `BlobReader` manually positioned via the image block, reading bytes with `ReadByte()`/`ReadUInt32()`/etc. Used ImmutableArray iteration with index for the byte conversion.
- **Files modified:** `Utinni.Cli/Commands/NativeExportProbe.cs`
- **Commit:** 4323cc6

**3. [Rule 3 - Blocking] PEReader without PrefetchEntireImage does not support GetEntireImage()**
- **Found during:** Task 4.1 test run (HasExport returning false for existing exports)
- **Issue:** `PEReader(stream)` with default options does not prefetch the whole image; `GetEntireImage()` would return empty/fail silently (exception caught by try/catch).
- **Fix:** Changed to `new PEReader(fs, PEStreamOptions.PrefetchEntireImage)`.
- **Files modified:** `Utinni.Cli/Commands/NativeExportProbe.cs`
- **Commit:** 4323cc6

**4. [Rule 3 - Blocking] Test 9 (source file walk) used Assembly.Location which is shadow-copied in .NET Framework test host**
- **Found during:** Task 4.1 test run
- **Issue:** `typeof(PluginInspection).Assembly.Location` returns the shadow-copy path (`C:\Users\kenne\AppData\Local\Temp\...`) not the worktree source path.
- **Fix:** Changed test to use `AppContext.BaseDirectory` as the starting point for the upward directory walk.
- **Files modified:** `Utinni.Cli.Tests/Commands/PluginInspectionTests.cs`
- **Commit:** 4323cc6

**5. [Rule 3 - Blocking] WrongIPluginShape.dll classified as kind=unknown instead of kind=managed**
- **Found during:** Task 4.3 fixture generation
- **Issue:** `Assembly.ReflectionOnlyLoadFrom` fails for WrongIPluginShape.dll because it references `System.ComponentModel.Composition` which is not pre-loaded in the reflection-only context. `CustomAttributeData.GetCustomAttributes` throws, so the `[InheritedExport(typeof(IPlugin))]` attribute is never detected.
- **Fix:** Added `ReflectionOnlyAssemblyResolve` event handler to resolve deps on demand + added `RawBytesHasPluginAttributeString` fallback that scans raw PE bytes for the IPlugin type name string when the ReflectionOnly path fails.
- **Files modified:** `Utinni.Cli/Commands/PluginInspection.cs`
- **Commit:** aecc849

**6. [Rule 1 - Bug] CommandDispatchTests validate-plugin stub row exits 3 not 1**
- **Found during:** Task 4.3 full test run
- **Issue:** `Run_WithVerbStub_ExitsOneAndEmitsNotImplementedJson` Theory had `[InlineData("validate-plugin", "/tmp/somedir")]` which now exits 3 (DirectoryNotFound) instead of 1 (NotImplemented).
- **Fix:** Removed the validate-plugin InlineData row. Same pattern as Plans 04-02 + 04-03.
- **Files modified:** `Utinni.Cli.Tests/Commands/CommandDispatchTests.cs`
- **Commit:** aecc849

**7. [Rule 1 - Bug] Help dispatch goldens (help.expected.txt, no-args.expected.txt) had stale short validate-plugin HelpText**
- **Found during:** Task 4.3 full test run
- **Issue:** The dispatch golden showed the old stub HelpText (short warning). The new HelpText is longer (includes PluginLoader composition warning).
- **Fix:** Updated both golden files to match the actual CommandLineParser-wrapped output.
- **Files modified:** `Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt`, `no-args.expected.txt`
- **Commit:** aecc849

**8. [Rule 1 - Bug] Help_ContainsTEoPMitigationWarning assertion failed on multi-line wrapped text**
- **Found during:** Task 4.3 test run
- **Issue:** CommandLineParser wraps HelpText at console width, inserting newlines. The assertion looked for an exact multi-word string that spans a line break.
- **Fix:** Added whitespace normalization (collapse wrapped continuation indentation to single spaces) before the assertion.
- **Files modified:** `Utinni.Cli.Tests/Commands/ValidatePluginCommandTests.cs`
- **Commit:** aecc849

**9. [Rule 1 - Bug] Test 7 (InspectDirectory_WrongIPluginShapeFixture) temp-dir deletion raises UnauthorizedAccessException**
- **Found during:** Post-Task 4.3 full test run
- **Issue:** `Assembly.ReflectionOnlyLoadFrom` on WrongIPluginShape.dll holds a file lock until the CLR AppDomain is unloaded. `Directory.Delete(tempDir, recursive: true)` then fails with `UnauthorizedAccessException`.
- **Fix:** Added GC.Collect() + GC.WaitForPendingFinalizers() before deletion, with `catch (UnauthorizedAccessException)` / `catch (IOException)` to leave temp dir for OS cleanup if still locked.
- **Files modified:** `Utinni.Cli.Tests/Commands/PluginInspectionTests.cs`
- **Commit:** db8ec20

## Threat Flags

None. Plan 04-04 adds a plugin-inspection command. The T-04-EoP threat is documented in HelpText and the threat model:
- Native path: PE-parse only, no code execution
- Managed path: PluginLoader composition executes module ctors (documented and intentional)
No new network endpoints, auth paths, or schema changes at trust boundaries.

## Known Stubs

None. All four roadmap commands are now implemented:
- parse-tre (Plan 04-02)
- list-objects (Plan 04-02)
- inspect-iff (Plan 04-03)
- validate-plugin (Plan 04-04)

## Phase 4 Wave Check (REVIEWS MEDIUM-16 evidence)

04-02-SUMMARY.md: EXISTS (TRE parser + parse-tre + list-objects — Plan 04-02)
04-03-SUMMARY.md: EXISTS (IFF parser + inspect-iff — Plan 04-03)
→ DEC-C3 promotion gate PASSED.

## Self-Check: PASSED
