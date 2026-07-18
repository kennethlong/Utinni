# Provider Request — §5.6 write-notify row: object::positionAndRotationChanged (gizmo-drag evidence)

**Status:** REQUEST (single advertised row; the §4C write-path probe result you asked us to bring back) · 2026-07-18
**From:** Utinni (consumer) · **To:** swg-client-v2 (provider) · **Baseline:** contract **v19 / 140 names**
**Copy convention:** delivered as `swg-client-v2/.planning/handoff/2026-07-18-utinni-goalB-positionchanged-row-REQUEST.md`;
the Utinni copy governs.

## 1. The evidence you asked for (§4C: "probe the write path; if dead on advertised come back with it")

Wave-3 unlocked the live gizmo on the advertised client (your camera-matrix rows — thank you, they
work; the gizmo renders and tracks). Dragging an axis then crashed. cdb-confirmed the write path:

```
Object::setTransform_o2w(...)          -> advertised (object::setTransform_o2w), works, moves the object
Object::positionAndRotationChanged(...) -> UNADVERTISED: hardcoded SWGEmu RVA 0x00B22A50, garbage on NGE -> AV
```

`positionAndRotationChanged` is the one call in the gizmo-drag (and managed bulk-move / SetSelectedNodePosition)
write path that never had an advertised row — only `getTransform_o2w`/`setTransform_o2w` do. So the transform
SET succeeds (object visually moves) and the spatial-bookkeeping NOTIFY crashes.

Consumer mitigation shipped (Utinni `71cb019`): `Object::positionAndRotationChanged` is guarded to a no-op
on the advertised client — the gizmo drag no longer crashes and the object moves visually (setTransform_o2w),
but its sphere-tree / portal-cell / collision state is NOT updated (the notify is skipped). That's an
acceptable editor-preview degrade, not correct — a dragged object may have stale collision/culling until a
scene change.

## 2. Ask

Advertise `object::positionAndRotationChanged` — the same `Object::positionAndRotationChanged(bool
dueToParentChange, Vector const& oldPosition)` __thiscall the consumer already models
(`void(__thiscall*)(Object*, bool, Vector&)`, our slot at the SWGEmu RVA 0x00B22A50). A plain member
row like the transform pair; no shim expected (Vector& is a primitive-ish math struct, not a
CrcString/basic_string ABI trap — but confirm the Vector layout matches if you'd rather pass it POD).
On the row landing we drop the advertised guard and the gizmo/bulk-move write becomes fully correct
(collision + portal + culling update live).

## 3. Not blocking Wave 3

Persistence (save/unload/reload), targeting, and gizmo RENDER + drag-move-visual all work on the
current stage. This row upgrades the gizmo write from "moves visually" to "moves correctly." Bundle it
with any next handback; no version pressure. (Also still standing from the Wave-3 handback: the
`cuiRadialMenuManager::clear` row would let us restore the radial-menu-clear-on-gizmo nicety we
likewise guarded off — Utinni `b3144e7`; strictly optional.)
