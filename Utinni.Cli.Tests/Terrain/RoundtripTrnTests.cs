// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain (SOE/Bootprint, All
// Rights Reserved) and the EA-IFF-85 public standard. No code, comments, identifier names, or test fixtures
// copied from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Linq;
using UtinniCoreDotNet.Formats.Terrain;
using Utinni.Cli.Tests.Fixtures.Trn;
using Xunit;

namespace Utinni.Cli.Tests.Terrain
{
    /// <summary>
    /// Byte-exact whole-file <c>roundtrip-trn</c> goldens over the FULL both-lineage version matrix
    /// (PROD-W2-TRN-03/04) PLUS the single-field exact-byte-span + float-bit-policy assertions for the
    /// <see cref="TrnFieldEncoder"/> (review concerns #2/#6). Every Tier-1 tag's low + high synthesized
    /// fixture (plus the minimal / unknown / dead / truncated / compositional fixtures) must
    /// <c>FromBytes → Serialize → SequenceEqual</c> the input byte-for-byte; a one-field edit must change
    /// ONLY that field's exact descriptor span; NaN/Infinity must be rejected.
    ///
    /// <para>Filter trait: <c>RoundtripTrn</c>.</para>
    /// </summary>
    public class RoundtripTrnTests
    {
        // ── whole-file byte identity (the DEC-C3 codec-level gate) ──────────────

        [Fact]
        [Trait("Category", "RoundtripTrn")]
        public void Roundtrip_MinimalTgen_ByteExact()
        {
            AssertRoundtripByteExact(TgenFixtureSynthesizer.MinimalTgen());
        }

        [Theory]
        [Trait("Category", "RoundtripTrn")]
        [InlineData("AHCN")]
        [InlineData("AHTR")]
        [InlineData("ACCN")]
        [InlineData("ACRH")]
        [InlineData("ASCN")]
        [InlineData("ASRP")]
        [InlineData("AFCN")]
        [InlineData("AFSC")]
        [InlineData("AFSN")]
        [InlineData("BCIR")]
        [InlineData("BREC")]
        [InlineData("FHGT")]
        [InlineData("FSLP")]
        public void Roundtrip_Tier1Tag_LowVersion_ByteExact(string tag)
        {
            AssertRoundtripByteExact(TgenFixtureSynthesizer.LowVersion(tag));
        }

        [Theory]
        [Trait("Category", "RoundtripTrn")]
        [InlineData("AHCN")]
        [InlineData("AHTR")]
        [InlineData("ACCN")]
        [InlineData("ACRH")]
        [InlineData("ASCN")]
        [InlineData("ASRP")]
        [InlineData("AFCN")]
        [InlineData("AFSC")]
        [InlineData("AFSN")]
        [InlineData("BCIR")]
        [InlineData("BREC")]
        [InlineData("FHGT")]
        [InlineData("FSLP")]
        public void Roundtrip_Tier1Tag_HighVersion_ByteExact(string tag)
        {
            AssertRoundtripByteExact(TgenFixtureSynthesizer.HighVersion(tag));
        }

        [Fact]
        [Trait("Category", "RoundtripTrn")]
        public void Roundtrip_UnknownTag_RawPreserved_ByteExact()
        {
            AssertRoundtripByteExact(TgenFixtureSynthesizer.WithUnknownTag());
        }

        [Fact]
        [Trait("Category", "RoundtripTrn")]
        public void Roundtrip_DeadTag_VerbatimReEmit_ByteExact()
        {
            AssertRoundtripByteExact(TgenFixtureSynthesizer.WithDeadTag());
        }

        [Fact]
        [Trait("Category", "RoundtripTrn")]
        public void Roundtrip_TruncatedKnownTag_RawPreserved_ByteExact()
        {
            AssertRoundtripByteExact(TgenFixtureSynthesizer.WithTruncatedKnownTag());
        }

        [Fact]
        [Trait("Category", "RoundtripTrn")]
        public void Roundtrip_CompositionalLayer_AllSiblingsByteExact()
        {
            AssertRoundtripByteExact(TgenFixtureSynthesizer.CompositionalLayer());
        }

        // ── exact-byte-span single-field edit (#2) ──────────────────────────────

        [Fact]
        [Trait("Category", "RoundtripTrn")]
        public void Encoder_EditHeight_OnlyHeightSpanDiffers_OperationUnchanged()
        {
            // AHCN v0000 DATA = [int32 operation][float height]; editing height touches ONLY bytes [4..8).
            byte[] payload = AhcnPayload();
            byte[] edited = TrnFieldEncoder.EncodeField(payload, "AHCN", "0000", "height", "987.25");

            Assert.Equal(payload.Length, edited.Length);
            // operation [0..4) byte-identical (copied verbatim, not re-derived).
            for (int i = 0; i < 4; i++) Assert.Equal(payload[i], edited[i]);
            // height [4..8) is the only span allowed to differ.
            Assert.False(payload.Skip(4).Take(4).SequenceEqual(edited.Skip(4).Take(4)),
                "height edit must change the height span");
        }

        [Fact]
        [Trait("Category", "RoundtripTrn")]
        public void Encoder_EditOperation_OnlyOperationSpanDiffers()
        {
            byte[] payload = AhcnPayload();
            byte[] edited = TrnFieldEncoder.EncodeField(payload, "AHCN", "0000", "operation", "3");

            Assert.Equal(payload.Length, edited.Length);
            // height [4..8) untouched (exact float bits preserved — no parse/format canonicalization, #6).
            for (int i = 4; i < 8; i++) Assert.Equal(payload[i], edited[i]);
            Assert.False(payload.Take(4).SequenceEqual(edited.Take(4)));
        }

        [Fact]
        [Trait("Category", "RoundtripTrn")]
        public void Encoder_EditOneFloat_OtherFloatFieldKeepsExactBits()
        {
            // ACRH v0000 DATA = [float lowHeight][float highHeight]; editing lowHeight must leave the
            // highHeight float bits BYTE-identical (no round-trip through parse/format — #6).
            byte[] payload = ExtractFirstDataPayload(TgenFixtureSynthesizer.WithTypedTag("ACRH", "0000"));
            byte[] edited = TrnFieldEncoder.EncodeField(payload, "ACRH", "0000", "lowHeight", "999.25");

            Assert.Equal(payload.Length, edited.Length);
            // highHeight is the second float [4..8): bit-for-bit unchanged.
            for (int i = 4; i < 8; i++) Assert.Equal(payload[i], edited[i]);
        }

        // ── float bit policy: NaN/Infinity rejected; unknown field throws (#6) ──

        [Theory]
        [Trait("Category", "RoundtripTrn")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("-Infinity")]
        public void Encoder_FloatNaNOrInfinity_Rejected_NoWrite(string badValue)
        {
            byte[] payload = AhcnPayload();
            Assert.Throws<ArgumentException>(
                () => TrnFieldEncoder.EncodeField(payload, "AHCN", "0000", "height", badValue));
        }

        [Fact]
        [Trait("Category", "RoundtripTrn")]
        public void Encoder_UnknownFieldName_Throws_NotSilentNoOp()
        {
            byte[] payload = AhcnPayload();
            Assert.Throws<ArgumentException>(
                () => TrnFieldEncoder.EncodeField(payload, "AHCN", "0000", "notARealField", "1"));
        }

        [Fact]
        [Trait("Category", "RoundtripTrn")]
        public void Encoder_UnknownTagVersion_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => TrnFieldEncoder.EncodeField(new byte[8], "ZZZZ", "0000", "x", "1"));
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        private static byte[] AhcnPayload() => ExtractFirstDataPayload(TgenFixtureSynthesizer.WithTypedTag("AHCN", "0000"));

        private static void AssertRoundtripByteExact(byte[] input)
        {
            TerrainDocument doc = TerrainDocument.FromBytes(input);
            byte[] roundtripped = doc.Serialize();
            // Re-parse for structural validity.
            TerrainDocument reparsed = TerrainDocument.FromBytes(roundtripped);
            Assert.NotNull(reparsed);
            Assert.True(input.Length == roundtripped.Length && input.SequenceEqual(roundtripped),
                "whole-file roundtrip must be byte-identical");
        }

        private static byte[] ExtractFirstDataPayload(byte[] fixtureBytes)
        {
            using (var ms = new System.IO.MemoryStream(fixtureBytes, writable: false))
            {
                var d = UtinniCoreDotNet.Formats.Iff.IffReader.Read(ms);
                return FindFirstDataPayload(d.Root);
            }
        }

        private static byte[] FindFirstDataPayload(UtinniCoreDotNet.Formats.Iff.IffChunk chunk)
        {
            var leaf = chunk as UtinniCoreDotNet.Formats.Iff.IffLeafChunk;
            if (leaf != null && leaf.TypeId == "DATA") return leaf.Data;
            var container = chunk as UtinniCoreDotNet.Formats.Iff.IffContainerChunk;
            if (container != null)
            {
                foreach (var c in container.Children)
                {
                    byte[] found = FindFirstDataPayload(c);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
