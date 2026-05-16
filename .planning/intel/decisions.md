# Decisions (Candidate)

> All three ingested docs are classified as DOC (lowest precedence). No ADR/SPEC/PRD was ingested, so nothing here is a LOCKED decision — these are **candidate decisions** distilled from strategic/audit prose for the roadmapper to consider when drafting PROJECT.md `<decisions>` blocks. None of these gate downstream work until a maintainer ratifies them (typically by promoting one into an ADR).

---

## D-01 — Utinni is the integrated, plugin-based modding tool for SWG (not just the snapshot editor it is today)

- **Source:** `docs/ai/vision.md` — "The goal" + "Why Utinni is the right foundation"
- **Status:** Candidate (strategic, asserted by maintainer in vision)
- **Locked:** false
- **Scope:** Product positioning, plugin pipeline, framework prioritisation
- **Statement:** Utinni's product target is a one-stop modding tool that replaces the ~30 separate legacy editors a modder juggles today. Plugins (TRE Browser, IFF Editor, Datatable Editor, Conversation Editor, Quest Editor, Buildout Editor, Mod Manager, etc.) carry the feature surface; the framework provides the in-process client, render hooks, ImGui/ImGuizmo overlay, and .NET host.

## D-02 — Foundations before features

- **Source:** `docs/ai/vision.md` — "What that means for prioritisation"; reinforced by `docs/ai/assessment.md` — "Recommended sequencing"
- **Status:** Candidate
- **Locked:** false
- **Scope:** Roadmap sequencing
- **Statement:** Before piling new plugins on, the framework is stabilised: critical bugs fixed (Weeks 1–3 of the assessment), then strategic reworks for plugin-author ergonomics (Weeks 4–6), then plugin pipeline. Order is: stabilise → polish authoring → ship plugins wave-by-wave.

## D-03 — Sovereign MIT fork; upstream coordination is opportunistic, not blocking

- **Source:** `docs/ai/vision.md` — "Strategic position"
- **Status:** Candidate
- **Locked:** false
- **Scope:** Fork governance
- **Statement:** Both `Utinni` and `UtinniPlugins` advance independently of `ptklatt/Utinni` and `ptklatt/UtinniPlugins` (which appear dormant). Clean fixes are offered upstream; the fork is not gated on upstream acceptance. Compose with SWG-Source conventions where server-side touch points exist.

## D-04 — Anti-goals: not a launcher, not a server-side manager, not a DCC, not a cheat enabler

- **Source:** `docs/ai/vision.md` — "Anti-goals"
- **Status:** Candidate (explicit scope-limiting decisions)
- **Locked:** false
- **Scope:** Product scope boundaries
- **Statement:** Utinni explicitly is NOT (a) a server-side mod manager (SWG-Source / swg-main own that), (b) a launcher/patcher (SWGEmu and community launchers own that), (c) a Maya/3ds Max replacement (DCC tools own mesh/animation/texture authoring), (d) a multiplayer-cheat enabler (all editing is local/offline; shards may detect and reject modified clients and that's accepted). These shape every "should we build it?" question.

## D-05 — Wave-1 plugin set is the post-1.0 plugin focus

- **Source:** `docs/ai/vision.md` — "Wave 1 — round out what we have"; `docs/ai/assessment.md` — "After this, the Wave 1 plugins ... become tractable"
- **Status:** Candidate (list, not a commitment)
- **Locked:** false
- **Scope:** Plugin sequencing
- **Statement:** First plugin wave after the 1.0 framework cut is: TRE Browser (read-only), IFF Editor (read+write), Datatable Editor (`.tab`), String-table Editor (`.stf`), Object Template Editor. Wave 2 (Conversation, Quest, Buildout, Particle, UI Page, Shader), Wave 3 (Mod Manager, Mod Packager, Community Hub, Asset Diff), and Wave 4 (Mesh viewer, Animation, Script editor, Texture) are downstream and partially out-of-scope.

## D-06 — Adopt the Jawa Toolbox `*Impl` separation as the canonical plugin architecture

- **Source:** `docs/ai/assessment.md` — "Solid foundations" + plugin-architecture call-out
- **Status:** Candidate
- **Locked:** false
- **Scope:** Plugin SPI conventions
- **Statement:** Promote the Jawa Toolbox pattern (thin presentation SubPanel, business logic in `*Impl`, callbacks registered in `*Impl` constructor) as the documented canonical plugin shape. Modernised templates should emit this structure by default.

## D-07 — Add CI before doing anything else strategic

- **Source:** `docs/ai/assessment.md` — R-D, Week 4 sequencing; `docs/ai/test-harness-plan.md` — Tier 1
- **Status:** Candidate (highest-leverage durability move)
- **Locked:** false
- **Scope:** Engineering process
- **Statement:** Land a minimal GitHub Actions Windows-runner `build.yml` (msbuild Release/x86) as the first durability investment. `.editorconfig` and a smoke-test xUnit project follow. CI exists before Wave-1 plugin work starts.

## D-08 — Pragmatic, tiered testing (not blanket TDD)

- **Source:** `docs/ai/test-harness-plan.md` — "Testing philosophy"
- **Status:** Candidate
- **Locked:** false
- **Scope:** Testing strategy
- **Statement:** TDD applies to pure-logic and file-format layers (parsers, plugin loader, settings, transforms, data models). Native D3D9 detours, in-process injection, and WinForms UI get smoke/integration tests via tiers 2–3 (CLI shim + recorded fixtures + mock D3D9). Tier 4 (manual maintainer loop) is the explicit, scoped residual.

## D-09 — Target a "1.0" cut at ~6–8 person-weeks of focused work

- **Source:** `docs/ai/assessment.md` — "Effort estimate"
- **Status:** Candidate (estimate, not commitment)
- **Locked:** false
- **Scope:** Release target
- **Statement:** 15 critical bug fixes (~2 wk) + 8 strategic reworks (~3–4 wk) + cleanups/dep bumps (~1 wk) = ~6–8 person-weeks to a confident 1.0 framework cut. Week 9 is packaging + release workflow + tag.

---

## Notes for the roadmapper

- None of these are LOCKED. The roadmapper may promote any of them into a proper ADR (with Context/Decision/Consequences) if `gsd-new-project` decides to gate downstream work on them. Most natural ADR candidates: D-01 (product target), D-04 (anti-goals), D-08 (testing strategy).
- D-02, D-05, D-07, D-09 read more like roadmap ordering than decisions — the roadmapper may prefer to encode them as phase ordering in ROADMAP.md rather than ADRs.
- D-03 and D-06 are conventions; encode in CONVENTIONS.md or a thin ADR.
