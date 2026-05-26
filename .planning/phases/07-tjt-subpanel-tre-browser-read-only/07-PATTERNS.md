# Phase 7: TJT subpanel — TRE Browser (read-only) - Pattern Map

**Mapped:** 2026-05-26
**Files analyzed:** 16 new/modified files (5 framework TRE, 4 framework decoders, 1 CLI verb, 6 TJT UI/wiring)
**Analogs found:** 16 / 16 (15 code analogs in the two real repos + 4 swg-client-v2 format-spec sources)

> **Cross-repo phase.** Format code (TRE/IFF/decoders + CLI) lands in **this repo** (`D:/Code/Utinni`,
> `UtinniCoreDotNet/Formats/` + `Utinni.Cli/`). UI code lands in the **sibling repo**
> (`D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/`). The `swg-client-v2` repo
> (`D:/Code/swg-client-v2`) is a **read-to-port format-spec source only** — never an analog to copy
> structurally, and never a runtime dependency (D-02).

> **Read-path > re-invent.** Per RESEARCH §"Don't Hand-Roll", almost every new file *extends* or
> *follows* an existing C# analog. The two genuinely new engineering pieces — (1) the TRE
> version-dispatch + zlib + lazy refactor and (2) the per-type decoder set — both have authoritative
> format-spec references to port from, but their **C# structure/idiom** must copy the existing
> `Formats/Tre` and `Formats/Iff` analogs in this repo.

---

## File Classification

> File names below follow RESEARCH §"Recommended Project Structure". Where the UI-SPEC resolved an
> open question (host = resizable `UtinniForm`, NOT 417px `SubPanel`), the UI file is named `FormTreBrowser`.

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `UtinniCoreDotNet/Formats/Tre/TreFile.cs` (EXTEND) | model/reader | file-I/O, transform | itself (existing) + `Formats/Iff/IffReader.cs` (dispatch idiom) | exact (self-extend) |
| `UtinniCoreDotNet/Formats/Tre/TreVersion.cs` (NEW) | model/enum | transform | `Formats/Iff/IffParseException.cs` (`TreParseError` enum sibling) | role-match |
| `UtinniCoreDotNet/Formats/Tre/TreRecord.cs` (EXTEND/split) | model | transform | itself (existing) | exact (self-extend) |
| `UtinniCoreDotNet/Formats/Tre/TreHeader.cs` (EXTEND) | model | transform | itself (existing) | exact (self-extend) |
| `UtinniCoreDotNet/Formats/Tre/CotMasterIndex.cs` (NEW) | service/reader | file-I/O, transform | `Formats/Tre/TreFile.cs` (block read + ValidateLength) | role-match |
| `UtinniCoreDotNet/Formats/Tre/TreParseException.cs` (EXTEND) | utility/error | — | itself + `Formats/Iff/IffParseException.cs` | exact (self-extend) |
| `UtinniCoreDotNet/Formats/Decoders/DataTableDecoder.cs` (NEW) | service/decoder | transform | `Formats/Iff/IffReader.cs` (consumer of) + `DataTable.cpp` (spec) | role-match |
| `UtinniCoreDotNet/Formats/Decoders/StringTableDecoder.cs` (NEW) | service/decoder | transform | `DataTableDecoder` sibling + `LocalizedStringTableReaderWriter.cpp` (spec) | role-match |
| `UtinniCoreDotNet/Formats/Decoders/ObjectTemplateDecoder.cs` (NEW) | service/decoder | transform | `DataTableDecoder` sibling + `ObjectTemplate.cpp` (spec) | role-match |
| `UtinniCoreDotNet/Formats/Decoders/AppearanceSummary.cs` (NEW) | service/decoder | transform | `DataTableDecoder` sibling + `swg_blender` mesh/skel readers (spec) | role-match |
| `Utinni.Cli/Commands/*.cs` (NEW or EXTEND parse-tre/inspect-iff) | controller (verb) | request-response | `Utinni.Cli/Commands/ParseTreCommand.cs` + `InspectIffCommand.cs` | exact |
| `TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (NEW) | component/form | event-driven | `TheJawaToolboxDotNet/UI/Forms/FormObjectBrowser.cs` | exact |
| `TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.Designer.cs` (NEW) | component/designer | — | `FormObjectBrowser.Designer.cs` | exact |
| `TheJawaToolboxDotNet/Plugin.cs` (MODIFY) | provider/registration | — | `Plugin.cs` (existing `forms.Add` line) | exact (self-extend) |
| (detail-pane sub-controls: IFF chunk tree, metadata, structured ListViews) | component | event-driven | `FormObjectBrowser` `tvDirectories` + UI-SPEC control table | role-match |
| (golden test fixtures + tests) | test | — | `Utinni.Cli.Tests/Fixtures/` + existing parse-tre/inspect-iff goldens | exact |

---

## Pattern Assignments

### `UtinniCoreDotNet/Formats/Tre/TreFile.cs` (model/reader, file-I/O + transform) — EXTEND

**Analog:** itself (the existing eager-read 0005/0006 reader) + `Formats/Iff/IffReader.cs` for the dispatch idiom.

This is the central refactor (RESEARCH Pattern 1 + Pitfalls 1-5). Three changes layer onto the existing structure: (a) version-dispatch the header + per-record decode, (b) zlib-vs-raw-deflate framing detection, (c) lazy/TOC-only enumeration (do NOT eager-read all payloads).

**License/provenance header to preserve verbatim** (`TreFile.cs:1-26`) — every `Formats/` file carries the MIT block + the "Format understood by reading swg-client-v2… No code… copied" attribution. New files MUST carry the same block (D-02 provenance hygiene):
```csharp
// Format understood by reading swg-client-v2/src/engine/shared/library/sharedFile/src/shared/{TreeFile,Iff}.{h,cpp}
// (SOE/Bootprint, All Rights Reserved). No code, comments, identifier names, or test fixtures copied
// from any reference source. Implementation original to Utinni under MIT.
```

**The version gate to REPLACE with dispatch** (`TreFile.cs:130-135`) — current hard-fail on anything but 0005/0006:
```csharp
string version = Encoding.ASCII.GetString(versionBytes);
if (version != "0005" && version != "0006")
{
    throw new TreParseException(TreParseError.UnsupportedVersion,
        "Unsupported TRE version '" + version + "'. Phase 4 supports 0005 and 0006 only.");
}
```
Replace with a `TreVersion` dispatch (D-06): recognize `0004/0005/0006/5000/6000`; route `5000` defensively as a 6000 sibling, enumerate-only (Pitfall 3 — do NOT assert its layout).

**The per-record decode loop to version-dispatch** (`TreFile.cs:220-255`) — current **size-first** order (keep for 0004/0005/0006 until a real SWGEmu fixture confirms Open Q1; do NOT change the path the existing CLI goldens exercise):
```csharp
for (int i = 0; i < recordCount; i++)
{
    int dataSize            = infoBr.ReadInt32();   // uncompressed   ← size-first (existing fixtures)
    int dataOffset          = infoBr.ReadInt32();
    int dataCompression     = infoBr.ReadInt32();
    int dataCompressedSize  = infoBr.ReadInt32();
    int checksum            = infoBr.ReadInt32();
    int nameOffset          = infoBr.ReadInt32();
    ...
}
```
For v6000/COT2000 use the **crc-first** 32-byte-stride layout (RESEARCH §"Per-TRE v6000 header"): `crc, length, offset, compressor, compressedLength, fileNameOffset, +8 pad`. The existing 24-byte `RecordInfoSize` const (`TreFile.cs:58`) must become version-dependent.

**The deflate path to make zlib-aware** (`TreFile.cs:344-345`, and the block-level path at `397-450`):
```csharp
using (var compressedStream = new MemoryStream(compressed))
using (var deflate = new DeflateStream(compressedStream, CompressionMode.Decompress))
```
Per Pitfall 2: detect `0x78 0x9c` framing (`b0 == 0x78 && (b0<<8|b1) % 31 == 0`), strip 2 header bytes + ignore trailing 4-byte Adler32, feed remainder to `DeflateStream`. Keep the raw-deflate fallback so the synthesized fixtures stay green.

**Eager-read block to make lazy** (`TreFile.cs:258-290`) — the `compressedCache = new byte[recordCount][]` loop is the anti-pattern (213k entries / 5.5 GB). Per D-08/Anti-Patterns: enumerate TOC + names only; read ONE payload on demand via a new lazy accessor. The `GetRecordData(int)` accessor signature (`TreFile.cs:311`) is the seam to keep — change it to seek/read on demand rather than read from the cache.

**Bounds-check helper to REUSE for new fields** (`TreFile.cs:476-488`) — `ValidateLength(claimed, maxBound, kindOnNegative, kindOnOverflow, fieldName)` is the single source of truth for V5 bounds checks. Extend to the new COT2000/v6000 fields (`treeFileIndex < numTreeFiles`, `fileNameOffset`, TOC-size-vs-stream-length) per Security Domain.

---

### `UtinniCoreDotNet/Formats/Tre/TreVersion.cs` (model/enum, transform) — NEW

**Analog:** `Formats/Tre/TreParseException.cs` (`TreParseError` enum, lines 33-41) — same file shape: license header + namespace + small public enum.

**Enum-as-discriminant pattern** (`TreParseException.cs:33-41`):
```csharp
public enum TreParseError
{
    BadMagic,
    UnsupportedVersion,
    Truncated,
    NegativeLength,
    ChunkLengthExceedsCap,
    DeflateExpansionExceedsCap
}
```
Mirror this for `TreVersion { V0004, V0005, V0006, V5000, V6000 }` plus a `Parse(string tag)` dispatcher. `5000` resolves but is flagged enumerate-only (Pitfall 3).

---

### `UtinniCoreDotNet/Formats/Tre/CotMasterIndex.cs` (service/reader, file-I/O + transform) — NEW

**Analog:** `Formats/Tre/TreFile.cs` — copy its read-block + ValidateLength + null-terminated-name idioms; this is a sibling reader for the COT2000/SearchTOC master index that builds the complete 213k-path enumeration (D-05 "everything the client can load").

**Block-read helper to copy** (`TreFile.cs:397-450`, `ReadBlock`) — seek, short-read detection, deflate cap. **Null-terminated-name reader to copy** (`TreFile.cs:455-469`, `ReadNullTerminatedAscii`).

**Format spec to PORT (not copy code):** `swg-client-v2/tools/swg_blender/swg_pipeline/tre_reader.py` `detect_master_index_kind` + the COT2000 header/global-TOC-entry layout in RESEARCH §"Detecting master-index kind" (offsets documented there: COT2000 magic `" COT2000"`, header fields at 8/12/16/20/24/28/32, 32-byte global TOC entry with `fileNameLength` as a length to convert to cumulative offset).

---

### `UtinniCoreDotNet/Formats/Decoders/DataTableDecoder.cs` (service/decoder, transform) — NEW

**Analog (structure/idiom):** `Formats/Iff/IffReader.cs` — a `public static` class with a `Read`/`Decode` factory, no JSON, no console (parser-purity rule, `IffReader.cs:56-58`). The decoder is a **pure read over `IffDocument`** output, not a new byte walker.

**Parser-purity rule to honor** (`IffReader.cs:56-58`):
```
/// <para><b>Parser purity:</b> This class contains NO JSON serialisation references,
/// no console output calls, and no file write calls.
/// All JSON output lives in <c>Utinni.Cli.Commands.InspectIffCommand</c>.</para>
```

**Leaf-payload access pattern** — the decoder reads scalars from `IffLeafChunk.Data` (`IffLeafChunk.cs:47`, a defensive byte[] copy). Per Pitfall 6, IFF chunk **tags are big-endian** (already handled by `IffReader.ReadInt32Be`, lines 348-359) but **payload scalars are little-endian** — decode `Data` with `BitConverter` on the LE host. Dispatch on the container's `SubTypeId` (`IffContainerChunk.cs:49`).

**Format spec to PORT:** `swg-client-v2/.../sharedUtility/src/shared/DataTable.cpp` `load_0000`/`load_0001` — RESEARCH §"Datatable structured decode": root `FORM "DTII"` → `FORM "0000"|"0001"` → `COLS` (int32 numCols + null-term names), `TYPE` (per-col enum/format), `ROWS` (int32 numRows + numRows*numCols cells).

**Error idiom to mirror:** add a decoder-specific exception following `TreParseException`/`IffParseException` (kind enum + message), so the detail pane's parse-failure state (UI-SPEC States) can surface `<reason>`.

---

### `UtinniCoreDotNet/Formats/Decoders/{StringTableDecoder,ObjectTemplateDecoder,AppearanceSummary}.cs` (service/decoder, transform) — NEW

**Analog:** `DataTableDecoder.cs` (the sibling NEW file above) for C# structure; each is a pure `IffDocument` consumer dispatched on root FORM tag (RESEARCH Pattern 5).

**Format spec sources (D-09 reference split):**
- `StringTableDecoder` → C++ `LocalizedStringTableReaderWriter.cpp` (per `iff-tre-codebase-map.md`).
- `ObjectTemplateDecoder` → C++ `ObjectTemplate.cpp` inherited-field walk.
- `AppearanceSummary` (mesh/SKMG/SKTM/KFAT counts) → `swg_blender` mesh/skeleton/anim readers (the strongest reference for graphics asset decode, D-09).

> The C++ loaders and `swg_blender` Python are **read-to-port format specs**, NOT structural analogs. Copy the C# class shape from `DataTableDecoder.cs`; port only the layout/algorithm.

---

### `Utinni.Cli/Commands/*.cs` (controller/verb, request-response) — NEW or EXTEND

**Analog:** `Utinni.Cli/Commands/ParseTreCommand.cs` (exact for the TRE path) and `InspectIffCommand.cs` (exact for IFF/decoder paths). Keeps CLI + browser on one code path (success criterion #4 / Pitfall 7).

**Verb-handler skeleton to copy** (`ParseTreCommand.cs:33-68`) — `[Verb]` options class + `static int Run(opts)` + FileNotFound→exit 3, ParseException→exit 2, IOException→exit 2, generic NOT caught:
```csharp
[Verb("parse-tre", HelpText = "Parse a .tre archive and emit sorted-key JSON to stdout.")]
public class ParseTreOptions
{
    [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .tre file.")]
    public string Path { get; set; }
}

public static int Run(ParseTreOptions o)
{
    if (!File.Exists(o.Path))
        return JsonOutput.EmitError("parse-tre", "FileNotFound", "TRE file not found: " + o.Path, exitCode: 3);
    try
    {
        var tre = TreFile.Open(o.Path);
        return JsonOutput.EmitSuccess("parse-tre", BuildResult(tre, o.Path));
    }
    catch (TreParseException ex)
    {
        return JsonOutput.EmitError("parse-tre", ex.Kind.ToString(), ex.Message, exitCode: 2);
    }
    catch (IOException ex)
    {
        return JsonOutput.EmitError("parse-tre", "IoError", ex.Message, exitCode: 2);
    }
}
```

**JSON-result-builder pattern to copy** (`InspectIffCommand.cs:88-99`, `BuildResult`) — the result object NEVER pre-adds `schemaVersion`/`command`; `JsonOutput.EmitSuccess` wraps it. Keys sorted by `JsonOutput`'s sort pass. The flat+tree dual projection (`InspectIffCommand.cs:108-194`) is a **locked schemaVersion:1 contract** — do NOT drop it.

**Verb registration to MODIFY** (`Program.cs:43-53`) — add any new decode verb to the `ParseArguments<...>` type list AND the `.MapResult(...)` lambda list:
```csharp
return parser.ParseArguments<
        Commands.ParseTreOptions,
        Commands.ListObjectsOptions,
        Commands.InspectIffOptions,
        Commands.ValidatePluginOptions>(args)
    .MapResult(
        (Commands.ParseTreOptions o)    => Commands.ParseTreCommand.Run(o),
        ...
        errs => 1);
```

> **Anti-pattern to avoid** (`ListObjectsCommand.cs:42-70`): `list-objects` uses a provisional `OBJS` byte-scan (REVIEWS MEDIUM-13 debt). New decode paths route through the real `IffReader` + decoders, NOT sentinel scanning (RESEARCH Anti-Patterns).

---

### `TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (component/form, event-driven) — NEW

**Analog:** `TheJawaToolboxDotNet/UI/Forms/FormObjectBrowser.cs` — the **direct template** (D-03). UI-SPEC resolved host = resizable `UtinniForm` in the `forms` list (NOT a 417px SubPanel), so this is a Form exactly like `FormObjectBrowser`.

**Class shape + ctor + theming** (`FormObjectBrowser.cs:46-120`):
```csharp
public partial class FormTreBrowser : UtinniForm, IEditorForm
{
    private readonly IEditorPlugin editorPlugin;
    private readonly UtINI ini;
    public FormTreBrowser(IEditorPlugin editorPlugin)
    {
        InitializeComponent();
        // TJT icon load from plugin dir, guarded (FormObjectBrowser.cs:84-90)
        this.editorPlugin = editorPlugin;
        ini = editorPlugin.GetConfig();
        CreateSettings();
        ini.Load();
        Width  = ini.GetInt("TreBrowser", "width");   // persist size to settings.ini
        Height = ini.GetInt("TreBrowser", "height");
        // Theming pulled from Colors.*(), NEVER raw FromArgb (UI-SPEC Color):
        tvDirectories.BackColor = Colors.PrimaryHighlight();
        tvDirectories.ForeColor = Colors.Font();
        tvDirectories.BorderStyle = BorderStyle.None;
        Task load = LoadRepo();
    }
}
```

**Settings-persistence pattern** (`FormObjectBrowser.cs:122-128`, `CreateSettings`) — `ini.AddSetting("ObjectBrowser", "width", "525", UtINI.Value.Types.VtInt)`. Mirror under a `[TreBrowser]` section (UI-SPEC Window: default 1100×700, min 760×480, persist `SplitterDistance`).

**Async load + game-readiness poll** (`FormObjectBrowser.cs:130-135`) — the marshaling seam for the Repository overlay:
```csharp
private async Task LoadRepo()
{
    while (!Game.IsRunning)
    {
        await Task.Delay(1);
    }
    var dirInfo = Game.Repository.GetDirectoryInfo("object");
    ...
}
```
**Differences for TreBrowser** (RESEARCH Pattern 4): (a) the disk TRE enumeration does NOT require a running game — populate the tree from disk first on a background `Task`, marshal node inserts via `Control.Invoke`; overlay the `Game.Repository` "loaded" flags once `Game.IsRunning`; (b) cover ALL directories + ALL extensions (not just `object/`/`.iff`); (c) lazy `BeforeExpand` node population (100k+ entries, Pitfall 5 — never build all 213k nodes up front).

**Virtual-path tree-build (split on `/`) pattern** (`FormObjectBrowser.cs:192-205`):
```csharp
TreeNode curNode = tmpRootNode;
string dirPath = dir.Key;
string[] splitPaths;
while ((splitPaths = dirPath.Split('/')).Length > 1)
{
    if (!curNode.Nodes.ContainsKey(splitPaths[0]))
        curNode.Nodes.Add(splitPaths[0], splitPaths[0]);
    curNode = curNode.Nodes[curNode.Nodes.IndexOfKey(splitPaths[0])];
    dirPath = dirPath.Substring(splitPaths[0].Length + 1);
}
```

**AfterSelect path-reconstruction pattern** (`FormObjectBrowser.cs:240-252`) — walk parents to rebuild the full virtual path, then populate the detail pane:
```csharp
string dirPath = tvDirectories.SelectedNode.Text + '/';
TreeNode curNode = tvDirectories.SelectedNode;
while (curNode.Parent != null)
{
    curNode = curNode.Parent;
    dirPath = curNode.Text + "/" + dirPath;
}
```

**Filter pattern to ADAPT** (`FormObjectBrowser.cs:271-295`, `txtFilter_TextChanged` + `FilterFiles`) — current code filters synchronously on every `TextChanged` with `filename.Contains(...)`. UI-SPEC requires a **250ms debounce** (restarting WinForms `Timer`) and case-insensitive substring on the full path (100k+ entries would jank synchronously).

**Already-open guard** (`FormObjectBrowser.cs:219-236`, `Create`) — copy verbatim for the `IEditorForm.Create` "Activate : Show" behavior (UI-SPEC Window Open behavior):
```csharp
public Form Create(IEditorPlugin editorPlugin, List<Form> parentChildren)
{
    foreach (Form form in parentChildren)
        if (form.GetType() == typeof(FormTreBrowser)) { form.Activate(); return null; }
    FormTreBrowser f = new FormTreBrowser(editorPlugin);
    f.Show();
    parentChildren.Add(f);
    return f;
}
```

---

### `TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.Designer.cs` (component/designer) — NEW

**Analog:** `FormObjectBrowser.Designer.cs` — control declarations, themed `TreeView` wiring, `AfterSelect`/`TextChanged` event hookup, `AutoScaleMode.Font` + `AutoScaleDimensions(6F,13F)`, `ContextMenuStrip` wiring, `DrawName = true`.

**Themed `TreeView` declaration** (`FormObjectBrowser.Designer.cs:56,72-81`) + **`ContextMenuStrip` wiring** (`63-64,175-187`) — reuse for the nav tree, the second IFF chunk `TreeView` (UI-SPEC §detail pane section 3), and the `Copy path`/`Copy CRC` `UtinniContextMenuStrip` (UI-SPEC §metadata header).

**Form footer block** (`FormObjectBrowser.Designer.cs:228-249`) — `AutoScaleMode = Font`, `DrawName = true`, `Controls.Add(...)` order. For the two-region layout add a `SplitContainer` (`Orientation = Vertical`, `BackColor = Colors.Primary()`, per UI-SPEC §two-region split). Detail pane = a `Panel` with `AutoScroll = true` hosting `CollapsiblePanel` sections.

> **Theming caveat:** the existing Designer hard-codes `Color.FromArgb(64,64,64)` literals (`txtFilter` line 113) and the accent-button literal (line 162) — UI-SPEC §Color flags these as the **known anti-pattern**. The new browser MUST pull from `Colors.*()` accessors, not literals.

---

### `TheJawaToolboxDotNet/Plugin.cs` (provider/registration) — MODIFY

**Analog:** itself — the existing `forms.Add(new FormObjectBrowser(this))` line is the exact registration seam (RESEARCH Pattern 3, UI-SPEC §MEF SPI conformance).

**Registration line to add** (`Plugin.cs:57`):
```csharp
forms.Add(new FormObjectBrowser(this));
forms.Add(new FormTreBrowser(this));   // NEW — UI-SPEC: register via GetForms(), NOT GetSubPanels()
```
Keep `GetSubPanels()` returning `null` (`Plugin.cs:97`) and `GetStandalonePanels()` returning the existing control container (`Plugin.cs:92-95`) — do NOT widen the SPI (CON-M-01/02).

---

## Shared Patterns

### Provenance + license header
**Source:** every `Formats/` file, e.g. `TreFile.cs:1-26`
**Apply to:** all NEW framework files (`TreVersion`, `CotMasterIndex`, all 4 decoders)
The MIT block + the "Format understood by reading swg-client-v2… No code… copied… original to Utinni under MIT" attribution (D-02). New decoders that reference C++ loaders/`swg_blender` should adjust the cited path but keep the "no code copied" assertion.

### Parser purity (no JSON / no console / no file-write in `Formats/`)
**Source:** `Formats/Iff/IffReader.cs:56-58`
**Apply to:** all framework readers/decoders. JSON lives only in `Utinni.Cli` (Pitfall 7). A method only called from `TheJawaToolboxDotNet` and never from a CLI verb/test is the warning sign of drift.

### Bounds-checking + DoS caps (V5 / T-04-DoS)
**Source:** `Formats/Tre/TreFile.cs:476-488` (`ValidateLength`), `:54-55` (`MaxBlockSize = 256 MB`); `Formats/Iff/IffReader.cs:70` (`MaxChunkSize = 64 MB`), `:166-179` (ordered negative→cap checks)
**Apply to:** TRE reader (new COT2000/v6000 fields), CotMasterIndex, all decoders. Validate every offset against stream/block length before use; cap inflate output; validate the zlib header (`%31==0`) before trusting it (Security Domain).

### Structured exception with kind-enum
**Source:** `Formats/Tre/TreParseException.cs` (`Kind` + `TreParseError` enum); `Formats/Iff/IffParseException.cs` (sibling)
**Apply to:** new decoder exceptions. The CLI surfaces `ex.Kind.ToString()` as `error.kind` (`ParseTreCommand.cs:59`); the UI parse-failure state surfaces `ex.Message` (UI-SPEC States).

### Themed-control reuse (no raw `Color.FromArgb`)
**Source:** `UI/Theme/Colors.cs` accessors; `FormObjectBrowser.cs:102-109` theming block; `UI/Controls/UtinniTextbox.cs:40-44`
**Apply to:** all TreBrowser UI. `Colors.Primary()` (#282828, dominant), `Colors.PrimaryHighlight()` (#404040, raised data surfaces), `Colors.Font()` (loaded nodes), `Colors.FontDisabled()` (#646464, on-disk-not-loaded overlay), `Colors.Secondary()` (#007ACC, reserved accent). UI-SPEC §Color forbids literals.

### Live Repository overlay (read-only consumption, CON-N-02)
**Source:** `FormObjectBrowser.cs:137-141` + `Generated/UtinniCore.cs:10546-10787` (`Repository`/`DirectoryInfo` binding)
**Apply to:** the tree's "loaded/resolvable" overlay (D-05). Consume `Game.Repository.GetDirectoryInfo(dir).StartIndex/Size` + `GetFilenameAt(i)` + `FilenameCount` read-only — do NOT modify the native `hkSearchTree` detour. The harvest is a destructive one-shot (RESEARCH Runtime State) — never call `getAllFilenames()` a second time; treat the overlay as best-effort (disk is source of truth).
```csharp
var dirInfo = Game.Repository.GetDirectoryInfo("object");
for (int i = 0; i < dirInfo.Size; i++)
{
    string fn = Game.Repository.GetFilenameAt(dirInfo.StartIndex + i);
    ...
}
```

### Game-thread vs UI-thread marshaling
**Source:** `FormObjectBrowser.cs:130-135` (`await Task.Delay` poll) + `:444,497` (`GameCallbacks.AddPreMainLoopCall`/`AddMainLoopCall`)
**Apply to:** background disk enumeration + the live overlay read. Mutate UI controls only via `Control.Invoke`; touch game state only via `GameCallbacks` (UI-SPEC §Accessibility/Interaction). No modal dialogs during load (inline status labels).

### CLI verb + JSON-envelope + golden-test lock-step (success criterion #4)
**Source:** `ParseTreCommand.cs` + `InspectIffCommand.cs` + `Program.cs:43-53`
**Apply to:** every new/extended decode path. The browser calls the same `Formats/` API a CLI verb exercises with a golden fixture (TEST-03 precedent, Pitfall 7).

### IEditorForm / UtinniForm host conformance
**Source:** `UI/Forms/IEditorForm.cs` (`GetName()` + `Create(...)`); `UI/Forms/UtinniForm.cs` (resizable titlebar, `DrawName`, `IconImage`, 32px titlebar, `Colors.Secondary()` 2px accent rule at `OnPaint:159`)
**Apply to:** `FormTreBrowser` (implements `IEditorForm`, subclasses `UtinniForm`). The titlebar accent rule is the same `Colors.Secondary()` 2px line UI-SPEC reserves for the type/version banner.

---

## No Analog Found

No files lack a usable C# analog. Two areas have **format-spec references but no structural C# analog to copy** — flagged so the planner knows to copy idiom from the sibling decoder, not from the spec source:

| File | Role | Data Flow | Reason / Reference |
|------|------|-----------|--------------------|
| `Formats/Decoders/AppearanceSummary.cs` | decoder | transform | No existing C# mesh/skeleton decoder. Structure copies `DataTableDecoder.cs`; layout ports from `swg_blender` mesh/skel readers (D-09). No byte-verified fixture this session (RESEARCH A3 — `swg-main/serverdata` not probed). |
| `Formats/Tre/CotMasterIndex.cs` | reader | file-I/O | No existing master-index reader (existing `TreFile` is per-archive only). Structure copies `TreFile.cs` block-read idioms; COT2000 layout ports from `tre_reader.py` + `sample-tre-files.md`. |

**`5000`-version handling has no fixture anywhere** (Pitfall 3 / D-06b): recognize the tag, route through the v6000 structural-sibling path behind a fixture-gated branch, degrade to enumerate-only. Add a skipped/`[Trait]`-gated test that activates when a fixture appears. Do NOT assert a layout.

---

## Metadata

**Analog search scope:**
- This repo: `UtinniCoreDotNet/Formats/{Tre,Iff}/`, `UtinniCoreDotNet/UI/{Controls,Forms,Theme}/`, `UtinniCoreDotNet/PluginFramework/`, `UtinniCoreDotNet/Generated/UtinniCore.cs` (Repository binding), `Utinni.Cli/{Program.cs,Commands/}`
- Sibling repo: `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/{Plugin.cs,UI/Forms/,UI/SubPanels/}`
- Format-spec source (read-to-port, NOT analog): `D:/Code/swg-client-v2/tools/swg_blender/`, `docs/research/`, `src/engine/.../DataTable.cpp`

**Files scanned (read in full or targeted):** TreFile.cs, TreRecord.cs, TreHeader.cs, TreParseException.cs, IffReader.cs, IffDocument.cs, IffLeafChunk.cs, IffContainerChunk.cs, IffChunk.cs, ParseTreCommand.cs, InspectIffCommand.cs, ListObjectsCommand.cs (head), Program.cs, IEditorPlugin.cs, IEditorForm.cs, SubPanel.cs, CollapsiblePanel.cs, Colors.cs, UtinniForm.cs, UtinniTextbox.cs, FormObjectBrowser.cs, FormObjectBrowser.Designer.cs, Plugin.cs, Generated/UtinniCore.cs §Repository.

**Project skills:** none found (`.claude/skills`, `.agents/skills` absent). No root `CLAUDE.md`.

**Pattern extraction date:** 2026-05-26
