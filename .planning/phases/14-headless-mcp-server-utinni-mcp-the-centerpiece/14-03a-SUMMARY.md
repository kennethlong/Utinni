---
phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece
plan: 03a
subsystem: api
tags: [cli, apply-save, loose-override, verify-before-commit, write-atomic, datatable, iff, stf, object-template, net472, golden-tests]

# Dependency graph
requires:
  - phase: 13-wrap-revived-compilers
    provides: "utinni-cli SaveCommand (LooseOverridePath.Resolve + WriteAtomic + .tre->repack-tre redirect) and the roundtrip-tab/iff/stf/ot apply+verify algorithms reused verbatim by apply-save-*"
  - phase: 09-tjt-datatable-editor
    provides: "DataTableWriter + MutableDataTableDocument + per-cell ROWS-slice comparison"
  - phase: 11-tjt-object-template-editor
    provides: "MutableObjectTemplate.Serialize + ObjectTemplateParamCodec + per-param-payload comparison"
provides:
  - "apply-save-tab CLI verb: typed DTII cell/row/column mutation -> verify untouched -> WriteAtomic loose-override (persists the mutated bytes)"
  - "apply-save-ot CLI verb: typed OT add/remove/edit override -> verify untouched params -> WriteAtomic"
  - "apply-save-iff CLI verb: generic IFF leaf mutate/remove -> verify untouched leaves -> WriteAtomic"
  - "apply-save-stf CLI verb: --edit-text KEY=VALUE -> verify untouched entries + sourceCrc preserved -> WriteAtomic"
  - "SaveCommandIo.WriteAtomic shared atomic-write core (Flush(true)) for all WRITE verbs"
  - "Locked envelope { written, path, bytesWritten, backupPath:null, validated } PLUS bytesEqualUntouched; EXIT-CODE contract (0 ok / 2 verify-failed) for the MCP host to wrap opaquely"
affects: [14-03-write-save-tools, mcp-tool-plans]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "apply-save-* = (roundtrip-* apply+verify) FUSED with (save WriteAtomic-to-loose-override), persisting the SAME mutatedBytes it verified (no re-load, no re-serialize between verify and commit)"
    - "verify-before-commit in ONE process: a failed untouched-region check exits 2 and writes NOTHING (no TOCTOU window, fail-closed)"
    - "internal-static Func<byte[],byte[]> test seam (InternalsVisibleTo Utinni.Cli.Tests) to drive the failed-verify-no-write path deterministically through the real CLI"
    - "MCP host decides persist-vs-fail on the EXIT CODE alone, never by parsing a domain field"

key-files:
  created:
    - "Utinni.Cli/Commands/ApplySaveTabCommand.cs"
    - "Utinni.Cli/Commands/ApplySaveOtCommand.cs"
    - "Utinni.Cli/Commands/ApplySaveIffCommand.cs"
    - "Utinni.Cli/Commands/ApplySaveStfCommand.cs"
    - "Utinni.Cli/Commands/SaveCommandIo.cs"
    - "Utinni.Cli.Tests/Commands/ApplySaveTabCommandTests.cs"
    - "Utinni.Cli.Tests/Commands/ApplySaveOtCommandTests.cs"
    - "Utinni.Cli.Tests/Commands/ApplySaveIffCommandTests.cs"
    - "Utinni.Cli.Tests/Commands/ApplySaveStfCommandTests.cs"
  modified:
    - "Utinni.Cli/Program.cs"
    - "Utinni.Cli.Tests/Fixtures/dispatch/no-args.expected.txt"
    - "Utinni.Cli.Tests/Fixtures/dispatch/help.expected.txt"

key-decisions:
  - "Shared SaveCommandIo.WriteAtomic helper (Flush(true)) rather than copy-pasting the atomic write into all 4 verbs"
  - "internal-static TestPerturbSerialized hook (reusing the existing InternalsVisibleTo Utinni.Cli.Tests) to drive failed-verify-no-write through the full CLI, since a canonical fixture+normal mutation never trips the untouched verify"
  - "apply-save-iff handles generic IFF leaf edits; apply-save-tab handles typed DTII datatable semantics (Cursor naming concern) — documented in code comments; MCP 14-03 tool names map accordingly"
  - "apply-save-* REQUIRES exactly one mutation (unlike roundtrip's no-op path) — it is a write verb"
  - "apply-save-stf folds sourceCrcPreserved into the verify gate (a sourceCrc drift also fails closed, exit 2)"

patterns-established:
  - "Pattern: apply+verify+commit fused write verb persisting the verified bytes (closes the save-re-serializes-the-unchanged-file gap)"
  - "Pattern: fail-closed verify-before-commit exit-2 contract consumed opaquely by the MCP host"

requirements-completed: [MCP-02, AUTH-05]

# Metrics
duration: 12min
completed: 2026-06-07
---

# Phase 14 Plan 03a: apply-save-* CLI Family Summary

**Four new golden-tested `utinni-cli` write verbs — apply-save-tab/ot/iff/stf — that each, in ONE atomic operation, apply exactly ONE typed mutation to a loose-override asset, verify byte-identity on the UNTOUCHED region, and ONLY on a clean verify WriteAtomic-commit the SAME mutated bytes — closing the reviewer-confirmed gap where the old `save` (re-serialize-the-unchanged-file) + `roundtrip-*` (verify-then-discard) two-step never persisted the edit.**

## Performance

- **Duration:** 12 min
- **Started:** 2026-06-07T01:43:22Z
- **Completed:** 2026-06-07T01:55Z
- **Tasks:** 2 (both TDD, golden-tested-first)
- **Files modified:** 12 (9 created, 3 modified)

## Accomplishments
- **apply-save-tab + apply-save-ot** (Task 1): typed datatable and object-template edit-and-persist verbs that reuse the roundtrip-tab / roundtrip-ot apply + untouched-comparison logic verbatim and add the one net-new step — `WriteAtomic` the MUTATED bytes — gated behind a verify-before-commit. Read-back tests prove the written file re-parses to the edited value and the file hash changed; failed-verify tests prove exit 2 + on-disk byte-unchanged.
- **apply-save-iff + apply-save-stf** (Task 2): generic-IFF leaf edits and string-table text edits with the same fuse. apply-save-stf additionally folds the D-02b `sourceCrcPreserved` policy into the fail-closed verify. Non-ASCII round-trip proven (João -> Théed re-parses clean).
- **SaveCommandIo.WriteAtomic** shared atomic-write core (`Flush(true)`) so all WRITE verbs share one durable-write path.
- The four verbs publish the locked envelope `{ written, path, bytesWritten, backupPath:null, validated }` plus `bytesEqualUntouched`, and the uniform exit-code contract (0 ok / 1 usage / 2 verify-failed|parse|containment / 3 not-found) so the 14-03 MCP host can wrap each 1:1 and decide persist-vs-fail on the EXIT CODE alone.
- Dispatch no-args/help goldens refreshed to enumerate all 4 new verbs.

## Task Commits

Each task was committed atomically:

1. **Task 1: apply-save-tab + apply-save-ot verbs + golden tests** - `7eed25f` (feat)
2. **Task 2: apply-save-iff + apply-save-stf verbs + dispatch-help golden refresh** - `bb3e5bc` (feat)

_Both tasks are `tdd="true"`; the verbs and their golden tests landed together per the established CLI-verb discipline (the verb must compile to be golden-tested in-process). RED/GREEN was exercised iteratively: the first run surfaced a real bug in the failed-verify test fixture (perturbing a non-existent row), which was fixed before GREEN._

## Files Created/Modified
- `Utinni.Cli/Commands/ApplySaveTabCommand.cs` — typed DTII apply+verify+commit; reuses RoundtripTabCommand mutation + per-cell ROWS-slice comparison.
- `Utinni.Cli/Commands/ApplySaveOtCommand.cs` — typed OT apply+verify+commit; reuses RoundtripOtCommand add/remove/edit + per-param-payload comparison.
- `Utinni.Cli/Commands/ApplySaveIffCommand.cs` — generic IFF leaf mutate/remove apply+verify+commit; reuses RoundtripIffCommand leaf-find + ParseHex + untouched-leaf comparison.
- `Utinni.Cli/Commands/ApplySaveStfCommand.cs` — .stf --edit-text apply+verify+commit; reuses RoundtripStfCommand per-entry-slice comparison + sourceCrc preserve.
- `Utinni.Cli/Commands/SaveCommandIo.cs` — shared atomic-write core (Flush(true)).
- `Utinni.Cli/Program.cs` — ParseArguments Type[] + Dispatch switch wire the 4 new verbs (now 21 verbs via the no-cap Type[] overload).
- `Utinni.Cli.Tests/Commands/ApplySaveTabCommandTests.cs` — 8 facts (read-back, remove-row/col, failed-verify-no-write, usage/containment/.tre/not-found, envelope).
- `Utinni.Cli.Tests/Commands/ApplySaveOtCommandTests.cs` — 9 facts (add/edit/remove read-back, failed-verify, duplicate-add, edit-nonexistent, missing-value-int, no-mutation, containment, not-found).
- `Utinni.Cli.Tests/Commands/ApplySaveIffCommandTests.cs` — 6 facts (mutate-leaf read-back, remove-leaf, failed-verify, no-mutation, containment, not-found).
- `Utinni.Cli.Tests/Commands/ApplySaveStfCommandTests.cs` — 9 facts (edit read-back + untouched + crc, non-ASCII, failed-verify, missing-key, malformed, no-mutation, containment, not-found).
- `Utinni.Cli.Tests/Fixtures/dispatch/{no-args,help}.expected.txt` — golden refresh listing the 4 new verbs.

## Decisions Made
- **Shared SaveCommandIo.WriteAtomic** rather than 4 copies of the atomic write — one durable-write source of truth across `save` + the apply-save-* family.
- **internal-static TestPerturbSerialized seam** to drive the failed-verify-no-write contract through the REAL CLI: a canonical fixture + a normal single mutation never trips the untouched-region check, so each verb exposes a test-only hook (null in production) that perturbs an UNTOUCHED region of the serialized bytes; the test asserts exit 2 + the on-disk file is byte-unchanged. This satisfies the T-14-08 reviewer gate without weakening production.
- **apply-save-iff = generic IFF, apply-save-tab = typed DTII** — documented in code comments per the Cursor divergent naming concern; the 14-03 MCP tool names map accordingly.
- **Exactly-one-mutation required** (not roundtrip's optional no-op) since these are write verbs.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Failed-verify test fixture perturbed a non-existent row**
- **Found during:** Task 1 (ApplySaveTabCommandTests)
- **Issue:** The first failed-verify test draft perturbed cell (1,0) but `BuildV1AllTypes` has only one row, so the perturb hook threw `ArgumentOutOfRangeException` instead of producing a verify-failure (the test crashed rather than asserting exit 2).
- **Fix:** Perturb cell (0,1) — a different UNTOUCHED cell in the same single row — so the untouched-region verify trips deterministically.
- **Files modified:** Utinni.Cli.Tests/Commands/ApplySaveTabCommandTests.cs
- **Verification:** ApplySaveTab_FailedUntouchedVerify_ExitsTwo_AndFileIsByteUnchanged passes (exit 2, file byte-unchanged).
- **Committed in:** 7eed25f (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 test-fixture bug).
**Impact on plan:** No scope change. The fix only corrected a test-harness off-by-fixture error; the production verbs match the plan exactly.

## Issues Encountered
- `dotnet build` fails on `UtinniCoreDotNet.csproj` with MSB3823/MSB3822 (non-string .resx resources) — the documented WinForms-resource limitation. Built with VS2026 MSBuild (`MSBuild.exe -p:Configuration=… -p:Platform=x86`) and ran tests with `dotnet test --no-build`, matching the repo's established net472 toolchain.
- The Git Bash tool munges MSBuild `/p:` switches into paths; used the `-p:` dash form instead.
- `DataTableCellValue.AsInt()` and `ObjectTemplateParamValue` int access are `internal` to UtinniCoreDotNet (not visible to the test project), so read-back assertions use the public `ToCsvString(columnType)` (datatable) and public `IntValue` (OT) accessors.

## Known Stubs
None. All four verbs are fully wired (dispatch + apply + verify + commit) and golden-tested with read-back persistence proofs.

## Threat Flags
None. The verbs ADD the mitigations the plan's threat register requires (T-14-08 verify-before-commit, T-14-01 containment, T-14-14 .tre->repack-tre redirect, T-14-15 persist-the-verified-bytes) and introduce no new trust-boundary surface beyond the already-modeled loose-override write path.

## Next Phase Readiness
- The 14-03 MCP write/save tool plan can now wrap each apply-save-* verb 1:1 over the CliDispatcher seam (14-01) and decide persist-vs-fail on the EXIT CODE alone — never parsing `bytesEqualUntouched`.
- All 4 verbs green in Debug + Release|x86; full Utinni.Cli.Tests suite 239 passed / 2 pre-existing skips.
- No blockers.

## Self-Check: PASSED

- All 9 created files verified present (see below).
- Both task commits verified in `git log`.

---
*Phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece*
*Completed: 2026-06-07*
