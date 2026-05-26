# Phase 7: TJT subpanel — TRE Browser (read-only) - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-26
**Phase:** 7-tjt-subpanel-tre-browser-read-only
**Areas discussed:** Maya-plugin parity / scope, Render surface, Data source & completeness, Parsing baseline (TRE format coverage), Tree organization, File detail view

---

## Reference redirect (pre-discussion)

User redirected the "parsing baseline" framing twice:
1. Pointed to `D:/Code/swg-client-v2/tools/swg_blender` (a Python IFF/TRE toolkit Cursor is building).
2. Clarified: **not** a Python dependency — use the `swg-client-v2/docs/research` docs on what the Maya plugin did, and the Maya plugin source, as a **reference implementation**, aiming for **parity with what the Maya plugin used to offer**. Later added the Python code is also usable as a reference implementation.

Investigation found the MayaExporter is a **write-only art-asset exporter** (mesh/skeleton/animation/shader/collision/building) — DCC authoring — which collides with locked anti-goal DEC-A3 and is a different axis from the roadmapped Wave-1 data editors. Surfaced this three-way tension before continuing.

---

## Maya-plugin parity / scope

| Option | Description | Selected |
|--------|-------------|----------|
| Format-aware browse | Use the research as read/inspect spec; no authoring; stays in DEC-A3 + Phase 7 scope | |
| Re-open DEC-A3 (new vision) | Treat Maya export parity as a new capability axis; milestone/vision decision | |
| Read-only art viewing | Structured viewers, no export | |

**User's choice:** "Maya parity is eventual goal, we should plan that for later. For now read only parity and we will plan write functionality later." (≈ Format-aware browse now + Re-open-DEC-A3 deferred to a later milestone.)
**Notes:** Python code is a reference implementation, not a runtime dependency.

---

## Render surface

| Option | Description | Selected |
|--------|-------------|----------|
| WinForms SubPanel | IEditorPlugin SubPanel on the FormObjectBrowser TreeView pattern; matches DEC-C4 + roadmap | |
| ImGui HUD overlay | Chromeless overlay per the 06-01 directive | |
| WinForms now, HUD later | Ship WinForms; revisit ImGui HUD as optional later polish | ✓ |

**User's choice:** WinForms now, HUD later.
**Notes:** Makes Phase 7 the explicit exception to the 06-01 HUD-style overlay directive.

---

## Data source & completeness

| Option | Description | Selected |
|--------|-------------|----------|
| Full .tre TOC enumeration | Read every archive's TOC for the complete filename graph | |
| Live Repository (harvested) | Only files the running client has touched; zero parsing | |
| Hybrid (TOC tree + live overlay) | Full TOC tree + live Repository overlay for loaded/resolvable status | ✓ |

**User's choice:** Hybrid (TOC tree + live overlay).
**Notes:** Satisfies both PROD-01's "powered by getAllFilenames hook" and "covers every asset class" / the parse-tre golden tie-in.

---

## Parsing baseline (TRE format coverage)

| Option | Description | Selected |
|--------|-------------|----------|
| SWGEmu Pre-CU (0005/0006) | Only the injected client's format | |
| Both Pre-CU + Restoration v6000 | 0005/0006 AND v6000/COT2000 | ✓ |
| Restoration v6000 primary | Only the swg-client-v2 format | |

**User's choice:** Both Pre-CU + Restoration v6000.
**Notes:** Browser is client-agnostic. v6000 payloads encrypted → enumerate-only (content extract needs TreeFileExtractor). IFF reader is version-agnostic; reference split for per-type decoders (swg_blender for mesh/skeleton/anim/shader; C++ loaders for datatable/STF/object-template).

---

## File detail view

| Option | Description | Selected |
|--------|-------------|----------|
| IFF tree + type header | Metadata + universal IFF chunk tree + one-line type/version banner; deep decode deferred to Phases 9-11 | |
| Deep per-type decode now | Also datatable rows/cols, mesh/skeleton stats, STF entries, object-template fields in Phase 7 | ✓ |
| Metadata only | Path/size/archive/CRC only | |

**User's choice:** Deep per-type decode now.
**Notes:** Scope-expanding, consciously accepted. These read-only decoders become the foundation Phases 8-11 make editable (no rework). Planner will likely split Phase 7 into multiple plans.

---

## Claude's Discretion

- Code placement (read parsers/decoders extended framework-side in `UtinniCoreDotNet/Formats/`, shared with the CLI; IFF write primitives → TJT in Phase 8).
- Plan splitting of the (large) Phase 7.
- Search/filter UX, column set, theming, SubPanel/StandalonePanel/Form placement within TJT.

## Deferred Ideas

- Maya-exporter WRITE/authoring parity → later milestone (re-open DEC-A3; parity checklist is the backlog).
- ImGui HUD-overlay presentation → optional later polish.
- v6000 payload extraction/decrypt → blocked without TreeFileExtractor; enumerate-only in V1.
- Editable surfaces for decoded types → Phases 8-11.
- Reviewed-not-folded todos: `gamecallbacks-gc-av-flake-fix.md`, `loader-lock-harness-flake-fix.md` (already resolved in 06-04; keyword false-positives).
