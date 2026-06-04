---
phase: 13
slug: wrap-revived-compilers-as-cli-verbs-close-ot-tier-2
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-03
---

# Phase 13 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (managed: Utinni.Cli.Tests + UtinniCoreDotNet.Tests, net472) + Catch2 (native UtinniCore.Tests) + golden-fixture harness |
| **Config file** | none — existing test projects (Utinni.Cli.Tests, UtinniCoreDotNet.Tests) |
| **Quick run command** | `dotnet test Utinni.Cli.Tests --no-build` |
| **Full suite command** | VS2026 MSBuild (Debug\|x86) then `dotnet test --no-build` (per feedback_dotnet_build_msbuild_resources) |
| **Estimated runtime** | ~{N} seconds (to be measured Wave 0) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test Utinni.Cli.Tests --no-build`
- **After every plan wave:** Run full managed + native suite
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** {N} seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| {N}-01-01 | 01 | 1 | AUTH-{XX} | T-13-01 / — | {expected secure behavior or "N/A"} | unit | `{command}` | ✅ / ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*
*(Filled by the planner per-task and the Nyquist auditor during execution.)*

---

## Wave 0 Requirements

- [ ] Golden fixtures for the new BUILD/SAVE/schema verbs in `Utinni.Cli.Tests`
- [ ] Real pre-CU `.tre` extraction fixtures (cross-check oracle inputs, D-05)
- [ ] `.rsp`-synthesis round-trip harness (D-06)

*To be finalized by the planner against RESEARCH.md.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| {behavior} | AUTH-{XX} | {reason} | {steps} |

*Most BUILD verbs are headless CLI + Get-FileHash — automatable (no live SWG needed). RESID-01 typed display may have a manual editor smoke.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < {N}s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
