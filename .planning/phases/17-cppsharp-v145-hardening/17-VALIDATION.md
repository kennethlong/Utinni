---
phase: 17
slug: cppsharp-v145-hardening
status: planned
nyquist_compliant: true
wave_0_complete: true
created: 2026-06-14
---

# Phase 17 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `17-RESEARCH.md` § Validation Architecture (HIGH confidence).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (`UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`), net472 |
| **Config file** | per-project `.csproj`; built via VS2026 MSBuild (never `dotnet build`) |
| **Quick run command** | `dotnet test UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj --no-build -c Release` |
| **Full suite command** | MSBuild `Utinni.sln` (Release\|x86) THEN `dotnet test --no-build` per net472 project |
| **Estimated runtime** | ~30–90 seconds (managed test lanes) |

---

## Sampling Rate

- **After every task commit:** Run the relevant filtered test (`--filter AbiSurface` or
  `--filter FrozenPluginCompose`) + a clean regen + `git checkout -- UtinniCoreDotNet/Generated/UtinniCore.cs`.
- **After every plan wave:** Full net472 test lanes + the two new CI tripwire steps + both-repo
  (Utinni + UtinniPlugins) Release\|x86 build.
- **Before `/gsd:verify-work`:** Full suite green.
- **Max feedback latency:** ~90 seconds (managed lanes); CI tripwires are push-triggered, verify-only.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| spike | 01 | 1 | CPPS-01 | — | N/A | doc + script | `grep '__clang_major__' yvals_core.h` tabulation (committed script) | ❌ W0 | ⬜ pending |
| redirect-doc | 01 | 1 | CPPS-02 | T-V1 (silent SPOF) | Redirect documented as supported config | doc gate | doc-exists + link from `regen-bindings.md` | ❌ W0 | ⬜ pending |
| ci-cpp23 | 02 | 2 | CPPS-03a | T-V5 (path injection) | Scan scoped to `UtinniCore/` `#include` lines; paths quoted | CI scan | new `ci.yml` step: `#include` allowlist scan | ❌ W0 | ⬜ pending |
| ci-clang20 | 02 | 2 | CPPS-03b | T-V14 (spoofed registry) | Committed pin asserted, not live network trust | CI probe | new `ci.yml` step: version-pin assert | ❌ W0 | ⬜ pending |
| abi-diff | 03 | 2 | CPPS-04a | T-V12 (tampered baseline) | Baseline is repo-reviewed committed artifact | unit | `dotnet test UtinniCoreDotNet.Tests --no-build -c Release --filter AbiSurface` | ❌ W0 | ⬜ pending |
| compose-fixture | 03 | 2 | CPPS-04b | T-V10 (DLL static-ctor exec) | Frozen DLL is repo-controlled, reviewed binary | unit | `dotnet test UtinniCoreDotNet.Tests --no-build -c Release --filter FrozenPluginCompose` | ❌ W0 | ⬜ pending |
| lockstep | 03 | 2 | CPPS-04 | T-V10 | TJT rebuild matched to frozen surface | cross-repo build | MSBuild both repos Release\|x86 | n/a (build) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `UtinniCoreDotNet.Tests/AbiSurfaceTests.cs` — covers CPPS-04a (per-block-hash set diff)
- [ ] `UtinniCoreDotNet.Tests/FrozenPluginComposeTests.cs` — covers CPPS-04b (frozen-DLL MEF compose)
- [ ] `UtinniCoreDotNet.Tests/Fixtures/FrozenPlugin/<frozen>.dll` — committed binary fixture (CPPS-04b)
- [ ] ABI baseline block-hash file (committed) + the per-block-hash diff tool (CPPS-04a)
- [ ] `allowed-cpp-stl-headers.txt` (or inline allowlist) for the CI scan (CPPS-03a)
- [ ] Spike script + result doc (CPPS-01) and supported-config doc (CPPS-02)
- [ ] Two new `ci.yml` steps (CPPS-03a/b) — self-hosted, push-only, PowerShell-5.1, verify-only
- [ ] Framework install: **none** — xUnit/net472 lanes already exist.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| A surface-breaking regen ultimately detonates at MEF compose only on inject | CPPS-04 | RNDR/MEF compose is a runtime behavior in the injected x86 client; no headless inject path exists | Maintainer live-smoke inject after a lockstep rebuild — the compose gate + frozen fixture are the automated proxy that must catch it first |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** plans 17-01/02/03 created; AbiSurfaceTests + FrozenPluginComposeTests scaffolded in plan 17-03 (Wave-0 gaps closed in-plan)
