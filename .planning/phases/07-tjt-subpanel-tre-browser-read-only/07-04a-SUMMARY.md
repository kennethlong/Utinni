---
phase: 07-tjt-subpanel-tre-browser-read-only
plan: 04a
subsystem: formats
tags: [decoders, datatable, stringtable, objecttemplate, decode-iff, cli, iff, stf, little-endian]

requires:
  - phase: 07-tjt-subpanel-tre-browser-read-only
    provides: "07-01 shared Formats/Iff reader (IffReader.Read(Stream)) + IffContainerChunk/IffLeafChunk model"
  - phase: 07-tjt-subpanel-tre-browser-read-only
    provides: "07-03 IffChunk.OffsetBytes (present on the model; not needed by decoders)"
provides:
  - "Formats/Decoders/DecoderException (DecoderError kind-enum: UnexpectedForm/Truncated/NegativeCount/CountExceedsCap/UnsupportedVersion)"
  - "Formats/Decoders/IffPayloadCursor: shared bounds-checked little-endian reader (int32/uint32/float/bytes/NUL-string) over a byte buffer (Pitfall 6)"
  - "DataTableDecoder: FORM DTII -> FORM 0000/0001 -> COLS/TYPE/ROWS into columns + typed rows (Int/Float/String), forged-count guarded"
  - "StringTableDecoder: the RAW (non-IFF) .stf magic+version binary -> (id, name, text) entries; UTF-16LE text, non-ASCII round-trips"
  - "ObjectTemplateDecoder: BOUNDED posture — root type + declared DERV base name + LOCAL param fields (name + raw-hex value, InheritedFrom), no cross-IFF resolution"
  - "decode-iff CLI verb: .stf-magic sniff -> StringTableDecoder; IFF root -> DataTable (DTII) / ObjectTemplate (shape-detected) / UnsupportedForm; schemaVersion:1 envelope"
affects: [07-04b, 08, 09, 10, 11]

tech-stack:
  added: []
  patterns:
    - "IFF chunk PAYLOAD scalars are little-endian (Pitfall 6) while chunk tags/lengths are big-endian — the shared IffPayloadCursor reads payloads LE; the IffReader reads tags/lengths BE"
    - "Forged counts (numCols/numRows/STF entry/template param) are rejected BEFORE allocation by a division-form guard (count > bytes / stride) -> DecoderException, never OutOfMemoryException"
    - "The .stf string table is NOT an IFF container — it is a raw little-endian magic(0xABCD)+version binary; sniff the magic and decode raw bytes, do not route it through the IFF parser"
    - "Object-template param VALUES are type-specific (per generated template loadFromIff) and are NOT generically typeable — the bounded decoder shows raw value bytes as hex; it reads only the declared base name + local param names, never following the base into another IFF"
    - "Synthesize IFF/STF test fixtures in-code via a labeled MINIMAL-CONTRACT builder + temp files; gate real loose-asset confidence behind SWG_LOOSE_IFF_DIR (skips cleanly in CI)"

key-files:
  created:
    - "Utinni/UtinniCoreDotNet/Formats/Decoders/DecoderException.cs"
    - "Utinni/UtinniCoreDotNet/Formats/Decoders/IffPayloadCursor.cs"
    - "Utinni/UtinniCoreDotNet/Formats/Decoders/DataTableDecoder.cs"
    - "Utinni/UtinniCoreDotNet/Formats/Decoders/StringTableDecoder.cs"
    - "Utinni/UtinniCoreDotNet/Formats/Decoders/ObjectTemplateDecoder.cs"
    - "Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs"
    - "Utinni/Utinni.Cli.Tests/Infrastructure/IffBuilder.cs"
    - "Utinni/Utinni.Cli.Tests/Commands/DecoderTests.cs"
  modified:
    - "Utinni/UtinniCoreDotNet/UtinniCoreDotNet.csproj (5 explicit <Compile Include> — old-style csproj)"
    - "Utinni/Utinni.Cli/Program.cs (DecodeIffOptions in ParseArguments + MapResult)"
    - "Utinni/Utinni.Cli.Tests/Infrastructure/FixturePath.cs (LooseIffDir/HasLooseIffDir gate)"
    - "Utinni/Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt + no-args.expected.txt (verb-list bump)"

key-decisions:
  - "StringTableDecoder consumes raw byte[], NOT IffDocument — the .stf is a raw magic+version binary (LocalizedStringTable*.cpp), not IFF. decode-iff sniffs the 0xABCD magic before the IFF parser. Reverses the plan's 'dispatch STF on root SubTypeId' assumption."
  - "ObjectTemplateDecoder renders param VALUES as raw hex (type-specific values are not generically decodable) and reads only the declared base name + LOCAL param names — bounded read-only posture (review consensus #3); never opens another document, never touches the shared TRE facade, never recurses; within-file parent-class forms are not expanded."
  - "decode-iff golden tests assert on the PARSED JSON envelope structure (schemaVersion:1 + decoded fields) over in-code IffBuilder fixtures written to temp files, rather than committing opaque binary .iff fixtures + byte-exact .expected.json goldens. Deterministic, reviewer-readable, and avoids golden regen churn."

patterns-established:
  - "IffPayloadCursor is the shared LE/bounds-checked payload reader all current and future per-type decoders use; Phase 8-11 editable surfaces build on the same decoder models."

requirements-completed: [PROD-01, PROD-W1-TRE]

duration: ~3h
completed: 2026-05-27
---

# Phase 7 Plan 04a: DataTable / StringTable / ObjectTemplate Decoders + decode-iff Summary

**Three pure, bounds-checked, read-only per-type decoders in `Formats/Decoders/` — datatable (DTII), string-table (.stf), and object-template (bounded: declared base + local fields) — plus the `decode-iff` CLI verb that exercises them on the same `IffReader` path the browser will use (07-04b). 18 tests green; the full `Utinni.Cli.Tests` suite is 101 passed / 1 skipped. Real-asset STF decode is verified against `swg-main/serverdata`; a real-datatable parse exposed a pre-existing IffReader word-pad limitation flagged below.**

## Verification (automated — no human checkpoint in 07-04a)
- `dotnet test Utinni.Cli.Tests --filter Decoder|DecodeIff`: **18 green** (10 decoder unit + 6 decode-iff CLI + 1 STF bad-magic + 1 env-gated real-STF supplemental).
- Full `Utinni.Cli.Tests` suite: **101 passed / 1 skipped** (the pre-existing SearchTOC fixture-gated skip). inspect-iff lane unaffected.
- Both repos build Release/x86 (Utinni only this plan; UtinniCoreDotNet.dll + utinni-cli.exe).
- **Real-asset confidence (SWG_LOOSE_IFF_DIR → `D:/Code/swg-main/serverdata`):** the env-gated supplemental decoded a real `.stf` (`armor_rehue.stf` → id 1 / name "equipped" / real localized text) — PASS.

## Task Commits (Utinni-only)
1. **DataTableDecoder + decode-iff (DTII)** (Task 1) — `8b6222e` (feat) — DecoderException, shared IffPayloadCursor, DataTableDecoder, decode-iff verb + Program.cs registration, 9 tests.
2. **StringTable + ObjectTemplate decoders + dispatch** (Task 2) — `6311d57` (feat) — StringTableDecoder (raw .stf), ObjectTemplateDecoder (bounded), decode-iff STF-sniff + template dispatch, dispatch-golden bump, 8 more tests.
3. **Env-gated real-.stf supplemental** — `d98f81d` (test) — FixturePath.LooseIffDir + the real-layout STF test.

## Deviations from Plan
1. **[Rule 1 — format reality] The .stf is NOT IFF.** The plan's interface said "dispatch STF on root SubTypeId" (assuming an IFF FORM). Reality (per `LocalizedStringTable*.cpp`): a `.stf` is a raw little-endian `magic(0xABCD) + version(byte) + nextUniqueId + count` header, then `count` string entries (id + crc + charCount + UTF-16LE text) + a name→id table. So `StringTableDecoder.Decode` takes `byte[]`, and `decode-iff` sniffs the leading magic before handing anything to `IffReader`. Text is UTF-16LE (non-ASCII round-trips; malformed units replaced, T-07-16).
2. **[Rule 1 — bounded posture made concrete] Object-template param VALUES are type-specific → shown as raw hex.** Each param's value is read by a generated template-class `loadFromIff` (a 1-byte data-type marker + type-specific bytes); there is no generic value typing. The bounded decoder honestly extracts root type + DERV base name + LOCAL param **names** with their raw value bytes rendered as hex, `InheritedFrom="local"` (+ an `@base` row whose `InheritedFrom` is the base name). It never opens another IFF/TRE entry, never touches the shared TRE facade, never recurses; within-file parent-class forms are not expanded. This is the bounded read-only Phase-7 surface (review consensus #3 / Codex).
3. **[Rule 3 — cleaner test strategy] In-code MINIMAL-CONTRACT fixtures, not committed binary `.iff` + `.expected.json` goldens.** Added a labeled `IffBuilder` test helper that synthesizes IFF/STF byte streams deterministically; decoder unit tests parse in-memory, decode-iff CLI tests write to a temp file and assert on the parsed JSON envelope structure. Reviewer-readable and avoids opaque binaries / golden-regen churn. (Two supporting files added: `IffBuilder.cs`; `IffPayloadCursor.cs` — a shared LE/bounds-checked cursor reused by all three decoders.)
4. **[expected] Dispatch goldens bumped.** Adding the `decode-iff` verb changed CommandLineParser's auto-generated `--help` / no-args verb list; `help.expected.txt` + `no-args.expected.txt` were regenerated from the masked actual output.
5. **[Rule 1] Real-asset supplemental: STF added (env-gated, verified); datatable NOT.** `swg-main/serverdata` is present; the real STF supplemental decodes it cleanly. A real datatable could not be added as a passing supplemental — see Issues.

## Issues Encountered
**Real SWG datatables are NOT word-padded, and the 07-01 `IffReader`'s STRICT EA-IFF-85 pad handling rejects them. (Pre-existing IffReader limitation, out of 07-04a decoder scope; a REQUIRED 07-04b precursor.)**

- Evidence: `serverdata/datatables/appearance/alternate_lightsaber_shaders.iff` has `COLS` length 33 (odd) immediately followed by `TYPE` at offset 65 — **no pad byte**. The header layout is exactly what `DataTableDecoder` expects (`FORM DTII → FORM 0001 → COLS/TYPE/ROWS`), but `IffReader` (07-01, REVIEWS MEDIUM-11 STRICT pad) consumes a phantom pad byte for the odd `COLS` chunk, misaligns by one, and throws `MalformedFourCc` at offset 66. So `decode-iff` (and `inspect-iff`) cannot parse real un-padded datatables today.
- Scope call: the `DataTableDecoder` logic is correct (verified on synthesized padded fixtures + the real file's matching header). The defect is in `IffReader`'s pad strictness vs SWG's no-pad reality — 07-01/Phase-4 territory whose change touches the locked `inspect-iff` `malformed-missing-padbyte` golden and possibly `ws.iff` (list-objects, which may pad). Fixing it inside 07-04a would balloon scope into another plan's contract under a hasty change, so it is **flagged, not fixed here.**
- Recommended fix (07-04b precursor): make `IffReader` tolerant of an absent pad byte after an odd-length chunk (peek whether the next 4 bytes are a valid printable TypeID before consuming a pad), re-validating the `malformed-missing-padbyte` + `ws.iff` paths. STF and the synthesized/padded paths are unaffected.

## User Setup Required
None. (Optional: set `SWG_LOOSE_IFF_DIR` to a serverdata-style tree to exercise the real-`.stf` supplemental locally; CI leaves it unset and the test skips cleanly.)

## Next Phase Readiness
- **07-04b** renders these decoders' output as structured views in `TreDetailPane.pnlStructured` (the placeholder 07-03 stubbed) + adds the mesh/shader/UI-page summaries. **Precursor:** the `IffReader` pad-tolerance fix must land (or 07-04b must scope it) before real-client datatables render — without it the datatable structured view fails on real assets. STF real-layout confidence is established; object-template real-asset confidence is not yet exercised (client templates live in the 5000 `.tre`, not probed this plan).
- The decoder models (`DataTableView`, `StfTable`, `ObjectTemplateView`) are the read-only foundation Phases 9–11 make editable with no rework (D-13). No blockers for the decoder layer itself.

---
*Phase: 07-tjt-subpanel-tre-browser-read-only*
*Completed: 2026-05-27*
