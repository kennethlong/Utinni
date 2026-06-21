---
phase: 23-user-definable-iff-chunk-templates
plan: 05
subsystem: cli
tags: [iff, template-engine, verbs-first, dec-v2-verbs-first, dec-c3-byte-exact, d-15, d-14-worked-examples, apply-save, fail-closed, path-containment, d-12-pack-allow-list]

# Dependency graph
requires:
  - phase: 23-user-definable-iff-chunk-templates
    plan: 03
    provides: "KernelCodec.Decode/Encode (byte-exact, D-10 count auto-recompute) + FitChecker.FitCheck + TemplateJson (de)serializer"
  - phase: 23-user-definable-iff-chunk-templates
    plan: 04
    provides: "TemplateResolver.Resolve (version-FORM match + D-05 altitude + D-07 fit) + TemplatePackStore (D-12 scanned allow-list, LoadAll/LoadEffective/DefaultRoots)"
  - phase: 14-mcp-server
    provides: "ApplySaveIffCommand (the --root-contained/atomic/fail-closed apply-save backbone + TestPerturbSerialized seam) + LooseOverridePath.Resolve + SaveCommandIo.WriteAtomic"
  - phase: 13-cli-verbs
    provides: "DecodeTrnCommand (the alias-delegation precedent) + JsonOutput.EmitSuccess/EmitError + Program.cs Type[] ParseArguments + Dispatch"
provides:
  - "decode-with-template: resolves a template-eligible leaf, decodes via KernelCodec, emits a navigable envelope + the D-07 FitReport (a no-match/built-in-suppressed/ambiguous leaf yields a clear matched=false envelope, not a crash)"
  - "roundtrip-template: the DEC-C3 byte-exact gate — decode->encode->assert byte-identical (exit 0 identical / exit 2 mismatch)"
  - "apply-save-template: clones ApplySaveIffCommand's contained/atomic/fail-closed backbone verbatim; the ONLY delta is newPayload = KernelCodec.Encode (decode -> --set scalar edits -> encode)"
  - "list-templates: enumerates the D-12 scanned pack allow-list, reporting each template's match key + source pack + load-order priority + skip reasons"
  - "Two shipped D-14 worked-example templates (counted_records.json count-from-prior + flat_composite.json preset composite) that double as byte-exact WorkedExample goldens"
  - "TemplatePackStore.DefaultRoots now scans the shipped Examples dir too, so the worked examples are discoverable by default"
affects: [23-06, 23-07, 23-08]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Shared TemplateDecodeShared pool/resolve/decode-envelope builder consumed by decode-with-template, roundtrip-template, AND apply-save-template so the three verbs' resolve+decode logic cannot fork (the decode-trn alias-delegation precedent)"
    - "apply-save-template is a byte-for-byte structural clone of ApplySaveIffCommand's backbone — only the newPayload source changes (KernelCodec.Encode instead of a --mutate-hex literal), so the proven containment/atomic/fail-closed/verify guarantees transfer wholesale"
    - "The TestPerturbSerialized Func<byte[],byte[]> seam is reused (same shape as ApplySaveIffCommand) so the fail-closed golden corrupts an untouched leaf in the serialized output and proves no-write-on-failed-verify"
    - "Worked-example templates ship as Content/CopyToOutputDirectory under Formats/Template/Examples and double as the WorkedExample goldens — the teaching artifact IS the golden (DEC-C3)"
    - "matchTagOnly worked examples engage on CNTR/PRST at every version-FORM (the layout is version-invariant for these leaves), still version-FORM-aware per D-04"

key-files:
  created:
    - Utinni.Cli/Commands/DecodeWithTemplateCommand.cs
    - Utinni.Cli/Commands/RoundtripTemplateCommand.cs
    - Utinni.Cli/Commands/ListTemplatesCommand.cs
    - Utinni.Cli/Commands/ApplySaveTemplateCommand.cs
    - UtinniCoreDotNet/Formats/Template/Examples/counted_records.json
    - UtinniCoreDotNet/Formats/Template/Examples/flat_composite.json
    - Utinni.Cli.Tests/Template/TemplateAuthoring.cs
  modified:
    - Utinni.Cli/Program.cs
    - UtinniCoreDotNet/Formats/Template/TemplatePackStore.cs
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj
    - Utinni.Cli.Tests/Template/RoundtripTemplateCommandTests.cs
    - Utinni.Cli.Tests/Template/ApplySaveTemplateTests.cs
    - Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt
    - Utinni.Cli.Tests/Fixtures/dispatch/no-args.expected.txt

key-decisions:
  - "apply-save-template's edit model is --mutate-leaf <stableId> + repeatable --set name=value: the leaf is decoded through its resolved template, the named scalar fields are coerced to their decoded CLR type and overwritten, then re-encoded. This keeps the verb a thin shell over KernelCodec.Encode and reuses the exact ApplySaveIffCommand control flow"
  - "TemplatePackStore.DefaultRoots adds a second shipped root (Formats/Template/Examples, priority 1) so the D-14 worked examples are discovered by list-templates and the resolver by default — the plan's verify step ('the examples enumerate') would otherwise fail since the examples ship outside the Presets pack dir"
  - "The worked-example templates use matchTagOnly=true (not a version-pinned ancestor path) because the CNTR/PRST fixtures have a version-invariant layout — a single template byte-exactly round-trips both VersionLow (0001) and VersionHigh (0003), and the Theory asserts both"
  - "Program.cs verb registration was split across the two task commits (3 verbs in Task 1, the 4th in Task 2) so each task's commit is independently buildable; the Type[] ParseArguments overload has no arity cap (33 verbs now registered)"

patterns-established:
  - "A single shared resolve+decode-envelope builder backs every read/roundtrip/write template verb — the verbs-first surface cannot drift from itself or from a future decode-iff --template branch"
  - "Cloning a proven apply-save backbone and swapping ONLY the payload source is the safe way to add a new typed apply-save member (inherits containment/atomic/fail-closed for free)"

requirements-completed: [PROD-IFFT-02, PROD-IFFT-03]

# Metrics
duration: 13min
completed: 2026-06-21
---

# Phase 23 Plan 05: Verbs-First Template Surface Summary

**The verbs-first engine surface (D-15 / DEC-V2-VERBS-FIRST): four `utinni-cli` verbs over the headless template engine — `decode-with-template` (navigable decode envelope + D-07 FitReport), `roundtrip-template` (the DEC-C3 byte-exact gate), `list-templates` (the D-12 scanned-pack inventory), and `apply-save-template` (a byte-for-byte clone of ApplySaveIffCommand's --root-contained/atomic/fail-closed backbone whose ONLY delta is newPayload = KernelCodec.Encode) — plus two shipped D-14 worked-example templates (count-from-prior + preset-composite) that double as the byte-exact WorkedExample goldens. All four verbs share one resolve+decode-envelope builder so they cannot fork; the previously RED verb goldens (Roundtrip / Decode / List / ApplySaveTemplate&FailClosed / WorkedExample) are all GREEN.**

## Performance

- **Duration:** ~13 min
- **Started:** 2026-06-21T04:19:26Z
- **Completed:** 2026-06-21T04:32:32Z
- **Tasks:** 2
- **Files modified:** 14 (7 created, 7 modified)

## Accomplishments
- **Task 1 — decode-with-template + roundtrip-template + list-templates:** three verbs cloning DecodeTrnCommand's whole-file shape (the `[Verb]`/`[Value(0)]`/File.Exists->exit-3 guard/typed-exception->exit-2 catch ladder/`// NOTE: Generic Exception intentionally NOT caught.` tail). A shared `TemplateDecodeShared` helper owns the ONE pack-load (explicit `--template` wins, else `TemplatePackStore.DefaultRoots(--templates-dir).LoadEffective()`), the leaf pick (explicit `--leaf` or the single auto-resolved eligible leaf), and the decode-envelope builder (resolve -> KernelCodec.Decode -> project values + the D-07 FitReport). `roundtrip-template` is the DEC-C3 gate: decode->encode->assert byte-identical (exit 0 identical, exit 2 mismatch/no-match). `list-templates` enumerates the scanned allow-list with each template's match key + source pack + priority + skip reasons. Registered all three in Program.cs.
- **Task 2 — apply-save-template + 4th-verb registration + 2 worked examples:** `ApplySaveTemplateCommand` clones ApplySaveIffCommand's control flow EXACTLY — `--root` containment via `LooseOverridePath.Resolve` (exit 2 on escape), File.Exists->exit 3, `MutableIffDocument.FromDocument`, `SetPayload`, `IffWriter.Write` (free length ripple), re-parse-for-validity, `CompareUntouchedLeaves` byte-identity verify (fail-closed exit 2, NO write), `SaveCommandIo.WriteAtomic` on clean verify, and the reused `TestPerturbSerialized` seam. The ONLY difference: `newPayload = KernelCodec.Encode(decoded)` after decoding the leaf and applying `--set name=value` scalar edits. Shipped `counted_records.json` (a u32 count + N×{u16,f32} CountFromField array — exercises D-10 count recompute) and `flat_composite.json` (vector + quaternion + matrix + color preset structs — exercises D-09 presets) as Content/CopyToOutputDirectory; both double as the WorkedExample goldens. `TemplatePackStore.DefaultRoots` now also scans the shipped Examples dir.
- **Goldens GREEN:** full Template suite 71 passed / 0 skipped / 0 failed (the 3 previously-RED-via-Skip verb goldens — Roundtrip, WorkedExample, ApplySaveTemplate&FailClosed — turned GREEN, plus new Decode / List / Containment tests). Full `Utinni.Cli.Tests`: 504 passed / 2 skipped / 0 failed. `utinni-cli list-templates` enumerates 9 templates (7 presets + 2 worked examples) and `utinni-cli --help` lists all 4 template verbs.

## Task Commits

Each task was committed atomically:

1. **Task 1: decode-with-template + roundtrip-template + list-templates verbs** - `cecafab` (feat)
2. **Task 2: apply-save-template verb + 2 worked-example templates + 4th verb registered** - `66b7beb` (feat)
3. **Deviation (Rule 1): rebless dispatch help/no-args verb-list goldens** - `b1fe2c5` (test)

**Plan metadata:** (this commit) (docs: complete plan)

_TDD note: both tasks are `tdd="true"`. The 23-01 RED-via-Skip verb goldens (RoundtripTemplateCommandTests / ApplySaveTemplateTests) were authored as real test bodies (the Wave-0 stubs `Assert.True(false)` under `[Skip]`); the verbs were then implemented to turn them GREEN. The byte-exact roundtrip + fail-closed + worked-example assertions are real behavioral checks, not skips._

## Files Created/Modified
- `Utinni.Cli/Commands/DecodeWithTemplateCommand.cs` - decode-with-template verb + the shared `TemplateDecodeShared` pool/leaf-pick/decode-envelope builder (consumed by all three resolve-bearing verbs)
- `Utinni.Cli/Commands/RoundtripTemplateCommand.cs` - roundtrip-template, the DEC-C3 byte-exact gate
- `Utinni.Cli/Commands/ListTemplatesCommand.cs` - list-templates, the D-12 scanned-pack inventory
- `Utinni.Cli/Commands/ApplySaveTemplateCommand.cs` - apply-save-template, the ApplySaveIffCommand clone with KernelCodec.Encode newPayload + the TestPerturbSerialized seam
- `UtinniCoreDotNet/Formats/Template/Examples/counted_records.json` - D-14 count-from-prior worked example (golden + teaching artifact)
- `UtinniCoreDotNet/Formats/Template/Examples/flat_composite.json` - D-14 preset-composite worked example (golden + teaching artifact)
- `Utinni.Cli.Tests/Template/TemplateAuthoring.cs` - inline TemplateModel authoring (KERN/wrong-layout/unmatched) + a throwaway TempDir helper
- `Utinni.Cli/Program.cs` - registered the 4 template verbs in ParseArguments (Type[]) + Dispatch
- `UtinniCoreDotNet/Formats/Template/TemplatePackStore.cs` - DefaultRoots adds the shipped Examples root
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj` - Content/CopyToOutputDirectory for the 2 worked examples
- `Utinni.Cli.Tests/Template/RoundtripTemplateCommandTests.cs` - verb-level roundtrip/decode/list/worked-example goldens
- `Utinni.Cli.Tests/Template/ApplySaveTemplateTests.cs` - fail-closed (perturbed untouched leaf, exit 2 no write) + containment (root escape, exit 2) goldens
- `Utinni.Cli.Tests/Fixtures/dispatch/{help,no-args}.expected.txt` - reblessed verb-list goldens (Rule 1 deviation)

## Decisions Made
See the `key-decisions` frontmatter. Load-bearing: (1) apply-save-template's edit model is `--mutate-leaf` + repeatable `--set name=value`, decoding the leaf through its template then re-encoding — thin over KernelCodec.Encode, reusing the ApplySaveIffCommand backbone verbatim; (2) DefaultRoots scans the shipped Examples dir so the worked examples enumerate by default (the plan's verify step requires it); (3) the worked examples are matchTagOnly so one template byte-exactly round-trips both version-FORMs.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Reblessed the dispatch help/no-args verb-list goldens**
- **Found during:** Task 2 (full-suite regression after registering the 4th verb)
- **Issue:** `CommandDispatchTests.Run_WithHelpFlag_...` and `..._WithNoArgs_...` compare CLI output against `Fixtures/dispatch/{help,no-args}.expected.txt`. Adding 4 verbs appends 4 rows AND widens the CommandLineParser description column (the longer `decode-with-template`/`apply-save-template` names push the wrap point right), so the goldens no longer matched.
- **Fix:** Reblessed both goldens from the actual CLI output after verifying via diff that the ONLY changes are the 4 new verb rows + the column re-wrap (no content drift on existing verbs).
- **Files modified:** `Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt`, `Utinni.Cli.Tests/Fixtures/dispatch/no-args.expected.txt`
- **Commit:** `b1fe2c5`

**2. [Rule 2 - Missing critical functionality] DefaultRoots scans the shipped Examples dir**
- **Found during:** Task 2
- **Issue:** The plan ships the worked examples under `Formats/Template/Examples/` and its verify step expects `list-templates` to enumerate them, but `TemplatePackStore.DefaultRoots` only scanned `Presets/`.
- **Fix:** Added a second shipped root (`Formats/Template/Examples`, priority 1) to `DefaultRoots` so the worked examples are discovered by `list-templates` and the resolver by default. Single-sourced through the same `TemplatePackRoot` mechanism (no new loader).
- **Files modified:** `UtinniCoreDotNet/Formats/Template/TemplatePackStore.cs`
- **Commit:** `66b7beb`

## Authentication Gates

None.

## Issues Encountered

- **Verb registration ordering vs per-task buildability:** the 3 Task-1 verbs were registered in Program.cs in Task 1's commit (not deferred to Task 2 as the plan literally split it) because the verbs are not invokable — and the Task-1 goldens not runnable — until they are in ParseArguments + Dispatch. The 4th verb (apply-save-template) registered in Task 2. Both task commits build clean in isolation. (Bookkeeping, not code.)
- **Git Bash MSBuild switch + multi-project:** used dash-form `-p:`/`-t:` switches (Git Bash mangles `/t:`); `MSBuild.exe` rejects multiple `.csproj` args (MSB1008), so the solution was rebuilt instead. (Tooling, not code.)
- **Pre-existing out-of-scope failure — `AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline`:** the documented Phase-17 harness gotcha (incremental MSBuild skips the post-build `UtinniCoreDotNetGen.exe` regen). VERIFIED out of scope: this plan is pure managed `Utinni.Cli` + `Formats/Template` work; `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs` was run after each build and the file is unchanged. Not chased (already in deferred-items.md + the prior-wave note).

## Known Stubs

None. All four verbs are complete and wired to the real engine (TemplateResolver + KernelCodec + TemplatePackStore + the ApplySaveIffCommand security backbone). The worked-example templates carry real layouts that byte-exactly round-trip their fixtures. No hardcoded empty values, placeholder text, or unwired data paths.

## Next Phase Readiness
- The verbs-first surface is complete: 23-06 (MCP) wraps these 4 verbs as thin dispatchers (zero format logic per DEC-V2-MCP-OOP); 23-07 (UI) consumes `list-templates` for its picker and `decode-with-template`/`apply-save-template` for the editor.
- `TemplateDecodeShared` is the single resolve+decode-envelope builder the MCP/UI route through (no re-implementation).
- Carried forward (out of scope): the pre-existing CppSharp ABI-baseline drift (deferred-items.md) still needs a dedicated re-bless task.

## Self-Check: PASSED
- FOUND: Utinni.Cli/Commands/DecodeWithTemplateCommand.cs
- FOUND: Utinni.Cli/Commands/RoundtripTemplateCommand.cs
- FOUND: Utinni.Cli/Commands/ListTemplatesCommand.cs
- FOUND: Utinni.Cli/Commands/ApplySaveTemplateCommand.cs
- FOUND: UtinniCoreDotNet/Formats/Template/Examples/counted_records.json
- FOUND: UtinniCoreDotNet/Formats/Template/Examples/flat_composite.json
- FOUND commit: cecafab (Task 1)
- FOUND commit: 66b7beb (Task 2)
- FOUND commit: b1fe2c5 (golden rebless)

---
*Phase: 23-user-definable-iff-chunk-templates*
*Completed: 2026-06-21*
