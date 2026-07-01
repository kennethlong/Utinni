---
phase: 24
slug: client-entry-point-advertisement-getenginehookpoints
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-06-21
validated: 2026-06-30
---

# Phase 24 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

> **Retroactive reconciliation (2026-06-30, v2.1 milestone audit):** left at `draft` after the phase
> closed. The `[endpoints]` Catch2 lane shipped CI-green; live-smoke Checkpoints 1+3 PASSED 2026-06-22
> and the DX11 Checkpoint 2 CLOSED 2026-06-23. Flipped to `complete` / `nyquist_compliant: true`.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Catch2 (native `UtinniCore.Tests`, built via MSBuild) + xUnit (managed lanes, unchanged this phase) |
| **Config file** | none — Wave 0 adds `UtinniCore.Tests/endpoints_tests.cpp` (no framework install; Catch2 already present) |
| **Quick run command** | Catch2 `[endpoints]` tag filter (e.g. `UtinniCore.Tests.exe "[endpoints]"`) after the resolver TU + test build |
| **Full suite command** | VS2026 MSBuild `Utinni.sln /p:Configuration=Release /p:Platform=x86` → Catch2 `UtinniCore.Tests` + `dotnet test --no-build` |
| **Estimated runtime** | ~2–5 s for the `[endpoints]` filter; full suite per the existing CI lanes |

---

## Sampling Rate

- **After every task commit:** Run the Catch2 `[endpoints]` quick filter
- **After every plan wave:** Run the full suite (MSBuild + Catch2 + `dotnet test --no-build`)
- **Before `/gsd:verify-work`:** Full suite must be green, THEN the 3 maintainer live-smokes
- **Max feedback latency:** ~5 seconds (headless units); live-smokes are irreducibly manual

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 24-01-xx | 01 | 0 | EPA-02 | T-24-01 | Resolver binds names from a fixture table; missing-name leaves the RVA literal unchanged (no null-deref) | unit (Catch2) | `[endpoints][resolve]` | ❌ W0 | ⬜ pending |
| 24-01-xx | 01 | 0 | EPA-02 | T-24-03 | Export-absent → resolver is a strict no-op; `pFn` slots untouched (SWGEmu path verbatim) | unit (Catch2) | `[endpoints][dualpath]` | ❌ W0 | ⬜ pending |
| 24-01-xx | 01 | 0 | EPA-04 | T-24-04 | `s_bindings[]` names are a compile-time subset of `utinni_engine_hookpoints.inc`; coverage summary counts resolved/missing | compile-time `static_assert` + Catch2 | `[endpoints][coverage]` | ❌ W0 | ⬜ pending |
| 24-01-xx | 01 | 0 | EPA-04 | T-24-02 | Version-mismatch (`version=999`) logs a soft warning but still resolves by name | unit (Catch2) | `[endpoints][version]` | ❌ W0 | ⬜ pending |
| 24-01-xx | 01 | 0 | EPA-02/EPA-04 | T-24-01 | Resolver null-checks `pGet`/`table`/`entries`/`addr` (malformed/partial table → graceful bail, never deref) | unit (Catch2) | `[endpoints][robustness]` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky. Task IDs are placeholders until the planner fixes plan/task numbering.*

---

## Wave 0 Requirements

- [ ] `UtinniCore.Tests/endpoints_tests.cpp` — resolver bind / export-absent no-op / version-mismatch / coverage-count units (covers EPA-02, EPA-04). The resolver MUST be unit-testable WITHOUT injection: factor `resolve()` to accept a `const UtinniEngineHookPoints*` (synthetic fixture) + a binding list; the `GetProcAddress` discovery stays a thin shell.
- [ ] A compile-time X-macro subset `static_assert` that every `s_bindings[]` name exists in `utinni_engine_hookpoints.inc`.
- [ ] No new framework install — Catch2 + xUnit already present.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| First detour against `config::loadOverrideConfig` completes — no `0xC0000005 READ target=0x00401000` on `SwgClient_r.exe` | EPA-01 / EPA-02 | Irreducible maintainer-only injection (no headless inject path per AGENTS.md) | `dumpbin /exports stage/SwgClient_r.exe` shows `GetEngineHookPoints`; inject UtinniCore; grep log for VEH FATAL absence |
| SWGEmu Pre-CU D3D9 live-smoke unchanged (no regression) | EPA-02 | Maintainer-only inject + overlay eyeball | Inject into the existing SWGEmu client; confirm overlay renders + mouse/keyboard as today (no resolver behavior change on the absent-export path) |
| DX11 overlay renders on `SwgClient_r.exe` (closes D-08 / D-22) | EPA-03 | Maintainer-only inject; live render | Inject with rasterMajor=11; confirm ImGui DX11 overlay visible via the resolved `graphics::install` → `kickoff()` path |

*The 3 live-smokes prove only inject + render, not resolver logic — Wave-0 units make everything else green automatically.*

---

## Validation Sign-Off

- [ ] All tasks have automated verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 5s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
