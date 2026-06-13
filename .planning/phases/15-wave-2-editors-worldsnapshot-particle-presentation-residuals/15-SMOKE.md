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
| A9 | Undo (each op) | Each bulk op reverses atomically (N undo commands compose) | ✅ **RE-VERIFIED PASS 2026-06-13** (no crash + actually reverts; see "A9 RE-VERIFY" section below + follow-on fixes `b26e4bd` / WorldSnapshotCommands obj-optional). Original crash evidence preserved below. ❌ **CRASH 2026-06-12** — Move applied cleanly (104 X −5151.1→−5051.1 in grid). `Ctrl+Z` while the Placements child window was focused did NOTHING (no revert, no crash) — undo hotkey not routed from the child window to the editor undo manager. Clicking the **main-editor Undo arrow** then **crashed the client**: `utinni.log` 19:32:15 `VEH FATAL: code=0xC0000005 … module=<unmapped-EIP> base=0x0 rva=0x03667F2A READ target=0x00000000` → `ExceptionHandler invoked`. Null-deref AV at a JIT/managed address. No fresh `.mdmp` (int3 halt). Client process gone. **HYPOTHESIS:** Snapshot ▸ Reload reverts the node list but does NOT invalidate the editor undo stack → undo command holds a stale/dangling node pointer from before the reload → null-deref on undo. NOT yet scoped whether a clean move→undo (no reload in the stack) also crashes. |

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

**DEFECT (BLOCKING — client crash) — Undo of a WorldSnapshot bulk op null-derefs:** A9 — invoking the
editor Undo (main-window Undo arrow) after any WS bulk op crashes the client with `0xC0000005`
null-read at a managed/JIT address. Reproduced TWICE: (1) Move→Snapshot Reload→Undo (`utinni.log`
19:32:15); (2) **clean-stack relaunch repro** — matched Naboo terrain, fresh snapshot Load (empty
undo stack), Move ΔX=100 (grid updated 2097.7→2197.7, object resolved, move worked), Undo →
**crashed again**, identical signature (`utinni.log` 20:14:48 `VEH FATAL 0xC0000005 …
module=<unmapped-EIP> … READ target=0x0`, same int3 `0x00AA1E3F`, same ESP `0x001AF9FC`).
**SCOPING RESULT: the crash is NOT reload-specific** — undo crashes with a single clean Move command
on matched terrain. The reload only made the unresolved-object condition easier to hit.
**ROOT CAUSE (confirmed in code):** `WorldSnapshotNodePositionChangedCommand.SetPosition()`
(`UtinniCoreDotNet/Commands/WorldSnapshotCommands.cs:140-141`) does
`var obj = Network.GetObjectById(nodeCopy.Id); obj.Transform.Position = position;` with **no null
guard**. After `Snapshot ▸ Reload` (`WorldSnapshotImpl.Reload()` line 122 calls native
`WorldSnapshot.Reload()` and does NOT clear the editor undo stack), the re-resolved in-world object
for that node id is not currently instantiated, so `Network.GetObjectById` returns null → null-deref
AV. The Add/Remove/Rotation undo commands have the same unguarded `WorldSnapshotReaderWriter`/`obj`
lookups (`LastNode`, `ParentNode.LastChild`, `GetNodeById`). Secondary finding: `Ctrl+Z` is not
routed from the `FormSnapshotPlacements` child window to the editor undo manager (no-op from that
window). **Gap-closure fix:** (1) null-guard `obj`/`node` lookups in ALL WS undo command bodies
(Position/Rotation/Add/Remove `Execute` AND `Undo`) — bail gracefully (and re-derive/skip) when
`Network.GetObjectById`/node lookup returns null instead of dereferencing; (2) clear the editor undo
stack on snapshot Unload/Reload so stale commands can't run; (3) ideally wire Ctrl+Z from the
placements window. **This is a hard blocker on PROD-W2-WS sign-off** — undo is a core advertised bulk-op
affordance (A9) and currently takes down the client.

**GAP-CLOSURE 2026-06-13 (plans 15-09 / 15-10 / 15-11 — fixes shipped, awaiting live re-verify):**
The two A9 defects above (the BLOCKING undo crash + the minor stale-gizmo) and the Ctrl+Z routing gap
have been fixed in code and the fixed injection build reassembled. The original crash evidence above
(the `0xC0000005` records + both DEFECT blocks) is preserved verbatim — this is an annotation, not a
rewrite.

- **A9 BLOCKING undo crash — root cause fixed (15-09):** the WS bulk-op `IUndoCommand` bodies now resolve
  obj+node FIRST, then null-guard every `Network.GetObjectById` / node lookup via the pure
  `WorldSnapshotCommandGuard` bail-on-null helper (with unit coverage). All four WS command
  `Execute` AND `Undo` paths (Position / Rotation / Add / Remove; ParentNode before LastChild)
  bail gracefully instead of dereferencing null — so a stale/dangling node pointer no longer
  null-derefs the client. (Utinni `43b9dc9` helper, `08eeb51` guards.)
- **A9 secondary (stale undo stack) + Ctrl+Z routing fixed (15-10):** `UndoRedoManager.Clear()` is now a
  public seam; the editor undo stack is cleared on snapshot **Load / Unload / Reload** (and on
  BulkDelete / RemoveNode) so a pre-reload command can no longer run against a reverted node list;
  `Ctrl+Z` / `Ctrl+Y` now route from the `FormSnapshotPlacements` child window to the editor undo
  manager with refresh-after-undo ordering. (Utinni `8a888b7`; UtinniPlugins `0b7e1a1` / `d61b922`.)
- **GAP 2 (minor) stale selection gizmo on re-resolve / node-removal — fixed (15-10):** the in-world
  manipulation gizmo is cleared when its target node disappears on reload / node removal.
- **Fixed injection build reassembled + content-verified (15-11, this plan):** the full automated gate is
  green with zero regression (`UtinniCoreDotNet.Tests` 697 pass / 0 fail incl. the new 15-09/15-10
  facts; `Utinni.Cli.Tests` 249 pass / 2 skip; `Utinni.Mcp.Tests` 77 pass; native `UtinniCore.Tests.exe`
  84 assertions / 27 cases + `[resid04]` 8 / 1). The deployable layout at `D:/Code/Utinni/bin/Release/`
  was rebuilt (`Utinni.sln` + `TheJawaToolbox.sln` Release|x86, MSBuild exit 0) and **content-verified
  to carry the fix** (not just freshly-timestamped): the deployed `UtinniCoreDotNet.dll` defines type
  `WorldSnapshotCommandGuard` AND `UndoRedoManager` exposes a public `Clear`, and the deployed
  `Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` references `ClearUndoStack` (verified via
  reflection-only type/method enumeration of the deployed PEs). The maintainer cannot be handed the
  crashing DLL again.
- **A9 re-verify is the maintainer's live-smoke continuation under plan 15-08:** inject the reassembled
  `bin/Release/` build and re-run the A9 undo path (plus the still-open Checklist A re-run and
  Checklists B / C / D). **Record the A9 re-verify result back in THIS `15-SMOKE.md`** against the fixed
  build. This plan (15-11) does NOT sign off the phase — the Maintainer Sign-Off block remains the gate.

### A9 RE-VERIFY — 2026-06-13 (live, maintainer-driven) — ✅ PASS (with two follow-on fixes)

Re-ran the A9 path against a freshly rebuilt `bin/Release/` (UtinniCore.dll 09:48 + UtinniCoreDotNet.dll 10:00):

- ✅ **No crash on Undo** — single Move → main-editor Undo no longer crashes (15-09 guard holds).
- ✅ **No crash on the original recipe** — Move → Snapshot ▸ Reload → Undo: no crash (the `objNull` reload case).
- ✅ **Ctrl+Z from the Placements child window** reverts + the grid auto-refreshes (15-10 routing).
- ✅ **Undo now actually REVERTS** (was a silent no-op even after the 15-09 guard) — see follow-on fix #2.
- ✅ **Gizmo on grid-select + stale-gizmo clear** work once **node-editing mode is toggled ON** (the gizmo
  requires `EnableNodeEditing`; it was simply off — NOT a regression).

**Two defects found + fixed live during this re-verify (both blocked the smoke):**

1. **Inject-time client crash (NEW, blocked everything) — `directx9.cpp`.** On injection the `compileShader`
   D3D9 detour was installed on a hardcoded absolute address (`0x62A4F9DB`) into `s207_r.dll` with no
   validity guard — the only detour skipping the `Detour::CheckPointer` treatment the 7 vtable hooks use.
   When `s207_r.dll` is relocated by ASLR (observed live at base `0x14310000`), that address is unmapped
   and `Detour::Create`'s prologue read faults (`0xC0000005`), killing the client before the menu. Fix:
   resolve `s207_r.dll`'s actual base via `GetModuleHandleA` + PE `ImageBase`, relocate the address, and
   guard with a committed+executable `VirtualQuery` check (skip + log otherwise). Live-verified: clean
   inject + render at both relocated (`0x1435F9DB`) and preferred (`0x62A4F9DB`) bases.
   Committed: Utinni `b26e4bd`.

2. **Undo silently did not revert (functional A9 gap under the crash) — `WorldSnapshotCommands.cs`.** With
   the crash gone, Undo still left the object at the new position. Diag proved `Network.GetObjectById(id)`
   returns **null** for these snapshot placements (`objNull=True, nodeNull=False`), and the shipped 15-09
   guard `ShouldApply(obj, node)` required BOTH non-null → it bailed before reverting. Also the undo
   resolved the live node via the COPIED node's `ParentNode` (null on a copy). Fix: resolve the LIVE node
   by id from the live tree (mirrors the working `BulkMove`), make the in-world object OPTIONAL
   (node-required; revert the node data so the snapshot re-resolve repaints it), and pass the target
   position to `PositionAndRotationChanged`. Same fix applied to the rotation undo. Live-verified: Undo
   reverts + auto-refreshes. (Temporary `[A9-diag]` logging still in the deployed build — to be stripped
   before the final commit.)

**A7 (Delete) re-confirmed 2026-06-13 — ✅ PASS (tier-b deferral, end-to-end):** Delete → red confirm →
gizmo clears immediately (GAP 2 fix). The in-world object + grid row defer (engine does not de-spawn an
already-rendered object without a scene change; the node IS removed from the editor list). Verified the
full deferral: **Snapshot ▸ Reload cleared the grid row**, and a **Scene-panel Load (scene change)
de-spawned the in-world armoire**. Matches the LOCKED badge "Placements re-resolve on the next scene
change." **Candor follow-on (polish, non-blocking):** the delete confirm dialog says "This removes N
object placements from the snapshot" with no mention that the in-world object persists until a scene
change — RESID-03-style over-promise; recommend a one-line copy amend so the dialog matches the deferred
reality. Also a minor consistency note: `BulkDelete` omits the `WorldSnapshot.DetailLevelChanged()` call
that `BulkMove`/`BulkRetemplate` make (the immediate grid refresh relies on the 250ms `ScheduleRefresh`
timer instead).

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

**2026-06-13 live:** B1–B4 PASS (TRE Browser → Open in Particle Editor opens `FormParticleEditor`;
emitter tree + Field/Value/Type grid populate; raw/unknown leaf shows greyed Consolas hex; double-click →
hex sub-editor edits a leaf). _(B5–B8 in progress.)_

**DEFECT (B4/B5, display-only, non-blocking) — param grid not refreshed after a raw-bytes edit:** editing a
leaf via the hex sub-editor updates the MODEL correctly (re-opening the hex dialog shows the new value; Save
persists it), but the **Field/Value grid cell keeps showing the old hex** until the node is reselected. Root
cause: `FormParticleEditor.ApplyLeafEdit` → `AfterModelMutated()` calls `emitterTree.RefreshMutable(...)`
(tree only) and never re-runs `BindParamGrid(...)` for the current selection. Fix (managed-only, batched into
the cleanup rebuild): re-bind the param grid to the edited leaf after `AfterModelMutated()` — also covers
DoUndo/DoRedo, which share that method. Not data-affecting (the edit is real); display refresh only.

**2026-06-13 live, B5–B8:**

- **B6 ✅ PASS** — reload badge shows verbatim "Reloads on next scene change or relog." (top-right). The
  preview button is correctly disabled. (Minor candor polish: no tooltip for the *no-hot-retrigger-hook*
  reason — the only preview tooltip is the "No live client — start SWG…" case, which correctly doesn't fire
  with SWG running. Add a disabled-reason tooltip — batched.)
- **B8 ✅ PASS** — DEC-A3/D-11 footer shows verbatim: "Utinni edits emitter, timing, and color parameters
  and swaps texture/mesh references — authoring the referenced meshes or textures stays in Blender."
- **B5 ❌ BLOCKING (cross-editor) — loose-override Save fails: `netstandard.dll` façade not resolvable in the
  injected client.** Status bar: *"Could not load file or assembly 'UtinniCoreDotNet.PathContainment,
  Version=1.0.0.0, Culture=neutral, PublicKeyToken=null' or one of its dependencies."* `PathContainment.dll`
  (the netstandard2.0 assembly that owns `LooseOverridePath` since the Phase-14 `TypeForwardedTo` rework) IS
  deployed in `bin/Release`, but **`netstandard.dll` is not** — and a netstandard2.0 assembly can't load in
  the injected .NET-Framework host without the façade. This breaks the loose-override save tier for **every**
  editor (IFF/Datatable/Stringtable/ObjectTemplate/WorldSnapshot/Particle), not just Particle — it was never
  caught because in-injected loose-override save had no live test (CLI/MCP saves run in a net472 process where
  the façade resolves). `netstandard.dll` is available on the box
  (`C:\Windows\Microsoft.NET\Framework\v4.0.30319\netstandard.dll` + 4.7.2 Facades). Fix: ship `netstandard.dll`
  in the injected deploy (and confirm the injected assembly resolver probes its location); a cached failed-bind
  means a relaunch is required to pick it up. **Headline gap-closure item.**
- **B7 ⚠ blocked by B5** — Explain effect on this (raw-preserved, TRE-sourced) `.prt` returns the honest
  "Couldn't read this effect — save it to a file first, then try again." It needs an on-disk file to run
  `utinni-cli decode-iff`; you can't save-first because B5 save fails. Also separately: `utinni-cli.exe` is not
  shipped in the injection build (`LocateCli` would fail even with a file) — packaging gap. Both fold into the
  gap-closure. (The `.prt` opened here is version-0001 raw-preserved: tree shows FORM PEFT→FORM 0001, grid
  shows the D-05 raw-bytes fallback, footer "1 groups · 0 emitters · 1 raw-preserved" — the degrade path is
  working; B2/B3 typed surface was confirmed on a typed `.prt` earlier in B1–B4.)

**Checklist B verdict:** core editor works (open/parse/tree/grid/typed+raw/hex-edit/undo B1–B4, badge B6,
footer B8); **Save + Explain + Preview-disclosure-tooltip need gap-closure** (headline = the netstandard
façade / loose-override save break, which is cross-editor and also blocks Checklist D's `.stf`/`.ot` saves).

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

### 2026-06-13 live — C partial; windowed→fullscreen is a BLOCKING RESID-04 gap

- **C1/C2 — NOT exercised.** At login SWG made only two `NONEXCLUSIVE FOREGROUND (0x6)`
  `SetCooperativeLevel` calls (`caller=0x0041E5C5` / `0x0041EC1A`); the DI hook is installed
  (`patched IDirectInputDevice8A::SetCooperativeLevel vtbl[13]`). **Switching to fullscreen produced NO
  new `SetCooperativeLevel` / NO `EXCLUSIVE` request** — so SWG's fullscreen here is a *window-level*
  fullscreen, not a D3D9 exclusive switch. The D-12 suppress therefore never engages (correctly — nothing
  to suppress); we have not yet found a path that makes this build request EXCLUSIVE, so C1/C2 remain
  uncaptured.
- **D-13 holds ✅ — no Utinni device Reset, no crash.** No `Reset` / `D3DERR` / `DEVICELOST` / `VEH FATAL`
  across the transition; the SWG process stayed alive and responsive (`Responding=True`).
- **C3 windowed→fullscreen — ❌ BLOCKING gap (embed detaches + input lockup).** SWG resized to the correct
  size and kept rendering in-world, **but lost its pinning** — the SWG HWND detached from the WinForms host
  panel (editor chrome fell behind; black gutter on the right). Then **input locked up entirely** — no
  keystrokes, no mouse. Log shows the window bouncing `WM_ACTIVATE INACTIVE` / `WM_KILLFOCUS
  (gained-focus-to=0x00000000)`. Root cause (analysis): SWG's window-level fullscreen restyles/repositions
  its own HWND; nothing re-asserts Utinni's reparent/pin (and the input/focus routing that rides on it), so
  the embed pops out and input dies. The D-12 suppress addresses the *input/exclusive* axis but NOT the
  *window-pinning* axis of a window-level fullscreen. Follow-on symptom: with the window stuck `INACTIVE`
  and focus unrecoverable, SWG paused (no animation, no audio — its normal unfocused behavior) and could
  not be reactivated — the session had to be torn down. **This is the live RESID-04 residual the phase was
  meant to close — it is NOT closed for the fullscreen edge.** Fix direction: detect SWG's window
  style/extent change (e.g. in `hkWndProcHandler` / the PanelGame reparent layer) and re-assert the embed
  (reparent + reposition + restore input/focus), or intercept/deny SWG's window-level fullscreen while
  embedded. Gap-closure item.
- **C4 login→world ✅** embed survived earlier this session; **C5–C11, C.3 toggle — NOT walked** (the
  fullscreen transition locked input, ending the matrix walk for this session). Resume after the gap-closure
  fix.

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

## WAVE-5 GAP-CLOSURE 2026-06-13 (plans 15-12 … 15-17) — fixed + COMPLETE build reassembled, awaiting live re-smoke (15-18)

This annotates (does NOT rewrite) the 2026-06-13 live-smoke defect records above. The B5/B7/C3 defect
blocks, the A9 `0xC0000005` crash evidence, and the original Checklist-B/C findings are preserved
verbatim — the fixes below close them in code and reassemble the deployable build; the maintainer's live
re-verify under 15-18 is the gate.

- **B5 (netstandard façade / loose-override Save break — was BLOCKING, cross-editor) — fixed in 15-12.**
  Root cause: `ExecuteInDefaultAppDomain` runs in the default AppDomain whose APPBASE is the host exe dir
  (SWGEmu.exe), not the Utinni inject root, so the netstandard2.0 `UtinniCoreDotNet.PathContainment` façade
  never bound under injection → loose-override Save threw for **every** editor. Fix: a BCL-only
  `InjectedAssemblyResolver.ResolveProbePath` (narrow file-existence-gated allow-list = exactly
  `{ netstandard, UtinniCoreDotNet.PathContainment }`) installed as an `AppDomain.AssemblyResolve` handler
  as the FIRST statement in `Startup.EntryPoint` (before `new PluginLoader()`), plus `netstandard.dll`
  shipped next to `UtinniCoreDotNet.dll`. **Re-enables loose-override Save for every editor and unblocks
  Checklist D** (`.stf`/`.ot` saves). A cached failed-bind means a relaunch is required to pick it up.
- **C3 (windowed→fullscreen embed detach + input lockup — was BLOCKING RESID-04) — fixed in 15-13.**
  The 2026-06-13 smoke proved C3 is a WINDOW-LEVEL restyle (SWG mutates its own `GWL_STYLE`/
  `GWLP_HWNDPARENT` with ZERO new `SetCooperativeLevel`/EXCLUSIVE request — so the D-12 DirectInput
  suppress correctly never fires), and nothing re-asserted the owned-popup reparent → the embed detached
  and focus dropped to 0x0. Fix: a 250 ms `embedWatchdogTimer` in `PanelGame.cs` re-asserts the embed
  (re-strip frame + re-set owner + `RepositionSwgWindow` HWND_TOP/SWP_NOACTIVATE + `Activate()` to pull
  focus back) when it detects the style/owner change — **window-side only, NO device `Reset` (D-13)**; the
  `[resid04]` no-Reset gate stays green (8 assertions / 1 case).
- **A9 revert finalized — 15-14.** The 15-09 guard stopped the crash; 15-14 stops the silent no-op:
  `SetPosition`/`SetRotation` now resolve the LIVE node by id (not the copied node's dead `ParentNode`),
  make the in-world object OPTIONAL (node-required), and revert the node data + `DetailLevelChanged`. All 7
  temporary `[A9-diag]` `Log.Info` lines were stripped → the deployed `UtinniCoreDotNet.dll` is
  diagnostic-free (content-verified: no `A9-diag` string in the deployed PE). Live A9 already re-verified
  PASS 2026-06-13 above; this re-confirms against the diagnostic-free build.
- **B7 (utinni-cli not shipped / `LocateCli` probe) — fixed in 15-15 + completed in 15-17.** 15-15 widened
  `ParticleReadAssist.LocateCli` to probe the Utinni inject root (two levels up from the plugin dir +
  bounded marker walk-up). 15-17 (this plan) ships `utinni-cli.exe` **and its full net472 dependency
  closure** (`CommandLine.dll`, `Newtonsoft.Json.dll`, `System.Collections.Immutable.dll`,
  `System.Reflection.Metadata.dll`, `utinni-cli.exe.config`) into `bin/Release/` so the Explain-effect
  read-assist can shell `decode-iff`.
- **B4/B5 grid-rebind, B6 no-hook preview tooltip, A7 delete-confirm candor + `BulkDelete`
  `DetailLevelChanged` — folded into 15-16.** `FormParticleEditor.AfterModelMutated` now re-binds the param
  grid after a raw-hex leaf edit (and Undo/Redo); a `PreviewNoHookTooltip` gives the honest
  no-hot-retrigger-hook disabled reason; the delete-confirm dialog appends "The in-world object stays
  visible until the next scene change." (no instant-de-spawn over-promise); `WorldSnapshotImpl.BulkDelete`
  adds the immediate-grid-refresh `WorldSnapshot.DetailLevelChanged()` call.

### Reassembled + content-verified COMPLETE build (15-17)

The full automated gate is **green with zero regression** after the wave-5 fixes: `Utinni.sln` +
`TheJawaToolbox.sln` Release|x86 MSBuild exit 0; `UtinniCoreDotNet.Tests` 706 pass / 0 fail (incl. the new
`InjectedAssemblyResolver` + node-only guard facts); `Utinni.Cli.Tests` 249 pass / 2 skip;
`Utinni.Mcp.Tests` 77 pass; native `UtinniCore.Tests.exe` 84 assertions / 27 cases + `[resid04]` 8
assertions / 1 case (the no-Reset gate held after the 15-13 watchdog).

The deployable injection build at `D:/Code/Utinni/bin/Release/` was rebuilt and **completed with the two
previously-missing files** — `netstandard.dll` (next to `UtinniCoreDotNet.dll`, B5 façade) and
`utinni-cli.exe` + its net472 dependency closure (B7) — then **content-verified** (anti-stale, not mtime
alone) via reflection-only enumeration + a byte-string grep of the DEPLOYED PEs: the deployed
`UtinniCoreDotNet.dll` defines `InjectedAssemblyResolver` AND `WorldSnapshotCommandGuard` AND exposes
`UndoRedoManager.Clear`, and contains NO `A9-diag` string; `netstandard.dll`, `utinni-cli.exe` (+ closure),
and the deployed `Plugins/TheJawaToolbox/TheJawaToolboxDotNet.dll` are all present. The maintainer cannot be
handed a stale or incomplete build.

### Remaining live re-smoke → plan 15-18 (record results back in THIS file)

Inject the reassembled `bin/Release/` build and resume the still-open smoke against the fixed + complete
binaries:
- **B5–B8** now that loose-override Save works (Save → dirty clears; Explain-effect shells `decode-iff` via
  the shipped `utinni-cli.exe`; Preview disabled-reason tooltip).
- **Checklist C full matrix INCLUDING the C3 windowed→fullscreen re-verify** (the embed should re-assert via
  the 15-13 watchdog; confirm no detach / no input lockup, no device Reset) plus C4–C15.
- **Checklist D** `.stf`/`.ot` loose-override saves (unblocked by the B5 façade fix).

This plan (15-17) does NOT sign off the phase — the **Maintainer Sign-Off** block below remains the gate.

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
