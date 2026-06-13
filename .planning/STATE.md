---
gsd_state_version: 1.0
milestone: v2.0
milestone_name: — "AI-Assisted SWG Tools
status: executing
stopped_at: Completed 15-05-PLAN.md (RESID-04 fullscreen suppress + no-Reset gate)
last_updated: "2026-06-13T01:43:00.163Z"
last_activity: 2026-06-13 -- Phase 15 planning complete
progress:
  total_phases: 20
  completed_phases: 15
  total_plans: 81
  completed_plans: 78
  percent: 96
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-06-01)

**Core value:** A modder downloads Utinni, installs once, and from a single application can see, edit, and live-preview every asset the SWG client loads — replacing the fragmented 15-year-old editor zoo with one stable, plugin-driven tool.
**Current focus:** Phase 15 — wave-2-editors-worldsnapshot-particle-presentation-residuals

## Current Position

Milestone: v2.0 "AI-Assisted SWG Tools" (Phases 12–16)
Phase: 15 (wave-2-editors-worldsnapshot-particle-presentation-residuals) — EXECUTING
Plan: 8 of 8
Status: Ready to execute
Last activity: 2026-06-13 -- Phase 15 planning complete

## v2.0 Roadmap Summary (created 2026-06-01)

V1 shipped `v1.0.0` 2026-06-01 (Phases 1–11; all five Wave-1 subpanels demoed live). Milestone v2.0 turns Utinni from a tool that *edits* assets into one that *authors* them and makes the pipeline AI-drivable. Five phases, research-recommended build order (revive-spike-as-hard-gate first; headless-MCP before live):

| Phase | Goal | Requirements |
|-------|------|--------------|
| 12 | Revive-feasibility spike (HARD GATE) — lift-shift `TreeFileBuilder`/`TemplateCompiler`/`TemplateDefinitionCompiler` to a Utinni-owned `tools/` tree, verify standalone v145 build/link, strip dead deps, manifest + pinned SHA; fix intro-skip crash | AUTH-01, RESID-02 |
| 13 | Wrap revived compilers as `utinni-cli` verbs (`compile-*`/`build-*`) + new SAVE verb + close OT Tier-2 typed display | AUTH-02, AUTH-03, AUTH-04, AUTH-05, AUTH-06, RESID-01 |
| 14 | Headless `Utinni.Mcp` (net10, stdio) — read tools + write/SAVE tools w/ loose-override default, verify-before-commit, fail-closed root, MCP-SECURITY.md | MCP-01, MCP-02 |
| 15 | Wave-2 editors (WorldSnapshot first, then Particle `.prt` codec) as TJT SubPanels + presentation residuals | PROD-W2-WS, PROD-W2-PRT, RESID-03, RESID-04 |
| 16 | Live-injected MCP bridge (named-pipe IPC, optional/last) + formalize Blender file-format boundary | MCP-03, ECO-01 |

**Coverage:** 16/16 v2.0 requirements mapped, no orphans, no duplicates.
**Research flags:** Phase 12 (empirical build pass), Phase 15 (`.prt` codec depth), Phase 16 (live-IPC mechanism) — plan with `--research-phase`.

## Wave 2 Summary

**Wave 1 (06-01) shipped earlier:** Overlay-debug investigation; ImGui Demo screen Tier-4 sign-off; HUD-style overlay directive captured.

**Wave 2 (06-02) merged 2026-05-24 (commit `2f57dfa`):** vcpkg manifest mode + OutputSink CON-N-09 fence + PlatformToolset v142 → v145 sweep + VSIX widen `[16.0,19.0)` + CppSharp parser pinned to VS 2019 14.29 STL (Path 1). Two independent pre-merge reviews:

- **CODEX**: accept + 4 pre-merge cleanups (fail-hard builtins, numeric Version ordering, BOM strip, untracked-file confirmation) → applied as `adc72f8`
- **cursor**: accept + per-type block hash certification (119/119 partial-class blocks byte-identical; only inter-type reordering; no `[ModuleInitializer]`/`[ComImport]`/explicit-`cctor` concerns); preferred follow-up = commit Path 1-regen baseline → applied as `d69988d`

CppSharp blocker resolved by Path 1 (parser-include redirect to VS 2019 14.29 STL while build uses v145). The vendored CppSharp 0.10.5's clang 11 parser now reads its original-pairing STL; the v145-built `UtinniCore.dll` is unaffected.

**Deferred to follow-on plan 06-02b (post-V1):** the per-dep vcpkg migration commits (delete `external/{catch2,spdlog,imgui,imguizmo}` + rewire .vcxproj include/lib paths). The vcpkg manifest currently declares the deps but the build still resolves them via the vendored `external/` trees. Not blocking v1.0-rc.1.

See: `[[project-vs2026-cppsharp-block]]` auto-memory; `06-02-PATH1-SUMMARY.md`; `.planning/research/cppsharp-msvc-14.5-upgrade.md`.

## Wave 3 Summary

**Wave 3 (06-03) complete 2026-05-25** — closed the last two STAB-05 open questions:

- **CON-O-08 + CON-B-03 (commit `4f5b5b6`, CI-green):** DXSDK June 2010 retired. The sole `D3DXVECTOR3` (the RESZ depth-resolve dummy vertex in `depth_texture.cpp`) is now a local 3-float `Vec3`; passed by address to `DrawPrimitiveUP`. DXSDK include/lib paths stripped from `UtinniCore.vcxproj` (3 configs) + CI Verify step removed. DirectXMath is the documented forward path (`CONVENTIONS.md`).
- **CON-O-06 (commit `164ca59`):** LeksysINI deleted; replaced by a hand-rolled, round-trip-preserving INI parser inside the PIMPL `UtINI::Impl`. `utini.h` byte-for-byte unchanged → all 15+ callsites untouched. Raw-line model preserves order/comments/blanks/malformed lines; coercion mirrors legacy AsBool/AsInt/AsDouble.
- **Fence (commit `a18f503`):** 12 Catch2 cases in `UtinniCore.Tests/UtINI/IniParserTests.cpp`; full native suite 76 assertions / 26 cases green.

All 8 CON-O-01..08 now dispositioned in `assessment.md` §Open questions. Executed inline (worktrees disabled this session) per the Windows/vcpkg build-recipe rationale. See `06-03-SUMMARY.md`.

## Wave 4 + 5 Summary

**Wave 4 (06-04) complete 2026-05-25** — folded CI-stability fixes: loader-lock-harness 50ms contention flake (best-of-3 min) + GameCallbacks ForceGCCollect AV isolation. Per-fix atomic + regression assertion. See `06-04-SUMMARY.md`.

**Wave 5 (06-05) complete 2026-05-25, CI-green** — STAB-03 cleanups + STAB-04 audit, executed inline (20 atomic commits on master + paired UtinniPlugins `c9cfa9d`):

- Full-repo `.clang-format` (Allman/4-space, left pointers) in one commit + `.git-blame-ignore-revs` + CI `clang-format check` style gate.
- `TJT.ico`/`TJT.png` ejected to UtinniPlugins/TheJawaToolbox; neutral gear `utinni.ico` is the new UtinniForm default. D-16 polish bundle (Directory.Build.props SDK unify, Prefer32Bit drop, ExampleEditorPlugin path, .gitignore doc, namespace Std, `Native.SendMessage` IntPtr+shim, licenses.txt UTF-8/DetourXS/nvapi, typos).
- Dead-code purge (Launcher VS-attach machinery, dead render/scene/io hooks, empty stubs) with the `swg/ui/` carve-out preserved.
- STAB-04: `06-AUDIT.md` (23 foundations PASS) + 23 fail-on-violation xUnit grep Facts in `UtinniCoreDotNet.Tests/PreservationAudit/`; regression probe confirmed fail-on-violation. **STAB-03 + STAB-04 delivered.** See `06-05-SUMMARY.md`.

**Remaining in Phase 6:** 06-06 (TEST-04 Tier-4 residual doc + WiX MSI installer + `release.yml` + `v1.0.0-rc.1` tag). Phase-level verification + completion run after 06-06.

## Performance Metrics

**Velocity:**

- Total plans completed: 34
- Average duration: —
- Total execution time: —

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| — | — | — | — |
| 01 | 2 | - | - |
| 04 | 4 | - | - |
| 05 | 2 | - | - |
| 06 | 7 | - | - |
| 08 | 7 | - | - |
| 09 | 7 | - | - |
| 11 | 5 | - | - |

**Recent Trend:**

- Last 5 plans: (none yet)
- Trend: (no data)

*Updated after each plan completion*
| Phase 07 P00 | 25 min | 2 tasks | 14 files |
| Phase 07 P01 | 2h | 3 tasks | 19 files |
| Phase 07 P02 | 2h | 3 tasks | 6 files |
| Phase 07 P03 | 1.5h | 4 tasks | 8 files |
| Phase 07 P04a | 3h | 2 tasks | 12 files |
| Phase 07 P04b | 3h | 3 tasks | 7 files |
| Phase 08 P01 | ~95 min | 4 tasks | 9 new + 2 modified |
| Phase 08 P02 | ~75 min | 2 tasks | 10 new + 3 modified |
| Phase 08 P03 | ~8 min | 2 tasks | 1 new + 2 modified |
| Phase 08 P04 | 15 | 4 tasks | 9 files |
| Phase 08 P05 | ~240 min | 5 tasks (4 auto + 1 smoke) | 8 new + 5 modified |
| Phase 08 P06 | ~30 min | 5 tasks (4 auto + 1 smoke-automation-only) | 5 new + 4 modified |
| Phase 08 P07 | ~360 min | 4 tasks (3 auto + 1 smoke-automation-augmented via continuation Tasks 5/5a-5e) | 10 new + 6 modified across both repos |
| Phase 09 P01 | ~50 min | 3 tasks (Task 0 docs + 2 TDD) | 15 new + 3 modified |
| Phase 09 P02 | 40 min | 2 tasks | 18 files |
| Phase 09 P03 | ~95 min | 3 auto tasks (+ 1 live-host checkpoint pending) | 9 new + 3 modified across both repos |
| Phase 09 P04 | 75 min | 2 tasks | 13 files |
| Phase 09 P05 | ~70 min | 2 tasks | 7 files across both repos |
| Phase 09 P06 | 75 min | 4 tasks | 14 files |
| Phase 09 P07 | ~20 min | 1 checkpoint task (automation pre-checks + smoke artifact; live ACK pending) | 3 files |
| Phase 11 P01 | 45 min | 2 tasks | 8 files |
| Phase 11 P02 | ~12min | 2 tasks | 13 files |
| Phase 11 P03 | ~9min | 2 tasks | 7 files (cross-repo) |
| Phase 11 P04 | 28min | 2 tasks | 7 files |
| Phase 12 P01 | ~3.5h | 2 tasks | 440 files |
| Phase 12 P02 | ~2h | 4 tasks | 2700 files |
| Phase 12 P03 | ~1h | 3 tasks | 4 files |
| Phase 12 P04 | ~1h | 4 tasks (2 checkpoints) | 2 new + 1 modified |
| Phase 14 P01 | 35min | 3 tasks | 18 files |
| Phase 14 P03a | 12min | 2 tasks | 12 files |
| Phase 14 P02 | ~20min | 2 tasks | 4 files |
| Phase 14 P03 | ~25min | 2 tasks | 7 files |
| Phase 14 P04 | ~50min | 4 tasks | 12 files |
| Phase 15 P01 | ~75 min | 2 tasks | 10 files |
| Phase 15 P02 | ~95 min | 3 tasks | 13 files |
| Phase 15 P03 | ~45 min | 2 tasks | 4 files |
| Phase 15 P04 | ~40min | 2 tasks | 11 files |
| Phase 15 P05 | ~25min | 2 tasks | 5 files |
| Phase 15 P06 | ~70min | 2 tasks | 9 files (cross-repo) |
| Phase 15 P07 | ~20min | 1 tasks | 2 files |

## Accumulated Context

### Roadmap Evolution

- Phase 02.1 inserted after Phase 2: Phase 02 gap closure — critical correctness + harness quality from 02-REVIEW.md (URGENT)
- 2026-06-01: V1 shipped (`v1.0.0`, Phases 1–11). Milestone v2.0 "AI-Assisted SWG Tools" roadmapped — Phases 12–16 appended (revive-spike HARD GATE → revive+wrap/OT-Tier-2 → headless MCP → Wave-2 editors → live-MCP+Blender). 16 v2.0 requirements mapped, 100% coverage. Backlog 999.1 (MCP server) is now realized by Phases 14 + 16; retained in Backlog pending `/gsd:review-backlog` close.

### Decisions

Full decision log lives in PROJECT.md Key Decisions table. V1 starts with four locked anti-goal decisions (DEC-A1..A4 — not a server-side manager, not a launcher, not a DCC, not a cheat enabler) and three non-locked candidate decisions (DEC-C1 product target, DEC-C2 anti-goals as scope filter, DEC-C3 tiered testing strategy). v2.0 adds the LOCKED lift-and-shift constraint (revive build tools into a Utinni-owned `tools/` tree at shared v145; never build in `swg-client-v2`) and the separate-process headless-first MCP shape (net10 stdio shelling `utinni-cli`; never host the SDK in-proc in the x86 client).

- [Phase ?]: Phase 08 P03: extracted Phase 7's IFF chunk-tree TreeView + BuildChunkNode into shared IffChunkTree UserControl (D-09); TreDetailPane delegates LoadIff to it with zero public-read-API change.
- [Phase ?]: Phase 08 P04: FormIffEditor editable host + IffEditController editor-local undo/redo (D-08) + 8 D-03 structural ops + D-04 leaf editing in hex/text/replace-from-file modes; cross-repo (Utinni controller + tests, UtinniPlugins forms); round-2 HIGH-A csproj coverage closed across both old-style projects.
- [Phase 8]: Phase 08 P05 auto tasks: framework-side LooseOverridePath (root-containment, 14 tests) + ReloadAssetClassifier (4-tier routing table, 22 tests) + TreRecordIndexResolver (W-3 degraded fallback, 4 tests); plugin-side IffSaveTargets (SaveLooseOverride / SaveToPath / SaveInPlace with Flush(true) MEDIUM-9 barrier) + ClientReloadDispatcher (game-thread tiered reload with GroundScene.Get().ReloadTerrain() INSTANCE call); FormIffEditor Save▾ menu (Save in place / Save as loose override / Save As… / Patch live client / Repack — Source-gated per W-3 + round-2 MEDIUM 5) + Reload-in-client (4 outcomes) + Open… + TRE Browser hand-off via OpenFromTreEntry; FormTreBrowser context-menu Open-in-IFF-Editor; Plugin.cs registration in try/catch (SPI unchanged). 143/143 IFF + Saving tests pass.
- [Phase 8]: Phase 08 P05 Task 5 smoke approved 2026-05-28 ("approved, dig in"). Singleton-form hide-not-dispose pattern emerged as canonical fix for all MEF-registered editor forms (Phases 9-11 must apply from start): on `CloseReason.UserClosing` cancel close + Hide() instead of disposing; editor-host shutdown reasons fall through normally. Smoke-discovered defects b899504 (FormIffEditor AV closure) + ce2a0a4 (FormTreBrowser defensive) landed mid-smoke in UtinniPlugins. Open Q2 (loose-override subdir) + Open Q3 (per-asset reload matrix) deferred for follow-on observation pass; dirty-discard UX gap on TRE-Browser hand-off path deferred as polish.
- [Phase 8]: Phase 08 P07 (.tre repack, D-05.4) COMPLETE 2026-05-28. Auto Tasks 1-3 shipped TreFile.GetRecordCompressedBytes + GetRecordNameBytes APIs (Task 1: 13 [Fact]s, 08-REVIEWS HIGH-1 + round-2 MEDIUM 6), TreWriter full-rebuild repack (Task 2: 3 [Fact]s with nine-invariant TOC + name-byte-identity helper), and TreRepackSaveTarget + Save▾ ▸ Repack wire (Task 3, cross-repo). Task 4 outcome rewritten from "live-SWG smoke" to "smoke=automation-augmented" per maintainer direction ("automate this against test assets, this should be automated if possible"). Continuation Tasks 5/5a-5e: extracted TreBackupPath + TreRepackLock to UtinniCoreDotNet/Saving/ (checker B-1 pattern, mirrors 08-05 LooseOverridePath + 08-06 LivePatchValidator); refactored plugin TreRepackSaveTarget to delegate to framework helpers; shipped 5 new test classes (TreRepackRoundTripTests + TreRepackLogicalPathTests + TreRepackLockedArchiveTests + TreRepackBackupTests + TreRepackByteDiffTests, 15 outcomes covering on-disk repack contract). 319/319 Utinni tests pass (Debug + Release configs); both repos build clean Debug+Release|x86. Open Q1 (cursor N-H1 ACK = SWG client path-CRC resolution under live injection) + Open Q5 (UI end-to-end + tiered reload after scene change) deferred-but-acceptable for V1 per the precedent set by 08-05 Task 5 maintainer smoke approval on automation-augmented verification.
- [Phase 9]: Phase 09 P01 (typed DTII format primitives) COMPLETE 2026-05-29. Ten pure-managed `UtinniCoreDotNet/Formats/Datatable/*.cs` files compose on Phase 8 IFF primitives: DataTableColumnType (type-spec parser + MangleValue port, corrected `e(a=0,b=1,c=2)[default]` enum grammar per Assumption A1), DataTableCellValue (sealed Int/Float/String union), DataTableHashCrc (Crc::normalizeAndCalculate port), DataTableDocument.FromIff (V0000/V0001 typed reader + per-cell CF-04 slice capture + 16M-cell DoS cap), MutableDataTableCell (hybrid DOM + CaptureState/RestoreState undo primitive — item 3), MutableDataTableDocument.BuildMutableIff (FORM DTII tree; comment columns preserved in COLS+TYPE with zero-byte ROWS — item 13), DataTableWriter (composes IffWriter.Write). DEVIATION (Rule 1): CRC empty/null = SOE crcNull = 0, NOT the plan's speculative 0xFFFFFFFF (Crc.cpp:19,73-76). 73 Datatable xUnit facts incl SC4 EditCell→RestoreState-Undo→byte-exact invariant + negative Value-set-back case; full UtinniCoreDotNet.Tests 404/404 green; Debug+Release|x86 clean. PROD-W1-DT requirement remains Pending (needs full end-to-end editor demo across plans 09-01..09-07). CaptureState/RestoreState ready for 09-04 EditCellValue + 09-06 ApplyCsvImport.
- [Phase 8]: Phase 08 P06 (in-memory live patch, D-05.3) COMPLETE 2026-05-28. Framework-side `LivePatchValidator` pure-function bounds gate (BCL-only; no WinForms / UtinniCore.Memory / GameCallbacks / TJT — checker B-1) closes round-2 HIGH-B / CONTEXT D-05.3 "unit-tested" claim with 5 [Fact]s (NoClient / ZeroTarget / Growth / Shrink / SameLengthHappyPath). Plugin-side `LivePatchSaveTarget` (game-thread CON-N-04 mapped-memory write via `GameCallbacks.AddMainLoopCall` + `UtinniCore.Memory.memory.Copy`; pin/unpin in finally on same thread) consumes the validator before AddMainLoopCall. `FormSaveConfirmDialog` (risk-proportional per-call modal with Color.Red emphasis + explicit-verb buttons; reusable by 08-07 repack). `FormIffEditor` Save▾ ▸ Patch live client menu wire: provenance-gated on `Source is OpenSource.ClientMemory cm` AND `Game.IsRunning`; honest-disabled tooltip otherwise (round-2 MEDIUM 11, 3 hits). Round-2 HIGH-A: 4 new csproj entries (1 in Utinni + 3 in UtinniPlugins). Task 5 approved by maintainer on automation alone (option 3 — smoke=automation-only); Open Q4 (full functional live-patch smoke with maintainer-only ClientMemory debug construction) deferred to later observation/doc pass. Singleton-form pattern note: FormSaveConfirmDialog is per-call modal (`using (var dlg = ...)`) — default WinForms dispose-on-close is CORRECT; the 08-05 hide-not-dispose pattern applies only to plugin-registered GetForms() instances.
- [Phase 9]: Phase 09 P03 (FormDatatableEditor host + grid + per-type widgets) autonomous Tasks 1-3 COMPLETE 2026-05-29 (Task 4 live-host checkpoint pending). Cross-repo: UtinniPlugins ships ThemedDataGridView (UI-SPEC token map verbatim, no ARGB literals, non-virtual BindMutable + CellFormatting overlays), DatatableColumnFactory, DatatableHashStringEditor (int-vs-source UX, source not persisted), DatatableNumericUpDownEditingControl, FormDatatableEditor host + Designer (CF-09 add-order) + Plugin.cs registration; Utinni ships SingletonFormClosePolicy framework helper (CI-coverable hide-not-dispose decision, no TJT ref) + 6-fact xUnit guard + the DataGridView bind-latency probe. Grid-binding commit-back seam isolated to FormDatatableEditor.CommitCell so 09-04 swaps the direct cell.Value setter for controller.Apply(EditCellValue). **Bind-latency: cold ~265 ms, warm ~89-122 ms (200x30, production CellFormatting path) — STRADDLES 100 ms; Plan 09-06 SHOULD include a VirtualMode fallback.** DEVIATION (Rule 3): STA probe via created+joined STA thread instead of adding Xunit.StaFact package. 411/411 UtinniCoreDotNet.Tests pass (404 baseline + 6 + 1); both repos build Debug|x86 clean. Commits: Utinni 6a40e05/84a24c6, UtinniPlugins 697a30c/ef0a0c8.
- [Phase ?]: Phase 09 P02: roundtrip-tab CLI verb is the SC4 byte-exact-untouched-cells gate via a per-cell ROWS-slice comparison (re-parse both byte arrays, pair untouched cells by stable (row,col) with explicit remove-row/remove-column index-shift maps). Added public framework primitives RemoveRowAt/RemoveColumnAt/ResolveColumnIndex/TryCoerceCellValue/GetOriginalSliceForCompare (reusable by 09-04/09-06). 16 facts, 8 committed goldens; Utinni.Cli.Tests 139 pass, UtinniCoreDotNet.Tests 404/404.
- [Phase 9]: Phase 09 P05 (entry points + save targets) COMPLETE 2026-05-29. `TJT.Saving.DatatableSaveTargets` < 100-line (87-line body) composition shim forwards verbatim to Phase 8's IffSaveTargets (modes 1/2/3) + TreRepackSaveTarget (mode 4) — zero new save plumbing/path-defense/repack orchestration. FormDatatableEditor: 5 Save▾ click handlers + `controller.MarkSaved()` on each save-success (iter-2 item 8) + `RefreshSaveMenuEnabledState` rewritten to compose the provenance gate ON TOP of 09-04's NeedsReview gate (Save-As escape hatch on Unknown per round-2 MEDIUM 5) + public `OpenFromTreEntry`/`OpenFromMutableIff` + `saveInFlight` MEDIUM-9 barrier + reload-dispatch audit trail keeping CF-05 locked copy. FormTreBrowser D-10.2 (Open-in-Datatable-Editor, extension-only `.tab` visibility) + FormIffEditor D-10.3 (Switch-to-typed-datatable-view, visible iff root TypeId==DTII) — both additive, no public-signature change. DEVIATION (Rule 3): DatatableSaveTargetsTests target the FRAMEWORK composition legs (the WinForms/native TJT assembly is not project-referenceable from the x86 test project — Phase 8 precedent); 10 new facts (7 save + 3 reload-routing). DEVIATION (Rule 1 doc): no `RefusedV6000Encrypted` enum — V6000 refusal = `TreRepackResult.Failed` (TreWriter.Repack throws NotSupportedException). Namespace is `TJT.Saving` (matches SHIPPED Phase 8), not the plan's `TheJawaToolboxDotNet.Saving`. 121/121 Datatable subsuite; 458/458 UtinniCoreDotNet.Tests; TJT MSBuild Debug|x86 green. PROD-W1-DT save + entry-point surface CLOSED. Commits: Utinni 3b02999; UtinniPlugins 149904c/b3dd75d.
- [Phase ?]: Phase 09 P04 (T4 schema-mutation engine) COMPLETE 2026-05-29. DatatableEditController (verbatim IffEditController port + NeedsReviewCount/PendingCascadeContext/MarkSaved seams) + 11 D-01 T4 commands (EditCellValue via CaptureState/RestoreState byte-exact undo; RemoveRow/RemoveColumn insert-by-reference CR-01 port; ChangeColumnType D-04 mangle cascade) + ApplyCsvImport stub for 09-06. Cascade state on controller, ZERO form-local lastCascadeContext. MarkSaved rebaseline = per-cell (RebaselineAfterSave). TJT FormAddColumnDialog + FormTypeChangeCascadeDialog per-call modals; FormDatatableEditor controller wire + R-04 NeedsReview save-block on every Save menu item + D-02 safety-net (reused FormSaveConfirmDialog). DEVIATION (Rule 3): added MutableDataTableRow.InsertCellInternal + MutableDataTableCell.RebaselineAfterSave. 37 controller Facts; 111/111 Datatable subsuite; both repos Debug+Release|x86 clean. Commits Utinni 997716a / UtinniPlugins 0868c88.
- [Phase ?]: Phase 09 P06 (CSV + Find/Replace + sort + VirtualMode) COMPLETE 2026-05-29. Framework CsvCellCoercion.PlanImport (checker B-1; per-cell diff + DoS caps) + DataTableCellValue.ToCsvString + ApplyCsvImportCommand single-transaction reverse-order undo (replaces 09-04 stub). TJT DatatableCsvSerializer (UTF-8 BOM export + <100-line RFC-4180 parser) + FormCsvImportPreviewDialog (locked D-08 copy). FormDatatableEditor: Find/Replace (Ctrl+F/H, F3/Shift+F3, Esc, 200ms debounce, regex 2s matchTimeout, per-column-type-validated Replace) + CSV Import/Export + column-click view-only sort (D-09) + DT_Comment frozen-row toggle. D-09 dual defense: writer grep 0:0:0 + Sort_DoesNotMutateModelOrder STA fact. Task 4 VirtualMode EXECUTED (09-03 measured 265.63ms cold > 100ms): row-threshold(150) fallback, CellValueNeeded/Pushed->controller. 475/475 tests; both repos Debug+Release|x86 green. Commits Utinni a090726/c84c655; UtinniPlugins 3ff92e3/2c60048/f1bb651.
- [Phase ?]: Phase 11 P01 (object-template format core) COMPLETE 2026-05-31. New UtinniCoreDotNet/Formats/ObjectTemplate/ folder: ObjectTemplateParamValue (Bool/Int/Float/String/None/RawBytesHexFallback union + verbatim delta byte + UI-SPEC ParamTypeLabel), ObjectTemplateParamCodec (self-describing tag decode/encode with consume-exactly-or-hex defensive posture T-11-01 — WEIGHTED_LIST/RANGE/DIE_ROLL/short/long route to hex fallback), MutableObjectTemplate (mutable model over Phase 8 MutableIffDocument; EditOverride mutates captured leaf in place hybrid-DOM; Add/RemoveOverride re-derive machine-managed int32 paramCount D-04), ObjectTemplateWriter (composes IffWriter.Write). Added ReadInt8 to IffPayloadCursor. DECISION: int vs float SINGLE numeric is byte-indistinguishable to the generic schema-free decoder (D-03) — canonical decode is typed Int with delta preserved; byte-exactness holds either way; V2 typed schema disambiguates the widget label. 15 new facts (10 codec + 5 model); full suite 608/608 green Debug+Release x86. Zero scene-UndoRedoManager coupling (CON-M-05). Commits: 407d62b, d73a58c.
- [Phase 11]: Phase 11 P02 (OT inheritance resolver + edit controller + roundtrip-ot) COMPLETE 2026-05-31. ObjectTemplateResolver.Resolve replicates the client `if(!isLoaded()) return base->getXxx()` as a nearest-supplier effective-merge with origin markers (LocalOverride/Inherited/UnresolvedBase) + ancestor breadcrumb; NEW depth/cycle guard (visited-set + MaxDepth=64, the single new defensive control T-11-04). ResolveViaArchive wires TrePayloadResolver.TryResolve (false-return = D-01 graceful degradation; never throws on the open path — unresolved base stops the walk + records a Resolved=false breadcrumb segment, locals stay editable). ObjectTemplateEditController clones DatatableEditController's core Apply/Undo/Redo/MarkSaved skeleton (Phase-9 cascade machinery dropped), with ZERO scene undo/redo-manager coupling (CON-M-05, extra-load-bearing); ObjectTemplateEditCommands EditValue/AddOverride/RemoveOverride capture byte-exact prior state for undo. roundtrip-ot CLI verb = param-LEVEL untouched byte-exact gate after a typed mutation (CF-02; the typed-OT analog of roundtrip-tab's per-cell slice), goldens incl. hex-fallback + unresolved-base degradation. 20 new facts (5 resolver + 6 controller + 9 CLI); full UtinniCoreDotNet.Tests 619/619 + Utinni.Cli.Tests 165/165 green Debug+Release|x86. DEVIATION (Rule 1): refreshed dispatch help/no-args goldens for the new verb. Commits: 36861d9, 81bacd1, 244c45a.
- [Phase 11]: Phase 11 P03 (Object Template Editor host + hand-offs) COMPLETE 2026-05-31. Cross-repo. FormObjectTemplateEditor (UtinniForm, IEditorForm) clones FormDatatableEditor: 4-column effective-inheritance grid (Field/Effective value/Origin/Type) with Phase 11 deviations (AllowUserToOrderColumns=false, MultiSelect=false/FullRowSelect, Value-only Fill); binds OWN DataGridViewRows from EffectiveField (ThemedDataGridView.BindMutable is datatable-typed) + form-local CellFormatting for origin overlays (local-override Origin accent / inherited rows grey+italic / unresolved-base red); background DERV-chain resolve (Task.Run -> BeginInvoke) that NEVER blocks the open (D-01 LOCKED — lazily-cached TreArchiveIndex from resolved client root via ResolveViaArchive, null-locator graceful-degradation fallback); ancestor breadcrumb root->this (terminal this accent, unresolved segment red); editor-local ObjectTemplateEditController undo/redo (Ctrl+Z/Y, ZERO scene-manager coupling CON-M-05); Show-inherited toggle (default ON, persisted); singleton hide-not-dispose via SingletonFormClosePolicy. NEW framework OtHandoffPolicy (UtinniCoreDotNet/UI; mirrors DatatableHandoffPolicy): ShouldOfferObjectTemplateEditor (.iff visibility gate) + IsObjectTemplatePayload (click-time LooksLikeObjectTemplate content sniff, never throws). 5th SubPanel registered in Plugin.cs try/catch; GetSubPanels() stays null (SPI NOT widened, CON-M-01/02, T-11-10). IFF Editor 'Switch to typed object-template view' (mutable-root sniff, OpenFromMutableIff no re-parse) + TRE Browser 'Open in Object Template Editor' (off-UI-thread TryResolve -> IsObjectTemplatePayload gate -> OpenFromTreEntry) hand-offs, both HIDDEN when the sniff fails. Save/Promote/Revert/Reload click bodies are status-only stubs (wired 11-04/11-05); grid ReadOnly this plan. UtinniCoreDotNet + TJT clean Debug+Release|x86 (WinForms-host logic verified by MSBuild-green build per Phase 8/9/10 precedent — not project-referenceable from the x86 test project); framework xUnit 619/619 green (OtHandoffPolicy additive). Commits — Utinni: 484211c (OtHandoffPolicy); UtinniPlugins: bc32f12 (host), 0504dfa (registration + hand-offs).
- [Phase ?]: Phase 11 P04: OT editor mutations/widgets/save/reload — per-type widgets (bool/int/float/string) + Consolas hex fallback (FormParamHexEditor) for complex params; origin-branching commit (Inherited edit -> AddOverride promote); Save modes 1/2/4 via ObjectTemplateSaveTargets shim (mode 3 disabled CF-03, V6000 refused); LOCKED CF-05 tier-(b) reload badge (states candor, no refetch hook); ReloadAssetClassifier verify-test. 624/624 framework Debug+Release|x86; TJT clean both configs. Commits UtinniPlugins 758330d/a9b738f, Utinni 78ec981.
- [Phase 14]: 14-01: single-source LooseOverridePath in netstandard2.0 + [TypeForwardedTo] binary identity (re-export forbidden); net10 Utinni.Mcp host on ModelContextProtocol 1.4.0 with fail-closed ResolvedRoot pin + injectable-timeout CliDispatcher subprocess seam; net10 CI lane added off the x86 pass
- [Phase 14]: 14-03a: apply-save-tab/ot/iff/stf CLI verbs FUSE (roundtrip-* apply+verify) with (save WriteAtomic-to-loose-override), persisting the SAME mutatedBytes they verified (no re-load between verify+commit) — closes the reviewer-confirmed gap where save re-serialized the UNCHANGED file and roundtrip-* discarded the mutated bytes. Fail-closed: a failed untouched-region verify -> exit 2 + write NOTHING. MCP host (14-03) wraps each 1:1 and decides persist-vs-fail on the EXIT CODE alone. SCOPED documented exception to the Phase-14-adds-ZERO-verbs guard-rail (cross-AI review BLOCKING finding). Shared SaveCommandIo.WriteAtomic (Flush(true)); internal-static TestPerturbSerialized seam drives failed-verify-no-write through the real CLI. 37 apply-save+dispatch golden tests; full Utinni.Cli.Tests 239 pass / 2 skip; Debug+Release|x86 green. Commits 7eed25f, bb3e5bc.
- [Phase ?]: 14-02: CliResultMapper is the single chokepoint interpreting utinni-cli stdout — verbatim envelope pass-through (text + StructuredContent JsonElement) with SHAPE validation (schemaVersion + command + result XOR error) + exit-code taxonomy (hard McpException on exe-missing/timeout/non-JSON/empty/malformed/out-of-range/exit-0-with-error/non-zero-with-result). 5 thin read tools resolve->dispatch->map; get_template_schema writes a host-temp --out OUTSIDE resolvedRoot (IDisposable, cleaned up). MCP-01 read surface in place.
- [Phase ?]: 14-03: save_* MCP write tools each wrap ONE apply-save-* verb opaquely (one Process.Start), deciding persist-vs-fail on the EXIT CODE alone (0 persisted / 2 verify-failed) — never parsing bytesEqualUntouched. repack_tre is a distinct off-by-default Destructive tool with a HOST-SIDE dry_run=true plan-only gate (no spawn, no backup claim; repack-tre verb has no --dry-run flag), unreachable from save_*. roundtrip_check is verify-only non-persisting. SaveVerb owns format->verb map + typed-arg->argv builders + per-resolved-path SemaphoreSlim serialization (T-14-16). Path-escape blocked at the MCP boundary with ZERO CLI spawns. CliDispatcher unsealed + RunAsync virtual as a test-only stub seam (prod unchanged). MCP-02 write half complete.
- [Phase ?]: 14-04: real McpClient round-trips the built Utinni.Mcp.exe end-to-end (--cli-path pinned, no transcript fallback) — exact 11-tool surface + multi-format read + edit-save-READ-BACK persistence + isolated-copy repack + boundary escape; committed binary Fixtures (generate-once-via-32bit-PS); MCP-SECURITY.md 17-threat register (each mitigate row cites file:line + a proving test, 5-layer advisory-vs-enforcement model + apply-save ZERO-verbs exception). Both lanes green. MCP-01+02 integration-proven.
- [Phase ?]: 15-01: WorldSnapshot bulk ops compose shipped WorldSnapshotCommands as N ordered descriptors (retemplate=remove+add per node); duplicate ids not deduped; zero new format code (D-02). Companion FormSnapshotPlacements table launched from SnapshotPanel button.
- [Phase ?]: 15-02: .prt/FORM PEFT typed codec (PROD-W2-PRT format half) in UtinniCoreDotNet/Formats/Particle. WaveForm(3 ver)/ColorRamp(2 ver) leaves + PEFT->EMGP->EMTR tree composing shipped IffReader/IffWriter/IffPayloadCursor; byte-exactness via consume-exactly-or-hex (typed decode accepted only when it re-encodes identical, else raw-preserve); degrade-don't-abort D-05 raw-preserves any unrecognized FORM-version sub-tree and NEVER hard-aborts (Pitfall 2); division-form count DoS guard T-15-02. 21 facts, 663/663 suite green Debug+Release|x86. PROD-W2-PRT editor+live-preview half remains a later plan.
- [Phase ?]: 15-03: D-09 live-preview spike — reject ParticleManager (debug/config singleton, no live instances); choose ClientEffectManager (m_particleSystems) via AppearanceTemplateList reload + ParticleEffectAppearance::restart(). HONEST FINDING: no reachable native hot-retrigger hook this phase — ship documented no-op stub ParticlePreview seam returning NotReachable; editor degrades to tier-(b) badge; real hook is a scoped follow-on (15-08+) behind the same seam.
- [Phase ?]: 15-04: .prt read-assist headless — decode-iff auto-dispatches FORM PEFT; new roundtrip-particle byte-exact gate (degraded fixtures round-trip identical, D-05); summarize_particle MCP read tool = thin decode-iff dispatch (ReadOnly D-07, ZERO format logic D-06), the same .prt read path the in-app Explain effect button reuses (D-08).
- [Phase 15]: 15-06 (PROD-W2-PRT editor half) COMPLETE 2026-06-07. New FormParticleEditor : UtinniForm cloning the Phase-11 OT shell: emitter tree (IffChunkTree.LoadMutable over effect.SourceIff) + typed param grid (ThemedDataGridView, Field/Value/Type) with the D-05 honest greyed Consolas-9pt hex fallback + LOCKED "preserved as original bytes" tooltip, inherited Save▾ provenance gating (ParticleSaveTargets shim → Phase-8 IffSaveTargets/TreRepackSaveTarget), editor-local leaf-edit undo/redo (Before/After leaf-payload stack, CON-M-05, NOT a ParticleEditController — codec has no command layer), raw-leaf hex edit via FormParamHexEditor→EditLeafPayload, singleton hide-not-dispose (D-03), CF-09 Fill-first add order. Explain effect = read-assist ONLY (D-07): ParticleReadAssist shells utinni-cli decode-iff (same verb summarize_particle dispatches, D-08) with 15s timeout backstop, NO codec in the AI handler, no write/prompt-to-mutate, degrades to honest error if CLI absent (D-06). Preview in client (D-09) state-encoded on Game.IsRunning + IsRetriggerHookReachable() (=false this phase per 15-03 → LOCKED tier-(b) degraded reload badge "Reloads on next scene change or relog."; live-capable "Re-triggers live instances on Preview." wired for 15-08 via the single seam method). DEC-A3/D-11 boundary sentence surfaced verbatim as dimmed footer. NEW framework ParticleHandoffPolicy (UtinniCoreDotNet/UI; mirrors OtHandoffPolicy): ShouldOfferParticleEditor (.prt gate) + IsParticlePayload (cheap FORM PEFT byte sniff, never throws) — 18 facts. Registered in Plugin.cs GetForms() try/catch; GetSubPanels() stays null (CON-M-01/02). TRE Browser "Open in Particle Editor" hand-off gated on the policy (HIDDEN when sniff fails). Param grid read-only this phase (typed scalar inline edit + WaveForm/ColorRamp curve editor deferred per UI-SPEC Assumption 4 — typed surface VISIBLE + hex fallback editable). 18/18 ParticleHandoff facts green; TJT solution Debug|x86 green (VS2026 MSBuild exit 0). Commits — Utinni: 26af8e6 (ParticleHandoffPolicy); UtinniPlugins: 589f206 (editor + hand-off + registration).
- [Phase 15]: 15-05 (RESID-04 automatable half) COMPLETE 2026-06-07. Suppress SWG's exclusive-fullscreen mode switch at the DirectInput cooperative-level layer — `hkSetCooperativeLevel` rewrites `DISCL_EXCLUSIVE` -> `DISCL_NONEXCLUSIVE` (FOREGROUND/BACKGROUND/NOWINKEY preserved) behind a default-ON `std::atomic<bool>` toggle (D-12), keeping the owned-popup embed windowed instead of detaching to true D3D9 exclusive fullscreen (RESEARCH A4 / `chat-open-d3d9-fullscreen.md` prime suspect). Existing DISCL flag + caller-PC logging retained as the A4 live-confirmation instrument; the redirect logs the old->new flag rewrite. Toggle exposed via new exported `DirectInput::setSuppressExclusiveFullscreen`/`getSuppressExclusiveFullscreen` so 15-08 can A/B live without a rebuild and the deferred detached-fullscreen fallback (Open Q3) stays reachable. PanelGame.cs resize path documented: window-side `SetWindowPos`-only, NO Utinni-initiated device `Reset` (D-13), owned-popup unchanged (no `WS_CHILD`), imgui RT-space mouse/DisplaySize mapping holds across resize via windowed COPY Present self-stretch. D-13 enforced by a new Catch2 `[resid04]` `NoDeviceResetTests.cpp` — a comment-stripped source grep-gate over directx9.cpp + direct_input.cpp + PanelGame.cs counting `->Reset(`/`.Reset(` invocations == 0 (hkReset's free-function `reset(pDevice,...)` SWG-own pass-through naturally excluded), with an anti-trivial self-check section (grep-gate hygiene). UtinniCore + UtinniCore.Tests Release|x86 green; 8 assertions pass. Generated/UtinniCore.cs regen churn reverted (never committed). Live edge-case-matrix confirmation folded into 15-08. Commits bf5843d, 6ae1dd7.
- [Phase ?]: 15-07 (RESID-03 automatable half) COMPLETE: extended ReloadAssetClassifier with named WorldSnapshotExtensions{.ws}+ParticleExtensions{.prt} sets routing both to tier-(b) PendingNextSceneChange (D-14), backing the LOCKED WS 'Placements re-resolve on the next scene change.' + Particle DEGRADED 'Reloads on next scene change or relog.' badge copy; conservative unknown fallback preserved exactly. Particle LIVE-capable badge is a form-side runtime affordance (15-03 hook+Game.IsRunning), NOT a classifier tier. ParticleSnapshotReloadRoutingTests 9 facts incl honesty guard; dotnet test --filter ReloadRouting 15/15. Live SC3 render-on-reload is 15-08 smoke. Commit c63122a.

### Pending Todos

- **RESID-04 — SWG window resize / windowed↔fullscreen edge cases** (`.planning/todos/pending/swg-window-resize-fullscreen-edge-cases.md`, `resolves_phase:15`). Live-confirmed 2026-06-03 during the 12-04 re-run: on login→fullscreen the embedded SWG window detaches + overlays the WinForms editor, cursor/movement input dies (non-recoverable in-session), and minimize takes both windows down. **New trigger data point:** fires on the login→load-into-world path, not just chat-open Enter. **Workaround:** drop SWG resolution off fullscreen. Maintainer triage: low priority, likely not a hard find.

### Blockers/Concerns

**Open concerns from live UAT 2026-05-18/19 (not phase-gated; track separately):**

1. **~~WR-03 exit dialog~~ RESOLVED implicitly by Phase B window ownership (commits `2ce028c` + `1789400`).** Plan 02.1-02 fixed the `delete depthTexture` UAF in `directX::cleanup()` (verified empty body at `directx9.cpp:410-427`). The remaining "Direct3D could not be correctly initialized" dialog persisted through 2026-05-19/20 and was confirmed to "disappear in passthrough-everything builds; reappear with any detour active." **Verified clean exit 2026-05-21 morning** (utinni.log 09:11:57): full session — login + Naboo scene load + scene transitions + `/quit` + close — produced a clean `hkCleanupScene -> cleanUpSceneCallbacks complete; EXIT` chain followed by orderly process exit. **No dialog, no SWGEmu.exe-stage.*.{txt,mdmp} dump.** Theory: Phase B's `GWLP_HWNDPARENT` ownership changed the shutdown sequence. Pre-Phase-B, SWG was a standalone top-level window — closing FormMain left SWG's lifecycle awkward and D3D9 teardown ran at the wrong time, tripping over still-active detours. Post-Phase-B with ownership, closing FormMain cleanly tears down the whole owned-window group: SWG receives WM_CLOSE/WM_DESTROY through normal channels and runs its own shutdown the way it expects, with detours seeing operations in the right order. Consistent with the "passthrough-everything builds don't fire it" observation — the detours weren't the bug per se, they were just interacting badly with an out-of-order self-shutdown. **Earlier 2026-05-18 "no exit dialog" report was a false negative** (delayed dialog mistaken for startup). Today's report is from a different post-Phase-B-bis build with full window-group ownership, so the mechanism is different. Re-open if it surfaces in a future run.

2. **~~D3D9 vtable pattern doesn't match modern d3d9.dll~~ RESOLVED 2026-05-19 (commit 2c57d38)** — Replaced the broken `d3d9.dll` byte-pattern scan in `directx9.cpp::getVtbl()` with the conventional dummy-device approach (`Direct3DCreate9` + hidden 1x1 window + `CreateDevice(HAL)` + read vtable pointer + snapshot 119 entries + release). Proved via probe of buildable SWG Source client that modern `d3d9.dll` (Win11 24H2 6.2.26100.8328) allocates IDirect3DDevice9 vtables per-instance on the heap — no static `.rdata` table exists for pattern scanning. Probe data archived in `.planning/SESSION-HANDOFF-2026-05-19.md`. After this commit, injection log shows no DirectX9 critical errors; D3D9 detours install cleanly.

3. **Editor-mode HWND-override hooks were wedging SWG init — RESOLVED 2026-05-19 (commits 18c5e22 + 74f64fc)** — Bisection (13 rounds) traced post-d3d9-fix audio-init stall to two editor-mode code paths that override SWG's HWND with the editor's:
   - `hkSetupStartInstall` set `pStartupData->createOwnWindow=false` + `windowHandle=Client::getHwnd()` → SWG silently hung after audio init.
   - `hkSetupInstall` (DirectInput) replaced SWG's HWND with editor's top-level HWND → `SetCooperativeLevel` returned `DIERR_INVALIDPARAM` because the editor HWND is on the CLR thread, not SWG's main thread.
   Both hooks now pass through. New integration model: SWG creates its own window normally; managed side will reparent that HWND into the editor's PanelGame via `SetParent` + `WS_CHILD` style change. Managed-side reparenting still not implemented — see open item #10.

4. **~~Managed-side CLR exception 0xE0434352 during character template load~~ LIKELY OBSOLETE 2026-05-19 night** — Was hypothesized as a downstream consequence of #6 (the jmp-self halt). With #6 now RESOLVED, the consequential CLR exception has not reproduced in any of tonight's successful boot runs (SWG progressed past character template load into the login screen and intro). Marked obsolete pending re-observation. If it re-surfaces independently, original investigation plan still valid: VS 2026 → Debug → Exception Settings → check "Common Language Runtime Exceptions" → run Launcher.exe under VS to capture the throwing managed line.

5. **~~SWG window invisible during runtime~~ RESOLVED IMPLICITLY 2026-05-19 night** — Symptom was a direct consequence of #6 (main thread halted in `EB FE` so `clientMain` never returned to enter the render loop). With #6 fixed, the SWG window now appears normally during the boot sequence and shows the pre-login flow + login screen. **Window reparenting into the editor's PanelGame is a separate concern** — see open item #10.

6. **~~SWG main thread halts in jmp-self at `0x0131DC7A`~~ RESOLVED 2026-05-19 night (commits `dad9845..20fbad5`)** — Three-session investigation. Root cause was **Utinni's own Launcher** writing `EB FE` to SWGEmu's PE entry as a stall mechanism while UtinniCore.dll was injected (`Launcher/main.cpp:351-352`). The matching restore code at lines 382-384 sat behind `inject(procInfo)` → `WaitForSingleObject(hInitThread, INFINITE)` → blocked forever because `utinni_init` blocks in `clr::load()` → `Application.Run(FormMain)` for the editor's lifetime. The function at `0x0131DC7A` is MSVC `__tmainCRTStartup` (CRT entry), NOT SWG `Os::install` as initially hypothesized — corrected via CODEX peer review. Variance between sessions ("sometimes halts immediately, sometimes runs to preloading then halts later") was CPU I-cache nondeterminism (`WriteProcessMemory` doesn't flush instruction cache) — CODEX's catch. **Fix architecture:** named-event signal-based sync. Launcher creates `Local\UtinniReady_<pid>` event, passes name to `utinni_init` via `lpThreadParam`. Managed `Startup.EntryPoint` calls `Native.SignalLauncherReady()` after all four `*Callbacks.Initialize()` calls and immediately before `Application.Run`. Launcher waits on the event (30s timeout) instead of the thread handle, then restores PE entry + `FlushInstructionCache` + resumes main thread. Full mechanics in `.planning/SESSION-HANDOFF-2026-05-19-NIGHT.md` and in Claude's auto-memory `project_eb_fe_patch_origin.md`.

**NEW issues from 2026-05-19 night session (downstream of the resolved boot pipeline; all non-blocking):**

7. **~~Lok scene-load second-cycle access violation at `0x00b3f620`~~ RESOLVED 2026-05-20 by Phase B fix (commit `2f02fad`)** — see prior detail (HWND/size oscillation hypothesis confirmed dead in code). Side observation: initial Tatooine load fires `hkSetScene` twice with the same scene pointer at login (SWG-internal intro→world transition, not a Utinni regression); TJT/plugin authors should know setScene callbacks may double-fire on initial login.

8. **~~Naboo scene-load: SWG memory pool exhausts at ~300 MB~~ RESOLVED 2026-05-20 by Phase B fix (commit `2f02fad`)** — apparent OOM was pool fragmentation from SWG's internal re-init cycling (Issue #7), not true address-space exhaustion. Re-open if Naboo crashes on longer playthrough; LARGEADDRESSAWARE investigation kept as fallback.

9. **~~Cursor doesn't display + special keys (delete/tab/return) don't work in game~~ RESOLVED 2026-05-20 (commits `f5fa073..2f02fad`)** — old-model editor meddling (PanelGame focus/mouse handlers + `Client.SetHwnd` shadowing + editor-mode cursor side-effects) stripped; SWG owns its own window. Diag logs retained as regression detectors.

10. **~~SWG window reparenting~~ FULLY RESOLVED 2026-05-21** — owned-popup reparenting in `PanelGame.cs` (strip caption/frame, keep WS_POPUP, FormMain owner via GWLP_HWNDPARENT, HWND_TOP + SWP_NOACTIVATE for Z-order). **Do NOT call `pDevice->Reset` on SWG's device** (owns untracked default-pool resources → D3DERR_INVALIDCALL → DEVICELOST → fatal crash; live-verified). D3D9 windowed COPY Present handles backbuffer-vs-window mismatch by stretching; just resize the window. **Directly relevant to v2.0 RESID-04 (window-resize/fullscreen edge cases, Phase 15).**

11. **~~In-game Return dead~~ RESOLVED 2026-05-20 by Phase H (commit `6047416`)** — detour `swg::cuiChatWindow::chatEnterHandler` at `0x00F3E420`; override broken submit-empty-and-close with `enableTextInput(true)` when chat is in display mode. **Root cause deferred:** under editor injection SWG's input-map context selector routes in-game Enter to the chat-input-mode binding instead of the game-mode openChat binding (see auto-memory `project_swg_context_routing`).

12. **~~In-game Esc doesn't open system menu~~ NOT-A-BUG / WORKING AS DESIGNED (2026-05-20)** — Action ID `0x12` is `untarget`, not `gameMenuActivate`; SWG binds Esc to clear-target (invisible no-op when no target). See auto-memory `project_swg_keymap_reality`.

Eleven open questions (CON-O-01..CON-O-11) are tracked as phase-gated unresolved constraints — see ROADMAP.md "Open-Question → Phase Mapping" section.

## Deferred Items

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| *(none)* | | | |

## Resolved Deferred Items

| Category | Item | Resolved | Notes |
|----------|------|----------|-------|
| Tier-4 manual UAT | Plan 02-03 Task 3 — C-01 live SWG injection | 2026-05-18 | PASSED. Editor host + TJT plugin came up alongside live SWGEmu client; no loader-lock hang. See `02-HUMAN-UAT.md` §1. En-route fixes (commits `cb547bb`, `92758ff`, `9a108d1`, `3254059`, `389fc83`) landed during the UAT run. |
| Tier-4 manual UAT | Plan 02-04 Task 2 — C-09 live SWG minimize/restore | 2026-05-18 | PASSED. Editor stays responsive across rapid minimize/restore cycles; no UI-thread CPU spike. See `02-HUMAN-UAT.md` §2. |

## Session Continuity

Last session: 2026-06-07T16:13:36.267Z
Stopped at: Completed 15-05-PLAN.md (RESID-04 fullscreen suppress + no-Reset gate)
Resume file: None

## Ingest Provenance

Bootstrapped 2026-05-16 via `/gsd:ingest-docs` from `docs/ai/vision.md`, `docs/ai/assessment.md`, and `docs/ai/test-harness-plan.md`. Zero blockers, zero warnings, four INFO items auto-resolved (all three sources are DOC-precedence; reciprocal vision↔assessment cross-reference is benign narrative linkage). Codebase intel at `.planning/codebase/` (from prior `/gsd:map-codebase`) treated as read-only reference. Synthesis artefacts at `.planning/intel/` (SYNTHESIS.md, decisions.md, requirements.md, constraints.md, context.md) and conflict report at `.planning/INGEST-CONFLICTS.md`. v2.0 milestone research at `.planning/research/` (SUMMARY.md, STACK.md, FEATURES.md, ARCHITECTURE.md, PITFALLS.md), completed 2026-06-01.
