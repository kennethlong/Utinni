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
    /// WR-01 real function-pointer reseat harness for Network::cast (CR-02/CR-03 regression guard).
    ///
    /// utinni_test_networkCast reseats swg::network::cast to a naked-asm test double
    /// (testCastDoubleNaked) that writes the 64-bit sentinel 0xDEADBEEFCAFEBABE through
    /// the int64_t* OUT param (ECX in __thiscall ABI). Network::cast in network.cpp is
    /// invoked through the real call chain — this is NOT a hardcoded return value.
    ///
    /// Regression coverage:
    ///   - CR-02 regression (OUT param narrowed back to swgptr*/int32_t*): the upper 32 bits
    ///     of the sentinel (0xDEADBEEF) would be silently dropped — Assert.Equal below fails.
    ///   - CR-03 regression (UB shift re-introduced): the low/high split in Network::cast would
    ///     corrupt the sentinel high half — Assert.Equal below fails.
    ///
    /// Full live-SWG proof (real cast at RVA 0xAA4900) remains Tier-4 manual per D-05.
    /// The full 64-bit sentinel assertion is stronger than the previous 0xDEADBEEF stub.
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
        public void NetworkCast_WithTestDouble_ReturnsFullSentinel()
        {
            // Arrange: any id — the test double ignores the input and writes the sentinel
            // through the int64_t* OUT param (ECX in __thiscall ABI).
            const int syntheticId = 0x12345678;

            // Act: utinni_test_networkCast calls Network::cast via the reseated pointer.
            // If UtinniCore.dll is absent in the test output directory, this throws
            // DllNotFoundException — the CI build's CopyNativeArtifactsForTests target
            // ensures the DLL is present.
            long result = NativeBridge.Utinni_TestNetworkCast(syntheticId);

            // Assert: the full 64-bit sentinel must survive the round-trip through the
            // int64_t* OUT param. The upper 32 bits (0xDEADBEEF) are the regression
            // discriminator — a 4-byte slot would silently truncate them to 0.
            Assert.Equal(unchecked((long)0xDEADBEEFCAFEBABEL), result);
        }
    }
}
