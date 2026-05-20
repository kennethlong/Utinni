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
using System.Windows.Forms;
using UtinniCore.ImguiImpl;
using UtinniCore.Utinni;
using UtinniCoreDotNet.Hotkeys;
using UtinniCoreDotNet.PluginFramework;
using UtinniCoreDotNet.Utility;

namespace UtinniCoreDotNet.UI.Controls
{
    public class PanelGame : Panel
    {
        protected override void WndProc(ref Message m)
        {
            IntPtr swgWndProc = new IntPtr(0x00AA0970);
            Native.CallWindowProc(swgWndProc, m.HWnd, m.Msg, m.WParam, m.LParam); // Call and handle SWG's WndProc
            base.WndProc(ref m);
        }

        // 2026-05-20 Issue #9: PanelGame is empty in the new SWG-owns-its-own-
        // window model. HasFocus stays false -- there is no way the editor
        // panel can hold "game focus" when SWG is a separate top-level window.
        // FormMain.ProcessCmdKey reads this to decide hotkey routing; with it
        // pinned false, plugin hotkeys still process (their OnGameFocusOnly
        // gate is the only consumer that cares) and the editor stops swallowing
        // Tab. Revisit when Issue #10 (reparent SWG into PanelGame) lands.
        public bool HasFocus;

        private readonly PluginLoader pluginLoader;

        public PanelGame(PluginLoader pluginLoader)
        {
            base.Dock = DockStyle.Fill;
            base.AllowDrop = true;

            Disposed += PanelGame_Disposed;

            // 2026-05-20 Issue #9: focus/mouse-leave/mouse-move handlers REMOVED.
            // In the new architecture SWG owns its own top-level window, so
            // PanelGame focus events no longer correlate with game-input intent.
            // The old handlers fired Client.SuspendInput on every focus loss,
            // which unacquired DirectInput the moment the user clicked into
            // SWG's window -- killing Tab/Del/Return at the login screen.
            // Diagnosed via 2026-05-20 utinni.log capture (Phase A diag).

            KeyDown += PanelGame_KeyDown;

            Layout += PanelGame_Layout;

            GameDragDropEventHandlers.Initialize(this);

            this.pluginLoader = pluginLoader;
        }

        private void PanelGame_Layout(object sender, LayoutEventArgs e)
        {
            // 2026-05-20 Issue #9: Client.SetHwnd(Handle) call REMOVED. SWG now
            // creates its own top-level window; overwriting Client::hwnd with
            // PanelGame's handle made resumeInput() steal focus away from SWG
            // every cycle, and forced hkMainLoop to push the wrong HWND into
            // SWG's render path. SetHInstance is kept because hkMainLoop's
            // editor-mode branch waits for Client::getHInstance() != null as a
            // startup gate.
            Client.SetHInstance(Process.GetCurrentProcess().Handle);
        }

        private void PanelGame_Disposed(object sender, EventArgs e)
        {
            Game.Quit();
        }

        private void PanelGame_KeyDown(object sender, KeyEventArgs e)
        {
            foreach (IPlugin plugin in pluginLoader.Plugins)
            {
                IEditorPlugin editorPlugin = (IEditorPlugin)plugin;
                if (editorPlugin != null)
                {
                    HotkeyManager hotkeyManager = editorPlugin.GetHotkeyManager();

                    if (hotkeyManager != null && hotkeyManager.OnGameFocusOnly)
                    {
                        hotkeyManager.ProcessInput(e.Modifiers, e.KeyCode, HasFocus);
                    }
                }
            }
        }

    }
}
