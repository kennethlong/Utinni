---
phase: 14
reviewers: [codex, cursor]
reviewed_at: 2026-06-06
plans_reviewed: [14-01-PLAN.md, 14-02-PLAN.md, 14-03-PLAN.md, 14-04-PLAN.md]
self_skipped: claude (executing CLI — skipped for independence)
unavailable: [gemini, opencode, qwen, coderabbit]
verdict: BLOCKING — both reviewers independently flag 14-03 save composition as phase-blocking (MCP-02 not delivered as written)
---

# Cross-AI Plan Review — Phase 14

Two independent reviewers (Codex / ChatGPT-authed, Cursor-agent / headless) reviewed all four plans plus full project, roadmap, requirements, and decision context. Claude skipped as the executing CLI.

**Headline:** Both reviewers, working blind to each other, landed on the **same HIGH / phase-blocking finding**: the Plan 14-03 save two-step composition (`roundtrip-*` verify → `save` persist) **does not actually persist the typed edit**. `roundtrip-*` mutates and verifies in memory only and never writes; `save` then re-serializes the *unchanged* on-disk bytes. As written, MCP-02 and success criteria 2/5 cannot be met — and the planned tests would report green while edits are silently discarded.

---

## Codex Review

## Plan 14-01

### Summary

Strong foundation plan, but it carries two material risks: the cross-TFM `LooseOverridePath` extraction is underspecified for a legacy net472 solution, and the dispatcher/locator contract may not match how downstream integration tests will launch the built CLI. The plan is directionally correct for the separate net10 stdio host, but it needs sharper compatibility checks and less reliance on manual NuGet approval as a security control.

### Strengths

- Correctly keeps `Utinni.Mcp` as a separate net10 process using stdio only.
- Pins `resolvedRoot` fail-closed before MCP transport startup.
- Reuses the existing `LooseOverridePath.Resolve` containment model instead of creating a second path policy.
- Uses `ProcessStartInfo.ArgumentList`, avoiding shell quoting bugs.
- Adds explicit tests for path traversal, exe-missing, timeout, and CI separation from the x86 MSBuild lane.

### Concerns

- **HIGH:** `LooseOverridePath` type-forwarding is risky as described. If `UtinniCoreDotNet/Saving/LooseOverridePath.cs` is replaced with `[assembly: TypeForwardedTo]`, consumers must still receive and load `UtinniCoreDotNet.PathContainment.dll`. Existing net472 app/plugin probing may not copy the new DLL automatically, especially in WinForms/plugin deployment paths.
- **HIGH:** Preserving namespace alone does not preserve binary compatibility if existing compiled plugins reference `UtinniCoreDotNet, type UtinniCoreDotNet.Saving.LooseOverridePath`. Type forwarding can work, but only if assembly loading and deployment are verified. A “thin delegate” preserves source compatibility but not necessarily binary identity.
- **MEDIUM:** `PinOrThrow(string[] args)` parsing for `--root` / `--cli-path` is informal. Edge cases like `--root=` / missing value / duplicate flags / quoted paths / env fallback precedence should be specified and tested.
- **MEDIUM:** `CliLocator` fallback “return canonical name so dispatcher File.Exists surfaces not-found” conflicts with `File.Exists("utinni-cli.exe")`: relative lookup depends on current working directory, not `PATH`. If the intent is PATH probing, `File.Exists` prevents it.
- **MEDIUM:** Dispatcher timeout test using `%ComSpec% /c pause` with stdin closed may not hang; `pause` can terminate when stdin is closed. This could make the timeout test invalid.
- **MEDIUM:** A hardcoded 60s timeout may be too short for large TRE/IFF operations and too long for unit tests. The plan needs injectable timeout for tests and possibly configurable timeout for real usage.
- **LOW:** The package legitimacy gate is manual and non-reproducible. It does not pin package hash, lock file, or source mapping, so it is weak as a supply-chain mitigation.
- **LOW:** The plan says logging to stderr, but should explicitly test that no host logs ever reach stdout before MCP framing.

### Suggestions

- Add a packaging/deployment test proving net472 callers still load `LooseOverridePath` after extraction, including the actual editor/plugin output layout.
- Prefer keeping a wrapper class in `UtinniCoreDotNet` that delegates to the shared implementation unless binary forwarding is explicitly tested.
- Make root/CLI arg parsing a small tested parser with duplicate/missing-value behavior.
- Inject dispatcher timeout for tests, e.g. constructor overload with `TimeSpan`, while defaulting production to 60s.
- Use a deterministic long-running test child, such as PowerShell `Start-Sleep`, not `pause`.
- Add NuGet lock file / package source mapping / checked-in package metadata if the supply-chain gate is meant to be enforceable.

### Risk Assessment

**MEDIUM.** The architecture is right, but the shared-library extraction can break legacy consumers in ways unit tests may miss.

---

## Plan 14-02

### Summary

The read-tool plan mostly respects the thin-dispatcher constraint, but `get_template_schema` is a red flag: it appears to require temp-file handling and possibly post-processing outside the ordinary envelope pass-through path. The mapper taxonomy is good, but it needs stronger validation of “valid CLI envelope” versus merely “valid JSON.”

### Strengths

- Good decision to expose `decode_iff` as the single typed read surface instead of inventing per-format tools.
- Keeps read tools small: resolve path, dispatch CLI verb, map result.
- Separates hard transport/execution errors from in-band CLI domain errors.
- Explicitly treats MCP annotations as advisory rather than enforcement.
- Requires structured content plus text mirror, useful for real MCP clients.

### Concerns

- **HIGH:** `get_template_schema` likely violates the “zero business logic” boundary. The plan says the tool may surface schema file contents “if convenient,” but reading a temp output file and deciding what to return is extra host behavior. If the CLI does not already emit the schema envelope/content needed by agents, the CLI verb is not actually the complete dispatch target.
- **HIGH:** `compile-definition --out <temp> --skip-native` writes to host temp outside `resolvedRoot`. That is probably acceptable for read-only schema generation, but it is not described in the threat model as a writable path boundary.
- **MEDIUM:** `CliResultMapper` only says parse valid JSON. It should require the expected envelope shape: `schemaVersion`, `command`, and either `result` or `error`. Otherwise arbitrary stdout JSON could be treated as a valid tool result.
- **MEDIUM:** Non-zero exit codes other than `1/2/3` are unspecified. A child crash with JSON-looking stdout could become an in-band agent-correctable result accidentally.
- **MEDIUM:** Passing absolute resolved paths to read verbs may leak local filesystem layout in CLI envelopes. That may be acceptable, but should be acknowledged in `MCP-SECURITY.md`.
- **LOW:** The plan says read tools cover “any supported SWG asset,” but the listed tools do not obviously cover all save/build/read verbs from Phase 13. It relies heavily on `decode-iff` auto-dispatch and `parse-tre`.

### Suggestions

- Treat `get_template_schema` as wrapping a CLI command that must itself emit the schema JSON envelope. If that is not true today, add/adjust CLI behavior first, or explicitly document this exception.
- Add mapper tests for malformed envelope shape, unknown exit code, empty stdout, stderr pollution, and exit `0` with an `error` envelope.
- Define hard-error behavior for exit codes outside `0/1/2/3`.
- Add a test ensuring host logging never contaminates stdout MCP transport.
- Include temp-file cleanup and temp-output path threat handling for `get_template_schema`.

### Risk Assessment

**MEDIUM.** Basic reads are low-risk, but `get_template_schema` blurs the thin-host rule and may introduce untested write/temp behavior.

---

## Plan 14-03

### Summary

This is the weakest plan. The stated two-step save composition does not actually prove byte-exact verify-before-commit unless the roundtrip verb produces the exact bytes that the later `save` verb will persist. As written, it appears to verify one operation and then run a separate save operation that may re-read unchanged source input or apply no mutation at all. The plan also requires inspecting `bytesEqualUntouched`, which means the MCP host must parse CLI business output, contradicting the “pass envelope through unchanged” rule.

### Strengths

- Correctly keeps `.tre` repack separate from normal save tools.
- Correctly makes `repack_tre` dry-run gated on the host because the CLI verb has no `--dry-run`.
- Recognizes the need for typed structured write args.
- Includes tests for failed-verify short-circuiting and dry-run not spawning the destructive verb.
- Reuses existing `TreBackupPath` / `TreRepackLock` through the CLI repack verb.

### Concerns

- **HIGH:** The save flow is logically incomplete. `roundtrip-*` verifies a mutation, then `save <relativePath> --root <root>` persists. Unless `roundtrip-*` writes the mutated asset somewhere consumed by `save`, the edit is not actually passed to the save step.
- **HIGH:** “Byte-exact verify-before-commit” is not satisfied by verifying in one command and persisting in another independent command. The persisted bytes may differ from the verified bytes due to TOCTOU, file changes, serializer differences, or because the mutation was never applied to the persisted input.
- **HIGH:** The host must parse `bytesEqualUntouched` from the roundtrip envelope to decide whether to persist. That is business-result interpretation in the MCP layer. The earlier rule said the mapper passes envelopes opaquely and tools do not parse CLI output.
- **HIGH:** The plan says “Every capability is a golden-tested CLI verb FIRST” and “Phase 14 adds ZERO verbs,” but the requested `save_*` capabilities are not actually single CLI verbs. They are host-composed workflows with conditional logic.
- **HIGH:** The typed write interfaces are underspecified for `save_iff`, `save_object_template`, and `save_stringtable`. “Mirroring roundtrip-*” is not enough for a published MCP API; downstream clients need stable schemas.
- **MEDIUM:** The `save` command receives `relativePath`, while roundtrip receives `abs`. That is probably intentional, but the plan should assert exact CLI signatures and tests should catch accidental absolute path acceptance by `save`.
- **MEDIUM:** `roundtrip_check` marked `ReadOnly = false, Idempotent = true` because it writes temp data is honest, but it may confuse agents. It should be clearly named and documented as non-persisting.
- **MEDIUM:** `repack_tre dry_run=true` only returns a notice, not a real validation. It does not prove the archive is repackable, writable, unlocked, or supported. Calling it “validates without writing” is inaccurate unless it runs a non-destructive probe.
- **MEDIUM:** `dry_run=false` can still be reached by any client that sets a boolean. That is okay under the locked constraints, but the threat model should not overstate “off-by-default” as strong protection.
- **LOW:** `CliResultMapper.DryRunNotice(abs)` may leak absolute paths into agent-visible output.

### Suggestions

- Do not implement edit-save as `roundtrip-*` followed by unrelated `save` unless the roundtrip command outputs a verified edited artifact path/hash that `save` then commits.
- Better shape: CLI should provide a golden-tested `save-*` or `edit-save` verb that takes typed mutation args, writes to a temp file, verifies bytes, then atomically commits to loose override. MCP wraps that one verb. If Phase 14 truly adds zero CLI verbs, the plan cannot honestly meet MCP-02 as written.
- If composition remains, have `roundtrip-*` emit `{verifiedEditedPath, sha256, mutationApplied, bytesEqualUntouched}` and have `save` accept that verified artifact plus expected hash. Otherwise TOCTOU remains.
- Move `bytesEqualUntouched` interpretation into a CLI exit code: failed verify should be exit `2` with an error envelope, so MCP does not parse domain fields.
- Define exact write-tool schemas for each format, including allowed operations, value typing, and enum constraints.
- Make `repack_tre dry_run=true` either clearly “plan only, no validation” or add a CLI probe verb that checks lock/support/backup path without writing.

### Risk Assessment

**HIGH.** The core MCP-02 save guarantee is not proven and may not even apply the requested edit.

---

## Plan 14-04

### Summary

The closing plan has the right intent, especially the real MCP client handshake, but it is too optimistic about integration feasibility and too willing to downgrade live tests to a recorded transcript. It also has mismatches in expected tool counts and may assert outcomes that the previous save composition cannot deliver.

### Strengths

- Correctly requires a real MCP client over stdio rather than only unit testing tool methods.
- Tests tool discovery, read, decode, edit-save, and repack behavior end to end.
- Makes `MCP-SECURITY.md` a required deliverable, not a post-hoc note.
- Requires documentation of advisory MCP hints versus deterministic enforcement.
- Includes fixture copying into a temp root, which is the right shape for path containment tests.

### Concerns

- **HIGH:** The fallback to a recorded stdio transcript would not satisfy SC5. The success criterion requires a real MCP client completes handshake and round-trips one read plus one edit-save.
- **HIGH:** `EditSaveRoundTrip` depends on Plan 14-03’s questionable two-step save. If that flow does not pass a verified edited artifact into `save`, this test may either fail or pass while not proving the mutation was persisted.
- **HIGH:** `RepackDryRun` expects `dry_run=false` to rewrite a TRE fixture and create a backup in CI. That is destructive integration behavior in a test suite. It must use an isolated copy, but the plan should also account for nondeterministic archive byte changes, file locks, and backup path cleanup.
- **MEDIUM:** Tool count is inconsistent. The plan says assert “9 core read+write tools; tolerate 2 optional ones,” but Plans 02 and 03 define 5 read tools plus 6 write/verify tools = 11 tools.
- **MEDIUM:** The tests need a reliable way to locate both `Utinni.Mcp` and `utinni-cli.exe` across local `Debug`, `Release`, and CI x86 output layouts. The plan handwaves this.
- **MEDIUM:** The net10 test cannot reference net472 fixture builders, but committed binary fixtures can rot. There should be provenance and minimal fixture documentation.
- **MEDIUM:** `MCP-SECURITY.md` with `threats_open: 0` is too easy to turn into paperwork. The plan asks for file:line evidence, but not for tests linked to each mitigated threat.
- **LOW:** The verify command for `MCP-SECURITY.md` checks string counts only; it can pass with a hollow document.

### Suggestions

- Remove the recorded-transcript fallback from acceptance criteria. If live stdio is flaky, fix process launch/test isolation.
- Correct expected tool count and exact required tool names.
- Add assertions that the edited value is actually present after save by reading/decoding the loose-override output through MCP or CLI.
- For repack, use a copied fixture only and assert backup location is also under the expected backup policy.
- Add test trace capture for server stderr on failure, while keeping stdout reserved for MCP.
- Strengthen security doc validation by checking required threat IDs and required headings, not just counts.

### Risk Assessment

**MEDIUM-HIGH.** The real-client direction is good, but the integration tests may either be flaky or fail to prove the security/edit claims.

---

## Phase-Wide Review (Codex)

### Summary

The phase has a solid architectural premise: net10 MCP over stdio, thin dispatch to the existing net472/x86 CLI, pinned resolved root, and loose-override default. The biggest issue is that the write/save requirement is stronger than what the proposed host composition can safely prove. The plans repeatedly assert “zero business logic” while adding orchestration that interprets CLI envelopes, creates temp outputs, and conditionally persists. Either the CLI must own those semantics as golden-tested verbs, or the MCP layer is no longer just a dispatcher.

### Concerns (phase-level)

- **HIGH:** MCP-02 is not convincingly satisfied. The save tools must edit and save with byte-exact verify-before-commit, but the plan composes separate verbs without proving the verified edited bytes are what get committed.
- **HIGH:** The phase says “Phase 14 adds ZERO verbs” and “Every capability is a golden-tested CLI verb FIRST,” yet write capabilities appear to require new CLI-level semantics that do not exist.
- **HIGH:** The MCP host is not consistently “zero business logic.” It parses `bytesEqualUntouched`, manages temp schema outputs, chooses whether to persist, and manufactures dry-run responses.
- **HIGH:** The TOCTOU window between roundtrip verify and save is not addressed.
- **MEDIUM:** Cross-TFM extraction can destabilize net472 runtime deployment even if compilation succeeds.
- **MEDIUM:** Several tests prove strings or call order, not actual safety properties (e.g. `grep no repack-tre in SaveTools` does not prove `.tre` cannot reach destructive code if `save` redirects internally).
- **MEDIUM:** Absolute path leakage is not assessed.
- **MEDIUM:** CI push-only self-hosted runner means failures appear late; local validation must match CI output paths/platform tightly.
- **LOW:** The supply-chain gate is mostly procedural, not technical.

### Risk Assessment

**HIGH.** MCP-01 is likely achievable with moderate risk. MCP-02, as planned, has a core semantic gap around edit-save atomicity and byte-exact verification.

---

## Cursor Review

### Phase as a Whole — Summary

The four-wave structure is coherent: foundation → read surface → write surface → security doc + integration proof. The thin-dispatcher philosophy, stdio-only seam, `ResolvedRoot` fail-closed model, and CI lane split are well aligned with Phase 13 reality. However, **Plan 14-03’s core save composition cannot persist typed edits** with the CLI verbs as they exist today. `roundtrip-*` verifies in memory only; `save` re-serializes bytes already on disk with no mutation parameters. RESEARCH flagged this (Open Q1 / A1); the planner resolved it without reconciling `SaveCommand` behavior. Until that is fixed, MCP-02 and success criteria 2/5 are not achievable as written. Secondary gaps: weak edit-persistence assertions in `RoundTripTests`, fixture sourcing for net10 integration tests, and several seam edge cases (stdout pollution, pipe deadlock, envelope parsing in save orchestration).

### Strengths

- Honest two-process boundary (net10 MCP host → net472/x86 `utinni-cli`) matches locked constraints.
- Reuse of `LooseOverridePath`, `TreBackupPath`, `TreRepackLock`, and Phase-13 `NativeToolRunner` discipline (60s timeout, stdin-close, no shell) is correct.
- Wave ordering is sensible: `ResolvedRoot` + `CliDispatcher` contracts before tools; `CliResultMapper` before write tools.
- `repack_tre` as a separate, `dry_run=true`-default, host-gated tool matches `RepackTreCommand` (no `--dry-run`).
- `MCP-SECURITY.md` as a design-time deliverable with advisory-vs-enforcement caveat is the right posture.
- NuGet legitimacy gate (Task 0) before first external package install is prudent.

### Concerns (phase-level)

| Severity | Concern |
|----------|---------|
| **HIGH** | **Save two-step composition does not persist edits.** `roundtrip-tab` (and siblings) read disk → mutate in memory → verify → return envelope; they never write. `save` then loads from disk and re-serializes without mutation args (`sourcePath = destPath` in loose-override mode). Plan 14-03 Task 1 sequences `roundtrip-*` then `save [relativePath, --root, root.Path]` — the edit is verified but never committed. |
| **HIGH** | **`EditSaveRoundTrip` can pass while MCP-02 fails.** Plan 14-04 only asserts `{written, path, bytesWritten, validated}` and file existence — not that the mutated cell/value changed. A no-op re-serialize satisfies those assertions. |
| **HIGH** | **RESEARCH Open Q1 was “resolved” without fixing the verb gap.** 14-RESEARCH.md line 288 explicitly flagged whether a net-new CLI surface is needed; 14-03 adopts interpretation 2a anyway. |
| **MEDIUM** | **Fixture strategy is underspecified.** Plan 14-04 says “copy bytes from `Utinni.Cli.Tests` fixtures,” but `.tre`/`.tab` binaries are mostly runtime-generated via `TreFixtureBuilder` / `IffBuilder`, not committed files. `get_template_schema` needs a `.tdf` fixture; none listed. |
| **MEDIUM** | **`SaveCompositionTests` likely won’t catch the persistence bug** — sequencing mocks proving “failed verify does not call save,” not end-to-end byte verification that the edit landed on disk. |
| **MEDIUM** | **Cross-TFM `LooseOverridePath` extraction risk.** Plan allows “type-forward OR thin re-export.” Wrong choice breaks type identity for net472 callers. `LooseOverridePathTests` must be an explicit gate after extraction; 14-01 doesn’t require it (14-04 does). |
| **MEDIUM** | **Save orchestration requires parsing CLI JSON** (`bytesEqualUntouched`) — contradicts 14-02’s “never parse CLI output in tools” rule. Acceptable for orchestration, but boundary is fuzzy and untested for malformed/partial envelopes. |
| **MEDIUM** | **SC1 “read any supported asset” is not integration-proven.** `RoundTripTests` covers `read_tre` + `decode_iff` only. |
| **MEDIUM** | **CI net10 lane** doesn’t pin `utinni-cli.exe` path for `RoundTripTests` (relies on prior MSBuild step + test discovery). |
| **LOW** | **60s timeout unit test** (`cmd /c pause`) adds ~60s to every CI run if not `[Trait("Category","Slow")]`-gated. |
| **LOW** | **Inherited stdout pipe deadlock risk.** `ReadToEndAsync` then `WaitForExit` — large stdout can deadlock. |
| **LOW** | **No concurrent-dispatch policy.** Parallel MCP tool calls could race on the same asset path. |
| **LOW** | **`DryRunNotice`** cannot report real `backupPath` without spawning — advisory only; must not be documented as proof of backup behavior. |

### Suggestions (phase-level)

1. **Block 14-03 until the persist path is designed.** Options: add mutation flags to `save` (mirror `roundtrip-*`); or `roundtrip-* --out <path>` + `save <temp> --path <resolved>`; or a single `apply-save-*` verb (verify+persist); or re-scope MCP-02 to re-serialize-only (drops the “typed edit” promise).
2. **Strengthen `EditSaveRoundTrip`:** after `save_datatable`, call `decode_iff` and assert the edited cell/value; compare file hash before/after.
3. **Commit minimal binary fixtures** under `Utinni.Mcp.Tests/Fixtures/` (`.tre`, `.tab`/DTII `.iff`, `.stf`, `.tdf`) with provenance notes — don’t assume `Utinni.Cli.Tests` has copyable binaries.
4. **Mandate `[assembly: TypeForwardedTo]`** in 14-01 Task 1 — disallow “thin re-export.”
5. **Add explicit regression** in 14-01 verification: `dotnet test UtinniCoreDotNet.Tests --filter LooseOverridePathTests` after extraction.
6. **Gate timeout test** behind a slow trait; run only on `workflow_dispatch`/nightly.
7. **Document save-tool envelope parsing** in `MCP-SECURITY.md` as host orchestration (layer 4), distinct from read pass-through.

### Codebase evidence cited by Cursor

```110:111:Utinni.Cli/Commands/SaveCommand.cs
                sourcePath = destPath; // re-serialize the existing loose asset in place
```

```136:168:Utinni.Cli/Commands/RoundtripTabCommand.cs
                byte[] roundtrippedBytes = new DataTableWriter(mutDoc).Serialize();
                // ... comparison only ...
                return JsonOutput.EmitSuccess("roundtrip-tab", result);
```

No `File.WriteAllBytes` in the roundtrip path. (Cursor's evidence should be verified against current source before acting — see Consensus actions.)

### Per-plan risk (Cursor)

- **14-01:** MEDIUM (HIGH if type-forward implemented wrong). Type-forward vs re-export ambiguity; no `LooseOverridePathTests` regression gate; `PinOrThrow` on a file/symlink root untested; timeout test slow.
- **14-02:** LOW–MEDIUM. `get_template_schema` temp `--out` outside root; no test that read tools resolve paths before dispatch; assert semantic (not string) envelope equality.
- **14-03:** HIGH (phase-blocking). Edit persistence impossible; "verify-before-commit" mislabeled (semantic no-op, not even TOCTOU); `save_datatable`→`roundtrip-tab` naming vs DTII `.iff`.
- **14-04:** MEDIUM (HIGH if 14-03 ships unchanged — tests may mask failure). `EditSaveRoundTrip` assertion gap; fixture reality; `RepackDryRun` needs a non-V6000 v0006 archive; shallow security-doc verify; recorded-transcript fallback would not satisfy SC5 literally.

### Bottom Line (Cursor)

**Do not execute Plan 14-03 Task 1 as written.** Resolve the roundtrip→save persistence gap first (almost certainly a small CLI extension). Strengthen 14-04 `EditSaveRoundTrip` to assert post-save content. The two-step narrative *sounds* like verify-before-commit while the actual CLI verbs implement verify-then-**re-serialize-unchanged** — the blind spot a careless reader misses.

### Cursor Risk Assessment: **HIGH** (phase-blocking)

---

## Consensus Summary

### Agreed Strengths (both reviewers)

- The net10 / stdio / separate-process seam is correct and matches the locked anti-pattern (no in-proc SDK in SWG.exe).
- Reuse of `LooseOverridePath`, `TreBackupPath`/`TreRepackLock`, and Phase-13 `NativeToolRunner` discipline is the right call.
- Wave ordering (foundation → read → write → security/integration) is sensible; contracts precede consumers.
- `repack_tre` as a separate, `dry_run=true`-default, host-gated Destructive tool correctly matches the verb having no `--dry-run` flag.
- `MCP-SECURITY.md` as a design-time deliverable with the advisory-not-enforcement caveat is the right posture.
- Real-`McpClient`-over-stdio integration test (not mocked JSON-RPC) is the correct SC5 proof harness.

### Agreed Concerns (raised by BOTH — highest priority)

1. **[PHASE-BLOCKING] 14-03 save composition does not persist the edit.** `roundtrip-*` verifies in memory and never writes; `save` re-serializes unchanged on-disk bytes. "Byte-exact verify-before-commit" is mislabeled — the committed bytes are the *pre-mutation* file. MCP-02 + SC2/SC5 fail as written. Both reviewers independently reached this; Cursor cites `SaveCommand.cs:110-111` (`sourcePath = destPath`) and `RoundtripTabCommand.cs:136-168` (compare-only, no write).
2. **`EditSaveRoundTrip` / `SaveCompositionTests` would pass while the edit is silently discarded.** Asserting `{written,path,bytesWritten,validated}` + file existence is satisfied by a no-op re-serialize. Both demand a read-back assertion of the actual mutated value (decode after save, compare hash).
3. **Host parsing `bytesEqualUntouched` contradicts the "zero business logic / opaque pass-through" mandate.** Both flag the save orchestration interpreting CLI envelope fields. Both recommend moving the verify→persist decision into a CLI exit code (failed verify = exit 2 + error envelope) so the host never parses domain fields.
4. **"Zero new verbs" is in tension with MCP-02.** Both conclude the typed-edit-then-persist capability is not a single golden-tested verb today; either a CLI verb must own it (verify+commit atomically, likely emitting `{verifiedEditedPath, sha256}`), or MCP-02 must be re-scoped.
5. **Cross-TFM `LooseOverridePath` extraction risk.** Both flag "type-forward OR thin re-export" as underspecified. Re-export creates a distinct type and breaks binary identity for compiled net472 plugins; deployment must also copy the new `UtinniCoreDotNet.PathContainment.dll`. Both: mandate `[assembly: TypeForwardedTo]` (not a wrapper) AND add a `LooseOverridePathTests` regression gate to 14-01's verification (currently only in 14-04). Codex additionally wants a deployment/runtime load test against the actual plugin output layout.
6. **`get_template_schema` blurs the thin-host boundary.** Both: it writes to host temp via `--out` outside `resolvedRoot` and may post-process the temp file. Document the temp-write boundary in `MCP-SECURITY.md`; ideally the CLI verb emits the schema envelope directly.
7. **`CliResultMapper` should validate envelope SHAPE, not just "valid JSON."** Both: require `schemaVersion` + `command` + (`result` xor `error`); define hard-error behavior for exit codes outside `0/1/2/3`; add tests for malformed envelope, empty stdout, stderr pollution, exit-0-with-error-envelope.
8. **14-04 fixture sourcing is unreal.** Both: net10 tests can't reference the net472 `TreFixtureBuilder`/`IffBuilder`, and those fixtures are runtime-generated, not committed binaries. Commit minimal binary fixtures with provenance; `get_template_schema` needs a `.tdf` fixture not currently listed.
9. **`RepackDryRun` `dry_run=false` is destructive in CI.** Both: must operate on an isolated copy; needs a supported v0006 archive (not a V6000/encrypted fixture); account for nondeterministic bytes, file locks, backup cleanup.
10. **`MCP-SECURITY.md` automated verify is shallow.** Both: counting `T-14-`/`advisory` strings can pass a hollow doc. Require each threat row to cite the *test* proving the mitigation, plus required headings — not just string counts.
11. **CLI-locator / artifact-path fragility.** Both: `RoundTripTests` and `CliDispatcher` need a robust way to find `utinni-cli.exe` across Debug/Release/CI x86 layouts; Codex flags `File.Exists(canonical-name)` won't PATH-probe.
12. **Absolute-path leakage.** Both: resolved absolute paths flow into CLI envelopes and `DryRunNotice` — acknowledge in `MCP-SECURITY.md` as accepted info-disclosure.

### Divergent / Unique Views (worth investigating)

- **Codex only — the timeout test stub is likely INVALID, not just slow.** `%ComSpec% /c pause` with stdin closed may *terminate immediately* rather than hang, so `DispatcherTests` timeout case may never exercise the 60s path. Codex recommends a deterministic long-runner (PowerShell `Start-Sleep`). (Cursor independently flagged the same test as *slow* — together: replace the stub AND gate it behind a slow trait. This directly contradicts 14-01 Task 2's chosen `%ComSpec% /c pause` stub.)
- **Codex only — make the dispatcher timeout injectable** (constructor `TimeSpan` overload) so tests don't wait 60s and production can tune for large TRE/IFF ops.
- **Codex only — `PinOrThrow` arg-parsing is informal:** specify/test `--root=`, missing value, duplicate flags, quoted paths, env-fallback precedence.
- **Codex only — supply-chain gate is procedural, not technical:** add a NuGet lock file / package source mapping / checked-in hash if it's meant to be enforceable.
- **Cursor only — `save_datatable` → `roundtrip-tab` naming confusion:** client datatables are often DTII `.iff`, not `.tab`; works on bytes but misleads agents/docs.
- **Cursor only — stdout pipe deadlock** in the inherited `ReadToEndAsync`-then-`WaitForExit` ordering on large stdout.
- **Cursor only — no concurrent-dispatch policy** (parallel tool calls racing on the same asset path).
- **Cursor only — add an MCP-boundary path-escape integration test** (`save_datatable` with `relativePath: "../../outside.tab"` → hard error, no save spawn); SC3 is currently unit-only, not proven at the MCP tool boundary.

### Recommended Action

Both reviewers return **HIGH / phase-blocking** with the same root cause. Before executing Wave 3:

1. **Verify the persistence gap against current source** — confirm `SaveCommand` loose-override mode re-serializes unchanged (`sourcePath = destPath`) and that no `roundtrip-*` verb writes the mutated bytes. (Both reviewers assert this; Cursor cites line numbers. Confirm before re-planning.)
2. If confirmed, **resolve the save persist path** — the cleanest option is a golden-tested CLI verb that takes typed mutation args, applies them, verifies byte-identity on untouched data, and atomically commits to loose-override in one operation (MCP wraps that single verb), OR `roundtrip-* --out <verified-artifact>` + `save` consuming that artifact + expected hash. This reconciles "zero business logic" with MCP-02. Acknowledge it is a small CLI extension (the "ZERO verbs" claim needs a scoped exception).
3. **Re-plan 14-03 and the 14-04 assertions** accordingly; fold the agreed-concern fixes (#2–#12) into the affected plans.
4. Feed this file back into planning: `/gsd:plan-phase 14 --reviews`.

Plans 14-01 and 14-02 are largely sound (MEDIUM risk) and can proceed with the type-forward + envelope-shape-validation + timeout-stub fixes folded in. 14-04 is sound scaffolding but cannot salvage an unfixed 14-03.
