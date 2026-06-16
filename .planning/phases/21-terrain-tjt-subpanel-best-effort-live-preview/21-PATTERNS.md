# Phase 21: Terrain TJT SubPanel (+ best-effort live preview) - Pattern Map

**Mapped:** 2026-06-16
**Files analyzed:** 6 new/modified (+ optional test additions)
**Analogs found:** 6 / 6 (every new file has a strong in-repo analog; zero greenfield)

> **Cross-repo note:** NEW UI files live in the sibling repo
> `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/`. They reference the Phase 20
> `Terrain/` model + the Phase 8 reload framework, both already shipped in
> `D:/Code/Utinni/UtinniCoreDotNet/`. Standing cross-repo write authority applies — paired commit, no
> human checkpoint except the D-07 live smoke.

---

## File Classification

| New/Modified File (repo) | Role | Data Flow | Closest Analog | Match Quality |
|--------------------------|------|-----------|----------------|---------------|
| `TheJawaToolboxDotNet/UI/SubPanels/TerrainSubPanel.cs` *(new, UtinniPlugins)* | component (docked SubPanel) | event-driven / request-response | `UI/SubPanels/SnapshotPanel.cs` | exact (docked live-tool SubPanel, D-01) |
| `TheJawaToolboxDotNet/UI/Forms/FormTerrainEditor.cs` *(new IF planner hosts tree+grid in a launched Form, D-02)* | component (Form host) | CRUD / transform | `UI/Forms/FormIffEditor.cs` + `UI/Controls/TreDetailPane.cs` | role-match (tree+detail editor Form) |
| `TheJawaToolboxDotNet/Saving/TerrainSaveTargets.cs` *(new IF in-proc save chosen over CLI shell-out)* | service (save target) | file-I/O | `Saving/IffSaveTargets.cs` | exact (in-proc atomic MutableIff save) |
| `TheJawaToolboxDotNet/Plugin.cs` *(modified — register the SubPanel)* | config (MEF registration) | — | `Plugin.cs` (this file's own try/catch idiom) | exact (self-precedent) |
| `TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` *(modified — add "Open in Terrain Editor")* | component (open hand-off) | event-driven | `FormTreBrowser.cs` `BuildTvTreContextMenu` (its own idiom) | exact (self-precedent) |
| `UtinniCoreDotNet.Tests/Saving/TerrainReloadCandorTests.cs` *(new — Wave-0 gap)* | test | — | `UtinniCoreDotNet.Tests/Saving/ReloadAssetClassifierTests.cs` | exact (framework-layer classifier test) |

**Consumed read-only (Phase 20 / Phase 8 — DO NOT modify, zero new format/reload logic):**
`TerrainDocument`, `TerrainLayer`, `TerrainNode`/`TerrainField`, `TerrainPalettes`, `TgenFieldLayouts`,
`TrnFieldEncoder`, `MutableIffNode`/`MutableIffDocument`, `ReloadAssetClassifier`, and the UtinniPlugins
`ClientReloadDispatcher`.

---

## Pattern Assignments

### `TerrainSubPanel.cs` (docked SubPanel — D-01 entry point)

**Analog:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/SubPanels/SnapshotPanel.cs`

**Class shape + ctor signature** (SnapshotPanel.cs:50, 67-82) — copy verbatim, including `: base("Terrain")`:
```csharp
public partial class SnapshotPanel : SubPanel, ISnapshotPanel
{
    private readonly IEditorPlugin editorPlugin;

    public SnapshotPanel(IEditorPlugin editorPlugin, HotkeyManager hotkeyManager, UtINI ini) : base("Snapshot")
    {
        InitializeComponent();
        this.editorPlugin = editorPlugin;     // keep the plugin ref for the undo seam (D-09)
        ...
    }
```
> The base `SubPanel(string name, ...)` ctor sets the panel's collapsible-header text and **pins width to
> 417px** (`SubPanel.cs:36,63` — enforced in `OnResize`). The thin Terrain SubPanel's banner +
> Open/Save/Preview/status must fit that 417px column (Pitfall 1). Heavy tree+grid → launched host (D-02).

**Scene-availability enable-gate** (SnapshotPanel.cs:211-229) — the precedent for enabling Open/Save/Preview
on a live client. Mirror for the Preview button specifically (gate on `Game.IsRunning` per Pitfall 4):
```csharp
private bool previousIsSceneActive;
public void UpdateSceneAvailability(bool isSceneActive)   // ISceneAvailability
{
    if (previousIsSceneActive == isSceneActive) return;     // idempotent — only on transition
    btnLoad.Enabled = isSceneActive;
    btnSave.Enabled = isSceneActive;
    // ... btnPreview.Enabled = isSceneActive;
    previousIsSceneActive = isSceneActive;
}
```

**Event-handler detach/reattach around programmatic control updates** (SnapshotPanel.cs:191-195, 299-309) —
the house pattern for setting a control value WITHOUT re-firing its `ValueChanged`/`CheckedChanged`; reuse for
the active-flag toggle and any typed-field value push-down:
```csharp
chkEnableNodeEditing.CheckedChanged -= chkEnableNodeEditing_CheckedChanged;
chkEnableNodeEditing.Checked = enable;
chkEnableNodeEditing.CheckedChanged += chkEnableNodeEditing_CheckedChanged;
```

**Companion-window launch (singleton, hide-not-dispose)** (SnapshotPanel.cs:120-139) — the exact pattern to
launch the roomy `FormTerrainEditor` host from the thin SubPanel if D-02 chooses the launched-Form route:
```csharp
if (placementsForm == null || placementsForm.IsDisposed)
    placementsForm = new FormSnapshotPlacements(worldSnapshot, editorPlugin);
if (placementsForm.Visible) placementsForm.Activate();
else placementsForm.Show();
```

---

### `FormTerrainEditor.cs` (roomy tree+field host — only IF D-02 chooses launched-Form, not in-panel)

**Analog:** `UI/Controls/TreDetailPane.cs` (layout/z-order) + `UI/Forms/FormIffEditor.cs` (reload + open + status)

**Nested-SplitContainer layout — the LOCKED Pitfall 8 idiom** (TreDetailPane.cs:716-742). Set `Size` BEFORE
`SplitterDistance`; add `Dock.Fill` content FIRST; use `Panel1MinSize`/`Panel2MinSize` (40/80). A throwing
ctor here makes the WHOLE plugin vanish from MEF compose:
```csharp
// 07-02 gotcha: set Size BEFORE SplitterDistance or the ctor throws and the plugin's MEF load fails.
splitOuter.Dock = DockStyle.Fill;
splitOuter.SplitterWidth = 4;
splitOuter.Panel1MinSize = 40;
splitOuter.Panel2MinSize = 80;
splitOuter.FixedPanel = FixedPanel.Panel1;   // tree keeps its size on window resize
splitOuter.Size = new Size(700, 600);
splitOuter.SplitterDistance = 160;
splitOuter.Panel1.Controls.Add(iffChunkTree);   // tree  → left
splitOuter.Panel2.Controls.Add(splitInner);     // detail → right
```

**Dock.Fill-front-most ordering** (TreDetailPane.cs:689-691, 713-714, 750-751) — add the Fill child first so
`Dock.Top`/`Dock.Bottom` strips (banner / action bar / status footer) claim their edges:
```csharp
pnlStructured.Controls.Add(lvStructured);    // Fill first
pnlStructured.Controls.Add(lblStructuredTrunc);
pnlStructured.Controls.Add(lblStructuredTitle);  // Top strips added AFTER the Fill child
```

**Theme conformance** (TreDetailPane.cs:687, 702-705, 711, 766-768) — never hard-code colors; the monospace
exception is `Consolas 9f` for raw-byte regions only:
```csharp
lblTitle.ForeColor = Colors.Font();
txtHex.BackColor   = Colors.PrimaryHighlight();
txtHex.Font        = new Font("Consolas", 9f);   // the monospace exception (UI-SPEC Typography)
lblInfoHeading.Font = Font;                       // Bold reserved to the banner — info heading uses colour
```

**Reload-candor switch — copy VERBATIM, used by BOTH on-save and manual Preview (D-04/D-05)**
(FormIffEditor.cs:1531-1561). Adapt only the case labels' text per the UI-SPEC candor table and the D-07
default (ship `PendingNextSceneChange` copy until the live smoke upgrades it):
```csharp
ReloadTier tier = ClientReloadDispatcher.Dispatch(savedPath, null);  // rootTypeId = null for .trn
switch (tier)
{
    case ReloadTier.ReloadedTerrain:
        lblStatus.Text = "Reloaded (terrain)";   // SUBJECT TO D-07 — ship "Reloads on next scene change" by default
        lblStatus.ForeColor = Colors.Font();
        break;
    case ReloadTier.PendingNextSceneChange:
        lblStatus.Text = "Reloads on next scene change";
        lblStatus.ForeColor = Colors.Font();
        break;
    case ReloadTier.Unavailable:
    default:
        lblStatus.Text = "No live client — start SWG to preview terrain edits in-session.";
        lblStatus.ForeColor = Color.Red;
        break;
}
```

**Preview/Reload button enable-gate + tooltip** (FormIffEditor.cs:1564-1589) — the defensive `Game.IsRunning`
try/catch + the save-first guard:
```csharp
bool clientUp = false;
try { clientUp = Game.IsRunning; } catch { clientUp = false; }   // Pitfall 4 — P/Invoke can throw
bool enable = !string.IsNullOrEmpty(lastSavedPath) && clientUp;
btnReload.Enabled = enable;
```
Save-first guard (FormIffEditor.cs:1533-1538) → adapt to the UI-SPEC copy
("Save first — Preview uses the last edit to classify the reload." / "Nothing to preview — edit a field first.").

**TRE-source open entry / provenance status** (FormIffEditor.cs:1500-1527) — the `OpenFromTreEntry` shape: build
the doc from payload, resolve the `OpenSource`, set a provenance status line, all inside try/catch with red
status on failure. For terrain: `TerrainDocument.FromBytes(payload)`; first commit from a TRE source is
"Save As Override…" (D-08).

---

### `TerrainSaveTargets.cs` (in-proc save — only IF planner picks in-proc over `apply-save-trn` CLI shell-out)

**Analog:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/IffSaveTargets.cs`

**Static save-target class + `SaveResult` Ok/Path/Message DTO** (IffSaveTargets.cs:50-71) — mirror the result
shape so the SubPanel maps it to "Saved → `<path>`" / "Save failed: `<reason>`" status copy.

**Atomic off-thread write idiom** (IffSaveTargets.cs:88, 165-166, 189-199) — `await Task.Run(() =>
WriteAtomic(doc, fullPath))`; the loose-override-resolving overload (`SaveLooseOverride`) takes
`(MutableIffDocument doc, OpenSource source, string resolvedRoot, ...)` and is the one to mirror for the D-08
loose-override matrix + `--root` containment.

**The in-proc edit→save sequence** (from RESEARCH Pattern 2; composes the Phase 20 surface) — the net-new logic
this file wraps:
```csharp
MutableIffNode leaf = /* FindMutableLeafByStableId(doc.Mutable, leafId) */; // walk doc.Mutable
byte[] original = leaf.GetPayloadCopy();
byte[] edited   = TrnFieldEncoder.EncodeField(original, tag, version, fieldName, value); // same length
leaf.SetPayload(edited);          // dirties ONE leaf; untouched leaves re-emit verbatim
byte[] bytes    = doc.Serialize(); // = IffWriter.Write(doc.Mutable)
// WriteAtomic(loosePath, bytes)
```
> `TrnFieldEncoder.EncodeField(dataPayload, tag, version, fieldName, value)`
> (`TrnFieldEncoder.cs:69`) throws `ArgumentException` on var-length / NaN / wrong-type values — surface that
> message as red status, never let it bubble (Pitfall 5). Gate the edit on `TerrainNode.IsEditable`
> (`TerrainNode.cs:99-100` — false for raw-preserved AND dead-skipped) BEFORE calling the encoder.

---

### `Plugin.cs` (modified — register the SubPanel)

**Analog:** the file's own MEF-guard idiom (Plugin.cs:62-69, 133-141).

**MEF-ctor guard (D-09)** — wrap the SubPanel construction in try/catch so a throwing ctor logs-and-continues
instead of silently dropping the entire TJT plugin from compose:
```csharp
try { forms.Add(new FormTreBrowser(this)); }
catch (Exception ex) { Log.Info("Failed to create FormTreBrowser; ... unavailable: " + ex); }
```

**SubPanel registration site** (Plugin.cs:133-141) — `GetSubPanels()` returns null today (Plugin.cs:178); the
existing SubPanels are registered via the `SubPanelContainer` in `GetStandalonePanels()`. **Add the
`TerrainSubPanel` to that same `SubPanelContainer("Controls", new SubPanel[]{ ... })` array** (the established
docked-SubPanel surface) — do NOT widen `GetSubPanels()` (CON-M-01/02, MEF SPI not widened):
```csharp
panels.Add(new SubPanelContainer("Controls", new SubPanel[]
{
    new ScenePanel(this, hotkeyManager, ini),
    new SnapshotPanel(this, hotkeyManager, ini),
    // ... add: new TerrainSubPanel(this, hotkeyManager, ini),
}));
```

**Undo seam** (Plugin.cs:154-161) — `AddUndoCommand`/`Undo`/`Redo`/`ClearUndoStack` are settable, **null until
`FormMain` wires them** (`IEditorPlugin.cs:44-53`). Every call site must null-check (`Undo?.Invoke()`) — D-09.

---

### `FormTreBrowser.cs` (modified — add "Open in Terrain Editor")

**Analog:** the file's own context-menu hand-off idiom (FormTreBrowser.cs:146-199).

**Add a menu item to `BuildTvTreContextMenu`** (FormTreBrowser.cs:169-174) — copy the Particle-editor entry; the
hand-off finds the editor by type in the plugin's `forms` list and calls its open-from-TRE entry. Gate
visibility on a resolvable `.trn` entry in `OnTvTreContextMenuOpening` (FormTreBrowser.cs:189-199):
```csharp
_miOpenInTerrainEditor = new ToolStripMenuItem("Open in Terrain Editor");
_miOpenInTerrainEditor.Click += OnOpenInTerrainEditor;
_tvTreContextMenu.Items.Add(_miOpenInTerrainEditor);
```
> If the planner keeps the tree+grid inside the thin SubPanel rather than a launched `IEditorForm`, the
> hand-off target is the SubPanel instance (reached via the plugin's `GetStandalonePanels()` list) instead of
> the forms list — adapt the lookup accordingly.

---

### `TerrainReloadCandorTests.cs` (new — Wave-0 test gap)

**Analog:** `D:/Code/Utinni/UtinniCoreDotNet.Tests/Saving/ReloadAssetClassifierTests.cs`

**Framework-layer classifier assertion** (ReloadAssetClassifierTests.cs:48-81) — the established pattern: assert
the pure `ReloadAssetClassifier.Classify` routing, NOT the plugin dispatcher (which P/Invokes `Game.IsRunning`
and lives in the un-referenced UtinniPlugins project). Add the explicit `.trn → ReloadedTerrain` case the
Wave-0 gap flags:
```csharp
[Fact]
public void Classify_Trn_ReturnsReloadedTerrain()
{
    Assert.Equal(ReloadTier.ReloadedTerrain, ReloadAssetClassifier.Classify(".trn", null));
}
```
> The candor-COPY string-assert (each `ReloadTier` → locked footer text) is the other Wave-0 gap. Because the
> copy lives in the UtinniPlugins UI (not referenced by this test project), the planner must either extract the
> tier→copy map into a testable pure helper or assert it via a UtinniPlugins-side test host — flag this as a
> planning decision (mirrors the "why framework-layer not plugin-layer" note in the analog's class doc).

---

## Shared Patterns

### Reload dispatch (the path to RIDE — D-05, never reinvent)
**Source:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/ClientReloadDispatcher.cs:80-128`
**Apply to:** both the on-save and manual Preview paths in `TerrainSubPanel`/`FormTerrainEditor`.
```csharp
ReloadTier tier = ClientReloadDispatcher.Dispatch(savedPath, null);   // null rootTypeId for .trn
```
The dispatcher already: gates on `Game.IsRunning` first (returns `Unavailable` if down), classifies
`.trn → ReloadedTerrain` (ClientReloadDispatcher.cs:107-115), and wraps the INSTANCE thiscall
`GroundScene.Get().ReloadTerrain()` in `GameCallbacks.AddMainLoopCall` (game thread). **Never** call a native
binding from the UI thread; **never** the bare static `GroundScene.ReloadTerrain()` (FORBIDDEN/grep-gated).

### Heap-free hot path (D-06 — the `0x0051fb0a` crash guard)
**Source:** native `UtinniCore/swg/scene/ground_scene.cpp` `dispatchSnapshot` (already in force) +
`[[project_rh_snapshot_no_heap_alloc]]`.
**Apply to:** every place the UI enqueues a reload. **Exactly ONE `Action` per save/preview** (push-on-edit),
NEVER a per-frame terrain callback (no `Add*Callback` that fires on draw/update). The native guard is already
in force; the UI's only job is to not defeat it.

### MEF-ctor guard + WinForms z-order (D-09 / Pitfall 8)
**Source:** `Plugin.cs:62-69` (try/catch isolation) + `TreDetailPane.cs:716-742` (Size-before-SplitterDistance)
+ `:689-691` (Dock.Fill front-most) + `[[feedback_winforms_dockfill_zorder]]`.
**Apply to:** the SubPanel ctor, the launched host ctor, and the whole panel build. A throwing ctor silently
deletes the `IEditorPlugin` from compose with no error.

### Theme conformance (UI-SPEC Color/Typography)
**Source:** `UtinniCoreDotNet.UI.Theme.Colors` via TreDetailPane precedent (`:687,702-705,766-768`).
**Apply to:** every control. `Colors.Primary()`/`PrimaryHighlight()`/`Font()`/`FontDisabled()`/`Secondary()`;
8.25pt base, Bold reserved to the banner, `Consolas 9f` only for raw-byte regions, `Color.Red` only for
error-tier status. No green "success" hue (D-07 forbids over-promising).

### Editability gate (Pitfall 5)
**Source:** `TerrainNode.IsEditable` (`TerrainNode.cs:99-100`); palettes read-only (`TerrainPalettes`); name
fields read-only this phase (Phase 20 D-06).
**Apply to:** the field editor in `TerrainSubPanel`/`FormTerrainEditor`. Typed leaf → editable fixed-length
scalar/enum + active-flag toggle only; raw-preserved / dead-skipped / palette / name → read-only generic list
with the matching UI-SPEC hint copy. NEVER a hard failure (Phase 20 D-01/D-02/D-03).

---

## Consumed Model Surface (Phase 20 — read-only, in-process, all `public`)

| Member | Location (`D:/Code/Utinni/UtinniCoreDotNet/`) | Use |
|--------|-----------------------------------------------|-----|
| `TerrainDocument.FromBytes(byte[])` / `.FromIff(...)` / `.Serialize()` / `.Mutable` / `.Layers` / `.Palettes` | `Formats/Terrain/TerrainDocument.cs:84,97,110,55,61,58` | decode + re-emit |
| `TerrainLayer.Name` / `.Active` / `.StableIdPath` / `.Nodes` / `.SubLayers` | `Formats/Terrain/TerrainLayer.cs:49,52,55,58,61` | tree population; `Active` ↔ `--field active` IHDR leaf |
| `TerrainNode.Tag/.Version/.TypedFields/.IsRawPreserved/.IsDeadSkipped/.RawHex/.StableIdPath/.IsEditable` | `Formats/Terrain/TerrainNode.cs:79-100` | node display + editability gate |
| `TerrainField.Name/.Value/.DisplayType/.Editable` | `Formats/Terrain/TerrainNode.cs:38-59` | per-field row |
| `TrnFieldEncoder.EncodeField(payload, tag, version, fieldName, value)` | `Formats/Terrain/TrnFieldEncoder.cs:69` | exact-span LE encode (throws on var-len/NaN) |
| `MutableIffNode.GetPayloadCopy()/.SetPayload(byte[])` | `Formats/Iff/MutableIffNode.cs` | dirty ONE leaf |
| `ReloadAssetClassifier.Classify(ext, rootTypeId)` | `Saving/ReloadAssetClassifier.cs:124` (`.trn`→`ReloadedTerrain` at `:138-141`) | reload-tier decision (already routes `.trn`) |

---

## No Analog Found

None. Every new file has a strong in-repo analog; the model + reload framework it consumes is fully shipped.
The single genuine UNKNOWN is **D-07** (does native `GroundScene::ReloadTerrain` visibly re-read a
procedurally-edited `.trn` in-session) — not a missing analog but a maintainer-live-smoke disposition. Ship the
honest `PendingNextSceneChange` copy by default; do NOT label preview "live" until observed (precedent:
`.planning/todos/pending/phase10-stringtable-sc3-live-reload-residual.md`).

## Metadata

**Analog search scope:** `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/{UI/SubPanels,UI/Forms,UI/Controls,Saving}` + `Plugin.cs`; `D:/Code/Utinni/UtinniCoreDotNet/{Formats/Terrain,Formats/Iff,Saving,PluginFramework,UI/Controls}` + `UtinniCoreDotNet.Tests/Saving`.
**Files scanned:** 14 read in full or targeted ranges.
**Pattern extraction date:** 2026-06-16
