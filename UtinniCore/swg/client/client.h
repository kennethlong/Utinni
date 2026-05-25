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

#include "utinni.h"

namespace utinni
{
struct StartupData
{
    bool createOwnWindow;   // 0x0000
    const char* windowName; // 0x0004
    HICON normalIcon;       // 0x0008
    HICON smallIcon;        // 0x000C
    HINSTANCE hInstance;    // 0x0010

    bool useNewWindowHandle; // 0x0014
    bool processMessagePump; // 0x0015
    HWND windowHandle;       // 0x0018

    bool writeMiniDumps; // 0x001C
    bool unk1;           // 0x001D
    bool unk2;           // 0x001E

    const char* commandLine; // 0x0020
    int argc;                // 0x0024
    char** argv;             // 0x0028
    const char* configFile;  // 0x002C
    const char* unk3;        // 0x0030

    float frameRateLimit; // 0x0034

    bool unk4; // 0x0038
    bool unk5;
    swgptr lostFocusCallback;
};

class UTINNI_API Client
{
public:
    static void setEditorMode(bool enable);
    static bool getEditorMode();

    static void setHwnd(void* newHwnd);
    static HWND getHwnd();
    static void setHInstance(void* newHInstance);
    static HINSTANCE getHInstance();

    // 2026-05-20 Issue #10: SWG's actual top-level HWND, captured from
    // D3D9's cParam.hFocusWindow at first BeginScene. Kept separate from
    // setHwnd/getHwnd (which was the old editor-mode override path now
    // intentionally unused -- repurposing it would reactivate
    // hkMainLoop/hkEndScene editor-mode branches with SWG's own HWND,
    // which may not behave correctly). PanelGame uses this to find
    // SWG's window for SetParent + WS_CHILD reparenting.
    static void setSwgHwnd(void* hwnd);
    static HWND getSwgHwnd();

    // Phase 3 R-C (per 03-CONTEXT D-18..D-20 / TD-18): single-source the
    // SWG WndProc RVA (0x00AA0970). Previously duplicated as the literal
    // `new IntPtr(0x00AA0970)` in UtinniCoreDotNet/UI/Controls/PanelGame.cs
    // (drifts independently from the native-side declaration at
    // swg/client/client.cpp:43). Surfacing the value once via this getter
    // makes the native declaration the single source of truth; the
    // C-linkage shim getSwgWndProcExport (in client.cpp outside the
    // utinni namespace) is what PanelGame.cs's P/Invoke binds to,
    // mirroring the getSwgHwndExport precedent because CppSharp drops
    // pointer-returning getters from the generated bindings.
    static void* getSwgWndProc();

    static void setSize(int width, int height);
    static int getWidth();
    static int getHeight();

    static void suspendInput();
    static void resumeInput();
    static bool isInputAllowed();

    static void detour();
};

} // namespace utinni
