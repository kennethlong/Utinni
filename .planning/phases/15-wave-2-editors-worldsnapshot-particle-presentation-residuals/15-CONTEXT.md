# Phase 15: Wave-2 editors (WorldSnapshot, Particle) + presentation residuals - Context

**Gathered:** 2026-06-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Land the first two Wave-2 DCC-style editors as TJT MEF `IEditorPlugin` SubPanels (the
unchanged DEC-C4 Wave-1 seam), and close the two D3D9/reload presentation residuals slotted
alongside the editor work:

- **PROD-W2-WS — WorldSnapshot:** grow the *already-shipped* TJT `SnapshotPanel` (in-world
  single-node targeting + gizmo translate/rotate + add/remove, backed by `WorldSnapshotImpl` /
  `WorldSnapshotReaderWriter`) into a real editor. **Zero new format work** — reuse the shipped codecs.
- **PROD-W2-PRT — Particle / client-effect:** a brand-new `.prt` codec in `UtinniCoreDotNet`
  (none exists today) plus a Particle SubPanel editor with live in-client preview when injected.
- **RESID-04:** enumerate and fix the SWG window-resize / windowed↔fullscreen edge cases
  (no device `Reset`).
- **RESID-03:** confirm and honestly state SC3 live-reload candor for string-table + object-template
  reload, applied to the editors' reload paths.

**Explicitly NOT in this phase:** Terrain `PROD-W2-TRN` (`.trn`, heavier codec) — deferred to v2.1.
No new plugin mechanism. No 3D mesh/skeleton/animation authoring (the Blender lane, DEC-A3).

</domain>

<decisions>
## Implementation Decisions

### WorldSnapshot editor delta (PROD-W2-WS)
- **D-01:** The Wave-2 delta on top of the shipped in-world gizmo panel is a **flat placements
  list/table + multi-select bulk operations**. Add a browsable table of ALL nodes in the loaded
  snapshot (id, object-template name, cell, position) with search/filter and click-to-select that
  drives the existing gizmo, PLUS multi-select for bulk move / delete / retemplate.
- **D-02:** Reuse the shipped `WorldSnapshotReaderWriter` / `WorldSnapshotImpl` — **zero new format
  work**. The table is a new view over existing data + existing edit operations; bulk ops compose
  the existing per-node edit commands.
- **D-03:** The panel conforms to the unchanged Wave-1 `IEditorPlugin.GetSubPanels()` seam and the
  canonical singleton **hide-not-dispose** pattern from Phase 8 (CON-T-05 `*Impl` separation).

### Particle `.prt` codec depth (PROD-W2-PRT)
- **D-04:** **Full typed decode** of the `.prt` / client-effect format — type the emitter / wave /
  timing / color fields, modeled on the `swg-client-v2` `clientParticle` C++ reference.
- **D-05:** **Degrade, never abort.** When the codec hits a `.prt` variant or field not covered by
  the reference, preserve the unrecognized chunk/field as **raw bytes** so save round-trips
  byte-safe; the editor greys-out what it cannot type. This mirrors the project's established
  OT-multichunk degrade-don't-abort precedent (`project_ot_multichunk_list_params`) and de-risks the
  MEDIUM-confidence, no-fixtures format.
- **D-06:** The codec lives in `UtinniCoreDotNet` alongside the other `Formats/*` codecs (format logic
  stays out of TJT and out of the MCP server — MCP dispatches to CLI verbs with zero format logic,
  per Phase 14).

### Particle editor surface, preview, and AI-assist (PROD-W2-PRT)
- **D-07:** **AI is read-assist only** in V1 — it explains / summarizes the loaded effect and
  suggests changes as text; the modder applies edits manually. The AI never writes `.prt` bytes
  directly. (No prompt-to-mutate path this phase.)
- **D-08:** AI read-assist is delivered on **both surfaces**: (a) the `.prt` codec is exposed via
  new `utinni-cli` verbs + MCP read/summarize tools so an MCP client (Claude Desktop/Code) can
  read-assist against headless Utinni, AND (b) an in-TJT assist button in the Particle SubPanel
  reuses that same read path inline while injected. The in-app button reuses the CLI/MCP path — it
  does not introduce an independent format or AI path.
- **D-09:** **Live in-client preview = hot-retrigger loaded instances.** When injected, after a
  save/reload the editor re-triggers the effect instances the game already has live in the scene
  (hooks into the running client-effect/particle manager). *(This is the heavier preview option —
  flag for research: the exact runtime hook into the effect manager is the open implementation
  question.)*

### Preview-vs-author boundary (DEC-A3 — one sentence per editor, MANDATORY)
- **D-10 — WorldSnapshot:** "Utinni places / transforms / retemplates **existing** object templates;
  creating the templates or their meshes is the DCC (Blender) lane."
- **D-11 — Particle:** "Utinni edits emitter / timing / color parameters and swaps texture / mesh
  **references**; authoring the referenced meshes / textures stays in Blender."

### RESID-04 — window-resize / windowed↔fullscreen (presentation)
- **D-12:** **Enumerate live, then fix the root cause.** Reproduce against a live injected session
  and fill the edge-case matrix (per the folded todo), confirm whether the cluster is one root cause
  (the prime suspect: SWG's exclusive-fullscreen mode switch detaching the embed), then apply the
  **targeted intercept/suppress-the-mode-switch** fix to keep SWG windowed-embedded.
- **D-13:** **Hard constraint — no `IDirect3DDevice9::Reset` on SWG's device** (it owns untracked
  default-pool resources → `D3DERR_INVALIDCALL` → DEVICELOST → crash). Resize the window and let
  windowed COPY `Present` self-stretch the backbuffer↔window mismatch
  (`feedback_d3d9_reset_third_party`). Keep RT-space mouse mapping correct across any resize
  (`feedback_imgui_embedded_d3d9_rt_space`).

### RESID-03 — SC3 live-reload candor
- **D-14:** **Live-observe + honest badges.** During this phase's live session, observe SC3 reload
  for `.stf` (Step-7 scene-change + stale-`sourceCrc` checks from the folded phase-10 todo) and for
  object-template reload, set honest reload-candor badge copy, and apply the same candor pattern to
  the new WorldSnapshot / Particle reload paths. Do NOT loosen badge copy to over-promise; if a
  reload is relog-only, the badge must say so.

### Claude's Discretion
- WorldSnapshot bulk-edit undo/command integration (compose existing per-node edit commands; exact
  command/undo wiring is the planner's call).
- Exact `.prt` field taxonomy and `UtinniCoreDotNet/Formats/Particle/` layout (follow the existing
  `Formats/*` codec structure).
- Table control choice / column layout for the placements list (follow existing TJT SubPanel UI
  conventions, e.g. the Datatable editor's grid).
- CLI verb naming for the `.prt` read/summarize tools (follow the Phase-13 verb conventions; mind the
  16-verb CommandLineParser cap noted in `project_phase13_cli_verbs`).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope & requirements
- `.planning/ROADMAP.md` — Phase 15 section (goal, success criteria, constraint guard-rails).
- `.planning/REQUIREMENTS.md` — PROD-W2-WS (L134), PROD-W2-PRT (L135), RESID-03 (L145), RESID-04 (L146);
  PROD-W2-TRN (L150) is the deferred v2.1 follow-up.

### WorldSnapshot (existing code to grow — PROD-W2-WS)
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/SubPanels/SnapshotPanel.cs` —
  the shipped panel to extend (node targeting, gizmo, add/remove, load/save/reload).
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/SWG/WorldSnapshotImpl.cs` —
  the `*Impl` backing the panel; `WorldSnapshotReaderWriter.Node` is the per-node model.
- `D:/Code/Utinni/UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs` — existing snapshot commands.
- `D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs` — the `IEditorPlugin`
  SubPanel registration seam (unchanged Wave-1 mechanism).

### Particle `.prt` codec spec reference (PROD-W2-PRT)
- `D:/Code/swg-client-v2/src/...clientParticle` (the C++ `clientParticle` library) — the format spec
  reference Utinni ports the typed decode from (read-only; no runtime dependency — see
  `project_swg_client_v2_reference`).
- `D:/Code/swg-client-v2/src/compile/win32/ClientEffectEditor/` — the original Qt client-effect editor
  (reference for which fields are author-relevant).

### Presentation residuals (folded todos — full edge-case matrices + prime-suspect analysis)
- `.planning/todos/pending/swg-window-resize-fullscreen-edge-cases.md` — RESID-04 matrix, live
  observations, prime-suspect root cause, no-Reset constraint, cross-refs.
- `.planning/todos/pending/phase10-stringtable-sc3-live-reload-residual.md` — RESID-03 Step-7 +
  stale-crc checks, badge-candor disposition, standing automated proxies.
- `.planning/debug/chat-open-d3d9-fullscreen.md` — UNRESOLVED D3D9 exclusive-fullscreen detach
  session (RESID-04 prime suspect; fold into the fix).

### Architecture seams & constraints (from prior phases / auto-memory)
- `feedback_d3d9_reset_third_party` — never `Reset` SWG's device; resize the window instead (D-13).
- `feedback_imgui_embedded_d3d9_rt_space` — RT-space mouse/DisplaySize mapping for the embedded window.
- `project_swg_cursor_clip_deadzone` — SWG's own `ClipCursor` right-edge dead zone; interacts with resize.
- `project_phase14_mcp_server` / `project_phase13_cli_verbs` — MCP-dispatches-to-CLI, zero format logic
  in the server; 16-verb CommandLineParser cap; atomic apply-save-* verbs.
- Phase 8 singleton hide-not-dispose SubPanel pattern (CON-M-01/02 SPI, CON-T-05 `*Impl` separation).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SnapshotPanel` + `WorldSnapshotImpl` + `WorldSnapshotReaderWriter`: the entire WorldSnapshot
  editing core already exists (load/save/saveAs/reload, per-node targeting + gizmo, add/remove). The
  Wave-2 delta is a list/table view + multi-select bulk ops layered over this — no new format code.
- `UtinniCoreDotNet/Formats/*` (Datatable, StringTable, Iff, Tre): the codec structure pattern the new
  `.prt` codec follows; `Formats/Datatable` grid + `Editing/*EditCommands` are the closest analogs for
  the placements table and bulk-edit commands.
- Phase-13 `utinni-cli` verbs + Phase-14 `Utinni.Mcp` tools: the path the `.prt` read/summarize tools
  plug into (format logic in `UtinniCoreDotNet`, CLI wraps it, MCP dispatches to CLI).

### Established Patterns
- MEF `IEditorPlugin.GetSubPanels()` Wave-1 seam — unchanged; both new editors register through it.
- Singleton **hide-not-dispose** for SubPanels (Phase 8); `*Impl` separation (CON-T-05).
- Degrade-don't-abort codec philosophy (OT multichunk) — the model for `.prt` raw-preserve of unknowns.
- Event-handler add/remove around `ValueChanged` to avoid feedback loops (see `SnapshotPanel`
  `UpdateSelectedNodeControlsPosition`); relevant to the new table's selection sync.

### Integration Points
- New Particle SubPanel registers via `Plugin.cs` alongside the existing editors.
- `.prt` codec in `UtinniCoreDotNet` ← consumed by both a new `utinni-cli` verb set and the in-TJT
  Particle editor.
- Live preview hooks into the running client-effect/particle manager (the open research item, D-09).
- RESID-04 fix lives in the D3D9 presentation / window-management layer (intercept SWG's fullscreen
  mode switch); touches the FormMain/PanelGame embed + RT-space mapping.

</code_context>

<specifics>
## Specific Ideas

- WorldSnapshot table should feel like the Datatable editor's grid (consistency across TJT editors).
- Particle preview is the "flashy" demo moment — hot-retrigger so an edit visibly changes the live
  effect in-scene, not just a static save.
- RESID-04 recovery hint from the maintainer's live session: dropping SWG's resolution from fullscreen
  back to default re-establishes the windowed/embed path — strong evidence the fix is to intercept the
  exclusive-fullscreen mode switch (keep it windowed-embedded), matching the Phase-B owned-popup model.
- Maintainer triaged RESID-04 as "low priority, likely not a hard find" — keep the fix targeted.

</specifics>

<deferred>
## Deferred Ideas

- **Terrain editor `PROD-W2-TRN` (`.trn`)** — explicitly deferred to v2.1 (heavier codec; the v2.1
  editor lead). Not in this phase.
- **Particle prompt-to-mutate AI** (AI writes `.prt` params directly) — deferred; V1 is read-assist
  only (D-07). Revisit once the typed codec + manual editor are proven.
- **Deliberate detached-fullscreen mode with clean re-attach** — the RESID-04 alternative to
  intercept/suppress; only revisit if the targeted suppress approach proves wrong.
- **`.prt` fixture corpus / golden round-trip tests** beyond what's needed to validate the typed
  decode — broader fixture coverage is a Tier-2 follow-up (no fixtures exist today).

### Folded Todos
- **`swg-window-resize-fullscreen-edge-cases.md`** (RESID-04) — folded into D-12/D-13. The todo's
  edge-case matrix, live observations, and prime-suspect analysis are the implementation spec for the
  RESID-04 work.
- **`phase10-stringtable-sc3-live-reload-residual.md`** (RESID-03) — folded into D-14. The todo's
  Step-7 scene-change + stale-`sourceCrc` checks and badge-candor disposition are the spec for the
  RESID-03 live observation.

</deferred>

---

*Phase: 15-wave-2-editors-worldsnapshot-particle-presentation-residuals*
*Context gathered: 2026-06-07*
