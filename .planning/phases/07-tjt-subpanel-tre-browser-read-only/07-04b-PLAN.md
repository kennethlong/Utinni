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
    - "Every decoder/summary is pure (no JSON, no console, no file-write) and is exercised by the decode-iff CLI verb with a golden test; the browser structured views call the same decoders (D-08, success criterion #4)"
    - "Unrecognized types return an empty summary (no throw) and the browser hides the structured view; the IFF chunk tree + hex peek still render (UI-SPEC)"
    - "All four structured views (datatable, STF, object-template, mesh family) PLUS the shader/UI-page summaries render in the detail pane via the SAME golden-tested Formats/Decoders the decode-iff CLI verb exercises (Pitfall 7 — no UI-only decode)"
    - "The reference split is honored: mesh/skeleton/anim from swg_blender (D-09); shader/UI-page structured summary via the universal IffReader (lightweight, FORM-tag + child-count)"
    - "swg_blender is a read-to-port format spec only — layout/algorithm ported to C#, no code/identifiers copied, no runtime dependency (D-02)"
    - "The decoders/summaries expose read-only output only — no write/authoring/export surface is added; DEC-A3 stays locked (D-01)"
    - "Each summary is structured as the read-only foundation later phases can build on with no rework (D-13)"
  artifacts:
    - path: "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/AppearanceSummary.cs"
      provides: "mesh/skeleton/animation structural-count summary decoder"
      contains: "class AppearanceSummary"
    - path: "D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/IffStructureSummary.cs"
      provides: "lightweight FORM-tag + child-count summary for shader (SHDR) and UI-page assets (PROD-01 criterion #3 coverage)"
      contains: "class IffStructureSummary"
    - path: "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs"
      provides: "structured-view ListViews/label grids dispatched on root FORM tag in pnlStructured (datatable/STF/template/mesh + shader/UI-page summary)"
      contains: "pnlStructured"
  key_links:
    - from: "Utinni.Cli/Commands/DecodeIffCommand.cs"
      to: "UtinniCoreDotNet.Formats.Decoders.AppearanceSummary / IffStructureSummary"
      via: "decode verb dispatch on mesh/skeleton/anim + shader/UI-page root tags"
      pattern: "AppearanceSummary|IffStructureSummary"
    - from: "TreDetailPane.cs"
      to: "UtinniCoreDotNet.Formats.Decoders.*"
      via: "structured-view rendering calls the same decoders the CLI verb exercises"
      pattern: "Decoder|AppearanceSummary|IffStructureSummary"
---

<objective>
Complete the deep per-type decode: add the mesh/skeleton/animation `AppearanceSummary` decoder (ported from swg_blender), add a lightweight `IffStructureSummary` (FORM-tag + child-count) for the shader and UI-page asset classes so PROD-01 criterion #3's "UI page" and "shader" are honestly covered by a structured view, extend the `decode-iff` verb to dispatch them, and render ALL structured views (datatable / STF / object-template / mesh family / shader+UI-page summary) in the detail pane's `pnlStructured` via the SAME golden-tested decoders. This is the second of the two split plans replacing the original oversized 07-04 (codex HIGH).

Purpose: This closes the cross-AI review's PROD-01 coverage gap (item 8 — 07-04 shipped no UI-page decoder and no dedicated shader structured view, putting criterion #3 at risk of "chunk tree = enough"). Rather than narrow the ROADMAP criterion, this plan adds the lightweight structured summaries so criterion #3 stays honestly met, and adds them to the live-smoke verification. It also completes D-12's type-specific structured views and the CLI/browser lock-step (Pitfall 7) for the graphics + UI families.
Output: `AppearanceSummary.cs` + `IffStructureSummary.cs` (framework), the extended `decode-iff` dispatch + goldens, and the structured-view rendering in `TreDetailPane`.
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
  ObjectTemplateDecoder.Decode(IffDocument) -> ObjectTemplateView
  DecoderException { DecoderError Kind }
  decode-iff verb dispatches root SubTypeId -> the matching decoder (extend with mesh/shader/UI-page)

Mesh/skeleton/anim reference (D-09 — PORT from swg_blender mesh/skeleton/anim readers):
  AppearanceSummary -> MESH/SKMG (vertex/shader counts), SKTM (joint count + joint names), KFAT/anim (frame count)

Shader / UI-page summary (lightweight, via the universal IffReader — review item 8):
  IffStructureSummary -> SHDR and UI-page root FORM tag + immediate child-FORM tags + child-count;
  no deep graphics decode required — FORM-tag + child-count is the honest structured view for these classes.

TreDetailPane (from plan 03): public Panel pnlStructured placeholder in section 4; LoadIff(IffDocument)
  already renders the chunk tree; Show* API exists. Render structured views into pnlStructured dispatched on root tag.

Theming: themed ListView (Details, Colors.PrimaryHighlight()/Colors.Font()/BorderStyle None), UtinniLabel grid,
  read-only TreeView for joint tree; NO Color.FromArgb literals.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: AppearanceSummary (mesh/skeleton/anim) + IffStructureSummary (shader/UI-page) + decode-iff dispatch + goldens</name>
  <files>D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/AppearanceSummary.cs, D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/IffStructureSummary.cs, D:/Code/Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs, D:/Code/Utinni/Utinni.Cli.Tests/Commands/DecoderTests.cs</files>
  <read_first>
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DataTableDecoder.cs (the sibling C# structure to copy — from 07-04a)
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DecoderException.cs (the kind-enum to reuse)
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Iff/IffContainerChunk.cs (SubTypeId / Children to walk for MESH/SKMG/SKTM/KFAT/SHDR/UI)
    - D:/Code/Utinni/Utinni.Cli/Commands/DecodeIffCommand.cs (the dispatch to extend — from 07-04a)
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-RESEARCH.md (Pitfall 6 endianness; reference split D-09; PROD-01 every-asset-class)
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-PATTERNS.md (AppearanceSummary section; No-Analog note — copy DataTableDecoder structure, port layout from swg_blender)
    - reference (read-to-port, NOT copy): D:/Code/swg-client-v2/tools/swg_blender/ mesh/skeleton/anim readers
  </read_first>
  <behavior>
    - Test: a MESH/SKMG IFF fixture decodes to structural counts (vertex count, shader count) without throwing.
    - Test: a SKTM skeleton fixture decodes to a joint count + a joint-name list/tree.
    - Test: a KFAT animation fixture decodes to a frame count.
    - Test: a SHDR shader fixture and a UI-page fixture each decode to an IffStructureSummary (root FORM tag + child-count + immediate child-FORM tag list) without throwing — proving criterion #3's shader + UI-page classes have a structured view.
    - Test: an unrecognized root FORM tag returns a null/empty summary from BOTH AppearanceSummary and IffStructureSummary (no throw — unknown types are normal).
    - Test: decode-iff dispatches MESH/SKMG/SKTM/KFAT to AppearanceSummary and SHDR/UI-page to IffStructureSummary and emits the schemaVersion:1 envelope; a forged count throws DecoderException, not OOM.
  </behavior>
  <action>
    Create `AppearanceSummary.cs` as a `public static` pure decoder (same structure as DataTableDecoder), porting the structural-count layout from the swg_blender mesh/skeleton/anim readers (D-09). Implement `public static AppearanceInfo Summarize(IffDocument doc)` dispatching on root `SubTypeId`: MESH/SKMG -> `{ int VertexCount, int ShaderCount }`; SKTM -> `{ int JointCount, IReadOnlyList<string> JointNames }`; KFAT/anim -> `{ int FrameCount }`. Return null/empty for unrecognized tags (do NOT throw). Bound every count with the division-form guard before allocating (joint-name list, frame array). Keep it pure (no JSON/console/file-write); LE scalars (Pitfall 6).

    Create `IffStructureSummary.cs` as a `public static` pure summary (review item 8 — keep criterion #3 honestly met for shader + UI-page WITHOUT a deep graphics decode). Implement `public static StructureInfo Summarize(IffDocument doc)` returning `{ string RootTag, int ChildCount, IReadOnlyList<string> ChildTags }` derived from `IffDocument.Root` (root FORM SubTypeId, count + tag list of immediate `IffContainerChunk.Children`). Recognize the SHDR shader root and the UI-page root (use the SWG UI-page root FORM tag from the IFF chunk structure; if the exact UI-page root tag is uncertain, recognize by extension hint passed in OR fall back to summarizing any FORM root — the point is a non-empty structured view for these classes). Return empty for a non-FORM/unrecognized root. Pure, bounds-checked, LE scalars.

    Extend the `decode-iff` verb dispatch (in `DecodeIffCommand.cs`) with a MESH/SKMG/SKTM/KFAT branch -> AppearanceSummary and a SHDR/UI-page branch -> IffStructureSummary, emitting the schemaVersion:1 envelope. Add `DecoderTests.cs` cases for each (fixtures acquired/synthesized as in 07-04a; the shader + UI-page summary cases and the unrecognized-tag no-throw case are required to prove criterion #3 coverage headlessly).
  </action>
  <verify>
    <automated>cd D:/Code/Utinni; dotnet test Utinni.Cli.Tests --filter "Decoder|DecodeIff"</automated>
  </verify>
  <acceptance_criteria>
    - `dotnet test Utinni.Cli.Tests --filter "Decoder|DecodeIff"` passes including the MESH/SKMG/SKTM/KFAT, the SHDR + UI-page summary, and the unrecognized-tag no-throw cases.
    - `AppearanceSummary.cs` and `IffStructureSummary.cs` exist with their classes + a Summarize(IffDocument) method; carry the provenance header (grep "original to Utinni under MIT"); both pure (no Newtonsoft/Console/File.Write).
    - decode-iff dispatches mesh family -> AppearanceSummary and shader/UI-page -> IffStructureSummary (grep both decoder names in DecodeIffCommand.cs).
    - A shader fixture and a UI-page fixture each decode to a non-empty StructureInfo (asserted) — criterion #3's shader + UI-page classes have a headless structured-view proof.
    - A forged count throws DecoderException (asserted), not OutOfMemoryException; unrecognized tags return empty without throwing (asserted).
  </acceptance_criteria>
  <done>The mesh/skeleton/anim summary AND the lightweight shader/UI-page structure summary land, are pure + bounds-checked + golden-tested via decode-iff, and give PROD-01 criterion #3's "UI page" and "shader" classes an honest structured view without deep graphics decode.</done>
</task>

<task type="auto">
  <name>Task 2: Structured-view rendering in TreDetailPane (all five view families via the shared decoders)</name>
  <files>D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs</files>
  <read_first>
    - D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs (the pnlStructured placeholder + LoadIff + Show* API from plan 03 to extend)
    - D:/Code/Utinni/UtinniCoreDotNet/Formats/Decoders/DataTableDecoder.cs, StringTableDecoder.cs, ObjectTemplateDecoder.cs (07-04a) + AppearanceSummary.cs, IffStructureSummary.cs (Task 1) — the decoders to call
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-UI-SPEC.md (Right region section 4: Datatable/STF/Object-template/Mesh structured views; unrecognized -> hide section 4)
    - .planning/phases/07-tjt-subpanel-tre-browser-read-only/07-PATTERNS.md (themed ListView reuse; theming caveat — no Color.FromArgb)
  </read_first>
  <action>
    In `TreDetailPane.cs`, render the structured view in the `pnlStructured` placeholder (section 4), dispatched on the root `IffContainerChunk.SubTypeId` after `LoadIff`/`ShowReadable`, calling the SAME `Formats/Decoders` classes the decode-iff verb exercises (Pitfall 7 — no UI-only decode logic):
    - DTII (datatable): a `ListView` (Details, themed `BackColor = Colors.PrimaryHighlight()`, `ForeColor = Colors.Font()`, `BorderStyle = None`) with one column per datatable column (header = column name + type) and one row per decoded row (via `DataTableDecoder.Decode`).
    - STF (string-table): a 2-column `ListView` (`String ID` | `Text`), one row per entry (via `StringTableDecoder.Decode`).
    - Object template: a 3-column `ListView` (`Field` | `Value` | `Inherited from`) (via `ObjectTemplateDecoder.Decode`).
    - Mesh/skeleton/animation: a label grid of structural counts (`UtinniLabel` rows: vertex/shader/joint/frame counts) plus a small read-only `TreeView` for the joint tree when present (via `AppearanceSummary.Summarize`).
    - Shader / UI-page: a small label grid showing the root FORM tag + child-count + immediate child-tag list (via `IffStructureSummary.Summarize`) — the structured view that makes criterion #3's shader + UI-page classes honestly covered, distinct from the universal chunk tree.
    - Unrecognized type: hide `pnlStructured` (section 4); the IFF chunk tree (section 3) and hex peek (section 5) still render (UI-SPEC).
    Wrap each decode in try/catch and fall back to hiding section 4 on a decoder exception (or route to the existing ShowParseFailure). All colors via `Colors.*()` — NO `Color.FromArgb` literals.
  </action>
  <verify>
    <automated>cd "D:/Code/UtinniPlugins/The Jawa Toolbox"; msbuild TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86 /t:Build /v:minimal</automated>
  </verify>
  <acceptance_criteria>
    - TheJawaToolboxDotNet builds Release/x86 with no errors.
    - `TreDetailPane.cs` renders structured views via the shared decoders: grep finds `DataTableDecoder`, `StringTableDecoder`, `ObjectTemplateDecoder`, `AppearanceSummary`, AND `IffStructureSummary` usage, plus a `ListView`.
    - Unrecognized types hide `pnlStructured` (grep for a `pnlStructured.Visible = false` path).
    - The shader/UI-page structured view is rendered from `IffStructureSummary` (grep) — distinct from the section-3 chunk tree.
    - Grep finds ZERO `Color.FromArgb` literals in the new/edited TreDetailPane structured-view code.
  </acceptance_criteria>
  <done>All five structured-view families (datatable, STF, object-template, mesh family, shader/UI-page summary) render in the detail pane via the same golden-tested Formats/Decoders, completing D-12 deep per-type decode and PROD-01 criterion #3 coverage with CLI/browser lock-step.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking-human">
  <name>Task 3: Live-SWG smoke — per-type structured views (incl. shader + UI-page) + decode-iff CLI parity (PROD-01 every-asset-class)</name>
  <action>After tasks 1-2 are auto-complete and both the CLI tests and the TJT build are green, pause for the human to inject Utinni + TJT into a live SWGEmu client and verify the structured views per the how-to-verify steps. This is the only manual step in this plan; all auto-verifiable work (decoder golden tests, CLI parity, build, grep gates) is done in tasks 1-2.</action>
  <what-built>The deep per-type structured views in the TRE Browser detail pane: a datatable shows rows/columns, a string-table shows id/text entries, an object template shows the inherited-field walk, a mesh/skeleton/animation shows structural counts, and a shader/UI-page shows the lightweight FORM-tag + child-count structured summary — all via the same golden-tested Formats/Decoders the decode-iff CLI verb exercises. (Live-host smoke — PROD-01 "every asset class" coverage including UI page + shader; no headless harness for the TJT UI host per VALIDATION.)</what-built>
  <how-to-verify>
    1. Build both repos and inject Utinni + TJT into a live SWGEmu client.
    2. Open the TRE Browser, select a `.tab` datatable entry (readable 0005/0006 archive); confirm the structured view shows a column-per-column ListView with typed cell values (not garbage / not byte-swapped counts).
    3. Select a `.stf` string-table entry; confirm the (String ID | Text) ListView populates, including any non-ASCII text rendering correctly.
    4. Select an object-template `.iff`; confirm the (Field | Value | Inherited from) ListView shows the inherited-field walk.
    5. Select a mesh/skeleton/animation asset; confirm the structural-count label grid (vertex/shader/joint/frame counts, joint tree) populates.
    6. Select a SHADER (.sht/SHDR) entry AND a UI-page entry; confirm each shows the structured summary (root FORM tag + child-count + child-tag list), NOT just the universal chunk tree — this is the criterion #3 "shader" + "UI page" coverage.
    7. Select an unrecognized type; confirm the structured section is hidden while the IFF chunk tree + hex peek still render.
    8. (CLI parity spot-check) Run `utinni-cli decode-iff <a datatable fixture>` and `decode-iff <a shader fixture>` and confirm the JSON matches what the panel shows for the same files.
  </how-to-verify>
  <resume-signal>Type "approved" or describe issues (e.g. datatable cells byte-swapped, STF non-ASCII mangled, shader/UI-page summary missing, structured view shown for unknown type).</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| IFF parse output -> AppearanceSummary / IffStructureSummary | The summaries read counts (vertex/shader/joint/frame, child-count) from attacker-influenceable IFF payloads; a forged count could drive over-allocation. |
| decode-iff argv -> summaries | User-supplied path; FileNotFound (exit 3) + exception envelope (exit 2) contract. |
| decoded summary -> TreDetailPane structured view | The UI renders the bounded decoder output only; a decoder exception hides section 4 rather than crashing. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-07-13 | Denial of Service | forged vertex/joint/frame/child counts driving over-allocation | mitigate | Division-form checked guard (`count > Data.Length / stride`) before allocating; reject negative counts; never `count * stride` as int. |
| T-07-14 | Tampering | out-of-bounds reads within a chunk payload | mitigate | Read scalars only within `IffLeafChunk.Data` bounds; checked cursor; Truncated on short read. |
| T-07-15 | Denial of Service | pathological IFF nesting reaching the summaries | mitigate | The shared IffReader enforces 64 MB cap + NestedChunkOverflow before the summaries run; summaries consume the bounded parse output only. |
| T-07-17 | Denial of Service | UI freeze rendering a huge structured ListView (many rows) | mitigate | The detail-pane decode runs in the existing off-thread AfterSelect path (07-03); a decoder exception hides section 4; the IFF reader's caps bound the row/count magnitude. |
| T-07-SC | Tampering | npm/pip/cargo installs | mitigate | No package installs (BCL + in-repo only; RESEARCH Package Legitimacy Audit). |
</threat_model>

<verification>
- `dotnet test Utinni.Cli.Tests --filter "Decoder|DecodeIff"` is green (mesh family + shader/UI-page summary + unrecognized-tag cases).
- TheJawaToolboxDotNet builds Release/x86.
- Summaries are pure (no JSON/console/file-write grep gate) and bounds-checked (forged-count throws).
- Structured views (all five families incl. shader/UI-page) call the shared decoders (no UI-only decode; Pitfall 7 grep gate).
- Live-host smoke (checkpoint) confirms PROD-01 every-asset-class coverage INCLUDING shader + UI page.
</verification>

<success_criteria>
- Mesh/skeleton/animation decode into structural-count views; shader + UI-page get a lightweight structured summary (D-12, PROD-01 criterion #3 kept intact — review item 8).
- IFF scalars read little-endian (Pitfall 6).
- Decoders/summaries pure + golden-tested via decode-iff; browser + CLI share one path (D-08, criterion #4).
- Reference split honored (D-09 — swg_blender for graphics; universal IffReader for shader/UI-page summary).
- Unrecognized types degrade gracefully (UI-SPEC).
- The decoders stay read-only — no write/authoring/export surface (D-01); foundation for later phases (D-13).
</success_criteria>

<output>
Create `.planning/phases/07-tjt-subpanel-tre-browser-read-only/07-04b-SUMMARY.md` when done.
</output>
