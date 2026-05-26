---
phase: 07-tjt-subpanel-tre-browser-read-only
plan: 04a
type: execute
wave: 4
depends_on: ["07-01", "07-03"]
files_modified:
  - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DataTableDecoder.cs"
  - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/StringTableDecoder.cs"
  - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/ObjectTemplateDecoder.cs"
  - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DecoderException.cs"
  - "D:/Code/Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs"
  - "D:/Code/Utinni/Utinni.Cli/Program.cs"
  - "D:/Code/Utinni/Utinni.Cli.Tests/Commands/DecoderTests.cs"
autonomous: true
requirements: [PROD-01, PROD-W1-TRE]

must_haves:
  truths:
    - "A datatable (FORM DTII) decodes to columns (name + type) and rows of typed cells via the shared IFF reader, not a byte-scan (D-09/D-12)"
    - "A string-table (.stf) decodes to (string id, text) entries (D-09/D-12)"
    - "An object template decodes its inherited-field walk (field, value, inherited-from) (D-09/D-12)"
    - "IFF payload scalars are read little-endian (Pitfall 6) while tags stay big-endian"
    - "Every decoder is pure (no JSON, no console, no file-write) and is exercised by the decode-iff CLI verb with a golden test; the browser structured views (07-04b) call the same decoders (D-08, success criterion #4)"
    - "Forged numCols/numRows over-allocation is blocked by checked arithmetic (division-form count*stride guard) before allocation — a forged count throws DecoderException, not OutOfMemoryException"
    - "The reference split is honored: datatable/STF/object-template from the C++ engine loaders (D-09)"
    - "C++ engine loaders are read-to-port format specs only — layout/algorithm ported to C#, no code/identifiers copied, no runtime dependency (D-02)"
    - "The decoders expose read-only output only — no write/authoring/export surface is added; DEC-A3 stays locked (D-01)"
    - "Each decoder is structured as the read-only foundation Phases 9-11 make editable with no rework (D-13)"
  artifacts:
    - path: "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DataTableDecoder.cs"
      provides: "DTII -> columns + rows decoder over IffDocument"
      contains: "class DataTableDecoder"
    - path: "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/StringTableDecoder.cs"
      provides: "STF -> (id, text) entry decoder"
      contains: "class StringTableDecoder"
    - path: "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/ObjectTemplateDecoder.cs"
      provides: "object-template inherited-field walk decoder"
      contains: "class ObjectTemplateDecoder"
    - path: "D:/Code/Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs"
      provides: "decode-iff CLI verb exercising the decoders for golden tests"
      contains: "decode-iff"
  key_links:
    - from: "Utinni.Cli/Commands/DecodeIffCommand.cs"
      to: "UtinniCoreDotNet.Formats.Decoders.*"
      via: "decode verb dispatch on root FORM tag"
      pattern: "DataTableDecoder|StringTableDecoder|ObjectTemplateDecoder"
    - from: "Utinni.Cli/Program.cs"
      to: "DecodeIffOptions"
      via: "verb registration in ParseArguments + MapResult"
      pattern: "DecodeIffOptions"
---

<objective>
Build the first three per-type structured decoders in the framework (`Formats/Decoders/`) — datatable, string-table, object-template — as pure consumers of the shared `Formats/Iff` parse output, ported from the D-09 C++ engine loaders. Add the `decode-iff` CLI verb so each decoder is golden-tested, and wire the verb dispatch on root FORM tag. This is the first of the two split plans that replace the original oversized 07-04 (codex HIGH: 07-04 too large) — it ships the data/STF/template decoders + CLI lock-step; 07-04b ships the mesh/shader/UI-page summaries + the detail-pane structured views.

Purpose: D-12's type-specific structured decode is the deepest part of Phase 7 and the read-only foundation Phases 9-11 make editable (D-13). Splitting the decoders into a framework-first plan (04a) and a graphics+UI plan (04b) shrinks blast radius so a format mistake in one family does not block the others. Keeping the decoders pure + CLI-golden-tested satisfies success criterion #4 and prevents the CLI/browser drift Pitfall 7 warns about.
Output: three decoder classes + a shared `DecoderException`, the `decode-iff` verb + registration, and golden tests.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-CONTEXT.md
@.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-RESEARCH.md
@.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-PATTERNS.md
@.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-01-SUMMARY.md
@.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-03-SUMMARY.md

<interfaces>
<!-- Shared-core contracts the decoders consume + the CLI verb idiom to copy. -->

Formats/Iff model (consume — do NOT add a new chunk-walker):
  IffDocument { IffChunk Root; IReadOnlyList<IffChunk> AllNodesInPreorder }
  IffContainerChunk : IffChunk { string TypeId; string SubTypeId; IReadOnlyList<IffChunk> Children }
  IffLeafChunk : IffChunk { string TypeId; byte[] Data }   // payload scalars are LITTLE-endian
  IffChunk { long OffsetBytes }   // added in 07-03 (not needed by decoders, but present)
  IffReader.Read(string|Stream) -> IffDocument

Datatable layout (PORT from swg-client-v2 .../sharedUtility/src/shared/DataTable.cpp load_0000/load_0001):
  FORM "DTII" -> FORM "0000"|"0001" ->
    chunk COLS: int32 numCols; numCols null-terminated column-name strings
    chunk TYPE: 0000 -> int32 type-enum per col (Int/Float/String); 0001 -> format string per col
    chunk ROWS: int32 numRows; numRows*numCols cells (decode per column type, LE scalars)

STF / object-template reference split (D-09):
  StringTableDecoder    -> C++ LocalizedStringTableReaderWriter.cpp (iff-tre-codebase-map.md)
  ObjectTemplateDecoder -> C++ ObjectTemplate.cpp inherited-field walk

CLI verb idiom (Utinni.Cli/Commands/ParseTreCommand.cs + InspectIffCommand.cs):
  [Verb("decode-iff", ...)] DecodeIffOptions { [Value(0)] Path }
  static int Run(opts): FileNotFound->exit 3; Decoder/IffParseException->exit 2; IOException->exit 2; generic NOT caught
  JsonOutput.EmitSuccess("decode-iff", BuildResult(...)) — result object NEVER pre-adds schemaVersion/command
  Program.cs: add DecodeIffOptions to ParseArguments<...> list AND MapResult(...) lambda list

Error idiom (Formats/Tre/TreParseException.cs + Formats/Iff/IffParseException.cs):
  kind-enum + message; CLI surfaces ex.Kind.ToString() as error.kind
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: DecoderException + DataTableDecoder + decode-iff verb + golden tests (DTII)</name>
  <files>D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DecoderException.cs, D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DataTableDecoder.cs, D:/Code/Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs, D:/Code/Utinni/Utinni.Cli/Program.cs, D:/Code/Utinni/Utinni.Cli.Tests/Commands/DecoderTests.cs</files>
  <read_first>
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/IffReader.cs (parser-purity rule lines 56-58; the model the decoders consume)
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/IffContainerChunk.cs and IffLeafChunk.cs (SubTypeId/Children/Data; LE-scalar caveat)
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Tre/TreParseException.cs (the kind-enum + sealed-exception idiom to mirror for DecoderException)
    - D:/Code/Utinni/Utinni.Cli/Commands/ParseTreCommand.cs and InspectIffCommand.cs (verb skeleton + BuildResult + exit codes to copy)
    - D:/Code/Utinni/Utinni.Cli/Program.cs (the ParseArguments + MapResult lists to extend)
    - D:/Code/Utinni/Utinni.Cli.Tests/Commands/InspectIffCommandTests.cs (golden-test idiom + fixture usage)
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-RESEARCH.md ("Datatable structured decode" code example; Pitfall 6 endianness; Anti-Patterns "Naive OBJS byte-scan"; Open Q2 fixture acquisition)
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-PATTERNS.md (DataTableDecoder.cs section; parser-purity; error idiom; CLI lock-step)
    - reference (read-to-port, NOT copy): D:/Code/swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTable.cpp
  </read_first>
  <behavior>
    - Test: a small DTII/0001 datatable fixture decodes to the expected column names + types and N rows of typed cells (Int/Float/String), with scalars read little-endian (numCols==1, not 16777216 — Pitfall 6 guard).
    - Test: decode-iff on a datatable emits sorted-key JSON (schemaVersion:1 envelope); FileNotFound -> exit 3; a malformed/truncated decoder input -> exit 2 with error.kind.
    - Test: a DTII with a forged numCols/numRows that would over-allocate throws DecoderException (bounds check via the division-form count*stride guard), not OutOfMemoryException.
  </behavior>
  <action>
    Create `DecoderException.cs` mirroring `TreParseException` (a `public enum DecoderError { UnexpectedForm, Truncated, NegativeCount, CountExceedsCap, UnsupportedVersion }` plus a sealed `DecoderException { DecoderError Kind; }`), carrying the verbatim MIT + provenance header.

    Create `DataTableDecoder.cs` as a `public static` class (parser-purity: NO JSON, NO console, NO file-write — mirror IffReader's docstring rule). Implement `public static DataTable Decode(IffDocument doc)` that dispatches on root `IffContainerChunk.SubTypeId == "DTII"`, descends to the `"0000"|"0001"` form, and reads the `COLS` (int32 numCols + numCols null-terminated names), `TYPE` (per-col enum for 0000 / format string for 0001), and `ROWS` (int32 numRows + numRows*numCols cells) leaf chunks from `IffLeafChunk.Data`, reading all scalars LITTLE-endian (Pitfall 6). Before allocating, bound numCols/numRows with the division-form checked guard (`numCols < 0` -> NegativeCount; `numCols > 0 && numCols > chunk.Data.Length / minCellStride` -> CountExceedsCap; same for numRows against numCols*cellStride) so a forged count cannot over-allocate. Advance a checked cursor within `IffLeafChunk.Data` bounds and throw `DecoderError.Truncated` on a short read — never read past the chunk byte[]. Return a `DataTable { IReadOnlyList<Column> Columns; IReadOnlyList<object[]> Rows }` plain model. Do NOT re-introduce a sentinel byte-scan (route through the IFF parse output only).

    Create the `decode-iff` CLI verb in `DecodeIffCommand.cs` copying the ParseTre/InspectIff skeleton: `[Verb("decode-iff", ...)]`, `Run(DecodeIffOptions o)` with FileNotFound=3 / DecoderException+IffParseException=2 / IOException=2 / generic-not-caught, reading via `IffReader.Read(o.Path)` then dispatching on root SubTypeId to the matching decoder (DTII -> DataTableDecoder in this task; the STF/template branches are added in Task 2) and emitting sorted-key JSON via `JsonOutput.EmitSuccess` (result object never pre-adds schemaVersion/command). Register `DecodeIffOptions` in `Program.cs` (add to both the `ParseArguments<...>` type list and the `.MapResult(...)` lambda list).

    Add `DecoderTests.cs` golden tests for the datatable + the decode-iff verb. Acquire small fixtures: probe for `D:/Code/swg-main/serverdata` loose IFF (Open Q2); if absent, author a tiny in-repo synthesized datatable `.iff` fixture by hand (CON-O-09 in-repo-synth precedent, no LFS) under `Utinni.Cli.Tests/Fixtures/iff/`. The little-endian numCols and the forged-count bounds-check are required cases.
  </action>
  <verify>
    <automated>cd D:/Code/Utinni; dotnet test Utinni.Cli.Tests --filter "Decoder|DecodeIff"</automated>
  </verify>
  <acceptance_criteria>
    - `dotnet test Utinni.Cli.Tests --filter "Decoder|DecodeIff"` passes.
    - `DecoderException.cs` and `DataTableDecoder.cs` exist with the provenance header (grep "original to Utinni under MIT" in each).
    - DataTableDecoder is pure: grep finds NO `Newtonsoft`, NO `Console.`, NO `File.Write`/`StreamWriter` in it.
    - `decode-iff` is registered: grep finds `DecodeIffOptions` in BOTH the `ParseArguments<` list and the `MapResult` list in Program.cs.
    - The datatable test asserts numCols read little-endian (a sentinel like 16777216 does NOT appear — proves LE not BE).
    - A forged numCols/numRows input throws `DecoderException` (asserted), not `OutOfMemoryException`; the division-form `Data.Length / stride` guard appears in DataTableDecoder.cs (grep, non-comment).
    - No `OBJS` sentinel byte-scan introduced (`grep -c OBJS` in Formats/Decoders returns 0).
  </acceptance_criteria>
  <done>The datatable decoder is pure, bounds-checked, little-endian-correct, and golden-tested via decode-iff; the CLI and browser will share one decode path.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: StringTableDecoder + ObjectTemplateDecoder + decode-iff dispatch + goldens</name>
  <files>D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/StringTableDecoder.cs, D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/ObjectTemplateDecoder.cs, D:/Code/Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs, D:/Code/Utinni/Utinni.Cli.Tests/Commands/DecoderTests.cs</files>
  <read_first>
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DataTableDecoder.cs (the sibling C# structure to copy — created in Task 1)
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DecoderException.cs (the kind-enum to reuse)
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/IffContainerChunk.cs and IffLeafChunk.cs (SubTypeId/Children/Data)
    - D:/Code/Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs (the dispatch to extend with STF + template branches — created in Task 1)
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-RESEARCH.md (Pitfall 6 endianness; reference split D-09)
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-PATTERNS.md (StringTableDecoder/ObjectTemplateDecoder section — copy DataTableDecoder structure, port layout from the C++ loaders)
    - reference (read-to-port, NOT copy): the LocalizedStringTableReaderWriter.cpp + ObjectTemplate.cpp loaders per iff-tre-codebase-map.md
  </read_first>
  <behavior>
    - Test: an STF fixture decodes to the expected (string id, text) entries, including a non-ASCII text entry round-tripping correctly (e.g. an accented character), with text decoded via the table's declared encoding.
    - Test: an object-template fixture decodes to its inherited-field walk (field name, value, inherited-from source).
    - Test: decode-iff dispatches STF and object-template root tags to their decoders and emits the schemaVersion:1 envelope; a forged-count STF/template input throws DecoderException, not OOM.
  </behavior>
  <action>
    Create `StringTableDecoder.cs` (sibling structure to DataTableDecoder) returning `StfTable { IReadOnlyList<StfEntry{ uint Id, string Text }> Entries }`, porting the layout from `LocalizedStringTableReaderWriter.cpp`. Decode text as the table's declared encoding so non-ASCII survives (UTF-16/UTF-8 per the format; verify the accented round-trip). Use replacement-on-invalid for malformed encoding sequences (no throw on bad bytes; T-07-16). Bound entry counts with the division-form guard before allocation.

    Create `ObjectTemplateDecoder.cs` (sibling structure) returning `ObjectTemplateView { string BaseTemplate; IReadOnlyList<TemplateField{ string Name, string Value, string InheritedFrom }> Fields }`, porting the inherited-field walk from `ObjectTemplate.cpp`. Bound field counts with the division-form guard.

    Both are `public static` pure decoders (no JSON/console/file-write). Extend the `decode-iff` verb dispatch (in `DecodeIffCommand.cs`) with the STF and object-template root-tag branches. Add `DecoderTests.cs` cases for each (fixtures acquired/synthesized as in Task 1; the non-ASCII STF round-trip and a forged-count case are required).
  </action>
  <verify>
    <automated>cd D:/Code/Utinni; dotnet test Utinni.Cli.Tests --filter "Decoder|DecodeIff"</automated>
  </verify>
  <acceptance_criteria>
    - `dotnet test Utinni.Cli.Tests --filter "Decoder|DecodeIff"` passes including the STF + object-template cases.
    - `StringTableDecoder.cs` and `ObjectTemplateDecoder.cs` exist with their classes + provenance header (grep each + "original to Utinni under MIT"); both pure (no Newtonsoft/Console/File.Write).
    - The STF test asserts a non-ASCII string round-trips byte-for-byte (correct declared encoding, not raw ASCII).
    - decode-iff dispatches DTII, STF, and object-template root tags (grep all three decoder names in DecodeIffCommand.cs).
    - A forged-count STF/template input throws `DecoderException` (asserted), not `OutOfMemoryException`.
  </acceptance_criteria>
  <done>The STF and object-template decoders land, are pure + bounds-checked, decode non-ASCII correctly, and are golden-tested via decode-iff alongside the datatable decoder.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| IFF parse output -> per-type decoders | The decoders read counts (numCols, numRows, entry counts, field counts) from attacker-influenceable IFF payloads; a forged count could drive over-allocation or out-of-bounds reads. |
| decode-iff argv -> decoders | User-supplied path; FileNotFound (exit 3) + exception envelope (exit 2) contract. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-07-13 | Denial of Service | forged numCols/numRows/entry/field counts driving over-allocation | mitigate | Division-form checked guard (`count > Data.Length / stride`) before allocating; reject negative counts (NegativeCount); validate count*stride against the chunk's Data length — never `count * stride` as int. |
| T-07-14 | Tampering | out-of-bounds cell/field reads within a chunk payload | mitigate | Read scalars only within `IffLeafChunk.Data` bounds; advance a checked cursor and throw DecoderError.Truncated on a short read. |
| T-07-15 | Denial of Service | pathological IFF nesting reaching the decoders | mitigate | The shared IffReader already enforces 64 MB cap + NestedChunkOverflow before the decoders run; decoders consume the bounded parse output only. |
| T-07-16 | Tampering | malformed STF encoding / surrogate abuse | mitigate | Decode text via the format's declared encoding with replacement on invalid sequences (no throw on bad bytes); display as data only, never execute/resolve. |
| T-07-SC | Tampering | npm/pip/cargo installs | mitigate | No package installs (BCL + in-repo only; RESEARCH Package Legitimacy Audit). |
</threat_model>

<verification>
- `dotnet test Utinni.Cli.Tests --filter "Decoder|DecodeIff"` is green (datatable + STF + object-template + forged-count cases).
- Decoders are pure (no JSON/console/file-write grep gate) and bounds-checked (forged-count throws DecoderException).
- decode-iff is registered and dispatches all three root tags; the JSON envelope is schemaVersion:1.
</verification>

<success_criteria>
- Datatable, string-table, and object-template decode into pure framework models (D-12, PROD-01 data/STF/template coverage).
- IFF scalars read little-endian (Pitfall 6); non-ASCII STF round-trips.
- Decoders pure + golden-tested via decode-iff; browser + CLI will share one path (D-08, criterion #4).
- Reference split honored (D-09 — C++ engine loaders for data/STF/template).
- The decoders stay read-only — no write/authoring/export surface (D-01); foundation Phases 9-11 make editable (D-13).
</success_criteria>

<output>
Create `.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-04a-SUMMARY.md` when done.
</output>
