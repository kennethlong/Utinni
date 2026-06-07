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

using System.IO;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Particle;
using Xunit;

namespace UtinniCoreDotNet.Tests.Formats.Particle
{
    /// <summary>
    /// Task 1 coverage (filter prefix <c>ParticleWaveForm</c>): the recurring leaf codecs ported first
    /// — <see cref="WaveFormCodec"/>, <see cref="ColorRampCodec"/>, the <see cref="ParticleFieldValue"/>
    /// typed union, and the <see cref="ParticleFixtureBuilder"/> synth fixture. All fixtures are built
    /// through the framework IFF primitives so byte-exact assertions are meaningful.
    /// </summary>
    public class ParticleCodecTests
    {
        // ── Test 1: WaveFormCodec.Read yields expected points; Write re-emits byte-identical ──

        [Fact]
        public void ParticleWaveForm_Decode0002_YieldsPointsAndReencodesByteIdentical()
        {
            float[][] pts = ParticleFixtureBuilder.TwoPoints();
            byte[] payload = ParticleFixtureBuilder.WaveForm0002Payload(1, 0, -5f, 5f, pts);

            ParticleWaveForm wf = WaveFormCodec.Decode("0002", payload);

            Assert.Equal("0002", wf.Version);
            Assert.Equal(1, wf.InterpolationType);
            Assert.Equal(0, wf.SampleType);
            Assert.Equal(-5f, wf.ValueMin);
            Assert.Equal(5f, wf.ValueMax);
            Assert.Equal(2, wf.ControlPoints.Count);
            Assert.Equal(0.0f, wf.ControlPoints[0].Percent);
            Assert.Equal(1.0f, wf.ControlPoints[0].Value);
            Assert.Equal(2.5f, wf.ControlPoints[1].Value);

            byte[] reencoded = WaveFormCodec.Encode(wf);
            Assert.Equal(payload, reencoded);
        }

        // ── Test 2: all 3 known versions round-trip byte-exact; unknown version raw-preserves ──

        [Theory]
        [InlineData("0000")]
        [InlineData("0001")]
        [InlineData("0002")]
        public void ParticleWaveForm_AllKnownVersions_RoundTripByteExact(string version)
        {
            float[][] pts = ParticleFixtureBuilder.TwoPoints();
            byte[] payload;
            switch (version)
            {
                case "0000": payload = ParticleFixtureBuilder.WaveForm0000Payload(1, pts); break;
                case "0001": payload = ParticleFixtureBuilder.WaveForm0001Payload(1, 0, pts); break;
                default: payload = ParticleFixtureBuilder.WaveForm0002Payload(1, 0, -5f, 5f, pts); break;
            }

            ParticleFieldValue value = WaveFormCodec.TryDecode(version, payload);
            Assert.Equal(ParticleFieldKind.WaveForm, value.Kind);
            Assert.Equal(payload, WaveFormCodec.Encode(value.WaveForm));
        }

        [Fact]
        public void ParticleWaveForm_UnknownVersion_RawPreservesWithoutThrowing()
        {
            byte[] payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02 };

            ParticleFieldValue value = WaveFormCodec.TryDecode("00FF", payload);

            Assert.Equal(ParticleFieldKind.RawBytesHexFallback, value.Kind);
            Assert.Equal(payload, value.GetRawBytesCopy());
        }

        // ── Test 3: ColorRampCodec round-trips byte-exact ──

        [Fact]
        public void ParticleWaveForm_ColorRamp0001_RoundTripsByteExact()
        {
            float[][] pts = ParticleFixtureBuilder.TwoColorPoints();
            byte[] payload = ParticleFixtureBuilder.ColorRamp0001Payload(0, 0, pts);

            ParticleColorRamp ramp = ColorRampCodec.Decode("0001", payload);
            Assert.Equal(2, ramp.ControlPoints.Count);
            Assert.Equal(1.0f, ramp.ControlPoints[0].Red);
            Assert.Equal(1.0f, ramp.ControlPoints[1].Blue);

            Assert.Equal(payload, ColorRampCodec.Encode(ramp));

            ParticleFieldValue value = ColorRampCodec.TryDecode("0001", payload);
            Assert.Equal(ParticleFieldKind.ColorRamp, value.Kind);
            Assert.Equal(payload, ColorRampCodec.Encode(value.ColorRamp));
        }

        // ── Test 4: ParticleFieldValue typed union + FromRawBytes preserves exact bytes ──

        [Fact]
        public void ParticleWaveForm_FieldValue_TypedUnionAndRawBytesPreserveExactBytes()
        {
            Assert.Equal(ParticleFieldKind.Float, ParticleFieldValue.FromFloat(1.5f).Kind);
            Assert.Equal(7, ParticleFieldValue.FromInt(7).IntValue);
            Assert.True(ParticleFieldValue.FromBool(true).BoolValue);
            Assert.Equal(3, ParticleFieldValue.FromEnum(3).EnumValue);

            byte[] original = new byte[] { 1, 2, 3, 4, 250, 251, 252 };
            ParticleFieldValue raw = ParticleFieldValue.FromRawBytes(original);
            Assert.Equal(ParticleFieldKind.RawBytesHexFallback, raw.Kind);

            byte[] copy = raw.GetRawBytesCopy();
            Assert.Equal(original, copy);

            // Defensive copy: mutating the returned array must not affect the stored bytes.
            copy[0] = 99;
            Assert.Equal(original, raw.GetRawBytesCopy());
        }

        // ── Test 5: ParticleFixtureBuilder produces a FORM PEFT that IffReader parses ──

        [Fact]
        public void ParticleWaveForm_FixtureBuilder_ProducesParsableFormPeft()
        {
            byte[] bytes = ParticleFixtureBuilder.BuildMinimalPeft("0000");

            using (var ms = new MemoryStream(bytes))
            {
                IffDocument doc = IffReader.Read(ms);
                Assert.NotNull(doc.Root);
                var container = doc.Root as IffContainerChunk;
                Assert.NotNull(container);
                Assert.Equal("PEFT", container.SubTypeId);
            }
        }

        // ── Test 6 (Pitfall 3 guard): no scaleAll(0.28f) applied on write-back ──

        [Fact]
        public void ParticleWaveForm_WeightField_RoundTripsWithoutScaleAllNormalization()
        {
            // A weight-style waveform whose value would be visibly altered by a 0.28 scale.
            float[][] pts = new[]
            {
                new[] { 0.0f, 100.0f, 0.0f, 0.0f },
                new[] { 1.0f, 100.0f, 0.0f, 0.0f }
            };
            byte[] payload = ParticleFixtureBuilder.WaveForm0002Payload(0, 0, -10000f, 10000f, pts);

            ParticleWaveForm wf = WaveFormCodec.Decode("0002", payload);

            // The decoded value is the ORIGINAL, not scaled by 0.28.
            Assert.Equal(100.0f, wf.ControlPoints[0].Value);
            Assert.NotEqual(28.0f, wf.ControlPoints[0].Value);

            // And write-back is byte-exact.
            Assert.Equal(payload, WaveFormCodec.Encode(wf));
        }
    }
}
