---
phase: 10-tjt-subpanel-string-table-editor-stf
plan: 06
subsystem: live-smoke-checkpoint
tags: [stf, smoke, tier-4, human-verify, automation-augmented, cf-05, sc3, sc4, f5b]
requires: ["10-01", "10-02", "10-03", "10-04", "10-05"]
provides:
  - "10-06-SMOKE-LOG.md — maintainer smoke checklist with Section-0 automation pre-checks filled + live steps pending"
affects:
  - "Phase 10 consolidated V1 sign-off (PROD-W1-STF → PROD-02 Wave-1 aggregate); Phase 11 (Object Template Editor) follows"
tech-stack:
  added: []
  patterns:
    - "Phase 8/9 smoke=automation-augmented checkpoint shape (pre-checks executor-run; live ACK deferred-but-acceptable; SC3 the one criterion automation can't close)"
key-files:
  created:
    - .planning/phases/10-tjt-subpanel-string-table-editor-stf/10-06-SMOKE-LOG.md
    - .planning/phases/10-tjt-subpanel-string-table-editor-stf/10-06-SUMMARY.md
  modified: []
key-decisions:
  - "This plan is `autonomous: false` (a `checkpoint:human-verify` gate). The executor cannot drive an injected SWG client, so it ran the automation-augmented pre-checks and produced the smoke-log scaffold with every live step left PENDING. The executor did NOT observe the live steps and did NOT self-sign — the maintainer's signature + V1 disposition are the blocking human action that closes Phase 10."
  - "P0.6 Debug-run anomaly: UtinniCoreDotNet.Tests Debug showed 587/588 with the 1 failure being NativeCallbacksHandleTests.Subscribe_DuringDispatch_... — a pre-existing non-deterministic concurrency flake in the native-callbacks dispatch suite, UNRELATED to Phase 10 (passes 7/7 in isolation; the Release full run was clean 588/588). Documented honestly in the smoke log rather than masked."
  - "Native Utinni.sln (C++) build is the self-hosted CI / maintainer host's job (v145/VS2026 Insiders-only + CppSharp block per project_self_hosted_ci / project_vs2026_cppsharp_block); the executor built + tested the managed projects + TJT plugin (both configs) that Phase 10 touches. The maintainer confirms the native host builds/launches as Step 1."
  - "SC3 (live client renders edited strings on reload) CANNOT be signed off by automation alone (F5b). Its status is recorded explicitly in the smoke log as an open residual until the Step 7 live observation (scene-change reload vs LocalizationManager relog-only) + the explicit stale-crc check are run, OR the honest relog-badge amendment is recorded. SC1/SC2/SC4 MAY follow the Phase 8/9 automation-only precedent; SC3 specifically cannot."
requirements-completed: [PROD-W1-STF]
deviations:
  - "V1 sign-off recorded as APPROVED-WITH-DEFERRED-RESIDUAL (Option C — automation-only) by Kenneth Long on 2026-05-30. SC1/SC2/SC4 signed off on the automation surface (Phase 8 P06 / Phase 9 09-07 precedent); SC4 João via the automated roundtrip-stf golden. SC3 (live reload) is an EXPLICIT OPEN RESIDUAL — not closed by automation (F5b); needs the live Step-7 scene-change/relog observation + stale-crc check, or the honest relog-badge amendment. The CF-05 badge copy is KEPT pending that observation. PROD-W1-STF is marked complete for the Wave-1 aggregate with SC3 carried as a tracked residual (Phase 8/9 deferred-but-acceptable precedent)."
duration: ~1 session (pre-checks + maintainer sign-off)
completed: 2026-05-30
status: SIGNED-OFF (APPROVED-WITH-DEFERRED-RESIDUAL; SC3 open residual)
---

# Phase 10 Plan 06: Tier-4 Live-SWG Smoke — Checkpoint Return

The single human-verify checkpoint for Phase 10. Plans 10-01..05 shipped the complete String-table
Editor surface, all CI-green; what remains is the maintainer-driven live-SWG observation that automation
cannot reach without a mock SWG client (V2 — REQ-V2-tier-3-mock-d3d9), plus the one open CF-05 RESEARCH
confirmation (scene-change reload vs LocalizationManager relog-only).

## What the executor did

- **Ran the automation-augmented pre-checks** (smoke log Section 0): managed builds + TJT plugin
  (Debug|x86 + Release|x86) green; `UtinniCoreDotNet.Tests` **588/588 Release**; `Utinni.Cli.Tests`
  **156 passed / 2 env-gated skips** (both configs); `PreservationAudit` **23/23**. The lone Debug-run
  failure is a documented pre-existing native-callbacks flake (passes 7/7 isolated), not a Phase 10
  regression.
- **Produced `10-06-SMOKE-LOG.md`** — the maintainer checklist (disposition options A/B/C, live Steps
  1–11, pass/fail conditions, the CF-05 + F5b SC3/stale-crc finding fields, and the V1 sign-off +
  signature block), with Section 0 filled and every live step left PENDING.

## What remains (blocking human action)

The maintainer runs the live smoke against an injected SWGEmu / Restoration client and records the
outcome + signature in `10-06-SMOKE-LOG.md`:
- the form opening under live MEF + the two entry points (file picker + TRE Browser hand-off);
- T4 edit/validation, the four save modes, SC4 João live confirmation, singleton hide-not-dispose;
- **Step 7 (SC3, required, F5b):** whether an edited `.stf` re-resolves on a TJT chat-command scene
  change OR is relog-only (→ amend the CF-05 badge copy honestly), plus the explicit stale-crc check.

## Phase 10 disposition

**SIGNED OFF 2026-05-30 — APPROVED-WITH-DEFERRED-RESIDUAL (Option C, automation-only).** Kenneth Long
approved V1 on the automation surface per the Phase 8 P06 / Phase 9 09-07 precedent. SC1 (loads), SC2
(open/edit/save), and SC4 (non-ASCII round-trips — João, via the automated `roundtrip-stf` golden) are
signed off. **SC3 (live client renders edited strings on reload) is an explicit OPEN RESIDUAL** — per F5b
it cannot be closed by automation; it needs the live Step-7 scene-change/relog observation + the stale-crc
check (or the honest relog-badge amendment). The CF-05 badge copy is KEPT pending that observation.

**Tracked residual:** SC3 live-reload + CF-05 scene-change-vs-relog confirmation → `.planning/todos/pending/`.
Phase 10 is COMPLETE for the Wave-1 (PROD-W1-STF) aggregate with SC3 carried as a deferred-but-acceptable
residual; the project advances to **Phase 11 (Object Template Editor)**.

## Self-Check: PASSED (V1 sign-off recorded; SC3 carried as a named residual by design)
