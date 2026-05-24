# Phase 6: Cleanups, dep bumps, open questions, Tier 4 doc, 1.0 cut - Context

**Gathered:** 2026-05-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Final pre-plugin sweep before the framework 1.0 cut. Phase delivers five inter-locked workstreams:

1. **STAB-05 open-question closure** — CON-O-06 (LeksysINI replaced with a ~200-LOC custom INI parser inside `UtINI/utini.cpp`'s PIMPL Impl) and CON-O-08 (DXSDK June 2010 fully removed; `depth_texture.cpp`'s sole `D3DXVECTOR3` usage replaced with a local 3-float struct; DXSDK include/lib paths stripped from every `.vcxproj` — side-effect closes CON-B-03 structurally). The other six CON-O-* questions are already dispositioned by Phases 2/3/4 per assessment.md.
2. **STAB-03 dep bumps + toolchain modernisation** — Full vcpkg migration (7 deps: catch2, CppSharp, DetourXS, nvapi, imgui, spdlog, ImGuizmo; LeksysINI excluded because it's being replaced, not bumped). imgui switches to the docking branch (gated on 06-01 overlay-debug; see below). spdlog 1.6 → 1.14 with CON-N-09 regression-test fence on `OutputSink`. PlatformToolset v142 → v145 (VS 2026 / Dev18). VSIX `[16.0,18.0)` → `[16.0,19.0)` per CON-B-01 audit-then-widen precedent.
3. **Overlay-debug investigation (folded in)** — User flagged that the imgui in-game overlay has never displayed in Utinni-injected sessions. Per [[feedback-d3d9-hook-diagnosis]], pattern-scan check first. Investigation lives in its own plan (06-01) and gates the imgui-docking branch switch in 06-02. Exit criterion: `ImGui::ShowDemoWindow` renders visibly over a live SWG client, exercising menus + sliders + buttons + tabs + plots + popups + drag-and-drop (full Demo). This is the highest bar of the four exit-criterion options because it proves render + input + state-mgmt end-to-end.
4. **STAB-03 cleanups** — ~30 enumerated cleanups from `docs/ai/assessment.md §Easy cleanups`. Full-repo `.clang-format` run as a single commit + `.git-blame-ignore-revs` entry. `TJT.ico` ejected from `UtinniCoreDotNet` to UtinniPlugins/TheJawaToolbox in paired commits (per [[feedback-utinniplugins-authority]]). Polish bundle: Windows SDK target unification (CON-B-02), `Prefer32Bit` removal, `ExampleEditorPlugin.csproj` Release output path fix, `.gitignore` Std/StdEdited convention doc, empty `namespace Std {}` deletion, `Native.SendMessage int → IntPtr`, licenses.txt additions (DetourXS + nvapi + mojibake `João`), typo fixes (`detatch → detach`, `redose → redoes`, stray semicolons). **Explicit exclusion:** `swg/ui/` commented-detour cleanups (cui_chat_window.cpp:166, cui_io.cpp:96, cui_hud.cpp:164,168, appearance.cpp:102-103) are SKIPPED entirely in Phase 6 because they sit adjacent to imgui_impl.cpp; per [[feedback-keep-scaffolding-wip]] we don't delete WIP-adjacent scaffolding before the overlay works.
5. **STAB-04 preservation audit + TEST-04 Tier-4 doc + 1.0 packaging + v1.0.0-rc.1 tag** — Audit each of the 24 CON-N-01..09 / CON-M-01..09 / CON-T-01..05 foundations via doc walk + automated grep checks (e.g., CON-N-01 detour-table pattern: grep for `pX x = (pX)0x` in `swg/*.cpp`; CON-T-02 triple-config: assert all `.vcxproj` have Debug+Release+RelWithDbgInfo). Tier-4 boundary doc lands at `.planning/codebase/TESTING.md` as a full residual enumeration (each scenario: procedure, success criterion, last-verified SHA). WiX MSI installer ships v1.0.0-rc.1 bundled with TJT (UtinniPlugins/TheJawaToolbox) per DEC-C4 "pair distribution"; installer offers an optional default-off SWG-client-path detection + utinni.cfg seed checkbox (preserves CON-D-01 "ships blank" via opt-in). Phase 6 ends with v1.0.0-rc.1 GitHub Pre-release; bake period + promotion to v1.0.0 happens post-phase as a follow-up.

Also folded into scope: two GSD CI-stability todos (`loader-lock-harness-flake-fix`, `gamecallbacks-gc-av-flake-fix`) because the 1.0 success criterion requires CI green on master. They land as their own dedicated plan, gated before the v1.0.0-rc.1 tag.

**In scope (this phase):**
- All five workstreams above.
- Cross-repo paired commits to `kennethlong/UtinniPlugins` for TJT.ico move + TJT bundling in MSI build workflow ([[feedback-utinniplugins-authority]] standing authority).
- Code-review at phase end (`/gsd:code-review 06`).
- Update `docs/ai/assessment.md` §"Status tracking" + §"Open questions" with dispositions for every Phase-6-closed item.

**Out of scope (this phase):**
- **`swg/ui/` commented-detour cleanups** — deferred entirely (see workstream 4 above). Carried forward to a post-overlay-fix phase.
- **`Add*Callback` vs `Add*Call` naming consolidation** — Phase 3 D-10 precedent: kept as wrappers for source-compat. Deferred to V2. Per [[feedback-caller-attrs-binary-compat]] cross-binary plugin DLLs break at MEF compose if signatures change.
- **`v1.0.0` final tag (non-rc)** — Phase 6 ends with `v1.0.0-rc.1`; promotion happens after bake period (no critical issues for N days, planner picks N=7-14).
- **Wave-1 plugins (TRE Browser, IFF Editor, Datatable, String-table, Object Template)** — Phases 7-11.
- **V2 work** — Wave-2/3 plugins, Tier 3 mock D3D9, full migration of remaining vendored deps (none left after Phase 6's full vcpkg migration), broader live-preview reload paths, mod packaging + community hub, etc.
- **Authenticode code signing of the MSI** — V1 ships unsigned (SmartScreen warning on first run is acceptable for OSS modding tool). Revisit in V2 once funding model is clearer.
- **MSIX / Inno Setup / InstallShield** — WiX picked as MSI authoring tool.
- **File-type associations (.tre/.iff/.tab/.stf/.otpl) in MSI** — Wave-1 plugins haven't shipped yet; associations would point to no-ops. Defer to V1 release at Phase 11 closure (or later).
- **Hybrid vcpkg adoption / leaving any dep vendored** — User locked "Full migration (7 deps)" answer including catch2 and CppSharp; CppSharp vcpkg-port quality is a planner-research risk that may force a fallback at plan time.
- **Spike on whether MSI ProductCode/UpgradeCode strategy needs locking** — planner discretion.

</domain>

<decisions>
## Implementation Decisions

### STAB-05 Open-Question Dispositions

- **D-01 (CON-O-08):** **DXSDK June 2010 removed cleanly.** Sole consumer is `UtinniCore/swg/graphics/depth_texture.cpp` (1 `#include <d3dx9.h>`, 1 `D3DXVECTOR3 vDummyPoint(0,0,0)` declaration, 1 `sizeof(D3DXVECTOR3)`). Replace with a local 3-float struct (`struct Vec3 { float x, y, z; };` or anonymous), drop the `d3dx9.h` include, strip DXSDK include/lib paths from every `.vcxproj` in the solution. Side effect: closes **CON-B-03** structurally — there is no longer any DXSDK path to set, so the "Debug/Release fail silently without DXSDK_DIR" failure mode is gone. Disposition note in `CONVENTIONS.md` (or a one-paragraph block in `docs/ai/assessment.md`): "D3DX9 math was retired in Phase 6; future GPU math should use DirectXMath from the Windows SDK." Update `docs/ai/assessment.md` §"Open questions" CON-O-08 with the resolved-pointer + commit SHA.

- **D-02 (CON-O-06):** **LeksysINI replaced with a ~200-LOC custom INI parser inside `UtINI/utini.cpp`'s `Impl`.** `UtINI` is PIMPL'd (CON-N-08-pattern, see `UtINI/utini.h:82-84`), so swap cost is contained inside `utini.cpp`; all ~15 callsites in Launcher/UtinniCore/UtinniCoreDotNet stay untouched. Hand-rolled parser owns: section headers `[Name]`, `key = value` pairs, quoted strings, leading/trailing whitespace trim, comments (`;` and `#`), sectionless keys, round-trip preservation (load → mutate → save without losing unrecognized lines). `external/leksysini/` deleted in the same commit; not migrated to vcpkg. README's "LeksysINI is temporary, will most likely be replaced" comment is removed (commitment finally honoured).

- **D-03:** **INI-parser tests live in Catch2 (Phase 5's `UtinniCore.Tests.exe`).** New file `UtinniCore.Tests/UtINI/IniParserTests.cpp`. Coverage: round-trip (load → mutate → save → reload, equality assertion); edge cases (quoted strings with embedded `=`, sectionless keys, `;` and `#` comments, comments preserved on save, empty values, repeated keys); negative cases (malformed sections, missing `=`, runaway quotes). Per [[feedback-max-harness]] — every test must fail if the parser were reverted to broken state.

- **D-04:** **Phase 6 disposition for all 8 CON-O-* questions documented in `docs/ai/assessment.md` §"Open questions".** Six were closed by Phases 2/3/4 (CON-O-01..05, -07). Phase 6 closes the last two (CON-O-06 + CON-O-08 per D-01/D-02 above). CON-O-09..11 are TEST-03 (Phase 4) and already closed. Final state: zero open CON-O-* at 1.0-rc tag.

### STAB-03 Dep Bumps + Toolchain

- **D-05:** **vcpkg adopted in manifest mode; full migration of all 7 remaining vendored deps.** `vcpkg.json` manifest at repo root; `vcpkg-configuration.json` for registry pin (likely a baseline-pinned commit of microsoft/vcpkg). CI adds a vcpkg install step (likely `microsoft/setup-vcpkg` or vendored bootstrap). Deps to migrate: **catch2** (just vendored in Phase 5 D-02 — Phase 6 is the deliberate "broader vcpkg call" Phase 5 explicitly deferred here; partial undo of Phase 5 work is accepted), **CppSharp** (build-time codegen tool; vcpkg-port quality is a known research risk — planner must verify the port exists and works before plan execution; fallback path: keep CppSharp vendored if port is broken), **DetourXS**, **nvapi**, **imgui** (docking branch port; see D-06), **spdlog** (1.14; see D-07), **ImGuizmo**. **LeksysINI excluded** because D-02 replaces it with a custom parser. **Migration risk noted in `<specifics>`.**

- **D-06:** **imgui switches to the docking branch (`docking` upstream branch maintained by ocornut) at the latest stable release-compatible commit.** Required for TJT Wave-1 dockable subpanels per DEC-C4. **GATED on 06-01 overlay-debug success** — the docking-branch switch lands ONLY after `ImGui::ShowDemoWindow` is confirmed working over a live SWG client. If 06-01 reveals base imgui rendering is fundamentally broken in our injection model, the planner may downgrade the bump to "imgui master 1.91" (no docking) and surface as a Phase-6 risk; if even master doesn't work, escalate (imgui bump may need to defer entirely).

- **D-07:** **spdlog 1.6.0 → 1.14 with regression-test fence on `OutputSink`.** CON-N-09 (`base_sink<std::mutex>` for `OutputSink`) is preserved by a Catch2 round-trip test (new `UtinniCore.Tests/Log/OutputSinkRoundTripTests.cpp`) that constructs a log message, attaches a recording sink, asserts the message reaches the recorder in the expected format. If the test fails, CON-N-09 has been violated; if the test passes after the spdlog bump, the foundation is preserved. Formatter API changes between 1.6 and 1.14 (fmt library migration) handled in the same commit; managed `[CallerMemberName]` logging from Phase 3 R-E (Log.FormatText) is unaffected because it lives on the managed side.

- **D-08:** **ImGuizmo bumped to latest stable.** Independent of imgui-bump risk (ImGuizmo is a small library; surface area in UtinniCore is the existing gizmo code in `swg/scene/`-adjacent files). Planner picks the target commit/tag during research.

- **D-09:** **PlatformToolset v142 → v145 (VS 2026 / Dev18).** Matches [[project-vs2026-toolchain]] memory (local default is VS 2026; VS 2022 fallback on disk). All `.vcxproj` files updated; CI's MSBuild step pins to v145 too. Contributors on VS 2022 need v145 build tools installed (Build Tools 2022 supports v145 via the C++ workload).

- **D-10:** **VSIX manifest range widens to `[16.0,19.0)` per CON-B-01 audit-then-widen precedent (Phase 2 C-12 disposition).** Keeps VS 2019 + VS 2022 + VS 2026 contributor support; matches CON-B-01's "VS 2019+VS 2022 required" + adds VS 2026.

### Overlay-Debug Investigation (folded into Phase 6 as 06-01)

- **D-11:** **06-01 = dedicated investigation plan; gates 06-02 dep-bumps.** Topic: "Why does the imgui in-game overlay never display in Utinni-injected sessions?" Per [[feedback-d3d9-hook-diagnosis]], **the d3d9.dll pattern-scan check is the 30-second first move BEFORE assuming SWG-side RVA drift or multi-day investigation.** Researcher walks: (a) pattern-scan validity in `directX::getVtbl` and detour install path (`UtinniCore/swg/graphics/depth_texture.cpp` + adjacent), (b) `imgui_impl.cpp` `isSetup` guard + `hkPresent` invocation, (c) ImGui::NewFrame/Render/EndFrame call ordering, (d) ImGui_ImplDX9_NewFrame + state save/restore around the SWG render. **Exit criterion:** `ImGui::ShowDemoWindow` renders visibly over a live SWG client, exercising menus + sliders + buttons + tabs + plots + popups + drag-and-drop end-to-end (full Demo screen). Tier-4 manual verification; documented in TEST-04 row at phase end. **If investigation reveals a deep architectural blocker** (e.g., SWG-side rendering pipeline incompatibility), planner escalates: imgui bump may downgrade, plan may extend, or 1.0-rc.1 may ship with overlay-broken disposition documented (least-preferred but possible).

### STAB-03 Cleanups + Build Polish

- **D-12:** **Full-repo `.clang-format` run as ONE atomic commit.** `.clang-format` file matches `CONVENTIONS.md` (Allman braces, 4-space indent, PascalCase, MIT header). Run `clang-format -i` across the entire C++ tree in one commit; ~10k+ line cosmetic diff stable forever after. Same commit adds `.git-blame-ignore-revs` with this commit's SHA so `git blame --ignore-revs-file` skips the bulk-reformat. CI gates new code via a `clang-format-check` step (existing diff stays untouched; new diff must conform). Lands AFTER 06-01 overlay-debug + 06-02 dep-bumps + 06-03 open-Qs to minimise rebase pain on those plans.

- **D-13:** **`Add*Callback` vs `Add*Call` naming consolidation DEFERRED to V2.** Phase 3 D-10 precedent: `Add*` retained as wrappers for source-compat. Per [[feedback-caller-attrs-binary-compat]] cross-binary plugin DLLs break at MEF compose if signatures change. Once UtinniPlugins ecosystem migration is done (V2), revisit.

- **D-14:** **Dead-code cleanup ordering rule.** Cleanups in **`swg/ui/`** (cui_chat_window.cpp:166, cui_io.cpp:96, cui_hud.cpp:164,168, appearance.cpp:102-103) are **SKIPPED ENTIRELY in Phase 6**. Per [[feedback-keep-scaffolding-wip]], don't delete WIP-adjacent scaffolding before the feature works; per D-11's overlay-debug investigation, some of these may turn out to be useful breadcrumbs for what the imgui overlay or input pipeline needs. Cleanups carry forward as "deferred pending post-overlay-fix audit" in `docs/ai/assessment.md`. **Other dead-code targets land normally:** `Launcher/main.cpp:33-172` `attachToVisualStudio` block, `utinni.cpp:71,75` commented detours, `swg/scene/render_world.cpp` `hkRender`/`hkClearVisibleCells` disabled experimental bodies, `swg/scene/client_world.cpp:46-58,63`, `swg/misc/io_win.cpp:50-57`, empty stub files `swg/appearance/particle.cpp` + `swg/scene/scene.cpp`.

- **D-15:** **`TJT.ico` ejected to UtinniPlugins in paired commits.** Source: `UtinniCoreDotNet/Resources/` (or wherever `UtinniForm`'s default icon currently resolves from). Target: `UtinniPlugins/TheJawaToolbox/Resources/`. `UtinniForm` default falls back to a **neutral framework icon** (generic gear, public-domain or original; planner discretion on the specific image). Cross-repo paired commits per [[feedback-utinniplugins-authority]] standing authority.

- **D-16:** **Polish bundle ships all items in one or two atomic commits.** Items: (a) Windows SDK target unification per CON-B-02 (`10.0` / `10.0.19041.0` / `10.0.16299.0` → one value in shared `Directory.Build.props`); (b) `UtinniCoreDotNetGen.csproj` `Prefer32Bit` removal (incoherent with `x64`); (c) `ExampleEditorPlugin.csproj:28` Release output path fix (`bin\Debug\Plugins\...` → `bin\Release\Plugins\...`); (d) `.gitignore` documentation block explaining Std.cs vs StdEdited.cs convention; (e) empty `namespace Std {}` blocks deleted from `Generated/StdEdited.cs`; (f) `Native.SendMessage int → IntPtr` — **planner must verify if `Native.SendMessage` is `internal` (no binary-compat impact, ship change) or `public` (add shim per [[feedback-caller-attrs-binary-compat]])**; (g) `licenses.txt` += DetourXS + nvapi entries + mojibake `Jo�o → João` fix; (h) typos: `void detatch()` → `void detach()` in `utinni.cpp:132`; `// Executes/redose` → `redoes` in `IUndoCommand.cs:31`; stray semicolons after `#include "utinni.h";`.

- **D-17:** **Folded CI-flake todos = dedicated plan.** Both `loader-lock-harness-flake-fix` (DllMain 50ms threshold flakes on shared windows-2022 runners; [[project-loader-lock-harness-ci-flake]]) and `gamecallbacks-gc-av-flake-fix` (RegisterCallback_ForceGCCollect AV under CI) land in a Phase-6 plan dedicated to CI stability. Lands AFTER 06-02 dep-bumps (vcpkg migration may touch the same workflow file) but BEFORE the 1.0-rc.1 tag plan (since 1.0 success criterion requires CI green on master). Each fix is atomic + has a regression test that would fail if reverted per [[feedback-max-harness]].

### STAB-04 Preservation Audit

- **D-18:** **STAB-04 audit = doc walk + automated grep checks.** Planner produces `06-AUDIT.md` (or extends the verification artifact) with a one-line "still intact at SHA <ref>" disposition for each of CON-N-01..09, CON-M-01..09, CON-T-01..05 (24 items total). Each line backed by an automated grep/script check in a new `Utinni.PreservationAudit.Tests` xUnit project (or extends the existing `UtinniCoreDotNet.Tests` per Phase 1/3/4 single-test-project convention with a new `PreservationAudit/` subfolder). Examples: CON-N-01 detour-table pattern → grep `pX x = (pX)0x` in `UtinniCore/swg/**/*.cpp`; CON-T-02 triple-config → assert every `.vcxproj` has `Debug + Release + RelWithDbgInfo`; CON-N-02 thin-wrapper firewall → grep that `utinni::` namespace headers only `#include` `utinni::` or pure-stdlib headers (no direct `swg::` includes); CON-M-01 `IPlugin` SPI shape → assert `PluginFramework/IPlugin.cs` exports `Initialize()`, `GetForms()`, `GetEditors()`, etc. CI runs these on every push so post-1.0 regressions get caught.

### TEST-04 Tier-4 Boundary Doc

- **D-19:** **TEST-04 doc = full residual enumeration extending `.planning/codebase/TESTING.md`** (per REQUIREMENTS.md §TEST-04 `.planning/codebase/TESTING.md or sibling`). Each row: scenario, manual procedure, success criterion, last-verified SHA. Enumeration: (a) imgui overlay rendering (06-01 exit-criterion procedure becomes the canonical Tier-4 row); (b) PanelGame.WndProc forwarding to live SWG (R-C precedent from Phase 3 D-06); (c) hkPresent + MMO render lifecycle; (d) D3D9 device-loss/reset paths (CON-N-06 preservation context); (e) plugin loader against real plugin DLLs (TJT, future Wave-1 plugins); (f) drag-drop in editor + WinForms STA (post-Phase 02.1 WR-09); (g) GPU-driver-specific bugs; (h) WinForms UI smoke (FlaUI explicitly EXCLUDED per CON-TT-03 — that's the deliberate Tier-3 V2 deferral). Referenced from `CONVENTIONS.md` per REQUIREMENTS.md §TEST-04.

### 1.0 Cut + Packaging

- **D-20:** **WiX MSI installer.** Open-source, MIT-licensed, free; matches Utinni's MIT distribution posture. WiX 5 (latest, .NET-based) is the target version. New `installer/` directory at repo root (or `Utinni.Installer/` sibling-project) houses the WiX project. New `.github/workflows/release.yml` triggered by `v1.0*` tag push: builds Release/x86, builds the MSI, attaches it to the auto-created GitHub Release. Installer behaviour: copy files to `C:\Program Files (x86)\Utinni\` (x86 per CON-P-02), Start menu shortcut, uninstaller, optional default-OFF "detect SWG client path + seed utinni.cfg" checkbox (preserves CON-D-01 "ships blank" via opt-in; if user opts in, installer probes `C:\SWGEmu\`, `C:\Program Files\SWGEmu\`, registry keys, prompts user to confirm the detected path). **No file-type associations** in 1.0-rc.1 — Wave-1 plugins (Phases 7-11) own the editors; associations defer to V1 release at Phase 11 closure.

- **D-21:** **MSI bundles TJT (UtinniPlugins/TheJawaToolbox) per DEC-C4 pair distribution.** Release workflow checks out UtinniPlugins at a pinned commit SHA (planner picks the pinning strategy: submodule vs explicit checkout-with-ref), builds TJT, drops the TJT DLL + dependencies into the MSI's plugin install dir (likely `C:\Program Files (x86)\Utinni\Plugins\TheJawaToolbox\`). One artifact for users matches the [[project-wave1-tjt-subpanels]] "Utinni + TJT as a pair" framing.

- **D-22:** **Phase 6 exits with `v1.0.0-rc.1` tagged + GitHub Pre-release.** SemVer scheme. Auto-generated release notes from git log between the project's first commit (or v0.x baseline if introduced) and v1.0.0-rc.1, with a manually-edited "Highlights" section summarising Phases 1-6. Bake period (planner picks N=7-14 days) before promotion to `v1.0.0`. Promotion criterion: "no critical issues" surfaced via maintainer or external feedback during bake; if issues surface, ship `v1.0.0-rc.2` etc. Promotion to `v1.0.0` final happens post-Phase-6 as a follow-up commit (or a tiny post-phase plan); ROADMAP Phase 11 V1 closure is a separate event.

- **D-23:** **MSI unsigned for V1.** SmartScreen warning on first install is acceptable for an OSS modding tool. Authenticode certificate cost ($200-700/year) + identity verification not justified pre-V2.

- **D-24:** **Pre-tag Tier-4 UAT signed in `06-VERIFICATION.md`.** Per [[feedback-max-harness]], CI green is necessary but not sufficient. Phase 6 ends with maintainer running the TEST-04 Tier-4 residual checklist by hand (overlay Demo screen, WndProc forwarding, drag-drop, plugin load with TJT, MSI install + uninstall round-trip on a clean VM). Each Tier-4 row gets last-verified-SHA updated. `06-VERIFICATION.md` captures the run + the maintainer-signed-off disposition. Then push `v1.0.0-rc.1` tag.

### Plan Structure (emerging from decisions)

The 24 D-* decisions above shape a **6-plan structure** (planner finalises ordering + atomicity):

- **06-01: Overlay-debug investigation.** Gate: `ImGui::ShowDemoWindow` visible over live SWG. Tier-4 manual UAT row.
- **06-02: Dep-bumps + toolchain.** vcpkg manifest + 7 deps migrated; imgui docking-branch (gated on 06-01); spdlog 1.14 + OutputSink regression test; ImGuizmo bump; PlatformToolset v145; VSIX widen `[16.0,19.0)`.
- **06-03: STAB-05 open questions.** CON-O-08 DXSDK removal (depth_texture.cpp + .vcxproj sweep + closes CON-B-03); CON-O-06 LeksysINI replacement (custom parser inside UtINI/utini.cpp Impl + Catch2 tests); update assessment.md §"Open questions" final disposition.
- **06-04: CI flake fixes.** Loader-lock-harness 50ms threshold + GameCallbacks GC AV. Atomic per-fix + regression tests. Gates the 1.0-rc.1 tag.
- **06-05: STAB-03 cleanups + STAB-04 audit.** Full-repo clang-format single commit + `.git-blame-ignore-revs`; TJT.ico ejection in paired commits to UtinniPlugins; polish bundle (Windows SDK target unification + Prefer32Bit + ExampleEditorPlugin path + .gitignore Std/StdEdited + namespace Std + Native.SendMessage IntPtr + licenses.txt + typos); STAB-04 audit `06-AUDIT.md` + automated grep tests in `Utinni.PreservationAudit.Tests` or `UtinniCoreDotNet.Tests/PreservationAudit/`.
- **06-06: TEST-04 Tier-4 doc + 1.0 packaging + tag.** Extend `.planning/codebase/TESTING.md` with full Tier-4 residual; reference from CONVENTIONS.md; new `.github/workflows/release.yml`; new `installer/` WiX project; cross-repo paired commits to UtinniPlugins for TJT bundling pin; maintainer-signed Tier-4 UAT in `06-VERIFICATION.md`; push `v1.0.0-rc.1` tag + GitHub Pre-release.

CI-gated plan boundaries (Phase 3/4/5 precedent). Each plan green on `master` before next starts.

### Folded Todos

- **`loader-lock-harness-flake-fix`** (score 0.6) — DllMain 50ms threshold flakes on shared windows-2022 runners. Per [[project-loader-lock-harness-ci-flake]] memory: re-run before investigating; confirmed flake via Phase 5 wave-1 push. Lands in 06-04 CI flake plan.
- **`gamecallbacks-gc-av-flake-fix`** (score 0.4) — `GameCallbacksTests.RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV` intermittent AV under CI. Lands in 06-04.

### Claude's Discretion

- Exact directory layout under `installer/` (flat WiX vs structured submodule).
- WiX 4 vs WiX 5 (latest stable at plan-time; planner picks).
- vcpkg baseline pin (specific microsoft/vcpkg commit SHA).
- Bake period N days (7-14, planner picks based on confidence + community engagement).
- Whether STAB-04 audit tests live in `UtinniCoreDotNet.Tests/PreservationAudit/` (single-test-project convention from Phase 1 D-04) or a new `Utinni.PreservationAudit.Tests` sibling project (Phase 1/3 sibling-project precedent).
- ImGuizmo target commit/tag (planner picks latest stable).
- Whether TJT pinning uses git submodule vs CI checkout-with-ref (former is more reproducible; latter is simpler).
- Exact UtinniForm default icon image (gear/wrench/lambda/some-generic).
- Exact `vcpkg-configuration.json` registry strategy.
- The MSI's `UpgradeCode` GUID (stable across versions per WiX upgrade-path convention) — planner picks once and documents.
- The xUnit test home for the STAB-04 grep checks — `Utinni.PreservationAudit.Tests` sibling, or extend `UtinniCoreDotNet.Tests` with a `PreservationAudit/` subfolder.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project context (locked decisions, requirements, constraints)
- `.planning/PROJECT.md` — V1 milestone scope; anti-goals (DEC-A1..A4 LOCKED); preservation guard-rails (24 load-bearing foundations — STAB-04 audits all of them at 1.0); DEC-C3 (tiered testing strategy LOCKED at Phase 4); DEC-C4 (Wave-1 ships as TJT subpanels LOCKED; informs the MSI-bundles-TJT decision in D-21).
- `.planning/REQUIREMENTS.md` §STAB-03 (~30 cleanups + dep bumps); §STAB-04 (audit 24 foundations intact); §STAB-05 (resolve CON-O-01..-08 — Phase 6 closes the last two, CON-O-06 + CON-O-08); §TEST-04 (Tier 4 boundary documented).
- `.planning/ROADMAP.md` §"Phase 6: Cleanups, dep bumps, open questions, Tier 4 doc, 1.0 cut" — goal, depends-on (Phase 5), requirements (STAB-03/04/05 + TEST-04), open-question dispositions (CON-O-06 + CON-O-08), preservation guard-rails, success criteria (#1-5).
- `.planning/intel/constraints.md` — **CON-N-01..-09** (native foundations — STAB-04 audit subset); **CON-M-01..-09** (managed foundations — audit subset); **CON-T-01..-05** (process/tooling — audit subset); **CON-B-01** (VS 2019 + VS 2022 contributor support; D-10 widens to add VS 2026); **CON-B-02** (Windows SDK target unification; D-16 addresses); **CON-B-03** (DXSDK paths in all configs; D-01 closes structurally by removing DXSDK); **CON-D-01** (utinni.cfg ships blank; D-20 preserves via opt-in seed); **CON-N-06** (imgui device-loss handling — 06-01 must preserve); **CON-N-09** (spdlog `base_sink<std::mutex>` — D-07 regression test); **CON-O-06**, **CON-O-08** (resolved here).
- `.planning/intel/decisions.md` — D-08 tiered testing philosophical home.

### Source documents (immutable inputs from ingest)
- `docs/ai/assessment.md` §"Easy cleanups" — full enumeration of ~30 cleanups (dead code, typos, build/config polish) that STAB-03 closes; §"Open questions" — CON-O-06 + CON-O-08 disposition rows updated at phase end with resolved-pointers + commit SHAs; §"Solid foundations" — the 24 load-bearing items that STAB-04 audits.
- `docs/ai/test-harness-plan.md` §"Tier 4 — manual residual" — the document that motivated TEST-04; Phase 6 closes the Tier-4-side row.
- `docs/ai/vision.md` — anti-goals (DEC-A1..A4) informing what MSI installer must NOT do (no server-side mod mgmt; no launcher/patcher; no DCC; no cheat-enabler).

### Prior-phase carry-forward
- `.planning/phases/01-ci-tier-1-c-scaffold/01-CONTEXT.md` — D-01 sibling test project convention; D-02 net472/x86; D-04 `[Method]_[Scenario]_[ExpectedOutcome]` naming; D-07 single `.github/workflows/ci.yml` that Phase 6 extends with `release.yml`; D-11 `.editorconfig` (Phase 6 adds `.clang-format` alongside).
- `.planning/phases/02-critical-bug-burn-down-c-01-c-15/02-CONTEXT.md` — D-04 max-harness; D-09 cross-repo posture (paired commits to UtinniPlugins OK per [[feedback-utinniplugins-authority]]); C-12 CON-O-04 audit-then-widen precedent (Phase 6 D-10 inherits for VSIX `[16.0,19.0)`).
- `.planning/phases/03-strategic-reworks-r-a-r-h/03-CONTEXT.md` — D-04 CI gate at every plan boundary; D-10 `Add*Callback` source-compat preservation (Phase 6 D-13 extends as defer-to-V2); D-23 R-G `Props.cs` idempotent merger.
- `.planning/phases/04-tier-2-cli-shim-golden-fixtures/04-CONTEXT.md` — D-06 parsers live managed in `UtinniCoreDotNet/Formats/` (informs CON-O-08 D-01 disposition note about DirectXMath substitution); D-11 `.github/workflows/ci.yml` extension pattern (Phase 6 release.yml is a parallel workflow, not an extension); D-12 cross-repo posture.
- `.planning/phases/05-tier-1-c-unit-tests/05-CONTEXT.md` — D-02 Catch2 vendored deliberately at `external/catch2/`; **Phase 6 D-05 explicitly migrates Catch2 to vcpkg as the "broader vcpkg call" Phase 5 D-02 deferred here**; D-03 `UtinniCore.Tests.exe` self-runner (Phase 6 D-03 + D-07 test homes for INI parser + OutputSink); D-04 CI lane required for master; D-06 max-harness posture (carried forward to Phase 6 D-07, D-17, D-18, D-24).

### Codebase intel (read-only reference)
- `.planning/codebase/TESTING.md` — Phase 6 D-19 extends this doc with the full Tier-4 residual enumeration; reference from CONVENTIONS.md per REQUIREMENTS.md §TEST-04.
- `.planning/codebase/STRUCTURE.md` — directory tree; locate `UtINI/`, `UtinniCore/swg/ui/`, `UtinniCore/swg/graphics/depth_texture.cpp`, `external/leksysini/`, `external/catch2/`, `external/imgui/`, `external/spdlog/`, `external/ImGuizmo/`.
- `.planning/codebase/STACK.md` — toolchain (MSBuild, vcxproj triple-config, net472/x86); Phase 6 D-09 bumps PlatformToolset v142 → v145.
- `.planning/codebase/CONVENTIONS.md` — Allman braces + 4-space indent + MIT header; Phase 6 D-12 `.clang-format` codifies these; D-19 references for TEST-04 visibility.
- `.planning/codebase/CONCERNS.md` — Phase 6 closes the ~30 cleanup items (`TD-25` empty stubs, `TD-26` disabled hooks, `TD-27` hardcoded font path, `TD-28` `TJT.ico` framework default leak) per STAB-03.
- `.planning/codebase/INTEGRATIONS.md` — `Native Process Integration` section informs the MSI installer's no-injection-at-install posture.
- `.planning/codebase/ARCHITECTURE.md` — overall structure; informs STAB-04 audit grep patterns.

### Surface this phase touches

**New projects / directories:**
- `installer/` or `Utinni.Installer/` (WiX MSI project; planner names final).
- `vcpkg.json` + `vcpkg-configuration.json` at repo root.
- `.clang-format` at repo root.
- `.git-blame-ignore-revs` at repo root (added by D-12).
- `UtinniCore.Tests/UtINI/IniParserTests.cpp` (D-03; INI parser tests).
- `UtinniCore.Tests/Log/OutputSinkRoundTripTests.cpp` (D-07; spdlog regression test).
- `Utinni.PreservationAudit.Tests/*.cs` OR `UtinniCoreDotNet.Tests/PreservationAudit/*.cs` (D-18; planner picks home).

**New workflows:**
- `.github/workflows/release.yml` (D-20; triggered on `v1.0*` tag).

**Existing files modified (high-impact subset):**
- Every `*.vcxproj` (PlatformToolset v145 per D-09; remove DXSDK paths per D-01; Windows SDK target unify per D-16).
- `UtinniCore/swg/graphics/depth_texture.cpp` (D-01; remove `d3dx9.h` + replace `D3DXVECTOR3`).
- `UtINI/utini.cpp` (D-02; replace LeksysINI with custom parser inside Impl).
- `UtinniCore/swg/ui/imgui_impl.cpp` (D-11; overlay-debug investigation focus).
- `external/imgui/` (D-06; docking branch + via vcpkg).
- `external/spdlog/` (D-07; 1.14 via vcpkg).
- `external/ImGuizmo/`, `external/catch2/`, `external/CppSharp/`, `external/DetourXS/`, `external/nvapi/` (D-05; migrated to vcpkg).
- `external/leksysini/` (D-02; deleted entirely).
- `.github/workflows/ci.yml` (D-05; add vcpkg install step).
- `docs/ai/assessment.md` §"Open questions" (D-04; CON-O-06 + CON-O-08 disposition updates).
- `docs/ai/assessment.md` §"Status tracking" + §"Easy cleanups" (STAB-03 closure tracking).
- `.planning/codebase/TESTING.md` (D-19; Tier-4 residual enumeration).
- `.planning/codebase/CONVENTIONS.md` (D-19; reference Tier-4 doc).
- `Utinni.sln` (likely add installer + preservation-audit-tests projects).

**Cross-repo touch (`kennethlong/UtinniPlugins`):**
- `TheJawaToolbox/Resources/TJT.ico` (D-15; received from Utinni).
- `TheJawaToolbox/<wherever-UtinniForm-default-icon-lived>` — paired source-side removal.
- A pinned-commit SHA written into Utinni's `release.yml` (D-21; planner picks pinning strategy).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`UtinniCore.Tests.exe` (Phase 5 Catch2 self-runner)** — INI parser tests (D-03) + spdlog OutputSink regression test (D-07) slot in directly. Existing `UtINI/`, `Log/`, `UtilityHelpers/` subfolder pattern available.
- **`UtinniCoreDotNet.Tests` (Phase 1/2/2.1/3/4 managed test project)** — STAB-04 audit grep tests can land here under `PreservationAudit/` (or in a sibling `Utinni.PreservationAudit.Tests` per Phase 1/3 sibling-project precedent).
- **`.github/workflows/ci.yml` (Phase 1 + extended in 4 + 5)** — Phase 6 extends with vcpkg install step in 06-02; new sibling `release.yml` for tag-triggered MSI build in 06-06.
- **`UtINI/utini.cpp` PIMPL Impl** — D-02's custom INI parser swaps Impl only, callers untouched. PIMPL is the existing pattern; Phase 6 preserves it (CON-N-08 family).
- **Phase 3 R-B fixture projects (`Utinni.CrtMatchPlugin`, `Utinni.LegacyPlugin`)** — TJT install path validation in Phase 6 D-21 can reuse these fixtures (or actually use TJT itself once it's bundled into the MSI).
- **`UtinniForm` default icon resolution** — known from CON-M-06 (custom title bar via `OnPaint` + `WM_NCHITTEST`); D-15 reuses the existing resource resolution code, just swaps the bundled image.

### Established Patterns
- **Atomic commit per task** — GSD executor default; Phase 6's larger plans (06-02 dep-bumps, 06-05 cleanups) still atomic-per-dep / atomic-per-cleanup-item.
- **CI-gated plan boundaries** (Phase 3 D-04 precedent) — Phase 6's 6 plans gate one another, lowest-blast-radius first (06-01 overlay-debug before 06-02 dep-bumps before 06-03 open-Qs before 06-04 CI flakes before 06-05 cleanups+audit before 06-06 packaging+tag).
- **Max-harness verification posture** ([[feedback-max-harness]]) — every D-XX with a behaviour change ships a regression test that fails if reverted (D-03 INI parser tests; D-07 spdlog OutputSink test; D-18 STAB-04 grep tests; D-24 maintainer Tier-4 UAT).
- **Sibling-project + single-test-project tension** (Phase 1 D-01 single vs Phase 3 D-07 single vs Phase 4 D-06 split) — Phase 6 D-18 leaves this to planner discretion (extend existing vs new sibling).
- **Cross-repo paired commits** ([[feedback-utinniplugins-authority]] + Phase 02 D-09 + Phase 3 D-26) — Phase 6 D-15 (TJT.ico) + D-21 (MSI TJT bundling pin) use this pattern; no human-action checkpoints required.
- **MIT license header on every C# file** (CONVENTIONS.md) — new files in `installer/`, `Utinni.PreservationAudit.Tests/`, `UtinniCoreDotNet.Tests/PreservationAudit/`, etc. must include the 23-line block.
- **PascalCase project + file naming** — `Utinni.Installer.csproj` (or `installer/Utinni.Installer.wixproj`), `Utinni.PreservationAudit.Tests.csproj`, `IniParserTests.cpp`, `OutputSinkRoundTripTests.cpp`.
- **Triple-config preservation (CON-T-02)** — every `.vcxproj` touched by D-01 (DXSDK strip), D-09 (toolset bump), D-16 (Windows SDK unify) keeps `Debug + Release + RelWithDbgInfo`.

### Integration Points
- **`Utinni.sln`** — adds installer project (D-20) and optionally `Utinni.PreservationAudit.Tests.csproj` (D-18).
- **`vcpkg.json` manifest mode** — first-class integration; CI step installs vcpkg via `microsoft/setup-vcpkg` (or custom bootstrap). Affects every `.vcxproj` that previously included `external/*` headers — now resolves via vcpkg's include path injection.
- **GitHub Actions** — release.yml is new (D-20); ci.yml gets a vcpkg install step (D-05) and remains the primary gate.
- **Cross-repo `UtinniPlugins` repo** — TJT.ico receipt (D-15); pinned commit SHA referenced in Utinni's release.yml (D-21).

### Phase-6 risks captured during discussion (see `<specifics>` for impact)

- **imgui overlay has never displayed in Utinni-injected sessions.** Per user's note 2026-05-23 during discuss-phase. Per [[feedback-d3d9-hook-diagnosis]], d3d9.dll pattern-scan check is the 30-sec first move before assuming SWG-side RVA drift. 06-01 investigation must clear before imgui-docking-branch bump in 06-02.
- **Catch2 just-vendored in Phase 5 → migrating to vcpkg in Phase 6** = partial undo of Phase 5 D-02 work. Accepted; consistent with Phase 5 D-02's explicit "Phase 6 STAB-03 owns the broader vcpkg-vs-vendored call" deferral.
- **CppSharp vcpkg-port quality unknown.** Build-time codegen tool; vcpkg port may not exist or may be broken. Planner researches at plan time; fallback path is "keep CppSharp vendored" if port unusable.
- **Full vcpkg migration during Phase 6** alongside cleanups + open-Qs + Tier-4-doc + 1.0-tag + flake fixes + overlay-debug = significant churn. CI-gated plan boundaries (D-02 / Phase 3 D-04 precedent) mitigate.
- **`swg/ui/` commented-detour cleanups deferred entirely** per [[feedback-keep-scaffolding-wip]] — risk of deleting something the overlay actually needs. Carry forward as "deferred pending post-overlay-fix audit".

</code_context>

<specifics>
## Specific Ideas

- **The d3d9 pattern-scan check is the 30-sec first move in 06-01.** Per [[feedback-d3d9-hook-diagnosis]] memory: when ImGui doesn't render in Utinni-injected sessions, test the d3d9.dll pattern-scan FIRST (30 sec) before assuming SWG-side RVA drift (multi-day investigation). Researcher MUST cite this memory in `06-01-RESEARCH.md` and explicitly check `directX::getVtbl` pattern-scan validity before any deeper investigation.
- **Catch2 + CppSharp vcpkg-port quality is the highest-risk research item in 06-02.** If either port is broken, planner falls back to keeping that specific dep vendored — partial vcpkg adoption is acceptable as a fallback, full migration is the goal but not at the cost of breaking the build.
- **`Native.SendMessage int → IntPtr` in D-16 needs an internal-vs-public visibility check before shipping.** Per [[feedback-caller-attrs-binary-compat]], cross-binary plugin DLLs break at MEF compose if signatures change. If `Native.SendMessage` is `public`, add a 1-arg `int`-overload shim that delegates to the new IntPtr method. Planner verifies at task time.
- **The full Demo screen exit criterion in D-11 is intentionally high.** `ImGui::ShowDemoWindow` exercises menus + sliders + buttons + tabs + plots + popups + drag-and-drop end-to-end. Lower exit bars ("any widget visible") were rejected because they wouldn't catch input-pipeline bugs that Wave-1 subpanels will need. If the high bar reveals additional bugs to fix before the imgui bump, scope expands within 06-01 — that's by design.
- **STAB-04 audit grep checks should fail-on-violation, not warn.** Phase 4 D-11's "warn-only is the wrong shape" principle extends here. Every CON-* grep test is a real CI gate that turns red if a future cleanup violates the foundation.
- **DEC-C4's "Utinni + TJT as a pair" framing is load-bearing for D-21.** The MSI bundles TJT because users install Utinni + TJT together per the V1 distribution model. If V2 introduces third-party plugin packaging, the MSI-pair model re-opens.
- **The bake period for v1.0.0-rc.1 → v1.0.0 is a post-Phase-6 task.** Phase 6 ends at the rc.1 tag + Pre-release; the bake + promotion live outside Phase 6's plans. Plan-phase researcher may surface "bake checklist criteria" as a follow-up artifact but it's NOT a Phase-6 deliverable.
- **`docs/ai/assessment.md` §"Status tracking" must be updated by every Phase-6 plan that closes a STAB-03 cleanup item or a CON-O-* disposition.** D-04 captures this; planner makes it explicit in each plan's task list.
- **Phase 5 D-02 explicitly deferred the "broader vcpkg call" here; D-05 honours that handoff cleanly.** No re-litigation needed; Phase 5 was right to vendor Catch2 standalone at the time.
- **`.git-blame-ignore-revs` is a standard git feature (since 2.23+)** and well-supported by GitHub's blame UI. D-12's full-repo clang-format SHA gets added in the same commit; future blame queries respect the ignore automatically when `--ignore-revs-file` is set.

</specifics>

<deferred>
## Deferred Ideas

- **`Add*Callback` vs `Add*Call` naming consolidation** — V2; per Phase 3 D-10 source-compat and [[feedback-caller-attrs-binary-compat]] memory. Revisit once UtinniPlugins ecosystem has fully migrated.
- **`swg/ui/` commented-detour cleanups** (cui_chat_window.cpp:166, cui_io.cpp:96, cui_hud.cpp:164,168, appearance.cpp:102-103) — deferred PENDING post-overlay-fix audit. Carry forward in `docs/ai/assessment.md`.
- **File-type associations in MSI** (.tre/.iff/.tab/.stf/.otpl) — Wave-1 plugins (Phases 7-11) own the editors; defer to V1 release at Phase 11 closure (or later) once the plugins exist.
- **Authenticode code-signing** — V2; cost not justified for V1 OSS modding tool. Revisit when funding model is clearer.
- **v1.0.0 final tag promotion** — happens post-Phase-6 after bake period (N=7-14 days, planner picks). Not a Phase-6 deliverable.
- **D3D11 migration** ([[project-d3d11-migration]] memory) — future R-letter item, not Phase 6 scope. SWG Source's D3D11 work is upstream; Utinni's parallel path is a separate phase.
- **TRE version 0005/0006 support gap** ([[project-tre-version-support-gap]] memory) — Phase 7 (TRE Browser) scope, not Phase 6.
- **Plugin hot-reload runtime flow** — Phase 3 D-16 enabled the structural groundwork; actually exercising unload-while-running is V2.
- **Surfacing the two `isSafeToUse` RVAs via UTINNI_API** — not needed today (no managed reader). Add when/if a Wave-1 subpanel needs them.
- **Coverage tooling** (coverlet, OpenCover, Codecov) — out of Phase 6 scope; revisit in V2 after Wave-1 subpanels land more test breadth.
- **Tier 3 mock-D3D9 + recorded fixtures** — REQ-V2-tier-3-mock-d3d9; deferred from V1 per ROADMAP.
- **Author-new-content / mod packaging / community hub** — Wave-3 plugins; V2+.
- **MSIX modernization** — rejected at D-20 in favour of WiX; revisit if Win10/11 sandboxing becomes a priority.
- **MSI ProductCode versioning strategy** (per-version vs single stable UpgradeCode) — Claude's discretion in D-20; planner picks once and documents in the installer project README.

### Reviewed Todos (not folded)

None — both pending GSD todos that matched Phase 6's scope (`loader-lock-harness-flake-fix`, `gamecallbacks-gc-av-flake-fix`) were folded into 06-04 per D-17.

</deferred>

---

*Phase: 06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut*
*Context gathered: 2026-05-23*
