---
phase: 9
slug: tjt-subpanel-datatable-editor-tab
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-29
last_revised: 2026-05-29
revision_history:
  - "iter-1 (2026-05-29): populated Per-Task Verification Map from each plan's <verify><automated> blocks per checker WARNING #3; nyquist_compliant flipped true (every task has an automated verify); wave_0_complete stays false (Wave 0 files are created during execution)."
---

# Phase 9 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> **Derived from** `09-RESEARCH.md` § Validation Architecture (research date 2026-05-29).
> Per-task entries populated from each PLAN.md `<verify><automated>` block (iter-1, 2026-05-29).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Managed framework** | xUnit 2.x (`UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` — SDK-style, auto-globs `**/*.cs`) |
| **CLI golden harness** | xUnit 2.x in `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` (reuses `Infrastructure/GoldenTestRunner.cs` + `Infrastructure/InProcessCliRunner.cs` from Phase 8 `RoundtripIffCommandTests`) |
| **Native framework** | Catch2 v3 (NOT consumed in Phase 9 — pure managed) |
| **Preservation grep gates** | xUnit fail-on-violation Facts in `UtinniCoreDotNet.Tests/PreservationAudit/` (Phase 6 STAB-04 pattern) |
| **Config file** | None (xUnit auto-discovers) |
| **Build tool** | VS 2026 MSBuild (mandatory — `dotnet build` fails on WinForms image .resx per `feedback_dotnet_build_msbuild_resources`); `dotnet test --no-build` is the run command |
| **Quick run command** | `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~Datatable"` |
| **Full suite command** | `dotnet test UtinniCoreDotNet.Tests --no-build && dotnet test Utinni.Cli.Tests --no-build && dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~PreservationAudit"` |
| **Estimated runtime** | quick: < 5 s steady-state; full: ~30–60 s across all three suites Debug\|x86 |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~Datatable"` (Datatable subsuite, target < 5 s)
- **After every plan wave:** Run full suite (UtinniCoreDotNet.Tests + Utinni.Cli.Tests + PreservationAudit) BOTH `Debug|x86` and `Release|x86` MSBuild clean across both repos (Utinni + UtinniPlugins)
- **Before `/gsd:verify-work`:** Full suite green AND `roundtrip-tab` golden green AND `/gsd:code-review 09` cross-AI gate AND maintainer-driven Tier-4 live-SWG smoke per Phase 8 precedent (smoke=automation-augmented; live ACK deferred-but-acceptable)
- **Max feedback latency:** < 5 s for quick subsuite; ≤ 60 s for full suite

---

## Wave Layout (iter-1, revised)

| Wave | Plans | Notes |
|------|-------|-------|
| 1 | 09-01 | Framework primitives (parallel with nothing — wave gate) |
| 2 | 09-02, 09-03 | CLI gate + TJT host (parallel — disjoint files_modified) |
| 3 | 09-04 | Controller + cascade + T4 ops (touches FormDatatableEditor.cs) |
| 4 | 09-05 | Save targets + entry-point hand-offs (rebases on 09-04 FormDatatableEditor.cs) |
| 5 | 09-06 | Bulk-edit features (CSV / Find/Replace / sort / frozen-row) |
| 6 | 09-07 | Tier-4 live-SWG smoke (maintainer-driven; phase capstone) |

**Wave gate rule:** same-wave plans MUST have zero `files_modified` overlap. Plan 09-04 and 09-05 BOTH touch `FormDatatableEditor.cs` so they are serialized via `09-05 depends_on: ["09-03", "09-04"]` (iter-1 fix #5 Option B).

---

## Per-Task Verification Map

> Populated 2026-05-29 (iter-1) from each PLAN.md `<verify><automated>` block per checker WARNING #3.
> One row per implementation/test task across all 7 plans. Checkpoint tasks (`type="checkpoint:*"`) have `<how-to-verify>` instead of automation and are listed in the **Manual-Only Verifications** table below.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 09-01-T0 | 09-01 | 1 | PROD-W1-DT | — | UI-SPEC A1 typo correction (planner artifact integrity) | grep gate | `grep -c "e[a:0,b:1,c:2]" 09-UI-SPEC.md 09-CONTEXT.md` returns 0:0 AND `grep -c "e(a=0,b=1,c=2)" 09-UI-SPEC.md 09-CONTEXT.md` returns ≥ 1 in both | ✅ (planning artifacts) | ⬜ pending |
| 09-01-T1 | 09-01 | 1 | PROD-W1-DT | T-09-01, T-09-04 | DataTableColumnType + DataTableHashCrc port (DT_HashString hash parity) | xUnit | `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~DataTableColumnTypeTests\|FullyQualifiedName~DataTableHashCrcTests"` exits 0 with ≥ 30 facts | ❌ W0 | ⬜ pending |
| 09-01-T2 | 09-01 | 1 | PROD-W1-DT (4 / SC4) | T-09-01, T-09-03, T-09-06 | Typed reader + hybrid-DOM + writer + SC4 byte-exact-on-untouched (framework layer) | xUnit | `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~DataTableDocumentTests\|FullyQualifiedName~DataTableWriterTests"` exits 0 with ≥ 30 facts; D-09 grep `grep -c "dataGridView" DataTableWriter.cs MutableDataTableDocument.cs` returns 0:0; `grep -c "IffWriter.Write" DataTableWriter.cs` returns ≥ 1 | ❌ W0 | ⬜ pending |
| 09-02-T1 | 09-02 | 2 | PROD-W1-DT (4 / SC4) | T-09-07 | roundtrip-tab verb + Program.cs wire (composition) | build + grep | `dotnet build Utinni.Cli --no-restore` exits 0; `grep -c "RoundtripTabOptions" Utinni.Cli/Program.cs` returns 2; `grep -c 'Verb("roundtrip-tab"' RoundtripTabCommand.cs` returns 1; `ls DataTableFixtureBuilder.cs` succeeds | ❌ W0 | ⬜ pending |
| 09-02-T2 | 09-02 | 2 | PROD-W1-DT (4 / SC4) | T-09-07, T-09-06 | roundtrip-tab golden suite (SC4 CLI gate — byte-exact on untouched cells after --mutate-cell) | xUnit golden | `dotnet test Utinni.Cli.Tests --no-build --filter "FullyQualifiedName~RoundtripTabCommandTests"` exits 0 with ≥ 10 facts; ≥ 1 golden file in `Utinni.Cli.Tests/Goldens/roundtrip-tab/`; named SC4 [Fact] present | ❌ W0 | ⬜ pending |
| 09-03-T1 | 09-03 | 2 | PROD-W1-DT (2b) | T-09-10, T-09-11 | TJT controls (ThemedDataGridView token map; column factory; floating hash editor; numeric editing control) | MSBuild + grep | TJT MSBuild Debug\|x86 green; `grep -c "UI\\Controls\\ThemedDataGridView.cs" csproj` returns 1; `grep -c "Color.FromArgb" ThemedDataGridView.cs` returns 0 (no raw ARGB literals) | ❌ W0 | ⬜ pending |
| 09-03-T2 | 09-03 | 2 | PROD-W1-DT (1, 2b), T-09-12 | T-09-12, T-09-13 | FormDatatableEditor + Plugin.cs reg + hide-not-dispose regression guard | MSBuild + xUnit + grep | TJT MSBuild Debug\|x86 green; `dotnet test --filter "FullyQualifiedName~FormDatatableEditorHideNotDisposeTests"` exits 0 with ≥ 2 [StaFact]s; `grep -c "FormDatatableEditor" Plugin.cs` ≥ 1; `grep -c "this.Controls.Add(this.gridSurface);" Designer.cs` returns 1 | ❌ W0 | ⬜ pending |
| 09-03-T3 | 09-03 | 2 | PROD-W1-DT (2b) | T-09-11 | DataGridView bind-latency probe (Plan 09-06 VirtualMode decision hinge); SUMMARY-record gate (iter-1 fix #6) | xUnit perf probe | `dotnet test --filter "FullyQualifiedName~DataGridViewBindLatencyProbeTests" -- xUnit.parallelizeAssembly=false` exits 0; stdout contains `DataGridViewBindLatency_CombatDataTableLike:` prefix; `grep -E "DataGridViewBindLatency.*[0-9]+\.[0-9]+ ms" 09-03-SUMMARY.md` returns ≥ 1 line | ❌ W0 | ⬜ pending |
| 09-03-T4 | 09-03 | 2 | PROD-W1-DT (1, 2b) | T-09-10, T-09-12 | Maintainer visual sanity check — FormDatatableEditor open path renders | **checkpoint:human-verify** | See Manual-Only Verifications below | ✅ (live host) | ⬜ pending |
| 09-04-T1 | 09-04 | 3 | PROD-W1-DT (T4 schema mutation — D-01), D-04 | T-09-15, T-09-18 | DatatableEditController + 11 commands + cascade + insert-by-reference + SC4 controller-layer | xUnit | `dotnet test --filter "FullyQualifiedName~DatatableEditControllerTests"` exits 0 with ≥ 30 (iter-1: ≥ 33) facts; `grep -c "Editing\\DatatableEditController.cs" csproj` returns 1; `grep -c "InsertAt" DatatableEditCommands.cs` ≥ 2 (RemoveRow + RemoveColumn UndoOps); `grep -c "SC4 (controller)" DatatableEditControllerTests.cs` returns 1 | ❌ W0 | ⬜ pending |
| 09-04-T2 | 09-04 | 3 | PROD-W1-DT, D-02, D-04, R-04 | T-09-15, T-09-16 | FormAddColumnDialog + FormTypeChangeCascadeDialog + FormDatatableEditor controller wire + D-02 safety-net + NeedsReview save block | MSBuild + grep + xUnit | TJT MSBuild Debug\|x86 green; `grep -c "FormAddColumnDialog" FormDatatableEditor.cs` ≥ 1; `grep -c "FormSaveConfirmDialog" FormDatatableEditor.cs` ≥ 1 (D-02 reuse); `grep -c "NeedsReviewCount" FormDatatableEditor.cs` ≥ 2; `grep -c "controller.Apply" FormDatatableEditor.cs` ≥ 5; full Datatable subsuite green | ❌ W0 | ⬜ pending |
| 09-05-T1 | 09-05 | 4 | PROD-W1-DT (2d / SC4), CF-03 mode 3 disabled, WR-06 | T-09-20, T-09-22 | DatatableSaveTargets composition shim + reload-routing defense-in-depth | MSBuild + xUnit + line-count | TJT MSBuild Debug\|x86 green; `dotnet test --filter "FullyQualifiedName~DatatableSaveTargetsTests\|FullyQualifiedName~DatatableReloadRoutingTests"` exits 0 with ≥ 8 facts; `wc -l DatatableSaveTargets.cs` < 100 lines; `grep -c "Saving\\DatatableSaveTargets.cs" csproj` returns 1 | ❌ W0 | ⬜ pending |
| 09-05-T2 | 09-05 | 4 | PROD-W1-DT (2a / 2d), D-10.2, D-10.3, R-04 | T-09-20, T-09-23, T-09-24 | FormDatatableEditor Save▾ + OpenFromMutableIff + reload-dispatch; TRE Browser + IFF Editor hand-offs | MSBuild + grep + xUnit | TJT MSBuild Debug\|x86 green; `grep -c "DatatableSaveTargets" FormDatatableEditor.cs` ≥ 4; `grep -c "FindOrCreateDatatableEditor" FormTreBrowser.cs FormIffEditor.cs` ≥ 1 per file; `grep -c "OpenFromMutableIff\|OpenFromTreEntry" FormDatatableEditor.cs` ≥ 2; `grep -c "Switch to typed datatable view" FormIffEditor.cs` ≥ 1; `grep -c "Open in Datatable Editor" FormTreBrowser.cs` ≥ 1; full Datatable subsuite green | ❌ W0 | ⬜ pending |
| 09-06-T1 | 09-06 | 5 | PROD-W1-DT (D-08 CSV byte-exact-on-untouched / SC4) | T-09-27, T-09-28 | CsvCellCoercion framework helper + ApplyCsvImportCommand transaction wrapper | xUnit | `dotnet test --filter "FullyQualifiedName~CsvCellCoercionTests\|FullyQualifiedName~ApplyCsvImport"` exits 0 with ≥ 12 combined facts; `grep -c "Formats\\Datatable\\CsvCellCoercion.cs" csproj` returns 1; `grep -c "NotImplementedException" DatatableEditCommands.cs` returns 0 (stub replaced); `grep -c "ApplyCsvImportCommand" DatatableEditCommands.cs` ≥ 1 | ❌ W0 | ⬜ pending |
| 09-06-T2 | 09-06 | 5 | PROD-W1-DT (D-08) | T-09-27 | TJT-side DatatableCsvSerializer + FormCsvImportPreviewDialog | MSBuild + grep | TJT MSBuild Debug\|x86 green; `grep -c "DatatableCsvSerializer\|CsvCellCoercion" DatatableCsvSerializer.cs` ≥ 1 each; csproj contains `Saving\DatatableCsvSerializer.cs` + 2 entries for `FormCsvImportPreviewDialog.cs` (Form + Designer) | ❌ W0 | ⬜ pending |
| 09-06-T3 | 09-06 | 5 | PROD-W1-DT (D-07 Find/Replace; D-09 view-only sort; comment-row toggle) | T-09-29 | FormDatatableEditor Find/Replace + Import/Export CSV + column-click sort + DT_Comment frozen-row + Sort_DoesNotMutateModelOrder regression fact | MSBuild + xUnit + grep | TJT MSBuild Debug\|x86 green; `dotnet test --filter "FullyQualifiedName~Sort_DoesNotMutateModelOrder"` exits 0; D-09 grep `grep -c "dataGridView" DatatableSaveTargets.cs DataTableWriter.cs MutableDataTableDocument.cs` returns 0:0:0; `grep -c "SortMode.Automatic" FormDatatableEditor.cs` ≥ 1; `grep -c "DatatableCsvSerializer" FormDatatableEditor.cs` ≥ 2; `grep -c "Edit comment rows" Designer.cs` ≥ 1; `grep -c "View order only" FormDatatableEditor.cs` ≥ 1 | ❌ W0 | ⬜ pending |
| 09-06-T4 | 09-06 | 5 | PROD-W1-DT (2b perf) | T-09-11 | Conditional VirtualMode fallback (gated on 09-03-T3 measurement; iter-1 fix #6 ABSENT-fallback applies) | conditional | `echo "Task 4 is conditional — verify status in 09-06-SUMMARY"` (no-op; SUMMARY documents executed-vs-skipped per the iter-1 fix #6 PRE-FLIGHT rule) | ❌ W0 (conditional) | ⬜ pending |
| 09-07-T1 | 09-07 | 6 | PROD-W1-DT (all 4 criteria) | T-09-33, T-09-34, T-09-35 | Tier-4 maintainer-driven live-SWG smoke (smoke=automation-augmented per Phase 8 precedent) | **checkpoint:human-verify (blocking-human)** | See Manual-Only Verifications below; outcome recorded in `09-07-SMOKE-LOG.md` | ✅ (live SWG) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

These files do not yet exist and must be added before later waves can sample
them. The plan that introduces each file is the "Wave 0" owner for that file;
downstream plans depend on it via `depends_on`.

**Framework primitives (`UtinniCoreDotNet/Formats/Datatable/` — NEW):**
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableColumnTypeTests.cs` — per-discriminator parse + `MangleValue` per `DT_*`. ~25–30 [Fact]s. (Owner: 09-01-T1)
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableHashCrcTests.cs` — CRC parity vs reference values. ~4–6 [Fact]s. (Owner: 09-01-T1)
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableDocumentTests.cs` — V0 + V1 fixtures; DT_Comment skip; null-cell defaults; cell-count mismatch error; per-DT_* read. ~15–20 [Fact]s. (Owner: 09-01-T2)
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableWriterTests.cs` — round-trip byte-exact (no edits); per-DT_* serialize; chunk-length roll-up; over-cap chunk rejection (via Phase 8 IffWriter inheritance); Sort_DoesNotMutateModelOrder (iter-1 added by 09-06-T3). ~15–20 [Fact]s + 1 (sort). (Owner: 09-01-T2; sort fact 09-06-T3)
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DatatableEditControllerTests.cs` — 11 commands × apply/undo/redo identity; baseline-clean dirty; single-transaction CSV import; type-change cascade flags needs-review; save-blocked while needs-review > 0; cascade-context state machine (iter-1 fix #8); 3 ApplyCsvImport facts (iter-1 added by 09-06-T1). ~30–40 [Fact]s. (Owner: 09-04-T1; CSV facts 09-06-T1)
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/CsvCellCoercionTests.cs` — per-DT_* coercion success/failure + CSV round-trip byte-exact-on-untouched + DoS caps. ~10–15 [Fact]s. (Owner: 09-06-T1)
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DatatableFixtures.cs` — synthetic .tab builders. (Owner: 09-01-T2)
- [ ] `UtinniCoreDotNet.Tests/UITests/FormDatatableEditorHideNotDisposeTests.cs` — singleton-form regression guard. (Owner: 09-03-T2)
- [ ] `UtinniCoreDotNet.Tests/PerfProbes/DataGridViewBindLatencyProbeTests.cs` — DataGridView perf probe. (Owner: 09-03-T3)
- [ ] `UtinniCoreDotNet.Tests/SavingTests/DatatableSaveTargetsTests.cs` — save shim coverage + V6000 refusal. (Owner: 09-05-T1)
- [ ] `UtinniCoreDotNet.Tests/SavingTests/DatatableReloadRoutingTests.cs` — DTII reload-routing defense-in-depth. (Owner: 09-05-T1)

**CLI round-trip gate:**
- [ ] `Utinni.Cli/Commands/RoundtripTabCommand.cs` — `roundtrip-tab` verb. (Owner: 09-02-T1)
- [ ] `Utinni.Cli.Tests/Commands/RoundtripTabCommandTests.cs` — golden suite. (Owner: 09-02-T2)

**Test fixtures (synthetic; no on-disk `.tab` checked in per Open Question 3 / Assumption A6):**
- [ ] `Utinni.Cli.Tests/Infrastructure/DataTableFixtureBuilder.cs` — emits valid DTII bytes for all seven fixtures. (Owner: 09-02-T1)

**TJT-side host + save plumbing (no new infrastructure — REUSES Phase 8):**
- [ ] `TheJawaToolboxDotNet/Saving/DatatableCsvSerializer.cs` — per-cell coercion extraction lives in `UtinniCoreDotNet/Formats/Datatable/CsvCellCoercion.cs` (framework-side per checker B-1; this TJT file is thin file-I/O + parser). (Owner: 09-06-T1 framework, 09-06-T2 TJT)

**Framework install:** N/A — pre-existing (xUnit 2.x, CommandLineParser, Newtonsoft.Json already on disk; CI green since Phase 4/8).

---

## Manual-Only Verifications

All below are **Tier-4 maintainer-driven live-SWG smokes** with no automated
substitute. Each maps to Phase 8 precedent (smoke=automation-augmented; live ACK
deferred-but-acceptable for V1 sign-off).

| Task | Plan | Behavior | Requirement | Why Manual | Test Instructions |
|------|------|----------|-------------|------------|-------------------|
| 09-03-T4 | 09-03 | Maintainer visual sanity check — FormDatatableEditor open path renders | PROD-W1-DT (1, 2b) | Visual + interactive — host menu open / theme paint / second-open hide-not-dispose under live MEF | See 09-03-PLAN.md Task 4 `<how-to-verify>` (10-step list). Resume signal: type "approved" or paste failure dialog text. |
| 09-07-T1 | 09-07 | Tier-4 live-SWG smoke (full feature surface against injected SWG) | PROD-W1-DT (all 4 criteria) | Requires live SWG client + injection harness + in-game observation; no in-process surrogate | See 09-07-PLAN.md Task 1 `<how-to-verify>` (13-step list, three option dispositions A/B/C). Outcome recorded in `09-07-SMOKE-LOG.md` with maintainer signature. |

The four originally-listed manual behaviors from iter-0 (subpanel-loads-against-live-SWG / picks-up-edit-on-scene-change / .tre-repack-round-trip / singleton-second-open) are subsumed under 09-07-T1's 13-step smoke; the 09-03-T4 maintainer visual check covers the pre-smoke editor-host sanity prior to full live integration.

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify (or are documented checkpoint tasks with `<how-to-verify>`) — iter-1 (2026-05-29)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify (the two checkpoint tasks 09-03-T4 + 09-07-T1 are at wave boundaries and bracketed by automation-bearing tasks)
- [ ] Wave 0 covers all MISSING references (all 12 W0 files listed above; created during execution)
- [x] No watch-mode flags
- [x] Feedback latency < 5 s for quick subsuite
- [x] `nyquist_compliant: true` set in frontmatter — iter-1 (2026-05-29)

**Approval:** pending iter-1 checker re-review.
