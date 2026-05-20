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

using System.Runtime.InteropServices;
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    /// <summary>
    /// Export-decoration harness: asserts every documented C-linkage export in
    /// UtinniCore.dll resolves via GetProcAddress using its undecorated name.
    ///
    /// Background: C-01 live UAT (2026-05-18) caught that utinni_init was exported
    /// as _utinni_init@4 (decorated x86 stdcall) while the Launcher's GetProcAddress
    /// used the undecorated name "utinni_init". The fix in utinni.cpp adds a
    /// /EXPORT linker alias. This test would have caught that gap before production.
    ///
    /// If a future code change re-introduces a __stdcall/__WINAPI export WITHOUT a
    /// matching /EXPORT alias, utinni_test_resolveExports returns a count less than
    /// the expected value and this test fails — closing the harness-decoration gap
    /// that Plan 02-03's LoaderLockHarness missed.
    ///
    /// Expected: 13 exports resolve (as of 2026-05-19).
    /// </summary>
    public class ExportResolutionTests
    {
        private static class NativeBridge
        {
            // utinni_test_resolveExports() -> int (count of exports resolved via
            // undecorated name; -1 if GetModuleHandleA("UtinniCore.dll") fails)
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_resolveExports")]
            public static extern int Utinni_TestResolveExports();
        }

        /// <summary>
        /// Expected number of C-linkage exports in UtinniCore.dll.
        /// Update this constant whenever a new export is added or removed.
        /// List:
        ///   utinni_init, utinni_findPattern, utinni_getVtbl,
        ///   utinni_test_freeConfigBuffer, utinni_test_networkCast,
        ///   utinni_clr_stop, utinni_triggerInstallCallbacks,
        ///   utinni_test_initPresentBlockedEvent, utinni_test_getPresentBlockedEvent,
        ///   utinni_test_initDepthTexture, utinni_test_getDepthTexturePtr,
        ///   getPresentBlockedEvent,
        ///   utinni_signal_launcher_ready (added 2026-05-19 for signal-event sync)
        /// </summary>
        private const int ExpectedExportCount = 13;

        [Fact]
        public void ResolveExports_AllDocumentedCLinkageExports_ResolveByUndecoratedName()
        {
            // Act: the export opens GetModuleHandleA("UtinniCore.dll") from inside the DLL
            // and calls GetProcAddress for each documented undecorated export name.
            int resolved = NativeBridge.Utinni_TestResolveExports();

            // Assert: -1 means GetModuleHandleA("UtinniCore.dll") itself failed — unexpected.
            Assert.NotEqual(-1, resolved);

            // Assert: all expected exports resolve. If any export is missing (e.g. a new
            // __stdcall export without a /EXPORT alias), this assertion fails with the
            // actual count so the developer can identify which export is broken.
            Assert.Equal(ExpectedExportCount, resolved);
        }
    }
}
