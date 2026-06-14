---
phase: 16
reviewers: [codex, cursor]
reviewed_at: 2026-06-14T13:21:40Z
plans_reviewed: [16-01-PLAN.md, 16-02-PLAN.md, 16-03-PLAN.md]
self_skipped: claude (running inside Claude Code — skipped for reviewer independence)
unavailable: [gemini, coderabbit, opencode, qwen]
---

# Cross-AI Plan Review — Phase 16

Two independent external AI CLIs reviewed all three Phase-16 plans (Codex `codex-cli 0.137.0`; Cursor agent via `cursor-agent`). `claude` was skipped because this review was orchestrated from inside Claude Code. Gemini, CodeRabbit, OpenCode, and Qwen are not installed on this machine.

Both reviewers independently converged on the same top-tier risks — the strongest signal in the report. See the **Consensus Summary** at the end.

---

## Codex Review

**Summary**

Overall, the plans are unusually disciplined: they preserve the out-of-proc MCP architecture, keep the live surface small, and explicitly separate Tier-2 protocol proof from Tier-4 live-client confirmation. The biggest risks are not architectural direction, but enforcement gaps: the pipe protocol agreement is only partially fixture-anchored, the live server lifecycle is underspecified, `reload-asset` can become a hollow success path, and the `WithToolsFromAssembly()` gating depends on SDK behavior that may be hard to prove robustly. ECO-01 is lower risk, though `validate-bundle` needs stricter containment and fixture provenance rules.

**Strengths**

- **16-01 / ECO-01** correctly keeps Utinni as reader and Blender as writer, with no runtime coupling and no new geometry codec.
- **16-01 Task 1** avoids a compile-time dependency on the future `ValidateBundleCommand` by using string-dispatch tests. Good staged plan.
- **16-01 Task 3** explicitly corrects the TRE version pitfalls: 5000 readable, 6000 enumerate-only, COT2000 not a TRE version.
- **16-02 / 16-03** preserve the locked architecture: MCP SDK remains in `Utinni.Mcp`, with only named-pipe IPC crossing into `SWG.exe`.
- **16-02 Task 1** has the right thread boundary: parse/decide on worker, enqueue exactly one game-thread action.
- **16-03 Task 2** correctly requires `ResolvedRoot.Resolve` before pipe send.
- **16-03 Task 3** covers the right Tier-2 proof surface: loopback ping, reload ack, timeout/no-hang, no-client behavior, and gating.

**Concerns**

- **HIGH — 16-02 Task 1 / 16-03 Task 1: framing descriptor is not actually contract-enforced.** The fixture has line 2 `frame=len32le+utf8json`, but acceptance only asserts line 1 equals `PipeName`. Both sides could silently drift on max frame size, JSON casing, required fields, error shape, or length-prefix behavior while still passing the pipe-name equality tests.
- **HIGH — 16-02 Task 1: named-pipe server lifecycle is underspecified.** The plan says "background-thread accept/read loop" but does not define cancellation, disposal, restart behavior, single vs multiple concurrent clients, repeated sequential connections, or behavior after a malformed frame closes one connection. A bug here can pass unit tests but hang or leak inside the injected client.
- **HIGH — 16-02 Task 1 / D-01: `reload-asset` risks shipping as a misleading no-op.** The plan is honest that the enqueued action is a TODO placeholder, but the ack fields include `accepted`, `queued`, and a classifier tier. That can read as "the preview action happened" when no reload binding exists. This satisfies "round-trip proven" only if the result schema clearly distinguishes `queuedPlaceholder` / `reloadAttempted=false` from real apply/reload.
- **HIGH — 16-03 Task 0 / Task 2: fail-closed tool hiding depends on fragile SDK registration behavior.** The plan relies on `LiveTools` lacking `McpServerToolType` so `WithToolsFromAssembly()` does not pick it up, then conditionally uses `WithTools<LiveTools>()`. That may be valid, but it is SDK-version-sensitive. The test must inspect the actual advertised tool list through the same host-building path, not just assert a branch or comment.
- **MEDIUM — 16-02 Task 1: current-user pipe ACL is probably not sufficient by itself.** Same-user local malware or another agent process can still trigger reloads if the pipe is running. Given `--enable-live` gates only MCP tool registration, the in-client server also needs its own start gate or nonce/session handshake if it can ever be launched independently.
- **MEDIUM — 16-03 Task 2: `live_ping` bypasses root gating by design, but still advertises live presence.** Probably acceptable, but the security doc should call out that enabling live exposes process liveness/game-running/pid to any MCP client with access to the server.
- **MEDIUM — 16-03 Task 2: absolute path crosses the pipe.** `live_reload_asset` resolves under `ResolvedRoot`, then sends the absolute path into the injected process. Safer than raw relative input, but can leak local filesystem layout and may not match the game's asset namespace. Consider sending both canonical relative path and resolved absolute path, or only the relative SWG asset path if the server does not need disk access.
- **MEDIUM — 16-02 Task 1: `ReloadAssetClassifier.Classify(ext, null)` may under-classify object templates or root-type-specific reloads.** Passing `null` is acceptable for minimal scope, but the ack tier may be less accurate than advertised. The plan should specify which asset classes are supported by Phase 16 and which return `PendingNextSceneChange` or `Unavailable`.
- **MEDIUM — 16-02 Task 1: heap-free hot path claim is slightly muddled.** The pipe server necessarily allocates on requests, which is fine because it is not per-frame. The plan should explicitly say no new per-frame allocations are introduced; the queued `Action` itself is an allocation but only per request.
- **MEDIUM — 16-01 Task 2: path traversal mitigation for bundle validation is too vague.** It says out-of-bundle refs are findings, but acceptance does not require rejecting absolute paths, `..`, symlinks/junction escapes, or mixed separators. This is the most likely ECO-01 security gap.
- **MEDIUM — 16-01 Task 1: fixture provenance is under-specified.** "Copy verbatim" is good, but there is no hash/checksum acceptance criterion. A future fixture refresh could silently change the cross-validation bytes.
- **MEDIUM — 16-03 Task 3: loopback tests may not cover server compatibility.** The client test uses its own stub "mirror" of the protocol. If both client and stub share the same duplicated wrong assumption, tests pass while the net472 server disagrees. A shared JSON/framing conformance vector would reduce this.
- **LOW — 16-01 Task 2: `validate-bundle` name may imply binary validation.** The plan says text-only, but users may expect `.msh`/`.tre` semantic validation. The output should clearly say it validates bundle references/search-path text, not mesh correctness.
- **LOW — 16-02 Task 2: optional env alias weakens explicit enable semantics.** `UTINNI_MCP_ENABLE_LIVE` may be convenient, but it makes live enablement less visible than a launch flag. If kept, it should require explicit truthy values, not "any non-empty".
- **LOW — 16-03 Task 1: no-client behavior differs by operation.** Ping maps to `{ listening:false }`; reload likely maps to hard error. That distinction should be explicit and tested so agents do not interpret reload transport failure inconsistently.

**Suggestions**

- Add protocol conformance tests in **16-02 Task 1** and **16-03 Task 3** that assert line 2 of `pipe-name.txt` equals the implemented framing descriptor, and add shared golden request/ack byte vectors for ping and reload.
- Extend the fixture to include protocol version, max frame size, JSON naming policy, and required envelope fields, or rename it from pipe-name fixture to protocol fixture.
- In **16-02 Task 1**, specify lifecycle behavior: one connection per `NamedPipeServerStream`, loop continues after client disconnect, malformed frame closes only that connection, `Stop/Dispose` cancels accept/read, restart is idempotent.
- Add tests for two sequential client connections and one malformed connection followed by a valid connection.
- Rename or reshape reload ack fields while the action is a placeholder: e.g. `accepted: true`, `queued: true`, `reloadAttempted: false`, `renderVisible: "not-guaranteed"`, `implementation: "placeholder-resid-03"`.
- In **16-03 Task 3**, verify live tool absence/presence through real MCP tool enumeration using the same `Program.cs` registration path. Do not settle for branch-level introspection unless SDK enumeration is impossible.
- Require `validate-bundle` to canonicalize paths with `Path.GetFullPath`, reject or report absolute paths and `..`, handle symlink/junction escape deliberately.
- Add SHA-256 hashes for pinned Blender fixtures in **16-01 Task 1** or in `swg_export_manifest.json`, and assert them in golden tests.
- Consider a server-side enable gate in **16-02**, not only MCP-side tool hiding. Even a local config/env flag in the injected host would reduce accidental exposure.
- In **16-03**, send a canonical relative asset path in the reload request, with absolute path optional only if the in-client side genuinely needs it.
- Document exact Phase-16 supported reload classes and expected tiers so `ReloadAssetClassifier.Classify(ext, null)` does not overpromise.
- Make timeout budgets explicit: connect timeout, read timeout, max frame size, and whether timeout applies to the full round trip or each stage.

**Risk Assessment: MEDIUM.** The architecture is sound and the plans mostly respect the locked decisions. The risk is that the implementation could pass the planned tests while leaving important operational gaps: pipe lifecycle leaks, partial protocol drift, fragile live tool gating, and a "reload" tool that only queues a documented no-op. ECO-01 is comparatively low risk, provided path containment and fixture hashes are tightened. The live bridge should remain acceptable for Phase 16 if the ack candor, protocol conformance, and lifecycle tests are strengthened before execution.

---

## Cursor Review

### 1. Summary

These plans are unusually well-grounded in shipped seams (`GameCallbacks.AddMainLoopCall`, `ReloadAssetClassifier`, `CliDispatcher`, `ServerArgs`, Phase-13 readers) and correctly split ECO-01 (low risk, wave 1) from MCP-03 (new mechanism, wave 2 with a real `depends_on: [16-02]`). The pipe-name fixture anchor is a pragmatic fix for the net472/net10 project-reference wall. However, MCP-03 has a **load-bearing integration hole**: **16-02 builds `LivePipeServer` + `StartListener` but no plan wires startup or runs a client↔server cross-implementation test**, so Tier-2 can go green while Tier-4 manual and real agent preview still fail silently. Secondary gaps include wrong JSON stack assumptions on net472, framing-descriptor enforcement only on paper, missing in-client path containment on `reload-asset`, and a wrong/missing `MCP-SECURITY.md` target path.

### 2. Strengths

- **Architecture reuse is correct.** 16-02's threading model (background accept/read → single `Action` on `mainLoopCallQueue`) matches how `GameCallbacks` is actually used today and respects the heap-free hot-path constraint (research A2).
- **Honest ack semantics.** Reusing `ReloadAssetClassifier` for tier reporting + explicitly overriding `!gameIsRunning → Unavailable` aligns with D-01 (`ReloadAssetClassifier.cs:78-80`).
- **D-04 is taken seriously.** 16-03 Task 0 (Assumption A1 spike) + conditional `WithTools<LiveTools>()` + explicit "no `[McpServerToolType]` on `LiveTools`" matches the real `Program.cs:74` `WithToolsFromAssembly()` scan.
- **Cross-TFM agreement mechanism is sound in principle.** Canonical `pipe-name.txt` linked into both test projects with byte-equality assertions is the right pattern when net10 cannot project-ref net472.
- **ECO-01 scope discipline.** Thin text-only `validate-bundle`, golden cross-validation via existing `parse-tre`/`decode-iff`, DEC-A3 honored, TreVersion pitfall 4 called out — all match research B1/B4.
- **16-01 Task 1 compile-boundary fix (WARNING-3).** String-runner `ValidateBundleTests` without type references to not-yet-built command code is a mature incremental-test pattern.
- **Never-hang discipline is explicitly cloned** from `CliDispatcher.cs:121-138` for the pipe client, with injectable timeouts and stub-server loopback.
- **D-01 placeholder is labeled honestly** in 16-02 Task 1 (`TODO(RESID-03)` on the enqueued Action).

### 3. Concerns

**MCP-03 track**

| ID | Severity | Concern |
|----|----------|---------|
| **C-01** | **HIGH** | **No plan wires `LivePipeServer.StartListener()` into `Startup.EntryPoint` (`main.cs`).** 16-02 Task 1 says "expose StartListener so the host decides" but **no task in 16-02 or 16-03 calls it**. Without this, `live_ping` always returns `{ listening: false }` in a real injected client regardless of `--enable-live`. Tier-2 loopback tests use stubs, so they won't catch this. |
| **C-02** | **HIGH** | **No cross-implementation integration test.** 16-03 Task 3 tests `LivePipeClient` against a **test stub server**, not `LivePipeServer` from 16-02. The `depends_on: [16-02]` edge is enforced only by **pipe-name literal equality**, not by proving the two implementations speak the same framing/envelope on the wire. Drift in JSON field names, tier serialization, or read-loop behavior can pass all tests. |
| **C-03** | **HIGH** | **`LiveBridgeProtocol` specifies `System.Text.Json` on net472, but `UtinniCoreDotNet` has no JSON dependency today** (uses `JavaScriptSerializer` elsewhere, e.g. `ObjectTemplateSchemaLoader.cs`). Claiming "BCL-only (System.Text.Json)" in 16-02 acceptance criteria is **incorrect for net472** unless a package/reference is added — which triggers the project's Package Legitimacy Gate that 16-02 threat model says is "not triggered." |
| **C-04** | **HIGH** | **In-client `reload-asset` has no path containment.** 16-03 correctly runs `ResolvedRoot.Resolve` before send, but 16-02 sends the **absolute path over the pipe** and the server enqueues a reload Action with **no equivalent root guard**. Any same-user local process that connects to `utinni-live-bridge` (T-16-10 mitigates cross-user, not same-user) can trigger reload attempts on arbitrary filesystem paths. T-16-21 covers MCP-side escape only. |
| **C-05** | **MEDIUM** | **Framing descriptor line 2 of `pipe-name.txt` is documentation-only.** Tests assert line 1 byte-equals `PipeName` on both sides, but `frame=len32le+utf8json` is never parsed or enforced. Duplicated framing code in net472 vs net10 can diverge (message mode vs length-prefix, endianness, max size) while pipe-name tests stay green. |
| **C-06** | **MEDIUM** | **D-04 gating is one-sided.** `--enable-live` gates MCP tools, but 16-02 defers in-client listener gating (research Open Q1). If `StartListener` is wired always-on, any local process can hit the pipe even when the agent tier is off — weaker than D-04's "fail-closed by absence" spirit. |
| **C-07** | **MEDIUM** | **Concurrent connections / server lifecycle unspecified.** No task covers `MaxAllowedServerInstances`, one-client-at-a-time vs parallel accepts, behavior when the accept loop throws, server restart after disconnect, or one-request-per-connection. Named-pipe servers commonly deadlock or leak instances without this. |
| **C-08** | **MEDIUM** | **Partial-read handling is client-tested only (16-03 Task 3).** 16-02 `LivePipeServerTests` lists oversize rejection but **not partial reads or mid-message disconnect on the server read loop** — asymmetric coverage. |
| **C-09** | **MEDIUM** | **Runtime `gameIsRunning` wiring is underspecified.** Pure tests inject a bool (good), but the production `Handle` path must read `Game.IsRunning` somewhere. Plan doesn't say where (worker vs enqueue boundary) or whether that read is thread-safe — Pitfall 2 says no game-state deref on worker, but `Game.IsRunning` may be a native-backed flag. |
| **C-10** | **MEDIUM** | **Assumption A1 remains load-bearing until Task 3.** Task 0 only `dotnet build`s; the actual "tools absent/present" assertion is deferred. If SDK 1.4.0 requires `[McpServerToolType]` for `WithTools<T>()` to discover methods, D-04 breaks late in wave 2. |
| **C-11** | **MEDIUM** | **`MCP-SECURITY.md` target path is wrong.** 16-03 Task 3 modifies `Utinni.Mcp/MCP-SECURITY.md`, but the repo only has `.planning/phases/14-.../MCP-SECURITY.md` — nothing under `Utinni.Mcp/`. Task will fail or create an untracked doc in the wrong place relative to Phase 14's stated deliverable. |
| **C-12** | **MEDIUM** | **Protocol version skew has no negotiation.** Requests have `{ op, path, ext }` with no `protocolVersion`; acks have `schemaVersion`. Field renames or tier enum serialization changes fail at runtime with no explicit mismatch error. |
| **C-13** | **LOW** | **`LivePipeClient` is registered unconditionally in DI (16-03 Task 2)** even when `EnableLive` is false. Harmless if unused, but inconsistent with fail-closed posture. |
| **C-14** | **LOW** | **D-01 placeholder risks "hollow feature" perception.** The TODO is explicit, but agents/users calling `live_reload_asset` get `queued: true` with a no-op Action — easy to misread as functional preview unless MCP tool descriptions and ack copy scream "transport-only until RESID-03." |

**ECO-01 track (16-01)**

| ID | Severity | Concern |
|----|----------|---------|
| **C-15** | **MEDIUM** | **`validate-bundle` path traversal is threat-modeled (T-16-01) but not specified in Task 2 action.** Needs explicit rules: reject absolute paths in manifest/RSP, normalize `..`, don't `File.Exists` on attacker-controlled absolute refs. Without acceptance tests, implementers may "check existence" via raw manifest strings. |
| **C-16** | **MEDIUM** | **Task 2 acceptance mentions malformed-manifest exit 2 but Task 1 scaffold only covers success + missing-manifest.** Malformed JSON/RSP cases may be skipped unless added explicitly. |
| **C-17** | **MEDIUM** | **Task ordering: contract doc (Task 3) follows verb (Task 2).** `validate-bundle` bucket rules are sourced from external `rsp_builder.py` at impl time — doc drift risk if Task 3 wording diverges from Task 2 code. No "doc ↔ verb parity" test. |
| **C-18** | **LOW** | **Task 4 human checkpoint blocks autonomous ECO-01 completion** (by design). D-05 success depends on maintainer approval; flag in milestone acceptance, not only as a resume gate. |
| **C-19** | **LOW** | **Task 3 verify uses bash `test -f` / `grep` on a Windows-primary repo.** May fail in PowerShell-only CI unless Git Bash is guaranteed. |
| **C-20** | **LOW** | **External fixture copy step depends on `D:/Code/swg-blender-plugin` at authoring time.** Committed fixtures insulate CI, but document fallback if absent. |

**Cross-plan / dependency**

| ID | Severity | Concern |
|----|----------|---------|
| **C-21** | **MEDIUM** | **Wave-1 plans (16-01, 16-02) are independent — good — but 16-02 Task 2 (`EnableLive`) lands before 16-03 consumes it with no intermediate verification** that `Program.cs` gating works end-to-end until all of 16-03 completes. |
| **C-22** | **LOW** | **Fixture lives under `Utinni.Cli.Tests/Fixtures/live/` for a cross-cutting protocol concern.** Works, but couples MCP/core tests to CLI test project layout. |

### 4. Suggestions

**MCP-03 (16-02 / 16-03)**

1. **Add 16-02 Task 3 (or extend Task 1): wire `LivePipeServer.StartListener()` in `UtinniCoreDotNet/main.cs`** after `GameCallbacks.Initialize()`. Add a pure-managed test that `StartListener` is idempotent and doesn't double-spawn accept loops.
2. **Add one Tier-2 integration test:** spin up real `LivePipeServer` on a randomized pipe name, point `LivePipeClient` at it, assert `ping` + `reload-asset` round-trip. Makes `depends_on: [16-02]` protocol-load-bearing, not just name-load-bearing.
3. **Fix JSON stack in 16-02:** use `JavaScriptSerializer` or manual framing + minimal parsing to stay truly in-box on net472, *or* add an explicit approved-package step with legitimacy gate. Align net10 client serialization field-for-field.
4. **Enforce framing descriptor line 2 in tests:** parse `pipe-name.txt` line 2 in both suites and assert both implementations' framing constant matches.
5. **In-client path policy for `reload-asset`:** either (a) send relative paths only and resolve inside the injected host against a pinned root, or (b) validate absolute paths against an allow-list. Add server-side tests for rejected paths.
6. **Close D-04 defense-in-depth loop:** gate `StartListener` on an in-client flag and document that MCP `--enable-live` alone is insufficient for listener start — or accept the asymmetry explicitly in `MCP-SECURITY.md`.
7. **Specify pipe server concurrency contract:** one instance, one active client, one request per connection (or persistent session) — test server restart after disconnect.
8. **Extend `LivePipeServerTests` (16-02 Task 1)** with server-side partial-read / disconnect cases mirroring 16-03 Task 3.
9. **Fix `MCP-SECURITY.md` path (16-03 Task 3):** copy the Phase-14 doc into `Utinni.Mcp/` first, or amend the planning artifact under `.planning/phases/14-.../` consistently.
10. **Clarify production `gameIsRunning` capture** in `LivePipeServer.Handle` and add one integration test with `gameIsRunning=false` over a real pipe.
11. **Tool descriptions for `live_reload_asset`:** state explicitly that `queued` means "marshal scheduled," not "visible reload occurred," until RESID-03.

**ECO-01 (16-01)**

1. **Task 2 action: add explicit containment rules** for manifest asset paths and RSP `@` RHS paths (relative-only, rooted under bundle root).
2. **Add malformed-manifest / malformed-rsp tests** in `ValidateBundleTests` before calling Task 2 done.
3. **Add a lightweight parity check:** snapshot test that bucket filenames in `ValidateBundleCommand` match sections in `blender-boundary-contract.md`.
4. **Task 3 verify:** replace bash grep with `dotnet`-friendly checks or a small doc-reading test.
5. **Consider Task-order swap:** write the contract-doc skeleton before `validate-bundle` so D-06 is authoritative during coding.

**Cross-plan**

1. **Add a Phase-16 "integration checklist" row:** client ↔ real server ↔ `StartListener` wired — distinct from loopback stub and Tier-4 manual.
2. **Document tier enum wire format** (`PendingNextSceneChange` string vs int) in `LiveBridgeProtocol` and test both ends.

### 5. Risk Assessment: **MEDIUM-HIGH**

ECO-01 (16-01) is **LOW–MEDIUM** — mostly doc + thin CLI + golden tests with strong existing reader coverage; main risks are path-validation gaps and the human-gated cross-repo pointer.

MCP-03 (16-02 + 16-03) is **MEDIUM-HIGH** despite excellent architectural choices, because the **automated proof does not yet close the real delivery path**: listener startup is unplanned, client/server implementations are not integration-tested together, JSON/framing duplication is under-enforced, and in-client reload-path trust is incomplete. The plans **can** satisfy D-01 at Tier-2 only if "pipe" means stub server; they **do not yet guarantee** the injected-client bridge works beyond manual Tier-4.

The D-01 no-op reload Action is **honest and aligned with locked decisions**, but product risk remains that users/agents interpret `queued: true` as functional preview — mitigate via tool docs and ack copy.

**Quick answers to focused questions**

| Question | Verdict |
|----------|---------|
| Pipe-name fixture robust? | **Partially.** Line 1 enforced; line 2 not tested. |
| Purity/injection pattern sound? | **Yes for unit tests**; production `Game.IsRunning` wiring needs one explicit sentence. |
| `WithToolsFromAssembly()` accidentally picks up `LiveTools`? | **Plan correctly prevents it** — still verify at SDK 1.4.0. |
| Does Phase 16 achieve stated goals? | **ECO-01: likely yes** (minus human checkpoint). **MCP-03 Tier-2: yes vs stubs; real bridge: gap on startup wiring + cross-impl test.** |
| D-01 placeholder honest or hollow? | **Honest in code comments; hollow in user-facing behavior** unless tool descriptions reinforce it. |

---

## Consensus Summary

Both reviewers rate ECO-01 (16-01) **LOW–MEDIUM** and MCP-03 (16-02/16-03) the real risk surface (Codex: MEDIUM overall; Cursor: MEDIUM-HIGH). Neither found an architectural flaw — the out-of-proc lock, minimal D-02 surface, reuse of shipped seams, and D-01 candor are all praised. **The risk is uniformly "the plan can go Tier-2 green while leaving real operational/integration gaps."**

### Agreed Strengths (both reviewers)
- Out-of-proc MCP architecture preserved; only the narrow named pipe crosses into `SWG.exe`.
- 16-02 threading model is correct: parse/decide on worker thread, enqueue exactly one `Action` onto the game-thread queue; heap-free hot path respected.
- ECO-01 scope discipline: Utinni reads / Blender writes, no geometry codec (DEC-A3), TreVersion pitfall-4 explicitly corrected (5000 readable, 6000 enumerate-only, COT2000 not a version).
- 16-01 Task 1 string-dispatch test pattern keeps the assembly compiling at the task boundary (WARNING-3 fix).
- `CliDispatcher` never-hang discipline correctly cloned for the pipe client; `ResolvedRoot.Resolve` runs before send on the MCP side.
- D-01 reload placeholder is honestly labeled in code.

### Agreed Concerns (raised by BOTH — highest priority)
1. **[HIGH] Framing descriptor (line 2 of `pipe-name.txt`) is not enforced.** Only the pipe name (line 1) gets byte-equality tests; the `frame=len32le+utf8json` framing can silently drift between the net472 and net10 duplications (max size, endianness, encoding, field names) while all tests stay green. *(Codex HIGH / Cursor C-05.)*
2. **[HIGH] No cross-implementation integration test.** `LivePipeClient` is only tested against a test-stub server, never against the real 16-02 `LivePipeServer`. The `depends_on: [16-02]` edge is enforced only by pipe-name literal equality, not by proving both ends speak the same wire protocol. *(Cursor C-02 HIGH / Codex MEDIUM "loopback may not cover server compatibility.")*
3. **[HIGH] In-client `reload-asset` has no path containment.** The MCP side resolves under `ResolvedRoot`, but 16-02's server enqueues a reload for the absolute path it receives with no equivalent guard — a same-user local process on the pipe can target arbitrary paths. *(Cursor C-04 HIGH / Codex MEDIUM.)*
4. **[HIGH] Fail-closed `--enable-live` gating depends on fragile, SDK-version-sensitive `WithToolsFromAssembly()` behavior.** Both insist the gating test inspect the **actually-advertised tool list through the real host-building path**, not a branch/comment assertion. *(Codex HIGH / Cursor C-10 MEDIUM.)*
5. **[HIGH/MEDIUM] Named-pipe server lifecycle is underspecified:** cancellation/disposal, concurrent vs sequential clients, restart after disconnect, accept-loop-throw recovery, one-request-per-connection, server-side partial-read / mid-message disconnect. Can pass unit tests but hang/leak inside the injected client. *(Codex HIGH / Cursor C-07+C-08 MEDIUM.)*
6. **[HIGH/LOW] `reload-asset` ack candor.** `accepted/queued/tier` reads as "preview happened" when the Action is a no-op. Both want the ack schema + MCP tool description to explicitly say "transport-only / `reloadAttempted=false` until RESID-03." *(Codex HIGH / Cursor C-14 LOW.)*
7. **[MEDIUM] `validate-bundle` path-traversal mitigation is too vague** — threat-modeled (T-16-01) but no acceptance rule to reject absolute paths / `..` / symlink escape; needs `Path.GetFullPath` canonicalization + tests. *(Both MEDIUM, Codex + Cursor C-15.)*
8. **[MEDIUM] D-04 gate is one-sided / current-user ACL alone is weak** — the in-client listener start isn't itself gated; a same-user process can reach the pipe even when the agent tier is off. Both suggest an in-client start gate (config/env/nonce) as defense-in-depth. *(Codex MEDIUM / Cursor C-06.)*

### Divergent / Unique-but-important Views
- **Cursor uniquely flagged two HIGH issues Codex missed — both look like genuine plan defects worth fixing before execution:**
  - **C-01 [HIGH]: `LivePipeServer.StartListener()` is never wired into startup (`main.cs`).** If true, `live_ping` returns `listening:false` in a real client regardless of `--enable-live` — the bridge silently never runs in-client, and Tier-2 stubs won't catch it. **This is the single most important finding in the report.**
  - **C-03 [HIGH]: `System.Text.Json` "BCL-only" claim is wrong for net472.** `UtinniCoreDotNet` has no STJ dependency today (uses `JavaScriptSerializer`); adding STJ pulls a package, contradicting 16-02's "Package Legitimacy Gate not triggered." Fix the serializer choice or add the package step honestly.
  - **C-11 [MEDIUM]: `MCP-SECURITY.md` is at `.planning/phases/14-.../`, not `Utinni.Mcp/`** — 16-03 Task 3's edit target path is wrong.
- **Codex uniquely flagged:** fixture provenance needs **SHA-256 hashes** asserted in golden tests (silent fixture-refresh drift); and `ReloadAssetClassifier.Classify(ext, null)` may **under-classify** root-type-specific reloads — document the supported asset classes.
- **Overall rating divergence is small and explained:** Codex MEDIUM vs Cursor MEDIUM-HIGH — the delta is exactly Cursor's two unique HIGH findings (StartListener wiring + net472 JSON stack).

### Recommended pre-execution actions (consensus-ordered)
1. **Verify & fix C-01** (StartListener wiring into `main.cs`) — add an explicit task; this is load-bearing for the whole MCP-03 deliverable.
2. **Verify & fix C-03** (net472 JSON serializer / package-gate honesty).
3. **Add a real client↔server integration test** (C-02) and **enforce framing line 2** (#1) — turn the `depends_on` edge into a wire-protocol proof.
4. **Add in-client path containment + server-side lifecycle/partial-read tests** (C-04, #5).
5. **Make the D-04 gating test assert the advertised tool list** through the real host path (#4); add in-client start-gate defense-in-depth (#8).
6. **Reshape the reload ack + tool description for candor** (#6); fix the `MCP-SECURITY.md` path (C-11).
7. **ECO-01:** tighten `validate-bundle` path containment + tests (#7), add fixture SHA-256 (Codex), add malformed-manifest tests, and a doc↔verb parity check.

To incorporate this feedback into planning:
```
/gsd:plan-phase 16 --reviews
```
