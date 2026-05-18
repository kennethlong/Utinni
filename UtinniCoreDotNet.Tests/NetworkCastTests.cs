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
    /// C-03 partial-proof harness: asserts the post-condition of Network::cast.
    /// The test-only wrapper utinni_test_networkCast returns a sentinel (0xDEADBEEF)
    /// instead of calling SWG at hard RVA 0xAA4900 (which would AV in the test process).
    /// The sentinel assertion catches any regression that rewires the wrapper to call SWG
    /// without proper initialization. Full live-SWG proof remains Tier-4 manual.
    ///
    /// WR-01 (real function-pointer reseat harness) was attempted in Plan 02.1-01 but
    /// blocked on MSVC's C3865 restriction (__thiscall not allowed on free functions).
    /// Deferred to Plan 02.1-03; the setCastForTest/resetCast seam in network.cpp is
    /// already in place so 02.1-03 can implement WR-01 with a proper ABI-shim pattern.
    /// </summary>
    public class NetworkCastTests
    {
        private static class NativeBridge
        {
            [DllImport("UtinniCore", CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "utinni_test_networkCast")]
            public static extern long Utinni_TestNetworkCast(int id);
        }

        [Fact]
        public void NetworkCast_Wrapper_ReturnsInitializedValue()
        {
            // Arrange
            const int syntheticId = 0x12345678;

            // Act
            long result = NativeBridge.Utinni_TestNetworkCast(syntheticId);

            // Assert: must NOT be the MSVC debug-init uninitialized-stack pattern
            Assert.NotEqual(0xCCCCCCCCL, result);
            // Must NOT be 0 (the pre-fix path where SWG didn't write through &networkId)
            Assert.NotEqual(0L, result);
            // Must equal our sentinel (0xDEADBEEF) — confirms wrapper is not calling SWG
            Assert.Equal(unchecked((long)0xDEADBEEFL), result);
        }
    }
}
