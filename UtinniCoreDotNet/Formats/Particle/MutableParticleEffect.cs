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
// Particle-effect (FORM PEFT) tree layout understood by reading swg-client-v2
// .../clientParticle/.../ParticleEffectDescription.cpp + ParticleEmitterGroupDescription.cpp +
// ParticleEmitterDescription.cpp (SOE/Bootprint, All Rights Reserved): FORM PEFT -> FORM <version>
// (0000/0001/0002) -> [FORM PTIM timing] + a count chunk (int32 groupCount [+ 4 floats in 0002]) +
// N x FORM EMGP, each EMGP -> [FORM PTIM] + a count chunk (int32 emitterCount) + N x FORM EMTR.
// Only the on-disk layout was studied — no code, comments, identifier names, or test fixtures copied
// from any reference source. Implementation original to Utinni under MIT.

using System;
using System.Collections.Generic;
using UtinniCoreDotNet.Formats.Decoders;
using UtinniCoreDotNet.Formats.Iff;

namespace UtinniCoreDotNet.Formats.Particle
{
    /// <summary>One mutable emitter-group: its captured EMGP node, the machine-managed emitter-count
    /// leaf, and the ordered typed emitter views.</summary>
    public sealed class MutableParticleEmitterGroup
    {
        private readonly List<ParticleEmitterDescription> _emitters;

        internal MutableIffNode GroupNode { get; }
        internal MutableIffNode CountLeaf { get; }

        internal MutableParticleEmitterGroup(MutableIffNode groupNode, MutableIffNode countLeaf,
            List<ParticleEmitterDescription> emitters)
        {
            GroupNode = groupNode;
            CountLeaf = countLeaf;
            _emitters = emitters;
        }

        /// <summary>The typed emitter views in this group, in on-disk order.</summary>
        public IReadOnlyList<ParticleEmitterDescription> Emitters { get { return _emitters.AsReadOnly(); } }
    }

    /// <summary>
    /// Mutable typed particle-effect model over a captured <see cref="MutableIffDocument"/> — the
    /// editable counterpart to the read-only typed walk, mirroring <c>MutableObjectTemplate</c>.
    ///
    /// <para><b>Byte-exact posture:</b> the authoritative bytes live in the captured
    /// <see cref="MutableIffDocument"/>. Untouched leaves re-emit verbatim through
    /// <see cref="IffWriter"/>; <see cref="EditField"/> mutates a captured leaf in place; structural
    /// mutations re-derive the machine-managed int32 count leaf little-endian
    /// (<see cref="RewriteCount"/>), exactly as <c>MutableObjectTemplate.RewriteCount</c>.</para>
    ///
    /// <para><b>Degrade-don't-abort (D-05):</b> an unrecognized PEFT root version or EMTR emitter
    /// version is raw-preserved — its whole sub-tree's captured bytes re-emit verbatim. The codec
    /// NEVER hard-aborts the way the SOE reference does (Pitfall 2).</para>
    /// </summary>
    public sealed class MutableParticleEffect
    {
        // Absolute cap on a group / emitter count to reject a forged/oversized count before the read
        // loop allocates (T-15-02). A real effect has a handful of groups/emitters.
        internal const int MaxNodeCount = 16 * 1024 * 1024;

        private readonly List<MutableParticleEmitterGroup> _groups;

        private MutableParticleEffect(MutableIffDocument source, string version, bool rawPreserved,
            MutableIffNode versionForm, MutableIffNode groupCountLeaf,
            List<MutableParticleEmitterGroup> groups)
        {
            SourceIff = source;
            Version = version;
            IsRawPreserved = rawPreserved;
            VersionForm = versionForm;
            GroupCountLeaf = groupCountLeaf;
            _groups = groups;
        }

        /// <summary>The captured raw-IFF document — the byte source for byte-exact re-emit.</summary>
        public MutableIffDocument SourceIff { get; }

        /// <summary>The PEFT root version tag (e.g. "0002"), or the unrecognized tag when raw-preserved.</summary>
        public string Version { get; }

        /// <summary>
        /// True when the PEFT root version was not recognized and the whole version sub-tree was
        /// raw-preserved (D-05). A raw-preserved effect still opens + round-trips byte-exact.
        /// </summary>
        public bool IsRawPreserved { get; }

        /// <summary>The ordered emitter groups (empty when the effect is raw-preserved).</summary>
        public IReadOnlyList<MutableParticleEmitterGroup> Groups { get { return _groups.AsReadOnly(); } }

        /// <summary>True iff any mutation has been applied since parse.</summary>
        public bool IsDirty { get; private set; }

        internal MutableIffNode VersionForm { get; }
        internal MutableIffNode GroupCountLeaf { get; }

        // Recognized PEFT root versions. Anything else raw-preserves (D-05).
        private static readonly HashSet<string> KnownEffectVersions = new HashSet<string>(StringComparer.Ordinal)
        {
            "0000", "0001", "0002"
        };

        /// <summary>
        /// Parses an effect from a captured <see cref="MutableIffDocument"/>. Walks
        /// <c>FORM PEFT → FORM &lt;version&gt; → [PTIM] + count chunk + N × FORM EMGP</c>, each group
        /// <c>→ [PTIM] + count chunk + N × FORM EMTR</c>. An unrecognized root version raw-preserves
        /// the whole sub-tree (D-05); an unrecognized emitter version is raw-preserved per-emitter.
        /// Throws <see cref="ParticleParseException"/> only on a non-PEFT root or a forged count.
        /// </summary>
        public static MutableParticleEffect FromMutableIff(MutableIffDocument mutable)
        {
            if (mutable == null) throw new ArgumentNullException("mutable");
            MutableIffNode root = mutable.Root;
            if (root == null || root.Kind != MutableIffNodeKind.Container || root.SubTypeId != "PEFT")
            {
                throw new ParticleParseException(ParticleParseError.UnexpectedForm,
                    "Particle root is not a FORM PEFT.");
            }

            MutableIffNode versionForm = FirstContainerChild(root);
            if (versionForm == null)
            {
                throw new ParticleParseException(ParticleParseError.UnexpectedForm,
                    "FORM PEFT has no version sub-form.");
            }

            string version = versionForm.SubTypeId;
            if (!KnownEffectVersions.Contains(version))
            {
                // D-05: unrecognized root version → raw-preserve the whole sub-tree. No field walk,
                // no count read, no throw. The captured bytes re-emit verbatim.
                return new MutableParticleEffect(mutable, version, true, versionForm, null,
                    new List<MutableParticleEmitterGroup>());
            }

            // The group-count chunk is the FIRST leaf child of the version form (PTIM is a container,
            // so it does not interfere). Its first int32 is the group count.
            MutableIffNode groupCountLeaf = FirstLeafChild(versionForm);
            if (groupCountLeaf == null)
            {
                throw new ParticleParseException(ParticleParseError.UnexpectedForm,
                    "PEFT version " + version + " has no group-count chunk.");
            }

            int groupCount = ReadLeadingInt32(groupCountLeaf);
            List<MutableIffNode> groupNodes = ContainerChildrenWithSubType(versionForm, "EMGP");
            GuardCount(groupCount, groupNodes.Count, "emitter-group");

            var groups = new List<MutableParticleEmitterGroup>(groupCount);
            for (int i = 0; i < groupCount && i < groupNodes.Count; i++)
            {
                groups.Add(BuildGroup(groupNodes[i]));
            }

            return new MutableParticleEffect(mutable, version, false, versionForm, groupCountLeaf, groups);
        }

        private static MutableParticleEmitterGroup BuildGroup(MutableIffNode emgp)
        {
            MutableIffNode emitterCountLeaf = FirstLeafChild(emgp);
            int emitterCount = (emitterCountLeaf != null) ? ReadLeadingInt32(emitterCountLeaf) : 0;

            List<MutableIffNode> emtrNodes = ContainerChildrenWithSubType(emgp, "EMTR");
            GuardCount(emitterCount, emtrNodes.Count, "emitter");

            var emitters = new List<ParticleEmitterDescription>(emitterCount);
            for (int i = 0; i < emitterCount && i < emtrNodes.Count; i++)
            {
                emitters.Add(ParticleEmitterDescription.FromEmtrNode(emtrNodes[i]));
            }

            return new MutableParticleEmitterGroup(emgp, emitterCountLeaf, emitters);
        }

        /// <summary>
        /// Edits a raw leaf payload in place (the generic per-leaf edit seam — the editor's per-field
        /// edit composes this with a re-encoded leaf payload). Mutating the captured leaf dirties it +
        /// its ancestors so untouched leaves still re-emit verbatim. Refuses to edit a leaf that does
        /// not belong to this effect's captured tree.
        /// </summary>
        public void EditLeafPayload(MutableIffNode leaf, byte[] newPayload)
        {
            if (leaf == null) throw new ArgumentNullException("leaf");
            if (newPayload == null) throw new ArgumentNullException("newPayload");
            if (IsRawPreserved)
            {
                throw new InvalidOperationException(
                    "This effect is raw-preserved (unrecognized version) — typed editing is refused.");
            }
            leaf.SetPayload(newPayload);
            IsDirty = true;
        }

        /// <summary>
        /// Re-derives a machine-managed count leaf little-endian (D-04), preserving any trailing bytes
        /// (e.g. the 4 effect-level floats in PEFT 0002) that follow the leading int32 count. Mirrors
        /// <c>MutableObjectTemplate.RewriteCount</c> but keeps the leaf's tail intact.
        /// </summary>
        public void RewriteCount(MutableIffNode countLeaf, int newCount)
        {
            if (countLeaf == null) throw new ArgumentNullException("countLeaf");
            if (newCount < 0) throw new ArgumentOutOfRangeException("newCount");

            byte[] existing = countLeaf.GetPayloadCopy();
            byte[] rebuilt = new byte[existing.Length < 4 ? 4 : existing.Length];
            // Leading int32 little-endian count.
            rebuilt[0] = (byte)(newCount & 0xFF);
            rebuilt[1] = (byte)((newCount >> 8) & 0xFF);
            rebuilt[2] = (byte)((newCount >> 16) & 0xFF);
            rebuilt[3] = (byte)((newCount >> 24) & 0xFF);
            // Preserve the trailing bytes verbatim.
            for (int i = 4; i < existing.Length; i++) rebuilt[i] = existing[i];

            countLeaf.SetPayload(rebuilt);
            IsDirty = true;
        }

        /// <summary>Serializes to byte-exact output via <see cref="ParticleEffectWriter"/>.</summary>
        public byte[] Serialize()
        {
            return ParticleEffectWriter.Serialize(this);
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static int ReadLeadingInt32(MutableIffNode leaf)
        {
            byte[] payload = leaf.GetPayloadCopy();
            var cursor = new IffPayloadCursor(payload);
            return cursor.ReadInt32Le(); // throws DecoderException(Truncated) on a < 4-byte chunk
        }

        // Division-form / cap count guard (T-15-02): rejects a negative count, a count above the
        // absolute cap, OR a count that exceeds the number of matching child FORMs actually present
        // (a forged count can't conjure sub-forms, so this also bounds the read loop). Applied BEFORE
        // the loop allocates.
        private static void GuardCount(int count, int presentChildren, string what)
        {
            if (count < 0)
            {
                throw new ParticleParseException(ParticleParseError.NegativeCount,
                    what + " count is negative: " + count + ".");
            }
            if (count > MaxNodeCount)
            {
                throw new ParticleParseException(ParticleParseError.CountExceedsCap,
                    what + " count " + count + " exceeds the cap " + MaxNodeCount + ".");
            }
            if (count > presentChildren)
            {
                throw new ParticleParseException(ParticleParseError.CountExceedsCap,
                    what + " count " + count + " exceeds the " + presentChildren
                    + " matching sub-form(s) present.");
            }
        }

        private static MutableIffNode FirstContainerChild(MutableIffNode parent)
        {
            foreach (MutableIffNode child in parent.Children)
            {
                if (child.Kind == MutableIffNodeKind.Container) return child;
            }
            return null;
        }

        private static MutableIffNode FirstLeafChild(MutableIffNode parent)
        {
            foreach (MutableIffNode child in parent.Children)
            {
                if (child.Kind == MutableIffNodeKind.Leaf) return child;
            }
            return null;
        }

        private static List<MutableIffNode> ContainerChildrenWithSubType(MutableIffNode parent, string subType)
        {
            var result = new List<MutableIffNode>();
            foreach (MutableIffNode child in parent.Children)
            {
                if (child.Kind == MutableIffNodeKind.Container && child.SubTypeId == subType)
                {
                    result.Add(child);
                }
            }
            return result;
        }
    }
}
