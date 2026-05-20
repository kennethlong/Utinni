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
