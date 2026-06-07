---
phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece
plan: 04
subsystem: api
tags: [mcp, modelcontextprotocol, net10, roundtrip, mcpclient, stdio, read-back, isolated-repack, threat-register, fixtures, security-doc]

# Dependency graph
requires:
  - phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece (plan 01)
    provides: "Utinni.Mcp host bootstrap + ResolvedRoot + CliLocator + ServerArgs (the --root/--cli-path launch contract the RoundTripTests pin)"
  - phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece (plan 02)
    provides: "5 read tools + CliResultMapper verbatim envelope (read_tre/decode_iff structured content the read assertions consume)"
  - phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece (plan 03)
    provides: "4 save tools + repack_tre + roundtrip_check + per-path serialization (the write surface + dry_run gate the round-trip drives)"
  - phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece (plan 03a)
    provides: "apply-save-* verbs that genuinely persist the verified bytes (makes the read-back centerpiece feasible)"
provides:
  - "RoundTripTests — real McpClient over StdioClientTransport against the BUILT Utinni.Mcp.exe (--cli-path pinned); 9 facts: handshake/exact-11-tools + multi-format read + edit-save-READ-BACK + verify-fail-no-write + isolated-copy repack + boundary path-escape"
  - "Committed minimal binary Fixtures/ (.tab/.iff/.stf/OT/.tdf + supported non-encrypted v0006 .tre) with provenance README + the generator-of-record (gen-fixtures.ps1)"
  - "MCP-SECURITY.md — design-time T-14 threat register (17 ids) mirroring 07-SECURITY.md; 5-layer model + advisory caveat + apply-save ZERO-verbs exception; each mitigate row cites file:line + a proving test"
  - "verify-mcp-security.ps1 — 5.1-safe structural gate (distinct-id count + required headings)"
affects: [phase-14-complete, 15-wave-2-editors, 16-live-mcp-bridge]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Real-MCP-client integration test: McpClient.CreateAsync(new StdioClientTransport{Command=<built server exe>, Arguments=[--root tempRoot, --cli-path <built utinni-cli>]}) — launches the server as a child process and drives ListToolsAsync/CallToolAsync over real stdio (no transcript fallback)"
    - "Edit->save->READ-BACK persistence proof: save_datatable a cell edit, then decode_iff the SAME loose path and assert the edited value + a CHANGED file hash — a no-op re-serialize fails the hash assertion"
    - "Destructive-write CI safety: dry_run=false repack runs ONLY on an isolated COPY; assert re-parse record-count (tolerate nondeterministic bytes) + a backup under the TreBackupPath policy, then clean the backup"
    - "Cross-TFM committed binary fixtures: generate ONCE via a 32-bit-PowerShell reflection load of the net472 builders, commit the bytes + provenance, copy to the net10 test output (the net10 project cannot reference the net472 builders)"
    - "Built-artifact resolver: walk up from AppContext.BaseDirectory to repo root, probe <projectBin>/{Release,Debug}/<tfm>/<exe>, prefer the matching config — no CWD/test-discovery reliance"

key-files:
  created:
    - "Utinni.Mcp.Tests/RoundTripTests.cs"
    - "Utinni.Mcp.Tests/Fixtures/README.md"
    - "Utinni.Mcp.Tests/Fixtures/sample.tab"
    - "Utinni.Mcp.Tests/Fixtures/sample.iff"
    - "Utinni.Mcp.Tests/Fixtures/sample.stf"
    - "Utinni.Mcp.Tests/Fixtures/sample_ot.iff"
    - "Utinni.Mcp.Tests/Fixtures/sample_v0006.tre"
    - "Utinni.Mcp.Tests/Fixtures/sample.tdf"
    - ".planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/MCP-SECURITY.md"
    - ".planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/verify-mcp-security.ps1"
    - ".planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/gen-fixtures.ps1"
  modified:
    - "Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj"

key-decisions:
  - "Fixtures generated ONCE via 32-bit PowerShell reflection-loading the built net472 DataTable/StringTable/Iff builders (and a direct MutableIffNode/IffWriter build for the OT), then committed — the net10 test project cannot ProjectReference the net472 builders (Consensus #8). Each fixture validated round-trips via the built utinni-cli before commit."
  - "sample_v0006.tre is the size-first EERT+0006 family (supported, repackable), NOT the EERT+6000 V6000/encrypted class — so repack-tre can actually rewrite an isolated copy (Consensus #9)."
  - "--mutate-cell takes a NUMERIC column index, not a name (the save_datatable columnId param accepts a name per its description but the verb requires an int index); the read-back test uses columnId='0'. Documented inline."
  - "EditSaveVerifyFail uses an OUT-OF-RANGE row (recordIndex=999) to trigger the in-band fail-closed no-write path deterministically over the real CLI (a perturb-seam is a unit-test-only hook, unavailable across the process boundary)."
  - "MCP-SECURITY.md authored as a design-time deliverable mirroring 07-SECURITY.md; each mitigate row cites BOTH file:line evidence AND the specific proving test (Consensus #10), with the 5-layer model + advisory caveat + the named apply-save ZERO-verbs exception."

patterns-established:
  - "Pattern: real-McpClient stdio round-trip against the built server exe with --cli-path pinned (the DEC-C3 automatable end-to-end proof)"
  - "Pattern: edit->save->read-back + hash-before/after as the persistence proof (no-op re-serialize fails)"
  - "Pattern: destructive-write CI test on an isolated copy with backup-under-policy assertion + cleanup"
  - "Pattern: per-threat-test-citation security register (file:line evidence + proving test per row)"

requirements-completed: [MCP-01, MCP-02]

# Metrics
duration: ~50min
completed: 2026-06-07
---

# Phase 14 Plan 04: Round-Trip Client + MCP-SECURITY.md Summary

**The phase-closing integration + security layer — a REAL `McpClient` completes the stdio handshake against the BUILT `Utinni.Mcp.exe` (launched as a child process with `utinni-cli.exe` pinned via `--cli-path`, no transcript fallback) and proves the EXACT 11-tool surface by name, multi-format read (`read_tre` over a supported non-encrypted v0006 archive + `decode_iff` over .tab/.stf/OT), the CENTERPIECE edit->save->READ-BACK persistence (save_datatable a cell edit, then decode_iff the same loose path and assert the edited value AND a changed file hash — a no-op re-serialize fails), verify-fail-no-write, an isolated-copy `repack_tre` dry-run-no-write/real-rewrite-with-backup, and a boundary path-escape; plus committed minimal binary `Fixtures/` (with a provenance README + the generator-of-record), and `MCP-SECURITY.md` — the design-time T-14 register (17 threats) mirroring `07-SECURITY.md`, encoding the 5-layer model + the advisory-not-enforcement caveat + the named `apply-save-*` ZERO-verbs exception, where EACH mitigate row cites both file:line evidence and the specific test proving it. Both CI lanes green.**

## Performance

- **Duration:** ~50 min
- **Completed:** 2026-06-07
- **Tasks:** 4 (all `auto`)
- **Files modified:** 12 (11 created, 1 modified)

## Accomplishments

- **Task 1 — committed binary Fixtures/ (+provenance):** generated 6 minimal real assets via a one-off generator (`gen-fixtures.ps1`) that 32-bit-PowerShell reflection-loads the built net472 `DataTableFixtureBuilder`/`StringTableFixtureBuilder`/`IffBuilder` (and builds the OT directly off the public `MutableIffNode`/`IffWriter` API): `sample.tab` (DTII all-types), `sample.stf` (multi-entry), `sample.iff` (plain FORM+leaf), `sample_ot.iff` (FORM SHOT, DERV base, 3 int params), `sample_v0006.tre` (supported size-first `EERT`+`0006`, NOT V6000/encrypted), and `sample.tdf` (text template-definition). `Fixtures/README.md` documents per-fixture provenance + the generator-of-record. The csproj copies `Fixtures/**` to the test output. Each fixture validated round-trips via the built `utinni-cli` (parse-tre / decode-iff / compile-definition) before commit.
- **Task 2 — RoundTripTests (9 facts):** a real `McpClient.CreateAsync` over `StdioClientTransport` launching the built `Utinni.Mcp.exe` with `--root <tempRoot> --cli-path <built utinni-cli.exe>` (Consensus #11). Asserts the EXACT 11 named tools; `read_tre`(v0006) + `decode_iff` over .tab/.stf/OT (SC1, each carries the right `type` discriminator); the **EditSaveRoundTrip centerpiece** (cell edit -> decode_iff read-back == new value + hash-changed, SC2); EditSaveVerifyFail (out-of-range row -> in-band error + file byte-unchanged); RepackDryRun (true=no-write/no-backup, false=rewrite-on-isolated-copy + backup-under-policy + same record-count, SC2); PathEscapeAtBoundary (hard error, no file outside root). No transcript fallback.
- **Task 3 — MCP-SECURITY.md:** the design-time T-14 register (17 distinct ids T-14-01..17 + T-14-SC) mirroring `07-SECURITY.md` section-for-section. Each `mitigate` row cites file:line evidence AND a proving test (e.g. T-14-15 -> `RoundTripTests.EditSaveRoundTrip`; T-14-08 -> `ApplySaveTabCommandTests` + `RoundTripTests.EditSaveVerifyFail`; T-14-01 -> `ResolvedRootTests` + `McpBoundaryPathEscapeTests` + the boundary round-trip; T-14-11 -> CI `LooseOverridePathTests`). Encodes the 5-layer model (annotations -> elicitation -> loose-override-default -> verify-before-commit -> backup) with the explicit caveat that layers 1–2 are ADVISORY and 3–5 are the deterministic enforcement, documents the SAVE-TOOL HOST ORCHESTRATION as layer 4 (distinct from the read pass-through), and names the scoped `apply-save-*` ZERO-verbs exception. `threats_open: 0`.
- **Task 4 — phase-close verification:** `verify-mcp-security.ps1` (5.1-safe) counts distinct `T-14-\d+` ids (require >=10; found 17) AND asserts the required headings/substrings (`## Trust Boundaries`, `## Threat Register`, `## Accepted Risks`, `5-layer`, `apply-save`, `advisory`) — exits 0. Both lanes green: net10 fast lane 71 passed; Utinni.Cli.Tests 239 passed / 2 skip (incl. the 4 ApplySave*CommandTests); UtinniCoreDotNet.Tests 637 passed (incl. the 18 LooseOverridePathTests binary-forward regression gate).

## Task Commits

Each task was committed atomically:

1. **Task 1: commit minimal binary Fixtures/ (+provenance) for net10 RoundTripTests** - `0c1eff5` (test)
2. **Task 2: RoundTripTests — real McpClient stdio + read-back + isolated repack** - `bc505b6` (test)
3. **Task 3: MCP-SECURITY.md — consolidated T-14 register with per-threat test citations** - `bc0dac5` (docs)
4. **Task 4: verify-mcp-security.ps1 — structural gate for MCP-SECURITY.md** - `40c334f` (test)

## Files Created/Modified

- `Utinni.Mcp.Tests/RoundTripTests.cs` — 9 real-McpClient facts (handshake/11-tools, multi-format read, edit-save-read-back, verify-fail, isolated repack, path-escape) + built-artifact resolver + stderr-on-failure diagnostics.
- `Utinni.Mcp.Tests/Fixtures/{README.md, sample.tab, sample.iff, sample.stf, sample_ot.iff, sample_v0006.tre, sample.tdf}` — the minimal committed assets + provenance.
- `Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj` — copies `Fixtures/**` to output (PreserveNewest).
- `.planning/phases/14-.../MCP-SECURITY.md` — the design-time T-14 register (SC4).
- `.planning/phases/14-.../verify-mcp-security.ps1` — the structural verify gate.
- `.planning/phases/14-.../gen-fixtures.ps1` — the one-off fixture generator-of-record.

## Decisions Made

- **Generate-once-then-commit fixtures via 32-bit PowerShell reflection** — the net10 test project cannot reference the net472 builders (Consensus #8); a 32-bit PS host is required because the builders pull in the x86 `UtinniCoreDotNet.dll`. Each fixture was validated via the built CLI before commit so the committed bytes are known-good supported assets.
- **`sample_v0006.tre` is `EERT`+`0006` (size-first, supported), NOT V6000/encrypted** (Consensus #9) — so the destructive repack real-write actually rewrites the isolated copy.
- **`--mutate-cell` is a numeric column index** — the round-trip read-back uses `columnId="0"`; documented inline since the tool param description says "id or name".
- **Per-threat test citation in the security doc** (Consensus #10) — each `mitigate` row names the test method/file, not a bare string.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Test API] save_datatable cell edit needs a NUMERIC column index, not a name**
- **Found during:** Task 2 (first real RoundTripTests run — EditSaveRoundTrip save returned IsError)
- **Issue:** The plan's behavior sketch implied `columnId:<new>` could be a column name; the `apply-save-tab --mutate-cell` verb requires a `row,col` INTEGER pair (a name yields a UsageError). The first run's save came back in-band-error so the read-back/hash assertions could not run.
- **Fix:** Use `columnId="0"` (the int column index) for the read-back centerpiece, and an OUT-OF-RANGE row (`recordIndex=999`) for the verify-fail-no-write case (a deterministic in-band fail-closed over the real CLI, since the unit-test perturb-seam is not reachable across the process boundary). Both documented inline.
- **Files modified:** Utinni.Mcp.Tests/RoundTripTests.cs
- **Verification:** RoundTripTests 9/9 pass; the read-back confirms the edited cell value AND a changed file hash.
- **Committed in:** bc505b6 (Task 2)

---

**Total deviations:** 1 auto-fixed (1 test-API reconciliation, no scope change).
**Impact on plan:** No scope change. The 11-tool surface, multi-format read, read-back persistence, isolated repack, security register, and both-lanes-green outcome match the plan exactly; the only adjustment is the column-index form for the datatable cell edit.

## Authentication Gates

None — no external service or auth was required. (The supply-chain legitimacy checkpoint for `ModelContextProtocol 1.4.0` was handled in Plan 01 Task 0; the synthetic fixtures committed here are generated deterministically from the project's own builders, so no package-legitimacy gate applies.)

## Issues Encountered

- The fixture generator first failed under 64-bit PowerShell (`BadImageFormatException` — the builders pull in the x86 `UtinniCoreDotNet.dll`); resolved by running it under `C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe`. PowerShell also wrapped `byte[]` return values into `Object[]` when passed to reflection `Invoke` — fixed with explicit `[byte[]]` casts at each call site.
- `dotnet test` defaults to the FULL net10 suite (71 facts; the RoundTripTests dominate runtime at ~53–56s because each fact launches a child server process); the targeted `--filter RoundTripTests` run is the 9 integration facts.

## Known Stubs

None. The RoundTripTests drive the real built server over real stdio against the real CLI; all 6 fixtures are real parseable assets (validated pre-commit); MCP-SECURITY.md is a complete register with no placeholder rows.

## Threat Flags

None. This plan adds only TEST-only temp-root surface (T-14-10, accepted in MCP-SECURITY.md) and the destructive-repack-in-CI mitigation (T-14-17, isolated-copy only). No new endpoints / auth / schema surface beyond the already-modeled read/write/repack paths — all consolidated in MCP-SECURITY.md.

## Next Phase Readiness

- Phase 14 is functionally complete: MCP-01 (read) + MCP-02 (write) are integration-proven end-to-end by a real MCP client; the security contract ships as a design-time deliverable.
- The round-trip pattern + committed fixtures are reusable for Phase 16's live-injected MCP bridge (MCP-03).
- No blockers.

## Self-Check: PASSED

- All 11 created files verified present.
- All 4 task commits (0c1eff5, bc505b6, bc0dac5, 40c334f) verified in git log.

---
*Phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece*
*Completed: 2026-06-07*
