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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using UtinniCoreDotNet.Callbacks;
using UtinniCoreDotNet.Live;
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

                // 16-02 Task 3 (MCP-03; C-01 / CUR-NEW-1 / R3-2): start the in-client live-bridge
                // named-pipe LISTENER here — AFTER the four *Callbacks.Initialize() calls above (so the
                // GameCallbacks.AddMainLoopCall marshal seam + the game-thread cache-refresh seam are
                // wired) and BEFORE the blocking Application.Run below (StartListener spawns a BACKGROUND
                // thread, so it is non-blocking; placing it pre-Run is correct).
                //
                //   * CLIENT-ROOT PINNING (CUR-NEW-1): the containment root is the resolved SWG CLIENT
                //     root (mirror FormIffEditor.ResolveClientRoot: MainModule dir → GetWorkingDirectory
                //     → [TreBrowser] clientDir) — the SAME root the editor save path + the MCP --root
                //     point at — NOT `injectRoot` (the Utinni DLL dir at main.cs above).
                //   * GAME-STATE CACHE (R3-2): the production probe `() => Game.IsRunning` is injected,
                //     but the native read is confined to the GAME THREAD — a recurring AddMainLoopCall
                //     re-enqueues itself each tick and calls RefreshGameStateOnGameThread(); the pipe
                //     worker reads ONLY the cached snapshot and never invokes the probe / Game.IsRunning.
                //   * DUAL-FLAG (CUR-NEW-6 / C-06 defense-in-depth): gated on the in-client
                //     `[Live] enableLiveBridge` config flag (default OFF, fail-closed), mirroring
                //     `enableEditorMode` below. --enable-live on the MCP host (16-03) gates the tool
                //     tier; BOTH must be ON for a working live path. Wrapped in try/catch (the
                //     AssemblyResolve/PluginLoader precedent) so a failure logs and never throws into
                //     the injection init path (D-04/D-06).
                try
                {
                    if (UtinniCore.Utinni.utinni.GetConfig().GetBool("Live", "enableLiveBridge"))
                    {
                        string clientRoot = ResolveClientRoot();
                        var liveServer = new LivePipeServer(
                            clientRoot,
                            gameStateProbe: () => UtinniCore.Utinni.Game.IsRunning, // native read — game-thread-only (below)
                            enqueue: GameCallbacks.AddMainLoopCall,
                            log: Log.InfoSimple);

                        // Wire the game-state cache refresh to a recurring game-thread tick: the Action
                        // runs on the game thread (AddMainLoopCall drains each frame), refreshes the
                        // cache from the native probe, then re-enqueues itself. R3-2: this is the ONLY
                        // path that reads native Game.IsRunning.
                        Action refresh = null;
                        refresh = () =>
                        {
                            liveServer.RefreshGameStateOnGameThread();
                            GameCallbacks.AddMainLoopCall(refresh);
                        };
                        GameCallbacks.AddMainLoopCall(refresh);

                        liveServer.StartListener(); // idempotent + background thread (non-blocking)
                        Log.InfoSimple("LivePipeServer: listening on '" + liveServer.PipeName
                            + "' (client root: " + (clientRoot ?? "(null)") + ").");
                    }
                }
                catch (Exception ex)
                {
                    Log.InfoSimple("LivePipeServer: failed to start (live bridge disabled): " + ex.Message);
                }

                if (UtinniCore.Utinni.utinni.GetConfig().GetBool("Editor", "enableEditorMode"))
                {
                    Application.Run(new FormMain(pluginLoader));
                }

            }
            return 0;
        }

        // 16-02 Task 3 (CUR-NEW-1 / R3-8): resolve the SWG CLIENT root the SAME way the editor save
        // path does (mirror UtinniPlugins FormIffEditor.ResolveClientRoot — MainModule dir →
        // GetWorkingDirectory → [TreBrowser] clientDir). This is the loose-override containment root the
        // MCP --root ALSO points at — NOT `injectRoot` (the Utinni DLL dir). Returns null if none
        // resolve (LivePipeServer's containment then rejects every relative path defensively).
        private static string ResolveClientRoot()
        {
            // (1) Process module directory (= SWGEmu.exe dir under injection).
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                string moduleDir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrEmpty(moduleDir) && Directory.Exists(moduleDir))
                {
                    return moduleDir;
                }
            }
            catch { /* fall through */ }
            // (2) GetWorkingDirectory.
            try
            {
                string wd = UtinniCore.Utility.utility.GetWorkingDirectory();
                if (!string.IsNullOrEmpty(wd) && Directory.Exists(wd))
                {
                    return wd;
                }
            }
            catch { /* binding unavailable outside a live client */ }
            // (3) ini fallback — sibling [TreBrowser] clientDir is the documented source.
            try
            {
                string configured = UtinniCore.Utinni.utinni.GetConfig().GetString("TreBrowser", "clientDir");
                if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured))
                {
                    return configured;
                }
            }
            catch { /* ini may not have the key yet */ }
            return null;
        }
    }
}
