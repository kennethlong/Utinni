# Phase 8: TJT subpanel — IFF Editor (read + write) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-27
**Phase:** 8-tjt-subpanel-iff-editor-read-write
**Areas discussed:** Write-primitive placement, Editing surface & scope, Save target & live reload, Edit model & round-trip

---

## Write-primitive placement (→ D-01)

| Option | Description | Selected |
|--------|-------------|----------|
| Framework-side, next to reader | IffWriter + mutable model in UtinniCoreDotNet/Formats/Iff; one code path; CLI gains write; Phases 9-11 consume via existing UtinniCoreDotNet.dll ref; amend Criterion 5 wording | ✓ |
| TJT-side per Criterion 5 | Write primitives in TheJawaToolboxDotNet; reader stays framework-side; honors literal Criterion 5 but splits read/write across assemblies; CLI can't write | |

**User's choice:** Framework-side, next to reader.
**Notes:** Resolves the documented tension between Phase-7 reader placement (D-08) and Criterion 5's literal "exported from TheJawaToolboxDotNet." Flagged: Criterion 5 wording to be reconciled to "shared non-plugin assembly."

## CLI round-trip harness (→ D-02)

| Option | Description | Selected |
|--------|-------------|----------|
| Add round-trip CLI verb + golden fixtures | parse → mutate → serialize → re-parse; byte-exact assert for untouched chunks; automated gate for Criterion 4 | ✓ |
| Framework xUnit round-trip tests only | Test round-trip in UtinniCoreDotNet.Tests; no CLI verb; not exposed via golden-fixture CLI harness | |

**User's choice:** Add round-trip CLI verb + golden fixtures.
**Notes:** Aligns with the standing max-harness-over-manual-smoke preference.

## Editing surface & scope (→ D-03)

| Option | Description | Selected |
|--------|-------------|----------|
| Chunk-level only: payload bytes + tree structure | Edit leaf payloads + add/remove/rename/reorder/duplicate; no typed parsing; clean 8↔9/11 boundary | ✓ |
| Chunk-level + generic typed-value helpers | Above + interpret leaf as int32/float/string primitives; overlaps 9-11; needs layout guesser | |
| Defer structural ops; payload-only for V1 | Edit existing leaf bytes only; smallest surface; may under-deliver | |

**User's choice:** Chunk-level only: payload bytes + tree structure.

## Payload editing modality (→ D-04)

| Option | Description | Selected |
|--------|-------------|----------|
| Editable hex view | Make Phase 7's read-only hex pane editable in place | ✓ |
| Replace payload from file / export to file | Right-click leaf → replace/export bytes | ✓ |
| Inline text edit for ASCII-ish payloads | Plain text box when payload is printable text | ✓ |

**User's choice:** All three (multi-select).

## Save target (→ D-05)

| Option | Description | Selected |
|--------|-------------|----------|
| Loose override file in client load path | Standard SWG modder iterate loop | ✓ |
| Save / Save-As to arbitrary path | Always-available baseline | ✓ |
| In-memory live patch of loaded IFF | CON-N-04 VirtualProtect bracket; instant but volatile; mapped-memory risk | ✓ |
| Repack into the source .tre | Full repack + CRC/TOC rebuild; high risk | ✓ |

**User's choice:** All four (multi-select).

## Save tiering (→ D-05) — probed because all four selected

| Option | Description | Selected |
|--------|-------------|----------|
| Core (loose+Save-As) must-have; live-patch + .tre repack stretch | Risky modes planned but non-blocking | |
| All four are hard V1 must-haves | All gate verification; large/risky; may warrant 8a/8b split | ✓ |
| Core only; defer both risky modes entirely | Smallest safest foundation | |

**User's choice:** All four are hard V1 must-haves.
**Notes:** Claude flagged the size/risk; planner should split into multiple plans (likely isolated plans for .tre repack and the mapped-memory live patch).

## Live reload (→ D-06)

| Option | Description | Selected |
|--------|-------------|----------|
| Rely on natural reload (scene change / re-enter) | Client picks up file on next natural load; no new hook | |
| Editor forces an in-session reload | Explicit reload action; needs a client reload/cache-invalidation mechanism | ✓ |
| In-memory live patch IS the instant-feedback path | No forced file reload; live patch covers instant feedback | |

**User's choice:** Editor forces an in-session reload.
**Notes:** Flagged as a research item — investigate whether a client reload/cache-invalidation hook exists; if not, designing one (or a scene-change-style fallback) is its own risk item.

## Edit model & round-trip (→ D-07)

| Option | Description | Selected |
|--------|-------------|----------|
| Hybrid mutable DOM, untouched nodes re-emit original bytes | Byte-exact untouched chunks + no-pad preserved for free; lengths roll up; handles structural ops | ✓ |
| Pure semantic re-serialize | Write everything from scratch; must perfectly reproduce SWG byte conventions; higher risk | |
| Byte-patch in place | Great for same-length edits; can't express structural ops; conflicts with D-03 | |

**User's choice:** Hybrid mutable DOM.

## Undo/redo (→ D-08)

| Option | Description | Selected |
|--------|-------------|----------|
| Editor-local undo/redo stack, independent of scene undo | Own history; avoids CON-M-05 entanglement | ✓ |
| Integrate with Utinni's UndoRedoManager | Unified undo; couples IFF edits to scene lifecycle; must honor CON-M-05 | |
| Dirty-flag + save/discard only | Simplest; no granular undo; weaker UX | |

**User's choice:** Editor-local undo/redo stack, independent of scene undo.

## Panel architecture (→ D-09)

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated IFF Editor subpanel reusing extracted chunk-tree control | Extract Phase 7's tree to shared control; TRE Browser stays read-only; Open-in-editor hand-off + file picker | ✓ |
| Make TreDetailPane editable in place | One control toggles browse/edit; mixes concerns; complicates Phase 7 panel | |
| Standalone editor Form/window | Own top-level Form; more space; diverges from DEC-C4 subpanel pattern | |

**User's choice:** Dedicated IFF Editor subpanel reusing an extracted shared chunk-tree control.

---

## Claude's Discretion
- Exact editor SubPanel UI layout / affordances → /gsd-ui-phase 8 + planner.
- ASCII-ish detection heuristic for inline text edit (D-04.3).
- Validation-before-save, loose-override conflict handling, large-file perf guards, "new IFF from scratch" — lightweight at planner discretion or follow-up.
- CLI round-trip verb naming/shape (D-02).
- Plan decomposition (multi-plan expected per D-05 split flag).

## Deferred Ideas
- Format-specific typed editing → Phases 9 (datatable), 10 (STF), 11 (object template).
- Art-asset write/authoring parity → post-V1 milestone gated behind LOCKED DEC-A3.
- Validation/integrity warnings, override conflict handling, perf guards, new-IFF-from-scratch — not locked as V1 requirements.
- ImGui chromeless HUD-overlay presentation of the editor — optional later polish.
- Reviewed-not-folded todos: `gamecallbacks-gc-av-flake-fix.md`, `loader-lock-harness-flake-fix.md` (CI flakes resolved in 06-04; keyword false positives).
