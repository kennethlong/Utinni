# Session Handoff: 2026-05-24 PM — CI fully GREEN on self-hosted v145 runner; spdlog migrated to vcpkg

> Picks up from `SESSION-HANDOFF-2026-05-24.md` (Phase 6 Waves 1+2 shipped, CI failing on a vcpkg version-database miss). This session set out to fix that one CI failure before Wave 3 — and discovered **CI had never actually run to completion on the v145 toolset**. Fixing it properly meant moving CI to a self-hosted runner (v145/VS 2026 is Insiders-only) and clearing a chain of 7 latent issues, most of them un-CI-validated Wave 2 changes. **CI is now fully green.** Master advanced `c011883` → `b7301ac`, all pushed.

---

## TL;DR

- **Drive move was clean.** Everything survived the relocation to the new drive — git tree, remotes (`kennethlong/Utinni`), sibling `D:/Code/UtinniPlugins`, and the VS 2026 toolchain (`D:\Program Files\Microsoft Visual Studio\18\Community`). No fixups needed.
- **CI is GREEN for the first time since before the v145 bump.** Run `b7301ac` passed every step (full Release build + net472 + CLI golden + native Debug/RelWithDbgInfo builds + Catch2 exe). URL: https://github.com/kennethlong/Utinni/actions/runs/26381046158
- **Root realization:** CI failed on vcpkg on *every* v145 run, so the v145 build + the Wave-2 test/config changes had **never been validated by CI**. Each fix peeled back to reveal the next.
- **CI now runs on a SELF-HOSTED runner on this machine** (`POPPOPS-WINDOWS`). v145 (VS 2026) is Insiders-only — no GA bootstrapper channel exists (`aka.ms/vs/18/release/...` → bing; only `/insiders/` resolves), so GitHub-hosted runners can't build this project. Decision made via AskUserQuestion. See `[[project-self-hosted-ci]]`.
- **spdlog migrated vendored 1.6.0 → vcpkg 1.17** (header-only). User chose the proper fix over a band-aid. This was the deferred "06-02b" per-dep migration, scoped to spdlog. Verified locally on v145 (Debug+Release).
- **7 commits, all pushed. Master at `b7301ac`, tree clean.**
- **⚠️ The runner is currently session-tied** (online via a background `run.cmd` started this session). It will go OFFLINE when this session/context ends. See Watch items for how to bring it back.
- **Memory: 1 new** (`project-self-hosted-ci`).

---

## What happened — the 7-fix chain to green

CI had never reached completion on v145, so fixing the first failure exposed the next, etc.:

| # | Commit | Issue | Fix |
|---|---|---|---|
| 1 | `9c683c9` | `vcpkg install` failed: "no version database entry for imgui at 1.92.0". NOT a stale baseline (`aa40adda` is microsoft/vcpkg HEAD) — vcpkg simply never published imgui **1.92.0** (catalog jumps 1.91.9→1.92.6). | `version>=` floor `1.92.0`→`1.92.6` (lowest real 1.92.x). Verified all 4 floors exist in catalog. |
| 2 | `ca8fbf6` | Next wall: `Verify v145 build tools` — runner (windows-2022) lacks v145; the on-the-fly install pulled `aka.ms/vs/18/release/vs_buildtools.exe` which **302-redirects to bing** (no GA v18 channel). v145 is Insiders-only. | **Moved CI to self-hosted runner** on this machine. All "install toolchain" steps → **verify-only** (the old DXSDK step *uninstalled VC++ 2010 runtimes* — must never run on a real box). Removed `pull_request` trigger (RCE risk on public repo). |
| 3 | `f90bd3f` | `pwsh: command not found` — this machine has only Windows PowerShell 5.1, not PowerShell 7 (which GitHub-hosted images ship). | Job-level `defaults.run.shell: powershell` + all `shell: pwsh`→`powershell`. Scripts are 5.1-safe. |
| 4 | `9a40130` | `OutputSinkRoundTripTests.cpp` (Wave-2 file, first-ever CI compile): C2039 `spdlog::logger` not a member — relied on a transitive include vendored spdlog 1.6.0 doesn't provide. | Add `#include <spdlog/logger.h>`. |
| 5 | `4dc7aef` | Link error: 5 unresolved `utinni::log::*` dllimport symbols. The `UtinniCore` ProjectReference had `LinkLibraryDependencies=false` (build-order only); this is the only native test calling exported symbols. | `LinkLibraryDependencies=true`. Safe: UtinniCore's DllMain is trivial (all injection work is in `utinni_init`). |
| 6 | `ea721b6` | net472 tests: 2/131 failed — `VsixManifestTests` expected `[16.0,18.0)` but Wave 2 widened the manifest to `[16.0,19.0)` (VS 2026 = v18). Tests never updated. | Update expectations + method names + comment to `[16.0,19.0)`. |
| 7 | `b7301ac` | `Build native tests (Debug)`: vendored spdlog 1.6.0's bundled **fmt v6** uses `stdext::checked_array_iterator`, which **MSVC 14.5x removed** (Debug-only, via `_SECURE_SCL`). The v145 bump broke the Debug build. | **Migrated spdlog 1.6.0 → vcpkg 1.17** (header-only, modern fmt). Delete `external/spdlog` (89 files); add vcpkg include + `SPDLOG_FMT_EXTERNAL;FMT_HEADER_ONLY;FMT_UNICODE=0` to UtinniCore + UtinniCore.Tests. |

---

## spdlog migration details (the substantive change)

- vcpkg ships spdlog **without bundled fmt** (built with the fmt port) → must define `SPDLOG_FMT_EXTERNAL` (uses `<fmt/...>`, present in vcpkg_installed) + `FMT_HEADER_ONLY` (keep prior no-lib header-only usage — no `spdlog.lib`/`fmt.lib` linkage).
- `FMT_UNICODE=0` bypasses modern fmt's `static_assert(!FMT_UNICODE || use_utf8)` **without** adding `/utf-8` — deliberately avoids changing the compiler execution charset, so SWG narrow-string handling in the injected runtime is byte-identical (the live path CI can't smoke-test). Chosen over `/utf-8` for that reason.
- Include ordering: `external` first, `vcpkg_installed\x86-windows\include` appended → imgui/catch2/imguizmo stay vendored; only spdlog+fmt resolve from vcpkg.
- Only 2 consumers (`UtinniCore/utility/log.cpp`, `UtinniCore.Tests/Log/OutputSinkRoundTripTests.cpp`). spdlog API used is unchanged 1.6→1.17.
- The `<spdlog/logger.h>` include from fix #4 stays valid (1.17 has it).

---

## State checkpoint

```
Branch:           master
HEAD:             b7301ac  build(spdlog): migrate vendored spdlog 1.6.0 -> vcpkg 1.17
Origin sync:      yes (all 7 commits pushed)
Working tree:     clean
CI status:        GREEN at b7301ac (run 26381046158) — all steps success/skipped

Self-hosted runner:
  name:    poppops-windows-utinni
  install: C:\actions-runner   (on C: — the 1.5 TB drive)
  labels:  [self-hosted, Windows, X64, utinni-v145]
  state:   ONLINE but SESSION-TIED (background run.cmd) — see Watch items

Phase 6 progress:
  06-01 ✅  06-02 ✅  06-03 ⬜ ready (STAB-05)  06-04/05/06 ⬜ blocked-by-deps
```

---

## ⚠️ Watch items / carry-forward

| Item | Detail |
|---|---|
| **Runner is session-tied — will go offline when this session ends** | It was brought online via a background `C:\actions-runner\run.cmd` started this session. When context is cleared / the session ends, that process dies and the runner goes offline → CI jobs queue (don't fail) until it's back. **To reactivate next session:** run `C:\actions-runner\run.cmd` in a terminal (non-admin, online in seconds). **For durability across reboots:** reconfigure as a Windows service from an **admin** shell — `cd C:\actions-runner; .\config.cmd --runasservice ...` (this runner v2.334.0 has no `svc.cmd`; service install is via the `--runasservice` config flag, may need a Windows logon account/password). Optional; the interactive `run.cmd` is fine for now. |
| **Per-dep vcpkg migration: spdlog DONE, 3 remain** | spdlog is now consumed from vcpkg. catch2 / imgui / imguizmo are still vendored under `external/` (declared in vcpkg.json but not consumed). Finishing them is optional polish, not blocking. |
| **Local builds now require `vcpkg_installed`** | Deleting `external/spdlog` means `<spdlog/...>` resolves only via `$(SolutionDir)vcpkg_installed\x86-windows\include`. Local dev must run `vcpkg install` first (CI produces it automatically). A partial copy (headers only) was staged locally this session for verification — gitignored. |
| **`[[project-loader-lock-harness-ci-flake]]` memory may be stale** | It blamed "shared windows-2022 runners under contention." CI is now a dedicated self-hosted runner (no contention), so that flake likely won't recur. Re-evaluate if it reappears. |
| **`pull_request` CI was removed** | Self-hosted + public repo = don't run untrusted fork PRs. If contributor PR CI is wanted later, add a SEPARATE hosted lane (windows-2022, `/p:PlatformToolset=v143` — the C++ builds fine on v143; CppSharp parser pin is toolset-independent). |
| **Chat-open D3D9 fullscreen** | `.planning/debug/chat-open-d3d9-fullscreen.md` — still queued, untouched (carried from prior handoffs). |

---

## What's next

**The original goal stands: resume Wave 3.** With CI green, proceed to STAB-05 closure:

```
# 1. Bring the runner back online if the session was cleared:
#    (in a terminal)  C:\actions-runner\run.cmd
# 2. Confirm green baseline:
gh run list --branch master --limit 1
# 3. Resume Phase 6 Wave 3 (STAB-05: DXSDK removal + LeksysINI replacement + Catch2 fences):
/gsd-execute-phase 6 --wave 3
```

Note: **Wave 3 (06-03) includes DXSDK removal** (`depth_texture.cpp` D3DXVECTOR3 → local struct + .vcxproj sweep). Once that lands, the CI "Verify DirectX SDK" step can be deleted too — the user already asked about this; it's queued, not done.

---

## Memory updates this session

- **NEW `project-self-hosted-ci`** — CI runs on a self-hosted runner on this machine (v145/VS2026 Insiders-only); runner location/name/labels, verify-only steps, push-only trigger, durability caveat, and the green-day fix chain. Cross-refs `[[project-vs2026-toolchain]]`, `[[project-vs2026-cppsharp-block]]`.

No memories deleted. (Consider trimming `[[project-loader-lock-harness-ci-flake]]` if the flake doesn't recur on the dedicated runner.)

---

## Reproduction recipe (for resume)

```bash
cat .planning/SESSION-HANDOFF-2026-05-24-PM.md
git log --oneline -8                 # master at b7301ac
git status --short                   # clean
gh api repos/kennethlong/Utinni/actions/runners --jq '.runners[] | "\(.name): \(.status)"'   # may be offline if session ended
# if offline: start C:\actions-runner\run.cmd, then resume Wave 3
```

---

*Session closed: 2026-05-24 PM. CI green for the first time on v145; self-hosted runner stood up; spdlog migrated to vcpkg 1.17; 7 commits `c011883`→`b7301ac` pushed. The "30-minute vcpkg fix" became a full CI excavation — but the pipeline now genuinely validates the v145 build end-to-end.*
