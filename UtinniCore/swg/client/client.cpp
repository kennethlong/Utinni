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

#include "client.h"
#include "swg/graphics/graphics.h"
#include "swg/game/game.h"
#include "swg/misc/direct_input.h"

namespace swg::client
{
using pSetupInstall = int(__cdecl*)(utinni::StartupData* pStartupData);
using pMainLoop = int(__cdecl*)(HINSTANCE hInstance, int a2, int a3);

using pWndProc = int(__stdcall*)(HWND Hwnd, UINT Message, WPARAM wParam, LPARAM lParam);

using pWriteCrashLog = long(__stdcall*)(swgptr unk);
using pWriteMiniDump = bool(__cdecl*)(const char* filename, swgptr unk);

pSetupInstall setupStartDataInstall = (pSetupInstall)0x00A9F970;
pMainLoop clientMain = (pMainLoop)0x00401050;

pWndProc wndProc = (pWndProc)0x00AA0970; // SWG's WndProc

pWriteCrashLog writeCrashLog = (pWriteCrashLog)0x00A9F640;
pWriteMiniDump writeMiniDump = (pWriteMiniDump)0x00A8A170;
}

bool enableEditorMode = false;
HWND hwnd = nullptr;
HINSTANCE hInstance = nullptr;
bool allowInput = false;

static std::string logDir = "logs/";

namespace utinni
{
void Client::setEditorMode(bool enable)
{
    enableEditorMode = enable;
}

bool Client::getEditorMode()
{
    return enableEditorMode;
}

void Client::setHwnd(void* newHwnd)
{
    // DIAG 2026-05-19 Issue #9: trace who's writing the HWND. In the new
    // SWG-owns-its-own-window model, the editor's PanelGame_Layout calls
    // this on every layout with PanelGame.Handle, which shadows SWG's
    // actual top-level HWND. Rate-limit: first-write + any change.
    static HWND lastLogged = (HWND)~0;
    if ((HWND)newHwnd != lastLogged)
    {
        char msg[96];
        snprintf(msg, sizeof(msg), "Client::setHwnd: 0x%p (prev 0x%p)",
                 newHwnd, (void*)lastLogged);
        utinni::log::info(msg);
        lastLogged = (HWND)newHwnd;
    }
    hwnd = (HWND)newHwnd;
}

HWND Client::getHwnd()
{
    return hwnd;
}

void Client::setHInstance(void* newHInstance)
{
    hInstance = (HINSTANCE)newHInstance;
}

HINSTANCE Client::getHInstance()
{
    return hInstance;
}

void Client::suspendInput()
{
    // DIAG 2026-05-19 Issue #9: log every call. If Game::isRunning() is
    // true at login screen, then PanelGame focus-loss / mouse-leave
    // fires DirectInput::suspend(), killing special keys (Tab/Del/Return).
    bool running = Game::isRunning();
    char msg[96];
    snprintf(msg, sizeof(msg), "Client::suspendInput: called (Game::isRunning=%d, getHwnd=0x%p)",
             running ? 1 : 0, (void*)Client::getHwnd());
    utinni::log::info(msg);

    if (running)
    {
        SetFocus(nullptr);
        Graphics::showMouseCursor(false);
        DirectInput::suspend();
        allowInput = false;
    }
}

void Client::resumeInput()
{
    bool running = Game::isRunning();
    char msg[96];
    snprintf(msg, sizeof(msg), "Client::resumeInput: called (Game::isRunning=%d, getHwnd=0x%p)",
             running ? 1 : 0, (void*)Client::getHwnd());
    utinni::log::info(msg);

    if (running)
    {
        SetFocus(Client::getHwnd());
        Graphics::showMouseCursor(true);
        DirectInput::resume();
        allowInput = true;
    }
}

bool Client::isInputAllowed()
{
    return allowInput;
}

int __cdecl hkSetupStartInstall(StartupData* pStartupData)
{
    // 2026-05-19: editor-mode pStartupData modifications REMOVED.
    //
    // The original code set createOwnWindow=false + useNewWindowHandle=true +
    // windowHandle=Client::getHwnd() to make SWG render directly into the
    // editor host's HWND. Bisection (rounds 0-10, see git log) proved that
    // SWG's setupStartDataInstall on the current SWGEmu binary rejects
    // createOwnWindow=false and hangs at audio init when it's applied.
    //
    // New integration model: SWG creates its own window normally, then the
    // managed side (FormMain) reparents that HWND into the editor's
    // PanelGame via Win32 SetParent + WS_CHILD style adjustment. That
    // moves the embedded-window plumbing entirely out of this hook.
    //
    // Hook is kept installed (not removed from Client::detour) so we have
    // a known fire point for future per-startup-data behavior changes.
    // First-fire log preserved so we can confirm hook firing in the log.
    static bool s_firstFire = true;
    if (s_firstFire)
    {
        s_firstFire = false;
        utinni::log::info("hkSetupStartInstall: first fire (passthrough; reparent-after-creation model)");
    }

    return swg::client::setupStartDataInstall(pStartupData);
}

LRESULT CALLBACK hkWndProc(HWND Hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    if (Client::getEditorMode())
    {
    }

    return CallWindowProc((WNDPROC)swg::client::wndProc, Hwnd, msg, wParam, lParam);
}

int __cdecl hkMainLoop(HINSTANCE hInstance, int unk1, int unk2)
{
    if (Client::getEditorMode())
    {
        while (Client::getHInstance() == nullptr)
        {
            Sleep(1); // Wait until the Editor has set the HWND and HInstance the game should use
             // ToDo add a timeout
        }
        return swg::client::clientMain(GetModuleHandle(nullptr), unk1, unk2);
    }
    else
    {
        return swg::client::clientMain(hInstance, unk1, unk2);
    }
}


long __stdcall hWriteCrashLog(swgptr unk)
{
    CreateDirectory((utility::getWorkingDirectory() + "/" + logDir).c_str(), nullptr);
    return swg::client::writeCrashLog(unk);
}

bool __cdecl hWriteMiniDump(const char* filename, swgptr unk)
{
    logDir += filename;
    return swg::client::writeMiniDump(logDir.c_str(), unk);
}

std::string fn_MidCrashLogWrite;
swgptr fnInput_MidCrashLogWrite;
swgptr fnModified_MidCrashLogWrite;
static constexpr swgptr start_MidCrashLogWrite = 0x00A9F766;
static constexpr swgptr return_MidCrashLogWrite = 0x00A9F76B;
__declspec(naked) void midCrashLogWrite()
{
    __asm
    {
        mov fnInput_MidCrashLogWrite, 0x0193C268
        pushad
        pushfd
    }

    fn_MidCrashLogWrite = logDir;
    fn_MidCrashLogWrite += (const char*)fnInput_MidCrashLogWrite;
    fnModified_MidCrashLogWrite = (swgptr)(fn_MidCrashLogWrite).c_str();

    __asm
    {
        popfd
        popad
        push fnModified_MidCrashLogWrite
        jmp[return_MidCrashLogWrite]
    }
}

void Client::detour()
{
    swg::client::setupStartDataInstall = (swg::client::pSetupInstall)Detour::Create((LPVOID)swg::client::setupStartDataInstall, hkSetupStartInstall, DETOUR_TYPE_PUSH_RET);
    swg::client::clientMain = (swg::client::pMainLoop)Detour::Create((LPVOID)swg::client::clientMain, hkMainLoop, DETOUR_TYPE_PUSH_RET);
    //swg::client::wndProc = (swg::client::pWndProc)Detour::Create((LPVOID)swg::client::wndProc, hkWndProc, DETOUR_TYPE_PUSH_RET);

    DirectInput::detour();

    // Move crash log location to logs/
    swg::client::writeCrashLog = (swg::client::pWriteCrashLog)Detour::Create((LPVOID)swg::client::writeCrashLog, hWriteCrashLog, DETOUR_TYPE_PUSH_RET);
    swg::client::writeMiniDump = (swg::client::pWriteMiniDump)Detour::Create((LPVOID)swg::client::writeMiniDump, hWriteMiniDump, DETOUR_TYPE_PUSH_RET);
    memory::createJMP(start_MidCrashLogWrite, (swgptr)midCrashLogWrite, 5);
}

}
