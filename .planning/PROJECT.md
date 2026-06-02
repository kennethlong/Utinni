# Utinni

## What This Is

Utinni is an in-process modding tool and framework for Star Wars Galaxies (SWG): a 32-bit native UtinniCore DLL injected into a live `SWG.exe`, a .NET Framework WinForms editor host, and an MEF-discovered C# plugin pipeline. Today it is primarily the Jawa Toolbox world-snapshot editor; the V1 milestone advances it toward a one-stop modding tool that replaces the ~30 separate SOE-era apps SWG modders juggle today.

## Core Value

A modder downloads Utinni, installs once, and from a single application can see, edit, and live-preview every asset the SWG client loads — replacing the fragmented 15-year-old editor zoo with one stable, plugin-driven tool.

## Sovereign-Fork Stance

Both `Utinni` and `UtinniPlugins` are MIT forks of `ptklatt/Utinni` and `ptklatt/UtinniPlugins`. Upstream appears dormant. This fork advances independently; clean fixes may be offered upstream as PRs but the fork is not gated on upstream acceptance. Composes with SWG-Source conventions where server-side touch points exist (which V1 explicitly avoids — see anti-goals).

## Current Milestone: v2.0 AI-Assisted SWG Tools

**Goal:** *Utinni authors, not just edits* — close the compile/build gaps and ship an MCP server so AI agents can drive the full SWG asset pipeline, then land the first DCC-style editors.

**Target features:**
- **AI / MCP integration** — an MCP server exposing Utinni's verified, byte-exact pipeline (read+edit+save TRE/IFF/datatable/string-table/object-template) as agent tools, built as a thin shim over the existing `Utinni.Cli` JSON verbs / `UtinniCoreDotNet`. *The centerpiece.*
- **Authoring & compile pipeline (revive+wrap)** — get the original build-chain CLIs compiling and wrap them: `TemplateCompiler`/`TemplateDefinitionCompiler` (`.tpf`/`.tpd` → `.iff`, which also yields the per-class param→type map the Object Template Editor's Tier-2 typed display needs), `TreeFileBuilder` (build-from-source `.tre`; Utinni only repacks today), the `DataTableTool` compile path, item exporters.
- **New asset editors (replace)** — first DCC-style Utinni SubPanels: Terrain, Particle/Effects; extend the Snapshot panel into a WorldSnapshot/object-placement editor; Animation live-in-client preview (coordinated with the Blender suite).
- **Ecosystem integration** — formalize the Utinni ↔ `swg-blender-plugin` boundary (Utinni opens/previews what Blender exports; Utinni stays out of 3D mesh/skel/anim authoring).
- **Carried residuals** — Object Template Tier-2 typed list-param display, the intro-skip scene-transition crash (VEH logger deployed), the SC3 live-reload residual, and the SWG window-resize/fullscreen edge cases.

**Strategy:** revive+wrap (cheap, high-leverage) before replace (the meatier editors). The revived compilers double as MCP write tools and unblock OT Tier-2. See `docs/ai/toolchain-inventory.md` for the full tool cross-walk and revive/replace rationale.

**Lift-and-shift constraint (locked):** when reviving a tool, **copy its source + required shared libs into a Utinni-owned build location** — do NOT build in-place against the `swg-client-v2` tree or modify it. `swg-client-v2` has an **active D3D9→D3D11 migration** in progress; lift-and-shift keeps our build decoupled from that churn and stays out of their way. (The revive targets — `TemplateCompiler`, `TreeFileBuilder`, exporters — are mostly headless/console and don't need the renderer, which makes lift-and-shift clean.)

**Prior milestone — V1 "Demo + CI green" — SHIPPED 2026-06-01 (`v1.0.0`):** all 15 critical bugs closed; R-A..R-H landed; Tier 1+2 CI green; all five Wave-1 subpanels (TRE Browser, IFF Editor, Datatable, String-table, Object Template) demoed end-to-end against the live SWG client.

## Requirements

### Validated

<!-- Shipped and confirmed valuable. -->

- [x] **TEST-01 (Tier 1 C# scaffold + CI green on master)** — Validated in Phase 1: GitHub Actions `windows-2022` workflow builds `Utinni.sln /p:Configuration=Release /p:Platform=x86` and runs `dotnet test` on every push/PR to master; CON-T-01 post-build chain (`xcopy data\` + `UtinniCoreDotNetGen.exe`) confirmed firing under CI; xUnit smoke project `UtinniCoreDotNet.Tests` discovers and runs `HotkeyTests.cs`; test-the-tester procedure exercised on throwaway PR proving red-on-failure surfaces and master badge stays green. Foundation enabling every subsequent phase's "gate on green pipeline" contract.
- [x] **TEST-03 (Tier 2 CLI shim with golden fixtures)** — Validated in Phase 4 (2026-05-23): `utinni-cli.exe` ships four verbs (`parse-tre`, `list-objects`, `inspect-iff`, `validate-plugin`) producing stable sorted-key JSON envelopes (top-level `schemaVersion`/`command` per REVIEWS HIGH-6); 50 `Utinni.Cli.Tests` Tier-2 goldens + 25 new Tier-1 parser tests (10 TRE + 15 IFF) in `UtinniCoreDotNet.Tests`; CI extended with second `dotnet test` lane (`.github/workflows/ci.yml:94-108`); 18 synthesized fixtures (5 dispatch + 5 TRE + 5 IFF + 4 plugin + 1 ws.iff, all ≤200 bytes per D-03); 4-cycle adversarial cross-AI plan review (Codex + Cursor PASS at iter-4). Closes CON-O-09 (fixture storage = in-repo synth, no LFS), CON-O-10, CON-O-11 in `docs/ai/assessment.md`. DEC-C3 (tiered testing strategy) promoted from Candidate to LOCKED ✓.
- [x] **PROD-W1-IFF (Wave-1 plugin: IFF Editor, read + write)** — Validated in Phase 8 (2026-05-29): the IFF Editor ships as a TJT subpanel (`FormIffEditor`) hosting a shared `IffChunkTree` UserControl (08-03), an `IffEditController` with 10 structural-op commands + editor-local undo/redo (08-04), and the full four-tier D-05 save matrix: loose-override + Save/Save-As file save (08-05, maintainer-approved live smoke), in-memory live-patch (08-06, infra-ready+user-disabled; 5 [Fact] bounds gates), and `.tre` archive repack (08-07, 15-outcome on-disk contract). Framework primitives — `IffWriter` byte-exact serializer, `MutableIffDocument` hybrid DOM, `OpenSource` 4-case discriminated union, `TreFile.GetRecord{CompressedBytes,NameBytes}`, `TreWriter`, `LooseOverridePath`, `ReloadAssetClassifier`, `LivePatchValidator`, `TreBackupPath`, `TreRepackLock`, `TreRecordIndexResolver`, `IffEditController` — all live in `UtinniCoreDotNet/{Formats\Iff,Editing,Saving,Formats\Tre}` and are consumed by direct ProjectReference (no linked-source). CLI `roundtrip-iff` verb + 11 goldens (identity, no-pad, payload-mutation, structural-removal) carry CI coverage. Tests: 331 UtinniCoreDotNet.Tests + 123 Utinni.Cli.Tests, all green. Code review found 1 BLOCKER + 7 Warnings (CR-01 Remove/Undo/Redo desync + WR-01..WR-07 path-traversal canonicalization, Windows hash case-sensitivity, EnumerateOnly guard, etc.); all fixed inline with +12 regression Facts. Deferred-but-acceptable: Open Q1 cursor N-H1 ACK, Open Q2 verified loose-override subdir, Open Q3 reload-matrix granularity, Open Q4 full live-patch functional smoke, Open Q5 UI end-to-end smoke, dirty-discard UX gap.

### Active

<!-- v2.0 "AI-Assisted SWG Tools" scope. REQ-IDs + acceptance defined in REQUIREMENTS.md (this milestone). -->
<!-- All V1 Active items above SHIPPED in v1.0.0 (2026-06-01); they move to Validated as the v2.0 requirements are written. -->

- [ ] AI/MCP: MCP server exposing Utinni's read+edit+save pipeline as agent tools (thin shim over Utinni.Cli / UtinniCoreDotNet)
- [ ] Authoring (revive+wrap): TemplateCompiler/TemplateDefinitionCompiler (`.tpf`/`.tpd` → `.iff` + param→type map)
- [ ] Authoring (revive+wrap): TreeFileBuilder (build-from-source `.tre`); DataTableTool compile path; item exporters
- [ ] New editor (replace): Terrain editor SubPanel
- [ ] New editor (replace): Particle / client-effect editor SubPanel
- [ ] New editor (extend): WorldSnapshot / object-placement editor (grow the Snapshot panel)
- [ ] Ecosystem: formalize the Utinni ↔ swg-blender-plugin boundary (open/preview Blender exports)
- [ ] Residuals: OT Tier-2 typed list-param display; intro-skip crash; SC3 live-reload; window-resize/fullscreen edge cases

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

Additional non-locked candidate decisions (D-02 foundations-before-features, D-05 wave-1-plugin-set, D-07 CI-before-anything-else-strategic, D-09 ~6–8 person-week effort estimate) are encoded as **roadmap phase ordering** in ROADMAP.md rather than as ADRs. D-03 (sovereign fork) and D-06 (Jawa Toolbox `*Impl` separation as canonical) are captured here as the Sovereign-Fork Stance section above and as CON-T-05 respectively.

---
*Last updated: 2026-06-01 — V1 shipped (`v1.0.0`, all five Wave-1 subpanels); milestone v2.0 "AI-Assisted SWG Tools" started (MCP server + revive/replace authoring pipeline; lift-and-shift constraint locked). Previous updates: 2026-05-29 — Phase 8 complete (TJT IFF Editor read+write with four-tier D-05 save matrix; PROD-W1-IFF validated). Earlier: 2026-05-23 (Phase 4 complete + DEC-C3 LOCKED + CON-O-09/10/11 dispositioned); 2026-05-17 (Phase 2 complete + DEC-C4 locked); initial creation via `/gsd:new-project` after `/gsd:ingest-docs` synthesis of vision.md + assessment.md + test-harness-plan.md.*
