---
phase: 07-tjt-subpanel-tre-browser-read-only
plan: 00
subsystem: testing
tags: [tre, cot2000, v6000, zlib, deflate, fixtures, golden-tests, xunit]

# Dependency graph
requires:
  - phase: 04-tier-2-cli-shim-golden-fixtures
    provides: "FixturePath/GoldenTestRunner/InProcessCliRunner test infrastructure + existing size-first synthesized-*-v000X.tre fixtures + parse-tre golden convention"
provides:
  - "Deterministic in-repo TreFixtureBuilder that emits synthetic v6000 / COT2000(+companions) / 5000 / 0004 / 4 malformed TRE fixtures"
  - "Self-contained COT2000 master index whose global TOC entries resolve into committed companion .tre archives (no env vars needed for the resolver path)"
  - "FixturePath.SampleTreDir()/HasSampleTreDir() SWG_SAMPLE_TRE_DIR env resolver so large real-archive goldens skip cleanly"
  - "Regenerate-and-compare self-test that fails CI if a committed fixture drifts from the builder (anti-drift)"
affects: [07-01, 07-02, 07-03, 07-04a, 07-04b]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Deterministic byte-builder as generator-of-record for committed binary fixtures (env-gated emit + regenerate-and-compare self-test)"
    - "In-repo synthetic fixtures SUPPLEMENT env-gated real-archive goldens so the v6000/COT2000/5000 code paths run on every CI run"

key-files:
  created:
    - "Utinni.Cli.Tests/Infrastructure/TreFixtureBuilder.cs"
    - "Utinni.Cli.Tests/Infrastructure/TreFixtureBuilderTests.cs"
    - "Utinni.Cli.Tests/Fixtures/tre/synthetic-v6000-2record.tre"
    - "Utinni.Cli.Tests/Fixtures/tre/zlib-framed-1record-v6000.tre"
    - "Utinni.Cli.Tests/Fixtures/tre/synthetic-cot2000-2tree.toc"
    - "Utinni.Cli.Tests/Fixtures/tre/cot2000/tree0.tre"
    - "Utinni.Cli.Tests/Fixtures/tre/cot2000/tree1.tre"
    - "Utinni.Cli.Tests/Fixtures/tre/synthetic-5000-header.tre"
    - "Utinni.Cli.Tests/Fixtures/tre/synthetic-0004-header.tre"
    - "Utinni.Cli.Tests/Fixtures/tre/malformed-count-stride-overflow.tre"
    - "Utinni.Cli.Tests/Fixtures/tre/malformed-offset-length-overflow.tre"
    - "Utinni.Cli.Tests/Fixtures/tre/malformed-zlib-bad-adler.tre"
    - "Utinni.Cli.Tests/Fixtures/tre/malformed-unknown-compressor.tre"
  modified:
    - "Utinni.Cli.Tests/Infrastructure/FixturePath.cs"

key-decisions:
  - "COT2000 master-index TOC block + name block are stored UNCOMPRESSED in the synthetic fixture; 07-01's reader must DETECT zlib framing (0x78 0x9c) on those blocks so the fixture AND the real zlib-compressed archives read through one code path."
  - "COT2000 global-TOC compressor enum (synthetic): 0=none, 1=raw-deflate, 2=zlib. The fixture exercises 0 (tree1) and 1 (tree0); compressor=2/enumerate-only is left to the env-gated real COT2000 set."
  - "Size-first 0004/0005/0006 family kept byte-identical; crc-first 32-byte stride used only for 6000/COT2000 (RESEARCH Open Q1 resolution — no silent re-decode of existing CLI goldens)."
  - "bad-zlib fixture corrupts the deflate body (valid 0x78 0x9c header) so the failure is detectable on the INFLATE side, not via Adler32 the .NET BCL ignores (review item 11). Filename kept as malformed-zlib-bad-adler.tre for continuity."
  - "Committed fixtures (re)emitted via an env-gated generator test (GSD_EMIT_FIXTURES=1) writing to the source tree; normal CI runs no-op it and the regenerate-and-compare tests enforce byte-identity."

patterns-established:
  - "Generator-of-record + regenerate-and-compare: a hand-edited committed fixture fails CI (threat T-07-00-01)."

requirements-completed: [PROD-W1-TRE, PROD-01]

# Metrics
duration: ~25 min
completed: 2026-05-26
---

# Phase 7 Plan 00: TRE Fixture Foundation Summary

**Deterministic in-repo TreFixtureBuilder emitting synthetic v6000 (crc-first zlib TOC), a self-contained COT2000 master index + 2 companion `.tre` archives, non-6000-layout 5000 + 0004 headers, and 4 adversarial fixtures — plus a `SWG_SAMPLE_TRE_DIR` env resolver so the real-archive goldens skip cleanly.**

## Performance

- **Duration:** ~25 min
- **Tasks:** 2 (both TDD-style: builder-as-generator + regenerate-and-compare self-test)
- **Files created:** 13 (2 infra + 11 fixtures incl. 2 COT2000 companions)
- **Files modified:** 1 (FixturePath.cs)
- **Tests:** 12 passing (`dotnet test Utinni.Cli.Tests --filter "TreFixtureBuilder"`)

## Accomplishments
- `TreFixtureBuilder` deterministic byte builder with 9 emit methods (5 valid + 4 malformed) + an `EmitAll` batch.
- Self-contained COT2000 master index: the global TOC entry for a readable path resolves into a committed `cot2000/tree{idx}.tre` companion at the declared offset and inflates to the expected payload — the RESOLVER path is CI-exercisable with no env vars (closes review consensus #2).
- Four malformed fixtures mapped to 07-01 threat IDs (count*stride T-07-01, offset+length T-07-03, truncated-zlib T-07-04, unknown-compressor).
- `FixturePath.SampleTreDir()/HasSampleTreDir()` env resolver (supplements, does not replace, in-repo CI coverage).
- Regenerate-and-compare self-test enforces byte-identity between committed fixtures and the builder (anti-drift).

## Task Commits

1. **Task 1: TreFixtureBuilder + FixturePath resolver + valid fixtures** - `122970d` (test)
2. **Task 2: Malformed/adversarial fixtures** - `75d4fa4` (test)

## Files Created/Modified
- `Utinni.Cli.Tests/Infrastructure/TreFixtureBuilder.cs` - deterministic emitters for all synthetic fixtures + zlib/Adler32/size-first/crc-first helpers
- `Utinni.Cli.Tests/Infrastructure/TreFixtureBuilderTests.cs` - 12 self-tests (regenerate-and-compare, structural asserts, COT2000 resolver contract, env resolver, malformed shapes) + env-gated generator
- `Utinni.Cli.Tests/Infrastructure/FixturePath.cs` - added `SampleTreDir()` + `HasSampleTreDir()`
- `Utinni.Cli.Tests/Fixtures/tre/*.tre`, `*.toc`, `cot2000/*.tre` - 11 committed synthetic fixtures

## Decisions Made
See `key-decisions` frontmatter. The load-bearing one for 07-01: the COT2000 reader must detect zlib framing on the master-index TOC/name blocks (the synthetic fixture stores them raw; real archives zlib them) so both read through one path.

## Deviations from Plan

None - plan executed exactly as written. The csproj `Content Include="Fixtures\**\*"` recursive glob already deploys the new `cot2000/` subdirectory to the output, so no csproj edit was needed (the plan's "register if non-SDK-style" branch is a no-op — the project is SDK-style).

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required. (The optional `SWG_SAMPLE_TRE_DIR` env var only enables the supplementary real-archive goldens; CI coverage does not depend on it.)

## Next Phase Readiness
- 07-01 has real v6000 / COT2000(resolvable) / 5000 / 0004 / malformed bytes to TDD against on every CI run.
- **Contract for 07-01:** the COT2000 reader must (1) detect zlib framing on the master-index TOC/name blocks, (2) map the global-TOC compressor enum {0=none,1=deflate,2=zlib}, and (3) resolve `treeFileIndex`/`offset`/`compressedLength` into the companion archive under `cot2000/`.
- No blockers.

---
*Phase: 07-tjt-subpanel-tre-browser-read-only*
*Completed: 2026-05-26*
