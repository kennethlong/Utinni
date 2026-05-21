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

// Phase 3 R-B fixture: LegacyPlugin DELIBERATELY OMITS the destroyPlugin
// export. CR-02 (03-REVIEW) disposition: PluginManager now REJECTS plugins
// missing destroyPlugin at load time (the previous "delete via host CRT"
// fallback was unsound for /MT plugins -- it was the very CON-B-04
// cross-CRT-free crash class R-B was designed to eliminate). This fixture
// therefore proves the rejection path: load attempt logs an error,
// loadFromDirectory returns 0, dispose is a no-op.
//
// Built with /MT to demonstrate that the rejection works even for the
// hardest case (a CRT-mismatched plugin where the old fallback would have
// silently corrupted the heap on dispose).
//
// IMPORTANT: this fixture does NOT include "plugin_framework/utinni_plugin.h"
// in a way that triggers the UTINNI_PLUGIN macro, because that macro now
// REQUIRES a matching destroyPlugin definition (D-13). Instead, we declare a
// minimal local copy of UtinniPlugin's interface that matches the ABI exactly
// (same vtable layout, same Information struct). Because the fixture is
// rejected before any vtable dispatch happens, layout drift between this
// local struct and the host's utinni::UtinniPlugin no longer creates a
// correctness hazard at runtime -- but the static_assert below still catches
// gross size regressions (e.g. accidentally adding a data member here).
//
// Diagnostic counter: legacy_getInitCount lets the test confirm init() was
// invoked even though destroyPlugin isn't exported (currently unused under
// the rejection regime, retained for any future test that re-enables the
// load path).

#include <new>
#include <cstdint>

namespace utinni_legacy
{
    // Mirror the layout of utinni::UtinniPlugin from
    // UtinniCore/plugin_framework/utinni_plugin.h. The host calls these via
    // the vtable, so layout must match exactly. The macros __declspec(dllimport)
    // / dllexport on the host side don't change the vtable shape.
    struct UtinniPlugin
    {
        struct Information
        {
            const char* name;
            const char* description;
            const char* author;
        };

        UtinniPlugin() {}
        virtual ~UtinniPlugin() {}

        virtual void init() {}
        virtual const Information& getInformation() const = 0;
    };

    // CR-02 (03-REVIEW) ABI sanity assert. The host's utinni::UtinniPlugin
    // contains exactly one logical member: the vtable pointer (on x86, sizeof
    // == 4). Any new data member added to either side (here OR in the host)
    // would push this size past 4 and the static_assert fires. This is a
    // weak check (it does not validate the vtable order, only the size),
    // but it's the strongest thing we can do without including the host
    // header (which would trip the UTINNI_PLUGIN macro requirement). The
    // load-time rejection in PluginManager makes vtable-layout drift moot
    // for this specific fixture (it never reaches a dispatch site), but
    // the assert documents the contract.
    static_assert(sizeof(UtinniPlugin) == sizeof(void*),
                  "utinni_legacy::UtinniPlugin must be vtable-pointer-only "
                  "(matches host utinni::UtinniPlugin layout on x86).");
    static_assert(sizeof(UtinniPlugin::Information) == 3 * sizeof(void*),
                  "utinni_legacy::UtinniPlugin::Information must be three "
                  "const char* fields (matches host layout).");
}

namespace
{
    int s_initCount = 0;

    class LegacyPlugin : public utinni_legacy::UtinniPlugin
    {
    public:
        void init() override { ++s_initCount; }

        const Information& getInformation() const override
        {
            static Information info = { "LegacyPlugin", "R-B fixture (legacy / no destroyPlugin)", "Phase 3" };
            return info;
        }
    };
}

// Only createPlugin is exported. destroyPlugin is DELIBERATELY OMITTED so
// PluginManager's GetProcAddress(hDll, "destroyPlugin") returns null and
// triggers the virtual-destructor fallback (~PluginManager: delete plugin).
//
// NB: the return type is utinni_legacy::UtinniPlugin* but the host stores it
// as utinni::UtinniPlugin*. The ABI is identical (layout-compatible) so the
// reinterpret happens implicitly across the LoadLibrary/GetProcAddress
// boundary -- the host only sees a pointer, then calls through the vtable.
extern "C" __declspec(dllexport) utinni_legacy::UtinniPlugin* createPlugin()
{
    return new LegacyPlugin();
}

// Diagnostic exports.
extern "C" __declspec(dllexport) int __cdecl legacy_getInitCount()
{
    return s_initCount;
}

extern "C" __declspec(dllexport) void __cdecl legacy_resetCounters()
{
    s_initCount = 0;
}

// DllMain stub for /MT DLL.
#include <windows.h>
BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD /*ul_reason_for_call*/, LPVOID /*lpReserved*/)
{
    return TRUE;
}
