# Roadmap: Utinni

A one-stop, plugin-based modding tool for Star Wars Galaxies — an injected `UtinniCore` DLL + .NET
WinForms editor host + MEF plugin pipeline — that replaces the ~30 separate SOE-era editor apps with
one stable tool, and (as of v2.0) makes the whole asset pipeline AI-drivable.

## Milestones

- ✅ **v1.0 MVP — "Demo + CI green"** — Phases 1–11 (shipped 2026-06-01, `v1.0.0`)
- ✅ **v2.0 — "AI-Assisted SWG Tools"** — Phases 12–16 (shipped 2026-06-14, `v2.0`)
- 📋 **v2.1 — "Wave-2 Editors + Foundation Hardening"** — Phases 17–24 (planning, started 2026-06-14)

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

### 📋 v2.1 — "Wave-2 Editors + Foundation Hardening" (Phases 17–24) — PLANNING

Ship the Terrain `.trn` editor (Wave-2 #1) + a ClientEffect editor on a hardened rendering/toolchain
base, so the live-preview editors survive SWG Source's D3D9→D3D11 flip — plus the user-definable IFF
chunk-templates quick win. **Foundation-before-features:** the CppSharp/v145 hardening (17) and the
render-backend seam (18→19) land before the new editors. 17→18→19 is a strict foundation chain; the
Terrain codec (20) is offline and gated only on 17 (the critical path / only long pole); the user-visible
editors (21/22) ride their codecs plus the seam; the IFF-template quick win (23) is an independent P2.

- [x] **Phase 17: CppSharp / v145 Hardening** — clang-capability spike → harden-the-redirect + ABI-diff/MEF-compose gate (FOUNDATION; research-phase)
 (completed 2026-06-15)
- [x] **Phase 18: Render-Backend Seam + Dx9Backend** — carve `IRenderBackend`, D3D9 overlay behaviorally unchanged (FOUNDATION) (completed 2026-06-15)
- [x] **Phase 19: Dx11Backend + Config Detection + Resize** — DXGI `Present`/`ResizeBuffers` hooks, one-backend-per-session detect (FOUNDATION; research-phase) (all 3 plans code-complete + CI lanes green 2026-06-15; live-smoke D-22 / RNDR-02/03/04 acceptance DEFERRED to Phase 24 — UtinniCore's hardcoded RVAs crash on the from-source D3D11 client until it advertises its entry points)
- [ ] **Phase 20: Terrain `.trn` Codec + Verbs + MCP** — decode→navigable layer tree, typed tags, scalar-leaf edit/save, verbs-first (FEATURE / critical path; research-phase)
- [ ] **Phase 21: Terrain TJT SubPanel (+ best-effort live preview)** — consumes the Phase 20 codec; live regen-on-save rides the seam, degrades honestly (FEATURE)
- [ ] **Phase 22: ClientEffect Editor** — command-list `.iff` codec + verbs + MCP + SubPanel (FEATURE; lowest risk)
- [ ] **Phase 23: User-Definable IFF Chunk Templates** — schema-driven decode/encode + manage-from-UI (QUICK WIN, Backlog 999.2)
- [ ] **Phase 24: Client Entry-Point Advertisement (`GetEngineHookPoints`)** — SWG-Source client advertises its ~198 engine entry points; UtinniCore consumes them (dual-path), retiring hardcoded RVAs on that client; unblocks the 18/19 live-smokes on D3D11 (FOUNDATION; promotes Backlog 999.7 advertisement half; gated on external swg-client-v2 readiness; research-phase)

## Phase Details

### Phase 17: CppSharp / v145 Hardening
**Goal**: The binding-generation toolchain is an explicit, documented, CI-guarded configuration — and a binding regen can never silently break a pre-built plugin DLL.
**Depends on**: Nothing (first v2.1 phase; settles the build surface before any new native headers land)
**Requirements**: CPPS-01, CPPS-02, CPPS-03, CPPS-04
**Success Criteria** (what must be TRUE):
  1. A documented clang-capability spike result records, empirically, that no released CppSharp parses the MSVC v145 (14.5x) STL — so native-v145 binding generation is known-unreachable with current tooling and why (ClangSharp/Biohazrd/C++-CLI ruled out). The spike is the FIRST task and its outcome sets the rest of the phase's acceptance.
  2. The VS2019-14.29 parser-include redirect is documented in-repo as the *supported* binding-generation configuration — explicit, not silently load-bearing.
  3. CI fails fast on either unblock/regression signal: (a) UtinniCore C++ adopting a C++23 STL header the 14.29 redirect cannot parse, and (b) a CppSharp release shipping clang ≥20.
  4. A binding regen that changes the generated public C# surface is caught before inject: a per-block-hash ABI diff (separating a real ABI change from the known reorder churn) plus a frozen-DLL MEF-compose fixture gate the surface, and TJT/Sytner rebuild in lockstep (standing cross-repo authority).
**Plans**: 3 plans
- [x] 17-01-PLAN.md — CPPS-01 clang-capability spike + CPPS-02 supported-config doc (de-stale regen-bindings.md, Program.cs doc pointer)
- [x] 17-02-PLAN.md — CPPS-03 CI tripwires: C++23-header HARD-FAIL scan + clang-20 WARN-loud committed-pin
- [x] 17-03-PLAN.md — CPPS-04 ABI gate: per-block-hash diff + --rebless + frozen TJT MEF-compose fixture + lockstep rebuild
**Research-phase**: yes — the clang-capability spike outcome determines whether the phase is harden-redirect or harden+net9-modernization; acceptance criteria cannot be fixed until the spike resolves.

### Phase 18: Render-Backend Seam + Dx9Backend
**Goal**: The ImGui overlay renders through a single `IRenderBackend` seam, with the existing D3D9 path behaviorally unchanged — so a risky D3D11 twin can be added later without forking the shared overlay logic.
**Depends on**: Phase 17 (clean build surface only)
**Requirements**: RNDR-01
**Success Criteria** (what must be TRUE):
  1. The overlay still renders and takes input in a live D3D9 SWG session, verified by the existing live-smoke — behavior is preserved, not changed.
  2. A `render_backend.{h,cpp}` seam exists in `UtinniCore/swg/graphics/` (newFrame / renderDrawData / onPreResize / onPostResize / renderTargetWidth/Height); `directx9.cpp` is carved into a `Dx9Backend` behind it.
  3. The ~1000-line API-neutral overlay logic (WndProc subclass / Issue #11 chat-context routing, RT-space input mapping, gizmo, renderCallbacks bus) is single-sourced in `imgui_impl.cpp`; only the seam dispatch sites (the former DX9 touch-points: newFrame / renderDrawData + the scene depth/color/stage accessors per the A2 amendment) call through the seam.
  4. The carve preserves the no-Reset / Present-stretch D3D9 contract verbatim — no `Reset` is introduced and the third-party device is never destabilized during the refactor.
**Plans**: 2 plans
- [x] 18-01-PLAN.md — Define IRenderBackend seam (10-pure-virtual ABC: 6 ROADMAP-named + A2 color + A2 stage get/set pair) + non-virtual Dx9Backend::init + Dx9Backend wrapper + D-07 mock-dispatch test (Wave 1)
- [x] 18-02-PLAN.md — Carve imgui_impl onto the seam (D-05 full purge, setup(HWND)) + D-06 source gate + D-08 live-smoke (Wave 2)
**UI hint**: yes

### Phase 19: Dx11Backend + Config Detection + Resize
**Goal**: The ImGui overlay renders and maps input correctly when the SWG client runs Direct3D 11, with exactly one backend installed per session and resize handled the DXGI way.
**Depends on**: Phase 18 (the seam must be settled before adding the twin)
**Requirements**: RNDR-02, RNDR-03, RNDR-04
**Success Criteria** (what must be TRUE):
  1. The overlay renders and maps input in render-target space under D3D11 — `Dx11Backend` hooks `IDXGISwapChain::Present` (vtbl idx 8) with a per-frame backbuffer-RTV rebind. ⚠ **Acquisition method superseded (CONTEXT D-09/D-10, user-approved):** the hook points are obtained from the client-advertised `gl11_r.dll` `GetHookPoints()` export, NOT a throwaway `D3D11CreateDeviceAndSwapChain` harvest — the blind harvest survives only as the offline CI test (D-21 layer 3). The *outcome* (Present idx 8 / ResizeBuffers idx 13, per-frame RTV rebind) is unchanged.
  2. Exactly one render backend installs per session, auto-detected from the loaded `gl%02d_r.dll` (one `GetModuleHandle` check at install), with a one-shot diagnostic log — no doubled input, no dual ImGui contexts, no dummy-device leaks.
  3. The overlay survives a window resize under D3D11 — the RTV is released/recreated inside the `ResizeBuffers` hook (vtbl idx 13); no `DXGI_ERROR_INVALID_CALL` (the DXGI analog of the forbidden D3D9 `Reset`), and the D3D9 never-Reset/stretch rule is NOT carried verbatim into the DXGI path.
**Plans**: 3 plans
- [x] 19-01-PLAN.md — vcpkg dx11-binding enable + Dx11Backend seam decl + extend D-06 neutrality gate to DX11/DXGI + offset-pin/detection/mock-dispatch tests (Wave 1)
- [x] 19-02-PLAN.md — directx11.{cpp,h} DXGI hook tier (advertised GetHookPoints consumer, Present idx 8 / ResizeBuffers idx 13) + render_backend_dx11.cpp twin (per-frame RTV rebind, resize release/recreate) + WARP harvest test + CppSharp parse-stage decision (Wave 2)
- [x] 19-03-PLAN.md — one-backend-per-session detection at setup() + latched DX11 install poll + one-shot log + maintainer live-smoke RNDR-02/03/04 (D-22) (Wave 3) — Task 1 code-complete + CI lanes green; Task 2 live-smoke DEFERRED to Phase 24 (blocked by hardcoded-RVA crash on SwgClient_r.exe, not by this code)
**Research-phase**: yes — confirm FIRST (a) the final renderer-DLL contract (`gl11_r.dll` source-grounded vs `Direct3d11.dll`) and (b) hard-cutover vs runtime-switch. **Scope: 32-bit only.** x64 is explicitly OUT of v2.1 (user-locked 2026-06-14) — the `swg-client-v2` `x64bit-Upgrade` branch is a deliberate later milestone, paired with the entry-point-advertisement mechanism (Backlog 999.7), not a v2.1 risk. This D3D11 work targets the **32-bit** SWG-Source client. Entry-point discovery for v2.1 stays the current RVA/pattern-scan model on both clients; advertisement is future + SWG-Source-only.
**UI hint**: yes

### Phase 20: Terrain `.trn` Codec + Verbs + MCP
**Goal**: A modder (and an AI agent) can decode, navigate, edit, and byte-exactly save a procedural `.trn` TerrainGenerator graph through golden-tested CLI verbs and an MCP read tool, across both SWG lineages.
**Depends on**: Phase 17 (clean bindings only — NOT the D3D11 work; can parallelize with 18/19 if staffed; starting early de-risks the milestone)
**Requirements**: PROD-W2-TRN-01, PROD-W2-TRN-02, PROD-W2-TRN-03, PROD-W2-TRN-04
**Success Criteria** (what must be TRUE):
  1. A modder can open a `.trn` (from a TRE archive or loose override) and navigate its procedural layer tree (TGEN → Layers → Boundaries/Filters/Affectors/sub-layers) with names + active flags, and view the six shared palettes read-only.
  2. Common terrain tags (height/shader/color/flora affectors; circle/rect boundaries; height/slope filters) display as typed fields; unknown/long-tail tags degrade to a generic field list — never a hard decode failure (raw-fallback passthrough on unknown chunks).
  3. A modder can edit + save scalar/enum leaf values and toggle a layer/affector active flag, byte-exact, via the loose-override save matrix (`MutableIffDocument`/`IffWriter`, inheriting IFF no-pad behavior).
  4. `.trn` decode/edit/save is exposed as golden-tested `utinni-cli` verbs (`decode-trn` / `roundtrip-trn` / `apply-save-trn`, verbs-first per DEC-V2-VERBS-FIRST) + an MCP read tool, validated against a fixture matrix spanning BOTH SWGEmu and Restoration lineages (synthesized ≤200-byte fixtures, DEC-C3) — confirm the CommandLineParser verb-count ceiling registers cleanly first.
**Plans**: 4 plans
- [ ] 20-01-PLAN.md — Wave-0 scaffold (post-review): FULL per-tag low+high TGEN fixture matrix + pinnable TgenEraVersions (D-12) + test-only verb-ceiling assertion (no stub verb, D-11) + four Skip stubs + EARLY real-asset version discovery (D-13)
- [ ] 20-02-PLAN.md — Single-source TgenFieldLayouts descriptor table + TgenDecoder + Terrain/ model (Serialize): version-first typed decode, palette state machine (ambiguous-not-guessed), raw/truncated→non-editable, physical-path stable-ids, DEAD-skip, navigable model (TRN-01/02)
- [ ] 20-03-PLAN.md — TrnFieldEncoder (exact-byte-span on shared descriptor, float-bit policy) + ResolveFieldContext + verbs: navigable decode-iff TGEN branch, decode-trn/roundtrip-trn, field-aware apply-save-trn + CLI golden tests (TRN-03/04 verb half)
- [ ] 20-04-PLAN.md — MCP summarize_terrain (OOP, schema/exit-code tested) + opt-in LargeFixtures palette set + DEC-C3 gate with real-asset pin as PREREQUISITE (TRN-04 MCP half)
**Research-phase**: yes — `.trn` is the most variant-rich SWG format Utinni has tackled; port from a pinned `swg-client-v2/sharedTerrain` SHA (read-only), and the per-tag typed-coverage matrix + SWGEmu-vs-Restoration version dispatch need format research against `sharedTerrain`.

### Phase 21: Terrain TJT SubPanel (+ best-effort live preview)
**Goal**: A modder edits a planet's terrain inside The Jawa Toolbox and sees the change — live in-client where reachable, otherwise via an honestly-labeled save-then-reload.
**Depends on**: Phase 20 (consumes the codec); benefits from Phases 18/19 (live preview rides the backend seam)
**Requirements**: PROD-W2-TRN-05
**Success Criteria** (what must be TRUE):
  1. A `TerrainSubPanel` (`IEditorPlugin`) ships inside TJT (DEC-C4) with a layer tree + property grid, opens from the TRE Browser and from a loose override, and saves via the loose-override matrix.
  2. On save, the terrain change previews live in-client where a heap-free hot-path regen is reachable; where it is not (this build), it degrades to save-then-reload with explicit candor — never a standalone Utinni renderer (DEC-A3 + the live-in-client lock).
  3. The preview/regen path is heap-free on the hot path (stack-allocated `dispatchSnapshot`, push-on-edit not per-frame) so it never re-triggers the `0x0051fb0a` scene-change crash (Pitfall 6); the SubPanel ctor is guarded against MEF silent-reject and uses Dock.Fill-front-most / nested SplitContainers (Pitfall 8).
**Plans**: TBD
**UI hint**: yes

### Phase 22: ClientEffect Editor
**Goal**: A modder (and an AI agent) can open, edit, and byte-exactly save a ClientEffect `.iff` command list across both lineages — cheap adjacent reuse of the shipped Particle editor pattern.
**Depends on**: Phase 17 (the lowest-risk feature; the v2.0 Particle editor proved the exact three-layer pattern — no foundation chain dependency beyond clean bindings)
**Requirements**: PROD-W2-CFX-01, PROD-W2-CFX-02
**Success Criteria** (what must be TRUE):
  1. A modder can open a ClientEffect `.iff`, view/edit its command list (CreateAppearance / PlaySound / CreateLight / CameraShake / ForceFeedback / …), and save byte-exact via the loose-override matrix — an `EffectsSubPanel` ships inside TJT (DEC-C4).
  2. ClientEffect decode/edit/save is exposed as golden-tested `utinni-cli` `effect-*` verbs (verbs-first per DEC-V2-VERBS-FIRST) + an MCP read tool, with reference-validation against the load order, validated across BOTH SWGEmu and Restoration fixtures (IFF no-pad, multi-chunk variants; raw-fallback never hard-abort).
**Plans**: TBD
**UI hint**: yes

### Phase 23: User-Definable IFF Chunk Templates
**Goal**: A modder can describe an arbitrary IFF chunk's binary layout once and have Utinni auto-decode, display, edit, and byte-exactly re-encode any matching chunk — turning Utinni from "the formats we coded" into "any format a modder can describe."
**Depends on**: Nothing (independent P2 on `IffPayloadCursor`; no foundation dependency — slots wherever staffing allows)
**Requirements**: PROD-IFFT-01, PROD-IFFT-02, PROD-IFFT-03
**Success Criteria** (what must be TRUE):
  1. A modder can describe an arbitrary IFF chunk's binary layout (primitives, colors, vectors, quaternions, matrices, arrays, structs) as a named, reusable template.
  2. Utinni auto-applies a matching template to decode/display an otherwise-hex chunk and re-encodes edits byte-exact — round-trip verified, because the hidden risk here is encode parity, not decode.
  3. Templates are manageable (create / edit / save / select) from the IFF Editor UI.
**Plans**: TBD
**UI hint**: yes

### Phase 24: Client Entry-Point Advertisement (`GetEngineHookPoints`)
**Goal**: The from-source SWG-Source client (`SwgClient_r.exe`) advertises its own engine entry points to UtinniCore via a well-known export, so UtinniCore attaches to the D3D11 client with **zero hardcoded RVAs** on that client — unblocking the Phase 18 (D-08) and Phase 19 (D-22) live-smokes and live-preview on D3D11.
**Depends on**: Phase 19 (the DX11 render path this completes the attach story for) **+ EXTERNAL: the swg-client-v2 build reaching a stopping point where the export can land.** Parallel to Phases 20–23 — does NOT gate the offline Terrain critical path.
**Requirements**: EPA-01, EPA-02, EPA-03, EPA-04
**Success Criteria** (what must be TRUE):
  1. Injecting UtinniCore into `swg-client-v2/stage/SwgClient_r.exe` no longer crashes in `createDetours()` — the first detour (`config::loadOverride`, today hardcoded `(pLoadOverrideConfig)0x00401000`) resolves through the advertised contract. (This is the exact crash observed 2026-06-15: `VEH FATAL 0xC0000005 … READ target=0x00401000`, the first detour off `swg::config::detour()`.)
  2. The advertised contract is sourced in the swg-client-v2 build by compile-time symbol reference (`&fn`), versioned, and exported from the **exe** module; a coverage test asserts every UtinniCore-hooked `swg::*` endpoint (~198 across ~30 subsystems) has a populated pointer — zero missing.
  3. UtinniCore runs dual-path discovery: advertised contract on the SWG-Source client, hardcoded-RVA on SWGEmu (Pre-CU), auto-selected by detecting the export — no config toggle. The existing SWGEmu D3D9 live-smoke still passes unchanged (no regression to the working client).
  4. The Phase-19 DX11 overlay installs + renders on the advertised D3D11 client with the kickoff no longer gated on a hardcoded `graphics::install` address — closing the Phase 18 (D-08) and Phase 19 (D-22) live-smokes on `SwgClient_r.exe`.
**Plans**: TBD (create via /gsd:plan-phase when swg-client-v2 is ready) — likely ~4: (1) contract struct/table design + exe-side export skeleton + coverage test in swg-client-v2; (2) UtinniCore consumer/resolver + dual-path selection + retire literals behind a `swg::endpoints` accessor; (3) decouple the DX11 kickoff from the SWGEmu install hook; (4) live-smoke acceptance (D-08 + D-22).
**Design notes / open questions** (resolve in discuss/plan):
  - **Contract shape**: a single versioned struct of ~198 named pointers (explicit, greppable, but large + brittle) vs. a name→pointer table / `GetEngineEntryPoint(name)` lookup (data-driven, version-tolerant — a missing name degrades gracefully, self-documenting). Leaning table; settle in the spike.
  - **Single source of truth**: a generated header shared by both repos so the field/name list cannot drift between swg-client-v2's export and UtinniCore's consumer.
  - **Staging**: MVP = the subset needed to boot + render the overlay + the TJT-driven scene-change repro path (config/client/graphics/game/scene/cui/command_parser); full = all ~198. The coverage test makes "full" measurable.
  - **Cross-repo write boundary**: this phase requires WRITE access to swg-client-v2 source (adding the export) — crossing the "`swg-client-v2` is a read-only reference corpus" rule (`[[project_swg_client_v2_reference]]`). The user owns + actively builds swg-client-v2, so this is sanctioned, but it is a NEW write target distinct from the standing UtinniPlugins authority.
**Research-phase**: yes — the contract shape (struct vs table), the maintainable enumeration of the ~198 endpoints, and cross-build-suffix handling (`_r`/`_d`/`_o`) need a short design spike before plans fix acceptance. **Scope: 32-bit only** — x64 (999.7's other half) stays user-locked-deferred.

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
| 17. CppSharp / v145 Hardening | v2.1 | 3/3 | Complete    | 2026-06-15 |
| 18. Render-Backend Seam + Dx9Backend | v2.1 | 2/2 | Complete    | 2026-06-15 |
| 19. Dx11Backend + Detection + Resize | v2.1 | 3/3 | Complete    | 2026-06-16 |
| 20. Terrain `.trn` Codec + Verbs + MCP | v2.1 | 0/4 | Planned      | - |
| 21. Terrain TJT SubPanel | v2.1 | 0/? | Not started | - |
| 22. ClientEffect Editor | v2.1 | 0/? | Not started | - |
| 23. IFF Chunk Templates | v2.1 | 0/? | Not started | - |
| 24. Client Entry-Point Advertisement | v2.1 | 0/? | Not started | - |

**Shipped: 2 milestones, 16 phases (+02.1), 94 plans, 31 requirements (15 v1 + 16 v2.0).**
**In progress: v2.1 — 8 phases (17–24), 22 requirements, ~14% complete (3/8 phases code-complete).**

## Backlog

Unsequenced ideas parked for a future milestone (999.x). Promote with `/gsd:review-backlog`.

### Phase 999.2: User-definable IFF chunk templates (PULLED INTO v2.1 — Phase 23)

**Goal:** Let a modder *describe the binary layout* of an arbitrary IFF chunk (a schema of primitives,
colors, vectors, quaternions, matrices, arrays, structs) and have Utinni auto-decode/display/edit it —
so modders can crack `.iff` formats Utinni doesn't natively support, without code changes.

> **Pulled into v2.1 (2026-06-14):** this backlog item is now **Phase 23** (PROD-IFFT-01..03). Retained
> here for provenance until `/gsd:review-backlog` closes it.

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

### Phase 999.4: Complete VS2026 v145 toolset bump — CppSharp upgrade (PARTIALLY ADDRESSED in v2.1 — Phase 17)

**Goal:** Finish the toolchain bump so the binding generator (`UtinniCoreDotNetGen`) runs natively on
v145, removing the VS 2019 14.29 STL parser-include redirect (Path 1) currently keeping CppSharp's
clang 11 alive.

> **Partially addressed in v2.1 (2026-06-14):** **Phase 17** addresses this — but the v2.1 research
> (HIGH) confirmed the *retire-the-redirect* goal is **not achievable** (no released CppSharp ships
> clang 20 for v145). Phase 17 re-scopes to **harden-the-redirect + tripwires + ABI/MEF-compose gate**;
> the native-v145 / ClangSharp migration stays deferred (LARGE, plugin-ABI break) and remains parked here.

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

### Phase 999.5: D3D11 render-path migration (PULLED INTO v2.1 — Phases 18–19)

**Goal:** Add a parallel D3D11 hook/overlay path alongside the existing D3D9 one, so Utinni keeps
rendering its ImGui overlay + live-preview when the SWG client runs on D3D11.

> **Pulled into v2.1 (2026-06-14):** realized by **Phase 18** (render-backend seam + Dx9Backend carve)
> and **Phase 19** (Dx11Backend + config detection + DXGI resize). Retained here for provenance until
> `/gsd:review-backlog` closes it.

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

### Phase 999.7: Entry-point advertisement mechanism + x64 support (BACKLOG)

**Goal:** Replace brittle RVA/pattern-scan entry-point discovery with a cooperative contract where the
**SWG-Source-based client advertises its own entry points** to Utinni, and add 64-bit support — the two
are paired and sequenced after v2.1.

**Context (captured 2026-06-14, from the v2.1 milestone-flag discussion):**
- **Client-specific dual-path discovery.** The **old SWGEmu (Pre-CU) client keeps today's mechanism**
  (hardcoded RVAs + pattern-scan — Utinni doesn't control its build). The **SWG-Source client advertises**
  its entry points via a well-known contract — a call into a known entry point / small API, or a
  **build-time config sidecar** shipped alongside the executable. Feasible only because the user CAN touch
  the SWG-Source build. Re-opens the RVA-discovery model (`CON-H-03`, `docs/ai/rva-realignment.md`).
- **32-bit FIRST, x64 later (user-locked).** v2.1 stays x86-only; an x64 SWG client (the `swg-client-v2`
  `x64bit-Upgrade` branch) breaks the entire x86 injection stack, so x64 is a deliberate future milestone
  paired with this advertisement work — NOT a v2.1 surprise.
- See auto-memory `[[project_entrypoint_advertisement_mechanism]]`; relates to `[[project_d3d11_migration]]`.

> **Advertisement half promoted into v2.1 (2026-06-15):** the SWG-Source entry-point advertisement is now
> **Phase 24** (EPA-01..04 — `GetEngineHookPoints`), pulled forward to unblock the Phase 18/19 D3D11
> live-smokes (UtinniCore's hardcoded RVAs crash on the from-source `SwgClient_r.exe`). The **x64 half stays
> in this backlog item** (user-locked 32-bit-first); promote it later as its own phase paired with the
> `x64bit-Upgrade` branch.

Plans:
- [ ] TBD x64 half only (advertisement half → Phase 24); promote with /gsd:review-backlog when ready

### Phase 999.8: Remaining Wave-2 editors (BACKLOG)

**Goal:** Finish the Wave-2 interactive-editor set — the DCC-style TJT SubPanels not covered by v2.0
(WorldSnapshot, Particle) or v2.1 (Terrain, ClientEffect). The standing marker for "the rest of Wave-2."

**Context (captured 2026-06-14; source `docs/ai/toolchain-inventory.md` §"Replace-next Wave-2 candidates"):**
- Remaining by modder-demand order: **Animation** (`AnimationEditor`; `.lat`/`.ash` state machines,
  skeletal anim) → **Shaders/Textures** (`ShaderBuilder`, `CreateShaderTemplate`, `TextureBuilder`;
  `.dds`, shader templates — some are CLI-ish, candidates for revive+wrap) → **Sound** (`SoundEditor`) →
  **UI** (`UiBuilder`; UI `.iff`).
- Also finish the **Effects family** v2.1 started: **Lightning**, **Swoosh** (`clientParticle` lib; same
  pattern as the shipped Particle + v2.1 ClientEffect editors — no stack divergence).
- All follow the proven three-layer pattern (Formats codec → `utinni-cli` verb → MCP tool → TJT
  SubPanel) and the locked live-in-client preview decision (never a standalone renderer; DEC-A3).
- Split into per-editor phases when a future milestone scopes this (likely 1–2 editors per milestone).

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)

### Phase 999.9: Wave-3 plugins (BACKLOG)

**Goal:** The ecosystem/distribution layer beyond per-format editors — the standing marker for "Wave-3."

**Context (captured 2026-06-14; from the V2 boundary + PROJECT.md Out-of-Scope deferrals):**
- **Mod Manager** (enable/disable/order installed mods) · **Packager** (one-click mod packaging) ·
  **Community Hub** (publish/consume mods) · **Asset Diff** (cross-archive/version diff — overlaps the
  WIP TRE diff tool and Backlog 999.3; coordinate).
- These are tool/workflow surfaces, not asset-format codecs; several touch packaging + distribution
  policy (`CON-D-01`: never default users into a specific shard's infra). Scope per-plugin when a future
  milestone reaches the distribution layer.

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)

### Phase 999.10: Utinni installer / one-click onboarding app (BACKLOG)

**Goal:** A real end-user installer for **Utinni itself** — so a modder can download, run a setup, and be
ready to inject, instead of building from source + injecting by hand (maintainer-only today). Makes the
PROJECT.md Core Value ("downloads Utinni, installs once") literally true.

**Context (discussed earlier; first captured 2026-06-14):**
- **Distinct from 999.9's Packager** — 999.9 packages *mods*; this onboards *Utinni* (UtinniCore +
  The Jawa Toolbox + official plugins + the launcher/injector) as one installable bundle.
- **Handles prerequisites + config:** the .NET Framework runtime (TJT/UtinniCoreDotNet net4.7.2), the
  VC++ redist (UtinniCore x86), first-run SWG client-path selection, and the loose-override config — plus
  optional **auto-update**.
- **Honors distribution policy `CON-D-01`:** ship blank server host/port; never default a user into any
  specific shard's infrastructure.
- **Why a marker now:** the build-and-inject-only reality is the gate between "maintainer tool" and
  "distributable tool"; broad adoption needs a packaged installer. Tech options to weigh at scoping:
  Inno Setup / NSIS / WiX / MSIX. Pairs naturally with a Wave-3 / 1.0-public-release milestone.

Plans:
- [ ] TBD (promote with /gsd:review-backlog when ready)
