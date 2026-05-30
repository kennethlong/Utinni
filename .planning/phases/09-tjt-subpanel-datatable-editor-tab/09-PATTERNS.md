# Phase 9: tjt-subpanel-datatable-editor-tab — Pattern Map

**Mapped:** 2026-05-29
**Files analyzed:** 30 new + 2 modified (+ 0 new test-project files; existing SDK-style auto-glob)
**Analogs found:** 30 / 30 (100%)
**Analog source mix:** Phase 8 (24 files) · Phase 7 (3 files) · Phase 4 (2 files) · cross-Phase reuse (1 file)

This phase is the most leveraged Wave-1 editor yet. Every new file mirrors a Phase-7 or Phase-8 shipped file at the function-signature level, with shape-deviation only where the DataGridView / typed-cell surface differs from the chunk-tree / hex-edit surface. **There is essentially zero greenfield architectural surface** — the work is mechanical port + per-cell typed elaboration of patterns already proven in 7/8.

---

## File Classification

### NEW — Framework primitives (`UtinniCoreDotNet/Formats/Datatable/`) — pure managed, safe additive surface

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `UtinniCoreDotNet/Formats/Datatable/DataTableColumnType.cs` | format-primitive (type-spec parser + MangleValue + EnumMap + default cell) | pure-function | `UtinniCoreDotNet/Formats/Iff/IffParseException.cs` (small companion type) + ported `DataTableColumnType.cpp` (SOE source, NOT in-repo) | role-match |
| `UtinniCoreDotNet/Formats/Datatable/DataTableCellValue.cs` | value-type (discriminated union: Int/Float/String) | pure-function | none in-repo (mirrors C++ `DataTableCell.h`) | derived |
| `UtinniCoreDotNet/Formats/Datatable/DataTableHashCrc.cs` | hash helper (SOE CRC variant for `DT_HashString`) | pure-function | port of `Crc::normalizeAndCalculate` (NOT in-repo) | derived |
| `UtinniCoreDotNet/Formats/Datatable/DataTableDocument.cs` | document (FromIff parser + typed cell decoder for V0/V1) | read-only | `UtinniCoreDotNet/Formats/Iff/IffDocument.cs` + `MutableIffDocument.cs::FromDocument` | role-match |
| `UtinniCoreDotNet/Formats/Datatable/MutableDataTableCell.cs` | mutable cell (typed value + IsDirty + captured original-bytes slice) | read-write | `UtinniCoreDotNet/Formats/Iff/MutableIffNode.cs` lines 156-296 (hybrid-DOM: payload + `capturedSlice` + `IsDirty` + `MarkDirtyAndInvalidateAncestors`) | exact |
| `UtinniCoreDotNet/Formats/Datatable/MutableDataTableColumn.cs` | mutable column (name + ColumnType + IsDirty) | read-write | `UtinniCoreDotNet/Formats/Iff/MutableIffNode.cs` lines 86-145 (TypeId / SubTypeId / IsDirty setters with ancestor invalidation) | exact |
| `UtinniCoreDotNet/Formats/Datatable/MutableDataTableRow.cs` | mutable row (cells[] + IsDirty roll-up from cells) | read-write | `MutableIffNode.cs` container + `Remove` / `ReorderUp/Down` (lines 297-404) | exact |
| `UtinniCoreDotNet/Formats/Datatable/MutableDataTableDocument.cs` | mutable document (version + columns + rows + Build → MutableIffDocument) | read-write | `UtinniCoreDotNet/Formats/Iff/MutableIffDocument.cs` (FromDocument / RemoveByStableId / DeriveStableId) | exact |
| `UtinniCoreDotNet/Formats/Datatable/DataTableWriter.cs` | serializer (MutableDataTableDocument → DTII bytes via IffWriter) | pure-function | `UtinniCoreDotNet/Formats/Iff/IffWriter.cs` (consumed verbatim — Phase 9 composes ON it; `IffWriter.Write(mutDoc)` is the leaf call) | exact (composition) |
| `UtinniCoreDotNet/Formats/Datatable/DataTableParseException.cs` | exception type | none | `UtinniCoreDotNet/Formats/Iff/IffParseException.cs` | exact |

### NEW — Editor controller (`UtinniCoreDotNet/Editing/`) — pure managed, no UI dep, unit-testable

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `UtinniCoreDotNet/Editing/DatatableEditController.cs` | controller (undo/redo + transactions + IsDirty + EditApplied event) | event-driven | `UtinniCoreDotNet/Editing/IffEditController.cs` lines 67-160 | exact (verbatim shape) |
| `UtinniCoreDotNet/Editing/IDatatableEditCommand.cs` + `DatatableEditCommands` factory | command interface + factory | strategy | `UtinniCoreDotNet/Editing/IffEditController.cs` lines 167-446 (`IIffEditCommand` + `IffEditCommands` factory + concrete command classes) | exact (verbatim shape) |

### NEW — Framework tests (`UtinniCoreDotNet.Tests/FormatsTests/Datatable/`) — SDK-style auto-glob (no csproj edit)

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableColumnTypeTests.cs` | test (parser + MangleValue per DT_*, ~25-30 [Fact]s) | unit-test | Phase 8 `UtinniCoreDotNet.Tests/FormatsTests/Iff/IffReaderTests.cs` pattern | role-match |
| `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableHashCrcTests.cs` | test (CRC parity, ~4-6 [Fact]s) | unit-test | (small focused suite) | role-match |
| `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableDocumentTests.cs` | test (V0 + V1 fixtures + DT_Comment + per-DT_* read; ~15-20 [Fact]s) | unit-test | Phase 8 `MutableIffDocumentTests.cs` | role-match |
| `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableWriterTests.cs` | test (round-trip byte-exact; ~15-20 [Fact]s) | unit-test | Phase 8 `IffWriterTests.cs` | role-match |
| `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DatatableEditControllerTests.cs` | test (apply/undo/redo × 10+ commands; cascade; CSV transaction; ~30-40 [Fact]s) | unit-test | Phase 8 `IffEditControllerTests.cs` | role-match |
| `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DatatableFixtures.cs` | test-helper (synthetic .tab builder) | test-fixture | `Utinni.Cli.Tests/Infrastructure/IffBuilder.cs` + `TreFixtureBuilder.cs` | exact pattern |

### NEW — CLI verb (`Utinni.Cli/Commands/`) — SDK-style csproj, auto-glob

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `Utinni.Cli/Commands/RoundtripTabCommand.cs` | CLI verb (`roundtrip-tab`) | request-response (process) | `Utinni.Cli/Commands/RoundtripIffCommand.cs` (lines 25-110+) | exact (verbatim shape) |

### MODIFIED — CLI verb registration

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `Utinni.Cli/Program.cs` | composition root (Parser.ParseArguments + MapResult) | request-response | (self — add `RoundtripTabOptions` to type list + MapResult line) | exact extension point |

### NEW — CLI tests (`Utinni.Cli.Tests/`) — SDK-style auto-glob

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `Utinni.Cli.Tests/Commands/RoundtripTabCommandTests.cs` | CLI test (golden harness) | unit-test | `Utinni.Cli.Tests/Commands/RoundtripIffCommandTests.cs` | exact pattern |
| `Utinni.Cli.Tests/Infrastructure/DataTableFixtureBuilder.cs` | test-helper (builds DTII bytes via IffBuilder) | test-fixture | `Utinni.Cli.Tests/Infrastructure/IffBuilder.cs` + `TreFixtureBuilder.cs` | exact pattern |

### NEW — TJT WinForms host (`The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/`) — explicit `<Compile Include>` required

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `UI/Forms/FormDatatableEditor.cs` (+ `.Designer.cs`) | TJT singleton form (UtinniForm; mirrors FormIffEditor lifecycle, ProcessCmdKey, BuildSaveMenu, RefreshSaveMenuEnabledState, hide-not-dispose, OpenFromTreEntry, ReloadStatusBadge) | UI-only (binds controller event) | `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` (entire file — lines 131, 205-232, 234-242, 246-260, 271-300, 306-365, 880-1019, 1410-1495, 1569-1596) | exact (verbatim host pattern) |
| `UI/Forms/FormAddColumnDialog.cs` (+ `.Designer.cs`) | TJT per-call modal (Add column: name + type combo) | UI-only | `UI/Forms/FormFourCcDialog.cs` (small input dialog with one text field + 2 buttons; per-call `using (var dlg = ...)` lifecycle) | exact pattern |
| `UI/Forms/FormTypeChangeCascadeDialog.cs` (+ `.Designer.cs`) | TJT per-call modal (D-04 cascade resolution: embedded grid + per-row Accept-mangled / Edit-cell + footer Revert-type) | UI-only | `UI/Forms/FormSaveConfirmDialog.cs` (per-call modal lifecycle + UtinniForm host + accept/cancel verbs) + an embedded `ThemedDataGridView` | role-match (own dialog, NOT FormSaveConfirmDialog reuse — UI-SPEC assumption #6) |
| `UI/Forms/FormCsvImportPreviewDialog.cs` (+ `.Designer.cs`) | TJT per-call modal (CSV preview: per-column diff + Color.Red invalid-rows list + Import/Cancel) | UI-only | `UI/Forms/FormSaveConfirmDialog.cs` (per-call modal lifecycle) + embedded `ThemedDataGridView` (per-column diff display) | role-match (own dialog, UI-SPEC assumption #7) |

### NEW — TJT controls (`The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/`) — explicit `<Compile Include>` required

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `UI/Controls/ThemedDataGridView.cs` | TJT control (DataGridView wrapper applying ~25 themed properties via Colors.*() in ctor) | UI-only | `UI/Controls/IffChunkTree.cs` (TJT-side UserControl alongside, applies themed TreeView properties in ctor) — same TJT.UI.Controls namespace pattern; Phases 10/11 reuse | role-match (TreeView→DataGridView; same wrapper idiom) |
| `UI/Controls/DatatableColumnFactory.cs` | TJT helper (switch on `DataTableColumnType.Type` → DataGridViewColumn subclass) | pure-function | RESEARCH.md § Pattern 3 inline `DatatableColumnFactory.Build` (lines 428-458) | derived (RESEARCH-spec'd) |
| `UI/Controls/DatatableHashStringEditor.cs` | TJT control (floating UtinniLabel anchored on CellBeginEdit; live hash preview at Consolas 9pt) | UI-only | (no in-repo analog; UI-SPEC § Per-type cell widget contract row `DT_HashString` is the spec) | derived (UI-SPEC) |
| `UI/Controls/DatatableNumericUpDownEditingControl.cs` | TJT control (UtinniNumericUpDown adapted to IDataGridViewEditingControl for `DT_Int`/`DT_Float` cell editor swap-in via `EditingControlShowing`) | UI-only | UtinniNumericUpDown (existing themed control in `UtinniCoreDotNet/UI/Controls/`) wrapped with `IDataGridViewEditingControl` (BCL interface) | derived (BCL pattern) |

### NEW — TJT save / serialization (`The Jawa Toolbox/TheJawaToolboxDotNet/Saving/`) — explicit `<Compile Include>` required

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `Saving/DatatableSaveTargets.cs` | TJT save target (thin composition shim: `Build() → byte[]` → `IffSaveTargets.SaveLooseOverride / SaveToPath / SaveInPlace` + `TreRepackSaveTarget.Apply`) | file-I/O | `Saving/IffSaveTargets.cs` (Phase 8 — 3 modes 1/2/4; reuse verbatim) + `Saving/TreRepackSaveTarget.cs` (Phase 8 — reuse verbatim) | exact (composition shim) |
| `Saving/DatatableCsvSerializer.cs` | TJT serializer (CSV/TSV export + parse + per-cell delta diff + Build CsvImportPlan via `DataTableColumnType.MangleValue`) | transform / file-I/O | RESEARCH.md § Pattern 5 `CsvImportPlan.Build` (lines 498-519) + Phase 7 `TreDetailPane` ASCII heuristic for hex-vs-text mode (similar parse-back idiom) | derived (no in-repo CSV analog) |

### MODIFIED — TJT plugin registration

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `TheJawaToolboxDotNet/Plugin.cs` | composition root (adds 1 `forms.Add(new FormDatatableEditor(this))` inside its own try/catch isolation block; MEF SPI unchanged — `GetForms` returns list with the new entry; `GetSubPanels` stays null) | UI-only | `Plugin.cs` lines 75-82 (Phase 8 — identical 7-line addition for FormIffEditor) | exact (verbatim line-block pattern) |

### MODIFIED — TJT IFF Editor hand-off (D-10.3)

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `UI/Forms/FormIffEditor.cs` | MODIFY — add `_miSwitchToDatatableView` ToolStripMenuItem, visible only when `document.Root.TypeId == "DTII"`; click handler finds-or-creates FormDatatableEditor + hands the MutableIffDocument (or its bytes + Source) directly without re-parsing | event-driven | self lines 880-928 (`BuildSaveMenu` — same context-menu / ToolStripMenuItem construction idiom) + lines 1416-1460 (`OpenFromTreEntry` — hand-off shape) | exact (verbatim extension pattern) |

### MODIFIED — TJT TRE Browser hand-off (D-10.2)

| File | Role | Data Flow | Closest Analog | Match Quality |
|------|------|-----------|----------------|---------------|
| `UI/Forms/FormTreBrowser.cs` | MODIFY — add `_miOpenInDatatableEditor` context-menu item alongside existing `_miOpenInIffEditor`; visibility predicate is `extension == ".tab" OR root tag == "DTII"` (cheap byte-scan via `IffReader.TryPeekRootTypeId` if added, else extension-only); click handler mirrors `OnOpenInIffEditor` verbatim, swapping `FormIffEditor` → `FormDatatableEditor` and `FindOrCreateIffEditor` → `FindOrCreateDatatableEditor` | event-driven | self lines 61, 144-146, 175-183, 185-235, 240-248 (verbatim) | exact (verbatim extension pattern) |

---

## Pattern Assignments

### `UtinniCoreDotNet/Formats/Datatable/MutableDataTableCell.cs` (mutable cell, hybrid-DOM, CF-04)

**Analog:** `D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/MutableIffNode.cs`

**Imports pattern** (MutableIffNode.cs lines 29-32):
```csharp
using System;
using System.Collections.Generic;

namespace UtinniCoreDotNet.Formats.Iff   // → namespace UtinniCoreDotNet.Formats.Datatable for Phase 9
```

**Hybrid value + captured-slice + IsDirty pattern** (MutableIffNode.cs lines 156-175, 245-296, 484-495 — mirror for the typed cell):
```csharp
// Field shape mirrors MutableIffNode.payload + capturedSlice + IsDirty.
private DataTableCellValue value;
private byte[] originalSlice;     // captured at FromIff time (CF-04 hybrid DOM)
private bool isDirty;

public bool IsDirty { get { return isDirty; } }

public byte[] GetPayloadCopy()   // defensive-copy idiom from MutableIffNode lines 255-260
{
    return (byte[])value.SerializeFresh().Clone();
}

public void SetValue(DataTableCellValue newValue)   // mirrors SetPayload lines 279-286
{
    if (newValue == null) throw new ArgumentNullException("newValue");
    if (DataTableCellValue.Equals(value, newValue)) return;   // no-op preserves originalSlice
    value = newValue;
    MarkDirtyAndInvalidateAncestors();   // ports MutableIffNode lines 484-495
}

private void MarkDirtyAndInvalidateAncestors()
{
    isDirty = true;
    originalSlice = null;
    if (Row != null) Row.MarkDirty();
    if (Column != null) Column.MarkDirty();   // column dirtiness rolls up similar to ancestor walk
}

// Writer entry point — emit originalSlice byte-for-byte if !isDirty AND has slice; else fresh.
internal void WritePayload(BinaryWriter bw, DataTableColumnType ct)
{
    if (!isDirty && originalSlice != null) { bw.Write(originalSlice); return; }
    WriteFreshPayload(bw, ct, value);   // BasicType switch: int32 / float32 / NUL-terminated string
}
```

**Deviation:** original-slice is per-cell (smaller granularity than MutableIffNode's per-chunk slice). Ancestor invalidation walks `Row → Document` (only two levels) instead of unbounded `Parent` chain.

---

### `UtinniCoreDotNet/Editing/DatatableEditController.cs` (controller — CF-06)

**Analog:** `D:/Code/Utinni/UtinniCoreDotNet/Editing/IffEditController.cs` lines 67-160 + 167-446

**Imports pattern** (IffEditController.cs lines 28-32):
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using UtinniCoreDotNet.Formats.Iff;   // → ...Formats.Datatable for Phase 9

namespace UtinniCoreDotNet.Editing
```

**Controller class shape** (IffEditController.cs lines 67-160 — port VERBATIM):
```csharp
public sealed class DatatableEditController   // mirrors IffEditController class shape
{
    private readonly MutableDataTableDocument document;
    private readonly Stack<IDatatableEditCommand> undoStack = new Stack<IDatatableEditCommand>();
    private readonly Stack<IDatatableEditCommand> redoStack = new Stack<IDatatableEditCommand>();
    private int netAppliedCount;   // baseline-clean dirty (Codex unique concern — Phase 8 D-08)

    public DatatableEditController(MutableDataTableDocument document) { /* lines 85-90 */ }
    public MutableDataTableDocument Document { get { return document; } }
    public bool IsDirty   => netAppliedCount > 0;   // lines 96-99
    public bool CanUndo   => undoStack.Count > 0;
    public bool CanRedo   => redoStack.Count > 0;
    public int NeedsReviewCount { get { return /* tally cells with NeedsReview flag */; } }   // NEW for D-04
    public event EventHandler EditApplied;

    public void Apply(IDatatableEditCommand cmd) { /* lines 117-125 */ }
    public void Undo() { /* lines 131-139 */ }
    public void Redo() { /* lines 145-153 */ }
}
```

**Command interface + factory** (IffEditController.cs lines 167-262):
```csharp
public interface IDatatableEditCommand   // mirrors IIffEditCommand
{
    void Do(MutableDataTableDocument doc);
    void UndoOp(MutableDataTableDocument doc);
}

public static class DatatableEditCommands   // mirrors IffEditCommands
{
    public static IDatatableEditCommand EditCellValue(MutableDataTableCell cell, DataTableCellValue newValue);   // mirrors EditLeafPayload
    public static IDatatableEditCommand AddRow(MutableDataTableDocument doc, int atIndex);                       // mirrors AddLeaf
    public static IDatatableEditCommand RemoveRow(MutableDataTableRow row);                                      // mirrors Remove (incl. CR-01 insert-by-reference fix)
    public static IDatatableEditCommand MoveRowUp/Down(MutableDataTableRow row);                                 // mirrors MoveUp/Down
    public static IDatatableEditCommand AddColumn(MutableDataTableDocument doc, string name, DataTableColumnType ct, int atIndex);
    public static IDatatableEditCommand RemoveColumn(MutableDataTableColumn col);
    public static IDatatableEditCommand MoveColumnLeft/Right(MutableDataTableColumn col);
    public static IDatatableEditCommand RenameColumn(MutableDataTableColumn col, string newName);
    public static IDatatableEditCommand ChangeColumnType(MutableDataTableColumn col, DataTableColumnType newCt);  // runs MangleValue cascade
    public static IDatatableEditCommand ApplyCsvImport(MutableDataTableDocument doc, CsvImportPlan plan);        // single-transaction wrap (D-08)
}
```

**RemoveCommand insert-by-reference fix** (IffEditController.cs lines 333-361 — port the 08-REVIEW CR-01 fix):
```csharp
// Re-attach the original node BY REFERENCE so a subsequent Do() finds it in
// parent.Children and removes it cleanly. (Avoid the snapshot+Materialize trap.)
public void UndoOp(MutableDataTableDocument doc)
{
    parent.InsertChildAtInternal(originalIndex, node);
}
```

**Deviation:** new `ApplyCsvImport` command wraps N `EditCellValue` deltas as a single undo entry (D-08). New `ChangeColumnType` command must run `MangleValue` on every cell in the column, set per-cell `NeedsReview` flag on failures, and (on Undo) revert the column type AND clear all `NeedsReview` flags it set.

---

### `UtinniCoreDotNet/Formats/Datatable/DataTableDocument.cs` (typed reader)

**Analog:** `UtinniCoreDotNet/Formats/Iff/MutableIffDocument.cs::FromDocument` + RESEARCH.md § Pattern 1 (lines 343-374 of RESEARCH)

**Reader entry pattern** (RESEARCH.md Pattern 1 lines 343-374 — port directly):
```csharp
public static DataTableDocument FromIff(MutableIffDocument iffDoc)
{
    if (iffDoc == null) throw new ArgumentNullException("iffDoc");
    var root = iffDoc.Root;
    if (root == null || root.Kind != MutableIffNodeKind.Container || root.TypeId != "FORM" || root.SubTypeId != "DTII")
        throw new DataTableParseException("Root is not FORM DTII.");
    var ver = root.Children[0];
    string version = ver.SubTypeId;     // "0000" or "0001"
    var cols  = ver.Children.FirstOrDefault(c => c.TypeId == "COLS");
    var types = ver.Children.FirstOrDefault(c => c.TypeId == "TYPE");
    var rows  = ver.Children.FirstOrDefault(c => c.TypeId == "ROWS");
    // ... per-cell decode per DataTableColumnType.BasicType ...
}
```

**No in-repo analog for the typed cell decoder.** Port from `swg-client-v2/src/engine/shared/library/sharedUtility/src/shared/DataTable.cpp:444-603`.

---

### `UtinniCoreDotNet/Formats/Datatable/DataTableWriter.cs` (typed writer — CF-01 / CF-04)

**Analog:** `UtinniCoreDotNet/Formats/Iff/IffWriter.cs` (Phase 9 COMPOSES on this — does NOT re-implement chunk framing)

**Build pattern** (RESEARCH.md § "Build a MutableIffDocument from a MutableDataTableDocument" lines 783-803):
```csharp
public byte[] Serialize()
{
    var dtii = MutableIffNode.NewContainer("FORM", "DTII");
    var ver  = MutableIffNode.NewContainer("FORM", Version);   // "0000" or "0001"
    dtii.AddChild(ver);
    ver.AddChild(BuildColsChunk());
    ver.AddChild(BuildTypeChunk());
    ver.AddChild(BuildRowsChunk());
    var doc = new MutableIffDocument(dtii);
    return IffWriter.Write(doc);   // Phase 8's writer: BE chunk headers, length roll-up, 64 MB cap
}
```

**Deviation:** Phase 9 builds a fresh `MutableIffDocument` per save (not a long-lived doc). The per-cell hybrid-DOM byte preservation lives inside the COLS/TYPE/ROWS chunk-body byte-writers, not in the `MutableIffNode` slice (which is at chunk granularity).

**Anti-pattern to avoid (RESEARCH.md § Pitfall 9):** `grep -c "dataGridView.Rows" DataTableWriter.cs` MUST be 0. Save iterates `MutableDataTableDocument.Rows` directly.

---

### `Utinni.Cli/Commands/RoundtripTabCommand.cs` (CLI verb — CF-02 / SC4 gate)

**Analog:** `D:/Code/Utinni/Utinni.Cli/Commands/RoundtripIffCommand.cs` lines 25-110+

**Imports + Verb attribute** (RoundtripIffCommand.cs lines 25-50):
```csharp
using System;
using System.IO;
using System.Linq;
using CommandLine;
using Newtonsoft.Json.Linq;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Datatable;   // NEW for Phase 9
using Utinni.Cli.Output;

namespace Utinni.Cli.Commands
{
    [Verb("roundtrip-tab", HelpText = "Parse → [optional mutate-cell | remove-row | remove-column] → serialize → re-parse a .tab; assert byte-exact untouched cells.")]
    public class RoundtripTabOptions
    {
        [Value(0, MetaName = "path", Required = true, HelpText = "Path to the .tab file.")]
        public string Path { get; set; }
        [Option("mutate-cell", HelpText = "row,col index pair, e.g. 3,5")]   public string MutateCell { get; set; }
        [Option("mutate-value", HelpText = "Replacement value for --mutate-cell (string; coerced per column type).")] public string MutateValue { get; set; }
        [Option("remove-row",    HelpText = "Row index to remove.")]                        public int? RemoveRow { get; set; }
        [Option("remove-column", HelpText = "Column index or name to remove.")]             public string RemoveColumn { get; set; }
    }
```

**Run() exit-code matrix** (RoundtripIffCommand.cs lines 79-110 — port VERBATIM):
```csharp
public static int Run(RoundtripTabOptions o)
{
    // FileNotFound → 3; IffParseException/DataTableParseException/IOException → 2; UsageError → 1.
    if (!File.Exists(o.Path)) return JsonOutput.EmitError("roundtrip-tab", "FileNotFound", "...", exitCode: 3);
    try
    {
        byte[] original = File.ReadAllBytes(o.Path);
        IffDocument originalDoc;
        using (var ms = new MemoryStream(original, writable: false)) { originalDoc = IffReader.Read(ms); }
        var mutDoc = MutableIffDocument.FromDocument(originalDoc, original);
        var dtDoc  = DataTableDocument.FromIff(mutDoc);
        // ... optional mutate/remove command via DatatableEditCommands ...
        byte[] roundtripped = new DataTableWriter(/* mutable model */).Serialize();
        // Assert byte-exact-on-untouched-cells; report JSON envelope via JsonOutput.
    }
    catch (DataTableParseException ex) { return JsonOutput.EmitError(...); }
}
```

---

### `Utinni.Cli/Program.cs` (MODIFY — add verb to MapResult)

**Analog:** self (Program.cs lines 43-57)

**Exact extension point** (lines 43-57):
```csharp
return parser.ParseArguments<
        Commands.ParseTreOptions,
        Commands.ListObjectsOptions,
        Commands.InspectIffOptions,
        Commands.DecodeIffOptions,
        Commands.RoundtripIffOptions,
        Commands.RoundtripTabOptions,     // ← ADD THIS LINE
        Commands.ValidatePluginOptions>(args)
    .MapResult(
        (Commands.ParseTreOptions o)       => Commands.ParseTreCommand.Run(o),
        // ... existing lines ...
        (Commands.RoundtripIffOptions o)   => Commands.RoundtripIffCommand.Run(o),
        (Commands.RoundtripTabOptions o)   => Commands.RoundtripTabCommand.Run(o),   // ← ADD THIS LINE
        (Commands.ValidatePluginOptions o) => Commands.ValidatePluginCommand.Run(o),
        errs => 1);
```

---

### `UI/Forms/FormDatatableEditor.cs` (TJT singleton form)

**Analog:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` (port VERBATIM with DataGridView replacing IffChunkTree)

**Hide-not-dispose intercept** (FormIffEditor.cs lines 1569-1596 — port VERBATIM, RESEARCH § Pitfall 5):
```csharp
private void FormDatatableEditor_FormClosing(object sender, FormClosingEventArgs e)
{
    try { /* TrySaveSettings — ini width/height/splitterDistance/findReplaceVisible/editCommentRows/looseOverrideDir */ }
    catch { /* best-effort */ }
    if (e.CloseReason == CloseReason.UserClosing)
    {
        e.Cancel = true;
        Hide();
    }
}
```

**ProcessCmdKey shortcuts** (FormIffEditor.cs lines 271-300 — port + extend with Ctrl+F / Ctrl+H / F3 / Shift+F3):
```csharp
protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
{
    if (keyData == (Keys.Control | Keys.Z)) { if (controller != null && controller.CanUndo) controller.Undo(); return true; }
    if (keyData == (Keys.Control | Keys.Y)) { if (controller != null && controller.CanRedo) controller.Redo(); return true; }
    if (keyData == (Keys.Control | Keys.S)) { /* Save-in-place if LooseFile, else SaveAs */ return true; }
    if (keyData == (Keys.Control | Keys.F)) { ToggleFindPane(); return true; }    // NEW
    if (keyData == (Keys.Control | Keys.H)) { ToggleReplacePane(); return true; } // NEW
    if (keyData ==  Keys.F3)                { FindNext();        return true; }   // NEW
    if (keyData == (Keys.Shift   | Keys.F3)){ FindPrev();        return true; }   // NEW
    return base.ProcessCmdKey(ref msg, keyData);
}
```

**LoadDocument shape** (FormIffEditor.cs lines 216-232 — port VERBATIM):
```csharp
public void LoadDocument(DataTableDocument dtDoc, OpenSource source, string displayName)
{
    if (dtDoc == null) throw new ArgumentNullException("dtDoc");
    this.dtDocument = dtDoc;
    this.controller = new DatatableEditController(dtDoc.Mutable);
    this.controller.EditApplied += OnEditApplied;
    this.Source = source ?? OpenSource.Unknown.Instance;
    this.displayName = displayName;
    this.rootTypeId = "DTII";   // always DTII for Phase 9
    this.lastSavedPath = null;
    gridSurface.BindMutable(dtDoc);   // NEW — replaces iffChunkTree.LoadMutable
    btnSave.Enabled = true;
    UpdateUndoRedoState();
    UpdateDirtyVisuals();
    RefreshSaveMenuEnabledState();
    RefreshReloadButtonState();
}
```

**OnEditApplied (refresh hook)** (FormIffEditor.cs lines 306-317 — port + add NeedsReviewCount → Save▾ disable):
```csharp
private void OnEditApplied(object sender, EventArgs e)
{
    gridSurface.RefreshMutable(dtDocument);
    DecorateDirtyCells();
    UpdateUndoRedoState();
    UpdateDirtyVisuals();
    RefreshSaveMenuEnabledState();   // re-evaluates NeedsReviewCount → all-Save-items-disabled
}
```

**UpdateDirtyVisuals** (FormIffEditor.cs lines 359-374 — port VERBATIM):
```csharp
private void UpdateDirtyVisuals()
{
    bool dirty = controller != null && controller.IsDirty;
    lblDirty.Text = dirty ? "Unsaved changes" : "";
    SetTitle(dirty ? "●" : null);
}
private const string BaseTitle = "Datatable Editor";   // ← Phase 9 title
```

**BuildSaveMenu + RefreshSaveMenuEnabledState** (FormIffEditor.cs lines 896-1019 — port VERBATIM; EXTEND `RefreshSaveMenuEnabledState` with the NeedsReviewCount gate):
```csharp
// In RefreshSaveMenuEnabledState (after the existing isLoose/isTre/isUnknown branches):
bool blockedByCascade = controller != null && controller.NeedsReviewCount > 0;
string cascadeTooltip = "Resolve " + controller.NeedsReviewCount + " cell(s) that need review before saving.";

if (miSaveInPlace      != null) { miSaveInPlace.Enabled      &= !blockedByCascade; if (blockedByCascade) miSaveInPlace.ToolTipText      = cascadeTooltip; }
if (miSaveLooseOverride != null){ miSaveLooseOverride.Enabled &= !blockedByCascade; if (blockedByCascade) miSaveLooseOverride.ToolTipText = cascadeTooltip; }
if (miSaveAs           != null) { miSaveAs.Enabled           &= !blockedByCascade; if (blockedByCascade) miSaveAs.ToolTipText           = cascadeTooltip; }
if (miPatchLive        != null) { miPatchLive.Enabled        &= !blockedByCascade; if (blockedByCascade) miPatchLive.ToolTipText        = cascadeTooltip; }
if (miRepackTre        != null) { miRepackTre.Enabled        &= !blockedByCascade; if (blockedByCascade) miRepackTre.ToolTipText        = cascadeTooltip; }
btnSave.Enabled = hasDoc && !blockedByCascade;   // top-level button too (UI-SPEC R-04)
```

**OpenFromTreEntry** (FormIffEditor.cs lines 1416-1460 — port VERBATIM, replacing the IffReader.Read path with a chained DataTableDocument.FromIff):
```csharp
public void OpenFromTreEntry(byte[] payload, string resolvedArchivePath, string logicalPath, long archiveLocalOffset)
{
    if (payload == null) { lblStatus.Text = "TRE entry has no payload to open."; lblStatus.ForeColor = Color.Red; return; }
    try
    {
        IffDocument iffDoc;
        using (var ms = new MemoryStream(payload, writable: false)) { iffDoc = IffReader.Read(ms); }
        MutableIffDocument mut = MutableIffDocument.FromDocument(iffDoc, payload);
        DataTableDocument dt   = DataTableDocument.FromIff(mut);    // NEW — typed wrap
        OpenSource src = TreRecordIndexResolver.ResolveOrUnknown(resolvedArchivePath, archiveLocalOffset, logicalPath);
        string name = logicalPath ?? Path.GetFileName(resolvedArchivePath ?? "");
        LoadDocument(dt, src, name);
        /* lines 1444-1453: status copy on success/Unknown */
    }
    catch (Exception ex) { lblStatus.Text = "TRE hand-off failed: " + ex.Message; lblStatus.ForeColor = Color.Red; }
}
```

**Reload-status badge (CF-05 LOCKED copy)** — adapt FormIffEditor.cs lines 1464-1495 BUT use the LOCKED CF-05 wording always (datatables are ALWAYS tier (b)):
```csharp
private void OnReloadClicked(object sender, EventArgs e)
{
    // Datatables ALWAYS take the tier-(b) PendingNextSceneChange branch — no other tier is reachable.
    lblStatus.Text = "Datatables re-resolve on the next scene change. Trigger one via TJT's chat-command load.";
    lblStatus.ForeColor = Colors.Font();
    // Optional: accent-pulse the reload-badge per UI-SPEC § States (1s timer).
    // DO NOT call ClientReloadDispatcher.Dispatch — the locked CF-05 contract says no scene-setup trigger.
}
```

**CreateSettings (mirrors FormIffEditor.cs lines 234-242):**
```csharp
private void CreateSettings()
{
    ini.AddSetting("DatatableEditor", "width", "1200", UtINI.Value.Types.VtInt);
    ini.AddSetting("DatatableEditor", "height", "760", UtINI.Value.Types.VtInt);
    ini.AddSetting("DatatableEditor", "splitterDistance", "0", UtINI.Value.Types.VtInt);
    ini.AddSetting("DatatableEditor", "findReplaceVisible", "false", UtINI.Value.Types.VtBool);
    ini.AddSetting("DatatableEditor", "editCommentRows", "false", UtINI.Value.Types.VtBool);
    ini.AddSetting("DatatableEditor", "looseOverrideDir", "", UtINI.Value.Types.VtString);
}
```

---

### `Plugin.cs` (MODIFY — register FormDatatableEditor)

**Analog:** self (Plugin.cs lines 75-82 — Phase 8's FormIffEditor registration, EXACT verbatim pattern):
```csharp
// EXISTING (DO NOT TOUCH):
try { forms.Add(new FormTreBrowser(this)); } catch (Exception ex) { Log.Info("...FormTreBrowser..."); }
try { forms.Add(new FormIffEditor(this)); }  catch (Exception ex) { Log.Info("...FormIffEditor..."); }

// ADD (Phase 9 — identical 7-line isolation block):
try { forms.Add(new FormDatatableEditor(this)); }
catch (Exception ex) { Log.Info("Failed to create FormDatatableEditor; Datatable Editor will be unavailable: " + ex); }
```

---

### `UI/Controls/ThemedDataGridView.cs` (themed grid wrapper)

**Analog:** `UI/Controls/IffChunkTree.cs` (same TJT.UI.Controls namespace + UserControl-with-themed-WinForms-control pattern)

**Imports** (IffChunkTree.cs lines 25-30):
```csharp
using System;
using System.Windows.Forms;
using UtinniCoreDotNet.Formats.Iff;       // (drop for Phase 9; not needed)
using UtinniCoreDotNet.UI.Controls;
using UtinniCoreDotNet.UI.Theme;
namespace TJT.UI.Controls
```

**Constructor token application** — apply the ~25 properties from 09-UI-SPEC § ThemedDataGridView token map (BackgroundColor, GridColor, BorderStyle, EnableHeadersVisualStyles=false, ColumnHeaders*, RowHeaders*, DefaultCellStyle, AlternatingRowsDefaultCellStyle, RowTemplate.Height=22, CellBorderStyle, MultiSelect=true, SelectionMode=CellSelect, ScrollBars=Both). All values from `Colors.*()` — no raw ARGB.

**Deviation:** This is a TJT-side control (UI-SPEC assumption #1), NOT framework-side. Phases 10/11 inherit. If framework promotion becomes needed later, follow the same code-motion pattern Phase 8 used for IFF primitives (no API change).

---

### `UI/Forms/FormAddColumnDialog.cs` (per-call modal — Add column)

**Analog:** `UI/Forms/FormFourCcDialog.cs` (Phase 8 — small input dialog with `using (var dlg = new FormFourCcDialog(...))` lifecycle)

**Lifecycle** (RESEARCH § Pitfall 6): per-call `using (var dlg = new FormAddColumnDialog(...)) { dlg.ShowDialog(this); }`. Default WinForms dispose-on-close is CORRECT. Do NOT apply hide-not-dispose.

---

### `UI/Forms/FormTypeChangeCascadeDialog.cs` (per-call modal — D-04 cascade resolution)

**Analog:** `UI/Forms/FormSaveConfirmDialog.cs` (per-call modal lifecycle + UtinniForm host) + embedded `ThemedDataGridView` (NEW)

**Lifecycle pattern** (FormSaveConfirmDialog.cs lines 33-56 XML comment is the contract spec):
```csharp
// Per-call: using (var dlg = new FormTypeChangeCascadeDialog(...)) { dlg.ShowDialog(this); }
// Default WinForms dispose-on-close is CORRECT. NOT a singleton.
```

**Deviation:** carries an embedded `ThemedDataGridView` listing affected cells (UI-SPEC assumption #6 — own dialog, NOT a FormSaveConfirmDialog reuse).

---

### `UI/Forms/FormCsvImportPreviewDialog.cs` (per-call modal — CSV preview)

**Analog:** `UI/Forms/FormSaveConfirmDialog.cs` (per-call modal lifecycle) + embedded `ThemedDataGridView` (per-column diff)

**Lifecycle:** same as FormTypeChangeCascadeDialog. UI-SPEC assumption #7 — own dialog.

---

### `Saving/DatatableSaveTargets.cs` (thin composition shim)

**Analog:** `Saving/IffSaveTargets.cs` (Phase 8 — Phase 9 REUSES VERBATIM via composition; this file is a tiny adapter)

**Shape:**
```csharp
public static class DatatableSaveTargets
{
    // Build bytes once via the typed writer, then dispatch through Phase 8's existing IffSaveTargets.
    public static async Task<IffSaveTargets.SaveResult> SaveLooseOverride(
        MutableDataTableDocument dt, OpenSource source, string resolvedRoot, string looseOverrideSubDir)
    {
        var mIff = new DataTableWriter(dt).BuildMutableIff();
        return await IffSaveTargets.SaveLooseOverride(mIff, source, resolvedRoot, looseOverrideSubDir);
    }
    // ditto for SaveToPath, SaveInPlace
    public static async Task<TreRepackSaveTarget.TreRepackResult> RepackIntoSourceTre(
        MutableDataTableDocument dt, OpenSource.TreArchive ta, bool createBackup)
    {
        byte[] bytes = new DataTableWriter(dt).Serialize();
        return await TreRepackSaveTarget.Apply(ta, bytes, createBackup);
    }
}
```

**This file is < 100 lines.** All save plumbing is reused from Phase 8 — no new save-mode logic, no new path-defense, no new repack orchestration.

---

### `Utinni.Cli.Tests/Infrastructure/DataTableFixtureBuilder.cs` (synthetic .tab fixture builder)

**Analog:** `Utinni.Cli.Tests/Infrastructure/IffBuilder.cs` + `TreFixtureBuilder.cs` (same composable-builder shape)

**Per RESEARCH.md Assumption A6 + § Wave 0 Gaps:** all `.tab` fixtures synthesized via this builder (no on-disk binary fixtures checked in). Build helpers: `BuildV0Minimal()`, `BuildV1Minimal()`, `BuildV1AllTypes()`, `BuildV1WithDefaultsAndEnums()`, `BuildV1WithComment()`, `BuildV1CombatDataTableLike()`, `BuildV1EmptyTable()`.

---

## Shared Patterns

### Pattern S-1: Hide-not-dispose for `GetForms()`-registered singletons

**Source:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` lines 1569-1596

**Apply to:** `FormDatatableEditor` from the start (mandatory per memory `singleton-form-hide-not-dispose` + RESEARCH § Pitfall 5). Do NOT apply to per-call modals (`FormAddColumnDialog`, `FormTypeChangeCascadeDialog`, `FormCsvImportPreviewDialog`) — Pitfall 6.

```csharp
private void FormX_FormClosing(object sender, FormClosingEventArgs e)
{
    if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
    // Editor-host shutdown → fall through, dispose normally.
}
```

### Pattern S-2: MEF SPI try/catch isolation in Plugin.cs

**Source:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs` lines 62-82

**Apply to:** `FormDatatableEditor` registration. Identical 7-line block; failure must NOT take down TJT.

### Pattern S-3: Save▾ provenance gating (RefreshSaveMenuEnabledState)

**Source:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` lines 933-1019

**Apply to:** `FormDatatableEditor.RefreshSaveMenuEnabledState`. Port VERBATIM with the additional `NeedsReviewCount > 0 → disable all + cascade tooltip` gate (UI-SPEC R-04).

### Pattern S-4: TRE Browser hand-off (off-UI-thread payload resolve + BeginInvoke + FindOrCreate)

**Source:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` lines 185-248

**Apply to:** new `OnOpenInDatatableEditor` + `FindOrCreateDatatableEditor` methods inside FormTreBrowser.cs. Visibility predicate adds an extension/root-tag check beyond `_miOpenInIffEditor`.

### Pattern S-5: Per-call modal lifecycle

**Source:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSaveConfirmDialog.cs` lines 33-56 (XML contract comment)

**Apply to:** `FormAddColumnDialog`, `FormTypeChangeCascadeDialog`, `FormCsvImportPreviewDialog`, AND the reused `FormSaveConfirmDialog` for column-reorder/delete safety-net (UI-SPEC assumption #5). Always `using (var dlg = new ...) { dlg.ShowDialog(this); }`. NEVER apply hide-not-dispose.

### Pattern S-6: Hybrid mutable DOM (CF-04)

**Source:** `D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/MutableIffNode.cs` lines 156-296, 484-495

**Apply to:** `MutableDataTableCell`, `MutableDataTableRow`, `MutableDataTableColumn`. Each owns an `originalSlice` field captured at FromIff time; dirty-mutation clears the slice and propagates up. SC4 byte-exact-on-untouched is structural, not a test-only invariant.

### Pattern S-7: CLI verb JSON envelope + exit-code matrix

**Source:** `D:/Code/Utinni/Utinni.Cli/Commands/RoundtripIffCommand.cs` lines 25-110+, `Utinni.Cli/Output/JsonOutput.cs`

**Apply to:** `RoundtripTabCommand`. FileNotFound → 3; parse/IO error → 2; UsageError → 1. Generic `System.Exception` NOT caught.

### Pattern S-8: MIT-license + Allman-brace + 4-space + `// ToDo` (no colon)

**Source:** every file in the repo (`.planning/codebase/CONVENTIONS.md`)

**Apply to:** every new `.cs` file in the phase. The 23-line MIT header block (`/** ... **/` doubled-asterisk close) is mandatory verbatim.

### Pattern S-9: Explicit `<Compile Include>` in TheJawaToolboxDotNet.csproj

**Source:** existing `<Compile Include>` entries in `TheJawaToolboxDotNet.csproj` (old-style csproj, non-SDK)

**Apply to:** every new `.cs` under `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/`. WinForms `.cs` files use `<SubType>Form</SubType>`; `.Designer.cs` partials use `<DependentUpon>`. `UtinniCoreDotNet.Tests`, `Utinni.Cli`, `Utinni.Cli.Tests` are SDK-style (auto-glob) — no csproj edit needed.

### Pattern S-10: No `.resx` for new forms

**Source:** memory `feedback_dotnet_build_msbuild_resources` + Phase 8 forms (FormIffEditor, FormFourCcDialog, FormSaveConfirmDialog all ship WITHOUT `.resx`)

**Apply to:** all new Phase 9 forms. Hand-written `.Designer.cs`; build with VS2026 MSBuild (NOT `dotnet build`).

---

## No Analog Found

| File | Role | Data Flow | Reason / Substitute |
|------|------|-----------|---------------------|
| `UtinniCoreDotNet/Formats/Datatable/DataTableColumnType.cs` | type-spec parser + MangleValue | pure-function | Port from `swg-client-v2/.../DataTableColumnType.cpp:84-232` + `:382-473` (cited in RESEARCH § Pattern, Pitfall 2, Open Question 2). NO in-repo C# analog. |
| `UtinniCoreDotNet/Formats/Datatable/DataTableHashCrc.cs` | SOE CRC variant | pure-function | Port from `swg-client-v2/.../sharedFoundation/Crc.{h,cpp}` (RESEARCH Open Question 1 — exact algorithm needs verification at port time). NO in-repo analog. |
| `UtinniCoreDotNet/Formats/Datatable/DataTableCellValue.cs` | discriminated-union value | pure-function | Derive from `DataTableCell.h`. C# pattern: sealed class with `IntValue` / `FloatValue` / `StringValue` subclasses or struct with discriminator + union fields. Planner picks. |
| `Saving/DatatableCsvSerializer.cs` | CSV parser/writer + per-cell delta diff | transform / file-I/O | NO in-repo CSV analog. RESEARCH § "Don't Hand-Roll" calls out: hand-roll a < 150-line RFC-4180-ish parser (UTF-8 BOM + double-quote escape); use UI-SPEC assumption #8 framing (bare-`name` header, optional `#`-comment second row). |
| `UI/Controls/DatatableHashStringEditor.cs` | floating hash preview label anchored to grid cell during edit | UI-only | No in-repo floating-overlay analog. UI-SPEC § Per-type cell widget contract row `DT_HashString` is the spec (Consolas 9pt, `Colors.FontDisabled()`, anchored on CellBeginEdit, disposed on CellEndEdit). |
| `UI/Controls/DatatableNumericUpDownEditingControl.cs` | UtinniNumericUpDown adapted to `IDataGridViewEditingControl` BCL interface | UI-only | BCL pattern (no in-repo analog); standard DataGridView `EditingControlShowing` swap-in. |

---

## High-Leverage Reuse Points (planner MUST NOT re-implement)

These five items represent the bulk of Phase 8's shipped value that Phase 9 inherits at near-zero cost. Re-implementing any of them is an anti-pattern (cost without benefit; introduces drift from the hardened Phase-8 contract).

### 1. `TheJawaToolboxDotNet/Saving/IffSaveTargets.cs` — save modes 1 / 2 / 4
**What it does:** `SaveLooseOverride` (mode 1) + `SaveToPath` (Save-As, mode 2) + `SaveInPlace` (sub-mode of 1 — overwrites the source loose file). Each handles `Flush(true)` MEDIUM-9 barrier, atomic write, root containment via `LooseOverridePath.Resolve`, off-UI-thread `Task` wrap, and structured `SaveResult` return.
**Phase 9 usage:** `DatatableSaveTargets` is a < 100-line composition shim that calls `new DataTableWriter(mDt).BuildMutableIff() → IffSaveTargets.{SaveLooseOverride,SaveToPath,SaveInPlace}`. **No new save plumbing whatsoever.**
**Source:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/IffSaveTargets.cs` (full file; ~280 lines)

### 2. `TheJawaToolboxDotNet/Saving/TreRepackSaveTarget.cs` — save mode 4 (.tre repack)
**What it does:** Full `.tre` repack with CRC/TOC rebuild, temp-write + `File.Replace` atomic swap, optional timestamped backup via `TreBackupPath`, locked-archive probe via `TreRepackLock`, parse-back sanity gate via `TreFile.Open`. Returns structured `TreRepackResult` enum (Replaced / BackedUpThenReplaced / RefusedClientHoldsArchive_LooseOverrideRecommended / etc.).
**Phase 9 usage:** `DatatableSaveTargets.RepackIntoSourceTre(dt, ta, createBackup)` calls `TreRepackSaveTarget.Apply(ta, dataTableWriter.Serialize(), createBackup)`. The repack does not care that the bytes are DTII — any IFF bytes are equally valid. **No new repack code.**
**Source:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/TreRepackSaveTarget.cs` (full file; ~330 lines)

### 3. `TheJawaToolboxDotNet/UI/Forms/FormSaveConfirmDialog.cs` — risk-proportional confirm modal
**What it does:** Per-call destructive-action modal with caller-supplied heading, body (`Color.Red` per UI-SPEC), explicit accept-verb / cancel-verb captions, optional `showBackupCheckbox` slot, structured `ConfirmOutcome` + `BackupRequested` return. Per-call lifecycle (`using (var dlg = new ...)`).
**Phase 9 reuse:** UI-SPEC assumption #5 — REUSE for the column-reorder/delete safety-net with the `showBackupCheckbox` slot relabeled `Don't ask again this session`; also reuse for repack-confirm + discard-while-dirty (same as Phase 8). Three of Phase 9's four destructive modals are this same dialog.
**Source:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormSaveConfirmDialog.cs` (lines 33-100+)

### 4. `FormIffEditor.BuildSaveMenu` / `RefreshSaveMenuEnabledState` / `UpdateDirtyVisuals` / `ProcessCmdKey` / `OpenFromTreEntry`
**What they do:** The complete Save▾ split-button drop-down (5 items, 4 provenance-gated + Save-As always-enabled), the `OpenSource` pattern-match enable-state refresh (Loose / Tre / ClientMemory / Unknown × `inFlight` × `clientUp` × `blockedByCascade`), the `● `-prefix title + `Unsaved changes` lblDirty UpdateDirtyVisuals, the Ctrl+Z/Y/S keyboard intercept that pre-empts focused-control WndProc, and the TRE-Browser hand-off entry point.
**Phase 9 reuse:** `FormDatatableEditor` ports VERBATIM. The only deviation is the new `NeedsReviewCount > 0 → all-Save-disabled` gate in `RefreshSaveMenuEnabledState` (UI-SPEC R-04) and adding Ctrl+F / Ctrl+H / F3 / Shift+F3 to ProcessCmdKey.
**Source:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` lines 271-300 (ProcessCmdKey), 359-374 (UpdateDirtyVisuals), 896-1019 (BuildSaveMenu + RefreshSaveMenuEnabledState), 1416-1460 (OpenFromTreEntry), 1569-1596 (FormClosing hide-not-dispose)

### 5. Singleton-form hide-not-dispose intercept (Phase 8 smoke-discovered defect class)
**What it does:** On `CloseReason.UserClosing`, the form cancels close + `Hide()` instead of disposing. This is the canonical pattern for plugin-registered `GetForms()` editors. Phase 8 hit this defect TWICE during live-SWG smoke (FormIffEditor commit `b899504`, FormTreBrowser defensive commit `ce2a0a4`) — second open of a singleton form throws `ObjectDisposedException` at `Form.CreateHandle` without this guard.
**Phase 9 reuse:** Apply from the START (RESEARCH § Pitfall 5; user-memory mandate). Add an xUnit test that constructs `FormDatatableEditor`, calls `OnFormClosing(CloseReason.UserClosing)`, asserts `!IsDisposed && !Visible`. **Do NOT apply to per-call modals** (Pitfall 6 — leak).
**Source:** memory `singleton-form-hide-not-dispose`; `FormIffEditor.cs` lines 1569-1596

---

## Binary-Compat Audit (per memory `feedback_caller_attrs_binary_compat`)

### Safe additive surface (no rebuild of pre-built plugins required)
- All NEW files under `UtinniCoreDotNet/Formats/Datatable/` — new namespace, new types, no existing public API touched.
- All NEW files under `UtinniCoreDotNet/Editing/Datatable*` — new namespace adjacent to existing `IffEditController`, no signature change.
- All NEW files under `Utinni.Cli/Commands/RoundtripTab*` — additive verb; existing `RoundtripIffCommand` unchanged.
- All NEW files under `The Jawa Toolbox/TheJawaToolboxDotNet/UI/` and `Saving/` — plugin-internal; no cross-plugin API.

### Modified files (audit on every plan)
- **`Utinni.Cli/Program.cs`** — additive (one new type to `ParseArguments<...>`; one new `MapResult` lambda). Not a public-API consumer.
- **`The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs`** — additive (one new `forms.Add(...)` line + try/catch isolation). Same shape as Phase 8's FormIffEditor addition.
- **`The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs`** — additive (one new `ToolStripMenuItem` + click handler for D-10.3 "Switch to typed datatable view"). Existing public surface (`LoadDocument`, `OpenFromTreEntry`, etc.) **must NOT change signature**. If any helper method gains a parameter (e.g. `OpenFromTreEntry` adding a default-argument), rebuild **every** consumer DLL in the same commit per memory `feedback_caller_attrs_binary_compat`.
- **`The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs`** — additive (one new `ToolStripMenuItem` + click handler + `FindOrCreateDatatableEditor` helper for D-10.2). Same constraint: no existing public-method signature change.

### Hot-zone (DO NOT TOUCH)
- `UtinniCoreDotNet/Formats/Iff/{IffReader,IffWriter,IffDocument,IffChunk,MutableIffDocument,MutableIffNode}.cs` — all public methods consumed by pre-built `FormIffEditor` and any external Wave-2 plugin. **Phase 9 composes on these — never mutates them.**
- `UtinniCoreDotNet/Editing/IffEditController.cs` — Phase 8's controller. Phase 9 builds an ADJACENT `DatatableEditController`; the IFF controller stays untouched.
- `UtinniCoreDotNet/Saving/{LooseOverridePath,ReloadAssetClassifier,TreBackupPath,TreRepackLock}.cs` — Phase 8 helpers, consumed verbatim.
- `UtinniCoreDotNet/Formats/Tre/{TreFile,TreWriter,TreRecordIndexResolver}.cs` — Phase 7/8 primitives, consumed verbatim.

---

## Metadata

**Analog search scope:**
- `D:/Code/Utinni/UtinniCoreDotNet/Formats/` (Iff/, Tre/ — Phase 4/7/8 primitives)
- `D:/Code/Utinni/UtinniCoreDotNet/Editing/` (Phase 8 controller)
- `D:/Code/Utinni/UtinniCoreDotNet/Saving/` (Phase 8 save helpers)
- `D:/Code/Utinni/Utinni.Cli/` (Phase 4/7/8 verbs) + `Utinni.Cli.Tests/Infrastructure/` (fixture builders)
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/` (Phase 7/8 TJT host)

**Files scanned:** ~45 (10 framework IFF + 4 framework editing + 8 framework saving + 6 CLI + 14 TJT UI / saving + 3 fixture-builders)

**Pattern extraction date:** 2026-05-29

**Confidence:** HIGH for every Phase-8 analog (one-month-old code, all signatures verified by direct read). MEDIUM for the C++→C# port files (`DataTableColumnType.cs`, `DataTableHashCrc.cs`, `DataTableCellValue.cs`, `DataTableDocument.cs::FromIff` typed-cell decoder) — analog is `swg-client-v2` C++ source cited at file:line in RESEARCH.md, not an in-repo C# file. RESEARCH Assumption A1 (enum syntax) and Open Questions 1 (CRC) + 2 (PackedObjVars/BitVector validator depth) must be resolved at port time.

## PATTERN MAPPING COMPLETE
