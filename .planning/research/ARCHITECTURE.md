# Architecture Research

**Domain:** AI-assisted desktop modding toolchain — MCP server + revived build CLIs + new DCC SubPanels, integrated into a shipped in-process injection tool (Utinni v1.0.0)
**Researched:** 2026-06-01
**Confidence:** HIGH (existing architecture read from source; MCP SDK framework support verified against NuGet/GitHub)

> Scope note: this file resolves the v2.0 *integration* forks only. The existing
> Utinni architecture (UtinniCore native → CppSharp bridge → UtinniCoreDotNet →
> TJT MEF host; `Utinni.Cli` JSON console over UtinniCoreDotNet) is taken as given
> — see `docs/ai/architecture.md`. Everything below is "how the four new
> capabilities bolt onto that without disturbing it."

---

## The four forks, resolved up front

| Fork | Decision | One-line rationale |
|------|----------|--------------------|
| **(a) MCP in-proc vs separate process** | **Separate process** (modern-.NET console), NOT in-proc inside the x86 net472 SWG-injected assembly | The official MCP C# SDK is net8.0/net9.0 + netstandard2.0; net472 consumption is fragile (Hosting/STJ/Channels redirect hell), and you never want an LLM transport loop living inside SWG.exe's address space. |
| **(a) MCP headless vs injected** | **Headless first** (file-pipeline over `Utinni.Cli`), live-injected MCP is a later, optional bridge | ~90% of agent value is read/edit/save/build on files with no client running; the CLI JSON surface already exists; injected-MCP needs a new IPC hop into SWG.exe and is a separate, riskier deliverable. |
| **(b) Revive-CLI integration** | **Wrapped subprocesses** invoked by the MCP server (and optionally by TJT), copied into a Utinni-owned `tools/` build tree (lift-and-shift), outputs land as `.iff`/`.tre` files the existing UtinniCoreDotNet readers consume | The compilers are the WRITE/BUILD tools; the byte-exact UtinniCoreDotNet writers stay the EDIT tools. They coexist by owning different verbs (compile-from-source vs mutate-existing). |
| **(c) New editors** | **MEF `IEditorPlugin` SubPanels inside TJT** — the established Wave-1 pattern, no new mechanism | DEC-C4 already locks Wave-1 editors as TJT subpanels; Terrain/Particle/WorldSnapshot follow the exact same `GetSubPanels()` seam. |
| **(d) Blender boundary** | **Pure file-format seam** — Utinni opens/previews what `swg-blender-plugin` exports; no process/library coupling | Both sides already share `.iff`/`.tre` understanding; the contract is "a directory of files + an `.rsp`/search-path manifest," not an API. |

---

## Standard Architecture

### System Overview — v2.0 target topology

```
┌──────────────────────────────────────────────────────────────────────┐
│  AI AGENT (Claude / Cursor / etc.)  —  MCP client, speaks stdio JSON-RPC│
└───────────────────────────────┬──────────────────────────────────────┘
                                │ stdio (MCP)
┌───────────────────────────────▼──────────────────────────────────────┐
│  NEW: Utinni.Mcp  (separate process, modern .NET console, AnyCPU/x64) │
│  · ModelContextProtocol SDK host  · tool registry  · arg validation   │
│  · maps MCP tool calls → subprocess invocations, parses JSON back      │
└──────┬───────────────────────────────────────────┬───────────────────┘
       │ Process.Start + stdout JSON                │ Process.Start + exit code
┌──────▼───────────────────────┐        ┌───────────▼────────────────────┐
│ utinni-cli.exe (EXISTING)    │        │ REVIVED BUILD CLIs (NEW, x86)   │
│ net472 x86 console over      │        │ tools/ — lift-and-shift @ v145  │
│ UtinniCoreDotNet             │        │ · TemplateCompiler (.tpf→.iff)  │
│ READ + EDIT (byte-exact)     │        │ · TemplateDefinitionCompiler    │
│ parse-tre/inspect-iff/       │        │ · TreeFileBuilder (src→.tre)    │
│ roundtrip-{iff,tab,stf,ot}   │        │ · DataTableTool compile path    │
│ → stable sorted-key JSON      │        │ · item exporters                │
└──────┬───────────────────────┘        └───────────┬────────────────────┘
       │ ProjectReference                            │ reads/writes
┌──────▼───────────────────────┐        ┌───────────▼────────────────────┐
│ UtinniCoreDotNet.dll          │        │  FILESYSTEM  (.tpf/.tpd source, │
│ Formats/ + Editing/ (PURE     │◀──────▶│  .iff/.tre artifacts, loose     │
│ MANAGED, headless-safe)       │  files │  overrides, .stf, datatables)   │
│ Saving/ writers byte-exact    │        └───────────┬────────────────────┘
└──────┬───────────────────────┘                     │ shared file formats
       │ same DLL, P/Invoke half                      │ (clean seam, no API)
┌──────▼───────────────────────┐        ┌───────────▼────────────────────┐
│ LIVE-INJECTED CONTEXT         │        │  swg-blender-plugin (EXTERNAL)  │
│ UtinniCore.dll in SWG.exe     │        │  Python/Blender — owns DCC mesh/│
│ + TJT MEF host (FormMain)     │        │  skel/anim authoring; emits     │
│ NEW SubPanels: Terrain,       │        │  .msh/.mgn/.skt/.sat/.rsp ...   │
│ Particle, WorldSnapshot       │        └─────────────────────────────────┘
│ (optional: live-injected MCP  │
│  bridge — LATER)              │
└───────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | New / Modified / Existing |
|-----------|----------------|---------------------------|
| **Utinni.Mcp** | MCP stdio server; tool schema; dispatch each tool to a subprocess; parse JSON envelope back to MCP result | **NEW** project |
| **utinni-cli.exe** | Headless READ + byte-exact EDIT verbs over UtinniCoreDotNet; emits versioned sorted-key JSON | EXISTING — **extended** with new verbs (compile wrappers, build-tre, validate) |
| **Revived build CLIs** (`tools/`) | Source→binary transforms: `.tpf`/`.tpd`→`.iff`, source-tree→`.tre`, datatable compile, item export | **NEW** (lift-and-shift of SOE-source tools, ported v143→v145) |
| **UtinniCoreDotNet `Formats/` + `Editing/` + `Saving/`** | Pure-managed parse/mutate/serialize; byte-exact writers; loose-override + repack save targets | EXISTING — possibly **refactored** to split a headless-only assembly (see Anti-Pattern 1) |
| **New TJT SubPanels** (Terrain, Particle, WorldSnapshot) | Interactive DCC-style editing surfaces docked in FormMain right-rail | **NEW** `IEditorPlugin`/`SubPanel` parts (UtinniPlugins repo) |
| **swg-blender-plugin** | DCC authoring (mesh/skel/anim) → file exports | EXTERNAL — **no Utinni code change**, only a documented format contract |

---

## Recommended Project Structure

New/changed footprint only (existing tree unchanged elsewhere):

```
Utinni/  (this repo)
├── Utinni.Cli/                    # EXISTING — add compile/build wrapper verbs
│   └── Commands/
│       ├── CompileTemplateCommand.cs    # NEW verb: shells tools/TemplateCompiler
│       ├── BuildTreCommand.cs           # NEW verb: shells tools/TreeFileBuilder
│       └── CompileDatatableCommand.cs   # NEW verb
├── Utinni.Mcp/                    # NEW PROJECT — modern .NET console MCP server
│   ├── Utinni.Mcp.csproj          # net8.0+ ; PackageRef ModelContextProtocol
│   ├── Program.cs                 # stdio host bootstrap
│   ├── Tools/
│   │   ├── ReadTools.cs           # parse-tre, inspect-iff, decode-iff  → utinni-cli
│   │   ├── EditTools.cs           # roundtrip-{iff,tab,stf,ot} mutate   → utinni-cli
│   │   └── BuildTools.cs          # compile-template, build-tre          → utinni-cli/tools
│   ├── CliRunner.cs               # Process.Start helper; JSON-envelope parse
│   └── ToolPaths.cs               # resolves utinni-cli.exe + tools/*.exe locations
├── tools/                         # NEW — lift-and-shifted SOE build CLIs
│   ├── shared/                    #   copied required shared libs (no D3D/renderer)
│   ├── TemplateCompiler/
│   ├── TemplateDefinitionCompiler/
│   ├── TreeFileBuilder/
│   └── tools.sln                  # builds at v145, decoupled from swg-client-v2
└── UtinniCoreDotNet/              # EXISTING — optional headless-split refactor
    └── (Formats/, Editing/, Saving/ are already headless-safe today)

UtinniPlugins/  (sibling repo)
└── TheJawaToolbox*/               # NEW SubPanels follow Wave-1 pattern
    ├── Terrain/    FormTerrainEditor + TerrainSubPanel
    ├── Particle/   FormParticleEditor + ParticleSubPanel
    └── WorldSnapshot/  grow existing Snapshot panel → placement editor
```

### Structure Rationale

- **`Utinni.Mcp/` is its own project, not a verb in `utinni-cli`:** the MCP server needs modern .NET (SDK requirement) and a long-lived stdio loop; `utinni-cli` is a short-lived net472 x86 per-invocation tool. Different runtime, different lifetime — separate process is the honest seam.
- **`tools/` is repo-local, not a reference into `swg-client-v2`:** locked lift-and-shift constraint. `swg-client-v2` has an active D3D9→D3D11 migration; building in-place would couple us to their churn. Revive targets are headless console exes with no renderer dependency, so the copy is clean.
- **New verbs live in `utinni-cli`, not in `Utinni.Mcp`:** keep the MCP server a thin dispatcher. Every capability is first a CLI verb (testable with goldens, the DEC-C3 Tier-2 pattern), then exposed as an MCP tool. The MCP layer adds zero business logic.
- **New SubPanels go in the UtinniPlugins repo:** DEC-C4 — Wave editors ship inside TJT, not as standalone framework code.

---

## Architectural Patterns

### Pattern 1: MCP-as-thin-dispatcher-over-CLI ("the CLI is the API")

**What:** The MCP server owns NO format logic. Each MCP tool is a declarative wrapper that builds an argv, `Process.Start`s `utinni-cli.exe` (or a revived compiler), reads the stdout JSON envelope, and returns it as the MCP tool result. Errors map from the existing `{ error: { kind, message } }` envelope + exit code.

**When to use:** Whenever the underlying capability already exists as (or can be added as) a CLI verb. This is the default for v2.0.

**Trade-offs:**
- (+) The MCP server is trivially testable and replaceable; the net472/modern-.NET boundary is a clean process boundary, not a binding-redirect minefield.
- (+) Reuses the entire DEC-C3 Tier-2 golden-fixture safety net — the CLI verbs are already adversarially tested.
- (+) Process isolation: a malformed asset that crashes a parser takes down a child process, not the agent's MCP server.
- (−) Per-call process spawn cost (tens of ms). Irrelevant for an interactive agent loop; relevant only for tight batch loops (mitigate later with a `--batch` verb or a persistent worker, not at v1).

**Example:**
```csharp
// Utinni.Mcp/Tools/ReadTools.cs  (modern .NET, MCP SDK)
[McpServerTool, Description("Parse a .tre archive and list its table of contents.")]
public static async Task<string> ParseTre(string path)
    => await CliRunner.RunJson("parse-tre", path);   // → utinni-cli.exe parse-tre <path>
```

### Pattern 2: Revive-and-wrap (subprocess build tools), not reimplement

**What:** The SOE compilers are lift-and-shifted into `tools/`, built at v145, and invoked as subprocesses that read source files and emit `.iff`/`.tre` artifacts to disk. They are the WRITE/BUILD half of the pipeline; UtinniCoreDotNet's byte-exact writers remain the EDIT half.

**When to use:** For deterministic source→binary transforms where reimplementing byte-exact behavior would be a large, error-prone port (`TemplateDefinitionCompiler`, `TreeFileBuilder`).

**Trade-offs:**
- (+) Cheap, high-leverage, byte-correct by construction (it's the original tool).
- (+) `TemplateCompiler` doubles as the OT Tier-2 unblock — it yields the per-class param→type map.
- (−) v143→v145 port risk (modern-STL friction, cf. CppSharp clang-11 STL pin). **This is the prime spike** — confirm `TemplateCompiler` + `TreeFileBuilder` actually compile at v145 before committing the wrap design.

### Pattern 3: Coexistence by verb ownership (compile vs mutate)

**What:** Two write paths exist and must not overlap. The split is by *operation kind*:
- **EDIT existing binary** → UtinniCoreDotNet byte-exact writers (`roundtrip-{iff,tab,stf,ot}` mutate/add/remove). Preserves untouched bytes exactly.
- **BUILD from source** → revived compilers (`.tpf`/`.tpd`→`.iff`, src-tree→`.tre`). Produces a fresh artifact from human-authored source.

**When to use:** The agent picks based on intent — "change this one param in an existing template" → EDIT verb; "compile this new template from source" → BUILD verb.

**Trade-offs:**
- (+) No double-implementation: you never have two code paths claiming to write the same format. They write at different *lifecycle stages*.
- (−) The agent (and the tool descriptions) must clearly distinguish the two so the LLM picks correctly — this is a tool-naming/description concern, not a code concern. Name them `edit_*` vs `compile_*`/`build_*`.

### Pattern 4: New editors as MEF SubPanels (unchanged Wave-1 seam)

**What:** Terrain/Particle/WorldSnapshot editors are `SubPanel` subclasses returned from an `IEditorPlugin.GetSubPanels()`, discovered by `PluginLoader` via MEF `DirectoryCatalog`, gated by `ut.ini → [Plugins]`. Identical to all five Wave-1 editors.

**When to use:** Every interactive editor. No new mechanism is introduced in v2.0 for editors.

**Trade-offs:**
- (+) Zero framework risk — the seam shipped and was demoed end-to-end against the live client in V1.
- (−) These editors are the *meatier* lift (terrain heightfields, particle live-preview). Their cost is in the editor UI + format depth, not in the plugin plumbing — which is why they come AFTER the cheap revive+wrap and headless MCP.

---

## Data Flow

### Primary flow — agent authors an asset (headless, no client)

```
AI agent
  │  MCP tool call: compile_template(source=foo.tpf)
  ▼
Utinni.Mcp  ──Process.Start──▶ utinni-cli.exe compile-template foo.tpf
  │                              │  shells tools/TemplateCompiler.exe foo.tpf -o foo.iff
  │                              ▼
  │                            foo.iff written to disk  +  JSON envelope { result: {...} }
  ◀──────────────────────────────┘
  │  MCP tool call: inspect_iff(path=foo.iff)
  ▼
Utinni.Mcp  ──Process.Start──▶ utinni-cli.exe inspect-iff foo.iff
  │                              │  UtinniCoreDotNet.Formats.Iff (pure managed)
  ◀──────────────────────────────┘  JSON: chunk tree, decoded params
  │  MCP tool call: edit_template(path=foo.iff, field=..., value=...)
  ▼
Utinni.Mcp  ──Process.Start──▶ utinni-cli.exe roundtrip-ot foo.iff --edit ... --value-int ...
                                 │  byte-exact writer; untouched params identical
                                 ▼  foo.iff rewritten + identity assertions in JSON
```

The agent loop is: **build (compiler) → inspect (reader) → edit (byte-exact writer) → re-inspect**, all over files, no SWG.exe in sight. This is the ~90%-of-value headless path and is what v2.0 should ship first.

### Secondary flow — Blender handoff (file seam)

```
swg-blender-plugin  ──export──▶  out/  (.msh/.mgn/.skt/.sat + .rsp manifest + client_search_paths.cfg)
                                   │  files only — no API call
                                   ▼
Utinni  ──open/preview──▶  utinni-cli inspect-iff / TRE Browser SubPanel / (later) live-inject preview
```

The boundary is a **directory of files + a manifest**. Utinni reads; Blender writes. Neither imports the other. The `.rsp`/search-path manifest is the only "protocol," and it is already a documented format (`swg_pipeline/rsp_builder.py` reimplements `TreeFileRspBuilder`'s `.rsp`).

### Tertiary flow — live-injected MCP (LATER, optional)

```
AI agent ──MCP──▶ Utinni.Mcp ──local IPC (named pipe)──▶ UtinniCore.dll in SWG.exe
                                                          │  drives the running client:
                                                          ▼  set scene, place object, live-reload
```
Deferred: requires a NEW IPC hop into the x86 injected process (the MCP server is out-of-proc and modern-.NET; the client half is in-proc x86). Build only after headless MCP proves the tool ergonomics.

---

## Suggested Build Order

Front-loads the cheap, high-leverage revive+wrap and headless MCP; defers the meaty editors and the optional injected-MCP bridge.

| Wave | Deliverable | Why here | Gating risk |
|------|-------------|----------|-------------|
| **0 (spike)** | Compile `TemplateCompiler` + `TreeFileBuilder` at v145 in `tools/` (lift-and-shift); confirm headless run | De-risks the entire revive strategy before any wrap design; cheapest possible failure point | v143→v145 modern-STL friction (the known unknown) |
| **1** | Wrap revived compilers as `utinni-cli` verbs (`compile-template`, `build-tre`, `compile-datatable`) + goldens | Compilers exist after Wave 0; CLI verb is the testable unit (DEC-C3 Tier-2); unblocks OT Tier-2 param→type map | low — pure wrapping + fixtures |
| **2** | `Utinni.Mcp` headless server: read+edit+build tools as thin dispatchers over `utinni-cli` | The centerpiece; everything it needs (read/edit/build verbs) now exists; pure dispatcher, no logic | low — but verify MCP SDK stdio handshake with a real client early |
| **3** | First replace editor SubPanel (Terrain OR Particle) | Meatier; needs the stable headless base under it; modder-demand-driven pick | medium — editor UI + format depth |
| **4** | WorldSnapshot/placement editor (grow Snapshot panel) + remaining editors | Extends existing panel; lower-risk than a from-scratch editor | medium |
| **5 (optional)** | Live-injected MCP bridge (named-pipe into SWG.exe) | Only after headless MCP proves the ergonomics; biggest new-mechanism risk | high — new IPC into x86 injected process |
| **parallel** | Formalize Blender file-format seam (document the contract, add open/preview verbs) | Pure documentation + reuse of existing readers; can run alongside any wave | low |

**Dependency rationale:**
- Wave 0 gates Wave 1 (can't wrap a tool that doesn't compile).
- Wave 1 gates Wave 2 (MCP build tools need the CLI build verbs).
- Wave 1 also unblocks the OT Tier-2 residual (param→type map from `TemplateCompiler`).
- Waves 3–4 (editors) depend on nothing in 0–2 except a stable repo; they're sequenced after because they're costlier, not because they're blocked.
- Wave 5 depends on Wave 2 (reuse the tool schema) and is explicitly optional/last.

---

## Integration Points

### External Services / Processes

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| AI agent (MCP client) | stdio JSON-RPC via MCP SDK | One process per agent session; `Utinni.Mcp` is the server |
| `utinni-cli.exe` | `Process.Start`, parse stdout JSON envelope, map exit code | EXISTING contract (sorted-key, `schemaVersion`, `{result}`/`{error}`) — reuse verbatim |
| Revived build CLIs | `Process.Start` from a new `utinni-cli` verb (or directly from MCP) | Outputs are files on disk; success = exit 0 + artifact present |
| `swg-blender-plugin` | Filesystem only — read its exported `.iff`/`.tre`/`.rsp` | No code dependency in either direction; contract = file formats + `.rsp` manifest |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Utinni.Mcp ↔ utinni-cli | subprocess + JSON over stdout | The load-bearing new seam; keeps modern-.NET MCP host off the net472 x86 surface |
| utinni-cli ↔ UtinniCoreDotNet | ProjectReference (in-proc, same net472 process) | EXISTING; headless `Formats/`+`Editing/`+`Saving/` only — no native DLL load needed |
| utinni-cli ↔ revived compilers | subprocess + exit code + file artifact | NEW verbs; keeps the v145-native build tools out of the managed process |
| UtinniCoreDotNet (headless half) ↔ (injected half) | Same DLL, two contexts | `Formats/Editing/Saving` are pure-managed and run with no native DLL; `Generated/UtinniCore.cs` P/Invoke + `Callbacks/` only bind when actually injected. **This is the key existing fact that makes headless-first possible.** |
| TJT host ↔ new SubPanels | MEF `IEditorPlugin.GetSubPanels()` | EXISTING Wave-1 seam, unchanged |

---

## Anti-Patterns

### Anti-Pattern 1: Putting the MCP server in-process inside SWG.exe (or inside UtinniCoreDotNet.dll)

**What people do:** Reach for "expose an MCP endpoint from the running editor" because the editor already has the format code loaded, and try to consume the MCP SDK from the net472 x86 assembly via its netstandard2.0 face.

**Why it's wrong:** (1) The official MCP C# SDK targets net8/net9; its netstandard2.0 surface is *technically* loadable on net472 but drags `Microsoft.Extensions.*`, modern `System.Text.Json`, and `System.Threading.Channels` into an x86 assembly that's already injected into a 2010-era game — binding-redirect and CRT-boundary risk for near-zero benefit. (2) ~90% of agent value needs no running client at all. (3) An LLM-driven transport loop inside the game process is an operational liability (it can't survive a client crash; it competes with the game thread).

**Do this instead:** Separate modern-.NET console process (`Utinni.Mcp`) that shells out to the existing `utinni-cli.exe`. The injected client stays untouched in v2.0. Add a live-injected bridge only later, as an explicit optional wave, via a narrow named-pipe IPC — never by hosting the SDK in-proc.

### Anti-Pattern 2: Reimplementing the SOE compilers in managed C#

**What people do:** Decide the byte-exact UtinniCoreDotNet writers should "just also do compile-from-source" so everything is one codebase.

**Why it's wrong:** `TemplateDefinitionCompiler`/`TreeFileBuilder` encode 2003-era SOE pipeline semantics; a from-scratch managed reimplementation is a large, byte-fragile port that duplicates a tool that already exists and is correct. It also blows the cheap-first strategy.

**Do this instead:** Lift-and-shift + wrap. Keep the two write paths separate by lifecycle (BUILD-from-source = compiler; EDIT-existing = byte-exact writer). Reimplement in managed code later only where live editing or round-trip demands it.

### Anti-Pattern 3: Building the revive tools in-place against swg-client-v2

**What people do:** Point the new build at `swg-client-v2`'s `swg.sln` to avoid copying source.

**Why it's wrong:** `swg-client-v2` has an active D3D9→D3D11 migration; coupling Utinni's build to that tree means their churn breaks our CI, and our v145 bump leaks into their v143 world. (Locked constraint.)

**Do this instead:** Copy source + required shared libs into repo-local `tools/`, build at v145, borrow swg-client-v2's SOE-source modernization as the *base* but own the v143→v145 delta. The revive targets are renderer-free, so the copy is clean.

### Anti-Pattern 4: Letting the MCP server own format/business logic

**What people do:** Parse IFF or compute byte-exactness inside `Utinni.Mcp` because "it's right there."

**Why it's wrong:** Splits logic across the process boundary, bypasses the DEC-C3 golden-fixture coverage, and forces format code to be re-ported to modern .NET.

**Do this instead:** Every capability is a `utinni-cli` verb first (golden-tested), then a one-line MCP dispatcher. The MCP layer is argv-build + JSON-parse only.

---

## Confidence & Open Questions

**HIGH confidence:**
- Existing headless pipeline is real and sufficient as the MCP substrate — `utinni-cli` ProjectReferences UtinniCoreDotNet, runs `Formats/Editing/Saving` with no native DLL, even probes PE exports without `LoadLibrary` (read from source).
- MCP C# SDK targets net8/net9 + netstandard2.0, NOT net472 first-class (verified NuGet + GitHub). → separate-process recommendation is well-grounded.
- New editors need no new mechanism (Wave-1 MEF SubPanel seam shipped + demoed in V1).

**MEDIUM confidence:**
- v143→v145 revive feasibility for the compilers. This is the **named spike (Wave 0)** and the single biggest gating risk; treat the build order as contingent on it. If a tool refuses to compile at v145, fall back to building that one tool at v143 in `tools/` and still wrap it (the subprocess seam is toolset-agnostic) — the lift-and-shift constraint forbids building in `swg-client-v2`, not building at v143 in our own tree.

**Open questions for phase-specific research:**
- Exact `.rsp`/search-path manifest contract for the Blender seam (where the published bundle lands, how Utinni discovers it) — defer to the Blender-boundary wave; `swg_pipeline/rsp_builder.py` is the reference.
- Whether `utinni-cli` should grow a persistent `--batch`/server mode to amortize process-spawn cost for agent loops — defer; not needed at v1, premature optimization now.
- Live-injected MCP IPC mechanism (named pipe vs local socket) and how it reconciles modern-.NET MCP host with x86 in-proc client — defer to the optional Wave 5.

## Sources

- `Utinni/docs/ai/architecture.md`, `plugin-framework.md`, `sdk.md` — existing topology (read from repo)
- `Utinni/Utinni.Cli/**` — headless CLI structure, JSON envelope, write verbs, PE-probe-without-LoadLibrary (read from repo, HIGH)
- `Utinni/UtinniCoreDotNet/UtinniCoreDotNet.csproj` — confirms Formats/Editing/Saving are pure-managed alongside the WinForms/P-Invoke half (read from repo, HIGH)
- `Utinni/docs/ai/toolchain-inventory.md` — revive/replace cross-walk, lift-and-shift + v145 constraint (read from repo, HIGH)
- `.planning/PROJECT.md` — v2.0 scope, DEC-C4, locked constraints (read from repo, HIGH)
- [modelcontextprotocol/csharp-sdk (GitHub)](https://github.com/modelcontextprotocol/csharp-sdk) — official SDK, stdio transport (MEDIUM)
- [ModelContextProtocol on NuGet](https://www.nuget.org/packages/ModelContextProtocol/) — target frameworks net8/9/10 + netstandard2.0, no first-class net472 (MEDIUM-HIGH, verified)
- [Build an MCP server in C# — .NET Blog](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/) — console+stdio pattern (MEDIUM)

---
*Architecture research for: AI-assisted SWG modding toolchain integration (Utinni v2.0)*
*Researched: 2026-06-01*
