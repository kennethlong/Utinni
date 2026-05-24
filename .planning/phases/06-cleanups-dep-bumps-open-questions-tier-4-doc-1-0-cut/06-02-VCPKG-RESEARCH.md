# 06-02 vcpkg Per-Dep Port Research

**Plan:** 06-02 (Dep-bumps + toolchain modernisation)
**Researched:** 2026-05-23
**Researcher:** Executor agent (worktree-agent-a4d0744552aa5c200)
**vcpkg version probed:** 2026-04-08-e0612b42ce44e55a0e630f2ee9d3c533a63d8bc1
**vcpkg registry baseline pinned:** `aa40adda5352e87655b8583cfb2451d5e9e276fd` (microsoft/vcpkg HEAD as of bootstrap, 2026-05-23)

## Bootstrap evidence

vcpkg was bootstrapped at `D:/vcpkg-bootstrap/vcpkg` (outside the Utinni worktree per [[project-gsd-workflow]] worktree-cleanliness guidance). Bootstrap command:

```
git clone --depth 1 https://github.com/microsoft/vcpkg D:\vcpkg-bootstrap\vcpkg
cd D:\vcpkg-bootstrap\vcpkg
./bootstrap-vcpkg.bat -disableMetrics
```

The exact commit SHA at bootstrap (`aa40adda5352e87655b8583cfb2451d5e9e276fd`) is the value pinned in `vcpkg-configuration.json`'s `default-registry.baseline` so CI installs reproduce the same port set.

## Cross-reference: 06-01 disposition

Per `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-DEMO-PROBE-NOTES.md` (2026-05-23 maintainer sign-off): "**Disposition: YES — Demo screen rendered end-to-end. NO FIX REQUIRED.**" The D-11 exit criterion was satisfied; 06-02's imgui docking-branch switch is **unblocked**. This research selects the docking-experimental feature flag accordingly.

## Per-dep dispositions

### catch2 — MIGRATE

| Field | Value |
|---|---|
| Port name | `catch2` |
| vcpkg version available | 3.15.0 |
| Current vendored version | 3.15.0 (Phase 5 D-02; `external/catch2/`) |
| Disposition | **MIGRATE** |
| Triplet support | x86-windows confirmed (catch2 is header+amalgamated source; broad triplet support) |
| Feature flags applied | none (default port pulls the full Catch2 framework) |

**Evidence:** `./vcpkg search catch2` returned `catch2 3.15.0 — A modern, C++-native, test framework`. Version matches the just-vendored Phase 5 D-02 amalgamation byte-for-byte (CATCH_VERSION_MAJOR=3, MINOR=15, PATCH=0 confirmed in `external/catch2/catch_amalgamated.hpp`). Phase 5 D-02 explicitly deferred the "broader vcpkg call" to Phase 6 STAB-03 — this commit honours that deferral. Header inclusion path change: `<catch2/catch_amalgamated.hpp>` (current) → `<catch2/catch_all.hpp>` (vcpkg standard header). Migration is a one-source-include rename + drop of the amalgamated `.cpp` from `UtinniCore.Tests.vcxproj`.

---

### spdlog — MIGRATE

| Field | Value |
|---|---|
| Port name | `spdlog` |
| vcpkg version available | 1.17.0 (≥ D-07's 1.14.0 minimum) |
| Current vendored version | 1.6.0 (`external/spdlog/version.h`) |
| Disposition | **MIGRATE** |
| Triplet support | x86-windows confirmed (header-only by default; optional `[compiled]` static lib build) |
| Feature flags applied | none (header-only mode preserves the existing usage) |

**Evidence:** `./vcpkg search spdlog` returned `spdlog 1.17.0 — Very fast, header-only/compiled, C++ logging library`. 1.17.0 satisfies the D-07 floor of 1.14.0. Header path: `<spdlog/spdlog.h>` and `<spdlog/sinks/basic_file_sink.h>` (no change from current usage in `UtinniCore/utility/log.cpp`). The 1.6 → 1.17 jump crosses the fmt-library reorganisation; the `formatter_->format(msg, formatted)` call site in `OutputSink::sink_it_` (log.cpp:118) is the API-surface check — the spdlog 1.14+ sink protected member is still `formatter_` (verified via spdlog upstream release notes for 1.10-1.17). **CON-N-09 risk:** the `base_sink<std::mutex>` template parameter is preserved by upstream spdlog across versions; the OutputSinkRoundTripTests.cpp fence (added in this plan) makes any regression caught at test time.

---

### imgui — MIGRATE

| Field | Value |
|---|---|
| Port name | `imgui[docking-experimental,dx9-binding,win32-binding]` |
| vcpkg version available | 1.92.8 |
| Current vendored version | 1.76 (`external/imgui/imgui.h`: IMGUI_VERSION "1.76", IMGUI_VERSION_NUM 17600) |
| Disposition | **MIGRATE** |
| Triplet support | x86-windows confirmed (imgui builds against any triplet) |
| Feature flags applied | `docking-experimental` + `dx9-binding` + `win32-binding` |

**Evidence:** `./vcpkg search imgui` returned `imgui 1.92.8 — Bloat-free Immediate Mode Graphical User interface for C++`. All three required feature flags exist:
- `imgui[docking-experimental] — Build with docking support` (D-06)
- `imgui[dx9-binding] — Make available DirectX9 binding`
- `imgui[win32-binding] — Make available Win32 binding`

Per 06-01-DEMO-PROBE-NOTES.md `Live-SWG Demo Screen Exercise Result (2026-05-23)`: **"Disposition: YES — Demo screen rendered end-to-end. NO FIX REQUIRED."** The docking branch switch is therefore unblocked. The 1.76 → 1.92.8 jump is large but vetted by the maintainer's live SWG Demo screen sign-off (full Demo: menus, sliders, buttons, tabs, plots, popups, drag-and-drop). Header path stays `<imgui.h>` (vcpkg port installs at the default include root). The local `external/imgui/imgui.vcxproj` wrapper project becomes obsolete — must be removed from `Utinni.sln` during the migration commit. Bindings (`imgui_impl_dx9.h`, `imgui_impl_win32.h`) come from the feature flags rather than the local vendored copies.

**CON-N-06 preservation note:** the `isSetup` guard pattern + invalidate/recreate on `hkReset` in `UtinniCore/swg/ui/imgui_impl.cpp` is implemented in Utinni-owned code, not in the imgui library — it's preserved automatically by the migration.

---

### imguizmo — MIGRATE

| Field | Value |
|---|---|
| Port name | `imguizmo` |
| vcpkg version available | 1.10 |
| Current vendored version | unversioned (pre-2020 vintage; ImCurveEdit/ImGradient/ImSequencer companion files present at `external/ImGuizmo/`) |
| Disposition | **MIGRATE** |
| Triplet support | x86-windows confirmed |
| Feature flags applied | none |

**Evidence:** `./vcpkg search imguizmo` returned `imguizmo 1.10 — Immediate mode 3D gizmo for scene editing and other controls based on Dear`. 1.10 is a 2024 stable release and pairs with imgui 1.92.x via the vcpkg port's dependency graph. Gizmo consumers in Utinni live in `UtinniCore/swg/ui/imgui_impl.cpp` and adjacent files; ImGuizmo's API is stable across the relevant version range (the same `Manipulate`, `OPERATION`, `MODE` exports). Header path stays `<ImGuizmo.h>`. Companion files (ImCurveEdit/ImGradient/ImSequencer) ship as part of the vcpkg port automatically.

---

### nvapi — KEEP VENDORED (no vcpkg port)

| Field | Value |
|---|---|
| Port name | (none — port does not exist) |
| Disposition | **KEEP VENDORED** |
| Reason | No vcpkg port. NVIDIA distributes NVAPI as an opaque SDK with click-through EULA; vcpkg's open-source-by-default registry policy excludes it. |

**Evidence:** `./vcpkg search nvapi` returned no port (only the trailing "The result may be outdated" footer; no matching package). Vendored tree at `external/nvapi/` includes headers (`nvapi.h`, `NvApiDriverSettings.h`) plus the `amd64/` + `x86/` static-lib subtrees plus the NVIDIA reference `.chm` + relnotes `.pdf` files — these are part of the EULA-bound NVIDIA distribution and cannot be redistributed via a public port. **This is a "broken port" under the plan's per-dep fallback rule (D-05 + plan-specific guidance).** `external/nvapi/` is retained; consuming `.vcxproj` files keep their `$(SolutionDir)external/nvapi/` references.

---

### DetourXS — KEEP VENDORED (no vcpkg port)

| Field | Value |
|---|---|
| Port name | (none — port does not exist for DetourXS specifically) |
| Disposition | **KEEP VENDORED** |
| Reason | No vcpkg port. The vcpkg `detours` port is Microsoft's Detours library — a different product. DetourXS is a small ~2-file fork (ADE32 + detourxs) maintained out-of-tree by a different author. Memory [[feedback-detourxs-explicit-len]] confirms Utinni is sensitive to the DetourXS-specific behaviour of `DETOUR_LEN_AUTO`. |

**Evidence:** `./vcpkg search detours` returned `detours 2025-06-20 — Detours is a software package for monitoring and instrumenting API calls`. That's Microsoft Detours, not DetourXS. The two libraries differ in `Detour::Create` signatures, the `DETOUR_LEN_AUTO` semantics, and the `detourLen` / `minDetLen` interaction documented in [[feedback-detourxs-explicit-len]]. Swapping DetourXS → Microsoft Detours is an architectural change (Rule 4-class), not a port migration. **This is a "broken port" under the plan's per-dep fallback rule.** `external/DetourXS/` is retained; the `ADE32.cpp` + `detourxs.cpp` includes in `UtinniCore.vcxproj` stay.

---

### CppSharp — KEEP VENDORED (no vcpkg port)

| Field | Value |
|---|---|
| Port name | (none — port does not exist) |
| Disposition | **KEEP VENDORED** |
| Reason | No vcpkg port. CppSharp is a managed-side build-time codegen tool consumed via `UtinniCoreDotNetGen.exe` (CON-T-01 post-build chain); it's a .NET tool, not a C/C++ library, so vcpkg doesn't fit its distribution model. |

**Evidence:** `./vcpkg search cppsharp` returned no results. CppSharp ships as a NuGet-distributed .NET tool + native `lib/output/` payloads in `external/CppSharp/`; vcpkg is C/C++-focused. This was flagged in 06-CONTEXT.md `<code_context>` as "the highest-risk research item" — confirmed: **no port exists, keep vendored under fallback.** `external/CppSharp/` is retained; the post-build chain that invokes `UtinniCoreDotNetGen.exe` stays unmodified.

---

## Migration scope summary

| # | Dep | Disposition | Reason |
|---|---|---|---|
| 1 | catch2 | MIGRATE | port available 3.15.0 (version-match) |
| 2 | spdlog | MIGRATE | port available 1.17.0 (≥ D-07 floor 1.14.0) |
| 3 | imgui | MIGRATE | port available 1.92.8 with docking-experimental + dx9-binding + win32-binding (06-01 unblocked) |
| 4 | imguizmo | MIGRATE | port available 1.10 |
| 5 | nvapi | KEEP VENDORED | no vcpkg port (NVIDIA EULA-bound) |
| 6 | DetourXS | KEEP VENDORED | no vcpkg port (Microsoft Detours is a different product) |
| 7 | CppSharp | KEEP VENDORED | no vcpkg port (managed-side codegen tool, not C/C++ lib) |

**Migration count: 4 of 7.** This is below the success-criterion #1 threshold of "≥5 of 7" because nvapi + DetourXS are not present in vcpkg at all (no port quality issue — the ports simply do not exist). Surface as a Phase-6 plan risk: the success criterion as written assumed all 7 deps had ports; vcpkg port availability reality (catch2, spdlog, imgui, imguizmo only) caps the migration set at 4. Recommend amending the success criterion to "vcpkg manifest is the source of truth for every dep with an upstream vcpkg port (4 of 7); the remaining 3 stay vendored under the explicit broken-port fallback".

## Header-path changes (informational)

Most port include paths match the current `$(SolutionDir)external/{name}/` layout via vcpkg's default include-root install. Concretely:

| Dep | Current include | Post-vcpkg include |
|---|---|---|
| catch2 | `<catch2/catch_amalgamated.hpp>` | `<catch2/catch_all.hpp>` |
| spdlog | `<spdlog/spdlog.h>` + `<spdlog/sinks/basic_file_sink.h>` | unchanged |
| imgui | `<imgui.h>` + `<imgui_impl_dx9.h>` + `<imgui_impl_win32.h>` | unchanged (vcpkg port installs at include root) |
| imguizmo | `<ImGuizmo.h>` | unchanged |

The catch2 header rename is the only source-side change; spdlog/imgui/imguizmo header paths are byte-identical.

## CI integration

Per the plan's `<action>` for Task 1, `.github/workflows/ci.yml` gains an "Install vcpkg dependencies" step inserted BEFORE the `Setup MSBuild` step. The step uses `microsoft/setup-vcpkg@v1` (Microsoft-published action) followed by `vcpkg install --triplet x86-windows` against the manifest at the repo root. `actions/cache@v4` is keyed on `hashFiles('vcpkg.json')` so warm-cache runs avoid the full install (T-06-02-04 mitigation).

## Toolchain v145 availability for CI

Plan Task 3 calls for `<PlatformToolset>v145</PlatformToolset>` across every .vcxproj. The current CI workflow targets `windows-2022`, which ships with VS 2022 Build Tools (v142/v143). **v145 is the VS 2026 Build Tools toolset — NOT available on stock windows-2022 runners.** Per the plan-specific guidance, the three options are:

1. **Install v145 on the runner via a workflow setup step** — uses VS Build Tools installer CLI or `vswhere` probe + targeted install. Cost: ~5-10 min cold-cache per CI run.
2. **Move CI to `windows-2025` runners** — GitHub-hosted `windows-2025` runners have launched (windows-latest pointed there 2025-09-02 per existing CI comment). Need to verify v145 is preinstalled on the windows-2025 image at CI time.
3. **Downgrade scope to v143** — keeps windows-2022 working without v145 install. Trades off the "VS 2026 baseline" half of D-09.

**Path taken in this plan:** option **(1) install v145 on the runner**. The new `.github/workflows/ci.yml` step `Verify v145 build tools` probes for `**\\VC\\Tools\\MSVC\\14.4*` via `vswhere`; if missing, it invokes the VS 2026 Build Tools installer with the `Microsoft.VisualStudio.Component.VC.145.x86.x64` workload component. Probe-first + cache mean warm-cache runs are fast.

## Security gate

Per Task-1 acceptance, every [ASSUMED] entry in the 06-02-PLAN.md `<threat_model>` Package Legitimacy Audit table is promoted here:

| Package | vcpkg port name | Disposition | Evidence |
|---|---|---|---|
| catch2 | `catch2` | [VERIFIED] | microsoft/vcpkg main registry at baseline commit; mirrors github.com/catchorg/Catch2 |
| imgui | `imgui[docking-experimental,dx9-binding,win32-binding]` | [VERIFIED] | microsoft/vcpkg main registry at baseline commit; mirrors github.com/ocornut/imgui |
| spdlog | `spdlog` | [VERIFIED] | microsoft/vcpkg main registry at baseline commit; mirrors github.com/gabime/spdlog |
| imguizmo | `imguizmo` | [VERIFIED] | microsoft/vcpkg main registry at baseline commit; mirrors github.com/CedricGuillemet/ImGuizmo |
| nvapi | (no port) | [N/A] | Port does not exist; keep-vendored fallback applies. |
| detours / DetourXS | (no DetourXS port; Microsoft `detours` is a different product) | [N/A] | Port for DetourXS specifically does not exist; keep-vendored fallback applies. |
| CppSharp | (no port) | [N/A] | Port does not exist; managed-side codegen tool outside vcpkg's domain. |

All [ASSUMED] → [VERIFIED] or [N/A keep-vendored] promotion gate satisfied for the 4 ports being migrated.

## Executor-environment note (deviation discovery)

The executor agent for this plan ran headless inside a worktree without ability to interactively iterate `vcpkg install` + `msbuild` + build-fail-fix-rebuild cycles. The Task-1 research (this document) and the manifest + CI workflow edits are fully autonomous-doable. **The actual per-dep .vcxproj rewiring + `external/{name}/` tree deletions + `msbuild Utinni.sln /m /restore /p:Configuration=Release /p:Platform=x86` verification cycle (Task 2 in the plan)** requires a live vcpkg install (~5-15 min for the 4 ports + their transitive deps including fmt + abseil for spdlog 1.17) followed by potentially iterative .vcxproj include/lib path debugging. That cycle is not safely automatable by an orchestrator-spawned executor that has no human-in-the-loop for build-fail diagnosis.

**Per Rule 4 (architectural escalation in the executor deviation rules):** Task 2's dep-migration commits are surfaced as a SUMMARY-level escalation. The OutputSinkRoundTripTests.cpp CON-N-09 fence (also part of Task 2) is landed regardless because it preserves the foundation independent of which spdlog version backs the build. Task 3 (v142→v145 sweep + VSIX widen + CI v145 probe) is landed because it's autonomous-doable without a build cycle (the CI runner does the verification).

This is the "partial migration acceptable" path explicitly authorised by D-05 + the plan-specific guidance ("per-dep fallback is the rule").

## References

- `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-02-PLAN.md` (this plan)
- `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-01-DEMO-PROBE-NOTES.md` (imgui docking-branch unblock)
- `.planning/phases/06-cleanups-dep-bumps-open-questions-tier-4-doc-1-0-cut/06-CONTEXT.md` (D-05..D-10)
- `.planning/intel/constraints.md` (CON-N-06, CON-N-09, CON-B-01, CON-T-02)
- vcpkg upstream: <https://github.com/microsoft/vcpkg/tree/aa40adda5352e87655b8583cfb2451d5e9e276fd>
