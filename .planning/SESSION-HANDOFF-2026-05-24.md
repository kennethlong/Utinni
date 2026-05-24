# Session Handoff: 2026-05-24 — Phase 6 Waves 1 + 2 shipped (v145 toolset live, CppSharp parser pinned)

> Picks up from `SESSION-HANDOFF-2026-05-23-PM.md` (R-A migration live-verified, debug branches pruned, chat-open D3D9 bug queued). This session executed Phase 6 Waves 1 + 2 in full: overlay-debug investigation (Tier-4 sign-off) and dep-bumps + v145 toolset + CppSharp parser redirect (Path 1). Two pre-merge consults (CODEX + cursor) validated the codegen reshuffle. Master advanced from `0dc8646` → `08a5c1f`, pushed. **CI failed on the first new run** for a fixable vcpkg-version-database issue — see Watch items.

---

## TL;DR

- **Phase 6 Wave 1 (06-01) shipped.** ImGui Demo screen rendered end-to-end over live SWG; Tier-4 sign-off in `06-01-VERIFICATION.md`; diag tripwires landed dormant (`g_showDemoWindowProbe = false`, debug-level logs). **HUD-style overlay directive captured** for Wave-1 TJT subpanels — chromeless `NoBackground | NoTitleBar | NoDecoration`, not floating windows on the game view.
- **Phase 6 Wave 2 (06-02) shipped via Path 1.** vcpkg manifest mode (4 of 7 deps portable; 3 kept-vendored with documented broken-port evidence) + OutputSink CON-N-09 fence + PlatformToolset v142 → v145 sweep + VSIX widen `[16.0,19.0)` + CppSharp parser pinned to VS 2019 14.29 MSVC STL.
- **CppSharp / MSVC 14.5x blocker resolved.** Vendored CppSharp 0.10.5 (clang 11) cannot parse VS 2026's STL; Path 1 points the parser at VS 2019 14.29 STL (the clang-11 original-pairing STL) while the build uses v145. Implementation in `UtinniCoreDotNetGen/Program.cs::ConfigureCppSharpParserStl()` with three-tier resolver (env override → vswhere → default-path probe). CppSharp blocker memory updated to RESOLVED. Two alternative paths (upgrade CppSharp; defer v145) documented in research note.
- **Pre-merge cross-AI review fired.** CODEX (paste-in) + cursor-agent (manual paste, my CLI flaked) both verdicted **MERGE**. Cursor went further than CODEX with per-type block hash check: **119/119 partial-class blocks byte-identical** between v142 baseline and Path 1 regen; only inter-type positions changed. CODEX flagged 4 pre-merge cleanups (fail-hard builtins, numeric `System.Version` ordering, BOM strip, untracked-file confirmation) → all applied as `adc72f8`.
- **Path 1-regenerated `UtinniCore.cs` committed as new tracked baseline** (`d69988d`) per cursor's preferred follow-up. Eliminates perpetual `git status` codegen drift on future builds.
- **CI failed on first push to `08a5c1f`.** vcpkg version-database doesn't have an entry for imgui `1.92.0` at the pinned baseline `aa40adda...`. NOT a Path 1 regression — it's the vcpkg manifest from `0ab49ae` declaring a version the pinned registry doesn't know. Fixable (see Watch items).
- **Master at `08a5c1f`, origin synced.** Working tree clean. Both worktrees removed.
- **Memory updates: 2** (new `project-hud-style-overlay-directive`; updated `project-vs2026-cppsharp-block` from blocker → resolved with full Path 1 context).

---

## What happened (long, because two waves)

### Wave 1 — Plan 06-01 (overlay-debug investigation)

| Step | What | Outcome |
|---|---|---|
| Dispatch executor | gsd-executor, worktree isolation, EXPECTED_BASE `0dc8646` | Returned with structured CHECKPOINT after Tasks 1+2 (pattern-scan disposition + diag tripwires + ShowDemoWindow probe) |
| **Discovery: first-executor's commits auto-merged to master by Claude Code's worktree harness** | git log on the main checkout showed `171a5ec` reachable from master pre-Task-3 | Continuation agent had to redo Tasks 1+2 on a fresh worktree so all three tasks could land as a clean linear block |
| Worktree dispatch race | I `cd`'d into the worktree in an earlier PS call and the cwd persisted; an attempted merge landed on the first worktree's branch instead of master | Caught via `git branch --show-current` returning the worktree branch name; switched back via `cd /d/Code/Utinni` + `git merge --ff-only` |
| **User launched from main-checkout path, not worktree** | utinni.log showed no diag tripwires fired → realized old DLL was loaded | Backed up main DLL → copied worktree's UtinniCore.dll over → user re-ran. **Both tripwires fired exactly once.** Demo screen rendered end-to-end across all 7 widget categories |
| **HUD aesthetic directive captured** | User noted "we are gonna want a heads up display style interface, not a window sitting on the game view" | Added as Design Note section in `06-01-DEMO-PROBE-NOTES.md` + memory `project-hud-style-overlay-directive.md` |
| Continuation agent for Task 3 | Maintainer-signed Tier-4 evidence + TESTING.md Tier-4 row + diag rollback (flag flipped, info → debug demotion) | Landed atomic commit; worktree merged to master |
| Push | `5f63b36..origin/master` | Pushed cleanly |

### Wave 2 — Plan 06-02 (vcpkg + dep bumps + v145 toolset + CppSharp parser redirect)

| Step | What | Outcome |
|---|---|---|
| Dispatch executor | gsd-executor, worktree isolation | 4 commits landed (vcpkg manifest, OutputSink test, v142→v144 toolset sweep, plan summary). **Per-dep migration deferred (Rule 4)** — executor explicitly couldn't safely run iterative vcpkg install + .vcxproj rewiring + build-fail-fix loop in headless mode |
| Verify deferred build | I tried to build the worktree | **MSB8020: v144 toolset not found.** Executor's value was wrong |
| **User correction: VS 2026 toolset is v145, not v144** | Verified by inspecting `MSBuild\Microsoft\VC\v180\Platforms\Win32\PlatformToolsets\` — only `v145/` and `ClangCL/` exist | Bulk fix v144 → v145 across 19 files (13 .vcxproj/.props + 4 phase-6 plan docs + ROADMAP + ci.yml). Build retried |
| Verify v145 build | msbuild on v145 | **C++ compiles. CppSharp codegen fails with 16+ parse errors in MSVC 14.5x's STL headers.** clang 11 can't parse C++23 features (`__builtin_verbose_trap`, static lambda operator(), new tuple<T> asserts) |
| **Phase 6 paused** | I summarized the blocker, offered three resolution paths, user opted to "pause Phase 6 here; investigate CppSharp separately" | Worktree parked locked; commit `83a8056` was the v145 fixup; TODO + memory + STATE captured the blocker state. `63609c1` pushed to origin |
| **User flagged: "CppSharp works with VS 2019 out of the box; research what it'd take to get on the latest toolset"** | Dispatched gsd-research agent in background | ~13 min later: research doc at `.planning/research/cppsharp-msvc-14.5-upgrade.md`. Key findings: vendored CppSharp = 0.10.5 (clang 11); upstream v1.2 (clang 19, Nov 2025) supports MSVC 14.4x = v143 only; **no CppSharp release supports v145 (would need clang 20+)**. Three paths analyzed; Path 1 (parser-include redirect to VS 2019 14.29 STL) recommended |
| **User: "what toolset is v1.2 running on?"** | This question reframed the decision — Path 2 (upgrade) wouldn't reach v145. Path 1 became the only viable path to v145 right now | Confirmed; user asked to "dispatch Path 1; should we consult our friends or wait" |
| **My recommendation: consult AFTER executor produces draft, BEFORE merging.** Approach review = research doc; code review = consult later | Dispatched Path 1 executor in background | 7 + 1 commits, build verified clean, codegen diff = 2,930 lines but sorted-set equality = zero. Generated UtinniCore.cs reset to baseline (not committed) per recurring exclude-codegen-drift rule |
| **Pre-merge cross-AI review** | User: "Lets send to both codex and cursor" | Drafted `REVIEW-PROMPT.md`; pasted into CODEX; user pasted into cursor in parallel (my cursor-agent CLI was flaky in this environment) |
| CODEX verdict | **Accept + 4 pre-merge cleanups** (fail-hard builtins, numeric Version ordering, BOM strip, untracked-file confirmation). Real risk = AST/layout truth, not linking | Applied all 4 as commit `adc72f8`. Build re-verified clean on v145 (CS0104 `Version` ambiguity between `CppSharp.Version` and `System.Version` caught + fixed during the verify cycle by qualifying as `System.Version`) |
| Cursor verdict | **Accept; merge as-is.** Independently reproduced executor's diff and went one step further: **119/119 partial-class blocks byte-identical** between baseline and regen, only inter-type block positions changed. Explicitly verified absence of `[ModuleInitializer]`/`[ComImport]`/explicit `.cctor`/`[TypeInitializer]` in the generated file (the order-sensitive C# constructs). Strongly recommends committing the Path 1-regenerated baseline post-merge | All boxes checked |
| Merge | `git merge --no-ff worktree-agent-a9558795aaec945c4`. Conflict on `.gitignore` (master added `.claude/` ignore; worktree branch added `/vcpkg_installed/` ignore) | Resolved by keeping both sections. Merge commit `2f57dfa` |
| Path 1-regen baseline | Cursor's preferred follow-up: commit the regenerated UtinniCore.cs so subsequent builds don't perpetually dirty the working tree | Rebuilt on master; 118/118 partial-class set identical; committed as `d69988d` (151 add / 151 delete — actual line-level delta is small) |
| Tracking + push | `08a5c1f` updates ROADMAP, STATE, moves CppSharp TODO to completed | `63609c1..08a5c1f` pushed cleanly |
| **CI failed on first run** | `08a5c1f` triggered CI; failed at the `vcpkg install` step: `error: no version database entry for imgui at 1.92.0` | Captured below in Watch items |

---

## Path 1 mechanism in one paragraph

CppSharp 0.10.5 ships clang 11. MSVC STL has a header-resident clang-version gate in `yvals_core.h`: VS 2019 14.29 says `#if __clang_major__ < 11`, VS 2022 14.44 requires clang 19, VS 2026 14.5x requires clang 20. Path 1 sets `driver.ParserOptions.NoStandardIncludes = true` (suppresses LLVM 11's auto-detect of newest VS), explicitly `AddSystemIncludeDirs(...)` to VS 2019 14.29 MSVC + Windows SDK 10.0.19041 (the clang-11 original-pairing STL — guaranteed parseable), re-attaches `driver.ParserOptions.BuiltinsDir` (clang intrinsics), and adds `_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH` as belt-and-suspenders. The parsed AST has the same types as before; the v145-compiled `UtinniCore.dll` has the same ABI surface (CppSharp marshals layout from AST, not from live STL, and Utinni already hand-curates `StdEdited.cs` per CON-O-05 so std::string-class layout drift is pre-mitigated). Path 1 is parse-time scaffolding only.

---

## What's next

**Phase 6 status: 2/6 plans complete.** Remaining waves:

| Wave | Plan | What | Autonomy |
|---|---|---|---|
| 3 | 06-03 | STAB-05 open questions — DXSDK removal (`depth_texture.cpp` `D3DXVECTOR3` → local 3-float struct + .vcxproj sweep) + LeksysINI replacement (custom INI parser inside `UtINI::Impl`) + Catch2 fence tests | Autonomous |
| 4 | 06-04 | CI flake fixes — loader-lock-harness 50ms threshold + GameCallbacks ForceGCCollect AV. Each atomic + regression-targeted | Autonomous |
| 5 | 06-05 | Cleanups + STAB-04 audit — full-repo clang-format atomic commit + TJT.ico cross-repo eject to UtinniPlugins + polish bundle + preservation grep tests | Autonomous, **cross-repo to UtinniPlugins** |
| 6 | 06-06 | TEST-04 Tier-4 doc + WiX MSI installer + maintainer Tier-4 UAT + v1.0.0-rc.1 tag + GitHub Pre-release | **Non-autonomous** (manual UAT + tag push) |

**Recommendation for resume:** fix the CI vcpkg-version issue first (see Watch items) so subsequent waves' CI runs are clean, then **`/gsd-execute-phase 6 --wave 3`** to start STAB-05 closure.

---

## Watch items / carry-forward

| Item | Detail |
|---|---|
| **CI failed on `08a5c1f` — vcpkg version-database miss** | First post-merge CI run failed at the new `vcpkg install` step. Root cause: vcpkg manifest at `vcpkg.json` declares `imgui` at version `1.92.0`; the pinned registry baseline `aa40adda5352e87655b8583cfb2451d5e9e276fd` has no entry for that version. Local dev works because the user has a different vcpkg cache; CI sees only the pinned baseline. Fix options: (a) bump the baseline pin to a newer microsoft/vcpkg commit that has imgui ≥ 1.92.0 in its version database; (b) downgrade the imgui manifest version to one the current baseline knows about (run `vcpkg search imgui --baseline aa40adda...` to see what's available); (c) use `version>=` constraints rather than exact pins. This is **not** a Path 1 regression — it's a manifest-baseline mismatch from the original executor's research. **Estimated fix: 30 min.** Suggest opening this as the first work on resume, before Wave 3. |
| **Per-dep vcpkg migration deferred to 06-02b** | vcpkg manifest declares 4 portable deps (catch2, spdlog, imgui, imguizmo) but the build still uses vendored `external/<dep>/` copies. Migration = delete `external/<dep>/` + rewire .vcxproj include/lib paths to vcpkg-installed. Was originally Task 2's scope but the executor escalated as Rule 4 (iterative vcpkg-install + build-fail-fix loop unsafe in headless mode). **Not blocking v1.0-rc.1**; follow-on plan after Phase 6 closes. Documented in `06-02-SUMMARY.md` "Notes for Follow-On Plan". |
| **CppSharp upgrade to v1.2 (clang 19)** | Would lock in v143 (VS 2022) but doesn't reach v145; would require net4.7.2 → net9.0 migration of `UtinniCoreDotNetGen` + PostBuildEvent rewrite (NuGet only ships net9/net10). Deferred to Phase 7+ milestone work. Tracked in `[[project-vs2026-cppsharp-block]]` memory. Path 1's parser-include pin is the v145-today answer. |
| **Path 1 limits if Utinni C++ adopts C++23 STL features** | Today UtinniCore C++ has zero usage of `<format>`, `<ranges>`, `<concepts>`, `<expected>`, `<coroutine>`, `<span>`, `<barrier>`, `<latch>`, `<stop_token>`, `<jthread>`, `<source_location>`, `<chrono>` (verified by orchestrator grep). If any of those become an `#include` in the project, the parser-include pin breaks. Mitigation: CODEX recommended a structural-binding-diff CI harness as future safety; deferred for now. |
| **CODEX flagged: post-merge structural-diff harness for future safety** | The current safety is sorted-set + per-type-block-hash + cursor-verified semantic equivalence on this one diff. CODEX suggested adding a CI step that compares regen vs tracked baseline via per-type block hash on every build, so reorder-only drift doesn't fail the build but signature drift would. Future safeguard; not required for V1. |
| **CppSharp regen drift was eliminated (this session)** | After `d69988d`, the tracked `UtinniCoreDotNet/Generated/UtinniCore.cs` matches what subsequent v145 builds will produce. Future fresh checkouts won't see the recurring `M Generated/UtinniCore.cs` on every build. Reverts the previous-3+-sessions "restore before staging" workflow. |
| **Cursor-agent CLI flake on Windows** | `cursor-agent.cmd -p --mode ask --output-format text "<prompt>"` invocation from PowerShell `Start-Job` or harness `Bash` background didn't produce output reliably in this environment. User pasted manually instead. Either the CLI's stdin/positional-arg handling is buggy on this PS+Bash mixed environment, or there's a profile/auth issue. **Not investigating further this session.** When future automated cursor-agent consults are wanted, prefer the manual-paste flow until the CLI quirks are diagnosed. |
| **Chat-open D3D9 fullscreen still queued from yesterday** | `.planning/debug/chat-open-d3d9-fullscreen.md` — Phase H side-effect, not regression from this session. Untouched. |

---

## Memory updates this session

- **NEW `project-hud-style-overlay-directive`** — Wave-1 TJT subpanels (Phases 7-11 per DEC-C4) default to chromeless HUD overlays (`NoBackground | NoTitleBar | NoDecoration | NoMove | NoResize`), not floating windows on the game view. Captured during 06-01 live SWG Demo screen exercise; documented in `06-01-DEMO-PROBE-NOTES.md` with ranked alternatives.
- **UPDATED `project-vs2026-cppsharp-block`** — rewrote from blocker → **RESOLVED 2026-05-24 via Path 1**. Captured: vendored CppSharp 0.10.5 identification, MSVC STL clang-version gate, three-tier path resolver implementation, two alternatives weighed (CppSharp v1.2 upgrade deferred to Phase 7+; Path 3 defer rejected), CODEX + cursor verdict provenance, Path 1 limits (C++23 STL feature watch), cross-refs.

No memories deleted.

---

## File state checkpoint

```
Branch:           master
HEAD:             08a5c1f docs(phase-6): mark Wave 2 complete; resolve CppSharp TODO
Origin sync:      yes (08a5c1f pushed)
Working tree:     clean
Worktrees:        only the main checkout (both Path 1 and the parked v144-fixup worktrees removed)
CI status:        FAILED at 08a5c1f on vcpkg version-database miss (see Watch items)

Phase 6 progress:
  06-01 ✅ complete  (overlay-debug investigation, Tier-4 sign-off)
  06-02 ✅ complete  (vcpkg manifest + v145 toolset + Path 1 parser pin)
  06-03 ⬜ ready     (STAB-05 open questions)
  06-04 ⬜ blocked   (depends on 06-03)
  06-05 ⬜ blocked   (depends on 06-04, cross-repo to UtinniPlugins)
  06-06 ⬜ blocked   (depends on 06-05, non-autonomous, v1.0.0-rc.1 tag)
```

---

## Reproduction recipe (for resume)

```bash
# Confirm state
cat .planning/SESSION-HANDOFF-2026-05-24.md
git log --oneline -5                                  # master at 08a5c1f
git worktree list                                     # only D:/Code/Utinni
git status --short                                    # clean

# Check CI status
gh run list --branch master --limit 3 --json conclusion,headSha,createdAt

# If CI failure on 08a5c1f is still red, first action on resume:
# inspect the vcpkg version-database miss, then fix manifest baseline
gh run view <run-id> --log-failed | grep -i "no version database"

# Resume Wave 3 after CI green:
/gsd-execute-phase 6 --wave 3
```

---

*Session closed: 2026-05-24. Master ahead of yesterday's `0dc8646` by 24 commits, 2 plans complete, CppSharp blocker resolved, HUD aesthetic captured for Wave-1, CI fix queued.*
