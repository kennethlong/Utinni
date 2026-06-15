/**
 * MIT License
 *
 * Copyright (c) 2020 Philip Klatt
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 **/

#pragma once

// Phase 18 / RNDR-01: the single runtime-polymorphic render seam (D-01). This
// header is what imgui_impl.cpp will include INSTEAD of the DX9 bindings, so it
// MUST stay free of <d3d9.h> / <imgui_impl_dx9.h> / IDirect3DDevice9 internals
// (D-05 full purge). Only imgui's opaque texture/draw-data types are pulled in;
// the device type is forward-declared for the non-virtual init() signature so
// no DX9 binding header leaks into the seam header.
#include <imgui.h>   // ImTextureID + ImDrawData ONLY -- NO <d3d9.h>
#include <Windows.h> // HWND for the non-virtual Dx9Backend::init() return type

// Forward declaration only -- keeps <d3d9.h> out of this header (D-05 gate).
struct IDirect3DDevice9;

namespace render_backend
{
// IRenderBackend is a runtime-polymorphic pure-virtual ABC (D-01). A single
// global instance is installed at setup(); Phase 19 adds Dx11Backend behind the
// SAME vtable without re-touching imgui_impl. The vtable is exactly 10 pure
// virtuals (D-02 + the A2 amendment): the 6 ROADMAP-named members plus the A2
// scene color accessor and the A2 scene-depth-stage get/set pair (so
// DrawColorWindow and the !enableUi diagnostic stage slider survive Plan 02's
// purge API-neutrally).
class IRenderBackend
{
public:
    virtual ~IRenderBackend() = default;

    // Per-frame hot path (plain virtual call -- zero heap; see render_backend.cpp).
    virtual void newFrame() = 0;                            // -> the backend's new-frame call
    virtual void renderDrawData(ImDrawData* drawData) = 0;  // -> the backend's draw-data submit

    // Resize hooks (D-03): honest no-ops in Dx9Backend. They exist only so
    // Phase 19's Dx11 ResizeBuffers has a home; D3D9 NEVER Resets.
    virtual void onPreResize() = 0;
    virtual void onPostResize() = 0;

    // Forward-compat seam hooks for Phase 19 (D-04 clarified): they forward to
    // utinni::Graphics::getCurrentRenderTargetWidth/Height(). UNUSED in Phase 18 --
    // imgui_impl continues to read Graphics:: directly in its RT-space block; the
    // seam exposes the dims, it does not relocate the present-stretch math.
    virtual int renderTargetWidth() = 0;
    virtual int renderTargetHeight() = 0;

    // API-neutral scene-depth/color accessors (D-05 + A2): ImTextureID is imgui's
    // opaque handle (the LCD type Dx11Backend also satisfies in Phase 19 with an
    // ID3D11ShaderResourceView*). Return (ImTextureID)0 when no texture is live.
    virtual ImTextureID sceneDepthTexture() = 0;
    virtual ImTextureID sceneColorTexture() = 0;

    // API-neutral depth-stage get/set for the !enableUi diagnostic slider (A2).
    virtual int sceneDepthStage() = 0;
    virtual void setSceneDepthStage(int stage) = 0;
};

// Concrete D3D9 backend. Wraps the existing directX:: free functions verbatim
// (wrap, don't rewrite); the detour/device/vtable-harvest internals stay in
// directx9.cpp untouched. The 10 vtable overrides forward to directX:: and to
// the imgui backend binding calls; init() is the NON-VIRTUAL device-bearing entry.
class Dx9Backend final : public IRenderBackend
{
public:
    void newFrame() override;
    void renderDrawData(ImDrawData* drawData) override;
    void onPreResize() override;
    void onPostResize() override;
    int renderTargetWidth() override;
    int renderTargetHeight() override;
    ImTextureID sceneDepthTexture() override;
    ImTextureID sceneColorTexture() override;
    int sceneDepthStage() override;
    void setSceneDepthStage(int stage) override;

    // NON-VIRTUAL, deliberately OFF the IRenderBackend vtable (Phase 19's Dx11
    // init signature differs). Plan 02's setup() calls this: it does the
    // device-bearing GetCreationParameters + the imgui DX9 backend init and
    // returns hFocusWindow. Returns nullptr if no device is available (caller bails).
    HWND init(IDirect3DDevice9* device);

    // Phase 18 / RNDR-01 (review amendment 6): hkPresent stashes its LIVE pDevice
    // here BEFORE calling the DX9-API-neutral imgui_impl::setup(HWND). setup()
    // cannot name a Direct3D device type (D-05 gate), so it calls init(nullptr)
    // and init() consumes this stashed device as its PRIMARY source --
    // directX::getDevice() is the assert/fallback only. Win32 HWND in / device out
    // never cross the imgui_impl seam as a DX9 type.
    void stashDevice(IDirect3DDevice9* device);
    IDirect3DDevice9* stashedDevice() const;

private:
    IDirect3DDevice9* m_stashedDevice = nullptr;
};

// Accessor trio (mirrors the directx9.h namespace-of-free-functions convention,
// but deliberately carries NO DLL-export macro -- the seam is internal: it links
// via project reference, has no DLL export, and adds no CppSharp binding surface).
//
// get() is NULLABLE: it returns nullptr until setup() installs the backend, so
// Plan 02 must guard every seam call site.
IRenderBackend* get();
void set(IRenderBackend* backend);   // setup() installs the Dx9 singleton; the D-07 test installs a mock
Dx9Backend* dx9Singleton();          // address of the static-storage Dx9Backend (never heap-allocated)
} // namespace render_backend
