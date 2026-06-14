# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v2.0 — AI-Assisted SWG Tools

**Shipped:** 2026-06-14
**Phases:** 5 (12–16) | **Plans:** 39 | **Commits:** 202

### What Was Built
- 3 revived SOE build CLIs (`TreeFileBuilder`/`TemplateCompiler`/`TemplateDefinitionCompiler`) building standalone at v145 in a Utinni-owned `tools/` tree, CI-enforced.
- 17 `utinni-cli` BUILD/EDIT verbs wrapping the revived compilers + the byte-exact writers; OT Tier-2 typed display closed.
- Headless net10 `Utinni.Mcp` stdio MCP server (11 tools, zero format logic) with a 5-layer safety model + `MCP-SECURITY.md`.
- Two Wave-2 DCC-style TJT SubPanels (WorldSnapshot + Particle/`.prt`) and the RESID-04 fullscreen embed fix.
- Live-injected MCP bridge (named-pipe IPC, SDK out-of-proc) + the documented Utinni ↔ swg-blender-plugin file-format contract.

### What Worked
- **Hard-gate-first sequencing.** Putting the revive-feasibility spike (AUTH-01) as a literal hard gate before any AUTH dependency meant the empirical "does it actually build at v145" risk was retired before 39 plans of downstream work committed to it.
- **Verbs-first, MCP-as-thin-dispatcher.** Every capability landed as a golden-tested `utinni-cli` verb first; the MCP server stayed a pure subprocess dispatcher with zero business logic — which made the security surface auditable and the format codecs single-sourced.
- **Adversarial cross-AI plan review caught a real persistence bug.** Both reviewers independently flagged the `roundtrip→save` two-step as a no-op that re-serialized the UNCHANGED file; the fix (atomic `apply-save-*` verbs persisting the verified mutated bytes) was a scoped, *named* exception rather than a silent one.
- **Honest-degradation discipline.** Where a native hook wasn't reachable (Particle live-preview, SC3 render-on-reload), the editors shipped honest "reloads on next scene change / relog" candor instead of over-promising — and the limit was tracked, not hidden.

### What Was Inefficient
- **Phase 15 took 21 plans** (7 core + 14 defect-driven gap-closure across 3 live-smoke rounds). The Wave-2 editors' injection-time defects (AssemblyResolve, netstandard.dll, fullscreen embed watchdog, CLI inject-root probe) only surfaced under a real injected session — the automated tiers couldn't reach them, so each round was find→fix→reassemble→re-smoke.
- **v1.0 was never formally closed.** It shipped + tagged but skipped `/gsd:complete-milestone`, so this close had to retroactively reconstruct the v1.0 archives and reconcile a REQUIREMENTS.md that intermingled both milestones — bookkeeping debt that compounded.
- **Bookkeeping lag.** The requirements traceability table marked implemented reqs `Pending`/`[ ]` for the whole milestone; the audit had to do a 3-source cross-reference to establish true status before close.

### Patterns Established
- **Lift-and-shift for reviving third-party build tools** — copy source + shared libs into a repo-owned `tools/` tree at the shared toolset, pin an exact upstream SHA (not a branch HEAD), never `#include`/ProjectReference into the live upstream tree. Keeps your build decoupled from upstream churn (here, an active D3D9→D3D11 migration).
- **Separate-process headless-first integration boundary** — host the modern-.NET SDK out-of-proc and cross to the legacy x86 in-proc client only via a narrow named pipe; the pipe IS the architecture boundary.
- **Scoped, named exceptions to a guard-rail** — when a constraint ("Phase 14 adds zero verbs") must break, break it explicitly with a one-line rationale and a proving test, never silently.
- **Cross-TFM byte-exact JSON needs a hand-rolled canonical writer** + golden byte-vectors; serializer defaults don't byte-match across net10/net472.

### Key Lessons
1. **Injection-time defects need a real session in the loop early.** Budget gap-closure rounds for any feature that runs under live injection; the automated tiers (1–3) structurally cannot reach AssemblyResolve / window-management / inject-root path bugs.
2. **Close every milestone through the workflow, even retroactive ones.** Skipping `/gsd:complete-milestone` for v1.0 left a tag with no archive and a requirements file that drifted across two milestones — pay this immediately, not at the next close.
3. **"Revive" is REAL porting, not a clean lift.** Even at a shared toolset, expect engine-API drift, C++20/SAFESEH/CRT-compat friction. Treat "is configured at v145" and "actually builds + links" as different facts resolved by a build pass, not by reading.
4. **Verbs-first keeps the AI-integration surface small and auditable.** The MCP layer's value came from owning ZERO format logic.

### Cost Observations
- Model mix: opus-heavy (all GSD researcher/planner/executor/verifier roles pinned to opus via config; main-loop opus).
- Notable: 14 of Phase 15's 21 plans were gap-closure — the live-smoke rounds dominated the milestone's plan count without being in the original roadmap.

---

## Milestone: v1.0 — MVP "Demo + CI green"

**Shipped:** 2026-06-01 (`v1.0.0`)
**Phases:** 11 (+ 02.1) | **Plans:** 55 | GSD planning bootstrapped 2026-05-16

> Retrospective written retroactively at the v2.0 close from STATE.md + phase artifacts.

### What Was Built
- A stabilised injected-DLL framework: all 15 critical bugs closed, 8 strategic reworks landed, toolchain modernised (vcpkg, v145, DXSDK retired).
- A 4-tier test harness (Tier-1 C#/C++, Tier-2 `utinni-cli` goldens) gating master on a self-hosted runner.
- Five Wave-1 editors shipped as Jawa Toolbox subpanels (TRE Browser, IFF, Datatable, String-table, Object Template), each byte-exact and demoed live.

### What Worked
- **Foundations-before-features.** CI + the test harness landed first as the smallest durability unlock; every later phase gated on a green pipeline, so regressions surfaced at the commit that caused them.
- **Shared format primitives in one assembly.** IFF read/write shipped in `UtinniCoreDotNet/Formats/Iff` (Phase 8) and Phases 9–11 consumed it directly — no inter-plugin coupling, no versioning surface.
- **Singleton hide-not-dispose** emerged as the canonical pattern for MEF-registered editor forms after the Phase 8 smoke; Phases 9–11 applied it from the start.

### What Was Inefficient
- **Native + injected verification is maintainer-gated.** Tier-4 (real-SWG-injection smoke, visual judgment) couldn't be automated; live defects (e.g. scene-change AV, fullscreen detach) needed live sessions and bisects.
- **Generated `UtinniCore.cs` regen churn** produced huge symmetric no-op diffs on every build — a standing `git checkout --` discipline rather than a fix.

### Patterns Established
- **Tiered, pragmatic testing (DEC-C3 LOCKED)** — TDD for pure-logic + file-format layers only; native detours + WinForms UI use smoke/integration (Tiers 2–4).
- **Subpanels-inside-TJT (DEC-C4)** for the V1 editor set — distribution granularity is "Utinni + TJT as a pair," avoiding a cross-plugin IFF versioning surface.
- **De-flake inside CI-covered code, never the injection hot path** (loader-lock harness best-of-3 min).

### Key Lessons
1. **Anti-goals are a scope filter, not a footnote.** The four locked DEC-A* anti-goals (no server manager / launcher / DCC / cheat) kept Wave-1 focused on the editor zoo Utinni actually replaces.
2. **Detour-library correctness is load-bearing.** The DetourXS explicit-length trap and CRT-vs-SWG fingerprint lessons each saved multi-day investigations once captured.

### Cost Observations
- Opus-heavy GSD role config established here and carried into v2.0.

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Phases | Plans | Key Change |
|-----------|--------|-------|------------|
| v1.0 | 11 (+02.1) | 55 | Foundations-before-features; 4-tier harness + self-hosted v145 CI established; DEC-C3/C4 locked. |
| v2.0 | 5 | 39 | Hard-gate-first sequencing; verbs-first AI integration; lift-and-shift revive; live-smoke gap-closure rounds dominate editor phases. |

### Cumulative Quality

| Milestone | Test Posture | Notable |
|-----------|--------------|---------|
| v1.0 | Tiers 1–2 green on master; Tier-4 manual residual documented | Byte-exact writers for all 5 Wave-1 formats |
| v2.0 | + net10 MCP lane; integration PASS 5/5 seams; Nyquist 5/5 | Byte-exact cross-TFM JSON goldens; MCP-SECURITY 17-threat register |

### Top Lessons (Verified Across Milestones)

1. **Injection/Tier-4 defects need a maintainer-in-the-loop live session** — true in both milestones; budget for it, don't assume automation reaches it.
2. **Close every milestone through the workflow** — v1.0's skipped close created reconciliation debt paid at the v2.0 close.
3. **Single-source the format codecs; keep consumers thin** — shared IFF primitives (v1.0) and the verbs-first MCP dispatcher (v2.0) are the same lesson at two layers.
