# Phase 21: Terrain TJT SubPanel (+ best-effort live preview) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-16
**Phase:** 21-terrain-tjt-subpanel-best-effort-live-preview
**Areas discussed:** UI form factor, Tree + grid layout, Preview trigger + candor, Open/save flow, SubPanel scope

---

## UI form factor

| Option | Description | Selected |
|--------|-------------|----------|
| Form (match format-editor precedent) | A `FormTerrainEditor` opened from TRE Browser, like `FormParticleEditor`/`FormIffEditor`. | |
| Docked SubPanel (match live-tool precedent) | Always-present panel in the main TJT window like `SnapshotPanel`/`ScenePanel`; suited to live iterate-and-watch. | ✓ |
| Both (Form editor + thin live SubPanel) | Form edits; a small SubPanel surfaces live-preview status/controls. | |

**User's choice:** Docked SubPanel (match live-tool precedent)
**Notes:** Honors the ROADMAP "TerrainSubPanel" name; the live-preview iterate-and-watch workflow is the intent → editing sits next to the live view. → CONTEXT D-01.

---

## Tree + grid layout

| Option | Description | Selected |
|--------|-------------|----------|
| Tree-left / WinForms PropertyGrid-right | Stock `PropertyGrid` for typed Tier-1 fields; raw-fallback shows generic field list. Fastest, free categorization. | |
| Tree-left / custom typed field pane | Mirror IFF editor's `IffChunkTree` + `TreDetailPane`; more control, more cost. | |
| Let planner choose from precedent | Lock tree + per-node typed/raw field editor; defer the exact control to planning. | ✓ |

**User's choice:** Let planner choose from precedent
**Notes:** Locked invariants only (tree + typed Tier-1 + raw-fallback + read-only palettes); control choice deferred. → CONTEXT D-03.

---

## Preview trigger + candor

| Option | Description | Selected |
|--------|-------------|----------|
| Auto on save (ride existing dispatcher) | Save routes through `ClientReloadDispatcher` → `ReloadTerrain`; surfaces tiered honest copy. | |
| Explicit Preview/Apply button | Dedicated regen without full save; decouples "try live" from "commit." | |
| Both — auto on save + manual preview | Preview button for iteration plus auto-reload on save. | ✓ |

**User's choice:** Both — auto on save + manual preview
**Notes:** Both paths ride the same reachability + heap-free dispatch; manual lets the modder iterate pre-commit. → CONTEXT D-04/D-05/D-06.

---

## Open/save flow

| Option | Description | Selected |
|--------|-------------|----------|
| TRE-Browser open → save-as loose override | Open read-only from TRE, edits write to loose override; plus direct open of existing loose-override `.trn`. | ✓ |
| Loose-override only (simplest) | Only opens already-extracted loose overrides; TRE extract is a separate step. | |
| Let planner derive from criterion 1 | Lock criterion-1 wording, planning details mechanics. | |

**User's choice:** TRE-Browser open → save-as loose override
**Notes:** → CONTEXT D-08.

---

## SubPanel scope (follow-up — resolving the docked-panel real-estate tension)

| Option | Description | Selected |
|--------|-------------|----------|
| Full editor inside the docked SubPanel | Tree + field editor + preview controls all in the docked panel (tall, ~SnapshotPanel scale). Cramped width tradeoff. | |
| SubPanel hosts a larger standalone editing area | Docked panel carries live-preview controls + entry; launches the full tree+grid into a roomier standalone panel/child for editing. | |
| Let planner decide from real estate | Lock docked entry + live controls + tree/grid/typed-raw; planner picks in-panel vs hosted-standalone from actual control sizes. | ✓ |

**User's choice:** Let planner decide from real estate
**Notes:** → CONTEXT D-02. Must honor Pitfall 8 (Dock.Fill front-most / nested SplitContainer) whichever way it lands.

---

## Claude's Discretion

- In-panel vs hosted-standalone editing area (D-02) and tree/field control choice (D-03).
- Model surface consumed from the Phase 20 codec (use as-is; no new format logic in TJT).
- Manual-preview mechanism (temp loose-override reload vs in-memory apply) provided it stays heap-free and inside loose-override containment.
- Active-flag-toggle and read-only-palette presentation details within the locked invariants.

## Deferred Ideas

- Variable-length name edits (Phase 20 D-06) — stays deferred.
- 2D sampled-map preview (`Sampler*` port) — v2.1.x.
- Structural authoring / boundary painting — own milestone.
- Long-tail affector typed coverage beyond Tier-1 — Tier-2 follow-up.

**Reviewed (not folded):** `phase09-datatable-editor-review-warnings.md` (off-domain),
`phase10-stringtable-sc3-live-reload-residual.md` (cited as the candor precedent in D-07, resolves
Phase 15), `swg-window-resize-fullscreen-edge-cases.md` (render-backend domain).
