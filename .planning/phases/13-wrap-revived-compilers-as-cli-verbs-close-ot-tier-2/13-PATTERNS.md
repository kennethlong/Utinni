# Phase 13: Wrap revived compilers as CLI verbs + close OT Tier-2 - Pattern Map

**Mapped:** 2026-06-03
**Files analyzed:** 13 new + 4 modified (+ native lift artifacts)
**Analogs found:** 16 / 17 (one genuinely-new pattern: the subprocess seam)

> **Read this first (planner):** the research at `13-RESEARCH.md` already cites exact files
> and line numbers. This map adds the *verified* analog excerpts + the two non-obvious findings
> that change plan structure:
> 1. **All four `*SaveTargets` classes live in the WinForms plugin (`TJT.Saving`), NOT in
>    `UtinniCoreDotNet`** — confirmed below. The `save` verb (AUTH-05) **cannot reference them**;
>    it must call the framework primitive `LooseOverridePath.Resolve` + `IffWriter.Write` directly,
>    OR a thin framework extraction must be planned. This is RESEARCH Open Q2 / Pitfall 6, now CONFIRMED.
> 2. **`TreRecord.Compressor` is `internal`** (exposed publicly only as the `CompressionKind` string).
>    The `.rsp`-synthesis helper (AUTH-04) lives in `Utinni.Cli.Tests` (already has
>    `InternalsVisibleTo`) or must use `CompressionKind == "none"` for the `@u` marker decision.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Utinni.Cli/Commands/CompileTemplateCommand.cs` (AUTH-03) | command/verb | request-response (subprocess) | `Utinni.Cli/Commands/RoundtripTabCommand.cs` | role-match (envelope/exit-codes exact; subprocess NEW) |
| `Utinni.Cli/Commands/BuildTreCommand.cs` (AUTH-04) | command/verb | batch (subprocess + .rsp synth) | `RoundtripTabCommand.cs` + `TreFile.cs` reader | role-match |
| `Utinni.Cli/Commands/CompileDefinitionCommand.cs` (AUTH-02) | command/verb | transform (subprocess → JSON) | `RoundtripTabCommand.cs` + `JsonOutput.cs` | role-match |
| `Utinni.Cli/Commands/CompileDatatableCommand.cs` (AUTH-06) | command/verb | transform (subprocess) | `RoundtripTabCommand.cs` | role-match |
| `Utinni.Cli/Commands/ExportArmorCommand.cs` (AUTH-06) | command/verb | batch (subprocess chain) | `RoundtripTabCommand.cs` | role-match |
| `Utinni.Cli/Commands/ExportWeaponCommand.cs` (AUTH-06) | command/verb | batch (subprocess chain) | `RoundtripTabCommand.cs` | role-match |
| `Utinni.Cli/Commands/SaveCommand.cs` (AUTH-05) | command/verb | file-I/O (in-process write) | `RoundtripIffCommand`/`RoundtripTabCommand` + `IffSaveTargets` | role-match (cross-repo analog) |
| `Utinni.Cli/Commands/RepackTreCommand.cs` (D-10) | command/verb | file-I/O (destructive) | `TreRepackSaveTarget` (plugin) + `TreBackupPath` | role-match (cross-repo analog) |
| `Utinni.Cli/Commands/Subprocess/NativeToolRunner.cs` | utility | request-response (Process.Start) | **NONE** (`NativeExportProbe.cs` is closest but in-process) | **NO ANALOG** |
| `Utinni.Cli/Program.cs` (modify: register 8 new verbs) | route | request-response | `Utinni.Cli/Program.cs` (self) | exact |
| `Utinni.Cli.Tests/*GoldenTests.cs` (per verb) | test | golden compare | `GoldenTestRunner.cs` + existing roundtrip golden tests | exact |
| OT typed-display widgets in `FormObjectTemplateEditor.cs` (RESID-01) | component/editor | event-driven (UI) | `FormObjectTemplateEditor.cs` (self, Phase-11) + `ObjectTemplateParamCodec` | role-match |
| Schema-consumer in OT codec/editor (RESID-01) | service | transform (schema lookup) | `ObjectTemplateParamCodec.cs` `RawBytesHexFallback` path | exact (extends residual) |
| `tools/src/.../DataTableTool/...DataTableTool.vcxproj` (AUTH-06 lift) | native app | transform | `tools/src/engine/shared/application/TemplateCompiler/...vcxproj` | exact (Phase-12 lift) |
| `tools/src/.../ArmorExporterTool/...vcxproj` (AUTH-06 lift) | native app | batch | `tools/src/.../TemplateCompiler/...vcxproj` | exact |
| `tools/src/.../CoreWeaponExporterTool/...vcxproj` (AUTH-06 lift) | native app | batch | `tools/src/.../TemplateCompiler/...vcxproj` | exact |
| `tools/src/engine/shared/library/sharedXml/...vcxproj` (AUTH-06 lift) | native lib | leaf lib | any `tools/src/.../library/*.vcxproj` (e.g. `sharedRandom`) | exact |

---

## Pattern Assignments

### `Utinni.Cli/Commands/Compile*Command.cs` + `Build*Command.cs` (BUILD verbs, AUTH-02/03/04/06)

**Analog:** `Utinni.Cli/Commands/RoundtripTabCommand.cs` (envelope + exit-code contract) — `D:/Code/Utinni/Utinni.Cli/Commands/RoundtripTabCommand.cs`

**Verb + Options pattern** (RoundtripTabCommand.cs:38-55) — copy the `[Verb]`/`[Value]`/`[Option]` shape:
```csharp
[Verb("roundtrip-tab", HelpText = "...")]
public class RoundtripTabOptions
{
    [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .tab file.")]
    public string Path { get; set; }
    [Option("mutate-cell", HelpText = "...")] public string MutateCell { get; set; }
}
// BUILD verbs add: [Option("tool-path", HelpText="Override path to the native exe ...")] string ToolPath
```

**Static command class + Run signature** (RoundtripTabCommand.cs:83-85): each verb is
`public static class XCommand { public static int Run(XOptions o) {...} }` — never instance.

**Exit-code contract** (RoundtripTabCommand.cs:79-81, verified): `0` success; `1` UsageError;
`2` parse/tool error; `3` FileNotFound. **Generic `Exception` is intentionally NOT caught**
(RoundtripTabCommand.cs:182). BUILD verbs map native non-zero exit → CLI exit `2` (ToolError).

**FileNotFound guard** (RoundtripTabCommand.cs:106-110) — copy verbatim for the input path:
```csharp
if (!File.Exists(o.Path))
    return JsonOutput.EmitError("roundtrip-tab", "FileNotFound", ".tab file not found: " + o.Path, exitCode: 3);
```

**Success envelope** (RoundtripTabCommand.cs:157-168): build a `JObject` of result fields (sorted-key
is applied by `JsonOutput`), then `return JsonOutput.EmitSuccess("<verb>", result);`. BUILD-verb result
fields per RESEARCH:`{ tool, exitCode, outputPath, produced, stderr }`.

---

### `Utinni.Cli/Commands/Subprocess/NativeToolRunner.cs` (shared subprocess helper) — **NO ANALOG**

**This is the only genuinely-new managed pattern in the phase.** `NativeExportProbe.cs` parses PE
export tables **in-process** and spawns nothing — it is NOT a subprocess analog. Build per the
RESEARCH-synthesized shape (`13-RESEARCH.md` §"BUILD-verb subprocess wrapping", lines 156-181 + Code
Examples 341-355). Hard rules (carried into the map so the planner can't miss them):
- `UseShellExecute=false`, explicit arg array (never string-concat user paths — V5/command-injection).
- `RedirectStandardOutput`/`StandardError=true`, `CreateNoWindow=true`, `WorkingDirectory=<staged tool dir>`.
- `NormalizeBanner(...)` strips `__DATE__ " " __TIME__` before any golden compare (Pitfall 3).
- Resolve exe beside `utinni-cli`; missing exe → exit `3` FileNotFound (mirror RoundtripTabCommand:106).
- Emit through `JsonOutput.EmitSuccess`/`EmitError` so the sorted-key contract holds.

---

### `Utinni.Cli/Program.cs` (modify — register new verbs)

**Analog:** self (`D:/Code/Utinni/Utinni.Cli/Program.cs:43-63`). Add each new `*Options` to BOTH the
`ParseArguments<...>` type list AND the `MapResult(...)` lambda list. Pattern (verified):
```csharp
.ParseArguments<Commands.RoundtripTabOptions, /* + new */>(args)
.MapResult(
    (Commands.RoundtripTabOptions o) => Commands.RoundtripTabCommand.Run(o),
    /* + (Commands.CompileTemplateOptions o) => Commands.CompileTemplateCommand.Run(o), ... */
    errs => 1);   // exit 1 on usage error
```
Note `settings.CaseSensitive = false` (Program.cs:40) — verb names are matched case-insensitively.

---

### `Utinni.Cli/Commands/SaveCommand.cs` (AUTH-05) — **cross-repo analog, framework-leg caveat**

**Analog (write logic):** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/IffSaveTargets.cs`
**Analog (path defense — CLI-referenceable):** `D:/Code/Utinni/UtinniCoreDotNet/Saving/LooseOverridePath.cs`

**CONFIRMED BLOCKER (RESEARCH Open Q2 / Pitfall 6):** `IffSaveTargets`, `DatatableSaveTargets`,
`ObjectTemplateSaveTargets`, `StringTableSaveTargets`, `TreRepackSaveTarget` ALL live in the
WinForms plugin namespace `TJT.Saving` (`UtinniPlugins/.../Saving/*.cs`) — **NOT** in `UtinniCoreDotNet`.
They are `async Task<SaveResult>` and take `OpenSource` + WinForms `SaveFileDialog`-derived paths.
The net472 `Utinni.Cli` **cannot reference the plugin assembly.** Two planner options:
1. **Reimplement the thin write in the CLI** over framework primitives (recommended — cheap): the
   *actual* write is just `IffWriter.Write(doc)` + atomic `FileStream` + `Flush(true)`
   (IffSaveTargets.cs:265-282, the `WriteAtomic` core) gated by `LooseOverridePath.Resolve`
   (LooseOverridePath.cs:73, IS in `UtinniCoreDotNet`, IS referenceable).
2. **Extract the 4 write cores to `UtinniCoreDotNet/Saving/`** as framework legs, then both the
   plugin and the CLI call them. Heavier; only if the planner wants a single source of truth.

**Path-defense to reuse verbatim** (LooseOverridePath.cs:73 — rooted/`..`/prefix-match defenses):
```csharp
string fullPath = LooseOverridePath.Resolve(resolvedRoot, relAssetPath); // throws ArgumentException on escape
```

**Atomic-write core to mirror** (IffSaveTargets.cs:265-282):
```csharp
byte[] bytes = IffWriter.Write(doc);
Directory.CreateDirectory(Path.GetDirectoryName(path));
using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
{ fs.Write(bytes, 0, bytes.Length); fs.Flush(true); } // Flush(true) = stale-bytes-reload barrier
```

**Envelope (ROADMAP-locked, D-09):** `{ written, path, bytesWritten, backupPath, validated }` —
emit via `JsonOutput.EmitSuccess("save", result)`. `--path` explicit override; loose-override default.

---

### `Utinni.Cli/Commands/RepackTreCommand.cs` (D-10) — separate verb, NOT through `save`

**Analog:** `D:/Code/UtinniPlugins/.../Saving/TreRepackSaveTarget.cs` (plugin) +
`D:/Code/Utinni/UtinniCoreDotNet/Saving/TreBackupPath.cs` + `TreRepackLock.cs` (framework, referenceable).
Same framework-vs-plugin split as `save` — the backup/lock primitives ARE in `UtinniCoreDotNet/Saving/`;
the repack orchestration is plugin-side. Keep repack OUT of `save` (D-10); Phase-14 MCP gates it `dry_run`.

---

### `.rsp` synthesis helper (AUTH-04, lives test-side or as a CLI helper)

**Analog (reader API):** `D:/Code/Utinni/UtinniCoreDotNet/Formats/Tre/TreFile.cs` +
`TreRecord.cs`. Verified API surface for synthesis:
- `treFile.Records` → `IReadOnlyList<TreRecord>`, **index == original data-block order = `.rsp` order**
  (TreFile.cs:86). Do NOT pre-sort (builder CRC-sorts the TOC itself).
- `rec.Name` (TreRecord.cs:78) = tree path; `treFile.GetRecordData(i)` = payload to extract to disk.
- **Compression marker — caveat:** `TreRecord.Compressor` is `internal` (TreRecord.cs:69); the public
  surface is `rec.CompressionKind` (`"none"`/`"deflate"`, TreRecord.cs:53). Emit the `@u` uncompressed
  marker when `CompressionKind == "none"`. (Synthesis code in `Utinni.Cli.Tests` already has
  `InternalsVisibleTo` and may read `Compressor` directly if preferred.)

**Builder `.rsp` line format (native, VERIFIED in RESEARCH):** `<diskPath> @ <treePath>` (disk-first),
`@u` for uncompressed. Emit in `Records` order. See `13-RESEARCH.md` §AUTH-04 + Code Example 327-339.

---

### OT typed-display close (RESID-01) — extends Phase-11 codec

**Analog:** `D:/Code/Utinni/UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateParamCodec.cs`
(the `RawBytesHexFallback` residual = the ~17% multi-chunk list/struct params) +
`D:/Code/UtinniPlugins/.../UI/Forms/FormObjectTemplateEditor.cs` (typed-scalar widgets, Phase-11).

**The residual this phase types** (ObjectTemplateParamCodec.cs:188-210): anything with interior
structure routes to `Fallback(...)` → `ObjectTemplateParamValue.FromRawBytes(...)`:
```csharp
// Anything else (unexpected length / interior structure) → byte-exact hex fallback.
return Fallback(valueRegion, ObjectTemplateDataTypeTag.Single);
```
RESID-01: the schema artifact's `ListType != LIST_NONE` params (slots/attributes/hair, D-07) get
structured widgets in `FormObjectTemplateEditor`; the rare tail degrades to typed-label + hex. The
`Encode` inverse (ObjectTemplateParamCodec.cs:217-269) already round-trips `RawBytesHexFallback`
byte-for-byte — typed display is a **read/display layer over the existing bytes**, no codec rewrite.

**Schema source (AUTH-02, D-08):** the static per-class JSON from `compile-definition` is consumed by
BOTH `FormObjectTemplateEditor` (runtime `(type, list_type)` → widget) AND the Area-2 cross-check
tests. Schema vocabulary = `ParamType` (14) + `ListType` (4) enums (RESEARCH §AUTH-02, lines 209-214).

---

### Native tool lifts (AUTH-06) — DataTableTool / ArmorExporterTool / CoreWeaponExporterTool / sharedXml

**Analog (app vcxproj):** `D:/Code/Utinni/tools/src/engine/shared/application/TemplateCompiler/build/win32/TemplateCompiler.vcxproj`
**Analog (leaf-lib vcxproj):** `tools/src/engine/shared/library/sharedRandom/build/win32/sharedRandom.vcxproj`
**Analog (build shim):** `tools/Directory.Build.props` — **already lists `sharedXml\include\public`**
in its `AdditionalIncludeDirectories` (line 9) AND the `libxml\include` path. Phase 12 forward-prepared
the include surface; the lift adds the `sharedXml` *project* + the 3 app projects to `Utinni.Tools.sln`.

**Lift procedure (Phase-12 pattern, DEPENDENCY-MANIFEST.md:1-9):** `git archive` from pinned SHA
`@5fce7bb8` (PINNED-SHA.md) → add vcxprojs to `Utinni.Tools.sln` (currently 31 projects → 35) →
`Directory.Build.props` auto-imports the shim. CI hard-gate auto-extends (the lane builds the whole sln).

**Revival deltas to expect** (DEPENDENCY-MANIFEST.md:35, 43, 51 — the established pattern):
- `/SAFESEH:NO` on each new EXE (zlib/P4 libs predate Safe-SEH).
- `UTINNI_TOOLS_NO_SHAREDLOG` (already a global define in Directory.Build.props:12).
- C++20 `char16_t`/`wchar_t` ports if the exporter touches `Unicode::String` (per template-tool delta).
- **NEW for AUTH-06 (Pitfall 1):** source-stub the exporters' Perforce helpers to no-op (mirror the
  `UTINNI_TOOLS_NO_SHAREDLOG` decouple precedent); document as a DEPENDENCY-MANIFEST revival delta.
- **NEW (Pitfall 2):** the exporters `system("TemplateCompiler ...")` — stage all exes in one dir,
  set the subprocess `WorkingDirectory`; ship a `tools.cfg` fixture beside the exporter exe.

---

### Golden tests (per verb)

**Analog:** `D:/Code/Utinni/Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs` +
`InProcessCliRunner.cs` + existing roundtrip golden tests.
- `GoldenTestRunner.Matches(fixtureKey, actualJson)` (line 40) — JSON golden via `JToken.DeepEquals`,
  dumps to `TestResults/<key>/` on mismatch.
- `GoldenTestRunner.MatchesText(fixtureKey, actualText)` (line 60) — exact text after CRLF-normalize.
- Fixtures resolve via `FixturePath.Resolve(...)` under `<BaseDirectory>/Fixtures/`.
- **In-process runner works for the in-process verbs (`save`); the BUILD verbs run a real subprocess**
  → their golden tests assert on the captured envelope + the produced artifact, with `NormalizeBanner`
  applied before any byte-compare (Pitfall 3).

---

## Shared Patterns

### Subprocess invocation (NEW — `NativeToolRunner`)
**Source:** none (first subprocess seam in `utinni-cli`); synthesize per `13-RESEARCH.md` lines 156-181.
**Apply to:** all 5 BUILD verbs (CompileTemplate, BuildTre, CompileDefinition, CompileDatatable, Export*).
`UseShellExecute=false` + arg array + redirect stdout/stderr + `NormalizeBanner` + `WorkingDirectory`.

### JSON envelope (sorted-key)
**Source:** `D:/Code/Utinni/Utinni.Cli/Output/JsonOutput.cs` — `EmitSuccess(cmd, result)` (line 50) /
`EmitError(cmd, kind, message, exitCode)` (line 70). Root envelope `{ command, result|error, schemaVersion:1 }`,
recursively key-sorted (Ordinal), LF-normalized. `TypeNameHandling.None` (line 40, T-04 mitigation).
**Apply to:** every new verb (BUILD + SAVE + repack). Do NOT hand-roll JSON.

### Exit-code contract
**Source:** `RoundtripTabCommand.cs:79-81` (`0`/`1`/`2`/`3`); `Program.cs:63` (`errs => 1`).
**Apply to:** every new verb. Native non-zero → exit `2` with captured stderr.

### Path containment
**Source:** `D:/Code/Utinni/UtinniCoreDotNet/Saving/LooseOverridePath.cs:73` (rooted/`..`/prefix-match).
**Apply to:** `save` (loose-override leg) and any verb writing under the client tree.

### Native lift (build shim + revival deltas)
**Source:** `tools/Directory.Build.props` + `tools/DEPENDENCY-MANIFEST.md` + `tools/PINNED-SHA.md`.
**Apply to:** the 3 AUTH-06 app lifts + `sharedXml`. `git archive @5fce7bb8` → add to `Utinni.Tools.sln`
→ `/SAFESEH:NO` + C++20 ports + Perforce-stub.

### Golden harness
**Source:** `Utinni.Cli.Tests/Infrastructure/GoldenTestRunner.cs` + `FixturePath.cs` + `InProcessCliRunner.cs`.
**Apply to:** all verb tests; add `NormalizeBanner` for native-output goldens.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Utinni.Cli/Commands/Subprocess/NativeToolRunner.cs` | utility | request-response (Process.Start) | No `Process.Start` precedent exists in `Utinni.Cli` — the CLI is fully in-process today (`NativeExportProbe.cs` parses PE tables in-process, spawns nothing). First subprocess seam; synthesize from `13-RESEARCH.md` §"BUILD-verb subprocess wrapping". |

**Partial-analog / cross-repo caveat (not "no analog", but flagged):**
- `SaveCommand.cs` / `RepackTreCommand.cs` — the only write analogs (`*SaveTargets`) live in the
  **WinForms plugin** (`TJT.Saving`), unreferenceable from net472 `Utinni.Cli`. Use the framework
  primitives (`LooseOverridePath`, `IffWriter.Write`, `TreBackupPath`) + reimplement the thin write,
  OR extract framework legs. **This is a plan-structure decision, not a copy-paste.**

---

## Metadata

**Analog search scope:** `Utinni.Cli/Commands`, `Utinni.Cli/Output`, `Utinni.Cli.Tests/Infrastructure`,
`UtinniCoreDotNet/Formats/{Tre,ObjectTemplate,Datatable}`, `UtinniCoreDotNet/Saving`, `tools/`,
and the sibling `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/{Saving,UI/Forms}`.
**Files scanned:** ~20 read in full or targeted; analog inventory cross-checked against `13-RESEARCH.md` Sources.
**Pattern extraction date:** 2026-06-03
