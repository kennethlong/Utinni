---
phase: 15
slug: wave-2-editors-worldsnapshot-particle-presentation-residuals
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-07
---

# Phase 15 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (managed) — `UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`, `Utinni.Mcp.Tests`; Catch2 (native) — `UtinniCore.Tests` |
| **Config file** | `.csproj` per test project (all present + verified) |
| **Quick run command** | `dotnet test UtinniCoreDotNet.Tests --no-build` (Debug x86) |
| **Full suite command** | VS2026 MSBuild Debug+Release\|x86, then `dotnet test --no-build` across `UtinniCoreDotNet.Tests` + `Utinni.Cli.Tests` + `Utinni.Mcp.Tests`, then run `bin\Release\UtinniCore.Tests.exe` |
| **Estimated runtime** | ~120 s full suite (codec + CLI golden + MCP dispatch + native); ~25 s quick |

**Cross-repo build note (LOCKED):** `dotnet build` CANNOT compile the WinForms/native projects (MSB3823 on `.resx` images). Build with **VS2026 MSBuild** (`Debug+Release|x86`), run xUnit via `dotnet test --no-build`. WinForms-host UI (`FormSnapshotPlacements`, `FormParticleEditor`, `SnapshotPanel` grow) is verified by **MSBuild-green build**, not project-reference tests — the x86 WinForms/native TJT assembly is not project-referenceable from the x86 test project (the established Phase 8-11 precedent). Factor as much logic as possible into testable `UtinniCoreDotNet` helpers.

---

## Sampling Rate

- **After every task commit:** Run `dotnet test UtinniCoreDotNet.Tests --no-build` (codec + framework-leg suite). For CLI tasks also `dotnet test Utinni.Cli.Tests --no-build`; for MCP tasks `dotnet test Utinni.Mcp.Tests --no-build`.
- **After every plan wave:** Full suite (managed three projects + native `UtinniCore.Tests.exe`), Debug+Release\|x86; both repos MSBuild-green.
- **Before `/gsd:verify-work`:** Full suite green + Tier-4 maintainer smoke for the three live-dependent items (PROD-W2-WS/PRT live demo, RESID-03 live-observe, RESID-04 repro).
- **Max feedback latency:** ~25 s (quick) / ~120 s (full).

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 15-01-01 | 01 | 1 | PROD-W2-WS | T-15-05 | Bulk op composes N undo commands atomically; no UI-thread snapshot mutation | unit (framework-leg) | `dotnet test UtinniCoreDotNet.Tests --no-build --filter Snapshot` | ❌ W0 | ⬜ pending |
| 15-01-02 | 01 | 1 | PROD-W2-WS | T-15-05 | Placements table/bulk host builds; selection-sync detach/reattach holds | build + framework-leg | VS2026 MSBuild (UtinniPlugins) + `--filter SnapshotBulk` | ❌ W0 | ⬜ pending |
| 15-02-01 | 02 | 1 | PROD-W2-PRT | T-15-01 | `IffPayloadCursor` bounds-checks every read; truncation → `DecoderException`, never OOB | unit | `dotnet test UtinniCoreDotNet.Tests --no-build --filter ParticleWaveForm` | ❌ W0 | ⬜ pending |
| 15-02-02 | 02 | 1 | PROD-W2-PRT | T-15-01 / T-15-02 | Typed PEFT decode; count guarded before loop (DoS cap) | unit | `--filter ParticleDecode` | ❌ W0 | ⬜ pending |
| 15-02-03 | 02 | 1 | PROD-W2-PRT | T-15-03 | Unrecognized EMTR/PEFT version → raw-preserve sub-form, round-trip byte-exact; never FATAL | unit | `--filter ParticleDegrade` | ❌ W0 | ⬜ pending |
| 15-03-01 | 03 | 1 | PROD-W2-PRT | T-15-05 | Native retrigger export reachable OR documented-absent; heap-free marshalling | build + spike doc | VS2026 MSBuild (UtinniCore) + grep no per-frame alloc | ❌ W0 | ⬜ pending |
| 15-04-01 | 04 | 2 | PROD-W2-PRT | T-15-01 / T-15-02 | `decode-iff` PEFT dispatch + `roundtrip-particle` byte-exact gate; exit-code taxonomy | CLI golden | `dotnet test Utinni.Cli.Tests --no-build --filter Particle` | ❌ W0 | ⬜ pending |
| 15-04-02 | 04 | 2 | PROD-W2-PRT | T-15-04 | MCP read tool resolves under pinned root, dispatches by exit code, zero format logic | integration | `dotnet test Utinni.Mcp.Tests --no-build --filter Particle` | ❌ W0 | ⬜ pending |
| 15-05-01 | 05 | 2 | RESID-04 | T-15-06 | Utinni never calls `pDevice->Reset`; DI cooperative-level suppress is opt-in | native grep-gate + build | Catch2/grep asserts no Utinni-initiated `Reset`; VS2026 MSBuild | ❌ W0 | ⬜ pending |
| 15-06-01 | 06 | 3 | PROD-W2-PRT | T-15-01 | Particle editor Form builds; raw-preserved cells render degrade style; AI button reuses CLI/MCP path | build | VS2026 MSBuild (UtinniPlugins) green | ❌ W0 | ⬜ pending |
| 15-07-01 | 07 | 3 | RESID-03 | — | `.ws`/`.prt`/`.stf`/`.ot` classify tier-(b); badge copy honest (no over-promise) | unit | `dotnet test UtinniCoreDotNet.Tests --no-build --filter ReloadRouting` | ✅ (extend) | ⬜ pending |
| 15-08-01 | 08 | 4 | PROD-W2-WS / PRT / RESID-03 / RESID-04 | T-15-06 | Live-SWG smoke: WS/Particle demo, RESID-03 live-observe, RESID-04 matrix + no-Reset held | manual (Tier-4) | maintainer live session — record in smoke log | n/a | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `UtinniCoreDotNet.Tests/Formats/Particle/ParticleCodecTests.cs` — PROD-W2-PRT typed decode + degrade-don't-abort stubs (created in 15-02)
- [ ] `UtinniCoreDotNet.Tests/Formats/Particle/ParticleFixtureBuilder.cs` — synth minimal `FORM PEFT` via `IffWriter` (no `.prt` fixtures exist today); extract-from-`.tre` is the alternate path
- [ ] `Utinni.Cli.Tests` `roundtrip-particle` golden(s) — byte-exact round-trip (synth or extracted fixture) (created in 15-04)
- [ ] `Utinni.Mcp.Tests` particle read-tool dispatch test (created in 15-04)
- [ ] `UtinniCoreDotNet.Tests/.../SnapshotBulkOpTests.cs` — WorldSnapshot bulk-op command-composition framework-leg helper + test (created in 15-01)
- [ ] Extend `StringTableReloadRoutingTests` (`ReloadAssetClassifier`) to assert the new WS/Particle reload paths classify with honest tier-(b) copy (created in 15-07)
- [ ] Optional native regression gate (Catch2 or grep) asserting Utinni never calls `pDevice->Reset` (created in 15-05)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| WorldSnapshot SubPanel + placements table load inside TJT against a live SWG client; bulk move/delete/retemplate visibly affect placements | PROD-W2-WS | Requires live injected SWGEmu/Restoration client; automation cannot reach (CON-TT-03; Tier-3 mock-D3D9 deferred) | Inject TJT, load a snapshot, open `Placements…`, multi-select, bulk move/delete/retemplate, confirm in-world |
| Particle editor opens a `.prt`, edits a typed field, saves, and (injected) the `Preview in client` hot-retrigger visibly refreshes the live effect | PROD-W2-PRT | Live injected client + a real `.prt` asset; visual judgment | Open a `.prt`, edit emitter/color, save loose-override, click `Preview in client`, confirm the live effect changes |
| SC3 live render-on-reload for `.stf`/`.ot`; confirm which reload path actually refreshes vs relog-only | RESID-03 | Live render judgment; cannot automate without mock-D3D9 | Live session: save `.stf`/`.ot` edit, trigger scene change, observe whether the edit renders; record honest candor outcome |
| RESID-04 window-resize / windowed↔fullscreen edge-case matrix; confirm the suppress fix keeps SWG windowed-embedded; no Utinni `Reset` | RESID-04 | Live D3D9 presentation behavior; GPU/driver-specific; Tier-4 | Live session: walk the edge-case matrix (windowed→fullscreen, login→world, chat-open Enter, maximize/restore, minimize/restore, free resize, multi-cycle, alt-tab); read `direct_input.cpp` DISCL log to confirm trigger; confirm embed stays attached after fix |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (live-only items routed to Tier-4 manual table)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-06-07
