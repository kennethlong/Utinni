# Session Handoff: 2026-05-25 PM — Wave 3 (06-03) shipped + CI-green; Wave 4 (06-04 CI-flake fixes) is next

> Picks up from `SESSION-HANDOFF-2026-05-25.md`. This session executed **Phase 6 Wave 3 (06-03)** end-to-end — DXSDK June 2010 removed + LeksysINI replaced + Catch2 fence — all CI-green on master. **Phase 6 is now 3/6 plans done (50%).** Next is **Wave 4 (06-04)**, the two CI-stability fixes that gate the 1.0-rc.1 tag.

---

## TL;DR

- **Wave 3 / 06-03 DONE, CI-green.** Master advanced `633d1b4` → `da17e5c` (4 commits). Both last STAB-05 open questions closed: **CON-O-08** (DXSDK June 2010 fully retired) and **CON-O-06** (LeksysINI → hand-rolled parser). All 8 CON-O-01..08 now dispositioned in `assessment.md`.
- **Next: Wave 4 / 06-04** — `06-04-PLAN.md` exists and is READY. Closes D-17: the loader-lock-harness 50 ms-threshold flake + the GameCallbacks `ForceGCCollect` AV flake. **Gates the 1.0-rc.1 tag** (06-06) because 1.0 success criterion #5 requires CI green on master.
- **Execution mode for this repo (IMPORTANT):** run C++ build waves **INLINE on the main tree**, NOT via a spawned `gsd-executor`. `workflow.use_worktrees=false` is set. Rationale + full recipe below. See `[[project-gsd-worktrees-off]]`.

---

## State checkpoint

```
Branch:        master
HEAD:          da17e5c  docs(06-03): plan complete — SUMMARY + tracking
Origin sync:   yes (pushed)
Working tree:  CLEAN
CI:            GREEN at da17e5c (run 26409716619, self-hosted v145, 8m1s)
Phase 6:       06-01 ✅  06-02 ✅  06-02b ✅  06-03 ✅ (this session)
               06-04 ⬜ READY (Wave 4 — THIS IS NEXT)   06-05 ⬜   06-06 ⬜

Self-hosted runner:  Runner.Listener pid 6432, ONLINE since 2026-05-24 23:36
                     (persists across sessions/clears; push-triggered CI). Verify with
                     `Get-Process Runner.Listener`; restart `C:\actions-runner\run.cmd` if OFFLINE.
```

Wave-3 commits (all on master, all CI-verified):
- `4f5b5b6` feat(06-03): remove DXSDK June 2010; close CON-O-08 + CON-B-03  *(own CI-green run)*
- `164ca59` feat(06-03): replace LeksysINI with custom INI parser inside UtINI::Impl; close CON-O-06
- `a18f503` test(06-03): Catch2 fence for custom INI parser (12 cases)  *(CI-green: 76 assertions / 26 cases)*
- `da17e5c` docs(06-03): plan complete — SUMMARY + tracking

---

## ⚙️ Build / execution recipe (this machine) — READ BEFORE EXECUTING

This is why we run inline rather than via a worktree-isolated subagent:

- **vcpkg manifest mode.** `vcpkg_installed/` already exists in the main checkout. A fresh git worktree would have none → 20-40 min recompile of imgui/spdlog/catch2/imguizmo. So: **no worktrees** (`workflow.use_worktrees=false` already set).
- **MSBuild via PowerShell, NOT Bash** (Bash mangles `/m` `/p:` via MSYS path conversion). Path:
  `D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`
- **Build pattern that worked all of Wave 3** (run in background to dodge the 10-min foreground cap; log to file; read only exit code + errors):
  ```powershell
  $msb = "D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
  & $msb Utinni.sln /m /restore /p:Configuration=Release /p:Platform=x86 /p:RestorePackagesConfig=true /nologo "/flp:logfile=build.log;verbosity=normal" "/clp:ErrorsOnly;Summary"
  "EXIT=$LASTEXITCODE"
  ```
  Each Wave-3 incremental build was ~10-18 s. Full native test run: `bin\Release\UtinniCore.Tests.exe --reporter console`.
- **CppSharp post-build churn:** every full build regenerates `UtinniCoreDotNet/Generated/UtinniCore.cs` (~5674-line reorder). **`git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs` before every commit** of unrelated work.
- **Push permission:** pre-authorized `git push origin master` for this repo (`[[feedback-push-permission]]`). CI fires on push to master.
- **Watch CI without polling:** `gh run watch <run-id> --exit-status` in a `run_in_background` Bash call → harness notifies on completion. Get the run id with `gh run list --branch master --limit 1 --json databaseId -q '.[0].databaseId'`.
- **Gotcha that bit me twice in Wave 3:** GSD acceptance greps like "grep X returns zero matches" are **literal** — the token must be absent even in code *comments*. Reword comments (e.g. "the legacy DXSDK vector type"); keep the historical name only in non-gated docs. See `[[feedback-gsd-grep-gate-hygiene]]`.

---

## What Wave 4 / 06-04 is (3 tasks)

`06-04-PLAN.md` — closes D-17. Sequencing is strict: investigation → harness fix → test fix, **each code commit must show green CI before the next starts**.

- **Task 1 — `docs(06-04):` investigation (no code).** Write `06-04-FLAKE-INVESTIGATION.md` with two sections (Loader-Lock-Harness, GameCallbacks). For EACH flake, collect past-failure evidence via `gh run list --branch master --workflow=CI --limit 50 --json conclusion,databaseId,headSha,createdAt --jq '.[]|select(.conclusion=="failure")'` + `gh run view <ID> --log-failed`, then **select exactly one mitigation OPT** per flake and write `Selected:`.
- **Task 2 — `fix(06-04):` loader-lock-harness** (`Utinni.LoaderLockHarness/main.cpp`). Current code: one `LoadLibraryA` + one `QueryPerformanceCounter`, `return (elapsedMs < 50.0) ? 0 : 1;`. Implement the chosen OPT (A=best-of-3 min / B=warmup+median-of-5 / C=raise threshold w/ empirical comment / D=in-DLL `getDllMainEntryExitMs` export). Add a `#ifdef LOADER_LOCK_HARNESS_REGRESSION_PROBE` block + an OPT cross-ref comment.
- **Task 3 — `test(06-04):` GameCallbacks** (`UtinniCoreDotNet.Tests/GameCallbacksTests.cs`). The `RegisterCallback_ForceGCCollect_CallbackStillFiresWithoutAV` test's Probe 1 (native `Utinni_TriggerInstallCallbacks` P/Invoke) AVs intermittently in the test process. Implement chosen OPT (A=sentinel-probe gate / B=catch SEH+AccessViolationException / C=split into two methods w/ `[SkippableFact]` NuGet). Add the regression-fence comment.
- **CI gate (heavier than Wave 3):** each fix must show **3 consecutive green CI runs on master** (`gh run list --branch master --limit 3 --json conclusion -q '[.[].conclusion]'` → three "success"). Trigger extra runs via `gh run rerun <id>` or `gh workflow run CI` (workflow_dispatch is enabled in ci.yml).

**Preliminary lean (NOT decided — Task 1 decides with evidence):** loader-lock OPT-B (warmup + median-of-5, self-contained, no ABI change); GameCallbacks OPT-A (sentinel probe + `File.Exists` gate, avoids adding the SkippableFact NuGet dep that OPT-C needs). Let the evidence in Task 1 confirm.

---

## ⚠️ Watch items / carry-forward

| Item | Detail |
|---|---|
| **Runner durability** | pid 6432 up since 2026-05-24; survives /clear. If a pushed run queues forever, restart `C:\actions-runner\run.cmd`. For permanence: `svc.cmd install` from an admin shell (`[[project-self-hosted-ci]]`). |
| **3-consecutive-green gate** | 06-04 Tasks 2 & 3 each need 3 green CI runs. Budget ~6-8 min per run; use `gh run rerun`/`workflow_dispatch` to get runs 2 & 3 without empty commits. |
| **Ancillary LeksysINI doc refs (Wave-3 leftover)** | `docs/ai/build.md`, `docs/ai/core.md`, `docs/ai/index.md`, generated `docs/*.html` still list LeksysINI as a vendored dep. Out of 06-03 scope; fold into a future docs-regen pass (06-06 area). |
| **CI Node 20 deprecation (pre-existing, non-fatal)** | bump `actions/cache@v4`/`checkout@v4`/`setup-msbuild@v2` before 2026-06-02. Carried. |

---

## Kickoff (after `/clear`)

```
# 1. Confirm runner online:  Get-Process Runner.Listener   (else C:\actions-runner\run.cmd)
# 2. Confirm green baseline:  gh run list --branch master --limit 1
# 3. Execute Wave 4:  /gsd-execute-phase 6 --wave 4
#    -> Single-plan wave (06-04). Run it INLINE per the recipe above; do NOT spawn a
#       worktree-isolated gsd-executor (use_worktrees is already false).
```

> After 06-04: Wave 5 = 06-05 (clang-format sweep + dead-code purge + STAB-04 audit), Wave 6 = 06-06 (Tier-4 TESTING.md + release.yml + WiX MSI + v1.0.0-rc.1 tag). 06-04 unblocks the 06-05 cleanup sweep.

---

*Session closed: 2026-05-25 PM. Wave 3 (06-03) shipped DXSDK removal + LeksysINI replacement + 12-case Catch2 fence, 4 commits, all CI-green; Phase 6 at 3/6. New memories: `[[project-gsd-worktrees-off]]`, `[[feedback-gsd-grep-gate-hygiene]]`. Wave 4 (06-04 CI-flake fixes) is planned and ready.*
