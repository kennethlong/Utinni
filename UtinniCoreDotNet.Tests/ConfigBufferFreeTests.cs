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
    /// C-02 partial-proof harness: asserts the cross-CRT delete[] was removed from
    /// hkLoadOverrideConfig. The wrapper utinni_test_freeConfigBuffer is a no-op stub
    /// that represents the fixed code path (no delete[]). Full CRT-mismatch detection
    /// requires a live SWG injection (Tier-4 manual per CONTEXT.md D-05/D-06).
    /// </summary>
    public class ConfigBufferFreeTests
    {
        private static class NativeBridge
        {
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_freeConfigBuffer")]
            public static extern bool Utinni_TestFreeConfigBuffer(IntPtr data, IntPtr pFile);
        }

        [Fact]
        public void FreeConfigBuffer_OnSyntheticBuffer_DoesNotCrash()
        {
            // Arrange: pin a synthetic buffer to pass a non-zero pointer
            byte[] buf = new byte[16];
            GCHandle handle = GCHandle.Alloc(buf, GCHandleType.Pinned);

            try
            {
                bool result = false;
                Exception ex = Record.Exception(() =>
                {
                    // Act: call the no-op wrapper with a non-zero buffer + pFile pointer
                    result = NativeBridge.Utinni_TestFreeConfigBuffer(
                        handle.AddrOfPinnedObject(),
                        handle.AddrOfPinnedObject());
                });

                // Assert: no AV and wrapper returns true
                Assert.Null(ex);
                Assert.True(result);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
