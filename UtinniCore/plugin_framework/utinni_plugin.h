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
    struct UTINNI_API UtinniPlugin
    {
        struct UTINNI_API Information
        {
            const char* name;
            const char* description;
            const char* author;
        };

        UtinniPlugin() {};
        virtual ~UtinniPlugin() {}

        virtual void init() {}

        virtual const Information& getInformation() const = 0;
    };
}


// Phase 3 R-B (per 03-CONTEXT D-13): the UTINNI_PLUGIN macro declares BOTH
// createPlugin() AND destroyPlugin(UtinniPlugin*) so plugins own both their
// allocation AND deallocation in their own CRT. This eliminates the cross-CRT
// delete crash class (CON-B-04 territory): when the host (UtinniCore.dll, /MD)
// previously did `delete plugin` on an object new'd inside a /MT plugin, the
// heap mismatch could AV at shutdown. Symmetric ABI = plugin allocates with
// plugin's `new`, plugin frees with plugin's `delete`, host never touches the
// allocator boundary.
//
// Plugins that use this macro must define a matching destroyPlugin body, e.g.:
//   UTINNI_PLUGIN;
//   utinni::UtinniPlugin* createPlugin() { return new MyPlugin(); }
//   void destroyPlugin(utinni::UtinniPlugin* p) { delete p; }
//
// D-15 / CON-O-07 disposition: legacy plugins (Sytner — upstream dormant per
// project_fork_strategy memory) that omit destroyPlugin compile-break-at-link
// against the new macro. CR-02 (03-REVIEW): PluginManager now rejects
// plugins without destroyPlugin at load time -- the previous virtual-destructor
// fallback ran in the host /MD CRT against plugin-CRT-allocated memory and
// was the CON-B-04 cross-CRT-free crash class. Plugin authors must adopt
// UTINNI_PLUGIN; there is no compat fallback.
//
// WR-02 (03-REVIEW) shutdown-lifecycle contract:
//
//   ~PluginManager invokes loaded.destroyFn(loaded.plugin) FIRST and then
//   FreeLibrary(loaded.hModule). Implications for destroyPlugin authors:
//
//     1. destroyPlugin runs while the framework is STILL FULLY FUNCTIONAL:
//        UtinniCore is loaded, log::* works, callback registries are still
//        live. If you previously subscribed callbacks (Subscribe*Callback),
//        unsubscribe them in destroyPlugin BEFORE doing anything else that
//        might invoke them.
//
//     2. Do NOT call utinni::log::* from inside destroyPlugin if you
//        previously subscribed an output sink callback that lives in your
//        own DLL -- the log dispatch could re-enter your own callback,
//        possibly on a callback whose pin is being torn down. Either
//        unsubscribe the output-sink callback first, or avoid log calls
//        during destroyPlugin.
//
//     3. FreeLibrary runs AFTER destroyPlugin returns. Anything that runs
//        in DLL_PROCESS_DETACH (e.g. static destructors in your DLL) must
//        not call back into the host -- the host may have already torn
//        down state by then.
//
//     4. Do not call FreeLibraryAndExitThread() from within destroyPlugin;
//        the loader expects destroyPlugin to return normally so the
//        FreeLibrary call can pair correctly.
//
// These constraints are theoretical for the current set of plugins (TJT,
// CrtMatchPlugin) but they're worth pinning down before more plugins ship.
#define UTINNI_PLUGIN \
    extern "C" __declspec(dllexport) utinni::UtinniPlugin* createPlugin(); \
    extern "C" __declspec(dllexport) void destroyPlugin(utinni::UtinniPlugin* p)

//#define UTINNI_PLUGIN extern "C" UTINNI_API utinni::UtinniPlugin* createPlugin()