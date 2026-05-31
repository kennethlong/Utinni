---
phase: 11-tjt-subpanel-object-template-editor
plan: 01
subsystem: format-core
tags: [object-template, iff, self-describing-codec, hybrid-dom, byte-exact, mutable-model]

# Dependency graph
requires:
  - phase: 07-tjt-subpanel-tre-browser-read-only
    provides: ObjectTemplateDecoder parse path + LooksLikeObjectTemplate sniff + IffPayloadCursor
  - phase: 08-tjt-subpanel-iff-editor-read-write
    provides: MutableIffDocument hybrid-DOM + MutableIffNode in-place leaf mutation + IffWriter byte-exact serializer
  - phase: 09-tjt-subpanel-datatable-editor-tab
    provides: MutableDataTableDocument mutable-model-over-IFF shape + DataTableCellValue value-union shape
provides:
  - Self-describing object-template param value model (typed scalar union + delta byte + raw-bytes hex fallback)
  - Defensive consume-exactly-or-hex param codec (decode/encode byte-exact)
  - ReadInt8 1-byte cursor primitive on IffPayloadCursor
  - Mutable object-template model over MutableIffDocument (edit/add/remove local param chunks, machine-managed count)
  - Byte-exact object-template writer composing IffWriter
affects: [11-02-resolver, 11-03-editor-host, 11-04-mutations-widgets, 11-05-save-reload]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Self-describing scalar decode with consume-exactly-or-hex defensive posture (T-11-01)"
    - "Hybrid-DOM in-place leaf mutation for byte-exact untouched-param preservation (OT caveat over full rebuild)"
    - "Machine-managed param-count re-derivation on structural mutation (D-04)"

key-files:
  created:
    - UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateParamValue.cs
    - UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateParamCodec.cs
    - UtinniCoreDotNet/Formats/ObjectTemplate/MutableObjectTemplate.cs
    - UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateWriter.cs
    - UtinniCoreDotNet.Tests/ObjectTemplate/ObjectTemplateParamTests.cs
    - UtinniCoreDotNet.Tests/ObjectTemplate/MutableObjectTemplateTests.cs
  modified:
    - UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj

key-decisions:
  - "int vs float SINGLE numeric is byte-indistinguishable to the generic schema-free decoder; canonical decode is typed Int (delta preserved). Byte-exactness holds either way because the 4 value bytes + delta re-emit verbatim. V2's typed schema disambiguates the widget label."
  - "Hex-fallback variant carries the verbatim value-region bytes (everything after the field-name NUL), so any complex/ambiguous param round-trips byte-for-byte and is never mis-typed."
  - "EditOverride mutates the captured MutableIffNode leaf in place (hybrid-DOM); never a full rebuild — untouched params re-emit their captured slice through IffWriter."

patterns-established:
  - "Consume-exactly-or-hex: a scalar decode is accepted only when it consumes cursor.Remaining == 0; any inconsistency routes to RawBytesHexFallback (T-11-01 mitigation)."
  - "Machine-managed count: Add/RemoveOverride re-derive the int32 paramCount leaf from the live local-param count (mirrors MutableDataTableDocument writing Columns.Count)."

requirements-completed: [PROD-W1-OT]

# Metrics
duration: ~45min
completed: 2026-05-31
---

# Phase 11 Plan 01: Object Template Format Core Summary

**Self-describing typed object-template param codec (consume-exactly-or-hex defensive decode/encode) + a mutable model over Phase 8's MutableIffDocument that edits/adds/removes local param chunks byte-exactly with a machine-managed count, plus the single new ReadInt8 cursor primitive.**

## Performance

- **Duration:** ~45 min
- **Started:** 2026-05-31 (Phase 11 execution start)
- **Completed:** 2026-05-31
- **Tasks:** 2 (both TDD auto tasks)
- **Files modified:** 8 (6 created, 2 modified)

## Accomplishments
- `ObjectTemplateParamValue` — sealed discriminated value carrier (Bool/Int/Float/String/None/RawBytesHexFallback) with verbatim delta byte and the UI-SPEC `ParamTypeLabel` accessor.
- `ObjectTemplateParamCodec` — self-describing data-type-tag decode/encode with the RESEARCH-locked consume-exactly-or-hex defensive posture (WEIGHTED_LIST / RANGE / DIE_ROLL / short / long → hex fallback, never mis-typed).
- `ReadInt8()` — the only read-layer primitive Phase 11 adds to `IffPayloadCursor` (binary-compat safe; the type is `internal sealed`).
- `MutableObjectTemplate` — mutable model over the Phase 8 `MutableIffDocument` (FromMutableIff parse; version / DERV base / ordered local params). EditOverride mutates the captured leaf in place; Add/RemoveOverride re-derive the machine-managed `int32` paramCount leaf from the live count (D-04).
- `ObjectTemplateWriter` — composes `IffWriter.Write` over the mutated SourceIff (no bespoke serializer; the Phase 9 DataTableWriter shape).
- 15 new xUnit facts (10 codec + 5 mutable model), all green; full suite 608/608 green Debug+Release|x86 (no regression above the 475+ baseline).

## Task Commits

Each task was committed atomically:

1. **Task 1: Self-describing param value model + codec + ReadInt8 cursor helper** — `407d62b` (feat)
2. **Task 2: Mutable object-template model + byte-exact writer** — `d73a58c` (feat)

**Plan metadata:** (final docs commit — this SUMMARY + STATE/ROADMAP/REQUIREMENTS)

_Both tasks carried `tdd="true"`. Each new implementation file was created together with its test file and committed as one atomic `feat` per task (the implementation and its [Fact]s are inseparable new code in a from-scratch format folder)._

## Files Created/Modified
- `UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateParamValue.cs` — typed scalar value union + delta byte + raw-bytes hex-fallback variant + ParamTypeLabel.
- `UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateParamCodec.cs` — self-describing tag decode/encode with consume-exactly-or-hex posture; returns/consumes ObjectTemplateParamEntry.
- `UtinniCoreDotNet/Formats/ObjectTemplate/MutableObjectTemplate.cs` — mutable model over MutableIffDocument; edit/add/remove local param chunks; machine-managed count.
- `UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateWriter.cs` — byte-exact serialize composing IffWriter over the mutated MutableIffDocument.
- `UtinniCoreDotNet.Tests/ObjectTemplate/ObjectTemplateParamTests.cs` — 10 facts (scalar decode/encode byte-identity, '+' delta verbatim, NONE, WEIGHTED_LIST + short/long → hex fallback, ParamTypeLabel).
- `UtinniCoreDotNet.Tests/ObjectTemplate/MutableObjectTemplateTests.cs` — 5 facts (no-mutation == input bytes exactly, EditOverride leaves other chunks byte-identical, Add +1 / Remove -1 count).
- `UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs` — added `ReadInt8()`.
- `UtinniCoreDotNet/UtinniCoreDotNet.csproj` — 4 new `<Compile Include>` entries (old-style project).

## Decisions Made
- **int/float numeric SINGLE → typed Int (canonical).** The on-disk SINGLE numeric region (`tag · delta · 4 value bytes`) is byte-identical for int and float; the generic, schema-free V1 decoder (D-03) cannot disambiguate from bytes alone. It decodes the canonical numeric SINGLE as a typed Int with the delta byte preserved verbatim. Byte-exactness is preserved regardless: encoding an Int re-emits the same delta + 4 value bytes the region carried, so a float-intended param still round-trips byte-for-byte (only the editor's widget label differs; V2's typed schema disambiguates). A typed Float value carried in from a caller likewise encodes its 4 value bytes verbatim. This satisfies the plan's must_haves truth #3 (delta round-trips verbatim) and the byte-exact truth simultaneously.
- **Hex-fallback carries the verbatim value-region bytes** (everything after the field-name NUL), making the round-trip trivially byte-exact for any complex/ambiguous param.
- **bool sniff requires a 0/1 value byte** so a malformed 1-byte-after-tag region with a non-boolean byte routes to hex fallback rather than a misleading typed bool.

## Deviations from Plan

None — plan executed exactly as written. The plan's Task 1 explicitly granted executor discretion on the int-vs-float and Vector/TriggerVolume/StringId nested-scalar decodes within D-02 ("MAY be implemented or routed to fallback"); the canonical-Int decision above is taken inside that granted latitude, not a deviation. No CLAUDE.md exists in the working tree (confirmed by RESEARCH); no auto-fixes (Rules 1-3) or architectural escalations (Rule 4) were needed.

## Issues Encountered
- **Git Bash mangled `/p:` MSBuild switches** into a path (`'C:/Program Files/Git/nologo'`). Resolved by using dash-prefixed switches (`-p:Configuration=...`) when invoking the Dev18 MSBuild from the Bash tool.
- **Generated/UtinniCore.cs regen churn** (CppSharp reorders on every build) was `git checkout --`'d after each build per the locked regen-churn rule; never committed.

## Threat Surface
The two threat-register mitigations for this plan are covered:
- **T-11-01** (codec over-read on a forged param chunk) — decode runs exclusively through the bounds-checked `IffPayloadCursor`; the consume-exactly guard routes any inconsistent payload to RawBytesHexFallback. Asserted by `ShortScalarPayload_RoutesToHexFallback` and `LongScalarPayload_RoutesToHexFallback`.
- **T-11-02** (scalar mis-decode corrupts on write) — hybrid-DOM in-place mutation re-emits untouched params verbatim; the no-mutation == input-bytes [Fact] (`Serialize_NoMutation_ReturnsInputBytesExactly`) is the regression gate.

No new security-relevant surface (network/auth/file-access at trust boundaries) was introduced beyond the planned disk-bytes → codec/model and model → bytes boundaries already in the plan's threat model. No threat flags.

## User Setup Required
None — no external service configuration required.

## Verification Performed
- `dotnet test --no-build --filter ObjectTemplateParam` → 10 passed (Task 1 gate).
- `dotnet test --no-build --filter MutableObjectTemplate` → 5 passed (Task 2 gate).
- `dotnet test --no-build --filter ObjectTemplate` → 15 passed (both classes).
- Full `UtinniCoreDotNet.Tests` suite: **608 passed, 0 failed** in BOTH Debug|x86 and Release|x86 (no regression; baseline was 475+).
- Build: VS2026 MSBuild (Dev18, `D:\Program Files\Microsoft Visual Studio\18\Community`) Debug+Release|x86 clean (warnings only — pre-existing CS0108 in Generated/UtinniCore.cs and xUnit analyzer style warnings).
- Acceptance grep gates: `ReadInt8` present in IffPayloadCursor.cs; `FromDocument|IffWriter` ≥2 across the two new files; `UndoRedoManager` returns 0 matches in `Formats/ObjectTemplate/` (CON-M-05 disentanglement).

## Next Phase Readiness
- **Ready for 11-02 (inheritance-chain resolver + round-trip CLI golden):** the typed param decode (`ObjectTemplateParamCodec`) and the local-param model (`MutableObjectTemplate.LocalParams`) are the inputs the DERV-chain effective-merge resolver consumes. `RootType` / `Version` / `BaseTemplateName` are exposed on the model for the resolver's chain walk.
- **Ready for 11-03/04 (editor host + mutations):** EditOverride/AddOverride/RemoveOverride are the three D-04 mutations the editor-local undo controller (clone of DatatableEditController) will wrap; `ParamTypeLabel` feeds the UI Type column.
- No blockers. CON-M-05 disentanglement verified (zero scene-UndoRedoManager coupling in the format core).

## Self-Check: PASSED

All 6 created source/test files + the SUMMARY exist on disk; both task commits (`407d62b`, `d73a58c`) are present in git history.

---
*Phase: 11-tjt-subpanel-object-template-editor*
*Completed: 2026-05-31*
