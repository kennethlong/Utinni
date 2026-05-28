# Phase 8: TJT subpanel — IFF Editor (read + write) - Context

**Gathered:** 2026-05-27
**Status:** Ready for execution (plans pass; reviews integrated)
**Last uplift:** 2026-05-28 — D-05 (V1 same-length-only + ClientMemory disposition) and D-06 (tiered acceptance) updated to match plans after cross-AI review (codex + cursor, both HIGH risk → resolved). See `08-REVIEWS.md` for the full review record and `08-01`/`08-05`/`08-06` plans' `assumes:` blocks for plan-level traceability.

<domain>
## Phase Boundary

A **read + write IFF editor**, shipped as an `IEditorPlugin` WinForms SubPanel **inside The Jawa Toolbox** (per DEC-C4). It makes the Phase-7 read-only IFF chunk-tree surface **editable** and ships the IFF **read/write primitives** that Phases 9–11 (datatable / string-table / object-template editors) layer typed editing on top of. The most-leveraged Wave-1 subpanel — its primitives are the foundation for the rest of Wave 1.

**In scope:** generic **chunk-level** IFF editing — editing leaf chunk **payload bytes** (hex / replace-from-file / inline text) and **tree structure** (add / remove / rename-retag / reorder / duplicate chunks; edit FORM sub-type tags); an `IffWriter` + a mutable edit model living framework-side next to the existing reader; a CLI round-trip verb with golden fixtures; **four save modes** (loose-override file, Save/Save-As, in-memory live patch, `.tre` repack); an editor-forced in-session client reload; editor-local undo/redo.

**Explicitly OUT of scope (deferred):** **format-specific typed editing** (datatable cells, STF entries, object-template fields) — that is exactly what Phases 9–11 add on top of these primitives. Art-asset write/authoring parity (mesh/skeleton/animation/shader) remains the deferred post-V1 milestone gated behind LOCKED DEC-A3.
</domain>

<decisions>
## Implementation Decisions

### Write-primitive placement & sharing
- **D-01:** **IFF write primitives ship framework-side** in `UtinniCoreDotNet/Formats/Iff/` (`IffWriter` + a mutable edit model), **next to the existing `IffReader`** — NOT in `TheJawaToolboxDotNet`. Rationale: one IFF code path shared by the CLI and TJT; consistent with Phase 7's reader placement (D-08); `TheJawaToolboxDotNet` already references `UtinniCoreDotNet.dll`, so Phases 9–11 consume the primitives via that existing direct DLL reference (no inter-plugin version surface — honors DEC-C4's real intent). **ROADMAP RECONCILIATION REQUIRED:** Phase 8 Success Criterion 5 currently reads "IFF primitives are exported from `TheJawaToolboxDotNet`." Amend its wording to "exported from a shared, non-plugin assembly (`UtinniCoreDotNet/Formats/Iff`) that Phases 9–11 reference directly." The *intent* (no public-API/plugin-version concern) is satisfied; only the literal location wording changes.
- **D-02:** **Add a CLI round-trip verb + golden fixtures** (e.g. `utinni-cli roundtrip-iff`, or a `--write`/round-trip mode on the IFF path) that parses → (optionally mutates) → serializes → re-parses and asserts **byte-exact identity for untouched chunks**. This is the **automated gate for Success Criterion 4** (max-harness-over-manual-smoke), exercising the same framework-side write path the subpanel uses — mirrors the Phase-4 `inspect-iff`/`decode-iff` golden pattern.

### Editing surface & scope (Phase 8 ↔ 9/10/11 boundary)
- **D-03:** **Chunk-level editing only.** The user edits (a) leaf chunk **payload bytes** and (b) **tree structure**: add / remove / rename-retag / reorder / duplicate chunks, and edit FORM sub-type tags. **No format-specific field parsing** — typed editing (datatable/STF/object-template) is the explicit deliverable of Phases 9–11 built on these primitives. Matches the roadmap goal "Foundational read/write subpanel over IFF chunks."
- **D-04:** **Leaf payload editing offers three modes:** (1) an **editable hex view** (Phase 7's read-only `txtHex` becomes editable for the selected leaf; length may grow/shrink) — the primary path; (2) **Replace bytes from file… / Export bytes to file…** (right-click a leaf) for large or externally-prepared payloads; (3) **inline text edit** when a payload is printable ASCII-ish (convenience; detection heuristic is planner's discretion).

### Save target & live reload
- **D-05:** **All four save modes are hard V1 must-haves** that gate phase completion:
  1. **Loose override file** in the client's load path (the standard SWG modder iterate loop; planner derives the exact override directory from the injected client config).
  2. **Save / Save-As** to an arbitrary path (and save-in-place when the IFF was opened from a loose file) — always-available baseline.
  3. **In-memory live patch** of the loaded IFF via the **CON-N-04 VirtualProtect bracket** (instant in-session feedback; volatile / lost on reload; touches mapped client memory — higher risk).
  4. **Repack into the source `.tre`** (full repack with CRC/TOC rebuild) — closest to edit-in-place; highest risk.
  > **PLAN-SPLIT FLAG:** This is a large, risk-bearing surface. The planner should split Phase 8 into multiple plans (and consider 8a/8b sub-phasing). The `.tre` repack (CRC/TOC rebuild) and the mapped-memory live patch (CON-N-04) each carry distinct failure modes and warrant **isolated plans** with their own verification. Files opened from the TRE Browser come from read-only packed `.tre`, so for those, save mode 1/2/4 apply (not in-place).
  >
  > **V1 SAME-LENGTH-ONLY (post-cross-AI-review, 2026-05-28):** live-patch (mode 3) refuses any rewritten IFF whose serialized length differs from the original mapped length (both growth AND shrink). Refusing growth is necessary; refusing shrink avoids stale tail bytes after the new EOF inside the mapped region. Shrink-safe live patches (zero-fill or length-field update) are a documented post-V1 milestone.
  >
  > **V1 ClientMemory open path (post-cross-AI-review, 2026-05-28):** no current Phase-8 open path constructs an `OpenSource.ClientMemory` provenance descriptor (mapped-memory address + length) for an opened IFF. 08-06 ships with the live-patch menu **DISABLED** behind an honest tooltip until a follow-up phase wires the discovery path. Acceptance for D-05.3 in Phase 8 is **reduced-mode: implementation-complete and unit-tested for its bounds gate, ready to be enabled by the follow-up phase**. Future phase TBD.
- **D-06:** **TIERED forced in-session client reload (post-cross-AI-review, 2026-05-28):** the editor surfaces the reload outcome candidly per asset class — it never pretends a class reloads when it doesn't:
  - **(a) Textures / shaders / terrain → in-session pass** via `Graphics.ReloadTextures()` / `GroundScene.ReloadTerrain()` (or equivalent direct hook). The client re-reads on the next frame.
  - **(b) Datatable / STF / object-template / unknown IFF → "reloads on next scene change."** The editor candidly tells the user the asset re-resolves on the next TJT-driven scene change (the existing chat-command parser callback path) — it does NOT fabricate speculative scene-setup triggers via `AddSetSceneCallback` (which is a notification hook, not a trigger).
  - **(c) No live client → "Unavailable."** The reload button is disabled with an honest tooltip when no injected client is running.
  >
  > **Rationale for the tier (recorded 2026-05-28):** the cross-AI reviewers (codex + cursor) both flagged the original "forces an in-session reload for all asset types" wording as a HIGH design hole — no concrete reload mechanism exists for datatable/STF/object-template in-session today, and inventing one via speculative scene-setup triggers would risk reentrancy. Tiered acceptance trades silent over-promise for explicit, honest UX. PROD-W1-IFF Criterion 2 ("client reloads correctly") is interpreted under this tier matrix.
  >
  > **Original wording (superseded):** "The editor forces an in-session client reload after a file-based save … investigate whether a client-side asset-reload / cache-invalidation hook exists in SWG/Utinni and how it interacts with the existing TJT-driven scene-change path. If no reload mechanism exists, designing one is its own research + risk item — surface a fallback (e.g. trigger a scene-change-style reload) if a direct hook is infeasible." The research resolved as: direct hooks exist only for textures/shaders/terrain; datatable/STF/object-template have no in-session reload — TIERED disposition adopted.

### Edit model & round-trip fidelity
- **D-07:** **Hybrid mutable DOM.** Each node retains its **original raw bytes** from the read. On save: untouched leaves **emit their original bytes verbatim** (guarantees byte-exact unedited chunks AND preserves the SWG no-pad quirk for free — Criterion 4 becomes near-tautological for untouched subtrees); edited / added leaves emit fresh bytes; **container lengths roll up bottom-up** from children. Handles both payload edits and structural ops. (The existing `IffDocument` is `sealed`/immutable — the mutable model is a sibling, not a mutation of the reader's output type.)

### Edit history
- **D-08:** **Editor-local undo/redo stack**, **independent** of Utinni's scene `UndoRedoManager`. IFF file edits are not scene state, so this avoids CON-M-05 entanglement entirely — a scene cleanup will not wipe IFF edit history, and IFF saves never touch the scene undo stack.

### Panel architecture & entry points
- **D-09:** **A dedicated, editable IFF Editor SubPanel**, separate from Phase 7's read-only TRE Browser detail pane. **Extract** Phase 7's chunk-tree control (`TreDetailPane.LoadIff` was built `public` + standalone for exactly this — D-13) into a **shared control** the new editor builds on; the **TRE Browser stays read-only**. Entry points (Criterion 2): **"Open in IFF Editor" hand-off** from a selected TRE entry, and a **file picker** for loose files. Phases 9–11 reuse the same shared chunk-tree control.

### Claude's Discretion
- **Exact UI layout** of the editor SubPanel (hex/tree/structural-op affordances, toolbar, context menus, dirty-state indicator) → defer to `/gsd-ui-phase 8` (UI-SPEC) and planner.
- **ASCII-ish detection heuristic** for the inline text edit mode (D-04.3).
- **Validation-before-save** behavior, conflict handling when a loose override already exists, large-file/perf guards, and "new IFF from scratch" — standard approaches; planner/researcher's call (raised but not locked; see Deferred).
- **CLI verb naming/shape** for the round-trip harness (D-02).
- **Plan decomposition** — strongly expected to be multi-plan given D-05 (see PLAN-SPLIT FLAG).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 8 roadmap / project decisions
- `.planning/ROADMAP.md` § Phase 8 — goal, success criteria (note **Criterion 5 reconciliation** per D-01), preservation guard-rails (CON-M-01/02 SPI, CON-T-05 `*Impl` separation, CON-M-05 UndoRedoManager scene-cleanup, CON-N-04 VirtualProtect bracket).
- `.planning/PROJECT.md` § Key Decisions — **DEC-C4** (subpanel-inside-TJT, LOCKED), **DEC-A3** (not-a-DCC-replacement, LOCKED — gates the deferred write-authoring milestone).
- `.planning/REQUIREMENTS.md` — **PROD-W1-IFF** (read+write IFF; "open, view chunk hierarchy, edit chunk content, save back to a file the live client reloads correctly; CLI shim covers `inspect-iff` with golden fixtures") and **PROD-02** (Wave-1 edit aggregate).
- `.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-CONTEXT.md` — Phase 7 decisions carried forward (D-01..D-13 there; esp. D-02/D-08/D-13).

### Existing Utinni assets to extend / reuse (this repo)
- `UtinniCoreDotNet/Formats/Iff/IffReader.cs` — the read-only EA-IFF-85 parser; the **write path is the sibling to add here** (D-01). Note its **detect-don't-assume pad handling** (SWG writes no pad) — the writer must preserve this (D-07).
- `UtinniCoreDotNet/Formats/Iff/IffDocument.cs` (`sealed`, immutable: `Root`, `AllNodesInPreorder`), `IffChunk.cs`, `IffContainerChunk.cs` (`SubTypeId`, `Children`), `IffLeafChunk.cs` (`Payload`), `IffParseException.cs` — the read model the mutable edit model mirrors.
- `Utinni.Cli/Commands/InspectIffCommand.cs`, `Utinni.Cli/Commands/DecodeIffCommand.cs` (+ `Utinni.Cli.Tests` golden fixtures) — the existing IFF CLI verbs + golden pattern the round-trip verb (D-02) follows.
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs` — **the surface to make editable**: `public void LoadIff(IffDocument)` + the `tvChunks` chunk tree + read-only `txtHex` hex pane. Extract the chunk tree into a shared control (D-09); make hex editable (D-04).
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs` (+ `.Designer.cs`) — the Phase-7 browser the "Open in IFF Editor" hand-off originates from (D-09).
- `UtinniCoreDotNet/UI/Controls/SubPanel.cs`, `SubPanelContainer.cs` — SubPanel base + container the editor registers into.
- `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs` — the MEF SPI (CON-M-01/02).
- `UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs` — `SubPanelContainer` registration (where the editor panel plugs in).
- `TheJawaToolboxDotNet.csproj` — references `UtinniCoreDotNet.dll` via `HintPath ..\..\..\Utinni\bin\$(Configuration)\UtinniCoreDotNet.dll` (the consumption path for D-01 primitives).

### swg-client-v2 reference (read-only spec/impl, NOT a runtime dep — D-02 of Phase 7)
- `../swg-client-v2/tools/swg_blender/swg_iff/reader.py` — IFF navigation reference (stack-based FORM/chunk, BE headers / LE payloads; mirrors C++ `Iff.cpp`). For the **write** path, the IFF write semantics (chunk framing, length/size rollup, FORM/PROP) reverse from the same `Iff.{h,cpp}` family.
- `../swg-client-v2/docs/research/iff-tre-codebase-map.md` — index of the C++ `Iff`/`TreeFile` loaders/readers/**writers** + reuse strategy. **Primary reference for the IFF write framing and the `.tre` repack (CRC/TOC) path** (D-05.4).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`Formats/Iff/IffReader` + `IffDocument` (UtinniCoreDotNet):** read path complete and golden-tested; the write path is a sibling to add (D-01). The reader's pad-detection logic encodes the SWG no-pad convention the writer must honor.
- **`TreDetailPane.LoadIff` + `tvChunks` + `txtHex` (TJT):** Phase 7 built the chunk tree `public`/standalone specifically so Phase 8 reuses it (D-13). Extract → shared control; make hex editable.
- **`Utinni.Cli` IFF verbs + golden fixtures:** the harness pattern the round-trip verb (D-02) extends.
- **`SubPanel`/`SubPanelContainer` + Utinni themed controls:** the WinForms host surface + dark theme (see `TreDetailPane` for the themed ListView/TreeView/hex patterns).

### Established Patterns
- `IEditorPlugin` MEF export → aggregation in `FormMain` (CON-M-01/02); `*Impl` separation (CON-T-05) for any native/managed split.
- **`[CallerMemberName]`/binary-compat caution:** adding NEW types to `UtinniCoreDotNet` is safe; do not change existing public signatures consumed by pre-built plugins without rebuilding them in the same commit.
- Game-thread vs UI-thread marshaling (`Control.Invoke` / `GameCallbacks.AddMainLoopCall`) for any live-client interaction (in-memory patch + forced reload, D-05.3/D-06).
- **CON-N-04 VirtualProtect bracket** is the required pattern for the in-memory live patch (D-05.3) — any write to mapped client memory must bracket with `VirtualProtect` save/restore.

### Integration Points
- New editable SubPanel registers via TJT `Plugin.cs` `SubPanelContainer`; "Open in IFF Editor" hand-off wires from `FormTreBrowser` selection.
- Loose-override save target derives from the injected client's `.tre` load/config path (where SWG resolves loose files with priority).
- Forced in-session reload (D-06) interacts with the **TJT-driven scene-change** asset-reload path — see the scene-change-via-TJT and naked-after-scene-change baseline notes.

</code_context>

<specifics>
## Specific Ideas

- Replaces the **SOE-era `IFFEditor`** (read+write side) — the editing experience parity target.
- The IFF editor is deliberately the **generic foundation**: Phases 9–11 are "add a typed view + typed edit on top of these exact primitives," not new parsers.
- Criterion 4's "no corruption of unedited chunks" is treated as a **byte-exact** guarantee, made structural by D-07 (untouched nodes re-emit original bytes) and gated by the D-02 round-trip CLI harness.
</specifics>

<deferred>
## Deferred Ideas

- **Format-specific typed editing** (datatable cells, STF entries, object-template fields) → Phases 9, 10, 11 respectively, built on Phase 8's primitives.
- **Art-asset WRITE/authoring parity** (mesh/skeleton/animation/shader) → post-V1 milestone gated behind LOCKED DEC-A3 (see Phase 7 deferred + `maya-exporter-parity-checklist.md`).
- **Validation-before-save / structural integrity warnings, conflict handling when a loose override already exists, large-file perf guards, and "new IFF from scratch"** — raised at wrap-up; not locked as V1 requirements. Planner may include lightweight versions at discretion; otherwise revisit in a follow-up.
- **ImGui chromeless HUD-overlay presentation** of the editor — optional later polish (per Phase 7 D-04 / the 06-01 directive).

### Reviewed Todos (not folded)
- `gamecallbacks-gc-av-flake-fix.md` — CI-stability flake, already resolved in 06-04. Keyword false-positive match (also reviewed-not-folded in Phase 7); unrelated to the IFF editor.
- `loader-lock-harness-flake-fix.md` — CI-stability flake, already resolved in 06-04. Keyword false-positive match (also reviewed-not-folded in Phase 7); unrelated to the IFF editor.

</deferred>

---

*Phase: 08-tjt-subpanel-iff-editor-read-write*
*Context gathered: 2026-05-27*
