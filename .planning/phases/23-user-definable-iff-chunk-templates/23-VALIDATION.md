---
phase: 23
slug: user-definable-iff-chunk-templates
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-20
---

# Phase 23 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from `23-RESEARCH.md` → "Validation Architecture". The byte-exact round-trip
> gate (DEC-C3) is validated through **synthesize-through-the-writer** fixtures
> (canonical-by-construction) across BOTH SWGEmu and SWG Infinity version-FORM lineages,
> mirroring the proven Phase 20/22 fixture idiom.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (net472 `UtinniCoreDotNet.Tests`, `Utinni.Cli.Tests`; net10 `Utinni.Mcp.Tests`) |
| **Config file** | none — xUnit auto-discovers; build the solution with MSBuild first |
| **Quick run command** | `dotnet test Utinni.Cli.Tests --no-build --filter "FullyQualifiedName~Template"` |
| **Full suite command** | MSBuild `Utinni.sln /p:Configuration=Release /p:Platform=x86` then `dotnet test --no-build` (per AGENTS.md: never `dotnet build` — MSB3823 on WinForms .resx) |
| **Estimated runtime** | quick ~30s; full suite per existing lanes |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test Utinni.Cli.Tests --no-build --filter "FullyQualifiedName~Template"` (sub-30s)
- **After every plan wave:** Full `dotnet test --no-build` (after an MSBuild build)
- **Before `/gsd:verify-work`:** Full suite green — the count-recompute + worked-example goldens are the DEC-C3 byte-exact gate
- **Max feedback latency:** ~30 seconds (quick run)

---

## Per-Task Verification Map

> Filled at plan time per task. Requirement → behavior map below is the source of truth the
> planner must cover. Every row is currently a Wave-0 gap (❌ W0) — no Template tests exist yet.

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| PROD-IFFT-01 | Kernel decodes every type (ints/f32/f64/cstring/char[n]/raw/pad/struct/3 array kinds) + presets | unit | `dotnet test Utinni.Cli.Tests --no-build --filter "Template&KernelCodec"` | ❌ W0 | ⬜ pending |
| PROD-IFFT-01 | D-09 presets decode to exact values (vector x,y,z; quat w,x,y,z; matrix 3×4 row-major; 3 color forms) | unit | `...--filter "Template&Preset"` | ❌ W0 | ⬜ pending |
| PROD-IFFT-02 | **count-from-prior array grow/shrink round-trips byte-exact + count field recomputed** (CRITICAL) | golden | `...--filter "Template&CountRecompute&Roundtrip"` | ❌ W0 | ⬜ pending |
| PROD-IFFT-02 | trailing-remainder + fixed-count arrays round-trip byte-exact | golden | `...--filter "Template&Array&Roundtrip"` | ❌ W0 | ⬜ pending |
| PROD-IFFT-02 | version-FORM-aware match (CLEF-CPAP-style 3-layout) picks the right layout | unit | `...--filter "Template&VersionMatch"` | ❌ W0 | ⬜ pending |
| PROD-IFFT-02 | D-05 altitude: a template never engages on a built-in root-FORM file | unit | `...--filter "Template&Precedence"` | ❌ W0 | ⬜ pending |
| PROD-IFFT-02 | `apply-save-template` fails closed on a failed untouched-leaf verify (no write) | integration | `...--filter "ApplySaveTemplate&FailClosed"` | ❌ W0 | ⬜ pending |
| PROD-IFFT-03 | D-14 worked-example chunk templates round-trip byte-exact (double as goldens) | golden | `...--filter "Template&WorkedExample&Roundtrip"` | ❌ W0 | ⬜ pending |
| PROD-IFFT-03 | MCP thin tool shells decode-with-template, zero format logic | integration | `dotnet test Utinni.Mcp.Tests --no-build --filter "Template"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Utinni.Cli.Tests/Template/TemplateTestFixtures.cs` — synthesize-through-`IffWriter` fixtures
  (clone `ClefTestFixtures.cs` idiom: `MutableIffNode.NewContainer` + `AddContainer` + `AddLeaf` with
  kernel-encoded payloads). Covers PROD-IFFT-01/02/03.
- [ ] `Utinni.Cli.Tests/Template/KernelCodecTests.cs` — per-type decode/encode + the count-recompute
  golden (the CRITICAL test).
- [ ] `Utinni.Cli.Tests/Template/RoundtripTemplateCommandTests.cs` — verb-level byte-exact gate.
- [ ] `Utinni.Cli.Tests/Template/ApplySaveTemplateTests.cs` — fail-closed + atomic write.
- [ ] `Utinni.Mcp.Tests/Template/...` — thin-tool shell-out shape.
- [ ] Worked-example template JSON files (D-14) committed under the shipped pack dir + referenced as fixtures.
- Framework install: none — xUnit already present in all three test projects.

### Dual-lineage fixture matrix (DEC-C3)

The two real lineages are **SWGEmu** and **SWG Infinity** (Restoration excluded — proprietary TRE
encryption makes payloads unreachable; v6000+ is enumerate-only). For templates the lineage axis is
**version-FORM divergence**, not encryption: build each worked-example fixture at a low ("SWGEmu-era")
AND a high ("Infinity-era") version FORM where they differ; where versions are identical, document
"no observed lineage drift" and keep one fixture. The CLEF CPAP-style 3-layout case is the canonical
version-divergence exemplar to mirror.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Template create / edit / save / select from the IFF Editor UI | PROD-IFFT-03 | WinForms SubPanel UI; no headless UIA harness in CI | Drive TJT IFF Editor via the live-smoke path (windows-mcp): create a template, apply to a hex chunk, edit a field, save, confirm byte-exact re-encode |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 30s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-06-20 (plan-checker gate pass)
