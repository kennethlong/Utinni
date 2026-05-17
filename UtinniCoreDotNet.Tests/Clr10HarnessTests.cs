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
    /// C-10 harness: asserts clr::stop() is idempotent and null-safe. The first call
    /// hits the path where pClr* pointers are likely null (CLR not started in test process);
    /// the second call was the AV site pre-fix. Both must complete without exception.
    /// </summary>
    public class Clr10HarnessTests
    {
        private static class NativeBridge
        {
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_clr_stop")]
            public static extern void Utinni_ClrStop();
        }

        [Fact]
        public void ClrStop_CalledTwiceConsecutively_NoAccessViolation()
        {
            // The first call: pClr* pointers are null (CLR not started in dotnet test process)
            // The second call: the C-10 fix ensures the null-check prevents AV
            Exception ex = Record.Exception(() =>
            {
                NativeBridge.Utinni_ClrStop();
                NativeBridge.Utinni_ClrStop();
            });

            Assert.Null(ex);
        }
    }
}
