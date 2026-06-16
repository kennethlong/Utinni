---
title: TRE-Browser "Open in Terrain Editor" fails while the docked Terrain section is collapsed
area: tjt-ui
status: OPEN
opened: 2026-06-16
source: 21-SMOKE-LOG.md (R3)
owner: maintainer
severity: low (UX — clear workaround: expand the Terrain section first)
---

# "Terrain Editor is unavailable in this session" when the Terrain SubPanel is collapsed

## What's open

`FormTreBrowser.FindTerrainSubPanel()` walks the docked control tree
(`SubPanelContainer → CollapsiblePanel → SubPanel`) to reach the singleton-launching `TerrainSubPanel`
(D-02). The `CollapsiblePanel` realizes its `TerrainSubPanel` child lazily — only after the section is first
expanded — so on a fresh session (Terrain section collapsed by default) the walk returns `null` and the
"Open in Terrain Editor" hand-off reports **"Terrain Editor is unavailable in this session."**

Workaround (used in the 21-04 smoke): expand the docked **Terrain** section once, then the hand-off works.

## To close

- Either realize the `TerrainSubPanel` eagerly (construct its child on panel add, independent of expand
  state), OR have `Plugin.cs` keep a direct reference to the constructed `TerrainSubPanel` instance and have
  `FindTerrainSubPanel` consult that registry instead of (or before) the lazy control-tree walk.
- Regression: assert the hand-off resolves the panel with the Terrain section collapsed.
