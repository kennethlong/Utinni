# Phase 16: Live-injected MCP bridge + Blender ecosystem boundary - Pattern Map

**Mapped:** 2026-06-13
**Files analyzed:** 12 new/modified files (6 MCP-03 track, 6 ECO-01 track)
**Analogs found:** 11 / 12 (1 doc-only file has no code analog; uses a docs/ai sibling as a structural model)

Every new file in this phase is **additive** and has a strong in-repo analog. The two tracks are cleanly separable. All excerpts below are real, verified file paths + line numbers the planner should reference directly in plan action sections.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Utinni.Mcp/Tools/LiveTools.cs` (NEW) | tool (MCP) | request-response | `Utinni.Mcp/Tools/ReadTools.cs` | exact (role + flow) |
| `Utinni.Mcp/Server/LivePipeClient.cs` (NEW) | dispatch-target / client | request-response (IPC) | `Utinni.Mcp/Server/CliDispatcher.cs` | role-match (subprocess→pipe) |
| `Utinni.Mcp/Server/ServerArgs.cs` (MODIFY: add `EnableLive`) | config | — | `Utinni.Mcp/Server/ServerArgs.cs` (self; `--root`/`--cli-path` precedent) | exact |
| `Utinni.Mcp/Program.cs` (MODIFY: conditional `WithTools<LiveTools>()`) | config / bootstrap | — | `Utinni.Mcp/Program.cs` (self; `WithToolsFromAssembly()` line) | exact |
| `UtinniCoreDotNet/.../LivePipeServer.cs` (NEW) | service (pipe server) | event-driven / request-response (IPC) | `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` + `Callbacks/GameCallbacks.cs` (marshal seam) | role-match |
| `Utinni.Mcp.Tests/LivePipeProtocolTests.cs` + `UtinniCoreDotNet.Tests/LivePipeServerTests.cs` (NEW) | test | request-response | `Utinni.Mcp.Tests/DispatcherTests.cs` (stub-child) + `LivePatchValidatorTests` (injected-state pure fn) | exact |
| `Utinni.Cli/Commands/ValidateBundleCommand.cs` (NEW) | command (CLI verb) | file-I/O / transform | `Utinni.Cli/Commands/InspectIffCommand.cs` (+ `ParseTreCommand.cs`) | exact (role + flow) |
| `Utinni.Cli/Program.cs` (MODIFY: register `validate-bundle` verb) | config / dispatch | — | `Utinni.Cli/Program.cs` (self; `ParseArguments` Type[] + `Dispatch` switch) | exact |
| `docs/ai/blender-boundary-contract.md` (NEW) | doc | — | `docs/ai/toolchain-inventory.md` (sibling doc) | structural-only |
| `Utinni.Cli.Tests/Commands/BlenderBoundaryGoldenTests.cs` (NEW) | test (golden) | file-I/O | `Utinni.Cli.Tests/Commands/ParseTreCommandTests.cs` | exact |
| `Utinni.Cli.Tests/Fixtures/blender/*` (NEW pinned fixtures) | fixture | — | existing `Utinni.Cli.Tests/Fixtures/tre/*` (in-repo, no LFS) | exact |
| `D:/Code/swg-blender-plugin/REFERENCES.md` (MODIFY: pointer note) | doc | — | existing REFERENCES.md table | structural-only |

---

## Pattern Assignments — TRACK A (MCP-03)

### `Utinni.Mcp/Tools/LiveTools.cs` (tool, request-response) — NEW

**Analog:** `Utinni.Mcp/Tools/ReadTools.cs` (the canonical thin MCP tool class). For the write/non-readonly shape (`live_reload_asset`), also mirror `Utinni.Mcp/Tools/RepackTool.cs`.

**Class + tool-attribute shape** (`ReadTools.cs:54-70`) — a `[McpServerToolType]` static class; each tool is a static `async Task<CallToolResult>` taking its DI dependencies as parameters, resolving the path under root, dispatching, mapping:
```csharp
[McpServerToolType]
public static class ReadTools
{
    [McpServerTool(Name = "read_tre", ReadOnly = true, Idempotent = true)]
    [Description("Parse a .tre archive ... and return the utinni-cli JSON envelope.")]
    public static async Task<CallToolResult> ReadTre(
        ResolvedRoot root,
        CliDispatcher cli,
        [Description(PathParamDescription)] string relativePath)
    {
        string abs = root.Resolve(relativePath);          // throws on escape → SDK tool error
        CliInvocationResult r = await cli.RunAsync("parse-tre", new[] { abs }).ConfigureAwait(false);
        return CliResultMapper.ToCallToolResult(r);        // verbatim envelope pass-through
    }
}
```

**CRITICAL D-04 deviation — do NOT decorate `LiveTools` with `[McpServerToolType]`.** RESEARCH Pitfall 1 / Assumption A1: `WithToolsFromAssembly()` (Program.cs:74) scans for `[McpServerToolType]` and would register `LiveTools` unconditionally, defeating the gate. `LiveTools` must be registered ONLY via the conditional `WithTools<LiveTools>()` (see Program.cs assignment below). The `[McpServerTool]` per-method attributes may remain. Confirm the exact scan/`WithTools<T>` interaction with a ~10-line Wave-0 spike.

**Tool shapes to build** (D-02 minimal surface):
- `live_ping` — mirror the `ReadOnly = true, Idempotent = true` attribute pair (ReadTools.cs:60). Takes `LivePipeClient` (DI). No path arg. Returns `{ listening, gameRunning, pid }`.
- `live_reload_asset` — mirror `RepackTool.cs:59` non-readonly attrs (`ReadOnly = false, Idempotent = false`). Takes `ResolvedRoot root, LivePipeClient pipe, string relativePath`. MUST call `root.Resolve(relativePath)` FIRST (ReadTools.cs:67 / RepackTool.cs:74) — the live tier must not bypass the fail-closed root — then send over the pipe. Returns `{ accepted, tier, queued, path }`.

**DI-parameter injection precedent:** tools receive `ResolvedRoot`/`CliDispatcher` as method params (ReadTools.cs:62-65); `LiveTools` receives `LivePipeClient` the same way (registered as a singleton — see Program.cs).

---

### `Utinni.Mcp/Server/LivePipeClient.cs` (dispatch-target, IPC request-response) — NEW

**Analog:** `Utinni.Mcp/Server/CliDispatcher.cs` — the existing "lone subprocess seam." `LivePipeClient` is the parallel "lone named-pipe seam." Copy its **hang-proofing discipline** wholesale.

**Class shape + injectable-timeout ctor** (`CliDispatcher.cs:56-74`):
```csharp
public class CliDispatcher                                   // non-sealed, RunAsync virtual → test stub
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
    private readonly string _cliExePath;
    private readonly TimeSpan _timeout;
    public CliDispatcher(string cliExePath) : this(cliExePath, DefaultTimeout) { }
    public CliDispatcher(string cliExePath, TimeSpan timeout) { ... }
    public virtual async Task<CliInvocationResult> RunAsync(string verb, IReadOnlyList<string> args) { ... }
}
```
Replicate for `LivePipeClient(string pipeName)` + `LivePipeClient(string pipeName, TimeSpan timeout)`; make the connect/send method `virtual` so the loopback test can substitute a recording/stub (DispatcherTests pattern). Use a SHORT connect timeout.

**Timeout / never-hang discipline to copy** (`CliDispatcher.cs:121-138`): wrap the I/O in a `CancellationTokenSource(_timeout)`; on `OperationCanceledException` return a hard-error result object, never throw, never hang. RESEARCH Pitfall 3: a no-client `live_ping` must map "no server listening" to a clean `{ listening: false }` result (mirror `CliInvocationResult.TimedOutResult` / `.ExeMissing` non-throwing returns at CliDispatcher.cs:89,132).

**Wire format (Claude's discretion — RESEARCH A3 recommendation):** 4-byte LE length-prefix + UTF-8 JSON body over `NamedPipeClientStream`. Request `{ "op": "ping" }` / `{ "op": "reload-asset", "path": ..., "ext": ... }`. Ack envelope mirrors the CLI's sorted-key shape `{ schemaVersion, op, result }` (see `JsonOutput` discipline in ParseTreCommand.cs:73).

---

### `Utinni.Mcp/Server/ServerArgs.cs` (config) — MODIFY (add `EnableLive`)

**Analog:** the file itself — extend it exactly as `--root` / `--cli-path` are handled.

**Property + flag-constant pattern** (`ServerArgs.cs:43-50`):
```csharp
public string? Root { get; private set; }
public string? CliPath { get; private set; }
private const string RootFlag = "--root";
private const string CliPathFlag = "--cli-path";
```
Add `public bool EnableLive { get; private set; }` + `private const string EnableLiveFlag = "--enable-live";`.

**Parse-loop registration** (`ServerArgs.cs:65-94`): `--enable-live` is a BOOLEAN presence flag (not value-taking), so it differs slightly from the value flags — add a branch `if (token == EnableLiveFlag) { result.EnableLive = true; continue; }` before the value-flag branches. Optionally add an env alias `UTINNI_MCP_ENABLE_LIVE` mirroring the `UTINNI_MCP_ROOT` precedent. Extend `ServerArgsTests` (the parser is test-backed; tolerant/first-wins/equals-form contract documented in the class XML doc at ServerArgs.cs:28-39).

---

### `Utinni.Mcp/Program.cs` (bootstrap) — MODIFY (conditional registration)

**Analog:** the file itself — the current `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` block (Program.cs:71-74) and the singleton-registration pattern (Program.cs:67).

**Singleton-registration precedent** (`Program.cs:65-67`):
```csharp
var serverArgs = ServerArgs.Parse(args);
var cliExePath = CliLocator.Resolve(serverArgs.CliPath);
builder.Services.AddSingleton(new CliDispatcher(cliExePath));
```
Add alongside it: `builder.Services.AddSingleton(new LivePipeClient(LivePipeName));`

**Conditional tool registration (D-04 — the load-bearing change)** — capture the builder, keep the assembly scan for existing tools, conditionally add the live tier:
```csharp
var mcp = builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();          // existing read/save/repack tools (Program.cs:74)
if (serverArgs.EnableLive)
    mcp.WithTools<LiveTools>();        // ONLY registered when --enable-live (fail-closed by absence)
```
Note the existing startup-order comment block (Program.cs:39-46) documents the fail-closed `ResolvedRoot.PinOrThrow` precedent — the live gate follows the same "refuse/omit by default" philosophy.

---

### `UtinniCoreDotNet/.../LivePipeServer.cs` (service, IPC + game-thread marshal) — NEW

**Analogs (composite):**
- **Pure-managed, game-state-injected class shape** → `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` and `UtinniCoreDotNet/Editing/LivePatchValidator.cs`.
- **Game-thread marshaling seam (reuse, do NOT rebuild)** → `UtinniCoreDotNet/Callbacks/GameCallbacks.cs`.
- **Ack-tier classification (reuse verbatim)** → `ReloadAssetClassifier.Classify(...)`.

**The marshal seam to reuse** (`GameCallbacks.cs:63, 254-257`) — RESEARCH A2; satisfies `project_rh_snapshot_no_heap_alloc` for free. The pipe-server worker thread NEVER touches game state; it enqueues one already-allocated `Action`:
```csharp
private static readonly ConcurrentQueue<Action> mainLoopCallQueue = new ConcurrentQueue<Action>();   // :63
public static void AddMainLoopCall(Action call) { mainLoopCallQueue.Enqueue(call); }                  // :254-257
// drained on the game thread each frame via DequeueMainLoopCalls → CallbackHelpers.Drain (:264-266)
```
On a `reload-asset` request the worker calls `GameCallbacks.AddMainLoopCall(() => /* reload binding */)`. RESEARCH Pitfall 2: the worker thread itself must not dereference `UtinniCore.dll` game state.

**Ack-tier classification to reuse** (`ReloadAssetClassifier.cs:124`):
```csharp
public static ReloadTier Classify(string extension, string rootTypeIdOrNull)
// → ReloadedTextures | ReloadedTerrain | PendingNextSceneChange | Unavailable
```
The ack `tier` field comes verbatim from this. `!Game.IsRunning` maps to `Unavailable` upstream (ReloadAssetClassifier.cs:78-80 doc). For most Blender `.msh`/`.mgn` assets the honest tier is `PendingNextSceneChange` — exactly the D-01 best-effort-render candor.

**Game-state-injected purity pattern** (`LivePatchValidator.cs:135-147`) — keep the server's classification/decision logic pure and BCL-only by passing `gameIsRunning` as a scalar rather than reading the binding, so it unit-tests without `UtinniCore.dll`:
```csharp
public static LivePatchValidation Validate(
    IntPtr targetAddr, int originalMappedLength, int rewrittenLength, bool gameIsRunning)
{
    if (!gameIsRunning) return LivePatchValidation.RefusedNoClient;   // :144-147
    ...
}
```
Apply this to the envelope→tier→enqueue decision path so `LivePipeServerTests` can inject `gameIsRunning`.

**Threading:** a dedicated background thread owns the `NamedPipeServerStream` accept/read loop (RESEARCH A2). Add a `PipeSecurity`/`PipeAccessRule` restricting the pipe to the current user (RESEARCH Security Domain V13). The pipe name is a shared compile-time constant referenced by both `LivePipeClient` and `LivePipeServer` (RESEARCH Runtime State Inventory).

---

### Loopback / pure-managed tests — NEW

**Analog (loopback protocol):** `Utinni.Mcp.Tests/DispatcherTests.cs` — the established "test against a stub child to avoid the real target" pattern.

**Stub-target + injectable-timeout test shape** (`DispatcherTests.cs:50-70`):
```csharp
public class DispatcherTests
{
    [Fact]
    public async Task ExeMissing_ReturnsExeFoundFalse_WithoutThrowing()
    {
        var dispatcher = new CliDispatcher(missing, TimeSpan.FromSeconds(5));
        CliInvocationResult result = await dispatcher.RunAsync("anything", Array.Empty<string>());
        Assert.False(result.ExeFound);
        Assert.False(result.TimedOut);
    }
}
```
For `LivePipeProtocolTests`: stand up a `NamedPipeServerStream` test-stub on a randomized pipe name implementing ping + reload-asset, point a `LivePipeClient` at it, assert the round-trip mapping + framing edge cases (partial read / oversize body / server-close → hard error, never hang). The DispatcherTests injectable-timeout + non-throwing-result discipline carries over directly.

**Analog (pure-managed injected-state unit):** `UtinniCoreDotNet.Tests/...LivePatchValidatorTests` (passes `gameIsRunning` as a scalar). `LivePipeServerTests` asserts envelope-parse → `ReloadAssetClassifier` tier → enqueue with `Game.IsRunning`/`mainLoopCallQueue` injected.

---

## Pattern Assignments — TRACK B (ECO-01)

### `Utinni.Cli/Commands/ValidateBundleCommand.cs` (command, file-I/O/transform) — NEW

**Analog:** `Utinni.Cli/Commands/InspectIffCommand.cs` (closest by flow — reads files, builds a JSON result, emits via `JsonOutput`). `ParseTreCommand.cs` is the secondary analog for the error-taxonomy shape.

**Verb-options + command-class shape** (`InspectIffCommand.cs:35-40, 54-80`):
```csharp
[Verb("inspect-iff", HelpText = "Emit the chunk tree of an IFF file as JSON.")]
public class InspectIffOptions
{
    [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .iff file.")]
    public string Path { get; set; }
}

public static class InspectIffCommand
{
    public static int Run(InspectIffOptions o)
    {
        if (!File.Exists(o.Path))
            return JsonOutput.EmitError("inspect-iff", "FileNotFound", "IFF file not found: " + o.Path, exitCode: 3);
        try
        {
            var doc = IffReader.Read(o.Path);
            return JsonOutput.EmitSuccess("inspect-iff", BuildResult(doc, o.Path));
        }
        catch (IffParseException ex) { return JsonOutput.EmitError("inspect-iff", ex.Kind.ToString(), ex.Message, exitCode: 2); }
        catch (IOException ex)       { return JsonOutput.EmitError("inspect-iff", "IoError", ex.Message, exitCode: 2); }
    }
}
```
Mirror exactly: `[Verb("validate-bundle", ...)]` + `ValidateBundleOptions` with a `[Value(0, ...)]` path (bundle root or `swg_export_manifest.json`); `Run` with the FileNotFound→exit-3, parse-error→exit-2, IoError→exit-2, success-envelope taxonomy. The result object NEVER pre-adds `schemaVersion`/`command` (ParseTreCommand.cs:71-75 note) — `JsonOutput.EmitSuccess` wraps it.

**Body (thin text-parse + existence-check — RESEARCH B1/B3):** parse the `swg_export_manifest.json` index, existence-check referenced assets, parse `rsp/*.rsp` `{treefile_path} @ {explicit_path}` lines (RESEARCH B2), validate the `searchPathN=` cfg fragment, optionally cross-check bucket classification against the contract rules. NO binary codec — `.rsp`/`.cfg` are TEXT (DEC-A3 clean). Reuse `parse-tre`/`decode-iff`/`inspect-iff` for the IFF/TRE assets (do not re-open them here).

---

### `Utinni.Cli/Program.cs` (dispatch) — MODIFY (register `validate-bundle`)

**Analog:** the file itself. Add `typeof(Commands.ValidateBundleOptions)` to the `ParseArguments` Type[] (Program.cs:48-70) and a `case Commands.ValidateBundleOptions o: return Commands.ValidateBundleCommand.Run(o);` to the `Dispatch` switch (Program.cs:79-103).

**WATCH-OUT (memory `project_phase13_cli_verbs`):** `ParseArguments<T..>` tops out at 16 type args — the CLI already uses the **`Type[]` overload** (Program.cs:43-48 comment) to escape that cap, so adding one more verb is safe. The `Dispatch` method switches on concrete option type (Program.cs:77-104).

---

### `docs/ai/blender-boundary-contract.md` (doc) — NEW

**Analog (structural only — no code):** `docs/ai/toolchain-inventory.md` (a sibling Utinni-authoritative `docs/ai/` reference doc; same home directory and audience).

**Must document all four (D-06), sourced from the verified research:**
1. **`.rsp` search-path contract** (RESEARCH B2): line format `{treefile_path} @ {explicit_path}`, the 6 suffix→bucket→filename rules (`.mp3`→music … `.msh`→`data_compressed_mesh_static.rsp`, else `other`), earlier-roots-win priority, the `searchPath{priority}=` / `searchPath_{sku:02d}_{priority}=` cfg dialects (Phase-7 bundle writes priority 12, sku 0).
2. **Format-version matrix** (RESEARCH B4 — mirror `UtinniCoreDotNet/Formats/Tre/TreVersion.cs:35-106` EXACTLY): `{0004,0005,0006,5000}` readable, `6000` enumerate-only. **Pitfall 4: do NOT write "5000 = encrypted" or "COT2000 = a TRE version"** — V5000 is readable, COT2000 is a master-index concept (`CotMasterIndex`), not a `TreVersion`. The stale `project_tre_version_support_gap` memory is superseded by the shipped code.
3. **Directory/bundle layout** (RESEARCH B3): the `<bundle_root>/appearance|shader|texture|rsp/ + client_search_paths.cfg + swg_export_manifest.json` tree from `export_bundle.py`.
4. **Anti-coupling rules** (RESEARCH B5): Utinni READS / Blender WRITES; neither imports the other; cites DEC-A3 + `project_swg_toolchain_crosswalk`.

---

### `Utinni.Cli.Tests/Commands/BlenderBoundaryGoldenTests.cs` + fixtures — NEW

**Analog:** `Utinni.Cli.Tests/Commands/ParseTreCommandTests.cs` (the in-process golden-test pattern) + `Utinni.Cli.Tests/Infrastructure/FixturePath.cs` (fixture resolution).

**Golden-test shape** (`ParseTreCommandTests.cs:59-68`):
```csharp
[Fact]
public void Run_WithSynthesizedV0005ThreeRecord_ExitsZeroAndMatchesGolden()
{
    var fixturePath = FixturePath.Resolve("tre/synthesized-3record-v0005.tre");
    var result = InProcessCliRunner.Run("parse-tre", fixturePath);
    Assert.Equal(0, result.ExitCode);
    var masked = MaskPath(result.Stdout, fixturePath);
    GoldenTestRunner.Matches("tre/synthesized-3record-v0005", masked);
}
```
Replicate three asserts (RESEARCH B6): `parse-tre` opens the pinned `.tre`; `decode-iff` summarizes the pinned `.msh` (MESH `AppearanceSummary`, count-only); `validate-bundle`/`validate-rsp` accepts a pinned `.rsp` against the B2 contract. Use `FixturePath.Resolve("blender/...")` + the `MaskPath` sentinel discipline (ParseTreCommandTests.cs:44-49) for the absolute-path masking.

**Fixture pinning** (RESEARCH B6 — CON-O-09 resolved: in-repo synthetic, NO LFS): pin `frn_all_bed_sm_s1_l0.msh` (6099 B) + `retail_mini_0005.tre` (119 B) + a sample `.rsp` into `Utinni.Cli.Tests/Fixtures/blender/`. `FixturePath.Resolve` (FixturePath.cs:37-50) already resolves `AppContext.BaseDirectory/Fixtures/<rel>` — no infra change; just add the `blender/` subdir alongside the existing `tre/` fixtures.

---

### `D:/Code/swg-blender-plugin/REFERENCES.md` (pointer doc) — MODIFY

**Analog (structural only):** the existing "External references" table in that file (RESEARCH B6 / D-05). Add a row pointing at Utinni's `docs/ai/blender-boundary-contract.md` as the authoritative copy. (Cross-repo edit authority is pre-authorized per the `feedback_utinniplugins_authority`-class standing-authority pattern, but this is the swg-blender-plugin repo — confirm scope at plan time; it is a one-line pointer, not a code change.)

---

## Shared Patterns

### Fail-closed path containment (ResolvedRoot)
**Source:** `Utinni.Mcp/Tools/ReadTools.cs:67` (`string abs = root.Resolve(relativePath);`)
**Apply to:** `live_reload_asset` in `LiveTools.cs` — resolve the path through `ResolvedRoot.Resolve` BEFORE any pipe send (the live tier must not bypass the root; RESEARCH A5 + Security V4). `live_ping` takes no path and is exempt.

### Verbatim envelope pass-through / sorted-key JSON
**Source:** `Utinni.Mcp/Tools/ReadTools.cs:69` (`CliResultMapper.ToCallToolResult(r)`); `Utinni.Cli/Commands/ParseTreCommand.cs:71-75` (`{ command, result, schemaVersion }`, result never pre-adds command/schemaVersion).
**Apply to:** all `live_*` tools (ack envelope mirrors `{ schemaVersion, op, result }`) and `validate-bundle` (`JsonOutput.EmitSuccess`/`EmitError`).

### Never-hang IPC discipline (injectable timeout + non-throwing hard-error result)
**Source:** `Utinni.Mcp/Server/CliDispatcher.cs:64-74` (dual ctor, injectable timeout) + `:121-138` (CTS timeout → result object, never throw).
**Apply to:** `LivePipeClient` (short connect timeout; no-client → `{ listening: false }`, never hang — RESEARCH Pitfall 3).

### CLI error taxonomy (exit-code contract)
**Source:** `Utinni.Cli/Commands/InspectIffCommand.cs:59-77` (FileNotFound→3, ParseException→2, IoError→2; generic Exception intentionally NOT caught).
**Apply to:** `ValidateBundleCommand.Run`.

### In-repo fixtures, no LFS (CON-O-09)
**Source:** `Utinni.Cli.Tests/Infrastructure/FixturePath.cs:37-50` (resolves `Fixtures/<rel>` under `AppContext.BaseDirectory`); existing `Fixtures/tre/` committed binaries.
**Apply to:** the pinned `Fixtures/blender/` Blender goldens.

### Game-thread marshal + pure-managed injected state (heap-free hot path)
**Source:** `UtinniCoreDotNet/Callbacks/GameCallbacks.cs:63,254-257` (`mainLoopCallQueue` + `AddMainLoopCall`); `UtinniCoreDotNet/Editing/LivePatchValidator.cs:135-147` (`gameIsRunning` injected as a scalar).
**Apply to:** `LivePipeServer` — enqueue one `Action`, never touch game state off the game thread; keep the decision path pure for unit testability.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `docs/ai/blender-boundary-contract.md` | doc | — | No code analog (it is prose). Uses `docs/ai/toolchain-inventory.md` as a structural/home-directory model only; all content is sourced from verified RESEARCH B2-B5. |

(There is **no codebase analog for a managed `NamedPipeServerStream` server** — RESEARCH A1 confirms no existing named-pipe/worker-thread seam in Utinni-authored native or managed code. `LivePipeServer` is genuinely net-new in its IPC mechanism, but its two hardest parts — game-thread marshaling and ack classification — are fully covered by the `GameCallbacks` + `ReloadAssetClassifier` analogs above, so it is classified role-match rather than no-analog.)

---

## Metadata

**Analog search scope:** `Utinni.Mcp/{Tools,Server}/`, `Utinni.Cli/{Commands,Program.cs}`, `Utinni.Cli.Tests/{Commands,Infrastructure,Fixtures}/`, `Utinni.Mcp.Tests/`, `UtinniCoreDotNet/{Callbacks,Saving,Editing}/`, `docs/ai/`.
**Files scanned (read in full or targeted):** Program.cs (MCP), ReadTools.cs, RepackTool.cs, CliDispatcher.cs, ServerArgs.cs, DispatcherTests.cs, ParseTreCommand.cs, InspectIffCommand.cs, DecodeIffCommand.cs (TryDecode dispatch), Program.cs (CLI), ParseTreCommandTests.cs, FixturePath.cs, GameCallbacks.cs, ReloadAssetClassifier.cs, LivePatchValidator.cs.
**Pattern extraction date:** 2026-06-13
