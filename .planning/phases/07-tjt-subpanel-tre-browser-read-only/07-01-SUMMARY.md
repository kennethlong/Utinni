---
phase: 07-tjt-subpanel-tre-browser-read-only
plan: 01
subsystem: api
tags: [tre, cot2000, v6000, zlib, deflate, iff, lazy-read, version-dispatch, formats]

requires:
  - phase: 07-tjt-subpanel-tre-browser-read-only
    provides: "07-00 synthetic v6000/COT2000(+companions)/5000/0004/malformed fixtures + SWG_SAMPLE_TRE_DIR resolver"
  - phase: 04-tier-2-cli-shim-golden-fixtures
    provides: "parse-tre/list-objects CLI + IffReader + golden test harness + size-first TreFile reader"
provides:
  - "Version-dispatching TRE reader (0004/0005/0006 size-first + 6000 crc-first; 5000 enumerate-empty) with zlib-aware block inflate"
  - "Lazy TOC-only enumeration: Open(string) reads payloads on demand by path; Open(Stream) is metadata-only (throws on GetRecordData); internal PayloadReadCount test seam"
  - "CotMasterIndex: COT2000 cross-archive enumeration + SearchTOC recognized-and-deferred"
  - "Shared TreArchiveIndex (flat AllPaths + resolution-complete TreEntryDescriptor) + TrePayloadResolver.TryResolve single-contract facade consumed by CLI + (Wave 2+) browser"
  - "list-objects migrated onto the shared IffReader path (criterion #4 lock-step)"
affects: [07-02, 07-03, 07-04a, 07-04b]

tech-stack:
  added: []
  patterns:
    - "Version-tag dispatch (TreVersions.Parse) keying record stride/field-order/enumerate-only posture"
    - "Lazy on-demand payload read with an internal PayloadReadCount seam (InternalsVisibleTo) proving zero eager reads"
    - "One shared browse/resolve facade (TreArchiveIndex + TrePayloadResolver) mechanically enforcing the single-code-path criterion"
    - "Division-form count guard bounded by the inflate cap (not streamLength) so zlib-compressed TOCs are not falsely rejected; subtraction-form offset guards"

key-files:
  created:
    - "UtinniCoreDotNet/Formats/Tre/TreVersion.cs"
    - "UtinniCoreDotNet/Formats/Tre/CotMasterIndex.cs"
    - "UtinniCoreDotNet/Formats/Tre/TreArchiveIndex.cs"
    - "UtinniCoreDotNet/Formats/Tre/TrePayloadResolver.cs"
    - "Utinni.Cli.Tests/Commands/ParseTreReaderTests.cs"
    - "Utinni.Cli.Tests/Commands/CotMasterIndexTests.cs"
    - "Utinni.Cli.Tests/Commands/TreArchiveIndexTests.cs"
  modified:
    - "UtinniCoreDotNet/Formats/Tre/TreFile.cs"
    - "UtinniCoreDotNet/Formats/Tre/TreHeader.cs"
    - "UtinniCoreDotNet/Formats/Tre/TreRecord.cs"
    - "UtinniCoreDotNet/Formats/Tre/TreParseException.cs"
    - "UtinniCoreDotNet/Properties/AssemblyInfo.cs"
    - "UtinniCoreDotNet/UtinniCoreDotNet.csproj"
    - "Utinni.Cli/Commands/ParseTreCommand.cs"
    - "Utinni.Cli/Commands/ListObjectsCommand.cs"
    - "Utinni.Cli.Tests/Commands/ParseTreCommandTests.cs (golden message only)"
    - "Utinni.Cli.Tests/Fixtures/tre/unsupported-version.expected.json"
    - "Utinni.Cli.Tests/Infrastructure/TreFixtureBuilder.cs (COT2000 tree-name relative paths)"
    - "Utinni.Cli.Tests/Fixtures/tre/synthetic-cot2000-2tree.toc (regenerated)"
    - "UtinniCoreDotNet.Tests/FormatsTests/Tre/TreFileTests.cs (migrated to lazy contract)"

key-decisions:
  - "Kept TreHeader.Version as a STRING renamed to VersionTag (JSON contract value) and added a TreVersion enum + EnumerateOnly bool — JSON `version` stays byte-identical."
  - "Division-form count guard bounded by the 256 MB inflate cap, NOT (streamLength - header)/stride: the plan's literal streamLength form falsely rejects zlib-compressed v6000 TOCs (caught by the 07-00 bad-zlib fixture). Overflow/DoS protection is preserved; the post-read infoBytes-length check still catches an undersized table."
  - "Open(Stream).GetRecordData throws InvalidOperationException (stream-backed has no path for lazy reads); tier-1 TreFileTests migrated from the old eager-read-after-dispose contract to Open(string) lazy reads."
  - "SearchTOC is RECOGNIZED + deferred (documented UnsupportedVersion error + TODO(searchtoc-fixture) + a skipped fixture-gated test), not silently dropped (review item 6)."
  - "COT2000 master-index TOC/name blocks read via zlib auto-detect (0x78 0x9c) so the raw synthetic fixture AND zlib-compressed real archives read through one path."
  - "07-00 COT2000 fixture tree names changed from bare to .toc-relative (cot2000/treeN.tre) so TrePayloadResolver resolves the companions under the containment guard; .toc regenerated."
  - "TreVersions.Parse/IsEnumerateOnly live on a TreVersions helper class (enums can't carry static methods); the key-link 'TreVersion.Parse' reads as TreVersions.Parse."

patterns-established:
  - "MSBuild (VS2026) builds UtinniCoreDotNet (WinForms image resources); `dotnet build` fails MSB3823 — build with MSBuild, run with `dotnet test --no-build`."

requirements-completed: [PROD-W1-TRE, PROD-01]

duration: ~2h
completed: 2026-05-26
---

# Phase 7 Plan 01: Version-Dispatching Zlib-Aware Lazy TRE Reader + Shared Facade Summary

> **CORRECTION (07-02 live smoke, 2026-05-27):** this plan originally treated **5000 as enumerate-empty** (per planning assumption D-06b). That was WRONG — 5000 is the readable SWGEmu Pre-CU client format (crc-first 24-byte stride, zlib blocks). Corrected in commit `d75c701`: `IsCrcFirst(V5000)=true`, `IsEnumerateOnly(V5000)=false`, the synthetic-5000 fixture is now a valid readable archive, and the 5000 tests assert enumeration. See 07-02-SUMMARY + [[project-tre-version-support-gap]]. Read all "5000 enumerate-empty" statements below as superseded.

**Refactored the 0005/0006-only eager TRE reader into a version-dispatching (0004/0005/0006 + 5000 + 6000), zlib-aware, lazy TOC-only enumerator; added the COT2000/SearchTOC master-index reader and the shared TreArchiveIndex + TrePayloadResolver.TryResolve facade that the CLI and browser both consume; migrated list-objects onto the shared IffReader path.**

## Performance
- **Duration:** ~2h
- **Tasks:** 3 (all auto/TDD)
- **Tests:** Utinni.Cli.Tests 82 passed / 1 skipped (SearchTOC fixture-gated); UtinniCoreDotNet.Tests 155 passed. Full suites green.

## Accomplishments
- `TreVersions.Parse` dispatch + `TreVersion` enum; v6000 crc-first 32-byte stride alongside the unchanged size-first 24-byte path; **5000 enumerate-empty, never routed through the v6000 stride**.
- zlib RFC1950 framing (0x78 0x9c, %31) detected/stripped; truncated frames → `InvalidZlibTrailer` (inflate-side, not Adler); unknown compressor → `UnknownCompressor`; checked-arithmetic guards (division/subtraction form).
- Lazy TOC-only enumeration with an internal `PayloadReadCount` seam; explicit `Open(Stream)` metadata-only contract.
- `CotMasterIndex` (COT2000 parsed, SearchTOC recognized+deferred) + `TreArchiveIndex`/`TrePayloadResolver` shared facade with a resolution-complete `TreEntryDescriptor` and a single `TryResolve` contract. The 07-00 self-contained COT2000 fixture resolves a readable companion payload in CI with no env var.
- `list-objects` now reads OBJS through the shared `IffReader` (criterion #4 lock-step); REVIEWS-MEDIUM-13 byte-scan retired; golden byte-identical.

## Task Commits
1. **Task 1: version-dispatch + zlib + lazy + 5000 + 0004 golden + checked arithmetic** — `8f16835` (feat)
2. **Task 2: CotMasterIndex + TreArchiveIndex/TrePayloadResolver facade** — `db6ea7a` (feat)
3. **Task 3: list-objects → shared IffReader** — `52cae4f` (refactor)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug in plan-specified guard] Count guard bounded by inflate cap, not streamLength**
- **Found during:** Task 1 (the 07-00 bad-zlib fixture tripped the count guard before reaching the inflate)
- **Issue:** The plan's literal `recordCount > (streamLength - headerSize)/stride` guard falsely rejects v6000, whose TOC is zlib-compressed on disk (uncompressed table legitimately exceeds the file).
- **Fix:** Bound against `MaxBlockSize / recordStride` (256 MB inflate cap) — still overflow-safe, still catches the count*stride-overflow fixture; the post-read info-block-size check catches an undersized table (so truncated.tre's golden stays unchanged).
- **Verification:** malformed-count-stride → ChunkLengthExceedsCap; malformed-zlib-bad-adler → InvalidZlibTrailer; truncated golden byte-identical.

**2. [Rule 1 - Cross-plan fixture correction] 07-00 COT2000 tree names made .toc-relative**
- **Found during:** Task 2 (resolver could not resolve bare tree names against the .toc dir because companions live in a cot2000/ subdir)
- **Fix:** Store tree names as `cot2000/treeN.tre`; regenerated the committed `.toc`. Resolver containment-checks the result (rejects `..`/rooted, verifies it stays under the base dir).
- **Verification:** TrePayloadResolver returns the readable companion payload in CI without env vars.

**3. [Rule 1 - Necessary scope] Tier-1 TreFileTests migrated to the lazy contract**
- **Issue:** Existing tier-1 tests asserted the eager-read-after-stream-dispose contract the plan deliberately replaces.
- **Fix:** Payload tests open via a temp-file path (lazy); added an `Open(Stream).GetRecordData` → InvalidOperationException test and a PayloadReadCount test. UtinniCoreDotNet.Tests stays 155 green.

**Other notes:** parse-tre help golden kept stable (list-objects HelpText left as-is to avoid touching the unrelated dispatch goldens). `TreVersions.Parse` (plural helper) used because C# enums cannot carry static methods.

**Total deviations:** 3 auto-fixed (all Rule 1). **Impact:** all necessary for correctness; the count-guard fix is load-bearing for every real v6000 archive. No scope creep.

## Issues Encountered
- `dotnet build` cannot compile UtinniCoreDotNet's WinForms image resources (MSB3823). Resolved by building with VS2026 MSBuild and running tests via `dotnet test --no-build` (recorded as a pattern for the rest of the phase).

## User Setup Required
None.

## Next Phase Readiness
- 07-02 (TRE Browser shell) can build its lazy virtual-path tree over `TreArchiveIndex.AllPaths` and resolve payloads via `TrePayloadResolver.TryResolve` — the same facade the CLI uses.
- **Cross-repo build note for 07-02+:** build Utinni with VS2026 MSBuild (not `dotnet build`); the TJT consumer compiles against the new `Formats/Tre` surface.
- No blockers.

---
*Phase: 07-tjt-subpanel-tre-browser-read-only*
*Completed: 2026-05-26*
