---
phase: 07-tjt-subpanel-tre-browser-read-only
plan: 03
subsystem: ui
tags: [tjt, winforms, tre-browser, iff, detail-pane, offset, cross-repo, swgemu-5000]

requires:
  - phase: 07-tjt-subpanel-tre-browser-read-only
    provides: "07-01 shared TrePayloadResolver.TryResolve + TreArchiveIndex.TryGetDescriptor; Formats/Iff reader (IffReader.Read(Stream))"
  - phase: 07-tjt-subpanel-tre-browser-read-only
    provides: "07-02 FormTreBrowser shell with the SplitContainer Panel2 (pnlDetail) host + the SWG virtual-path TreeView whose leaf nodes carry the trie PathNode (FullPath)"
provides:
  - "IffChunk.OffsetBytes (long): byte offset of a chunk's TypeID header within the document, threaded from the existing IffReader.ReadChunk stream position — the ONLY framework change, no second parse pass"
  - "TreDetailPane (UserControl): metadata header (path/size/archive/CRC/compression + Copy path/CRC) + Colors.Secondary()-accented type/version banner + universal IFF chunk tree (TAG · size · @offset) + Consolas 4 KB hex peek"
  - "Four DISTINCT non-readable detail states: encrypted/enumerate-only, unsupported-but-readable-raw, parse-failure, empty (review item 12 — a readable non-FORM payload is NEVER mislabeled encrypted)"
  - "FormTreBrowser.AfterSelect: on-demand single-payload resolve off the UI thread via the single pinned TrePayloadResolver.TryResolve, dispatching to the detail-pane states"
  - "Public TreDetailPane.LoadIff(IffDocument) — reusable read-only IFF chunk-tree surface Phase 8's editor makes editable with no rework (D-13)"
affects: [07-04a, 07-04b, 08]

tech-stack:
  added: []
  patterns:
    - "Thread the parser's already-tracked stream position (ReadChunk chunkStart) into the model via the ctor as OffsetBytes — never add a second parse pass to surface an offset (Pitfall 7)"
    - "Reconstruct the selected virtual path from the trie PathNode.FullPath carried on the TreeNode.Tag, NOT a TreeNode.Parent text-walk (the lazy tree's display text carries a type-tag suffix; FullPath is the canonical key)"
    - "Resolve one payload on demand off-thread (Task) and marshal each detail-pane Show* call back via BeginInvoke; TryResolve's bool return IS the encrypted-vs-readable branch (no pre-branch on EnumerateOnly, no throwing Resolve overload)"
    - "Gate the 'encrypted' UX on the enumerate-only resolver signal ONLY; a readable payload that doesn't begin with FORM is the distinct unsupported-raw state showing real bytes (review item 12)"

key-files:
  created:
    - "UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Controls/TreDetailPane.cs"
  modified:
    - "Utinni/UtinniCoreDotNet/Formats/Iff/IffChunk.cs (new OffsetBytes + extended protected ctor)"
    - "Utinni/UtinniCoreDotNet/Formats/Iff/IffContainerChunk.cs (pass offset through base ctor)"
    - "Utinni/UtinniCoreDotNet/Formats/Iff/IffLeafChunk.cs (pass offset through base ctor)"
    - "Utinni/UtinniCoreDotNet/Formats/Iff/IffReader.cs (thread chunkStart into the constructed chunk)"
    - "Utinni/Utinni.Cli.Tests/Commands/InspectIffCommandTests.cs (OffsetBytes unit assertions; JSON goldens byte-identical)"
    - "UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs (AfterSelect resolve+dispatch; detail pane wired into Panel2)"
    - "UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj (explicit Compile Include for TreDetailPane.cs)"

key-decisions:
  - "IffChunk.OffsetBytes is kept UI-only (NOT surfaced in the inspect-iff JSON) so the LOCKED schemaVersion:1 goldens stay byte-identical; correctness is covered by a unit test asserting root OffsetBytes==0 and child header positions against synthetic-nested.iff."
  - "The selected path is reconstructed from the trie PathNode.FullPath on the leaf's Tag, not a TreeNode.Parent walk — the display text carries a type-tag suffix so a text-walk would corrupt the key (deviation from the plan's FormObjectBrowser parent-walk reference)."
  - "ShowEncrypted keeps a meta.Version branch but uses truthful copy: V6000 gets the 'Extract with TreeFileExtractor.exe' enumerate-only panel; the planned v5000 'layout not yet verified' string is OMITTED because v5000 is now readable (07-02 discovery) and never reaches ShowEncrypted — the else branch is a generic truthful enumerate-only note for any future lineage."

patterns-established:
  - "OffsetBytes-from-existing-position is the template for surfacing any parser-internal coordinate on the model without a re-parse; Phase 8's editor consumes the same OffsetBytes for write-back targeting."

requirements-completed: [PROD-W1-TRE, PROD-01]

duration: ~1.5h (incl. live-smoke verify)
completed: 2026-05-27
---

# Phase 7 Plan 03: TRE Browser Detail Pane Summary

**Selecting a TRE entry now resolves one payload on demand off-thread and renders a metadata header, a type/version banner, the universal IFF chunk tree with honest `TAG · size · @offset`, and a 4 KB Consolas hex peek — degrading cleanly to four distinct non-readable states (encrypted, unsupported-raw, parse-failure, empty) so one bad file never crashes the browser. The `@offset` is sourced from a new framework `IffChunk.OffsetBytes` threaded from the existing parser position, with no second parse pass.**

## Live-smoke result (PROD-01 — "view individual file metadata" + universal chunk tree)
- User reinjected Utinni + TJT into the live SWGEmu client, opened the TRE Browser, and **approved**: metadata header populates immediately on selection; the IFF chunk tree renders the FORM/chunk hierarchy with **real byte `@offset` values** (not zero/placeholder); the type/version banner reads the decoded type + root FORM + version with the thin `Colors.Secondary()` accent rule; the hex peek shows a hex+ASCII dump.
- Both repos build Release/x86 (cross-repo build gate, review item 10): Utinni first (IffChunk.OffsetBytes), then TheJawaToolboxDotNet against the pinned Utinni output.
- inspect-iff lane green (JSON goldens byte-identical; OffsetBytes is UI-only).

## Task Commits (cross-repo)
**Utinni:**
1. **IffChunk.OffsetBytes** (Task 1) — `fdafafd` (feat) — base ctor + IffContainerChunk/IffLeafChunk pass-through + IffReader threads the existing `chunkStart`; unit test asserts root==0 and child header positions; goldens byte-identical.

**UtinniPlugins:**
2. **Detail pane** (Tasks 2+3) — `38797d1` (feat) — `TreDetailPane` (metadata + banner + chunk tree + hex peek + four states) and `FormTreBrowser.AfterSelect` (off-thread TryResolve + dispatch) + Panel2 wiring + csproj `Compile Include`.

## Deviations from Plan

Two deviations, both Rule-1 (the real tree/format behavior exposed them); neither is scope creep:

**1. Path reconstruction uses the trie `PathNode.FullPath`, not a `TreeNode.Parent` text-walk.** The plan referenced FormObjectBrowser's parent-walk (prepend `Text + "/"`). That is wrong for this tree: 07-02's leaf display text carries a type-tag suffix (`segment + " " + TypeTag(...)`), so walking `Parent.Text` would build a corrupted key. The lazy tree already carries the canonical `PathNode` (with `FullPath`) on each node's `Tag`; `AfterSelect` reads `pn.FullPath` directly (`FormTreBrowser.cs:407`). Cleaner and correct.

**2. The planned v5000 "layout not yet verified" encrypted-banner string was omitted.** The plan (written before the 07-02 discovery) had `ShowEncrypted` pick a v5000-specific "Recognized TRE version 5000 — layout not yet verified; enumerate-only" banner. But **v5000 is now the readable SWGEmu Pre-CU format** (07-02 / `[[project-tre-version-support-gap]]`), is NOT enumerate-only, and therefore never reaches `ShowEncrypted`. `ShowEncrypted` keeps the `meta.Version` branch (review LOW: banner version accuracy) but uses truthful copy: `V6000` → the "Encrypted payload (v6000) — enumerate-only … Extract with TreeFileExtractor.exe" panel; the `else` branch is a generic truthful "Enumerate-only payload" note for any future enumerate-only lineage (`TreDetailPane.cs:160-180`). The plan's v5000 banner would have been a lie.

**Total deviations:** 2. **Impact:** #1 is load-bearing for correct payload resolution; #2 keeps the encrypted-state copy honest given the reclassified 5000 format. Both are downstream consequences of the lazy-trie design (07-02) and the 5000 reclassification (07-01/07-02), not new scope.

## Issues Encountered
None. (Pre-existing cosmetic: the bottom-left status/legend labels in Panel1 remain cramped — state mirrored in the title + log. 07-04b can tidy the left-panel labels; the detail pane reworked only Panel2.)

## User Setup Required
None.

## Next Phase Readiness
- **07-04a** (autonomous, Utinni-only, dotnet-testable): `Formats/Decoders/` — DataTable/StringTable/ObjectTemplate decoders (bounded posture) + `DecoderException` + the `decode-iff` CLI verb + golden tests. Pure consumers of the `Formats/Iff` output this plan already renders.
- **07-04b** (cross-repo + final smoke): deep decoders (AppearanceSummary, IffStructureSummary) rendered into `TreDetailPane.pnlStructured` — the placeholder host this plan stubbed (section 4, hidden).
- `IffChunk.OffsetBytes` and the reusable `LoadIff(IffDocument)` surface are now available to Phase 8's IFF editor (D-13). No blockers.

---
*Phase: 07-tjt-subpanel-tre-browser-read-only*
*Completed: 2026-05-27*
