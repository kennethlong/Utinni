---
phase: 12
slug: revive-feasibility-spike-hard-gate-intro-skip-crash
status: validated
nyquist_compliant: true
wave_0_complete: true
created: 2026-06-02
validated: 2026-06-14
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
| AUTH-01 | TreeFileBuilder builds+links @ v145 standalone (cheapest first-green: 12 ProjRefs, zlib-only, no P4) | build | `MSBuild …/t:TreeFileBuilder /p:Platform=Win32` | ✅ `_d.exe` on disk | ✅ green |
| AUTH-01 | TemplateCompiler builds+links @ v145 (P4 kept or stubbed) | build | `MSBuild …/t:TemplateCompiler` | ✅ `_d.exe` on disk | ✅ green |
| AUTH-01 | TemplateDefinitionCompiler builds+links @ v145 | build | `MSBuild …/t:TemplateDefinitionCompiler` | ✅ `_d.exe` on disk | ✅ green |
| AUTH-01 | TreeFileBuilder `.tre` byte-exact vs known-good | golden (real asset) | run + `Get-FileHash` compare | ⚠️ no ref pair (A1) | ⚠️ deferred-gate-finding → Phase 13 |
| AUTH-01 | TemplateCompiler `.iff` byte-exact | golden (real asset) | run `-compile` + hash compare | ⚠️ no `.tpf`+`.iff` (A1) | ⚠️ deferred-gate-finding → Phase 13 |
| AUTH-01 | TemplateDefinitionCompiler generated C++ byte-exact (or normalized) | golden (text) | run `-compile` + diff (banner pinned) | ⚠️ no `.tdf`+ref (A1) | ⚠️ deferred-gate-finding → Phase 13 |
| AUTH-01 | Build lane green on self-hosted v145 CI | CI gate | push → runner build step | ✅ `ci.yml:196-202` | ✅ green |
| AUTH-01 | Dependency manifest + pinned SHA recorded | artifact check | file presence + content | ✅ both present | ✅ green |
| RESID-02 | Faulting module+RVA captured via VEH on live repro | manual Tier-4 | live inject → grep `VEH FATAL` | ✅ VEH deployed (`utinni.cpp:291`) | ✅ A5 no-repro (no fault to capture) |
| RESID-02 | Root-cause fixed (Utinni-side) OR documented (game-side) | manual + code/doc | re-run repro / analysis doc | ✅ `12-RESID-02-RCA.md` | ✅ green (RCA + 12-UAT Test 5) |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky/deferred*

**Compliance rationale:** Every AUTOMATABLE requirement is automated — the three tool builds + the self-hosted CI hard-gate lane (`ci.yml:196-202`, non-zero MSBuild exit fails the job) + the artifact-presence checks. The three byte-exact golden rows are NOT coverage holes: they are the documented **A1 gate-finding** — zero compatible source→known-good reference pairs exist (retail corpus is Restoration v6000/encrypted; the one 0005 asset has no source/`.rsp`; no `.tpf`/`.tdf` exist), so no test is constructible until real assets flow through the Phase-13 golden-fixture harness. The byte-exact smoke harness (`tools/smoke/byte-exact-smoke.ps1`) is staged and activates the instant a reference pair is supplied. RESID-02 is inherently Tier-4 live-injection, dispositioned A5 no-repro and maintainer-confirmed. This is consistent with the project convention (Phases 14/15/16 are `nyquist_compliant: true` with documented Tier-4 residuals).

---

## Wave 0 Requirements

- [x] `tools/Utinni.Tools.sln` + lifted vcxprojs (TreeFileBuilder first) — covers AUTH-01 build
- [x] `tools/external/` (zlib 1.1.4, pcre 4.1, perforce keep-link) — link deps
- [x] `tools/DEPENDENCY-MANIFEST.md` + `tools/PINNED-SHA.md` (`5fce7bb8`) — D-08 artifacts
- [x] CI workflow build-lane step for the self-hosted runner — D-07 (`ci.yml:196-202`)
- [x] **Reference-pair availability checkpoint (maintainer)** — resolved as per-tool A1 gate-findings (no compatible pair exists; deferred to Phase 13)
- [x] `.gitignore` entry for the tools' `compile/` OutDir
- [x] (RESID-02) VEH line path confirmed (`bin\Release\utinni.log`, watched live in 12-04)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Faulting module+RVA capture | RESID-02 | Requires a live injected SWG session + TJT-driven scene change; cannot run in CI | Inject Utinni, trigger TJT scene-change/intro-skip path, reproduce crash, read VEH `VEH FATAL` log line for module+RVA |
| Intro-skip no longer crashes (or game-side RCA documented) | RESID-02 | Same live-session dependency; "naked, but in world" = success (baseline, not a crash) | Re-run the repro after fix; if fault resolves into SWG.exe game code, deliver documented RCA (module+RVA+mechanism) instead |
| Byte-exact reference-pair availability | AUTH-01 (D-09) | Reference pairs largely absent from swg-client-v2; need a real client install | Maintainer checkpoint: confirm/provide source→known-good pairs per tool before any byte-exact smoke is committed |

---

## Validation Sign-Off

- [x] All tasks have an automated build/compare verify or a justified manual-only disposition
- [x] Sampling continuity: no 3 consecutive build tasks without an automated build/compare
- [x] Wave 0 covers all MISSING references (sln, externals, reference pairs → A1 gate-finding, CI lane)
- [x] No watch-mode flags
- [x] Feedback latency < 180s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-06-14 (post-execution audit)

---

## Validation Audit 2026-06-14

State-A post-execution audit (file was the planning-time draft; never updated after the phase shipped).

| Metric | Count |
|--------|-------|
| Requirements/behaviors mapped | 10 |
| Automated (build / CI gate / artifact check) | 5 ✅ |
| Manual-only, done (RESID-02 RCA + UAT) | 2 ✅ |
| Deferred gate-findings (A1 byte-exact, no reference assets) | 3 ⚠️ → Phase 13 |
| MISSING gaps (test generatable, unfilled) | 0 |
| Tests generated this audit | 0 (no constructible test — assets absent / inherently Tier-4) |

**Disposition:** `nyquist_compliant: true`. No `gsd-nyquist-auditor` spawn — there were zero fixable MISSING gaps. The non-automated rows are inherently manual-only (live-injection) or blocked on absent reference assets (the documented A1 gate-finding, retiring in Phase 13's golden-fixture harness), all already enumerated in Manual-Only. Cross-confirmed by `12-UAT.md` (5/5 pass, committed `ec24e27`).
