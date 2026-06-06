# Phase 14: Headless MCP server (`Utinni.Mcp`) — the centerpiece - Context

**Gathered:** 2026-06-05
**Status:** Ready for planning

<domain>
## Phase Boundary

Build a separate **net10** `Utinni.Mcp` console process that speaks **MCP over stdio** and dispatches every tool call to a `Process.Start` of the existing `utinni-cli.exe` (net472, x86). The server owns **ZERO** format/business logic — it is a dispatch shim plus a first-class, design-time security contract (`MCP-SECURITY.md`). Delivers MCP-01 (read tools) + MCP-02 (edit/save tools + security register).

**In scope:** the net10 stdio MCP server; read tools wrapping the existing `utinni-cli` read/inspect verbs; write tools wrapping the net-new `save` verb (loose-override default) + the off-by-default `dry_run`-gated `.tre` repack; `resolvedRoot` fail-closed pinning + path-escape test; `MCP-SECURITY.md` threat register; a real-MCP-client round-trip test (handshake + one read + one edit→save).

**Out of scope:** the LIVE-injected MCP bridge / named-pipe IPC into the x86 host (MCP-03 → Phase 16); Wave-2 editors (Phase 15); any new format/codec or CLI-verb work (all verbs already ship from Phase 13 — see code_context). No HTTP/SSE transport.

**Key precondition (verified):** `Utinni.Cli/Commands/` already contains every verb this phase wraps — `ParseTre`, `ListObjects`, `InspectIff`, `DecodeIff` (read); `RoundtripIff/Ot/Stf/Tab` (edit-roundtrip); `Save` (net-new write surface, all 4 formats); `RepackTre` (destructive); `BuildTre`, `CompileTemplate`, `CompileDefinition`, `CompileDatatable`, `ExportArmor`, `ExportWeapon` (build). The CLI emits sorted-key JSON envelopes. Phase 14 adds no verbs.
</domain>

<decisions>
## Implementation Decisions

**Discussion outcome:** The user reviewed the four open implementation decisions below and explicitly **delegated all four to research** ("I want Claude to decide based on research"). They are therefore recorded as **research-directed decisions**: each carries a recommended lean and the bounding constraints, and the `gsd-phase-researcher` resolves the final choice against current facts (MCP spec/SDK state, net10 availability, the live CLI surface). None require returning to the user before planning.

### D-01 — Tool surface shape *(research-directed)*
- **Question:** Per-verb fine-grained tools (e.g. `read_tre`, `read_iff`, `read_datatable`, `read_stf`, `read_object_template`, `save_iff`, `save_datatable`, …) vs a few coarse dispatcher tools (`read_asset(path)` that auto-detects format and routes internally).
- **Recommended lean:** **Fine-grained, format/intent-named tools**, one MCP tool per meaningful CLI capability — NOT a single generic `read_asset`/`write_asset`. Rationale is locked by the roadmap guard-rail "over-broad tool shapes are un-retrofittable once agents depend on them," and fine-grained tools give the agent typed per-format arg schemas (record index, column id, typed value) instead of a stringly-typed blob. Group/name by **format × intent** (read / edit-save / build), mirroring the CLI verb taxonomy so the MCP surface is a thin 1:1-ish projection of the golden-tested verbs.
- **Research must decide:** the exact tool count and naming, whether the rarely-used build verbs (`compile-*`, `build-tre`, exporters) ship as MCP tools in this phase or are deferred (they are authoring/CLI-primary and may not need agent exposure yet), and how `inspect`/`decode` read variants collapse or stay distinct. Keep the write surface minimal and safe-by-construction.

### D-02 — MCP SDK vs hand-rolled JSON-RPC *(research-directed)*
- **Question:** Use the official `ModelContextProtocol` C# SDK vs a minimal hand-rolled JSON-RPC-2.0-over-stdio loop.
- **Recommended lean:** **Use the official C# SDK** if research confirms it (a) targets/runs on net10, (b) is at a stable-enough version, and (c) supports stdio server transport + tool registration + input-schema generation + tool annotations (`destructiveHint`, `readOnlyHint`). The SDK gives the handshake, framing, and annotation plumbing for free and keeps us spec-current; hand-rolling reimplements all of that. The roadmap guard-rail "NEVER host the MCP SDK in-proc inside the net472/x86 injected client" is about WHERE the SDK lives (the separate net10 process — exactly this phase), not WHETHER to use it.
- **Research must decide:** exact NuGet package + pinned version, net10 compatibility, and the fallback to a hand-rolled JSON-RPC loop **only if** the SDK is unavailable/unstable on net10 or can't express the required tool annotations. Confirm net10 SDK is installed on the self-hosted CI runner (`project_self_hosted_ci`) or pin the runtime accordingly.

### D-03 — Root configuration + safety/elicitation UX *(research-directed)*
- **Question:** How `resolvedRoot` is supplied to the headless server, and whether the 5-layer model's "elicitation" step is a real interactive MCP prompt or advisory-only in a headless agent loop.
- **Recommended lean:** Supply the root via an explicit **`--root` CLI arg to the server process** (the MCP client launch command sets it), with an **env-var fallback** (e.g. `UTINNI_MCP_ROOT`); **fail closed** if neither is set or the path doesn't resolve. NEVER accept an absolute path from the agent in any tool call; canonicalize once at startup via `LooseOverridePath.Resolve` and validate every tool path stays under it (path-escape/traversal test is a success criterion). For "elicitation": treat it as **MCP elicitation when the client supports it, advisory annotations + the loose-override-default + verify-before-commit layers as the always-on enforcement otherwise** — the `MCP-SECURITY.md` register must state plainly that tool hints are advisory-not-enforcement and that real safety comes from the structural layers (resolved-root pinning, loose-override default, byte-exact verify, backup/recovery), not from the agent honoring a hint.
- **Research must decide:** the precise root-config mechanism (arg vs env vs config file) against how the target MCP clients launch servers, and whether the SDK exposes an elicitation capability worth wiring on the `.tre` repack path.

### D-04 — CLI result + error/timeout mapping *(research-directed)*
- **Question:** Pass the `utinni-cli` sorted-key JSON envelope straight through as the MCP tool result vs re-shape into MCP structured-content blocks; and how CLI non-zero exits / SOE-tool hang-on-error surface to the agent.
- **Recommended lean:** **Pass the CLI JSON envelope through as the tool's structured result** (it is already a stable, sorted-key, schema-versioned contract — re-shaping would re-introduce business logic the shim is forbidden to own), optionally wrapped so the agent gets both a text and a structured-content view. Map failures as: **transport/exec failures and the Phase-13 SOE-tool hang → a hard MCP tool error after a timeout backstop**; **expected domain failures the CLI reports in-band (`{ok:false,...}`/non-zero with a JSON envelope) → return the envelope as the tool result** so the agent can reason about it, rather than an opaque protocol error. Every subprocess invocation MUST carry the Phase-13 timeout backstop (SOE tools hang-on-error) and surface a clear timeout error.
- **Research must decide:** exact MCP content-block shaping (text vs `structuredContent` vs both) per the SDK's capabilities, the timeout value/strategy, and the precise success/failure→MCP-error taxonomy.

### Locked by ROADMAP / prior phases (carried forward — NOT re-discussed)
- net10 process; **stdio transport only** (HTTP/SSE out of scope and deprecated).
- The separate process IS the honest seam — NEVER host the MCP SDK/transport loop inside `SWG.exe`'s net472/x86 address space (Anti-Pattern 1).
- `resolvedRoot` pinned **fail-closed** at startup; never accept an absolute path from the agent; canonicalize once via `LooseOverridePath.Resolve`.
- Write tools take **typed structured args only** (record index, column id, typed value) — never "apply the change you inferred."
- `save` defaults to the **loose-override tier**; result envelope is the ROADMAP/Phase-13 `{written, path, bytesWritten, backupPath, validated}` (Phase-13 D-09).
- `.tre` **repack is its own distinct, off-by-default tool**, `destructiveHint`+`dry_run`-annotated, routed through `TreBackupPath` — NOT reachable through `save` (Phase-13 D-10).
- Every capability is a golden-tested **CLI verb FIRST**; MCP stays a **thin dispatcher with zero business logic** (Phase-13 carried-forward).
- `MCP-SECURITY.md` is a **first-class design-time deliverable**, mirroring Phase-7's threat register — NOT a later hardening pass. Documents the 5-layer model (annotations → elicitation → loose-override-default → verify-before-commit → backup/recovery) + the advisory-not-enforcement caveat on tool hints.

### Claude's Discretion
- All of D-01..D-04's final resolution (the user delegated them to research — see above).
- Project layout for the new net10 `Utinni.Mcp` project (solution placement, where it builds in CI, how it locates `utinni-cli.exe` — adjacent build output vs configured path).
- Test approach for the real-MCP-client round-trip success criterion (scripted in-process MCP client vs a recorded stdio transcript), consistent with the DEC-C3 tiered testing strategy.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + requirements
- `.planning/ROADMAP.md` §"Phase 14" — goal, the 5 success criteria, and the constraint guard-rails (net10/stdio, no in-proc SDK, resolvedRoot fail-closed, typed-args-only, repack-off-by-default-dry_run, `MCP-SECURITY.md` as design-time deliverable).
- `.planning/REQUIREMENTS.md` — MCP-01 + MCP-02 statements + acceptance (lines ~119-120); MCP-03 (line ~121) is the Phase-16 out-of-scope sibling.

### The CLI surface this phase wraps (the dispatch target)
- `Utinni.Cli/Program.cs` — CommandLineParser `MapResult` verb dispatch + the sorted-key `JsonOutput` envelope contract.
- `Utinni.Cli/Commands/*Command.cs` — every verb the MCP tools wrap: read (`ParseTre`, `ListObjects`, `InspectIff`, `DecodeIff`), edit-roundtrip (`RoundtripIff/Ot/Stf/Tab`), write (`Save` — all 4 formats, loose-override default), destructive (`RepackTre`), build (`BuildTre`, `CompileTemplate`, `CompileDefinition`, `CompileDatatable`, `ExportArmor`, `ExportWeapon`).
- `Utinni.Cli/Commands/SaveCommand.cs` — the net-new write surface MCP-02 wraps; confirm its `{written,path,bytesWritten,backupPath,validated}` envelope + loose-override-default before designing the write tools.
- `Utinni.Cli/Commands/RepackTreCommand.cs` — the destructive repack the off-by-default `dry_run` MCP tool wraps (routed through `TreBackupPath`).
- `Utinni.Cli/Commands/Subprocess/` — existing subprocess-invocation mechanics + the Phase-13 timeout backstop for hang-on-error native tools; the MCP→CLI seam mirrors this one layer up.

### Safety primitives to reuse (do NOT re-implement)
- `UtinniCoreDotNet/Saving/LooseOverridePath.cs` (`Resolve`) — the canonicalizer behind `resolvedRoot` pinning + path-escape defense.
- `UtinniCoreDotNet/Formats/Tre/{TreBackupPath,TreRepackLock,TreRecordIndexResolver}.cs` — backup/recovery + repack-lock behind the destructive tool.

### Prior-phase decision context
- `.planning/phases/13-wrap-revived-compilers-as-cli-verbs-close-ot-tier-2/13-CONTEXT.md` — D-09 (SAVE verb, all 4 formats, envelope) and D-10 (repack as its own dry_run-gated tool, not via `save`); the "every capability is a CLI verb first, MCP is a thin dispatcher" seam.
- `docs/ai/toolchain-inventory.md` — the revive/replace cross-walk; situates the MCP server as the centerpiece over the verified CLI pipeline.

### Memories
- `project_phase13_cli_verbs` — the 17 utinni-cli verbs + the gotchas Phase-14 inherits (SOE tools hang-on-error → timeout backstop; `.tpf`/`.tdf` registered-class gate; 16-verb CommandLineParser cap).
- `project_auth01_revive_build_track` — Phase-12/13 build track + the `.mcp.json`/windows-mcp wiring context.
- `project_self_hosted_ci` — the self-hosted v145 CI runner; confirm net10 SDK availability there for the new managed build/test lane.
- `project_vision` + `project_swg_toolchain_crosswalk` — why the MCP server is the v2.0 centerpiece (agents drive the full asset pipeline).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`Utinni.Cli` (net472, x86) — complete dispatch target.** All read/roundtrip/`save`/`repack`/build verbs already ship and are golden-tested (`Utinni.Cli.Tests`). Phase 14 adds ZERO verbs; it `Process.Start`s this exe and forwards the sorted-key JSON envelope.
- **Sorted-key `JsonOutput` envelope** — a stable, schema-versioned contract; pass it through as the MCP tool result rather than re-shaping (keeps the shim logic-free — D-04).
- **`LooseOverridePath.Resolve` + `TreBackupPath`/`TreRepackLock`** — the structural safety layers; `resolvedRoot` pinning and the destructive-tool backup path reuse these directly, not new code.
- **`Commands/Subprocess/` + Phase-13 timeout backstop** — the proven subprocess-with-timeout pattern; the MCP→CLI seam is the same shape one layer up.

### Established Patterns
- **Coexistence-by-verb-ownership** — the CLI verb name is the routing signal; MCP tools should preserve that taxonomy (read/edit/build, per-format) rather than collapsing it (informs D-01).
- **Thin-dispatcher discipline** — Phase 13 deliberately pushed all logic into golden-tested CLI verbs so Phase 14 has none. Any temptation to parse/transform CLI output in the MCP layer is an anti-pattern.
- **DEC-C3 tiered testing** — the real-MCP-client round-trip success criterion lands as an automatable test (scripted client / recorded transcript), not a manual smoke.

### Integration Points
- New net10 `Utinni.Mcp` project joins the solution + a managed build/test lane on the self-hosted v145 runner; it must locate the `utinni-cli.exe` build output at runtime.
- MCP tools `Process.Start` `utinni-cli.exe`; the x86/net472-vs-net10 boundary is exactly why this is a separate process (the locked anti-pattern).
- `MCP-SECURITY.md` lands in the phase/docs tree as a design-time deliverable, cross-referencing the Phase-7 threat-register format.

</code_context>

<specifics>
## Specific Ideas

- The user's explicit instruction for this phase: **decide all four open implementation areas (tool surface, SDK-vs-hand-rolled, root/safety UX, result/error mapping) via research** rather than interactive discussion. Research should produce a concrete, justified pick for each with the bounding constraints already captured in D-01..D-04 — and must NOT re-open the ROADMAP/Phase-13 locked items.
- Treat the existing `.mcp.json` (currently only `windows-mcp`) as the integration reference for how an MCP server is registered/launched on this machine when designing the root-config + launch story (D-03).

</specifics>

<deferred>
## Deferred Ideas

- **Live-injected MCP bridge (named-pipe IPC into the x86 host for in-client preview)** — MCP-03, already roadmapped to **Phase 16**. Out of scope here; the stdio→CLI seam is the Phase-14 boundary.
- **Exposing the build/authoring verbs (`compile-*`, `build-tre`, exporters) as MCP tools** — research (D-01) decides whether any ship this phase; if not, they remain CLI-primary and can be added as MCP tools in a later increment without re-architecting the shim.

### Reviewed Todos (not folded)
The `todo.match-phase 14` query surfaced 4 weak keyword matches (score 0.4–0.6), NONE in Phase-14's MCP-shim scope — all reviewed and left for their proper homes:
- `phase09-datatable-editor-review-warnings.md` (Phase-9 editor code-quality) — editor-side, not MCP.
- `phase10-stringtable-sc3-live-reload-residual.md` (RESID-03 live-reload candor) — roadmapped to Phase 15.
- `swg-window-resize-fullscreen-edge-cases.md` (RESID-04) — roadmapped to Phase 15.
- `loader-lock-harness-flake-fix.md` (CI-stability flake) — infra, not MCP scope.

</deferred>

---

*Phase: 14-Headless MCP server (`Utinni.Mcp`) — the centerpiece*
*Context gathered: 2026-06-05*
