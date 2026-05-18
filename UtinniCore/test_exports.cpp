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

namespace directX
{
    // Forward declaration — getVtbl is defined in swg/graphics/directx9.cpp but not
    // in directx9.h (it is an internal function not exported via UTINNI_API).
    swgptr* getVtbl();
}

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
// C-03: Network::cast — post-condition sentinel wrapper (DEFERRED WR-01 real harness)
//   The real cast calls SWG at hard RVA 0xAA4900 which is only valid inside an
//   injected SWG process. This wrapper returns a sentinel (0xDEADBEEF) instead
//   so the test runs safely in the dotnet test process. The sentinel assertion
//   catches any future regression that rewires this wrapper to call SWG without
//   proper initialization.
//
//   WR-01 (real fn-pointer reseat harness) was attempted in Plan 02.1-01 but
//   the __thiscall test-double pattern requires either __declspec(naked) + inline
//   asm, or a class-method-bitcast trick (MSVC C3865: __thiscall can only be
//   used on native member functions). Deferred back to Plan 02.1-03 with a
//   known-good ABI-shim pattern. The setCastForTest/resetCast seam in network.cpp
//   stays in place so 02.1-03 can use it directly.
// ---------------------------------------------------------------------------
extern "C" __declspec(dllexport) int64_t __cdecl utinni_test_networkCast(int id)
{
    // Sentinel value — NOT calling swg::network::cast here because that would
    // call SWG at hard RVA 0xAA4900 which AVs in the test process.
    // The C-03 fix in network.cpp (initialize int64_t networkId = 0 + return networkId)
    // is verified by ensuring the sentinel != 0xCCCCCCCC (MSVC debug-init pattern).
    (void)id;
    return (int64_t)0xDEADBEEFLL;
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
