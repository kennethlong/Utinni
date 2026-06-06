# Phase 14: Headless MCP server (`Utinni.Mcp`) — the centerpiece - Pattern Map

**Mapped:** 2026-06-05
**Files analyzed:** 13 (8 source + 3 test + 1 csproj-pair + 1 doc deliverable)
**Analogs found:** 11 / 13 (2 are net-new shapes with partial/structural analogs)

> **CRITICAL TFM BOUNDARY (planner must honor):** `Utinni.Mcp` and `Utinni.Mcp.Tests` are
> **net10**; every analog below lives in **net472** (`Utinni.Cli`, `UtinniCoreDotNet`,
> `Utinni.Cli.Tests`). The analogs supply *discipline and shape* (subprocess timeout, fail-closed
> containment, envelope contract, exit-code taxonomy, test fixture style) — they are **copied as
> patterns, not referenced as assemblies**. The one genuine cross-TFM reference question
> (`LooseOverridePath` net472 → net10 host) is called out in **Shared Pattern: Path Containment**
> and Research Pitfall 4. Do NOT add a net472 ProjectReference from a net10 project as the default.

---

## File Classification

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `Utinni.Mcp/Program.cs` | entry/host bootstrap | request-response (stdio loop) | `Utinni.Cli/Program.cs` (verb-dispatch entry) | role-match (different framework: generic-host vs CommandLineParser) |
| `Utinni.Mcp/Server/ResolvedRoot.cs` | config/guard | transform (path canonicalize, fail-closed) | `UtinniCoreDotNet/Saving/LooseOverridePath.cs` | exact (containment) + the `PinOrThrow` startup half is net-new |
| `Utinni.Mcp/Server/CliDispatcher.cs` | service (subprocess seam) | request-response (spawn + capture) | `Utinni.Cli/Commands/Subprocess/NativeToolRunner.cs` | exact |
| (CLI exe locator, inside `CliDispatcher` or sibling) | utility (path probe) | transform | `Utinni.Cli/Commands/Subprocess/NativeToolResolver.cs` | exact |
| `Utinni.Mcp/Tools/ReadTools.cs` | controller (MCP tool surface) | request-response | `Utinni.Cli/Commands/*Command.cs` verb wrappers (e.g. `ParseTreCommand`) | role-match (SDK attributes vs CommandLineParser verbs) |
| `Utinni.Mcp/Tools/SaveTools.cs` | controller (write tools) | CRUD (typed edit → persist) | `Utinni.Cli/Commands/SaveCommand.cs` + `RoundtripTabCommand.cs` (typed-mutation args) | role-match + composition (see Save-tool fork) |
| `Utinni.Mcp/Tools/RepackTool.cs` | controller (destructive tool) | CRUD (destructive, dry_run-gated) | `Utinni.Cli/Commands/RepackTreCommand.cs` | exact (the verb it wraps) |
| `Utinni.Mcp/Tools/CliResultMapper.cs` (envelope mapper) | utility (pass-through mapper) | transform (JSON → MCP content) | `Utinni.Cli/Output/JsonOutput.cs` (the envelope contract it passes through) | role-match (consumer of the same contract) |
| `Utinni.Mcp/Utinni.Mcp.csproj` | config | — | (net-new SDK-style; no net472 csproj analog) | no analog (new TFM) |
| `Utinni.Mcp.Tests/Utinni.Mcp.Tests.csproj` | config (test) | — | `Utinni.Cli.Tests` csproj structure | role-match (must be net10, NOT net472) |
| `Utinni.Mcp.Tests/RoundTripTests.cs` | test (integration) | request-response | (net-new: real `McpClient` stdio) — closest discipline `Utinni.Cli.Tests/Commands/SaveCommandTests.cs` (fixture+temp-dir+envelope-assert) | partial (new transport) |
| `Utinni.Mcp.Tests/ResolvedRootTests.cs` | test (unit) | transform | path-escape assertions over `LooseOverridePath` (see `07-SECURITY.md` T-07-05) | role-match |
| `Utinni.Mcp.Tests/DispatcherTests.cs` | test (integration) | request-response | `Utinni.Cli.Tests/Subprocess/NativeToolRunnerTests.cs` | exact |
| `MCP-SECURITY.md` (doc deliverable) | doc (threat register) | — | `.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-SECURITY.md` | exact (format) |

---

## Pattern Assignments

### `Utinni.Mcp/Server/CliDispatcher.cs` (service, subprocess seam)

**Analog:** `Utinni.Cli/Commands/Subprocess/NativeToolRunner.cs` — the proven subprocess-with-timeout
discipline, one layer up. The MCP host spawns `utinni-cli.exe`; `NativeToolRunner` spawns the SOE
build natives. **Same hang-proofing, same 60 s constant, same stdin-close, same kill-on-timeout.**

**Timeout constant + rationale** (`NativeToolRunner.cs:70`):
```csharp
// Wall-clock backstop for a single native-tool invocation. Generous for any real build ...
// while bounding a wedged error/exit path to a finite wait.
private const int ToolTimeoutMs = 60000;
```
> Use the **same 60_000 ms** in `CliDispatcher` (Research D-04). The CLI's own read/save verbs
> finish in well under a second; 60 s only bites a wedged path (SOE-native hang inherited via the CLI).

**Spawn + stdin-close + capture + kill-on-timeout — copy this discipline exactly** (`NativeToolRunner.cs:135-173`):
```csharp
var psi = new ProcessStartInfo
{
    FileName = toolExe,
    Arguments = BuildArguments(args),   // net472 manual quoting — see net10 NOTE below
    UseShellExecute = false,            // NO shell → no metacharacter injection (T-13-04 / ASVS V5)
    RedirectStandardInput = true,       // close stdin so a native getchar() gets EOF, never hangs
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};
using (var process = new Process())
{
    process.StartInfo = psi;
    process.Start();
    process.StandardInput.Close();      // EOF any stray getchar() (usage/error paths)
    Task<string> outTask = process.StandardOutput.ReadToEndAsync();
    Task<string> errTask = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(ToolTimeoutMs))
    {
        try { process.Kill(); } catch { /* already exiting */ }
        process.WaitForExit(5000);
        return new NativeToolResult { ExeFound = true, TimedOut = true };
    }
    return new NativeToolResult { ExeFound = true, ExitCode = process.ExitCode,
        Stdout = outTask.Result, Stderr = errTask.Result };
}
```

**net10 IMPROVEMENT over the analog (Research State-of-the-Art):** the analog hand-rolls
`BuildArguments`/`AppendArgument` (the entire CommandLineToArgvW quoting algorithm, `NativeToolRunner.cs:224-289`)
*because net472 `ProcessStartInfo` has no `ArgumentList`*. **net10 HAS `psi.ArgumentList.Add(...)`** —
the host adds each argv entry per-arg with zero manual quoting. **Do NOT port the 65-line quoting
algorithm; use `ArgumentList`.** The security property (no shell-metachar / arg-boundary injection)
is preserved structurally by `UseShellExecute=false` + `ArgumentList`. Research's
`CliDispatcher` example (RESEARCH.md:408-438) shows the net10 `ArgumentList` + `WaitForExitAsync(cts.Token)`
form — prefer it.

**EXE-missing handling** (`NativeToolRunner.cs:130-133`): `File.Exists(toolExe)` check returns an
`ExeFound=false` result WITHOUT throwing → host maps to a hard MCP error (transport/exec failure,
Research D-04 taxonomy row "CLI exe missing").

---

### CLI exe locator (utility, inside or beside `CliDispatcher.cs`)

**Analog:** `Utinni.Cli/Commands/Subprocess/NativeToolResolver.cs` — the explicit-override → adjacent-probe
→ best-effort-fallback shape. The MCP host locating `utinni-cli.exe` mirrors this for finding the CLI exe.

**Full pattern to mirror** (`NativeToolResolver.cs:40-63`):
```csharp
public static string Resolve(string toolPathOverride, string toolBaseName)
{
    if (!string.IsNullOrEmpty(toolPathOverride)) return toolPathOverride;  // explicit --cli-path / env wins

    string baseDir = AppContext.BaseDirectory;            // probe adjacent to the host assembly
    string[] candidates =
    {
        Path.Combine(baseDir, toolBaseName + ".exe"),
        Path.Combine(baseDir, toolBaseName + "_r.exe"),   // (SOE build suffixes — likely N/A for utinni-cli)
        Path.Combine(baseDir, toolBaseName + "_d.exe"),
        Path.Combine(baseDir, toolBaseName + "_o.exe")
    };
    foreach (string candidate in candidates)
        if (File.Exists(candidate)) return candidate;
    return candidates[0];   // return canonical name so the File.Exists error message is sensible
}
```
> **Adapt for the MCP host:** prefer `--cli-path` arg / `UTINNI_CLI_PATH` env (Research Pitfall 5),
> else probe `utinni-cli.exe` adjacent / at a configured relative location (the net10 host and net472
> CLI build to *different* `bin/` trees — Research Pitfall 5 warns of exactly this). Drop the
> `_r/_d/_o` suffix variants (those are SOE-native build conventions; `utinni-cli` ships one name).
> Return best-effort even when missing so the dispatcher's `File.Exists` surfaces a clear
> "utinni-cli.exe not found" hard error.

---

### `Utinni.Mcp/Server/ResolvedRoot.cs` (config/guard, transform)

**Analog:** `UtinniCoreDotNet/Saving/LooseOverridePath.cs` — the canonical root-containment defense.
`ResolvedRoot` is **two halves**: (1) a net-new **`PinOrThrow(args)` startup pin** (no direct analog —
Research code example RESEARCH.md:382-405 is the template), and (2) a per-call **`Resolve(relativePath)`**
that delegates to / re-implements `LooseOverridePath.Resolve`.

**The containment defenses to preserve (THE security contract for SC3)** — `LooseOverridePath.cs`
implements every defense the path-escape test must exercise:

| Defense | Evidence | Rejects |
|---------|----------|---------|
| canonicalize root once at entry | `LooseOverridePath.cs:90-99` | `C:\swg-client\..\swg-client` false-rejects |
| reject rooted relPath | `LooseOverridePath.cs:103-108` | `C:\evil.iff`, `D:foo`, `\unc`, `/abs` |
| explicit `..` segment scan (split on `/` AND `\`) | `LooseOverridePath.cs:113-122` | `../../etc` BEFORE GetFullPath can escape |
| trailing-separator + `StartsWith` (OrdinalIgnoreCase) | `LooseOverridePath.cs:128-164` | prefix attack `C:\swg-clientx\loot` vs root `C:\swg-client` |

**Per-call delegate shape** (RESEARCH.md:403-404, wrapping the analog):
```csharp
public string Resolve(string relativeAssetPath) =>
    LooseOverridePath.Resolve(Path, relativeAssetPath); // throws ArgumentException on escape → host maps to tool error
```

**Startup-pin shape (net-new — fail-closed)** (RESEARCH.md:389-400):
```csharp
public static ResolvedRoot PinOrThrow(string[] args)
{
    string root = ReadArg(args, "--root") ?? Environment.GetEnvironmentVariable("UTINNI_MCP_ROOT");
    if (string.IsNullOrEmpty(root))
        throw new InvalidOperationException("fail-closed: no client root configured. Pass --root <path> or set UTINNI_MCP_ROOT.");
    string full = Path.GetFullPath(root);                 // canonicalize once
    if (!Directory.Exists(full))
        throw new InvalidOperationException($"fail-closed: configured root does not exist: {full}");
    return new ResolvedRoot(full);
}
```
> See **Shared Pattern: Path Containment** for the net472→net10 cross-TFM decision (extract-to-netstandard2.0
> vs re-implement-with-parity-golden — Research Pitfall 4 / A2). Whichever ships, `ResolvedRootTests.cs`
> runs against it.

---

### `Utinni.Mcp/Tools/ReadTools.cs` (controller, request-response)

**Analog:** `Utinni.Cli/Commands/*Command.cs` verb wrappers (the read verbs `ParseTreCommand`,
`InspectIffCommand`, `DecodeIffCommand`, `ListObjectsCommand`). Each MCP read tool is a **thin
attribute-decorated wrapper** that resolves the path under root, dispatches the CLI verb, and
returns the envelope — it owns NO format logic (the verb does).

**Verb → tool mapping (Research D-01, 4 read tools — `decode_iff` collapses the typed reads):**

| MCP tool | CLI verb (confirmed `[Verb]` name) | Annotations |
|----------|-----------------------------------|-------------|
| `read_tre` | `parse-tre` (`ParseTreCommand.cs`) | ReadOnly, Idempotent |
| `inspect_iff` | `inspect-iff` (`InspectIffCommand.cs`) | ReadOnly, Idempotent |
| `decode_iff` | `decode-iff` (`DecodeIffCommand.cs` — auto-dispatches datatable/stf/OT by root FORM) | ReadOnly, Idempotent |
| `list_world_objects` | `list-objects` (`ListObjectsCommand.cs`) | ReadOnly, Idempotent |

**Tool shape to copy** (RESEARCH.md:206-220 — the SDK attribute model; path-resolve + dispatch + map):
```csharp
[McpServerToolType]
public static class ReadTools
{
    [McpServerTool(ReadOnly = true, Idempotent = true),
     Description("Parse a .tre archive header + record table. Returns the utinni-cli parse-tre JSON envelope.")]
    public static async Task<CallToolResult> ReadTre(
        ResolvedRoot root, CliDispatcher cli,
        [Description("Asset path relative to the configured client root. Never an absolute path.")] string relativePath)
    {
        string abs = root.Resolve(relativePath);                  // containment; throws → tool error
        var r = await cli.RunAsync("parse-tre", new[] { abs });
        return CliResultMapper.ToCallToolResult(r);               // pass-through envelope
    }
}
```
> **Anti-pattern (Research):** do NOT parse/transform the CLI's output inside the tool — that is the
> forbidden business logic crossing the seam. The tool's only jobs: resolve path, pick verb+args,
> call dispatcher, hand the result to the mapper.

---

### `Utinni.Mcp/Tools/SaveTools.cs` (controller, write — CRUD)

**Analogs:** `Utinni.Cli/Commands/SaveCommand.cs` (the persist leg + the locked envelope + the
loose-override default + the `.tre`→`repack-tre` redirect) **AND** `Utinni.Cli/Commands/RoundtripTabCommand.cs`
(the **typed-mutation arg shape** the locked "typed args only" constraint demands).

**THE ONE GENUINE FORK (Research D-01 / Open Q1 / A1 — planner MUST rule):**
- The `save` verb is **re-serialize-in-place** — it does NOT take typed edit args
  (`SaveCommand.cs:75-143`: load → `DetectFormat` → `Serialize` → `WriteAtomic` → envelope).
- The `roundtrip-*` verbs DO take typed mutations + assert byte-exact-on-untouched. Confirmed in
  `RoundtripTabCommand.cs:44-53`: `--mutate-cell row,col` + `--mutate-value`, `--remove-row`,
  `--remove-column`. (Sibling shapes: `roundtrip-iff` `--mutate-leaf`/`--remove-leaf`; `roundtrip-ot`
  add/remove/edit override; `roundtrip-stf` entry edit.)
- **Research recommendation (interpretation 2 / A1):** the host composes a **two-step**:
  wrap `roundtrip-*` to apply ONE typed edit + verify byte-exact, then `save` to persist to
  loose-override. This satisfies BOTH locked constraints ("typed args only" + "verify-before-commit")
  with zero new CLI work, and is **logic-free orchestration** (arguably allowed). Planner confirms A1.

**Locked envelope the save tools pass through (DO NOT reshape)** (`SaveCommand.cs:135-142`):
```csharp
var result = new JObject
{
    ["backupPath"] = JValue.CreateNull(),  // loose-override takes no backup (D-10: repack owns backups)
    ["bytesWritten"] = serialized.Length,
    ["path"] = destPath,
    ["validated"] = validated,             // re-parse check (verify-before-commit signal)
    ["written"] = true
};
return JsonOutput.EmitSuccess("save", result);
```

**Loose-override containment the save verb already enforces** (`SaveCommand.cs:102-110`) — the host's
`ResolvedRoot` pins the SAME root and passes only relative paths; the CLI's `--root` + `LooseOverridePath`
is the defense-in-depth second gate:
```csharp
try { destPath = LooseOverridePath.Resolve(o.Root, o.Asset); }      // rejects ../rooted
catch (ArgumentException ex) { return JsonOutput.EmitError("save", "PathContainment", ex.Message, exitCode: 2); }
```

**Save tools (4, Research D-01):** `save_iff`, `save_datatable`, `save_stringtable`,
`save_object_template` — each wraps the relevant `roundtrip-*`(edit+verify)+`save`(persist) pair.

---

### `Utinni.Mcp/Tools/RepackTool.cs` (controller, destructive — CRUD, dry_run-gated)

**Analog:** `Utinni.Cli/Commands/RepackTreCommand.cs` — the destructive verb this single off-by-default
tool wraps. The backup/lock/refuse-encrypted safety lives INSIDE the verb; the tool just gates `dry_run`.

**What the verb already does (the host does NOT re-implement — `RepackTreCommand.cs:57-103`):**
- `TreRepackLock.Probe` → refuse a live-client-held archive (`FileInUse`, exit 2) — `:74-80`
- backup BEFORE overwrite via `TreBackupPath.NextAvailable` — `:88-89`
- `TreWriter.Repack` throws `NotSupportedException` for V6000/encrypted → exit 2 BEFORE any write — `:70,105-107`
- same locked envelope as save, **`backupPath` POPULATED** — `:95-102`

**dry_run gate is HOST-SIDE (Research Pattern 3 / A5):** the `repack-tre` verb has **no `--dry-run`
flag** (confirmed — `RepackTreOptions` has only `[Value(0)] Path`). So when `dry_run=true` (the
default) the host simply does NOT spawn `repack-tre`:
```csharp
[McpServerTool(Destructive = true, Idempotent = false, ReadOnly = false), Description("Destructively repack a .tre after a timestamped backup. dry_run defaults to true.")]
public static async Task<CallToolResult> RepackTre(ResolvedRoot root, CliDispatcher cli,
    string relativePath, bool dry_run = true)
{
    string abs = root.Resolve(relativePath);
    if (dry_run) return CliResultMapper.DryRunNotice(abs);   // do NOT invoke the verb
    var r = await cli.RunAsync("repack-tre", new[] { abs }); // backupPath populated in envelope
    return CliResultMapper.ToCallToolResult(r);
}
```

---

### `Utinni.Mcp/Tools/CliResultMapper.cs` (utility, pass-through transform)

**Analog (the contract it consumes):** `Utinni.Cli/Output/JsonOutput.cs` — the sorted-key envelope
the mapper passes through UNCHANGED. The host **parses it as opaque JSON** (`JsonDocument.Parse`) to
attach as `structuredContent` + mirror as text — it never rewrites fields (Research D-04 anti-pattern).

**The envelope shapes the mapper receives** (`JsonOutput.cs:50-85`):
```csharp
// SUCCESS: { "command": cmd, "result": {...}, "schemaVersion": 1 }   (exit 0)
// ERROR:   { "command": cmd, "error": { "kind": k, "message": m }, "schemaVersion": 1 }  (exit verbatim)
```

**Exit-code → MCP mapping (Research D-04 taxonomy — the mapper's decision table):**

| CLI outcome | Exit | Envelope present? | MCP mapping |
|-------------|------|-------------------|-------------|
| Success | 0 | yes (`result`) | text + structuredContent, `isError=false` |
| Usage / Domain / NotFound | 1 / 2 / 3 | yes (`error`) | **return envelope as tool result, `isError=true`** (in-band; agent self-corrects) |
| exe missing / Process.Start fail / non-JSON stdout / killed | — | NO | **hard MCP error** (`McpException`) |
| timeout (60 s) | — | NO | **hard MCP error** "utinni-cli did not exit within 60s (killed)" |

> The CLI exit-code contract is uniform across verbs (0 ok / 1 usage / 2 domain / 3 notfound) — see
> `NativeToolRunner.Run` (`:91-105`) and every `*Command.Run` for the same `EmitError(..., exitCode:)`
> pattern. The mapper keys off (a) did we get a valid JSON envelope and (b) the exit code.

---

### `Utinni.Mcp.Tests/DispatcherTests.cs` (test, integration)

**Analog:** `Utinni.Cli.Tests/Subprocess/NativeToolRunnerTests.cs` — the exact timeout / exit-code /
missing-exe assertion style, one layer up. Drives a stub child for deterministic outcomes.

**Patterns to copy:**
- **Missing-exe → hard error without throwing** (`NativeToolRunnerTests.cs:113-127`): point at a
  random non-existent `.exe`, assert the dispatcher returns the exe-missing result (host → hard MCP error).
- **Non-zero exit mapping** (`NativeToolRunnerTests.cs:99-111`): `cmd /c exit 7` style stub.
- **Timeout (SC, Research §Validation 3):** point the dispatcher at a stub that sleeps > 60 s (or
  `cmd /c pause` with stdin closed) and assert a hard error within ~60 s + child killed. The analog
  uses `%ComSpec%` (`cmd.exe`, always present — `:44-47`) as the deterministic stub driver; reuse that.
- **ConsoleLock + StringWriter capture** (`NativeToolRunnerTests.cs:40,49-66`): net472 uses
  `Console.SetOut`; the net10 dispatcher returns a result object directly (no console capture needed)
  — assert on the returned `CliInvocationResult`, NOT on captured stdout.

---

### `Utinni.Mcp.Tests/ResolvedRootTests.cs` (test, unit) — SC3 path-escape

**Analog:** the path-containment threat (`07-SECURITY.md` T-07-05 row) + the defenses in
`LooseOverridePath.cs`. This is the **named SC3 success-criterion test**.

**Required reject cases (Research §Validation 1 — each must throw `ArgumentException`):**
`../../etc`, `C:\evil.iff`, `\\unc\x`, `D:foo`, `swg-clientx\loot` (prefix attack); and a legit
`creature/path.iff` must resolve under root. Maps 1:1 to the `LooseOverridePath.cs` defenses
(`:103-108` rooted, `:113-122` `..` scan, `:158-164` prefix `StartsWith`).

---

### `Utinni.Mcp.Tests/RoundTripTests.cs` (test, integration) — SC5 real-MCP-client

**Analog (discipline only):** `Utinni.Cli.Tests/Commands/SaveCommandTests.cs` — the temp-work-dir +
`IDisposable` cleanup + fixture-build + envelope-field-assert style (`SaveCommandTests.cs:37-68`).
The **transport is net-new**: a scripted in-process `McpClient` over `StdioClientTransport` launching
the built `Utinni.Mcp.exe` (Research §Validation 2, RESEARCH.md:511).

**Patterns to copy from the analog:**
- temp-dir fixture root + `IDisposable.Dispose` recursive cleanup (`SaveCommandTests.cs:41-51`).
- `IffBuilder.Form(...)` / `Int32Le` fixture construction (`SaveCommandTests.cs:56-68`) for the
  read/edit-save round-trip asset — **but these live in `Utinni.Cli.Tests` (net472)**; the net10 test
  reuses a *fixture file copied into the temp root* (Research §Validation Wave-0: "reuse `Utinni.Cli.Tests`
  fixtures"), it cannot reference the net472 builder type.
- assert the returned envelope fields (`{written, path, bytesWritten, validated}`) — the same fields
  `SaveCommandTests` asserts on the CLI envelope.

**Net-new shape (no analog):** `McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
{ Command = "<Utinni.Mcp.exe>", Arguments = ["--root", "<fixtureRoot>"] }))` → `ListToolsAsync()` →
`CallToolAsync("read_tre", …)` + `CallToolAsync("save_…", …)` + repack-dry-run. (RESEARCH.md:511.)

---

### `MCP-SECURITY.md` (doc deliverable, threat register)

**Analog:** `.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-SECURITY.md` — the EXACT format
to mirror (Research §Security). Copy the structure section-for-section.

**Structure to replicate** (`07-SECURITY.md`):
- **YAML front-matter** (`:1-8`): `phase / slug / status / threats_open / asvs_level / created`.
- **Trust Boundaries table** (`:20-30`): `| Boundary | Description | Data Crossing |` — for Phase 14
  the boundaries are: agent→stdio→host; host→`Process.Start`→net472/x86 CLI; relative-path→`ResolvedRoot`;
  CLI stdout→mapper.
- **Threat Register table** (`:36-59`): `| Threat ID | Category | Component | Disposition | Mitigation
  (verified evidence) | Status |` with **`T-14-NN` ids + file:line evidence** (mirror the `T-07-NN`
  style at `:40-58`). Research §Security supplies the 8 threat rows (absolute-path escape, annotation-as-
  enforcement, repack corruption, arg-injection, CLI hang/DoS, stdout pollution, decompression bomb
  inherited, supply-chain NuGet).
- **Accepted Risks Log** (`:66-73`): `| Risk ID | Threat Ref | Rationale | Accepted By | Date |`.
- **Audit Trail + Sign-Off** (`:106-122`).
- **Footers** verbatim (`:61-62` disposition/status legend).

**Phase-14-specific content the register MUST encode (Research §Security):** the **5-layer model**
(annotations → elicitation → loose-override-default → verify-before-commit → backup/recovery) with the
explicit caveat that **layers 1–2 are advisory** (MCP tool hints are NOT a security boundary) and
**layers 3–5 are the deterministic enforcement**.

---

## Shared Patterns

### Path Containment (`resolvedRoot` + every tool path) — THE access-control boundary

**Source:** `UtinniCoreDotNet/Saving/LooseOverridePath.cs` (`Resolve`, `:73-167`).
**Apply to:** `ResolvedRoot.cs` (startup pin + per-call resolve), every tool in `ReadTools`/`SaveTools`/`RepackTool`.

**Cross-TFM decision (Research Pitfall 4 / A2 — planner picks ONE):**
- **(a)** extract `LooseOverridePath` into a tiny `netstandard2.0` shared lib both net472 + net10 reference
  (DRYer, single source of truth — research's `[ASSUMED]` preference).
- **(b)** re-implement the ~40-line `Resolve` in the net10 host (cleaner seam — host owns its containment)
  with a **shared golden/parity test** asserting identical behavior to the net472 original.
> Default to NOT a raw net472→net10 ProjectReference (emits NU1701, fragile — Pitfall 4 warning signs).
> Either way the SC3 path-escape test runs against whichever implementation ships.

### Subprocess Timeout Discipline (60 s backstop + stdin-close + kill)

**Source:** `Utinni.Cli/Commands/Subprocess/NativeToolRunner.cs` (`:70,135-173`).
**Apply to:** `CliDispatcher.cs` (the host backstops the CLI; the CLI already backstops its own natives —
double backstop covers the SOE-native hang inherited from Phase 13).

### Sorted-Key JSON Envelope Contract (pass-through, never reshape)

**Source:** `Utinni.Cli/Output/JsonOutput.cs` (`:50-85`) — success `{command,result,schemaVersion}` /
error `{command,error:{kind,message},schemaVersion}`.
**Apply to:** `CliResultMapper.cs` (parse-as-opaque-JSON, attach as `structuredContent` + text mirror).

### CLI Exit-Code Taxonomy (0 ok / 1 usage / 2 domain / 3 notfound)

**Source:** uniform across `NativeToolRunner.Run` (`:91-105`) + every `*Command.Run` (`SaveCommand.cs:82-115`,
`RepackTreCommand.cs:61-115`). **Apply to:** `CliResultMapper.cs` error taxonomy (in-band envelope →
`isError=true`; no-envelope/timeout → hard MCP error).

### Threat-Register Format (design-time deliverable)

**Source:** `07-SECURITY.md` (front-matter + Trust Boundaries + `T-NN-NN` register + Accepted Risks).
**Apply to:** `MCP-SECURITY.md`.

---

## No Analog Found

| File | Role | Data Flow | Reason / closest substitute |
|------|------|-----------|------------------------------|
| `Utinni.Mcp/Utinni.Mcp.csproj` | config | — | No net472 csproj is a usable template for an SDK-style net10 project with `ModelContextProtocol` 1.4.0. Use Research Installation block (RESEARCH.md:78-86) + Pattern 1 (`Program.cs` host bootstrap). |
| `Utinni.Mcp/Program.cs` (generic-host MCP bootstrap) | entry | request-response | `Utinni.Cli/Program.cs` is the *role* analog (an entry point that dispatches) but the framework differs entirely: generic-host `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` (RESEARCH.md:185-199) vs CommandLineParser `ParseArguments`. Key carry-over: **logging to stderr** (`LogToStandardErrorThreshold`, Research Pitfall 2) because stdout is the MCP transport — there is NO net472 analog for that constraint. |
| `RoundTripTests.cs` real-`McpClient` stdio handshake | test | request-response | New transport (net10 SDK `McpClient`/`StdioClientTransport`). Only the fixture/cleanup discipline is borrowed from `SaveCommandTests.cs`. |

---

## Metadata

**Analog search scope:** `Utinni.Cli/Commands/`, `Utinni.Cli/Commands/Subprocess/`, `Utinni.Cli/Output/`,
`Utinni.Cli/Program.cs`, `UtinniCoreDotNet/Saving/`, `Utinni.Cli.Tests/{Commands,Subprocess,Infrastructure}/`,
`.planning/phases/07-*/07-SECURITY.md`.
**Files scanned:** 11 read in full + 2 grep sweeps (verb names, roundtrip mutation args).
**Key cross-cutting finding:** the entire Phase-14 surface is a **thin projection** of net472 analogs
one process-layer up; the ONLY net-new shapes are the MCP SDK host bootstrap, the `McpClient` round-trip
transport, and the `ResolvedRoot.PinOrThrow` startup half. The single genuine *design* fork is the
save-tool typed-edit composition (compose `roundtrip-*`+`save` — A1, planner to confirm).
**Pattern extraction date:** 2026-06-05
```