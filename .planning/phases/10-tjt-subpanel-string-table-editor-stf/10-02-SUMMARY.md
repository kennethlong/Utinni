---
phase: 10-tjt-subpanel-string-table-editor-stf
plan: 02
subsystem: cli-harness
tags: [stf, roundtrip-stf, golden, sc4, byte-exact, cf-02]
requires: ["10-01"]
provides:
  - "utinni-cli roundtrip-stf verb (CF-02 SC4 byte-exact CI gate)"
  - "Utinni.Cli.Tests StringTableFixtureBuilder (CLI-side .stf fixtures) + 10 committed goldens"
  - "MutableStringTableEntry.GetOriginalStringBytesForCompare() public accessor (additive)"
affects:
  - "10-03/10-04/10-05 (downstream editor work is gated by this SC4 correctness proof on every push)"
tech-stack:
  added: []
  patterns:
    - "roundtrip-tab verb + golden suite pattern ported to the flat .stf format"
    - "F6 golden-contract split (whole-file / canonical-normalized / per-entry-slice)"
    - "populate-to-source golden helper (GSD_GOLDEN_UPDATE=1 or first-run writes the source golden)"
key-files:
  created:
    - Utinni.Cli/Commands/RoundtripStfCommand.cs
    - Utinni.Cli.Tests/Commands/RoundtripStfCommandTests.cs
    - Utinni.Cli.Tests/Infrastructure/StringTableFixtureBuilder.cs
    - "Utinni.Cli.Tests/Goldens/roundtrip-stf/ (10 goldens)"
  modified:
    - Utinni.Cli/Program.cs
    - UtinniCoreDotNet/Formats/StringTable/MutableStringTableEntry.cs
key-decisions:
  - "F2c real-extracted .stf golden is BLOCKED in this environment and shipped as [Fact(Skip=...)] with a documented reason: committing a real copyrighted .stf violates the repo CON-O-09 no-copyrighted-fixtures posture, and no TRE archive is available here to extract one. The plan permits the 'document the blocker' branch. A6 (real-world canonical ordering) is confirmed at the 10-06 maintainer smoke (the maintainer has live TRE access). The synthetic builder + drift detector fully cover the round-trip mechanics."
  - "Per-entry-slice comparison reads MutableStringTableEntry.GetOriginalStringBytesForCompare() (new public accessor mirroring Phase 9's GetOriginalSliceForCompare) — additive + binary-compat-safe, so the CLI compares captured original slices (malformed UTF-16 included) without internal access."
  - "CanonicalReemit() defeats the F2a short-circuit (SetOriginalFileBytes(null)) to force a genuine canonical re-serialize for the non-canonical comparison baseline."
requirements-completed: [PROD-W1-STF]
duration: ~1 session
completed: 2026-05-30
---

# Phase 10 Plan 02: roundtrip-stf CLI Golden Gate Summary

Ships the CF-02 automated SC4 correctness gate — `utinni-cli roundtrip-stf` — mirroring the Phase 8
`roundtrip-iff` / Phase 9 `roundtrip-tab` verbs, consuming 10-01's `StringTableDocument.FromBytes` +
`StringTableWriter.Serialize`. CI now fails on every push if a `.stf` does not round-trip byte-exact.

## What shipped

- **`RoundtripStfCommand`** — `[Verb("roundtrip-stf")]` with `<path>` + `--edit-text KEY=VALUE`; exit
  matrix 0 success / 1 UsageError / 2 StringTableParseException|IOError / 3 FileNotFound; sorted-key
  JSON envelope via `JsonOutput`.
- **F6 golden-contract split** reported in `comparisonGranularity`:
  - canonical no-mutate → `whole-file` vs the original bytes (F2a short-circuit makes this hold);
  - non-canonical no-mutate → `canonical-normalized` (output compared to a forced canonical re-emit);
  - `--edit-text` → `per-entry-slice` byte-exact-on-untouched + `sourceCrcPreserved` (D-02b).
- **`StringTableFixtureBuilder`** — CLI-side lock-step duplicate of 10-01's fixtures (BCL-only).
- **10 committed goldens** + the non-optional drift detector; `MutableStringTableEntry.GetOriginalStringBytesForCompare()` public accessor added (additive).

## Verification

- `Utinni.Cli.Tests`: **251 passed, 1 skipped (F2c), 0 failed** (Release, --no-build).
- Named SC4 facts green: "João UTF-16 round-trips byte-exact", "non-BMP surrogate-pair round-trips",
  "byte-exact on untouched + D-02b sourceCrc preserved after --edit-text".
- Goldens verified twice: populate run (GSD_GOLDEN_UPDATE=1) then a clean DeepEquals run.
- Plan grep gates: Program.cs `RoundtripStfOptions`=2, verb=1, sourceCrc≥1 — all pass.
- Framework StringTable tests stay green after the accessor addition.

## Final per-entry-slice comparison algorithm

After `--edit-text`, re-parse BOTH the loaded and the roundtripped bytes fresh; index the roundtripped
entries by id; for every loaded entry whose Name ≠ the edited key, compare
`GetOriginalStringBytesForCompare()` byte-for-byte against its id-matched roundtripped counterpart
(reports `firstMismatch {id, reason}` on the first divergence). The edited entry's `sourceCrc` is
captured before the edit and asserted unchanged after.

## GSD_GOLDEN_UPDATE ergonomics under Windows PowerShell

`AssertGolden` resolves the SOURCE goldens dir as `BaseDirectory/../../../Goldens/roundtrip-stf` (up
out of `bin/Release/net472`) and writes there when `GSD_GOLDEN_UPDATE=1` OR the golden is absent, so a
first run self-populates committable files. Set/clear the env var via `$env:GSD_GOLDEN_UPDATE = "1"` /
`Remove-Item Env:\GSD_GOLDEN_UPDATE` — no ergonomics issues encountered.

## Deviations from Plan

**[Rule 4 → documented-blocker branch] F2c real-extracted .stf golden not committed.** The plan calls
the real-`.stf` golden a "prerequisite, NOT optional" but explicitly permits "document the blocker."
Committing a real copyrighted `.stf` conflicts with the repo's CON-O-09 no-copyrighted-fixtures posture
(cited in this same plan for the synthetic builders), and no TRE archive is available in this
environment to extract one. Shipped as `[Fact(Skip=<reason>)]` and recorded as a 10-06 maintainer
residual rather than reding CI. **Impact:** the A6 real-world-canonical-ordering confirmation moves to
the 10-06 live smoke; synthetic round-trip mechanics are fully covered in CI.

**Total deviations:** 1 (documented blocker, no code impact).

## Self-Check: PASSED
