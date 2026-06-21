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
// RED-via-Skip (Wave 0, plan 23-01): every test that targets the unbuilt KernelCodec engine uses
// [Fact(Skip=...)] / [Theory(Skip=...)] with a tracking note so the suite is GREEN-with-skips, never
// red-broken. Wave 1/2 removes the Skip as it implements each behavior. Test-METHOD names embed the
// VALIDATION.md --filter tokens (methodDisplay=method makes the method name the VSTest DisplayName);
// the trait Category mirrors the token set for trait-based selection too.

using Xunit;

namespace Utinni.Cli.Tests.Template
{
    /// <summary>
    /// Per-type kernel decode/encode goldens + the CRITICAL count-from-prior grow/shrink round-trip
    /// (DEC-C3) + array / preset / version-match / precedence goldens. Filter tokens live in the
    /// method names: Template, KernelCodec, CountRecompute, Roundtrip, Array, Preset, VersionMatch,
    /// Precedence. All RED-via-Skip pending the Wave 1/2 engine.
    /// </summary>
    public sealed class KernelCodecTests
    {
        private const string RedKernel = "RED — Wave 1 implements KernelCodec; tracking PROD-IFFT-01";
        private const string RedCount = "RED — Wave 1/2 implements count-from-prior recompute; tracking PROD-IFFT-02 (DEC-C3 CRITICAL)";
        private const string RedArray = "RED — Wave 1 implements array repeat kinds; tracking PROD-IFFT-02";
        private const string RedPreset = "RED — Wave 1 implements D-09 presets; tracking PROD-IFFT-01";
        private const string RedVersion = "RED — Wave 2 implements version-FORM-aware match; tracking PROD-IFFT-02";
        private const string RedPrecedence = "RED — Wave 2 implements D-05 altitude/precedence; tracking PROD-IFFT-02";

        // ── per-type decode/encode (PROD-IFFT-01) — filter "Template&KernelCodec" ──

        [Theory(Skip = RedKernel)]
        [Trait("Category", "TemplateKernelCodec")]
        [InlineData(TemplateTestFixtures.VersionLow)]
        [InlineData(TemplateTestFixtures.VersionHigh)]
        public void Template_KernelCodec_EveryScalarType_DecodesToExpectedValue_ReEncodesByteIdentical(string version)
        {
            // Decode TemplateTestFixtures.BuildKern(version) -> assert each field value; re-encode byte-exact.
            Assert.True(false, "pending Wave 1 KernelCodec.Decode/Encode");
        }

        // ── CRITICAL count-from-prior grow/shrink round-trip (PROD-IFFT-02, DEC-C3) ──
        // filter "Template&CountRecompute&Roundtrip"

        [Fact(Skip = RedCount)]
        [Trait("Category", "TemplateCountRecomputeRoundtrip")]
        public void Template_CountRecompute_Roundtrip_AddElement_CountFieldReadsNPlus1_PayloadByteExact()
        {
            // Decode CountFromPrior(n=3) -> add one element -> encode -> count field on-wire reads 4 AND
            // the whole payload equals CountFromPrior(n=4) byte-for-byte.
            Assert.True(false, "pending Wave 1/2 count-from-prior recompute (DEC-C3 grow)");
        }

        [Fact(Skip = RedCount)]
        [Trait("Category", "TemplateCountRecomputeRoundtrip")]
        public void Template_CountRecompute_Roundtrip_RemoveElement_CountFieldReadsNMinus1_PayloadByteExact()
        {
            // Decode CountFromPrior(n=3) -> remove one element -> encode -> count field on-wire reads 2 AND
            // the whole payload equals CountFromPrior(n=2) byte-for-byte.
            Assert.True(false, "pending Wave 1/2 count-from-prior recompute (DEC-C3 shrink)");
        }

        // ── trailing-remainder + fixed-count arrays (PROD-IFFT-02) — filter "Template&Array&Roundtrip" ──

        [Theory(Skip = RedArray)]
        [Trait("Category", "TemplateArrayRoundtrip")]
        [InlineData(TemplateTestFixtures.VersionLow)]
        [InlineData(TemplateTestFixtures.VersionHigh)]
        public void Template_Array_Roundtrip_FixedCountAndUntilEnd_ByteExact(string version)
        {
            // Decode BuildArrays(version) -> re-encode -> byte-exact (FixedCount[3] + UntilEnd remainder).
            Assert.True(false, "pending Wave 1 array repeat kinds");
        }

        // ── D-09 presets (PROD-IFFT-01) — filter "Template&Preset" ──

        [Fact(Skip = RedPreset)]
        [Trait("Category", "TemplatePreset")]
        public void Template_Preset_VectorQuaternionMatrixColor_DecodeToExactValues()
        {
            // Decode BuildPresets -> vector(1,2,3); quaternion w-FIRST(1,0,0,0); matrix 3x4 row-major
            // (0..11); color PackedRgb(255,128,0). Re-encode byte-exact.
            Assert.True(false, "pending Wave 1 D-09 presets");
        }

        // ── version-FORM divergence (PROD-IFFT-02) — filter "Template&VersionMatch" ──

        [Theory(Skip = RedVersion)]
        [Trait("Category", "TemplateVersionMatch")]
        [InlineData(TemplateTestFixtures.VersionLow)]
        [InlineData(TemplateTestFixtures.VersionHigh)]
        public void Template_VersionMatch_DivergentCpapLayout_PicksRightLayoutPerVersionForm(string version)
        {
            // BuildVersionDivergent(version): low layout = 1 trailing float, high = 5; the version-aware
            // template selects the matching layout and round-trips byte-exact.
            Assert.True(false, "pending Wave 2 version-FORM-aware match");
        }

        // ── D-05 altitude / precedence (PROD-IFFT-02) — filter "Template&Precedence" ──

        [Fact(Skip = RedPrecedence)]
        [Trait("Category", "TemplatePrecedence")]
        public void Template_Precedence_BuiltinRootForm_TemplateNeverEngages()
        {
            // BuildBuiltinRootFile (root FORM sub-type owned by a built-in): the resolver must NOT engage
            // any user template on its leaves (D-05 altitude).
            Assert.True(false, "pending Wave 2 D-05 altitude/precedence");
        }
    }
}
