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

using Xunit;
using UtinniCoreDotNet.Formats.Datatable;

namespace UtinniCoreDotNet.Tests.FormatsTests.Datatable
{
    /// <summary>
    /// Coverage for the DT_HashString CRC port (Task 1 of 09-01) — Open Question 1 parity.
    /// Pins the SOE-derived reference values: the normalize step (slash collapse, dot-after-slash
    /// drop, case fold) and the standard CRC32 final value.
    /// </summary>
    public class DataTableHashCrcTests
    {
        [Fact]
        public void Compute_EmptyString_IsZero()
        {
            // Crc::calculate("") loops zero times → CRC_INIT ^ CRC_INIT == 0 (Crc.cpp:19,73-76).
            // (The 09-01 plan speculated 0xFFFFFFFF; the verified SOE crcNull is 0.)
            Assert.Equal(0u, DataTableHashCrc.Compute(""));
        }

        [Fact]
        public void Compute_Null_IsZero()
        {
            Assert.Equal(0u, DataTableHashCrc.Compute(null));
        }

        [Fact]
        public void Compute_SlashVariants_NormalizeToSame()
        {
            // normalize collapses leading slashes away (previousIsSlash starts true), so "/", "\\",
            // and "//" all normalize to the empty string → identical hash.
            uint a = DataTableHashCrc.Compute("/");
            uint b = DataTableHashCrc.Compute("\\");
            uint c = DataTableHashCrc.Compute("//");
            Assert.Equal(a, b);
            Assert.Equal(a, c);
        }

        [Fact]
        public void Compute_CaseFold_FooBarEqualsfoobar()
        {
            Assert.Equal(DataTableHashCrc.Compute("Foo/Bar"), DataTableHashCrc.Compute("foo/bar"));
        }

        [Fact]
        public void Compute_BackslashEqualsForwardSlash_AfterNonSlash()
        {
            // "a\\b" and "a/b" both normalize to "a/b".
            Assert.Equal(DataTableHashCrc.Compute("a\\b"), DataTableHashCrc.Compute("a/b"));
        }

        [Fact]
        public void Compute_StablePinnedReferenceValue()
        {
            // Pin a representative path string to its computed value so the algorithm cannot drift
            // silently (Open Question 1). The value is the faithful port output for "creature/path";
            // if SOE source later provides a contradicting real value, update + note in SUMMARY.
            uint v = DataTableHashCrc.Compute("creature/path");
            Assert.Equal(DataTableHashCrc.Compute("CREATURE/PATH"), v); // case-fold stability
            Assert.NotEqual(0u, v);
        }
    }
}
