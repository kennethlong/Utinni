# Stack Research

**Domain:** v2.1 "Wave-2 Editors + Foundation Hardening" — additions to an existing 32-bit native-injection + .NET Framework WinForms + MEF plugin app (Utinni). Scope: Terrain (`.trn`) editor, one adjacent Effects editor, D3D11 render-path foundation, real CppSharp v145 upgrade.
**Researched:** 2026-06-14
**Confidence:** HIGH for D3D11 backend + CppSharp state (Context7 + repo + upstream verified); HIGH for terrain format (reference source on disk); MEDIUM for terrain-visualization recommendation (a design call, not a single canonical library).

> **Framing.** This is a *subsequent-milestone* stack delta. Almost everything Utinni needs already ships (DetourXS, ImGui 1.92.6 via vcpkg, CppSharp 0.10.5, WinForms host, the dummy-device D3D9 vtable-harvest pattern). The v2.1 "additions" are mostly **one new vcpkg feature flag, one new pair of C++ source files, and a build-config decision** — not new third-party dependencies. The most important findings are *negative*: no released CppSharp reaches v145, and `.trn` needs no heightmap library. Both are spelled out below.

---

## Recommended Stack

### Core Technologies (the actual v2.1 deltas)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **ImGui DX11 backend** (`imgui_impl_dx11.cpp/.h`) | ships **inside imgui 1.92.6** (already vendored via vcpkg) | The renderer half of the parallel D3D11 overlay path | It is the exact peer of the `imgui_impl_dx9` Utinni already uses. Same `NewFrame`/`RenderDrawData` contract; only the init signature differs (`ID3D11Device*` + `ID3D11DeviceContext*` instead of `IDirect3DDevice9*`). **No new dependency** — just add the `dx11-binding` feature to the existing vcpkg `imgui` entry. (Context7 `/ocornut/imgui`) |
| **DetourXS** (vendored `external/DetourXS`) | current vendored copy | Detour `IDXGISwapChain::Present` + `ResizeBuffers` (the D3D11 equivalents of D3D9 `Present`/`Reset`) | **Already in the repo and already the hooking mechanism.** Its `Create(LPVOID lpFuncOrig, ...)` by-address overload is exactly what a DXGI vtable-harvest needs (verified in `detourxs.h`). The same `DETOUR_TYPE_PUSH_RET` + `Detour::CheckPointer` pattern the 7 D3D9 hooks use transfers 1:1. **Do NOT introduce MinHook** — see "What NOT to Use." |
| **DXGI / D3D11 SDK headers** (`<d3d11.h>`, `<dxgi.h>`/`<dxgi1_2.h>`) | Windows SDK 10.0.19041+ (already installed; used by the CppSharp redirect) | Compile the new `directx11.cpp` hook + dummy-device vtable harvest | Ships with the Windows 10 SDK already on the box. Link `d3d11.lib`/`dxgi.lib`, or `GetProcAddress("D3D11CreateDeviceAndSwapChain")` dynamically to mirror the existing `Direct3DCreate9` dynamic-load (avoids adding libs to the x86 link line). |
| **CppSharp** (vendored `external/CppSharp`) | **stays 0.10.5 (clang 11)** for v2.1 | Binding generator (`UtinniCoreDotNetGen`) | **There is no CppSharp release that parses v145 (MSVC 14.5x) STL.** Newest is v1.2 / NuGet `1.1.84.17100` (2025-11-19, **clang 19**), which reaches only v143/14.4x. v145's STL hard-requires **clang 20**, which no CppSharp ships. So the "real upgrade retiring the parser-include redirect" the milestone names **cannot be delivered by a stock CppSharp bump in v2.1.** See question (b) below for the honest options. (Verified: nuget.org/packages/CppSharp + github.com/mono/CppSharp/releases, 2026-06-14.) |

### Supporting Libraries (terrain + effects editors)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| **swg-client-v2 `sharedTerrain`** (read-only reference at `D:/Code/swg-client-v2`, SHA `d6496005e`) | pinned SHA | The `.trn` (PTAT/TGEN) format spec to port: `TerrainGenerator`, `Affector*`, `Boundary`, `Filter`, `*Group` (Shader/Flora/Radial/Fractal/Environment), `SamplerProceduralTerrainAppearance` (the CPU heightmap sampler) | Port the **parse/serialize + CPU-sample** logic into managed `UtinniCoreDotNet/Formats` for the Terrain editor. Read-only reference, **no runtime dependency** (DEC-V2-LIFT-SHIFT: do not `#include`/ProjectReference the live tree). |
| **`System.Drawing` `Bitmap` + `LockBits`** | net4.7.2 framework-built-in | Render a generated heightmap / shader-mask / flora-density 2D preview inside the WinForms SubPanel | The terrain heightmap is **not stored** in `.trn` — it is *generated* by running the affector graph (`SamplerProceduralTerrainAppearance`). Sample to a `float[,]`, map to a `Bitmap` via `LockBits`, blit into a `PictureBox`/owner-drawn panel. **Zero new dependency.** This is the right "3D-ish 2D" visualization for a procedural editor — height as grayscale/colormap, slope/flora as overlays. |
| **ImGuizmo** (vendored, vcpkg `imguizmo >=1.10`) | already present | In-client gizmo for terrain *boundary*/layer manipulation and effects-node placement, if the live-preview path is built | Already wired in `imgui_impl.cpp` for the WorldSnapshot gizmo. Reusable for terrain boundary polylines / effect emitter transforms with no new dep. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| **VS 2026 MSBuild (Dev18, v145)** | Build the new `directx11.cpp` + terrain/effects code | Already the local default. Build C++ via MSBuild, run xUnit via `dotnet test --no-build` (the WinForms `.resx` MSB3823 constraint is unchanged). |
| **VS 2019 BuildTools 14.29 STL (the parser-include redirect)** | Keep `UtinniCoreDotNetGen` codegen working at v145 build-time | **Stays in force for v2.1** unless the milestone accepts a CppSharp net9 migration that still won't reach v145 (see below). The redirect (`ConfigureCppSharpParserStl()`) is the only path that produces correct bindings against a v145 build today. |
| **vcpkg (manifest mode)** | Pulls imgui+dx11 backend | One-line manifest edit (add `dx11-binding` feature); no new top-level dependency. |

---

## Question-by-Question Findings

### (a) D3D11 overlay: which ImGui backend + Present-hook approach pairs with the existing DetourXS D3D9 hook?

**Recommendation: a parallel `swg/graphics/directx11.cpp` that mirrors `directx9.cpp` exactly — DetourXS by-address detours on a DXGI vtable harvested from a dummy device — plus `imgui_impl_dx11`.** No new hooking library.

The existing D3D9 path (`directx9.cpp`) already does the hard part in a way that transfers directly:
1. **Dummy-device vtable harvest.** `getVtbl()` creates a throwaway `IDirect3DDevice9` via the public API, `memcpy`s its 119-entry vtable, releases the device, and detours the function bodies in `d3d9.dll`'s `.text`. The D3D11 analogue is identical: call `D3D11CreateDeviceAndSwapChain` against a hidden 1×1 `HWND`, snapshot the **`IDXGISwapChain` vtable** (and optionally the `ID3D11DeviceContext` vtable), release, detour the bodies. This is the standard, well-documented overlay pattern (RenderHook, DX11-ImGui-HookKit, Niemand's writeup — all use exactly this).
2. **Detour mechanism is unchanged.** `Detour::Create((LPVOID)addr, hkPresentDXGI, DETOUR_TYPE_PUSH_RET)` works on the harvested DXGI addresses just as it does on the D3D9 ones. The `Detour::CheckPointer` null-guard and `DETOUR_LEN_AUTO` discipline (memory: DetourXS explicit-length trap) carry over verbatim.
3. **Hook targets:** `IDXGISwapChain::Present` is **vtable index 8**; `IDXGISwapChain::ResizeBuffers` is **index 13** (the DXGI equivalent of D3D9 `Reset` — handle window/back-buffer resize here, *not* a D3D9-style `Reset`; memory: d3d9-reset-third-party applies, DXGI has clean `ResizeBuffers` semantics so this is actually *easier* than the D3D9 case).
4. **ImGui backend swap.** `hkPresentDXGI` calls `imgui_impl::renderDx11()` which uses `ImGui_ImplDX11_NewFrame()` / `ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData())`. Init needs `ID3D11Device*` + `ID3D11DeviceContext*` (pull `ID3D11Device` from `swapChain->GetDevice(...)`, then `GetImmediateContext`), and a render-target view built from `swapChain->GetBuffer(0)`. `imgui_impl_win32` (the input/wndproc half) is **shared unchanged** between both paths.
5. **Runtime path selection.** At `utinni_init`, detect which graphics DLL the host loaded: `GetModuleHandle("d3d11.dll")`/`dxgi.dll` present → install the D3D11 path; else the existing D3D9 path. This is the cleanest way to honor the open question in [[project-d3d11-migration]] ("concurrent vs hard cutover") without committing to either — the foundation supports both, only one installs per session.

**Integration cost:** one new `.cpp/.h` pair structurally cloned from `directx9.cpp`, a `renderDx11()` sibling in `imgui_impl.cpp`, and a `dx11-binding` vcpkg feature. The `depthTexture`/post-processing D3D9 surfaces (`depth_texture.h`, `post_processing.cpp`) are **D3D9-only and do not port in v2.1** — the foundation goal is "overlay + live-preview keep rendering," not full D3D11 post-processing parity. Scope that out explicitly.

**RT-space mouse mapping** (memory: imgui-embedded-d3d9-rt-space) is reusable: the same DisplaySize/mouse scaling logic in `imgui_impl::render()` applies because the embedded-window-stretch problem is API-agnostic.

### (b) CppSharp clang vs MSVC v145 — does any release reach v145? What TFM does the latest require?

**Honest answer: NO released CppSharp parses v145 (MSVC 14.5x) STL, and none is on the horizon.** This is the single most important finding in this document, because the milestone names "finish a REAL CppSharp upgrade so UtinniCoreDotNetGen runs natively on MSVC 14.5x STL, retiring the parser-include redirect" as a target — and **that specific deliverable is not achievable with a stock CppSharp in v2.1.**

The chain of facts (all verified 2026-06-14):

| Fact | Evidence |
|------|----------|
| Newest CppSharp = **v1.2 / NuGet `1.1.84.17100`**, published **2025-11-19**, bundling **clang 19** | nuget.org/packages/CppSharp; github.com/mono/CppSharp/releases — no release newer than v1.2 exists |
| clang 19 reaches MSVC **14.4x (v143)** STL only | v1.2 release notes "clang 19 for modern MSVC support"; VS 2022 17.13 STL requires clang 19 |
| v145 (MSVC 14.51/14.52) STL **hard-requires clang 20** | `yvals_core.h` in 14.51/14.52: `_EMIT_STL_ERROR(STL1000, "...expected Clang 20 or newer.")` (verified in the prior research, local headers) |
| **No CppSharp ships clang 20** | confirmed via releases page; the leading edge is still clang 19 |
| Latest CppSharp NuGet TFM = **net9.0 (win-x64)** / net10.0 (linux-x64); **no net4.7.2, no win-x86** | nupkg contents in prior research; nuget.org page shows net6.0+ |

**Therefore the v2.1 options, ranked:**

1. **KEEP the parser-include redirect (Path 1), and re-scope the milestone's "retire the redirect" goal.** The redirect (clang 11 ↔ VS 2019 14.29 STL while the build links v145) is *already working and shipped* (commit `2f57dfa`, `d69988d` baseline). It produces bindings byte-identical to the v142 baseline. **This is the correct v2.1 recommendation:** the redirect is not debt to be paid this milestone — it is the only mechanism that works, *because the upstream piece (clang 20 CppSharp) does not exist yet.* Recommend the roadmap reframe D-09 from "retire the redirect" to "harden + document the redirect, and add a CI tripwire for the clang-20 CppSharp release."
2. **Upgrade to CppSharp v1.2 (clang 19) + migrate `UtinniCoreDotNetGen` net4.7.2→net9.0, but pin the parser STL to VS 2022 14.4x.** This is a *partial* modernization: it gets off clang 11 and onto a maintained CppSharp, and narrows the STL-version gap from "VS 2019" to "VS 2022" — but it **still does not parse v145 natively** (clang 19 ≠ clang 20), so a redirect (now to 14.4x instead of 14.29) is still required. Net cost: the net9 migration (SDK-style csproj, drop App.config, PostBuildEvent → `dotnet UtinniCoreDotNetGen.dll` or self-contained publish, regen + diff bindings, lockstep-validate UtinniPlugins). 1–2 days, MEDIUM-HIGH risk (5-year jump in generator semantics). **Worth doing as "modernize the build pipeline," NOT as "reach v145 natively" — those are different goals and only the first is attainable.**
3. **Wait for a clang-20 CppSharp release.** Microsoft's v145/14.5x STL is the leading edge; CppSharp historically trails MSVC STL by ~1 toolset (clang 16→VS2022 in v1.1; clang 19→14.4x in v1.2). A clang-20 CppSharp that natively parses v145 is *plausible but unscheduled* — the prior research notes "Phase 6's encounter with this is plausibly the leading edge." Add a tripwire; do not block v2.1 on it.

**TFM impact, stated plainly:** any move *off* the vendored 0.10.5 forces `UtinniCoreDotNetGen` from **net4.7.2 → net9.0/net10.0** (no modern CppSharp targets .NET Framework). That migration is real work and pulls a .NET 9/10 runtime requirement onto dev/CI boxes. Since it buys *no* native v145 capability (option 2 still needs a redirect), **defer it** unless the milestone independently wants the build-pipeline modernization.

### (c) Libraries for terrain heightmap / "3D-ish 2D" visualization in WinForms

**Recommendation: none — use `System.Drawing.Bitmap` + `LockBits` (framework built-in), feeding from a ported `SamplerProceduralTerrainAppearance` CPU sampler.** Optionally, the live in-client preview (the differentiator) renders the *real* terrain via the injected engine.

The decisive format fact: **`.trn` stores a procedural *graph*, not a heightmap.** It is a `PTAT` IFF form wrapping a `TGEN` (TerrainGenerator) sub-form of **layers** containing **affectors** (height/color/shader/flora/radial-flora/road/river/ribbon), **filters** (slope/height/shader/direction/fractal), **boundaries** (region shapes), and **families/groups** (shader/flora/radial/fractal/environment). The visible heightmap is *computed* by evaluating that graph at sample points — that is what `SamplerProceduralTerrainAppearance` (CPU) and `ClientProceduralTerrainAppearance` (GPU chunks) do. So a Terrain editor's job is: (1) parse/edit the PTAT/TGEN graph (a tree editor, the bulk of the work), and (2) *render a preview* by sampling the graph.

Given that, the visualization need is "rasterize a `float[,]` (height) — plus optional `byte[,]` masks for shader/flora/slope — into a 2D image." The standard, dependency-free WinForms answer is `Bitmap` + `LockBits` for fast pixel writes, blitted into a panel. This is "3D-ish 2D" done right for a procedural editor (grayscale/colormap height, hillshade via slope, flora-density overlay). **Do not pull a 3D engine, a charting library, or a mesh viewer for this** — it would duplicate the in-client preview and violate the SIE "live preview = in-client via the real engine" decision (toolchain-inventory.md) and DEC-A3 (no 3D mesh/anim authoring).

If a *true 3D* terrain preview is later wanted, the locked answer is **live in-client** (inject, let the real SWG engine render the `.trn`), not a standalone renderer — same rationale that governs the IFF/appearance preview.

---

## Installation

```jsonc
// vcpkg.json — add the dx11-binding feature to the EXISTING imgui entry.
// (No new top-level dependency; imgui is already >=1.92.6.)
{
  "name": "imgui",
  "features": [
    "docking-experimental",
    "dx9-binding",
    "dx11-binding",   // <-- ADD: pulls imgui_impl_dx11.cpp/.h
    "win32-binding"
  ],
  "version>=": "1.92.6"
}
```

```text
// Windows SDK libs for the new directx11.cpp (or GetProcAddress them, mirroring Direct3DCreate9):
//   d3d11.lib, dxgi.lib   — already available in the installed Windows 10 SDK (10.0.19041+)
// No NuGet / vcpkg additions beyond the imgui feature flag.
```

```text
// CppSharp: NO install change recommended for v2.1.
//   Keep external/CppSharp 0.10.5 + the VS2019 14.29 parser-include redirect.
//   (Upgrade to NuGet CppSharp 1.1.84.17100 only if doing the net9 build-pipeline
//    modernization — and note it still needs a redirect, just to 14.4x.)
```

---

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| **DetourXS by-address DXGI hook** (reuse existing) | **MinHook** (used by most public DX11-ImGui kits) | Only if DetourXS proves unable to relocate a DXGI prologue cleanly on a given Windows build. Given DetourXS already drives all 7 D3D9 hooks + the s207 shader detour in production, this is unlikely. Mixing two hooking libs adds a dep and a second trampoline allocator for no benefit. |
| **Keep CppSharp 0.10.5 + parser redirect** | **CppSharp 1.2 (clang 19) + net9 generator** | When the goal shifts to "modernize the build pipeline / get off clang 11," AND the team accepts that it *still* needs a parser redirect (to 14.4x) because clang 19 ≠ clang 20. Not a v145-native win. |
| **`System.Drawing` Bitmap+LockBits heightmap** | **In-client live 3D preview via the injected engine** | When a *true 3D* terrain walkthrough is wanted. This is the locked long-term answer (live preview = real engine), but it is heavier than the 2D sampled preview and depends on injection being live; ship the 2D preview first. |
| **Port `sharedTerrain` parse/sample into managed** | **Revive+wrap the MFC `TerrainEditor.exe`** | Never for the editor itself — toolchain-inventory.md classifies interactive editors as REPLACE, not revive. (Revive+wrap is only for headless build CLIs.) The MFC app is reference, not a shippable component. |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| **MinHook (as a new dependency)** | Utinni already hooks via DetourXS; the public DX11-overlay kits use MinHook only because they start from scratch. Adding it duplicates the trampoline/relocation machinery already proven in `directx9.cpp`. | The existing `Detour::Create(addr, hk, DETOUR_TYPE_PUSH_RET)` on harvested DXGI vtable addresses. |
| **A 3D engine / mesh viewer / OpenTK / Helix Toolkit for terrain preview** | Violates "live preview = in-client via the real engine" (toolchain-inventory.md) and DEC-A3; duplicates the differentiator a standalone editor structurally can't match; heavy new dependency. | `Bitmap`+`LockBits` 2D sampled preview now; in-client live 3D later. |
| **A stock CppSharp bump as the "v145-native" deliverable** | No released CppSharp ships clang 20; v1.2 reaches 14.4x only. Framing the bump as "retires the redirect" sets an unmeetable acceptance criterion. | Keep + harden the parser-include redirect; add a clang-20-CppSharp release tripwire; treat any CppSharp bump as *pipeline modernization*, scored separately. |
| **D3D9 `IDirect3DDevice9::Reset` semantics carried into D3D11** | DXGI uses `ResizeBuffers`, not `Reset`; the third-party-Reset corruption (memory: d3d9-reset-third-party) is D3D9-specific. | `IDXGISwapChain::ResizeBuffers` (vtable idx 13) with RTV teardown/rebuild; cleaner than the D3D9 case. |
| **Dragging swg-client-v2's renderer / `clientTerrain` GPU path into Utinni** | DEC-V2-LIFT-SHIFT: never `#include`/ProjectReference the live tree; it's mid D3D9→D3D11 churn. | Port only the headless `sharedTerrain` parse + `SamplerProceduralTerrainAppearance` CPU-sample logic into managed code, pinned to SHA `d6496005e`. |

---

## Stack Patterns by Variant

**If SWG Source ships D3D11 as a hard cutover (D3D9 gone):**
- Install only the D3D11 path; the D3D9 `depthTexture`/post-processing surfaces are dead and can be left dormant.
- Because [[project-d3d11-migration]] flags this as an open question, build the foundation as runtime-selected (detect loaded graphics DLL), so a cutover needs no code change — only the D3D11 branch fires.

**If SWG Source keeps D3D9 selectable alongside D3D11:**
- Both `directx9.cpp` and `directx11.cpp` install conditionally on the detected graphics DLL; `imgui_impl_win32` and the WinForms host are shared. One overlay path is live per session.

**If the team wants build-pipeline modernization this milestone:**
- Take CppSharp 1.2 + net9 `UtinniCoreDotNetGen`, redirect parser to VS 2022 14.4x STL (clang 19's pairing). Accept it is NOT v145-native. Lockstep-revalidate UtinniPlugins binding compat. Otherwise, keep 0.10.5 + the 14.29 redirect untouched.

**If `.trn` versions diverge (SWGEmu vs Restoration/newer):**
- Mirror the TRE version-support stance (memory: tre-version-support-gap): the PTAT/TGEN loader in `sharedTerrain` carries explicit version branches (`ProceduralTerrainAppearanceTemplate.cpp` has many `version` checks) — port the version dispatch, enumerate-only on unparseable/encrypted variants.

---

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| imgui 1.92.6 + `dx11-binding` | DetourXS (vendored) | `imgui_impl_dx11` is renderer-only; hooking stays DetourXS. `imgui_impl_win32` shared with the D3D9 path. |
| imgui 1.92.6 `dx11-binding` | Windows SDK 10.0.19041+ d3d11/dxgi | DX11 backend needs `<d3d11.h>`/`<dxgi.h>` already present. |
| CppSharp 0.10.5 (clang 11) | MSVC v145 build + VS 2019 14.29 STL parser redirect | The ONLY combination that yields correct bindings at v145 build today. Shipped + baseline-pinned (`d69988d`). |
| CppSharp 1.2 (clang 19) | MSVC ≤14.4x (v143) STL parse | Does **not** parse v145/14.5x. Requires net9/net10 generator (no net4.7.2). |
| `System.Drawing` | net4.7.2 WinForms host | Built-in; `LockBits` for fast heightmap raster. No NuGet. |
| swg-client-v2 `sharedTerrain` @ `d6496005e` | read-only reference | Pin the SHA (DEC-V2-LIFT-SHIFT); master is `d6496005e` today and actively churning. |

---

## Sources

- `/ocornut/imgui` (Context7) — DX11 backend Init/NewFrame/RenderDrawData contract; `ID3D11Device*`+`ID3D11DeviceContext*` init signature — HIGH
- [nuget.org/packages/CppSharp](https://www.nuget.org/packages/CppSharp/) — latest `1.1.84.17100` (2025-11-19), net9.0/net6.0, no net472 — HIGH
- [github.com/mono/CppSharp/releases](https://github.com/mono/CppSharp/releases) — no release newer than v1.2; v1.2 = clang 19; **no clang 20 / v145 release** — HIGH
- [TRN (FileFormat) — SWGANH Wiki](http://wiki.swganh.org/index.php/TRN_(FileFormat)) + [PCG Wiki: SWG](http://pcg.wikidot.com/pcg-games:star-wars-galaxies) — `.trn` = procedural affector/filter/boundary graph, not stored heightmap — MEDIUM (cross-checked against on-disk source)
- `D:/Code/swg-client-v2` `sharedTerrain` (SHA `d6496005e`) — PTAT/TGEN IFF form, `Affector*`/`Boundary`/`Filter`/`*Group`, `SamplerProceduralTerrainAppearance` CPU sampler; `TerrainEditor` MFC app = reference only — HIGH (source on disk)
- `D:/Code/Utinni/UtinniCore/swg/graphics/directx9.cpp` + `swg/ui/imgui_impl.cpp` — existing dummy-device vtable-harvest + DetourXS `DETOUR_TYPE_PUSH_RET` pattern the D3D11 path clones — HIGH (repo)
- `D:/Code/Utinni/external/DetourXS/detourxs.h` — by-address `Create()` overload confirms DXGI-address detour support — HIGH (repo)
- `.planning/research/cppsharp-msvc-14.5-upgrade.md` + memory [[project-vs2026-cppsharp-block]] — prior v2.0 CppSharp analysis; Path 1 redirect shipped (`2f57dfa`/`d69988d`) — HIGH
- [RenderHook](https://github.com/Jakhb/RenderHook), [DX11-ImGui-HookKit](https://github.com/Piotrixek/DX11-ImGui-HookKit), [Niemand: Hook DX11+ImGui](https://niemand.com.ar/2019/01/01/how-to-hook-directx-11-imgui/) — DXGI Present=vtbl 8, ResizeBuffers=vtbl 13, dummy-device harvest pattern — MEDIUM (community, multi-source agreement)

---
*Stack research for: Utinni v2.1 "Wave-2 Editors + Foundation Hardening" — Terrain editor, Effects editor, D3D11 render-path foundation, CppSharp v145 upgrade*
*Researched: 2026-06-14*
