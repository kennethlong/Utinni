---
phase: 22-clienteffect-editor
verified: 2026-06-30T00:00:00Z
status: passed
score: 2/2 must-haves verified
overrides_applied: 0
retroactive: true   # generated 2026-06-30 during the v2.1 milestone audit; reconstructed from SUMMARY + live-smoke records (no contemporaneous VERIFICATION.md was written at phase close 2026-06-19)
re_verification:
  previous_status: none
human_verification:
  - "22-04 maintainer live-SWG smoke PASSED 2026-06-19 (surfaced + fixed 4 form-internal bugs); editor-saved .cef round-trips byte-exact and reloads in-client"
---

# Phase 22: ClientEffect Editor — Verification Report (RETROACTIVE)

> **Retroactive note (2026-06-30):** Phase 22 shipped 2026-06-19 without a contemporaneous VERIFICATION.md.
> This report was reconstructed during the v2.1 milestone audit from the phase SUMMARY files (22-01..04)
> and the maintainer live-smoke record, to close the audit trail. Evidence is cited to those artifacts.

**Phase Goal:** A modder (and an AI agent) can open, edit, and byte-exactly save a ClientEffect `.iff`
command list across both lineages — cheap adjacent reuse of the shipped Particle editor pattern.
**Verified:** 2026-06-30 (retroactive) · **Status:** passed

## Goal Achievement

The goal decomposes into the two ROADMAP success criteria (= PROD-W2-CFX-01/02). Both are observably
delivered: a CLEF command-list codec (decode/edit/byte-exact encode) is exposed as golden-tested
`effect-*` CLI verbs + a thin MCP read tool, and an `EffectsSubPanel` + `FormClientEffectEditor` ship
inside The Jawa Toolbox (DEC-C4) with byte-exact loose-override save — maintainer-live-smoke PASSED
end-to-end. The "both lineages" real-asset matrix carries the standing format-reality caveat (high-era
Restoration `.cef` is v6000-encrypted → enumerate-only), unchanged from the plan allowance.

### Observable Truths

| # | Truth (Success Criterion) | Status | Evidence |
| --- | --- | --- | --- |
| 1 | Open a ClientEffect `.iff`/`.cef`, view/edit its command list, save byte-exact via loose-override; an EffectsSubPanel ships inside TJT (PROD-W2-CFX-01) | ✓ VERIFIED | 22-01 CLEF codec (`MutableClientEffect`/`ClientEffectDocument`/`ClefFieldCodec`/`ClefCommandDefaults`); 22-04 `EffectsSubPanel` → singleton `FormClientEffectEditor` (D-04 idiom), flat command list w/ StableId rows, version-aware typed field editor, raw/hex degrade, add/remove/reorder, byte-exact in-proc `ClientEffectSaveTargets` under `<root>/loose/`; TRE Browser "Open in Effects Editor" hand-off gated by `EffectHandoffPolicy`. Maintainer live-smoke PASSED (22-04, 2026-06-19). |
| 2 | CLEF decode/edit/save exposed as golden-tested `utinni-cli` verbs + MCP read tool, across both lineages (PROD-W2-CFX-02) | ✓ VERIFIED (caveat) | 22-02 `decode-effect`/`roundtrip-effect`/`apply-save-effect` verbs + `summarize_clienteffect` MCP tool (thin shell, zero format logic); codec + handoff-policy tests green. **Caveat:** high-era Restoration `.cef` is v6000-encrypted → enumerate-only, so the real-asset "both lineages" matrix is caveated (accepted format reality, not a defect). |

**Score:** 2/2 truths verified

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
| --- | --- | --- | --- | --- |
| PROD-W2-CFX-01 | 22-01, 22-04 | In-app ClientEffect editor: view/edit/save byte-exact | ✓ SATISFIED | Truth 1; live-smoke PASS |
| PROD-W2-CFX-02 | 22-02 | `effect-*` verbs + MCP read tool, both lineages | ✓ SATISFIED (caveat) | Truth 2; Restoration-encrypted enumerate-only |

No orphaned requirements — both IDs (REQUIREMENTS.md, mapped to Phase 22) are claimed by plans and verified.

### Locked-Invariant Compliance

- **Byte-exact round-trip (codec gate):** ✓ CLEF codec byte-exact; in-proc↔CLI save-parity test; live-smoke byte-exact `.cef` round-trip.
- **DEC-V2-VERBS-FIRST:** ✓ engine lands as `effect-*` verbs before MCP/UI consume it.
- **DEC-V2-MCP-OOP:** ✓ `summarize_clienteffect` shells `decode-iff` (CLEF auto-dispatch); zero format logic in the MCP process.
- **DEC-C4:** ✓ ships as a TJT SubPanel (EffectsSubPanel + FormClientEffectEditor), not a separate plugin.

### Deferred / Out-of-Scope (not gaps)

| Item | Disposition |
| --- | --- |
| In-client "Preview" is honest-candor only (no live retrigger this build) | D-07 accepted at close. **Superseded post-v2.1:** the advertised-client editor-unlock follow-on later delivered `.cef` live re-play (Bucket B-2, 2026-06-28). |
| `FormClientEffectEditor` regression tests not CI-gated | The form lives in UtinniPlugins (no test project/CI); the CI-reachable seam (`EffectHandoffPolicy`) IS unit-tested. |
| PROD-W2-CFX-02 "both lineages" real-asset coverage | Restoration v6000 `.cef` encrypted → enumerate-only (accepted format reality). |
| Stale root `bin/Release/utinni-cli.exe` | Output-staging artifact; the `Utinni.Cli/bin/.../utinni-cli.exe` has the verbs. Not a phase defect. |

### Gaps Summary

No gaps. Both success criteria are observably satisfied: the CLEF codec is byte-exact and headless; the
`effect-*` verbs + MCP tool are wired; the EffectsSubPanel/FormClientEffectEditor carry the full
open/edit/save surface; the maintainer live-smoke passed end-to-end (four form bugs found + fixed). The
only caveat (Restoration-encrypted real assets) is accepted format reality, not a delivery gap.

---

_Verified: 2026-06-30 (retroactive, v2.1 milestone audit)_
_Verifier: Claude — reconstructed from 22-01..04 SUMMARY + live-smoke records_
