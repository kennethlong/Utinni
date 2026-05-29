---
phase: 09-tjt-subpanel-datatable-editor-tab
plan: 02
subsystem: cli-harness
tags: [datatable, dtii, cli, golden-tests, sc4, byte-exact]
requires:
  - "09-01: DataTableDocument.FromIff / MutableDataTableDocument / DataTableWriter.Serialize / MutableDataTableCell"
  - "Phase 8: IffReader.Read / MutableIffDocument.FromDocument / IffWriter / JsonOutput envelope"
provides:
  - "utinni-cli roundtrip-tab verb (CF-02 structural-correctness gate for SC4)"
  - "MutableDataTableDocument.RemoveRowAt / RemoveColumnAt / ResolveColumnIndex (public structural primitives, reusable by 09-04/09-06)"
  - "MutableDataTableCell.GetOriginalSliceForCompare (read-side ROWS-slice accessor)"
  - "DataTableColumnType.TryCoerceCellValue (mangle + typed-value factory, reusable by 09-04/09-06)"
  - "DataTableFixtureBuilder (CLI-side fixture builder, lock-step with 09-01 DatatableFixtures)"
affects:
  - "Plan 09-04 (wires --add-row/--add-column/--change-column-type into RoundtripTabCommand additively; consumes RemoveRowAt/RemoveColumnAt/TryCoerceCellValue)"
  - "Plan 09-06 (CSV import path can invoke roundtrip-tab to validate byte-exact-untouched cells)"
tech-stack:
  added: []
  patterns:
    - "Phase 4/8 CLI golden harness: InProcessCliRunner + JsonOutput sorted-key envelope + committed golden JSON compared via JToken.DeepEquals"
    - "per-cell ROWS-slice comparison: re-parse BOTH byte arrays into fresh no-op-loaded docs, pair untouched cells by stable (row,col) index with explicit index-shift maps"
key-files:
  created:
    - "Utinni.Cli/Commands/RoundtripTabCommand.cs"
    - "Utinni.Cli.Tests/Infrastructure/DataTableFixtureBuilder.cs"
    - "Utinni.Cli.Tests/Commands/RoundtripTabCommandTests.cs"
    - "Utinni.Cli.Tests/Goldens/roundtrip-tab/*.json (8 envelopes)"
  modified:
    - "Utinni.Cli/Program.cs (additive ParseArguments + MapResult)"
    - "UtinniCoreDotNet/Formats/Datatable/MutableDataTableDocument.cs (RemoveRowAt/RemoveColumnAt/ResolveColumnIndex)"
    - "UtinniCoreDotNet/Formats/Datatable/MutableDataTableRow.cs (RemoveCellInternal)"
    - "UtinniCoreDotNet/Formats/Datatable/MutableDataTableCell.cs (GetOriginalSliceForCompare)"
    - "UtinniCoreDotNet/Formats/Datatable/DataTableColumnType.cs (TryCoerceCellValue)"
    - "Utinni.Cli.Tests/Utinni.Cli.Tests.csproj (Goldens/** Content copy)"
    - "Utinni.Cli.Tests/Fixtures/dispatch/{help,no-args}.expected.txt (new verb in help listing)"
decisions:
  - "Adapted to the real GoldenTestRunner/committed-.json infra; the plan's referenced GSD_GOLDEN_UPDATE=1 mechanism does not exist in this codebase (Rule 3)."
  - "Added public structural+coercion primitives to the framework (RemoveRowAt/RemoveColumnAt/ResolveColumnIndex/TryCoerceCellValue/GetOriginalSliceForCompare) because the verb lives in Utinni.Cli (no InternalsVisibleTo) and the plan-required mutations had no public surface yet (Rule 2/3)."
metrics:
  duration: "~40 min"
  completed: "2026-05-29"
  tasks: 2
  files: "3 created + 7 modified + 8 goldens"
---

# Phase 9 Plan 02: CLI roundtrip-tab Golden Gate Summary

Shipped the `utinni-cli roundtrip-tab` verb — the CLI-level structural-correctness gate for Phase 9 (CF-02), the typed-datatable analog of Phase 8's `roundtrip-iff`. It loads a `.tab`, optionally applies one structural mutation (`--mutate-cell` / `--remove-row` / `--remove-column`), serializes via 09-01's `DataTableWriter`, re-parses, and asserts byte-exact identity on untouched cells — the automated enforcer for Success Criterion 4 ("no silent schema corruption").

## What shipped

**Task 1 — verb + wire + fixture builder (commit `375927f`):**
- `RoundtripTabCommand.cs`: `[Verb("roundtrip-tab")]` options + `Run(opts)`. Exit-code matrix mirrors `RoundtripIffCommand` verbatim (0 success / 1 UsageError / 2 DataTableParseException·IffParseException·IOException / 3 FileNotFound). JSON envelope via `JsonOutput.EmitSuccess`/`EmitError`.
- `Program.cs`: additive `RoundtripTabOptions` in `ParseArguments<...>` + matching `MapResult` lambda (inserted before `ValidatePluginOptions`).
- `DataTableFixtureBuilder.cs`: CLI-side duplicate of 09-01's `DatatableFixtures` (7 `Build*` helpers) + the `BuildV1OneCellDiffersFrom` one-cell-differs helper.

**Task 2 — golden suite (commit `251a495`):**
- `RoundtripTabCommandTests.cs`: **16 [Fact]s** — 6 no-mutate canonical fixtures, the SC4 mutate-cell byte-exact-untouched gate, the one-cell-differs isolation golden, remove-row + remove-column (index + by-name) index-shift, the FileNotFound/MalformedDtii/ConflictingFlags exit-code matrix, the non-optional drift detector, and the envelope-shape guard.
- **8 committed golden envelopes** under `Utinni.Cli.Tests/Goldens/roundtrip-tab/`, compared via `AssertGolden` (masked path + `JToken.DeepEquals`).

## Cross-link to the roundtrip-iff analog

`RoundtripTabCommand` mirrors `RoundtripIffCommand` at the verb-attribute, `Run()` exit-code, and JSON-envelope level. The structural differences are (a) the typed `DataTableDocument.FromIff` wrap after `MutableIffDocument.FromDocument`, (b) the `--mutate-cell`/`--remove-row`/`--remove-column` branch logic, and (c) the per-cell ROWS-slice comparison (vs roundtrip-iff's per-leaf-id comparison).

## The per-cell ROWS-slice comparison algorithm (iter-2 item 3)

After ANY mutation, the command re-parses **both** the loaded bytes and the serialized bytes into **fresh no-op-loaded** `MutableDataTableDocument`s (so every cell's `originalSlice` reflects exactly the bytes on disk for that cell's ROWS payload — it never reads the mutated in-memory doc, whose edited cell has a nulled slice). Untouched cells are paired by stable (row,col) index and their slices compared byte-for-byte via `GetOriginalSliceForCompare()`.

**Index-shift maps:**
- `--mutate-cell R,C`: identity map; cell (R,C) excluded, all others 1:1.
- `--remove-row N`: loaded row `r → r` for `r < N`; loaded row `r → r-1` for `r > N`; loaded row `N` excluded.
- `--remove-column N`: loaded col `c → c` for `c < N`; loaded col `c → c-1` for `c > N`; loaded col `N` excluded.

`bytesEqualUntouched` is true iff every paired untouched-cell slice is byte-identical; on a mismatch the envelope reports the first `{rowIndex, colIndex}` via `firstMismatch`. `comparisonGranularity` is `"whole-file"` for no-mutate (the only full-file byte-exact assertion, scoped to canonical fixtures per iter-2 item 4) and `"per-cell-rows-slice"` for any mutation.

## Deviations from Plan

### Rule 3 — adapted to the real golden infrastructure
The plan's `<interfaces>` block referenced `GoldenTestRunner.AssertGolden(actualJson, path)` with a `GSD_GOLDEN_UPDATE=1` overwrite mechanism. That API does not exist in this codebase — the real `GoldenTestRunner.Matches(fixtureKey, json)` compares against committed `.expected.json` files. Adapted: the tests assert on the parsed envelope fields directly (robust against the per-run temp path) AND compare a path-masked envelope against the 8 committed golden JSONs via a local `AssertGolden`/`MaskPath` helper + `JToken.DeepEquals` (the established Phase 8 `MaskPath` idiom). Goldens live at the plan-enumerated `Goldens/roundtrip-tab/` path with a `csproj` Content copy.

### Rule 2/3 — added public framework primitives the verb required
The verb lives in `Utinni.Cli` (production project, NO `InternalsVisibleTo`), but the plan-required mutations had no public surface on the 09-01 model: `Rows`/`Columns` are `IList<>` (RemoveAt works) but `MutableDataTableRow.Cells` is read-only and cell removal was `internal`-only, and there was no public string→typed-value coercion. Added (in the framework, where row internals are reachable, so the column list and each row's cell list stay in lock-step):
- `MutableDataTableDocument.RemoveRowAt(int)` / `RemoveColumnAt(int)` (column remove also trims each row's cell) / `ResolveColumnIndex(string)`.
- `MutableDataTableRow.RemoveCellInternal(int)` (internal; called only by `RemoveColumnAt`).
- `MutableDataTableCell.GetOriginalSliceForCompare()` (public read-only slice copy).
- `DataTableColumnType.TryCoerceCellValue(string, out DataTableCellValue)` (runs `MangleValue` then builds the typed value for the column's `BasicType`).
These are reusable by Plan 09-04 (EditCellValue / structural ops) and 09-06 (CSV import), exactly as the plan anticipated the controller layer needing them.

### Rule 1 — updated dispatch help/no-args goldens
Adding the `roundtrip-tab` verb changed the CLI help listing, failing `CommandDispatchTests.Run_With{HelpFlag,NoArgs}_*`. Updated `Fixtures/dispatch/{help,no-args}.expected.txt` to include the new verb block (alphabetically between `roundtrip-iff` and `validate-plugin`) — a direct, expected consequence of the Program.cs wire, not a defect.

## Golden-update ergonomics note (Windows PowerShell)
The repo's golden mechanism is committed `.expected.{json,txt}` + `JToken.DeepEquals`/exact-text compare, with mismatches auto-dumped to `bin/.../TestResults/<key>/actual.{json,txt}`. There is NO `GSD_GOLDEN_UPDATE` env-var path; goldens are authored by hand (or copied from the dumped `actual.*`). On Windows the dumper CRLF-normalizes to LF, and `git add` warns "LF will be replaced by CRLF" — benign (the test layer normalizes both sides before comparing).

## Verification

- `dotnet test Utinni.Cli.Tests --filter "FullyQualifiedName~RoundtripTab"` → **16/16 pass**.
- Full `Utinni.Cli.Tests` → **139 pass / 1 skip / 0 fail**; `UtinniCoreDotNet.Tests` → **404/404** (no regression).
- `dotnet build Utinni.Cli` Debug + Release → **0 errors**.
- `grep -c "per-cell-rows-slice" RoundtripTabCommand.cs` → 2; `grep -c RoundtripTabOptions Program.cs` → 2; 8 golden files present.
- Verification gate 3 (`grep "roundtrip-tab" Program.cs`) returns 0 by construction: `Program.cs` wires the verb via the PascalCase identifiers `RoundtripTabOptions`/`RoundtripTabCommand`, not the hyphenated verb string (the literal lives only in the `[Verb]` attribute in `RoundtripTabCommand.cs`, 12 hits). The functional intent — verb discoverable + dispatched — is proven by the passing dispatch goldens and the 16 facts.

## Known Stubs
None. The verb is fully wired and exercised end-to-end by the golden suite.

## Self-Check: PASSED
- Created files exist: `Utinni.Cli/Commands/RoundtripTabCommand.cs`, `Utinni.Cli.Tests/Infrastructure/DataTableFixtureBuilder.cs`, `Utinni.Cli.Tests/Commands/RoundtripTabCommandTests.cs`, 8 goldens under `Utinni.Cli.Tests/Goldens/roundtrip-tab/`.
- Commits exist: `375927f` (Task 1), `251a495` (Task 2).
