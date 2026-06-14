# Phase 16: Live-injected MCP bridge + Blender ecosystem boundary - Context

**Gathered:** 2026-06-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Two **independent tracks**, explicitly sequenced last in v2.0:

- **MCP-03 — Live-injected MCP bridge:** an *optional, gated, user-disabled-today* named-pipe IPC hop into the **x86 injected client** (`SWG.exe` + UtinniCore) so an AI agent can **preview an edit in-client**. The net10 MCP host stays **out-of-proc** (Phase 14's locked anti-pattern); only a **narrow named pipe** crosses into the in-proc client. The pipe *client* lives in the out-of-proc `Utinni.Mcp` server; the SDK is never hosted inside `SWG.exe`.
- **ECO-01 — Blender ecosystem boundary:** pure **documentation + reuse of existing readers**. Formalize the Utinni ↔ `swg-blender-plugin` file-format / `.rsp` search-path contract as a documented seam. Utinni **reads**, Blender **writes**, neither imports the other (honors **DEC-A3**, the no-3D-authoring anti-goal). Reference impl: `D:/Code/swg-blender-plugin` (`swg_pipeline/rsp_builder.py`, `export_bundle.py`).

**In scope:**
- The `live_*` tool tier on the existing `Utinni.Mcp` server + the named-pipe bridge into the injected host (ping + reload-asset).
- The Utinni-authoritative boundary contract doc (`.rsp` search-path contract + `.iff`/`.tre` format-version matrix + bundle/directory layout + anti-coupling rules).
- Open/preview verbs over Blender-exported bundles **reusing existing readers** (exact verb/format set is research-directed; no 3D decode).
- Cross-validation against shared golden fixtures (Blender repo is the fixture source; Utinni reads a pinned copy).

**Out of scope:**
- Re-opening the disabled loose searchPath / RESID-03 live render-on-reload path (visible re-render is best-effort, not a Phase-16 success gate).
- Any new mesh codec (`.msh`/`.mgn` geometry decode) — that is Blender's job (DEC-A3).
- Hosting the MCP SDK/transport in-proc inside `SWG.exe` (locked anti-pattern).
- Runtime coupling between Utinni and `swg-blender-plugin` in either direction.

**Track sequencing note:** ECO-01 has **no hard dependency** and may be pulled into its own track / done first (it is doc-only + low-risk). MCP-03 depends on Phase 14 (reuse the MCP host + tool ergonomics) and carries the highest new-mechanism risk.

</domain>

<decisions>
## Implementation Decisions

### Live bridge — preview ambition (MCP-03)
- **D-01 — Preview success bar = round-trip proven, render best-effort.** Success = the agent sends an edit over the pipe and the injected client **acknowledges + attempts to apply/reload** it (returns an ack envelope). The **bridge mechanism is the deliverable**; actual visible re-render is best-effort and may be blocked by the disabled loose searchPath gate. Render fidelity rides on **RESID-03 whenever it lands** — it is explicitly NOT a Phase-16 success gate. This keeps the highest-risk new mechanism the focus without coupling success to the deferred render path.

### Live bridge — command surface (MCP-03)
- **D-02 — Minimal verb surface: `ping` + `reload-asset` only.** `ping` = health/handshake (is a client injected & listening?). `reload-asset` = apply/reload an edited asset, returns an ack envelope. Smallest contract that satisfies D-01's round-trip bar. Narrow surface is un-retrofittable-safe, mirroring Phase 14's "minimal write surface" discipline. **NOT in v1:** scene/spawn control, readback/status query, screenshot-back.

### Live bridge — architecture (MCP-03)
- **D-03 — `live_*` tool tier on the existing `Utinni.Mcp` server** (not a distinct bridge process). The net10 `Utinni.Mcp` gains a small set of `live_*` tools; the **named-pipe *client*** code is just another dispatch target alongside the existing `CliDispatcher`. One MCP endpoint the agent already knows. The out-of-proc boundary still holds — the pipe client is on the *out-of-proc* side; the *server* end of the pipe is the only thing inside `SWG.exe`.

### Live bridge — gating / opt-in (MCP-03)
- **D-04 — Server-launch flag, tools hidden when off (fail-closed by absence).** An explicit opt-in at server startup (e.g. `--enable-live` arg / env var, mirroring Phase 14's `--root` convention). When **off (the default)**, the `live_*` tools are **not registered/advertised** — the agent never sees them. Turning the tier on requires deliberate user action. Consistent with `resolvedRoot`'s startup-pinning, fail-closed model. **NOT chosen:** always-visible-fail-at-call-time; auto-detect-by-pipe-presence (weaker opt-in guarantee).

### Blender boundary — contract home (ECO-01)
- **D-05 — Authoritative in Utinni, mirrored pointer in Blender repo.** The contract doc lives in the **Utinni repo** (e.g. `docs/ai/blender-boundary-contract.md`) as the single source of truth (Utinni owns format+injection per `project_swg_toolchain_crosswalk`). The `swg-blender-plugin` repo gets a short pointer/README note referencing it. One authoritative copy, no drift.

### Blender boundary — contract scope (ECO-01)
- **D-06 — The contract must nail down all four:**
  1. **`.rsp` search-path contract** — the `data_*.rsp` manifest format + bucket rules + load priority / search-path ordering (the `TreeFileRspBuilder` format `rsp_builder.py` mirrors) + the `client_search_paths.cfg` fragment. The literal seam that makes Blender exports loadable.
  2. **Format-version matrix (`.iff`/`.tre`)** — which versions Utinni **reads** vs which versions `swg-blender-plugin` **writes**; the compatibility table that prevents silent version mismatch (ties to the TRE `0004/0005/0006` vs `5000/6000/COT2000` support reality — see `project_tre_version_support_gap`).
  3. **Directory / bundle layout** — the on-disk shape of a Blender export bundle (`export_bundle.py` output: loose serverdata tree + `rsp/` + `cfg`) that Utinni's open/preview verbs consume.
  4. **Ownership / anti-coupling rules** — explicit no-runtime-coupling statement (Utinni reads, Blender writes, neither imports the other; honors DEC-A3).

### Blender boundary — open/preview verbs (ECO-01)
- **D-07 — Principle locked: reuse existing readers, no 3D decode (DEC-A3); exact verb/format reachability is research-directed.** Lock the principle that open/preview verbs **reuse Phase-13/14 readers** (e.g. `ParseTre`, `InspectIff`, `DecodeIff`) and do **not** add mesh-geometry codecs. The **researcher determines** exactly which verbs/formats are reachable from the current reader set vs. need thin additions. (Bundle-level open `.tre` + validate `.rsp`/`.iff` is the expected baseline; `.msh`/`.mgn` geometry stays opaque.)

### Blender boundary — cross-validation fixtures (ECO-01, SC4)
- **D-08 — Blender repo is the fixture source; Utinni reads a pinned copy.** `swg-blender-plugin/tests/golden/` (e.g. `frn_all_bed_sm_s1_l0.msh`) is the **fixture origin**. Utinni **vendors/pins a copy** and asserts it can open the bundle + that the `.rsp` conforms to the contract. Cross-validation = **Blender writes, Utinni reads the same bytes**. Fixture-storage mechanics defer to the existing **CON-O-09** in-repo-vs-LFS policy.

### Claude's Discretion
- D-07's exact reader/verb scope (delegated to research).
- Named-pipe wire format / message framing, threading placement of the pipe server inside the injected host (must honor the hot-path heap-free constraint — see `project_rh_snapshot_no_heap_alloc`), and the `live_*` tool input-schema shapes — all implementation details for research + planning.
- The precise authentication/trust model on the named pipe (local-only, ACL) — research-directed; flagged as a security gray area not deep-dived in discussion.
- CI/test approach for the un-injectable bridge (e.g. a loopback named-pipe protocol test exercising ping + reload-asset *without* a live SWG client, given CI can't inject) consistent with DEC-C3 tiered testing; live in-client confirmation is Tier-4 manual.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + requirements
- `.planning/ROADMAP.md` §"Phase 16" — goal, the 4 success criteria, constraint guard-rails (out-of-proc MCP host + narrow named pipe; live-patch tier gated/opt-in; Blender boundary is a file-format seam not a process coupling).
- `.planning/REQUIREMENTS.md` — MCP-03 (line ~121) + ECO-01 (line ~139) statements + acceptance.

### Phase 14 precedent — the MCP host this phase extends
- `.planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/14-CONTEXT.md` — locked: net10/stdio, thin-dispatcher, never-host-SDK-in-proc, resolvedRoot fail-closed, typed-args-only, `MCP-SECURITY.md` as design-time deliverable.
- `Utinni.Mcp/Program.cs`, `Utinni.Mcp/Server/{CliDispatcher,CliLocator,ResolvedRoot,ServerArgs}.cs`, `Utinni.Mcp/Tools/*.cs` — the existing server the `live_*` tier joins; `ServerArgs` is the `--root` precedent for the `--enable-live` flag; `CliDispatcher` is the dispatch-target pattern the pipe client mirrors.
- `Utinni.Mcp/MCP-SECURITY.md` (Phase-14 deliverable) — extend with the live-tier threat surface (named-pipe trust/ACL, opt-in gating).

### Blender boundary reference implementation (read-only, no runtime dep)
- `D:/Code/swg-blender-plugin/swg_pipeline/rsp_builder.py` — the `TreeFileRspBuilder`-format `.rsp` bucket rules + filenames the contract must document (the reference for D-06.1).
- `D:/Code/swg-blender-plugin/swg_pipeline/export_bundle.py` — bundle layout + `client_search_paths.cfg` fragment writer (D-06.3 / D-06.1).
- `D:/Code/swg-blender-plugin/PIPELINE.md` + `README.md` — full workflow + module map (mesh formats live on the Blender side).
- `D:/Code/swg-blender-plugin/tests/golden/` — the cross-validation fixture origin (D-08).
- *(Note: `D:/swg-blender-plugin` does NOT exist; `D:/Code/swg-blender-plugin` is the only working copy.)*

### Format readers / safety primitives to reuse (do NOT re-implement)
- `Utinni.Cli/Commands/*Command.cs` — `ParseTre`, `InspectIff`, `DecodeIff` (the open/preview reader set for D-07).
- `UtinniCoreDotNet/Formats/Tre/*` + the TRE version-support reality (`project_tre_version_support_gap`) — feeds the format-version matrix (D-06.2).

### Open-question dependency
- **CON-O-09** (fixture storage in-repo vs LFS) — governs where the pinned Blender golden copy lands (D-08).

### Anti-goal / strategy
- `.planning/PROJECT.md` — **DEC-A3** (no 3D authoring) — the boundary anti-coupling rule (D-06.4) cites this.
- `docs/ai/toolchain-inventory.md` — the revive/replace cross-walk; situates Utinni-owns-format / Blender-owns-DCC.

### Memories
- `project_phase14_mcp_server` — the headless MCP server + gotchas this phase inherits.
- `project_swg_toolchain_crosswalk` — Utinni owns format+injection; swg-blender-plugin = Maya replacement (read/author split) → anchors D-05.
- `project_scene_change_via_tjt` + `project_rh_snapshot_no_heap_alloc` — in-client change is TJT-callback-driven; injected hot paths must stay heap-free → constrains the pipe-server placement.
- `project_swg_client_loose_overrides` + `project_tre_version_support_gap` — the disabled loose searchPath (bounds D-01's render ceiling) + the TRE version matrix (D-06.2).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`Utinni.Mcp` (net10 stdio server)** — the host the `live_*` tier joins. `CliDispatcher` shows the dispatch-target pattern; `ServerArgs`/`ResolvedRoot` show the startup-flag + fail-closed pinning the `--enable-live` gate copies.
- **`Utinni.Cli` read verbs** (`ParseTre`, `InspectIff`, `DecodeIff`) — the existing readers the ECO-01 open/preview verbs reuse (no new codecs for D-07's baseline).
- **`swg-blender-plugin` `rsp_builder.py` / `export_bundle.py`** — the reference implementation the boundary contract documents; not imported, only mirrored as a spec.

### Established Patterns
- **Out-of-proc-only MCP host (Phase 14 anti-pattern lock)** — the live bridge crosses to x86 ONLY via the narrow named pipe; SDK never enters `SWG.exe`.
- **Thin-dispatcher discipline** — `live_*` tools forward to the pipe; zero format/business logic in the MCP layer, same as the CLI dispatcher.
- **Fail-closed-by-startup-flag** — `resolvedRoot` precedent → the live tier is unregistered unless explicitly enabled.
- **Heap-free injected hot paths** (`project_rh_snapshot_no_heap_alloc`) — constrains any pipe-server loop placed inside the injected host.

### Integration Points
- New `live_*` tools register on `Utinni.Mcp` only when `--enable-live`; they open a named-pipe client to a server end inside the injected UtinniCore host.
- The boundary contract doc lands in `docs/ai/` (Utinni-authoritative); a pointer note lands in `D:/Code/swg-blender-plugin`.
- Cross-validation test vendors a pinned copy of `swg-blender-plugin/tests/golden/` into Utinni's fixture tree (per CON-O-09).

</code_context>

<specifics>
## Specific Ideas

- User confirmed the Blender reference copy is **`D:/Code/swg-blender-plugin`** (the bare `D:/swg-blender-plugin` does not exist). Anchor all ECO-01 refs there.
- The live bridge's value is **proving the hop**, not photorealistic preview — the user accepted that visible re-render rides on the deferred RESID-03 path and is best-effort.
- Keep the live verb surface deliberately tiny (`ping` + `reload-asset`) — explicitly rejected scene/spawn and screenshot-back for v1 to avoid an un-retrofittable broad surface.

</specifics>

<deferred>
## Deferred Ideas

- **Visible live render-on-reload (RESID-03)** — gated on re-enabling the disabled loose searchPath; deferred from Phase 15, remains deferred. Phase 16's `reload-asset` attempts apply/reload but does not gate success on visible re-render.
- **Scene/spawn control + readback/screenshot-back over the live bridge** — explicitly out of the v1 verb surface (D-02); a natural later increment once the minimal hop is proven.
- **Mesh-metadata peek (`.msh`/`.mgn` header read)** — considered for open/preview; deferred to keep DEC-A3's no-3D line clean (D-07).

### Reviewed Todos (not folded)
The `todo.match-phase 16` query surfaced 4 weak keyword matches (score 0.6), NONE in Phase-16's bridge/Blender-boundary scope:
- `phase10-stringtable-sc3-live-reload-residual.md` (RESID-03 live-reload candor) — directly *related* (it bounds D-01's render ceiling) but is the deferred RESID-03 path itself, not Phase-16 scope. Left as deferred.
- `swg-window-resize-fullscreen-edge-cases.md` (RESID-04) — Phase-15 presentation residual, not bridge/boundary.
- `phase09-datatable-editor-review-warnings.md` (Phase-9 editor code-quality) — editor-side, unrelated.
- `loader-lock-harness-flake-fix.md` (CI-stability flake) — infra, unrelated.

</deferred>

---

*Phase: 16-Live-injected MCP bridge + Blender ecosystem boundary*
*Context gathered: 2026-06-13*
