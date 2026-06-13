# Phase 15 — Tier-4 Maintainer Live-SWG Smoke Log

> **Plan:** 15-08 (Wave 4) · **Requirements:** PROD-W2-WS, PROD-W2-PRT, RESID-03, RESID-04
> **Tier:** 4 (maintainer-in-the-loop live injected session — automation cannot reach; CON-TT-03)
> **Status:** ⬜ AWAITING MAINTAINER SIGN-OFF

This is the maintainer-signed phase gate for Phase 15. The automatable codec / CLI / MCP / UI-build /
classifier work (15-01..15-07) is green in both repos and the deployable injection build is assembled.
This log records the four live-only behaviors that need a real injected SWG client.

---

## Build / Test State (Task 1 — automated, green)

| Check | Result |
|-------|--------|
| Utinni `Utinni.sln` Release\|x86 (VS2026 MSBuild v145) | ✅ exit 0 |
| UtinniPlugins `TheJawaToolbox.sln` Release\|x86 | ✅ exit 0 (`TheJawaToolbox.dll` + `TheJawaToolboxDotNet.dll`) |
| `UtinniCoreDotNet.Tests` (Release) | ✅ 690 passed / 0 failed |
| `Utinni.Cli.Tests` (Release) | ✅ 249 passed / 2 skipped (fixture-gated) / 0 failed |
| `Utinni.Mcp.Tests` (net10, Release) | ✅ 77 passed / 0 failed |
| Native `UtinniCore.Tests.exe` (full) | ✅ 84 assertions / 27 cases |
| Native `UtinniCore.Tests.exe [resid04]` (D-13 no-Reset gate) | ✅ 8 assertions / 1 case |
| `Generated/UtinniCore.cs` CppSharp churn | reverted (never committed) |

### Assembled injection build

The maintainer injects this layout (built fresh by Task 1):

```
D:/Code/Utinni/bin/Release/
  Launcher.exe
  UtinniCore.dll
  UtinniCoreDotNet.dll
  Plugins/TheJawaToolbox/
    TheJawaToolbox.dll            (C++ plugin)
    TheJawaToolboxDotNet.dll      (managed TJT)
    Resources/  input.ini  settings.ini
```

The native DISCL diagnostic + suppress-toggle line up at `D:/Code/Utinni/bin/Release/utinni.log`.

---

## How to run

1. Launch the SWGEmu/Restoration client via `Launcher.exe` (the standard Utinni inject path).
2. The Jawa Toolbox loads as the editor host (the 5 Wave-1 SubPanels + the two new Wave-2 editors).
3. Work through the four checklists below; record outcome in each row's **Result** column and any
   defect notes inline. Confirm each new editor follows the **unchanged Wave-1 MEF seam** (registered via
   `Plugin.cs` `GetForms()`/SubPanel button; `GetSubPanels()` is NOT widened).
4. When done, fill the **Maintainer Sign-Off** block and type `approved` (or record defects).

> Baselines to remember (so non-issues aren't mis-flagged):
> - **Naked-after-scene-change is expected** (equipment not re-rendered after a TJT-driven scene change).
>   "Naked, but in world" = success — `project_tjt_scene_change_naked_baseline`.
> - **Right-edge cursor dead-zone when stretched/maximized** is a SEPARATE deferred item
>   (`project_swg_cursor_clip_deadzone`), NOT part of RESID-04 here.
> - Scene changes are driven by **TJT's chat-command parser** (`project_scene_change_via_tjt`).

---

## Checklist A — PROD-W2-WS · WorldSnapshot placements table + bulk ops (live demo)

Open a snapshot in the Snapshot panel, open `Placements…`, and exercise single + multi-select bulk ops.
The bulk ops compose the shipped `WorldSnapshotCommands` as N ordered descriptors (15-01); confirm they
visibly affect in-world placements and that undo reverses them.

| # | Step | Expected | Result |
|---|------|----------|--------|
| A1 | TJT loads; open the Snapshot panel | Panel + the new `Placements…` button visible (MEF seam unchanged) | ✅ 2026-06-12 |
| A2 | Load a `.ws` snapshot | Placements populate; in-world objects appear | ✅ 2026-06-12 — naboo snapshot loaded, 5442 placements, in-world objects render |
| A3 | Click `Placements…` | The `FormSnapshotPlacements` table opens with the placement rows | ✅ 2026-06-12 — `Snapshot Placements — naboo`, 5442 placements; filter works (`armoire` → 2/5442, `theed` → 543) | 
| A4 | Single-select a row | The gizmo drives that single placement (selection-sync holds) | ✅ 2026-06-12 — row-select → sidebar Selected Node sync + in-world translate gizmo on the node; maintainer confirmed gizmo drives movement |
| A5 | Multi-select rows | Multi-row selection holds; detach/reattach is stable | ✅ 2026-06-12 — 2 armoire rows multi-selected (`2 selected`), held stably through Move/Delete |
| A6 | `Move selected…` | Selected placements visibly move in-world | ✅ 2026-06-12 — Move dialog (ΔX/ΔY/ΔZ + Apply); ΔX=3 → both armoires + grid Position values shifted in-world (maintainer confirmed) |
| A8 | `Retemplate selected…` | Selected placements swap template (remove+add per node) in-world | ✅ 2026-06-12 — selected streetlamps 104+105 → Retemplate → `object/tangible/furniture/cheap/shared_armoire_s01.iff` → Apply. 2 new armoire nodes (`9995373`/`9995374`) created at the **exact preserved transforms** of 104/105 (-5110/4184, -5151/4290) + armoire rendered in-world. Add half immediate; Remove half deferred (tier-b, same as Delete). Composes Remove+Add descriptors per 15-01 design. |
| A7 | `Delete selected` (red confirm) | Selected placements disappear in-world after the confirm | ✅ 2026-06-12 (tier-b) — red confirm fired ("Delete 2 placements? …undoable in the editor until you save."), selection cleared. Live render + grid defer (matches LOCKED badge "Placements re-resolve on the next scene change"); after Snapshot ▸ Reload the deleted armoires + rows are gone. **History note (maintainer-confirmed):** base naboo snapshot has NO armoires — all armoire nodes are in-memory adds. Reload re-reads the authored node list (reverts unsaved node edits) and does NOT de-spawn already-rendered objects until a scene change. So delete/retemplate persistence is governed by **Save** (loose-override), not reload; reload-revert is expected, not a bug. |
| A9 | Undo (each op) | Each bulk op reverses atomically (N undo commands compose) | ❌ **CRASH 2026-06-12** — Move applied cleanly (104 X −5151.1→−5051.1 in grid). `Ctrl+Z` while the Placements child window was focused did NOTHING (no revert, no crash) — undo hotkey not routed from the child window to the editor undo manager. Clicking the **main-editor Undo arrow** then **crashed the client**: `utinni.log` 19:32:15 `VEH FATAL: code=0xC0000005 … module=<unmapped-EIP> base=0x0 rva=0x03667F2A READ target=0x00000000` → `ExceptionHandler invoked`. Null-deref AV at a JIT/managed address. No fresh `.mdmp` (int3 halt). Client process gone. **HYPOTHESIS:** Snapshot ▸ Reload reverts the node list but does NOT invalidate the editor undo stack → undo command holds a stale/dangling node pointer from before the reload → null-deref on undo. NOT yet scoped whether a clean move→undo (no reload in the stack) also crashes. |

**Checklist A outcome / defects:**

> Banked from the 2026-06-12 session (interrupted by the phantom-walk defect, since RESOLVED —
> stale blender-plugin searchPath overrides in `swgemu_live.cfg`, NOT injection; see
> `.planning/debug/resolved/phantom-forward-walk.md`): A1/A3 confirmed, A4 selection-sync half
> confirmed. RESID-03 WS LOCKED badge verbatim confirmed: "Placements re-resolve on the next
> scene change." DEC-A3/D-11 boundary footer verbatim confirmed (Blender-lane sentence).
> A2, A4-gizmo-visual, A5–A9 remain for the post-fix session.

**2026-06-12 live (MCP-driven + maintainer):** A1–A7 PASS. WorldSnapshot load, placements table,
single-select gizmo, multi-select, bulk Move, and bulk Delete (tier-b re-resolve) all confirmed.
Bulk ops compose `WorldSnapshotCommands` as ordered descriptors with per-op undo, exactly as 15-01
designed. RESID-03 WS badge + DEC-A3/D-11 footer verbatim confirmed.

**DEFECT (minor / polish, non-blocking) — stale selection gizmo after re-resolve:** after `Delete
selected` + snapshot reload, the deleted placements are gone but the in-world manipulation gizmo
(translate adjuster) remains rendered at the old selected-node location — it is not cleared when its
target node disappears on re-resolve. Cosmetic; the gizmo clears on next selection change. Candidate
follow-on: clear/hide the gizmo when the selected node is removed or on snapshot reload. Does NOT
block the PROD-W2-WS sign-off.

**DEFECT (BLOCKING — client crash) — Undo after a Snapshot ▸ Reload null-derefs:** A9 — after Move
(or any bulk op) followed by a `Snapshot ▸ Reload`, invoking the editor Undo (main-window Undo arrow)
crashes the client with `0xC0000005` null-read at a managed/JIT address (`utinni.log` 19:32:15).
**ROOT CAUSE (confirmed in code):** `WorldSnapshotNodePositionChangedCommand.SetPosition()`
(`UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs:140-141`) does
`var obj = Network.GetObjectById(nodeCopy.Id); obj.Transform.Position = position;` with **no null
guard**. After `Snapshot ▸ Reload` (`WorldSnapshotImpl.Reload()` line 122 calls native
`WorldSnapshot.Reload()` and does NOT clear the editor undo stack), the re-resolved in-world object
for that node id is not currently instantiated, so `Network.GetObjectById` returns null → null-deref
AV. The Add/Remove/Rotation undo commands have the same unguarded `WorldSnapshotReaderWriter`/`obj`
lookups (`LastNode`, `ParentNode.LastChild`, `GetNodeById`). Secondary finding: `Ctrl+Z` is not
routed from the `FormSnapshotPlacements` child window to the editor undo manager (no-op from that
window). **Scoping still open:** NOT confirmed whether a clean `load → move → undo` (no reload in the
stack) reverses correctly or also crashes — needs a fresh-stack repro after relaunch.
**Gap-closure fix:** (1) null-guard `obj`/`node` lookups in all WS undo command bodies; (2) clear the
editor undo stack on snapshot Unload/Reload so stale commands can't run; (3) ideally wire Ctrl+Z from
the placements window.

---

## Checklist B — PROD-W2-PRT · Particle editor + preview (live demo)

Open a real `.prt` (extract via the TRE Browser), edit, save loose-override, and try the preview.
**Expectation set by 15-03:** there is NO reachable native hot-retrigger hook this phase — the
`Preview in client` button is state-disabled and the reload badge degrades to the LOCKED tier-(b) copy
`Reloads on next scene change or relog.`. Confirming this honest degraded fallback is a PASS;
a live hot-retrigger would only be possible once a future plan wires the seam.

| # | Step | Expected | Result |
|---|------|----------|--------|
| B1 | Extract a `.prt` via the TRE Browser → `Open in Particle Editor` | Hand-off opens `FormParticleEditor` (gate hidden for non-`.prt`) | ⬜ |
| B2 | Emitter tree + typed grid populate | `PEFT → EMGP → EMTR → …` tree; Field/Value/Type grid fills | ⬜ |
| B3 | Inspect a raw-preserved/unknown leaf | Renders greyed-out Consolas hex + "preserved as original bytes" tooltip (D-05) | ⬜ |
| B4 | Edit a leaf via double-click → hex sub-editor → save back | Edit is byte-safe + editor-local undoable | ⬜ |
| B5 | `Save (loose override)` | Writes to the loose-override path; dirty marker clears | ⬜ |
| B6 | Inspect `Preview in client` | **Disabled** (no reachable hook this phase) with the honest tooltip; reload badge = `Reloads on next scene change or relog.` (degraded tier-(b) is the EXPECTED honest fallback per 15-03) | ⬜ |
| B7 | `Explain effect` (AI read-assist) | Read-assist pane fills via `utinni-cli decode-iff` (read-only, no codec in the AI handler); honest error if CLI absent | ⬜ |
| B8 | DEC-A3/D-11 footer | The preview-vs-author boundary sentence shows verbatim (dimmed footer) | ⬜ |

**Checklist B outcome / defects** (note: live hot-retrigger refresh OR confirm the honest degraded fallback):

_(record here)_

---

## Checklist C — RESID-04 · Window-resize / windowed↔fullscreen edge-case matrix

Walk the matrix; read the DISCL log to confirm the A4 trigger + the D-12 suppress fire; A/B the toggle.
The D-12 suppress (default ON) rewrites `DISCL_EXCLUSIVE → DISCL_NONEXCLUSIVE` at the DirectInput
cooperative-level layer to keep SWG windowed-embedded with **NO Utinni-initiated device Reset (D-13)**.

### C.1 — DISCL log read (confirm A4 + suppress)

Open `D:/Code/Utinni/bin/Release/utinni.log` during/after login→world and chat-open. Confirm:

| # | Log line to find | Confirms | Result |
|---|------------------|----------|--------|
| C1 | `DI::SetCooperativeLevel: ... flags=EXCLUSIVE ... caller=0x…` | SWG requests EXCLUSIVE (A4 trigger) + caller PC | ⬜ |
| C2 | `DI::SetCooperativeLevel: D-12 suppress -> redirected EXCLUSIVE to NONEXCLUSIVE (0x… -> 0x…)` | The suppress fix fires | ⬜ |

### C.2 — Edge-case matrix (suppress ON / default)

For each transition record: embed survives? SWG renders at right size? mouse maps (RT-space)? cursor clip OK? recovers on reverse?

| # | Transition | Expected (suppress ON) | Result |
|---|------------|------------------------|--------|
| C3 | windowed → fullscreen | Stays windowed-embedded (no detach/overlay) | ⬜ |
| C4 | login → load-into-world | Embed survives (the prime live trigger per 2026-06-03) | ⬜ |
| C5 | chat-open Enter | Embed survives; chat/input still route (FOREGROUND preserved) | ⬜ |
| C6 | maximize → restore | Embed + SWG render recover | ⬜ |
| C7 | minimize → restore | Both windows recover together cleanly | ⬜ |
| C8 | free resize-drag (continuous) | Window-side SetWindowPos only; RT-space mapping holds; no Reset | ⬜ |
| C9 | multi-cycle (win→fs→win→fs…) | No cumulative drift / leak | ⬜ |
| C10 | alt-tab away/back (each mode) | Embed returns to its rect; input re-establishes | ⬜ |
| C11 | monitor / DPI change (if reproducible) | Embed survives (or record N/A) | ⬜ |

### C.3 — Toggle A/B + no-Reset confirmation

| # | Step | Expected | Result |
|---|------|----------|--------|
| C12 | With suppress ON (default): re-run C4/C5 | SWG stays windowed-embedded; input/chat work; **no detach** | ⬜ |
| C13 | Flip `DirectInput::setSuppressExclusiveFullscreen(false)` live; re-run C4 | Un-suppressed mode switch observable (the toggle IS the lever) — confirms A/B | ⬜ |
| C14 | Restore suppress ON | Windowed-embedded restored | ⬜ |
| C15 | NO crash + NO Utinni device `Reset` across the whole matrix | No `D3DERR_INVALIDCALL` / DEVICELOST; clean session (D-13 held behaviorally) | ⬜ |

**Checklist C outcome / defects** (update `swg-window-resize-fullscreen-edge-cases.md` with the per-row findings):

> Preliminary data point (2026-06-12 session, pre-walk-fix): TJT Scene▸Load `naboo.trn` scene
> change — embed survived, `hkSetScene` clean (supports C4; re-walk in the full matrix).
> Note: utinni.log that session showed only `NONEXCLUSIVE FOREGROUND (0x6)` requests at char
> select — no EXCLUSIVE request seen yet; C1/C2 need the fullscreen/login-to-world triggers.

_(record here)_

---

## Checklist D — RESID-03 · SC3 live render-on-reload for `.stf` / `.ot`

Save an `.stf` and an `.ot` loose-override edit, trigger a TJT-driven scene change, and observe whether
the edit renders on reload vs relog-only. Confirm the editor badge copy is **honest** against what
actually happened (no over-promise). Both `.stf` and `.ot` route to tier-(b) `PendingNextSceneChange`
(15-07); the LOCKED badge copy must match the observed behavior.

| # | Step | Expected / record | Result |
|---|------|-------------------|--------|
| D1 | Edit + save an `.stf` (loose override) | Save succeeds; reload badge shows tier-(b) copy | ⬜ |
| D2 | Trigger a TJT chat-command scene change | Scene reloads | ⬜ |
| D3 | Does the edited string render on reload? | Record: renders-on-scene-change **OR** relog-only | ⬜ |
| D4 | Edit + save an `.ot` (loose override) | Save succeeds; reload badge shows tier-(b) copy | ⬜ |
| D5 | Trigger a scene change | Scene reloads | ⬜ |
| D6 | Does the edited template render on reload? | Record: renders-on-scene-change **OR** relog-only | ⬜ |
| D7 | F5b stale-crc check (`.stf`) | Edited text still renders with the preserved stale `sourceCrc` (expected harmless) | ⬜ |
| D8 | Badge candor | Confirm the badge copy honestly matches D3/D6 (amend to relog wording if relog-only; do NOT over-promise) | ⬜ |

**Checklist D outcome / defects** (record the SC3 disposition + close `phase10-stringtable-sc3-live-reload-residual.md`):

_(record here)_

---

## Maintainer Sign-Off

- [ ] Checklist A (WS demo) completed
- [ ] Checklist B (Particle demo) completed
- [ ] Checklist C (RESID-04 matrix + DISCL log + toggle A/B + no-Reset) completed
- [ ] Checklist D (RESID-03 SC3) completed
- [ ] `swg-window-resize-fullscreen-edge-cases.md` updated/closed per the findings
- [ ] `phase10-stringtable-sc3-live-reload-residual.md` updated/closed per the findings

**Disposition:** _(approved / approved-with-deferred-residual / defects — see notes)_

**Signed:** _(maintainer)_  **Date:** _(YYYY-MM-DD)_

**Notes / follow-on defects:**

> Session logistics (2026-06-12): TJT `ToggleFreeCam = Shift+Tab` hotkey collides with Claude
> Code's permission-mode cycling, and permission prompts steal focus from the game — MCP-driven
> UI automation was unreliable; maintainer-drives mode is the working pattern for this smoke.
> Build under test: Task-1 assembled build + `04fa26d` `[DebugBisect]` ini-gated skip groups
> (default-off, live-verified clean standing/NPCs-normal post-walk-fix).

_(record here)_
