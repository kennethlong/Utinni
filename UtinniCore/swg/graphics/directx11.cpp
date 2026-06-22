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

// Phase 19 / RNDR-02: the DXGI/D3D11 hook tier -- the twin of directx9.cpp's
// directX:: namespace. It owns the Present/ResizeBuffers trampolines, the
// advertised-contract consumer (GetHookPoints() poll, which REPLACES the D3D9
// throwaway-device harvest in production -- D-10), the DetourXS install, and the
// per-frame kick-off poll. It is ONE of only two new TUs allowed <d3d11.h>/
// <dxgi1_2.h> (the other is render_backend_dx11.cpp). The imgui DX11-backend
// binding calls live in render_backend_dx11.cpp (D-05 purge): directx11.cpp drives
// the seam (set + init + setup) but does NOT itself call the imgui DX11 binding.
#include "directx11.h"
#include <d3d11.h>
#include <dxgi1_2.h>
#include "utinni.h"
#include <cstdio> // std::snprintf for the one-shot acquisition diagnostic (tryInstall)
#include "swg/ui/imgui_impl.h"
#include "graphics.h" // utinni::Graphics::subscribe/unsubscribePrePresentCallback
#include "render_backend.h"
#include "DetourXS/detourxs.h"

namespace directX11
{
// --- The advertised contract POD (spec §3.1 / producer Direct3d11.cpp:872-877). ---
// Field order/type MUST be byte-identical to the producer (Pitfall 6 ABI match).
struct UtinniDx11HookPoints
{
    IDXGISwapChain1* swapChain;
    ID3D11Device* device;
    ID3D11DeviceContext* context;
};
using pGetHookPoints = UtinniDx11HookPoints(__cdecl*)();

// --- Trampoline typedefs. base IDXGISwapChain::Present -- the base-interface     ---
// --- present slot (idx 8), NOT the IDXGISwapChain1 variant (spec §4.4);          ---
// --- IDXGISwapChain1 inherits the same slots. ---
using pSwapChainPresent = HRESULT(__stdcall*)(IDXGISwapChain* pSwapChain, UINT SyncInterval, UINT Flags);
using pResizeBuffers = HRESULT(__stdcall*)(IDXGISwapChain* pSwapChain, UINT BufferCount, UINT Width, UINT Height, DXGI_FORMAT NewFormat, UINT SwapChainFlags);

static pSwapChainPresent origPresent = nullptr;
static pResizeBuffers origResizeBuffers = nullptr;

// --- Borrowed pointers from the advertised contract. NEVER Released (spec §4.1). ---
static IDXGISwapChain1* s_swapChain = nullptr;
static ID3D11Device* s_device = nullptr;
static ID3D11DeviceContext* s_context = nullptr;

// --- Install latch (Pitfall 3): fronts the whole tryInstall sequence so a second ---
// --- call after the latch is a no-op (no double-init/double-detour/2nd ImGui ctx). ---
static bool s_installed = false;

// --- The per-frame poll handle (Graphics::subscribePrePresentCallback return). ---
static int s_pollHandle = 0;

// --- DXGI Present hook. One-shot first-fire log; render BEFORE the original; NO ---
// --- blockPresentCall branch and NO depth-texture reach-in (D3D9-only). ---
HRESULT __stdcall hkSwapChainPresent(IDXGISwapChain* sc, UINT syncInterval, UINT flags)
{
    static bool s_firstPresent = true;
    if (s_firstPresent)
    {
        s_firstPresent = false;
        utinni::log::info("directX11::hkSwapChainPresent: first fire (DXGI detour confirmed)");
    }

    imgui_impl::render(); // -> seam newFrame()/renderDrawData() (render_backend_dx11.cpp)

    return origPresent(sc, syncInterval, flags);
}

// --- DXGI ResizeBuffers hook. Release Utinni's RTV (onPreResize) BEFORE the     ---
// --- original, run the original, recreate the RTV (onPostResize) AFTER, and     ---
// --- return the original's HRESULT. The release-before ordering avoids          ---
// --- DXGI_ERROR_INVALID_CALL (Pitfall 2 / T-19-07). ---
HRESULT __stdcall hkResizeBuffers(IDXGISwapChain* sc, UINT bufferCount, UINT width, UINT height, DXGI_FORMAT newFormat, UINT swapChainFlags)
{
    render_backend::IRenderBackend* backend = render_backend::get();
    if (backend != nullptr)
    {
        backend->onPreResize(); // release ONLY Utinni's own RTV, before the original
    }

    HRESULT hr = origResizeBuffers(sc, bufferCount, width, height, newFormat, swapChainFlags);

    if (backend != nullptr)
    {
        backend->onPostResize(); // recreate the RTV from the resized backbuffer
    }

    return hr;
}

// --- The advertised-contract consumer. Polled once/frame until the swapchain is ---
// --- live, then runs the LOCKED install sequence and latches. Idempotent. ---
bool tryInstall()
{
    if (s_installed)
    {
        return true; // latch fronts the whole sequence -- a second call is a no-op
    }

    HMODULE hGl11 = GetModuleHandleA("gl11_r.dll");
    if (hGl11 == nullptr)
    {
        hGl11 = GetModuleHandleA("gl11_d.dll"); // debug client
    }
    if (hGl11 == nullptr)
    {
        return false; // no D3D11 client -- leave the Dx9 default in place
    }

    auto getHookPoints = (pGetHookPoints)GetProcAddress(hGl11, "GetHookPoints");
    if (getHookPoints == nullptr)
    {
        // spec §7.2: a gl11_r.dll without the export is a safe no-overlay state.
        // Log ONCE then keep polling is pointless (the export will never appear in
        // this session) -- latch installed to stop the per-frame call would be
        // wrong (we did NOT install). Instead log a one-shot warning here.
        static bool s_warnedNoExport = true;
        if (s_warnedNoExport)
        {
            s_warnedNoExport = false;
            utinni::log::warning("directX11::tryInstall: gl11 loaded but GetHookPoints not exported; no D3D11 overlay (graceful bail)");
        }
        return false; // graceful bail (T-19-04) -- must NOT crash
    }

    UtinniDx11HookPoints hp = getHookPoints();

    // One-shot acquisition diagnostic (24-DX11-ADVERTISED-CLIENT-GAP.md). The first
    // time the advertised swapChain is non-null, log all THREE borrowed pointers. On a
    // correct gl11 provider they are non-null together (EPA-08 proof: D3D11CreateDevice
    // populates device+context BEFORE CreateSwapChainForHwnd, and destroy() resets the
    // swapChain first -- so there is no window where swapChain != null && (device == null
    // || context == null)). Logging them turns the otherwise-silent advertised-exe + DX11
    // 0xC0000005 WRITE target=0x34 into an observable: if device/context were ever null
    // behind a non-null swapChain, this line shows it instead of faulting downstream in
    // dx11Singleton()->init(s_device, s_context).
    static bool s_loggedAcquire = false;
    if (!s_loggedAcquire && hp.swapChain != nullptr)
    {
        s_loggedAcquire = true;
        char buf[176];
        std::snprintf(buf, sizeof(buf),
                      "directX11::tryInstall: GetHookPoints acquired -- swapChain=0x%p device=0x%p context=0x%p",
                      (void*)hp.swapChain, (void*)hp.device, (void*)hp.context);
        utinni::log::info(buf);
    }

    // Poll until ALL THREE advertised pointers are live, not just the swapChain. The
    // LOCKED install sequence below dereferences device + context (step 4,
    // dx11Singleton()->init(s_device, s_context)); requiring them here converts a null
    // device/context into a clean poll-again instead of a downstream null-deref. Per
    // EPA-08 the provider already guarantees all three together, so on a correct client
    // this never delays install beyond the existing swapChain-null wait.
    if (hp.swapChain == nullptr || hp.device == nullptr || hp.context == nullptr)
    {
        return false; // not ready (spec §3.3 / T-19-03) -- poll again next frame
    }

    // === LOCKED install sequence (the seam the whole phase converges on) ===
    // (1) stash the borrowed pointers for the accessors (NEVER Released).
    s_swapChain = hp.swapChain;
    s_device = hp.device;
    s_context = hp.context;

    // (2) read vtbl idx 8/13 off the LIVE swapchain; CheckPointer + Create both.
    swgptr* vtbl = *(swgptr**)hp.swapChain; // mirrors directx9.cpp:542 `*(swgptr**)pDevice`
    swgptr presentAddr = Detour::CheckPointer(vtbl[dxgi_Present_Index]);
    origPresent = (pSwapChainPresent)Detour::Create((LPVOID)presentAddr, hkSwapChainPresent, DETOUR_TYPE_PUSH_RET);
    swgptr resizeAddr = Detour::CheckPointer(vtbl[dxgi_ResizeBuffers_Index]);
    origResizeBuffers = (pResizeBuffers)Detour::Create((LPVOID)resizeAddr, hkResizeBuffers, DETOUR_TYPE_PUSH_RET);

    // (3) install the DX11 backend BEFORE init (mirrors imgui_impl.cpp "set before init").
    render_backend::set(render_backend::dx11Singleton());

    // (4) the DX11 device-bearing init (the imgui DX11 binding init runs inside;
    //     HWND from the swapChain). If it returns null, undo and bail -- no overlay
    //     rather than a half-install (checker Blocker 1).
    const HWND hwnd = render_backend::dx11Singleton()->init(s_device, s_context);
    if (hwnd == nullptr)
    {
        render_backend::set(nullptr);
        utinni::log::warning("directX11::tryInstall: DX11 init() returned null HWND; aborting install (no overlay)");
        return false;
    }

    // (5) drive the HWND-bearing setup tail (CreateContext-if-needed, WndProc
    //     subclass, style/font, isSetup=true). Plan 03 makes setup() SKIP its own
    //     dx9Singleton()->init(nullptr) when the installed backend is not the dx9
    //     singleton (the DX11 device init already happened in step 4).
    imgui_impl::setup(hwnd);

    s_installed = true;
    utinni::log::info("directX11::tryInstall: D3D11 overlay installed (advertised swapchain latched)");
    return true;
}

// --- The per-frame poll thunk (file-local void(*)()). Calls tryInstall() while ---
// --- not-yet-latched; once latched it unsubscribes itself to stop the wasted    ---
// --- per-frame call (mirrors directx9.cpp:378 one-shot-then-stop discipline). ---
static void pollThunk()
{
    if (s_installed)
    {
        return; // already latched -- the live hkSwapChainPresent is the per-frame hook now
    }

    if (tryInstall())
    {
        // Symmetric unsubscribe (T-19-09 / CON-H-03): the prePresent registry
        // snapshots subscribers under lock before dispatch, so unsubscribing from
        // inside the dispatch is safe; the single static handle guards double-unsub.
        if (s_pollHandle != 0)
        {
            utinni::Graphics::unsubscribePrePresentCallback(s_pollHandle);
            s_pollHandle = 0;
        }
    }
}

// --- The single owned kick-off site, invoked ONCE from graphics.cpp::hkInstall. ---
// --- Subscribes the poll thunk to the already-installed per-frame prePresent tick ---
// --- (the bootstrap-correct shape: NOT a self-installed DXGI Present detour, which ---
// --- cannot exist before the swapchain exists). ---
void kickoff()
{
    if (s_pollHandle != 0 || s_installed)
    {
        return; // idempotent -- already subscribed (or already latched)
    }
    s_pollHandle = utinni::Graphics::subscribePrePresentCallback(&pollThunk);
    utinni::log::info("directX11::kickoff: subscribed per-frame GetHookPoints poll (D3D11 acquisition)");
}

void cleanup()
{
    // WR-03 (mirror directx9.cpp::cleanup): do NOT free borrowed/device-bound state
    // on DLL_PROCESS_DETACH. The advertised swapChain/device/context are BORROWED
    // (client-owned ComPtrs) and are NEVER Released by Utinni (spec §4.1 / T-19-05).
    // The OS reclaims everything on process exit; a UAF-on-exit is worse than a
    // bounded process-lifetime leak. cleanup() intentionally releases nothing.
}

IDXGISwapChain1* getSwapChain()
{
    return s_swapChain;
}

ID3D11Device* getDevice()
{
    return s_device;
}

ID3D11DeviceContext* getContext()
{
    return s_context;
}
} // namespace directX11
