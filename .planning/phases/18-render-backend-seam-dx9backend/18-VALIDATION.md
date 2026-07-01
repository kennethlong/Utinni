---
phase: 18
slug: render-backend-seam-dx9backend
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-06-15
validated: 2026-06-30
---

# Phase 18 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

> **Retroactive reconciliation (2026-06-30, v2.1 milestone audit):** left at `draft` after the phase
> closed. Phase 18 shipped CI-green with the D-08 D3D9 live-smoke PASSED (2026-06-15); flipped to
> `complete` / `nyquist_compliant: true`.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Catch2 (native, `UtinniCore.Tests`) + xUnit (`UtinniCoreDotNet.Tests` PreservationAudit) |
| **Config file** | none — clones existing `NoDeviceResetTests.cpp` source-gate harness |
| **Quick run command** | `dotnet test --no-build` (managed PreservationAudit lane) |
| **Full suite command** | MSBuild `Utinni.sln /p:Configuration=Release /p:Platform=x86` then native Catch2 suite + `dotnet test --no-build` |
| **Estimated runtime** | build-bound (managed lane ~seconds; native suite seconds after build) |

---

## Sampling Rate

- **After every task commit:** Run the managed PreservationAudit lane / native Catch2 source-gate
- **After every plan wave:** Run the full suite (MSBuild + Catch2 + dotnet test)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** build-bound

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 18-01-01 | 01 | 1 | RNDR-01 | — | N/A | unit | `dotnet test --no-build` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

> The planner populates this map per task. Two automated layers are the CI-enforceable
> regression protection (D-06 structural API-neutrality grep-gate; D-07 Catch2 seam
> dispatch test with a mock `IRenderBackend`). The live-smoke (D-08) is manual-only.

---

## Wave 0 Requirements

- [ ] D-06 structural gate — clone `NoDeviceResetTests.cpp` comment-stripping source-gate, assert ZERO `ImGui_ImplDX9_` / `IDirect3DDevice9` / `#include <d3d9.h>` in `imgui_impl.{cpp,h}`
- [ ] D-07 seam dispatch test — mock/no-op `IRenderBackend` asserting all 10 pure virtuals route through the vtable (heap-free)

*Existing infrastructure (Catch2 + xUnit PreservationAudit) covers the framework; new test files added in-phase.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Overlay renders + takes input in live D3D9 SWG session | RNDR-01 | Maintainer live-smoke; CI cannot inject into live SWG.exe (D-08) | Inject `UtinniCore.dll`, confirm overlay renders, mouse + keyboard + Issue #11 chat-context routing work, no Reset/crash through resize |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency build-bound
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
