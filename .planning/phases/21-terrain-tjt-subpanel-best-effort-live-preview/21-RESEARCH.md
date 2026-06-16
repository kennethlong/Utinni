# Phase 21: Terrain TJT SubPanel (+ best-effort live preview) - Research

**Researched:** 2026-06-16
**Domain:** WinForms (.NET Framework net4.7.2, x86) editor surface inside The Jawa Toolbox, consuming the shipped Phase 20 `.trn` codec + the shipped tiered reload dispatcher; best-effort live-in-client terrain preview.
**Confidence:** HIGH (every claim below is from in-repo source read this session; the ONE genuine unknown is D-07's "does ReloadTerrain visibly re-read procedural edits in-session" — that is maintainer-live-smoke-only and is flagged explicitly).

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Editor ships as a **docked `TerrainSubPanel` (`IEditorPlugin`)**, matching the live-tool precedent (`ScenePanel`/`SnapshotPanel`), NOT the format-editor `Form` precedent. (User chose this over "Form" and over "both.")
- **D-02:** **Where the heavy tree+grid editing UI physically lives — in the docked panel itself vs. hosted in a roomier standalone area (`GetStandalonePanels`/child) launched from the docked panel — is left to the planner**, decided from real control sizes. Lock: docked-SubPanel entry point + live-preview controls; the tree+grid+typed/raw field editor may be in-panel or hosted standalone. Must honor Pitfall 8.
- **D-03:** **Exact control choice is the planner's, from existing precedent.** Locked: a navigable layer tree (TGEN → Layers → Boundaries/Filters/Affectors/sub-layers, names + active flags) + a per-node field editor where Tier-1 typed tags display as typed fields and unknown/long-tail degrade to a generic field list — never a hard failure. The six shared palettes render read-only. Precedent: `IffChunkTree`+`TreDetailPane` vs. stock `PropertyGrid`.
- **D-04:** Preview fires **two ways: automatically on save AND via an explicit manual Preview/Apply action.** Both route through the SAME reachability + dispatch path.
- **D-05:** **Ride the EXISTING terrain reload infrastructure, do not invent a new one.** `ClientReloadDispatcher.Dispatch` already classifies `terrain → ReloadTier.ReloadedTerrain → GroundScene.Get().ReloadTerrain()` (INSTANCE ThisCall, bare static FORBIDDEN/grep-gated), game-thread-dispatched via `GameCallbacks.AddMainLoopCall`, `Game.IsRunning`-gated. Surface candor tiers (`ReloadedTerrain`/`PendingNextSceneChange`/`Unavailable`) verbatim — do NOT loosen the copy.
- **D-06:** Preview/regen dispatch MUST be **heap-free on the hot path** — push-on-edit (and push-on-preview), NOT per-frame; stack-allocated snapshot. The manual Preview path is held to the same contract.
- **D-07 (open research question):** Whether `GroundScene::ReloadTerrain` actually re-reads a procedurally-edited `.trn` in-session vs. requiring a scene change/relog. Precedent = `phase10-stringtable-sc3-live-reload-residual` (observe-then-honestly-label). Plan a maintainer live-smoke; ship the honest fallback copy if regen doesn't visibly update. Do NOT over-promise.
- **D-08:** Entry points = open read-only from the TRE Browser → edits write to a loose override, AND direct open of an existing loose-override `.trn`. Save goes through the loose-override matrix / Phase 20 `apply-save-trn` field-aware path with fail-closed `--root` containment.
- **D-09:** `TerrainSubPanel` ctor is **guarded against MEF silent-reject**; wire the editor-undo seam (`AddUndoCommand`/`Undo`/`Redo`/`ClearUndoStack`) with null-checks (may be null until `FormMain` wires it).

### Claude's Discretion
- Exact in-panel vs hosted-standalone layout (D-02) and the tree/field control choice (D-03).
- JSON/model surface consumed from the Phase 20 codec (use the shipped `Terrain/` model + `decode-trn` output as-is; no codec logic in TJT).
- Whether the manual Preview reuses `apply-save-trn` to a temp loose override then reloads, or applies in-memory before save — provided both stay heap-free (D-06) and inside the loose-override containment.
- Active-flag-toggle and read-only-palette presentation details within the locked invariants.

### Deferred Ideas (OUT OF SCOPE)
- **Variable-length name edits** (layer/palette-family names) — stays deferred from Phase 20 D-06.
- **2D sampled-map preview** (`Sampler*` port) — v2.1.x.
- **Structural authoring / boundary painting** — own milestone.
- **Long-tail affector typed coverage** beyond Phase 20's Tier-1 set — Tier-2 follow-up (raw-fallback remains the contract).
- Any new `.trn` codec / format logic — Phase 20, complete; this phase only consumes it.
- A standalone renderer of any kind (DEC-A3) — preview is live-in-client only.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PROD-W2-TRN-05 | On save, the terrain change previews live in-client where a heap-free hot-path regen is reachable; where it is not (this build), it degrades to save-then-reload with explicit candor — never a standalone Utinni renderer. | The reachability/dispatch path **already exists and already routes `.trn`**: `ReloadAssetClassifier.Classify(".trn", null) == ReloadTier.ReloadedTerrain` (`UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs:94,138-141`) → `ClientReloadDispatcher.Dispatch` → `GameCallbacks.AddMainLoopCall(() => GroundScene.Get().ReloadTerrain())` (`ClientReloadDispatcher.cs:107-115`). The heap-free contract is satisfied **upstream in native** by `dispatchSnapshot` (`ground_scene.cpp:104-141`); the managed side enqueues ONE `Action` per save/preview (push-on-edit, not per-frame) — see Pitfall sections. This phase is **wiring a SubPanel into a path that is already built and already byte-routes terrain**, not building regen infra. |
</phase_requirements>

---

## Summary

The single most important finding for planning: **almost the entire backend for this phase already exists and is already wired for terrain.** Phase 20 shipped a complete, fully-`public`, in-process `.trn` model + edit encoder (`UtinniCoreDotNet/Formats/Terrain/*` and `Formats/Decoders/TgenDecoder.cs`). Phase 8 shipped `ReloadAssetClassifier` + `ClientReloadDispatcher`, which **already classify `.trn` → `ReloadedTerrain` and already dispatch `GroundScene.Get().ReloadTerrain()` on the game thread, `Game.IsRunning`-gated.** The heap-free hot-path guard (`dispatchSnapshot`, the `0x0051fb0a` fix) lives in native `ground_scene.cpp` and is already in force on the callback dispatch path. This phase is **a WinForms consumer**: a tree + field-editor + Open/Save/Preview/status-footer, calling the shipped model for decode, the shipped `TrnFieldEncoder`/`MutableIffNode.SetPayload`/`IffWriter.Write` for edit-save, and the shipped `ClientReloadDispatcher.Dispatch` for preview. Zero new format logic, zero new reload infra (D-05).

The single most important **architectural tension** the planner must resolve: **D-01 says "docked SubPanel," but EVERY shipped heavy editor (IFF, Datatable, Stringtable, Object-Template, Particle) is a `Form` registered via `GetForms()`, NOT a SubPanel.** `GetSubPanels()` returns `null` in `Plugin.cs:178`. The reason is structural: a `SubPanel` is a `UserControl` **hard-pinned to 417px width** (`SubPanel.cs:36,63`), hosted in a narrow `CollapsiblePanel` column (`pnlPlugins`). A tree+property-grid terrain editor does not fit comfortably in 417px. This is exactly why D-02 exists. The realistic interpretation that satisfies BOTH D-01 (docked entry point) and the 417px reality is: **a thin docked `TerrainSubPanel` (banner + Open/Save/Preview/status, all of which fit in 417px) that LAUNCHES a roomier surface** — either a `GetStandalonePanels()` `SubPanelContainer` page or a child `Form` — for the actual tree+grid. The planner should weigh this against the precedents in the Layout section below.

The single genuine unknown (D-07): static analysis **cannot** tell you whether `GroundScene::ReloadTerrain` (native `0x0051A4F0`) re-reads a procedurally-edited `.trn` graph in-session, or whether the running scene caches the procedural graph so edits only take on the next scene change/relog. This is maintainer-live-smoke-only, exactly like the Phase 10 stringtable SC3 precedent. **Worse**: the Phase 10 todo records that on the maintainer's machine the loose-override `searchPath` is currently **disabled** (phantom-walk mitigation) and `useSwgOverrideCfg=false`, so the client does not even pick up loose overrides without a config change the maintainer declined to make for a smoke. The honest default this phase must ship is the **`PendingNextSceneChange` copy** until a live observation upgrades it.

**Primary recommendation:** Build a thin docked `TerrainSubPanel` (Open/Save/Preview/status, fits 417px) that hosts the tree+field editor in a roomier launched surface; consume the Phase 20 `Terrain/` model and `TrnFieldEncoder` **in-process** (mirror `IffSaveTargets` in-proc `IffWriter.Write` style, NOT a CLI shell-out); fire preview through `ClientReloadDispatcher.Dispatch(savedPath, null)`; ship the `PendingNextSceneChange` candor copy by default and gate any "live" wording behind a maintainer live-smoke todo (D-07).

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Decode `.trn` → navigable tree + palettes | Managed codec (`UtinniCoreDotNet.Formats.Terrain`, this repo) | — | Phase 20 shipped `TerrainDocument.FromBytes/FromIff`; UI calls it, adds nothing. |
| Tree navigation + field display/edit UI | WinForms SubPanel/host (UtinniPlugins sibling repo) | — | New code; the only net-new surface in this phase. |
| Encode ONE fixed-length field edit | Managed codec (`TrnFieldEncoder`, this repo) | — | Phase 20 shipped exact-span LE encoder; UI supplies (tag,version,field,value). |
| Byte-exact re-emit / save | Managed IFF DOM (`MutableIffNode.SetPayload` + `IffWriter.Write`, this repo) | CLI `apply-save-trn` (alternative) | In-proc mirrors `IffSaveTargets`; CLI path also exists. Planner picks (see Discretion). |
| Loose-override path containment | Managed (`LooseOverridePath.Resolve`) | — | Phase 20 `apply-save-trn` uses it; same fail-closed `--root` rule. |
| Reload classification (`.trn`→tier) | Managed framework (`ReloadAssetClassifier`, this repo) | — | Already routes `.trn`→`ReloadedTerrain`. No change. |
| Game-thread reload dispatch | Managed dispatcher (`ClientReloadDispatcher`, UtinniPlugins) + native `GroundScene::reloadTerrain` | — | Already built; UI calls `Dispatch`. |
| Heap-free hot-path guard | Native (`ground_scene.cpp dispatchSnapshot`) | — | Already in force; UI must not introduce per-frame work — push-on-edit only. |
| Actual terrain rendering / regen | **Live SWG client (real engine)** | — | DEC-A3: Utinni never renders terrain. Preview is the real client reloading. |

---

## Standard Stack

No new packages. Everything is in-repo and already shipped.

### Core (consumed as-is — all `public`, in-process)
| Component | Location (this repo unless noted) | Purpose | Provenance |
|-----------|-----------------------------------|---------|-----------|
| `TerrainDocument` | `UtinniCoreDotNet/Formats/Terrain/TerrainDocument.cs` | `FromBytes(byte[])` / `FromIff(IffDocument, byte[])` → decoded doc; holds `Mutable`; `Serialize()` = byte-exact re-emit | [VERIFIED: source read] |
| `TerrainLayer` | `Formats/Terrain/TerrainLayer.cs` | `Name`, `Active`, `StableIdPath`, `Nodes`, `SubLayers` (recursion) | [VERIFIED: source read] |
| `TerrainNode` | `Formats/Terrain/TerrainNode.cs` | One boundary/filter/affector. `Tag`, `Version`, `TypedFields`, `IsRawPreserved`, `IsDeadSkipped`, `RawHex`, `StableIdPath`, `IsEditable` | [VERIFIED: source read] |
| `TerrainField` | `Formats/Terrain/TerrainNode.cs` | `Name`, `Value` (string), `DisplayType`, `Editable` | [VERIFIED: source read] |
| `TerrainPalettes` / `TerrainPalette` / `TerrainPaletteFamily` | `Formats/Terrain/TerrainPalettes.cs` | Six fixed-load-order read-only palettes; `Role`, `Present`, `Ambiguous`, `Families` (`FamilyId`→`Name`) | [VERIFIED: source read] |
| `TgenFieldLayouts` / `TgenFieldDescriptor` | `Formats/Terrain/TgenFieldLayouts.cs` | Tier-1 typed-tag descriptor table; `For(tag,version)`, `HasLayout`, `LayerHeaderTag="IHDR"`, `ActiveFieldName` | [VERIFIED: source read] |
| `TgenDisplayType` enum | `Formats/Terrain/TgenFieldLayouts.cs:50` | `ScalarFloat`, `Int32`, `Enum32`, `ActiveFlag`, `FamilyIdRef` | [VERIFIED: source read] |
| `TrnFieldEncoder.EncodeField(payload, tag, version, fieldName, value)` | `Formats/Terrain/TrnFieldEncoder.cs:69` | Returns same-length payload with ONE field overwritten; rejects var-length/NaN/Inf | [VERIFIED: source read] |
| `MutableIffNode` | `Formats/Iff/MutableIffNode.cs` | `Kind`, `SubTypeId`, `Parent`, `Children`, `GetPayloadCopy()`, `SetPayload(byte[])` | [VERIFIED: source read] |
| `MutableIffDocument.DeriveStableId` | `Formats/Iff/MutableIffDocument.cs:161` | Stable-id derivation for leaf addressing | [VERIFIED: source read] |
| `ReloadAssetClassifier` / `ReloadTier` | `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` | `.trn` already → `ReloadedTerrain` (`:94,138-141`) | [VERIFIED: source read] |
| `ClientReloadDispatcher.Dispatch(savedPath, rootTypeIdOrNull)` | `UtinniPlugins/…/Saving/ClientReloadDispatcher.cs:80` | Game-thread, `Game.IsRunning`-gated reload; returns the tier | [VERIFIED: source read] |
| `IEditorPlugin` | `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs` | `GetForms()`/`GetStandalonePanels()`/`GetSubPanels()` + undo seam | [VERIFIED: source read] |
| `SubPanel` / `SubPanelContainer` | `UtinniCoreDotNet/UI/Controls/SubPanel.cs`, `SubPanelContainer.cs` | Docked-panel base (`UserControl`, **417px fixed width**); `FlowLayoutPanel` host | [VERIFIED: source read] |
| `Colors` theme | `UtinniCoreDotNet.UI.Theme.Colors` | `Primary()`/`PrimaryHighlight()`/`Secondary()`/`Font()`/`FontDisabled()` | [VERIFIED: UI-SPEC + precedent] |

### Supporting (precedents to copy)
| Precedent | Location (UtinniPlugins) | Copy For |
|-----------|--------------------------|----------|
| `FormParticleEditor` | `…/UI/Forms/FormParticleEditor.cs` | The save▾ menu + Open-from-TRE + Preview/Reload candor + locked tier copy + `Game.IsRunning` save-gating. The closest analog (Wave-2 format editor with live-preview ambition). |
| `FormIffEditor` | `…/UI/Forms/FormIffEditor.cs` | The canonical `OnReloadClicked` switch over `ReloadTier` (`:1531-1562`) with verbatim status copy; `lastSavedPath` plumbing. |
| `SnapshotPanel` | `…/UI/SubPanels/SnapshotPanel.cs` | The substantial-SubPanel precedent (~480 LOC) + `UpdateSceneAvailability(bool)` (`:212`). Live-tool docked form-factor. |
| `IffChunkTree` + `TreDetailPane` | `…/UI/Controls/` | Tree + custom detail-pane precedent (D-03 option A). |
| `FormTreBrowser` | `…/UI/Forms/FormTreBrowser.cs` | The "Open in <Editor>" context-menu hand-off pattern (`:143-176`) + client-root resolution. |
| `IffSaveTargets` | `…/Saving/IffSaveTargets.cs` | In-proc atomic save via `IffWriter.Write(MutableIffDocument)` + `WriteAtomic` (`:271`). The save-target idiom to mirror. |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Docked `SubPanel` only (D-01 literal) | Thin SubPanel entry + launched `Form`/standalone for the editor | 417px width can't host tree+grid; ALL shipped heavy editors chose `Form`. The planner MUST resolve this (D-02). See Layout section. |
| In-proc `TrnFieldEncoder` + `IffWriter.Write` | Shell out to `apply-save-trn` CLI | In-proc is the established TJT save idiom (`IffSaveTargets`); CLI re-launches a process + re-parses. CLI gives the verify-only-target-span belt-and-suspenders for free. Planner's call (Discretion). |
| `IffChunkTree`/`TreDetailPane` custom pane | stock WinForms `PropertyGrid` | `PropertyGrid` gives free categorization + typed editors but is awkward for the typed-vs-raw-fallback split and per-field editability gating. Custom pane matches house style. See D-03 tradeoffs. |

**Installation:** None. New `.cs` files compile into the existing `TheJawaToolboxDotNet` project (sibling repo) referencing the existing `UtinniCoreDotNet.dll`.

## Package Legitimacy Audit

N/A — this phase installs **zero external packages**. All dependencies are in-repo, already shipped, and already CI-gated. Build is VS2026/v145 MSBuild + `dotnet test --no-build` xUnit (no NuGet add).

---

## Architecture Patterns

### System Architecture Diagram (data flow)

```
[TRE Browser] --"Open in Terrain Editor"-->  payload bytes + provenance
       |                                              |
       v                                              v
[Open Terrain Override…] --loose .trn bytes--> TerrainDocument.FromBytes(bytes)   (Phase 20 codec, in-proc)
                                                       |
                                          decoded:  Layers (tree) + Palettes (read-only) + Mutable DOM
                                                       |
                                                       v
                                          [Layer Tree]  --select node-->  [Field Editor]
                                                                              |
                                                       Tier-1 typed -> typed fields (editable)
                                                       raw/dead/palette/name -> read-only generic list
                                                                              |
                                                          user edits ONE fixed-length field / active flag
                                                                              |
                                                                              v
                                       TrnFieldEncoder.EncodeField(payload, tag, ver, field, value)  (same-length)
                                                                              |
                                          MutableIffNode(leaf).SetPayload(newPayload)  (dirties ONE leaf)
                                                                              |
                                   +------ Save -------+                +----- Preview (no disk) -----+
                                   v                   v                v                             |
                       IffWriter.Write(Mutable)   apply-save-trn      (apply in-memory OR temp        |
                       -> WriteAtomic(loose path)  CLI (alt)           loose override per Discretion)  |
                                   |                                                                   |
                                   +------------------+----------------------------------------------+
                                                      v
                              ClientReloadDispatcher.Dispatch(savedPath, null)   (D-05 — DO NOT reinvent)
                                                      |
                          Game.IsRunning? --no--> ReloadTier.Unavailable  (red "No live client…")
                                  |yes
                                  v
                  Classify(".trn") == ReloadedTerrain
                                  |
                  GameCallbacks.AddMainLoopCall(() => GroundScene.Get().ReloadTerrain())   (game thread, INSTANCE thiscall)
                                  |
                  native swg::groundScene::reloadTerrain @ 0x0051A4F0   (real SWG engine reloads terrain)
                                  |
                  D-07 UNKNOWN: does the running scene re-read procedural edits in-session,
                                or only on next scene change? -> ship PendingNextSceneChange copy until observed.
```

### Recommended Project Structure (sibling repo — UtinniPlugins)
```
The Jawa Toolbox/TheJawaToolboxDotNet/
├── UI/
│   ├── SubPanels/
│   │   └── TerrainSubPanel.cs            # D-01 docked entry: banner + Open/Save/Preview/status (fits 417px)
│   └── Forms/  (OR SubPanels/ standalone) # D-02 planner choice: the roomy tree+grid host
│       └── FormTerrainEditor.cs          # IF planner chooses launched-Form host
├── Saving/
│   └── TerrainSaveTargets.cs             # in-proc save mirror of IffSaveTargets (if not shelling apply-save-trn)
└── Plugin.cs                              # register TerrainSubPanel (see Registration section)
```

### Pattern 1: Mirror the IFF/Particle reload-candor switch verbatim
**What:** The status footer reflects the tier returned by `ClientReloadDispatcher.Dispatch`. The exact `switch` already exists in `FormIffEditor.OnReloadClicked`.
**When to use:** Both the on-save auto-reload AND the manual Preview (D-04) route through this same switch.
**Example (verbatim from `FormIffEditor.cs:1539-1561`):**
```csharp
// Source: UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs:1539
ReloadTier tier = ClientReloadDispatcher.Dispatch(lastSavedPath, rootTypeId); // rootTypeId = null for .trn
switch (tier)
{
    case ReloadTier.ReloadedTerrain:
        lblStatus.Text = "Reloaded (terrain)";     // SUBJECT TO D-07 — see candor table
        lblStatus.ForeColor = Colors.Font();
        break;
    case ReloadTier.PendingNextSceneChange:
        lblStatus.Text = "Reloads on next scene change";
        lblStatus.ForeColor = Colors.Font();
        break;
    case ReloadTier.Unavailable:
    default:
        lblStatus.Text = "No live client — start SWG to reload edits in-session.";
        lblStatus.ForeColor = Color.Red;
        break;
}
```

### Pattern 2: In-proc edit-save (mirror IffSaveTargets, NOT a CLI shell-out)
**What:** TJT save targets work in-process on the `MutableIffDocument`, serializing directly with `IffWriter.Write`, then atomic write. The terrain edit reuses the exact Phase 20 encoder + DOM.
**Example (the in-proc equivalent of `ApplySaveTrnCommand`):**
```csharp
// Locate the DATA leaf by stable id (same DeriveStableId walk the CLI uses).
MutableIffNode leaf = /* FindMutableLeafByStableId(doc.Mutable, leafId) */;
byte[] original = leaf.GetPayloadCopy();
byte[] edited = TrnFieldEncoder.EncodeField(original, tag, version, fieldName, value); // same length
leaf.SetPayload(edited);                       // dirties ONE leaf; untouched leaves re-emit verbatim
byte[] bytes = doc.Serialize();                // = IffWriter.Write(doc.Mutable)
// WriteAtomic(loosePath, bytes)  — mirror IffSaveTargets.WriteAtomic (cs:271)
```
**Note:** `ApplySaveTrnCommand.ResolveFieldContext` (`ApplySaveTrnCommand.cs:266`) shows the leaf-addressing rule: for a typed field, `leaf.Parent.SubTypeId` = version FORM, `grandparent.SubTypeId` = tag FORM; for `--field active`, the leaf's parent FORM must be `IHDR`, written at offset 0. The UI must compute `(tag, version, leafId)` from the selected `TerrainNode.StableIdPath` / `TerrainLayer.StableIdPath` the same way.

### Pattern 3: MEF-ctor guard (D-09)
**What:** A throwing `IEditorPlugin`/registered-child ctor makes the WHOLE plugin vanish from MEF compose with no error. `Plugin.cs` wraps every editor-form registration in try/catch.
**Example (verbatim idiom from `Plugin.cs:62-69`):**
```csharp
try { forms.Add(new FormTerrainEditor(this)); }  // OR the SubPanel registration
catch (Exception ex) { Log.Info("Failed to create Terrain editor; will be unavailable: " + ex); }
```
The panel build itself (nested SplitContainer, etc.) must also be exception-safe (Pitfall 8).

### Anti-Patterns to Avoid
- **Calling a native binding (`GroundScene.Get().ReloadTerrain()`, `Game.IsRunning`) directly from the UI thread.** Always via `ClientReloadDispatcher.Dispatch` (which wraps the reload in `AddMainLoopCall`) and always try/catch `Game.IsRunning` (it P/Invokes and can throw outside an injected client — `ClientReloadDispatcher.cs:82-89`).
- **Inventing a new reload trigger** (e.g. fabricating a scene change via `AddSetSceneCallback`). That hook is a notification, NOT a trigger; fabricating one risks reentrancy and turns the documented "naked after scene change" baseline into a perceived regression (`ClientReloadDispatcher.cs:53-56`, `[[project_tjt_scene_change_naked_baseline]]`).
- **Re-implementing the `.trn` codec / field offsets in TJT.** All offsets live in `TgenFieldLayouts` (single source). The UI never knows a byte offset.
- **Editing the source TRE in place.** TREs are read-only; the first commit from a TRE-opened doc is "Save As Override…" (D-08).
- **Per-frame work on the regen path.** One `Action` per save/preview only (D-06).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Decode `.trn` tree/palettes | A TJT-side TGEN parser | `TerrainDocument.FromBytes/FromIff` | Phase 20 shipped it; byte-exact, both-lineage tested. |
| Encode a field edit | LE byte packing in the UI | `TrnFieldEncoder.EncodeField` | Single-source offsets; rejects var-length/NaN; exact-span. |
| Byte-exact re-emit | Manual chunk framing | `MutableIffNode.SetPayload` + `IffWriter.Write` | Captured-slice DOM; untouched leaves verbatim. |
| Loose-override path safety | Manual path join | `LooseOverridePath.Resolve` (via `apply-save-trn` or directly) | Fail-closed `--root` containment. |
| Reload classification | A `.trn`→tier map in TJT | `ReloadAssetClassifier.Classify` | Already routes `.trn`→`ReloadedTerrain`. |
| Game-thread reload | `AddMainLoopCall` + binding by hand | `ClientReloadDispatcher.Dispatch` | Already game-thread-dispatched, `Game.IsRunning`-gated, INSTANCE thiscall. |
| Heap-free dispatch | A managed snapshot scheme | (already native) `dispatchSnapshot` | Native guard already in force; UI just must not add per-frame work. |
| Reload-tier copy | New wording | The locked verbatim strings (candor table) | D-05/D-07 forbid loosening; consistency with shipped editors. |

**Key insight:** This phase is ~90% wiring of already-shipped, already-CI-gated, already-terrain-routing components. The net-new code is WinForms presentation + the `(tag,version,leafId)` derivation from a selected tree node + a save-target. The temptation to "improve" the reload or the codec is the trap; the locked decisions (D-05/D-06/D-09) exist to prevent it.

## Runtime State Inventory

> This is a UI/wiring phase (new WinForms code + a save path), not a rename/refactor/migration. The categories below are answered for completeness, but no data migration is in scope.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — edits write a loose-override `.trn` file; no DB/datastore keys involved. | None. |
| Live service config | **CRITICAL for D-07:** the SWG client's loose-override `searchPath` is **disabled** on the maintainer's machine (`swgemu_live.cfg` priority-27 `searchPath_00_27=…\loose` commented out; `ut.ini useSwgOverrideCfg=false`) per the 2026-06-12 phantom-walk mitigation (`[[project_swg_client_loose_overrides]]`, phase10 todo 2026-06-13 entry). Without re-enabling it, the client will NOT pick up a saved loose `.trn` regardless of reload tier. | Plan the live-smoke task to NOTE this prerequisite; the maintainer decides whether to re-enable for the smoke. Do NOT assume a saved override is visible to the client. |
| OS-registered state | None. | None. |
| Secrets/env vars | None. | None. |
| Build artifacts | New `.cs` files compile into `TheJawaToolboxDotNet.dll` (sibling repo). The build copies into `Plugins/<name>/`. `Generated/UtinniCore.cs` is NOT touched (pure managed consumer; no bridge change). | Standard MSBuild rebuild + cross-repo paired commit. |

## Common Pitfalls

### Pitfall 1: D-01 "docked SubPanel" vs the 417px width reality (the layout trap)
**What goes wrong:** Planning a full tree+property-grid into a `SubPanel` and discovering it is hard-pinned to 417px (`SubPanel.cs:36,63`, enforced in `OnResize`), hosted in a narrow `CollapsiblePanel`/`FlowLayoutPanel` column.
**Why it happens:** D-01 names "SubPanel"; the precedent SubPanels (`ScenePanel`, etc.) are narrow control strips, not editors. ALL heavy editors are `Form`s (`Plugin.cs:57-131`); `GetSubPanels()` returns null (`Plugin.cs:178`).
**How to avoid:** Honor D-01's docked **entry point** with a thin SubPanel (banner + Open/Save/Preview/status all fit 417px), and host the tree+grid in a launched roomy surface (`GetStandalonePanels()` page or a child `Form`) per D-02. Decide from real control sizes.
**Warning signs:** A tree control narrower than ~200px after the splitter; a property grid that needs horizontal scroll for field names.

### Pitfall 2: Dock.Fill must be front-most / SplitContainer Size-before-SplitterDistance (Pitfall 8, the MEF killer)
**What goes wrong:** (a) A `Dock.Fill` content region added LAST docks first and starves the `Top`/`Bottom` strips (banner/action-bar/footer go empty — exactly the 07-04b regression). (b) Setting `SplitterDistance` before `Size` at construction throws, and the throwing ctor makes the WHOLE `IEditorPlugin` vanish from MEF compose with no error.
**Why it happens:** WinForms z-order docking semantics + MEF's silent-reject-on-ctor-throw.
**How to avoid:** Add the `Dock.Fill` content region FIRST (front-most) so `Top`/`Bottom` claim edges (`TreDetailPane.BuildContentArea` precedent, `[[feedback_winforms_dockfill_zorder]]`). Set `Size` BEFORE `SplitterDistance`; use `Panel1MinSize`/`Panel2MinSize` (40/80 precedent, `TreDetailPane:717-743`). Wrap the entire panel build in the D-09 try/catch.
**Warning signs:** The Terrain editor silently absent from the menu after a build (MEF dropped it); empty docked strips.

### Pitfall 3: Heap-free hot path / never per-frame (D-06, the `0x0051fb0a` crash)
**What goes wrong:** Per-frame heap allocation on the render/scene hot path fragmented SWG's allocator and crashed scene change at `0x0051fb0a` (inside `GroundScene::ctor`). The fix was the stack-allocated `dispatchSnapshot` (`ground_scene.cpp:104-141`, `[[project_rh_snapshot_no_heap_alloc]]`).
**Why it happens:** Subscribing a callback that does work every frame, or rebuilding a collection per dispatch.
**How to avoid:** This phase enqueues exactly ONE `Action` per save/preview via `AddMainLoopCall` (push-on-edit, not per-frame). Do NOT add a per-frame terrain callback. The native guard is already in force; the UI's only job is to not defeat it.
**Warning signs:** Any `Add*Callback` that fires on draw/update; allocating inside an `AddMainLoopCall` body that runs repeatedly.

### Pitfall 4: `Game.IsRunning` P/Invoke can throw outside an injected client
**What goes wrong:** Reading `Game.IsRunning` directly crashes the caller when no native binding is bound (e.g. the editor opened with no live SWG).
**How to avoid:** Always `try { clientUp = Game.IsRunning; } catch { clientUp = false; }` — the exact defensive pattern at `ClientReloadDispatcher.cs:82-89` and every TJT call site. Use it for the Preview button enable-gate too (mirror `FormParticleEditor.PreviewAvailable` / `RefreshReloadButtonState`).

### Pitfall 5: Editing a non-editable node (raw-fallback / dead / palette / name)
**What goes wrong:** Letting the user edit a node the decoder did not fully understand corrupts a half-understood payload.
**How to avoid:** Gate editability on `TerrainNode.IsEditable` (`TerrainNode.cs:100`: false for raw-preserved AND dead-skipped). Palettes are read-only (`TerrainPalettes`). Name fields are read-only this phase (D-06). The `apply-save-trn` path already rejects non-editable targets (`ApplySaveTrnCommand.cs:149-154`); the in-proc path must mirror that gate. Show the matching hint copy (candor table) — never a hard failure.

### Pitfall 6: D-07 — do NOT label preview "live" before a maintainer observes it
**What goes wrong:** Shipping "Reloaded (terrain)" copy when, in fact, the running scene caches the procedural graph and the edit only appears on next scene change/relog → the tool lies to the modder.
**How to avoid:** Default to the `PendingNextSceneChange` copy; gate "live" wording behind a maintainer live-smoke (precedent: phase10 SC3 todo). See the dedicated D-07 section below.

## Code Examples

### Open from TRE Browser (mirror the existing hand-off)
```csharp
// Source: FormTreBrowser.cs:143-176 (context-menu hand-off) + FormParticleEditor.cs:369 (open entry)
// TRE-side: add "Open in Terrain Editor", enabled only for a resolvable .trn entry, → finds the
// editor by type in the plugin's forms list and calls its open-from-TRE entry:
public void OpenFromTreEntry(byte[] payload, string resolvedArchivePath, string logicalPath, long archiveLocalOffset)
{
    TerrainDocument doc = TerrainDocument.FromBytes(payload);   // Phase 20 codec
    // Source = OpenSource.TreArchive(...) → first commit is "Save As Override…" (read-only source)
    // populate the tree from doc.Layers + doc.Palettes
}
```

### Direct loose-override open
```csharp
// Source: FormParticleEditor.cs:311 (loose-file open path)
byte[] bytes = File.ReadAllBytes(loosePath);
TerrainDocument doc = TerrainDocument.FromBytes(bytes);
// Source = OpenSource.LooseFile(loosePath) → "Save" commits in place; lastSavedPath = loosePath
```

### Preview without disk (D-04 manual path, per Discretion)
```csharp
// Option A (recommended, simplest, stays in containment): apply edit to a temp loose override, then Dispatch.
//   - edit in-memory (SetPayload), Serialize(), WriteAtomic(tempLoosePath), Dispatch(tempLoosePath, null)
// Option B: keep an in-memory edited copy, write the real loose override only on Save.
// EITHER way: ONE AddMainLoopCall per Preview (D-06); route through ClientReloadDispatcher (D-05).
ReloadTier tier = ClientReloadDispatcher.Dispatch(previewPath, null);  // rootTypeId null for .trn
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Each editor builds its own reload logic | Centralized `ReloadAssetClassifier` + `ClientReloadDispatcher` tiers | Phase 8 (08-REVIEWS HIGH-4) | Terrain editor consumes the dispatcher, adds nothing. |
| Heap snapshot per dispatch (`std::vector::reserve`) | Stack-allocated `dispatchSnapshot` (kInlineCap=16) | 2026-05-22 (Phase 3 R-H) | The `0x0051fb0a` crash is fixed at the native dispatch site; UI must stay push-on-edit. |
| Hand-rolled per-format codecs in TJT | Pure-managed codecs in `UtinniCoreDotNet/Formats/*` consumed in-proc | Phases 8-20 | Terrain UI is a thin consumer; zero codec in TJT. |

**Deprecated/outdated:**
- The bare static `GroundScene.ReloadTerrain()` (non-instance) is **FORBIDDEN and grep-gated** — it is bound nowhere and no-ops silently. Use the INSTANCE `GroundScene.Get().ReloadTerrain()` (already what `ClientReloadDispatcher` calls). Do not introduce the static form.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The thin-SubPanel-launches-roomy-host interpretation best satisfies D-01+D-02 given the 417px width. | Summary / Pitfall 1 | LOW — D-02 explicitly leaves this to the planner; this is a recommendation, not a constraint. The planner may legitimately cram a compact tree+grid into the SubPanel column if control sizes allow. |
| A2 | In-proc edit-save (mirror `IffSaveTargets`) is preferable to shelling `apply-save-trn`. | Pattern 2 / Alternatives | LOW — D-discretion explicitly allows either; both are correct. |
| A3 | `GroundScene::ReloadTerrain` will NOT visibly re-read procedural edits in-session (so `PendingNextSceneChange` is the honest default). | D-07 section | This is the genuine unknown — see D-07. Marked `[ASSUMED]` until maintainer live-smoke. The plan must ship the honest fallback and a smoke task; it must NOT hard-code "live." |

## Open Questions

### D-07 — Does `GroundScene::ReloadTerrain` re-read a procedurally-edited `.trn` in-session? (THE phase-defining unknown)
**What we know (statically):**
- `GroundScene::reloadTerrain()` (`UtinniCore/swg/scene/ground_scene.cpp:458-461`) is a one-line thunk to the native `swg::groundScene::reloadTerrain` at RVA `0x0051A4F0` (`:59`), an INSTANCE `__thiscall`.
- `ClientReloadDispatcher` already classifies `.trn` → `ReloadedTerrain` and already dispatches this on the game thread (`:107-115`).
- The native body is NOT in our source (it's the SWG client's own function); we cannot read what it does. We do NOT know whether it re-resolves the on-disk `.trn` or merely re-applies an already-cached procedural graph.
**What's unclear:**
- Whether the running `GroundScene`/`Terrain` caches the decoded procedural graph such that a fresh on-disk edit is ignored until a scene change/relog (the Phase 10 LocalizationManager-style caching question).
- Whether the client even SEES the loose override: per the phase10 todo (2026-06-13), the loose `searchPath` is currently **disabled** on the maintainer's machine and `useSwgOverrideCfg=false` — so a saved override is not picked up without a config change the maintainer declined for the last smoke.
**Recommendation:**
1. Ship the **honest default**: status copy = "Reloads on next scene change" (`PendingNextSceneChange` wording), even though the classifier returns `ReloadedTerrain`. The UI-SPEC already encodes this contingency: *"if the maintainer live-smoke shows ReloadTerrain does NOT visibly re-read procedural edits in-session, this build ships the `PendingNextSceneChange` copy instead — do NOT label it 'live' until observed."*
2. Plan a **maintainer live-smoke task** (precedent: `phase10-stringtable-sc3-live-reload-residual`): with loose `searchPath` re-enabled, save an edited `.trn`, fire Preview, observe whether terrain visibly changes (a) immediately, (b) after a TJT scene change, or (c) only after relog. Record the disposition in a SMOKE-LOG and set the final copy accordingly.
3. Note the searchPath prerequisite in the smoke task explicitly — without it, the smoke observes nothing regardless of reload tier.
**Do NOT** block the phase on D-07. The save + tiered-candor path is fully shippable and automation-verifiable; only the "live vs pending" wording disposition awaits the smoke.

### Open Question 2 — Active-flag leaf addressing from the tree
**What we know:** `--field active` mutates the int32 at offset 0 of the `IHDR` DATA leaf under `LAYR` (`ApplySaveTrnCommand.cs:73-75,272-278`); `TerrainLayer.Active` reads the SAME leaf (`TerrainLayer.cs:51`). `TerrainLayer.StableIdPath` is the LAYR FORM's path.
**What's unclear:** The UI must derive the IHDR DATA leaf's stable id from the selected `TerrainLayer`. The CLI resolves this by asserting the addressed leaf's parent is `IHDR`. The in-proc path must walk from the layer's StableIdPath to its IHDR/DATA child, or expose the leaf id from the model.
**Recommendation:** Confirm during Wave-0 whether the model surfaces the IHDR leaf id directly, or whether the UI must walk `doc.Mutable` for the IHDR child of the selected layer (the `FindMutableLeafByStableId` + parent-is-IHDR check from `ApplySaveTrnCommand` is the reference walk).

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| VS2026 MSBuild (v145) | Managed build of sibling repo | ✓ | Dev18 | None (NOT `dotnet build` — MSB3823 on WinForms .resx). |
| `UtinniCoreDotNet.dll` (Phase 20 codec) | All decode/edit | ✓ | shipped | None — hard dependency; already in tree. |
| `ClientReloadDispatcher` / `ReloadAssetClassifier` | Preview | ✓ | shipped | None — already terrain-routing. |
| Live injected SWG client | D-07 live-smoke ONLY | ✗ (maintainer-only) | — | Save + tiered candor verified by automation/unit; live disposition deferred to maintainer smoke (Pitfall 6 / D-07). |
| Loose-override `searchPath` enabled in client cfg | Client to see saved override (smoke) | ✗ (disabled — phantom-walk mitigation) | — | Maintainer re-enables for the smoke; otherwise no fallback (override invisible). |

**Missing dependencies with no fallback:** None that block the buildable/automation-verifiable deliverable.
**Missing dependencies with fallback:** Live SWG + enabled searchPath — required only for the D-07 live disposition, which is explicitly a maintainer-deferred smoke (not a build gate).

## Validation Architecture

> `nyquist_validation` not explicitly false in config (treated enabled). This phase is WinForms UI in the sibling repo; the codec/encoder/classifier it consumes are already covered by Phase 20 + Phase 8 xUnit suites.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (`UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`) |
| Config file | per-project `.csproj` |
| Quick run command | `dotnet test --no-build` (after VS2026 MSBuild) |
| Full suite command | VS2026 MSBuild `Utinni.sln` (x86/Release) then `dotnet test --no-build` + native Catch2 |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PROD-W2-TRN-05 | `.trn` classifies as `ReloadedTerrain` | unit | `dotnet test --no-build` (ReloadAssetClassifierTests) | ✅ `UtinniCoreDotNet.Tests/Saving/ReloadAssetClassifierTests.cs` (extend with explicit `.trn` assertion if absent) |
| PROD-W2-TRN-05 | Edit→save byte-exact (one field span only) | unit | `dotnet test --no-build` (apply-save-trn goldens) | ✅ Phase 20 `Utinni.Cli.Tests` — the in-proc save target should add a parity test |
| PROD-W2-TRN-05 | Non-editable node rejected | unit | `dotnet test --no-build` | ✅ Phase 20 (`ApplySaveTrnCommand` rejects raw/dead) — mirror for in-proc path |
| PROD-W2-TRN-05 | Tiered candor copy correct per tier | unit (string assert) | `dotnet test --no-build` | ❌ Wave 0 — assert the editor's status copy matches locked strings per `ReloadTier` |
| PROD-W2-TRN-05 | Live render-on-reload disposition | **manual (maintainer)** | n/a — live SWG smoke | ❌ D-07 todo (precedent: phase10 SC3) |

### Sampling Rate
- **Per task commit:** `dotnet test --no-build` on the touched test project.
- **Per wave merge:** full `dotnet test --no-build` + cross-repo build of `TheJawaToolboxDotNet`.
- **Phase gate:** full suite green + the D-07 maintainer live-smoke disposition recorded (the ONLY non-automatable gate).

### Wave 0 Gaps
- [ ] A unit/string-assert that the Terrain editor's status footer maps each `ReloadTier` to the locked copy (live=`PendingNextSceneChange` default per D-07).
- [ ] An in-proc edit-save parity test (UI save path produces byte-identical output to `apply-save-trn` for the same edit) — if the planner chooses the in-proc path.
- [ ] Confirm `ReloadAssetClassifierTests` has an explicit `.trn → ReloadedTerrain` assertion (add if missing).
- [ ] A maintainer live-smoke task for D-07 (with the searchPath-enabled prerequisite noted).

## Security Domain

> `security_enforcement` not disabled in config (treated enabled). This is a local offline desktop modding tool (no network, no auth, no multi-user). The only relevant control is path containment.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — (local desktop tool, single user) |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | yes | `TrnFieldEncoder` rejects NaN/Inf/var-length; tree gates on `IsEditable`; value parsers are invariant-culture (already shipped) |
| V6 Cryptography | no | — |
| V12 File / Path | yes | `LooseOverridePath.Resolve` fail-closed `--root` containment (no path escape — `ApplySaveTrnCommand.cs:99-107`); atomic write |

### Known Threat Patterns for {WinForms desktop editor writing files}
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal on save (relAsset escapes client root) | Tampering | `LooseOverridePath.Resolve` containment — already enforced by the shipped save path; the in-proc path must reuse it. |
| Overwriting the source TRE | Tampering | TREs never edited in place; first commit from TRE source is "Save As Override…" (D-08). |
| Corrupting a half-understood payload | Tampering | Edit gated on `TerrainNode.IsEditable`; encoder verifies exact-span; CLI re-parses + verifies untouched-leaf byte-identity. |

## Sources

### Primary (HIGH confidence — all read this session)
- `UtinniCoreDotNet/Formats/Terrain/{TerrainDocument,TerrainNode,TerrainLayer,TerrainPalettes,TgenFieldLayouts,TrnFieldEncoder}.cs` — the Phase 20 model + encoder surface.
- `UtinniCoreDotNet/Formats/Iff/{MutableIffNode,MutableIffDocument}.cs` — the DOM edit/serialize surface.
- `UtinniCoreDotNet/Saving/ReloadAssetClassifier.cs` — `.trn`→`ReloadedTerrain` already wired.
- `Utinni.Cli/Commands/ApplySaveTrnCommand.cs` — the canonical edit-save algorithm + leaf addressing + containment.
- `UtinniCore/swg/scene/ground_scene.cpp:40-160,455-461` — `reloadTerrain` thunk (RVA `0x0051A4F0`) + `dispatchSnapshot` heap-free guard.
- `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs` + `UI/Controls/{SubPanel,SubPanelContainer}.cs` + `UI/Forms/FormMain.cs:300-379` — plugin contract + 417px SubPanel reality + compose loop.
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/ClientReloadDispatcher.cs` — tiered dispatch + verbatim semantics.
- `D:/Code/UtinniPlugins/.../UI/Forms/{FormIffEditor,FormParticleEditor,FormTreBrowser}.cs` + `UI/SubPanels/SnapshotPanel.cs` + `Saving/IffSaveTargets.cs` + `Plugin.cs` — UI precedents, locked candor copy, in-proc save idiom, registration.
- `.planning/phases/20-terrain-trn-codec-verbs-mcp/{20-CONTEXT,20-RESEARCH}.md` — Tier-1 taxonomy + seven pitfalls + edit scope.
- `.planning/todos/pending/phase10-stringtable-sc3-live-reload-residual.md` — D-07 candor precedent + the disabled-searchPath blocker.
- `.planning/phases/21-…/21-CONTEXT.md` + `21-UI-SPEC.md` — locked decisions + the approved visual/copy contract.

### Secondary (MEDIUM)
- Auto-memory: `[[project_rh_snapshot_no_heap_alloc]]`, `[[project_tjt_scene_change_naked_baseline]]`, `[[project_swg_client_loose_overrides]]`, `[[feedback_winforms_dockfill_zorder]]`.

### Tertiary (LOW)
- None — no web sources needed; this is an in-repo wiring phase.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every component read in source this session; all `public`, all shipped.
- Architecture (consume codec + ride dispatcher): HIGH — the path already routes terrain; verified line-by-line.
- Layout (D-01 vs 417px): HIGH on the constraint (417px is hard-pinned in source); the resolution is D-02 planner's choice (flagged, not decided).
- Pitfalls: HIGH — each traces to a source line or a documented memory/crash.
- D-07 live-vs-pending disposition: LOW (genuinely unknowable statically) — flagged as the one maintainer-smoke item; honest fallback is the shippable default.

**Research date:** 2026-06-16
**Valid until:** 2026-07-16 (stable; in-repo source). Re-verify if Phase 20 model surface or `ClientReloadDispatcher` change before planning.

---

## What you need to know to PLAN this phase well (the bottom line)

1. **The backend is done.** Decode (`TerrainDocument`), edit (`TrnFieldEncoder` + `MutableIffNode.SetPayload` + `IffWriter.Write`), containment (`LooseOverridePath`), classify (`ReloadAssetClassifier` already → `ReloadedTerrain`), dispatch (`ClientReloadDispatcher` already game-thread + `Game.IsRunning`-gated + INSTANCE thiscall), and the heap-free guard (native `dispatchSnapshot`) all exist and are CI-gated. Plan tasks that CONSUME them; forbid re-implementation (D-05).

2. **Resolve the D-01/D-02 layout call FIRST.** D-01 says "docked SubPanel," but `SubPanel` is pinned to 417px and every shipped heavy editor is a `Form`. Recommended resolution: thin docked `TerrainSubPanel` (Open/Save/Preview/status entry point) that launches a roomier tree+grid host (standalone page or child Form). The planner decides from real control sizes — but must make the call explicitly, because it shapes every subsequent task.

3. **Pick the D-03 tree/field control** (custom `IffChunkTree`+`TreDetailPane`-style pane vs stock `PropertyGrid`) and the save mechanism (in-proc mirror of `IffSaveTargets` vs shell `apply-save-trn`). Both are genuinely open; in-proc + custom-pane matches house style, CLI + `PropertyGrid` is cheaper but rougher.

4. **Gate editability on `TerrainNode.IsEditable`**; palettes and names are read-only; show the locked hint copy on raw/dead/palette/name selection. Never hard-fail.

5. **Ship the honest `PendingNextSceneChange` candor by default (D-07)** and plan a maintainer live-smoke (with the disabled-searchPath prerequisite noted) before any "live" wording. The save + tiered-candor path is fully shippable and automation-verifiable without the smoke; only the live-vs-pending wording awaits it.

6. **Respect the two MEF/WinForms traps (Pitfall 8 / D-09):** Dock.Fill front-most, SplitContainer Size-before-SplitterDistance, whole build inside a try/catch, undo-seam null-checked. A throwing ctor silently deletes the editor.

7. **One `AddMainLoopCall` per save/preview (D-06).** No per-frame terrain work. Always `try/catch` `Game.IsRunning`.

---

## RESEARCH COMPLETE

**Phase:** 21 - Terrain TJT SubPanel (+ best-effort live preview)
**Confidence:** HIGH (one flagged maintainer-smoke unknown: D-07 live-vs-pending)

### Key Findings
- **`.trn` is ALREADY end-to-end wired for reload:** `ReloadAssetClassifier.Classify(".trn") == ReloadedTerrain` → `ClientReloadDispatcher.Dispatch` → `GameCallbacks.AddMainLoopCall(() => GroundScene.Get().ReloadTerrain())` (INSTANCE thiscall, native RVA `0x0051A4F0`). This phase wires a SubPanel into a path that already byte-routes terrain. Zero new reload infra (D-05).
- **The Phase 20 model is fully `public` and in-process:** `TerrainDocument`/`TerrainLayer`/`TerrainNode`/`TerrainPalettes`/`TgenFieldLayouts`/`TrnFieldEncoder`. Edit-save = `EncodeField` → `SetPayload` → `IffWriter.Write` (mirror `IffSaveTargets` in-proc; CLI `apply-save-trn` is the alternative). Zero new codec (Phase 20 done).
- **D-01 tension:** `SubPanel` is hard-pinned to 417px (`SubPanel.cs:36,63`); ALL shipped heavy editors are `Form`s, `GetSubPanels()` returns null. Recommended: thin docked entry + launched roomy host (D-02 is the escape hatch — planner must decide explicitly).
- **D-07 is the one genuine unknown:** native `ReloadTerrain` body is not in our source; we cannot statically determine in-session re-read. Worse, the maintainer's loose `searchPath` is currently disabled (phantom-walk mitigation). Ship `PendingNextSceneChange` copy by default; gate "live" behind a maintainer live-smoke (phase10 SC3 precedent).
- **Locked candor copy verified verbatim** from `FormIffEditor.OnReloadClicked` (`:1539-1561`): "Reloaded (terrain)" / "Reloads on next scene change" / "No live client — start SWG to reload edits in-session."

### File Created
`.planning/phases/21-terrain-tjt-subpanel-best-effort-live-preview/21-RESEARCH.md`

### Confidence Assessment
| Area | Level | Reason |
|------|-------|--------|
| Standard Stack | HIGH | All components read in source; all public, all shipped, all CI-gated. |
| Architecture | HIGH | Reload path already routes terrain; verified line-by-line. |
| Pitfalls | HIGH | Each traces to a source line or documented crash/memory. |
| D-07 disposition | LOW | Unknowable statically; honest fallback is the shippable default; maintainer smoke deferred. |

### Open Questions
- D-07 (live vs pending render disposition) — maintainer live-smoke, honest `PendingNextSceneChange` default.
- Active-flag leaf-id derivation from a selected `TerrainLayer` (Wave-0 confirm: model surfaces IHDR leaf id, or UI walks `doc.Mutable`).

### Ready for Planning
Research complete. The planner can create PLAN.md files. The two decisions the planner MUST make explicitly before sequencing tasks: (1) D-01/D-02 docked-thin-vs-launched-host layout, (2) D-03 tree/field control + save mechanism. Everything else is consuming shipped, verified components.
