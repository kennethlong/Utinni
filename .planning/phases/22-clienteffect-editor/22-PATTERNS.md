# Phase 22: ClientEffect Editor - Pattern Map

**Mapped:** 2026-06-17
**Files analyzed:** 13 new + 2 modified
**Analogs found:** 15 / 15 (every layer has a near-1:1 live analog)

> This phase is **reuse-by-composition**. The codec is the Particle three-layer stack cloned
> near-1:1; the UI is the Terrain `SubPanel → Form` idiom; the byte-exact engine (the only "new"
> mechanism, D-01 variable-length edits) is the *already-shipped* `MutableIffNode` /
> `IffWriter` length-ripple. There is essentially **no greenfield architecture** — every new file
> has a concrete, current analog with file:line excerpts below. The planner should copy these
> shapes verbatim and change only the format-specific payload logic.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `UtinniCoreDotNet/Formats/ClientEffect/ClientEffectDocument.cs` | model/codec entry | transform (decode) | `Formats/Particle/ParticleEffectDocument.cs` | exact |
| `UtinniCoreDotNet/Formats/ClientEffect/MutableClientEffect.cs` | model | transform | `Formats/Particle/MutableParticleEffect.cs` | exact |
| `UtinniCoreDotNet/Formats/ClientEffect/ClientEffectCommand.cs` | model (per-command view) | transform | `Formats/Particle/ParticleEmitterDescription.cs` | role-match |
| `UtinniCoreDotNet/Formats/ClientEffect/ClefFieldCodec.cs` | utility (encode/decode) | transform (variable-length) | `IffPayloadCursor.cs` (read) + `TrnFieldEncoder` LE idiom (write) | role-match (NEW: var-length) |
| `UtinniCoreDotNet/Formats/ClientEffect/ClientEffectParseException.cs` | exception type | — | `Formats/Particle/ParticleParseException.cs` | exact |
| `UtinniCoreDotNet/Formats/Decoders/ClefDecoder.cs` | service (dispatch detect) | request-response | `Formats/Decoders/TgenDecoder.cs` (`LooksLikeTerrain`) | exact |
| `Utinni.Cli/Commands/DecodeEffectCommand.cs` | controller (CLI verb) | request-response | `Utinni.Cli/Commands/DecodeTrnCommand.cs` (alias-delegation) | exact |
| `Utinni.Cli/Commands/RoundtripEffectCommand.cs` | controller (CLI verb) | request-response | `Utinni.Cli/Commands/RoundtripParticleCommand.cs` | exact |
| `Utinni.Cli/Commands/ApplySaveEffectCommand.cs` | controller (CLI verb) | request-response (save) | `Utinni.Cli/Commands/ApplySaveTrnCommand.cs` | role-match (NEW: var-length verify) |
| `Utinni.Cli/Program.cs` (MODIFIED) | router | request-response | self (existing `Type[]` + `Dispatch` switch) | exact |
| `Utinni.Cli/Commands/DecodeIffCommand.cs` (MODIFIED) | router (CLEF branch) | request-response | self (PEFT / TGEN branches) | exact |
| `Utinni.Mcp/Tools/ReadTools.cs` (MODIFIED) | tool (MCP read) | request-response | self (`SummarizeParticle` / `SummarizeTerrain`) | exact |
| `TJT/UI/SubPanels/EffectsSubPanel.cs` | provider (docked entry) | event-driven (launcher) | `TJT/UI/SubPanels/TerrainSubPanel.cs` | exact |
| `TJT/UI/Forms/FormClientEffectEditor.cs` | component (roomy host) | event-driven (editor) | `FormTerrainEditor.cs` + `FormParticleEditor.cs` (preview seam) | exact |
| `TJT/Saving/ClientEffectSaveTargets.cs` | service (save) | file-I/O | `TJT/Saving/IffSaveTargets.cs` + `TerrainSaveTargets.cs` | exact |
| `TJT/Saving/TerrainSaveTargets.cs` (MODIFIED, folded todo) | service (save) | file-I/O | self (already has `looseOverrideSubDir`, 21-06 R2) | verify-only |

---

## Pattern Assignments

### `Formats/ClientEffect/ClientEffectDocument.cs` (model/codec entry, transform)

**Analog:** `UtinniCoreDotNet/Formats/Particle/ParticleEffectDocument.cs` (exact)

Clone the two static entry methods verbatim, change the type names. `FromBytes` is the codec
front door; `FromIff` is what the `decode-iff` CLEF branch calls (it already has the parsed
`IffDocument`). Source `ParticleEffectDocument.cs:49-70`:

```csharp
public static MutableParticleEffect FromBytes(byte[] bytes)
{
    if (bytes == null) throw new System.ArgumentNullException("bytes");
    IffDocument iffDoc;
    using (var ms = new MemoryStream(bytes, writable: false))
        iffDoc = IffReader.Read(ms); // tree-walk reader
    MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, bytes);
    return MutableParticleEffect.FromMutableIff(mutableIff);
}
public static MutableParticleEffect FromIff(IffDocument iffDoc, byte[] sourceBytes) { /* same, skips re-read */ }
```

The `Serialize()` side is the `ParticleEffectWriter` shape — there is **no bespoke serializer**;
it is `IffWriter.Write(model.SourceIff)` (source `ParticleEffectWriter.cs:45-54`). The planner may
fold this into `MutableClientEffect.Serialize()` rather than a separate writer class.

---

### `Formats/ClientEffect/MutableClientEffect.cs` (model, transform)

**Analog:** `UtinniCoreDotNet/Formats/Particle/MutableParticleEffect.cs` (exact)

This is the heart of the raw-fallback discipline (D-13). Copy the class skeleton:

**Forged-count cap + raw-preserve flag + known-version set** (`MutableParticleEffect.cs:78,104,115-119`):
```csharp
internal const int MaxNodeCount = 16 * 1024 * 1024;          // reject forged counts before alloc (T-15-02)
public bool IsRawPreserved { get; }                          // true when version unrecognized → whole sub-tree verbatim
private static readonly HashSet<string> KnownEffectVersions = new HashSet<string>(StringComparer.Ordinal)
    { "0000", "0001", "0002" };                              // CLEF: { "0001", "0002", "0003" }
```

**The FromMutableIff dispatch** (`:128-136`) — throw only on wrong root / forged count, never on an
unknown version:
```csharp
public static MutableParticleEffect FromMutableIff(MutableIffDocument mutable)
{
    MutableIffNode root = mutable.Root;
    if (root == null || root.Kind != MutableIffNodeKind.Container || root.SubTypeId != "PEFT")
        throw new ParticleParseException(ParticleParseError.UnexpectedForm, "...not a FORM PEFT.");
    MutableIffNode versionForm = FirstContainerChild(root);
    // if versionForm tag NOT in KnownEffectVersions → set rawPreserved=true, return (whole sub-tree re-emits verbatim)
    // else walk the flat command list...
}
```

**CLEF delta vs PEFT:** PEFT walks a recursive group→emitter tree with count chunks. CLEF is
**flat** — a single `while (more children)` loop over command chunks (no count chunk). For each
command leaf: known tag (CPAP/PSND/CLGT/CAMS/FFBK) → typed `ClientEffectCommand` view; unknown tag
→ raw command view (leaf payload never touched → re-emits its captured slice for free). This mirrors
the SOE `while(!atEndOfForm)` switch (`ClientEffectTemplate.cpp:275`) — see RESEARCH § CLEF Codec Spec.

`SourceIff` holds the `MutableIffDocument` for byte-exact re-emit; `Serialize()` returns
`IffWriter.Write(SourceIff)`.

---

### `Formats/ClientEffect/ClefFieldCodec.cs` (utility, transform — the ONE genuinely new asset)

**Analog (read side):** `Formats/Decoders/IffPayloadCursor.cs` (exact — every primitive exists)
**Analog (write side):** the LE-primitive idiom from `ApplySaveTrnCommand` / `TrnFieldEncoder`

**Read** — `IffPayloadCursor` already exposes every CLEF primitive. Construct it over a command
leaf's `GetPayloadCopy()` and read in the exact SOE field order (string THEN scalars for CPAP/FFBK):

```csharp
// IffPayloadCursor.cs:62-153 — ReadInt32Le / ReadInt8 / ReadFloatLe / ReadCString(Encoding) all present.
public float ReadFloatLe() { /* host-independent: copies 4 LE bytes, reverses on BE host */ }
public string ReadCString(Encoding encoding)
{
    int start = _pos;
    while (_pos < _data.Length && _data[_pos] != 0) _pos++;
    if (_pos >= _data.Length) throw new DecoderException(DecoderError.Truncated, "Unterminated string...");
    string s = encoding.GetString(_data, start, _pos - start);
    _pos++; // consume the NUL terminator  ← on-disk byte count = strlen+1 (Pitfall 2)
    return s;
}
```

**Write** — this is the new bit. `TrnFieldEncoder` is **fixed-span only and explicitly rejects
length changes** (see ApplySaveTrn excerpt below), so it CANNOT be reused. Build a fresh command-chunk
payload via a `MemoryStream`, reusing only the host-independent LE primitive idiom
(`BitConverter.GetBytes` + reverse-on-BE-host) and the NUL-terminated C-string (RESEARCH § Code Examples):

```csharp
static byte[] FloatLe(float f){ var b=BitConverter.GetBytes(f); if(!BitConverter.IsLittleEndian) Array.Reverse(b); return b; }
using (var ms = new MemoryStream()) {
    byte[] s = Encoding.ASCII.GetBytes(name); ms.Write(s,0,s.Length); ms.WriteByte(0x00);  // C-string, no length prefix
    ms.Write(FloatLe(time),0,4);
    // version-conditional CPAP fields: v0002 +bool8, v0003 +4 floats (preserve SOURCE version, D-03)
    cpapLeaf.SetPayload(ms.ToArray());   // length change ripples FORM lengths automatically (verified below)
}
```

---

### `Formats/ClientEffect/ClientEffectParseException.cs` (exception)

**Analog:** `Formats/Particle/ParticleParseException.cs` (exact) — clone enum + ctor; add the
`catch (ClientEffectParseException ex)` rung wherever `ParticleParseException` is caught (decode-iff,
roundtrip, apply-save).

---

### `Formats/Decoders/ClefDecoder.cs` (service, detect-dispatch)

**Analog:** `Formats/Decoders/TgenDecoder.cs` `LooksLikeTerrain` (exact)

CLEF detection is simpler than terrain (no PTAT wrapper) — a single sub-type compare. Source
`TgenDecoder.cs:84-92`:
```csharp
public static bool LooksLikeTerrain(IffChunk root)
{
    var container = root as IffContainerChunk;
    if (container == null) return false;
    if (container.SubTypeId == RootTgen) return true;
    if (container.SubTypeId == RootPtat) return FindContainerChild(container, RootTgen) != null;
    return false;
}
```
For CLEF: `return (root as IffContainerChunk)?.SubTypeId == "CLEF";` — the planner can inline this in
the `decode-iff` branch (cf. the PEFT branch, which inlines the `SubTypeId == "PEFT"` check) and skip
a dedicated decoder class, OR keep `ClefDecoder.LooksLikeClientEffect` for symmetry with `TgenDecoder`.

---

### `Utinni.Cli/Commands/DecodeEffectCommand.cs` (CLI verb, request-response)

**Analog:** `Utinni.Cli/Commands/DecodeTrnCommand.cs` (exact — the alias-delegation precedent)

Clone verbatim; delegate to a `DecodeIffCommand.BuildClefResult` (the CLEF branch is required
regardless of whether this alias exists — D-11). Source `DecodeTrnCommand.cs:53-83`:
```csharp
public static int Run(DecodeTrnOptions o)
{
    if (!File.Exists(o.Path))
        return JsonOutput.EmitError("decode-trn", "FileNotFound", "...", exitCode: 3);
    try {
        byte[] bytes = File.ReadAllBytes(o.Path);
        TerrainDocument doc = TerrainDocument.FromBytes(bytes);
        return JsonOutput.EmitSuccess("decode-trn", DecodeIffCommand.BuildTerrainResult(doc, o.Path));
    }
    catch (TerrainParseException ex) { return JsonOutput.EmitError("decode-trn", ex.Kind.ToString(), ex.Message, exitCode: 2); }
    catch (DecoderException ex)      { ... exitCode: 2 }
    catch (IffParseException ex)     { ... exitCode: 2 }
    catch (IOException ex)           { ... "IoError", exitCode: 2 }
    // NOTE: Generic Exception intentionally NOT caught.
}
```
**Exit-code contract (LOCKED, shared by every verb): 0 success; 1 UsageError; 2 parse/decode/IO;
3 FileNotFound. Generic `Exception` is intentionally NOT caught.**

---

### `Utinni.Cli/Commands/RoundtripEffectCommand.cs` (CLI verb — the byte-exact gate)

**Analog:** `Utinni.Cli/Commands/RoundtripParticleCommand.cs` (exact)

This is the strongest byte-exact gate (D-01/D-13). Clone the load→serialize→re-parse→`SequenceEqual`
shape. Source `RoundtripParticleCommand.cs:71-95`:
```csharp
byte[] loadedBytes = File.ReadAllBytes(o.Path);
MutableParticleEffect model = ParticleEffectDocument.FromBytes(loadedBytes);
byte[] roundtrippedBytes = model.Serialize();
MutableParticleEffect rtModel = ParticleEffectDocument.FromBytes(roundtrippedBytes); // re-parse for structural validity
bool bytesEqual = loadedBytes.Length == roundtrippedBytes.Length && loadedBytes.SequenceEqual(roundtrippedBytes);
var result = new JObject {
    ["bytesIdentical"] = bytesEqual, ["comparisonGranularity"] = "whole-file",
    ["rawPreserved"] = rtModel.IsRawPreserved, ["rootType"] = "PEFT",      // CLEF: "CLEF"
    ["source"] = o.Path, ["version"] = rtModel.Version
};
return JsonOutput.EmitSuccess("roundtrip-particle", result);
```
The full catch ladder (`:97-113`): `ParticleParseException` (→ `ClientEffectParseException`),
`DecoderException`, `IffParseException`, `IOException` — all exit 2.

---

### `Utinni.Cli/Commands/ApplySaveEffectCommand.cs` (CLI verb, save — variable-length DELTA)

**Analog:** `Utinni.Cli/Commands/ApplySaveTrnCommand.cs` (role-match — copy the scaffold, REPLACE the verify)

Copy the load → `--root` containment → decode → locate-leaf → reject-non-editable → mutate → re-parse →
atomic-write scaffold. The **ONE delta** the planner must spell out: the fixed-length guard at
`ApplySaveTrnCommand.cs:178-183` must be **removed/inverted** for CLEF (length change is the point, D-01).

**Containment + FileNotFound** (`ApplySaveTrnCommand.cs:99-113`):
```csharp
try { destPath = LooseOverridePath.Resolve(o.Root, o.RelAsset); }
catch (ArgumentException ex) { return JsonOutput.EmitError("apply-save-trn", "PathContainment", ex.Message, exitCode: 2); }
if (!File.Exists(destPath)) return JsonOutput.EmitError("apply-save-trn", "FileNotFound", "...", exitCode: 3);
```

**Reject a half-understood (raw-fallback) node before any write** (`:149-154`) — adapt to CLEF:
reject an edit addressed at a raw/unknown-command leaf:
```csharp
if (!isActiveEdit && !TargetTypedNodeIsEditable(terrain, o.LeafId))
    return JsonOutput.EmitError("apply-save-trn", "UsageError",
        "--leaf addresses a non-editable (raw-fallback / truncated / DEAD) node; refusing to rewrite a half-understood payload (#4).", exitCode: 1);
```

**The fixed-length guard to REMOVE for CLEF** (`:178-185`):
```csharp
if (newPayload.Length != originalPayload.Length)   // ← TrnFieldEncoder is fixed-span; CLEF must NOT keep this guard
    return JsonOutput.EmitError("apply-save-trn", "VerifyFailed",
        "fixed-length edit produced a different-length payload; nothing written.", exitCode: 2);
leaf.SetPayload(newPayload);
byte[] mutatedBytes = IffWriter.Write(mutable);
```

**Replacement verify (RESEARCH § The Length-Ripple Mechanism, "the single design delta"):** the
`apply-save-trn` `OnlyTargetSpanDiffers` byte-span verify CANNOT be reused (it asserts a fixed span).
The CLEF verify is **structural**: (i) output re-parses, (ii) all UNTOUCHED command chunks are
byte-identical, (iii) the edited command decodes back to the requested value. The flag shape for
add/remove/reorder (D-02) is the planner's discretion — see RESEARCH Open Question 2 (e.g.
`--add-command CPAP`, `--remove-leaf <stableId>`, `--reorder <stableId> up|down`), or scope CLI to
field edits + roundtrip and let the in-app editor own list authoring. The DOM supports all of it.

---

### `Utinni.Cli/Program.cs` (MODIFIED — router)

**Analog:** self (exact — the `Type[]` ParseArguments + `object` MapResult → `Dispatch` switch)

D-12 is already solved (CLI at 26 verbs today). Add each new `effect-*` options type to BOTH lists.
Source `Program.cs:48-77` (the `Type[]` overload) and `:83-111` (the switch):
```csharp
return parser.ParseArguments(args,
        typeof(Commands.ParseTreOptions), ... typeof(Commands.ValidateBundleOptions),
        /* + typeof(Commands.DecodeEffectOptions), RoundtripEffectOptions, ApplySaveEffectOptions */)
    .MapResult((object opts) => Dispatch(opts), errs => 1);   // exit 1 on usage error
// ...
switch (opts) {
    case Commands.DecodeTrnOptions o: return Commands.DecodeTrnCommand.Run(o);
    // + case Commands.DecodeEffectOptions o: return Commands.DecodeEffectCommand.Run(o);  (and the other two)
}
```

---

### `Utinni.Cli/Commands/DecodeIffCommand.cs` (MODIFIED — CLEF branch)

**Analog:** self (exact — the PEFT and TGEN branches at `:88-107`)

Add a CLEF branch right after the PEFT branch and a `catch (ClientEffectParseException)` rung. Source
`DecodeIffCommand.cs:92-97`:
```csharp
if ((doc.Root as IffContainerChunk)?.SubTypeId == "PEFT")
    return JsonOutput.EmitSuccess("decode-iff",
        BuildParticleResult(ParticleEffectDocument.FromIff(doc, bytes), o.Path));
// + CLEF branch (the exact template):
//   if ((doc.Root as IffContainerChunk)?.SubTypeId == "CLEF")
//       return JsonOutput.EmitSuccess("decode-iff", BuildClefResult(ClientEffectDocument.FromIff(doc, bytes), o.Path));
```
Add `catch (ClientEffectParseException ex)` to the catch ladder at `:117-136`. This branch is what
gives the MCP `decode_iff` tool CLEF routing for free.

---

### `Utinni.Mcp/Tools/ReadTools.cs` (MODIFIED — MCP read tool)

**Analog:** self (exact — `SummarizeParticle` / `SummarizeTerrain` at `:97-127`)

ZERO format logic (MCP-OOP lock, DEC-V2-MCP-OOP). Copy `SummarizeTerrain` verbatim, change
Name/Description, dispatch `decode-iff` (the CLEF root auto-routes). Source `ReadTools.cs:119-127`:
```csharp
[McpServerTool(Name = "summarize_terrain", ReadOnly = true, Idempotent = true)]
[Description("...ZERO format logic here — the named pipe / subprocess boundary to the x86 utinni-cli IS the architecture boundary (MCP-OOP, DEC-V2-MCP-OOP).")]
public static async Task<CallToolResult> SummarizeTerrain(ResolvedRoot root, CliDispatcher cli,
    [Description(PathParamDescription)] string relativePath)
{
    string abs = root.Resolve(relativePath);                 // throws on escape → SDK tool error
    CliInvocationResult r = await cli.RunAsync("decode-iff", new[] { abs }).ConfigureAwait(false);
    return CliResultMapper.ToCallToolResult(r);              // verbatim envelope pass-through
}
```
New tool: `summarize_clienteffect`.

---

### `TJT/UI/SubPanels/EffectsSubPanel.cs` (provider — thin docked launcher, DEC-C4)

**Analog:** `TJT/UI/SubPanels/TerrainSubPanel.cs` (exact — D-04 idiom)

Clone the whole file. The four load-bearing patterns:

**1. Ctor signature + MEF-safe try/catch (Pitfall 5/8)** (`TerrainSubPanel.cs:101-121`):
```csharp
public TerrainSubPanel(IEditorPlugin editorPlugin, HotkeyManager hotkeyManager, UtINI ini) : base("Terrain")
{
    InitializeComponent();
    this.editorPlugin = editorPlugin;
    try { BuildContent(); contentReady = true; }
    catch (Exception ex) { SurfaceBuildFailure(ex); }   // a throwing ctor cascades the WHOLE IEditorPlugin out of MEF compose
}
```
The ctor signature `(IEditorPlugin, HotkeyManager, UtINI)` is mandatory — it slots into the existing
`SubPanelContainer("Controls", …)` array in `Plugin.cs`.

**2. Pitfall-8 Dock order (Bottom/Top strips FIRST, claim edges)** (`:128-214`): add `Dock.Bottom`
candor footer and `Dock.Top` banner before the free-positioned body controls; the 417px width pin is
why the real editor lives in a Form, not the panel.

**3. Singleton hide-not-dispose host launch** (`:243-259`):
```csharp
private FormTerrainEditor LaunchHost()
{
    if (!contentReady) return null;
    if (terrainForm == null || terrainForm.IsDisposed) terrainForm = new FormTerrainEditor(editorPlugin);
    if (terrainForm.Visible) terrainForm.Activate(); else terrainForm.Show();
    return terrainForm;
}
```

**4. TRE-Browser hand-off entry + direct-open entry** (`:296-327`): `OpenFromTre(payload, archivePath,
logicalPath)` and `OpenLooseOverride(loosePath)` — both launch the singleton and forward to the host,
wrapped in try/catch (never tear down the panel). The undo seam (`editorPlugin?.ClearUndoStack?.Invoke()`)
is NULL until FormMain wires it — null-check every call.

---

### `TJT/UI/Forms/FormClientEffectEditor.cs` (component — roomy host editor)

**Analog (host/save/open):** `FormTerrainEditor.cs` ; **Analog (preview seam):** `FormParticleEditor.cs`

**D-06 interior (planner's discretion):** a flat command-list control + a per-command typed field
editor; unknown tags/versions degrade to raw/hex, never a hard failure. Honor Pitfall 4 (Dock.Fill
front-most / nested SplitContainer, size before SplitterDistance).

**The Preview seam — copy the HONEST-CANDOR discipline, NOT a working retrigger** (RESEARCH Pitfall 3:
Phase 15's spike found NO reachable native hot-retrigger hook). Source `FormParticleEditor.cs:710-746`:
```csharp
private static bool IsRetriggerHookReachable()
{
    // 15-03: native ParticlePreview.isRetriggerAvailable() returns false this phase. Single seam.
    return false;
}
private void OnPreviewClicked(object sender, EventArgs e)
{
    if (effect == null) return;
    bool clientUp = false; try { clientUp = Game.IsRunning; } catch { clientUp = false; }   // P/Invoke ALWAYS try/catch
    if (!(clientUp && IsRetriggerHookReachable())) {
        // Honest degraded path — keep no-client distinct from no-hook; dimmed styling; NO retrigger.
        lblStatus.Text = clientUp ? PreviewNoHookTooltip : PreviewUnavailableTooltip;
        lblStatus.ForeColor = Colors.FontDisabled();
        return;
    }
    // Live-capable path (reachable only once a native hook lands): marshal on the game thread, heap-free.
}
```
The button is ENABLED whenever a doc is open (a disabled control never shows a tooltip), and branches
on reachability at click. Use distinct no-client vs no-hook copy. CLEF `.iff` classifies to
`PendingNextSceneChange` in `ClientReloadDispatcher` (no live binding), so the honest tier is correct.

---

### `TJT/Saving/ClientEffectSaveTargets.cs` (service, file-I/O)

**Analog:** `TJT/Saving/IffSaveTargets.cs` (exact) + `TerrainSaveTargets.SaveLooseOverride` (the
`looseOverrideSubDir` convention)

Off-UI-thread `async Task<SaveResult>` save. Compose the override base = `resolvedRoot /
looseOverrideSubDir (default "loose") / <logical>`, all legs through `LooseOverridePath.Resolve`
(fail-closed `..`/rooted/prefix-match rejection), atomic write with `Flush(true)` (the MEDIUM-9
stale-bytes reload-race barrier). Source `TerrainSaveTargets.cs:136-154`:
```csharp
overrideBase = string.IsNullOrEmpty(looseOverrideSubDir)
    ? resolvedRoot
    : LooseOverridePath.Resolve(resolvedRoot, looseOverrideSubDir);   // <root>/loose
fullPath = LooseOverridePath.Resolve(overrideBase, relAssetPath);     // <root>/loose/<logical>
```
`looseOverrideSubDir` defaults to `"loose"` (the `IffSaveTargets` convention, D-10).

---

### `TJT/Saving/TerrainSaveTargets.cs` (MODIFIED — folded todo `phase21-terrain-override-loose-subdir`)

**Analog:** self (verify-only — the fix mostly shipped in 21-06 R2)

`SaveLooseOverride` ALREADY takes a `looseOverrideSubDir` param defaulting to `"loose"`
(`TerrainSaveTargets.cs:283-297`, the excerpt above). The todo file is **STALE** (still
`status: OPEN`). Residual work for the planner (RESEARCH § Runtime State Inventory + Open Question 1):
1. **Verify** a test asserts the resolved terrain destination is under `<root>\loose\` —
   `UtinniCoreDotNet.Tests/SavingTests/TerrainLooseOverridePathTests.cs` EXISTS; confirm it asserts the
   `\loose\` segment (close-item #3), add if absent.
2. **Confirm the CLI half**: `ApplySaveTrnCommand.cs:102` resolves `LooseOverridePath.Resolve(o.Root,
   o.RelAsset)` with **no subdir** (assumption A2). Confirm the caller pre-composes `loose/` into
   `--root`, else align the CLI subdir convention with the editor.
3. **Close the stale todo** (pending → done).

---

## Shared Patterns

### Byte-exact length-ripple (D-01 / D-02 — the "only new bit", ALREADY SOLVED)
**Source:** `UtinniCoreDotNet/Formats/Iff/MutableIffNode.cs` + `IffWriter.cs`
**Apply to:** the codec, `apply-save-effect`, and every roundtrip/edit test.

The DOM re-stamps ancestor FORM lengths automatically — there is **no stored length to keep in
sync**. Every mutation calls `MarkDirtyAndInvalidateAncestors` (`MutableIffNode.cs:484-495`):
```csharp
private void MarkDirtyAndInvalidateAncestors()
{
    IsDirty = true; capturedSlice = null;
    var p = Parent;
    while (p != null) { p.IsDirty = true; p.capturedSlice = null; p = p.Parent; }
}
```
`SetPayload` (any length, `:279-286`), `AddLeaf` (`:306-314`), `Remove` (`:332-342`), `ReorderUp/Down`
(`:381-404`) all call it. On write, `IffWriter.WriteContainerFresh` recomputes the length from actual
child bytes under `checked` (`IffWriter.cs:144-187`):
```csharp
long childTotal = childBuf.Length;
long innerLen; checked { innerLen = 4L + childTotal; }     // 4 = sub-type FourCC; recomputed from real child bytes
// overflow/64MB guard, then WriteBe32(output, (uint)innerLen)
```
A clean (unedited) leaf re-emits its captured verbatim slice → untouched commands are byte-identical;
a raw-fallback unknown command is just a never-edited leaf → re-emits verbatim for free.
**The planner's job is to EXERCISE this with goldens, not build it.**

### LE-scalar / NUL-C-string primitives (endianness split, Pitfall 6 + 2)
**Source:** `Formats/Decoders/IffPayloadCursor.cs:62-153` (read) ; `BitConverter.GetBytes + reverse-on-BE`
idiom (write).
**Apply to:** `ClefFieldCodec` only. IFF tags+lengths are big-endian (`IffReader.ReadInt32Be`); chunk
PAYLOAD scalars are little-endian. Strings are NUL-terminated (strlen+1 on disk), NEVER length-prefixed.

### Fail-closed loose-override path containment (V12)
**Source:** `UtinniCoreDotNet.PathContainment/LooseOverridePath.cs` (`Resolve(resolvedRoot,
relAssetPath)`, `:69+`) — rejects null/empty, rooted paths, any `..` segment, and prefix-match escapes;
final `StartsWith(root + separator)` ordinal-ignore-case on Windows.
**Apply to:** `apply-save-effect` (`--root` containment) and `ClientEffectSaveTargets`.

### Game-thread / honest-candor reload tier (D-07 / D-08)
**Source:** `TJT/Saving/ClientReloadDispatcher.cs` — gates on `Game.IsRunning` FIRST (wrapped in
try/catch, P/Invoke can throw outside an injected client, `:87-93`); dispatches via
`GameCallbacks.AddMainLoopCall`; `.iff`/CLEF → `ReloadTier.PendingNextSceneChange` (no live binding).
**Apply to:** the `FormClientEffectEditor` Preview action. Never fabricate a scene-change trigger.

### CLI verb exit-code contract + catch ladder (LOCKED)
**Source:** every `*Command.cs`. **0** success; **1** UsageError; **2** parse/decode/IO; **3**
FileNotFound. Catch `<Format>ParseException` / `DecoderException` / `IffParseException` / `IOException`;
**generic `Exception` is intentionally NOT caught**.
**Apply to:** all three `effect-*` verbs.

---

## No Analog Found

None. Every new file has a current, near-1:1 analog in the live repo. The single design *delta* (not
a missing analog) is the variable-length `apply-save-effect` verify, which adapts — rather than copies
— `ApplySaveTrnCommand`'s fixed-span verify (documented in that file's assignment above).

---

## Metadata

**Analog search scope:**
- `D:/Code/Utinni/UtinniCoreDotNet/Formats/{Particle,Decoders,Iff}/`
- `D:/Code/Utinni/UtinniCoreDotNet.PathContainment/`
- `D:/Code/Utinni/Utinni.Cli/Commands/` + `Program.cs`
- `D:/Code/Utinni/Utinni.Mcp/Tools/`
- `D:/Code/Utinni/UtinniCoreDotNet/PluginFramework/`
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/{UI/SubPanels,UI/Forms,Saving}/`

**Files scanned:** ~30 (15 analogs read in full or in the load-bearing range).
**Reference (read-only, NOT analogs — port understanding only):**
`D:/Code/swg-client-v2/.../clientEffect/{ClientEffectTemplate.cpp,.h,ClientEffectTemplateRW.cpp}` —
the format spec, already line-cited in 22-RESEARCH.md § CLEF Codec Spec.
**Pattern extraction date:** 2026-06-17
