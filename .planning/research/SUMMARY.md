# Project Research Summary

**Project:** Utinni - v2.1 "Wave-2 Editors + Foundation Hardening"
**Domain:** Injected-DLL SWG modding tool (x86 in-process UtinniCore C++17/20 + .NET Framework WinForms host + CppSharp CLR bridge + MEF IEditorPlugin plugins; out-of-proc net10 CLI/MCP)
**Researched:** 2026-06-14
**Confidence:** HIGH for the render-backend + CppSharp/ABI findings (grounded in this project captured incidents + direct inspection of directx9.cpp and the swg-client-v2 D3D11 source); MEDIUM for the .trn/effects codec scope (format-shape verified against swg-client-v2/sharedTerrain, but no v2.1 fixtures exist yet).

## Executive Summary

v2.1 is a foundation-before-features milestone on an already-shipped system. Two new asset editors (procedural Terrain .trn and one adjacent Effects-family .iff) ride on top of two pieces of enabling debt that must land first: a parallel D3D11 render-path foundation (so the in-client overlay/live-preview survives the SWG client eventual D3D9->D3D11 flip) and a v145-toolset / CppSharp-bump reckoning. The most important conclusions are negative and scope-altering, and they should drive requirements re-scoping before the roadmapper commits to phase acceptance criteria.

The single biggest scope correction: the milestone named goal - "finish a REAL CppSharp upgrade so UtinniCoreDotNetGen runs natively on MSVC 14.5x STL, retiring the parser-include redirect" - is NOT achievable in v2.1. v145 STL hard-requires clang 20 (yvals_core.h STL1000 guard); no released CppSharp ships a clang newer than 19 (v1.2, reaching only 14.4x/v143). The correct v2.1 deliverable is therefore: a clang-capability spike FIRST -> harden the existing, working VS2019-14.29 parser redirect + add a C++23-STL-header tripwire + a clang-20-CppSharp release tripwire, with an OPTIONAL net9/10 generator-pipeline modernization scored separately (it still needs a redirect, just to 14.4x). Flag this for requirements re-scoping explicitly - framing the bump as "retires the redirect" sets an unmeetable acceptance criterion.

The D3D11 work is a parallel path selected by config, not a cutover: SWG loads its renderer as a swappable DLL gl%02d_r.dll keyed on ConfigClientGraphics::getRasterMajor() (5-7 -> D3D9 gl05_r.dll; 11 -> D3D11 gl11_r.dll), so backend detection is a one-call GetModuleHandle check and installing both hooks is harmful (double input, two ImGui contexts). The seam is a small IRenderBackend interface in UtinniCore/swg/graphics/; the ~1000-line API-neutral imgui_impl.cpp stays single-sourced. Terrain is the critical path and the only long pole - .trn is a procedural TerrainGenerator graph (NOT a heightmap), and v1 scope is deliberately bounded to decode->navigable tree + typed read of common tags + scalar-leaf edit/save. Effects is cheap adjacent reuse of the shipped Particle codec pattern, and both editors are zero-new-framework. The dominant risks are all already-captured Utinni incidents re-surfacing (D3D9 Reset on a third-party device, per-frame heap alloc crashing scene change, CppSharp regen silently breaking pre-built plugin DLLs, codecs aborting on unfixtured format variants) plus one milestone-existential external risk: the swg-client-v2 x64bit-Upgrade branch, which would break the entire x86 injection stack if it lands before/with D3D11.

## Key Findings

### Recommended Stack

The v2.1 stack delta is tiny: one new vcpkg feature flag, one new pair of C++ source files, and a build-config decision - not new third-party dependencies. Almost everything needed already ships (DetourXS, imgui 1.92.6 via vcpkg, CppSharp 0.10.5, the dummy-device D3D9 vtable-harvest pattern). The most important stack findings are negative: no released CppSharp reaches v145, and .trn needs no heightmap library.

**Core technologies (the actual deltas):**
- **imgui DX11 backend** (imgui_impl_dx11) - the renderer half of the D3D11 overlay; ships inside the already-vendored imgui 1.92.6. Add the dx11-binding vcpkg feature - no new dependency.
- **DetourXS (existing, vendored)** - detour IDXGISwapChain::Present (vtbl idx 8) + ResizeBuffers (idx 13) by-address, exactly as the 7 D3D9 hooks do. Do NOT add MinHook - it duplicates proven trampoline machinery.
- **DXGI / D3D11 SDK headers** (Windows SDK 10.0.19041+, already installed) - compile the new directx11.cpp; GetProcAddress("D3D11CreateDeviceAndSwapChain") to mirror the existing dynamic Direct3DCreate9 load.
- **CppSharp stays 0.10.5 (clang 11) + the VS2019-14.29 redirect for v2.1** - no released CppSharp parses v145; any move off forces UtinniCoreDotNetGen net4.7.2->net9/10 and still needs a redirect.
- **System.Drawing.Bitmap + LockBits (framework built-in)** - the only terrain-visualization library needed, and only if/when a 2D sampled-map preview lands. The .trn heightmap is generated, not stored; no 3D engine / mesh viewer / heightmap library (violates the locked live-in-client preview decision + DEC-A3).

### Expected Features

.trn is a serialized TerrainGenerator (top tag TGEN/PTAT): six shared palettes (Shader/Flora/Radial/Environment/Fractal/Bitmap) + a tree of Layers, each holding Boundaries (BCIR/BREC/BPOL/...) + Filters (FHGT/FSLP/...) + ~25 Affectors (AHCN/ASCN/AFSC/...) + nested sub-layers. The original SOE editor is a 100+-file MFC app - which is exactly why a full clone is NOT v1.

**Must have (table stakes):**
- Terrain: decode .trn -> navigable layer tree (TGEN -> Layers -> Boundaries/Filters/Affectors/sub-layers, names + active flags, six shared palettes read-only) - the headline; the bulk of the work; degrade the long-tail tags to a generic field list.
- Terrain: typed read for common tags (height/shader/color/flora affectors; circle/rect boundaries; height/slope filters) - generic field-list fallback for the rest.
- Terrain: edit + save scalar/enum leaves + active-toggle via the loose-override D-05 save matrix (MutableIffDocument/IffWriter) - earns editor.
- Terrain: open from TRE Browser / loose override - consistency via TreArchiveIndex + TrePayloadResolver.
- One effects editor (recommend ClientEffect - a flat command-list .iff): decode + edit + save + reference-validation + open-from-TRE - cheap adjacent reuse of the shipped Particle codec.
- (Foundation) D3D11 render-path + the v145/CppSharp hardening - enabling debt landed first so live preview survives the client renderer flip.

**Should have (competitive differentiators):**
- Terrain live-in-client regen on save - the marquee differentiator (no SOE tool edited a live planet); honestly degraded if a full live regen is not reachable (Particle precedent). NEVER a standalone Utinni renderer.
- MCP/CLI verb for .trn + effects - falls out of verbs-first (DEC-V2-VERBS-FIRST).
- Quick win 999.3 - TRE override/version-history view - composes on TreArchiveIndex/TrePayloadResolver/CotMasterIndex; a P2.
- Quick win 999.2 - user-definable IFF chunk templates - composes on IffPayloadCursor; byte-exact encode round-trip is the hidden risk; a P2.

**Defer (v1.x / v2.2+):**
- Terrain 2D sampled-map preview (needs the Sampler port) - high value, too big for v1.
- Terrain structural authoring / boundary painting - the full SOE 100-file surface; a milestone of its own.
- Terrain long-tail affector typed coverage (river/road/ribbon/environment/exclude/passable).
- Second + third effects editors (Lightning, Swoosh) - finish the clientParticle family later.

**Anti-features (locked out):** standalone Utinni terrain renderer / 3D fly-through; a C# reimplementation of the procedural generator for preview; editing baked heightmaps (terrain is procedural - there is none); server-side terrain regen (DEC-A1).

### Architecture Approach

The substrate is FIXED (swg::* shim -> utinni::* facade -> CppSharp CLR bridge -> MEF IEditorPlugin -> TJT WinForms host; tools/ CLIs -> utinni-cli verbs -> net10 Utinni.Mcp). v2.1 bolts on with zero new framework - both editors follow the exact three-layer pattern the v2.0 Particle editor proved: Formats/<X> codec -> Utinni.Cli verb (DEC-V2-VERBS-FIRST) -> Utinni.Mcp read tool -> TJT IEditorPlugin SubPanel. .trn is the heaviest codec but is IFF-structured, so Formats/Iff primitives carry it.

**Major components (deltas):**
1. UtinniCore/swg/graphics/render_backend.{h,cpp} (NEW) - a small IRenderBackend seam (newFrame/renderDrawData/onPreResize/onPostResize/renderTargetWidth/Height); detect-and-install one backend at hook-install time.
2. directx9.cpp -> Dx9Backend (carved) + directx11.cpp -> Dx11Backend (NEW) - hook fork, ImGui-backend fork, and resize-semantics fork live ONLY here (~4 backend calls + vtbl-harvest + resize). The 7 D3D9-only SWG detours (wireframe, depth-texture, s207_r.dll shader override) STAY in directx9.cpp with no D3D11 twin.
3. imgui_impl.cpp (carved, single-sourced) - WndProc subclass (Issue #11 chat-context routing), RT-space input mapping, gizmo, renderCallbacks bus all stay shared and API-neutral; only the four backend-touching lines call THROUGH the seam.
4. Formats/Terrain/ + Formats/Effects/ (NEW managed codecs) + Utinni.Cli trn-*/effect-* verbs (NEW) + Utinni.Mcp read tools (NEW, zero format logic) + TJT TerrainSubPanel/EffectsSubPanel (NEW).
5. UtinniCoreDotNetGen (MODIFIED) - CppSharp hardening; the OUTPUT keeps targeting net4.7.2 (the injected host is pinned to net4.7.2 x86 by the hosted CLR - it CANNOT move).

**Backend module-name reconciliation:** the two researcher leads named different modules to check. Architecture said Direct3d11.dll (the swg-client-v2 application project); Pitfalls said gl11_r.dll grounded in clientGraphics/Graphics.cpp:195-253 (gl%02d_r.dll loaded by getRasterMajor()). Prefer the source-grounded gl%02d_r.dll naming - GetModuleHandleA("gl11_r.dll") -> D3D11, else gl05/06/07_r.dll -> D3D9 - and check the Direct3d11.dll/d3d11.dll names as a fallback only. Confirm the final contract against swg-client-v2 before coding (it is actively churning).

### Critical Pitfalls

1. **D3D11 hook acquisition is fundamentally different from D3D9 - not a backend swap.** D3D9 patches shared .text in d3d9.dll; DXGI has no shared .text for Present. Hook IDXGISwapChain::Present (vtbl idx 8) acquired from a throwaway D3D11CreateDeviceAndSwapChain (hooking D3D11CreateDevice is too early). Rebind the backbuffer RTV every frame - the SWG flip-model device unbinds the RTV after Present.
2. **Detect the backend and install exactly ONE path.** Both hooks live = doubled input + two fighting ImGui contexts + dummy-device leaks. One GetModuleHandle("gl11_r.dll") check at install; log the detected backend once (30-second ground truth beats a multi-day why-is-input-doubled hunt).
3. **DXGI resize semantics are inverted from D3D9.** No Reset; resize is IDXGISwapChain::ResizeBuffers (idx 13). Release/recreate the cached RTV inside the ResizeBuffers hook - holding a stale buffer fails with DXGI_ERROR_INVALID_CALL, the DXGI analog of the forbidden D3D9 Reset crash. Do NOT carry the D3D9 never-Reset/stretch-the-window rules verbatim.
4. **A CppSharp bump silently changing generated public C# signatures detonates every pre-built plugin DLL at MEF compose** (MissingMethodException/CompositionException at inject). Gate with a per-block hash diff (separate real ABI change from the known reorder churn), rebuild TJT/Sytner in the SAME wave (standing cross-repo authority), add the frozen-DLL MEF-compose fixture, and live-smoke the inject (the only place this surfaces).
5. **The CppSharp upgrade not actually reaching v145 - the Path-2 dead-end.** Make the clang-capability spike the FIRST task; if no shipping CppSharp clears v145 14.5x STL (it will not), keep the redirect, document it as supported, and re-scope to harden-redirect + C++23-header-tripwire rather than sinking days into a TFM migration that does not unblock removal.
6. **Per-frame heap alloc in a live-preview/callback hook re-triggers the 0x0051fb0a scene-change crash.** Terrain regenerates on zone change, so a terrain-preview callback fires exactly during the fragile GroundScene construction window. Use the stack-allocated dispatchSnapshot (kInlineCap=16); push preview data on-edit, not per-frame; default to save-then-reload preview.
7. **A codec that aborts on unfixtured multi-chunk/version variants - the OT/IFF/TRE rework tax, x3 already paid.** Port from swg-client-v2/sharedTerrain (not guesswork); raw-fallback passthrough on unknown chunks (never hard-abort); golden-fixture BOTH SWGEmu and Restoration lineages; byte-exact roundtrip-* verb before the UI.

## Implications for Roadmap

Suggested ~6-phase structure, foundation-before-features. (1)->(2)->(3) is a strict foundation chain; (4) is offline and gated only on (1); (5)+(6) are the user-visible payoff. Quick wins (999.2/999.3) are independent P2s that slot anywhere staffing allows.

### Phase 1: CppSharp / v145 Hardening (clang-spike -> harden-redirect)
**Rationale:** FOUNDATION. Pure toolchain; settles the build surface before any new native (directx11.cpp) headers are added. Independent of everything else. Re-scope from the milestone stated retire-the-redirect goal - that is not achievable (no clang-20 CppSharp). The clang-capability spike is the FIRST task; the honest outcome is harden-the-redirect.
**Delivers:** documented clang-capability spike result; hardened + documented VS2019-14.29 parser redirect; C++23-STL-header CI tripwire; clang-20-CppSharp release tripwire; (optional, scored separately) net9/10 generator-pipeline modernization.
**Addresses:** the foundation half of the milestone goal.
**Avoids:** Pitfalls 4 (per-block hash gate + frozen-DLL fixture + lockstep TJT/Sytner rebuild - the primary acceptance gate) and 5 (spike-first, do not chase v145-native).

### Phase 2: Render-Backend Seam (carve) + Dx9Backend
**Rationale:** FOUNDATION. Refactor imgui_impl.cpp/directx9.cpp behind IRenderBackend with the EXISTING D3D9 overlay behaviorally unchanged - must precede Dx11 so the interface is settled. Depends on (1) only for a clean build.
**Delivers:** render_backend.{h,cpp} seam; Dx9Backend with the carve verified by the existing live-smoke (overlay still renders/inputs); the ~1000-line shared overlay logic single-sourced.
**Uses:** existing DetourXS + the dummy-device getVtbl() harvest.
**Implements:** the IRenderBackend component.
**Avoids:** the fork-imgui_impl.cpp anti-pattern; calling Reset/destabilizing the D3D9 device during the refactor (preserve the no-Reset, Present-stretch contract verbatim).

### Phase 3: Dx11Backend + Config-Based Backend Detection + Resize
**Rationale:** FOUNDATION completion. New directx11.cpp + detectAndInstall(). Stand up + test against OS d3d11.dll via the dummy-device harness even without a routine D3D11 SWG build. Depends on (2).
**Delivers:** Dx11Backend hooking IDXGISwapChain::Present (idx 8) + ResizeBuffers (idx 13); per-frame RTV rebind; release/recreate RTV inside ResizeBuffers; gl11_r.dll detection installing exactly one path with a one-shot diagnostic log.
**Uses:** imgui dx11-binding vcpkg feature; DXGI/D3D11 SDK headers.
**Avoids:** Pitfalls 1 (acquisition model), 2 (install-one), 3 (flip-model resize). Confirm hard-cutover vs runtime-switch AND the x64 question with swg-client-v2 FIRST.

### Phase 4: Terrain .trn Codec + Verbs + MCP Tool
**Rationale:** FEATURE core, the critical path / only long pole. Pure managed/offline - depends on (1) for clean bindings but NOT on the D3D11 work (could parallelize with 2-3 if staffed). Get decode right first; every other Terrain feature sits on it.
**Delivers:** Formats/Terrain/ codec (decode->navigable layer tree, typed read for common tags, scalar-leaf edit/save) + decode-trn/roundtrip-trn/apply-save-trn verbs + MCP read tool, golden-tested across SWGEmu + Restoration fixtures.
**Uses:** ported swg-client-v2/sharedTerrain (pinned SHA, read-only); Formats/Iff primitives.
**Avoids:** Pitfall 7 (raw-fallback on unknown chunks; both lineages fixtured; verbs-first byte-exact round-trip; inherit IFF no-pad behavior).

### Phase 5: Terrain TJT SubPanel (+ optional live-in-client preview)
**Rationale:** FEATURE. TerrainSubPanel consumes the (4) codec; optional live in-client regen-on-save rides the (2)/(3) backend seam - the payoff for foundation-first. Depends on (4); benefits from (2)/(3).
**Delivers:** TerrainSubPanel (IEditorPlugin) with layer tree + property grid; open-from-TRE; loose-override save; live preview honestly degraded if not heap-free-reachable.
**Avoids:** Pitfall 6 (heap-free hot path; save-then-reload default), 8 (Dock.Fill front-most / nested SplitContainers; guard the ctor against MEF silent-reject), 9 (vanilla-baseline-first before live-smoke bisects).

### Phase 6: One Adjacent Effects Editor (codec + verb + SubPanel)
**Rationale:** FEATURE, lowest risk, pattern fully proven by now. Recommended target ClientEffect (flat command-list .iff); Lightning/Swoosh are alternates - the choice is a requirements-scoping user call with no stack divergence.
**Delivers:** Formats/Effects/ codec + effect-* verbs + MCP tool + EffectsSubPanel, with reference-validation against the load order.
**Avoids:** Pitfalls 7 (IFF no-pad, multi-chunk variants), 8 (SubPanel layout/MEF).

### Phase Ordering Rationale
- (1)->(2)->(3) is a strict foundation chain: clean build surface -> settle the seam with the SAFE D3D9 carve (verifiable by existing live-smoke) -> add the RISKY D3D11 twin last. This is the milestone explicit foundation-before-features intent.
- (4) is offline and gated only on (1) - Terrain codec can start the moment the bump lands; it is the critical path, so starting it early de-risks the milestone.
- (5)+(6) are the user-visible payoff and depend on their codecs plus (for live preview) the backend seam - which is precisely why foundation goes first: live preview breaks the moment the client flips to D3D11 if the seam is not there.
- Quick wins (999.2/999.3) are independent P2s - they touch only existing IFF/TRE Formats + UI, no foundation dependency, slot anywhere.

### Research Flags

Phases likely needing /gsd:plan-phase --research-phase <N> during planning:
- **Phase 1 (CppSharp):** the clang-capability spike outcome determines whether the phase is harden-redirect or harden+net9-modernization - needs a spike before acceptance criteria can be fixed.
- **Phase 3 (Dx11Backend):** depends on an externally-churning swg-client-v2 Direct3d11.dll/gl11_r.dll contract; the hard-cutover-vs-runtime-switch and x64 questions need confirmation before design lock.
- **Phase 4 (Terrain codec):** .trn is the most variant-rich SWG format Utinni has tackled; the per-tag typed-coverage matrix and the SWGEmu-vs-Restoration version dispatch need format research against sharedTerrain.

Phases with standard patterns (skip research-phase):
- **Phase 2 (seam carve):** pure refactor of read-in-full source behind a thin interface; behavior-preserving, well-understood.
- **Phase 5 / Phase 6 (SubPanels + effects editor):** the Particle editor proved the exact three-layer pattern; ClientEffect is a flat command list.

### Watch Out For (this project captured pitfalls - carry forward into every phase)
- Never call D3D9 Reset on the SWG third-party device (DEVICELOST + crash); the seam preserves no-Reset/Present-stretch verbatim.
- RT-space input mapping + AddMousePosEvent (imgui 1.87+) - the embedded-window-stretch mapping is API-neutral and stays shared.
- Heap-free callback hot paths - dispatchSnapshot stack-snapshot pattern; never per-frame std::vector/new/std::string on the render/update thread.
- Generated/UtinniCore.cs reorders every build - always git checkout -- it, never commit; and a REAL ABI break can hide inside that reorder churn (the Pitfall-4 per-block hash gate exists to catch it).
- Byte-exact round-trip across BOTH SWGEmu and Restoration fixtures - the user mods both clients; one-file coverage is not coverage.
- Vanilla-baseline-first before any live-smoke bisect - priority-27 searchPath_NN_27 loose overrides shadow data machine-wide (the 06-12 phantom-walk).

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | D3D11 backend + CppSharp state verified (Context7 + repo + upstream nuget/github 2026-06-14); terrain-visualization recommendation is a design call (no single canonical library), graded MEDIUM within an otherwise HIGH section. |
| Features | HIGH | Format facts grounded in swg-client-v2/sharedTerrain/clientEffect/clientParticle + the original SOE TerrainEditor; LOW only on relative modder-demand ranking within the effects family. |
| Architecture | HIGH | All integration points verified against current D:/Code/Utinni + swg-client-v2 source; the D3D11 runtime-detection design is MEDIUM (depends on a churning swg-client-v2 renderer-DLL contract). |
| Pitfalls | HIGH | Render-path + CppSharp/ABI pitfalls anchored to this project captured incidents + direct source inspection; .trn/effects codec pitfalls MEDIUM (format-shape verified, no v2.1 fixtures yet). |

**Overall confidence:** HIGH

### Gaps to Address
- No released CppSharp reaches v145 (clang 20). The milestone retire-the-redirect goal must be re-scoped at requirements time to harden-the-redirect + tripwires. Resolve via the Phase-1 clang-capability spike before fixing acceptance criteria.
- swg-client-v2 D3D11 renderer-DLL contract is actively churning. Confirm before Phase 3: (1) final module name (gl11_r.dll source-grounded vs Direct3d11.dll), (2) hard-cutover vs runtime-switch, (3) whether the x64bit-Upgrade branch lands before/with D3D11 - an x64 SWG client breaks the ENTIRE x86 injection stack, not just the overlay (milestone-existential, confirm early).
- No v2.1 .trn/effects fixtures exist yet. Build synthesized <=200-byte fixtures (DEC-C3) across BOTH SWGEmu and Restoration lineages during Phase 4/6; coverage must be a fixture matrix, not one-file.
- CommandLineParser verb-count ceiling. The tree is at 23 *Command.cs files (prior cap was 16); confirm the dispatcher registers cleanly before adding trn-*/effect-* verbs.
- 2D sampled-map preview needs a Sampler port (deferred). If/when it lands, System.Drawing.Bitmap + LockBits suffices - no new dependency.

## Sources

### Primary (HIGH confidence)
- /ocornut/imgui (Context7) - DX11 backend Init/NewFrame/RenderDrawData contract; ID3D11Device*+ID3D11DeviceContext* init signature.
- nuget.org/packages/CppSharp + github.com/mono/CppSharp/releases (2026-06-14) - latest v1.2 = clang 19 (reaches 14.4x only); no clang-20/v145 release; latest TFM net9.0, no net472/x86.
- D:/Code/Utinni/UtinniCore/swg/graphics/directx9.cpp + swg/ui/imgui_impl.cpp - dummy-device vtable-harvest + DetourXS DETOUR_TYPE_PUSH_RET pattern; the 7 D3D9 detours; hkReset bracket; s207_r.dll guard (read in full).
- D:/Code/swg-client-v2/.../clientGraphics/.../Graphics.cpp:195-253 - gl%02d_r.dll backend selection by getRasterMajor() (5-7 D3D9, 11 D3D11).
- D:/Code/swg-client-v2/.../Direct3d11/.../Direct3d11_Device.cpp - D3D11CreateDevice + CreateSwapChainForHwnd, DXGI_SWAP_EFFECT_FLIP_DISCARD, RTV-unbind-after-Present, DEVICE_REMOVED = restart.
- D:/Code/swg-client-v2/.../sharedTerrain/.../generator/TerrainGenerator.h + Affector*/Boundary/Filter/*Group + SamplerProceduralTerrainAppearance (SHA d6496005e) - .trn procedural structure + tag set; clientEffect/ClientEffectTemplate.h + clientParticle/{Lightning,Swoosh}AppearanceTemplate.h.
- D:/Code/Utinni/UtinniCoreDotNet/Formats/Particle/* - the proven Wave-2 codec/panel template; Formats/{Iff,Tre}/* the reuse surfaces; Utinni.Cli/Commands/* (23 verbs) + Utinni.Mcp/Tools/*.
- Utinni captured incidents (auto-memory) - feedback_d3d9_reset_third_party, feedback_imgui_embedded_d3d9_rt_space, feedback_caller_attrs_binary_compat, project_utinnicore_cs_regen_churn, project_vs2026_cppsharp_block, project_rh_snapshot_no_heap_alloc, project_ot_multichunk_list_params, project_swg_iff_no_pad, project_tre_version_support_gap, project_swg_client_loose_overrides, feedback_winforms_dockfill_zorder.
- .planning/PROJECT.md (milestone scope, CON-H/N/M/T, DEC-V2/DEC-A/DEC-C locks) + docs/ai/toolchain-inventory.md (revive/replace cross-walk, Wave-2 census, locked live-in-client preview).

### Secondary (MEDIUM confidence)
- RenderHook / DX11-ImGui-HookKit / Niemand DX11+ImGui writeup - DXGI Present=vtbl 8, ResizeBuffers=vtbl 13, dummy-device harvest (community, multi-source agreement).
- TRN (FileFormat) SWGANH Wiki + PCG Wiki - .trn = procedural graph not stored heightmap (cross-checked against on-disk source).
- swg-client-v2/.../application/Direct3d11/ existence (HIGH for existence; MEDIUM for final contract - actively churning per .planning/research/CONSULT-19..23).

### Tertiary (LOW confidence)
- Relative modder-demand ranking within the effects family (ClientEffect vs Lightning vs Swoosh) - a requirements-scoping user call, no stack divergence either way.

---
*Research completed: 2026-06-14*
*Ready for roadmap: yes*
