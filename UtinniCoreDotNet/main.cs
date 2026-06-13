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
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using UtinniCoreDotNet.Callbacks;
using UtinniCoreDotNet.PluginFramework;
using UtinniCoreDotNet.UI.Forms;
using UtinniCoreDotNet.Utility;

namespace UtinniCoreDotNet
{
    internal static class Startup
    {
        private static bool initialized;

        [STAThread]
        private static int EntryPoint(string args)
        {
            if (!initialized)
            {
                initialized = true;

                // 15-12 (15-SMOKE B5 — BLOCKING): install the injected-host AssemblyResolve handler
                // BEFORE anything that can touch UtinniCoreDotNet.PathContainment / LooseOverridePath
                // (the PluginLoader below constructs plugins that call LooseOverridePath at
                // registration). Under injection clr::load() calls
                // ExecuteInDefaultAppDomain(<injectRoot>/UtinniCoreDotNet.dll, ...) which runs in the
                // DEFAULT AppDomain whose APPBASE is the host exe dir (SWGEmu.exe), NOT the Utinni
                // inject root — so the BCL probe never searches the inject root and the netstandard2.0
                // PathContainment façade (netstandard.dll) fails to bind. This handler probes the
                // inject root (the directory of the executing UtinniCoreDotNet.dll) for the narrow
                // allow-list (netstandard + UtinniCoreDotNet.PathContainment) via the unit-tested
                // InjectedAssemblyResolver decision, and binds from there. It NEVER throws: a failed
                // LoadFrom logs and returns null so the default bind cascade is not aborted.
                // NOTE (live): a cached FAILED bind is not re-driven — the maintainer must RELAUNCH to
                // pick up this fix in a fresh process. Injected-only behavior; confirmed live in the
                // 15-18 re-smoke against the 15-17 reassembled build.
                string injectRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
                {
                    string probePath = InjectedAssemblyResolver.ResolveProbePath(injectRoot, resolveArgs.Name, File.Exists);
                    if (probePath == null)
                    {
                        return null;
                    }

                    try
                    {
                        return Assembly.LoadFrom(probePath);
                    }
                    catch (Exception ex)
                    {
                        Log.Info($"InjectedAssemblyResolver: failed to load '{probePath}' for '{resolveArgs.Name}': {ex.Message}");
                        return null;
                    }
                };

                Application.EnableVisualStyles();

                Log.Setup();

                // Load plugins from the /Plugins/ directory
                PluginLoader pluginLoader = new PluginLoader();

                // Initialize callbacks that aren't purely editor related
                GameCallbacks.Initialize();
                GroundSceneCallbacks.Initialize();
                ObjectCallbacks.Initialize();
                CuiCallbacks.Initialize();

                // 2026-05-19: signal the Launcher that all C++ + managed plugin
                // setup is complete and SWG's main thread is safe to resume past
                // the EB FE stall the Launcher applied at PE entry. By this point
                // createDetours/createPatches have run (native side) and the four
                // *Callbacks.Initialize() calls above have wired managed
                // callbacks, so any hook that fires post-resume has its
                // registrations in place. After this returns the next call is
                // Application.Run which blocks for the editor's lifetime --
                // hence we cannot defer the signal further. See Launcher/
                // main.cpp loadDll() and UtinniCore/utinni.cpp
                // utinni_signal_launcher_ready for the wait/signal mechanics.
                Native.SignalLauncherReady();

                if (UtinniCore.Utinni.utinni.GetConfig().GetBool("Editor", "enableEditorMode"))
                {
                    Application.Run(new FormMain(pluginLoader));
                }

            }
            return 0;
        }
    }
}
