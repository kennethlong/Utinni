---
phase: 07-tjt-subpanel-tre-browser-read-only
plan: 04b
subsystem: formats-ui
tags: [decoders, appearance, mesh, skeleton, animation, shader, ui-page, decode-iff, tre-browser, structured-view, splitcontainer]

requires:
  - phase: 07-tjt-subpanel-tre-browser-read-only
    provides: "07-04a Formats/Decoders (DataTable/StringTable/ObjectTemplate) + decode-iff verb + IffPayloadCursor"
  - phase: 07-tjt-subpanel-tre-browser-read-only
    provides: "07-03 TreDetailPane (pnlStructured placeholder, ShowReadable/ShowUnsupportedRaw, LoadIff chunk tree)"
provides:
  - "Formats/Decoders/AppearanceSummary: mesh/skeletal-mesh/skeleton/animation structural counts (MESH SPS/CNT shader + VTXA vertex; SKMG INFO offsets 16/28; SKTM INFO joint count + NAME joint names; KFAT/CKAT INFO frame count)"
  - "Formats/Decoders/IffStructureSummary: lightweight RootTag + child-count/tags summary; classifies shader (SSHT/CSHD locked tag) and UI-page (.gui/ui path hint, since UI pages are text not IFF); any-FORM no-throw fallback"
  - "decode-iff extended: MESH/SKMG/SKTM/KFAT/CKAT -> appearance, SSHT/CSHD -> structure(shader), non-IFF .gui text -> ui-page"
  - "TreDetailPane: all five structured-view families rendered row-capped (5000) via the shared decoders, in a three-section (tree / table / raw-bytes) two-splitter layout with overflow scrollbars"
affects: [08, 09, 10, 11]

tech-stack:
  added: []
  patterns:
    - "Mesh/skeleton/anim structural counts are read from the INFO/CNT chunks at the swg_blender-documented offsets (LE scalars); unknown roots return null (no throw); joint-name allocation is forged-count guarded"
    - "Shader is recognized by its LOCKED root FORM tag (SSHT/CSHD); UI pages are TEXT (not IFF) so the UI-page class is recognized by the .gui/ui path-extension hint — the any-FORM path is the unrecognized no-throw fallback only"
    - "Detail-pane sections (chunk tree / structured table / raw bytes) are independently resizable via two nested horizontal SplitContainers; a Dock.Fill control must be at the FRONT (do NOT SendToBack it — that docks it first and starves siblings)"
    - "Hand-coded SplitContainer: set a definite Size BEFORE SplitterDistance or the ctor throws and the plugin's MEF load fails (07-02 gotcha)"

key-files:
  created:
    - "Utinni/UtinniCoreDotNet/Formats/Decoders/AppearanceSummary.cs"
    - "Utinni/UtinniCoreDotNet/Formats/Decoders/IffStructureSummary.cs"
  modified:
    - "Utinni/UtinniCoreDotNet/UtinniCoreDotNet.csproj (2 explicit <Compile Include>)"
    - "Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs (mesh/shader dispatch + .gui text sniff)"
    - "Utinni/Utinni.Cli.Tests/Commands/DecoderTests.cs (mesh/skeleton/anim/shader/ui-page tests)"
    - "UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs (structured views + 3-section splitter layout)"
    - "UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs (.stf route to ShowStringTable)"

key-decisions:
  - "SWG UI pages (.gui) are TEXT, not IFF (no UI-page FORM tag exists — UILoader/UIPage). Per user decision, the UI-page class is recognized by the .gui/ui path-extension hint and shown as labeled text; IffStructureSummary still gives shaders (SSHT/CSHD) a real IFF structured summary. Reverses the plan's review-consensus-#1 'locked UI-page root FORM tag' premise."
  - "Shader root FORM tag is SSHT (static) / CSHD (customizable), NOT the plan's placeholder 'SHDR' — verified against StaticShaderTemplate.cpp + a real 2d_bloom.sht."
  - "The detail pane is a THREE-section, two-splitter layout (tree / table / raw bytes) so resizing raw bytes never clobbers the tree (live-smoke request); replaces the fixed-dock stacking that caused the header-overlap + empty-region defects."

patterns-established:
  - "decode-iff is now the full CLI mirror of the browser's per-type decode path across all asset families (datatable/STF/object-template/mesh/shader/UI-page) — the Pitfall-7 CLI/UI lock-step is complete."

requirements-completed: [PROD-01, PROD-W1-TRE]

duration: ~3h (incl. live-smoke layout iteration)
completed: 2026-05-27
---

# Phase 7 Plan 04b: Deep Decoders + Structured Views Summary

**The mesh/skeleton/animation `AppearanceSummary` and the shader/UI-page `IffStructureSummary` complete the per-type decode set; `decode-iff` now dispatches every asset family; and the TRE Browser detail pane renders all five structured-view families row-capped via the SAME golden-tested decoders, in a three-section (tree / table / raw bytes) two-splitter layout with overflow scrollbars — verified live (user: "All looks good"). This completes Phase 7.**

## Live-smoke result (PROD-01 every-asset-class — APPROVED)
- User injected Utinni + TJT and confirmed the detail pane: datatable shows a column-per-column grid with typed cells (row-capped), string table shows id/name/text, object template shows declared base + local fields, mesh/skeleton/anim shows the count grid, shader/UI-page show the structured summary, and unrecognized types hide the structured section while the chunk tree + raw bytes still render.
- The three sections (chunk tree / structured table / raw bytes) are independently resizable via two splitters; expanding raw bytes no longer clobbers the tree; each section scrolls on overflow. **Approved after two layout iterations** (see Deviations).
- Real-asset CLI parity spot-checks: `arrow_disk.msh` → 160 vertices / 1 shader; `2d_bloom.sht` → SSHT shader; plus 07-04a's datatable/STF.

## Task Commits
**Utinni:**
1. **AppearanceSummary + IffStructureSummary + decode-iff dispatch** (Task 1) — `5f139ca` (feat) — both decoders pure + bounds-checked; mesh/skeleton/anim counts, shader by locked SSHT/CSHD tag, UI-page by path hint; 31 decoder tests green.

**UtinniPlugins:**
2. **Row-capped structured views** (Task 2) — `a818dcc` (feat) — all five families in `pnlStructured` via the shared decoders + `.stf` route in `FormTreBrowser`.
3. **Live-smoke layout fixes** — `8f98405` (a wrong Fill-primary attempt) → `69b1fd0` (revert; z-order lesson) → `684aea5` (the final three-section two-splitter layout + overflow scrollbars).

## Deviations from Plan
1. **[Rule 1 — format reality, user-decided] UI pages are TEXT, not IFF.** The plan's review-consensus #1 assumed a "LOCKED UI-page root FORM tag"; no such tag exists (SWG UI is text via `UILoader`/`UIPage`, and there are zero `.gui` IFF assets in the client/serverdata). Per the user's call, the UI-page class is recognized by the `.gui`/`ui/` path-extension hint (the plan's own "AND/OR the path/ext hint" clause) and shown as labeled "UI page (text)"; the shader class keeps a real IFF structured summary. `decode-iff` sniffs a non-IFF `.gui` and emits a `ui-page` text result.
2. **[Rule 1] Shader tag is SSHT/CSHD**, not the plan's placeholder "SHDR" — verified against the engine loaders + a real `.sht`.
3. **[Rule 1] `.stf` route added to `FormTreBrowser.cs`** (beyond the plan's TreDetailPane-only file list) because the string table is non-IFF (07-04a) and needs browser-side dispatch to `ShowStringTable` before the IFF check.
4. **[simplification] Joint "tree" rendered as a capped flat joint-name list** in the uniform structured ListView rather than a separate read-only TreeView — kept the structured section to one control type.
5. **[Rule 1 — live-smoke driven] Three-section two-splitter layout.** The plan said render in `pnlStructured`; the live smoke exposed that the fixed-dock stacking (chunk tree Fill + structured Bottom + hex panel) overlapped the table header and, in a wrong intermediate fix, collapsed sections. Reworked to two nested horizontal `SplitContainer`s (tree / table / raw bytes) with overflow scrollbars per the user's request. **Lesson captured:** a `Dock.Fill` control must be at the FRONT of the z-order — `SendToBack` makes it dock first and starve its siblings (the `8f98405`→`69b1fd0` round-trip).

## Issues Encountered
- **WinForms z-order on a Dock.Fill control:** sending a Fill control to the back docks it first and gives it the whole area, starving the Top/Bottom siblings (live-smoke symptom: chunk tree showed, structured region was empty). Resolved by the splitter layout, which sidesteps manual z-order entirely. (No outstanding issues.)
- The SplitContainer ctor-crash trap (07-02) was pre-empted by setting a definite `Size` before `SplitterDistance`.

## User Setup Required
None. (Optional: `SWG_LOOSE_IFF_DIR` exercises the env-gated real-asset decoder tests locally.)

## Next Phase Readiness
- **Phase 7 is COMPLETE** (6/6 plans). The TRE Browser ships as a TJT subpanel with: version-dispatch TRE reader (0004/0005/0006/5000/6000/COT2000), lazy 125k-path virtual tree + filter, on-demand payload resolve, the universal IFF chunk tree with honest `@offset`, and per-type structured views for datatable / string-table / object-template / mesh family / shader / UI-page — all golden-tested via `decode-iff` (CLI/UI lock-step).
- The decoder models + the reusable `LoadIff` chunk-tree surface are the read-only foundation Phases 8–11 (IFF/Datatable/Stringtable/Object-Template editors) make editable with no rework (D-13).
- No blockers. Suggested next: phase verification / `/gsd-verify-work`, or proceed to the next milestone phase.

---
*Phase: 07-tjt-subpanel-tre-browser-read-only*
*Completed: 2026-05-27*
