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
    /// WR-01 real harness for CR-02/CR-03: function-pointer reseat test double.
    /// utinni_test_networkCast reseats swg::network::cast to a test double that writes
    /// the 64-bit sentinel 0xDEADBEEFCAFEBABE through the OUT param, then calls
    /// Network::cast and restores the real pointer.
    ///
    /// The full 64-bit sentinel round-trips only if the OUT param is declared as
    /// int64_t* (8-byte slot). If CR-02 is reverted (swgptr* 4-byte slot), the high
    /// 32 bits of the sentinel are lost and this assertion fails — the test would
    /// catch the regression.
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
            // Arrange: pass a 32-bit id; the test double writes 0xDEADBEEFCAFEBABE
            // through the OUT param. The full 64-bit value round-trips only if
            // the OUT param is declared as int64_t* (8-byte slot, not swgptr* 4-byte).
            const int syntheticId = 0x12345678;

            // Act
            long result = NativeBridge.Utinni_TestNetworkCast(syntheticId);

            // Assert: full 64-bit sentinel must survive.
            // If CR-02 is reverted (OUT param back to swgptr* / 4 bytes), the high
            // 32 bits of the sentinel are lost and this assertion fails.
            Assert.Equal(unchecked((long)0xDEADBEEFCAFEBABEL), result);
        }
    }
}
