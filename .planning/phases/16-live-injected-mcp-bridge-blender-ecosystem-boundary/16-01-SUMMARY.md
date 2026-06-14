---
phase: 16-live-injected-mcp-bridge-blender-ecosystem-boundary
plan: 01
subsystem: testing
tags: [blender, eco-01, validate-bundle, cli, rsp, tre, cross-validation, path-containment]

# Dependency graph
requires:
  - phase: 13-cli-verbs
    provides: parse-tre / decode-iff / inspect-iff readers + JsonOutput envelope + CLI dispatch
  - phase: 08-iff-editor
    provides: LooseOverridePath root-containment helper (netstandard2.0 PathContainment)
provides:
  - Utinni-authoritative Blender boundary contract doc (docs/ai/blender-boundary-contract.md, D-05/D-06)
  - validate-bundle CLI verb (thin TEXT-only bundle validator, no 3D/IFF codec, DEC-A3 clean)
  - shared LooseOverridePath.IsContainedUnderRoot predicate (single source-of-truth containment gate)
  - pinned Blender golden fixtures + SHA-256 provenance manifest (D-08 cross-validation source)
  - cross-validation finding CV-1 (Blender crc-first vs Utinni size-first v0005 TOC order)
affects: [16-02 (live-bridge track shares the test-infra patterns), future Blender-ecosystem work]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Thin TEXT-only CLI verb (manifest/.rsp/.cfg parse + File.Exists; no binary codec) mirroring InspectIffCommand taxonomy"
    - "Single shared IsContainedUnderRoot predicate routes BOTH relative + absolute ref branches (no drift)"
    - "SHA-256 provenance manifest asserted by a golden test (silent fixture-refresh drift guard)"
    - "Doc<->verb parity test: bucket-filename set asserted present in the contract doc (Windows-safe, no git grep)"

key-files:
  created:
    - docs/ai/blender-boundary-contract.md
    - Utinni.Cli/Commands/ValidateBundleCommand.cs
    - Utinni.Cli.Tests/Commands/BlenderBoundaryGoldenTests.cs
    - Utinni.Cli.Tests/Commands/ValidateBundleTests.cs
    - Utinni.Cli.Tests/Fixtures/blender/ (frn_all_bed_sm_s1_l0.msh, retail_mini_0005.tre, data_compressed_mesh_static.rsp, client_search_paths.cfg, swg_export_manifest.json, fixture-hashes.txt)
  modified:
    - Utinni.Cli/Program.cs
    - UtinniCoreDotNet.PathContainment/LooseOverridePath.cs
    - Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt
    - Utinni.Cli.Tests/Fixtures/dispatch/no-args.expected.txt

key-decisions:
  - "validate-bundle exit 0 with envelope findings for a structurally-valid bundle (CDX-NEW-9); only unparseable manifest/.rsp is exit 2"
  - "Contained-absolute .rsp RHS allowed + probed; escaping ref recorded-not-probed (CUR-NEW-3 / C-15)"
  - "Extracted a real shared IsContainedUnderRoot predicate (R3-6 preferred path) rather than mirroring LooseOverridePath's tail"
  - "Pinned retail_mini_0005.tre golden asserts the REAL parse-tre exit-2 (CV-1 finding), not a false exit-0"

patterns-established:
  - "Bucket suffix->filename table is a single named static (BucketFilenames) so the doc<->verb parity test references one source"

requirements-completed: [ECO-01]

# Metrics
duration: ~85min
completed: 2026-06-14
---

# Phase 16 Plan 01: Blender ecosystem boundary (ECO-01) Summary

**Formalized the Utinni ↔ swg-blender-plugin file-format seam: a four-surface Utinni-authoritative contract doc, a thin `validate-bundle` CLI verb (TEXT-only, contained-absolute-aware path containment via a single shared `IsContainedUnderRoot` predicate, explicit `valid`/`hasRejectedRefs` envelope), SHA-256-pinned Blender golden fixtures, and a cross-validation suite that proved the working `.msh` read AND surfaced a real crc-first-vs-size-first v0005 TOC disagreement (CV-1).**

## Performance

- **Duration:** ~85 min
- **Tasks:** 3 of 4 (Task 4 = blocking-human cross-repo checkpoint, see below)
- **Files created:** 9 (1 doc, 1 verb, 2 test classes, 6 fixtures incl. dir)
- **Files modified:** 4

## Accomplishments
- `docs/ai/blender-boundary-contract.md` documents all four D-06 surfaces: the `.rsp` `{path} @ {ABSOLUTE path}` line format + 7 suffix→bucket→filename rules + cfg dialects; the `swg_export_manifest.json` schema sourced verbatim from the real exporter (`client_cfg` nested inside `assets`, unknown-field tolerance, R3-5/CDX-NEW-10); the TRE version matrix mirroring `TreVersion.cs` (5000 READABLE, 6000 enumerate-only, no COT2000-as-version); the bundle layout; and the anti-coupling rules (DEC-A3, no geometry codec). Plus the exit/valid semantics (CDX-NEW-9) and the CV-1 finding.
- `validate-bundle` verb: parses manifest + `.rsp` + `.cfg` as TEXT only, existence-checks contained refs, routes BOTH relative and absolute refs through the single shared `IsContainedUnderRoot` predicate, allows contained absolutes / rejects (never probes) escapes, and emits an explicit `valid`/`hasRejectedRefs` envelope.
- Pinned `frn_all_bed_sm_s1_l0.msh` + `retail_mini_0005.tre` verbatim (in-repo, no LFS) with a SHA-256 provenance manifest asserted by a golden test (T-16-04 drift guard).
- 14 ECO-01 tests green (3 golden + 10 verb + 1 doc↔verb parity); full Utinni.Cli.Tests suite 263 passed / 2 skipped / 0 failed.

## Task Commits

1. **Task 1: Pin Blender goldens + cross-validation scaffold** - `1cf0415` (test)
2. **Task 2: validate-bundle verb + shared IsContainedUnderRoot** - `d6c60c6` (feat)
3. **Task 3: Boundary contract doc + doc↔verb parity test** - `9b0f9ee` (docs)
4. **Task 4: Cross-repo pointer note** - PENDING (blocking-human checkpoint; see below)

## Cross-Validation Findings

**CV-1 — Blender's synthetic v0005 `.tre` uses crc-first TOC order; Utinni reads size-first.**
- **Found during:** Task 1 (running `parse-tre` over the pinned `retail_mini_0005.tre`).
- **Finding:** `retail_mini_0005.tre` is a synthetic reader-unit fixture (`swg-blender-plugin/tests/test_tre_versions.py`), not a real `TreeFileBuilder` export. Its v0005 TOC entries are laid out crc-first (`tre_reader.py` `TOC_ENTRY_FMT="<Iiiiii"`: crc, length, offset, comp, clen, fnoff), whereas Utinni reads the 0004/0005/0006 family size-first (`uncompressedSize, offset, compressor, compressedSize, checksum, nameOffset`, validated against the live SWGEmu client per `TreVersion.cs`). The two orders disagree, so `parse-tre` reads the second TOC int (length=36) into the compressor slot → `UnknownCompressor` exit 2.
- **Resolution (NOT a reader fix):** Utinni's size-first v0005 reader is correct for real SWGEmu/TreeFileBuilder output; the synthetic fixture's layout is the outlier. The golden test asserts the REAL exit-2 behavior (keeping the disagreement visible + guarding both fixture and reader against silent change), and the contract doc records CV-1 + the rule that anyone hand-authoring a cross-tool v0005 `.tre` MUST use the engine's size-first TOC order. The working cross-validation is the `.msh` path (decode-iff exit 0, count-only mesh: 139 verts, 1 shader).

## Decisions Made
See `key-decisions` frontmatter. The pivotal one: surface CV-1 honestly (assert real exit-2) rather than force a false exit-0, since cross-validation exists precisely to catch this class of boundary mismatch.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Plan assumed `parse-tre` over `retail_mini_0005.tre` exits 0; it exits 2 (CV-1).**
- **Found during:** Task 1.
- **Issue:** The plan's acceptance criterion "parse-tre opens the pinned .tre exits 0" is contradicted by reality — the Blender synthetic mini-TRE's crc-first v0005 TOC is not readable by Utinni's size-first v0005 reader.
- **Fix:** The golden test asserts the actual exit-2 `UnknownCompressor` behavior with a clear CV-1 comment, and the contract doc documents the finding. The reader was NOT changed (it is correct for real exports). The `.msh` cross-validation (the genuine D-08 proof) passes.
- **Files modified:** Utinni.Cli.Tests/Commands/BlenderBoundaryGoldenTests.cs, docs/ai/blender-boundary-contract.md.
- **Committed in:** `1cf0415` / `9b0f9ee`.

**2. [Rule 3 - Blocking] `dotnet test` / `dotnet build` fails on UtinniCoreDotNet (MSB3823 image .resx).**
- **Found during:** Task 1 verify.
- **Issue:** The plan's verify is `dotnet test Utinni.Cli.Tests --filter ...`, but `dotnet build` of UtinniCoreDotNet hits MSB3823 (non-string .resx) on this toolchain (known: memory `feedback_dotnet_build_msbuild_resources`).
- **Fix:** Built with VS2026 MSBuild (`-p:Configuration=Release -p:Platform=x86`), then ran `dotnet test --no-build -c Release --filter ...`. Equivalent coverage; Windows-safe (C-19).
- **Files modified:** none (build-recipe only).
- **Verification:** all ECO-01 filters green via the MSBuild + `dotnet test --no-build` recipe.

**3. [Rule 1 - Bug] New verb changed CLP help/no-args output; refreshed dispatch goldens.**
- **Found during:** Task 2.
- **Issue:** Registering `validate-bundle` made it appear in `--help`/no-args, failing `dispatch/help` + `dispatch/no-args` text goldens. Also the em-dash in the HelpText rendered literally in-process.
- **Fix:** Inserted the `validate-bundle` block into both goldens (verbatim CLP wrapping) and switched the HelpText em-dash to an ASCII hyphen for stable cross-context output.
- **Files modified:** Utinni.Cli.Tests/Fixtures/dispatch/{help,no-args}.expected.txt, Utinni.Cli/Commands/ValidateBundleCommand.cs.
- **Committed in:** `d6c60c6`.

---

**Total deviations:** 3 (1 plan-premise correction surfaced as CV-1, 1 build-recipe, 1 golden refresh). **Impact:** No scope creep. CV-1 is the highest-value output (a real boundary finding the cross-validation was designed to catch).

## Issues Encountered
None beyond the deviations above.

## User Setup Required
None.

## Next Phase Readiness / Task 4 Checkpoint

**Task 4 (cross-repo pointer note in `D:/Code/swg-blender-plugin/REFERENCES.md`) is a `checkpoint:human-action`, gate=blocking-human.** swg-blender-plugin is a THIRD repo outside standing write authority — the executor did NOT perform the write. Awaiting human approval. Proposed change:

- **File:** `D:/Code/swg-blender-plugin/REFERENCES.md` — append ONE row to the existing "External references (D:/Code)" table:

  | Path | Contents |
  | --- | --- |
  | D:/Code/Utinni/docs/ai/blender-boundary-contract.md | Authoritative Utinni ↔ swg-blender-plugin file-format / .rsp search-path boundary contract (ECO-01). Utinni owns format+injection (reads/validates exports); this repo owns DCC authoring (writes). Neither repo imports the other. |

- **Proposed commit message (in the swg-blender-plugin repo):**
  `docs: point at Utinni-authoritative Blender boundary contract (ECO-01)`

Once approved, the orchestrator adds the row + commits in swg-blender-plugin, then Task 4's verify (`git grep -q "blender-boundary-contract" -- REFERENCES.md`) closes the plan.

## Self-Check: PASSED

All created files present on disk; all three task commits (`1cf0415`, `d6c60c6`, `9b0f9ee`) exist in git history. ECO-01 filters + full Utinni.Cli.Tests suite green.

---
*Phase: 16-live-injected-mcp-bridge-blender-ecosystem-boundary*
*Tasks 1-3 completed: 2026-06-14 (Task 4 pending human checkpoint)*
