# Phase 14: Headless MCP server (`Utinni.Mcp`) — the centerpiece - Research

**Researched:** 2026-06-05
**Domain:** net10 MCP (Model Context Protocol) stdio server, C# official SDK, subprocess dispatch to net472/x86 `utinni-cli.exe`
**Confidence:** HIGH (SDK + net10 facts verified against NuGet/official docs and the live machine; CLI surface read from source)

## Summary

Phase 14 builds a separate **net10** `Utinni.Mcp` console process that speaks MCP over stdio and dispatches every tool call to `Process.Start` of the existing `utinni-cli.exe` (net472, x86). The four delegated decisions (D-01..D-04) all resolve cleanly in favor of the recommended leans, because the external facts cooperate: the official `ModelContextProtocol` C# SDK is at **1.4.0** (published 2026-06-04), **explicitly targets `net10.0`** (alongside net8.0/net9.0/netstandard2.0), and ships first-class stdio **server** transport, attribute-based tool registration with auto-generated input schemas, the four tool **annotations** the security model needs (`ReadOnly`/`Destructive`/`Idempotent`/`OpenWorld`), **structured content** output, and **elicitation**. The single biggest planning risk — net10 SDK availability on the self-hosted v145 runner — is **already retired**: the .NET 10 SDK (`10.0.300`) and runtime (`10.0.8`) are installed on this machine (verified via `dotnet --list-sdks`/`--list-runtimes`).

The architecture is a clean two-process seam that mirrors the Phase-13 `NativeToolRunner` one layer up: net10 SDK host → `Process.Start(utinni-cli.exe, [verb, args…])` → capture sorted-key JSON envelope on stdout → return it to the agent as MCP structured content. The MCP host owns **zero** format/business logic. The security contract is structural, not advisory: `resolvedRoot` is pinned fail-closed at startup via the reused `LooseOverridePath.Resolve`, agents pass only relative paths + typed args, `save` defaults to loose-override, and `.tre` repack is a distinct off-by-default `destructiveHint`+`dry_run` tool. The MCP `destructiveHint`/`readOnlyHint` annotations are explicitly **advisory** — the official MCP guidance is blunt that hints are not a security boundary — so `MCP-SECURITY.md` must state that real safety lives in the deterministic layers (root pinning, loose-override default, verify-before-commit, backup/recovery), with annotations as UX-only signals.

**Primary recommendation:** Use the official `ModelContextProtocol` SDK v1.4.0 on net10, register **fine-grained format×intent tools** (≈11 tools this phase: 4 read + 4 save + 1 repack + 2 schema/build-adjacent, build/compile verbs deferred), pass the CLI JSON envelope through as `structuredContent` + a text mirror, spawn the CLI with a 60 s timeout backstop, supply `resolvedRoot` via `--root` arg with `UTINNI_MCP_ROOT` env fallback (fail-closed), and validate with a net10 in-process `McpClient` round-trip test plus a `LooseOverridePath` path-escape unit test.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| MCP protocol handshake / JSON-RPC framing / stdio loop | MCP host (net10 process) | — | SDK owns this; never in SWG.exe (Anti-Pattern 1) |
| Tool registration + input-schema generation + annotations | MCP host (net10 process) | — | SDK attribute-driven; thin declarative surface |
| `resolvedRoot` pinning + path-escape defense | MCP host (net10 process) | UtinniCoreDotNet (`LooseOverridePath`) | Reuse the canonicalizer; host enforces fail-closed at startup |
| Format parse / serialize / save / repack / compile | `utinni-cli.exe` (net472/x86 subprocess) | UtinniCoreDotNet + tools/ natives | All logic already golden-tested as CLI verbs; host calls, never reimplements |
| Subprocess spawn + timeout + stdout capture | MCP host (net10 process) | — | The MCP→CLI seam, mirroring Phase-13 `NativeToolRunner` |
| JSON envelope shaping | `utinni-cli.exe` (already sorted-key) | MCP host (pass-through wrap only) | Re-shaping = forbidden business logic in the shim |

## User Constraints (from CONTEXT.md)

### Locked Decisions (carried from ROADMAP / Phase-13 — NOT re-opened)
- **net10 process; stdio transport only** (HTTP/SSE out of scope and deprecated).
- The separate process IS the honest seam — **NEVER host the MCP SDK/transport loop inside `SWG.exe`'s net472/x86 address space** (Anti-Pattern 1).
- `resolvedRoot` pinned **fail-closed** at startup; **never accept an absolute path from the agent**; canonicalize once via `LooseOverridePath.Resolve`.
- Write tools take **typed structured args only** (record index, column id, typed value) — never "apply the change you inferred."
- `save` defaults to the **loose-override tier**; result envelope is the Phase-13 `{written, path, bytesWritten, backupPath, validated}`.
- `.tre` **repack is its own distinct, off-by-default tool**, `destructiveHint`+`dry_run`-annotated, routed through `TreBackupPath` — **NOT reachable through `save`**.
- Every capability is a golden-tested **CLI verb FIRST**; MCP stays a **thin dispatcher with zero business logic**.
- **`MCP-SECURITY.md` is a first-class design-time deliverable**, mirroring Phase-7's threat register; documents the 5-layer model + the advisory-not-enforcement caveat on tool hints.

### Claude's Discretion (resolved by this research — see D-01..D-04 below)
- Final resolution of D-01 (tool surface), D-02 (SDK vs hand-roll), D-03 (root config + elicitation), D-04 (result/error mapping).
- `Utinni.Mcp` project layout / solution placement / CI build lane / how it locates `utinni-cli.exe`.
- Real-MCP-client round-trip test approach (scripted in-process client vs recorded transcript).

### Deferred Ideas (OUT OF SCOPE)
- **Live-injected MCP bridge (named-pipe IPC into the x86 host)** — MCP-03 → Phase 16.
- **Exposing build/authoring verbs (`compile-*`, `build-tre`, exporters) as MCP tools** — D-01 defers them this phase (CLI-primary; addable later without re-architecting the shim).

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MCP-01 | AI agent can READ any supported SWG asset (TRE/IFF/datatable/.tab/.stf/OT) through a net10 stdio `Utinni.Mcp` server wrapping the existing `utinni-cli` JSON verbs. | D-01 read-tool taxonomy (4 read tools wrapping `parse-tre`/`inspect-iff`/`decode-iff`/`list-objects`); D-02 SDK stdio server; D-04 envelope pass-through. |
| MCP-02 | AI agent can EDIT + SAVE assets via MCP write tools defaulting to loose-override, byte-exact verify-before-commit, `dry_run` gate on destructive repack, `resolvedRoot` pinned fail-closed, documented in `MCP-SECURITY.md`. No agent write can corrupt a source archive or escape the resolved root. | D-01 save tools (wrap `save` verb) + distinct `repack_tre` tool; D-03 `resolvedRoot` via `LooseOverridePath` + fail-closed; D-04 error taxonomy; Security Domain + Validation Architecture sections (path-escape test). |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `ModelContextProtocol` | **1.4.0** | Main SDK: hosting + DI extensions (`AddMcpServer`, `WithStdioServerTransport`, `WithToolsFromAssembly`), `[McpServerTool]`/`[McpServerToolType]` attributes, annotations, structured content, elicitation. | Official Microsoft-collaboration SDK; the only first-party C# MCP impl. Targets net10.0. `[CITED: nuget.org/packages/ModelContextProtocol/1.4.0]` |
| `Microsoft.Extensions.Hosting` | 10.0.x | `Host.CreateApplicationBuilder` generic host the SDK plugs into. | Canonical SDK quickstart dependency. `[CITED: devblogs.microsoft.com build-a-mcp-server-in-csharp]` |
| `Microsoft.Extensions.Logging.Console` | 10.0.x | Console logger configured to log to **stderr** (stdout is the MCP transport — must stay clean). | SDK quickstart sets `LogToStandardErrorThreshold = Trace` so logs never corrupt the stdio JSON-RPC channel. `[CITED: devblogs.microsoft.com]` |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `ModelContextProtocol.Core` | 1.4.0 | Transitive dep of the main package (low-level client/server APIs). | Pulled automatically; the **test** project references it for the `McpClient` round-trip. |
| `System.Text.Json` | (in-box net10) | The SDK serializes with STJ; the host can `JsonDocument.Parse` the CLI stdout envelope to re-emit as `structuredContent`. | D-04 envelope pass-through. Note the CLI itself emits Newtonsoft JSON — the host only needs to parse it as opaque JSON, not re-serialize with matching settings. |
| `xunit` 2.9.3 + `Microsoft.NET.Test.Sdk` 17.13.0 | (match existing) | Test framework parity with `Utinni.Cli.Tests`. | The MCP round-trip test project (must be net10 — see Validation Architecture). |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Official `ModelContextProtocol` SDK | Hand-rolled JSON-RPC-2.0-over-stdio loop | **REJECTED.** The SDK is net10-native, stable (1.4.0), and gives handshake/framing/schema-gen/annotations/elicitation for free. Hand-rolling reimplements the entire `initialize` handshake, capability negotiation, framing, schema generation, and annotation plumbing — and would drift from the spec. The roadmap guard-rail ("never host the SDK in-proc in SWG.exe") is about WHERE the SDK lives (the separate net10 process — exactly this design), not WHETHER to use it. Hand-roll is the **fallback only if** the SDK were unavailable/unstable on net10 — which it is not. |
| `ModelContextProtocol` (full) | `ModelContextProtocol.Core` only | Core omits the hosting/DI extensions (`AddMcpServer`/`WithStdioServerTransport`). The host wants the full package for the attribute-discovery + generic-host wiring; Core is fine for the test client. |

**Installation:**
```bash
# In the new Utinni.Mcp net10 project:
dotnet add package ModelContextProtocol --version 1.4.0
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Logging.Console
# In the net10 test project (round-trip client):
dotnet add package ModelContextProtocol --version 1.4.0
```

**Version verification (performed this session):**
- `ModelContextProtocol` latest = **1.4.0**, published **2026-06-04**, targets **net8.0 / net9.0 / net10.0 / netstandard2.0**, depends on `Microsoft.Extensions.Hosting.Abstractions >= 10.0.7`, `Microsoft.Extensions.Caching.Abstractions >= 10.0.7`, `ModelContextProtocol.Core >= 1.4.0`. `[CITED: nuget.org/packages/ModelContextProtocol/1.4.0]`
- .NET 10 reached GA **2025-11-11** as an **LTS** release (supported to 2028-11-14). `[CITED: devblogs.microsoft.com/dotnet/announcing-dotnet-10]`
- **Self-hosted runner (this machine):** `dotnet --list-sdks` → `9.0.310`, `10.0.300`; `dotnet --list-runtimes` → `Microsoft.NETCore.App 10.0.8`. **net10 SDK + runtime present — D-02's CI risk is retired.** `[VERIFIED: dotnet --list-sdks/--list-runtimes on D:\Code\Utinni]`

## Package Legitimacy Audit

> slopcheck could not be installed in this session (no network pip access confirmed). All packages below are nonetheless verified against the **official NuGet registry** and **official Microsoft docs/blog**, which is a stronger signal than registry-existence alone. The single security-relevant package (`ModelContextProtocol`) is a first-party Microsoft-collaboration SDK with an established multi-version release history (0.1.0-preview.x → 1.0.0 → 1.4.0). Per protocol, the planner SHOULD still gate the first install behind a `checkpoint:human-verify` task confirming the package id `ModelContextProtocol` and version `1.4.0` against nuget.org.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| `ModelContextProtocol` | NuGet | 1.0.0 GA + preview history since early 2025 | high (official SDK) | github.com/modelcontextprotocol/csharp-sdk | unavailable | Approved — verify id+version at install |
| `ModelContextProtocol.Core` | NuGet | same release train | high (transitive) | same | unavailable | Approved (transitive) |
| `Microsoft.Extensions.Hosting` | NuGet | first-party MS, years | very high | dotnet/runtime | unavailable | Approved |
| `Microsoft.Extensions.Logging.Console` | NuGet | first-party MS, years | very high | dotnet/runtime | unavailable | Approved |

**Packages removed due to slopcheck [SLOP] verdict:** none.
**Packages flagged as suspicious [SUS]:** none.
**No `postinstall`-equivalent risk:** NuGet packages have no npm-style postinstall scripts; the build-time risk surface is analyzers/MSBuild tasks — none of these four ship custom build tasks of concern.

## Architecture Patterns

### System Architecture Diagram

```
                          ┌─────────────────────────────────────────────┐
   AI agent / MCP client  │              Utinni.Mcp  (net10 process)     │
   (Claude Desktop, etc.) │                                              │
        │                 │   stdin  ──► MCP SDK stdio server transport  │
        │  JSON-RPC 2.0   │            (initialize handshake, framing)   │
        ├────stdio───────►│                      │                       │
        │                 │                      ▼                       │
        │◄───stdout───────┤            Tool dispatch (attribute-routed)  │
        │   (results)     │            [McpServerTool] methods           │
   stderr◄────(logs)──────┤                      │                       │
                          │     ┌────────────────┼───────────────┐       │
                          │     │ resolvedRoot pin (startup)      │       │
                          │     │ LooseOverridePath.Resolve(root, │       │
                          │     │   relPath)  ── fail-closed       │       │
                          │     └────────────────┬───────────────┘       │
                          │                      ▼                       │
                          │   Process.Start("utinni-cli.exe",            │
                          │     [verb, resolvedPath, --typed-args…])     │
                          │     60s timeout backstop, capture stdout     │
                          └──────────────────────┬───────────────────────┘
                                                 │  (separate process boundary —
                                                 │   net472/x86 vs net10; the honest seam)
                                                 ▼
                          ┌─────────────────────────────────────────────┐
                          │        utinni-cli.exe  (net472, x86)         │
                          │   CommandLineParser → verb command → Run()   │
                          │   UtinniCoreDotNet readers/writers/savers    │
                          │   tools/ natives (compile-*/build-*)         │
                          │   emits sorted-key JSON envelope on stdout   │
                          │   exit 0 ok / 1 usage / 2 domain / 3 notfound│
                          └──────────────────────┬───────────────────────┘
                                                 ▼
                                  disk: .tre / .iff / .tab / .stf / OT
                                  (loose-override writes under resolvedRoot;
                                   repack via TreBackupPath backup-then-overwrite)
```

The agent never sees an absolute path or the CLI's argv shape — the host resolves a relative asset path under the pinned root, builds the exact CLI argv, and returns the CLI's own JSON envelope verbatim.

### Recommended Project Structure
```
Utinni.Mcp/                      # NEW net10 console project (SDK-style)
├── Utinni.Mcp.csproj            # <TargetFramework>net10.0</TargetFramework>, ModelContextProtocol 1.4.0
├── Program.cs                   # Host.CreateApplicationBuilder + AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()
├── Server/
│   ├── ResolvedRoot.cs          # startup pin: read --root|UTINNI_MCP_ROOT, GetFullPath, fail-closed; wraps LooseOverridePath.Resolve
│   └── CliDispatcher.cs         # Process.Start(utinni-cli.exe), 60s timeout, capture stdout/stderr/exit → CliInvocationResult
├── Tools/
│   ├── ReadTools.cs             # [McpServerToolType]: read_tre, inspect_iff, decode_iff, list_world_objects
│   ├── SaveTools.cs             # save_iff, save_datatable, save_stringtable, save_object_template
│   └── RepackTool.cs            # repack_tre  (Destructive=true, dry_run param default true)
└── (references UtinniCoreDotNet ONLY for LooseOverridePath — see Pitfall 4)

Utinni.Mcp.Tests/               # NEW net10 xUnit project (CANNOT be net472)
├── Utinni.Mcp.Tests.csproj     # net10.0, ModelContextProtocol 1.4.0 (McpClient), xunit
├── RoundTripTests.cs           # in-process McpClient → StdioClientTransport launches Utinni.Mcp → handshake + read + edit→save
└── ResolvedRootTests.cs        # path-escape unit test over the startup pin / LooseOverridePath

docs/ (or phase tree)/
└── MCP-SECURITY.md             # design-time threat register (mirrors 07-SECURITY.md format)
```

### Pattern 1: Minimal stdio MCP server (net10)
**What:** Generic-host bootstrap; SDK auto-discovers `[McpServerToolType]` classes and `[McpServerTool]` methods, generating input schemas from typed parameters.
**When to use:** The `Utinni.Mcp` `Program.cs` entry point.
```csharp
// Source: devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp [CITED]
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);
// Logs MUST go to stderr — stdout is the MCP JSON-RPC transport.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Pin resolvedRoot at startup (fail-closed) BEFORE the server starts; register as a singleton
// so [McpServerTool] methods can take it as a DI parameter.
var resolvedRoot = ResolvedRoot.PinOrThrow(args); // reads --root | UTINNI_MCP_ROOT, GetFullPath, must exist
builder.Services.AddSingleton(resolvedRoot);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

### Pattern 2: A read tool wrapping a CLI verb, returning structured content
**What:** Typed args → resolve path under root → spawn CLI → return the CLI envelope as both text and structured content.
```csharp
// Source: SDK attribute model [CITED: McpServerToolAttribute API]; envelope/dispatch original to Utinni.
[McpServerToolType]
public static class ReadTools
{
    [McpServerTool(ReadOnly = true, Idempotent = true),
     Description("Parse a .tre archive header + record table. Returns the utinni-cli parse-tre JSON envelope.")]
    public static async Task<CallToolResult> ReadTre(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description("Asset path relative to the configured client root. Never an absolute path.")] string relativePath)
    {
        string abs = root.Resolve(relativePath);           // LooseOverridePath under the pinned root; throws → host maps to tool error
        var r = await cli.RunAsync("parse-tre", new[] { abs });
        return CliResultMapper.ToCallToolResult(r);        // pass-through: text mirror + structuredContent = parsed envelope
    }
}
```

### Pattern 3: The destructive repack tool (off-by-default dry_run)
```csharp
[McpServerTool(Destructive = true, Idempotent = false, ReadOnly = false),
 Description("Destructively repack a .tre archive (full rebuild) after a timestamped backup. dry_run defaults to true.")]
public static async Task<CallToolResult> RepackTre(
    ResolvedRoot root, CliDispatcher cli,
    [Description("Asset path relative to the client root.")] string relativePath,
    [Description("When true (default), validates the repack without writing. Set false to actually overwrite.")] bool dry_run = true)
{
    string abs = root.Resolve(relativePath);
    if (dry_run)
        return CliResultMapper.DryRunNotice(abs); // do NOT spawn repack-tre; report what WOULD happen
    var r = await cli.RunAsync("repack-tre", new[] { abs });
    return CliResultMapper.ToCallToolResult(r);    // backupPath populated in the envelope
}
```
Note: the CLI `repack-tre` verb has **no `--dry-run` flag** (confirmed in `RepackTreCommand.cs`). The `dry_run` gate is therefore enforced **host-side** — when `dry_run=true` the host simply does not invoke the verb. If a true byte-validating dry-run is wanted, that is a Phase-13-class CLI change (out of scope) — keep the host-side gate.

### Anti-Patterns to Avoid
- **Hosting the MCP SDK inside SWG.exe** — the net472/x86 injected client must never run an LLM transport loop in its address space (Anti-Pattern 1; locked). The separate net10 process is the design.
- **Re-shaping / re-parsing-and-rebuilding the CLI envelope** — the sorted-key envelope is a stable contract; the host parses it as opaque JSON to attach as `structuredContent` and mirrors it as text, but never rewrites fields. Rewriting = forbidden business logic.
- **Accepting absolute paths from the agent** — every tool takes a relative path resolved under the pinned root; absolute/`..`/rooted inputs are rejected by `LooseOverridePath.Resolve`.
- **Writing logs to stdout** — corrupts the JSON-RPC channel; all logging goes to stderr.
- **Letting a wedged CLI hang the tool call** — every spawn carries the 60 s timeout backstop (SOE natives hang-on-error; mirrors `NativeToolRunner`).
- **Trusting tool annotations as enforcement** — they are advisory UX hints only (see Security Domain).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| MCP `initialize` handshake / capability negotiation / JSON-RPC framing | Custom stdio JSON-RPC loop | `ModelContextProtocol` SDK `WithStdioServerTransport()` | Spec-exact, maintained with Microsoft, net10-native; hand-rolling drifts from the spec (current protocol rev 2025-06-18). |
| Tool input-schema generation | Hand-written JSON Schema per tool | SDK auto-generates from typed `[McpServerTool]` method params | Typed args (record index, column id) become the schema for free; keeps args structurally constrained. |
| Tool annotations (`destructiveHint` etc.) | Hand-emitted annotation JSON | `[McpServerTool(Destructive=…, ReadOnly=…, Idempotent=…)]` | SDK emits the spec annotation object. |
| Path-traversal / root-escape defense | New canonicalizer | **Reuse `UtinniCoreDotNet.Saving.LooseOverridePath.Resolve`** | Already defends rooted paths, `..` segments, canonicalization escape, and prefix-match attacks; battle-tested in Phase 8 + the `save` verb. |
| Subprocess spawn + timeout + arg-quoting | New process runner | Mirror Phase-13 `NativeToolRunner` (Process.Start, `UseShellExecute=false`, close stdin, `WaitForExit(timeout)` + Kill, per-arg quoting) | net10 has `ProcessStartInfo.ArgumentList` (cleaner than the net472 manual quoting), but the timeout/stdin-close/kill discipline is the proven pattern. |
| Backup/recovery on repack | New backup logic | The CLI `repack-tre` verb already routes through `TreBackupPath`/`TreRepackLock` | Backups happen inside the verb; the host just invokes it. |

**Key insight:** Phase 13 deliberately pushed every byte of format/business logic into golden-tested CLI verbs precisely so Phase 14 has none to write. The MCP host is a declarative tool surface + a path canonicalizer reuse + a subprocess runner. If you find yourself parsing IFF, computing CRCs, or transforming asset data in the net10 project, you have crossed the seam.

## Decision Resolutions (D-01..D-04)

### D-01 — Tool surface shape → **Fine-grained format×intent tools; build/compile verbs deferred** `[VERIFIED: source-read of all 17 CLI verbs]`

Adopt fine-grained, one-tool-per-meaningful-capability naming (the recommended lean; coarse `read_asset`/`write_asset` rejected — over-broad shapes are un-retrofittable and lose typed per-format arg schemas). Recommended **11-tool surface** for this phase:

| MCP tool | Wraps CLI verb | Annotations | Typed args |
|----------|----------------|-------------|------------|
| `read_tre` | `parse-tre` | ReadOnly, Idempotent | relativePath |
| `inspect_iff` | `inspect-iff` | ReadOnly, Idempotent | relativePath |
| `decode_iff` | `decode-iff` | ReadOnly, Idempotent | relativePath (auto-dispatches datatable/stf/OT/appearance/structure by root form) |
| `list_world_objects` | `list-objects` | ReadOnly, Idempotent | relativePath (ws.iff) |
| `save_iff` | `apply-save-iff` | (write) | relativePath, **typed edit args** (see note) |
| `save_datatable` | `apply-save-tab` | (write) | relativePath, typed cell edits |
| `save_stringtable` | `apply-save-stf` | (write) | relativePath, typed entry edits |
| `save_object_template` | `apply-save-ot` | (write) | relativePath, typed field edits |
| `repack_tre` | `repack-tre` | **Destructive**, not Idempotent, dry_run=true default | relativePath, dry_run |
| `get_template_schema` | `compile-definition --skip-native` | ReadOnly, Idempotent | tdfRelativePath (or pre-built schema artifact) |
| `roundtrip_check` *(optional)* | `roundtrip-iff/tab/stf/ot` | ReadOnly-ish (writes to temp), Idempotent | relativePath (verify-only; supports verify-before-commit) |

**`decode_iff` collapses the read variants:** `decode-iff` already auto-dispatches on the root FORM tag (datatable/stf/OT/mesh/shader/ui-page), so a single `decode_iff` tool covers all typed reads — no need for separate `read_datatable`/`read_stf`/`read_object_template`. `inspect_iff` (raw chunk tree) stays distinct from `decode_iff` (typed model) because they answer different questions (structure vs semantics). This yields **4 read tools**, not 6+.

**Save-tool arg shape — RESOLVED post cross-AI review (2026-06-06).** The original draft of this section weighed two interpretations and tentatively recommended a **host-side two-step composition** (`roundtrip-*` to edit+verify, then `save` to persist). **That recommendation was BLOCKED by both cross-AI reviewers and is now overturned** — see `14-REVIEWS.md` (consensus finding #1) and the verified source evidence below. The two-step does **not** persist the edit:
- `SaveCommand.cs:110` — loose-override mode sets `sourcePath = destPath`; `save` re-serializes the **unchanged** on-disk file and accepts **no** mutation args.
- `RoundtripTabCommand.cs:137` — applies the mutation in memory, serializes `roundtrippedBytes`, compares untouched slices, then `EmitSuccess` — **no `WriteAtomic`/`File.WriteAllBytes`**; the mutated bytes are **discarded**.

So composing `roundtrip-* → save` verifies an edit it never commits — "byte-exact verify-before-commit" would be mislabeled and the persisted bytes are the *pre-mutation* file. MCP-02 + SC2/SC5 are unachievable that way.

**Resolution (LOCKED, user-approved):** add a **single atomic CLI verb family** — `apply-save-{tab,iff,stf,ot}` (Plan **14-03a**) — that in **one** operation applies one typed mutation → verifies byte-identity on the untouched region → `WriteAtomic`-commits the **mutated** in-memory bytes to the loose-override destination → emits a CLI envelope. Failed verify = exit 2, **no write**. The MCP `save_*` tools wrap that single verb **opaquely** and decide persist-vs-fail on the **exit code only** — the host never parses `bytesEqualUntouched` or any other domain field. This reconciles "typed structured args only" + "byte-exact verify-before-commit" + "zero business logic in the host," and closes the TOCTOU window. These verbs are golden-tested in `Utinni.Cli.Tests` first, and constitute a **scoped, documented exception** to the phase's "Phase 14 adds ZERO verbs" guard-rail (named in 14-03a and `MCP-SECURITY.md`, not silent). Interpretation 1 (re-serialize-only) is rejected because it drops the typed-edit promise; the old interpretation-2 two-step is rejected because it does not persist.

**Build/compile verbs deferred** (`compile-template`, `build-tre`, `compile-datatable`, `export-armor`, `export-weapon`): per CONTEXT D-01 + Deferred Ideas, these are authoring/CLI-primary, rarely agent-driven, and addable later without re-architecting the shim. `compile-definition` is the one exception worth surfacing as `get_template_schema` (read-only schema fetch) because it directly supports an agent understanding OT typed structure. **Do not ship the BUILD verbs as MCP tools this phase.**

### D-02 — SDK vs hand-rolled → **Official `ModelContextProtocol` 1.4.0 SDK on net10** `[CITED: nuget.org; devblogs.microsoft.com]`

Use the SDK. All three lean-conditions are met: (a) targets net10.0 explicitly; (b) stable at 1.4.0 with a real release history; (c) supports stdio server transport + tool registration + input-schema generation + the required annotations. The hand-rolled JSON-RPC fallback is **not triggered**. Pin `ModelContextProtocol` **1.4.0**. Confirmed net10 SDK (`10.0.300`) + runtime (`10.0.8`) on the self-hosted runner, so the new managed build/test lane builds there with no toolchain install.

**CI lane note:** the existing Utinni CI builds Release/x86 (net472) + native v145. The net10 `Utinni.Mcp` + `Utinni.Mcp.Tests` build with `dotnet build`/`dotnet test` against the installed net10 SDK — a *separate* lane (or step) from the x86 MSBuild lane. Worktrees are off (`use_worktrees=false`), so build inline. The net10 project must **not** be added to the x86 `Release|x86` solution configuration mapping in a way that forces the x86 MSBuild lane to compile a net10 project (it can't target x86-only). Recommend: keep `Utinni.Mcp` SDK-style and build it via `dotnet`, not the x86 MSBuild solution pass — confirm solution-config placement in planning (Claude's Discretion item).

### D-03 — Root config + elicitation → **`--root` arg + `UTINNI_MCP_ROOT` env fallback, fail-closed; elicitation advisory-only** `[CITED: StdioClientTransportOptions; ElicitationCapability]`

**Root config:** Supply `resolvedRoot` via an explicit **`--root <path>` CLI arg** to the server process, with **`UTINNI_MCP_ROOT` env-var fallback**. **Fail closed:** if neither is set, or the path doesn't `GetFullPath`/exist, the server refuses to start (write a clear stderr diagnostic and exit non-zero — the agent never gets a half-initialized server). Canonicalize once at startup; every tool path is then `LooseOverridePath.Resolve(resolvedRoot, relativePath)`.

This matches how MCP clients launch stdio servers: `StdioClientTransportOptions` exposes `Command`, `Arguments`, and `EnvironmentVariables` — so a client config (like the existing `.mcp.json` `windows-mcp` entry: `command` + `args` + `env`) supplies the root either as an arg or env var. The existing `.mcp.json` is the integration reference: the `Utinni.Mcp` registration will look like:
```jsonc
// .mcp.json (illustrative — the launch story)
"utinni-mcp": {
  "command": "D:\\Code\\Utinni\\<...>\\Utinni.Mcp.exe",   // or "dotnet" + Utinni.Mcp.dll
  "args": ["--root", "D:\\SWGEmu-Client\\SWGEmu"],
  "env": { "UTINNI_MCP_ROOT": "D:\\SWGEmu-Client\\SWGEmu" } // fallback if --root omitted
}
```

**Elicitation:** the SDK **does** expose elicitation (`IMcpServer.ElicitAsync`, gated on the client declaring the `elicitation` capability at `initialize`; primitive-typed schemas only). Wire it as an **optional confirmation on the `.tre` repack path** (when `dry_run=false`): if the client supports elicitation, prompt "confirm destructive repack of X". BUT — and `MCP-SECURITY.md` must state this plainly — **elicitation is advisory, not enforcement**: a headless agent loop may auto-accept or the client may not support it. Real safety is the structural layers (root pinning, loose-override default, the `dry_run=true` default, backup/recovery). Treat elicitation as a UX nicety, never as the gate.

### D-04 — CLI result + error/timeout mapping → **Pass envelope through as structuredContent + text; in-band failures returned, transport/hang → hard error** `[VERIFIED: source-read of envelope + exit codes; CITED: SDK structuredContent]`

**Result shaping:** Return the CLI's sorted-key JSON envelope as **both** a text content block (the raw JSON, so any client renders it) **and** `structuredContent` (the parsed envelope object, so schema-aware clients get typed access). The SDK supports `StructuredContent` + `OutputSchema`. The host parses the CLI stdout as opaque JSON (`JsonDocument.Parse`) — it does NOT rewrite fields. This keeps the shim logic-free.

**Error taxonomy** (the CLI exit-code contract is uniform across verbs — confirmed in source):

| CLI outcome | Exit code | Envelope | MCP mapping |
|-------------|-----------|----------|-------------|
| Success | 0 | `{command, result, schemaVersion}` | Tool result: text + structuredContent; `isError=false` |
| Usage error | 1 | `{command, error:{kind,message}, schemaVersion}` | Return envelope as tool result with `isError=true` (agent can self-correct args) |
| Domain failure (parse/containment/path-escape/not-supported/file-in-use) | 2 | `{command, error:{kind,message}, schemaVersion}` | Return envelope as tool result with `isError=true` (in-band; agent reasons about it) |
| File not found | 3 | `{command, error:{kind,...}}` | Return envelope, `isError=true` |
| **CLI exe missing / Process.Start fails / non-JSON on stdout / killed-after-timeout** | — | (no valid envelope) | **Hard MCP tool error** (throw `McpException` / return error result) — transport/exec failure, not a domain answer |
| **Timeout (wedged CLI / SOE-native hang)** | — | — | **Hard MCP tool error** "utinni-cli did not exit within 60s (killed)" |

So: **expected, in-band CLI failures** (exit 1/2/3 with a valid JSON envelope) are returned **as the tool result** (mark `isError=true` so the agent knows it failed, but hand it the structured envelope to reason about). **Transport/exec failures and timeouts** become **hard MCP errors**. This mirrors the lean exactly.

**Timeout value:** **60 s** per invocation (same constant as Phase-13 `NativeToolRunner.ToolTimeoutMs`). The read/save verbs are in-process net472 and finish in well under a second; 60 s is a generous backstop that only bites a wedged path. Use `Process.WaitForExit(60000)` → `Kill()` → `WaitForExit(5000)` (the proven `NativeToolRunner` discipline). Close stdin after start so any stray native `getchar()` gets EOF.

## Runtime State Inventory

> Not a rename/refactor/migration phase — this is a greenfield net10 project addition. Section included only to confirm nothing was missed.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — the MCP host stores nothing; all state is in files the CLI already manages. | none |
| Live service config | `.mcp.json` gains a new `utinni-mcp` server entry (additive; `windows-mcp` unchanged). | add registration (D-03 launch story) |
| OS-registered state | None — stdio server launched on demand by the MCP client, not OS-registered. | none |
| Secrets/env vars | New `UTINNI_MCP_ROOT` env var (the root fallback). Not a secret — a path. | document in MCP-SECURITY.md |
| Build artifacts | New `Utinni.Mcp.dll`/`.exe` (net10) + test assembly; new NuGet package restore. | CI build lane |

**Nothing found in category Stored data / OS-registered state:** verified — the server is stateless between calls; each tool call is a fresh `Process.Start`.

## Common Pitfalls

### Pitfall 1: net472/x86 test assembly cannot reference the net10 SDK
**What goes wrong:** Trying to add the MCP round-trip test to the existing `Utinni.Cli.Tests` (net472) project fails — the `ModelContextProtocol` SDK + `McpClient` are net10. The existing `InProcessCliRunner` runs the CLI in-process via `Console.SetOut` (net472).
**Why it happens:** TFM mismatch; net472 cannot load a net10 SDK assembly.
**How to avoid:** Create a **separate net10 `Utinni.Mcp.Tests`** project. The round-trip test uses `McpClient` + `StdioClientTransport` to launch the built `Utinni.Mcp.exe` as a child process (real stdio handshake), not in-process console capture.
**Warning signs:** `MSB3271`/`NU1201` target-framework-incompatible errors when adding the SDK to a net472 csproj.

### Pitfall 2: stdout pollution corrupts the JSON-RPC channel
**What goes wrong:** Any `Console.WriteLine` / default logger writing to stdout interleaves with the MCP framing and breaks the client.
**Why it happens:** The SDK uses stdout as the transport.
**How to avoid:** Configure logging to stderr (`LogToStandardErrorThreshold = LogLevel.Trace`); never `Console.Write` to stdout in tool code; the CLI subprocess's stdout is captured by the host (it never reaches the MCP stdout directly — the host re-emits it inside a proper tool-result frame).
**Warning signs:** Client reports "invalid JSON-RPC message" / handshake failure.

### Pitfall 3: SOE-native hang-on-error wedging a build tool (inherited from Phase 13)
**What goes wrong:** If a BUILD verb were ever surfaced, the underlying SOE tool hangs on an error path waiting for `getchar()`.
**Why it happens:** Documented Phase-13 gotcha (`project_phase13_cli_verbs`).
**How to avoid:** This phase defers BUILD verbs (D-01), so the risk is mostly moot — but the host's 60 s timeout backstop + stdin-close covers it regardless. The CLI itself already backstops its native subprocess; the host backstops the CLI.
**Warning signs:** A tool call that never returns; the 60 s kill fires.

### Pitfall 4: `LooseOverridePath` lives in net472 `UtinniCoreDotNet` — referencing it from net10
**What goes wrong:** The reusable `LooseOverridePath.Resolve` is in `UtinniCoreDotNet` (net472). A net10 project referencing a net472 assembly works **only** if the consumed code is BCL-only and the net472 assembly is loadable under net10 (it is, for pure-managed BCL-only code via .NET's net472→net10 compat shims), but this is fragile and may emit NU1701 warnings.
**Why it happens:** Cross-TFM reference.
**How to avoid:** **Two clean options** (planner picks): (a) multi-target `UtinniCoreDotNet.Saving.LooseOverridePath` by extracting it (it's pure System/System.IO, ~100 lines) into a tiny `netstandard2.0` shared lib both TFMs reference; or (b) **re-implement the ~40-line `Resolve` in the net10 host** (it's small, BCL-only, and the net10 host genuinely owns the startup-pin responsibility) with a **shared golden test** asserting parity against the net472 original. Option (a) is DRYer; option (b) is the cleaner seam (the host owns its own containment). `[ASSUMED]` option (a) preferred for single-source-of-truth — confirm with planner. Either way, the path-escape success-criterion test must run against whichever implementation ships.
**Warning signs:** `NU1701` "package was restored using .NETFramework … may not be fully compatible"; runtime `TypeLoadException`.

### Pitfall 5: Locating `utinni-cli.exe` at runtime
**What goes wrong:** The net10 host and the net472 CLI build to different output directories; the host can't find the exe.
**Why it happens:** Separate TFMs, separate `bin/` trees.
**How to avoid:** Mirror the Phase-13 `NativeToolResolver` pattern: prefer an explicit `--cli-path` arg / `UTINNI_CLI_PATH` env, else probe a configured/adjacent location, fail with a clear "utinni-cli.exe not found" error. Don't hard-code an absolute path. (Claude's Discretion: solution layout / how it locates the exe.)
**Warning signs:** Every tool call returns FileNotFound for the CLI.

## Code Examples

### Resolved-root startup pin (fail-closed)
```csharp
// Source: original to Utinni; wraps LooseOverridePath (or its net10 twin). [VERIFIED: LooseOverridePath.cs behavior]
public sealed class ResolvedRoot
{
    public string Path { get; }
    private ResolvedRoot(string p) => Path = p;

    public static ResolvedRoot PinOrThrow(string[] args)
    {
        string root = ReadArg(args, "--root")
                      ?? Environment.GetEnvironmentVariable("UTINNI_MCP_ROOT");
        if (string.IsNullOrEmpty(root))
            throw new InvalidOperationException(
                "fail-closed: no client root configured. Pass --root <path> or set UTINNI_MCP_ROOT.");
        string full = System.IO.Path.GetFullPath(root); // canonicalize once
        if (!Directory.Exists(full))
            throw new InvalidOperationException($"fail-closed: configured root does not exist: {full}");
        return new ResolvedRoot(full);
    }

    // Every tool path goes through here — relative only, never absolute.
    public string Resolve(string relativeAssetPath) =>
        LooseOverridePath.Resolve(Path, relativeAssetPath); // throws ArgumentException on escape → host maps to tool error
}
```

### Subprocess dispatch with timeout (net10, using ArgumentList)
```csharp
// Source: original to Utinni; mirrors Phase-13 NativeToolRunner discipline. [VERIFIED: NativeToolRunner.cs]
public sealed class CliDispatcher
{
    private const int TimeoutMs = 60_000;
    private readonly string _cliExe;
    public CliDispatcher(string cliExe) => _cliExe = cliExe;

    public async Task<CliInvocationResult> RunAsync(string verb, IReadOnlyList<string> args)
    {
        if (!File.Exists(_cliExe)) return CliInvocationResult.ExeMissing(_cliExe);
        var psi = new ProcessStartInfo
        {
            FileName = _cliExe, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        psi.ArgumentList.Add(verb);                  // net10: no manual quoting needed
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        p.StandardInput.Close();                     // EOF any stray getchar()
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(TimeoutMs);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) { try { p.Kill(true); } catch { } return CliInvocationResult.TimedOut(TimeoutMs); }
        return CliInvocationResult.Completed(p.ExitCode, await outTask, await errTask);
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| HTTP+SSE MCP transport | stdio (local) + Streamable HTTP (remote); raw SSE deprecated | spec 2025-03/2025-06 | Phase-14 uses stdio (locked); no SSE — aligns with current spec. |
| MCP C# SDK preview (`0.1.0-preview.x`) | GA `1.0.0` → `1.4.0` | 1.0.0 GA mid-2025; 1.4.0 2026-06-04 | The SDK is no longer bleeding-edge; safe to pin. |
| Tool annotations treated as guarantees | Annotations are advisory UX hints, explicitly not a security boundary | MCP guidance 2026-03 | `MCP-SECURITY.md` must encode the advisory-not-enforcement caveat. |
| net472 manual `ProcessStartInfo.Arguments` quoting | net10 `ProcessStartInfo.ArgumentList` (per-arg, no quoting) | .NET Core 2.1+ | The net10 host gets cleaner arg passing than the net472 CLI had to hand-roll. |

**Deprecated/outdated:**
- MCP `0.x` preview package versions — superseded by 1.4.0.
- SSE-only transport — deprecated; not used here.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Two-step host composition (`roundtrip-*` to apply+verify a typed edit, then `save` to persist) is acceptable as "logic-free orchestration" and does not violate the "thin dispatcher zero business logic" rule. | D-01 save tools | If rejected, a net-new combined CLI verb is needed (Phase-13-class work) OR save tools ship as re-serialize-only and the "typed edit" promise is partially unmet. Planner must rule. |
| A2 | Option (a) — extracting `LooseOverridePath` into a `netstandard2.0` shared lib — is preferred over re-implementing in net10. | Pitfall 4 | Low: option (b) (re-implement + parity golden test) is equally safe; this is a layout preference, not correctness. |
| A3 | The net10 `Utinni.Mcp` project should build via `dotnet build`/`dotnet test`, NOT the x86 MSBuild solution pass, to avoid forcing a net10 project through the x86 lane. | D-02 CI lane | Low-medium: if the solution config is mis-wired, the x86 CI lane could fail trying to build a non-x86 net10 project. Planner verifies solution-config mapping. |
| A4 | `decode_iff` alone (auto-dispatching) is sufficient for typed reads; no per-format read tools needed. | D-01 | Low: `decode-iff` source confirms auto-dispatch on root form; if an agent needs format-specific read ergonomics, add later (non-breaking). |
| A5 | The CLI `repack-tre` verb has no `--dry-run`, so the `dry_run` gate is host-side (don't-invoke-when-true). | D-04 / Pattern 3 | Low: confirmed by reading `RepackTreCommand.cs` (no dry-run flag). A true byte-validating dry-run would be a CLI change (out of scope). |

## Open Questions (RESOLVED)

Both forks were ruled by the gsd-planner during planning (2026-06-05); recorded inline here so no open decision blocks execution.

1. **Save-tool typed-edit shape (the one genuine design fork).**
   - What we know: locked constraints demand "typed structured args only" + "byte-exact verify-before-commit"; the current `save` verb is re-serialize-in-place (no edit args); the `roundtrip-*` verbs DO take typed mutations + assert untouched-byte identity.
   - What's unclear: whether to (2a) compose `roundtrip-*`(edit+verify) + `save`(persist) two-step in the host, (2b) ship re-serialize-only save tools + a separate `apply_edit` tool, or (2c) add a net-new CLI verb.
   - Original (2026-06-05) ruling: **2a** (two-step composition, no new verb). ~~Adopted.~~
   - **RE-RESOLVED → 2c (2026-06-06, post cross-AI review — OVERTURNS the 2a ruling).** Both reviewers independently returned HIGH/phase-blocking: the 2a two-step **never persists the edit** — `SaveCommand.cs:110` re-serializes the unchanged file (`sourcePath = destPath`, no mutation args) and `RoundtripTabCommand.cs:137` discards the mutated bytes (compare-only, no write). 2a is therefore rejected. **Final shape:** a single atomic CLI verb family `apply-save-{tab,iff,stf,ot}` (Plan **14-03a**) that applies one typed mutation → verifies untouched-byte identity → `WriteAtomic`-commits the **mutated** bytes in one op (failed verify = exit 2, no write); MCP `save_*` wrap that single verb opaquely and branch on exit code only. This is a scoped, documented exception to "Phase 14 adds ZERO verbs," golden-tested in `Utinni.Cli.Tests` first. Ruling now lives in **Plan 14-03a** (the verbs) + **Plan 14-03, Task 1** (the thin `save_*` wrappers). See `14-REVIEWS.md` consensus #1.

2. **Solution placement / CI lane for the net10 project.**
   - What we know: net10 SDK is installed; worktrees off; existing CI is x86 MSBuild + native v145.
   - What's unclear: exact `Utinni.sln` config mapping and CI step ordering (Claude's Discretion).
   - Recommendation: build `Utinni.Mcp` + `Utinni.Mcp.Tests` via a dedicated `dotnet build`/`dotnet test` CI step against net10; keep them out of the `Release|x86` MSBuild pass (A3).
   - **RESOLVED → dedicated net10 `dotnet test` lane.** The `Utinni.Mcp` + `Utinni.Mcp.Tests` net10 projects build/test via a dedicated `dotnet` CI step, kept OUT of the `Release|x86` MSBuild solution pass (a net10 project cannot target x86-only). Ruling lives in **Plan 14-01, Task 1** (scaffold) + **Task 3** (net10 dotnet-test CI lane).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | building `Utinni.Mcp` (net10) | ✓ | 10.0.300 | — |
| .NET 10 runtime | running `Utinni.Mcp` / tests | ✓ | 10.0.8 | — |
| .NET 9 SDK | (incidental) | ✓ | 9.0.310 | — |
| `utinni-cli.exe` (net472/x86) | every MCP tool dispatch target | ✓ (built by Phase 4/13 lane) | net472 | — (hard dependency; host fails clearly if absent) |
| `ModelContextProtocol` 1.4.0 (NuGet) | the SDK | ✓ (restorable) | 1.4.0 | hand-rolled JSON-RPC (NOT triggered) |
| MCP client for live round-trip | success criterion 5 | ✓ (in-test `McpClient` from the SDK) | 1.4.0 | recorded stdio transcript |

**Missing dependencies with no fallback:** none — net10 SDK/runtime present, CLI present, SDK restorable.
**Missing dependencies with fallback:** none blocking.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (parity with existing) — but in a **net10** test project for the MCP tests |
| Config file | new `Utinni.Mcp.Tests.csproj` (net10.0); existing `Utinni.Cli.Tests` stays net472 |
| Quick run command | `dotnet test Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj` |
| Full suite command | `dotnet test Utinni.sln` (x86 lane) **+** `dotnet test Utinni.Mcp.Tests/...` (net10 lane) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| MCP-02 (SC3) | A relative path with `..`/rooted/escape is rejected by the resolved-root pin | unit | `dotnet test --filter ResolvedRootTests` | ❌ Wave 0 |
| MCP-01 (SC5) | Real `McpClient` completes stdio handshake + `ListToolsAsync` returns the expected tools | integration | `dotnet test --filter RoundTripTests.Handshake` | ❌ Wave 0 |
| MCP-01 (SC5) | `read_tre`/`decode_iff` tool call returns the CLI envelope as structured content | integration | `dotnet test --filter RoundTripTests.ReadRoundTrip` | ❌ Wave 0 |
| MCP-02 (SC5) | edit→save tool call writes a loose-override file + returns `{written,path,bytesWritten,validated}` | integration | `dotnet test --filter RoundTripTests.EditSaveRoundTrip` | ❌ Wave 0 |
| MCP-02 (SC2) | `repack_tre` with `dry_run=true` (default) does NOT write; `dry_run=false` writes + backs up | integration | `dotnet test --filter RoundTripTests.RepackDryRun` | ❌ Wave 0 |
| MCP-02 | A wedged/missing CLI → hard MCP tool error after 60 s timeout (use a fake long-running stub) | integration | `dotnet test --filter DispatcherTests.Timeout` | ❌ Wave 0 |

**The three named testable success criteria:**
1. **Path-escape test (SC3):** unit-test `ResolvedRoot.Resolve` (or `LooseOverridePath.Resolve`) with `../../etc`, `C:\evil.iff`, `\\unc\x`, `D:foo`, `swg-clientx\loot` (prefix attack) — each must throw; a legit `creature/path.iff` must resolve under root. This re-uses the exact defenses already in `LooseOverridePath` (verified present).
2. **Real-MCP-client round-trip (SC5):** **scripted in-process `McpClient`** (recommended over recorded transcript) — `McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions { Command = "<Utinni.Mcp.exe>", Arguments = ["--root", "<fixtureRoot>"] }))`, then `ListToolsAsync()`, one `CallToolAsync("read_tre", …)`, one `CallToolAsync("save_…", …)`, assert the returned envelope fields. This exercises the genuine stdio handshake against the built server exe (DEC-C3 automatable tier — not a manual smoke). A recorded transcript is the fallback if launching a child process in CI proves flaky.
3. **Subprocess-timeout behavior (SC, implicit in MCP-02 robustness):** point the dispatcher at a stub exe that sleeps > 60 s (or never exits), assert the tool call returns a hard error within ~60 s and the child is killed. Mirrors `NativeToolRunner` timeout semantics one layer up.

### Sampling Rate
- **Per task commit:** `dotnet test Utinni.Mcp.Tests/...` (fast net10 unit + integration subset).
- **Per wave merge:** full net10 suite + the existing x86 `dotnet test Utinni.sln`.
- **Phase gate:** both lanes green before `/gsd:verify-work`.

### Wave 0 Gaps
- [ ] `Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj` — net10 xUnit project + `ModelContextProtocol` 1.4.0 ref.
- [ ] `ResolvedRootTests.cs` — path-escape unit tests (SC3).
- [ ] `RoundTripTests.cs` — `McpClient` handshake + read + edit→save + repack-dry-run (SC5/SC2).
- [ ] `DispatcherTests.cs` — timeout/exe-missing hard-error tests.
- [ ] A small fixture root dir (reuse `Utinni.Cli.Tests` fixtures: a sample `.tre`/`.iff`/`.tab`/`.stf`) under a temp resolved-root.
- [ ] CI step: net10 `dotnet test` lane added alongside the x86 MSBuild lane.

## Security Domain

> `security_enforcement` absent in config → enabled. This phase's security contract IS a first-class deliverable (`MCP-SECURITY.md`).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1 Architecture | yes | Separate-process seam (net10 host ≠ x86 client); trust boundary at the stdio + Process.Start hops. |
| V2 Authentication | no | Local stdio server launched by a trusted MCP client; no network auth surface (stdio-only, no HTTP). |
| V3 Session Management | no | Stateless per-call; no sessions. |
| V4 Access Control | yes | `resolvedRoot` fail-closed pin = the access-control boundary; agent confined to paths under root. Loose-override-default + repack-off-by-default = least-privilege write surface. |
| V5 Input Validation | yes | `LooseOverridePath.Resolve` (rooted/`..`/canonicalization/prefix-attack defenses); typed tool args (record index/column id/value) constrain inputs via auto-generated schema; CLI `UseShellExecute=false` (no shell injection). |
| V6 Cryptography | no | No crypto in scope (TRE zlib framing handled inside the CLI/core; not re-implemented here). |
| V12 Files & Resources | yes | Path containment (V4/V5 above); decompression-bomb + over-allocation defenses already in the CLI's readers (Phase-7 register); the host adds the 60 s subprocess timeout (resource bound). |

### Known Threat Patterns for {net10 MCP host → net472/x86 CLI subprocess over stdio}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Agent supplies absolute / `..` / rooted path to escape the client root | Elevation / Tampering | `LooseOverridePath.Resolve` fail-closed; reject before any file op (SC3 test). |
| Agent relies on a `readOnlyHint`/`destructiveHint` to gate behavior | Spoofing / Tampering | Annotations are **advisory only** (MCP guidance); structural layers enforce — `MCP-SECURITY.md` states this caveat plainly. |
| Destructive `.tre` repack corrupting a source archive | Tampering / DoS | Distinct off-by-default tool; `dry_run=true` default (host-gated); CLI routes through `TreBackupPath` (backup-before-overwrite) + `TreRepackLock` (refuse in-use archive). |
| Shell-metacharacter / arg-injection via tool args into the CLI | Tampering | `UseShellExecute=false` (no shell) + `ProcessStartInfo.ArgumentList` (per-arg, no concatenation). |
| Wedged CLI / SOE-native hang (DoS via no-return tool call) | DoS | 60 s `WaitForExit` + `Kill` + stdin-close; hard MCP error on timeout. |
| stdout log pollution corrupting the JSON-RPC channel | DoS (integrity) | All logging to stderr (`LogToStandardErrorThreshold`); CLI stdout captured + re-framed, never passed through raw. |
| Decompression bomb / forged counts in a malicious asset | DoS | Already mitigated in the CLI's readers (Phase-7 T-07-01/02/13 register); the host adds no new parsing surface. |
| Supply-chain: malicious NuGet package | Tampering | Pin `ModelContextProtocol 1.4.0` (first-party MS-collab SDK); `checkpoint:human-verify` the id+version at first install (slopcheck unavailable this session). |

`MCP-SECURITY.md` should mirror `07-SECURITY.md`'s format (front-matter `phase/slug/status/threats_open/asvs_level`, Trust Boundaries table, Threat Register table with `T-14-NN` ids + file:line evidence, Accepted Risks) and document the **5-layer model** (annotations → elicitation → loose-override-default → verify-before-commit → backup/recovery) with the explicit caveat that layers 1–2 are advisory and layers 3–5 are the deterministic enforcement.

## Sources

### Primary (HIGH confidence)
- `nuget.org/packages/ModelContextProtocol/1.4.0` — target frameworks (net8/9/10 + netstandard2.0), deps, publish date 2026-06-04. `[CITED]`
- `devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp` — canonical stdio server bootstrap (`AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`, stderr logging, `[McpServerTool]`). `[CITED]`
- `devblogs.microsoft.com/dotnet/announcing-dotnet-10` — .NET 10 GA 2025-11-11, LTS to 2028. `[CITED]`
- `modelcontextprotocol.github.io/csharp-sdk` API pages — `McpServerToolAttribute` (Destructive/Idempotent/ReadOnly/OpenWorld), `StructuredContent`/`OutputSchema`, `ElicitationCapability`/`ElicitAsync`. `[CITED]`
- `blog.modelcontextprotocol.io/posts/2026-03-16-tool-annotations` — annotations are advisory, not a security boundary. `[CITED]`
- `deepwiki.com/modelcontextprotocol/csharp-sdk/4.1-stdio-transport` — `StdioClientTransportOptions` (Command/Arguments/EnvironmentVariables), `McpClient.CreateAsync`. `[CITED]`
- **Live machine:** `dotnet --list-sdks` (10.0.300) / `--list-runtimes` (NETCore.App 10.0.8). `[VERIFIED]`
- **Source read:** all 17 `Utinni.Cli/Commands/*Command.cs`, `JsonOutput.cs`, `LooseOverridePath.cs`, `NativeToolRunner.cs`, `Utinni.Cli.Tests` infra. `[VERIFIED]`

### Secondary (MEDIUM confidence)
- `dometrain.com` / `deepwiki` MCP C# overviews (cross-verified against official docs).

### Tertiary (LOW confidence)
- None relied upon for load-bearing claims.

## Metadata

**Confidence breakdown:**
- Standard stack (SDK + net10): **HIGH** — version/TFM/deps verified on NuGet; net10 SDK+runtime verified on the machine.
- Architecture (two-process seam, dispatch): **HIGH** — directly mirrors the read-verified Phase-13 `NativeToolRunner` + locked constraints.
- D-01 tool surface: **HIGH** for the read/repack/schema tools; **MEDIUM** for the save-tool typed-edit shape (one genuine fork — A1/Open Q1, needs planner confirmation).
- Pitfalls: **HIGH** — grounded in source reads (TFM mismatch, `LooseOverridePath` location, CLI exit-code contract).
- Security: **HIGH** — annotations-advisory caveat from official MCP guidance; defenses verified present in `LooseOverridePath`.

**Research date:** 2026-06-05
**Valid until:** ~2026-07-05 (SDK is fast-moving — re-verify the `ModelContextProtocol` version at plan time; 1.4.0 shipped the day before this research).

## RESEARCH COMPLETE

**Phase:** 14 - Headless MCP server (`Utinni.Mcp`) — the centerpiece
**Confidence:** HIGH

### Key Findings
- **D-02 resolved decisively:** official `ModelContextProtocol` SDK **1.4.0** targets **net10.0**, ships stdio server transport + attribute tool registration + input-schema gen + the four annotations + structured content + elicitation. **net10 SDK (10.0.300) + runtime (10.0.8) are installed on the self-hosted runner** — the single biggest planning risk is retired. No hand-roll needed.
- **D-01:** fine-grained ~11-tool surface (4 read incl. auto-dispatching `decode_iff`, 4 save, 1 `repack_tre` Destructive+dry_run, `get_template_schema`, optional `roundtrip_check`); BUILD/compile verbs deferred. The one genuine fork is the **save-tool typed-edit shape** (compose `roundtrip-*`+`save` vs re-serialize-only) — flagged for planner (A1/Open Q1).
- **D-03:** `--root` arg + `UTINNI_MCP_ROOT` env fallback, fail-closed, canonicalize once via `LooseOverridePath`; elicitation wired as advisory-only confirmation on repack.
- **D-04:** pass the CLI sorted-key envelope through as `structuredContent` + text; in-band CLI failures (exit 1/2/3 with envelope) → tool result `isError=true`; transport/exec failure + 60 s timeout → hard MCP error.
- **Test architecture:** the MCP round-trip + path-escape tests must live in a **separate net10 test project** (net472 `Utinni.Cli.Tests` can't reference the net10 SDK); scripted in-process `McpClient` launches the built server exe over real stdio.

### File Created
`.planning/phases/14-headless-mcp-server-utinni-mcp-the-centerpiece/14-RESEARCH.md`

### Confidence Assessment
| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | SDK version/TFM/deps verified on NuGet; net10 verified on machine |
| Architecture | HIGH | Mirrors read-verified Phase-13 seam + locked constraints |
| Pitfalls | HIGH | Grounded in source reads (TFM mismatch, LooseOverridePath location, exit-code contract) |
| D-01 save-tool shape | HIGH | Resolved post-review → atomic `apply-save-*` verb (14-03a); two-step rejected |

### Open Questions
1. ~~Save-tool typed-edit shape~~ **RESOLVED (post cross-AI review):** atomic `apply-save-{tab,iff,stf,ot}` CLI verb family (Plan 14-03a) — applies one typed edit + verifies + commits in one op; MCP `save_*` wrap it opaquely (exit-code branch). The earlier "two-step composition (A1)" recommendation was overturned by both reviewers as non-persisting — see the RESOLVED Open Questions block above and `14-REVIEWS.md`.
2. Solution placement / CI lane for the net10 project (Claude's Discretion; recommend a dedicated `dotnet test` lane, keep out of the x86 MSBuild pass).

### Ready for Planning
Research complete. The planner can create PLAN.md files: a net10 `Utinni.Mcp` project + `Utinni.Mcp.Tests`, the ~11-tool surface, the `ResolvedRoot` pin reusing `LooseOverridePath`, the `CliDispatcher` with 60 s backstop, the envelope pass-through mapper, `MCP-SECURITY.md`, and the three testable success criteria (path-escape, McpClient round-trip, subprocess timeout).
