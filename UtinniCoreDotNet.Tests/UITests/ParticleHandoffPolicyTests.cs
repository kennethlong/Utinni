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
    /// PROD-W2-PRT (15-06) TRE Browser → Particle Editor hand-off gating. The framework-leg testable
    /// seam mirroring <see cref="OtHandoffPolicy"/> / <see cref="DatatableHandoffPolicy"/>: a cheap
    /// extension visibility gate (<c>.prt</c>) + a click-time content sniff (<c>FORM PEFT</c>) that
    /// NEVER throws on malformed/truncated bytes. The TRE Browser hand-off is HIDDEN when the sniff
    /// fails (UI-SPEC §Host Placement Particle). Pure framework helper — no WinForms, no
    /// TheJawaToolboxDotNet reference.
    /// </summary>
    public class ParticleHandoffPolicyTests
    {
        // ── Test 1: ShouldOfferParticleEditor extension gate ──────────────────────
        [Theory]
        [InlineData("foo.prt", false, true)]
        [InlineData("appearance/pt_fire.prt", false, true)]
        [InlineData("APPEARANCE/PT_FIRE.PRT", false, true)] // case-insensitive
        [InlineData("appearance\\pt_fire.prt", false, true)] // backslash separators
        // non-.prt extensions are never offered (use Open in IFF Editor, etc.).
        [InlineData("appearance/mesh.iff", false, false)]
        [InlineData("readme.txt", false, false)]
        [InlineData("pt_fire", false, false)]
        // enumerate-only (encrypted) payloads can't be opened → never offered.
        [InlineData("foo.prt", true, false)]
        // null / empty.
        [InlineData("", false, false)]
        [InlineData(null, false, false)]
        public void ShouldOfferParticleEditor_GatesOnExtensionAndResolvability(
            string logicalPath, bool enumerateOnly, bool expected)
        {
            Assert.Equal(expected, ParticleHandoffPolicy.ShouldOfferParticleEditor(logicalPath, enumerateOnly));
        }

        // ── Test 2: IsParticlePayload content sniff ───────────────────────────────
        [Fact(DisplayName = "IsParticlePayload: a FORM PEFT header is recognized as a particle effect")]
        public void IsParticlePayload_TrueForFormPeftHeader()
        {
            // FORM | big-endian length | PEFT  (bytes[0..4]=="FORM", bytes[8..12]=="PEFT")
            byte[] payload = { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0, 0, 0, 8, (byte)'P', (byte)'E', (byte)'F', (byte)'T' };
            Assert.True(ParticleHandoffPolicy.IsParticlePayload(payload));
        }

        [Fact(DisplayName = "IsParticlePayload: a non-PEFT FORM (e.g. another asset) is rejected")]
        public void IsParticlePayload_FalseForNonPeftForm()
        {
            byte[] payload = { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0, 0, 0, 8, (byte)'D', (byte)'T', (byte)'I', (byte)'I' };
            Assert.False(ParticleHandoffPolicy.IsParticlePayload(payload));
        }

        [Fact(DisplayName = "IsParticlePayload: a non-FORM top-level chunk is rejected")]
        public void IsParticlePayload_FalseWhenNotForm()
        {
            byte[] payload = { (byte)'X', (byte)'X', (byte)'X', (byte)'X', 0, 0, 0, 8, (byte)'P', (byte)'E', (byte)'F', (byte)'T' };
            Assert.False(ParticleHandoffPolicy.IsParticlePayload(payload));
        }

        // ── Test 3: IsParticlePayload never throws on malformed/truncated bytes ────
        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        [InlineData(8)]
        [InlineData(11)]
        public void IsParticlePayload_FalseForShortOrNullPayload_NeverThrows(int length)
        {
            Assert.False(ParticleHandoffPolicy.IsParticlePayload(new byte[length]));
            Assert.False(ParticleHandoffPolicy.IsParticlePayload(null));
        }

        [Fact(DisplayName = "IsParticlePayload: garbage / truncated bytes never throw, return false")]
        public void IsParticlePayload_FalseOnGarbage_NeverThrows()
        {
            // A "FORM" header claiming PEFT but with a truncated/garbage body must not throw.
            byte[] truncated = { (byte)'F', (byte)'O', (byte)'R', (byte)'M', 0x7F, 0x7F, 0x7F, 0x7F, (byte)'P', (byte)'E', (byte)'F' };
            Assert.False(ParticleHandoffPolicy.IsParticlePayload(truncated));

            byte[] garbage = { 0x00, 0xFF, 0x10, 0x42, 0x99, 0x01, 0x02, 0x03 };
            Assert.False(ParticleHandoffPolicy.IsParticlePayload(garbage));
        }
    }
}
