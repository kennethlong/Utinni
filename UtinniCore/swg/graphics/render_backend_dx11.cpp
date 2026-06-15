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

// Phase 19 / RNDR-02: render_backend_dx11.cpp is the DX11-BEARING half of the
// seam (the DXGI twin of render_backend_dx9.cpp). It is one of only two new TUs
// that may include <imgui_impl_dx11.h> (the other is directx11.cpp); the
// ImGui_ImplDX11_* calls live HERE per the D-05 purge. The Dx11Backend twin plugs
// the SAME 10 vtable overrides into the SAME IRenderBackend seam. KEY DIFFERENCES
// from Dx9Backend: newFrame() rebinds the flip-discard-unbound backbuffer RTV
// before the imgui new-frame; onPreResize/onPostResize are NOT no-ops -- they
// release/recreate the RTV (true DXGI ResizeBuffers, D-18, NOT the D3D9 never-Reset
// rule); init() takes BOTH device + immediate context. The directX11:: hook tier
// owns the borrowed swapChain/device/context (NEVER Released); this file releases
// ONLY Utinni-created RTVs (and the GetBuffer backbuffer temp).
#include "render_backend.h"
#include <d3d11.h>
#include <dxgi1_2.h>
#include <imgui_impl_dx11.h>
#include "directx11.h" // directX11:: getSwapChain/getDevice/getContext
#include "graphics.h"  // utinni::Graphics::getCurrentRenderTargetWidth/Height
#include "utinni.h"
#include <cassert>

namespace render_backend
{
// --- Private helper: (re)create the cached backbuffer RTV from the live, ---
// --- advertised swapchain. The GetBuffer backbuffer temp IS Utinni-acquired ---
// --- and is released here; the swapChain/device are BORROWED and NOT released. ---
static ID3D11RenderTargetView* createBackbufferRtv()
{
    IDXGISwapChain1* swapChain = directX11::getSwapChain();
    ID3D11Device* device = directX11::getDevice();
    if (swapChain == nullptr || device == nullptr)
    {
        return nullptr;
    }

    ID3D11Texture2D* backbuffer = nullptr;
    HRESULT hr = swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&backbuffer));
    if (FAILED(hr) || backbuffer == nullptr)
    {
        utinni::log::warning("Dx11Backend: GetBuffer(0) failed; no backbuffer RTV");
        return nullptr;
    }

    ID3D11RenderTargetView* rtv = nullptr;
    hr = device->CreateRenderTargetView(backbuffer, nullptr, &rtv);
    backbuffer->Release(); // Utinni-acquired temp -- release (the swapChain is borrowed)
    if (FAILED(hr))
    {
        utinni::log::warning("Dx11Backend: CreateRenderTargetView failed");
        return nullptr;
    }
    return rtv;
}

// --- Per-frame hot path. Flip-discard (producer Direct3d11_Device.cpp:570) ---
// --- unbinds the RTV after every Present, so rebind the CACHED RTV before the ---
// --- imgui new-frame (heap-free: the RTV is created once and recreated only on ---
// --- resize). ---
void Dx11Backend::newFrame()
{
    if (m_rtv == nullptr)
    {
        m_rtv = createBackbufferRtv();
    }

    ID3D11DeviceContext* context = directX11::getContext();
    if (context != nullptr && m_rtv != nullptr)
    {
        ID3D11RenderTargetView* rtv = m_rtv;
        context->OMSetRenderTargets(1, &rtv, nullptr); // rebind the flip-discard-unbound RTV
    }

    ImGui_ImplDX11_NewFrame();
}

void Dx11Backend::renderDrawData(ImDrawData* drawData)
{
    ImGui_ImplDX11_RenderDrawData(drawData);
}

// --- Resize hooks (D-18): REAL work, unlike Dx9's no-ops. Release the RTV ---
// --- BEFORE the original ResizeBuffers (else DXGI_ERROR_INVALID_CALL), recreate ---
// --- it from the resized backbuffer AFTER. ---
void Dx11Backend::onPreResize()
{
    if (m_rtv != nullptr)
    {
        m_rtv->Release(); // Utinni-created RTV -- safe to release
        m_rtv = nullptr;
    }
}

void Dx11Backend::onPostResize()
{
    m_rtv = createBackbufferRtv();
}

// --- RT-dims (D-19): forward to the API-neutral SWG Graphics facade, verbatim ---
// --- from Dx9Backend. ---
int Dx11Backend::renderTargetWidth()
{
    return utinni::Graphics::getCurrentRenderTargetWidth();
}

int Dx11Backend::renderTargetHeight()
{
    return utinni::Graphics::getCurrentRenderTargetHeight();
}

// --- Scene depth/color + stage (MVP: no DX11 depth SRV this phase). ---
ImTextureID Dx11Backend::sceneDepthTexture()
{
    return (ImTextureID)0;
}

ImTextureID Dx11Backend::sceneColorTexture()
{
    return (ImTextureID)0;
}

int Dx11Backend::sceneDepthStage()
{
    return 0;
}

void Dx11Backend::setSceneDepthStage(int /*stage*/)
{
    // no-op: no DX11 depth-stage diagnostic wired this phase (MVP)
}

// --- NON-VIRTUAL device-bearing init (off the vtable). Takes BOTH device + ---
// --- immediate context (ImGui_ImplDX11_Init needs both). HWND derived from the ---
// --- advertised swapchain. Returns null if device/context/swapchain unavailable ---
// --- (caller -- directX11::tryInstall -- bails). ---
HWND Dx11Backend::init(ID3D11Device* device, ID3D11DeviceContext* context)
{
    if (device == nullptr || context == nullptr)
    {
        assert(false && "Dx11Backend::init: device/context must be non-null");
        return nullptr;
    }

    IDXGISwapChain1* swapChain = directX11::getSwapChain();
    if (swapChain == nullptr)
    {
        return nullptr;
    }

    HWND hwnd = nullptr;
    if (FAILED(swapChain->GetHwnd(&hwnd)) || hwnd == nullptr)
    {
        utinni::log::warning("Dx11Backend::init: swapChain->GetHwnd failed; no overlay HWND");
        return nullptr;
    }

    ImGui_ImplDX11_Init(device, context);
    return hwnd;
}

// --- Static-storage singleton (no heap; heap-free hot path) + dx11 accessor. ---
static Dx11Backend s_dx11Backend;

Dx11Backend* dx11Singleton()
{
    return &s_dx11Backend;
}
} // namespace render_backend
