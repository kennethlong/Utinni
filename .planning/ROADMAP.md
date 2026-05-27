# Roadmap: Utinni

## Overview

V1 ships in two halves, sequenced "foundations before features" per vision.md and assessment.md. **Half 1 (Phases 1–6)** stabilises the framework: a CI + Tier-1 C# scaffold lands first as the smallest unlock, then the 15 critical bugs burn down, then the 8 strategic reworks polish plugin-authoring ergonomics, then the Tier 2 CLI shim and Tier 1 C++ tests close the test-harness gap, then a cleanup + open-questions sweep produces the 1.0 framework cut. **Half 2 (Phases 7–11)** delivers the Wave-1 plugin set on top of that stabilised framework: TRE Browser, then IFF Editor (foundational, most-leveraged), then Datatable, then String-table, then Object Template. V1 ships when (a) Tier 1 + Tier 2 CI is green with all 15 critical bugs closed and (b) all five Wave-1 plugins demo end-to-end against a live SWG client — the user-supplied "Demo + CI green" metric.

**Deferred to V2 (called out here so the boundary is explicit):** Tier 3 mock-D3D9 + recorded-fixtures harness; Wave-2 plugins (Conversation, Quest, Buildout, Particle, UI Page, Shader); Wave-3 plugins (Mod Manager, Packager, Community Hub, Asset Diff); broader live-preview reload paths beyond Wave-1 needs; mod packaging and community-hub publish/consume; Wave-4 maybe-someday plugins. Tier 4 manual residual is documented in V1 (TEST-04) but not automated.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3, …, 11): Planned V1 milestone work
- Decimal phases (e.g. 2.1) reserved for urgent insertions if a critical bug surfaces mid-V1

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: CI + Tier 1 C# scaffold** — Land a green Windows-runner CI on `main` and stand up xUnit so every subsequent phase can be gated on test results.
- [ ] **Phase 2: Critical bug burn-down (C-01..C-15)** — Close the 15 enumerated critical bugs from assessment.md; eliminate silent failures, crashes, data loss.
- [ ] **Phase 3: Strategic reworks (R-A..R-H)** — Land the 8 ergonomics reworks so plugin authoring is "genuinely pleasant" and seams exist for downstream native testing.
- [ ] **Phase 4: Tier 2 CLI shim + golden fixtures** — Build `utinni-cli` against the same core libs as WinForms; convert ~60–70% of manual verify loops to unattended CI runs.
- [ ] **Phase 5: Tier 1 C++ unit tests** — Wire Catch2 through CI against the refactored UtinniCore seams produced in Phase 3.
- [ ] **Phase 6: Cleanups, dep bumps, open questions, Tier 4 doc, 1.0 cut** — Final framework sweep: ~30 cleanups, modernise deps, answer or defer the 8 open questions, document Tier 4 residual, tag 1.0.
- [ ] **Phase 7: TJT subpanel — TRE Browser (read-only)** — First Wave-1 editor; lowest-risk read-only surface; proves the subpanel pattern within TJT and exercises the framework plugin pipeline end-to-end. Per DEC-C4, ships as an `IEditorPlugin` subpanel inside The Jawa Toolbox, not as a standalone plugin.
- [ ] **Phase 8: TJT subpanel — IFF Editor (read + write)** — Foundational read/write subpanel; ships IFF chunk read/write primitives in `TheJawaToolboxDotNet`/`TheJawaToolbox` so Phases 9-11 layer on the same shared code. Per DEC-C4, ships as a TJT subpanel.
- [ ] **Phase 9: TJT subpanel — Datatable Editor (`.tab`)** — Tabular data editor on top of Phase 8's IFF foundation. Per DEC-C4, ships as a TJT subpanel.
- [ ] **Phase 10: TJT subpanel — String-table Editor (`.stf`)** — Localised-text editor on top of Phase 8's IFF foundation. Per DEC-C4, ships as a TJT subpanel.
- [ ] **Phase 11: TJT subpanel — Object Template Editor** — Object-template editor; the final V1 subpanel; "Demo + CI green" milestone closure. Per DEC-C4, ships as a TJT subpanel.

## Phase Details

### Phase 1: CI + Tier 1 C# scaffold
**Goal**: A green GitHub Actions Windows-runner CI workflow runs `msbuild Release/x86` and `dotnet test` on every push to `main`, with a smoke-test xUnit project in the solution. This is the smallest possible durability unlock; every subsequent phase gates on this pipeline being green.
**Depends on**: Nothing (first phase)
**Requirements**: TEST-01
**Open questions to resolve**: CON-O-10 (test project layout — single `Utinni.Tests.sln` vs per-project `*.Tests` folders) must be answered before scaffolding.
**Preservation guard-rails**: Phase touches CON-T-01 (`UtinniCore.vcxproj` post-build chain — CI must invoke it correctly). No refactor of the chain itself; just ensure CI honours it.
**Success Criteria** (what must be TRUE):
  1. A push to `main` triggers a GitHub Actions Windows-runner workflow that builds Release/x86 and runs `dotnet test`, with a visible green badge.
  2. An xUnit (or NUnit) test project exists in the Utinni solution and contains at least one smoke test that exercises a real Utinni code path.
  3. A test failure on `main` blocks the workflow and is visible on the commit.
  4. `.editorconfig` exists at repo root and is applied by the build.
**Plans**: 2 plans
**Plans**:
- [x] 01-01-PLAN.md — Test project scaffold: .editorconfig, UtinniCoreDotNet.Tests SDK-style xUnit csproj + HotkeyTests.cs (3 pass + 1 C-08-Skip), Utinni.sln update, packages.lock.json
- [x] 01-02-PLAN.md — CI workflow: .github/workflows/ci.yml on windows-2022 (project-targeted dotnet test), README badge for master, test-the-tester human-verify checkpoint

### Phase 2: Critical bug burn-down (C-01..C-15)
**Goal**: All 15 critical bugs enumerated in assessment.md are closed. Framework no longer exhibits the listed silent failures, crashes, or data losses; class-of-bug constraints CON-H-01..-05, CON-L-01..-04, CON-B-04, CON-D-01 are honoured going forward.
**Depends on**: Phase 1 (need CI to gate fixes; need xUnit hook for any fix that grows a unit test).
**Requirements**: STAB-01
**Open questions to resolve**: CON-O-01 (`isSafeToUse` operator — `||` vs `&&`) must be answered to ship one of the C-class fixes correctly; CON-O-02 (was `AddPostDrawLoopCall` ever actually used?) bears on C-04 fix shape; CON-O-03 (delegate-corruption smell ↔ likely missing `GCHandle.Alloc`) bears on C-NN fix in `GameCallbacks.cs`; CON-O-04 (VS 2019 pin rationale) gates the C-12 widening to VS 2022.
**Preservation guard-rails**: C-07 fix must preserve CON-M-05 (`UndoRedoManager.OnCleanupCallback` clearing-on-scene-cleanup design); the bug is in thread-safety + dead `AllowMerge`, not in the cleanup callback itself. C-11 fix must preserve CON-N-04 (`utility/memory::copy` `VirtualProtect` bracket). Any fix touching detours must preserve CON-N-01 (detour-table pattern).
**Success Criteria** (what must be TRUE):
  1. Every C-01 through C-15 item is closed in code and verified — no observable regression of the listed symptoms (loader-lock crash, cross-CRT delete crash, uninitialised reads, swallowed plugin exceptions, busy-wait UI freeze, null derefs, etc.).
  2. CI (from Phase 1) stays green across the burn-down; each fix lands behind a green build.
  3. `data/utinni.cfg` ships with blank server host/port per CON-D-01 (C-14 fix).
  4. DllMain no longer does heavy startup per CON-H-01 (C-01 fix); CLR bring-up is deferred to a safe initialization point.
**Plans**: 4 plans
**Plans**:
- [x] 02-01-PLAN.md — Trivial criticals (Wave 1): C-04 queue drain, C-06 plugin loader isolation, C-08 Hotkey TryParse, C-12 VSIX VS 2022 widening, C-13 TJT Debug path (cross-repo), C-14 utinni.cfg blank login
- [x] 02-02-PLAN.md — Single-file criticals (Wave 2): C-02 cross-CRT delete, C-03 Network::cast uninit, C-05 drag-drop static field, C-07 UndoRedoManager + Phase-1 D-06 testability seam, C-10 clr::stop null deref, C-11 DirectX findPattern null check, C-15 CppSharp slnDir, C-16 GameCallbacks delegate-pinning audit, KB-05 isSafeToUse && operator
- [x] 02-03-PLAN.md — Architectural C-01 (Wave 3): DllMain loader-lock — utinni_init export + launcher CreateRemoteThread (path a per RESEARCH); Utinni.LoaderLockHarness sibling project
- [x] 02-04-PLAN.md — Architectural C-09 (Wave 3): UI/game-thread busy-wait deadlock — Win32 CreateEvent + EventWaitHandle signaller

### Phase 02.1: Phase 02 gap closure — critical correctness + harness quality from 02-REVIEW.md

**Goal:** Close 4 correctness gaps (CR-02/CR-03/CR-04/WR-03) and 4 harness-quality regressions (WR-01/WR-02/WR-05/WR-09) that the Phase 02 code review surfaced after executor agents reported plans complete. Every finding closed with a regression test that would fail if the fix were reverted.
**Requirements:** [STAB-01]
**Depends on:** Phase 2
**Plans:** 3/3 plans complete

Plans:
- [x] 02.1-01-PLAN.md — Native correctness fixes (CR-02/CR-03/WR-09)
- [x] 02.1-02-PLAN.md — Lazy-init race hardening (CR-04/WR-03)
- [x] 02.1-03-PLAN.md — Harness quality uplift + phase bookkeeping (WR-01/WR-02/WR-05 + utinni_test_resolveExports)

### Phase 3: Strategic reworks (R-A..R-H)
**Goal**: All 8 strategic reworks from assessment.md land. Plugin authoring is "genuinely pleasant"; native code grows the testable seams that Phase 5 (C++ unit tests) and Phase 4 (CLI shim) depend on.
**Depends on**: Phase 2 (criticals must close first so reworks aren't reworking around live bugs).
**Requirements**: STAB-02
**Open questions to resolve**: CON-O-05 (`StdEdited.cs` curation criteria) bears on R-F (CppSharp header auto-discovery); CON-O-07 (Sytner's plugin status) may bear on R-B (plugin lifecycle contract).
**Preservation guard-rails**: R-A symmetric Add/Remove must preserve every callback's existing Add semantics (CON-H-05 forward, not retroactive). R-B plugin lifecycle must preserve CON-M-01/02 (`IPlugin`/`IEditorPlugin` MEF SPI shape) and CON-N-08 (`PluginManager` pImpl idiom). R-C single-source RVAs must preserve CON-N-01 (detour-table pattern) while exposing the RVAs via `UTINNI_API` per CON-H-03. R-G `Directory.Build.props` wizard must preserve CON-T-04 (`Props.cs` factoring) and CON-T-03 (two-language template parity).
**Success Criteria** (what must be TRUE):
  1. Every callback exposed by the framework has symmetric Add/Remove and is safe to unsubscribe from (R-A).
  2. Plugins follow a symmetric `createPlugin`/`destroyPlugin` ABI; `plugin->init()` is actually called; failed `LoadLibrary` is logged (R-B).
  3. Hard-coded RVAs that were duplicated between native and managed are exposed once via `UTINNI_API` and consumed from a single source (R-C).
  4. Callback subscriber lists are snapshot under lock before iteration (R-H).
  5. CppSharp generation auto-discovers headers (R-F); `Directory.Build.props` wizard is idempotent (R-G); logging adopts `[CallerMemberName]` (R-E).
**Plans**: 3 plans
**Plans**:
- [x] 03-01-PLAN.md — Callbacks: R-A handle-based Subscribe/Unsubscribe + R-H snapshot iteration (managed + native) + IN-05 Drain helper consolidation
- [x] 03-02-PLAN.md — Plugin lifecycle + RVAs: R-B symmetric createPlugin/destroyPlugin ABI + two-phase init + HMODULE tracking + R-C single-source WndProc RVA via UTINNI_API
- [x] 03-03-PLAN.md — Build-tooling + logging: R-E [CallerMemberName] logging + R-F CppSharp header auto-discovery + R-G idempotent Directory.Build.props merger

### Phase 4: Tier 2 CLI shim + golden fixtures
**Goal**: A `utinni-cli` executable in the same solution references the same core libraries as the WinForms tool and exposes the operations the UI calls. Golden-file tests against checked-in fixtures convert an estimated 60–70% of manual "Kenny please verify" loops into unattended CI runs.
**Depends on**: Phase 3 (R-B plugin lifecycle and R-C single-source RVAs produced the structural seams CLI relies on; without R-B, CLI would re-implement plugin loading).
**Requirements**: TEST-03
**Open questions to resolve**: CON-O-09 (fixture storage — in-repo vs Git LFS for binary TRE samples) and CON-O-11 (CLI distribution — public artifact vs test-harness-internal) must be answered as part of this phase.
**Preservation guard-rails**: CLI must compose with CON-N-02 (`utinni::` thin-wrapper firewall) and CON-M-01 (`IPlugin` SPI). UI continues to be a consumer of the same core, not a sibling implementation — verifies CON-N-08 / CON-M-01 still hold under two consumers.
**Success Criteria** (what must be TRUE):
  1. `utinni-cli` builds in CI and ships at least four commands: `parse-tre`, `list-objects`, `validate-plugin`, `inspect-iff`.
  2. At least one golden-file regression test per command exists in `tests/fixtures/` and runs in CI.
  3. The WinForms UI continues to function — UI is one of two consumers of the same core, not the sole consumer.
  4. CI runs both `dotnet test` (TEST-01) and the CLI golden suite on every push.
**Plans**: TBD
**Plans (placeholder)**:
- [x] 04-01: TBD

### Phase 5: Tier 1 C++ unit tests
**Goal**: Catch2 wired into CI (MSBuild + direct exe invocation per 05-CONTEXT.md D-03; ctest+vcpkg deferred to Phase 6 STAB-03) for UtinniCore. At least one native parser or helper has non-trivial coverage. CI runs the native test target on every push.
**Depends on**: Phase 3 (refactored seams from R-A..R-H make native testing tractable) and Phase 4 (CI pipeline already runs multi-language tests, just add the C++ target).
**Requirements**: TEST-02
**Open questions to resolve**: None new — CON-O-09/-10/-11 already resolved in Phase 4.
**Preservation guard-rails**: Catch2 must not poison CON-T-02 (`RelWithDbgInfo` configuration plumbed end-to-end) — pick a Catch2 wiring strategy that respects the three-config layout. No refactor of CON-N-01..-09.
**Success Criteria** (what must be TRUE):
  1. A Catch2 test executable builds in CI under all three native configs (Debug + Release + RelWithDbgInfo) and runs via direct exe invocation (`bin\Release\UtinniCore.Tests.exe`) per 05-CONTEXT.md D-03 — Catch2 as its own self-runner with stacked `--reporter console + junit::out=...`, no `ctest` wrapper.
  2. At least one UtinniCore parser or helper has Catch2 coverage (target candidates: TRE/IFF parsers in native, math helpers, `utility/memory::copy` round-trip).
  3. CI gates `master` on `dotnet test` (Phase 1) + CLI golden tests (Phase 4) + native unit tests via direct exe invocation (this phase, per D-03) — all green.
**Plans**: 2 plans
**Plans**:
- [x] 05-01-PLAN.md - Scaffold + smoke: vendor Catch2 v3.15.0 at external/catch2/, create UtinniCore.Tests.vcxproj (triple-config Application), register in Utinni.sln with full Debug+Release+RelWithDbgInfo postSolution mappings (NOT the LoaderLockHarness collapse pattern), 3 smoke TEST_CASEs (REQUIRE/REQUIRE_THROWS_AS/SECTION re-entry), third CI lane in ci.yml with stacked --reporter console + junit::out=...
- [x] 05-02-PLAN.md - Seed coverage: 6 TEST_CASEs in StringUtilityTests.cpp covering stringUtility::toBool/toString(int,fillCount)/toHexString/trim* with D-06 max-harness failure-mode table; docs/ai/test-harness-plan.md Tier 1 C++ side row closed; phase-end /gsd:code-review 05 checkpoint

### Phase 6: Cleanups, dep bumps, open questions, Tier 4 doc, 1.0 cut
**Goal**: Final pre-plugin sweep. ~30 enumerated cleanups land, dependencies are modernised, remaining open questions are answered or explicitly deferred, the Tier 4 manual residual is documented, the 24 preservation items are audited intact, and the framework hits the "1.0 cut" packaging + tag.
**Depends on**: Phase 5 (need full test coverage in place before doing the cleanup sweep so we can detect regressions; need green CI before tagging 1.0).
**Requirements**: STAB-03, STAB-04 (audit), STAB-05, TEST-04
**Open questions to resolve**: CON-O-06 (LeksysINI replacement plan) and CON-O-08 (DXSDK June 2010 vs Windows 10 SDK replaceability) are answered or explicitly deferred here. CON-O-08 in particular is a Phase 6 decision because the modernisation tradeoff touches dep bumps. The Tier 4 documentation in TEST-04 explicitly defines what does NOT ship automated in V1 — the manual-injection / visual-judgment / GPU-driver / WinForms-smoke residual.
**Preservation guard-rails**: This is the audit phase — verify every CON-N-01..-09, CON-M-01..-09, CON-T-01..-05 is still intact at the 1.0 tag (STAB-04). Cleanups touching CON-B-02 (unified Windows SDK target) and CON-B-03 (DXSDK include paths for Debug/Release) are constraint-implementing, not constraint-violating.
**Success Criteria** (what must be TRUE):
  1. The ~30 cleanups enumerated in assessment.md show as done; no dead code, no typos, unified Windows SDK target, DXSDK paths working in all three configs, `.clang-format` applied.
  2. imgui / spdlog / ImGuizmo dependency bumps land and CI stays green across all three configs.
  3. Each of the 8 open questions (CON-O-01..-08) has a documented disposition (answered + cited or "deferred to V2 with reason"); CON-O-09/-10/-11 already resolved in Phase 4.
  4. Tier 4 manual residual is documented in `.planning/codebase/TESTING.md` (or sibling) and referenced from CONVENTIONS.md (TEST-04).
  5. A 1.0 tag exists on `main`; a release artifact / packaging script produces a shippable build; the audit of CON-N-* / CON-M-* / CON-T-* preservation items passes (STAB-04).
**Plans**: 6 plans
**Plans**:
- [x] 06-01-PLAN.md — Overlay-debug investigation (d3d9 pattern-scan disposition + ImGui::ShowDemoWindow over live SWG; Tier-4 row; gates 06-02 imgui-docking switch per D-11)
- [x] 06-02-PLAN.md — Dep-bumps + toolchain (vcpkg manifest + 7 deps; spdlog 1.14 + OutputSink fence; ImGuizmo bump; imgui per 06-01 disposition; PlatformToolset v145; VSIX [16.0,19.0))
- [x] 06-03-PLAN.md — STAB-05 open questions (CON-O-08 DXSDK removal; CON-O-06 LeksysINI replacement inside UtINI::Impl; 12+ Catch2 INI fence cases)
- [x] 06-04-PLAN.md — CI flake fixes (loader-lock-harness 50ms contention + GameCallbacks ForceGCCollect AV; per-flake atomic fix + regression assertion)
- [x] 06-05-PLAN.md — STAB-03 cleanups + STAB-04 audit (full-repo clang-format + .git-blame-ignore-revs; TJT.ico cross-repo ejection; D-16 polish bundle a-h with Native.SendMessage IntPtr + int shim; dead-code purge excluding swg/ui/; 24-item preservation audit + xUnit grep tests)
- [x] 06-06-PLAN.md — TEST-04 Tier-4 doc + 1.0 packaging + tag (TESTING.md Tier-4 enumeration + CONVENTIONS.md cross-ref; WiX 5 MSI in installer/; release.yml on v1.0* tag; cross-repo TJT pinning; maintainer-signed 06-VERIFICATION.md; v1.0.0-rc.1 tag + GitHub Pre-release)

### Phase 7: TJT subpanel — TRE Browser (read-only)
**Architecture (DEC-C4):** Ships as an `IEditorPlugin` subpanel INSIDE The Jawa Toolbox plugin in the `UtinniPlugins` repo. NOT a standalone plugin. Distribution: users install Utinni + TJT as a pair.
**Goal**: First Wave-1 editor against the stabilised 1.0 framework. Read-only browser over the `.tre` virtual filesystem; surfaces every IFF, datatable, template, UI page, shader, and string-table entry the running client can load (proves PROD-01 end-to-end). Replaces SOE-era `TreeFileExtractor`. Proves the subpanel pattern within TJT — the `IEditorPlugin` MEF export + dockable `UserControl` shape that Phases 8-11 will repeat.
**Depends on**: Phase 6 (needs 1.0 framework — stable plugin lifecycle, single-source RVAs, CI gate); Phase 4 (CLI shim covers `parse-tre` / `list-objects` for golden testing).
**Requirements**: PROD-W1-TRE, PROD-01
**Open questions to resolve**: None — all are resolved by Phase 6.
**Preservation guard-rails**: Subpanel must conform to CON-M-01/02 (IPlugin/IEditorPlugin SPI) and the canonical Jawa Toolbox `*Impl` separation pattern (CON-T-05). Uses the existing `treefile::getAllFilenames` hook (CON-N-02 thin-wrapper surface) without modifying the native hook.
**Success Criteria** (what must be TRUE):
  1. TRE Browser subpanel loads inside TJT in the editor host against a live SWG client.
  2. User can navigate the full `.tre` mount set, expand subtrees, and view individual file metadata.
  3. The browse surface covers every asset class PROD-01 lists (IFF, datatable, template, UI page, shader, string-table entry).
  4. The CLI golden lane covers the same code paths the subpanel uses for browse via the shared `Formats/` core: `utinni-cli parse-tre` / `list-objects` (Phase 4) PLUS `TreArchiveIndexTests` (browse-index + payload resolution), `decode-iff` (per-type decoders + shader/UI-page summary), and `inspect-iff` (IFF chunk tree / OffsetBytes) — do NOT over-fit `parse-tre` alone (revised post cross-AI review Round 2; the shared `TreArchiveIndex`/`TrePayloadResolver`/`Formats/Decoders` facade is the lock-step mechanism).
**Plans**: 6 plans (revised from 4 after cross-AI review — added 07-00 Wave-0 fixture gate; split 07-04 into 07-04a/07-04b)
**UI hint**: yes
**Plans**:
- [x] 07-00-PLAN.md — Wave-0 fixture gate: deterministic in-repo synthetic TRE/COT2000 fixtures (v6000 zlib crc-first TOC, SELF-CONTAINED COT2000 ≥2-tree index + companion .tre archives, non-6000-layout 5000 header, 0004 header, 4 malformed incl. detectable-bad zlib frame) via TreFixtureBuilder + FixturePath SWG_SAMPLE_TRE_DIR resolver; gates Wave 1 (Wave 0)
- [x] 07-01-PLAN.md — TRE reader version-dispatch (0004/0005/0006/5000-enumerate-only/6000/COT2000 + SearchTOC) + zlib framing + lazy TOC-only + internal PayloadReadCount + Open(Stream) contract + named checked arithmetic + CotMasterIndex + resolution-complete TreEntryDescriptor + single TrePayloadResolver.TryResolve facade; parse-tre + 0004 golden + list-objects (OBJS byte-scan retired) golden tests (Wave 1)
- [x] 07-02-PLAN.md — TRE Browser shell: resizable UtinniForm + virtual-path TreeView (lazy, per-branch batched) + 250ms-debounced flat-index filter (whole-node bold, 5000-match cap → flat-ListView fallback) + concrete client-dir resolution (GetWorkingDirectory primary + ini fallback per CONTEXT line 100) + Game.Repository install-time-snapshot overlay (FilenameCount+GetFilenameAt); GetForms() registration (Wave 2)
- [x] 07-03-PLAN.md — IffChunk.OffsetBytes (framework) + inspect-iff golden bump + Detail pane: metadata header + type/version banner (version-accurate encrypted) + universal IFF chunk tree (TAG·size·@offset) + raw hex peek + DISTINCT encrypted/unsupported-raw/parse-fail states; AfterSelect on-demand resolve via TrePayloadResolver.TryResolve (Wave 3)
- [ ] 07-04a-PLAN.md — Framework decoders: datatable + string-table + object-template (bounded: declared base + local fields, no recursive cross-IFF walk; pure, LE scalars, division-form checked counts) + decode-iff CLI verb + golden tests (Wave 4)
- [ ] 07-04b-PLAN.md — Mesh/skeleton/anim AppearanceSummary + shader/UI-page IffStructureSummary (locked UI-page tag + path/ext hint; criterion #3 coverage) + decode-iff dispatch + goldens + all five row-capped structured-view families in the detail pane (Wave 5)

### Phase 8: TJT subpanel — IFF Editor (read + write)
**Architecture (DEC-C4):** Ships as an `IEditorPlugin` subpanel inside TJT. IFF chunk read/write primitives (`Iff::read`, `Iff::write`, FORM/PROP semantics, BLOB streams) ship in `TheJawaToolboxDotNet` and `TheJawaToolbox` (sibling classes to the panel) so Phases 9-11 layer on the same shared code WITHIN TJT — no inter-plugin coupling, no library-version surface.
**Goal**: Foundational read/write subpanel over IFF chunks across the client's IFF surface. Replaces SOE-era `IFFEditor`. Most-leveraged Wave-1 subpanel — Phases 9, 10, 11 all consume the IFF primitives that ship here.
**Depends on**: Phase 7 (TRE Browser proves subpanel pattern; IFF Editor builds on the same browse surface for "open from TRE").
**Requirements**: PROD-W1-IFF; contributes to PROD-02 aggregate.
**Open questions to resolve**: None.
**Preservation guard-rails**: Conforms to CON-M-01/02 SPI and CON-T-05 `*Impl` separation. Save paths must not break CON-M-05 (UndoRedoManager scene-cleanup contract) or CON-N-04 (memory write VirtualProtect bracket — if IFF writes touch any mapped client memory).
**Success Criteria** (what must be TRUE):
  1. IFF Editor subpanel loads inside TJT in the editor host against a live SWG client.
  2. User can open an IFF file (via TRE Browser subpanel or file picker), view chunk hierarchy, edit chunk content, and save modifications back to a file the live client reloads correctly.
  3. `utinni-cli inspect-iff` golden test (from Phase 4) covers the same read path the subpanel uses.
  4. Edits survive a save → reload round trip without corrupting unedited chunks.
  5. IFF primitives are exported from `TheJawaToolboxDotNet` such that Phases 9-11 subpanels can consume them via direct same-assembly reference, no public-API versioning concern.
**Plans**: TBD
**UI hint**: yes
**Plans (placeholder)**:
- [ ] 08-01: TBD

### Phase 9: TJT subpanel — Datatable Editor (`.tab`)
**Architecture (DEC-C4):** Ships as an `IEditorPlugin` subpanel inside TJT. Reuses the IFF primitives that Phase 8 shipped in `TheJawaToolboxDotNet` — same-assembly reference, no public-API contract.
**Goal**: View and edit `.tab` datatables (tabular client data). Replaces SOE-era `SwgDataTableTool`. Layers on Phase 8's IFF read/write where `.tab` is IFF-backed.
**Depends on**: Phase 8 (IFF read/write primitives in TJT).
**Requirements**: PROD-W1-DT; contributes to PROD-02 aggregate.
**Open questions to resolve**: None.
**Preservation guard-rails**: Conforms to CON-M-01/02 SPI and CON-T-05 `*Impl` separation.
**Success Criteria** (what must be TRUE):
  1. Datatable Editor subpanel loads inside TJT in the editor host against a live SWG client.
  2. User can open a `.tab` file, view rows and columns, edit cell values, and save back.
  3. The live SWG client picks up the edit on the relevant reload path for the datatable in question.
  4. Edits preserve schema (column types, foreign-key-style references) without silent corruption.
**Plans**: TBD
**UI hint**: yes
**Plans (placeholder)**:
- [ ] 09-01: TBD

### Phase 10: TJT subpanel — String-table Editor (`.stf`)
**Architecture (DEC-C4):** Ships as an `IEditorPlugin` subpanel inside TJT. Reuses Phase 8 IFF primitives.
**Goal**: View and edit `.stf` string tables (localised in-game text). Replaces SOE-era `SwgStringEditor`. Layers on Phase 8's IFF read/write.
**Depends on**: Phase 8 (IFF read/write primitives in TJT).
**Requirements**: PROD-W1-STF; contributes to PROD-02 aggregate.
**Open questions to resolve**: None.
**Preservation guard-rails**: Conforms to CON-M-01/02 SPI and CON-T-05 `*Impl` separation. Unicode handling must preserve the typo-fix policy from STAB-03 (e.g. `Jo�o → João` — string editor itself must not regress encoded text on round-trip).
**Success Criteria** (what must be TRUE):
  1. String-table Editor subpanel loads inside TJT in the editor host against a live SWG client.
  2. User can open a `.stf` file, view string entries (with localisation keys), edit text, and save back.
  3. The live SWG client renders edited strings on reload.
  4. Edits round-trip cleanly for non-ASCII characters (e.g. `João`).
**Plans**: TBD
**UI hint**: yes
**Plans (placeholder)**:
- [ ] 10-01: TBD

### Phase 11: TJT subpanel — Object Template Editor
**Architecture (DEC-C4):** Ships as an `IEditorPlugin` subpanel inside TJT. Reuses Phase 8 IFF primitives. Final V1 subpanel.
**Goal**: Edit object templates (the `.iff`-based template hierarchy driving in-world object behaviour and appearance). Final V1 subpanel; this phase closes the "Demo + CI green" milestone.
**Depends on**: Phase 10 (sequence-end position; benefits from all four prior Wave-1 subpanels being demoable).
**Requirements**: PROD-W1-OT; contributes to PROD-02 aggregate. **V1 closure: at this phase's success, V1 ships.**
**Open questions to resolve**: None.
**Preservation guard-rails**: Conforms to CON-M-01/02 SPI and CON-T-05 `*Impl` separation. Save path must not break CON-M-05 (UndoRedoManager on scene cleanup) since object templates can affect live-scene objects.
**Success Criteria** (what must be TRUE):
  1. Object Template Editor subpanel loads inside TJT in the editor host against a live SWG client.
  2. User can open an object template, view inherited fields, edit overrideable fields, and save back.
  3. The live SWG client reflects the edit when the object respawns or reloads.
  4. **V1 release gate met**: all 15 critical bugs are closed, Tier 1 + Tier 2 CI is green on `main`, and all five Wave-1 subpanels (TRE Browser, IFF Editor, Datatable Editor, String-table Editor, Object Template Editor) demo end-to-end inside TJT against a live SWG client. Tag V1.
**Plans**: TBD
**UI hint**: yes
**Plans (placeholder)**:
- [ ] 11-01: TBD

## V2 Scope Boundary

Explicitly deferred to V2; called out here so the V1 boundary is clear:

- **Tier 3 mock-D3D9 + recorded fixtures** (REQ-V2-tier-3-mock-d3d9) — V1 covers the D3D9 detour regression surface via the documented Tier 4 manual residual (TEST-04). Tier 3 is a separate V2 effort.
- **Wave-2 plugins** (REQ-V2-wave-2-plugins) — Conversation, Quest, Buildout, Particle, UI Page, Shader. **MAY ship as standalone plugins (not TJT subpanels);** DEC-C4's "subpanel inside TJT" choice is V1-scoped. If a third-party plugin ecosystem develops, V2 re-opens the subpanel-vs-standalone call per plugin and likely moves IFF primitives from TJT into UtinniCore so cross-plugin code-sharing stops being a TJT internal concern.
- **Wave-3 plugins** (REQ-V2-wave-3-plugins) — Mod Manager, Packager, Community Hub, Asset Diff. **Same subpanel-vs-standalone re-open as Wave-2.**
- **Broader live-preview reload paths** (REQ-V2-live-preview-edits) — V1 piggybacks on whatever reload paths Wave-1 subpanels need; broader live-preview is V2.
- **Author-new-content workflow** (REQ-V2-author-new-content) — V2+.
- **One-click packaging + community hub** (REQ-V2-one-click-package, REQ-V2-share-to-hub) — Wave-3 work, V2+.
- **Promote IFF primitives from TJT to UtinniCore framework** (REQ-V2-iff-framework-promotion, derived from DEC-C4 V1 scope-fence) — Phase 8 ships IFF read/write inside `TheJawaToolboxDotNet`/`TheJawaToolbox`. If V2 introduces third-party plugins that need IFF parsing, promote those primitives up into UtinniCore so they're not buried inside TJT. Pure code-motion refactor at that point; the V1 subpanels just rebase their `using` directives.

## Open-Question → Phase Mapping

Eight inherited open questions from assessment.md plus three test-harness opens. Each gates a specific phase plan:

| Open Question | Gates Phase | Drives |
|---------------|-------------|--------|
| CON-O-10 (test project layout) | Phase 1 | Where xUnit project lives in the solution |
| CON-O-01 (`isSafeToUse` operator) | Phase 2 | Correctness of one C-class fix |
| CON-O-02 (`AddPostDrawLoopCall` usage) | Phase 2 | Shape of C-04 fix |
| CON-O-03 (delegate corruption smell) | Phase 2 | `GCHandle.Alloc` fix in `GameCallbacks.cs` |
| CON-O-04 (VS 2019 pin rationale) | Phase 2 | C-12 widening to VS 2022 |
| CON-O-05 (StdEdited.cs curation) | Phase 3 | R-F CppSharp header auto-discovery |
| CON-O-07 (Sytner's plugin status) | Phase 3 | R-B plugin lifecycle contract |
| CON-O-09 (fixture storage in-repo vs LFS) | Phase 4 | Where TRE binary samples live |
| CON-O-11 (CLI public vs internal) | Phase 4 | `utinni-cli` distribution policy |
| CON-O-06 (LeksysINI replacement plan) | Phase 6 | Dependency-bump decision |
| CON-O-08 (DXSDK vs Windows 10 SDK) | Phase 6 | DXSDK modernisation decision |

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 (no decimal insertions yet).

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. CI + Tier 1 C# scaffold | 0/2 | Not started | - |
| 2. Critical bug burn-down (C-01..C-15) | 0/TBD | Not started | - |
| 3. Strategic reworks (R-A..R-H) | 0/TBD | Not started | - |
| 4. Tier 2 CLI shim + golden fixtures | 0/TBD | Not started | - |
| 5. Tier 1 C++ unit tests | 0/TBD | Not started | - |
| 6. Cleanups, dep bumps, open questions, Tier 4 doc, 1.0 cut | 5/6 | In Progress|  |
| 7. Wave-1 plugin — TRE Browser | 0/6 | Planned | - |
| 8. Wave-1 plugin — IFF Editor | 0/TBD | Not started | - |
| 9. Wave-1 plugin — Datatable Editor | 0/TBD | Not started | - |
| 10. Wave-1 plugin — String-table Editor | 0/TBD | Not started | - |
| 11. Wave-1 plugin — Object Template Editor | 0/TBD | Not started | - |
