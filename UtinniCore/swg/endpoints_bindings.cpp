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

// Phase 24 / EPA-02 -- the SYMBOL-BEARING half of the swg::endpoints resolver
// (Option-A split, mirroring render_backend_dx9.cpp vs render_backend.cpp).
//
// This TU is compiled ONLY into UtinniCore.dll -- NEVER into UtinniCore.Tests --
// because it references the per-subsystem swg::* pFn literals, which are
// namespace-scope definitions with external linkage in misc/config.cpp +
// graphics/graphics.cpp but are NOT exported (no UTINNI_API) and so are absent
// from UtinniCore.lib's import surface. Dragging them into the test link is the
// LNK2001 the split avoids. The injection-free pure resolve()/lookupByName() +
// the subset static_assert live in endpoints.cpp and ARE compiled into the test.

#include "endpoints.h"

#include "utinni.h"          // byte/swgptr + the build's common prelude

// resolveFromExe() uses GetModuleHandleA/GetProcAddress -- the only Windows reach.
#include <Windows.h>

// ----------------------------------------------------------------------
// The per-subsystem pFn literal storage cells bound THIS plan (the critical-path
// rows that compile against existing literals -- config + the EPA-03
// graphics::install seam). Re-declared extern here so we can take their addresses
// for s_bindings[]. The typedefs MUST byte-match the originating TUs (verified
// against config.cpp:30-39 + graphics.cpp:37,61). The full 79-name catalog binds
// in Plan 02.
// ----------------------------------------------------------------------
namespace swg::config
{
using pLoadConfigFileBuffer = bool(__cdecl*)(byte* buffer, int bufferLength);
using pLoadConfigFileString = bool(__cdecl*)(const char* filename);
using pLoadOverrideConfig = int(__cdecl*)();

extern pLoadConfigFileBuffer loadConfigFileBuffer;
extern pLoadConfigFileString loadConfigFileString;
extern pLoadOverrideConfig loadOverrideConfig;
} // namespace swg::config

namespace swg::graphics
{
using pInstall = bool(__cdecl*)();

extern pInstall install;
} // namespace swg::graphics

namespace swg::endpoints
{
// ----------------------------------------------------------------------
// Critical-path binding seed (this plan): config crash-fixer + buffer/string
// loaders + the EPA-03 graphics::install row. The contract NAME is the key; the
// slot is the storage cell of the existing pFn literal. consoleHelper::sendInput
// (D-02 / WR-05) is deliberately ABSENT -- it stays on its RVA literal (3-arg ABI
// mismatch) and is allow-listed out of the coverage gate (see endpoints.cpp). The
// remaining 74 names bind in Plan 02.
// ----------------------------------------------------------------------
static const Binding s_bindings[] = {
    {"config::loadOverrideConfig", (void**)&swg::config::loadOverrideConfig},
    {"config::loadConfigFileBuffer", (void**)&swg::config::loadConfigFileBuffer},
    {"config::loadConfigFileString", (void**)&swg::config::loadConfigFileString},
    {"graphics::install", (void**)&swg::graphics::install},
};

// ----------------------------------------------------------------------
// D-03a (this side): the same X-macro subset assert, re-applied against the
// s_bindings[] names that live HERE. A bogus binding name in s_bindings[] fails
// the BUILD here, not silently at runtime (EPA-04 layer a / T-24-04). Kept
// duplicated next to the table it guards (the predicate has no external deps).
// ----------------------------------------------------------------------
namespace
{
constexpr bool ceStrEq(const char* a, const char* b)
{
    while (*a && (*a == *b))
    {
        ++a;
        ++b;
    }
    return *a == *b;
}

constexpr bool isInHookpointInc(const char* n)
{
    return false
#define UTINNI_HOOKPOINT(group, name) || ceStrEq(n, #group "::" #name)
#include "swg/utinni_engine_hookpoints.inc"
#undef UTINNI_HOOKPOINT
        ;
}

static_assert(isInHookpointInc("config::loadOverrideConfig"), "binding not in .inc");
static_assert(isInHookpointInc("config::loadConfigFileBuffer"), "binding not in .inc");
static_assert(isInHookpointInc("config::loadConfigFileString"), "binding not in .inc");
static_assert(isInHookpointInc("graphics::install"), "binding not in .inc");
} // namespace

bool resolveFromExe()
{
    using pGetEngineHookPoints = const UtinniEngineHookPoints*(__cdecl*)();

    HMODULE hExe = GetModuleHandleA(nullptr); // the injected SWG client exe
    auto pGet = reinterpret_cast<pGetEngineHookPoints>(
        GetProcAddress(hExe, "GetEngineHookPoints"));
    if (pGet == nullptr)
    {
        // SWGEmu Pre-CU: no export -> STRICT NO-OP. Every swg::* RVA literal is
        // left exactly as-is so the existing D3D9 path is byte-for-byte unchanged
        // (D-00 / criterion 3 / T-24-03).
        utinni::log::info("endpoints: no GetEngineHookPoints export -- RVA path (SWGEmu Pre-CU)");
        return false;
    }

    const UtinniEngineHookPoints* table = pGet();
    resolve(table, s_bindings, sizeof(s_bindings) / sizeof(s_bindings[0]));
    return true;
}
} // namespace swg::endpoints
