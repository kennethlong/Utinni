---
phase: 12
slug: revive-feasibility-spike-hard-gate-intro-skip-crash
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-02
---

# Phase 12 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
>
> This phase is unusual: AUTH-01's validation is a **build-success + byte-exact binary-compare gate**
> (not unit tests), and RESID-02 is an inherently **Tier-4 manual** live-injection smoke. The map below
> reflects that — "automated command" means an MSBuild target or a `Get-FileHash` compare, not xUnit.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | (AUTH-01) MSBuild build-success + binary hash-compare; xUnit `Utinni.Cli.Tests` golden harness is the *conceptual* analog for deterministic artifact compare |
| **Config file** | `tools/Utinni.Tools.sln` (created this phase); CI workflow `.github/workflows/ci.yml`-class build lane |
| **Quick run command** | `MSBuild tools\Utinni.Tools.sln /p:Configuration=Debug /p:Platform=Win32 /t:<Tool>` |
| **Full suite command** | `MSBuild tools\Utinni.Tools.sln /p:Configuration=Debug /p:Platform=Win32` + per-tool byte-exact hash-compare smoke |
| **Estimated runtime** | ~60–180s per tool build; smoke compares are sub-second |

---

## Sampling Rate

- **After every task commit:** `MSBuild …/t:<Tool> /p:Platform=Win32` (the tool touched).
- **After every plan wave:** full `Utinni.Tools.sln` build + all available byte-exact smokes.
- **Before `/gsd:verify-work`:** all three tools build+link green on the self-hosted v145 runner; every
  available byte-exact smoke green (or the D-09 gate finding explicitly surfaced + resolved per tool);
  RESID-02 captured and dispositioned.
- **Max feedback latency:** ~180s (a single-tool build).

---

## Per-Task Verification Map

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| AUTH-01 | TreeFileBuilder builds+links @ v145 standalone (cheapest first-green: 12 ProjRefs, zlib-only, no P4) | build | `MSBuild …/t:TreeFileBuilder /p:Platform=Win32` | ❌ W0 (lift `tools/`) | ⬜ pending |
| AUTH-01 | TemplateCompiler builds+links @ v145 (P4 kept or stubbed) | build | `MSBuild …/t:TemplateCompiler` | ❌ W0 | ⬜ pending |
| AUTH-01 | TemplateDefinitionCompiler builds+links @ v145 | build | `MSBuild …/t:TemplateDefinitionCompiler` | ❌ W0 | ⬜ pending |
| AUTH-01 | TreeFileBuilder `.tre` byte-exact vs known-good | golden (real asset) | run + `Get-FileHash` compare | ❌ W0 (needs ref pair — A1) | ⬜ pending |
| AUTH-01 | TemplateCompiler `.iff` byte-exact | golden (real asset) | run `-compile` + hash compare | ❌ W0 (needs `.tpf`+`.iff` pair — A1) | ⬜ pending |
| AUTH-01 | TemplateDefinitionCompiler generated C++ byte-exact (or normalized) | golden (text) | run `-compile` + diff (banner pinned) | ❌ W0 (needs `.tdf` + ref — A1; banner Pitfall 6) | ⬜ pending |
| AUTH-01 | Build lane green on self-hosted v145 CI | CI gate | push → runner build step | ❌ W0 (D-07 wiring) | ⬜ pending |
| AUTH-01 | Dependency manifest + pinned SHA recorded | artifact check | file presence + content | ❌ W0 (D-08) | ⬜ pending |
| RESID-02 | Faulting module+RVA captured via VEH on live repro | manual Tier-4 | live inject → grep `VEH FATAL` | ✅ VEH deployed (`d1096ac`) | ⬜ pending |
| RESID-02 | Root-cause fixed (Utinni-side) OR documented (game-side) | manual + code/doc | re-run repro / analysis doc | ❌ depends on capture | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tools/Utinni.Tools.sln` + lifted vcxprojs (TreeFileBuilder first) — covers AUTH-01 build
- [ ] `tools/external/` (zlib 1.1.4, pcre 4.1, perforce-or-stub) — link deps
- [ ] `tools/DEPENDENCY-MANIFEST.md` + `tools/PINNED-SHA.md` (`5fce7bb8`) — D-08 artifacts
- [ ] CI workflow build-lane step for the self-hosted runner — D-07
- [ ] **Reference-pair availability checkpoint (maintainer)** — gates all byte-exact smokes (A1)
- [ ] `.gitignore` entry for the tools' `compile/` OutDir
- [ ] (RESID-02) confirm the concrete spdlog log-file path the VEH line lands in

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Faulting module+RVA capture | RESID-02 | Requires a live injected SWG session + TJT-driven scene change; cannot run in CI | Inject Utinni, trigger TJT scene-change/intro-skip path, reproduce crash, read VEH `VEH FATAL` log line for module+RVA |
| Intro-skip no longer crashes (or game-side RCA documented) | RESID-02 | Same live-session dependency; "naked, but in world" = success (baseline, not a crash) | Re-run the repro after fix; if fault resolves into SWG.exe game code, deliver documented RCA (module+RVA+mechanism) instead |
| Byte-exact reference-pair availability | AUTH-01 (D-09) | Reference pairs largely absent from swg-client-v2; need a real client install | Maintainer checkpoint: confirm/provide source→known-good pairs per tool before any byte-exact smoke is committed |

---

## Validation Sign-Off

- [ ] All tasks have an automated build/compare verify or a Wave 0 dependency
- [ ] Sampling continuity: no 3 consecutive build tasks without an automated build/compare
- [ ] Wave 0 covers all MISSING references (sln, externals, reference pairs, CI lane)
- [ ] No watch-mode flags
- [ ] Feedback latency < 180s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
