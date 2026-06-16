---
phase: 20-terrain-trn-codec-verbs-mcp
plan: 03
subsystem: terrain-trn-codec
tags: [terrain, trn, tgen, encoder, apply-save, decode-trn, roundtrip-trn, verbs, byte-exact, wave-2]
requires:
  - "TgenFieldLayouts single-source descriptor table + TgenDecoder + TerrainDocument/Layer/Node/Palettes (Plan 02 — consumed, not re-created)"
  - "TgenFixtureSynthesizer + TgenEraVersions.InfinityEra both-lineage fixture matrix (Plan 01)"
  - "MutableIffNode (GetPayloadCopy/SetPayload/Parent) + IffWriter verbatim re-emit + LooseOverridePath + SaveCommandIo (the apply-save-* atomics)"
provides:
  - "TrnFieldEncoder: re-encode ONE field inside a packed DATA payload via the SAME TgenFieldLayouts descriptor (exact byte-span replacement, untouched bytes verbatim, byte-exact); float bit policy (NaN/Inf rejected)"
  - "decode-iff TGEN auto-route + decode-trn alias: NAVIGABLE terrain envelope (layer tree + name/active/children + palette family names + typed/raw/dead kind + stableId + editable)"
  - "roundtrip-trn: whole-file byte-identity assertion over the full low+high matrix"
  - "apply-save-trn: field-aware contained atomic save — ResolveFieldContext parent-walk recovers (tag,version); --field active mutates the IHDR int32; exact-span + untouched-leaf verify before write"
affects:
  - "Plan 04 (MCP/Terrain): TerrainReadTool dispatches through the SAME decode-iff TGEN route / TerrainDocument (MCP-for-free, #9); AFCN v0000 still-ASSUMED resolution lands there"
  - "REQUIREMENTS PROD-W2-TRN-03 (edit+save byte-exact) + the verb half of PROD-W2-TRN-04 are met"
tech-stack:
  added: []
  patterns:
    - "Exact-byte-span single-field re-encode on the shared decoder descriptor (no offset literals; no whole-payload reserialization)"
    - "Parent-chain (tag,version) recovery (ResolveFieldContext) — the leaf's own node does NOT carry them"
    - "Pinned active-flag DOM location (IHDR int32 @ offset 0) = the read↔write parity leaf the decoder reads from"
    - "decode-iff root-FORM auto-route mirroring the PEFT branch (one decoder, CLI + MCP never drift)"
key-files:
  created:
    - "UtinniCoreDotNet/Formats/Terrain/TrnFieldEncoder.cs"
    - "Utinni.Cli/Commands/DecodeTrnCommand.cs"
    - "Utinni.Cli/Commands/RoundtripTrnCommand.cs"
    - "Utinni.Cli/Commands/ApplySaveTrnCommand.cs"
    - "Utinni.Cli.Tests/Terrain/TrnVerbGoldenTests.cs"
    - ".planning/phases/20-terrain-trn-codec-verbs-mcp/deferred-items.md"
  modified:
    - "Utinni.Cli/Commands/DecodeIffCommand.cs (TGEN branch + BuildTerrainResult navigable envelope + TerrainParseException catch)"
    - "Utinni.Cli/Program.cs (decode-trn + roundtrip-trn + apply-save-trn registered in Type[] + Dispatch)"
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj (TrnFieldEncoder.cs Compile Include — explicit-include csproj)"
    - "Utinni.Cli.Tests/Terrain/RoundtripTrnTests.cs (un-Skipped + implemented: full-matrix byte identity + exact-span + float-bit + NaN/Inf)"
    - "Utinni.Cli.Tests/Terrain/TgenFieldLayoutTests.cs (offset-parity: encoder edit span == decoder descriptor span)"
    - "Utinni.Cli.Tests/Terrain/ApplySaveTrnTests.cs (un-Skipped + implemented: exact-span / active re-decode parity / negative battery)"
    - "Utinni.Cli.Tests/Fixtures/trn/TgenFixtureSynthesizer.cs (WithLayerHeader — IHDR-bearing active-flag parity fixture)"
    - "Utinni.Cli.Tests/Fixtures/dispatch/{help,no-args}.expected.txt (refreshed for the 3 new verbs)"
decisions:
  - "D-05..D-09 honored: fixed-length edits only; variable-length/name rejected (D-06); contained atomic write (D-07); surface = decode-iff TGEN + decode-trn + roundtrip-trn + apply-save-trn (D-08); field-aware DeriveStableId addressing + ResolveFieldContext parent-walk (D-09)"
  - "Active-flag location (#10): --field active writes the int32 @ offset 0 of the IHDR DATA leaf under LAYR — bypasses TgenFieldLayouts (IHDR has no descriptor) and writes directly to the SAME leaf the decoder reads Active from; resolved by asserting the leaf's container parent is an IHDR FORM"
  - "--leaf addresses the DATA leaf directly (the editable payload); ResolveFieldContext walks leaf.Parent (version FORM) -> grandparent (tag FORM) for typed nodes"
  - "TrnFieldEncoder lands in the Task-1 commit; apply-save-trn (Task 3) re-locates the descriptor span itself (for the exact-span verify) AND delegates the encode to TrnFieldEncoder — one source of offsets"
metrics:
  duration: "~35m wall (2026-06-16 ~13:03 -> 13:38 UTC)"
  completed: "2026-06-16"
  tasks_completed: 3
  files_created: 6
  files_modified: 9
---

# Phase 20 Plan 03: TGEN Edit Path + CLI Verb Surface (byte-exact) Summary

Built the second genuinely-new piece of the terrain codec — the single-field re-encoder — plus the read +
roundtrip + edit verb surface, all with the byte-exact correctness the reviewers demanded. `TrnFieldEncoder`
consumes the SAME single-source `TgenFieldLayouts` table the Plan-02 decoder reads (offset-parity tested,
#2) and overwrites ONLY the target field's exact byte span while copying every other byte verbatim
(untouched floats keep exact bits; NaN/Infinity rejected, #6). `apply-save-trn` recovers (tag, version) by
walking the parent chain (`ResolveFieldContext`, #10), pins the active-flag DOM location to the IHDR int32
the decoder reads `Active` from (read↔write parity, proven by re-decode), and verifies exact-span +
untouched-leaf byte-identity before an atomic contained write. `decode-iff` auto-routes a TGEN/PTAT root to
a NAVIGABLE envelope (#9); `decode-trn` is its alias; `roundtrip-trn` asserts whole-file byte identity over
the full low+high matrix. TRN-03 and the verb half of TRN-04 are met.

## What Was Built

### Task 1 — `TrnFieldEncoder` (exact-byte-span single-field re-encode) (commit `2056b62`)

- **`TrnFieldEncoder.EncodeField(payload, tag, version, fieldName, value)`** looks up
  `TgenFieldLayouts.For(tag, version)` (the SINGLE source the decoder reads — NO offset literals, #2),
  finds the named descriptor, validates `Editable`, parses `--value` per the descriptor's `EditParser`, and
  writes ONLY `[offset .. offset+width)` into a COPY of the input. Every other byte is copied verbatim — the
  encoder NEVER reserializes from a typed model, so untouched fields (incl. floats) keep exact bits (#6).
  Floats accept invariant-culture decimal and REJECT `NaN`/`Infinity`; unknown field name / unknown
  (tag,version) / non-editable / variable-length (D-06) all throw `ArgumentException` (no silent no-op).
- **Offset-parity test** (`TgenFieldLayoutTests`, filter `TgenLayout`): for every (tag,version) and every
  editable field, encoding a value changes ONLY the descriptor's `[offset..offset+width)` span — proving the
  encoder edit span == the decoder descriptor span (single-source parity, #2).
- **`RoundtripTrnTests`** (filter `RoundtripTrn`): whole-file `FromBytes → Serialize → SequenceEqual` byte
  identity across the FULL Plan-01 matrix (minimal / every Tier-1 low+high / unknown / dead / truncated /
  compositional) — the DEC-C3 codec-level gate — PLUS exact-span (`height` edit → only [4..8) differ),
  untouched-float-bits, and NaN/Inf-rejection assertions.

### Task 2 — decode-iff TGEN branch + decode-trn + roundtrip-trn + CLI goldens (commit `b3cb376`)

- **`DecodeIffCommand`** gained a TGEN branch immediately after PEFT: `TgenDecoder.LooksLikeTerrain(root)` →
  `TerrainDocument.FromIff` → `BuildTerrainResult`. `BuildTerrainResult` emits a NAVIGABLE envelope (#9):
  `type:terrain`, `rootType:TGEN`, a `palettes` array (slot role / present / ambiguous / family names), and a
  `layers` array recursively exposing each layer's `name` / `active` / `children` (each child `tag` /
  `version` / `kind ∈ {typed,raw,dead}` / `stableId` / `editable`, typed children carry named `fields`, raw
  children carry `hex`) + convenience counts. `TerrainParseException` → exit 2 added.
- **`DecodeTrnCommand`** (`decode-trn`): thin discoverability ALIAS delegating to the SAME `BuildTerrainResult`
  (an alias, not a fork — Claude's Discretion).
- **`RoundtripTrnCommand`** (`roundtrip-trn`): `FromBytes → Serialize → re-parse → SequenceEqual` whole-file;
  emits `bytesIdentical` / `comparisonGranularity:whole-file` / `rootType:TGEN`; exit 0/1/2/3.
- Registered all three (Task 2 added two, Task 3 the third) in `Program.cs` Type[] + Dispatch.
- **`TrnVerbGoldenTests`** (filter `TrnVerbGolden`, 11 tests): invokes the verbs through
  `InProcessCliRunner` (the shipped entry) and asserts the envelope schema/shape (navigable tree present,
  sorted keys, `type:terrain`/`rootType:TGEN`), exit-code mapping (0 / FileNotFound 3 / malformed 2), and
  that `--help` enumerates the new verbs (#5/#18).

### Task 3 — `apply-save-trn` (ResolveFieldContext + exact-span verify) (commit `f70d591`)

- **`ApplySaveTrnCommand`** (`apply-save-trn --root --leaf --field --value`): `.tre`-magic reject →
  `LooseOverridePath.Resolve` (escape → exit 2, no write) → read + `MutableIffDocument` → `TerrainDocument`
  decode to gate on `TerrainNode.IsEditable` (raw/truncated/DEAD rejected, no write, #4) →
  `FindMutableLeafByStableId` → `ResolveFieldContext` (walks `leaf.Parent` to recover (tag,version) from the
  version FORM + tag FORM — NOT the leaf's own node, #10) → `GetPayloadCopy` → encode ONE field → `SetPayload`
  → `IffWriter.Write` → re-parse → verify EXACT target span differs ONLY (#2) AND every untouched leaf
  byte-identical → atomic `WriteAtomic` only on a clean verify.
- **Active-flag (#10, ONE decision block):** `--field active` requires `--leaf` to address the IHDR DATA leaf
  (asserted: the leaf's container parent SubTypeId == "IHDR") and writes the int32 at offset 0 — the SAME leaf
  the Plan-02 decoder reads `TerrainLayer.Active` from. (IHDR has no `TgenFieldLayouts` entry, so active is
  written directly; all typed fields route through `TrnFieldEncoder`.)
- **`TgenFixtureSynthesizer.WithLayerHeader`** added — an IHDR-bearing layer fixture so the parity test has a
  real on-disk active flag to toggle + re-read.
- **`ApplySaveTrnTests`** (filter `ApplySaveTrn`, 9 tests): single-scalar exact-span + untouched-byte-identity,
  AHCN operation enum edit, exact-byte-span whole-file diff, `ApplySave_ToggleActive_RoundtripDecodeReflectsEdit`
  (toggle active → save → re-decode via `TerrainDocument.FromBytes` → assert `Layers[0].Active == edited` AND
  name preserved — read↔write parity end-to-end, #10), and the negative battery (non-editable node rejected
  no-write #4, `--root` escape exit 2 no-write, `.tre` reject, bad-leaf reject, failed-verify exit 2 byte-unchanged).

## Reviewer concerns closed

- **#2** single-source descriptor + exact-byte-span: encoder consumes `TgenFieldLayouts`; offset-parity test
  + exact-span assertions + apply-save verify all green.
- **#6** float bit policy: untouched floats keep exact bits (verbatim copy, no parse/format); NaN/Infinity rejected.
- **#9** navigable envelope: layer tree + palettes + typed fields, not summary-only.
- **#10** ResolveFieldContext parent-walk + pinned active-flag location + read↔write parity proven by re-decode.
- **#5/#18** verb goldens: envelope schema + exit-code mapping for the TGEN verbs.

## Deviations from Plan

### Auto-fixed / structural (no architectural change)

**1. [Rule 3 — Build wiring] `TrnFieldEncoder.cs` added to `UtinniCoreDotNet.csproj` Compile Include**
- `UtinniCoreDotNet` is an explicit-include csproj (no SDK globbing) — the encoder would not compile into the
  DLL otherwise. Added one `<Compile Include>` entry. **Commit:** `2056b62`.

**2. [Rule 2 — Verify hardening] apply-save-trn re-derives the descriptor span itself for the exact-span verify**
- The plan's verify ("output differs ONLY in the target field's exact byte span") needs the span at the
  command layer. `ApplySaveTrnCommand` looks up the SAME `TgenFieldLayouts` descriptor to get
  `(offset,width)` for the verify, then delegates the actual encode to `TrnFieldEncoder` — still one source of
  offsets, no duplication. **Commit:** `f70d591`.

### Out-of-scope (deferred — NOT fixed)

**3. Pre-existing `AbiSurfaceTests` ABI-drift failure (Phase-17 regen-churn artifact)**
- `UtinniCoreDotNet.Tests.AbiSurfaceTests.GeneratedSurface_MatchesBlessedBaseline_IgnoringReorderChurn` fails
  with **ADDED(0) / REMOVED(20)** vs the blessed baseline. **ADDED=0 proves Plan 20-03 added ZERO native ABI
  surface** — the delta is pure CppSharp regen/reorder churn between this machine's `Generated/UtinniCore.cs`
  and the Phase-17-frozen fixture (the known Phase-17 gotcha: the ABI gate needs `UtinniCoreDotNetGen.exe` to
  RUN + a re-bless; the mandated `git checkout -- Generated/UtinniCore.cs` leaves a divergent file). Plan
  20-03 touches only managed `Formats/Terrain/*` + CLI verbs — zero native/Generated/ABI-baseline files.
  Re-blessing is a maintainer-checkpoint operation (TJT rebuild + fixture re-freeze), out of Wave-2 terrain
  scope. Logged to `deferred-items.md`. NOT fixed (SCOPE BOUNDARY).

No architectural deviations. No new packages (threat T-20-SC: accept — pure managed, zero installs).

## Authentication Gates

None.

## Known Stubs

None introduced by this plan. The Plan-04 Mcp `TerrainReadTool` stubs remain Skip-marked (their wave). The
`AFCN` typed layout still uses the ASSUMED v0000 (`TgenEraVersions`) — documented, Plan 04 Task 3 resolves it;
not a blocking stub (AFCN raw-falls-back cleanly if the assumption is wrong).

## Verification

- **MSBuild** `Utinni.sln /p:Configuration=Release /p:Platform=x86` → built with no errors (PowerShell-invoked).
- `Generated/UtinniCore.cs` reverted via `git checkout --` after each build (NOT committed — confirmed absent
  from all three commits `2056b62` / `b3cb376` / `f70d591`).
- `dotnet test Utinni.Cli.Tests --no-build -c Release --filter "Category=RoundtripTrn|Category=TgenLayout|Category=TrnVerbGolden|Category=ApplySaveTrn"`
  → **105 passed, 0 failed**.
- Full `Utinni.Cli.Tests` lane → **401 passed, 2 skipped, 0 failed** (the 2 skips are pre-existing
  real-asset-dependent tests: CotMasterIndex + RealExtractedStf — unrelated).
- `Utinni.Mcp.Tests` → **102 passed, 4 skipped** (the 4 skips are the Plan-04 terrain stubs).
- `UtinniCoreDotNet.Tests` → **772 passed, 1 failed** = the pre-existing `AbiSurfaceTests` regen-churn artifact
  (ADDED=0; deferred, deviation #3). All other lanes green.

## Self-Check: PASSED

- All 6 created files + this SUMMARY present on disk (verified).
- All three task commits (`2056b62`, `b3cb376`, `f70d591`) present in git log.
- No file deletions in any commit; `Generated/UtinniCore.cs` not committed.
