---
phase: 7
round: 2
note: Second-pass review of the 6 revised plans (07-00..07-04b). Round-1 review (codex+cursor) is preserved in git history at commit ed61442; its feedback was incorporated via commit eb4ff8d.
reviewers: [codex, cursor]
skipped: [claude]
skipped_reason: claude is the executing CLI (self-review excluded for independence)
unavailable: [gemini, opencode, qwen]
reviewed_at: 2026-05-26T22:19:10Z
plans_reviewed: [07-00-PLAN.md, 07-01-PLAN.md, 07-02-PLAN.md, 07-03-PLAN.md, 07-04a-PLAN.md, 07-04b-PLAN.md]
---

# Cross-AI Plan Review — Phase 7 (Round 2)

> Second-pass review of the revised TRE Browser plan set. Both reviewers were given the full project/roadmap/requirements context, 07-CONTEXT, 07-RESEARCH, 07-UI-SPEC, and all six PLAN files, and were asked to focus on whether the round-1 revisions are sound and what gaps remain.

## Codex Review

## Summary
The revised Phase 7 plan set is materially stronger than the first-pass shape. The added `07-00` fixture gate and the `07-04a` / `07-04b` split are sound revisions: they reduce skip-only coverage, make parser work testable before UI work, and isolate the riskiest decoder families. Overall, the plans now have clear wave ordering, good attention to malformed TRE inputs, explicit CLI/browser code-path sharing, and reasonable manual gates for live SWG verification. Remaining risk is mostly in scope size and a few places where the plans specify behavior that may be harder or less well-defined than the acceptance criteria imply, especially `5000`, `COT2000` path resolution, object-template inheritance, UI-page detection, and WinForms performance at 100k+ paths.

## Strengths
- `07-00` is a good second-pass fix. It turns prior “fixture assumptions” into deterministic artifacts and makes v6000/COT2000/malformed coverage available in CI instead of relying on `SWG_SAMPLE_TRE_DIR`.

- `07-01` correctly preserves the existing 0005/0006 size-first path while adding crc-first v6000/COT2000 dispatch. That is the right conservative choice given the unresolved real-SWGEmu fixture question.

- The explicit lazy contract in `07-01` is strong: `PayloadReadCount`, `Open(Stream)` documented failure for payload reads, and `parse-tre` zero-payload-read assertions directly target the main performance risk.

- The `TreArchiveIndex` / `TrePayloadResolver` facade is a good architectural anchor. It gives the CLI and browser a shared browse/payload path rather than relying on review discipline.

- `07-02` properly resolves the SubPanel/Form contradiction by following the approved UI spec: resizable `UtinniForm`, `GetForms()` registration, `GetSubPanels()` unchanged.

- `07-03` fixes a real gap by adding `IffChunk.OffsetBytes` before promising `@offset` in the UI. That is a good example of plan claims being backed by framework data.

- Splitting `07-04` into `07-04a` and `07-04b` is justified. The data/STF/template decoders and graphics/UI summaries have different references, risks, and validation needs.

- The plans consistently preserve read-only scope and avoid adding extraction/write/export paths, which keeps DEC-A3 intact.

## Concerns
- **HIGH: `07-01 Task 2` may underspecify how `TrePayloadResolver` resolves COT2000 entries back to physical `.tre` files.**  
  `CotEntry` has `TreeFileIndex`, `Offset`, `Length`, `CompressedLength`, but the descriptor only lists `SourceArchive`. For COT2000, `SourceArchive` must be resolved relative to the `.toc` / sample directory and must carry enough information to open the right archive. If this mapping is wrong, the browser can enumerate paths but fail all payload resolution.

- **HIGH: `07-04a Task 2` object-template “inherited-field walk” is likely underplanned.**  
  Object template inheritance may require resolving base-template references across other IFFs/TRE entries, not just decoding one selected IFF document. The plan does not define whether inheritance is recursive, local-only, best-effort, or descriptor-resolver-backed. This could silently become either incomplete or much larger than planned.

- **HIGH: `07-04b Task 1` UI-page recognition is ambiguous.**  
  The plan says “use the SWG UI-page root FORM tag; if uncertain, recognize by extension hint passed in OR fall back to summarizing any FORM root.” But the decoder signature is `Summarize(IffDocument doc)`, with no extension hint. This creates a mismatch and risks criterion #3 being satisfied by a too-broad “any FORM root” summary rather than real UI-page detection.

- **MEDIUM: `07-00 Task 1` fixture naming inconsistency may confuse later tests.**  
  The file list says `zlib-framed-1record-v0006.tre`, but behavior/action says “single-record v6000 fixture” and `WriteZlibFramedV6000OneRecord(path)`. Either it is v0006 with zlib framing or v6000; the plan should make this consistent before implementation.

- **MEDIUM: `07-00 Task 2` bad-Adler validation may be overpromised with .NET Framework `DeflateStream`.**  
  The plan requires detecting invalid Adler trailers, but if the implementation strips the zlib header/trailer and feeds only raw deflate to `DeflateStream`, the Adler value is not validated by the BCL. The plan needs to explicitly require computing Adler32 over inflated bytes and comparing the trailer if “bad Adler” is a distinct error.

- **MEDIUM: `07-01 Task 1` treats all v6000 as enumerate-only while also testing payload block inflate.**  
  That is not necessarily wrong, but the distinction should be explicit: v6000 TOC/name blocks are zlib-readable; v6000 file payloads are enumerate-only/obfuscated. Otherwise implementers may accidentally block all v6000 compressed-block reading, including TOC/name.

- **MEDIUM: `07-02 Task 1/2` lazy tree plus filter behavior has a hidden complexity gap.**  
  Filtering over `_allPaths` and “prune the tree to ancestor chains” can still imply building a large filtered `TreeNode` hierarchy on the UI thread. The plan says scan flat index once, but it should also cap or batch UI node creation for broad filters like `a` or `.iff`.

- **MEDIUM: `07-02 Task 2` `Control.Invoke` from background enumeration can still stall with many small invokes.**  
  The plan says marshal every node mutation via `Control.Invoke`. For large trees this can be slow. It should batch node additions per top-level branch or per UI update tick.

- **MEDIUM: `07-03 Task 3` “non-FORM-tag obfuscation guard” may misclassify valid non-IFF assets.**  
  The browser is supposed to surface datatables, shaders, UI pages, string tables, templates, etc., but not every selected payload may begin with `FORM`. If non-IFF assets exist or `.tab`/`.stf` wrappers differ, routing them to encrypted state could produce misleading UX. Consider “unsupported/readable raw” distinct from encrypted.

- **MEDIUM: `07-04a` decoder fixture acquisition is still partly open-ended.**  
  “Probe `swg-main`, else synthesize tiny fixtures” is acceptable, but for object templates and STF, tiny hand-authored fixtures may not represent real loader layouts. This is okay for first coverage, but should be labeled smoke coverage rather than format confidence.

- **LOW: `07-01` acceptance relies on grep gates that may be brittle.**  
  Grep for specific field names/order, `% 31`, or absence of `GetRecordData` can catch obvious regressions, but these should not substitute for behavior tests. The behavior tests are present, so keep grep gates secondary.

- **LOW: `07-02` says “autonomous: false” because of human smoke, but tasks 1-2 are auto.**  
  This is fine, but make sure the executor does not block before finishing the auto-verifiable build/grep work.

- **LOW: `07-03 Task 2` “Color.Red only” conflicts slightly with existing theme abstraction.**  
  UI spec allows `Color.Red`, so this is acceptable, but if the theme has a destructive accessor later, prefer that. Not a blocker.

## Suggestions
- In `07-01`, explicitly define `TreEntryDescriptor` fields needed for COT2000 payload resolution: master index path, base directory, tree file name/index, physical archive path, and archive-local offset semantics.

- In `07-00`, rename or rewrite the zlib fixture consistently: either `zlib-framed-1record-v6000.tre` / `WriteZlibFramedV6000OneRecord`, or keep `v0006` and explain why a 0006 archive can contain zlib-framed blocks.

- In `07-00` / `07-01`, if `InvalidZlibTrailer` means bad Adler, require explicit Adler32 verification after inflate. If not, rename the malformed fixture/error to “invalid zlib frame/truncated trailer.”

- In `07-04a`, define object-template inheritance semantics before implementation: local fields only, one-level base reference display, or recursive resolution through `TreArchiveIndex`/`TrePayloadResolver`. For Phase 7, a best-effort read-only “declared base + local fields” may be safer than promising full inherited walk.

- In `07-04b`, change `IffStructureSummary.Summarize(IffDocument doc)` to accept metadata such as virtual path/extension, or define exact UI-page FORM tags in the plan. Avoid “summarize any FORM root” as proof of UI-page coverage.

- In `07-02`, add batching requirements for UI updates: build node batches off-thread as plain models, then `BeginInvoke` one branch or page at a time. Add a broad-filter stress test or manual check for `.iff`.

- In `07-03`, add an “unsupported readable payload” state separate from encrypted/enumerate-only. Use encrypted only when `TreVersion.IsEnumerateOnly` or known obfuscation applies.

- In `07-04a/b`, label synthesized decoder fixtures as minimal contract fixtures and, where possible, add one env-gated real loose-IFF fixture test per decoder family.

## Risk Assessment
Overall risk: **MEDIUM-HIGH**.

The parser foundation is now much better controlled, and the fixture gate materially lowers the chance of skip-heavy false green. The remaining risk comes from breadth: this phase includes archive dispatch, master-index enumeration, lazy WinForms browsing, live repository overlay, universal IFF detail rendering, and multiple per-type decoders. The riskiest areas are not basic TRE malformed handling anymore; they are semantic correctness of COT2000 payload resolution, object-template inheritance, UI-page/shader coverage, and WinForms responsiveness at real archive scale. The plan is implementable, but it should tighten those few contracts before execution to avoid discovering scope ambiguity mid-wave.

---

## Cursor Review

# Phase 7 Plans — Second-Pass Cross-AI Review

## 1. Summary

The first-pass revisions landed well: **07-00** as a Wave-0 gate, **07-04a/07-04b** split, **5000 enumerate-only** with a deliberately wrong layout fixture, **PayloadReadCount**, **TreArchiveIndex/TrePayloadResolver**, flat-index debounced search, **FilenameCount+GetFilenameAt** overlay semantics, **IffChunk.OffsetBytes**, and **shader/UI-page structured summaries** all directly address the prior review. Wave ordering (`07-00 → 07-01 → 07-02/03 → 07-04a → 07-04b`) is coherent, threat registers are tied to fixtures, and Tier-4 manual smokes are scoped correctly.

The remaining gaps are mostly **integration seams the plans assume but do not specify**: where the browser gets the client `.tre` directory, whether **SearchTOC** master indexes are actually read, whether the **COT2000 synthetic fixture set is self-contained**, and whether **filter-driven tree rebuilds** stay responsive at 213k paths. Those are execution risks, not plan-structure failures. Overall the set is **approvable with targeted amendments** before Wave 1 execution.

---

## 2. Strengths

- **07-00 closes the CI blind spot cleanly.** Synthetic v6000/COT2000/5000/0004 + malformed fixtures, `TreFixtureBuilder` regenerate-and-compare, and `FixturePath.SampleTreDir()` directly fix the “everything env-gated” problem without re-litigating it.
- **07-01 Task 1 is the right conservative split on Open Q1.** Keeping **size-first for 0004/0005/0006** to preserve Phase-4 goldens while using **crc-first only for v6000** is the correct tradeoff until a real SWGEmu archive exists.
- **5000 handling is now provable, not speculative.** Empty records, no v6000 stride routing, and a non-6000 layout fixture (`synthetic-5000-header.tre`) match D-06b and review consensus #1.
- **Shared facade is better than grep-for-classname.** `TreArchiveIndex` + `TrePayloadResolver` with dedicated tests (`TreArchiveIndexTests`) is a real mechanical lock-step mechanism for criterion #4’s browse/resolve paths.
- **07-04 split reduces blast radius.** Framework decoders + `decode-iff` (04a) before graphics/UI summaries + detail-pane wiring (04b) is the right sequencing for a phase this large.
- **07-03 OffsetBytes fix is minimal and correct.** Threading existing `chunkStart` through `IffReader` avoids a second parse pass and unblocks honest `@offset` rendering and Phase 8 reuse (D-13).
- **Preservation guard-rails are explicit.** CON-M-01/02 (no SPI widening), CON-N-02 (read-only `Repository`), D-01/D-07 (read-only + enumerate-only degradation) appear in every plan’s must-haves.
- **07-VALIDATION.md tracks the revised plan IDs** and documents the Wave-0 gate, decoder lanes, and three blocking-human smokes aligned with TEST-04.

---

## 3. Concerns

### HIGH

- **SearchTOC master index is advertised but not tasked (07-01).** Objective and `CotMasterIndex` artifact text say “COT2000 / **SearchTOC**,” and `07-RESEARCH.md` documents both kinds. **07-01 Task 2** only detects `first 8 bytes == " COT2000"`. SWGEmu installs that use SearchTOC-style master indexes would fail “full mount set” browsing even with a correct per-`.tre` fallback, unless `TreArchiveIndex.Build` never sees a master index and only scans loose `.tre` files—which is not specified.
- **Client `.tre` directory source is unspecified (07-02 Task 2).** The plan says “reuse the existing TJT/ini config accessor the same way other panels resolve client paths,” but **`FormObjectBrowser` does not read disk TREs at all**—it only consumes `Game.Repository`. There is no existing ini key or helper for `TreArchiveIndex.Build(clientDirOrIndexPath)`. Without an explicit decision (`utility.GetWorkingDirectory()`, SWG.exe directory, new `[TreBrowser] clientDir` ini setting, file picker, etc.), criterion 1+2 live smoke is blocked at implementation time.
- **COT2000 synthetic fixture may be incomplete for end-to-end facade tests (07-00 / 07-01 Task 2).** `synthetic-cot2000-2tree.toc` lists **two `.tre` names**, but **07-00 only commits the `.toc`**, not companion archives. `TreArchiveIndexTests` and `TrePayloadResolver` “readable entry” cases need resolvable per-archive payloads; otherwise tests either skip the resolver path or pass only at the `CotMasterIndex` enumeration layer.
- **UI-page root FORM tag is still fuzzy at the criterion-3 boundary (07-04b Task 1).** The plan admits uncertainty (“if exact UI-page root tag is uncertain… fall back to summarizing any FORM root”). That weakens the headless proof that **PROD-01 criterion #3** (“UI page” as a distinct asset class) is met, versus “any IFF gets a generic child-count summary.”

### MEDIUM

- **Filter + lazy tree may still jank at scale (07-02 Task 1).** The flat `_allPaths` scan fixes per-tick tree re-walk, but **rebuilding/pruning/expanding the TreeView on every 250ms tick** for large match sets can still allocate heavily. No match cap, incremental update strategy, or “show flat results list when filter non-empty” fallback is specified.
- **`TrePayloadResolver` enumerate-only contract is ambiguous (07-01 Task 2).** Action text says “returns decompressed bytes… **and reports/raises** enumerate-only.” **07-03 Task 3** branches on `d.EnumerateOnly` before calling `Resolve`, but the resolver API itself should specify one behavior (dedicated exception type vs. `TryResolve` out-param) so UI and tests don’t diverge.
- **`Game.Repository` threading guidance conflicts with precedent (07-02 Task 2).** Plan requires `Control.Invoke` / `GameCallbacks` for overlay reads; **`FormObjectBrowser.LoadRepo()` reads `Game.Repository` directly from a background `Task`** without marshaling. If the native harvest vector isn’t thread-safe for concurrent reads, overlay builds could flake in live SWG—worth an explicit thread-safety assertion or matching the established pattern deliberately.
- **Cross-repo CI gap (07-02, 07-03, 07-04b).** Framework changes ship in Utinni; UI in UtinniPlugins. Automated verification for TJT is **local `msbuild` only**—Utinni’s GitHub Actions lane won’t catch TJT compile breaks when `TreArchiveIndex`, new decoders, or `IffChunk.OffsetBytes` land. Paired commits are pre-authorized, but there’s no CI guard on the sibling repo.
- **0004 dispatch lacks an explicit golden (07-00 + 07-01 Task 1).** `synthetic-0004-header.tre` is emitted, but Task 1 behavior tests list 0005/0006 goldens, v6000, 5000, zlib, malformed—not a dedicated **0004 enumeration** case. Risk of “recognized tag, untested path.”
- **`PayloadReadCount` visibility left open (07-01 Task 2).** “Public or internal + InternalsVisibleTo” should be pinned; if public, it becomes shipping surface on `TreFile` beyond test intent.
- **Criterion #4 wording vs. verb coverage.** The ROADMAP says `parse-tre` / `list-objects` goldens cover browse paths. The revised design honestly adds **`TreArchiveIndexTests` + `decode-iff` + `inspect-iff`** as the coverage mechanism. That’s sound, but **success criterion #4 in ROADMAP/PROD-W1-TRE acceptance text is now slightly stale** relative to the plans—executors may over-fit `parse-tre` alone.

### LOW

- **`zlib-framed-1record-v0006.tre` naming (07-00 Task 1).** Filename suggests **0006/size-first lineage**, but content is **v6000 crc-first zlib**. Low risk if builder tests document the intent, but easy to mis-wire in 07-01 dispatch tests.
- **`TreFile` XML contract still documents eager-read (current code).** 07-01 should explicitly task updating the class docstring that still states “REVIEWS HIGH-4 eager-read path A”—otherwise future phases inherit wrong contract docs.
- **Decoder IFF fixtures still Wave-0 `[ ]` items (07-VALIDATION.md lines 105–106).** Acceptable deferral to 07-04a/b Task 1, but **04a TDD starts blocked** if neither `swg-main/serverdata` nor hand-authored synth IFFs exist on the executor machine.
- **Large structured ListViews without row caps (07-04b Task 2, T-07-17).** Threat register mentions UI freeze; no hard cap (e.g., first N rows + “truncated” label) for huge datatables.
- **Phase title vs. UI host.** Roadmap still says “subpanel”; approved **07-UI-SPEC** chose **`UtinniForm` via `GetForms()`**. Plans 07-02+ align with UI-SPEC—cosmetic traceability only.

---

## 4. Suggestions

1. **07-01 Task 2 — Add SearchTOC detection or document explicit deferral.** Either port `tre_reader.py`’s `detect_master_index_kind` fully (COT2000 **and** SearchTOC), or add a must-have: “SearchTOC unsupported in V1 → `TreArchiveIndex.Build` scans directory for `*.tre` only” with a skipped test gated on a future fixture. Do not leave SearchTOC in objective text without a task.

2. **07-02 Task 2 — Pin the TRE root path source before coding.** Add a concrete decision block, e.g.:
   - Primary: `utility.GetWorkingDirectory()` (or SWG process directory) + detect `*.toc` / `*.tre`;
   - Fallback: new ini key `[TreBrowser] clientDir` with first-run error state if missing;
   - Document in `07-02` acceptance: grep for the chosen API + manual smoke step “confirm tree populates without manual path entry.”

3. **07-00 Task 1 — Emit companion archives for COT2000.** Extend `WriteCot2000TwoTree` to also write two tiny referenced `.tre` files (or a fixture subdirectory) so **07-01 `TreArchiveIndexTests` / `TrePayloadResolver.Resolve`** can run fully in CI without env vars.

4. **07-04b Task 1 — Lock UI-page root FORM tag(s) during fixture authoring.** When synthesizing the UI-page golden, record the exact root tag in a comment + test name (e.g., `UIPG` / whatever the fixture uses). Remove “summarize any FORM root” as the primary path—keep that only for the unrecognized-type no-throw test.

5. **07-02 Task 1 — Add filter performance guard.** When filter text is non-empty and matches exceed e.g. 5k paths, show a status label (“N matches—refine filter”) and avoid full TreeView rebuild; or switch to a flat `ListView` for filtered mode.

6. **07-01 Task 2 — Specify `TrePayloadResolver` API:**
   ```csharp
   // Pick one:
   bool TryResolve(TreEntryDescriptor d, out byte[] payload);
   // or
   byte[] Resolve(...) // throws TreEnumerateOnlyException
   ```
   Align **07-03 AfterSelect** with the same contract.

7. **07-01 Task 1 — Add `ParseTre_0004Header_EnumeratesRecords` golden** using `synthetic-0004-header.tre` (even if record layout is “unverified,” assert non-throw + expected record count/names from builder).

8. **Cross-repo CI (optional but high leverage).** Add a minimal Utinni workflow step or documented gate: build `TheJawaToolboxDotNet.csproj` against the pinned Utinni release output after framework waves—or note in **07-02/07-03 verification** that executor must run both builds before marking wave complete.

9. **Update ROADMAP success criterion #4 text** to mention `TreArchiveIndex`/`decode-iff`/`inspect-iff` golden lanes, not only `parse-tre`/`list-objects`, so verification matches the revised architecture.

---

## 5. Risk Assessment

**Overall risk: MEDIUM**

**Justification:** The hardest parser work (version dispatch, zlib, lazy TOC, bounds checks, 5000 posture, fixture gate) is now well specified and test-backed—that was the first review’s core worry, and the revisions are sound. What remains is **integration and completeness risk**: disk enumeration may not find archives without a defined client path; SearchTOC may be silently dropped; COT2000 facade tests may not exercise payload resolution in CI; and filter/tree UX may still struggle at 213k paths despite debouncing. None of these are plan-structure failures, but any one could fail PROD-W1-TRE criteria 1–2 or weaken PROD-01 criterion #3 in live SWG even with green `dotnet test` lanes.

**Success criteria mapping (honest read):**

| Criterion | Likely met by revised plans? | Notes |
|-----------|------------------------------|-------|
| **1** — Subpanel loads in TJT against live client | **Conditional** | 07-02 registration + manual smoke OK; blocked on TRE path resolution |
| **2** — Navigate full mount set, expand, metadata | **Conditional** | Lazy tree + overlay OK if enumeration root is correct |
| **3** — Every asset class with structured view | **Mostly** | 07-04b + universal IFF tree; UI-page tag still soft |
| **4** — CLI goldens cover same code paths | **Mostly** | Strong via shared `Formats/` + `TreArchiveIndexTests` / `decode-iff` / `inspect-iff`; ROADMAP wording lags the design |

**Revisions sound?** **Yes** for the items they targeted (Wave-0 gate, 5000, lazy contract, facade, 07-04 split, overlay semantics, OffsetBytes, shader/UI summary). **Second-pass focus:** fix the **TRE root path**, **SearchTOC/companion-fixture completeness**, and **filter scalability** before executing **07-02**—those are the highest-leverage remaining edits.

---

## Consensus Summary

Two independent reviewers (Codex, Cursor) agree the round-1 revisions landed well: the parser foundation, fixture gate, and architecture are sound, and the residual risk is **semantic correctness + integration seams, not plan structure**. Both consider the set approvable with targeted amendments before Wave-1 execution.

### Agreed Strengths (2+ reviewers)
- **07-00 Wave-0 fixture gate closes the CI blind spot** — turns prior env-gated fixture *assumptions* into deterministic in-repo artifacts (v6000/COT2000/5000/0004 + malformed), available in CI without `SWG_SAMPLE_TRE_DIR`.
- **07-01 conservative dispatch is the right call** — size-first for 0004/0005/0006 (preserves Phase-4 goldens), crc-first only for v6000, until a real SWGEmu v6000 archive exists.
- **TreArchiveIndex / TrePayloadResolver shared facade is the correct architectural anchor** — gives CLI and browser a mechanically shared browse/resolve path instead of relying on review discipline.
- **07-04 -> 07-04a/04b split is justified** — decoders vs graphics/UI summaries have different references, risks, and validation needs.
- **07-03 IffChunk.OffsetBytes fix is minimal and correct** — backs the @offset UI claim with real framework data and unblocks Phase 8 reuse (D-13).
- **Read-only scope preserved across all plans** — DEC-A3, D-01, CON-M-01/02, CON-N-02 appear in every plan's must-haves.

### Agreed Concerns (2+ reviewers — highest priority)
1. **HIGH — UI-page root FORM-tag recognition is ambiguous (07-04b Task 1).** Both: the "fall back to summarizing any FORM root" escape hatch weakens the headless proof that PROD-01 **criterion #3** (UI page as a *distinct* asset class) is met, and the `Summarize(IffDocument)` signature carries no extension/path hint. → Lock the exact UI-page root tag(s) during fixture authoring; pass virtual-path/extension metadata into the summarizer; reserve "any FORM root" for the unrecognized-type no-throw test only.
2. **HIGH — COT2000 payload resolution is underspecified (07-01 Task 2 / 07-00 Task 1).** Codex: the descriptor only lists `SourceArchive`, but resolving a COT2000 entry to a physical `.tre` needs master-index path, base dir, tree-file index, and archive-local offset semantics. Cursor: 07-00 commits only the `.toc`, not the two companion `.tre` archives it references, so `TreArchiveIndexTests` / `TrePayloadResolver.Resolve` "readable entry" cases can't run in CI. → Define the descriptor fields for COT2000 resolution **and** emit the companion archives in the fixture.
3. **HIGH/MEDIUM — Resolver contract + object-template inheritance semantics are undefined (07-01 Task 2, 07-04a Task 2).** Cursor: `TrePayloadResolver` "returns bytes **and** reports/raises enumerate-only" is two behaviors — pin one (`TryResolve` out-param **or** dedicated exception) and align 07-03 `AfterSelect`. Codex: the object-template inherited-field walk doesn't state whether inheritance is recursive / local-only / best-effort, risking silent scope blow-up. → Specify the resolver API once; pick a bounded inheritance posture for Phase 7 (e.g. declared base + local fields).
4. **MEDIUM — Filter + lazy TreeView responsiveness at ~213k paths (07-02 Task 1/2).** Both: the flat `_allPaths` scan fixes the per-tick re-walk, but rebuilding/pruning the TreeView on every 250ms tick for broad filters ("a", ".iff") still allocates heavily, and per-node `Control.Invoke` can stall. → Add a match cap / flat-`ListView` fallback for non-empty filters; batch node mutations per branch.
5. **MEDIUM/LOW — zlib fixture naming inconsistency (07-00 Task 1).** Both: `zlib-framed-1record-v0006.tre` implies a 0006/size-first lineage, but the content is v6000 crc-first. Easy to mis-wire in 07-01 dispatch tests. → Rename to `v6000` (and the `WriteZlibFramed…` builder) or document why a 0006 archive carries zlib-framed blocks.

### Divergent Views (worth investigating)
- **Overall risk level differs:** Codex = **MEDIUM-HIGH** (emphasizes breadth — archive dispatch + master-index + lazy WinForms + live overlay + universal IFF detail + multiple decoders in one phase). Cursor = **MEDIUM** (parser core is now well test-backed; remainder is integration/completeness risk). Both agree the parser foundation is sound.
- **Unique to Cursor:**
  - **HIGH — Client `.tre` directory source is unspecified (07-02 Task 2).** `FormObjectBrowser` only reads `Game.Repository`, never disk TREs; there is no existing ini key/helper for `TreArchiveIndex.Build(clientDir)`. Without a concrete decision (working dir / SWG.exe dir / new `[TreBrowser] clientDir` ini / file picker), criterion 1+2 live smoke is blocked at implementation time.
  - **HIGH — SearchTOC master index advertised but not tasked (07-01).** Objective and `CotMasterIndex` text mention COT2000 **and SearchTOC**, but Task 2 only detects `COT2000`. SWGEmu installs using SearchTOC-style master indexes would fail full-mount-set browse. → Either port the full master-index detection or explicitly defer SearchTOC with a skipped, fixture-gated test.
  - **MEDIUM — Cross-repo CI gap (07-02/03/04b).** Framework changes ship in Utinni; UI in UtinniPlugins. Utinni's Actions lane won't catch TJT compile breaks when `TreArchiveIndex` / new decoders / `IffChunk.OffsetBytes` land. → Add a TJT build step or a documented "build both before marking wave complete" gate.
  - **MEDIUM — No dedicated 0004 enumeration golden** despite emitting `synthetic-0004-header.tre` ("recognized tag, untested path").
  - **MEDIUM — Game.Repository threading conflict:** `LoadRepo()` reads `Game.Repository` directly from a background `Task` while the plan elsewhere requires `Control.Invoke`/`GameCallbacks` for overlay reads — pin the thread-safety story.
- **Unique to Codex:**
  - **MEDIUM — bad-Adler validation may be overpromised (07-00 Task 2).** If the impl strips the zlib header/trailer and feeds raw deflate to `DeflateStream`, the BCL never validates the Adler32 trailer. → Require explicit Adler32 verification after inflate, or rename the malformed fixture/error to "invalid zlib frame / truncated trailer".
  - **MEDIUM — non-FORM-tag obfuscation guard may misclassify (07-03 Task 3).** Not every selected payload begins with `FORM`; routing valid non-IFF assets to the "encrypted" state is misleading. → Add an "unsupported/readable raw" state distinct from encrypted/enumerate-only.

### Recommended highest-leverage edits before executing 07-02
Pin the **TRE root path source**, resolve **SearchTOC + COT2000 companion-fixture completeness**, lock the **UI-page FORM tag**, and define the **resolver/inheritance contracts** — these gate the live-smoke success criteria and PROD-01 criterion #3 even with green `dotnet test` lanes.
