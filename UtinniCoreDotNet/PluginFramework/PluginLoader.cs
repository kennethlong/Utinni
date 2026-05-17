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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Reflection;
using UtinniCore.Utinni;
using UtinniCoreDotNet.Utility;

namespace UtinniCoreDotNet.PluginFramework
{
    public class PluginLoader
    {
        public IEnumerable<IPlugin> Plugins;

        // C-06 testability surface: per-plugin failure descriptions are appended here
        // in addition to being routed through Log.Error. Lets tests assert that the
        // broken plugin's name surfaced without needing to hook the native log sink
        // (which requires the full UtinniCore CLR bridge to be initialized).
        public IList<string> LoadErrors { get; } = new List<string>();

        public PluginLoader()
        {
            Load();
        }

        // C-06 testability seam: skip the auto-Load from the production-style ctor so
        // tests can construct a loader without invoking the native PluginManager.
        // `autoLoad` is the friendly false sentinel; tests follow with Load(pluginDir).
        public PluginLoader(bool autoLoad)
        {
            if (autoLoad)
            {
                Load();
            }
        }

        // C-06 fix: per-plugin try/catch isolation. The previous single AggregateCatalog
        // + ComposeParts collapsed every plugin's load failure into one exception,
        // tearing down the editor for ALL plugins when ANY single plugin's [Export] or
        // ctor threw.
        //
        // Testability seam (pluginDir parameter): in production (no arg) we use
        // utinni.GetPath() + "/Plugins/" + the PluginManager's enabled plugin configs;
        // in tests we point at an explicit directory containing fixture DLLs and bypass
        // PluginManager entirely.
        public void Load(string pluginDir = null)
        {
            var loaded = new List<IPlugin>();
            LoadErrors.Clear();

            if (pluginDir == null)
            {
                LoadFromPluginManager(loaded);
            }
            else
            {
                LoadFromDirectory(pluginDir, loaded);
            }

            Plugins = loaded;
            Info(loaded.Count + " .NET Plugin(s) loaded");
        }

        // Production path: enumerate enabled plugins from the native PluginManager and
        // load each one's subdirectory as a separate isolated DirectoryCatalog.
        private void LoadFromPluginManager(List<IPlugin> loaded)
        {
            var pluginsRoot = utinni.GetPath() + "/Plugins/";
            var pluginManager = utinni.GetPluginManager();

            for (int j = 0; j < pluginManager.PluginConfigCount; j++)
            {
                var cfg = pluginManager.GetPluginConfigAt(j);
                if (!cfg.IsEnabled)
                {
                    continue;
                }

                var subDir = pluginsRoot + cfg.DirectoryName + "/";
                try
                {
                    LoadCatalog(new DirectoryCatalog(subDir), cfg.DirectoryName, loaded);
                }
                catch (Exception ex)
                {
                    // Catalog construction itself can throw (missing dir, etc.).
                    Error("PluginLoader: failed to scan '" + cfg.DirectoryName + "': "
                        + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        // Test path: load every *.dll in the given directory as its own per-DLL
        // DirectoryCatalog (via searchPattern), so one bad DLL does not poison the rest.
        private void LoadFromDirectory(string pluginDir, List<IPlugin> loaded)
        {
            if (!Directory.Exists(pluginDir))
            {
                return;
            }

            foreach (var dllPath in Directory.GetFiles(pluginDir, "*.dll"))
            {
                var dllName = Path.GetFileName(dllPath);
                try
                {
                    LoadCatalog(new DirectoryCatalog(pluginDir, dllName), dllName, loaded);
                }
                catch (Exception ex)
                {
                    Error("PluginLoader: failed to scan '" + dllName + "': "
                        + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private void LoadCatalog(ComposablePartCatalog catalog, string label, List<IPlugin> loaded)
        {
            try
            {
                var container = new CompositionContainer(catalog);
                var perPluginLoader = new PerPluginLoader();
                container.ComposeParts(perPluginLoader);

                if (perPluginLoader.Plugins != null)
                {
                    foreach (var p in perPluginLoader.Plugins)
                    {
                        loaded.Add(p);
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Error("PluginLoader: ReflectionTypeLoadException loading '" + label + "':");
                if (ex.LoaderExceptions != null)
                {
                    foreach (var le in ex.LoaderExceptions)
                    {
                        if (le != null)
                        {
                            Error("  " + le.GetType().Name + ": " + le.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Error("PluginLoader: failed to load '" + label + "': "
                    + ex.GetType().Name + ": " + ex.Message);
            }
        }

        // Routes a single error message through both LoadErrors (test visibility) and
        // Log.Error (production logging). Wrapped in try/catch so unit-test mode — where
        // the native log sink may not be initialized — does not throw out of Load.
        private void Error(string msg)
        {
            LoadErrors.Add(msg);
            try
            {
                Log.Error(msg);
            }
            catch
            {
                // Log sink not initialized (unit-test mode). LoadErrors retains the
                // message for assertions.
            }
        }

        private static void Info(string msg)
        {
            try
            {
                Log.Info(msg);
            }
            catch
            {
                // Log sink not initialized (unit-test mode).
            }
        }

        // Private nested holder for [ImportMany] composition. One instance per isolated
        // catalog so a throwing constructor in one plugin can't contaminate sibling
        // plugins' Plugins collection.
        private class PerPluginLoader
        {
            [ImportMany(typeof(IPlugin))]
            public IEnumerable<IPlugin> Plugins { get; set; }
        }
    }
}
