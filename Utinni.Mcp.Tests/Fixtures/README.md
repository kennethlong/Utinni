# Utinni.Mcp.Tests Fixtures — Provenance

These are the MINIMAL real binary assets the net10 `RoundTripTests` copy into a temp
resolved-root and drive the built `Utinni.Mcp.exe` against over real stdio (Plan 14-04,
SC1 / SC2 / SC5).

**Why they are committed (not generated at test time):** the net10 test project CANNOT
reference the net472 fixture builders in `Utinni.Cli.Tests` (cross-TFM; the builders pull
in the x86 `UtinniCoreDotNet`). Per 14-REVIEWS Consensus #8 the bytes are GENERATED ONCE,
committed, and treated as the source of truth thereafter. The generator-of-record is
`.planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/gen-fixtures.ps1`
(run under 32-bit PowerShell so the x86 `UtinniCoreDotNet.dll` loads). Re-run it only if a
builder changes; then re-commit the bytes.

Each committed fixture was validated post-generation by parsing it through the built
`utinni-cli.exe` (parse-tre / decode-iff / compile-definition) — every one round-trips to a
typed envelope, so the committed bytes are known-good, supported, NON-encrypted assets.

| Fixture | Format / version | Produced by | Used by (test) |
|---------|------------------|-------------|----------------|
| `sample.tab` | DTII datatable, V0001, 8 columns (i/f/s/h/b/e/v/p), 1 row | `DataTableFixtureBuilder.BuildV1AllTypes()` (net472) | `DecodeMultiFormat` (datatable), `EditSaveRoundTrip` (cell-edit + read-back), `EditSaveVerifyFail`, `PathEscapeAtBoundary` (copied as the edit target) |
| `sample.iff` | plain EA-IFF-85 `FORM TEST` with one `DATA` leaf | `IffBuilder.Form("TEST", Leaf("DATA", …))` (net472) | generic-IFF surface coverage (`inspect_iff`); NOT a typed-decode target |
| `sample.stf` | string table, v1, 3 entries (alpha/beta/gamma) | `StringTableFixtureBuilder.BuildV1MultiEntry()` (net472) | `DecodeMultiFormat` (stringtable) |
| `sample_ot.iff` | object template `FORM SHOT`, DERV base `object/base.iff`, 3 int params (hitPoints/armor/scale) | direct `MutableIffNode`/`IffWriter` build (mirrors `ApplySaveOtCommandTests.BuildTemplate`) | `DecodeMultiFormat` (objecttemplate) |
| `sample_v0006.tre` | **supported, NON-encrypted size-first `EERT`+`0006` archive** (2 records: one raw-deflate, one stored), NOT the `EERT`+`6000` V6000/encrypted class | size-first `0006` build (replicates `TreFixtureBuilder.BuildSizeFirstArchive("0006", …)`) | `ReadRoundTrip` (`read_tre`), `RepackDryRun` (copied to an ISOLATED temp path; `dry_run=true` no-write then `dry_run=false` real rewrite + backup) |
| `sample.tdf` | minimal text template-definition (int/float/bool/string + `list int`) | authored text (mirrors `Utinni.Cli.Tests/Fixtures/tdf/mintest.tdf`) | `get_template_schema` coverage (compile-definition --skip-native) |

**Non-encrypted v0006 note (Consensus #9):** `sample_v0006.tre` is the size-first SWGEmu
`0006` family (header tag `EERT` + ASCII `0006`), which the TRE reader fully supports and
the repack path can rewrite. It is deliberately NOT the `EERT`+`6000` (V6000) header class,
whose payloads are encrypted / enumerate-only and which `repack-tre` refuses. The repack
real-write test therefore operates only on an ISOLATED COPY of this fixture and asserts the
rewritten archive re-parses to the same record count (tolerating nondeterministic bytes).
