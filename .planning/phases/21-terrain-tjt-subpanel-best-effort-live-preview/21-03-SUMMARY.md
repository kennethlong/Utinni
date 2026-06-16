---
phase: 21-terrain-tjt-subpanel-best-effort-live-preview
plan: 03
subsystem: ui
tags: [terrain, trn, tjt, subpanel, mef, tre-browser, handoff, live-preview, dec-c4]

# Dependency graph
requires:
  - phase: 21 plan 01
    provides: "TerrainSaveTargets.SaveLooseOverride/ApplyFieldEdit/ResolveIhdrLeafStableId + TerrainReloadCandor.StatusCopy (consumed transitively via the host)"
  - phase: 21 plan 02
    provides: "FormTerrainEditor (TJT.UI.Forms) — the roomy host; OpenFromTreEntry(payload, archivePath, logicalPath) + OpenLooseOverride(loosePath) open entries"
  - phase: 08 (reload framework)
    provides: "ClientReloadDispatcher (consumed transitively via the host's Save/Preview)"
provides:
  - "TerrainSubPanel (TJT.UI.SubPanels): the D-01 thin docked entry SubPanel registered in the existing SubPanelContainer(\"Controls\", …) array; launches the singleton FormTerrainEditor (hide-not-dispose); public OpenFromTre/OpenLooseOverride entries for the TRE-Browser hand-off"
  - "FormTreBrowser \"Open in Terrain Editor\" context-menu hand-off (gated to a resolvable .trn) → reaches the docked TerrainSubPanel via GetStandalonePanels() and calls OpenFromTre (read-only; first commit Save As Override, D-08)"
  - "Net-new public FormTerrainEditor.PromptOpenLooseOverride() — the single .trn loose-override file picker, callable from the thin SubPanel"
affects: [21-04 (D-07 maintainer live-smoke now has the full user-reachable path: TRE Browser → Open in Terrain Editor, and the docked panel → Open Terrain Override)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Thin-docked-SubPanel-launches-roomy-Form host (D-01 entry + D-02 escape hatch): the 417px-pinned SubPanel is a pure launcher; all tree+field editing lives in FormTerrainEditor (SnapshotPanel → FormSnapshotPlacements precedent applied to terrain)"
    - "Registration via the existing SubPanelContainer(\"Controls\", …) array — GetSubPanels() stays null, the MEF SPI is NOT widened (CON-M-01/02, STAB-04)"
    - "TRE-Browser hand-off to a SubPanel-owned host: the hand-off target is reached via GetStandalonePanels() (the \"Controls\" container), walking the FlowLayoutPanel→CollapsiblePanel→SubPanel control tree, NOT the forms list (consistent with D-02)"

key-files:
  created:
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/SubPanels/TerrainSubPanel.cs"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/SubPanels/TerrainSubPanel.Designer.cs"
  modified:
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/Plugin.cs (register TerrainSubPanel in the Controls array)"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTreBrowser.cs (Open in Terrain Editor hand-off)"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/UI/Forms/FormTerrainEditor.cs (net-new public PromptOpenLooseOverride)"
    - "D:/Code/UtinniPlugins/The Jawa Toolbox/TheJawaToolboxDotNet/TheJawaToolboxDotNet.csproj (register the two new sources)"

key-decisions:
  - "Loose-override picker lives ONCE in the host (FormTerrainEditor.PromptOpenLooseOverride); the thin SubPanel is a pure launcher with no modal dialog of its own — keeps the .trn picker single-sourced AND satisfies the literal 'zero ShowDialog in the SubPanel' acceptance gate"
  - "TRE-Browser hand-off gate is extension-only (.trn) at menu-open time (payload resolves lazily on click); the terrain codec degrades-not-fails inside FormTerrainEditor.OpenFromTreEntry, so no separate FORM-PTAT content policy was added (the IFF editor's enumerate-only-guard idiom is matched)"
  - "Hand-off reaches the host via GetStandalonePanels() (D-02 — the docked SubPanel owns the singleton Form), walking the control tree because SubPanelContainer exposes no SubPanels accessor (it wraps each SubPanel in a CollapsiblePanel)"

patterns-established:
  - "Pattern 1: thin docked entry SubPanel as a pure launcher for a roomy host Form (the 417px-width escape hatch, D-01+D-02)"
  - "Pattern 2: TRE-Browser hand-off to a SubPanel-owned singleton host (reached via GetStandalonePanels() control-tree walk, not the forms list)"
  - "Pattern 3: single-source modal dialog — the picker lives in the host, exposed public so the launcher triggers it without duplicating ShowDialog"

requirements-completed: [PROD-W2-TRN-05]

# Metrics
duration: 6min
completed: 2026-06-16
---

# Phase 21 Plan 03: TerrainSubPanel docked entry + TRE-Browser hand-off Summary

**The D-01 thin docked `TerrainSubPanel` that launches the roomy `FormTerrainEditor` host as a singleton (hide-not-dispose), registered via the existing `SubPanelContainer("Controls", …)` array (MEF SPI unchanged — `GetSubPanels()` stays null), wired to both D-08 entry points — the panel's own "Open Terrain Override…" loose-override open and the TRE Browser's `.trn`-gated "Open in Terrain Editor" hand-off (read-only → Save As Override) — with an MEF-reject-guarded ctor and a null-safe undo seam.**

## Performance

- **Duration:** ~6 min (research/reads → Task 1 commit 8637bbc → Task 2 commit f9c75e2)
- **Tasks:** 2
- **Files modified:** 6 (2 created, 4 modified; all in the UtinniPlugins sibling repo)

## Accomplishments
- `TerrainSubPanel` (`TJT.UI.SubPanels`, `: SubPanel, ISceneAvailability`, ctor `(IEditorPlugin, HotkeyManager, UtINI) : base("Terrain")`): the thin docked entry point (D-01). Fits the 417px-pinned column: a `Dock.Top` banner (Bold) + 2px `Colors.Secondary()` accent rule, an action row with "Open Terrain Editor" (launch) + "Open Terrain Override…" (loose open) buttons, a `Colors.FontDisabled()` hint that TRE-sourced opens come from the TRE Browser, and the dimmed verbatim DEC-A3 candor footer.
- MEF-safe ctor (D-09 / Pitfall 8): the WHOLE control build runs inside the ctor try/catch — a partial build surfaces a read-only state label rather than throwing (a throwing SubPanel ctor in the `SubPanelContainer` array would silently cascade the entire `IEditorPlugin` out of MEF compose). The undo seam is referenced only in comments and is null-safe by construction (no bare `editorPlugin.Undo/.Redo/.ClearUndoStack`).
- Singleton hide-not-dispose host launch (SnapshotPanel companion-window idiom): `if (terrainForm == null || terrainForm.IsDisposed) terrainForm = new FormTerrainEditor(editorPlugin); if (terrainForm.Visible) Activate(); else Show();` — never a `ShowDialog`-per-click. The Designer's `Dispose` tears the host down when the panel itself is disposed.
- Public `OpenFromTre(payload, archivePath, logicalPath)` and `OpenLooseOverride(loosePath)` entries (both launch the singleton then forward to the host's `OpenFromTreEntry`/`OpenLooseOverride`, never throwing) — the surface the Task-2 TRE hand-off calls.
- `Plugin.cs`: `new TerrainSubPanel(this, hotkeyManager, ini)` added to the existing `SubPanelContainer("Controls", new SubPanel[]{ … })` array (after `SnapshotPanel`). `GetSubPanels()` STILL returns null — the MEF SPI is NOT widened (CON-M-01/02, STAB-04), matching every shipped SubPanel.
- `FormTreBrowser`: an "Open in Terrain Editor" `ToolStripMenuItem` added to `BuildTvTreContextMenu`, gated in `OnTvTreContextMenuOpening` to a non-enumerate-only `.trn` entry; on click it resolves the payload OFF the UI thread (mirroring `OnOpenInIffEditor`), then `BeginInvoke`s back, locates the docked `TerrainSubPanel` via `GetStandalonePanels()` (the "Controls" container, walking the `FlowLayoutPanel → CollapsiblePanel → SubPanel` control tree), and calls `OpenFromTre`. First commit from a TRE source is "Save As Override…" (host-enforced, D-08 — the TRE is never edited in place).
- `FormTerrainEditor`: extracted the loose-override file picker into a net-new public `PromptOpenLooseOverride()` (the existing `OnOpenClicked` now delegates to it) so the thin SubPanel triggers the SAME `.trn` dialog instead of running its own modal — one source of the picker.

## Task Commits

Each task committed atomically in the UtinniPlugins sibling repo (cross-repo paired — no human checkpoint per standing authority; the live smoke is Plan 04):

1. **Task 1: TerrainSubPanel docked entry + singleton host launch + host's public PromptOpenLooseOverride** — `8637bbc` (feat) [UtinniPlugins]
2. **Task 2: register TerrainSubPanel in Plugin.cs + TRE-Browser "Open in Terrain Editor" hand-off** — `f9c75e2` (feat) [UtinniPlugins]

## Files Created/Modified
- `…/UI/SubPanels/TerrainSubPanel.cs` — the thin docked entry SubPanel (banner + Open buttons + hint + DEC-A3 footer; singleton hide-not-dispose host launch; public `OpenFromTre`/`OpenLooseOverride`; idempotent `UpdateSceneAvailability`; MEF-safe ctor; null-safe undo seam).
- `…/UI/SubPanels/TerrainSubPanel.Designer.cs` — IContainer/Dispose plumbing + a no-op `InitializeComponent` (the layout is built imperatively in the .cs so the Pitfall-8 Dock order is explicit); disposes the launched host.
- `…/Plugin.cs` — `TerrainSubPanel` added to the `SubPanelContainer("Controls", …)` array; `GetSubPanels()` unchanged (returns null).
- `…/UI/Forms/FormTreBrowser.cs` — "Open in Terrain Editor" menu item + `.trn` visibility gate + off-thread payload-resolve click handler + `FindTerrainSubPanel` (GetStandalonePanels control-tree walk).
- `…/UI/Forms/FormTerrainEditor.cs` — net-new public `PromptOpenLooseOverride()` (the single `.trn` picker; `OnOpenClicked` delegates to it).
- `…/TheJawaToolboxDotNet.csproj` — registered both new SubPanel sources (old-style explicit `<Compile Include>` project).

## Decisions Made
- All planner-locked calls honored: D-01 (docked SubPanel entry), D-02 (the heavy tree+grid lives in the launched `FormTerrainEditor`), D-08 (both entry points; TRE read-only → Save As Override), D-09 (MEF-safe ctor + null-safe undo seam), CON-M-01/02 / STAB-04 (`GetSubPanels()` stays null — SPI not widened).
- The loose-override picker is single-sourced in the host (`PromptOpenLooseOverride`) and the SubPanel is a pure launcher — this both keeps the `.trn` dialog in one place and satisfies the literal "zero `ShowDialog` in the SubPanel" acceptance gate.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Extracted net-new public `FormTerrainEditor.PromptOpenLooseOverride()`**
- **Found during:** Task 1 (the SubPanel's "Open Terrain Override…" button).
- **Issue:** The plan's Task-1 acceptance gate is literal — `grep -c 'ShowDialog'` must equal `0` in `TerrainSubPanel.cs`. The straightforward implementation (a `.trn` `OpenFileDialog` inside the SubPanel) would trip that gate AND duplicate the picker the host (`FormTerrainEditor.OnOpenClicked`) already owns.
- **Fix:** Extracted the host's existing loose-override picker into a net-new public `PromptOpenLooseOverride()` (`OnOpenClicked` now delegates to it). The SubPanel's "Open Terrain Override…" button launches the singleton host and calls `host.PromptOpenLooseOverride()` — no modal dialog in the panel, one source of the `.trn` picker.
- **Files modified:** `…/UI/Forms/FormTerrainEditor.cs`, `…/UI/SubPanels/TerrainSubPanel.cs`
- **Commit:** `8637bbc`

**2. [Rule 3 - Blocking] `FindTerrainSubPanel` walks the control tree (SubPanelContainer has no SubPanels accessor)**
- **Found during:** Task 2 (the hand-off lookup).
- **Issue:** The plan describes finding the `TerrainSubPanel` "in the plugin's `GetStandalonePanels()` 'Controls' container." `SubPanelContainer` is a `FlowLayoutPanel` that wraps each `SubPanel` in a `CollapsiblePanel` (private field, no public `SubPanels` accessor) — so a direct `container.SubPanels` enumeration does not compile.
- **Fix:** `FindTerrainSubPanel` recursively walks the `SubPanelContainer.Controls` tree (`FlowLayoutPanel → CollapsiblePanel → SubPanel`) and returns the first `TerrainSubPanel`. Still reaches the host via `GetStandalonePanels()` (D-02), not the forms list.
- **Files modified:** `…/UI/Forms/FormTreBrowser.cs`
- **Commit:** `f9c75e2`

**Total deviations:** 2 (both blocking, both Rule 3). No scope creep — #1 is a single-source-the-picker refactor; #2 is the correct traversal of the existing container type.

## Issues Encountered
- **Git Bash mangles `/p:` MSBuild switches.** MSYS path-conversion strips the leading `/` from `/p:Configuration=…`, producing `MSB1008: Only one project can be specified`. Used the dash form (`-p:Configuration=Release -p:Platform=x86 -t:Build -v:m`) which MSYS leaves untouched. The build is otherwise clean (VS2026 MSBuild x86/Release, `D:\…\Plugins\TheJawaToolbox\TheJawaToolboxDotNet.dll`).
- **No framework change in this plan.** It is a pure UtinniPlugins UI change (a separate repo); the Utinni `UtinniCoreDotNet` framework and `Generated/UtinniCore.cs` are byte-untouched, so the Plan 01 framework suites are unaffected (no rebuild of the Utinni solution was needed or done). The documented pre-existing Phase-17 `AbiSurfaceTests` ABI-gate drift remains independent of this phase (tracked in `deferred-items.md`).

## Known Stubs
None. The SubPanel is a thin-but-complete launcher: both D-08 entry points are wired and reach the fully-functional Plan-02 host. `UpdateSceneAvailability` is intentionally a no-op-on-content latch (the panel has no live-only control of its own; the host's Preview button gates on `Game.IsRunning` internally) — that is correct behavior, not a stub. The D-07 live-render disposition (does `GroundScene::ReloadTerrain` re-read a procedurally-edited `.trn` in-session) remains the Plan-04 maintainer live-smoke by design; the host's footer honestly reports the `PendingNextSceneChange` tier via `TerrainReloadCandor` until then.

## Threat Flags
None. The two plan-registered trust boundaries are both mitigated as specified: the MEF-compose boundary (`TerrainSubPanel` ctor is wholly inside try/catch — a partial build surfaces a state label, never throws the compose), and the TRE→editor boundary (TRE opened read-only; first commit is host-enforced "Save As Override…"). `GetSubPanels()` stays null (SPI not widened). No new network/auth/file surface beyond the host's already-contained loose-override save path.

## User Setup Required
None for the build/automation gate. The D-07 live-SWG smoke is the Plan 04 maintainer gate (it also requires re-enabling the loose-override `searchPath` the maintainer disabled for the phantom-walk mitigation — noted in RESEARCH / Plan 02).

## Next Phase Readiness
- **Plan 04** now has the complete user-reachable path to exercise in the live smoke: TRE Browser right-click a `.trn` → "Open in Terrain Editor" (read-only → Save As Override), AND the docked Terrain panel → "Open Terrain Override…" (direct loose open) → edit a field → Save/Preview → observe the in-client reload disposition. Flipping `TerrainReloadCandor.LivePreviewObserved` (if the smoke confirms in-session re-read) upgrades the host's footer automatically.
- No blockers. The pre-existing Phase-17 ABI-gate failure is independent of this phase.

## Self-Check: PASSED

All created/modified files exist on disk (`TerrainSubPanel.cs`, `TerrainSubPanel.Designer.cs`, `Plugin.cs`, `FormTreBrowser.cs`, `FormTerrainEditor.cs`) and both task commits resolve in the UtinniPlugins repo (`8637bbc`, `f9c75e2`). `TheJawaToolbox.sln` builds clean (VS2026 MSBuild x86/Release); the singleton-launch (`IsDisposed` + `Activate`/`Show`, zero `ShowDialog`), null-safe-undo, SPI-unwidened (`GetSubPanels() { return null; }`), `.trn`-gated hand-off, and `GetStandalonePanels()` lookup grep gates all pass.

---
*Phase: 21-terrain-tjt-subpanel-best-effort-live-preview*
*Completed: 2026-06-16*
