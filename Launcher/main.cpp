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

#include <Windows.h>
#include <string>
#include <TlHelp32.h>
#include <Shlwapi.h>
#include <stdexcept>
#include <filesystem>

#include "UtINI/utini.h"

#pragma comment(linker, "/SUBSYSTEM:windows /ENTRY:mainCRTStartup") // Disables the popping up of the console
#pragma comment(lib, "version.lib")                                 // To use GetFileVersionInfo, etc

void throwError(const std::string& message)
{
    MessageBoxA(nullptr, message.c_str(), "Error", MB_OK);
    throw std::runtime_error(message);
}

void inject(PROCESS_INFORMATION procInfo, LPVOID utinniInitArg)
{
    char curDirBuffer[MAX_PATH];
    GetModuleFileName(nullptr, curDirBuffer, MAX_PATH);
    std::string currentDir = std::string(curDirBuffer);
    currentDir = currentDir.substr(0, currentDir.find_last_of('\\') + 1);

    std::string dllFilename = currentDir + "UtinniCore.dll";
    LPVOID lpMemory = (LPVOID)VirtualAllocEx(procInfo.hProcess, nullptr, dllFilename.length(), MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
    WriteProcessMemory(procInfo.hProcess, lpMemory, (LPVOID)dllFilename.c_str(), dllFilename.length(), nullptr);

    LPVOID lpLoadLibraryA = (LPVOID)GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
    HANDLE hThread = CreateRemoteThread(procInfo.hProcess, nullptr, 0, (LPTHREAD_START_ROUTINE)lpLoadLibraryA, lpMemory, 0, nullptr);

    WaitForInputIdle(procInfo.hProcess, 5000);

    if (!hThread)
    {
        throwError("[ERROR] Couldn't open LoadLibraryA thread. Dll not injected.");
    }

    if (WaitForSingleObject(hThread, INFINITE))
    {
        throwError("[ERROR] Thread didn't return 0. Dll not injected.");
    }

    DWORD hDll;
    if (!GetExitCodeThread(hThread, &hDll))
    {
        throwError("[ERROR] Can't get LoadLibraryA return handle.");
    }

    if (hDll == 0x00000000)
    {
        throwError("[ERROR] LoadLibraryA couldn't inject dll.");
    }

    // C-01: After LoadLibraryA returns, fire utinni_init on a fresh remote thread.
    // The local LoadLibrary is used only to resolve the export offset via GetProcAddress;
    // it is freed immediately after. The remote address is computed via the standard
    // DLL-injection idiom: local GetProcAddress offset + remote hDll base.
    HMODULE localCore = LoadLibraryA(dllFilename.c_str());
    if (!localCore)
    {
        throwError("[ERROR] Local LoadLibraryA(UtinniCore.dll) failed for utinni_init resolution.");
    }
    FARPROC localInit = GetProcAddress(localCore, "utinni_init");
    if (!localInit)
    {
        FreeLibrary(localCore);
        throwError("[ERROR] utinni_init export not found in UtinniCore.dll.");
    }
    SIZE_T initOffset = (BYTE*)localInit - (BYTE*)localCore;
    FreeLibrary(localCore);

    FARPROC remoteInit = (FARPROC)((BYTE*)hDll + initOffset);
    // 2026-05-19: pass the ready-event name (in REMOTE process memory) as
    // lpThreadParam. utinni_init captures it, then the managed EntryPoint signals
    // the event right before Application.Run blocks (see UtinniCoreDotNet/main.cs).
    // We no longer WaitForSingleObject(hInitThread, INFINITE) -- that would block
    // for the entire editor session because Application.Run never returns until
    // the form closes, which means the OEP-restore at loadDll() lines 382-384
    // never ran. The wait now happens on the named event in loadDll().
    HANDLE hInitThread = CreateRemoteThread(procInfo.hProcess, nullptr, 0,
                                            (LPTHREAD_START_ROUTINE)remoteInit, utinniInitArg, 0, nullptr);
    if (!hInitThread)
    {
        throwError("[ERROR] Couldn't open utinni_init remote thread.");
    }
    // Intentionally do NOT wait on hInitThread here. Caller waits on the named
    // ready event instead. The thread stays alive running Application.Run for
    // the editor's lifetime; the OS reclaims the handle on process exit.
    CloseHandle(hInitThread);
}

std::string swgClientPath;
std::string getSwgClientFilename()
{
    char curDirBuffer[MAX_PATH];
    HMODULE handle = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCSTR)&inject, &handle);
    GetModuleFileNameA(handle, curDirBuffer, sizeof(curDirBuffer));
    const std::string path = std::string(curDirBuffer);
    std::string iniFilename = path.substr(0, path.find_last_of("\\/")) + "\\ut.ini";

    utinni::UtINI ini(iniFilename);
    ini.createUtinniSettings();
    ini.load();

    swgClientPath = ini.getString("Launcher", "swgClientPath");
    std::string swgClientName = ini.getString("Launcher", "swgClientName");

    if (swgClientPath.empty() || swgClientName.empty() || !swgClientName.find(".exe") || !std::filesystem::exists(swgClientPath + swgClientName))
    {
        char filenameBuffer[MAX_PATH];

        OPENFILENAME ofn;
        ZeroMemory(&filenameBuffer, sizeof(filenameBuffer));
        ZeroMemory(&ofn, sizeof(ofn));
        ofn.lStructSize = sizeof(ofn);
        ofn.hwndOwner = nullptr;
        ofn.lpstrFilter = "Executable Files\0*.exe\0";
        ofn.lpstrFile = filenameBuffer;
        ofn.nMaxFile = MAX_PATH;
        ofn.lpstrTitle = "Please select your SWG Client";
        ofn.Flags = OFN_DONTADDTORECENT | OFN_FILEMUSTEXIST;

        if (GetOpenFileNameA(&ofn))
        {
            const std::string filename = std::string(filenameBuffer);
            const int lastSlashPos = filename.find_last_of('\\');
            swgClientPath = filename.substr(0, lastSlashPos + 1);
            swgClientName = filename.substr(lastSlashPos + 1);

            ini.setString("Launcher", "swgClientPath", swgClientPath.c_str());
            ini.setString("Launcher", "swgClientName", swgClientName.c_str());

            ini.save();
        }
        else
        {
            throwError("[ERROR] Can't open the selected file.");
        }
    }

    std::string result = swgClientPath + swgClientName;

    if (result.size() > 4 && result.compare(result.size() - 3, 3, "exe") != 0)
    {
        throwError("[ERROR] Target SWG client is not an executable.");
    }

    DWORD targetHandle = 0;
    const DWORD targetSize = GetFileVersionInfoSize(result.c_str(), &targetHandle);
    if (targetSize <= 0)
    {
        throwError("[ERROR] Target SWG client size is invalid.");
    }

    uint32_t verInfoSize;
    const char* targetProductName;
    BYTE* targetVersionInfo = new BYTE[targetSize];
    if (GetFileVersionInfo(result.c_str(), targetHandle, targetSize, targetVersionInfo))
    {
        if (VerQueryValue(targetVersionInfo, TEXT("\\StringFileInfo\\040904B0\\ProductName"), (LPVOID*)&targetProductName, &verInfoSize) && strcmp(targetProductName, "Star Wars Galaxies") != 0)
        {
            delete[] targetVersionInfo;

            ini.setString("Launcher", "swgClientPath", "");
            ini.setString("Launcher", "swgClientName", "");

            ini.save();

            throwError("[ERROR] Target client is not a valid SWG client.");
        }
        delete[] targetVersionInfo;
    }

    return result;
}

void loadDll(const std::string& cmdLine)
{
    STARTUPINFOA StartupInfo = {0};
    StartupInfo.cb = sizeof(StartupInfo);
    PROCESS_INFORMATION procInfo;

    const std::string swgClientFilename = getSwgClientFilename();
    if (CreateProcess(swgClientFilename.c_str(), const_cast<char*>(cmdLine.c_str()), nullptr, nullptr, false, CREATE_SUSPENDED, nullptr, swgClientPath.c_str(), &StartupInfo, &procInfo))
    {
        const HANDLE hProcess(procInfo.hProcess);

        try
        {
            // Locate the entry point
            HANDLE hFile = CreateFileA(swgClientFilename.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
            LPVOID hFileMapping = CreateFileMapping(hFile, nullptr, PAGE_READONLY, 0, 0, nullptr);
            PIMAGE_DOS_HEADER dosHeader = (PIMAGE_DOS_HEADER)MapViewOfFile(hFileMapping, FILE_MAP_READ, 0, 0, 0);
            PIMAGE_NT_HEADERS peHeader = (PIMAGE_NT_HEADERS)((u_char*)dosHeader + dosHeader->e_lfanew);
            const DWORD entryRva = peHeader->OptionalHeader.AddressOfEntryPoint;

            // Resolve the ACTUAL load base of the suspended target. OptionalHeader.ImageBase is
            // only the *preferred* base; an ASLR (/DYNAMICBASE) image such as the advertised
            // SwgClient_r.exe (DllCharacteristics 0x8140) is relocated by the loader, so adding
            // AddressOfEntryPoint to the preferred base points at the wrong VA -- the EB FE patch
            // lands on stale memory, the real entry runs unpatched, and the spin-wait below times
            // out ("Timed out trying to reach the entry point") even though the SWG window opens.
            // On x86 the initial thread of a CREATE_SUSPENDED process has EBX = PEB; the actual
            // image base lives at PEB+0x08 (PEB.ImageBaseAddress). Fixed-base SWGEmu.exe
            // (DllCharacteristics 0x0000) resolves to its usual 0x00400000 -- no behaviour change.
            DWORD_PTR actualBase = peHeader->OptionalHeader.ImageBase;
            {
                CONTEXT startCtx{};
                startCtx.ContextFlags = CONTEXT_INTEGER;
                if (GetThreadContext(procInfo.hThread, &startCtx))
                {
                    DWORD_PTR remoteImageBase = 0;
                    if (ReadProcessMemory(hProcess, (LPCVOID)(startCtx.Ebx + 0x08),
                                          &remoteImageBase, sizeof(remoteImageBase), nullptr)
                        && remoteImageBase != 0)
                    {
                        actualBase = remoteImageBase;
                    }
                }
            }
            LPVOID entry = (LPVOID)(actualBase + entryRva);

            // DIAG: surface preferred-vs-actual base so an ASLR relocation is visible in the log.
            {
                char dbg[176];
                _snprintf_s(dbg, sizeof(dbg), _TRUNCATE,
                            "[LAUNCHER] image base preferred=0x%08X actual=0x%08X entryRVA=0x%08X entry=0x%08X\n",
                            (DWORD)peHeader->OptionalHeader.ImageBase, (DWORD)actualBase,
                            (DWORD)entryRva, (DWORD)entry);
                OutputDebugStringA(dbg);
            }

            // store original entry point
            unsigned char oep[2];
            ReadProcessMemory(hProcess, entry, oep, 2, nullptr);

            // 2026-05-19: named ready event for signal-based sync with utinni_init.
            // Created BEFORE the EB FE write so we own the event when the remote
            // thread spins up. utinni_init captures the name from lpThreadParam and
            // the managed-side EntryPoint signals it just before Application.Run.
            std::string readyEventName = "Local\\UtinniReady_" + std::to_string(procInfo.dwProcessId);
            HANDLE hReady = CreateEventA(nullptr, TRUE /* manual reset */, FALSE /* initial state */, readyEventName.c_str());
            if (hReady == nullptr)
            {
                throwError("[ERROR] CreateEventA failed for utinni_ready event.");
            }

            // Allocate remote memory and write the event-name C string for utinni_init.
            // +1 for the NUL terminator.
            const SIZE_T eventNameLen = readyEventName.size() + 1;
            LPVOID remoteEventName = VirtualAllocEx(procInfo.hProcess, nullptr, eventNameLen, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (remoteEventName == nullptr)
            {
                CloseHandle(hReady);
                throwError("[ERROR] VirtualAllocEx for event-name string failed.");
            }
            WriteProcessMemory(procInfo.hProcess, remoteEventName, readyEventName.c_str(), eventNameLen, nullptr);

            // patch original entry point with ian infinite loop
            unsigned char patchedOep[]{0xEB, 0xFE};
            WriteProcessMemory(hProcess, entry, patchedOep, 2, nullptr);
            // CRITICAL: flush instruction cache so the main thread fetches the patched
            // bytes rather than a stale prefetched original. Without this, the CPU's
            // I-cache may execute the pre-patch bytes for some duration after
            // ResumeThread, producing nondeterministic "sometimes halts at entry,
            // sometimes runs through" behaviour. Diagnosed 2026-05-19.
            FlushInstructionCache(hProcess, entry, 2);

            // DIAG: read back to confirm what the patch left in memory.
            {
                unsigned char afterPatch[2] = {0, 0};
                ReadProcessMemory(hProcess, entry, afterPatch, 2, nullptr);
                char dbg[128];
                _snprintf_s(dbg, sizeof(dbg), _TRUNCATE,
                            "[LAUNCHER] post-patch bytes at 0x%08X: %02X %02X (expected EB FE)\n",
                            (DWORD)entry, afterPatch[0], afterPatch[1]);
                OutputDebugStringA(dbg);
            }

            // Do patching here

            // Resume the main thread
            ResumeThread(procInfo.hThread);
            CONTEXT context{};

            // Wait until the main thread is stuck at the entry point
            for (unsigned int i = 0; i < 50 && context.Eip != (DWORD)entry; ++i)
            {
                Sleep(100);
                context.ContextFlags = CONTEXT_CONTROL;
                GetThreadContext(procInfo.hThread, &context);
            }
            if (context.Eip != (DWORD)entry)
            {
                // Wait timed out
                throwError("Timed out trying to reach the entry point");
            }

            // inject DLL -- fires LoadLibraryA + utinni_init remote threads. We pass
            // remoteEventName as utinni_init's lpThreadParam so utinni_init can later
            // open the named event and have the managed side signal us.
            inject(procInfo, remoteEventName);

            // 2026-05-19: wait on the named event (signaled from managed EntryPoint
            // right before Application.Run) instead of the utinni_init thread handle.
            // The thread never exits during a normal editor session because
            // Application.Run blocks it for the form's lifetime, so the old
            // WaitForSingleObject(hInitThread, INFINITE) deadlocked the restore.
            // 30s timeout covers first-run CLR/JIT/AV-scan worst cases.
            DWORD waitResult = WaitForSingleObject(hReady, 30000);
            CloseHandle(hReady);

            if (waitResult != WAIT_OBJECT_0)
            {
                throwError("[ERROR] Timed out waiting for utinni ready signal (30s).");
            }

            SuspendThread(procInfo.hThread);
            WriteProcessMemory(hProcess, entry, oep, 2, nullptr); // Restore original entry point
            // Same flush rationale as the patch write: ensure the main thread
            // executes the restored bytes, not a cached EB FE.
            FlushInstructionCache(hProcess, entry, 2);

            // DIAG: confirm the restore landed.
            {
                unsigned char afterRestore[2] = {0, 0};
                ReadProcessMemory(hProcess, entry, afterRestore, 2, nullptr);
                char dbg[128];
                _snprintf_s(dbg, sizeof(dbg), _TRUNCATE,
                            "[LAUNCHER] post-restore bytes at 0x%08X: %02X %02X (expected %02X %02X)\n",
                            (DWORD)entry, afterRestore[0], afterRestore[1], oep[0], oep[1]);
                OutputDebugStringA(dbg);
            }

            ResumeThread(procInfo.hThread);

            // The remote-event-name allocation in SWG's process stays alive for
            // utinni_init's lifetime (it holds a pointer to it). The OS reclaims
            // it on SWG process exit; intentional leak.
        }
        catch (...)
        {
            TerminateProcess(hProcess, -1);
            throw;
        }
    }
    else
    {
        throwError("Unable to load the specified executable");
    }
}

LRESULT WINAPI WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProc(hWnd, msg, wParam, lParam);
}

int main(int argc, char* argv[])
{
    std::string argsCombined;

    for (int i = 1; i < argc; ++i)
    {
        // Syntax for passing swg configs is as follows: '--' marks the start of args, -s marks the start of a section followed by variables separated by whitespace
        // Inside the .cfg, sections are marked by being wrapped in brackets, ie [ClientGame]
        // Example: -- -s SectionName1 variable1=true variable2=false -s SectionName2 variable1=true variable2=false
        // Real world example: -- -s ClientGame loginClientID=Local groundScene=terrain/lok.trn
        argsCombined.append(" " + std::string(argv[i]));
    }

    loadDll(argsCombined);
    return 0;
}
