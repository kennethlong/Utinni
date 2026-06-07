# Phase 15: Wave-2 editors (WorldSnapshot, Particle) + presentation residuals - Pattern Map

**Mapped:** 2026-06-07
**Files analyzed:** 17 new/modified (2 repos: Utinni + UtinniPlugins)
**Analogs found:** 16 / 17 (1 genuinely-new native hook has no analog)

> **Scope note:** Two repos. `D:/Code/Utinni` holds the native C++ (`UtinniCore`, CppSharp-exposed),
> the managed codecs/wrappers (`UtinniCoreDotNet`), `Utinni.Cli`, `Utinni.Mcp`. `D:/Code/UtinniPlugins`
> holds The Jawa Toolbox (TJT) where the MEF `IEditorPlugin` Forms/SubPanels live. The five Wave-1
> editors (Phases 7-11) are the structural analogs. `D:/Code/swg-client-v2/.../clientParticle` is a
> **read-only SOE format spec reference**, NOT a Utinni analog — never copy its code/identifiers.

---

## File Classification

| New/Modified File | Repo | Role | Data Flow | Closest Analog | Match |
|-------------------|------|------|-----------|----------------|-------|
| `Formats/Particle/ParticleEffectDocument.cs` | Utinni | managed codec | transform (IFF tree-walk) | `Formats/ObjectTemplate/MutableObjectTemplate.cs` | role + flow (exact) |
| `Formats/Particle/MutableParticleEffect.cs` | Utinni | managed codec | transform | `Formats/ObjectTemplate/MutableObjectTemplate.cs` | exact |
| `Formats/Particle/ParticleFieldValue.cs` | Utinni | managed codec (typed union) | transform | `Formats/ObjectTemplate/ObjectTemplateParamValue.cs` | exact |
| `Formats/Particle/WaveFormCodec.cs` | Utinni | managed codec (leaf) | transform | `Formats/ObjectTemplate/ObjectTemplateParamCodec.cs` | role-match |
| `Formats/Particle/ColorRampCodec.cs` | Utinni | managed codec (leaf) | transform | `Formats/ObjectTemplate/ObjectTemplateParamCodec.cs` | role-match |
| `Formats/Particle/ParticleEmitterDescription.cs` | Utinni | managed codec | transform (15-version degrade) | `ObjectTemplateParamCodec.cs` + `MutableObjectTemplate.FromMutableIff` catch-block | exact (degrade) |
| `Formats/Particle/ParticleEffectWriter.cs` | Utinni | managed codec (writer) | transform | `Formats/ObjectTemplate/ObjectTemplateWriter.cs` | exact |
| `Utinni.Cli/Commands/DecodeParticleCommand.cs` | Utinni | CLI verb | request-response | `Utinni.Cli/Commands/DecodeIffCommand.cs` (auto-dispatch) / `RoundtripOtCommand.cs` | exact |
| `Utinni.Cli/Commands/RoundtripParticleCommand.cs` | Utinni | CLI verb | request-response | `Utinni.Cli/Commands/RoundtripOtCommand.cs` | exact |
| `Utinni.Cli/Program.cs` (MODIFY) | Utinni | CLI dispatch | request-response | self (lines 48-102) | exact |
| `Utinni.Mcp/Tools/ReadTools.cs` (MODIFY) | Utinni | MCP tool | request-response (dispatch-by-exit) | self (`DecodeIff`, lines 84-95) | exact |
| `SnapshotPanel.cs` (MODIFY: + `Placements…` button) | UtinniPlugins | SubPanel | event-driven | self | exact (grow) |
| `FormSnapshotPlacements.cs` (NEW) | UtinniPlugins | WinForms Form (grid host) | CRUD / event-driven | `FormObjectTemplateEditor.cs` + `FormDatatableEditor.cs` | exact |
| `WorldSnapshotImpl.cs` (MODIFY: bulk ops) | UtinniPlugins | `*Impl` game-thread wrapper | event-driven (marshalled) | self (`SetSelectedNodePosition`, `RemoveNode`) | exact |
| `FormParticleEditor.cs` (NEW) | UtinniPlugins | WinForms Form (editor shell) | CRUD | `FormObjectTemplateEditor.cs` | exact |
| `Plugin.cs` (MODIFY: register Particle Form) | UtinniPlugins | MEF registration seam | — | self (lines 108-119) | exact |
| RESID-04 native fix (`direct_input.cpp` + `directx9.cpp` + `PanelGame.cs`) | both | D3D9/DI presentation hook | event-driven | self (existing hooks) | exact (extend) |
| Particle **live-preview hot-retrigger** native hook | Utinni | native export (NEW) | event-driven (per save/reload) | — | **NO ANALOG** |
| `ReloadAssetClassifier.cs` (MODIFY/extend tests) | Utinni | classifier | transform | self | exact |

---

## Pattern Assignments

### `Formats/Particle/*` codec (managed codec, transform) — PROD-W2-PRT

**Analog:** `D:/Code/Utinni/UtinniCoreDotNet/Formats/ObjectTemplate/` (the whole folder is the structural model).

The `.prt` (`FORM PEFT`) codec mirrors the OT codec's **3-file split**: a per-leaf typed codec
(`ObjectTemplateParamCodec` → `WaveFormCodec`/`ColorRampCodec`/`ParticleEmitterDescription`), a typed
union value (`ObjectTemplateParamValue` → `ParticleFieldValue`), and a mutable model over a captured
`MutableIffDocument` (`MutableObjectTemplate` → `MutableParticleEffect`).

**Parse entry pattern** — `bytes → IffReader.Read → MutableIffDocument.FromDocument → Mutable*`
(copy from `RoundtripOtCommand.ParseOt`, lines 190-199):
```csharp
private static MutableObjectTemplate ParseOt(byte[] bytes)
{
    IffDocument iffDoc;
    using (var ms = new MemoryStream(bytes, writable: false))
    {
        iffDoc = IffReader.Read(ms);            // tree-walk reader (Don't Hand-Roll)
    }
    MutableIffDocument mutableIff = MutableIffDocument.FromDocument(iffDoc, bytes);
    return MutableObjectTemplate.FromMutableIff(mutableIff);
}
```
Public IFF primitive signatures the codec composes (verified):
- `IffReader.Read(Stream input)` / `IffReader.Read(string path)` → `IffDocument` (`Formats/Iff/IffReader.cs:92,104`)
- `IffWriter.Write(MutableIffDocument doc, Stream output)` (`Formats/Iff/IffWriter.cs:86`)

**Degrade-don't-abort pattern (D-05)** — the load-bearing pattern. Copy the
**consume-exactly-or-hex** posture from `ObjectTemplateParamCodec.cs:84-120` AND the
**catch-and-raw-preserve at the loop level** from `MutableObjectTemplate.FromMutableIff` lines 194-214:
```csharp
// Source: MutableObjectTemplate.FromMutableIff (Formats/ObjectTemplate/MutableObjectTemplate.cs:194-216)
try
{
    entry = ObjectTemplateParamCodec.Decode(payload);
}
catch (DecoderException)
{
    // ... capture the chunk verbatim as a raw hex-fallback param. The template still opens,
    // and round-trip stays byte-exact: Serialize re-emits the full captured IFF tree ...
    entry = new ObjectTemplateParamEntry(rawName,
        ObjectTemplateParamValue.FromRawBytes(payload, ObjectTemplateDataTypeTag.None));
}
```
For `.prt` the **unit of raw-preservation is the unrecognized `FORM <version>` sub-form** (preserve
the whole sub-tree bytes), not just a leaf. `EMTR` has 15 versions — type the corpus-present ones, a
`default:` branch raw-preserves the rest. **NEVER replicate the SOE `FATAL` (Pitfall 2).**

**Little-endian payload scalars (Pitfall 6)** — use `IffPayloadCursor` for every payload read; tags +
chunk lengths are big-endian but scalars are little-endian. Methods available (verified
`Formats/Decoders/IffPayloadCursor.cs`): `ReadInt8`, `ReadInt32Le`, `ReadUInt32Le`, `ReadFloatLe`,
`ReadCString(Encoding)`, `ReadBytes(int)`, `.Remaining`. It is `internal` to `UtinniCoreDotNet` and
truncation-safe (throws `DecoderException(Truncated)` rather than OOB).

**Mutable in-place edit + machine-managed count (re-encode ONLY the touched leaf)** — copy
`MutableObjectTemplate.EditOverride` (lines 226-241) and `RewriteCount` (lines 291-302): edits mutate
the captured leaf in place via `leaf.SetPayload(...)`; structural mutations re-derive the count leaf
little-endian. Untouched bytes re-emit verbatim through `IffWriter`. This is the byte-exact
round-trip mechanism the `.prt` writer inherits.

**Header doc-comment convention (MANDATORY):** every `Formats/*` file opens with the MIT header +
the **"Format understood by reading swg-client-v2 ... no code or identifiers copied ... original to
Utinni under MIT"** provenance comment (see `IffPayloadCursor.cs:24-28`,
`ObjectTemplateParamCodec.cs:24-29`). The `.prt` files MUST carry the same provenance language
(`clientParticle` is SOE/Bootprint, All Rights Reserved).

---

### `Utinni.Cli/Commands/RoundtripParticleCommand.cs` (CLI verb, request-response)

**Analog:** `D:/Code/Utinni/Utinni.Cli/Commands/RoundtripOtCommand.cs` (read in full).

Copy verbatim: the `[Verb(...)]` + `[Value]/[Option]` options class shape (lines 38-55), the
`JsonOutput.EmitError/EmitSuccess` envelope, and the **exit-code taxonomy** (lines 70-72, 174-186):
```
0 success; 1 UsageError; 2 IffParseException/DecoderException/IOException; 3 FileNotFound.
Generic System.Exception is intentionally NOT caught.
```
`DecodeParticleCommand` follows the same options/JSON-envelope shape; or — preferred per the MCP
note below — extend the existing `decode-iff` auto-dispatch (it already dispatches typed reads by
root FORM; `PEFT` is a new branch) rather than adding a standalone decode verb.

**CLI registration (MODIFY `Program.cs`)** — the 16-verb cap is already solved (lines 43-47). Add the
option type to the `Type[]` (lines 48-69) and a `case` to `Dispatch` (lines 80-101):
```csharp
// Program.cs:84 pattern
case Commands.DecodeIffOptions o:    return Commands.DecodeIffCommand.Run(o);
// add: case Commands.RoundtripParticleOptions o: return Commands.RoundtripParticleCommand.Run(o);
```

---

### `Utinni.Mcp/Tools/ReadTools.cs` (MCP tool, request-response) — D-08

**Analog:** self — the `DecodeIff` tool (lines 84-95).

The MCP server has **ZERO format logic** (D-06, Phase-14 rule). A `.prt` read/summarize tool is a
thin attribute-decorated wrapper that resolves the path under the pinned root, dispatches the CLI
verb by name, and passes the envelope through. Copy `DecodeIff` exactly:
```csharp
// Source: Utinni.Mcp/Tools/ReadTools.cs:84-95
[McpServerTool(Name = "decode_iff", ReadOnly = true, Idempotent = true)]
public static async Task<CallToolResult> DecodeIff(ResolvedRoot root, CliDispatcher cli,
    [Description(PathParamDescription)] string relativePath)
{
    string abs = root.Resolve(relativePath);                 // throws on escape → SDK tool error
    CliInvocationResult r = await cli.RunAsync("decode-iff", new[] { abs }).ConfigureAwait(false);
    return CliResultMapper.ToCallToolResult(r);              // verbatim envelope pass-through
}
```
Note the class doc-comment already says `decode_iff` is the SINGLE typed-read tool because the CLI
auto-dispatches by root FORM — **prefer adding `PEFT` to the CLI's `decode-iff` dispatch over a new
MCP tool**, unless a dedicated `summarize_particle` read is wanted for D-08 AI read-assist.

---

### `FormSnapshotPlacements.cs` (NEW WinForms Form, grid host) — PROD-W2-WS / D-01

**Analog:** `FormObjectTemplateEditor.cs` (shell) + `FormDatatableEditor.cs` (grid). Read
`FormObjectTemplateEditor.cs` header + class decl (lines 24-130).

Clone the **resizable `UtinniForm : IEditorForm` shell** with a `ThemedDataGridView` Fill region.
Per UI-SPEC, this is a companion window launched from the 417px SubPanel (`SubPanel.cs:36
const int width = 417` — too narrow for a real table).

**Singleton hide-not-dispose (MANDATORY for MEF-registered forms)** — the `FormClosing` handler
delegates to the shipped framework predicate (do NOT re-implement):
```csharp
// Source: UtinniCoreDotNet/UI/SingletonFormClosePolicy.cs:58-61
public static bool ShouldHideInsteadOfDispose(CloseReason reason)
{
    return reason == CloseReason.UserClosing;   // user close → Hide(); host-shutdown → dispose
}
// FormObjectTemplateEditor_FormClosing is the thin WinForms adapter that calls this (see its
// class doc-comment, FormObjectTemplateEditor.cs:70-73). Apply from commit 1.
```

**Grid + dock-fill order (CF-09):** the Fill `ThemedDataGridView` is added FIRST (front-most) per
`feedback_winforms_dockfill_zorder`. Read-only grid; `MultiSelect=true`, `FullRowSelect`. Edits flow
through bulk-op modals and the gizmo, never inline (keeps undo wired through `WorldSnapshotCommands`).

**Selection-sync feedback-loop guard (Pattern 2 — load-bearing)** — detach/reattach the change
handler around programmatic selection, exactly as the shipped panel does:
```csharp
// Source: SnapshotPanel.cs:246-259 (UpdateSelectedNodeControlsPosition)
nudNodePosX.ValueChanged -= nudNodePos_ValueChanged;   // detach
nudNodePosX.Value = (decimal)position.X;               // set programmatically
nudNodePosX.ValueChanged += nudNodePos_ValueChanged;   // reattach
```
The same `-=`/set/`+=` idiom wraps the table's `SelectionChanged` when a single row drives the gizmo.

**Reading the node list for the table** — use the native CppSharp-exposed reader (zero new format,
D-02). Verified API (`UtinniCore/swg/scene/world_snapshot.h:73-91`):
`WorldSnapshotReaderWriter.Get()` → `getNodeCount()`, `getNodeAt(i)`, `getObjectTemplateName(idx)`,
`getNodeById(id[, parentObject])`, `getLastNode()`; `Node` exposes `Id`, `ParentId`, `Radius`,
`Transform`, `ObjectTemplateName` (via `getObjectTemplateName`). Read all on the game thread.

**MEF registration (MODIFY `Plugin.cs`)** — add to `GetForms()` inside try/catch isolation; do NOT
widen `GetSubPanels()` (stays `null`, CON-M-01/02). Copy the lines 108-119 pattern:
```csharp
// Source: Plugin.cs:112-119
try { forms.Add(new FormObjectTemplateEditor(this)); }
catch (Exception ex) { Log.Info("Failed to create FormObjectTemplateEditor; ... " + ex); }
// add: try { forms.Add(new FormParticleEditor(this)); } catch (Exception ex) { Log.Info(...); }
// FormSnapshotPlacements is launched by the existing SnapshotPanel button (planner discretion
// whether it ALSO goes in GetForms()).
```

---

### `WorldSnapshotImpl.cs` (MODIFY: bulk ops) — `*Impl` game-thread wrapper, event-driven

**Analog:** self — `SetSelectedNodePosition` (lines 279-310) and `RemoveNode` (lines 158-176).

**Game-thread marshalling (Pattern 1 — every live-client edit)** — all bulk ops enqueue ONE
`GroundSceneCallbacks.AddUpdateLoopCall` composing N existing per-node commands so the whole bulk
lands atomically on one game-frame:
```csharp
// Composition pattern, derived from RemoveNode (WorldSnapshotImpl.cs:162-174):
GroundSceneCallbacks.AddUpdateLoopCall(() =>
{
    foreach (var nodeId in selectedNodeIds)
    {
        var node = WorldSnapshotReaderWriter.Get().GetNodeById(nodeId);
        if (node == null) continue;
        editorPlugin.AddUndoCommand(this,
            new AddUndoCommandEventArgs(new RemoveWorldSnapshotNodeCommand(node)));  // compose shipped cmd
        WorldSnapshot.RemoveNode(node);
    }
});
```
**Compose the shipped `IUndoCommand` set** (`UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs`):
`AddWorldSnapshotNodeCommand`, `RemoveWorldSnapshotNodeCommand`,
`WorldSnapshotNodePositionChangedCommand`, `WorldSnapshotNodeRotationChangedCommand`. Bulk move =
N `PositionChanged`; bulk delete = N `Remove`; bulk retemplate = remove + add (or a new composite).
Each command's ctor snapshots `new WorldSnapshotReaderWriter.Node(node)` and itself marshals via
`AddUpdateLoopCall` (lines 46-52) — so the composite-vs-N-commands wiring is the planner's call
(CONTEXT discretion). **Never run snapshot edits on the WinForms thread** (allocator corruption).

---

### `FormParticleEditor.cs` (NEW WinForms Form, editor shell) — PROD-W2-PRT

**Analog:** `FormObjectTemplateEditor.cs` (direct clone — read header + class decl, lines 24-130).

Same shell as the OT editor: `UtinniForm : IEditorForm`, singleton hide-not-dispose
(`SingletonFormClosePolicy`), editor-local undo/redo (independent of the scene `UndoRedoManager` —
the OT editor's `ObjectTemplateEditController` is the model, lines 96-97), the inherited Phase-8
`Save ▾` drop-down + `OpenSource` provenance gating (lines 111-130), and the
**typed-widget + Consolas-9pt hex fallback** for greyed-out unknowns (the OT editor's
`ObjectTemplateSchema` + raw-bytes-fallback display, the visible surface of D-05).

The left emitter tree reuses **`IffChunkTree`**
(`D:/Code/UtinniPlugins/.../UI/Controls/IffChunkTree.cs`); the right param grid reuses
**`ThemedDataGridView`** (`.../UI/Controls/ThemedDataGridView.cs`) with the Phase-9 token map. The
in-app **AI `Explain effect` button reuses the SAME CLI/MCP `.prt` read path** (D-08) — no
independent AI/format path.

**Background-task open/parse/save** — `FormObjectTemplateEditor` opens on a `Task` (it imports
`System.Threading.Tasks`, line 38); UI mutations marshal via `Control.Invoke`. Mirror that for `.prt`
open/parse/save/AI-read so the form stays responsive (UI-SPEC: no modal spinners).

---

### RESID-04 — window-resize / windowed↔fullscreen (D3D9/DI presentation hook, event-driven)

**Analogs (all self — extend existing hooks, do NOT re-hook):**

1. **`UtinniCore/swg/misc/direct_input.cpp`** — the `SetCooperativeLevel` vtable shim ALREADY
   intercepts + logs `DISCL_*` flags + caller PC (the prime-suspect intercept point):
   ```cpp
   // direct_input.cpp:92-105 (hkSetCooperativeLevel) — already patched at vtbl[13] (line 116-125)
   HRESULT __stdcall hkSetCooperativeLevel(IDirectInputDevice8A* pThis, HWND hwnd, DWORD dwFlags)
   {
       // logs "DI::SetCooperativeLevel: device=... hwnd=... flags=%s caller=0x%p" then:
       return origSetCooperativeLevel(pThis, hwnd, dwFlags);   // ← suppress/redirect HERE (D-12)
   }
   ```
   First live run: read the DISCL log to confirm the exclusive-fullscreen trigger + caller context
   (Open Question 3 / assumption A4), then suppress (force `DISCL_NONEXCLUSIVE`) or redirect.

2. **`UtinniCore/swg/graphics/directx9.cpp`** — `hkPresent` (line 265) is the windowed COPY-stretch
   present path; `hkReset` (line 365) passes through SWG's own Reset. The in-source comment at
   lines 273-275 already records the no-Reset rationale (`Reset` returns `D3DERR_INVALIDCALL` →
   DEVICELOST → crash). `pp.Windowed = TRUE` is set at line 488. **HARD CONSTRAINT (D-13): Utinni
   must NEVER call `pDevice->Reset` itself** (`feedback_d3d9_reset_third_party`). The optional
   regression gate (RESEARCH Validation map) greps that Utinni never invokes `Reset`.

3. **`UtinniCoreDotNet/UI/Controls/PanelGame.cs`** — the owned-popup reparent (`WS_POPUP` +
   `GWLP_HWNDPARENT`, lines 192-210) + `SetWindowPos` reposition (line 252). Resize is window-side
   `SetWindowPos`, never a device Reset. Keep RT-space mouse mapping correct across resize
   (`feedback_imgui_embedded_d3d9_rt_space`). **Anti-pattern: `WS_CHILD` reparenting breaks
   DirectInput** (it needs a top-level HWND) — keep the owned-popup model.

**UI surface (UI-SPEC):** only a non-modal detach NOTICE banner (`Dock=Top`, `Colors.Secondary()`
accent) — never a modal (it must not steal focus from the recovering client).

---

### RESID-03 — reload candor (classifier, transform)

**Analog:** self — `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` (read in full).

The 4-tier classifier already routes `.iff`(SHOT/STOT/SBOT) + `.stf` to
`ReloadTier.PendingNextSceneChange` (tier-b) with a CONSERVATIVE unknown fallback (lines 107-139).
RESID-03 is **live-observe + honest badge copy** — extend the routing test map for the new
WS/Particle reload paths (RESEARCH Wave-0 gap), set the LOCKED candor copy from UI-SPEC's Reload
Candor Contract, and **never loosen badge copy to over-promise**. The `.ws` (WorldSnapshot) path
inherits the `PendingNextSceneChange` candor: "Placements re-resolve on the next scene change."
The Particle live-capable badge ("Re-triggers live instances on Preview.") is honest ONLY when the
D-09 hook is reachable AND `Game.IsRunning`; otherwise it degrades to the tier-b copy.

---

## Shared Patterns

### Game-thread marshalling (Pattern 1)
**Source:** `WorldSnapshotImpl.cs` — every method wraps `GroundSceneCallbacks.AddUpdateLoopCall(() => {...})`.
**Apply to:** ALL WorldSnapshot table/bulk ops; the Particle live-preview hot-retrigger.
**Heap-free hot path:** if the D-09 preview runs on a per-frame callback, keep it allocation-free
(`project_rh_snapshot_no_heap_alloc` — a per-frame `vector::reserve()` crashed scene change at
`0x0051fb0a`). Marshal once per save/reload, not per frame.

### Event-handler detach/reattach (Pattern 2)
**Source:** `SnapshotPanel.cs:246-259`.
**Apply to:** the new placements-table↔gizmo selection sync, and any programmatic control-value push.

### Degrade-don't-abort raw-preserve (Pattern 3, D-05)
**Source:** `ObjectTemplateParamCodec.cs:84-120` (consume-exactly-or-hex) +
`MutableObjectTemplate.FromMutableIff:194-216` (loop-level catch → raw-preserve).
**Apply to:** the ENTIRE `.prt` codec — type the common path, raw-preserve every unrecognized
`FORM <version>` sub-tree. Never `FATAL`.

### Singleton hide-not-dispose (Pattern 4)
**Source:** `UtinniCoreDotNet/UI/SingletonFormClosePolicy.cs:58-61`; adapter idiom in
`FormObjectTemplateEditor.cs:70-73`.
**Apply to:** `FormParticleEditor` AND `FormSnapshotPlacements` (both MEF-registered singletons).

### MEF registration try/catch isolation
**Source:** `Plugin.cs:62-119` (one isolated `try { forms.Add(new Form…(this)); } catch { Log.Info }`
per Wave-1 editor). `GetSubPanels()` stays `null` (CON-M-01/02 NOT widened).
**Apply to:** registering `FormParticleEditor`.

### IFF tree-walk + little-endian payload cursor (Don't Hand-Roll)
**Source:** `IffReader.Read` / `IffWriter.Write` / `MutableIffDocument.FromDocument` /
`IffPayloadCursor`. Parse entry idiom: `RoundtripOtCommand.ParseOt:190-199`.
**Apply to:** the `.prt` codec — never hand-roll chunk-length/pad-byte/endianness handling.

### CLI JSON-envelope + exit-code taxonomy
**Source:** `RoundtripOtCommand.cs` (`JsonOutput.EmitSuccess/EmitError`, exit codes 0/1/2/3, generic
Exception NOT caught). Registration: `Program.cs` `Type[]` + `Dispatch` switch (cap already solved).
**Apply to:** the new `.prt` CLI verb(s).

### MCP thin dispatch-by-exit-code (zero format logic, D-06)
**Source:** `ReadTools.cs:84-95` (`DecodeIff`). `ResolvedRoot.Resolve` (fail-closed path containment)
→ `cli.RunAsync(verb, args)` → `CliResultMapper.ToCallToolResult`.
**Apply to:** the `.prt` MCP read tool (or extend `decode-iff` auto-dispatch).

### `Formats/*` provenance header
**Source:** `IffPayloadCursor.cs:24-28`, `ObjectTemplateParamCodec.cs:24-29`.
**Apply to:** every new `Formats/Particle/*` file (clientParticle is SOE/Bootprint, ARR — study
on-disk layout only; original implementation under MIT).

---

## No Analog Found

| File / Capability | Role | Data Flow | Reason |
|-------------------|------|-----------|--------|
| Particle **live-preview hot-retrigger** native hook (D-09) | native export (CppSharp) | event-driven (per save/reload) | No existing native export re-triggers live effect/particle-manager instances. RESEARCH A3/Open-Q2: may need a NEW `UtinniCore` export (`ParticleManager` vs `ClientEffectManager` — spike early). If it touches a CppSharp-exposed native type, `Generated/UtinniCore.cs` reorders — `git checkout --` it, never commit (`project_utinnicore_cs_regen_churn`). The marshalling/heap-free harness is borrowable from `GroundSceneCallbacks` + `project_rh_snapshot_no_heap_alloc`, but the manager hook itself is greenfield. |

---

## Metadata

**Analog search scope:**
- `D:/Code/Utinni/UtinniCoreDotNet/Formats/{ObjectTemplate,Iff,Decoders}/`, `/Commands/`, `/Saving/`, `/UI/`
- `D:/Code/Utinni/Utinni.Cli/{Program.cs,Commands/}`, `D:/Code/Utinni/Utinni.Mcp/{Tools,Server}/`
- `D:/Code/Utinni/UtinniCore/swg/{scene/world_snapshot.h, graphics/directx9.cpp, misc/direct_input.cpp}`
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/{Plugin.cs, SWG/, UI/SubPanels/, UI/Forms/, UI/Controls/}`

**Files scanned:** ~22 (12 read in full; the rest via targeted Grep for signatures/anchors).
**Pattern extraction date:** 2026-06-07
