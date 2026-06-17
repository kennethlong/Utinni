# Phase 22: ClientEffect Editor - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-17
**Phase:** 22-clienteffect-editor
**Areas discussed:** Edit scope (strings), Editor form factor, Live preview, Open/save workflow

---

## Edit scope (strings)

### Q1 — Variable-length string edits (repoint appearance/sound/FF refs) in scope?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — strings + scalars | Full edit: string refs AND scalar/flag fields, byte-exact with FORM-length re-stamping (captured-slice IffWriter recomputes parent lengths). | ✓ |
| Scalars only (defer strings) | Mirror terrain D-05: fixed-length scalar/enum/flag only; strings deferred. Editor mostly read-only in practice. | |

**User's choice:** Yes — strings + scalars (→ D-01)

### Q2 — Add / remove / reorder commands?

| Option | Description | Selected |
|--------|-------------|----------|
| Add + remove too | Full list authoring: edit fields + add + delete + reorder. | ✓ |
| Edit + add/remove, no reorder | Edit, add, delete; skip reorder (order carries no timing meaning). | |
| Edit-in-place only | Field edits on existing commands only; no structural changes. | |

**User's choice:** Add + remove too (full list authoring) (→ D-02)

### Q3 — CLEF version treatment on save?

| Option | Description | Selected |
|--------|-------------|----------|
| Preserve source version | Re-emit at the file's existing version; show only that version's fields; added commands at file's version. Never upgrade. | ✓ |
| Normalize to v0003 | Upgrade all CPAP to current write version; uniform UI but breaks byte-exact + may feed a newer file to an older client. | |

**User's choice:** Preserve source version (→ D-03)

---

## Editor form factor

### Q1 — Form factor?

| Option | Description | Selected |
|--------|-------------|----------|
| Thin EffectsSubPanel → Form | Docked EffectsSubPanel entry launches a roomy FormClientEffectEditor (Phase 21 terrain idiom). | ✓ |
| Standalone Form only | Clone FormParticleEditor directly via GetForms(); no docked entry. | |
| Docked SubPanel only | Whole editor in the docked panel; too narrow for a list+grid. | |

**User's choice:** Thin EffectsSubPanel → Form (→ D-04)

### Q2 — Generic Effects container now, or ClientEffect-scoped?

| Option | Description | Selected |
|--------|-------------|----------|
| ClientEffect-scoped now | Name it EffectsSubPanel (room to grow) but scope this phase to ClientEffect only; no speculative container. | ✓ |
| Generic Effects container | Build a real multi-format Effects home up front with Lightning/Swoosh extension points. | |

**User's choice:** ClientEffect-scoped now (→ D-05)

---

## Live preview

### Q1 — Include live preview, or static editor only?

| Option | Description | Selected |
|--------|-------------|----------|
| Include re-trigger preview | Match the Particle editor's "Preview in client" replay; game-thread, Game.IsRunning-gated, honest candor. Beyond written criteria but on-pattern. | ✓ |
| Static editor only | Decode/edit/save with no preview, exactly per criteria. | |
| You decide | Let the planner judge from re-trigger reachability. | |

**User's choice:** Include re-trigger preview (→ D-07, D-08)

### Q2 — When should the replay fire?

| Option | Description | Selected |
|--------|-------------|----------|
| Manual Preview button only | Replay only on click; avoids an effect firing at the player on every save. | ✓ |
| Manual + auto-on-save | Replay on button AND after each save (Phase 21 dual trigger); may be jarring for an effect. | |

**User's choice:** Manual Preview button only (→ D-07)

---

## Open/save workflow

### Q1 — Open entry points?

| Option | Description | Selected |
|--------|-------------|----------|
| TRE-read-only + loose open | Open read-only from TRE Browser → loose override, AND direct loose-override open (terrain D-08). | ✓ |
| Loose-override only | Only open files already in a loose override; no TRE Browser hand-off. | |

**User's choice:** TRE-read-only + loose open (→ D-09)

### Q2 — Handle the loose/ subdir consistency issue?

| Option | Description | Selected |
|--------|-------------|----------|
| ClientEffect correct; terrain stays own todo | Ensure EffectsSubPanel saves under <root>/loose/; leave terrain bug as its Phase 21 residual. | |
| Also fix terrain here | Fold phase21-terrain-override-loose-subdir.md; fix terrain's save path while in the shared plumbing. | ✓ |

**User's choice:** Also fix terrain here (→ D-10, Folded Todos)

---

## Claude's Discretion

- Internal codec class layout (`ClientEffect/` model + `Decoders/ClefDecoder.cs`).
- Exact `effect-*` verb names + `apply-save-effect` flag shape (D-11).
- JSON envelope shape for `decode-effect` / `decode-iff` CLEF output (D-11).
- Interior `EffectsSubPanel`/`FormClientEffectEditor` control choice (D-06).
- Whether `decode-effect` is standalone or a thin alias delegating to `DecodeIffCommand` (D-11).
- Live-replay effect-instantiation target/mechanism — reuse the Particle editor's existing path (D-08).

## Deferred Ideas

- Generic multi-format Effects container for Lightning/Swoosh — future milestone.
- Auto-replay on save — deliberately not built; possible future opt-in toggle.
- Resolving (vs preserving) appearance/sound/FF template references — SWG-side; possible future read-assist.
