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
using System.Drawing;
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

        // 2026-05-20 Issue #10 Phase B (owned-popup reparenting). SWG creates
        // its own top-level window; we capture that HWND in imgui_impl::setup
        // (first hkBeginScene), then on the managed side we strip its frame
        // styles, set FormMain as its owner via GWLP_HWNDPARENT, and position
        // it over PanelGame's client rect in screen coords.
        //
        // Why poll: capture happens downstream of PanelGame's ctor, so the
        // SWG HWND isn't ready yet. Once captured, we reparent once and stop
        // the timer; ongoing realignment is handled by Resize + LocationChanged.
        //
        // Why owned popup, NOT WS_CHILD: per CODEX consult #3 + MSFT docs,
        // IDirectInputDevice8::SetCooperativeLevel requires a TOP-LEVEL HWND.
        // WS_CHILD would break DirectInput. Owned-popup keeps top-level identity
        // while still letting FormMain own/move/minimize SWG as a group.
        private bool swgReparented;
        private readonly Timer reparentPollTimer = new Timer { Interval = 100 };
        private Form ownerFormCached;

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

            // Issue #10 Phase B wiring. Start poll on handle creation, stop on
            // dispose, realign on resize, and hook FormMain.LocationChanged as
            // soon as the owning form is reachable.
            reparentPollTimer.Tick += ReparentPollTimer_Tick;
            HandleCreated += PanelGame_HandleCreated_ForReparent;
            Resize += (s, e) => RepositionSwgWindow();

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
            reparentPollTimer.Stop();
            reparentPollTimer.Dispose();
            if (ownerFormCached != null)
            {
                ownerFormCached.LocationChanged -= OwnerForm_LocationChanged;
                ownerFormCached = null;
            }
            Game.Quit();
        }

        private void PanelGame_HandleCreated_ForReparent(object sender, EventArgs e)
        {
            // FindForm walks up to the top-level form once our HWND exists.
            // Cache + subscribe so we can realign on FormMain drags.
            ownerFormCached = FindForm();
            if (ownerFormCached != null)
            {
                ownerFormCached.LocationChanged += OwnerForm_LocationChanged;
            }
            reparentPollTimer.Start();
        }

        private void OwnerForm_LocationChanged(object sender, EventArgs e)
        {
            RepositionSwgWindow();
        }

        private void ReparentPollTimer_Tick(object sender, EventArgs e)
        {
            if (swgReparented)
            {
                reparentPollTimer.Stop();
                return;
            }
            if (!IsHandleCreated) return;
            if (ownerFormCached == null || !ownerFormCached.IsHandleCreated) return;

            IntPtr swgHwnd = Native.GetSwgHwnd();
            if (swgHwnd == IntPtr.Zero) return;

            ReparentSwgWindow(swgHwnd, ownerFormCached.Handle);
            reparentPollTimer.Stop();
        }

        private void ReparentSwgWindow(IntPtr swgHwnd, IntPtr ownerHwnd)
        {
            int oldStyle = Native.GetWindowLong(swgHwnd, Native.GWL_STYLE);
            uint u = unchecked((uint)oldStyle);
            uint frameMask = Native.WS_CAPTION | Native.WS_THICKFRAME
                           | Native.WS_MINIMIZEBOX | Native.WS_MAXIMIZEBOX
                           | Native.WS_SYSMENU | Native.WS_BORDER | Native.WS_DLGFRAME;
            u = (u & ~frameMask) | Native.WS_POPUP;
            Native.SetWindowLong(swgHwnd, Native.GWL_STYLE, unchecked((int)u));

            // GWLP_HWNDPARENT: documentation calls it "parent" but it actually
            // sets the OWNER. SWG stays top-level but gets minimized/closed
            // with FormMain as a group.
            Native.SetWindowLong(swgHwnd, Native.GWLP_HWNDPARENT, ownerHwnd.ToInt32());

            // Flag MUST flip before RepositionSwgWindow so the !swgReparented
            // guard inside it doesn't early-return. Without this the SWP_FRAMECHANGED
            // never fires and the stripped frame stays visible.
            swgReparented = true;
            RepositionSwgWindow();

            Log.Info("PanelGame: reparented SWG hwnd=0x" + swgHwnd.ToInt64().ToString("X")
                + " owner=0x" + ownerHwnd.ToInt64().ToString("X")
                + " oldStyle=0x" + oldStyle.ToString("X8") + " newStyle=0x" + u.ToString("X8")
                + " (Issue #10 Phase B -- owned popup)");
        }

        private void RepositionSwgWindow()
        {
            if (!swgReparented) return;
            if (!IsHandleCreated) return;
            IntPtr swgHwnd = Native.GetSwgHwnd();
            if (swgHwnd == IntPtr.Zero) return;

            // 2026-05-20 Issue #10 Phase B (iter 2): position-only, NOT size.
            // The first launch attempt (full SetWindowPos with PanelGame.ClientSize)
            // produced a black SWG window: D3D9's swapchain was created at SWG's
            // original window dimensions and Present() doesn't auto-rescale on
            // window resize in windowed mode. Triggering IDirect3DDevice9::Reset()
            // with new BackBufferWidth/Height + ImGui invalidate/recreate is a
            // separate concern (Phase B-bis). For now: leave SWG at its native
            // size, just move it over PanelGame to validate the owned-popup
            // reparent + frame-strip + DirectInput coop-level survival.
            Point screenOrigin = PointToScreen(Point.Empty);
            bool ok = Native.SetWindowPos(swgHwnd, IntPtr.Zero,
                screenOrigin.X, screenOrigin.Y,
                0, 0, // ignored under SWP_NOSIZE
                Native.SWP_NOSIZE | Native.SWP_FRAMECHANGED | Native.SWP_NOZORDER
                | Native.SWP_SHOWWINDOW | Native.SWP_NOACTIVATE);

            // Diag: cap to first 8 calls to avoid log spam during drag.
            if (s_repositionLogCount < 8)
            {
                s_repositionLogCount++;
                Log.Info("PanelGame.RepositionSwgWindow: SetWindowPos("
                    + "x=" + screenOrigin.X + ",y=" + screenOrigin.Y
                    + ",NOSIZE) -> " + (ok ? "OK" : "FAIL"));
            }
        }

        private static int s_repositionLogCount;

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
