# Phase 18: Render-Backend Seam + Dx9Backend - Pattern Map

**Mapped:** 2026-06-15
**Files analyzed:** 8 (4 new, 4 modified)
**Analogs found:** 8 / 8

> This is a **pure carve/refactor**, not greenfield. The dominant pattern is *wrap, don't rewrite*:
> the new seam forwards to existing live-verified `directX::` free functions verbatim. Every line
> number below was re-verified against source by the researcher and re-confirmed in this mapping pass.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `UtinniCore/swg/graphics/render_backend.h` (NEW) | interface (pure-virtual ABC) | request-response (per-frame dispatch) | `UtinniCore/swg/graphics/directx9.h` (namespace façade) + imgui_impl namespace decls | role-match (no existing ABC; closest is the `directX::` exported façade shape) |
| `UtinniCore/swg/graphics/render_backend.cpp` (NEW) | provider (concrete Dx9Backend + static singleton) | request-response | `UtinniCore/swg/graphics/directx9.cpp` getter/state free fns (lines 637-665) | exact (wraps these very functions) |
| `UtinniCore/swg/ui/imgui_impl.cpp` (MODIFIED) | view/controller (overlay logic) | event-driven (WndProc) + per-frame render | itself (self-source after carve) | n/a — surgical edit of 6 touch-points |
| `UtinniCore/swg/ui/imgui_impl.h` (MODIFIED) | header | — | itself | n/a — purge `<d3d9.h>` + `IDirect3DDevice9` param |
| `UtinniCore.Tests/Graphics/RenderBackendSeamTests.cpp` (NEW) | test (Catch2 mock-dispatch, D-07) | unit | `UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp` (Catch2 structure) | role-match (test framework + file layout) |
| `UtinniCore.Tests/Graphics/ImguiApiNeutralityTests.cpp` (NEW) | test (Catch2 source grep-gate, D-06) | unit (file-read) | `UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp` | **exact** (clone its helpers verbatim) |
| `UtinniCore.Tests/Graphics/SourceGateUtil.h` (NEW, optional) | test utility (shared helpers) | — | `NoDeviceResetTests.cpp` anon namespace (lines 63-170) | exact (lift the helpers) |
| `UtinniCore.vcxproj` + `UtinniCore.Tests.vcxproj` (MODIFIED) | config (build wiring) | — | existing `<ClCompile>`/`<ClInclude>` entries | exact |

## Pattern Assignments

### `render_backend.h` (NEW — interface / pure-virtual ABC)

**Analog:** `UtinniCore/swg/graphics/directx9.h` — the closest existing "interface façade" shape (a
namespace of free-function decls fronting the DX9 internals). There is no pre-existing pure-virtual ABC
in this codebase, so the *namespace + free get()/set()* convention from `directx9.h` is the analog for
the seam's accessor pair; the vtable shape itself comes from RESEARCH Pattern 1.

**License header + include-guard pattern** — every UtinniCore source file opens with the MIT block then
`#pragma once`. Copy verbatim from `directx9.h:1-25` (the 23-line MIT header) then:

```cpp
#pragma once
#include <imgui.h>   // ImTextureID + ImDrawData ONLY — NO <d3d9.h> (this header is what imgui_impl.cpp includes instead)
```

Note the contrast with the analog: `directx9.h:26-28` includes `"utinni.h"`, `"depth_texture.h"`, and
`<d3d9.h>`. **The seam header must NOT include `<d3d9.h>`** (D-05) — that is the whole point. Pull only
`<imgui.h>` for the `ImTextureID`/`ImDrawData` opaque types.

**Namespace + free accessor-pair pattern** (mirror `directx9.h:30-42` namespace-of-free-functions, but
front a vtable). The `UTINNI_API` export keyword from `directx9.h:38-41` is **deliberately NOT used** —
the seam is internal (RESEARCH §Runtime State: `render_backend::set/get` link via project reference, no
DLL export, no CppSharp binding surface). Member set per D-02 + the A2 amendment (10 pure virtuals — 6 ROADMAP-named + A2 color + A2 stage get/set pair):

```cpp
namespace render_backend
{
class IRenderBackend
{
public:
    virtual ~IRenderBackend() = default;
    virtual void newFrame() = 0;                            // -> imgui_impl.cpp:419
    virtual void renderDrawData(ImDrawData* drawData) = 0;  // -> imgui_impl.cpp:562
    virtual void onPreResize() = 0;                         // D-03 no-op in Dx9
    virtual void onPostResize() = 0;                        // D-03 no-op in Dx9
    virtual int renderTargetWidth() = 0;                    // D-04
    virtual int renderTargetHeight() = 0;                   // D-04
    virtual ImTextureID sceneDepthTexture() = 0;            // D-05 (imgui_impl.cpp:364 cast)
    virtual ImTextureID sceneColorTexture() = 0;            // A2 amendment (imgui_impl.cpp:390 cast)
    // Neutral depth-stage get/set for the !enableUi diagnostic slider (imgui_impl.cpp:499-503):
    virtual int  sceneDepthStage() = 0;
    virtual void setSceneDepthStage(int stage) = 0;
};

IRenderBackend* get();
void set(IRenderBackend* backend);   // setup() installs the Dx9 singleton; D-07 test installs a mock
} // namespace render_backend
```

> **Planner note (A2 RESOLVED to 10 pure virtuals):** the orchestrator prompt locks the A2 amendment — keep
> `sceneColorTexture()` + the neutral stage get/set so `DrawColorWindow` (imgui_impl.cpp:374-397) and
> the `!enableUi` stage slider (imgui_impl.cpp:499-503) survive the purge API-neutrally. Do NOT drop
> the diagnostic windows.

**C++17 constraint:** header must compile under `stdcpp17` (RESEARCH: UtinniCore.Tests vcxproj
74/89/108) — no C++20/23 features (also satisfies the CPPS-03 C++23-header CI tripwire).

---

### `render_backend.cpp` (NEW — provider / concrete Dx9Backend + static singleton)

**Analog:** `UtinniCore/swg/graphics/directx9.cpp` lines 637-665 — the exact getter/state free functions
the backend wraps. **Wrap, don't rewrite** (RESEARCH Anti-Patterns + Don't-Hand-Roll): the backend
forwards to these verbatim; the detour/device/vtable-harvest internals stay in `directx9.cpp` untouched.

**The free functions to forward to** (`directx9.cpp:637-665`, read this pass — these are the body of
every Dx9Backend member):

```cpp
DepthTexture* getDepthTexture() { return depthTexture; }          // :637  -> sceneDepth/ColorTexture
void          toggleWireframe()  { enableWireframe = !enableWireframe; } // :642 (not seam-routed)
void          blockPresent(bool value) { ... }                    // :647 (not seam-routed)
bool          isPresentBlocked() { return !isPresenting; }        // :657 (not seam-routed)
IDirect3DDevice9* getDevice() { return pDirectXDevice; }          // :662  -> backend self-sources device
```

**Concrete Dx9Backend (the only file that may `#include <imgui_impl_dx9.h>` / `"directx9.h"`):**

```cpp
#include "render_backend.h"
#include <imgui_impl_dx9.h>
#include "directx9.h"          // directX:: free fns above
#include "graphics.h"

namespace render_backend
{
class Dx9Backend final : public IRenderBackend
{
public:
    void newFrame() override { ImGui_ImplDX9_NewFrame(); }                          // was imgui_impl.cpp:419
    void renderDrawData(ImDrawData* d) override { ImGui_ImplDX9_RenderDrawData(d); }// was imgui_impl.cpp:562
    void onPreResize() override {}   // D-03 — D3D9 NEVER Resets; honest no-op
    void onPostResize() override {}  // D-03
    int renderTargetWidth()  override { return utinni::Graphics::getCurrentRenderTargetWidth(); }  // D-04
    int renderTargetHeight() override { return utinni::Graphics::getCurrentRenderTargetHeight(); } // D-04
    ImTextureID sceneDepthTexture() override
    {   // folds the null guard from imgui_impl.cpp:350 into the accessor (return 0 when no tex live)
        auto* t = directX::getDepthTexture();
        if (t == nullptr || t->getTextureColor() == nullptr) return (ImTextureID)0;
        return (ImTextureID)t->getTextureDepth();   // matches imgui_impl.cpp:364 cast
    }
    ImTextureID sceneColorTexture() override
    {
        auto* t = directX::getDepthTexture();
        if (t == nullptr || t->getTextureColor() == nullptr) return (ImTextureID)0;
        return (ImTextureID)t->getTextureColor();   // matches imgui_impl.cpp:390 cast
    }
    int  sceneDepthStage() override { auto* t = directX::getDepthTexture(); return t ? t->getStage() : 0; }
    void setSceneDepthStage(int s) override { if (auto* t = directX::getDepthTexture()) t->setStage(s); }
};

static Dx9Backend s_dx9Backend;          // static storage — NO heap (Pitfall 1 heap-free hot path)
static IRenderBackend* s_active = nullptr;
IRenderBackend* get() { return s_active; }
void set(IRenderBackend* b) { s_active = b; }
Dx9Backend* dx9Singleton() { return &s_dx9Backend; }   // setup() uses this for selection
} // namespace render_backend
```

**Heap-free hot-path rule** (`[[project_rh_snapshot_no_heap_alloc]]`, RESEARCH Pitfall 1): the singleton
is static-storage, never `new`'d / `shared_ptr`'d per frame. A plain virtual call is one pointer
indirection — it fully satisfies the rule. No `std::function`, `make_unique`, or container in the seam.

**Detour-ordering preservation** (RESEARCH Pitfall 2): do NOT move `initPresentBlockedEvent()` /
`initDepthTexture()` into the Dx9Backend constructor. They stay as `directX::` free fns called from
`utinni.cpp:371-372` BEFORE `createDetours()` (utinni.cpp:375). The singleton constructor stays trivial.

---

### `imgui_impl.cpp` (MODIFIED — the six-touch-point carve)

**No analog — surgical edit.** Replace exactly six lines (all VERIFIED in source this pass):

| Line | Today | Post-carve |
|------|-------|-----------|
| `:274` | `ImGui_ImplDX9_Init(pDevice);` | move into Dx9Backend init; `setup()` calls `render_backend::set(dx9Singleton())` + `backend->init(...)` |
| `:419` | `ImGui_ImplDX9_NewFrame();` | `render_backend::get()->newFrame();` |
| `:562` | `ImGui_ImplDX9_RenderDrawData(ImGui::GetDrawData());` | `render_backend::get()->renderDrawData(ImGui::GetDrawData());` |
| `:349` | `auto depthTex = directX::getDepthTexture();` (DrawDepthWindow) | use `render_backend::get()->sceneDepthTexture()` |
| `:376` | `auto colorTex = directX::getDepthTexture();` (DrawColorWindow) | use `render_backend::get()->sceneColorTexture()` |
| `:489` | `auto depthTex = directX::getDepthTexture();` (Tests window) | seam `sceneDepthStage()`/`setSceneDepthStage()` |

**setup() device-acquisition pattern** (RESEARCH Pattern 3 / A3) — current `setup(IDirect3DDevice9*)`
at imgui_impl.cpp:257-274 does three DX9 things that must move behind the seam:

```cpp
// imgui_impl.cpp:257-274 (TODAY — what is carved):
void setup(IDirect3DDevice9* pDevice)
{
    if (isSetup) return;
    D3DDEVICE_CREATION_PARAMETERS cParam;
    pDevice->GetCreationParameters(&cParam);                 // :264 DX9 — moves to backend
    utinni::Client::setSwgHwnd(cParam.hFocusWindow);         // :269 MUST SURVIVE (Issue #10 PanelGame reparent)
    IMGUI_CHECKVERSION();
    ImGui::CreateContext();
    ImGui_ImplWin32_Init(cParam.hFocusWindow);               // :273 Win32 — STAYS (does NOT trip D-06 gate)
    ImGui_ImplDX9_Init(pDevice);                             // :274 DX9 — moves to backend
    ...
    originalWndProcHandler = (WNDPROC)SetWindowLongPtr(cParam.hFocusWindow, GWL_WNDPROC, (LONG)hkWndProcHandler); // :281 STAYS
}
```

Post-carve recommended shape (A3 — backend self-sources device via `directX::getDevice()`): `setup()`
takes `HWND` (not a DX9 type — `HWND` is Win32 and does NOT trip the D-06 gate). The backend's `init()`
does `GetCreationParameters` + `ImGui_ImplDX9_Init` internally and publishes `hFocusWindow`.
**Critical:** `Client::setSwgHwnd(hFocusWindow)` MUST still happen (consumed managed-side by PanelGame).

**What STAYS in imgui_impl.cpp untouched** (RESEARCH Pitfall 4 — do NOT sweep these out):
- `hkWndProcHandler` WndProc subclass + Issue #11 chat-context routing (lines 144-254)
- RT-space input map `io.AddMousePosEvent` (lines 447-463, read this pass) — reads `Graphics::` (the
  API-neutral SWG façade), NOT DX9
- `ImGui_ImplWin32_Init` (:273) / `ImGui_ImplWin32_NewFrame` (:420) — Win32, gate is on `ImGui_ImplDX9_`
- `DirectInput::suspend/resume` arbitration (lines 537-550), `renderCallbacks` `dispatchSnapshot` (:512)
- gizmo namespace (lines 631-1005)

**hkReset stays in directx9.cpp** (RESEARCH Pitfall 3): the `ImGui_ImplDX9_InvalidateDeviceObjects` /
`_CreateDeviceObjects` calls live at `directx9.cpp:373/375` (read this pass) — NOT in imgui_impl, so the
D-05 purge does not touch them. The pass-through `reset(pDevice, ...)` at `directx9.cpp:374` is SWG's own
Reset through the captured vtable (lowercase free fn), not a Utinni `->Reset(`.

---

### `imgui_impl.h` (MODIFIED — header purge)

**Two edits** (VERIFIED this pass at imgui_impl.h:27,38):

```cpp
// imgui_impl.h:27  REMOVE:
#include <d3d9.h>
// imgui_impl.h:38  CHANGE from:
extern void setup(IDirect3DDevice9* pDevice);
//             to (A3 — HWND is Win32, not DX9):
extern void setup(HWND hwnd);
```

Add `#include "swg/graphics/render_backend.h"` where the seam types are needed. `setup()` is NOT
`UTINNI_API`-exported (imgui_impl.h:38, plain `extern`, only called from directx9.cpp) — changing its
signature is **ABI-safe** (no managed/plugin consumer; RESEARCH §Runtime State).

---

### `ImguiApiNeutralityTests.cpp` (NEW — D-06 source grep-gate) — **EXACT CLONE**

**Analog:** `UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp` — clone its three helpers VERBATIM.

**`repoRootFromThisFile()`** (NoDeviceResetTests.cpp:69-87) — derives repo root from `__FILE__` by
stripping 3 path segments (`/UtinniCore.Tests/Graphics/<file>.cpp`), normalizing `\` to `/`. **Reuse
exactly** — both new test files sit in the same `Graphics/` folder, so the 3-segment strip is identical.

**`readFile()`** (NoDeviceResetTests.cpp:89-96) — binary `ifstream` + `REQUIRE(in.good())` + `rdbuf()`.
Reuse verbatim.

**`stripComments()`** (NoDeviceResetTests.cpp:106-145) — handles `//` line + `/* */` block comments;
"errs toward stripping" so it can only make the gate stricter. Reuse verbatim. This is the load-bearing
grep-gate-hygiene helper (`[[feedback_gsd_grep_gate_hygiene]]`).

**Self-check SECTION pattern** (NoDeviceResetTests.cpp:174-188) — proves the stripper removes a
planted-in-comment token (the un-stripped sample must have a non-zero count, the stripped one zero).
Mirror this exactly for the DX9 symbols:

```cpp
SECTION("stripper hygiene self-check")   // mirror NoDeviceResetTests.cpp:174-188
{
    const std::string s = "int x; // IDirect3DDevice9 in a comment\n#include <d3d9.h> // also comment\n";
    REQUIRE(countSubstr(stripComments(s), "IDirect3DDevice9") == 0);  // comment stripped
    REQUIRE(countSubstr(s, "IDirect3DDevice9") == 1);                 // proves stripper does real work
}
```

**Gate on concrete symbol forms ONLY** (D-06 / Pitfall 5 — NEVER the bare string "D3D9"/"DX9"; comments
at imgui_impl.cpp:52-55, 422-446 mention D3D9/Reset constantly):
- `#include <d3d9.h>` (and `#include "d3d9.h"`)
- `IDirect3DDevice9`
- `ImGui_ImplDX9_` (prefix — catches `_Init`/`_NewFrame`/`_RenderDrawData`/`_Invalidate`/`_Create`)
- `directX::` (namespace reach-in — catches `getDepthTexture`/`getDevice`)

Assert each `== 0` against `imgui_impl.cpp` AND `imgui_impl.h`. Catch2 TEST_CASE tag: `[rndr01][graphics]`.

---

### `RenderBackendSeamTests.cpp` (NEW — D-07 mock-dispatch test)

**Analog:** `UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp` (Catch2 file structure: MIT header,
`#include <catch2/catch_all.hpp>`, anon-namespace helpers, `TEST_CASE(... , "[tag]")`).

Define a `MockBackend : render_backend::IRenderBackend` that counts each member call, install via
`render_backend::set(&mock)`, drive every member through `render_backend::get()->...`, assert each
counter `== 1`, then `render_backend::set(nullptr)` to restore for other tests. No live device required
(that is the point — CI cannot run D3D9). Tag `[rndr01][graphics]`. **Must include all 10 pure virtuals** per
the A2 amendment (add `sceneColorTexture` + the stage get/set to the mock + assertions).

**Test-seam note** (A4): D-07 testability depends on the free `render_backend::set()` setter existing
alongside `get()`. `setup()` calls `set(dx9Singleton())` once under its `isSetup` guard in production.

---

### `SourceGateUtil.h` (NEW, optional but recommended)

**Analog:** the anonymous-namespace helper block in NoDeviceResetTests.cpp:63-170. To reuse
`stripComments`/`readFile`/`repoRootFromThisFile` across both `NoDeviceResetTests.cpp` and the new
`ImguiApiNeutralityTests.cpp` without an ODR clash, lift them into `Graphics/SourceGateUtil.h` (RESEARCH
Wave-0 recommendation (a)). Alternative (b): keep per-file copies in distinct anonymous namespaces (file-
local, so duplication is legal). Recommend (a) — single-source the gate plumbing.

---

### Build wiring (MODIFIED vcxproj entries) — **EXACT PATTERN**

**Production** `UtinniCore/UtinniCore.vcxproj` (VERIFIED neighbor lines this pass):
- Add `<ClCompile Include="swg\graphics\render_backend.cpp" />` next to line 210
  (`swg\graphics\directx9.cpp`).
- Add `<ClInclude Include="swg\graphics\render_backend.h" />` next to line 289
  (`swg\graphics\directx9.h`).

**Test** `UtinniCore.Tests/UtinniCore.Tests.vcxproj` (VERIFIED line 135 this pass) — mirror the
`NoDeviceResetTests.cpp` registration with the comment-tagged-by-phase convention seen at lines 130-135:

```xml
<!-- Phase 15 15-05 Task 2: RESID-04 / D-13 no-device-Reset regression gate. -->
<ClCompile Include="Graphics\NoDeviceResetTests.cpp" />
<!-- Phase 18 / RNDR-01 D-06: imgui_impl DX9-API-neutrality source gate. -->
<ClCompile Include="Graphics\ImguiApiNeutralityTests.cpp" />
<!-- Phase 18 / RNDR-01 D-07: IRenderBackend mock-dispatch seam test. -->
<ClCompile Include="Graphics\RenderBackendSeamTests.cpp" />
```

`UtinniCore.Tests` already `ProjectReference`s `UtinniCore.vcxproj` with `LinkLibraryDependencies=true`
(vcxproj:138-146, read this pass), so the test exe links `render_backend.cpp`'s non-exported symbols.
**No `.vcxproj.filters` edit** — verified no such file exists. **Never commit `Generated/UtinniCore.cs`**
(`git checkout --` the CppSharp churn) — but no new binding surface is added here.

## Shared Patterns

### MIT license header + `#pragma once`
**Source:** every UtinniCore file (e.g. `directx9.h:1-25`, `imgui_impl.h:1-25`).
**Apply to:** all new `.h`/`.cpp`/test files. Copy the 23-line MIT block verbatim, then `#pragma once`.

### Comment-stripping source grep-gate (D-06)
**Source:** `UtinniCore.Tests/Graphics/NoDeviceResetTests.cpp:63-170` + self-check SECTION 174-188.
**Apply to:** `ImguiApiNeutralityTests.cpp` (+ `SourceGateUtil.h`). Strip comments first; gate on
concrete symbol forms only; include a planted-in-comment self-check proving the stripper does real work.
`[[feedback_gsd_grep_gate_hygiene]]` — never gate on bare "D3D9"/"DX9".

### Wrap-don't-rewrite (the carve discipline)
**Source:** `directx9.cpp:637-665` (the free fns) + the live-verified detour/vtable internals (528-616,
435-526) the researcher flags as fragile.
**Apply to:** `render_backend.cpp` — Dx9Backend forwards to `directX::` verbatim; the only new behavior
is one virtual indirection per frame. Do NOT re-implement the 7 vtable detours, the dummy-device harvest,
the Present-block, or the RESZ depth-resolve.

### Heap-free per-frame dispatch
**Source:** the `dispatchSnapshot` stack-snapshot pattern (imgui_impl.cpp:512) +
`[[project_rh_snapshot_no_heap_alloc]]`.
**Apply to:** `render_backend.cpp` static-storage singleton + plain virtual calls. No `std::function`,
`new`, `shared_ptr`, or container in the seam's per-frame path.

### No-Reset contract preservation (success criterion #4)
**Source:** `NoDeviceResetTests.cpp` (gates `->Reset(`/`.Reset(` count == 0) + `directx9.cpp:365-378`
(hkReset's pass-through `reset(...)` is SWG's own, lowercase free fn — not a Utinni-initiated Reset).
**Apply to:** `render_backend.cpp` — `onPreResize`/`onPostResize` are `{}` honest no-ops (D-03); never
add `->Reset(`/`.Reset(`. The existing NoDeviceResetTests.cpp continues to gate this unchanged.

### Phase-tagged vcxproj `<ClCompile>` registration
**Source:** `UtinniCore.Tests.vcxproj:130-135` (XML comment `<!-- Phase N ... -->` above each test entry).
**Apply to:** both new test entries — keep the phase/req/decision-tagged comment convention.

## No Analog Found

None. Every new/modified file has a strong codebase analog (the test files clone an exact precedent; the
seam wraps existing `directX::` free functions; the edits are surgical). This is a carve, not greenfield.

## Metadata

**Analog search scope:** `UtinniCore/swg/graphics/`, `UtinniCore/swg/ui/`, `UtinniCore.Tests/Graphics/`,
the two `.vcxproj` build files.
**Files scanned (read this pass):** `NoDeviceResetTests.cpp`, `directx9.h`, `imgui_impl.h`,
`imgui_impl.cpp` (lines 255-414, 414-573), `directx9.cpp` (lines 359-382, 636-666),
`UtinniCore.vcxproj` (205-214, 286-291), `UtinniCore.Tests.vcxproj` (130-147).
**Pattern extraction date:** 2026-06-15
