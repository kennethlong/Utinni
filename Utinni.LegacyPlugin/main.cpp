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
// export to exercise PluginManager's virtual-destructor fallback path (D-13
// + CON-O-07 disposition D-15). This is the "Sytner shape" -- a plugin that
// only exports createPlugin and relies on the host to delete via the base
// UtinniPlugin virtual destructor.
//
// Built with /MT to MISMATCH UtinniCore.dll's /MD CRT -- proves the loader
// correctly detects "destroyPlugin missing" via GetProcAddress returning
// null and falls back to virtual destructor (vs the symmetric CrtMatchPlugin
// which exports destroyPlugin and routes through it).
//
// IMPORTANT: this fixture does NOT include "plugin_framework/utinni_plugin.h"
// in a way that triggers the UTINNI_PLUGIN macro, because that macro now
// REQUIRES a matching destroyPlugin definition (D-13). Instead, we declare a
// minimal local copy of UtinniPlugin's interface that matches the ABI exactly
// (same vtable layout, same Information struct). The host only ever calls
// virtual functions through the UtinniPlugin* base pointer, so as long as
// the vtable layout matches the host's expectations the legacy plugin works.
//
// Diagnostic counter: legacy_getInitCount lets the test confirm init() was
// invoked even though destroyPlugin isn't exported.

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
