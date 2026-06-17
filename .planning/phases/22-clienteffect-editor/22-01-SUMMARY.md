---
phase: 22-clienteffect-editor
plan: 01
subsystem: testing
tags: [clef, clienteffect, iff, codec, byte-exact, stableid, xunit, net472]

# Dependency graph
requires:
  - phase: 20-terrain-codec
    provides: MutableIffDocument.DeriveStableId ordinal-path stable-id contract (reused verbatim)
  - phase: 15-wave2-editors
    provides: MutableParticleEffect three-layer codec shape (cloned near-1:1) + the length-ripple DOM
provides:
  - "Pure-managed CLEF codec (Formats/ClientEffect/*) — the foundation every other Phase 22 plan composes on"
  - "ClientEffectDocument.FromBytes/FromIff front door (compose-never-reframe over MutableIffDocument)"
  - "Per-command ClientEffectCommand typed view with DeriveStableId-backed StableId + IsRaw raw-fallback"
  - "ClefFieldCodec variable-length LE/cstring encoders + D-03 CPAP encode-version guard"
  - "ClefDecoder.LooksLikeClientEffect (root SubTypeId == CLEF) for the Plan 02 decode-iff dispatch"
  - "9 committed golden fixtures (8 .iff + 1 writer-independent hand-authored .hex) + ClefFixtureBuilder"
  - "ClientEffectCodecTests — 43 green facts/theories under the ClientEffect filter"
affects: [22-02 CLI verbs (decode-clef/roundtrip-clef/apply-save-clef), 22-03 MCP read tool, 22-04 EffectsSubPanel]

# Tech tracking
tech-stack:
  added: []  # pure-managed; ZERO external packages (composition of in-repo assemblies)
  patterns:
    - "Particle three-layer clone (Document / Mutable* / per-command view + parse exception)"
    - "Per-command try/catch -> raw view (the Particle :187-201 idiom) for truncated/unknown/residual command leaves"
    - "Consume-all-bytes guard (cursor.Remaining == 0) forces residual-byte commands to raw"
    - "Version-conditional CPAP encoder that REFUSES fields above the source version (D-03 encode boundary)"
    - "Writer-independent hand-authored hex fixture as a cross-check against a shared builder/writer bug"

key-files:
  created:
    - UtinniCoreDotNet/Formats/ClientEffect/ClientEffectParseException.cs
    - UtinniCoreDotNet/Formats/ClientEffect/ClefFieldCodec.cs
    - UtinniCoreDotNet/Formats/ClientEffect/ClientEffectCommand.cs
    - UtinniCoreDotNet/Formats/ClientEffect/MutableClientEffect.cs
    - UtinniCoreDotNet/Formats/ClientEffect/ClientEffectDocument.cs
    - UtinniCoreDotNet/Formats/Decoders/ClefDecoder.cs
    - UtinniCoreDotNet.Tests/ClientEffect/ClefFixtureBuilder.cs
    - UtinniCoreDotNet.Tests/ClientEffect/ClientEffectCodecTests.cs
    - UtinniCoreDotNet.Tests/Fixtures/ClientEffect/ (9 committed goldens)
  modified:
    - UtinniCoreDotNet/UtinniCoreDotNet.csproj (register the 6 new ClientEffect compile items)
    - UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj (Content-copy the 10 golden fixtures)

key-decisions:
  - "Cloned the Particle stack near-1:1; the ONE new asset is ClefFieldCodec (variable-length, NOT fixed-span TrnFieldEncoder)"
  - "Command list is FLAT leaf children of the version FORM (no count chunk, unlike PEFT) — walk in physical order so ordinals match on-disk position"
  - "StableId reuses MutableIffDocument.DeriveStableId verbatim (REVIEWS #1) — the command view invents NO id scheme of its own"
  - "Serialize() == IffWriter.Write(SourceIff) — no bespoke writer; FORM lengths re-roll automatically"

patterns-established:
  - "Committed-golden integrity check: a Theory asserts each on-disk fixture == its deterministic builder output, catching golden/builder drift"
  - "Old-style (non-SDK) csproj REQUIRES explicit <Compile Include> per new file — globbing does not apply"

requirements-completed: [PROD-W2-CFX-01]

# Metrics
duration: ~40min
completed: 2026-06-17
---

# Phase 22 Plan 01: ClientEffect CLEF Codec Foundation Summary

**A pure-managed, byte-exact CLEF (FORM CLEF) codec — typed flat command list across versions 0001/0002/0003 with DeriveStableId-backed stable ids, layered raw-fallback (unknown version / unknown tag / truncated-known / residual-byte), and a D-03 CPAP encode-version guard — proven by 9 committed goldens and 43 green tests.**

## Performance

- **Duration:** ~40 min
- **Started:** 2026-06-17T21:32Z (approx)
- **Completed:** 2026-06-17
- **Tasks:** 2 of 2
- **Files modified/created:** 18 (6 codec + 2 test code + 9 fixtures + 2 csproj − overlap)

## Accomplishments
- Built the CLEF codec by cloning the Particle three-layer stack near-1:1; the only genuinely new asset is `ClefFieldCodec` (variable-length LE/cstring encode, the right tool for the string-edit that is the point of CFX).
- Hardened the three cross-AI-review concerns rooted in this plan: per-command DeriveStableId StableId (REVIEWS #1), canonical-payload consume-all-bytes + CLGT-23-byte/endianness + D-03 encode guard (REVIEWS HIGH #2 / #7), and truncated-known-tag per-command degrade (REVIEWS #4) — plus a writer-independent hand-authored hex fixture (REVIEWS #6).
- Committed 9 golden fixtures (8 synthesized `.iff` + 1 hand-authored `.hex`) and a 489-line, 43-test suite; all green under `--filter "FullyQualifiedName~ClientEffect"`.

## Task Commits

Each task was committed atomically:

1. **Task 1: CLEF model + decoder + variable-length field codec** - `7646dee` (feat) — includes the Rule-3 csproj compile-registration fold-in
2. **Task 2: Synthesized goldens + hand-authored hex fixture + codec test suite** - `ee8acf0` (test)

_Note: this plan's per-task TDD gate is the compile/verify gate (the codec) and the test suite — committed as one feat + one test commit rather than a separate RED commit, since the test project depends on the Task-1 types existing to compile at all._

## Files Created/Modified
- `Formats/ClientEffect/ClientEffectParseException.cs` — `ClientEffectParseError` enum (UnexpectedForm/ForgedCount/Truncated/VersionFieldMismatch) + ctor.
- `Formats/ClientEffect/ClefFieldCodec.cs` — variable-length LE/cstring writers + per-command payload encoders; CPAP encoder throws `VersionFieldMismatch` above the source version (D-03).
- `Formats/ClientEffect/ClientEffectCommand.cs` — per-command typed view; StableId, IsRaw, version-aware accessors; consume-all-bytes guard via `cursor.Remaining`.
- `Formats/ClientEffect/MutableClientEffect.cs` — model over MutableIffDocument; KnownEffectVersions {0001,0002,0003}; per-command catch -> raw; add/remove/reorder/edit; Serialize() == IffWriter.Write.
- `Formats/ClientEffect/ClientEffectDocument.cs` — FromBytes/FromIff front door.
- `Formats/Decoders/ClefDecoder.cs` — `LooksLikeClientEffect` (SubTypeId == CLEF).
- `Tests/ClientEffect/ClefFixtureBuilder.cs` — deterministic fixture synthesizer + the committed-file manifest.
- `Tests/ClientEffect/ClientEffectCodecTests.cs` — 43 tests (roundtrip / stableId / canonical-payload / CLGT-no-pad / string-edit / list-mutation / field-edit / raw-fallback / truncated-degrade / encode-guard / hand-authored / load-order / wrong-root).
- `Tests/Fixtures/ClientEffect/*` — 9 committed goldens.
- both `.csproj` — compile-item registration (production) + fixture Content-copy (tests).

## Decisions Made
See `key-decisions` frontmatter. Notably: the command list is FLAT leaf children of the version FORM (no count chunk, unlike PEFT), walked in physical-child order so the ordinal in the stable id matches the on-disk position — which is exactly why field-edit keeps the stable id and reorder/add shifts it (the contract 22-02's verify must split on).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Register new source files in the non-SDK csproj**
- **Found during:** Task 1 (after first build "succeeded" but the DLL contained none of the new types).
- **Issue:** `UtinniCoreDotNet.csproj` is an old-style (non-SDK) project with explicit `<Compile Include>` entries — it does NOT glob `**/*.cs`. The new `Formats/ClientEffect/*.cs` + `ClefDecoder.cs` were silently excluded from compilation, so the codec types never made it into `UtinniCoreDotNet.dll` (verified via reflection load of the built DLL).
- **Fix:** Added 6 explicit `<Compile Include>` entries (5 ClientEffect + ClefDecoder) and folded the change into the Task-1 commit (it is required for Task 1's files to compile at all).
- **Files modified:** `UtinniCoreDotNet/UtinniCoreDotNet.csproj`
- **Commit:** `7646dee` (amended)

**2. [Rule 3 - Blocking] Content-copy the committed goldens for test discovery**
- **Found during:** Task 2.
- **Issue:** the test csproj puts `Fixtures\**` in `DefaultItemExcludes`, so committed fixtures are not auto-copied to the test output dir where the tests read them via `AppContext.BaseDirectory`.
- **Fix:** Added explicit `<Content Include ... CopyToOutputDirectory="PreserveNewest">` for all 10 fixtures (mirrors the existing abi-baseline / FrozenPlugin convention).
- **Files modified:** `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj`
- **Commit:** `ee8acf0`

## Verification
- MSBuild of `UtinniCoreDotNet` (Release/x86): clean (only pre-existing Generated/UtinniCore.cs CS0108 warnings).
- MSBuild of `UtinniCoreDotNet.Tests` (Release/x86): clean.
- `dotnet test --no-build --filter "FullyQualifiedName~ClientEffect"`: **Passed! Failed: 0, Passed: 43**.
- `git status` shows `Generated/UtinniCore.cs` unmodified (CppSharp churn `git checkout --`'d after each build).
- All 9 golden fixtures (8 `.iff` + 1 hand-authored `.hex`) are git-tracked and detected by git as binary (no CRLF mangling).

## Known Stubs
None — the codec is fully wired; no placeholder data flows to any consumer. (The CLI/MCP/UI surfaces are out of this plan's scope and land in 22-02/03/04.)

## Threat Flags
None — this plan writes no files (the codec is in-memory only); the threat register's `mitigate` dispositions (T-22-malformed / -residual / -version-upgrade / -forged-count / -version-abort) are all covered by `ClefTruncatedKnown`, `ClefCanonicalPayload`, `ClefEncodeVersionGuard`, the wrong-root throw test, and `ClefRawFallback`. Path-containment/save-target threats are modeled in 22-02/22-04.

## Self-Check: PASSED
- Created files verified present (codec + tests + 9 fixtures) on disk.
- Commits `7646dee` and `ee8acf0` verified in `git log`.
