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

#include "plugin_manager.h"
#include <filesystem>
#include <exception>
#include "utility/string_utility.h"

namespace utinni
{
using pCreatePlugin = UtinniPlugin* (*)();
// Phase 3 R-B (D-13): symmetric destroyPlugin export -- plugin owns both
// alloc and free in its own CRT, eliminating the cross-CRT delete crash
// class (CON-B-04). May be null for legacy plugins (CON-O-07 disposition
// D-15: Sytner shape; fall back to virtual destructor at shutdown).
using pDestroyPlugin = void (*)(UtinniPlugin*);

struct PluginManager::Impl
{
    std::vector<PluginConfig> pluginConfigs;

    // Phase 3 R-B / D-16: track HMODULE alongside plugin pointer (and the
    // cached destroyPlugin export) so ~PluginManager can call destroyPlugin
    // in plugin's CRT then FreeLibrary in host's CRT in the correct order.
    // CON-N-08 invariant preserved: this struct is the anonymous pImpl in
    // the .cpp -- plugin_manager.h public surface (lines 32-52) does NOT
    // change.
    struct LoadedPlugin
    {
        HMODULE hModule;
        UtinniPlugin* plugin;
        pDestroyPlugin destroyFn; // nullptr for legacy plugins
    };
    std::vector<LoadedPlugin> plugins;

    // R-B test-bridge observable (Task 2 / utinni_test_lastLoadLibraryError).
    // Reset to 0 at the start of every loadPlugins invocation; set to the
    // most-recent failing LoadLibrary's GetLastError if any DLL in the
    // current pass failed to load. Read by xUnit to assert the
    // log+continue path fires.
    DWORD lastLoadLibraryError = 0;
};

PluginManager::PluginManager() : pImpl(new Impl) {}

PluginManager::~PluginManager()
{
    // Phase 3 R-B / D-13 + D-16: shutdown order is destroyPlugin (in
    // plugin's CRT) followed by FreeLibrary (host's ref-count decrement;
    // not a CRT allocation, safe from host CRT).
    //
    // CR-02 (03-REVIEW): the previous "legacy fallback" `delete loaded.plugin`
    // path was unsound by construction -- it ran in UtinniCore.dll's /MD CRT
    // against memory that could have been allocated in any plugin's CRT
    // (notably a /MT-built fixture's static CRT). The cross-CRT free is
    // exactly the CON-B-04 crash class R-B was designed to eliminate, so
    // we now REJECT plugins that don't export destroyPlugin at load time
    // (see loadPlugins / test_loadFromDirectory). Anything still reaching
    // this loop therefore has a non-null destroyFn -- assert that
    // invariant and call destroyFn unconditionally.
    for (const auto& loaded : pImpl->plugins)
    {
        if (loaded.destroyFn != nullptr)
        {
            loaded.destroyFn(loaded.plugin);
        }
        else
        {
            // Defense-in-depth: should be unreachable given the
            // load-time rejection above. Log + leak the allocation
            // rather than risk a cross-CRT delete crash. CON-N-08
            // safety: not freeing the plugin memory here is the lesser
            // evil; a true legacy plugin would not have reached this
            // point post-fix.
            log::critical(
                "PluginManager::~PluginManager: encountered plugin with null destroyFn "
                "(should have been rejected at load time per CR-02). Leaking allocation "
                "to avoid cross-CRT delete crash.");
        }
        if (loaded.hModule != nullptr)
        {
            FreeLibrary(loaded.hModule);
        }
    }

    delete pImpl;
}

void PluginManager::loadPlugins() const
{
    const std::string pluginDir = getPath() + "/Plugins/";

    auto& cfg = getConfig();
    // R-B test-bridge: reset the captured LoadLibrary error at the start
    // of every loadPlugins call so xUnit can isolate per-test observations.
    pImpl->lastLoadLibraryError = 0;

    // Get the [Plugins] load order list from ut.ini
    int i = 0;
    while (true)
    {
        std::string curStr = cfg.getString("Plugins", std::string("plugin_" + stringUtility::toString(i, 2)).c_str());
        if (!curStr.empty())
        {
            i++;
            const int splitterPos = curStr.find_first_of(',');
            if (splitterPos == -1)
            {
                log::error(std::string("Failed to parse [Plugins] priority list value due to missing separator: " + curStr).c_str());
                continue;
            }

            std::string directoryName = curStr.substr(splitterPos + 1);
            std::string isEnabled = curStr.substr(0, splitterPos);
            PluginConfig pluginInfo = {stringUtility::trim(directoryName), stringUtility::toBool(stringUtility::trim(isEnabled))};
            pImpl->pluginConfigs.emplace_back(pluginInfo);
        }
        else
        {
            // Stop if [Plugins] plugin_ + i doesn't exist
            break;
        }
    }

    // Create directory in case it doesn't exist for whatever reason
    CreateDirectory(pluginDir.c_str(), nullptr);

    // Check if each found plugin directory exists in the [Plugins] load order, if not, add it at the back
    std::vector<std::string> directories;
    for (const auto& dir : std::filesystem::recursive_directory_iterator(pluginDir))
    {
        if (dir.is_directory())
        {
            const std::string& dirFilename = dir.path().filename().string();

            auto it = std::find_if(pImpl->pluginConfigs.begin(), pImpl->pluginConfigs.end(), [&dirFilename](const PluginConfig& pluginInfo)
                                   { return pluginInfo.directoryName == dirFilename; });

            if (it == pImpl->pluginConfigs.end())
            {
                PluginConfig pluginInfo = {dirFilename, true};
                pImpl->pluginConfigs.emplace_back(pluginInfo);
                // ToDo potentially add a cfg setting to not automatically enable the plugin if it didn't exist in the list
            }
        }
    }

    // Update the [Plugins] section in ut.ini & load the plugins in the correct load order set in the cfg
    cfg.DeleteSection("Plugins");
    for (size_t j = 0; j < pImpl->pluginConfigs.size(); j++)
    {
        const auto pluginCfg = pImpl->pluginConfigs[j];
        cfg.setString("Plugins", std::string("plugin_" + stringUtility::toString(j, 2)).c_str(),
                      std::string(stringUtility::toString(pluginCfg.isEnabled) + ", " + pluginCfg.directoryName).c_str());

        // if the plugin is enabled, scan its directory for .DLL's and see if they implement createPlugin
        if (pluginCfg.isEnabled)
        {
            auto currentPluginDir = pluginDir + pluginCfg.directoryName + "/";

            if (!std::filesystem::exists(currentPluginDir))
            {
                pImpl->pluginConfigs.erase(pImpl->pluginConfigs.begin() + j);
                j--;
                continue;
            }

            // Phase 3 R-B / D-14 + D-17: two-phase init. Pass 1 creates all
            // plugins (LoadLibrary + createPlugin + capture HMODULE + cached
            // destroyPlugin). LoadLibrary failures are surfaced visibly via
            // log::error with GetLastError -- no MessageBox (would block
            // startup); log + continue, same disposition as Phase 02 C-06
            // PluginLoader per-plugin try/catch. Pass 2 invokes init() on
            // every loaded plugin with per-plugin try/catch isolation, so
            // one throwing plugin doesn't kill the rest of the load
            // sequence (extends C-06 from compose-time to init-time).
            std::vector<typename Impl::LoadedPlugin> currentDirPlugins;

            for (const auto& file : std::filesystem::recursive_directory_iterator(currentPluginDir))
            {
                if (file.path().extension() != ".dll")
                {
                    continue;
                }

                const std::string dllPath = file.path().string();
                const HMODULE hDllInstance = LoadLibrary(dllPath.c_str());
                if (hDllInstance == nullptr)
                {
                    // D-17: log + continue. GetLastError captured for the
                    // test bridge (utinni_test_lastLoadLibraryError) and
                    // formatted into the log line for human triage.
                    const DWORD err = GetLastError();
                    pImpl->lastLoadLibraryError = err;
                    char errMsg[768];
                    std::snprintf(errMsg, sizeof(errMsg),
                                  "Failed to load plugin DLL '%s': GetLastError=%lu",
                                  dllPath.c_str(), static_cast<unsigned long>(err));
                    log::error(errMsg);
                    continue;
                }

                const auto createPlugin = (pCreatePlugin)GetProcAddress(hDllInstance, "createPlugin");
                if (createPlugin == nullptr)
                {
                    // Not a valid Utinni plugin; release the DLL and move on.
                    FreeLibrary(hDllInstance);
                    continue;
                }

                UtinniPlugin* plugin = createPlugin();
                if (plugin == nullptr)
                {
                    FreeLibrary(hDllInstance);
                    continue;
                }

                // D-13: cached destroyPlugin export. CR-02 (03-REVIEW):
                // plugins without destroyPlugin are rejected at load
                // time. The previous "legacy fallback" (`delete plugin`
                // in the host /MD CRT) was unsafe for plugins allocated
                // in a different CRT (notably /MT static CRT) -- that's
                // the CON-B-04 cross-CRT free crash class R-B was
                // designed to eliminate. Legacy plugins (CON-O-07
                // disposition D-15) must migrate to the symmetric ABI
                // (UTINNI_PLUGIN macro emits both createPlugin and
                // destroyPlugin).
                const auto destroyPluginFn = (pDestroyPlugin)GetProcAddress(hDllInstance, "destroyPlugin");
                if (destroyPluginFn == nullptr)
                {
                    char errMsg[768];
                    std::snprintf(errMsg, sizeof(errMsg),
                                  "Rejecting plugin DLL '%s': no destroyPlugin export "
                                  "(legacy ABI is no longer supported -- the new "
                                  "UTINNI_PLUGIN macro emits both createPlugin and "
                                  "destroyPlugin; rebuild the plugin against the "
                                  "current SDK).",
                                  dllPath.c_str());
                    log::error(errMsg);
                    // The plugin's createPlugin already returned an
                    // object allocated in the plugin's CRT. We CANNOT
                    // call `delete` on it from here (that's the very
                    // bug we're refusing to perpetuate). Leak the
                    // allocation; FreeLibrary will tear down the
                    // plugin's static heap on DLL unload anyway, so
                    // process-lifetime impact is bounded.
                    FreeLibrary(hDllInstance);
                    continue;
                }

                currentDirPlugins.push_back({hDllInstance, plugin, destroyPluginFn});
                pImpl->plugins.push_back({hDllInstance, plugin, destroyPluginFn});
            }

            // Pass 2: init() each plugin loaded in pass 1. Per-plugin
            // try/catch isolation (D-14 + Phase 02 C-06 disposition).
            // Subscribe-time exceptions (typical: missing dependency, bad
            // config) are logged but don't bring down the rest of the load.
            for (const auto& loaded : currentDirPlugins)
            {
                try
                {
                    loaded.plugin->init();
                }
                catch (const std::exception& ex)
                {
                    char errMsg[768];
                    const char* pluginName = "<unknown>";
                    // getInformation may itself throw on a broken plugin;
                    // guard via a nested try (defensive, mirroring
                    // PluginLoader's C-06 isolation depth).
                    try
                    {
                        pluginName = loaded.plugin->getInformation().name;
                    }
                    catch (...)
                    {
                        pluginName = "<unknown (getInformation threw)>";
                    }
                    std::snprintf(errMsg, sizeof(errMsg),
                                  "Plugin init() threw: %s (plugin name=%s)",
                                  ex.what(), pluginName);
                    log::error(errMsg);
                }
                catch (...)
                {
                    log::error("Plugin init() threw unknown (non-std::exception) type");
                }
            }
        }
    }
    cfg.save();
}

int PluginManager::getPluginConfigCount() const
{
    return pImpl->pluginConfigs.size();
}

const PluginManager::PluginConfig& PluginManager::getPluginConfigAt(int i)
{
    return pImpl->pluginConfigs.at(i);
}

// -- Phase 3 R-B test-bridge plumbing (Plan 03-02 Task 2) ----------------
//
// Helpers below live in the same TU as PluginManager so they have access
// to the Impl struct (CON-N-08 invariant preserved: nothing in
// plugin_manager.h changes). The xUnit Facts in PluginManagerLifecycleTests
// P/Invoke into the extern "C" exports at the bottom of this file to drive
// load-from-arbitrary-dir scenarios without depending on the global
// [Plugins] section / getPath() + /Plugins/ path that production
// loadPlugins reads. Placed in a named test_internal namespace so the
// extern "C" exports outside `namespace utinni` can name them.

namespace test_internal
{
// Test-only Impl-equivalent struct. Defined separately from
// PluginManager::Impl because Impl is private inside the PluginManager
// class (CON-N-08 invariant: cannot widen the public header to add a
// friend or test-accessor). The two structs share the same LoadedPlugin
// shape so the loader/disposer logic is identical, but they are
// distinct types -- production PluginManager state and test state
// never alias.
struct TestImpl
{
    struct LoadedPlugin
    {
        HMODULE hModule;
        UtinniPlugin* plugin;
        pDestroyPlugin destroyFn;
    };
    std::vector<LoadedPlugin> plugins;
    DWORD lastLoadLibraryError = 0;
};

// Test-only: load every .dll in `dir` directly (no [Plugins] cfg
// filtering). Mirrors the two-phase logic in PluginManager::loadPlugins
// but skips the [Plugins] section enumeration so xUnit can stage a
// fresh temp dir without polluting ut.ini. Same observable
// side-effects: lastLoadLibraryError captured, log::error on
// LoadLibrary failure, per-plugin try/catch isolation around init().
inline void test_loadFromDirectory(TestImpl& impl, const std::string& dir)
{
    impl.lastLoadLibraryError = 0;
    std::vector<TestImpl::LoadedPlugin> currentDirPlugins;

    for (const auto& file : std::filesystem::recursive_directory_iterator(dir))
    {
        if (file.path().extension() != ".dll")
        {
            continue;
        }

        const std::string dllPath = file.path().string();
        const HMODULE hDllInstance = LoadLibrary(dllPath.c_str());
        if (hDllInstance == nullptr)
        {
            const DWORD err = GetLastError();
            impl.lastLoadLibraryError = err;
            char errMsg[768];
            std::snprintf(errMsg, sizeof(errMsg),
                          "Failed to load plugin DLL '%s': GetLastError=%lu",
                          dllPath.c_str(), static_cast<unsigned long>(err));
            log::error(errMsg);
            continue;
        }

        const auto createPlugin = (pCreatePlugin)GetProcAddress(hDllInstance, "createPlugin");
        if (createPlugin == nullptr)
        {
            FreeLibrary(hDllInstance);
            continue;
        }

        UtinniPlugin* plugin = createPlugin();
        if (plugin == nullptr)
        {
            FreeLibrary(hDllInstance);
            continue;
        }

        // CR-02 (03-REVIEW) parity with production loadPlugins:
        // reject plugins missing destroyPlugin. Mirrors the
        // load-time rejection in loadPlugins to keep test behavior
        // observably identical to production.
        const auto destroyPluginFn = (pDestroyPlugin)GetProcAddress(hDllInstance, "destroyPlugin");
        if (destroyPluginFn == nullptr)
        {
            char errMsg[768];
            std::snprintf(errMsg, sizeof(errMsg),
                          "Rejecting plugin DLL '%s': no destroyPlugin export "
                          "(legacy ABI is no longer supported).",
                          dllPath.c_str());
            log::error(errMsg);
            FreeLibrary(hDllInstance);
            continue;
        }
        currentDirPlugins.push_back({hDllInstance, plugin, destroyPluginFn});
        impl.plugins.push_back({hDllInstance, plugin, destroyPluginFn});
    }

    for (const auto& loaded : currentDirPlugins)
    {
        try
        {
            loaded.plugin->init();
        }
        catch (const std::exception& ex)
        {
            char errMsg[768];
            const char* pluginName = "<unknown>";
            try
            {
                pluginName = loaded.plugin->getInformation().name;
            }
            catch (...)
            {
                pluginName = "<unknown (getInformation threw)>";
            }
            std::snprintf(errMsg, sizeof(errMsg),
                          "Plugin init() threw: %s (plugin name=%s)",
                          ex.what(), pluginName);
            log::error(errMsg);
        }
        catch (...)
        {
            log::error("Plugin init() threw unknown (non-std::exception) type");
        }
    }
}

// File-static test TestImpl instance, owned by the test exports below.
// Standalone (not via PluginManager) so CON-N-08 stays byte-identical
// -- no test-seam additions to PluginManager's header.
inline TestImpl*& s_testImpl()
{
    // Function-local static keeps the symbol private to this TU
    // while still being settable from utinni_test_* exports.
    static TestImpl* impl = nullptr;
    return impl;
}

// Test-only teardown: replicate ~PluginManager's shutdown order on
// the standalone TestImpl. destroyPlugin (or virtual dtor fallback)
// followed by FreeLibrary. Logs nothing on the success path.
inline void test_disposeImpl()
{
    TestImpl*& impl = s_testImpl();
    if (impl == nullptr)
    {
        return;
    }
    for (const auto& loaded : impl->plugins)
    {
        if (loaded.destroyFn != nullptr)
        {
            loaded.destroyFn(loaded.plugin);
        }
        else
        {
            // CR-02 parity with ~PluginManager: should be unreachable
            // because test_loadFromDirectory now rejects plugins
            // without destroyPlugin. Leak rather than risk
            // cross-CRT free.
            log::critical(
                "test_disposeImpl: encountered plugin with null destroyFn "
                "(should have been rejected at load time per CR-02). Leaking.");
        }
        if (loaded.hModule != nullptr)
        {
            FreeLibrary(loaded.hModule);
        }
    }
    delete impl;
    impl = nullptr;
}
} // namespace test_internal
} // namespace utinni

// -- extern "C" test exports ------------------------------------------------
//
// __cdecl per the C-01 export-decoration discipline (avoids stdcall name
// mangling on x86). All five exports route through the file-static test
// Impl in plugin_manager.cpp -- standalone from production PluginManager so
// CON-N-08 stays byte-identical (no test-only members on the public class).

extern "C" __declspec(dllexport) int __cdecl utinni_test_pluginManagerLoadFromDir(const char* dir)
{
    if (dir == nullptr)
    {
        return -1;
    }
    // Dispose any prior test Impl to avoid leaking across tests.
    utinni::test_internal::test_disposeImpl();
    auto& impl = utinni::test_internal::s_testImpl();
    impl = new utinni::test_internal::TestImpl();
    utinni::test_internal::test_loadFromDirectory(*impl, std::string(dir));
    return static_cast<int>(impl->plugins.size());
}

extern "C" __declspec(dllexport) int __cdecl utinni_test_pluginManagerLoadedCount()
{
    auto* impl = utinni::test_internal::s_testImpl();
    if (impl == nullptr)
    {
        return 0;
    }
    return static_cast<int>(impl->plugins.size());
}

extern "C" __declspec(dllexport) void __cdecl utinni_test_pluginManagerDispose()
{
    utinni::test_internal::test_disposeImpl();
}

extern "C" __declspec(dllexport) unsigned long __cdecl utinni_test_lastLoadLibraryError()
{
    auto* impl = utinni::test_internal::s_testImpl();
    if (impl == nullptr)
    {
        return 0;
    }
    return static_cast<unsigned long>(impl->lastLoadLibraryError);
}
