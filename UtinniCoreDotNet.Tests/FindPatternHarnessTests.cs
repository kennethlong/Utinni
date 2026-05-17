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
    /// C-11 harness tests for directX9::getVtbl null-check fix.
    /// Tests 1+2: memory::findPattern wrapper via utinni_findPattern — absent and present patterns.
    /// Test 3: utinni_getVtbl in test process where d3d9.dll is not loaded — must return 0.
    /// CON-N-04 preserved: memory::copy (VirtualProtect bracket) is NOT touched by the C-11 fix.
    /// </summary>
    public class FindPatternHarnessTests
    {
        private static class NativeBridge
        {
            // utinni_findPattern(buffer, bufferLen, pattern, mask) -> uintptr_t
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_findPattern")]
            public static extern UIntPtr Utinni_FindPattern(
                IntPtr buffer,
                UIntPtr bufferLen,
                [MarshalAs(UnmanagedType.LPStr)] string pattern,
                [MarshalAs(UnmanagedType.LPStr)] string mask);

            // utinni_getVtbl() -> uintptr_t (0 when d3d9.dll not loaded)
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_getVtbl")]
            public static extern UIntPtr Utinni_GetVtbl();
        }

        [Fact]
        public void FindPattern_PatternAbsent_ReturnsZero()
        {
            // Arrange: a zero-filled buffer of 0x1000 bytes; pattern is 4 bytes that won't match
            byte[] buf = new byte[0x1000]; // zero-filled
            GCHandle handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try
            {
                // Act: pattern "\xDE\xAD\xBE\xEF" is absent in a zero buffer
                UIntPtr result = NativeBridge.Utinni_FindPattern(
                    handle.AddrOfPinnedObject(),
                    (UIntPtr)buf.Length,
                    "\xDE\xAD\xBE\xEF",
                    "xxxx");

                // Assert
                Assert.Equal(UIntPtr.Zero, result);
            }
            finally
            {
                handle.Free();
            }
        }

        [Fact]
        public void FindPattern_PatternPresent_ReturnsNonZero()
        {
            // Arrange: buffer with a known pattern at offset 64
            byte[] buf = new byte[0x1000];
            buf[64] = 0xDE;
            buf[65] = 0xAD;
            buf[66] = 0xBE;
            buf[67] = 0xEF;

            GCHandle handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try
            {
                // Act
                UIntPtr result = NativeBridge.Utinni_FindPattern(
                    handle.AddrOfPinnedObject(),
                    (UIntPtr)buf.Length,
                    "\xDE\xAD\xBE\xEF",
                    "xxxx");

                // Assert: result should be non-zero (points into the buffer)
                Assert.NotEqual(UIntPtr.Zero, result);
            }
            finally
            {
                handle.Free();
            }
        }

        [Fact]
        public void GetVtbl_WithoutD3d9Loaded_ReturnsNull()
        {
            // In the dotnet test process d3d9.dll is not loaded.
            // The C-11 fix adds a GetModuleHandle null-check; getVtbl returns nullptr => 0.
            UIntPtr result = NativeBridge.Utinni_GetVtbl();
            Assert.Equal(UIntPtr.Zero, result);
        }
    }
}
