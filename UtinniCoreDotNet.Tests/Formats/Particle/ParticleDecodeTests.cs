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

using System.Collections.Generic;
using System.IO;
using UtinniCoreDotNet.Formats.Iff;
using UtinniCoreDotNet.Formats.Particle;
using Xunit;

namespace UtinniCoreDotNet.Tests.Formats.Particle
{
    /// <summary>
    /// Task 2 coverage (filter prefix <c>ParticleDecode</c>): the PEFT typed-decode tree
    /// (<see cref="ParticleEffectDocument"/> + <see cref="MutableParticleEffect"/> + EMGP/EMTR) and the
    /// byte-exact <see cref="ParticleEffectWriter"/>. Fixtures are built through the framework IFF
    /// primitives so byte-exact assertions are meaningful.
    /// </summary>
    public class ParticleDecodeTests
    {
        // ── Test 1: FromBytes yields expected group / emitter counts + typed structure ──

        [Fact]
        public void ParticleDecode_MinimalPeft_YieldsGroupAndEmitterCounts()
        {
            byte[] bytes = ParticleFixtureBuilder.BuildMinimalPeft("0002");

            MutableParticleEffect effect = ParticleEffectDocument.FromBytes(bytes);

            Assert.Equal("0002", effect.Version);
            Assert.False(effect.IsRawPreserved);
            Assert.Single(effect.Groups);
            Assert.Single(effect.Groups[0].Emitters);
        }

        // ── Test 2: a known-version EMTR decodes its WaveForm field via WaveFormCodec ──

        [Fact]
        public void ParticleDecode_KnownEmtr_DecodesWaveFormField()
        {
            byte[] bytes = ParticleFixtureBuilder.BuildMinimalPeft("0011");

            MutableParticleEffect effect = ParticleEffectDocument.FromBytes(bytes);

            ParticleEmitterDescription emitter = effect.Groups[0].Emitters[0];
            Assert.False(emitter.IsRawPreserved);
            Assert.Single(emitter.WaveFormFields);
            Assert.Equal(ParticleFieldKind.WaveForm, emitter.WaveFormFields[0].Kind);
            Assert.Equal("0002", emitter.WaveFormFields[0].WaveForm.Version);
            Assert.Equal(2, emitter.WaveFormFields[0].WaveForm.ControlPoints.Count);
        }

        // ── Test 3: editing one leaf mutates only that leaf; untouched leaves stay byte-identical ──

        [Fact]
        public void ParticleDecode_EditOneLeaf_LeavesUntouchedLeavesByteIdentical()
        {
            byte[] bytes = ParticleFixtureBuilder.BuildMinimalPeft("0002");
            MutableParticleEffect effect = ParticleEffectDocument.FromBytes(bytes);

            // Find the emitter scalar chunk (tag "0000" leaf directly under the EMTR version form) and
            // capture every OTHER leaf's payload before the edit.
            var leaves = new List<MutableIffNode>();
            Collect(effect.SourceIff.Root, leaves);
            MutableIffNode scalar = FindEmtrScalarLeaf(effect.SourceIff.Root);
            Assert.NotNull(scalar);

            var before = new Dictionary<MutableIffNode, byte[]>();
            foreach (MutableIffNode l in leaves)
            {
                if (!ReferenceEquals(l, scalar)) before[l] = l.GetPayloadCopy();
            }

            effect.EditLeafPayload(scalar, ParticleFixtureBuilder.Int32Le(999));

            foreach (KeyValuePair<MutableIffNode, byte[]> kv in before)
            {
                Assert.Equal(kv.Value, kv.Key.GetPayloadCopy());
            }

            // The edited effect re-emits with the new scalar value and is re-parseable.
            byte[] outBytes = effect.Serialize();
            MutableParticleEffect reparsed = ParticleEffectDocument.FromBytes(outBytes);
            Assert.Equal("0002", reparsed.Version);
        }

        // ── Test 4: a structural count change re-derives the count leaf little-endian ──

        [Fact]
        public void ParticleDecode_RewriteCount_ReDerivesLeadingInt32AndKeepsTrailingBytes()
        {
            byte[] bytes = ParticleFixtureBuilder.BuildMinimalPeft("0002");
            MutableParticleEffect effect = ParticleEffectDocument.FromBytes(bytes);

            // The PEFT 0002 group-count leaf carries int32 count + 4 trailing floats. Rewrite the
            // count to 5; the leading int32 must change, the trailing 16 bytes must survive verbatim.
            MutableIffNode versionForm = FirstContainer(effect.SourceIff.Root);
            MutableIffNode countLeaf = FirstLeaf(versionForm);
            byte[] originalTail = Tail(countLeaf.GetPayloadCopy(), 4);

            effect.RewriteCount(countLeaf, 5);

            byte[] rewritten = countLeaf.GetPayloadCopy();
            var cursor = new System.IO.BinaryReader(new MemoryStream(rewritten));
            int newCount = cursor.ReadInt32(); // BinaryReader is little-endian on x86
            Assert.Equal(5, newCount);
            Assert.Equal(originalTail, Tail(rewritten, 4));
        }

        // ── Test 5: no-edit round-trip is byte-identical ──

        [Fact]
        public void ParticleDecode_NoEditRoundTrip_IsByteIdentical()
        {
            byte[] bytes = ParticleFixtureBuilder.BuildMinimalPeft("0002");

            MutableParticleEffect effect = ParticleEffectDocument.FromBytes(bytes);
            byte[] outBytes = ParticleEffectWriter.Serialize(effect);

            Assert.Equal(bytes, outBytes);
        }

        // ── Test 6 (DoS guard): an oversized group/emitter count is rejected before allocation ──

        [Fact]
        public void ParticleDecode_ForgedGroupCount_RejectedWithParticleParseException()
        {
            byte[] bytes = ParticleFixtureBuilder.BuildMinimalPeft("0002");

            // Forge the group-count chunk's leading int32 to a huge value while only one EMGP exists.
            MutableIffNode root = ParseRoot(bytes);
            MutableIffNode versionForm = FirstContainer(root);
            MutableIffNode countLeaf = FirstLeaf(versionForm);
            byte[] payload = countLeaf.GetPayloadCopy();
            payload[0] = 0xFF; payload[1] = 0xFF; payload[2] = 0xFF; payload[3] = 0x7F; // ~2.1B
            countLeaf.SetPayload(payload);
            byte[] forged = IffWriter.Write(new MutableIffDocument(root));

            ParticleParseException ex = Assert.Throws<ParticleParseException>(
                () => ParticleEffectDocument.FromBytes(forged));
            Assert.Equal(ParticleParseError.CountExceedsCap, ex.Kind);
        }

        [Fact]
        public void ParticleDecode_NonPeftRoot_RejectedWithUnexpectedForm()
        {
            MutableIffNode root = MutableIffNode.NewContainer("FORM", "DTII");
            root.AddLeaf("0000", new byte[] { 1, 2, 3, 4 });
            byte[] bytes = IffWriter.Write(new MutableIffDocument(root));

            ParticleParseException ex = Assert.Throws<ParticleParseException>(
                () => ParticleEffectDocument.FromBytes(bytes));
            Assert.Equal(ParticleParseError.UnexpectedForm, ex.Kind);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static MutableIffNode ParseRoot(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                IffDocument doc = IffReader.Read(ms);
                return MutableIffDocument.FromDocument(doc, bytes).Root;
            }
        }

        private static void Collect(MutableIffNode node, List<MutableIffNode> leaves)
        {
            if (node.Kind == MutableIffNodeKind.Leaf) { leaves.Add(node); return; }
            foreach (MutableIffNode child in node.Children) Collect(child, leaves);
        }

        private static MutableIffNode FindEmtrScalarLeaf(MutableIffNode node)
        {
            if (node.Kind == MutableIffNodeKind.Container && node.SubTypeId != null
                && node.SubTypeId.Length == 4 && IsDigits(node.SubTypeId))
            {
                // a version sub-form; look for its first direct leaf child that is not part of a WVFM.
                if (node.Parent != null && node.Parent.Kind == MutableIffNodeKind.Container
                    && node.Parent.SubTypeId == "EMTR")
                {
                    foreach (MutableIffNode child in node.Children)
                    {
                        if (child.Kind == MutableIffNodeKind.Leaf) return child;
                    }
                }
            }
            foreach (MutableIffNode child in node.Children)
            {
                MutableIffNode found = FindEmtrScalarLeaf(child);
                if (found != null) return found;
            }
            return null;
        }

        private static bool IsDigits(string s)
        {
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        private static MutableIffNode FirstContainer(MutableIffNode parent)
        {
            foreach (MutableIffNode child in parent.Children)
                if (child.Kind == MutableIffNodeKind.Container) return child;
            return null;
        }

        private static MutableIffNode FirstLeaf(MutableIffNode parent)
        {
            foreach (MutableIffNode child in parent.Children)
                if (child.Kind == MutableIffNodeKind.Leaf) return child;
            return null;
        }

        private static byte[] Tail(byte[] b, int fromIndex)
        {
            var t = new byte[b.Length - fromIndex];
            for (int i = 0; i < t.Length; i++) t[i] = b[i + fromIndex];
            return t;
        }
    }
}
