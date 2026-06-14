# Roadmap: Utinni

## Overview

V1 ships in two halves, sequenced "foundations before features" per vision.md and assessment.md. **Half 1 (Phases 1–6)** stabilises the framework: a CI + Tier-1 C# scaffold lands first as the smallest unlock, then the 15 critical bugs burn down, then the 8 strategic reworks polish plugin-authoring ergonomics, then the Tier 2 CLI shim and Tier 1 C++ tests close the test-harness gap, then a cleanup + open-questions sweep produces the 1.0 framework cut. **Half 2 (Phases 7–11)** delivers the Wave-1 plugin set on top of that stabilised framework: TRE Browser, then IFF Editor (foundational, most-leveraged), then Datatable, then String-table, then Object Template. V1 ships when (a) Tier 1 + Tier 2 CI is green with all 15 critical bugs closed and (b) all five Wave-1 plugins demo end-to-end against a live SWG client — the user-supplied "Demo + CI green" metric.

**V1 SHIPPED 2026-06-01 (`v1.0.0`).** Milestone **v2.0 "AI-Assisted SWG Tools" (Phases 12–16)** turns Utinni from a tool that *edits* assets into one that *authors* them, and makes the whole pipeline drivable by an AI agent. v2.0 strongly honors the research-recommended build order: a **hard-gate revive-feasibility spike first** (Phase 12), then cheap **revive+wrap** of the SOE build CLIs which also unblocks OT Tier-2 (Phase 13), then the **headless MCP server** centerpiece (Phase 14), then the meatier **Wave-2 editors** (Phase 15), with the **live-injected MCP bridge + Blender boundary** explicitly last (Phase 16). See `.planning/research/SUMMARY.md` + `ARCHITECTURE.md`.

**Deferred to V1's V2 boundary (now partly in-scope for v2.0):** Tier 3 mock-D3D9 + recorded-fixtures harness (still deferred); the remaining Wave-2 plugins (Conversation, Quest, Buildout, UI Page, Shader) and Terrain editor (`PROD-W2-TRN`, deferred to v2.1); Wave-3 plugins; one-click packaging + community hub. Tier 4 manual residual is documented in V1 (TEST-04) but not automated.

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
- [x] **Phase 11: TJT subpanel — Object Template Editor** — Object-template editor; the final V1 subpanel; "Demo + CI green" milestone closure. Per DEC-C4, ships as a TJT subpanel. (completed 2026-06-01)

### Milestone v2.0 — "AI-Assisted SWG Tools" (Phases 12–16)

**Phase Numbering (v2.0):** Continues from V1 — V1 ended at Phase 11, so v2.0 starts at **Phase 12**. Integer phases (12–16) are planned milestone work; decimal phases (e.g. 12.1) reserved for urgent insertions. Build order strongly honors the research-recommended sequence: **revive-feasibility spike as a hard gate first**, then cheap revive+wrap (which also unblocks OT Tier-2), then the headless MCP centerpiece, then the meatier Wave-2 editors, with live-injected MCP explicitly last.

- [ ] **Phase 12: Revive-feasibility spike (HARD GATE) + intro-skip crash** — Verify `TreeFileBuilder` + `TemplateCompiler`/`TemplateDefinitionCompiler` build + link standalone at v145 in a Utinni-owned `tools/` tree (lift-and-shift), strip dead Perforce/transitive deps, produce a per-tool dependency manifest + pinned `swg-client-v2` SHA. Diagnose + fix the independent intro-skip scene-transition crash (RESID-02). Gates all of AUTH.
- [x] **Phase 13: Wrap compilers as CLI verbs + close OT Tier-2** — Wrap the revived compilers/builders as `utinni-cli` verbs (`compile-definition`, `compile-template`, `build-tre`, datatable compile + item exporters); add the net-new CLI SAVE verb; surface the param→type schema to close the Object Template Editor's Tier-2 typed display (RESID-01).
 (completed 2026-06-05)
- [x] **Phase 14: Headless MCP server (`Utinni.Mcp`) — the centerpiece** — A separate net10 stdio MCP process shelling `utinni-cli`: read tools over the existing verbs + write/SAVE tools defaulting to loose-override with byte-exact verify-before-commit, fail-closed `resolvedRoot`, and a first-class `MCP-SECURITY.md` threat register.
 (completed 2026-06-07)
- [x] **Phase 15: Wave-2 editors (WorldSnapshot, Particle) + presentation residuals** — Ship the WorldSnapshot/object-placement SubPanel (zero new deps) then the Particle/client-effect SubPanel (new `.prt` codec) as TJT MEF SubPanels; enumerate + fix the SWG window-resize/fullscreen edge cases (RESID-04) and confirm SC3 live-reload candor (RESID-03). **CLOSED 2026-06-13** (approved-with-deferred-residual; RESID-03 live render-on-reload deferred, gated on the disabled loose searchPath).
- [x] **Phase 16: Live-injected MCP bridge + Blender ecosystem boundary** — Add the optional named-pipe IPC live bridge so an agent can preview an edit in the injected client (MCP-03); formalize the Utinni ↔ `swg-blender-plugin` file-format / `.rsp` search-path contract (ECO-01). (completed 2026-06-14)

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
- [x] 07-04a-PLAN.md — Framework decoders: datatable + string-table + object-template (bounded: declared base + local fields, no recursive cross-IFF walk; pure, LE scalars, division-form checked counts) + decode-iff CLI verb + golden tests (Wave 4)
- [x] 07-04b-PLAN.md — Mesh/skeleton/anim AppearanceSummary + shader/UI-page IffStructureSummary (locked UI-page tag + path/ext hint; criterion #3 coverage) + decode-iff dispatch + goldens + all five row-capped structured-view families in the detail pane (Wave 5)

### Phase 8: TJT subpanel — IFF Editor (read + write)
**Architecture (DEC-C4 + D-01 reconciliation):** Editor subpanel still ships inside TJT (DEC-C4 host placement unchanged). IFF chunk read/write primitives (`IffReader`, `IffWriter`, `MutableIffDocument`, `OpenSource`) ship in `UtinniCoreDotNet/Formats/Iff/` (next to the existing reader) so Phases 9-11 consume them via the direct `UtinniCoreDotNet.dll` reference — no inter-plugin coupling, no library-version surface. The original DEC-C4 intent (one shared code path; no public-API/plugin-version concern) is preserved; only the primitives' assembly location is reconciled per CONTEXT.md D-01.
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
  5. IFF primitives are exported from a shared, non-plugin assembly (`UtinniCoreDotNet/Formats/Iff`) that Phases 9-11 reference directly via the existing `UtinniCoreDotNet.dll` reference — preserving the original intent (no public-API/plugin-version concern). Reconciled per CONTEXT.md D-01 (DEC-C4 host placement is unchanged; only the primitives' assembly location is reconciled).
**Plans**: 7 plans
**UI hint**: yes
**Plans**:
- [x] 08-01-PLAN.md — IffWriter + mutable hybrid DOM (D-07) + framework xUnit + ROADMAP Criterion 5 amendment (D-01) + OpenSource 4-case provenance + RemoveByStableId (round-3 R3-M3)
- [x] 08-02-PLAN.md — roundtrip-iff CLI verb + golden fixtures (D-02; byte-exact gate for Criterion 4); structural --remove-leaf golden closes round-2 MEDIUM 8 / cursor N-M4
- [x] 08-03-PLAN.md — Extract shared IffChunkTree control (D-09); TreDetailPane consumes it (read API preserved)
- [x] 08-04-PLAN.md — FormIffEditor shell + leaf editing (D-04) + structural ops (D-03) + editor-local undo/redo (D-08)
- [x] 08-05-PLAN.md — File save modes 1/2 (D-05.1/2) + tiered forced reload (D-06) + TRE hand-off + Plugin.cs registration + live smoke (approved 2026-05-28)
- [x] 08-06-PLAN.md — In-memory live patch (D-05.3) via CON-N-04 bracket + confirm dialog; LivePatchValidator + 5 [Fact]s (round-2 HIGH-B); 4 new csproj entries (round-2 HIGH-A); Task 5 approved on automation alone (smoke=automation-only); D-05.3 infra-ready, user-disabled (round-2 MEDIUM 11)
- [x] 08-07-PLAN.md — .tre repack (D-05.4) TreWriter + repack save target + 5 new test classes (15 outcomes) automating the on-disk contract per maintainer direction; smoke=automation-augmented; Open Q1 (cursor N-H1 live-client path-CRC ACK) + Open Q5 (UI end-to-end) deferred; consolidated Phase 8 criteria sign-off pending

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
**Plans**: 7 plans (5 waves; 09-04 + 09-05 parallel in Wave 3)
**UI hint**: yes
**Plans**:
- [x] 09-01-PLAN.md — Typed DTII format primitives (CF-01 framework-side): DataTableColumnType + MangleValue + DataTableHashCrc port + DataTableDocument.FromIff + MutableDataTable* hybrid DOM (CF-04 per-cell originalSlice) + DataTableWriter composing IffWriter; UI-SPEC Assumption A1 enum-syntax typo correction (Task 0)
- [x] 09-02-PLAN.md — `utinni-cli roundtrip-tab` verb + golden suite + DataTableFixtureBuilder; SC4 byte-exact-on-untouched-cells CLI gate (CF-02)
- [x] 09-03-PLAN.md — FormDatatableEditor host + ThemedDataGridView + per-type cell widgets + Plugin.cs registration + singleton hide-not-dispose pattern from commit 1 + DataGridView bind-latency probe (gates Plan 09-06 VirtualMode decision)
- [x] 09-04-PLAN.md — DatatableEditController (CF-06) + 11 T4 commands + D-04 cascade + FormAddColumnDialog + FormTypeChangeCascadeDialog + R-04 save-block on every Save▾ item + D-02 safety-net via reused FormSaveConfirmDialog
- [x] 09-05-PLAN.md — DatatableSaveTargets composition shim + FormDatatableEditor Save▾ wire (4 modes; mode 3 disabled per CF-03) + TRE Browser D-10.2 hand-off + IFF Editor D-10.3 hand-off + ClientReloadDispatcher tier-(b) wire (CF-05 locked UI)
- [x] 09-06-PLAN.md — CSV/TSV delta-import + Find/Replace + column-click view-only sort (D-09 grep gate + Sort_DoesNotMutateModelOrder xUnit fact) + DT_Comment frozen-row toggle + framework CsvCellCoercion (checker B-1 extraction) + conditional VirtualMode fallback (gated on 09-03 measurement)
- [x] 09-07-PLAN.md — Tier-4 maintainer live-SWG smoke (Phase 8 precedent: smoke=automation-augmented; live ACK deferred-but-acceptable for V1)

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
**Plans**: 6 plans
**UI hint**: yes
**Plans**:
- [x] 10-01-PLAN.md — Format core: flat-binary StringTableDocument.FromBytes reader (greenfield) + mutable model + explicit byte-exact StringTableWriter (canonical order, nextUniqueId/sourceCrc preserved, UTF-16LE verbatim) + StringTableEditController + 4 T4 commands
- [x] 10-02-PLAN.md — utinni-cli roundtrip-stf SC4 byte-exact golden gate (CF-02) + StringTableFixtureBuilder + named João + sourceCrc-preserve goldens
- [ ] 10-03-PLAN.md — FormStringTableEditor host + two-column (Key/Text) ThemedDataGridView + name validation + T4 mutation via controller + Plugin.cs registration
- [ ] 10-04-PLAN.md — Bulk/translation: Find/Replace (key+text) + CSV/TSV delta-import + export + view-only sort + live filter (Ctrl+L) + PO/gettext export
- [ ] 10-05-PLAN.md — Save targets (modes 1/2/4; mode 3 disabled) + reload dispatch (tier-b) + StringTableHandoffPolicy + TRE Browser "Open in String-table Editor" hand-off (D-04)
- [ ] 10-06-PLAN.md — Tier-4 maintainer live-SWG smoke (automation-augmented; CF-05 scene-change-vs-relog confirmation)

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
**Plans**: 5 plans
**UI hint**: yes
**Plans**:
- [x] 11-01-PLAN.md — Framework format core: ReadInt8 cursor helper + self-describing param value codec (typed scalars + delta + hex fallback) + MutableObjectTemplate over MutableIffDocument + byte-exact ObjectTemplateWriter (Wave 1)
- [x] 11-02-PLAN.md — DERV-chain effective-merge resolver (origin markers + graceful degradation + depth/cycle guard) + editor-local ObjectTemplateEditController (override/revert/edit + undo) + roundtrip-ot CLI golden gate (Wave 2)
- [x] 11-03-PLAN.md — FormObjectTemplateEditor host (toolbar + breadcrumb + Field/Value/Origin/Type grid + undo/redo + singleton policy) + 5th SubPanel registration + TRE Browser and IFF Editor hand-offs (Wave 3)
- [x] 11-04-PLAN.md — Per-type value widgets + hex-fallback sub-editor + override/revert/edit mutations + Save modes 1/2/4 shim + locked CF-05 reload badge + classifier verify-test (Wave 4)
- [x] 11-05-PLAN.md — Full automated regression + V1 release-gate evidence doc + live-SWG smoke (SC1/SC2/SC3) + V1 sign-off and tag (Wave 5)

### Phase 12: Revive-feasibility spike (HARD GATE) + intro-skip crash
**Milestone**: v2.0 — "AI-Assisted SWG Tools"
**Goal**: Prove the entire revive+wrap strategy is viable *before* anything depends on it. Lift-and-shift `TreeFileBuilder` + `TemplateCompiler` + `TemplateDefinitionCompiler` into a Utinni-owned `tools/` tree, verify each builds + links standalone at v145, strip the dead Perforce/Alienbrain/transitive dependency graph, and produce a per-tool dependency manifest plus a pinned `swg-client-v2` source SHA. In parallel, diagnose and fix the independent intro-skip scene-transition crash (RESID-02) — it is early-slottable because it blocks nothing else and is its own self-contained defect.
**Depends on**: V1 (`v1.0.0`) — the stabilised framework, the shared v145 toolset, and the `Utinni.Cli`/`UtinniCoreDotNet` headless pipeline the later phases wrap.
**Requirements**: AUTH-01, RESID-02
**Hard-gate note**: AUTH-01 gates ALL of AUTH (Phase 13's compile/build verbs cannot wrap a tool that will not compile). Status is uneven and empirical, not uniform — `TemplateCompiler.vcxproj` has built v145 Debug objects on disk (likely-green); `TreeFileBuilder.vcxproj` is v145-configured with no build output (unverified). Treat "is v145 in the vcxproj" and "actually builds + links" as different facts; resolve by a build pass, not by reading. **Documented fallback:** if a tool refuses v145, build *that* tool at v143 in `tools/` and still wrap it — the subprocess seam is toolset-agnostic; the lift-and-shift constraint forbids building *in* `swg-client-v2`, not building at a given toolset in our own tree.
**Constraint guard-rails**: Lift-and-shift LOCKED — copy source + required shared libs into repo-local `tools/`; never `#include`/ProjectReference across into the live `swg-client-v2` tree (it is mid-D3D9→D3D11 migration on `koogie-msvc-cpp20-base`). Record the exact lifted-from x86 SHA (pin a SHA, not a branch HEAD). Watch x64 vs Utinni's hard x86 constraint (CON-P-02) — the revive targets are `Win32`/x86 today; an upstream `x64bit-Upgrade` migration would diverge.
**Success Criteria** (what must be TRUE):
  1. `TemplateCompiler`, `TemplateDefinitionCompiler`, and `TreeFileBuilder` each build + link standalone at v145 (or the documented v143 fallback for any tool that refuses) from a Utinni-owned `tools/Utinni.Tools.sln`, with no reference into `swg-client-v2`.
  2. A per-tool dependency manifest exists enumerating each tool's real `#include` closure and required shared libs (zlib, pcre/4.1), with the dead `perforce`/`alienbrain` include paths pruned.
  3. The exact `swg-client-v2` source SHA the tools were lifted from is recorded in-repo (a SHA, not a branch).
  4. Each built tool runs headless once against a sample input and produces a non-empty artifact (or a clear, captured failure mode if a tool is parked behind the v143 fallback).
  5. The intro-skip scene-transition crash (RESID-02) is diagnosed to a faulting address/module via the deployed VEH logger and fixed; the intro-skip path no longer crashes on a live injected session.
**Plans**: 4 plans (3 waves; AUTH-01 build track in waves 1-3, RESID-02 parallel in wave 1)
**Plans**:
- [ ] 12-01-PLAN.md — Lift tools/ tree + Utinni.Tools.sln + zlib 1.1.4 + PINNED-SHA.md; TreeFileBuilder first-green @ v145/Win32 (Wave 1)
- [ ] 12-02-PLAN.md — Lift TemplateCompiler + TemplateDefinitionCompiler (pcre 4.1 + Perforce keep-or-stub); v145 build+link with D-10 stop-and-ask fallback gate (Wave 2)
- [ ] 12-03-PLAN.md — Reference-pair availability checkpoint + per-tool byte-exact smokes (D-09, no fallback) + DEPENDENCY-MANIFEST.md + self-hosted v145 CI build lane (Wave 3)
- [ ] 12-04-PLAN.md — RESID-02 intro-skip crash: live VEH capture + module/RVA root-cause analysis + Utinni-side fix or documented game-side RCA (Wave 1, parallel)
**Research flag**: yes — the genuine unknown is empirical dependency closure + per-tool build status; budget a real build pass (`/gsd:plan-phase --research-phase 12`).

### Phase 13: Wrap revived compilers as CLI verbs + close OT Tier-2
**Milestone**: v2.0 — "AI-Assisted SWG Tools"
**Goal**: Now that the tools compile (Phase 12), wrap them as golden-tested `utinni-cli` verbs (the DEC-C3 Tier-2 pattern) and add the net-new write surface the MCP server will need. `TemplateDefinitionCompiler`'s `.tdf`/`.tpd` → per-class param→type schema is the cheapest route to closing the Object Template Editor's carried Tier-2 residual, so this phase pays a centerpiece authoring feature *and* a V1 residual in one. Coexistence-by-verb-ownership is preserved: BUILD-from-source uses the revived compilers (`compile-*`/`build-*` verbs); EDIT-existing-binary continues to use the byte-exact `UtinniCoreDotNet` writers (`roundtrip-*`/the new SAVE verb).
**Depends on**: Phase 12 (HARD GATE — cannot wrap a tool that will not compile); reuses the existing `Utinni.Cli` golden-fixture harness.
**Requirements**: AUTH-02, AUTH-03, AUTH-04, AUTH-05, AUTH-06, RESID-01
**Constraint guard-rails**: Every capability is a CLI verb FIRST (golden-tested), so the Phase-14 MCP layer stays a thin dispatcher with zero business logic. Name BUILD verbs `compile-*`/`build-*` (distinct from EDIT `roundtrip-*`/`save`) so the downstream LLM picks the right write path. The SAVE verb is the single biggest net-new code item — specify its shape per-format, loose-override-default, structured `{written, path, bytesWritten, backupPath, validated}` result.
**Success Criteria** (what must be TRUE):
  1. A modder can compile a `.tpf` object-template source to a byte-correct object-template `.iff` via a `utinni-cli` verb wrapping the revived `TemplateCompiler` (AUTH-03), with golden-fixture coverage.
  2. A modder can build a `.tre` archive from a source tree via a `utinni-cli` verb wrapping the revived `TreeFileBuilder` (AUTH-04).
  3. `TemplateDefinitionCompiler`'s `.tdf`/`.tpd` → per-class param→type schema is surfaced via a `utinni-cli` verb (AUTH-02), and the Object Template Editor displays list/struct params typed rather than raw (RESID-01 closed).
  4. `utinni-cli` gains a SAVE verb that writes an edited asset (loose-override or repack) with a structured result envelope — the net-new write surface MCP-02 wraps (AUTH-05).
  5. A modder can compile a datatable from CSV/XML source and run the `ArmorExporterTool`/`WeaponExporterTool` item exporters via `utinni-cli` verbs (AUTH-06), each with at least one golden fixture.
**Plans**: 6 plans (4 waves; AUTH-06 native lift + subprocess/.rsp primitives + SAVE in wave 1, build verbs in waves 2-3, RESID-01 typed display in wave 4)
**Plans**:
- [x] 13-01-PLAN.md — Lift the 3 AUTH-06 natives (DataTableTool/ArmorExporterTool/CoreWeaponExporterTool) + sharedXml/libxml2 into tools/; Perforce-stub; build green @ v145 (Wave 1) [AUTH-06]
- [x] 13-02-PLAN.md — NativeToolRunner subprocess seam + banner-normalize + RspSynthesizer (.rsp recipe from a real .tre) (Wave 1) [AUTH-04]
- [x] 13-03-PLAN.md — SAVE verb (4-format, loose-override default, locked envelope) + separate repack-tre verb (D-10) over framework primitives (Wave 1) [AUTH-05]
- [x] 13-04-PLAN.md — compile-template (.tpf->.iff, cross-checked) + build-tre (synth-.rsp byte-exact ladder) (Wave 2) [AUTH-03, AUTH-04]
- [x] 13-05-PLAN.md — compile-definition (schema artifact, D-08) + compile-datatable (managed-oracle cross-check) + export-armor/export-weapon (Wave 3) [AUTH-02, AUTH-06]
- [x] 13-06-PLAN.md — OT editor typed list/struct display over the committed schema (RESID-01 close) + Tier-4 visual checkpoint (Wave 4) [RESID-01]

### Phase 14: Headless MCP server (`Utinni.Mcp`) — the centerpiece
**Milestone**: v2.0 — "AI-Assisted SWG Tools"
**Goal**: The centerpiece — a separate modern-.NET (net10) `Utinni.Mcp` console process speaking MCP over stdio, owning ZERO format/business logic and dispatching every tool call to a `Process.Start` of `utinni-cli.exe` (or a Phase-13 build verb). Read tools wrap the existing nine `utinni-cli` verbs; write tools wrap the new SAVE verb and default to the loose-override tier with the full 5-layer safety model. The MCP security contract is a first-class, design-time deliverable (an `MCP-SECURITY.md` threat register mirroring Phase-7's), NOT a later hardening pass — over-broad tool shapes are un-retrofittable once agents depend on them.
**Depends on**: Phase 13 (read/edit/build/SAVE verbs must exist as CLI verbs before the dispatcher can wrap them).
**Requirements**: MCP-01, MCP-02
**Constraint guard-rails**: NEVER host the MCP SDK in-proc inside the net472/x86 injected client (Anti-Pattern 1) — the SDK targets net8/9/10, and an LLM transport loop must not live in SWG.exe's address space; the separate process is the honest seam. Pin `resolvedRoot` at server start, canonicalize once via `LooseOverridePath.Resolve`, NEVER accept an absolute path from the agent, fail closed if no root is configured. Write tools take typed structured args only (record index, column id, typed value) — never "apply the change you inferred". Gate `.tre` repack behind a distinct, off-by-default, `destructiveHint`+`dry_run`-annotated tool routed through `TreBackupPath`. stdio transport only (HTTP/SSE is out of scope and deprecated).
**Success Criteria** (what must be TRUE):
  1. An AI agent can READ any supported SWG asset (TRE/IFF/datatable/`.tab`/`.stf`/object-template) through the headless `Utinni.Mcp` server — a net10 process, stdio transport, wrapping the existing `utinni-cli` JSON verbs (MCP-01).
  2. An AI agent can EDIT + SAVE assets via per-format MCP write tools that default to the loose-override tier, with byte-exact verify-before-commit and a `dry_run` gate on destructive repack (MCP-02).
  3. The server pins `resolvedRoot` fail-closed at startup; no agent write can escape the resolved root or corrupt a source archive — demonstrated by a path-traversal/escape test.
  4. An `MCP-SECURITY.md` threat register documents the 5-layer defense model (annotations → elicitation → loose-override-default → verify-before-commit → backup/recovery) and the advisory-not-enforcement caveat on tool hints.
  5. A real MCP client completes the stdio handshake and round-trips at least one read tool and one edit→save tool against a sample asset.
**Plans**: 5 plans (4 waves; revised post cross-AI `--reviews` 2026-06-06 - both reviewers flagged the old 14-03 `roundtrip->save` two-step as a NO-OP that never persists the edit. Fix: a NEW golden-tested CLI verb family `apply-save-*` (apply-typed-edit + verify-untouched + WriteAtomic-to-loose-override in ONE atomic verb, failed-verify = exit 2), split out as Plan 14-03a. The MCP `save_*` tools wrap that single verb OPAQUELY. This is a scoped, documented exception to the "Phase 14 adds ZERO verbs" guard-rail - named, not silent.)
**Wave plan:** Wave 1 = net10 scaffold+ResolvedRoot/CliDispatcher foundation (14-01) IN PARALLEL WITH the net-new `apply-save-*` CLI verbs (14-03a; zero deps, touches only `Utinni.Cli`/`Utinni.Cli.Tests` — no overlap with 14-01). Wave 2 = read tools (14-02; `Utinni.Mcp`). Wave 3 = MCP write/repack tools (14-03; wraps the apply-save-* verbs + the 14-01 seam + the 14-02 mapper). Wave 4 = MCP-SECURITY.md + real-client integration close-out (14-04).
**Plans**:
- [x] 14-01-PLAN.md - net10 Utinni.Mcp scaffold + fail-closed ResolvedRoot + CliDispatcher (injectable timeout, default 60s) + netstandard2.0 LooseOverridePath extract via [TypeForwardedTo] + LooseOverridePathTests regression gate + Wave-0 tests + net10 CI lane + NuGet legitimacy gate (Wave 1) [MCP-01, MCP-02]
- [x] 14-02-PLAN.md - read tools (read_tre/inspect_iff/decode_iff/list_world_objects + get_template_schema) + CliResultMapper envelope-SHAPE validation + exit-code taxonomy + temp-boundary doc for get_template_schema (Wave 2) [MCP-01]
- [x] 14-03a-PLAN.md - NEW golden-tested CLI verbs apply-save-tab/iff/stf/ot (apply ONE typed edit -> verify byte-identity on untouched -> WriteAtomic loose-override; failed-verify = exit 2, no write) - the persist path the reviewers proved was missing (Wave 1; Utinni.Cli only) [MCP-02, AUTH-05]
- [x] 14-03-PLAN.md - MCP write tools (save_* wrap the SINGLE apply-save-* verb opaquely) + repack_tre (Destructive, host-side dry_run gate) + roundtrip_check (verify-only) + MCP-boundary path-escape integration test (Wave 3) [MCP-02]
- [x] 14-04-PLAN.md - MCP-SECURITY.md threat register (each row cites its proving test) + real-McpClient RoundTripTests with read-back-after-save assertion + committed binary Fixtures/ + isolated-copy repack + exact tool-count (Wave 4) [MCP-01, MCP-02]

### Phase 15: Wave-2 editors (WorldSnapshot, Particle) + presentation residuals
**Milestone**: v2.0 — "AI-Assisted SWG Tools"
**Goal**: Land the first Wave-2 DCC-style editors as TJT MEF `IEditorPlugin` SubPanels (the unchanged DEC-C4 Wave-1 seam). **WorldSnapshot first** — it grows the existing Snapshot panel, is injection-native already (Utinni's origin), and needs zero new format work. **Particle second** — the flashy AI-assist showcase, requiring a new `.prt` client-effect codec in `UtinniCoreDotNet`. Slot the two presentation residuals here: RESID-04 (window-resize / windowed↔fullscreen edge cases) pairs naturally with any D3D9-presentation editor work, and RESID-03 (SC3 live-reload candor) pairs with the editors' reload paths. (Terrain `PROD-W2-TRN` is explicitly deferred to v2.1 — heavier codec.)
**Depends on**: Phase 14 (stable headless base; matching MCP edit/save tools layer on the Phase-13 verbs + Phase-14 dispatcher). Editors are *costlier*, not *blocked* — sequenced after the cheap headless base.
**Requirements**: PROD-W2-WS, PROD-W2-PRT, RESID-03, RESID-04
**Constraint guard-rails**: No new plugin mechanism — Terrain/Particle/WorldSnapshot follow the exact `IEditorPlugin.GetSubPanels()` seam shipped + demoed in V1; conform to CON-M-01/02 SPI + CON-T-05 `*Impl` separation; apply the canonical singleton hide-not-dispose pattern from Phase 8. Encode a one-sentence preview-vs-author test per editor to stay out of 3D mesh/skel/anim authoring (DEC-A3, the Blender lane). RESID-04 presentation work must not call `IDirect3DDevice9::Reset` on SWG's device (owns untracked default-pool resources → crash) — resize the window and let windowed COPY Present handle the mismatch.
**Success Criteria** (what must be TRUE):
  1. A modder can view + edit object placements in a world snapshot via a Utinni SubPanel that extends the existing Snapshot panel and reuses shipped codecs — zero new format work (PROD-W2-WS).
  2. A modder can open + edit a particle / client-effect asset in a Utinni SubPanel, with live in-client preview when injected, backed by a new `.prt` codec in `UtinniCoreDotNet` (PROD-W2-PRT).
  3. Both SubPanels load inside TJT against a live SWG client and follow the Wave-1 MEF SubPanel seam unchanged.
  4. The SWG window-resize / windowed↔fullscreen edge cases are enumerated and fixed without a device Reset (RESID-04).
  5. SC3 live-reload semantics for string-table + object-template reload are confirmed and honestly stated in the editor reload-candor UI (RESID-03).
**Plans**: 21 plans (9 waves; 15-09..15-11 gap closure from the 15-SMOKE A9 undo-crash; 15-12..15-18 defect-driven gap closure from the 2026-06-13 15-SMOKE live smoke; 15-19..15-21 round-3 gap closure for B6 + D-ii)
**Plans**:
- [x] 15-01-PLAN.md — WorldSnapshot editor: placements table + multi-select bulk move/delete/retemplate (FormSnapshotPlacements companion window; WorldSnapshotBulkComposer) [PROD-W2-WS] (Wave 1)
- [x] 15-02-PLAN.md — .prt/PEFT typed codec in Formats/Particle (WaveForm/ColorRamp/EMTR) + degrade-dont-abort raw-preserve + byte-exact round-trip [PROD-W2-PRT] (Wave 1)
- [x] 15-03-PLAN.md — D-09 live-preview native hot-retrigger SPIKE (ParticleManager export or documented fallback; heap-free) [PROD-W2-PRT] (Wave 1)
- [x] 15-04-PLAN.md — .prt CLI verbs (decode-iff PEFT dispatch + roundtrip-particle golden) + thin MCP read tool (D-08) [PROD-W2-PRT] (Wave 2)
- [x] 15-05-PLAN.md — RESID-04: suppress exclusive-fullscreen mode switch (direct_input.cpp) + no-Reset regression gate (D-12/D-13) [RESID-04] (Wave 2)
- [x] 15-06-PLAN.md — FormParticleEditor: emitter tree + typed grid + hex fallback + AI Explain-effect (reuses 15-04 read path) + state-encoded Preview [PROD-W2-PRT] (Wave 3)
- [x] 15-07-PLAN.md — RESID-03: route .ws/.prt to honest tier-(b) reload candor + routing tests (D-14) [RESID-03] (Wave 3)
- [~] 15-08-PLAN.md — Tier-4 maintainer live-SWG smoke: WS/Particle demo + RESID-04 matrix + RESID-03 SC3 (autonomous:false) [PROD-W2-WS/PRT, RESID-03/04] (Wave 4) — SUPERSEDED: the live smoke was executed across the gap-closure re-smokes 15-18 + 15-21 (defects found → fixed → re-verified); closure signed in 15-SMOKE.md
- [x] 15-09-PLAN.md — GAP: null-guard all WS IUndoCommand Execute/Undo bodies + pure WorldSnapshotCommandGuard helper (fixes A9 undo-crash root cause) [PROD-W2-WS] (Wave 1, gap_closure)
- [x] 15-10-PLAN.md — GAP: UndoRedoManager.Clear() + IEditorPlugin undo seam; clear stack + gizmo on snapshot Load/Unload/Reload; route Ctrl+Z from FormSnapshotPlacements [PROD-W2-WS] (Wave 1, gap_closure)
- [x] 15-11-PLAN.md — GAP: full Release gate + reassemble bin/Release injection build for the 15-08 A9 re-verify; record outcome in 15-SMOKE.md [PROD-W2-WS] (Wave 2, gap_closure)
- [x] 15-12-PLAN.md — GAP: inject-root AssemblyResolve handler + ship netstandard.dll (B5 façade — loose-override Save works under injection for every editor) [PROD-W2-PRT, RESID-03] (Wave 5, gap_closure)
- [x] 15-13-PLAN.md — GAP: window-side watchdog re-asserts owned-popup embed on SWG's window-level fullscreen restyle, no device Reset (C3) [RESID-04] (Wave 5, gap_closure)
- [x] 15-14-PLAN.md — GAP: finalize A9 revert (live-node-by-id + optional-obj) + strip [A9-diag] logging + node-only guard coverage [PROD-W2-WS] (Wave 5, gap_closure)
- [x] 15-15-PLAN.md — GAP: ParticleReadAssist.LocateCli inject-root probe so a deployed utinni-cli.exe resolves (B7) [PROD-W2-PRT] (Wave 5, gap_closure)
- [x] 15-16-PLAN.md — GAP: managed polish — Particle param-grid rebind (B4/B5), honest no-hook preview tooltip (B6), delete-confirm candor + BulkDelete DetailLevelChanged (A7) [PROD-W2-PRT, PROD-W2-WS, RESID-03] (Wave 5, gap_closure)
- [x] 15-17-PLAN.md — GAP: full Release gate + reassemble bin/Release with netstandard.dll + utinni-cli.exe, content-verified [PROD-W2-WS/PRT, RESID-03/04] (Wave 6, gap_closure)
- [x] 15-18-PLAN.md — GAP: Tier-4 maintainer live re-smoke (B5-B8 + Checklist C incl C3 + Checklist D); sign-off gates closure (autonomous:false) [PROD-W2-WS/PRT, RESID-03/04] (Wave 7, gap_closure)
- [x] 15-19-PLAN.md — GAP: Particle `Preview in client` no-hook candor reachable (btnPreview enabled-on-doc + click surfaces LOCKED copy, no retrigger) — fixes B6 [PROD-W2-PRT] (Wave 8, gap_closure)
- [x] 15-20-PLAN.md — GAP: pure `LogicalAssetPath` helper (12 facts) wired into the SaveLooseOverride sites so raw-`Open…` loose overrides preserve the logical subpath — fixes D-ii [RESID-03, PROD-W2-PRT] (Wave 8, gap_closure)
- [x] 15-21-PLAN.md — GAP: reassemble + content-verify bin/Release with the B6+D-ii fixes; live re-verify both (windows-mcp) + final Maintainer Sign-Off — Phase 15 CLOSED approved-with-deferred-residual (autonomous:false) [PROD-W2-PRT, RESID-03, RESID-04, PROD-W2-WS] (Wave 9, gap_closure)
**Research flag**: yes — `.prt` codec format depth is MEDIUM-confidence; `swg-client-v2` is the spec reference but no Utinni fixtures exist yet (`/gsd:plan-phase --research-phase 15`).
**UI hint**: yes

### Phase 16: Live-injected MCP bridge + Blender ecosystem boundary
**Milestone**: v2.0 — "AI-Assisted SWG Tools"
**Goal**: The ambitious, explicitly-last increment — let an AI agent drive the LIVE-injected client over an MCP bridge to preview an edit in-client, via a NEW named-pipe IPC hop into the x86 host (the biggest new-mechanism risk; never host the SDK in-proc). In parallel (it has no hard dependency and could run earlier as its own track), formalize the Utinni ↔ `swg-blender-plugin` boundary as a documented file-format / `.rsp` search-path contract — pure documentation plus reuse of existing readers, honoring DEC-A3 (no 3D authoring).
**Depends on**: Phase 14 (reuse the headless MCP tool schema + ergonomics before adding the live hop). The Blender boundary (ECO-01) depends on nothing and may be pulled earlier into its own track if convenient.
**Requirements**: MCP-03, ECO-01
**Constraint guard-rails**: The live bridge crosses to the x86 in-proc client ONLY via a narrow named-pipe IPC — the modern-.NET MCP host stays out-of-proc (Anti-Pattern 1). The live-patch tier is gated/opt-in (infra-ready but user-disabled today). The Blender boundary is a file-format seam (a directory of files + an `.rsp` manifest), NOT a process/library coupling — Utinni reads, Blender writes, neither imports the other; `swg_pipeline/rsp_builder.py` is the reference.
**Success Criteria** (what must be TRUE):
  1. An AI agent can drive the live-injected client over the MCP bridge (named-pipe IPC into the x86 host) to preview an edit in-client (MCP-03).
  2. The live bridge runs the MCP host out-of-proc and crosses to the injected client only via the named pipe — the SDK is never hosted inside SWG.exe.
  3. The Utinni ↔ `swg-blender-plugin` boundary is formalized as a documented `.iff`/`.tre` format-version + `.rsp` search-path contract, with open/preview verbs for Blender exports and no runtime coupling in either direction (ECO-01).
  4. `UtinniCoreDotNet` (C#) and the Blender side cross-validate against shared golden fixtures, confirming the file-format seam holds.
**Plans**: 3 plans (2 waves; ECO-01 and the MCP-03 in-client track parallelize in Wave 1; the MCP-03 host wiring lands in Wave 2)
**Plans**:
- [~] 16-01-PLAN.md — ECO-01 Blender boundary: contract doc (`.rsp`/version-matrix/bundle-layout/anti-coupling, D-06) + thin `validate-bundle` CLI verb + pinned cross-validation fixtures/golden + cross-repo pointer note (autonomous:false) [ECO-01] (Wave 1) — Tasks 1-3 DONE (1cf0415/d6c60c6/9b0f9ee); Task 4 (cross-repo pointer note) at blocking-human checkpoint. Cross-validation finding CV-1 surfaced (Blender crc-first vs Utinni size-first v0005 TOC).
- [x] 16-02-PLAN.md — MCP-03 in-client half: managed `LivePipeServer` in `UtinniCoreDotNet` (background accept loop + game-thread `mainLoopCallQueue` marshal + `ReloadAssetClassifier` ack tier) + shared protocol/pipe-name + `ServerArgs.EnableLive` fail-closed flag + pure-managed Wave-0 tests [MCP-03] (Wave 1)
- [x] 16-03-PLAN.md — MCP-03 host half: `LivePipeClient` (CliDispatcher twin, never-hang) + `live_ping`/`live_reload_asset` tools + D-04 conditional `WithTools<LiveTools>()` gating + loopback protocol test + MCP-SECURITY.md live-tier addendum [MCP-03] (Wave 2)
**Research flag**: addressed — 16-RESEARCH.md resolved the IPC mechanism (managed `System.IO.Pipes` server in the already-hosted CLR; the pipe is the arch boundary, no in-proc SDK).

## V2 Scope Boundary

Explicitly deferred to V1's V2 boundary; called out here so the V1 boundary is clear. (Several of these are now in-scope for **v2.0** — see Phases 12–16 above; the items below remain deferred.)

- **Tier 3 mock-D3D9 + recorded fixtures** (REQ-V2-tier-3-mock-d3d9) — V1 covers the D3D9 detour regression surface via the documented Tier 4 manual residual (TEST-04). Tier 3 is a separate effort, still deferred (revisit if MCP-03 / editor live-preview testing demands it).
- **Remaining Wave-2 plugins** (REQ-V2-wave-2-plugins) — Conversation, Quest, Buildout, UI Page, Shader, and the Terrain editor (`PROD-W2-TRN`, v2.1). v2.0 ships only WorldSnapshot + Particle (Phase 15). **MAY ship as standalone plugins (not TJT subpanels);** DEC-C4's "subpanel inside TJT" choice is V1-scoped.
- **Wave-3 plugins** (REQ-V2-wave-3-plugins) — Mod Manager, Packager, Community Hub, Asset Diff. **Same subpanel-vs-standalone re-open as Wave-2.**
- **Broader live-preview reload paths** (REQ-V2-live-preview-edits) — V1 piggybacks on whatever reload paths Wave-1 subpanels need; broader live-preview beyond Phase 16's bridge is later.
- **Author-new-content workflow** (REQ-V2-author-new-content) — beyond v2.0's compile/build slice.
- **One-click packaging + community hub** (REQ-V2-one-click-package, REQ-V2-share-to-hub) — Wave-3 work, later.
- **Promote IFF primitives from TJT to UtinniCore framework** (REQ-V2-iff-framework-promotion, derived from DEC-C4 V1 scope-fence) — Phase 8 ships IFF read/write inside `TheJawaToolboxDotNet`/`TheJawaToolbox`. If a third-party plugin ecosystem needs IFF parsing, promote those primitives up into UtinniCore. Pure code-motion refactor at that point; the V1 subpanels just rebase their `using` directives.

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
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 (V1, no decimal insertions) → 12 → 13 → 14 → 15 → 16 (v2.0).

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. CI + Tier 1 C# scaffold | 0/2 | Not started | - |
| 2. Critical bug burn-down (C-01..C-15) | 0/TBD | Not started | - |
| 3. Strategic reworks (R-A..R-H) | 0/TBD | Not started | - |
| 4. Tier 2 CLI shim + golden fixtures | 0/TBD | Not started | - |
| 5. Tier 1 C++ unit tests | 0/TBD | Not started | - |
| 6. Cleanups, dep bumps, open questions, Tier 4 doc, 1.0 cut | 5/6 | In Progress|  |
| 7. Wave-1 plugin — TRE Browser | 0/6 | Planned | - |
| 8. Wave-1 plugin — IFF Editor | 6/7 | In Progress|  |
| 9. Wave-1 plugin — Datatable Editor | 1/7 | In Progress|  |
| 10. Wave-1 plugin — String-table Editor | 2/6 | In Progress|  |
| 11. Wave-1 plugin — Object Template Editor | 5/5 | Complete    | 2026-06-01 |
| 12. Revive-feasibility spike (HARD GATE) + intro-skip crash | 0/TBD | Not started | - |
| 13. Wrap compilers as CLI verbs + OT Tier-2 | 6/6 | Complete   | 2026-06-05 |
| 14. Headless MCP server (`Utinni.Mcp`) | 5/5 | Complete   | 2026-06-07 |
| 15. Wave-2 editors (WorldSnapshot, Particle) + residuals | 19/21 | In Progress|  |
| 16. Live-injected MCP bridge + Blender boundary | 3/3 | Complete   | 2026-06-14 |

## Backlog

Unsequenced ideas parked for a future milestone (999.x). Promote with `/gsd:review-backlog`.

### Phase 999.1: MCP server for Utinni (BACKLOG)

**Goal:** Expose Utinni's asset pipeline as an MCP server so Claude/agents can drive the verified, byte-exact codecs programmatically — e.g. "find every creature template inheriting `shared_humanoid` and bump scale", "decode this datatable, edit rows, repack the .tre" — without hand-editing binaries.
**Requirements:** TBD
**Plans:** 0 plans

> **Note (2026-06-01):** This backlog idea is now the basis for v2.0 Phases 14 (headless MCP) + 16 (live-injected MCP). Retained here as the original capture; promote/close via `/gsd:review-backlog` once the v2.0 phases are planned.

**Context (captured 2026-05-31, post-V1):**
- **Foundation already exists:** `Utinni.Cli` emits structured JSON over `UtinniCoreDotNet` (`decode-iff`, `parse-tre`, `roundtrip-ot/tab/stf`, `list-objects`, …). The MCP server is largely a thin shim over those verbs / the library.
- **Read tools:** `tre_list` / `tre_extract`, `iff_decode`, `datatable_read`, `stringtable_read`, `object_template_resolve` (the DERV effective-inheritance view).
- **Write tools:** `object_template_edit` / `datatable_edit` / `stf_edit` → `save` (loose-override / repack), routed through the existing byte-exact writers + path-traversal defenses so an agent physically can't corrupt an archive.
- **Honest limits carry over:** CF-05 reload candor, OT multi-chunk raw-fallback, V6000 enumerate-only.
- **Scope fork:** headless file-pipeline MCP (no running client — ~90% of the value, easy V-next starting point) **vs.** live-injected tooling (scene load / live patch — needs the injection layer, bigger lift). Headless is the obvious first slice.

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)

### Phase 999.2: User-definable IFF chunk templates (BACKLOG)

**Goal:** Let a modder *describe the binary layout* of an arbitrary IFF chunk (a schema of primitives, colors, vectors, quaternions, matrices, arrays, structs) and have Utinni auto-decode/display/edit it — so modders can crack `.iff` formats Utinni doesn't natively support, without code changes.
**Requirements:** TBD
**Plans:** 0 plans

**Context (captured 2026-06-02, from the Sytner's IFF Editor (SIE) comparison):**
- SIE's standout power feature: user-defined chunk templates auto-applied to matching chunks. Utinni today only decodes the formats it hardcodes (datatable/stf/object-template); generic/unknown `.iff` chunks fall back to hex.
- **Why it's high-leverage:** turns Utinni from "the formats we coded" into "any format a modder can describe." Schema-driven decode is also **MCP-friendly** — an agent could be handed (or derive) a schema and read/edit an unknown chunk via a tool.
- Composes on the existing `UtinniCoreDotNet/Formats/Iff` reader + `IffPayloadCursor`; the new piece is a schema model + a schema-driven decode/encode pass + a UI to define/manage templates.
- Source: SIE feature comparison (Mod the Galaxy "About SIE"); see the v2.0 research / `docs/ai/toolchain-inventory.md` context.

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)

### Phase 999.3: TRE override / version history view (BACKLOG)

**Goal:** Show, for any logical path, every version of that file across the whole `.tre`/`.toc` load order — and let the modder open/extract/diff any historical version — i.e. a "what overrode what" patch-stack view for debugging load order.
**Requirements:** TBD
**Plans:** 0 plans

**Context (captured 2026-06-02, from the SIE comparison):**
- SIE works from a *repository* of `.tre`/`.toc` files (not one archive) and can show/extract/open any version of a file in the override history. Utinni's TRE Browser browses archives but does not surface the cross-archive override chain.
- **Why it matters:** load-order/override debugging is a top modder pain point ("which `.tre` is actually winning for this path?"). A diff between the base and the override is the natural payoff.
- Composes on `TreArchiveIndex` (already resolves logical paths across the load order) + `TrePayloadResolver`; the new piece is exposing the full per-path resolution chain (not just the winner) + a versions/diff UI.
- Source: SIE feature comparison (Mod the Galaxy "About SIE").

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)
