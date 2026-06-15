# Utinni

## What This Is

Utinni is an in-process modding tool and framework for Star Wars Galaxies (SWG): a 32-bit native UtinniCore DLL injected into a live `SWG.exe`, a .NET Framework WinForms editor host, and an MEF-discovered C# plugin pipeline. As of v2.0 it is a one-stop modding tool that **sees, edits, authors, and AI-drives** the SWG asset pipeline — replacing the ~30 separate SOE-era apps SWG modders juggle. v1.0 shipped the stabilised framework + five Wave-1 editors (TRE Browser, IFF, Datatable, String-table, Object Template) as Jawa Toolbox subpanels; v2.0 added the revived SOE build/compile CLIs wrapped as `utinni-cli` verbs, a headless net10 MCP server (so AI agents can read/edit/save the pipeline), the first Wave-2 DCC-style editors (WorldSnapshot, Particle), a live-injected MCP bridge, and the formalized Blender file-format boundary.

## Core Value

A modder downloads Utinni, installs once, and from a single application can see, edit, and live-preview every asset the SWG client loads — replacing the fragmented 15-year-old editor zoo with one stable, plugin-driven tool.

## Sovereign-Fork Stance

Both `Utinni` and `UtinniPlugins` are MIT forks of `ptklatt/Utinni` and `ptklatt/UtinniPlugins`. Upstream appears dormant. This fork advances independently; clean fixes may be offered upstream as PRs but the fork is not gated on upstream acceptance. Composes with SWG-Source conventions where server-side touch points exist (which V1 explicitly avoids — see anti-goals).

## Current State

**Shipped:** v2.0 "AI-Assisted SWG Tools" (Phases 12–16) on 2026-06-14 (`v2.0`), building on v1.0 MVP
(Phases 1–11) shipped 2026-06-01 (`v1.0.0`). 16 phases (+02.1), 94 plans, 31 requirements (15 v1 +
16 v2.0), all satisfied. The v2.0 milestone audit: 16/16 requirements satisfied, integration PASS (5/5
seams WIRED, 2/2 E2E flows COMPLETE), Nyquist 5/5 compliant. See `.planning/MILESTONES.md` +
`.planning/milestones/`.

**What works today:** the injected framework + 4-tier test harness; five Wave-1 editors (TRE/IFF/
Datatable/String-table/Object-Template) as TJT subpanels; the revived `TreeFileBuilder`/`TemplateCompiler`/
`TemplateDefinitionCompiler` + item exporters wrapped as 17 `utinni-cli` BUILD/EDIT verbs; the headless
net10 `Utinni.Mcp` server (11 tools, loose-override-default writes, byte-exact verify-before-commit,
fail-closed `resolvedRoot`, `MCP-SECURITY.md`); two Wave-2 editors (WorldSnapshot, Particle/`.prt`); the
named-pipe live-injected MCP bridge; and the documented Utinni ↔ swg-blender-plugin contract.

**Next milestone:** v2.1 "Wave-2 Editors + Foundation Hardening" — started 2026-06-14 (Phases 17+).
See the Current Milestone section below.

**v2.1 progress:** Phase 17 (CppSharp / v145 hardening) complete 2026-06-15 — CPPS-01..04 satisfied:
the clang-capability spike + supported-config docs (the 14.29 parser-include redirect is now the
documented, accepted config), two asymmetric CI tripwires (C++23-STL-header hard-fail scan + clang-20
pin warn-loud), and the CPPS-04 ABI gate (per-block-hash diff with `--rebless` + a frozen-TJT
MEF-compose fixture) — a binding regen can no longer silently break a pre-built plugin DLL. Verified
7/7 via green CI run `8cc05b4`.

**Phase 18 (Render-Backend Seam + Dx9Backend) complete 2026-06-15 — RNDR-01 satisfied:** the ImGui
overlay now renders through a single 10-member `IRenderBackend` ABC seam (`UtinniCore/swg/graphics/
render_backend.{h,cpp}` + a DX9-bearing `render_backend_dx9.cpp`, an Option-A two-TU split that keeps
the seam zero-`UTINNI_API` per CPPS-04 while staying unit-testable device-free). `imgui_impl.{cpp,h}`
are fully DX9-API-neutral (D-06 source gate, hardened post-review to be string-literal-aware + cover the
full reach-in token set) and single-source the ~1000-line overlay logic; the no-Reset / Present-stretch
D3D9 contract is preserved verbatim. Verified 4/4 must-haves + D-08 maintainer live-smoke (approved); a
post-review pass hardened four non-blocking warnings (WR-01..04). Native Catch2 suite 151/29. The seam
is the foundation Phase 19's Dx11Backend twin plugs into without forking the overlay. Next: Phase 19
(Dx11Backend + config detection + DXGI resize). Pre-existing window-resize/fullscreen edge cases stay
tracked as an open todo (a pure refactor cannot regress them).

<details>
<summary>v2.0 milestone goal + target features (shipped — archived for reference)</summary>

**Goal:** *Utinni authors, not just edits* — close the compile/build gaps and ship an MCP server so AI agents can drive the full SWG asset pipeline, then land the first DCC-style editors.

**Target features:**
- **AI / MCP integration** — an MCP server exposing Utinni's verified, byte-exact pipeline (read+edit+save TRE/IFF/datatable/string-table/object-template) as agent tools, built as a thin shim over the existing `Utinni.Cli` JSON verbs / `UtinniCoreDotNet`. *The centerpiece.*
- **Authoring & compile pipeline (revive+wrap)** — get the original build-chain CLIs compiling and wrap them: `TemplateCompiler`/`TemplateDefinitionCompiler` (`.tpf`/`.tpd` → `.iff`, which also yields the per-class param→type map the Object Template Editor's Tier-2 typed display needs), `TreeFileBuilder` (build-from-source `.tre`; Utinni only repacks today), the `DataTableTool` compile path, item exporters.
- **New asset editors (replace)** — first DCC-style Utinni SubPanels: Terrain, Particle/Effects; extend the Snapshot panel into a WorldSnapshot/object-placement editor; Animation live-in-client preview (coordinated with the Blender suite).
- **Ecosystem integration** — formalize the Utinni ↔ `swg-blender-plugin` boundary (Utinni opens/previews what Blender exports; Utinni stays out of 3D mesh/skel/anim authoring).
- **Carried residuals** — Object Template Tier-2 typed list-param display, the intro-skip scene-transition crash (VEH logger deployed), the SC3 live-reload residual, and the SWG window-resize/fullscreen edge cases.

**Strategy:** revive+wrap (cheap, high-leverage) before replace (the meatier editors). The revived compilers double as MCP write tools and unblock OT Tier-2. See `docs/ai/toolchain-inventory.md` for the full tool cross-walk and revive/replace rationale.

**Lift-and-shift constraint (locked):** when reviving a tool, **copy its source + required shared libs into a Utinni-owned build location** — do NOT build in-place against the `swg-client-v2` tree or modify it. `swg-client-v2` has an **active D3D9→D3D11 migration** in progress; lift-and-shift keeps our build decoupled from that churn and stays out of their way. (The revive targets — `TemplateCompiler`, `TreeFileBuilder`, exporters — are mostly headless/console and don't need the renderer, which makes lift-and-shift clean.)

**Toolchain (v145 — shared target, CONFIRMED by v2.0 research):** `swg-client-v2` is **already on VS2026 / v145 / stdcpp20** (its `swg.sln` is `VisualStudioVersion = 18.1`; the revive targets' `.vcxproj` are `PlatformToolset=v145`; STLport453 is gone from disk). So there is **no v143→v145 port** — we share Utinni's exact toolset. The revive spike therefore collapses to: **verify the lifted-out tool compiles + links standalone at v145**, **strip dead deps** (e.g. `TemplateCompiler`/`sharedTemplate` carry a dead `perforce/include` path never `#included`), and **produce a per-tool dependency manifest**. Status is **uneven** — `TemplateCompiler.vcxproj` already has built v145 objects on disk; `TreeFileBuilder` is `v145`-configured but has **no build output** (unverified) — do not assume uniform. (The earlier "borrow + port the delta" framing was an overstatement, corrected here per `.planning/research/SUMMARY.md`.)

**Prior milestone — V1 "Demo + CI green" — SHIPPED 2026-06-01 (`v1.0.0`):** all 15 critical bugs closed; R-A..R-H landed; Tier 1+2 CI green; all five Wave-1 subpanels (TRE Browser, IFF Editor, Datatable, String-table, Object Template) demoed end-to-end against the live SWG client.

</details>

## Current Milestone: v2.1 "Wave-2 Editors + Foundation Hardening"

**Goal:** Ship the highest-demand Wave-2 DCC-style editor (Terrain) plus one adjacent editor, on a
hardened rendering/toolchain base — so the live-preview editors survive SWG Source's D3D9→D3D11 flip and
the v145 toolset bump is real (a native CppSharp upgrade), not a parser-include redirect.

**Target features:**
- **Terrain editor** (`.trn`) — Wave-2 #1 by modder demand, the milestone headline (`PROD-W2-TRN`).
- **One adjacent Wave-2 editor** — Effects family next (ClientEffect/Lightning/Swoosh; Particle already
  shipped in v2.0), exact target confirmed at requirements scoping.
- **D3D11 render-path foundation** — a parallel hook/overlay path alongside the existing D3D9 one so the
  ImGui overlay + live preview keep rendering when the SWG client runs D3D11 (from Backlog 999.5).
- **v145 toolset bump completion** — finish the real CppSharp upgrade and retire the VS2019-14.29-STL
  parser-include redirect (D-09, from Backlog 999.4).
- **Optional quick wins** — user-definable IFF chunk templates (999.2), TRE override/version-history view
  (999.3).

**Strategy:** Foundation-before-features — land the enabling debt (D3D11 path, v145 bump) early so the
new live-preview editors build on a stable base. Prioritization source: `docs/ai/toolchain-inventory.md`
(SWG toolchain cross-walk + 2026-06-02 SIE comparison). Deferred to v2.2+: remaining Wave-2 editors
(Animation, Shaders/Textures, Sound, UI) and the Maya-write boundary re-eval (999.6, re-opens DEC-A3).
The v2.0 locks (DEC-V2-LIFT-SHIFT, DEC-V2-MCP-OOP, DEC-V2-VERBS-FIRST) stay in force.

## Requirements

### Validated

<!-- Shipped and confirmed valuable. -->

- [x] **TEST-01 (Tier 1 C# scaffold + CI green on master)** — Validated in Phase 1: GitHub Actions `windows-2022` workflow builds `Utinni.sln /p:Configuration=Release /p:Platform=x86` and runs `dotnet test` on every push/PR to master; CON-T-01 post-build chain (`xcopy data\` + `UtinniCoreDotNetGen.exe`) confirmed firing under CI; xUnit smoke project `UtinniCoreDotNet.Tests` discovers and runs `HotkeyTests.cs`; test-the-tester procedure exercised on throwaway PR proving red-on-failure surfaces and master badge stays green. Foundation enabling every subsequent phase's "gate on green pipeline" contract.
- [x] **TEST-03 (Tier 2 CLI shim with golden fixtures)** — Validated in Phase 4 (2026-05-23): `utinni-cli.exe` ships four verbs (`parse-tre`, `list-objects`, `inspect-iff`, `validate-plugin`) producing stable sorted-key JSON envelopes (top-level `schemaVersion`/`command` per REVIEWS HIGH-6); 50 `Utinni.Cli.Tests` Tier-2 goldens + 25 new Tier-1 parser tests (10 TRE + 15 IFF) in `UtinniCoreDotNet.Tests`; CI extended with second `dotnet test` lane (`.github/workflows/ci.yml:94-108`); 18 synthesized fixtures (5 dispatch + 5 TRE + 5 IFF + 4 plugin + 1 ws.iff, all ≤200 bytes per D-03); 4-cycle adversarial cross-AI plan review (Codex + Cursor PASS at iter-4). Closes CON-O-09 (fixture storage = in-repo synth, no LFS), CON-O-10, CON-O-11 in `docs/ai/assessment.md`. DEC-C3 (tiered testing strategy) promoted from Candidate to LOCKED ✓.
- [x] **PROD-W1-IFF (Wave-1 plugin: IFF Editor, read + write)** — Validated in Phase 8 (2026-05-29): the IFF Editor ships as a TJT subpanel (`FormIffEditor`) hosting a shared `IffChunkTree` UserControl (08-03), an `IffEditController` with 10 structural-op commands + editor-local undo/redo (08-04), and the full four-tier D-05 save matrix. Framework primitives (`IffWriter`, `MutableIffDocument`, `OpenSource`, `TreWriter`, `LooseOverridePath`, `ReloadAssetClassifier`, …) live in `UtinniCoreDotNet/Formats` and are consumed by direct ProjectReference. CLI `roundtrip-iff` + 11 goldens; 331 UtinniCoreDotNet.Tests + 123 Utinni.Cli.Tests green.
- [x] **v1.0 MVP — all 15 v1 requirements (STAB-01..05, TEST-01..04, PROD-W1-TRE/IFF/DT/STF/OT, PROD-01/02)** — Validated; shipped `v1.0.0` 2026-06-01. Full outcomes in `milestones/v1.0-REQUIREMENTS.md`.
- [x] **v2.0 AI/MCP — MCP-01/02/03** — Validated (Phases 14, 16): headless net10 `Utinni.Mcp` read+edit+save server (loose-override default, byte-exact verify-before-commit, fail-closed root, `MCP-SECURITY.md`) + named-pipe live-injected bridge.
- [x] **v2.0 Authoring — AUTH-01..06** — Validated (Phases 12–13): revived `TreeFileBuilder`/`TemplateCompiler`/`TemplateDefinitionCompiler` + item exporters at v145 in `tools/`, wrapped as `compile-*`/`build-*`/`apply-save-*` `utinni-cli` verbs. (AUTH-03/06 byte-exact *success* goldens = documented A1 gate-findings.)
- [x] **v2.0 Wave-2 editors — PROD-W2-WS, PROD-W2-PRT** — Validated (Phase 15): WorldSnapshot/object-placement + Particle/`.prt` SubPanels. (Particle live-preview hook honestly degraded.)
- [x] **v2.0 Ecosystem — ECO-01** — Validated (Phase 16): documented Utinni ↔ swg-blender-plugin file-format / `.rsp` contract + `validate-bundle` verb + cross-validated goldens.
- [x] **v2.0 Residuals — RESID-01..04** — Validated (Phases 12, 13, 15): OT typed display, intro-skip crash (no-repro), SC3 live-reload candor (save-tier; live render deferred), window-resize/fullscreen (C3 PASS).

### Active

<!-- Next milestone (v2.1+) scope — define via /gsd:new-milestone. REQUIREMENTS.md was archived at the v2.0 close; a fresh one is created with the next milestone. -->

v2.1 "Wave-2 Editors + Foundation Hardening" requirements are being defined in `REQUIREMENTS.md` (this
`/gsd:new-milestone` run). Scope: Terrain editor (`PROD-W2-TRN`) + one adjacent Wave-2 editor, on a
hardened base (D3D11 render-path foundation + v145 toolset bump completion), with optional quick wins
(IFF chunk templates, TRE override-history). See the Current Milestone section above and `REQUIREMENTS.md`.

### Out of Scope (V1)

<!-- Either deferred to V2 or permanently excluded. -->

- **Live-preview reload paths beyond what Wave-1 plugins need** — deferred to V2 (REQ-live-preview-edits in REQUIREMENTS.md)
- **Author-new-content workflow (new meshes, scripts, quests)** — deferred to V2 (REQ-author-new-content)
- **One-click mod packaging** — deferred to V2 (REQ-one-click-package; Wave-3 plugin)
- **Community hub publish/consume** — deferred to V2 (REQ-share-to-hub; Wave-3 plugin)
- **Wave-2 plugins** (Conversation, Quest, Buildout, Particle, UI Page, Shader) — V2+
- **Wave-3 plugins** (Mod Manager, Packager, Community Hub, Asset Diff) — V2+
- **Tier 3 test harness (mock D3D9 + recorded fixtures)** — deferred to V2 per scope; covered by Tier 4 manual loop in V1
- **FlaUI WinForms automation** — deliberately skipped (CON-TT-03; too flaky)

### Out of Scope (Permanent — Anti-Goals)

These are locked product-scope decisions; see Key Decisions below.

- Server-side mod management (SWG-Source / swg-main own that)
- Launcher / patcher (SWGEmu and community launchers own that)
- DCC replacement — mesh / animation / texture authoring (Maya / 3ds Max own that)
- Multiplayer-cheat enabling (all editing is local/offline; shards may detect and reject modified clients — accepted)

## Context

**Architecture as audited 2026-05-16.** Solid foundations, localised execution bugs. Clean separation: `swg::*` shim (RE'd game) → `utinni::*` thin façade → CLR bridge (CppSharp-generated) → MEF `IPlugin` / `IEditorPlugin` SPI → WinForms editor host. Two-language plugin model (C++ + .NET runtime + .NET editor) with parity templates. Detour-table pattern (`using pX = ...; pX x = (pX)0xRVA; Detour::Create(...)`) is uniform and greppable. Framework today covers ~10–15% of the eventual product surface; the rest is plugins, not framework rewrites.

**Effort estimate:** assessment.md sizes V1 stability work at ~6–8 person-weeks of focused work (15 critical fixes ~2 wk + 8 strategic reworks ~3–4 wk + cleanups/dep bumps ~1 wk + 1.0 cut packaging ~1 wk). Plugin waves are downstream of that.

**Sister repo.** `UtinniPlugins` lives at `github.com/kennethlong/UtinniPlugins` (separate repo, not vendored here). Wave-1 plugin work will land in that repo against the framework changes made here. V1 success requires both repos at green.

**Test harness gap.** Today, verification = build → inject → eyeball in WinForms. Only the maintainer can close that loop. Tier 1 + Tier 2 (V1 scope) targets ~60–70% conversion of "Kenny please verify" loops into unattended CI runs.

**Codebase intel.** Pre-existing reference docs at `.planning/codebase/`: ARCHITECTURE.md, STACK.md, STRUCTURE.md, CONCERNS.md, CONVENTIONS.md, INTEGRATIONS.md, TESTING.md. Treat as read-only context.

## Constraints

### Platform / runtime

- **Platform**: Windows-only desktop application — no Linux fallback (CON-P-01).
- **Target process**: 32-bit `SWG.exe`; UtinniCore is an x86 in-process DLL (CON-P-02).
- **Graphics SDK**: DXSDK June 2010 currently required for `d3dx9.h` math helpers (CON-P-03); replaceability is open question CON-O-08.
- **Build toolchains**: VS 2019 (native UtinniCore) and VS 2022 (managed); contributor support must cover both `[16.0,18.0)` (CON-B-01).

### Preservation (negative — do-not-refactor)

24 load-bearing design elements enumerated in `.planning/intel/constraints.md` family CON-N-* (9 native), CON-M-* (9 managed), CON-T-* (5 process/tooling). Every V1 phase plan that touches a preserved item must include explicit justification. Examples: `swg::<subsystem>` detour-table pattern (CON-N-01), `utinni::` thin-wrapper firewall (CON-N-02), `PluginManager` pImpl idiom (CON-N-08), `IPlugin`/`IEditorPlugin` MEF SPI (CON-M-01/02), `UtinniCore.vcxproj` post-build chain (CON-T-01), two-language template parity (CON-T-03).

### Technical class-of-bug constraints (from assessment.md fixes)

- **CON-H-01**: DllMain must not do heavy startup (`LoadLibrary` + CLR bring-up are explicitly forbidden in `DLL_PROCESS_ATTACH`). Defer to first SWG callback or exported `utinni_init`.
- **CON-H-02**: Pattern-scan results must be null-checked before use.
- **CON-H-03**: Hard-coded RVAs must have a single source of truth, exposed via `UTINNI_API`.
- **CON-H-04**: Callback subscriber lists must be safe under concurrent dispatch + mutation (snapshot under lock before iterating).
- **CON-H-05**: Every callback must have symmetric `Add` / `Remove`.
- **CON-L-01..-04**: Plugin ABI symmetric; `init()` actually invoked; plugin load failures isolated and logged; plugin-side exceptions must not bubble through framework callbacks.
- **CON-B-04**: Cross-CRT discipline — every cross-boundary allocation/free must use the originator's allocator.

### Distribution policy

- **CON-D-01**: `data/utinni.cfg` ships with blank server host/port. Never default users into any specific shard's infrastructure (potential ToS issue).

### Open / unresolved

Eight inherited open questions (CON-O-01..CON-O-08 from assessment.md) plus three test-harness opens (CON-O-09..CON-O-11). Each gates a specific phase plan — see ROADMAP.md for phase-to-question mapping.

## Key Decisions

| Decision | Source | Status | Notes |
|----------|--------|--------|-------|
| **DEC-A1**: Utinni is NOT a server-side mod manager. SWG-Source / swg-main own that. | user-locked from vision.md anti-goals | LOCKED ✓ | Permanent product-scope boundary (CON-S-01) |
| **DEC-A2**: Utinni is NOT a launcher / patcher. SWGEmu and community launchers own that. | user-locked from vision.md anti-goals | LOCKED ✓ | Permanent product-scope boundary (CON-S-02) |
| **DEC-A3**: Utinni is NOT a Maya / 3ds Max replacement. DCC tools own mesh / animation / texture authoring. | user-locked from vision.md anti-goals | LOCKED ✓ | Permanent product-scope boundary (CON-S-03) |
| **DEC-A4**: Utinni is NOT a multiplayer-cheat enabler. All editing is local/offline; live shards may detect and reject modified clients — accepted. | user-locked from vision.md anti-goals | LOCKED ✓ | Permanent product-scope boundary (CON-S-04) |
| **DEC-C1 (candidate D-01)**: Utinni's product target is a one-stop, plugin-based modding tool — not just the snapshot editor it is today. | vision.md "The goal" | Candidate — non-locked | Promote to ADR when V1 ships and Wave-1 demo lands. |
| **DEC-C2 (candidate D-04)**: The four anti-goals (DEC-A1..A4) form the canonical "should we build it?" scope filter for all future plugin proposals. | vision.md "Anti-goals" | Candidate — non-locked | Wrapper decision; the four anti-goals themselves are LOCKED. |
| **DEC-C3 (LOCKED, was candidate D-08)**: Testing strategy is pragmatic and tiered, not blanket TDD. TDD applies only to pure-logic and file-format layers; native detours and WinForms UI use smoke/integration tests via Tiers 2–4. | test-harness-plan.md "Testing philosophy" | LOCKED ✓ | Promoted at Phase 4 close (Plan 04-04). |
| **DEC-C4**: Wave-1 deliverables (TRE Browser, IFF Editor, Datatable Editor, String-table Editor, Object Template Editor — Phases 7-11) ship as `IEditorPlugin` subpanels INSIDE The Jawa Toolbox, NOT as separate plugins. Distribution granularity is "Utinni + TJT as a pair"; cherry-picking individual editors is not a V1 user story. Shared format code (IFF read/write, format helpers, common UI patterns) lives in `TheJawaToolboxDotNet`/`TheJawaToolbox` next to the panels that consume it. V2 Wave-2 (Conversation, Quest, Buildout, Particle, UI Page, Shader) and Wave-3 (Mod Manager, Packager, Community Hub, Asset Diff) plugins MAY still ship as separate plugins — this decision is V1-scoped. | session 2026-05-17 (during Phase 02.1 prep, after the "fundamental extensions" framing) | LOCKED ✓ for V1 | Locks Phase 7-11 architecture before plan-phase work begins. Rationale: these editors are fundamental to Utinni's V1 value (one-stop modding tool), not optional add-ons; cross-plugin IFF coupling would create a versioning/load-order surface that doesn't pay rent for the V1 user model. Re-opens for V2 if third-party plugin ecosystem develops. |
| **DEC-V2-LIFT-SHIFT**: When reviving a third-party build tool, copy its source + required shared libs into a Utinni-owned `tools/` tree at the shared toolset (v145), pin the exact upstream `swg-client-v2` SHA (not a branch HEAD), and never `#include`/ProjectReference into the live upstream tree. | v2.0 research SUMMARY.md + Phase 12 | LOCKED ✓ | ✓ Good — kept Utinni's build decoupled from `swg-client-v2`'s active D3D9→D3D11 migration. "Revive" proved to be REAL porting (engine-API drift, C++20, SAFESEH, CRT-compat), not a clean lift. |
| **DEC-V2-MCP-OOP**: The MCP server is a separate modern-.NET (net10) stdio process shelling `utinni-cli`; NEVER host the MCP SDK in-proc in the net472/x86 injected client. The live-bridge crosses to the in-proc client only via a narrow named pipe — the pipe IS the architecture boundary. | Phase 14 + Phase 16 | LOCKED ✓ | ✓ Good — kept the AI-integration surface small/auditable (MCP layer owns ZERO format logic) and kept the LLM transport loop out of SWG.exe's address space. |
| **DEC-V2-VERBS-FIRST**: Every capability lands as a golden-tested `utinni-cli` verb FIRST; the MCP layer stays a thin dispatcher with zero business logic. Scoped, *named* exceptions allowed (the Phase-14 `apply-save-*` verbs fixing the reviewer-found save-no-op). | Phase 13/14 | LOCKED ✓ | ✓ Good — single-sourced the codecs and made the security surface reviewable; the named-exception discipline kept guard-rail breaks explicit. |

Additional non-locked candidate decisions (D-02 foundations-before-features, D-05 wave-1-plugin-set, D-07 CI-before-anything-else-strategic, D-09 ~6–8 person-week effort estimate) are encoded as **roadmap phase ordering** in ROADMAP.md rather than as ADRs. D-03 (sovereign fork) and D-06 (Jawa Toolbox `*Impl` separation as canonical) are captured here as the Sovereign-Fork Stance section above and as CON-T-05 respectively.

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-06-15 — Phase 17 (CppSharp / v145 hardening, CPPS-01..04) complete; CI 8cc05b4 green. Prior: 2026-06-14 — v2.1 "Wave-2 Editors + Foundation Hardening" milestone started (Phases 17+:
Terrain + one adjacent Wave-2 editor on a hardened D3D11/v145 base; optional IFF-chunk-template + TRE-
history quick wins). Previous: 2026-06-14 after v2.0 milestone — v2.0 "AI-Assisted SWG Tools" SHIPPED (`v2.0`, Phases 12–16: revived SOE compilers as `utinni-cli` verbs + headless net10 MCP server + Wave-2 editors + live MCP bridge + Blender boundary; DEC-V2-LIFT-SHIFT / MCP-OOP / VERBS-FIRST locked). v1.0 retroactively archived at the same close (was tagged but never run through `/gsd:complete-milestone`). Previous updates: 2026-06-01 — V1 shipped (`v1.0.0`); 2026-05-29 — Phase 8 complete (PROD-W1-IFF validated); 2026-05-23 (Phase 4 + DEC-C3 LOCKED); 2026-05-17 (Phase 2 + DEC-C4 locked); initial creation via `/gsd:new-project` after `/gsd:ingest-docs` synthesis of vision.md + assessment.md + test-harness-plan.md.*
