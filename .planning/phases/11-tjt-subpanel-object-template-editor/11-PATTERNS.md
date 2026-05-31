# Phase 11: TJT subpanel — Object Template Editor - Pattern Map

**Mapped:** 2026-05-30
**Files analyzed:** 14 (new/modified)
**Analogs found:** 14 / 14 (all have a strong in-repo analog — Phase 11 adds essentially zero new infrastructure)

> **Project instructions:** No `./CLAUDE.md` and no `.claude/skills/` or `.agents/skills/` exist in the working tree (verified). Conventions come from `.planning/codebase/CONVENTIONS.md` + auto-memory. Every new file MUST carry the MIT provenance header + the swg-client-v2 "layout study only, no code/identifiers copied" comment (see `ObjectTemplateDecoder.cs` lines 1–30 as the canonical template).
>
> **Primary structural template:** the **Datatable lineage** (IFF-based, Phase 9), NOT the String-table lineage (flat-format, Phase 10). Object templates ARE IFF, so byte-exactness is free from `MutableIffDocument`/`IffWriter` — the same path the datatable model rides. Use `FormDatatableEditor` / `DatatableEditController` / `MutableDataTableDocument` / `DatatableSaveTargets` as the clone targets, not their String-table siblings.

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateParam*.cs` (typed param value model: scalar union + delta + hex-fallback marker) | model | transform | `Formats/Datatable/MutableDataTableCell.cs` + `Formats/Datatable/DataTableCellValue.cs` | role-match |
| `UtinniCoreDotNet/Formats/ObjectTemplate/MutableObjectTemplate.cs` (mutable model over `MutableIffDocument`; promote/revert/edit ops) | model | transform | `Formats/Datatable/MutableDataTableDocument.cs` | exact (both IFF-mutable models) |
| `UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateResolver.cs` (D-01 DERV-chain walk → effective merged view) | service | request-response | *(NEW — no analog; see § No Analog Found)*; resolution primitive = `Formats/Tre/TrePayloadResolver.cs` | partial (resolver is new; the cross-IFF fetch it calls is reused) |
| `UtinniCoreDotNet/Formats/ObjectTemplate/EffectiveField.cs` (row: name + effective value + origin + ancestor breadcrumb) | model | transform | `Formats/Decoders/ObjectTemplateDecoder.cs` → `ObjectTemplateField` (already has `InheritedFrom` slot) | role-match |
| `UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateWriter.cs` (byte-exact serialize via `IffWriter`) | service | transform | `Formats/Datatable/MutableDataTableDocument.BuildMutableIff` + `DataTableWriter` | role-match |
| `UtinniCoreDotNet/Editing/ObjectTemplateEditController.cs` (editor-local undo/redo) | controller | event-driven | `Editing/DatatableEditController.cs` | exact (clone target) |
| `UtinniCoreDotNet/Editing/ObjectTemplateEditCommands.cs` + `IObjectTemplateEditCommand.cs` | controller | event-driven | `Editing/DatatableEditCommands.cs` + `IDatatableEditCommand.cs` | exact |
| `Utinni.Cli/Commands/Roundtrip*Command.cs` (extend `roundtrip-iff` OR add `roundtrip-ot`) | CLI command | request-response | `Utinni.Cli/Commands/RoundtripIffCommand.cs` (+ `RoundtripTabCommand.cs` for the param-slice variant) | exact |
| `.../TheJawaToolboxDotNet/UI/Forms/FormObjectTemplateEditor.cs` (+ `.Designer.cs`) | component (host form) | request-response | `.../UI/Forms/FormDatatableEditor.cs` | exact (clone target) |
| `.../TheJawaToolboxDotNet/Saving/ObjectTemplateSaveTargets.cs` (<100-line shim) | service | file-I/O | `.../Saving/DatatableSaveTargets.cs` | exact (clone target) |
| `.../TheJawaToolboxDotNet/Plugin.cs` (register 5th SubPanel form) | config | — | `Plugin.cs` lines 87–106 (Datatable/Stringtable registration) | exact |
| `.../TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` (add "Switch to typed object-template view") | component (hand-off) | event-driven | `FormIffEditor.cs` lines 661–684, 1592–1618 ("Switch to typed datatable view") | exact |
| `.../TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (add "Open in Object Template Editor") | component (hand-off) | event-driven | `FormTreBrowser.cs` lines 283–354 (`OnOpenInDatatableEditor`) | exact |
| `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` | config | — | **VERIFY-ONLY** — already classifies object-template `.iff` (SHOT/STOT/SBOT + conservative `.iff` fallback) → `PendingNextSceneChange` | exact (no change expected) |

---

## Pattern Assignments

### `UtinniCoreDotNet/Formats/ObjectTemplate/MutableObjectTemplate.cs` (model, transform)

**Analog:** `UtinniCoreDotNet/Formats/Datatable/MutableDataTableDocument.cs`

**Provenance header pattern** — copy verbatim from `ObjectTemplateDecoder.cs` lines 1–30 (MIT block + the swg-client-v2 layout-study comment). The reference policy (`project_swg_client_v2_reference.md`) is LOCKED: study layout only, copy no identifiers/code/comments.

**Mutable-model-over-IFF pattern** (`MutableDataTableDocument.cs` lines 54–95) — hold a `MutableIffDocument SourceIff` back-reference, a version string, the structural collection (datatable: columns+rows; OT: the version-form's param-chunk set), and a private `IsDirty`:
```csharp
public sealed class MutableDataTableDocument
{
    public string Version { get; }
    public IList<MutableDataTableColumn> Columns { get; }
    public IList<MutableDataTableRow> Rows { get; }
    public MutableIffDocument SourceIff { get; internal set; }  // the byte source, retained for slice lookup
    public bool IsDirty { get; private set; }
    internal void MarkDirty() { IsDirty = true; }
```
**For OT:** the parallel is `string Version` (the digit-tagged version-form tag, e.g. "0000"), an ordered list of mutable param chunks (each a NUL name + self-describing typed value), and the `MutableIffDocument SourceIff` captured at parse. The version-form **param-count chunk is machine-managed** (D-04) — the writer rewrites it from the live param count, exactly as `MutableDataTableDocument.BuildColsPayload` writes `Columns.Count` (lines 199–213), never a user-edited field.

**Structural-mutation-keeps-shape-in-lockstep pattern** (`MutableDataTableDocument.RemoveColumnAt` lines 126–144) — when you remove a unit, also fix the bookkeeping in the same call + `MarkDirty()`. **For OT:** `RemoveOverride(name)` deletes the local param chunk AND the writer re-derives the count; `AddOverride(name, value)` appends a chunk AND the writer re-derives the count.

**Byte-exact re-serialize pattern** (`MutableDataTableDocument.BuildMutableIff` lines 186–196) — materialize a fresh container tree and hand it to `IffWriter`:
```csharp
public MutableIffDocument BuildMutableIff()
{
    var dtiiRoot = MutableIffNode.NewContainer("FORM", "DTII");
    MutableIffNode versionForm = dtiiRoot.AddContainer("FORM", Version);
    versionForm.AddLeaf("COLS", BuildColsPayload());
    versionForm.AddLeaf("TYPE", BuildTypePayload());
    versionForm.AddLeaf("ROWS", BuildRowsPayload());
    return new MutableIffDocument(dtiiRoot);
}
```
**OT caveat — prefer hybrid-DOM in-place mutation over full rebuild for untouched-byte-exactness.** Because the CF-02 gate asserts byte-exact identity for **untouched params**, the OT writer should mutate the captured `MutableIffDocument` leaves in place (the Phase 8 hybrid-DOM path) for untouched params rather than rebuilding every chunk from a typed model (a full rebuild would re-encode and could perturb untouched bytes). The datatable rebuilds because it has no "untouched-subset" guarantee at the cell level; OT's CF-02 demands the Phase-8 in-place idiom — see the `MutableIffDocument.FromDocument` capture (`MutableIffDocument.cs` lines 80–128) and the CLI `--mutate-leaf` idiom (`RoundtripIffCommand.cs` lines 157–251) that already proves untouched-leaf identity after a single-leaf edit.

---

### `UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateParam*.cs` (model, transform — the self-describing typed value decode/encode)

**Analog:** `Formats/Decoders/IffPayloadCursor.cs` (read primitives) + `Formats/Datatable/DataTableCellValue.cs` (value union shape)

**Cursor read pattern — the cursor ALREADY exposes every primitive the OT scalar decode needs** (`IffPayloadCursor.cs`): `ReadCString(Encoding.ASCII)` (lines 123–140), `ReadInt32Le()` (lines 62–71), **`ReadFloatLe()` (lines 99–116) — RESEARCH Open-Question #1 is RESOLVED: the LE float read exists**, `ReadBytes(int)` (lines 89–97), `Remaining` (line 56). The self-describing scalar decode (RESEARCH MUST-CONFIRM #2):
```csharp
// param chunk payload = [NUL name][int8 dataTypeTag]([int8 deltaType] for numeric)[value...]
var cursor = new IffPayloadCursor(paramLeaf.Data);   // NOTE: IffPayloadCursor is `internal sealed` — see binary-compat note
string fieldName = cursor.ReadCString(Encoding.ASCII);
byte dataTypeTag = /* read 1 byte */;                // 0 NONE,1 SINGLE,2 WEIGHTED_LIST,3 RANGE,4 DIE_ROLL
// Integer/Float ONLY: a delta byte (' '/'+'/'-') follows the tag. Bool/String/StringId do NOT carry it.
```
> **GAP — add an `int8` read helper.** `IffPayloadCursor` has `ReadInt32Le`/`ReadFloatLe`/`ReadBytes`/`ReadCString` but **no `ReadByte()`/`ReadInt8()`** for the 1-byte data-type and delta tags. Add a 1-line additive helper (binary-compat safe — `IffPayloadCursor` is `internal`, so no public-signature break). This is the single primitive Phase 11 must add to the read layer.

**Defensive-decode posture (RESEARCH-locked, UI-SPEC § Per-type value widget contract):** attempt the scalar decode for the leading tag; **if it does not consume `cursor.Remaining == 0` exactly, treat the whole param as `raw bytes (hex)` fallback** rather than guessing. This preserves byte-exactness and routes struct/weighted-list/dynamic-variable params to the Phase 8 hex leaf editor. Mirror the `ObjectTemplateDecoder` param loop (`ObjectTemplateDecoder.cs` lines 181–188) which already reads name + remaining bytes per param.

**Value-union shape** — clone `DataTableCellValue` (a discriminated value carrier) for the OT scalar union (bool/int/float/string/stringId/template-ref/enum/vector/trigger-volume + a `RawBytes` hex-fallback variant + the int8 delta byte preserved verbatim).

---

### `UtinniCoreDotNet/Formats/ObjectTemplate/ObjectTemplateResolver.cs` (service, request-response — D-01 inheritance walk)

**Analog:** NEW logic, but the cross-IFF fetch primitive is `Formats/Tre/TrePayloadResolver.cs`, and the per-template parse is `ObjectTemplateDecoder.Decode` (`ObjectTemplateDecoder.cs` lines 119–191).

**Base-resolution + graceful-degradation pattern** (`TrePayloadResolver.TryResolve` lines 52–130) — `TryResolve` returns **false** (NOT throw) for an `EnumerateOnly`/V6000 entry (lines 56–59) or a missing archive (lines 87–90); it only throws on path-traversal / integrity failures. This IS the D-01 degradation hook:
```csharp
if (TrePayloadResolver.TryResolve(baseDescriptor, out byte[] baseBytes))
{
    IffDocument baseDoc = /* IffReader.Read(baseBytes) */;
    ObjectTemplateView baseView = ObjectTemplateDecoder.Decode(baseDoc);  // local params + its own DERV
    // merge: a field T does NOT locally override → "inherited from <baseName>"; recurse on baseView.BaseTemplate
}
else
{
    // D-01 LOCKED degradation: render inherited rows as "unresolved base <name>"; NEVER throw, NEVER block the open.
}
```
**Effective-value semantics (RESEARCH MUST-CONFIRM #3 — replicate `if(!isLoaded()) return base->getXxx()`):** effective value of a field = the value from the nearest ancestor (including T) that has a *local* param chunk for it. `ObjectTemplateDecoder` already separates "local" fields (`InheritedFrom == "local"`, line 187) from the declared `@base` row (lines 137–149) — the resolver walks `ObjectTemplateView.BaseTemplate` (line 70) recursively.

**Origin marker reuses the existing slot** — `ObjectTemplateField.InheritedFrom` (`ObjectTemplateDecoder.cs` lines 50–51) is already "local" vs base-name; `EffectiveField` extends it to {`local override`, `inherited from <name>`, `unresolved base <name>`}.

---

### `UtinniCoreDotNet/Editing/ObjectTemplateEditController.cs` (controller, event-driven — CF-04 undo/redo)

**Analog:** `UtinniCoreDotNet/Editing/DatatableEditController.cs` — **clone, swapping the document type.**

**The Apply/Undo/Redo/netAppliedCount idiom** (`DatatableEditController.cs` lines 167–206) — copy verbatim with `MutableDataTableDocument` → `MutableObjectTemplate` and `IDatatableEditCommand` → `IObjectTemplateEditCommand`:
```csharp
public void Apply(IDatatableEditCommand command)
{
    command.Do(document);
    undoStack.Push(command);
    redoStack.Clear();
    netAppliedCount++;
    RaiseEditApplied();
}
public void Undo() { if (!CanUndo) return; var c = undoStack.Pop(); c.UndoOp(document); redoStack.Push(c); netAppliedCount--; RaiseEditApplied(); }
```
**Baseline-clean dirty semantics** (lines 118–119): `IsDirty => netAppliedCount > 0`; `MarkSaved()` (lines 218–233) resets the baseline after a save. Clone the `EditApplied` event (lines 157–161) → host re-binds grid + dirty visuals + undo/redo button states.

**CON-M-05 GUARD-RAIL (extra-load-bearing here):** the controller MUST NOT reference the scene `UndoRedoManager`. `DatatableEditController` is pure-managed with NO scene-manager `using` — keep it that way. Object templates touch live-scene objects, so entangling the editor undo with scene cleanup is the explicit preservation violation (CON-M-05 / RESEARCH Pitfall 4). **Drop** the Phase-9 cascade machinery (`PendingTypeChangeCascade`, `NeedsReviewCount`, `RecomputeCascadeState`, lines 42–71, 132–155, 237–252) — there is no column-type cascade in OT (UI-SPEC: "Needs review" state is N/A). The OT controller is the *simpler* core Apply/Undo/Redo/MarkSaved skeleton.

---

### `.../TheJawaToolboxDotNet/UI/Forms/FormObjectTemplateEditor.cs` (component, request-response — the host form)

**Analog:** `.../UI/Forms/FormDatatableEditor.cs` — **clone the shape.** (1798 lines; the load-bearing patterns below.)

**Class declaration + IEditorForm + singleton-create** (`FormDatatableEditor.cs` lines 69, 232–246):
```csharp
public partial class FormObjectTemplateEditor : UtinniForm, IEditorForm
// ...
public Form Create(IEditorPlugin plugin, List<Form> parentChildren)
{
    foreach (Form form in parentChildren)
        if (form.GetType() == typeof(FormObjectTemplateEditor)) { form.Activate(); return null; }
    var newForm = new FormObjectTemplateEditor(plugin);
    newForm.Show();
    parentChildren.Add(newForm);
    return newForm;
}
```

**Constructor + theme + settings + toolbar wiring** (lines 127–226) — `Colors.*()` accessors only (no raw ARGB; `Color.Red` is the sole allowed literal); `ini.GetInt("ObjectTemplateEditor","width"/"height")`; `CreateSettings()` (lines 250–258) adds the `[ObjectTemplateEditor]` keys (width/height, `findReplaceVisible`, `showInheritedRows`, `looseOverrideDir`). Toolbar `ToolTip.SetToolTip` strings carry the R-02 glyph-button tooltips (lines 169–174).

**LoadDocument + controller wiring + grid bind** (lines 267–325):
```csharp
if (controller != null) controller.EditApplied -= OnEditApplied;
controller = new ObjectTemplateEditController(/* mutable OT */);
controller.EditApplied += OnEditApplied;
gridSurface.BindMutable(/* effective rows + 4 columns */);
```
**OT column set is FIXED (Field · Effective value · Origin · Type)** — UI-SPEC deviations vs Phase 9: `AllowUserToOrderColumns = false`, `MultiSelect = false` / `SelectionMode = FullRowSelect`, only **Effective value** is `AutoSizeMode = Fill`. The view-only sort + LOCKED tooltip pattern (lines 297–301) carries over with re-worded copy: `"View order only — save preserves the on-disk param order."`

**OnEditApplied refresh roll-up** (lines 329–337) — re-bind grid, update undo/redo state, dirty visuals, save-menu enabled-state, counters.

**Per-type editing-control swap** (`OnEditingControlShowing` lines 1166–1191; `CommitCell` lines 1038–1109) — the `EditingControlShowing` + `CellEndEdit`/`CellValueChanged` commit-back idiom routes the edit through `controller.Apply(EditCommands.EditCellValue(...))`. **For OT**, the commit path additionally PROMOTES an inherited row to a local override on commit (D-04): if the edited row's origin is "inherited", `controller.Apply(AddOverride(...))` instead of `EditValue(...)`. The bool/int/float widget swaps (lines 1176–1190) map directly to the OT scalar widgets (`DataGridViewCheckBoxColumn`, `UtinniNumericUpDown` with `DecimalPlaces = 0`/`6`).

**Hex-fallback sub-editor** — NOT in the datatable form; this is the one host surface borrowed from Phase 8. Surface complex params (`raw bytes (hex)`) via a context-menu `Edit raw bytes…` opening the Phase 8 IFF hex/text leaf editor as a per-call modal (`using (...)`), then replace the raw leaf via `MutableIffDocument`. Mirror the modal-dialog `using` idiom seen throughout the form (e.g. `FormAddColumnDialog` use at lines 369–377).

**Save▾ build + provenance-gated enabled-state** (lines 1243–1320) — clone `BuildSaveMenu` (5 items; `miPatchLive` disabled CF-03 with the inherited tooltip, lines 1252–1254) and `RefreshSaveMenuEnabledState` (the `OpenSource` pattern-match gate: `LooseFile`/`TreArchive`/`Unknown`). **Drop** the Phase-9 `blockedByCascade`/`NeedsReviewCount` term — no cascade in OT.

**Save click handlers + DoFileSaveAsync** (lines 1336–1543) — clone `OnSaveInPlaceClick`/`OnSaveLooseOverrideClick`/`OnSaveAsClick`/`OnRepackTreClick` and the `DoFileSaveAsync` orchestrator (lines 1480–1520): `saveInFlight` barrier, `Saving (<mode>)…` / `Saved … (<mode>)` / failure copy, `controller.MarkSaved()` on success, `DispatchReload`. Repack uses `FormSaveConfirmDialog` (REUSE, do not clone — lines 1406–1420).

**Reload candor (CF-05)** (`OnReloadClicked` lines 1737–1770) — the editor STATES the reload, never triggers it. Clone the badge-pulse + status-copy idiom; the OT copy is the **LOCKED** UI-SPEC wording: badge `"Reloads on next scene change (relog to guarantee)."`, click-status the longer OT-cache-reality sentence. **Do NOT loosen** (CF-05).

**Singleton hide-not-dispose** (`FormDatatableEditor_FormClosing` lines 1774–1795) — clone verbatim:
```csharp
if (SingletonFormClosePolicy.ShouldHideInsteadOfDispose(e.CloseReason)) { e.Cancel = true; Hide(); }
```

**Dirty visuals + counters + Open** (lines 1648–1733) — `SetTitle("●" / null)`, `lblDirty = "Unsaved changes"` at `Colors.Secondary()`; counters re-worded to `{fields} fields · {overrides} local · {dirty} dirty` (+ `· {unresolved} unresolved` at `Color.Red`). `OnOpenClicked`/`OpenFromLooseFile` (lines 1697–1733): `IffReader.Read` → `MutableIffDocument.FromDocument(iff, bytes)` → typed OT doc → `LoadDocument(..., new OpenSource.LooseFile(path), name)`.

---

### `.../TheJawaToolboxDotNet/Saving/ObjectTemplateSaveTargets.cs` (service, file-I/O — the <100-line shim)

**Analog:** `.../Saving/DatatableSaveTargets.cs` — **clone, swapping the build step.** Each method builds the intermediate `MutableIffDocument` from the typed OT model, then forwards VERBATIM to the Phase 8 dispatchers (`IffSaveTargets.SaveLooseOverride`/`SaveToPath`/`SaveInPlace`, `TreRepackSaveTarget.Apply`):
```csharp
public static Task<IffSaveTargets.SaveResult> SaveLooseOverride(/* OT model */, OpenSource source, string root, string subDir)
    => IffSaveTargets.SaveLooseOverride(BuildMutableIff(/* OT model */), source, root, subDir);

public static Task<TreRepackSaveTarget.TreRepackResult> RepackIntoSourceTre(/* OT */, OpenSource.TreArchive ta, bool createBackup)
{
    byte[] otBytes = /* OT writer .Serialize() */;
    return TreRepackSaveTarget.Apply(ta, otBytes, createBackup);   // atomic File.Replace, V6000 WR-06 refusal, backup — all free
}
```
The V6000 enumerate-only refusal, atomic swap, repack-lock, and timestamped backup all come free from the Phase 8 layer (`DatatableSaveTargets.cs` lines 32–47, 93–102).

---

### `.../TheJawaToolboxDotNet/Plugin.cs` (config — register the 5th SubPanel)

**Analog:** `Plugin.cs` lines 87–106 — clone the try/catch isolation block exactly:
```csharp
// 11-xx: register the Object Template Editor (5th and final V1 SubPanel). MEF SPI NOT widened
// (GetSubPanels() stays null — CON-M-01/02). Hand-offs find this form by type in the forms list.
try { forms.Add(new FormObjectTemplateEditor(this)); }
catch (Exception ex) { Log.Info("Failed to create FormObjectTemplateEditor; Object Template Editor will be unavailable: " + ex); }
```
`GetForms()` (lines 136–139) returns the shared `forms` list; `GetSubPanels()` stays `return null` (line 146). The form is found by hand-offs via `editorPlugin.GetForms()` + `f as FormObjectTemplateEditor`.

---

### `.../TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` (component — "Switch to typed object-template view" hand-off)

**Analog:** `FormIffEditor.cs` lines 661–684 (menu build) + 1592–1618 (handler) — the existing "Switch to typed datatable view". Applies to OT because **object templates ARE IFF** (contrast Phase 10's flat `.stf`, which had no IFF-Editor hand-off).

**Menu item + visibility predicate** (lines 676–684) — add a sibling `miSwitchToObjectTemplateView`; visibility predicate is `ObjectTemplateDecoder.LooksLikeObjectTemplate(document.Root)` (the type-agnostic sniff, `ObjectTemplateDecoder.cs` lines 103–113) — HIDDEN when false (manual hand-off, not auto-route, mirrors Phase 9 D-10.3).

**Handler** (lines 1592–1618) — clone `OnSwitchToDatatableViewClick` + `FindOrCreateDatatableEditor`:
```csharp
private void OnSwitchToObjectTemplateViewClick(object sender, EventArgs e)
{
    if (document == null) return;
    var editor = FindOrCreateObjectTemplateEditor();   // foreach GetForms() → f as FormObjectTemplateEditor
    if (editor == null) { lblStatus.Text = "Object Template Editor is unavailable."; ... return; }
    editor.OpenFromMutableIff(this.document, this.Source, this.displayName);  // no re-parse
    editor.Show(); editor.Activate();
}
```
`FormObjectTemplateEditor` must expose an `OpenFromMutableIff(MutableIffDocument, OpenSource, string)` method — clone `FormDatatableEditor.OpenFromMutableIff` (lines 1593–1611), wrapping the mutable IFF as a typed OT doc instead of `DataTableDocument.FromIff`.

---

### `.../TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (component — "Open in Object Template Editor" hand-off)

**Analog:** `FormTreBrowser.cs` lines 283–354 (`OnOpenInDatatableEditor` + `FindOrCreateDatatableEditor`).

**Off-UI-thread resolve → marshal → content-gate → hand-off** (lines 291–339) — clone exactly:
```csharp
Task.Run(() => {
    bool ok = TrePayloadResolver.TryResolve(descriptor, out byte[] payload);
    BeginInvoke((Action)(() => {
        if (!ok) { lblStatus.Text = "Cannot open " + logicalPath + " — payload is enumerate-only."; return; }
        if (!ObjectTemplateDecoder.LooksLikeObjectTemplate(/* parsed root */))   // content gate (OT equivalent of IsDtiiPayload)
        { lblStatus.Text = logicalPath + " is not an object template — use Open in IFF Editor."; return; }
        var editor = FindOrCreateObjectTemplateEditor();
        editor.OpenFromTreEntry(payload, descriptor.ResolvedArchivePath, logicalPath, descriptor.ArchiveLocalOffset);
        editor.Show(); editor.Activate();
    }));
});
```
Context-menu item + visibility predicate (lines 152–204) — add `_miOpenInObjectTemplateEditor`; predicate via a `ShouldOfferObjectTemplateEditor(path, enumerateOnly)` policy mirroring `DatatableHandoffPolicy`. `FormObjectTemplateEditor.OpenFromTreEntry` clones `FormDatatableEditor.OpenFromTreEntry` (lines 1552–1586): parse → `MutableIffDocument.FromDocument` → typed OT doc → `TreRecordIndexResolver.ResolveOrUnknown` for provenance → `LoadDocument`.

---

### `Utinni.Cli/Commands/Roundtrip*Command.cs` (CLI command — CF-02 golden gate)

**Analog:** `Utinni.Cli/Commands/RoundtripIffCommand.cs` (+ `RoundtripTabCommand.cs` for the param-slice variant).

**RECOMMENDATION (RESEARCH-confirmed):** object templates ARE IFF, so the existing **`roundtrip-iff`** verb already exercises the `MutableIffDocument`/`IffWriter` byte-exact path on any OT `.iff` for the **no-mutation** gate (lines 122–151) AND the **single-leaf mutate** gate (the `--mutate-leaf <id> --mutate-hex` path, lines 157–251, which already asserts untouched-leaf identity after an edit). **A dedicated `roundtrip-ot` adds value ONLY for a param-LEVEL (not chunk-level) assertion** after a typed override/revert mutation — analogous to `RoundtripTabCommand`'s per-cell slice. Planner decides; the lower-risk default is to extend `roundtrip-iff` for byte-exactness and add a thin `roundtrip-ot --add-override/--remove-override` only if the per-param slice gate is wanted.

**Verb + envelope + exit-code pattern** (`RoundtripIffCommand.cs` lines 36–50, 77–136) — `[Verb(...)]` options class, `JsonOutput.EmitSuccess/EmitError`, exit codes (FileNotFound→3, parse/IO→2, usage→1; generic `Exception` intentionally NOT caught). Goldens land in `Utinni.Cli.Tests` (the CF-02 harness, per CONTEXT § canonical refs). Include an **unresolved-base fixture** in the golden set (RESEARCH Open-Question #3) so the D-01 degradation path is regression-tested.

---

### `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` (config — VERIFY-ONLY, no change expected)

**Analog:** itself — **already correct.** `Classify(".iff", rootTypeId)` routes object templates (SHOT/STOT/SBOT via `ObjectTemplateExtensions` lines 96–98, AND the conservative `.iff`/unknown fallback lines 132–138) → `ReloadTier.PendingNextSceneChange` (`ReloadAssetClassifier.cs` lines 107–139). **Action: verify the demo fixtures' root TypeIds; extend the SHOT/STOT/SBOT allowlist ONLY if a real fixture surfaces a root TypeId outside it** — the `.iff` fallback already returns the conservative tier regardless (RESEARCH Assumption A1). Do NOT rebuild this classifier.

---

## Shared Patterns

### MIT + reference-policy provenance header
**Source:** `Formats/Decoders/ObjectTemplateDecoder.cs` lines 1–30
**Apply to:** EVERY new `.cs` file (framework + plugin + CLI).
The MIT block (Philip Klatt, lines 1–23) + the swg-client-v2 "layout study only — no code, comments, identifier names, or test fixtures copied … Implementation original to Utinni under MIT." comment (lines 24–30). LOCKED reference policy (`project_swg_client_v2_reference.md`).

### Theme via `Colors.*()` accessors
**Source:** `FormDatatableEditor.cs` lines 150–161
**Apply to:** all WinForms surfaces (host form, breadcrumb panel, hex sub-editor host).
Never `Color.FromArgb(...)`; `Color.Red` is the ONLY allowed raw literal (destructive/unresolved emphasis). Italic + `Colors.FontDisabled()` for inherited rows; `Colors.Secondary()` accent for local-override + dirty.

### Hybrid-DOM byte-exact edit (Phase 8 / 9)
**Source:** `Formats/Iff/MutableIffDocument.cs` lines 80–128 (capture) + `RoundtripIffCommand.cs` lines 157–251 (untouched-leaf proof)
**Apply to:** the OT writer + all OT mutations.
`MutableIffDocument.FromDocument(doc, sourceBytes)` captures each node's verbatim slice; untouched nodes re-emit byte-for-byte; any edit dirties the node AND every ancestor. This is the CF-02 byte-exactness guarantee — free because OT is IFF.

### Editor-local undo/redo disentangled from scene `UndoRedoManager` (CF-04 / CON-M-05)
**Source:** `Editing/DatatableEditController.cs` lines 92–233
**Apply to:** `ObjectTemplateEditController` (extra-load-bearing — OT edits touch live-scene objects).
Pure-managed Apply/Undo/Redo/MarkSaved over the mutable doc; `IsDirty => netAppliedCount > 0`; `EditApplied` event drives host refresh. NEVER reference the scene `UndoRedoManager`.

### Save modes 1/2/4 over Phase 8 targets (CF-03)
**Source:** `.../Saving/DatatableSaveTargets.cs` (shim) → `IffSaveTargets` / `TreRepackSaveTarget` (Phase 8)
**Apply to:** `ObjectTemplateSaveTargets` + the form's save handlers.
Mode 3 (live patch) ships DISABLED with the inherited tooltip; V6000 archives refused by the Phase 8 layer (WR-06).

### Graceful degradation via `TryResolve == false` (D-01 LOCKED)
**Source:** `Formats/Tre/TrePayloadResolver.cs` lines 52–130
**Apply to:** the resolver's base-chain walk + the breadcrumb/origin rendering.
`TryResolve` returns false (never throws) for enumerate-only/V6000/missing archive — render "unresolved base `<name>`", keep local params editable, NEVER block the open.

### Singleton-form hide-not-dispose + `GetForms()` registration (CF-06)
**Source:** `FormDatatableEditor.cs` lines 232–246 + 1774–1795; `Plugin.cs` lines 87–106
**Apply to:** the host form + its registration.
`Create` returns the existing instance on re-open; `FormClosing` cancels + `Hide()`s on `UserClosing` via `SingletonFormClosePolicy.ShouldHideInsteadOfDispose`.

### Hand-off "find by type in GetForms()" + OpenFrom* entry points
**Source:** `FormIffEditor.cs` 1610–1618, `FormTreBrowser.cs` 346–354, `FormDatatableEditor.cs` 1552–1611
**Apply to:** both hand-off sites + the host form's `OpenFromTreEntry` / `OpenFromMutableIff`.
`foreach (IEditorForm f in editorPlugin.GetForms()) { editor = f as FormObjectTemplateEditor; if (editor != null) return editor; }`. The small per-form duplication is the accepted V1 posture (shared abstract base is a deferred V2 refactor).

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Formats/ObjectTemplate/ObjectTemplateResolver.cs` (DERV-chain effective-merge) | service | request-response | No existing "merge a parent-chain into one effective row-per-field-with-origin" view exists. Phase 7's `ObjectTemplateDecoder` deliberately does NOT walk the chain (its own comment defers it to Phase 11, D-13). The resolver is genuinely new LOGIC — but its two primitives are reused: `TrePayloadResolver.TryResolve` (cross-IFF fetch) + `ObjectTemplateDecoder.Decode` (per-template parse). Build the merge per RESEARCH MUST-CONFIRM #3 (`if(!isLoaded()) return base->getXxx()` semantics). |
| Hex-fallback sub-editor surfacing inside the host form | component | request-response | The datatable host has no raw-hex leaf editor; this surface is borrowed from the **Phase 8 IFF Editor** hex/text leaf editing control (`FormIffEditor`), wrapped as a per-call modal. Not a from-scratch widget — reuse the existing Phase 8 leaf editor. |
| 1-byte `ReadInt8`/`ReadByte` on `IffPayloadCursor` | utility | transform | The cursor exposes `ReadInt32Le`/`ReadFloatLe`/`ReadBytes`/`ReadCString` but no 1-byte read for the data-type/delta tags. Add a 1-line additive helper (binary-compat safe — the type is `internal sealed`). This is the only read-layer primitive Phase 11 must add. |

---

## Metadata

**Analog search scope:**
- `UtinniCoreDotNet/Formats/{ObjectTemplate(new),Datatable,StringTable,Iff,Tre,Decoders}/`
- `UtinniCoreDotNet/{Editing,Saving}/`
- `Utinni.Cli/Commands/`
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/{UI/Forms,UI/Controls,Saving}/` + `Plugin.cs`

**Files scanned:** ~50 (Formats + Editing + Saving + CLI + TJT forms); 11 read in full for concrete excerpts.

**Key cross-cutting facts confirmed during mapping:**
- `IffPayloadCursor.ReadFloatLe()` EXISTS (lines 99–116) — RESEARCH Open-Question #1 RESOLVED; no float-read gap. The only read-layer gap is the 1-byte tag read.
- `IffPayloadCursor` is `internal sealed` — adding the int8 helper is binary-compat safe.
- `ReloadAssetClassifier` ALREADY routes object-template `.iff` → `PendingNextSceneChange` — verify-only, no rebuild (CF-05).
- The Datatable lineage (IFF-mutable) is the correct structural template; the String-table lineage (flat) is NOT — object templates compose on `MutableIffDocument` for free byte-exactness.
- Phase 11 adds essentially ZERO new infrastructure: the only genuinely new code is the self-describing scalar decode/encode, the DERV-chain effective-merge resolver, and the three override/revert/edit mutations.

**Pattern extraction date:** 2026-05-30
