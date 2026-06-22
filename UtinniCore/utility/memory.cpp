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

#include "memory.h"
#include "log.h"
#include <TlHelp32.h>
#include <Psapi.h>
#include <cstdio>

namespace memory
{

// Utinni guard (Phase 24): the raw patch helpers below VirtualProtect a target
// address and then write, ignoring VirtualProtect failure. On the ASLR-relocated
// advertised client a hardcoded SWGEmu literal that was NOT resolved from the
// GetEngineHookPoints catalog is unmapped, so the write faults (0xC0000005 WRITE).
// This validates the target page is committed (and covers the write) before the
// helper proceeds; if not, the patch is skipped. On SWGEmu every target is a valid
// mapped address, so nothing is skipped (byte-for-byte unchanged). The inline
// write<T>/read<T> templates in memory.h are intentionally NOT guarded -- they run
// on already-resolved/valid pointers on hot paths.
static bool isTargetCommitted(swgptr address, size_t length)
{
    if (address == 0)
        return false;

    MEMORY_BASIC_INFORMATION mbi;
    if (VirtualQuery((LPCVOID)address, &mbi, sizeof(mbi)) == 0)
        return false;
    if (mbi.State != MEM_COMMIT)
        return false;
    if (mbi.Protect & (PAGE_GUARD | PAGE_NOACCESS))
        return false;

    const BYTE* regionEnd = (const BYTE*)mbi.BaseAddress + mbi.RegionSize;
    if ((const BYTE*)address + length > regionEnd)
        return false;

    return true;
}

static void logSkippedWrite(const char* fn, swgptr address)
{
    char dbg[128];
    _snprintf_s(dbg, sizeof(dbg), _TRUNCATE,
                "%s skipped unmapped target 0x%08X (endpoint unresolved on advertised client)",
                fn, (DWORD)address);
    utinni::log::warning(dbg);
}

swgptr findPattern(swgptr startAddress, size_t length, const char* pattern, const char* mask)
{
    swgptr pos = 0;
    const size_t searchLength = strlen(mask) - 1;
    for (swgptr returnAddress = startAddress; returnAddress < startAddress + length; returnAddress++)
    {
        if (*(PBYTE)returnAddress == ((PBYTE)pattern)[pos] || mask[pos] == '?')
        {
            if (mask[pos + 1] == '\0')
            {
                return returnAddress - searchLength;
            }
            pos++;
        }
        else
        {
            pos = 0;
        }
    }
    return 0;
}

swgptr findPattern(const char* moduleName, const char* pattern, const char* mask)
{
    HINSTANCE moduleHandle = GetModuleHandle(moduleName);
    MODULEINFO moduleInfo;
    GetModuleInformation(GetCurrentProcess(), moduleHandle, &moduleInfo, sizeof(MODULEINFO));

    return findPattern((swgptr)moduleHandle, moduleInfo.SizeOfImage, pattern, mask);
}

void copy(swgptr pDest, swgptr pSource, size_t length)
{
    if (length != 0 && !isTargetCommitted(pDest, length))
    {
        logSkippedWrite("memory::copy", pDest);
        return;
    }

    DWORD oldProtect;
    DWORD newProtect;

    const LPVOID addr = (LPVOID)pDest;
    VirtualProtect(addr, length, PAGE_EXECUTE_READWRITE, &oldProtect);
    memcpy(addr, (LPVOID)pSource, length);
    VirtualProtect(addr, length, oldProtect, &newProtect);
}

void write(swgptr address, swgptr value, int length)
{
    copy(address, value, length);
}

void set(swgptr pDest, swgptr value, size_t length)
{
    if (length != 0 && !isTargetCommitted(pDest, length))
    {
        logSkippedWrite("memory::set", pDest);
        return;
    }

    DWORD oldProtect;
    DWORD newProtect;

    const LPVOID addr = (LPVOID)pDest;
    VirtualProtect(addr, length, PAGE_EXECUTE_READWRITE, &oldProtect);
    memset(addr, value, length);
    VirtualProtect(addr, length, oldProtect, &newProtect);
}

void patchAddress(swgptr address, swgptr value)
{
    if (!isTargetCommitted(address, 4))
    {
        logSkippedWrite("memory::patchAddress", address);
        return;
    }

    DWORD oldProtect;
    DWORD newProtect;

    VirtualProtect((LPVOID)address, 4, PAGE_EXECUTE_READWRITE, &oldProtect);
    write<swgptr>(address, value);
    VirtualProtect((LPVOID)address, 4, oldProtect, &newProtect);
}

std::tuple<swgptr, std::vector<char>> nopAddress(swgptr address, int nopCount)
{
    if (nopCount > 0 && !isTargetCommitted(address, (size_t)nopCount))
    {
        logSkippedWrite("memory::nopAddress", address);
        return std::tuple<swgptr, std::vector<char>>(address, std::vector<char>());
    }

    // Not the most efficient. ToDo improve in the future
    std::vector<char> originalBytes((const char*)address, (const char*)address + nopCount);
    set(address, 0x90, nopCount); // 0x90 = NOP
    return std::tuple<swgptr, std::vector<char>>(address, originalBytes);
}

void restoreBytes(const std::tuple<swgptr, std::vector<char>>& originalBytes)
{
    copy(std::get<0>(originalBytes), (swgptr)std::get<1>(originalBytes).data(), std::get<1>(originalBytes).size());
}

void createJMP(swgptr address, swgptr jumpToAddress, size_t overrideLength)
{
    if (!isTargetCommitted(address, overrideLength))
    {
        logSkippedWrite("memory::createJMP", address);
        return;
    }

    DWORD oldProtect;
    DWORD newProtect;

    VirtualProtect((LPVOID)address, overrideLength, PAGE_EXECUTE_READWRITE, &oldProtect);
    write<char>(address, 0xE9); // 0xE9 = JMP
    write<swgptr>(address + 0x1, (jumpToAddress - address) - 5);
    for (swgptr i = 0x5; i < overrideLength; i++)
    {
        write<char>(address + i, 0x90); // 0x90 = NOP
    }
    VirtualProtect((LPVOID)address, overrideLength, oldProtect, &newProtect);
}

swgptr getAddress(swgptr baseAddress, int ptrDepth)
{
    swgptr address = read<swgptr>(baseAddress);
    for (int i = 0; i < ptrDepth; ++i)
    {
        address = read<swgptr>(address);
    }
    return address;
}

swgptr getAddress(swgptr baseAddress, std::vector<int>& offsets)
{
    swgptr address = read<swgptr>(baseAddress);
    for (swgptr offset : offsets)
    {
        address = read<swgptr>(address + offset);
    }
    return address;
}

} // namespace memory
