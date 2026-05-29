---
phase: 9
reviewers: [codex, cursor]
reviewed_at: 2026-05-29
plans_reviewed: [09-01-PLAN.md, 09-02-PLAN.md, 09-03-PLAN.md, 09-04-PLAN.md, 09-05-PLAN.md, 09-06-PLAN.md, 09-07-PLAN.md]
note: "claude skipped for independence (this review ran inside Claude Code). gemini/opencode/qwen not installed. cursor-agent required prompt-via-file (it hangs on piped stdin in -p mode)."
---

# Cross-AI Plan Review — Phase 9

> Two independent reviewers: **codex** (gpt-5.5) and **cursor** (cursor-agent). Each received the
> identical prompt: PROJECT.md context, full ROADMAP + REQUIREMENTS, 09-CONTEXT, 09-RESEARCH, and
> all seven 09-*-PLAN.md files. `claude` was skipped because this review executed inside Claude Code.

## Codex Review

## Summary

The Phase 9 plan set is strong in traceability, reuse of Phase 8 save/reload primitives, and explicit validation of the core "no silent schema corruption" requirement. The high-level sequencing is mostly sound: format primitives → CLI gate/UI shell → controller/save wiring → productivity features → smoke. The biggest risks are not strategic, but execution-level: several plans assume test projects can directly reference `UtinniPlugins` UI types, some byte-exact invariants are specified in ways the proposed APIs may not actually satisfy, and parts of the WinForms/DataGridView plan are over-scoped for a single phase.

## Strengths

- Clear layering: `Formats/Datatable` in `UtinniCoreDotNet`, TJT UI in `UtinniPlugins`, save/reload composition through Phase 8 primitives.
- Good reuse of existing hardened paths: `IffWriter`, `IffSaveTargets`, `TreRepackSaveTarget`, `LooseOverridePath`, `ReloadAssetClassifier`.
- The enum syntax correction is correctly identified and folded into Plan 09-01 before implementation.
- Strong emphasis on automated gates: parser tests, writer tests, CLI `roundtrip-tab`, controller undo/redo tests, save-target tests, CSV coercion tests.
- Correct product candor around datatable reload behavior: "reloads on next scene change," no fake live reload hook.
- Good preservation of DEC-C4: subpanel inside TJT, not standalone plugin.
- Plans correctly keep in-memory live patch disabled for `.tab` in V1.
- The "Save As remains enabled as escape hatch" posture is good and consistent with Phase 8.

## Concerns

- **HIGH: Core test project referencing TJT UI types is likely invalid.**
  Plan 09-03 adds `UtinniCoreDotNet.Tests/UITests/FormDatatableEditorHideNotDisposeTests.cs` and a DataGridView probe that instantiate `FormDatatableEditor` / TJT controls from `UtinniPlugins`. Unless `UtinniCoreDotNet.Tests` references `TheJawaToolboxDotNet.dll`, these tests will not compile. Adding that reference also creates awkward cross-repo build coupling.

- **HIGH: Byte-exact undo invariant is underspecified and possibly impossible with the proposed cell API.**
  Plan 09-01 says setting a cell to a new value clears `originalSlice`, then a direct revert to the old value should make serialized bytes exactly equal to the loaded bytes. That cannot hold unless the API can restore the original slice/dirty state, not just set the same typed value. This is later handled in controller commands, but the direct mutation test in 09-01 is suspect.

- **HIGH: Full-file byte-exact round-trip may be overpromised.**
  `DataTableWriter` rebuilds `COLS`, `TYPE`, and `ROWS` through `MutableIffDocument`/`IffWriter`. Per-cell slices preserve cell payload bytes, but not necessarily whole chunk payload quirks, original type-spec spelling, extra bytes, unusual chunk order, or hand-authored oddities. The plan should distinguish "canonical byte-exact for our synthetic fixtures" from "byte-exact for arbitrary real `.tab`."

- **HIGH: Plan 09-06 changes `DataTableCellValue.cs` but omits it from `files_modified`.**
  Task 1 explicitly adds `ToCsvString(DataTableColumnType ct)` to `DataTableCellValue`, but the frontmatter does not list `UtinniCoreDotNet/Formats/Datatable/DataTableCellValue.cs`.

- **MEDIUM: Golden files are missing from Plan 09-02 `files_modified`.**
  The plan says goldens are created under `Utinni.Cli.Tests/Goldens/roundtrip-tab/*.json`, but those files are not listed. That can lead to incomplete commits or unclear review scope.

- **MEDIUM: DataGridView sort test does not really test DataGridView sorting.**
  Plan 09-06's `Sort_DoesNotMutateModelOrder` only serializes the model twice. The real risk is UI save code accidentally reading `gridSurface.Rows`. The grep gate helps, but the xUnit fact as written is weak.

- **MEDIUM: Plan 09-03 says Save button stubs throw `NotImplementedException`.**
  This is acceptable only if no interim visual smoke clicks them except where explicitly expected. It creates a rough user-facing checkpoint. Prefer disabled controls or status text over deliberate exceptions.

- **MEDIUM: `DT_HashString` UX is ambiguous.**
  The plan says the model stores only the int32 hash, while UI offers free text + hash preview. For a loaded file, there is no original string. Editing/exporting/reimporting hash-string cells can easily hash an already-hashed integer string unless the UI is explicit about "hash integer" vs "source string."

- **MEDIUM: `DT_Comment` behavior remains internally inconsistent.**
  Research says SOE writer skips comment columns, but Plan 09-01 says Phase 9 must not replicate that skip to preserve hand-crafted files, while `DataTableCellValue.SerializeFresh` says DT_Comment emits nothing. That may be fine for ROWS payload, but the COLS/TYPE behavior should be made explicit.

- **MEDIUM: CSV import/apply creates another command surface before the command model is fully proven.**
  `ApplyCsvImport` needs captured original slices and dirty state restoration. If `MutableDataTableCell` does not expose a safe internal restore API, tests will force awkward access or reflection.

- **LOW: Plan scope is large.**
  Phase 9 includes typed parser/writer, full schema mutation, save modes, three entry points, CSV import/export, find/replace, sort, and live smoke. It is coherent but heavy; execution risk is mostly from breadth.

## Suggestions

- Move UI-specific tests out of `UtinniCoreDotNet.Tests`, or add a deliberate `UtinniPlugins` test project. Do not quietly introduce cross-repo UI assembly references into core tests.
- Add an explicit `MutableDataTableCell.CaptureState()` / `RestoreState()` internal API in Plan 09-01 if byte-exact undo is a required invariant. Use it in controller commands and tests.
- Reword byte-exact claims: require byte-exact for supported/canonical DTII fixtures and untouched cell payload slices, not arbitrary malformed or hand-crafted chunk layouts unless the implementation truly preserves all original chunks.
- Add all generated golden JSON files and `DataTableCellValue.cs` to the relevant `files_modified` lists.
- Replace interim `NotImplementedException` UI stubs with disabled controls plus tooltips/status text. It is less brittle during checkpoint testing.
- Add one integration-style test or review gate proving save code never reads `DataGridView.Rows`; grep is useful, but make it part of plan acceptance with exact target files.
- Clarify `DT_HashString` display/export semantics before implementation: either display stored int32 by default with optional "enter source string to compute hash," or store a UI-only last-entered source string and document it is not persisted.
- Split Plan 09-06 if execution starts to strain: CSV import/export and Find/Replace/sort are separable enough to become two plans.

## Risk Assessment

**Overall risk: MEDIUM-HIGH.**
The architecture is sound and heavily leverages already-shipped Phase 8 primitives, which lowers save/reload risk. The main risk is implementation correctness under a broad scope: byte-exact preservation, undo/redo state restoration, cross-repo WinForms testing, and DataGridView behavior all have sharp edges. Tightening the test placement and original-slice restoration model would bring this closer to MEDIUM.

---

## Cursor Review

## Summary

Phase 9 is unusually mature planning: seven wave-ordered plans with strong Phase 8 reuse, a repeated SC4 byte-exact invariant at framework/CLI/controller/CSV layers, explicit cross-repo pairing, and iter-1 fixes that corrected wave ordering (09-05 → wave 4) and cascade-state ownership. The decomposition should deliver PROD-W1-DT if executed faithfully. The main risks are not architectural ambition but **WinForms grid↔model binding underspecification**, a **D-09 sort test that does not exercise sort**, **stale contradictions inside 09-04 Task 2**, and **perf/VirtualMode decision logic that may force unnecessary VirtualMode work**.

## Strengths

- **Wave dependency graph is sound after iter-1 fixes**: 09-01 foundation → parallel 09-02/03 → 09-04 controller → serial 09-05 save/hand-offs → 09-06 productivity → 09-07 smoke.
- **SC4 round-trip fidelity is layered deliberately**: `originalSlice` hybrid DOM (09-01), `roundtrip-tab` CLI gate (09-02), controller undo test (09-04), CSV unchanged-cell preservation (09-06).
- **Phase 8 composition over reimplementation**: `IffWriter`, `IffSaveTargets`, `TreRepackSaveTarget`, `FormIffEditor` port patterns, CR-01 insert-by-reference undo — lowers integration risk.
- **Threat models and ASVS mapping** are present per plan, with DoS caps on parse (cell count, chunk size, CSV row/cell limits).
- **D-04 cascade + R-04 save block** is well specified with controller-level `NeedsReviewCount`, modal resolution, and per-menu-item disable rules.
- **Cross-repo discipline**: explicit `<Compile Include>` gates, paired commits, hide-not-dispose from commit 1 with xUnit guard.
- **09-07 smoke** mirrors Phase 8 "automation-augmented" posture with clear pass/fail/disposition vocabulary and blocking criteria for SC4 defects.

## Concerns

- **HIGH — `Sort_DoesNotMutateModelOrder` does not test sorting.** Task 3 in 09-06 serializes twice without invoking `DataGridView.Sort` or any view-order mutation. It only confirms the writer is stable, which 09-01 already guarantees. D-09's core risk (view sort leaking into save order via binding mistakes) is not actually covered.
- **HIGH — Grid↔model binding is underspecified in 09-03.** `ThemedDataGridView.BindMutable` sets `RowCount` but non-virtual mode requires explicit cell population or binding; `CellValueNeeded` applies only to VirtualMode. The plan does not clearly define how values display, how `CellEndEdit` commits to `MutableDataTableCell.Value`, or how CheckBox/ComboBox columns sync back. This is the highest execution-risk gap for the UI plan.
- **HIGH — 09-04 Task 2 contradicts iter-1 Execution Notes.** Task 2 behavior still says the form stores `lastCascadeContext`, while Execution Notes (#8) moved cascade context to `DatatableEditController.PendingCascadeContext` with required xUnit facts. Executors following Task 2 verbatim could reintroduce the rejected design.
- **MEDIUM — 09-03 perf probe may mislead VirtualMode decision.** Task 3 recommends measuring plain `DataGridView` instead of `ThemedDataGridView` + `CellFormatting` overlays; 09-06 Task 4 then builds VirtualMode unconditionally if the SUMMARY measurement is absent — a risky default that could add substantial complexity without evidence.
- **MEDIUM — 09-02 CLI "untouched cells byte-exact" comparison is underspecified.** Comparing per-cell `originalSlice` after re-parse/serialize for `--mutate-cell` is subtle; slices are cleared on edit, and post-serialize slices may not align with pre-mutation disk layout without a defined comparison algorithm (cell payload offsets in ROWS chunk).
- **MEDIUM — 09-05 introduces `controller.MarkSaved()` without a guaranteed 09-04 seam.** Save handlers assume a post-save dirty reset API that is not in 09-04's controller surface; this is a cross-plan API gap.
- **MEDIUM — Direct model mutation in 09-02 CLI for remove-row/column bypasses controller invariants.** Acceptable for golden gating, but may diverge from editor paths (e.g., `IsAdded`, cascade flags, column-cell alignment) unless mutations mirror command logic exactly.
- **MEDIUM — Stale narrative drift.** 09-04 objective still claims parallel execution with 09-05; 09-05 context still mentions overriding implicit dependency to depend only on 09-03. Could confuse orchestration/review even if wave numbers are correct.
- **MEDIUM — DT_HashString CSV round-trip is inherently lossy** (int32 on disk vs string in CSV). Documented, but smoke Step 10 "bytes equal direct edit" may fail if users expect string round-trip through CSV.
- **LOW — Duplicate fixture builders** (`DatatableFixtures` vs `DataTableFixtureBuilder`) with only optional drift detector increases maintenance burden.
- **LOW — Find/Replace regex without timeout** accepted for V1; acceptable but could freeze UI on bad patterns.
- **LOW — TRE Browser hand-off ships extension-only visibility**; DTII-in-non-.tab files require IFF Editor detour — acceptable per plan, but UX gap worth noting in smoke.

## Suggestions

- **Replace `Sort_DoesNotMutateModelOrder` with a real integration test**: STA `[Fact]` that binds a `DataGridView`/`ThemedDataGridView` to a fixture, calls `Sort` on a column, then serializes via `DataTableWriter` and asserts row order bytes match pre-sort model order (and optionally that view order differs). Keep the writer-side `grep` gate as a secondary check.
- **Add an explicit 09-03 "grid binding contract" task section**: document non-virtual population (`Rows.Add` + per-cell `.Value` assignment or `DataGridView` databinding to a view model), `CellValueChanged`/`CellEndEdit` → `controller.Apply(EditCellValue)` (or direct setter pre-09-04), and type-specific editors (CheckBox/ComboBox/NumericUpDown) commit paths.
- **Reconcile 09-04 Task 2 with Execution Notes #8**: remove all `lastCascadeContext` references; wire `btnResolveCascade` exclusively to `controller.PendingCascadeContext`; require the three cascade xUnit facts in Task 1 verification grep gates.
- **Define CLI untouched-cell comparison algorithm in 09-02**: e.g., extract each cell's ROWS payload slice by stable (row,col) indexing from both byte arrays via re-parse, or compare full file for no-mutate and structured cell payloads for mutate paths — with a golden fixture where only one cell differs.
- **Add `MarkSaved()` (or equivalent) to 09-04 controller must-haves** if 09-05 depends on it: reset `netAppliedCount`, optionally refresh baseline slices after successful save, and test it.
- **Tighten VirtualMode gating**: if 09-03 measurement is absent, default to **non-VirtualMode + documented follow-up**, not unconditional VirtualMode; or require maintainer checkpoint before enabling Task 4.
- **Perf probe should use production control path**: measure `ThemedDataGridView.BindMutable` with `CellFormatting` enabled, even if via a HintPath reference to TJT in tests.
- **Add one golden using a real extracted `.tab`** (small, in-repo synth is fine but label it) in 09-02 to complement synthetic builders and catch endianness/comment-row quirks.
- **Clarify dirty-discard on Open-replace** (09-03 truth mentions Plan 09-04 wiring); ensure 09-04/05 explicitly hook `FormSaveConfirmDialog` before `LoadDocument` replacement to avoid silent loss.

## Risk Assessment

**Overall: MEDIUM**

Justification: Format/CLI/controller/save architecture is strong and heavily test-backed (~120+ facts), with clear Phase 8 reuse and SC4 enforcement at multiple layers — that pulls risk down. Risk rises materially on the **WinForms DataGridView integration layer** (binding, edit commit, sort vs model, VirtualMode branch) and on **a few internal plan contradictions/tests that don't prove what they claim** (sort test, cascade context, CLI untouched-byte compare). Execution is likely to succeed for framework and save paths first; the UI grid seam and D-09/D-08 CSV edge cases are where schedule rework is most probable without the suggested clarifications. Phase goals remain achievable if those gaps are closed before or during 09-03/09-06 execution.

---

## Consensus Summary

Both reviewers independently judge the **architecture sound and the Phase 8 reuse excellent** — the
framework/CLI/controller/save layers carry low risk because they compose hardened, already-shipped
primitives. Both locate essentially all the real risk in the **same place: the WinForms /
DataGridView UI seam (Plan 09-03 + 09-06)** plus a handful of **plan-internal contradictions and
tests that don't prove what they claim.** Neither reviewer found a strategic/sequencing flaw —
the wave graph and SC4-at-every-layer strategy earned praise from both.

### Agreed Strengths (2+ reviewers)

- **Clean layering + Phase 8 composition over reimplementation** — `IffWriter`, `IffSaveTargets`,
  `TreRepackSaveTarget`, `LooseOverridePath`, `ReloadAssetClassifier`, `FormIffEditor` port patterns,
  CR-01 insert-by-reference undo. Both flagged this as the single biggest risk-reducer.
- **SC4 byte-exact enforced at multiple independent layers** (framework writer test → CLI
  `roundtrip-tab` gate → controller undo test → CSV unchanged-cell preservation).
- **Strong automated-gate emphasis** (~120+ facts) and explicit threat models / ASVS mapping per plan.
- **Correct product candor on reload** ("reloads on next scene change," no fake hook) and correct
  DEC-C4 / disabled-live-patch / Save-As-escape-hatch postures.

### Agreed Concerns (2+ reviewers — highest priority for `--reviews` replan)

1. **`Sort_DoesNotMutateModelOrder` (09-06) does not actually test sorting.** *(codex MEDIUM, cursor
   HIGH — strongest consensus.)* It serializes the model twice, proving only writer stability (already
   guaranteed by 09-01). The real D-09 risk — UI save code reading `gridSurface.Rows` — is untested.
   **Both** recommend a real STA integration test that calls `DataGridView.Sort` then serializes and
   asserts model order is unchanged, keeping the grep gate as secondary.
2. **DataGridView grid↔model binding / overall UI seam is the top execution risk.** *(cursor HIGH,
   codex HIGH via cross-repo test concern + "WinForms plan over-scoped".)* 09-03 sets `RowCount` but
   never defines non-virtual cell population, `CellEndEdit → MutableDataTableCell.Value` commit, or
   CheckBox/ComboBox/NumericUpDown sync-back. Add an explicit "grid binding contract" section to 09-03.
3. **Byte-exact / untouched-cell preservation is underspecified at the API + comparison level.**
   *(codex HIGH on the 09-01 undo invariant + "full-file byte-exact overpromised"; cursor MEDIUM on
   the 09-02 CLI comparison algorithm.)* Consensus fix: add an explicit
   `MutableDataTableCell.CaptureState()/RestoreState()` internal API in 09-01, define the exact
   per-cell ROWS-slice comparison algorithm in 09-02, and reword "byte-exact" to mean *canonical
   DTII fixtures + untouched cell payload slices*, not arbitrary hand-authored chunk layouts.
4. **`DT_HashString` semantics are ambiguous / CSV round-trip is lossy.** *(codex MEDIUM, cursor
   MEDIUM.)* Disk stores int32 hash; UI offers free text + preview; there is no source string for a
   loaded file. Decide before implementation: display stored int32 by default with an explicit
   "enter source string to compute hash" affordance, and document that CSV cannot round-trip the
   source string. Flag smoke Step 10 accordingly.
5. **Phase scope is broad** *(codex LOW explicit, cursor implicit via UI-seam emphasis)* — coherent
   but heavy; 09-06 is a split candidate (CSV vs Find/Replace/sort) if execution strains.

### Divergent Views (worth investigating)

- **Overall risk rating: codex MEDIUM-HIGH vs cursor MEDIUM.** Same diagnosis, different weighting —
  codex weights the cross-repo WinForms-test-compilation problem and broad scope more heavily; cursor
  weights the strong test backing more. The delta is one notch and resolves the same way: close the
  UI-seam + contradiction gaps and both converge on MEDIUM.
- **Cross-repo UI test placement (codex HIGH, unique framing).** Codex flags that
  `UtinniCoreDotNet.Tests` instantiating `FormDatatableEditor`/TJT controls (09-03) won't compile
  without a `TheJawaToolboxDotNet.dll` reference, and warns against quietly adding that coupling.
  Cursor implicitly accepts the coupling ("even if via a HintPath reference to TJT in tests"). **These
  conflict** — resolve deliberately: either a dedicated UtinniPlugins test project or a documented,
  intentional HintPath reference, not an accidental one.
- **`09-04 Task 2` contradicts iter-1 Execution Notes #8 (cursor HIGH, unique).** Cursor caught that
  Task 2's body still describes the form-local `lastCascadeContext` field that Execution Notes #8
  explicitly moved to `DatatableEditController.PendingCascadeContext`. Codex did not flag it. Verbatim
  executors could reintroduce the rejected design — **reconcile before execution.**
- **`controller.MarkSaved()` cross-plan gap (cursor MEDIUM, unique).** 09-05 save handlers assume a
  post-save dirty-reset API not present in 09-04's controller surface. Add it to 09-04 must-haves.

### Codex-only items worth folding

- **09-06 mutates `DataTableCellValue.cs` (adds `ToCsvString`) but omits it from `files_modified`** (HIGH).
- **09-02 golden JSON files not listed in `files_modified`** (MEDIUM) — risks incomplete commits.
- **`NotImplementedException` UI stubs in 09-03** are brittle at the visual checkpoint — prefer
  disabled controls + tooltip/status text (MEDIUM).
- **`DT_Comment` COLS/TYPE behavior should be made explicit** alongside the documented ROWS skip (MEDIUM).

### Cursor-only items worth folding

- **09-03 perf probe measures plain `DataGridView`, not `ThemedDataGridView` + `CellFormatting`**, and
  09-06 Task 4 defaults to *unconditional VirtualMode* when the measurement is absent — invert to
  non-VirtualMode + documented follow-up (MEDIUM).
- **Stale narrative drift** in 09-04 objective ("parallel with 09-05") and 09-05 context
  ("overriding implicit dependency") post-iter-1 wave renumber (MEDIUM).
- **Duplicate fixture builders** with only an optional drift detector; **Find/Replace regex without
  timeout**; **TRE-Browser extension-only hand-off visibility** (all LOW).

---

*Recommended next step:* feed this back into planning with `/gsd:plan-phase 9 --reviews`. The
highest-leverage replan targets, in order: (1) real sort test + explicit 09-03 grid-binding
contract; (2) `CaptureState/RestoreState` API in 09-01 + defined 09-02 comparison algorithm + reworded
byte-exact claims; (3) reconcile 09-04 Task 2 vs Execution Notes #8 and add `MarkSaved()` to 09-04;
(4) decide cross-repo UI-test placement deliberately; (5) `files_modified` completeness
(`DataTableCellValue.cs`, 09-02 goldens) + DT_HashString semantics decision.
