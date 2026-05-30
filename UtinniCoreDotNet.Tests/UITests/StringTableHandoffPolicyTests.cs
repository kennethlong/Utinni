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

using UtinniCoreDotNet.UI;
using Xunit;

namespace UtinniCoreDotNet.Tests.UITests
{
    /// <summary>
    /// Coverage for <see cref="StringTableHandoffPolicy.ShouldOfferStringTableEditor"/> (Plan 10-05 D-04).
    /// Mirrors the Phase 9 <c>DatatableHandoffPolicyTests</c> shape, simplified for the flat <c>.stf</c>
    /// (no datatables/ path rule): extension <c>.stf</c> OR the 0xABCD magic sniff, AND not enumerate-only.
    /// </summary>
    public class StringTableHandoffPolicyTests
    {
        // A minimal payload that passes StringTableDecoder.LooksLikeStf: 0xABCD LE magic = CD AB 00 00.
        private static byte[] StfMagicPayload()
        {
            return new byte[] { 0xCD, 0xAB, 0x00, 0x00, 0x01 };
        }

        [Fact]
        public void StfExtension_Offered()
        {
            Assert.True(StringTableHandoffPolicy.ShouldOfferStringTableEditor("string/en/ui.stf", null, false));
        }

        [Fact]
        public void StfExtension_CaseInsensitive_Offered()
        {
            Assert.True(StringTableHandoffPolicy.ShouldOfferStringTableEditor("string/en/UI.STF", null, false));
        }

        [Fact]
        public void StfExtension_OfferedEvenWhenPayloadNull_F11()
        {
            // The live menu gate has no payload at Opening time — extension alone must offer the item.
            Assert.True(StringTableHandoffPolicy.ShouldOfferStringTableEditor("a/b/c.stf", null, false));
        }

        [Fact]
        public void MagicSniff_ExtensionlessPayload_Offered()
        {
            // No .stf extension, but the payload's 0xABCD magic sniffs as a string table (secondary gate).
            Assert.True(StringTableHandoffPolicy.ShouldOfferStringTableEditor("string/en/ui", StfMagicPayload(), false));
        }

        [Fact]
        public void EnumerateOnly_NeverOffered()
        {
            Assert.False(StringTableHandoffPolicy.ShouldOfferStringTableEditor("string/en/ui.stf", StfMagicPayload(), true));
        }

        [Fact]
        public void NonStfNonMagic_NotOffered()
        {
            Assert.False(StringTableHandoffPolicy.ShouldOfferStringTableEditor("datatables/mob/creatures.iff", null, false));
        }

        [Fact]
        public void NonStfExtensionWithNonMagicPayload_NotOffered()
        {
            byte[] notStf = new byte[] { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0, 0, 0, 4 };
            Assert.False(StringTableHandoffPolicy.ShouldOfferStringTableEditor("foo.iff", notStf, false));
        }

        [Fact]
        public void NullPath_NullPayload_NotOffered()
        {
            Assert.False(StringTableHandoffPolicy.ShouldOfferStringTableEditor(null, null, false));
        }

        [Fact]
        public void EmptyPath_NullPayload_NotOffered()
        {
            Assert.False(StringTableHandoffPolicy.ShouldOfferStringTableEditor("", null, false));
        }
    }
}
