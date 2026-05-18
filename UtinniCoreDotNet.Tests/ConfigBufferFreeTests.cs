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
using Xunit;

namespace UtinniCoreDotNet.Tests
{
    /// <summary>
    /// WR-02 cross-heap harness: asserts the C-02 cross-CRT delete[] was removed from
    /// hkLoadOverrideConfig. Allocates a buffer on the OS heap (HeapAlloc via
    /// Marshal.AllocHGlobal) and passes it to utinni_test_freeConfigBuffer.
    ///
    /// PARTIAL-PROOF note (Concern A, 02.1-PLAN-CHECK.md §Concern A):
    ///   UtinniCore.vcxproj Release|Win32 has no explicit RuntimeLibrary entry —
    ///   the effective CRT is /MT (static). Modern MSVC v142 /MT routes new[]/delete[]
    ///   through _malloc_base/_free_base -> RtlHeapAlloc -> GetProcessHeap().
    ///   Marshal.AllocHGlobal also uses GetProcessHeap(). Because both heaps resolve
    ///   to the SAME process heap in Release/MT, calling delete[] on a Marshal.AllocHGlobal
    ///   pointer in Release would NOT crash — it would free the pointer correctly via the
    ///   shared process heap. This makes the test a partial-proof: it exercises the no-op
    ///   export path and is better than the old GCHandle.Pinned stub, but cannot detect a
    ///   Release-build cross-heap free without a separate /MD fixture.
    ///
    ///   Follow-up: Phase 6 STAB-03 should add a Utinni.CrossCrtFreeFixture vcxproj with
    ///   /MD linkage (distinct CRT heap) to fully close WR-02 in Release.
    ///
    ///   In Debug builds (/MTd), delete[] on a GetProcessHeap() pointer DOES crash:
    ///   _ASSERTE(_BLOCK_TYPE_IS_VALID(pHead->nBlockUse)) fires immediately.
    /// </summary>
    public class ConfigBufferFreeTests
    {
        private static class NativeBridge
        {
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_freeConfigBuffer")]
            public static extern bool Utinni_TestFreeConfigBuffer(IntPtr data, IntPtr pFile);
        }

        /// <summary>
        /// WR-02 real cross-heap harness: allocates a buffer on the OS heap via
        /// Marshal.AllocHGlobal (HeapAlloc). Passes it to utinni_test_freeConfigBuffer.
        /// Post-C-02 fix: the export is a no-op (no delete[] call) -> no crash.
        /// Regression: if delete[] is re-introduced in hkLoadOverrideConfig and the
        /// test export exercises that path, CRT free() on a HeapAlloc pointer causes
        /// heap corruption in Debug, which Record.Exception catches as a crash/AV.
        /// See PARTIAL-PROOF note on class summary for Release/MT behaviour.
        /// </summary>
        [Fact]
        public void FreeConfigBuffer_WithOsHeapBuffer_DoesNotCrash()
        {
            // Arrange: OS heap pointer (HeapAlloc via Marshal.AllocHGlobal).
            // This is distinct from UtinniCore's CRT heap in Debug (/MTd) builds.
            // In Release (/MT) both resolve to GetProcessHeap() — see PARTIAL-PROOF note.
            IntPtr osHeapBuffer = Marshal.AllocHGlobal(16);
            try
            {
                Exception ex = Record.Exception(() =>
                {
                    // Act: call the (now no-op) export with an OS-heap pointer.
                    // If delete[] were re-introduced, this would crash in Debug.
                    bool result = NativeBridge.Utinni_TestFreeConfigBuffer(osHeapBuffer, osHeapBuffer);
                    Assert.True(result);
                });

                // Assert: no crash occurred — the C-02 fix (no delete[]) is intact.
                Assert.Null(ex);
            }
            finally
            {
                // Always free via the correct owner (OS heap).
                Marshal.FreeHGlobal(osHeapBuffer);
            }
        }
    }
}
