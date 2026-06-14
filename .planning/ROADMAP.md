# Roadmap: Utinni

A one-stop, plugin-based modding tool for Star Wars Galaxies — an injected `UtinniCore` DLL + .NET
WinForms editor host + MEF plugin pipeline — that replaces the ~30 separate SOE-era editor apps with
one stable tool, and (as of v2.0) makes the whole asset pipeline AI-drivable.

## Milestones

- ✅ **v1.0 MVP — "Demo + CI green"** — Phases 1–11 (shipped 2026-06-01, `v1.0.0`)
- ✅ **v2.0 — "AI-Assisted SWG Tools"** — Phases 12–16 (shipped 2026-06-14, `v2.0`)
- 📋 **v2.1+ (next)** — to be defined via `/gsd:new-milestone`

Full per-milestone phase detail + requirements are archived under `.planning/milestones/`.

## Phases

<details>
<summary>✅ v1.0 MVP — "Demo + CI green" (Phases 1–11) — SHIPPED 2026-06-01</summary>

Stabilised the injected-DLL framework, then shipped the first five editors as Jawa Toolbox subpanels.
Full detail: [`milestones/v1.0-ROADMAP.md`](milestones/v1.0-ROADMAP.md).

- [x] Phase 1: CI + Tier 1 C# scaffold (2/2 plans)
- [x] Phase 2: Critical bug burn-down C-01..C-15 (4/4 plans)
- [x] Phase 02.1: Phase 02 gap closure — review correctness + harness quality (3/3 plans, INSERTED)
- [x] Phase 3: Strategic reworks R-A..R-H (3/3 plans)
- [x] Phase 4: Tier 2 CLI shim + golden fixtures (4/4 plans)
- [x] Phase 5: Tier 1 C++ unit tests (2/2 plans)
- [x] Phase 6: Cleanups, dep bumps, open questions, Tier 4 doc, 1.0 cut (6/6 plans)
- [x] Phase 7: TJT subpanel — TRE Browser, read-only (6/6 plans)
- [x] Phase 8: TJT subpanel — IFF Editor, read + write (7/7 plans)
- [x] Phase 9: TJT subpanel — Datatable Editor `.tab` (7/7 plans)
- [x] Phase 10: TJT subpanel — String-table Editor `.stf` (6/6 plans)
- [x] Phase 11: TJT subpanel — Object Template Editor (5/5 plans) — V1 closure

</details>

<details>
<summary>✅ v2.0 — "AI-Assisted SWG Tools" (Phases 12–16) — SHIPPED 2026-06-14</summary>

Turned Utinni from a tool that *edits* assets into one that *authors* them, and made the pipeline
AI-drivable. Build order: revive-feasibility spike (hard gate) → revive+wrap SOE CLIs → headless MCP
server → Wave-2 editors → live-injected MCP bridge + Blender boundary, last.
Full detail: [`milestones/v2.0-ROADMAP.md`](milestones/v2.0-ROADMAP.md) · audit:
[`milestones/v2.0-MILESTONE-AUDIT.md`](milestones/v2.0-MILESTONE-AUDIT.md).

- [x] Phase 12: Revive-feasibility spike (HARD GATE) + intro-skip crash (4/4 plans)
- [x] Phase 13: Wrap revived compilers as CLI verbs + close OT Tier-2 (6/6 plans)
- [x] Phase 14: Headless MCP server `Utinni.Mcp` — the centerpiece (5/5 plans)
- [x] Phase 15: Wave-2 editors (WorldSnapshot, Particle) + presentation residuals (21/21 plans)
- [x] Phase 16: Live-injected MCP bridge + Blender ecosystem boundary (3/3 plans)

</details>

### 📋 v2.1+ — Next milestone (planned)

To be defined via `/gsd:new-milestone`. Candidate scope drawn from the V2 boundary + Backlog below:
Terrain editor (`PROD-W2-TRN`), the remaining Wave-2 plugins (Conversation, Quest, Buildout, UI Page,
Shader), Wave-3 plugins (Mod Manager, Packager, Community Hub, Asset Diff), Tier-3 mock-D3D9 harness,
one-click packaging + community hub. Several Backlog 999.x ideas (schema-driven IFF chunk templates,
TRE override/version-history view) are strong candidates.

## Progress

| Phase | Milestone | Plans | Status | Completed |
|-------|-----------|-------|--------|-----------|
| 1. CI + Tier 1 C# scaffold | v1.0 | 2/2 | ✅ Complete | 2026-05 |
| 2. Critical bug burn-down | v1.0 | 4/4 | ✅ Complete | 2026-05 |
| 02.1. Phase 02 gap closure | v1.0 | 3/3 | ✅ Complete | 2026-05 |
| 3. Strategic reworks | v1.0 | 3/3 | ✅ Complete | 2026-05 |
| 4. Tier 2 CLI shim | v1.0 | 4/4 | ✅ Complete | 2026-05-23 |
| 5. Tier 1 C++ unit tests | v1.0 | 2/2 | ✅ Complete | 2026-05 |
| 6. Cleanups + 1.0 cut | v1.0 | 6/6 | ✅ Complete | 2026-05-25 |
| 7. TRE Browser | v1.0 | 6/6 | ✅ Complete | 2026-05 |
| 8. IFF Editor | v1.0 | 7/7 | ✅ Complete | 2026-05-29 |
| 9. Datatable Editor | v1.0 | 7/7 | ✅ Complete | 2026-05 |
| 10. String-table Editor | v1.0 | 6/6 | ✅ Complete | 2026-05 |
| 11. Object Template Editor | v1.0 | 5/5 | ✅ Complete | 2026-06-01 |
| 12. Revive spike + intro-skip | v2.0 | 4/4 | ✅ Complete | 2026-06-14 |
| 13. Wrap compilers + OT Tier-2 | v2.0 | 6/6 | ✅ Complete | 2026-06-05 |
| 14. Headless MCP server | v2.0 | 5/5 | ✅ Complete | 2026-06-07 |
| 15. Wave-2 editors + residuals | v2.0 | 21/21 | ✅ Complete | 2026-06-13 |
| 16. Live MCP bridge + Blender | v2.0 | 3/3 | ✅ Complete | 2026-06-14 |

**Shipped: 2 milestones, 16 phases (+02.1), 94 plans, 31 requirements (15 v1 + 16 v2.0).**

## Backlog

Unsequenced ideas parked for a future milestone (999.x). Promote with `/gsd:review-backlog`.

### Phase 999.2: User-definable IFF chunk templates (BACKLOG)

**Goal:** Let a modder *describe the binary layout* of an arbitrary IFF chunk (a schema of primitives,
colors, vectors, quaternions, matrices, arrays, structs) and have Utinni auto-decode/display/edit it —
so modders can crack `.iff` formats Utinni doesn't natively support, without code changes.

**Context (captured 2026-06-02, from the Sytner's IFF Editor comparison):**
- SIE's standout power feature: user-defined chunk templates auto-applied to matching chunks. Utinni today only decodes hardcoded formats (datatable/stf/object-template); unknown `.iff` chunks fall back to hex.
- High-leverage: turns Utinni from "the formats we coded" into "any format a modder can describe." Schema-driven decode is also **MCP-friendly** — an agent could derive a schema and read/edit an unknown chunk via a tool.
- Composes on the existing `UtinniCoreDotNet/Formats/Iff` reader + `IffPayloadCursor`; the new piece is a schema model + schema-driven decode/encode pass + a UI to define/manage templates.

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)

### Phase 999.3: TRE override / version history view (BACKLOG)

**Goal:** Show, for any logical path, every version of that file across the whole `.tre`/`.toc` load
order — and let the modder open/extract/diff any historical version — a "what overrode what" patch-stack
view for debugging load order.

**Context (captured 2026-06-02, from the SIE comparison):**
- SIE works from a *repository* of `.tre`/`.toc` files and can show/extract/open any version of a file in the override history. Utinni's TRE Browser browses archives but does not surface the cross-archive override chain.
- Why it matters: load-order/override debugging is a top modder pain point ("which `.tre` is actually winning for this path?"). A diff between base and override is the natural payoff.
- Composes on `TreArchiveIndex` (already resolves logical paths across the load order) + `TrePayloadResolver`; the new piece is exposing the full per-path resolution chain + a versions/diff UI.

> **Deferred at v2.1 scoping (2026-06-14):** a separate WIP **TRE diff tool** (~1 day from MVP) is
> solving the same load-order-resolution + diff problem. Hold 999.3 in backlog and revisit once that
> tool reaches MVP so this view can reuse its design. 999.2 (above) WAS pulled into v2.1.

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)

### Phase 999.4: Complete VS2026 v145 toolset bump — CppSharp upgrade (BACKLOG)

**Goal:** Finish the toolchain bump so the binding generator (`UtinniCoreDotNetGen`) runs natively on
v145, removing the VS 2019 14.29 STL parser-include redirect (Path 1) currently keeping CppSharp's
clang 11 alive.

**Context (captured 2026-06-14, from `[[project-vs2026-cppsharp-block]]` + `[[project-vs2026-toolchain]]`; D-09):**
- v2.0 ships green on **v145** for the C++ build, but the vendored **CppSharp 0.10.5 (clang 11)** can't
  parse the MSVC 14.5x STL. Path 1 (Wave-2, commit `d69988d`) works around this by pointing CppSharp's
  *parser* at the VS 2019 14.29 STL while the build itself uses v145 — a redirect, not a real bump.
- The clean fix needs a CppSharp upgrade: **Path 2** = vendored CppSharp → v1.2 (clang 19), but that only
  reaches v143 (no CppSharp release ships clang 20+ yet for v145) **and** forces a net4.7.2 → net9.0
  migration of `UtinniCoreDotNetGen`. So this is genuinely blocked on upstream CppSharp + a generator
  TFM migration — a Phase-6-class project, not a quick edit.
- Couples to **999.5 (D3D11)** — a newer toolset has better DXGI/D3D11 header hygiene (v144+).

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)

### Phase 999.5: D3D11 render-path migration (BACKLOG)

**Goal:** Add a parallel D3D11 hook/overlay path alongside the existing D3D9 one, so Utinni keeps
rendering its ImGui overlay + live-preview when the SWG client runs on D3D11.

**Context (captured 2026-06-14, from `[[project-d3d11-migration]]`):**
- SWG Source (`swg-client-v2`) has an **active D3D9→D3D11 migration** (`Direct3d11` project, incomplete).
  Utinni hooks **D3D9 explicitly** (pattern-scan + `Present`/`Reset` detours, not an API-abstracted
  renderer), so a D3D11 client needs a second, parallel hook path — not a config flag.
- Future **R-letter** (rework) item; was explicitly out of Phase 3 scope. Don't volunteer as a refactor
  target during unrelated work.
- Sequencing note: may coincide with / benefit from **999.4** (the v145+ toolset has cleaner D3D11
  headers). The lift-and-shift revive boundary already keeps Utinni decoupled from `swg-client-v2`'s
  D3D11 churn, so this is additive, not a forced migration.

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)

### Phase 999.6: 3D-asset authoring parity — re-evaluate Maya-write boundary (BACKLOG)

**Goal:** Revisit whether Utinni should own any 3D mesh/skeleton/animation **write/authoring** parity
(the old `MayaExporter` lane), or whether that stays entirely with the Blender suite.

**Context (captured 2026-06-14, from `[[project-swg-client-v2-reference]]` + `[[project-swg-toolchain-crosswalk]]`; re-opens DEC-A3):**
- **Locked v2.0 decision (do NOT silently override):** Utinni does NOT own 3D mesh/skeleton/anim
  authoring — that's **`D:/Code/swg-blender-plugin`**'s job (Python + Blender; import/export for static,
  skeletal, animation; `.msh/.mgn/.skt/.lod/.pob/.sat/.apt/.lmg/.ans`). The locked appearance-preview
  decision is **live-in-client via the real SWG engine**, NOT a standalone renderer (the path Sytner's
  IFF Editor took). See `docs/ai/toolchain-inventory.md` §"Maya → Blender export path".
- **Why this is a backlog item, not settled:** "Maya WRITE / authoring parity" is recorded as a
  *deferred post-V1 milestone that re-opens DEC-A3*. Promoting it means explicitly re-deciding the
  Utinni-vs-Blender boundary — only do so if Blender parity stalls or a format gap forces Utinni's hand.
  Default disposition remains **keep it Blender's lane**; this stub exists so the deferral is visible,
  not so we build it by default.

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)
