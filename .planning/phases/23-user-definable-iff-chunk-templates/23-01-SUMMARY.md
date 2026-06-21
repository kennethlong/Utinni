---
phase: 23-user-definable-iff-chunk-templates
plan: 01
subsystem: testing
tags: [iff, template-engine, kernel-codec, xunit, byte-exact-roundtrip, contracts-first, red-via-skip]

# Dependency graph
requires:
  - phase: 22-clienteffect-editor
    provides: ClefTestFixtures synthesize-through-IffWriter idiom + ClefFieldCodec MIT/provenance header pattern
  - phase: 07-iff-editor
    provides: MutableIffNode / MutableIffDocument / IffWriter (compose-through-the-writer primitives)
provides:
  - "Frozen template-engine contract surface (KernelType/RepeatKind/RepeatSpec/NamedValueMap/FieldRecord/TemplateModel/FitReport/DecodedTemplate + KernelCodec Decode/Encode/Fit stubs)"
  - "Synthesize-through-IffWriter template fixtures at low+high version FORMs (KERN/CNTR/FIXA/PRST/CPAP/builtin-root)"
  - "10 RED-via-Skip goldens covering every VALIDATION.md --filter row, incl. the CRITICAL DEC-C3 count-from-prior grow/shrink"
affects: [23-02, 23-03, 23-04, 23-05, 23-06, 23-07, 23-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Contracts-first (interface-first) ordering: freeze the public surface in ITemplateContracts.cs so Waves 1-2 implement against exact names, no scavenger hunt"
    - "RED-via-Skip: [Fact/Theory(Skip=...)] with a tracking note so the suite is GREEN-with-skips, never red-broken, pending the engine"
    - "Filter-token-in-method-name: test method names embed the VALIDATION.md --filter tokens (methodDisplay=method)"

key-files:
  created:
    - UtinniCoreDotNet/Formats/Template/ITemplateContracts.cs
    - Utinni.Cli.Tests/Template/TemplateTestFixtures.cs
    - Utinni.Cli.Tests/Template/KernelCodecTests.cs
    - Utinni.Cli.Tests/Template/RoundtripTemplateCommandTests.cs
    - Utinni.Cli.Tests/Template/ApplySaveTemplateTests.cs
  modified:
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj

key-decisions:
  - "Declared the engine class (KernelCodec) with NotImplementedException stubs in the frozen contract file (vs leaving it to Wave 1) so the test files have a concrete callable surface and the assembly compiles"
  - "Chose root FORM sub-type TPLX (no Utinni built-in decoder) so fixture leaves are template-eligible per the D-05 altitude rule"
  - "Version axis 0001 (low) / 0003 (high) with a widening CPAP-style layout mirrors the proven CLEF version-FORM divergence"

patterns-established:
  - "ITemplateContracts.cs is the single frozen surface; do not rename its types without re-pinning the dependent test files"
  - "Count-from-prior arrays carry RepeatSpec.CountFieldName; encode MUST recompute that field from element count (DEC-C3)"

requirements-completed: [PROD-IFFT-01, PROD-IFFT-02, PROD-IFFT-03]

# Metrics
duration: 8min
completed: 2026-06-21
---

# Phase 23 Plan 01: Wave-0 Template Test Scaffolding Summary

**Frozen `ITemplateContracts.cs` engine surface (kernel types/array kinds/value-maps/FitReport/DecodedTemplate) plus 4 synthesize-through-IffWriter test files holding 10 RED-via-Skip goldens — including the CRITICAL DEC-C3 count-from-prior grow/shrink byte-exact round-trip — all discovered and green-with-skips against the Release build.**

## Performance

- **Duration:** 8 min
- **Started:** 2026-06-21T03:23:54Z
- **Completed:** 2026-06-21T03:31:40Z
- **Tasks:** 2
- **Files modified:** 6 (5 created, 1 modified)

## Accomplishments
- Froze the template-engine public contract surface (`ITemplateContracts.cs`): `KernelType` (all D-08 members), `RepeatKind`/`RepeatSpec` (3 kinds, D-10), `NamedValueMap` (D-11 enum vs flags), `FieldRecord`, `TemplateModel` (carries `Version`, D-13), `FitReport`/`FieldPlausibility` (D-17.2), a `DecodedTemplate` value carrier, and `KernelCodec.Decode/Encode/Fit` `NotImplementedException` stubs — definitions only, compiles in headless UtinniCoreDotNet, `Generated/UtinniCore.cs` untouched.
- Wrote `TemplateTestFixtures.cs`: real compose-through-`IffWriter.Write` fixtures with kernel-encoded LE payloads (every scalar type; the count-from-prior CRITICAL shape; fixed+remainder arrays; D-09 vector/quat-w-first/matrix-3x4/PackedRgb presets; a version-divergent CPAP layout; a built-in-root precedence file) at low+high version FORMs.
- Wrote 3 RED test files (10 skipped goldens) whose method names embed the exact VALIDATION.md `--filter` tokens; verified each filter resolves ≥1 test, all SKIP, suite GREEN.
- Confirmed no new `PackageReference` was added (threat T-23-SC mitigation held).

## Task Commits

1. **Task 1: Freeze the template-engine contract surface** - `d4afba7` (feat)
2. **Task 2: Synthesize-through-IffWriter fixtures + RED test files** - `b916bbe` (test)

_Task 2 was marked `tdd="true"` but is Wave-0 scaffolding: the "test" it commits IS the RED suite itself (no engine implementation in this plan), so it is a single `test(...)` commit rather than a RED→GREEN pair. The GREEN gate lands in Wave 1 (plan 23-02+) when KernelCodec is implemented and the Skips are removed._

## Files Created/Modified
- `UtinniCoreDotNet/Formats/Template/ITemplateContracts.cs` - frozen contract surface (model + enums + POCOs + codec stubs)
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj` - registered the new contract file (classic-style project, explicit `<Compile Include>`)
- `Utinni.Cli.Tests/Template/TemplateTestFixtures.cs` - synthesize-through-IffWriter fixture builders
- `Utinni.Cli.Tests/Template/KernelCodecTests.cs` - per-type + count-recompute + array + preset + version-match + precedence RED goldens
- `Utinni.Cli.Tests/Template/RoundtripTemplateCommandTests.cs` - verb-level + D-14 worked-example RED goldens
- `Utinni.Cli.Tests/Template/ApplySaveTemplateTests.cs` - fail-closed RED golden

## Decisions Made
- Declared `KernelCodec` (engine class) with `NotImplementedException` stubs inside the frozen contract file rather than deferring the class to Wave 1 — the test files need a concrete callable type to reference so the assembly compiles. The split keeps logic out (stubs throw) while giving Waves 1-2 the exact entry-point signatures.
- Fixture root FORM sub-type `TPLX` was chosen because Utinni has no built-in decoder for it, making the inner leaves template-eligible (D-05 altitude); a sibling `BuildBuiltinRootFile` using a built-in-owned `CLEF` root drives the precedence negative.

## Deviations from Plan
None - plan executed exactly as written. (The plan explicitly permitted the "declare the engine class as stubs OR defer to Wave 1 — pick the split that compiles" choice; the stub split was taken and is documented above, not a deviation.)

## Issues Encountered
- `dotnet test --no-build` first ran the STALE `bin/Debug/net472` assembly (which lacks the new Template tests) because the solution was built Release. Resolved by passing `-c Release` so discovery targets the freshly-built Release assembly. The VALIDATION.md quick-run command omits `-c Release`; future task verification in this phase should pass `-c Release` (or build Debug) to avoid discovering a stale assembly.

## Next Phase Readiness
- The frozen surface is in place: Wave 1 (plan 23-02+) implements `KernelCodec.Decode/Encode` against `ITemplateContracts.cs` and removes the `Skip=` on `KernelCodecTests` per-type + count-recompute goldens (the GREEN gate).
- The CRITICAL DEC-C3 count-from-prior grow/shrink golden exists and is resolvable by `--filter "Template&CountRecompute&Roundtrip"` (currently 2 skipped tests).
- No blockers.

## Self-Check: PASSED
- FOUND: UtinniCoreDotNet/Formats/Template/ITemplateContracts.cs
- FOUND: Utinni.Cli.Tests/Template/TemplateTestFixtures.cs
- FOUND: Utinni.Cli.Tests/Template/KernelCodecTests.cs
- FOUND: Utinni.Cli.Tests/Template/RoundtripTemplateCommandTests.cs
- FOUND: Utinni.Cli.Tests/Template/ApplySaveTemplateTests.cs
- FOUND commit: d4afba7 (Task 1)
- FOUND commit: b916bbe (Task 2)

---
*Phase: 23-user-definable-iff-chunk-templates*
*Completed: 2026-06-21*
