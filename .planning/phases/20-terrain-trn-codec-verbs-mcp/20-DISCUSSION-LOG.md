# Phase 20: Terrain `.trn` Codec + Verbs + MCP - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-15
**Phase:** 20-terrain-trn-codec-verbs-mcp
**Areas discussed:** Typed-tag coverage, Edit scope, Fixture sourcing, Verb surface shape

---

## Typed-tag coverage

**Q1 — v1 typed-decode breadth:**

| Option | Description | Selected |
|--------|-------------|----------|
| Research Tier-1 set | Typed: AHCN/AHTR height, ACCN/ACRH color, ASCN/ASRP shader, AFCN/AFSC/AFSN flora; BCIR/BREC boundaries; FHGT/FSLP filters. Rest raw-fallback. | ✓ |
| Trim to a minimal core | Just AHCN/ASCN/BCIR/BREC/FHGT + active flags. | |
| Extend beyond Tier-1 | Add fractal-referencing AHFR/ACRF/FFRA as typed too. | |

**User's choice:** Research Tier-1 set.

**Q2 — unknown FORM version behavior:**

| Option | Description | Selected |
|--------|-------------|----------|
| Raw-fallback the whole chunk | Unknown version → emit raw {tag, version, hex}; stays byte-exact, no over-read (Pitfall 5). | ✓ |
| Best-effort partial decode | Decode shared fields, raw-tail the rest. Riskier (offset mis-read). | |

**User's choice:** Raw-fallback the whole chunk.

**Notes:** User accepted the research tag taxonomy as-is — no tags moved between tiers.
DEAD-tag recognize-and-skip + positional palette decode are research-locked, captured as D-03/D-04.

---

## Edit scope

**Q1 — apply-save-trn v1 operations:**

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed-length only | Scalar/enum values + active-flag toggle. No length ripple, byte-exact trivial. Matches criterion 3. | ✓ |
| Include name edits | Also variable-length name edits; needs length-ripple proof (Open Q3). | |
| Fixed-length now, name-edit if cheap | Build fixed; add name-edit only if a round-trip fixture proves it. | |

**User's choice:** Fixed-length only. (Name edits deferred to a future phase.)

**Q2 — save-path constraint:**

| Option | Description | Selected |
|--------|-------------|----------|
| Same --root containment as apply-save-iff | Reuse fail-closed --root + atomic write. | ✓ |
| Discuss a different scheme | — | |

**User's choice:** Same --root containment as apply-save-iff.

---

## Fixture sourcing

**Q1 — both-lineage fixture matrix source:**

| Option | Description | Selected |
|--------|-------------|----------|
| Synthesize hand-rolled fixtures | ≤200-byte TGEN goldens, low+high version per Tier-1 tag, no retail assets. | (part) |
| Also pin against a real .trn pair | Drop one real .trn per client to pin exact versions, still synthesize goldens. | (part) |
| Real .trn pair only | Use real assets directly. Not recommended. | |

**User's choice (free text):** "You have access to all those versions and access to the TRE
extractor, so you could source those, I think we do 1 and you source a couple of examples."
→ Hybrid: synthesized goldens are the committed corpus (option 1) PLUS source a couple of real
examples to pin versions.

**Q2 — extraction path:**

| Option | Description | Selected |
|--------|-------------|----------|
| utinni-cli TRE verbs | Dogfood Utinni's revived TRE extract verbs against client archives. | ✓ |
| You'll provide the files directly | User drops extracted .trn into a fixtures-input folder. | |
| Decide during planning | Leave mechanism to planner/Wave-0. | |

**User's choice:** utinni-cli TRE verbs (dogfood).

**Q3 — how the real pair is used:**

| Option | Description | Selected |
|--------|-------------|----------|
| Pin versions + roundtrip-only | Decode to record exact versions + run roundtrip-trn; keep OUT of committed goldens. | ✓ |
| Commit a small real one as a golden | If small + unencrypted, commit it. | |

**User's choice:** Pin versions + roundtrip-only (real assets stay out of the committed golden corpus).

---

## Verb surface shape

**Q1 — verb/read surface:**

| Option | Description | Selected |
|--------|-------------|----------|
| decode-iff branch + 3 trn verbs | TGEN branch in decode-iff (free MCP) + decode-trn + roundtrip-trn + apply-save-trn + summarize_terrain. | ✓ |
| Minimal: decode-iff branch + 2 verbs | Skip the standalone decode-trn alias. | |
| Standalone decode-trn primary | Don't branch decode-iff. Not recommended (loses free MCP routing). | |

**User's choice:** decode-iff branch + 3 trn verbs.

**Q2 — apply-save-trn granularity:**

| Option | Description | Selected |
|--------|-------------|----------|
| Field-aware (--field/--value) | Read tag's DATA layout, replace ONE field, re-emit payload (Pitfall 1). | ✓ |
| Reuse whole-leaf apply-save-iff | Caller re-encodes the packed payload by hand. Not recommended. | |

**User's choice:** Field-aware (--field/--value).

---

## Claude's Discretion

- Internal decoder class layout under `Formats/` (precedent: `Decoders/TgenDecoder.cs` + a
  `Terrain/` model subdir mirroring `Particle/`).
- Shared-codec-library vs. verb-command split.
- JSON envelope shape for decode output (follow existing decoder conventions).
- Whether `decode-trn` is a separate command or folded into `DecodeIffCommand` (the decode-iff
  TGEN branch is required either way).

## Deferred Ideas

- Variable-length name edits (needs length-ripple proof) — future terrain-edit phase.
- 2D sampled-map preview (Sampler* port) — v2.1.x.
- Structural authoring / boundary painting — own milestone.
- Long-tail / fractal-referencing affector typed coverage beyond raw-fallback — Tier-2 follow-up.
- Live in-client regen-on-save + TJT SubPanel — Phase 21 (PROD-W2-TRN-05).
- Reviewed-not-folded todos (weak keyword matches, off-domain): phase09-datatable-editor-review-warnings,
  phase10-stringtable-sc3-live-reload-residual, swg-window-resize-fullscreen-edge-cases.
