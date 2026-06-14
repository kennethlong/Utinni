# Architecture Research

**Domain:** SWG modding framework — v2.1 "Wave-2 Editors + Foundation Hardening" milestone integration
**Researched:** 2026-06-14
**Confidence:** HIGH (all integration points verified against current source in `D:/Code/Utinni` and `D:/Code/swg-client-v2`; D3D11 runtime-detection design is MEDIUM — depends on a swg-client-v2 renderer-DLL contract that is still actively churning)

> Scope note: This is a SUBSEQUENT-milestone architecture study. The substrate (`swg::*` shim → `utinni::*` façade → CppSharp CLR bridge → MEF `IEditorPlugin` → TJT WinForms host; detour-table pattern; `tools/` revived CLIs → `utinni-cli` verbs → net10 `Utinni.Mcp`) is treated as FIXED. This document covers only how the four v2.1 features bolt onto it, what is new vs modified, and the build order.

---

## Standard Architecture (current substrate, annotated for v2.1 touch points)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  SWG.exe process (x86, injected)                          ★ = v2.1 touch point │
│                                                                                │
│  ┌──────────────────────────────────────────────────────────────────────────┐ │
│  │ UtinniCore.dll (native C++17/20, v145)                                    │ │
│  │                                                                           │ │
│  │  createDetours()  (utinni.cpp:58 — single detour registry)               │ │
│  │     │                                                                     │ │
│  │     ├── swg/graphics/directx9.cpp  ── 7 vtbl detours + dummy-device       │ │
│  │     │     getVtbl() harvest + ImGui_ImplDX9 + RT-space stretch    ★ FORK  │ │
│  │     │                                                                     │ │
│  │     ├── swg/ui/imgui_impl.cpp ── ImGui ctx, WndProc subclass,             │ │
│  │     │     RT-space input mapping, gizmo, renderCallbacks bus      ★ SHARE │ │
│  │     │                                                                     │ │
│  │     └── swg/scene/terrain.cpp ── RVA shim (time-of-day/weather)   ★ EXTEND │ │
│  │                                                                           │ │
│  │  clr.cpp → ExecuteInDefaultAppDomain ─────────────────────────┐          │ │
│  └───────────────────────────────────────────────────────────────┼──────────┘ │
│                                                                   ▼            │
│  ┌──────────────────────────────────────────────────────────────────────────┐ │
│  │ UtinniCoreDotNet.dll (managed, net4.7.2 x86)                              │ │
│  │   Generated/UtinniCore.cs  ◀── CppSharp (UtinniCoreDotNetGen)     ★ BUMP  │ │
│  │   Formats/{Iff,Tre,Datatable,StringTable,ObjectTemplate,Particle}        │ │
│  │           + Formats/Terrain/  + Formats/Effects/                  ★ NEW   │ │
│  │   PluginFramework (MEF IEditorPlugin SPI)                                 │ │
│  └──────────────────────────────────────────────────────────────────────────┘ │
│                                                                   │            │
│  ┌──────────────────────────────────────────────────────────────────────────┐ │
│  │ The Jawa Toolbox (UtinniPlugins repo) — IEditorPlugin host               │ │
│  │   TerrainSubPanel  + EffectsSubPanel  (Wave-2 editors)           ★ NEW   │ │
│  └──────────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────────┘

   OUT-OF-PROCESS (net10):  Utinni.Cli  ──verbs──▶  Utinni.Mcp  (DEC-V2-VERBS-FIRST)
     ★ NEW: trn-* / effect-* verbs in Utinni.Cli/Commands/, mirrored as MCP read tools
```

### Component Responsibilities (v2.1 deltas only)

| Component | Responsibility in v2.1 | New / Modified |
|-----------|------------------------|----------------|
| `UtinniCore/swg/graphics/directx11.cpp` | Parallel DXGI swapchain hook (`Present`/`ResizeBuffers`) mirroring `directx9.cpp`; harvests the D3D11 swapchain+device-context vtables via dummy-device | **NEW** |
| `UtinniCore/swg/graphics/render_backend.{h,cpp}` | Backend-detection + dispatch seam: a small `IRenderBackend`-style C++ interface (`newFrame`/`renderDrawData`/`onResize`) with two impls (dx9, dx11); selected once at hook-install time | **NEW** |
| `UtinniCore/swg/ui/imgui_impl.cpp` | All ImGui logic ABOVE the backend line — context, WndProc subclass, RT-space input mapping, gizmo, `renderCallbacks` bus — stays single-sourced; only the `ImGui_ImplDX9_*` calls move behind the backend seam | **MODIFIED** (carved, not forked) |
| `UtinniCore/swg/scene/terrain.cpp` | Extend RVA shim with terrain-read accessors the live-preview path needs (heightmap/layer/reload queries) | **MODIFIED** |
| `UtinniCoreDotNetGen/Program.cs` | CppSharp upgrade: retire the VS2019-14.29 parser-include redirect; possibly bump generator TFM net4.7.2→net9/10 + CppSharp 0.10.5→1.x | **MODIFIED** |
| `UtinniCoreDotNet/Formats/Terrain/` | `.trn` codec (IFF-container based) — parse/mutate/write, golden-tested | **NEW** |
| `UtinniCoreDotNet/Formats/Effects/` | ClientEffect/Lightning/Swoosh codec (IFF-based) | **NEW** |
| `Utinni.Cli/Commands/{DecodeTrnCommand,RoundtripTrnCommand,...}.cs` | `utinni-cli` verbs for `.trn` + effect formats (DEC-V2-VERBS-FIRST) | **NEW** |
| `Utinni.Mcp/Tools/` | Thin read-tool wrappers over the new verbs (zero format logic — DEC-V2-MCP-OOP) | **NEW** |
| TJT `TerrainSubPanel` / `EffectsSubPanel` (UtinniPlugins repo) | `IEditorPlugin` SubPanels consuming the Formats codecs | **NEW** |

---

## (a) The D3D11 abstraction seam — concrete recommendation

### Where the seam belongs: a small render-backend interface in UtinniCore, NOT a hook-fork and NOT an ImGui-backend-swap-only

There are three candidate seams. The evidence rules out two of them:

**Rejected — "separate D3D11 hook installs the same overlay" (hook-layer-only seam).**
This is the shape the stale `project_d3d11_migration` memory sketched ("parallel `directx11.cpp` … `imgui_impl_dx11` alongside"). It works for the *hook* but leaves a fork hazard: `imgui_impl.cpp` is ~1000 lines of SHARED, hard-won logic — the WndProc subclass (Issue #11 chat-context routing), the RT-space input mapping (`feedback_imgui_embedded_d3d9_rt_space`), the gizmo, the `renderCallbacks` bus, the `WantCaptureMouse` game-input arbitration. None of that is D3D9-specific. If the D3D11 path stands up its own copy of that overlay, every future input/overlay fix lands twice. **Reject as the primary seam.**

**Rejected — "ImGui-backend-swap layer only."**
ImGui already has the swap built in (`ImGui_ImplDX9_*` vs `ImGui_ImplDX11_*` take `IDirect3DDevice9*` vs `ID3D11Device*`+`ID3D11DeviceContext*`). But the swap point is not just the ImGui backend init/render calls — it's also *where you hook* (D3D9 device vtable Present vs DXGI swapchain Present), *what you harvest* (one device vtable vs swapchain+context vtables), and *resize semantics* (D3D9: never Reset a third-party device, stretch via Present — `feedback_d3d9_reset_third_party`; D3D11: `IDXGISwapChain::ResizeBuffers`, a genuinely different contract). An ImGui-only swap doesn't cover the hook or resize halves. **Reject as insufficient.**

**Recommended — a thin `IRenderBackend` seam in `UtinniCore/swg/graphics/`, with `imgui_impl.cpp` calling THROUGH it.**

Introduce `render_backend.h`:

```cpp
namespace swg::render_backend {
struct IRenderBackend {
    virtual ~IRenderBackend() = default;
    virtual bool installHooks() = 0;            // harvest vtbl + Detour::Create the present/resize entries
    virtual void newFrame() = 0;                // ImGui_Impl<DX>_NewFrame()
    virtual void renderDrawData() = 0;          // ImGui_Impl<DX>_RenderDrawData(GetDrawData())
    virtual void onPreResize() = 0;             // DX9: InvalidateDeviceObjects; DX11: release RTV before ResizeBuffers
    virtual void onPostResize() = 0;            // DX9: CreateDeviceObjects;     DX11: recreate RTV
    virtual int  renderTargetWidth() = 0;       // backbuffer dims for RT-space mapping
    virtual int  renderTargetHeight() = 0;
};
IRenderBackend* get();        // returns the one detected backend
void detectAndInstall();      // called once from createDetours()
}
```

- `directx9.cpp` becomes `Dx9Backend : IRenderBackend` — its existing `detour()` body, `getVtbl()` harvest, and the `ImGui_ImplDX9_*` calls move behind the vtable interface with **near-zero behavior change**. The 7 SWG-specific vtable detours (`DrawIndexedPrimitive` wireframe, `SetRenderTarget`, depth-texture, `s207_r.dll` shader override) STAY in `directx9.cpp` — they are D3D9-only SWG-engine concerns, not overlay concerns, and there is no D3D11 equivalent to share. The `IRenderBackend` interface covers ONLY the overlay-render + resize + RT-dims surface.
- `directx11.cpp` is the new `Dx11Backend : IRenderBackend`. It harvests the DXGI swapchain `Present`/`ResizeBuffers` vtable entries (same dummy-device-then-snapshot trick `getVtbl()` uses for D3D9 — create a throwaway swapchain via `D3D11CreateDeviceAndSwapChain`, snapshot `IDXGISwapChain`'s vtable, release), `Detour::Create`s them, and routes through `ImGui_ImplDX11_*`. Resize uses `ResizeBuffers` semantics, not the D3D9 stretch hack.
- `imgui_impl.cpp` keeps `setup()`, `render()`, the WndProc subclass, RT-space input mapping, gizmo, and the callback bus EXACTLY as-is, except the four backend-touching lines (`ImGui_ImplDX9_NewFrame`, `_RenderDrawData`, `_InvalidateDeviceObjects`, `_CreateDeviceObjects`) become `render_backend::get()->newFrame()` / `->renderDrawData()` / `->onPreResize()` / `->onPostResize()`. The RT-space stretch math is BACKEND-AGNOSTIC (it maps client→backbuffer regardless of API), so it stays in `imgui_impl.cpp` untouched.

**Shared vs forked vs D3D9-only:**

| Concern | Disposition |
|---------|-------------|
| ImGui context, fonts, style, `CreateContext` | SHARED (`imgui_impl.cpp`, API-agnostic) |
| WndProc subclass + chat-context routing (Issue #11) | SHARED |
| RT-space input mapping + `WantCaptureMouse` arbitration | SHARED (math is backbuffer-relative, API-neutral) |
| `renderCallbacks` bus, gizmo (`imgui_gizmo`) | SHARED |
| Hook install (vtbl harvest + `Detour::Create`) | FORKED behind `IRenderBackend::installHooks()` — different vtables |
| ImGui backend init/newframe/render | FORKED behind the seam (`ImGui_ImplDX9_*` vs `_DX11_*`) |
| Resize handling | FORKED (Present-stretch vs `ResizeBuffers`) |
| `DrawIndexedPrimitive` wireframe, `SetRenderTarget`, depth-texture, `s207_r.dll` shader override | D3D9-ONLY — stays in `directx9.cpp`, no seam, no D3D11 twin |

### Runtime detection: which renderer did the SWG client load?

SWG's engine loads its renderer as a **named plugin DLL** (the historical `Direct3d9.dll` model). `swg-client-v2`'s in-progress migration ships a sibling **`Direct3d11.dll`** (verified: `swg-client-v2/src/engine/client/application/Direct3d11/` builds `Direct3d11.vcxproj`). This is the detection hook:

```cpp
void render_backend::detectAndInstall() {
    if (GetModuleHandleA("Direct3d11.dll") || GetModuleHandleA("d3d11.dll"))
        backend = new Dx11Backend();
    else if (GetModuleHandleA("d3d9.dll"))               // stock SWGEmu + Direct3d9.dll path
        backend = new Dx9Backend();
    else { /* log::critical, install nothing — overlay disabled, game still runs */ }
    if (backend) backend->installHooks();
}
```

Mirror the existing null-checked, log-and-continue discipline (`directx9.cpp::getVtbl()` already does this for `d3d9.dll`-not-loaded). Call `detectAndInstall()` from `createDetours()` in place of the current direct `directX::detour()` call.

**Detection timing caveat (the real risk):** `getVtbl()`'s comment notes Utinni injects "after the game has bootstrapped its render subsystem," so by hook-install time the renderer DLL is already mapped — module-presence detection is sound. BUT if a build supports runtime renderer switching (D3D9↔D3D11 via Options menu) the install-time detection won't follow a switch. The stale memory flagged this exact question and it is still unresolved. **Open question to confirm with swg-client-v2:** is the D3D11 SWG client a hard cutover (one renderer per launch, chosen at engine init from config) or a runtime-selectable swap? Hard-cutover (almost certainly the case) makes the install-time seam correct and simple; runtime-switchable would force a re-detect-on-device-recreate path. **Recommend: design for hard-cutover, log loudly if both DLLs are resident, defer runtime-switch support to a later milestone.**

**Foundation, not feature:** the seam is pure refactor + parallel path; it ships with the existing D3D9 overlay behaviorally unchanged (the Dx9Backend carve-out is verifiable by the existing live-smoke). The Dx11Backend can be stood up and tested even before a D3D11 SWG build is routinely available, because the dummy-device harvest works against the OS `d3d11.dll` the same way `getVtbl()` works against OS `d3d9.dll` in the xUnit harness today.

---

## (b) CppSharp / TFM bump — blast radius (honest assessment)

The bump has two separable sub-changes with very different blast radii.

### Sub-change 1 — retire the parser-include redirect / upgrade CppSharp (the generator's problem)

Today (per `project_vs2026_cppsharp_block`): the C++ builds at v145, but CppSharp 0.10.5's clang-11 parser can't read MSVC 14.5x STL, so `UtinniCoreDotNetGen` points its PARSER includes at VS2019 14.29 STL (`ConfigureCppSharpParserStl()`) while the build LINKS v145. Generator-time-only redirect.

**Blast radius: contained to the binding-generation toolchain. Does NOT ripple into the injected runtime.**

- The generator (`UtinniCoreDotNetGen`, net4.7.2) runs at build time only; it is NOT loaded into SWG.exe. Changing its TFM or CppSharp version cannot, by itself, change the injected client's behavior.
- The OUTPUT — `Generated/UtinniCore.cs` — is what matters for runtime. The bump's real risk is **codegen drift**: a newer CppSharp may emit different P/Invoke signatures/marshalling for the same headers. The existing structural-binding-diff harness (the "119/119 partial-class blocks byte-identical" check from the v142→v145 validation) is the right gate: re-run it across the bump and require the DllImport set semantically identical.
- Watch item from the memory: the 14.29 parser-pin fails the moment Utinni C++ adopts a C++23 STL header (`<format>`, `<ranges>`, `<expected>`, `<span>`, etc.). v2.1's D3D11 work is plain C++ + Win32/DXGI — low risk of pulling those in, but flag it if the Dx11Backend reaches for `<span>`/`<expected>`. (Note: a successful CppSharp upgrade that reaches v143+ STL would partly RELAX this pin — Path 2 in the memory.)
- `project_utinnicore_cs_regen_churn`: every build re-emits `Generated/UtinniCore.cs` in non-deterministic order (huge symmetric no-op diff). Unchanged by the bump; must keep being `git checkout --`'d, never committed. A clean bump should re-baseline the file ONCE (as the v142→v145 work did at `d69988d`) then discard churn.

### Sub-change 2 — TFM bump net4.7.2 → net9/10 for the generator

**Blast radius: ALSO contained to the generator — IF AND ONLY IF the bump is scoped to `UtinniCoreDotNetGen` and does not touch `UtinniCoreDotNet`.**

This is the critical distinction the milestone framing must get right:

- `UtinniCoreDotNet` (the injected managed host) is **pinned to net4.7.2 x86 by CON-N/M and the hosted CLR** (`clr.cpp` boots `v4.0.30319`). It CANNOT move to net9/10 — a hard injection constraint, not a preference. The Path-2 note in `project_vs2026_cppsharp_block` explicitly couples "upgrade CppSharp to v1.2" with "net4.7.2 → net9.0 migration of `UtinniCoreDotNetGen`" — i.e. only the GENERATOR migrates, never the runtime host.
- Supported shape: generator on modern .NET (to host a newer CppSharp that ships a newer clang), emitting C# that still TARGETS net4.7.2 (the OUTPUT TargetFramework is a CppSharp option independent of the generator's own TFM).

### The binary-compat landmine (must be called out)

`feedback_caller_attrs_binary_compat`: any change to a PUBLIC method signature in `UtinniCoreDotNet.dll` that is binary-breaking (added/removed/renamed overloads, added `[Caller*]` defaults) throws `MissingMethodException`/`CompositionException` at MEF compose-time against PRE-BUILT plugin DLLs in `kennethlong/UtinniPlugins` (TJT). A CppSharp bump that changes generated P/Invoke signatures is exactly this class of change.

- **Mitigation (in-scope for the bump phase):** rebuild TJT (`TheJawaToolboxDotNet`) in the SAME commit/wave as the bump (standing cross-repo authority exists per `feedback_utinniplugins_authority`; paired cross-repo commits need no human checkpoint — only the live-SWG smoke does), and add the long-noted-but-absent "frozen old-signature plugin DLL still loads" xUnit ABI-watchdog fixture.
- **Net:** the bump is generator-internal, but its OUTPUT is an ABI surface — treat the regen as a deliberate ABI bump, gate it with the structural-diff harness AND a plugin-load fixture, and rebuild TJT in lockstep.

### CI ripple

- CI builds `Utinni.sln` on the self-hosted v145 runner (`project_self_hosted_ci`). A generator TFM bump means the runner needs the net9/10 SDK present (it builds the generator as a build step). Low effort, a runner-provisioning step.
- The `dotnet build` MSB3823-on-resx limitation (`feedback_dotnet_build_msbuild_resources`) is unaffected — managed build stays MSBuild-then-`dotnet test --no-build`.

---

## (c) Terrain + effects editors — confirmed: pure pattern reuse, NO new framework

Both editors slot cleanly into the established three-layer pattern with zero new infrastructure. Verified against the live Particle (`.prt`) editor that shipped in v2.0 — it is the exact template.

### The established pattern (proven by Particle in v2.0)

```
Formats codec          Utinni.Cli verb              MCP tool            TJT SubPanel
─────────────          ───────────────              ────────            ────────────
Formats/Particle/  →   Commands/*Command.cs   →   Tools/ReadTools  →  ParticleSubPanel
(ParseException,       (DEC-V2-VERBS-FIRST,        (zero format        (IEditorPlugin,
 Document, Writer,      golden-tested)              logic,              consumes Formats
 Mutable*, codecs)                                  DEC-V2-MCP-OOP)     via ProjectReference)
```

`Formats/Particle/` already contains: `ParticleParseException`, `ParticleEffectDocument`, `MutableParticleEffect`, `ParticleEffectWriter`, `ParticleEmitterDescription`, `ParticleFieldValue`, `ColorRampCodec`, `WaveFormCodec`. This is the exact file-set shape Terrain and Effects replicate.

### Terrain (`.trn`)

| Layer | New component | Pattern source |
|-------|---------------|----------------|
| Codec | `UtinniCoreDotNet/Formats/Terrain/` — `TerrainDocument`, `MutableTerrainDocument`, `TerrainWriter`, `TerrainParseException`, per-layer/affector codecs | `Formats/Particle/` + `Formats/Iff/` (`.trn` is an IFF container — reuse `IffReader`/`IffWriter`/`MutableIffDocument`) |
| Verb | `Utinni.Cli/Commands/DecodeTrnCommand.cs`, `RoundtripTrnCommand.cs` (+ `ApplySaveTrnCommand.cs` if write lands) | existing `DecodeIffCommand`/`RoundtripOtCommand` |
| MCP | `Utinni.Mcp/Tools/` read wrapper | existing `ReadTools.cs` |
| Editor | TJT `TerrainSubPanel` (`IEditorPlugin`) in UtinniPlugins repo | `ParticleSubPanel` + `WorldSnapshotSubPanel` |
| (optional) live read | extend `swg/scene/terrain.cpp` RVA shim | existing terrain time-of-day/weather/reload accessors |

`.trn` is the heaviest Wave-2 codec (layered terrain — shaders, affectors, boundaries, height fractals) but it is structurally IFF, so the `Formats/Iff` primitives carry it. No new parser framework.

### Effects (ClientEffect / Lightning / Swoosh)

All three are IFF-based effect formats — same `Formats/<Effect>/` + verb + SubPanel pattern, smaller than `.trn`. The exact target is confirmed at requirements scoping; whichever is chosen, it reuses Particle's shape one-for-one.

### Watch items (inherited gotchas, not blockers)

- **CommandLineParser cap** (`project_phase14_mcp_server` / `project_phase13_cli_verbs`): the original note was a 16-verb cap; the tree now has **23 `*Command.cs` files**, so the cap was lifted or worked around already — but new `trn-*`/`effect-*` verbs push further. Confirm the dispatcher in `Utinni.Cli/Program.cs` registers cleanly past the prior ceiling before adding verbs.
- **WinForms Dock.Fill Z-order** (`feedback_winforms_dockfill_zorder`): the terrain editor's multi-section pane (layer tree + property grid + preview) must keep Dock.Fill front-most / use nested SplitContainers — same trap the v1 editors hit.
- **Particle live-preview honestly degraded** (PROD-W2-PRT note): the terrain SubPanel should scope live-preview the same way — codec + save first, live in-client preview as a separately-gated deliverable, not assumed.

---

## Data Flow (v2.1 additions)

### D3D11 overlay frame (new path, mirrors the D3D9 one)

```
SWG D3D11 render thread
   ↓ IDXGISwapChain::Present  (detoured by Dx11Backend::installHooks)
hkDxgiPresent
   ↓ imgui_impl::render()        ← UNCHANGED shared entry point
      ↓ render_backend::get()->newFrame()   → ImGui_ImplDX11_NewFrame()
      ↓ [RT-space input mapping — shared, API-neutral]
      ↓ renderCallbacks dispatch + gizmo     ← shared
      ↓ render_backend::get()->renderDrawData() → ImGui_ImplDX11_RenderDrawData()
   ↓ original Present
```

### Terrain edit (offline, no new flow shape)

```
.trn file → IffReader → TerrainDocument → TerrainSubPanel (edit) → MutableTerrainDocument
   → TerrainWriter → IffWriter → loose-override .trn   (identical to the v2.0 IFF/OT save matrix)
   parallel: utinni-cli decode-trn / roundtrip-trn → JSON envelope → Utinni.Mcp read tool
```

---

## Suggested Phase Build Order (foundation-before-features)

The milestone goal is explicit: enabling debt lands first so the live-preview editors build on a stable base. Dependencies drive the order.

| # | Phase | Why here / depends on | Risk |
|---|-------|-----------------------|------|
| **1** | **CppSharp / v145 bump completion** | FOUNDATION. Pure toolchain; unblocks a clean native build surface and removes the 14.29 parser-pin debt before any new native (`directx11.cpp`) headers are added. Independent of everything else. Gate: structural-binding-diff harness GREEN + new plugin-load ABI fixture + TJT rebuilt in lockstep. | MED — codegen drift + plugin binary-compat (mitigated by harnesses) |
| **2** | **Render-backend seam (carve) + Dx9Backend** | FOUNDATION. Refactor `imgui_impl.cpp`/`directx9.cpp` behind `IRenderBackend` with the EXISTING D3D9 overlay behaviorally unchanged. No D3D11 yet. Verifiable by the existing live-smoke (overlay still renders/inputs). Must precede Dx11 so the interface is settled. Depends on (1) only for a clean build. | LOW-MED — touches the hot render/input path; gate with live-smoke (never destabilize injection for a refactor) |
| **3** | **Dx11Backend + runtime detection** | FOUNDATION completion. New `directx11.cpp` + `detectAndInstall()`. Stand up + test against OS `d3d11.dll` via the dummy-device harness even without a routine D3D11 SWG build. Confirm hard-cutover vs runtime-switch with swg-client-v2 first. Depends on (2). | MED — depends on an externally-churning swg-client-v2 `Direct3d11.dll` contract |
| **4** | **Terrain `.trn` codec + verbs + MCP tool** | FEATURE core. `Formats/Terrain/` + `decode-trn`/`roundtrip-trn` verbs + read tool, golden-tested (DEC-V2-VERBS-FIRST). Pure managed/offline — depends on (1) for clean bindings but NOT on the D3D11 work. Could parallelize with 2–3 if staffing allowed, but ordered after foundation per milestone intent. | LOW-MED — `.trn` is the heaviest codec |
| **5** | **Terrain TJT SubPanel (+ optional live preview on the new backend)** | FEATURE. `TerrainSubPanel` consumes (4)'s codec; optional live in-client preview rides the (2)/(3) backend seam — the payoff for foundation-first. Depends on (4) and benefits from (2)/(3). | MED — live preview gated separately, honestly degraded if needed |
| **6** | **One adjacent effects editor (codec + verb + SubPanel)** | FEATURE. ClientEffect/Lightning/Swoosh — smaller repeat of (4)+(5). Last because lowest-risk and the pattern is fully proven by then. | LOW |

Optional quick wins (IFF chunk templates 999.2, TRE override-history 999.3) are independent of all six and can slot anywhere staffing allows — they touch only existing IFF/TRE Formats + UI, no foundation dependency.

**Ordering rationale:** (1)→(2)→(3) is a strict foundation chain (clean build → settle the seam with the safe D3D9 carve → add the risky D3D11 twin). (4) is offline and gated only on (1), so it can start as soon as the bump lands. (5)+(6) are the user-visible payoff and depend on their codecs plus (for live preview) the backend seam — which is exactly why foundation goes first.

---

## Anti-Patterns (v2.1-specific)

### Forking `imgui_impl.cpp` for D3D11
**What people do:** copy `imgui_impl.cpp` to `imgui_impl_dx11.cpp` so each backend has "its own" overlay.
**Why it's wrong:** the WndProc subclass, RT-space mapping, gizmo, and callback bus are ~1000 lines of API-NEUTRAL, hard-won logic (Issue #11, RT-space, WantCaptureMouse arbitration). Forking doubles every future input/overlay fix.
**Do this instead:** carve only the four backend-touching calls behind `IRenderBackend`; keep all overlay/input logic single-sourced.

### Migrating `UtinniCoreDotNet` itself to net9/10 during the CppSharp bump
**What people do:** read "TFM bump" as "modernize the managed host too."
**Why it's wrong:** the injected host is pinned to net4.7.2 x86 by the hosted CLR (`clr.cpp` v4.0.30319) — a hard injection constraint. Only the build-time GENERATOR moves.
**Do this instead:** bump `UtinniCoreDotNetGen`'s TFM only; keep CppSharp's OUTPUT targeting net4.7.2.

### Shipping a binding regen without rebuilding TJT
**What people do:** regenerate `Generated/UtinniCore.cs`, commit, ship — TJT still built against the old signatures.
**Why it's wrong:** `MissingMethodException`/`CompositionException` at MEF compose; the editor's plugins silently fail to load (`feedback_caller_attrs_binary_compat`).
**Do this instead:** rebuild TJT in the same wave; add the frozen-old-DLL plugin-load ABI fixture; treat regen as a deliberate ABI bump.

### Calling `Reset`/destabilizing the D3D9 device while carving the seam
**What people do:** "improve" resize handling during the backend refactor.
**Why it's wrong:** `feedback_d3d9_reset_third_party` — Reset on SWG's device DEVICELOSTs and crashes (`SetVertexShaderConstantF failed`, VEH int3). The seam must preserve the existing no-Reset, Present-stretch behavior for the D3D9 path verbatim.
**Do this instead:** Dx9Backend's `onPreResize`/`onPostResize` wrap ONLY the existing `ImGui_ImplDX9_Invalidate/CreateDeviceObjects` calls; the no-Reset contract is unchanged. D3D11's `ResizeBuffers` semantics live only in Dx11Backend.

---

## Integration Points

### Internal boundaries (v2.1)

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `imgui_impl.cpp` ↔ `render_backend` | C++ virtual interface (`IRenderBackend`) | the new seam; keep it minimal (newframe/render/resize/dims only) |
| `render_backend::detectAndInstall()` ↔ OS/SWG renderer DLL | `GetModuleHandleA("Direct3d11.dll"/"d3d11.dll"/"d3d9.dll")` | install-time detection; hard-cutover assumption |
| `Dx11Backend` ↔ DXGI swapchain | dummy-device vtbl harvest + `Detour::Create` | mirrors `directx9.cpp::getVtbl()` exactly |
| `Formats/Terrain` ↔ `Formats/Iff` | direct call (`IffReader`/`IffWriter`) | `.trn` is IFF-structured; no new parser |
| `Utinni.Cli` verbs ↔ Formats | direct call (codec lives in `UtinniCoreDotNet`) | DEC-V2-VERBS-FIRST |
| `Utinni.Mcp` tools ↔ `utinni-cli` | shell-out (out-of-proc, zero format logic) | DEC-V2-MCP-OOP |
| TJT SubPanels ↔ Formats | ProjectReference (shared format code next to consumers) | DEC-C4 — editors ship inside TJT |

### External dependency (the one real unknown)

| Dependency | Integration | Risk / gotcha |
|------------|-------------|---------------|
| swg-client-v2 `Direct3d11.dll` renderer | Utinni detects + hooks its DXGI swapchain at runtime | ACTIVELY CHURNING (CONSULT-19..23 D3D11 work ongoing in swg-client-v2). Confirm: (1) final renderer DLL name, (2) hard-cutover vs runtime-switch, (3) whether it stays x86 (Utinni is x86-only) or the `x64bit-Upgrade` branch lands first — an x64 SWG client would break the ENTIRE injection stack, not just the overlay. DEC-V2-LIFT-SHIFT keeps Utinni decoupled — detect at runtime, don't compile against their tree. |

---

## Sources

- `D:/Code/Utinni/UtinniCore/swg/graphics/directx9.cpp` — existing D3D9 hook, `getVtbl()` dummy-device harvest, `hkPresent`/`hkReset`, the 7 vtable detours + `s207_r.dll` override (HIGH — read in full)
- `D:/Code/Utinni/UtinniCore/swg/ui/imgui_impl.cpp` — shared overlay logic: WndProc subclass, RT-space input mapping, gizmo, `renderCallbacks` bus (HIGH — read in full)
- `D:/Code/Utinni/UtinniCore/swg/scene/terrain.cpp` — existing terrain RVA shim (HIGH)
- `D:/Code/Utinni/UtinniCoreDotNet/Formats/Particle/*` — the proven Wave-2 codec template (HIGH — globbed full file-set)
- `D:/Code/Utinni/Utinni.Cli/Commands/*Command.cs` (23 verbs) + `Utinni.Mcp/Tools/*` — verb + MCP-tool pattern (HIGH)
- `D:/Code/swg-client-v2/src/engine/client/application/Direct3d11/` (`Direct3d11.vcxproj`, `Direct3d11.cpp`, `Direct3d11_Device.cpp`) — confirms the parallel renderer DLL exists and is named `Direct3d11.dll` (HIGH for existence; MEDIUM for final contract — actively churning per `.planning/research/CONSULT-19..23`)
- `.planning/PROJECT.md` — milestone scope, DEC-C4 / DEC-V2-* locks, CON-N/M/T preservation families (HIGH)
- Memory: `project_d3d11_migration`, `project_vs2026_cppsharp_block`, `feedback_imgui_embedded_d3d9_rt_space`, `feedback_d3d9_reset_third_party`, `feedback_caller_attrs_binary_compat`, `project_utinnicore_cs_regen_churn` (MEDIUM — 15-24 days old, point-in-time; cross-checked against current source where load-bearing)
- `docs/ai/toolchain-inventory.md` — revive/replace strategy, Wave-2 editor census, D3D11-migration note (HIGH)

---
*Architecture research for: SWG modding framework — v2.1 Wave-2 Editors + Foundation Hardening*
*Researched: 2026-06-14*
