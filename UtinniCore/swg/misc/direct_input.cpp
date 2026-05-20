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

namespace swg::directInput
{
using pSuspend = void(__cdecl*)();
using pResume = void(__cdecl*)();
using pSetupInstall = int(__cdecl*)(HINSTANCE hInstance, HWND hwnd, DWORD menuKey, DWORD unk);

pSuspend suspend = (pSuspend)0x00420880;
pSuspend resume = (pSuspend)0x00420890;

pSetupInstall setupInstall = (pSetupInstall)0x00421490;
}

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
    // Cursor side-effects (write SWG's HCURSOR global + disable hardware cursor)
    // kept inside the editor-mode block — they're independent of the HWND choice.
    static bool s_firstFire = true;
    if (s_firstFire)
    {
        s_firstFire = false;
        utinni::log::info("hkSetupInstall: first fire (passthrough HWND; cursor side-effects retained)");
    }

    if (Client::getEditorMode())
    {
        // Create the main cursor and write its pointer to the global SWG Cursor address
        memory::write<HCURSOR>(0x0193C5E0, LoadCursor(nullptr, IDC_ARROW)); // SWG's HCURSOR address
        Graphics::useHardwareCursor(false); // Turning this to false makes the game render its own cursor
    }

    return swg::directInput::setupInstall(hInstance, hwnd, menuKey, unk);
}

void DirectInput::detour()
{
    swg::directInput::setupInstall = (swg::directInput::pSetupInstall)Detour::Create((LPVOID)swg::directInput::setupInstall, hkSetupInstall, DETOUR_TYPE_PUSH_RET);
}
}
