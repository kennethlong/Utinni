# Session Handoff: 2026-05-24 NIGHT — vcpkg migration COMPLETE (catch2 + imgui 1.76→1.92.8 + ImGuizmo)

> Picks up from `SESSION-HANDOFF-2026-05-24-PM.md`, whose carry-forward watch item read: *"Per-dep vcpkg migration: spdlog DONE, 3 remain (catch2 / imgui / imguizmo)."* This session **finished all three.** The user explicitly chose to attempt the full imgui version bump (1.76 → 1.92.8) rather than leave it vendored. What looked like a multi-day port turned out tractable after investigation. Master advanced `31df993` → `d71feb2` (2 commits), both pushed.

---

## TL;DR

- **All four vcpkg-available C++ deps are now consumed from vcpkg.** catch2 (Catch2 v3), spdlog (header-only, from the PM session), imgui (**bumped 1.76 → 1.92.8**), imguizmo. `external/` now holds only the non-vcpkg deps: CppSharp, DetourXS, nvapi, LeksysINI (LeksysINI is slated for *replacement* in Wave 3 STAB-05, not vcpkg migration).
- **2 commits, both pushed. Master at `d71feb2`.**
  - `874c01b` build(catch2): migrate vendored catch_amalgamated → vcpkg Catch2 v3
  - `d71feb2` build(imgui): migrate vendored imgui 1.76 + ImGuizmo → vcpkg imgui 1.92.8
- **Verified locally on v145 (VS 2026):** UtinniCore Debug + Release + RelWithDbgInfo all build + link against vcpkg imgui/imguizmo (static libs); native test suite passes **14 cases / 41 assertions** in Debug & Release.
- **⚠️ The live SWG-injected imgui render/input path is NOT smoke-tested** and can't be CI-validated. The riskiest change is the `hkWndProcHandler` rewrite (manual io-poking → `ImGui_ImplWin32_WndProcHandler`). **Needs an in-game smoke.**
- **CI: GREEN ✅** — `d71feb2` validated on the self-hosted v145 runner (run `26383517412`: Release build + net472 + CLI golden + native Debug/RelWithDbgInfo builds + Catch2 exe — all steps success, only `on-failure` upload steps skipped). The catch2-only run `874c01b` was cancelled mid-build as redundant (d71feb2 supersedes it).
- **⚠️ Runner is session-tied AGAIN.** It went offline this session (prior session's `run.cmd` had died); I restarted `C:\actions-runner\run.cmd`. It dies when this session ends. See Watch items.
- **Memory: 1 new** (`project-vcpkg-migration-complete`).

---

## The imgui bump — why it wasn't the multi-day port it first looked like

The PM handoff and a first read suggested imgui was a hard blocker (frozen prebuilt 1.76 lib + "custom imconfig" + custom `imgui_user`). Investigation flipped each concern:

| First fear | Reality |
|---|---|
| Custom `imconfig.h` (MyVec2/MyVec4/MyMatrix44/MyFunction) must be ported | Those lines are imgui's **stock commented-out example** (inside `/* */`), never activated. Stock vcpkg imgui is equivalent. |
| `imgui_user.{cpp,h}` ~30 bespoke widgets must be re-implemented against 1.92 | **Zero call sites** in UtinniCore or the plugins. Dropped, not ported. |
| Version bump breaks every plugin that includes imgui | **No C++ plugin includes imgui directly** — they use UtinniCore's exported API. Bump is contained to UtinniCore. |
| Huge API surface to port | **Every break was confined to one file**, `imgui_impl.cpp` (verified by a repo-wide removed-API sweep). |

imgui & imguizmo are a **coupled cluster** (imguizmo builds against imgui's internal API), so they migrated together. Both are **static libs** from vcpkg → no new runtime DLL dependency in the injected `UtinniCore.dll`.

### `imgui_impl.cpp` 1.76 → 1.92.8 port (the substantive change)

- includes `imgui/imgui.h` … → `<imgui.h>` (vcpkg headers at include root); dropped `#pragma comment(lib, "imgui/lib/imgui.lib")`.
- **`hkWndProcHandler`**: replaced the pre-1.87 manual `io.MouseDown[]` / `io.KeysDown[]` poking (`io.KeysDown` was **removed in 1.87**) with a single `ImGui_ImplWin32_WndProcHandler()` call — the canonical 1.92 integration. Forward-declared it locally because imgui_impl_win32.h wraps the decl in `#if 0`. **Diagnostics preserved** (Enter/Esc/F11/F12/WM_CHAR/focus logging); still falls through to SWG's wndproc (shared-input overlay).
- `IsAnyWindowHovered()` → `IsWindowHovered(ImGuiHoveredFlags_AnyWindow)` (removed).
- `GetKeyIndex(ImGuiKey_Escape)` → `ImGuiKey_Escape` (ImGuiKey is the native enum now).
- dropped `ImGuiWindowFlags_AlwaysUseWindowPadding` (obsoleted 1.90; no-op on top-level windows).
- `AddImage((void*)tex,…)` kept as-is — `ImTextureRef` has a `void*` legacy ctor.
- `game.cpp`: dropped dead `#include <imgui/imgui_user.h>`. `directx9.cpp`: `<imgui/imgui_impl_dx9.h>` → `<imgui_impl_dx9.h>`.
- `UtinniCore.vcxproj`: dropped `external/ImGuizmo/ImGuizmo.cpp` compile; link `imgui.lib`+`imguizmo.lib` (`imguid.lib` in Debug) from `vcpkg_installed` lib dirs.
- Deleted `external/imgui` (incl. orphan `imgui.vcxproj` — never in the .sln — and the prebuilt 1.76 `imgui.lib`) and `external/ImGuizmo`.

### catch2 (commit `874c01b`)

- 3 test sources: `<catch2/catch_amalgamated.hpp>` → `<catch2/catch_all.hpp>`.
- `UtinniCore.Tests.vcxproj`: dropped the amalgamated `.cpp`/`.hpp`; link `Catch2`+`Catch2Main` (the latter from `vcpkg_installed/.../lib/manual-link/`), `Catch2d`/`Catch2Maind` for Debug. `Catch2Main` supplies the runner `main()`; none is defined in-project.
- Deleted `external/catch2`.

---

## Local build/verify recipe (reproducible)

vcpkg is **not** bootstrapped on this machine by default (`VCPKG_ROOT` empty). To build locally you must produce `vcpkg_installed` first (CI does this automatically; it's gitignored):

```powershell
# 1. Bootstrap vcpkg + real install (mirrors CI; ~minutes; builds imgui w/ 3 features)
$vcpkgDir = "$env:TEMP\utinni-vcpkg"
if (-not (Test-Path "$vcpkgDir\.git")) { git clone --depth 1 https://github.com/microsoft/vcpkg "$vcpkgDir" }
& "$vcpkgDir\bootstrap-vcpkg.bat" -disableMetrics
& "$vcpkgDir\vcpkg.exe" install --triplet x86-windows --x-manifest-root="D:\Code\Utinni" --x-install-root="D:\Code\Utinni\vcpkg_installed"

# 2. Build (msbuild path; this machine has no setup-msbuild)
$mb = "D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
& $mb Utinni.sln /m /restore /p:Configuration=Release /p:Platform=x86 /p:RestorePackagesConfig=true   # full (fires CppSharp post-build)
& $mb Utinni.sln /m /p:Configuration=Debug          /p:Platform=x86 /t:UtinniCore_Tests               # native test (Debug)
& $mb Utinni.sln /m /p:Configuration=RelWithDbgInfo /p:Platform=x86 /t:UtinniCore_Tests
& $mb Utinni.sln /m /p:Configuration=Release        /p:Platform=x86 /t:UtinniCore_Tests
& "D:\Code\Utinni\bin\Release\UtinniCore.Tests.exe" --reporter console   # 14 cases / 41 assertions
```

> NOTE: the CppSharp post-build regenerates `UtinniCoreDotNet/Generated/UtinniCore.cs` with massive non-deterministic line-reordering churn (5674±) on every build. It's committed but CI never commits it. **Revert that churn** (`git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs`) before committing unrelated work — both commits this session did.

---

## State checkpoint

```
Branch:        master
HEAD:          d71feb2  build(imgui): migrate vendored imgui 1.76 + ImGuizmo -> vcpkg imgui 1.92.8
Prev:          874c01b  build(catch2): migrate vendored catch_amalgamated -> vcpkg Catch2 v3
Origin sync:   yes (both pushed)
Working tree:  clean (after handoff commit)
CI:            GREEN at d71feb2 (run 26383517412, self-hosted v145, all steps success/skipped)

Self-hosted runner:
  name:  poppops-windows-utinni @ C:\actions-runner
  state: ONLINE but SESSION-TIED (run.cmd restarted this session) — dies when session ends

Phase 6 progress:
  06-01 ✅  06-02 ✅  06-02b(vcpkg per-dep) ✅ COMPLETE  06-03 ⬜ ready (STAB-05)  06-04/05/06 ⬜ blocked-by-deps
```

---

## ⚠️ Watch items / carry-forward

| Item | Detail |
|---|---|
| **Live SWG imgui smoke REQUIRED before relying on the bump** | The 1.92.8 render + input path runs only in an injected live session, which CI can't exercise. Highest-risk change: `hkWndProcHandler` now routes input via `ImGui_ImplWin32_WndProcHandler` instead of manual io-poking. Smoke the in-game overlay: does it render, do mouse + keyboard (esp. text fields, Esc-to-cancel-gizmo) work? See `[[project-vcpkg-migration-complete]]`, `[[feedback-d3d9-hook-diagnosis]]`. |
| **Runner is session-tied — will go offline when this session ends** | Restarted via `C:\actions-runner\run.cmd` this session. For durability across reboots, reconfigure as a Windows service from an **admin** shell. Until then: if CI jobs queue, run `C:\actions-runner\run.cmd`. |
| **Runner job-dispatch race (lesson learned this session)** | Cancelling a run that the runner had already picked up, then restarting the listener, left subsequent pushed runs **stuck queued indefinitely** even though the runner showed `online/idle` — GitHub wasn't re-offering the jobs. Fix that worked: (1) `Stop-Process` the listener + start `run.cmd` fresh, (2) confirm `_diag\Runner_*.log` shows **"Listening for Jobs"**, (3) then `gh workflow run ci.yml --ref master` (fresh dispatch) — it was picked up in seconds. Don't cancel an in-flight run unless necessary; if runs stick queued, restart the listener and re-dispatch rather than waiting. |
| **vcpkg migration is DONE** | The PM handoff's "3 remain" watch item is resolved. Remaining `external/` deps are not vcpkg candidates: CppSharp (vendored fork, blocks the v145 CppSharp bump), DetourXS, nvapi, LeksysINI (Wave-3 *replacement* target). |
| **Chat-open D3D9 fullscreen** | `.planning/debug/chat-open-d3d9-fullscreen.md` — still queued, untouched (carried from prior handoffs). |
| **`[[project-loader-lock-harness-ci-flake]]` may be stale** | Dedicated self-hosted runner ≠ shared-contention; re-evaluate if it recurs. (Carried.) |

---

## What's next

The original PM-handoff goal still stands: **resume Wave 3 (06-03, STAB-05)** — DXSDK removal (`depth_texture.cpp` D3DXVECTOR3 → local struct + .vcxproj sweep), LeksysINI replacement, Catch2 fences. Once DXSDK removal lands, the CI "Verify DirectX SDK" step can be deleted too.

```
# 1. Bring runner online if the session was cleared:  C:\actions-runner\run.cmd
# 2. Confirm green baseline:  gh run list --branch master --limit 1
# 3. (Recommended) live in-game smoke of the imgui 1.92.8 overlay before Wave 3.
# 4. Resume:  /gsd-execute-phase 6 --wave 3
```

---

*Session closed: 2026-05-24 NIGHT. vcpkg migration completed end-to-end — catch2 + imgui 1.76→1.92.8 + ImGuizmo, all consumed from vcpkg, locally verified across 3 configs + native tests. The imgui bump's blast radius was contained to one file; its live render/input path still needs an in-game smoke. 2 commits `31df993`→`d71feb2` pushed.*
