---
phase: 16
review_round: 3
reviewers: [codex, cursor]
reviewed_at: 2026-06-14T14:55:32Z
plans_reviewed: [16-01-PLAN.md, 16-02-PLAN.md, 16-03-PLAN.md]
self_skipped: claude (running inside Claude Code - skipped for reviewer independence)
unavailable: [gemini, coderabbit, opencode, qwen]
prior_rounds: round 2 + round 1 preserved below
---
# Cross-AI Plan Review â€” Phase 16 (Round 3, post round-2 replan)

Third cross-AI review round, run against the round-2-replanned 16-01/16-02/16-03 plans (commit `4d1f2f4`). Two independent external CLIs (Codex `codex exec`; Cursor agent via `cursor-agent.cmd`); `claude` self-skipped for independence; Gemini/CodeRabbit/OpenCode/Qwen not installed.

**Both reviewers agree the plans are largely execution-ready and the round-2 HIGH fixes are load-bearing, not decorative** â€” but neither will rubber-stamp. They independently converged on a real weakness the round-2 fix *introduced*: the cross-TFM wire proof relies on **byte-exact JSON equality across two different serializers** (`JavaScriptSerializer` vs `System.Text.Json`), which is fragile/under-specified. And both flag that the `gameIsRunning` fix is source-level (no `Game.IsRunning` token in the loop) but **semantically** still risks an off-game-thread native read.

**Round-2 fix verdict (both reviewers):** item 2 (client-root containment) = **CLOSED**; item 3 (contained-absolute `.rsp`) = **CLOSED**; item 1 (cross-TFM wire proof) = **PARTIAL**.

> Rounds 1 (C-01..C-19) and 2 (CDX-NEW-*/CUR-NEW-*) are preserved in the Appendix below.

---

## Codex Review (Round 3)

**Summary** â€” Mostly execution-ready, but not clean. The plans materially address the Round-2 HIGH items and are much stronger, especially around relative-on-wire, server-side containment, fail-closed live gating, and committed wire fixtures. The main remaining risk is execution complexity: the serializer/golden-vector plan is feasible but brittle unless canonical JSON serialization is explicitly owned rather than left to serializer reflection order, and the MCP root vs in-client client-root relationship is still operationally assumed rather than handshake-verified.

**Round-2 fix verification:** wire proof = **PARTIAL** (byte-exact JSON across `JavaScriptSerializer` and `System.Text.Json` is fragile unless property order, whitespace, escaping, enum string encoding, and null omission are explicitly canonicalized); client-root containment = **CLOSED**; contained-absolute `.rsp` = **CLOSED**.

**Strengths**
- D-01 candor preserved (`reloadAttempted=false`, queued â‰  visible reload, render non-gating).
- Fail-closed live tier tested through real MCP tool enumeration, not a branch assertion.
- Server-side containment is defense-in-depth against direct same-user pipe callers.
- MCP SDK correctly never hosted in-process.
- ECO-01 stays thin (docs, text validation, reader reuse, no geometry decoder).
- Dual-flag operator contract now visible in the right places.

**Concerns**
- **R3-1 HIGH â€” Golden JSON byte-exactness is underspecified and may be flaky.** `JavaScriptSerializer` has no naming policy, enum-string policy, or canonical ordering guarantee equivalent to STJ. camelCase via lowercase C# member names can work, and string tier via string DTO fields can work, but byte-exact equality still depends on member declaration/reflection order and escaping. The plan sometimes says "key-order-independent equality" and elsewhere "byte-exact" â€” that contradiction weakens the proof.
- **R3-2 MEDIUM â€” Root reconciliation is still operational, not protocol-verified.** MCP validates under `--root`, sends a relative path; the injected server resolves under its own client root. Safe from escape, but if the roots differ the agent may preview a different file than MCP validated. Same/different-root cases are tested, but there's no runtime handshake/diagnostic exposing the server's pinned root.
- **R3-3 MEDIUM â€” Task verification uses `--no-build` after adding new files.** Several tasks add `.cs`/`.csproj` entries then verify with `dotnet test --no-build` â†’ can pass stale binaries or fail confusingly. Undercuts the acceptance gates.
- **R3-4 MEDIUM â€” 16-02 Task 1 is too large and failure-prone.** Combines protocol DTOs, framing, pipe ACLs, server lifecycle, timeouts, containment, golden fixtures, JS serialization, game-thread marshal, and many tests in one batch.
- **R3-5 MEDIUM â€” `Game.IsRunning` boundary wording still ambiguous.** Production `Func<bool>` "reads `Game.IsRunning` at the marshal boundary," but the decision returns the ack immediately and enqueue happens separately. If the accept loop invokes the Func before enqueue, it violates the intent. Make the production shape explicit: a game-thread-cached scalar, or native read only inside a game-thread action.
- **R3-6 LOW â€” Golden fixtures cover JSON payloads, not full frame bytes** (omit the 4-byte length prefix) â€” the "golden byte-vector" label is slightly overstated.
- **R3-7 LOW â€” `validate-bundle` manifest contract may expand beyond real Blender output** (unknown-field tolerance, version behavior, bucket parity) â€” risks designing a new contract before the exporter commits to it.

**Suggestions**
- Define a tiny canonical JSON writer for the live wire on both sides (or manually emit ordered fields) so byte-exact fixtures become deterministic instead of serializer-order dependent.
- Compare exact UTF-8 bytes for all four payloads on both TFMs, and separately test semantic parse with reordered JSON for reader tolerance.
- Add `live_ping` debug fields (`serverClientRoot`/`serverClientRootHash`) or a mismatch note so root mismatches are visible.
- Replace task-level `dotnet test --no-build` with `dotnet test` whenever a task creates/edits source/project files.
- Split 16-02 Task 1 into protocol/goldens, server core, lifecycle/pipe tests, and main wiring.
- For `Game.IsRunning`, require a source assertion that no production path invokes it from the accept/read thread â€” not just "loop body contains no reference."

**Risk Assessment: MEDIUM.** Architecture is directionally sound; prior HIGHs mostly addressed. Remaining risk is execution risk (deterministic cross-serializer JSON, complex pipe lifecycle tests, root-mismatch diagnostics). With canonical serialization and corrected build gates, this drops to LOW-MEDIUM.

---

## Cursor Review (Round 3)

**Summary** â€” After two rounds, the plans are substantively improved and largely execution-ready; round-2 HIGH fixes are concrete tasks/tests/acceptance criteria, not prose. ECO-01 is well-scoped; MCP-03 correctly reuses shipped seams and closes the TFM wall with shared fixtures + a net472 cross-impl test. Would not rubber-stamp: two areas can still fail during execution without plan tweaks â€” (a) byte-exact JSON agreement across the two serializers, and (b) the `gameIsRunning` threading contract is internally inconsistent.

**Round-2 fix verification:** wire proof = **PARTIAL** (strategy is real, but byte-exact across two serializers is fragile â€” key order, enum encoding, escaping; the cross-impl test uses net472 protocol code, not `LivePipeClient`, so the proof is indirect: net10 bytes == goldens == net472 bytes == integration client); client-root containment = **CLOSED**; contained-absolute `.rsp` = **CLOSED** (verified against `rsp_builder.py:58,62-64`).

**Strengths**
- Round-2 fixes are load-bearing (golden fixtures, client-root pinning, contained-absolute branch, `valid`/`hasRejectedRefs` envelope, D-04 gating locked in Task 0 via real `McpClient.ListToolsAsync`).
- Strong reuse discipline (no new codecs, no STJ on net472, no in-proc SDK, containment via shipped `LooseOverridePath`).
- Thoughtful test boundaries (Task-1 RED scaffolds compile without Task-2 types; SHA-256 pinning; docâ†”verb parity replaces bash grep).
- Threat models cite proving tests; D-01/D-02/D-04 preserved; ECO-01 matches real Blender output.

**Concerns**
- **R3-1 HIGH â€” Byte-exact golden vectors across JS vs STJ is fragile.** Key ordering not guaranteed across serializers even with matching declaration order; `JsonStringEnumConverter` can emit `"pendingNextSceneChange"` not `"PendingNextSceneChange"` unless configured carefully; escaping/number/nested-`result` order can diverge on ack bodies. The cross-impl test drives the server with net472 serialization, not `LivePipeClient`, so a net10-only wire bug could slip through if golden tests are brittle. Real risk for nested ack shapes.
- **R3-2 HIGH â€” `gameIsRunning` / CDX-NEW-2 threading gap.** The plan forbids `Game.IsRunning` literally in the accept loop but requires the ack to set `queued`/tier/`gameRunning` before returning â€” implying `getGameIsRunning()` is called on the worker thread. If the production `Func<bool>` reads native-backed `Game.IsRunning`, that still violates Pitfall 2; the fix is source-level, not semantic. The "game-thread-owned cached bool" is mentioned but not required in acceptance criteria. Biggest remaining MCP-03 correctness risk.
- **R3-3 MEDIUM â€” MCP `--root` vs in-client auto-resolved client root can diverge** (CWD drift, ini override). Root-reconciliation tests prove independent containment, not alignment; no runtime diagnostic (e.g. `live_ping` reporting resolved roots).
- **R3-4 MEDIUM â€” 16-02 Task 1 scope/size risk** (protocol DTOs + framing + full server + ACL + lifecycle + timeouts + dispose + ~15 test categories in one atomic task).
- **R3-5 MEDIUM â€” Manifest schema fixture fidelity.** Real Blender shape uses top-level `pipeline_version`/`exported_at`/`bundle_type`/`output_dir`/`assets` + optional `rsp_files`/`tre_files`, with `client_cfg` inside `assets`. If Task 1 fixtures simplify this, `validate-bundle` risks becoming fixture-shaped despite CDX-NEW-10 intent.
- **R3-6 MEDIUM â€” Mirrored absolute containment can drift from `LooseOverridePath`.** Relative refs delegate; absolute refs mirror the tail algorithm in hand-written code (must duplicate WR-07 root re-canonicalization). No shared helper or paired test asserts mirror â‰¡ helper for contained absolutes with messy roots (`C:\bundle\..\bundle`).
- **R3-7 LOW â€” Ping ack "key-order-independent AND byte-exact" tension** â€” pick one contract per message type.
- **R3-8 LOW â€” External `FormIffEditor` line-number drift** (`:1596-1630` cited; source lives in UtinniPlugins) â€” executor must grep, not trust line numbers.
- **R3-9 LOW â€” 16-01 Task 2 `--no-build` verify** after Task 1 full build â€” local "tests only" runs can false-green stale binaries.

**Suggestions**
1. Lock the wire contract before Task 1 goldens: generate once from net472 `JavaScriptSerializer`; net10 use a string-typed `tier` property (not `JsonStringEnumConverter`), `JsonNamingPolicy.CamelCase`, no indentation, fixed member order. If byte-exact remains required for **requests only**, make ack goldens **consume-only** on net10 (deserialize + field assert), not re-serialize byte-exact â€” reduces fragility while preserving cross-TFM read compatibility.
2. Amend 16-02 acceptance (CDX-NEW-2): require a game-thread-updated cached snapshot (`volatile bool`/`Interlocked`) for `gameIsRunning`; worker reads cache only; explicitly forbid a `getGameIsRunning()` that P/Invokes on the pipe thread; add a test counting Func invocations on worker vs game thread.
3. Split 16-02 Task 1 into (A) protocol + fixtures + golden serialization tests and (B) `LivePipeServer` + lifecycle.
4. Manifest schema table in Task 3 sourced verbatim from `export_manifest.py` + `assets` block; Task 1 fixture must match.
5. Operator alignment: document that `--root` MUST equal the in-client resolved client root; optionally expose resolved client root in the ping ack for debugging.
6. Extract a shared `IsContainedUnderRoot(canonicalRoot, canonicalCandidate)` used by both the `LooseOverridePath` tail and the `validate-bundle` absolute branch â€” or a paired test running the same cases through both.

**Risk Assessment: MEDIUM.** ECO-01 = LOW (clear scope, shipped helpers, Blender contract verified). MCP-03 protocol/security = MEDIUM (threading + cross-serializer wire proof are execution traps). Task sizing = MEDIUM (16-02 Task 1 heavy). D-01/D-06 regression = LOW (explicitly preserved). Bottom line: close R3-1 and R3-2 in the plan text (wire contract + cached `gameIsRunning`) and execution can proceed with confidence.

---

## Consensus Summary

Two independent reviewers, very strong convergence. The round-2 replan genuinely closed two of its three HIGH items (client-root containment, contained-absolute `.rsp`). The third (cross-TFM wire proof) is **PARTIAL by unanimous verdict** â€” the chosen mechanism (byte-exact equality across two different serializers) is the weak link. This round's findings are execution-detail-level, not architectural reversals.

### Round-2 fix verdict (both reviewers agree)
| Round-2 HIGH | Verdict |
|---|---|
| Client-root containment (not injectRoot), relative-on-wire + server-side `LooseOverridePath.Resolve` | **CLOSED** |
| `validate-bundle` allows contained-absolute `.rsp`, rejects only escapes | **CLOSED** |
| Cross-TFM wire proof via golden byte-vectors | **PARTIAL** |

### Agreed Concerns (raised by both â€” highest priority)
1. **[HIGH â€” both] Byte-exact JSON across `JavaScriptSerializer` and `System.Text.Json` is fragile/under-specified** (Codex R3-1, Cursor R3-1). Two different serializers are not guaranteed to emit identical bytes (key order, enum-string casing, escaping, whitespace, null omission), and the plan contradicts itself ("key-order-independent equality" vs "byte-exact"). **Both propose the same class of fix:** either own a tiny canonical JSON writer on both sides so output is deterministic, OR split the contract â€” byte-exact for the simple **requests**, and **consume-only/semantic** assertion for the nested **acks** (net10 deserializes the committed ack goldens + asserts fields, rather than re-serializing byte-exact). Resolve the byte-exact-vs-key-order-independent contradiction explicitly.
2. **[HIGH Cursor / MEDIUM Codex] The `gameIsRunning` fix is source-level, not semantic** (Cursor R3-2, Codex R3-5). Forbidding the `Game.IsRunning` *token* in the loop doesn't help if the worker invokes a `Func<bool>` that P/Invokes native `Game.IsRunning` before enqueue. **Both propose:** require a game-thread-updated cached scalar (`volatile`/`Interlocked`) that the worker reads, forbid a P/Invoking Func on the pipe thread, and add an acceptance criterion / test asserting the native read only happens on the game thread (Cursor: invocation-thread counting test; Codex: source assertion that no production path invokes it from the accept thread).
3. **[MEDIUM â€” both] Root reconciliation is operational, not verified** (Codex R3-2, Cursor R3-3). `--root` and the in-client auto-resolved client root can diverge at runtime (CWD drift, ini override) â†’ the agent previews a *different file* than MCP validated. Tests prove independent containment, not alignment. **Both propose:** expose the server's resolved client root (or a hash) in the `live_ping` ack as a runtime diagnostic, and document the alignment requirement.
4. **[MEDIUM â€” both] 16-02 Task 1 is too large** (Codex R3-4, Cursor R3-4). Split into protocol+goldens / server-core+lifecycle (+ keep the acceptance criteria). This is the one structural change both reviewers independently recommend.
5. **[MEDIUM Codex / LOW Cursor] `--no-build` verify after adding files** (Codex R3-3, Cursor R3-9) â€” can false-green stale binaries; use full `dotnet test` for tasks that add/edit source or csproj.
6. **[MEDIUM Cursor / LOW Codex] `validate-bundle` manifest schema fidelity / over-contracting** (Cursor R3-5, Codex R3-7) â€” source the schema verbatim from the real `export_manifest.py`/`export_bundle.py` shape (`client_cfg` lives inside `assets`); don't invent a contract the exporter hasn't committed to.

### Reviewer-Unique (worth noting)
- **[LOW, Codex] R3-6** â€” golden fixtures hold JSON payloads only (no 4-byte length prefix); "byte-vector" is slightly overstated (the framing fixture + integration test cover the prefix).
- **[MEDIUM, Cursor] R3-6** â€” extract a shared `IsContainedUnderRoot` so the mirrored absolute-containment branch can't drift from `LooseOverridePath` (incl. WR-07 root re-canonicalization for messy roots like `C:\bundle\..\bundle`).
- **[LOW, Cursor] R3-8** â€” `FormIffEditor` line numbers cited (`:1596-1630`) live in the UtinniPlugins repo and may drift; executor must grep.

### Divergent Views
- Only severity weighting differs: Cursor rates the `gameIsRunning` threading gap HIGH, Codex MEDIUM. Both rate the cross-serializer JSON HIGH. Both land overall **MEDIUM** and both say the same two items (wire contract + cached game-state) are the gate to confident execution.

### Recommendation
Both reviewers say: **fix R3-1 (wire contract) and R3-2 (cached `gameIsRunning`) in the plan text, and the wave is execution-ready.** These are now narrow, well-understood execution details â€” not architecture. This is round 3; the architecture has been stable across all three rounds and the remaining items are the kind normally caught in code review or a single rework loop. Reasonable paths:

- **Targeted replan (one more `--reviews` pass)** to bake in the wire-contract decision (canonical writer or request-byte-exact + ack-consume-only), the cached-game-state acceptance criterion, the 16-02 Task 1 split, the `--no-build`â†’`dotnet test` fixes, the `live_ping` root diagnostic, and the manifest-schema-from-source fix. Cheap relative to hitting them mid-execution.
- **Accept and handle at execution time** as planned deviations â€” viable given the architecture is sound and the fixes are local, but the cross-serializer byte-exact assumption is the one most likely to cause a real rework loop if not decided up front.

Feed into planning with: `/gsd:plan-phase 16 --reviews`


---

# Earlier Rounds (Round 2 + Round 1)

_Preserved verbatim below. Round 1 = C-01..C-19; Round 2 = CDX-NEW-*/CUR-NEW-* (all incorporated into the current plans)._

# Cross-AI Plan Review â€” Phase 16 (Round 2, post re-plan)

Second cross-AI review round, run against the **re-planned** 16-01/16-02/16-03 plans (commit `1de4883`) that already incorporated Round-1 findings C-01..C-19. Two independent external CLIs reviewed all three plans (Codex `codex exec`; Cursor agent via `cursor-agent.cmd`). `claude` was skipped for reviewer independence (orchestrated from inside Claude Code). Gemini, CodeRabbit, OpenCode, and Qwen are not installed.

Both reviewers agree the re-plan is a **material improvement** and is execution-ready *with targeted amendments* â€” not a rubber stamp. They independently converged on the same headline gap: **Tier-2 can go green while the real agent-facing path stays broken**, because the net10 `LivePipeClient` is never actually tested against the net472 server wire. See **Consensus Summary** at the end.

> Round-1 review (the C-01..C-19 findings the current plans cite) is preserved verbatim in the **Appendix** below.

---

## Codex Review (Round 2)

**Summary**

The re-plan is substantially stronger than a typical phase plan: it names the critical boundaries, preserves the out-of-proc MCP lock, adds server-side and MCP-side containment, and tries to prove protocol agreement with more than string equality. That said, the live bridge plan still has several execution risks around net472 named-pipe APIs, real MCP tool enumeration, thread ownership of `Game.IsRunning`, and whether the proposed cross-TFM tests actually prove the net10 client can speak to the net472 server. ECO-01 is lower risk, but `validate-bundle` has a few semantic ambiguities that could turn into brittle tests or misleading validation.

**Strengths**

- Clear preservation of Phase 14 architecture: MCP SDK remains out-of-proc; only a narrow named pipe crosses into the injected host.
- Good fail-closed posture: `--enable-live` hides MCP tools, and a separate in-client flag gates the listener.
- Minimal live verb surface is appropriate: `ping` and `reload-asset` only.
- Both sides of path containment are explicitly covered: MCP `ResolvedRoot.Resolve` before send, and in-client canonicalization before enqueue.
- The plan correctly avoids visible re-render as a success gate and requires `reloadAttempted=false` candor.
- The shared fixture for pipe name plus framing is a useful drift guard across the TFM wall.
- ECO-01 rightly reuses existing readers and limits `validate-bundle` to text/manifest validation instead of adding geometry decode.
- Fixture SHA pinning for Blender goldens is a good response to silent drift risk.

**Concerns**

- **HIGH â€” CDX-NEW-1: The cross-TFM protocol proof is still incomplete.** 16-03 says the real net472 integration test uses a "LiveBridgeProtocol-driven client," not the actual net10 `LivePipeClient`. Because net10 cannot reference net472, the plan proves the net472 server can talk to itself using its own protocol implementation, while the net10 side is only checked by fixture constants and loopback stubs. That does not fully prove net10 `System.Text.Json` payloads deserialize correctly with net472 `JavaScriptSerializer`.
- **HIGH â€” CDX-NEW-2: `Game.IsRunning` capture boundary is internally inconsistent.** The plan says `Game.IsRunning` must never be dereferenced on the worker thread, but also says the public handle captures `gameIsRunning` "at the marshal/enqueue boundary." If the pipe worker calls the handle and that handle reads `Game.IsRunning`, the violation remains. The only safe options: read it inside the enqueued game-thread action, inject it from a game-thread-owned state cache, or always return an `Unknown/queued`-style ack without reading native state on the worker.
- **HIGH â€” CDX-NEW-3: In-client `StartListener` gating may depend on config APIs not safe/available at that point.** The plan assumes `utinni.GetConfig().GetBool("Live","enableLiveBridge")` is cheap and safe during startup. If config access allocates, blocks, touches native state, or is unavailable before `Native.SignalLauncherReady`, this creates a new injection-startup failure mode. The try/catch avoids crash, but not hidden non-start or deadlock risk.
- **MEDIUM â€” CDX-NEW-4: Named-pipe security implementation may not work as planned on the target runtime/OS shape.** `PipeSecurity`/`PipeAccessRule` on .NET Framework is platform/API-sensitive. The plan doesn't specify exact constructor overloads, current-user SID resolution, owner rights, or how tests assert the ACL. "Contains PipeSecurity current-user ACL" is weaker than proving non-current-user denial (hard locally).
- **MEDIUM â€” CDX-NEW-5: Gating proof via "real MCP tool enumeration" may be brittle or impossible.** The plan acknowledges SDK 1.4.0 may not expose a clean enumeration surface, but still makes this a central acceptance criterion. If enumeration is impossible, "closest registration introspection" risks becoming a weak branch-level assertion. Needs a concrete fallback now, not during execution.
- **MEDIUM â€” CDX-NEW-6: Tool registration plan may conflict with `WithToolsFromAssembly()` behavior.** The plan assumes omitting `McpServerToolType` from `LiveTools` prevents method-level `[McpServerTool]` discovery by assembly scanning. That needs confirmation against actual SDK behavior; if method attributes are discovered independently, live tools could leak when `--enable-live` is off.
- **MEDIUM â€” CDX-NEW-7: Sequential single-client pipe server can starve or block live tools.** A connected client that stalls after connect or sends a slow frame can occupy the only accept loop until timeout/close. Server-side read timeouts/cancellation mechanics need to be explicit.
- **MEDIUM â€” CDX-NEW-8: `Stop/Dispose` for a blocking `NamedPipeServerStream.WaitForConnection` is underspecified.** On .NET Framework, cancellation support is limited depending on sync vs async API choice. "CTS + closing the pipe" may not reliably unblock all waits. Tests should prove disposal returns within a bounded time.
- **MEDIUM â€” CDX-NEW-9: `validate-bundle` success semantics may be misleading.** Structurally valid bundles exit 0 even with missing/rejected refs. The name "validate" implies nonzero on failure; if downstream agents use exit code only, a bundle with rejected traversal refs could be treated as valid.
- **MEDIUM â€” CDX-NEW-10: `validate-bundle` manifest schema is vague.** References `swg_export_manifest.json` shape but doesn't define required/optional fields, unknown-field handling, array vs object shapes, or versioning. Risk: a validator that passes the current fixture but isn't a stable contract.
- **MEDIUM â€” CDX-NEW-11: Path containment via string `StartsWith` needs separator and root-equality handling.** `bundleRootFull + Path.DirectorySeparatorChar` rejects the root itself unless special-cased, and can mishandle trailing/alternate separators. Fixable, but should be explicit.
- **LOW â€” CDX-NEW-12: Absolute path policy conflicts with local tool ergonomics.** ECO-01 rejects absolute refs inside manifests and `.rsp` RHS; Blender/export workflows may naturally emit absolute explicit paths. If the existing exporter does this today, the happy path may fail unless the contract forces relative bundle paths. *(Cursor rates this HIGH â€” see CUR-NEW-3.)*
- **LOW â€” CDX-NEW-13: Fixture location under `Utinni.Cli.Tests` is awkward for protocol fixtures.** A pipe protocol fixture shared by MCP and Core tests living in CLI test fixtures couples unrelated test projects to CLI test conventions.
- **LOW â€” CDX-NEW-14: Cross-repo pointer task is operationally ambiguous.** 16-01 is `autonomous: false`, but Task 4 asks for human approval and a commit in another repo. Success criteria should distinguish "Utinni complete" from "external pointer complete."

**Suggestions**

- Add a net10-generated request/ack **golden byte fixture** test: `Utinni.Mcp.Tests` writes canonical `ping`/`reload-asset` frames to files/embedded strings; `UtinniCoreDotNet.Tests` deserializes those exact bytes with `LiveBridgeProtocol`, and vice-versa for acks. Closes CDX-NEW-1 without cross-referencing projects.
- Resolve `Game.IsRunning` design before implementation: prefer `Func<bool> getGameIsRunningOnGameThread` invoked only inside an enqueued action, or have the ack avoid claiming current game state. No pipe-worker path calls `Game.IsRunning`.
- Define concrete server read/connect timeouts and prove them: stalled client after connect, length-prefix-only, slow body, oversized declared frame, dispose-while-waiting.
- Make the MCP gating fallback concrete: factor host registration into a testable method returning the configured builder/service collection; assert registered tool metadata if possible and assert absence of the `LivePipeClient` service when off regardless.
- Explicitly test whether `[McpServerTool]` methods in a class without `[McpServerToolType]` are ignored by `WithToolsFromAssembly()`. If not, move live tools to an excluded namespace/assembly, or register all tool types explicitly.
- For containment, use a helper that normalizes root once, accepts root equality where intended, appends a separator consistently, and tests case-insensitive Windows paths, UNC, drive-relative `C:foo`, mixed slashes, trailing spaces, and symlink/junction behavior.
- Decide whether `validate-bundle` exits nonzero on rejected refs. If keeping exit 0, name the envelope field clearly (`valid:false`, `findings`, `hasRejectedRefs`) so agents can't mistake transport success for validation success.
- Define the manifest schema in the contract and tests: required fields, allowed bundle types, path fields, `rsp_files` shape, `client_cfg`, unknown fields, schema/version behavior.

**Risk Assessment: MEDIUM-HIGH.** ECO-01 is mostly low-medium (main issue: contract/schema precision). MCP-03 is higher because it crosses process, TFM, serialization, named-pipe lifecycle, and injected-client threading boundaries. The two biggest remaining risks are material: the cross-impl proof doesn't yet prove the actual net10 client against the net472 server wire, and the `Game.IsRunning` boundary isn't clean enough for an injected native-backed host. Address those before execution.

---

## Cursor Review (Round 2)

**Summary**

The re-plan is a material improvement over the prior round: most C-01..C-19 findings are addressed with concrete tasks, acceptance criteria, and tests. Architecture remains correct â€” out-of-proc MCP, minimal `live_*` surface, game-thread marshal via `GameCallbacks.AddMainLoopCall`, ECO-01 as reader reuse + thin text verb. **Remaining risk is no longer "wrong architecture" but "Tier-2 green, Tier-4/agent path still broken or misleading."** Highest residual gaps: (a) MCP `--root` vs in-client containment root ambiguity, (b) no proof that net10 `LivePipeClient` interoperates with the real net472 server, (c) `validate-bundle` absolute-path policy likely incompatible with real Blender `.rsp` output, (d) dual enable flags with no operator contract.

**Strengths**

- Prior review closure is real, not cosmetic (C-01 main.cs wiring, C-03 JavaScriptSerializer, C-04/C-15 containment both sides, C-05 framing line 2, C-07/C-08 lifecycle + partial-read, C-14 candor, C-17 docâ†”verb parity, C-19 dotnet tests vs bash grep all landed).
- Wave ordering is sound; 16-03 correctly makes the `depends_on:[16-02]` edge protocol-load-bearing via `LiveBridgeIntegrationTests`.
- Cross-TFM agreement strategy (canonical `pipe-name.txt` linked into both test projects) is the right pattern given the TFM wall.
- Threading model matches shipped seams (background accept â†’ pure decision with injected `gameIsRunning` â†’ single `AddMainLoopCall`; allocations per-request, not per-frame).
- Fail-closed posture is layered (MCP `--enable-live` + gated `LivePipeClient` singleton + in-client listener flag + `ResolvedRoot.Resolve` before send).
- ECO-01 scope discipline holds (no geometry codec, TreVersion pitfall 4 corrected, SHA-256 drift guard, string-runner compile boundary, malformed tests).
- **Gating test has a proven precedent**: `RoundTripTests.Handshake_ListsExactlyTheTwelveNamedTools` already uses `McpClient.ListToolsAsync()` â€” C-10 is feasible if Task 3 mirrors that pattern with/without `--enable-live`. *(This is the concrete fallback Codex CDX-NEW-5 asks for.)*

**Concerns**

- **HIGH â€” CUR-NEW-1: MCP `--root` vs in-client containment root is unresolved.** 16-02 Task 3 says reuse `injectRoot` (Utinni DLL dir from `main.cs:62`) *or* "the resolved client root the save path uses" â€” but `injectRoot` â‰  SWG client root (`ResolvedRoot`/`--root`). MCP resolves assets under client root and sends an absolute path; server containment against `injectRoot` will reject every legitimate reload in production (or accept wrong paths if misconfigured). No acceptance test covers this cross-root scenario.
- **HIGH â€” CUR-NEW-2: C-02 is only half-closed.** `LiveBridgeIntegrationTests` (net472) exercises the real `LivePipeServer` against a **net472 `LiveBridgeProtocol` client**, not net10 `LivePipeClient`. Tier-2 can pass while the agent-facing client fails on the wire. Fixture line-2 + camelCase policy is necessary but not sufficient for the actual MCP dispatch path.
- **HIGH â€” CUR-NEW-3: `validate-bundle` rejects all absolute refs, but Blender `.rsp` uses absolute RHS paths.** `rsp_builder.py` writes `{treefile} @ {abspath}`. Task 2 treats *any* absolute manifest/.rsp ref as `rejectedRefs` without a "contained absolute is OK" branch. Happy-path fixtures are synthetic relative-only, so tests won't catch mismatch with real Blender exports â€” undermining D-08 cross-validation for the bundle text contract.
- **MEDIUM â€” CUR-NEW-4: JavaScriptSerializer â†” STJ camelCase alignment is underspecified.** Fixture declares `json=camelCase`; net10 uses `JsonNamingPolicy.CamelCase`. Default `JavaScriptSerializer` emits **PascalCase** unless DTOs use camelCase identifiers, `[ScriptProperty]`, or a custom resolver. Cross-impl test is net472-on-net472, so net10 casing bugs won't surface.
- **MEDIUM â€” CUR-NEW-5: `protocolVersion` is added (C-12) but mismatch handling is absent.** No server rejection of wrong version, no skew test, no forward-compat story. Silent partial parse or ignored fields possible.
- **MEDIUM â€” CUR-NEW-6: Dual enable flags with empty `user_setup`.** MCP `--enable-live` and in-client `Live.enableLiveBridge` must BOTH be ON for the real path. Not documented in `user_setup`, Tier-4 checklist, or contract doc â€” predictable "live_ping always false" operator failure.
- **MEDIUM â€” CUR-NEW-7: `validate-bundle` containment mirrors `StartsWith` but not `LooseOverridePath.Resolve`.** The project already has battle-tested containment in `LooseOverridePath` (explicit `..` segment scan, prefix-attack guard, root re-canonicalization). Plan reimplements a subset; symlink/junction escapes via `File.Exists` on contained paths remain unaddressed (T-16-01 partial). *(Converges with CDX-NEW-11.)*
- **MEDIUM â€” CUR-NEW-8: Assumption A1 still load-bearing until 16-03 Task 3.** Task 0 only `dotnet build`s. If SDK 1.4.0 requires class-level `[McpServerToolType]` for method discovery via `WithTools<LiveTools>()`, D-04 breaks late. *(Converges with CDX-NEW-6.)*
- **MEDIUM â€” CUR-NEW-9: `ReloadTier` wire format undocumented.** Enum serializes as integers by default on both stacks unless explicitly configured. Agents reading `"tier":2` vs `"PendingNextSceneChange"` is ambiguous; not in fixture, contract, or tests.
- **LOW â€” CUR-NEW-10: 16-01 Task 4 verify still uses bash `grep`** on a Windows-primary repo (C-19 fixed in Task 3 only).
- **LOW â€” CUR-NEW-11: Fixed pipe name `utinni-live-bridge`.** Second injected client / stale listener after crash is an operational footgun; no instance disambiguation.
- **LOW â€” CUR-NEW-12: D-01 "round-trip proven" vs placeholder reload.** `accepted:true` + `queued:true` + real classifier tier still reads as functional preview to agents unless tool descriptions are aggressively explicit â€” make it a gated acceptance string-match, not prose intent.

**Suggestions**

1. **Pin one client root for live reload (blocks CUR-NEW-1).** Specify exactly how `LivePipeServer` obtains the SWG client root (same semantics as MCP `--root`/`LogicalAssetPath`/save path â€” *not* `injectRoot`). Prefer a wire change: send `relativePath` (+ optional resolved path for debug), resolve server-side under the pinned client root via `LooseOverridePath.Resolve`. Add integration test: MCP root = temp client tree, server root = same tree, contained reload succeeds; mismatched roots fail predictably.
2. **Close the net10â†”net472 wire gap (CUR-NEW-2).** Add shared **golden wire byte vectors** (ping/reload request+ack) asserted by both `LivePipeProtocolTests` and `LiveBridgeIntegrationTests`; or a subprocess test (net472 server + spawned net10 client). Minimum: the net472 integration test uses byte-exact frames that net10 tests also assert the client produces/consumes. *(Identical to CDX suggestion for CDX-NEW-1.)*
3. **Fix `validate-bundle` absolute-path policy (CUR-NEW-3).** Allow absolute RHS when the canonical path is contained under the bundle root; reject only escaping/absent absolutes. Add a Blender-faithful absolute-`.rsp` fixture. Document in D-06 surface (1).
4. **Make JSON casing explicit (CUR-NEW-4).** Use camelCase C# property names on wire DTOs or document `[ScriptProperty]` on every field; add a test asserting serialized ping-request bytes match a pinned golden string on **both** sides.
5. **Handle `protocolVersion` mismatch (CUR-NEW-5).** Server returns a structured reject ack when `protocolVersion != 1`; test on both sides.
6. **Document dual-flag operator contract (CUR-NEW-6).** Add to `16-VALIDATION.md` Tier-4 checklist and plan `user_setup`: both flags required, neither alone sufficient.
7. **Reuse `LooseOverridePath` for bundle containment (CUR-NEW-7).** Delegate relative-ref containment to the shared helper; document symlink policy.
8. **Lock A1 early in Task 0.** Task 0 should emit a failingâ†’passing enumeration test, not just `dotnet build`.
9. **Document `ReloadTier` wire encoding (CUR-NEW-9).** Pin as integer 0â€“3 or string names; round-trip test in cross-impl test; mention in the MCP tool result schema.
10. **Extend ECO-01 success criteria.** Make the Task-4 human checkpoint visibly non-gating for merge but gating for ECO-01 "done" in the phase verification table.

**Risk Assessment: MEDIUM.** 16-01 ECO-01 = LOWâ€“MEDIUM (CUR-NEW-3 could invalidate the primary bundle validator). 16-02+16-03 MCP-03 = MEDIUM (architecture sound; CUR-NEW-1 can make the whole live tier non-functional in real use while all tests pass; CUR-NEW-2 + CUR-NEW-4 leave the agent-facing wire path unproven end-to-end). Phase-goal verdict: D-06/ECO-01 likely achievable after CUR-NEW-3; D-01 Tier-2 achievable; **D-01 real agent path not yet guaranteed** (root alignment + net10 wire proof + dual-flag ops doc missing); D-04 likely achievable if the enumeration test lands. **Fix CUR-NEW-1 and CUR-NEW-3 before Wave 1 execution; fix CUR-NEW-2/CUR-NEW-4 before calling MCP-03 complete.**

---

## Consensus Summary

Two independent reviewers, strong convergence. The re-plan correctly closed Round-1's C-01..C-19; the new findings are second-order *enforcement* gaps, not architectural reversals. The dominant theme both reviewers reached independently: **the test suite can be fully green while the real agent path is broken**, because the only thing never exercised end-to-end is the actual net10 `LivePipeClient` â†” net472 `LivePipeServer` wire.

### Agreed Strengths (2+ reviewers)
- Out-of-proc MCP architecture preserved; only a narrow named pipe crosses into `SWG.exe`.
- Minimal `live_*` surface (`ping` + `reload-asset`) and layered fail-closed gating.
- Path containment addressed on **both** the MCP side and the in-client server side.
- Reload candor (`reloadAttempted=false`, transport-only note); visible re-render correctly non-gating.
- Shared pipe-name/framing fixture as a cross-TFM drift guard; SHA-256 fixture provenance.
- ECO-01 reader-reuse discipline (no geometry codec; TreVersion pitfall-4 corrected).

### Agreed Concerns (raised by both â€” highest priority)
1. **[HIGH] Cross-TFM wire proof is incomplete** (CDX-NEW-1 = CUR-NEW-2). The net472 integration test proves net472-against-itself; the net10 client is only checked by fixture constants + loopback stubs. **Both propose the same fix: shared golden wire byte-vectors (ping/reload request+ack) asserted by both test projects, or a subprocess net10-clientâ†”net472-server test.** This is the single most important amendment.
2. **[HIGH per Cursor / LOW per Codex] `validate-bundle` absolute-path policy breaks real Blender output** (CUR-NEW-3 = CDX-NEW-12). `rsp_builder.py` emits absolute RHS paths; rejecting *all* absolutes makes the validator fail real bundles while synthetic relative-only fixtures stay green. Fix: allow **contained** absolutes; add a Blender-faithful fixture.
3. **[MEDIUM] camelCase serialization alignment is unverified** (CUR-NEW-4, amplifies CDX-NEW-1). `JavaScriptSerializer` defaults to PascalCase; the net472-on-net472 test will never catch a net10 casing mismatch. Fix folds into the golden-byte-vector test above.
4. **[MEDIUM] Containment via `StartsWith` is under-specified; reuse the existing helper** (CDX-NEW-11 = CUR-NEW-7). Both flag separator/root-equality edge cases. Cursor adds the concrete fix: delegate to the battle-tested `LooseOverridePath.Resolve` instead of reimplementing a subset.
5. **[MEDIUM] D-04 gating proof feasibility** (CDX-NEW-5/6 = CUR-NEW-8). Both worry the SDK-1.4.0 enumeration + the no-class-attribute discovery assumption could break late. **Divergence resolves in the plan's favor:** Cursor found the concrete precedent Codex asked for â€” `RoundTripTests.Handshake_ListsExactlyTheTwelveNamedTools` already calls `McpClient.ListToolsAsync()`, so the real-enumeration assertion is feasible. Mirror that pattern with/without `--enable-live` and lock it in Task 0, not Task 3.

### Reviewer-Unique HIGHs (one reviewer, but concrete and serious)
- **[HIGH, Cursor only] CUR-NEW-1 â€” MCP `--root` vs in-client `injectRoot` mismatch.** Server containment against `injectRoot` (the Utinni DLL dir) â‰  the SWG client root the MCP side resolves under, so *every* legitimate reload could be rejected in production while tests pass. Resolve which root the server pins, and add a cross-root integration test.
- **[HIGH, Codex only] CDX-NEW-2 â€” `Game.IsRunning` capture boundary self-contradiction.** If the worker thread calls a handle that reads native-backed `Game.IsRunning`, the "never deref off the game thread" rule is still violated. Decide the mechanism (read inside the enqueued action, or inject a game-thread-owned cached bool) before implementation.

### Divergent Views
- **Risk level:** Codex says MEDIUM-HIGH; Cursor says MEDIUM. The delta is mostly Codex weighting the net472 named-pipe lifecycle/ACL mechanics (CDX-NEW-4/7/8) and the `Game.IsRunning` boundary higher; Cursor treats the threading model as essentially sound and weights the root-mismatch operational gap higher.
- **Absolute-path severity:** Cursor HIGH vs Codex LOW for the same `validate-bundle` issue â€” Cursor checked `rsp_builder.py` and found it emits absolute paths, making this a real interop break rather than a theoretical ergonomics nit. Treat as HIGH.
- **Enumeration feasibility:** Codex pessimistic (wants a fallback now); Cursor optimistic (found the working precedent). The precedent settles it â€” feasible.

### Recommended action before execution
Fix the two HIGH interop blockers first â€” **(1) the net10â†”net472 golden-byte-vector wire proof** and **(2/CUR-NEW-1) the live-reload client-root definition + cross-root test** â€” plus **(CUR-NEW-3) the contained-absolute `.rsp` policy**. These three are the ones that let a fully-green Tier-2 hide a broken real path. The rest (protocolVersion mismatch handling, ReloadTier wire encoding, dual-flag ops doc, `LooseOverridePath` reuse, lifecycle timeouts, A1 lock-in Task 0) are MEDIUM amendments that can ride into the same replan.

Feed into planning with: `/gsd:plan-phase 16 --reviews`


---

# Appendix: Round 1 Review (C-01..C-19, incorporated into the current plans)

_Preserved verbatim. The current plans cite these C-codes as REVIEW-DRIVEN EDITS._

# Cross-AI Plan Review â€” Phase 16

Two independent external AI CLIs reviewed all three Phase-16 plans (Codex `codex-cli 0.137.0`; Cursor agent via `cursor-agent`). `claude` was skipped because this review was orchestrated from inside Claude Code. Gemini, CodeRabbit, OpenCode, and Qwen are not installed on this machine.

Both reviewers independently converged on the same top-tier risks â€” the strongest signal in the report. See the **Consensus Summary** at the end.

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

- **HIGH â€” 16-02 Task 1 / 16-03 Task 1: framing descriptor is not actually contract-enforced.** The fixture has line 2 `frame=len32le+utf8json`, but acceptance only asserts line 1 equals `PipeName`. Both sides could silently drift on max frame size, JSON casing, required fields, error shape, or length-prefix behavior while still passing the pipe-name equality tests.
- **HIGH â€” 16-02 Task 1: named-pipe server lifecycle is underspecified.** The plan says "background-thread accept/read loop" but does not define cancellation, disposal, restart behavior, single vs multiple concurrent clients, repeated sequential connections, or behavior after a malformed frame closes one connection. A bug here can pass unit tests but hang or leak inside the injected client.
- **HIGH â€” 16-02 Task 1 / D-01: `reload-asset` risks shipping as a misleading no-op.** The plan is honest that the enqueued action is a TODO placeholder, but the ack fields include `accepted`, `queued`, and a classifier tier. That can read as "the preview action happened" when no reload binding exists. This satisfies "round-trip proven" only if the result schema clearly distinguishes `queuedPlaceholder` / `reloadAttempted=false` from real apply/reload.
- **HIGH â€” 16-03 Task 0 / Task 2: fail-closed tool hiding depends on fragile SDK registration behavior.** The plan relies on `LiveTools` lacking `McpServerToolType` so `WithToolsFromAssembly()` does not pick it up, then conditionally uses `WithTools<LiveTools>()`. That may be valid, but it is SDK-version-sensitive. The test must inspect the actual advertised tool list through the same host-building path, not just assert a branch or comment.
- **MEDIUM â€” 16-02 Task 1: current-user pipe ACL is probably not sufficient by itself.** Same-user local malware or another agent process can still trigger reloads if the pipe is running. Given `--enable-live` gates only MCP tool registration, the in-client server also needs its own start gate or nonce/session handshake if it can ever be launched independently.
- **MEDIUM â€” 16-03 Task 2: `live_ping` bypasses root gating by design, but still advertises live presence.** Probably acceptable, but the security doc should call out that enabling live exposes process liveness/game-running/pid to any MCP client with access to the server.
- **MEDIUM â€” 16-03 Task 2: absolute path crosses the pipe.** `live_reload_asset` resolves under `ResolvedRoot`, then sends the absolute path into the injected process. Safer than raw relative input, but can leak local filesystem layout and may not match the game's asset namespace. Consider sending both canonical relative path and resolved absolute path, or only the relative SWG asset path if the server does not need disk access.
- **MEDIUM â€” 16-02 Task 1: `ReloadAssetClassifier.Classify(ext, null)` may under-classify object templates or root-type-specific reloads.** Passing `null` is acceptable for minimal scope, but the ack tier may be less accurate than advertised. The plan should specify which asset classes are supported by Phase 16 and which return `PendingNextSceneChange` or `Unavailable`.
- **MEDIUM â€” 16-02 Task 1: heap-free hot path claim is slightly muddled.** The pipe server necessarily allocates on requests, which is fine because it is not per-frame. The plan should explicitly say no new per-frame allocations are introduced; the queued `Action` itself is an allocation but only per request.
- **MEDIUM â€” 16-01 Task 2: path traversal mitigation for bundle validation is too vague.** It says out-of-bundle refs are findings, but acceptance does not require rejecting absolute paths, `..`, symlinks/junction escapes, or mixed separators. This is the most likely ECO-01 security gap.
- **MEDIUM â€” 16-01 Task 1: fixture provenance is under-specified.** "Copy verbatim" is good, but there is no hash/checksum acceptance criterion. A future fixture refresh could silently change the cross-validation bytes.
- **MEDIUM â€” 16-03 Task 3: loopback tests may not cover server compatibility.** The client test uses its own stub "mirror" of the protocol. If both client and stub share the same duplicated wrong assumption, tests pass while the net472 server disagrees. A shared JSON/framing conformance vector would reduce this.
- **LOW â€” 16-01 Task 2: `validate-bundle` name may imply binary validation.** The plan says text-only, but users may expect `.msh`/`.tre` semantic validation. The output should clearly say it validates bundle references/search-path text, not mesh correctness.
- **LOW â€” 16-02 Task 2: optional env alias weakens explicit enable semantics.** `UTINNI_MCP_ENABLE_LIVE` may be convenient, but it makes live enablement less visible than a launch flag. If kept, it should require explicit truthy values, not "any non-empty".
- **LOW â€” 16-03 Task 1: no-client behavior differs by operation.** Ping maps to `{ listening:false }`; reload likely maps to hard error. That distinction should be explicit and tested so agents do not interpret reload transport failure inconsistently.

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

These plans are unusually well-grounded in shipped seams (`GameCallbacks.AddMainLoopCall`, `ReloadAssetClassifier`, `CliDispatcher`, `ServerArgs`, Phase-13 readers) and correctly split ECO-01 (low risk, wave 1) from MCP-03 (new mechanism, wave 2 with a real `depends_on: [16-02]`). The pipe-name fixture anchor is a pragmatic fix for the net472/net10 project-reference wall. However, MCP-03 has a **load-bearing integration hole**: **16-02 builds `LivePipeServer` + `StartListener` but no plan wires startup or runs a clientâ†”server cross-implementation test**, so Tier-2 can go green while Tier-4 manual and real agent preview still fail silently. Secondary gaps include wrong JSON stack assumptions on net472, framing-descriptor enforcement only on paper, missing in-client path containment on `reload-asset`, and a wrong/missing `MCP-SECURITY.md` target path.

### 2. Strengths

- **Architecture reuse is correct.** 16-02's threading model (background accept/read â†’ single `Action` on `mainLoopCallQueue`) matches how `GameCallbacks` is actually used today and respects the heap-free hot-path constraint (research A2).
- **Honest ack semantics.** Reusing `ReloadAssetClassifier` for tier reporting + explicitly overriding `!gameIsRunning â†’ Unavailable` aligns with D-01 (`ReloadAssetClassifier.cs:78-80`).
- **D-04 is taken seriously.** 16-03 Task 0 (Assumption A1 spike) + conditional `WithTools<LiveTools>()` + explicit "no `[McpServerToolType]` on `LiveTools`" matches the real `Program.cs:74` `WithToolsFromAssembly()` scan.
- **Cross-TFM agreement mechanism is sound in principle.** Canonical `pipe-name.txt` linked into both test projects with byte-equality assertions is the right pattern when net10 cannot project-ref net472.
- **ECO-01 scope discipline.** Thin text-only `validate-bundle`, golden cross-validation via existing `parse-tre`/`decode-iff`, DEC-A3 honored, TreVersion pitfall 4 called out â€” all match research B1/B4.
- **16-01 Task 1 compile-boundary fix (WARNING-3).** String-runner `ValidateBundleTests` without type references to not-yet-built command code is a mature incremental-test pattern.
- **Never-hang discipline is explicitly cloned** from `CliDispatcher.cs:121-138` for the pipe client, with injectable timeouts and stub-server loopback.
- **D-01 placeholder is labeled honestly** in 16-02 Task 1 (`TODO(RESID-03)` on the enqueued Action).

### 3. Concerns

**MCP-03 track**

| ID | Severity | Concern |
|----|----------|---------|
| **C-01** | **HIGH** | **No plan wires `LivePipeServer.StartListener()` into `Startup.EntryPoint` (`main.cs`).** 16-02 Task 1 says "expose StartListener so the host decides" but **no task in 16-02 or 16-03 calls it**. Without this, `live_ping` always returns `{ listening: false }` in a real injected client regardless of `--enable-live`. Tier-2 loopback tests use stubs, so they won't catch this. |
| **C-02** | **HIGH** | **No cross-implementation integration test.** 16-03 Task 3 tests `LivePipeClient` against a **test stub server**, not `LivePipeServer` from 16-02. The `depends_on: [16-02]` edge is enforced only by **pipe-name literal equality**, not by proving the two implementations speak the same framing/envelope on the wire. Drift in JSON field names, tier serialization, or read-loop behavior can pass all tests. |
| **C-03** | **HIGH** | **`LiveBridgeProtocol` specifies `System.Text.Json` on net472, but `UtinniCoreDotNet` has no JSON dependency today** (uses `JavaScriptSerializer` elsewhere, e.g. `ObjectTemplateSchemaLoader.cs`). Claiming "BCL-only (System.Text.Json)" in 16-02 acceptance criteria is **incorrect for net472** unless a package/reference is added â€” which triggers the project's Package Legitimacy Gate that 16-02 threat model says is "not triggered." |
| **C-04** | **HIGH** | **In-client `reload-asset` has no path containment.** 16-03 correctly runs `ResolvedRoot.Resolve` before send, but 16-02 sends the **absolute path over the pipe** and the server enqueues a reload Action with **no equivalent root guard**. Any same-user local process that connects to `utinni-live-bridge` (T-16-10 mitigates cross-user, not same-user) can trigger reload attempts on arbitrary filesystem paths. T-16-21 covers MCP-side escape only. |
| **C-05** | **MEDIUM** | **Framing descriptor line 2 of `pipe-name.txt` is documentation-only.** Tests assert line 1 byte-equals `PipeName` on both sides, but `frame=len32le+utf8json` is never parsed or enforced. Duplicated framing code in net472 vs net10 can diverge (message mode vs length-prefix, endianness, max size) while pipe-name tests stay green. |
| **C-06** | **MEDIUM** | **D-04 gating is one-sided.** `--enable-live` gates MCP tools, but 16-02 defers in-client listener gating (research Open Q1). If `StartListener` is wired always-on, any local process can hit the pipe even when the agent tier is off â€” weaker than D-04's "fail-closed by absence" spirit. |
| **C-07** | **MEDIUM** | **Concurrent connections / server lifecycle unspecified.** No task covers `MaxAllowedServerInstances`, one-client-at-a-time vs parallel accepts, behavior when the accept loop throws, server restart after disconnect, or one-request-per-connection. Named-pipe servers commonly deadlock or leak instances without this. |
| **C-08** | **MEDIUM** | **Partial-read handling is client-tested only (16-03 Task 3).** 16-02 `LivePipeServerTests` lists oversize rejection but **not partial reads or mid-message disconnect on the server read loop** â€” asymmetric coverage. |
| **C-09** | **MEDIUM** | **Runtime `gameIsRunning` wiring is underspecified.** Pure tests inject a bool (good), but the production `Handle` path must read `Game.IsRunning` somewhere. Plan doesn't say where (worker vs enqueue boundary) or whether that read is thread-safe â€” Pitfall 2 says no game-state deref on worker, but `Game.IsRunning` may be a native-backed flag. |
| **C-10** | **MEDIUM** | **Assumption A1 remains load-bearing until Task 3.** Task 0 only `dotnet build`s; the actual "tools absent/present" assertion is deferred. If SDK 1.4.0 requires `[McpServerToolType]` for `WithTools<T>()` to discover methods, D-04 breaks late in wave 2. |
| **C-11** | **MEDIUM** | **`MCP-SECURITY.md` target path is wrong.** 16-03 Task 3 modifies `Utinni.Mcp/MCP-SECURITY.md`, but the repo only has `.planning/phases/14-.../MCP-SECURITY.md` â€” nothing under `Utinni.Mcp/`. Task will fail or create an untracked doc in the wrong place relative to Phase 14's stated deliverable. |
| **C-12** | **MEDIUM** | **Protocol version skew has no negotiation.** Requests have `{ op, path, ext }` with no `protocolVersion`; acks have `schemaVersion`. Field renames or tier enum serialization changes fail at runtime with no explicit mismatch error. |
| **C-13** | **LOW** | **`LivePipeClient` is registered unconditionally in DI (16-03 Task 2)** even when `EnableLive` is false. Harmless if unused, but inconsistent with fail-closed posture. |
| **C-14** | **LOW** | **D-01 placeholder risks "hollow feature" perception.** The TODO is explicit, but agents/users calling `live_reload_asset` get `queued: true` with a no-op Action â€” easy to misread as functional preview unless MCP tool descriptions and ack copy scream "transport-only until RESID-03." |

**ECO-01 track (16-01)**

| ID | Severity | Concern |
|----|----------|---------|
| **C-15** | **MEDIUM** | **`validate-bundle` path traversal is threat-modeled (T-16-01) but not specified in Task 2 action.** Needs explicit rules: reject absolute paths in manifest/RSP, normalize `..`, don't `File.Exists` on attacker-controlled absolute refs. Without acceptance tests, implementers may "check existence" via raw manifest strings. |
| **C-16** | **MEDIUM** | **Task 2 acceptance mentions malformed-manifest exit 2 but Task 1 scaffold only covers success + missing-manifest.** Malformed JSON/RSP cases may be skipped unless added explicitly. |
| **C-17** | **MEDIUM** | **Task ordering: contract doc (Task 3) follows verb (Task 2).** `validate-bundle` bucket rules are sourced from external `rsp_builder.py` at impl time â€” doc drift risk if Task 3 wording diverges from Task 2 code. No "doc â†” verb parity" test. |
| **C-18** | **LOW** | **Task 4 human checkpoint blocks autonomous ECO-01 completion** (by design). D-05 success depends on maintainer approval; flag in milestone acceptance, not only as a resume gate. |
| **C-19** | **LOW** | **Task 3 verify uses bash `test -f` / `grep` on a Windows-primary repo.** May fail in PowerShell-only CI unless Git Bash is guaranteed. |
| **C-20** | **LOW** | **External fixture copy step depends on `D:/Code/swg-blender-plugin` at authoring time.** Committed fixtures insulate CI, but document fallback if absent. |

**Cross-plan / dependency**

| ID | Severity | Concern |
|----|----------|---------|
| **C-21** | **MEDIUM** | **Wave-1 plans (16-01, 16-02) are independent â€” good â€” but 16-02 Task 2 (`EnableLive`) lands before 16-03 consumes it with no intermediate verification** that `Program.cs` gating works end-to-end until all of 16-03 completes. |
| **C-22** | **LOW** | **Fixture lives under `Utinni.Cli.Tests/Fixtures/live/` for a cross-cutting protocol concern.** Works, but couples MCP/core tests to CLI test project layout. |

### 4. Suggestions

**MCP-03 (16-02 / 16-03)**

1. **Add 16-02 Task 3 (or extend Task 1): wire `LivePipeServer.StartListener()` in `UtinniCoreDotNet/main.cs`** after `GameCallbacks.Initialize()`. Add a pure-managed test that `StartListener` is idempotent and doesn't double-spawn accept loops.
2. **Add one Tier-2 integration test:** spin up real `LivePipeServer` on a randomized pipe name, point `LivePipeClient` at it, assert `ping` + `reload-asset` round-trip. Makes `depends_on: [16-02]` protocol-load-bearing, not just name-load-bearing.
3. **Fix JSON stack in 16-02:** use `JavaScriptSerializer` or manual framing + minimal parsing to stay truly in-box on net472, *or* add an explicit approved-package step with legitimacy gate. Align net10 client serialization field-for-field.
4. **Enforce framing descriptor line 2 in tests:** parse `pipe-name.txt` line 2 in both suites and assert both implementations' framing constant matches.
5. **In-client path policy for `reload-asset`:** either (a) send relative paths only and resolve inside the injected host against a pinned root, or (b) validate absolute paths against an allow-list. Add server-side tests for rejected paths.
6. **Close D-04 defense-in-depth loop:** gate `StartListener` on an in-client flag and document that MCP `--enable-live` alone is insufficient for listener start â€” or accept the asymmetry explicitly in `MCP-SECURITY.md`.
7. **Specify pipe server concurrency contract:** one instance, one active client, one request per connection (or persistent session) â€” test server restart after disconnect.
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

1. **Add a Phase-16 "integration checklist" row:** client â†” real server â†” `StartListener` wired â€” distinct from loopback stub and Tier-4 manual.
2. **Document tier enum wire format** (`PendingNextSceneChange` string vs int) in `LiveBridgeProtocol` and test both ends.

### 5. Risk Assessment: **MEDIUM-HIGH**

ECO-01 (16-01) is **LOWâ€“MEDIUM** â€” mostly doc + thin CLI + golden tests with strong existing reader coverage; main risks are path-validation gaps and the human-gated cross-repo pointer.

MCP-03 (16-02 + 16-03) is **MEDIUM-HIGH** despite excellent architectural choices, because the **automated proof does not yet close the real delivery path**: listener startup is unplanned, client/server implementations are not integration-tested together, JSON/framing duplication is under-enforced, and in-client reload-path trust is incomplete. The plans **can** satisfy D-01 at Tier-2 only if "pipe" means stub server; they **do not yet guarantee** the injected-client bridge works beyond manual Tier-4.

The D-01 no-op reload Action is **honest and aligned with locked decisions**, but product risk remains that users/agents interpret `queued: true` as functional preview â€” mitigate via tool docs and ack copy.

**Quick answers to focused questions**

| Question | Verdict |
|----------|---------|
| Pipe-name fixture robust? | **Partially.** Line 1 enforced; line 2 not tested. |
| Purity/injection pattern sound? | **Yes for unit tests**; production `Game.IsRunning` wiring needs one explicit sentence. |
| `WithToolsFromAssembly()` accidentally picks up `LiveTools`? | **Plan correctly prevents it** â€” still verify at SDK 1.4.0. |
| Does Phase 16 achieve stated goals? | **ECO-01: likely yes** (minus human checkpoint). **MCP-03 Tier-2: yes vs stubs; real bridge: gap on startup wiring + cross-impl test.** |
| D-01 placeholder honest or hollow? | **Honest in code comments; hollow in user-facing behavior** unless tool descriptions reinforce it. |

---

## Consensus Summary

Both reviewers rate ECO-01 (16-01) **LOWâ€“MEDIUM** and MCP-03 (16-02/16-03) the real risk surface (Codex: MEDIUM overall; Cursor: MEDIUM-HIGH). Neither found an architectural flaw â€” the out-of-proc lock, minimal D-02 surface, reuse of shipped seams, and D-01 candor are all praised. **The risk is uniformly "the plan can go Tier-2 green while leaving real operational/integration gaps."**

### Agreed Strengths (both reviewers)
- Out-of-proc MCP architecture preserved; only the narrow named pipe crosses into `SWG.exe`.
- 16-02 threading model is correct: parse/decide on worker thread, enqueue exactly one `Action` onto the game-thread queue; heap-free hot path respected.
- ECO-01 scope discipline: Utinni reads / Blender writes, no geometry codec (DEC-A3), TreVersion pitfall-4 explicitly corrected (5000 readable, 6000 enumerate-only, COT2000 not a version).
- 16-01 Task 1 string-dispatch test pattern keeps the assembly compiling at the task boundary (WARNING-3 fix).
- `CliDispatcher` never-hang discipline correctly cloned for the pipe client; `ResolvedRoot.Resolve` runs before send on the MCP side.
- D-01 reload placeholder is honestly labeled in code.

### Agreed Concerns (raised by BOTH â€” highest priority)
1. **[HIGH] Framing descriptor (line 2 of `pipe-name.txt`) is not enforced.** Only the pipe name (line 1) gets byte-equality tests; the `frame=len32le+utf8json` framing can silently drift between the net472 and net10 duplications (max size, endianness, encoding, field names) while all tests stay green. *(Codex HIGH / Cursor C-05.)*
2. **[HIGH] No cross-implementation integration test.** `LivePipeClient` is only tested against a test-stub server, never against the real 16-02 `LivePipeServer`. The `depends_on: [16-02]` edge is enforced only by pipe-name literal equality, not by proving both ends speak the same wire protocol. *(Cursor C-02 HIGH / Codex MEDIUM "loopback may not cover server compatibility.")*
3. **[HIGH] In-client `reload-asset` has no path containment.** The MCP side resolves under `ResolvedRoot`, but 16-02's server enqueues a reload for the absolute path it receives with no equivalent guard â€” a same-user local process on the pipe can target arbitrary paths. *(Cursor C-04 HIGH / Codex MEDIUM.)*
4. **[HIGH] Fail-closed `--enable-live` gating depends on fragile, SDK-version-sensitive `WithToolsFromAssembly()` behavior.** Both insist the gating test inspect the **actually-advertised tool list through the real host-building path**, not a branch/comment assertion. *(Codex HIGH / Cursor C-10 MEDIUM.)*
5. **[HIGH/MEDIUM] Named-pipe server lifecycle is underspecified:** cancellation/disposal, concurrent vs sequential clients, restart after disconnect, accept-loop-throw recovery, one-request-per-connection, server-side partial-read / mid-message disconnect. Can pass unit tests but hang/leak inside the injected client. *(Codex HIGH / Cursor C-07+C-08 MEDIUM.)*
6. **[HIGH/LOW] `reload-asset` ack candor.** `accepted/queued/tier` reads as "preview happened" when the Action is a no-op. Both want the ack schema + MCP tool description to explicitly say "transport-only / `reloadAttempted=false` until RESID-03." *(Codex HIGH / Cursor C-14 LOW.)*
7. **[MEDIUM] `validate-bundle` path-traversal mitigation is too vague** â€” threat-modeled (T-16-01) but no acceptance rule to reject absolute paths / `..` / symlink escape; needs `Path.GetFullPath` canonicalization + tests. *(Both MEDIUM, Codex + Cursor C-15.)*
8. **[MEDIUM] D-04 gate is one-sided / current-user ACL alone is weak** â€” the in-client listener start isn't itself gated; a same-user process can reach the pipe even when the agent tier is off. Both suggest an in-client start gate (config/env/nonce) as defense-in-depth. *(Codex MEDIUM / Cursor C-06.)*

### Divergent / Unique-but-important Views
- **Cursor uniquely flagged two HIGH issues Codex missed â€” both look like genuine plan defects worth fixing before execution:**
  - **C-01 [HIGH]: `LivePipeServer.StartListener()` is never wired into startup (`main.cs`).** If true, `live_ping` returns `listening:false` in a real client regardless of `--enable-live` â€” the bridge silently never runs in-client, and Tier-2 stubs won't catch it. **This is the single most important finding in the report.**
  - **C-03 [HIGH]: `System.Text.Json` "BCL-only" claim is wrong for net472.** `UtinniCoreDotNet` has no STJ dependency today (uses `JavaScriptSerializer`); adding STJ pulls a package, contradicting 16-02's "Package Legitimacy Gate not triggered." Fix the serializer choice or add the package step honestly.
  - **C-11 [MEDIUM]: `MCP-SECURITY.md` is at `.planning/phases/14-.../`, not `Utinni.Mcp/`** â€” 16-03 Task 3's edit target path is wrong.
- **Codex uniquely flagged:** fixture provenance needs **SHA-256 hashes** asserted in golden tests (silent fixture-refresh drift); and `ReloadAssetClassifier.Classify(ext, null)` may **under-classify** root-type-specific reloads â€” document the supported asset classes.
- **Overall rating divergence is small and explained:** Codex MEDIUM vs Cursor MEDIUM-HIGH â€” the delta is exactly Cursor's two unique HIGH findings (StartListener wiring + net472 JSON stack).

### Recommended pre-execution actions (consensus-ordered)
1. **Verify & fix C-01** (StartListener wiring into `main.cs`) â€” add an explicit task; this is load-bearing for the whole MCP-03 deliverable.
2. **Verify & fix C-03** (net472 JSON serializer / package-gate honesty).
3. **Add a real clientâ†”server integration test** (C-02) and **enforce framing line 2** (#1) â€” turn the `depends_on` edge into a wire-protocol proof.
4. **Add in-client path containment + server-side lifecycle/partial-read tests** (C-04, #5).
5. **Make the D-04 gating test assert the advertised tool list** through the real host path (#4); add in-client start-gate defense-in-depth (#8).
6. **Reshape the reload ack + tool description for candor** (#6); fix the `MCP-SECURITY.md` path (C-11).
7. **ECO-01:** tighten `validate-bundle` path containment + tests (#7), add fixture SHA-256 (Codex), add malformed-manifest tests, and a docâ†”verb parity check.

To incorporate this feedback into planning:
```
/gsd:plan-phase 16 --reviews
```


