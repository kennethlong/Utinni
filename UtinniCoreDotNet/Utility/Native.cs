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

        // Phase 24 embed-startup mitigation. IsAdvertisedClient() reflects whether the
        // GetEngineHookPoints contract was found (the advertised client); HasClientPresented()
        // latches on the client's first present. PanelGame defers the advertised-client embed
        // reparent + its SetWindowPos-resize until the client has presented, so the embed's
        // early WM_SIZE doesn't drive the client's DX11 resize path before its render state is
        // ready. CppSharp drops these; hand-rolled P/Invoke (same pattern as GetSwgHwnd).
        [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "isAdvertisedClientExport")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool IsAdvertisedClient();

        [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "hasClientPresentedExport")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool HasClientPresented();

        // 2026-06-25 (Enter->fullscreen embed fix): push the current embed rect (screen-space) to the
        // native WndProc subclass so it clamps SWG's WM_WINDOWPOSCHANGING to the panel -- SWG's own
        // fullscreen toggle can't grow the window past the embed. Called from RepositionSwgWindow.
        [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "utinni_setEmbedClampRect")]
        public static extern void SetEmbedClampRect(int x, int y, int w, int h);

        // WS-4 Terrain reload (2026-06-26): reload the current scene's terrain on BOTH targets without going
        // through GroundScene.Get().ReloadTerrain(). On the advertised client GroundScene.Get() is nullptr by
        // design (so dormant Tier-2 editor loops stay asleep); this export reloads via the per-frame latched
        // instance instead. SWGEmu: functionally identical to the old path. No-op if no scene is loaded.
        [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "utinni_reloadCurrentTerrain")]
        public static extern void ReloadCurrentTerrain();

        // v13 free-cam (Wave 4): toggle / query free-cam on BOTH targets without GroundScene.Get()
        // (nullptr on the advertised client by design). Same latch pattern as ReloadCurrentTerrain;
        // no-op / false if no scene is loaded. toggleFreeCamera lazily installs the advertised alter detour.
        [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "utinni_toggleFreeCamera")]
        public static extern void ToggleFreeCamera();

        [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "utinni_isFreeCameraActive")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool IsFreeCameraActive();

        // Phase 24: gate calls through SWG function pointers that may be UNRESOLVED
        // on the advertised (GetEngineHookPoints) client. On that client a SWG entry
        // point absent from the catalog keeps its hardcoded SWGEmu RVA, which is
        // unmapped -- CALLing it faults (0xC0000005 EXEC). IsExecutableAddress lets a
        // caller check ONCE (e.g. at ctor time) whether the target is committed +
        // executable. On SWGEmu the address is a valid mapped function, so this
        // returns true and behaviour is unchanged.
        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, IntPtr dwLength);

        public static bool IsExecutableAddress(IntPtr addr)
        {
            if (addr == IntPtr.Zero)
                return false;

            const uint MEM_COMMIT = 0x1000;
            const uint PAGE_GUARD = 0x100;
            const uint PAGE_NOACCESS = 0x01;
            const uint PAGE_EXECUTE = 0x10, PAGE_EXECUTE_READ = 0x20,
                       PAGE_EXECUTE_READWRITE = 0x40, PAGE_EXECUTE_WRITECOPY = 0x80;

            if (VirtualQuery(addr, out MEMORY_BASIC_INFORMATION mbi,
                    (IntPtr)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) == IntPtr.Zero)
                return false;
            if (mbi.State != MEM_COMMIT)
                return false;
            if ((mbi.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0)
                return false;

            uint prot = mbi.Protect & 0xFF;
            return prot == PAGE_EXECUTE || prot == PAGE_EXECUTE_READ ||
                   prot == PAGE_EXECUTE_READWRITE || prot == PAGE_EXECUTE_WRITECOPY;
        }

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
