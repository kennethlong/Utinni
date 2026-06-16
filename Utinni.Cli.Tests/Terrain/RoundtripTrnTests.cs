// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain (SOE/Bootprint, All
// Rights Reserved) and the EA-IFF-85 public standard. No code, comments, identifier names, or test fixtures
// copied from any reference source. Implementation original to Utinni under MIT.

using Xunit;

namespace Utinni.Cli.Tests.Terrain
{
    /// <summary>
    /// Byte-exact whole-file <c>roundtrip-trn</c> goldens over the FULL both-lineage version matrix
    /// (PROD-W2-TRN-03/04). Every Tier-1 tag's low + high synthesized fixture (plus the minimal /
    /// unknown / dead / truncated / compositional fixtures) must <c>ReadAllBytes → MutableIffDocument →
    /// IffWriter → SequenceEqual</c> the input byte-for-byte.
    ///
    /// <para>All <c>Skip</c>-marked until Wave 2 (the <c>roundtrip-trn</c> verb is built in Plan 03).
    /// COLLECTED by the runner so the wave inherits no silent gap. Filter trait: <c>RoundtripTrn</c>.</para>
    /// </summary>
    public class RoundtripTrnTests
    {
        private const string WaveTwo = "Wave 2 — pending roundtrip-trn verb (Plan 03)";

        [Fact(Skip = WaveTwo)]
        [Trait("Category", "RoundtripTrn")]
        public void Roundtrip_MinimalTgen_ByteExact()
        {
            // TODO Wave 2: MinimalTgen() → roundtrip-trn → bytes identical.
        }

        [Theory(Skip = WaveTwo)]
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
            // TODO Wave 2: low ("SWGEmu-era") fixture for each Tier-1 tag → roundtrip-trn → byte-exact.
            _ = tag;
        }

        [Theory(Skip = WaveTwo)]
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
            // TODO Wave 2: high ("Restoration-era") fixture for each Tier-1 tag → roundtrip-trn → byte-exact.
            _ = tag;
        }

        [Fact(Skip = WaveTwo)]
        [Trait("Category", "RoundtripTrn")]
        public void Roundtrip_UnknownTag_RawPreserved_ByteExact()
        {
            // TODO Wave 2: WithUnknownTag() raw-preserved sub-tree → roundtrip-trn → byte-exact.
        }

        [Fact(Skip = WaveTwo)]
        [Trait("Category", "RoundtripTrn")]
        public void Roundtrip_DeadTag_VerbatimReEmit_ByteExact()
        {
            // TODO Wave 2: WithDeadTag() recognized-and-skipped form re-emits verbatim → byte-exact.
        }

        [Fact(Skip = WaveTwo)]
        [Trait("Category", "RoundtripTrn")]
        public void Roundtrip_CompositionalLayer_AllSiblingsByteExact()
        {
            // TODO Wave 2: CompositionalLayer() (typed + DEAD + unknown adjacency) → roundtrip-trn → byte-exact (#16).
        }
    }
}
