---
phase: 14-headless-mcp-server-utinni-mcp-the-centerpiece
verified: 2026-06-06T00:00:00Z
status: passed
score: 5/5 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: none
  previous_score: none
---

# Phase 14: Headless `Utinni.Mcp` server — Verification Report

**Phase Goal:** A separate net10 stdio MCP process owning ZERO format/business logic, dispatching every tool call to `Process.Start` of `utinni-cli.exe`. Read tools wrap existing CLI verbs; write tools wrap the SAVE verb at the loose-override tier with the 5-layer safety model and a first-class `MCP-SECURITY.md` threat register.
**Verified:** 2026-06-06
**Status:** PASSED
**Re-verification:** No — initial verification

## Goal Achievement

This verification did NOT trust SUMMARY.md. Every claim was checked against source, and the SC1/SC2/SC3/SC5 real-client round-trip was **executed live** in this verification run (both `Utinni.Mcp.exe` net10 and `utinni-cli.exe` net472 were already built, so `RoundTripTests` ran rather than being skipped). All 71 net10 tests passed including the full integration suite; 33 apply-save CLI golden tests passed including read-back-after-persist.

### Observable Truths (ROADMAP Success Criteria)

| # | Truth (Success Criterion) | Status | Evidence |
| --- | --- | --- | --- |
| SC1 | An AI agent can READ any supported asset (TRE/IFF/datatable/`.tab`/`.stf`/OT) through the net10 stdio server wrapping CLI verbs (MCP-01) | ✓ VERIFIED | `ReadTools.cs` exposes `read_tre`/`inspect_iff`/`decode_iff`/`list_world_objects`/`get_template_schema`, each a thin `root.Resolve → cli.RunAsync(verb) → CliResultMapper` wrapper. Live: `RoundTripTests.ReadRoundTrip_ReadTre` + `DecodeMultiFormat_DecodeIff` Theory over `.tab`/`.stf`/OT all PASSED against the built net10 exe over real stdio. |
| SC2 | EDIT+SAVE via per-format write tools defaulting to loose-override, byte-exact verify-before-commit, `dry_run` gate on destructive repack (MCP-02) | ✓ VERIFIED | `SaveTools.cs` 4 `save_*` tools each wrap ONE `apply-save-*` verb opaquely (decide on exit code, never parse domain fields). `ApplySaveTabCommand.cs:189-200` applies → verifies untouched → `WriteAtomic` the SAME verified bytes. Live: `RoundTripTests.EditSaveRoundTrip_…ReadBackReflectsEditedCell_AndFileHashChanged` PASSED (a no-op re-serialize cannot pass this); `EditSaveVerifyFail_…LeavesFileUnchanged` PASSED; `RepackDryRun_TrueNoWrite_FalseRewritesIsolatedCopy` PASSED. |
| SC3 | `resolvedRoot` pinned fail-closed at startup; no write escapes the root — proven by a path-traversal test | ✓ VERIFIED | `ResolvedRoot.PinOrThrow` throws before transport opens when no root / non-existent dir (`Program.cs:59`). `LooseOverridePath.Resolve` rejects rooted/`..`/canonicalization-escape/prefix-attack (`PathContainment/LooseOverridePath.cs:81+`). Live: `ResolvedRootTests` (5 escape classes) + `McpBoundaryPathEscapeTests` (zero CLI spawn on escape) + `RoundTripTests.PathEscapeAtBoundary_…NoFileOutsideRoot` all PASSED. |
| SC4 | `MCP-SECURITY.md` documents the 5-layer model + advisory-not-enforcement caveat | ✓ VERIFIED | `MCP-SECURITY.md` present: 5-layer table (layers 1-2 advisory, 3-5 enforcement), named ZERO-verbs exception for `apply-save-*`, full T-14-01..17 + T-14-SC register where EACH row cites its proving test, Accepted Risks log (AR-14-01..05) signed by Kenneth Long 2026-06-07, `threats_open: 0`. |
| SC5 | A real MCP client completes the stdio handshake and round-trips ≥1 read tool and ≥1 edit→save tool | ✓ VERIFIED | `RoundTripTests.cs` uses the real SDK `McpClient` + `StdioClientTransport` against the built `Utinni.Mcp.exe`. Live: `Handshake_ListsExactlyTheElevenNamedTools` PASSED (exact 11-tool set), plus read + edit→save→read-back all PASSED. |

**Score:** 5/5 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
| --- | --- | --- | --- |
| `Utinni.Mcp/Utinni.Mcp.csproj` | net10 console, ModelContextProtocol 1.4.0 | ✓ VERIFIED | `net10.0`; `ModelContextProtocol 1.4.0` + `Microsoft.Extensions.Hosting/Logging.Console 10.0.0`; NuGet-legitimacy comment + lock file pin. |
| `Utinni.Mcp/Server/ResolvedRoot.cs` | fail-closed pin + per-call Resolve delegating to LooseOverridePath | ✓ VERIFIED | `PinOrThrow` + `Resolve → LooseOverridePath.Resolve`. |
| `Utinni.Mcp/Server/CliDispatcher.cs` | subprocess seam, injectable 60s timeout, async-both-streams, kill-on-timeout | ✓ VERIFIED | `ArgumentList` per-arg, stdin close, both reads before `WaitForExitAsync`, `Kill(entireProcessTree)` on cancel, `ExeMissing` without throw. |
| `Utinni.Mcp/Tools/ReadTools.cs` | 5 read tools | ✓ VERIFIED | 4 reads + `get_template_schema` (temp `--out` boundary). |
| `Utinni.Mcp/Tools/SaveTools.cs` | 4 save tools wrapping apply-save-* opaquely | ✓ VERIFIED | save_datatable/iff/stringtable/object_template; typed args; serialized per path. |
| `Utinni.Mcp/Tools/RepackTool.cs` | repack_tre, Destructive, dry_run default true | ✓ VERIFIED | host-side `if (dry_run)` gate; no spawn / no backup claim on dry run. |
| `Utinni.Mcp/Tools/VerifyTools.cs` | roundtrip_check verify-only | ✓ VERIFIED | non-persisting roundtrip-* dispatch. |
| `Utinni.Mcp/Tools/CliResultMapper.cs` | opaque envelope pass-through + shape validation + exit taxonomy | ✓ VERIFIED | schemaVersion+command+(result XOR error) shape gate; exit-code cross-check; hard McpException on transport/shape failures; verbatim structuredContent. |
| `Utinni.Cli/Commands/ApplySave*.cs` (4) | net-new persist verbs | ✓ VERIFIED | all 4 present, wired in `Program.cs:66-100`, golden-tested (33 tests pass). |
| `UtinniCoreDotNet.PathContainment/LooseOverridePath.cs` | netstandard2.0 single source | ✓ VERIFIED | `netstandard2.0` TFM; full containment logic. |
| `UtinniCoreDotNet/Saving/LooseOverridePath.cs` | TypeForwardedTo shim (NOT wrapper) | ✓ VERIFIED | `[assembly: TypeForwardedTo(...)]` — binary identity preserved for net472 plugins. |
| `MCP-SECURITY.md` | threat register | ✓ VERIFIED | see SC4. |
| `Utinni.Mcp.Tests/RoundTripTests.cs` + `Fixtures/` | real-client integration + committed binary fixtures | ✓ VERIFIED | 5 binary fixtures + README provenance; tests executed live and passed. |

### Key Link Verification

| From | To | Via | Status |
| --- | --- | --- | --- |
| `ResolvedRoot.Resolve` | `LooseOverridePath.Resolve` | delegate call | ✓ WIRED |
| `UtinniCoreDotNet/Saving/LooseOverridePath` | PathContainment type | `[TypeForwardedTo]` | ✓ WIRED |
| `ReadTools` | `cli.RunAsync` + mapper | resolve→dispatch→map | ✓ WIRED |
| `SaveTools` | `apply-save-*` verb | one `RunAsync(ApplyVerb(...))`, decide on exit code | ✓ WIRED |
| `RepackTool` | `repack-tre` verb | host-side `if (dry_run)` gate | ✓ WIRED |
| `ApplySaveTabCommand` | `LooseOverridePath.Resolve` + `DataTableWriter` + `WriteAtomic` | apply→serialize→verify→commit | ✓ WIRED |
| `Program.cs` | 4 ApplySave verbs | ParseArguments + Dispatch switch | ✓ WIRED |
| `RoundTripTests` | built `Utinni.Mcp.exe` | `StdioClientTransport` + `--cli-path` | ✓ WIRED (executed live) |

### Behavioral Spot-Checks (executed in this verification)

| Behavior | Command | Result | Status |
| --- | --- | --- | --- |
| net10 MCP fast lane | `dotnet test Utinni.Mcp.Tests --filter "Category!=Slow"` | 71/71 passed (54.6s) — incl. full RoundTripTests | ✓ PASS |
| apply-save persist verbs | `dotnet test Utinni.Cli.Tests --filter ~ApplySave` | 33/33 passed | ✓ PASS |
| Real stdio handshake + 11-tool surface | `RoundTripTests.Handshake_ListsExactlyTheElevenNamedTools` | passed | ✓ PASS |
| Edit→save→read-back, hash changed | `RoundTripTests.EditSaveRoundTrip_…` | passed | ✓ PASS |
| Verify-fail leaves file unchanged | `RoundTripTests.EditSaveVerifyFail_…` | passed | ✓ PASS |
| Path-escape: no file outside root | `RoundTripTests.PathEscapeAtBoundary_…` | passed | ✓ PASS |
| Repack dry-run no-write / real rewrite+backup | `RoundTripTests.RepackDryRun_…` | passed | ✓ PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
| --- | --- | --- | --- | --- |
| MCP-01 | 14-01, 14-02, 14-04 | Headless net10 MCP READ tools wrapping CLI verbs | ✓ SATISFIED | SC1, SC5 verified; ReadTools + RoundTripTests live. |
| MCP-02 | 14-01, 14-03a, 14-03, 14-04 | MCP write/SAVE, loose-override default, verify-before-commit, dry_run repack gate, fail-closed root, MCP-SECURITY.md | ✓ SATISFIED | SC2, SC3, SC4 verified; SaveTools + apply-save-* verbs + RepackTool + RoundTripTests live. |
| AUTH-05 | 14-03a (write surface completed) | utinni-cli SAVE verb writing an edited asset | ✓ SATISFIED | `apply-save-*` family is the net-new persist write surface; 33 golden tests pass with read-back. (REQUIREMENTS.md line 203 already records this completion location.) |

### Anti-Patterns Found

None. Scan of all phase-modified source (`Utinni.Mcp/`, `Utinni.Mcp.Tests/`, `Utinni.Cli/Commands/ApplySave*.cs`, `UtinniCoreDotNet.PathContainment/`) found zero `TBD/FIXME/XXX` debt markers and zero `TODO/HACK/PLACEHOLDER`/"not yet implemented" markers. All three new projects registered in `Utinni.sln`. The TypeForwardedTo shim is a genuine binary-identity forward, not a stub.

### Human Verification Required

None. This phase produces headless, fully automatable code with no UI/visual surface and no live-SWG-injection dependency. The SC5 "real MCP client round-trip" criterion — which would normally be a human/integration item — is covered by an automated real-`McpClient` stdio test that was executed live in this verification (not merely claimed). No planner-deferred `<human-check>` blocks were found in the phase plans.

### Gaps Summary

No gaps. All five ROADMAP success criteria are observably true in the codebase, all required artifacts exist/are substantive/are wired/flow real data, both requirements (MCP-01, MCP-02) plus AUTH-05's write-surface completion are satisfied, and the critical reviewer-flagged persistence gap (old `roundtrip→save` two-step that persisted PRE-mutation bytes) is genuinely closed by the `apply-save-*` verb family with read-back proof at both the CLI golden-test layer (33 tests) and the real-client integration layer (`RoundTripTests.EditSaveRoundTrip` with hash-changed assertion).

Notable strength: the verifier was able to run the SC5 integration suite end-to-end (both binaries pre-built), so SC1/SC2/SC3/SC5 are empirically confirmed in this run rather than trusted from SUMMARY.md.

One non-blocking observation (informational, not a gap): `RoundTripTests` is NOT marked `[Trait("Category","Slow")]`, so the CI net10 fast lane (`--filter "Category!=Slow"`) will attempt it on every push. It requires a pre-built net472 `utinni-cli.exe`; if a CI run reaches the net10 lane without that artifact present, the test throws `FileNotFoundException` (a hard fail, not a silent skip) rather than being excluded. This did not affect verification (both binaries were built and all tests passed) and is arguably the desired fail-loud behavior, but the CI lane ordering must guarantee `utinni-cli.exe` (Release/net472) is built before the net10 lane runs. Flagged for CI-hygiene awareness only.

---

_Verified: 2026-06-06_
_Verifier: Claude (gsd-verifier)_
