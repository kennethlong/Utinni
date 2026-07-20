---
title: SWGEmu parity for the .ilf pointer-keyed pick + manipulation path
area: editor-unlock / world-editor
status: OPEN
opened: 2026-07-19
source: 24-CONSULT-69-EXPERIMENT-RESULT.md (advertised PASS) + SWGEmu TRE census this session
owner: consumer-only (no provider involvement — the provider speaks for the NGE client only)
severity: low (Core3 spawns most interior furniture server-side with real ids, so the id-less
  .ilf slice is smaller on SWGEmu than on the advertised client)
---

# SWGEmu parity: .ilf interior-decoration selection + manipulation

## Context

CONSULT-69's decisive experiment PASSED on the advertised client (2026-07-19): the hud's
pointer-keyed hover pick reaches id-less .ilf interior decorations, and the latched Object*
drives the advertised transform rows — a cantina chair moved live, zero engine changes.

That path is advertised-only at every leg (cuiHud g_instance/getTarget rows, v20
collideScreenRay, the probe tick inside hkUpdateLoop's advertised block, the engine
preference targeting filter). SWGEmu has none of those rows.

## Why it plausibly ports (consumer-only)

- The Pre-CU content class EXISTS: 220 `.ilf` entries across the SWGEmu TREs (110 in
  data_other_00.tre + patches) — measured 2026-07-19 via `utinni-cli parse-tre`.
- SWGEmu already has equivalent, pointer-keyed pick machinery in-tree (Utinni's original
  2020-era path, all SWGEmu literals): `targetUnderCursor` (mid-CuiHud::update asm hook
  capturing the Object* under cursor), `hkGetTarget`'s own clientWorld::collide cursor ray
  (hit object + point), `collideCursorWithWorld`.
- Manipulation legs are SWGEmu literals too: `object::move` / `setTransform_o2w` (the
  existing SWGEmu gizmo already drives them).
- The targeting filter equivalent is the AllowTargetEverything byte patch (works today).

## The work

1. Re-run the CONSULT-69 experiment shape on SWGEmu: wire the probe/latch/nudge to
   `targetUnderCursor` instead of the advertised watcher. Verify (a) the 2002 engine's
   .ilf spawns are likewise id-less, (b) they are pointer-stable within a scene, (c) the
   byte-patch filter lets the pick reach them.
2. If PASS: fold SWGEmu into whatever real .ilf gizmo path ships for the advertised client
   (dual-path at the pick-read seam, shared latch/manipulate/UX).
3. Persistence: SWGEmu inherits whatever model wins the CONSULT-69 product question
   (per-instance vs per-template); the .ilf loose-override mechanics should behave the same
   (searchPath override) but need a SWGEmu verification pass.

## Blocked on / sequenced after

The advertised .ilf gizmo path shipping first (this item is parity, not pathfinding), and
the CONSULT-69 product-question answer for the persistence half.
