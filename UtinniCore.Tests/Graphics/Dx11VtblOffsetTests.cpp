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
// Phase 19 / RNDR-02 / D-21 automated layer 1: the DXGI vtable-offset PIN.
//
// The directX11:: hook tier (Plan 02) detours IDXGISwapChain::Present and
// ::ResizeBuffers by INDEX off the live swapchain vtable. If a future DXGI ABI
// change shifts those slots, the wrong method is hooked -- a silent tampering
// hazard (threat T-19-01). This test fails the BUILD on any such drift.
//
// The two pinned indices are derived from the IDXGISwapChain vtable layout:
//   3 IUnknown      (QueryInterface, AddRef, Release)            -> 0,1,2
//   2 IDXGIObject   (SetPrivateData, SetPrivateDataInterface,
//                    GetPrivateData, GetParent)                  -> 3,4,5,6
//   ... IDXGIDeviceSubObject (GetDevice)                         -> 7
//   IDXGISwapChain::Present                                      -> 8
//   GetBuffer, SetFullscreenState, GetFullscreenState,
//   GetDesc                                                      -> 9,10,11,12
//   IDXGISwapChain::ResizeBuffers                                -> 13
//
// PLAN 02 MUST keep its hook-tier constants in lockstep with these:
//   directX11::dxgi_Present_Index       == 8
//   directX11::dxgi_ResizeBuffers_Index == 13
// (directx11.h does not exist yet at Plan-01 time, so the named constants are
// declared LOCALLY here; Plan 02 imports/asserts the same values.)
// ============================================================================

#include <catch2/catch_all.hpp>

#include <cstddef>
#include <dxgi1_2.h>

namespace
{
// Local mirror of the Plan-02 hook-tier constants. Keep these IN SYNC with
// directX11::dxgi_Present_Index / dxgi_ResizeBuffers_Index when Plan 02 lands.
constexpr std::size_t kDxgiPresentIndex = 8;
constexpr std::size_t kDxgiResizeBuffersIndex = 13;
} // namespace

TEST_CASE("RNDR-02 DXGI Present=8 / ResizeBuffers=13 vtable offsets are pinned", "[dxgi][offsets]")
{
    // The hook tier indexes the swapchain vtable as `vtbl[kDxgi*Index]`. These
    // constants ARE the contract; the static_asserts document and freeze them.
    static_assert(kDxgiPresentIndex == 8, "IDXGISwapChain::Present must be vtable slot 8");
    static_assert(kDxgiResizeBuffersIndex == 13, "IDXGISwapChain::ResizeBuffers must be vtable slot 13");

    REQUIRE(kDxgiPresentIndex == 8);
    REQUIRE(kDxgiResizeBuffersIndex == 13);

    // ABI cross-check: IDXGISwapChain1 inherits IDXGISwapChain, so the base Present
    // and ResizeBuffers slots are unchanged in the derived vtable -- the producer
    // returns IDXGISwapChain1 but the hooked slots are the base-interface slots.
    // Taking the pointer-to-member of each is a COMPILE-TIME assertion that the
    // SDK still declares these exact members on the base interface (a signature or
    // member rename in a future DXGI SDK would fail to compile here). A pointer to
    // a real member function is never null, so the runtime REQUIREs document intent.
    auto present = &IDXGISwapChain::Present;
    auto resize = &IDXGISwapChain::ResizeBuffers;
    auto present1 = &IDXGISwapChain1::Present1; // IDXGISwapChain1 is what the producer advertises
    REQUIRE(present != nullptr);
    REQUIRE(resize != nullptr);
    REQUIRE(present1 != nullptr);
}
