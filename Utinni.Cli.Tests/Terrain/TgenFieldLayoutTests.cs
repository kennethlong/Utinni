// Format understood by reading swg-client-v2/src/engine/shared/library/sharedTerrain (SOE/Bootprint, All
// Rights Reserved) and the EA-IFF-85 public standard. No code, comments, identifier names, or test fixtures
// copied from any reference source. Implementation original to Utinni under MIT.

using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Terrain;
using Utinni.Cli.Tests.Fixtures.Trn;
using Xunit;

namespace Utinni.Cli.Tests.Terrain
{
    /// <summary>
    /// Offset-parity tests for the single-source <see cref="TgenFieldLayouts"/> descriptor table
    /// (review concern #2). Proves, per (tag, version) in <see cref="TgenEraVersions"/>, that the
    /// descriptor offsets are contiguous + non-overlapping AND that their widths sum to the EXACT fixed
    /// DATA payload length the <see cref="TgenFixtureSynthesizer"/> emits — the foundation the Plan-03
    /// encoder's byte-exact parity test builds on.
    ///
    /// <para>Filter trait <c>TgenLayout</c>.</para>
    /// </summary>
    public class TgenFieldLayoutTests
    {
        // The Tier-1 tags whose typed layout the descriptor table covers (D-01).
        public static IEnumerable<object[]> Tier1TagArms()
        {
            foreach (string tag in TgenEraVersions.Tier1Tags)
            {
                yield return new object[] { tag, TgenEraVersions.Low(tag) };
                yield return new object[] { tag, TgenEraVersions.High(tag) };
            }
        }

        [Theory]
        [Trait("Category", "TgenLayout")]
        [MemberData(nameof(Tier1TagArms))]
        public void For_EveryTier1TagVersion_ReturnsOrderedContiguousNonOverlappingDescriptors(string tag, string version)
        {
            IReadOnlyList<TgenFieldDescriptor> descriptors = TgenFieldLayouts.For(tag, version);
            Assert.NotNull(descriptors);
            Assert.NotEmpty(descriptors);

            int expectedOffset = 0;
            foreach (TgenFieldDescriptor d in descriptors)
            {
                Assert.True(d.Width > 0, tag + "/" + version + " field '" + d.Name + "' has non-positive width.");
                Assert.Equal(expectedOffset, d.Offset); // contiguous + non-overlapping (each starts where the last ended).
                Assert.Equal(TgenFieldEndian.Little, d.Endian);
                expectedOffset += d.Width;
            }
        }

        [Theory]
        [Trait("Category", "TgenLayout")]
        [MemberData(nameof(Tier1TagArms))]
        public void DescriptorWidths_SumToTheSynthesizedDataPayloadLength(string tag, string version)
        {
            int descriptorSum = TgenFieldLayouts.PayloadLength(tag, version);
            Assert.True(descriptorSum > 0, "No descriptor layout for " + tag + "/" + version + ".");

            int fixtureDataLength = ExtractDataPayloadLength(TgenFixtureSynthesizer.WithTypedTag(tag, version));
            Assert.Equal(fixtureDataLength, descriptorSum);
        }

        [Fact]
        [Trait("Category", "TgenLayout")]
        public void For_UnknownTagOrVersion_ReturnsNull_NeverThrows()
        {
            Assert.Null(TgenFieldLayouts.For("ZZZZ", "0000"));
            Assert.Null(TgenFieldLayouts.For("AHCN", "9999")); // known tag, unrecognized version (D-02).
            Assert.Null(TgenFieldLayouts.For(null, "0000"));
            Assert.Null(TgenFieldLayouts.For("AHCN", null));
        }

        [Theory]
        [Trait("Category", "TgenLayout")]
        [InlineData("AHCN", "0000", "operation", TgenDisplayType.Enum32)]
        [InlineData("AHCN", "0000", "height", TgenDisplayType.ScalarFloat)]
        [InlineData("ASCN", "0001", "familyId", TgenDisplayType.FamilyIdRef)]
        public void For_NamedField_HasExpectedDisplayTypeAndIsEditable(string tag, string version, string field, TgenDisplayType expected)
        {
            IReadOnlyList<TgenFieldDescriptor> descriptors = TgenFieldLayouts.For(tag, version);
            Assert.NotNull(descriptors);
            TgenFieldDescriptor d = descriptors.FirstByName(field);
            Assert.NotNull(d);
            Assert.Equal(expected, d.DisplayType);
            Assert.True(d.Editable);
        }

        // Extracts the length of the FIRST DATA leaf in the synthesized fixture (the typed node's payload),
        // so the descriptor-width sum can be asserted against the real on-disk fixed payload length.
        private static int ExtractDataPayloadLength(byte[] fixtureBytes)
        {
            using (var ms = new System.IO.MemoryStream(fixtureBytes, writable: false))
            {
                var doc = UtinniCoreDotNet.Formats.Iff.IffReader.Read(ms);
                return FindFirstDataLength(doc.Root);
            }
        }

        private static int FindFirstDataLength(UtinniCoreDotNet.Formats.Iff.IffChunk chunk)
        {
            var leaf = chunk as UtinniCoreDotNet.Formats.Iff.IffLeafChunk;
            if (leaf != null && leaf.TypeId == "DATA") return leaf.Data.Length;
            var container = chunk as UtinniCoreDotNet.Formats.Iff.IffContainerChunk;
            if (container != null)
            {
                foreach (var c in container.Children)
                {
                    int found = FindFirstDataLength(c);
                    if (found >= 0) return found;
                }
            }
            return -1;
        }
    }

    internal static class TgenFieldDescriptorTestExtensions
    {
        public static TgenFieldDescriptor FirstByName(this IReadOnlyList<TgenFieldDescriptor> descriptors, string name)
        {
            foreach (var d in descriptors)
                if (d.Name == name) return d;
            return null;
        }
    }
}
