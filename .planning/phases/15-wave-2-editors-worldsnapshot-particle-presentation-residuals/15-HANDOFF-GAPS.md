# Phase 15 — Gap-Closure Handoff (A9 undo-crash fix shipped; restart for maintainer live smoke)

> Written 2026-06-12 at the end of `/gsd-execute-phase 15 --gaps-only`. Session being restarted.
> **TL;DR:** The three gap-closure plans (15-09, 15-10, 15-11) are **100% complete and committed**
> in both repos. The A9 WorldSnapshot undo-crash (`0xC0000005`) is fixed in code, the fix is
> verified present in the **deployed** DLLs, and `bin/Release/` is reassembled and ready to inject.
> The ONLY remaining work is the **maintainer live-SWG smoke** (15-08 Task 2) — re-verify A9 plus
> Checklists B/C/D against the fixed build — then close the phase. Supersedes `15-HANDOFF.md`
> (which was the pre-gap-closure pause).

---

## Where we are

- **Phase:** 15 — `wave-2-editors-worldsnapshot-particle-presentation-residuals`
- **Milestone:** v2.0 — AI-Assisted SWG Tools
- **Requirements:** PROD-W2-WS, PROD-W2-PRT, RESID-03, RESID-04
- **Structure:** 11 plans. 15-01..15-08 were the original phase (7 done + 15-08 paused at the live
  smoke). 15-09/15-10/15-11 are the gap-closure plans, **all now done**.
- **STATE.md status:** `verifying` — phase is NOT complete; gated on the maintainer live smoke.
- **Branching:** `none` — all work on `master` in both repos. Worktrees OFF (sequential inline).

### Git state at this handoff (both trees CLEAN)
- **Utinni** `D:/Code/Utinni` — gap-closure commits, in order:
  - `c0999e1` docs(15): plan A9 undo-crash gap closure (15-09..15-11)
  - `d1659b1` docs(15): revise gap-closure plans per checker
  - `c84b92d` docs(15): fold SDK IEditorPlugin consistency into 15-10 gap plan
  - `fb4b49e` docs(15): record gap-closure planning complete in STATE/ROADMAP
  - `43b9dc9` feat(15-09): pure WorldSnapshotCommandGuard bail-on-null helper + unit coverage
  - `08eeb51` fix(15-09): null-guard all four WS IUndoCommand Execute/Undo bodies (A9 crash)
  - `5342683` docs(15-09): complete A9 WS undo-crash null-guard plan
  - `8a888b7` feat(15-10): UndoRedoManager.Clear() + IEditorPlugin undo seam + FormMain wiring
  - `14b92ae` docs(15-10): complete WS undo-stack clear seam + Ctrl+Z routing + stale-gizmo plan
  - `c2d5d31` docs(15-11): record A9 gap-closure + content-verified build (15-SMOKE.md annotation)
  - `9547638` docs(15-11): complete gap-closure Release gate + reassembled injection build plan
- **UtinniPlugins** `D:/Code/UtinniPlugins` — paired 15-10 commits:
  - `0b7e1a1` feat(15-10): TJT Plugin implements widened IEditorPlugin undo seam
  - `d61b922` feat(15-10): clear undo stack + gizmo on snapshot boundaries; route Ctrl+Z from placements
- **Nothing pushed yet** — both repos are ahead of origin (standing push permission exists for
  `git push origin <branch>`; confirm before any force/delete).

> ⚠ Spawned agent IDs from this session are dead after restart. Any continuation MUST be a **fresh**
> agent — do NOT SendMessage to old IDs.

---

## What the gap closure fixed (from 15-SMOKE.md Checklist A9)

The blocking defect: editor **Undo** of any WorldSnapshot bulk op null-deref'd the SWG client
(`0xC0000005` READ target=0x0 at a managed/JIT address). Reproduced twice including a clean-stack
repro (fresh Load → single Move → Undo → crash; **not** reload-specific).

| Plan | Repo(s) | Fix |
|------|---------|-----|
| **15-09** | Utinni | Root-cause fix. New pure `WorldSnapshotCommandGuard` helper (BCL-only, unit-tested, 6 facts). All four WS `IUndoCommand` bodies (Position/Rotation/Add/Remove, Execute **and** Undo) restructured to **resolve object+node first → guard via `ShouldApply` → only then deref**. `ParentNode` guarded before `.LastChild`; `RemoveNode` skipped on null. |
| **15-10** | Utinni + UtinniPlugins | Structural belt-and-suspenders. Public `UndoRedoManager.Clear()`; `IEditorPlugin` widened with `Undo`/`Redo`/`ClearUndoStack` delegates, wired by `FormMain`; TJT + both SDK artifacts implement them. `WorldSnapshotImpl` clears the undo stack on Load/Unload/Reload and `DisableGizmo()` on Unload/Reload/BulkDelete/RemoveNode (GAP 2 stale-gizmo). `FormSnapshotPlacements.ProcessCmdKey` routes Ctrl+Z→Undo / Ctrl+Y→Redo, then `RefreshTable()` AFTER the undo (FIFO). |
| **15-11** | build/docs | Full Release gate + reassembled `bin/Release/` injection build. **Content-of-fix gate PASSED** on the deployed PEs: `UtinniCoreDotNet.dll` defines `WorldSnapshotCommandGuard` + exposes `UndoRedoManager.Clear`; `TheJawaToolboxDotNet.dll` references `ClearUndoStack` — so the maintainer cannot be handed a stale crashing DLL. A9 row in 15-SMOKE.md marked fix-shipped/awaiting-live-re-verify. |

### Build/test gate (15-11, all green)
- `Utinni.sln` Release|x86 (VS2026 MSBuild v145) → exit 0; `TheJawaToolbox.sln` Release|x86 → exit 0
  (paired rebuild against the widened `IEditorPlugin` — binary-compat verified, no MEF
  MissingMethodException).
- `UtinniCoreDotNet.Tests` **697/0** · `Utinni.Cli.Tests` **249/2-skip/0** · `Utinni.Mcp.Tests` **77/0**
- Native `UtinniCore.Tests.exe` **84 assertions/27 cases** + `[resid04]` no-Reset gate **8/1**
- `Generated/UtinniCore.cs` CppSharp churn reverted (never committed)

### Assembled injection build (ready to inject)
`D:/Code/Utinni/bin/Release/` — `Launcher.exe` + `UtinniCore.dll` + `UtinniCoreDotNet.dll` +
`Plugins/TheJawaToolbox/{TheJawaToolbox.dll, TheJawaToolboxDotNet.dll, Resources/, input.ini, settings.ini}`.
DISCL diagnostic + suppress-toggle log writes to `D:/Code/Utinni/bin/Release/utinni.log`.

---

## The ONLY remaining work — maintainer live-SWG smoke (15-08 Task 2)

Automation cannot reach this (CON-TT-03: live GPU/D3D9 render judgment). Inject `bin/Release/` and
work through `15-SMOKE.md`:

1. **A9 RE-VERIFY (the gap fix — do this first):** Load a `.ws` snapshot → `Placements…` →
   single Move → **Undo via the main-editor Undo arrow** → confirm it **reverses cleanly, no crash**.
   Then repeat the original crash recipe (Move → Snapshot ▸ Reload → Undo) → confirm **no crash**.
   Confirm **Ctrl+Z from the Placements child window** now works (was a silent no-op) and the grid
   reflects the revert. Confirm the **stale gizmo clears** after Delete + reload (GAP 2).
2. **Checklist B (PROD-W2-PRT):** Particle editor — extract a `.prt`, emitter tree/typed grid,
   greyed-hex unknowns, edit + Save loose-override, `Explain effect`. Honest fallback = PASS
   (`Preview in client` disabled with tier-(b) badge `Reloads on next scene change or relog.`).
3. **Checklist C (RESID-04):** DISCL log confirms EXCLUSIVE A4 trigger + D-12 suppress redirect;
   walk the windowed↔fullscreen matrix; A/B the suppress toggle; confirm **no crash, no Utinni device Reset**.
4. **Checklist D (RESID-03):** Save `.stf` + `.ot` loose-override → TJT scene change → record
   render-on-reload vs relog-only → confirm badge copy is honest.

Then fill `15-SMOKE.md` outcomes/defects, update/close the two folded RESID todos
(`.planning/todos/pending/swg-window-resize-fullscreen-edge-cases.md`,
`.planning/todos/pending/phase10-stringtable-sc3-live-reload-residual.md`), and type **"approved"**
in the sign-off block.

---

## How to resume after restart

### If the maintainer ran the smoke and signed `15-SMOKE.md` "approved"
Run `/gsd-execute-phase 15` (or a fresh `gsd-executor`) to close **15-08**:
1. Verify the sign-off + the four checklists.
2. Update/close the two folded RESID todos per findings.
3. Write `15-08-SUMMARY.md`; `gsd-sdk query roadmap.update-plan-progress 15 15-08 complete`.
4. Then the **post-phase completion flow**: code_review_gate (`Skill gsd-code-review 15`, advisory) →
   regression_gate (managed `dotnet test --no-build`; native `UtinniCore.Tests.exe`) →
   verify_phase_goal (`gsd-verifier`, opus → `15-VERIFICATION.md`) → `gsd-sdk query phase.complete 15`
   → close todos / PROJECT.md / offer next. **No auto-advance** (auto-mode is false).
   Note: PROD-W2-WS, RESID-03, RESID-04 are gated on this live sign-off — don't mark Validated before it.

### If the maintainer reports DEFECTS
`/gsd:plan-phase 15 --gaps` → new gap plans → `/gsd:execute-phase 15 --gaps-only`. Do NOT mark complete.

---

## Restart note — permission prompts (the reason for this restart)

You reported still getting permission prompts despite intending bypass mode. Permission behavior is
owned by the Claude Code harness, not the model. On relaunch, to stop the prompts:
- Relaunch with the correct flag: `claude --dangerously-skip-permissions` (note: there is no
  `skipDangerousModePermission` flag — a wrong name is silently ignored and you stay in the prompting
  default mode), **or** press **Shift+Tab** in-session to cycle to "bypass permissions" (red).
- Bypass is refused if running as root/sandboxed, or if `settings.json` has `permissions.deny` rules
  (always enforced); a `PreToolUse` hook can also still interject.
- Alternative (no full bypass): run `/fewer-permission-prompts` to allowlist the safe repeat commands
  (git, gsd-sdk, dotnet, MSBuild) in `.claude/settings.json`.

---

## Gotchas to carry into the restart

- **Build with VS2026 MSBuild** at the resolved absolute path
  `D:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`
  (`%ProgramFiles%` with a space breaks under Git-Bash `cmd /c`), Release|x86.
  `dotnet build` FAILS on UtinniCoreDotNet/TJT (MSB3823 on image .resx); run xUnit via
  `dotnet test --no-build -c Release`.
- **`Generated/UtinniCore.cs`** — CppSharp reorders it every C++ build → symmetric no-op diff.
  Always `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs`; never commit it.
- **`UtinniCoreDotNet.csproj` is old-style (non-globbing)** — new source files need an explicit
  `<Compile Include>` (15-09 added the guard source this way).
- **Worktrees OFF** (`workflow.use_worktrees=false`) — all execution sequential inline on the main tree.
- **Cross-repo authority** — standing write/commit authority on both `kennethlong/{Utinni,UtinniPlugins}`;
  paired commits need no human checkpoint (only the live smoke does). Files commit to their own repo.
- **Binary-compat** — `IEditorPlugin` was widened (public API). TJT is the only in-gate implementer and
  was rebuilt in the paired 15-10 commit; the two SDK artifacts
  (`sdk/examples/ExampleEditorPlugin`, `sdk/UtinniPluginTemplates/DotNetEditorPluginTemplate`) were
  updated for consistency (they're outside `Utinni.sln`, so they don't break the gate).
- **`gsd-sdk query init.execute-phase 15`** uses a **positional** phase arg (`--phase 15` returns
  phase_found=false).

## Key files touched by gap closure (for orientation)
- 15-09: `UtinniCoreDotNet/Commands/WorldSnapshotCommandGuard.cs` (new),
  `UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs`,
  `UtinniCoreDotNet.Tests/Commands/WorldSnapshotCommandGuardTests.cs` (new)
- 15-10 (Utinni): `UtinniCoreDotNet/UndoRedo/UndoRedoManager.cs`,
  `UtinniCoreDotNet/PluginFramework/IEditorPlugin.cs`, `UtinniCoreDotNet/UI/Forms/FormMain.cs`,
  `UtinniCoreDotNet.Tests/UndoRedoManagerTests.cs`,
  `sdk/examples/ExampleEditorPlugin/ExampleEditorPlugin.cs`,
  `sdk/UtinniPluginTemplates/DotNetEditorPluginTemplate/Plugin.cs`
- 15-10 (UtinniPlugins): `…/TheJawaToolboxDotNet/Plugin.cs`, `…/SWG/WorldSnapshotImpl.cs`,
  `…/UI/Forms/FormSnapshotPlacements.cs`
- 15-11: no source commit — gated build to `bin/Release/` (gitignored) + `15-SMOKE.md` annotation
