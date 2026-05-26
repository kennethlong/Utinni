---
phase: 07
slug: tjt-subpanel-tre-browser-read-only
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-26
---

# Phase 07 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit / MSTest (existing `Utinni.Cli.Tests`) + golden-fixture asserts |
| **Config file** | `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` (existing from Phase 4) |
| **Quick run command** | `dotnet test Utinni.Cli.Tests --filter Category=Tre` |
| **Full suite command** | `dotnet test Utinni.Cli.Tests` |
| **Estimated runtime** | ~{N} seconds (TBD — confirm during Wave 0) |

> The TRE/IFF read path is shared between `Utinni.Cli` (golden-tested) and the
> TJT browser (success criterion #4), so CLI golden tests validate the same code
> the subpanel calls. UI-host behavior (subpanel load, tree expand) is
> manual/live-SWG only — see Manual-Only Verifications.

---

## Sampling Rate

- **After every task commit:** Run `dotnet test Utinni.Cli.Tests --filter Category=Tre`
- **After every plan wave:** Run `dotnet test Utinni.Cli.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** {N} seconds (TBD)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| {N}-01-01 | 01 | 1 | PROD-W1-TRE | — | N/A | unit | `dotnet test Utinni.Cli.Tests --filter Category=Tre` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

> Populated by the planner. Key validation targets surfaced by research:
> - **TRE version dispatch** (0004/0005/0006 size-first vs 6000/COT2000 crc-first) — golden assert against real fixtures in `D:\Sample-TRE-Files\`.
> - **zlib RFC1950 framing** (`0x78 0x9c` strip) — decode-roundtrip assert on a real v6000 block.
> - **5000 defensive path** — assert "recognized tag → enumerate-only, no layout assertion" (no fixture available; structural-sibling-of-6000 behavior).
> - **Per-type decoders** (datatable/STF/object-template/mesh) — structural asserts vs known fixtures.

---

## Wave 0 Requirements

- [ ] TRE golden fixtures wired from `D:\Sample-TRE-Files\` (real v6000/COT2000 set + Utinni's synthesized 0005 fixture)
- [ ] Source a real SWGEmu 0004/0005/0006 fixture if available (open question #1 — until then keep size-first for those versions)
- [ ] Confirm `Utinni.Cli.Tests` framework + quick-run timing

*If none: "Existing infrastructure covers all phase requirements."*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| TRE Browser subpanel loads inside TJT against a live SWG client | PROD-W1-TRE (criterion 1) | Requires injected live SWG session; no headless harness for TJT UI host | Inject Utinni+TJT into SWG, open TJT, confirm TRE Browser subpanel renders |
| Navigate full `.tre` mount set, expand subtrees, view metadata | PROD-W1-TRE (criterion 2) | UI interaction in live host | Expand virtual-path tree, select files, confirm metadata pane populates |

*If none: "All phase behaviors have automated verification."*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < {N}s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
