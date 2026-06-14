# Phase 16: Live-injected MCP bridge + Blender ecosystem boundary - Research

**Researched:** 2026-06-13
**Domain:** Named-pipe IPC into an x86 CLR-hosting injected client (MCP-03) + file-format boundary documentation & reader reuse (ECO-01)
**Confidence:** HIGH (both tracks build on shipped, in-repo code; the one external-library question — conditional MCP tool registration — is verified against official SDK docs)

## Summary

This phase has two independent tracks with sharply different risk profiles. **MCP-03** (the live bridge) is the higher-risk new mechanism, but the research found that nearly every hard problem is already solved by shipped Utinni infrastructure: the injected client **already hosts the .NET CLR in-process** (`UtinniCore/clr.cpp` runs `UtinniCoreDotNet.dll`), so the named-pipe *server* can live in **managed net472 code** (`UtinniCoreDotNet`), not in C++. `System.IO.Pipes.NamedPipeServerStream` is a BCL primitive available in net472, and named pipes are OS-level and **arch-agnostic** — a net10 client process talks to an x86 net472 server process over the same pipe name with zero marshaling concerns. The heap-free-hot-path constraint (`project_rh_snapshot_no_heap_alloc`) is satisfied for free: there is an existing thread-marshaling seam — `GameCallbacks.mainLoopCallQueue` (a `ConcurrentQueue<Action>` drained on the game thread via `AddMainLoopCallback`) — so the pipe-server worker thread never touches the render/callback hot path; it just enqueues a reload `Action`.

**ECO-01** (the Blender boundary) is doc-plus-reader-reuse and **lower-risk than even the planner may expect**. The decisive finding: the canonical Blender export (`frn_all_bed_sm_s1_l0.msh`) is an IFF container with root `FORM...MESH`, and the shipped `decode-iff` verb's `TryDecode` **already** routes `MESH`/`SKMG`/`SKTM`/shader/object-template/datatable/string-table/UI-page roots — emitting a structural-count *summary* for meshes (no geometry decode, honoring DEC-A3). The golden `.tre` (`retail_mini_0005.tre` = `EERT0005`) is read by `parse-tre` today. So the "open a Blender bundle + validate `.rsp`/`.iff`" baseline is **fully reachable from the current reader set with zero new codecs** — the deliverable is a thin bundle-walking verb + the contract doc, not new format work.

**Primary recommendation:** Run ECO-01 first as its own track (doc + a thin `validate-bundle`/reuse-existing-readers verb; near-zero risk). For MCP-03, host the pipe *server* in `UtinniCoreDotNet` (managed, net472), use a length-prefixed JSON line protocol over `NamedPipeServerStream`/`NamedPipeClientStream`, marshal `reload-asset` onto the existing `mainLoopCallQueue`, reuse `ReloadAssetClassifier` for the honest ack tier, register the `live_*` tools via `WithTools<LiveTools>()` **conditionally** on `--enable-live` (NOT the assembly scan), and prove ping + reload-asset with a loopback `NamedPipe` protocol test in `Utinni.Mcp.Tests` (no live client).

## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01 — Preview success bar = round-trip proven, render best-effort.** Success = the agent sends an edit over the pipe and the injected client acknowledges + attempts to apply/reload it (returns an ack envelope). The bridge mechanism is the deliverable; actual visible re-render is best-effort and may be blocked by the disabled loose searchPath gate. Render fidelity rides on RESID-03 whenever it lands — explicitly NOT a Phase-16 success gate.
- **D-02 — Minimal verb surface: `ping` + `reload-asset` only.** `ping` = health/handshake (is a client injected & listening?). `reload-asset` = apply/reload an edited asset, returns an ack envelope. NOT in v1: scene/spawn control, readback/status query, screenshot-back.
- **D-03 — `live_*` tool tier on the existing `Utinni.Mcp` server** (not a distinct bridge process). The named-pipe *client* code is just another dispatch target alongside the existing `CliDispatcher`. The out-of-proc boundary still holds — the pipe client is on the out-of-proc side; the *server* end of the pipe is the only thing inside `SWG.exe`.
- **D-04 — Server-launch flag, tools hidden when off (fail-closed by absence).** Explicit opt-in at server startup (e.g. `--enable-live` arg / env var, mirroring `--root`). When off (default), the `live_*` tools are not registered/advertised. NOT chosen: always-visible-fail-at-call-time; auto-detect-by-pipe-presence.
- **D-05 — Contract authoritative in Utinni, mirrored pointer in Blender repo.** Doc lives in the Utinni repo (e.g. `docs/ai/blender-boundary-contract.md`). The `swg-blender-plugin` repo gets a short pointer/README note referencing it.
- **D-06 — The contract must nail down all four:** (1) `.rsp` search-path contract; (2) format-version matrix (`.iff`/`.tre`); (3) directory/bundle layout; (4) ownership/anti-coupling rules.
- **D-07 — Principle locked: reuse existing readers, no 3D decode (DEC-A3); exact verb/format reachability is research-directed.** (This research resolves it — see ECO-01 reachability verdict below.)
- **D-08 — Blender repo is the fixture source; Utinni reads a pinned copy.** `swg-blender-plugin/tests/golden/` is the fixture origin. Utinni vendors/pins a copy. Fixture-storage mechanics defer to CON-O-09.

### Claude's Discretion

- D-07's exact reader/verb scope (delegated to research — **resolved below**).
- Named-pipe wire format / message framing, threading placement of the pipe server inside the injected host (must honor the hot-path heap-free constraint), and the `live_*` tool input-schema shapes — **resolved below**.
- The precise authentication/trust model on the named pipe (local-only, ACL) — **resolved below** (Security Domain).
- CI/test approach for the un-injectable bridge (loopback named-pipe protocol test) — **resolved below**.

### Deferred Ideas (OUT OF SCOPE)

- Visible live render-on-reload (RESID-03) — gated on re-enabling the disabled loose searchPath; remains deferred. `reload-asset` attempts apply/reload but does NOT gate success on visible re-render.
- Scene/spawn control + readback/screenshot-back over the live bridge — out of the v1 verb surface (D-02).
- Mesh-metadata peek (`.msh`/`.mgn` header read) for open/preview — deferred to keep DEC-A3's no-3D line clean (D-07). *(Note: this is partially obviated — `decode-iff` already emits a MESH structural summary; see reachability verdict.)*

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MCP-03 | An AI agent can drive the LIVE-injected client over an MCP bridge (named-pipe IPC into the x86 host) to preview an edit in-client. | Pipe-server placement (managed `UtinniCoreDotNet`), wire format (length-prefixed JSON), hot-path-safe marshaling (`mainLoopCallQueue`), `WithTools<T>()` gating, loopback test harness — all resolved below. |
| ECO-01 | The Utinni ↔ `swg-blender-plugin` boundary is formalized as a documented file-format / `.rsp` search-path contract — Utinni opens/previews what Blender exports; no runtime coupling (honors DEC-A3). | `.rsp` contract surface extracted from `rsp_builder.py`; bundle layout from `export_bundle.py`; format-version matrix from `TreVersion.cs`; reachability verdict (existing readers cover the baseline); fixture pin location (CON-O-09 resolved). |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| MCP tool surface (`live_ping`, `live_reload_asset`) | Out-of-proc MCP host (`Utinni.Mcp`, net10) | — | Locked anti-pattern: SDK never enters SWG.exe. Tools forward to a pipe client. |
| Named-pipe **client** | Out-of-proc MCP host (`Utinni.Mcp`, net10) | — | Lives on the out-of-proc side (D-03); a new dispatch target alongside `CliDispatcher`. |
| Named-pipe **server** | Injected client, **managed** (`UtinniCoreDotNet`, net472 CLR) | — | CLR is already hosted in-proc (`clr.cpp`); managed `System.IO.Pipes` avoids C++ and the arch boundary. |
| Reload action execution (touch game state) | Injected client, **game thread** | — | Must run on the SWG main loop thread; reached via `GameCallbacks.mainLoopCallQueue`. |
| Pipe-server worker loop (blocking accept/read) | Injected client, **background thread** | — | A blocking `WaitForConnection`/read loop must NOT run on the render/callback hot path (heap-free constraint). |
| Reload-tier classification (ack disposition) | Injected client, managed | — | Reuse shipped `ReloadAssetClassifier` — honest tier (PendingNextSceneChange / Unavailable). |
| Bundle open/preview (`.tre` + `.iff`/`.rsp` validate) | CLI / format core (`Utinni.Cli` + `UtinniCoreDotNet.Formats`) | MCP read tools (existing) | Reuse `parse-tre` + `decode-iff` + `inspect-iff`; no new codec (DEC-A3). |
| `.rsp` / bundle contract authorship | Documentation (`docs/ai/`) | — | Utinni owns format+injection (`project_swg_toolchain_crosswalk`); doc is the seam, not code. |

---

# TRACK A — MCP-03: Live-injected MCP bridge

## A1. Pipe-server placement — the central architecture decision

**Finding (HIGH):** The injected client **already hosts the .NET CLR in-process**. `UtinniCore/clr.cpp` calls `ExecuteInDefaultAppDomain(..., L"UtinniCoreDotNet.Startup", L"EntryPoint", ...)` `[VERIFIED: codebase UtinniCore/clr.cpp:128]`. `UtinniCoreDotNet` targets **net472** (`<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>`) `[VERIFIED: codebase UtinniCoreDotNet.csproj]`.

**Consequence:** The named-pipe **server end** should live in **managed net472 code inside `UtinniCoreDotNet`**, NOT in C++ UtinniCore. This is the strongly-recommended placement because:

1. **`System.IO.Pipes.NamedPipeServerStream` is a BCL type available in net472** — no C++ named-pipe (`CreateNamedPipe`/`ConnectNamedPipe`) needed. Grep confirms there is **no existing named-pipe or worker-thread seam in the Utinni-authored native code** (`CreateNamedPipe`/`std::thread` appear only in the *lifted SOE tools* under `tools/`, never in `UtinniCore/`) `[VERIFIED: codebase grep]` — so hand-rolling a C++ pipe server would be net-new native code on the injection hot path's process, exactly what the heap-free constraint warns against.
2. **Named pipes are arch-agnostic and process-boundary-clean.** The net10 `Utinni.Mcp` (x64/AnyCPU) client and the x86 net472 server communicate over a named string identifier at the OS level; there is **no cross-arch in-proc marshaling** — the "x86 vs net10" concern in the additional-context question dissolves because the pipe is the boundary, not a shared address space.
3. Managed code can use the **shipped thread-marshaling seam** (A2) directly; a C++ server would have to P/Invoke back into managed.

**Where it hooks:** Start the pipe-server background thread from `UtinniCoreDotNet.Startup.EntryPoint` (the CLR entrypoint) **only when the live tier is enabled** — but note the enable decision is on the *MCP host* side (D-04). The in-client server can simply always listen on a fixed pipe name; the gating that matters for safety is that the *tools are unregistered* when `--enable-live` is off (D-04), so no agent can reach the pipe. A second, in-client opt-in (e.g. a config flag to even start the listener) is a reasonable defense-in-depth option for the planner to consider, but is not required by D-04.

## A2. Threading & the heap-free hot-path constraint

**Finding (HIGH):** There is an existing, shipped thread-marshaling seam purpose-built for "do this work on the game thread from another thread":

- `GameCallbacks.mainLoopCallQueue` is a `ConcurrentQueue<Action>` `[VERIFIED: codebase GameCallbacks.cs:63]`.
- It is drained on the game thread each frame by `DequeueMainLoopCalls` → `CallbackHelpers.Drain(mainLoopCallQueue)`, registered via `UtinniCore.Utinni.Game.AddMainLoopCallback(...)` `[VERIFIED: codebase GameCallbacks.cs:86,264-266]`.
- The enqueue API is `QueueMainLoopCall(call)` → `mainLoopCallQueue.Enqueue(call)` `[VERIFIED: codebase GameCallbacks.cs:256]`.

**Recommended threading model:**

1. A **dedicated background thread** owns the `NamedPipeServerStream` accept/read loop (blocking `WaitForConnection` / async `ReadAsync`). This thread NEVER touches game state directly and NEVER runs on the render/callback path — it just parses a request envelope.
2. On a `reload-asset` request, the worker thread **enqueues an `Action` onto `mainLoopCallQueue`** (the shipped seam). The game thread drains and executes it next frame, performing the actual reload via the existing reload binding (and respecting CON-N-04's `VirtualProtect` bracket if it touches mapped memory).
3. The ack is produced from the classification (A4) — for D-01 the ack can return *immediately* with the honest tier (e.g. "queued; reloads on next scene change") because **render is best-effort, not a success gate**. The planner may optionally have the game thread signal completion back to the worker via a `ManualResetEventSlim`, but this is not required by D-01.

**Why this satisfies `project_rh_snapshot_no_heap_alloc`:** the crash that memory documents was a *per-frame heap allocation in the callback dispatch hot path*. The pipe-server's allocations (envelope parsing, JSON) happen on the **background thread, off the hot path**. The only thing crossing onto the game thread is a single already-allocated `Action` enqueued onto a `ConcurrentQueue` — the exact pattern the shipped queue was built for. No new per-frame allocation is introduced. `[VERIFIED: codebase GameCallbacks.cs + CITED: memory project_rh_snapshot_no_heap_alloc]`

## A3. Wire format / message framing (Claude's Discretion — recommendation)

**Recommendation (HIGH confidence in mechanism; the exact schema is a plan-time lock):** A **length-prefixed (or newline-delimited) UTF-8 JSON request/response** over the pipe, in **message-transmission mode** (`PipeTransmissionMode.Message` on the server, with a matching client) OR — simpler and recommended — a **4-byte little-endian length prefix + JSON body** over a byte-stream pipe. The length-prefix framing is preferred because it is transmission-mode-independent and trivially testable in a loopback.

**Request envelope (proposed):**
```json
{ "op": "ping" }
{ "op": "reload-asset", "path": "appearance/mesh/foo.msh", "ext": ".msh" }
```

**Ack envelope (proposed — mirror the CLI sorted-key envelope discipline from `JsonOutput`):**
```json
{ "schemaVersion": 1, "op": "ping", "result": { "listening": true, "gameRunning": true, "pid": 12345 } }
{ "schemaVersion": 1, "op": "reload-asset",
  "result": { "accepted": true, "tier": "PendingNextSceneChange", "queued": true, "path": "appearance/mesh/foo.msh" } }
```

**Rationale:**
- Mirrors the existing `Utinni.Cli` envelope shape (`schemaVersion` + `command`/`op` + `result` XOR `error`) `[VERIFIED: codebase JsonOutput / ParseTreCommand.cs:73]`, so the `CliResultMapper` discipline and the agent's mental model carry over.
- `tier` comes verbatim from `ReloadAssetClassifier` (A4) — the ack is *honest* about whether a visible reload will happen, satisfying D-01's "acknowledges + attempts to apply/reload."
- Keep the schema **deliberately tiny** (D-02): only `ping` and `reload-asset`. No status-query/readback `op`.

**Pitfall to encode:** the MCP host's stdout is reserved for MCP framing; the pipe is a *separate* channel and does not share that constraint, but the in-client server must not write its protocol to any stream SWG reads. Use the pipe only.

## A4. The ack disposition — reuse `ReloadAssetClassifier`

**Finding (HIGH):** `UtinniCoreDotNet.Saving.ReloadAssetClassifier.Classify(extension, rootTypeIdOrNull)` already returns the honest 4-tier disposition: `ReloadedTextures` / `ReloadedTerrain` / `PendingNextSceneChange` / `Unavailable` `[VERIFIED: codebase ReloadAssetClassifier.cs:124]`. The dispatcher maps `!Game.IsRunning` → `Unavailable` upstream `[VERIFIED: codebase ReloadAssetClassifier.cs:78-80]`.

**Use it directly for the `reload-asset` ack `tier` field.** This is the single biggest "don't hand-roll" win on this track: the honest-candor reload semantics were already designed and shipped in Phase 8/15 (RESID-03). The bridge does not need to re-derive whether a `.msh` will visibly reload — it asks the classifier and reports the tier. For most Blender-exported assets (`.msh`/`.mgn` carried as appearances, eventually referenced by templates), the honest tier is `PendingNextSceneChange`, which is exactly the D-01 "best-effort render" candor.

## A5. The `live_*` tool tier + D-04 conditional registration

**Finding (HIGH — verified against official SDK docs):** The .NET MCP SDK (`ModelContextProtocol` 1.4.0 `[VERIFIED: codebase Utinni.Mcp.csproj:28]`) offers two registration paths on the builder:
- `WithToolsFromAssembly()` — scans the assembly and registers **every** `[McpServerToolType]`/`[McpServerTool]`. This is what `Program.cs` uses today `[VERIFIED: codebase Program.cs:74]`.
- `WithTools<T>()` — registers a **single, explicitly-named** tool type. `[CITED: csharp.sdk.modelcontextprotocol.io/concepts/tools/tools.html]`

**D-04 implementation (recommended):** Do **NOT** put the `live_*` tools in a `[McpServerToolType]` class that the assembly scan would pick up — that would make them always-visible (the rejected option). Instead:

1. Keep `WithToolsFromAssembly()` for the existing read/save/repack tools.
2. Add a `LiveTools` class (it may still carry `[McpServerTool]` method attributes for the SDK's per-method metadata) but **place it so the assembly scan does not auto-register it**, OR — cleaner — register it only via an explicit, conditional `WithTools<LiveTools>()`:
   ```csharp
   var serverArgs = ServerArgs.Parse(args); // extend with EnableLive
   var mcp = builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
   if (serverArgs.EnableLive)
       mcp.WithTools<LiveTools>();   // ONLY registered when --enable-live
   ```
   **Caveat to verify at plan time:** if `LiveTools` is decorated `[McpServerToolType]`, `WithToolsFromAssembly()` would *also* discover it, defeating the gate. The plan must ensure exactly one of two things: (a) `LiveTools` is NOT `[McpServerToolType]`-decorated and is registered only via `WithTools<T>()`, or (b) the assembly scan is scoped to exclude it. Option (a) is simplest and is the recommended lock. `[ASSUMED — exact attribute/scan interaction at SDK 1.4.0 needs a 10-line spike to confirm WithTools<T> works on a non-[McpServerToolType] class; see Assumptions A1]`

3. Extend `ServerArgs` with an `EnableLive` bool, mirroring the existing `--root`/`--cli-path` flag parsing (space + equals form, first-wins, tolerant) `[VERIFIED: codebase ServerArgs.cs:57-97]`. An env-var alias (`UTINNI_MCP_ENABLE_LIVE`) mirrors the `UTINNI_MCP_ROOT` precedent.

**The pipe client as a new dispatch target (D-03):** Add a `LivePipeClient` (in `Utinni.Mcp/Server/`) alongside `CliDispatcher`, registered as a DI singleton the same way (`builder.Services.AddSingleton(new LivePipeClient(pipeName))`) `[VERIFIED: pattern from Program.cs:67]`. The `live_*` tools take `LivePipeClient` as a DI parameter (exactly as `ReadTools` take `CliDispatcher`/`ResolvedRoot` `[VERIFIED: codebase ReadTools.cs:62-69]`). The client is a thin connect → send envelope → read ack → map; **zero format/business logic** (thin-dispatcher discipline, same as the CLI side).

**Tool shapes (proposed, D-02 minimal):**
```csharp
[McpServerTool(Name = "live_ping", ReadOnly = true, Idempotent = true)]
// returns { listening, gameRunning, pid } or a "no client" disposition

[McpServerTool(Name = "live_reload_asset", ReadOnly = false, Idempotent = false)]
// arg: relativePath (resolved under ResolvedRoot, same as read tools)
// returns { accepted, tier, queued, path }
```
`live_reload_asset` should resolve its path through `ResolvedRoot.Resolve` (throws on escape, SDK maps to tool error) exactly like the read/save tools `[VERIFIED: codebase ReadTools.cs:67]` — the live tier must NOT bypass the fail-closed root.

## A6. CI / loopback test harness (un-injectable bridge)

**Finding (HIGH):** `Utinni.Mcp.Tests` is a net10 xUnit project (`Microsoft.NET.Test.Sdk` 17.13.0, `xunit` 2.9.3) `[VERIFIED: codebase Utinni.Mcp.Tests.csproj]` with an established pattern of testing the dispatcher against *stub children* (cmd.exe/powershell) to avoid needing the real target `[VERIFIED: codebase DispatcherTests.cs:35-49]`.

**Recommended harness (DEC-C3 Tier-2):** A **loopback named-pipe protocol test** that stands up a `NamedPipeServerStream` (a *test stub* server implementing the ping + reload-asset protocol) on a randomized pipe name, points a `LivePipeClient` at it, and asserts:
- `live_ping` → server replies `{ listening: true, ... }` → client maps to the expected MCP result.
- `live_reload_asset` → server replies an ack envelope with a `tier` → client maps it; assert the request envelope the client *sent* is well-formed (mirrors `SaveCompositionTests` asserting typed argv per spawn `[VERIFIED: codebase referenced in MCP-SECURITY.md T-14-16]`).
- Framing edge cases: partial reads, oversize body, a server that closes mid-message → client returns a hard error, never hangs (mirror the `CliDispatcher` timeout discipline).

This exercises the **entire pipe protocol + envelope mapping without a live SWG client**. The in-client server's *game-thread marshaling* and the actual reload binding are the **Tier-4 manual** residual (live injected confirmation), consistent with D-01 (render best-effort) and the project's tiered-testing model. The managed pipe-server class in `UtinniCoreDotNet` can additionally get a pure-managed unit test (envelope parse → `ReloadAssetClassifier` tier → ack serialize) in `UtinniCoreDotNet.Tests`, with the `Game.IsRunning`/`mainLoopCallQueue` dependencies injected as the existing pure-function tests do (e.g. `LivePatchValidatorTests` passes `gameIsRunning` as a scalar `[VERIFIED: codebase LivePatchValidator.cs:135 + LivePatchValidatorTests.cs]`).

## A7. Don't-hand-roll (Track A)

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Cross-process IPC into x86 client | A custom C++ `CreateNamedPipe` server + P/Invoke marshaling | `System.IO.Pipes.NamedPipeServerStream` in net472 `UtinniCoreDotNet` | CLR is already hosted in-proc; BCL pipe is arch-agnostic and off-hot-path. No native code on the injection process. |
| Thread → game-thread handoff | A new per-frame queue / `BeginInvoke` shim | `GameCallbacks.mainLoopCallQueue` + `QueueMainLoopCall` (shipped) | Purpose-built, hot-path-safe, already drained each frame. Re-inventing it risks the `0x0051fb0a` heap-alloc crash. |
| "Will this asset visibly reload?" | New reload-semantics logic in the bridge | `ReloadAssetClassifier.Classify(...)` (shipped) | Honest 4-tier disposition already designed for RESID-03 candor; the ack just reports the tier. |
| Conditional tool visibility | A custom runtime tool-filter / always-visible-fail-at-call | `WithTools<LiveTools>()` gated on `--enable-live` | SDK-native explicit registration; fail-closed-by-absence per D-04. |
| Launch-flag parsing for `--enable-live` | Ad-hoc `args` scan in `Program.cs` | Extend the tested `ServerArgs.Parse` | Existing tolerant, tested parser (`ServerArgsTests`); `--root` is the precedent. |
| Ack JSON envelope shape | A bespoke format | Mirror `JsonOutput` sorted-key `{schemaVersion, op, result XOR error}` | Consistent with the whole CLI/MCP surface; the agent already knows it. |

---

# TRACK B — ECO-01: Blender ecosystem boundary

## B1. D-07 reader/verb reachability verdict (THE key research deliverable)

**Verdict (HIGH confidence — verified against the actual golden bytes and the shipped decoder dispatch):**

| Blender-exported asset | On-disk form | Reachable from current readers? | Verb | Output |
|------------------------|--------------|--------------------------------|------|--------|
| `.tre` bundle archive | `EERT0005` etc. (verified: `retail_mini_0005.tre` = `EERT0005`) | **YES — works today** | `parse-tre` | header + record table (`parse-tre` reads V0004/0005/0006/5000; V6000 enumerate-only) |
| `.msh` static mesh | `FORM...MESH` (verified: `frn_all_bed_sm_s1_l0.msh`) | **YES — works today, summary only** | `decode-iff` | `MESH` → `AppearanceSummary` structural-count summary (vertex/shader counts; **no geometry decode** — DEC-A3 honored) |
| `.mgn` skeletal mesh | `FORM...SKMG` | **YES — works today, summary only** | `decode-iff` | `SKMG` → `AppearanceSummary` (joint/shader counts; no geometry) |
| `.skt` skeleton | `FORM...SKTM` | **YES — works today, summary only** | `decode-iff` | `SKTM` → `AppearanceSummary` (joint count) |
| `.ans` animation | `FORM...KFAT`/`CKAT` | **YES — works today, summary only** | `decode-iff` | `KFAT`/`CKAT` → `AppearanceSummary` (frame count) |
| `.sht` shader | `FORM...SSHT`/`CSHD` | **YES — works today** | `decode-iff` | `SSHT`/`CSHD` → `IffStructureSummary` (FORM-tag + child-count) |
| `.sat` skeletal appearance | IFF container | **YES via raw chunk tree** | `inspect-iff` | raw chunk tree (no typed appearance decoder for SAT, but `inspect-iff` always works on any IFF) |
| `.rsp` manifest | text (`path @ explicitpath` lines) | **NO reader today — thin addition needed** | (new) `validate-bundle` or text parse | line-format validation against the contract (B2) |
| `client_search_paths.cfg` | text (`searchPathN=...` lines) | **NO reader today — thin addition needed** | (new) `validate-bundle` | cfg-fragment validation |
| `.dds` texture | DDS binary | **NO decode (intentional, DEC-A3)** | — | out of scope; existence-check only |

**`[VERIFIED: codebase DecodeIffCommand.cs:141-163 (TryDecode dispatch) + xxd of golden .msh/.tre bytes]`**

**Concrete reachability statement for the planner:**
- **Works today, zero new code:** Opening the bundle's `.tre`, and previewing every IFF-backed asset (`.msh`/`.mgn`/`.skt`/`.ans`/`.sht` as structural summaries; any `.iff` via `inspect-iff`'s raw chunk tree). The shipped `decode-iff` `TryDecode` already routes mesh/skeletal/shader roots to summary builders that **deliberately avoid geometry decode** — this is exactly DEC-A3-compliant preview, and it already exists.
- **Needs a thin addition (NOT a codec):** `.rsp` and `client_search_paths.cfg` are **text** formats. A small `validate-bundle` verb (or a `validate-rsp` verb) that (1) parses the `.rsp` `treefile_path @ explicit_path` lines, (2) checks each referenced file exists under the bundle, (3) checks the `searchPathN=` cfg fragment, and (4) optionally cross-checks bucket classification against the contract rules (B2). This is plain text parsing + filesystem existence checks — **no binary codec, no DEC-A3 concern.**
- **Out of scope (DEC-A3):** Any geometry/vertex/weight decode of `.msh`/`.mgn`, and `.dds` pixel decode. The summaries stop at counts.

**Recommendation:** The ECO-01 "open/preview verbs" deliverable is satisfied by (a) documenting that `parse-tre` + `decode-iff` + `inspect-iff` already open a Blender bundle's archive and IFF assets, plus (b) one thin new `validate-bundle` verb for the text `.rsp`/`.cfg` contract surface. This is the minimal, DEC-A3-honoring scope.

## B2. The `.rsp` search-path contract surface (D-06.1 — extracted from the reference impl)

**Source:** `D:/Code/swg-blender-plugin/swg_pipeline/rsp_builder.py` (`TreeFileRspBuilder`-format) `[VERIFIED: read file]`.

**`.rsp` line format:** `{treefile_path} @ {explicit_path}\n` — TreeFile-relative path, a literal ` @ ` separator, then the absolute/explicit on-disk source path. Paths use forward slashes. Lines are sorted by `treefile_path`. `[VERIFIED: rsp_builder.py format_rsp_line / write_rsp_file]`

**Bucket rules (suffix-match, then catch-all `other`):**

| Suffix | Bucket | `.rsp` filename |
|--------|--------|-----------------|
| `.mp3` | music | `data_uncompressed_music.rsp` |
| `.wav` | sample | `data_uncompressed_sample.rsp` |
| `.dds` | texture | `data_compressed_texture.rsp` |
| `.ans` | animation | `data_compressed_animation.rsp` |
| `.mgn` | mesh_skeletal | `data_compressed_mesh_skeletal.rsp` |
| `.msh` | mesh_static | `data_compressed_mesh_static.rsp` |
| (everything else) | other | `data_compressed_other.rsp` |

`[VERIFIED: rsp_builder.py _BUCKET_RULES + RSP_FILENAMES]`

**Load priority / search-path ordering:** "earlier roots win on duplicates" — `build_rsp_maps` records the first occurrence of each treefile path across the supplied search roots (`if rel not in bucket_map`) `[VERIFIED: rsp_builder.py build_rsp_maps:48-59]`. Empty buckets are skipped (no file written).

**The cfg fragment (`client_search_paths.cfg`):** two cfg dialects `[VERIFIED: rsp_builder.py client_search_path_snippet:91-109]`:
- Legacy: `searchPath{priority}={resolved_abs_path}` (e.g. `searchPath0=...`).
- SWGSource / multi-SKU: `searchPath_{sku:02d}_{priority}=...` (e.g. `searchPath_00_12=...`), which is *higher priority* than `searchTree_00_8`.
- The Phase-7 bundle writes the SWGSource form at **priority 12, sku 0** with a comment "Priority 12 beats searchTree_00_7/8 (retail TREs at 7-8)" `[VERIFIED: export_bundle.py:602-616]`.

**`[CONTRACT IMPLICATION]`** The doc (D-06.1) must state: Utinni reads loose bundles via these `searchPathN=` overrides (the *loose* path); the *packed* path (`.rsp` → `.tre` via `TreeFileBuilder`) is the AUTH-04 territory. Critically, `project_swg_client_loose_overrides` and D-01 note the loose searchPath is **currently disabled** in the user's environment — so the contract documents the seam, but live render through it is best-effort (the RESID-03 dependency), exactly as D-01 locks.

## B3. Bundle / directory layout (D-06.3 — from `export_bundle.py`)

**Source:** `D:/Code/swg-blender-plugin/swg_pipeline/export_bundle.py` `[VERIFIED: read file]`. On-disk shape of an export bundle (serverdata layout):

```
<bundle_root>/
├── appearance/
│   ├── mesh/<name>.msh              # static mesh (FORM MESH)
│   ├── mesh/<name>.mgn              # skeletal mesh (FORM SKMG)
│   ├── skeleton/<name>.skt          # skeleton (FORM SKTM)
│   ├── animation/<name>.ans         # animation (FORM KFAT/CKAT)
│   └── <name>.sat                   # skeletal appearance (skeletal bundles)
├── shader/<name>.sht                # shader (FORM SSHT/CSHD)
├── texture/<name>.dds               # textures (optional; copied or normal-baked)
├── rsp/                             # data_*.rsp manifests (one per non-empty bucket)
│   ├── data_compressed_mesh_static.rsp
│   ├── data_compressed_mesh_skeletal.rsp
│   └── ...
├── client_search_paths.cfg          # searchPathN= fragment
├── swg_export_manifest.json         # bundle manifest (bundle_type, assets, rsp_files)
└── (PHASE7_SPAWN_NOTES.md / swgsource_client_d_snippet.cfg in the phase7 combined bundle)
```

`[VERIFIED: export_bundle.py export_static_bundle / export_skeletal_bundle_from_files / export_phase7_validation_bundle]`

The manifest (`swg_export_manifest.json`) is a JSON descriptor with `bundle_type` (`static`/`skeletal`), `output_dir`, `assets` (mesh/shaders/textures/rsp_files paths), and `client_cfg` `[VERIFIED: export_bundle.py BundleResult.as_dict / build_export_manifest]`. **Utinni's `validate-bundle` should consume this manifest** as the authoritative bundle index (rather than re-walking the tree), then existence-check + IFF-validate the referenced assets.

## B4. Format-version matrix (D-06.2)

**Utinni READS (from `TreVersion.cs` `[VERIFIED: codebase TreVersion.cs:35-106]`):**

| TRE version tag | Lineage | Stride | Utinni disposition |
|-----------------|---------|--------|--------------------|
| `0004` | SWGEmu Pre-CU | 24 (size-first) | Read (real engine layout unverified — Wave 0 fixture) |
| `0005` | SWGEmu Pre-CU | 24 (size-first) | Read (fixture-validated) |
| `0006` | SWGEmu Pre-CU | 24 (size-first) | Read (fixture-validated) |
| `5000` | SWGEmu Pre-CU | 24 (size-first, zlib TOC) | **Read** (verified vs live client's `EERT5000`; corrects the old "encrypted" assumption) |
| `6000` | Restoration | 32 (crc-first +8 pad, zlib TOC) | **Enumerate-only** (payloads encrypted) |
| (any other, e.g. `9999`) | — | — | `UnsupportedVersion` throw |

> **Correction to prior planning notes:** the additional-context/memory `project_tre_version_support_gap` mentions "COT2000" and characterizes 5000 as newer/possibly-encrypted. The **shipped code is authoritative**: `TreVersions.Parse` accepts exactly `{0004,0005,0006,5000,6000}`; **V5000 is readable** (size-first, zlib TOC), and **only V6000 is enumerate-only** `[VERIFIED: codebase TreVersion.cs:43,79-86]`. "COT2000" is a *master-index/multi-archive* concept (`CotMasterIndex`), not a `TreVersion` enum member. The doc's matrix must reflect the shipped reality, not the stale memory.

**`swg-blender-plugin` WRITES:** The reference pipeline's `tre_list.py` doc states it is "version-aware: 0004/0005=24-byte TOC, 6000=32-byte TOC" `[VERIFIED: PIPELINE.md]`. The golden `.tre` it ships is **`EERT0005`** `[VERIFIED: xxd retail_mini_0005.tre]`. Blender's packing path uses `TreeFileBuilder.exe` (the same SOE tool Utinni revived in AUTH-04). **Compatibility verdict:** Blender writes within the Pre-CU `0004/0005/0006` family that Utinni reads fully — **no version mismatch on the loose/packed path the contract covers.** The doc should state: Utinni reads everything Blender's bundle export produces; V6000 (Restoration, encrypted) is out of the Blender-write path and is enumerate-only on the Utinni read side regardless.

**IFF asset versions:** the golden `.msh` is `MESH...0005` (an internal MESH form version) `[VERIFIED: xxd]`. Utinni's IFF reader is version-agnostic at the container level (it reads the chunk tree; the per-form summaries read counts, not version-gated fields), so IFF-version drift is not a contract risk for the *preview* baseline.

## B5. Anti-coupling rules (D-06.4)

The doc must state explicitly (cites DEC-A3 / `project_swg_toolchain_crosswalk`):
- **Utinni READS, Blender WRITES.** Neither repo imports the other; no runtime dependency in either direction `[VERIFIED: .mcp.json wires only windows-mcp; no swg-blender-plugin reference in any Utinni csproj — grep]`.
- **Utinni owns format + injection; Blender owns DCC authoring** (Maya replacement).
- The boundary is a **file-format + search-path seam on disk**, not a process/API coupling.
- DEC-A3: Utinni adds **no** mesh/skeleton/animation/texture geometry codec — the summaries stop at structural counts.

## B6. Cross-validation fixture (D-08 / SC4) — pin location

**Finding (HIGH):** **CON-O-09 is RESOLVED** — fixture storage policy is **in-repo synthetic, NO Git LFS** (locked in Phase 4) `[VERIFIED: codebase .planning/PROJECT.md:41 + REQUIREMENTS.md:61]`. Utinni's test projects already carry committed binary fixtures under `Fixtures/` dirs (`Utinni.Cli.Tests/Fixtures`, `Utinni.Mcp.Tests/Fixtures`, `UtinniCoreDotNet.Tests/Fixtures`) `[VERIFIED: codebase find]`, resolved at runtime via `FixturePath` (`AppContext.BaseDirectory/Fixtures/...`) `[VERIFIED: codebase FixturePath.cs:40]`.

**Recommendation:** Pin the Blender golden copy (e.g. `frn_all_bed_sm_s1_l0.msh` = 6099 bytes, and `retail_mini_0005.tre` = 119 bytes — both tiny, well within the in-repo policy) into **`Utinni.Cli.Tests/Fixtures/blender/`** (or `Utinni.Mcp.Tests/Fixtures/` if the cross-validation is asserted at the MCP layer). A cross-validation test asserts: (1) `parse-tre` opens the pinned `.tre`; (2) `decode-iff` summarizes the pinned `.msh` (MESH appearance, non-zero counts, raw-preserve disposition); (3) `validate-bundle`/`validate-rsp` accepts a pinned sample `.rsp` against the B2 contract rules. This realizes "Blender writes, Utinni reads the same bytes" with no live client and no LFS. The fixtures are tiny — no LFS pressure, fully consistent with CON-O-09.

**Pointer note (D-05):** the mirror pointer in the Blender repo lands naturally in `D:/Code/swg-blender-plugin/REFERENCES.md` (the existing "External references" table) `[VERIFIED: read REFERENCES.md]` or `README.md`, pointing at Utinni's `docs/ai/blender-boundary-contract.md`.

## B7. Don't-hand-roll (Track B)

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Open a Blender `.tre` bundle | New TRE reader | `parse-tre` (shipped) | Reads 0004/0005/0006/5000; 6000 enumerate-only. Already golden-tested. |
| Preview `.msh`/`.mgn`/`.skt`/`.sht` | New mesh/shader codec (DEC-A3 violation) | `decode-iff` → `AppearanceSummary`/`IffStructureSummary` (shipped) | Already routes these roots to **count-only summaries**; DEC-A3-compliant by construction. |
| Inspect any IFF chunk tree | New IFF parser | `inspect-iff` (shipped) | Universal raw chunk tree for any IFF, incl. `.sat`. |
| Reload-tier honesty in the doc | New reload-semantics prose | Cite `ReloadAssetClassifier` tiers (shipped) | Already the project's source of truth for reload candor. |

---

## Standard Stack

No new external packages are required for either track. Both build entirely on the BCL + the existing in-repo libraries + the already-pinned MCP SDK.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.IO.Pipes` (BCL) | net472 (server) / net10 (client) | Named-pipe IPC across the process/arch boundary | In-box; arch-agnostic; no dependency to vet |
| `ModelContextProtocol` | 1.4.0 (already pinned) | `live_*` tool registration via `WithTools<T>()` | Already approved + lock-filed in Phase 14 `[VERIFIED: Utinni.Mcp.csproj:28]` |
| `System.Text.Json` (BCL) | net10 / net472 | Wire-envelope (de)serialization | In-box; mirrors existing `JsonOutput` discipline |
| `Utinni.Cli` verbs (`parse-tre`/`decode-iff`/`inspect-iff`) | in-repo | ECO-01 bundle open/preview | Already golden-tested; reachability verified |
| `UtinniCoreDotNet.Saving.ReloadAssetClassifier` | in-repo | `reload-asset` ack tier | Shipped honest-candor classifier |
| `UtinniCoreDotNet.Callbacks.GameCallbacks` (`mainLoopCallQueue`) | in-repo | Game-thread marshaling | Shipped hot-path-safe queue |

**Installation:** None. No `npm`/`NuGet`/`pip` install for new functionality. (The existing `ModelContextProtocol 1.4.0` + `Microsoft.Extensions.*` are already restored and lock-filed.)

## Package Legitimacy Audit

> No new external packages are installed by this phase. Both tracks use only BCL types and already-vetted in-repo / already-pinned dependencies. The Package Legitimacy Gate is therefore **not triggered** — there is nothing new to slopcheck. (The one pre-existing third-party dep, `ModelContextProtocol 1.4.0`, was already audited at Phase 14: id+version+publisher confirmed against nuget.org with a committed lock file — `T-14-SC`.)

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram (MCP-03 live bridge)

```
AI agent
  │  JSON-RPC / stdio
  ▼
Utinni.Mcp  (net10, OUT-OF-PROC)
  ├─ ReadTools / SaveTools / RepackTool ──► CliDispatcher ──► utinni-cli.exe (net472/x86)   [existing]
  └─ LiveTools  (registered ONLY when --enable-live, via WithTools<LiveTools>())             [NEW]
        │  live_ping / live_reload_asset
        ▼
     LivePipeClient  (NamedPipeClientStream)                                                 [NEW]
        │  length-prefixed JSON envelope  { op, path }  /  { schemaVersion, op, result }
        │  ===== OS named-pipe boundary (arch-agnostic) =====
        ▼
  ┌─────────────────────────────────────────────────────────────────────┐
  │ SWG.exe (x86)  —  UtinniCore hosts the .NET CLR (clr.cpp)            │
  │   UtinniCoreDotNet (net472)                                          │
  │     LivePipeServer  (NamedPipeServerStream, BACKGROUND thread)       [NEW]
  │        │  parse envelope (off hot-path; heap-safe here)              │
  │        ├─ ping  ─────────────────────► reply { listening, gameRunning } │
  │        └─ reload-asset ──► ReloadAssetClassifier.Classify(ext,type)  [reuse]
  │                            │  enqueue Action ──► mainLoopCallQueue   [reuse, hot-path-safe]
  │                            ▼                         │ drained on game thread each frame
  │                       reply ack { accepted, tier, queued }           │
  │                                                      ▼               │
  │                                          (game thread) reload binding │
  │                                          (best-effort render — D-01)  │
  └─────────────────────────────────────────────────────────────────────┘
```

### System Architecture Diagram (ECO-01 boundary — data flow)

```
swg-blender-plugin (Blender, WRITES)
   export_bundle.py ──► <bundle_root>/  { appearance/*.msh|.mgn|.skt, shader/*.sht,
                                          rsp/data_*.rsp, client_search_paths.cfg, manifest.json }
        │  (no import, no runtime coupling — file-on-disk seam)
        ▼
Utinni (READS)
   utinni-cli parse-tre   ──► .tre header + record table
   utinni-cli decode-iff  ──► .msh/.mgn/.skt → AppearanceSummary (counts only, NO geometry — DEC-A3)
                              .sht → IffStructureSummary
   utinni-cli inspect-iff ──► any .iff raw chunk tree (.sat etc.)
   utinni-cli validate-bundle (NEW, thin) ──► parse rsp/*.rsp + client_search_paths.cfg (text),
                                              existence-check referenced assets, check bucket rules
   docs/ai/blender-boundary-contract.md  ◄── authoritative; REFERENCES.md in Blender repo points here
```

### Anti-Patterns to Avoid
- **Hosting the MCP SDK / transport in-proc in SWG.exe** — LOCKED anti-pattern (Phase 14). Only the narrow named pipe crosses; the SDK stays in the out-of-proc net10 host.
- **A C++ named-pipe server on the injection process** — re-introduces native code with its own threading on the host process; the CLR is already there, use managed pipes.
- **Doing reload work on the pipe-server thread or a callback hot path** — must marshal onto `mainLoopCallQueue`; per-frame heap alloc on the dispatch path is the documented crash.
- **Always-registered `live_*` tools that fail at call time** — D-04 rejects this; gate registration, not just execution.
- **A mesh/`.mgn` geometry decoder for "better preview"** — DEC-A3 violation; the count-summary is the ceiling.

## Common Pitfalls

### Pitfall 1: `WithToolsFromAssembly()` silently re-registers the gated `LiveTools`
**What goes wrong:** If `LiveTools` carries `[McpServerToolType]`, the existing `WithToolsFromAssembly()` scan registers it unconditionally — the `--enable-live` gate is defeated and the tools are always visible.
**How to avoid:** Register `LiveTools` ONLY via an explicit conditional `WithTools<LiveTools>()`, and ensure it is not also picked up by the assembly scan (simplest: don't decorate the class with `[McpServerToolType]`, or scope the scan). A 10-line spike at plan time confirms the exact SDK 1.4.0 behavior (Assumption A1).
**Warning signs:** `live_*` tools appear in the tool list even when launched without `--enable-live`.

### Pitfall 2: Pipe-server thread touches game state directly
**What goes wrong:** The background pipe thread calls a reload binding that dereferences `UtinniCore.dll` game state off the game thread → race / AV.
**How to avoid:** The pipe thread NEVER touches game state; it enqueues an `Action` on `mainLoopCallQueue`. Only the game thread (draining the queue) touches game state.
**Warning signs:** intermittent crashes on reload under a live client; works in the loopback test (which has no game thread).

### Pitfall 3: Treating a no-client `live_ping` as an error/hang
**What goes wrong:** If no client is injected, the pipe `Connect` blocks or throws; a naive client hangs the MCP call.
**How to avoid:** The `LivePipeClient` connects with a short timeout (mirror `CliDispatcher`'s timeout discipline) and maps "no server listening" to a clean `{ listening: false }` result, never a hang. `live_ping` is *designed* to report absence honestly (D-02: "is a client injected & listening?").
**Warning signs:** the MCP call never returns when SWG is not running.

### Pitfall 4: Documenting V5000 as encrypted / inventing "COT2000" as a TRE version
**What goes wrong:** The contract's version matrix repeats a stale planning assumption (5000 = unknown/encrypted; "COT2000" = a TRE version), contradicting the shipped reader.
**How to avoid:** Mirror `TreVersion.cs` exactly: `{0004,0005,0006,5000}` readable, `6000` enumerate-only, "COT2000" is a master-index concept not a version. (See B4 correction.)
**Warning signs:** the doc's matrix disagrees with `TreVersions.Parse`.

### Pitfall 5: Promising visible re-render as a success criterion
**What goes wrong:** Tying MCP-03 acceptance to a visible in-client reload — which is gated on the disabled loose searchPath (RESID-03) and will fail.
**How to avoid:** Success = ack round-trip proven (D-01). The ack reports the honest `tier`; visible render is best-effort. The loopback test proves the protocol; live render is Tier-4 manual and explicitly non-gating.
**Warning signs:** a success criterion that says "the mesh visibly updates in-client."

## Runtime State Inventory

> This phase is additive (new bridge + new doc + reuse), NOT a rename/refactor/migration. A full Runtime State Inventory is not applicable. The relevant runtime-state-adjacent facts:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Live service config | The named-pipe **name** is new runtime state the agent and the in-client server must agree on. It is NOT stored in git as live config — it's a compile-time constant or a launch flag on both ends. | Define one canonical pipe name (e.g. `utinni-live-bridge`); document it; both `LivePipeClient` and `LivePipeServer` reference the same constant. |
| OS-registered state | None — named pipes are ephemeral OS objects created at runtime, not registered (no Task Scheduler / service entry). | None — verified: pipe lifetime is process-scoped. |
| Stored data | None — no datastore keys are renamed or created. | None. |
| Secrets/env vars | New `--enable-live` flag (+ optional `UTINNI_MCP_ENABLE_LIVE` env). A PATH/flag, not a secret (mirrors `UTINNI_MCP_ROOT` = AR-14-03). | Document in MCP-SECURITY addendum; no secret handling. |
| Build artifacts | New `LivePipeServer` ships in `UtinniCoreDotNet.dll` (already injected); new `LiveTools`/`LivePipeClient` ship in `Utinni.Mcp`. No stale artifact from a rename. | Normal rebuild; ensure the `.mcp.json`/launch config can pass `--enable-live`. |

## Validation Architecture

> nyquist_validation is ENABLED (the `workflow.nyquist_validation` key is ABSENT from `.planning/config.json`; absent = enabled). `[VERIFIED: codebase config.json]`

### Test Framework
| Property | Value |
|----------|-------|
| Framework (MCP host) | xUnit 2.9.3 + Microsoft.NET.Test.Sdk 17.13.0, net10 (`Utinni.Mcp.Tests`) `[VERIFIED]` |
| Framework (format core / in-client managed) | xUnit, net472 (`UtinniCoreDotNet.Tests`) `[VERIFIED: existing LivePatchValidatorTests]` |
| Framework (CLI verbs) | xUnit, net472 (`Utinni.Cli.Tests`) `[VERIFIED]` |
| Quick run command | `dotnet test Utinni.Mcp.Tests --no-build` (per-project, fast lane) |
| Full suite command | the three `dotnet test` lanes in `.github/workflows/ci.yml` (net10 MCP, net472 CLI, native) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MCP-03 | `live_ping` round-trips over a loopback pipe (no live client) | integration (loopback) | `dotnet test Utinni.Mcp.Tests --filter LivePipe` | ❌ Wave 0 |
| MCP-03 | `live_reload_asset` sends a well-formed envelope + maps the ack tier | integration (loopback) | `dotnet test Utinni.Mcp.Tests --filter LivePipe` | ❌ Wave 0 |
| MCP-03 | `--enable-live` off ⇒ `live_*` tools NOT registered (fail-closed) | unit | `dotnet test Utinni.Mcp.Tests --filter ServerArgs` (extend) | ❌ Wave 0 |
| MCP-03 | In-client server: envelope parse → `ReloadAssetClassifier` tier → enqueue (game-state injected) | unit (pure-managed) | `dotnet test UtinniCoreDotNet.Tests --filter LivePipeServer` | ❌ Wave 0 |
| MCP-03 | Pipe framing edge cases (partial read / oversize / server-close) → hard error, no hang | integration | `dotnet test Utinni.Mcp.Tests --filter LivePipe` | ❌ Wave 0 |
| MCP-03 | Live in-client confirmation (visible/queued reload) | **Tier-4 manual** | — (maintainer smoke; non-gating per D-01) | n/a |
| ECO-01 | `parse-tre` opens the pinned Blender golden `.tre` | golden | `dotnet test Utinni.Cli.Tests --filter Blender` | ❌ Wave 0 (fixture pin) |
| ECO-01 | `decode-iff` summarizes the pinned `.msh` (MESH appearance, count-only) | golden | `dotnet test Utinni.Cli.Tests --filter Blender` | ⚠️ decoder exists; fixture+assert new |
| ECO-01 | `validate-bundle`/`validate-rsp` accepts a pinned `.rsp` against the B2 contract rules | golden | `dotnet test Utinni.Cli.Tests --filter ValidateBundle` | ❌ Wave 0 (new verb) |
| ECO-01 | Contract doc exists + Blender pointer note exists | doc/presence | (presence check / review) | n/a |

### Sampling Rate
- **Per task commit:** the relevant per-project `dotnet test` quick lane.
- **Per wave merge:** all three CI `dotnet test` lanes green.
- **Phase gate:** full suite green before `/gsd:verify-work`; plus the Tier-4 manual live-bridge smoke (non-gating, documented).

### Wave 0 Gaps
- [ ] `Utinni.Mcp.Tests/LivePipeProtocolTests.cs` — loopback ping + reload-asset + framing edge cases (covers MCP-03).
- [ ] `Utinni.Mcp.Tests/ServerArgsTests.cs` (extend) — `--enable-live` parse + gated-registration assertion.
- [ ] `UtinniCoreDotNet.Tests/LivePipeServerTests.cs` — pure-managed envelope→tier→enqueue (game-state injected).
- [ ] `Utinni.Cli.Tests/Commands/BlenderBoundaryGoldenTests.cs` — pinned `.tre`/`.msh`/`.rsp` cross-validation.
- [ ] `Utinni.Cli.Tests/Fixtures/blender/` — pinned `frn_all_bed_sm_s1_l0.msh` (6099 B) + `retail_mini_0005.tre` (119 B) + a sample `.rsp` (in-repo, no LFS — CON-O-09).
- [ ] `Utinni.Cli.Tests/Commands/ValidateBundleTests.cs` — the new thin verb (if a `validate-bundle`/`validate-rsp` verb is planned).

*(The MESH/SKMG appearance decoders and the `parse-tre`/`decode-iff`/`inspect-iff` read paths already exist and are golden-tested — only the Blender-specific fixtures + asserts and the thin `validate-bundle` verb are new.)*

## Security Domain

> `security_enforcement` is enabled (no `false` in config). This section EXTENDS `MCP-SECURITY.md` (the Phase-14 deliverable) with the live-tier threat surface, as D-04/canonical-refs direct. The planner should add these rows to (or a Phase-16 addendum of) `Utinni.Mcp/MCP-SECURITY.md`.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1 Architecture | yes | Out-of-proc MCP host + narrow named pipe; SDK never in SWG.exe (locked); least-surface verbs (ping + reload-asset only) |
| V4 Access Control | yes | `--enable-live` fail-closed-by-absence gate (D-04); `live_reload_asset` path resolved under fail-closed `ResolvedRoot` (no escape) |
| V5 Input Validation | yes | Wire envelope is shape-validated (op ∈ {ping,reload-asset}; path under root); reject malformed/oversize frames |
| V13 API / IPC | yes | **Named-pipe ACL — local-only, current-user.** `System.IO.Pipes` with a `PipeSecurity`/`PipeAccessRule` restricting to the current user / local machine; no `NamedPipeServerStream` exposed to network or "Everyone" |
| V7 Error Handling | yes | No-client / framing errors → clean error result, never a hang or a leaked exception across the pipe |

### Known Threat Patterns for {named-pipe bridge into an injected x86 client}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Unauthorized local process connects to the pipe and triggers reloads | Spoofing / Elevation | `PipeSecurity` ACL restricting the pipe to the current user; default `NamedPipeServerStream` is local-machine only (no network). Document as a Phase-16 threat row (analog of T-14-04). |
| Agent reaches `live_*` when not intended | Elevation | D-04 fail-closed gate: tools UNREGISTERED unless `--enable-live`; the pipe name is not advertised. Defense-in-depth: optional in-client opt-in to even start the listener. |
| Path-escape via `live_reload_asset` path arg | Tampering | `ResolvedRoot.Resolve` (same fail-closed containment as read/save tools) — throws before any pipe send. |
| Malformed / oversize wire frame DoS on the in-client server thread | DoS | Bounded read (max envelope size), `op` allow-list, per-connection timeout; a bad frame closes that connection only, never the game thread. |
| Reload work on the wrong thread → client crash | Tampering (integrity) | Marshal via `mainLoopCallQueue`; never touch game state off the game thread (Pitfall 2). |
| Pipe name collision / squatting (another process pre-creates the pipe) | Spoofing | Use a sufficiently-specific pipe name; the server creates it (`NamedPipeServerStream` ownership); document the trust assumption (single-user desktop tool, analog of AR-14-03/AR-14-04). |
| Visible-render over-promise | (candor) | Ack reports honest `ReloadAssetClassifier` tier; render best-effort (D-01) — not a security item but a correctness/candor control the doc must state. |

**Accepted-risk analog:** Like AR-14-03/AR-14-04, the live bridge is a **single-user, local desktop** surface; the named pipe is local-only and the `--enable-live` flag/pipe-name are PATHs/identifiers, not secrets. The planner should log a Phase-16 accepted-risk row to that effect.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Assume the in-client bridge needs C++ named-pipe + cross-arch marshaling | Managed `System.IO.Pipes` server in the already-hosted CLR | This research | No native code; arch boundary is the pipe, not shared memory |
| Assume reload semantics must be re-derived in the bridge | Reuse shipped `ReloadAssetClassifier` honest tiers | Phase 8/15 (RESID-03) | Ack candor for free |
| Plan-era: TRE 5000 "unknown/encrypted", "COT2000" a version | Shipped reader: 5000 readable, only 6000 enumerate-only, COT2000 = master index | Phase 7 (verified vs live client) | Contract matrix must follow the code, not stale notes |
| Plan-era: ECO-01 may need new mesh-peek codec | `decode-iff` already summarizes MESH/SKMG/SKTM/shader (count-only) | Phase 7 (07-04b) | DEC-A3-compliant preview already reachable |

**Deprecated/outdated:**
- The notion that ECO-01 requires net-new format work — superseded by the reachability verdict (B1). Only a thin text-parsing `validate-bundle` verb is new.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `WithTools<LiveTools>()` registers a single tool type AND a non-`[McpServerToolType]` class can host `[McpServerTool]` methods so the assembly scan does not also grab it — at SDK 1.4.0 specifically. Verified that `WithTools<T>()` exists; the exact interaction with `WithToolsFromAssembly()` for a gated class needs a ~10-line spike. | A5 / Pitfall 1 | If the scan grabs `LiveTools` regardless, the D-04 gate is defeated; mitigation is a scoped scan or a separate assembly. Low risk (multiple registration mechanisms exist), but the gate is the whole point of D-04 — confirm in Wave 0. |
| A2 | The pipe name should be a fixed constant shared by client + server. The exact name is a plan-time choice (not load-bearing for the mechanism). | Runtime State Inventory | None mechanical; only a naming/ACL convention to lock. |

*All other claims in this research are `[VERIFIED]` against the codebase / golden bytes or `[CITED]` to the official MCP SDK docs.*

## Open Questions

1. **Should the in-client `LivePipeServer` listener be gated by its own in-client flag, in addition to the `--enable-live` MCP-host gate?**
   - What we know: D-04 gates the *tools* (the agent can't reach the pipe when off). The in-client server could always-listen on a fixed pipe and rely on the tool gate + ACL.
   - What's unclear: whether the maintainer wants defense-in-depth (the listener itself off by default).
   - Recommendation: default the listener to **start only when a Utinni config/flag enables it** (cheap defense-in-depth), but treat the tool-side `--enable-live` as the authoritative gate per D-04. Surface to the user at discuss/plan time.

2. **Does the ack need a completion signal back from the game thread, or is "accepted + queued + tier" sufficient for D-01?**
   - What we know: D-01 says success = "acknowledges + attempts to apply/reload"; render is best-effort.
   - Recommendation: ship the **immediate ack** (accepted/queued/tier) for v1 — it fully satisfies D-01. A round-trip completion signal is a natural later increment (and would need the game thread to signal the worker; non-trivial, not required now).

3. **One new `validate-bundle` verb, or fold bundle validation into existing verbs?**
   - Recommendation: a single thin `validate-bundle` verb (consumes `swg_export_manifest.json`, existence-checks assets, validates `.rsp`/`.cfg` text against B2). Keeps the contract validation in one golden-testable place. (Claude's-discretion under D-07; flag for plan.)

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET CLR in injected client | MCP-03 pipe-server placement | ✓ | v4.0.30319 / net472 | — (it's how Utinni already runs) |
| `System.IO.Pipes` (BCL) | MCP-03 both ends | ✓ | in-box (net472 + net10) | — |
| `ModelContextProtocol` | `live_*` tool registration | ✓ | 1.4.0 (pinned + lock-filed) | — |
| `utinni-cli.exe` + format readers | ECO-01 open/preview | ✓ | in-repo, shipped | — |
| `D:/Code/swg-blender-plugin` (reference + fixture source) | ECO-01 contract + fixtures | ✓ | working copy present (verified) | golden files are tiny; pin in-repo |
| Live injected SWG client | MCP-03 Tier-4 manual confirmation | ✗ at CI | — | loopback pipe test (Tier-2) covers the protocol; live = manual, non-gating (D-01) |

**Missing dependencies with no fallback:** none block planning or the automated (Tier-2) deliverable.
**Missing dependencies with fallback:** a live SWG client is unavailable in CI — the loopback named-pipe test is the documented fallback; live render is Tier-4 manual and explicitly non-gating (D-01).

## Sources

### Primary (HIGH confidence)
- Codebase (verified by direct read): `UtinniCore/clr.cpp` (CLR host + entrypoint), `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` (`mainLoopCallQueue` marshaling), `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` (reload tiers), `UtinniCoreDotNet/Editing/LivePatchValidator.cs` (pure-managed gate precedent), `UtinniCoreDotNet/Formats/Tre/TreVersion.cs` (version matrix), `Utinni.Cli/Commands/{ParseTre,DecodeIff}Command.cs` (reachability), `Utinni.Cli/Program.cs` (verb dispatch / 16-arg cap note), `Utinni.Mcp/Program.cs` + `Server/{ServerArgs,CliDispatcher}.cs` + `Tools/{ReadTools,RepackTool}.cs` (host pattern), `Utinni.Mcp.Tests/DispatcherTests.cs` (stub-child test pattern), `.planning/phases/14-.../MCP-SECURITY.md` (5-layer model to extend).
- Golden bytes (verified via `xxd`): `frn_all_bed_sm_s1_l0.msh` = `FORM...MESH...0005`; `retail_mini_0005.tre` = `EERT0005`.
- Reference impl (verified by direct read): `D:/Code/swg-blender-plugin/swg_pipeline/{rsp_builder,export_bundle}.py`, `PIPELINE.md`, `REFERENCES.md`.
- `csharp.sdk.modelcontextprotocol.io/concepts/tools/tools.html` — `WithTools<T>()` vs `WithToolsFromAssembly()`.

### Secondary (MEDIUM confidence)
- `.NET Blog` / dotnet MCP SDK getting-started (registration patterns, corroborating `WithTools<T>`).

### Tertiary (LOW confidence)
- None relied upon. (The one assumption, A1, is flagged for a Wave-0 spike rather than asserted.)

## Metadata

**Confidence breakdown:**
- MCP-03 architecture (pipe placement, marshaling, ack reuse): HIGH — every mechanism is shipped in-repo and verified by read.
- MCP-03 tool gating (D-04): HIGH on `WithTools<T>` existence (official docs); one flagged assumption (A1) on the exact scan interaction → Wave-0 spike.
- ECO-01 reachability + contract surface: HIGH — verified against the actual golden bytes and the reference Python.
- Format-version matrix: HIGH — read directly from `TreVersion.cs` (corrects a stale memory).
- Security/ACL model: MEDIUM-HIGH — standard `System.IO.Pipes` ACL practice; exact `PipeSecurity` rules are a plan-time lock.

**Research date:** 2026-06-13
**Valid until:** ~2026-07-13 (stable; in-repo code + a pinned SDK. Re-verify only if `ModelContextProtocol` is bumped past 1.4.0 or the CLR/host wiring changes.)

## RESEARCH COMPLETE

**Phase:** 16 - Live-injected MCP bridge + Blender ecosystem boundary
**Confidence:** HIGH

### Key Findings
- **The injected client already hosts the .NET CLR** (`clr.cpp`), so the named-pipe **server lives in managed net472 `UtinniCoreDotNet`** — no C++, no cross-arch marshaling (the pipe IS the boundary). The `project_rh_snapshot_no_heap_alloc` constraint is satisfied by the **shipped `mainLoopCallQueue`** marshaling seam; the bridge enqueues one `Action`, no new hot-path allocation.
- **D-04 gating is `WithTools<LiveTools>()` registered conditionally on `--enable-live`** (extend the tested `ServerArgs`), NOT the assembly scan — verified against official SDK docs (one 10-line Wave-0 spike to confirm the scan-exclusion, Assumption A1).
- **The `reload-asset` ack reuses shipped `ReloadAssetClassifier`** for the honest tier — D-01's "best-effort render" candor is already designed (RESID-03).
- **ECO-01 is near-zero risk: the existing readers already open a Blender bundle** — verified the golden `.msh` is `FORM...MESH` (handled by `decode-iff` as a count-only AppearanceSummary, DEC-A3-clean) and the golden `.tre` is `EERT0005` (read by `parse-tre`). Only a thin text-parsing `validate-bundle` verb (`.rsp`/`.cfg`) is net-new.
- **CON-O-09 resolved (in-repo synthetic, no LFS)** — pin the tiny Blender goldens (6099 B `.msh`, 119 B `.tre`) into `Utinni.Cli.Tests/Fixtures/blender/`. The version matrix correction (5000 readable; only 6000 enumerate-only; "COT2000" = master index, not a version) must flow into the contract doc.

### File Created
`.planning/phases/16-live-injected-mcp-bridge-blender-ecosystem-boundary/16-RESEARCH.md`

### Confidence Assessment
| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | No new packages; all BCL + already-pinned/in-repo |
| Architecture (MCP-03) | HIGH | Every mechanism shipped + verified by read |
| Architecture (ECO-01) | HIGH | Reachability verified against actual golden bytes |
| Pitfalls | HIGH | Drawn from shipped code + documented memories |

### Open Questions (non-blocking)
1. In-client listener defense-in-depth gate (in addition to the D-04 tool gate) — recommend yes, confirm with maintainer.
2. Immediate-ack vs completion-signal for `reload-asset` — recommend immediate ack for v1 (satisfies D-01).
3. One `validate-bundle` verb vs folding into existing verbs — recommend one thin verb.

### Ready for Planning
Research complete. The two tracks are cleanly separable (ECO-01 first as low-risk doc+reuse; MCP-03 as the new-mechanism track). The planner has exact files, signatures, the wire-format recommendation, the precise reader/verb reachability verdict, the contract surface to document, and the loopback test design.
