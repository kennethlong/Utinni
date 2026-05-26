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
        // Phase 3 R-C (per 03-CONTEXT D-18..D-20 / TD-18): cached at ctor
        // time via Native.GetSwgWndProc() (P/Invoke into getSwgWndProcExport
        // which returns the SWG WndProc RVA declared once in
        // UtinniCore/swg/client/client.cpp). Previously this file held
        // a duplicated literal for that RVA here -- the value could drift
        // independently from the native-side declaration. Single source of
        // truth now lives in client.cpp; this field is the cached read-once
        // value, hot-path read in WndProc has zero per-message overhead.
        // (A grep gate in UtinniCoreDotNet.Tests/GetSwgWndProcTests.cs
        // asserts the literal RVA does not re-appear in this file.)
        private readonly IntPtr swgWndProcAddr;

        protected override void WndProc(ref Message m)
        {
            Native.CallWindowProc(swgWndProcAddr, m.HWnd, m.Msg, m.WParam, m.LParam); // Call and handle SWG's WndProc
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
            // Phase 3 R-C / D-20: resolve the SWG WndProc address once at
            // ctor time and cache as the readonly field above. WndProc reads
            // from the cache on the hot path -- no per-message P/Invoke
            // overhead.
            swgWndProcAddr = Native.GetSwgWndProc();

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
            //
            // Phase B-bis (#10) -- 2026-05-21 revert: the IDirect3DDevice9::Reset
            // approach (handoff sketch) is unworkable for SWG. SWG owns dozens
            // of default-pool resources we can't enumerate, so Reset() returns
            // D3DERR_INVALIDCALL and leaves the device in DEVICELOST -- next
            // SetVertexShaderConstantF crashes the process. Live-verified
            // crash on first launch of the Reset wiring. Going back to plain
            // SetWindowPos (with size, no SWP_NOSIZE) and relying on D3D9
            // windowed Present's natural stretch-to-pDestRect-or-client behavior;
            // diagnostics added to hkPresent to confirm what SWG actually
            // passes for pSourceRect/pDestRect/SwapEffect on the next run.
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

            // Don't reposition while the host form is minimized. WinForms reports a
            // minimized window's PointToScreen(Empty) as the ~(-32000,-32000) sentinel,
            // and the Resize handler fires on minimize -- propagating that to the SWG
            // child parks it far off-screen, so it "disappears" and returns mis-placed
            // (a gap) after restore. Restore is itself a Resize, which re-runs this with
            // valid coordinates and re-syncs the embed. (Tier-4 06-06 UAT finding.)
            Form host = FindForm();
            if (host != null && host.WindowState == FormWindowState.Minimized) return;

            // Phase B-bis (#10): position AND size + Z-order to TOP. 2026-05-21
            // verify: SWG renders correctly at the new (smaller) size via D3D9's
            // own stretching, BUT was getting buried behind FormMain in Z-order
            // because the owned-popup relationship (set via post-creation
            // GWLP_HWNDPARENT) doesn't always trigger Z-order recomputation.
            // Drop SWP_NOZORDER and pass HWND_TOP (IntPtr.Zero) as hWndInsertAfter
            // to bring SWG above FormMain. SWP_NOACTIVATE prevents focus theft;
            // FormMain stays the active window, SWG is just visually on top.
            // Subsequent FormMain activations carry SWG with them as a group
            // via the ownership relationship.
            Point screenOrigin = PointToScreen(Point.Empty);
            // Defensive: reject the off-screen minimized/transitional sentinel even if a
            // transitional WindowState slips past the guard above. Real panel origins are
            // never near -32000; moving SWG there is the "disappeared" bug.
            if (screenOrigin.X <= -30000 || screenOrigin.Y <= -30000) return;
            Size cs = ClientSize;
            bool ok = Native.SetWindowPos(swgHwnd, IntPtr.Zero,
                screenOrigin.X, screenOrigin.Y,
                cs.Width, cs.Height,
                Native.SWP_FRAMECHANGED | Native.SWP_SHOWWINDOW | Native.SWP_NOACTIVATE);

            // Diag: cap to first 8 calls to avoid log spam during drag.
            if (s_repositionLogCount < 8)
            {
                s_repositionLogCount++;
                Log.Info("PanelGame.RepositionSwgWindow: SetWindowPos("
                    + "x=" + screenOrigin.X + ",y=" + screenOrigin.Y
                    + ",w=" + cs.Width + ",h=" + cs.Height
                    + ",HWND_TOP) -> " + (ok ? "OK" : "FAIL"));
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
