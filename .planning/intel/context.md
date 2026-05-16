# Context

> Free-form context distilled from the three DOC inputs. Organised by topic. Every claim is attributed to its source.

---

## Product positioning

**Source:** `docs/ai/vision.md`

Utinni today is one tool out of ~30 that an SWG modder uses; specifically the world-snapshot editor (powered by Jawa Toolbox). The vision is to make Utinni the *only* tool a modder opens — covering browsing, editing, previewing, authoring, packaging, and sharing. The phrase used: "download Utinni, install once, do everything."

The competitive landscape today is fragmented:
- 15+ year old SOE-era editors (`TreeFileExtractor`, `IFFEditor`, `SwgConversationEditor`, `SwgSpaceQuestEditor`, `SwgDataTableTool`, `SwgStringEditor`, `SwgTerrainEditor`, `ParticleEditor`, `SwgCreatureEditor`-class tools) — many won't run cleanly on modern Windows.
- DCC tools for mesh/animation/texture (Maya / 3ds Max + SOE exporter plugins).
- Hand-edit-in-text-editor for `.iff`, `.tab`, `.stf`, `.inc`, scripts.
- Modding forums + ad-hoc archives for distribution.

The strategic insight: because the live SWG client is loaded *inside* the editor (via `LoadLibrary` injection), Utinni's plugins have free access to anything the client can load and render — a deeper integration than any standalone SOE tool. The `TreeFile` hook, DirectX hooks, ImGui/ImGuizmo overlay, and .NET WinForms host are infrastructure that every future plugin gets for free.

Framework today covers ~10–15% of the surface area. The rest is plugins, not framework rewrites.

---

## Strategic position vs upstream

**Source:** `docs/ai/vision.md` — "Strategic position"

Both `Utinni` and `UtinniPlugins` are MIT-licensed forks of `ptklatt/Utinni` and `ptklatt/UtinniPlugins`. Upstreams appear dormant. The fork strategy:
1. Build a sovereign fork that advances independently.
2. Offer fixes upstream where they're clean PRs (community benefits even if direction differs).
3. Cooperate with SWG-Source for any server-side touch points (they maintain `client-tools/` and `swg-main/`; compose where possible).

Goal: Utinni is the *default* modding tool, not necessarily the only one. New modders should be told "download Utinni" and be productive in an afternoon.

---

## Architecture as it stands today (audited 2026-05-16)

**Source:** `docs/ai/assessment.md` — Executive summary

The audit verdict: **architecture is solid; execution has real bugs but they're localised.** Components called out as well-shaped:
- Clean separation: SWG shim (`swg::*`) → public façade (`utinni::*`) → CLR bridge → MEF plugin contract → editor host.
- Uniform detour-installation convention (easy to extend).
- Two-language plugin model (C++ + C#) — consistent and well-documented.
- Specific subsystems to keep: UndoRedo event model, HotkeyManager + INI persistence, themed WinForms controls, `*Impl`-separated plugin pattern (from Jawa Toolbox).

The risky code lives in known files — none of them foundations. Fixes can ship incrementally without rewriting anything. Biggest investment is CI, not refactoring.

Effort estimate to a confident 1.0: ~6–8 person-weeks total.

---

## The 1.0 work, by phase

**Source:** `docs/ai/assessment.md` — "Recommended sequencing"

The assessment proposes 9 weeks of work (loosely ordered, parallelisable):

1. **Week 1 — stop the bleeding.** Trivial/low-effort criticals: C-04 (post-draw queue), C-06 (PluginLoader exception swallowing), C-08 (Hotkey TryParse), C-13 (TJT Debug path), C-14 (utinni.cfg login server), C-12 (VSIX 16+17).
2. **Week 2 — durability.** Single-file criticals: C-02, C-03, C-05, C-07, C-10, C-11, C-15.
3. **Week 3 — architectural.** The riskier criticals: C-01 (DllMain loader-lock) and C-09 (UI/game-thread busy-wait).
4. **Week 4 — leverage.** R-D (CI build workflow), dead-code sweep (~250 lines), C++ formatter pass.
5. **Weeks 5–6 — strategic reworks.** R-A (symmetric callbacks), R-B (plugin lifecycle), R-C (single-source RVAs), R-E (`[CallerMemberName]` logging).
6. **Weeks 7–8 — modernisation.** Bump imgui to docking branch, spdlog 1.14, ImGuizmo latest. R-F (CppSharp auto-discovery), R-G (Directory.Build.props idempotent), R-H (snapshot iteration). LeksysINI decision. SDK-style csproj migration if widening to VS 2022.
7. **Week 9 — 1.0 cut.** Packaging script, release workflow, tag 1.0.

After this, Wave-1 plugins (TRE Browser, IFF Editor, Datatable Editor, String-table Editor, Object Template Editor) become tractable.

---

## Plugin pipeline waves

**Source:** `docs/ai/vision.md` — "What plugins we'd want, in rough order"

Long-range list, ordered by (impact × leverage on existing Utinni capabilities). Each is months of work; they ship as they're ready.

- **Wave 1 — round out what we have:** TRE Browser (read-only), IFF Editor (read+write), Datatable Editor (`.tab`), String-table Editor (`.stf`), Object Template Editor.
- **Wave 2 — content authoring:** Conversation Tree Editor, Quest Editor, Buildout / World Editor, Particle Editor, UI Page Editor, Shader Inspector / Editor.
- **Wave 3 — workflow:** Mod Manager, Mod Packager / Builder, Community Hub Browser (optional), Asset Diff / Compare.
- **Wave 4 — maybe-someday:** Mesh viewer (read-only), Animation previewer, Script editor (server-side — likely out of scope), Texture authoring (likely out of scope).

Wave 4 items are realistically the territory of separate tools Utinni hands off to. Mesh/texture authoring need specialised UX that DCC tools have spent decades on.

---

## Test harness strategy

**Source:** `docs/ai/test-harness-plan.md`

Default verification today is: build → launch SWG client → click around in WinForms → eyeball it. Only the maintainer can close that loop; nothing regression-protects future changes.

Plan replaces this with four tiers:

- **Tier 1 — Pure unit tests** (fully autonomous). xUnit/NUnit C# tests + Catch2 C++ tests. Targets: TRE/IFF parsers, plugin manifest loader, settings serialization/migration, math helpers, data-model logic. `dotnet test` + `ctest` in CI.
- **Tier 2 — CLI shim** (highest leverage). `utinni-cli` executable exposing the operations the WinForms UI calls. Commands: `parse-tre`, `list-objects`, `validate-plugin`, `inspect-iff`. Golden-file tests against checked-in fixtures. Estimated to convert ~60–70% of "Kenny please verify" loops to unattended runs. Side benefit: the UI becomes one of two consumers of the core, not the sole consumer.
- **Tier 3 — Recorded fixtures + mock D3D9**. One-time maintainer capture of TRE/IFF + D3D9 call trace; stub `IDirect3DDevice9` replays the trace through hook code. Regression-test detours without running the game. Optional pixel-hash screenshots.
- **Tier 4 — Manual maintainer**. Explicit, scoped residual: real `SWG.exe` injection, visual judgment for UI, GPU-driver-specific bugs, WinForms smoke (FlaUI deliberately skipped — too flaky).

Suggested phase order: Tier 1 C# first (smallest, unblocks everything), then Tier 2 CLI shim (biggest payoff, depends on Tier 1 having extracted seams), then Tier 1 C++ (pairs with native cleanup), then Tier 3 (only when touching hook code intentionally).

Status: Draft, not yet planned as a GSD phase.

---

## Open questions inherited from project history

**Source:** `docs/ai/assessment.md` — "Open questions for project history"

These need someone-who-was-there to answer. Captured here as context; tracked as unresolved constraints (CON-O-01..-08) in `constraints.md`:

1. `isSafeToUse` — code uses `||`, doc says `&&`. Which is correct?
2. Was `AddPostDrawLoopCall` ever actually used? Broken since 2020.
3. The "storing this in a variable prevents corruption" comment in `GameCallbacks.cs:46` — likely missing `GCHandle.Alloc` on a delegate passed to unmanaged.
4. VS 2019 pin — was there a real reason, or just history?
5. `StdEdited.cs` curation criteria — what's hand-maintained vs auto-generated?
6. LeksysINI — README says "temporary, will most likely be replaced". What was the plan?
7. Sytner's plugin — code elsewhere never merged, or always aspirational?
8. DXSDK June 2010 — could it be replaced with Windows 10 SDK's d3d9 headers? (DXSDK has `d3dx9.h`, Windows SDK lacks it.)

Test-harness-plan adds three more (CON-O-09..-11): test project layout, fixture storage (in-repo vs LFS), CLI shim public vs internal.

---

## Cross-document linkage

- vision.md and assessment.md cross-reference each other (vision points to assessment for "current health"; assessment points to vision for "the direction this serves"). This is a reciprocal "See also" relationship, not a content-derivation cycle.
- test-harness-plan.md references both vision.md and assessment.md as upstream context.
- All three reference `.planning/codebase/` (mapped earlier) for grounding facts — those files exist (`ARCHITECTURE.md`, `CONCERNS.md`, `CONVENTIONS.md`, `INTEGRATIONS.md`, `STACK.md`, `STRUCTURE.md`, `TESTING.md`) and are available for downstream consumers but are not part of the ingest set.

---

## Notes for the roadmapper

- The phase-by-phase sequencing in assessment.md (Weeks 1–9) is a strong roadmap skeleton. Roadmapper may lift it largely intact.
- Plugin waves are deliberately long-horizon — they probably become "phase epics" rather than fitting in V1.
- Test harness plan is explicitly Draft and not yet phase-planned — surface to maintainer when the roadmap is drafted to decide whether it lands inside the 6–8-week 1.0 work or after.
