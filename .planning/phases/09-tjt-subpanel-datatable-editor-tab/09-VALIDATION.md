---
phase: 9
slug: tjt-subpanel-datatable-editor-tab
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-29
---

# Phase 9 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> **Derived from** `09-RESEARCH.md` § Validation Architecture (research date 2026-05-29).
> Per-task entries are filled by the planner during PLAN.md authoring; this doc
> defines the framework, sampling cadence, Wave 0 gaps, and the manual-only
> residue ahead of plan creation so the planner can map tasks against a fixed
> contract.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Managed framework** | xUnit 2.x (`UtinniCoreDotNet.Tests/UtinniCoreDotNet.Tests.csproj` — SDK-style, auto-globs `**/*.cs`) |
| **CLI golden harness** | xUnit 2.x in `Utinni.Cli.Tests/Utinni.Cli.Tests.csproj` (reuses `Infrastructure/GoldenTestRunner.cs` + `Infrastructure/InProcessCliRunner.cs` from Phase 8 `RoundtripIffCommandTests`) |
| **Native framework** | Catch2 v3 (NOT consumed in Phase 9 — pure managed) |
| **Preservation grep gates** | xUnit fail-on-violation Facts in `UtinniCoreDotNet.Tests/PreservationAudit/` (Phase 6 STAB-04 pattern) |
| **Config file** | None (xUnit auto-discovers) |
| **Build tool** | VS 2026 MSBuild (mandatory — `dotnet build` fails on WinForms image .resx per `feedback_dotnet_build_msbuild_resources`); `dotnet test --no-build` is the run command |
| **Quick run command** | `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~Datatable"` |
| **Full suite command** | `dotnet test UtinniCoreDotNet.Tests --no-build && dotnet test Utinni.Cli.Tests --no-build && dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~PreservationAudit"` |
| **Estimated runtime** | quick: < 5 s steady-state; full: ~30–60 s across all three suites Debug\|x86 |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test UtinniCoreDotNet.Tests --no-build --filter "FullyQualifiedName~Datatable"` (Datatable subsuite, target < 5 s)
- **After every plan wave:** Run full suite (UtinniCoreDotNet.Tests + Utinni.Cli.Tests + PreservationAudit) BOTH `Debug|x86` and `Release|x86` MSBuild clean across both repos (Utinni + UtinniPlugins)
- **Before `/gsd:verify-work`:** Full suite green AND `roundtrip-tab` golden green AND `/gsd:code-review 09` cross-AI gate AND maintainer-driven Tier-4 live-SWG smoke per Phase 8 precedent (smoke=automation-augmented; live ACK deferred-but-acceptable)
- **Max feedback latency:** < 5 s for quick subsuite; ≤ 60 s for full suite

---

## Per-Task Verification Map

> **Filled by the planner during PLAN.md authoring.** Each plan task with
> `<automated>` verify must back-link to this table; tasks that defer to Wave 0
> infrastructure must reference the gap row below.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD     | TBD  | TBD  | PROD-W1-DT  | TBD        | TBD             | TBD       | TBD               | ❌ W0 / ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*
*Populated after `/gsd:plan-phase` planner spawn; do not invent rows here.*

---

## Wave 0 Requirements

These files do not yet exist and must be added before later waves can sample
them. The plan that introduces each file is the "Wave 0" owner for that file;
downstream plans depend on it via `depends_on`.

**Framework primitives (`UtinniCoreDotNet/Formats/Datatable/` — NEW):**
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableColumnTypeTests.cs` — per-discriminator parse + `MangleValue` per `DT_*`. ~25–30 [Fact]s.
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableHashCrcTests.cs` — CRC parity vs reference values. ~4–6 [Fact]s.
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableDocumentTests.cs` — V0 + V1 fixtures; DT_Comment skip; null-cell defaults; cell-count mismatch error; per-DT_* read. ~15–20 [Fact]s.
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DataTableWriterTests.cs` — round-trip byte-exact (no edits); per-DT_* serialize; chunk-length roll-up; over-cap chunk rejection (via Phase 8 IffWriter inheritance). ~15–20 [Fact]s.
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DatatableEditControllerTests.cs` — 10+ commands × apply/undo/redo identity; baseline-clean dirty; single-transaction CSV import; type-change cascade flags needs-review; save-blocked while needs-review > 0. ~30–40 [Fact]s (largest test file in the phase).

**CLI round-trip gate (the structural SC4 enforcer):**
- [ ] `Utinni.Cli/Commands/RoundtripTabCommand.cs` — `roundtrip-tab` verb (parse → optional `--mutate-cell row col hex` or `--mutate-cell-typed row col value` → serialize → re-parse → byte-exact-untouched-cells assertion). Mirrors `RoundtripIffCommand`.
- [ ] `Utinni.Cli.Tests/Commands/RoundtripTabCommandTests.cs` — golden suite against `DataTableFixtureBuilder`-built `.tab` files. ~10–15 [Fact]s.

**Test fixtures (synthetic; no on-disk `.tab` checked in per Open Question 3 / Assumption A6):**
- [ ] `Utinni.Cli.Tests/Infrastructure/DataTableFixtureBuilder.cs` — emits valid DTII bytes for V0 minimal / V1 minimal / V1 all-types / V1 with-defaults-and-enums / V1 with-comment / V1 CombatDataTable-like (~200 × ~30) / V1 empty. Mirrors `IffBuilder.cs` + `TreFixtureBuilder.cs`.
- [ ] `UtinniCoreDotNet.Tests/FormatsTests/Datatable/DatatableFixtures.cs` — managed-side wrapper if cross-project visibility prevents direct `Utinni.Cli.Tests` reuse (planner picks).

**TJT-side host + save plumbing (no new infrastructure — REUSES Phase 8):**
- [ ] `TheJawaToolboxDotNet/Saving/DatatableCsvSerializer.cs` — preferred placement per checker B-1 pattern is to extract the per-cell coercion check into a framework helper and test there (mirrors `LooseOverridePath` / `LivePatchValidator` posture). Planner decides.

**Framework install:** N/A — pre-existing (xUnit 2.x, CommandLineParser, Newtonsoft.Json already on disk; CI green since Phase 4/8).

---

## Manual-Only Verifications

All four below are **Tier-4 maintainer-driven live-SWG smokes** with no automated
substitute. Each maps to Phase 8 precedent (smoke=automation-augmented; live ACK
deferred-but-acceptable for V1 sign-off).

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Datatable Editor subpanel loads inside TJT against live SWG | PROD-W1-DT(1) | Requires live SWG client + injection harness — no in-process surrogate | Launch SWG with Utinni → open TJT → confirm Datatable Editor menu item registered → `Open…` a real `.tab` from a TRE archive → editor shows rows/columns |
| Live SWG client picks up `.tab` edit on next scene change | PROD-W1-DT(3) / CF-05 | No in-session reload hook exists for datatables (confirmed by `DataTableManager.h:25-30` lack of invalidation); only path is "save → TJT chat-command scene change → re-resolve" | Open `.tab` → edit cell → save (mode 1 loose override OR mode 2 Save-As OR mode 4 repack) → run TJT `loadScene <name>` chat command → observe the edited datatable value in-game |
| `.tre` repack save target round-trip on real SWGEmu / Restoration archive | PROD-W1-DT(4) / CF-03 mode 4 | TreRepackLock arbitration needs a real live archive race (Phase 8 WR-06 V6000 reject covered automated) | Open `.tab` from packed `.tre` → edit → Save▾ ▸ Repack → confirm modal → observe archive replaced + backup created + lock honored if SWG holds the archive |
| Singleton-form hide-not-dispose smoke (FormDatatableEditor second-open) | Pitfall 5 — Phase 8 smoke-discovered defect class | xUnit can assert `OnFormClosing(UserClosing) → !IsDisposed && !Visible` but cannot replicate the full MEF re-open lifecycle | Open FormDatatableEditor → close via X → re-open from TRE Browser / IFF Editor hand-off → confirm no `ObjectDisposedException` and editor state restores |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 5 s for quick subsuite
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
