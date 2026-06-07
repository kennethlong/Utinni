# Phase 15: Wave-2 editors (WorldSnapshot, Particle) + presentation residuals - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-07
**Phase:** 15-wave-2-editors-worldsnapshot-particle-presentation-residuals
**Areas discussed:** WorldSnapshot delta, Particle .prt codec depth, Particle editor + AI-assist, RESID-03/04 scope

---

## WorldSnapshot editor delta (PROD-W2-WS)

| Option | Description | Selected |
|--------|-------------|----------|
| Placements list/table + search | Browsable table of all nodes + search/filter + click-to-select driving the existing gizmo | |
| List + multi-select bulk ops | The table above PLUS multi-select for bulk move/delete/retemplate | ✓ |
| Conform + polish only | Treat the existing panel as the deliverable; minimal new UI | |

**User's choice:** List + multi-select bulk ops
**Notes:** The shipped `SnapshotPanel` already does in-world single-node gizmo editing; the Wave-2 delta is a real editor surface (table + bulk ops) over the existing `WorldSnapshotReaderWriter` — zero new format work.

---

## Particle `.prt` codec depth (PROD-W2-PRT)

| Option | Description | Selected |
|--------|-------------|----------|
| Read + targeted-edit, raw-preserve | Decode only exposed fields; raw-preserve the rest | |
| Full typed decode | Type every emitter/wave/timing field | ✓ |
| Inspect-only first slice | Read-only viewer this phase; edit deferred | |

**User's choice:** Full typed decode
**Notes:** Ambitious format investment. Flagged tension: full typed decode + zero fixtures + MEDIUM confidence is the riskiest codec path — resolved by the degrade-on-unknown fallback decision below.

### Fallback on unrecognized `.prt` variants

| Option | Description | Selected |
|--------|-------------|----------|
| Degrade: raw-preserve unknowns | Type known fields; raw-preserve unrecognized chunks for byte-safe round-trip; never abort | ✓ |
| Hard-fail with diagnostic | Refuse to open unrecognized variants | |

**User's choice:** Degrade: raw-preserve unknowns
**Notes:** Matches the project's OT-multichunk degrade-don't-abort precedent; de-risks the no-fixtures format.

---

## Particle editor + AI-assist (PROD-W2-PRT)

| Option | Description | Selected |
|--------|-------------|----------|
| Prompt to parameter tweak | NL prompt mutates decoded emitter params via the codec | |
| AI read-assist only | AI explains/summarizes + suggests as text; modder applies manually | ✓ |
| Defer AI, ship manual editor | Manual editor now, AI later | |

**User's choice:** AI read-assist only

### AI read-assist delivery surface

| Option | Description | Selected |
|--------|-------------|----------|
| Via Phase-14 MCP server | Codec via CLI/MCP read tools; AI runs in an MCP client | |
| In-TJT AI panel | In-editor button calls AI directly | |
| Both surfaces | CLI/MCP read tools AND an in-TJT assist button reusing that path | ✓ |

**User's choice:** Both surfaces
**Notes:** Coherent with the v2.0 "AI-Assisted" milestone + Phase-14 MCP-as-centerpiece. The in-app button reuses the CLI/MCP read path — no independent AI/format path.

### Live in-client preview

| Option | Description | Selected |
|--------|-------------|----------|
| Play at camera on demand | Preview button spawns/plays the effect at camera via injected runtime | |
| Hot-retrigger loaded instances | Re-trigger the effect instances already live in the scene after save/reload | ✓ |
| No live preview in V1 | Edit + save only | |

**User's choice:** Hot-retrigger loaded instances
**Notes:** The heavier option — the runtime hook into the running effect manager is the open research item for the planner/researcher.

---

## Preview-vs-author boundary (DEC-A3)

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, use these | WorldSnapshot: place/transform/retemplate existing templates. Particle: edit params + swap texture/mesh references | ✓ |
| Particle should be stricter | Params only, no reference swapping | |
| Let me reword | User supplies wording | |

**User's choice:** Yes, use these (the proposed one-sentence tests for both editors)

---

## RESID-04 — window-resize / windowed↔fullscreen

| Option | Description | Selected |
|--------|-------------|----------|
| Enumerate live, fix root cause | Fill the matrix, confirm root cause, apply targeted intercept/suppress fix | ✓ |
| Targeted fix only | Straight to intercepting the fullscreen switch, skip the matrix | |
| Enumerate-only, defer fix | Document symptoms, defer the fix | |

**User's choice:** Enumerate live, fix root cause
**Notes:** Prime suspect = exclusive-fullscreen mode switch detaching the embed. Hard constraint: no device `Reset` (resize the window). Maintainer triaged "low priority, likely not a hard find" — keep the fix targeted.

---

## RESID-03 — SC3 live-reload candor

| Option | Description | Selected |
|--------|-------------|----------|
| Live-observe + honest badges | Observe SC3 for .stf + OT, set honest badge copy, apply candor to new editors' reload paths | ✓ |
| Candor badges, observe if possible | Wire badges; attempt observation but don't block | |
| Defer RESID-03 | Badges on new editors only; defer the live observation | |

**User's choice:** Live-observe + honest badges
**Notes:** Needs a live injected session. Do not loosen badge copy to over-promise; relog-only reloads must say so.

---

## Claude's Discretion

- WorldSnapshot bulk-edit undo/command wiring (compose existing per-node edit commands).
- `.prt` field taxonomy + `UtinniCoreDotNet/Formats/Particle/` layout.
- Placements table control/column layout (follow Datatable editor grid conventions).
- `.prt` CLI verb naming (Phase-13 conventions; mind the 16-verb CommandLineParser cap).

## Deferred Ideas

- Terrain editor `PROD-W2-TRN` (`.trn`) → v2.1.
- Particle prompt-to-mutate AI → after typed codec + manual editor are proven.
- Deliberate detached-fullscreen mode with clean re-attach → only if targeted suppress proves wrong.
- Broader `.prt` fixture corpus / golden round-trip tests → Tier-2 follow-up.
