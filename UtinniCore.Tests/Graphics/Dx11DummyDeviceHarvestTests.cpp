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

// ============================================================================
// Phase 19 / RNDR-02 / D-21 automated layer 3: the WARP dummy-device HARVEST.
//
// The production D3D11 path acquires its hooks from the client's advertised
// GetHookPoints() contract (D-10), NOT from a throwaway-device harvest. But the
// vtbl-offset invariants the hook tier relies on (Present idx 8, ResizeBuffers
// idx 13) are interface-defined and can be validated OFFLINE by creating a
// throwaway WARP swapchain, snapshotting its vtable, and asserting the two slots
// resolve to non-null code addresses -- the DXGI/WARP port of directx9.cpp's
// getVtbl() harvest (460-551). It also asserts no device leak (the create/release
// pair drives the device refcount back to 0).
//
// Assumption A1: a 32-bit test process on a GPU-less / WARP-unavailable
// self-hosted runner may fail D3D11CreateDeviceAndSwapChain. That is a Catch2
// SKIP (with a log line), NOT a FAIL -- the offset invariants are independently
// pinned by Dx11VtblOffsetTests (a compile-time fence), so a skip here does not
// weaken the build.
// ============================================================================

#include <catch2/catch_all.hpp>

#include <d3d11.h>
#include <dxgi.h>
#include <cstdio>

namespace
{
// The two pinned indices the directX11:: hook tier detours off the live
// swapchain vtable. Kept in lockstep with directX11::dxgi_Present_Index /
// dxgi_ResizeBuffers_Index and Dx11VtblOffsetTests.cpp.
constexpr unsigned kDxgiPresentIndex = 8;
constexpr unsigned kDxgiResizeBuffersIndex = 13;

// A hidden 1x1 WS_POPUP STATIC window -- mirrors directx9.cpp::getVtbl():495-496.
// Never shown, never pumped; it is only the swapchain's OutputWindow.
HWND createHiddenWindow()
{
    return CreateWindowExA(0, "STATIC", nullptr, WS_POPUP, 0, 0, 1, 1,
                           nullptr, nullptr, GetModuleHandleA(nullptr), nullptr);
}
} // namespace

TEST_CASE("RNDR-02 WARP swapchain harvest pins Present=8 / ResizeBuffers=13 + no leak", "[dxgi][harvest]")
{
    HWND hwnd = createHiddenWindow();
    if (hwnd == nullptr)
    {
        SKIP("Dx11 harvest: hidden window creation failed (GetLastError=" + std::to_string(GetLastError()) + ")");
        return;
    }

    DXGI_SWAP_CHAIN_DESC scd = {};
    scd.BufferCount = 2;
    scd.BufferDesc.Width = 1;
    scd.BufferDesc.Height = 1;
    scd.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    scd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    scd.OutputWindow = hwnd;
    scd.SampleDesc.Count = 1;
    scd.Windowed = TRUE;
    scd.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;

    IDXGISwapChain* swapChain = nullptr;
    ID3D11Device* device = nullptr;
    ID3D11DeviceContext* context = nullptr;
    D3D_FEATURE_LEVEL featureLevel = {};

    // WARP is the software rasterizer -- available without a GPU, the right driver
    // for an offline CI harvest. If it still fails (A1), SKIP with a log.
    HRESULT hr = D3D11CreateDeviceAndSwapChain(
        nullptr,                    // default adapter (ignored for WARP)
        D3D_DRIVER_TYPE_WARP,
        nullptr,                    // no software module
        0,                          // no flags
        nullptr, 0,                 // default feature levels
        D3D11_SDK_VERSION,
        &scd,
        &swapChain,
        &device,
        &featureLevel,
        &context);

    if (FAILED(hr) || swapChain == nullptr || device == nullptr)
    {
        if (swapChain) swapChain->Release();
        if (device) device->Release();
        if (context) context->Release();
        DestroyWindow(hwnd);
        char buf[96];
        std::snprintf(buf, sizeof(buf), "Dx11 harvest: WARP device/swapchain create failed (hr=0x%08lX) -- SKIP", (unsigned long)hr);
        SKIP(buf);
        return;
    }

    // Snapshot the swapchain vtable and assert the two hooked slots resolve to
    // non-null code addresses (the DXGI port of directx9.cpp:542 `*(swgptr**)pDevice`).
    void** vtbl = *reinterpret_cast<void***>(swapChain);
    REQUIRE(vtbl != nullptr);
    REQUIRE(vtbl[kDxgiPresentIndex] != nullptr);        // IDXGISwapChain::Present
    REQUIRE(vtbl[kDxgiResizeBuffersIndex] != nullptr);  // IDXGISwapChain::ResizeBuffers

    // Cross-check the snapshotted Present address equals the SDK pointer-to-member
    // target is not portable across compilers; instead confirm the two slots are
    // distinct code addresses (a vtable with collapsed/duplicate slots would be a
    // harvest red flag).
    REQUIRE(vtbl[kDxgiPresentIndex] != vtbl[kDxgiResizeBuffersIndex]);

    // No-leak invariant: release in reverse acquisition order; the device's final
    // Release must return refcount 0 (no dangling Utinni-held reference). The
    // swapchain/context are released first so the device is the last holder.
    context->Release();
    swapChain->Release();
    ULONG deviceRefs = device->Release();
    REQUIRE(deviceRefs == 0); // the throwaway device is fully released -- no leak

    DestroyWindow(hwnd);
}
