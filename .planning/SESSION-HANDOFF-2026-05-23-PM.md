# Session Handoff: 2026-05-23 (PM) — R-A migration smoke PASSED, branches pruned, chat-open D3D9 fullscreen filed

> Picks up from `SESSION-HANDOFF-2026-05-23.md` (R-A heap-free migration shipped, live smoke deferred). This session ran the deferred live SWG smoke, confirmed the migration is solid across many warps, pruned the 12 debug cycle branches, and surfaced one new bug (chat-open via Enter triggers true D3D9 exclusive fullscreen) that has been triaged + queued. Master is **still at `765738c` + handoff `ab0270a`** — no new commits this session. Working tree clean.

---

## TL;DR

- **R-A heap-free migration LIVE-VERIFIED.** User ran many TJT-driven warps in real SWGEmu; no AV at `0x0051fb0a` or related sites. Naked-after-warp baseline holds. The `dispatchSnapshot` pattern is solid across all 11 R-A files. Bug class fully closed.
- **12 debug branches pruned** (`debug/03-scene-change-av-cycle{1..11}*` + `cycle10-revert-creature`). Local-only deletes; no remote refs touched.
- **New bug queued:** chat-open via Enter triggers true D3D9 exclusive fullscreen. Chat opens and is interactive — Phase H functionally works — but the SWG window detaches from the editor embed and never reattaches. Full triage in `.planning/debug/chat-open-d3d9-fullscreen.md`. **Not blocking** Phase 4.
- **No new commits.** Build verified at `765738c` (clean rebuild + 106/106 tests pass), debug note + memory update are out-of-repo.
- **Memory updates this session: 1** (`project-swg-context-routing` updated with Phase H side-effect + pointer to the debug note).

---

## What happened (brief)

| Step | What | Outcome |
|---|---|---|
| State verify | `git log -3`, branch list, DLL timestamps | Matched morning handoff (`ab0270a` on top of `765738c`, 12 cycle branches alive, clean tree) |
| Rebuild | Release/x86 via VS 2026 MSBuild | Clean; native `UtinniCore.dll` unchanged (no C++ source diff post-commit), managed `UtinniCoreDotNet.dll` rebuilt |
| Test re-run | xUnit on `UtinniCoreDotNet.Tests` | **106/106 pass** |
| CppSharp churn | `UtinniCoreDotNet/Generated/UtinniCore.cs` dirtied as expected | `git restore`d before further work |
| Live SWG smoke | User launched, many TJT-driven `/warp` scene changes | **NO CRASH**. Naked-after-warp confirmed baseline. R-A migration verified live. |
| Side-effect surfaced | Pressing Enter to open chat → true D3D9 exclusive fullscreen, embed lost | Triaged, filed |
| Branch prune | `git branch -D` 12 debug branches | All deleted cleanly |
| Bug filing | `.planning/debug/chat-open-d3d9-fullscreen.md` + memory update | Out-of-repo (untracked working note + memory file) |

---

## Chat-open D3D9 fullscreen — the queued bug in one paragraph

Pressing Enter to open chat (Phase H code path, commit `6047416`) triggers a true D3D9 exclusive fullscreen device-mode switch (display flicker, SWG window detaches from editor embed, never reattaches). Chat input works post-fullscreen (Phase H is functionally correct). Prime suspect: `enableTextInput(*, true, true, false)` — the `setKeyboardInput=true` arg likely triggers a DirectInput re-acquire that pulls D3D9 into fullscreen via the SetCooperativeLevel shim (commit `18e79c3`). Critical comparison point: `forceOpenChatInputFromCpp` calls `enableTextInput` with **identical args** — if the editor's button-driven open-chat path doesn't fullscreen, the difference is calling context (input-pump thread vs UI thread), not the args. **First test next session:** smoke at `9c8edd3` (Phase 3 close, pre-R-A) to confirm pre-existing vs R-A-regression. Full verification recipe in the debug note.

---

## What's next

**Two-way fork. User pick.**

1. **Phase 4 — Tier 2 CLI shim + golden fixtures** (`/gsd-discuss-phase 4`). Goal: `utinni-cli` exe in the same solution, golden-file tests against checked-in fixtures, target ~60-70% of "Kenny please verify" loops becoming unattended CI runs. Resolves CON-O-09 (fixture storage) and CON-O-11 (CLI distribution). This is the planned next phase per ROADMAP.
2. **Detour: chat-open D3D9 fullscreen triage.** ~1-2 sessions (verify pre-existing vs regression via bisect at `9c8edd3`, run diag probes, draft fix). Promote ahead of Phase 4 only if the editor session is actually blocked by it.

Recommendation: **Phase 4 next, chat bug after.** The fullscreen issue is annoying but doesn't block modding workflows (modders rarely need in-game chat). Phase 4 unlocks the test-harness multiplier this project really needs.

---

## Watch items / carry-forward

| Item | Detail |
|---|---|
| **Chat-open D3D9 fullscreen unresolved** | Queue note at `.planning/debug/chat-open-d3d9-fullscreen.md`. Bisect target: `9c8edd3`. Suspect: `setKeyboardInput=true` → DirectInput re-acquire → D3D9 cooperative-level pull. Cross-references: [[project-swg-context-routing]], [[feedback-d3d9-reset-third-party]], commits `6047416`, `18e79c3`, `74f64fc`, `f5fa073`. |
| **Phase H landed but has side effect** | Phase H (Issue #11) — `hkChatEnter` detour at `0x00F3E420` — works for chat opening but introduces the fullscreen side effect above. Don't revert Phase H to "fix" the fullscreen issue; that would re-break Enter-opens-chat. Fix path is to make `enableTextInput(*, true, true, false)` safe in the input-dispatcher context, not to undo the override. |
| **11-fold template duplication still pending** | Carried forward from morning handoff. Anonymous-namespace `dispatchSnapshot` / `CallbackEntry<Fn>` copies across the 11 R-A files. Internal linkage, zero runtime cost. Consolidation to a shared header is clean follow-up; not urgent. |
| **CppSharp regen artifact churn** | `UtinniCoreDotNet/Generated/UtinniCore.cs` dirties on every build. Restore before staging. Same behavior as last 3+ sessions. |
| **VS 2026 PlatformToolset bump deferred** | v142 still pinned. v144 bump is dedicated future phase, NOT scope creep. See [[project-vs2026-toolchain]]. |

---

## Memory updates this session

- **UPDATED `project-swg-context-routing`** — appended "Known Phase H side-effect (queued 2026-05-23)" block noting the Enter → D3D9 fullscreen behavior with full pointer to `.planning/debug/chat-open-d3d9-fullscreen.md` and the prime suspect (DirectInput re-acquire via `setKeyboardInput=true`). Bisect target `9c8edd3` recorded.

No new memories. The Phase H side-effect is a state addition to an existing memory, and the debug note carries the durable triage detail.

---

## Reproduction recipe (for resume)

```bash
# Confirm state — should match the morning handoff exactly minus the debug branches
cat .planning/SESSION-HANDOFF-2026-05-23-PM.md       # this file
cat .planning/SESSION-HANDOFF-2026-05-23.md          # morning handoff (R-A shipped)
git log --oneline -3                                  # master at ab0270a (handoff) on 765738c (migration)
git branch | grep cycle | wc -l                       # 0 (all pruned)
git status --short                                    # clean

# Read the queued bug if you're going to triage it
cat .planning/debug/chat-open-d3d9-fullscreen.md

# Sanity rebuild + tests (optional — only if you suspect drift)
& 'D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' Utinni.sln /p:Configuration=Release /p:Platform=x86 /v:minimal /m
git restore UtinniCoreDotNet/Generated/UtinniCore.cs  # drop CppSharp regen churn
dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --configuration Release --no-build --logger "console;verbosity=minimal" --nologo
# Expect: 106/106 tests pass

# Then either:
#   /gsd-discuss-phase 4                            # Phase 4 CLI shim (recommended)
# OR
#   # Triage chat-open fullscreen first:
#   git checkout 9c8edd3                             # Phase 3 close, pre-R-A
#   # Rebuild + live smoke — does Enter trigger fullscreen here too?
#   # If yes: pre-existing, file as own phase
#   # If no:  R-A regression, deep-dive imgui_impl.cpp render dispatch ordering
```

---

## Tasks closed this session

- `#1 Verify state matches morning handoff` → completed
- `#2 Clean rebuild + retest` → completed (106/106)
- `#3 Live SWG smoke (TJT-driven scene change)` → completed (PASSED — many warps, no AV)
- `#4 Prune 12 debug cycle branches` → completed
- `#5 Triage + file chat-open D3D9 fullscreen bug` → completed (`.planning/debug/chat-open-d3d9-fullscreen.md` + memory updated)

No tasks open at end of session.

---

*Written 2026-05-23 afternoon. Master at `ab0270a` on `765738c` (unchanged this session). R-A migration LIVE-VERIFIED. Chat-open D3D9 fullscreen queued for follow-up. Next session: Phase 4 (CLI shim) recommended, or chat-fullscreen triage if blocking.*
