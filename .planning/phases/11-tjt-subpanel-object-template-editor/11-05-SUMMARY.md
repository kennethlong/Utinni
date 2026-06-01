---
phase: 11-tjt-subpanel-object-template-editor
plan: 05
title: Live-SWG smoke + V1 release gate + tag
status: complete
type: checkpoint
completed: 2026-05-31
requirements: [PROD-W1-OT, PROD-02]
---

# Plan 11-05 Summary — V1 Release Gate + Tag

Closes Phase 11 **and the V1 milestone.** Three tasks: automated regression + the SC4 evidence
doc (auto), the maintainer-in-the-loop live-SWG smoke (human-verify), and the V1 tag (human-action).

## What shipped

- **Task 1 (auto):** Full automated suite green both configs + `11-V1-RELEASE-GATE.md` (the SC4
  aggregate evidence: 5 subpanels, Tier 1+2 CI green on main, 15 critical bugs closed, honest SC3
  residual) + appended the OT SC3 live-reload residual to `docs/ai/test-harness-plan.md`. Commit `4d3eea8`.
- **Task 2 (live-SWG smoke — APPROVED 2026-05-31):** Maintainer ran the Object Template Editor against
  a live SWG client — "approved, all five subpanels demo and edits work." SC1 (loads in TJT), SC2 (view
  inherited fields + edit/override/revert + save) demonstrated; SC4 five-subpanel same-session demo
  confirmed; SC3 per the honest CF-05 tier-(b) relog residual.
- **Task 3 (V1 tag — human-action):** Annotated **`v1.0.0`** tagged on the CI-green commit `d68387f` and
  pushed (triggers `release.yml`). Gate doc sign-off + tag lines recorded.

## En-route fixes (surfaced + resolved during the live smoke, re-verified before sign-off)

The real-file smoke exposed defects synthetic tests missed:

| Fix | Commit | What |
|-----|--------|------|
| OT editor edit crash | UtinniPlugins `0b26bb5` | `BindEffectiveView` re-entrancy loop (programmatic cell-set → commit → `AddOverride` "already exists"); `suppressCommit` guard |
| Multi-chunk param parse | Utinni `d68387f` | 2,756/15,853 templates (17% — draft schematics `SDSC`, hair `STOT`) aborted on nameless list-element chunks; now degrade to raw hex-fallback. **15,850/15,850 parse + round-trip byte-exact** + CI-safe regression test |
| Dead Find/Replace pane | UtinniPlugins `9966304` | Removed the never-wired Find/Replace clone artifact (single-template grid doesn't need it) |
| In-game Enter pre-world AV | Utinni `2300e44` | Gate `hkChatEnter` override on an active ground scene (latent Phase H edge case) |
| Crash-address logger | Utinni `d1096ac` | Extended the VEH to log fatal-class exceptions with module+RVA (the dump-less intro-skip scene-transition crash) — diagnostic, pending repro |

## Verification (actually performed)

- **CI green on the tagged commit:** run `26730021837`, HEAD `d68387f` — `conclusion: success` (clang-format
  → Build Release|x86 → Tier 1 C# → Tier 2 CLI golden → Tier 1 native Catch2 → native triple-config build).
- `UtinniCoreDotNet.Tests` **625/625** (Debug+Release|x86); `Utinni.Cli.Tests` 165 pass / 2 env-skip;
  `roundtrip-ot` goldens 9/9.
- Object-template parse + round-trip verified against the **entire live SWGEmu corpus** (15,850/15,850
  byte-exact, 0 throws; 3 non-templates correctly sniff-rejected).
- Live five-subpanel demo + OT editor edit flow maintainer-approved.

## Open / tracked (not V1 blockers)

- **OT multi-chunk typed display (Tier-2):** list/struct params (`StructParamOT`, `@reference`, dynamic
  vars) show as raw rows pending a full port of SWG's per-class template-definition param map. Memory:
  `project-ot-multichunk-list-params`.
- **Intro-skip scene-transition crash:** VEH crash-logger deployed; will capture the faulting address/module
  on next repro.
- **SC3 live re-resolution residual:** relog-reliable / scene-change-conditional / respawn-best-effort
  (CF-05 cache reality) — mirrors Phase 10's deferred SC3 residual.

## Outcome

**PROD-W1-OT closed; PROD-02 (Wave-1 aggregate) closed. V1 shipped — `v1.0.0` tagged on `d68387f`.**
