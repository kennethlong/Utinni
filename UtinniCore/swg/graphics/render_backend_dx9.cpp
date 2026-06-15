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

// Phase 18 / RNDR-01: render_backend_dx9.cpp is the DX9-BEARING half of the seam
// (Option A split, 18-01 Task 2 decision). It is the ONLY file that may include
// the DX9 bindings. The Dx9Backend wraps the existing live-verified directX::
// free functions verbatim (wrap, don't rewrite) -- it re-implements none of the
// detour/device/vtable-harvest internals. It also owns the static-storage
// Dx9Backend singleton and dx9Singleton(). Splitting these device-bound symbols
// out of render_backend.cpp lets the D-07 test compile the DX9-free TU directly
// and link the mock-dispatch contract without a live device (no LNK2019 cascade
// through directX::/DetourXS/DepthTexture/CuiManager).
#include "render_backend.h"
#include <d3d9.h>
#include <imgui_impl_dx9.h>
#include "directx9.h"   // directX:: free functions (device/depth/etc.)
#include "graphics.h"   // utinni::Graphics::getCurrentRenderTargetWidth/Height
#include <cassert>

namespace render_backend
{
// --- Per-frame hot path: plain forwards to the imgui DX9 backend bindings. ---
void Dx9Backend::newFrame()
{
    ImGui_ImplDX9_NewFrame();
}

void Dx9Backend::renderDrawData(ImDrawData* drawData)
{
    ImGui_ImplDX9_RenderDrawData(drawData);
}

// --- Resize hooks (D-03): honest no-ops. D3D9 never re-initializes the device; ---
// --- these exist only so Phase 19's Dx11 ResizeBuffers has a home. The bodies   ---
// --- stay empty -- no device re-init call is ever introduced here.              ---
void Dx9Backend::onPreResize() {}
void Dx9Backend::onPostResize() {}

// --- Render-target dims (D-04): forward to the API-neutral SWG Graphics facade. ---
int Dx9Backend::renderTargetWidth()
{
    return utinni::Graphics::getCurrentRenderTargetWidth();
}

int Dx9Backend::renderTargetHeight()
{
    return utinni::Graphics::getCurrentRenderTargetHeight();
}

// --- Scene depth/color accessors (D-05 + A2): fold the imgui_impl null guards ---
// --- into the accessor so the consumer never sees directX::DepthTexture.       ---
ImTextureID Dx9Backend::sceneDepthTexture()
{
    auto* t = directX::getDepthTexture();
    if (t == nullptr || t->getTextureColor() == nullptr)
    {
        return (ImTextureID)0;
    }
    return (ImTextureID)t->getTextureDepth();
}

ImTextureID Dx9Backend::sceneColorTexture()
{
    auto* t = directX::getDepthTexture();
    if (t == nullptr || t->getTextureColor() == nullptr)
    {
        return (ImTextureID)0;
    }
    return (ImTextureID)t->getTextureColor();
}

int Dx9Backend::sceneDepthStage()
{
    auto* t = directX::getDepthTexture();
    return t ? t->getStage() : 0;
}

void Dx9Backend::setSceneDepthStage(int stage)
{
    if (auto* t = directX::getDepthTexture())
    {
        t->setStage(stage);
    }
}

// --- NON-VIRTUAL device-bearing init (off the vtable). pDevice from hkPresent ---
// --- is the primary source; directX::getDevice() is the assert/fallback only.  ---
HWND Dx9Backend::init(IDirect3DDevice9* device)
{
    if (device == nullptr)
    {
        // Fallback to the captured device only if the caller passed null.
        device = directX::getDevice();
        assert(device != nullptr && "Dx9Backend::init: no D3D9 device available");
    }

    if (device == nullptr)
    {
        // Still null -- caller (Plan 02 setup()) bails; no overlay install.
        return nullptr;
    }

    D3DDEVICE_CREATION_PARAMETERS cParam;
    device->GetCreationParameters(&cParam);
    ImGui_ImplDX9_Init(device);
    return cParam.hFocusWindow;
}

// --- Static-storage singleton (no heap; heap-free hot path) + dx9 accessor. ---
static Dx9Backend s_dx9Backend;

Dx9Backend* dx9Singleton()
{
    return &s_dx9Backend;
}
} // namespace render_backend
