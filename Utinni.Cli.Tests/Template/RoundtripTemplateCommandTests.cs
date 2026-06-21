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
// Implementation original to Utinni under MIT.
//
// RED-via-Skip (Wave 0, plan 23-01): verb-level byte-exact gate + D-14 worked-example goldens. The
// worked-example fixtures double as the DEC-C3 byte-exact gate before /gsd:verify-work. Method names
// embed the VALIDATION.md --filter tokens (Template, Roundtrip, WorkedExample).

using Xunit;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>
    /// Verb-level (utinni-cli) decode-&gt;encode byte-exact goldens for the template path, including the
    /// D-14 worked-example chunk templates. Filter tokens: Template, Roundtrip, WorkedExample. All
    /// RED-via-Skip pending the Wave 1/2 engine + the shipped worked-example template pack.
    /// </summary>
    public sealed class RoundtripTemplateCommandTests
    {
        private const string RedVerb = "RED — Wave 1/2 wires the roundtrip-template verb; tracking PROD-IFFT-02";
        private const string RedWorked = "RED — Wave 2 ships D-14 worked-example templates; tracking PROD-IFFT-03 (doubles as DEC-C3 byte-exact gate)";

        // ── verb-level byte-exact (PROD-IFFT-02) — filter "Template&Roundtrip" ──

        [Theory(Skip = RedVerb)]
        [Trait("Category", "TemplateRoundtrip")]
        [InlineData(TemplateTestFixtures.VersionLow)]
        [InlineData(TemplateTestFixtures.VersionHigh)]
        public void Template_Roundtrip_VerbDecodeEncodeFixture_BytesIdentical(string version)
        {
            // roundtrip-template against BuildKern(version): exit 0 + bytesIdentical true.
            Assert.True(false, "pending Wave 1/2 roundtrip-template verb");
        }

        // ── D-14 worked-example goldens (PROD-IFFT-03) — filter "Template&WorkedExample&Roundtrip" ──

        [Theory(Skip = RedWorked)]
        [Trait("Category", "TemplateWorkedExampleRoundtrip")]
        [InlineData(TemplateTestFixtures.VersionLow)]
        [InlineData(TemplateTestFixtures.VersionHigh)]
        public void Template_WorkedExample_Roundtrip_ShippedTemplatePack_BytesIdentical(string version)
        {
            // The D-14 worked-example template applied to its fixture chunk round-trips byte-exact at
            // both the low and high version FORM (where layouts diverge).
            Assert.True(false, "pending Wave 2 D-14 worked-example template pack");
        }
    }
}
