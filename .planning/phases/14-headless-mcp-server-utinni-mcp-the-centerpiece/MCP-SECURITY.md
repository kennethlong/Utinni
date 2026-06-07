---
phase: 14
slug: headless-mcp-server-utinni-mcp-the-centerpiece
status: draft
threats_open: 0
asvs_level: 1
created: 2026-06-07
---

# Phase 14 — Security

> Per-phase security contract for the headless `Utinni.Mcp` server (net10, stdio) — the AI-drivable
> centerpiece. This register is a FIRST-CLASS, design-time deliverable (the security contract is
> un-retrofittable once agents depend on the tools). It consolidates every `T-14-NN` declared across
> Plans 14-01 / 14-02 / 14-03 / 14-03a, mirroring `07-SECURITY.md` section-for-section. EACH threat
> row carries BOTH the file:line evidence in the shipped code AND the specific TEST that proves the
> mitigation (Consensus #10 — the verify is NOT a bare string count).
>
> Architecture under audit: a net10 host shells the net472/x86 `utinni-cli.exe` over a child process
> (the "two-process honest seam") — the MCP SDK is NEVER hosted in-proc in the x86 client. Agents speak
> JSON-RPC over stdio; the host resolves every relative asset path through a fail-closed pinned root,
> dispatches ONE CLI verb, and passes the verb's sorted-key JSON envelope through OPAQUELY.

---

## The 5-Layer Save Model (and what is advisory vs. enforcement)

A write/save flows through five layers. **Layers 1–2 are ADVISORY (UX hints only — NOT a security
boundary); layers 3–5 are the DETERMINISTIC enforcement.** A headless agent can auto-accept any
prompt, so the contract must hold even when layers 1–2 are ignored.

| # | Layer | Kind | What it does | Enforced where |
|---|-------|------|--------------|----------------|
| 1 | Tool annotations (`Destructive` / `ReadOnly` / `Idempotent`) | **advisory** | Hints a host UI MAY surface. A headless agent may ignore them. | `SaveTools.cs` / `RepackTool.cs` attribute args (T-14-02) |
| 2 | Elicitation / confirmation prompt | **advisory** | A prompt a host MAY show before a destructive call. Auto-acceptable. | not relied upon (T-14-09) |
| 3 | Loose-override default + fail-closed root | enforcement | Writes land in the loose-override tier under a pinned, contained root; path-escape throws BEFORE any spawn. | `ResolvedRoot.cs:60,105` + `LooseOverridePath.cs:81` (T-14-01, T-14-04) |
| 4 | **Save-tool HOST ORCHESTRATION + verify-before-commit** | enforcement | The host wraps the SINGLE `apply-save-*` verb in ONE spawn and decides persist-vs-fail on the EXIT CODE alone; the verb verifies byte-identity on the untouched region in-process BEFORE WriteAtomic (no TOCTOU). | `SaveTools.cs:99` host orchestration + `ApplySaveTabCommand.cs:184,200` verb (T-14-08, T-14-15) |
| 5 | Backup / recovery (destructive repack) | enforcement | `repack_tre` (off by default) takes a timestamped backup + refuses an in-use or V6000/encrypted archive. | `RepackTreCommand.cs:74,88` (T-14-03) |

### Layer 4 detail — the SAVE-TOOL HOST ORCHESTRATION (distinct from the read pass-through)

The READ tools are a thin pass-through: `root.Resolve -> cli.RunAsync(verb) -> CliResultMapper`
(`ReadTools.cs:67`). The SAVE tools add a DISTINCT host layer: they translate a TYPED mutation into
the `apply-save-*` argv (`SaveVerb.cs:92`), run exactly ONE spawn under a per-resolved-path lock
(`SaveTools.cs:99`, `SaveVerb.cs:186`), and decide persist-vs-fail on the verb's EXIT CODE — never by
parsing `bytesEqualUntouched` or any other domain field (`CliResultMapper.cs:107`). The verify (untouched-region
byte-identity) and the commit (WriteAtomic of the SAME verified bytes) happen INSIDE the single verb
process, so there is no TOCTOU window between verify and write.

### Scoped ZERO-VERBS exception — the `apply-save-*` CLI family

Phase 14's guard-rail was "the MCP host adds ZERO new CLI verbs — it is a thin dispatcher over the
Phase-13 surface." A cross-AI `--reviews` pass surfaced a BLOCKING gap: the old `save` verb
re-serialized the UNCHANGED on-disk file and `roundtrip-*` verified-then-DISCARDED the mutated bytes,
so no two-step host orchestration could actually PERSIST an edit. The scoped, documented exception
(Plan 14-03a) added the `apply-save-tab` / `apply-save-iff` / `apply-save-stf` / `apply-save-ot` verb
family — each FUSES (roundtrip apply+verify) with (WriteAtomic commit of the SAME verified bytes) in
ONE process. The exception is deliberately narrow and keeps the architecture intact:

- The verbs are **golden-tested-first in `Utinni.Cli`** (37 apply-save + dispatch tests), so the write
  logic is proven in the CLI, not the MCP host.
- The MCP host stays a **thin dispatcher** — each `save_*` tool wraps exactly ONE `apply-save-*` verb
  and decides on the exit code; the net10 / stdio / separate-process seam is unchanged.
- No other new verbs were added.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| agent → stdio → `Utinni.Mcp` host | The JSON-RPC handshake + tool calls; stdout is RESERVED for MCP framing (all host logs go to stderr — `Program.cs:51`). | JSON-RPC framing |
| host → `Process.Start` → net472/x86 `utinni-cli.exe` | The two-process honest seam; `UseShellExecute=false` + per-arg `ArgumentList` (no shell). | per-arg argv + stdout/stderr |
| agent relative path → `ResolvedRoot.Resolve` | Every agent-supplied path is RELATIVE and resolved under the pinned root; rooted/`..`/canonicalization/prefix-attack escapes throw before any spawn. | relative filesystem path |
| CLI stdout → `CliResultMapper` | The verb's sorted-key JSON envelope; shape-validated (schemaVersion + command + result XOR error) + exit-code cross-checked, then passed through verbatim. | JSON envelope |
| `get_template_schema` → host-temp `--out` | A writable boundary OUTSIDE the resolved root (`Path.GetTempPath`), IDisposable-cleaned; the host parses NOTHING from it. | temp artifact path |
| NuGet restore → build | `ModelContextProtocol 1.4.0` + Hosting/Logging; pinned via a committed lock file. | package bytes |
| committed fixture bytes → temp root → tools (TEST-only) | The 14-04 RoundTripTests copy committed fixtures into a temp resolved-root; no shipping code reads them. | committed binary |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation (file:line evidence) | Proving Test | Status |
|-----------|----------|-----------|-------------|---------------------------------|--------------|--------|
| T-14-01 | Elevation/Tampering | path resolution (read + save + repack) | mitigate | `ResolvedRoot.Resolve` delegates to `LooseOverridePath.Resolve` (rooted/`..`/canonicalization/prefix defenses) — `ResolvedRoot.cs:105`, `LooseOverridePath.cs:81,133`; save/repack resolve BEFORE any spawn (no CLI on escape) — `SaveTools.cs:90`, `RepackTool.cs:74` | `ResolvedRootTests` + `McpBoundaryPathEscapeTests` + `RoundTripTests.PathEscapeAtBoundary` | closed |
| T-14-02 | Spoofing/Tampering | `ReadOnly`/`Destructive`/`Idempotent` annotations | accept | Annotations are ADVISORY UX hints, NOT enforcement — layers 3–5 enforce; documented in the 5-layer model + Accepted Risks. `SaveTools.cs:69`, `RepackTool.cs:59` | (advisory — enforcement proven by T-14-03/08/16 tests) | closed |
| T-14-03 | Tampering/DoS | `repack_tre` destructive overwrite | mitigate | Distinct off-by-default tool; host-side `dry_run=true` plan-only gate (no spawn, no backup claim) — `RepackTool.cs:78`; the verb takes a timestamped backup + refuses in-use + refuses V6000/encrypted — `RepackTreCommand.cs:74,88`, `TreWriter.Repack` | `McpBoundaryPathEscapeTests` (dry-run no-spawn) + `RoundTripTests.RepackDryRun` (dry no-write / real rewrite + backup on an isolated copy) | closed |
| T-14-04 | Elevation | `ResolvedRoot.PinOrThrow` (fail-closed) | mitigate | No `--root`/`UTINNI_MCP_ROOT` or a non-existent dir → server REFUSES to start (throws before the transport opens); no absolute path ever accepted from the agent — `ResolvedRoot.cs:60` | `ResolvedRootTests` + `ServerArgsTests` (precedence + fail-closed) | closed |
| T-14-05 | DoS | `CliDispatcher` (hung/slow child) | mitigate | Injectable timeout (default 60s) + `WaitForExitAsync` + `Kill(true)` whole tree on timeout; both streams read async (no pipe deadlock); exe-missing without throw — `CliDispatcher.cs:59,95`; mapper turns TimedOut into a hard error — `CliResultMapper.cs` | `DispatcherTests` (timeout-kill / large-stdout / exe-missing) | closed |
| T-14-06 | Tampering | argv injection into the CLI | mitigate | `UseShellExecute=false` (no shell, no metacharacters) + per-arg `ProcessStartInfo.ArgumentList` (no concatenation, no manual quoting) — `CliDispatcher.cs:95,104` | `DispatcherTests` + `SaveCompositionTests` (typed argv asserted per spawn) | closed |
| T-14-07 | Tampering | `CliResultMapper` stdout interpretation | mitigate | Envelope parsed as opaque JSON, SHAPE-validated (schemaVersion + command + result XOR error) + exit-code cross-check; malformed/empty/out-of-range/exit-0-with-error/non-zero-with-result → HARD `McpException` (no field rewrite) — `CliResultMapper.cs:89,99,104,107` | `CliResultMapperTests` (18 taxonomy facts incl. deep-equality pass-through) | closed |
| T-14-08 | Tampering | save without verify | mitigate | The `apply-save-*` verb verifies byte-identity on the UNTOUCHED region BEFORE WriteAtomic IN ONE PROCESS (no TOCTOU); failed verify → exit 2 + NO write — `ApplySaveTabCommand.cs:184,192,200`; the host decides on exit code only — `SaveTools.cs:99`, `CliResultMapper.cs:107` | `ApplySaveTabCommandTests.FailedUntouchedVerify_…` + `RoundTripTests.EditSaveVerifyFail` | closed |
| T-14-09 | Tampering | elicitation treated as a gate | accept | Elicitation is ADVISORY and may be auto-accepted by a headless agent; the real gate is `dry_run`-default + the verb's backup, NEVER the prompt. Documented in the 5-layer model + Accepted Risks. | (advisory — enforcement proven by T-14-03 tests) | closed |
| T-14-10 | Information Disclosure | RoundTripTests temp-root fixtures | accept | Test-only temp dirs, recursively cleaned on `DisposeAsync`; no shipping code reads them — `RoundTripTests.cs` (`_tempRoot`, `TryDeleteDir`). Logged in Accepted Risks. | `RoundTripTests` (IAsyncLifetime cleanup) | closed |
| T-14-11 | Tampering | `LooseOverridePath` cross-TFM move | mitigate | `[TypeForwardedTo]` preserves binary type identity for compiled net472 plugins (re-export forbidden, Consensus #5) — `UtinniCoreDotNet/Saving/LooseOverridePath.cs` shim → `UtinniCoreDotNet.PathContainment` | CI `LooseOverridePathTests` (net472 binary-forward regression gate) | closed |
| T-14-12 | Information Disclosure | resolved absolute paths in CLI envelopes / DryRunNotice | accept | Resolved absolute paths flow into the verbatim CLI envelopes (`path`, `schemaPath`) and the DryRunNotice; accepted info-disclosure to the TRUSTED LOCAL agent (single-user desktop tool). Logged in Accepted Risks. `CliResultMapper.cs`, `RepackTool.cs:80` | (accepted — surfaced by `RoundTripTests` structured envelopes) | closed |
| T-14-13 | Tampering | `get_template_schema` host-temp `--out` boundary | mitigate | Temp file under `Path.GetTempPath()` (OUTSIDE resolvedRoot), IDisposable cleanup on every outcome; `--skip-native` (no compiler run); host parses NOTHING from it — `TempSchemaOutput.cs:59,79`, `ReadTools.cs:124` | `CliResultMapperTests` + `RoundTripTests` (`get_template_schema` in the 11-tool surface) | closed |
| T-14-14 | Tampering | `apply-save-*` reaching destructive repack | mitigate | A `.tre` input to an apply-save verb is REJECTED with a usage error pointing at `repack-tre` (exit 1); apply-save writes only loose-override files, never repacks — `ApplySaveTabCommand.cs:141,145` | `ApplySaveTabCommandTests.TreInput_ExitsOne_PointsAtRepackTre` | closed |
| T-14-15 | Tampering | persisted bytes ≠ verified bytes | mitigate | The verb commits the SAME in-memory mutated bytes it verified (no re-load, no re-serialize between verify and commit) — `ApplySaveTabCommand.cs:189,200` | `ApplySaveTabCommandTests.MutateCell_PersistsEdit_ReadBack…` + **`RoundTripTests.EditSaveRoundTrip` (read-back + hash-changed centerpiece)** | closed |
| T-14-16 | Tampering | concurrent same-path writes | mitigate | Per-resolved-path host-side serialization (`SemaphoreSlim`-per-path, case-insensitive key); same-asset writes serialized, different assets parallel — `SaveVerb.cs:178,186`, consumed by `SaveTools.cs:99` + `RepackTool.cs:84` | `SaveCompositionTests` (same-path MaxConcurrent==1 / different-path ==2) | closed |
| T-14-17 | Tampering/DoS | RepackDryRun real-write in CI | mitigate | `dry_run=false` runs ONLY on an ISOLATED COPY of a supported non-encrypted v0006 archive; backup asserted under the `TreBackupPath` policy + cleaned up; never touches a committed fixture in place — `RoundTripTests.cs` (`RepackDryRun` isolated-copy + `BackupSiblings`) | `RoundTripTests.RepackDryRun` | closed |
| T-14-SC | Tampering | NuGet supply-chain (`ModelContextProtocol 1.4.0`) | mitigate | Plan 01 Task 0 blocking-human checkpoint confirmed id+version+publisher against nuget.org; committed NuGet lock file (`RestorePackagesWithLockFile`) pins the approved versions — `Utinni.Mcp/Utinni.Mcp.csproj`, `packages.lock.json` | CI `dotnet build`/`restore` with `--locked-mode` semantics (lock-file pin) | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-14-01 | T-14-02 | Tool annotations (`Destructive`/`ReadOnly`/`Idempotent`) are ADVISORY MCP hints, not a security boundary; a headless agent may ignore them. Enforcement is the deterministic layers 3–5 (fail-closed root, verify-before-commit, dry-run-default + backup). | Kenneth Long | 2026-06-07 |
| AR-14-02 | T-14-09 | Elicitation / confirmation prompts are advisory and auto-acceptable by a headless agent; the real destructive-write gate is `dry_run`-default + the verb's backup/lock/refuse-encrypted, never the prompt. | Kenneth Long | 2026-06-07 |
| AR-14-03 | T-14-04 | `UTINNI_MCP_ROOT` (or `--root`) is a PATH, not a secret — it pins the access-control boundary; it is not credential material and is safe in process args / env. The fail-closed pin means a misconfiguration refuses to start rather than serving an unbounded root. | Kenneth Long | 2026-06-07 |
| AR-14-04 | T-14-12 | Resolved absolute paths leak into the verbatim CLI envelopes (`path`, `schemaPath`) and the DryRunNotice. Accepted info-disclosure to the TRUSTED LOCAL agent of a single-user desktop modding tool; no remote/multi-tenant surface. | Kenneth Long | 2026-06-07 |
| AR-14-05 | T-14-10 | The 14-04 RoundTripTests create test-only temp resolved-roots populated from committed fixtures and recursively clean them on Dispose; no shipping code reads those temp dirs. | Kenneth Long | 2026-06-07 |

*Accepted risks do not resurface in future audit runs.*

---

## Unregistered Flags

The `## Threat Flags` sections of 14-01/14-02/14-03/14-03a all report "None" — no new attack surface was
declared during implementation beyond the modeled apply-save + repack write paths and the documented
`get_template_schema` temp boundary. The 14-04 RoundTripTests add only TEST-only temp surface (T-14-10,
accepted). No unregistered flags.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-07 | 18 | 18 | 0 | gsd-plan-executor (Claude Opus 4.8) — design-time authoring |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Every `mitigate` threat carries file:line evidence AND a cited proving test
- [x] Accepted risks documented in the Accepted Risks Log
- [x] 5-layer model encoded with the advisory (layers 1–2) vs. enforcement (layers 3–5) caveat
- [x] Save-tool host orchestration documented as a distinct layer 4 (vs. the read pass-through)
- [x] The scoped `apply-save-*` ZERO-verbs exception named and justified
- [x] `threats_open: 0` confirmed

**Approval:** draft authored 2026-06-07 (design-time deliverable; phase verification confirms the cited tests pass)
