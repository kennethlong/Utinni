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
| AR-16-01 | T-16-AR16 | The live bridge is a SINGLE-USER, LOCAL desktop surface: a local-only named pipe (current-user `PipeSecurity` ACL, server-side, 16-02) plus the in-client start gate (`[Live] enableLiveBridge`, 16-02 Task 3) plus the MCP-side `--enable-live` tool gate. `--enable-live` / `UTINNI_MCP_ENABLE_LIVE` / the pipe name are IDENTIFIERS, not secrets (analog of AR-14-03). The dual-flag operator contract + the root-reconciliation requirement (below) are documented; no remote / multi-tenant surface. | Kenneth Long | 2026-06-14 |

*Accepted risks do not resurface in future audit runs.*

---

## Phase 16 — Live-Tier Addendum (MCP-03 named-pipe bridge)

> Extends this Phase-14 register (C-11: this is the real on-disk path; there is NO
> `Utinni.Mcp/MCP-SECURITY.md`) with the live-injected named-pipe bridge surface (16-02 in-client
> half + 16-03 host half). **Gate verdict: ASVS L1; no HIGH-severity threats; all live-tier threats
> are `mitigate` or `accept`.**

### Applicable ASVS Categories (live tier)

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1 Architecture | yes | Out-of-proc MCP host + a narrow named pipe; the SDK NEVER enters SWG.exe (locked); least-surface verbs (ping + reload-asset only, D-02). Only a RELATIVE path crosses the pipe (CUR-NEW-1). |
| V4 Access Control | yes | `--enable-live` fail-closed-by-absence gate (D-04, proven via real `McpClient.ListToolsAsync` enumeration); `live_reload_asset` path resolved under the fail-closed `ResolvedRoot` BEFORE any pipe send; the in-client server independently re-contains the relative path under its OWN pinned client root. |
| V5 Input Validation | yes | The wire envelope is shape-validated (op ∈ {ping, reload-asset}; `protocolVersion` checked; path re-resolved under the client root); malformed / oversize / partial frames rejected; the OUTPUT bytes come from a Utinni-owned canonical writer (deterministic, golden-asserted). |
| V13 API / IPC | yes | **Named-pipe ACL — local-only, current-user.** `NamedPipeServerStream` with a `PipeSecurity` carrying exactly ONE `PipeAccessRule` for `WindowsIdentity.GetCurrent().User` (FullControl/Allow) and NO other rules (deny-others-by-omission); local-machine only (never network / "Everyone"). |
| V7 Error Handling | yes | No-client / framing / skew errors → a clean result object, NEVER a hang and NEVER a leaked exception across the pipe (bounded connect/read timeouts on both ends). |

### STRIDE Threat Register (live tier)

| Threat ID | Category | Component | Disposition | Mitigation (file:line evidence) | Proving Test | Status |
|-----------|----------|-----------|-------------|---------------------------------|--------------|--------|
| T-16-20 | Elevation | Agent reaches `live_*` when the tier is not intended to be on | mitigate | D-04 fail-closed gate: `live_*` tools AND the `LivePipeClient` singleton are UNREGISTERED unless `--enable-live` (C-13); `LiveTools` carries NO `[McpServerToolType]` so the assembly scan never grabs it — `Utinni.Mcp/Program.cs` (`if (serverArgs.EnableLive)`), `Utinni.Mcp/Tools/LiveTools.cs` | `LivePipeProtocolTests.LiveTools_AreAbsentWithoutEnableLive_PresentWithIt` (REAL `McpClient.ListToolsAsync` off⇒absent / on⇒present, C-10/CDX-NEW-5/6) | closed |
| T-16-21 | Tampering | Path-escape via `live_reload_asset` path arg (MCP side) | mitigate | `ResolvedRoot.Resolve` runs BEFORE any pipe send (throws on escape → SDK tool error); the RELATIVE path is then sent on the wire and RE-resolved server-side under the in-client client root via the SAME `LooseOverridePath.Resolve` (root reconciliation, CUR-NEW-1) — no absolute crosses the pipe — `Utinni.Mcp/Tools/LiveTools.cs` (`root.Resolve` then `pipe.ReloadAssetAsync(relativePath, ext)`), `UtinniCoreDotNet/Live/LivePipeServer.cs:487` | `LivePipeLoopbackTests.Loopback_ReloadAsset_SendsRelativeEnvelope_AndMapsAck` + `LiveBridgeIntegrationTests.ReloadAsset_AbsoluteOrEscape_RejectedNoEnqueue` + `…RootReconciliation_SameRoot_Succeeds_DifferentRoot_DiagnosticExposesDivergence` | closed |
| T-16-22 | DoS | Malformed / oversize / partial ack frame hangs the MCP call | mitigate | Injectable short connect/read timeout + bounded `MaxFrameBytes` (oversize declared length rejected before reading the body); a transport failure returns a result object, never a hang — `Utinni.Mcp/Server/LivePipeClient.cs` (`ExchangeAsync` CTS + `ReadFrameAsync` MaxFrameBytes guard); server side `UtinniCoreDotNet/Live/LivePipeServer.cs` (`ReadFrameBounded` partial-read + bounded timeout) | `LivePipeLoopbackTests.{OversizeAckFrame,PartialAckFrame,ServerClosesMidMessage}_…_HardErrorNoHang` (each completes well within the timeout) | closed |
| T-16-23 | Spoofing | No client listening → naive client hangs the MCP call | mitigate | A short connect timeout maps no-server to `listening:false` (Pitfall 3) — `Utinni.Mcp/Server/LivePipeClient.cs` (`DefaultTimeout` 2s, `LivePingResult.NotListening`) | `LivePipeLoopbackTests.NoClient_Ping_ReturnsListeningFalseQuickly_NeverHangs` | closed |
| T-16-24 | (candor) | Visible-render over-promise | mitigate (correctness) | The ack reports the honest `ReloadAssetClassifier` tier (string-encoded) with `reloadAttempted=false` + a transport-only note; the `live_reload_asset` Description + mapped result state queued != visible reload (C-14); render is best-effort, NOT a success gate (D-01) — `UtinniCoreDotNet/Live/LivePipeServer.cs:533`, `Utinni.Mcp/Tools/LiveTools.cs` (candor field + Description) | `LivePipeLoopbackTests.GoldenWire_ReloadAck_ConsumedExact` (reloadAttempted=false) + `LiveBridgeIntegrationTests.ReloadAsset_ContainedRelative_…` | closed |
| T-16-25 | Tampering (protocol drift) | net10/net472 wire shapes silently diverge (framing, JSON casing, tier encoding, field names, protocol version, `clientRoot`) → a green net472-on-net472 test hides a broken net10 path | mitigate | Wire OUTPUT bytes are produced on BOTH sides by a Utinni-owned CANONICAL writer (net472 `CanonicalJson` + a net10 field-for-field re-impl, R3-1) so bytes are deterministic by construction, NOT serializer reflection order; committed GOLDEN WIRE BYTE-VECTORS (ping/reload request+ack, ALL FOUR incl. nested acks, R3-7) asserted byte-EXACT on BOTH sides; `protocolVersion` in the envelope + a skew-surfaces-structured-error path (C-12/CUR-NEW-5); a real net472 cross-impl round-trip (C-02) — `Utinni.Mcp/Server/CanonicalJson.cs`, `UtinniCoreDotNet/Live/CanonicalJson.cs` | `LivePipeLoopbackTests.GoldenWire_*` (all four byte-exact) + `…ProtocolVersionSkew_Ping_SurfacesStructuredError` + `…FixtureEquality_PipeNameAndFraming` + `LiveBridgeIntegrationTests` (real wire) | closed |
| T-16-26 | (correctness) | `--root` and the in-client resolved client root diverge → the agent previews a DIFFERENT file than MCP validated | mitigate | The `live_ping` ack carries a `clientRoot` diagnostic (R3-3) so a runtime mismatch is visible to the operator/agent; relative-on-wire + own-root resolution keeps containment safe regardless. **Root-reconciliation requirement: `--root` MUST equal the in-client resolved client root; the `live_ping` ack's `clientRoot` field is the runtime verification mechanism** (the operator compares `--root` vs the reported `clientRoot`) — `UtinniCoreDotNet/Live/LivePipeServer.cs:473`, `Utinni.Mcp/Tools/LiveTools.cs` (clientRoot surfaced) | `LiveBridgeIntegrationTests.RootReconciliation_SameRoot_Succeeds_DifferentRoot_DiagnosticExposesDivergence` + `LivePipeLoopbackTests.Loopback_Ping_RoundTripsAndMapsClientRoot` | closed |
| T-16-AR16 | (accepted risk) | Single-user local desktop pipe; `--enable-live` / pipe-name are identifiers not secrets | accept | Local-only named pipe + current-user ACL (server side, 16-02, exact construction in `LivePipeServer.BuildCurrentUserPipeSecurity`) + in-client start gate (C-06, 16-02 Task 3); DUAL-flag operator contract + root-reconciliation requirement documented (CUR-NEW-6/R3-3); analog AR-14-03/04 — see AR-16-01 | (accepted — see AR-16-01) | closed |
| T-16-SC | Tampering | npm/pip/cargo installs | n/a | No new packages — net10 OUTPUT uses the hand-rolled net10 `CanonicalJson` (in-box `System.Text.Json` only for the tools' result JSON / optional parse); net472 uses the hand-rolled `CanonicalJson` (C-03); only the already-pinned `ModelContextProtocol 1.4.0`. RESEARCH Package Legitimacy Audit: gate not triggered. | (n/a — no install) | closed |

### Dual-flag operator contract (CUR-NEW-6)

The live tier requires **BOTH** flags — neither alone produces a working live path:

1. **In-client listener:** `[Live] enableLiveBridge=true` in the in-client config (16-02 Task 3, default OFF) so `LivePipeServer.StartListener` actually runs inside the injected client.
2. **MCP tool tier:** launch `Utinni.Mcp` with `--enable-live` (or `UTINNI_MCP_ENABLE_LIVE=1`, default OFF) so the `live_*` tools are advertised + the `LivePipeClient` is registered.

`--enable-live` alone advertises the tools but the listener never started → `live_ping` returns
`listening:false`; the in-client flag alone starts the listener but the agent never sees the tools.
`--enable-live` / `UTINNI_MCP_ENABLE_LIVE` / the pipe name are IDENTIFIERS, not secrets (AR-16-01).

### Root-reconciliation requirement (R3-3)

`--root` (the MCP host's pinned access-control root) MUST equal the in-client resolved client root
(the SWG client tree the injected `LivePipeServer` pins). When they diverge, the agent could validate
a path against one tree while the live client previews a different file. The **runtime verification
mechanism is the `live_ping` ack's `clientRoot` diagnostic**: the operator/agent compares `--root`
against the reported `clientRoot` BEFORE trusting a preview. Containment is safe regardless of a
mismatch (only a RELATIVE path crosses the pipe, re-resolved under the server's OWN root), but a
mismatch means the *preview targets a different tree* — hence the operator-visible diagnostic.

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
| 2026-06-14 | 26 | 26 | 0 | gsd-plan-executor (Claude Opus 4.8) — Phase-16 live-tier addendum (T-16-20..26 + T-16-AR16 + T-16-SC); cited tests green |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Every `mitigate` threat carries file:line evidence AND a cited proving test
- [x] Accepted risks documented in the Accepted Risks Log
- [x] 5-layer model encoded with the advisory (layers 1–2) vs. enforcement (layers 3–5) caveat
- [x] Save-tool host orchestration documented as a distinct layer 4 (vs. the read pass-through)
- [x] The scoped `apply-save-*` ZERO-verbs exception named and justified
- [x] Phase-16 live-tier addendum: ASVS rows + STRIDE register (T-16-20..26) + dual-flag operator contract + root-reconciliation requirement + accepted-risk row (AR-16-01)
- [x] `threats_open: 0` confirmed

**Approval:** draft authored 2026-06-07 (design-time deliverable; phase verification confirms the cited tests pass). Phase-16 live-tier addendum added 2026-06-14 (16-03; cited live tests green — `LivePipeProtocolTests`/`LivePipeLoopbackTests` net10 + `LiveBridgeIntegrationTests` net472).
