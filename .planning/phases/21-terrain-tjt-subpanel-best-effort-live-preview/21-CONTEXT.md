# Phase 21: Terrain TJT SubPanel (+ best-effort live preview) - Context

**Gathered:** 2026-06-16
**Status:** Ready for planning

<domain>
## Phase Boundary

A terrain editor surface that ships **inside The Jawa Toolbox** (DEC-C4, `IEditorPlugin`) and
**consumes the Phase 20 `.trn` codec** — it does NOT re-implement decode/edit/save. A modder opens a
planet's `.trn` (read-only from a TRE archive, or directly from a loose override), navigates its TGEN
procedural layer tree (Layers → Boundaries/Filters/Affectors/sub-layers) with names + active flags,
views the six shared palettes read-only, edits **fixed-length scalar/enum leaf values + active-flag
toggles** (the Phase 20 D-05 edit scope), and saves byte-exact through the loose-override matrix. On
save — and on an explicit manual preview — the change previews **live in-client where a heap-free regen
is reachable** (riding the existing `ClientReloadDispatcher` → `GroundScene::ReloadTerrain` tier),
degrading to honest save-then-reload candor where it is not. **Never a standalone Utinni renderer**
(DEC-A3 + the live-in-client lock).

**In scope:**
- A `TerrainSubPanel` (`IEditorPlugin`, docked-SubPanel form factor) inside TJT.
- Layer tree + typed/raw-fallback per-node field editor, consuming the Phase 20 model.
- Fixed-length leaf edits + active-flag toggles, saved via the loose-override matrix (Phase 20
  `apply-save-trn` field-aware path).
- Open from the TRE Browser (read-only → save-as loose override) AND direct open of an existing
  loose-override `.trn`.
- Live preview via the EXISTING terrain reload tier, fired both **auto-on-save** and via an
  **explicit manual Preview** action; honest tiered candor when live regen is unreachable.

**OUT of scope (do not research/plan — later phases / deferred):**
- Any new `.trn` codec / format logic — that is Phase 20, complete; this phase only consumes it.
- Variable-length **name edits** (layer/family names) — deferred with Phase 20 D-06.
- Structural authoring / boundary painting (full SOE TerrainEditor surface) — own milestone.
- 2D sampled-map preview (needs `Sampler*` port) — v2.1.x.
- A standalone renderer of any kind (DEC-A3) — preview is live-in-client only.
- Long-tail affector typed coverage beyond Phase 20's Tier-1 set (raw-fallback is the contract).

</domain>

<decisions>
## Implementation Decisions

### UI form factor (criterion 1)
- **D-01:** The editor ships as a **docked `TerrainSubPanel` (`IEditorPlugin`)**, matching the live-tool
  precedent (`ScenePanel`/`SnapshotPanel`) rather than the format-editor Form precedent
  (`FormParticleEditor`/`FormIffEditor`). Rationale: the live iterate-and-watch preview workflow is the
  point of this phase, and a docked panel keeps the terrain alongside the live view. (User chose this
  over "Form" and over "both.")
- **D-02:** **Where the heavy tree+grid editing UI physically lives — in the docked panel itself vs.
  hosted in a roomier standalone area (`GetStandalonePanels`/child) launched from the docked panel — is
  left to the planner**, decided from the real control sizes. Lock: docked-SubPanel entry point +
  live-preview controls; the tree+grid+typed/raw field editor may be in-panel or hosted standalone.
  Whatever is chosen must honor Pitfall 8 (Dock.Fill front-most / nested `SplitContainer`, size before
  splitter distance).

### Tree + field-editor layout (criteria 1 + 2)
- **D-03:** **Exact control choice is the planner's, from existing precedent.** Locked invariants only:
  a navigable layer tree (TGEN → Layers → Boundaries/Filters/Affectors/sub-layers, names + active flags)
  + a per-node field editor where **Tier-1 typed tags display as typed fields and unknown/long-tail tags
  degrade to a generic field list — never a hard failure** (Phase 20 D-01/D-02). The six shared palettes
  render **read-only**. Precedent to weigh: the IFF editor's `IffChunkTree` + `TreDetailPane` custom pane
  vs. the stock WinForms `PropertyGrid` (free categorization). (User chose "let planner choose.")

### Live preview trigger + candor (criteria 2 + 3)
- **D-04:** Preview fires **two ways: automatically on save AND via an explicit manual Preview/Apply
  action.** Both route through the SAME reachability + dispatch path (below) — the manual path lets the
  modder iterate without committing to disk; the on-save path mirrors the IFF/STF reload precedent.
- **D-05:** **Ride the EXISTING terrain reload infrastructure, do not invent a new one.**
  `ClientReloadDispatcher.Dispatch` (UtinniPlugins `TheJawaToolboxDotNet/Saving/ClientReloadDispatcher.cs`)
  already classifies `terrain → ReloadTier.ReloadedTerrain → GroundScene.Get().ReloadTerrain()` (INSTANCE
  ThisCall, the bare static is grep-gated/FORBIDDEN), dispatched onto the game thread via
  `GameCallbacks.AddMainLoopCall`, and gates on `Game.IsRunning` first. The candor tiers
  (`ReloadedTerrain` / `PendingNextSceneChange` / `Unavailable`) are the honest-degradation vocabulary —
  surface them verbatim, do NOT loosen the copy to over-promise.
- **D-06:** The preview/regen dispatch MUST be **heap-free on the hot path** — push-on-edit (and
  push-on-preview), NOT per-frame; stack-allocated snapshot pattern — so it never re-triggers the
  `0x0051fb0a` scene-change crash (Pitfall 6, `[[project_rh_snapshot_no_heap_alloc]]`). The manual
  Preview path is held to the same heap-free contract as the on-save path.
- **D-07 (open research question for the planner):** Whether `GroundScene::ReloadTerrain` actually
  re-reads a procedurally-edited `.trn` in-session (true live preview) or whether procedural-graph edits
  require a scene change / relog to take effect determines which candor tier this build honestly reports.
  The Phase 10 `phase10-stringtable-sc3-live-reload-residual` todo is the **precedent**: a maintainer-only
  live observation decides whether "previews live" or "reloads on next scene change" is the honest copy —
  do NOT over-promise live preview until observed. Plan a maintainer live-smoke for this, and ship the
  honest fallback copy if regen doesn't visibly update.

### Open / save workflow (criterion 1)
- **D-08:** Entry points = **open read-only from the TRE Browser → edits write to a loose override**
  (TREs are never edited in place), **AND** direct open of an existing loose-override `.trn`. Save goes
  through the loose-override matrix / Phase 20 `apply-save-trn` field-aware path with the fail-closed
  `--root` containment (Phase 20 D-07). (User chose this over loose-override-only.)

### Plugin-host hygiene (criterion 3)
- **D-09:** The `TerrainSubPanel` ctor is **guarded against MEF silent-reject** (a throwing ctor makes
  the whole `IEditorPlugin` vanish from compose with no error — Pitfall 8); wire the editor-undo seam
  (`AddUndoCommand` / `Undo` / `Redo` / `ClearUndoStack`) with null-checks per the `IEditorPlugin`
  contract (it may be null until `FormMain` wires it).

### Claude's Discretion
- Exact in-panel vs hosted-standalone layout (D-02) and the tree/field control choice (D-03).
- JSON/model surface consumed from the Phase 20 codec (use the shipped `Terrain/` model + `decode-trn`
  output as-is; do not add codec logic in TJT).
- Whether the manual Preview reuses `apply-save-trn` to a temp loose override then reloads, or applies
  in-memory before save — provided both stay heap-free (D-06) and inside the loose-override containment.
- Active-flag-toggle and read-only-palette presentation details within the locked invariants.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/ROADMAP.md` §"Phase 21" — goal + 3 success criteria (the acceptance contract).
- `.planning/REQUIREMENTS.md` — `PROD-W2-TRN-05` (the one requirement this phase satisfies).
- `.planning/phases/20-terrain-trn-codec-verbs-mcp/20-CONTEXT.md` — the codec this phase CONSUMES; the
  edit-scope lock (D-05 fixed-length only / D-06 name-edits deferred), `apply-save-trn` field-aware
  contract (D-09), `--root` containment (D-07), and the Tier-1 typed-tag set + raw-fallback rule
  (D-01/D-02) all originate there.
- `.planning/phases/20-terrain-trn-codec-verbs-mcp/20-RESEARCH.md` — TGEN tag taxonomy, palette load
  order, version dispatch, and the seven pitfalls (the codec's display semantics the UI must respect).

### Live reload / preview infrastructure (the path to ride — DO NOT reinvent)
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Saving/ClientReloadDispatcher.cs` —
  the tiered forced-reload dispatcher; `terrain → ReloadedTerrain → GroundScene.Get().ReloadTerrain()`
  already exists, game-thread-dispatched, `Game.IsRunning`-gated. The candor tiers are the honest copy.
- `UtinniCore/swg/scene/ground_scene.cpp:458-461` — `GroundScene::reloadTerrain()` (the bound
  INSTANCE thiscall, RVA `0x0051A4F0`); `dispatchSnapshot` heap-free pattern (Pitfall 6 origin).
- `.planning/todos/pending/phase10-stringtable-sc3-live-reload-residual.md` — the live-reload candor
  precedent (observe-then-honestly-label, never over-promise); D-07's model for the terrain live-smoke.

### TJT / IEditorPlugin host (the surface this plugs into)
- `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs` — the plugin contract: `GetForms()` /
  `GetStandalonePanels()` / `GetSubPanels()` + the settable undo seam (`AddUndoCommand`/`Undo`/`Redo`/
  `ClearUndoStack`, null until `FormMain` wires it).
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/SubPanels/SnapshotPanel.cs` — the
  docked-SubPanel precedent (D-01) and a substantial-panel example (~480 lines).
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormIffEditor.cs` +
  `UI/Controls/IffChunkTree.cs` + `UI/Controls/TreDetailPane.cs` — the tree+detail-pane precedent (D-03).
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` — the
  TRE-Browser open entry point (D-08).
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs` — the plugin registration /
  compose surface.

### Project invariants
- `AGENTS.md` — DEC-C4 (editors ship inside TJT), DEC-A3 (no standalone renderer; live-in-client only),
  loose-override save matrix, IFF no-pad.
- `docs/ai/lessons.md` + auto-memory `[[project_rh_snapshot_no_heap_alloc]]` (heap-free hot path,
  `0x0051fb0a`), `[[project_tjt_scene_change_naked_baseline]]` (naked-after-scene-change is baseline,
  not a regression), `[[feedback_winforms_dockfill_zorder]]` (Dock.Fill front-most), Pitfall 8.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`ClientReloadDispatcher` (UtinniPlugins TJT):** already routes terrain saves to an in-session
  `ReloadTerrain` with the full honest-tier vocabulary — the live-preview path is ~mostly wiring the
  SubPanel save/preview into this, not building a new regen path.
- **Phase 20 `.trn` codec + `apply-save-trn` + `Terrain/` model:** decode/navigate/edit/save are done;
  this phase is a UI consumer with ZERO new format logic.
- **IFF editor (`IffChunkTree` + `TreDetailPane`) and `SnapshotPanel`:** the two competing layout
  precedents the planner picks between (D-02/D-03).
- **`FormTreBrowser` open path + loose-override save matrix:** the open/save plumbing (D-08).

### Established Patterns
- `IEditorPlugin` compose via MEF — a throwing ctor silently drops the whole plugin (guard it, D-09).
- Game-thread dispatch via `GameCallbacks.AddMainLoopCall`; never call a native binding from the UI thread.
- Heap-free push-on-edit dispatch (`dispatchSnapshot`) — the hot-path crash guard (D-06).
- WinForms Dock.Fill front-most / nested `SplitContainer` (size before splitter distance) — Pitfall 8.

### Integration Points
- New `TerrainSubPanel` compiles into `TheJawaToolboxDotNet` (sibling UtinniPlugins repo — standing
  cross-repo write authority; paired commit, no human checkpoint except the live smoke).
- Consumes the Phase 20 `Terrain/` model + codec from `UtinniCoreDotNet` (already in this repo).
- Save → loose-override matrix; preview → `ClientReloadDispatcher` → `GroundScene::ReloadTerrain`.

</code_context>

<specifics>
## Specific Ideas

- User explicitly chose the **docked-SubPanel** live-tool form factor over the format-editor Form
  pattern, honoring the ROADMAP "TerrainSubPanel" name — the live iterate-and-watch workflow is the
  intent, so the editing surface should sit next to the live view.
- User wants **both** an explicit manual Preview action and auto-on-save reload — iteration without
  committing to disk matters to them.
- User deferred the two interior-layout calls (in-panel vs hosted-standalone; tree control choice) to
  the planner, trusting precedent — keep those genuinely open, don't pre-bias the plan.

</specifics>

<deferred>
## Deferred Ideas

- **Variable-length name edits** (layer/palette-family names) — stays deferred from Phase 20 D-06.
- **2D sampled-map preview** (`Sampler*` port) — v2.1.x.
- **Structural authoring / boundary painting** — own milestone.
- **Long-tail affector typed coverage** beyond Phase 20's Tier-1 set — Tier-2 follow-up (raw-fallback
  remains the contract for this phase).

### Reviewed Todos (not folded)
Three todos surfaced via weak generic-keyword matches (score 0.6, "phase"/"reload"/"seam" only):
- `phase09-datatable-editor-review-warnings.md` — Phase 9 datatable code-review edges; off-domain.
- `phase10-stringtable-sc3-live-reload-residual.md` — NOT folded as scope, but **cited as the candor
  precedent in D-07** (observe-then-honestly-label the reload tier). It resolves Phase 15, not this phase.
- `swg-window-resize-fullscreen-edge-cases.md` — D3D9/DXGI presentation resize; render-backend domain
  (Phase 18/19/24), not terrain UI.

</deferred>

---

*Phase: 21-terrain-tjt-subpanel-best-effort-live-preview*
*Context gathered: 2026-06-16*
