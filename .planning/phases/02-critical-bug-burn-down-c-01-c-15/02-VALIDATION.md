---
phase: 02
slug: critical-bug-burn-down-c-01-c-15
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-16
---

# Phase 02 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `02-RESEARCH.md` §Validation Architecture (max-harness posture per CONTEXT.md D-04..D-07).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (pinned, Phase 1) |
| **Config file** | `UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` (no `.runsettings`) |
| **Quick run command** | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` |
| **Full suite command** | `msbuild Utinni.sln /m /restore /p:Configuration=Release /p:Platform=x86 /p:RestorePackagesConfig=true && dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build --configuration Release` |
| **Estimated runtime** | ~10 s quick (per task); ~6 min full (per wave) |

---

## Sampling Rate

- **After every task commit:** Run quick command (project-targeted `dotnet test --no-build`, ~10 s).
- **After every plan wave:** Run full suite (`msbuild + dotnet test`, ~6 min).
- **Before `/gsd:verify-work`:** Full suite must be green on CI on `master`.
- **Max feedback latency:** 10 s per commit; 6 min per wave.

---

## Per-Task Verification Map

> One row per C-NN bug. `Test Type`: unit (managed xUnit), integration (P/Invoke or process-isolated), partial-proof (harness covers fix surface but full proof requires Tier 4 manual), manual (no automation possible).

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 02-01-C04 | 01 | 1 | STAB-01-C04 | — | `DequeuePostDrawLoopCalls` drains its own queue (not foreign queue); `Drain(ConcurrentQueue<Action>)` helper used by both queue sites | unit | `dotnet test --filter FullyQualifiedName~GroundSceneCallbacksTests` | ❌ W0 | ⬜ pending |
| 02-01-C06 | 01 | 1 | STAB-01-C06 | — | Broken plugin does NOT tear down editor; surviving plugins still load; broken plugin name surfaces in log | unit + fixture | `dotnet test --filter FullyQualifiedName~PluginLoaderTests` | ❌ W0 (test + 2 fixture csprojs) | ⬜ pending |
| 02-01-C08 | 01 | 1 | STAB-01-C08 | — | `Hotkey.ProcessString` does not throw on malformed input; multi-modifier parses | unit | `dotnet test --filter FullyQualifiedName~HotkeyTests` | ✅ (Phase 1 skipped tests unskip + new MalformedModifier case) | ⬜ pending |
| 02-01-C12 | 01 | 1 | STAB-01-C12 | — | VSIX `InstallationTarget Version="[16.0,18.0)"` (VS 2019 + VS 2022) | unit (XML assert) | `dotnet test --filter FullyQualifiedName~VsixManifestTests` | ❌ W0 | ⬜ pending |
| 02-01-C13 | 01 | 1 | STAB-01-C13 | — | TJT Debug build outputs to `bin/Debug/`; `.sln` restores `Debug\|Win32.Build.0` entry | manual (cross-repo) | `cd UtinniPlugins && msbuild "The Jawa Toolbox/TheJawaToolbox.sln" /p:Configuration=Debug /p:Platform=x86 && ls Utinni/bin/Debug/Plugins/TheJawaToolbox` | N/A (cross-repo) | ⬜ pending |
| 02-01-C14 | 01 | 1 | STAB-01-C14 | — | `data/utinni.cfg` ships with `loginServerAddress0=` and `loginServerPort0=` (blank per CON-D-01) | unit (file-content) | `dotnet test --filter FullyQualifiedName~UtinniCfgTests` | ❌ W0 | ⬜ pending |
| 02-02-C02 | 02 | 2 | STAB-01-C02 | — | Cross-CRT `delete[]` removed; SWG buffer freed by SWG-side allocator wrapper | partial-proof | `dotnet test --filter FullyQualifiedName~ConfigBufferFreeTests` | ❌ W0 (partial proof; full = Tier 4 manual) | ⬜ pending |
| 02-02-C03 | 02 | 2 | STAB-01-C03 | — | `Network::cast` post-condition wrapper returns non-uninit value | partial-proof | `dotnet test --filter FullyQualifiedName~NetworkCastTests` | ❌ W0 (partial proof; live cast = Tier 4 manual) | ⬜ pending |
| 02-02-C05 | 02 | 2 | STAB-01-C05 | — | `GameDragDropEventHandlers` subscriber receives synthesized `DragEventArgs` | unit (WinForms fixture) | `dotnet test --filter FullyQualifiedName~GameDragDropEventHandlersTests` | ❌ W0 | ⬜ pending |
| 02-02-C07 | 02 | 2 | STAB-01-C07 | — | `UndoRedoManager` thread-safe under concurrent push; `AllowMerge` gate called before `Merge`; `RedoCommands.Clear` after merge check (TD-29) | unit | `dotnet test --filter FullyQualifiedName~UndoRedoManagerTests` | ❌ W0 | ⬜ pending |
| 02-02-C10 | 02 | 2 | STAB-01-C10 | — | `clr::stop()` is idempotent — no AV on second call | integration (P/Invoke) | `dotnet test --filter FullyQualifiedName~Clr10HarnessTests` | ❌ W0 (test + native export) | ⬜ pending |
| 02-02-C11 | 02 | 2 | STAB-01-C11 | — | `findPattern` returns 0 on absent pattern; `getVtbl()` bails with `log::critical` rather than `memcpy` from `0x2`; CON-N-04 VirtualProtect bracket preserved | integration (P/Invoke) | `dotnet test --filter FullyQualifiedName~FindPatternHarnessTests` | ❌ W0 (test + native export) | ⬜ pending |
| 02-02-C15 | 02 | 2 | STAB-01-C15 | — | `ResolveSlnDir` pure function with three resolution modes (`\bin\` walk-up, `$(SolutionDir)` arg, env-var fallback) | unit | `dotnet test --filter FullyQualifiedName~CppSharpSlnDirTests` | ❌ W0 | ⬜ pending |
| 02-02-C16 | 02 | 2 | STAB-01-C16 | — | Delegate survives `GC.Collect()` between registration and dispatch; comment de-stalification on `GameCallbacks.cs:46` | integration (P/Invoke + GC.Collect) | `dotnet test --filter FullyQualifiedName~GameCallbacksTests` | ❌ W0 (test + native test-only export `Game::triggerInstallCallbacks`) | ⬜ pending |
| 02-02-KB05 | 02 | 2 | STAB-01-KB05 | — | `isSafeToUse` uses `&&` per internals.md:218-231 (default-fallback per D-12) | manual + commit-time grep | `grep '||' UtinniCore/swg/game/game.cpp:307` returns nothing | N/A (code review) | ⬜ pending |
| 02-03-C01 | 03 | 3 | STAB-01-C01 | — | `DllMain` returns < 50 ms (no heavy startup); CLR bring-up deferred per CON-H-01; success criterion #4 | partial-proof (process-isolated timing) | `dotnet test --filter FullyQualifiedName~LoaderLockHarnessTests` | ❌ W0 (test + `Utinni.LoaderLockHarness` sibling project + `utinni_init` export) | ⬜ pending |
| 02-04-C09 | 04 | 3 | STAB-01-C09 | — | `WaitForPresentBlock` returns within timeout; no `Thread.Sleep(1)` spin observed; Win32 `CreateEvent` + `EventWaitHandle` (no new NuGet) | unit (mock signaller) | `dotnet test --filter FullyQualifiedName~FormMainSignallerTests` | ❌ W0 (test + native `getPresentBlockedEvent` export) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

> Files/scaffolding that must exist before any C-NN fix can ship its red-before-green regression test.

### New xUnit test files (UtinniCoreDotNet.Tests/)
- [ ] `GroundSceneCallbacksTests.cs` — covers STAB-01-C04 (`DequeuePostDrawLoopCalls` queue drain)
- [ ] `GameCallbacksTests.cs` — covers STAB-01-C16 (delegate-pinning + `GC.Collect()` survival)
- [ ] `GameDragDropEventHandlersTests.cs` — covers STAB-01-C05 (WinForms panel fixture)
- [ ] `PluginLoaderTests.cs` — covers STAB-01-C06 (broken-plugin isolation)
- [ ] `UndoRedoManagerTests.cs` — covers STAB-01-C07 (thread-safety + AllowMerge + Clear ordering)
- [ ] `Clr10HarnessTests.cs` — covers STAB-01-C10 (idempotent `clr::stop()`)
- [ ] `FindPatternHarnessTests.cs` — covers STAB-01-C11 (`findPattern` + `getVtbl` bail)
- [ ] `VsixManifestTests.cs` — covers STAB-01-C12 (XML version range assert)
- [ ] `UtinniCfgTests.cs` — covers STAB-01-C14 (blank-login file assert)
- [ ] `CppSharpSlnDirTests.cs` — covers STAB-01-C15 (`ResolveSlnDir` pure function)
- [ ] `LoaderLockHarnessTests.cs` — covers STAB-01-C01 (process-isolated DllMain timing)
- [ ] `FormMainSignallerTests.cs` — covers STAB-01-C09 (mock signaller)
- [ ] `ConfigBufferFreeTests.cs` — covers STAB-01-C02 (partial proof — testable free wrapper)
- [ ] `NetworkCastTests.cs` — covers STAB-01-C03 (partial proof — post-condition wrapper)

### Fixture projects (UtinniCoreDotNet.Tests/Fixtures/)
- [ ] `Fixtures/BrokenPlugin/BrokenPlugin.csproj` + source — deliberately-broken plugin DLL (throws in `IPlugin.Initialize`)
- [ ] `Fixtures/GoodPlugin/GoodPlugin.csproj` + source — companion good plugin (loads next to broken one)

### Sibling project (Utinni.sln addition)
- [ ] `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj` — native C++ helper exe (`net472` not applicable; x86, `OutputType=Exe`, depends on `UtinniCore`)
- [ ] `Utinni.LoaderLockHarness/main.cpp` — `LoadLibraryA("UtinniCore.dll")` bracketed by `QueryPerformanceCounter`; exits 0 if `<50 ms`, exits 1 otherwise

### Native test-only exports (UtinniCore.dll)
- [ ] `utinni_clr_stop` (C-10 P/Invoke target)
- [ ] `utinni_findPattern` (C-11 P/Invoke target)
- [ ] `utinni_getVtbl` (C-11 P/Invoke target)
- [ ] `Game::triggerInstallCallbacks` (C-16 P/Invoke target — triggers a delegate after a forced GC pass)
- [ ] `getPresentBlockedEvent` (C-09 — also production code; exports the `HANDLE` from `CreateEvent`)

### Production-code seam
- [ ] `utinni_init` export added to `UtinniCore.dll` (C-01 fix surface — invoked by launcher via `CreateRemoteThread`)
- [ ] `[assembly: InternalsVisibleTo("UtinniCoreDotNet.Tests")]` added to `UtinniCoreDotNet/Properties/AssemblyInfo.cs` (enables `Drain`, `WaitForPresentBlock`, `ResolveSlnDir` private-method tests)

### Solution wiring (Utinni.sln)
- [ ] Add `Fixtures/BrokenPlugin/BrokenPlugin.csproj` entry
- [ ] Add `Fixtures/GoodPlugin/GoodPlugin.csproj` entry
- [ ] Add `Utinni.LoaderLockHarness/Utinni.LoaderLockHarness.vcxproj` entry (x86, Release + Debug configurations)

### Framework deps
- [ ] `System.Windows.Forms` ref added to `UtinniCoreDotNet.Tests.csproj` (for C-05 `Panel` fixture and C-09 `FormMain` test)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| TJT Debug build outputs to correct path | STAB-01-C13 | UtinniPlugins has no CI workflow as of Phase 2; cross-repo build cannot run from this repo's CI | `git -C ../UtinniPlugins pull && cd ../UtinniPlugins && msbuild "The Jawa Toolbox/TheJawaToolbox.sln" /p:Configuration=Debug /p:Platform=x86 && Test-Path "Utinni/bin/Debug/Plugins/TheJawaToolbox.dll"` |
| VS 2019 + VS 2022 install of `UtinniPluginTemplates.vsix` | STAB-01-C12 | Cross-IDE template install automation is Phase-6+ ergonomics, not Phase-2 cost | Build VSIX locally; `vsixinstaller.exe UtinniPluginTemplates.vsix` against VS 2019 IDE then VS 2022 IDE; document `Verified-by:` block in commit message |
| Live `Network::cast` against SWG client | STAB-01-C03 | The cast invokes SWG at hard-coded RVA `0xAA4900`; harness covers post-condition wrapper only | Attach to running SWG client; trigger a network event that flows through `Network::cast`; observe no uninitialised-read in debugger watch |
| Full proof of "no deadlock under loader-lock contention" | STAB-01-C01 | Loader-lock contention requires a second thread doing `LoadLibrary` during DllMain; deterministic repro is Tier 4 | Inject `UtinniCore.dll` into SWG client via launcher; observe game window appears within 2 s; no hang in Task Manager → Resource Monitor → CPU → Waiting Chain |
| Cross-CRT free does not corrupt heap | STAB-01-C02 | True CRT-mismatch detection requires _CrtCheckMemory + a known-bad allocator fixture; out-of-scope automation cost | Page Heap (`gflags /p /enable utinni.exe`) on dev workstation; run editor through `swg/misc/config` load path; observe no heap-corruption STATUS code |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (14 of 17 automated; 3 manual; 4 partial-proof with automated harness + listed Tier 4 manual)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify (all 4 plans interleave automated tests with manual checkpoints)
- [x] Wave 0 covers all MISSING references (14 test files + 2 fixture projects + 1 sibling project + 6 native exports + 1 `InternalsVisibleTo`)
- [x] No watch-mode flags (`dotnet test` runs once, exits)
- [x] Feedback latency < 10 s per commit; < 6 min per wave
- [ ] `nyquist_compliant: true` set in frontmatter — pending plan-checker pass

**Approval:** pending (set to `approved 2026-05-16` after plan-checker green-lights all four plans)
