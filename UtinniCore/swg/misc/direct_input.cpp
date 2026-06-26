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

#include "direct_input.h"
#include "swg/client/client.h"
#include "swg/endpoints.h"
#include "swg/graphics/graphics.h"

#include <dinput.h>
#include <intrin.h>
#include <atomic>

// dxguid.lib provides the GUID_SysKeyboard / GUID_SysMouse symbol bodies used
// by the vtable shim below. Only the GUIDs are linked; we resolve all
// functions dynamically via dinput8.dll. No COM init required.
#pragma comment(lib, "dxguid.lib")

namespace swg::directInput
{
using pSuspend = void(__cdecl*)();
using pResume = void(__cdecl*)();
using pSetupInstall = int(__cdecl*)(HINSTANCE hInstance, HWND hwnd, DWORD menuKey, DWORD unk);

pSuspend suspend = (pSuspend)0x00420880;
pSuspend resume = (pSuspend)0x00420890;

pSetupInstall setupInstall = (pSetupInstall)0x00421490;
} // namespace swg::directInput

// WS-0: the advertised-client Enter-mask masks in-world Enter ONLY (never login/character-select). It
// reads the consumer-maintained "advertised editor scene loaded" flag (set after the advertised loadScene,
// cleared on cleanup -- defined in game.cpp). Forward-declared so we don't pull game.h into this DI TU.
namespace swg::game
{
bool isEditorSceneLoaded();
} // namespace swg::game

// 2026-05-20 Issue #10 Phase B prereq (per CODEX consult #3):
// COM-vtable shim around IDirectInputDevice8::SetCooperativeLevel. We need
// the baseline coop-level flags SWG uses BEFORE the managed side reparents
// SWG's top-level window into PanelGame, because Microsoft docs are explicit
// that SetCooperativeLevel requires a TOP-LEVEL HWND -- reparenting to
// WS_CHILD would break it. The CODEX-guided fix is a borderless OWNED popup
// (preserves top-level), but we want runtime confirmation of the flags first.
//
// Hook chain:
//   DirectInput8Create (function detour on dinput8.dll!DirectInput8Create)
//     -> captures IDirectInput8A* on first call
//     -> patches IDirectInput8A vtbl[3]  (CreateDevice)
//   CreateDevice (vtable patch)
//     -> captures IDirectInputDevice8A* on each device (keyboard, mouse)
//     -> patches IDirectInputDevice8A vtbl[13]  (SetCooperativeLevel) once
//        (keyboard + mouse share the same vtable in dinput8)
//   SetCooperativeLevel (vtable patch)
//     -> logs hwnd + decoded DISCL_* flags + per-call caller PC, calls through.
//
// Lifecycle: DirectInput::detour() runs during utinni_init (NOT DllMain),
// BEFORE SWG's main thread is released from EB FE. dinput8.dll is statically
// imported by SWGEmu.exe so GetModuleHandle succeeds.
namespace
{
using pDirectInput8Create = HRESULT(WINAPI*)(HINSTANCE, DWORD, REFIID, LPVOID*, LPUNKNOWN);
using pDIO_CreateDevice = HRESULT(__stdcall*)(IDirectInput8A*, REFGUID, IDirectInputDevice8A**, LPUNKNOWN);
using pDID_SetCooperativeLevel = HRESULT(__stdcall*)(IDirectInputDevice8A*, HWND, DWORD);

pDirectInput8Create origDirectInput8Create = nullptr;
pDIO_CreateDevice origCreateDevice = nullptr;
pDID_SetCooperativeLevel origSetCooperativeLevel = nullptr;

// 2026-06-25 advertised-client Enter-mask: SWG reads the keyboard via BUFFERED
// GetDeviceData (vtbl[10], DIPROP_BUFFERSIZE set; see clientDirectInput DirectInput.cpp),
// NOT immediate GetDeviceState. On the advertised editor client chat/input subsystems are
// locked, so a live in-world Enter has no real use -- it only triggers SWG's native
// chat-open -> window-level fullscreen restyle (the embed grow+snap-back bounce,
// swg-window-resize-fullscreen-edge-cases.md). We filter DIK_RETURN/DIK_NUMPADENTER out of
// the KEYBOARD device's GetDeviceData buffer so SWG never sees the keypress. Keyboard-device
// + advertised-client gated -> zero effect on SWGEmu connected play. REVISIT when MISC/INPUT
// unlocks and in-world Enter gains a real use.
using pDID_GetDeviceData = HRESULT(__stdcall*)(IDirectInputDevice8A*, DWORD, LPDIDEVICEOBJECTDATA, LPDWORD, DWORD);
pDID_GetDeviceData origGetDeviceData = nullptr;
IDirectInputDevice8A* s_keyboardDevice = nullptr;

// 2026-06-07 RESID-04 / D-12: runtime toggle for the exclusive-fullscreen
// suppression. Default ON. When ON, an incoming DISCL_EXCLUSIVE request is
// redirected to DISCL_NONEXCLUSIVE (FOREGROUND/BACKGROUND preserved) so SWG
// stays windowed-embedded instead of flipping the D3D9 device into true
// exclusive fullscreen (which detaches the owned-popup embed and kills input
// routing -- see .planning/debug/chat-open-d3d9-fullscreen.md +
// .planning/todos/pending/swg-window-resize-fullscreen-edge-cases.md).
//
// The toggle exists so the maintainer can A/B the suppression LIVE without a
// rebuild (15-08 smoke): flip s_suppressExclusiveFullscreen to false to let
// SWG's original DISCL_EXCLUSIVE through and observe the un-suppressed mode
// switch (the DISCL log below is the confirmation instrument). It also keeps
// the deferred "detached-fullscreen-with-reattach" fallback (CONTEXT Open Q3)
// reachable should suppress prove wrong on the first live run.
//
// std::atomic<bool> so a maintainer-driven toggle (future debug verb / hotkey)
// can flip it from another thread without tearing relative to the DI pump.
std::atomic<bool> s_suppressExclusiveFullscreen{true};

// WS-0 (staged Enter-mask kill switch). Crew review 2026-06-25: input filtering at the DI layer is
// coupled/risky -- land the actual masking AFTER one live smoke validates the scene-loaded edges. So the
// advertised Enter-mask ships DISARMED: hkGetDeviceData only drops Enter when this is true AND a scene is
// loaded AND we're the advertised client. Read ONCE from the [Editor] advertisedEnterMask ini key in
// DirectInput::detour() at init (default false) -- to arm it for the WS-2 smoke, set the key in ut.ini and
// RELAUNCH (it is not a live toggle). While false, Enter passes through untouched on EVERY target (no change).
std::atomic<bool> s_enterMaskEnabled{false};

void decodeCoopFlags(DWORD f, char* out, size_t sz)
{
    snprintf(out, sz, "%s%s%s%s%s(0x%lX)",
             (f & DISCL_EXCLUSIVE) ? "EXCLUSIVE " : "",
             (f & DISCL_NONEXCLUSIVE) ? "NONEXCLUSIVE " : "",
             (f & DISCL_FOREGROUND) ? "FOREGROUND " : "",
             (f & DISCL_BACKGROUND) ? "BACKGROUND " : "",
             (f & DISCL_NOWINKEY) ? "NOWINKEY " : "",
             (unsigned long)f);
}

HRESULT __stdcall hkSetCooperativeLevel(IDirectInputDevice8A* pThis, HWND hwnd, DWORD dwFlags)
{
    // Caller PC (return address): caller-of-this-function = *(esp), which on
    // __stdcall after the call instruction is one slot above our first arg.
    // Use _ReturnAddress() intrinsic for portability.
    void* ret = _ReturnAddress();
    char flagstr[96];
    decodeCoopFlags(dwFlags, flagstr, sizeof(flagstr));
    char msg[240];
    snprintf(msg, sizeof(msg),
             "DI::SetCooperativeLevel: device=0x%p hwnd=0x%p flags=%s caller=0x%p",
             (void*)pThis, (void*)hwnd, flagstr, ret);
    utinni::log::info(msg);

    // RESID-04 / D-12: suppress/redirect the exclusive cooperative level to keep
    // SWG windowed-embedded. DISCL_EXCLUSIVE is the prime-suspect trigger
    // (RESEARCH A4) for SWG flipping the D3D9 device into true exclusive
    // fullscreen; rewriting it to DISCL_NONEXCLUSIVE keeps the owned-popup embed
    // intact and the OS hardware cursor + input routing working. We preserve
    // DISCL_FOREGROUND/DISCL_BACKGROUND (and any other coop bits) so chat/input
    // acquisition semantics are otherwise unchanged. NEVER touch the D3D9 device
    // here -- the no-Reset constraint (D-13) lives entirely in the windowed
    // Present self-stretch path; this is a pure DirectInput-level redirect.
    //
    // WS-0 (advertised client): the D-12 redirect was a D3D9/SWGEmu embed mechanism. Before WS-0 the DI
    // detour was NOT installed on the advertised client, so SWG's coop level passed through UNMODIFIED and
    // the (DX11/RNDR-04) native embed worked. WS-0 installs this hook on the advertised client too (for the
    // keyboard-device vtable capture), so to preserve that known-good behavior we EXCLUDE the advertised
    // client from the rewrite here -- the hook stays a pure pass-through + log there. Whether the advertised
    // embed actually wants NONEXCLUSIVE is a WS-2 smoke question; arming it there is a separate slice (like
    // the staged Enter-mask). SWGEmu is unchanged (isAdvertisedClient() is false -- D-00).
    DWORD effectiveFlags = dwFlags;
    if (s_suppressExclusiveFullscreen.load(std::memory_order_relaxed) && (dwFlags & DISCL_EXCLUSIVE) &&
        !swg::endpoints::isAdvertisedClient())
    {
        // Clear EXCLUSIVE, set NONEXCLUSIVE; leave FOREGROUND/BACKGROUND/NOWINKEY
        // exactly as SWG requested.
        effectiveFlags = (dwFlags & ~static_cast<DWORD>(DISCL_EXCLUSIVE)) | DISCL_NONEXCLUSIVE;

        char redirMsg[200];
        snprintf(redirMsg, sizeof(redirMsg),
                 "DI::SetCooperativeLevel: D-12 suppress -> redirected EXCLUSIVE to "
                 "NONEXCLUSIVE (0x%lX -> 0x%lX) to keep SWG windowed-embedded",
                 (unsigned long)dwFlags, (unsigned long)effectiveFlags);
        utinni::log::info(redirMsg);
    }

    return origSetCooperativeLevel(pThis, hwnd, effectiveFlags);
}

HRESULT __stdcall hkGetDeviceData(IDirectInputDevice8A* pThis, DWORD cbObjectData,
                                  LPDIDEVICEOBJECTDATA rgdod, LPDWORD pdwInOut, DWORD dwFlags)
{
    HRESULT hr = origGetDeviceData(pThis, cbObjectData, rgdod, pdwInOut, dwFlags);

    // Advertised-client Enter-mask: drop DIK_RETURN/DIK_NUMPADENTER from the keyboard's buffered
    // events so SWG's native in-world Enter -> chat-open -> fullscreen restyle never fires. Only
    // the keyboard device, only the advertised editor client, only the array-returning form
    // (rgdod != null). The count-query form (rgdod == null) and the mouse device pass through.
    // The dropped events stay flushed from the DI buffer (SWG's reads are non-peek); they are
    // simply hidden from SWG.
    //
    // WS-0 gating: mask ONLY while an advertised editor scene is loaded (isEditorSceneLoaded()) AND the
    // kill switch is armed (s_enterMaskEnabled, default OFF). The scene-loaded gate is what fixes the
    // reverted bug -- the prior unconditional advertised mask killed Enter on login + character-select.
    // The kill switch ships disarmed until one smoke validates the scene-loaded edges (crew 2026-06-25).
    if (SUCCEEDED(hr) && pThis == s_keyboardDevice && rgdod != nullptr && pdwInOut != nullptr &&
        cbObjectData == sizeof(DIDEVICEOBJECTDATA) && swg::endpoints::isAdvertisedClient() &&
        s_enterMaskEnabled.load(std::memory_order_relaxed) && swg::game::isEditorSceneLoaded())
    {
        DWORD const n = *pdwInOut;
        DWORD w = 0;
        for (DWORD r = 0; r < n; ++r)
        {
            if (rgdod[r].dwOfs == DIK_RETURN || rgdod[r].dwOfs == DIK_NUMPADENTER)
                continue; // hide the Enter press/release from SWG
            if (w != r)
                rgdod[w] = rgdod[r];
            ++w;
        }
        *pdwInOut = w;
    }
    return hr;
}

void patchDeviceVtableOnce(IDirectInputDevice8A* device)
{
    if (device == nullptr)
        return;
    if (origSetCooperativeLevel != nullptr)
        return; // already patched (keyboard + mouse share the dinput8 device vtable)

    void** vtbl = *(void***)device;
    DWORD oldProtect = 0;

    origSetCooperativeLevel = (pDID_SetCooperativeLevel)vtbl[13];
    if (!VirtualProtect(&vtbl[13], sizeof(void*), PAGE_READWRITE, &oldProtect))
    {
        utinni::log::critical("DI: VirtualProtect(RW) on vtbl[13] FAILED -- shim DISABLED");
        origSetCooperativeLevel = nullptr;
        return;
    }
    vtbl[13] = (void*)hkSetCooperativeLevel;
    VirtualProtect(&vtbl[13], sizeof(void*), oldProtect, &oldProtect);
    utinni::log::info("DI: patched IDirectInputDevice8A::SetCooperativeLevel vtbl[13]");

    // Enter-mask: patch GetDeviceData (vtbl[10]) on the same shared device vtable. Gated to the
    // keyboard device + advertised client inside the hook, so mouse/SWGEmu are unaffected.
    origGetDeviceData = (pDID_GetDeviceData)vtbl[10];
    if (VirtualProtect(&vtbl[10], sizeof(void*), PAGE_READWRITE, &oldProtect))
    {
        vtbl[10] = (void*)hkGetDeviceData;
        VirtualProtect(&vtbl[10], sizeof(void*), oldProtect, &oldProtect);
        utinni::log::info("DI: patched IDirectInputDevice8A::GetDeviceData vtbl[10] (advertised Enter-mask)");
    }
    else
    {
        utinni::log::critical("DI: VirtualProtect(RW) on vtbl[10] FAILED -- Enter-mask DISABLED");
        origGetDeviceData = nullptr;
    }
}

HRESULT __stdcall hkCreateDevice(IDirectInput8A* pThis, REFGUID rguid,
                                 IDirectInputDevice8A** lplpDevice, LPUNKNOWN pUnkOuter)
{
    HRESULT hr = origCreateDevice(pThis, rguid, lplpDevice, pUnkOuter);
    if (SUCCEEDED(hr) && lplpDevice != nullptr && *lplpDevice != nullptr)
    {
        const char* devType = "?";
        if (IsEqualGUID(rguid, GUID_SysKeyboard))
        {
            devType = "keyboard";
            s_keyboardDevice = *lplpDevice; // Enter-mask: gate GetDeviceData filtering to this device
        }
        else if (IsEqualGUID(rguid, GUID_SysMouse))
            devType = "mouse";

        char msg[160];
        snprintf(msg, sizeof(msg),
                 "DI::CreateDevice -> %s device=0x%p (hr=0x%lX)",
                 devType, (void*)*lplpDevice, (unsigned long)hr);
        utinni::log::info(msg);

        patchDeviceVtableOnce(*lplpDevice);
    }
    return hr;
}

HRESULT WINAPI hkDirectInput8Create(HINSTANCE hinst, DWORD dwVersion, REFIID riidltf,
                                    LPVOID* ppvOut, LPUNKNOWN punkOuter)
{
    HRESULT hr = origDirectInput8Create(hinst, dwVersion, riidltf, ppvOut, punkOuter);
    if (SUCCEEDED(hr) && ppvOut != nullptr && *ppvOut != nullptr && origCreateDevice == nullptr)
    {
        char msg[160];
        snprintf(msg, sizeof(msg),
                 "DI::DirectInput8Create -> 0x%p (ver=0x%lX hr=0x%lX)",
                 *ppvOut, (unsigned long)dwVersion, (unsigned long)hr);
        utinni::log::info(msg);

        void** vtbl = *(void***)(*ppvOut);
        origCreateDevice = (pDIO_CreateDevice)vtbl[3];

        DWORD oldProtect = 0;
        if (VirtualProtect(&vtbl[3], sizeof(void*), PAGE_READWRITE, &oldProtect))
        {
            vtbl[3] = (void*)hkCreateDevice;
            VirtualProtect(&vtbl[3], sizeof(void*), oldProtect, &oldProtect);
            utinni::log::info("DI: patched IDirectInput8A::CreateDevice vtbl[3]");
        }
        else
        {
            utinni::log::critical("DI: VirtualProtect(RW) on vtbl[3] FAILED -- device-capture DISABLED");
            origCreateDevice = nullptr;
        }
    }
    return hr;
}
} // anonymous namespace

namespace utinni
{
void DirectInput::suspend()
{
    // WS-0 (advertised-client RVA-safety): swg::directInput::suspend/resume are hardcoded SWGEmu RVAs
    // (0x00420880/0x00420890) NOT in the advertised catalog -> garbage on the advertised DX11 client.
    // imgui_impl calls these on overlay mouse-capture; with the overlay live there, an unguarded call is a
    // likely AV (crew impl review). Degrade to a no-op on the advertised client; SWGEmu unchanged (D-00).
    if (swg::endpoints::isAdvertisedClient())
    {
        return;
    }

    // DIAG 2026-05-19 Issue #9: every call. Pair with Client::suspendInput logs.
    utinni::log::info("DirectInput::suspend: unacquiring keyboard/mouse devices");
    swg::directInput::suspend();
}

void DirectInput::resume()
{
    if (swg::endpoints::isAdvertisedClient())
    {
        return; // advertised: 0x00420890 is an unbound SWGEmu RVA (see suspend())
    }

    utinni::log::info("DirectInput::resume: re-acquiring keyboard/mouse devices");
    swg::directInput::resume();
}

int __cdecl hkSetupInstall(HINSTANCE hInstance, HWND hwnd, DWORD menuKey, DWORD unk)
{
    // 2026-05-19: HWND override removed. The original editor-mode path replaced
    // SWG's HWND with Client::getHwnd() walked up to its top-level ancestor and
    // passed THAT into DirectInput's setupInstall. On the current SWGEmu binary
    // DirectInput rejects it (SetCooperativeLevel returns DIERR_INVALIDPARAM "6"
    // → ExceptionHandler fires) because the editor's top-level HWND is on the
    // CLR thread, not SWG's main thread. SWG now uses its OWN HWND for
    // DirectInput; the managed-side reparent-after-creation step doesn't break
    // DirectInput's binding because reparenting preserves the original HWND.
    //
    // 2026-05-20 Issue #9: cursor side-effects (HCURSOR write + software-cursor
    // mode) REMOVED. They were a holdover from the old "SWG renders into
    // PanelGame" model, where SWG's software cursor needed to render into the
    // editor's framebuffer. In the new model SWG owns its top-level window and
    // the OS hardware cursor works natively -- forcing software-cursor mode
    // left users with no visible cursor on the login screen. Let SWG's
    // default (useHardwareCursor=true) stand.
    static bool s_firstFire = true;
    if (s_firstFire)
    {
        s_firstFire = false;
        utinni::log::info("hkSetupInstall: first fire (passthrough HWND; cursor side-effects dropped)");
    }

    return swg::directInput::setupInstall(hInstance, hwnd, menuKey, unk);
}

void DirectInput::setSuppressExclusiveFullscreen(bool enabled)
{
    // RESID-04 / D-12 live A/B toggle. Both the toggle and the DI pump that
    // reads it are atomic, so flipping from a managed/debug thread is safe.
    s_suppressExclusiveFullscreen.store(enabled, std::memory_order_relaxed);
    utinni::log::info(enabled
                          ? "DirectInput: exclusive-fullscreen suppression ENABLED (D-12, keep windowed-embedded)"
                          : "DirectInput: exclusive-fullscreen suppression DISABLED (D-12, allow SWG's native mode switch)");
}

bool DirectInput::getSuppressExclusiveFullscreen()
{
    return s_suppressExclusiveFullscreen.load(std::memory_order_relaxed);
}

void DirectInput::detour()
{
    // WS-0 (staged Enter-mask kill switch): pick up the [Editor] advertisedEnterMask ini key ONCE (default
    // false). detour() runs in utinni_init AFTER ini.load(), so getConfig() is populated. Default-off keeps
    // the Enter-mask disarmed until a smoke validates it; to arm it for WS-2, set the key in ut.ini + relaunch.
    s_enterMaskEnabled.store(utinni::getConfig().getBool("Editor", "advertisedEnterMask"),
                             std::memory_order_relaxed);

    // WS-0a: split the detour by target. The setupInstall detour targets a HARDCODED SWGEmu RVA
    // (0x00421490) that is NOT in the advertised catalog -> garbage on the advertised DX11 client. Gate it
    // on installable() so it installs ONLY where its target is mapped: on SWGEmu byte-for-byte as before
    // (D-00); on the advertised client it is SKIPPED. The DirectInput8Create import detour below is a
    // dinput8.dll IMPORT (present on both exes, no runtime dependency on setupInstall) -> install it
    // UNCONDITIONALLY so the advertised client gets the keyboard-device vtable capture (the Enter-mask +
    // the SetCooperativeLevel embed shim).
    if (swg::endpoints::installable((const void*)swg::directInput::setupInstall))
    {
        swg::directInput::setupInstall = (swg::directInput::pSetupInstall)Detour::Create((LPVOID)swg::directInput::setupInstall, hkSetupInstall, DETOUR_TYPE_PUSH_RET);
    }
    else
    {
        utinni::log::info("DI: setupInstall detour SKIPPED (advertised client -- unbound SWGEmu RVA)");
    }

    // 2026-05-20 Issue #10 Phase B prereq: install DirectInput8Create detour
    // so we can vtable-patch CreateDevice -> SetCooperativeLevel and log the
    // coop-level flags SWG asks for. Fires once on first DI8 init inside
    // SWG's setupInstall (which is upstream of this detour). See the
    // anonymous namespace above for the full hook chain.
    LPVOID orig = Detour::Create("dinput8.dll", "DirectInput8Create",
                                 (LPVOID)hkDirectInput8Create, DETOUR_TYPE_PUSH_RET);
    if (orig != nullptr)
    {
        origDirectInput8Create = (pDirectInput8Create)orig;
        utinni::log::info("DI: DirectInput8Create detour installed (vtable shim ready)");
    }
    else
    {
        utinni::log::critical("DI: Detour::Create(dinput8.dll, DirectInput8Create) FAILED -- shim DISABLED");
    }
}
} // namespace utinni
