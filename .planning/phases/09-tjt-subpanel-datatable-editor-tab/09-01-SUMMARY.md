---
phase: 09-tjt-subpanel-datatable-editor-tab
plan: 01
subsystem: file-format
tags: [datatable, dtii, tab, iff, crc, hybrid-dom, port, soe]

# Dependency graph
requires:
  - phase: 08-tjt-subpanel-iff-editor-read-write
    provides: "IffReader / IffWriter / MutableIffDocument / MutableIffNode / IffParseException — the byte-exact EA-IFF-85 read/write primitives Phase 9's typed DTII model composes on without modifying any public signature"
provides:
  - "DataTableColumnType — type-spec parser (i/f/s/c/h/p/b/e/v/z) + MangleValue port + EnumMap/BitVectorMap/DefaultValue/BasicType"
  - "DataTableCellValue — sealed Int/Float/String discriminated union + SerializeFresh + CF-04 value equality"
  - "DataTableHashCrc.Compute — SOE Crc::normalizeAndCalculate port (normalize + CRC32); empty/null hash = 0"
  - "DataTableDocument.FromIff — typed V0000/V0001 reader with per-cell original-slice capture + cell-count sanity cap"
  - "MutableDataTableCell — per-cell hybrid DOM with CaptureState()/RestoreState(CellState) undo primitive"
  - "MutableDataTableColumn / MutableDataTableRow / MutableDataTableDocument.BuildMutableIff — mutable model + FORM DTII tree materialization"
  - "DataTableWriter.Serialize / BuildMutableIff — composes IffWriter.Write; exposes the MutableIffDocument tree for Plan 09-05 save targets"
  - "DatatableFixtures — 7 canonical-by-construction synthetic .tab builders (test-side)"
affects: [09-02, 09-03, 09-04, 09-05, 09-06, 09-07]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Per-cell hybrid DOM (CF-04): originalSlice + IsDirty mirroring MutableIffNode at cell granularity"
    - "CaptureState/RestoreState undo primitive — restores verbatim byte slice atomically, bypassing the Value setter's slice-nulling (item 3)"
    - "Canonical-by-construction fixtures — synthetic .tab built through the framework writer so round-trip whole-file equality is the correct assertion"

key-files:
  created:
    - "UtinniCoreDotNet/Formats/Datatable/DataTableColumnType.cs"
    - "UtinniCoreDotNet/Formats/Datatable/DataTableCellValue.cs"
    - "UtinniCoreDotNet/Formats/Datatable/DataTableHashCrc.cs"
    - "UtinniCoreDotNet/Formats/Datatable/DataTableParseException.cs"
    - "UtinniCoreDotNet/Formats/Datatable/DataTableDocument.cs"
    - "UtinniCoreDotNet/Formats/Datatable/MutableDataTableCell.cs"
    - "UtinniCoreDotNet/Formats/Datatable/MutableDataTableColumn.cs"
    - "UtinniCoreDotNet/Formats/Datatable/MutableDataTableRow.cs"
    - "UtinniCoreDotNet/Formats/Datatable/MutableDataTableDocument.cs"
    - "UtinniCoreDotNet/Formats/Datatable/DataTableWriter.cs"
    - "UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableColumnTypeTests.cs"
    - "UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableHashCrcTests.cs"
    - "UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableDocumentTests.cs"
    - "UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableWriterTests.cs"
    - "UtinniCoreDotNet.Tests/FormatsTests/Datatable/DatatableFixtures.cs"
  modified:
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj"
    - ".planning/phases/09-tjt-subpanel-datatable-editor-tab/09-UI-SPEC.md"
    - ".planning/phases/09-tjt-subpanel-datatable-editor-tab/09-CONTEXT.md"

key-decisions:
  - "DataTableCellValue shipped as a sealed abstract base with Int/Float/String subclasses (not a struct) — lets SerializeFresh and the editor widgets pattern-match on the concrete type with no out-of-band discriminator"
  - "DataTableHashCrc.Compute('') / Compute(null) returns 0, NOT 0xFFFFFFFF — the plan speculated 0xFFFFFFFF but the verified SOE crcNull = Crc::calculate('') = CRC_INIT ^ CRC_INIT = 0 (Crc.cpp:19,73-76)"
  - "CellState shipped as internal readonly struct (value + originalSlice + isDirty + needsReview); CaptureState() returns it, RestoreState(CellState) restores all four atomically"
  - "DT_Comment columns preserved in COLS+TYPE with zero-byte ROWS payload (item 13) — Phase 9 round-trips from disk, so it does NOT replicate SOE _saveColumns/_saveTypes comment-column skip"

patterns-established:
  - "Old-style csproj explicit <Compile Include> for every new Formats/Datatable/*.cs file (round-2 HIGH-A, Phase 8 Plan 05 precedent)"
  - "Internal CaptureState/RestoreState consumed by SC4 writer test now, Plan 09-04 EditCellValue + 09-06 ApplyCsvImport later"

requirements-completed: [PROD-W1-DT]

# Metrics
duration: ~50min
completed: 2026-05-29
---

# Phase 9 Plan 01: Typed DTII Format Primitives Summary

**Ten pure-managed `Formats/Datatable/` files port the SOE DTII model — a type-spec parser + MangleValue, a normalize-then-CRC32 hash, a V0000/V0001 typed reader with per-cell original-byte capture, and a writer that composes Phase 8's IffWriter — with a CaptureState/RestoreState undo primitive that makes the edit→undo→byte-exact-load invariant hold.**

## Performance

- **Duration:** ~50 min
- **Started:** 2026-05-29T~21:45Z
- **Completed:** 2026-05-29T22:34Z
- **Tasks:** 3 (Task 0 planning-artifact correction + Task 1 + Task 2, both TDD)
- **Files modified/created:** 15 created + 3 modified

## Accomplishments
- `DataTableColumnType` parses all ten discriminators with the corrected `e(a=0,b=1,c=2)[default]` enum grammar, ports `MangleValue` (Bool/HashString/Enum/BitVector/PackedObjVars coercion), and ships strict PackedObjVars + BitVector parse-back validators.
- `DataTableHashCrc.Compute` is a faithful `Crc::normalizeAndCalculate` port (slash-collapse / dot-after-slash drop / ASCII case-fold + standard CRC32 table), with the empty-string value corrected to the true SOE `crcNull` of 0.
- `DataTableDocument.FromIff` reads V0000 (int ordinals) and V0001 (type-spec strings), decodes each cell on `BasicType`, captures the exact ROWS byte slice per cell (CF-04), and enforces a 16 M-cell DoS sanity cap.
- `MutableDataTableCell` carries the hybrid-DOM state plus the internal `CaptureState()/RestoreState(CellState)` API; `MutableDataTableDocument.BuildMutableIff` emits the `FORM DTII{FORM <ver>{COLS,TYPE,ROWS}}` tree (comment columns preserved, zero-byte comment cells); `DataTableWriter` composes `IffWriter.Write`.
- 73 Datatable xUnit facts (40 Task 1 + 33 Task 2) including the SC4 `EditCellValue → RestoreState-Undo → bytes EXACTLY equal loaded` invariant and its negative Value-set-back counter-case. Full UtinniCoreDotNet.Tests suite 404/404 green; Debug + Release|x86 build clean on the new files.

## Task Commits

1. **Task 0: UI-SPEC + CONTEXT enum-syntax typo (Assumption A1)** - `ef192d1` (docs)
2. **Task 1: DataTableColumnType + CellValue + HashCrc + ParseException + tests** - `ca7cd31` (feat, TDD)
3. **Task 2: typed reader + mutable hybrid-DOM + writer + fixtures + tests** - `4880f47` (feat, TDD)

_Note: this plan's two implementation tasks are TDD; tests and implementation landed together per task (the test project's SDK-glob requires the implementation to compile before the no-build test run, so RED/GREEN are squashed into one atomic feat commit per task)._

## Files Created/Modified
- `Formats/Datatable/DataTableColumnType.cs` - type-spec parser + MangleValue port + enum/bitvector maps
- `Formats/Datatable/DataTableCellValue.cs` - Int/Float/String union + SerializeFresh + value equality
- `Formats/Datatable/DataTableHashCrc.cs` - normalize + CRC32 port
- `Formats/Datatable/DataTableParseException.cs` - parse exception (IffParseException shape)
- `Formats/Datatable/DataTableDocument.cs` - FromIff typed reader + per-cell slice capture + DoS cap
- `Formats/Datatable/MutableDataTableCell.cs` - hybrid-DOM cell + CaptureState/RestoreState + WritePayload
- `Formats/Datatable/MutableDataTableColumn.cs` - name + type + dirty/added/reordered flags
- `Formats/Datatable/MutableDataTableRow.cs` - cells + computed dirty roll-up
- `Formats/Datatable/MutableDataTableDocument.cs` - version + columns + rows + BuildMutableIff
- `Formats/Datatable/DataTableWriter.cs` - Serialize / BuildMutableIff composing IffWriter
- `UtinniCoreDotNet.csproj` - 10 explicit `<Compile Include>` entries (old-style)
- 5 test files under `UtinniCoreDotNet.Tests/FormatsTests/Datatable/` (auto-glob, no test-csproj edit)
- `09-UI-SPEC.md` / `09-CONTEXT.md` - corrected enum grammar (Task 0)

## CaptureState / RestoreState contract (for Plan 09-04 / 09-06 consumers)
- `internal struct MutableDataTableCell.CellState { DataTableCellValue Value; byte[] OriginalSlice; bool IsDirty; bool NeedsReview; }`
- `internal CellState MutableDataTableCell.CaptureState()` — snapshots all four fields (slice reference copied; it is never mutated in place)
- `internal void MutableDataTableCell.RestoreState(CellState state)` — restores all four directly, bypassing the `Value` setter's slice-nulling so `WritePayload` re-emits the original bytes verbatim
- Both are `internal` (assembly-visible to the framework + InternalsVisibleTo test/CLI assemblies); 09-04 EditCellValue UndoOp and 09-06 ApplyCsvImport per-cell revert MUST use RestoreState, never a second `Value` set.

## Decisions Made
See `key-decisions` frontmatter. The load-bearing one: **CRC empty/null = 0**, contradicting the plan's speculative `0xFFFFFFFF`. Verified against `Crc.cpp:19` (`crcNull = Crc::calculate("")`) and `Crc.cpp:73-76` (`calculate` returns `crc ^ CRC_INIT`, so an empty loop yields `CRC_INIT ^ CRC_INIT = 0`). The pinned `Compute("")==0` fact encodes this.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] CRC empty-string reference value corrected from 0xFFFFFFFF to 0**
- **Found during:** Task 1 (DataTableHashCrc port + DataTableHashCrcTests)
- **Issue:** The plan's behavior block + DataTableHashCrcTests guidance asserted `Compute("") == 0xFFFFFFFF`. The verified SOE source (`Crc.cpp:19,66-77`) computes `crcNull = Crc::calculate("")`, and `calculate` final-XORs with `CRC_INIT`, so the empty-loop result is `CRC_INIT ^ CRC_INIT = 0`. Pinning `0xFFFFFFFF` would have encoded a wrong reference value and made DT_HashString cells un-resolvable client-side (the very T-09-04 drift the test exists to prevent).
- **Fix:** Implemented the faithful port (returns 0 for null/empty) and pinned the test to `0u`; documented the correction in `DataTableHashCrc.cs` XML + this summary.
- **Files modified:** `DataTableHashCrc.cs`, `DataTableHashCrcTests.cs`
- **Verification:** `Compute_EmptyString_IsZero` + `Compute_Null_IsZero` pass; normalize-equivalence + case-fold facts pin the algorithm shape.
- **Committed in:** `ca7cd31` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 bug — wrong reference constant in the plan text).
**Impact on plan:** The correction is required for byte-exact parity with the SOE client; it strengthens the Open Question 1 resolution rather than changing scope. No scope creep.

## Issues Encountered
- The `grep -c "Formats\Datatable\..."` verification commands look like they return 0 under a bash shell because bash collapses the `\\\\` escaping before grep sees it; the csproj entries ARE present (confirmed via the ripgrep-based Grep tool returning 10 Datatable entries). The plan's literal commands match correctly in a shell that passes `\\` through to grep.

## Open Question Follow-ups
- **Open Question 1 (CRC reference values):** resolved at port time. The empty-string value is pinned to the verified SOE `crcNull = 0`. No real-world CombatDataTable hash-column reference value was decoded (no on-disk fixture is checked in per Assumption A6); the pinned representative-string facts assert algorithm STABILITY (case-fold + slash-normalize equivalence + non-zero). If a future plan decodes a real `.tab` hash column whose stored int32 contradicts this port, update the pin and note it here.

## Next Phase Readiness
- Plan 09-02 (CLI roundtrip-tab golden gate) can author `RoundtripTabCommand.cs` + its `DataTableFixtureBuilder` against `DataTableDocument.FromIff` / `DataTableWriter.Serialize` now.
- Plan 09-03 can measure DataGridView perf against `DatatableFixtures.BuildV1CombatDataTableLike()` (200×30).
- Plan 09-04 EditCellValue + Plan 09-06 ApplyCsvImport have the CaptureState/RestoreState undo primitive ready.
- No blockers. No new external packages (slopcheck surface empty per T-09-SC).

## Self-Check: PASSED

All 10 framework files + 5 test files + SUMMARY.md verified present on disk; all three task commits (`ef192d1`, `ca7cd31`, `4880f47`) verified in git log.

---
*Phase: 09-tjt-subpanel-datatable-editor-tab*
*Completed: 2026-05-29*
