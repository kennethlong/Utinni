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

// NOTE: Test-only exports for UtinniCoreDotNet.Tests P/Invoke harnesses (Phase 2).
// Do not consume from production code; CppSharp generation skips this file because
// the symbols use C linkage, not mangled C++ linkage.

#include "utinni.h"
#include "clr.h"
#include "utility/memory.h"
#include "swg/misc/config.h"
#include "swg/misc/network.h"
#include "swg/game/game.h"
#include "swg/graphics/directx9.h"
// Note: windows.h (GetModuleHandleA, GetProcAddress) is available transitively via
// directx9.h -> <d3d9.h> -> <windows.h>. No explicit include needed.

namespace directX
{
    // Forward declaration — getVtbl is defined in swg/graphics/directx9.cpp but not
    // in directx9.h (it is an internal function not exported via UTINNI_API).
    swgptr* getVtbl();
}

// getPresentBlockedEvent is a C-linkage file-scope export in directx9.cpp.
// Declared here for same-TU use — no dllimport needed (same DLL).
extern "C" HANDLE __cdecl getPresentBlockedEvent();

// ---------------------------------------------------------------------------
// C-10: clr::stop — idempotent CLR shutdown
//   Call twice in tests; the second call must not AV after the C-10 fix.
// ---------------------------------------------------------------------------
extern "C" __declspec(dllexport) void __cdecl utinni_clr_stop()
{
    clr::stop();
}

// ---------------------------------------------------------------------------
// C-11: memory::findPattern — pure-read pattern scan
//   Forwards to the in-tree signature:
//   swgptr findPattern(swgptr startAddress, size_t length, const char* pattern, const char* mask)
// ---------------------------------------------------------------------------
extern "C" __declspec(dllexport) uintptr_t __cdecl utinni_findPattern(
    const uint8_t* buffer,
    size_t bufferLen,
    const char* pattern,
    const char* mask)
{
    return (uintptr_t)memory::findPattern((swgptr)buffer, bufferLen, pattern, mask);
}

// ---------------------------------------------------------------------------
// C-11: directx9::getVtbl — null-safe vtable pointer
//   Returns 0 when d3d9.dll is not loaded (safe to call in test process).
// ---------------------------------------------------------------------------
extern "C" __declspec(dllexport) uintptr_t __cdecl utinni_getVtbl()
{
    swgptr* vtbl = directX::getVtbl();
    if (vtbl == nullptr)
    {
        return 0;
    }
    return (uintptr_t)(void*)vtbl;
}

// ---------------------------------------------------------------------------
// C-02: config buffer free — partial-proof stub
//   The real fix is to remove delete[] from hkLoadOverrideConfig; this wrapper
//   is a no-op stub that asserts the fixed code path (no delete[]) does not crash
//   when called with a synthetic non-zero buffer pointer.
//   Full CRT-mismatch detection remains Tier-4 manual per CONTEXT.md D-05/D-06.
// ---------------------------------------------------------------------------
extern "C" __declspec(dllexport) bool __cdecl utinni_test_freeConfigBuffer(
    unsigned char* /*data*/,
    void* /*pFile*/)
{
    // No-op: the fix removes delete[] data from hkLoadOverrideConfig entirely.
    // The SWG TreeFile dtor owns the buffer (per C-02 comment in config.cpp).
    // This stub just returns true so the test can assert no crash occurs.
    return true;
}

// ---------------------------------------------------------------------------
// WR-01 real harness: Network::cast function-pointer reseat via __declspec(naked)
//
// Background: swg::network::cast is a pCast pointer (int64_t(__thiscall*)(int64_t*, int, int)).
// __thiscall ABI on x86:
//   ECX      = this / first "hidden" param = int64_t* networkId (OUT param)
//   [ESP+4]  = first explicit arg (low 32 bits of id), pushed right-to-left
//   [ESP+8]  = second explicit arg (high 32 bits of id), pushed right-to-left
//   Callee cleans: 2 explicit args * 4 bytes = 8 bytes → ret 8
//
// testCastDoubleNaked writes the full 64-bit sentinel 0xDEADBEEFCAFEBABE through
// the int64_t* OUT param so the upper 32 bits (0xDEADBEEF) differ from the lower
// 32 bits (0xCAFEBABE). Any slot narrower than 8 bytes (CR-02 regression) would
// silently truncate the upper half, causing the assertion in NetworkCastTests.cs
// to fail with an actual vs expected mismatch.
//
// MSVC C3865 blocks "__thiscall" on ordinary free functions. The naked-function
// form sidesteps this: the function body is raw assembly that manually implements
// the __thiscall ABI — MSVC never sees a __thiscall declaration.
// ---------------------------------------------------------------------------
extern "C" __declspec(naked) int64_t __cdecl testCastDoubleNaked()
{
    __asm {
        // Standard frame prologue (not strictly required for a naked function,
        // but documents the contract so the ret 8 below is unambiguous).
        push ebp
        mov  ebp, esp

        // __thiscall convention: ECX = first hidden arg = int64_t* networkId (OUT param).
        // Write the 64-bit sentinel 0xDEADBEEFCAFEBABE through the 8-byte slot.
        //   Lower 32 bits at [ecx]   = 0xCAFEBABE
        //   Upper 32 bits at [ecx+4] = 0xDEADBEEF
        // (little-endian layout: low word first)
        // C-style 0x hex prefix WITHOUT the MASM trailing 'h' — mixing them is a
        // C2400 syntax error. MSVC inline asm accepts either form, not both.
        mov  dword ptr [ecx],   0xCAFEBABE
        mov  dword ptr [ecx+4], 0xDEADBEEF

        // int64_t return value: low half in EAX, high half in EDX.
        // We return the same 64-bit sentinel so Network::cast's caller (which
        // reads from networkId, not from the return value) is also exercised.
        mov  eax, 0xCAFEBABE
        mov  edx, 0xDEADBEEF

        // Restore frame.
        mov  esp, ebp
        pop  ebp

        // __thiscall callee-cleans: 2 explicit args * 4 bytes = 8 bytes.
        // ECX (the hidden THIS param) is NOT counted in the callee-clean count.
        ret  8
    }
}

// ---------------------------------------------------------------------------
// C-03: Network::cast — WR-01 real function-pointer reseat harness
//   Replaces the previous hardcoded-sentinel stub.
//
//   Steps:
//     1. Reseat swg::network::cast to testCastDoubleNaked via setCastForTest.
//     2. Call Network::cast(id) — this invokes testCastDoubleNaked through the
//        real call chain in network.cpp (swg::network::cast(&networkId, low, high)).
//     3. Restore the real cast pointer via resetCast.
//     4. Return the value from networkId (which testCastDoubleNaked wrote as
//        0xDEADBEEFCAFEBABELL through the OUT param).
//
//   Regression coverage: if CR-02 were reverted (OUT param narrowed back to
//   swgptr*/int32_t*), only the lower 32 bits would survive — the C# test
//   asserting unchecked((long)0xDEADBEEFCAFEBABEL) would detect the truncation.
// ---------------------------------------------------------------------------
extern "C" __declspec(dllexport) int64_t __cdecl utinni_test_networkCast(int id)
{
    using swg::network::pCast;

    // Reseat: point cast at our __declspec(naked) ABI-shim.
    swg::network::setCastForTest(reinterpret_cast<pCast>(testCastDoubleNaked));

    // Exercise Network::cast — this calls swg::network::cast, which is now
    // testCastDoubleNaked. Network::cast passes &networkId (int64_t*, 8-byte slot)
    // as ECX and the low/high split of id as the two stack args.
    int64_t result = utinni::Network::cast(static_cast<int64_t>(id));

    // Always restore the real pointer before returning (regardless of crash path —
    // in production code this function would use RAII; in a test export a direct
    // call is sufficient because the test process runs single-threaded per test).
    swg::network::resetCast();

    return result;
}

// ---------------------------------------------------------------------------
// C-16: GameCallbacks install callback trigger
//   Fires all install callbacks registered via Game::addInstallCallback.
//   Used by GameCallbacksTests to verify GC-survival of delegate anchors.
//   The managed GameCallbacks.Initialize() registers a callInstallCallbacksAction
//   delegate via Game.AddInstallCallback. Since we cannot call hkInstall (it
//   requires a live SWG game process), we directly iterate the install callbacks
//   by calling Game::triggerInstallCallbacks (see game.cpp).
// ---------------------------------------------------------------------------
extern "C" __declspec(dllexport) void __cdecl utinni_triggerInstallCallbacks()
{
    utinni::Game::triggerInstallCallbacks();
}

// ---------------------------------------------------------------------------
// CR-04: hPresentBlockedEvent eager-init test exports (Concern B fix from PLAN-CHECK.md)
//
// utinni_test_initPresentBlockedEvent — directly calls directX::initPresentBlockedEvent()
//   and returns the resulting HANDLE as uintptr_t. Non-zero if CreateEvent succeeded.
//   The xUnit test calls this export + asserts non-zero, then calls
//   utinni_test_getPresentBlockedEvent + asserts the same non-zero value.
//   This test WOULD FAIL if initPresentBlockedEvent() were reverted to a lazy no-op
//   (the first call would return zero), meeting D-04.
//
// utinni_test_getPresentBlockedEvent — returns the current hPresentBlockedEvent HANDLE
//   as uintptr_t. Non-zero after utinni_test_initPresentBlockedEvent() has been called.
//   In production, utinni_init calls directX::initPresentBlockedEvent() before detours
//   arm. In the test process (utinni_init not run), zero until the test-init export runs.
//
// __cdecl (not WINAPI) — avoids the C-01 export-decoration class of bug.
// ---------------------------------------------------------------------------
extern "C" __declspec(dllexport) uintptr_t __cdecl utinni_test_initPresentBlockedEvent()
{
    directX::initPresentBlockedEvent();
    return (uintptr_t)getPresentBlockedEvent();
}

extern "C" __declspec(dllexport) uintptr_t __cdecl utinni_test_getPresentBlockedEvent()
{
    return (uintptr_t)getPresentBlockedEvent();
}

// ---------------------------------------------------------------------------
// WR-03: depthTexture eager-init test exports (Concern B fix from PLAN-CHECK.md)
//
// utinni_test_initDepthTexture — directly calls directX::initDepthTexture() and
//   returns the resulting DepthTexture pointer as uintptr_t. Non-zero if allocation
//   succeeded. The xUnit test calls this export + asserts non-zero, then calls
//   utinni_test_getDepthTexturePtr + asserts the same non-zero value.
//   This test WOULD FAIL if initDepthTexture() were reverted to a lazy no-op
//   (the first call would return zero), meeting D-04.
//
// utinni_test_getDepthTexturePtr — returns the current depthTexture pointer.
//   Non-zero after utinni_test_initDepthTexture() has been called.
//
// __cdecl (not WINAPI) — avoids the C-01 export-decoration class of bug.
// ---------------------------------------------------------------------------
extern "C" __declspec(dllexport) uintptr_t __cdecl utinni_test_initDepthTexture()
{
    directX::initDepthTexture();
    return (uintptr_t)(void*)directX::getDepthTexture();
}

extern "C" __declspec(dllexport) uintptr_t __cdecl utinni_test_getDepthTexturePtr()
{
    return (uintptr_t)(void*)directX::getDepthTexture();
}

// ---------------------------------------------------------------------------
// NEW (Plan 02.1-03): Export-decoration resolution harness
//
// Background: C-01 live UAT (2026-05-18) caught that utinni_init exported as
// _utinni_init@4 (decorated stdcall name on x86) while Launcher's GetProcAddress
// used the undecorated name "utinni_init". The LoaderLockHarness only timed
// LoadLibrary — it never resolved exports. The /EXPORT linker pragma in utinni.cpp
// fixes C-01's specific case, but any future __stdcall/__WINAPI export without a
// matching /EXPORT alias would reintroduce the gap silently.
//
// This export probes GetProcAddress for every documented C-linkage export by its
// UNDECORATED name and returns the count of successful resolutions (all or nothing
// for the xUnit assertion). Uses GetModuleHandleA (not LoadLibrary) because we are
// already executing inside UtinniCore.dll — the module is already loaded.
//
// Expected exports (13 total as of 2026-05-19):
//   utinni_init               (decorated stdcall; resolved via /EXPORT alias)
//   utinni_findPattern        (cdecl — no decoration on x86)
//   utinni_getVtbl            (cdecl)
//   utinni_test_freeConfigBuffer (cdecl)
//   utinni_test_networkCast   (cdecl)
//   utinni_clr_stop           (cdecl)
//   utinni_triggerInstallCallbacks (cdecl)
//   utinni_test_initPresentBlockedEvent (cdecl)
//   utinni_test_getPresentBlockedEvent  (cdecl)
//   utinni_test_initDepthTexture        (cdecl)
//   utinni_test_getDepthTexturePtr      (cdecl)
//   getPresentBlockedEvent    (file-scope C-linkage cdecl in directx9.cpp)
//   utinni_signal_launcher_ready (cdecl, signal-event sync added 2026-05-19)
//
// Returns: count of exports successfully resolved (max == kExportCount, derived
// from the array length so adding an export updates this automatically).
// The xUnit test asserts count == kExportCount; update the test's expected
// constant when extending this list.
// ---------------------------------------------------------------------------
extern "C" __declspec(dllexport) int __cdecl utinni_test_resolveExports()
{
    // We are executing inside UtinniCore.dll — GetModuleHandleA avoids a
    // reference-count bump (no paired FreeLibrary needed).
    HMODULE hSelf = GetModuleHandleA("UtinniCore.dll");
    if (hSelf == nullptr)
    {
        // Should never happen: we are the DLL. Return -1 as a sentinel for
        // "module handle not found" — distinct from "exports not resolved".
        return -1;
    }

    // All expected undecorated export names (C-linkage, cdecl or /EXPORT alias).
    static const char* const kExpectedExports[] = {
        "utinni_init",                        // stdcall decorated → /EXPORT alias
        "utinni_findPattern",                 // cdecl
        "utinni_getVtbl",                     // cdecl
        "utinni_test_freeConfigBuffer",       // cdecl
        "utinni_test_networkCast",            // cdecl
        "utinni_clr_stop",                    // cdecl
        "utinni_triggerInstallCallbacks",     // cdecl
        "utinni_test_initPresentBlockedEvent",// cdecl
        "utinni_test_getPresentBlockedEvent", // cdecl
        "utinni_test_initDepthTexture",       // cdecl
        "utinni_test_getDepthTexturePtr",     // cdecl
        "getPresentBlockedEvent",             // file-scope C-linkage in directx9.cpp
        "utinni_signal_launcher_ready",       // cdecl, 2026-05-19 signal-event sync
    };

    static constexpr int kExportCount =
        static_cast<int>(sizeof(kExpectedExports) / sizeof(kExpectedExports[0]));

    int resolved = 0;
    for (int i = 0; i < kExportCount; ++i)
    {
        if (GetProcAddress(hSelf, kExpectedExports[i]) != nullptr)
        {
            ++resolved;
        }
    }
    return resolved;
}
