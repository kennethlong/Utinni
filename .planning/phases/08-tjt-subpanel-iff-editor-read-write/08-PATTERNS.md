# Phase 8: TJT subpanel — IFF Editor (read + write) - Pattern Map

**Mapped:** 2026-05-27
**Files analyzed:** 14 new + 6 modified
**Analogs found:** 20 / 20 (every new/modified file has a strong in-repo analog — there is essentially no novel surface)

> **Cross-repo phase.** Framework primitives (writer, mutable DOM, `.tre` repack, CLI verb, tests)
> live in `D:/Code/Utinni`. Editor/UI code lives in the sibling `D:/Code/UtinniPlugins`
> (`The Jawa Toolbox/TheJawaToolboxDotNet/`). All analog file paths below are absolute and labeled
> with their repo.
>
> **Key insight (from RESEARCH "Don't Hand-Roll"):** every "new" file is the *write mirror* or a
> *thin extension* of an already-tested read primitive. Treat each as "invert/extend the cited
> analog," not greenfield.

---

## File Classification

| New/Modified File | Repo | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|------|-----------|----------------|---------------|
| `UtinniCoreDotNet/Formats/Iff/IffWriter.cs` | Utinni | model/serializer | transform (graph→bytes) | `UtinniCoreDotNet/Formats/Iff/IffReader.cs` (inverse) | exact (write-mirror) |
| `UtinniCoreDotNet/Formats/Iff/MutableIffDocument.cs` | Utinni | model | transform | `UtinniCoreDotNet/Formats/Iff/IffDocument.cs` | exact (mutable sibling) |
| `UtinniCoreDotNet/Formats/Iff/MutableIffNode.cs` | Utinni | model | transform | `IffChunk.cs` / `IffContainerChunk.cs` / `IffLeafChunk.cs` | exact (mutable sibling) |
| `UtinniCoreDotNet/Formats/Tre/TreWriter.cs` | Utinni | model/serializer | file-I/O + transform | `UtinniCoreDotNet/Formats/Tre/TreFile.cs` (inverse) | exact (write-mirror) |
| `Utinni.Cli/Commands/RoundtripIffCommand.cs` | Utinni | CLI command | request-response (verb→JSON) | `Utinni.Cli/Commands/InspectIffCommand.cs` | exact |
| `Utinni.Cli/Program.cs` (MODIFY) | Utinni | CLI dispatch | request-response | self (existing `MapResult` block) | exact |
| `Utinni.Cli.Tests/Commands/RoundtripIffCommandTests.cs` | Utinni | test | golden harness | `Utinni.Cli.Tests/Commands/InspectIffCommandTests.cs` | exact |
| `Utinni.Cli.Tests/Fixtures/iff/roundtrip/*.iff` + `*.expected.json` | Utinni | test fixture | golden data | `Utinni.Cli.Tests/Fixtures/iff/odd-chunk-no-pad.*` | exact |
| `UtinniCoreDotNet.Tests/FormatsTests/Iff/IffWriterTests.cs` | Utinni | test | unit | `UtinniCoreDotNet.Tests/FormatsTests/Iff/IffReaderTests.cs` | exact |
| `UtinniCoreDotNet.Tests/FormatsTests/Iff/IffWriterFixtures.cs` | Utinni | test fixture | unit data | `UtinniCoreDotNet.Tests/FormatsTests/Iff/IffReaderFixtures.cs` | exact |
| `UtinniCoreDotNet.Tests/FormatsTests/Tre/TreWriterTests.cs` | Utinni | test | unit/harness | `UtinniCoreDotNet.Tests/FormatsTests/Tre/TreFileTests.cs` + `Utinni.Cli.Tests/Infrastructure/TreFixtureBuilder.cs` | role-match |
| `.../TheJawaToolboxDotNet/UI/Controls/IffChunkTree.cs` (shared control) | UtinniPlugins | component (UserControl) | event-driven | `.../UI/Controls/TreDetailPane.cs` (extract `tvChunks`+`LoadIff`) | exact |
| `.../TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs` (MODIFY) | UtinniPlugins | component | event-driven | self (consume the extracted control) | exact |
| `.../TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` (+ `.Designer.cs`) | UtinniPlugins | component (Form) | event-driven | `.../UI/Forms/FormTreBrowser.cs` (+`.Designer.cs`) | exact |
| `.../TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (MODIFY) | UtinniPlugins | component | event-driven | self (add "Open in IFF Editor" hand-off) | exact |
| `.../TheJawaToolboxDotNet/UI/Forms/FormFourCcDialog.cs` (FourCC entry modal) | UtinniPlugins | component (dialog) | request-response | `UtinniCoreDotNet/UI/Forms/FormHotkeyEditorDialog.cs` | role-match |
| `.../TheJawaToolboxDotNet/UI/Forms/FormSaveConfirmDialog.cs` (risk confirms) | UtinniPlugins | component (dialog) | request-response | `UtinniCoreDotNet/UI/Forms/FormHotkeyEditorDialog.cs` | role-match |
| `.../TheJawaToolboxDotNet/Plugin.cs` (MODIFY) | UtinniPlugins | registration | wiring | self (`forms.Add(...)` try/catch block) | exact |
| in-memory live-patch helper (in `FormIffEditor` or a save-mode helper) | UtinniPlugins | service (game-thread) | event-driven → native | `UtinniCoreDotNet/Callbacks/GameCallbacks.cs` + `Memory.memory.Copy` binding | role-match |
| forced-reload helper (in `FormIffEditor` or save-mode helper) | UtinniPlugins | service (game-thread) | event-driven → native | `GameCallbacks.AddMainLoopCall` + `Graphics.ReloadTextures`/`GroundScene.ReloadTerrain` bindings | role-match |

> **Plan-split note (D-05 PLAN-SPLIT FLAG):** the in-memory live patch and the `.tre` repack each
> carry distinct failure modes — RESEARCH recommends 8a (writer + mutable DOM + CLI + loose-override +
> Save/Save-As + shared control + FormIffEditor), 8b (in-memory live patch), 8c (`.tre` repack). The
> per-file analogs below are split-agnostic; the planner assigns them to plans.

---

## Pattern Assignments

### `UtinniCoreDotNet/Formats/Iff/IffWriter.cs` (NEW — model/serializer, transform)

**Analog:** `D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/IffReader.cs` (the exact inverse).

**License header + provenance banner** — copy verbatim (lines 1–27 of every `Formats/Iff/*.cs`):
the MIT block, then the `// Format understood by reading swg-client-v2/...` provenance comment.
All new `Formats/Iff` files MUST carry this banner.

**Big-endian framing primitive** — invert `IffReader.ReadInt32Be` (IffReader.cs lines 343–354):
```csharp
// Reader (existing): assemble MSB-first
return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
// Writer (new): emit MSB-first (mirror; RESEARCH § Code Examples WriteBe32)
private static void WriteBe32(Stream s, uint v)
{
    s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16));
    s.WriteByte((byte)(v >> 8));  s.WriteByte((byte)v);
}
```

**No-pad invariant (CRITICAL — Pitfall 1):** the reader DETECTS the pad rather than assumes it
(IffReader.cs lines 307–327, the 07-04a reversal). The writer must emit **no** pad byte for
odd-length chunks. The `IffBuilder.Leaf` test helper documents the convention: *"tag(4) +
big-endian length(4) + raw payload (no trailing pad)"* (IffBuilder.cs lines 88–99).

**Container framing** — mirror `IffReader.ReadContainerChunk` (IffReader.cs lines 213–280): a
container payload is `BE_u32(subType4) · child0 · child1 · …`; the declared length is
`4 (subType) + Σ child serialized sizes`. Serialize children first, then prepend the header (D-07
bottom-up roll-up; Pitfall 2).

**Container TypeID set** — reuse the reader's exact set (IffReader.cs lines 78–83):
`{ "FORM", "LIST", "CAT " }` (trailing space on `CAT ` is load-bearing).

**Output size cap (Security V5):** reuse the reader's `MaxChunkSize = 64 * 1024 * 1024`
(IffReader.cs line 71) — do not serialize a chunk payload above the cap.

---

### `UtinniCoreDotNet/Formats/Iff/MutableIffDocument.cs` + `MutableIffNode.cs` (NEW — model, transform)

**Analog:** `IffDocument.cs` (sealed/immutable read result) + `IffChunk.cs` / `IffContainerChunk.cs`
/ `IffLeafChunk.cs` (the read node hierarchy the mutable model mirrors).

**Read model is immutable by design — build a SIBLING, never mutate it** (Anti-pattern, RESEARCH):
- `IffDocument` is `sealed` with read-only `Root` / `AllNodesInPreorder` (IffDocument.cs lines 43–60).
- `IffLeafChunk.Data` is copy-on-construction (IffLeafChunk.cs lines 50–63) — **property is `Data`,
  NOT `Payload`** (RESEARCH structure note corrects CONTEXT).
- `IffChunk` exposes `TypeId`, `LengthBytes`, `Id`, `OffsetBytes` (IffChunk.cs lines 44–81);
  `IffContainerChunk` adds `SubTypeId` + `Children` (IffContainerChunk.cs lines 42–61).

**Hybrid-DOM (D-07) construction** — `OffsetBytes` (IffChunk.cs lines 65–71) gives each node's
TypeID byte position, enabling a verbatim source-slice capture. Sketch (RESEARCH § Code Examples):
```csharp
public static MutableIffDocument FromDocument(IffDocument doc, byte[] sourceBytes)
{
    // each node's verbatim slice = sourceBytes[node.OffsetBytes .. OffsetBytes + 8 + node.LengthBytes]
    // untouched nodes re-emit that slice; dirty nodes rebuild. Propagate a dirty bit upward on edit.
}
```
A leaf stores either its captured raw slice (incl. header — simplest verbatim re-emit) OR
payload+dirty-flag (A5 — either valid). A container is "untouched" iff no descendant changed.

**Stable-id format to mirror** when adding/reordering nodes (IffChunk.cs lines 58–63;
`IffReaderTests.Read_HappyPath_AssignsStableIdsToEveryNode` lines 382–417): `FORM:WSNP/0`,
nested `FORM:WSNP/0/FORM:OBJS/3`, leaf `FORM:WSNP/0/DATA:DATA/0`. Structural ops must re-derive ids.

---

### `UtinniCoreDotNet/Formats/Tre/TreWriter.cs` (NEW — model/serializer, file-I/O + transform) — PLAN 8c

**Analog:** `D:/Code/Utinni/UtinniCoreDotNet/Formats/Tre/TreFile.cs` (reverse of `Parse` + `Inflate`).

**Repack = full rebuild, NOT in-place (Pitfall 5 / Anti-pattern).** Invert `TreFile.Parse`
(TreFile.cs lines 127–346): 36-byte header (`EERT` + version tag + 7 u32 fields, lines 139–187),
then payload blobs, then TOC block (24- or 32-byte stride per `TreVersions.RecordStride`,
lines 271–297), then the name block.

**zlib framing — invert `TreFile.Inflate`** (TreFile.cs lines 486–562): for write, prepend the
RFC1950 2-byte header (`0x78 0x9c`, %31==0) + DeflateStream-compressed body + a 4-byte Adler32
trailer (BCL `DeflateStream` neither emits nor validates Adler — compute with a tiny loop, per
RESEARCH "Don't Hand-Roll"). Read side strips `[2 .. len-4]` (lines 505–506) — mirror exactly.

**CRC avoidance (A1 / Open Q1):** Phase 8 edits payloads, not paths — **preserve each entry's
stored `TreRecord.Checksum`** (read at TreFile.cs lines 271–297, `Checksum` field). The SWG
TreeFile path-CRC algorithm is UNVERIFIED; do not recompute it. Untouched entries: re-read via
`TreFile.GetRecordData(i)` (lines 363–425) and re-emit byte-for-byte.

**Inflate/expansion cap (Security V5):** keep `MaxBlockSize = 256 * 1024 * 1024` (TreFile.cs line 62)
on any decompress during repack.

**Verification:** repack a synthetic TRE fixture and byte-compare untouched entries against the
original (the `fc /b` discipline; use `Utinni.Cli.Tests/Infrastructure/TreFixtureBuilder.cs` to
synthesize). Files opened from the read-only packed `.tre` (TRE Browser) can only repack (mode 4) /
loose-override (mode 1) / Save-As (mode 2) — never in-place (CONTEXT D-05).

---

### `Utinni.Cli/Commands/RoundtripIffCommand.cs` (NEW — CLI command, request-response)

**Analog:** `D:/Code/Utinni/Utinni.Cli/Commands/InspectIffCommand.cs`.

**Verb + options shape** (InspectIffCommand.cs lines 35–40):
```csharp
[Verb("roundtrip-iff", HelpText = "Parse → serialize → re-parse an IFF; assert byte-exact untouched chunks.")]
public class RoundtripIffOptions
{
    [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .iff file.")]
    public string Path { get; set; }
}
```

**Run skeleton + exit-code contract** (InspectIffCommand.cs lines 56–80) — copy this structure
exactly (FileNotFound→3, IffParseException→2, IOException→2; generic NOT caught):
```csharp
public static int Run(RoundtripIffOptions o)
{
    if (!File.Exists(o.Path))
        return JsonOutput.EmitError("roundtrip-iff", "FileNotFound", "IFF file not found: " + o.Path, exitCode: 3);
    try
    {
        byte[] original = File.ReadAllBytes(o.Path);
        var doc = IffReader.Read(new MemoryStream(original));
        var mutable = MutableIffDocument.FromDocument(doc, original);
        byte[] rewritten = IffWriter.Write(mutable);           // no-mutation case → byte-exact
        return JsonOutput.EmitSuccess("roundtrip-iff", BuildResult(original, rewritten, o.Path));
    }
    catch (IffParseException ex) { return JsonOutput.EmitError("roundtrip-iff", ex.Kind.ToString(), ex.Message, exitCode: 2); }
    catch (IOException ex)       { return JsonOutput.EmitError("roundtrip-iff", "IoError", ex.Message, exitCode: 2); }
}
```

**JSON envelope** — emit via `JsonOutput.EmitSuccess/EmitError` (JsonOutput.cs lines 50–85); the
result object NEVER pre-adds `schemaVersion`/`command` (those go at root — InspectIffCommand.cs
lines 82–99 + the HIGH-6 guard). Result fields sketch (RESEARCH): `{ byteExact, originalLength,
rewrittenLength, source }` — keys are auto-sorted by `JsonOutput.SortJObjectKeys`.

---

### `Utinni.Cli/Program.cs` (MODIFY — CLI dispatch)

**Analog:** self. Add the new verb to the existing `ParseArguments<...>` type list and `MapResult`
chain (Program.cs lines 43–55) — one type + one lambda, mirroring the `InspectIffOptions` lines:
```csharp
Commands.InspectIffOptions,
Commands.RoundtripIffOptions,   // ADD
...
(Commands.InspectIffOptions o)   => Commands.InspectIffCommand.Run(o),
(Commands.RoundtripIffOptions o) => Commands.RoundtripIffCommand.Run(o),   // ADD
```

---

### `Utinni.Cli.Tests/Commands/RoundtripIffCommandTests.cs` (+ fixtures) (NEW — golden harness)

**Analog:** `Utinni.Cli.Tests/Commands/InspectIffCommandTests.cs` + `Infrastructure/GoldenTestRunner.cs`
+ `Infrastructure/IffBuilder.cs`; fixtures mirror `Fixtures/iff/odd-chunk-no-pad.{iff,expected.json}`.

**Theory + golden compare** (InspectIffCommandTests.cs lines 48–73): run in-process via
`InProcessCliRunner.Run("roundtrip-iff", fixturePath)`, `MaskPath` the absolute path, assert exit
code, then `GoldenTestRunner.Matches("iff/roundtrip/" + name, masked)` (GoldenTestRunner.cs lines
40–52 — `JToken.DeepEquals` with mismatch dump).

**Mandatory fixtures (Wave-0 gaps + Pitfall 1):**
- an **odd-length-chunk** fixture round-tripped to byte-identity (model on `odd-chunk-no-pad.iff`) —
  guards the no-pad regression; warning sign is "output 1 byte longer per odd chunk."
- a no-mutation `byteExact: true` golden for a nested fixture (synthesize via `IffBuilder.Form`/`.Leaf`,
  IffBuilder.cs lines 89–131 — note `Container` adds a parent-counted pad for *reader* fixtures; the
  roundtrip golden must encode SWG no-pad input).

---

### `UtinniCoreDotNet.Tests/FormatsTests/Iff/IffWriterTests.cs` + `IffWriterFixtures.cs` (NEW — unit)

**Analog:** `UtinniCoreDotNet.Tests/FormatsTests/Iff/IffReaderTests.cs` + `IffReaderFixtures.cs`.

**Test naming convention** (IffReaderTests.cs line 38): `[Method]_[Scenario]_[ExpectedOutcome]`.
**Fixture builder pattern** — mirror `IffReaderFixtures` `WriteInt32Be`/`WriteFourCc` BE helpers
(IffReaderFixtures.cs lines 45–57) and `BuildHappyPath` (lines 73–130) for round-trip inputs.

**Coverage to assert (Wave-0 gaps):**
- write→re-parse round-trip of `BuildHappyPath` returns an equal tree (reuse `IffReaderTests`
  assertions lines 51–105 against the re-parsed doc).
- edited leaf re-serializes + parent length rolls up (Pitfall 2 — re-parse must not throw
  `NestedChunkOverflow`/`Truncated`).
- structural ops (add/remove/rename/reorder/duplicate) survive write→re-parse.
- odd-length chunk emits **no** pad on round-trip (Pitfall 1).

---

### `.../TheJawaToolboxDotNet/UI/Controls/IffChunkTree.cs` (NEW shared UserControl) + `TreDetailPane.cs` (MODIFY)

**Analog:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs`
(extract `tvChunks` + `LoadIff` + `BuildChunkNode`; keep the read API intact — UI-SPEC § Host Placement).

**The exact surface to extract** — `LoadIff` (TreDetailPane.cs lines 188–198) + `BuildChunkNode`
(lines 491–507) + the themed `tvChunks` setup (lines 670–675):
```csharp
public void LoadIff(IffDocument doc) {
    tvChunks.BeginUpdate(); tvChunks.Nodes.Clear();
    if (doc?.Root != null) { tvChunks.Nodes.Add(BuildChunkNode(doc.Root)); tvChunks.Nodes[0].Expand(); }
    tvChunks.EndUpdate();
}
// node label format (KEEP for parity — UI-SPEC Layout): "TAG [SubType]  ·  N bytes  ·  @offset"
// themed: BackColor=Colors.PrimaryHighlight(); ForeColor=Colors.Font(); BorderStyle=None;
//         HideSelection=false; ShowLines=true;
```
The editable control binds to `MutableIffDocument` (not `IffDocument`) and adds the structural-op
context menu (D-03). **Extraction must NOT change `TreDetailPane`'s public read API** consumed by
Phase 7 (UI-SPEC). After extraction, rebuild TJT in the same commit (binary-compat caution).

**Editable hex / inline text (D-04)** — the read-only `txtHex` TextBox (TreDetailPane.cs lines
91, 711–719: `Multiline`, `ReadOnly=true`, `Consolas 9pt`, `ScrollBars.Both`, `WordWrap=false`,
themed) becomes `ReadOnly=false` for a selected leaf. The `HexDump` formatter (lines 514–544) is
the display format; an editable hex view needs the inverse parse-hex-back-to-bytes (new). UI-SPEC
Assumption 2 keeps the TextBox — do not hand-roll a hex grid for V1.

**Context menu** — reuse the `UtinniContextMenuStrip` + `ToolStripMenuItem` pattern
(TreDetailPane.cs `metaMenu` lines 72, 625–631) for the leaf (Replace/Export bytes) and tree
(Add/Remove/Rename/Reorder/Duplicate/Edit-sub-type) menus.

**Four-state degradation** — keep `ShowEmpty`/`ShowReadable`/`ShowParseFailure`/state-panel pattern
(TreDetailPane.cs lines 113–254, 473–489) so one bad file never crashes the editor (Security DoS).

---

### `.../TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` (+ `.Designer.cs`) (NEW — editable Form)

**Analog:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs`
(+ `.Designer.cs`). It is a `UtinniForm` implementing `IEditorForm` (UI-SPEC § Host Placement — a
NEW resizable Form via `GetForms()`, NOT a SubPanel).

**Class shape + ctor + icon + ini** (FormTreBrowser.cs lines 42–129):
```csharp
public partial class FormIffEditor : UtinniForm, IEditorForm
{
    private readonly IEditorPlugin editorPlugin;
    private readonly UtINI ini;
    public FormIffEditor(IEditorPlugin editorPlugin) {
        InitializeComponent();
        // load TJT.ico from the plugin dir, guarded (lines 64–71)
        this.editorPlugin = editorPlugin; ini = editorPlugin.GetConfig();
        CreateSettings(); ini.Load();
        Width = ini.GetInt("IffEditor","width"); Height = ini.GetInt("IffEditor","height");
        // splitter restore in try/catch (lines 84–96) — a stale ini value must NOT bubble from the ctor
    }
}
```

**`[IffEditor]` ini section** — mirror `CreateSettings` (FormTreBrowser.cs lines 179–187:
`width`/`height`/`splitterDistance` + a `looseOverrideDir` fallback key, the analog of `clientDir`)
and the best-effort `FormClosing` persist (lines 685–698).

**`IEditorForm.Create` "already open → Activate" guard** (FormTreBrowser.cs lines 194–209) — copy
verbatim; UI-SPEC V1 is single-document (open-replaces with a dirty-prompt).

**Loose-override dir derivation (Pitfall 7 / save mode 1)** — reuse `ResolveClientTreDir`
(FormTreBrowser.cs lines 312–379: process-module-dir primary → `GetWorkingDirectory()` →
`[…]Dir` ini fallback). The loose-override sub-dir is the planner's open item (Open Q2).

**Off-UI-thread I/O + marshaling** (FormTreBrowser.cs lines 225–265, 486–503): file
open/read/save and `.tre` repack run on `Task.Run`; UI mutation marshals via the captured
`SynchronizationContext` (await continuation) or `BeginInvoke`/`IsHandleCreated` guard.

**WinForms layout gotchas (Pitfall 6)** — mirror the `TreDetailPane`/`FormTreBrowser`
SplitContainer discipline: set `Size` BEFORE `SplitterDistance` (TreDetailPane.cs lines 733–756),
keep `Dock.Fill` controls added FIRST (added-first comments lines 105, 703, 727, 764). UI-SPEC
layout: Dock.Top toolbar (28px) → vertical `SplitContainer` (tree 360px | leaf editor) → Dock.Bottom
status strip.

**Title dirty-marker** — UtinniForm draws the title in `OnPaint`; call `Invalidate()` after a Text
change (FormTreBrowser.cs `SetTitle` lines 298–303) to surface the `●` unsaved marker.

**Toolbar / Undo-Redo** — UI-SPEC mandates editor-local undo/redo (D-08); do NOT reuse
`UndoRedoTitlebarButton` or route through `IEditorPlugin.AddUndoCommand` (Anti-pattern) — those feed
the scene `UndoRedoManager`.

---

### `.../TheJawaToolboxDotNet/UI/Forms/FormFourCcDialog.cs` + `FormSaveConfirmDialog.cs` (NEW — small modals)

**Analog:** `D:/Code/Utinni/UtinniCoreDotNet/UI/Forms/FormHotkeyEditorDialog.cs` (+ `.Designer.cs`).

**Small `UtinniForm` modal pattern** (FormHotkeyEditorDialog.cs lines 30–54): a partial `UtinniForm`,
`InitializeComponent()`, a public result field, a `txtInput` + OK/Cancel. For FourCC: `MaxLength = 4`,
explicit-verb buttons (UI-SPEC Copywriting — never bare OK/Cancel for risk confirms), `Color.Red`
emphasis text inside the live-patch/repack confirms (UI-SPEC § Destructive).

---

### `.../TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (MODIFY — "Open in IFF Editor" hand-off)

**Analog:** self. Add a right-click `UtinniContextMenuStrip` entry on a selected TRE leaf (the
metadata-menu pattern, FormTreBrowser.cs has the selection plumbing at `tvTre_AfterSelect`
lines 452–503 and the resolved `TreMetadata`/payload at `DispatchDetail` lines 505–536). The
hand-off passes the resolved payload bytes to `FormIffEditor` (D-09). Keep the browser read-only.

---

### `.../TheJawaToolboxDotNet/Plugin.cs` (MODIFY — registration)

**Analog:** self. Register `FormIffEditor` in the `forms` list inside the SAME try/catch isolation
block the TRE Browser uses (Plugin.cs lines 62–69) — a throwing ctor must NOT fail the plugin's MEF
load and remove TJT from the menu (Pitfall 6):
```csharp
try { forms.Add(new FormIffEditor(this)); }
catch (Exception ex) { Log.Info("Failed to create FormIffEditor; IFF Editor will be unavailable: " + ex); }
```
`GetSubPanels()` stays `null`; the SPI is NOT widened (UI-SPEC MEF conformance / STAB-04).

---

### In-memory live-patch helper (NEW — game-thread service) — PLAN 8b

**Analog:** `D:/Code/Utinni/UtinniCoreDotNet/Callbacks/GameCallbacks.cs` (`AddMainLoopCall`,
lines 254–257) + the `Memory.memory.Copy` binding (`Generated/UtinniCore.cs` lines 660–662,
702–705: `?copy@memory@@YAXIII@Z` → `Copy(uint pDest, uint pSource, uint length)`).

**Mandatory pattern (Pattern 3 / Pitfall 3 / CON-N-04):** marshal onto the game thread, then write
via `memory::copy` (the VirtualProtect bracket is inside the native fn — do NOT hand-roll a write
that skips it):
```csharp
GameCallbacks.AddMainLoopCall(() => {
    Memory.memory.Copy(targetAddr, srcBytesAddr, length);   // CON-N-04 bracketed
});
```
Gate behind a `Game.IsRunning` check (FormTreBrowser.cs uses `Game.IsRunning` at line 393) + the
UI-SPEC confirm dialog. Volatile by design (lost on reload). NEVER write mapped memory from the UI
thread.

### Forced-reload helper (NEW — game-thread service) — PLAN 8b/8c

**Analog:** `GameCallbacks.AddMainLoopCall` + the reload bindings in `Generated/UtinniCore.cs`:
`Graphics.ReloadTextures()` (lines 11775, 12060–12062), `GroundScene.ReloadTerrain()`
(lines 14343, 14428–14430), `Graphics.flushResources(bool)` (line 11769).

**Tiered reload (Pitfall 4 — no general IFF reload hook exists):** on the game thread, dispatch by
asset class — texture/shader → `ReloadTextures`; terrain → `ReloadTerrain`; otherwise a
scene-change-style re-setup (the user's established TJT repro path) or the candid UI-SPEC fallback
copy *"reload happens on the next scene change."* Post-scene-change "naked" is the documented
baseline, NOT a reload failure. Live-SWG (Tier-4) validation only.

---

## Shared Patterns

### License header + provenance banner (ALL new framework files)
**Source:** every `UtinniCoreDotNet/Formats/**/*.cs` (e.g. IffReader.cs lines 1–27).
**Apply to:** all new `Formats/Iff` + `Formats/Tre` files.
The MIT block (lines 1–23) + the `// Format understood by reading swg-client-v2/...` provenance
comment (lines 24–27). MIT block (lines 1–23) also heads every CLI/test/UI file in both repos.

### Big-endian I/O (IFF) vs little-endian (TRE)
**Source:** `IffReader.ReadInt32Be` (IffReader.cs 343–354) is BE; `TreFile.Parse` uses
`BinaryReader.ReadInt32()` LE with an explicit little-endian-host guard (TreFile.cs 130–133).
**Apply to:** `IffWriter` emits BE; `TreWriter` emits LE header/TOC fields. Do not cross them.

### JSON envelope (CLI verbs)
**Source:** `Utinni.Cli/Output/JsonOutput.cs` (`EmitSuccess`/`EmitError`, lines 50–85; sorted keys
+ LF normalization, lines 87–141).
**Apply to:** `RoundtripIffCommand`. schemaVersion + command at ROOT; result object never pre-adds
them (HIGH-6); exit codes FileNotFound→3, parse/IO→2, usage→1.

### Golden-test harness
**Source:** `GoldenTestRunner.Matches` (lines 40–52) + `InProcessCliRunner` + `MaskPath`
(InspectIffCommandTests.cs lines 48–73) + `FixturePath.Resolve`.
**Apply to:** `RoundtripIffCommandTests` and any new CLI golden.

### Game-thread marshaling + VirtualProtect bracket (live-client touch)
**Source:** `GameCallbacks.AddMainLoopCall` (GameCallbacks.cs 254–257); `Memory.memory.Copy`
(`Generated/UtinniCore.cs` 660–662). `Game.IsRunning` gate (FormTreBrowser.cs 393).
**Apply to:** in-memory live patch (mode 3) + forced reload (D-06). The ONLY sanctioned mapped-memory
write path (CON-N-04).

### WinForms themed-control reuse + layout discipline
**Source:** `Colors.*()` accessors only (TreDetailPane.cs / FormTreBrowser.cs throughout — no raw
`Color.FromArgb`); `UtinniContextMenuStrip`/`UtinniLabel`/`UtinniButton`; SplitContainer Size-before-
SplitterDistance (TreDetailPane.cs 733–756); Dock.Fill added FIRST; ctor try/catch isolation
(Plugin.cs 62–69, FormTreBrowser.cs 84–96).
**Apply to:** `IffChunkTree`, `FormIffEditor`, both dialogs.

### Plugin/MEF SPI preservation (CON-M-01/02, STAB-04)
**Source:** `IEditorPlugin` (`InheritedExport`, IEditorPlugin.cs 41–51); `IEditorForm`
(IEditorForm.cs 31–35); `Plugin.cs` `GetForms()`/`GetSubPanels()` (lines 99–109).
**Apply to:** register `FormIffEditor` via `GetForms()`; do NOT widen the interface; adding NEW
types to `UtinniCoreDotNet` is binary-safe, but rebuild TJT in the same commit after any
`Formats/Iff` public-surface addition.

---

## No Analog Found

None. Every new/modified file has a strong in-repo analog (the phase is deliberately the
write-mirror of an already-shipped read foundation). The only genuinely *new behaviors* —
hex-string-to-bytes parsing for the editable hex view, the editor-local undo/redo stack, and the
tiered forced-reload dispatch — are small additions layered onto the analogs above, not whole files
without precedent. The planner should lean on RESEARCH § Code Examples for those slivers and the
cited analogs for everything structural.

---

## Metadata

**Analog search scope:**
- `D:/Code/Utinni/UtinniCoreDotNet/Formats/{Iff,Tre}/`
- `D:/Code/Utinni/Utinni.Cli/Commands/` + `Utinni.Cli/Output/` + `Utinni.Cli/Program.cs`
- `D:/Code/Utinni/Utinni.Cli.Tests/{Commands,Infrastructure,Fixtures/iff}/`
- `D:/Code/Utinni/UtinniCoreDotNet.Tests/FormatsTests/{Iff,Tre}/`
- `D:/Code/Utinni/UtinniCoreDotNet/{PluginFramework,Callbacks,UI/Forms,UI/Controls,Generated}/`
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/{UI/Controls,UI/Forms,Plugin.cs}`

**Files read for extraction:** 19 (IffReader, IffDocument, IffChunk, IffContainerChunk, IffLeafChunk,
TreFile, InspectIffCommand, DecodeIffCommand, JsonOutput, Program, InspectIffCommandTests, IffBuilder,
GoldenTestRunner, IffReaderTests, IffReaderFixtures, TreDetailPane, FormTreBrowser, Plugin,
FormHotkeyEditorDialog, IEditorPlugin, IEditorForm, GameCallbacks, Generated/UtinniCore.cs §memory/reload).

**Pattern extraction date:** 2026-05-27
