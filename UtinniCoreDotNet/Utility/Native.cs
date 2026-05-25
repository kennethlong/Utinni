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
using System.Runtime.InteropServices;

namespace UtinniCoreDotNet.Utility
{
    public static class Native
    {
        public const int WM_SYSCOMMAND = 0x112;
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_MOUSEMOVE = 0x0200;

        public const int SC_DRAGMOVE = 0xF012; // SC_MOVE | HTCAPTION
        public const int SC_MINIMIZE = 0xF020;
        public const int SC_RESTORE = 0xF120;
        public const int SC_MAXIMIZE = 0xF030;

        public const int CS_DROPSHADOW = 0x20000;

        // 2026-05-20 Issue #10 Phase B (owned-popup reparenting). GWL_* indices
        // for SetWindowLong / GWLP_HWNDPARENT for owner-relationship setting
        // (GWLP_HWNDPARENT is the documented index but the field really sets
        // the OWNER, not a parent -- a misnomer in the Win32 API).
        public const int GWL_STYLE       = -16;
        public const int GWLP_HWNDPARENT = -8;

        // Window styles we care about for stripping SWG's frame.
        public const uint WS_POPUP       = 0x80000000;
        public const uint WS_CAPTION     = 0x00C00000;
        public const uint WS_THICKFRAME  = 0x00040000;
        public const uint WS_MINIMIZEBOX = 0x00020000;
        public const uint WS_MAXIMIZEBOX = 0x00010000;
        public const uint WS_SYSMENU     = 0x00080000;
        public const uint WS_BORDER      = 0x00800000;
        public const uint WS_DLGFRAME    = 0x00400000;

        // SetWindowPos flags.
        public const uint SWP_NOSIZE        = 0x0001;
        public const uint SWP_NOZORDER      = 0x0004;
        public const uint SWP_NOACTIVATE    = 0x0010;
        public const uint SWP_FRAMECHANGED  = 0x0020;
        public const uint SWP_SHOWWINDOW    = 0x0040;

        public enum WM_HitTests
        {
            HTNOWHERE = 0,
            HTCLIENT = 1,
            HTCAPTION = 2,
            HTGROWBOX = 4,
            HTSIZE = HTGROWBOX,
            HTMINBUTTON = 8,
            HTMAXBUTTON = 9,
            HTLEFT = 10,
            HTRIGHT = 11,
            HTTOP = 12,
            HTTOPLEFT = 13,
            HTTOPRIGHT = 14,
            HTBOTTOM = 15,
            HTBOTTOMLEFT = 16,
            HTBOTTOMRIGHT = 17,
            HTREDUCE = HTMINBUTTON,
            HTZOOM = HTMAXBUTTON,
            HTSIZEFIRST = HTLEFT,
            HTSIZELAST = HTBOTTOMRIGHT,
            HTTRANSPARENT = -1
        }

        [DllImport("user32.dll", EntryPoint = "ReleaseCapture")]
        public static extern void ReleaseCapture();

        // Phase 6 06-05 D-16(f): IntPtr wParam/lParam is the x64-safe primary signature.
        // Native.SendMessage is `public`, so per [[feedback-caller-attrs-binary-compat]] the
        // original int-int overload is retained below as a delegating binary-compat shim --
        // pre-built plugin DLLs that bound to the int signature still resolve at MEF compose.
        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        public static extern void SendMessage(System.IntPtr hWnd, int wMsg, System.IntPtr wParam, System.IntPtr lParam);

        // Binary-compat shim: original int-int signature, delegates to the IntPtr primary.
        public static void SendMessage(System.IntPtr hWnd, int wMsg, int wParam, int lParam)
        {
            SendMessage(hWnd, wMsg, new System.IntPtr(wParam), new System.IntPtr(lParam));
        }

        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(System.Windows.Forms.Keys vKey);

        [DllImport("user32.dll")]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        // 2026-05-20 Issue #10 Phase B: window-style + owner-set + reposition
        // primitives for the owned-popup reparent (see PanelGame.ReparentSwgWindow).
        // 32-bit build only -- SetWindowLong (not SetWindowLongPtr) is correct here.
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        // 2026-05-20 Issue #10 Phase B: read SWG's top-level HWND via the
        // C-linkage export added in client.cpp. CppSharp's binding generator
        // drops pointer-returning getters so getSwgHwnd() doesn't survive
        // into Generated/UtinniCore.cs -- this hand-rolled P/Invoke replaces it.
        [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "getSwgHwndExport")]
        public static extern IntPtr GetSwgHwnd();

        // Phase 3 R-C (per 03-CONTEXT D-18..D-20 / TD-18): read the
        // SWG WndProc RVA (0x00AA0970) via the C-linkage export added
        // in client.cpp. Replaces the literal `new IntPtr(0x00AA0970)`
        // that PanelGame.cs previously hardcoded -- single source of
        // truth lives in client.cpp:43. CppSharp drops pointer-returning
        // getters so Client::getSwgWndProc() doesn't survive into
        // Generated/UtinniCore.cs -- this hand-rolled P/Invoke replaces it
        // (same pattern as GetSwgHwnd / getPresentBlockedEvent).
        [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "getSwgWndProcExport")]
        public static extern IntPtr GetSwgWndProc();

        // 2026-05-19: signal the named ready event so the Launcher can restore
        // SWGEmu's PE entry bytes (originally patched to EB FE to stall the main
        // thread during injection). Called from Startup.EntryPoint right before
        // Application.Run blocks. The native side opens the event by name (the
        // Launcher created it as "Local\\UtinniReady_<pid>") and SetEvents it.
        [DllImport("UtinniCore.dll", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "utinni_signal_launcher_ready")]
        public static extern void SignalLauncherReady();
    }
}
