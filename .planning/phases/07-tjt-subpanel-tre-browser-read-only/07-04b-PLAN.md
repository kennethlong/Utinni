---
phase: 07-tjt-subpanel-tre-browser-read-only
plan: 04b
type: execute
wave: 5
depends_on: ["07-03", "07-04a"]
files_modified:
  - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/AppearanceSummary.cs"
  - "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/IffStructureSummary.cs"
  - "D:/Code/Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs"
  - "D:/Code/Utinni/Utinni.Cli.Tests/Commands/DecoderTests.cs"
  - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs"
autonomous: false
requirements: [PROD-01, PROD-W1-TRE]

must_haves:
  truths:
    - "A mesh/skeletal-mesh/skeleton/animation IFF decodes to structural counts (vertex/shader/joint/frame counts, joint tree) (D-09/D-12)"
    - "A shader (SHDR) and a UI-page IFF decode to a lightweight structured summary (root FORM tag + child-count, key sub-FORM names) so PROD-01 criterion #3's 'UI page' and 'shader' asset classes are honestly covered by a structured view, not only the universal IFF chunk tree (ROADMAP criterion #3 kept intact; review item 8)"
    - "IffStructureSummary.Summarize accepts a virtual-path/extension HINT (Summarize(IffDocument doc, string virtualPathOrExt)) so the UI-page + shader asset classes are recognized by the LOCKED root FORM tag AND/OR the path/extension — the 'fall back to any FORM root' path is reserved ONLY for the unrecognized-type no-throw test, NOT the primary UI-page detection path (review consensus #1 / both reviewers)"
    - "The exact UI-page root FORM tag(s) are LOCKED during fixture authoring and asserted BY NAME in the UI-page golden test (e.g. the recorded tag in a test-name + comment), so criterion #3's UI-page class has a concrete, named structured-view proof — not a generic 'any FORM gets a child-count' summary (review consensus #1)"
    - "Every decoder/summary is pure (no JSON, no console, no file-write) and is exercised by the decode-iff CLI verb with a golden test; the browser structured views call the same decoders (D-08, success criterion #4)"
    - "Unrecognized types return an empty summary (no throw) and the browser hides the structured view; the IFF chunk tree + hex peek still render (UI-SPEC)"
    - "All four structured views (datatable, STF, object-template, mesh family) PLUS the shader/UI-page summaries render in the detail pane via the SAME golden-tested Formats/Decoders the decode-iff CLI verb exercises (Pitfall 7 — no UI-only decode)"
    - "Large structured ListViews are ROW-CAPPED (review LOW / T-07-17): the detail-pane structured view renders the first N rows (e.g. 5000) and shows a '… {total} rows — showing first {N}' truncation label rather than adding hundreds of thousands of ListView rows synchronously"
    - "The reference split is honored: mesh/skeleton/anim from swg_blender (D-09); shader/UI-page structured summary via the universal IffReader (lightweight, FORM-tag + child-count)"
    - "swg_blender is a read-to-port format spec only — layout/algorithm ported to C#, no code/identifiers copied, no runtime dependency (D-02)"
    - "The decoders/summaries expose read-only output only — no write/authoring/export surface is added; DEC-A3 stays locked (D-01)"
    - "Both repos build before the wave is marked complete (review item 10 cross-repo CI gap): the executor builds Utinni Release/x86 (AppearanceSummary/IffStructureSummary + decode-iff land here) AND TheJawaToolboxDotNet Release/x86 against the pinned Utinni output"
    - "Each summary is structured as the read-only foundation later phases can build on with no rework (D-13)"
  artifacts:
    - path: "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/AppearanceSummary.cs"
      provides: "mesh/skeleton/animation structural-count summary decoder"
      contains: "class AppearanceSummary"
    - path: "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/IffStructureSummary.cs"
      provides: "lightweight FORM-tag + child-count summary for shader (SHDR) and UI-page assets, recognized via locked root tag + virtual-path/extension hint (PROD-01 criterion #3 coverage)"
      contains: "class IffStructureSummary"
    - path: "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs"
      provides: "row-capped structured-view ListViews/label grids dispatched on root FORM tag in pnlStructured (datatable/STF/template/mesh + shader/UI-page summary)"
      contains: "pnlStructured"
  key_links:
    - from: "Utinni.Cli/Commands/DecodeIffCommand.cs"
      to: "UtinniCoreDotNet.Formats.Decoders.AppearanceSummary / IffStructureSummary"
      via: "decode verb dispatch on mesh/skeleton/anim + shader/UI-page root tags (passing the path/ext hint to IffStructureSummary)"
      pattern: "AppearanceSummary|IffStructureSummary"
    - from: "TreDetailPane.cs"
      to: "UtinniCoreDotNet.Formats.Decoders.*"
      via: "structured-view rendering calls the same decoders the CLI verb exercises"
      pattern: "Decoder|AppearanceSummary|IffStructureSummary"
---

<objective>
Complete the deep per-type decode: add the mesh/skeleton/animation `AppearanceSummary` decoder (ported from swg_blender), add a lightweight `IffStructureSummary` (FORM-tag + child-count) for the shader and UI-page asset classes — recognized via a LOCKED root FORM tag plus a virtual-path/extension hint, so PROD-01 criterion #3's "UI page" and "shader" are honestly covered by a structured view (not a generic "any FORM" summary) — extend the `decode-iff` verb to dispatch them, and render ALL structured views (datatable / STF / object-template / mesh family / shader+UI-page summary) in the detail pane's `pnlStructured` via the SAME golden-tested decoders, ROW-CAPPED so a huge datatable cannot freeze the UI. This is the second of the two split plans replacing the original oversized 07-04 (codex HIGH).

Purpose: This closes the cross-AI review's PROD-01 coverage gap (consensus item 1 / item 8 — 07-04 shipped no UI-page decoder and no dedicated shader structured view, and the original summarizer carried no path/extension hint, putting criterion #3 at risk of "chunk tree = enough" or "any FORM = UI page"). Rather than narrow the ROADMAP criterion, this plan adds the lightweight structured summaries with a LOCKED UI-page tag + path/ext hint so criterion #3 stays honestly met, adds a row cap to the structured ListViews (review LOW / T-07-17), and adds them to the live-smoke verification. It also completes D-12's type-specific structured views and the CLI/browser lock-step (Pitfall 7) for the graphics + UI families.
Output: `AppearanceSummary.cs` + `IffStructureSummary.cs` (framework, the latter with the path/ext-hint signature), the extended `decode-iff` dispatch + goldens (with the UI-page tag locked + asserted by name), and the row-capped structured-view rendering in `TreDetailPane`.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-CONTEXT.md
@.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-RESEARCH.md
@.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-PATTERNS.md
@.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-UI-SPEC.md
@.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-03-SUMMARY.md
@.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-04a-SUMMARY.md

<interfaces>
<!-- Shared-core contracts the summaries consume + the structured-view host from plan 03. -->

Formats/Iff model (consume — do NOT add a new chunk-walker):
  IffDocument { IffChunk Root; IReadOnlyList<IffChunk> AllNodesInPreorder }
  IffContainerChunk : IffChunk { string TypeId; string SubTypeId; IReadOnlyList<IffChunk> Children }
  IffLeafChunk : IffChunk { string TypeId; byte[] Data }   // payload scalars are LITTLE-endian
  IffReader.Read(string|Stream) -> IffDocument

Decoders from 07-04a (consume + dispatch alongside):
  DataTableDecoder.Decode(IffDocument) -> DataTable
  StringTableDecoder.Decode(IffDocument) -> StfTable
  ObjectTemplateDecoder.Decode(IffDocument) -> ObjectTemplateView  (bounded: declared base + local fields)
  DecoderException { DecoderError Kind }
  decode-iff verb dispatches root SubTypeId -> the matching decoder (extend with mesh/shader/UI-page)

Mesh/skeleton/anim reference (D-09 — PORT from swg_blender mesh/skeleton/anim readers):
  AppearanceSummary -> MESH/SKMG (vertex/shader counts), SKTM (joint count + joint names), KFAT/anim (frame count)

Shader / UI-page summary (lightweight, via the universal IffReader — review consensus #1 / item 8):
  IffStructureSummary.Summarize(IffDocument doc, string virtualPathOrExt) -> StructureInfo
    Recognizes SHDR (shader) by its locked root FORM tag, and the UI-page asset class by the LOCKED
    UI-page root FORM tag(s) AND/OR the virtual-path/extension hint (.gui / ui/ prefix per the SWG layout).
    The hint lets the UI pass meta.Path; the CLI passes the verb's path. "Any FORM root" is the
    UNRECOGNIZED-type no-throw fallback ONLY, never the primary UI-page detection path.

TreDetailPane (from plan 03): public Panel pnlStructured placeholder in section 4; LoadIff(IffDocument)
  already renders the chunk tree; Show* API exists (incl. ShowUnsupportedRaw). Render row-capped structured
  views into pnlStructured dispatched on root tag.

Theming: themed ListView (Details, Colors.PrimaryHighlight()/Colors.Font()/BorderStyle None), UtinniLabel grid,
  read-only TreeView for joint tree; NO Color.FromArgb literals.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: AppearanceSummary (mesh/skeleton/anim) + IffStructureSummary (shader/UI-page, locked tag + path/ext hint) + decode-iff dispatch + goldens</name>
  <files>D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/AppearanceSummary.cs, D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/IffStructureSummary.cs, D:/Code/Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs, D:/Code/Utinni/Utinni.Cli.Tests/Commands/DecoderTests.cs</files>
  <read_first>
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DataTableDecoder.cs (the sibling C# structure to copy — from 07-04a)
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DecoderException.cs (the kind-enum to reuse)
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/IffContainerChunk.cs (SubTypeId / Children to walk for MESH/SKMG/SKTM/KFAT/SHDR/UI)
    - D:/Code/Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs (the dispatch to extend — from 07-04a; thread the path/ext hint into the IffStructureSummary call)
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-RESEARCH.md (Pitfall 6 endianness; reference split D-09; PROD-01 every-asset-class)
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-PATTERNS.md (AppearanceSummary section; No-Analog note — copy DataTableDecoder structure, port layout from swg_blender)
    - reference (read-to-port, NOT copy): D:/Code/swg-client-v2/tools/swg_blender/ mesh/skeleton/anim readers; the SWG UI-page (.gui) root FORM tag in swg_iff / the engine UI loaders to LOCK the tag
  </read_first>
  <behavior>
    - Test: a MESH/SKMG IFF fixture decodes to structural counts (vertex count, shader count) without throwing.
    - Test: a SKTM skeleton fixture decodes to a joint count + a joint-name list/tree.
    - Test: a KFAT animation fixture decodes to a frame count.
    - Test: a SHDR shader fixture decodes to an IffStructureSummary (root FORM tag + child-count + immediate child-FORM tag list) recognized by the locked SHDR root tag.
    - Test (review consensus #1 — UI-page tag LOCKED + asserted by name): a UI-page fixture authored with the LOCKED UI-page root FORM tag decodes to a non-empty IffStructureSummary; the test asserts the recognized root tag BY NAME (the exact tag recorded in the fixture, in the test name + a comment) AND asserts recognition succeeds via the locked tag and/or the virtual-path/extension hint — NOT via the any-FORM fallback.
    - Test: an unrecognized root FORM tag returns a null/empty summary from BOTH AppearanceSummary and IffStructureSummary (no throw — unknown types are normal); the any-FORM child-count fallback is exercised ONLY in this unrecognized-type no-throw case (review consensus #1), not for UI-page detection.
    - Test: decode-iff dispatches MESH/SKMG/SKTM/KFAT to AppearanceSummary and SHDR/UI-page to IffStructureSummary (passing the path/ext hint) and emits the schemaVersion:1 envelope; a forged count throws DecoderException, not OOM.
  </behavior>
  <action>
    Create `AppearanceSummary.cs` as a `public static` pure decoder (same structure as DataTableDecoder), porting the structural-count layout from the swg_blender mesh/skeleton/anim readers (D-09). Implement `public static AppearanceInfo Summarize(IffDocument doc)` dispatching on root `SubTypeId`: MESH/SKMG -> `{ int VertexCount, int ShaderCount }`; SKTM -> `{ int JointCount, IReadOnlyList<string> JointNames }`; KFAT/anim -> `{ int FrameCount }`. Return null/empty for unrecognized tags (do NOT throw). Bound every count with the division-form guard before allocating (joint-name list, frame array). Keep it pure (no JSON/console/file-write); LE scalars (Pitfall 6).

    Create `IffStructureSummary.cs` as a `public static` pure summary (review consensus #1 / item 8 — keep criterion #3 honestly met for shader + UI-page WITHOUT a deep graphics decode, and WITHOUT an ambiguous any-FORM escape hatch as the primary path). Implement `public static StructureInfo Summarize(IffDocument doc, string virtualPathOrExt)` returning `{ string RootTag, int ChildCount, IReadOnlyList<string> ChildTags, string RecognizedAs }` derived from `IffDocument.Root` (root FORM SubTypeId, count + tag list of immediate `IffContainerChunk.Children`). LOCK the recognition: recognize the SHDR shader by its root FORM tag, and recognize the UI-page asset class by the LOCKED UI-page root FORM tag(s) — determined during fixture authoring from swg_iff / the engine UI loaders — AND/OR by the `virtualPathOrExt` hint (e.g. a `.gui` extension or `ui/` path prefix). Set `RecognizedAs` to "shader" / "ui-page" / "" accordingly. Reserve "summarize ANY FORM root" ONLY for the unrecognized-type case so a non-empty StructureInfo is returned without throwing — it is NOT how UI-page or shader are recognized (review consensus #1). When the root is non-FORM/unrecognized AND no hint matches, return an empty StructureInfo (RecognizedAs == ""). Pure, bounds-checked, LE scalars.

    Extend the `decode-iff` verb dispatch (in `DecodeIffCommand.cs`) with a MESH/SKMG/SKTM/KFAT branch -> AppearanceSummary and a SHDR/UI-page branch -> IffStructureSummary, passing the verb's input path as the `virtualPathOrExt` hint, emitting the schemaVersion:1 envelope. Add `DecoderTests.cs` cases for each (synthesized minimal-contract fixtures, labeled smoke as in 07-04a; the shader summary, the UI-page summary with the LOCKED tag asserted by name, and the unrecognized-tag no-throw any-FORM-fallback case are required to prove criterion #3 coverage headlessly). When authoring the UI-page fixture, RECORD the exact root tag used in the fixture in a comment + the test name (e.g. `Summarize_UiPage_<TAG>_RecognizedByLockedTag`).
  </action>
  <verify>
    <automated>cd D:/Code/Utinni; dotnet test Utinni.Cli.Tests --filter "Decoder|DecodeIff"</automated>
  </verify>
  <acceptance_criteria>
    - `dotnet test Utinni.Cli.Tests --filter "Decoder|DecodeIff"` passes including the MESH/SKMG/SKTM/KFAT, the SHDR + UI-page summary (locked tag), and the unrecognized-tag no-throw cases.
    - `AppearanceSummary.cs` and `IffStructureSummary.cs` exist with their classes; carry the provenance header (grep "original to Utinni under MIT"); both pure (no Newtonsoft/Console/File.Write).
    - `IffStructureSummary.Summarize` has the path/ext-hint signature `Summarize(IffDocument doc, string virtualPathOrExt)` (grep the two-arg signature — review consensus #1) and exposes `RecognizedAs`; the decode-iff dispatch passes the input path as the hint (grep).
    - decode-iff dispatches mesh family -> AppearanceSummary and shader/UI-page -> IffStructureSummary (grep both decoder names in DecodeIffCommand.cs).
    - The UI-page test LOCKS the root tag: the test name + a comment record the exact UI-page root FORM tag, and the test asserts recognition happens via the locked tag / path-ext hint (grep the recorded tag in the test name) — NOT via the any-FORM fallback; a separate unrecognized-type test exercises the any-FORM no-throw fallback (grep both tests).
    - A shader fixture and a UI-page fixture each decode to a non-empty StructureInfo with `RecognizedAs` set (asserted) — criterion #3's shader + UI-page classes have a headless, named structured-view proof (review consensus #1).
    - A forged count throws DecoderException (asserted), not OutOfMemoryException; unrecognized tags return empty (RecognizedAs == "") without throwing (asserted).
  </acceptance_criteria>
  <done>The mesh/skeleton/anim summary AND the lightweight shader/UI-page structure summary land, are pure + bounds-checked + golden-tested via decode-iff, recognize UI-page + shader via a LOCKED root tag + path/ext hint (the any-FORM path reserved for the unrecognized no-throw case), and give PROD-01 criterion #3's "UI page" and "shader" classes an honest, named structured view without deep graphics decode.</done>
</task>

<task type="auto">
  <name>Task 2: Row-capped structured-view rendering in TreDetailPane (all five view families via the shared decoders)</name>
  <files>D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs</files>
  <read_first>
    - D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs (the pnlStructured placeholder + LoadIff + Show* API from plan 03 to extend)
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DataTableDecoder.cs, StringTableDecoder.cs, ObjectTemplateDecoder.cs (07-04a) + AppearanceSummary.cs, IffStructureSummary.cs (Task 1) — the decoders to call
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-UI-SPEC.md (Right region section 4: Datatable/STF/Object-template/Mesh structured views; unrecognized -> hide section 4)
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-PATTERNS.md (themed ListView reuse; theming caveat — no Color.FromArgb)
  </read_first>
  <action>
    In `TreDetailPane.cs`, render the structured view in the `pnlStructured` placeholder (section 4), dispatched on the root `IffContainerChunk.SubTypeId` after `LoadIff`/`ShowReadable`, calling the SAME `Formats/Decoders` classes the decode-iff verb exercises (Pitfall 7 — no UI-only decode logic). Define `const int StructuredRowCap = 5000` and apply it to every ListView-backed view (review LOW / T-07-17): add only the first `StructuredRowCap` rows and, when the decoded row count exceeds the cap, append a dimmed `… {total} rows — showing first {cap}` truncation label rather than adding all rows synchronously.
    - DTII (datatable): a `ListView` (Details, themed `BackColor = Colors.PrimaryHighlight()`, `ForeColor = Colors.Font()`, `BorderStyle = None`) with one column per datatable column (header = column name + type) and the first `StructuredRowCap` decoded rows (via `DataTableDecoder.Decode`), with the truncation label when exceeded.
    - STF (string-table): a 2-column `ListView` (`String ID` | `Text`), first `StructuredRowCap` entries (via `StringTableDecoder.Decode`), truncation label when exceeded.
    - Object template: a 3-column `ListView` (`Field` | `Value` | `Inherited from`) showing the declared base reference + local fields (via `ObjectTemplateDecoder.Decode` — bounded posture from 07-04a; the `Inherited from` column shows 'local' / the declared base name, NOT a resolved chain).
    - Mesh/skeleton/animation: a label grid of structural counts (`UtinniLabel` rows: vertex/shader/joint/frame counts) plus a small read-only `TreeView` for the joint tree when present, joint nodes capped at `StructuredRowCap` (via `AppearanceSummary.Summarize`).
    - Shader / UI-page: a small label grid showing the root FORM tag + child-count + immediate child-tag list (via `IffStructureSummary.Summarize(doc, meta.Path)` — pass the selected entry's virtual path as the hint so UI-page recognition matches the locked-tag/path-hint contract from Task 1) — the structured view that makes criterion #3's shader + UI-page classes honestly covered, distinct from the universal chunk tree. Render it only when `RecognizedAs` is "shader"/"ui-page".
    - Unrecognized type (empty summary / RecognizedAs == ""): hide `pnlStructured` (section 4); the IFF chunk tree (section 3) and hex peek (section 5) still render (UI-SPEC).
    Wrap each decode in try/catch and fall back to hiding section 4 on a decoder exception (or route to the existing ShowParseFailure). All colors via `Colors.*()` — NO `Color.FromArgb` literals.
  </action>
  <verify>
    <automated>cd "D:/Code/UtinniPlugins/The Jawa Toolbox"; msbuild TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86 /t:Build /v:minimal</automated>
  </verify>
  <acceptance_criteria>
    - TheJawaToolboxDotNet builds Release/x86 with no errors.
    - `TreDetailPane.cs` renders structured views via the shared decoders: grep finds `DataTableDecoder`, `StringTableDecoder`, `ObjectTemplateDecoder`, `AppearanceSummary`, AND `IffStructureSummary` usage, plus a `ListView`.
    - The shader/UI-page view calls `IffStructureSummary.Summarize` with the entry path as the hint (grep the two-arg call passing `meta.Path` — review consensus #1) and renders only when `RecognizedAs` is set.
    - Structured ListViews are row-capped: `StructuredRowCap` (= 5000) exists and a `showing first` truncation label is appended when exceeded (grep `StructuredRowCap` and `showing first` — review LOW / T-07-17).
    - Unrecognized types hide `pnlStructured` (grep for a `pnlStructured.Visible = false` path).
    - Grep finds ZERO `Color.FromArgb` literals in the new/edited TreDetailPane structured-view code.
  </acceptance_criteria>
  <done>All five structured-view families (datatable, STF, object-template, mesh family, shader/UI-page summary) render row-capped in the detail pane via the same golden-tested Formats/Decoders, with UI-page recognized via the locked-tag/path-hint contract, completing D-12 deep per-type decode and PROD-01 criterion #3 coverage with CLI/browser lock-step.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking-human">
  <name>Task 3: Build BOTH repos + Live-SWG smoke — per-type structured views (incl. shader + UI-page) + decode-iff CLI parity (PROD-01 every-asset-class)</name>
  <action>After tasks 1-2 are auto-complete, the executor MUST build BOTH repos before pausing (review item 10 cross-repo CI gap — the decoders land in Utinni; the structured-view rendering in TJT): build Utinni Release/x86 + run the decode-iff lane, AND build TheJawaToolboxDotNet Release/x86 against the pinned Utinni output. THEN pause for the human to inject Utinni + TJT into a live SWGEmu client and verify the structured views per the how-to-verify steps. This is the only manual step in this plan; all auto-verifiable work (decoder golden tests, CLI parity, both builds, grep gates) is done in tasks 1-2 + the build gate above.</action>
  <what-built>The deep per-type structured views in the TRE Browser detail pane: a datatable shows rows/columns (row-capped), a string-table shows id/text entries (row-capped), an object template shows the declared base + local fields (bounded posture), a mesh/skeleton/animation shows structural counts, and a shader/UI-page shows the lightweight FORM-tag + child-count structured summary recognized via the locked tag + path hint — all via the same golden-tested Formats/Decoders the decode-iff CLI verb exercises. (Live-host smoke — PROD-01 "every asset class" coverage including UI page + shader; no headless harness for the TJT UI host per VALIDATION.)</what-built>
  <how-to-verify>
    1. Build both repos and inject Utinni + TJT into a live SWGEmu client (the build-both gate above must already be green — review item 10).
    2. Open the TRE Browser, select a `.tab` datatable entry (readable 0005/0006 archive); confirm the structured view shows a column-per-column ListView with typed cell values (not garbage / not byte-swapped counts); for a large table, confirm the row cap + `showing first` label (review LOW).
    3. Select a `.stf` string-table entry; confirm the (String ID | Text) ListView populates, including any non-ASCII text rendering correctly.
    4. Select an object-template `.iff`; confirm the (Field | Value | Inherited from) ListView shows the declared base reference + local fields (Inherited from = 'local' / the base name — NOT a fully resolved inherited chain, the bounded Phase-7 posture).
    5. Select a mesh/skeleton/animation asset; confirm the structural-count label grid (vertex/shader/joint/frame counts, joint tree) populates.
    6. Select a SHADER (.sht/SHDR) entry AND a UI-page (.gui / ui/...) entry; confirm each shows the structured summary (root FORM tag + child-count + child-tag list), NOT just the universal chunk tree — and confirm the UI-page entry is recognized as a UI page (not a generic any-FORM summary). This is the criterion #3 "shader" + "UI page" coverage.
    7. Select an unrecognized type; confirm the structured section is hidden while the IFF chunk tree + hex peek still render.
    8. (CLI parity spot-check) Run `utinni-cli decode-iff <a datatable fixture>` and `decode-iff <a UI-page fixture>` and confirm the JSON matches what the panel shows for the same files (and the UI-page JSON shows RecognizedAs ui-page).
  </how-to-verify>
  <resume-signal>Type "approved" or describe issues (e.g. datatable cells byte-swapped, STF non-ASCII mangled, UI-page not recognized / shown as generic FORM, structured view shown for unknown type, huge table freezes without row cap).</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| IFF parse output -> AppearanceSummary / IffStructureSummary | The summaries read counts (vertex/shader/joint/frame, child-count) from attacker-influenceable IFF payloads; a forged count could drive over-allocation. |
| decode-iff argv -> summaries | User-supplied path; FileNotFound (exit 3) + exception envelope (exit 2) contract. |
| decoded summary -> TreDetailPane structured view | The UI renders the bounded, row-capped decoder output only; a decoder exception hides section 4 rather than crashing. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-07-13 | Denial of Service | forged vertex/joint/frame/child counts driving over-allocation | mitigate | Division-form checked guard (`count > Data.Length / stride`) before allocating; reject negative counts; never `count * stride` as int. |
| T-07-14 | Tampering | out-of-bounds reads within a chunk payload | mitigate | Read scalars only within `IffLeafChunk.Data` bounds; checked cursor; Truncated on short read. |
| T-07-15 | Denial of Service | pathological IFF nesting reaching the summaries | mitigate | The shared IffReader enforces 64 MB cap + NestedChunkOverflow before the summaries run; summaries consume the bounded parse output only. |
| T-07-17 | Denial of Service | UI freeze rendering a huge structured ListView (many rows) | mitigate | The detail-pane decode runs in the existing off-thread AfterSelect path (07-03); structured ListViews are ROW-CAPPED at StructuredRowCap (5000) with a `showing first N` truncation label so a huge datatable cannot add hundreds of thousands of rows synchronously; a decoder exception hides section 4; the IFF reader's caps bound the row/count magnitude. |
| T-07-SC | Tampering | npm/pip/cargo installs | mitigate | No package installs (BCL + in-repo only; RESEARCH Package Legitimacy Audit). |
</threat_model>

<verification>
- `dotnet test Utinni.Cli.Tests --filter "Decoder|DecodeIff"` is green (mesh family + shader/UI-page summary with locked-tag assertion + unrecognized-tag cases).
- BOTH repos build Release/x86 (Utinni + TheJawaToolboxDotNet) before the wave is marked complete (review item 10).
- Summaries are pure (no JSON/console/file-write grep gate) and bounds-checked (forged-count throws); IffStructureSummary takes the path/ext hint and UI-page is recognized by the locked tag (review consensus #1).
- Structured views (all five families incl. shader/UI-page) call the shared decoders (no UI-only decode; Pitfall 7 grep gate) and are row-capped (review LOW / T-07-17).
- Live-host smoke (checkpoint) confirms PROD-01 every-asset-class coverage INCLUDING shader + UI page recognized as such.
</verification>

<success_criteria>
- Mesh/skeleton/animation decode into structural-count views; shader + UI-page get a lightweight structured summary recognized via a LOCKED root tag + path/ext hint (D-12, PROD-01 criterion #3 kept intact — review consensus #1 / item 8).
- IFF scalars read little-endian (Pitfall 6); structured ListViews are row-capped (review LOW / T-07-17).
- Decoders/summaries pure + golden-tested via decode-iff with the UI-page tag locked + asserted by name; browser + CLI share one path (D-08, criterion #4).
- Reference split honored (D-09 — swg_blender for graphics; universal IffReader for shader/UI-page summary).
- Unrecognized types degrade gracefully (UI-SPEC); both repos build before wave complete (review item 10).
- The decoders stay read-only — no write/authoring/export surface (D-01); foundation for later phases (D-13).
</success_criteria>

<output>
Create `.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-04b-SUMMARY.md` when done.
</output>
</objective>
</content>
