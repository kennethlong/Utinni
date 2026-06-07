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

using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Particle;
using Xunit;

namespace UtinniCoreDotNet.Tests.Formats.Particle
{
    /// <summary>
    /// Task 3 coverage (filter prefix <c>ParticleDegrade</c>): degrade-don't-abort raw-preserve at the
    /// FORM-version granularity (D-05). An unrecognized EMTR/PEFT version (and an over-length / truncated
    /// typed leaf) is captured verbatim and round-trips byte-exact; the codec NEVER hard-aborts.
    /// </summary>
    public class ParticleDegradeTests
    {
        // ── Test 1: an unknown EMTR version is raw-preserved; decode does not throw ──

        [Fact]
        public void ParticleDegrade_UnknownEmtrVersion_RawPreservedWithoutThrowing()
        {
            byte[] bytes = ParticleFixtureBuilder.BuildMinimalPeft("00FF");

            MutableParticleEffect effect = ParticleEffectDocument.FromBytes(bytes);

            Assert.False(effect.IsRawPreserved); // the EFFECT root is fine
            ParticleEmitterDescription emitter = effect.Groups[0].Emitters[0];
            Assert.True(emitter.IsRawPreserved); // the EMITTER degraded
            Assert.Empty(emitter.WaveFormFields);
        }

        // ── Test 2: round-tripping a degraded (unknown-EMTR) file is byte-identical ──

        [Fact]
        public void ParticleDegrade_UnknownEmtrVersion_RoundTripsByteIdentical()
        {
            byte[] bytes = ParticleFixtureBuilder.BuildMinimalPeft("00FF");

            MutableParticleEffect effect = ParticleEffectDocument.FromBytes(bytes);
            byte[] outBytes = effect.Serialize();

            Assert.Equal(bytes, outBytes);
        }

        // ── Test 3: an unknown PEFT ROOT version degrades + round-trips byte-identical ──

        [Fact]
        public void ParticleDegrade_UnknownPeftRootVersion_RawPreservedAndRoundTripsByteIdentical()
        {
            byte[] bytes = ParticleFixtureBuilder.BuildPeftWithRootVersion("0099");

            MutableParticleEffect effect = ParticleEffectDocument.FromBytes(bytes);

            Assert.True(effect.IsRawPreserved);
            Assert.Empty(effect.Groups);
            Assert.Equal(bytes, effect.Serialize());
        }

        // ── Test 4: a truncated / over-length typed leaf raw-preserves rather than OOB-throwing ──

        [Fact]
        public void ParticleDegrade_TruncatedWaveFormLeaf_RawPreservesAndRoundTripsByteIdentical()
        {
            byte[] bytes = ParticleFixtureBuilder.BuildPeftWithTruncatedWaveForm();

            MutableParticleEffect effect = ParticleEffectDocument.FromBytes(bytes);

            ParticleEmitterDescription emitter = effect.Groups[0].Emitters[0];
            // The emitter's recognized version still walks; the bad WVFM field is raw-preserved.
            Assert.Single(emitter.WaveFormFields);
            Assert.Equal(ParticleFieldKind.RawBytesHexFallback, emitter.WaveFormFields[0].Kind);

            // And the whole effect round-trips byte-exact regardless.
            Assert.Equal(bytes, effect.Serialize());
        }

        // ── Test 5 (negative): never hard-aborts on unknown version; typed edit on a raw-preserved
        //     effect is refused gracefully (no corruption). ──

        [Fact]
        public void ParticleDegrade_UnknownVersion_NeverThrows_AndEditOnRawPreservedIsRefused()
        {
            // No synth unknown-version fixture throws to the caller.
            byte[] unknownEmtr = ParticleFixtureBuilder.BuildMinimalPeft("0042");
            byte[] unknownRoot = ParticleFixtureBuilder.BuildPeftWithRootVersion("00AB");

            MutableParticleEffect e1 = ParticleEffectDocument.FromBytes(unknownEmtr); // does not throw
            MutableParticleEffect e2 = ParticleEffectDocument.FromBytes(unknownRoot); // does not throw

            Assert.NotNull(e1);
            Assert.True(e2.IsRawPreserved);

            // A typed edit on a raw-preserved effect is refused with a clean exception, not a corruption.
            MutableIffNode anyLeaf = FirstLeaf(e2.SourceIff.Root);
            Assert.Throws<System.InvalidOperationException>(
                () => e2.EditLeafPayload(anyLeaf, new byte[] { 0 }));

            // The refused edit left the bytes untouched — still byte-exact.
            Assert.Equal(unknownRoot, e2.Serialize());
        }

        private static MutableIffNode FirstLeaf(MutableIffNode node)
        {
            if (node.Kind == MutableIffNodeKind.Leaf) return node;
            foreach (MutableIffNode child in node.Children)
            {
                MutableIffNode found = FirstLeaf(child);
                if (found != null) return found;
            }
            return null;
        }
    }
}
