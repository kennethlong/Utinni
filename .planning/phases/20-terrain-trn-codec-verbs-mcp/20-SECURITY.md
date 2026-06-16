# SECURITY.md — Phase 20: terrain-trn-codec-verbs-mcp

**Audit date:** 2026-06-16
**ASVS Level:** 1
**Auditor:** gsd-security-auditor (claude-sonnet-4-6)
**Verdict:** SECURED — threats_open: 0
**Register origin:** authored at plan-time (4/4 PLAN.md `<threat_model>` blocks parseable)

---

## Security Audit 2026-06-16

| Metric | Count |
|--------|-------|
| Threats found | 14 |
| Closed | 14 |
| Open | 0 |

(13 `mitigate`-disposition threats verified against implementation; T-20-SC `accept` — documented accepted risk, zero new package manifests.)

---

## Threat Verification

| Threat ID | Category | Disposition | Status | Evidence |
|-----------|----------|-------------|--------|----------|
| T-20-01 | Tampering | mitigate | CLOSED | `TgenFixtureSynthesizer.cs:359` — `AssertAllFixturesWellFramed()` re-parses every emitted fixture via `IffReader.Read` and asserts `bytes.Length <= MaxFixtureBytes (200)`. Called in non-Skipped `[Fact]` at `TgenDecoderTests.cs:40-42`. |
| T-20-01b | Tampering/Info-disc | mitigate | CLOSED | `TreFile.cs:52` + `TreWriter.cs:115-127` — v6000+ payloads are `enumerate-only`; `TreWriter.Repack` throws `NotSupportedException` on `Header.EnumerateOnly`. Plan-01 Task 3 checkpoint recorded "concern #15 did not trigger" (neither client's .trn was v6000+/encrypted); `TgenEraVersions.cs:24-25` documents this observation. No decrypt-on-encrypted path exists. |
| T-20-02 | Tampering/DoS | mitigate | CLOSED | `IffPayloadCursor.cs:155-163` — `Need(n)` throws `DecoderException(Truncated)` on over-read. `TgenDecoder.cs:310-327` — `DecodeNode` catches `DecoderException` and falls back to `TerrainNode.RawPreserved` with `IsEditable=false`; consumed-length check at line 317-319 catches trailing bytes. `TgenDecoderTests.cs:323-331` — `Decode_TruncatedKnownTag_RawFallbackNonEditable_NoOverRead` asserts `IsRawPreserved` + `IsEditable=false`. |
| T-20-03 | Tampering | mitigate | CLOSED | `TgenDecoder.cs:297-306` — version read FIRST (`FindVersion`); unknown tag/unrecognized version returns `TerrainNode.RawPreserved` without throwing. `TgenDecoderTests.cs` (filter `TgenRawFallback`) — tests for unknown tag, unrecognized version, and unknown FourCC all green. |
| T-20-04 | Tampering | mitigate | CLOSED | `TgenDecoder.cs:67-70` — `DeadTags` HashSet (`BALL`, `BSPL`, `AHSM`, `AHBM`, `ACBM`, `ASBM`, `AFBM`). `TgenDecoder.cs:293-295` — DEAD tags return `TerrainNode.DeadSkipped` before any raw-fallback path. `TgenDecoder.cs:243` — stable-id ordinals increment for every child including DEAD/raw siblings. `TgenDecoderTests.cs` (filter `TgenRawFallback`) includes DEAD adjacency test. |
| T-20-04b | Tampering | mitigate | CLOSED | `TgenDecoder.cs:173-183` — single MGRP bound to Fractal slot and marked `ambiguous: true`; `TgenDecoderTests.cs:376-386` — `Decode_OnlyOneMgrp_DisambiguatesByLoadOrderNotTag_MarksAmbiguous` asserts `Fractal.Present=true`, `Fractal.Ambiguous=true`, `Bitmap.Present=false`. |
| T-20-05 | Tampering | mitigate | CLOSED | `ApplySaveTrnCommand.cs:102-107` — `LooseOverridePath.Resolve` call; `ArgumentException` caught → `exit 2` + no write. `LooseOverridePath.cs` (real impl at `UtinniCoreDotNet.PathContainment\LooseOverridePath.cs:81-169`) — throws `ArgumentException` on `..` traversal, rooted path, and canonicalization escape. `ApplySaveTrnTests.cs:258-263` — `ApplySave_PathOutsideRoot_FailClosed_NoWrite` asserts `exit=2` and `"PathContainment"` kind. Atomic write via `SaveCommandIo.WriteAtomic` at line 219 (only reached after clean verify). |
| T-20-06 | Tampering | mitigate | CLOSED | `TrnFieldEncoder.cs:131-133` — `result = (byte[])dataPayload.Clone(); Array.Copy(fieldBytes, 0, result, target.Offset, target.Width)` — ONLY the exact descriptor span is overwritten; all other bytes cloned verbatim. `TrnFieldEncoder.cs:154-157` — `float.IsNaN(f) || float.IsInfinity(f)` throws `ArgumentException`. `RoundtripTrnTests.cs:92` uses `WithTruncatedKnownTag`; exact-span + untouched-float-bits tests in `RoundtripTrnTests.cs` (filter `RoundtripTrn`). |
| T-20-07 | Tampering | mitigate | CLOSED | `ApplySaveTrnCommand.cs:149-154` — `TargetTypedNodeIsEditable` gate: returns `false` for raw/truncated/DEAD nodes → `JsonOutput.EmitError` exit 1, no write. `ApplySaveTrnTests.cs:241-253` — `ApplySave_NonEditableNode_Rejected_NoWrite` asserts `exit=1`, `"UsageError"`, and file byte-unchanged. |
| T-20-07b | Tampering | mitigate | CLOSED | `ApplySaveTrnCommand.cs:266-288` — `ResolveFieldContext` walks `leaf.Parent` → version FORM → tag FORM (grandparent), never reads the DATA leaf's own node for tag/version. `TrnFieldEncoder.cs:76` — `TgenFieldLayouts.For(tag, version)` — same table the decoder validated. `ApplySaveTrnTests.cs:119` — result asserts `tag="AHCN"`, `version="0000"` (parent-chain recovery confirmed). |
| T-20-08 | Tampering/Elevation | mitigate | CLOSED | `ReadTools.cs:124` — `string abs = root.Resolve(relativePath);` — throws on escape → SDK tool error (no decode). `TerrainReadToolTests.cs:157-` — path-escape test (Test 4) asserts tool error surfaces before any CLI spawn. `TerrainReadToolCliIntegrationTests.cs` also exercises path containment. |
| T-20-09 | Repudiation/Info-disc | mitigate | CLOSED | `ReadTools.cs:119-127` — `SummarizeTerrain` body references ONLY `root.Resolve`, `cli.RunAsync("decode-iff", ...)`, and `CliResultMapper.ToCallToolResult(r)`. No IFF/TGEN types present. `CliResultMapper.cs:138` — `bool isError = r.ExitCode != 0` — nonzero CLI exit propagates as `IsError=true`. `TerrainReadToolTests.cs:143` — `SummarizeTerrain_MalformedInput_NonzeroExit_SurfacesToolError_NotSuccessEnvelope` asserts `result.IsError=true`. |
| T-20-10 | Tampering | mitigate | CLOSED | `TgenEraVersions.cs:18-26` — explicitly documents "OBSERVED (Plan 01 Task 3, 2026-06-16) — grounded against real client assets"; records that neither client was v6000+/encrypted (concern #15 did not trigger). `20-04-SUMMARY.md` Task 3 section documents the checkpoint: pin ratified, matrix green on pinned versions, DEC-C3 declared closed. DEC-C3 close was gated on Task 3 confirmation (prerequisite, not post-hoc). |
| T-20-SC | Tampering | accept | CLOSED-accepted | No new package manifests added in Phase 20. `git log` over `*.csproj`/`packages.config`/`package.json` shows no Phase-20 commits touching package manifests — the three Phase-20 csproj edits (`9c9f1e4`, `2056b62`, `20-03`) added only `<Compile Include>` entries for new source files, no `<PackageReference>` additions. All SUMMARY.md files confirm "No new packages (threat T-20-SC: accept — pure managed, zero installs)." |

---

## Accepted Risks Log

| Risk ID | Description | Rationale | Accepted by |
|---------|-------------|-----------|-------------|
| T-20-SC | npm/pip/cargo installs | No package-manager installs this phase — pure managed, zero new external packages. Verified: no new PackageReference entries in any .csproj modified during Phase 20. | Plan threat model, all four SUMMARY.md confirmations |

---

## Unregistered Flags

None. All threat flags from SUMMARY.md `## Deviations` sections map to existing threat IDs or are architectural deviations (not security surface):

- AbiSurfaceTests ABI drift (ADDED=0, pre-existing regen churn) — no new attack surface; deferred per `deferred-items.md`.
- `TgenDecoder.cs` landing in Task-1 commit (build-ordering structural change) — no security impact.
- `TrnFieldEncoder.cs` added to csproj Compile Include — no security impact.
- apply-save-trn re-derives descriptor span for exact-span verify — strengthens T-20-06, not a gap.

---

## Notes

**T-20-01b (encrypted TRE):** The declared mitigation is "v6000+ reported enumerate-only; no decode on encrypted blob." The TRE layer pre-existed this phase and gates the behavior via `TreFile.Header.EnumerateOnly` + `TreWriter` rejection. During Phase 20's real-asset observation checkpoint, neither SWGEmu `patch_00.tre` nor SWG Infinity `mtg_planets.tre` were v6000+/encrypted — the encrypted-payload branch was documented as not triggering (no false green). The existing enumerate-only enforcement in the TRE layer is the operative control.

**T-20-07b (ResolveFieldContext):** The active-flag path bypasses `TgenFieldLayouts` for the IHDR (no descriptor entry per D-09 decision). However, `ApplySaveTrnCommand.cs:314` routes the IHDR active write THROUGH `TgenFieldLayouts.For(LayerHeaderTag, LayerHeaderVersion)` + `TrnFieldEncoder.EncodeField` (per WR-03 / the `2026-06-16` review fix commit `4d5fb28`). This preserves the single-source encoder for the active-flag write as well.
