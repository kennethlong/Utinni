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
#include "swg/graphics/graphics.h"

#include <dinput.h>
#include <intrin.h>

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
    return origSetCooperativeLevel(pThis, hwnd, dwFlags);
}

void patchDeviceVtableOnce(IDirectInputDevice8A* device)
{
    if (device == nullptr)
        return;
    if (origSetCooperativeLevel != nullptr)
        return; // already patched

    void** vtbl = *(void***)device;
    origSetCooperativeLevel = (pDID_SetCooperativeLevel)vtbl[13];

    DWORD oldProtect = 0;
    if (!VirtualProtect(&vtbl[13], sizeof(void*), PAGE_READWRITE, &oldProtect))
    {
        utinni::log::critical("DI: VirtualProtect(RW) on vtbl[13] FAILED -- shim DISABLED");
        origSetCooperativeLevel = nullptr;
        return;
    }
    vtbl[13] = (void*)hkSetCooperativeLevel;
    VirtualProtect(&vtbl[13], sizeof(void*), oldProtect, &oldProtect);

    utinni::log::info("DI: patched IDirectInputDevice8A::SetCooperativeLevel vtbl[13]");
}

HRESULT __stdcall hkCreateDevice(IDirectInput8A* pThis, REFGUID rguid,
                                 IDirectInputDevice8A** lplpDevice, LPUNKNOWN pUnkOuter)
{
    HRESULT hr = origCreateDevice(pThis, rguid, lplpDevice, pUnkOuter);
    if (SUCCEEDED(hr) && lplpDevice != nullptr && *lplpDevice != nullptr)
    {
        const char* devType = "?";
        if (IsEqualGUID(rguid, GUID_SysKeyboard))
            devType = "keyboard";
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
    // DIAG 2026-05-19 Issue #9: every call. Pair with Client::suspendInput logs.
    utinni::log::info("DirectInput::suspend: unacquiring keyboard/mouse devices");
    swg::directInput::suspend();
}

void DirectInput::resume()
{
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

void DirectInput::detour()
{
    swg::directInput::setupInstall = (swg::directInput::pSetupInstall)Detour::Create((LPVOID)swg::directInput::setupInstall, hkSetupInstall, DETOUR_TYPE_PUSH_RET);

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
