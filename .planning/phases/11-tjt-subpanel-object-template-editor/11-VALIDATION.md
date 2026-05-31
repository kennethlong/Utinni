---
phase: 11
slug: tjt-subpanel-object-template-editor
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-05-30
---

# Phase 11 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Transcribed from `11-RESEARCH.md` §Validation Architecture (the load-bearing source of truth).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (C#, .NET Framework) — `UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`. (Catch2 native suite exists but is not exercised this phase.) |
| **Config file** | existing `.csproj` test projects — no new config |
| **Quick run command** | `dotnet test UtinniCoreDotNet.Tests --no-build --filter ObjectTemplate` (after MSBuild) |
| **Full suite command** | MSBuild (VS2026 / v145, Debug+Release\|x86) then `dotnet test --no-build` across `UtinniCoreDotNet.Tests` + `Utinni.Cli.Tests` |
| **Estimated runtime** | quick filtered run ~10–20s; full suite ~3–5 min (dominated by the MSBuild build step — `dotnet build` cannot compile TJT resx, so MSBuild is the only build path, then `--no-build` for xUnit) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test UtinniCoreDotNet.Tests --no-build --filter ObjectTemplate`
- **After every plan wave:** Run the full suite (MSBuild Debug+Release\|x86, then `dotnet test --no-build` across both test projects)
- **Before `/gsd:verify-work`:** Full suite must be green AND the V1 release-gate aggregate satisfied (all 5 Wave-1 subpanels demo, Tier 1+2 CI green on `main`, 15 critical bugs closed), then tag V1
- **Max feedback latency:** ~20 seconds (quick filtered run)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 11-01-01 | 01 | 1 | PROD-W1-OT | V5 input-validation (forged param count → over-read) | Self-describing scalar decode consumes-exactly-or-falls-to-hex; never over-reads on a malformed type tag | unit (tdd) | `dotnet test UtinniCoreDotNet.Tests --no-build --filter ObjectTemplateParam` | ❌ W0 (new) | ⬜ pending |
| 11-01-02 | 01 | 1 | PROD-W1-OT | V5 (bounded slice capture) | Byte-exact write on untouched params (CF-02); `MutableIffDocument` offset validation reused | unit + golden (tdd) | `dotnet test UtinniCoreDotNet.Tests --no-build --filter ObjectTemplate` | ❌ W0 (new) | ⬜ pending |
| 11-02-01 | 02 | 2 | PROD-W1-OT | **DERV depth/cycle guard (NEW control); path-traversal on base name** | Effective-merge = nearest-ancestor-with-chunk; cyclic/deep DERV bounded by depth cap + visited-set; unresolved base degrades (never throws) | unit (tdd) | `dotnet test UtinniCoreDotNet.Tests --no-build --filter ObjectTemplateResolver` (+ `--filter UnresolvedBase`, `--filter Cycle`) | ❌ W0 (new) | ⬜ pending |
| 11-02-02 | 02 | 2 | PROD-W1-OT | — | Override/add-override/revert mutations + editor-local undo (D-04/CF-04); param-level byte-exact slice (roundtrip-ot) | unit + golden (tdd) | `dotnet test UtinniCoreDotNet.Tests --no-build --filter ObjectTemplateEditController` + `dotnet test Utinni.Cli.Tests --no-build --filter Roundtrip` | ❌ W0 (new) | ⬜ pending |
| 11-03-01 | 03 | 3 | PROD-W1-OT | — | Host form renders effective view (Field·Value·Origin·Type); no data-path logic | compile gate | MSBuild `TheJawaToolboxDotNet.csproj` (Debug\|x86) | n/a (UI) | ⬜ pending |
| 11-03-02 | 03 | 3 | PROD-W1-OT | — | 5th SubPanel registration + TRE/IFF hand-offs | compile gate | MSBuild `TheJawaToolboxDotNet.csproj` (Debug\|x86) | n/a (UI) | ⬜ pending |
| 11-04-01 | 04 | 4 | PROD-W1-OT | V5 (hex-fallback never mis-types a complex param) | Per-type widgets + hex-fallback sub-editor + override/revert/edit mutations | compile gate | MSBuild `TheJawaToolboxDotNet.csproj` (Debug\|x86) | n/a (UI) | ⬜ pending |
| 11-04-02 | 04 | 4 | PROD-W1-OT | V5 (save-back byte integrity) | Save modes 1/2/4 shim; CF-05 reload badge verbatim (grep-gated); classifier → PendingNextSceneChange | unit + grep gate | `dotnet test UtinniCoreDotNet.Tests --no-build --filter ReloadAssetClassifier` + badge-wording grep assertion | ✅ extend | ⬜ pending |
| 11-05-01 | 05 | 5 | PROD-W1-OT | all-of-above (regression) | Full automated regression + V1 release-gate evidence doc | full suite | MSBuild (Debug+Release\|x86) + `dotnet test --no-build` (both projects) | ✅ aggregate | ⬜ pending |
| 11-05-02 | 05 | 5 | PROD-W1-OT | — | Live-SWG smoke SC1/SC2/SC3 + five-subpanel demo | manual / Tier-4 | live-client demo (checkpoint:human-verify) | manual residual | ⬜ pending |
| 11-05-03 | 05 | 5 | PROD-02 | — | V1 release-gate sign-off + tag V1 | manual | release checklist + `git tag` (checkpoint:human-action) | manual residual | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `UtinniCoreDotNet.Tests/ObjectTemplate/ObjectTemplateParamTests.cs` — self-describing scalar decode/encode (PROD-W1-OT)
- [ ] `UtinniCoreDotNet.Tests/ObjectTemplate/ObjectTemplateResolverTests.cs` — effective-merge + origin + unresolved-base + depth/cycle guard (PROD-W1-OT, D-01)
- [ ] `UtinniCoreDotNet.Tests/Editing/ObjectTemplateEditControllerTests.cs` — override/revert/edit + undo (D-04/CF-04)
- [ ] `Utinni.Cli.Tests` goldens — multi-level chain, complex-param hex-fallback, unresolved-base (CF-02); `roundtrip-ot`
- [ ] Extend `ReloadAssetClassifier` tests with an OT root-type case if not already present
- [ ] Framework install: none — existing xUnit infra covers all phase requirements

*The four logic tasks (11-01-01, 11-01-02, 11-02-01, 11-02-02) are `tdd="true"` and create their own `*Tests.cs` files inline (RED→GREEN), so Wave 0 is satisfied by the TDD tasks themselves rather than a separate Wave-0 plan.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| SC1: subpanel loads inside TJT against a live SWG client | PROD-W1-OT | Requires injected live SWGEmu/Restoration client (D3D9 detour surface) | Inject Utinni, open TJT, confirm Object Template Editor SubPanel loads (11-05-02) |
| SC2: open → view inherited fields → edit overrideable → save back | PROD-W1-OT | End-to-end UI + live client | Open a tangible template, edit one scalar override, save (mode 1/4) (11-05-02) |
| SC3: live client reflects the edit | PROD-W1-OT | Cache reality: **relog-reliable, respawn-best-effort, scene-change-conditional** (CF-05); not unit-observable | Edit → save → **relog** client → observe object reflects edit. Documented Tier-4 residual (precedent: Phases 8/9/10) (11-05-02) |
| V1 release-gate aggregate (5 subpanels demo, CI green, 15 bugs closed) | PROD-02 | Milestone gate spanning prior phases; manual sign-off + tag | Run the V1 release checklist; tag V1 (11-05-03) |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies — every auto/tdd task carries an MSBuild compile gate or a `dotnet test` filter; checkpoint tasks (11-05-02/03) are the documented Tier-4 manual residual
- [x] Sampling continuity: no 3 consecutive tasks without an automated verify — every task has an automated command. *Note: 11-03-01, 11-03-02, 11-04-01 are compile-gate-only (WinForms UI is not unit-testable in this harness, per Phase 8/9/10 precedent); their data-path correctness is covered upstream by the Wave 1/2 unit + `roundtrip-ot` goldens and downstream by the Plan 05 live smoke.*
- [x] Wave 0 covers all MISSING references — the four new `*Tests.cs` files are created inline by the TDD tasks
- [x] No watch-mode flags — all commands use `--no-build` (one-shot), no `--watch`
- [x] Feedback latency < 20s — quick filtered run
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-05-30
