# Phase 13: Wrap revived compilers as CLI verbs + close OT Tier-2 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-03
**Phase:** 13-Wrap revived compilers as CLI verbs + close OT Tier-2
**Areas discussed:** AUTH-06 scope, Golden-test meaning, OT Tier-2 depth (RESID-01), SAVE verb coverage (+ user-raised: pre-CU TRE corpus, .rsp synthesis)

---

## AUTH-06 scope (datatable compile + item exporters)

| Option | Description | Selected |
|--------|-------------|----------|
| Managed datatable + defer exporters | Wrap managed DataTableWriter for compile-datatable; research scopes exporter lift, defer if costly | |
| Full AUTH-06 — lift both exporters | Managed datatable + lift ArmorExporterTool & CoreWeaponExporterTool natives | |
| Purist — lift all 3 natives | Lift DataTableTool + both exporters as natives (byte-exact, strict coexistence) | ✓ |

**User's choice:** Purist — lift all 3 natives.
**Notes:** Strong commitment to SOE byte-exact fidelity + strict BUILD-is-native ownership. Implies a mini-Phase-12 lift on top of verb-wrapping.

### AUTH-06 follow-up — lift risk posture

| Option | Description | Selected |
|--------|-------------|----------|
| Lift-with-escape-hatch | Commit to all 3; if an exporter pulls server-side libs (multi-day lift), THAT tool falls back (managed/defer) rather than blocking the phase; research scopes closures first | ✓ |
| Hard commit — lift no matter the cost | All 3 lifted regardless of closure depth | |
| Use Phase-12 v143 fallback first | Reach for v143 build before managed/defer | |

**User's choice:** Lift-with-escape-hatch.
**Notes:** Ambition tempered by a no-schedule-blow-up guard. Per-exporter dependency-closure scoping is the gating research step.

---

## Golden-test meaning (BUILD-verb correctness gate)

| Option | Description | Selected |
|--------|-------------|----------|
| Native-vs-managed cross-check + regression goldens | Managed reader/writer as independent oracle; commit native outputs as regression goldens; self-consistency round-trip for build-tre | ✓ |
| Self-consistency round-trip only | Compile → decode → assert structure; no independent oracle | |
| Hand-authored source+expected fixtures | Lock tool's own output as golden; risks baking in bugs | |

**User's choice:** Native-vs-managed cross-check + regression goldens.
**Notes:** Then user revealed the live SWGEmu client holds real pre-CU 0005/0006 .tre files → upgrades the oracle's inputs to real SOE data. Distinct from Phase-12's v6000 gate-finding corpus. Captured as D-05.

---

## OT Tier-2 depth (RESID-01) — display depth

| Option | Description | Selected |
|--------|-------------|----------|
| Structured for common, labeled-hex for rare | Real structured editors for slots/attributes/hair; exotic tail → typed label + hex | ✓ |
| Full structured typing for everything | Every schema-described param structured-typed, incl. exotic tail | |
| Labels only (read-typed) | Schema labels types; complex values stay raw hex | |

**User's choice:** Structured for common, labeled-hex for rare.
**Notes:** 80/20 close; scalars already typed in Phase 11, the residual is the ~17% multi-chunk params.

### OT Tier-2 — schema delivery

| Option | Description | Selected |
|--------|-------------|----------|
| Static generated artifact | compile-definition emits per-class JSON schema, committed; editor loads it; same artifact feeds Area-2 tests | ✓ |
| Live per-class lookup | Editor invokes schema verb per template open | |
| You decide | — | |

**User's choice:** Static generated artifact.
**Notes:** One schema, two consumers (editor + cross-check tests); decouples editor from native tool at runtime.

---

## SAVE verb coverage + repack posture

| Option | Description | Selected |
|--------|-------------|----------|
| All 4 formats; repack a SEPARATE verb | save = IFF/datatable/stf/OT loose-override default; repack its own explicit verb | ✓ |
| All 4 formats; repack via save --mode repack | Repack reachable through save with flag | |
| Subset first (IFF + datatable) | Smaller surface now | |

**User's choice:** All 4 formats; repack a SEPARATE verb.
**Notes:** Safe-by-construction default write surface; Phase-14 MCP gates repack as its own dry_run tool. Cheap since all 4 managed save targets already exist.

---

## User-raised research threads (not multiple-choice — captured as CONTEXT seeds)

- **Pre-CU TRE corpus:** the live SWGEmu client (`D:\SWGEmu-Client\SWGEmu\`) ships 53 real 2003/2006 0005/0006 `.tre` archives → real SOE reference data for the cross-check oracle and the RESID-01 typed-decode validation set. (CONTEXT D-05.)
- **`.rsp` synthesis:** `TreeFileBuilder` needs an `.rsp` (`diskPath@treePath` recipe). No authoritative SOE `.rsp` is findable (server-side build artifact), but we can synthesize one from a real `.tre` via the Phase-7 reader (paths + order + per-file compression) → rebuild → byte-compare, with zlib 1.1.4 determinism-matched. Reframes Phase-12's "byte-exact blocked by missing .rsp" finding. (CONTEXT D-06.)

## Claude's Discretion
- Exact verb names within the `compile-*`/`build-*` convention.
- CSV-vs-XML datatable/exporter input format (resolve against what the lifted natives accept).
- Subprocess invocation mechanics for wrapping the native exes.

## Deferred Ideas
None new — discussion stayed in scope. RESID-04 (window edge-cases) already routed to Phase 15. Five weak todo matches reviewed, none folded (see CONTEXT `<deferred>`).
