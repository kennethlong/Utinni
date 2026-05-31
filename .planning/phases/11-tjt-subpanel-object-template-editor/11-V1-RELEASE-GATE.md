---
phase: 11-tjt-subpanel-object-template-editor
plan: 05
artifact: V1-RELEASE-GATE
maps_to: ROADMAP Phase 11 Success Criteria SC4
status: EVIDENCE-COMPLETE — maintainer sign-off + V1 tag PENDING (Task 2 live smoke + Task 3 tag)
created: 2026-05-31
---

# V1 Release Gate — Object Template Editor / Wave-1 Closure

This is the aggregate evidence checklist for **ROADMAP Phase 11 Success Criterion SC4** — the V1
release gate. Each gate condition has one evidence row with its proof. The four automatable /
documentary conditions (five subpanels exist + demo, Tier 1 + Tier 2 CI green on `main`, the 15
critical bugs closed) are filled by this plan's Task 1. The **live five-subpanel demo confirmation**
(SC1/SC2/SC3 for the Object Template Editor + a same-session demo of all five subpanels) and the
**V1 tag** are the human-gated steps (Task 2 smoke + Task 3 tag) and are left UNCHECKED below.

> **SC4 (ROADMAP, verbatim):** "V1 release gate met: all 15 critical bugs are closed, Tier 1 + Tier 2
> CI is green on `main`, and all five Wave-1 subpanels (TRE Browser, IFF Editor, Datatable Editor,
> String-table Editor, Object Template Editor) demo end-to-end inside TJT against a live SWG client.
> Tag V1."

## Automation context (Task 1, 2026-05-31)

- **Build:** VS2026 MSBuild (Dev18, v145). `Utinni.sln` **Debug|x86 AND Release|x86** — Build succeeded,
  0 errors (69 pre-existing xUnit-analyzer warnings only). `TheJawaToolboxDotNet.csproj` (UtinniPlugins)
  **Debug|x86 AND Release|x86** — clean (`-> bin\{Debug,Release}\Plugins\TheJawaToolbox\TheJawaToolboxDotNet.dll`).
- **Full automated suite (`dotnet test --no-build`, BOTH configs):**
  - `UtinniCoreDotNet.Tests` — **624 passed / 0 failed / 0 skipped** (Debug|x86 AND Release|x86).
  - `Utinni.Cli.Tests` — **165 passed / 0 failed / 2 skipped** (env-gated skips, both configs; consistent
    with Phase 10's 09-07 / 10-06 precedent).
  - Plan filtered acceptance gates: `--filter ObjectTemplate` → **31 passed / 0 failed**;
    `--filter RoundtripOt` → **9 passed / 0 failed** (the multi-level chain, complex-param hex-fallback,
    and unresolved-base degradation goldens from 11-02).
- **Native (C++ Catch2) suite:** built + run in the self-hosted CI lane (see CI row below), not on the
  executor host — v145/VS2026-Insiders + CppSharp parser pin make the native build the CI/host's job per
  `project_self_hosted_ci` / `project_vs2026_cppsharp_block`, the Phase 8/9/10 precedent.

## SC4 Evidence Checklist

### Condition 1 — All five Wave-1 subpanels demo end-to-end inside TJT

Each subpanel ships as an `IEditorPlugin` SubPanel registered in TJT `Plugin.cs` `SubPanelContainer`
(CF-06 / DEC-C4). Existence + registration + CI-green + the per-phase smoke disposition are documentary;
the **same-session live five-subpanel demo** is confirmed at the Task 2 smoke.

| # | Subpanel | Phase | Build/registration evidence | Live-smoke disposition (documentary) |
|---|----------|-------|------------------------------|---------------------------------------|
| [x] | **TRE Browser** (read-only) | P7 | 07-01..07-04b complete; `docs(07)` UI-audit remediation `0d6d623`; CI green `26535760166` | 07-02 / 07-03 live smoke **APPROVED** (STATE: "live smoke approved") |
| [x] | **IFF Editor** (read+write) | P8 | 08-01..08-07 complete; `docs(08-07)` `617cea0` | 08-05 Task 5 smoke **APPROVED** 2026-05-28 ("approved, dig in"); 08-06/08-07 automation-augmented |
| [x] | **Datatable Editor** (`.tab`) | P9 | 09-01..09-07 complete; `docs(09-07)` `d364209` | 09-07 Tier-4 smoke artifact; automation-augmented, live ACK deferred-but-acceptable (Phase 8 precedent) |
| [x] | **String-table Editor** (`.stf`) | P10 | 10-01..10-06 complete; `docs(10-06)` `3dbc3ad` | 10-06 **SIGNED-OFF 2026-05-30** APPROVED-WITH-DEFERRED-RESIDUAL (SC1/SC2/SC4; SC3 open residual) |
| [x] | **Object Template Editor** | P11 | 11-01..11-04 complete; 5th SubPanel registered (11-03 `bc32f12`); mutations/widgets/save/badge (11-04 `758330d`) | **PENDING** — Task 2 live smoke (this plan) |

- [ ] **Live five-subpanel same-session demo confirmed (SC4 demo leg) — Task 2 smoke (BLOCKING HUMAN).**
  All five subpanels open and demo inside TJT in the same live SWG session. UNCHECKED until the maintainer
  records the Task 2 smoke outcome.

### Condition 2 — Tier 1 + Tier 2 CI green on `main`

- [x] **Tier 1 (C# `dotnet test` + native Catch2) + Tier 2 (CLI golden) CI green on `master`.**
  - **Evidence (LIVE — observed this session):** CI run **`26701536710`** on `master` HEAD `3d4227c`
    (`docs(11-04): complete OT editor mutations + widgets + save + reload-badge plan`) — **conclusion:
    success**, 0 non-success steps. Lanes in `.github/workflows/ci.yml`: clang-format style gate →
    Build (Release|x86) → **Tier 1 C# `dotnet test UtinniCoreDotNet.Tests`** → **Tier 2 `dotnet test
    Utinni.Cli.Tests`** (CLI golden) → **Tier 1 native `bin\Release\UtinniCore.Tests.exe`** (Catch2,
    console+junit reporters) → native Debug|x86 + RelWithDbgInfo|x86 triple-config build verify.
  - **Corroborating prior green:** `26535760166` (`docs(07)` UI-audit remediation, 2026-05-27) — last
    completed success before the HEAD run.
  - **Documentary note:** this is the canonical V1 "Tier 1 + Tier 2 CI green on main" condition. The HEAD
    run above was observed directly via `gh run view --json conclusion` (not fabricated); the V1 tag in
    Task 3 should be applied to a `master` commit that carries a green CI run (the HEAD `3d4227c` run, or
    the green run for whatever commit Task 1's docs land on).

### Condition 3 — The 15 critical bugs (C-01..C-15 / STAB-01) closed

- [x] **All 15 critical bugs closed in code, each with a fix commit.**
  - **Evidence:** `docs/ai/assessment.md` "Critical issues" status table — every C-01..C-15 row reads
    `done` with its fix commit: C-01 `b2f5c16`, C-02 `8e88879`, C-03 `70038a9`, C-04 `9aa0eb9`,
    C-05 `5fd0dac`, C-06 `efdb80b`, C-07 `1a8ff42`, C-08 `c6879b5`, C-09 `c3ba6fd`, C-10 `eabc0d2`,
    C-11 `ba1402a`, C-12 `88b5b6b`, C-13 (UtinniPlugins `1c1eb0a`), C-14 `e7c6699`, C-15 `8a4d7f9`.
  - **Phase provenance:** Phase 2 (02-01..02-04) + Phase 02.1 gap-closure (CR-02/03/04 + WR-01/02/03/05/09,
    each with a fail-on-revert regression test). STATE.md records Phase 2 + 02.1 complete; ROADMAP
    Requirements: STAB-01.
  - **Live UAT corroboration:** the two architectural criticals (C-01 loader-lock, C-09 busy-wait) carry
    PASSED Tier-4 live-SWG UAT entries (STATE "Resolved Deferred Items": 02-03 Task 3 + 02-04 Task 2,
    2026-05-18).

### SC3 honest residual — Object-template live re-resolution (CF-05 cache reality)

- [x] **SC3 residual documented (not a blocker; tracked).**
  Object-template live re-resolution is **relog-reliable, scene-change-conditional, respawn-best-effort**.
  This follows directly from the verified `ObjectTemplateList` / `DataResourceList<ObjectTemplate>` cache:
  it is CRC-keyed and refcount-evicted and **never re-reads a cached template from disk on `fetch`**
  (11-RESEARCH MUST-CONFIRM #1, HIGH confidence from swg-client-v2 source). Consequences:
  - **Respawn** (template still cached) → edit **NOT** reliably reflected (cache hit returns the stale
    instance) — expected, not a defect.
  - **TJT-driven scene change** → **conditional** — reflected only if the edited template's references all
    drop (bases shared across scenes persist).
  - **Full relog / client restart** → **reliable** — cache rebuilt from disk.
  - The editor STATES this via the LOCKED CF-05 tier-(b) badge ("Reloads on next scene change (relog to
    guarantee)") and **never triggers** a reload (no `ObjectTemplateList::reload` / refetch-hook call;
    CON-M-05 preserved; verified by the 11-04 refetch-hook grep gate reading 0).
  - **Automation closes:** byte-exactness (`roundtrip-ot` goldens), self-describing scalar decode/encode,
    DERV-chain effective-merge + origin, unresolved-base graceful degradation, and the
    `ReloadAssetClassifier` OT-root → `PendingNextSceneChange` tier (11-04 `ReloadAssetClassifierTests`).
  - **Live observation = the bounded Tier-4 residual**, mirroring **Phase 10's deferred SC3 live-reload
    residual** (10-06 APPROVED-WITH-DEFERRED-RESIDUAL, 2026-05-30) and the Phase 8/9 automation-augmented
    precedent. Recorded in `docs/ai/test-harness-plan.md` Tier-4 section (this plan, Task 1).

## Maintainer Sign-off + V1 Tag (Tasks 2 + 3 — BLOCKING HUMAN)

These lines are deliberately UNCHECKED. They are closed by the maintainer at the Task 2 live smoke and the
Task 3 tag action, NOT by the executor.

- [ ] **Task 2 — Live-SWG smoke APPROVED.** SC1 (OT Editor loads inside TJT), SC2 (view inherited fields +
  edit/override/revert + save), SC3 (reflected on the honest CF-05 path — relog-reliable), and the
  five-subpanel same-session demo confirmed against a live SWG client. Resume-signal recorded.
  - **Disposition:** _______________________________  **Maintainer:** ______________  **Date:** __________
- [ ] **Task 3 — V1 release gate SIGNED OFF.** Every SC4 row above satisfied (SC3 residual acknowledged per
  the Phase 8/9/10 precedent).
  - **Maintainer signature:** ______________________  **Date:** __________
- [ ] **Task 3 — V1 TAG applied.** Tagged the agreed `master` commit (e.g. `v1.0.0`, following the Phase 6
  `release.yml` / `v1.0.0-rc.1` precedent); GitHub release/artifact produced.
  - **Tag name:** ______________  **Tagged commit:** ______________  **Date:** __________

---
*Created 2026-05-31 by Plan 11-05 Task 1. Evidence rows reflect automation + documentary proof as of HEAD
`3d4227c`; the sign-off + tag lines are the human-gated Task 2/3 steps.*
