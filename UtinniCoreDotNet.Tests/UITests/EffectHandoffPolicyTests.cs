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
    /// PROD-W2-CFX-01 (22-04) TRE Browser → Effects (ClientEffect) Editor hand-off gating. The
    /// framework-leg testable seam mirroring <see cref="ParticleHandoffPolicy"/> /
    /// <see cref="OtHandoffPolicy"/>: a cheap extension visibility gate (<c>.cef</c>) + a content sniff
    /// (<c>FORM CLEF</c>) that NEVER throws on malformed/truncated bytes. Regression cover for the
    /// 22-04 live-smoke gap where the TRE Browser offered no "Open in Effects Editor" entry at all. Pure
    /// framework helper — no WinForms, no TheJawaToolboxDotNet reference.
    /// </summary>
    public class EffectHandoffPolicyTests
    {
        // ── Test 1: ShouldOfferEffectsEditor extension gate ───────────────────────
        [Theory]
        [InlineData("foo.cef", false, true)]
        [InlineData("clienteffect/combat_cr_spit_impact.cef", false, true)]
        [InlineData("CLIENTEFFECT/COMBAT_CR_SPIT_IMPACT.CEF", false, true)] // case-insensitive
        [InlineData("clienteffect\\combat_cr_spit_impact.cef", false, true)] // backslash separators
        // non-.cef extensions are never offered (use Open in IFF Editor, etc.).
        [InlineData("appearance/mesh.iff", false, false)]
        [InlineData("readme.txt", false, false)]
        [InlineData("combat_cr_spit_impact", false, false)]
        // enumerate-only (encrypted) payloads can't be opened → never offered.
        [InlineData("foo.cef", true, false)]
        // null / empty.
        [InlineData("", false, false)]
        [InlineData(null, false, false)]
        public void ShouldOfferEffectsEditor_GatesOnExtensionAndResolvability(
            string logicalPath, bool enumerateOnly, bool expected)
        {
            Assert.Equal(expected, EffectHandoffPolicy.ShouldOfferEffectsEditor(logicalPath, enumerateOnly));
        }

        // ── Test 2: IsClientEffectPayload content sniff ───────────────────────────
        [Fact(DisplayName = "IsClientEffectPayload: a FORM CLEF header is recognized as a client effect")]
        public void IsClientEffectPayload_TrueForFormClefHeader()
        {
            // FORM | big-endian length | CLEF  (bytes[0..4]=="FORM", bytes[8..12]=="CLEF")
            byte[] payload = { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0, 0, 0, 8, (byte)'C', (byte)'L', (byte)'E', (byte)'F' };
            Assert.True(EffectHandoffPolicy.IsClientEffectPayload(payload));
        }

        [Fact(DisplayName = "IsClientEffectPayload: a non-CLEF FORM (e.g. another asset) is rejected")]
        public void IsClientEffectPayload_FalseForNonClefForm()
        {
            byte[] payload = { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0, 0, 0, 8, (byte)'P', (byte)'E', (byte)'F', (byte)'T' };
            Assert.False(EffectHandoffPolicy.IsClientEffectPayload(payload));
        }

        [Fact(DisplayName = "IsClientEffectPayload: a non-FORM top-level chunk is rejected")]
        public void IsClientEffectPayload_FalseWhenNotForm()
        {
            byte[] payload = { (byte)'X', (byte)'X', (byte)'X', (byte)'X', 0, 0, 0, 8, (byte)'C', (byte)'L', (byte)'E', (byte)'F' };
            Assert.False(EffectHandoffPolicy.IsClientEffectPayload(payload));
        }

        // ── Test 3: IsClientEffectPayload never throws on malformed/truncated bytes ─
        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(11)]
        public void IsClientEffectPayload_FalseForShortOrNullPayload_NeverThrows(int length)
        {
            Assert.False(EffectHandoffPolicy.IsClientEffectPayload(new byte[length]));
            Assert.False(EffectHandoffPolicy.IsClientEffectPayload(null));
        }

        [Fact(DisplayName = "IsClientEffectPayload: garbage / truncated bytes never throw, return false")]
        public void IsClientEffectPayload_FalseOnGarbage_NeverThrows()
        {
            byte[] truncated = { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0x7F, 0x7F, 0x7F, 0x7F, (byte)'C', (byte)'L', (byte)'E' };
            Assert.False(EffectHandoffPolicy.IsClientEffectPayload(truncated));

            byte[] garbage = { 0x00, 0xFF, 0x10, 0x42, 0x99, 0x01, 0x02, 0x03 };
            Assert.False(EffectHandoffPolicy.IsClientEffectPayload(garbage));
        }
    }
}
