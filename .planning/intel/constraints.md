# Constraints

> Distilled from `docs/ai/assessment.md`, `docs/ai/vision.md`, and `docs/ai/test-harness-plan.md`. Constraints fall into four families:
> 1. **Preservation constraints** — 24 load-bearing design elements that must not be refactored away (negative constraints).
> 2. **Scope constraints** — anti-goals from the vision that delimit what Utinni will and will not do.
> 3. **Technical / platform constraints** — Windows-only, x86 in-process, DXSDK June 2010, etc.
> 4. **Open / unresolved constraints** — questions whose answer will shape downstream decisions.

---

## 1. Preservation constraints (from assessment.md "Solid foundations")

These are explicitly flagged "don't refactor". Treat as hard negative constraints during 1.0 work.

### Native architecture (9)

- **CON-N-01** `swg::<subsystem>` detour-table pattern (`using pX = ...; pX x = (pX)0xRVA;` + optional `Detour::Create` slot swap). Uniform, greppable, single-table find/replace for RVA churn.
  - Source: assessment.md
  - Type: preservation
- **CON-N-02** `utinni::` thin-wrapper firewall over `swg::*`. Right separation between RE'd SWG and CLR/plugin-callable surface.
- **CON-N-03** Mid-function naked trampolines (`midPopCell`, `midCrashLogWrite`, `midCtor`) with `pushad`/`popad` register save.
- **CON-N-04** `utility/memory::copy` / `createJMP` bracket every write with `VirtualProtect` save/restore.
- **CON-N-05** Launcher's suspended-process + EP-park (`EB FE`) + `CreateRemoteThread(LoadLibraryA)` + OEP-restore — textbook implementation.
- **CON-N-06** `imgui_impl` device-loss handling (`isSetup` guard, invalidate/recreate on `hkReset`).
- **CON-N-07** `Game::loadScene`'s two-frame state machine (`loadNewScene` + `sceneCleaned` ping-pong).
- **CON-N-08** `PluginManager` pImpl idiom keeping STL out of the DLL boundary.
- **CON-N-09** `spdlog::sinks::base_sink<std::mutex>` for the `OutputSink`.

### Managed architecture (9)

- **CON-M-01** `IPlugin` / `IEditorPlugin` interfaces — minimal, friendly, null returns from `GetForms()` etc. are a clean low-coupling SPI.
- **CON-M-02** `[InheritedExport]` for MEF discovery — plugin authors just implement the interface.
- **CON-M-03** `WorldSnapshotCommands` copy-on-construct (`new WorldSnapshotReaderWriter.Node(node)`) — captures state at command-creation time.
- **CON-M-04** `HotkeyManager`'s `CreateSettings` / `Load` / `Save` triplet against `UtINI`.
- **CON-M-05** `UndoRedoManager.OnCleanupCallback` clearing both stacks on scene-cleanup — prevents undoing into a dead world. (Note: the `UndoRedoManager` itself has bug C-07; the *cleanup-callback* is the preserved design.)
- **CON-M-06** `UtinniForm`'s custom title bar via `OnPaint` + `WM_NCHITTEST` regions.
- **CON-M-07** `Log.AddOutputSinkCallback` pattern (typo `AddOuputSink` is a separate fix item) — UI-agnostic fanout; `FormLog` correctly marshals via `BeginInvoke`.
- **CON-M-08** `PanelGame.PanelGame_Layout` re-calling `Client.SetHwnd(Handle)` on every layout — subtle but correct for re-parenting.
- **CON-M-09** `FormObjectBrowser` drag-drop orchestration (preview-object follows cursor, ray-cast via `cui_hud.CollideCursorWithWorld`, commit on drop) — to be documented as canonical.

### Process / tooling (5)

- **CON-T-01** `UtinniCore.vcxproj` post-build chain (copy `data/` then run `UtinniCoreDotNetGen.exe`).
- **CON-T-02** `RelWithDbgInfo` configuration plumbed end-to-end across all five projects + templates + examples.
- **CON-T-03** Two-language template parity (C++ + .NET runtime + .NET editor).
- **CON-T-04** `Props.cs` factoring — centralised plugin MSBuild boilerplate in one wizard-emitted file.
- **CON-T-05** Jawa Toolbox `*Impl` separation pattern — promote to canonical (see D-06 in decisions.md).

Total: 23 preservation items as enumerated in assessment.md (the assessment's count of 24 includes promotion-to-canonical of the `*Impl` pattern; rendered here as a single item).

---

## 2. Scope constraints (from vision.md "Anti-goals")

Hard negative scope. Reject features that violate these without explicit re-scoping.

- **CON-S-01 — Not a server-side mod manager.** SWG-Source / swg-main handle server scripting and data. Integrate with their conventions; never own them.
  - Source: vision.md
  - Type: scope (anti-goal)
- **CON-S-02 — Not a launcher / patcher.** SWGEmu's launcher and community launchers exist. Utinni is the editor, not the day-to-day play client.
- **CON-S-03 — Not Maya / 3ds Max.** Hand off to DCCs for mesh/animation/texture authoring. Plug into export pipelines; do not reinvent them.
- **CON-S-04 — Not a multiplayer-cheat enabler.** All editing is local-asset / offline-scene work. Live shards may detect and reject modified clients — that is accepted.

Wave-4-equivalent items the vision flags as "realistically the territory of separate tools that Utinni can hand off to, not absorb" — Mesh authoring, Animation authoring, Texture authoring, server-side Script editing — fall under CON-S-03/CON-S-01 and are treated as out-of-scope unless re-scoped.

---

## 3. Technical / platform constraints

### Platform

- **CON-P-01 — Windows-only.** Utinni is a Windows-only desktop application. Test infrastructure runs on GitHub Actions Windows runners; there is no Linux fallback.
  - Source: test-harness-plan.md
- **CON-P-02 — In-process injection into x86 SWG.exe.** UtinniCore is a 32-bit DLL injected into the live client. All native build configs are `x86`.
  - Source: assessment.md (multiple)
- **CON-P-03 — DXSDK June 2010 dependency.** UtinniCore currently depends on the legacy DXSDK for `d3dx9.h` math helpers. (Open question — see CON-O-08 below — whether this can be replaced with Windows 10 SDK's d3d9 headers.)
  - Source: assessment.md

### Build / toolchain

- **CON-B-01 — VS 2019+VS 2022 contributor support required.** Bug C-12 pins VSIX to `[16.0,17.0)`; the constraint going forward is `[16.0,18.0)`.
  - Source: assessment.md (C-12)
- **CON-B-02 — Windows SDK target version must be unified across `.vcxproj` files.** Currently inconsistent (`10.0` / `10.0.19041.0` / `10.0.16299.0`); pick one in shared `Directory.Build.props`.
  - Source: assessment.md (cleanups)
- **CON-B-03 — DXSDK include/lib paths must work in all configurations.** Currently only `RelWithDbgInfo`; Debug/Release fail silently without `DXSDK_DIR`.
  - Source: assessment.md (cleanups)
- **CON-B-04 — CRT compatibility across the C++/C# bridge.** Cross-CRT `delete[]` (C-02) and cross-CRT plugin destruction (R-B) are class-of-bug constraints: every cross-boundary allocation/free must use the originator's allocator.
  - Source: assessment.md (C-02, R-B)

### Hooking / detour

- **CON-H-01 — DllMain MUST NOT do heavy startup.** Microsoft explicitly forbids `LoadLibrary` + CLR bring-up inside `DLL_PROCESS_ATTACH`. Defer to first SWG callback (`Game::install`) or a separate `CreateRemoteThread` to an exported `utinni_init`.
  - Source: assessment.md (C-01)
- **CON-H-02 — Pattern-scan results must be null-checked before use.** `findPattern` returning 0 + `memcpy` from address `0x2` is a crash class (C-11).
  - Source: assessment.md (C-11)
- **CON-H-03 — Hard-coded RVAs must have a single source of truth.** `0x00AA0970` (SWG WndProc) and the two `isSafeToUse` flag addresses are duplicated between native and managed today; expose via `UTINNI_API` (R-C).
  - Source: assessment.md (R-C)
- **CON-H-04 — Callback subscriber lists must be safe under concurrent dispatch + mutation.** Snapshot under lock before iterating (R-H). Native callback vectors and managed `SynchronizedCollection<T>` both apply.
  - Source: assessment.md (R-H)
- **CON-H-05 — Symmetric `Add` / `Remove` is required for every callback.** Plugins that subscribe then dispose continue firing callbacks against dead controls otherwise (R-A).
  - Source: assessment.md (R-A)

### Plugin lifecycle

- **CON-L-01 — Plugin ABI must be symmetric.** `createPlugin` paired with `destroyPlugin`; never `delete` across a CRT boundary (R-B).
  - Source: assessment.md (R-B)
- **CON-L-02 — `plugin->init()` must actually be invoked after the load loop.** Declared today but never called (R-B).
  - Source: assessment.md (R-B)
- **CON-L-03 — Plugin load failures must be isolated and logged.** One bad plugin must not tear down the editor (C-06). Wrap per-plugin in its own `AssemblyCatalog` with try/catch; log offending DLL + `ReflectionTypeLoadException.LoaderExceptions[*]`.
  - Source: assessment.md (C-06)
- **CON-L-04 — Plugin-side exceptions must not bubble through framework callbacks.** Bug class — `Hotkey.ProcessString` throwing on a typo'd `input.ini` token tears the editor down via the C-06 compounding (C-08). Constraint: framework code parsing plugin config must use `TryParse`-style fallible APIs.
  - Source: assessment.md (C-08)

### Testing

- **CON-TT-01 — TDD applies to pure-logic and file-format layers only.** Native detours, in-process injection, and WinForms UI use smoke/integration tests; explicit Tier-4 residual is accepted.
  - Source: test-harness-plan.md
- **CON-TT-02 — Fixture storage TBD.** In-repo vs Git LFS for binary TRE samples is unresolved (see CON-O-09).
  - Source: test-harness-plan.md
- **CON-TT-03 — FlaUI WinForms automation deliberately skipped.** Too flaky; not a current investment.
  - Source: test-harness-plan.md

### Distribution / defaults

- **CON-D-01 — `data/utinni.cfg` ships with blank server host/port.** Avoid defaulting users into any specific shard's infrastructure (potential ToS issue). C-14 is the current bug; the constraint is the policy going forward.
  - Source: assessment.md (C-14)

---

## 4. Open / unresolved constraints

These will become hard constraints once answered. From assessment.md "Open questions":

- **CON-O-01 — `isSafeToUse` operator.** Code at `game.cpp:307` uses `||`; documentation says `&&`. One is a bug.
- **CON-O-02 — Was `AddPostDrawLoopCall` ever actually used?** Broken since 2020 (C-04). If never used, fix is trivial; if used, downstream code may rely on broken behaviour.
- **CON-O-03 — Native delegate corruption smell.** "Very odd bug … storing this in a variable prevents corruption" comment in `GameCallbacks.cs:46` likely indicates a GC-collected delegate passed to unmanaged without `GCHandle.Alloc`. Original repro would confirm the fix.
- **CON-O-04 — VS 2019 pin rationale.** Was there a real reason (compiler bug with x86 + CLR hosting?) or just history? Drives confidence in widening to VS 2022 (C-12).
- **CON-O-05 — `StdEdited.cs` curation criteria.** What is hand-maintained vs auto-generated?
- **CON-O-06 — LeksysINI replacement plan.** README says "temporary, will most likely be replaced" — what was intended?
- **CON-O-07 — Sytner's plugin status.** Code elsewhere never merged, or aspirational?
- **CON-O-08 — DXSDK June 2010 vs Windows 10 SDK.** Could DXSDK be replaced? DXSDK has `d3dx9.h`; Windows SDK lacks it. Check if Utinni actually uses `d3dx9` math helpers.
- **CON-O-09 — Test fixture storage.** In-repo (small) vs Git LFS (binary TRE samples can be big).
  - Source: test-harness-plan.md
- **CON-O-10 — Test project layout.** Single `Utinni.Tests.sln` vs per-project `*.Tests` folders.
  - Source: test-harness-plan.md
- **CON-O-11 — CLI shim distribution.** Public artifact, or test-harness-internal only?
  - Source: test-harness-plan.md

---

## Notes for the roadmapper

- Preservation constraints (Section 1) are the strongest signal here. Every phase in ROADMAP.md should reference them as guard-rails ("does this phase touch any CON-N-*, CON-M-*, CON-T-* item? If yes, justify.").
- Scope constraints (Section 2) are the right input to the project's PROJECT.md `<anti-goals>` block (if your template has one) or to a top-of-doc scope statement.
- Open questions (Section 4) should be resolved as part of the relevant phases — most are inline with specific bug fixes (CON-O-01..-03 with C-04/C-07/C-08; CON-O-04 with C-12; CON-O-08 with DXSDK modernisation). CON-O-09/-10/-11 are best resolved in the testing phase.
