---
phase: 24-client-entry-point-advertisement-getenginehookpoints
verified: 2026-06-30T00:00:00Z
status: passed
score: 4/4 must-haves verified
overrides_applied: 0
retroactive: true   # generated 2026-06-30 during the v2.1 milestone audit; reconstructed from SUMMARY + live-smoke records (no contemporaneous VERIFICATION.md was written at phase close 2026-06-22)
re_verification:
  previous_status: none
human_verification:
  - "Checkpoint 1 (advertised-client inject, no createDetours crash) PASS 2026-06-22"
  - "Checkpoint 3 (SWGEmu D3D9 no-regression / D-00) PASS 2026-06-22"
  - "Checkpoint 2 (DX11 overlay renders) DEFERRED 2026-06-22 then RESOLVED 2026-06-23 (advertised DX11 client boots -> login -> world -> embed-scale)"
---

# Phase 24: Client Entry-Point Advertisement (`GetEngineHookPoints`) — Verification Report (RETROACTIVE)

> **Retroactive note (2026-06-30):** Phase 24 formally closed 2026-06-22 without a contemporaneous
> VERIFICATION.md; the acceptance was recorded in `24-04-SUMMARY` + the live-smoke checkpoint results.
> This report was reconstructed during the v2.1 milestone audit. It also folds in the **2026-06-23 DX11
> resolution** (post-close, at/before the `v2.1` tag `194481c`) that `24-04-SUMMARY` originally recorded
> as deferred.

**Phase Goal:** The from-source SWG-Source client (`SwgClient_r.exe`) advertises its own engine entry
points to UtinniCore via a well-known export, so UtinniCore attaches to the D3D11 client with **zero
hardcoded RVAs** on that client — unblocking the Phase 18 (D-08) and Phase 19 (D-22) live-smokes and
live-preview on D3D11.
**Verified:** 2026-06-30 (retroactive) · **Status:** passed

## Goal Achievement

The goal decomposes into the four ROADMAP success criteria (= EPA-01..04). All four are observably
delivered and live-proven: `SwgClient_r.exe` exports `GetEngineHookPoints` (dumpbin-confirmed); a single
`swg::endpoints` resolver consumes it dual-path (advertised on the from-source client, hardcoded-RVA on
SWGEmu, auto-selected — no config toggle); injecting no longer crashes in `createDetours()` (the exact
2026-06-15 `0xC0000005 READ target=0x00401000` is gone); a missing/partial export degrades cleanly with a
coverage self-check; and the DX11 overlay installs + renders on the advertised client (resolved 2026-06-23),
closing the Phase-18 D-08 / Phase-19 D-22 DX11 live-smokes. The advertised client shipped **RENDER-only**
(~77/230 hook points); the broader ~198/230 full hook coverage is an explicit post-v2.1 follow-on milestone.

### Observable Truths

| # | Truth (Success Criterion) | Status | Evidence |
| --- | --- | --- | --- |
| 1 | Injecting UtinniCore into `SwgClient_r.exe` no longer crashes in `createDetours()`; the first detour resolves through the advertised contract (EPA-01/EPA-02) | ✓ VERIFIED | **Checkpoint 1 PASS 2026-06-22.** Resolver logs `endpoints: resolved 77/77 by name`; `config::loadOverrideConfig` resolves non-null; reaches login embedded in TJT, 0 FATAL. The 2026-06-15 `0x00401000` crash is gone. Five live-discovered issues fixed (`afcf70f` ASLR, `c666ed4` unmapped-target guards, `3effd45` RENDER-only group gate + PanelGame WndProc gate + resolved-but-unsafe subsystem gates). |
| 2 | The advertised contract is compile-time symbol-sourced (`&fn`), versioned, exported from the exe; a coverage test asserts every hooked `swg::*` endpoint is populated (EPA-01) | ✓ VERIFIED | `dumpbin /exports SwgClient_r.exe` → `82 51 00700280 GetEngineHookPoints` (sibling `SwgClient_d.exe` at ordinal 83). Delivered RENDER-only (~77/230); full ~198/230 = follow-on milestone (EPA-01 "full retirement NOT achievable this phase"). |
| 3 | Dual-path discovery: advertised on SWG-Source, hardcoded-RVA on SWGEmu, auto-selected; existing SWGEmu D3D9 live-smoke passes unchanged (EPA-02/EPA-04, D-00) | ✓ VERIFIED | **Checkpoint 3 PASS 2026-06-22.** SWGEmu logs `no GetEngineHookPoints export -- RVA path (SWGEmu Pre-CU)`, 0 FATAL, zero advertised-only lines — full SWGEmu hook set installs as before. D-00 proven byte-for-byte. Graceful-bail + coverage self-check (`utinni_verifyNoNullNoDup`) present (EPA-04). |
| 4 | The Phase-19 DX11 overlay installs + renders on the advertised D3D11 client, kickoff no longer gated on a hardcoded `graphics::install` address — closing D-08/D-22 (EPA-03) | ✓ VERIFIED | **Checkpoint 2 DEFERRED 2026-06-22 → RESOLVED 2026-06-23.** EPA-03 headless (24-03: `graphics::install` resolved → `hkInstall` → `directX11::kickoff`). The 06-22 DX11 null-deref (`0xC0000005 WRITE 0x00000034`) was cleared 06-23 across `d2040ca`/`46b189b`/`8df6f20`/`0a5c072` + provider embed-resize → advertised DX11 client boots → renders login → loads worlds → embed-scales. Closes Phase-18 D-08 + Phase-19 D-22. |

**Score:** 4/4 truths verified

### Automation Gate (24-04)

| Gate | Result |
| --- | --- |
| Full solution build (Release/x86) | PASS (after Rule-3 fix `4af6c12`: exclude zero-UTINNI_API `endpoints.h` from CppSharp discovery) |
| Native `[endpoints]` Catch2 filter | PASS — 186 assertions / 8 cases |
| Full native Catch2 suite | PASS — 400 assertions / 41 cases |
| `Utinni.Cli.Tests` managed lane | PASS — 511 / 2 skipped |
| `dumpbin /exports SwgClient_r.exe` → `GetEngineHookPoints` | PASS — `82 51 00700280` |
| ABI gate (`AbiSurfaceTests`) | 28 ADDED / 0 REMOVED — re-blessed by maintainer (`9275187`, additive/binary-compatible; FrozenPluginComposeTests pass) |

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
| --- | --- | --- | --- | --- |
| EPA-01 | 24-04 | Client advertises `GetEngineHookPoints` (versioned, `&fn`, exe-exported) | ✓ SATISFIED | Truth 2 (dumpbin); RENDER-only, full coverage = follow-on |
| EPA-02 | 24-01, 24-02, 24-04 | UtinniCore consumes the contract dual-path; no hardcoded literal deref on advertised | ✓ SATISFIED | Truths 1 & 3; resolver 77/77 |
| EPA-03 | 24-03, 24-04 | DX11 kickoff decoupled from hardcoded `graphics::install` | ✓ SATISFIED | Truth 4; DX11 live RESOLVED 2026-06-23 |
| EPA-04 | 24-01, 24-02, 24-04 | Missing/partial export degrades cleanly; coverage check | ✓ SATISFIED | Truth 3; graceful-bail + `verifyNoNullNoDup` |

No orphaned requirements — all four IDs (REQUIREMENTS.md, mapped to Phase 24) are claimed by plans and verified.

### Locked-Invariant Compliance

- **D-00 (SWGEmu unchanged):** ✓ Checkpoint 3 — byte-for-byte; every advertised-client guard short-circuits on `!isAdvertisedClient()`.
- **CON-H (null-checked pattern-scan / single-source RVA):** ✓ resolver populates from the advertised table; unadvertised remainder guarded (`VirtualQuery`, `IsExecutableAddress`).
- **CPPS-04 ABI gate:** ✓ 28 ADDED / 0 REMOVED caught by the gate, maintainer-reblessed in lockstep.

### Deferred / Out-of-Scope (not gaps)

| Item | Disposition |
| --- | --- |
| Advertised client is RENDER-only (~77/230 hook points) | Full ~198/230 coverage is an explicit **post-v2.1 follow-on milestone** (EPA-01 scope note). The advertised-client editor-unlock arc is delivering it incrementally (Effects/Chat/Radial/World-pick/Free-cam, v13/119 names). |
| 2 latent ABI-trap slots (`worldSnapshot::addObject`, `treeFile::open`) | Resolve-by-name but mismatch the consumer typedef; never called on the critical path (24-02). Guard before any future consumer use. |
| 8 inert full-catalog endpoint slots | Bound for D-01 completeness with no consumer call-site; nullptr on SWGEmu; harmless. |
| `AbiSurfaceTests` local-RED | Documented incremental-MSBuild-skips-Gen.exe artifact; CI (which runs the gen) is authoritative. Baseline reblessed `9275187`. |

### Gaps Summary

No gaps against the phase's stated scope. All four success criteria are observably satisfied and
live-proven (Checkpoints 1+3 PASS 2026-06-22; Checkpoint 2 / DX11 RESOLVED 2026-06-23, at/before the
`v2.1` tag). SWGEmu is byte-for-byte unchanged (D-00). The RENDER-only advertised client with full hook
coverage as a follow-on is an explicit, documented scope boundary — not an unmet requirement.

---

_Verified: 2026-06-30 (retroactive, v2.1 milestone audit)_
_Verifier: Claude — reconstructed from 24-01..04 SUMMARY + live-smoke checkpoint records_
