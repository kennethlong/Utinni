# Phase 11: TJT subpanel — Object Template Editor - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-30
**Phase:** 11-tjt-subpanel-object-template-editor
**Areas discussed:** Inheritance resolution, Typed value editing, Type coverage + schema, Editing scope (T-level)

---

## Gray-area selection

Four Phase-11-specific gray areas were offered (reload semantics + write-strategy/byte-exactness were folded into the framing as CF-05 / Phase-8-IFF-lineage carried-forward). User selected **all four** to discuss.

---

## Inheritance resolution

| Option | Description | Selected |
|--------|-------------|----------|
| Effective view + origin | One merged row per field with effective resolved value + origin marker (local override vs inherited-from-`<base>`); ancestor chain as breadcrumb; editing an inherited field promotes it to a local override. SOE template-editor mental model. | ✓ |
| Local-only + base panel | Show only this template's local params editable (Phase 7's read set), with the resolved base chain in a separate read-only side panel. Simpler, but inherited fields not editable inline — weaker against SC2. | |
| You decide | Defer presentation to planner/UI-phase after researcher confirms nesting depth + field counts. | |

**User's choice:** Effective view + origin
**Notes:** Directly satisfies SC2 ("view inherited fields, edit overrideable fields"). Claude added a locked edge-case behaviour: on an unresolvable base template, degrade gracefully (show local fields, flag inherited rows as "unresolved base"), never block the open.

---

## Typed value editing

| Option | Description | Selected |
|--------|-------------|----------|
| Hybrid: typed + hex | Typed widgets for common scalar param types (bool/int/float/string/stringId/template-ref/enum + range/delta wrappers); raw hex/bytes fallback (Phase 8 IFF leaf editing) for complex types (struct/list/dynamicvar). No param type ever uneditable. | ✓ |
| Full typed, all types | Typed widget for EVERY value type incl. struct params, weighted/random lists, dynamic-variable lists. Most faithful, no hex fallback — but the hardest encodings risk the final-V1-phase timeline. | |
| Raw structural only | No per-type widgets; edit every param value via the Phase 8 hex/text leaf editor. Lowest effort, fully generic, poor UX. | |

**User's choice:** Hybrid: typed + hex
**Notes:** SWG param values are self-describing on disk, so typed decode needs no external schema. Hex fallback guarantees completeness for types not modeled in V1.

---

## Type coverage + schema

| Option | Description | Selected |
|--------|-------------|----------|
| Generic, all types | One generic param editor across EVERY template type, driven purely by the self-describing `.iff`. No `.tdf`/loader schema port. "Add an override for a field present nowhere in the chain" → V2 (needs schema). Lowest risk for the milestone-closing phase. | ✓ |
| Type-aware subset | Port generated `Shared*ObjectTemplate.cpp` schema for common types (tangible + shared bases) for not-yet-present-field add, enum dropdowns, validation. Richer but bounded coverage + significant porting on the riskiest phase. | |
| Generic + cheap hints | Generic across all types plus opportunistic enum/field hints where cheap. Middle ground. | |

**User's choice:** Generic, all types
**Notes:** The `.tdf` schema SOURCE isn't in the swg-client-v2 corpus anyway (only the generated loaders are). This bounds the next decision: adding a field absent from the entire chain is inherently V2.

---

## Editing scope (T-level)

| Option | Description | Selected |
|--------|-------------|----------|
| Override/revert + edit | (1) edit a local override's value, (2) add override — promote an inherited field to local, (3) remove override — revert to inherited. Param-count + `@derived`/loaded flags machine-managed (writer-maintained), not hand-edited. Change-base NOT in V1. | ✓ |
| Edit existing only | Edit only values of params already locally present (Phase 7's set). No promote/revert. Simplest, but undercuts "edit overrideable fields." | |
| Override/revert + change base | Recommended set PLUS editing the DERV base reference (re-parent). Powerful but risky — re-parenting invalidates the resolved view + can orphan local overrides. | |

**User's choice:** Override/revert + edit
**Notes:** Machine-managed boundary mirrors Phase 10's machine-managed `id`. Change-base (DERV re-parenting) and adding-a-chain-absent-field both deferred to V2.

---

## Claude's Discretion

- Editor surface / columns / origin rendering / override-revert triggers / breadcrumb / hex-fallback sub-editor surfacing — deferred to planner + `/gsd-ui-phase 11`. Locked floor: inherited fields viewable with visible origin; values editable; override/revert exist; structural bookkeeping machine-managed.
- `ObjectTemplate` mutable model ↔ existing `ObjectTemplateDecoder` relationship (wrap/reuse vs supersede) — recommend reuse + parallel mutable type on `MutableIffDocument`.
- CLI round-trip verb shape — whether existing `roundtrip-iff` subsumes object templates (they ARE IFF) or a dedicated verb adds value.
- CF-05 reload-trigger badge wording — follows researcher's confirmation of `ObjectTemplateList` reload semantics (respawn vs scene-change vs relog).
- Plan decomposition (~4–6 plans), incl. the final V1 release-gate verification + tag.

## Deferred Ideas

- Type-aware schema (port `Shared*ObjectTemplate.cpp` / `.tdf`) → not-yet-present-field add, enum dropdowns, validation. Biggest V2 enrichment.
- Change-base / DERV re-parenting → V2.
- Full typed widgets for struct/weighted-list/dynamicvar types → V2 (V1 uses hex fallback).
- New-object-template-from-scratch designer → V2.
- In-memory live patch (Phase 8 mode 3) → stays disabled inherited.
- Cross-reference / "find usages" → V2.
- Shared abstract editor base class across all five SubPanels → post-Wave-1 refactor (strongest candidate yet).
