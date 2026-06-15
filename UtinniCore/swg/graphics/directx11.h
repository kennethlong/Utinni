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

// Phase 19 / RNDR-02: the DXGI/D3D11 hook tier -- the twin of directx9.h's
// directX:: namespace. Unlike directX::, NOTHING here is UTINNI_API: the hook
// tier needs no managed binding (the consumer side acquires its pointers from
// the client's advertised GetHookPoints() export, not from a managed caller).
// This header carries ONLY free-function decls + the two pinned vtbl-index
// constants; the DX11/DXGI concrete types stay in directx11.cpp /
// render_backend_dx11.cpp (the only two TUs allowed <d3d11.h>/<imgui_impl_dx11.h>).
#include "utinni.h"
#include <dxgi1_2.h> // IDXGISwapChain1 for the accessor return types

// Forward declarations only -- keep <d3d11.h> out of this header (D-05 gate). The
// accessor return types are named by pointer; <d3d11.h> is pulled only in the two
// DX11-bearing TUs (directx11.cpp + render_backend_dx11.cpp).
struct ID3D11Device;
struct ID3D11DeviceContext;

namespace directX11
{
// DXGI IDXGISwapChain vtable indices the hook tier detours. Kept in lockstep
// with Dx11VtblOffsetTests.cpp (Plan 01) -- a drift there fails the build; a
// drift here would silently hook the wrong slot. base IDXGISwapChain::Present
// (NOT Present1); IDXGISwapChain1 inherits the same slots.
enum DxgiSwapChainVtbl
{
    dxgi_Present_Index = 8,
    dxgi_ResizeBuffers_Index = 13
};

// kickoff() is invoked ONCE from graphics.cpp::hkInstall (beside directX::detour()).
// It subscribes a per-frame poll thunk to the existing prePresent tick; the thunk
// calls tryInstall() each frame until the advertised swapchain latches, then
// unsubscribes itself. This solves the bootstrap problem: a DXGI Present detour
// cannot exist before the swapchain exists, so the pre-swapchain per-frame tick
// MUST come from the already-installed prePresent callback, not a self-detour.
void kickoff();

// tryInstall() is the advertised-contract consumer: GetModuleHandleA gl11_r/_d.dll
// -> GetProcAddress("GetHookPoints") -> poll swapChain != null -> install detours
// + the DX11 backend + the overlay. Idempotent: latched, a second call is a no-op.
bool tryInstall();

void cleanup();

// Borrowed accessors -- the advertised swapChain/device/context. NEVER Released.
IDXGISwapChain1* getSwapChain();
ID3D11Device* getDevice();
ID3D11DeviceContext* getContext();
} // namespace directX11
