---
phase: 7
reviewers: [codex, cursor]
reviewed_at: 2026-05-26T21:42:57Z
plans_reviewed: [07-01-PLAN.md, 07-02-PLAN.md, 07-03-PLAN.md, 07-04-PLAN.md]
self_skipped: claude (running inside Claude Code — excluded for review independence)
unavailable: [gemini, coderabbit, opencode, qwen]
---

# Cross-AI Plan Review — Phase 7

Two independent external AI systems reviewed the four Phase-7 plans against the roadmap goal, locked decisions (D-01..D-13), research, and the approved UI-SPEC. `claude` was skipped (this session runs inside Claude Code). The per-reviewer sections are verbatim; the Consensus Summary is synthesized.

---

## Codex Review
*(codex-cli 0.133.0, GPT via ChatGPT)*

## Summary

The four-plan split is directionally strong: it isolates the riskiest parser work first, keeps CLI/browser parity explicit, and puts manual live-SWG verification at the right UI boundaries. However, the plans are still too optimistic for autonomous execution. The biggest risks are binary-parser correctness gaps, under-specified real mount discovery, weak enforcement of shared code paths, and an overlarge Plan 04 that tries to deliver several independent file-format decoders plus UI rendering in one wave. I would not treat these as execution-ready until the parser API contracts, fixture strategy, 5000 behavior, and cross-repo dependency mechanics are tightened.

## Strengths

- The phase is split in the right broad order: shared TRE parser first, browser shell second, detail pane third, per-type decoders last.
- The plans correctly identify the highest-risk parser issues: version-specific TOC layouts, zlib-vs-raw-deflate, lazy enumeration, v6000 enumerate-only behavior, and malicious count/offset handling.
- CLI parity is repeatedly called out, with `parse-tre`, `inspect-iff`, and `decode-iff` intended to cover browser-used code paths.
- The UI plan respects the locked product decision to host inside TJT via `GetForms()` without widening `IEditorPlugin`.
- Manual checkpoints are appropriately limited to live-host behavior that CI cannot currently verify.
- The read-only fence is explicit across all plans: no export, authoring, write, or Maya parity work.

## Concerns

- **HIGH: `Open(Stream)` vs lazy payload access is under-specified.**  
  Plan 07-01/07-02 says `TreFile.Open(Stream)` exists, but lazy payload reads reopen by stored path only when constructed via `Open(string)`. The plan does not define what `GetRecordData()` does for stream-backed instances. Throwing later would surprise existing callers; keeping the whole stream would violate the lazy goal. This needs an explicit contract.

- **HIGH: 5000 handling is internally contradictory.**  
  The plan says “recognized without asserting a layout,” but also says route through the v6000 32-byte stride. That is asserting enough layout to mis-enumerate silently. For a no-fixture/no-spec version, safer behavior is: parse header minimally, mark unsupported/enumerate-only, expose no records unless a fixture-gated parser is enabled.

- **HIGH: COT2000/sample tests depend on local `D:/Sample-TRE-Files`, but CI coverage will skip the most important real-world path.**  
  Env-gated skip is useful locally, but it means the v6000/COT2000 path can rot in CI. The plan needs small synthetic COT2000/v6000 fixtures in-repo that exercise zlib, 32-byte crc-first records, cumulative filename lengths, and treeFileIndex bounds.

- **HIGH: integer overflow checks are named but not specified enough.**  
  Count × stride, cumulative name offsets, offset + compressedLength, declared output size, and row × col cell loops all need checked arithmetic. “ValidateLength” alone is not enough. Autonomous agents may miss `int` overflow before validation.

- **HIGH: Plan 04 scope is too large and format-risky.**  
  Datatable, STF, object-template inherited fields, mesh, skeletal mesh, skeleton, animation, CLI verb, golden tests, and WinForms structured views is too much for one plan. Object-template inheritance and graphics formats are especially likely to need multiple iterations.

- **HIGH: “same code path” is asserted but not mechanically enforced for browser payload resolution.**  
  UI plans can still build TJT-side lookup/index models and accidentally bypass `TreFile`/`CotMasterIndex` for important behavior. Grep gates for class names are weak. The plan should require a shared framework model/service API consumed by both CLI and UI.

- **MEDIUM: mount-source discovery is vague.**  
  Plan 07-02 says “configured SWG client dir” and “reuse existing config accessor,” but does not identify the actual ini key, SWG cfg file, TRE mount order source, or COT2000 filename. This is a major integration point and should not be left to executor discovery.

- **MEDIUM: `Game.Repository` threading is underspecified.**  
  The plans say read via `Game.Repository` and marshal UI updates, but do not state whether repository access itself must occur on the game thread, UI thread, or is thread-safe. If bindings touch native state, this matters.

- **MEDIUM: lazy `TreeView` plus search/filter conflicts.**  
  Search over 213k entries requires an in-memory path index even if UI nodes are lazy. The plan says top-level-only nodes and filter prunes ancestor chains, but does not specify the backing trie/index structure or performance target.

- **MEDIUM: chunk tree offset display may not be supported by current `IffChunk`.**  
  The plan wants `TAG · size · @offset`, but the provided interface lists no offset property. Either Plan 03 must add offsets to `IffReader`/`IffChunk` in the framework, or the UI cannot honestly render offsets without a second parser.

- **MEDIUM: v6000 encrypted detection may incorrectly label non-IFF assets.**  
  “Payload does not begin with FORM/known tag” is not sufficient because some valid assets may not be IFF or may have other magic. It should distinguish archive-level enumerate-only from per-file unsupported type.

- **MEDIUM: `DeflateStream` zlib handling is too hand-wavy around Adler32.**  
  “Feed the remainder ignoring trailing 4 bytes” can accidentally include the Adler32 unless the input stream is bounded correctly. The plan should specify slicing `[2..^4]` for zlib-framed blocks and validating minimum length.

- **MEDIUM: 0004 is accepted but no layout is specified.**  
  The plan maps `0004` to the size-first 24-byte path without fixture evidence. This may be acceptable as defensive compatibility, but tests should include a synthetic 0004 header and clearly mark real-layout unverified.

- **MEDIUM: compressed block semantics are unclear.**  
  Compression kind values are assumed but not enumerated in the plan. `0`, raw deflate, zlib, and unknown compressor behavior should be explicitly defined, including error kind on unknown compressor.

- **MEDIUM: Plan 03 claims same path as `inspect-iff`, but uses `IffReader.Read(Stream)` while CLI uses `Read(path)`.**  
  Same parser, yes, but not identical surrounding path. If path-based `IffReader` has different validation or metadata behavior, tests may miss browser-only behavior. Probably acceptable, but the claim should be softened or tested.

- **LOW: UI grep gates are brittle.**  
  Checking for `Task`, `Control.Invoke`, `BeforeExpand`, or absence of `Color.FromArgb` does not prove correct behavior. Add small unit-testable model classes for tree indexing/filtering if possible.

- **LOW: “bold matched substring in TreeView labels” is not feasible with standard WinForms `TreeView` per-substring styling.**  
  Standard `TreeNode.NodeFont` can bold the whole node, not a substring, unless owner-draw is implemented. The plan should say bold matching nodes, not substring, or require owner draw.

- **LOW: non-ASCII STF fixture conflicts with default ASCII editing guidance.**  
  It is reasonable for a binary fixture/test, but the plan should specify fixture bytes generated by test helper or binary file, not source literals that risk encoding churn.

## Suggestions

- Define the final `TreFile` contract before execution:
  - `Open(string)` supports lazy `GetRecordData`.
  - `Open(Stream)` either eagerly owns only metadata and `GetRecordData` throws a documented `InvalidOperationException`, or copies to a temp/owned stream with explicit lifecycle.
  - Add tests for both.

- Replace the 5000 behavior with safer fixture-gated semantics:
  - Recognize tag.
  - Set `Header.Version = V5000`, `EnumerateOnly = true`, `Records = empty` or throw a typed `UnsupportedVersion` only when payload enumeration is requested.
  - Do not parse v6000-style records until a real/synthetic spec-backed fixture exists.

- Add small in-repo synthetic fixtures for:
  - v6000 crc-first zlib TOC/name block.
  - COT2000 global TOC with two tree files.
  - malformed count × stride overflow.
  - offset + length overflow.
  - zlib block with invalid Adler/truncated trailer.
  - unknown compressor.

- Move shared browse indexing out of TJT UI into framework code, for example:
  - `TreArchiveIndex`
  - `TreMountIndex`
  - `TreEntryDescriptor`
  - `TrePayloadResolver`
  
  Then CLI and browser can both consume the same descriptor/payload APIs, and success criterion #4 becomes enforceable.

- Split Plan 04 into at least three plans:
  - 04a datatable + STF decoders + CLI tests.
  - 04b object-template decoder.
  - 04c appearance/mesh/skeleton/animation summary + UI structured rendering.
  
  This reduces blast radius and makes format mistakes easier to isolate.

- Explicitly document mount discovery:
  - exact SWG config file(s) read,
  - COT2000 master index filename/pattern,
  - TRE search directory,
  - archive precedence/order,
  - behavior when no configured client path exists.

- Add checked arithmetic requirements by name:
  - use `checked` for `count * stride`,
  - validate `offset >= 0`,
  - validate `length >= 0`,
  - validate `offset <= streamLength - length`,
  - validate cumulative name offsets cannot overflow,
  - validate `rows <= MaxRows`, `cols <= MaxCols`, and `rows * cols <= MaxCells`.

- Update Plan 03 if offsets are required:
  - either add `OffsetBytes` to `IffChunk` in `UtinniCoreDotNet/Formats/Iff`,
  - or remove `@offset` from the UI requirement.

- Replace substring-bold requirement with a feasible WinForms requirement:
  - bold entire matching leaf/ancestor node, or
  - explicitly require owner-drawn `TreeView`.

- Add cross-repo build verification after Plan 04:
  - `dotnet test Utinni.Cli.Tests`
  - `msbuild Utinni.sln /p:Configuration=Release /p:Platform=x86`
  - `msbuild TheJawaToolboxDotNet.csproj /p:Configuration=Release /p:Platform=x86`

## Risk Assessment

**Overall risk: HIGH.**

The plan handles the right problems, but this phase combines untrusted binary parsing, incomplete format knowledge, large data volumes, cross-repo integration, and WinForms UI work. Plan 07-01 is the critical path and still has contract ambiguity around lazy streams, overflow validation, and 5000. Plan 07-04 is the largest delivery risk because deep per-type decode across several unrelated SWG formats is unlikely to be completed robustly in one autonomous pass. With the suggested fixture additions, stricter parser contracts, and Plan 04 split, the risk would drop to **MEDIUM**.

---

## Cursor Review
*(cursor-agent, ask mode)*

# Cross-AI Plan Review — Phase 7 (TRE Browser)

## 1. Summary

The four-plan split (framework TRE refactor → TJT shell/overlay → detail pane → per-type decoders) is well aligned with research, locked decisions, and the existing codebase. Plan **07-01** is the strongest: it turns verified binary pitfalls (field-order divergence, zlib framing, eager read) into concrete TDD tasks with a solid threat model. Plans **07-02–07-04** inherit that core but lean on grep/build gates and three blocking live-SWG smokes; several **PROD-01** asset classes (UI page, shader) and roadmap **list-objects** parity are only partially specified. **Wave 0** in `07-VALIDATION.md` is still unchecked while plans assume fixtures/env gates exist—execution risk before wave 1 starts. Overall: **executable with fixes**, not “ship as-is.”

---

## 2. Strengths

- **Research → plan traceability:** Pitfalls 1–7 from `07-RESEARCH.md` map to explicit tasks (version dispatch, zlib `%31==0`, lazy TOC, `5000` enumerate-only, Pitfall 7 CLI lock-step). Matches current `TreFile.cs` (0005/0006-only gate, size-first loop, eager `compressedCache`, raw `DeflateStream`).
- **07-01 security posture:** STRIDE register (T-07-01..05), `ValidateLength`, 256 MB caps, `treeFileIndex < numTreeFiles`, forged-count tests—appropriate for untrusted `.tre`/`.toc` input.
- **Open Q1 handled conservatively:** Keeps size-first for V0004/V0005/V0006 so Phase 4 `parse-tre` goldens stay byte-identical; crc-first only for V6000/COT2000—reduces silent mis-decode risk.
- **D-08 enforcement on the framework side:** Parsers/decoders in `UtinniCoreDotNet/Formats/`; new `decode-iff` verb; grep gates against JSON/console in decoders and `OBJS` scans—good drift prevention for **framework** code.
- **UI architecture:** Resizable `UtinniForm` via `GetForms()` (not 417px `SubPanel`) matches `07-UI-SPEC.md`; lazy `BeforeExpand` + background `Task` + `Control.Invoke` address 213k-entry Pitfall 5; `TreDetailPane.LoadIff` sets up Phase 8 (D-13).
- **Cross-repo awareness:** `Plugin.cs` registration pattern is accurate (`forms.Add(new FormObjectBrowser(this))` at line 57); `autonomous: false` on UI plans with explicit human-verify steps matches VALIDATION Tier-4 residual.
- **Validation doc:** `07-VALIDATION.md` ties tasks to filters, documents env-gating on `SWG_SAMPLE_TRE_DIR`, and separates headless CLI coverage from TJT manual smoke.

---

## 3. Concerns

### HIGH

- **Wave 0 not done vs. plan dependencies:** `07-VALIDATION.md` Wave 0 items (Sample-TRE wiring, zlib fixture, synthesized `5000` header, lazy-enumeration assertion, decoder IFF fixtures, SWGEmu fixture) are still `[ ]`. **07-01 Task 1** expects v6000/COT2000 env-gated tests and a synthesized `5000` fixture—executors will hit red CI or skip-heavy green unless Wave 0 runs first. Plans do not gate wave 1 on Wave 0 completion.
- **PROD-01 “every asset class” vs. 07-04 scope:** Roadmap/PROD-01 requires IFF, datatable, template, **UI page**, **shader**, string-table. **07-04** ships decoders for DTII, STF, object template, and MESH/SKMG/SKTM/KFAT summaries only—no **UI page** decoder and no dedicated **shader** structured view (only `[SHDR]` tree tags + universal IFF tree). Manual smoke step 5 does not ask the human to verify UI page or shader structured views. Criterion 3 is at risk of “chunk tree = enough” without explicit acceptance.
- **`list-objects` not in lock-step (success criterion #4):** Phase 7 success criteria cite **`parse-tre` and `list-objects`**. None of the four plans migrate `ListObjectsCommand` off the provisional `OBJS` byte-scan (documented debt since Phase 4). Browse enumeration uses `CotMasterIndex`/`TreFile`, not `list-objects`—criterion #4 is only half addressed unless criterion wording is narrowed.
- **Repository overlay semantics vs. D-05:** Overlay is built from `Game.Repository` after `Game.IsRunning`. Native `Repository` captures a **one-time** harvest at construction (`filenames = treefile::getAllFilenames()` moves/clears the static set in `tree_file.cpp`). Entries mounted **after** that moment will show as “dimmed / not loaded” even if resolvable—contradicts “currently loaded/resolvable” unless documented as install-time snapshot only. Plan says “best-effort” but manual verify doesn’t test late-mounted trees.
- **Filter over ~213k paths unspecified:** **07-02** requires debounced substring filter pruning ancestor chains but does not require a **flat path index** (e.g. `List<string>` or trie) built at enumeration time. Naive re-walk of the tree or rescan of `CotMasterIndex.Entries` on every 250 ms tick can still freeze or spike CPU—Pitfall 5 mitigation incomplete for search, not just initial build.

### MEDIUM

- **`5000` as structural sibling of `6000`:** Reasonable given D-06b (recognize tag, enumerate-only, `TODO(5000-fixture)`), but routing **5000** through **32-byte crc-first** layout without a fixture can mis-enumerate if `5000` is not a 6000 sibling (research assumption A2). Mitigation (enumerate-only, no payload assert) limits blast radius; still a **silent wrong TOC** risk, not just “can’t decode content.”
- **Lazy `GetRecordData` + `Open(Stream)` contract:** **07-01** drops eager cache and re-opens by path for `Open(string)`. `Open(Stream)` is used by tests/CLI; plan does not say how lazy payload read works when **no path** is stored—risk of regressing stream-based tests or reintroducing eager read for that entry point.
- **“No payload read” test is weakly specified:** Task 2 acceptance allows “payload-read counter or timing/IO bound”—easy to satisfy without proving `parse-tre` never touches payloads. Prefer a hard seam (e.g. `IPayloadAccess` mock, or `TreFile.PayloadReads` test-only counter).
- **Overlay build via `GetDirectoryInfo` only:** Plan grep-checks `GetDirectoryInfo`/`GetFilenameAt` but building a full-path `HashSet` realistically needs **`FilenameCount` + loop** (top-level `GetDirectoryInfo` only indexes first path segment per `repository.cpp`). Worth making explicit so implementers don’t under-harvest overlay coverage.
- **Cross-repo CI gap:** `ci.yml` runs `dotnet test` on **Utinni** only; **07-02/03/04** verify TJT via local `msbuild` only. A broken TJT reference to new `Formats` APIs won’t fail master CI until release workflow or manual build—paired-commit discipline is assumed, not automated.
- **0004 in enum, same decoder as 0005/0006:** D-06 includes **0004**; no distinct header/stride test if 0004 differs from 0005—only `Parse("0004")` enum test. Low probability issue but untested divergence.
- **Integer overflow on `recordCount * stride`:** Bounds checks against stream length are specified; explicit **checked multiply** before allocation (e.g. `recordCount > streamLength / stride`) is not—relevant on 32-bit x86 host.
- **Concurrent file access:** Lazy `GetRecordData` re-opens files without mentioning shared read locking; usually fine on Windows, but multiple rapid selections could overlap reads—minor, but worth a single shared open or `FileShare.Read` note (already used in `Open(string)`).

### LOW

- **Phase title vs. delivery:** Phase is “TJT **subpanel**” but locked decision is **`UtinniForm` / `GetForms()`**—naming confusion for executors skimming roadmap only.
- **`cbTypeFacet` optional but in Designer:** UI-SPEC says V1-optional; included with “All types” only—harmless scope noise.
- **Encrypted banner hard-codes “v6000”:** **07-03** copy says `Encrypted payload (v6000)` even for **V5000** enumerate-only—minor UX inaccuracy.
- **`Color.Red` exception:** Theming rule is mostly `Colors.*`; parse-fail heading uses `Color.Red` per UI-SPEC—acceptable but inconsistent with “no literals” grep gates elsewhere.

---

## 4. Suggestions

- **Block 07-01 on Wave 0 completion** (or add a 07-00 plan): Check in minimal in-repo fixtures (zlib block, `5000` header, multi-record lazy test, tiny DTII/STF/template IFFs); document `SWG_SAMPLE_TRE_DIR` on the self-hosted runner in `ci.yml` or `release.yml` so v6000/COT2000 goldens aren’t permanently skipped on CI.
- **Clarify PROD-01 coverage in 07-04 must_haves:** Either add lightweight **shader / UI page** summaries (even “FORM tag + child count” via `IffReader` only), or amend success criteria / manual smoke to state: *structured decode for DTII/STF/template/mesh family; UI page/shader via universal IFF tree only*—so criterion 3 isn’t ambiguous.
- **Add `list-objects` migration task** (07-01 or small 07-05): Reimplement `ListObjectsCommand` using `CotMasterIndex`/`TreFile` + `IffReader`, or drop `list-objects` from Phase 7 success criterion #4 in ROADMAP.
- **07-02 Task 2 — specify overlay harvest:** `for (i = 0; i < Game.Repository.FilenameCount; i++) loaded.Add(Game.Repository.GetFilenameAt(i));` on game thread or marshaled; document install-time snapshot limitation in legend copy.
- **07-02 — require flat path list for filter:** e.g. `IReadOnlyList<string> _allPaths` from `CotMasterIndex.Entries` (paths only, no payloads) built once on background task; filter operates on that list, then projects to tree nodes.
- **07-01 — harden lazy contract:** Store optional `sourcePath` on `TreFile`; `Open(Stream)` either requires path parameter for lazy mode or documents test-only eager exception; add deterministic `PayloadReadCount` for tests.
- **07-01 — checked arithmetic:** `if (recordCount > 0 && recordCount > (streamLength - headerSize) / recordStride) throw ...` before `new List<TreRecord>(recordCount)`.
- **Paired cross-repo verify:** After 07-01, add CI or release step: build `TheJawaToolboxDotNet` against freshly built `UtinniCoreDotNet` (release workflow already checks out both—mirror a subset in PR CI if possible).
- **5000:** Add explicit acceptance: wrong TOC on synthetic “not actually 6000-layout” file must not throw—only `EnumerateOnly` + empty/garbage names acceptable; keeps defensive posture honest.

---

## 5. Risk Assessment

**Overall risk: MEDIUM–HIGH**

| Area | Level | Justification |
|------|--------|----------------|
| **07-01 framework / binary parsing** | **MEDIUM** | Strong TDD + threat model; main residual is Open Q1 (SWGEmu real fixture), `5000` layout guess, and lazy/stream API edge cases. |
| **07-02 UI scale + overlay** | **MEDIUM–HIGH** | Lazy expand helps; filter + overlay semantics + no CI for TJT compound execution risk. |
| **07-03 detail pane** | **MEDIUM** | Good isolation/try-catch; depends on correct lazy payload read and encryption detection. |
| **07-04 decoders** | **MEDIUM–HIGH** | Largest porting surface (C++/Python → C#); fixture quality drives confidence; UI page/shader gap vs PROD-01. |
| **Phase success criteria (all 4)** | **MEDIUM** | Criteria 1–2 achievable via 07-02 smokes; criterion 4 partial without `list-objects`; criterion 3 needs explicit scope line for UI page/shader. |

**Why not LOW:** Wave 0/fixture gap, 213k-entry search ambiguity, Repository snapshot vs “loaded” wording, and PROD-01/class coverage holes can produce a green CLI + passing smokes on SWGEmu-only paths while Restoration browse or “every asset class” claims are weak.

**Why not uniformly HIGH:** 07-01 directly fixes verified production bugs (zlib, v6000 layout); validation strategy and CLI golden path are mature from Phase 4; human checkpoints are appropriate for TJT where automation cannot go.

---

### Success criteria checklist (roadmap)

| Criterion | Plans | Verdict |
|-----------|--------|---------|
| **1** TRE Browser loads in TJT vs live client | 07-02 Task 3 | Covered (manual) |
| **2** Navigate full mount set, expand, metadata | 07-02 Tasks 1–2 + smoke | Covered if COT2000 + lazy expand work; filter/index risk |
| **3** Every PROD-01 asset class | 07-03 (IFF tree) + 07-04 (partial structured) | **Gap** for UI page + shader structured views unless IFF tree alone counts |
| **4** `parse-tre` / `list-objects` same paths as browse | 07-01 (`parse-tre`, `CotMasterIndex`) | **`parse-tre` yes; `list-objects` not planned** |

I can go deeper on any single plan (e.g. draft Wave 0 task list or a tightened 07-04 acceptance table) if useful.

---

## Consensus Summary

Both reviewers independently rate the phase **MEDIUM–HIGH risk** and agree the 4-plan split and its ordering are sound, that **07-01 is the strongest plan**, and that the read-only fence and CLI-parity intent are well expressed — but **neither would ship the plans as-is**. The substantive code risk concentrates in **07-01** (parser contract edges) and **07-04** (scope breadth + format coverage); several other items are cheap doc/criterion clarifications.

### Agreed Strengths (raised by both)
- Correct decomposition order: shared TRE parser → browser shell → detail pane → per-type decoders.
- 07-01 security posture is strong and appropriate for untrusted `.tre`/`.toc` input: STRIDE register, `ValidateLength`, 256 MB caps, `treeFileIndex < numTreeFiles` bounds, forged-count tests.
- Open Q1 handled conservatively — size-first kept for V0004/V0005/V0006 so Phase-4 `parse-tre` goldens stay byte-identical; crc-first only for V6000/COT2000.
- Read-only fence (D-01) explicit across all plans; no write/authoring/export creep.
- D-08 enforced framework-side (`Formats/` decoders + `decode-iff` verb + JSON/console grep gates) — good drift prevention for framework code.
- UI architecture matches the approved UI-SPEC (resizable `UtinniForm` via `GetForms()`, lazy `BeforeExpand` + background `Task` + `Control.Invoke`); manual checkpoints correctly limited to live-host behavior CI can't reach.

### Agreed Concerns (raised by both — highest priority)
1. **`5000` handling is unsound as specified.** (codex HIGH / cursor MEDIUM) Routing `5000` through the v6000 32-byte crc-first stride without a fixture *is* asserting layout and risks **silent mis-enumeration**. Both recommend: recognize the tag, set `EnumerateOnly`, expose **no** records (no payload assert) until a fixture exists; a synthetic non-6000-layout `5000` file must **not throw** — only enumerate-empty.
2. **`Open(Stream)` lazy contract is under-specified.** (codex HIGH / cursor MEDIUM) Lazy `GetRecordData` re-opens by stored path, but stream-backed instances (used by tests/CLI) have no defined behavior. Define it explicitly (documented `InvalidOperationException`, or store an optional `sourcePath`) and add a deterministic `PayloadReadCount` test counter instead of the current timing/IO-bound proxy.
3. **CI permanently skips the real COT2000/v6000 path.** (both HIGH) The `SWG_SAMPLE_TRE_DIR` env-gate means the most important Restoration path can rot. Add small **in-repo synthetic fixtures** (zlib block, crc-first TOC, malformed count×stride / offset+length overflow, truncated zlib trailer, unknown compressor) so v6000/COT2000 goldens run on CI.
4. **Wave 0 is a prerequisite but isn't gated.** (cursor HIGH; codex implied) `07-VALIDATION.md` Wave 0 fixture items are still unchecked, yet 07-01 assumes they exist — executors will hit red CI or skip-heavy green. Block 07-01 on Wave 0 completion (or add a `07-00` fixture plan).
5. **Plan 04 / PROD-01 "every asset class" coverage gap.** (both HIGH, different angles) Codex: 07-04 is too large (datatable + STF + object-template + mesh family + CLI + goldens + WinForms views in one) → split into ~3. Cursor: 07-04 ships **no UI-page decoder and no dedicated shader structured view**, so success criterion #3 is at risk of "chunk tree = enough." Resolution: either add lightweight shader/UI-page summaries (even FORM-tag + child-count via `IffReader`) **or** amend criterion #3 wording to state IFF-tree-only for those classes — and split the plan to shrink blast radius.
6. **Search/filter over ~213k paths is under-specified.** (cursor HIGH / codex MEDIUM) Lazy tree nodes fix the initial build, but filter needs a **flat path index** (`List<string>`/trie) built once at enumeration; a naive re-walk per 250 ms tick will spike CPU. Mitigation must cover *search*, not just initial load.
7. **Shared-code-path / criterion #4 not mechanically enforced.** (codex HIGH; cursor's `list-objects` angle) Codex: move shared browse indexing into framework (`TreArchiveIndex`/`TrePayloadResolver`) so CLI + UI provably share it — grep-for-classname gates are weak. Cursor: criterion #4 cites `parse-tre` **and `list-objects`**, but no plan migrates `ListObjectsCommand` off the provisional `OBJS` byte-scan — either add a migration task or narrow the ROADMAP criterion.

### Divergent Views (worth investigating)
- **Integer overflow on `count × stride`:** codex rates **HIGH** (autonomous agents may miss `int` overflow before validation; wants `checked` arithmetic specified by name); cursor rates it lower, noting the host is **32-bit x86** and stream-length bounds largely cover it. *Resolution:* the division-form guard (`recordCount > (streamLength - headerSize) / stride`) before allocation is nearly free — adopt codex's stricter stance.
- **Repository overlay semantics (D-05):** cursor *uniquely* flags that `Game.Repository` is a **one-time harvest snapshot** (`treefile::getAllFilenames` moves/clears the static set), so trees mounted *after* construction render as "dimmed / not loaded," contradicting D-05's "currently loaded/resolvable." Recommends documenting it as an install-time snapshot. Codex raised only the related threading question (which thread Repository access must occur on), not the snapshot semantics.
- **WinForms substring-bold:** codex *uniquely* notes standard `TreeView` cannot bold a *substring* (only the whole node) without owner-draw — the UI-SPEC requirement may be infeasible as written; bold the whole matching node or require owner-draw.

### Recommended next step
Several agreed concerns are cheap clarifications (criterion #3 wording, lazy-stream contract doc, `5000` enumerate-only acceptance, `list-objects` decision). The substantive work is: in-repo synthetic fixtures + Wave-0 gating, the `07-01` parser-contract hardening, and splitting/narrowing `07-04`. To fold these in:

`/gsd:plan-phase 7 --reviews`
