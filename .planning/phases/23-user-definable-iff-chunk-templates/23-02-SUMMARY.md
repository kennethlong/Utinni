---
phase: 23-user-definable-iff-chunk-templates
plan: 02
subsystem: formats
tags: [iff, template-engine, schema-layer, json, newtonsoft, d-09-presets, byte-exact, security-poco]

# Dependency graph
requires:
  - phase: 23-user-definable-iff-chunk-templates
    plan: 01
    provides: "Frozen ITemplateContracts.cs POCO surface (TemplateModel/FieldRecord/RepeatSpec/NamedValueMap/KernelType/RepeatKind)"
provides:
  - "TemplateJson (de)serializer: sorted-key indented write + fixed-POCO read with TypeNameHandling=None and typed TemplateException on a bad enum"
  - "TemplateException + TemplateModelDefaults (CurrentVersion/DefaultEncoding + deep-clone) supporting the frozen POCOs"
  - "7 shipped D-09 preset JSON files (vector/quaternion/matrix/color/colorArgb32/colorArgbF/stringId) with pinned byte layouts, CopyToOutputDirectory next to the assembly"
affects: [23-03, 23-04, 23-05, 23-06, 23-07, 23-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Schema POCOs frozen in a separate contract file; this plan adds (de)serialization + supporting model machinery around them without redefining the types"
    - "Fixed-POCO deserialize with TypeNameHandling=None is the hostile-template gadget mitigation; an unknown StringEnumConverter member rewraps as a typed TemplateException"
    - "Presets are pure JSON sugar over the kernel (D-08): byte-exactness lives entirely in the kernel codec, so a preset can never introduce a parity bug"
    - "xUnit shadow-copies the test assembly; resolve shipped Content via AppContext.BaseDirectory, never Assembly.Location"

key-files:
  created:
    - UtinniCoreDotNet/Formats/Template/TemplateModel.cs
    - UtinniCoreDotNet/Formats/Template/TemplateJson.cs
    - UtinniCoreDotNet/Formats/Template/Presets/vector.json
    - UtinniCoreDotNet/Formats/Template/Presets/quaternion.json
    - UtinniCoreDotNet/Formats/Template/Presets/matrix.json
    - UtinniCoreDotNet/Formats/Template/Presets/color.json
    - UtinniCoreDotNet/Formats/Template/Presets/colorArgb32.json
    - UtinniCoreDotNet/Formats/Template/Presets/colorArgbF.json
    - UtinniCoreDotNet/Formats/Template/Presets/stringId.json
    - Utinni.Cli.Tests/Template/TemplateJsonTests.cs
    - Utinni.Cli.Tests/Template/TemplatePresetTests.cs
  modified:
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj
    - Utinni.Cli.Tests/Utinni.Cli.Tests.csproj

key-decisions:
  - "Did NOT redefine TemplateModel/FieldRecord/etc. in TemplateModel.cs (they are frozen in ITemplateContracts.cs); the file instead carries TemplateException + TemplateModelDefaults so the schema layer has supporting machinery without a duplicate-type error"
  - "Enabled NuGet PackageReference restore on the classic-style UtinniCoreDotNet project (RestoreProjectStyle=PackageReference) so it can consume Newtonsoft.Json 13.0.3 (the same version Utinni.Cli already pulls) -- zero new solution package (T-23-02-SC)"
  - "Resolved preset files in the preset test via AppContext.BaseDirectory rather than Assembly.Location to survive xUnit's shadow-copy of the test assembly"

patterns-established:
  - "TemplateJson is the only code that turns a template file into the POCO and back; its WriteSettings/ReadSettings are the canonical (de)serialization contract Waves 23-03+ reuse"
  - "Every shipped preset carries a `description` field citing the swg-client-v2 source line for its pinned layout (D-09 audit trail + D-14 teaching artifact)"

requirements-completed: [PROD-IFFT-01]

# Metrics
duration: 6min
completed: 2026-06-21
---

# Phase 23 Plan 02: Template Schema Layer (POCO + JSON + D-09 Presets) Summary

**The schema half of PROD-IFFT-01: a Newtonsoft (de)serializer (sorted-key write, fixed-POCO read with `TypeNameHandling=None` + typed `TemplateException` on a bad enum, version-default forward-migration) over the frozen 23-01 POCOs, plus 7 shipped preset JSON files carrying the EXACT swg-client-v2-pinned D-09 byte layouts (w-first quaternion, 3x4 row-major matrix, three color forms) as pure kernel sugar.**

## Performance

- **Duration:** 6 min
- **Started:** 2026-06-21T03:38:17Z
- **Completed:** 2026-06-21
- **Tasks:** 2
- **Files modified:** 13 (11 created, 2 modified)

## Accomplishments
- `TemplateJson.cs`: `Serialize` emits sorted-key, indented JSON via an alphabetical-order contract resolver so equal models always produce byte-identical text (D-01 clean diffs); `Deserialize` targets the fixed `TemplateModel` POCO with `TypeNameHandling = None` and `MetadataPropertyHandling = Ignore` (a hostile `$type` gadget is never resolved -- T-23-02-DE), defaults a missing `version` to 0 (D-13 forward-migration), and rewraps an unknown `KernelType`/`RepeatKind` member as a typed `TemplateException` instead of silently defaulting to type 0 (T-23-02-EN).
- `TemplateModel.cs`: `TemplateException` (typed schema error) + `TemplateModelDefaults` (`CurrentVersion`, `DefaultEncoding = "ascii"`, deep-clone helper following the `ClientEffectCommand` defensive-copy discipline) -- supporting machinery around the FROZEN contract POCOs, which are NOT redefined here.
- 7 preset JSON files under `Presets/` with EXACT D-09 layouts, each with a `version` field and a `description` citing the swg-client-v2 source line: `vector`(x,y,z 3xf32), `quaternion`(w,x,y,z **w-FIRST** 4xf32), `matrix`(**3x4 row-major** 12xf32), `color`(PackedRgb 3xu8 r,g,b), `colorArgb32`(single u32 ARGB), `colorArgbF`(a,r,g,b 4xf32), `stringId`(table+text two NUL C-strings, documented override-prone per Pitfall 2). Marked `CopyToOutputDirectory=PreserveNewest` so they ship next to the assembly.
- 19 new GREEN tests (round-trip / sorted-key / version-default / `$type`-gadget / bad-enum-throws + per-preset schema-shape); full Template suite green (19 passed, 10 Wave-0 RED skips remain for 23-03+).
- Enabled `RestoreProjectStyle=PackageReference` on the classic-style `UtinniCoreDotNet` so it consumes Newtonsoft.Json 13.0.3 (the version `Utinni.Cli` already pulls) -- no new solution package (T-23-02-SC held).

## Task Commits

1. **Task 1: TemplateModel + TemplateJson (de)serializer (TDD GREEN)** - `50c8a1c` (feat)
2. **Task 2: Shipped D-09 preset pack (pinned byte layouts)** - `89184eb` (feat)

_Task 1 was `tdd="true"`: the test (`TemplateJsonTests`) and implementation were authored together as a single GREEN landing against the 23-01 frozen contract (the RED-via-Skip suite was established in 23-01). The new JSON tests are not skipped -- they assert real behavior and pass._

## Files Created/Modified
- `UtinniCoreDotNet/Formats/Template/TemplateModel.cs` - `TemplateException` + `TemplateModelDefaults` (defaults + deep clone)
- `UtinniCoreDotNet/Formats/Template/TemplateJson.cs` - sorted-key Serialize + fixed-POCO/TypeNameHandling=None Deserialize
- `UtinniCoreDotNet/Formats/Template/Presets/{vector,quaternion,matrix,color,colorArgb32,colorArgbF,stringId}.json` - the shipped D-09 preset pack
- `Utinni.Cli.Tests/Template/TemplateJsonTests.cs` - round-trip / version / security goldens
- `Utinni.Cli.Tests/Template/TemplatePresetTests.cs` - per-preset schema-shape goldens (loaded via AppContext.BaseDirectory)
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj` - Compile items, Newtonsoft PackageReference + RestoreProjectStyle, Presets Content
- `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` - linked the preset pack into the test output under the runtime-relative path

## Decisions Made
- **No POCO redefinition.** The plan said "flesh out TemplateModel.cs from the contract shapes," but the 23-01 contract POCOs are already complete and FROZEN in `ITemplateContracts.cs`. Re-declaring them in `TemplateModel.cs` would be a duplicate-type compile error and would violate the frozen-contract invariant. `TemplateModel.cs` therefore holds the supporting model machinery (`TemplateException`, defaults, deep-clone) the schema layer needs -- still satisfying the `min_lines: 60` artifact while honoring the frozen surface.
- **PackageReference on a classic project.** `UtinniCoreDotNet` is a classic-style csproj with no `packages.config`. `Newtonsoft.Json.dll` was already deployed to `bin/Release` (pulled by `Utinni.Cli`) but the model layer needs a compile-time reference. Added `RestoreProjectStyle=PackageReference` + `<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />` (the established project version) so restore resolves from the global cache -- introducing no new package to the solution.
- **AppContext.BaseDirectory for preset loading.** The first preset-test run failed because xUnit shadow-copies the test assembly to a Temp dir, so `Assembly.Location` pointed at the shadow copy (no Content). Switched to `AppContext.BaseDirectory`, which points at the real build output where the linked presets land.

## Deviations from Plan
None affecting scope. Two implementation choices the plan left to discretion or that were forced by the frozen contract are documented above (no POCO redefinition; PackageReference enablement). Neither changes the deliverable surface.

## Issues Encountered
- The classic-style `UtinniCoreDotNet` did not restore the Newtonsoft `PackageReference` until `RestoreProjectStyle=PackageReference` was added and an explicit `-t:Restore` was run; the solution build then went green. (Blocking-issue auto-fix, Rule 3.)
- The preset test initially could not find the JSON next to the assembly (xUnit shadow-copy + `Assembly.Location`); fixed by resolving via `AppContext.BaseDirectory` and linking the preset pack into the test project output under the runtime-relative path. (Rule 1.)

## Next Phase Readiness
- The schema layer is complete: 23-03 implements `KernelCodec.Decode/Encode` against `ITemplateContracts.cs` and removes the `Skip=` on the per-type / count-recompute / array / preset-decode / version-match / precedence goldens (the byte-exact GREEN gate).
- The shipped presets are schema-valid and pinned; 23-03's `Template_Preset_VectorQuaternionMatrixColor_DecodeToExactValues` golden (currently skipped) will decode them through the kernel for byte-exact value assertions.
- No blockers.

## Self-Check: PASSED
- FOUND: UtinniCoreDotNet/Formats/Template/TemplateModel.cs
- FOUND: UtinniCoreDotNet/Formats/Template/TemplateJson.cs
- FOUND: UtinniCoreDotNet/Formats/Template/Presets/vector.json
- FOUND: UtinniCoreDotNet/Formats/Template/Presets/quaternion.json
- FOUND: UtinniCoreDotNet/Formats/Template/Presets/matrix.json
- FOUND: UtinniCoreDotNet/Formats/Template/Presets/color.json
- FOUND: UtinniCoreDotNet/Formats/Template/Presets/colorArgb32.json
- FOUND: UtinniCoreDotNet/Formats/Template/Presets/colorArgbF.json
- FOUND: UtinniCoreDotNet/Formats/Template/Presets/stringId.json
- FOUND: Utinni.Cli.Tests/Template/TemplateJsonTests.cs
- FOUND: Utinni.Cli.Tests/Template/TemplatePresetTests.cs
- FOUND commit: 50c8a1c (Task 1)
- FOUND commit: 89184eb (Task 2)

---
*Phase: 23-user-definable-iff-chunk-templates*
*Completed: 2026-06-21*
