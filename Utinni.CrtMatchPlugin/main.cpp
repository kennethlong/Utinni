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

// Phase 3 R-B fixture: CrtMatchPlugin exercises the symmetric createPlugin/
// destroyPlugin ABI path (D-13). Built with /MD to match UtinniCore.dll's CRT.
// Companion to Utinni.LegacyPlugin (which omits destroyPlugin entirely to
// exercise the loader's virtual-destructor fallback).
//
// Diagnostic exports (crtmatch_get*Count) let PluginManagerLifecycleTests
// observe the two-phase init order (D-14) and the destroyPlugin-on-shutdown
// invariant (D-13) via P/Invoke. Pattern follows the getSwgHwndExport
// C-linkage precedent (client.cpp:290-293) and the Phase 02 utinni_test_*
// bridge convention.

#include "plugin_framework/utinni_plugin.h"
#include <stdexcept>

namespace
{
    // Test-observable counters (read via the diagnostic exports below). They
    // are file-scope to keep the fixture self-contained -- no UtinniCore log
    // dependency, no managed-side wiring. Each is independent so the test
    // can assert two-phase ordering: at the between-passes observation
    // point createCount must be > 0 while initCount must still be 0.
    int s_createCount = 0;
    int s_initCount = 0;
    int s_destroyCount = 0;

    // Optional throw-on-init switch for the Plugin-init-throws regression
    // test (Task 2 Test 3). Defaults to false; the test toggles it via
    // crtmatch_setInitShouldThrow before triggering the load.
    bool s_initShouldThrow = false;

    class CrtMatchPlugin : public utinni::UtinniPlugin
    {
    public:
        void init() override
        {
            ++s_initCount;
            if (s_initShouldThrow)
            {
                throw std::runtime_error("CrtMatchPlugin::init deliberately threw");
            }
        }

        const Information& getInformation() const override
        {
            static Information info = { "CrtMatchPlugin", "R-B fixture (CRT match)", "Phase 3" };
            return info;
        }
    };
}

extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin()
{
    ++s_createCount;
    return new CrtMatchPlugin();
}

// Phase 3 R-B / D-13: symmetric destroyPlugin export. Plugin owns both
// the alloc and the free in its own CRT -- eliminates the cross-CRT delete
// crash class (CON-B-04). The test-observable counter lets us assert that
// ~PluginManager actually went through this path (vs the legacy virtual-
// destructor fallback exercised by Utinni.LegacyPlugin).
extern "C" __declspec(dllexport) void destroyPlugin(utinni::UtinniPlugin* p)
{
    ++s_destroyCount;
    delete p;
}

// -- Diagnostic exports (per PATTERNS.md line 185-189 + checker W5) ----------
//
// Distinct s_create vs s_init counters give the two-phase ordering Fact a
// between-passes observation point: after all createPlugins have returned
// but before init() runs, createCount == N and initCount == 0.

extern "C" __declspec(dllexport) int __cdecl crtmatch_getCreateCount()
{
    return s_createCount;
}

extern "C" __declspec(dllexport) int __cdecl crtmatch_getInitCount()
{
    return s_initCount;
}

extern "C" __declspec(dllexport) int __cdecl crtmatch_getDestroyCount()
{
    return s_destroyCount;
}

extern "C" __declspec(dllexport) void __cdecl crtmatch_resetCounters()
{
    s_createCount = 0;
    s_initCount = 0;
    s_destroyCount = 0;
    s_initShouldThrow = false;
}

extern "C" __declspec(dllexport) void __cdecl crtmatch_setInitShouldThrow(int shouldThrow)
{
    s_initShouldThrow = (shouldThrow != 0);
}
