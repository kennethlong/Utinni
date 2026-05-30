---
phase: 10-tjt-subpanel-string-table-editor-stf
plan: 01
subsystem: framework-formats
tags: [stf, string-table, format-core, writer, edit-controller, byte-exact]
requires: []
provides:
  - "UtinniCoreDotNet.Formats.StringTable.StringTableDocument.FromBytes (flat-binary .stf reader)"
  - "UtinniCoreDotNet.Formats.StringTable.MutableStringTableDocument / MutableStringTableEntry (T4 model + ValidateName + CaptureState/RestoreState)"
  - "UtinniCoreDotNet.Formats.StringTable.StringTableWriter.Serialize (byte-exact, F2a/F2b)"
  - "UtinniCoreDotNet.Editing.StringTableEditController + StringTableEditCommands (EditText/AddEntry/RemoveEntry/RenameKey) + MarkSaved"
affects:
  - "10-02 (roundtrip-stf CLI golden gate consumes this public API)"
  - "10-03 (editor shell consumes the controller + model)"
  - "10-04 / 10-05 (bulk features + save targets compose on the model + controller)"
tech-stack:
  added: []
  patterns:
    - "Phase 9 mutable-model + writer + edit-controller idiom specialized for a flat (non-IFF) format"
    - "CaptureState/RestoreState byte-exact undo primitive (mirrors MutableDataTableCell)"
    - "insert-by-reference undo for RemoveEntry (Phase 8 CR-01)"
key-files:
  created:
    - UtinniCoreDotNet/Formats/StringTable/StringTableParseException.cs
    - UtinniCoreDotNet/Formats/StringTable/StringTableEntry.cs
    - UtinniCoreDotNet/Formats/StringTable/StringTableDocument.cs
    - UtinniCoreDotNet/Formats/StringTable/MutableStringTableEntry.cs
    - UtinniCoreDotNet/Formats/StringTable/MutableStringTableDocument.cs
    - UtinniCoreDotNet/Formats/StringTable/StringTableWriter.cs
    - UtinniCoreDotNet/Editing/IStringTableEditCommand.cs
    - UtinniCoreDotNet/Editing/StringTableEditController.cs
    - UtinniCoreDotNet/Editing/StringTableEditCommands.cs
    - UtinniCoreDotNet.Tests/FormatsTests/StringTable/StringTableFixtures.cs
    - UtinniCoreDotNet.Tests/FormatsTests/StringTable/StringTableDocumentTests.cs
    - UtinniCoreDotNet.Tests/FormatsTests/StringTable/StringTableWriterTests.cs
    - UtinniCoreDotNet.Tests/FormatsTests/StringTable/StringTableEditControllerTests.cs
  modified:
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj
key-decisions:
  - "D-02b sourceCrc-on-edit = PRESERVE on edit, 0 on add. Confirmed safe via F5a: LocalizationManager::getLocalizedStringValue (LocalizationManager.cpp:459) resolves by table + textIndex(id) or text(name) -> getLocalizedString; it NEVER consults sourceCrc, so a preserved-but-stale crc cannot break an edit's render."
  - "validateStringName ported FULL (F3b): non-empty; first char lowercase [a-z]; every char in [a-z0-9_+]; no duplicate. Source: SOE validateStringName = isalpha(name[0]) && find_first_not_of('a-z0-9_+')==npos (valid set is lowercase-only, so first char is effectively [a-z])."
  - "addString auto-name format = id.ToString(\"D3\") + \"_default\" (matches SOE sprintf(\"%03ld_default\", m_nextUniqueId)); nextUniqueId increments after add."
  - "F9 null-name-entry disposition = tolerate-with-warning (not throw): name-less rows load with Name==null and a Warnings entry ('a save will normalize it'); keeps the editable surface name-keyed without dropping the load."
  - "F2a zero-dirty short-circuit + F2b per-entry original-slice re-emit make byte-exactness unconditional for the no-mutate case and preserve malformed UTF-16 verbatim, neutralizing the unverified canonical-ordering assumption for untouched files."
requirements-completed: [PROD-W1-STF]
duration: ~1 session
completed: 2026-05-30
---

# Phase 10 Plan 01: StringTable Format Core Summary

Greenfield framework-side `UtinniCoreDotNet/Formats/StringTable/` namespace delivering the flat-binary
`.stf` reader, mutable model, byte-exact writer, and editor-local undo/redo controller that every
downstream Phase-10 plan composes on. The `.stf` is a flat little-endian custom binary (NOT IFF), so
byte-exactness is engineered explicitly here rather than inherited from the Phase 8/9 IFF hybrid-DOM.

## What shipped

- **`StringTableDocument.FromBytes`** — flat reader: magic `0xABCD` (u32 LE) + version (0/1) +
  nextUniqueId (u32) + count (u32), then `count` string entries (id u32 + sourceCrc u32 + charCount u32
  + UTF-16LE text) and up to `count` name entries (id u32 + length u32 + ASCII name). Forged-count and
  truncation guards (13-byte minimum header); malformed UTF-16 replaced with U+FFFD (never throws,
  STAB-03). Captures the full original whole-file bytes (F2a) and each entry's original string-block
  slice (F2b). Tolerates string-only / partial name blocks with a load warning (F9).
- **`MutableStringTableDocument` / `MutableStringTableEntry`** — T4 model ops (AddEntry auto-id +
  `{NNN}_default` auto-name + sourceCrc 0, RemoveEntryByName, RenameKey), the full `ValidateName`
  ruleset, `CaptureState`/`RestoreState` byte-exact undo primitive, and `RebaselineAfterSave`.
- **`StringTableWriter.Serialize`** — F2a zero-dirty short-circuit (returns original bytes verbatim);
  otherwise canonical re-serialize (strings id-ascending, names name-ascending Ordinal) with untouched
  entries re-emitting their captured slice and dirty/added entries re-encoding UTF-16LE verbatim. NO
  `unfubar` smart-quote rewrite (grep-gated == 0; D-02 / SC4 / STAB-03).
- **`StringTableEditController` + `StringTableEditCommands`** — Apply/Undo/Redo/IsDirty/CanUndo/CanRedo/
  EditApplied + the four T4 verbs; `MarkSaved` rebaselines whole-file + per-entry baselines (F10,
  capturing post-save bytes BEFORE clearing dirty so the short-circuit emits the saved bytes);
  RemoveEntry undo re-inserts the same instance by reference (CR-01). No type-change cascade.

## Verification

- `dotnet test --filter StringTable` (Release|x86, --no-build): **42 / 42 passing** (document parse,
  writer byte-exact incl. João + empty + non-canonical-normalize + malformed-UTF-16 survival + non-BMP
  + sourceCrc-preserve + added-entry nextUniqueId, controller T4 verbs + SC4 undo + name validation +
  MarkSaved rebaseline).
- Full `UtinniCoreDotNet.Tests` suite: **545 / 545 passing** (no regression).
- Release|x86 MSBuild: clean (0 errors; pre-existing generated-code / xUnit-analyzer warnings only).
- Plan grep gates: csproj-doc=1, fromBytes=2, unfubar-call=0, restoreState=4, cascade=0. All pass.

## Deviations from Plan

**[Rule 1 - Bug] FromBytes let DecoderException escape on an empty/truncated buffer.** Found during:
Task 1 test run (`Parse_NullData_Throws` expected `StringTableParseException`, got the low-level
`IffPayloadCursor` `DecoderException`). Fix: added a 13-byte minimum-header guard at the top of
`FromBytes` that throws `StringTableParseException`. Re-verified: 42/42 green. Files modified:
`StringTableDocument.cs`. 

**[Rule 1 - Grep hygiene] Cascade gate matched a comment.** The controller header comment said "no
NeedsReview seam", which the plan's `grep NeedsReview|PendingCascade == 0` gate counted. Reworded to
"no per-cell review seam" (no behavioral change). Gate now 0.

**Total deviations:** 2 auto-fixed (1 correctness bug, 1 grep-hygiene). **Impact:** none on the
delivered API or behavior.

## Notes for downstream plans

- Public API for 10-02/10-03: `StringTableDocument.FromBytes(byte[]) -> { Version, NextUniqueId,
  Mutable, Warnings }`; `StringTableWriter.Serialize(MutableStringTableDocument) -> byte[]`;
  `new StringTableEditController(doc.Mutable)`; `StringTableEditCommands.{EditText,AddEntry,RemoveEntry,
  RenameKey}`.
- 10-02 (CLI `roundtrip-stf`) should reuse `StringTableDocument` + `StringTableWriter` directly and
  assert byte-exact-untouched on synthetic fixtures (a real extracted `.stf` is still the A6
  confirmation per RESEARCH Open Question 2).
- The F5b live stale-crc confirmation remains a 10-06 smoke residual.

## Self-Check: PASSED
