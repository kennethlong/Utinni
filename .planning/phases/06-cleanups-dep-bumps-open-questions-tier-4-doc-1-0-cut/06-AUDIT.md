# STAB-04 Preservation Audit — v1.0-rc.1

**Phase:** 06 · **Plan:** 06-05 Task 4 · **Decision:** D-18
**Audited against:** `a3e3bd3` (Task 3 tip; the dead-code purge + cleanups sweep)
**Automated by:** `UtinniCoreDotNet.Tests/PreservationAudit/PreservationAuditTests.cs`
(23 fail-on-violation xUnit Facts) + `RepoRoot.cs`.

## Methodology

STAB-04 requires confirming the 23 load-bearing "Solid foundations" enumerated in
`.planning/intel/constraints.md` (CON-N-01..09 native, CON-M-01..09 managed, CON-T-01..05
process/tooling) are still intact at the 1.0-rc.1 cut — i.e. that the 1.0 cleanup work
(Phases 2–6) did not refactor any of them away.

Each item below records: **(a)** the constraint, **(b)** the audit procedure (the grep / file
probe that confirms it), **(c)** the result with evidence, and **(d)** the SHA it was last
verified against. Every item is backed by a `[Fact]` that **fails the build** if the
foundation is violated — "warn-only is the wrong shape" (Phase 4 D-11). CI runs these on every
push to master, so a post-1.0 regression turns the lane red at the introducing commit.

Two items have a cross-repo component (the orchestration lives in
`kennethlong/UtinniPlugins`): CON-M-09 (drag-drop) is audited via its Utinni-side ray-cast
primitive; CON-T-05 (TheJawaToolbox `*Impl` separation) is a soft check that validates on a
local dev box and is document-only on the CI runner. All 23 items: **PASS** at `a3e3bd3`.

---

## Native architecture (CON-N-01..09)

### CON-N-01 — `swg::<subsystem>` detour-table pattern
- **Constraint:** uniform `using pX = ...; pX x = (pX)0xRVA;` RE'd-pointer tables, greppable for single-table RVA churn.
- **Procedure:** count files under `UtinniCore/swg/` matching `\(p[A-Z][A-Za-z0-9_]*\)0x[0-9A-Fa-f]{5,}`.
- **Result:** PASS — 59 files match (threshold ≥30). Fact `CON_N_01_DetourTablePattern_Present`.
- **Last verified:** `a3e3bd3`.

### CON-N-02 — `utinni::` thin-wrapper firewall over `swg::*`
- **Constraint:** the public facade does not leak `swg::*` includes; RE'd code is wrapped before reaching the CLR/plugin surface.
- **Procedure:** assert `UtinniCore/utinni.h` contains **no** `#include "swg/...`; assert `object.cpp` keeps both `namespace swg` and `namespace utinni`.
- **Result:** PASS — utinni.h has zero `swg/` includes; object.cpp keeps the dual namespaces. Fact `CON_N_02_ThinWrapperFirewall_Intact`.
- **Last verified:** `a3e3bd3`.

### CON-N-03 — mid-function naked trampolines
- **Constraint:** `midPopCell` / `midCrashLogWrite` / `midCtor` style `__declspec(naked)` trampolines with `pushad`/`popad`.
- **Procedure:** count `UtinniCore` source matching `__declspec\(naked\)|midPopCell|midCrashLogWrite|midCtor`.
- **Result:** PASS — present (≥1). Fact `CON_N_03_NakedMidTrampolines_Present`.
- **Last verified:** `a3e3bd3`.

### CON-N-04 — `memory::copy` / `createJMP` VirtualProtect bracket
- **Constraint:** every patch write is bracketed by a `VirtualProtect` save/restore.
- **Procedure:** assert `UtinniCore/utility/memory.cpp` contains ≥2 `VirtualProtect` calls.
- **Result:** PASS. Fact `CON_N_04_VirtualProtectBracket_Present`.
- **Last verified:** `a3e3bd3`.

### CON-N-05 — Launcher suspended-process injection flow
- **Constraint:** suspended-process + EP-park (`EB FE`) + `CreateRemoteThread(LoadLibraryA)` + OEP-restore.
- **Procedure:** assert `Launcher/main.cpp` contains `CreateRemoteThread`, `LoadLibraryA`, and the `0xEB, 0xFE` EP-park patch.
- **Result:** PASS — all three present. Fact `CON_N_05_LauncherInjectionFlow_Intact`.
- **Last verified:** `a3e3bd3`.

### CON-N-06 — imgui device-loss handling (`isSetup` guard)
- **Constraint:** `imgui_impl` guards setup/device-loss with `isSetup`.
- **Procedure:** assert `UtinniCore/swg/ui/imgui_impl.cpp` contains `isSetup`.
- **Result:** PASS. Fact `CON_N_06_ImguiIsSetupGuard_Present`.
- **Last verified:** `a3e3bd3`.

### CON-N-07 — `Game::loadScene` two-frame state machine
- **Constraint:** `loadNewScene` + `sceneCleaned` ping-pong.
- **Procedure:** assert `UtinniCore/swg/game/game.cpp` contains both `loadNewScene` and `sceneCleaned`.
- **Result:** PASS. Fact `CON_N_07_LoadSceneStateMachine_Intact`.
- **Last verified:** `a3e3bd3`.

### CON-N-08 — pImpl idiom keeps STL out of the DLL boundary
- **Constraint:** `PluginManager` and `UtINI` use the forward-declared `struct Impl; Impl* pImpl{}` idiom.
- **Procedure:** assert both `UtinniCore/plugin_framework/plugin_manager.h` and `UtINI/utini.h` contain `struct Impl;` and `Impl* pImpl`.
- **Result:** PASS — both present. Fact `CON_N_08_PimplIdiom_Present`.
- **Last verified:** `a3e3bd3`.

### CON-N-09 — `OutputSink` `base_sink<std::mutex>`
- **Constraint:** the spdlog `OutputSink` derives from `base_sink<std::mutex>`.
- **Procedure:** assert `UtinniCore/utility/log.cpp` contains `base_sink<std::mutex>`.
- **Result:** PASS. (Also fenced by 06-02's `OutputSinkRoundTripTests` across the spdlog 1.14 bump.) Fact `CON_N_09_OutputSinkBaseSink_Present`.
- **Last verified:** `a3e3bd3`.

---

## Managed architecture (CON-M-01..09)

### CON-M-01 — `IPlugin` / `IEditorPlugin` minimal SPI
- **Constraint:** small, friendly SPI; `null` returns from `GetForms()` etc. are clean low-coupling.
- **Procedure:** assert `IPlugin.cs` declares `interface IPlugin` + `GetConfig`; `IEditorPlugin.cs` declares `GetForms` + `GetSubPanels`.
- **Result:** PASS. (IEditorPlugin exposes `GetForms` / `GetStandalonePanels` / `GetSubPanels` — no `GetEditors`; the constraint's "etc." is satisfied.) Fact `CON_M_01_PluginSpiShape_Intact`.
- **Last verified:** `a3e3bd3`.

### CON-M-02 — `[InheritedExport]` MEF discovery
- **Constraint:** plugin interfaces carry `[InheritedExport]` so authors just implement the interface.
- **Procedure:** assert `IPlugin.cs` or `IEditorPlugin.cs` contains `InheritedExport`.
- **Result:** PASS. Fact `CON_M_02_InheritedExport_Present`.
- **Last verified:** `a3e3bd3`.

### CON-M-03 — `WorldSnapshotCommands` copy-on-construct
- **Constraint:** commands capture state at creation via `new WorldSnapshotReaderWriter.Node(node)`.
- **Procedure:** assert `WorldSnapshotCommands.cs` contains `new WorldSnapshotReaderWriter.Node(`.
- **Result:** PASS (two callsites). Fact `CON_M_03_WorldSnapshotCopyOnConstruct_Present`.
- **Last verified:** `a3e3bd3`.

### CON-M-04 — `HotkeyManager` CreateSettings / Load / Save triplet
- **Constraint:** the triplet against `UtINI`.
- **Procedure:** assert `HotkeyManager.cs` contains `CreateSettings`, `Load`, `Save`.
- **Result:** PASS. Fact `CON_M_04_HotkeyManagerTriplet_Present`.
- **Last verified:** `a3e3bd3`.

### CON-M-05 — `UndoRedoManager.OnCleanupCallback`
- **Constraint:** clears both undo/redo stacks on scene-cleanup (prevents undoing into a dead world).
- **Procedure:** assert `UndoRedoManager.cs` contains `OnCleanupCallback`.
- **Result:** PASS. Fact `CON_M_05_UndoRedoCleanupCallback_Present`.
- **Last verified:** `a3e3bd3`.

### CON-M-06 — `UtinniForm` custom title bar
- **Constraint:** custom title bar via `OnPaint` + `WM_NCHITTEST` regions.
- **Procedure:** assert `UtinniForm.cs` contains `WM_NCHITTEST` and `OnPaint`.
- **Result:** PASS. Fact `CON_M_06_UtinniFormCustomTitlebar_Intact`.
- **Last verified:** `a3e3bd3`.

### CON-M-07 — `Log.AddOutputSinkCallback` fanout
- **Constraint:** UI-agnostic log fanout (the `AddOuputSink` typo was a separate, already-fixed item).
- **Procedure:** assert `Log.cs` contains `AddOutputSinkCallback`.
- **Result:** PASS. Fact `CON_M_07_LogOutputSinkCallback_Present`.
- **Last verified:** `a3e3bd3`.

### CON-M-08 — PanelGame owns SWG-window placement *(evolved)*
- **Constraint (original):** `PanelGame_Layout` re-calling `Client.SetHwnd(Handle)` on every layout.
- **Disposition:** the `SetHwnd`-on-every-layout mechanism was **superseded** by the owned-popup
  reparenting introduced in Issue #9/#10 (`PanelGame.cs:136` records the removal). The *foundation*
  — PanelGame owns the placement of the live SWG window — is **preserved** via the newer
  `ReparentSwgWindow` + reparent-poll mechanism. This is the one foundation whose implementation
  changed since the assessment; it is intact in spirit, evolved in mechanism.
- **Procedure:** assert `PanelGame.cs` contains `ReparentSwgWindow` and `PanelGame_Layout`.
- **Result:** PASS. Fact `CON_M_08_PanelGameOwnsSwgWindow_Intact`.
- **Last verified:** `a3e3bd3`.

### CON-M-09 — drag-drop orchestration *(cross-repo; Utinni-side primitive audited)*
- **Constraint:** `FormObjectBrowser` drag-drop (preview follows cursor, ray-cast via
  `cui_hud.CollideCursorWithWorld`, commit on drop). `FormObjectBrowser` itself lives in
  `kennethlong/UtinniPlugins` (TheJawaToolbox).
- **Procedure:** audit the Utinni-side primitive the orchestration depends on — assert
  `UtinniCore/swg/ui/` exposes `CollideCursorWithWorld`. Full orchestration is verified by the
  Tier-4 drag-drop residual (TESTING.md) and the live-SWG smoke.
- **Result:** PASS — the ray-cast primitive is present in cui_hud. Fact `CON_M_09_DragDropRaycastPrimitive_Present`.
- **Last verified:** `a3e3bd3`.

---

## Process / tooling (CON-T-01..05)

### CON-T-01 — UtinniCore post-build chain
- **Constraint:** post-build copies `data/` then runs `UtinniCoreDotNetGen.exe`.
- **Procedure:** assert `UtinniCore.vcxproj` contains a `PostBuildEvent` with `xcopy` and `UtinniCoreDotNetGen.exe`.
- **Result:** PASS. Fact `CON_T_01_PostBuildChain_Intact`.
- **Last verified:** `a3e3bd3`.

### CON-T-02 — `RelWithDbgInfo` triple-config end-to-end
- **Constraint:** Debug + Release + RelWithDbgInfo plumbed across the five core projects + templates + examples.
- **Scope note:** the invariant covers `UtinniCore`, `UtINI`, `Launcher`, `UtinniCore-Symbols`,
  `UtinniCore.Tests`, and the C++ example (`ExampleCppPlugin`). The Phase-3 R-B test fixtures
  (`Utinni.CrtMatchPlugin`, `Utinni.LegacyPlugin`) and the Phase-6 `Utinni.LoaderLockHarness` are
  test-only Debug+Release projects — deliberately **not** triple-config and outside CON-T-02's
  "five projects + templates + examples" scope. The C++ plugin template
  (`UtinniCppPluginTemplate`) uses tokenised VS config placeholders rather than literal
  `ProjectConfiguration` entries, so it is covered by CON-T-03 parity rather than a literal grep here.
- **Procedure:** for each of the six in-scope projects, assert ≥3 `ProjectConfiguration Include` entries and the presence of `RelWithDbgInfo`.
- **Result:** PASS — all six in-scope projects keep the triple-config (3 configs each). Fact `CON_T_02_TripleConfig_Intact`.
- **Last verified:** `a3e3bd3`.

### CON-T-03 — two-language template parity
- **Constraint:** C++ + .NET plugin templates ship side by side.
- **Procedure:** assert `sdk/UtinniCppPluginTemplate/` (C++) and `sdk/UtinniPluginTemplates/DotNetPluginTemplate/` (.NET) exist.
- **Result:** PASS. Fact `CON_T_03_TwoLanguageTemplateParity_Present`.
- **Last verified:** `a3e3bd3`.

### CON-T-04 — `Props.cs` factoring
- **Constraint:** centralised plugin MSBuild boilerplate in one wizard-emitted file.
- **Procedure:** assert `sdk/UtinniPluginTemplates/Vsix/Utility/Props.cs` exists.
- **Result:** PASS. Fact `CON_T_04_PropsCsFactoring_Present`.
- **Last verified:** `a3e3bd3`.

### CON-T-05 — Jawa Toolbox `*Impl` separation *(cross-repo; soft check + document-only on CI)*
- **Constraint:** TheJawaToolbox promotes the `*Impl.cs` separation pattern to canonical (D-06).
  This lives entirely in `kennethlong/UtinniPlugins`, which is **not** checked out on the
  Utinni CI runner.
- **Procedure:** when the sibling `../UtinniPlugins` repo is present (local dev box), assert
  TheJawaToolbox contains ≥1 `*Impl.cs` file; on the CI runner (sibling absent) the check passes
  and the invariant is recorded here (document-only, per planner discretion in D-18).
- **Result:** PASS — verified locally; `TheJawaToolboxDotNet/SWG/` ships `GroundSceneImpl.cs`,
  `PlayerObjectImpl.cs`, `WorldSnapshotImpl.cs`. Fact `CON_T_05_JawaToolboxImplSeparation_Documented`.
- **Last verified:** `a3e3bd3` (Utinni) + UtinniPlugins `c9cfa9d`.

### Evolved and cross-repo dispositions

Not every foundation is a verbatim grep against the original assessment text; three warrant an
explicit disposition so the PASS results above are not over-read:

- **CON-M-08 (evolved):** the original SetHwnd-on-every-layout mechanism was deliberately
  replaced by owned-popup reparenting in Issue #9/#10. The foundation (PanelGame owns SWG-window
  placement) is intact; the audit verifies the current mechanism (`ReparentSwgWindow`).
- **CON-M-09 (cross-repo orchestration):** `FormObjectBrowser` drag-drop lives in
  `kennethlong/UtinniPlugins`. The audit verifies the Utinni-side ray-cast primitive it depends
  on (`cui_hud.CollideCursorWithWorld`); the full orchestration is a Tier-4 residual + live-SWG smoke.
- **CON-T-05 (cross-repo):** TheJawaToolbox `*Impl` separation lives in UtinniPlugins (not checked
  out on the CI runner). The Fact validates it when the sibling repo is present and is
  document-only otherwise.

---

## Summary

| Family | Items | Result | Automated Facts |
|--------|-------|--------|-----------------|
| Native (CON-N-01..09) | 9 | 9 PASS | 9 |
| Managed (CON-M-01..09) | 9 | 9 PASS (CON-M-08 evolved; CON-M-09 cross-repo primitive) | 9 |
| Process/tooling (CON-T-01..05) | 5 | 5 PASS (CON-T-05 cross-repo soft check) | 5 |
| **Total** | **23** | **23 PASS** | **23** |

All 23 load-bearing foundations are intact at `a3e3bd3`. The grep tests fail the CI build if any
regresses; a deliberate-violation probe demonstrating fail-on-violation is recorded in
`06-05-SUMMARY.md`.
