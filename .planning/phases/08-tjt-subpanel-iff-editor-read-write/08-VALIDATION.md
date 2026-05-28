---
phase: 8
slug: tjt-subpanel-iff-editor-read-write
status: ready
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-27
---

# Phase 8 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from 08-RESEARCH.md § Validation Architecture (Test Framework, Phase Requirements → Test Map,
> Sampling Rate, Wave 0 Gaps) plus 08-CONTEXT.md locked decisions D-02/D-05/D-06.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (managed) — the existing `UtinniCoreDotNet.Tests` (framework unit lane) + `Utinni.Cli.Tests` (CLI golden lane). Both target net472, x86 (matches the rest of the solution). |
| **Config file** | `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` (net472, x86, `AppendPlatformToOutputPath=false`) + `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` |
| **Quick run command** | `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~IffWriter|FullyQualifiedName~TreWriter|FullyQualifiedName~IffEditController"` (per-plan filter — narrow to the lane the just-edited task lives in) |
| **Full suite command** | `dotnet test --no-build` (after a VS2026 MSBuild Release; `MEMORY: dotnet-build-msbuild-resources` — `dotnet build` cannot compile WinForms .resx, so build with MSBuild and test with `--no-build`) |
| **Estimated runtime** | Quick filter ~5-15s; full managed suite ~60-90s on the self-hosted runner |

---

## Sampling Rate

- **After every task commit:** Run the lane-narrowed quick filter above (`FullyQualifiedName~<TestClass>`); the per-task `<verify><automated>` lines in each PLAN already pin the exact filter (e.g. 08-01 Task 2 uses `~IffWriter`; 08-02 uses `~Roundtrip`; 08-07 uses `~TreWriter`).
- **After every plan wave:** Run `dotnet test --no-build` (the full managed suite — both UtinniCoreDotNet.Tests and Utinni.Cli.Tests).
- **Before `/gsd:verify-work`:** Full suite must be green AND the Tier-4 live-SWG smoke observations (Plans 08-05 / 08-06 / 08-07) must be recorded in their SUMMARYs.
- **Max feedback latency:** Quick filter < 30 seconds; full suite < 120 seconds on the self-hosted runner.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 08-01-01 | 01 | 1 | PROD-W1-IFF | T-08-03 | Mutable DOM stays a sibling — never mutates IffDocument/IffLeafChunk (D-07 verbatim re-emit invariant) | unit (compile) | `msbuild Utinni.sln /t:UtinniCoreDotNet /p:Configuration=Release` | ❌ Wave 0 (this task creates the types) | ⬜ pending |
| 08-01-02 | 01 | 1 | PROD-W1-IFF | T-08-01 / T-08-02 / T-08-03 | 64 MB MaxChunkSize cap reused from IffReader; bottom-up length roll-up; NO pad byte on round-trip | unit | `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~IffWriter"` | ❌ Wave 0 (this task creates IffWriterTests.cs) | ⬜ pending |
| 08-01-03 | 01 | 1 | PROD-W1-IFF | — | ROADMAP doc-only reconciliation (D-01) | doc grep | `grep -v '^#' .planning/ROADMAP.md \| grep -c "UtinniCoreDotNet/Formats/Iff"` | ✅ ROADMAP exists | ⬜ pending |
| 08-02-01 | 02 | 2 | PROD-W1-IFF | T-08-04 / T-08-05 | Bounded exit codes; byteExact: true gate for Success Criterion 4 | golden / CLI | `dotnet test Utinni.Cli.Tests --no-build --filter "FullyQualifiedName~Roundtrip"` | ❌ Wave 0 (this task creates the verb + fixtures) | ⬜ pending |
| 08-03-01 | 03 | 2 | PROD-W1-IFF | T-08-06 | Read API of TreDetailPane preserved (D-09 — Phase 7 still works read-only) | unit (compile + grep) | `msbuild Utinni.sln /t:"The Jawa Toolbox\TheJawaToolboxDotNet" /p:Configuration=Release` (cross-repo) + `grep -c "LoadIff" .../UI/Controls/TreDetailPane.cs` | ✅ TreDetailPane exists | ⬜ pending |
| 08-04-01 | 04 | 3 | PROD-W1-IFF | T-08-10 | Editor-local undo/redo independent of scene UndoRedoManager (D-08 / CON-M-05) | unit + grep gate | `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~IffEditControllerTests"` (new — see Task 4) + `grep -c "AddUndoCommand\|UndoRedoTitlebarButton\|UndoRedoManager" .../Editing/IffEditController.cs` (must be 0) | ❌ Wave 0 (IffEditController + tests new this plan) | ⬜ pending |
| 08-04-02 | 04 | 3 | PROD-W1-IFF | — | WinForms gotchas (Size before SplitterDistance; Dock.Fill front; no .resx) | compile + grep | `msbuild` (TheJawaToolbox) + `grep -c "class FormIffEditor" .../UI/Forms/FormIffEditor.cs` | ❌ Wave 0 (new this plan) | ⬜ pending |
| 08-04-03 | 04 | 3 | PROD-W1-IFF | T-08-08 / T-08-09 | Hex validation surfaces Color.Red copy, never silently drops | compile + grep | `grep -c "Replace bytes from file\|Rename / retag\|Add FORM" .../UI/Forms/FormIffEditor.cs` | ❌ Wave 0 | ⬜ pending |
| 08-04-04 | 04 | 3 | PROD-W1-IFF | T-08-10 | Apply→Undo restores state; Apply→Undo→Redo restores edit; each D-03 op independently undoable; dirty propagates to root | unit | `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~IffEditControllerTests"` | ❌ Wave 0 (this task creates IffEditControllerTests.cs via linked-source pattern) | ⬜ pending |
| 08-05-01 | 05 | 4 | PROD-W1-IFF | T-08-11 / T-08-12 | Loose-override dir derived from injected client config (D-05.1); off-UI-thread write | compile + grep | `grep -c "ResolveClientTreDir\|looseOverrideDir" .../Saving/IffSaveTargets.cs` | ❌ Wave 0 (new this plan) | ⬜ pending |
| 08-05-02 | 05 | 4 | PROD-W1-IFF | T-08-13 | Tiered reload on game thread (textures → terrain → scene-change → candid copy) | compile + grep | `grep -c "AddMainLoopCall\|ReloadTextures\|ReloadTerrain" .../Saving/ClientReloadDispatcher.cs` | ❌ Wave 0 | ⬜ pending |
| 08-05-03 | 05 | 4 | PROD-W1-IFF | — | Plugin.cs try/catch isolation preserved; MEF SPI unchanged | compile + grep | `grep -c "FormIffEditor" .../Plugin.cs` | ✅ Plugin.cs exists | ⬜ pending |
| 08-05-04 | 05 | 4 | PROD-W1-IFF | T-08-11 / T-08-13 | Live load + edit + save + reload (Success Criteria 1 + 2 maintainer sign-off) | manual smoke (Tier 4) | live-SWG injection per `<how-to-verify>`; record observation in SUMMARY | n/a — maintainer-in-loop | ⬜ pending |
| 08-06-01 | 06 | 5 | PROD-W1-IFF | T-08-14 / T-08-15 | Mapped-memory write ALWAYS via Memory.memory.Copy on game thread (CON-N-04 VirtualProtect bracket; never UI thread) | compile + grep | `grep -c "AddMainLoopCall.*memory\.Copy\|memory\.Copy" .../Saving/LivePatchSaveTarget.cs` (≥1) AND `grep -c "Marshal\.Copy\|WriteProcessMemory\|VirtualProtect" .../Saving/LivePatchSaveTarget.cs` (must be 0 — no hand-rolled bracket bypass) | ❌ Wave 0 (new this plan) | ⬜ pending |
| 08-06-02 | 06 | 5 | PROD-W1-IFF | T-08-16 | Confirm dialog uses explicit-verb buttons + Color.Red emphasis; states volatility | compile + grep | `grep -c "class FormSaveConfirmDialog" .../UI/Forms/FormSaveConfirmDialog.cs` | ❌ Wave 0 | ⬜ pending |
| 08-06-03 | 06 | 5 | PROD-W1-IFF | T-08-14 | Live patch in-session visible; volatile (lost on reload) | manual smoke (Tier 4) | live-SWG injection; record observation | n/a | ⬜ pending |
| 08-07-01 | 07 | 6 | PROD-W1-IFF | T-08-17 / T-08-18 | Untouched entries re-emitted byte-for-byte; stored TreRecord.Checksum preserved (NO path-CRC recompute — Open Q1/A1); MaxBlockSize 256 MB inflate cap retained | unit / harness | `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~TreWriter"` | ❌ Wave 0 (new this plan) | ⬜ pending |
| 08-07-02 | 07 | 6 | PROD-W1-IFF | T-08-19 | Atomic temp-then-replace; opt-in backup; failed write never truncates the archive | compile + grep | `grep -c "TreWriter\|TreRepackSaveTarget" .../Saving/TreRepackSaveTarget.cs` | ❌ Wave 0 | ⬜ pending |
| 08-07-03 | 07 | 6 | PROD-W1-IFF | T-08-17 / T-08-19 | Client resolves the repacked archive incl. untouched entries (no CRC/TOC resolution failure — Pitfall 5); Phase 8 consolidated criteria 1-5 sign-off | manual smoke (Tier 4) | live-SWG injection per `<how-to-verify>`; record observation + criteria sign-off in SUMMARY | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Sampling-continuity audit (no 3 consecutive tasks without automated verify)

Tasks 08-05-04, 08-06-03, 08-07-03 are the Tier-4 manual smokes. They are NOT consecutive in execution order — each is the final task of its own plan and each is bracketed by automated tasks in the prior/next plans (08-05-01..03 before; 08-06-01..02 between; 08-07-01..02 between). No 3-in-a-row manual run.

---

## Wave 0 Requirements

- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Iff/IffWriterFixtures.cs` + `IffWriterTests.cs` (created in 08-01 Task 2; mirrors `IffReaderFixtures.cs` / `IffReaderTests.cs`)
- [ ] `Utinni.Cli/Commands/RoundtripIffCommand.cs` (created in 08-02 Task 1)
- [ ] `Utinni.Cli.Tests/Commands/RoundtripIffCommandTests.cs` + `Fixtures/iff/roundtrip/*.{iff,expected.json}` (created in 08-02; mirrors `InspectIffCommandTests` + the existing `odd-chunk-no-pad.*` shape)
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Iff/IffEditControllerTests.cs` (created in 08-04 Task 4 — links `IffEditController.cs` from the UtinniPlugins sibling repo via `<Compile Include="..\..\UtinniPlugins\The Jawa Toolbox\TheJawaToolboxDotNet\Editing\IffEditController.cs" Link="Editing\IffEditController.cs" />`, mirroring the existing `SlnDirResolver.cs` / `HeaderDiscovery.cs` / `Props.cs` linked-source pattern in `UtinniCoreDotNet.Tests.csproj`)
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Tre/TreWriterTests.cs` (created in 08-07 Task 1 — links `Utinni.Cli.Tests/Infrastructure/TreFixtureBuilder.cs` via the same `<Compile Include>` linked-source pattern)

**Framework install:** None — xUnit 2.9.3 + Microsoft.NET.Test.Sdk are already in both test csprojs; CommandLineParser + Newtonsoft.Json are already in `Utinni.Cli.csproj`.

**Set `wave_0_complete: true` in this file's frontmatter after 08-01 Task 2 + 08-02 Task 1 + 08-04 Task 4 + 08-07 Task 1 ship (the four Wave-0 test scaffolds above).**

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| IFF Editor subpanel loads in TJT against a live SWG client (Success Criterion 1) | PROD-W1-IFF | Requires an injected live SWGEmu/Restoration client; no CI runner can inject | 08-05 Task 4 `<how-to-verify>` (build TJT, inject, open IFF Editor from the TJT host) |
| Live load → edit → save → reload round trip (Success Criterion 2) | PROD-W1-IFF | Requires a live client to confirm the reload actually surfaces the edit | 08-05 Task 4 `<how-to-verify>` (open IFF via TRE Browser hand-off, edit a leaf, Save as loose override, trigger tiered reload, confirm edit visible) |
| In-memory live patch applies in-session (D-05.3) | PROD-W1-IFF | Mapped-client-memory write only meaningful with a running client | 08-06 Task 3 `<how-to-verify>` (inject, edit, `Save ▾ → Patch live client`, confirm dialog, verify the change in-session and that it is lost on reload) |
| `.tre` repack: client resolves the repacked archive incl. untouched entries (D-05.4; Pitfall 5 path-CRC risk gate) | PROD-W1-IFF | Live archive resolution can only be observed in an injected client; the path-CRC algorithm is unverified (Open Q1) — sidestepped by preserving stored Checksum, but the live resolution gate proves the sidestep holds | 08-07 Task 3 `<how-to-verify>` (open IFF from packed `.tre`, repack with backup on, atomic replace, reload, confirm the client resolves the archive AND every untouched entry) |
| D-06 scene-change-style reload matrix per asset class (RESEARCH Open Q3) | PROD-W1-IFF | "Does a scene change re-read datatables/templates/STF?" can only be answered in a live client; outcome documented in `TESTING.md` for future reuse | 08-05 Task 4 includes a smoke matrix: try one of each asset class (texture / terrain / shader / datatable / STF / template), record which reload tier surfaces the edit |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies (verified: each `<verify><automated>` line in 08-01..08-07 is either an existing command or backed by a Wave 0 scaffold above)
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify (verified above — the three Tier-4 manual tasks are non-consecutive in execution order)
- [ ] Wave 0 covers all MISSING references (the four Wave-0 scaffolds above cover 08-01 Task 2, 08-02 Task 1, 08-04 Task 4, 08-07 Task 1)
- [ ] No watch-mode flags (all commands use `--no-build` with explicit `--filter`; no `-w`/`--watch`)
- [ ] Feedback latency < 120s (quick filter < 30s; full suite < 120s on the self-hosted runner)
- [ ] `nyquist_compliant: true` set in frontmatter ✓

**Approval:** approved 2026-05-27 (planner — populated from 08-RESEARCH.md § Validation Architecture per checker B-1; pending sign-off carries forward to `wave_0_complete: true` after Wave-0 scaffolds ship)
